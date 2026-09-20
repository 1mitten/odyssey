# 01 — Architecture

The simulation as it will be built, following ADR 0005. Read that ADR for *why*; this document is *what*.

**Decided by measurement:** plain C# structure-of-arrays with Burst jobs on measured hot paths, single-threaded and deterministic. The alternative (Unity DOTS/Entities) was implemented against the same pinned workload, produced the identical state hash, and lost on speed (1.72×), on interception-style patching, on debuggability, and on the ability to keep Unity out of the simulation assembly.

## 1. The shape in one paragraph

A world is a plain C# object graph plus a set of flat arrays, built by a composition root from Defs and a seed. It ticks on a fixed step, single-threaded, in a defined phase order. At the end of each tick it publishes an immutable snapshot of itself into a double buffer. Presentation and UI read that snapshot and never touch the simulation; they send player actions back as intents on a queue consumed at the next tick boundary. Nothing in the simulation references UnityEngine, so the whole thing runs — and is tested — headless.

## 2. Assemblies

As built (the decided names and what became of them are in the note below):

```
Odyssey.Sim              no UnityEngine   the simulation
Odyssey.Sim.Contracts    no UnityEngine   views, handles, intents, reason codes — the seam
Odyssey.Hud              no UnityEngine   UI directors, headless-testable
Odyssey.Presentation     UnityEngine      world rendering, camera, cut-away, the UI Toolkit shell (Presentation/Ui)
Odyssey.Editor           UnityEngine      editor tooling (SyntyInventory, SyntyImport, scene generators)
Odyssey.Tests.Sim / Hud / Presentation (EditMode) and Odyssey.Tests.PlayMode (PlayMode)
```

Dependency direction, enforced rather than encouraged: `Presentation → Hud → Sim.Contracts ← Sim`. **`Hud` never references `Sim`.**

**Names as built (noted 2026-09-19 by the baseline audit):** `Odyssey.Ui.Core` became `Odyssey.Hud`, and `Odyssey.Ui.Unity` never became an assembly of its own — the UI Toolkit shell lives in `Odyssey.Presentation/Ui`. The test assemblies kept the real area names (`…Tests.Hud`, `…Tests.Presentation`, `…Tests.PlayMode`), and `Odyssey.Editor` lives at `Assets/Editor/Odyssey/`. The rule above is what matters and it holds: `Odyssey.Hud` references only `Sim.Contracts`, `Odyssey.Presentation` references `Hud`, `Sim` and `Sim.Contracts`, and the asmdefs enforce it.

Unit U01 of the slice plan asserts the UnityEngine-free property with a reflection test over `Sim` and `Sim.Contracts`. That test is the reason this project can run simulation tests under a plain dotnet SDK with no editor, and it is the property ECS would have made impossible.

## 3. The tick

A fixed step. Speeds are tick-rate multipliers, never a variable delta — a variable timestep and a determinism gate cannot coexist.

**Tick groups**, in RimWorld's proven shape (`a-15-time-and-simulation.md`): every tick, rare (250), long (2000), with **hash-offset phase spreading** so roughly 1/interval of each population runs on any given tick. The benchmark confirms the property that makes this work: cost scales with *ticking things*, not with cell count. 2.5 million cells are cheap; 20,000 always-ticking things would not be.

Phase order within a tick is fixed and is part of the determinism contract:

1. Consume intents from the `IntentBus` (validate, apply or reject with a reason).
2. World systems — grid propagation, support solving, region rebuild for dirty chunks.
3. Thing ticks, by tick group.
4. Pawn ticks — needs, think tree, job execution, movement.
5. Deferred structural events — collapses, spawns, removals — applied at a single point so nothing mutates the world mid-scan.
6. Publish the snapshot; swap the buffer.

Step 5 exists because the alternative — letting a collapse mutate the grid while a work giver is scanning it — is the classic source of both crashes and non-determinism.

## 3a. Subsystems, and why they are not called directors

Phases 2 and 4 are open to registered **subsystems**. Each declares its own phase and its order within that phase, so the sequence of `AddSystem` calls in the composition root cannot change behaviour — a refactor that moves a registration line is guaranteed not to alter simulation results. Ordering ties break on name, never on registration accident or dictionary iteration. The schedule is sorted once at construction, because the tick has roughly 5.5 ms on the target machine and must not spend any of it deciding what to run.

This exists because the alternative was what the project actually had for a while: a tick method with *comments* where the systems should be, and well-built subsystems written as libraries that nothing called. Registration makes the architecture real rather than implied.

**The vocabulary is deliberate.** The simulation has **systems**; the interface layer has **directors** (`SliceDirector`, `ToolDirector`, `AlertDirector` and the rest, owned by the UI line of work). They are not the same pattern and must not share a name:

| | Subsystem | Director |
|---|---|---|
| Lives in | `Odyssey.Sim` | `Odyssey.Hud` |
| Runs | inside a tick, in a fixed phase | per frame, or on an event |
| May mutate the world | yes, that is its job | **never** |
| Reads | the world directly | the published snapshot only |
| Ordering | declared phase and order | frame order, not simulation-critical |

### The subsystem catalogue

| System | Phase | Order | Responsibility |
|---|---|---|---|
| `SupportSystem` | WorldSystems | 10 | Incremental support solve; turns collapses into deferred structural events |
| *Navigation* (M2) | WorldSystems | 20 | Region and district rebuild for dirty chunks — after support, because a collapse changes what is walkable |
| *Grid propagation* (M4) | WorldSystems | 30 | Fire, gas and heat over an active frontier |
| *Needs* (M2) | Pawns | 10 | Needs decay on the 150-tick cadence |
| *Jobs* (M2) | Pawns | 20 | Think tree, work givers, job execution |
| *Movement* (M2) | Pawns | 30 | Path following, after jobs have decided where to go |

Two rules that keep this honest. A subsystem **never applies a structural change inline**: collapses, spawns and removals are deferred to phase 5, because another system in the same phase may be part-way through scanning the grid. And the Things phase is closed to subsystems — it belongs to the tick-group dispatcher, and registering a system there throws rather than silently never running.

### What stays a plain library

`SupportSolver`, `WorldGenerator` and the pathfinder are **not** systems. They are pure libraries that take data and answer questions, with thin system adapters where they need to run per tick. That separation is what let the support solver be exercised by roughly 7,900 random edits in a unit test with no world, no tick and no scene in sight — and it is why that test caught a real propagation bug.

## 4. Data layout

Structure-of-arrays for anything per-cell, at the index convention fixed in `02-world-and-layers.md` (`index = (y * 250 + z) * 250 + x`). Measured: ~10 MB native for the full 250 × 250 × 40 grid, which is not a bottleneck.

Things and pawns are **plain C# objects** — public, unsealed, virtual at decision points — held in arrays with integer handles as their identity. Handles, not references, are what crosses the snapshot boundary and what goes into save files.

Defs are frozen after load, reached through integer `DefHandle<T>` indices, with tick-time stat tables flattened into struct-of-arrays form (`04-data-model.md`).

## 5. Threading and Burst

**Single-threaded by decision** (Phase 1 Q6), because determinism is a milestone gate and a free-threaded simulation is the standard way to lose it.

Burst is applied **per measured hot path, behind a clean boundary** — a job that takes arrays in and writes arrays out, called synchronously with `.Run()`. It is not an architecture and not a default. The one proven case is grid propagation: 0.283 ms for 8,000 frontier cells across six neighbours including the vertical ones.

Two operational rules learned from the benchmark:

- **Burst compilation must be synchronous in tests and benchmarks.** Asynchronous compilation bled into the first measured window and inflated the grid phase from 0.283 ms to 0.498 ms mean, with a 3.927 ms maximum.
- **The A-star is not Bursted yet**, and that is deliberate — it is the project's main remaining performance reserve, to be spent only if M2's reachability work does not free enough budget.

## 6. The sim→UI seam

Adopted from the UI design line as a *constraint on* this architecture rather than an output of it, and measured as a judged phase of the benchmark.

**Read:** the simulation builds an immutable snapshot into a pooled back buffer at tick end and swaps a single reference. Views are immutable structs keyed by stable handles (`PawnId`, `ThingId`, `CellRef(x,z,y)`) — never references to simulation objects. Expensive detail views are subscription-driven, so a closed panel costs nothing. A panel open on a pawn that dies mid-tick holds only an id: the next snapshot lacks that key and the panel closes cleanly rather than dereferencing freed state.

**Write:** the UI never mutates the simulation. Actions become immutable intent structs on an `IntentBus`, consumed at a tick boundary, validated, and rejectable with a reason code the UI can surface.

**Measured cost:** 0.186 ms/tick to publish a 62,500-cell slice plus pawn and thing views, with a 69 KB double buffer — against budgets of 0.8 ms and 2 MB. Comfortable.

This seam also buys deterministic replay (intents are a replayable log), undo for designations, thread-safety if the tick ever moves off the main thread, and the natural place for a modder to intercept player actions.

## 7. Save format

Binary container with per-section LZ4, MemoryPack for section payloads (`d-06-save-load.md`). The cell grid is palette-encoded and bit-packed in 25 × 25 × 5 chunks — **0.31 MiB** for 2.5 million cells, against 4.77 MiB for plain binary and 26.2 MiB for per-cell XML.

References resolve in four phases (write → read and register load ids → resolve references → post-load init). Defs are referenced by name through a per-save name table, never deep-saved, so content can change under an existing save and a missing mod degrades to a named sentinel rather than an exception.

Mid-job pawn state persists — job, driver toil index and progress, queue, reservations. Paths are **not** saved; they are recomputed, because a recomputed path is correct by construction and a saved one can be stale.

**Post-load re-derivation, not parsing, is the load-time budget.** Regions, reachability districts, support values and caches are all rebuilt on load.

## 8. Test strategy

Pragmatic TDD (Phase 1 Q5). Test-first for every Sim system. Tests construct a world through the composition root — no scene, no GameObject, no `Assets/Synty/`.

Four gates, in ascending cost, all runnable headless:

1. **Unit tests** per system.
2. **Byte-stability** — saving the same state twice produces identical bytes. The cheapest possible detector for unordered iteration, which is the most common determinism leak.
3. **Determinism** — same seed, same state hash after N ticks, across two separate processes.
4. **Resume equivalence** — 60,000 ticks unbroken versus 30,000 → save → load → 30,000, with matching hashes. This *is* the M0 headless one-day gate rather than a separate test.

Plus a per-tick hash dump behind a flag, so a divergence can be binary-searched to the first bad tick instead of being reasoned about.

The determinism hazard list from `a-15-time-and-simulation.md` is the standing checklist: unordered dictionary iteration, unseeded or UI-touched RNG, wall-clock timers, float accumulation, save/load asymmetry. The architecture avoids each by construction — sorted writes, counter-derived ids, integer costs in pathfinding, per-tick RNG reseeding that carries no state, and caches excluded from the hash.

## 9. What this architecture costs

Stated honestly, because every choice has a bill:

- **No free parallelism.** If the simulation ever needs multiple cores, that is a deliberate future project with its own ADR, not a flag to flip. ECS would have made it easier; determinism made it not worth it now.
- **Manual data layout.** Structure-of-arrays is written by hand rather than generated by a framework. The benchmark suggests that is a few hundred lines, not a subsystem.
- **The frame budget is tight.** At a 3× hardware discount, three ticks leave 3.8 ms of a frame for rendering; at 4× there is nothing. This is the project's top technical risk and it is a pathfinding problem: 65% of the tick is A-star. The reason recorded here at first — that most of that was futile searches for unreachable targets — was falsified by a follow-up experiment: only 14% of budget exhaustions were unreachable, under 1% on a structured map. `05-ai-and-jobs.md` §4 and §6 are the answer (reachability must be free for the job-giver scan; the replan win came from the abstract stage and a better heuristic), and M2 measures it further.
