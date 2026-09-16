#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.World;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The arithmetic behind a colonist swinging an axe at a tree, which is invented rather than
    /// animated — no pack we own contains a work clip.
    ///
    /// What is being tested here is timing rather than correctness, because there is no correct
    /// answer to test against. A swing that is symmetric in time reads as a metronome; a swing
    /// with no dwell at the bottom reads as waving; a swing whose pose jumps at a junction snaps
    /// the arm across the screen for one frame. Each of those is a property of the curve that can
    /// be stated and checked, and none of them is visible in the code that produces it.
    /// </summary>
    public class WorkSwingTests
    {
        [Test]
        public void ThePhaseAlwaysLandsInsideOneStroke()
        {
            Assert.That(WorkSwing.Phase(0f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(WorkSwing.Phase(WorkSwing.StrokeSeconds * 0.5f), Is.EqualTo(0.5f).Within(1e-4f));

            // Three strokes on is the same instant as none. It has to be stated as a wrap and not
            // as an equality: 1.15 * 3 divided by 1.15 is not 3 in single precision, so the phase
            // comes back a hair under 1 rather than at 0. Both are the same point on a circle,
            // and the stroke is flat across it, so nothing is visible — but an assertion that
            // demanded zero would fail for a reason that has nothing to do with the swing.
            float wrapped = WorkSwing.Phase(WorkSwing.StrokeSeconds * 3f);
            Assert.That(Mathf.Min(wrapped, 1f - wrapped), Is.LessThan(1e-3f));

            // And whatever it is handed, the phase is a phase.
            for (int step = 0; step < 50; step++)
            {
                float phase = WorkSwing.Phase(step * 0.37f, step * 0.11f);
                Assert.That(phase, Is.InRange(0f, 1f));
            }

            // A negative offset is the awkward one: C# gives a negative remainder, and a negative
            // phase falls through every branch of Stroke to the dwell, so the arm would sit at
            // the bottom of the swing for as long as the offset lasted.
            Assert.That(WorkSwing.Phase(0f, -0.25f), Is.EqualTo(0.75f).Within(1e-4f));
        }

        [Test]
        public void TheAxeGoesUpSlowlyAndComesDownFast()
        {
            float raiseSpan = InRaise(0.1f) - InRaise(0.9f);
            float strikeSpan = InStrike(0.9f) - InStrike(0.1f);

            Assert.That(raiseSpan, Is.GreaterThan(strikeSpan * 2f),
                "an axe is lifted deliberately and dropped under gravity; a swing that is " +
                "symmetric in time reads as a metronome rather than as work");
        }

        [Test]
        public void TheStrikeAccelerates()
        {
            // Two equal slices of the downstroke. If the second does not cover more ground than
            // the first, the blade is arriving at a constant speed, which no falling axe does.
            float start = Trough();
            float end = InStrike(0.99f);
            float middle = (start + end) * 0.5f;

            float first = WorkSwing.Stroke(middle) - WorkSwing.Stroke(start);
            float second = WorkSwing.Stroke(end) - WorkSwing.Stroke(middle);

            Assert.That(second, Is.GreaterThan(first));
        }

        [Test]
        public void TheBladeDwellsInTheWood()
        {
            // The beat after impact in which the woodcutter is doing nothing at all. Without it
            // the arm turns round the instant it arrives and the motion never reads as a blow
            // landing on something solid.
            Assert.That(WorkSwing.Stroke(0.85f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(WorkSwing.Stroke(0.99f), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void ThePoseNeverJumps()
        {
            // Sampled about twice per frame at 120 Hz, which is finer than this will ever be
            // drawn. A jump at a junction between the three parts of the stroke teleports the arm
            // for one frame, and one frame is exactly long enough to be seen and not long enough
            // to be caught by looking.
            //
            // The thresholds sit above the fastest the curve legitimately moves, which is the
            // last instant of the strike: the blade is accelerating there and covers about
            // seven degrees of shoulder in a step. Anything past twelve is a tear, not a swing.
            var previous = WorkSwing.At(0f);
            for (int step = 1; step <= 240; step++)
            {
                var current = WorkSwing.At(step / 240f);
                Assert.That(Mathf.Abs(current.Shoulder - previous.Shoulder), Is.LessThan(12f),
                    $"the shoulder jumped at phase {step / 240f:0.000}");
                Assert.That(Mathf.Abs(current.Spine - previous.Spine), Is.LessThan(3f),
                    $"the spine jumped at phase {step / 240f:0.000}");
                previous = current;
            }
        }

        [Test]
        public void TheArmAndTheBodyMoveTogether()
        {
            WorkSwing top = WorkSwing.At(Trough());
            WorkSwing impact = WorkSwing.At(0.9f);

            Assert.That(impact.Shoulder, Is.GreaterThan(top.Shoulder), "the arm comes down out of the raise");
            Assert.That(impact.Elbow, Is.GreaterThan(top.Elbow), "the forearm straightens into the blow");

            // The spine's sign runs the other way from the arm's — it stands up where an arm
            // hangs down — so folding forward into the blow is the *larger* angle at impact. This
            // assertion exists because the swing has had this backwards in both directions on the
            // way here, and neither was visible in any reading of the code: once the colonist
            // raised the axe and put it back at her side, once she leant away from her own blow.
            Assert.That(impact.Spine, Is.GreaterThan(top.Spine), "the body folds into the blow, not away from it");
        }

        [Test]
        public void AWeightOfZeroIsTheRestPose()
        {
            // How the pose eases in and out. A figure that snapped into a full swing on the tick
            // the walk ended would pop, and a figure that kept the last angle after the tree fell
            // would stand there with one arm in the air.
            WorkSwing rest = WorkSwing.At(0.4f).Scaled(0f);
            Assert.That(rest.Shoulder, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(rest.Elbow, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(rest.Spine, Is.EqualTo(0f).Within(1e-4f));

            WorkSwing full = WorkSwing.At(0.4f);
            Assert.That(full.Scaled(1f).Shoulder, Is.EqualTo(full.Shoulder).Within(1e-4f));
        }

        const int Steps = 20_000;

        /// <summary>
        /// The phase at which the axe is highest, which is where the raise ends and the strike
        /// begins. Found rather than assumed, so the tests keep meaning what they say when the
        /// timings in <see cref="WorkSwing"/> are tuned by eye.
        /// </summary>
        static float Trough()
        {
            float best = 0f, lowest = float.MaxValue;
            for (int step = 0; step <= Steps; step++)
            {
                float phase = step / (float)Steps;
                float stroke = WorkSwing.Stroke(phase);
                if (stroke >= lowest) continue;
                lowest = stroke;
                best = phase;
            }
            return best;
        }

        /// <summary>The phase during the raise at which the stroke has fallen to <paramref name="stroke"/>.</summary>
        static float InRaise(float stroke)
        {
            float trough = Trough();
            for (int step = 0; step <= Steps; step++)
            {
                float phase = trough * step / Steps;
                if (WorkSwing.Stroke(phase) <= stroke) return phase;
            }
            return trough;
        }

        /// <summary>The phase during the strike at which the stroke has risen to <paramref name="stroke"/>.</summary>
        static float InStrike(float stroke)
        {
            float trough = Trough();
            for (int step = 0; step <= Steps; step++)
            {
                float phase = trough + (1f - trough) * step / Steps;
                if (WorkSwing.Stroke(phase) >= stroke) return phase;
            }
            return 1f;
        }
    }

    /// <summary>
    /// Where a working figure is drawn, as against where the simulation says the pawn is.
    ///
    /// The two are allowed to differ and the difference is the whole point: a cell is 2.5 m and a
    /// person is half of one, so a colonist drawn on the cell centre is either inside the trunk or
    /// shoulder to it, and in neither is there room for an axe to travel.
    /// </summary>
    public class WorkStanceTests
    {
        static readonly Vector3 Tree = new Vector3(10f, 3f, 10f);

        [Test]
        public void AFigureStepsInToExactlyTheWorkingDistance()
        {
            // Far too close, and much too far: both end up at the same distance, so the picture
            // is the same whichever cell the job happened to send the colonist to.
            Vector3 close = Tree + new Vector3(0.2f, 0f, 0f);
            Vector3 far = Tree + new Vector3(3.5f, 0f, 0f);

            foreach (Vector3 start in new[] { close, far })
            {
                Vector3 stand = WorkStance.StandAt(start, Tree, Vector3.forward, 1f);
                Assert.That(Flat(stand - Tree).magnitude, Is.EqualTo(WorkStance.StandOff).Within(1e-3f));
            }
        }

        [Test]
        public void TheFigureStepsStraightInAndKeepsItsHeight()
        {
            Vector3 start = Tree + new Vector3(2f, 0.8f, 2f);
            Vector3 stand = WorkStance.StandAt(start, Tree, Vector3.forward, 1f);

            Assert.That(stand.y, Is.EqualTo(start.y).Within(1e-4f),
                "the step is across the ground, never up or down it");

            // On the same bearing from the tree as it started: it walks in along its own line
            // rather than round the trunk to some canonical side.
            Vector3 before = Flat(start - Tree).normalized;
            Vector3 after = Flat(stand - Tree).normalized;
            Assert.That(Vector3.Dot(before, after), Is.EqualTo(1f).Within(1e-3f));
        }

        [Test]
        public void AFigureStandingInTheTrunkBacksOffTheWayItCameIn()
        {
            // The case the committed fell job actually produces: the colonist walks into the
            // tree's own cell, so the positions give no direction at all and the only thing left
            // to go on is which way it is pointed.
            Vector3 stand = WorkStance.StandAt(Tree, Tree, Vector3.forward, 1f);

            Assert.That(Flat(stand - Tree).magnitude, Is.EqualTo(WorkStance.StandOff).Within(1e-3f));
            Assert.That(Vector3.Dot(Flat(stand - Tree).normalized, Vector3.forward),
                Is.EqualTo(-1f).Within(1e-3f), "it backs off, rather than stepping through the trunk");
        }

        [Test]
        public void ItNeverReturnsNowhereEvenWithNothingToGoOn()
        {
            // Standing in the trunk *and* facing nowhere, which a figure leased this frame is.
            // Any direction beats a zero vector, which would leave the figure at the tree centre
            // and, worse, make the stand position depend on floating-point noise.
            Vector3 stand = WorkStance.StandAt(Tree, Tree, Vector3.zero, 1f);
            Assert.That(Flat(stand - Tree).magnitude, Is.EqualTo(WorkStance.StandOff).Within(1e-3f));
        }

        [Test]
        public void NoWeightMeansNoStep()
        {
            // How the step eases in. At zero the figure is exactly where the simulation put it,
            // which is what makes the walk-in and the step-up join without a seam.
            Vector3 start = Tree + new Vector3(3f, 0f, 1f);
            Assert.That(WorkStance.StandAt(start, Tree, Vector3.forward, 0f), Is.EqualTo(start));

            Vector3 half = WorkStance.StandAt(start, Tree, Vector3.forward, 0.5f);
            Vector3 full = WorkStance.StandAt(start, Tree, Vector3.forward, 1f);
            Assert.That(Vector3.Distance(start, half), Is.LessThan(Vector3.Distance(start, full)));
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
