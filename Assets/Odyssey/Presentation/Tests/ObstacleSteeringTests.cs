#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    public class ObstacleSteeringTests
    {
        static PawnView MakePawn(int id, CellRef cell, CellRef next, int movePercent) =>
            new PawnView(new PawnId(id), cell, 100, 100, 50, -1, next, movePercent);

        [Test]
        public void ApproachingTree_VeersRightInAnticipation()
        {
            var world = new RenderTestWorld(5, 5, 2);
            // Place a conifer tree at (2, 2, 0)
            world.Edifice(2, 2, 0, NaturalContent.EdificeTreeConifer, blocking: false);
            world.Publish();

            Assert.That(world.Model.HasObstacle(new CellRef(2, 2, 0)), Is.True);

            // Pawn moving North from (2, 1, 0) towards (2, 2, 0)
            CellRef approaching = new CellRef(2, 1, 0);
            CellRef treeCell = new CellRef(2, 2, 0);

            // In first half of approaching cell, stay near centre
            var pEarly = MakePawn(1, approaching, treeCell, 20);
            Vector3 posEarly = PawnPose.Of(pEarly, 0f, 0, out _, world.Model);
            float baseEarlyX = CellMetrics.FloorCentre(approaching).x;
            Assert.That(Mathf.Abs(posEarly.x - baseEarlyX), Is.LessThan(0.05f),
                "in early approaching cell, figure should stay on center");

            // In second half of approaching cell, anticipation smoothly ramps up lateral offset
            var pMid = MakePawn(1, approaching, treeCell, 75);
            Vector3 posMid = PawnPose.Of(pMid, 0f, 0, out _, world.Model);
            Assert.That(posMid.x - baseEarlyX, Is.GreaterThan(0.2f),
                "in late approaching cell, anticipation must start veering to the right");

            // Near boundary, figure is already offset to the side
            var pArriving = MakePawn(1, approaching, treeCell, 99);
            Vector3 posArriving = PawnPose.Of(pArriving, 0f, 0, out _, world.Model);
            Assert.That(posArriving.x - baseEarlyX, Is.GreaterThanOrEqualTo(0.55f),
                "by boundary crossing, figure must have veered to the side of the tile");
        }

        [Test]
        public void InsideTreeCell_MaintainsOffsetAroundTrunk()
        {
            var world = new RenderTestWorld(5, 5, 2);
            world.Edifice(2, 2, 0, NaturalContent.EdificeTreeBroadleaf, blocking: false);
            world.Publish();

            CellRef treeCell = new CellRef(2, 2, 0);
            CellRef exitCell = new CellRef(2, 3, 0);

            // Crossing through tree cell
            var pInTree = MakePawn(1, treeCell, exitCell, 50);
            Vector3 posInTree = PawnPose.Of(pInTree, 0f, 0, out _, world.Model);

            float baseCenterX = CellMetrics.FloorCentre(treeCell).x;
            float offsetFromTrunk = posInTree.x - baseCenterX;

            Assert.That(offsetFromTrunk, Is.GreaterThanOrEqualTo(0.50f),
                "figure must walk around the center tree trunk");
            Assert.That(offsetFromTrunk, Is.LessThanOrEqualTo(SteeringCurve.HardClampedMax),
                "figure must remain within tile bounds (< 0.75 m)");
        }
    }
}
