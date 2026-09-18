#nullable enable

using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The shape of a hop: how a figure's height moves across a step that changes layer.
    ///
    /// <para><b>Why a hop needed a shape at all.</b> A hop was drawn as a straight line between two
    /// cell centres, raised onto whatever ground was under it — so going up, the figure slid up the
    /// bank at a constant rate, and going down it slid back down at a constant rate. Neither is a
    /// hop. The owner saw the first of the two in play on 2026-09-18 (<i>"the colonists looked too
    /// fast going up definitely"</i>), and the number behind it is in <c>MoveCost.JumpUp</c>: the
    /// drawn path up a step is 3.91 m and it was being covered in 2.25 s, faster than walking.
    /// Re-pricing the step fixes the speed; this fixes the motion, and neither would have been
    /// enough alone — a slow slide is a colonist stuck on a hill, which is what the last retune of
    /// that constant produced.</para>
    ///
    /// <para><b>Nothing here is simulated.</b> A hop's price, its legality and its two ends are the
    /// simulation's (`NavGraph.HopCost`, `NavGraph.IsHop`); this only decides where the figure is
    /// drawn between them, like <see cref="GroundRelief"/> and <see cref="BankLayout"/>. It cannot
    /// move a pawn, change a path or touch the state hash.</para>
    ///
    /// <para><b>Both curves end exactly where the standing pose is.</b> <see cref="Leap"/> and
    /// <see cref="Fall"/> are 0 at the start and 1 at the end, so the drawn figure meets the
    /// arriving cell's surface at the moment the simulation says it arrives, and meets the leaving
    /// cell's surface at the moment it left. A curve that did not would show as a jolt at one end
    /// of every step — the fault <c>WalkOnReliefTests</c> was written for.</para>
    /// </summary>
    public static class HopArc
    {
        /// <summary>
        /// The part of a hop up spent gathering before anything rises, as a fraction of the step.
        ///
        /// <para>A jump starts from a crouch, and without this the figure begins rising in the
        /// first frame — which reads as an escalator rather than as effort. Short on purpose: the
        /// figure is still walking forward during the gather and its feet are on the bank, so every
        /// frame of it is a frame of the gait sliding. At <c>MoveCost.JumpUp</c> = 240 this is
        /// about half a second.</para>
        /// </summary>
        public const float Gather = 0.12f;

        /// <summary>
        /// How far above the lip the figure passes, in metres, at the top of a hop up.
        ///
        /// <para>This is what makes it a hop rather than a ramp: without it the curve arrives at
        /// the upper surface and stops, and a body that never goes above what it is climbing onto
        /// has not jumped onto anything. A third of a metre is a clear vault at the play camera's
        /// 48° and well under the half-metre at which a colonist looks thrown.</para>
        /// </summary>
        public const float Clearance = 0.35f;

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
        /// How far above the ground it left the figure is, in metres, at this point through a hop
        /// <b>up</b> of <paramref name="rise"/> metres.
        ///
        /// <para><b>A thrown body, solved rather than eased.</b> The curve is the parabola that
        /// leaves the lower ground at the end of the gather, passes exactly
        /// <see cref="Clearance"/> above the upper ground at the top of its flight, and comes back
        /// down onto that upper ground exactly as the step ends. So the figure goes <i>over</i> the
        /// lip and settles onto it, which is the difference between hopping onto a block and being
        /// carried up a ramp.</para>
        ///
        /// <para><b>It takes the rise rather than assuming a layer</b>, and that is not generality
        /// for its own sake: a colonist hopping up a terrace starts half way up the bank in the
        /// cell at its foot, so the real climb is about 1.5 m and not the 3.0 m of a layer. An arc
        /// built for the layer would have cleared the lip by twice what it should and read as a
        /// leap. The first cut of this class made exactly that mistake in the other direction — a
        /// fixed arch added to a fixed climb — and cleared the lip by 15 cm instead of the 35 it
        /// claimed. <c>HopArcTests.AClimbGetsAboveWhatItIsClimbingOnTo</c> is what caught it.</para>
        ///
        /// <para>Solving <c>y = au - bu²</c> for <c>y(1) = rise</c> and an apex of
        /// <c>rise + Clearance</c> gives <c>a = 2(rise + C) + 2√(C(rise + C))</c> and
        /// <c>b = a - rise</c>; the other root of that quadratic puts the apex past the landing,
        /// which is a figure still rising as it arrives.</para>
        /// </summary>
        public static float Climb(float t, float rise)
        {
            float u = Flight(t, Gather);

            // A hop that does not rise is not a fault worth an exception — the ground clamp in
            // PawnPose covers it — but a negative one would take the root of a negative number and
            // draw the figure at NaN, which is a colonist that disappears.
            float h = Mathf.Max(0f, rise);
            float a = 2f * (h + Clearance) + 2f * Mathf.Sqrt(Clearance * (h + Clearance));
            float b = a - h;

            return a * u - b * u * u;
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
