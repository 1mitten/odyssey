#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Shared scaffolding for the pathing tests: a world you can dig holes in, and an exhaustive
    /// cell-level flood fill to check the region machinery against.
    /// </summary>
    static class NavWorld
    {
        /// <summary>
        /// An open world with a floor slab under every cell, so "walkable" means "not walled in".
        /// Walls are added afterwards by <see cref="SetSolid"/>.
        /// </summary>
        public static CellGrid MakeCells(int sx, int sz, int sy)
        {
            var cells = new CellGrid(new GridSize(sx, sz, sy));
            for (int i = 0; i < cells.Size.CellCount; i++) cells.Floor[i] = 1;
            return cells;
        }

        public static void SetSolid(CellGrid cells, NavGraph nav, int index, bool solid)
        {
            if (solid) cells.Flags[index] |= CellFlags.SolidTerrain;
            else cells.Flags[index] &= ~CellFlags.SolidTerrain;
            nav.MarkDirty(index);
        }

        public static void SetFloor(CellGrid cells, NavGraph nav, int index, bool hasFloor)
        {
            cells.Floor[index] = hasFloor ? (ushort)1 : (ushort)0;
            nav.MarkDirty(index);
        }

        /// <summary>
        /// The oracle: a breadth-first flood over cells using exactly the moves the cell search
        /// is allowed to make — four orthogonal steps and declared portal edges. If the district
        /// answer and this answer ever disagree, the region machinery is wrong.
        /// </summary>
        public static HashSet<int> FloodFromCell(NavGraph nav, int start, TraverseMode mode)
        {
            var seen = new HashSet<int>();
            if (!nav.Grid.CanEnter(start, mode)) return seen;

            GridSize size = nav.Size;
            var queue = new Queue<int>();
            seen.Add(start);
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                CellRef p = size.FromIndex(c);
                Step(p.X > 0, c - 1);
                Step(p.X + 1 < size.SizeX, c + 1);
                Step(p.Z > 0, c - size.SizeX);
                Step(p.Z + 1 < size.SizeZ, c + size.SizeX);

                if (p.X > 0 && p.Z > 0) StepDiag(c - 1 - size.SizeX, c - 1, c - size.SizeX);
                if (p.X + 1 < size.SizeX && p.Z > 0) StepDiag(c + 1 - size.SizeX, c + 1, c - size.SizeX);
                if (p.X > 0 && p.Z + 1 < size.SizeZ) StepDiag(c - 1 + size.SizeX, c - 1, c + size.SizeX);
                if (p.X + 1 < size.SizeX && p.Z + 1 < size.SizeZ) StepDiag(c + 1 + size.SizeX, c + 1, c + size.SizeX);

                for (int e = nav.FirstPortalEdge(c); e != -1; e = nav.PortalEdgeNext(e))
                {
                    if (!TraverseModes.Allows(nav.PortalEdgeMode(e), mode)) continue;
                    Step(true, nav.PortalEdgeTarget(e));
                }

                void Step(bool inBounds, int n)
                {
                    if (!inBounds || seen.Contains(n)) return;
                    if (!nav.Grid.CanEnter(n, mode)) return;
                    seen.Add(n);
                    queue.Enqueue(n);
                }

                void StepDiag(int n, int c1, int c2)
                {
                    if (seen.Contains(n)) return;
                    if (!nav.Grid.CanEnter(n, mode)) return;
                    if (!nav.Grid.CanWalkInto(c1, mode) || !nav.Grid.CanWalkInto(c2, mode)) return;
                    seen.Add(n);
                    queue.Enqueue(n);
                }
            }

            return seen;
        }
    }

    public class ReachabilityTests
    {
        [Test]
        public void ReachabilityIsADistrictComparisonAndAgreesWithAnExhaustiveFlood()
        {
            // A cluttered, multi-layer world with stairs in it, then every walkable cell checked
            // against a flood fill from three different starting points. This is the test that
            // says the cheap answer is the same as the expensive one.
            var cells = NavWorld.MakeCells(23, 19, 4);
            var nav = new NavGraph(cells);
            var rng = new DeterministicRandom(20260915u);

            for (int i = 0; i < cells.Size.CellCount; i++)
                if (rng.NextInt(100) < 32) cells.Flags[i] |= CellFlags.SolidTerrain;

            for (int s = 0; s < 40; s++)
            {
                int x = rng.NextInt(23), z = rng.NextInt(19), y = rng.NextInt(3);
                int lower = cells.Index(x, z, y);
                int upper = lower + cells.Size.LayerStride;
                cells.Flags[lower] &= ~CellFlags.SolidTerrain;
                cells.Flags[upper] &= ~CellFlags.SolidTerrain;
                nav.AddConnector(ConnectorKind.Stair, new[] { lower }, new[] { upper });
            }

            nav.MarkAllDirty();
            nav.Rebuild();

            int checkedPairs = 0;
            foreach (int start in new[] { cells.Index(0, 0, 0), cells.Index(11, 9, 1), cells.Index(22, 18, 3) })
            {
                if (!nav.Grid.CanEnter(start, TraverseMode.Colonist)) continue;
                HashSet<int> flood = NavWorld.FloodFromCell(nav, start, TraverseMode.Colonist);
                for (int c = 0; c < cells.Size.CellCount; c++)
                {
                    if (!nav.Grid.CanEnter(c, TraverseMode.Colonist)) continue;
                    bool expected = flood.Contains(c);
                    Assert.That(nav.Reachable(start, c, TraverseMode.Colonist), Is.EqualTo(expected),
                        $"district answer disagrees with the flood for cell {c}");
                    checkedPairs++;
                }
            }

            Assert.That(checkedPairs, Is.GreaterThan(2000), "the fixture stopped exercising anything");

            // And it really is an integer comparison over two array reads, not a search.
            int a = cells.Index(0, 0, 0);
            int b = cells.Index(11, 9, 1);
            Assert.That(nav.Reachable(a, b, TraverseMode.Colonist),
                Is.EqualTo(nav.DistrictOfCell(a, TraverseMode.Colonist) ==
                           nav.DistrictOfCell(b, TraverseMode.Colonist)));
        }

        [Test]
        public void AWallMakesTwoCellsUnreachableAndRemovingOneCellOfItRestoresThem()
        {
            // The room straddles a block boundary on purpose: a region never spans a block, so a
            // wall that is one region's problem on one side has to become a link problem on the
            // other, and this is where that goes wrong if it is going to.
            var cells = NavWorld.MakeCells(20, 20, 1);
            var nav = new NavGraph(cells);
            nav.Rebuild();

            int inside = cells.Index(10, 10, 0);
            int outside = cells.Index(0, 0, 0);
            Assert.That(nav.Reachable(outside, inside, TraverseMode.Colonist), Is.True);

            var ring = new List<int>();
            for (int x = 9; x <= 11; x++)
                for (int z = 9; z <= 11; z++)
                {
                    if (x == 10 && z == 10) continue;
                    ring.Add(cells.Index(x, z, 0));
                }

            foreach (int c in ring) NavWorld.SetSolid(cells, nav, c, true);
            nav.Rebuild();

            Assert.That(nav.Reachable(outside, inside, TraverseMode.Colonist), Is.False,
                "a sealed room must be unreachable");
            Assert.That(NavWorld.FloodFromCell(nav, outside, TraverseMode.Colonist).Contains(inside), Is.False);

            NavWorld.SetSolid(cells, nav, cells.Index(10, 9, 0), false);
            nav.Rebuild();

            Assert.That(nav.Reachable(outside, inside, TraverseMode.Colonist), Is.True,
                "removing one wall cell must reconnect within the same rebuild");
        }

        [Test]
        public void AStairMakesTwoLayersReachableAndRemovingItUnmakesThem()
        {
            var cells = NavWorld.MakeCells(20, 20, 2);
            var nav = new NavGraph(cells);
            nav.Rebuild();

            int below = cells.Index(1, 1, 0);
            int above = cells.Index(1, 1, 1);
            Assert.That(nav.Reachable(below, above, TraverseMode.Colonist), Is.False,
                "with no connector there is no vertical edge at all");

            int lowerA = cells.Index(5, 5, 0);
            int lowerB = cells.Index(6, 5, 0);
            int upperA = cells.Index(5, 5, 1);
            int upperB = cells.Index(6, 5, 1);
            int stair = nav.AddConnector(ConnectorKind.Stair,
                new[] { lowerA, lowerB }, new[] { upperA, upperB });
            nav.Rebuild();

            Assert.That(nav.Reachable(below, above, TraverseMode.Colonist), Is.True);
            Assert.That(NavWorld.FloodFromCell(nav, below, TraverseMode.Colonist).Contains(above), Is.True);

            nav.RemoveConnector(stair);
            nav.Rebuild();

            Assert.That(nav.Reachable(below, above, TraverseMode.Colonist), Is.False,
                "tearing out the stair must tear out the portal link with it");
        }

        [Test]
        public void ALadderIsAColonistRouteAndNotAHaulerOrAnimalOne()
        {
            var cells = NavWorld.MakeCells(20, 20, 2);
            var nav = new NavGraph(cells);
            nav.AddConnector(ConnectorKind.Ladder,
                new[] { cells.Index(5, 5, 0) }, new[] { cells.Index(5, 5, 1) });
            nav.Rebuild();

            int below = cells.Index(1, 1, 0);
            int above = cells.Index(1, 1, 1);
            Assert.That(nav.Reachable(below, above, TraverseMode.Colonist), Is.True);
            Assert.That(nav.Reachable(below, above, TraverseMode.Hauler), Is.False,
                "a bulky load cannot go up a ladder");
            Assert.That(nav.Reachable(below, above, TraverseMode.Animal), Is.False);
        }

        [Test]
        public void AClosedDoorPassesAColonistAndStopsAnAnimal()
        {
            var cells = NavWorld.MakeCells(12, 5, 1);
            var nav = new NavGraph(cells);

            // A wall across the map with one door in it.
            for (int z = 0; z < 5; z++)
            {
                int c = cells.Index(6, z, 0);
                cells.Flags[c] |= CellFlags.SolidTerrain;
            }

            int door = cells.Index(6, 2, 0);
            cells.Flags[door] &= ~CellFlags.SolidTerrain;
            cells.Flags[door] |= CellFlags.BlockingEdifice;
            nav.SetDoor(door, true);
            nav.MarkAllDirty();
            nav.Rebuild();

            int west = cells.Index(0, 2, 0);
            int east = cells.Index(11, 2, 0);
            Assert.That(nav.Reachable(west, east, TraverseMode.Colonist), Is.True);
            Assert.That(nav.Reachable(west, east, TraverseMode.Animal), Is.False,
                "an animal cannot open a door");

            nav.SetDoorOpen(door, true);
            nav.Rebuild();
            Assert.That(nav.Reachable(west, east, TraverseMode.Animal), Is.True);
        }
    }

    public class IncrementalRebuildTests
    {
        /// <summary>
        /// The oracle for incremental maintenance is a rebuild from scratch. Region and link ids
        /// come from free lists, so the two may number things differently while describing the
        /// same world; the fingerprint compares only what is id-independent.
        /// </summary>
        [Test]
        public void IncrementalRebuildMatchesAFullRebuildOverRandomisedEdits()
        {
            var cells = NavWorld.MakeCells(23, 19, 4); // not multiples of 10: clipped blocks too
            var nav = new NavGraph(cells);
            var rng = new DeterministicRandom(4242u);
            var connectors = new List<(ConnectorKind Kind, int[] Lower, int[] Upper)>();
            var doors = new List<int>();

            for (int i = 0; i < cells.Size.CellCount; i++)
                if (rng.NextInt(100) < 30) cells.Flags[i] |= CellFlags.SolidTerrain;

            nav.MarkAllDirty();
            nav.Rebuild();

            for (int round = 0; round < 40; round++)
            {
                for (int edit = 0; edit < 6; edit++)
                {
                    int choice = rng.NextInt(10);
                    int x = rng.NextInt(23), z = rng.NextInt(19), y = rng.NextInt(4);
                    int c = cells.Index(x, z, y);

                    if (choice < 4)
                    {
                        NavWorld.SetSolid(cells, nav, c, rng.NextInt(2) == 0);
                    }
                    else if (choice < 7)
                    {
                        // Mining a floor makes a hole, which is where fall edges come from.
                        NavWorld.SetFloor(cells, nav, c, rng.NextInt(2) == 0);
                    }
                    else if (choice < 9 && y < 3)
                    {
                        int lower = c;
                        int upper = c + cells.Size.LayerStride;
                        cells.Flags[lower] &= ~CellFlags.SolidTerrain;
                        cells.Flags[upper] &= ~CellFlags.SolidTerrain;
                        cells.Floor[lower] = 1;
                        cells.Floor[upper] = 1;
                        nav.MarkDirty(lower);
                        nav.MarkDirty(upper);
                        var kind = rng.NextInt(2) == 0 ? ConnectorKind.Stair : ConnectorKind.Ladder;
                        nav.AddConnector(kind, new[] { lower }, new[] { upper });
                        connectors.Add((kind, new[] { lower }, new[] { upper }));
                    }
                    else if (connectors.Count > 0)
                    {
                        int drop = rng.NextInt(connectors.Count);
                        // Connector ids run in registration order and removals leave a hole, so
                        // index into the live list by counting.
                        int id = LiveConnectorId(nav, connectors, drop);
                        nav.RemoveConnector(id);
                        connectors.RemoveAt(drop);
                    }
                }

                nav.Rebuild();
                Assert.That(nav.DerivedTablesDisagree(), Is.Null,
                    $"round {round}: a derived table disagrees with the same table computed from scratch");

                var fresh = new NavGraph(cells);
                foreach ((ConnectorKind kind, int[] lower, int[] upper) in connectors)
                    fresh.AddConnector(kind, (int[])lower.Clone(), (int[])upper.Clone());
                foreach (int d in doors) fresh.SetDoor(d, true);
                fresh.MarkAllDirty();
                fresh.Rebuild();

                Assert.That(nav.StructureFingerprint(), Is.EqualTo(fresh.StructureFingerprint()),
                    $"round {round}: incremental rebuild diverged from a rebuild from scratch");
                Assert.That(nav.LiveRegionCount(), Is.EqualTo(fresh.LiveRegionCount()),
                    $"round {round}: region count diverged");
                Assert.That(nav.LiveLinkCount(), Is.EqualTo(fresh.LiveLinkCount()),
                    $"round {round}: link count diverged — a stale link outlived its edit");
            }

            Assert.That(connectors.Count, Is.GreaterThan(0), "the fixture stopped exercising connectors");
        }

        /// <summary>
        /// The same oracle on the board the game generates (HT1, design 05 §7c). The fixture above
        /// is a random maze; a real board is one giant surface district with caverns and ponds cut
        /// out of it, which is the shape a local district repair has to get right — splits, merges
        /// and the ordinary edit that changes nothing. Three hundred edits, one rebuild each: mining
        /// rock, raising it back, and taking a floor away, all near the start so they touch the
        /// district everybody lives in. Every rebuild is checked against the same graph's tables
        /// recomputed from scratch, and every fiftieth against a graph built from scratch.
        /// </summary>
        [Test]
        public void ThePlayedMapKeepsItsDerivedTablesExactUnderEdits()
        {
            GridSize size = BoardSizes.Standard;
            var cells = PlayedMap.Generate(size, 4242u, out var generated);
            var nav = new NavGraph(cells);
            nav.Rebuild();
            Assert.That(nav.DerivedTablesDisagree(), Is.Null, "the first build");

            var rng = new DeterministicRandom(20260925u);
            CellRef start = generated.StartCell;
            int splitsOrMerges = 0;
            for (int edit = 0; edit < 300; edit++)
            {
                int x = Math.Clamp(start.X + rng.NextInt(41) - 20, 0, size.SizeX - 1);
                int z = Math.Clamp(start.Z + rng.NextInt(41) - 20, 0, size.SizeZ - 1);
                int y = Math.Clamp(start.Y + rng.NextInt(5) - 3, 0, size.SizeY - 1);
                int c = cells.Size.Index(x, z, y);
                int choice = rng.NextInt(10);
                if (choice < 5) NavWorld.SetSolid(cells, nav, c, false);
                else if (choice < 8) NavWorld.SetSolid(cells, nav, c, true);
                else NavWorld.SetFloor(cells, nav, c, false);

                int before = nav.DistrictCount(TraverseMode.Colonist);
                nav.Rebuild();
                if (nav.DistrictCount(TraverseMode.Colonist) != before) splitsOrMerges++;
                Assert.That(nav.DerivedTablesDisagree(), Is.Null, $"edit {edit} at {Size(cells).FromIndex(c)}");

                if (edit % 50 != 49) continue;
                var fresh = new NavGraph(cells);
                fresh.MarkAllDirty();
                fresh.Rebuild();
                Assert.That(nav.StructureFingerprint(), Is.EqualTo(fresh.StructureFingerprint()),
                    $"edit {edit}: the incremental graph describes a different world from one built from scratch");
            }

            Assert.That(splitsOrMerges, Is.GreaterThan(0), "the control: no edit ever split or merged a district");
        }

        static GridSize Size(CellGrid cells) => cells.Size;

        /// <summary>
        /// Two halves of a board six blocks square, joined by one gap in a wall at x = 30, so each
        /// half reaches blocks the edit never dirties (HT1). Returns the board, the graph and a cell
        /// in each half.
        /// </summary>
        static (CellGrid Cells, NavGraph Nav, int West, int East, int Gap) TwoHalves(bool gapOpen)
        {
            // Six blocks by six: an edit dirties two, well under the quarter of the board at which a
            // rebuild numbers every district afresh, so this is the repair and not the full pass.
            var cells = NavWorld.MakeCells(60, 60, 1);
            for (int z = 0; z < 60; z++) cells.Flags[cells.Index(30, z, 0)] |= CellFlags.SolidTerrain;
            int gap = cells.Index(30, 25, 0);
            if (gapOpen) cells.Flags[gap] &= ~CellFlags.SolidTerrain;
            var nav = new NavGraph(cells);
            nav.Rebuild();
            return (cells, nav, cells.Index(2, 2, 0), cells.Index(57, 57, 0), gap);
        }

        /// <summary>
        /// Filling the one gap splits a district in two, and both pieces keep regions far from the
        /// edit: the search from one side runs out of frontier first, and that closed piece must
        /// take a fresh id while the other keeps the old one (design 05 §7b).
        /// </summary>
        [Test]
        public void FillingTheOneGapSplitsTheWorldInTwo()
        {
            var (cells, nav, west, east, gap) = TwoHalves(gapOpen: true);
            Assert.That(nav.Reachable(west, east, TraverseMode.Colonist), Is.True, "the control: one world");

            NavWorld.SetSolid(cells, nav, gap, true);
            nav.Rebuild();

            Assert.That(nav.DerivedTablesDisagree(), Is.Null);
            Assert.That(nav.Reachable(west, east, TraverseMode.Colonist), Is.False, "the halves still read as one");
            Assert.That(nav.DistrictCount(TraverseMode.Colonist), Is.EqualTo(2));
        }

        /// <summary>
        /// Opening the gap merges two districts whose far ends the edit never reached: the smaller
        /// district's id is walked out from its own regions and replaced, not just the regions the
        /// search visited (design 05 §7b).
        /// </summary>
        [Test]
        public void OpeningTheGapMergesTheTwoHalves()
        {
            var (cells, nav, west, east, gap) = TwoHalves(gapOpen: false);
            Assert.That(nav.Reachable(west, east, TraverseMode.Colonist), Is.False, "the control: two worlds");

            NavWorld.SetSolid(cells, nav, gap, false);
            nav.Rebuild();

            Assert.That(nav.DerivedTablesDisagree(), Is.Null);
            Assert.That(nav.Reachable(west, east, TraverseMode.Colonist), Is.True, "the halves still read as two");
            Assert.That(nav.DistrictCount(TraverseMode.Colonist), Is.EqualTo(1));
        }


        /// <summary>
        /// The split only the ends of a <i>freed</i> link can see (HT1, design 05 §7b). A dead-end
        /// room in one block, reached by a corridor from the block next door; the corridor is walled
        /// at the last cell before the boundary. The link across the boundary is freed and not built
        /// again, so the room's region is at the end of nothing new — only the freed link names it.
        /// Its block is not dirty, so it is not re-flooded either. Without the freed link's ends as
        /// seeds, the room keeps the district of the world outside it.
        /// </summary>
        [Test]
        public void WallingARoomOffAcrossABlockBoundaryGivesItADistrictOfItsOwn()
        {
            // Everything solid, then carve: an open yard in the first block (x 0..8), a corridor along
            // z = 5 from x = 0 to x = 14, and a room at x 12..16 in the second block.
            // Four blocks by four, so the edit is repaired and not the full pass (see TwoHalves).
            var cells = NavWorld.MakeCells(40, 40, 1);
            for (int i = 0; i < cells.Size.CellCount; i++) cells.Flags[i] |= CellFlags.SolidTerrain;
            void Open(int x, int z) => cells.Flags[cells.Index(x, z, 0)] &= ~CellFlags.SolidTerrain;
            for (int x = 0; x <= 8; x++) for (int z = 0; z <= 9; z++) Open(x, z);
            for (int x = 0; x <= 14; x++) Open(x, 5);
            for (int x = 12; x <= 16; x++) for (int z = 3; z <= 7; z++) Open(x, z);

            var nav = new NavGraph(cells);
            nav.Rebuild();
            int yard = cells.Index(2, 2, 0), room = cells.Index(14, 4, 0);
            Assert.That(nav.Reachable(yard, room, TraverseMode.Colonist), Is.True, "the control: the room is reached");

            // x = 9 is the last cell of the first block; the corridor crosses into the second at 10.
            NavWorld.SetSolid(cells, nav, cells.Index(9, 5, 0), true);
            nav.Rebuild();

            Assert.That(nav.DerivedTablesDisagree(), Is.Null);
            Assert.That(nav.Reachable(yard, room, TraverseMode.Colonist), Is.False, "the walled room still reads as reachable");
            Assert.That(nav.DistrictCount(TraverseMode.Colonist), Is.EqualTo(2));
        }


        /// <summary>
        /// A hole mined in one block is fallen into from the block next door. The edit dirties
        /// the block holding the hole; the fall edge belongs to the block holding the ledge. If
        /// the rebuild does not widen to the horizontal neighbours, that edge never appears, and
        /// nothing else in the system notices until something asks the region graph about falling.
        /// </summary>
        [Test]
        public void AHoleMinedAtABlockBoundaryStillProducesItsFallEdge()
        {
            var cells = NavWorld.MakeCells(20, 20, 2);
            var nav = new NavGraph(cells);
            nav.Rebuild();
            int before = nav.LiveLinkCount();

            // x = 10 is the first cell of the second block; x = 9 is the last cell of the first.
            NavWorld.SetFloor(cells, nav, cells.Index(10, 5, 1), false);
            nav.Rebuild();

            var fresh = new NavGraph(cells);
            fresh.MarkAllDirty();
            fresh.Rebuild();

            Assert.That(nav.LiveLinkCount(), Is.EqualTo(fresh.LiveLinkCount()),
                "the fall edge from the neighbouring block is missing (or a stale link survived)");
            Assert.That(nav.StructureFingerprint(), Is.EqualTo(fresh.StructureFingerprint()));
            Assert.That(nav.LiveLinkCount(), Is.Not.EqualTo(before), "the edit changed nothing");
        }

        static int LiveConnectorId(NavGraph nav, List<(ConnectorKind, int[], int[])> live, int index)
        {
            int seen = 0;
            for (int id = 0; ; id++)
            {
                Connector? con = nav.GetConnector(id);
                if (con == null) continue;
                if (seen == index) return id;
                seen++;
            }
        }
    }

    public class PathfindingTests
    {
        [Test]
        public void APathIsIdenticalAcrossRepeatedRunsAndAcrossAGraphRebuiltFromScratch()
        {
            var cells = NavWorld.MakeCells(23, 19, 4);
            var nav = new NavGraph(cells);
            var rng = new DeterministicRandom(777u);
            var connectors = new List<(ConnectorKind, int[], int[])>();

            for (int i = 0; i < cells.Size.CellCount; i++)
                if (rng.NextInt(100) < 30) cells.Flags[i] |= CellFlags.SolidTerrain;

            for (int s = 0; s < 30; s++)
            {
                int x = rng.NextInt(23), z = rng.NextInt(19), y = rng.NextInt(3);
                int lower = cells.Index(x, z, y);
                int upper = lower + cells.Size.LayerStride;
                cells.Flags[lower] &= ~CellFlags.SolidTerrain;
                cells.Flags[upper] &= ~CellFlags.SolidTerrain;
                nav.AddConnector(ConnectorKind.Stair, new[] { lower }, new[] { upper });
                connectors.Add((ConnectorKind.Stair, new[] { lower }, new[] { upper }));
            }

            nav.MarkAllDirty();
            nav.Rebuild();

            // Nudge the graph around so the incremental machinery has done real work and the
            // free lists are not in their pristine order any more.
            for (int i = 0; i < 50; i++)
            {
                int c = rng.NextInt(cells.Size.CellCount);
                NavWorld.SetSolid(cells, nav, c, false);
            }

            nav.Rebuild();

            var fresh = new NavGraph(cells);
            foreach ((ConnectorKind kind, int[] lower, int[] upper) in connectors)
                fresh.AddConnector(kind, (int[])lower.Clone(), (int[])upper.Clone());
            fresh.MarkAllDirty();
            fresh.Rebuild();

            var finderA = new PathFinder(nav);
            var finderB = new PathFinder(nav);
            var finderFresh = new PathFinder(fresh);

            int compared = 0;
            for (int trial = 0; trial < 400; trial++)
            {
                int a = rng.NextInt(cells.Size.CellCount);
                int b = rng.NextInt(cells.Size.CellCount);
                if (!nav.Grid.CanEnter(a, TraverseMode.Colonist)) continue;
                if (!nav.Grid.CanEnter(b, TraverseMode.Colonist)) continue;

                PathResult r1 = finderA.FindPath(a, b, TraverseMode.Colonist);
                int[] p1 = r1.Ok ? finderA.PathToArray() : Array.Empty<int>();
                PathResult r2 = finderA.FindPath(a, b, TraverseMode.Colonist);
                PathResult r3 = finderB.FindPath(a, b, TraverseMode.Colonist);
                PathResult r4 = finderFresh.FindPath(a, b, TraverseMode.Colonist);

                Assert.That(r2.Checksum, Is.EqualTo(r1.Checksum), "same finder, same request, twice");
                Assert.That(r3.Checksum, Is.EqualTo(r1.Checksum), "a second finder over the same graph");
                Assert.That(r4.Status, Is.EqualTo(r1.Status), "a graph rebuilt from scratch");
                Assert.That(r4.Checksum, Is.EqualTo(r1.Checksum),
                    "a path must not depend on the history of edits that produced the graph");

                if (r1.Ok)
                {
                    Assert.That(finderFresh.PathToArray(), Is.EqualTo(p1));
                    compared++;
                }
            }

            Assert.That(compared, Is.GreaterThan(50), "the fixture stopped finding paths");
        }

        [Test]
        public void APathNeverSkipsALayer()
        {
            var cells = NavWorld.MakeCells(23, 19, 4);
            var nav = new NavGraph(cells);
            var rng = new DeterministicRandom(31337u);

            for (int i = 0; i < cells.Size.CellCount; i++)
                if (rng.NextInt(100) < 30) cells.Flags[i] |= CellFlags.SolidTerrain;

            for (int s = 0; s < 30; s++)
            {
                int x = rng.NextInt(23), z = rng.NextInt(19), y = rng.NextInt(3);
                int lower = cells.Index(x, z, y);
                int upper = lower + cells.Size.LayerStride;
                cells.Flags[lower] &= ~CellFlags.SolidTerrain;
                cells.Flags[upper] &= ~CellFlags.SolidTerrain;
                nav.AddConnector(ConnectorKind.Stair, new[] { lower }, new[] { upper });
            }

            nav.MarkAllDirty();
            nav.Rebuild();
            var finder = new PathFinder(nav);

            int crossLayer = 0;
            for (int trial = 0; trial < 600; trial++)
            {
                int a = rng.NextInt(cells.Size.CellCount);
                int b = rng.NextInt(cells.Size.CellCount);
                if (!nav.Grid.CanEnter(a, TraverseMode.Colonist)) continue;
                if (!nav.Grid.CanEnter(b, TraverseMode.Colonist)) continue;

                PathResult r = finder.FindPath(a, b, TraverseMode.Colonist);
                if (!r.Ok) continue;

                int[] path = finder.PathToArray();
                Assert.That(finder.ValidatePath(path, TraverseMode.Colonist), Is.True,
                    "every consecutive pair of a path must be a real graph edge");

                for (int i = 1; i < path.Length; i++)
                {
                    CellRef p = cells.FromIndex(path[i - 1]);
                    CellRef q = cells.FromIndex(path[i]);
                    Assert.That(Math.Abs(p.Y - q.Y), Is.LessThanOrEqualTo(1),
                        "a path that skips a layer is a bug, not a convenience");
                    if (p.Y != q.Y)
                    {
                        crossLayer++;
                        Assert.That(nav.IsLegalStep(path[i - 1], path[i], TraverseMode.Colonist), Is.True,
                            "a layer change must be a declared portal, never an implied jump");
                    }
                }
            }

            Assert.That(crossLayer, Is.GreaterThan(0), "the fixture never crossed a layer");
        }

        [Test]
        public void AHandBuiltPathThatJumpsALayerIsRejected()
        {
            var cells = NavWorld.MakeCells(10, 10, 3);
            var nav = new NavGraph(cells);
            nav.AddConnector(ConnectorKind.Stair,
                new[] { cells.Index(5, 5, 0) }, new[] { cells.Index(5, 5, 1) });
            nav.Rebuild();
            var finder = new PathFinder(nav);

            // Two cells one layer apart with no connector between them: teleporting.
            int[] bogus = { cells.Index(1, 1, 0), cells.Index(1, 1, 1) };
            Assert.That(finder.ValidatePath(bogus, TraverseMode.Colonist), Is.False);

            // Two layers apart is worse, and must also be refused.
            int[] worse = { cells.Index(5, 5, 0), cells.Index(5, 5, 2) };
            Assert.That(finder.ValidatePath(worse, TraverseMode.Colonist), Is.False);

            // The declared portal is the one step that is legal.
            int[] real = { cells.Index(5, 5, 0), cells.Index(5, 5, 1) };
            Assert.That(finder.ValidatePath(real, TraverseMode.Colonist), Is.True);
        }

        [Test]
        public void AConnectorCannotDeclareEndsMoreThanOneLayerApart()
        {
            var cells = NavWorld.MakeCells(10, 10, 4);
            var nav = new NavGraph(cells);
            Assert.Throws<ArgumentException>(() => nav.AddConnector(ConnectorKind.Stair,
                new[] { cells.Index(5, 5, 0) }, new[] { cells.Index(5, 5, 2) }));
        }

        [Test]
        public void AFallEdgeIsOneWay()
        {
            var cells = NavWorld.MakeCells(10, 10, 2);
            var nav = new NavGraph(cells);

            // Mine the floor out of one cell on the upper layer. It becomes air: nothing to
            // stand on, nothing in the way.
            int hole = cells.Index(5, 5, 1);
            NavWorld.SetFloor(cells, nav, hole, false);
            nav.Rebuild();

            int ledge = cells.Index(4, 5, 1);
            int landing = cells.Index(5, 5, 0);

            Assert.That(nav.RegionOfCell(hole), Is.LessThan(0), "a hole is not part of any region");
            Assert.That(nav.Reachable(ledge, landing, TraverseMode.Colonist), Is.False,
                "an edge you cannot come back along must not merge two districts");

            var finder = new PathFinder(nav);
            var falling = new PathOptions(20_000, 20_000, allowFalls: true);

            PathResult down = finder.FindPath(ledge, landing, TraverseMode.Colonist, falling);
            Assert.That(down.Status, Is.EqualTo(PathStatus.Success));
            int[] path = finder.PathToArray();
            Assert.That(path, Is.EqualTo(new[] { ledge, landing }));
            Assert.That(down.Cost, Is.EqualTo(MoveCost.Fall));
            Assert.That(finder.ValidatePath(path, TraverseMode.Colonist, allowFalls: true), Is.True);
            Assert.That(finder.ValidatePath(path, TraverseMode.Colonist), Is.False,
                "without the falling flag the same step is not a legal edge");

            PathResult up = finder.FindPath(landing, ledge, TraverseMode.Colonist, falling);
            Assert.That(up.Status, Is.Not.EqualTo(PathStatus.Success),
                "a fall edge is one-way: you do not climb back up a hole");
        }

        [Test]
        public void TheBudgetIsCountedInNodesAndTheServiceDrainsInRequestOrder()
        {
            var cells = NavWorld.MakeCells(40, 40, 1);
            var nav = new NavGraph(cells);
            nav.Rebuild();
            var finder = new PathFinder(nav);

            int a = cells.Index(0, 0, 0);
            int b = cells.Index(39, 39, 0);

            PathResult tight = finder.FindPath(a, b, TraverseMode.Colonist, new PathOptions(8, 8));
            Assert.That(tight.Status, Is.EqualTo(PathStatus.BudgetExhausted));
            Assert.That(tight.CellNodes + tight.RegionNodes, Is.GreaterThan(0));

            var service = new PathService(finder, nodeBudgetPerTick: 100_000, nodeBudgetPerRequest: 20_000);
            for (int i = 0; i < 5; i++)
                service.Enqueue(new PathRequest(i, a, cells.Index(30 + i, 30, 0), TraverseMode.Colonist));

            service.Serve();
            Assert.That(service.Served.Count, Is.EqualTo(5));
            for (int i = 0; i < 5; i++)
            {
                Assert.That(service.Served[i].Request.AgentId, Is.EqualTo(i), "FIFO, ties by agent id");
                Assert.That(service.Served[i].Result.Ok, Is.True);
            }
        }
    }


    /// <summary>
    /// The experiment the unit exists to justify.
    ///
    /// The D1 architecture benchmark's phase 3 measured a naive layer-aware A-star at 65% of
    /// total tick time, with 1,058 of its 1,800 replans exhausting a 20,000-node budget and
    /// returning failure. The premise under test was that these were mostly futile searches for
    /// unreachable targets, and that an O(1) district comparison before the search would make
    /// them vanish. That premise was falsified — only 14% of the exhausted searches were actually
    /// district-unreachable, under 1% on a structured map (<c>docs/design/05-ai-and-jobs.md</c>
    /// §6) — but the architecture stayed right for a different reason: reachability must be
    /// answered before pathing because every job-giver scan asks it thousands of times per tick,
    /// and it must be free there. The replan speed-up this benchmark measures comes mostly from
    /// the abstract region stage and a better heuristic, not from the reachability check.
    ///
    /// Three arms over an identical, pre-recorded list of 1,800 (start, goal) requests:
    ///   A. naive — one cell A-star over the whole grid with a distance heuristic, no regions.
    ///   B. two-stage — abstract region search then corridor-constrained cell search, but with
    ///      the district check switched off, so unreachable goals are still searched for.
    ///   C. full — the district comparison first, then the two-stage search.
    ///
    /// Arm B is there to keep the answer honest: without it the result could not say whether a
    /// win came from the reachability check or merely from a better heuristic.
    ///
    /// Two worlds, because the answer turns out to depend on the world:
    ///   * the D1 mirror — 35% uniformly random solid, which is white noise, not architecture;
    ///   * a structured world — rooms, walls and doorways, which is what the game generates.
    /// </summary>
    [Explicit("Benchmark: builds a 2.5 M cell world. Run with --filter TestCategory=Benchmark.")]
    [Category("Benchmark")]
    public class PathingBenchmark
    {
        const int SizeX = 250, SizeZ = 250, SizeY = 40;
        const int Portals = 200;
        const int Pawns = 50;
        const int Requests = 1800;
        const int Budget = 20_000;
        // The heuristic's layer-change term, swept to show how much it matters. 0 means "use the
        // graph's own estimate from connector density", which is the shipping behaviour.
        static readonly int[] SweepHints = { 290, 1500, 10000, 0 };

        [Test]
        public void TheD1MirrorWorld()
        {
            // docs/research/d-01-bench/WORKLOAD.md phase 3, as closely as the cell model allows:
            // seed 12345, the same draw order, 35% solid, every open cell standable.
            uint s = 12345u;
            var size = new GridSize(SizeX, SizeZ, SizeY);
            var cells = new CellGrid(size);
            for (int i = 0; i < size.CellCount; i++)
            {
                cells.Floor[i] = 1;
                if (Next(ref s) % 100 < 35) cells.Flags[i] |= CellFlags.SolidTerrain;
            }

            for (int k = 0; k < 2000; k++) Next(ref s);                   // heat sources
            for (int i = 0; i < 20000; i++) { Next(ref s); Next(ref s); } // things

            Run("d1-mirror-35pc-noise", cells, ref s);
        }

        [Test]
        public void AStructuredWorldOfRoomsAndDoorways()
        {
            // The same scale and the same request generator, over something shaped like a
            // building instead of like static: 11 x 11 rooms on a wall lattice, one doorway per
            // wall segment, and light rubble inside the rooms.
            uint s = 12345u;
            var size = new GridSize(SizeX, SizeZ, SizeY);
            var cells = new CellGrid(size);
            for (int i = 0; i < size.CellCount; i++) cells.Floor[i] = 1;

            for (int y = 0; y < SizeY; y++)
            for (int z = 0; z < SizeZ; z++)
            for (int x = 0; x < SizeX; x++)
            {
                int idx = size.Index(x, z, y);
                bool wall = x % 12 == 0 || z % 12 == 0;
                if (wall)
                {
                    // One doorway per wall segment, placed by a hash of the segment so the world
                    // is reproducible without consuming the shared stream.
                    int seg = x % 12 == 0 ? Mix(x, z / 12, y) : Mix(x / 12, z, y);
                    int offset = 1 + seg % 11;
                    bool doorway = (x % 12 == 0 && z % 12 == offset)
                                   || (z % 12 == 0 && x % 12 == offset);
                    if (!doorway) cells.Flags[idx] |= CellFlags.SolidTerrain;
                }
                else if (Next(ref s) % 100 < 8)
                {
                    cells.Flags[idx] |= CellFlags.SolidTerrain;
                }
            }

            Run("structured-rooms-and-doorways", cells, ref s);
        }

        void Run(string scenario, CellGrid cells, ref uint s)
        {
            GridSize size = cells.Size;
            int cellCount = size.CellCount;
            var nav = new NavGraph(cells);

            int accepted = 0;
            while (accepted < Portals)
            {
                int x = (int)(Next(ref s) % SizeX);
                int z = (int)(Next(ref s) % SizeZ);
                int y = (int)(Next(ref s) % (SizeY - 1));
                int a = size.Index(x, z, y);
                int b = a + size.LayerStride;
                if ((cells.Flags[a] & CellFlags.SolidTerrain) != 0) continue;
                if ((cells.Flags[b] & CellFlags.SolidTerrain) != 0) continue;
                nav.AddConnector(ConnectorKind.Stair, new[] { a }, new[] { b });
                accepted++;
            }

            var pawnCell = new int[Pawns];
            for (int p = 0; p < Pawns; p++)
            {
                int idx;
                do { idx = (int)(Next(ref s) % cellCount); } while ((cells.Flags[idx] & CellFlags.SolidTerrain) != 0);
                pawnCell[p] = idx;
            }

            var buildWatch = Stopwatch.StartNew();
            nav.MarkAllDirty();
            nav.Rebuild();
            buildWatch.Stop();

            // Record the request stream once, so all three arms answer the same questions.
            var finder = new PathFinder(nav);
            var starts = new int[Requests];
            var goals = new int[Requests];
            var walk = (int[])pawnCell.Clone();
            for (int tick = 0; tick < Requests; tick++)
            {
                int p = tick % Pawns;
                CellRef here = size.FromIndex(walk[p]);
                int target = -1;
                for (int k = 0; k < 50; k++)
                {
                    int dx = (int)(Next(ref s) % 81) - 40;
                    int dz = (int)(Next(ref s) % 81) - 40;
                    int dy = (int)(Next(ref s) % 7) - 3;
                    int t = size.Index(Clamp(here.X + dx, SizeX), Clamp(here.Z + dz, SizeZ), Clamp(here.Y + dy, SizeY));
                    if (target < 0 && (cells.Flags[t] & CellFlags.SolidTerrain) == 0 && t != walk[p]) target = t;
                }

                starts[tick] = walk[p];
                goals[tick] = target < 0 ? walk[p] : target;

                // Advance the pawn along its path, as the workload's movement phase does, so
                // later requests do not all start where the pawn was dropped.
                PathResult r = finder.FindPath(starts[tick], goals[tick], TraverseMode.Colonist);
                if (r.Ok)
                {
                    int[] path = finder.PathToArray();
                    walk[p] = path[Math.Min(path.Length - 1, 63)];
                }
            }

            int unreachable = 0;
            int crossLayer = 0;
            for (int i = 0; i < Requests; i++)
            {
                if (!nav.Reachable(starts[i], goals[i], TraverseMode.Colonist)) unreachable++;
                else if (size.FromIndex(starts[i]).Y != size.FromIndex(goals[i]).Y) crossLayer++;
            }

            foreach (int hint in SweepHints)
            {
                Arm sweep = RunArm($"sweep hint={hint}", nav, starts, goals,
                    new PathOptions(Budget, Budget), hint);
                Console.WriteLine($"U17SWEEP scenario={scenario} hint={hint} total_ms={sweep.TotalMs:F1} " +
                                  $"ok={sweep.Ok} exhausted={sweep.Exhausted} " +
                                  $"cell_nodes={sweep.CellNodes} region_nodes={sweep.RegionNodes}");
            }

            // A0 reproduces the D1 baseline exactly: no regions, and the workload's own heuristic,
            // which prices a layer change at one orthogonal step. That last detail is the whole
            // reason its replans failed as often as they did.
            Arm baseline = RunArm("A0 naive, D1 heuristic", nav, starts, goals,
                new PathOptions(Budget, Budget, false, true, true), MoveCost.Orthogonal);
            Arm naive = RunArm("A naive cell A-star", nav, starts, goals,
                new PathOptions(Budget, Budget, false, true, true));
            Arm twoStage = RunArm("B two-stage, no district check", nav, starts, goals,
                new PathOptions(Budget, Budget, false, true, false));
            Arm full = RunArm("C two-stage + district check", nav, starts, goals,
                new PathOptions(Budget, Budget, false, false, false));

            Console.WriteLine($"U17RESULT scenario={scenario} world={SizeX}x{SizeZ}x{SizeY} portals={Portals} requests={Requests}");
            Console.WriteLine($"U17RESULT scenario={scenario} nav_build_ms={buildWatch.Elapsed.TotalMilliseconds:F1} " +
                              $"regions={nav.LiveRegionCount()} links={nav.LiveLinkCount()} " +
                              $"districts={nav.DistrictCount(TraverseMode.Colonist)} " +
                              $"cells_per_region={cellCount / Math.Max(1, nav.LiveRegionCount())}");
            Console.WriteLine($"U17RESULT scenario={scenario} district_unreachable={unreachable} of {Requests} " +
                              $"reachable_cross_layer={crossLayer}");
            foreach (Arm arm in new[] { baseline, naive, twoStage, full })
                Console.WriteLine($"U17RESULT scenario={scenario} arm=\"{arm.Name}\" total_ms={arm.TotalMs:F1} " +
                                  $"mean_ms={arm.TotalMs / Requests:F4} max_ms={arm.MaxMs:F3} " +
                                  $"ok={arm.Ok} unreachable={arm.Unreachable} budget_exhausted={arm.Exhausted} " +
                                  $"exhausted_in_abstract={arm.ExhaustedAbstract} " +
                                  $"cell_nodes={arm.CellNodes} region_nodes={arm.RegionNodes}");
            Console.WriteLine($"U17RESULT scenario={scenario} " +
                              $"speedup_vs_d1_baseline={baseline.TotalMs / Math.Max(0.0001, full.TotalMs):F1}x speedup_vs_naive={naive.TotalMs / Math.Max(0.0001, full.TotalMs):F1}x " +
                              $"speedup_vs_two_stage={twoStage.TotalMs / Math.Max(0.0001, full.TotalMs):F1}x");

            Assert.That(full.Exhausted, Is.LessThanOrEqualTo(naive.Exhausted),
                "the district check must not make budget exhaustion worse");
            Assert.That(full.TotalMs, Is.LessThan(naive.TotalMs),
                "if this fails the design is wrong and we need to know now");
        }

        struct Arm
        {
            public string Name;
            public double TotalMs;
            public double MaxMs;
            public int Ok, Unreachable, Exhausted, ExhaustedAbstract;
            public long CellNodes, RegionNodes;
        }

        static Arm RunArm(string name, NavGraph nav, int[] starts, int[] goals, PathOptions options,
            int layerChangeHint = 0)
        {
            var finder = new PathFinder(nav);
            if (layerChangeHint > 0) finder.LayerChangeHint = layerChangeHint;
            var arm = new Arm { Name = name };

            // One untimed pass so the scratch arrays are paged in and the JIT has seen the loop.
            for (int i = 0; i < 40; i++) finder.FindPath(starts[i], goals[i], TraverseMode.Colonist, options);

            var watch = new Stopwatch();
            for (int i = 0; i < starts.Length; i++)
            {
                watch.Restart();
                PathResult r = finder.FindPath(starts[i], goals[i], TraverseMode.Colonist, options);
                watch.Stop();
                double ms = watch.Elapsed.TotalMilliseconds;
                arm.TotalMs += ms;
                if (ms > arm.MaxMs) arm.MaxMs = ms;
                arm.CellNodes += r.CellNodes;
                arm.RegionNodes += r.RegionNodes;
                switch (r.Status)
                {
                    case PathStatus.Success:
                        arm.Ok++;
                        break;
                    case PathStatus.BudgetExhausted:
                        arm.Exhausted++;
                        if (r.CellNodes == 0) arm.ExhaustedAbstract++;
                        break;
                    default:
                        arm.Unreachable++;
                        break;
                }
            }

            return arm;
        }

        static int Clamp(int v, int max) => v < 0 ? 0 : v >= max ? max - 1 : v;

        static int Mix(int a, int b, int c)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)c) * 16777619u;
                h ^= h >> 15;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        static uint Next(ref uint s)
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return s;
        }
    }
}
