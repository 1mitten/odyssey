#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// The two short moments at either end of a carry: a load coming up off the ground into the
    /// arms, and a load going back down out of them. Design 24 §6a and §6c.
    ///
    /// <para><b>Why there is anything here at all.</b> The simulation transfers a thing in one
    /// instant, because a thing is either in a cell or in a pair of hands and there is nothing
    /// sensible in between. Drawn literally, that instant is a teleport: the pile is at the middle
    /// of the cell, the palms are a third of a metre in front of the colonist and rather higher,
    /// and the load crosses that gap in no time at all. The owner's report is exactly this
    /// (2026-09-19): "instead of snapping to position it should fall it drop into position - and
    /// also be picked/raised out of pick up."</para>
    ///
    /// <para><b>It is drawing and nothing else.</b> The thing is in the hands, or in the cell, on
    /// the tick the simulation says so; this only governs where it is <em>drawn</em> for the
    /// fraction of a second either side. Nothing here may change when a load arrives, what it
    /// weighs, or what anything may pick up — the same bargain <see cref="WaterLine"/> makes for
    /// the float, and for the same reason.</para>
    ///
    /// <para><b>The two ends are not the same curve, and that is the whole of what makes one read
    /// as a lift and the other as a drop.</b> <see cref="Gesture"/> makes the same argument for
    /// the body: going down is a fall the knees catch, coming up is work against a weight. A load
    /// obeys it more literally than a body does — it really is falling — so the drop accelerates
    /// and the raise decelerates. Reverse them and a colonist appears to be placing something
    /// delicately and then snatching it off the floor.</para>
    /// </summary>
    public static class CarryHandover
    {
        /// <summary>
        /// How long a load takes to come up into the hands, in seconds.
        ///
        /// <para>Shorter than the lift's own 0.8 s, and it has to be: the grasp lands at the
        /// middle of the crouch and the colonist is upright again 0.4 s later, so anything longer
        /// would have the load still travelling after she had set off walking — which is the very
        /// fault <c>LiftTicks</c> was introduced to fix, in a new costume.</para>
        /// </summary>
        public static float RaiseSeconds { get; set; } = 0.30f;

        /// <summary>
        /// How long a load takes to settle from the hands to the floor, in seconds.
        ///
        /// <para>Longer than the raise. A load is lowered under control and then released, where
        /// it is picked up in one movement — and <c>Gesture.Stow</c> is itself the slower of the
        /// two crouches for the same reason.</para>
        /// </summary>
        public static float FallSeconds { get; set; } = 0.36f;

        /// <summary>
        /// Where the load is, between the place it started and the place it is going, 0 at the
        /// start and 1 arrived.
        ///
        /// <para>Eased <em>out</em>: quick off the floor, slowing as it reaches the cradle. That
        /// is a thing being lifted by somebody straightening up, whose hands are fastest in the
        /// middle of the rise and slowest at the top.</para>
        /// </summary>
        public static float Raised(float elapsed)
        {
            float t = Phase(elapsed, RaiseSeconds);
            return 1f - (1f - t) * (1f - t);
        }

        /// <summary>
        /// The same, going down. Eased <em>in</em>: slow out of the hands and quickest at the
        /// floor, which is what falling is.
        /// </summary>
        public static float Fallen(float elapsed)
        {
            float t = Phase(elapsed, FallSeconds);
            return t * t;
        }

        /// <summary>Whether a raise that began this long ago is over.</summary>
        public static bool RaiseFinished(float elapsed) => elapsed >= RaiseSeconds;

        /// <summary>Whether a fall that began this long ago is over.</summary>
        public static bool FallFinished(float elapsed) => elapsed >= FallSeconds;

        /// <summary>
        /// Whether a fall crossed its own end between two elapsed times — the frame the load
        /// touches down, and the only frame it does.
        ///
        /// <para>This exists so that the sound of a load landing has a rule rather than a
        /// condition. <see cref="FallFinished"/> is true forever after the landing, so anything
        /// that polls it fires every frame for the rest of the carry; what is wanted is the edge,
        /// and an edge needs both sides of the step. Design 24 §12.</para>
        /// </summary>
        public static bool FallLanded(float before, float after) =>
            before < FallSeconds && after >= FallSeconds;

        static float Phase(float elapsed, float seconds) =>
            seconds <= 1e-4f ? 1f : Mathf.Clamp01(elapsed / seconds);
    }
}
