#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Which two gaits a speed falls between, how far between them it is, and how fast to play
    /// them. All of the arithmetic in animating a walk, and none of the Unity plumbing.
    ///
    /// It is separated out because this is where the mistakes live and they are all silent ones.
    /// Pick the wrong pair and a colonist runs on the spot; get the rate wrong and the feet skate
    /// over the grass. Neither throws, neither logs, and both look like "the animation is bad"
    /// rather than like a number being out by a factor. Pulled out here it is ordinary arithmetic
    /// with a test against it.
    ///
    /// The gait speeds handed in must be **as drawn** — after any scale on the figure — and in
    /// ascending order, slowest first, starting at zero for standing still.
    /// </summary>
    public readonly struct GaitBlend
    {
        /// <summary>The slower of the two gaits in play. Carries weight <c>1 - T</c>.</summary>
        public readonly int Lower;

        /// <summary>The faster of the two gaits in play. Carries weight <c>T</c>.</summary>
        public readonly int Upper;

        /// <summary>How far between the two, 0 to 1.</summary>
        public readonly float T;

        /// <summary>Playback rate for every clip, so a blend does not tear its own footfalls apart.</summary>
        public readonly float Rate;

        public GaitBlend(int lower, int upper, float t, float rate)
        {
            Lower = lower;
            Upper = upper;
            T = t;
            Rate = rate;
        }

        public float WeightOf(int gait) =>
            gait == Upper ? T : gait == Lower ? 1f - T : 0f;

        /// <summary>
        /// Solve the blend for a ground speed.
        ///
        /// Inside the range the gaits cover, a linear blend between the pair that bracket the
        /// speed already strides at exactly that speed — that is what a blend between two
        /// calibrated gaits means — so the clips play at their authored rate and the feet grip.
        /// Only above the fastest gait does the rate have to stretch, and that is the one case
        /// where the pawn genuinely outruns every clip anybody authored. The stretch is capped,
        /// because past a point a sped-up run reads as a cartoon rather than as haste.
        /// </summary>
        public static GaitBlend Solve(float[] speeds, float speed)
        {
            if (speeds.Length == 0) return new GaitBlend(0, 0, 1f, 1f);

            int top = speeds.Length - 1;
            if (top == 0) return new GaitBlend(0, 0, 1f, 1f);

            if (speed >= speeds[top])
            {
                float rate = speeds[top] > 0.01f ? Mathf.Clamp(speed / speeds[top], 1f, 2.5f) : 1f;
                return new GaitBlend(top, top, 1f, rate);
            }

            int upper = 1;
            while (upper < top && speed > speeds[upper]) upper++;

            float low = speeds[upper - 1];
            float high = speeds[upper];
            float t = high - low > 1e-4f ? Mathf.Clamp01((speed - low) / (high - low)) : 0f;
            return new GaitBlend(upper - 1, upper, t, 1f);
        }
    }
}
