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
    }
}
