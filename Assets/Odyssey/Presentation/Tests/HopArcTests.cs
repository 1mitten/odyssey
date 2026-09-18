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
        }

        [TearDown]
        public void Restore()
        {
            BankLayout.Reset();
            GroundRelief.Reset();
        }

        // ------------------------------------------------------------------ the curves

        [Test]
        public void BothCurvesStartAndFinishExactlyOnTheGround()
        {
            // The one property that cannot be traded for a nicer shape. The simulation says the
            // pawn is in the leaving cell at 0 and the arriving cell at 1; a curve that does not
            // agree puts a jolt at one end of every step, which reads as a teleport and gets
            // blamed on the animation.
            Assert.That(HopArc.Climb(0f, 1.5f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(HopArc.Climb(1f, 1.5f), Is.EqualTo(1.5f).Within(Tolerance));
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
        public void AClimbGathersBeforeItRises()
        {
            // A jump starts from a crouch. Without the gather the figure rises in the first frame,
            // which is an escalator rather than effort.
            Assert.That(HopArc.Climb(HopArc.Gather * 0.5f, 1.5f), Is.EqualTo(0f).Within(Tolerance),
                "the climb had already started during the gather");
            Assert.That(HopArc.Climb(HopArc.Gather + 0.05f, 1.5f), Is.GreaterThan(0f),
                "the climb never leaves the gather");
        }

        [Test]
        public void AClimbGoesOverTheLipAndComesDownOnIt()
        {
            // The property that makes it a hop rather than a ramp, on the curve itself: the top of
            // the flight is above the ground being landed on, so the figure crosses the lip and
            // settles, rather than arriving from underneath at the final frame.
            foreach (float rise in new[] { 0f, 0.75f, 1.5f, 3.0f })
            {
                float highest = float.MinValue;
                float apexAt = 0f;
                for (int i = 0; i <= 1000; i++)
                {
                    float t = i / 1000f;
                    float y = HopArc.Climb(t, rise);
                    if (y <= highest) continue;
                    highest = y;
                    apexAt = t;
                }

                Assert.That(highest, Is.EqualTo(rise + HopArc.Clearance).Within(0.01f),
                    $"a {rise} m hop peaks at {highest} rather than {HopArc.Clearance} m over the lip");
                Assert.That(apexAt, Is.LessThan(0.98f),
                    $"a {rise} m hop is still rising when it lands, which is not a landing");
                Assert.That(HopArc.Climb(1f, rise), Is.EqualTo(rise).Within(Tolerance),
                    $"a {rise} m hop does not finish on the ground it climbed to");
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

        [Test]
        public void TheClearanceIsAHopAndNotAThrow()
        {
            Assert.That(HopArc.Clearance, Is.InRange(0.15f, 0.5f),
                "under fifteen centimetres nobody sees the figure leave the ground, and over half a " +
                "metre it is being thrown rather than hopping");
        }

        // ------------------------------------------------------------------ on the board

        /// <summary>
        /// The terrace of <c>BankFootingTests</c>: high ground at x &lt; 3, low ground beyond it,
        /// so (3, z, 2) is the cell at the foot of the step and (2, z, 3) is the ground above it.
        /// </summary>
        static RenderTestWorld Terrace() =>
            RenderTestWorld.Terrace(rise: 1, stepTerrain: NaturalContent.TerrainGrass);

        static readonly CellRef Foot = new CellRef(3, 3, 2);
        static readonly CellRef Top = new CellRef(2, 3, 3);

        static PawnView Standing(CellRef cell) => Walking(cell, cell, 0);

        static PawnView Walking(CellRef from, CellRef to, int percent) =>
            new PawnView(new PawnId(1), from, 100, 100, 50, -1, to, percent);

        static Vector3 DrawnAt(RenderTestWorld world, CellRef from, CellRef to, int percent) =>
            PawnPose.Of(Walking(from, to, percent), 0f, 0, out _, world.Model);

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
            Assert.That(DrawnAt(world, Foot, Top, 100).y,
                Is.EqualTo(DrawnStanding(world, Top).y).Within(0.01f),
                "a climb does not end on the ground it climbed to");
            Assert.That(DrawnAt(world, Top, Foot, 0).y,
                Is.EqualTo(DrawnStanding(world, Top).y).Within(0.01f),
                "a drop does not start where the colonist was standing");
            Assert.That(DrawnAt(world, Top, Foot, 100).y,
                Is.EqualTo(DrawnStanding(world, Foot).y).Within(0.01f),
                "a drop does not land on the ground below it");
        }

        [Test]
        public void AClimbGetsAboveWhatItIsClimbingOnTo()
        {
            // The difference between a hop and a ramp, stated on the board rather than on the
            // curve: at some point in the step the figure is higher than the ground it is landing
            // on. A slide up a bank never is, which is what this used to draw.
            RenderTestWorld world = Terrace();
            float landing = DrawnStanding(world, Top).y;

            float highest = float.MinValue;
            for (int percent = 0; percent <= 100; percent++)
                highest = Mathf.Max(highest, DrawnAt(world, Foot, Top, percent).y);

            Assert.That(highest, Is.EqualTo(landing + HopArc.Clearance).Within(0.02f),
                "the figure does not pass over the block it is hopping onto by its own clearance");
        }

        [Test]
        public void NothingIsEverDrawnInsideTheHillside()
        {
            // The clamp. A hop's chord passes a metre and a half inside the block being climbed,
            // and the fall passes below the bank it drops past — both are pushed back out onto the
            // drawn surface. Four hundred samples each way, the instrument BankFootingTests uses.
            RenderTestWorld world = Terrace();

            for (int i = 0; i <= 400; i++)
            {
                int percent = Mathf.RoundToInt(i * 0.25f);
                float up = DrawnAt(world, Foot, Top, percent).y;
                float down = DrawnAt(world, Top, Foot, percent).y;

                Assert.That(up, Is.GreaterThanOrEqualTo(FloorUnder(world, Foot, Top, percent) - 0.01f),
                    $"the climb is inside the ground at {percent}%");
                Assert.That(down, Is.GreaterThanOrEqualTo(FloorUnder(world, Top, Foot, percent) - 0.01f),
                    $"the drop is inside the ground at {percent}%");
            }
        }

        /// <summary>
        /// The drawn surface under the figure at this point of the step: the same expression
        /// <c>PawnPose</c> clamps against, which is the cell it is over plus whatever bank is in it.
        /// </summary>
        static float FloorUnder(RenderTestWorld world, CellRef from, CellRef to, int percent)
        {
            float t = percent * 0.01f;
            CellRef over = t < 0.5f ? from : to;
            Vector3 a = CellMetrics.FloorCentre(from), b = CellMetrics.FloorCentre(to);
            float x = Mathf.Lerp(a.x, b.x, t), z = Mathf.Lerp(a.z, b.z, t);
            return CellMetrics.FloorCentre(over).y + BankLayout.RiseAt(world.Model, over, x, z);
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
            float half = DrawnAt(world, Top, Foot, 50).y;

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
            float previous = DrawnAt(world, Foot, Top, 0).y;
            float largest = 0f;

            for (int frame = 1; frame <= frames; frame++)
            {
                float y = DrawnAt(world, Foot, Top, Mathf.RoundToInt(frame * 100f / frames)).y;
                largest = Mathf.Max(largest, Mathf.Abs(y - previous));
                previous = y;
            }

            Assert.That(largest, Is.LessThan(0.1f),
                $"a single frame of the climb moves the figure {largest * 1000f:F0} mm, which is a snap");
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
