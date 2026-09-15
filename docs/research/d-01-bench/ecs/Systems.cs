using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Bench
{
    public static class Rng
    {
        // xorshift32, one shared stream (contract section 0)
        public static uint Next(ref uint s)
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return s;
        }
    }

    // =====================================================================
    // Phase 1 - grid propagation
    // =====================================================================
    [BurstCompile]
    public partial struct Phase1System : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var d = ref SystemAPI.GetSingletonRW<SimData>().ValueRW;

            var solid = d.Solid;
            var temp = d.Temp;
            var mark = d.Mark;
            var cur = d.FrontierCur;
            var next = d.FrontierNext;

            next.Clear();

            int count = cur.Length;
            for (int fi = 0; fi < count; fi++)
            {
                int c = cur[fi];
                int x = c % W.X;
                int rem = c / W.X;
                int z = rem % W.Z;
                int y = rem / W.Z;

                // fixed neighbour order: -x, +x, -z, +z, -y, +y
                if (x > 0) Diffuse(temp, solid, mark, next, c, c - 1);
                if (x < W.X - 1) Diffuse(temp, solid, mark, next, c, c + 1);
                if (z > 0) Diffuse(temp, solid, mark, next, c, c - W.X);
                if (z < W.Z - 1) Diffuse(temp, solid, mark, next, c, c + W.X);
                if (y > 0) Diffuse(temp, solid, mark, next, c, c - W.LayerCells);
                if (y < W.Y - 1) Diffuse(temp, solid, mark, next, c, c + W.LayerCells);

                // decay
                temp[c] = (short)(temp[c] - temp[c] / 64);
            }

            // top-up, the only PRNG use inside the timed loop
            uint s = d.Rng;
            while (next.Length < W.FrontierTopUp)
            {
                int idx = (int)(Rng.Next(ref s) % (uint)W.CellCount);
                temp[idx] = (short)(temp[idx] + 1000);
                if (mark[idx] == 0)
                {
                    mark[idx] = 1;
                    next.Add(idx);
                }
            }
            d.Rng = s;

            // clear only the marks we set
            int nlen = next.Length;
            for (int i = 0; i < nlen; i++) mark[next[i]] = 0;

            // swap frontiers
            d.FrontierCur = next;
            d.FrontierNext = cur;
        }

        static void Diffuse(NativeArray<short> temp, NativeArray<byte> solid, NativeArray<byte> mark,
                            NativeList<int> next, int c, int n)
        {
            if (solid[n] == 1) return;
            int delta = (temp[c] - temp[n]) / 4;
            if (delta == 0) return;
            temp[n] = (short)(temp[n] + delta);
            temp[c] = (short)(temp[c] - delta);
            if (mark[n] == 0 && next.Length < W.FrontierCap)
            {
                mark[n] = 1;
                next.Add(n);
            }
        }
    }

    // =====================================================================
    // Phase 2 - things
    // =====================================================================
    [BurstCompile]
    public partial struct Phase2System : ISystem
    {
        ComponentLookup<ThingCell> _cell;
        ComponentLookup<ThingState> _st;
        ComponentLookup<ThingTicker> _tk;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimData>();
            _cell = state.GetComponentLookup<ThingCell>(true);
            _st = state.GetComponentLookup<ThingState>(false);
            _tk = state.GetComponentLookup<ThingTicker>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _cell.Update(ref state);
            _st.Update(ref state);
            _tk.Update(ref state);

            ref var d = ref SystemAPI.GetSingletonRW<SimData>().ValueRW;
            var temp = d.Temp;
            var things = d.Things;
            int tick = d.Tick;

            // explicit index order 0 .. N-1, never chunk order
            for (int i = 0; i < W.ThingCount; i++)
            {
                Entity e = things[i];
                int interval = _tk[e].Value == 0 ? 250 : 2000;
                if (((tick + i) % interval) == 0)
                {
                    int cell = _cell[e].Value;
                    var s = _st[e];
                    s.Value += (temp[cell] & 0xFF) + 1;
                    _st[e] = s;
                }
            }
        }
    }

    // =====================================================================
    // Phase 3 - pawns
    // =====================================================================
    [BurstCompile]
    public partial struct Phase3System : ISystem
    {
        ComponentLookup<PawnCell> _cell;
        ComponentLookup<PawnNeed> _need;
        ComponentLookup<PawnPathCursor> _cursor;
        BufferLookup<PawnPathElem> _path;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimData>();
            _cell = state.GetComponentLookup<PawnCell>(false);
            _need = state.GetComponentLookup<PawnNeed>(false);
            _cursor = state.GetComponentLookup<PawnPathCursor>(false);
            _path = state.GetBufferLookup<PawnPathElem>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _cell.Update(ref state);
            _need.Update(ref state);
            _cursor.Update(ref state);
            _path.Update(ref state);

            ref var d = ref SystemAPI.GetSingletonRW<SimData>().ValueRW;
            var pawns = d.Pawns;
            int tick = d.Tick;

            // 1. needs
            for (int p = 0; p < W.PawnCount; p++)
            {
                Entity e = pawns[p];
                var n = _need[e];
                n.Value -= 1;
                if (n.Value < 0) n.Value = 100000;
                _need[e] = n;
            }

            // 2. movement
            for (int p = 0; p < W.PawnCount; p++)
            {
                Entity e = pawns[p];
                var cur = _cursor[e];
                var buf = _path[e];
                if (cur.Step < buf.Length)
                {
                    var c = _cell[e];
                    c.Value = buf[cur.Step].Value;
                    _cell[e] = c;
                    cur.Step += 1;
                    _cursor[e] = cur;
                }
            }

            // 3. replan, one pawn per tick
            {
                int p = tick % W.PawnCount;
                Entity e = pawns[p];
                int pc = _cell[e].Value;
                int px = pc % W.X;
                int prem = pc / W.X;
                int pz = prem % W.Z;
                int py = prem / W.Z;

                uint s = d.Rng;
                int target = -1;
                int tx = 0, tz = 0, ty = 0;
                for (int k = 0; k < 50; k++)
                {
                    int dx = (int)(Rng.Next(ref s) % 81u) - 40;
                    int dz = (int)(Rng.Next(ref s) % 81u) - 40;
                    int dy = (int)(Rng.Next(ref s) % 7u) - 3;
                    int cx = math.clamp(px + dx, 0, W.X - 1);
                    int cz = math.clamp(pz + dz, 0, W.Z - 1);
                    int cy = math.clamp(py + dy, 0, W.Y - 1);
                    int t = (cy * W.Z + cz) * W.X + cx;
                    if (target < 0 && d.Solid[t] == 0 && t != pc)
                    {
                        target = t;
                        tx = cx; tz = cz; ty = cy;
                    }
                }
                d.Rng = s;

                if (target >= 0)
                {
                    d.Replans += 1;
                    bool ok = AStar(ref d, pc, target, tx, tz, ty);
                    if (ok) d.ReplanOk += 1;
                    var buf = _path[e];
                    buf.Clear();
                    if (ok)
                    {
                        // PathScratch holds the route from target back to the first step
                        // after the start; reverse it and keep the first 64 steps.
                        int n = d.PathScratch.Length;
                        int take = n < W.PathCap ? n : W.PathCap;
                        for (int i = 0; i < take; i++)
                            buf.Add(new PawnPathElem { Value = d.PathScratch[n - 1 - i] });
                        var cur = _cursor[e];
                        cur.Step = 0;
                        _cursor[e] = cur;
                    }
                }
            }
        }

        // -----------------------------------------------------------------
        // A* over the layered grid plus portals. Persistent per-cell arrays,
        // reset via the touched list only.
        // -----------------------------------------------------------------
        static bool AStar(ref SimData d, int start, int target, int tx, int tz, int ty)
        {
            var g = d.G;
            var cameFrom = d.CameFrom;
            var closed = d.Closed;
            var touched = d.Touched;
            var heap = d.Heap;
            var solid = d.Solid;

            touched.Clear();
            heap.Clear();
            d.PathScratch.Clear();

            g[start] = 0;
            cameFrom[start] = -1;
            touched.Add(start);
            HeapPush(heap, Heuristic(start, tx, tz, ty), start);

            bool found = false;
            int pops = 0;
            int budgetMode = d.BudgetMode;
            while (heap.Length > 0)
            {
                if (pops >= W.AStarPopBudget) break;
                HeapItem it = HeapPop(heap);
                if (budgetMode == 0) pops++;
                int c = it.Cell;
                if (closed[c] == 1) continue;
                closed[c] = 1;
                if (budgetMode != 0) pops++;
                if (c == target) { found = true; break; }

                int x = c % W.X;
                int rem = c / W.X;
                int z = rem % W.Z;

                int gc = g[c];
                if (x > 0) Relax(g, cameFrom, closed, touched, heap, solid, c, c - 1, gc, tx, tz, ty);
                if (x < W.X - 1) Relax(g, cameFrom, closed, touched, heap, solid, c, c + 1, gc, tx, tz, ty);
                if (z > 0) Relax(g, cameFrom, closed, touched, heap, solid, c, c - W.X, gc, tx, tz, ty);
                if (z < W.Z - 1) Relax(g, cameFrom, closed, touched, heap, solid, c, c + W.X, gc, tx, tz, ty);

                if (d.PortalHead.TryGetValue(c, out int node))
                {
                    while (node >= 0)
                    {
                        Relax(g, cameFrom, closed, touched, heap, solid, c, d.PortalPartner[node], gc, tx, tz, ty);
                        node = d.PortalNext[node];
                    }
                }
            }

            if (found)
            {
                int cur = target;
                while (cur != start)
                {
                    d.PathScratch.Add(cur);
                    cur = cameFrom[cur];
                }
            }

            // reset only what we touched
            int tl = touched.Length;
            for (int i = 0; i < tl; i++)
            {
                int c = touched[i];
                g[c] = int.MaxValue;
                cameFrom[c] = -1;
                closed[c] = 0;
            }

            return found;
        }

        static void Relax(NativeArray<int> g, NativeArray<int> cameFrom, NativeArray<byte> closed,
                          NativeList<int> touched, NativeList<HeapItem> heap, NativeArray<byte> solid,
                          int c, int n, int gc, int tx, int tz, int ty)
        {
            if (solid[n] == 1) return;
            if (closed[n] == 1) return;
            int tentative = gc + 10;
            int old = g[n];
            if (tentative < old)
            {
                if (old == int.MaxValue) touched.Add(n);
                g[n] = tentative;
                cameFrom[n] = c;
                HeapPush(heap, tentative + Heuristic(n, tx, tz, ty), n);
            }
        }

        static int Heuristic(int c, int tx, int tz, int ty)
        {
            int x = c % W.X;
            int rem = c / W.X;
            int z = rem % W.Z;
            int y = rem / W.Z;
            return 10 * (math.abs(x - tx) + math.abs(z - tz)) + 10 * math.abs(y - ty);
        }

        static bool Less(in HeapItem a, in HeapItem b)
        {
            if (a.F != b.F) return a.F < b.F;
            return a.Cell < b.Cell;
        }

        static void HeapPush(NativeList<HeapItem> heap, int f, int cell)
        {
            heap.Add(new HeapItem { F = f, Cell = cell });
            int i = heap.Length - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (Less(heap[i], heap[parent]))
                {
                    var tmp = heap[i]; heap[i] = heap[parent]; heap[parent] = tmp;
                    i = parent;
                }
                else break;
            }
        }

        static HeapItem HeapPop(NativeList<HeapItem> heap)
        {
            HeapItem root = heap[0];
            int last = heap.Length - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            int n = heap.Length;
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1;
                int r = l + 1;
                if (l >= n) break;
                int best = l;
                // on an exact tie between children, keep the left child
                if (r < n && Less(heap[r], heap[l])) best = r;
                if (Less(heap[best], heap[i]))
                {
                    var tmp = heap[i]; heap[i] = heap[best]; heap[best] = tmp;
                    i = best;
                }
                else break;
            }
            return root;
        }
    }

    // =====================================================================
    // Phase 4 - the view build
    // =====================================================================
    [BurstCompile]
    public partial struct Phase4System : ISystem
    {
        ComponentLookup<PawnCell> _pcell;
        ComponentLookup<PawnNeed> _pneed;
        ComponentLookup<ThingCell> _tcell;
        ComponentLookup<ThingState> _tstate;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimData>();
            _pcell = state.GetComponentLookup<PawnCell>(true);
            _pneed = state.GetComponentLookup<PawnNeed>(true);
            _tcell = state.GetComponentLookup<ThingCell>(true);
            _tstate = state.GetComponentLookup<ThingState>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _pcell.Update(ref state);
            _pneed.Update(ref state);
            _tcell.Update(ref state);
            _tstate.Update(ref state);

            ref var d = ref SystemAPI.GetSingletonRW<SimData>().ValueRW;

            var slice = d.FrontIsA == 1 ? d.SliceB : d.SliceA;
            var view = d.FrontIsA == 1 ? d.ViewB : d.ViewA;

            var solid = d.Solid;
            var temp = d.Temp;

            int ySlice = d.Tick % W.Y;
            int b = ySlice * W.LayerCells;
            for (int i = 0; i < W.LayerCells; i++)
            {
                int ci = b + i;
                slice[i] = solid[ci] == 1 ? (byte)255 : (byte)(temp[ci] & 0xFF);
            }

            int o = 0;
            for (int p = 0; p < W.PawnCount; p++)
            {
                Entity e = d.Pawns[p];
                view[o++] = p;
                view[o++] = _pcell[e].Value;
                view[o++] = _pneed[e].Value;
            }
            for (int i = 0; i < 500; i++)
            {
                Entity e = d.Things[i];
                view[o++] = i;
                view[o++] = _tcell[e].Value;
                view[o++] = _tstate[e].Value;
            }

            // single reference swap, no copying
            d.FrontIsA ^= 1;
        }
    }
}
