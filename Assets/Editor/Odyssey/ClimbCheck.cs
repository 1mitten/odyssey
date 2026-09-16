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
    /// Photograph a colonist on a wall, point by point through the cycle, so the climb can be
    /// judged by eye.
    ///
    /// <para><c>GestureCheck</c>'s sibling and largely a copy of it. The climb needed its own sheet
    /// the moment it grew legs: arms alone were a pose anybody could accept or reject in one
    /// picture, and a four-limbed cycle has a property no single picture shows — whether the arm
    /// and the leg that rise together are on opposite sides. Photographed at one instant, a
    /// contralateral climb and a frog's bound look much the same.</para>
    ///
    /// <para><b>The climb is forced, and the wall is not there.</b> A real one happens where a
    /// shaft has been dug, which on a wooded board is nowhere until a colonist has spent a morning
    /// mining, and it is over in under a second when it happens. So the harness sets
    /// <c>ForceClimbFace</c> and walks <c>HeldClimbPhase</c> by hand, exactly as the lift's sheet
    /// forces its gesture, and the figure climbs a face of clear air.</para>
    ///
    /// <para><b>What that can settle and what it cannot,</b> which matters more here than it did
    /// for the lift. It settles the figure: boots below the hips and apart, knees bent alternately,
    /// the opposite arm and leg rising together, no limb straightened out to a hold it could never
    /// reach. It cannot settle the figure against the rock — there is no rock in the picture. That
    /// half was settled when the lean landed, against a real shaft, and is not reopened here.</para>
    ///
    /// <para><b>The boots stand in the grass, and that is the sheet and not the pose.</b> A real
    /// climber is half way between two layers with nothing under it; a forced one is a colonist
    /// standing on a meadow, so the pushing leg — which goes nearly its whole length below the hip
    /// — puts its boot through the ground. Read the height of each boot against the <em>hip</em>,
    /// not against the grass.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.ClimbCheck.Run</c>. Output:
    /// <c>Logs/climb-N.png</c>, one per point in the cycle, plus a board-camera shot.</para>
    /// </summary>
    public static class ClimbCheck
    {
        /// <summary>Frames per second the harness pretends to run at. There is no player loop here.</summary>
        const float FrameSeconds = 1f / 60f;

        /// <summary>
        /// Where in the climb to shoot.
        ///
        /// <para>Evenly spaced, unlike the lift's, and for the opposite reason: a climb has no
        /// moment worth more than the others. It is a cycle, and what is being judged is the whole
        /// of it — so the pictures are laid out at even intervals over one full reach and the sheet
        /// reads as a strip of film.</para>
        ///
        /// <para>Half a cell rather than a whole one. The arms take two reaches per cell, so half a
        /// cell is one complete cycle and a whole one would photograph the same eight poses
        /// twice.</para>
        /// </summary>
        static readonly float[] Phases =
            { 0f, 0.0625f, 0.125f, 0.1875f, 0.25f, 0.3125f, 0.375f, 0.4375f, 0.5f };

        [MenuItem("Odyssey/Presentation/Check the climb")]
        public static void RunFromMenu() => Shoot(exitWhenDone: false);

        public static void Run() => Shoot(Application.isBatchMode);

        static void Shoot(bool exitWhenDone)
        {
            Execute(exitWhenDone);
            if (!exitWhenDone) ShotFolder.Reveal("climb-*.png");
        }

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
                    .AddColony(pawns, designations, support, nav)
                    .Build();

                // Bare, for the same reason the lift's sheet is: a working figure is posed by its
                // stroke and never reaches the climb branch at all.
                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, ScenarioDef.Bare());

                lightingRoot = new GameObject("ClimbRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                figures = new PawnFigureDirector(catalogue, lightingRoot.transform, 0) { World = model };
                if (!figures.Enabled)
                    Debug.LogWarning("[Climb] no live figures: no character art or no gait clips. " +
                                     "The pictures will show the baked meshes and prove nothing.");

                int movePerTick = ContentPack.Pawns().Movement.movePerTick;

                for (int tick = 0; tick < 400; tick++)
                {
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                }

                PawnView subject = FirstDrawn(world.Views.Current, figures);
                if (subject.Id.Value == 0)
                {
                    Debug.LogError("[Climb] no colonist is being drawn; nothing to photograph.");
                    exitCode = 1;
                    return;
                }

                cameraObject = new GameObject("ClimbCamera");
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

                figures.ForceClimbFace = Vector3.forward;

                // Throw the first few pictures away, and let the lean ease all the way in while
                // they are being thrown. Two things are being warmed at once: the character's
                // material, which draws flat yellow on the first frame it is touched, and
                // ClimbWeight, which takes 0.15 s to reach the wall and would otherwise put the
                // first two or three pictures of every sheet part way on to it.
                figures.HeldClimbPhase = 0f;
                for (int warm = 0; warm < 16; warm++)
                {
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                    figures.TryGetFacing(subject.Id, out float warmFacing);
                    PlayScene.Shoot(camera, Chest(figures, subject.Id), 6f, SideOn(warmFacing), 7.5f,
                        "Logs/climb-warm.png");
                }

                foreach (float phase in Phases)
                {
                    figures.HeldClimbPhase = phase;

                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);

                    string path = $"Logs/climb-{phase:0.000}.png";
                    figures.TryGetFacing(subject.Id, out float facing);
                    PlayScene.Shoot(camera, Chest(figures, subject.Id), 6f, SideOn(facing), 7.5f, path);
                    Debug.Log($"[Climb] {path}: worst boot {figures.MeasuredFootReach:0.000} m from " +
                              $"its hold; {figures.DescribeClimb()}");
                }

                // And from the bearing a player actually sees the board from, because a pose that
                // reads in profile and vanishes at the board camera's pitch has solved nothing.
                figures.HeldClimbPhase = 0.125f;
                world.Tick();
                figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                figures.Evaluate(FrameSeconds);
                PlayScene.Shoot(camera, Chest(figures, subject.Id), 22f, 45f, 11f, "Logs/climb-board.png");

                figures.HeldClimbPhase = null;
                figures.ForceClimbFace = null;

                Debug.Log($"[Climb] wrote Logs/climb-*.png for colonist {subject.Id} at {subject.Cell}. " +
                          "A boot distance of zero at every phase with no legs reported means no leg " +
                          "bones were bound and the pose did nothing.");
            }
            catch (Exception error)
            {
                Debug.LogError($"[Climb] {error}");
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
        /// Across the climber rather than along it, which for this sheet is the whole of it.
        ///
        /// <para>A climb is a motion in the sagittal plane — arms up the wall, knees out from it —
        /// and from behind, which is where the figure's own bearing puts a camera when the figure
        /// has been turned to face its rock, none of that is visible at all. The first sheet was
        /// nine pictures of a colonist's back with one hand showing over its shoulder.</para>
        ///
        /// <para>Ninety degrees, unlike <c>GestureCheck</c>, and the difference is not a
        /// disagreement. That harness photographs a figure whose facing nothing has set, so it
        /// inherits whatever the walk left and the mesh's own ninety-degree turn inside the prefab
        /// happens to cancel the correction. Here the yaw is driven straight from the forced wall
        /// direction, so the correction is needed.</para>
        /// </summary>
        static float SideOn(float facing) => facing + 90f;

        /// <summary>The first colonist the director is actually drawing, or a default view.</summary>
        static PawnView FirstDrawn(WorldSnapshot snapshot, PawnFigureDirector figures)
        {
            foreach (PawnView pawn in snapshot.Pawns)
                if (figures.Drawn.Contains(pawn.Id.Value)) return pawn;
            return default;
        }

        /// <summary>
        /// Framed at the chest of the drawn figure, high enough to keep both the overhead hand and
        /// the pushing boot in one picture.
        ///
        /// <para>A climber is the tallest pose in the game — a hand at full stretch above the crown
        /// and a boot at full stretch below the hip — so it is framed higher and no tighter than
        /// the crouch, which is the shortest. Crop either end and the sheet loses the only thing it
        /// is for.</para>
        /// </summary>
        static Vector3 Chest(PawnFigureDirector figures, PawnId id) =>
            (figures.TryGetFeet(id, out Vector3 feet) ? feet : Vector3.zero) + Vector3.up * 1.4f;
    }
}
