# D1 benchmark — the exact workload (both candidates implement this, unchanged)

Purpose: make two independently written implementations produce **identical simulation results**, so their timings are comparable and the comparison is about architecture, not about who wrote a cheaper loop. Every constant, every iteration order and every tie-break below is part of the contract. Deviating invalidates the run; if you must deviate, say so loudly in the results block.

Integer maths only in the simulation. No floating point anywhere in phases 1-3 (timing and reporting may use doubles). C# integer division truncates toward zero; rely on that.

## 0. Determinism primitives

**PRNG** — xorshift32, one shared stream, seeded `s = 12345u`. Every draw below comes from this one stream, in the order written, so setup is byte-identical across implementations.

```csharp
uint Next(ref uint s) { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
```

**State hash** — FNV-1a 64-bit, starting `h = 14695981039346656037UL`, and for each byte `h ^= b; h *= 1099511628211UL;`. Byte stream, in this exact order:

1. `temp[i]` for i = 0 .. cellCount-1, each 2 bytes little-endian (int16).
2. `thingState[i]` for i = 0 .. 19999, each 4 bytes little-endian (int32).
3. For p = 0 .. 49: `pawnCell[p]` then `pawnNeed[p]`, each 4 bytes little-endian (int32).

Report the hash as 16 lowercase hex digits.

## 1. World

Dimensions X = 250, Z = 250, Y = 40 layers; cellCount = 2,500,000.
Index convention, fixed: `index = (y * 250 + z) * 250 + x`.

Two cell arrays, structure-of-arrays:

- `byte[] solid` — 1 blocked, 0 open.
- `short[] temp` — integer temperature, initialised 0.

Setup, in this order:

1. For i = 0 .. cellCount-1: `solid[i] = (Next(ref s) % 100) < 35 ? 1 : 0`.
2. Heat sources, for k = 0 .. 1999: `idx = (int)(Next(ref s) % cellCount)`; `temp[idx] = 1000`; append idx to the initial frontier list (duplicates allowed, order preserved).

## 2. Phase 1 — grid propagation (the fire/gas/heat stand-in)

Every tick, over a frontier list of cell indices capped at 8,000 entries.

Neighbour order is fixed: -x, +x, -z, +z, -y, +y, i.e. index deltas -1, +1, -250, +250, -62500, +62500, each bounds-checked on its own axis (no wrapping: x=0 has no -x neighbour, y=39 has no +y neighbour).

For each cell c in the current frontier, in list order:

1. For each in-bounds neighbour n in the fixed order: if `solid[n] == 1`, skip. Otherwise `int delta = (temp[c] - temp[n]) / 4;` and if delta is non-zero then `temp[n] += delta;` `temp[c] -= delta;` and, if n is not already marked for the next frontier and that frontier holds fewer than 8,000 entries, mark it and append n. Note that `temp[c]` is re-read for each neighbour, so the order matters. That is intended.
2. Decay: `temp[c] -= temp[c] / 64;`

Marking uses a per-cell byte marker array, cleared after the tick by resetting only the indices that were appended — never a full 2.5 MB clear.

Top-up, after building the next frontier: while it holds fewer than 2,000 entries, `idx = (int)(Next(ref s) % cellCount)`; `temp[idx] += 1000`; if not already marked, mark and append. This is the only use of the PRNG inside the timed loop and it keeps the simulation in steady state, so its call order is part of the hash contract.

Swap the frontiers. This phase is the designated Burst-jobbed hot path in both candidates.

## 3. Phase 2 — things

20,000 things, fields `int thingCell`, `int thingState`, `byte thingTicker`.

Setup, after the world setup, for i = 0 .. 19999 in order: `thingCell[i] = (int)(Next(ref s) % cellCount)`; `thingTicker[i] = (byte)(Next(ref s) % 2)`; `thingState[i] = 0`.

Per tick, for i = 0 .. 19999 in order, with `interval = thingTicker[i] == 0 ? 250 : 2000`: if `((tick + i) % interval) == 0` then `thingState[i] += (temp[thingCell[i]] & 0xFF) + 1;`

`tick` is the 0-based counter, counting warm-up ticks too, and it keeps running into the measured window.

## 4. Phase 3 — pawns (movement plus layer-aware A-star)

50 pawns, fields `int pawnCell`, `int pawnNeed`, `int[] pawnPath` (capacity 64), `int pawnPathLen`, `int pawnPathStep`.

**Portals (stairs).** 200 portals, built after the thing setup: repeat until 200 are accepted — `x = (int)(Next(ref s) % 250)`, `z = (int)(Next(ref s) % 250)`, `y = (int)(Next(ref s) % 39)`; `a = (y*250+z)*250+x`, `b = a + 62500`; accept only if `solid[a] == 0 && solid[b] == 0`; on acceptance store the pair. Portals are bidirectional. Build a lookup from cell index to portal partners; a cell may hold more than one, and insertion order is preserved.

**Pawn setup**, after portals, for p = 0 .. 49: draw `idx = (int)(Next(ref s) % cellCount)` repeatedly until `solid[idx] == 0`; `pawnCell[p] = idx`; `pawnNeed[p] = 50000`; path length and step 0.

**Per tick**, in this order:

1. Needs, for p = 0 .. 49: `pawnNeed[p] -= 1;` and if it goes below 0, set it to 100000.
2. Movement, for p = 0 .. 49: if `pawnPathStep[p] < pawnPathLen[p]`, set `pawnCell[p] = pawnPath[p][pawnPathStep[p]]` and increment the step.
3. Replan, one pawn per tick, `p = tick % 50`: choose a target, run A-star, and on success store up to the first 64 steps after the start cell and reset the step to 0; on failure set the path length to 0.

**Target choice**, bounded so search cost stays realistic. The pawn is at (px,pz,py). Loop k = 0 .. 49, and on **every** iteration draw all three values so the stream stays aligned: `dx = (int)(Next(ref s) % 81) - 40`, `dz = (int)(Next(ref s) % 81) - 40`, `dy = (int)(Next(ref s) % 7) - 3`. Clamp tx, tz, ty into range, form `t = (ty*250+tz)*250+tx`, and if no target has been accepted yet and `solid[t] == 0` and `t != pawnCell[p]`, accept t and remember it. Keep looping to the end regardless. If nothing was accepted, skip the replan.

**A-star contract**, because two implementations must agree:

- Neighbours of cell c at (x,z,y): the four horizontal moves -x, +x, -z, +z in that order, same layer, bounds-checked, skipping solid; then the portal partners of that cell in insertion order, also skipping solid. Every move costs 10.
- Heuristic to (tx,tz,ty): `h = 10 * (|x-tx| + |z-tz|) + 10 * |y-ty|`.
- Open set: a binary min-heap of (f, cellIndex), ordered by f ascending, ties broken by cellIndex ascending. In sift-down, when the two children compare exactly equal, choose the left child. Lazy deletion: duplicates may be pushed; on pop, skip a cell already closed.
- `g` and `cameFrom` are per-cell int arrays, `closed` a per-cell byte array, all persistent across searches. Every cell a search touches is appended to a touched list and reset after that search — never clear the whole arrays.
- Expansion budget: stop after 20,000 pops and treat it as failure.
- On popping the target, walk cameFrom back to the start, reverse, and store the first 64 steps after the start.

## 5. Phase 4 — the view build (the UI seam)

A judged phase, not an afterthought: budget 0.8 ms/tick or less, and 2 MB or less double-buffered. The simulation writes into a pooled back buffer, then swaps a reference; nothing allocates in steady state.

Per tick, into the back buffer:

1. Slice colours for the active slice layer `ySlice = tick % 40`: all 62,500 cells of that layer in index order, one byte each, `solid[i] == 1 ? 255 : (temp[i] & 0xFF)`.
2. 50 pawn views: for p = 0 .. 49, the triple (pawnId, cell, need).
3. 500 thing views: for i = 0 .. 499, the triple (thingId, cell, state).

Then swap the front and back buffer references. A single reference swap, no copying.

## 6. Harness and reporting

- Warm-up 300 ticks, untimed but fully executed, tick counter starting at 0 and running on. Measured window: the next 1,500 ticks.
- Time each phase separately with `System.Diagnostics.Stopwatch`, converted to ms.
- Report per phase and for the whole tick: mean, p95, max, in ms to three decimals.
- Allocation: `GC.GetAllocatedBytesForCurrentThread()` delta across the measured window divided by 1,500, as bytes/tick. Steady-state target is 0.
- GC: `GC.CollectionCount(0/1/2)` deltas across the measured window.
- Memory: `GC.GetTotalMemory(true)` after setup, plus total bytes explicitly allocated in native containers if the candidate uses them.
- Determinism gate: run the whole thing twice in the same process, fresh setup each time, and print both state hashes. They must match, or the candidate fails its own gate.
- Print one machine-readable block, exactly these keys, one per line:

```
D1RESULT candidate=<plain|ecs>
D1RESULT unity=<version> packages=<name@version,...>
D1RESULT hash_run1=<16 hex> hash_run2=<16 hex>
D1RESULT phase1_ms_mean=<> phase1_ms_p95=<> phase1_ms_max=<>
D1RESULT phase2_ms_mean=<> phase2_ms_p95=<> phase2_ms_max=<>
D1RESULT phase3_ms_mean=<> phase3_ms_p95=<> phase3_ms_max=<>
D1RESULT phase4_ms_mean=<> phase4_ms_p95=<> phase4_ms_max=<>
D1RESULT tick_ms_mean=<> tick_ms_p95=<> tick_ms_max=<>
D1RESULT alloc_bytes_per_tick=<> gc0=<> gc1=<> gc2=<>
D1RESULT managed_bytes=<> native_bytes=<>
D1RESULT setup_ms=<>
```

## 7. What each candidate may vary (this is the whole point)

Only the data layout and execution model.

- Candidate **plain**: structure-of-arrays in plain managed arrays, or NativeArray where it helps, plain C# classes and structs for things and pawns, one Burst IJob for phase 1, everything single-threaded and called directly.
- Candidate **ecs**: the same arrays held by a system or singleton, things and pawns as entities with components, Burst ISystems, single-threaded scheduling.

Both run the identical algorithm above and must produce the identical hash.
