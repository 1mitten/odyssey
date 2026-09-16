#nullable enable
using System;
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

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Photograph the ground with its relief off and on, at the pitches that matter.
    ///
    /// The whole of this feature is a judgement about how something looks, and reasoning about a
    /// renderer from its source is guesswork — so it needs an instrument, in the same way the axe
    /// swing needed <c>SwingCheck</c> and the grass needed <c>MeadowCheck</c>. The thing being
    /// judged here is whether the board still reads as a carpet of blocks, which no test can
    /// answer.
    ///
    /// The pitches are chosen rather than swept, because the camera rig makes them mean different
    /// things. At the default 48 degrees the horizon is not in frame at all and only ground between
    /// roughly 50 m and 220 m is visible, so that shot answers "does the board itself read as
    /// land". At 20 degrees, the shallowest the rig allows, the horizon sits at the top of the
    /// frame and the surround is most of the picture, so that shot answers "is there a landscape
    /// out there". A single pitch would have answered one and hidden the other.
    ///
    /// Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.ReliefCheck.Run</c>. It must run
    /// with a real graphics device, so not under -nographics.
    /// </summary>
    public static class ReliefCheck
    {
        [MenuItem("Odyssey/Presentation/Check the ground relief")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        readonly struct Condition
        {
            public Condition(string name, float amplitude)
            {
                Name = name;
                Amplitude = amplitude;
            }

            public readonly string Name;
            public readonly float Amplitude;
        }

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            ChunkRenderer? renderer = null;
            ModuleLibrary? library = null;
            Action<ScriptableRenderContext, Camera>? hook = null;

            float amplitudeWas = GroundRelief.Amplitude;
            float periodWas = GroundRelief.Period;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var size = new GridSize(PlayScene.PlaySizeXZ, PlayScene.PlaySizeXZ, PlayScene.PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);

                // The board the scene actually loads, trees and all: relief has to be judged
                // against the wood standing on it, not against a bare plane, because it is the
                // trees that show whether anything is floating.
                gen.MakeWooded();

                var grid = new CellGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                int activeLayer = result.StartCell.Y;
                var slice = new SliceSettings();

                lightingRoot = new GameObject("ReliefRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                cameraObject = new GameObject("ReliefCamera");
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
                    new Condition("flat", 0f),
                    new Condition("relief", GroundRelief.BoardAmplitude),
                };

                foreach (Condition condition in conditions)
                {
                    GroundRelief.Amplitude = condition.Amplitude;
                    GroundRelief.Period = 150f;

                    // A renderer per condition: the relief is baked into the instance matrices at
                    // mesh time, so a renderer built under one amplitude keeps it.
                    library = new ModuleLibrary(catalogue);
                    var chunks = new ChunkGrid(size);
                    var model = new Odyssey.Presentation.World.WorldRenderModel(size, chunks, library);
                    model.RefreshAll(grid, result.Natural!.Context.Edifices);
                    renderer = new ChunkRenderer(model);
                    renderer.Skirt.Enabled = true;
                    renderer.Skirt.Build();

                    ChunkRenderer active = renderer;
                    hook = (context, rendering) =>
                    {
                        if (rendering != camera) return;
                        active.ViewerPosition = rendering.transform.position;
                        active.Render(activeLayer, slice);
                    };
                    RenderPipelineManager.beginCameraRendering += hook;

                    // Close and low: is a colonist standing on ground, or on a table?
                    PlayScene.Shoot(camera, focus, 14f, 40f, $"Logs/relief-{condition.Name}-low.png");
                    // The play camera's own pitch: the view the game is actually judged in.
                    PlayScene.Shoot(camera, focus, 48f, 160f, $"Logs/relief-{condition.Name}-play.png");
                    // The shallowest the rig allows, where the horizon and the surround appear.
                    PlayScene.Shoot(camera, focus, 20f, 160f, $"Logs/relief-{condition.Name}-horizon.png");

                    RenderPipelineManager.beginCameraRendering -= hook;
                    hook = null;
                    Debug.Log($"[Relief] {condition.Name}: amplitude {condition.Amplitude} m — " +
                              $"{renderer.DrawCalls} draw calls, {renderer.InstancesDrawn} instances");

                    renderer.Dispose();
                    renderer = null;
                    library.Dispose();
                    library = null;
                }

                Debug.Log("[Relief] wrote Logs/relief-{flat,relief}-{low,play,horizon}.png");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Relief] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                renderer?.Dispose();
                library?.Dispose();
                GroundRelief.Amplitude = amplitudeWas;
                GroundRelief.Period = periodWas;
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }
    }
}
