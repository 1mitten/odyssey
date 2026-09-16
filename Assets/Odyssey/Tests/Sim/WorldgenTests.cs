#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Worldgen (U11, U12, U13): the ten ruined-city passes of
    /// docs/design/02-world-and-layers.md section 6.
    ///
    /// The tests that matter most are the determinism ones. A generator that is almost
    /// deterministic is worse than one that is obviously not, because the desync it causes turns
    /// up a milestone later in a save file nobody can reproduce.
    /// </summary>
    public class WorldgenTests
    {
        /// <summary>The slice map: 60 x 60 x 5 — one service layer, street level, three storeys.</summary>
        static readonly GridSize SliceSize = new GridSize(60, 60, 5);

        /// <summary>Deep enough to have every stratum band, small enough to stay fast.</summary>
        static readonly GridSize DeepSize = new GridSize(40, 40, 36);

        static WorldGenResult Generate(uint seed, GridSize? size = null, int throughPass = WorldGenerator.PassCount)
        {
            var actual = size ?? SliceSize;
            var grid = new CellGrid(actual);
            var gen = MapGenDef.For(actual);
            return WorldGenerator.Generate(grid, seed, gen, TemplateLibrary.Slice(), throughPass, null);
        }

        // ---------------------------------------------------------------- determinism

        [Test]
        public void SameSeedProducesAnIdenticalGrid()
        {
            var a = Generate(12345);
            var b = Generate(12345);

            Assert.That(b.GridHash, Is.EqualTo(a.GridHash), "same seed, different grid hash");
            Assert.That(b.Report.ShellsStamped, Is.EqualTo(a.Report.ShellsStamped));
            Assert.That(b.Report.DamagedCells, Is.EqualTo(a.Report.DamagedCells));
            Assert.That(b.Report.SalvageCells, Is.EqualTo(a.Report.SalvageCells));
            Assert.That(b.StartCell, Is.EqualTo(a.StartCell));
        }

        [Test]
        public void GenerationCarriesNoStateBetweenRuns()
        {
            // Generating something else in between must not shift a later map by one draw. If any
            // pass ever reaches for a shared or static random stream, this is what catches it.
            var first = Generate(777);
            Generate(999, DeepSize);
            Generate(31337);
            var again = Generate(777);

            Assert.That(again.GridHash, Is.EqualTo(first.GridHash));
        }

        [Test]
        public void DifferentSeedsProduceDifferentGrids()
        {
            var hashes = new HashSet<ulong>();
            for (uint seed = 1; seed <= 8; seed++)
                Assert.That(hashes.Add(Generate(seed).GridHash), Is.True, $"seed {seed} collided");
        }

        [Test]
        public void EveryFieldIsIntegerSoTheGridHashIsMachineIndependent()
        {
            // StateHash refuses to fold a float on purpose. Generating and hashing at all is the
            // proof that nothing in worldgen smuggled one into a cell field.
            var result = Generate(4242);
            Assert.That(result.GridHash, Is.Not.Zero);
        }

        // ---------------------------------------------------------------- pass 1, streets

        [Test]
        public void StreetsExistOnBothAxes()
        {
            var result = Generate(5, SliceSize, throughPass: 1);
            Assert.That(result.Report.StreetsX, Is.GreaterThan(0));
            Assert.That(result.Report.StreetsZ, Is.GreaterThan(0));
            Assert.That(result.Report.StreetColumns, Is.GreaterThan(0));
            Assert.That(result.Report.StreetColumns, Is.LessThan(SliceSize.LayerStride),
                "a map that is all street is not a city");
        }

        [Test]
        public void TheStreetNetworkIsConnected()
        {
            for (uint seed = 1; seed <= 6; seed++)
            {
                var result = Generate(seed, SliceSize, throughPass: 1);
                var context = result.Context;
                AssertStreetsConnected(context);
            }
        }

        static void AssertStreetsConnected(WorldGenContext context)
        {
            int sizeX = context.Size.SizeX, sizeZ = context.Size.SizeZ;
            int start = -1, total = 0;
            for (int i = 0; i < context.IsStreet.Length; i++)
            {
                if (!context.IsStreet[i]) continue;
                total++;
                if (start < 0) start = i;
            }
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "no street cells at all");

            var seen = new bool[context.IsStreet.Length];
            var stack = new Stack<int>();
            stack.Push(start);
            seen[start] = true;
            int reached = 0;
            while (stack.Count > 0)
            {
                int column = stack.Pop();
                reached++;
                int x = column % sizeX, z = column / sizeX;
                if (x > 0) Visit(column - 1);
                if (x + 1 < sizeX) Visit(column + 1);
                if (z > 0) Visit(column - sizeX);
                if (z + 1 < sizeZ) Visit(column + sizeX);

                void Visit(int neighbour)
                {
                    if (seen[neighbour] || !context.IsStreet[neighbour]) return;
                    seen[neighbour] = true;
                    stack.Push(neighbour);
                }
            }

            Assert.That(reached, Is.EqualTo(total), "the street network is in more than one piece");
        }

        [Test]
        public void BlocksNeverOverlapAStreet()
        {
            var result = Generate(21, SliceSize, throughPass: 1);
            var context = result.Context;
            foreach (var block in context.Blocks)
            {
                for (int x = block.X0; x <= block.X1; x++)
                    Assert.That(context.IsStreetX[x], Is.False, $"{block} covers street line x={x}");
                for (int z = block.Z0; z <= block.Z1; z++)
                    Assert.That(context.IsStreetZ[z], Is.False, $"{block} covers street line z={z}");
            }
        }

        // ---------------------------------------------------------------- passes 2 and 3

        [Test]
        public void PlotsTileTheirBlocksWithoutOverlapping()
        {
            var result = Generate(88, SliceSize, throughPass: 2);
            var context = result.Context;
            Assert.That(context.Plots.Count, Is.GreaterThan(0));

            var owner = new int[context.Columns];
            for (int i = 0; i < owner.Length; i++) owner[i] = -1;

            for (int p = 0; p < context.Plots.Count; p++)
            {
                var plot = context.Plots[p];
                for (int z = plot.Z0; z <= plot.Z1; z++)
                for (int x = plot.X0; x <= plot.X1; x++)
                {
                    int column = context.Column(x, z);
                    Assert.That(owner[column], Is.EqualTo(-1),
                        $"plot {p} overlaps plot {owner[column]} at ({x},{z})");
                    Assert.That(context.IsStreet[column], Is.False, $"plot {p} sits on a street");
                    owner[column] = p;
                }
            }
        }

        [Test]
        public void StampedShellsLandInsideTheirPlotAndDoNotOverlap()
        {
            for (uint seed = 1; seed <= 4; seed++)
            {
                var result = Generate(seed, SliceSize, throughPass: 3);
                var context = result.Context;
                Assert.That(context.Shells.Count, Is.GreaterThan(0), $"seed {seed} stamped nothing");

                var occupied = new bool[context.Columns];
                foreach (var shell in context.Shells)
                {
                    var plot = context.Plots[shell.PlotIndex];
                    var template = context.Templates[shell.TemplateIndex];

                    Assert.That(shell.X0, Is.GreaterThanOrEqualTo(plot.X0));
                    Assert.That(shell.Z0, Is.GreaterThanOrEqualTo(plot.Z0));
                    Assert.That(shell.X0 + template.SizeX - 1, Is.LessThanOrEqualTo(plot.X1),
                        $"{template.Id} overruns {plot} in x");
                    Assert.That(shell.Z0 + template.SizeZ - 1, Is.LessThanOrEqualTo(plot.Z1),
                        $"{template.Id} overruns {plot} in z");

                    for (int z = shell.Z0; z < shell.Z0 + template.SizeZ; z++)
                    for (int x = shell.X0; x < shell.X0 + template.SizeX; x++)
                    {
                        int column = context.Column(x, z);
                        Assert.That(occupied[column], Is.False, $"two shells overlap at ({x},{z})");
                        occupied[column] = true;
                    }
                }
            }
        }

        [Test]
        public void StampingWritesSlabsWallsAndVerticalExtent()
        {
            var result = Generate(64, SliceSize, throughPass: 3);
            var context = result.Context;
            var grid = context.Grid;

            Assert.That(result.Report.StampedCells, Is.GreaterThan(0));

            int walls = 0, slabs = 0, layersTouched = 0;
            for (int y = 0; y < context.Size.SizeY; y++)
            {
                bool touched = false;
                for (int i = y * context.Size.LayerStride; i < (y + 1) * context.Size.LayerStride; i++)
                {
                    if (!context.IsClaimed(i)) continue;
                    touched = true;
                    if (grid.Floor[i] != 0) slabs++;
                    if (grid.Edifice[i] >= 0) walls++;
                }
                if (touched) layersTouched++;
            }

            Assert.That(walls, Is.GreaterThan(0), "no edifices stamped");
            Assert.That(slabs, Is.GreaterThan(0), "no slabs stamped");
            Assert.That(layersTouched, Is.GreaterThan(1), "templates only reached one layer");
        }

        [Test]
        public void StampedSlabsBeginLifeSupportedByConstruction()
        {
            var result = Generate(65, SliceSize, throughPass: 3);
            var context = result.Context;
            byte expected = context.Gen.constructedSupport;

            int checkedCells = 0;
            for (int i = 0; i < context.Grid.Floor.Length; i++)
            {
                if (!context.IsClaimed(i) || context.Grid.Floor[i] == 0) continue;
                Assert.That(context.Grid.Support[i], Is.EqualTo(expected));
                checkedCells++;
            }
            Assert.That(checkedCells, Is.GreaterThan(0));
        }

        // ---------------------------------------------------------------- pass 4, damage

        [Test]
        public void TheDamagePassChangesCellsAndIsReproducible()
        {
            var undamaged = Generate(2024, SliceSize, throughPass: 3);
            var damagedOnce = Generate(2024, SliceSize, throughPass: 4);
            var damagedTwice = Generate(2024, SliceSize, throughPass: 4);

            Assert.That(damagedOnce.Report.DamagedCells, Is.GreaterThan(0), "the damage pass did nothing");
            Assert.That(damagedOnce.Report.RemovedEdifices, Is.GreaterThan(0));
            Assert.That(damagedOnce.Report.SlabHoles, Is.GreaterThan(0));
            Assert.That(damagedOnce.GridHash, Is.Not.EqualTo(undamaged.GridHash),
                "damage left the grid byte-identical");
            Assert.That(damagedTwice.GridHash, Is.EqualTo(damagedOnce.GridHash),
                "the damage pass is not reproducible");
        }

        [Test]
        public void DamageVariesBetweenShellsOfTheSameTemplate()
        {
            // The whole justification for the damage pass is that it makes one template into many
            // different ruins. If every shell took identical damage the pass would be pointless.
            var result = Generate(31, SliceSize, throughPass: 4);
            var context = result.Context;

            var signatures = new HashSet<int>();
            int sampled = 0;
            foreach (var shell in context.Shells)
            {
                var template = context.Templates[shell.TemplateIndex];
                if (template.Id != "Shell_TerraceSmall") continue;

                int missing = 0;
                for (int layer = template.BottomLayer; layer <= template.TopLayer; layer++)
                for (int tz = 0; tz < template.SizeZ; tz++)
                for (int tx = 0; tx < template.SizeX; tx++)
                {
                    var kind = template.Cell(layer, tx, tz);
                    if (!ShellTemplate.Blocks(kind)) continue;
                    int index = context.Index(shell.X0 + tx, shell.Z0 + tz, context.GroundLayer + layer);
                    if (context.Grid.Edifice[index] < 0) missing++;
                }
                signatures.Add(missing);
                sampled++;
            }

            Assert.That(sampled, Is.GreaterThan(4), "not enough terraces to judge variety");
            Assert.That(signatures.Count, Is.GreaterThan(1), "every shell took identical damage");
        }

        // ---------------------------------------------------------------- pass 5, intactness

        [Test]
        public void TheIntactnessGridCoversTheStreetLayerWithSurfaceMaterials()
        {
            var result = Generate(19, SliceSize, throughPass: 5);
            var context = result.Context;
            int y = context.GroundLayer;

            var counts = new int[CoreContent.TerrainCount];
            for (int z = 0; z < context.Size.SizeZ; z++)
            for (int x = 0; x < context.Size.SizeX; x++)
            {
                int index = context.Index(x, z, y);
                if (context.IsClaimed(index)) continue;
                counts[context.Grid.Terrain[index]]++;
                Assert.That(context.Intactness[context.Column(x, z)], Is.InRange(0, ValueNoise.Scale - 1));
            }

            int surfaced = counts[CoreContent.TerrainPavement] + counts[CoreContent.TerrainCrackedPavement] +
                           counts[CoreContent.TerrainRubble] + counts[CoreContent.TerrainSoil];
            Assert.That(counts[CoreContent.TerrainAir], Is.Zero, "unsurfaced cells at street level");
            Assert.That(surfaced, Is.GreaterThan(0));

            int distinct = 0;
            for (int i = 0; i < counts.Length; i++) if (counts[i] > 0) distinct++;
            Assert.That(distinct, Is.GreaterThan(1), "the intactness field produced one material everywhere");
        }

        [Test]
        public void StreetsAreBiasedTowardIntactPavement()
        {
            var result = Generate(23, SliceSize, throughPass: 5);
            var context = result.Context;

            long streetTotal = 0, streetCount = 0, otherTotal = 0, otherCount = 0;
            for (int column = 0; column < context.Columns; column++)
            {
                if (context.IsStreet[column]) { streetTotal += context.Intactness[column]; streetCount++; }
                else { otherTotal += context.Intactness[column]; otherCount++; }
            }

            Assert.That(streetCount, Is.GreaterThan(0));
            Assert.That(otherCount, Is.GreaterThan(0));
            Assert.That(streetTotal / streetCount, Is.GreaterThan(otherTotal / otherCount));
        }

        // ---------------------------------------------------------------- pass 6, strata

        [Test]
        public void StrataAppearAtTheRightLayers()
        {
            var result = Generate(101, DeepSize);
            var context = result.Context;
            int ground = context.GroundLayer;
            Assert.That(ground, Is.GreaterThanOrEqualTo(10), "the deep fixture is not deep enough to test bands");

            // Depth 1: the service stratum is engineered fill, cut by tunnels.
            AssertLayerOnly(context, ground - 1, "service", new[]
            {
                CoreContent.TerrainFill, CoreContent.TerrainAir, CoreContent.TerrainSalvage,
            });

            // Depth 2 and 3: deep infrastructure, the metro and the buried-city seam.
            for (int depth = 2; depth <= 3; depth++)
                AssertLayerOnly(context, ground - depth, "deep infrastructure", new[]
                {
                    CoreContent.TerrainFill, CoreContent.TerrainBuriedSeam, CoreContent.TerrainAir,
                    CoreContent.TerrainSalvage,
                });

            // Depth 7 is always past the fill/soil boundary (max 6) and never past the rock
            // boundary (min 7), so it must be the soil band.
            AssertLayerOnly(context, ground - 7, "soil band", new[]
            {
                CoreContent.TerrainSoil, CoreContent.TerrainGravel, CoreContent.TerrainAir,
                CoreContent.TerrainSalvage,
            });

            // Below the deepest possible rock boundary there is nothing but rock and caves.
            AssertLayerOnly(context, ground - context.Gen.maxRockDepth - 1, "rock", new[]
            {
                CoreContent.TerrainRock, CoreContent.TerrainAir, CoreContent.TerrainSalvage,
            });

            Assert.That(result.Report.SeamCells, Is.GreaterThan(0), "no buried-city seam");
            Assert.That(result.Report.TunnelCells, Is.GreaterThan(0), "no service tunnels");
            Assert.That(result.Report.MetroCells, Is.GreaterThan(0), "no metro");
            Assert.That(result.Report.SolidCells, Is.GreaterThan(0));
        }

        static void AssertLayerOnly(WorldGenContext context, int y, string band, ushort[] allowed)
        {
            Assert.That(y, Is.InRange(0, context.Size.SizeY - 1), $"{band} layer is off the map");
            for (int i = y * context.Size.LayerStride; i < (y + 1) * context.Size.LayerStride; i++)
            {
                if (context.IsClaimed(i)) continue;
                ushort terrain = context.Grid.Terrain[i];
                bool ok = false;
                for (int a = 0; a < allowed.Length; a++) if (allowed[a] == terrain) ok = true;
                Assert.That(ok, Is.True,
                    $"{band} layer {y} has {CoreContent.TerrainAt(terrain).defName} at {context.Size.FromIndex(i)}");
            }
        }

        [Test]
        public void AboveGroundIsAirExceptWhereAShellStands()
        {
            var result = Generate(102, SliceSize);
            var context = result.Context;

            for (int y = context.GroundLayer + 1; y < context.Size.SizeY; y++)
            for (int i = y * context.Size.LayerStride; i < (y + 1) * context.Size.LayerStride; i++)
            {
                if (context.IsClaimed(i)) continue;
                Assert.That(context.Grid.Terrain[i], Is.EqualTo(CoreContent.TerrainAir));
                Assert.That(context.Grid.Floor[i], Is.EqualTo(CoreContent.SlabNone));
            }
        }

        [Test]
        public void StrataDoNotOverwriteABasement()
        {
            // A template carries its own vertical extent, so a basement claims a cell in the
            // service stratum. Pass 6 runs afterwards and must leave it alone.
            var result = Generate(55, SliceSize);
            var context = result.Context;

            int basements = 0;
            foreach (var shell in context.Shells)
            {
                var template = context.Templates[shell.TemplateIndex];
                if (template.BottomLayer >= 0) continue;
                for (int tz = 0; tz < template.SizeZ; tz++)
                for (int tx = 0; tx < template.SizeX; tx++)
                {
                    if (template.Cell(template.BottomLayer, tx, tz) != ShellCellKind.Open) continue;
                    int index = context.Index(shell.X0 + tx, shell.Z0 + tz,
                                              context.GroundLayer + template.BottomLayer);
                    Assert.That(context.Grid.Terrain[index], Is.EqualTo(CoreContent.TerrainAir),
                        "a basement was buried by the strata pass");
                    basements++;
                }
            }
            Assert.That(basements, Is.GreaterThan(0), "no basement was stamped, so nothing was tested");
        }

        // ---------------------------------------------------------------- passes 7 to 9

        [Test]
        public void SalvageIsScatteredAndWeightedTowardTheBuriedSeam()
        {
            var result = Generate(303, DeepSize);
            var context = result.Context;
            Assert.That(result.Report.SalvageDeposits, Is.GreaterThan(0));
            Assert.That(result.Report.SalvageCells, Is.GreaterThanOrEqualTo(result.Report.SalvageDeposits));

            int ground = context.GroundLayer;
            int inSeam = 0;
            foreach (var deposit in result.SalvageDeposits)
            {
                var cell = context.Size.FromIndex(deposit.CellIndex);
                int depth = ground - cell.Y;
                if (depth == 2 || depth == 3) inSeam++;
            }
            Assert.That(inSeam * 2, Is.GreaterThan(result.SalvageDeposits.Count / 2),
                "salvage is not weighted toward the buried-city seam");
        }

        [Test]
        public void UtilityTapsArePlacedAndSpacedApart()
        {
            var result = Generate(404, SliceSize);
            var context = result.Context;
            Assert.That(result.Report.UtilityTaps, Is.GreaterThanOrEqualTo(context.Gen.minUtilityTaps));

            int spacing = context.Gen.utilityTapSpacing;
            for (int i = 0; i < result.UtilityTaps.Count; i++)
            for (int j = i + 1; j < result.UtilityTaps.Count; j++)
            {
                var a = context.Size.FromIndex(result.UtilityTaps[i].CellIndex);
                var b = context.Size.FromIndex(result.UtilityTaps[j].CellIndex);
                int dx = a.X - b.X, dz = a.Z - b.Z;
                Assert.That(dx * dx + dz * dz, Is.GreaterThanOrEqualTo(spacing * spacing),
                    $"taps {a} and {b} are closer than the minimum spacing");
            }
        }

        [Test]
        public void SealedVaultsAreGeneratedUndergroundAndHaveNoDoor()
        {
            var result = Generate(505, DeepSize);
            var context = result.Context;
            Assert.That(result.Report.SealedVaults, Is.GreaterThanOrEqualTo(context.Gen.minVaults));

            foreach (var vault in result.SealedVaults)
            {
                Assert.That(vault.Layer, Is.LessThan(context.GroundLayer), "a vault surfaced");
                for (int z = vault.Z0; z < vault.Z0 + vault.SizeZ; z++)
                for (int x = vault.X0; x < vault.X0 + vault.SizeX; x++)
                {
                    int index = context.Index(x, z, vault.Layer);
                    bool perimeter = x == vault.X0 || z == vault.Z0 ||
                                     x == vault.X0 + vault.SizeX - 1 || z == vault.Z0 + vault.SizeZ - 1;
                    int handle = context.Grid.Edifice[index];
                    if (perimeter)
                    {
                        Assert.That(handle, Is.GreaterThanOrEqualTo(0), "a vault wall is missing");
                        Assert.That(context.Edifices[handle].Def, Is.EqualTo(CoreContent.EdificeVaultWall));
                    }
                    else
                    {
                        Assert.That(context.Grid.Floor[index], Is.Not.EqualTo(CoreContent.SlabNone));
                    }
                }
            }
        }

        // ---------------------------------------------------------------- pass 10, start

        [Test]
        public void TheStartCellIsAWalkableStreetCellAtStreetLevel()
        {
            for (uint seed = 1; seed <= 5; seed++)
            {
                var result = Generate(seed);
                var context = result.Context;
                var start = result.StartCell;

                Assert.That(start.Y, Is.EqualTo(context.GroundLayer));
                Assert.That(context.IsStreet[context.Column(start.X, start.Z)], Is.True);
                Assert.That(context.Grid.IsWalkable(context.Index(start.X, start.Z, start.Y)), Is.True);
            }
        }

        [Test]
        public void TheStructuralHookIsCalledWhenOneIsSupplied()
        {
            // A supplied check replaces the default one. The probe records that pass 10 reaches
            // it exactly once, which is what keeps the hook itself testable now that the real
            // check does the work.
            var probe = new RecordingStructuralCheck();
            var grid = new CellGrid(SliceSize);
            var result = WorldGenerator.Generate(grid, 9, MapGenDef.For(SliceSize),
                TemplateLibrary.Slice(), WorldGenerator.PassCount, probe);

            Assert.That(probe.Calls, Is.EqualTo(1));
            Assert.That(result.Report.StructuralCheckRan, Is.True);
        }

        sealed class RecordingStructuralCheck : IStructuralConsistencyCheck
        {
            public int Calls;
            public void Verify(CellGrid grid, WorldGenContext context) => Calls++;
        }

        [Test]
        public void TheFullSupportSolveIsTheDefaultCheck()
        {
            // Passing null is not "skip the check" — it is "use the real one". Every worldgen
            // test in this file therefore proves its map stands up, whether it meant to or not.
            var grid = new CellGrid(SliceSize);
            var result = WorldGenerator.Generate(grid, 9, MapGenDef.For(SliceSize),
                TemplateLibrary.Slice(), WorldGenerator.PassCount, null);

            Assert.That(result.Report.StructuralCheckRan, Is.True);
        }

        [Test]
        public void EveryShippedTemplateStandsAtEveryDamageSetting()
        {
            // The guarantee from 02-world-and-layers.md section 4: a stamped shell must be
            // *initially* consistent, judged by the ordinary rule with every construction-trust
            // mark revoked. A template that cannot hold itself up is a content bug, and this is
            // where it is caught — not on tick one.
            //
            // The undamaged map is the template's own exam, so shedding is off and any collapse
            // throws. A damaged map is allowed to shed, because the damage pass removes walls
            // that were holding slabs up and a ruin has already dropped what those walls carried;
            // the assertion there is that it settles, and that what is left stands on its own.
            foreach (var damage in DamageSettings)
            {
                for (uint seed = 1; seed <= 10; seed++)
                {
                    var grid = new CellGrid(SliceSize);
                    var gen = damage.Apply(MapGenDef.For(SliceSize));
                    var check = new SupportConsistencyCheck(allowShedding: damage.Damages);

                    // Pass 10's check is the assertion: reaching the next line means no throw.
                    var result = WorldGenerator.Generate(grid, seed, gen, TemplateLibrary.Slice(),
                        WorldGenerator.PassCount, check);
                    var report = result.Report;

                    Assert.That(report.StructuralCheckRan, Is.True,
                        $"{damage.Name}, seed {seed}: the structural check did not run");
                    Assert.That(report.SettleRounds, Is.LessThanOrEqualTo(2),
                        $"{damage.Name}, seed {seed}: settling took {report.SettleRounds} solves");

                    if (!damage.Damages)
                        Assert.That(report.SettledSlabs, Is.Zero,
                            $"{damage.Name}, seed {seed}: an undamaged map shed a slab");

                    // Whatever survived stands on the ordinary rule with no construction trust at
                    // all, which is the property the first tick depends on.
                    var audit = new SupportSolver(grid);
                    audit.ClearAllConstructionMarks();
                    Assert.That(audit.SolveFull(), Is.Empty,
                        $"{damage.Name}, seed {seed}: the settled map still has slabs that cannot stand");
                }
            }
        }

        /// <summary>
        /// Every damage setting <see cref="MapGenDef"/> exposes, as the extremes of its band.
        /// Damage off is the pure template — a failure there is the template's own fault. Damage
        /// at full is the ruin the generator actually ships.
        /// </summary>
        static readonly DamageSetting[] DamageSettings =
        {
            new DamageSetting("damage off", false, gen =>
            {
                gen.minDamageIntensity = 0;
                gen.maxDamageIntensity = 0;
                gen.toppleChance = 0;
            }),
            new DamageSetting("default damage", true, _ => { }),
            new DamageSetting("damage at full", true, gen =>
            {
                gen.minDamageIntensity = 1000;
                gen.maxDamageIntensity = 1000;
                gen.toppleChance = 1000;
            }),
        };

        sealed class DamageSetting
        {
            readonly System.Action<MapGenDef> _configure;

            public DamageSetting(string name, bool damages, System.Action<MapGenDef> configure)
            {
                Name = name;
                Damages = damages;
                _configure = configure;
            }

            public string Name { get; }

            /// <summary>Whether this setting can remove a wall, and so orphan a slab.</summary>
            public bool Damages { get; }

            public MapGenDef Apply(MapGenDef gen)
            {
                _configure(gen);
                return gen;
            }

            public override string ToString() => Name;
        }

        [Test]
        public void EveryPassRunsAndTheConsistencyAssertionPasses()
        {
            for (uint seed = 1; seed <= 5; seed++)
            {
                // Pass 10 throws if the map is internally inconsistent, so reaching here is the
                // assertion. The report's pass count proves nothing was quietly skipped.
                var result = Generate(seed);
                Assert.That(result.Report.PassesRun, Is.EqualTo(WorldGenerator.PassCount));
            }
        }

        // ---------------------------------------------------------------- U13, templates

        [Test]
        public void TheSliceTemplatesCompileAndAreAuthoredInCells()
        {
            var set = TemplateLibrary.Slice();
            Assert.That(set.Count, Is.EqualTo(3));

            foreach (var id in new[] { "Shell_BlockMedium", "Shell_TerraceSmall", "Shell_TowerTall" })
                Assert.That(set.IndexOf(id), Is.GreaterThanOrEqualTo(0), $"{id} is missing");

            var block = set[set.IndexOf("Shell_BlockMedium")];
            Assert.That(block.SizeX, Is.EqualTo(10));
            Assert.That(block.SizeZ, Is.EqualTo(8));
            Assert.That(block.BottomLayer, Is.EqualTo(-1), "the block carries its own basement");
            Assert.That(block.TopLayer, Is.EqualTo(2));
            Assert.That(block.HighestLayer, Is.EqualTo(3), "the roof is one layer above the top storey");
            Assert.That(block.Cell(-1, 0, 0), Is.EqualTo(ShellCellKind.Wall));
            Assert.That(block.Cell(0, 4, 0), Is.EqualTo(ShellCellKind.Door));
        }

        [Test]
        public void TemplatesReferenceModulesByIdOnly()
        {
            // A clone without the licensed Synty packs must still load every template, so nothing
            // here may be an asset path, a GUID or anything the packs own.
            foreach (var def in TemplateLibrary.SliceDefs())
            {
                var template = ShellTemplate.Compile(def);
                foreach (var kind in new[]
                {
                    ShellCellKind.Wall, ShellCellKind.Door, ShellCellKind.Window,
                    ShellCellKind.Pillar, ShellCellKind.StairLower, ShellCellKind.Ladder,
                    ShellCellKind.Open,
                })
                {
                    string module = template.ModuleFor(kind);
                    Assert.That(module, Does.StartWith("odyssey."), $"{template.Id}/{kind} is not an id");
                    Assert.That(module, Does.Not.Contain("Assets/"));
                    Assert.That(module, Does.Not.Contain("Synty"));
                }
            }
        }

        [Test]
        public void TheVerticalExtentDecidesWhereATemplateMayStand()
        {
            var set = TemplateLibrary.Slice();
            var tower = set[set.IndexOf("Shell_TowerTall")];
            var terrace = set[set.IndexOf("Shell_TerraceSmall")];

            // The slice map has one layer below street level and three above, so the tower can
            // never fit however large the plot is.
            Assert.That(tower.Fits(40, 40, 1, 3), Is.False);
            Assert.That(tower.Fits(12, 12, 1, 6), Is.True);
            Assert.That(terrace.Fits(6, 6, 0, 2), Is.True);
            Assert.That(terrace.Fits(5, 6, 0, 2), Is.False, "a template must fit its plot footprint");
        }

        [Test]
        public void ASliceMapNeverStampsATower()
        {
            var result = Generate(606, SliceSize);
            var context = result.Context;
            int towerIndex = context.Templates.IndexOf("Shell_TowerTall");
            foreach (var shell in context.Shells)
                Assert.That(shell.TemplateIndex, Is.Not.EqualTo(towerIndex));
        }

        [Test]
        public void ADeepMapDoesStampTowers()
        {
            // A tower needs both a twelve-cell-square plot and the layers to stand in, so it is
            // deliberately rare; the test therefore counts across several maps rather than
            // insisting that any one seed produces one.
            var size = new GridSize(120, 120, 36);
            int towers = 0;
            for (uint seed = 601; seed <= 608; seed++)
            {
                var result = Generate(seed, size);
                var context = result.Context;
                int towerIndex = context.Templates.IndexOf("Shell_TowerTall");
                foreach (var shell in context.Shells) if (shell.TemplateIndex == towerIndex) towers++;
            }
            Assert.That(towers, Is.GreaterThan(0));
        }

        [Test]
        public void ARowCountMismatchIsAContentError()
        {
            var def = TemplateLibrary.TerraceSmall();
            def.rows.RemoveAt(def.rows.Count - 1);
            var ex = Assert.Throws<DefLoadException>(() => ShellTemplate.Compile(def));
            Assert.That(ex!.Message, Does.Contain("Shell_TerraceSmall"));
        }

        [Test]
        public void ARowOfTheWrongWidthIsAContentError()
        {
            var def = TemplateLibrary.TerraceSmall();
            def.rows[1] = "#...#";
            Assert.Throws<DefLoadException>(() => ShellTemplate.Compile(def));
        }

        [Test]
        public void AnUnpairedStairIsAContentError()
        {
            var def = TemplateLibrary.BlockMedium();
            def.rows[3] = "#....>...#";   // the lower half of the basement stair removed
            Assert.Throws<DefLoadException>(() => ShellTemplate.Compile(def));
        }

        [Test]
        public void AConnectorWithNoHeadroomIsAContentError()
        {
            var def = TemplateLibrary.TerraceSmall();
            def.rows[8] = "#..#.#";       // wall directly above the ladder on layer 1
            Assert.Throws<DefLoadException>(() => ShellTemplate.Compile(def));
        }

        [Test]
        public void AnUnknownTemplateCharacterIsAContentError()
        {
            var def = TemplateLibrary.TerraceSmall();
            def.rows[1] = "#..?.#";
            Assert.Throws<DefLoadException>(() => ShellTemplate.Compile(def));
        }

        [Test]
        public void TheTemplateSetOrderDoesNotDependOnLoadOrder()
        {
            var forward = TemplateSet.FromDefs(TemplateLibrary.SliceDefs());
            var reversed = TemplateLibrary.SliceDefs();
            reversed.Reverse();
            var backward = TemplateSet.FromDefs(reversed);

            for (int i = 0; i < forward.Count; i++)
                Assert.That(backward[i].Id, Is.EqualTo(forward[i].Id));
        }

        // ---------------------------------------------------------------- the noise field

        [Test]
        public void ValueNoiseStaysInRangeAndRepeats()
        {
            for (int z = 0; z < 40; z++)
            for (int x = 0; x < 40; x++)
            {
                int a = ValueNoise.Fractal2D(7, x, z, 16, 3);
                int b = ValueNoise.Fractal2D(7, x, z, 16, 3);
                Assert.That(a, Is.InRange(0, ValueNoise.Scale - 1));
                Assert.That(b, Is.EqualTo(a));
                Assert.That(ValueNoise.Value3D(7, x, z, 3, 12), Is.InRange(0, ValueNoise.Scale - 1));
            }
        }

        [Test]
        public void ValueNoiseIsContinuousAcrossLatticeBoundaries()
        {
            // Value noise that jumps at a lattice boundary produces visible grid artefacts in the
            // intactness field, which would be the sort of bug one only ever sees in a screenshot.
            int worst = 0;
            for (int x = 0; x < 200; x++)
            {
                int here = ValueNoise.Value2D(99, x, 13, 8);
                int next = ValueNoise.Value2D(99, x + 1, 13, 8);
                int step = here > next ? here - next : next - here;
                if (step > worst) worst = step;
            }
            Assert.That(worst, Is.LessThan(ValueNoise.Scale / 3), $"noise jumped by {worst}");
        }

        [Test]
        public void ValueNoiseHandlesNegativeCoordinates()
        {
            Assert.That(ValueNoise.Value2D(3, -17, -4, 8), Is.InRange(0, ValueNoise.Scale - 1));
            Assert.That(ValueNoise.Value3D(3, -17, -4, -2, 8), Is.InRange(0, ValueNoise.Scale - 1));
        }

        [Test]
        public void BandMapsTheFullNoiseRangeOntoTheRequestedSpan()
        {
            var seen = new HashSet<int>();
            for (int n = 0; n < ValueNoise.Scale; n++)
            {
                int banded = ValueNoise.Band(n, 4, 6);
                Assert.That(banded, Is.InRange(4, 6));
                seen.Add(banded);
            }
            Assert.That(seen.Count, Is.EqualTo(3));
        }

        // ---------------------------------------------------------------- performance

        [Test]
        public void GeneratingTheSliceMapIsFast()
        {
            Generate(1);   // warm the JIT so the measurement is of the generator, not of startup

            var watch = Stopwatch.StartNew();
            var result = Generate(2);
            watch.Stop();

            TestContext.WriteLine($"slice {SliceSize}: {watch.ElapsedMilliseconds} ms — {result.Report}");
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(500));
        }

        [Test]
        public void GeneratingTheScaleTargetMapIsReasonable()
        {
            // 250 x 250 x 40, the scale target from ADR 0002. This is the test that keeps the
            // large path honest, per docs/design/02-world-and-layers.md section 7.
            //
            // The budget is the M1 one: 2,000 ms, ten times the 215 ms first measured, and now
            // inclusive of pass 10's full support solve over all 2.5M cells. The median of five
            // seeds rather than one run, because a single timing on a shared machine measures the
            // machine's mood as much as the generator.
            var size = GridSize.ScaleTarget;
            var times = new List<long>();
            WorldGenResult? last = null;

            for (uint seed = 4242; seed < 4247; seed++)
            {
                var grid = new CellGrid(size);
                var watch = Stopwatch.StartNew();
                last = WorldGenerator.Generate(grid, seed, MapGenDef.For(size));
                watch.Stop();
                times.Add(watch.ElapsedMilliseconds);

                Assert.That(last.Report.ShellsStamped, Is.GreaterThan(100));
                Assert.That(last.Report.PassesRun, Is.EqualTo(WorldGenerator.PassCount));
            }

            long median = Median(times);
            TestContext.WriteLine($"scale target {size}: median {median} ms of {Listed(times)} — {last!.Report}");
            TestContext.WriteLine($"  settled {last.Report.SettledSlabs} slab(s) in {last.Report.SettleRounds} solves");
            Assert.That(median, Is.LessThan(2_000), $"M1 generation budget: {Listed(times)}");
        }

        /// <summary>The middle of an odd-length sample, which is what the budget is judged on.</summary>
        internal static long Median(List<long> times)
        {
            var sorted = new List<long>(times);
            sorted.Sort();
            return sorted[sorted.Count / 2];
        }

        internal static string Listed(List<long> times) => $"[{string.Join(", ", times)}] ms";
    }
}
