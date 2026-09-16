#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The wooded board is what the scene loads: the barren board with its trees and its water
    /// put back, and nothing else. It gives up the barren board's "anything that is not grass is
    /// a bug", so what it keeps has to be said exactly — flat grass and trees everywhere the
    /// water is not, water exactly one layer down, and no rock or ore anywhere.
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
        public void TheDrySurfaceIsFlatGrass()
        {
            var (grid, outcome) = Generate(60, 60, 8);
            var size = grid.Size;
            var ctx = outcome.Natural!.Context;
            int expected = -1;

            for (int gz = 0; gz < size.SizeZ; gz++)
            for (int gx = 0; gx < size.SizeX; gx++)
            {
                int column = ctx.Column(gx, gz);
                if (ctx.Water[column] != (byte)WaterClass.None) continue;

                int top = TopOf(grid, gx, gz);
                if (expected < 0) expected = top;
                Assert.That(top, Is.EqualTo(expected), $"column {gx},{gz} steps to layer {top}");
                Assert.That(grid.Terrain[size.Index(gx, gz, top)], Is.EqualTo(NaturalContent.TerrainGrass),
                    $"column {gx},{gz} is not grass on top");
            }
        }

        /// <summary>
        /// The one break in an otherwise flat board, and the shape of it: a channel is cut
        /// exactly one layer down, holds one cell of water over a bed of sand or gravel, and its
        /// banks are the ordinary board. A bog is not a channel — it is wet ground that was never
        /// lowered, so it stays level with the grass around it.
        /// </summary>
        [Test]
        public void TheOnlyBreakInTheBoardIsItsWater()
        {
            var (grid, outcome) = Generate(60, 60, 8);
            var size = grid.Size;
            var ctx = outcome.Natural!.Context;

            int dryLevel = -1;
            for (int column = 0; column < ctx.Columns; column++)
                if (ctx.Water[column] == (byte)WaterClass.None) { dryLevel = ctx.SurfaceY[column]; break; }
            Assert.That(dryLevel, Is.GreaterThan(0), "the board is all water");

            int water = 0, marsh = 0;
            for (int gz = 0; gz < size.SizeZ; gz++)
            for (int gx = 0; gx < size.SizeX; gx++)
            {
                int column = ctx.Column(gx, gz);
                var kind = (WaterClass)ctx.Water[column];
                if (kind == WaterClass.None) continue;

                int surface = ctx.SurfaceY[column];
                ushort bed = grid.Terrain[size.Index(gx, gz, surface)];

                if (kind == WaterClass.Marsh)
                {
                    Assert.That(surface, Is.EqualTo(dryLevel), $"the bog at {gx},{gz} was lowered");
                    Assert.That(bed, Is.EqualTo(NaturalContent.TerrainMarsh));
                    marsh++;
                    continue;
                }

                Assert.That(surface, Is.EqualTo(dryLevel - 1),
                    $"the channel at {gx},{gz} is not exactly one layer down");
                Assert.That(NaturalContent.IsGround(bed), Is.True, $"the bed at {gx},{gz} is not ground");

                ushort here = grid.Terrain[size.Index(gx, gz, surface + 1)];
                Assert.That(here, Is.EqualTo(kind == WaterClass.Shallow
                    ? NaturalContent.TerrainShallowWater
                    : NaturalContent.TerrainDeepWater), $"the cell at {gx},{gz} is not the water it was planned as");
                water++;
            }

            Assert.That(water, Is.GreaterThan(0), "the wooded board has no water at all");
            Assert.That(marsh, Is.GreaterThan(0), "water with no bog around it");
        }

        [Test]
        public void EveryTreeStandsOnTheSurfaceAndBlocksNothing()
        {
            // A tree sits in the air cell above the ground it grows from and a colonist walks
            // through it (NaturalContent.EdificeTreeConifer): woodland is a supply of wood, not
            // a wall. Every surface cell therefore stays walkable, trees included.
            var (grid, outcome) = Generate(60, 60, 8);
            var size = grid.Size;
            var ctx = outcome.Natural!.Context;
            var placed = ctx.Edifices;
            int standing = 0;

            for (int gz = 0; gz < size.SizeZ; gz++)
            for (int gx = 0; gx < size.SizeX; gx++)
            {
                // Per column rather than per layer. On a board with no water those are the same
                // scan, because every column stands at the same height; a channel is exactly the
                // thing that makes them different.
                int column = ctx.Column(gx, gz);
                int cell = size.Index(gx, gz, ctx.TopSolidY[column] + 1);

                if (ctx.Water[column] == (byte)WaterClass.Deep)
                {
                    Assert.That(grid.IsWalkable(cell), Is.False, $"deep water at {gx},{gz} is walkable");
                    continue;
                }

                Assert.That(grid.IsWalkable(cell), Is.True, $"cell {gx},{gz} is not walkable");
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
