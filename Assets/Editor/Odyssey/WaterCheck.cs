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
    /// Photograph the water: a stream on the board the scene loads, and a wide river, from the
    /// pitch the game is played at and from close enough to see the surface.
    ///
    /// Water is the part of this feature no test can judge. That it generates, that it is a third
    /// of walking speed and that it never severs the map are all checkable and all checked; how it
    /// *looks* is not, and the shading is where most of the work went — ripples, a sun glint, a
    /// Fresnel-weighted reflection and a shore that dissolves against the depth of the bed behind
    /// it. All four are invisible to anything but an eye.
    ///
    /// Three pitches, chosen rather than swept, for the reason <c>ReliefCheck</c> gives. At 48
    /// degrees, the play camera's own, the water is seen from above and the answer is whether the
    /// bed reads through the shallows and not through the deep. At 20 the reflection takes over,
    /// because Fresnel is an angle, and that shot is the only one where the surface shading is
    /// really visible. The close shot is where the ripples are.
    ///
    /// A river is forced for the second set because the default map rarely has one — once in
    /// eight — and the wide, deep, ford-crossed case is exactly the one worth looking at.
    ///
    /// Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.WaterCheck.Run</c>. A real graphics
    /// device, so not under -nographics.
    /// </summary>
    public static class WaterCheck
    {
        [MenuItem("Odyssey/Presentation/Check the water")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        readonly struct Condition
        {
            public Condition(string name, bool river, uint seed)
            {
                Name = name; River = river; Seed = seed;
            }

            public readonly string Name;
            public readonly bool River;
            public readonly uint Seed;
        }

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
                var slice = new SliceSettings();

                lightingRoot = new GameObject("WaterRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                cameraObject = new GameObject("WaterCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                var conditions = new[]
                {
                    new Condition("stream", river: false, seed: 3u),
                    new Condition("river", river: true, seed: 8u),
                };

                foreach (Condition condition in conditions)
                {
                    var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                    gen.MakeWooded();
                    gen.riverChancePerMille = condition.River ? 1000 : 0;

                    var grid = new CellGrid(size);
                    MapGenOutcome result = MapGenerator.Generate(grid, condition.Seed, gen);
                    int activeLayer = result.StartCell.Y;

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

                    // Point the camera at the water rather than at the start, which the generator
                    // deliberately keeps dry: a picture of the place with no water in it would
                    // answer nothing.
                    Vector3 focus = LookAtTheWater(result.Natural!.Context, activeLayer);

                    PlayScene.Shoot(camera, focus, 48f, 70f, $"Logs/water-{condition.Name}-play.png");
                    PlayScene.Shoot(camera, focus, 20f, 70f, $"Logs/water-{condition.Name}-grazing.png");
                    PlayScene.Shoot(camera, focus, 34f, 22f, $"Logs/water-{condition.Name}-close.png");

                    // Lower and nearer than the camera rig will ever go, and deliberately so.
                    // Seams between tiles are an angle problem: a height difference between one
                    // tile and the next projects across the screen as the cotangent of the pitch,
                    // so a step invisible from above opens into a band you can see the riverbed
                    // through near the waterline. This is the shot that caught it, and the shot
                    // that has to keep proving it has not come back.
                    PlayScene.Shoot(camera, focus, 9f, 14f, $"Logs/water-{condition.Name}-waterline.png");

                    RenderPipelineManager.beginCameraRendering -= hook;
                    hook = null;

                    var report = result.Natural!.Report;
                    Debug.Log($"[Water] {condition.Name}: {report.WaterCells} cells " +
                              $"({report.ShallowWaterCells} shallow, {report.DeepWaterCells} deep, " +
                              $"{report.MarshCells} marsh, {report.Fords} fords) — " +
                              $"{renderer.DrawCalls} draw calls, {renderer.InstancesDrawn} instances");

                    renderer.Dispose();
                    renderer = null;
                    library.Dispose();
                    library = null;
                }

                Debug.Log("[Water] wrote Logs/water-{stream,river}-{play,grazing,close}.png");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Water] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                renderer?.Dispose();
                library?.Dispose();
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// The middle of the biggest body of water on the map, in world space. Biggest rather than
        /// nearest, because the shot wants the thing at its most characteristic: a river's deep
        /// channel says more about the shading than the tail of a brook does.
        /// </summary>
        static Vector3 LookAtTheWater(NaturalGenContext ctx, int layer)
        {
            int best = -1, bestCells = 0;
            foreach (WaterShape shape in ctx.WaterShapes)
            {
                if (shape.Cells <= bestCells) continue;
                bestCells = shape.Cells;
                best = shape.CentreColumn;
            }

            if (best < 0) best = ctx.Column(ctx.Size.SizeX / 2, ctx.Size.SizeZ / 2);

            int x = best % ctx.Size.SizeX, z = best / ctx.Size.SizeX;
            return new Vector3(x * CellMetrics.SizeXZ, layer * CellMetrics.SizeY, z * CellMetrics.SizeXZ);
        }
    }
}
