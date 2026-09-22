#nullable enable
using System;
using System.Linq;
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
    /// What the grass looks like, one lever at a time.
    ///
    /// <para><b>Why a tool and not a playtest.</b> The owner is the only person who can press
    /// Play and there are fifty-eight things waiting for them
    /// (<c>docs/plans/playtest-queue.md</c>). Everything about whether the grass is *right* — does
    /// the lean do anything, does the banding read, does a denser field turn to soup — is
    /// answerable from a photograph, and only "is it pleasant to look at for an hour" is not. This
    /// is the same argument <see cref="MeadowCheck"/> makes, and it is a separate tool from that
    /// one because that one exists to answer a different question (whether the ink darkens the
    /// far field) and its conditions should not be muddled with these.</para>
    ///
    /// <para>The control that earns its keep is <c>flat</c>: the grass with the camera lean
    /// switched off. A blade is a thin upright card and this camera looks down at 48°, so without
    /// the lean the field should read as grey fuzz. If <c>flat</c> and <c>shipped</c> look the
    /// same, the lean is doing nothing and the number is wrong.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.GrassCheck.Run</c>. It must
    /// run with graphics, as every picture-taking command here does.</para>
    /// </summary>
    public static class GrassCheck
    {
        readonly struct Condition
        {
            public readonly string Name;
            public readonly float FaceCamera, Banding;
            public readonly int Density;

            public Condition(string name, float faceCamera, float banding, int density)
            {
                Name = name;
                FaceCamera = faceCamera;
                Banding = banding;
                Density = density;
            }
        }

        /// <summary>
        /// Where the camera stands for each picture: a label, a pitch in degrees and a distance
        /// in metres, orbiting the start cell.
        ///
        /// <b>The play framing is kept although it currently renders nothing.</b> 48 degrees at
        /// 48 m is the angle the game is played at and the one <see cref="MeadowCheck"/> calls
        /// "the play camera", and in this harness it comes out a blank frame of sky. That is not
        /// this tool: MeadowCheck unmodified does the same, so does a condition with no grass at
        /// all, and <c>ReliefCheck</c> at the same 48 degrees is fine. Everything ruled out is in
        /// <c>docs/lessons.md</c>. The shot stays in the sheet because a blank frame is evidence
        /// and because it will start answering the moment somebody fixes the harness; the two
        /// grazing shots are what can be judged today.
        /// </summary>
        static readonly (string Label, float Pitch, float Distance)[] Framings =
        {
            ("low", 14f, 40f),
            ("closer", 14f, 18f),
            ("play", 48f, 48f),
        };

        [MenuItem("Odyssey/Presentation/Check the grass")]
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
                gen.MakeBarren();
                var grid = new CellGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                int activeLayer = result.StartCell.Y;
                var slice = new SliceSettings();

                lightingRoot = new GameObject("GrassRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                cameraObject = new GameObject("GrassCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;

                var focus = new Vector3(
                    result.StartCell.X * CellMetrics.SizeXZ,
                    activeLayer * CellMetrics.SizeY,
                    result.StartCell.Z * CellMetrics.SizeXZ);

                var conditions = new[]
                {
                    new Condition("bare", GrassLook.FaceCamera, 0f, 0),
                    new Condition("shipped", GrassLook.FaceCamera, 0f, 60),
                    new Condition("flat", 0f, 0f, 60),
                    new Condition("banded", GrassLook.FaceCamera, 1f, 60),
                    new Condition("dense", GrassLook.FaceCamera, 0f, 150),
                };

                foreach (Condition condition in conditions)
                {
                    GrassLook.Reset();
                    GrassLook.FaceCamera = condition.FaceCamera;
                    GrassLook.Banding = condition.Banding;

                    // A renderer per condition, for the reason MeadowCheck gives: a material is
                    // cloned the first time a bucket asks for it, so anything that changes what a
                    // clone holds needs a cache that has not built one yet.
                    library = new ModuleLibrary(catalogue);
                    var chunks = new ChunkGrid(size);
                    var model = new Odyssey.Presentation.World.WorldRenderModel(size, chunks, library);
                    model.RefreshAll(grid, result.Natural!.Context.Edifices);
                    renderer = new ChunkRenderer(model) { ScatterDensity = condition.Density };

                    ChunkRenderer active = renderer;
                    hook = (context, rendering) =>
                    {
                        if (rendering != camera) return;
                        active.ViewerPosition = rendering.transform.position;
                        active.Render(activeLayer, slice);
                    };
                    RenderPipelineManager.beginCameraRendering += hook;

                    foreach ((string label, float pitch, float distance) in Framings)
                    {
                        PlayScene.Shoot(camera, focus, pitch, distance,
                            $"Logs/grass-{condition.Name}-{label}.png");
                        Debug.Log($"[Grass] {condition.Name}/{label}: camera at " +
                                  $"{camera.transform.position} looking at {focus}");
                    }

                    RenderPipelineManager.beginCameraRendering -= hook;
                    hook = null;

                    // The cost, beside the picture, because the two are one judgement: a look that
                    // is worth 900 extra draw calls is not worth it. A meadow must cost instances.
                    Debug.Log($"[Grass] {condition.Name}: lean={condition.FaceCamera:0.00} " +
                              $"banding={condition.Banding:0.00} density={condition.Density} — " +
                              $"{renderer.DrawCalls} draw calls, {renderer.InstancesDrawn} instances");

                    renderer.Dispose();
                    renderer = null;
                    library.Dispose();
                    library = null;
                }

                Debug.Log("[Grass] wrote Logs/grass-<condition>-{close,play,range}.png for " +
                          string.Join(", ", conditions.Select(c => c.Name)));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Grass] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                renderer?.Dispose();
                library?.Dispose();
                GrassLook.Reset();
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }
    }
}
