#nullable enable
using System;
using System.Linq;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// A controlled test for "the meadow goes dark towards the horizon".
    ///
    /// The real play world under the real renderer and lighting, photographed from the low view
    /// where the darkness showed, one lever at a time. It exists because reasoning from the
    /// source had produced three confident diagnoses in a row — facet lighting, the cut-out
    /// fringe, a multisample mismatch — and the pictures refuted each within a minute.
    ///
    /// **What it found, 2026-09-16.** Ambient occlusion, the foliage clip threshold and the leaf
    /// texture's alpha import were each photographed and changed nothing. Switching the outline
    /// off cleared the field. Moving the outline earlier and the grass later did not: the ink
    /// was on the *ground*, because the shader's first-difference edge test fired on a plane seen
    /// at a grazing angle. The outline shader now measures the second difference of inverse
    /// depth, which is zero on any plane; and grass is drawn after the ink so that tufts are
    /// never outlined (<see cref="MaterialCache.FoliageQueue"/>).
    ///
    /// The conditions kept: the game as configured; the ink off, which the baseline must match
    /// on the ground; and the grass drawn as an opaque under the ink, which is the picture that
    /// justifies drawing it late.
    ///
    /// Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.MeadowCheck.Run</c>. It must run
    /// with graphics, as every picture-taking command here does.
    /// </summary>
    public static class MeadowCheck
    {
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";

        readonly struct Condition
        {
            public readonly string Name;
            public readonly bool Ink, OpaqueGrass;

            public Condition(string name, bool ink, bool opaqueGrass)
            {
                Name = name;
                Ink = ink;
                OpaqueGrass = opaqueGrass;
            }
        }

        [MenuItem("Odyssey/Presentation/Check the meadow at range")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            OutlineFeature? outline = data == null ? null : data.rendererFeatures.OfType<OutlineFeature>().FirstOrDefault();
            bool outlineWas = outline != null && outline.isActive;
            RenderPassEvent stageWas = outline != null ? outline.stage : RenderPassEvent.BeforeRenderingTransparents;

            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            ChunkRenderer? renderer = null;
            ModuleLibrary? library = null;
            Action<ScriptableRenderContext, Camera>? hook = null;

            try
            {
                if (outline == null) Debug.LogWarning("[Meadow] no outline feature found; the ink column is moot.");

                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var size = new GridSize(PlayScene.PlaySizeXZ, PlayScene.PlaySizeXZ, PlayScene.PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeBarren();
                var grid = new CellGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                int activeLayer = result.StartCell.Y;
                var slice = new SliceSettings();

                lightingRoot = new GameObject("MeadowRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                cameraObject = new GameObject("MeadowCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                var focus = new Vector3(
                    result.StartCell.X * CellMetrics.SizeXZ,
                    activeLayer * CellMetrics.SizeY,
                    result.StartCell.Z * CellMetrics.SizeXZ);

                var conditions = new[]
                {
                    new Condition("baseline", ink: true, opaqueGrass: false),
                    new Condition("noink", ink: false, opaqueGrass: false),
                    new Condition("opaque", ink: true, opaqueGrass: true),
                };

                foreach (Condition condition in conditions)
                {
                    outline?.SetActive(condition.Ink);
                    // Opaque grass under the ink means the ink has to run after everything, as it
                    // did before grass was drawn late; otherwise the condition tests nothing.
                    if (outline != null)
                        outline.stage = condition.OpaqueGrass ? RenderPassEvent.BeforeRenderingPostProcessing : stageWas;
                    MaterialCache.FoliageQueue = condition.OpaqueGrass
                        ? (int)RenderQueue.AlphaTest : MaterialCache.DefaultFoliageQueue;

                    // A renderer per condition: a material is cloned the first time a bucket asks
                    // for it, so anything that changes what a clone holds needs a fresh cache.
                    library = new ModuleLibrary(catalogue);
                    var chunks = new ChunkGrid(size);
                    var model = new Odyssey.Presentation.World.WorldRenderModel(size, chunks, library);
                    model.RefreshAll(grid, result.Natural!.Context.Edifices);
                    renderer = new ChunkRenderer(model);

                    ChunkRenderer active = renderer;
                    hook = (context, rendering) =>
                    {
                        if (rendering != camera) return;
                        active.ViewerPosition = rendering.transform.position;
                        active.Render(activeLayer, slice);
                    };
                    RenderPipelineManager.beginCameraRendering += hook;

                    // The owner's view: low and close, looking across the field. Then the board
                    // rim, and the play camera, so a fix for one can be checked against the others.
                    PlayScene.Shoot(camera, focus, 14f, 40f, $"Logs/meadow-{condition.Name}-low.png");
                    PlayScene.Shoot(camera, focus, 9f, 150f, $"Logs/meadow-{condition.Name}-horizon.png");
                    PlayScene.Shoot(camera, focus, 48f, 48f, $"Logs/meadow-{condition.Name}-play.png");

                    RenderPipelineManager.beginCameraRendering -= hook;
                    hook = null;
                    Debug.Log($"[Meadow] {condition.Name}: ink={condition.Ink} opaqueGrass={condition.OpaqueGrass} " +
                              $"— {renderer.DrawCalls} draw calls, {renderer.InstancesDrawn} instances");

                    renderer.Dispose();
                    renderer = null;
                    library.Dispose();
                    library = null;
                }

                Debug.Log("[Meadow] wrote Logs/meadow-<condition>-{low,horizon,play}.png for " +
                          string.Join(", ", conditions.Select(c => c.Name)));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Meadow] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                renderer?.Dispose();
                library?.Dispose();
                outline?.SetActive(outlineWas);
                if (outline != null) outline.stage = stageWas;
                MaterialCache.FoliageQueue = MaterialCache.DefaultFoliageQueue;
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }
    }
}
