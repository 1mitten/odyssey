#nullable enable

namespace Odyssey.Sim.Worldgen
{
    /// <summary>
    /// Integer value noise. Every worldgen field that wants smooth variation — district damage
    /// intensity, surface intactness, the fill/soil/rock boundaries, caves — comes from here.
    ///
    /// Written rather than taken from a library for two reasons. The first is the hard rule that
    /// nothing in the simulation may be floating point (<see cref="Contracts.StateHash"/> refuses
    /// to hash a float on purpose): a float noise field would make the map depend on the exact
    /// rounding of the machine that generated it. The second is that it is about forty lines, and
    /// a dependency for forty lines is a bad trade.
    ///
    /// Values are in [0, <see cref="Scale"/>) throughout. Lattice values come from a 32-bit
    /// avalanche hash of the integer coordinate, so there is no permutation table to carry, no
    /// state to save, and evaluation at a point is O(1) and order-independent — a pass may
    /// evaluate cells in any order and get the same field.
    /// </summary>
    public static class ValueNoise
    {
        /// <summary>Exclusive upper bound of every value this class returns.</summary>
        public const int Scale = 1024;

        /// <summary>Fixed-point denominator for the interpolation fraction.</summary>
        const int FractionOne = 256;

        static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        static int Lattice(uint seed, int a, int b)
        {
            unchecked
            {
                uint h = seed ^ 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                return (int)(Mix(h) >> 22);
            }
        }

        static int Lattice(uint seed, int a, int b, int c)
        {
            unchecked
            {
                uint h = seed ^ 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)c) * 16777619u;
                return (int)(Mix(h) >> 22);
            }
        }

        /// <summary>Floor division, so negative coordinates tile the same way positive ones do.</summary>
        static int FloorDiv(int value, int divisor)
        {
            int q = value / divisor;
            if ((value % divisor) != 0 && ((value < 0) != (divisor < 0))) q--;
            return q;
        }

        /// <summary>The classic 3t^2 - 2t^3 ease, in fixed point. Input and output 0..255.</summary>
        static int Smooth(int t) => (t * t * (3 * FractionOne - 2 * t)) >> 16;

        static int Lerp(int a, int b, int t) => a + (((b - a) * t) >> 8);

        /// <summary>One octave of 2D value noise with the given lattice period, in cells.</summary>
        public static int Value2D(uint seed, int x, int z, int period)
        {
            if (period < 1) period = 1;
            int cx = FloorDiv(x, period);
            int cz = FloorDiv(z, period);
            int tx = Smooth((x - cx * period) * FractionOne / period);
            int tz = Smooth((z - cz * period) * FractionOne / period);

            int v00 = Lattice(seed, cx, cz);
            int v10 = Lattice(seed, cx + 1, cz);
            int v01 = Lattice(seed, cx, cz + 1);
            int v11 = Lattice(seed, cx + 1, cz + 1);

            return Lerp(Lerp(v00, v10, tx), Lerp(v01, v11, tx), tz);
        }

        /// <summary>
        /// One octave of 2D value noise that repeats every <paramref name="circumference"/> along x
        /// (design 57 §4b): the lattice column is taken modulo the number of cells round the
        /// circumference, so the field's east edge meets its west with no seam and no trigonometry —
        /// the planet's wrap, in integers. <paramref name="period"/> must divide the circumference.
        /// </summary>
        public static int Value2DWrapped(uint seed, int x, int z, int period, int circumference)
        {
            if (period < 1) period = 1;
            int cells = circumference / period;
            if (cells < 1) cells = 1;
            int wrapped = x % circumference;
            if (wrapped < 0) wrapped += circumference;
            int cx = wrapped / period;
            int cz = FloorDiv(z, period);
            int tx = Smooth((wrapped - cx * period) * FractionOne / period);
            int tz = Smooth((z - cz * period) * FractionOne / period);
            int cx1 = (cx + 1) % cells;

            int v00 = Lattice(seed, cx, cz);
            int v10 = Lattice(seed, cx1, cz);
            int v01 = Lattice(seed, cx, cz + 1);
            int v11 = Lattice(seed, cx1, cz + 1);

            return Lerp(Lerp(v00, v10, tx), Lerp(v01, v11, tx), tz);
        }

        /// <summary>One octave of 3D value noise. The third axis is the layer.</summary>
        public static int Value3D(uint seed, int x, int z, int y, int period)
        {
            if (period < 1) period = 1;
            int cx = FloorDiv(x, period);
            int cz = FloorDiv(z, period);
            int cy = FloorDiv(y, period);
            int tx = Smooth((x - cx * period) * FractionOne / period);
            int tz = Smooth((z - cz * period) * FractionOne / period);
            int ty = Smooth((y - cy * period) * FractionOne / period);

            int a = Lerp(Lerp(Lattice(seed, cx, cz, cy), Lattice(seed, cx + 1, cz, cy), tx),
                         Lerp(Lattice(seed, cx, cz + 1, cy), Lattice(seed, cx + 1, cz + 1, cy), tx), tz);
            int b = Lerp(Lerp(Lattice(seed, cx, cz, cy + 1), Lattice(seed, cx + 1, cz, cy + 1), tx),
                         Lerp(Lattice(seed, cx, cz + 1, cy + 1), Lattice(seed, cx + 1, cz + 1, cy + 1), tx), tz);
            return Lerp(a, b, ty);
        }

        /// <summary>
        /// Summed octaves, each at half the period and half the weight of the last. The result is
        /// renormalised to [0, <see cref="Scale"/>) by the accumulated weight, so the band a
        /// threshold compares against does not shift when the octave count changes.
        /// </summary>
        public static int Fractal2D(uint seed, int x, int z, int period, int octaves)
        {
            int sum = 0, weight = 1024, total = 0, p = period;
            for (int o = 0; o < octaves && weight > 0; o++)
            {
                sum += Value2D(seed + (uint)o * 7919u, x, z, p) * weight;
                total += weight;
                weight >>= 1;
                p = p > 1 ? p >> 1 : 1;
            }
            return total == 0 ? 0 : sum / total;
        }

        public static int Fractal3D(uint seed, int x, int z, int y, int period, int octaves)
        {
            int sum = 0, weight = 1024, total = 0, p = period;
            for (int o = 0; o < octaves && weight > 0; o++)
            {
                sum += Value3D(seed + (uint)o * 7919u, x, z, y, p) * weight;
                total += weight;
                weight >>= 1;
                p = p > 1 ? p >> 1 : 1;
            }
            return total == 0 ? 0 : sum / total;
        }

        /// <summary>Maps a 0..1023 noise value onto [min, max], inclusive, with integer maths.</summary>
        public static int Band(int noise, int min, int max)
        {
            if (max <= min) return min;
            return min + noise * (max - min + 1) / Scale;
        }
    }
}
