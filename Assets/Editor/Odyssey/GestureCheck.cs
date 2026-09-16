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
    /// Photograph a one-shot gesture, phase by phase, so it can be judged by eye.
    ///
    /// <para><c>SwingCheck</c>'s sibling and, where it can be, a copy of it: a real colonist on a
    /// real board under the game's own lighting, because a pose set up by hand proves only that the
    /// pose can be set up by hand. Two things had to differ, and both are properties of a one-shot
    /// rather than decisions.</para>
    ///
    /// <para><b>The gesture is forced rather than waited for.</b> A lift lasts eight-tenths of a
    /// second and begins on the tick a haul happens to reach its load, which nothing can predict.
    /// Waiting for one means catching it by luck. Worse, the obvious fix — stop the world and step
    /// frames — turns on the pause inference (<c>PawnFigureDirector.Sync</c>: a quarter second with
    /// no tick is a pause) and freezes the very clock being photographed. So the harness sets
    /// <c>ForceGesture</c> and walks <c>HeldGesturePhase</c> by hand.</para>
    ///
    /// <para><b>It shoots from the side, and here that is not a subtlety.</b> A crouch is entirely
    /// a vertical motion, so a camera anywhere near the figure's own bearing sees the shoulders
    /// come towards it and almost nothing else — the hips could drop half a metre or not move at
    /// all and the two pictures would be hard to tell apart. Across the figure, the whole of it is
    /// in profile. <c>MeasuredCrouchDrop</c> is printed beside the pictures for the same reason
    /// <c>MeasuredBladeGap</c> is: an eye is a poor judge of how far something moved, and twice
    /// already a swing was declared to be missing its tree when it was not.</para>
    ///
    /// <para><b>The bearing is the figure's own facing, not square across it, and that took four
    /// sheets to find out.</b> Aimed at <c>facing + 90</c> — which is what "side on" plainly means,
    /// and what <c>SwingCheck</c> does relative to the line of work — it came out square behind the
    /// colonist every time, while the arithmetic insisted it was perpendicular. It was: the
    /// <em>mesh</em> inside a Synty character prefab is turned ninety degrees from its own root, so
    /// the figure faces across the bearing its transform reports. Nothing here reasons about that.
    /// Four bearings were shot at the bottom of the motion and the profile picked out by looking,
    /// exactly as the axe's blade roll was. If a future pack's characters are built differently,
    /// shoot the four again rather than arguing with the picture.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.GestureCheck.Run</c>. Output:
    /// <c>Logs/lift-N.png</c> and <c>Logs/stow-N.png</c>, one per phase, plus a wide shot.</para>
    /// </summary>
    public static class GestureCheck
    {
        /// <summary>Which gesture the sheet is shot in, set by whichever menu item was used.</summary>
        static PawnGesture Motion = PawnGesture.Lift;

        /// <summary>Frames per second the harness pretends to run at. There is no player loop here.</summary>
        const float FrameSeconds = 1f / 60f;

        /// <summary>
        /// The phases to photograph.
        ///
        /// Not evenly spaced. Standing, three through the drop, two across the hold — which is the
        /// part worth most and lasts least — and three on the way up. An even sweep spends as many
        /// pictures on the empty top of the motion as on the bottom of it.
        /// </summary>
        static readonly float[] Phases =
            { 0f, 0.15f, 0.28f, 0.4f, 0.47f, 0.55f, 0.7f, 0.85f, 1f };

        [MenuItem("Odyssey/Presentation/Check the lift")]
        public static void RunLiftFromMenu() => Shoot(PawnGesture.Lift, exitWhenDone: false);

        [MenuItem("Odyssey/Presentation/Check the set-down")]
        public static void RunStowFromMenu() => Shoot(PawnGesture.Stow, exitWhenDone: false);

        public static void Run() => Shoot(PawnGesture.Lift, Application.isBatchMode);

        public static void RunStow() => Shoot(PawnGesture.Stow, Application.isBatchMode);

        static string Tag => Motion == PawnGesture.Stow ? "stow" : "lift";

        static void Shoot(PawnGesture motion, bool exitWhenDone)
        {
            Motion = motion;
            Execute(exitWhenDone);
            if (!exitWhenDone) ShotFolder.Reveal($"{Tag}-*.png");
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
                    grid, nav, new PathService(new PathFinder(nav)), PawnContent.Core())
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

                // Bare rather than Playtest: no trees are marked, so nobody takes up an axe. A
                // working figure is posed by the swing and never reaches the gesture branch at all,
                // so a colony full of woodcutters would photograph nothing.
                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, ScenarioDef.Bare());

                lightingRoot = new GameObject("GestureRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                figures = new PawnFigureDirector(catalogue, lightingRoot.transform, 0);
                if (!figures.Enabled)
                    Debug.LogWarning("[Gesture] no live figures: no character art or no gait clips. " +
                                     "The pictures will show the baked meshes and prove nothing.");

                int movePerTick = PawnContent.Core().Movement.movePerTick;

                // Let the colony settle and let the figures learn where everybody is. Synced every
                // tick, not only at the end: a director handed a world that has already run sees
                // everybody teleport in, and a figure that has not moved has no gait at all.
                for (int tick = 0; tick < 400; tick++)
                {
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                }

                PawnView subject = FirstDrawn(world.Views.Current, figures);
                if (subject.Id.Value == 0)
                {
                    Debug.LogError("[Gesture] no colonist is being drawn; nothing to photograph.");
                    exitCode = 1;
                    return;
                }

                cameraObject = new GameObject("GestureCamera");
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

                figures.ForceGesture = Motion;

                // Throw the first few pictures away. A character drawn in the first frames after
                // its material is first touched comes out flat yellow — untextured, unlit, the
                // whole colonist one colour — and then draws correctly from a frame or two later.
                // It is the same class of thing ChipDirector warms its particle material for, and
                // the same fix: pay the cost somewhere nobody is looking.
                figures.HeldGesturePhase = 0.47f;
                for (int warm = 0; warm < 4; warm++)
                {
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                    figures.TryGetFacing(subject.Id, out float warmFacing);
                    PlayScene.Shoot(camera, Waist(figures, subject.Id), 6f, warmFacing, 7.5f,
                        "Logs/gesture-warm.png");
                }

                foreach (float phase in Phases)
                {
                    figures.HeldGesturePhase = phase;

                    // The world goes on ticking so that the pause inference stays off, but the
                    // phase is held, so what moves between pictures is only the gesture.
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);

                    string path = $"Logs/{Tag}-{phase:0.00}.png";
                    figures.TryGetFacing(subject.Id, out float facing);
                    PlayScene.Shoot(camera, Waist(figures, subject.Id), 6f, facing, 7.5f, path);
                    Debug.Log($"[Gesture] {path}: hips {figures.MeasuredCrouchDrop:0.000} m below " +
                              $"standing; {figures.CrouchedFigures} of {figures.Drawn.Count} figures " +
                              $"stooping, {figures.LeglessFigures} with no pelvis to stoop with");
                }

                // And from the only bearing a player ever sees the board from, because a pose that
                // reads in profile and vanishes at the board camera's pitch has solved nothing.
                figures.HeldGesturePhase = 0.47f;
                world.Tick();
                figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                figures.Evaluate(FrameSeconds);
                PlayScene.Shoot(camera, Waist(figures, subject.Id), 22f, 45f, 11f,
                    $"Logs/{Tag}-board.png");

                figures.HeldGesturePhase = null;
                figures.ForceGesture = null;

                Debug.Log($"[Gesture] {Tag}: wrote Logs/{Tag}-*.png for colonist {subject.Id} " +
                          $"at {subject.Cell}; deepest crouch {figures.MeasuredCrouchDrop:0.000} m. " +
                          "A drop of zero means no leg bones were bound and the pose did nothing.");
            }
            catch (Exception error)
            {
                Debug.LogError($"[Gesture] {error}");
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

        /// <summary>The first colonist the director is actually drawing, or a default view.</summary>
        static PawnView FirstDrawn(WorldSnapshot snapshot, PawnFigureDirector figures)
        {
            foreach (PawnView pawn in snapshot.Pawns)
                if (figures.Drawn.Contains(pawn.Id.Value)) return pawn;
            return default;
        }

        /// <summary>
        /// Across the figure rather than along it.
        ///
        /// <para>A crouch is a vertical motion, so from in front or behind the shoulders merely come
        /// towards the lens and half a metre of hip drop looks much like none. The profile is where
        /// the knee, the hip and the back are all visible at once, which is the whole of what there
        /// is to judge.</para>
        ///
        /// <para><b>Asked of the figure and not of the snapshot, which the first sheet got wrong.</b>
        /// The obvious bearing is the line from the pawn's cell to its next cell — and that is zero
        /// for a pawn standing still, which is exactly what this harness photographs. It fell back
        /// to a fixed bearing and shot the whole motion head-on: nine pictures of the one view in
        /// which a crouch cannot be seen.</para>
        /// </summary>
        static float SideOn(PawnFigureDirector figures, PawnId id) =>
            (figures.TryGetFacing(id, out float yaw) ? yaw : 0f) + 90f;

        /// <summary>
        /// Framed at the chest of the *drawn* figure, and far enough back to keep the boots in.
        ///
        /// <para>Two corrections from the first sheet. It framed on the pawn's cell centre, which is
        /// where the simulation keeps the colonist and not where the colonist is drawn — and it
        /// framed at 0.9 m on figures drawn half again as large as life, so the feet sat on the
        /// bottom edge. Feet are not optional here: a crouch whose feet slide is the exact failure
        /// the leg solve exists to prevent, and a picture that crops them cannot show it.</para>
        /// </summary>
        static Vector3 Waist(PawnFigureDirector figures, PawnId id) =>
            (figures.TryGetFeet(id, out Vector3 feet) ? feet : Vector3.zero) + Vector3.up * 1.25f;
    }
}
