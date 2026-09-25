#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// <b>How big a bird is drawn for how far away the camera is</b> (design 50 §4, the owner's
    /// answer 1): life size close in, growing to ×1.75 at the far zoom, eased between.
    ///
    /// <para>Life size alone left a rook about 5 px across at 160 m and 1080p, which is gone; ×1.75
    /// everywhere made one look oversized beside a colonist at 10–20 m. Only the drawn mesh scales.
    /// Where a bird flies and perches is the same at every zoom.</para>
    /// </summary>
    public static class BirdScale
    {
        /// <summary>At or nearer than this many metres from the focus, a bird is its real size.</summary>
        public const float Near = 25f;

        /// <summary>At or further than this, a bird is <see cref="FarScale"/>.</summary>
        public const float Far = 140f;

        /// <summary>The size at the far zoom, as a multiple of life size.</summary>
        public const float FarScale = 1.75f;

        /// <summary>The drawn size for a camera <paramref name="cameraDistance"/> metres from its focus.</summary>
        public static float For(float cameraDistance)
        {
            float t = (cameraDistance - Near) / (Far - Near);
            t = Math.Max(0f, Math.Min(1f, t));
            t = t * t * (3f - 2f * t);
            return 1f + (FarScale - 1f) * t;
        }
    }
}
