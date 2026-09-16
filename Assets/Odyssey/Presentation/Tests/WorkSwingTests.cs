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
        }

        [Test]
        public void EveryFigureStartsItsStrokeAtTheBeginning()
        {
            // The fix for work that snapped on. The offset used to shift the phase, so a colonist
            // taking up an axe started wherever its own constant named — arms half raised — and
            // the ease-in had to carry it there from a standing idle. Whatever the offset, a clock
            // at nought is now the start of a stroke.
            foreach (float offset in new[] { 0f, 0.25f, 0.618f, 0.99f })
                Assert.That(WorkSwing.Phase(0f, offset), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void TwoColonistsDriftApartRatherThanStartingApart()
        {
            // In step at the first blow, plainly out of it a few strokes later, which is how two
            // people chopping actually fall out of time.
            const float A = 0.1f, B = 0.9f;
            Assert.That(WorkSwing.Phase(0f, A), Is.EqualTo(WorkSwing.Phase(0f, B)).Within(1e-5f));

            float apart = Mathf.Abs(WorkSwing.Phase(WorkSwing.StrokeSeconds * 4f, A)
                                    - WorkSwing.Phase(WorkSwing.StrokeSeconds * 4f, B));
            Assert.That(Mathf.Min(apart, 1f - apart), Is.GreaterThan(0.15f),
                "four strokes in and still in unison reads as a machine");
        }

        [Test]
        public void ANominalOffsetIsTheNominalStroke()
        {
            // The spread is either side of the stated length, not on top of it, or the whole
            // colony would quietly work faster or slower than the number in the source says.
            Assert.That(WorkSwing.PeriodFor(0.5f), Is.EqualTo(WorkSwing.StrokeSeconds).Within(1e-4f));
            Assert.That(WorkSwing.PeriodFor(0f),
                Is.EqualTo(WorkSwing.StrokeSeconds * (1f - WorkSwing.StrokeSpread * 0.5f)).Within(1e-4f));
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

        [Test]
        public void TheBlowLandsOnceAndOnlyOnce()
        {
            // Walking a whole stroke a frame at a time, exactly one frame is the one the chips
            // fly on. Two would double every burst; none would mean an axe that never connects.
            int landings = 0;
            float previous = 0f;
            for (int step = 1; step <= 600; step++)
            {
                float current = WorkSwing.Phase(step * WorkSwing.StrokeSeconds / 200f);
                if (WorkSwing.Lands(previous, current)) landings++;
                previous = current;
            }

            Assert.That(landings, Is.EqualTo(3), "three strokes in, three blows landed");
        }

        [Test]
        public void TheWrapDoesNotLandASecondBlow()
        {
            // The phase restarts inside the dwell, with the blade already in the wood. Firing
            // again there would put a second burst of chips a quarter of a second after the first,
            // for one blow.
            Assert.That(WorkSwing.Lands(0.9f, 0.05f), Is.False);
        }

        [Test]
        public void ADroppedFrameStillLands()
        {
            // A frame long enough to step over the whole strike. Rare, but a stutter is not a
            // reason for an axe to pass through a tree in silence.
            Assert.That(WorkSwing.Lands(0.5f, 0.95f), Is.True);
            Assert.That(WorkSwing.Lands(0.3f, 0.2f), Is.True, "and a long frame that also wrapped");
        }

        [Test]
        public void NothingLandsWhileTheAxeIsStillGoingUp()
        {
            Assert.That(WorkSwing.Lands(0.1f, 0.2f), Is.False);
            Assert.That(WorkSwing.Lands(0.6f, 0.7f), Is.False);
            Assert.That(WorkSwing.Lands(0.85f, 0.9f), Is.False, "nor during the dwell after it");
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

        /// <summary>
        /// A stand-in for what a figure measures off its own struck pose: the offset from its feet
        /// to its blade, in its own frame. Forward and to the left, because the swing comes over
        /// the shoulder and across the body.
        /// </summary>
        static readonly Vector3 Strike = new Vector3(-0.7f, 0f, 1.3f);

        [Test]
        public void TheBladeLandsInTheTree()
        {
            // The whole contract. Wherever the pawn happens to be standing, the figure is drawn
            // where its own strike offset puts the edge in the wood — which is what a stand
            // computed as a *distance* could not do once the swing went diagonal, because most of
            // a metre and a half of reach was sideways.
            foreach (Vector3 start in new[]
            {
                Tree + new Vector3(0.2f, 0f, 0f),
                Tree + new Vector3(3.5f, 0f, 0f),
                Tree + new Vector3(-2f, 0f, 2.5f),
            })
            {
                Vector3 stand = WorkStance.StandAt(start, Tree, Vector3.forward, 1f, Strike);
                Vector3 edge = stand + Strike;
                Assert.That(Flat(edge - Tree).magnitude, Is.LessThan(WorkStance.Bite + 1e-3f),
                    $"the axe missed the tree from {start}");
            }
        }

        [Test]
        public void TheEdgeStopsInsideTheWoodRatherThanAtItsCentre()
        {
            // Aimed at the centre the axe is buried to the eye; aimed at the near face it stops on
            // the bark, which reads as not quite touching. The bite is the difference.
            Vector3 stand = WorkStance.StandAt(Tree + Vector3.right * 2f, Tree, Vector3.forward, 1f, Strike);
            Vector3 edge = stand + Strike;

            Assert.That(Flat(edge - Tree).magnitude, Is.EqualTo(WorkStance.Bite).Within(1e-3f));
            Assert.That(Vector3.Dot(Flat(edge - Tree).normalized, Vector3.right), Is.GreaterThan(0.9f),
                "the edge stops short on the side the colonist is standing, not past the far side");
        }

        [Test]
        public void TheFigureKeepsItsHeight()
        {
            Vector3 start = Tree + new Vector3(2f, 0.8f, 2f);
            Vector3 stand = WorkStance.StandAt(start, Tree, Vector3.forward, 1f, Strike);

            Assert.That(stand.y, Is.EqualTo(start.y).Within(1e-4f),
                "the step is across the ground, never up or down it");
        }

        [Test]
        public void AFigureStandingInTheTrunkBacksOffTheWayItCameIn()
        {
            // The case the committed fell job actually produces: the colonist walks into the
            // tree's own cell, so the positions give no direction at all and the only thing left
            // to go on is which way it is pointed.
            Vector3 stand = WorkStance.StandAt(Tree, Tree, Vector3.forward, 1f, Strike);

            Assert.That(Flat(stand - Tree).magnitude, Is.GreaterThan(0.1f), "it is still in the trunk");
            Assert.That(Flat(stand + Strike - Tree).magnitude, Is.LessThan(WorkStance.Bite + 1e-3f));
        }

        [Test]
        public void ItNeverReturnsNowhereEvenWithNothingToGoOn()
        {
            // Standing in the trunk and facing nowhere, which a figure leased this frame is. Any
            // direction beats a zero vector, which would make the stand depend on nothing but
            // floating-point noise.
            Vector3 stand = WorkStance.StandAt(Tree, Tree, Vector3.zero, 1f, Strike);
            Assert.That(Flat(stand - Tree).magnitude, Is.GreaterThan(0.1f));
        }

        [Test]
        public void NoStrikeOffsetEverPutsAColonistInsideTheTrunk()
        {
            // The strike is solved off a pose that is still being tuned by eye, and a bad set of
            // angles could solve to no offset at all. The floor is what stops that arriving on
            // screen as a colonist standing in the middle of the tree she is felling; the blow
            // lands short instead, which is a great deal less wrong.
            Vector3 stand = WorkStance.StandAt(Tree + Vector3.right, Tree, Vector3.forward, 1f, Vector3.zero);
            Assert.That(Flat(stand - Tree).magnitude,
                Is.EqualTo(WorkStance.MinimumStandOff).Within(1e-3f));
        }

        [Test]
        public void NoWeightMeansNoStep()
        {
            // How the step eases in. At zero the figure is exactly where the simulation put it,
            // which is what makes the walk-in and the step-up join without a seam.
            Vector3 start = Tree + new Vector3(3f, 0f, 1f);
            Assert.That(WorkStance.StandAt(start, Tree, Vector3.forward, 0f, Strike), Is.EqualTo(start));

            Vector3 half = WorkStance.StandAt(start, Tree, Vector3.forward, 0.5f, Strike);
            Vector3 full = WorkStance.StandAt(start, Tree, Vector3.forward, 1f, Strike);
            Assert.That(Vector3.Distance(start, half), Is.LessThan(Vector3.Distance(start, full)));
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
