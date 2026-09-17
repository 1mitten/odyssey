#nullable enable
using System;
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
    /// Photograph a colonist swimming, through one stroke.
    ///
    /// <para><b>The pose is the only part of this nobody can check by arithmetic.</b> That the
    /// figure floats at the waterline, that the rise blends across the step, that it never sits
    /// above the bank beside it and that the gait stops walking are all measured in
    /// <c>WaterLineTests</c>. Whether a computed front crawl reads as swimming — rather than as a
    /// person lying down in a river — is a judgement, and it wants pictures. The same bargain
    /// <c>ClimbCheck</c> and <c>GestureCheck</c> make, and this is built on their shape.</para>
    ///
    /// <para><b>The weight is forced rather than arranged</b>, through
    /// <c>PawnFigureDirector.ForceSwim</c>. Walking a real colonist into a real stream at the
    /// moment a camera is pointed at it is a lot of scaffolding to photograph a shape, and the
    /// forced weight is the whole of what the water does to a figure — so the picture is the same
    /// picture. The board is the wooded meadow all the same, so the light and the ground are the
    /// game's.</para>
    ///
    /// <para><b>Both a side-on set and a board-pitch shot.</b> A stroke is a motion in the sagittal
    /// plane and side on is where it is visible; but a pose that reads in profile and vanishes at
    /// the pitch the game is played at has solved nothing, which is the lesson <c>ClimbCheck</c>
    /// already wrote down. The part-weight shots are the water's edge — the frames where a
    /// colonist is half in — and they are the ones most likely to look wrong.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.SwimCheck.Run</c>. It needs a
    /// real graphics device, so not under <c>-nographics</c>.</para>
    /// </summary>
    public static class SwimCheck
    {
        [MenuItem("Odyssey/Presentation/Check the swim")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        const float FrameSeconds = 1f / 60f;

        /// <summary>
        /// Where in the stroke to stop. Eight, evenly, so the sheet shows the whole cycle and a
        /// pose that is only wrong at one end of it cannot hide between two pictures.
        /// </summary>
        static readonly float[] Phases = { 0f, 0.125f, 0.25f, 0.375f, 0.5f, 0.625f, 0.75f, 0.875f };

        /// <summary>
        /// How far into the water to photograph, for the edge cases. A colonist at the bank spends
        /// a step at each of these and they are where a blended pose goes wrong.
        /// </summary>
        static readonly float[] Weights = { 0.25f, 0.5f, 0.75f };

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

                // **Photographed in real water, not on the grass with the weight forced on.** The
                // first sheet did the latter and it answered half the question: it showed the pose
                // is a swim shape, and it could not show how that shape sits against the waterline,
                // which is the whole of what the owner reported. A prone figure floating over a
                // meadow also reads as a body rather than as a swimmer, which is a judgement the
                // picture was inviting and had no business inviting.
                if (!WaterDepthCheck.TryFindShallowWater(grid, size, result.StartCell.Y, out CellRef wet))
                {
                    Debug.LogError("[Swim] this board has no shallow water to photograph in. " +
                                   "Try another seed.");
                    exitCode = 1;
                    return;
                }

                int activeLayer = wet.Y;
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

                // Bare, for the reason the climb's sheet is bare: a working figure is posed by its
                // stroke and never reaches the swim branch at all.
                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, ScenarioDef.Bare());

                // And one more, standing in the stream. Spawned rather than walked there: a
                // colonist given a destination across water would take a route round it, which is
                // the correct behaviour and the wrong photograph.
                Pawn swimmer = pawns.Pawns.Spawn(size.Index(wet.X, wet.Z, wet.Y));

                lightingRoot = new GameObject("SwimRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                figures = new PawnFigureDirector(catalogue, lightingRoot.transform, 0) { World = model };
                if (!figures.Enabled)
                    Debug.LogWarning("[Swim] no live figures: no character art or no gait clips. " +
                                     "The pictures will show the baked meshes and prove nothing.");

                int movePerTick = ContentPack.Pawns().Movement.movePerTick;

                int wetIndex = size.Index(wet.X, wet.Z, wet.Y);

                for (int tick = 0; tick < 400; tick++)
                    Step(world, figures, activeLayer, slice, movePerTick, swimmer, wetIndex);

                // The swimmer specifically, not whoever the director happens to be drawing first:
                // the other five are on dry land and would make a sheet of people standing about.
                PawnView subject = ViewOf(world.Views.Current, swimmer.Id, figures);
                if (subject.Id.Value == 0)
                {
                    Debug.LogError("[Swim] the colonist placed in the water is not being drawn; " +
                                   "nothing to photograph.");
                    exitCode = 1;
                    return;
                }

                Debug.Log($"[Swim] colonist {subject.Id} is standing in shallow water at {subject.Cell}; " +
                          $"the water is {WaterLine.SurfaceAbove(model, wet):0.00} m over the bed and a " +
                          $"floating figure sits {WaterLine.FloatRise(model, wet):0.00} m above it.");

                cameraObject = new GameObject("SwimCamera");
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

                // No forced weight: the colonist is in real water, so WaterLine drives it and the
                // pictures show what the game will do rather than what the harness asked for.
                figures.HeldSwimPhase = 0f;

                // Thrown away, for the reason the climb's warm-up is: a character's material draws
                // flat yellow on the first frame it is touched.
                for (int warm = 0; warm < 16; warm++)
                {
                    Step(world, figures, activeLayer, slice, movePerTick, swimmer, wetIndex);
                    figures.TryGetFacing(subject.Id, out float warmFacing);
                    PlayScene.Shoot(camera, Middle(figures, subject.Id), 6f, SideOn(warmFacing), 7f,
                        "Logs/swim-warm.png");
                }

                foreach (float phase in Phases)
                {
                    figures.HeldSwimPhase = phase;
                    Step(world, figures, activeLayer, slice, movePerTick, swimmer, wetIndex);

                    string path = $"Logs/swim-{phase:0.000}.png";
                    figures.TryGetFacing(subject.Id, out float facing);
                    PlayScene.Shoot(camera, Middle(figures, subject.Id), 6f, SideOn(facing), 7f, path);
                    Debug.Log($"[Swim] {path}: {figures.SwimmingFigures} swimming, " +
                              $"pitch {figures.MeasuredSwimPitch:0.0}°");
                }

                // The water's edge: half a colonist's worth of swimmer. A pose that only works at
                // full weight is a pose that snaps on at the bank.
                foreach (float weight in Weights)
                {
                    figures.ForceSwim = weight;
                    figures.HeldSwimPhase = 0.25f;
                    Step(world, figures, activeLayer, slice, movePerTick, swimmer, wetIndex);

                    figures.TryGetFacing(subject.Id, out float facing);
                    PlayScene.Shoot(camera, Middle(figures, subject.Id), 6f, SideOn(facing), 7f,
                        $"Logs/swim-weight-{Mathf.RoundToInt(weight * 100f):D2}.png");
                    Debug.Log($"[Swim] weight {weight:0.00}: pitch {figures.MeasuredSwimPitch:0.0}°");
                }

                // And from the bearing a player actually sees the board from.
                figures.ForceSwim = null;
                figures.HeldSwimPhase = 0.25f;
                Step(world, figures, activeLayer, slice, movePerTick, swimmer, wetIndex);
                PlayScene.Shoot(camera, Middle(figures, subject.Id), 22f, 45f, 10f, "Logs/swim-board.png");
                PlayScene.Shoot(camera, Middle(figures, subject.Id), 48f, 45f, 12f, "Logs/swim-play.png");

                figures.ForceSwim = null;
                figures.HeldSwimPhase = null;

                Debug.Log($"[Swim] wrote Logs/swim-*.png for colonist {subject.Id} at {subject.Cell}. " +
                          "A pitch of zero at every phase means no hips were bound and the pose did " +
                          "nothing, whatever the pictures look like.");
            }
            catch (Exception error)
            {
                Debug.LogError($"[Swim] {error}");
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
        /// One frame, with the swimmer held in its stream.
        ///
        /// <para><b>Pinned every tick, and it has to be.</b> A colonist with nothing to do wanders,
        /// and a wandering colonist walks out of the water within a few seconds of game time —
        /// which is correct behaviour and would have produced four hundred ticks of warm-up
        /// followed by a sheet of somebody standing on a bank. Set before the tick rather than
        /// after it, because the snapshot presentation reads is published during the tick.</para>
        /// </summary>
        static void Step(SimWorld world, PawnFigureDirector figures, int layer,
            SliceSettings slice, int movePerTick, Pawn swimmer, int cell)
        {
            swimmer.Cell = cell;
            swimmer.ClearPath();

            world.Tick();
            figures.Sync(world.Views.Current, layer, slice, 0f, movePerTick, FrameSeconds);
            figures.Evaluate(FrameSeconds);
        }

        /// <summary>Across the swimmer, where the stroke lives. See ClimbCheck.SideOn.</summary>
        static float SideOn(float facing) => facing + 90f;

        static PawnView ViewOf(WorldSnapshot snapshot, PawnId id, PawnFigureDirector figures)
        {
            foreach (PawnView pawn in snapshot.Pawns)
                if (pawn.Id.Value == id.Value && figures.Drawn.Contains(pawn.Id.Value)) return pawn;
            return default;
        }

        /// <summary>
        /// Framed at the middle of a prone figure rather than at its chest.
        ///
        /// <para>A swimmer is the game's <em>longest</em> pose where a climber is its tallest: the
        /// body lies out along its own bearing, so the thing to keep in frame runs forward from the
        /// feet rather than up from them. Framed at the chest as the climb is, the sheet crops the
        /// trailing legs, which are half of what says this is a swim and not a float.</para>
        /// </summary>
        static Vector3 Middle(PawnFigureDirector figures, PawnId id) =>
            (figures.TryGetFeet(id, out Vector3 feet) ? feet : Vector3.zero) + Vector3.up * 0.9f;
    }
}
