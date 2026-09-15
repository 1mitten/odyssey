# Plan — the vertical slice (M0 → M3)

**Target:** five pawns survive ten in-game days (600,000 ticks) unattended in a headless run, with zero errors, reproducible from a seed. That is the whole definition of done; everything below serves it.

This file is written to be executed by a session with no other context. Read `CLAUDE.md`, then `docs/design/00-vision.md`, `02-world-and-layers.md` and `03-systems-catalogue.md`, then this. Do not start a unit whose dependencies are unmet.

**Before any of this begins:** Phase 3 ends with a hard stop for the owner's approval (brief §6). No unit below is started until that approval is given.

## Status, 2026-09-15

| Milestone | State |
|---|---|
| **M0** | U01–U07 **done**. Only U08 (CI) remains, and it needs the owner to register a self-hosted runner. |
| **M1** | U09 cell grid, U10 support solver, U11–U13 worldgen and templates **done**. U14–U16 rendering, camera and inspection **in progress**. |
| **M2** | U17–U18 reachability and pathfinding **done, and measured**. U19–U24 pawns and characters **in progress**. |
| **M3** | Not started. |

142 tests green in the fast tier. Measured: support solve 54 ms for 2.5M cells and 0.003 ms per edit; worldgen 215 ms for a full map; pathfinding 2.4× faster than naive with budget exhaustion down 91%.

Two corrections worth carrying forward. The pathfinding premise in §4 of `05-ai-and-jobs.md` was **falsified by its own experiment** and has been rewritten: the win came from hierarchical search, not from the reachability check, which is kept for a different and better reason. And the cell size briefly had a competing "provisional" value from the UI line; the measured 2.5 × 2.5 × 3.0 m stands (ADR 0002).

## How to read a unit

Each unit has an id, a size (**S** ≈ a focused session, **M** ≈ a day, **L** ≈ several days), its dependencies, and **done criteria that are testable**. A unit is finished when its criteria pass in a headless run, not when the code exists.

**Parallelism.** Units in the same milestone with disjoint dependencies can be run as separate agents against separate branches; the `∥` marker names units that may proceed alongside each other. The rule that makes this safe: one unit owns one set of files, and shared files (Def schemas, the cell record, `INDEX.md`) are owned by the coordinating session only.

**Every unit** obeys the standing conventions in `CLAUDE.md`: layer-aware from the first commit, test-first for Sim, nothing in Sim referencing UnityEngine, nothing depending on `Assets/Synty/`.

---

## M0 — Foundations

The machinery. No gameplay. At the end of M0 an empty world ticks deterministically, saves, loads and proves it.

| Unit | Size | Depends on | Done when |
|---|---|---|---|
| **U01 Assemblies and analysers** | S | — | `Odyssey.Sim`, `Odyssey.Sim.Contracts`, `Odyssey.Ui.Core`, `Odyssey.Presentation`, `Odyssey.Editor` and three test assemblies exist with correct references. Nullable and analysers on. A test reflects over Sim and Sim.Contracts and **fails if either references a UnityEngine type**. |
| **U02 Tick loop** ∥ | M | U01 | Fixed tick with groups (every / rare 250 / long 2000) and hash-offset phase spreading; speeds as tick-rate multipliers; pause. Driven from an EditMode test with no scene. Test: a thing on the rare group ticks exactly 1/250 of ticks, and the population is evenly spread across phases. |
| **U03 Determinism harness** ∥ | M | U02 | FNV-1a state hash over sim state; a seeded world; a test asserting two *separate processes* agree after 10,000 ticks. Per-tick hash dump behind a flag, for binary-searching the first divergent tick later. |
| **U04 Def loader** ∥ | L | U01 | Parse → inherit → patch → deserialise → resolve references → validate, per `d-07-data-pipeline.md`. Errors name file and line. Tests load fixture Defs from a temp folder. A Def referencing a missing Def fails loudly at load, never at tick time. |
| **U05 Save/load** | L | U03, U04 | Binary container per `d-06-save-load.md`. **Round-trip test**: hash at N, save, load into a fresh world, hash, assert equal. **Byte-stability test**: saving the same state twice produces identical bytes (this is the cheap detector for unordered iteration). Includes the **MemoryPack-under-IL2CPP spike** named in that research as its riskiest assumption — settle it before building on it, and fall back to MessagePack-CSharp if it fails. |
| **U06 Composition root** | S | U02, U04 | One entry point builds a world from (Defs, seed) and returns a tickable object. No singletons, no static mutable state. Every test constructs its world through it. |
| **U07 The sim→UI seam** | M | U02 | `WorldViewStore` double buffer and `IntentBus` per `docs/design/ui-plan-reconciliation.md` and ADR 0004. Views are immutable structs keyed by stable handles; intents are consumed at tick boundaries and may be rejected with a reason code. Budget: view build ≤ 0.8 ms/tick, ≤ 2 MB double-buffered — the benchmark already measured this shape, so the target is known to be reachable. |
| **U08 CI** ∥ | M | U01 | Self-hosted Windows runner per `d-08-ci-tooling.md` (Unity Personal cannot do headless activation — read that file before attempting anything else). EditMode tests on every push, PlayMode with a graphics device, results published as a check, **branch protection on**. Without the last part there is a report, not a gate. |

**M0 gate:** the standing five-part gate in `08-milestones.md`, plus a clone without `Assets/Synty/` passing every Sim test.

---

## M1 — World

At the end of M1 a ruined city exists, holds itself up, and can be looked at and clicked on.

| Unit | Size | Depends on | Done when |
|---|---|---|---|
| **U09 Cell grid and chunks** | M | U06 | SoA arrays at the fixed index convention (`02-world-and-layers.md` §1–2), 250 × 250 × 40 allocated; 25 × 25 per-layer chunk grid with dirty tracking. Benchmark-derived expectation: the grid alone costs ~10 MB native and is not a bottleneck. |
| **U10 Support solver** | L | U09 | Full-map solve and incremental dirty-frontier solve, per the recursive bottom-up rule. **The incremental path is tested against the full solve as its oracle**, over randomised edit sequences. Collapse cascades breadth-first, drops what stood on the slab, leaves rubble. |
| **U11 Worldgen: surface** | L | U09 | Passes 1–5: street grid, plots, stamped shells, damage, intactness grid. Deterministic from seed. |
| **U12 Worldgen: depth and finish** | M | U11 | Passes 6–10: strata, salvage deposits, utility taps, sealed vaults, start location. Ends with a **full support solve that asserts consistency** — a template that cannot stand is a failing test, not a runtime surprise. |
| **U13 Shell templates** ∥ | M | U11 | A template format authored in **cells, not metres**, carrying its own vertical extent; two or three shells built from the `SM_Bld_Base_*` kit. Template content lives outside `Assets/Synty/` and references modules by id, so a clone without the packs still loads the templates. |
| **U14 Rendering** | L | U09, U13 | Instanced per-chunk buckets keyed by (mesh, stuff); one cached material per stuff with `_BaseColor`, per `e-04-tint-strategy.md`. **Validation spike: 20,000 walls across several materials, batch count checked in the Frame Debugger** before the scheme is trusted. |
| **U15 Camera and slice** | M | U14 | The slice table in `06-rendering-and-camera.md`: active layer drawn roofless, layers above ghosted and **never interactive**, N layers below darkened. Selection raycasts stop at the active layer — test this explicitly, it is the single most-reported complaint in the closest comparable game. |
| **U16 Cell inspection** ∥ | S | U15, U07 | Click a cell, read back terrain, floor, edifice, support value and region — through the snapshot, never by touching sim objects. |

**M1 gate addition:** a full 250 × 250 × 40 generation completes inside a stated time budget and the support assertion passes for every shipped template.

---

## M2 — Pawns

At the end of M2, three pawns live in a ruined shell: they walk upstairs, sleep, eat and haul, unattended, for a day.

| Unit | Size | Depends on | Done when |
|---|---|---|---|
| **U17 Region graph** | L | U10 | Regions per layer, portal edges for stairs, ladders and holes; incremental rebuild on world edits **tested against a full rebuild**. This unit exists because the benchmark showed 59% of cross-layer A-star searches exhausting their budget — reachability must be answered before pathing, never by pathing. |
| **U18 Pathfinding** | L | U17 | Layer-aware A-star over the region graph, deterministic tie-breaks (f, then cell index; left child on equal sift-down), per-agent replan budget, paths invalidated by edits. Per `d-04-pathfinding.md`. Test: same seed, same paths, across processes. |
| **U19 Needs and mood** ∥ | M | U04, U02 | Food, rest, minimal joy on the 150-tick cadence; mood as base plus summed thought offsets with drift; thoughts as situational and memory kinds; one mental-break behaviour at threshold. Tuning constants are Defs, not literals. |
| **U20 Skills** ∥ | S | U19 | 0–20 with experience from work and a passion multiplier. |
| **U21 Job pipeline** | L | U18, U19 | Think tree (depth-first, first valid job wins), work givers on a pre-sorted list ordered by player priority 1–4, job drivers and toils, and **reservations** as all-or-nothing claims released on any job end. Per `a-03-work-and-jobs.md`. |
| **U22 The first jobs** | M | U21 | Haul, plus needs-driven eat and sleep. Work-giver scans are ordered by a cheap distance estimate that counts a layer change as real cost, and gated by reachability before any path is computed. |
| **U23 Characters and animation** ∥ | M | U14 | Polygon ~50-bone rig, one shared controller; idle and walk from Base Locomotion; carry, mine, build, sleep, eat, downed retargeted from Mixamo into `Assets/Art/` — **never into `Assets/Synty/`**. Per `e-02-characters-animation.md`. |
| **U24 Pawn presentation** | S | U23, U15 | Pawns render and animate, culled with the slice. |

**M2 demo:** the three-pawn day, run headless and repeatable.

---

## M3 — Build and dig → the slice is complete

| Unit | Size | Depends on | Done when |
|---|---|---|---|
| **U25 Designations** ∥ | M | U21, U07 | Mine, deconstruct, build, cancel, forbid/allow — as intents, validated and rejectable with a reason. |
| **U26 Build pipeline** | L | U25, U22 | Blueprint → materials hauled → frame → work applied → built thing, with a success roll at completion. Deconstruct refunds half. |
| **U27 Materials** ∥ | M | U04, U26 | Two or three materials with `stat = base × factor + offset`. Quality tiers explicitly deferred. |
| **U28 Mining and salvage** | M | U25 | Three speeds by target: breach a slab, clear rubble, mine rock. Yields salvage into the world. |
| **U29 Roofs as floors** | L | U26, U10 | Building a slab creates the floor above; removing support collapses it, cascading, with rubble and fall damage. **This is the unit the whole project exists to prove** — test it hard, including the ruined-shell case where mining a wall orphans a pre-existing slab. |
| **U30 Stockpiles** | M | U22, U04 | Zones **per layer** with priority and filter; stacking; haul-to-best by filter → space → priority → distance; named storage groups sharing one settings record across layers. Per `a-14-bills-stockpiles-inventory.md`. |
| **U31 Support preview** ∥ | S | U29, U15 | The build preview shows support values and highlights cells a deconstruction would orphan. Cheap now, and the thing that stops the collapse rule feeling arbitrary. |
| **U32 The ten-day run** | M | all of M3 | Five pawns, 600,000 ticks, unattended, headless, zero errors, reproducible from seed. Determinism and resume-equivalence gates pass. `docs/milestones/M3-report.md` written. |

---

## Risk register for the slice

Ordered by how much trouble each would cause, with the cheapest experiment that would settle it.

1. **Pathfinding cost at 50 agents across 40 layers.** The benchmark already showed A-star at 65% of tick cost with most cross-layer searches failing. *Experiment:* U17 first, and re-run the D1 phase-3 workload with reachability culling in front of it — the number should collapse.
2. **Collapse cascades behaving badly in generated ruins.** A stamped shell that quietly fails its support rule would take the map down on tick one. *Experiment:* the generation-time full solve assertion in U12, run over every template at every damage intensity.
3. **A determinism leak found late.** *Experiment:* byte-stability and round-trip tests exist from U05 onwards, so a leak surfaces in the unit that causes it rather than on day ten.
4. **MemoryPack under IL2CPP.** Named by the save research as its riskiest assumption. *Experiment:* the small spike inside U05, before anything depends on it.
5. **Rendering the slice at full scale.** *Experiment:* the 20,000-wall Frame Debugger spike in U14, plus the still-pending D3 performance half.

## What this plan deliberately leaves out

Health beyond alive/downed/dead, temperature, light, power, plants, cooking, animals, combat, the storyteller, research, factions and trade. Every one of them is in `03-systems-catalogue.md` with a milestone. None of them is needed to prove that colonists can live in a three-dimensional ruin, which is the only question the slice asks.
