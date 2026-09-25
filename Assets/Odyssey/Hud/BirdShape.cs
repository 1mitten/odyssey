#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>The four colour slots a bird's faces are painted from.</summary>
    public enum BirdColour : byte
    {
        Body = 0,
        Wing = 1,
        Under = 2,
        Beak = 3,
    }

    /// <summary>
    /// <b>A bird built in code</b> (design 50 §2), so the game needs no model and no licence: flat
    /// triangles for a bird of <b>unit wingspan</b>, nose along +Z, up +Y, right wing +X. Presentation
    /// copies it into a mesh once per species (<c>BirdMeshes</c>). The layout lives here so the fast
    /// tier can check it, the pattern <see cref="BloodShapes"/> set.
    ///
    /// <para><b>Unshared vertices.</b> Every triangle has its own three, because each face is one flat
    /// colour and a shared vertex would blend two. At 28–32 triangles the duplication costs nothing.</para>
    ///
    /// <para><b>The wing weight is what the shader flaps by</b>: 0 at the shoulder, 0.45 at the elbow,
    /// 1 at the tip. It lifts a vertex by <c>weight^1.4</c>, so the outer panel travels further than the
    /// inner one and the wing bends at the elbow. The body, beak and tail are weight 0 and never move.</para>
    /// </summary>
    public static class BirdShape
    {
        /// <summary>Weight at the shoulder, the elbow and the tip.</summary>
        public const float Shoulder = 0f, Elbow = 0.45f, Tip = 1f;

        /// <summary>
        /// Build <paramref name="species"/>: vertex positions as x, y, z triples, a wing weight and a
        /// colour slot per vertex, and triangles as vertex indices (0, 1, 2, 3, …, because nothing is shared).
        /// </summary>
        public static void Build(BirdSpecies species, out float[] positions, out float[] weights,
            out BirdColour[] colours, out int[] triangles)
        {
            var p = new List<float>();
            var w = new List<float>();
            var c = new List<BirdColour>();

            void Tri(Vec a, Vec b, Vec d, BirdColour colour, float wa = 0f, float wb = 0f, float wd = 0f)
            {
                p.Add(a.X); p.Add(a.Y); p.Add(a.Z);
                p.Add(b.X); p.Add(b.Y); p.Add(b.Z);
                p.Add(d.X); p.Add(d.Y); p.Add(d.Z);
                w.Add(wa); w.Add(wb); w.Add(wd);
                c.Add(colour); c.Add(colour); c.Add(colour);
            }

            // Everything below is in units of the span, so a span of 1 is the whole bird's width.
            float l = species.Length / species.Span;
            float r = l * 0.13f;

            // The body: a diamond cross-section, nose cone, mid band and tail cone. Top faces are the
            // body colour, bottom faces the underside.
            var nose = new Vec(0f, 0f, l * 0.55f);
            var tail = new Vec(0f, 0.01f, -l * 0.45f);
            Vec[] ring =
            {
                new Vec(0f, r * 0.85f, l * 0.08f),  // top
                new Vec(r, 0f, l * 0.08f),          // right
                new Vec(0f, -r * 0.85f, l * 0.08f), // bottom
                new Vec(-r, 0f, l * 0.08f),         // left
            };
            Vec[] back =
            {
                new Vec(0f, r * 0.5f, -l * 0.22f),
                new Vec(r * 0.6f, 0f, -l * 0.22f),
                new Vec(0f, -r * 0.5f, -l * 0.22f),
                new Vec(-r * 0.6f, 0f, -l * 0.22f),
            };
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                // Sides 0 (top-right) and 3 (left-top) face up; 1 and 2 face down.
                BirdColour colour = i == 0 || i == 3 ? BirdColour.Body : BirdColour.Under;
                Tri(nose, ring[j], ring[i], colour);
                // The band's quad is split along a diagonal that mirrors across the keel: the right
                // half leans one way and the left half the other, or the bird is not symmetric.
                if (i < 2)
                {
                    Tri(ring[i], ring[j], back[i], colour);
                    Tri(back[i], ring[j], back[j], colour);
                }
                else
                {
                    Tri(ring[i], ring[j], back[j], colour);
                    Tri(ring[i], back[j], back[i], colour);
                }
                Tri(back[i], back[j], tail, colour);
            }

            // The beak: two faces meeting at the tip, which is a mirror image of itself.
            float bw = 0.012f, bl = l * 0.16f;
            var tip = new Vec(0f, -0.005f, l * 0.55f + bl);
            Tri(new Vec(-bw, 0.01f, l * 0.52f), new Vec(bw, 0.01f, l * 0.52f), tip, BirdColour.Beak);
            Tri(new Vec(bw, -0.012f, l * 0.52f), new Vec(-bw, -0.012f, l * 0.52f), tip, BirdColour.Beak);

            // The tail fan: two faces, notched at the middle.
            float tw = 0.11f, tl = l * 0.32f;
            var mid = new Vec(0f, 0f, tail.Z - tl * 0.9f);
            Tri(tail, new Vec(-tw, 0f, tail.Z - tl), mid, BirdColour.Wing);
            Tri(tail, mid, new Vec(tw, 0f, tail.Z - tl), BirdColour.Wing);

            // The wings: an inner and an outer panel a side, meeting at the elbow.
            const float sweep = 0.05f;
            float chord = species.WingChord;
            foreach (float s in new[] { 1f, -1f })
            {
                var shoulderFront = new Vec(s * r * 0.8f, 0.02f, l * 0.14f);
                var shoulderBack = new Vec(s * r * 0.8f, 0.02f, -l * 0.10f);
                var elbowFront = new Vec(s * 0.24f, 0.03f, l * 0.10f - sweep * 0.5f);
                var elbowBack = new Vec(s * 0.22f, 0.03f, -l * 0.20f - sweep * 0.3f);
                var tipFront = new Vec(s * 0.5f, 0.03f, -sweep * 1.6f + 0.02f);
                var tipBack = new Vec(s * 0.47f, 0.03f, -sweep * 1.6f - chord);

                Tri(shoulderFront, elbowFront, shoulderBack, BirdColour.Wing, Shoulder, Elbow, Shoulder);
                Tri(shoulderBack, elbowFront, elbowBack, BirdColour.Wing, Shoulder, Elbow, Elbow);
                Tri(elbowFront, tipFront, elbowBack, BirdColour.Wing, Elbow, Tip, Elbow);
                Tri(elbowBack, tipFront, tipBack, BirdColour.Wing, Elbow, Tip, Tip);

                if (species.FingeredTips)
                {
                    // The primaries a soaring bird spreads: two slivers past the tip.
                    var finger = new Vec(s * 0.44f, 0.03f, tipBack.Z - 0.04f);
                    var outer = new Vec(s * 0.53f, 0.03f, tipFront.Z + 0.02f);
                    Tri(elbowBack, tipBack, finger, BirdColour.Wing, Elbow, Tip, 0.95f);
                    Tri(tipFront, outer, tipBack, BirdColour.Wing, Tip, Tip, Tip);
                }
            }

            positions = p.ToArray();
            weights = w.ToArray();
            colours = c.ToArray();
            triangles = new int[weights.Length];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
        }

        /// <summary>How many triangles a species is built from.</summary>
        public static int TriangleCount(BirdSpecies species)
        {
            Build(species, out _, out _, out _, out int[] triangles);
            return triangles.Length / 3;
        }

        /// <summary>The colour a slot is painted, as 0xRRGGBB.</summary>
        public static uint ColourOf(BirdSpecies species, BirdColour slot) => slot switch
        {
            BirdColour.Body => species.Body,
            BirdColour.Wing => species.Wing,
            BirdColour.Under => species.Under,
            BirdColour.Beak => species.Beak,
            _ => throw new ArgumentOutOfRangeException(nameof(slot)),
        };

        readonly struct Vec
        {
            public readonly float X, Y, Z;
            public Vec(float x, float y, float z) { X = x; Y = y; Z = z; }
        }
    }
}
