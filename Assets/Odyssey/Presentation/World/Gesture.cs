#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// A one-shot motion: how deep it goes, how long it takes, and how it gets there and back.
    ///
    /// <para><b>Why this is not a <see cref="WorkStroke"/>.</b> A stroke is a cycle driven by a
    /// clock that runs for as long as a state holds, and the figure is somewhere in it at every
    /// instant. A lift happens once, in response to something that has already finished, and then
    /// it is over. The difference is not in the curve — both are a phase from 0 to 1 — but in what
    /// starts and ends them, and pretending otherwise would mean a lift that repeated for as long
    /// as the colonist was holding something.</para>
    ///
    /// <para><b>The deeper difference is what the pose is solved against.</b> Every angle in
    /// <see cref="WorkStroke"/> had to be settled against a photograph, because a stroke sweeps an
    /// arc at a thing whose position the arithmetic does not know — knowing where the trunk is
    /// tells you nothing about what the shoulder is doing eight-tenths of a second earlier. A lift
    /// is the other case: the hands have to arrive at a point we know exactly, so the pose is
    /// solved rather than authored, and it is then correct on all sixty-one rigs and on ground the
    /// relief has tilted, with no contact sheet at all. <c>13-gestures.md</c> §3 is the argument.
    /// What is left here is the timing, which is taste, and a shape that is not.</para>
    ///
    /// <para>The one authored number is the <em>depth</em>, and it is a fraction of the figure's
    /// own standing hip height rather than a distance in metres, for the same reason: a 0.4 m drop
    /// is a squat on a short colonist and a nod on a tall one.</para>
    /// </summary>
    public readonly struct Gesture
    {
        /// <summary>How long the whole motion takes, in seconds of game time.</summary>
        public readonly float Seconds;

        /// <summary>
        /// How far down the figure goes at the bottom, as a fraction of its standing hip height.
        /// </summary>
        public readonly float Depth;

        /// <summary>Where in the motion the descent finishes.</summary>
        public readonly float DownEnds;

        /// <summary>Where in the motion the rise begins. Between the two, the hands are at the floor.</summary>
        public readonly float HoldEnds;

        public Gesture(float seconds, float depth, float downEnds, float holdEnds)
        {
            Seconds = seconds;
            Depth = depth;
            DownEnds = downEnds;
            HoldEnds = holdEnds;
        }

        /// <summary>
        /// How far into the motion the figure is at this phase, 0 at standing and 1 at the bottom.
        ///
        /// <para><b>Down quickly, hold, up slowly</b>, and the asymmetry is the whole of what makes
        /// it read as a lift rather than as a bob. Going down is a fall the knees catch: it is
        /// eased out only, so it leaves at speed and settles. Coming up is work against the weight
        /// of whatever has just been picked up, so it is eased in — slow to start, quickest in the
        /// middle. Reverse the two and the figure appears to be putting something down.</para>
        ///
        /// <para>The hold in the middle is short and it is not decoration. Without it the hands
        /// touch the floor for exactly one instant, and at thirty frames a second that instant is
        /// as likely as not to fall between two frames — so the one moment the gesture exists to
        /// show is the one most likely never to be drawn.</para>
        /// </summary>
        public float At(float phase)
        {
            if (phase <= 0f) return 0f;
            if (phase >= 1f) return 0f;

            if (phase < DownEnds)
            {
                // Eased out: fast away from standing, settling into the crouch.
                float t = phase / Mathf.Max(DownEnds, 1e-4f);
                return 1f - (1f - t) * (1f - t);
            }

            if (phase < HoldEnds) return 1f;

            // Eased in: slow off the floor, then away.
            float rise = (phase - HoldEnds) / Mathf.Max(1f - HoldEnds, 1e-4f);
            return 1f - rise * rise;
        }

        /// <summary>
        /// Where the hands are, 0 at the figure's own rest and 1 at the thing on the floor.
        ///
        /// <para>The same curve as <see cref="At"/> and deliberately so: hands that reach the floor
        /// before the knees have bent belong to somebody bowing, and knees that bend before the
        /// hands move belong to somebody sitting down. One curve is the cheapest possible way of
        /// saying they are one motion.</para>
        /// </summary>
        public float Hands(float phase) => At(phase);

        /// <summary>Whether a motion of this length is over.</summary>
        public bool Finished(float elapsed) => elapsed >= Seconds;

        /// <summary>How far through, 0 to 1, clamped at both ends.</summary>
        public float Phase(float elapsed) =>
            Seconds <= 1e-4f ? 1f : Mathf.Clamp01(elapsed / Seconds);

        /// <summary>
        /// Taking something off the ground. **Proposed, not settled.**
        ///
        /// <para>Eight-tenths of a second, which is about what it takes a person to stoop and
        /// straighten and is short enough that a colonist on a short haul is not still rising when
        /// it reaches the pile. A third of the hip height is a real bend at the knee without being
        /// a squat — a colonist picking up a log is not doing a deadlift.</para>
        /// </summary>
        public static readonly Gesture Lift = new Gesture(
            seconds: 0.8f, depth: 0.33f, downEnds: 0.4f, holdEnds: 0.55f);

        /// <summary>
        /// Setting something down. Slower going down and quicker coming up, which is the lift
        /// turned over: the load is lowered under control and the empty body straightens freely.
        /// </summary>
        public static readonly Gesture Stow = new Gesture(
            seconds: 0.85f, depth: 0.33f, downEnds: 0.5f, holdEnds: 0.62f);

        /// <summary>
        /// Working seed into a plot. The pickup re-timed (owner, 2026-09-18: *"the animation for
        /// sowing seeds should not be chopping axe - reuse the pickup animation where the
        /// colonist goes to knees and holds for a while, then comes to feet"*): the same depth and
        /// the same curve, with the hold stretched over most of the motion because the hold
        /// <i>is</i> the work. Timed against the carrot's <c>sowWorkTicks</c> at the tuned rate —
        /// about 2.8 s — so the rise lands as the settle toil begins; the clock is the gesture's
        /// own and not the toil's, which is the one approximation in the reuse and the reason
        /// this page says "for now" with the owner's own words.
        /// </summary>
        public static readonly Gesture Sow = new Gesture(
            seconds: 2.6f, depth: 0.33f, downEnds: 0.16f, holdEnds: 0.84f);

        /// <summary>
        /// How old a sow kneel must be before the seed specks may appear, in seconds of the
        /// gesture's own clock. Half the motion, which is the middle of the hold — the figure
        /// is down at the soil and working before the ground shows anything (owner, 2026-09-19:
        /// *"it should have a delay so the colonist is actually bent down for some time and
        /// seeds appear"*).
        ///
        /// <para><b>A property of the gesture, not a number in the caller.</b> The specks are
        /// gated on this wherever they are drawn, so retiming the kneel retimes the seeds with
        /// it and the two cannot disagree — the same argument that gives every swing its price
        /// in one owner. Measured on the <see cref="GestureSerial"/> clock presentation keeps
        /// for each figure, because the specks are a drawing of the kneel and owe their timing
        /// to nothing the simulation stores.</para>
        /// </summary>
        public static float SeedSpecksAfter => Sow.Seconds * 0.5f;
    }
}
