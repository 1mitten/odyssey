#nullable enable

using Odyssey.Sim.Pathing;
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// How a figure is drawn across a step that changes layer: treading up it, or dropping off it.
    ///
    /// <para><b>Going up, there is nothing here at all, and that is the design.</b> A bank is a
    /// ramp — <c>BankMesh.HeightAt</c> is a plane from the floor of the lower cell to the rim of the
    /// upper one — so a climbing figure is drawn <b>on that surface</b>, sampled where it stands,
    /// and needs no curve of its own. Going down there is nothing underfoot past the edge, so the
    /// height has to come from somewhere: a beat at the lip and then a fall (<see cref="Fall"/>).
    /// </para>
    ///
    /// <para><b>Three reports got it to that.</b> The speed first (<i>"the colonists looked too fast
    /// going up definitely"</i>), answered in <c>MoveCost.JumpUp</c> and in
    /// <c>NaturalContent.CostClassSlope</c>. Then the shape (<i>"it looks like they jump a bit and
    /// not flat with the terrain"</i>), which took away a parabola that vaulted the lip. Then
    /// <b>(2026-09-19)</b> <i>"keep it simple … a consistently slow speed from top to bottom and
    /// motions exactly just above the terrace surface, as it jolts and jitters the colonists at
    /// certain points; smoother is preferred and predictable"</i> — which took away what had
    /// replaced the parabola: strides, a hold-and-push rhythm that was four deliberate jolts a
    /// climb. Both replacements were inventions; the ramp was always there to be walked on.</para>
    ///
    /// <para><b>What is left is the pacing and the fall.</b> <see cref="ClimbWeight"/> tells
    /// <c>PawnPose.StepPace</c> how to spend a step's time so that the ramp is climbed at one steady
    /// speed and the flat ground either side of it is walked; nothing decides <i>where</i> a
    /// climbing figure is any more except the ground itself.</para>
    ///
    /// <para><b>Nothing here is simulated.</b> A hop's price, its legality and its two ends are the
    /// simulation's (<c>NavGraph.HopCost</c>, <c>NavGraph.IsHop</c>); this only decides where the
    /// figure is drawn between them, like <see cref="GroundRelief"/> and <see cref="BankLayout"/>.
    /// It cannot move a pawn, change a path or touch the state hash.</para>
    /// </summary>
    public static class HopArc
    {
        /// <summary>
        /// The part of a drop spent leaving the edge before the fall begins, as a fraction.
        ///
        /// <para><b>Derived, not chosen.</b> <c>MoveCost.Drop</c> is 50, which is 0.83 s at 60
        /// ticks a second, and a 3.0 m free fall under gravity takes 0.78 s. The difference is the
        /// step off the edge: hold for 0.05 s, then fall at the speed of gravity and land exactly
        /// when the simulation says the step ends. <c>HopArcTests.AFallIsAtTheSpeedOfGravity</c>
        /// pins that arithmetic, so retuning <c>MoveCost.Drop</c> fails a test here rather than
        /// quietly making colonists fall at the wrong speed.</para>
        /// </summary>
        public const float StepOff = 0.06f;

        /// <summary>
        /// What a metre of climbing is worth in metres of walking, when a step's time is spread
        /// over the path it is drawn along (<c>PawnPose.StepPace</c>).
        ///
        /// <para><b>Derived from the prices, so it cannot drift from them.</b> Climbing a terrace is
        /// two steps — the walk on to the foot cell and the hop out of it — and since a terrace foot
        /// is priced as a slope (<c>NaturalContent.CostClassSlope</c>) both cost
        /// <c>MoveCost.JumpUp</c>. Between them they cover two cells of ground and one layer of
        /// rise. Take the ground at walking pace, which is what <c>MoveCost.Orthogonal</c> buys, and
        /// whatever is left over is what the rise costs: 480 ticks less 200 for the 5 m of ground
        /// leaves 280 for 3 m of climb, or 93 ticks a metre against walking's 40. Hence about 2.3.
        /// </para>
        ///
        /// <para>The consequence is the one the owner asked for: the flat halves of both steps are
        /// drawn at a walking pace and the ramp between them at one steady 0.6 m/s, instead of the
        /// climb changing speed half way up and the flat top crawling.</para>
        /// </summary>
        public static readonly float ClimbWeight = ClimbWeightFromPrices();

        static float ClimbWeightFromPrices()
        {
            // Two steps make a terrace climb; both are priced at a hop, and between them they carry
            // two cells of ground and one layer of rise.
            float pair = 2f * MoveCost.JumpUp;
            float ground = 2f * MoveCost.Orthogonal;
            float perMetreWalking = MoveCost.Orthogonal / CellMetrics.SizeXZ;
            float perMetreClimbing = (pair - ground) / CellMetrics.SizeY;

            // A climb that costs nothing extra is not a climb; fall back to counting it as ground
            // rather than dividing the step into nothing.
            return perMetreClimbing > 0f ? perMetreClimbing / perMetreWalking : 1f;
        }

        /// <summary>
        /// How much of the descent is done, 0 to 1, at this point through a hop <b>down</b>.
        ///
        /// <para>A square, because that is what falling is: distance goes as the square of time. The
        /// old linear version dropped a colonist three metres at a constant rate, which reads as
        /// being lowered rather than as letting go — the owner's word for the version before that
        /// was "floating".</para>
        ///
        /// <para>The curve is allowed to pass <i>below</i> the bank it is falling past, and the
        /// caller clamps it: a body that runs off a slope stays on the slope until the slope falls
        /// away faster than it does, and then it is in the air. That is one rule for both halves of
        /// the drop and it replaced a hand-faded lift that had to be tuned.</para>
        /// </summary>
        public static float Fall(float t)
        {
            float u = Flight(t, StepOff);
            return u * u;
        }

        /// <summary>
        /// How far through the airborne part of the step this is: nothing until <paramref name="hold"/>
        /// of it has passed, then 0 to 1 over what is left.
        /// </summary>
        static float Flight(float t, float hold)
        {
            float u = (Mathf.Clamp01(t) - hold) / (1f - hold);
            return Mathf.Clamp01(u);
        }
    }
}
