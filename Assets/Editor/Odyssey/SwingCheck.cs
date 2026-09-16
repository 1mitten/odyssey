#nullable enable
using System;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Photograph one axe stroke, frame by frame, so it can be judged by eye.
    ///
    /// The swing is invented rather than animated — there is no work clip in any pack we own, so
    /// <see cref="WorkSwing"/> computes the arm and torso angles and the director lays them over
    /// the idle (design 06 §6a). Every number in it was chosen by reasoning about what an axe
    /// does, and not one of them can be confirmed that way: an arm that passes through the chest,
    /// an axe held by the blade, a stroke that lands short of the trunk and a shoulder that pops
    /// on the first frame all read as perfectly sensible code. This is the loop that closes that
    /// gap, and the numbers it exists to tune are <c>AxeGripOffset</c>, <c>AxeGripEuler</c> and
    /// the six angle constants in <see cref="WorkSwing"/>.
    ///
    /// It photographs the real thing: the play world, the real generator, a real colonist who
    /// walked to a real marked tree under the job system, and the game's own lighting. Nothing
    /// here is posed by hand, because a pose set up by hand would prove only that the pose can be
    /// set up by hand.
    ///
    /// Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.SwingCheck.Run</c>. It must run
    /// with graphics, like every picture-taking command here. Output: <c>Logs/swing-N.png</c>,
    /// one per sample across a single stroke, plus a wide shot for scale.
    /// </summary>
    public static class SwingCheck
    {
        /// <summary>Frames per second the harness pretends to run at. There is no player loop here.</summary>
        const float FrameSeconds = 1f / 60f;

        /// <summary>How many pictures to take across one stroke.</summary>
        const int Samples = 9;

        [MenuItem("Odyssey/Presentation/Check the axe swing")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            ChunkRenderer? renderer = null;
            ModuleLibrary? library = null;
            PawnFigureDirector? figures = null;
            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            Action<ScriptableRenderContext, Camera>? hook = null;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var size = new GridSize(PlayScene.PlaySizeXZ, PlayScene.PlaySizeXZ, PlayScene.PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeWooded();
                var grid = new CellGrid(size);
                var chunks = new ChunkGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                int activeLayer = result.StartCell.Y;
                var slice = new SliceSettings();

                library = new ModuleLibrary(catalogue);
                var model = new WorldRenderModel(size, chunks, library);
                model.RefreshAll(grid, result.Natural!.Context.Edifices);
                renderer = new ChunkRenderer(model);

                var nav = new NavGraph(grid);
                nav.Rebuild();
                var pawns = new PawnContext(
                    grid, nav, new PathService(new PathFinder(nav)), PawnContent.Core()) { Chunks = chunks };
                var support = new SupportSystem(grid, new SupportSolver(grid), chunks);
                var mirror = new GridMirrorContributor(grid, result.Edifices, model);
                var designations = new Odyssey.Sim.Designations.DesignationGrid(grid, result.Edifices);

                SimWorld world = new SimWorldBuilder()
                    .WithSeed(1u)
                    .WithSize(size)
                    .AddSnapshotContributor(mirror)
                    .AddColony(pawns, designations, support, nav)
                    .Build();

                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, 5);
                ColonyScenario.DesignateTreesNear(designations, result.StartCell, 10);

                lightingRoot = new GameObject("SwingRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                figures = new PawnFigureDirector(catalogue, lightingRoot.transform, 0);
                if (!figures.Enabled)
                    Debug.LogWarning("[Swing] no live figures: no character art or no gait clips. " +
                                     "The pictures will show the baked meshes and prove nothing.");

                int movePerTick = PawnContent.Core().Movement.movePerTick;

                // Run the colony until somebody has actually walked to a tree and started. The
                // director is synced throughout rather than only at the end, because a figure's
                // speed is measured from how far it moved since the last frame: a director handed
                // a world that has already run for a thousand ticks sees five people teleport in
                // and stand still, and the walk-in is half of what these pictures are checking.
                PawnView worker = default;
                for (int tick = 0; tick < 12_000 && !worker.Working; tick++)
                {
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                    worker = FirstWorker(world.Views.Current);
                }

                if (!worker.Working)
                {
                    Debug.LogError("[Swing] nobody started work within 12,000 ticks; nothing to photograph.");
                    exitCode = 1;
                    return;
                }

                cameraObject = new GameObject("SwingCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                ChunkRenderer active = renderer;
                PawnFigureDirector drawn = figures;
                WorldSnapshot Current() => world.Views.Current;
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    active.Render(activeLayer, slice);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                // One stroke, sampled evenly. The frames between samples still run, so what is
                // photographed is a continuous swing rather than nine independent poses.
                int strokeFrames = Mathf.CeilToInt(WorkSwing.StrokeSeconds / FrameSeconds);
                int every = Mathf.Max(1, strokeFrames / (Samples - 1));

                for (int sample = 0; sample < Samples; sample++)
                {
                    for (int frame = 0; frame < every; frame++)
                    {
                        world.Tick();
                        drawn.Sync(Current(), activeLayer, slice, 0f, movePerTick, FrameSeconds);
                        drawn.Evaluate(FrameSeconds);
                    }

                    PawnView now = FirstWorker(Current());
                    if (!now.Working)
                    {
                        Debug.LogWarning($"[Swing] the work ended after {sample} samples; the tree came down.");
                        break;
                    }

                    // Close and low, because what is being judged is a hand, a haft and a
                    // shoulder. The board camera cannot settle any of those.
                    PlayScene.Shoot(camera, Waist(now), 12f, 5.5f, $"Logs/swing-{sample}.png");
                }

                PawnView last = FirstWorker(Current());
                if (last.Working)
                    PlayScene.Shoot(camera, Waist(last), 30f, 22f, "Logs/swing-wide.png");

                Debug.Log($"[Swing] wrote Logs/swing-0..{Samples - 1}.png and Logs/swing-wide.png; " +
                          $"worker {worker.Id} at {worker.Cell} swinging at {worker.WorkCell}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Swing] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                figures?.Dispose();
                renderer?.Dispose();
                library?.Dispose();
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>The first pawn in the snapshot that is working, or a default view.</summary>
        static PawnView FirstWorker(WorldSnapshot snapshot)
        {
            foreach (PawnView pawn in snapshot.Pawns)
                if (pawn.Working) return pawn;
            return default;
        }

        /// <summary>
        /// Roughly where a colonist's hands are, which is what the camera should be looking at.
        ///
        /// Framed on the floor centre the figure's head leaves the top of the picture, and the
        /// axe at the top of its arc leaves it entirely — which is the half of the stroke most
        /// worth seeing.
        /// </summary>
        static Vector3 Waist(in PawnView pawn) =>
            CellMetrics.FloorCentre(pawn.Cell) + Vector3.up * 1.3f;
    }
}
