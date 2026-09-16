#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Caverns are sealed: voids in the rock with no way in but a pick.
    ///
    /// The word "sealed" is doing real work, so it is tested as a property of the grid rather than
    /// taken from the pass's own bookkeeping. A chamber that reached the open air would be a pit
    /// in the meadow; one that reached the bedrock would be a crack in the floor of the world.
    /// </summary>
    public class CavernTests
    {
        static (CellGrid grid, NaturalGenContext ctx, NaturalMapResult result) Generate(
            uint seed = 1u, int x = 120, int z = 120, int y = 16)
        {
            var size = new GridSize(x, z, y);
            var grid = new CellGrid(size);
            var gen = NaturalMapGenDef.For(size).MakeWooded();
            var result = NaturalMapGenerator.Generate(grid, seed, gen);
            return (grid, result.Context, result);
        }

        [Test]
        public void ThePlayedBoardHasAFewChambers()
        {
            var (_, _, result) = Generate();
            Assert.That(result.Caverns.Count, Is.InRange(3, 5), "the played board's cavern count moved");
            Assert.That(result.Report.CavernCells, Is.GreaterThan(0));
        }

        [Test]
        public void NoChamberReachesTheOpenAir()
        {
            var (grid, ctx, _) = Generate();
            var size = grid.Size;

            foreach (int cell in ctx.CavernCells)
            {
                CellRef at = size.FromIndex(cell);
                int column = ctx.Column(at.X, at.Z);

                Assert.That(at.Y, Is.LessThan(ctx.SubsoilBaseY[column] - 1),
                    $"the cavern cell {at} is up against the subsoil holding the surface up");
                Assert.That(at.Y, Is.GreaterThan(ctx.BedrockTopY[column]),
                    $"the cavern cell {at} has broken into the bedrock");
                Assert.That(grid.IsSolidTerrain(cell), Is.False, $"the cavern cell {at} is still solid");
            }
        }

        [Test]
        public void AChamberFloorIsStandableAndItsCeilingIsNot()
        {
            // The point of a chamber is that it is somewhere a colonist can be, the moment the
            // wall is breached. That needs a floor, which underground means solid rock below.
            var (grid, ctx, _) = Generate();
            var size = grid.Size;
            int standable = 0;

            foreach (int cell in ctx.CavernCells)
            {
                if (grid.IsWalkable(cell)) standable++;

                int above = cell + size.LayerStride;
                Assert.That(above, Is.LessThan(size.CellCount));
                if (!ctx.IsCavern(above))
                    Assert.That(grid.IsSolidTerrain(above), Is.True,
                        $"the cavern cell {size.FromIndex(cell)} has no rock over its head");
            }

            Assert.That(standable, Is.GreaterThan(0), "not one cavern cell can be stood in");
        }

        [Test]
        public void TheBarrenBoardHasNone()
        {
            var size = new GridSize(60, 60, 16);
            var grid = new CellGrid(size);
            var gen = NaturalMapGenDef.For(size).MakeBarren();
            var result = NaturalMapGenerator.Generate(grid, 1u, gen);

            Assert.That(result.Caverns.Count, Is.Zero, "the barren baseline grew a hole");
            Assert.That(result.Report.CavernCells, Is.Zero);
        }

        [Test]
        public void ChambersAreTheSameFromTheSameSeed()
        {
            var (_, a, _) = Generate(9u);
            var (_, b, _) = Generate(9u);

            Assert.That(a.CavernCells.Count, Is.EqualTo(b.CavernCells.Count));
            for (int i = 0; i < a.CavernCells.Count; i++)
                Assert.That(a.CavernCells[i], Is.EqualTo(b.CavernCells[i]), $"cavern cell {i} differs between runs");
        }

        [Test]
        public void SwitchingCavernsOffDoesNotMoveTheOre()
        {
            // The ore pass draws its cavern-wall index whether or not it uses it, so that a map
            // with no caverns rolls the same ore as a map with them. Without that the two
            // features would be entangled: turning caverns off would reshuffle every deposit,
            // and no A/B of the cavern feature would mean anything.
            var size = new GridSize(120, 120, 16);

            var withGrid = new CellGrid(size);
            var with = NaturalMapGenerator.Generate(withGrid, 3u, NaturalMapGenDef.For(size).MakeWooded());

            var withoutDef = NaturalMapGenDef.For(size).MakeWooded();
            withoutDef.cavernsPer10000Columns = 0;
            var withoutGrid = new CellGrid(size);
            var without = NaturalMapGenerator.Generate(withoutGrid, 3u, withoutDef);

            Assert.That(without.Caverns.Count, Is.Zero);
            Assert.That(with.OreDeposits.Count, Is.EqualTo(without.OreDeposits.Count),
                "switching caverns off changed how many deposits there are");
        }
    }
}
