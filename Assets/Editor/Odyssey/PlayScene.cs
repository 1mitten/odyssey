#nullable enable
// Generates the playable scene and the module catalogue it reads.
//
// Scenes in this project are generated, never hand-authored, so that they are reproducible and
// reviewable as code. This one is the M1 look-and-fly scene: a camera rig, a light, and the
// bootstrap that generates a world on Play and draws it.
//
// Editor menu:  Odyssey > Presentation > Build play scene
//               Odyssey > Presentation > Rebuild module catalogue
// Headless:     scripts/unity.sh exec Odyssey.EditorTools.PlayScene.Build
//               scripts/unity.sh exec Odyssey.EditorTools.PlayScene.RebuildCatalogue
//
// The catalogue maps module id strings to Synty prefabs by *name*, resolved here against
// Assets/Synty. A clone without the licensed packs generates the same catalogue with every prefab
// reference empty, and the renderer draws primitives instead: the scene still builds, still runs
// and still flies around.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Odyssey.Presentation.Audio;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Sim;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.Ui;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Odyssey.Presentation.Rendering;
using UnityEngine.UIElements;

namespace Odyssey.EditorTools
{
    public static class PlayScene
    {
        const string ScenePath = "Assets/Scenes/Play.unity";
        internal const string CataloguePath = "Assets/Odyssey/Presentation/ModuleCatalogue.asset";
        internal const string AudioCataloguePath = "Assets/Odyssey/Presentation/Audio/AudioCatalogue.asset";

        // The HUD's three assets: an authored stylesheet, plus a theme and panel settings made
        // once on demand. Real assets rather than in-memory ones because the scene serialises the
        // panel reference, the same reason the sky material is a real asset.
        const string HudStylesPath = "Assets/Odyssey/Presentation/Ui/Hud.uss";
        const string HudThemePath = "Assets/Odyssey/Presentation/Ui/RuntimeTheme.tss";
        const string HudPanelPath = "Assets/Odyssey/Presentation/Ui/HudPanelSettings.asset";
        const string HudUiFontPath = "Assets/Odyssey/Presentation/Ui/Fonts/ArchivoNarrow.ttf";
        const string HudMonoFontPath = "Assets/Odyssey/Presentation/Ui/Fonts/IBMPlexMono-Medium.ttf";

        /// <summary>The mockup's canvas, which the stylesheet's pixel sizes are authored against.</summary>
        /// <summary>
        /// The canvas the HUD is authored against.
        ///
        /// <para>Moved from 1200x800 to 1920x1080 on 2026-09-16 with the interface rebuild. The
        /// specification gives every anchor, width and row height in 1080p pixels — left:20,
        /// width:288, rows 29 tall — and a panel scaled against a 1200x800 reference would draw
        /// every one of them 1.6 times too large. It is also the resolution the acceptance
        /// criteria are stated at, and two of the three they name (1280x720 and 2560x1440) are the
        /// same shape, so all three resolve to this one logical canvas.</para>
        /// </summary>
        public static readonly Vector2Int HudReferenceResolution = new Vector2Int(1920, 1080);

        /// <summary>
        /// The world the play scene is built with, and the world "Measure a slice" measures. One
        /// pair of constants so the two cannot drift apart again: a benchmark of a map the game
        /// does not load is worse than no benchmark, because it still produces a number.
        /// </summary>
        internal const int PlaySizeXZ = 120;
        internal const int PlayLayers = 16;

        [MenuItem("Odyssey/Presentation/Build play scene")]
        public static void BuildFromMenu() => BuildInternal(exitWhenDone: false);

        /// <summary>Batchmode entry point. Exits the editor with 0 on success, 1 on failure.</summary>
        public static void Build() => BuildInternal(Application.isBatchMode);

        /// <summary>
        /// Make the scene this tool generates the one a player build ships.
        ///
        /// <para><b>Because the two had never been made to agree, and nothing noticed for
        /// months.</b> The build settings still carried the Unity template's
        /// <c>SampleScene.unity</c> on 2026-09-19, long after that file stopped existing, and it
        /// went unseen because nothing in this repository had ever built a player — both test
        /// tiers run in the editor's own domain and never read the list. Generating a scene and
        /// leaving a hand-maintained pointer at it is the kind of second source of truth the
        /// project's own convention exists to avoid: scenes here are generated, not
        /// hand-authored, so that they are reproducible.</para>
        ///
        /// <para>Idempotent, and it writes nothing when the list already says this. A no-op save
        /// of <c>EditorBuildSettings.asset</c> is a spurious diff on every scene rebuild.</para>
        /// </summary>
        static void RegisterInBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 1 && scenes[0].enabled && scenes[0].path == ScenePath) return;

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log($"[PlayScene] build settings now ship {ScenePath} and nothing else.");
        }

        [MenuItem("Odyssey/Presentation/Rebuild module catalogue")]
        public static void RebuildCatalogueFromMenu() => RebuildInternal(exitWhenDone: false);

        public static void RebuildCatalogue() => RebuildInternal(Application.isBatchMode);

        [MenuItem("Odyssey/Presentation/Measure a slice")]
        public static void MeasureFromMenu() => MeasureInternal(exitWhenDone: false);

        /// <summary>
        /// Build the world the play scene builds, mesh a slice and report what it costs to submit.
        ///
        /// Headless and with no graphics device, so it runs in CI: the renderer is put in dry-run
        /// mode, which does everything except hand the buckets to the GPU. The draw-call and
        /// instance counts are therefore the real ones; frame time is not measured here, because
        /// a number for that without a GPU would be a fiction.
        /// </summary>
        public static void Measure() => MeasureInternal(Application.isBatchMode);

        /// <summary>
        /// Render the play view to PNG files on disk, headless.
        ///
        /// **Why this exists.** Four rounds of visual faults were diagnosed by reading code and
        /// reasoning about what the renderer ought to produce, and reasoning got the cause wrong
        /// more than once: the shadow acne, the raking sun and the fog wash were all invisible in
        /// the source and obvious in a picture. A renderer whose output nobody can look at is
        /// debugged by guesswork.
        ///
        /// It needs a real graphics device, so unlike every other entry point here it must run
        /// **without** <c>-nographics</c>: use <c>scripts/unity.sh shot</c>. The instanced draws
        /// are submitted from the render-pipeline callback because <c>RenderMeshInstanced</c>
        /// enqueues for the camera currently rendering, and outside a running player there is no
        /// frame loop to enqueue them in.
        /// </summary>
        public static void Screenshot() => ScreenshotInternal(Application.isBatchMode);

        static void ScreenshotInternal(bool exitWhenDone)
        {
            int exitCode = 0;
            ChunkRenderer? renderer = null;
            Odyssey.Presentation.World.PawnFigureDirector? figures = null;
            System.Action<ScriptableRenderContext, Camera>? hook = null;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
                var size = new GridSize(PlaySizeXZ, PlaySizeXZ, PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeWooded();   // the board the scene loads, trees and all
                var grid = new CellGrid(size);
                var chunks = new ChunkGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);

                using var library = new ModuleLibrary(catalogue);
                var model = new Odyssey.Presentation.World.WorldRenderModel(size, chunks, library);
                model.RefreshAll(grid, result.Natural!.Context.Edifices);

                renderer = new ChunkRenderer(model);
                int activeLayer = result.StartCell.Y;
                // The depth the picture is "at ground level" relative to, so the x-ray shot below
                // — which slices at the foot of an outcrop — reports the treatment a player would
                // actually get there rather than the surface one.
                var slice = new SliceSettings { surfaceLayer = activeLayer };

                // The scene's own lighting, so the picture matches what the player sees rather
                // than some convenient studio setup that would hide the very faults being hunted.
                var lighting = new GameObject("ShotRoot").transform;
                BuildLighting(lighting);

                var cameraObject = new GameObject("ShotCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                // Colonists too, and through the real simulation rather than a few poses dropped
                // on the grass. The point of this harness is that the picture is the game: if the
                // figures are placed wrong, sunk into the ground or facing nowhere, that has to
                // show up here rather than the first time somebody presses Play.
                var nav = new NavGraph(grid);
                nav.Rebuild();
                var pawns = new PawnContext(
                    grid, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns())
                    { Chunks = chunks };
                var support = new SupportSystem(grid, new SupportSolver(grid), chunks);
                var mirror = new Odyssey.Presentation.World.GridMirrorContributor(
                    grid, result.Edifices, model);
                var designations = new Odyssey.Sim.Designations.DesignationGrid(grid, result.Edifices);

                SimWorld world = new SimWorldBuilder()
                    .WithSeed(1u)
                    .WithSize(size)
                    .AddSnapshotContributor(mirror)
                    .AddColony(pawns, designations, support, nav, result.Placements, out _)
                    .Build();

                // The scene's own scenario, orders included, then long enough for the first tree
                // to come down and its wood to be lying there: the picture has to show the job
                // line, not just the colonists setting off along it.
                ScenarioDef scenario = ScenarioDef.Playtest();
                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, scenario);
                ColonyScenario.GiveStartingOrders(designations, result.StartCell, scenario);
                for (int i = 0; i < 2_400; i++) world.Tick();

                var actorMaterial = new Material(library.FallbackMaterial) { name = "Odyssey/Actor" };
                actorMaterial.SetColor("_BaseColor", new Color(0.98f, 0.36f, 0.20f));

                // Live figures, run forward far enough to be genuinely mid-stride.
                //
                // There is no player loop here, so nothing advances on its own: the world is
                // ticked, the director is synced and the animation graphs are stepped by hand, all
                // on the same nominal frame time. It has to be several frames rather than one,
                // because a figure's speed is measured from how far it moved since the last frame
                // and a figure leased this instant has not moved at all — a single frame would
                // photograph five people standing still and prove nothing about the walk.
                figures = new Odyssey.Presentation.World.PawnFigureDirector(catalogue, lighting, 0)
                    { World = model };   // so a climber can find its wall
                int movePerTick = ContentPack.Pawns().Movement.movePerTick;
                const float FrameSeconds = 1f / 60f;
                for (int frame = 0; frame < 40; frame++)
                {
                    world.Tick();
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                }
                Debug.Log($"[Shot] live figures: {figures.FigureCount} of {world.Views.Current.PawnCount} " +
                          $"pawns, fastest {figures.FastestSpeed:0.00} m/s" +
                          $"{(figures.Enabled ? string.Empty : " (director disabled: no art or no gaits)")}");

                ChunkRenderer active = renderer;
                Odyssey.Presentation.World.PawnFigureDirector walking = figures;

                // Which layer the hook cuts at. A frame is drawn by Shoot calling camera.Render,
                // which runs this hook — so rendering a different slice before shooting achieves
                // nothing, and the layer has to be something the hook itself reads.
                int[] shotLayer = { activeLayer };

                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    active.Render(shotLayer[0], slice);
                    active.RenderActors(world.Views.Current, shotLayer[0], slice, actorMaterial,
                        drawnAsFigures: walking.Drawn);

                    // Both cursors, so a picture can settle whether they look right: the cell
                    // bracket on a patch of empty ground, and the colonist bracket on somebody who
                    // is walking. Neither can be checked by reading the arithmetic.
                    var cursor = new Color(0.30f, 0.92f, 1.00f, 0.7f);
                    WorldSnapshot shown = world.Views.Current;

                    // Every tier in one picture: the start cell is empty ground, so it gets the
                    // floor ring; the first item gets a bracket fitted to its own art; the full
                    // cube goes on the cell beside the start so the three can be compared by eye.
                    active.DrawFloorBracket(result.StartCell, cursor);
                    active.DrawCellHighlight(
                        new CellRef(result.StartCell.X + 2, result.StartCell.Z, activeLayer), cursor);
                    if (shown.ThingCount > 0)
                    {
                        ThingView thing = shown.Things[0];
                        ResolvedModule item = library[library.Resolve(
                            ModuleIds.Item(thing.DefIndex), ModuleShape.Pillar)];
                        active.DrawSelectionBracket(
                            CellMetrics.FloorCentre(thing.Cell) + item.Bounds.center,
                            item.Bounds.size + Vector3.one * 0.16f, cursor);
                    }

                    // Whoever is nearest the start cell, because that is the middle of the frame.
                    // A cursor photographed at the edge of the picture proves nothing.
                    int nearest = -1, best = int.MaxValue;
                    for (int p = 0; p < shown.Pawns.Length; p++)
                    {
                        int dx = shown.Pawns[p].Cell.X - result.StartCell.X;
                        int dz = shown.Pawns[p].Cell.Z - result.StartCell.Z;
                        int d = dx * dx + dz * dz;
                        if (d >= best) continue;
                        best = d;
                        nearest = p;
                    }

                    if (nearest >= 0)
                    {
                        Vector3 feet = PawnPose.Of(shown.Pawns[nearest], 0f, movePerTick, out _);
                        var box = new Vector3(1.15f, 2.7f, 1.15f);
                        active.DrawSelectionBracket(feet + Vector3.up * (box.y * 0.5f), box, cursor);
                    }
                };
                RenderPipelineManager.beginCameraRendering += hook;

                var focus = new Vector3(
                    result.StartCell.X * CellMetrics.SizeXZ,
                    activeLayer * CellMetrics.SizeY,
                    result.StartCell.Z * CellMetrics.SizeXZ);

                Shoot(camera, focus, 48f, 48f, "Logs/shot-play.png");
                Shoot(camera, focus, 42f, 18f, "Logs/shot-close.png");
                Shoot(camera, focus, 70f, 26f, "Logs/shot-down.png");

                // Low and far, which is the only framing that shows the rim of the board and the
                // sky above it. The play camera never looks this flat, but the sky, the fog and
                // the edge of the world are only checkable from here.
                Shoot(camera, focus, 9f, 150f, "Logs/shot-horizon.png");

                // An outcrop, close. The start pass deliberately puts the colony on flat ground
                // clear of rock, so every framing above is guaranteed to have none in it — which
                // made judging the stone impossible from the pictures that existed. This one goes
                // and finds some.
                var natural = result.Natural;
                if (natural != null && natural.Outcrops.Count > 0)
                {
                    // The TALLEST outcrop, not the nearest, and shot from low down. A stack is
                    // the only thing that can show the fault this framing exists to catch: a
                    // one-cell lump has no layer boundary to open a slot at, and a boundary seen
                    // from above is edge-on to nothing. Height breaks ties towards the near one.
                    int nearestRock = -1;
                    int bestRock = int.MinValue;
                    foreach (RockOutcrop outcrop in natural.Outcrops)
                    {
                        CellRef at = size.FromIndex(outcrop.CellIndex);
                        int d = Mathf.Abs(at.X - result.StartCell.X) + Mathf.Abs(at.Z - result.StartCell.Z);
                        int score = outcrop.Height * 1000 - d;
                        if (score <= bestRock) continue;
                        bestRock = score;
                        nearestRock = outcrop.CellIndex;
                    }

                    if (nearestRock >= 0)
                    {
                        // Aimed at the middle of the mass, not at the ground it stands on, and far
                        // enough back to hold the whole stack: a frame that cuts the top off
                        // cannot answer whether the top is right.
                        CellRef at = size.FromIndex(nearestRock);
                        var rockFocus = new Vector3(
                            at.X * CellMetrics.SizeXZ,
                            (at.Y + 1.2f) * CellMetrics.SizeY,
                            at.Z * CellMetrics.SizeXZ);
                        Shoot(camera, rockFocus, 20f, 26f, "Logs/shot-rock.png");
                        Debug.Log($"[Shot] the tallest outcrop near the start is at {at}");
                    }
                }

                // Whoever is swinging at rock, close and side on.
                //
                // SwingCheck is the harness meant for this and it segfaults inside the render
                // pipeline in batchmode — twice, on a settled assembly, after six of its nine
                // samples. That is worth its own fix and is not worth blocking a picture on: the
                // ordinary screenshot path renders this same world reliably, and what is wanted
                // here is only whether a miner has a pick in its hands and is facing the stone.
                //
                // The colony has already run 2,400 ticks by this point, which is long enough for
                // the miners the scenario appoints to have reached the outcrop it marks.
                WorldSnapshot published = world.Views.Current;
                PawnView miner = default;
                bool foundMiner = false;

                // Prefer a miner cutting SIDEWAYS. One cutting the cell under its own feet is
                // doing the same work, but the line from worker to work is straight down, so
                // there is no side-on bearing to be had from it at all — the horizontal component
                // is zero and any bearing derived from it is arbitrary. The first attempt at this
                // put the camera inside the outcrop for exactly that reason.
                for (int i = 0; i < published.Pawns.Length; i++)
                {
                    PawnView worker = published.Pawns[i];
                    if (!worker.Working || worker.JobDef != JobHandle.Mine) continue;

                    bool sideways = worker.WorkCell.Y == worker.Cell.Y;
                    if (!foundMiner || sideways) { miner = worker; foundMiner = true; }
                    if (sideways) break;
                }

                if (foundMiner)
                {
                    // Side on to the line between the miner and the rock, and midway along it.
                    //
                    // Not a taste in framing. A three-quarter view puts the two at different
                    // depths and the one thing worth seeing — whether the head is in the stone,
                    // short of it or buried past it — then reads as whatever you please. Across
                    // the line it reads as what it is, which is why SwingCheck shoots this way and
                    // why this borrows its bearing rather than inventing one.
                    Vector3 toWork = CellMetrics.FloorCentre(miner.WorkCell)
                                   - CellMetrics.FloorCentre(miner.Cell);
                    toWork.y = 0f;

                    // Straight up or down leaves nothing to be side-on to. Quarter past the
                    // figure's own facing is at least a stable choice rather than a silent
                    // fallback to world forward, which is what pointed the camera into the rock.
                    float sideOn = toWork.sqrMagnitude > 1e-4f
                        ? PawnPose.YawOf(toWork) + 90f
                        : 45f;

                    Vector3 waist = (CellMetrics.FloorCentre(miner.Cell)
                                   + CellMetrics.FloorCentre(miner.WorkCell)) * 0.5f + Vector3.up * 1.3f;
                    Shoot(camera, waist, 12f, sideOn, 6.5f, "Logs/shot-miner.png");

                    // And again with the pick in the air.
                    //
                    // The struck pose is the one worth measuring and the WORST one to judge a
                    // tool's head by: the aim puts the head just inside the rock, so at the
                    // moment of the blow it is buried in the stone where nothing can see it —
                    // which is correct, and tells you nothing about which way round it is.
                    // Held part way up the raise, the head is against the sky.
                    //
                    // This is what the blade roll has to be settled from, and BitAxis cannot
                    // settle it: it signs the head towards its fat side, and a pick sticks out
                    // both ways — point one side, adze the other — so the sign is whichever end
                    // the modeller made heavier rather than anything anybody chose.
                    figures.HeldPhase = 0.30f;
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                    Shoot(camera, waist, 12f, sideOn, 5.5f, "Logs/shot-miner-raised.png");
                    figures.HeldPhase = null;
                    figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                    Debug.Log($"[Shot] a miner at {miner.Cell} is cutting {miner.WorkCell}" +
                              (toWork.sqrMagnitude > 1e-4f ? " sideways" : " under its own feet"));

                    // Measured, not squinted at. An empty hand in a photograph is either a tool
                    // the catalogue never gave us or a tool fitted somewhere absurd, and those
                    // want different fixes.
                    Debug.Log($"[Shot] tools — {figures.DescribeTools()}");
                    Debug.Log($"[Shot] reach {figures.MeasuredReach:F2} m, " +
                              $"blade gap {figures.MeasuredBladeGap:F2} m, " +
                              $"blade height {figures.MeasuredBladeHeight:F2} m, " +
                              $"sideways {figures.MeasuredStrikeSideways:F2} m");
                }

                // And a miner cutting the layer BELOW itself, which is the case the downward aim
                // was built for and the only one that can show whether it works.
                //
                // Kept separate from the shot above rather than folded into its preference order:
                // that one deliberately wants a SIDEWAYS cut, because a level swing is what its
                // measurements are about. These are two different poses and they want two
                // pictures, not one picture of whichever happened to be running.
                PawnView digger = default;
                bool foundDigger = false;
                for (int i = 0; i < published.Pawns.Length; i++)
                {
                    PawnView worker = published.Pawns[i];
                    if (!worker.Working || worker.JobDef != JobHandle.Mine) continue;
                    if (worker.WorkCell.Y >= worker.Cell.Y) continue;
                    digger = worker;
                    foundDigger = true;
                    break;
                }

                if (foundDigger)
                {
                    Vector3 toWork = CellMetrics.FloorCentre(digger.WorkCell)
                                   - CellMetrics.FloorCentre(digger.Cell);
                    toWork.y = 0f;
                    float sideOn = toWork.sqrMagnitude > 1e-4f
                        ? PawnPose.YawOf(toWork) + 90f
                        : 45f;

                    // Framed on the stone's own top face rather than on a waist height between
                    // the two: the whole question is whether the head reaches that face, and a
                    // frame centred a cell above it puts the answer at the bottom of the picture.
                    Vector3 face = CellMetrics.FloorCentre(digger.WorkCell)
                                 + Vector3.up * CellMetrics.SizeY;
                    Shoot(camera, face + Vector3.up * 0.9f, 8f, sideOn, 6.0f, "Logs/shot-miner-down.png");

                    Debug.Log($"[Shot] a miner at {digger.Cell} is cutting {digger.WorkCell}, " +
                              (toWork.sqrMagnitude > 1e-4f ? "from the rim" : "from directly on top"));
                }
                else
                {
                    Debug.Log("[Shot] no miner was cutting a layer below itself, so no downward shot");
                }

                // And a miner cutting the layer ABOVE itself, which is the owner's decision that
                // a pick goes overhead. Waited for rather than hoped for: the stance is one of
                // four and it is not the common one.
                {
                    // Made rather than waited for. Four stances share the work and this is not the
                    // common one, so 6,000 ticks of an ordinary colony went by without a single
                    // colonist happening to cut a ceiling. Marking a cell that can ONLY be reached
                    // from underneath is the honest way to photograph the stance that handles it.
                    for (int gz = 0; gz < size.SizeZ; gz++)
                    for (int gx = 0; gx < size.SizeX; gx++)
                    for (int gy = 1; gy < size.SizeY - 1; gy++)
                    {
                        int under = size.Index(gx, gz, gy);
                        int rock = under + size.LayerStride;
                        if (!grid.IsWalkable(under)) continue;
                        if (!designations.CanMine(rock)) continue;

                        // Only from below, which means none of the three stances the giver
                        // prefers: nothing walkable beside the rock on its own layer (beside),
                        // nothing walkable on the layer above it (the rim, and standing on top).
                        bool onlyFromUnder = true;
                        for (int dy = 1; dy <= 2 && onlyFromUnder; dy++)
                        for (int dz = -1; dz <= 1 && onlyFromUnder; dz++)
                        for (int dx = -1; dx <= 1 && onlyFromUnder; dx++)
                        {
                            if (dy == 1 && dx == 0 && dz == 0) continue;   // the rock itself
                            if (!size.Contains(gx + dx, gz + dz, gy + dy)) continue;
                            if (grid.IsWalkable(size.Index(gx + dx, gz + dz, gy + dy)))
                                onlyFromUnder = false;
                        }

                        if (!onlyFromUnder) continue;
                        designations.Designate(size.FromIndex(rock), Odyssey.Sim.Designations.DesignationKind.Mine);
                        Debug.Log($"[Shot] marked {size.FromIndex(rock)} which can only be cut " +
                                  $"from {size.FromIndex(under)} underneath it");
                        gz = size.SizeZ; gx = size.SizeX; break;
                    }

                    PawnView reacher = default;
                    bool foundReacher = false;
                    for (int waited = 0; waited < 12_000 && !foundReacher; waited++)
                    {
                        world.Tick();
                        figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                        figures.Evaluate(FrameSeconds);

                        var live = world.Views.Current.Pawns;
                        for (int i = 0; i < live.Length; i++)
                        {
                            PawnView who = live[i];
                            if (!who.Working || who.JobDef != JobHandle.Mine) continue;
                            if (who.WorkCell.Y <= who.Cell.Y) continue;
                            reacher = who;
                            foundReacher = true;
                            break;
                        }
                    }

                    if (foundReacher)
                    {
                        Vector3 toWork = CellMetrics.FloorCentre(reacher.WorkCell)
                                       - CellMetrics.FloorCentre(reacher.Cell);
                        toWork.y = 0f;
                        float sideOn = toWork.sqrMagnitude > 1e-4f
                            ? PawnPose.YawOf(toWork) + 90f
                            : 45f;

                        // Framed on the face it is reaching for: the BOTTOM of the cell above,
                        // which is the only part of it a person could ever touch.
                        Vector3 face = CellMetrics.FloorCentre(reacher.WorkCell);
                        Shoot(camera, face, 4f, sideOn, 7f, "Logs/shot-miner-up.png");
                        Debug.Log($"[Shot] a miner at {reacher.Cell} is cutting {reacher.WorkCell} " +
                                  "overhead");
                        Debug.Log($"[Shot] tools (raise) — {figures.DescribeTools()}");
                    }
                    else
                    {
                        Debug.Log("[Shot] no miner was cutting a layer above itself, so no reach shot");
                    }
                }

                // Spoil on the floor: what a dug-out cell actually leaves to look at.
                {
                    CellRef spoil = default;
                    int most = 0;
                    for (int i = 0; i < published.Things.Length; i++)
                    {
                        ThingView thing = published.Things[i];
                        if (thing.DefIndex != ItemIndex.Stone || thing.Stack <= most) continue;
                        most = thing.Stack;
                        spoil = thing.Cell;
                    }

                    if (most > 0)
                    {
                        // Steeply down and well back. The first attempt shot it from 4.5 m at 30
                        // degrees and spent most of the frame inside the rock face beside it:
                        // spoil lies at the foot of a wall, so anything near the horizontal is
                        // looking through the wall.
                        Shoot(camera, CellMetrics.FloorCentre(spoil) + Vector3.up * 0.3f,
                              55f, 35f, 9f, "Logs/shot-spoil.png");
                        Debug.Log($"[Shot] the biggest heap of stone is {most} at {spoil}");
                    }
                    else
                    {
                        Debug.Log("[Shot] no stone on the ground yet, so no spoil shot");
                    }
                }

                // Somebody on a shaft wall, which is the one pose with nothing under it.
                //
                // Worth its own frame because every fault it can have is invisible from anywhere
                // else: a climber drawn in the walk cycle, or turned to face north, or with its
                // arms at its sides, all look like an ordinary colonist until you notice it is
                // three metres up a hole. The frame is deliberately side on and close.
                {
                    // Waited for rather than hoped for. A drop costs a hundred ticks and a climb
                    // two hundred and seventy, so on any one frame of a five-colonist board the
                    // odds of catching somebody on a wall are poor — the first version of this
                    // shot simply reported that nobody was climbing, which says nothing at all
                    // about whether the pose works.
                    PawnView climber = default;
                    bool found = false;
                    for (int waited = 0; waited < 4_000 && !found; waited++)
                    {
                        world.Tick();
                        figures.Sync(world.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                        figures.Evaluate(FrameSeconds);

                        var live = world.Views.Current.Pawns;
                        for (int i = 0; i < live.Length; i++)
                        {
                            PawnView who = live[i];
                            if (who.MovePercent <= 20 || who.MovePercent >= 80) continue;
                            if (who.NextCell.Y == who.Cell.Y) continue;
                            if (who.Cell.Y < 0) continue;
                            climber = who;
                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        Vector3 between = (CellMetrics.FloorCentre(climber.Cell)
                                         + CellMetrics.FloorCentre(climber.NextCell)) * 0.5f;

                        // Looking AT the wall the colonist is on, so the rock is behind it and the
                        // camera is on the open side. A fixed bearing put the outcrop between the
                        // camera and the subject as often as not, and a photograph of a rock
                        // proves nothing about the pose behind it.
                        CellRef lower = climber.NextCell.Y < climber.Cell.Y
                            ? climber.NextCell : climber.Cell;
                        Vector3 toWall = Vector3.zero;
                        if (lower.X > 0 && grid.IsSolidTerrain(size.Index(lower.X - 1, lower.Z, lower.Y)))
                            toWall = Vector3.left;
                        else if (lower.X < size.SizeX - 1
                                 && grid.IsSolidTerrain(size.Index(lower.X + 1, lower.Z, lower.Y)))
                            toWall = Vector3.right;
                        else if (lower.Z > 0 && grid.IsSolidTerrain(size.Index(lower.X, lower.Z - 1, lower.Y)))
                            toWall = Vector3.back;
                        else if (lower.Z < size.SizeZ - 1
                                 && grid.IsSolidTerrain(size.Index(lower.X, lower.Z + 1, lower.Y)))
                            toWall = Vector3.forward;

                        float bearing = toWall == Vector3.zero ? 35f : PawnPose.YawOf(toWall);
                        Shoot(camera, between + Vector3.up * 1.4f, 10f, bearing, 11f, "Logs/shot-climb.png");
                        Debug.Log($"[Shot] a colonist is {climber.MovePercent}% of the way from " +
                                  $"{climber.Cell} to {climber.NextCell}");
                        Debug.Log($"[Shot] climbing — {figures.DescribeClimb()}");
                    }
                    else
                    {
                        Debug.Log("[Shot] nobody was mid-climb, so no climbing shot");
                    }
                }
                // The slice seen from the layer a miner is working on, which is the one view the
                // whole layer model exists for: what is ABOVE the active layer has to read, or a
                // player standing in a quarry cannot see the rock still over their head.
                {
                    // The tallest outcrop, viewed with the slice set at its foot so every cell of
                    // it above the first is drawn through the x-ray.
                    int tallest = -1, tallestTop = -1;
                    for (int z = 0; z < size.SizeZ; z++)
                    for (int x = 0; x < size.SizeX; x++)
                    {
                        int top = -1;
                        for (int y = size.SizeY - 1; y >= 0; y--)
                            if (grid.Terrain[size.Index(x, z, y)] == NaturalContent.TerrainRock) { top = y; break; }
                        if (top > tallestTop) { tallestTop = top; tallest = size.Index(x, z, top); }
                    }

                    if (tallest >= 0)
                    {
                        CellRef at = size.FromIndex(tallest);
                        int foot = Mathf.Max(0, at.Y - 2);
                        Debug.Log($"[Shot] the tallest rock is at {at}; slicing at L{foot} " +
                                  $"puts {at.Y - foot} layer(s) of it above the cut");

                        // Reported from the layer actually being cut at, because the treatment
                        // above now depends on it: below the surface it is one ceiling layer, at
                        // or above it every layer above. See SliceSettings.followDepth.
                        Debug.Log($"[Shot] x-ray at L{foot}: above={slice.AboveAt(foot)}, " +
                                  $"underground={slice.BelowSurface(foot)}, " +
                                  $"top visible L{slice.HighestVisibleLayer(foot, size.SizeY)}, " +
                                  $"alpha +1 {slice.AlphaAbove(foot, 1):0.00}, +2 {slice.AlphaAbove(foot, 2):0.00}, " +
                                  $"+3 {slice.AlphaAbove(foot, 3):0.00}, +4 {slice.AlphaAbove(foot, 4):0.00}");

                        shotLayer[0] = foot;
                        Shoot(camera, CellMetrics.FloorCentre(new CellRef(at.X, at.Z, foot)),
                              22f, 40f, 24f, "Logs/shot-xray.png");
                        shotLayer[0] = activeLayer;
                    }
                }

                // A grass-free twin of the horizon shot was tried here, swapping in a second
                // renderer with scatter off, and it drew grass anyway — on two consecutive
                // renders, with the swap plainly in place. Not a one-frame latency, then, and not
                // understood; it is left out rather than left in as a picture that lies. If it is
                // wanted again, prove the swap with a renderer that draws something unmistakable
                // before trusting it to draw nothing.

                Debug.Log("[Shot] wrote Logs/shot-play.png, Logs/shot-close.png, " +
                          "Logs/shot-down.png, Logs/shot-horizon.png, Logs/shot-rock.png, Logs/shot-miner.png");

                // Before the root goes: a playable graph bound to an Animator that has just been
                // destroyed under it complains, and the complaint would be the picture's epitaph.
                figures.Dispose();
                figures = null;

                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(lighting.gameObject);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Shot] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                figures?.Dispose();
                renderer?.Dispose();
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>The bounding box a resolved module actually occupies once placed, in metres.</summary>
        static string Describe(ResolvedModule module)
        {
            if (module.Parts.Length == 0) return "no geometry";

            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (ModulePart part in module.Parts)
            {
                Bounds b = part.Mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? b.min.x : b.max.x,
                        (corner & 2) == 0 ? b.min.y : b.max.y,
                        (corner & 4) == 0 ? b.min.z : b.max.z);
                    point = part.Local.MultiplyPoint3x4(point);
                    min = Vector3.Min(min, point);
                    max = Vector3.Max(max, point);
                }
            }

            Vector3 size = max - min;
            return $"{size.x:0.00} wide x {size.y:0.00} tall x {size.z:0.00} deep, base y {min.y:0.00}";
        }

        /// <summary>
        /// The standard three-quarter shot: the board camera's own bearing, which is the one
        /// every picture of the world should be judged in unless there is a reason otherwise.
        /// </summary>
        internal static void Shoot(Camera camera, Vector3 focus, float pitch, float distance, string path) =>
            Shoot(camera, focus, pitch, 45f, distance, path);

        /// <summary>
        /// The same, from a bearing of your choosing.
        ///
        /// Worth having because a three-quarter view cannot answer a question about a distance
        /// along one particular line — a colonist and the tree she is working on sit at different
        /// depths in it, so the gap between an axe head and a trunk can be read as anything you
        /// like. Side on to that line, it can only be read as what it is.
        /// </summary>
        internal static void Shoot(Camera camera, Vector3 focus, float pitch, float yaw, float distance, string path)
        {
            // **A camera made in script has post-processing switched off.** URP keeps it per
            // camera and defaults it to false, so every photograph this project has ever taken
            // was of an ungraded image — which did not matter while there was no volume to
            // apply, and matters entirely now that the warmth, the bloom and the vignette are
            // all in one. A contact sheet that does not show the grade is answering a question
            // nobody asked.
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            camera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);

            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2,
            };
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(Path.GetFullPath("Logs"));
            File.WriteAllBytes(Path.GetFullPath(path), image.EncodeToPNG());

            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }

        /// <summary>The play scene's own sun and ambient, for a tool that wants to judge art by it.</summary>
        public static void BuildSheetLighting(Transform root) => BuildLighting(root);

        /// <summary>
        /// A single three-quarter shot of whatever is already in the scene, framed on a point.
        ///
        /// For tools that assemble their own subject — a contact sheet of candidate props, say —
        /// and want it photographed under the game's lighting rather than under a studio setup
        /// that would flatter everything equally.
        /// </summary>
        public static void ShootAt(Vector3 focus, float span, string path)
        {
            var cameraObject = new GameObject("SheetCamera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);
                Shoot(camera, focus, 38f, span * 1.6f, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        /// <summary>
        /// The same measurement over the ruined city, which is what the renderer was built for.
        ///
        /// The barren meadow the scene loads has no walls in it, so every bucket number taken from
        /// it says nothing about the case the plan's U14 validation asks for — thousands of wall
        /// panels across several materials. This is the headless half of that validation: draw
        /// calls and instances for a stamped city slice. The other half, batch counts in the Frame
        /// Debugger, needs an editor window and an eye, and is listed as owner work.
        /// </summary>
        public static void MeasureCity() => MeasureInternal(Application.isBatchMode, MapType.RuinedCity);

        static void MeasureInternal(bool exitWhenDone, MapType mapType = MapType.Natural)
        {
            int exitCode = 0;
            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);

                // Deliberately the world the play scene builds, not a convenient small one. A
                // performance number for a map nobody loads answers no question worth asking, and
                // this measurement had quietly gone on describing a 60 x 60 city long after the
                // scene moved to a barren 120 x 120 wilderness.
                var size = new GridSize(PlaySizeXZ, PlaySizeXZ, PlayLayers);
                MapGenDef gen = MapGenerator.DefaultDef(mapType, size);
                if (gen is NaturalMapGenDef natural) natural.MakeWooded();
                var grid = new CellGrid(size);
                var chunks = new ChunkGrid(size);

                var clock = System.Diagnostics.Stopwatch.StartNew();
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                double genMs = clock.Elapsed.TotalMilliseconds;

                using var library = new ModuleLibrary(catalogue);
                var model = new Odyssey.Presentation.World.WorldRenderModel(size, chunks, library);
                // A city has stamped shells to resolve into modules first; a meadow has none.
                if (result.City != null) model.ApplyTemplates(result.City, gen);
                var edifices = result.City != null
                    ? result.City.Context.Edifices
                    : result.Natural!.Context.Edifices;
                string genReport = result.City != null
                    ? result.City.Report.ToString()
                    : result.Natural!.Report.ToString();
                model.RefreshAll(grid, edifices);

                // The layer the scene actually opens on: the air cell a colonist stands in, which
                // is one above the ground. Measuring the layer below it would quietly report the
                // cost of a slice the player never sees.
                int activeLayer = result.StartCell.Y;
                var renderer = new ChunkRenderer(model) { SubmitToGpu = false };
                // Benched with the player's own policy: at the surface that is every layer above,
                // not four, and measuring four would understate what the frame really costs.
                var slice = new SliceSettings { surfaceLayer = activeLayer };

                clock.Restart();
                renderer.Render(activeLayer, slice);
                double firstMs = clock.Elapsed.TotalMilliseconds;
                int meshed = renderer.ChunksMeshedThisFrame;

                clock.Restart();
                for (int i = 0; i < 100; i++) renderer.Render(activeLayer, slice);
                double steadyMs = clock.Elapsed.TotalMilliseconds / 100d;

                // A full re-mesh once everything is warm: the honest cost of rebuilding every
                // chunk of a slice, against the 4 ms budget in 06-rendering-and-camera.md section 4.
                model.RefreshAll(grid, edifices);
                clock.Restart();
                renderer.Render(activeLayer, slice);
                double remeshMs = clock.Elapsed.TotalMilliseconds;
                int remeshed = renderer.ChunksMeshedThisFrame;

                var report = new System.Text.StringBuilder();
                report.AppendLine($"[Measure] {mapType} {size} seed 1: worldgen {genMs:0.0} ms, {genReport}");
                report.AppendLine(
                    $"[Measure] first slice: {firstMs:0.00} ms including {meshed} chunk meshes; " +
                    $"steady submit {steadyMs:0.00} ms/frame; " +
                    $"warm full re-mesh of {remeshed} chunks {remeshMs:0.00} ms " +
                    $"({(remeshed > 0 ? remeshMs / remeshed : 0d):0.000} ms/chunk)");
                report.AppendLine(
                    $"[Measure] draw calls {renderer.DrawCalls}, instances {renderer.InstancesDrawn}, " +
                    $"chunks drawn {renderer.ChunksDrawn}, materials {renderer.MaterialCount}, " +
                    $"modules with art {library.ArtBackedCount()}/{library.Count - 1}");

                // The colonist figure is baked from a rigged character at load, and two things
                // about that bake can fail silently. If the pose clip does not retarget, the bake
                // captures the bind pose and every colonist stands in a T-pose, which shows up
                // here as a width near the 2 m arm span rather than near half a metre. If the
                // placement is wrong the figure sinks into the ground or floats above it, which
                // shows up as a base far from zero. Both are cheaper to read as numbers than to
                // hunt for on screen.
                ResolvedModule colonist = library[library.Resolve(ModuleIds.Colonist(0), ModuleShape.Pillar)];
                report.AppendLine(
                    $"[Measure] colonist: {colonist.Parts.Length} parts, art {colonist.UsesArt}, " +
                    Describe(colonist));

                for (int layer = 0; layer < size.SizeY; layer++)
                {
                    renderer.Render(layer, slice);
                    report.AppendLine(
                        $"[Measure] slice at layer {layer}: {renderer.DrawCalls} calls, " +
                        $"{renderer.InstancesDrawn} instances, {renderer.ChunksDrawn} chunks");
                }

                renderer.Dispose();
                Debug.Log(report.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[Measure] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        static void BuildInternal(bool exitWhenDone)
        {
            int exitCode = 0;
            try
            {
                ModuleCatalogue catalogue = BuildCatalogueAsset();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Odyssey").transform;

                BuildLighting(root);
                SliceCameraRig rig = BuildCamera(root);
                BuildBootstrap(root, rig, catalogue);

                Directory.CreateDirectory(Path.GetFullPath("Assets/Scenes"));
                AssetDatabase.Refresh();
                EditorSceneManager.SaveScene(scene, ScenePath);
                RegisterInBuildSettings();
                Debug.Log($"[PlayScene] saved {ScenePath}. " +
                          $"Catalogue: {catalogue.ResolvedPrefabCount()}/{catalogue.Entries.Count} rows have art.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayScene] failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        static void RebuildInternal(bool exitWhenDone)
        {
            int exitCode = 0;
            try
            {
                ModuleCatalogue catalogue = BuildCatalogueAsset();
                var missing = catalogue.Entries
                    .Where(e => e.prefab == null && !string.IsNullOrEmpty(e.prefabName))
                    .Select(e => e.prefabName)
                    .Distinct()
                    .ToList();
                Debug.Log($"[PlayScene] catalogue rebuilt at {CataloguePath}: " +
                          $"{catalogue.ResolvedPrefabCount()}/{catalogue.Entries.Count} rows have art" +
                          (missing.Count == 0 ? "." : $"; unresolved prefabs: {string.Join(", ", missing)}."));
            }
            catch (Exception e)
            {
                Debug.LogError($"[PlayScene] catalogue rebuild failed: {e}");
                exitCode = 1;
            }
            finally
            {
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        // ------------------------------------------------------------ catalogue

        /// <summary>
        /// The module table.
        ///
        /// Sources: <c>e-01-module-mapping.md</c> for which Synty piece plays which part, and
        /// <c>synty-inventory.csv</c> for the measurements that justify each one. The whole
        /// buildable kit is PolygonGeneric's <c>SM_Bld_Base_*</c> family, built on our exact pitch:
        /// walls 2.50 x 3.01, floors 2.50 x 2.50, a stair rising 1.50 m per 2.5 m run.
        ///
        /// Rows with an empty prefab name are deliberate, not gaps: underground strata are drawn
        /// as tinted blocks because no pack ships a cubic rock module, and a tinted block is what
        /// a cut-away of bedrock should look like anyway.
        /// </summary>
        /// <summary>
        /// Every character a colonist can be drawn as.
        ///
        /// **Why all of them.** A colony of five identical people is the most artificial thing on
        /// the board, and every character in the four packs is built on the same ~50-bone Polygon
        /// humanoid rig, so any of them can be a colonist for the price of a row — the locomotion
        /// clips retarget onto all of them with no per-character work at all
        /// (<c>e-02-characters-animation.md</c>). Sixty-odd faces out of art already paid for.
        ///
        /// **Why these and not literally every skinned prefab.** Four are excluded: the scarecrow,
        /// the skeleton, the two robots and the hologram are rigged humanoids but they are not
        /// people, and a colony with a skeleton hauling crates raises a question the game has no
        /// answer to yet. They are listed below the cast so reinstating one is a line move rather
        /// than a search. Everything else is in, including the aliens and the augmented, on the
        /// grounds that this is a ruined sci-fi city and a mixed population is the premise.
        ///
        /// The owner's veto is meant to be exercised here: strike a name, rebuild the catalogue,
        /// and that face stops appearing. Nothing else has to change.
        /// </summary>
        static readonly string[] Colonists =
        {
            // PolygonGeneric — the everyday population.
            "SM_Gen_Chr_Business_Female_01",
            "SM_Gen_Chr_Business_Male_01",
            "SM_Gen_Chr_Jumpsuit_Female_01",
            "SM_Gen_Chr_Jumpsuit_Male_01",
            "SM_Gen_Chr_Peasent_Female_01",
            "SM_Gen_Chr_Peasent_Male_01",
            "SM_Gen_Chr_Prisoner_Female_01",
            "SM_Gen_Chr_Prisoner_Male_01",
            "SM_Gen_Chr_Space_Male_01",
            "SM_Gen_Chr_Street_Female_01",
            "SM_Gen_Chr_Street_Female_02",
            "SM_Gen_Chr_Street_Female_03",
            "SM_Gen_Chr_Street_Female_04",
            "SM_Gen_Chr_Street_Male_01",
            "SM_Gen_Chr_Street_Male_02",
            "SM_Gen_Chr_Street_Male_03",
            "SM_Gen_Chr_Street_Male_04",

            // PolygonSciFiCity — the city this game is set in the ruins of.
            "SM_Chr_Alien_Male_01",
            "SM_Chr_Alien_Male_02",
            "SM_Chr_Android_Female_01",
            "SM_Chr_Augmented_Male_01",
            "SM_Chr_Cop_01",
            "SM_Chr_CyberPunk_Male_01",
            "SM_Chr_Cyber_Female_01",
            "SM_Chr_Cyber_Male_01",
            "SM_Chr_CyborgNinja_01",
            "SM_Chr_Garbage_Male_01",
            "SM_Chr_Hacker_Female_01",
            "SM_Chr_Junky_Female_01",
            "SM_Chr_Junky_Male_01",
            "SM_Chr_Medical_Male_01",
            "SM_Chr_Monk_Male_01",
            "SM_Chr_Muscle_Male_01",
            "SM_Chr_Rich_Female_01",
            "SM_Chr_Rich_Male_01",

            // PolygonFarm — survivors from outside the city.
            "SM_Chr_FarmBoy_01",
            "SM_Chr_FarmGirl_01",
            "SM_Chr_Farmer_Female_01",
            "SM_Chr_Farmer_Male_01",
            "SM_Chr_Farmer_Male_Old_01",

            // PolygonWesternFrontier — the same, further out.
            "SM_Chr_Bandit_Male_01",
            "SM_Chr_Captain_Male_01",
            "SM_Chr_GoldMiner_Male_01",
            "SM_Chr_GoldMiner_Male_02",
            "SM_Chr_Hunter_Male_01",
            "SM_Chr_Mexican_Female_01",
            "SM_Chr_Mexican_Male_01",
            "SM_Chr_Mexican_Male_02",
            "SM_Chr_NativeAmericanChief_Male_01",
            "SM_Chr_NativeAmericanElder_Female_01",
            "SM_Chr_NativeAmericanWarrior_Female_01",
            "SM_Chr_NativeAmericanWarrior_Male_01",
            "SM_Chr_NativeAmericanWarrior_Male_02",
            "SM_Chr_NativeAmericanWarrior_Male_03",
            "SM_Chr_NativeAmerican_Female_01",
            "SM_Chr_Priest_Male_01",
            "SM_Chr_Salesman_Male_01",
            "SM_Chr_Soldier_Male_01",
            "SM_Chr_Thug_Male_01",
            "SM_Chr_Thug_Male_02",
            "SM_Chr_Traveller_Female_01",

            // Rigged, humanoid, and deliberately left out until the game can say what they are:
            //   SM_Chr_Scarecrow_01, SM_Gen_Chr_Skeleton_01, SM_Gen_Chr_Charred_01,
            //   SM_Gen_Chr_Robot_01, SM_Chr_Robot_01, SM_Chr_Hologram_Female_01,
            //   SM_Gen_Chr_Underwear_Female_01, SM_Gen_Chr_Underwear_Male_01
        };

        static List<ModuleEntry> Rows()
        {
            var rows = new List<ModuleEntry>();

            void Wall(string id, string prefab) => rows.Add(new ModuleEntry
            {
                moduleId = id, shape = ModuleShape.WallPanel, prefabName = prefab,
                centreXZ = true, baseAtY = true,
            });

            void Slab(string id, string prefab) => rows.Add(new ModuleEntry
            {
                moduleId = id, shape = ModuleShape.FloorSlab, prefabName = prefab,
                // The walking surface is the cell floor and the slab's own thickness hangs below
                // it — so it is the slab's TOP that is placed, never its base. baseAtY would lift
                // every floor in the world by its own thickness.
                //
                // **That intent used to be spelled `baseAtY = false`, which is not the same thing**
                // and was only ever right by luck: with neither rule on, a piece lands on whatever
                // pivot convention its own artist used. Measured 2026-09-18 (`SlabHeightProbe`),
                // the plank deck's top came out at +0.008 m and the street tile's at +0.033 m, so a
                // stone floor stood 25 mm proud of the wood beside it on the same layer, and the
                // owner reported a grey tile sitting at the wrong height in their deck. One rule
                // with one owner now: every slab's top face is the same height.
                //
                // The clearance above the plane is topAtY's own, not a number repeated here —
                // CellMetrics.SlabLift carries it and says why it cannot be zero.
                centreXZ = true, baseAtY = false, topAtY = true,
            });

            void Block(string id) => rows.Add(new ModuleEntry
            {
                moduleId = id, shape = ModuleShape.SolidBlock, prefabName = string.Empty,
            });

            // Standing decoration on the ground surface. Its base sits on the cell top, the mesher
            // having already placed it there, and it is centred so the scatter offset is measured
            // from the middle of the clump rather than from whichever corner the artist modelled
            // it around.
            void Tuft(string id, string prefab, float size) => rows.Add(new ModuleEntry
            {
                moduleId = id, shape = ModuleShape.Pillar, prefabName = prefab,
                centreXZ = true, baseAtY = true,
                scale = new Vector3(size, size, size),
            });

            // A cell-shaped box wearing a tiling terrain texture. See the note above the natural
            // terrain rows for why this is the one kind of pack material a box may wear.
            void Ground(string id, string material) => rows.Add(new ModuleEntry
            {
                moduleId = id, shape = ModuleShape.SolidBlock, prefabName = string.Empty,
                materialName = material,
                // One repeat per cell, so a cell reads as one tile laid on the floor rather than
                // as a patch of fine noise, and no normal map, so the tile is lit flatly from
                // above instead of being broken into a bumpy mottle.
                materialTilesPerCell = 1f, flattenNormalMap = true,
            });

            // A run of chipped stone lumps sharing one material, one row per variant. Variant 0
            // keeps the unsuffixed terrain id, so anything that asks for plain "terrain.rock"
            // still gets an answer.
            void StoneVariants(string id, string material)
            {
                for (int v = 0; v < RockMesh.Variants; v++)
                {
                    string variantId = v <= 0 ? id : id + "." + v.ToString();
                    rows.Add(new ModuleEntry
                    {
                        moduleId = variantId, shape = ModuleShape.RockBlock, prefabName = string.Empty,
                        materialName = material,
                        materialTilesPerCell = 1f, flattenNormalMap = true,
                    });
                }
            }

            // Walls, windows and doors. Several template ids share one piece for now; the point of
            // the catalogue is that giving the tower its own curtain wall is an edit here.
            Wall(ModuleIds.Wall, "SM_Bld_Base_Wall_01");
            Wall("odyssey.module.wall.panel", "SM_Bld_Base_Wall_01");
            Wall("odyssey.module.wall.block", "SM_Bld_Base_Wall_01");
            Wall("odyssey.module.wall.curtain", "SM_Bld_Base_Wall_Window_Double_01");
            Wall(ModuleIds.Window, "SM_Bld_Base_Wall_Window_01");
            Wall("odyssey.module.wall.window", "SM_Bld_Base_Wall_Window_01");
            Wall("odyssey.module.wall.shopfront", "SM_Bld_Base_Wall_Window_Double_01");
            Wall("odyssey.module.wall.glazed", "SM_Bld_Base_Wall_Window_Double_01");
            Wall(ModuleIds.VaultWall, "SM_Bld_Base_Wall_01");

            Wall(ModuleIds.Door, "SM_Bld_Base_Wall_Door_Large_01");
            Wall("odyssey.module.door.single", "SM_Bld_Base_Wall_Door_Large_01");
            Wall("odyssey.module.door.double", "SM_Bld_Base_Wall_Door_Double_01");
            Wall("odyssey.module.door.lobby", "SM_Bld_Base_Wall_Door_Double_Large_01");
            Wall(ModuleIds.DoorLeaf, "SM_Bld_Base_Door_Large_01");

            rows.Add(new ModuleEntry
            {
                moduleId = ModuleIds.Pillar, shape = ModuleShape.Pillar,
                prefabName = "SM_Bld_Base_Pillar_01",
            });

            // Half a flight per cell: 1.50 m of rise over a 2.5 m run, so two cells climb one
            // 3.0 m layer, which is the rule the templates are authored to. The piece carries a
            // 0.33 m skirt below its pivot that must sink into the slab, so no base snapping.
            foreach (string id in new[] { ModuleIds.Stair, "odyssey.module.stair.straight", "odyssey.module.stair.core" })
                rows.Add(new ModuleEntry
                {
                    moduleId = id, shape = ModuleShape.StairFlight, prefabName = "SM_Bld_Base_Stairs_01",
                    centreXZ = true, baseAtY = false,
                });

            // Exactly one layer tall, pushed back against the wall it is fixed to.
            foreach (string id in new[] { ModuleIds.Ladder, "odyssey.module.ladder.fixed" })
                rows.Add(new ModuleEntry
                {
                    moduleId = id, shape = ModuleShape.Ladder, prefabName = "SM_Gen_Bld_Ladder_01",
                    centreXZ = true, baseAtY = true, offset = new Vector3(0f, 0f, -1.15f),
                });

            Slab(ModuleIds.Slab, "SM_Bld_Base_Floor_Combined_01");
            Slab("odyssey.module.slab.concrete", "SM_Bld_Base_Floor_Combined_01");
            Slab("odyssey.module.slab.deck", "SM_Bld_Base_Floor_Combined_01");

            // Slab art per material, which the three ids above cannot express: they are the
            // *template's* floor, and a colonist chooses the material after the template is
            // stamped. All three resolve to the planked deck, so before this a stone floor was
            // that deck under a 0.86 grey multiply and read as wood (owner, 2026-09-17).
            //
            // Wood keeps the deck — it is what the deck mesh already is, and the owner liked it.
            // Stone takes the street tile, which is one cell square, one material and already
            // in the build as Pavement, so its look is known rather than guessed.
            Slab(ModuleIds.SlabOf("wood"), "SM_Bld_Base_Floor_Combined_01");
            Slab(ModuleIds.SlabOf("stone"), "SM_Env_Ground_Tile_Half_01");

            rows.Add(new ModuleEntry
            {
                moduleId = ModuleIds.UtilityTap, shape = ModuleShape.Pillar, prefabName = string.Empty,
            });

            // The campfire (design 31 §3). The owner's report was that it drew as a wooden block,
            // on the ghost and on the board alike, which it did: ModuleIds.Campfire had no row, so
            // it resolved to the SolidBlock fallback. This is the row its own comment promised —
            // "one row on this id upgrades every campfire when the art arrives" — and it fixes
            // both surfaces at once, because the ghost and the mesher ask ModuleForEdificeAt the
            // same question and place the answer with the same Drape(FloorCentre) transform.
            //
            // **The big ring, scaled, and not the small one.** SM_Prop_Campfire_Small_01 is the
            // one that fits a 2.5 m cell unaided at 1.29 m — but it carries a SECOND renderer,
            // SM_Prop_Campfire_Pot_01, a cooking pot on a tripod, and FlattenPrefab takes every
            // MeshFilter under a prefab. There is no per-part exclusion on ModuleEntry, so that
            // prefab puts a cooking pot on every campfire in the colony. It would also be wrong
            // on the merits: nothing cooks yet, no stove exists in any owned pack, and design 28
            // describes this as kindling and a ring of stones for 3 wood.
            //
            // SM_Prop_Campfire_01 is one renderer, 714 triangles, no pot, and 3.28 m across —
            // 0.78 m wider than the cell. 0.70 brings it to 2.30 m, which sits inside the cell
            // with a margin at the corners, where the ring is widest. centreXZ and baseAtY do the
            // rest: the pack's pivot convention is neutralised once here rather than once per
            // instance.
            rows.Add(new ModuleEntry
            {
                moduleId = ModuleIds.Campfire, shape = ModuleShape.Pillar,
                prefabName = "SM_Prop_Campfire_01",
                centreXZ = true, baseAtY = true,
                scale = new Vector3(0.70f, 0.70f, 0.70f),
            });

            // Street surfaces: the half tiles are exactly one cell square.
            Slab(ModuleIds.Terrain("Pavement"), "SM_Env_Ground_Tile_Half_01");
            Slab(ModuleIds.Terrain("CrackedPavement"), "SM_Env_Ground_Tile_Half_02");
            Slab(ModuleIds.Terrain("Rubble"), "SM_Env_Ground_Tile_Half_03");
            Slab(ModuleIds.Terrain("Soil"), "SM_Env_Ground_Tile_Half_04");
            Slab(ModuleIds.Terrain("Gravel"), "SM_Env_Ground_Tile_Half_05");

            // Strata. Tinted blocks on purpose: no pack has a cubic rock module, and a cut-away of
            // bedrock is a coloured mass, not a prop.
            Block(ModuleIds.Terrain("EngineeredFill"));
            // Rock is shared with the wilderness map and is registered once, below, as a run of
            // chipped lumps. A second row here would shadow the first variant and leave one cell
            // in six a smooth cube among five jagged ones.
            Block(ModuleIds.Terrain("BuriedSeam"));
            Block(ModuleIds.Terrain("Salvage"));

            // ---- natural wilderness (ADR 0008) --------------------------------------------
            // Ground and strata stay tinted blocks for the same reason the city strata do: the
            // Nature Biomes pack has no cubic ground module. Its "grass planes" measure
            // 1.00 x h x 0.00 — they are standing grass cards for scattering, not tiles. The
            // colours that make these read as landscape are in StuffPalette.TerrainSolids.
            // Ground uses the square tile family, which measures exactly 5.00 x 5.00 — two cells
            // — so scaling by half gives a seamless one-cell tile with no gaps and no overlap.
            //
            // An earlier attempt used SM_Gen_Env_Ground_Grass_01. That is an organic patch with a
            // rounded outline, authored to be strewn across a landscape, and tiling it produced
            // circles across the map. Square art for a square grid; scatter art stays scatter.
            //
            // Two attempts at using pack art then failed, and the reason they failed is the
            // reason the current approach works. Pointing a cell at a flat ground-tile prefab drew
            // a thin plane inside every cell that z-fought with its neighbours: flicker and stray
            // shapes. Taking only that prefab's material failed worse: Synty *props* are UV-mapped
            // into a shared colour atlas, so the material on a cell-sized box samples the entire
            // atlas across each face rather than the grass swatch, which is exactly where the
            // stray blades and the dark patches came from.
            //
            // The Nature Biomes pack also ships **terrain** materials, which are a different kind
            // of asset altogether: ordinary tiling textures with no atlas, authored for Unity
            // terrain layers. One of those worn by the cell-shaped box is what ground should have
            // been all along. Each face carries one unit of UV, the texture tiles twice across it,
            // and it is authored to wrap, so the pattern continues across a cell boundary instead
            // of restarting. The result reads as a field rather than a grid of stamps.
            //
            // A clone without the packs resolves these to null and gets the flat tints in
            // StuffPalette.TerrainSolids, which is still a perfectly legible landscape.
            Ground(ModuleIds.Terrain("Grass"), "Mat_Grass_Textures_01");
            Ground(ModuleIds.Terrain("BareEarth"), "Mat_Dirt_01");
            Ground(ModuleIds.Terrain("PackedGravel"), "Mat_Gravel_01");
            Ground(ModuleIds.Terrain("Subsoil"), "Mat_Mud_01");

            // Marsh, restored 2026-09-16. The committed catalogue carried this row and the
            // builder had stopped emitting it, so the asset and its generator disagreed and the
            // next person to rebuild the catalogue would silently have taken marsh's texture away
            // — which is precisely the fault the water work went and fixed, an untextured dark
            // olive slab that reads as shadow rather than as ground. Found by rebuilding the
            // catalogue with the packs present and reading the diff line by line.
            Ground(ModuleIds.Terrain("Marsh"), "Mat_Dirt_01");

            // No meadow texture reads as these, and a wrong texture is worse than an honest
            // colour: sand would come out as mud, and an ore seam has to stay findable at a
            // glance. They keep their tints until a pack with the right ground arrives.
            Block(ModuleIds.Terrain("Sand"));

            // Stone: one row per lump, because each lump is its own module. The material is the
            // same on all of them — what varies is the geometry, which is ours and needs no pack,
            // so a clone without the art still gets chipped rock in a flat colour.
            //
            // Rock wears the rough face rather than Mat_Rock_01. The smooth one is a warm brown
            // that reads as earth at board distance, which is what made an outcrop look like a
            // mud-brick; on the rough one the same tint reads as stone.
            StoneVariants(ModuleIds.Terrain("Rock"), "Mat_Rock_Rough_01");
            StoneVariants(ModuleIds.Terrain("Bedrock"), "Mat_Rock_Rough_01");
            StoneVariants(ModuleIds.Terrain("IronOre"), string.Empty);
            StoneVariants(ModuleIds.Terrain("CoalSeam"), string.Empty);

            // Tufts of grass strewn over the ground. Chosen on triangles per square metre of
            // cover, because there is one of these on nearly every one of fourteen thousand
            // cells and nothing else in the world is instanced that heavily.
            //
            // The Nature Biomes meadow set, and it is worth saying why it rather than the grass in
            // the other three packs. Those are stands of thin blades — reeds, authored to edge a
            // pond — and strewn over a field they read as a marsh. These are clumps: 1.9 m across,
            // which is most of a 2.5 m cell, for fifty triangles, in a short, a medium and a tall
            // built on the same footprint. Height variation from one silhouette family is exactly
            // what a meadow wants, and all three were picked off a contact sheet rather than off
            // their names (Odyssey > Presentation > Shoot scatter sheet).
            //
            // No scale correction: they are already sized for this grid, which is the other half
            // of why they were chosen.
            Tuft(ModuleIds.GrassTuftA, "SM_Env_Grass_Med_Clump_03", 1.0f);
            // Med_Clump_02 replaced Short_Clump_03 here: the short clump is a flat olive-brown
            // patch that reads as dry grass up close and as a dark spot at board distance, and
            // the far field was collecting dark spots. The contact sheet shows Med_Clump_02 as a
            // lighter, fuller tuft on the same footprint.
            Tuft(ModuleIds.GrassTuftB, "SM_Env_Grass_Med_Clump_02", 1.0f);
            Tuft(ModuleIds.GrassTuftC, "SM_Env_Grass_Tall_Clump_03", 1.0f);

            // A crop's drawn stages: sprout, half-grown, mature, one row each, standing on the
            // soil like a tuft does. The ids are the PlantDef's own module ids, so this table and
            // the simulation read from one copy of the names.
            //
            // Pillow, and why: the fallback box for a pillow spans the unit cube with its scale
            // reading directly in metres, so one row sizes both the art and the primitive a
            // pack-less clone draws — and a rounded mound is the honest stand-in for a leafy
            // plant, where a pillar would be a green stake. The per-stage scale keeps the real
            // art near its authored size (measured 0.31/0.44/0.63 m across in the inventory) and
            // the fallbacks a visible quarter/half/metre mound, so the stages still read without
            // the packs.
            void Crop(string id, string prefab, float size, float sink = 0f) => rows.Add(new ModuleEntry
            {
                moduleId = id, shape = ModuleShape.Pillow, prefabName = prefab,
                centreXZ = true, baseAtY = true,
                scale = new Vector3(size, size, size),
                offset = new Vector3(0f, -sink, 0f),
            });

            // Scale one throughout (owner, 2026-09-18: "there is a carrot in multiple grow stages
            // from planting, growing, to sprouting and popping out - use this through the
            // different stages"): the S/M/L prefabs are the stages, staged by the artist, and a
            // second scaling on top shrinks a sprout to a speck. The size ladder these rows used
            // to carry (0.5/0.75/1.0) was authored for the Pillow fallbacks, which it still
            // serves by shape alone - a mound grows only by being drawn at three stages' heights,
            // and losing that on a pack-less checkout is the cheaper half of the trade.
            // Bigger and sunk (owner, 2026-09-18: "the carrots need to be much bigger and inset
            // into the ground to be pulled out"): scale 1.4 with a quarter-metre of the root
            // below the soil line, so the mature carrot reads as sitting IN the field rather
            // than on it. Both apply per plant, so a plot's whole yield sinks alike.
            // The sink is the STAGE's, not the cell's: a stage-one sprout is barely a hand tall,
            // and the first cut sank it 0.15 m under the soil it had just broken - whole plots
            // showed one carrot where five grew (owner, 2026-09-19). Young plants sit on the
            // ground; only the mature root insets, there to be pulled.
            Crop("odyssey.module.carrot.s", "SM_Prop_Carrot_01_S", 1.4f, sink: 0f);
            Crop("odyssey.module.carrot.m", "SM_Prop_Carrot_01_M", 1.4f, sink: 0.05f);
            Crop("odyssey.module.carrot.l", "SM_Prop_Carrot_01_L", 1.4f, sink: 0.25f);


            // Trees are the pieces that actually make this look like a place. Measured widths
            // decide the casting: the pines are 1.78–2.12 m and sit inside a 2.5 m cell, while the
            // broadleaf trees run 2.74–4.32 m. Tree_03 at 2.74 m is the closest fit, and a little
            // overspill between neighbouring trees reads as canopy rather than as error.
            rows.Add(new ModuleEntry
            {
                moduleId = "odyssey.module.tree.conifer", shape = ModuleShape.Pillar,
                prefabName = "SM_Gen_Env_Tree_Pine_01", centreXZ = true, baseAtY = true,
            });
            rows.Add(new ModuleEntry
            {
                moduleId = "odyssey.module.tree.broadleaf", shape = ModuleShape.Pillar,
                prefabName = "SM_Gen_Env_Tree_03", centreXZ = true, baseAtY = true,
            });

            // The axe a colonist swings while felling. One row, held by whoever is working: it is
            // parented to a hand rather than placed in a cell, so it needs no shape, no centring
            // and no base — the hand decides where it is.
            //
            // The Generic pack's, and not the Farm or Western Frontier tool of the same name,
            // because Generic is already the pack the trees come from and a felling axe wants to
            // read as a tool rather than as a weapon or as set dressing for a barn.
            rows.Add(new ModuleEntry
            {
                moduleId = ModuleIds.ToolAxe, shape = ModuleShape.Pillar,
                prefabName = "SM_Gen_Wep_Axe_01",
            });

            // The pick, from the same pack for the same reasons, and chosen over two alternatives
            // on measurements rather than taste (12-work-poses-and-tools.md §5): 0.53 x 0.74 x
            // 0.09 against the axe's 0.27 x 0.74 x 0.08 — the same haft length to the centimetre
            // and the same pivot convention, so it is the one pick in the packs already known to
            // suit the fitting path. Western Frontier's is 0.98 m of haft, which reads as a sledge
            // at board height, pivots at the butt rather than mid-haft, and has its filename
            // misspelt in the pack, which is a permanent trap for anyone grepping.
            //
            // One caution recorded where it will be read: GripTool finds the haft as the long axis
            // of the bounds, and the pick's margin is 0.74 against 0.53 — 1.4 : 1, against the
            // axe's 2.7 : 1. The right axis still wins, but a pick modelled a hand longer in the
            // head would be gripped by its own point, and it would look deliberate.
            rows.Add(new ModuleEntry
            {
                moduleId = ModuleIds.ToolPickaxe, shape = ModuleShape.Pillar,
                prefabName = "SM_Gen_Wep_Pickaxe_01",
            });

            // The builder's hammer, and the first prop in the project that could not be chosen on
            // merit, because there is no choice: SM_Wep_Hammer_01 is the only hammer in all 7,222
            // imported assets. The axe and the pick were both picked from Generic over Farm and
            // Western Frontier alternatives; here Western Frontier is the whole field.
            //
            // What that costs, recorded rather than discovered: 768 triangles against the axe's
            // 172 and the pick's 160, and two materials against their one, from a pack nothing
            // else in the game draws from. None of it matters much — a tool is one instantiated
            // prefab parented to a hand while the work lasts, not a chunk-instanced module, so it
            // adds a material and not a draw-call bucket, and at most one per working colonist.
            //
            // Two measurements that bear on the fitting path, both from synty-inventory.csv:
            //
            //   Haft ratio 0.63 : 0.21, or 3 : 1. GripTool finds the haft as the long axis of the
            //   combined bounds, and this is a wider margin than the pick's 1.4 : 1 — so of the
            //   three tools the hammer is the one least likely to be gripped by its own head.
            //
            //   The pivot is at the butt (minY 0.00, maxY 0.63) where the axe and pick sit
            //   mid-haft (-0.18 to 0.56). 12-work-poses-and-tools.md rejected Western Frontier's
            //   pickaxe partly for this. It should not in fact matter: the fitting works off the
            //   mesh bounds and slides the tool until the grip is in the palm, so where the
            //   modeller put the origin never enters the arithmetic. This is the first prop to
            //   prove that, which is worth knowing when the contact sheet is judged — a hammer
            //   held a hand's width out of the fist means the claim is wrong.
            rows.Add(new ModuleEntry
            {
                moduleId = ModuleIds.ToolHammer, shape = ModuleShape.Pillar,
                prefabName = "SM_Wep_Hammer_01",
            });

            // Colonists. A Synty character is a rigged humanoid with no MeshFilter anywhere on it,
            // so the ordinary prefab path finds no geometry at all and quietly falls back to a
            // grey box; ModuleLibrary.CollectSkinned explains why baking is the way out and what
            // it costs. The pose clip matters as much as the mesh: bake without one and every
            // colonist stands in a T-pose.
            //
            // Measured, not guessed: the character is 1.79 m to the crown with its feet on the
            // prefab origin, against a cell 2.5 m square and 3.0 m tall. At true scale that is a
            // few pixels once the camera pulls back, which is how five colonists managed to be
            // invisible before. The board view wants them read at a glance, so they are drawn
            // half again as large, which brings them to 2.5 m and still leaves headroom in a cell.
            for (int variant = 0; variant < Colonists.Length; variant++) Colonist(variant);

            // One row per face a colonist can wear. See Colonists for the cast and the argument.
            void Colonist(int variant)
            {
                string prefab = Colonists[variant];

                // The locomotion pack ships every clip masculine and feminine, and the packs name
                // their characters, so the two can simply be matched up. It costs one string test
                // and it is the difference between a colony of people and a colony of people half
                // of whom walk like somebody else.
                bool feminine = prefab.IndexOf("Female", StringComparison.OrdinalIgnoreCase) >= 0
                                || prefab.IndexOf("Girl", StringComparison.OrdinalIgnoreCase) >= 0;
                string suffix = feminine ? "Femn" : "Masc";

                rows.Add(new ModuleEntry
                {
                    moduleId = ModuleIds.Colonist(variant), shape = ModuleShape.Pillar,
                    prefabName = prefab,
                    poseClipName = $"A_Idle_Standing_{suffix}",
                    centreXZ = true, baseAtY = true,
                    scale = new Vector3(1.4f, 1.4f, 1.4f),
                    locomotion = new List<LocomotionEntry>
                    {
                        new LocomotionEntry { clipName = $"A_Idle_Standing_{suffix}", metresPerSecond = 0f },
                        new LocomotionEntry
                        {
                            clipName = $"A_Walk_F_{suffix}",
                            speedFromClipName = $"A_Walk_F_RootMotion_{suffix}",
                        },
                        new LocomotionEntry
                        {
                            clipName = $"A_Run_F_{suffix}",
                            speedFromClipName = $"A_Run_F_RootMotion_{suffix}",
                        },
                    },
                });
            }


            // Loose items on the ground. Before these rows existed every item fell through to the
            // stand-in marker, so a scenario that scatters twelve ration stacks and eight pieces
            // of salvage painted twenty orange boxes across a barren map — the first thing anyone
            // noticed about the scene, and not in a good way.
            //
            // What each piece is has to be readable from the silhouette, because at board-camera
            // height colour tells you very little and outline tells you nearly everything: a
            // rounded stack of ration sacks against a square crate of stripped machine parts.
            // Both were chosen by rendering them and looking. The pack's "supplies" box is in
            // fact full of scrap components, which reads as salvage and not at all as food, and a
            // first pass that used it for meals had the two exactly the wrong way round.
            //
            // Measured against the 2.5 m cell, as everything here is. The sack stack is
            // 1.00 x 0.96 x 1.00 and the crate 0.80 x 0.78 x 0.73; at true scale both read as
            // litter dropped on a field, so each is drawn about half again as large and still
            // leaves a clear margin inside the cell.
            rows.Add(new ModuleEntry
            {
                moduleId = ModuleIds.ItemMeal, shape = ModuleShape.Pillar,
                prefabName = "SM_Gen_Prop_Sack_Stack_01",
                centreXZ = true, baseAtY = true,
                scale = new Vector3(1.6f, 1.6f, 1.6f),
            });
            rows.Add(new ModuleEntry
            {
                moduleId = ModuleIds.ItemSalvage, shape = ModuleShape.Pillar,
                prefabName = "SM_Prop_Box_Supplies_01",
                centreXZ = true, baseAtY = true,
                scale = new Vector3(1.5f, 1.5f, 1.5f),
            });
            // Felled wood. The Western Frontier log pile is 5.71 m long, so at 0.4 it lies 2.3 m
            // across the cell and half a metre high: a stack of logs on the ground, which is
            // exactly what a felled tree leaves. The short logs in the same pack are litter.
            rows.Add(new ModuleEntry
            {
                moduleId = ModuleIds.ItemWood, shape = ModuleShape.Pillar,
                prefabName = "SM_Prop_LogPile_01",
                centreXZ = true, baseAtY = true,
                scale = new Vector3(0.4f, 0.4f, 0.4f),
            });

            // What a mine leaves. No pack contains ore, so all three are rock, and the job these
            // rows do is to make them three *different* rocks, chosen for silhouette because that
            // is the only axis available — items are drawn by the actor pass with no per-item
            // tint, so colour cannot tell them apart. All three are recorded as art gaps in the
            // registry and want real tiles eventually.
            //
            // **One row is one rock, not one pile.** These three are drawn several times over by
            // ItemHeap, scattered across the cell floor, with the count reading the stack size —
            // so every scale here is the size of a single lump you could carry, and a full
            // stockpile square is seven of them rather than one enormous one. The first version
            // used SM_Prop_StonePile_01, a 1.48 m cairn, which is the shape of a monument and not
            // of eight stone knocked off a rock face.
            rows.Add(new ModuleEntry
            {
                // 1.37 x 1.04 x 1.14 at source; 0.48 x 0.36 x 0.40 at this scale. A squat lump
                // about shin high — the plainest "grey rock" in any pack we own.
                moduleId = ModuleIds.ItemStone, shape = ModuleShape.Pillar,
                prefabName = "SM_Gen_Env_Rock_03",
                centreXZ = true, baseAtY = true,
                scale = new Vector3(0.35f, 0.35f, 0.35f),
            });
            rows.Add(new ModuleEntry
            {
                // 2.12 x 3.38 x 2.82 at source; 0.34 x 0.54 x 0.45 here. Taller than it is wide,
                // where stone is wider than it is tall: ore reads as shards split off a seam.
                moduleId = ModuleIds.ItemIronOre, shape = ModuleShape.Pillar,
                prefabName = "SM_Gen_Env_Rock_08",
                centreXZ = true, baseAtY = true,
                scale = new Vector3(0.16f, 0.16f, 0.16f),
            });
            rows.Add(new ModuleEntry
            {
                // 1.11 x 0.30 x 0.89 at source; 0.55 x 0.15 x 0.45 here. Low rubble, and the
                // flattest of the three, so a coal pile is never mistaken for a stone one.
                moduleId = ModuleIds.ItemCoal, shape = ModuleShape.Pillar,
                prefabName = "SM_Gen_Env_Rock_Pebbles_02",
                centreXZ = true, baseAtY = true,
                scale = new Vector3(0.5f, 0.5f, 0.5f),
            });
            // A harvest dropped on the field. The heap pass scatters one lump per few carrots in
            // the stack, so the row is one carrot you could carry, not a pile: the mature crop's
            // own art at 0.6 is about 0.38 m across, in the band the ore lumps sit in. Pillow for
            // the reason the crop stages give — a mound is the honest fallback for a vegetable,
            // where the pillar's stake is the shape of a signpost.
            rows.Add(new ModuleEntry
            {
                moduleId = ModuleIds.ItemCarrots, shape = ModuleShape.Pillow,
                prefabName = "SM_Prop_Carrot_01_L",
                centreXZ = true, baseAtY = true,
                scale = new Vector3(0.6f, 0.6f, 0.6f),
            });

            return rows;
        }

        static ModuleCatalogue BuildCatalogueAsset()
        {
            var rows = Rows();
            var cache = new Dictionary<string, GameObject?>(StringComparer.Ordinal);
            foreach (ModuleEntry row in rows)
            {
                if (string.IsNullOrEmpty(row.prefabName)) continue;
                if (!cache.TryGetValue(row.prefabName, out GameObject? prefab))
                {
                    prefab = FindSyntyPrefab(row.prefabName);
                    cache[row.prefabName] = prefab;
                }
                row.prefab = prefab;
            }

            var materials = new Dictionary<string, Material?>(StringComparer.Ordinal);
            foreach (ModuleEntry row in rows)
            {
                if (string.IsNullOrEmpty(row.materialName)) continue;
                if (!materials.TryGetValue(row.materialName, out Material? material))
                {
                    material = FindSyntyMaterial(row.materialName);
                    materials[row.materialName] = material;
                }
                row.material = material;
            }

            var clips = new Dictionary<string, AnimationClip?>(StringComparer.Ordinal);
            foreach (ModuleEntry row in rows)
            {
                if (string.IsNullOrEmpty(row.poseClipName)) continue;
                if (!clips.TryGetValue(row.poseClipName, out AnimationClip? clip))
                {
                    clip = FindSyntyClip(row.poseClipName);
                    clips[row.poseClipName] = clip;
                }
                row.poseClip = clip;
            }

            ResolveGaits(rows, clips);

            var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(CataloguePath);
            if (catalogue == null)
            {
                catalogue = ScriptableObject.CreateInstance<ModuleCatalogue>();
                Directory.CreateDirectory(Path.GetFullPath(Path.GetDirectoryName(CataloguePath)!));
                AssetDatabase.CreateAsset(catalogue, CataloguePath);
            }
            catalogue.SetEntries(rows);
            EditorUtility.SetDirty(catalogue);
            AssetDatabase.SaveAssets();
            return catalogue;
        }

        /// <summary>
        /// Resolve each gait's clip, and calibrate how fast it covers ground.
        ///
        /// **Why the speed is read from a different clip than the one that plays.** The pack ships
        /// every locomotion clip twice: once in place, and once with root motion. A pawn plays the
        /// in-place twin, because the simulation owns where anybody is. But an in-place clip
        /// travels nowhere by definition, so it cannot say how long its own stride was — and that
        /// length is exactly what decides whether the feet grip the ground or skate over it. The
        /// root-motion twin does move, and its average root velocity is that number, sitting in
        /// the art where nobody has to guess it.
        ///
        /// A gait declaring no twin keeps whatever speed the row set by hand, which is how idle
        /// stays at zero.
        /// </summary>
        static void ResolveGaits(List<ModuleEntry> rows, Dictionary<string, AnimationClip?> clips)
        {
            foreach (ModuleEntry row in rows)
            foreach (LocomotionEntry gait in row.locomotion)
            {
                gait.clip = LookUpClip(gait.clipName, clips);
                if (string.IsNullOrEmpty(gait.speedFromClipName)) continue;

                AnimationClip? twin = LookUpClip(gait.speedFromClipName, clips);
                if (twin == null) continue;

                float measured = MeasureGaitSpeed(twin);
                if (measured > 0.01f) gait.metresPerSecond = measured;
                else
                    Debug.LogWarning(
                        $"[Odyssey] {gait.speedFromClipName} reports no root travel, so " +
                        $"{gait.clipName} keeps its declared {gait.metresPerSecond} m/s.");
            }
        }

        static AnimationClip? LookUpClip(string name, Dictionary<string, AnimationClip?> cache)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (cache.TryGetValue(name, out AnimationClip? cached)) return cached;
            AnimationClip? clip = FindSyntyClip(name);
            cache[name] = clip;
            return clip;
        }

        /// <summary>
        /// Metres per second a root-motion clip covers, horizontally.
        ///
        /// Horizontally on purpose: a walk cycle bobs, and counting the vertical would report a
        /// stride slightly longer than the one that actually touches the floor.
        /// </summary>
        static float MeasureGaitSpeed(AnimationClip clip)
        {
            Vector3 velocity = clip.averageSpeed;
            return new Vector2(velocity.x, velocity.z).magnitude;
        }

        /// <summary>Exact-name lookup under Assets/Synty. Absent packs give null, which is fine.</summary>
        static GameObject? FindSyntyPrefab(string exactName)
        {
            if (!Directory.Exists(Path.GetFullPath("Assets/Synty"))) return null;
            string[] guids = AssetDatabase.FindAssets($"{exactName} t:Prefab", new[] { "Assets/Synty" });
            string? path = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => string.Equals(Path.GetFileNameWithoutExtension(p), exactName,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .FirstOrDefault();
            return path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>
        /// Exact-name clip lookup under Assets/Synty. Absent packs give null.
        ///
        /// A clip in this pack is a sub-asset of an FBX rather than a file of its own, so the file
        /// is located by name and then opened to find the clip inside it. Unity's own preview
        /// clips share that file and must be skipped, or the pose comes from a thumbnail.
        /// </summary>
        static AnimationClip? FindSyntyClip(string exactName)
        {
            if (!Directory.Exists(Path.GetFullPath("Assets/Synty"))) return null;
            string[] guids = AssetDatabase.FindAssets($"{exactName} t:AnimationClip", new[] { "Assets/Synty" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetFileNameWithoutExtension(path), exactName,
                        StringComparison.OrdinalIgnoreCase)) continue;

                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                        return clip;
            }
            return null;
        }

        /// <summary>Exact-name material lookup under Assets/Synty. Absent packs give null.</summary>
        static Material? FindSyntyMaterial(string exactName)
        {
            if (!Directory.Exists(Path.GetFullPath("Assets/Synty"))) return null;
            string[] guids = AssetDatabase.FindAssets($"{exactName} t:Material", new[] { "Assets/Synty" });
            string? path = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => string.Equals(Path.GetFileNameWithoutExtension(p), exactName,
                    StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p, StringComparer.Ordinal)
                .FirstOrDefault();
            return path == null ? null : AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        // ---------------------------------------------------------------- scene

        static void BuildLighting(Transform root)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            // The hour the scene is baked at. The cycle takes over on the first frame; this is
            // only so that opening the scene shows a lit board rather than whatever the sky
            // happened to be saved as.
            DaylightState baked = GoldenHour.Baked;
            sun.type = LightType.Directional;
            sun.intensity = baked.SunIntensity;
            sun.color = baked.SunColour;
            sun.shadows = LightShadows.Soft;
            // The lever the 72-degree decision never pulled. At full strength a shadowed
            // fragment falls back to ambient alone and loses the key light's hue, so a board
            // mostly in shadow goes mostly grey; at 0.6 it keeps the warmth and only darkens.
            sun.shadowStrength = baked.ShadowStrength;
            sun.transform.SetParent(root, false);
            // Steeply overhead, not raking across the board. At 50 degrees the key light struck the
            // ground at a glancing angle, which is how you light a landscape you walk through and
            // the wrong way to light one you look down at: it cost the ground a quarter of its
            // brightness and threw long shadows across the very surface the player is reading. A
            // high sun puts the light on the ground, keeps the tiles evenly lit, and leaves just
            // enough offset for a colonist or a wall to cast a short shadow that grounds them.
            sun.transform.rotation = Quaternion.Euler(baked.SunElevation, baked.SunAzimuth, 0f);

            // Flat-lit low-poly, as the concept renders are: a strong key, a generous cool ambient
            // so nothing goes black. Cel shading was raised and rejected
            // on 2026-09-15 (06-rendering-and-camera.md section 1).
            //
            // Lifted towards the reference art, which is high-key: the ground there is evenly lit
            // and reads as a bright field, with the only real shadows cast by buildings and people
            // onto it. Ambient does most of that work, because it is what fills the parts of a
            // scene the key light does not reach, and a dim ambient is what made the first pass
            // look overcast.
            //
            // **And it is now the shadow colour, which it was not before.** With the key raking at
            // 30 degrees the board is mostly shadow, and a shadowed fragment is lit by ambient
            // alone — so this is what the shadows *are*. A cool sky against the warm key is what
            // puts the blue in them: a large hue gap and a small value gap, as the references have.
            // Grey ambient would give grey shadows, which is the overcast look this avoids.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = baked.AmbientSky;
            RenderSettings.ambientEquatorColor = baked.AmbientEquator;
            RenderSettings.ambientGroundColor = baked.AmbientGround;
            // **Fog crosses the board now, and the comment this replaces argued the opposite.**
            //
            // It was right for a blue-grey daylight wash: fog that started at 90 m covered the
            // playing area the moment the camera pulled back, and a colony sim is looked at rather
            // than walked through, so the board must stay legible corner to corner. What changed is
            // not the reasoning but the colour. Haze the same warm tone as the horizon does not
            // obscure the distance, it *places* it: the far side of the board reads as further away
            // instead of merely smaller, and the surround dissolves into a sky it matches exactly.
            //
            // Exponential-squared rather than linear, because the curve is the argument. At this
            // density the air is 1% at 50 m, 20% at 224 m, about a third at the rim and 97% by
            // 900 m — so the near cells are untouched and nothing is hidden that the player reads.
            // Plain exponential would put 18% on the nearest cells. The linear 460-to-1,100 pair
            // this replaces never touched the board at all, which is why the board never had any
            // depth to it.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            // The horizon colour of the sky below, so what fades out at the rim of the board fades
            // into the sky rather than into a grey that does not belong to anything.
            RenderSettings.fogColor = baked.Horizon;
            RenderSettings.fogDensity = baked.FogDensity;

            RenderSettings.skybox = SkyMaterial();

            // The grade, as a global volume on the scene rather than the pipeline asset's
            // default profile. That default is how this project ended up with no post-processing
            // at all: the asset pointed at a profile GUID that resolved to nothing, and nobody
            // noticed for months because a missing volume looks exactly like a scene nobody has
            // graded yet. A volume in the scene is visible in the hierarchy and travels with it.
            var volume = new GameObject("Golden Hour").AddComponent<Volume>();
            volume.transform.SetParent(root, false);
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.sharedProfile = GoldenHour.BuildProfile();
            DynamicGI.UpdateEnvironment();
        }

        // The horizon colour lives in GoldenHour, not here. It was a constant in this file and is
        // not any more, deliberately: the fog takes the same value, and two horizon colours in two
        // places is exactly how the sky and the haze drift apart — which is the one identity the
        // whole look rests on.

        const string SkyMaterialPath = "Assets/Settings/OdysseySky.mat";

        /// <summary>
        /// The sky material, made once and then reused.
        ///
        /// A real asset rather than one built in memory, because <c>RenderSettings.skybox</c> is
        /// serialised into the scene: a material created at build time would be a reference to
        /// nothing the moment the scene was reopened. Created here rather than by hand for the
        /// usual reason — a step done once on one machine is invisible to every clone afterwards.
        ///
        /// The colours are written on every call, so retuning the sky is an edit here and a
        /// rebuild, not a hunt through an inspector. A serialised value would otherwise win over
        /// anything changed in code, silently.
        /// </summary>
        public static Material? SkyMaterial()
        {
            Shader shader = Shader.Find("Odyssey/GradientSky");
            if (shader == null)
            {
                Debug.LogWarning("[Odyssey] shader Odyssey/GradientSky not found; no sky.");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "OdysseySky" };
                Directory.CreateDirectory(Path.GetFullPath(Path.GetDirectoryName(SkyMaterialPath)!));
                AssetDatabase.CreateAsset(material, SkyMaterialPath);
            }

            DaylightState baked = GoldenHour.Baked;
            material.shader = shader;
            material.SetColor("_SkyColour", baked.Zenith);
            material.SetColor("_HorizonColour", baked.Horizon);
            // Below the horizon is haze, not floor. Fog tints the far board towards the horizon
            // so a dark underside put a grey band between the board's rim and the horizon —
            // pale ground, then dark nothing, then pale sky — that read as a darkness in the
            // distance with no cause. A shade under the horizon colour lets the rim fade into
            // distance instead of falling off an edge.
            material.SetColor("_GroundColour", baked.BelowHorizon);
            material.SetFloat("_HorizonFalloff", 2.2f);
            material.SetFloat("_GroundFalloff", 3.0f);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        static SliceCameraRig BuildCamera(Transform root)
        {
            var go = new GameObject("Slice Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(root, false);

            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.3f;
            // Far enough to contain the surround. The board itself needs 600 m and had exactly
            // that; the land outside it reaches 1,220 m past the rim, and a far plane at 600 cut
            // it off in a hard arc with sky beyond — which is the board-game edge again, only
            // moved. It costs nothing: fog is opaque by 1,100 m, so everything the extra range
            // admits is already the colour of the sky, and reversed-Z leaves the depth precision
            // where it was.
            camera.farClipPlane = 1800f;

            // Anti-aliasing, which this project has never had — no MSAA, no post AA, nothing.
            // It matters more now than it did: bloom and a warm grade on an aliased image look
            // worse than either alone, because a stair-stepped edge is exactly what a bloom
            // threshold catches and smears.
            //
            // SMAA rather than FXAA, and the reason is our own outline. FXAA finds edges by
            // luminance contrast and softens them, and a one-pixel post-drawn ink line is the
            // precise pattern it destroys. TAA would jitter the same line and wants motion
            // vectors we do not produce for instanced geometry. MSAA cannot help at all, since
            // the outline is drawn after the resolve.
            //
            // Post-processing is switched on here because URP keeps it per camera and defaults it
            // to false. A camera built in script therefore renders no volume at all, which is a
            // silent way to have a grade and not see it.
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.antialiasing =
                AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.renderPostProcessing = true;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            go.AddComponent<AudioListener>();

            var rig = go.AddComponent<SliceCameraRig>();
            // Set here rather than left to the field initialiser, because a serialised value wins
            // over a C# default and the scene would keep whatever the first build wrote for ever.
            // Opaque now: the cursor is corner brackets, not a wash over the thing selected.
            rig.selectionColour = Color.white;

            // Same reason, and the same trap caught a second time. The active layer keeps its
            // ceiling, so from the default depth you can see the floor above and build on it —
            // the owner asked for this twice ("I couldn't see the upper floor from the normal
            // view still"), and the first fix moved the C# default only, which a scene built
            // before it overrides for ever.
            rig.slice.suppressActiveCeiling = false;
            return rig;
        }

        static void BuildBootstrap(Transform root, SliceCameraRig rig, ModuleCatalogue catalogue)
        {
            var go = new GameObject("Bootstrap");
            go.transform.SetParent(root, false);
            var boot = go.AddComponent<OdysseyBootstrap>();
            go.AddComponent<SelectionPresenter>();   // hit-tests a click for the selection director
            go.AddComponent<DesignatePresenter>();   // arms a tool and turns a drag into orders
            go.AddComponent<SettingsPresenter>();    // owns Escape, and throws the graphics levers

            // The HUD: one UI Toolkit document over the live world (ADR 0003), built in code by
            // the shell and styled by the authored sheet.
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = BuildHudPanelSettings();
            var hud = go.AddComponent<HudShell>();
            AssetDatabase.ImportAsset(HudStylesPath);
            hud.hudStyles = AssetDatabase.LoadAssetAtPath<StyleSheet>(HudStylesPath);
            if (hud.hudStyles == null)
                Debug.LogWarning($"[PlayScene] HUD stylesheet missing at {HudStylesPath}; the HUD will draw unstyled.");

            // The two faces, both SIL Open Font Licence and both committed with their licences
            // beside them. Optional by design: a clone that has not imported them keeps the
            // panel's own theme font, so the layout is identical and only the letter shapes
            // differ. That is why this warns rather than throwing.
            hud.uiFont = AssetDatabase.LoadAssetAtPath<Font>(HudUiFontPath);
            hud.monoFont = AssetDatabase.LoadAssetAtPath<Font>(HudMonoFontPath);
            if (hud.uiFont == null || hud.monoFont == null)
                Debug.LogWarning($"[PlayScene] HUD fonts missing at {HudUiFontPath} / {HudMonoFontPath}; " +
                                 "the HUD keeps the runtime theme's default face.");

            boot.sizeX = PlaySizeXZ;
            boot.sizeZ = PlaySizeXZ;
            boot.layers = PlayLayers;
            boot.seed = 1;
            boot.moduleCatalogue = catalogue;
            boot.cameraRig = rig;
            // The sound table, on the same generated-asset bargain as the module catalogue: the
            // asset is built by AudioSetup and loaded here by fixed path, and a clone without it
            // gets a silent game that still runs.
            boot.audioCatalogue = AssetDatabase.LoadAssetAtPath<AudioCatalogue>(AudioCataloguePath);
            // Written explicitly, because the scene serialises these and a C# default changes
            // nothing for a field the scene already holds. Sparse by owner request: six cells in
            // ten get a tuft. Zero on the look seed means a fresh cast of colonists every session.
            boot.grassScatter = 60;
            boot.colonistLookSeed = 0;
        }

        /// <summary>
        /// The panel settings the HUD renders through, made once and then reused.
        ///
        /// Pixel-for-pixel on purpose: the HUD is authored in screen pixels against the mockup,
        /// and the scale policy (continuous text, stepped icons, per ADR 0007) arrives with the
        /// settings panel rather than being guessed here.
        /// </summary>
        static PanelSettings BuildHudPanelSettings()
        {
            // A theme is a text stylesheet that imports Unity's default runtime theme, and it has
            // to be written as one: a ThemeStyleSheet made with CreateInstance and saved as YAML
            // under a .tss name is fed to the stylesheet importer as text, comes out empty, and
            // a panel with no default font draws nothing at all. That is the first HUD build in a
            // sentence. The file is authored and committed; this only recreates it if it is gone.
            if (!File.Exists(Path.GetFullPath(HudThemePath)))
            {
                Directory.CreateDirectory(Path.GetFullPath(Path.GetDirectoryName(HudThemePath)!));
                File.WriteAllText(Path.GetFullPath(HudThemePath), "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(HudThemePath);
            }
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(HudThemePath);
            if (theme == null)
                throw new InvalidOperationException($"the HUD theme at {HudThemePath} did not import as a ThemeStyleSheet");

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(HudPanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                Directory.CreateDirectory(Path.GetFullPath(Path.GetDirectoryName(HudPanelPath)!));
                AssetDatabase.CreateAsset(panel, HudPanelPath);
            }
            panel.themeStyleSheet = theme;

            // Scale with the screen, against the 1920 x 1080 canvas the interface is authored in.
            // The sheet is written in those pixels, so this is the size it was designed to be read
            // at: 1:1 on a 1080p monitor, 2x at 4K. Constant pixel size was the first setting, and
            // at 4K it made every eleven-pixel label eleven pixels tall, which nobody could read.
            // Matching width and height equally keeps a wide screen and a tall one the same
            // distance from the design. Icons are meant to step at 32 and 64 rather than scale
            // continuously (ADR 0007); the placeholder squares scale with everything else until
            // the pipeline replaces them.
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = HudReferenceResolution;
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            return panel;
        }
    }
}
