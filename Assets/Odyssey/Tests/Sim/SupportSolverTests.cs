#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Structural support and collapse, per docs/design/02-world-and-layers.md section 4.
    ///
    /// The load-bearing test in this file is <see cref="IncrementalSolveMatchesFullSolveOverRandomEdits"/>:
    /// the full solve is the specification, the incremental solve is the optimisation, and the
    /// only thing that keeps the optimisation honest is that they agree for every state.
    /// </summary>
    public class SupportSolverTests
    {
        const ushort Rock = 1;
        const ushort Concrete = 1;

        /// <summary>A world with a bedrock bottom layer and nothing else. Layer 0 is the deepest
        /// layer, as the storage convention requires; building happens on layer 1 and up.</summary>
        static CellGrid MakeGrid(int sizeX, int sizeZ, int sizeY)
        {
            var grid = new CellGrid(new GridSize(sizeX, sizeZ, sizeY));
            for (int local = 0; local < grid.Size.LayerStride; local++)
            {
                grid.Terrain[local] = Rock;
                grid.Flags[local] |= CellFlags.SolidTerrain;
            }
            return grid;
        }

        static void SetFloor(CellGrid grid, int x, int z, int y)
        {
            int i = grid.Index(x, z, y);
            grid.Floor[i] = Concrete;
            grid.FloorStuff[i] = Concrete;
        }

        static void SetEdifice(CellGrid grid, int x, int z, int y, int handle)
        {
            int i = grid.Index(x, z, y);
            grid.Edifice[i] = handle;
            grid.Flags[i] |= CellFlags.BlockingEdifice;
        }

        static void ClearEdifice(CellGrid grid, int x, int z, int y)
        {
            int i = grid.Index(x, z, y);
            grid.Edifice[i] = -1;
            grid.Flags[i] &= ~CellFlags.BlockingEdifice;
        }

        static int Support(CellGrid grid, int x, int z, int y) => grid.Support[grid.Index(x, z, y)];

        static bool HasSlab(CellGrid grid, int x, int z, int y) => grid.Floor[grid.Index(x, z, y)] != 0;

        // ----------------------------------------------------------------------------------
        // The rule
        // ----------------------------------------------------------------------------------

        [Test]
        public void SlabRestingOnSolidGroundIsFullySupported()
        {
            var grid = MakeGrid(5, 5, 3);
            SetFloor(grid, 2, 2, 1);

            var solver = new SupportSolver(grid);
            var collapsed = solver.SolveFull();

            Assert.That(Support(grid, 2, 2, 1), Is.EqualTo(solver.MaxSupport));
            Assert.That(collapsed, Is.Empty);
            Assert.That(HasSlab(grid, 2, 2, 1), Is.True);
        }

        [Test]
        public void SupportDecaysByOnePerCellFromAWall()
        {
            // A wall on layer 1 holds up the slab directly above it; the run of slabs beside it
            // spans outwards, losing one point of support per cell.
            var grid = MakeGrid(12, 3, 4);
            SetEdifice(grid, 0, 1, 1, handle: 10);
            for (int x = 0; x < 4; x++) SetFloor(grid, x, 1, 2);

            var solver = new SupportSolver(grid);
            var collapsed = solver.SolveFull();

            Assert.That(Support(grid, 0, 1, 2), Is.EqualTo(4));
            Assert.That(Support(grid, 1, 1, 2), Is.EqualTo(3));
            Assert.That(Support(grid, 2, 1, 2), Is.EqualTo(2));
            Assert.That(Support(grid, 3, 1, 2), Is.EqualTo(1));
            Assert.That(collapsed, Is.Empty);
        }

        [Test]
        public void SpanLongerThanMaxSupportIsUnsupportedAndComesDown()
        {
            var grid = MakeGrid(12, 3, 4);
            SetEdifice(grid, 0, 1, 1, handle: 10);
            for (int x = 0; x < 8; x++) SetFloor(grid, x, 1, 2);

            var solver = new SupportSolver(grid);
            var collapsed = solver.SolveFull();

            // Four cells of reach: the fifth and everything past it is holding nothing up.
            for (int x = 0; x < 4; x++) Assert.That(HasSlab(grid, x, 1, 2), Is.True, $"x={x}");
            for (int x = 4; x < 8; x++) Assert.That(HasSlab(grid, x, 1, 2), Is.False, $"x={x}");
            Assert.That(collapsed.Count, Is.EqualTo(4));
            Assert.That(collapsed[0], Is.EqualTo(new CellRef(4, 1, 2)));
            Assert.That(collapsed[3], Is.EqualTo(new CellRef(7, 1, 2)));
        }

        [Test]
        public void APillarInTheMiddleExtendsReach()
        {
            var grid = MakeGrid(12, 3, 4);
            SetEdifice(grid, 0, 1, 1, handle: 10);
            SetEdifice(grid, 7, 1, 1, handle: 11); // the pillar
            for (int x = 0; x < 8; x++) SetFloor(grid, x, 1, 2);

            var solver = new SupportSolver(grid);
            var collapsed = solver.SolveFull();

            Assert.That(collapsed, Is.Empty, "the pillar should carry the far end of the span");
            Assert.That(Support(grid, 0, 1, 2), Is.EqualTo(4));
            Assert.That(Support(grid, 3, 1, 2), Is.EqualTo(1));
            Assert.That(Support(grid, 4, 1, 2), Is.EqualTo(1), "reached from the pillar instead");
            Assert.That(Support(grid, 7, 1, 2), Is.EqualTo(4));
        }

        // ----------------------------------------------------------------------------------
        // Collapse
        // ----------------------------------------------------------------------------------

        [Test]
        public void RemovingTheLastSupportCollapsesTheSlab()
        {
            var grid = MakeGrid(12, 3, 4);
            SetEdifice(grid, 0, 1, 1, handle: 10);
            for (int x = 0; x < 4; x++) SetFloor(grid, x, 1, 2);

            var solver = new SupportSolver(grid);
            Assert.That(solver.SolveFull(), Is.Empty);

            ClearEdifice(grid, 0, 1, 1);
            solver.MarkDirty(0, 1, 1);
            var collapsed = solver.SolveIncremental();

            Assert.That(collapsed.Count, Is.EqualTo(4));
            for (int x = 0; x < 4; x++) Assert.That(HasSlab(grid, x, 1, 2), Is.False, $"x={x}");
        }

        [Test]
        public void CollapseCascadesToEverythingThatDependedOnIt()
        {
            // One pillar carrying a diamond of slab four cells across. Pull it and the whole
            // section comes down in the same pass, not one tile.
            var grid = MakeGrid(9, 9, 3);
            SetEdifice(grid, 3, 3, 1, handle: 7);

            var expected = new List<CellRef>();
            for (int z = 0; z < 9; z++)
            for (int x = 0; x < 9; x++)
            {
                if (Math.Abs(x - 3) + Math.Abs(z - 3) > 3) continue;
                SetFloor(grid, x, z, 2);
                expected.Add(new CellRef(x, z, 2));
            }
            Assert.That(expected.Count, Is.EqualTo(25));

            var solver = new SupportSolver(grid);
            Assert.That(solver.SolveFull(), Is.Empty, "the diamond is within reach of the pillar");
            Assert.That(Support(grid, 3, 3, 2), Is.EqualTo(4));
            Assert.That(Support(grid, 0, 3, 2), Is.EqualTo(1));

            ClearEdifice(grid, 3, 3, 1);
            solver.MarkDirty(3, 3, 1);
            var collapsed = solver.SolveIncremental();

            Assert.That(collapsed.Count, Is.EqualTo(25));
            foreach (var cell in expected)
                Assert.That(grid.Floor[grid.Index(cell)], Is.EqualTo(0), $"{cell} should have fallen");

            // Reported in ascending index order, so the report is reproducible.
            for (int k = 1; k < collapsed.Count; k++)
                Assert.That(grid.Index(collapsed[k]), Is.GreaterThan(grid.Index(collapsed[k - 1])));
        }

        [Test]
        public void MiningAWallDropsTheFloorAboveTheNextRoom()
        {
            // Two rooms in a ruined shell, sharing a wall. The shared wall is the only thing
            // reaching the far half of the upper floor. This is the design's central case.
            var grid = MakeGrid(12, 3, 4);
            SetEdifice(grid, 0, 1, 1, handle: 1);  // outer wall
            SetEdifice(grid, 5, 1, 1, handle: 2);  // shared wall
            for (int x = 0; x < 9; x++) SetFloor(grid, x, 1, 2);

            var solver = new SupportSolver(grid);
            Assert.That(solver.SolveFull(), Is.Empty, "the shell is consistent as stamped");

            // A colonist mines the shared wall for salvage.
            ClearEdifice(grid, 5, 1, 1);
            solver.MarkDirty(5, 1, 1);
            var collapsed = solver.SolveIncremental();

            Assert.That(Support(grid, 0, 1, 2), Is.EqualTo(4));
            Assert.That(Support(grid, 3, 1, 2), Is.EqualTo(1));
            Assert.That(collapsed.Count, Is.EqualTo(5), "the ceiling of the next room comes down");
            for (int x = 0; x < 4; x++) Assert.That(HasSlab(grid, x, 1, 2), Is.True, $"x={x}");
            for (int x = 4; x < 9; x++) Assert.That(HasSlab(grid, x, 1, 2), Is.False, $"x={x}");
        }

        // ----------------------------------------------------------------------------------
        // Ruined shells
        // ----------------------------------------------------------------------------------

        [Test]
        public void StampedShellStandsUntilSomethingBeneathItChanges()
        {
            // A pre-war slab spanning ten cells off a single wall. Nothing in our rule could hold
            // that up, which is the point: worldgen stamps it already standing.
            var grid = MakeGrid(12, 3, 4);
            SetEdifice(grid, 0, 1, 1, handle: 1);
            for (int x = 0; x < 10; x++) SetFloor(grid, x, 1, 2);

            var solver = new SupportSolver(grid);
            for (int x = 0; x < 10; x++) solver.MarkSupportedByConstruction(x, 1, 2);

            Assert.That(solver.SolveFull(), Is.Empty, "a stamped shell begins life standing");
            for (int x = 0; x < 10; x++) Assert.That(Support(grid, x, 1, 2), Is.EqualTo(4), $"x={x}");

            // Something is cleared out of the room under one cell. That cell alone loses its
            // trust and is re-validated by the ordinary rule; its neighbours still carry it.
            solver.MarkDirty(6, 1, 1);
            Assert.That(solver.SolveIncremental(), Is.Empty);
            Assert.That(solver.IsSupportedByConstruction(new CellRef(6, 1, 2)), Is.False);
            Assert.That(Support(grid, 6, 1, 2), Is.EqualTo(3), "re-validated, not assumed");

            // Now the whole storey below is disturbed. With no trust left the ordinary rule is
            // all there is, and the over-span is condemned.
            for (int x = 0; x < 10; x++) solver.MarkDirty(x, 1, 1);
            var collapsed = solver.SolveIncremental();

            Assert.That(Support(grid, 0, 1, 2), Is.EqualTo(4));
            Assert.That(Support(grid, 3, 1, 2), Is.EqualTo(1));
            Assert.That(collapsed.Count, Is.EqualTo(6));
            Assert.That(collapsed[0], Is.EqualTo(new CellRef(4, 1, 2)));
            Assert.That(collapsed[5], Is.EqualTo(new CellRef(9, 1, 2)));
        }

        [Test]
        public void ClearingConstructionMarksIsTheWorldgenConsistencyCheck()
        {
            // Worldgen's final assertion: with no stamped trust, does the template stand up?
            var grid = MakeGrid(12, 3, 4);
            SetEdifice(grid, 0, 1, 1, handle: 1);
            for (int x = 0; x < 10; x++) SetFloor(grid, x, 1, 2);

            var solver = new SupportSolver(grid);
            for (int x = 0; x < 10; x++) solver.MarkSupportedByConstruction(x, 1, 2);
            Assert.That(solver.SolveFull(), Is.Empty);

            solver.ClearAllConstructionMarks();
            var collapsed = solver.SolveFull();

            Assert.That(collapsed.Count, Is.EqualTo(6), "the template is a content bug");
        }

        // ----------------------------------------------------------------------------------
        // The oracle
        // ----------------------------------------------------------------------------------

        /// <summary>
        /// The test that matters most in this unit. The full solve is the specification; the
        /// incremental solve is an optimisation that is only allowed to exist while it produces
        /// the identical world — every support value, every slab, every flag, and the same
        /// collapse report in the same order.
        ///
        /// Each iteration applies a handful of random structural edits, clones the world, gives
        /// the clone a fresh solver with the same construction marks, and makes the two solvers
        /// judge that one world. Divergence is reported with the cell and both values, because a
        /// pass/fail on a 2,300-cell grid is not something anyone can debug.
        ///
        /// S_max is varied as well as the seed: a small S_max makes spans short and the world
        /// permanently on the edge of collapse, which is where a frontier bug would hide.
        /// </summary>
        [TestCase(4u, 20260915u, 1000)]
        [TestCase(2u, 76543u, 1000)]
        [TestCase(6u, 987654321u, 600)]
        public void IncrementalSolveMatchesFullSolveOverRandomEdits(uint maxSupport, uint seed, int iterations)
        {
            var rng = new DeterministicRandom(seed);
            var grid = new CellGrid(new GridSize(24, 24, 4));
            var solver = new SupportSolver(grid, (int)maxSupport);

            // A messy, deliberately fragile starting world: slabs nearly everywhere but supports
            // scarce, so spans are long, marginal, and constantly on the edge of coming down.
            // Collapse is the path most likely to hide a divergence, so the fixture has to keep
            // provoking it rather than settling into a well-propped state. Even the bedrock layer
            // is patchy, which is why most of what gets built has to reach for its support.
            for (int local = 0; local < grid.Size.LayerStride; local++)
            {
                if (rng.NextInt(70) != 0) continue;
                grid.Terrain[local] = Rock;
                grid.Flags[local] |= CellFlags.SolidTerrain;
            }

            for (int y = 1; y < grid.Size.SizeY; y++)
            for (int z = 0; z < grid.Size.SizeZ; z++)
            for (int x = 0; x < grid.Size.SizeX; x++)
            {
                int i = grid.Index(x, z, y);
                int roll = rng.NextInt(90);
                if (roll == 0)
                {
                    grid.Terrain[i] = Rock;
                    grid.Flags[i] |= CellFlags.SolidTerrain;
                }
                else if (roll == 1)
                {
                    grid.Edifice[i] = 1000 + i;
                    grid.Flags[i] |= CellFlags.BlockingEdifice;
                }
                if (rng.NextInt(8) != 0)
                {
                    grid.Floor[i] = Concrete;
                    grid.FloorStuff[i] = Concrete;
                }
            }

            solver.SolveFull();

            int totalEdits = 0;
            int totalCollapses = 0;

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                int edits = rng.NextInt(1, 6);
                for (int e = 0; e < edits; e++)
                {
                    int i = rng.NextInt(grid.Size.CellCount);
                    solver.MarkDirty(i);
                    totalEdits++;

                    switch (rng.NextInt(5))
                    {
                        case 0: // build or breach a slab
                            bool slab = grid.Floor[i] == 0;
                            grid.Floor[i] = slab ? Concrete : (ushort)0;
                            grid.FloorStuff[i] = grid.Floor[i];
                            break;
                        case 1: // mine or place natural rock
                            if ((grid.Flags[i] & CellFlags.SolidTerrain) != 0)
                            {
                                grid.Flags[i] &= ~CellFlags.SolidTerrain;
                                grid.Terrain[i] = 0;
                            }
                            else
                            {
                                grid.Flags[i] |= CellFlags.SolidTerrain;
                                grid.Terrain[i] = Rock;
                            }
                            break;
                        case 2: // build or deconstruct a wall
                            if (grid.Edifice[i] >= 0)
                            {
                                grid.Edifice[i] = -1;
                                grid.Flags[i] &= ~CellFlags.BlockingEdifice;
                            }
                            else
                            {
                                grid.Edifice[i] = 1000 + i;
                                grid.Flags[i] |= CellFlags.BlockingEdifice;
                            }
                            break;
                        case 3: // worldgen-style stamping onto a live world
                            if (grid.Floor[i] == 0)
                            {
                                grid.Floor[i] = Concrete;
                                grid.FloorStuff[i] = Concrete;
                            }
                            solver.MarkSupportedByConstruction(i);
                            break;
                        default: // an edit that changes nothing, which must also be handled
                            break;
                    }
                }

                // Take the oracle's copy after the edits and before either solve, so both are
                // judging exactly the same world.
                var copy = CloneGrid(grid);
                var oracle = new SupportSolver(copy, (int)maxSupport);
                for (int i = 0; i < grid.Size.CellCount; i++)
                    if (solver.IsSupportedByConstruction(i))
                        oracle.MarkSupportedByConstruction(i);

                var fromFull = ToArray(oracle.SolveFull());
                var fromIncremental = ToArray(solver.SolveIncremental());
                totalCollapses += fromIncremental.Length;

                AssertGridsMatch(grid, copy, iteration);
                Assert.That(fromIncremental, Is.EqualTo(fromFull), $"collapse report differs at iteration {iteration}");
            }

            TestContext.WriteLine($"oracle S_max={maxSupport} seed={seed}: {iterations} iterations, {totalEdits} edits, {totalCollapses} collapses");
            Assert.That(totalCollapses, Is.GreaterThan(0), "the run must actually exercise collapse");
        }

        static CellRef[] ToArray(IReadOnlyList<CellRef> list)
        {
            var result = new CellRef[list.Count];
            for (int i = 0; i < list.Count; i++) result[i] = list[i];
            return result;
        }

        static CellGrid CloneGrid(CellGrid source)
        {
            var copy = new CellGrid(source.Size);
            Array.Copy(source.Terrain, copy.Terrain, source.Terrain.Length);
            Array.Copy(source.Floor, copy.Floor, source.Floor.Length);
            Array.Copy(source.FloorStuff, copy.FloorStuff, source.FloorStuff.Length);
            Array.Copy(source.Edifice, copy.Edifice, source.Edifice.Length);
            Array.Copy(source.Support, copy.Support, source.Support.Length);
            Array.Copy(source.Region, copy.Region, source.Region.Length);
            Array.Copy(source.Flags, copy.Flags, source.Flags.Length);
            return copy;
        }

        static void AssertGridsMatch(CellGrid actual, CellGrid expected, int iteration)
        {
            for (int i = 0; i < actual.Size.CellCount; i++)
            {
                if (actual.Support[i] == expected.Support[i]
                    && actual.Floor[i] == expected.Floor[i]
                    && actual.FloorStuff[i] == expected.FloorStuff[i]
                    && actual.Flags[i] == expected.Flags[i])
                    continue;

                var cell = actual.Size.FromIndex(i);
                Assert.Fail(
                    $"iteration {iteration}, cell {cell}: " +
                    $"support incremental={actual.Support[i]} full={expected.Support[i]}, " +
                    $"floor incremental={actual.Floor[i]} full={expected.Floor[i]}, " +
                    $"flags incremental={actual.Flags[i]} full={expected.Flags[i]}");
            }
        }

        // ----------------------------------------------------------------------------------
        // Plumbing
        // ----------------------------------------------------------------------------------

        [Test]
        public void SupportIfBuiltAnswersTheBuildPreviewQuestion()
        {
            var grid = MakeGrid(12, 3, 4);
            SetEdifice(grid, 0, 1, 1, handle: 10);
            for (int x = 0; x < 4; x++) SetFloor(grid, x, 1, 2);

            var solver = new SupportSolver(grid);
            solver.SolveFull();

            Assert.That(solver.SupportIfBuilt(new CellRef(0, 1, 2)), Is.EqualTo(4), "sits on the wall");
            Assert.That(solver.SupportIfBuilt(new CellRef(4, 1, 2)), Is.EqualTo(0), "one cell too far");
            Assert.That(solver.SupportIfBuilt(new CellRef(3, 1, 2)), Is.EqualTo(1), "just reachable");
            // Support is stored as zero where there is no slab, because load cannot cross a hole.
            Assert.That(Support(grid, 4, 1, 2), Is.EqualTo(0));
        }

        [Test]
        public void SolvingWithNothingDirtyIsANoOp()
        {
            var grid = MakeGrid(6, 6, 3);
            SetFloor(grid, 2, 2, 1);
            var solver = new SupportSolver(grid);
            solver.SolveFull();

            Assert.That(solver.DirtyCount, Is.EqualTo(0));
            Assert.That(solver.SolveIncremental(), Is.Empty);
            Assert.That(Support(grid, 2, 2, 1), Is.EqualTo(4));
        }

        [Test]
        public void ScanForDirtyPicksUpFlagsSetByOtherSystems()
        {
            var grid = MakeGrid(12, 3, 4);
            SetEdifice(grid, 0, 1, 1, handle: 10);
            for (int x = 0; x < 4; x++) SetFloor(grid, x, 1, 2);
            var solver = new SupportSolver(grid);
            solver.SolveFull();

            // A system that writes the flag directly rather than calling MarkDirty.
            ClearEdifice(grid, 0, 1, 1);
            grid.Flags[grid.Index(0, 1, 1)] |= CellFlags.SupportDirty;
            solver.ScanForDirty();

            Assert.That(solver.DirtyCount, Is.EqualTo(1));
            Assert.That(solver.SolveIncremental().Count, Is.EqualTo(4));
            Assert.That(grid.Flags[grid.Index(0, 1, 1)] & CellFlags.SupportDirty, Is.EqualTo(CellFlags.None));
        }

        [Test]
        public void ScanForDirtyRevokesConstructionTrustJustAsMarkDirtyDoes()
        {
            // The two entry points must mean exactly the same thing, or a stamped shell would
            // survive an edit purely because of which one the editing system happened to call.
            var grid = MakeGrid(12, 3, 4);
            SetEdifice(grid, 0, 1, 1, handle: 1);
            for (int x = 0; x < 10; x++) SetFloor(grid, x, 1, 2);

            var solver = new SupportSolver(grid);
            for (int x = 0; x < 10; x++) solver.MarkSupportedByConstruction(x, 1, 2);
            solver.SolveFull();

            for (int x = 0; x < 10; x++) grid.Flags[grid.Index(x, 1, 1)] |= CellFlags.SupportDirty;
            solver.ScanForDirty();

            for (int x = 0; x < 10; x++)
                Assert.That(solver.IsSupportedByConstruction(new CellRef(x, 1, 2)), Is.False, $"x={x}");
            Assert.That(solver.SolveIncremental().Count, Is.EqualTo(6));
        }

        [Test]
        public void DirtyMarkingIsIdempotent()
        {
            var grid = MakeGrid(6, 6, 3);
            var solver = new SupportSolver(grid);
            solver.MarkDirty(2, 2, 1);
            solver.MarkDirty(2, 2, 1);
            solver.MarkDirty(new CellRef(2, 2, 1));
            Assert.That(solver.DirtyCount, Is.EqualTo(1));
        }

        [Test]
        public void MaxSupportIsAParameter()
        {
            var grid = MakeGrid(12, 3, 4);
            SetEdifice(grid, 0, 1, 1, handle: 10);
            for (int x = 0; x < 4; x++) SetFloor(grid, x, 1, 2);

            var solver = new SupportSolver(grid, maxSupport: 2);
            var collapsed = solver.SolveFull();

            Assert.That(Support(grid, 0, 1, 2), Is.EqualTo(2));
            Assert.That(Support(grid, 1, 1, 2), Is.EqualTo(1));
            Assert.That(collapsed.Count, Is.EqualTo(2));
        }

        [Test]
        public void FullSolveHandlesTheScaleTarget()
        {
            // 250 x 250 x 40 = 2.5 million cells. Ten layers of rock, then thirty storeys of
            // slab on a regular grid of columns: dense work on every layer, no short cuts.
            var grid = new CellGrid(GridSize.ScaleTarget);
            var size = grid.Size;

            for (int y = 0; y < 10; y++)
            for (int local = 0; local < size.LayerStride; local++)
            {
                int i = y * size.LayerStride + local;
                grid.Terrain[i] = Rock;
                grid.Flags[i] |= CellFlags.SolidTerrain;
            }

            for (int y = 10; y < size.SizeY; y++)
            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
            {
                int i = size.Index(x, z, y);
                grid.Floor[i] = Concrete;
                grid.FloorStuff[i] = Concrete;
                if (y < size.SizeY - 1 && x % 3 == 0 && z % 3 == 0)
                {
                    grid.Edifice[i] = i;
                    grid.Flags[i] |= CellFlags.BlockingEdifice;
                }
            }

            var solver = new SupportSolver(grid);
            var watch = Stopwatch.StartNew();
            var collapsed = solver.SolveFull();
            watch.Stop();

            TestContext.WriteLine($"full solve at {size}: {watch.ElapsedMilliseconds} ms, {collapsed.Count} collapses");
            Assert.That(collapsed, Is.Empty, "every slab is within reach of a column");
            Assert.That(Support(grid, 1, 1, 20), Is.EqualTo(2));
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(5000), "a full solve must stay linear");
        }

        [Test]
        public void IncrementalSolveAtTheScaleTargetTouchesOnlyTheFrontier()
        {
            var grid = new CellGrid(GridSize.ScaleTarget);
            var size = grid.Size;
            for (int local = 0; local < size.LayerStride; local++)
            {
                grid.Terrain[local] = Rock;
                grid.Flags[local] |= CellFlags.SolidTerrain;
            }
            for (int local = 0; local < size.LayerStride; local++)
            {
                int i = size.LayerStride + local;
                grid.Floor[i] = Concrete;
                grid.FloorStuff[i] = Concrete;
            }

            var solver = new SupportSolver(grid);
            solver.SolveFull();

            // One cell of rock mined out from under the middle of a floor spanning the map.
            int mined = size.Index(125, 125, 0);
            grid.Flags[mined] &= ~CellFlags.SolidTerrain;
            grid.Terrain[mined] = 0;
            solver.MarkDirty(mined);

            var watch = Stopwatch.StartNew();
            var collapsed = solver.SolveIncremental();
            watch.Stop();

            TestContext.WriteLine($"incremental solve after one edit: {watch.Elapsed.TotalMilliseconds:F3} ms");
            Assert.That(collapsed, Is.Empty, "the slab is still carried by its neighbours");
            Assert.That(Support(grid, 125, 125, 1), Is.EqualTo(3));
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(50), "one edit must not cost a map scan");
        }
    }
}
