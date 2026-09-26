#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// How far one cell is from another in millimetres (design 47 §2a): the one place a shot's
    /// length is measured, because the hit's fall-off is the first rule in the simulation that
    /// needs a metre rather than a cell.
    ///
    /// <para><b>Cell centre to cell centre, over the anisotropic cell</b> — 2.5 m across, 3.0 m tall
    /// (<see cref="GridSize.CellSizeXZMm"/>, <see cref="GridSize.CellSizeYMm"/>). Height adds to the
    /// distance, and so to the fall-off, and does nothing else: there is no height bonus.</para>
    ///
    /// <para><b>Integer only.</b> The root is Newton's in <c>long</c>, never <c>Math.Sqrt</c>: a
    /// double root is exact on every machine the project has tried and is still a promise nobody
    /// has made, and a hit chance read off it goes into the state hash through the roll.</para>
    /// </summary>
    public static class RangedGeometry
    {
        /// <summary>
        /// The squared distance between the centres of cells <paramref name="a"/> and
        /// <paramref name="b"/> in square millimetres: <c>(Δx·2500)² + (Δz·2500)² + (Δy·3000)²</c>.
        /// A <c>long</c>, because the scale target's diagonal squared is about 8 × 10¹¹. Compare a
        /// squared range against this where only "within reach" is asked; take the root only where
        /// the length itself is wanted.
        /// </summary>
        public static long DistanceMm2(GridSize size, int a, int b)
        {
            CellRef p = size.FromIndex(a), q = size.FromIndex(b);
            long dx = (long)(q.X - p.X) * GridSize.CellSizeXZMm;
            long dz = (long)(q.Z - p.Z) * GridSize.CellSizeXZMm;
            long dy = (long)(q.Y - p.Y) * GridSize.CellSizeYMm;
            return dx * dx + dz * dz + dy * dy;
        }

        /// <summary>The distance between two cell centres in whole millimetres, rounded down.</summary>
        public static int DistanceMm(GridSize size, int a, int b) => ISqrt(DistanceMm2(size, a, b));

        /// <summary>The largest value <see cref="ISqrt"/> takes: the root of anything larger is not an <c>int</c>.</summary>
        public const long ISqrtMax = (long)int.MaxValue * int.MaxValue + 2L * int.MaxValue;

        /// <summary>
        /// The integer square root: the largest <c>r</c> with <c>r² ≤ v</c>, exactly. Newton's
        /// iteration from a power of two at or above the root, which falls monotonically onto the
        /// floor and stops there — a handful of divisions for any distance on any board.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="v"/> is negative or above <see cref="ISqrtMax"/>.</exception>
        public static int ISqrt(long v)
        {
            if (v < 0 || v > ISqrtMax) throw new ArgumentOutOfRangeException(nameof(v));
            if (v < 2) return (int)v;

            // 2^ceil(bits / 2) is at or above the root, so the iteration only ever falls.
            int bits = 0;
            for (long t = v; t != 0; t >>= 1) bits++;
            long x = 1L << ((bits + 1) >> 1);
            long y = (x + v / x) >> 1;
            while (y < x)
            {
                x = y;
                y = (x + v / x) >> 1;
            }
            return (int)x;
        }
    }
}
