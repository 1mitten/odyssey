#nullable enable
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The ore and the caverns by depth (design 62 §5, DM3): what the played board actually holds,
    /// per band per seed, at 16 layers (today's board) and at 32 (deep mining's), both built
    /// through <see cref="PlayedMap"/> so the census is of the board a colony is generated on.
    ///
    /// <para>The census is printed as well as asserted, because §5c's numbers are tuned against
    /// it: a failure here should be read beside the table it prints.</para>
    /// </summary>
    public class NaturalOreCensusTests
    {
        static readonly GridSize Shallow = new GridSize(120, 120, 16);
        static readonly GridSize Deep = new GridSize(120, 120, 32);
        static readonly uint[] Seeds = { 1u, 2u, 3u };

        /// <summary>One kind's numbers on one board.</summary>
        sealed class KindCensus
        {
            public int Deposits, Cells, CellsInBand, OffBand, OnCaveWall, InDeepStone;
            public long DepthSum;
            public int MinDeposit = int.MaxValue, MaxDeposit;
            public double MeanDepth => Cells == 0 ? 0 : (double)DepthSum / Cells;
            public double InBandShare => Cells == 0 ? 0 : (double)CellsInBand / Cells;
        }

        sealed class Census
        {
            public NaturalMapResult Result = null!;
            public CellGrid Grid = null!;
            public KindCensus[] Kinds = null!;
            public int DeepStone;
            public List<int> CavernSizes = new List<int>();
            public List<int> CavernFloorDepths = new List<int>();
        }

        static Census Take(GridSize size, uint seed)
        {
            CellGrid grid = PlayedMap.Generate(size, seed, out NaturalMapResult result);
            var ctx = result.Context;
            var census = new Census { Result = result, Grid = grid, Kinds = new KindCensus[NaturalContent.OreKindCount] };
            for (int k = 0; k < census.Kinds.Length; k++) census.Kinds[k] = new KindCensus();

            foreach (OreDeposit deposit in result.OreDeposits)
            {
                KindCensus kind = census.Kinds[deposit.Kind];
                kind.Deposits++;
                if (deposit.OffBand) kind.OffBand++;
                if (deposit.OnCaveWall) kind.OnCaveWall++;
                if (deposit.Cells < kind.MinDeposit) kind.MinDeposit = deposit.Cells;
                if (deposit.Cells > kind.MaxDeposit) kind.MaxDeposit = deposit.Cells;
            }

            for (int index = 0; index < size.CellCount; index++)
            {
                ushort terrain = grid.Terrain[index];
                if (terrain == NaturalContent.TerrainDeepStone) census.DeepStone++;
                int k = NaturalContent.OreKindOf(terrain);
                if (k < 0) continue;

                CellRef at = size.FromIndex(index);
                int column = ctx.Column(at.X, at.Z);
                int depth = ctx.SurfaceY[column] - at.Y;
                var ore = NaturalContent.OreAt(k);
                KindCensus kind = census.Kinds[k];
                kind.Cells++;
                kind.DepthSum += depth;
                if (depth >= ore.MinDepth && depth <= ore.MaxDepth) kind.CellsInBand++;
                if (at.Y < ctx.DeepStoneTopY[column]) kind.InDeepStone++;
            }

            foreach (CavernChamber chamber in result.Caverns)
            {
                census.CavernSizes.Add(chamber.Cells);
                CellRef at = size.FromIndex(chamber.CellIndex);
                census.CavernFloorDepths.Add(ctx.SurfaceY[ctx.Column(at.X, at.Z)] - at.Y);
            }
            return census;
        }

        static string Print(GridSize size, uint seed, Census census)
        {
            var text = new StringBuilder();
            text.AppendLine($"{size} seed {seed}: deep stone {census.DeepStone}, caverns " +
                $"[{string.Join(", ", census.CavernSizes)}] floors at depth [{string.Join(", ", census.CavernFloorDepths)}], " +
                $"pillars {census.Result.Report.CavernPillars}");
            text.AppendLine("  kind            band   deposits  cells  size     mean depth  in band  off-band  cave wall  deep stone");
            for (int k = 0; k < census.Kinds.Length; k++)
            {
                var ore = NaturalContent.OreAt(k);
                KindCensus c = census.Kinds[k];
                string name = WorldContent.OreOrder[k];
                text.AppendLine($"  {name,-16}{ore.MinDepth,2}-{ore.MaxDepth,-2}  {c.Deposits,8}  {c.Cells,5}  " +
                    $"{(c.Deposits == 0 ? "-" : c.MinDeposit + "-" + c.MaxDeposit),-7}  {c.MeanDepth,10:F1}  " +
                    $"{c.InBandShare,7:P0}  {c.OffBand,8}  {c.OnCaveWall,9}  {c.InDeepStone,10}");
            }
            return text.ToString();
        }

        static int Kind(string defName) => System.Array.IndexOf(WorldContent.OreOrder, defName);

        [Test]
        public void TheCensusAt32LayersIsWhatTheBandsAskFor()
        {
            var wall = new int[2];
            var wallOf = new int[2];
            foreach (uint seed in Seeds)
            {
                Census census = Take(Deep, seed);
                TestContext.WriteLine(Print(Deep, seed, census));

                Assert.That(census.DeepStone, Is.GreaterThan(0), "a 32-layer board has no deep stone");
                for (int k = 0; k < census.Kinds.Length; k++)
                {
                    var ore = NaturalContent.OreAt(k);
                    KindCensus c = census.Kinds[k];
                    string name = WorldContent.OreOrder[k];
                    Assert.That(c.Deposits, Is.GreaterThan(0), $"seed {seed}: no {name}");
                    Assert.That(c.MaxDeposit, Is.LessThanOrEqualTo(ore.MaxCells), $"seed {seed}: a {name} deposit over its size");
                    Assert.That(c.MeanDepth, Is.InRange(ore.MinDepth - 1.0, ore.MaxDepth + 1.0),
                        $"seed {seed}: {name}'s mean depth is outside its band");
                    Assert.That(c.InBandShare, Is.GreaterThanOrEqualTo(0.75), $"seed {seed}: too little {name} in its band");
                    // Off-band is about one deposit in twenty; allow for a small count's luck.
                    Assert.That(c.OffBand, Is.LessThanOrEqualTo(System.Math.Max(3, c.Deposits * 15 / 100)),
                        $"seed {seed}: too much {name} off its band");
                }

                // Emberquartz is band 18-20 only and never drawn off it.
                Assert.That(census.Kinds[Kind("Ore_Emberquartz")].OffBand, Is.Zero);
                // Gold favours the deep stone its band sits in.
                KindCensus gold = census.Kinds[Kind("Ore_Gold")];
                Assert.That(gold.InDeepStone * 2, Is.GreaterThan(gold.Cells), $"seed {seed}: gold does not favour deep stone");

                wall[0] += gold.OnCaveWall; wallOf[0] += gold.Deposits;
                KindCensus gems = census.Kinds[Kind("Ore_Gems")];
                wall[1] += gems.OnCaveWall; wallOf[1] += gems.Deposits;

                Assert.That(census.CavernSizes.Count, Is.InRange(3, 5), $"seed {seed}: cavern count");
                foreach (int cells in census.CavernSizes)
                    Assert.That(cells, Is.InRange(150, 1500), $"seed {seed}: a cavern of {cells} cells");
            }

            // 30-50 % of gold and gem deposits touch a cave wall (design 62 §5c), across the seeds.
            TestContext.WriteLine($"cave wall: gold {wall[0]}/{wallOf[0]}, gems {wall[1]}/{wallOf[1]}");
            Assert.That(wall[0] * 100 / wallOf[0], Is.InRange(25, 55), "gold on a cave wall");
            Assert.That(wall[1] * 100 / wallOf[1], Is.InRange(25, 55), "gems on a cave wall");
        }

        /// <summary>
        /// Today's board is too shallow for most of the bands: they clamp into the rock rather
        /// than vanish, so every kind is still on it, and the rock is all plain rock.
        /// </summary>
        [Test]
        public void At16LayersEveryKindIsClampedIntoTheRockThatExists()
        {
            foreach (uint seed in Seeds)
            {
                Census census = Take(Shallow, seed);
                TestContext.WriteLine(Print(Shallow, seed, census));

                Assert.That(census.DeepStone, Is.Zero, "a 16-layer column reached the deep stone and lost its rock");
                for (int k = 0; k < census.Kinds.Length; k++)
                    Assert.That(census.Kinds[k].Deposits, Is.GreaterThan(0), $"seed {seed}: no {WorldContent.OreOrder[k]}");
                Assert.That(census.CavernSizes.Count, Is.InRange(3, 5), $"seed {seed}: cavern count");
            }
        }

        [TestCase(16)]
        [TestCase(32)]
        public void NoDepositSitsOutsideTheRockBand(int layers)
        {
            var size = new GridSize(120, 120, layers);
            foreach (uint seed in Seeds)
            {
                CellGrid grid = PlayedMap.Generate(size, seed, out NaturalMapResult result);
                var ctx = result.Context;
                int found = 0;
                for (int index = 0; index < size.CellCount; index++)
                {
                    if (!NaturalContent.IsOre(grid.Terrain[index])) continue;
                    found++;
                    CellRef at = size.FromIndex(index);
                    int column = ctx.Column(at.X, at.Z);
                    Assert.That(at.Y, Is.LessThan(ctx.SubsoilBaseY[column]), $"ore at {at} is above the rock");
                    Assert.That(at.Y, Is.GreaterThanOrEqualTo(ctx.BedrockTopY[column]), $"ore at {at} is in the bedrock");
                    Assert.That(grid.IsSolidTerrain(index), Is.True);
                }
                Assert.That(found, Is.EqualTo(result.Report.OreCells), "the report and the grid disagree about the ore");
            }
        }

        /// <summary>Deep stone lies from <c>deepStoneDepth</c> below the surface to the bedrock, and nowhere else.</summary>
        [Test]
        public void DeepStoneIsTheBottomOfEachDeepColumn()
        {
            CellGrid grid = PlayedMap.Generate(Deep, 1u, out NaturalMapResult result);
            var ctx = result.Context;
            int depthLimit = ctx.Gen.deepStoneDepth;
            for (int z = 0; z < Deep.SizeZ; z++)
            for (int x = 0; x < Deep.SizeX; x++)
            {
                int column = ctx.Column(x, z);
                for (int y = 0; y < Deep.SizeY; y++)
                {
                    int index = Deep.Index(x, z, y);
                    if (grid.Terrain[index] != NaturalContent.TerrainDeepStone) continue;
                    Assert.That(ctx.SurfaceY[column] - y, Is.GreaterThanOrEqualTo(depthLimit), $"deep stone at {x},{z},{y} is too shallow");
                    Assert.That(y, Is.GreaterThanOrEqualTo(ctx.BedrockTopY[column]));
                }
            }
        }

        /// <summary>
        /// Every carved cavern cell's ceiling is within <see cref="RockSpan.Cells"/> of a column of
        /// rock standing to below the floor, asked of the grid by <see cref="RockSpan"/> itself —
        /// the same check the cave-in unit will make.
        /// </summary>
        [TestCase(16)]
        [TestCase(32)]
        public void EveryCavernCeilingIsWithinTheSpan(int layers)
        {
            var size = new GridSize(120, 120, layers);
            foreach (uint seed in Seeds)
            {
                CellGrid grid = PlayedMap.Generate(size, seed, out NaturalMapResult result);
                Assert.That(result.Context.CavernCells.Count, Is.GreaterThan(0));
                int bad = RockSpan.FirstUnsupported(grid, result.Context.CavernCells);
                Assert.That(bad, Is.EqualTo(-1),
                    $"seed {seed}: the cavern cell {(bad < 0 ? default : size.FromIndex(bad))} is more than {RockSpan.Cells} steps from a support");
            }
        }

        /// <summary>The control: the check has to be able to fail. A room fifteen wide with no pillar does.</summary>
        [Test]
        public void TheSpanCheckFindsAnUnheldRoom()
        {
            var size = new GridSize(40, 40, 8);
            var grid = new CellGrid(size);
            for (int i = 0; i < size.CellCount; i++)
            {
                grid.Terrain[i] = NaturalContent.TerrainRock;
                grid.Flags[i] |= CellFlags.SolidTerrain;
            }

            var holes = new List<int>();
            for (int z = 10; z < 25; z++)
            for (int x = 10; x < 25; x++)
            {
                int cell = size.Index(x, z, 3);
                grid.Flags[cell] &= ~CellFlags.SolidTerrain;
                grid.Terrain[cell] = NaturalContent.TerrainAir;
                holes.Add(cell);
            }

            Assert.That(RockSpan.FirstUnsupported(grid, holes), Is.Not.EqualTo(-1), "a fifteen-wide room passed");
            Assert.That(RockSpan.StepsToSupport(grid, size.Index(10, 10, 3)), Is.EqualTo(1), "the corner is one step from the wall");
            Assert.That(RockSpan.StepsToSupport(grid, size.Index(17, 17, 3)), Is.EqualTo(-1), "the middle is eight from any wall");

            // A pillar in the middle holds it.
            for (int y = 0; y < size.SizeY; y++)
            {
                int pillar = size.Index(17, 17, y);
                grid.Flags[pillar] |= CellFlags.SolidTerrain;
            }
            holes.Remove(size.Index(17, 17, 3));
            Assert.That(RockSpan.FirstUnsupported(grid, holes), Is.EqualTo(-1), "a pillar in the middle did not hold the room");
        }

        [Test]
        public void TheSameSeedTakesTheSameCensus()
        {
            CellGrid a = PlayedMap.Generate(Deep, 7u, out NaturalMapResult first);
            CellGrid b = PlayedMap.Generate(Deep, 7u, out NaturalMapResult second);
            Assert.That(second.GridHash, Is.EqualTo(first.GridHash));
            Assert.That(second.OreDeposits.Count, Is.EqualTo(first.OreDeposits.Count));
            for (int i = 0; i < first.OreDeposits.Count; i++)
            {
                Assert.That(second.OreDeposits[i].CellIndex, Is.EqualTo(first.OreDeposits[i].CellIndex), $"deposit {i}");
                Assert.That(second.OreDeposits[i].Cells, Is.EqualTo(first.OreDeposits[i].Cells), $"deposit {i}");
            }
            Assert.That(second.Context.CavernCells, Is.EqualTo(first.Context.CavernCells));
        }

        /// <summary>The bare board is still the baseline: no ore, no caverns, no deep stone on a 16-layer board.</summary>
        [Test]
        public void TheBareBoardHasNoOre()
        {
            var size = Shallow;
            var grid = new CellGrid(size);
            var result = NaturalMapGenerator.Generate(grid, 1u, NaturalMapGenDef.For(size).MakeBarren());
            Assert.That(result.OreDeposits.Count, Is.Zero);
            Assert.That(result.Report.OreCells, Is.Zero);
            Assert.That(result.Caverns.Count, Is.Zero);
        }
    }
}
