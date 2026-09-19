#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    public class AvoidancePathingTests
    {
        [Test]
        public void ParallelCorridors_PrefersClearCorridorOverOccupiedOne()
        {
            // Grid layout (x from 0 to 6, z from 0 to 4, y = 0):
            // z=0: Wall
            // z=1: Corridor A (x=1..5)
            // z=2: Wall divider between x=1..5, open at x=0 and x=6
            // z=3: Corridor B (x=1..5)
            // z=4: Wall
            // Start at (0, 2, 0), Goal at (6, 2, 0)
            // Both Corridor A and Corridor B are equal length.

            var cells = NavWorld.MakeCells(7, 5, 1);
            var nav = new NavGraph(cells);

            // Build boundary walls and central divider
            for (int x = 0; x < 7; x++)
            {
                NavWorld.SetSolid(cells, nav, cells.Index(x, 0, 0), true);
                NavWorld.SetSolid(cells, nav, cells.Index(x, 4, 0), true);
            }
            for (int x = 1; x <= 5; x++)
            {
                NavWorld.SetSolid(cells, nav, cells.Index(x, 2, 0), true);
            }

            nav.Rebuild();

            int start = cells.Index(0, 2, 0);
            int goal = cells.Index(6, 2, 0);

            var finder = new PathFinder(nav);

            // Case 1: Corridor A (z=1) is occupied at x=3
            int occA = cells.Index(3, 1, 0);
            finder.Occupancy = c => c == occA;

            var resA = finder.FindPath(start, goal, TraverseMode.Colonist);
            Assert.That(resA.Ok, Is.True);
            var pathA = finder.PathToArray();
            Assert.That(pathA, Has.No.Member(occA), "should route via Corridor B (z=3) to avoid occupied cell in Corridor A");

            // Case 2: Corridor B (z=3) is occupied at x=3
            int occB = cells.Index(3, 3, 0);
            finder.Occupancy = c => c == occB;

            var resB = finder.FindPath(start, goal, TraverseMode.Colonist);
            Assert.That(resB.Ok, Is.True);
            var pathB = finder.PathToArray();
            Assert.That(pathB, Has.No.Member(occB), "should route via Corridor A (z=1) to avoid occupied cell in Corridor B");
        }

        [Test]
        public void SingleCorridor_SoftAvoidanceTraversesOccupiedCell()
        {
            // Single 1-wide corridor from x=0 to x=6 at z=1.
            var cells = NavWorld.MakeCells(7, 3, 1);
            var nav = new NavGraph(cells);

            for (int x = 0; x < 7; x++)
            {
                NavWorld.SetSolid(cells, nav, cells.Index(x, 0, 0), true);
                NavWorld.SetSolid(cells, nav, cells.Index(x, 2, 0), true);
            }

            nav.Rebuild();

            int start = cells.Index(0, 1, 0);
            int goal = cells.Index(6, 1, 0);

            var finder = new PathFinder(nav);

            // Baseline cost when clear
            finder.Occupancy = null;
            var clearRes = finder.FindPath(start, goal, TraverseMode.Colonist);
            Assert.That(clearRes.Ok, Is.True);

            // Occupy middle cell (3, 1, 0)
            int occ = cells.Index(3, 1, 0);
            finder.Occupancy = c => c == occ;

            var occRes = finder.FindPath(start, goal, TraverseMode.Colonist);
            Assert.That(occRes.Ok, Is.True, "soft avoidance must not block single corridor");
            Assert.That(occRes.Cost, Is.EqualTo(clearRes.Cost + MoveCost.OccupiedBias),
                "cost should increase by exactly OccupiedBias");
            Assert.That(finder.PathToArray(), Contains.Item(occ), "path traverses the occupied cell");
        }

        [Test]
        public void GoalCellOccupied_PathStillFound()
        {
            var cells = NavWorld.MakeCells(5, 3, 1);
            var nav = new NavGraph(cells);
            nav.Rebuild();

            int start = cells.Index(0, 1, 0);
            int goal = cells.Index(4, 1, 0);

            var finder = new PathFinder(nav);
            finder.Occupancy = c => c == goal;

            var res = finder.FindPath(start, goal, TraverseMode.Colonist);
            Assert.That(res.Ok, Is.True);
            Assert.That(finder.PathToArray()[^1], Is.EqualTo(goal));
        }

        [Test]
        public void OccupiedBias_DoesNotCauseMassiveDetours()
        {
            // Corridor A (straight line x=0..6, z=1): 6 steps = 600 cost.
            // With occupied cell: 630 cost.
            // Detour via z=2 requires extra steps (at least 8 steps = 800 cost).
            // A* should choose Corridor A (630 < 800) rather than taking the expensive detour.
            var cells = NavWorld.MakeCells(7, 3, 1);
            var nav = new NavGraph(cells);
            // Open everywhere on z=1 and z=2 (z=0 is wall)
            for (int x = 0; x < 7; x++)
            {
                NavWorld.SetSolid(cells, nav, cells.Index(x, 0, 0), true);
            }
            // Put wall along z=2 except at endpoints x=0 and x=6
            for (int x = 1; x <= 5; x++)
            {
                NavWorld.SetSolid(cells, nav, cells.Index(x, 2, 0), true);
            }
            nav.Rebuild();

            int start = cells.Index(0, 1, 0);
            int goal = cells.Index(6, 1, 0);

            var finder = new PathFinder(nav);
            int occ = cells.Index(3, 1, 0);
            finder.Occupancy = c => c == occ;

            var res = finder.FindPath(start, goal, TraverseMode.Colonist);
            Assert.That(res.Ok, Is.True);
            Assert.That(finder.PathToArray(), Contains.Item(occ),
                "should accept +30 OccupiedBias rather than detour of +200 cost");
        }

        [Test]
        public void PawnRegistry_IsCellOccupiedByStandingPawn_DistinguishesStationaryFromMoving()
        {
            var cells = NavWorld.MakeCells(10, 10, 1);
            var nav = new NavGraph(cells);
            nav.Rebuild();

            var paths = new PathService(new PathFinder(nav));
            var ctx = new PawnContext(cells, nav, paths, ContentPack.Pawns());

            int cellA = cells.Index(2, 2, 0);
            int cellB = cells.Index(4, 4, 0);

            var p1 = ctx.Pawns.Spawn(cellA);
            var p2 = ctx.Pawns.Spawn(cellB);

            // Both start standing with no path
            Assert.That(ctx.Pawns.IsCellOccupiedByStandingPawn(cellA), Is.True);
            Assert.That(ctx.Pawns.IsCellOccupiedByStandingPawn(cellB), Is.True);
            Assert.That(ctx.Pawns.IsCellOccupiedByStandingPawn(cells.Index(0, 0, 0)), Is.False);

            // Simulate p1 moving (give it an active path)
            p1.AdoptPath(new[] { cellA, cellA + 1 }, 2);
            p1.PathIndex = 1;
            Assert.That(p1.HasPath, Is.True);

            // p1 is moving, so cellA is no longer considered occupied by a standing pawn
            Assert.That(ctx.Pawns.IsCellOccupiedByStandingPawn(cellA), Is.False);
            // p2 is still standing
            Assert.That(ctx.Pawns.IsCellOccupiedByStandingPawn(cellB), Is.True);
        }
    }
}
