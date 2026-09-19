#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    public class PawnPassingTests
    {
        static PawnView MakePawn(int id, CellRef cell, CellRef next, int movePercent) =>
            new PawnView(new PawnId(id), cell, 100, 100, 50, -1, next, movePercent);

        [Test]
        public void OpposingPawnsInCorridor_MaintainAtLeastOneMeterSeparation()
        {
            // Pawn 1 moving North (0, 0, 0) -> (0, 0, 1)
            // Pawn 2 moving South (0, 0, 1) -> (0, 0, 0)
            // Corridor along Z axis, width 2.5 m (X span is [0, 2.5], centreline at X=1.25).

            CellRef southCell = new CellRef(0, 0, 0);
            CellRef northCell = new CellRef(0, 1, 0);
            float baseX = CellMetrics.FloorCentre(southCell).x; // 1.25 m

            float minLateralDistance = float.MaxValue;
            float maxOffsetFromCenter = 0f;

            // Sample across passing encounter
            for (int percent = 10; percent <= 90; percent += 5)
            {
                var p1 = MakePawn(1, southCell, northCell, percent);
                var p2 = MakePawn(2, northCell, southCell, percent);
                var pair = new[] { p1, p2 };

                Vector3 pos1 = PawnPose.Of(p1, 0f, 0, out _, null, pair);
                Vector3 pos2 = PawnPose.Of(p2, 0f, 0, out _, null, pair);

                // Pawn 1 moves North: right is East (+X)
                Assert.That(pos1.x, Is.GreaterThanOrEqualTo(baseX - 1e-4f), "Pawn 1 should veer to its right (+X)");
                // Pawn 2 moves South: right is West (-X)
                Assert.That(pos2.x, Is.LessThanOrEqualTo(baseX + 1e-4f), "Pawn 2 should veer to its right (-X)");

                float lateralDist = pos1.x - pos2.x;
                if (lateralDist < minLateralDistance) minLateralDistance = lateralDist;

                maxOffsetFromCenter = Mathf.Max(maxOffsetFromCenter, Mathf.Abs(pos1.x - baseX), Mathf.Abs(pos2.x - baseX));
            }

            // At peak passing (midpoint), separation should be at least 1.0 m (nominally 1.2 m)
            var mid1 = MakePawn(1, southCell, northCell, 50);
            var mid2 = MakePawn(2, northCell, southCell, 50);
            var midPair = new[] { mid1, mid2 };
            Vector3 midPos1 = PawnPose.Of(mid1, 0f, 0, out _, null, midPair);
            Vector3 midPos2 = PawnPose.Of(mid2, 0f, 0, out _, null, midPair);
            float peakSeparation = midPos1.x - midPos2.x;

            Assert.That(peakSeparation, Is.GreaterThanOrEqualTo(1.0f),
                "passing pawns must maintain at least 1.0 m separation when abreast");
            Assert.That(peakSeparation, Is.EqualTo(1.20f).Within(0.05f),
                "passing pawns should achieve ~1.20 m mutual separation");

            // Pawns must strictly stay within 2.5 m corridor bounds (half-width 1.25 m)
            Assert.That(maxOffsetFromCenter, Is.LessThan(1.25f),
                "pawns must not escape the 2.5 m corridor tile bounds");
            Assert.That(maxOffsetFromCenter, Is.LessThanOrEqualTo(SteeringCurve.HardClampedMax),
                "lateral offset must respect HardClampedMax (0.75 m)");
        }

        [Test]
        public void SinglePawnAlone_MaintainsCellCentre()
        {
            CellRef from = new CellRef(0, 0, 0);
            CellRef to = new CellRef(0, 1, 0);
            float baseX = CellMetrics.FloorCentre(from).x;
            var p1 = MakePawn(1, from, to, 50);

            // Alone: no other pawns passed
            Vector3 posAlone = PawnPose.Of(p1, 0f, 0, out _, null);
            Assert.That(posAlone.x, Is.EqualTo(baseX).Within(1e-4f),
                "alone pawn should stay on cell centreline");
        }

        [Test]
        public void SameDirectionPawns_DoNotVeer()
        {
            // Two pawns moving North in single file
            CellRef c0 = new CellRef(0, 0, 0);
            CellRef c1 = new CellRef(0, 1, 0);
            CellRef c2 = new CellRef(0, 2, 0);
            float baseX = CellMetrics.FloorCentre(c0).x;

            var p1 = MakePawn(1, c0, c1, 50);
            var p2 = MakePawn(2, c1, c2, 50);
            var pair = new[] { p1, p2 };

            Vector3 pos1 = PawnPose.Of(p1, 0f, 0, out _, null, pair);
            Assert.That(pos1.x, Is.EqualTo(baseX).Within(1e-4f),
                "pawns travelling in same direction should not deflect sideways");
        }

        [Test]
        public void PassingStationaryPawn_VeersAroundThem()
        {
            CellRef c0 = new CellRef(0, 0, 0);
            CellRef c1 = new CellRef(0, 1, 0);
            float baseX = CellMetrics.FloorCentre(c0).x;

            // Pawn 1 walking into c1
            var p1 = MakePawn(1, c0, c1, 50);
            // Pawn 2 standing still in c1
            var p2 = new PawnView(new PawnId(2), c1, 100, 100, 50, -1, c1, 0);
            var pair = new[] { p1, p2 };

            Vector3 pos = PawnPose.Of(p1, 0f, 0, out _, null, pair);
            Assert.That(pos.x - baseX, Is.GreaterThan(0.5f),
                "walking pawn must veer to the side of the tile around the stationary pawn");
        }

        [Test]
        public void PassingEncounter_DistanceThreshold_ScalesSmoothlyWithoutThresholdPop()
        {
            // Pawn 1 moving North (0, 0) -> (0, 1)
            // Pawn 2 moving South (0, 4) -> (0, 3)
            CellRef p1From = new CellRef(0, 0, 0);
            CellRef p1To = new CellRef(0, 1, 0);

            // Test at distance just above 3.0 m vs just below 3.0 m
            // At 3.05 m distance, weight is 0.
            // At 2.95 m distance, weight is small (~0.003), completely avoiding any 60 cm cliff!
            var p1 = MakePawn(1, p1From, p1To, 50); // at z = 1.25 m
            float baseX = CellMetrics.FloorCentre(p1From).x;

            // Pawn 2 at z = 4.25 m (distance = 3.00 m)
            var p2JustAt = MakePawn(2, new CellRef(0, 4, 0), new CellRef(0, 3, 0), 10);
            var pairJustAt = new[] { p1, p2JustAt };
            Vector3 posJustAt = PawnPose.Of(p1, 0f, 0, out _, null, pairJustAt);

            // Pawn 2 at z = 4.15 m (distance = 2.90 m)
            var p2Close = MakePawn(2, new CellRef(0, 4, 0), new CellRef(0, 3, 0), 14);
            var pairClose = new[] { p1, p2Close };
            Vector3 posClose = PawnPose.Of(p1, 0f, 0, out _, null, pairClose);

            float offsetJustAt = Mathf.Abs(posJustAt.x - baseX);
            float offsetClose = Mathf.Abs(posClose.x - baseX);

            // Offset at transition entry must be very small (< 0.10 m), eliminating threshold pops
            Assert.That(offsetJustAt, Is.LessThan(0.01f), "at 3.0m threshold boundary, offset must be near zero");
            Assert.That(offsetClose, Is.LessThan(0.10f), "just inside threshold, offset begins with a gentle smooth curve");
        }
    }
}
