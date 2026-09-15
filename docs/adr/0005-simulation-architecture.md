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

## Decision

*Pending. To be filled from the `D1RESULT` blocks of both candidates, with the raw numbers reproduced in full and the reasoning shown against the criteria above.*

## Consequences

*Pending the decision. Will cover: the assembly layout that follows, what the Def system binds to, how tests construct a world, what a modder patches, and what would have to be true to revisit this.*

## Reproducing this

Both benchmark implementations are committed under `docs/research/d-01-bench/plain/` and `docs/research/d-01-bench/ecs/` alongside the workload contract. They are throwaway spike code and carry no tests, per the pragmatic-TDD policy — the determinism hash is their correctness check.
