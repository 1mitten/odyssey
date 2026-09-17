#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Presentation.CameraRig;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim;
using Odyssey.Sim.Construction;
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
    /// Photograph a roofed room, and then the same room with a wall pulled out from under it.
    ///
    /// <para><b>What it is for.</b> U29 landed with thirteen tests and nothing anybody had looked
    /// at. Four of its claims are look judgements a test cannot make: does a built floor read as a
    /// floor rather than as a lid, does it take its material's tint, does a collapse read as a
    /// collapse, and does rubble read as a mess to clear. <see cref="WallCheck"/> is the same
    /// argument made about a wall, and this is its other half.</para>
    ///
    /// <para><b>Every cell here is named by the picker, not by this file.</b> That is the point
    /// rather than a flourish. The floor tool was armable, draggable and inert until 2026-09-17
    /// because <c>SlicePicker</c> answered a click on a wall with the wall's own cell and
    /// <c>ConstructionGrid.Place</c> refused that cell — a disagreement neither assembly's tests
    /// could see, and one that a harness naming its own cells would photograph its way straight
    /// past. So the room is roofed by firing a ray down at each wall and ordering a floor at
    /// whatever comes back, which is exactly what a player's pointer does.</para>
    ///
    /// <para><b>The room is raised through <see cref="ConstructionGrid"/></b>, as
    /// <see cref="WallCheck"/> is and for the reason recorded there: <c>Raise</c> is what a
    /// colonist's last hammer stroke calls, and a harness that set cells itself would be
    /// photographing a building the game cannot build. The collapse is likewise the real one —
    /// <c>Demolish</c>, then ticks, so <c>SupportSolver</c> finds the orphans and
    /// <c>SupportSystem</c> drops them.</para>
    ///
    /// <para>Headless: <c>scripts/unity.sh shot Odyssey.EditorTools.FloorCheck.Run</c>. It needs a
    /// real graphics device, so not under <c>-nographics</c>.</para>
    /// </summary>
    public static class FloorCheck
    {
        /// <summary>The room's footprint, in cells. Wide enough that the roof has a middle.</summary>
        const int Wide = 6;
        const int Deep = 5;

        /// <summary>The gap between the standing room and the one that is pulled down.</summary>
        const int Apart = 4;

        [MenuItem("Odyssey/Presentation/Check a built floor")]
        public static void RunFromMenu() => Shoot(exitWhenDone: false);

        public static void Run() => Shoot(Application.isBatchMode);

        static void Shoot(bool exitWhenDone)
        {
            Execute(exitWhenDone);
            if (!exitWhenDone) ShotFolder.Reveal("floor-*.png");
        }

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            ChunkRenderer? renderer = null;
            ModuleLibrary? library = null;
            GameObject? lightingRoot = null;
            GameObject? cameraObject = null;
            Action<ScriptableRenderContext, Camera>? hook = null;

            float amplitudeWas = GroundRelief.Amplitude;
            float periodWas = GroundRelief.Period;

            try
            {
                var catalogue = AssetDatabase.LoadAssetAtPath<ModuleCatalogue>(PlayScene.CataloguePath);
                var size = new GridSize(PlayScene.PlaySizeXZ, PlayScene.PlaySizeXZ, PlayScene.PlayLayers);
                var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
                gen.MakeWooded();

                var grid = new CellGrid(size);
                var chunks = new ChunkGrid(size);
                MapGenOutcome result = MapGenerator.Generate(grid, 1u, gen);
                var slice = new SliceSettings();

                GroundRelief.Amplitude = GroundRelief.BoardAmplitude;
                GroundRelief.Period = 150f;

                library = new ModuleLibrary(catalogue);
                var model = new WorldRenderModel(size, chunks, library);
                model.RefreshAll(grid, result.Natural!.Context.Edifices);

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
                    .AddColony(pawns, designations, support, nav, result.Placements, out ConstructionGrid sites)
                    .Build();

                // Bare, for WallCheck's reason: nobody is placed with work to do, so nothing walks
                // into shot and no work giver builds something of its own while the sheet is taken.
                ColonyScenario.Place(grid, pawns, result.StartCell, 1u, ScenarioDef.Bare());

                // **Seed the support field, which a hand-composed world does not get for free.**
                // `ColonyWorld.RebuildDerived` does this on the real path and says why: support is
                // not in the save, so an unseeded grid holds zero everywhere and the first
                // incremental solve is wrong. Without it here the rooms went up, every wall was
                // pulled out from under one of them, and *nothing fell* — a harness fault that
                // reads exactly like the feature being broken, which is the reason this line
                // carries a comment instead of being quietly correct.
                support.Solver.SolveFull();

                CellRef start = result.StartCell;
                var standing = new Room(start, 0, StuffHandle.Wood);
                var doomed = new Room(start, Wide + Apart, StuffHandle.Stone);

                foreach (Room room in new[] { standing, doomed })
                {
                    room.ClearTheSite(grid, pawns, size);
                    room.RaiseWalls(sites, pawns, size);
                    room.RoofIt(sites, pawns, world, model, size, slice);
                }

                model.RefreshAll(grid, result.Edifices);

                Debug.Log($"[Floor] {standing}; {doomed}");
                if (standing.Slabs == 0)
                {
                    Debug.LogError("[Floor] no roof went up, so there is nothing to photograph. " +
                                   "If the walls stood and the slabs did not, the picker and " +
                                   "ConstructionGrid.Place have disagreed again — see " +
                                   "docs/design/17-floors-and-collapse.md, 'Which cell an order lands in'.");
                    exitCode = 1;
                    return;
                }

                lightingRoot = new GameObject("FloorRoot");
                PlayScene.BuildSheetLighting(lightingRoot.transform);

                int activeLayer = start.Y + 1;
                slice.surfaceLayer = start.Y;

                cameraObject = new GameObject("FloorCamera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 40f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 2000f;
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.backgroundColor = new Color(0.16f, 0.19f, 0.24f);

                renderer = new ChunkRenderer(model);
                renderer.Skirt.Enabled = true;
                renderer.Skirt.Build();

                ChunkRenderer active = renderer;
                int layer = activeLayer;
                hook = (context, rendering) =>
                {
                    if (rendering != camera) return;
                    active.ViewerPosition = rendering.transform.position;
                    active.Render(layer, slice);
                };
                RenderPipelineManager.beginCameraRendering += hook;

                Vector3 woodMiddle = standing.Middle();
                Vector3 stoneMiddle = doomed.Middle();
                Vector3 both = (woodMiddle + stoneMiddle) * 0.5f;

                // Before: both roofs on, side by side, so the material tint is judged against
                // something rather than from memory.
                PlayScene.Shoot(camera, both, 52f, 45f, 48f, "Logs/floor-pair-before.png");

                // Nearly overhead on the wooden roof: the shot that says whether a built floor
                // reads as a floor or as a lid on a box.
                PlayScene.Shoot(camera, woodMiddle, 74f, 45f, 26f, "Logs/floor-wood-over.png");

                // Low and along it, where the slab has a profile against the sky and the join
                // between a 3 m wall head and the slab it carries is unmissable.
                PlayScene.Shoot(camera, woodMiddle, 14f, 90f, 30f, "Logs/floor-wood-low.png");

                // The stone room, from the camera the collapse shot uses and not one pixel
                // different. A before and an after taken from two positions is not a comparison,
                // it is two pictures — and the first version of this sheet had exactly that
                // problem: an "after" that looked intact could not be told from an "after" of the
                // wrong room without counting trees in the background.
                PlayScene.Shoot(camera, stoneMiddle, 52f, 45f, 30f, "Logs/floor-stone-before.png");

                // Pull one wall out of the stone room and let the solver find it. Demolish is what
                // a deconstruct job's last stroke calls, so this is the collapse the game has,
                // not a collapse this file arranged.
                int pulled = doomed.PullAWallOut(sites, pawns, size);
                Debug.Log($"[Floor] after the pull: {support.Solver.DirtyCount} dirty cells, " +
                          $"{support.TotalCollapses} collapses so far; " +
                          $"pawns.Support is {(pawns.Support == null ? "null" : "bound")}; " +
                          doomed.Report(grid, size));
                for (int tick = 0; tick < 4; tick++) world.Tick();
                Debug.Log($"[Floor] after 4 ticks: {support.Solver.DirtyCount} dirty cells, " +
                          $"{support.TotalCollapses} collapses; " + doomed.Report(grid, size));
                Debug.Log($"[Floor] the doomed room's footprint, W wall F floor r rubble # rock:" +
                          doomed.Map(grid, size, start.Y - 1, start.Y + 2) +
                          "\n  and the standing one:" +
                          standing.Map(grid, size, start.Y - 1, start.Y + 2) +
                          $"\n  wood room centred on {woodMiddle}, stone on {stoneMiddle}");
                model.RefreshAll(grid, result.Edifices);

                int stillUp = doomed.SlabsStanding(grid);
                int rubble = doomed.RubbleNearby(grid, size);
                Debug.Log($"[Floor] pulled {pulled} wall cells: {doomed.Slabs - stillUp} of " +
                          $"{doomed.Slabs} slabs came down, {rubble} cells of rubble landed.");
                if (stillUp == doomed.Slabs)
                    Debug.LogWarning("[Floor] nothing fell. Either the room was small enough to " +
                                     "span the gap, or the support wiring is not reaching the solver.");

                PlayScene.Shoot(camera, stoneMiddle, 52f, 45f, 30f, "Logs/floor-collapse-after.png");
                PlayScene.Shoot(camera, stoneMiddle, 16f, 90f, 30f, "Logs/floor-collapse-low.png");
                PlayScene.Shoot(camera, both, 52f, 45f, 48f, "Logs/floor-pair-after.png");

                // Straight down and far enough back for both rooms, which is the one view in which
                // neither can hide behind the other. The oblique pair could not say which room was
                // in frame and that cost a run.
                PlayScene.Shoot(camera, both, 89f, 45f, 70f, "Logs/floor-pair-after-over.png");

                RenderPipelineManager.beginCameraRendering -= hook;
                hook = null;

                Debug.Log("[Floor] wrote Logs/floor-{pair-before,wood-over,wood-low,stone-before," +
                          $"collapse-after,collapse-low,pair-after}}.png; {renderer.DrawCalls} draw " +
                          $"calls, {renderer.InstancesDrawn} instances");
            }
            catch (Exception error)
            {
                Debug.LogError($"[Floor] {error}");
                exitCode = 1;
            }
            finally
            {
                if (hook != null) RenderPipelineManager.beginCameraRendering -= hook;
                renderer?.Dispose();
                library?.Dispose();
                GroundRelief.Amplitude = amplitudeWas;
                GroundRelief.Period = periodWas;
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightingRoot != null) UnityEngine.Object.DestroyImmediate(lightingRoot);
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }

        /// <summary>
        /// One room: where it is, what it is made of, and what went up.
        ///
        /// <para>A class rather than four loose methods because the collapse shot needs to know
        /// which cells the roof went into, and counting them afterwards by sweeping the grid would
        /// count the ruined city's decks along with ours.</para>
        /// </summary>
        sealed class Room
        {
            readonly CellRef _start;
            readonly int _offsetX;
            readonly int _stuff;
            readonly List<int> _wallCells = new List<int>();
            readonly List<int> _slabCells = new List<int>();

            public Room(CellRef start, int offsetX, int stuff)
            {
                _start = start;
                _offsetX = offsetX;
                _stuff = stuff;
            }

            public int Walls => _wallCells.Count;
            public int Slabs => _slabCells.Count;

            /// <summary>Clicks the picker answered with a cell the order would not take.</summary>
            public int Refused;

            /// <summary>Clicks that hit nothing at all.</summary>
            public int Missed;

            bool OnTheWall(int i, int j) => i == 0 || j == 0 || i == Wide - 1 || j == Deep - 1;

            /// <summary>
            /// Fell everything standing on the site and one cell round it, before a peg is put in.
            ///
            /// <para><b>A tree is a pillar, and that is the finding this method exists because
            /// of.</b> <c>SupportSolver.IsGrounded</c> asks whether <em>anything</em> fills the
            /// cell below — solid terrain, a wall, or an edifice, and a tree is an edifice. So a
            /// trunk makes the boundary over it a full-support source, whether or not a slab is
            /// laid on it, and that support crosses sideways into the roof beside it. Measured:
            /// every wall was pulled out from under a 6 × 5 roof and it did not move, because two
            /// or three trees in the room's own footprint were still holding it up at 4, 3, 2.</para>
            ///
            /// <para>That is the model working. It is also a useless photograph, so the site is
            /// cleared first — which is what a colony does anyway, and what
            /// <c>ScenarioDef.startingFellRadius</c> exists for. A one-cell margin as well,
            /// because a trunk beside the room feeds the roof just as a trunk inside it does.</para>
            ///
            /// <para>Directly on the grid rather than through a fell job, because the thing being
            /// photographed is the floor and not the axe. <c>MarkStructureChanged</c> is still
            /// called, so the solver is told exactly what a felling would have told it.</para>
            /// </summary>
            public void ClearTheSite(CellGrid grid, PawnContext pawns, GridSize size)
            {
                for (int i = -1; i <= Wide; i++)
                for (int j = -1; j <= Deep; j++)
                {
                    int x = _start.X + _offsetX + i, z = _start.Z + j;
                    if (!size.Contains(x, z, _start.Y)) continue;

                    int cell = size.Index(x, z, _start.Y);
                    if (grid.Edifice[cell] < 0) continue;

                    grid.RemoveEdifice(cell);
                    pawns.Chunks?.MarkDirty(size.FromIndex(cell));
                    pawns.MarkStructureChanged(cell);
                    Felled++;
                }
            }

            /// <summary>How many things were standing on the site before anything was built.</summary>
            public int Felled;

            /// <summary>The perimeter, ordered and finished the way a colonist finishes it.</summary>
            public void RaiseWalls(ConstructionGrid sites, PawnContext pawns, GridSize size)
            {
                for (int i = 0; i < Wide; i++)
                for (int j = 0; j < Deep; j++)
                {
                    if (!OnTheWall(i, j)) continue;

                    int x = _start.X + _offsetX + i, z = _start.Z + j;
                    if (!size.Contains(x, z, _start.Y)) continue;

                    var cell = new CellRef(x, z, _start.Y);
                    if (sites.Place(cell, BuildingHandle.Wall, _stuff) != IntentRejection.None) continue;

                    int index = size.Index(x, z, _start.Y);
                    sites.Raise(pawns, index);
                    _wallCells.Add(index);
                }
            }

            /// <summary>
            /// Roof it, in two rounds, every cell named by pointing at something.
            ///
            /// <para><b>Round one is the perimeter</b>, where a slab is grounded by the wall under
            /// it and needs no neighbour. <b>Round two is the middle</b>, which is only permitted
            /// once round one has been raised <em>and solved</em> — support is a settled value read
            /// off the grid, so the world has to tick between the rounds. Two rounds is enough for
            /// a room this size; it is also the gesture a player makes, and the reason a bridge is
            /// ordered outward a cell at a time rather than planned in one drag.</para>
            /// </summary>
            public void RoofIt(ConstructionGrid sites, PawnContext pawns, SimWorld world,
                WorldRenderModel model, GridSize size, SliceSettings slice)
            {
                for (int round = 0; round < 2; round++)
                {
                    world.Tick();
                    model.RefreshAll(pawns.Cells, sites.Edifices.Records);

                    // The slice the player would be holding. Round one caps the walls from the
                    // layer they stand on; round two roofs the middle from the storey above, which
                    // is the gesture WorkingLayer exists for.
                    int workingLayer = _start.Y + round;

                    for (int i = 0; i < Wide; i++)
                    for (int j = 0; j < Deep; j++)
                    {
                        if (OnTheWall(i, j) != (round == 0)) continue;

                        int x = _start.X + _offsetX + i, z = _start.Z + j;
                        if (!size.Contains(x, z, workingLayer)) continue;

                        // Round one only caps our OWN walls. A perimeter cell where a tree or a
                        // stream stopped the wall going up would otherwise be capped anyway — the
                        // picker names the tree, a tree grounds the boundary over it, and the slab
                        // stands on the canopy. That is the rule behaving correctly and it makes a
                        // useless photograph: the first version of this harness pulled every wall
                        // out of a room and nothing fell, because three slabs were resting on
                        // trees and feeding support to the other fifteen.
                        if (round == 0 && !_wallCells.Contains(size.Index(x, z, _start.Y))) continue;

                        // The pointer decides the column; the slice decides the layer. Exactly
                        // what DesignatePresenter hands the director, written out here because
                        // this harness has no director.
                        // The banded picker the rig uses, not the single-layer one: with the
                        // slice a storey up, the cell under the pointer is the floor of the room
                        // and the layer is thrown away anyway. Only the column is read.
                        if (!SlicePicker.Pick(DownAt(x, z), model, workingLayer, slice, out CellRef pointed))
                        {
                            Missed++;
                            continue;
                        }

                        var clicked = new CellRef(pointed.X, pointed.Z, workingLayer);
                        IntentRejection answer = sites.Place(clicked, BuildingHandle.Floor, _stuff);
                        if (answer != IntentRejection.None)
                        {
                            // The interesting failure, and the one worth a line rather than a
                            // silent skip: the pointer named a cell and the order would not take
                            // it. Logged sparingly so a ragged roof can be told from a rule that
                            // cannot be reached at all.
                            if (Refused++ < 3)
                                Debug.Log($"[Floor] +{_offsetX} round {round}: a click at " +
                                          $"({x},{z}) with the slice at L{workingLayer} means " +
                                          $"{clicked}, and a floor there is {answer}.");
                            continue;
                        }

                        // Where the order actually landed, which is the lift's answer rather than
                        // this file's guess about it.
                        int index = size.Index(clicked.X, clicked.Z, clicked.Y);
                        int site = sites.At(index) == BuildingHandle.Floor
                            ? index
                            : index + size.LayerStride;

                        sites.Raise(pawns, site);
                        _slabCells.Add(site);
                    }
                }
            }

            /// <summary>
            /// Take the walls out from under the roof.
            ///
            /// <para><b>All of them, and the first attempt took one side.</b> That left every roof
            /// row still grounded at both ends by the two side walls, five cells apart against an
            /// <c>S_max</c> of four, so nothing fell — a correct answer that photographs as a
            /// feature not working. The arithmetic of a partial pull is what
            /// <c>FloorsAndCollapseTests</c> is for; a picture wants the unambiguous case.</para>
            /// </summary>
            public int PullAWallOut(ConstructionGrid sites, PawnContext pawns, GridSize size)
            {
                int pulled = 0;
                for (int k = 0; k < _wallCells.Count; k++)
                    if (sites.Demolish(pawns, _wallCells[k], out _)) pulled++;

                return pulled;
            }

            /// <summary>The first three slabs, with what is under them and what holds them up.</summary>
            public string Report(CellGrid grid, GridSize size)
            {
                var sb = new System.Text.StringBuilder("slabs: ");
                for (int i = 0; i < _slabCells.Count && i < 3; i++)
                {
                    int s = _slabCells[i];
                    int below = s - size.LayerStride;
                    sb.Append($"{size.FromIndex(s)} floor={grid.Floor[s]} support={grid.Support[s]} " +
                              $"below(edifice={grid.Edifice[below]}, solid={grid.IsSolidTerrain(below)}); ");
                }

                return sb.ToString();
            }

            /// <summary>
            /// The footprint as characters, one line per layer: what is actually in the cells.
            ///
            /// <para>Here because a photograph could not answer the question. A matched
            /// before-and-after pair of the collapsed room differed only in one patch of terrain,
            /// while the log said every slab and every wall had gone — so either the renderer was
            /// stale or the pictures were of the other room, and no amount of looking at them
            /// settled which. Characters settle it.</para>
            /// </summary>
            public string Map(CellGrid grid, GridSize size, int fromLayer, int toLayer)
            {
                var sb = new System.Text.StringBuilder();
                for (int y = toLayer; y >= fromLayer; y--)
                {
                    sb.Append($"\n  L{y}: ");
                    for (int j = 0; j < Deep; j++)
                    {
                        for (int i = 0; i < Wide; i++)
                        {
                            int x = _start.X + _offsetX + i, z = _start.Z + j;
                            if (!size.Contains(x, z, y)) { sb.Append('?'); continue; }

                            int cell = size.Index(x, z, y);
                            sb.Append(
                                grid.Edifice[cell] >= 0 ? 'W'
                                : grid.Floor[cell] != CoreContent.SlabNone ? 'F'
                                : grid.Terrain[cell] == CoreContent.TerrainRubble ? 'r'
                                : grid.IsSolidTerrain(cell) ? '#'
                                : '.');
                        }

                        sb.Append('|');
                    }
                }

                return sb.ToString();
            }

            public int SlabsStanding(CellGrid grid)
            {
                int standing = 0;
                for (int i = 0; i < _slabCells.Count; i++)
                    if (grid.Floor[_slabCells[i]] != CoreContent.SlabNone) standing++;
                return standing;
            }

            /// <summary>How many cells of the room's footprint hold rubble, on any layer.</summary>
            public int RubbleNearby(CellGrid grid, GridSize size)
            {
                int heaps = 0;
                for (int i = 0; i < Wide; i++)
                for (int j = 0; j < Deep; j++)
                {
                    int x = _start.X + _offsetX + i, z = _start.Z + j;
                    for (int y = 0; y < size.SizeY; y++)
                    {
                        if (!size.Contains(x, z, y)) continue;
                        if (grid.Terrain[size.Index(x, z, y)] == CoreContent.TerrainRubble) heaps++;
                    }
                }

                return heaps;
            }

            public Vector3 Middle()
            {
                Vector3 floor = CellMetrics.FloorCentre(
                    _start.X + _offsetX + Wide / 2, _start.Z + Deep / 2, _start.Y);
                return GroundRelief.Lift(floor);
            }

            public override string ToString() =>
                $"room at +{_offsetX}: {Felled} felled, {Walls} wall cells, {Slabs} slabs " +
                $"({Refused} clicks refused, {Missed} hit nothing, out of {Wide * Deep})";
        }

        /// <summary>A ray straight down the middle of a column, from well above anything on it.</summary>
        static Ray DownAt(int x, int z)
        {
            var target = new Vector3(
                (x + 0.5f) * CellMetrics.SizeXZ, 0f, (z + 0.5f) * CellMetrics.SizeXZ);
            return new Ray(target + new Vector3(0f, 400f, 0f), Vector3.down);
        }
    }
}
