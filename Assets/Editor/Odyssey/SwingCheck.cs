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
    /// gap, and the numbers it exists to tune are <c>AxeBladeRoll</c>, <c>AxeGripFraction</c>,
    /// <c>WorkStance.StandOff</c> and the six angle constants in <see cref="WorkSwing"/>.
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
        /// <summary>
        /// Which style the sheet is shot in, set by whichever menu item was used.
        ///
        /// <para>It used to be a <c>const</c>, which meant settling the pick meant editing the
        /// harness and rebuilding. It is a field now for a stronger reason than convenience:
        /// <see cref="WorkStyle.Building"/> has no job to be caught doing, so the only way it can
        /// ever be photographed is <c>StyleOverride</c>, and the only way that can be reached is
        /// from here.</para>
        /// </summary>
        static int Style = WorkStyle.FellingIndex;

        /// <summary>
        /// What this style's pictures are called, so that three sheets can sit in <c>Logs/</c> at
        /// once and be compared. They all wrote <c>swing-N.png</c> before, which meant shooting
        /// the pick silently destroyed the axe's sheet — and the one thing a contact sheet is for
        /// is putting two answers side by side.
        /// </summary>
        static string Tag => Style == WorkStyle.MiningIndex ? "pick"
            : Style == WorkStyle.BuildingIndex ? "hammer"
            : "swing";

        /// <summary>Frames per second the harness pretends to run at. There is no player loop here.</summary>
        const float FrameSeconds = 1f / 60f;

        /// <summary>How many pictures to take across one stroke.</summary>
        const int Samples = 9;

        /// <summary>Blade rolls to photograph side by side, in degrees about the haft.</summary>
        /// <summary>
        /// Blade rolls to photograph side by side, in degrees about the haft.
        ///
        /// The whole circle, because the last sweep covered a quarter of it and the answer was
        /// outside. Which way a head faces cannot be reasoned out from a mesh whose axes belong to
        /// somebody else; it can only be looked at, so look at all of it at once.
        /// </summary>
        static readonly float[] BladeRolls =
            { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

        [MenuItem("Odyssey/Presentation/Check the axe swing")]
        public static void RunFromMenu() => Shoot(WorkStyle.FellingIndex, exitWhenDone: false);

        [MenuItem("Odyssey/Presentation/Check the pick swing")]
        public static void RunPickFromMenu() => Shoot(WorkStyle.MiningIndex, exitWhenDone: false);

        [MenuItem("Odyssey/Presentation/Check the hammer swing")]
        public static void RunHammerFromMenu() => Shoot(WorkStyle.BuildingIndex, exitWhenDone: false);

        public static void Run() => Shoot(WorkStyle.FellingIndex, Application.isBatchMode);

        public static void RunPick() => Shoot(WorkStyle.MiningIndex, Application.isBatchMode);

        public static void RunHammer() => Shoot(WorkStyle.BuildingIndex, Application.isBatchMode);

        /// <summary>
        /// Shoot one style's sheet.
        ///
        /// <para>The colonist photographed is always felling a tree, whatever style is asked for:
        /// felling is the only work the playtest scenario hands out at the start, and the harness's
        /// whole worth is that it photographs a real worker who really walked there. What the style
        /// changes is the tool in the hand and the arc it travels, which is the part being judged.
        /// A miner photographed cutting a tree is telling the truth about the stroke and a lie
        /// about the subject, and only the first is on trial here.</para>
        /// </summary>
        static void Shoot(int style, bool exitWhenDone)
        {
            Style = style;
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
                    .AddColony(pawns, designations, support, nav)
                    .Build();

                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, ScenarioDef.Playtest());
                ColonyScenario.DesignateTreesNear(designations, result.StartCell, 10);

                lightingRoot = new GameObject("SwingRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                figures = new PawnFigureDirector(catalogue, lightingRoot.transform, 0);

                // Work in the style asked for rather than the style the job implies. For felling
                // and mining these agree; for building there is no job to imply anything, which is
                // the whole reason the override exists.
                figures.StyleOverride = Style;
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
                // The style's own period, not the axe's. A hammer at 0.7 s sampled across the axe's
                // 1.15 s walks past the end of its stroke and into the next one, so the sheet shows
                // a blow and a half and the samples no longer divide a stroke evenly.
                int strokeFrames = Mathf.CeilToInt(
                    WorkStyle.All[Style].Stroke.StrokeSeconds / FrameSeconds);
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

                    // Close, low, and **side on to the line between the woodcutter and her
                    // tree**. The board camera's three-quarter bearing puts the two at different
                    // depths in the frame, and the one measurement that matters here — whether
                    // the blade arrives at the trunk with room to have travelled — then reads as
                    // whatever you please. Across the line it reads as what it is.
                    PlayScene.Shoot(camera, Waist(now), 12f, SideOn(now), 6.5f, $"Logs/{Tag}-{sample}.png");

                    // Across the whole stroke, not only at the blow. Two forearms on one haft are
                    // about a hand thick each, so anything under about 0.2 m apart is two meshes in
                    // the same place however far "above" the number says the off arm is — and the
                    // arms are closest together somewhere in the raise, not at the strike.
                    Debug.Log($"[Swing] sample {sample}: off forearm " +
                              $"{drawn.MeasuredOffArmAbove:+0.000;-0.000} m above the working one, {drawn.MeasuredArmGap:0.000} m apart; off palm {drawn.MeasuredGripGap:0.000} m off the haft, overreach {drawn.MeasuredGripOverreach:+0.000;-0.000} m, tool drift {drawn.MeasuredToolDrift:0.000} deg, hand at {drawn.MeasuredGripAt:0.00} of the haft, butt {drawn.MeasuredGripSpan:0.000} m from the off shoulder; butt at {drawn.MeasuredGripLocal.ToString("0.00")}, palm at {drawn.MeasuredPalmLocal.ToString("0.00")}");
                }

                // The blow itself, pinned rather than hoped for.
                //
                // The nine samples walk the stroke at a fixed number of frames apart, so which of
                // them lands on the moment of impact depends on the stroke's length — and the
                // moment the stroke's length became a per-figure thing, none of them reliably did.
                // This one is held at the dwell, so every run photographs the blow.
                drawn.HeldPhase = 0.9f;
                drawn.Sync(Current(), activeLayer, slice, 0f, movePerTick, FrameSeconds);
                drawn.Evaluate(FrameSeconds);
                PawnView struck = FirstWorker(Current());
                if (struck.Working)
                    PlayScene.Shoot(camera, Waist(struck), 12f, SideOn(struck), 6.5f, $"Logs/{Tag}-impact.png");

                // A contact sheet of the blade's roll, held at the moment of the blow.
                //
                // Which way the edge faces is the one part of the grip that is taste rather than
                // geometry, so it is chosen by looking at the same instant at several settings
                // side by side rather than by running the whole harness once per setting.
                drawn.HeldPhase = 0.9f;
                Debug.Log($"[Swing] blade sheet: {BladeRolls.Length} rolls, " +
                          $"{drawn.FigureCount} figures live, worker still working: {FirstWorker(Current()).Working}");
                foreach (float roll in BladeRolls)
                {
                    drawn.Styles[Style] = drawn.Styles[Style].With(bladeRoll: roll);
                    drawn.RegripTools();
                    drawn.Sync(Current(), activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    drawn.Evaluate(FrameSeconds);

                    string path = $"Logs/{Tag}-blade-{roll:000}.png";
                    PlayScene.Shoot(camera, drawn.LastBladePosition, 6f, SideOn(FirstWorker(Current())), 1.5f, path);
                    Debug.Log($"[Swing] wrote {path} at blade {drawn.LastBladePosition}");
                }
                // Put the roll back to what the style actually ships with before sweeping the yaw,
                // so the yaw sheet is shot at the head's real orientation and not at the last one
                // the loop happened to leave behind.
                //
                // This line used to read `bladeRoll: 270f` unconditionally, which was a fossil: the
                // axe's roll *was* 270 until eb5371f, where it went to 0 and BladeYaw was added to
                // do the job the roll could not — "the roll turns the head about the very line the
                // head is trying to be pointed along". The harness kept re-applying the superseded
                // answer, and would have applied the axe's superseded answer to the pick and the
                // hammer as well.
                drawn.Styles[Style] = drawn.Styles[Style].With(bladeRoll: WorkStyle.All[Style].BladeRoll);

                // And the same again about the upright, which is the turn that points the edge at
                // the tree rather than moving it around the haft.
                float yawWas = drawn.Styles[Style].BladeYaw;
                foreach (float yaw in BladeRolls)
                {
                    drawn.Styles[Style] = drawn.Styles[Style].With(bladeYaw: yaw);
                    drawn.RegripTools();
                    drawn.Sync(Current(), activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    drawn.Evaluate(FrameSeconds);

                    string path = $"Logs/{Tag}-yaw-{yaw:000}.png";
                    PlayScene.Shoot(camera, drawn.LastBladePosition, 6f, SideOn(FirstWorker(Current())), 1.5f, path);
                    Debug.Log($"[Swing] wrote {path}");
                }

                // The hands, from in front of the worker and from over its shoulder.
                //
                // **The side-on shot cannot answer this and never could.** In profile the two arms
                // are one behind the other, so which of them passes over the other — the thing the
                // owner is looking at — is precisely what is hidden. The grip is the one part of
                // this pose where the useful camera is the one square to the chest.
                //
                // The bearings are figure-relative, and the offsets are not the obvious ones: the
                // mesh inside a Synty character prefab is turned ninety degrees from its root, so
                // the transform's own yaw is a *profile* view and the front is a further 270.
                // docs/lessons.md, "Photographing a figure".
                // All the way round, at the blow, because a bearing that is "the front" for one
                // harness is not for another: the figure here is turned to face its tree, so the
                // offset that gave a profile in GestureCheck gives something else again. Shooting
                // the circle costs four pictures and settles it without an argument.
                drawn.HeldPhase = 0.9f;
                foreach (float turn in new[] { 0f, 90f, 180f, 270f })
                {
                    drawn.Sync(Current(), activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    drawn.Evaluate(FrameSeconds);

                    PawnView who = FirstWorker(Current());
                    if (!who.Working) break;
                    drawn.TryGetFacing(who.Id, out float about);

                    PlayScene.Shoot(camera, Chest(drawn, who), 8f, about + turn, 4.5f,
                        $"Logs/{Tag}-arms-{turn:000}.png");
                    PlayScene.Shoot(camera, Chest(drawn, who), 40f, about + turn, 4.5f,
                        $"Logs/{Tag}-arms-over-{turn:000}.png");
                }

                drawn.HeldPhase = null;
                drawn.Styles[Style] = drawn.Styles[Style].With(bladeYaw: yawWas);
                drawn.RegripTools();

                PawnView last = FirstWorker(Current());
                if (last.Working)
                {
                    // And the board camera's own bearing and height, which is the only one a
                    // player will ever see this from.
                    PlayScene.Shoot(camera, Waist(last), 30f, 22f, $"Logs/{Tag}-wide.png");
                }

                Debug.Log($"[Swing] {Tag}: wrote Logs/{Tag}-0..{Samples - 1}.png and Logs/{Tag}-wide.png; " +
                          $"worker {worker.Id} at {worker.Cell} swinging at {worker.WorkCell}; " +
                          $"strike {figures.MeasuredReach:0.00} m, of which {figures.MeasuredStrikeSideways:0.00} m " +
                          $"sideways; edge lands {figures.MeasuredBladeHeight:0.00} m off the ground, " +
                          $"{figures.MeasuredBladeGap:0.00} m from the middle of the trunk; " +
                          $"{figures.Chips?.ChipsThrown ?? 0} chips thrown; off forearm {figures.MeasuredOffArmAbove:+0.000;-0.000} m above the working one");
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
        /// What the camera should be looking at: the gap between the woodcutter and the tree.
        ///
        /// Halfway between the two cells and raised to about chest height. Framed on the
        /// colonist's own cell instead, two things go wrong at once — the head leaves the top of
        /// the picture and the axe leaves it entirely at the top of its arc, and the trunk sits
        /// off at the edge, so the one thing worth judging, whether the blade arrives at the
        /// tree with room to have travelled, cannot be seen at all.
        /// </summary>
        /// <summary>
        /// A bearing across the line from the woodcutter to her tree, so the camera looks along
        /// neither of them and the gap between the two is seen at its true size.
        /// </summary>
        static float SideOn(in PawnView pawn)
        {
            Vector3 toWork = CellMetrics.FloorCentre(pawn.WorkCell) - CellMetrics.FloorCentre(pawn.Cell);
            toWork.y = 0f;
            return PawnPose.YawOf(toWork.sqrMagnitude > 1e-4f ? toWork : Vector3.forward) + 90f;
        }

        /// <summary>
        /// The drawn figure's chest, for the grip shots. The figure's own position and not the
        /// pawn's cell, because <c>WorkStance</c> steps a working colonist off its cell to put its
        /// blade in the wood — up to a metre and a half, which at four metres' range is most of
        /// the frame.
        /// </summary>
        static Vector3 Chest(PawnFigureDirector figures, in PawnView pawn) =>
            (figures.TryGetFeet(pawn.Id, out Vector3 feet)
                ? feet
                : CellMetrics.FloorCentre(pawn.Cell)) + Vector3.up * 1.5f;

        static Vector3 Waist(in PawnView pawn) =>
            (CellMetrics.FloorCentre(pawn.Cell) + CellMetrics.FloorCentre(pawn.WorkCell)) * 0.5f
            + Vector3.up * 1.3f;
    }
}
