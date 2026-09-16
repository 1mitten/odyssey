#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What a colonist may do with height, and what it must never do (the owner's playtest,
    /// 2026-09-16).
    ///
    /// <para>Three reports, which turn out to be one mechanism: colonists try to get up faces more
    /// than one block high and stick there; they climb in places where nothing should be climbed;
    /// and they repeat the same failing route for ever. The tests below reproduce it before
    /// anything is changed.</para>
    /// </summary>
    public class VerticalMovementTests
    {
        static readonly GridSize Size = new GridSize(12, 12, 6);

        /// <summary>
        /// Solid ground up to and including <paramref name="groundLayer"/> everywhere, air above:
        /// a flat board with a floor a colonist can stand on at <c>groundLayer + 1</c>.
        /// </summary>
        static CellGrid FlatBoard(int groundLayer)
        {
            var grid = new CellGrid(Size);
            for (int y = 0; y <= groundLayer; y++)
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            {
                int index = Size.Index(x, z, y);
                grid.Terrain[index] = CoreContent.TerrainRock;
                grid.Flags[index] |= CellFlags.SolidTerrain;
            }
            return grid;
        }

        static void Fill(CellGrid grid, int x, int z, int fromY, int toY)
        {
            for (int y = fromY; y <= toY; y++)
            {
                int index = Size.Index(x, z, y);
                grid.Terrain[index] = CoreContent.TerrainRock;
                grid.Flags[index] |= CellFlags.SolidTerrain;
            }
        }

        static void Dig(CellGrid grid, int index)
        {
            grid.Terrain[index] = CoreContent.TerrainAir;
            grid.Flags[index] &= ~CellFlags.SolidTerrain;
        }

        static NavGraph GraphOf(CellGrid grid)
        {
            var nav = new NavGraph(grid);
            nav.Rebuild();
            return nav;
        }

        /// <summary>
        /// A step of one block is the most a colonist may take. Two blocks is a wall.
        ///
        /// <para>This is the rule the owner asked for in as many words, and the first half of it
        /// already holds: without a connector there is no vertical edge at all, so a two-block face
        /// is unreachable. It is pinned here so that adding the jump cannot quietly add a
        /// two-block jump with it.</para>
        /// </summary>
        [Test]
        public void ATwoBlockFaceIsNotClimbable()
        {
            CellGrid grid = FlatBoard(groundLayer: 0);
            // A plateau two blocks higher, filling half the board.
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 6; x < Size.SizeX; x++)
                Fill(grid, x, z, 1, 2);

            NavGraph nav = GraphOf(grid);
            var finder = new PathFinder(nav);

            int from = Size.Index(2, 6, 1);      // on the low ground
            int to = Size.Index(9, 6, 3);        // on top of the plateau
            PathResult result = finder.FindPath(from, to, TraverseMode.Colonist);

            Assert.That(result.Status, Is.EqualTo(PathStatus.Unreachable),
                "a colonist planned a route up a two-block face");
        }

        /// <summary>
        /// <b>The bug, reproduced.</b> A shaft whose only way out is a climb straight up puts the
        /// colonist in a cell with no floor — <c>NavFlags.ClimbOnly</c> — from which there is
        /// nowhere to walk. The route is planned, taken, and ends in mid-air.
        ///
        /// <para>This is what the owner saw as "climbing in places where there shouldn't be",
        /// and it is the same mechanism as the stuck-at-height report: the cell above a climb is
        /// standable and not walkable, so a pawn that reaches it has no legal step at all.</para>
        /// </summary>
        [Test]
        public void AClimbCanEndInMidAirWithNowhereToGo()
        {
            // TWO blocks deep, which is the case the owner reported. Rock to layer 2, so the
            // surface a colonist walks on is layer 3; dig out layers 1 and 2 of one column and
            // the floor of the shaft is layer 1, two below the surface.
            CellGrid grid = FlatBoard(groundLayer: 2);
            int hole = Size.Index(6, 6, 1);
            Dig(grid, hole);
            Dig(grid, hole + Size.LayerStride);

            NavGraph nav = GraphOf(grid);

            // The straight-up climb MineJob.ClimbOutOf falls back to when nothing can be stepped
            // off onto: from the floor of the hole into the air above it.
            int above = hole + Size.LayerStride;
            int id = nav.EnsureClimb(hole, above);
            nav.Rebuild();

            Assert.That(id, Is.GreaterThanOrEqualTo(0), "the climb was not created, so this test proves nothing");
            Assert.That(nav.Grid.IsClimbOnly(above), Is.True,
                "the cell above the climb should be standable-but-not-walkable");

            // From that cell, is there anywhere at all to go?
            var neighbours = new List<int>();
            CellRef at = Size.FromIndex(above);
            foreach ((int dx, int dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                if (!Size.Contains(at.X + dx, at.Z + dz, at.Y)) continue;
                int side = Size.Index(at.X + dx, at.Z + dz, at.Y);
                if (nav.IsLegalStep(above, side, TraverseMode.Colonist)) neighbours.Add(side);
            }

            Assert.That(neighbours, Is.Empty,
                "this test exists because the top of a straight-up climb has nowhere to walk to; " +
                "if that has changed, the bug it reproduces is fixed and the test should be rewritten");
        }

        /// <summary>
        /// And the consequence: a pawn that gets there is put back where it started, so the same
        /// route is planned again, taken again and fails again — the loop the owner watched.
        /// </summary>
        [Test]
        public void AClimbToNowhereReturnsThePawnToWhereItStarted()
        {
            CellGrid grid = FlatBoard(groundLayer: 2);
            int hole = Size.Index(6, 6, 1);
            Dig(grid, hole);
            Dig(grid, hole + Size.LayerStride);

            NavGraph nav = GraphOf(grid);
            int above = hole + Size.LayerStride;
            nav.EnsureClimb(hole, above);
            nav.Rebuild();

            // The climb is legal, so a pawn will take it...
            Assert.That(nav.IsLegalStep(hole, above, TraverseMode.Colonist), Is.True);

            // ...and the only thing under the cell it lands in is the hole it came from.
            Assert.That(grid.FirstFloorAtOrBelow(above), Is.EqualTo(hole),
                "letting go of the climb drops the pawn back into the hole it climbed out of, " +
                "which is the loop: climb, hang, drop, re-plan, climb");
        }
    }
}
