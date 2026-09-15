using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Bench
{
    /// <summary>
    /// D1 architecture benchmark, candidate "ecs".
    /// Driven headless from an editor static method: it builds its own World,
    /// creates the four ISystems and ticks them by hand. No scene, no GameObject,
    /// no bootstrap.
    /// </summary>
    public static class D1Ecs
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            var diag = new StringBuilder();
            int exitCode = 0;
            try
            {
                // Keep Burst out of the measured window: compile on first touch.
                try { Unity.Burst.BurstCompiler.Options.EnableBurstCompileSynchronously = true; } catch { }

                // Discarded prewarm so synchronous Burst compilation is not charged
                // to the reported setup_ms or to the first measured window.
                RunOnce(9, false, null, 1);

                var r1 = RunOnce(0, true, diag, 1);
                var r2 = RunOnce(1, false, null, 1);

                // The literal reading of "stop after 20,000 pops" - counting lazily
                // deleted duplicate pops against the budget too. Kept as a diagnostic:
                // it is the only point where the two candidates first disagreed.
                var rAlt = RunOnce(3, false, null, 0);
                diag.AppendLine($"D1DIAG budgetmode0_hash={rAlt.Hash} replan_ok={rAlt.ReplanOk} hash_temp={rAlt.HashTemp} hash_things={rAlt.HashThings} hash_pawns={rAlt.HashPawns}");

                // Burst off: same code path, managed IL, so phase 3 can be compared
                // against a hand-written managed A* on equal footing.
                Result r3 = default;
                bool noBurstOk = false;
                try
                {
                    Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = false;
                    r3 = RunOnce(2, false, null, 1);
                    noBurstOk = true;
                }
                catch (Exception e) { diag.AppendLine("D1DIAG noburst_failed=" + e.Message); }
                finally
                {
                    try { Unity.Burst.BurstCompiler.Options.EnableBurstCompilation = true; } catch { }
                }

                sb.AppendLine("D1RESULT candidate=ecs");
                sb.AppendLine($"D1RESULT unity={UnityEngine.Application.unityVersion} packages={PackageList()}");
                sb.AppendLine($"D1RESULT hash_run1={r1.Hash} hash_run2={r2.Hash}");
                sb.AppendLine(Line("phase1", r1.P1));
                sb.AppendLine(Line("phase2", r1.P2));
                sb.AppendLine(Line("phase3", r1.P3));
                sb.AppendLine(Line("phase4", r1.P4));
                sb.AppendLine(Line("tick", r1.Tick));
                sb.AppendLine($"D1RESULT alloc_bytes_per_tick={r1.AllocPerTick.ToString("F1", CultureInfo.InvariantCulture)} gc0={r1.Gc0} gc1={r1.Gc1} gc2={r1.Gc2}");
                sb.AppendLine($"D1RESULT managed_bytes={r1.ManagedBytes} native_bytes={r1.NativeBytes}");
                sb.AppendLine($"D1RESULT setup_ms={F(r1.SetupMs)}");

                if (noBurstOk)
                {
                    sb.AppendLine($"D1NOBURST hash={r3.Hash} setup_ms={F(r3.SetupMs)}");
                    sb.AppendLine(NbLine("phase1", r3.P1));
                    sb.AppendLine(NbLine("phase2", r3.P2));
                    sb.AppendLine(NbLine("phase3", r3.P3));
                    sb.AppendLine(NbLine("phase4", r3.P4));
                    sb.AppendLine(NbLine("tick", r3.Tick));
                }
                sb.Append(diag);
            }
            catch (Exception e)
            {
                exitCode = 1;
                sb.AppendLine("D1RESULT FAILED");
                sb.AppendLine(e.ToString());
            }

            string text = sb.ToString();
            Debug.Log("\n===D1BEGIN===\n" + text + "===D1END===");
            try
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "d1result.txt"), text);
            }
            catch { }

            Console.Out.Write("\n===D1BEGIN===\n" + text + "===D1END===\n");
            Console.Out.Flush();
            if (exitCode != 0) EditorApplication.Exit(exitCode);
        }

        static string Line(string phase, Stats s) =>
            $"D1RESULT {phase}_ms_mean={F(s.Mean)} {phase}_ms_p95={F(s.P95)} {phase}_ms_max={F(s.Max)}";

        static string NbLine(string phase, Stats s) =>
            $"D1NOBURST {phase}_ms_mean={F(s.Mean)} {phase}_ms_p95={F(s.P95)} {phase}_ms_max={F(s.Max)}";

        static string F(double v) => v.ToString("F3", CultureInfo.InvariantCulture);

        static string PackageList()
        {
            string[] names = { "com.unity.entities", "com.unity.burst", "com.unity.collections", "com.unity.mathematics" };
            var parts = new System.Collections.Generic.List<string>();
            foreach (var n in names)
            {
                try
                {
                    var pi = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + n + "/package.json");
                    parts.Add(pi != null ? $"{pi.name}@{pi.version}" : $"{n}@unknown");
                }
                catch { parts.Add($"{n}@unknown"); }
            }
            string burst;
            try { burst = Unity.Burst.BurstCompiler.Options.EnableBurstCompilation ? "on" : "off"; }
            catch { burst = "unknown"; }
            parts.Add("burst_enabled@" + burst);
            return string.Join(",", parts);
        }

        struct Stats { public double Mean, P95, Max; }

        struct Result
        {
            public string Hash;
            public Stats P1, P2, P3, P4, Tick;
            public double AllocPerTick;
            public int Gc0, Gc1, Gc2;
            public long ManagedBytes, NativeBytes;
            public double SetupMs;
            public int Replans, ReplanOk;
            public string HashTemp, HashThings, HashPawns;
        }

        // =============================================================
        static Result RunOnce(int runIndex, bool collectDiag, StringBuilder diag, int budgetMode)
        {
            var res = new Result();

            double freqMs = 1000.0 / Stopwatch.Frequency;
            long setupStart = Stopwatch.GetTimestamp();

            var world = new World("D1Bench" + runIndex);
            var em = world.EntityManager;

            SimData d = BuildSim(em);
            d.BudgetMode = budgetMode;

            Entity sim = em.CreateEntity(typeof(SimData));
            em.SetComponentData(sim, d);

            var h1 = world.CreateSystem<Phase1System>();
            var h2 = world.CreateSystem<Phase2System>();
            var h3 = world.CreateSystem<Phase3System>();
            var h4 = world.CreateSystem<Phase4System>();

            long setupEnd = Stopwatch.GetTimestamp();
            res.SetupMs = (setupEnd - setupStart) * freqMs;
            res.NativeBytes = d.NativeBytes;
            res.ManagedBytes = GC.GetTotalMemory(true);

            if (collectDiag)
            {
                long ones = 0;
                for (int i = 0; i < W.CellCount; i++) ones += d.Solid[i];
                diag.AppendLine($"D1DIAG solid_ones={ones} rng_after_setup={d.Rng} hash_setup={Hash(em, d, 7)}");
                diag.AppendLine($"D1DIAG setup_hash_temp={Hash(em, d, 1)} setup_hash_things={Hash(em, d, 2)} setup_hash_pawns={Hash(em, d, 4)}");
                diag.AppendLine($"D1DIAG setup_frontier_len={d.FrontierCur.Length} setup_pawn0={em.GetComponentData<PawnCell>(d.Pawns[0]).Value} setup_thing0={em.GetComponentData<ThingCell>(d.Things[0]).Value}");
            }

            int n = W.MeasuredTicks;
            var t1 = new double[n];
            var t2 = new double[n];
            var t3 = new double[n];
            var t4 = new double[n];
            var tt = new double[n];

            // ---- warm-up, fully executed, untimed ----
            for (int i = 0; i < W.WarmupTicks; i++)
            {
                Tick(world, h1, h2, h3, h4, em, sim);
                if (collectDiag && (i == 0 || i == 1 || i == 9 || i == 99))
                {
                    var cur = em.GetComponentData<SimData>(sim);
                    diag.AppendLine($"D1DIAG after_tick{i + 1} hash={Hash(em, cur, 7)} rng={cur.Rng} frontier={cur.FrontierCur.Length} replans={cur.Replans} ok={cur.ReplanOk}");
                }
            }
            if (collectDiag)
            {
                var cur = em.GetComponentData<SimData>(sim);
                diag.AppendLine($"D1DIAG after_warmup hash={Hash(em, cur, 7)} rng={cur.Rng} replans={cur.Replans} ok={cur.ReplanOk}");
            }

            // ---- measured window ----
            int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
            long alloc0 = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < n; i++)
            {
                long a = Stopwatch.GetTimestamp();
                h1.Update(world.Unmanaged);
                long b = Stopwatch.GetTimestamp();
                h2.Update(world.Unmanaged);
                long c = Stopwatch.GetTimestamp();
                h3.Update(world.Unmanaged);
                long e = Stopwatch.GetTimestamp();
                h4.Update(world.Unmanaged);
                long f = Stopwatch.GetTimestamp();

                t1[i] = (b - a) * freqMs;
                t2[i] = (c - b) * freqMs;
                t3[i] = (e - c) * freqMs;
                t4[i] = (f - e) * freqMs;
                tt[i] = (f - a) * freqMs;

                var s = em.GetComponentData<SimData>(sim);
                s.Tick += 1;
                em.SetComponentData(sim, s);
            }

            long alloc1 = GC.GetAllocatedBytesForCurrentThread();
            res.Gc0 = GC.CollectionCount(0) - gc0;
            res.Gc1 = GC.CollectionCount(1) - gc1;
            res.Gc2 = GC.CollectionCount(2) - gc2;
            res.AllocPerTick = (alloc1 - alloc0) / (double)n;

            res.P1 = Summarise(t1);
            res.P2 = Summarise(t2);
            res.P3 = Summarise(t3);
            res.P4 = Summarise(t4);
            res.Tick = Summarise(tt);

            var final = em.GetComponentData<SimData>(sim);
            res.Hash = Hash(em, final, 7);
            res.HashTemp = Hash(em, final, 1);
            res.HashThings = Hash(em, final, 2);
            res.HashPawns = Hash(em, final, 4);
            res.Replans = final.Replans;
            res.ReplanOk = final.ReplanOk;

            if (collectDiag)
            {
                long tsum = 0;
                for (int i = 0; i < W.CellCount; i++) tsum += final.Temp[i];
                var cells = new StringBuilder();
                for (int p = 0; p < 10; p++)
                {
                    if (p > 0) cells.Append('|');
                    cells.Append(em.GetComponentData<PawnCell>(final.Pawns[p]).Value);
                }
                diag.AppendLine($"D1DIAG final_hash_temp={Hash(em, final, 1)} final_hash_things={Hash(em, final, 2)} final_hash_pawns={Hash(em, final, 4)}");
                diag.AppendLine($"D1DIAG final_rng={final.Rng} final_tick={final.Tick} replans={final.Replans} replan_ok={final.ReplanOk} temp_sum={tsum}");
                diag.AppendLine($"D1DIAG final_pawncells={cells}");
            }

            Dispose(ref d);
            world.Dispose();

            return res;
        }

        static void Tick(World world, SystemHandle h1, SystemHandle h2, SystemHandle h3, SystemHandle h4,
                         EntityManager em, Entity sim)
        {
            h1.Update(world.Unmanaged);
            h2.Update(world.Unmanaged);
            h3.Update(world.Unmanaged);
            h4.Update(world.Unmanaged);
            var s = em.GetComponentData<SimData>(sim);
            s.Tick += 1;
            em.SetComponentData(sim, s);
        }

        static Stats Summarise(double[] v)
        {
            int n = v.Length;
            double sum = 0, max = 0;
            for (int i = 0; i < n; i++) { sum += v[i]; if (v[i] > max) max = v[i]; }
            var copy = (double[])v.Clone();
            Array.Sort(copy);
            // nearest rank: ceil(0.95 * n) - 1
            int idx = (int)Math.Ceiling(0.95 * n) - 1;
            if (idx < 0) idx = 0;
            if (idx >= n) idx = n - 1;
            return new Stats { Mean = sum / n, P95 = copy[idx], Max = max };
        }

        // =============================================================
        // Setup - contract sections 1, 3 and 4, in that exact PRNG order.
        // =============================================================
        static SimData BuildSim(EntityManager em)
        {
            var d = new SimData();
            long nb = 0;

            d.Solid = Alloc<byte>(W.CellCount, ref nb);
            d.Temp = Alloc<short>(W.CellCount, ref nb);
            d.Mark = Alloc<byte>(W.CellCount, ref nb);
            d.G = Alloc<int>(W.CellCount, ref nb);
            d.CameFrom = Alloc<int>(W.CellCount, ref nb);
            d.Closed = Alloc<byte>(W.CellCount, ref nb);

            d.FrontierCur = AllocList<int>(W.FrontierCap + 16, ref nb);
            d.FrontierNext = AllocList<int>(W.FrontierCap + 16, ref nb);
            d.Touched = AllocList<int>(1 << 18, ref nb);
            d.Heap = AllocList<HeapItem>(1 << 18, ref nb);
            d.PathScratch = AllocList<int>(1 << 14, ref nb);

            d.PortalHead = new NativeHashMap<int, int>(1024, Allocator.Persistent);
            d.PortalTail = new NativeHashMap<int, int>(1024, Allocator.Persistent);
            nb += 1024 * 8 * 2;
            d.PortalPartner = AllocList<int>(512, ref nb);
            d.PortalNext = AllocList<int>(512, ref nb);

            d.SliceA = Alloc<byte>(W.LayerCells, ref nb);
            d.SliceB = Alloc<byte>(W.LayerCells, ref nb);
            d.ViewA = Alloc<int>((W.PawnCount + 500) * 3, ref nb);
            d.ViewB = Alloc<int>((W.PawnCount + 500) * 3, ref nb);
            d.FrontIsA = 1;

            for (int i = 0; i < W.CellCount; i++) { d.G[i] = int.MaxValue; d.CameFrom[i] = -1; }

            uint s = 12345u;

            // 1. solid
            var solid = d.Solid;
            for (int i = 0; i < W.CellCount; i++)
                solid[i] = (byte)((Rng.Next(ref s) % 100u) < 35u ? 1 : 0);

            // 2. heat sources, appended to the initial frontier in draw order
            var temp = d.Temp;
            for (int k = 0; k < 2000; k++)
            {
                int idx = (int)(Rng.Next(ref s) % (uint)W.CellCount);
                temp[idx] = 1000;
                d.FrontierCur.Add(idx);
            }

            // 3. things
            var thingArch = em.CreateArchetype(typeof(ThingIndex), typeof(ThingCell),
                                               typeof(ThingState), typeof(ThingTicker));
            d.Things = new NativeArray<Entity>(W.ThingCount, Allocator.Persistent);
            nb += W.ThingCount * 8;
            em.CreateEntity(thingArch, d.Things);
            for (int i = 0; i < W.ThingCount; i++)
            {
                int cell = (int)(Rng.Next(ref s) % (uint)W.CellCount);
                byte ticker = (byte)(Rng.Next(ref s) % 2u);
                Entity e = d.Things[i];
                em.SetComponentData(e, new ThingIndex { Value = i });
                em.SetComponentData(e, new ThingCell { Value = cell });
                em.SetComponentData(e, new ThingTicker { Value = ticker });
                em.SetComponentData(e, new ThingState { Value = 0 });
            }

            // 4. portals
            int accepted = 0;
            while (accepted < W.PortalCount)
            {
                int x = (int)(Rng.Next(ref s) % (uint)W.X);
                int z = (int)(Rng.Next(ref s) % (uint)W.Z);
                int y = (int)(Rng.Next(ref s) % (uint)(W.Y - 1));
                int a = (y * W.Z + z) * W.X + x;
                int b = a + W.LayerCells;
                if (solid[a] == 0 && solid[b] == 0)
                {
                    AddPortal(ref d, a, b);
                    AddPortal(ref d, b, a);
                    accepted++;
                }
            }

            // 5. pawns
            var pawnArch = em.CreateArchetype(typeof(PawnIndex), typeof(PawnCell), typeof(PawnNeed),
                                              typeof(PawnPathCursor), typeof(PawnPathElem));
            d.Pawns = new NativeArray<Entity>(W.PawnCount, Allocator.Persistent);
            nb += W.PawnCount * 8;
            em.CreateEntity(pawnArch, d.Pawns);
            for (int p = 0; p < W.PawnCount; p++)
            {
                int idx;
                do { idx = (int)(Rng.Next(ref s) % (uint)W.CellCount); } while (solid[idx] != 0);
                Entity e = d.Pawns[p];
                em.SetComponentData(e, new PawnIndex { Value = p });
                em.SetComponentData(e, new PawnCell { Value = idx });
                em.SetComponentData(e, new PawnNeed { Value = 50000 });
                em.SetComponentData(e, new PawnPathCursor { Step = 0 });
                em.GetBuffer<PawnPathElem>(e).Clear();
            }

            d.Rng = s;
            d.Tick = 0;
            d.Replans = 0;
            d.ReplanOk = 0;
            d.NativeBytes = nb;
            return d;
        }

        static void AddPortal(ref SimData d, int from, int to)
        {
            int node = d.PortalPartner.Length;
            d.PortalPartner.Add(to);
            d.PortalNext.Add(-1);
            if (d.PortalTail.TryGetValue(from, out int tail))
            {
                d.PortalNext[tail] = node;
                d.PortalTail[from] = node;
            }
            else
            {
                d.PortalHead.Add(from, node);
                d.PortalTail.Add(from, node);
            }
        }

        static NativeArray<T> Alloc<T>(int len, ref long bytes) where T : unmanaged
        {
            bytes += (long)len * Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<T>();
            return new NativeArray<T>(len, Allocator.Persistent);
        }

        static NativeList<T> AllocList<T>(int cap, ref long bytes) where T : unmanaged
        {
            bytes += (long)cap * Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf<T>();
            return new NativeList<T>(cap, Allocator.Persistent);
        }

        static void Dispose(ref SimData d)
        {
            d.Solid.Dispose(); d.Temp.Dispose(); d.Mark.Dispose();
            d.G.Dispose(); d.CameFrom.Dispose(); d.Closed.Dispose();
            d.FrontierCur.Dispose(); d.FrontierNext.Dispose();
            d.Touched.Dispose(); d.Heap.Dispose(); d.PathScratch.Dispose();
            d.PortalHead.Dispose(); d.PortalTail.Dispose();
            d.PortalPartner.Dispose(); d.PortalNext.Dispose();
            d.SliceA.Dispose(); d.SliceB.Dispose(); d.ViewA.Dispose(); d.ViewB.Dispose();
            d.Things.Dispose(); d.Pawns.Dispose();
        }

        // =============================================================
        // State hash - FNV-1a 64, contract section 0.
        // sections is a bit mask: 1 = temp, 2 = things, 4 = pawns.
        // The contract hash is sections = 7.
        // =============================================================
        static string Hash(EntityManager em, SimData d, int sections)
        {
            ulong h = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            if ((sections & 1) != 0)
            {
                var temp = d.Temp;
                for (int i = 0; i < W.CellCount; i++)
                {
                    ushort v = (ushort)temp[i];
                    h ^= (byte)(v & 0xFF); h *= prime;
                    h ^= (byte)((v >> 8) & 0xFF); h *= prime;
                }
            }

            if ((sections & 2) != 0)
            {
                for (int i = 0; i < W.ThingCount; i++)
                {
                    uint v = (uint)em.GetComponentData<ThingState>(d.Things[i]).Value;
                    h ^= (byte)(v & 0xFF); h *= prime;
                    h ^= (byte)((v >> 8) & 0xFF); h *= prime;
                    h ^= (byte)((v >> 16) & 0xFF); h *= prime;
                    h ^= (byte)((v >> 24) & 0xFF); h *= prime;
                }
            }

            if ((sections & 4) != 0)
            {
                for (int p = 0; p < W.PawnCount; p++)
                {
                    Entity e = d.Pawns[p];
                    uint c = (uint)em.GetComponentData<PawnCell>(e).Value;
                    uint nd = (uint)em.GetComponentData<PawnNeed>(e).Value;
                    h ^= (byte)(c & 0xFF); h *= prime;
                    h ^= (byte)((c >> 8) & 0xFF); h *= prime;
                    h ^= (byte)((c >> 16) & 0xFF); h *= prime;
                    h ^= (byte)((c >> 24) & 0xFF); h *= prime;
                    h ^= (byte)(nd & 0xFF); h *= prime;
                    h ^= (byte)((nd >> 8) & 0xFF); h *= prime;
                    h ^= (byte)((nd >> 16) & 0xFF); h *= prime;
                    h ^= (byte)((nd >> 24) & 0xFF); h *= prime;
                }
            }

            return h.ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
