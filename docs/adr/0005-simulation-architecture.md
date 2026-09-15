# ADR 0005 — Simulation architecture

**Status: proposed — criteria fixed 2026-09-15 before the benchmark ran; decision and numbers pending.**

This section is written deliberately ahead of the results. Choosing how to judge after seeing the numbers is how a benchmark becomes a justification for whatever one already preferred.

## Context

Brief §2 left the simulation architecture open and Lane D1 was to settle it by measurement, judging on: **tick cost at 250 × 250 × 40, mod-friendliness, debuggability, and the ability to test it headless**. Phase 1 Q6 narrowed the field to two candidates and fixed the threading stance:

- **plain** — structure-of-arrays in plain managed arrays (or `NativeArray` where it helps), plain C# objects for things and pawns, Burst jobs only on hot paths. RimWorld's own model.
- **ecs** — Unity DOTS/Entities: cell arrays in a system or singleton, things and pawns as entities, Burst `ISystem`s.
- MonoBehaviour-per-thing was dropped as a known non-starter at 20,000+ things.

Both are benchmarked **single-threaded**, because the project decided determinism first and a parallel candidate against a serial one would measure the wrong thing.

The workload is pinned in `docs/research/d-01-bench/WORKLOAD.md` down to the iteration order and tie-breaks, so both candidates run the identical algorithm and must produce the identical state hash. Four phases are timed separately: grid propagation (the fire/gas stand-in, Burst-jobbed in both), 20,000 things on tick-group cadences, 50 pawns with layer-aware A-star, and the immutable view build that feeds the UI.

## Gates — a candidate that fails any of these is out, whatever its timings

1. **Self-determinism.** Two runs in the same process, same seed, identical state hash.
2. **Cross-candidate equivalence.** Both candidates produce the same hash. If they disagree, the workloads differ and no timing comparison is valid until that is resolved.
3. **Headless.** Runs under `-batchmode -nographics` with no scene, and can be driven from an EditMode test. Non-negotiable: every milestone gate is a headless run.
4. **The UI seam.** Phase 4, the view build, fits **≤ 0.8 ms/tick and ≤ 2 MB double-buffered** — the budget adopted from the UI design line (`docs/design/ui-plan-reconciliation.md`). An architecture that cannot afford to publish its own state is not a candidate, however fast its internals.
5. **Frame budget.** The whole tick must fit the performance target with margin: 60 FPS at 3× speed means three ticks inside a 16.6 ms frame *alongside rendering*, on a 2022 mid-range laptop. The benchmark runs on a much stronger machine, so the margin must be large enough to survive that gap, and the ADR will state the assumed factor rather than hand-wave it.

## Weighted criteria, for candidates that pass every gate

| Criterion | Weight | How it is scored |
|---|---|---|
| Tick cost at full scale | High | Mean and p95 total tick time; per-phase breakdown to show *where* the cost is |
| Mod-friendliness | High | Brief §2 mandates data-driven Defs and Harmony-style patching from day one. Can a modder add a system, patch an existing one, or add a field from outside the assembly, with classes public/unsealed/virtual? |
| Debuggability | High | Can a developer set a breakpoint mid-tick and inspect world state in a watch window? Do stack traces name the system that failed? |
| Headless testability | High | How much ceremony does a test need to build a world, tick it N times and assert? |
| Allocation and GC | Medium | Steady-state bytes/tick (target 0) and collection counts across 1,500 ticks |
| Memory footprint | Medium | Managed plus native bytes for the full map |
| Velocity | Medium | How much boilerplate does adding one new system cost? Judged from writing the benchmark itself, which implemented the same four phases in both |

## Pre-committed tie-break

If both candidates pass every gate and their total tick costs are **within 1.5× of each other**, the decision goes to **mod-friendliness and debuggability**, in that order — because at that point both are fast enough, and the brief names those two as judging criteria in their own right. Speed only wins outright when it is the difference between fitting the frame budget and not.

If the faster candidate is more than 1.5× faster *and* the slower one has no comfortable margin against gate 5, speed wins and the modding story becomes a design problem to solve rather than a reason to choose differently.

## The benchmark machine, and what its numbers do not mean

Both candidates run on the Windows dev machine: **AMD Ryzen 7 9800X3D, 32 GB**. That is a 2024-generation part with 96 MB of L3 cache, and this workload is exactly the kind that flatters it — 2.5 million cells of array data with a random-access A-star on top. The performance target is a **2022 mid-range laptop**, which has both lower clocks and dramatically less cache.

So the raw figures must be discounted before they are compared against the frame budget, and the discount is not a small one. A conservative factor for this workload is **3×, and possibly 4× for the cache-sensitive pathfinding phase** — larger than the usual single-thread gap, precisely because the X3D cache is doing so much work here.

The budget itself: 60 FPS at 3× game speed means **three ticks inside one 16.6 ms frame**, sharing that frame with rendering and UI. A tick therefore has roughly 5.5 ms to itself on the *target* machine, before rendering takes its share.

This is the number that decides whether either candidate is viable, and it will be applied to both identically.

## Results: candidate "plain"

```
D1RESULT candidate=plain
D1RESULT unity=6000.3.24f1 packages=com.unity.burst@1.8.30,com.unity.collections@2.6.8,com.unity.mathematics@1.3.3
D1RESULT hash_run1=c7d0d7f512c0feca hash_run2=c7d0d7f512c0feca
D1RESULT phase1_ms_mean=0.283 phase1_ms_p95=0.481 phase1_ms_max=0.918
D1RESULT phase2_ms_mean=0.025 phase2_ms_p95=0.027 phase2_ms_max=0.052
D1RESULT phase3_ms_mean=0.929 phase3_ms_p95=1.671 phase3_ms_max=2.413
D1RESULT phase4_ms_mean=0.186 phase4_ms_p95=0.211 phase4_ms_max=0.448
D1RESULT tick_ms_mean=1.423 tick_ms_p95=2.288 tick_ms_max=3.478
D1RESULT alloc_bytes_per_tick=0.000 gc0=0 gc1=0 gc2=0
D1RESULT managed_bytes=326684672 native_bytes=10064012
D1RESULT setup_ms=9.388
```

Gates: self-determinism **passed** (identical hash across two in-process runs and two separate Unity invocations); headless **passed**; the UI seam **passed** with room to spare (0.186 ms against 0.8 ms, and a 69 KB double buffer against a 2 MB cap); allocation is a true zero, with no garbage collection of any generation across 1,500 ticks. Cross-candidate equivalence is pending the ECS run.

The source was checked against `WORKLOAD.md` rather than taken on trust: PRNG constants and seed, the index convention, the fixed neighbour order with per-axis bounds, the decay rule, the in-loop top-up draw, mark-clearing limited to touched indices, and the phase-4 slice write all match the contract.

`managed_bytes` is `GC.GetTotalMemory(true)` for the whole editor process, as the contract literally specifies, and is therefore mostly editor baseline; the figure that describes the simulation is the **27.5 MB** managed delta, dominated by the A-star scratch arrays, plus **10.1 MB** native for the cell grid.

### Margin against the target hardware

Three ticks must fit one 16.6 ms frame alongside rendering.

| | Per tick | Three ticks | Frame headroom left for rendering |
|---|---|---|---|
| Measured, 9800X3D | 1.42 ms | 4.3 ms | 12.3 ms — comfortable |
| Discounted 3× | 4.27 ms | 12.8 ms | 3.8 ms — tight |
| Discounted 4× | 5.69 ms | 17.1 ms | none — over budget |

**This is the most important number in the benchmark, and it is not a comfortable one.** As measured, with no reachability culling, the plain candidate does not have reliable margin on a 2022 mid-range laptop.

It is also, on inspection, the *right* discomfort rather than an architectural problem. Phase 3 is 65% of the tick, and the diagnostics show **1,058 of 1,800 replans exhausted the full 20,000-node budget and returned failure** — they were searches for targets that were never reachable. The expensive searches are the futile ones. `d-04-pathfinding.md` answers exactly this with district ids that make `Reachable()` an integer comparison, and the first experiment in M2 is to re-run this phase with that check in front of the search. If phase 3 does not collapse, the design is wrong and we find out in the cheapest possible place.

Two consequences either way: the frame budget is a **pathfinding** problem, not an architecture problem, and it must not be used to choose between the candidates unless they differ materially on phase 3 for structural reasons.

## Decision

*Pending the ECS run. To be filled from both `D1RESULT` blocks, with the reasoning shown against the criteria above.*

## Consequences

*Pending the decision. Will cover: the assembly layout that follows, what the Def system binds to, how tests construct a world, what a modder patches, and what would have to be true to revisit this.*

## Reproducing this

Both benchmark implementations are committed under `docs/research/d-01-bench/plain/` and `docs/research/d-01-bench/ecs/` alongside the workload contract. They are throwaway spike code and carry no tests, per the pragmatic-TDD policy — the determinism hash is their correctness check.
