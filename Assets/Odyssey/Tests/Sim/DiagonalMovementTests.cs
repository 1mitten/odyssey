#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    [TestFixture]
    public class DiagonalMovementTests
    {
        [Test]
        public void StraightDiagonalPathTakesDirectStepsAndCalculatesDiagonalCost()
        {
            CellGrid cells = NavWorld.MakeCells(20, 20, 1);
            var nav = new NavGraph(cells);
            nav.Rebuild();
            var finder = new PathFinder(nav);

            int start = cells.Index(2, 2, 0);
            int goal = cells.Index(12, 12, 0);
            PathResult result = finder.FindPath(start, goal, TraverseMode.Colonist);

            Assert.That(result.Ok, Is.True);
            // 10 diagonal steps from (2,2) to (12,12) = 11 cells total in path
            Assert.That(result.Length, Is.EqualTo(11));
            // 10 steps * 141 = 1410
            Assert.That(result.Cost, Is.EqualTo(1410));

            int[] path = finder.PathToArray();
            for (int i = 1; i < path.Length; i++)
            {
                CellRef prev = cells.Size.FromIndex(path[i - 1]);
                CellRef curr = cells.Size.FromIndex(path[i]);
                Assert.That(Math.Abs(curr.X - prev.X), Is.EqualTo(1));
                Assert.That(Math.Abs(curr.Z - prev.Z), Is.EqualTo(1));
                Assert.That(curr.Y, Is.EqualTo(prev.Y));
            }
        }

        [Test]
        public void StrictCornerCuttingPreventsMovingDiagonallyAroundSolidWall()
        {
            CellGrid cells = NavWorld.MakeCells(10, 10, 1);
            var nav = new NavGraph(cells);

            // Place a solid wall at (3, 2)
            NavWorld.SetSolid(cells, nav, cells.Index(3, 2, 0), true);
            nav.Rebuild();

            // Try to move diagonally from (2, 2) to (3, 3).
            // Intermediate orthogonal cells are (3, 2) [WALL] and (2, 3) [FLOOR].
            // Because (3, 2) is solid, strict corner cutting forbids the direct diagonal step.
            Assert.That(nav.IsLegalStep(cells.Index(2, 2, 0), cells.Index(3, 3, 0), TraverseMode.Colonist), Is.False);

            // But moving around the wall via (2, 3) then to (3, 3) must be legal:
            Assert.That(nav.IsLegalStep(cells.Index(2, 2, 0), cells.Index(2, 3, 0), TraverseMode.Colonist), Is.True);
            Assert.That(nav.IsLegalStep(cells.Index(2, 3, 0), cells.Index(3, 3, 0), TraverseMode.Colonist), Is.True);

            var finder = new PathFinder(nav);
            PathResult path = finder.FindPath(cells.Index(2, 2, 0), cells.Index(3, 3, 0), TraverseMode.Colonist);
            Assert.That(path.Ok, Is.True);
            // Must step around the wall in 2 orthogonal steps (cost 200) rather than cutting the corner (141)
            Assert.That(path.Cost, Is.EqualTo(200));
            Assert.That(path.Length, Is.EqualTo(3)); // start, (2,3), (3,3)
        }

        [Test]
        public void StrictCornerCuttingPreventsSqueezingThroughDiagonalWallSeam()
        {
            CellGrid cells = NavWorld.MakeCells(10, 10, 1);
            var nav = new NavGraph(cells);

            // Place two diagonal walls meeting at a point: (3, 2) and (2, 3)
            NavWorld.SetSolid(cells, nav, cells.Index(3, 2, 0), true);
            NavWorld.SetSolid(cells, nav, cells.Index(2, 3, 0), true);
            nav.Rebuild();

            // Squeezing from (2, 2) to (3, 3) must be strictly forbidden
            Assert.That(nav.IsLegalStep(cells.Index(2, 2, 0), cells.Index(3, 3, 0), TraverseMode.Colonist), Is.False);
        }

        [Test]
        public void DiagonalEnterCostScalesTerrainExtraProportionally()
        {
            CellGrid cells = NavWorld.MakeCells(10, 10, 1);
            var nav = new NavGraph(cells);
            // Give class 1 a terrain extra cost of 50
            nav.Grid.CostByClass[1] = 50;
            nav.Grid.CostClass[cells.Index(5, 5, 0)] = 1;

            // Orthogonal cost = 100 + 50 = 150
            int orthoCost = nav.Grid.EnterCost(cells.Index(5, 5, 0), TraverseMode.Colonist, diagonal: false);
            Assert.That(orthoCost, Is.EqualTo(150));

            // Diagonal cost = 141 + (50 * 141 + 50) / 100 = 141 + 71 = 212
            int diagCost = nav.Grid.EnterCost(cells.Index(5, 5, 0), TraverseMode.Colonist, diagonal: true);
            Assert.That(diagCost, Is.EqualTo(212));
        }

        [Test]
        public void ReachabilityMatchesOracleWithDiagonalSteps()
        {
            CellGrid cells = NavWorld.MakeCells(15, 15, 1);
            var nav = new NavGraph(cells);
            var rng = new DeterministicRandom(12345u);

            // Scatter some solid obstacles
            for (int i = 0; i < cells.Size.CellCount; i++)
                if (rng.NextInt(100) < 25) cells.Flags[i] |= CellFlags.SolidTerrain;

            nav.Rebuild();

            int start = cells.Index(1, 1, 0);
            if (!nav.Grid.CanEnter(start, TraverseMode.Colonist))
            {
                cells.Flags[start] &= ~CellFlags.SolidTerrain;
                nav.Rebuild();
            }

            HashSet<int> flooded = NavWorld.FloodFromCell(nav, start, TraverseMode.Colonist);
            for (int i = 0; i < cells.Size.CellCount; i++)
            {
                bool oracle = flooded.Contains(i);
                bool district = nav.Reachable(start, i, TraverseMode.Colonist);
                Assert.That(district, Is.EqualTo(oracle),
                    $"Cell {cells.Size.FromIndex(i)}: district reachability ({district}) != oracle flood ({oracle})");
            }
        }
    }
}
