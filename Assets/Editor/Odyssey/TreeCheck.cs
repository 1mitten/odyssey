#nullable enable
using System;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Photograph the woodland, three ways, so that somebody can judge whether a coloured wood is
    /// an improvement rather than taking a table of hex codes on trust.
    ///
    /// <para><b>Three conditions, because a single picture of the new wood cannot say what changed
    /// it.</b> <c>pack</c> draws the trees exactly as the game drew them before this existed,
    /// through the pack's own material. <c>plain</c> draws them through <c>Odyssey/Tree</c> with
    /// the repaint strength at zero, which is the fidelity control: if <c>plain</c> and <c>pack</c>
    /// differ then our shader is not drawing a Synty tree the way Synty's does, and that is a
    /// different bug from a palette somebody dislikes. <c>themed</c> is the game.</para>
    ///
    /// <para>It also prints the draw calls and instances of each condition off the real wooded
    /// board, which is the performance half of the feature measured where it actually lands —
    /// <c>TreeBucketTests</c> measures the bucket count structurally, and this measures what the
    /// renderer submits on the map the scene loads.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.TreeCheck.Run</c>. It must run
    /// with graphics, as every picture-taking command here does.</para>
    /// </summary>
    public static class TreeCheck
    {
        readonly struct Condition
        {
            public readonly string Name;
            public readonly bool Stands;
            public readonly bool Recolour;
            public readonly float Strength;
            public readonly float? Normals;

            public Condition(string name, bool stands, bool recolour, float strength, float? normals = null)
            {
                Name = name;
                Stands = stands;
                Recolour = recolour;
                Strength = strength;
                Normals = normals;
            }
        }

        [MenuItem("Odyssey/Presentation/Check the wood's colours")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            ChunkRenderer? renderer = null;
            ModuleLibrary? library = null;
            Action<ScriptableRenderContext, Camera>? hook = null;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var size = new GridSize(PlayScene.PlaySizeXZ, PlayScene.PlaySizeXZ, PlayScene.PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);

                // The board the scene actually loads, not the bare test baseline: the question is
                // what a wood looks like, and MakeBarren has no trees in it at all.
                gen.MakeWooded();
                var grid = new CellGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                int activeLayer = result.StartCell.Y;
                var slice = new SliceSettings();

                lightingRoot = new GameObject("TreeRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                cameraObject = new GameObject("TreeCamera");
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
                    // No stands in the first two: switching off the materials alone still leaves
                    // the mesher splitting a chunk's trees by stand, so the draw-call column would
                    // have been the feature measured against itself. The first run of this sheet
                    // reported 1815 calls three times over for exactly that reason.
                    new Condition("pack", stands: false, recolour: false, strength: 1f),
                    new Condition("plain", stands: false, recolour: true, strength: 0f),
                    // One bit of information about the tenth of a stop between "plain" and
                    // "pack": is it the normal map the two shaders disagree about, or the light?
                    new Condition("flat", stands: false, recolour: true, strength: 0f, normals: 0f),
                    new Condition("themed", stands: true, recolour: true, strength: 1f),
                };

                foreach (Condition condition in conditions)
                {
                    TreeLook.Stands = condition.Stands;
                    TreeMaterials.Enabled = condition.Recolour;
                    TreeMaterials.RemapStrength = condition.Strength;
                    TreeMaterials.NormalAmountOverride = condition.Normals;

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

                    // The play camera first, because that is the only view anybody judges the
                    // board from; then a low one across the wood, where a stand boundary shows;
                    // then close enough to see a tree's bark against its own canopy, which is the
                    // thing four colours exist for and the thing a board shot cannot show.
                    //
                    // The close span is 40 m and not the 14 m it was first written at. At 14 the
                    // frame was one trunk from edge to edge: it did show that the bark had been
                    // repainted, and nothing else at all — no canopy, no second tree, none of the
                    // four colours against each other.
                    PlayScene.Shoot(camera, focus, 48f, 48f, $"Logs/tree-{condition.Name}-play.png");
                    PlayScene.Shoot(camera, focus, 12f, 90f, $"Logs/tree-{condition.Name}-wood.png");
                    PlayScene.Shoot(camera, focus, 18f, 40f, $"Logs/tree-{condition.Name}-close.png");

                    RenderPipelineManager.beginCameraRendering -= hook;
                    hook = null;

                    Debug.Log($"[Tree] {condition.Name}: stands={condition.Stands} " +
                              $"recolour={condition.Recolour} " +
                              $"strength={condition.Strength} — {renderer.DrawCalls} draw calls, " +
                              $"{renderer.InstancesDrawn} instances, " +
                              $"{renderer.MaterialCount} tinted materials, " +
                              $"{renderer.Skirt.DrawCalls} surround draw calls");

                    renderer.Dispose();
                    renderer = null;
                    library.Dispose();
                    library = null;
                }

                Debug.Log("[Tree] wrote Logs/tree-{pack,plain,themed}-{play,wood,close}.png");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Tree] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                renderer?.Dispose();
                library?.Dispose();
                TreeLook.Stands = true;
                TreeMaterials.Enabled = true;
                TreeMaterials.RemapStrength = 1f;
                TreeMaterials.NormalAmountOverride = null;
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }
    }
}
