#nullable enable
using UnityEngine;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Grass thinned with distance from the camera without a pop (design 38 §5, §21): full cover
    /// near, fewer clumps further out, down to the Meadow rung's density in the far field.
    ///
    /// <para>Every clump has a <see cref="Rank"/> in [0, 1) from a hash of where it stands, and a
    /// distance has a <see cref="Keep"/> fraction. A clump is drawn while its rank is below the keep
    /// of its own distance, shrinking to nothing over the last <see cref="Soft"/> of it — so as the
    /// camera moves the far field loses and gains single clumps smoothly, never a chunk's worth at a
    /// seam. The shader does that shrink per clump (<c>Odyssey/Foliage</c>, <c>FoliageThin</c>);
    /// the CPU only stops submitting what cannot survive anywhere in a chunk: each grass bucket is
    /// sorted by rank once when the chunk is meshed, so the survivors are a prefix and the count is
    /// a binary search (<see cref="CountBelow"/>).</para>
    ///
    /// <para><b>The rank is mirrored in HLSL</b> (<c>FoliageRank</c>) and must agree exactly: both
    /// read the instance's translation — the same floats, since the shader is handed the matrix the
    /// mesher wrote — quantise it to a sixteenth of a metre (a power of two, so exact), and run the
    /// same integer PCG hash. <c>GrassThinningTests</c> pins the arithmetic.</para>
    /// </summary>
    public static class GrassThinning
    {
        /// <summary>The width of the shrink, in rank: a clump fades out over the last 5% of the keep
        /// fraction rather than disappearing at it.</summary>
        public const float Soft = 0.05f;

        /// <summary>A clump's place in the thinning order, in [0, 1). Deterministic in its position.</summary>
        public static float Rank(float x, float z)
        {
            uint ix = unchecked((uint)(int)Mathf.Floor(x * 16f));
            uint iz = unchecked((uint)(int)Mathf.Floor(z * 16f));
            uint h = Pcg(unchecked(ix + Pcg(iz)));
            return (h >> 8) * (1f / 16777216f);
        }

        /// <summary>The rank of the clump a placement matrix stands at.</summary>
        public static float Rank(in Matrix4x4 placement) => Rank(placement.m03, placement.m23);

        static uint Pcg(uint v)
        {
            unchecked
            {
                uint state = v * 747796405u + 2891336453u;
                uint word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
                return (word >> 22) ^ word;
            }
        }

        /// <summary>
        /// The fraction of clumps kept at a distance from the camera: all of them out to
        /// <paramref name="near"/>, then falling as the square of near over distance — the rate that
        /// keeps the number of clumps per pixel of screen roughly constant, since a clump's image
        /// shrinks with the square of its distance — and never below <paramref name="floor"/>.
        ///
        /// <para>Every rung is thinned, Meadow included: measured at the farthest pull, the cost of
        /// grass was having it at all (Meadow cost what Full did), not how much of it there was, so a
        /// rule that only thinned the rungs above Meadow bought nothing where the owner saw the drop
        /// (design 38 §21).</para>
        /// </summary>
        public static float Keep(float distance, float near, float floor)
        {
            if (distance <= near || near <= 0f) return 1f;
            float r = near / distance;
            return Mathf.Max(floor, r * r);
        }

        /// <summary>
        /// How many of a bucket's first <paramref name="count"/> matrices — sorted by rank — have a
        /// rank below <paramref name="keep"/>: the prefix worth submitting.
        /// </summary>
        public static int CountBelow(Matrix4x4[] sorted, int count, float keep)
        {
            if (keep >= 1f) return count;
            int lo = 0, hi = count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (Rank(in sorted[mid]) < keep) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        /// <summary>Sort a bucket's first <paramref name="count"/> matrices by rank, in place, using
        /// <paramref name="keys"/> as scratch (grown when too small).</summary>
        public static void SortByRank(Matrix4x4[] matrices, int count, ref float[] keys)
        {
            if (count < 2) return;
            if (keys.Length < count) keys = new float[Mathf.NextPowerOfTwo(count)];
            for (int i = 0; i < count; i++) keys[i] = Rank(in matrices[i]);
            System.Array.Sort(keys, matrices, 0, count);
        }
    }
}
