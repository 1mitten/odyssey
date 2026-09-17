#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
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
        /// <summary>
        /// The stroke these are about. They were written when there was exactly one and its
        /// numbers were consts on <see cref="WorkSwing"/>; the maths is unchanged and now belongs
        /// to a value, so a second stroke can exist.
        /// </summary>
        static readonly WorkStroke Stroke = WorkStroke.Axe;

        [Test]
        public void ThePhaseAlwaysLandsInsideOneStroke()
        {
            Assert.That(Stroke.Phase(0f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Stroke.Phase(Stroke.StrokeSeconds * 0.5f), Is.EqualTo(0.5f).Within(1e-4f));

            // Three strokes on is the same instant as none. It has to be stated as a wrap and not
            // as an equality: 1.15 * 3 divided by 1.15 is not 3 in single precision, so the phase
            // comes back a hair under 1 rather than at 0. Both are the same point on a circle,
            // and the stroke is flat across it, so nothing is visible — but an assertion that
            // demanded zero would fail for a reason that has nothing to do with the swing.
            float wrapped = Stroke.Phase(Stroke.StrokeSeconds * 3f);
            Assert.That(Mathf.Min(wrapped, 1f - wrapped), Is.LessThan(1e-3f));

            // And whatever it is handed, the phase is a phase.
            for (int step = 0; step < 50; step++)
            {
                float phase = Stroke.Phase(step * 0.37f, step * 0.11f);
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
                Assert.That(Stroke.Phase(0f, offset), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void TwoColonistsDriftApartRatherThanStartingApart()
        {
            // In step at the first blow, plainly out of it a few strokes later, which is how two
            // people chopping actually fall out of time.
            const float A = 0.1f, B = 0.9f;
            Assert.That(Stroke.Phase(0f, A), Is.EqualTo(Stroke.Phase(0f, B)).Within(1e-5f));

            float apart = Mathf.Abs(Stroke.Phase(Stroke.StrokeSeconds * 4f, A)
                                    - Stroke.Phase(Stroke.StrokeSeconds * 4f, B));
            Assert.That(Mathf.Min(apart, 1f - apart), Is.GreaterThan(0.15f),
                "four strokes in and still in unison reads as a machine");
        }

        [Test]
        public void ANominalOffsetIsTheNominalStroke()
        {
            // The spread is either side of the stated length, not on top of it, or the whole
            // colony would quietly work faster or slower than the number in the source says.
            Assert.That(Stroke.PeriodFor(0.5f), Is.EqualTo(Stroke.StrokeSeconds).Within(1e-4f));
            Assert.That(Stroke.PeriodFor(0f),
                Is.EqualTo(Stroke.StrokeSeconds * (1f - WorkStroke.StrokeSpread * 0.5f)).Within(1e-4f));
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

            float first = Stroke.Stroke(middle) - Stroke.Stroke(start);
            float second = Stroke.Stroke(end) - Stroke.Stroke(middle);

            Assert.That(second, Is.GreaterThan(first));
        }

        [Test]
        public void TheBladeDwellsInTheWood()
        {
            // The beat after impact in which the woodcutter is doing nothing at all. Without it
            // the arm turns round the instant it arrives and the motion never reads as a blow
            // landing on something solid.
            Assert.That(Stroke.Stroke(0.85f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(Stroke.Stroke(0.99f), Is.EqualTo(1f).Within(1e-4f));
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
            var previous = Stroke.At(0f);
            for (int step = 1; step <= 240; step++)
            {
                var current = Stroke.At(step / 240f);
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
            WorkSwing top = Stroke.At(Trough());
            WorkSwing impact = Stroke.At(0.9f);

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
            WorkSwing rest = Stroke.At(0.4f).Scaled(0f);
            Assert.That(rest.Shoulder, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(rest.Elbow, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(rest.Spine, Is.EqualTo(0f).Within(1e-4f));

            WorkSwing full = Stroke.At(0.4f);
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
                float current = Stroke.Phase(step * Stroke.StrokeSeconds / 200f);
                if (Stroke.Lands(previous, current)) landings++;
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
            Assert.That(Stroke.Lands(0.9f, 0.05f), Is.False);
        }

        [Test]
        public void ADroppedFrameStillLands()
        {
            // A frame long enough to step over the whole strike. Rare, but a stutter is not a
            // reason for an axe to pass through a tree in silence.
            Assert.That(Stroke.Lands(0.5f, 0.95f), Is.True);
            Assert.That(Stroke.Lands(0.3f, 0.2f), Is.True, "and a long frame that also wrapped");
        }

        [Test]
        public void NothingLandsWhileTheAxeIsStillGoingUp()
        {
            Assert.That(Stroke.Lands(0.1f, 0.2f), Is.False);
            Assert.That(Stroke.Lands(0.6f, 0.7f), Is.False);
            Assert.That(Stroke.Lands(0.85f, 0.9f), Is.False, "nor during the dwell after it");
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
                float stroke = Stroke.Stroke(phase);
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
                if (Stroke.Stroke(phase) <= stroke) return phase;
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
                if (Stroke.Stroke(phase) >= stroke) return phase;
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
        /// <summary>How far into the work felling aims. It used to be a constant on WorkStance.</summary>
        static readonly float Bite = WorkStyle.Felling.AimFromCentre;

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
                Vector3 stand = WorkStance.StandAt(start, Tree, Vector3.forward, 1f, Strike, Bite);
                Vector3 edge = stand + Strike;
                Assert.That(Flat(edge - Tree).magnitude, Is.LessThan(Bite + 1e-3f),
                    $"the axe missed the tree from {start}");
            }
        }

        [Test]
        public void TheEdgeStopsInsideTheWoodRatherThanAtItsCentre()
        {
            // Aimed at the centre the axe is buried to the eye; aimed at the near face it stops on
            // the bark, which reads as not quite touching. The bite is the difference.
            Vector3 stand = WorkStance.StandAt(Tree + Vector3.right * 2f, Tree, Vector3.forward, 1f, Strike, Bite);
            Vector3 edge = stand + Strike;

            Assert.That(Flat(edge - Tree).magnitude, Is.EqualTo(Bite).Within(1e-3f));
            Assert.That(Vector3.Dot(Flat(edge - Tree).normalized, Vector3.right), Is.GreaterThan(0.9f),
                "the edge stops short on the side the colonist is standing, not past the far side");
        }

        [Test]
        public void TheFigureKeepsItsHeight()
        {
            Vector3 start = Tree + new Vector3(2f, 0.8f, 2f);
            Vector3 stand = WorkStance.StandAt(start, Tree, Vector3.forward, 1f, Strike, Bite);

            Assert.That(stand.y, Is.EqualTo(start.y).Within(1e-4f),
                "the step is across the ground, never up or down it");
        }

        [Test]
        public void AFigureStandingInTheTrunkBacksOffTheWayItCameIn()
        {
            // The case the committed fell job actually produces: the colonist walks into the
            // tree's own cell, so the positions give no direction at all and the only thing left
            // to go on is which way it is pointed.
            Vector3 stand = WorkStance.StandAt(Tree, Tree, Vector3.forward, 1f, Strike, Bite);

            Assert.That(Flat(stand - Tree).magnitude, Is.GreaterThan(0.1f), "it is still in the trunk");
            Assert.That(Flat(stand + Strike - Tree).magnitude, Is.LessThan(Bite + 1e-3f));
        }

        [Test]
        public void ItNeverReturnsNowhereEvenWithNothingToGoOn()
        {
            // Standing in the trunk and facing nowhere, which a figure leased this frame is. Any
            // direction beats a zero vector, which would make the stand depend on nothing but
            // floating-point noise.
            Vector3 stand = WorkStance.StandAt(Tree, Tree, Vector3.zero, 1f, Strike, Bite);
            Assert.That(Flat(stand - Tree).magnitude, Is.GreaterThan(0.1f));
        }

        [Test]
        public void NoStrikeOffsetEverPutsAColonistInsideTheTrunk()
        {
            // The strike is solved off a pose that is still being tuned by eye, and a bad set of
            // angles could solve to no offset at all. The floor is what stops that arriving on
            // screen as a colonist standing in the middle of the tree she is felling; the blow
            // lands short instead, which is a great deal less wrong.
            Vector3 stand = WorkStance.StandAt(Tree + Vector3.right, Tree, Vector3.forward, 1f, Vector3.zero, Bite);
            Assert.That(Flat(stand - Tree).magnitude,
                Is.EqualTo(WorkStance.MinimumStandOff).Within(1e-3f));
        }

        [Test]
        public void NoWeightMeansNoStep()
        {
            // How the step eases in. At zero the figure is exactly where the simulation put it,
            // which is what makes the walk-in and the step-up join without a seam.
            Vector3 start = Tree + new Vector3(3f, 0f, 1f);
            Assert.That(WorkStance.StandAt(start, Tree, Vector3.forward, 0f, Strike, Bite), Is.EqualTo(start));

            Vector3 half = WorkStance.StandAt(start, Tree, Vector3.forward, 0.5f, Strike, Bite);
            Vector3 full = WorkStance.StandAt(start, Tree, Vector3.forward, 1f, Strike, Bite);
            Assert.That(Vector3.Distance(start, half), Is.LessThan(Vector3.Distance(start, full)));
        }

        /// <summary>
        /// Why the click hit-test and the selection bracket may not be built from
        /// <c>PawnPose.Of</c>, and have to ask the figure director where the figure actually is.
        ///
        /// A working figure is stepped off its cell so the axe reaches the wood. The colonist
        /// cursor is 1.15 m across, so a box centred on the cell reaches 0.575 m. The stand-off is
        /// at least <see cref="WorkStance.MinimumStandOff"/> before the strike offset is counted,
        /// which puts the person at or past the edge of the box that is supposed to select them.
        ///
        /// That is the whole of a playtest report from 2026-09-16: a colonist chopping a tree
        /// could not be clicked. It read as intermittent because the offset scales with the swing
        /// weight, so it comes and goes across a stroke.
        /// </summary>
        [Test]
        public void AWorkingFigureStandsOffItsCellFurtherThanTheClickBoxReaches()
        {
            // The fell job walks the colonist into the tree's own cell, so both are the same point.
            var cell = new Vector3(30f, 0f, 30f);
            var strike = new Vector3(0.45f, 0f, 0.35f);

            // The aim came in with mining, which needed to stop aiming at the middle of a 2.5 m
            // block of stone. This is a felling case, so it takes felling's.
            Vector3 stand = WorkStance.StandAt(
                cell, cell, Vector3.forward, 1f, strike, WorkStyle.Felling.AimFromCentre);

            float offset = Vector3.Distance(Flat(stand), Flat(cell));
            Assert.That(offset, Is.GreaterThanOrEqualTo(WorkStance.MinimumStandOff),
                "a fully weighted work stance stands at least the minimum off its work centre");

            const float ClickBoxHalfWidth = 1.15f * 0.5f;
            Assert.That(offset, Is.GreaterThan(ClickBoxHalfWidth * 0.8f),
                $"the figure stands {offset:0.00} m off the cell against a click box reaching " +
                $"{ClickBoxHalfWidth:0.00} m, so a box built from the pose cannot reliably contain " +
                "the person it is meant to select");
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    
        [Test]
        public void MiningAimsJustInsideTheRockFaceAndNotAtItsMiddle()
        {
            // The bug 12-work-poses-and-tools.md was written to catch. Felling aims 0.15 m past
            // the centre of the cell, which is about the middle of a trunk six-tenths of a metre
            // through. A rock CELL is 2.5 m through: aim 0.15 m past its centre and the pick
            // finishes 1.1 m inside solid stone, with the chips spawning in there with it, so the
            // head vanishes and the burst is never seen. MinimumStandOff does not save it — the
            // figure solves well outside that floor and still has its pick buried.
            float half = CellMetrics.SizeXZ * 0.5f;
            float aim = WorkStyle.Mining.AimFromCentre;

            Assert.That(aim, Is.LessThan(half), "mining aims outside the cell it is cutting");
            Assert.That(half - aim, Is.LessThan(0.3f),
                $"mining aims {half - aim:F2} m inside the face, which is a buried head again");
            Assert.That(aim, Is.GreaterThan(WorkStyle.Felling.AimFromCentre),
                "mining aims no further out than felling, so it is still aiming at the middle of the block");
        }

        [Test]
        public void EveryStyleAimsInsideItsOwnCellFromAnyApproach()
        {
            // Expressed as a distance from the centre rather than as a face point, and this is
            // why: on the diagonal the cell boundary is 1.77 m out rather than 1.25 m, so one
            // number is inside the cell from every approach angle where a chosen axis would not be.
            float halfDiagonal = CellMetrics.SizeXZ * 0.5f * Mathf.Sqrt(2f);

            foreach (WorkStyle style in WorkStyle.All)
            {
                Assert.That(style.AimFromCentre, Is.GreaterThan(0f),
                    "an aim at or behind the centre puts the head through to the far side");
                Assert.That(style.AimFromCentre, Is.LessThan(halfDiagonal),
                    "the aim leaves the cell when the worker stands on the diagonal");
            }
        }

        // ---- the builder's hammer ----------------------------------------------------------

        [Test]
        public void EveryStyleNamesAToolAndSomethingToThrow()
        {
            // A typo'd module id degrades in silence into bare-handed work, which is exactly what a
            // clone without the packs looks like, so nobody would ever suspect the id. An empty
            // chip recipe degrades into a blow with nothing coming off it, which reads as a
            // colonist waving a tool near a thing. Both are invisible in review.
            Assert.That(WorkStyle.All.Length, Is.EqualTo(WorkStyle.Count),
                "Count sizes the per-figure tool table; a style missing from it is never fitted");

            foreach (WorkStyle style in WorkStyle.All)
            {
                Assert.That(style.ToolModule, Is.Not.Null.And.Not.Empty);
                Assert.That(style.Chips.IsSomething, Is.True,
                    $"{style.ToolModule} lands with nothing coming off it");
            }
        }

        [Test]
        public void HammerIsSwungFromTheElbowWhereAnAxeIsSwungFromTheShoulder()
        {
            // The whole argument for the hammer being its own stroke rather than a fast axe, and
            // the one thing about it that is a measurement rather than taste: its haft is 0.63 m
            // against the axe's 0.74 (synty-inventory.csv). A short tool is swung from the elbow.
            //
            // So the shoulder comes back LESS (a smaller magnitude, and the angles are negative
            // because an arm hangs down) while the elbow cocks MORE. Copying the axe and merely
            // shortening the period would keep the long arc, and the figure would read as swinging
            // a hammer it wished were an axe.
            WorkStroke hammer = WorkStroke.Hammer;
            WorkStroke axe = WorkStroke.Axe;

            Assert.That(hammer.Raised.Shoulder, Is.GreaterThan(axe.Raised.Shoulder),
                "the hammer is brought back beside the ear, not behind the back");
            Assert.That(hammer.Raised.Elbow, Is.LessThan(axe.Raised.Elbow),
                "a short haft is cocked harder at the elbow to make up the arc");
        }

        [Test]
        public void HammerBeatsFasterAndRestsLessThanEitherOtherTool()
        {
            // Driving a frame together is a quick repeated tap; felling is an unhurried rhythm.
            Assert.That(WorkStroke.Hammer.StrokeSeconds, Is.LessThan(WorkStroke.Pick.StrokeSeconds));
            Assert.That(WorkStroke.Pick.StrokeSeconds, Is.LessThan(WorkStroke.Axe.StrokeSeconds));

            // A later StrikeEnds is a shorter dwell, because the dwell runs from there to the end.
            // An axe buries itself in the wood and rests; a pick bites and rebounds; a hammer
            // bounces off hardest of all. Until a stroke can recoil, spending almost no time at the
            // bottom is the only honest way to say so.
            Assert.That(WorkStroke.Hammer.StrikeEnds, Is.GreaterThan(WorkStroke.Pick.StrikeEnds));
            Assert.That(WorkStroke.Pick.StrikeEnds, Is.GreaterThan(WorkStroke.Axe.StrikeEnds));
        }

        [Test]
        public void HammerSwingsNearerTheAxesPlaneThanThePicks()
        {
            // The owner's brief was "akin to chopping rather than to mining" (2026-09-16), and this
            // is the number that carries it: an axe comes across the body at -30, a pick goes down
            // the midline at -8. The hammer belongs between them and on the axe's side of the gap.
            Assert.That(WorkStyle.Building.Tilt, Is.LessThan(WorkStyle.Mining.Tilt),
                "a hammer travels across the body; it does not come down the midline like a pick");
            Assert.That(WorkStyle.Building.Tilt, Is.GreaterThan(WorkStyle.Felling.Tilt),
                "a short haft cannot travel as far round as a felling axe without leaving the plane");
        }

        [Test]
        public void HammerAimsAtTheNearFaceLikeMiningAndNotAtTheCentreLikeFelling()
        {
            // The AimFromCentre bug, which would have been made a second time by copying the wrong
            // one of the two existing styles. A tree is 0.6 m through and its cell is mostly air,
            // so felling aims just past the middle. A wall under construction fills its cell, as
            // rock does, so an aim past the middle finishes a metre inside the timber and the head
            // — and the chips with it — are never seen.
            Assert.That(WorkStyle.Building.AimFromCentre,
                Is.EqualTo(WorkStyle.Mining.AimFromCentre).Within(0.001f));
            Assert.That(WorkStyle.Building.AimFromCentre,
                Is.GreaterThan(WorkStyle.Felling.AimFromCentre));
        }

        [Test]
        public void BuildingIsTheStyleABuilderIsSeenIn()
        {
            // This replaces a tripwire. Until the build pipeline landed nothing in the simulation
            // built, the hammer was reachable only through PawnFigureDirector.StyleOverride, and a
            // test asserted that no job mapped to it — with a message saying to add the row to
            // IndexForJob and delete the test the day building became real. It has, the row is
            // there, and the tripwire fired on 2026-09-17 exactly as it was written to.
            //
            // What is worth asserting now is the row itself, and that it is the *only* one: an
            // `IndexForJob` that answered building for everything would satisfy the first half.
            Assert.That(WorkStyle.IndexForJob(JobHandle.Build), Is.EqualTo(WorkStyle.BuildingIndex),
                "a colonist raising a wall swings the hammer");
            Assert.That(WorkStyle.IndexForJob(JobHandle.Mine), Is.EqualTo(WorkStyle.MiningIndex),
                "and a miner still swings the pick");
        }

        [Test]
        public void TimberIsItsOwnRecipeAndNotFellingChips()
        {
            // A hammer driving a joint together is not cutting anything, so what comes off is a
            // little dry splintering rather than the shavings an axe peels across the grain.
            Assert.That(ChipRecipe.Timber.Count, Is.LessThan(ChipRecipe.Wood.Count));
            Assert.That(ChipRecipe.Timber.Life.y, Is.LessThan(ChipRecipe.Wood.Life.y));
            Assert.That(ChipRecipe.Timber.Spread, Is.LessThan(ChipRecipe.Wood.Spread));
        }

        // ---- aiming the stroke downward ----------------------------------------------------

        [Test]
        public void DippingBowsTheBackAndCarriesTheArmWithIt()
        {
            // The whole of the mechanism, and the thing that is easy to get wrong. The director
            // gives the back `Spine` and the upper arm `Shoulder - Spine`, so that Shoulder means
            // the arm's pitch against the WORLD. Adding the dip to both therefore leaves the arm's
            // angle against the chest untouched and lowers the pair together, which is a person
            // leaning over a hole. Adding it to the shoulder alone would lower the arm against a
            // chest that stayed upright, which is a person pointing at the floor.
            var level = new WorkSwing(-52f, -16f, 22f);
            WorkSwing dipped = level.Dipped(45f);

            Assert.That(dipped.Spine, Is.EqualTo(level.Spine + 45f).Within(1e-4f),
                "the back did not fold any further into the blow");
            Assert.That(dipped.Shoulder - dipped.Spine, Is.EqualTo(level.Shoulder - level.Spine).Within(1e-4f),
                "the arm changed its angle against the chest, so it is pointing rather than leaning");
            Assert.That(dipped.Shoulder, Is.EqualTo(level.Shoulder + 45f).Within(1e-4f),
                "the arm's pitch against the world did not come down by the dip");
        }

        [Test]
        public void DippingLeavesTheElbowAlone()
        {
            // A local bend: how far the forearm is cocked against the upper arm means the same
            // thing whatever the rest of the body is doing. Dipping it too would straighten the
            // arm as the aim lowered, and the tool would arrive at a different distance from the
            // fist depending on how deep the work was.
            var level = new WorkSwing(-52f, -16f, 22f);
            Assert.That(level.Dipped(45f).Elbow, Is.EqualTo(level.Elbow).Within(1e-4f));
        }

        [Test]
        public void NoDipIsTheSamePoseBackAgain()
        {
            var level = new WorkSwing(-140f, -68f, -10f);
            WorkSwing same = level.Dipped(0f);
            Assert.That(same.Shoulder, Is.EqualTo(level.Shoulder));
            Assert.That(same.Elbow, Is.EqualTo(level.Elbow));
            Assert.That(same.Spine, Is.EqualTo(level.Spine));
        }

        [Test]
        public void OnlyMiningAimsDownward()
        {
            // A tree and the colonist felling it stand on the same floor, always. A miner cutting
            // the layer below does not, which is the one case that needed this.
            Assert.That(WorkStyle.Felling.Dip, Is.EqualTo(0f),
                "felling acquired a downward aim it has no use for");
            Assert.That(WorkStyle.Mining.Dip, Is.GreaterThan(0f),
                "mining cannot reach stone a layer below its feet");
        }

        [Test]
        public void TheDipIsBigEnoughToReachTheFloorAndNotSoBigItGoesThroughIt()
        {
            // Derived rather than dialled: the edge lands about 1.34 m up and 1.73 m in front of a
            // chain pivoting around the base of the spine, roughly a metre up. Bringing a point
            // 1.76 m out and 0.34 m above that pivot down to a metre below it wants some 46
            // degrees. This brackets that reasoning; PawnFigureDirector.MeasuredDippedBladeHeight
            // is what actually settles it, on a real rig, and DescribeTools prints it.
            Assert.That(WorkStyle.Mining.Dip, Is.GreaterThan(25f),
                "too shallow: the pick still passes over rock that is level with the boots");
            Assert.That(WorkStyle.Mining.Dip, Is.LessThan(70f),
                "too deep: the figure is folded double and the pick goes through the floor");
        }

        [Test]
        public void TheDippedStrokeIsStillAStrokeAndNotAHeldPose()
        {
            // Dipping must move the whole curve, not flatten it: a raise and a strike that end up
            // at the same angles are a miner holding a pick against a rock.
            // Off Raised and Struck rather than off At(0) and At(1): Stroke(0) is 1, so phase
            // nought is the moment the head is IN the work and the two would be the same pose.
            WorkStroke pick = WorkStroke.Pick;
            WorkSwing raised = pick.Raised.Dipped(WorkStyle.Mining.Dip);
            WorkSwing struck = pick.Struck.Dipped(WorkStyle.Mining.Dip);

            Assert.That(Mathf.Abs(struck.Shoulder - raised.Shoulder), Is.GreaterThan(45f),
                "the dipped stroke has no travel left in it");
        }
    }
}
