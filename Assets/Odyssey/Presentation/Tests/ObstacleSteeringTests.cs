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
        public void CrossingTreeCell_VeersAroundTrunkAndReturnsSeamlessly()
        {
            var world = new RenderTestWorld(5, 5, 2);
            world.Edifice(2, 2, 0, NaturalContent.EdificeTreeMeadow, blocking: false);
            world.Publish();

            Assert.That(world.Model.HasObstacle(new CellRef(2, 2, 0)), Is.True);

            CellRef treeCell = new CellRef(2, 2, 0);
            CellRef exitCell = new CellRef(2, 3, 0);
            float baseCenterX = CellMetrics.FloorCentre(treeCell).x;

            // At entry (0%), offset is zero (seamless boundary transition)
            var pEntry = MakePawn(1, treeCell, exitCell, 0);
            Vector3 posEntry = PawnPose.Of(pEntry, 0f, 0, out _, world.Model);
            Assert.That(posEntry.x - baseCenterX, Is.EqualTo(0f).Within(1e-4f),
                "at cell boundary entry, offset must be exactly zero to prevent position jolts");

            // In first quarter, starts veering before reaching trunk
            var pQuarter = MakePawn(1, treeCell, exitCell, 25);
            Vector3 posQuarter = PawnPose.Of(pQuarter, 0f, 0, out _, world.Model);
            Assert.That(posQuarter.x - baseCenterX, Is.GreaterThan(0.3f),
                "figure starts veering to the side before reaching the central tree trunk");

            // At midpoint (abreast of trunk), reaches peak offset
            var pMid = MakePawn(1, treeCell, exitCell, 50);
            Vector3 posMid = PawnPose.Of(pMid, 0f, 0, out _, world.Model);
            float offsetFromTrunk = posMid.x - baseCenterX;
            Assert.That(offsetFromTrunk, Is.GreaterThanOrEqualTo(0.55f),
                "figure must walk to the side of the tile around the central tree trunk");
            Assert.That(offsetFromTrunk, Is.LessThanOrEqualTo(SteeringCurve.HardClampedMax),
                "figure must remain within tile bounds (< 0.75 m)");

            // At exit (100%), offset returns to zero (seamless transition to next cell)
            var pExit = MakePawn(1, treeCell, exitCell, 100);
            Vector3 posExit = PawnPose.Of(pExit, 0f, 0, out _, world.Model);
            Assert.That(posExit.x - baseCenterX, Is.EqualTo(0f).Within(1e-4f),
                "at cell boundary exit, offset must return to zero to prevent position jolts");
        }

        [Test]
        public void StepTransition_IntoAndOutOfTreeCell_IsContinuous()
        {
            var world = new RenderTestWorld(5, 5, 2);
            world.Edifice(2, 2, 0, NaturalContent.EdificeTreeMeadow, blocking: false);
            world.Publish();

            CellRef approachCell = new CellRef(2, 1, 0);
            CellRef treeCell = new CellRef(2, 2, 0);
            CellRef exitCell = new CellRef(2, 3, 0);

            // End of approach step (s = 100%)
            var pApproachEnd = MakePawn(1, approachCell, treeCell, 100);
            Vector3 posApproachEnd = PawnPose.Of(pApproachEnd, 0f, 0, out _, world.Model);

            // Start of tree step (s = 0%)
            var pTreeStart = MakePawn(1, treeCell, exitCell, 0);
            Vector3 posTreeStart = PawnPose.Of(pTreeStart, 0f, 0, out _, world.Model);

            Assert.That(posTreeStart, Is.EqualTo(posApproachEnd).Within(1e-4f),
                "position across approach -> treeCell boundary must be perfectly continuous (zero jump)");

            // End of tree step (s = 100%)
            var pTreeEnd = MakePawn(1, treeCell, exitCell, 100);
            Vector3 posTreeEnd = PawnPose.Of(pTreeEnd, 0f, 0, out _, world.Model);

            // Start of exit step (s = 0%)
            var pExitStart = MakePawn(1, exitCell, new CellRef(2, 4, 0), 0);
            Vector3 posExitStart = PawnPose.Of(pExitStart, 0f, 0, out _, world.Model);

            Assert.That(posExitStart, Is.EqualTo(posTreeEnd).Within(1e-4f),
                "position across treeCell -> exitCell boundary must be perfectly continuous (zero jump)");
        }

        [Test]
        public void DiagonalStep_PastCornerTree_SteersAwayFromObstacleCorner()
        {
            var world = new RenderTestWorld(5, 5, 2);
            // Tree at corner (2, 3, 0)
            world.Edifice(2, 3, 0, NaturalContent.EdificeTreeMeadow, blocking: false);
            world.Publish();

            // Pawn moving diagonally from (2, 2, 0) to (3, 3, 0)
            CellRef start = new CellRef(2, 2, 0);
            CellRef goal = new CellRef(3, 3, 0);

            var pMid = MakePawn(1, start, goal, 50);
            Vector3 midPos = PawnPose.Of(pMid, 0f, 0, out _, world.Model);

            // Nominal midpoint of the diagonal step is (2.5, 2.5) * CellMetrics.Size
            Vector3 nominalMid = (CellMetrics.FloorCentre(start) + CellMetrics.FloorCentre(goal)) * 0.5f;

            // Since the tree is at (2, 3), deflection must push pawn towards (+X, -Z), away from (2, 3)
            Assert.That(midPos, Is.Not.EqualTo(nominalMid),
                "diagonal move past a corner obstacle must deflect away from the corner tree");
            Assert.That(Vector3.Distance(midPos, nominalMid), Is.GreaterThan(0.3f),
                "deflection away from corner tree must provide substantial clearance");
        }

        [Test]
        public void TreeObstacle_OnLowerTerraceLayer_IsDetectedAtColonistLayer()
        {
            var world = new RenderTestWorld(5, 5, 3);
            world.Edifice(2, 2, 0, NaturalContent.EdificeTreeMeadow, blocking: false);
            world.Publish();

            // Tree is rooted at Y=0, colonist is walking on layer Y=1
            Assert.That(world.Model.HasObstacle(new CellRef(2, 2, 0)), Is.True, "tree in its own cell");
            Assert.That(world.Model.HasObstacle(new CellRef(2, 2, 1)), Is.True, "tree extends up to layer above");
        }
    }
}
