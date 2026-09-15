using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using SW = System.Diagnostics.Stopwatch;

namespace Bench
{
    // D1 architecture benchmark, candidate "plain".
    // Structure-of-arrays; NativeArray for the cell grid (so phase 1 can be a Burst IJob),
    // plain managed arrays for things, pawns, A-star scratch and the view buffers.
    // Everything except the phase-1 job is straight single-threaded managed C#.
    public static unsafe class D1Plain
    {
        public const int SX = 250, SZ = 250, SY = 40;
        public const int CellCount = 2500000;
        public const int LayerStride = 62500;
        public const int ThingCount = 20000;
        public const int PawnCount = 50;
        public const int PortalCount = 200;
        public const int PathCap = 64;
        public const int WarmTicks = 300;
        public const int MeasTicks = 1500;
        public const int AStarBudget = 20000;

        // ---------------------------------------------------------------- sim

        private sealed class Sim : IDisposable
        {
            public uint s;

            public NativeArray<byte> solid;
            public NativeArray<short> temp;
            public NativeArray<byte> mark;
            public NativeArray<int> frontA, frontB;
            public NativeArray<int> meta;
            public NativeArray<uint> seedArr;
            public byte* solidP;
            public short* tempP;
            public long nativeBytes;

            public int[] thingCell = new int[ThingCount];
            public int[] thingState = new int[ThingCount];
            public byte[] thingTicker = new byte[ThingCount];

            public int[] pawnCell = new int[PawnCount];
            public int[] pawnNeed = new int[PawnCount];
            public int[] pawnPath = new int[PawnCount * PathCap];
            public int[] pawnPathLen = new int[PawnCount];
            public int[] pawnPathStep = new int[PawnCount];

            // portal lookup: open-addressed cell -> chain of partner entries, insertion order kept
            public const int PMask = 4095;
            public int[] pKey = new int[PMask + 1];
            public int[] pHead = new int[PMask + 1];
            public int[] portalPartner = new int[PortalCount * 2];
            public int[] portalNext = new int[PortalCount * 2];
            public int portalEntries;

            // A-star scratch, persistent across searches
            public int[] g = new int[CellCount];
            public int[] cameFrom = new int[CellCount];
            public byte[] closed = new byte[CellCount];
            public int[] touched = new int[200000];
            public int touchedCount;
            public long[] heap = new long[200000];
            public int heapCount;
            public int[] tmpPath = new int[32768];
            public int replanOk;      // diagnostics only
            public int replanTried;   // diagnostics only

            // view buffers, double-buffered and pooled
            public byte[] sliceA = new byte[LayerStride], sliceB = new byte[LayerStride];
            public int[] pawnViewA = new int[PawnCount * 3], pawnViewB = new int[PawnCount * 3];
            public int[] thingViewA = new int[500 * 3], thingViewB = new int[500 * 3];
            public bool viewFlip;

            public void Dispose()
            {
                if (solid.IsCreated) solid.Dispose();
                if (temp.IsCreated) temp.Dispose();
                if (mark.IsCreated) mark.Dispose();
                if (frontA.IsCreated) frontA.Dispose();
                if (frontB.IsCreated) frontB.Dispose();
                if (meta.IsCreated) meta.Dispose();
                if (seedArr.IsCreated) seedArr.Dispose();
            }

            public uint Next()
            {
                uint v = s;
                v ^= v << 13; v ^= v >> 17; v ^= v << 5;
                s = v;
                return v;
            }

            // ------------------------------------------------------------ setup

            public void Setup()
            {
                s = 12345u;

                solid = new NativeArray<byte>(CellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                temp = new NativeArray<short>(CellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                mark = new NativeArray<byte>(CellCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                frontA = new NativeArray<int>(GridJob.FrontierCap, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                frontB = new NativeArray<int>(GridJob.FrontierCap, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                meta = new NativeArray<int>(2, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                seedArr = new NativeArray<uint>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                nativeBytes = (long)CellCount * 1 + (long)CellCount * 2 + (long)CellCount * 1
                              + (long)GridJob.FrontierCap * 4 * 2 + 2 * 4 + 1 * 4;

                solidP = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(solid);
                tempP = (short*)NativeArrayUnsafeUtility.GetUnsafePtr(temp);

                // 1. solid
                for (int i = 0; i < CellCount; i++) solidP[i] = (Next() % 100) < 35 ? (byte)1 : (byte)0;

                // 2. heat sources -> initial frontier (duplicates allowed, order preserved)
                int* fa = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(frontA);
                int n0 = 0;
                for (int k = 0; k < 2000; k++)
                {
                    int idx = (int)(Next() % (uint)CellCount);
                    tempP[idx] = 1000;
                    fa[n0] = idx;
                    n0++;
                }
                meta[0] = n0;
                seedArr[0] = s;

                // 3. things
                for (int i = 0; i < ThingCount; i++)
                {
                    thingCell[i] = (int)(Next() % (uint)CellCount);
                    thingTicker[i] = (byte)(Next() % 2u);
                    thingState[i] = 0;
                }

                // 4. portals
                for (int i = 0; i <= PMask; i++) { pKey[i] = -1; pHead[i] = -1; }
                portalEntries = 0;
                int accepted = 0;
                while (accepted < PortalCount)
                {
                    int x = (int)(Next() % 250u);
                    int z = (int)(Next() % 250u);
                    int y = (int)(Next() % 39u);
                    int a = (y * 250 + z) * 250 + x;
                    int b = a + 62500;
                    if (solidP[a] == 0 && solidP[b] == 0)
                    {
                        PortalInsert(a, b);
                        PortalInsert(b, a);
                        accepted++;
                    }
                }

                // 5. pawns
                for (int p = 0; p < PawnCount; p++)
                {
                    int idx;
                    do { idx = (int)(Next() % (uint)CellCount); } while (solidP[idx] != 0);
                    pawnCell[p] = idx;
                    pawnNeed[p] = 50000;
                    pawnPathLen[p] = 0;
                    pawnPathStep[p] = 0;
                }

                // A-star scratch init
                for (int i = 0; i < CellCount; i++) g[i] = int.MaxValue;
                touchedCount = 0;
                heapCount = 0;
                viewFlip = false;
            }

            private void PortalInsert(int cell, int partner)
            {
                int slot = PortalSlot(cell);
                while (pKey[slot] != -1 && pKey[slot] != cell) slot = (slot + 1) & PMask;
                int e = portalEntries++;
                portalPartner[e] = partner;
                portalNext[e] = -1;
                if (pKey[slot] == -1) { pKey[slot] = cell; pHead[slot] = e; }
                else
                {
                    int t = pHead[slot];
                    while (portalNext[t] != -1) t = portalNext[t];
                    portalNext[t] = e;
                }
            }

            private static int PortalSlot(int cell)
            {
                return (int)((((uint)cell) * 2654435761u) >> 12) & PMask;
            }

            public int PortalFind(int cell)
            {
                int slot = PortalSlot(cell);
                while (pKey[slot] != -1)
                {
                    if (pKey[slot] == cell) return pHead[slot];
                    slot = (slot + 1) & PMask;
                }
                return -1;
            }

            // ------------------------------------------------------------ phase 1

            public void Phase1()
            {
                bool flip = (frontFlip);
                var job = new GridJob
                {
                    solid = solid,
                    temp = temp,
                    mark = mark,
                    cur = flip ? frontB : frontA,
                    next = flip ? frontA : frontB,
                    meta = meta,
                    seed = seedArr,
                };
                seedArr[0] = s;
                job.Run();
                meta[0] = meta[1];
                frontFlip = !flip;
                s = seedArr[0];
            }

            public bool frontFlip;

            // ------------------------------------------------------------ phase 2

            public void Phase2(int tick)
            {
                for (int i = 0; i < ThingCount; i++)
                {
                    int interval = thingTicker[i] == 0 ? 250 : 2000;
                    if (((tick + i) % interval) == 0)
                        thingState[i] += (tempP[thingCell[i]] & 0xFF) + 1;
                }
            }

            // ------------------------------------------------------------ phase 3

            public void Phase3(int tick)
            {
                for (int p = 0; p < PawnCount; p++)
                {
                    pawnNeed[p] -= 1;
                    if (pawnNeed[p] < 0) pawnNeed[p] = 100000;
                }

                for (int p = 0; p < PawnCount; p++)
                {
                    if (pawnPathStep[p] < pawnPathLen[p])
                    {
                        pawnCell[p] = pawnPath[p * PathCap + pawnPathStep[p]];
                        pawnPathStep[p]++;
                    }
                }

                int pr = tick % PawnCount;
                int pc = pawnCell[pr];
                int px = pc % 250;
                int rest = pc / 250;
                int pz = rest % 250;
                int py = rest / 250;

                int target = -1;
                for (int k = 0; k < 50; k++)
                {
                    int dx = (int)(Next() % 81u) - 40;
                    int dz = (int)(Next() % 81u) - 40;
                    int dy = (int)(Next() % 7u) - 3;
                    int tx = px + dx; if (tx < 0) tx = 0; else if (tx > 249) tx = 249;
                    int tz = pz + dz; if (tz < 0) tz = 0; else if (tz > 249) tz = 249;
                    int ty = py + dy; if (ty < 0) ty = 0; else if (ty > 39) ty = 39;
                    int t = (ty * 250 + tz) * 250 + tx;
                    if (target < 0 && solidP[t] == 0 && t != pc) target = t;
                }

                if (target < 0) return;
                replanTried++;
                AStar(pr, pc, target);
                if (pawnPathLen[pr] > 0) replanOk++;
            }

            private void HeapPush(long v)
            {
                int i = heapCount;
                heapCount++;
                heap[i] = v;
                while (i > 0)
                {
                    int par = (i - 1) >> 1;
                    if (heap[par] <= heap[i]) break;
                    long t = heap[par]; heap[par] = heap[i]; heap[i] = t;
                    i = par;
                }
            }

            private long HeapPop()
            {
                long top = heap[0];
                heapCount--;
                heap[0] = heap[heapCount];
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1;
                    if (l >= heapCount) break;
                    int c = l;
                    int r = l + 1;
                    // ties between the two children resolve to the left child
                    if (r < heapCount && heap[r] < heap[l]) c = r;
                    if (heap[i] <= heap[c]) break;
                    long t = heap[i]; heap[i] = heap[c]; heap[c] = t;
                    i = c;
                }
                return top;
            }

            private void AStar(int p, int start, int target)
            {
                int tx = target % 250;
                int trest = target / 250;
                int tz = trest % 250;
                int ty = trest / 250;

                heapCount = 0;
                touchedCount = 0;

                g[start] = 0;
                cameFrom[start] = -1;
                touched[touchedCount++] = start;
                {
                    int sx = start % 250;
                    int srest = start / 250;
                    int sz = srest % 250;
                    int sy = srest / 250;
                    int h = 10 * (Abs(sx - tx) + Abs(sz - tz)) + 10 * Abs(sy - ty);
                    HeapPush(((long)h << 32) | (uint)start);
                }

                int pops = 0;
                bool found = false;

                while (heapCount > 0)
                {
                    long key = HeapPop();
                    int c = (int)(key & 0xFFFFFFFFL);
                    if (closed[c] != 0) continue;
                    closed[c] = 1;
                    pops++;
                    if (c == target) { found = true; break; }
                    if (pops >= AStarBudget) break;

                    int ng = g[c] + 10;
                    int x = c % 250;
                    int rest = c / 250;
                    int z = rest % 250;

                    if (x > 0) Relax(c, c - 1, ng, tx, tz, ty);
                    if (x < 249) Relax(c, c + 1, ng, tx, tz, ty);
                    if (z > 0) Relax(c, c - 250, ng, tx, tz, ty);
                    if (z < 249) Relax(c, c + 250, ng, tx, tz, ty);
                    for (int e = PortalFind(c); e != -1; e = portalNext[e])
                        Relax(c, portalPartner[e], ng, tx, tz, ty);
                }

                if (found)
                {
                    int len = 0;
                    int cur = target;
                    while (cur != start)
                    {
                        tmpPath[len++] = cur;
                        cur = cameFrom[cur];
                    }
                    int store = len < PathCap ? len : PathCap;
                    for (int i = 0; i < store; i++) pawnPath[p * PathCap + i] = tmpPath[len - 1 - i];
                    pawnPathLen[p] = store;
                    pawnPathStep[p] = 0;
                }
                else
                {
                    pawnPathLen[p] = 0;
                    pawnPathStep[p] = 0;
                }

                for (int i = 0; i < touchedCount; i++)
                {
                    int c = touched[i];
                    g[c] = int.MaxValue;
                    closed[c] = 0;
                }
                touchedCount = 0;
                heapCount = 0;
            }

            private void Relax(int from, int n, int ng, int tx, int tz, int ty)
            {
                if (solidP[n] != 0) return;
                if (closed[n] != 0) return;
                if (ng >= g[n]) return;
                if (g[n] == int.MaxValue) touched[touchedCount++] = n;
                g[n] = ng;
                cameFrom[n] = from;
                int x = n % 250;
                int rest = n / 250;
                int z = rest % 250;
                int y = rest / 250;
                int h = 10 * (Abs(x - tx) + Abs(z - tz)) + 10 * Abs(y - ty);
                HeapPush(((long)(ng + h) << 32) | (uint)n);
            }

            private static int Abs(int v) { return v < 0 ? -v : v; }

            // ------------------------------------------------------------ phase 4

            public void Phase4(int tick)
            {
                byte[] slice = viewFlip ? sliceA : sliceB;
                int[] pv = viewFlip ? pawnViewA : pawnViewB;
                int[] tv = viewFlip ? thingViewA : thingViewB;

                int ySlice = tick % 40;
                int b0 = ySlice * LayerStride;
                for (int i = 0; i < LayerStride; i++)
                {
                    int idx = b0 + i;
                    slice[i] = solidP[idx] == 1 ? (byte)255 : (byte)(tempP[idx] & 0xFF);
                }
                for (int p = 0; p < PawnCount; p++)
                {
                    pv[p * 3] = p;
                    pv[p * 3 + 1] = pawnCell[p];
                    pv[p * 3 + 2] = pawnNeed[p];
                }
                for (int i = 0; i < 500; i++)
                {
                    tv[i * 3] = i;
                    tv[i * 3 + 1] = thingCell[i];
                    tv[i * 3 + 2] = thingState[i];
                }

                viewFlip = !viewFlip; // single reference swap, no copying
            }

            // ------------------------------------------------------------ hash

            public string Hash()
            {
                ulong h = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                for (int i = 0; i < CellCount; i++)
                {
                    ushort v = (ushort)tempP[i];
                    h ^= (byte)(v & 0xFF); h *= prime;
                    h ^= (byte)((v >> 8) & 0xFF); h *= prime;
                }
                for (int i = 0; i < ThingCount; i++)
                {
                    uint v = (uint)thingState[i];
                    h ^= (byte)(v & 0xFF); h *= prime;
                    h ^= (byte)((v >> 8) & 0xFF); h *= prime;
                    h ^= (byte)((v >> 16) & 0xFF); h *= prime;
                    h ^= (byte)((v >> 24) & 0xFF); h *= prime;
                }
                for (int p = 0; p < PawnCount; p++)
                {
                    uint a = (uint)pawnCell[p];
                    h ^= (byte)(a & 0xFF); h *= prime;
                    h ^= (byte)((a >> 8) & 0xFF); h *= prime;
                    h ^= (byte)((a >> 16) & 0xFF); h *= prime;
                    h ^= (byte)((a >> 24) & 0xFF); h *= prime;
                    uint b = (uint)pawnNeed[p];
                    h ^= (byte)(b & 0xFF); h *= prime;
                    h ^= (byte)((b >> 8) & 0xFF); h *= prime;
                    h ^= (byte)((b >> 16) & 0xFF); h *= prime;
                    h ^= (byte)((b >> 24) & 0xFF); h *= prime;
                }
                return h.ToString("x16", CultureInfo.InvariantCulture);
            }
        }

        // ---------------------------------------------------------------- harness

        private sealed class RunResult
        {
            public string hash;
            public double setupMs;
            public double[] p1 = new double[MeasTicks];
            public double[] p2 = new double[MeasTicks];
            public double[] p3 = new double[MeasTicks];
            public double[] p4 = new double[MeasTicks];
            public double[] tt = new double[MeasTicks];
            public long allocBytes;
            public int gc0, gc1, gc2;
            public long managedBytes;
            public long managedDelta;
            public long nativeBytes;
            public int replanOk;
            public int replanTried;
        }

        private static RunResult RunOnce()
        {
            var r = new RunResult();
            double freq = SW.Frequency;
            long baseline = GC.GetTotalMemory(true);
            var sim = new Sim();

            long t0 = SW.GetTimestamp();
            sim.Setup();
            long t1 = SW.GetTimestamp();
            r.setupMs = (t1 - t0) * 1000.0 / freq;
            r.nativeBytes = sim.nativeBytes;
            r.managedBytes = GC.GetTotalMemory(true);
            r.managedDelta = r.managedBytes - baseline;

            int tick = 0;
            for (; tick < WarmTicks; tick++)
            {
                sim.Phase1();
                sim.Phase2(tick);
                sim.Phase3(tick);
                sim.Phase4(tick);
            }

            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            long alloc0 = GC.GetAllocatedBytesForCurrentThread();

            for (int m = 0; m < MeasTicks; m++, tick++)
            {
                long a = SW.GetTimestamp();
                sim.Phase1();
                long b = SW.GetTimestamp();
                sim.Phase2(tick);
                long c = SW.GetTimestamp();
                sim.Phase3(tick);
                long d = SW.GetTimestamp();
                sim.Phase4(tick);
                long e = SW.GetTimestamp();
                r.p1[m] = (b - a) * 1000.0 / freq;
                r.p2[m] = (c - b) * 1000.0 / freq;
                r.p3[m] = (d - c) * 1000.0 / freq;
                r.p4[m] = (e - d) * 1000.0 / freq;
                r.tt[m] = (e - a) * 1000.0 / freq;
            }

            r.allocBytes = GC.GetAllocatedBytesForCurrentThread() - alloc0;
            r.gc0 = GC.CollectionCount(0) - g0;
            r.gc1 = GC.CollectionCount(1) - g1;
            r.gc2 = GC.CollectionCount(2) - g2;

            r.hash = sim.Hash();
            r.replanOk = sim.replanOk;
            r.replanTried = sim.replanTried;
            sim.Dispose();
            return r;
        }

        private static void Stats(double[] v, out double mean, out double p95, out double max)
        {
            int n = v.Length;
            double sum = 0, mx = 0;
            for (int i = 0; i < n; i++) { sum += v[i]; if (v[i] > mx) mx = v[i]; }
            mean = sum / n;
            max = mx;
            var copy = new double[n];
            Array.Copy(v, copy, n);
            Array.Sort(copy);
            int idx = (int)Math.Ceiling(0.95 * n) - 1; // nearest-rank p95
            if (idx < 0) idx = 0;
            if (idx >= n) idx = n - 1;
            p95 = copy[idx];
        }

        private static string F(double v) { return v.ToString("F3", CultureInfo.InvariantCulture); }

        private static string PhaseLine(string name, double[] v)
        {
            double mean, p95, max;
            Stats(v, out mean, out p95, out max);
            return "D1RESULT " + name + "_ms_mean=" + F(mean) + " " + name + "_ms_p95=" + F(p95) + " " + name + "_ms_max=" + F(max);
        }

        public static void Run()
        {
            var sb = new StringBuilder();
            try
            {
                var packages = new List<string>();
                foreach (var pi in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
                {
                    if (pi.name == "com.unity.burst" || pi.name == "com.unity.collections" || pi.name == "com.unity.mathematics")
                        packages.Add(pi.name + "@" + pi.version);
                }
                packages.Sort(StringComparer.Ordinal);

                bool burstEnabled = Unity.Burst.BurstCompiler.IsEnabled;
                // Compile the phase-1 job up front, so the untimed warm-up really is warm
                // and run 1 is not measuring Burst's background compile.
                Unity.Burst.BurstCompiler.Options.EnableBurstCompileSynchronously = true;

                var r1 = RunOnce();
                var r2 = RunOnce();

                sb.AppendLine("D1RESULT candidate=plain");
                sb.AppendLine("D1RESULT unity=" + Application.unityVersion + " packages=" + string.Join(",", packages.ToArray()));
                sb.AppendLine("D1RESULT hash_run1=" + r1.hash + " hash_run2=" + r2.hash);
                sb.AppendLine(PhaseLine("phase1", r1.p1));
                sb.AppendLine(PhaseLine("phase2", r1.p2));
                sb.AppendLine(PhaseLine("phase3", r1.p3));
                sb.AppendLine(PhaseLine("phase4", r1.p4));
                sb.AppendLine(PhaseLine("tick", r1.tt));
                sb.AppendLine("D1RESULT alloc_bytes_per_tick=" + F(r1.allocBytes / (double)MeasTicks)
                              + " gc0=" + r1.gc0 + " gc1=" + r1.gc1 + " gc2=" + r1.gc2);
                sb.AppendLine("D1RESULT managed_bytes=" + r1.managedBytes + " native_bytes=" + r1.nativeBytes);
                sb.AppendLine("D1RESULT setup_ms=" + F(r1.setupMs));

                sb.AppendLine("# burst_enabled=" + burstEnabled + " burst_synchronous=True");
                sb.AppendLine("# managed_bytes is GC.GetTotalMemory(true) for the whole editor process;");
                sb.AppendLine("# managed_delta_bytes is the rise attributable to the simulation itself.");
                sb.AppendLine("# managed_delta_bytes=" + r1.managedDelta);
                sb.AppendLine("# replans run1 tried=" + r1.replanTried + " succeeded=" + r1.replanOk
                              + " | run2 tried=" + r2.replanTried + " succeeded=" + r2.replanOk);
                sb.AppendLine("# run2 (second in-process run, for reference only)");
                sb.AppendLine("# " + PhaseLine("phase1", r2.p1));
                sb.AppendLine("# " + PhaseLine("phase2", r2.p2));
                sb.AppendLine("# " + PhaseLine("phase3", r2.p3));
                sb.AppendLine("# " + PhaseLine("phase4", r2.p4));
                sb.AppendLine("# " + PhaseLine("tick", r2.tt));
                sb.AppendLine("# run2 alloc_bytes_per_tick=" + F(r2.allocBytes / (double)MeasTicks)
                              + " gc0=" + r2.gc0 + " gc1=" + r2.gc1 + " gc2=" + r2.gc2
                              + " setup_ms=" + F(r2.setupMs));
            }
            catch (Exception ex)
            {
                sb.AppendLine("D1RESULT FAILED " + ex.GetType().Name + ": " + ex.Message);
                sb.AppendLine(ex.StackTrace);
            }

            string text = sb.ToString();
            Console.WriteLine(text);
            UnityEngine.Debug.Log(text);
            try
            {
                File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), "d1result.txt"), text);
            }
            catch (Exception) { }
        }
    }
}
