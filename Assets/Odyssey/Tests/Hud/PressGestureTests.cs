#nullable enable
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// Did the press travel, or was it a click? The whole of right-click's behaviour turns on this
    /// one question, and it is the part of the input path the PlayMode harness cannot reach.
    /// </summary>
    public class PressGestureTests
    {
        [Test]
        public void APressThatNeverMovesIsAClick()
        {
            var press = new PressGesture();
            press.Press(100f, 100f);
            press.MoveTo(100f, 100f);

            Assert.That(press.Release(), Is.True);
        }

        /// <summary>
        /// A press may wobble and still be a click. This is the whole reason there is a threshold
        /// rather than a test for exact equality: a hand on a mouse button is never still.
        /// </summary>
        [Test]
        public void ASmallWobbleIsStillAClick()
        {
            var press = new PressGesture();
            press.Press(100f, 100f);
            press.MoveTo(102f, 101f);
            press.MoveTo(101f, 103f);

            Assert.That(press.Release(), Is.True);
        }

        [Test]
        public void APressThatTravelsIsADrag()
        {
            var press = new PressGesture();
            press.Press(100f, 100f);
            press.MoveTo(200f, 140f);

            Assert.That(press.Travelled, Is.True, "it should know it is a drag before the button comes up");
            Assert.That(press.Release(), Is.False);
        }

        /// <summary>
        /// The boundary, stated rather than left to whoever reads the comparison. Exactly the
        /// threshold counts as travel; a hair inside it does not.
        /// </summary>
        [Test]
        public void TheThresholdItselfCounts()
        {
            var atIt = new PressGesture();
            atIt.Press(0f, 0f);
            atIt.MoveTo(PressGesture.OrbitThresholdPixels, 0f);
            Assert.That(atIt.Release(), Is.False, "exactly the threshold is a drag");

            var justInside = new PressGesture();
            justInside.Press(0f, 0f);
            justInside.MoveTo(PressGesture.OrbitThresholdPixels - 0.01f, 0f);
            Assert.That(justInside.Release(), Is.True, "a hair inside the threshold is a click");
        }

        /// <summary>
        /// An orbit that comes back to where it started is still an orbit. Without this, swinging
        /// the camera around and returning would put the player's tool down under them.
        /// </summary>
        [Test]
        public void TravelLatchesSoASweepAndBackIsNotAClick()
        {
            var press = new PressGesture();
            press.Press(100f, 100f);
            press.MoveTo(400f, 300f);
            press.MoveTo(100f, 100f);

            Assert.That(press.Release(), Is.False);
        }

        /// <summary>
        /// A release with no press before it is not a click on anything — the press happened over
        /// a panel, or before the window had focus, and the world must not act on it.
        /// </summary>
        [Test]
        public void AReleaseWithNoPressIsNotAClick()
        {
            var press = new PressGesture();
            Assert.That(press.Release(), Is.False);
        }

        [Test]
        public void AnAbandonedPressIsNotAClick()
        {
            var press = new PressGesture();
            press.Press(100f, 100f);
            press.Abandon();

            Assert.That(press.Down, Is.False);
            Assert.That(press.Release(), Is.False);
        }

        /// <summary>One press does not colour the next: a drag followed by a click is a click.</summary>
        [Test]
        public void ADragDoesNotPoisonTheNextPress()
        {
            var press = new PressGesture();
            press.Press(0f, 0f);
            press.MoveTo(500f, 500f);
            press.Release();

            press.Press(10f, 10f);
            Assert.That(press.Release(), Is.True);
        }

        /// <summary>Movement before a press is nothing at all, not the beginning of a drag.</summary>
        [Test]
        public void MovementWithTheButtonUpIsIgnored()
        {
            var press = new PressGesture();
            press.MoveTo(900f, 900f);
            press.Press(0f, 0f);

            Assert.That(press.Release(), Is.True);
        }
    }
}
