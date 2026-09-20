#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.World
{
    /// <summary>
    /// Kinematics for loose items and commodities falling to a floor below when ground or flooring
    /// beneath them is removed or collapses. Design 26.
    ///
    /// <para><b>Physics-based acceleration scaling with drop height.</b> A single-layer fall (3 m)
    /// completes in ~0.40 seconds; deeper multi-storey drops scale with the square root of height
    /// up to an 0.85 s cap, accelerating downward (<c>t^2</c>) to give a convincing gravitational
    /// punch on touchdown rather than a linear glide or constant-duration float.</para>
    /// </summary>
    public static class ItemFallMotion
    {
        public const float MinDuration = 0.30f;
        public const float MaxDuration = 0.85f;
        public const float BaseLayerDuration = 0.40f;

        /// <summary>
        /// How long an item takes to fall a given vertical distance in metres.
        /// </summary>
        public static float Duration(float dropHeightMetres)
        {
            if (dropHeightMetres <= 0.01f) return 0f;
            float layers = dropHeightMetres / 3.0f;
            float duration = 0.25f + 0.15f * Mathf.Sqrt(layers);
            return Mathf.Clamp(duration, MinDuration, MaxDuration);
        }

        /// <summary>
        /// The progress of a fall, 0 at the upper release point and 1 on the floor below.
        /// Eased in quadratically to reflect gravitational acceleration (v = gt, d = 0.5 * g * t^2).
        /// </summary>
        public static float Fallen(float elapsed, float duration)
        {
            if (duration <= 1e-4f) return 1f;
            float phase = Mathf.Clamp01(elapsed / duration);
            return phase * phase;
        }

        /// <summary>
        /// The 3D displacement remaining to the landing floor: added to the landing matrix placement
        /// so that at t=0 it draws at the starting position, and at t=duration it reaches zero.
        /// </summary>
        public static Vector3 FallingOffset(Vector3 startPos, Vector3 targetPos, float elapsed, float duration)
        {
            float t = Fallen(elapsed, duration);
            return Vector3.Lerp(startPos - targetPos, Vector3.zero, t);
        }

        /// <summary>Whether a fall is complete.</summary>
        public static bool FallFinished(float elapsed, float duration) => elapsed >= duration;

        /// <summary>
        /// Whether a fall crossed its completion boundary between two elapsed frames — the touchdown frame.
        /// </summary>
        public static bool FallLanded(float before, float after, float duration) =>
            before < duration && after >= duration;
    }
}
