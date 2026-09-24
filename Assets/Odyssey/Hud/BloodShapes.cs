#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// <b>The outlines of the three marks</b> (design 33 §10a), as flat triangle fans lying on the
    /// ground plane — x and z, facing up, inside a <b>unit radius</b> so a placement's scale is the
    /// mark's radius. Presentation copies them into meshes once (<c>PrimitiveMeshes.Blood</c>);
    /// the layout and the winding live here so the fast tier can check them, which is how the
    /// lock-on ring is built (<see cref="LockOnRing"/>).
    ///
    /// <para>Irregular by a fixed hash rather than by chance, so the shapes are the same every run
    /// and a mark's variety comes from its placement — turned along the blow, stretched, sized —
    /// rather than from a second mesh per mark. <b>+x is the way the blow went</b>: a splatter's
    /// satellite drops are thrown ahead of it.</para>
    /// </summary>
    public static class BloodShapes
    {
        /// <summary>Rim vertices of a main blob.</summary>
        public const int BlobRim = 20;

        /// <summary>Rim vertices of a satellite drop.</summary>
        public const int DropRim = 7;

        /// <summary>
        /// The outline of <paramref name="shape"/>: vertex positions in <paramref name="xs"/> and
        /// <paramref name="zs"/>, and triangles wound to face up.
        /// </summary>
        public static void Build(BloodShape shape, out float[] xs, out float[] zs, out int[] triangles)
        {
            var x = new List<float>();
            var z = new List<float>();
            var t = new List<int>();

            switch (shape)
            {
                case BloodShape.Splatter:
                    Blob(x, z, t, -0.18f, 0f, 0.62f, 0.22f, BlobRim, seed: 11);
                    // Thrown ahead along the blow, fanning out and shrinking with distance.
                    Blob(x, z, t, 0.52f, 0.10f, 0.13f, 0.2f, DropRim, seed: 21);
                    Blob(x, z, t, 0.66f, -0.16f, 0.10f, 0.2f, DropRim, seed: 22);
                    Blob(x, z, t, 0.78f, 0.24f, 0.08f, 0.2f, DropRim, seed: 23);
                    Blob(x, z, t, 0.86f, -0.02f, 0.07f, 0.2f, DropRim, seed: 24);
                    Blob(x, z, t, 0.88f, -0.24f, 0.05f, 0.2f, DropRim, seed: 25);
                    Blob(x, z, t, 0.40f, -0.40f, 0.06f, 0.2f, DropRim, seed: 26);
                    break;
                case BloodShape.Spot:
                    Blob(x, z, t, 0f, 0f, 0.78f, 0.16f, BlobRim, seed: 31);
                    Blob(x, z, t, 0.82f, 0.30f, 0.09f, 0.2f, DropRim, seed: 41);
                    Blob(x, z, t, -0.70f, -0.48f, 0.07f, 0.2f, DropRim, seed: 42);
                    break;
                case BloodShape.Pool:
                    Blob(x, z, t, 0f, 0f, 0.86f, 0.12f, BlobRim + 8, seed: 51);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
            }

            xs = x.ToArray();
            zs = z.ToArray();
            triangles = t.ToArray();
        }

        /// <summary>
        /// One fan: a centre vertex and <paramref name="rim"/> vertices around it at a radius that
        /// wobbles by up to <paramref name="wobble"/> of itself.
        /// </summary>
        static void Blob(List<float> xs, List<float> zs, List<int> triangles,
            float cx, float cz, float radius, float wobble, int rim, int seed)
        {
            int centre = xs.Count;
            xs.Add(cx);
            zs.Add(cz);

            for (int i = 0; i < rim; i++)
            {
                double angle = 2.0 * Math.PI * i / rim;
                // Two harmonics and a per-vertex hash: lumpy, not spiky.
                double lump = 0.55 * Math.Sin(3.0 * angle + seed) + 0.30 * Math.Sin(5.0 * angle + 2.0 * seed)
                              + 0.15 * (Hash(seed * 131 + i) * 2.0 - 1.0);
                double r = radius * (1.0 + wobble * lump);
                xs.Add(cx + (float)(r * Math.Cos(angle)));
                zs.Add(cz + (float)(r * Math.Sin(angle)));
            }

            // (centre, next, this) is the winding that faces up in this plane (LockOnRing's test).
            for (int i = 0; i < rim; i++)
            {
                int here = centre + 1 + i;
                int next = centre + 1 + (i + 1) % rim;
                triangles.Add(centre);
                triangles.Add(next);
                triangles.Add(here);
            }
        }

        /// <summary>A fixed hash in [0, 1).</summary>
        static double Hash(int n)
        {
            unchecked
            {
                uint h = (uint)n * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xFFFFFF) / (double)0x1000000;
            }
        }
    }
}
