#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Rock nobody has opened belongs to no navigation region (design 62 §2c, DM1).
    ///
    /// <para>The rule is one line in <see cref="NavGrid.KindOf"/>, and what it must not do is as
    /// important as what it does: a wall keeps its <see cref="RegionKind.Impassable"/> region (the
    /// conservative half — a room pass may want walls), a mined cell gets a region at once, and a
    /// region seeded on a wall never grows into the rock beside it. The whole-board form of the
    /// rule is asserted by <c>NavGraphStatisticsTests</c> over every cell of every offered
    /// board.</para>
    /// </summary>
    public class UnopenedRockTests
    {
        [Test]
        public void SolidGroundIsInNoRegionAndAWallStillIs()
        {
            var cells = NavWorld.MakeCells(10, 10, 2);
            int rock = cells.Index(2, 2, 0);
            int wall = cells.Index(6, 6, 0);
            cells.Flags[rock] |= CellFlags.SolidTerrain;
            cells.Flags[wall] |= CellFlags.BlockingEdifice;

            var nav = new NavGraph(cells);
            nav.Rebuild();

            Assert.That(nav.RegionOfCell(rock), Is.LessThan(0), "unopened rock carried a region");
            int wallRegion = nav.RegionOfCell(wall);
            Assert.That(wallRegion, Is.GreaterThanOrEqualTo(0), "a wall lost its region, which DM1 left alone");
            Assert.That(nav.KindOfRegion(wallRegion), Is.EqualTo(RegionKind.Impassable));
        }

        [Test]
        public void AWallsRegionDoesNotGrowIntoTheRockBesideIt()
        {
            var cells = NavWorld.MakeCells(10, 10, 1);
            int wall = cells.Index(4, 4, 0);
            int rock = cells.Index(5, 4, 0);
            cells.Flags[wall] |= CellFlags.BlockingEdifice;
            cells.Flags[rock] |= CellFlags.SolidTerrain;

            var nav = new NavGraph(cells);
            nav.Rebuild();

            int r = nav.RegionOfCell(wall);
            Assert.That(nav.RegionCellCount(r), Is.EqualTo(1), "the wall's region swallowed the rock beside it");
            Assert.That(nav.RegionOfCell(rock), Is.LessThan(0));
        }

        [Test]
        public void MiningARockCellGivesItARegionOnTheNextRebuild()
        {
            // Two layers: the rock sits on the lower one with more rock under the upper floor, so a
            // cleared cell has ground to stand on and becomes walkable.
            var cells = NavWorld.MakeCells(10, 10, 2);
            for (int x = 0; x < 10; x++)
            for (int z = 0; z < 10; z++)
                cells.Flags[cells.Index(x, z, 0)] |= CellFlags.SolidTerrain;

            var nav = new NavGraph(cells);
            nav.Rebuild();
            int cut = cells.Index(3, 3, 0);
            Assert.That(nav.RegionOfCell(cut), Is.LessThan(0));

            NavWorld.SetSolid(cells, nav, cut, false);
            nav.Rebuild();

            Assert.That(nav.RegionOfCell(cut), Is.GreaterThanOrEqualTo(0), "an opened cell stayed out of every region");
            Assert.That(nav.KindOfRegion(nav.RegionOfCell(cut)), Is.EqualTo(RegionKind.Walkable));
        }

        /// <summary>
        /// The measurement control restores the old rule exactly — or every before-and-after figure
        /// in <c>28-map-size.md</c> §12 compares the new rule with itself.
        /// </summary>
        [Test]
        public void TheControlPutsTheRockBackInARegion()
        {
            var cells = NavWorld.MakeCells(10, 10, 1);
            int rock = cells.Index(2, 2, 0);
            cells.Flags[rock] |= CellFlags.SolidTerrain;

            var nav = new NavGraph(cells);
            nav.Grid.SolidTerrainHasRegions = true;
            nav.Rebuild();

            int r = nav.RegionOfCell(rock);
            Assert.That(r, Is.GreaterThanOrEqualTo(0));
            Assert.That(nav.KindOfRegion(r), Is.EqualTo(RegionKind.Impassable));
        }

        [Test]
        public void ReachabilityIntoRockIsStillNo()
        {
            var cells = NavWorld.MakeCells(10, 10, 1);
            int rock = cells.Index(5, 5, 0);
            cells.Flags[rock] |= CellFlags.SolidTerrain;
            var nav = new NavGraph(cells);
            nav.Rebuild();

            int open = cells.Index(1, 1, 0);
            Assert.That(nav.Reachable(open, rock, TraverseMode.Colonist), Is.False);
            Assert.That(nav.DistrictOfCell(rock, TraverseMode.Colonist), Is.EqualTo(-1));
            // A solid goal was refused before any region was asked, and still is.
            var finder = new PathFinder(nav);
            PathResult result = finder.FindPath(open, rock, TraverseMode.Colonist, new PathOptions(20_000, 20_000));
            Assert.That(result.Status, Is.EqualTo(PathStatus.InvalidRequest));
        }
    }
}
