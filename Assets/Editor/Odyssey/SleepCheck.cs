#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
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
    /// Photograph a colonist asleep, in a real bed, in each of the four postures.
    ///
    /// <para><b>Written the day the sleeper's own length turned out to be wrong.</b> A colonist was
    /// being laid down 0.38 m long because the figure director's "hip height" measures a bone that
    /// stands on the floor, so she reached a metre and a half past the head of the bed and lay
    /// inside the mattress (<c>docs/design/20-beds.md</c> §7b). Every part of that is now asserted —
    /// <c>SleepPoseTests</c> holds the span, <c>FigureBuildTests</c> holds the measurement, and
    /// <c>SleepProbe</c> prints the clearance — and none of it answers the question the owner
    /// actually has to answer, which is whether a colonist in a bed <i>reads as asleep</i>. That
    /// wants pictures. The same bargain <c>SwimCheck</c>, <c>ClimbCheck</c> and
    /// <c>GestureCheck</c> make, and this is built on their shape.</para>
    ///
    /// <para><b>In real beds, not with the pose forced over the grass.</b> The whole of the fault
    /// was the relation between a body and the bed under it, so a sheet that photographed the body
    /// alone would have shown a perfectly convincing sleeping shape throughout the two days the
    /// bug existed — which is exactly what happened to the arithmetic, and is the reason this
    /// exists at all. The scenario's own beds are real furniture now (§7a), so the colonists are
    /// pinned into them and the picture is the game's.</para>
    ///
    /// <para><b>One colonist per posture, chosen rather than hoped for.</b> Which posture a
    /// colonist takes is a hash of her pawn id, so a colony of four is not four postures — it is
    /// four draws from a hat. The sheet picks a colonist for each of the four, which is the only
    /// way a posture that is wrong can be guaranteed to appear.</para>
    ///
    /// <para><b>Two bearings each, and the second is the one that matters.</b> Side on is where the
    /// shape of a lying body is legible; the board's own 48° pitch is where the player will
    /// actually see it, and a pose that reads in profile and vanishes from above has solved
    /// nothing — the lesson <c>ClimbCheck</c> already wrote down. The ground sleeper is here for
    /// the same reason: a colonist who could not reach a bed is drawn by the same code with no bed
    /// to be measured against, and she is the one most likely to look wrong.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.SleepCheck.Run</c>. It needs a
    /// real graphics device, so not under <c>-nographics</c>.</para>
    /// </summary>
    public static class SleepCheck
    {
        [MenuItem("Odyssey/Presentation/Check the sleep")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        const float FrameSeconds = 1f / 60f;

        /// <summary>
        /// Enough colonists that every posture is among them. Four would be four draws from a hat
        /// of four; twelve is what <c>APostureIsStablePerColonistAndVariesBetweenThem</c> already
        /// walks to prove all four turn up.
        /// </summary>
        const int Colonists = 12;

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
                    grid, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns())
                    { Chunks = chunks };
                var support = new SupportSystem(grid, new SupportSolver(grid), chunks);
                var mirror = new GridMirrorContributor(grid, result.Edifices, model);
                var designations = new Odyssey.Sim.Designations.DesignationGrid(grid, result.Edifices);

                SimWorld world = new SimWorldBuilder()
                    .WithSeed(1u)
                    .WithSize(size)
                    .AddSnapshotContributor(mirror)
                    .AddColony(pawns, designations, support, nav, result.Placements, out _)
                    .Build();

                // Bare with beds, because this sheet is about beds. `Playtest` gives none on
                // purpose (§7a) and would photograph a field of people asleep in the grass.
                ScenarioDef scenario = ScenarioDef.Bare();
                scenario.colonists = Colonists;
                scenario.beds = SleepPose.Postures.Length + 1; // one spare, so the pairing cannot run short
                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, scenario);

                // The scenario raised real furniture, so the renderer has to be told: the beds went
                // in after RefreshAll and nothing has dirtied their chunks.
                model.RefreshAll(grid, result.Edifices);

                IReadOnlyList<int> beds = pawns.Items.Beds;
                if (beds.Count < SleepPose.Postures.Length)
                {
                    Debug.LogError($"[Sleep] the scenario raised {beds.Count} beds and this sheet " +
                                   $"wants {SleepPose.Postures.Length}. Try another seed.");
                    exitCode = 1;
                    return;
                }

                // One colonist per posture, and a bed each.
                var sleepers = new List<(Pawn pawn, int bed, string posture)>();
                var taken = new HashSet<int>();
                foreach (SleepPose.Posture posture in SleepPose.Postures)
                {
                    foreach (Pawn candidate in pawns.Pawns.All)
                    {
                        if (taken.Contains(candidate.Id.Value)) continue;
                        if (SleepPose.PostureFor(candidate.Id.Value).Name != posture.Name) continue;

                        taken.Add(candidate.Id.Value);
                        sleepers.Add((candidate, beds[sleepers.Count], posture.Name));
                        break;
                    }
                }

                if (sleepers.Count < SleepPose.Postures.Length)
                {
                    Debug.LogError($"[Sleep] only {sleepers.Count} of {SleepPose.Postures.Length} " +
                                   $"postures are represented among {Colonists} colonists.");
                    exitCode = 1;
                    return;
                }

                // And one who never reaches a bed, drawn by the same code with nothing to lie on.
                Pawn onTheGround = null!;
                foreach (Pawn candidate in pawns.Pawns.All)
                    if (!taken.Contains(candidate.Id.Value)) { onTheGround = candidate; break; }

                lightingRoot = new GameObject("SleepRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                figures = new PawnFigureDirector(catalogue, lightingRoot.transform, 0) { World = model };
                if (!figures.Enabled)
                    Debug.LogWarning("[Sleep] no live figures: no character art or no gait clips. " +
                                     "The pictures will show the baked meshes and prove nothing.");

                int movePerTick = ContentPack.Pawns().Movement.movePerTick;

                cameraObject = new GameObject("SleepCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                ChunkRenderer active = renderer;
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    active.Render(activeLayer, slice);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                figures.ForceSleep = 1f;

                // Thrown away, for the reason the swim's warm-up is: a character's material draws
                // flat yellow on the first frame it is touched.
                //
                // **Aimed at a sleeper, not at the origin.** The first cut framed the warm-up on
                // Vector3.zero, which is a corner of the board with nobody in it, so not one
                // figure was ever drawn during the warm-up and every picture on the sheet came out
                // flat yellow anyway. A warm-up that does not include the subject warms nothing.
                for (int warm = 0; warm < 24; warm++)
                {
                    Step(world, figures, activeLayer, slice, movePerTick, sleepers, onTheGround);
                    PlayScene.Shoot(camera, BedMiddle(model, size, sleepers[0].bed), 48f, 45f, 14f,
                        "Logs/sleep-warm.png");
                }

                Debug.Log($"[Sleep] tallest figure {figures.MeasuredStandingHeight:0.000} m. " +
                          $"A figure at {FigureBuild.FallbackHeight:0.000} m exactly is one whose " +
                          "mesh could not be measured, and the pictures below are of the fallback.");

                foreach ((Pawn pawn, int bed, string posture) in sleepers)
                {
                    Step(world, figures, activeLayer, slice, movePerTick, sleepers, onTheGround);

                    CellRef at = size.FromIndex(bed);
                    int facing = model.BedFacing(bed);
                    Vector3 middle = BedMiddle(model, size, bed);
                    float yaw = Directions.Yaw[facing];

                    string name = posture.Replace(", ", "-").Replace(" ", "-");
                    // **Far enough back to see both ends of the bed**, which is the whole question:
                    // a 4.6 m bed with a 2.5 m colonist on it needs the frame to contain the
                    // mattress, not the colonist. The first cut stood 8 m off at a 40-degree field
                    // of view and photographed a shoulder.
                    //
                    // Side on to the bed, which is across the body: the length of a lying figure
                    // and its relation to both ends of the mattress are only legible from there.
                    PlayScene.Shoot(camera, middle, 12f, yaw + 90f, 13f, $"Logs/sleep-{name}-side.png");
                    // Along the bed from the foot, where a limb over an edge shows.
                    PlayScene.Shoot(camera, middle, 16f, yaw + 180f, 12f, $"Logs/sleep-{name}-foot.png");
                    // And the bearing and pitch the board is actually played at.
                    PlayScene.Shoot(camera, middle, 48f, yaw + 45f, 14f, $"Logs/sleep-{name}-play.png");

                    Debug.Log($"[Sleep] Logs/sleep-{name}-*.png: colonist {pawn.Id.Value} " +
                              $"in the bed at {at}, facing {facing}.");
                }

                Step(world, figures, activeLayer, slice, movePerTick, sleepers, onTheGround);
                Vector3 ground = GroundRelief.Lift(CellMetrics.FloorCentre(size.FromIndex(onTheGround.Cell)));
                PlayScene.Shoot(camera, ground, 12f, 45f, 12f, "Logs/sleep-ground-side.png");
                PlayScene.Shoot(camera, ground, 48f, 45f, 14f, "Logs/sleep-ground-play.png");

                Debug.Log($"[Sleep] wrote Logs/sleep-*.png. {figures.SleepingFigures} figures were " +
                          "laid down on the last frame; nought means the pose did nothing whatever " +
                          "the pictures look like.");

                figures.ForceSleep = null;
            }
            catch (Exception error)
            {
                Debug.LogError($"[Sleep] {error}");
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

        /// <summary>
        /// The middle of a bed's mattress, which is what a picture of a sleeper should be framed
        /// on — not the colonist. A body is 2.5 m and the bed it has to fit is 4.6 m, so framing on
        /// the figure crops the very ends that the question is about.
        /// </summary>
        static Vector3 BedMiddle(WorldRenderModel model, GridSize size, int bed)
        {
            CellRef at = size.FromIndex(bed);
            int facing = model.BedFacing(bed);
            return GroundRelief.Lift(BedShape.Origin(at.X, at.Z, at.Y, facing))
                   + Vector3.up * BedShape.MattressTop;
        }

        /// <summary>
        /// One frame, with every sleeper held in her own bed.
        ///
        /// <para><b>Pinned every tick, for the reason the swimmer is.</b> A colonist with a full
        /// rest need has no reason to be in a bed and walks out of it, which is correct behaviour
        /// and would produce a sheet of empty furniture. Set before the tick, because the snapshot
        /// presentation reads is published during it.</para>
        /// </summary>
        static void Step(SimWorld world, PawnFigureDirector figures, int layer, SliceSettings slice,
            int movePerTick, List<(Pawn pawn, int bed, string posture)> sleepers, Pawn onTheGround)
        {
            for (int i = 0; i < sleepers.Count; i++)
            {
                sleepers[i].pawn.Cell = sleepers[i].bed;
                sleepers[i].pawn.ClearPath();
            }

            onTheGround.ClearPath();

            world.Tick();
            figures.Sync(world.Views.Current, layer, slice, 0f, movePerTick, FrameSeconds);
            figures.Evaluate(FrameSeconds);
        }
    }
}
