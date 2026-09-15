# ADR 0005 — Simulation architecture

**Status: accepted 2026-09-15.** The criteria below were fixed before the benchmark ran; the decision applies them to the measured result.

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

## Results: candidate "ecs"

```
D1RESULT candidate=ecs
D1RESULT unity=6000.3.24f1 packages=com.unity.entities@1.4.8,com.unity.burst@1.8.30,com.unity.collections@2.6.8,com.unity.mathematics@1.3.3
D1RESULT hash_run1=c7d0d7f512c0feca hash_run2=c7d0d7f512c0feca
D1RESULT phase1_ms_mean=0.442 phase1_ms_p95=0.727 phase1_ms_max=1.049
D1RESULT phase2_ms_mean=0.171 phase2_ms_p95=0.182 phase2_ms_max=1.427
D1RESULT phase3_ms_mean=1.622 phase3_ms_p95=2.861 phase3_ms_max=3.638
D1RESULT phase4_ms_mean=0.212 phase4_ms_p95=0.223 phase4_ms_max=0.414
D1RESULT tick_ms_mean=2.446 tick_ms_p95=3.892 tick_ms_max=5.883
D1RESULT alloc_bytes_per_tick=0.0 gc0=0 gc1=0 gc2=0
D1RESULT managed_bytes=425279488 native_bytes=36094472
D1RESULT setup_ms=33.813
```

And the same ECS code with Burst globally disabled, which is the like-for-like comparison against the plain candidate's *managed* A-star:

```
D1NOBURST tick_ms_mean=6.857  phase1=0.699  phase2=1.256  phase3=4.343  phase4=0.559
```

**Gate 2, cross-candidate equivalence: passed.** Both candidates produced `c7d0d7f512c0feca`, and every diagnostic agrees — 1,800 replans attempted, 742 succeeded, identical temperature sum, identical solid count, identical PRNG state after setup. Two independently written implementations, in two different paradigms, arrived at bit-identical simulation state. That is the result that makes every other number here worth reading.

The run also exposed the one genuine ambiguity left in the contract — whether the 20,000-pop budget counts lazily-discarded pops — which has now been resolved and written into `WORKLOAD.md`.

## Comparison

| | plain | ecs | ratio |
|---|---|---|---|
| Phase 1, grid (Burst in both) | 0.283 ms | 0.442 ms | 1.6× |
| Phase 2, things | 0.025 ms | 0.171 ms | 6.8× |
| Phase 3, pawns and A-star | 0.929 ms *(managed)* | 1.622 ms *(Burst)* | 1.7× |
| Phase 4, view build | 0.186 ms | 0.212 ms | 1.1× |
| **Total tick, mean** | **1.423 ms** | **2.446 ms** | **1.72×** |
| Total tick, p95 | 2.288 ms | 3.892 ms | 1.7× |
| Native memory | 10.1 MB | 36.1 MB | 3.6× |
| Setup | 9.4 ms | 33.8 ms | 3.6× |
| Allocation per tick | 0 B | 0 B | — |

Three fairness points, all raised by the ECS implementer unprompted and all accepted:

1. **The ECS phase-3 figure is Burst-compiled; the plain one is not.** The honest same-compiler comparison is the `D1NOBURST` block: plain's managed A-star at 0.929 ms against ECS's managed A-star at 4.343 ms. ECS is not losing because of Burst — it is winning back some of a larger deficit with it.
2. **This cuts against ECS in one direction and for it in another.** ECS has already spent its Burst card on phase 3; plain has not. The plain candidate's largest cost centre is still unoptimised.
3. **The contract's index-order requirement penalises ECS**, forcing `ComponentLookup` random access instead of idiomatic chunk iteration in phase 2. That penalty is real for *this project*, though, not an artefact: deterministic iteration order is a gate we are not willing to trade away.

## Decision

**Plain C# structure-of-arrays with Burst jobs on measured hot paths.** Candidate "plain".

Applying the criteria as they were written before the numbers existed:

- **Tick cost.** Plain is **1.72× faster**, which exceeds the 1.5× threshold at which the tie-break would have handed the decision to mod-friendliness. The margin condition attached to that rule is also met: ECS at 2.446 ms needs 7.3 ms of a 16.6 ms frame for three ticks *on a 9800X3D*, and at the 3–4× discount this project applies for its 2022-laptop target it has no viable margin at all. Plain is tight; ECS is out of budget.
- **Mod-friendliness.** ECS is *better* at adding data — a mod assembly can declare its own `IComponentData` and attach it to existing pawns at runtime, changing the archetype without touching a base class, which is genuinely elegant. But it is substantially worse at **interception**, and interception is what RimWorld's modding culture actually does. `ISystem` is a struct with non-virtual methods; `OnUpdate` is Burst-compiled to a native function pointer, so a Harmony patch either does nothing or silently forces the managed fallback; and source generators rewrite the method body, so the IL a modder patches is not the IL we wrote. The convention this project already committed to — public, unsealed, virtual where cheap, so Harmony-style patching stays possible — is achievable in plain C# and effectively unachievable for Bursted systems.
- **Headless testability.** Both pass, and ECS was easier to drive than expected: `new World()`, `CreateSystem<T>()`, update the handles, no scene or bootstrap required. But Entities hard-depends on the physics, audio, uielements and analytics modules, which means **a `Sim` assembly with no UnityEngine dependency is not achievable under ECS**, and the simulation could never run under a bare dotnet SDK. Unit U01 of the slice plan asserts exactly that property with a reflection test. Choosing ECS would mean deleting that test and accepting Unity as a permanent dependency of the simulation.
- **Debuggability.** Plain C# steps in a debugger with values visible in a watch window. Bursted systems do not, and the source-generator rewriting means the code being executed is not the code on screen.
- **Memory and setup.** Plain uses 3.6× less native memory and starts 3.6× faster. Neither is decisive; both point the same way.

ECS loses on the criterion it was strongest on paper for — raw throughput — while also losing on three of the four criteria the brief named. There is no version of the weighting where it wins.

**A note on what this decision is not.** It is not a verdict on DOTS generally. ECS is built to exploit parallelism, and this benchmark deliberately forbade parallelism because the project chose determinism first. A different project, with a different threading stance, could reasonably reach the opposite conclusion from the same numbers.

## Consequences

1. **Assembly layout stands as planned**: `Odyssey.Sim` and `Odyssey.Sim.Contracts` carry no UnityEngine reference, and unit U01's reflection test enforces it. The simulation stays runnable under a plain dotnet SDK, which also unblocks fast Sim tests in environments with no Unity at all.
2. **Data layout is structure-of-arrays** over plain managed arrays and `NativeArray`, at the index convention fixed in `02-world-and-layers.md`. The benchmark confirms the cell grid costs ~10 MB and is not a bottleneck.
3. **Burst is applied per measured hot path, behind clean boundaries** — not as an architecture. Phase 1 (grid propagation) is the proven case at 0.283 ms for 8,000 frontier cells. Whether the cell A-star needs it is an open question that M2 answers with a measurement, and it is the project's main remaining performance reserve.
4. **Things and pawns are plain C# objects**, public and unsealed, with virtual decision points. `04-data-model.md` binds Defs to them via source-generated binders and integer handles.
5. **Tests construct a world through the composition root** with no scene, no GameObject and no Synty content.
6. **Modders patch ordinary C#.** The three seams in `07-modding.md` — Defs, registries, and not preventing Harmony — all remain viable, which they would not have been under ECS.
7. **The frame budget is a pathfinding problem, and it is now the project's top technical risk.** Plain has 3.8 ms of frame left for rendering at a 3× discount and none at 4×. The fix is not architectural: 65% of the tick is A-star and most of that is futile searches for unreachable targets. The district-id reachability check from `d-04-pathfinding.md`, measured in M2, is the first and cheapest experiment.
8. **Burst compilation must be synchronous in benchmarks and tests**, or asynchronous compilation bleeds into the measured window — it inflated phase 1 from 0.283 ms to 0.498 ms mean with a 3.927 ms maximum before this was set.

## What would make us revisit this

- The pathfinding work fails to bring the tick inside budget on target hardware *and* profiling shows the remaining cost is structural rather than algorithmic.
- The threading stance changes — if the project ever accepts a parallel simulation, ECS's case becomes much stronger and this ADR should be re-run rather than argued about.
- Agent or entity counts rise by an order of magnitude beyond the 50-colonist, 300-animal target.

## Reproducing this

Both implementations are committed under `docs/research/d-01-bench/plain/` and `docs/research/d-01-bench/ecs/` alongside the workload contract. They are throwaway spike code and carry no tests, per the pragmatic-TDD policy — the determinism hash is their correctness check, and it is a strong one: it caught the single contract ambiguity that separated the two runs.

## Reproducing this

Both benchmark implementations are committed under `docs/research/d-01-bench/plain/` and `docs/research/d-01-bench/ecs/` alongside the workload contract. They are throwaway spike code and carry no tests, per the pragmatic-TDD policy — the determinism hash is their correctness check.
