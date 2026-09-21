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
    /// Photograph a colonist climbing the one-cell stair, point by point, so the climb can be
    /// judged by eye.
    ///
    /// <para><c>ClimbCheck</c>'s sibling and largely a copy of it, for the report that made it
    /// necessary (owner, 2026-09-21): <i>"the animation for going upstairs is terrible it needs to
    /// ground 2 flights of stairs and make sure the feet get onto each step and surface as it
    /// climbs."</i> <c>StairWalkTests</c> says the path is on the measured surface, stays inside
    /// the cell and never jolts; none of that answers whether a figure looks like it is climbing a
    /// staircase, and this is the only thing that can.</para>
    ///
    /// <para><b>A real colonist really climbs it, which is worth the extra fixture.</b>
    /// <c>ClimbCheck</c> forces its pose because a ladder climb cannot be arranged on demand; a
    /// stair climb can. The board gets a stair, a wall beside it with a deck on top as the landing,
    /// wood on the ground and a floor ordered on the storey above — and the colonist has to go up
    /// the stair to build it. The harness then ticks until the pawn is <i>on</i> the stair and
    /// photographs it at a spread of progress values.
    ///
    /// The point of doing it this way: nothing here fabricates a <see cref="PawnView"/>, so the
    /// pictures are of the shipping path driven by the shipping job system. It also re-proves,
    /// incidentally, that material goes up a stair at all.</para>
    ///
    /// <para><b>Read the boots against the treads.</b> The question this sheet answers is whether
    /// the figure is on the flights — walking up the lower one, turning on the landing, walking up
    /// the upper one — rather than rising through the middle of the art, which is what it did
    /// before. It does not answer whether each foot lands on a particular tread: there is no foot
    /// IK in this game and the gait is a blend, so a boot may fall between two nosings. That is the
    /// same bargain every other walked surface makes.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.StairFigureCheck.Run</c>.
    /// Output: <c>Logs/stairfig-N.png</c>.</para>
    /// </summary>
    public static class StairFigureCheck
    {
        const float FrameSeconds = 1f / 60f;

        /// <summary>
        /// Where in the climb to shoot. Evenly spaced over the whole step, because what is being
        /// judged is the whole of it — the two flights and the turn between them.
        /// </summary>
        static readonly float[] Points = { 0f, 0.15f, 0.3f, 0.45f, 0.55f, 0.7f, 0.85f, 1f };

        [MenuItem("Odyssey/Presentation/Check the stair climb")]
        public static void RunFromMenu() => Execute(false);

        public static void Run() => Execute(Application.isBatchMode);

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            ChunkRenderer? renderer = null;
            PawnFigureDirector? figures = null;
            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            Action<ScriptableRenderContext, Camera>? hook = null;
            float amplitudeWas = GroundRelief.Amplitude;

            try
            {
                // Flat, for StairCheck's reason: the question is the figure against the flights,
                // and relief would drape the ground by an amount that has nothing to do with it.
                GroundRelief.Amplitude = 0f;

                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                if (catalogue == null)
                {
                    Debug.LogError("[StairFig] no catalogue; nothing to draw.");
                    exitCode = 1;
                    return;
                }

                var size = new GridSize(48, 48, 12);
                ScenarioDef scenario = ScenarioDef.Bare();
                scenario.colonists = 1;
                scenario.beds = 0;
                scenario.startingFellRadius = 0;
                ColonyWorld colony = ColonyWorld.Build(size, 1u, scenario, barren: true, wooded: false);

                CellRef start = colony.Start;
                int stair = size.Index(start.X, start.Z, start.Y);
                const int facing = 0;

                IntentRejection order = colony.Construction.Place(
                    size.FromIndex(stair), BuildingHandle.Stair, StuffHandle.Wood, facing);
                if (order != IntentRejection.None)
                {
                    Debug.LogError($"[StairFig] the stair order was refused: {order}.");
                    exitCode = 1;
                    return;
                }

                colony.Construction.Raise(colony.Pawns, stair);
                colony.World.Tick();

                // A storey to climb to: a wall beside the stair with a deck on top of it, which is
                // the landing the flight arrives beside. StairTests' own fixture, in pictures.
                int beside = stair + 1;
                int landing = beside + size.LayerStride;
                Raise(colony, beside, BuildingHandle.Wall);
                Raise(colony, landing, BuildingHandle.Floor);

                // And a reason to go up: one more deck plate on the storey above, with the wood for
                // it on the ground. The colonist has to climb the stair to build it.
                int siteCell = landing + 1;
                int pile = colony.Pawns.Items.NearestCellWithSpace(
                    colony.Grid, stair, ItemIndex.Wood, 20, JobDriver.DropSearchRadius);
                if (pile >= 0) colony.Pawns.Items.Spawn(ItemIndex.Wood, pile, 20);

                IntentRejection deck = colony.Construction.Place(
                    size.FromIndex(siteCell), BuildingHandle.Floor, StuffHandle.Wood);
                if (deck != IntentRejection.None)
                    Debug.LogWarning($"[StairFig] the deck order was refused ({deck}); "
                                     + "the colonist may have no reason to climb.");

                var chunks = new ChunkGrid(size);
                var library = new ModuleLibrary(catalogue);
                var model = new WorldRenderModel(size, chunks, library);
                model.RefreshAll(colony.Grid, colony.Outcome.Edifices);
                renderer = new ChunkRenderer(model);

                lightingRoot = new GameObject("StairFigRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                figures = new PawnFigureDirector(catalogue, lightingRoot.transform, 0) { World = model };
                if (!figures.Enabled)
                    Debug.LogWarning("[StairFig] no live figures: no character art or no gait clips. "
                                     + "The pictures will prove nothing about the pose.");

                int activeLayer = start.Y;
                var slice = new SliceSettings();
                int movePerTick = ContentPack.Pawns().Movement.movePerTick;

                cameraObject = new GameObject("StairFigCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                ChunkRenderer active = renderer;
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    // PrimeAll for StairCheck's reason: a probe gets one camera.Render() and a
                    // frame meshes eleven chunks, so without this the board arrives a shot at a
                    // time and the early pictures are empty meadow.
                    active.PrimeAll(activeLayer, slice);
                    active.Render(activeLayer, slice);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                CellRef below = size.FromIndex(stair);
                var above = new CellRef(below.X, below.Z, below.Y + 1);

                // Where to point the camera: the middle of the climb, half a layer up in the
                // stair's own cell, so one framing holds both flights.
                Vector3 aim = CellMetrics.FloorCentre(below) + Vector3.up * StairWalk.Landing;

                // Warm the material and the gait blend before anything is kept. A character draws
                // flat yellow on the first frame it is touched - ClimbCheck's note, same cause.
                for (int warm = 0; warm < 24; warm++)
                {
                    colony.World.Tick();
                    figures.Sync(colony.World.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);
                    PlayScene.Shoot(camera, aim, 20f, 35f, 7f, "Logs/stairfig-warm.png");
                }

                int taken = 0;
                int onStairTicks = 0;
                for (int tick = 0; tick < 120_000 && taken < Points.Length; tick++)
                {
                    colony.World.Tick();
                    figures.Sync(colony.World.Views.Current, activeLayer, slice, 0f, movePerTick, FrameSeconds);
                    figures.Evaluate(FrameSeconds);

                    if (!OnTheStair(colony.World.Views.Current, below, above, out PawnView climber)) continue;
                    onStairTicks++;

                    float t = climber.MovePerMille > 0 ? climber.MovePerMille * 0.001f
                        : climber.MovePercent * 0.01f;
                    if (t < Points[taken]) continue;

                    Vector3 where = StairWalk.At(below, facing, up: true, t, out Vector3 heading);
                    Debug.Log($"[StairFig] t={t:0.000} at ({where.x:0.00}, {where.y:0.00}, {where.z:0.00}), "
                              + $"{where.y - CellMetrics.FloorCentre(below).y:0.00} m above the cell floor, "
                              + $"facing {PawnPose.YawOf(heading):0} deg");

                    PlayScene.Shoot(camera, aim, 20f, 35f, 7f, $"Logs/stairfig-{taken}.png");
                    if (taken == 0) PlayScene.Shoot(camera, aim, 48f, 45f, 9f, "Logs/stairfig-play.png");
                    taken++;
                }

                if (taken == 0)
                {
                    Debug.LogError($"[StairFig] nobody ever set foot on the stair ({onStairTicks} "
                                   + "ticks on it); there is nothing to photograph and the fixture "
                                   + "is what is wrong, not the pose.");
                    exitCode = 1;
                    return;
                }

                Debug.Log($"[StairFig] wrote {taken} of Logs/stairfig-*.png, "
                          + $"from {onStairTicks} ticks spent on the stair.");
            }
            catch (Exception error)
            {
                Debug.LogError($"[StairFig] {error}");
                exitCode = 1;
            }
            finally
            {
                GroundRelief.Amplitude = amplitudeWas;
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                figures?.Dispose();
                renderer?.Dispose();
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>Put a thing up instantly, the way the test fixtures do.</summary>
        static void Raise(ColonyWorld colony, int cell, int building)
        {
            IntentRejection order = colony.Construction.Place(
                colony.Grid.Size.FromIndex(cell), building, StuffHandle.Wood);
            if (order != IntentRejection.None)
                Debug.LogWarning($"[StairFig] the order for {cell} was refused: {order}.");
            else
                colony.Construction.Raise(colony.Pawns, cell);
        }

        /// <summary>
        /// The colonist that is mid-step on the stair, if any: in its cell and walking into the one
        /// above it, which is exactly the condition <c>StairWalk.Crosses</c> tests.
        /// </summary>
        static bool OnTheStair(WorldSnapshot snapshot, CellRef below, CellRef above,
            out PawnView climber)
        {
            foreach (PawnView view in snapshot.Pawns)
            {
                if (!view.Cell.Equals(below) || !view.NextCell.Equals(above)) continue;
                climber = view;
                return true;
            }

            climber = default;
            return false;
        }
    }
}
