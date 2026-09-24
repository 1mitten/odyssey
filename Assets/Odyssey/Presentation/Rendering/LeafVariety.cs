#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// The colour a Meadow tree's leaves are dealt from where it stands (owner, 2026-09-24:
    /// "can there be more variety in the colour of the tree leaves" — mixed stands, like the
    /// reference; design 38 §17c).
    ///
    /// <para><b>The shader is the owner; this is its mirror.</b> <c>Odyssey/Foliage</c> deals the
    /// colour in its vertex stage from the instance's own position, so no colour is uploaded per
    /// tree and nothing is meshed, saved or hashed. This class repeats the same arithmetic so the
    /// distribution can be counted in a test — and <c>LeafVarietyTests</c> reads the constants out
    /// of the shader source and fails if the two drift apart, which is the only way a rule with two
    /// copies stays one rule (P1).</para>
    /// </summary>
    public static class LeafVariety
    {
        public enum Family { Green, Lime, Gold, Orange, Red }

        // The constants, exactly as the shader writes them.
        public const float StandScale = 0.018f;
        public const float StandOffset = 3.1f;
        public const float TreeScale = 0.731f;
        public const float TreeOffset = 11.3f;
        public const float PickScale = 1.917f;
        public const float PickOffset = 3.7f;
        public const float StandWeight = 0.7f;
        public const float AutumnAbove = 0.58f;
        public const float RedAbove = 0.965f;
        public const float LimeAbove = 0.8f;

        /// <summary>The family a tree standing at world <paramref name="x"/>, <paramref name="z"/> is dealt.</summary>
        public static Family At(float x, float z)
        {
            float s = Noise(x * StandScale + StandOffset, z * StandScale + StandOffset);
            float h = Hash(x * TreeScale + TreeOffset, z * TreeScale + TreeOffset);
            float h2 = Hash(x * PickScale + PickOffset, z * PickScale + PickOffset);
            float t = s * StandWeight + h * (1f - StandWeight);
            if (h2 > RedAbove) return Family.Red;
            if (t > AutumnAbove) return h2 < 0.5f ? Family.Gold : Family.Orange;
            return h > LimeAbove ? Family.Lime : Family.Green;
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        /// <summary><c>FoliageHash</c> in the shader.</summary>
        public static float Hash(float x, float y)
        {
            float px = Frac(x * 0.1031f), py = Frac(y * 0.1030f);
            float d = px * (py + 33.33f) + py * (px + 33.33f);
            px += d;
            py += d;
            return Frac((px + py) * px);
        }

        /// <summary><c>FoliageNoise</c> in the shader: value noise with a smoothstep.</summary>
        public static float Noise(float x, float y)
        {
            float ix = Mathf.Floor(x), iy = Mathf.Floor(y);
            float fx = x - ix, fy = y - iy;
            float ux = fx * fx * (3f - 2f * fx), uy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1f, iy), c = Hash(ix, iy + 1f), d = Hash(ix + 1f, iy + 1f);
            float bottom = a + (b - a) * ux, top = c + (d - c) * ux;
            return bottom + (top - bottom) * uy;
        }
    }
}
