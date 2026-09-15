#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The barren board is the prototype's baseline: flat, grass everywhere, nothing scattered on
    /// it. Its value is that on a uniform surface **anything that is not grass is a bug**, so it
    /// is worth asserting rather than eyeballing.
    /// </summary>
    public class BarrenMapTests
    {
        static (CellGrid grid, MapGenOutcome outcome) Generate(int x, int z, int y, uint seed = 1u)
        {
            var size = new GridSize(x, z, y);
            var grid = new CellGrid(size);
            var gen = NaturalMapGenDef.For(size).MakeBarren();
            return (grid, MapGenerator.Generate(grid, seed, gen));
        }

        [TestCase(60, 60, 5)]
        [TestCase(120, 120, 16)]
        public void EverySurfaceCellIsGrass(int x, int z, int y)
        {
            var (grid, _) = Generate(x, z, y);
            var size = grid.Size;

            for (int gz = 0; gz < size.SizeZ; gz++)
            for (int gx = 0; gx < size.SizeX; gx++)
            {
                int top = -1;
                for (int gy = size.SizeY - 1; gy >= 0; gy--)
                {
                    int index = size.Index(gx, gz, gy);
                    if (!grid.IsSolidTerrain(index)) continue;
                    top = gy;
                    break;
                }

                Assert.That(top, Is.GreaterThanOrEqualTo(0), $"column {gx},{gz} has no ground at all");
                Assert.That(grid.Terrain[size.Index(gx, gz, top)], Is.EqualTo(NaturalContent.TerrainGrass),
                    $"column {gx},{gz} is not grass on top");
            }
        }

        [Test]
        public void TheSurfaceIsFlat()
        {
            var (grid, _) = Generate(60, 60, 8);
            var size = grid.Size;
            int expected = -1;

            for (int gz = 0; gz < size.SizeZ; gz++)
            for (int gx = 0; gx < size.SizeX; gx++)
            {
                int top = -1;
                for (int gy = size.SizeY - 1; gy >= 0; gy--)
                {
                    if (!grid.IsSolidTerrain(size.Index(gx, gz, gy))) continue;
                    top = gy;
                    break;
                }
                if (expected < 0) expected = top;
                Assert.That(top, Is.EqualTo(expected), $"column {gx},{gz} steps to layer {top}");
            }
        }

        [Test]
        public void NothingIsScatteredOnIt()
        {
            var (grid, outcome) = Generate(120, 120, 16);
            Assert.That(outcome.Natural!.Trees.Count, Is.Zero, "a barren board has no trees");
            Assert.That(outcome.Natural!.Outcrops.Count, Is.Zero, "a barren board has no outcrops");
            Assert.That(outcome.Natural!.OreDeposits.Count, Is.Zero, "a barren board has no ore");

            // Nothing standing in any cell either, so the whole surface is walkable.
            for (int i = 0; i < grid.Edifice.Length; i++)
                Assert.That(grid.Edifice[i], Is.EqualTo(-1), "a barren board has nothing built or grown on it");
        }

        [Test]
        public void DiggingStillFindsRockBeneath()
        {
            // Barren means a plain surface, not a hollow world: the strata are untouched.
            var (grid, outcome) = Generate(60, 60, 12);
            var size = grid.Size;
            int below = outcome.StartCell.Y - 3;
            Assert.That(below, Is.GreaterThanOrEqualTo(0));
            Assert.That(grid.IsSolidTerrain(size.Index(outcome.StartCell.X, outcome.StartCell.Z, below)), Is.True,
                "there should still be something to mine under a barren board");
        }

        [Test]
        public void TheWholeSurfaceIsWalkable()
        {
            var (grid, outcome) = Generate(60, 60, 8);
            var size = grid.Size;
            int layer = outcome.StartCell.Y;

            for (int gz = 0; gz < size.SizeZ; gz++)
            for (int gx = 0; gx < size.SizeX; gx++)
                Assert.That(grid.IsWalkable(size.Index(gx, gz, layer)), Is.True,
                    $"cell {gx},{gz} on the start layer is not walkable");
        }
    }
}
