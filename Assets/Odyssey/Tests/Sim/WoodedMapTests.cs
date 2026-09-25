#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The wooded board is what the scene loads. It gives up the barren board's "anything that is
    /// not grass is a bug", so what it keeps has to be said exactly: grass cover everywhere, a
    /// terraced surface, woodland, water, rock outcrops standing on the ground, ore inside the
    /// rock, and a flat clearing to start in.
    ///
    /// <para>This file was re-based twice on 2026-09-16, once on each side of a merge, and the two
    /// re-bases disagreed about the board. The mining work stopped
    /// <see cref="NaturalMapGenDef.MakeWooded"/> routing through
    /// <see cref="NaturalMapGenDef.MakeBarren"/>, so the board gained terracing, outcrops and ore;
    /// the water work added ponds, streams and bogs to what it believed was still a flat table.
    /// Both features are wanted and only the flatness was ever an assumption, so the water
    /// assertions here are stated <b>per column</b> rather than against one board-wide ground
    /// level. <c>BarrenMapTests</c> still holds the flat, empty baseline.</para>
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
        public void TheDrySurfaceIsTerracedAndCoveredInGrass()
        {
            var (grid, outcome) = Generate(120, 120, 16);
            var size = grid.Size;
            var ctx = outcome.Natural!.Context;
            var report = outcome.Natural!.Report;

            // Terracing, not hills: more than one step, and never more steps than the relief
            // allows either side of the nominal ground layer.
            Assert.That(report.SurfaceSpread, Is.GreaterThan(1), "the board is still a flat table");
            Assert.That(report.SurfaceSpread, Is.LessThanOrEqualTo(2 * 2 + 1), "the relief exceeded its band");

            for (int gz = 0; gz < size.SizeZ; gz++)
            for (int gx = 0; gx < size.SizeX; gx++)
            {
                int column = ctx.Column(gx, gz);
                if (ctx.Water[column] != (byte)WaterClass.None) continue;

                int top = TopOf(grid, gx, gz);
                ushort terrain = grid.Terrain[size.Index(gx, gz, top)];

                // Grass everywhere the cover pass touched, and rock where an outcrop stands on
                // it: those are the only two things that can be the top of a wooded column.
                Assert.That(terrain == NaturalContent.TerrainGrass || terrain == NaturalContent.TerrainRock,
                    Is.True, $"column {gx},{gz} is topped with terrain {terrain}, neither grass nor rock");
            }
        }

        /// <summary>
        /// The shape of the water: a channel is cut one layer below the ground it was cut from,
        /// holds one cell of water over a bed of sand or gravel, and its banks are the ordinary
        /// board. A bog is not a channel — it is wet ground that was never lowered, so it stays
        /// level with the grass around it.
        ///
        /// <para><b>Per column, not against one board-wide level.</b> This was written when the
        /// wooded board was a flat table and could compare every wet column against a single dry
        /// height. The board is terraced now, so that comparison means nothing; what is actually
        /// claimed — and what the pass actually does — is that a channel sits one step below its
        /// own dry neighbours.</para>
        /// </summary>
        [Test]
        public void WaterSitsOneStepBelowTheGroundItWasCutFrom()
        {
            var (grid, outcome) = Generate(120, 120, 16);
            var size = grid.Size;
            var ctx = outcome.Natural!.Context;

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
                    Assert.That(bed, Is.EqualTo(NaturalContent.TerrainMarsh));
                    marsh++;
                    continue;
                }

                Assert.That(NaturalContent.IsGround(bed), Is.True, $"the bed at {gx},{gz} is not ground");

                ushort here = grid.Terrain[size.Index(gx, gz, surface + 1)];
                Assert.That(here, Is.EqualTo(kind == WaterClass.Shallow
                    ? NaturalContent.TerrainShallowWater
                    : NaturalContent.TerrainDeepWater), $"the cell at {gx},{gz} is not the water it was planned as");

                // One step down from a dry neighbour, wherever this column's own ground sits.
                // A channel with no dry neighbour at all is the middle of a wide river, which is
                // allowed; what is not is a channel level with the bank beside it.
                bool cutIn = true, sawDry = false;
                foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = gx + d.Item1, nz = gz + d.Item2;
                    if (!size.Contains(nx, nz, 0)) continue;
                    int neighbour = ctx.Column(nx, nz);
                    if (ctx.Water[neighbour] != (byte)WaterClass.None) continue;
                    sawDry = true;
                    if (ctx.SurfaceY[neighbour] <= surface) cutIn = false;
                }

                Assert.That(!sawDry || cutIn, Is.True,
                    $"the channel at {gx},{gz} is level with the dry ground beside it");
                water++;
            }

            Assert.That(water, Is.GreaterThan(0), "the wooded board has no water at all");
            Assert.That(marsh, Is.GreaterThan(0), "water with no bog around it");
        }

        [Test]
        public void EveryTreeStandsOnItsOwnGroundAndBlocksNothing()
        {
            // A tree sits in the air cell above the ground it grows from and a colonist walks
            // through it (NaturalContent.EdificeTreeBirch): woodland is a supply of wood, not
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

        /// <summary>
        /// **No tree grows at the foot of a terrace step.** Presentation fills that cell with a
        /// bank — a wedge of hillside spilling down from the ground above — and a tree standing in
        /// one is sheared off by it, which is what the owner reported on 2026-09-18.
        ///
        /// <para>Stated over the generated board rather than over a hand-built step, because the
        /// interesting cases are the ones nobody would think to build: the cell diagonally off a
        /// convex corner, the column beside an outcrop that happens to be exactly one layer proud,
        /// the grass beside a stream channel. <c>TerraceFootTests</c> is the other half — it is what
        /// says this guard and the mesher mean the same thing by a step.</para>
        /// </summary>
        [Test]
        public void NoTreeStandsAtTheFootOfATerraceStep()
        {
            var (grid, outcome) = Generate(120, 120, 16);

            foreach (var tree in outcome.Natural!.Trees)
                Assert.That(TerraceFoot.IsFoot(grid, tree.CellIndex), Is.False,
                    $"a tree stands at cell {tree.CellIndex}, at the foot of a terrace step, " +
                    "where the bank will shear it off");
        }

        /// <summary>
        /// And the guard is doing something. A guard that never fires is indistinguishable from no
        /// guard at all, and this is the number to read if the wood ever looks thinned: on the
        /// played board it is a few dozen out of some thousands.
        /// </summary>
        [Test]
        public void TheTerraceGuardRefusesSomeTrees()
        {
            var (_, outcome) = Generate(120, 120, 16);
            var report = outcome.Natural!.Report;

            Assert.That(report.TreesRefusedOnTerraceSteps, Is.GreaterThan(0),
                "no tree was refused on a terraced, wooded board, so the guard is not running");
            Assert.That(report.TreesRefusedOnTerraceSteps, Is.LessThan(report.Trees),
                "the guard took more of the wood than it left, which is not a guard but a felling");
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
