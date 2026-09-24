#nullable enable
using System;

namespace Odyssey.Hud
{
    /// <summary>
    /// <b>The lock-on ring's clock</b> (design 33 §7b; owner, 2026-09-23: <i>"when I right click to
    /// attack an enemy it wasn't clear ... paints a red transparent circle quickly around the
    /// selected enemy to indicate that target"</i>). A translucent red ring appears at
    /// <see cref="StartScale"/> times the target's footprint, snaps inward on to its feet over
    /// <see cref="SnapSeconds"/> with a cubic ease-out, flashes brighter once as it lands, then
    /// stays faint under the target while the order holds, and fades over
    /// <see cref="FadeSeconds"/> once it does not.
    ///
    /// <para><b>Unity-free, so the fast tier owns the shape.</b> The draw is one
    /// <c>Graphics.RenderMesh</c> in presentation that nothing can assert about; the curve is the
    /// part that can be wrong in a way somebody would notice — a ring that lands late, never
    /// flashes, or hangs about after the target is dead — and here it is a function of two
    /// numbers. <c>LockOnRingTests</c>.</para>
    ///
    /// <para><b>Every number is INVENTED</b> except the 1.6 and the 0.2 s, which are the owner's,
    /// and wants the playtest.</para>
    /// </summary>
    public static class LockOnRing
    {
        /// <summary>How big the ring is when it appears, as a multiple of the target's footprint (owner).</summary>
        public const float StartScale = 1.6f;

        /// <summary>How long the snap on to the feet takes (owner: "about 0.2 s").</summary>
        public const float SnapSeconds = 0.2f;

        /// <summary>How long the landing's flash takes to settle to the faint hold. INVENTED.</summary>
        public const float FlashSeconds = 0.18f;

        /// <summary>How long the ring takes to fade once the order no longer holds. INVENTED.</summary>
        public const float FadeSeconds = 0.25f;

        /// <summary>The ring's opacity on the frame it appears, before the bracket material's own factor. INVENTED.</summary>
        public const float SnapAlpha = 0.5f;

        /// <summary>Its opacity at the instant it lands: the flash, and the brightest it ever is. INVENTED.</summary>
        public const float FlashAlpha = 1f;

        /// <summary>Its opacity while the order holds: faint, because it sits under a fight the player is watching. INVENTED.</summary>
        public const float HoldAlpha = 0.35f;

        /// <summary>When a ring has done everything but hold: the snap and the flash.</summary>
        public const float SettleSeconds = SnapSeconds + FlashSeconds;

        /// <summary>
        /// How many opacities a ring may be drawn at. The bracket material is cached per colour
        /// (<c>MaterialCache</c>), so an alpha that took any value would mint a material per
        /// distinct frame of the animation; quantised, the ring's hue can never cost more than
        /// <see cref="AlphaSteps"/> + 1 materials, created once each and shared for ever.
        /// </summary>
        public const int AlphaSteps = 32;

        /// <summary>
        /// The ring's size, as a multiple of the footprint, <paramref name="sinceOrder"/> seconds
        /// after the order: <see cref="StartScale"/> at nought, closing with a cubic ease-out to
        /// exactly 1 at <see cref="SnapSeconds"/>, and 1 after. Cubic rather than a back-out
        /// because a back-out overshoots inside the feet, and a ring smaller than the target
        /// reads as the target shrinking.
        /// </summary>
        public static float Scale(float sinceOrder)
        {
            if (sinceOrder <= 0f) return StartScale;
            if (sinceOrder >= SnapSeconds) return 1f;
            float u = 1f - sinceOrder / SnapSeconds;
            return 1f + (StartScale - 1f) * u * u * u;
        }

        /// <summary>
        /// The ring's opacity while the order holds: rising from <see cref="SnapAlpha"/> as it
        /// closes, peaking at <see cref="FlashAlpha"/> on the instant it lands, then settling to
        /// <see cref="HoldAlpha"/> over <see cref="FlashSeconds"/> with a quadratic ease-out.
        /// </summary>
        public static float HeldAlpha(float sinceOrder)
        {
            if (sinceOrder <= 0f) return SnapAlpha;
            if (sinceOrder < SnapSeconds)
                return SnapAlpha + (FlashAlpha - SnapAlpha) * (sinceOrder / SnapSeconds);
            if (sinceOrder >= SettleSeconds) return HoldAlpha;
            float u = (sinceOrder - SnapSeconds) / FlashSeconds;
            float eased = 1f - (1f - u) * (1f - u);
            return FlashAlpha + (HoldAlpha - FlashAlpha) * eased;
        }

        /// <summary>
        /// The ring <paramref name="sinceOrder"/> seconds after its order, released
        /// <paramref name="sinceRelease"/> seconds ago — negative while the order still holds.
        /// A released ring keeps the size it had and fades linearly from the opacity it had.
        /// False once it has faded out, when there is nothing left to draw.
        /// </summary>
        public static bool Evaluate(float sinceOrder, float sinceRelease, out float scale, out float alpha)
        {
            if (sinceRelease < 0f)
            {
                scale = Scale(sinceOrder);
                alpha = HeldAlpha(sinceOrder);
                return true;
            }

            if (sinceRelease >= FadeSeconds)
            {
                scale = 1f;
                alpha = 0f;
                return false;
            }

            float atRelease = Math.Max(0f, sinceOrder - sinceRelease);
            scale = Scale(atRelease);
            alpha = HeldAlpha(atRelease) * (1f - sinceRelease / FadeSeconds);
            return true;
        }

        /// <summary>An opacity on the nearest of <see cref="AlphaSteps"/> steps. See there for why.</summary>
        public static float Quantise(float alpha)
        {
            if (alpha <= 0f) return 0f;
            if (alpha >= 1f) return 1f;
            return (float)Math.Round(alpha * AlphaSteps) / AlphaSteps;
        }

        /// <summary>
        /// The footprint's radius for a box of this width and depth in metres: half its longer
        /// side, so the ring at rest touches the ends of a hog and sits just outside a person's
        /// shoulders. The ring at <see cref="StartScale"/> is this times 1.6.
        /// </summary>
        public static float FootRadius(float width, float depth) => 0.5f * Math.Max(width, depth);

        /// <summary>
        /// The radius for a person lying down, in metres: about half a body's length, because a
        /// downed colonist's box is still the standing one (design 33 §6B, open) and a ring that
        /// size would sit on her middle. INVENTED; only an order to finish somebody draws it.
        /// </summary>
        public const float DownedRadius = 1.0f;

        // ---------------------------------------------------------------- the mesh

        /// <summary>How many segments the ring is built of: round at the play camera, and 96 triangles.</summary>
        public const int Segments = 48;

        /// <summary>
        /// The inner edge's radius on a ring of outer radius 1. The band is 15 % of the radius — at
        /// a person's 0.58 m, nine centimetres: a line, not a disc.
        /// </summary>
        public const float InnerRadius = 0.85f;

        /// <summary>
        /// Where vertex <paramref name="index"/> of the flat ring lies, on the ground plane, for a
        /// ring of outer radius 1: vertices 0 to <c>segments - 1</c> are the outer edge, the next
        /// <c>segments</c> the inner, both anticlockwise seen from above. Here, rather than in the
        /// mesh builder, so the fast tier can check which way it faces.
        /// </summary>
        public static void RingVertex(int index, int segments, out float x, out float z)
        {
            bool inner = index >= segments;
            int around = inner ? index - segments : index;
            double angle = 2.0 * Math.PI * around / segments;
            float radius = inner ? InnerRadius : 1f;
            x = (float)(radius * Math.Cos(angle));
            z = (float)(radius * Math.Sin(angle));
        }

        /// <summary>
        /// The ring's triangles, two to a segment, <b>wound to face up</b>: for every triangle
        /// (a, b, c), <c>(b - a) × (c - a)</c> points up, which is the rule
        /// <c>PrimitiveMeshTests.TheCubeFacesOutwards</c> holds the cube to. The obvious order
        /// — outer, next outer, inner — faces down, and a flat ring wound that way is culled from
        /// the only side the play camera ever sees.
        /// </summary>
        public static int[] RingTriangles(int segments)
        {
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int outer = i, outerNext = next, inner = segments + i, innerNext = segments + next;
                triangles[i * 6 + 0] = outer;
                triangles[i * 6 + 1] = inner;
                triangles[i * 6 + 2] = outerNext;
                triangles[i * 6 + 3] = inner;
                triangles[i * 6 + 4] = innerNext;
                triangles[i * 6 + 5] = outerNext;
            }
            return triangles;
        }
    }
}
