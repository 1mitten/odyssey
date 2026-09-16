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
    /// Can a colonist who is chopping a tree be clicked, and does the cursor land on them?
    ///
    /// **Why this exists.** It was reported three times from playtests and diagnosed wrong twice,
    /// both times by reading code. The cause turned out to be that a working figure is stepped off
    /// its cell by <see cref="WorkStance"/> so the axe reaches the wood, while the click box and
    /// the selection bracket were both built from the pawn's cell — so the person on screen stood
    /// outside the box meant to select them. Reading could have found that; reading did not.
    ///
    /// So this measures it instead, on the real thing: the play generator, a real colonist who
    /// walked to a real marked tree under the job system, the game's own lighting. It aims a ray
    /// at the figure the way a player aims a mouse at what they can see, and tests the same ray
    /// against a box built the old way and the new way. The old way is kept deliberately: a test
    /// that only exercises the fix cannot tell you the fix was needed.
    ///
    /// Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.SelectCheck.Run</c>. It needs
    /// graphics, like every picture-taking command here.
    /// Output: <c>Logs/select-chopper.png</c> and the numbers in the log.
    /// </summary>
    public static class SelectCheck
    {
        const float FrameSeconds = 1f / 60f;

        /// <summary>The colonist cursor, as <c>OdysseyBootstrap.colonistCursor</c> ships it.</summary>
        static readonly Vector3 Cursor = new Vector3(1.15f, 2.7f, 1.15f);

        [MenuItem("Odyssey/Presentation/Check selecting a working colonist")]
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

                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, ScenarioDef.Playtest());
                ColonyScenario.DesignateTreesNear(designations, result.StartCell, 10);

                lightingRoot = new GameObject("SelectRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                figures = new PawnFigureDirector(catalogue, lightingRoot.transform, 0);
                if (!figures.Enabled)
                    Debug.LogWarning("[Select] no live figures: no character art or no gait clips. " +
                                     "Without a figure there is no displacement to measure and this proves nothing.");

                int movePerTick = PawnContent.Core().Movement.movePerTick;

                // Synced every tick, as SwingCheck does, because the walk in is part of what puts
                // the figure where it ends up.
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
                    Debug.LogError("[Select] nobody started work within 12,000 ticks; nothing to click.");
                    exitCode = 1;
                    return;
                }

                // Let the stance take its full weight. The step off the cell eases in over a
                // stroke, so a figure caught on the first working frame has barely moved and the
                // displacement being measured would read as nearly nothing.
                for (int frame = 0; frame < 120; frame++)
                {
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                }

                worker = FirstWorker(world.Views.Current);
                if (!worker.Working)
                {
                    Debug.LogError("[Select] the work ended before the stance settled; the tree came down.");
                    exitCode = 1;
                    return;
                }

                Vector3 pose = PawnPose.Of(worker, 0f, movePerTick, out _);
                bool hasFigure = figures.TryGetFeet(worker.Id, out Vector3 feet);
                if (!hasFigure) feet = pose;

                float displacement = Vector3.Distance(Flat(feet), Flat(pose));

                cameraObject = new GameObject("SelectCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                ChunkRenderer active = renderer;
                Vector3 bracketAt = feet + Vector3.up * (Cursor.y * 0.5f);
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    active.Render(activeLayer, slice);
                    // The cursor the game would draw for this selection, so the picture shows
                    // whether it hugs the colonist or sits on the cell beside her.
                    active.DrawSelectionBracket(bracketAt, Cursor, Color.white);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                // Frame her the way the board camera does, then aim at the chest of the figure on
                // screen — which is what a player aims a mouse at.
                PlayScene.Shoot(camera, feet + Vector3.up * 1.0f, 14f, 45f, 9f, "Logs/select-chopper.png");

                Vector3 aimAt = feet + Vector3.up * 1.35f;
                Vector3 screen = camera.WorldToScreenPoint(aimAt);
                Ray ray = camera.ScreenPointToRay(screen);

                bool pickedCell = SlicePicker.Pick(ray, model, activeLayer, slice, out CellRef cell);
                bool hitsAtPose = Hits(ray, pose);
                bool hitsAtFigure = Hits(ray, feet);

                Debug.Log($"[Select] worker {worker.Id.Value} in cell {worker.Cell.X},{worker.Cell.Z},{worker.Cell.Y}; " +
                          $"figure resolved: {hasFigure}. Pose feet {pose}, figure feet {feet}, " +
                          $"displaced {displacement:0.000} m against a click box reaching {Cursor.x * 0.5f:0.000} m.");
                Debug.Log($"[Select] ray aimed at the figure's chest: " +
                          $"box built from the pose hits = {hitsAtPose} (the bug), " +
                          $"box built from the figure hits = {hitsAtFigure} (the fix). " +
                          $"SlicePicker returned a cell: {pickedCell}" +
                          (pickedCell ? $" at {cell.X},{cell.Z},{cell.Y}" : ""));

                if (!hitsAtFigure)
                {
                    Debug.LogError("[Select] the fix does not hold: a ray aimed at the figure misses a box " +
                                   "built at the figure. Something other than the horizontal stand-off is wrong.");
                    exitCode = 1;
                }
                else if (!hitsAtPose && displacement > Cursor.x * 0.5f)
                {
                    Debug.Log("[Select] confirmed end to end: the old box misses the colonist the player " +
                              "can see, the new box hits her, and the picture shows where the cursor lands.");
                }
                else if (hitsAtPose)
                {
                    Debug.LogWarning($"[Select] the old box also hits at this moment of the stroke " +
                                     $"({displacement:0.000} m displaced), so this frame would not have " +
                                     "reproduced the report. The fix is still the right one; the picture " +
                                     "is the thing to read.");
                }
            }
            catch (Exception error)
            {
                Debug.LogError($"[Select] {error}");
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

        /// <summary>The same box the pick builds, centred on a given pair of feet.</summary>
        static bool Hits(Ray ray, Vector3 feet)
        {
            var bounds = new Bounds(feet + Vector3.up * (Cursor.y * 0.5f), Cursor);
            return bounds.IntersectRay(ray);
        }

        static PawnView FirstWorker(WorldSnapshot snapshot)
        {
            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++)
                if (pawns[i].Working) return pawns[i];
            return default;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
