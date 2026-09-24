#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The stair gait's own claims, in two halves.
    ///
    /// <para><b>The curve, asked directly.</b> <see cref="StairGait.Lift"/> and
    /// <see cref="StairGait.Hold"/> are pure functions, and everything the integration depends
    /// on is stated about them here first: never negative, zero at both ends and at every tread
    /// boundary, amplitude a fixed share of one tread however tall the bank.</para>
    ///
    /// <para><b>The pose, asked through <see cref="PawnPose"/> on the same terrace fixture
    /// <c>BankFootingTests</c> walks.</b> The glide that fixture measured is still down there
    /// underneath the stair — the assertions compare the stair on against the stair off at the
    /// same instant, so what is being checked is exactly the modulation and nothing it rides
    /// on. The smoothness budget itself stays where it was: <c>BankFootingTests</c>'s
    /// worst-jump tests now run with the stair on, and passing unchanged is the proof that a
    /// step is not a jolt (the faults that killed the parabola and the strides measured an
    /// order of magnitude above it).</para>
    /// </summary>
    public class StairGaitTests
    {
        [SetUp]
        public void Reset()
        {
            BankLayout.Reset();
            StairGait.Reset();
        }

        [TearDown]
        public void Restore()
        {
            BankLayout.Reset();
            StairGait.Reset();
            GroundRelief.Reset();
        }

        static RenderTestWorld Terrace() =>
            RenderTestWorld.Terrace(rise: 1, stepTerrain: NaturalContent.TerrainGrass);

        static CellRef Bank(int z = 3) => new CellRef(3, z, 2);

        static PawnView Standing(CellRef cell) => Walking(cell, cell, 0);

        static PawnView Walking(CellRef from, CellRef to, int percent) =>
            new PawnView(new PawnId(1), from, 100, 100, 50, -1, to, percent,
                movePerMille: percent * 10);

        static PawnView WalkingPerMille(CellRef from, CellRef to, int perMille) =>
            new PawnView(new PawnId(1), from, 100, 100, 50, -1, to, perMille / 10,
                movePerMille: perMille);

        /// <summary>The drawn height at one instant, with the stair on or off. Sampling the off
        /// pose first for every point keeps the two comparable even though <c>Enabled</c> is a
        /// switch on the whole class.</summary>
        static float HeightAt(RenderTestWorld world, CellRef from, CellRef to, int perMille, bool stair)
        {
            StairGait.Enabled = stair;
            return PawnPose.Of(WalkingPerMille(from, to, perMille), 0f, 0, out _, world.Model).y;
        }

        // ------------------------------------------------------------------ the curve

        [Test]
        public void OneTreadIsHalfAMetreAndTheRiseFillsWholeOnes()
        {
            Assert.That(StairGait.Lift(1.5f, 0.21f), Is.GreaterThan(0f),
                "a climb is being drawn as nothing at all");
            // rise 1.5 at tread 0.5 is three treads: the lift touches zero four times.
            for (int tread = 0; tread <= 3; tread++)
                Assert.That(StairGait.Lift(1.5f, tread / 3f), Is.EqualTo(0f).Within(1e-5f),
                    $"the lift has not landed on the ramp at tread boundary {tread}");
        }

        [Test]
        public void NeitherCurveDipsBelowTheRampByMoreThanASole()
        {
            // Both curves open or close their movement a hair under the linear climb: the lift's
            // cosine *starts* with zero slope while the ramp is already climbing (up to 8 mm
            // under, at each tread's start), and the hold's cosine *ends* with zero slope while
            // the ramp is still descending (about 1 mm under, at each tread's end). The ground
            // clamp in OnTheDrawnGround turns both into landings rather than sinkings, and this
            // bound keeps the hairs hairs: if either dip ever grows past a sole's width, the
            // curve has stopped being a modulation of the ramp and started being a hole in it.
            float deepestLift = 0f, deepestHold = 0f;
            for (int i = 0; i <= 1000; i++)
            {
                float u = i / 1000f;
                deepestLift = Mathf.Min(deepestLift, StairGait.Lift(1.5f, u));
                deepestHold = Mathf.Min(deepestHold, StairGait.Hold(1.5f, u));
            }

            Assert.That(deepestLift, Is.GreaterThanOrEqualTo(-0.02f),
                $"the lift dipped {-deepestLift * 1000f:F1} mm below the ramp, past a sole's width");
            Assert.That(deepestHold, Is.GreaterThanOrEqualTo(-0.02f),
                $"the hold dipped {-deepestHold * 1000f:F1} mm below the ramp, past a sole's width");
        }

        [Test]
        public void TheHoldAlsoEndsOnTheRamp()
        {
            Assert.That(StairGait.Hold(1.5f, 0f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(StairGait.Hold(1.5f, 1f), Is.EqualTo(0f).Within(1e-5f));
            for (int tread = 0; tread <= 3; tread++)
                Assert.That(StairGait.Hold(1.5f, tread / 3f), Is.EqualTo(0f).Within(1e-5f),
                    $"a stepped descent is still holding at tread boundary {tread}");
        }

        [Test]
        public void AFlatHalfDrawsNoStairAtAll()
        {
            Assert.That(StairGait.Lift(0f, 0.5f), Is.EqualTo(0f));
            Assert.That(StairGait.Lift(0.04f, 0.5f), Is.EqualTo(0f), "rounding on nothing");
            Assert.That(StairGait.Hold(0f, 0.5f), Is.EqualTo(0f));
            Assert.That(StairGait.Lift(1.5f, -0.5f), Is.EqualTo(0f), "before the half begins");
        }

        [Test]
        public void TallerBanksGetMoreTreadsNotTallerOnes()
        {
            // The lead over the ramp is a fixed fraction of one tread, whatever the rise: a
            // three-metre bank is six small steps, not one enormous one. The exact peak is the
            // cosine's own business (it settles at about 0.62 of a tread); what is pinned is
            // that every rise pays the same lead and none pays a whole tread.
            float first = float.NaN;
            foreach (float rise in new[] { 1.5f, 3f, 4.5f })
            {
                float worst = 0f;
                for (int i = 0; i <= 1000; i++)
                    worst = Mathf.Max(worst, StairGait.Lift(rise, i / 1000f));

                if (float.IsNaN(first)) first = worst;
                Assert.That(worst, Is.EqualTo(first).Within(1e-3f),
                    $"a rise of {rise} scales its treads differently from a rise of 1.5");
                Assert.That(worst, Is.LessThan(StairGait.TreadHeight),
                    $"a rise of {rise} leads the ramp by more than a whole tread");
            }
        }

        [Test]
        public void TheStairsWorstFrameStaysInsideTheTeleportBudget()
        {
            // The glide's frame-to-frame claim ("a rhythm rather than a steady climb",
            // HopArcTests) is deliberately given up — a stair gait is a rhythm, that is the
            // feature. What is not given up is the ceiling that predates it: no frame of the
            // crossing may move the figure past the 50 mm teleport budget BankFootingTests
            // polices per mille, measured here at the tick rate the step is drawn at.
            RenderTestWorld world = Terrace();
            int ticks = MoveCost.JumpUp;

            Vector3 previous = PawnPose.Of(WalkingPerMille(Bank(), new CellRef(2, 3, 3), 0),
                0f, 0, out _, world.Model);
            float worst = 0f;
            for (int tick = 1; tick <= ticks; tick++)
            {
                Vector3 at = PawnPose.Of(
                    WalkingPerMille(Bank(), new CellRef(2, 3, 3), tick * 1000 / ticks),
                    0f, 0, out _, world.Model);
                worst = Mathf.Max(worst, Vector3.Distance(at, previous));
                previous = at;
            }

            Assert.That(worst, Is.LessThan(0.05f),
                $"a frame of the stair moves the figure {worst * 1000f:F0} mm, past the " +
                "teleport budget");
        }

        [Test]
        public void TheKneeLiftFadesInWhereThePlantingFadesOut()
        {
            const float reach = 0.32f;
            Assert.That(StairGait.KneeLift(0.5f, 0f, reach), Is.EqualTo(0f), "a planted foot");
            Assert.That(StairGait.KneeLift(0.5f, reach * 0.5f, reach), Is.EqualTo(0f),
                "still inside the planting band");
            Assert.That(StairGait.KneeLift(0.5f, -0.1f, reach), Is.EqualTo(0f), "a buried foot");
            Assert.That(StairGait.KneeLift(0.5f, reach, reach),
                Is.EqualTo(0.5f * StairGait.KneeLiftShare).Within(1e-4f),
                "a fully swung foot clears half a tread");

            float previous = 0f;
            for (int i = 0; i <= 20; i++)
            {
                float airGap = reach * 0.5f + (reach * 0.5f) * i / 20f;
                float lift = StairGait.KneeLift(0.5f, airGap, reach);
                Assert.That(lift, Is.GreaterThanOrEqualTo(previous - 1e-6f),
                    "the knee lift comes on non-monotonically");
                previous = lift;
            }
        }

        // ------------------------------------------------------------------ the pose

        [Test]
        public void ClimbingABankTheFigureIsNeverInsideItAndSometimesAboveIt()
        {
            RenderTestWorld world = Terrace();
            float highest = 0f;
            for (int perMille = 0; perMille <= 1000; perMille++)
            {
                float glide = HeightAt(world, Bank(), new CellRef(2, 3, 3), perMille, stair: false);
                float stair = HeightAt(world, Bank(), new CellRef(2, 3, 3), perMille, stair: true);
                Assert.That(stair, Is.GreaterThanOrEqualTo(glide - 1e-4f),
                    $"at {perMille} per mille the stair drew the figure inside the bank");
                highest = Mathf.Max(highest, stair - glide);
            }

            Assert.That(highest, Is.GreaterThan(0.1f),
                "the stair never led the ramp by a visible amount, so it is not a stair");
        }

        [Test]
        public void TheClimbNeverOnceMovesDownhill()
        {
            RenderTestWorld world = Terrace();
            var from = Bank();
            var to = new CellRef(2, 3, 3);

            float previous = HeightAt(world, from, to, 0, stair: true);
            for (int perMille = 1; perMille <= 1000; perMille++)
            {
                float y = HeightAt(world, from, to, perMille, stair: true);
                Assert.That(y, Is.GreaterThanOrEqualTo(previous - 1e-4f),
                    $"at {perMille} per mille the figure moved downhill while climbing " +
                    $"({(previous - y) * 1e3f:F1} mm)");
                previous = y;
            }

            // And it arrives where standing arrives, on the top of the step.
            float stood = PawnPose.Of(Standing(to), 0f, 0, out _, world.Model).y;
            Assert.That(previous, Is.EqualTo(stood).Within(1e-3f),
                "the stair did not land on the surface it arrives at");
        }

        [Test]
        public void DroppingOffABankStepsDownWithoutEverRising()
        {
            RenderTestWorld world = Terrace();
            var from = new CellRef(2, 3, 3);
            var to = Bank();

            float previous = HeightAt(world, from, to, 0, stair: true);
            float highestHold = 0f;
            for (int perMille = 1; perMille <= 1000; perMille++)
            {
                float y = HeightAt(world, from, to, perMille, stair: true);
                Assert.That(y, Is.LessThanOrEqualTo(previous + 1e-4f),
                    $"at {perMille} per mille the figure moved uphill while descending");
                previous = y;

                float glide = HeightAt(world, from, to, perMille, stair: false);
                highestHold = Mathf.Max(highestHold, y - glide);
            }

            Assert.That(highestHold, Is.GreaterThan(0.1f),
                "the descent never held above the ramp, so it is still a fall");
            float stood = PawnPose.Of(Standing(to), 0f, 0, out _, world.Model).y;
            Assert.That(previous, Is.EqualTo(stood).Within(1e-3f),
                "a stepped descent does not arrive on the surface it arrives at");
        }

        [Test]
        public void WalkingOnTheFlatIsUntouchedByAnyOfIt()
        {
            RenderTestWorld world = Terrace();
            var from = new CellRef(5, 3, 2);
            var to = new CellRef(6, 3, 2);

            for (int perMille = 0; perMille <= 1000; perMille += 10)
                Assert.That(
                    HeightAt(world, from, to, perMille, stair: true),
                    Is.EqualTo(HeightAt(world, from, to, perMille, stair: false)).Within(1e-6f),
                    $"the stair moved a figure on flat ground at {perMille} per mille");
        }

        [Test]
        public void WithoutAWorldNothingChangesAtAll()
        {
            // The arithmetic fixtures in PawnPoseTests must go on meaning what they meant, the
            // same bargain BankFootingTests already holds with the bank lift: no mirror, no
            // bank, no stair — the plain glide between two floor centres.
            var from = new CellRef(3, 3, 2);
            var to = new CellRef(3, 4, 2);

            for (int perMille = 0; perMille <= 1000; perMille += 10)
            {
                Vector3 at = PawnPose.Of(WalkingPerMille(from, to, perMille), 0f, 0, out _, null);
                float expected = Mathf.Lerp(
                    CellMetrics.FloorCentre(from).y, CellMetrics.FloorCentre(to).y, perMille / 1000f);
                Assert.That(at.y, Is.EqualTo(expected).Within(1e-5f),
                    $"with no world the stair moved the figure at {perMille} per mille");
            }
        }

        [Test]
        public void TheClimbQuestionHasOneOwnerAndItIncludesTheHop()
        {
            RenderTestWorld world = Terrace();

            Assert.That(StairGait.IsClimbing(world.Model, WalkingPerMille(Bank(), new CellRef(2, 3, 3), 400)),
                Is.True, "a hop up out of a bank cell is the stair climb");
            Assert.That(StairGait.IsClimbing(world.Model, WalkingPerMille(new CellRef(4, 3, 2), Bank(), 400)),
                Is.True, "stepping onto the foot of a bank is climbing its lower half");
            Assert.That(StairGait.IsClimbing(world.Model, WalkingPerMille(new CellRef(2, 3, 3), Bank(), 400)),
                Is.False, "a descent is not a climb");
            Assert.That(StairGait.IsClimbing(world.Model, WalkingPerMille(new CellRef(5, 3, 2), new CellRef(6, 3, 2), 400)),
                Is.False, "flat ground is not a climb");
            Assert.That(StairGait.IsClimbing(world.Model, Standing(Bank())),
                Is.False, "a standing pawn is climbing nothing");
            Assert.That(StairGait.IsClimbing(null, WalkingPerMille(Bank(), new CellRef(2, 3, 3), 400)),
                Is.False, "no mirror, no question");
        }
    }
}
