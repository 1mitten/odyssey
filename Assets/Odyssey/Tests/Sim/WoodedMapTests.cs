#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The wooded board is what the scene loads: the barren board with its trees put back and
    /// nothing else. It gives up the barren board's "anything that is not grass is a bug", so
    /// what it keeps has to be said exactly — flat, grass everywhere, trees, and no rock or ore.
    /// </summary>
    public class WoodedMapTests
    {
        static (CellGrid grid, MapGenOutcome outcome) Generate(int x, int z, int y, uint seed = 1u)
        {
            var size = new GridSize(x, z, y);
            var grid = new CellGrid(size);
            var gen = NaturalMapGenDef.For(size).MakeWooded();
            return (grid, MapGenerator.Generate(grid, seed, gen));
        }

        static int TopOf(CellGrid grid, int gx, int gz)
        {
            var size = grid.Size;
            for (int gy = size.SizeY - 1; gy >= 0; gy--)
                if (grid.IsSolidTerrain(size.Index(gx, gz, gy))) return gy;
            return -1;
        }

        [Test]
        public void ItHasTreesAndNothingElseScattered()
        {
            var (_, outcome) = Generate(120, 120, 16);
            Assert.That(outcome.Natural!.Trees.Count, Is.GreaterThan(100), "a wooded board grows trees");
            Assert.That(outcome.Natural!.Outcrops.Count, Is.Zero, "a wooded board has no outcrops");
            Assert.That(outcome.Natural!.OreDeposits.Count, Is.Zero, "a wooded board has no ore");
        }

        [Test]
        public void TheSurfaceIsFlatGrass()
        {
            var (grid, _) = Generate(60, 60, 8);
            var size = grid.Size;
            int expected = -1;

            for (int gz = 0; gz < size.SizeZ; gz++)
            for (int gx = 0; gx < size.SizeX; gx++)
            {
                int top = TopOf(grid, gx, gz);
                if (expected < 0) expected = top;
                Assert.That(top, Is.EqualTo(expected), $"column {gx},{gz} steps to layer {top}");
                Assert.That(grid.Terrain[size.Index(gx, gz, top)], Is.EqualTo(NaturalContent.TerrainGrass),
                    $"column {gx},{gz} is not grass on top");
            }
        }

        [Test]
        public void EveryTreeStandsOnTheSurfaceAndBlocksNothing()
        {
            // A tree sits in the air cell above the ground it grows from and a colonist walks
            // through it (NaturalContent.EdificeTreeConifer): woodland is a supply of wood, not
            // a wall. Every surface cell therefore stays walkable, trees included.
            var (grid, outcome) = Generate(60, 60, 8);
            var size = grid.Size;
            var placed = outcome.Natural!.Context.Edifices;
            int layer = outcome.StartCell.Y;
            int standing = 0;

            for (int gz = 0; gz < size.SizeZ; gz++)
            for (int gx = 0; gx < size.SizeX; gx++)
            {
                int cell = size.Index(gx, gz, layer);
                Assert.That(grid.IsWalkable(cell), Is.True, $"cell {gx},{gz} on the start layer is not walkable");
                // The grid holds a handle into the placement list, not the edifice kind itself.
                int handle = grid.Edifice[cell];
                if (handle < 0) continue;
                Assert.That(NaturalContent.IsTree(placed[handle].Def), Is.True,
                    $"cell {gx},{gz} holds something that is not a tree");
                standing++;
            }

            Assert.That(standing, Is.EqualTo(outcome.Natural!.Trees.Count),
                "every tree the report counts stands in a cell on the surface layer");
        }

        [Test]
        public void TheStartIsAClearing()
        {
            var (grid, outcome) = Generate(120, 120, 16);
            var size = grid.Size;
            int layer = outcome.StartCell.Y;
            for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int gx = outcome.StartCell.X + dx, gz = outcome.StartCell.Z + dz;
                Assert.That(grid.Edifice[size.Index(gx, gz, layer)], Is.EqualTo(-1),
                    $"a tree stands at {gx},{gz}, inside the start clearing");
            }
        }

        [Test]
        public void ItIsDeterministicFromTheSeed()
        {
            var (a, ao) = Generate(60, 60, 8, 7u);
            var (b, bo) = Generate(60, 60, 8, 7u);
            Assert.That(ao.Natural!.Trees.Count, Is.EqualTo(bo.Natural!.Trees.Count));
            for (int i = 0; i < a.Edifice.Length; i++)
                Assert.That(a.Edifice[i], Is.EqualTo(b.Edifice[i]), $"cell {i} differs between two runs of one seed");
        }
    }
}
