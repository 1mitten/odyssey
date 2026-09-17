#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// What a colonist in the water does with itself, as arithmetic.
    ///
    /// <para><b>Computed, not animated,</b> for the reason every pose in this folder is: no pack
    /// contains a swim clip, and the axe, pick and hammer strokes are already standing in for art
    /// we do not have. <c>13-gestures.md</c> §3 draws the line — author the angles when the figure
    /// aims at something whose position the arithmetic does not know, solve to a point when it must
    /// meet something it does. A swim is the first kind: there is nothing to reach for, only a
    /// cycle, so the angles are authored and the shape is the thing to get right.</para>
    ///
    /// <para><b>It is a stroke, not a gesture</b>, in this project's own vocabulary
    /// (<see cref="Gesture"/>'s own note draws the distinction). A lift happens once in response to
    /// something that has already finished; a swim is a cycle that runs for as long as the figure
    /// is in the water, and the figure is somewhere in it at every instant. So the phase comes off
    /// a clock, exactly as <see cref="WorkStroke"/>'s does.</para>
    ///
    /// <para><b>Everything is an angle or a fraction, never a distance in metres</b>, so it is
    /// right on all sixty-one characters without being authored sixty-one times — the packs differ
    /// in proportion and the director scales them besides.</para>
    /// </summary>
    public static class SwimPose
    {
        /// <summary>
        /// How far the body is pitched from upright towards prone, in degrees.
        ///
        /// <para>Not the full ninety. A person swimming at their own pace is not flat like a racer;
        /// the head is up, the chest is higher than the hips and the legs trail down behind. Flat
        /// on at this camera height also reads as a body floating face down, which is a different
        /// picture entirely and not the one anybody wants of their colonists.</para>
        /// </summary>
        public static float PitchDegrees { get; set; } = 62f;

        /// <summary>
        /// Full strokes a second at an ordinary swim.
        ///
        /// <para>Slower than a walk's cadence and deliberately: the whole read of swimming is that
        /// it is laboured. Front crawl is nearer one a second; this is a colonist crossing a stream
        /// with its belongings, not a swimmer doing lengths.</para>
        /// </summary>
        public static float StrokesPerSecond { get; set; } = 0.65f;

        /// <summary>How far an arm goes forward at the top of its reach, in degrees.</summary>
        public static float Reaching { get; set; } = -145f;

        /// <summary>And back at the end of the pull, alongside the hip.</summary>
        public static float Pulling { get; set; } = -20f;

        /// <summary>A little bend at the elbow, so the arm is not a plank.</summary>
        public static float ElbowBend { get; set; } = -22f;

        /// <summary>
        /// How far each leg kicks either side of trailing, in degrees.
        ///
        /// <para>Small. The legs of an ordinary swimmer flutter; they do not cycle like a walk, and
        /// a big kick at this distance reads as running in mid-air — which is precisely the fault
        /// the climb pose had before its legs were bound.</para>
        /// </summary>
        public static float KickDegrees { get; set; } = 14f;

        /// <summary>
        /// Kicks per stroke. Two, which is the slow end of what a real swimmer does and is what
        /// keeps the legs subordinate to the arms rather than competing with them.
        /// </summary>
        public static float KicksPerStroke { get; set; } = 2f;

        /// <summary>How long the pose takes to come on and go off, in seconds.</summary>
        public static float EaseSeconds { get; set; } = 0.35f;

        public static void Reset()
        {
            PitchDegrees = 62f;
            StrokesPerSecond = 0.65f;
            Reaching = -145f;
            Pulling = -20f;
            ElbowBend = -22f;
            KickDegrees = 14f;
            KicksPerStroke = 2f;
            EaseSeconds = 0.35f;
        }

        /// <summary>The phase of the stroke cycle at this clock reading, 0 to 1.</summary>
        public static float Phase(float clock)
        {
            float phase = clock * StrokesPerSecond;
            return phase - Mathf.Floor(phase);
        }

        /// <summary>
        /// The stroke's own sine, -1 to 1, where +1 is the right arm fully forward.
        ///
        /// <para>One number, read once, and both arms and both legs derived from it — the same
        /// bargain <c>ApplyClimbPose</c> makes. Deriving them separately is how the limbs come to
        /// disagree about which half of the cycle they are in.</para>
        /// </summary>
        public static float Swing(float phase) => Mathf.Sin(phase * 2f * Mathf.PI);

        /// <summary>
        /// The two arm pitches for this swing, in degrees, before the weight is applied.
        ///
        /// <para><b>Alternating, not together.</b> Both arms sweeping at once is a butterfly, which
        /// is a competitive stroke and reads as thrashing. The opposite phase is a front crawl,
        /// which is what a person crossing water actually does.</para>
        /// </summary>
        public static (float right, float left) Arms(float swing)
        {
            float t = (swing + 1f) * 0.5f;
            return (Mathf.Lerp(Pulling, Reaching, t), Mathf.Lerp(Reaching, Pulling, t));
        }

        /// <summary>
        /// The two leg pitches for this swing, in degrees, relative to trailing.
        ///
        /// <para><b>Contralateral with the arms</b>, the same relation <see cref="ClimbPose"/>
        /// keeps and for the same reason: the right arm reaches with the left leg. Same-side pairs
        /// move together in a bound, which belongs to an animal and not to a colonist.</para>
        /// </summary>
        /// <para>Driven off the <em>same</em> phase as the arms at a whole-number multiple, never
        /// off a second clock. Two clocks at a ratio of two agree at the start of a session and
        /// disagree after a float has accumulated for an hour, and the symptom — legs sliding
        /// slowly out of time with the arms — is the sort of thing that gets blamed on the art.
        /// </para>
        public static (float right, float left) Legs(float phase)
        {
            float kick = Mathf.Sin(phase * KicksPerStroke * 2f * Mathf.PI);
            return (kick * KickDegrees, -kick * KickDegrees);
        }

        /// <summary>One frame of easing towards being a swimmer, or away from it.</summary>
        public static float Settle(float weight, float wanted, float deltaTime) =>
            Mathf.MoveTowards(weight, wanted, deltaTime / Mathf.Max(1e-4f, EaseSeconds));
    }
}
