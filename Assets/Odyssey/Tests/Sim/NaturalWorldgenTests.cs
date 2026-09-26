#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The wilderness generator: an empty, natural map the colony builds on from nothing.
    ///
    /// The determinism tests matter most, for the reason the city generator's do: a generator that
    /// is almost deterministic is worse than one that obviously is not, because the desync turns
    /// up a milestone later in a save nobody can reproduce. After those come the structural
    /// invariants — a continuous surface, walkable ground, trees only on grass, ore only in rock —
    /// which are the things every later system is entitled to assume.
    /// </summary>
    public class NaturalWorldgenTests
    {
        /// <summary>Small, but deep enough to carry the whole stratum stack and both ore bands.</summary>
        static readonly GridSize SmallSize = new GridSize(48, 48, 24);

        static NaturalMapResult Generate(uint seed, GridSize? size = null, int throughPass = NaturalMapGenerator.PassCount)
        {
            var actual = size ?? SmallSize;
            var grid = new CellGrid(actual);
            var gen = NaturalMapGenDef.For(actual);
            return NaturalMapGenerator.Generate(grid, seed, gen, throughPass);
        }

        // ---------------------------------------------------------------- determinism

        [Test]
        public void SameSeedProducesAnIdenticalMap()
        {
            var a = Generate(12345);
            var b = Generate(12345);

            Assert.That(b.GridHash, Is.EqualTo(a.GridHash), "same seed, different grid hash");
            Assert.That(b.Report.Trees, Is.EqualTo(a.Report.Trees));
            Assert.That(b.Report.OreCells, Is.EqualTo(a.Report.OreCells));
            Assert.That(b.Report.OutcropCells, Is.EqualTo(a.Report.OutcropCells));
            Assert.That(b.StartCell, Is.EqualTo(a.StartCell));
        }

        [Test]
        public void DifferentSeedsProduceDifferentMaps()
        {
            var hashes = new HashSet<ulong>();
            for (uint seed = 1; seed <= 6; seed++) hashes.Add(Generate(seed).GridHash);

            Assert.That(hashes.Count, Is.EqualTo(6), "two seeds collided, which means a seed is being ignored");
        }

        [Test]
        public void GenerationCarriesNoStateBetweenRuns()
        {
            // Generating something else in between must not shift a later map by one draw. If any
            // pass ever reaches for a shared or static random stream, this is what catches it.
            var first = Generate(777);
            Generate(999, new GridSize(32, 32, 18));
            Generate(31337);
            var again = Generate(777);

            Assert.That(again.GridHash, Is.EqualTo(first.GridHash));
            Assert.That(again.StartCell, Is.EqualTo(first.StartCell));
        }

        [Test]
        public void TheSameSeedAtADifferentSizeIsADifferentMap()
        {
            var small = Generate(4242, new GridSize(32, 32, 18));
            var large = Generate(4242);

            Assert.That(large.GridHash, Is.Not.EqualTo(small.GridHash));
        }

        // ---------------------------------------------------------------- heightfield and strata

        [Test]
        public void TheSurfaceRollsRatherThanSittingFlat()
        {
            var result = Generate(8);

            // Terracing, not hills: a few layers of variation across the map, not one plane and
            // not a cliff face.
            Assert.That(result.Report.SurfaceSpread, Is.GreaterThanOrEqualTo(3),
                "the surface is flat, so the heightfield is doing nothing");
            Assert.That(result.Report.SurfaceSpread,
                Is.LessThanOrEqualTo(2 * result.Context.Gen.surfaceRelief + 1),
                "the surface varies by more than the relief parameter allows");
        }

        [Test]
        public void TheSurfaceIsContinuousWithNoHolesAndNothingFloating()
        {
            var result = Generate(31);
            var ctx = result.Context;
            var grid = ctx.Grid;

            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int column = ctx.Column(x, z);
                int top = ctx.TopSolidY[column];

                for (int y = 0; y < ctx.Size.SizeY; y++)
                {
                    int index = ctx.Index(x, z, y);
                    bool solid = grid.IsSolidTerrain(index);
                    if (y > top)
                    {
                        Assert.That(solid, Is.False, $"floating solid at {x},{z},{y}: something above the surface");
                    }
                    else if (ctx.IsCavern(index))
                    {
                        // A cavern is the one hole the column rule allows, and it is allowed by
                        // name: this cell is one the cavern pass recorded carving, and it has to
                        // sit strictly inside the column's rock band or it would undermine the
                        // surface or crack into the bedrock.
                        Assert.That(solid, Is.False, $"the cavern at {x},{z},{y} was not actually hollowed out");
                        Assert.That(y, Is.InRange(ctx.BedrockTopY[column] + 1, ctx.SubsoilBaseY[column] - 2),
                            $"the cavern at {x},{z},{y} has left the rock band");
                    }
                    else
                    {
                        Assert.That(solid, Is.True, $"hole at {x},{z},{y}: ground below the surface is not solid");
                    }
                }
            }
        }

        [Test]
        public void TheStrataStackFromBedrockUpToSoil()
        {
            var result = Generate(55, throughPass: 3);
            var ctx = result.Context;
            var grid = ctx.Grid;

            int bedrock = 0, rock = 0, subsoil = 0;
            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int column = ctx.Column(x, z);
                int surface = ctx.SurfaceY[column];

                Assert.That(grid.Terrain[ctx.Index(x, z, surface)],
                    Is.EqualTo(NaturalContent.TerrainGrass), "the surface cell is not soil");

                for (int y = 0; y < surface; y++)
                {
                    ushort terrain = grid.Terrain[ctx.Index(x, z, y)];
                    if (y >= ctx.SubsoilBaseY[column])
                    {
                        Assert.That(terrain, Is.EqualTo(NaturalContent.TerrainSubsoil));
                        subsoil++;
                    }
                    else if (y < ctx.BedrockTopY[column])
                    {
                        Assert.That(terrain, Is.EqualTo(NaturalContent.TerrainBedrock));
                        bedrock++;
                    }
                    else if (y < ctx.DeepStoneTopY[column])
                    {
                        // Deep stone, from deepStoneDepth below the surface (design 62 §5b).
                        Assert.That(terrain, Is.EqualTo(NaturalContent.TerrainDeepStone));
                        Assert.That(surface - y, Is.GreaterThanOrEqualTo(ctx.Gen.deepStoneDepth));
                    }
                    else
                    {
                        Assert.That(terrain, Is.EqualTo(NaturalContent.TerrainRock));
                        rock++;
                    }
                }
            }

            Assert.That(bedrock, Is.GreaterThan(0), "no bedrock at the bottom of the world");
            Assert.That(rock, Is.GreaterThan(0), "no rock stratum");
            Assert.That(subsoil, Is.GreaterThan(0), "no subsoil band");
        }

        [Test]
        public void EverySurfaceCellIsWalkableUnlessItIsOutOfOnesDepth()
        {
            var result = Generate(99);
            var ctx = result.Context;

            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int column = ctx.Column(x, z);
                int stand = ctx.TopSolidY[column] + 1;
                Assert.That(stand, Is.LessThan(ctx.Size.SizeY), $"column {x},{z} has no air above it");

                // Deep water is the single exception, and it is the point of it: everything else
                // the generator makes can be stood on. Shallow water still has to be walkable —
                // wading is slow, not impossible — so this is a narrow exemption, not a licence.
                if (ctx.Water[column] == (byte)WaterClass.Deep)
                {
                    Assert.That(ctx.Grid.IsWalkable(ctx.Index(x, z, stand)), Is.False,
                        $"deep water at {x},{z} can be walked on");
                    continue;
                }

                Assert.That(ctx.Grid.IsWalkable(ctx.Index(x, z, stand)), Is.True,
                    $"the cell above the ground at {x},{z} is not walkable");
            }
        }

        [Test]
        public void NeighbouringSurfaceCellsAreNeverMoreThanOneLayerApart()
        {
            // The cell model has no slopes, so a two-layer step is a wall a colonist cannot climb.
            var result = Generate(1234);
            var ctx = result.Context;

            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int here = ctx.SurfaceY[ctx.Column(x, z)];
                if (x + 1 < ctx.Size.SizeX)
                    Assert.That(Math.Abs(ctx.SurfaceY[ctx.Column(x + 1, z)] - here), Is.LessThanOrEqualTo(1),
                        $"a cliff between {x},{z} and {x + 1},{z}");
                if (z + 1 < ctx.Size.SizeZ)
                    Assert.That(Math.Abs(ctx.SurfaceY[ctx.Column(x, z + 1)] - here), Is.LessThanOrEqualTo(1),
                        $"a cliff between {x},{z} and {x},{z + 1}");
            }
        }

        [Test]
        public void ShallowMapsCompressTheStackRatherThanFailing()
        {
            // The city slice is 60 x 60 x 5, and three layers is the documented minimum. Neither
            // has room for the whole stratum stack; both must still produce a playable map.
            foreach (var size in new[] { new GridSize(60, 60, 5), new GridSize(20, 20, 3) })
            {
                var grid = new CellGrid(size);
                var result = NaturalMapGenerator.Generate(grid, 7, NaturalMapGenDef.For(size));
                var ctx = result.Context;
                var start = result.StartCell;

                Assert.That(grid.IsWalkable(ctx.Index(start.X, start.Z, start.Y)), Is.True,
                    $"{size}: the start cell is not walkable");

                for (int z = 0; z < size.SizeZ; z++)
                for (int x = 0; x < size.SizeX; x++)
                {
                    int column = ctx.Column(x, z);
                    Assert.That(ctx.SurfaceY[column], Is.LessThanOrEqualTo(size.SizeY - 2),
                        $"{size}: no headroom above the ground at {x},{z}");
                    Assert.That(grid.IsSolidTerrain(ctx.Index(x, z, 0)), Is.True,
                        $"{size}: the bottom of the world at {x},{z} is not solid");
                }
            }
        }

        // ---------------------------------------------------------------- surface cover

        [Test]
        public void GrassCoversMostOfTheMapWithPatchesForVariety()
        {
            var result = Generate(21);
            var report = result.Report;
            int patches = report.BareEarthCells + report.GravelCells + report.SandCells;

            Assert.That(report.GrassCells, Is.GreaterThan(result.Context.Columns / 2), "grass is not the norm");
            Assert.That(patches, Is.GreaterThan(0), "no bare patches at all, so the cover pass is inert");
            Assert.That(report.BareEarthCells, Is.GreaterThan(0));
            Assert.That(report.GravelCells + report.SandCells, Is.GreaterThan(0));
        }

        [Test]
        public void EverySurfaceCellIsOneOfTheGroundKinds()
        {
            var result = Generate(22);
            var ctx = result.Context;

            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int column = ctx.Column(x, z);
                ushort terrain = ctx.Grid.Terrain[ctx.Index(x, z, ctx.SurfaceY[column])];
                bool ground = NaturalContent.IsGround(terrain);
                bool outcrop = ctx.TopSolidY[column] != ctx.SurfaceY[column] &&
                               terrain == NaturalContent.TerrainRock;
                Assert.That(ground || outcrop, Is.True,
                    $"the surface at {x},{z} is terrain {terrain}, neither ground nor an outcrop");
            }
        }

        // ---------------------------------------------------------------- trees

        [Test]
        public void TreesOnlyEverSitOnGrass()
        {
            var result = Generate(64);
            var ctx = result.Context;

            Assert.That(result.Trees.Count, Is.GreaterThan(0), "no trees at all");

            foreach (var tree in result.Trees)
            {
                var cell = ctx.Size.FromIndex(tree.CellIndex);
                int below = tree.CellIndex - ctx.Size.LayerStride;

                Assert.That(ctx.Grid.Terrain[below], Is.EqualTo(NaturalContent.TerrainGrass),
                    $"a tree at {cell} is not rooted in grass");
                Assert.That(ctx.Grid.IsSolidTerrain(tree.CellIndex), Is.False, $"a tree at {cell} is inside solid ground");
                Assert.That(NaturalContent.IsTree(tree.Def), Is.True);
                Assert.That(ctx.Grid.Edifice[tree.CellIndex], Is.GreaterThanOrEqualTo(0));
            }
        }

        [Test]
        public void TreesDoNotBlockMovement()
        {
            var result = Generate(65);
            var ctx = result.Context;

            foreach (var tree in result.Trees)
                Assert.That(ctx.Grid.IsWalkable(tree.CellIndex), Is.True,
                    $"a tree at {ctx.Size.FromIndex(tree.CellIndex)} makes its cell unwalkable");
        }

        [Test]
        public void TreesFormCopsesAndClearings()
        {
            var result = Generate(66);
            var ctx = result.Context;

            // Count per 8 x 8 block. Uniform noise would give every block roughly the mean; a
            // clumping bias gives empty blocks and crowded ones.
            const int Block = 8;
            int blocksX = ctx.Size.SizeX / Block, blocksZ = ctx.Size.SizeZ / Block;
            var counts = new int[blocksX * blocksZ];

            foreach (var tree in result.Trees)
            {
                var cell = ctx.Size.FromIndex(tree.CellIndex);
                int bx = cell.X / Block, bz = cell.Z / Block;
                if (bx < blocksX && bz < blocksZ) counts[bz * blocksX + bx]++;
            }

            int total = 0, empty = 0, max = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                total += counts[i];
                if (counts[i] == 0) empty++;
                if (counts[i] > max) max = counts[i];
            }

            int mean = total / counts.Length;
            TestContext.WriteLine($"trees {result.Report.Trees}: mean per block {mean}, max {max}, empty blocks {empty}");
            Assert.That(mean, Is.GreaterThan(0), "no trees to speak of");
            Assert.That(max, Is.GreaterThan(mean * 2), "tree density is uniform, so nothing is clumping");
            Assert.That(empty, Is.GreaterThanOrEqualTo(3), "the map has no clearings worth the name");
        }

        [Test]
        public void TreeDensityFollowsItsParameter()
        {
            var sparse = GenerateWith(70, gen => gen.treeDensityPerMille = 40);
            var dense = GenerateWith(70, gen => gen.treeDensityPerMille = 600);

            Assert.That(dense.Report.Trees, Is.GreaterThan(sparse.Report.Trees * 3),
                "the density parameter barely moves the tree count");
        }

        [Test]
        public void TreesCanBeTurnedOff()
        {
            var result = GenerateWith(71, gen => gen.treeDensityPerMille = 0);
            Assert.That(result.Report.Trees, Is.Zero);
        }

        // ---------------------------------------------------------------- outcrops and ore

        [Test]
        public void OutcropsStandAboveTheGroundAndLeaveHeadroom()
        {
            var result = Generate(80);
            var ctx = result.Context;

            Assert.That(result.Outcrops.Count, Is.GreaterThan(0), "no outcrops on the map");
            Assert.That(result.Report.OutcropCells, Is.GreaterThan(0));

            int raised = 0;
            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int column = ctx.Column(x, z);
                if (ctx.TopSolidY[column] == ctx.SurfaceY[column]) continue;
                raised++;

                Assert.That(ctx.TopSolidY[column], Is.GreaterThan(ctx.SurfaceY[column]));
                Assert.That(ctx.TopSolidY[column], Is.LessThanOrEqualTo(ctx.Size.SizeY - 2),
                    "an outcrop reaches the top of the map and leaves nowhere to stand");

                for (int y = ctx.SurfaceY[column]; y <= ctx.TopSolidY[column]; y++)
                    Assert.That(ctx.Grid.Terrain[ctx.Index(x, z, y)], Is.EqualTo(NaturalContent.TerrainRock),
                        $"an outcrop column at {x},{z} is not stone all the way down");
            }

            Assert.That(raised, Is.GreaterThan(0));
        }

        [Test]
        public void OreOnlyEverSitsInsideRock()
        {
            var result = Generate(81);
            var ctx = result.Context;

            // Most of the attempted deposits should land, even on a map shallower than the
            // deepest ore band: the band is clamped into the rock rather than abandoned.
            int attempted = 0;
            foreach (var ore in NaturalContent.Ores)
                attempted += (ctx.Columns * ore.DepositsPer10000Columns + 5000) / 10000;
            Assert.That(result.OreDeposits.Count, Is.GreaterThan(attempted * 3 / 4),
                $"only {result.OreDeposits.Count} of {attempted} deposits landed");

            int found = 0;
            for (int z = 0; z < ctx.Size.SizeZ; z++)
            for (int x = 0; x < ctx.Size.SizeX; x++)
            {
                int column = ctx.Column(x, z);
                for (int y = 0; y < ctx.Size.SizeY; y++)
                {
                    int index = ctx.Index(x, z, y);
                    if (!NaturalContent.IsOre(ctx.Grid.Terrain[index])) continue;
                    found++;

                    Assert.That(ctx.Grid.IsSolidTerrain(index), Is.True);
                    Assert.That(y, Is.LessThan(ctx.SubsoilBaseY[column]),
                        $"ore at {x},{z},{y} is in the subsoil or the surface, not in rock");
                    Assert.That(y, Is.GreaterThanOrEqualTo(ctx.BedrockTopY[column]),
                        $"ore at {x},{z},{y} is inside the bedrock");
                }
            }

            Assert.That(found, Is.EqualTo(result.Report.OreCells));
        }

        [Test]
        public void BothOreKindsAppearAndCoalSitsDeeperThanIron()
        {
            var size = new GridSize(96, 96, 32);
            var result = Generate(82, size);
            var ctx = result.Context;

            Assert.That(result.Report.OreCellsByKind[0], Is.GreaterThan(0), "no iron");
            Assert.That(result.Report.OreCellsByKind[1], Is.GreaterThan(0), "no coal");

            long ironDepth = 0, ironCount = 0, coalDepth = 0, coalCount = 0;
            foreach (var deposit in result.OreDeposits)
            {
                var cell = ctx.Size.FromIndex(deposit.CellIndex);
                int depth = ctx.SurfaceY[ctx.Column(cell.X, cell.Z)] - cell.Y;
                if (deposit.Kind == 0) { ironDepth += depth; ironCount++; }
                else if (deposit.Kind == 1) { coalDepth += depth; coalCount++; }
            }

            Assert.That(ironCount, Is.GreaterThan(0));
            Assert.That(coalCount, Is.GreaterThan(0));
            TestContext.WriteLine($"mean depth: iron {ironDepth / ironCount}, coal {coalDepth / coalCount}");
            Assert.That(coalDepth / coalCount, Is.GreaterThan(ironDepth / ironCount),
                "the depth weighting does not put coal below iron");
        }

        // ---------------------------------------------------------------- the start location

        [Test]
        public void TheStartIsWalkableFlatAndClearOfTrees()
        {
            for (uint seed = 1; seed <= 5; seed++)
            {
                var result = Generate(seed);
                var ctx = result.Context;
                var start = result.StartCell;
                int radius = ctx.Gen.startClearingRadius;

                Assert.That(ctx.Size.Contains(start), Is.True);
                Assert.That(ctx.Grid.IsWalkable(ctx.Index(start.X, start.Z, start.Y)), Is.True,
                    $"seed {seed}: the start cell is not walkable");

                int startColumn = ctx.Column(start.X, start.Z);
                Assert.That(start.Y, Is.EqualTo(ctx.SurfaceY[startColumn] + 1),
                    $"seed {seed}: the start is not standing on the ground");

                for (int dz = -radius; dz <= radius; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = start.X + dx, z = start.Z + dz;
                    if (!ctx.Size.Contains(x, z, start.Y)) continue;
                    int column = ctx.Column(x, z);

                    Assert.That(ctx.SurfaceY[column], Is.EqualTo(ctx.SurfaceY[startColumn]),
                        $"seed {seed}: the clearing at {x},{z} steps up or down");
                    Assert.That(ctx.TopSolidY[column], Is.EqualTo(ctx.SurfaceY[column]),
                        $"seed {seed}: an outcrop stands in the clearing at {x},{z}");
                    Assert.That(ctx.HasTree[column], Is.False, $"seed {seed}: a tree stands in the clearing at {x},{z}");
                    Assert.That(ctx.Grid.Edifice[ctx.Index(x, z, ctx.SurfaceY[column] + 1)], Is.EqualTo(-1),
                        $"seed {seed}: something stands in the clearing at {x},{z}");
                    Assert.That(NaturalContent.IsGround(ctx.Grid.Terrain[ctx.Index(x, z, ctx.SurfaceY[column])]),
                        Is.True, $"seed {seed}: the clearing at {x},{z} is not buildable ground");
                }
            }
        }

        [Test]
        public void FelledTreesLeaveTheStandingList()
        {
            // Whatever the start pass clears must vanish from the tree list and from the grid, or
            // a later system will try to chop a tree that is not there.
            for (uint seed = 1; seed <= 4; seed++)
            {
                var result = Generate(seed);
                var ctx = result.Context;
                foreach (var tree in result.Trees)
                    Assert.That(ctx.Grid.Edifice[tree.CellIndex], Is.GreaterThanOrEqualTo(0),
                        "a felled tree is still listed as standing");
                Assert.That(result.Report.Trees, Is.EqualTo(result.Trees.Count));
            }
        }

        // ---------------------------------------------------------------- map type selection

        [Test]
        public void TheDefaultMapTypeIsTheRuinedCitySoNothingExistingChanges()
        {
            Assert.That(MapGenerator.TypeOf(MapGenDef.Slice()), Is.EqualTo(MapType.RuinedCity));
            Assert.That(MapGenerator.TypeOf(MapGenDef.For(SmallSize)), Is.EqualTo(MapType.RuinedCity));
            Assert.That(MapGenerator.TypeOf(NaturalMapGenDef.For(SmallSize)), Is.EqualTo(MapType.Natural));
        }

        [Test]
        public void SelectingTheRuinedCityRunsTheExistingGeneratorUnchanged()
        {
            var size = new GridSize(60, 60, 5);

            var direct = WorldGenerator.Generate(new CellGrid(size), 2468, MapGenDef.For(size));
            var viaFacade = MapGenerator.Generate(new CellGrid(size), 2468, MapType.RuinedCity);

            Assert.That(viaFacade.Type, Is.EqualTo(MapType.RuinedCity));
            Assert.That(viaFacade.City, Is.Not.Null);
            Assert.That(viaFacade.Natural, Is.Null);
            Assert.That(viaFacade.GridHash, Is.EqualTo(direct.GridHash), "the facade changed the city map");
            Assert.That(viaFacade.StartCell, Is.EqualTo(direct.StartCell));
        }

        [Test]
        public void SelectingNaturalRunsTheWildernessGenerator()
        {
            var direct = Generate(1357);
            var viaFacade = MapGenerator.Generate(new CellGrid(SmallSize), 1357, MapType.Natural);

            Assert.That(viaFacade.Type, Is.EqualTo(MapType.Natural));
            Assert.That(viaFacade.Natural, Is.Not.Null);
            Assert.That(viaFacade.City, Is.Null);
            Assert.That(viaFacade.GridHash, Is.EqualTo(direct.GridHash));
        }

        [Test]
        public void ANaturalDefSetToRuinedCityGeneratesACity()
        {
            var size = new GridSize(60, 60, 5);
            var gen = NaturalMapGenDef.For(size);
            gen.mapType = MapType.RuinedCity;

            var outcome = MapGenerator.Generate(new CellGrid(size), 99, gen);

            Assert.That(outcome.Type, Is.EqualTo(MapType.RuinedCity));
            Assert.That(outcome.City, Is.Not.Null);
            Assert.That(outcome.City!.Report.ShellsStamped, Is.GreaterThan(0));
        }

        [Test]
        public void AskingForANaturalMapWithoutItsParametersIsAnError()
        {
            Assert.That(() => NaturalMapGenerator.Generate(new CellGrid(SmallSize), 1, null!),
                Throws.ArgumentNullException);
        }

        [Test]
        public void PassesCanBeStoppedEarly()
        {
            var afterStrata = Generate(5, throughPass: 3);
            Assert.That(afterStrata.Report.PassesRun, Is.EqualTo(3));
            Assert.That(afterStrata.Report.Trees, Is.Zero);
            Assert.That(afterStrata.Report.OreCells, Is.Zero);

            var whole = Generate(5);
            Assert.That(whole.Report.PassesRun, Is.EqualTo(NaturalMapGenerator.PassCount));
            Assert.That(whole.GridHash, Is.Not.EqualTo(afterStrata.GridHash));
        }

        // ---------------------------------------------------------------- module ids

        [Test]
        public void EveryModuleIdIsWellFormedAndUnique()
        {
            var seen = new HashSet<string>();
            foreach (string id in NaturalContent.ModuleIds)
            {
                Assert.That(id, Does.StartWith("odyssey.module."));
                Assert.That(seen.Add(id), Is.True, $"duplicate module id {id}");
            }

            // Fourteen, and deep mining's five terrains (design 62 §5).
            Assert.That(NaturalContent.ModuleIds.Count, Is.EqualTo(19));
        }

        [Test]
        public void EveryTerrainAndEdificeTheGeneratorWritesHasAModuleId()
        {
            var result = Generate(11);
            var ctx = result.Context;

            for (int i = 0; i < ctx.Grid.Terrain.Length; i++)
            {
                ushort terrain = ctx.Grid.Terrain[i];
                if (terrain == NaturalContent.TerrainAir) continue;
                Assert.That(NaturalContent.ModuleForTerrain(terrain), Is.Not.Null,
                    $"terrain {terrain} has no module id");
            }

            foreach (var tree in result.Trees)
                Assert.That(NaturalContent.ModuleForEdifice(tree.Def), Is.Not.Null);

            Assert.That(result.ModuleIds, Is.EquivalentTo(NaturalContent.ModuleIds));
        }

        [Test]
        public void NaturalTerrainIndicesContinueWhereTheCityTableStops()
        {
            // The two tables must never mean two different things by the same index. If this
            // fails, CoreContent grew and NaturalContent was not renumbered with it.
            Assert.That((int)NaturalContent.FirstTerrain, Is.EqualTo(CoreContent.TerrainCount));
            Assert.That(NaturalContent.TerrainCount,
                Is.EqualTo(CoreContent.TerrainCount + NaturalContent.Terrain.Count));

            for (ushort i = 0; i < CoreContent.TerrainCount; i++)
                Assert.That(NaturalContent.IsSolid(i), Is.EqualTo(CoreContent.IsSolid(i)),
                    "the shared lookup disagrees with the city table about a core terrain");

            Assert.That(NaturalContent.IsSolid(NaturalContent.TerrainGrass), Is.True,
                "grass must be solid, or the cell above it has no floor");
        }

        // ---------------------------------------------------------------- performance

        [Test]
        public void GeneratingASmallMapIsFast()
        {
            Generate(1);   // warm the JIT so the measurement is of the generator, not of startup

            var watch = Stopwatch.StartNew();
            var result = Generate(2);
            watch.Stop();

            TestContext.WriteLine($"small {SmallSize}: {watch.ElapsedMilliseconds} ms — {result.Report}");
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(500));
        }

        [Test]
        public void GeneratingTheScaleTargetMapIsReasonable()
        {
            // 250 x 250 x 40, the scale target from ADR 0002, and the figure to compare with the
            // ruined-city generator's own scale-target test. Same M1 budget of 2,000 ms, same
            // five-seed median, for the same reason: one timing measures the machine's mood.
            var size = GridSize.ScaleTarget;
            var times = new List<long>();
            NaturalMapResult? last = null;

            for (uint seed = 4242; seed < 4247; seed++)
            {
                var grid = new CellGrid(size);
                var watch = Stopwatch.StartNew();
                last = NaturalMapGenerator.Generate(grid, seed, NaturalMapGenDef.For(size));
                watch.Stop();
                times.Add(watch.ElapsedMilliseconds);

                Assert.That(last.Report.PassesRun, Is.EqualTo(NaturalMapGenerator.PassCount));
                Assert.That(last.Report.Trees, Is.GreaterThan(100));
                Assert.That(last.Report.OreDeposits, Is.GreaterThan(10));
            }

            long median = WorldgenTests.Median(times);
            TestContext.WriteLine($"scale target {size}: median {median} ms of {WorldgenTests.Listed(times)} — {last!.Report}");
            Assert.That(median, Is.LessThan(2_000), $"M1 generation budget: {WorldgenTests.Listed(times)}");
        }

        /// <summary>
        /// Every board the menu offers generates, on five seeds each, and says what it made.
        ///
        /// <para><b>This is the first thing that can veto a bigger board, and it is seed-shaped.</b>
        /// Several generator knobs are per-map absolutes while the feature densities beside them
        /// are per-area: <c>minReachablePercent</c> and <c>maxForcedFords</c> are whole-map
        /// numbers, <c>pondsPer10000Columns</c> is not. So Huge draws roughly four times the
        /// ponds against the same three-ford budget, and <c>EnsureReachable</c> throws
        /// "The water shapes have severed the map" when the water wins. Seed 1 proves nothing
        /// about that; five seeds per board is the cheapest thing that does, and it costs about a
        /// second in the fast tier.</para>
        ///
        /// <para><b>On the map the game builds</b>, via <see cref="PlayedMap"/> — not on
        /// <c>NaturalMapGenDef.For</c>, which is the unmodified default def and which this arm
        /// used until 2026-09-21. The two are different boards, and the counts below are the
        /// ones a player would see.</para>
        ///
        /// <para>It prints rather than budgets. The one assertion is that every pass ran and the
        /// board is not empty of the things a colony needs — a generator that quietly produced a
        /// featureless plain would otherwise report beautiful timings.</para>
        /// </summary>
        [Test]
        public void EveryOfferedBoardGenerates(
            [ValueSource(nameof(OfferedBoards))] GridSize size)
        {
            var times = new List<long>();
            NaturalMapResult? last = null;

            for (uint seed = 1; seed <= 5; seed++)
            {
                var grid = new CellGrid(size);
                var watch = Stopwatch.StartNew();
                last = NaturalMapGenerator.Generate(grid, seed, PlayedMap.Def(size));
                watch.Stop();
                times.Add(watch.ElapsedMilliseconds);

                Assert.That(last.Report.PassesRun, Is.EqualTo(NaturalMapGenerator.PassCount),
                    $"seed {seed} on {size} stopped early");
                Assert.That(last.Report.Trees, Is.GreaterThan(0), $"seed {seed} on {size} is bare");
            }

            TestContext.WriteLine(
                $"[Board] {size}: {size.CellCount:N0} cells, generation median " +
                $"{WorldgenTests.Median(times)} ms of {WorldgenTests.Listed(times)} — {last!.Report}");
        }

        /// <summary>The boards <c>MapSizes</c> offers, mirrored in <see cref="BoardSizes"/>.</summary>
        static IEnumerable<GridSize> OfferedBoards()
        {
            yield return BoardSizes.Small;
            yield return BoardSizes.Standard;
            yield return BoardSizes.Large;
            yield return BoardSizes.Huge;
        }

        // ---------------------------------------------------------------- helpers

        static NaturalMapResult GenerateWith(uint seed, Action<NaturalMapGenDef> tune)
        {
            var gen = NaturalMapGenDef.For(SmallSize);
            tune(gen);
            return NaturalMapGenerator.Generate(new CellGrid(SmallSize), seed, gen);
        }
    }
}
