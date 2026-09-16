#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.World;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The arithmetic of a one-shot gesture: the curve a figure follows as it stoops to the floor
    /// and straightens up again.
    ///
    /// <para>As with <c>WorkSwingTests</c>, what is testable here is the <em>shape</em> and not the
    /// appearance. There is no correct crouch to compare against. But a motion that is symmetric in
    /// time reads as a bob, one with no hold at the bottom shows the hands touching the ground for
    /// an instant that is as likely as not to fall between two frames, and one that jumps at a
    /// junction tears the figure across the screen for exactly long enough to be seen and not long
    /// enough to be caught by looking. Each of those is a property of the curve, and none of them
    /// is visible in the code that produces it.</para>
    /// </summary>
    public class GesturePoseTests
    {
        [Test]
        public void AGestureBeginsAndEndsStandingUp()
        {
            // The one thing that must be exactly true rather than approximately: a gesture that
            // does not return to zero leaves the colonist permanently three inches shorter.
            foreach (Gesture motion in new[] { Gesture.Lift, Gesture.Stow })
            {
                Assert.That(motion.At(0f), Is.EqualTo(0f).Within(1e-5f));
                Assert.That(motion.At(1f), Is.EqualTo(0f).Within(1e-5f));
            }
        }

        [Test]
        public void TheHandsAreAtTheFloorLongEnoughToBeDrawnThere()
        {
            // Without a hold, the bottom of the motion is a single instant. At thirty frames a
            // second an instant falls between two frames about as often as it lands on one, so the
            // one moment the gesture exists to show is the one most likely never to be seen.
            Gesture lift = Gesture.Lift;
            Assert.That(lift.HoldEnds, Is.GreaterThan(lift.DownEnds),
                "there is no hold at all: the hands touch the ground for one instant");

            float middle = (lift.DownEnds + lift.HoldEnds) * 0.5f;
            Assert.That(lift.At(middle), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(lift.Hands(middle), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void ItDropsAwayQuicklyAndComesOffTheFloorSlowly()
        {
            // The asymmetry is what makes it read as a lift rather than as a bob, and the direction
            // of it is the part that is easy to get backwards. Going down is a fall the knees
            // catch, so it leaves standing at speed and settles. Coming up is work against the
            // weight of whatever has just been picked up, so it is slow to start. Reversed, the
            // figure appears to be putting something down.
            Gesture lift = Gesture.Lift;

            Assert.That(lift.At(lift.DownEnds * 0.5f), Is.GreaterThan(0.5f),
                "half way through the descent and less than half way down: that is a fall easing IN");

            float halfWayUp = lift.HoldEnds + (1f - lift.HoldEnds) * 0.5f;
            Assert.That(lift.At(halfWayUp), Is.GreaterThan(0.5f),
                "half way through the rise and already half up: the figure sprang off the floor");
        }

        [Test]
        public void AStowLowersItsLoadMoreCarefullyThanALiftTakesOne()
        {
            // A load is set down under control and an empty body straightens freely, so the descent
            // takes a larger share of a stow than it does of a lift.
            Assert.That(Gesture.Stow.DownEnds, Is.GreaterThan(Gesture.Lift.DownEnds));
        }

        [Test]
        public void ThePoseNeverJumps()
        {
            // Sampled far finer than this will ever be drawn. The junctions between the descent,
            // the hold and the rise are where a curve stitched from three pieces tears.
            foreach (Gesture motion in new[] { Gesture.Lift, Gesture.Stow })
            {
                float previous = motion.At(0f);
                for (int step = 1; step <= 480; step++)
                {
                    float current = motion.At(step / 480f);
                    Assert.That(Mathf.Abs(current - previous), Is.LessThan(0.02f),
                        $"the crouch jumped at phase {step / 480f:0.000}");
                    previous = current;
                }
            }
        }

        [Test]
        public void TheDepthIsNeverMoreThanTheLegsHave()
        {
            // Past about half the hip height a two-bone solve with the foot pinned runs out of leg:
            // the knee reaches full flexion and the solver straightens towards a target it cannot
            // reach, which draws as a colonist kneeling through its own shins.
            foreach (Gesture motion in new[] { Gesture.Lift, Gesture.Stow })
            {
                Assert.That(motion.Depth, Is.GreaterThan(0f));
                Assert.That(motion.Depth, Is.LessThanOrEqualTo(PawnFigureDirector.DeepestCrouch));
            }
        }

        [Test]
        public void ThePhaseIsAPhaseWhateverItIsHanded()
        {
            Gesture lift = Gesture.Lift;

            Assert.That(lift.Phase(-1f), Is.EqualTo(0f));
            Assert.That(lift.Phase(0f), Is.EqualTo(0f));
            Assert.That(lift.Phase(lift.Seconds * 0.5f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(lift.Phase(lift.Seconds * 9f), Is.EqualTo(1f));

            Assert.That(lift.Finished(lift.Seconds - 0.01f), Is.False);
            Assert.That(lift.Finished(lift.Seconds), Is.True);
        }

        [Test]
        public void ADroppedFrameStillEndsTheGesture()
        {
            // A one-shot has to be able to finish on a frame that stepped clean over the end of it.
            // The alternative is a colonist stuck in a stoop until the next thing it picks up.
            Assert.That(Gesture.Lift.Finished(Gesture.Lift.Seconds * 4f), Is.True);
            Assert.That(Gesture.Lift.At(Gesture.Lift.Phase(Gesture.Lift.Seconds * 4f)),
                Is.EqualTo(0f).Within(1e-5f), "and ends it standing up");
        }
    }
}
