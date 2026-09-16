#nullable enable
using System;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
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
    /// The day, photographed hour by hour.
    ///
    /// <para>The light is the one thing here that no test can judge. <c>DaylightTests</c> can hold
    /// that noon is blue and that night is dark without being black, because those are claims
    /// about numbers; it cannot say whether dawn looks like dawn. So the table gets a contact
    /// sheet, in the same spirit as the axe swing's and the climb's, and the owner reads the
    /// pictures.</para>
    ///
    /// <para><b>Shot at two pitches</b>, because the cycle is two different features depending on
    /// where you look. At the board pitch the day is shadows swinging across the ground and the
    /// colour of the grass; at the low pitch it is the sky, which is where the blue and the orange
    /// actually live and which the default view never shows at all.</para>
    ///
    /// <para>The wooded board rather than the barren one: bare ground at night is a flat dark
    /// rectangle and says nothing, where trees give the light something to model and shadows
    /// something to be thrown by.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.DaylightCheck.Run</c>.</para>
    /// </summary>
    public static class DaylightCheck
    {
        /// <summary>
        /// The hours worth a picture. Deliberately a mix of the table's own keys and hours between
        /// them, so the sheet shows the interpolation and not only the authored states — a crease
        /// at a key would be invisible in a sheet that only ever photographed keys.
        /// </summary>
        static readonly float[] Hours = { 2f, 5.5f, 7f, 9f, 12f, 16f, 19f, 20.5f };

        [MenuItem("Odyssey/Presentation/Check the daylight")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            ChunkRenderer? renderer = null;
            DaylightDirector? daylight = null;
            Action<ScriptableRenderContext, Camera>? hook = null;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var size = new GridSize(PlayScene.PlaySizeXZ, PlayScene.PlaySizeXZ, PlayScene.PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeWooded();
                var grid = new CellGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                int activeLayer = result.StartCell.Y;
                var slice = new SliceSettings();

                lightingRoot = new GameObject("DaylightRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                Light? sun = lightingRoot.GetComponentInChildren<Light>();
                if (sun == null) throw new InvalidOperationException("the lighting rig built no sun");

                // The director the game itself runs, driven by hand. A sheet that set the light
                // some other way would be a picture of this tool rather than of the game.
                daylight = new DaylightDirector(sun, RenderSettings.skybox);

                var library = new ModuleLibrary(catalogue);
                var chunks = new ChunkGrid(size);
                var model = new WorldRenderModel(size, chunks, library);
                model.RefreshAll(grid, result.Natural!.Context.Edifices);
                renderer = new ChunkRenderer(model);

                cameraObject = new GameObject("DaylightCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;

                // Submitted from inside the render, as every other sheet here does it. A world
                // drawn before the shutter opens is a world that is not in the picture.
                ChunkRenderer active = renderer;
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    active.Render(activeLayer, slice);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                var focus = new Vector3(
                    result.StartCell.X * CellMetrics.SizeXZ,
                    activeLayer * CellMetrics.SizeY,
                    result.StartCell.Z * CellMetrics.SizeXZ);

                foreach (float hour in Hours)
                {
                    daylight.ApplyHour(hour);
                    string label = hour.ToString("00.0").Replace('.', 'h');
                    PlayScene.Shoot(camera, focus, 48f, 120f, $"Logs/daylight-{label}-board.png");
                    PlayScene.Shoot(camera, focus, 14f, 40f, $"Logs/daylight-{label}-low.png");
                }

                Debug.Log($"[Daylight] {Hours.Length * 2} pictures in Logs/daylight-*.png; " +
                          $"{daylight.ProbeUpdates} probe updates.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Daylight] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                daylight?.Dispose();
                renderer?.Dispose();
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }
    }
}
