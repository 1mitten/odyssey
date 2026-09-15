using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Bench
{
    // D1 benchmark, candidate "plain".
    // Phase 1 (grid propagation) is the designated Burst-jobbed hot path.
    // It is a single IJob, executed with .Run() on the calling thread: single-threaded,
    // as the contract requires, but Burst-compiled native code.
    [BurstCompile]
    public struct GridJob : IJob
    {
        public const int CellCount = 2500000;
        public const int FrontierCap = 8000;
        public const int TopUpTarget = 2000;

        [ReadOnly] public NativeArray<byte> solid;
        public NativeArray<short> temp;
        public NativeArray<byte> mark;
        [ReadOnly] public NativeArray<int> cur;
        public NativeArray<int> next;

        // meta[0] = current frontier count (in), meta[1] = next frontier count (out)
        public NativeArray<int> meta;
        // seed[0] = the one shared xorshift32 stream
        public NativeArray<uint> seed;

        public void Execute()
        {
            int curCount = meta[0];
            int nextCount = 0;
            uint s = seed[0];

            for (int ci = 0; ci < curCount; ci++)
            {
                int c = cur[ci];
                int x = c % 250;
                int rest = c / 250;
                int z = rest % 250;
                int y = rest / 250;

                // Fixed neighbour order: -x, +x, -z, +z, -y, +y. Each bounds-checked on its own axis.
                if (x > 0) Step(c, c - 1, ref nextCount);
                if (x < 249) Step(c, c + 1, ref nextCount);
                if (z > 0) Step(c, c - 250, ref nextCount);
                if (z < 249) Step(c, c + 250, ref nextCount);
                if (y > 0) Step(c, c - 62500, ref nextCount);
                if (y < 39) Step(c, c + 62500, ref nextCount);

                int tc = temp[c];
                temp[c] = (short)(tc - tc / 64);
            }

            // Top-up. The only use of the shared PRNG inside the timed loop.
            while (nextCount < TopUpTarget)
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                int idx = (int)(s % (uint)CellCount);
                temp[idx] = (short)(temp[idx] + 1000);
                if (mark[idx] == 0)
                {
                    mark[idx] = 1;
                    next[nextCount] = idx;
                    nextCount++;
                }
            }

            // Clear only the marks we set.
            for (int i = 0; i < nextCount; i++) mark[next[i]] = 0;

            meta[1] = nextCount;
            seed[0] = s;
        }

        private void Step(int c, int n, ref int nextCount)
        {
            if (solid[n] == 1) return;
            int delta = (temp[c] - temp[n]) / 4;
            if (delta == 0) return;
            temp[n] = (short)(temp[n] + delta);
            temp[c] = (short)(temp[c] - delta);
            if (mark[n] == 0 && nextCount < FrontierCap)
            {
                mark[n] = 1;
                next[nextCount] = n;
                nextCount++;
            }
        }
    }
}
