#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.World;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// Where a climber's boots go: the arithmetic, which is all of it that can be checked without
    /// a photograph.
    ///
    /// <para>There is no correct foothold to compare against, and this does not pretend there is.
    /// What it pins are the things that would be wrong rather than ugly: the feet moving together
    /// instead of alternately, a hold asked for further away than the leg is long, and — the one a
    /// contact sheet can never catch — the two boots standing at different distances from the rock,
    /// which is one of them inside it. Each draws as a plausible enough figure and none of them is
    /// visible in the code.</para>
    /// </summary>
    public class ClimbPoseTests
    {
        /// <summary>Roughly what the lean leaves between the figure and the stone. See the director.</summary>
        static readonly Vector3 ToRock = new Vector3(0f, 0f, 0.3f);

        [Test]
        public void TheFeetAlternate()
        {
            // The frog test. Same-side arm and leg rising together is a real gait for a real
            // animal and not one for a colonist, and it is one subtraction away at every point in
            // the cycle — so it is checked at every point rather than at the extremes, where a
            // sign error and the truth happen to agree.
            for (int i = 0; i <= 32; i++)
            {
                float swing = Mathf.Sin(i / 32f * 2f * Mathf.PI);
                ClimbPose.StepsFrom(swing, out float left, out float right);

                Assert.That(left + right, Is.EqualTo(1f).Within(1e-5f),
                    $"at swing {swing:0.00} both feet are at {left:0.00} and {right:0.00}");
            }
        }

        [Test]
        public void TheRightArmReachesWithTheLeftFoot()
        {
            // Which way round, stated once so that swapping it is a failing test and not a
            // difference of opinion. PawnFigureDirector's swing is +1 with the right arm overhead.
            ClimbPose.StepsFrom(1f, out float left, out float right);

            Assert.That(left, Is.EqualTo(1f).Within(1e-5f), "the left foot should be stepped up");
            Assert.That(right, Is.EqualTo(0f).Within(1e-5f), "the right leg should be pushing");
        }

        [Test]
        public void BothBootsStandOnOneWall()
        {
            // The one a photograph cannot settle and the reason this file exists. A wall is a
            // plane: the boots may be at any height on it and must all be the same distance into
            // it, or the climber has a foot in the rock. Written as a reach and an angle — which is
            // how this started — they are not, and nothing in a picture of a figure on a face of
            // clear air would ever have said so.
            Vector3 hip = Vector3.up * 1.2f;

            for (int i = 0; i <= 20; i++)
            {
                Vector3 offset = ClimbPose.Foothold(hip, ToRock, Vector3.up, 0.9f, i / 20f) - hip;

                Assert.That(Vector3.Dot(offset, ToRock.normalized), Is.EqualTo(ToRock.magnitude).Within(1e-4f),
                    $"the boot at step {i / 20f:0.00} is off the wall");
            }
        }

        [Test]
        public void NoFootholdIsFurtherAwayThanTheLegIsLong()
        {
            // TwoBoneIk straightens towards a target it cannot reach and stops, which draws as a
            // leg hanging and measures as a leg that merely missed. Asking for one is therefore a
            // fault that reports itself as nothing at all, and the only place to catch it is here.
            const float LegLength = 0.9f;
            Vector3 hip = new Vector3(3f, 2f, 5f);

            for (int i = 0; i <= 20; i++)
            {
                Vector3 hold = ClimbPose.Foothold(hip, ToRock, Vector3.up, LegLength, i / 20f);

                Assert.That(Vector3.Distance(hip, hold), Is.LessThan(LegLength),
                    $"the foothold at step {i / 20f:0.00} is out of reach");
            }
        }

        [Test]
        public void AShortLegGivesUpItsHeightAndKeepsItsWall()
        {
            // The order the two compromises come in, which is the whole of what makes a climber
            // with the wrong proportions still look like a climber. A boot that has come up too
            // high is a small step; a boot that has come off the wall is somebody levitating.
            const float Stubby = 0.4f;
            Vector3 hip = Vector3.up;

            Vector3 offset = ClimbPose.Foothold(hip, ToRock, Vector3.up, Stubby, 0f) - hip;

            Assert.That(Vector3.Distance(hip, hip + offset), Is.LessThan(Stubby));
            Assert.That(offset.z, Is.EqualTo(ToRock.z).Within(1e-4f), "the boot left the wall");
            Assert.That(offset.y, Is.LessThan(0f), "and it should still be below the hip");
        }

        /// <summary>
        /// A rung is not a hold. The owner's side-on reference (2026-09-18) shows a ladder climber
        /// with the pushing leg nearly straight and the other knee drawn right up in front of the
        /// chest, because the rungs are a fixed distance apart and that distance is most of a shin.
        /// Posed at the rock's step the same figure shuffles up in half-steps.
        ///
        /// <para>Both ends of the cycle are checked, because the pushing end must <em>not</em> have
        /// moved: a climber at full stretch is at full stretch on either surface, and a ladder pose
        /// that lifted both feet would read as somebody hanging rather than climbing.</para>
        /// </summary>
        [Test]
        public void TheLadderStepComesUpHigherThanTheRockStep()
        {
            Vector3 hip = Vector3.up * 2f;

            float rock = ClimbPose.Foothold(hip, ToRock, Vector3.up, 0.9f, 1f).y;
            float rung = ClimbPose.Foothold(
                hip, ToRock, Vector3.up, 0.9f, 1f, ClimbPose.LadderSteppedDrop).y;

            Assert.That(rung, Is.GreaterThan(rock), "the stepped boot should be higher on a ladder");
            Assert.That(rung, Is.LessThan(hip.y), "and still below the hip");

            Assert.That(ClimbPose.Foothold(hip, ToRock, Vector3.up, 0.9f, 0f, ClimbPose.LadderSteppedDrop).y,
                Is.EqualTo(ClimbPose.Foothold(hip, ToRock, Vector3.up, 0.9f, 0f).y).Within(1e-4f),
                "the pushing leg is at full stretch on rock and on rungs alike");
        }

        [Test]
        public void EveryFootholdIsBelowTheHip()
        {
            // A climber's foot is not above its own hip, whatever else the arithmetic does.
            Vector3 hip = Vector3.up * 2f;

            for (int i = 0; i <= 20; i++)
                Assert.That(ClimbPose.Foothold(hip, ToRock, Vector3.up, 0.9f, i / 20f).y,
                    Is.LessThan(hip.y), $"step {i / 20f:0.00} is not below the hip");
        }

        [Test]
        public void AStepMovesTheFootMonotonicallyUpTheWall()
        {
            // The cycle is a lerp over one number, so a foot that went up and then back down
            // within one step would mean the two ends had been written in the wrong order — which
            // is invisible in a still and reads as a stutter in motion.
            Vector3 hip = Vector3.up * 2f;
            float previous = float.NegativeInfinity;

            for (int i = 0; i <= 20; i++)
            {
                float height = ClimbPose.Foothold(hip, ToRock, Vector3.up, 0.9f, i / 20f).y;

                Assert.That(height, Is.GreaterThan(previous),
                    $"the boot dropped between steps at {i / 20f:0.00}");
                previous = height;
            }
        }

        [Test]
        public void AFigureWithNoLegsAsksForNothing()
        {
            // Length zero rather than a division by it. Reached when a rig maps no legs at all,
            // which is the state every character was in until the crouch needed them.
            Vector3 hip = new Vector3(1f, 1f, 1f);

            Assert.That(ClimbPose.Foothold(hip, ToRock, Vector3.up, 0f, 0.5f), Is.EqualTo(hip));
        }
    }
}
