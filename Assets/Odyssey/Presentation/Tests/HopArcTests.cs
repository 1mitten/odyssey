#nullable enable
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// How a figure is drawn across a step that changes layer.
    ///
    /// <para>A hop was a straight line between two cell centres raised onto the ground, which drew
    /// a climb as a slide up the bank and a drop as a slide back down it. The owner saw the first
    /// in play on 2026-09-18 — <i>"the colonists looked too fast going up definitely … should be
    /// much slower"</i> — and the price moved with it: <c>MoveCost.JumpUp</c> 135 → 240, derived in
    /// its own comment from the length of the drawn path.</para>
    ///
    /// <para><b>The two halves have to be tested together, because either alone is a known
    /// fault.</b> A slow slide up a hillside is the "colonist stuck on a hill" that the previous
    /// retune of that constant produced and was rejected for; a fast arc is the thing just
    /// reported. So these pin the shape and <c>VerticalMovementTests</c> pins the price, and the
    /// bounds in the constant's own comment are what tie the two together.</para>
    /// </summary>
    public class HopArcTests
    {
        /// <summary>The composition root's fixed tick rate — <c>OdysseyBootstrap.ticksPerSecond</c>.</summary>
        const int TicksPerSecond = 60;

        const float Tolerance = 1e-4f;

        [SetUp]
        public void Reset()
        {
            BankLayout.Reset();
            GroundRelief.Reset();
            StairGait.Reset();
        }

        [TearDown]
        public void Restore()
        {
            BankLayout.Reset();
            GroundRelief.Reset();
            StairGait.Reset();
        }

        // ------------------------------------------------------------------ the curves

        [Test]
        public void TheFallStartsAndFinishesExactlyOnTheGround()
        {
            // The one property that cannot be traded for a nicer shape. The simulation says the
            // pawn is in the leaving cell at 0 and the arriving cell at 1; a curve that does not
            // agree puts a jolt at one end of every step, which reads as a teleport and gets
            // blamed on the animation.
            Assert.That(HopArc.Fall(0f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(HopArc.Fall(1f), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void AFallNeverRisesAgain()
        {
            float fall = 0f;
            for (int i = 0; i <= 400; i++)
            {
                float next = HopArc.Fall(i / 400f);
                Assert.That(next, Is.GreaterThanOrEqualTo(fall - Tolerance), $"the fall rose again at {i / 400f}");
                fall = next;
            }
        }

        [Test]
        public void AFallAccelerates()
        {
            float previous = 0f, lastGain = -1f;
            for (int i = 1; i <= 100; i++)
            {
                float t = HopArc.StepOff + (1f - HopArc.StepOff) * (i / 100f);
                float now = HopArc.Fall(t);
                float gain = now - previous;
                Assert.That(gain, Is.GreaterThanOrEqualTo(lastGain - Tolerance),
                    $"the fall stopped accelerating at {t}");
                previous = now;
                lastGain = gain;
            }
        }

        [Test]
        public void AFallIsAtTheSpeedOfGravity()
        {
            // **This is the test that couples the curve to the price**, and it is the reason
            // HopArc.StepOff is 0.06 rather than a number somebody liked. A drop costs 50, which is
            // 50 ticks, which is five sixths of a second; a three-metre free fall takes 0.78 s; the
            // difference is the step off the edge. Retune MoveCost.Drop and this fails, instead of
            // colonists quietly starting to fall at the wrong speed.
            float step = MoveCost.Drop / (float)TicksPerSecond;
            float falling = step * (1f - HopArc.StepOff);
            float acceleration = 2f * CellMetrics.SizeY / (falling * falling);

            Assert.That(acceleration, Is.EqualTo(9.81f).Within(1f),
                $"a drop of {CellMetrics.SizeY} m over {falling:F2} s is {acceleration:F2} m/s², " +
                "which is not falling");
        }

        // ------------------------------------------------------------------ on the board

        /// <summary>
        /// The terrace of <c>BankFootingTests</c>: high ground at x &lt; 3, low ground beyond it,
        /// so (3, z, 2) is the cell at the foot of the step and (2, z, 3) is the ground above it.
        /// </summary>
        static RenderTestWorld Terrace() =>
            RenderTestWorld.Terrace(rise: 1, stepTerrain: NaturalContent.TerrainGrass);

        /// <summary>
        /// A step of bare rock: earth spills down a step and stone does not, so no bank is drawn
        /// and there is nothing to tread on. The other half of every climb test.
        /// </summary>
        static RenderTestWorld SheerFace()
        {
            const int n = 6;
            var world = new RenderTestWorld(n, n, 8);
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int top = x < 3 ? 2 : 1;
                for (int y = 0; y <= top; y++) world.Solid(x, z, y, CoreContent.TerrainRock);
            }
            world.Publish();

            Assert.That(BankLayout.At(world.Model, new CellRef(3, 3, 2)).Exists, Is.False,
                "the fixture grew a bank against rock, so it is not testing a sheer face");
            return world;
        }

        static readonly CellRef Foot = new CellRef(3, 3, 2);
        static readonly CellRef Top = new CellRef(2, 3, 3);

        static PawnView Standing(CellRef cell) => Walking(cell, cell, 0);

        static PawnView Walking(CellRef from, CellRef to, int percent) =>
            new PawnView(new PawnId(1), from, 100, 100, 50, -1, to, percent,
                movePerMille: percent * 10);

        /// <summary>
        /// The pawn as the snapshot publishes it part way through a step: per mille, which is the
        /// resolution the figure is drawn at and roughly what one tick of a dear step advances.
        /// See <see cref="PawnView.MovePerMille"/> for why a percent is not fine enough.
        /// </summary>
        static PawnView WalkingPerMille(CellRef from, CellRef to, int perMille) =>
            new PawnView(new PawnId(1), from, 100, 100, 50, -1, to, perMille / 10,
                movePerMille: perMille);

        static Vector3 DrawnAt(RenderTestWorld world, CellRef from, CellRef to, int perMille) =>
            PawnPose.Of(WalkingPerMille(from, to, perMille), 0f, 0, out _, world.Model);

        /// <summary>The drawn position at a tick of a step that costs <paramref name="ticks"/>.</summary>
        static Vector3 DrawnAtTick(RenderTestWorld world, CellRef from, CellRef to, int tick, int ticks) =>
            PawnPose.Of(WalkingPerMille(from, to, Mathf.RoundToInt(tick * 1000f / ticks)),
                0f, 0, out _, world.Model);

        static Vector3 DrawnStanding(RenderTestWorld world, CellRef cell) =>
            PawnPose.Of(Standing(cell), 0f, 0, out _, world.Model);

        [Test]
        public void AThereIsAStepToHopUp()
        {
            // The fixture's own claim: every test below is vacuous without it.
            RenderTestWorld world = Terrace();
            Assert.That(NavGraph.IsHop(Foot, Top), Is.True, "the fixture's two cells are not a hop apart");
            Assert.That(BankLayout.At(world.Model, Foot).Exists, Is.True,
                "the cell at the foot of the step has no bank in it");
        }

        [Test]
        public void AHopBeginsAndEndsWhereTheFigureStands()
        {
            // Continuity with the standing pose, which is the property a moving figure shows and a
            // photograph does not. Both ends, both directions.
            RenderTestWorld world = Terrace();

            Assert.That(DrawnAt(world, Foot, Top, 0).y,
                Is.EqualTo(DrawnStanding(world, Foot).y).Within(0.01f),
                "a climb does not start where the colonist was standing");
            Assert.That(DrawnAt(world, Foot, Top, 1000).y,
                Is.EqualTo(DrawnStanding(world, Top).y).Within(0.01f),
                "a climb does not end on the ground it climbed to");
            Assert.That(DrawnAt(world, Top, Foot, 0).y,
                Is.EqualTo(DrawnStanding(world, Top).y).Within(0.01f),
                "a drop does not start where the colonist was standing");
            Assert.That(DrawnAt(world, Top, Foot, 1000).y,
                Is.EqualTo(DrawnStanding(world, Foot).y).Within(0.01f),
                "a drop does not land on the ground below it");
        }

        [Test]
        public void AClimbUpTheBoardStaysOnTheHillside()
        {
            // The same claim as the curve's, made where the owner is looking: across a real
            // terrace, the figure never rises above the ground it is climbing on to.
            RenderTestWorld world = Terrace();
            float landing = DrawnStanding(world, Top).y;

            float highest = float.MinValue;
            for (int perMille = 0; perMille <= 1000; perMille++)
                highest = Mathf.Max(highest, DrawnAt(world, Foot, Top, perMille).y);

            Assert.That(highest, Is.LessThanOrEqualTo(landing + 0.01f),
                $"the figure is drawn {(highest - landing) * 100f:F0} cm above the step it is " +
                "climbing on to, which reads as a jump rather than as walking up it");
        }

        [Test]
        public void AClimbIsDrawnOnTheRampAndNowhereElse()
        {
            // **"Motions exactly just above the terrace surface"** (owner, 2026-09-19). The figure
            // is the bank's own surface, sampled where it stands — not a curve fitted to it, not
            // strides quantised off it, not an arc over it. Every departure from this has been
            // reported: a parabola read as jumping, strides read as jolting.
            //
            // **This is the glide's own claim, pinned with the stair off** (design 40): the stair
            // gait departs from the surface on purpose, by up to two thirds of a tread between
            // treads, and its own surface relationship — never inside, touching down at every
            // tread boundary — lives in `StairGaitTests`, asked with the stair on. What this
            // keeps holding is that the surface-following underneath the stair is still exact.
            StairGait.Enabled = false;
            RenderTestWorld world = Terrace();

            for (int perMille = 0; perMille <= 1000; perMille++)
            {
                Vector3 at = DrawnAt(world, Foot, Top, perMille);
                float surface = FloorUnder(world, Foot, Top, at);

                Assert.That(at.y, Is.EqualTo(surface).Within(1e-3f),
                    $"at {perMille} per mille the figure is {(at.y - surface) * 1000f:F0} mm off " +
                    "the surface it is walking on");
            }
        }

        [Test]
        public void AClimbIsSmoothFrameToFrameAllTheWayUp()
        {
            // "It jolts and jitters the colonists at certain points; smoother is preferred and
            // predictable." So: while it is on the ramp, every frame moves the figure the same
            // distance. This measures the *variation* and not just the maximum, because a
            // hold-and-push rhythm passes a maximum test and is exactly what was complained about.
            //
            // **The glide's claim, pinned with the stair off** (design 40): a stair gait is a
            // rhythm on purpose — the owner's plan asked for steps — so the variation bound here
            // would forbid the feature rather than guard it. The stair's own frame budget, the
            // 50 mm teleport ceiling that predates it, is what `StairGaitTests` holds it to.
            StairGait.Enabled = false;
            RenderTestWorld world = Terrace();

            Vector3 footCentre = CellMetrics.FloorCentre(Foot), topCentre = CellMetrics.FloorCentre(Top);
            float boundary = (footCentre.x + topCentre.x) * 0.5f;

            float most = 0f, least = float.MaxValue, anywhere = 0f;
            Vector3 previous = DrawnAtTick(world, Foot, Top, 0, MoveCost.JumpUp);

            for (int tick = 1; tick <= MoveCost.JumpUp; tick++)
            {
                Vector3 at = DrawnAtTick(world, Foot, Top, tick, MoveCost.JumpUp);
                float moved = Vector3.Distance(at, previous);
                anywhere = Mathf.Max(anywhere, moved);

                // Wholly on the ramp: both ends of the frame are still short of the boundary. The
                // one frame that straddles it is spending part of its time climbing at 0.6 m/s and
                // part walking the top at 1.5, so it moves further than either — that is the speeds
                // meeting, which is what the terrain does there, and not a rhythm in the climb.
                bool onTheRamp = at.x > boundary && previous.x > boundary;
                if (!onTheRamp) { previous = at; continue; }

                most = Mathf.Max(most, moved);
                least = Mathf.Min(least, moved);
                previous = at;
            }

            Assert.That(anywhere, Is.LessThan(0.05f),
                $"a frame of the step moves the figure {anywhere * 1000f:F0} mm, past what a frame " +
                "of walking moves");
            Assert.That(most, Is.LessThan(least * 1.5f),
                $"on the ramp the climb moves between {least * 1000f:F0} mm and {most * 1000f:F0} mm " +
                "a frame, which is a rhythm rather than a steady climb");

            TestContext.WriteLine($"ramp: {least * 1000f:F1}-{most * 1000f:F1} mm a frame, " +
                                  $"worst anywhere in the step {anywhere * 1000f:F1} mm");
        }

        [Test]
        public void NothingIsEverDrawnInsideTheHillside()
        {
            // The clamp. A hop's chord passes a metre and a half inside the block being climbed,
            // and the fall passes below the bank it drops past — both are pushed back out onto the
            // drawn surface. Four hundred samples each way, the instrument BankFootingTests uses.
            RenderTestWorld world = Terrace();

            // **Measured where the figure actually is**, which since `PawnPose.StepPace` is not
            // where the clock says: the time is spent where the climbing is, so at a given fraction
            // of the step the figure is further back along the path than it used to be. An
            // instrument that samples the ground at the clock's position rather than the figure's
            // reports the difference between the two as sinking — it did, by 11 mm.
            for (int i = 0; i <= 1000; i++)
            {
                Vector3 up = DrawnAt(world, Foot, Top, i);
                Vector3 down = DrawnAt(world, Top, Foot, i);

                Assert.That(up.y, Is.GreaterThanOrEqualTo(FloorUnder(world, Foot, Top, up) - 0.01f),
                    $"the climb is inside the ground at {i} per mille");
                Assert.That(down.y, Is.GreaterThanOrEqualTo(FloorUnder(world, Top, Foot, down) - 0.01f),
                    $"the drop is inside the ground at {i} per mille");
            }
        }

        /// <summary>
        /// The drawn surface under the figure at this point of the step: the same expression
        /// <c>PawnPose</c> clamps against, which is the cell it is over plus whatever bank is in it.
        /// </summary>
        static float FloorUnder(RenderTestWorld world, CellRef from, CellRef to, Vector3 at)
        {
            // Which cell the figure is over, read off where it is drawn rather than off the clock.
            Vector3 a = CellMetrics.FloorCentre(from), b = CellMetrics.FloorCentre(to);
            float along = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z)
                ? Mathf.InverseLerp(a.x, b.x, at.x)
                : Mathf.InverseLerp(a.z, b.z, at.z);
            CellRef over = along < 0.5f ? from : to;
            return CellMetrics.FloorCentre(over).y + BankLayout.RiseAt(world.Model, over, at.x, at.z);
        }

        [Test]
        public void ADropHangsAtTheEdgeAndThenFalls()
        {
            // Not a lowering. Half way through the step a falling body has covered a quarter of its
            // drop, not half of it — that is what the square means, and it is the whole difference
            // between letting go and being winched down.
            RenderTestWorld world = Terrace();
            float from = DrawnStanding(world, Top).y;
            float to = DrawnStanding(world, Foot).y;
            float half = DrawnAt(world, Top, Foot, 500).y;

            float covered = (from - half) / (from - to);
            Assert.That(covered, Is.LessThan(0.4f),
                $"half way through the step the figure has already fallen {covered:P0} of the way");
        }

        [Test]
        public void AClimbNeverMovesFasterThanAFallingBody()
        {
            // The continuity instrument of WalkOnReliefTests, pointed at the new curve: a frame
            // that moves the figure much further than its neighbours reads as a snap, and
            // ObserveSpeed differences position frame to frame, so it also throws the gait.
            RenderTestWorld world = Terrace();

            // MoveCost.JumpUp ticks at 60 a second, one frame a tick.
            int frames = MoveCost.JumpUp;
            float previous = DrawnAtTick(world, Foot, Top, 0, frames).y;
            float largest = 0f;

            for (int frame = 1; frame <= frames; frame++)
            {
                float y = DrawnAtTick(world, Foot, Top, frame, frames).y;
                largest = Mathf.Max(largest, Mathf.Abs(y - previous));
                previous = y;
            }

            // The same bound BankFootingTests calls Smooth, and it is not arbitrary: an honest
            // frame of walking moves a colonist 25 mm, so twice that is the most a stride may move
            // her before it stops being a stride and becomes a snap.
            Assert.That(largest, Is.LessThan(0.05f),
                $"a single frame of the climb moves the figure {largest * 1000f:F0} mm, which is a snap");
        }

        [Test]
        public void TheFlatsAreWalkedAndTheRampIsClimbed()
        {
            // **The owner's third report, turned into an assertion** (2026-09-18: "the slowness
            // needs to start happening much earlier when entering the beginning of the tile … you
            // slow down and then you seem to still go slow on the flat so it's out of sync").
            //
            // A terrace climb is one ramp charged as two steps, split down the middle of the foot
            // cell. Both now cost a hop — the cell is a slope (`NaturalContent.CostClassSlope`) —
            // and `PawnPose.StepPace` spends each step's time where its climbing is. What that
            // has to produce is one speed on the ramp from bottom to top, and a walking pace on
            // the flat ground either side of it.
            RenderTestWorld world = Terrace();

            var approach = new CellRef(4, 3, 2);         // flat ground, one cell out
            var foot = Foot;                             // the bank cell
            var top = Top;                               // on the step

            // Walking on to the bank: flat for the first half, ramp for the second.
            Measure(world, approach, foot, MoveCost.JumpUp, out float flatIn, out float rampIn);

            // Hopping off it: ramp first, then the flat top.
            Measure(world, foot, top, MoveCost.JumpUp, out float rampOut, out float flatOut);

            const float walking = CellMetrics.SizeXZ / (MoveCost.Orthogonal / (float)TicksPerSecond);

            Assert.That(flatIn, Is.EqualTo(walking).Within(walking * 0.15f),
                $"the flat half of the walk on to the bank is drawn at {flatIn:F2} m/s, not a walk");
            Assert.That(flatOut, Is.EqualTo(walking).Within(walking * 0.15f),
                $"the flat top is drawn at {flatOut:F2} m/s, not a walk — this is the half the owner " +
                "saw still crawling");

            Assert.That(rampIn, Is.LessThan(walking * 0.6f),
                $"the bottom of the ramp is drawn at {rampIn:F2} m/s, which is not climbing");
            Assert.That(rampOut, Is.LessThan(walking * 0.6f),
                $"the top of the ramp is drawn at {rampOut:F2} m/s, which is not climbing");

            // And the whole point: one speed up the ramp, not two.
            Assert.That(rampIn, Is.EqualTo(rampOut).Within(Mathf.Max(rampIn, rampOut) * 0.2f),
                $"the ramp is climbed at {rampIn:F2} m/s in its bottom half and {rampOut:F2} m/s in " +
                "its top half, so the climb changes speed half way up");
        }

        /// <summary>
        /// The drawn speed over each half of a step, in metres a second along the path actually
        /// drawn — which is the only speed anybody can see.
        /// </summary>
        static void Measure(RenderTestWorld world, CellRef from, CellRef to, int ticks,
            out float firstHalf, out float secondHalf)
        {
            Vector3 boundary = (CellMetrics.FloorCentre(from) + CellMetrics.FloorCentre(to)) * 0.5f;
            bool alongX = Mathf.Abs(boundary.x - CellMetrics.FloorCentre(from).x) > 1e-3f;

            float firstDistance = 0f, secondDistance = 0f;
            int firstTicks = 0, secondTicks = 0;
            Vector3 previous = DrawnAtTick(world, from, to, 0, ticks);

            for (int tick = 1; tick <= ticks; tick++)
            {
                Vector3 at = DrawnAtTick(world, from, to, tick, ticks);
                float step = Vector3.Distance(at, previous);

                // Which half of the step the figure is in, read off where it is drawn.
                float here = alongX ? at.x : at.z;
                float edge = alongX ? boundary.x : boundary.z;
                float start = alongX ? CellMetrics.FloorCentre(from).x : CellMetrics.FloorCentre(from).z;
                bool first = Mathf.Abs(here - start) < Mathf.Abs(edge - start);

                if (first) { firstDistance += step; firstTicks++; }
                else { secondDistance += step; secondTicks++; }

                previous = at;
            }

            firstHalf = firstTicks > 0 ? firstDistance / (firstTicks / (float)TicksPerSecond) : 0f;
            secondHalf = secondTicks > 0 ? secondDistance / (secondTicks / (float)TicksPerSecond) : 0f;
        }

        [Test]
        public void AClimbIsNeverDrawnFasterThanAWalk()
        {
            // **The owner's report, turned into an assertion** (2026-09-18: "the colonists looked
            // too fast going up definitely"). A hop is drawn along the slope between two cell
            // centres — 2.5 m across and 3.0 m up is 3.91 m — and at 135 that was covered in 2.25 s,
            // or 1.74 m/s, against the 1.50 m/s of walking on the flat. Climbing a terrace was
            // quicker than strolling beside it. No amount of pose work hides that, so the price
            // carries it and this is the floor under the price.
            float slope = Mathf.Sqrt(CellMetrics.SizeXZ * CellMetrics.SizeXZ +
                                     CellMetrics.SizeY * CellMetrics.SizeY);
            float walking = CellMetrics.SizeXZ / (MoveCost.Orthogonal / (float)TicksPerSecond);
            float climbing = slope / (MoveCost.JumpUp / (float)TicksPerSecond);

            Assert.That(climbing, Is.LessThan(walking),
                $"a colonist is drawn climbing at {climbing:F2} m/s and walking at {walking:F2} m/s, " +
                "so going up a terrace looks faster than going along it");

            // And the ceiling, which is what keeps a terraced board crossable: past a stair, a
            // colonist walks to one rather than hopping a single block.
            Assert.That(MoveCost.JumpUp, Is.LessThan(MoveCost.StairUp),
                "a hop costs more than a stair, so nothing will hop a one-block step again");
        }

        [Test]
        public void ASheerFaceIsClimbedSmoothlyRatherThanInStrides()
        {
            // There is not always a ramp to tread. A bank is refused against rock, inside a working
            // and under a roof, and there the ground under the walker is flat for half the step and
            // then jumps a whole layer at the midpoint — strides taken off it would draw a colonist
            // standing still and then teleporting three metres. The chord underneath the strides is
            // what catches that, and this is the case that proves it.
            RenderTestWorld world = SheerFace();
            var foot = new CellRef(3, 3, 2);
            var top2 = new CellRef(2, 3, 3);

            float previous = DrawnAtTick(world, foot, top2, 0, MoveCost.JumpUp).y;
            float largest = 0f;
            for (int frame = 1; frame <= MoveCost.JumpUp; frame++)
            {
                float y = DrawnAtTick(world, foot, top2, frame, MoveCost.JumpUp).y;
                largest = Mathf.Max(largest, Mathf.Abs(y - previous));
                previous = y;
            }

            Assert.That(largest, Is.LessThan(0.05f),
                $"climbing a sheer face moves the figure {largest * 100f:F0} cm in one frame");
            Assert.That(DrawnAt(world, foot, top2, 1000).y,
                Is.EqualTo(DrawnStanding(world, top2).y).Within(0.01f),
                "a sheer climb does not finish on the ground above it");
        }

        [Test]
        public void ADropDownASheerFaceIsAsFastAsTheGeometryAllows()
        {
            // The mirror of the climb above, and the one place a frame moves further than a stride
            // may. The clamp holds the figure on the upper floor until the midpoint — which is
            // right, since it is standing on the ledge until it crosses the edge — so the whole
            // three metres has to happen in the second half of a step that costs 50 ticks. That is
            // 0.42 s for a layer, and no curve makes it gentler; only a cheaper `MoveCost.Drop` or
            // an earlier crossing would.
            //
            // What *was* worth fixing is that it used to be a discontinuity as well: the fall was
            // timed across the whole step, so it was 66 cm below the ledge by the time the clamp
            // let go and it snapped there in one frame — 657 mm, measured. It is now timed into the
            // second half, so the release is continuous and what is left is honest falling.
            //
            // Measured and pinned rather than made gentler, so that if `MoveCost.Drop` or the
            // crossing point changes, the number here moves and somebody reads this paragraph.
            RenderTestWorld world = SheerFace();
            var foot = new CellRef(3, 3, 2);
            var top2 = new CellRef(2, 3, 3);

            float previous = DrawnAtTick(world, top2, foot, 0, MoveCost.Drop).y;
            float largest = 0f;
            for (int tick = 1; tick <= MoveCost.Drop; tick++)
            {
                float y = DrawnAtTick(world, top2, foot, tick, MoveCost.Drop).y;
                largest = Mathf.Max(largest, Mathf.Abs(y - previous));
                previous = y;
            }

            Assert.That(largest, Is.LessThan(0.35f),
                $"a frame of a sheer drop moves the figure {largest * 100f:F0} cm, which is past " +
                "what the geometry alone accounts for");
            TestContext.WriteLine($"sheer drop: worst frame {largest * 1000f:F0} mm");
        }

        [Test]
        public void AStepOntoAFloorIsNotAHop()
        {
            // Stairs are not in the game yet (U44), and the day they land a colonist on one must
            // not be drawn vaulting up the stairwell. The simulation's own separation is that you
            // hop onto ground and take a stair to a storey, so the cell under the upper end has to
            // be solid terrain — here it is a slab over open air, and the arc must keep out of it.
            const int n = 6;
            var world = new RenderTestWorld(n, n, 8);
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                world.Solid(x, z, 0, NaturalContent.TerrainSubsoil);
                world.Solid(x, z, 1, NaturalContent.TerrainGrass);
            }
            world.Slab(2, 3, 3);
            world.Publish();

            var lower = new CellRef(3, 3, 2);
            var upper = new CellRef(2, 3, 3);
            Assert.That(NavGraph.IsHop(lower, upper), Is.True, "the fixture is not one layer and one cell");
            Assert.That(PawnPose.IsDrawnAsAHop(world.Model, Walking(lower, upper, 50)), Is.False,
                "a step onto a built floor is being drawn as a hop onto a block");
        }
    }
}
