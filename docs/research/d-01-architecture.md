# Lane D1 — Simulation architecture: two-way benchmark (spec written; runs pending)

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

Spec complete 2026-09-15; a bench-project skeleton was scaffolded the same day (session-local). **The runs have not happened yet** — they are the next unit of work, followed by `docs/adr/0005-simulation-architecture.md` with the numbers.

## Layer questions touched

Q10 (unit of simulation for gas/fire across layers): the grid-propagation phase is the cost model for it. Q12: the benchmark itself is the first thing the slice must prove.

## Sources

- `docs/research/phase1-answers.md` Q6 · `docs/design/ui-plan-reconciliation.md` · `a-15-time-and-simulation.md` (cadence model) · `c-cataclysm-dda.md` (per-level cache pattern for the grid)

## Confidence

Spec: high. Numbers: none yet — deliberately unclaimed until the runs exist.

## Could not be determined

Nothing yet; this file gains the results table when the runs land.
