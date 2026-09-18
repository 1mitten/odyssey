#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// A colonist looking about itself: the head and neck turning off the line of travel.
    ///
    /// <para><b>Computed, not animated,</b> for the reason every pose in this folder is. The
    /// Base Locomotion pack does ship additive look clips —
    /// <c>A_HeadLook_Additive_Neut</c> and <c>A_BodyLook_Additive_Neut</c> — and they were
    /// considered and not used (2026-09-18). Using them means an additive layer mixer over the
    /// gait, a pose-grid convention nobody here has written down, and a second animation path to
    /// keep in step with this one; the whole of what they buy is two angles. Two angles are
    /// arithmetic, they are right on all sixty-one rigs without authoring, and they can be tested
    /// with no Unity at all. <c>13-gestures.md</c> §3 draws this line and it falls on the same
    /// side as the axe, the swim and the climb.</para>
    ///
    /// <para><b>It is a stroke, not a gesture</b> (<see cref="Gesture"/>'s own distinction): there
    /// is no event and no end, only a cycle the figure is somewhere in at every instant. So the
    /// phase comes off a clock, exactly as <see cref="SwimPose"/>'s does.</para>
    ///
    /// <para><b>Everything is an angle</b>, never a distance, so the pose is right whatever the
    /// figure's proportions and whatever <c>WalkVariance.Build</c> did to its scale.</para>
    /// </summary>
    public static class LookAbout
    {
        /// <summary>
        /// How far off centre a colonist will turn its head, in degrees.
        ///
        /// <para>Comfortable human head-on-neck rotation is about 70°, and a colonist that reaches
        /// it constantly reads as nervous rather than alive. This is the outer bound of a wander
        /// that spends most of its time near the middle.</para>
        /// </summary>
        public static float YawDegrees { get; set; } = 38f;

        /// <summary>And up or down. Much smaller: the eye line does most of this in a real person,
        /// and a colonist studying the sky reads as broken.</summary>
        public static float PitchDegrees { get; set; } = 9f;

        /// <summary>
        /// How much of the turn the neck takes, the head taking the rest.
        ///
        /// <para>Splitting it is what stops the motion reading as a skull swivelling on a fixed
        /// body. A third in the neck is enough to carry the shoulders' suggestion without binding
        /// the spine, which is already spoken for by the work poses.</para>
        /// </summary>
        public static float NeckShare { get; set; } = 0.34f;

        /// <summary>Full wanders a second at the nominal rate. Slow on purpose — a head that
        /// sweeps at walking cadence reads as scanning for threats.</summary>
        public static float CyclesPerSecond { get; set; } = 0.11f;

        /// <summary>How long the pose eases in and out, in seconds.</summary>
        public static float EaseSeconds { get; set; } = 0.5f;

        public static void Reset()
        {
            YawDegrees = 38f;
            PitchDegrees = 9f;
            NeckShare = 0.34f;
            CyclesPerSecond = 0.11f;
            EaseSeconds = 0.5f;
        }

        /// <summary>Where in the wander a clock of this many seconds is, 0 to 1.</summary>
        public static float Phase(float seconds, float rate, float offset) =>
            Mathf.Repeat(seconds * CyclesPerSecond * rate + offset, 1f);

        /// <summary>
        /// The head's turn at this phase: yaw then pitch, in degrees, off the direction the figure
        /// already faces.
        ///
        /// <para>Two sine terms per axis, offset from each other and running at different rates
        /// from the yaw's. One term in each would trace a line or a circle and read as clockwork;
        /// this wanders, and <c>ItWandersRatherThanSweeping</c> pins that by counting turning
        /// points. The amplitudes sum to one, so the caller's clamps are a bound rather than a
        /// hope — the peak actually reached is about 88% of it.</para>
        ///
        /// <para><b>Every rate is a whole number of cycles, and that is not cosmetic.</b> The first
        /// version used 2.3, 1.7 and 0.7, which do not complete over the phase — so the head
        /// snapped <b>8.9°</b> as the cycle wrapped, once every nine seconds, on every colonist.
        /// Caught by <c>TheCycleJoinsUpAtTheWrap</c> rather than by anybody looking, which is the
        /// argument for that test existing: a snap that rare reads as a glitch in the renderer and
        /// would never have been traced back to a sine.</para>
        /// </summary>
        public static Vector2 At(float phase)
        {
            float t = phase * Mathf.PI * 2f;

            float yaw = 0.68f * Mathf.Sin(t) + 0.32f * Mathf.Sin(t * 3f + 1.1f);
            float pitch = 0.60f * Mathf.Sin(t * 2f + 0.6f) + 0.40f * Mathf.Sin(t * 3f + 2.1f);

            return new Vector2(yaw * YawDegrees, pitch * PitchDegrees);
        }

        /// <summary>
        /// The pose eased in or out towards <paramref name="target"/>, the way every other weight
        /// in this folder settles rather than switches.
        /// </summary>
        public static float Settle(float weight, float target, float deltaTime) =>
            Mathf.MoveTowards(weight, target,
                EaseSeconds <= 0f ? 1f : deltaTime / EaseSeconds);
    }
}
