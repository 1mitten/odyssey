#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What a colonist may do with height (owner, 2026-09-16).
    ///
    /// <para><b>The whole rule: one block up is a jump, one block down is a drop, and anything
    /// deeper wants a ladder.</b> It replaced climbing, which was a declared edge up a rock face
    /// and produced all three of the playtest reports at once — colonists stuck on faces more than
    /// one block high, climbing where nothing should be climbed, and repeating a failing route for
    /// ever. The mechanism was a climb that landed in a cell with no floor: from there nothing was
    /// walkable, so the pawn let go, fell back where it started, re-planned the same route and went
    /// again.</para>
    ///
    /// <para>The tests below pin the rule from both sides: what a jump can do, and what nothing can
    /// do any more.</para>
    /// </summary>
    public class VerticalMovementTests
    {
        static readonly GridSize Size = new GridSize(12, 12, 6);

        /// <summary>Solid to <paramref name="groundLayer"/> everywhere; a colonist stands above it.</summary>
        static CellGrid FlatBoard(int groundLayer)
        {
            var grid = new CellGrid(Size);
            for (int y = 0; y <= groundLayer; y++)
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
                Solid(grid, Size.Index(x, z, y));
            return grid;
        }

        static void Solid(CellGrid grid, int index)
        {
            grid.Terrain[index] = CoreContent.TerrainRock;
            grid.Flags[index] |= CellFlags.SolidTerrain;
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

        /// <summary>Ground one or more blocks higher, filling half the board from x = 6.</summary>
        static CellGrid StepBoard(int stepHeight)
        {
            CellGrid grid = FlatBoard(groundLayer: 0);
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 6; x < Size.SizeX; x++)
            for (int y = 1; y <= stepHeight; y++)
                Solid(grid, Size.Index(x, z, y));
            return grid;
        }

        [Test]
        public void AColonistJumpsUpOneBlockAndDropsBackDown()
        {
            NavGraph nav = GraphOf(StepBoard(stepHeight: 1));

            int below = Size.Index(5, 6, 1);   // on the low ground, against the step
            int above = Size.Index(6, 6, 2);   // on top of the step, next door

            Assert.That(nav.IsLegalStep(below, above, TraverseMode.Colonist), Is.True,
                "a colonist cannot jump up one block");
            Assert.That(nav.IsLegalStep(above, below, TraverseMode.Colonist), Is.True,
                "a colonist cannot drop down one block");
        }

        [Test]
        public void AJumpCostsMoreThanAStepAndADropCostsLess()
        {
            CellRef low = Size.FromIndex(Size.Index(5, 6, 1));
            CellRef high = Size.FromIndex(Size.Index(6, 6, 2));

            Assert.That(NavGraph.HopCost(low, high), Is.EqualTo(MoveCost.JumpUp));
            Assert.That(NavGraph.HopCost(high, low), Is.EqualTo(MoveCost.Drop));
            Assert.That(MoveCost.JumpUp, Is.GreaterThan(MoveCost.Orthogonal),
                "a jump became as cheap as walking, so nothing will ever prefer a stair");
        }

        /// <summary>
        /// A route over the step exists. There is no way round on this board, so a path at all is
        /// the proof that the jump is planned and not merely legal.
        /// </summary>
        [Test]
        public void ARouteCanCrossAOneBlockStep()
        {
            NavGraph nav = GraphOf(StepBoard(stepHeight: 1));
            var finder = new PathFinder(nav);

            PathResult result = finder.FindPath(
                Size.Index(2, 6, 1), Size.Index(9, 6, 2), TraverseMode.Colonist);

            Assert.That(result.Status, Is.EqualTo(PathStatus.Success),
                "nothing can get onto a one-block step");
        }

        /// <summary>
        /// And the reachability half, which is the one that decides whether a job is ever offered:
        /// every work-giver scan gates on the district comparison, so a jump the cell search can
        /// plan but the region graph does not know about is a job nobody is ever given.
        /// </summary>
        [Test]
        public void AOneBlockStepIsReachableAndNotJustPathable()
        {
            NavGraph nav = GraphOf(StepBoard(stepHeight: 1));

            Assert.That(nav.Reachable(Size.Index(2, 6, 1), Size.Index(9, 6, 2), TraverseMode.Colonist),
                Is.True, "the region graph does not carry the jump, so no job across it is ever offered");
        }

        [Test]
        public void ATwoBlockFaceIsAWall()
        {
            NavGraph nav = GraphOf(StepBoard(stepHeight: 2));
            var finder = new PathFinder(nav);

            int below = Size.Index(5, 6, 1);
            int above = Size.Index(6, 6, 3);

            Assert.That(nav.IsLegalStep(below, above, TraverseMode.Colonist), Is.False,
                "a colonist got up a two-block face in one step");
            Assert.That(finder.FindPath(Size.Index(2, 6, 1), above, TraverseMode.Colonist).Status,
                Is.EqualTo(PathStatus.Unreachable),
                "a colonist planned a route up a two-block face");
        }

        /// <summary>
        /// <b>The bug that produced all three reports, now impossible.</b> No move lands anywhere
        /// without a floor, so a pawn can never be left hanging — which is what it did before,
        /// followed by letting go, falling back and planning the same route again.
        /// </summary>
        [Test]
        public void NothingCanStepIntoACellWithNoFloor()
        {
            CellGrid grid = FlatBoard(groundLayer: 2);
            int shaft = Size.Index(6, 6, 1);
            Dig(grid, shaft);
            Dig(grid, shaft + Size.LayerStride);
            NavGraph nav = GraphOf(grid);

            int midAir = shaft + Size.LayerStride;   // one below the surface, nothing under it
            Assert.That(grid.HasFloor(midAir), Is.False, "the fixture is wrong: that cell has a floor");

            var into = new List<int>();
            CellRef at = Size.FromIndex(midAir);
            for (int dy = -1; dy <= 1; dy++)
            foreach ((int dx, int dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (0, 0) })
            {
                if (dx == 0 && dz == 0 && dy == 0) continue;
                if (!Size.Contains(at.X + dx, at.Z + dz, at.Y + dy)) continue;
                int from = Size.Index(at.X + dx, at.Z + dz, at.Y + dy);
                if (nav.IsLegalStep(from, midAir, TraverseMode.Colonist)) into.Add(from);
            }

            Assert.That(into, Is.Empty, "something can still step into a cell with nothing under it");
        }

        /// <summary>
        /// The mining half of "a ladder is required for two deep": the work giver will not hand
        /// out a cut whose hole nobody could jump out of.
        /// </summary>
        [Test]
        public void ACutThatWouldLeaveAnUnleavableHoleIsRefused()
        {
            CellGrid grid = FlatBoard(groundLayer: 2);

            // One cell already dug: its floor is one below the surface, which is a bench.
            Dig(grid, Size.Index(6, 6, 2));

            Assert.That(DesignationGrid.CanBeLeftAfterCutting(grid, Size.Index(7, 6, 2)), Is.True,
                "widening a bench was refused: there is ground beside it to step out onto");
            Assert.That(DesignationGrid.CanBeLeftAfterCutting(grid, Size.Index(6, 6, 1)), Is.False,
                "deepening the hole to two blocks was allowed, and it strands whoever digs it");
        }

        [Test]
        public void ABenchCanAlwaysBeLeft()
        {
            CellGrid grid = FlatBoard(groundLayer: 2);
            int bench = Size.Index(6, 6, 2);
            Dig(grid, bench);
            NavGraph nav = GraphOf(grid);

            Assert.That(nav.IsLegalStep(bench, Size.Index(7, 6, 3), TraverseMode.Colonist), Is.True,
                "a colonist cannot jump out of a one-block bench, which is the only hole it may dig");
        }
    }
}
