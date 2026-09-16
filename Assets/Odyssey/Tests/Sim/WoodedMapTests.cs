#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The wooded board is what the scene loads. It gives up the barren board's "anything that is
    /// not grass is a bug", so what it keeps has to be said exactly: grass cover everywhere, a
    /// terraced surface, woodland, rock outcrops standing on the ground, ore inside the rock, and
    /// a flat clearing to start in.
    ///
    /// <para>This file was re-based on 2026-09-16 for the mining MVP. It used to assert the
    /// opposite of three of these — that the board was dead flat and carried no outcrops and no
    /// ore — because the wooded board was built as the barren board with its trees put back. The
    /// change is deliberate and is the whole feature; the mechanism is
    /// <see cref="NaturalMapGenDef.MakeWooded"/> no longer routing through
    /// <see cref="NaturalMapGenDef.MakeBarren"/>. <c>BarrenMapTests</c> still holds the flat,
    /// empty baseline.</para>
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
        public void ItHasTreesRockAndOre()
        {
            var (_, outcome) = Generate(120, 120, 16);
            var natural = outcome.Natural!;
            Assert.That(natural.Trees.Count, Is.GreaterThan(100), "a wooded board grows trees");
            Assert.That(natural.Outcrops.Count, Is.GreaterThan(0), "a wooded board has stone standing on it");
            Assert.That(natural.OreDeposits.Count, Is.GreaterThan(0), "a wooded board has ore under it");
        }

        [Test]
        public void TheSurfaceIsTerracedAndCoveredInGrass()
        {
            var (grid, outcome) = Generate(120, 120, 16);
            var size = grid.Size;
            var report = outcome.Natural!.Report;

            // Terracing, not hills: more than one step, and never more steps than the relief
            // allows either side of the nominal ground layer.
            Assert.That(report.SurfaceSpread, Is.GreaterThan(1), "the board is still a flat table");
            Assert.That(report.SurfaceSpread, Is.LessThanOrEqualTo(2 * 2 + 1), "the relief exceeded its band");

            for (int gz = 0; gz < size.SizeZ; gz++)
            for (int gx = 0; gx < size.SizeX; gx++)
            {
                int top = TopOf(grid, gx, gz);
                ushort terrain = grid.Terrain[size.Index(gx, gz, top)];

                // Grass everywhere the cover pass touched, and rock where an outcrop stands on
                // it: those are the only two things that can be the top of a wooded column.
                Assert.That(terrain == NaturalContent.TerrainGrass || terrain == NaturalContent.TerrainRock,
                    Is.True, $"column {gx},{gz} is topped with terrain {terrain}, neither grass nor rock");
            }
        }

        [Test]
        public void EveryTreeStandsOnItsOwnGroundAndBlocksNothing()
        {
            // A tree sits in the air cell above the ground it grows from and a colonist walks
            // through it (NaturalContent.EdificeTreeConifer): woodland is a supply of wood, not
            // a wall. The board is terraced now, so this walks the trees the report lists rather
            // than one layer of the grid — on a stepped map a single layer is not the surface.
            var (grid, outcome) = Generate(120, 120, 16);
            var size = grid.Size;
            var placed = outcome.Natural!.Context.Edifices;

            foreach (TreePlacement tree in outcome.Natural!.Trees)
            {
                CellRef at = size.FromIndex(tree.CellIndex);
                Assert.That(grid.IsSolidTerrain(tree.CellIndex), Is.False, $"the tree at {at} is inside solid ground");
                Assert.That(grid.IsWalkable(tree.CellIndex), Is.True, $"the tree at {at} fences its own cell off");

                int below = tree.CellIndex - size.LayerStride;
                Assert.That(grid.IsSolidTerrain(below), Is.True, $"the tree at {at} is rooted in mid-air");
                Assert.That(NaturalContent.IsGround(grid.Terrain[below]), Is.True,
                    $"the tree at {at} grows out of terrain {grid.Terrain[below]}, which is not ground");

                int handle = grid.Edifice[tree.CellIndex];
                Assert.That(handle, Is.GreaterThanOrEqualTo(0), $"the tree at {at} left no edifice behind");
                Assert.That(NaturalContent.IsTree(placed[handle].Def), Is.True,
                    $"cell {at} holds something that is not a tree");
            }
        }

        [Test]
        public void TheStartIsAFlatClearing()
        {
            var (grid, outcome) = Generate(120, 120, 16);
            var size = grid.Size;
            int layer = outcome.StartCell.Y;

            for (int dz = -2; dz <= 2; dz++)
            for (int dx = -2; dx <= 2; dx++)
            {
                int gx = outcome.StartCell.X + dx, gz = outcome.StartCell.Z + dz;
                int cell = size.Index(gx, gz, layer);

                // One flat step, walkable, with nothing standing in it. A clearing that spans two
                // surface layers is two clearings with a step between them, which is the thing the
                // start pass scores hardest against.
                Assert.That(grid.Edifice[cell], Is.EqualTo(-1), $"a tree stands at {gx},{gz}, inside the start clearing");
                Assert.That(grid.IsWalkable(cell), Is.True, $"the clearing is not walkable at {gx},{gz}");
                Assert.That(TopOf(grid, gx, gz), Is.EqualTo(layer - 1), $"the clearing steps at {gx},{gz}");
            }
        }

        [Test]
        public void ItIsDeterministicFromTheSeed()
        {
            var (a, ao) = Generate(60, 60, 16, 7u);
            var (b, bo) = Generate(60, 60, 16, 7u);
            Assert.That(ao.Natural!.GridHash, Is.EqualTo(bo.Natural!.GridHash), "two runs of one seed disagree");
            Assert.That(ao.Natural!.Trees.Count, Is.EqualTo(bo.Natural!.Trees.Count));
            for (int i = 0; i < a.Edifice.Length; i++)
                Assert.That(a.Edifice[i], Is.EqualTo(b.Edifice[i]), $"cell {i} differs between two runs of one seed");
            for (int i = 0; i < a.Terrain.Length; i++)
                Assert.That(a.Terrain[i], Is.EqualTo(b.Terrain[i]), $"cell {i} differs between two runs of one seed");
        }
    }
}
