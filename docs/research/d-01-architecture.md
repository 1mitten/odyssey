# Lane D1 — Simulation architecture: two-way benchmark (complete; decision in ADR 0005)

## Question

Which simulation architecture carries the game — **plain C# sim objects/arrays with Burst jobs on hot paths**, or **DOTS/ECS (com.unity.entities)** — judged on tick cost at scale, memory, GC pressure, headless testability and mod-patchability? (MonoBehaviour-per-thing was dropped in Phase 1 Q6 as a known non-starter at this scale.) The winner is recorded as **ADR 0005**.

## Constraints the winner must satisfy (fixed before the contest)

- **Determinism first** (Phase 1 Q6): single-threaded fixed-tick simulation; Burst jobs only behind clean boundaries on hot paths. Both candidates are benchmarked single-threaded.
- **The sim→UI contract is part of the tick** (adopted from the UI session's plan; `docs/design/ui-plan-reconciliation.md`): every candidate's tick includes building the immutable snapshot back buffer — budget **≤ 0.8 ms/tick, ≤ 2 MB double-buffered**. An architecture that cannot afford its own view build loses regardless of raw tick speed.
- Sim code must be testable headless with no scene and no Synty content.
- Public/unsealed/virtual-where-cheap must remain possible for modders (this inherently favours plain C#; ECS must answer for it in the ADR's moddability section).

## Benchmark workload (identical logic in both candidates; deterministic, fixed seed)

World: **250 × 250 × 40 cells** (the scale target), integer maths throughout, per-run state hash printed — a candidate must produce the same hash on every run (self-determinism gate) before its numbers count.

Per tick, four phases, each timed separately:

1. **Grid propagation** (the fire/gas/heat stand-in): an active-frontier integer heat exchange over 6-neighbours including z; seeded with 2,000 active cells, frontier capped at 8,000, deterministic ordering. Burst-jobbed in both candidates (this is the "hot path behind a boundary").
2. **Things**: 20,000 static things bucketed Rare(250)/Long(2000) with hash-offset phase spreading (the RimWorld cadence model from `a-15-time-and-simulation.md`); small integer state updates.
3. **Pawns**: 50 agents — per-tick needs decay and cell-step movement, plus layer-aware A* replans (4-neighbour XZ + ~200 stair portals between layers, bounded search, at least one replan per tick round-robin).
4. **View build** (the UI seam): write the active slice (62,500 cell colour bytes), 50 pawn views and 500 thing views into the pooled back buffer; atomic swap. Timed against the 0.8 ms budget.

Measurement: 300 warm-up ticks, 1,500 measured. Report per-phase mean/p95/max ms, `GC.CollectionCount` deltas, managed-allocation delta per tick (steady-state target: 0 B), and total resident memory. Pass bar for the whole tick: comfortably inside the 60 FPS × 3-speed budget on the RTX-5070-Ti machine, with the margin stated so the 2022-laptop target stays plausible.

## Method (so any session can execute this)

The benchmark runs in a **throwaway Unity project** (not the main repo — no URP, no Synty, nothing to muddy the numbers): `Packages/manifest.json` containing only `com.unity.entities` 1.4.x, `com.unity.burst`, `com.unity.collections`, `com.unity.mathematics`; `ProjectSettings/ProjectVersion.txt` copied from the repo (6000.3.24f1); benchmark code under `Assets/Bench/` with an editor entry point per candidate (`Bench.D1.RunPlain` / `Bench.D1.RunEcs`) invoked via `-batchmode -nographics -executeMethod`, writing a results block to the log. Candidate A: SoA `NativeArray` grid + plain C# classes for pawns/things + one Burst `IJob` for the grid phase. Candidate B: same grid arrays held by a system, things/pawns as entities with Burst `ISystem`s, single-threaded schedule. The spike code is committed under `docs/research/d-01-bench/` in the repo for reproducibility (it is throwaway per the TDD policy — no tests, the hash gate is the correctness check).

## Status

**Complete 2026-09-15.** Both candidates implemented, run and cross-validated; see Results below. Decision recorded in `docs/adr/0005-simulation-architecture.md`. Sources for both are committed under `docs/research/d-01-bench/`.

## Layer questions touched

Q10 (unit of simulation for gas/fire across layers): the grid-propagation phase is the cost model for it. Q12: the benchmark itself is the first thing the slice must prove.

## Sources

- `docs/research/phase1-answers.md` Q6 · `docs/design/ui-plan-reconciliation.md` · `a-15-time-and-simulation.md` (cadence model) · `c-cataclysm-dda.md` (per-level cache pattern for the grid)

## Confidence

High. Both candidates passed the self-determinism gate and agreed with each other on a 64-bit state hash, the plain source was checked line by line against the contract, and the fairness caveats were raised by the implementers themselves. The main residual uncertainty is the hardware discount factor for the 2022-laptop target, which is an estimate rather than a measurement.

## Could not be determined

The true hardware discount for the target laptop (estimated at 3–4x, not measured — nobody has run this on such a machine). Whether the cell A-star needs Burst, which is deliberately left as the remaining performance reserve. How much of phase 3 the district-reachability check actually removes, which M2 measures.

---

## Results (added 2026-09-15, both runs complete)

Both candidates ran on the Windows dev machine (Ryzen 7 9800X3D). **The equivalence gate passed**: both produced state hash `c7d0d7f512c0feca`, with matching diagnostics (1,800 replans attempted, 742 succeeded, identical temperature sum, solid count and post-setup PRNG state). Two independent implementations in two paradigms reached bit-identical simulation state, which is what makes the timings comparable.

| | plain | ecs | ratio |
|---|---|---|---|
| Phase 1, grid (Burst in both) | 0.283 ms | 0.442 ms | 1.6x |
| Phase 2, things | 0.025 ms | 0.171 ms | 6.8x |
| Phase 3, pawns and A-star | 0.929 ms (managed) | 1.622 ms (Burst) | 1.7x |
| Phase 4, view build | 0.186 ms | 0.212 ms | 1.1x |
| **Total tick, mean** | **1.423 ms** | **2.446 ms** | **1.72x** |
| Native memory | 10.1 MB | 36.1 MB | 3.6x |
| Setup | 9.4 ms | 33.8 ms | 3.6x |
| Allocation per tick | 0 B | 0 B | equal |

With Burst disabled in the ECS candidate, the same-compiler comparison against plain's managed A-star is 4.343 ms versus 0.929 ms for phase 3, and 6.857 ms versus 1.423 ms for the whole tick.

**Decision: plain C# structure-of-arrays with Burst on measured hot paths.** Full reasoning, gates, fairness caveats and consequences in `docs/adr/0005-simulation-architecture.md`.

Three findings that outlived the contest itself:

1. **Pathfinding, not architecture, is the performance risk.** Phase 3 is 65% of the plain tick, and 1,058 of 1,800 replans exhausted the full 20,000-node budget and returned failure — futile searches for unreachable targets. This is the direct evidence for the district-id reachability design in `d-04-pathfinding.md`.
2. **The frame budget is tight on target hardware.** Three ticks per frame at a 3x hardware discount leave 3.8 ms for rendering; at 4x there is none. See the margin table in the ADR.
3. **The UI seam is affordable.** Publishing an immutable 62,500-cell slice plus pawn and thing views costs 0.186 ms and 69 KB against budgets of 0.8 ms and 2 MB, so snapshot-read/intent-write is not a performance compromise.

The workload contract gained one clarification from the runs: the 20,000-pop budget counts only pops that expand, not lazily-discarded duplicates. Counting both is self-consistent but yields a different world (hash `589e9d8d4279a733`, 736 successful replans).
