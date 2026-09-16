#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Where a pawn is drawn between two ticks.
    ///
    /// This used to live inside the instanced pass, and the animated figures needed exactly the
    /// same answer. Two copies would agree for as long as nobody touched either, and the day they
    /// disagreed the symptom would be a colonist's figure walking half a cell away from the
    /// colonist the game thinks it is showing you — which nobody would read as a duplicated
    /// formula.
    /// </summary>
    public class PawnPoseTests
    {
        static PawnView Pawn(CellRef cell, CellRef next, int movePercent) =>
            new PawnView(new PawnId(1), cell, 100, 100, 50, -1, next, movePercent);

        [SetUp]
        public void SetUp() => GroundRelief.Reset();

        [TearDown]
        public void TearDown() => GroundRelief.Reset();

        /// <summary>
        /// A pawn is drawn on the ground that is drawn, not on the flat grid underneath it — and
        /// at her own position along the walk, so she follows the slope instead of cutting the
        /// chord between two cell centres.
        /// </summary>
        [Test]
        public void APawnWalksOnTheDrawnGround()
        {
            GroundRelief.Amplitude = GroundRelief.BoardAmplitude;

            var cell = new CellRef(12, 9, 1);
            Vector3 at = PawnPose.Of(Pawn(cell, cell, 0), 0f, 0, out _);
            Vector3 flat = CellMetrics.FloorCentre(cell);

            Assert.That(at.x, Is.EqualTo(flat.x).Within(1e-4f), "a lift never moves anyone sideways");
            Assert.That(at.z, Is.EqualTo(flat.z).Within(1e-4f));
            Assert.That(at.y - flat.y,
                Is.EqualTo(GroundRelief.HeightAt(flat.x, flat.z)).Within(1e-4f));

            // Half way between two cells, she stands on the ground half way between them, which is
            // not the average of the two ends unless the ground happens to be straight there.
            var from = new CellRef(12, 9, 1);
            var to = new CellRef(13, 9, 1);
            Vector3 mid = PawnPose.Of(Pawn(from, to, 50), 0f, 0, out _);
            Assert.That(mid.y - CellMetrics.FloorCentre(from).y,
                Is.EqualTo(GroundRelief.HeightAt(mid.x, mid.z)).Within(1e-4f),
                "the height is taken where she is, not at either end of the step");
        }

        [Test]
        public void AStandingPawnSitsOnItsCellAndHasNoHeading()
        {
            var cell = new CellRef(4, 7, 2);
            Vector3 position = PawnPose.Of(Pawn(cell, cell, 0), 0.5f, 2, out Vector3 heading);

            Assert.That(position, Is.EqualTo(CellMetrics.FloorCentre(cell)));
            Assert.That(heading, Is.EqualTo(Vector3.zero),
                "a still pawn must keep facing where it was, not snap to a default bearing");
        }

        [Test]
        public void HalfwayAcrossIsHalfwayBetweenTheTwoCells()
        {
            var from = new CellRef(4, 7, 2);
            var to = new CellRef(5, 7, 2);
            Vector3 position = PawnPose.Of(Pawn(from, to, 50), 0f, 0, out _);

            Vector3 midpoint = (CellMetrics.FloorCentre(from) + CellMetrics.FloorCentre(to)) * 0.5f;
            Assert.That(Vector3.Distance(position, midpoint), Is.LessThan(1e-4f));
        }

        [Test]
        public void APartTickCarriesThePawnOnRatherThanStalling()
        {
            var from = new CellRef(0, 0, 1);
            var to = new CellRef(1, 0, 1);

            Vector3 onTheTick = PawnPose.Of(Pawn(from, to, 50), 0f, 2, out _);
            Vector3 betweenTicks = PawnPose.Of(Pawn(from, to, 50), 1f, 2, out _);

            Assert.That(betweenTicks.x, Is.GreaterThan(onTheTick.x),
                "without this a 144 Hz display shows the same position for two frames in three");
        }

        [Test]
        public void ExtrapolationNeverOvershootsTheCellBeingEntered()
        {
            var from = new CellRef(0, 0, 1);
            var to = new CellRef(1, 0, 1);

            // A pawn all but arrived, on a frame sitting at the very end of its tick. Unclamped
            // this runs past the target cell and back, which reads as a stumble on every step.
            Vector3 position = PawnPose.Of(Pawn(from, to, 100), 1f, 8, out _);

            Assert.That(Vector3.Distance(position, CellMetrics.FloorCentre(to)), Is.LessThan(1e-4f));
        }

        [Test]
        public void YawFollowsTheHeading()
        {
            Assert.That(PawnPose.YawOf(Vector3.forward), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(PawnPose.YawOf(Vector3.right), Is.EqualTo(90f).Within(1e-3f));
            Assert.That(PawnPose.YawOf(Vector3.zero), Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void AVerticalStepGivesNoBearingAtAll()
        {
            // **The jolt the owner reported on a descent, and it was never about the ladder.**
            //
            // A step that only changes layer travels (0, ±3, 0). That is not a zero vector, so the
            // old code handed it on as a heading — and its yaw is Atan2(0, 0), which is not "no
            // bearing", it is zero, which is due north. Every colonist entering a shaft therefore
            // turned slowly to face north and turned back on the way out. Callers already treat a
            // zero heading as "keep facing wherever you were", which is exactly right for a climb.
            var pawn = Pawn(new CellRef(4, 4, 6), new CellRef(4, 4, 5), 40);
            PawnPose.Of(pawn, 0f, 0, out Vector3 heading);

            Assert.That(heading, Is.EqualTo(Vector3.zero),
                "a climb handed back a bearing, and the only one it can hand back is north");
        }

        [Test]
        public void AVerticalStepStillCarriesTheFigureDownTheShaft()
        {
            // Flattening the BEARING must not flatten the travel: the figure still has to move.
            var pawn = Pawn(new CellRef(4, 4, 6), new CellRef(4, 4, 5), 50);
            Vector3 at = PawnPose.Of(pawn, 0f, 0, out _);

            Vector3 top = CellMetrics.FloorCentre(new CellRef(4, 4, 6));
            Vector3 bottom = CellMetrics.FloorCentre(new CellRef(4, 4, 5));

            Assert.That(at.y, Is.EqualTo((top.y + bottom.y) * 0.5f).Within(1e-3f),
                "half way down the shaft is half way down the shaft");
            Assert.That(at.x, Is.EqualTo(top.x).Within(1e-3f));
            Assert.That(at.z, Is.EqualTo(top.z).Within(1e-3f));
        }

        [Test]
        public void ADiagonalDescentKeepsTheHorizontalPartOfItsBearing()
        {
            // A step that changes layer AND moves across still has a bearing, and it is the
            // horizontal one. Flattening must take the vertical part out, not the whole thing.
            var pawn = Pawn(new CellRef(4, 4, 6), new CellRef(5, 4, 5), 50);
            PawnPose.Of(pawn, 0f, 0, out Vector3 heading);

            Assert.That(heading.y, Is.EqualTo(0f));
            Assert.That(heading.x, Is.GreaterThan(0f), "the across-the-map part of the bearing was lost");
            Assert.That(PawnPose.YawOf(heading), Is.EqualTo(90f).Within(0.01f));
        }
    }
}
