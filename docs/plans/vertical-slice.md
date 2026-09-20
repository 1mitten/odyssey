# Plan — the vertical slice (M0 → M3)

**Target:** five pawns survive ten in-game days (600,000 ticks) unattended in a headless run, with zero errors, reproducible from a seed. That is the whole definition of done; everything below serves it.

This file is written to be executed by a session with no other context. Read `CLAUDE.md`, then `docs/design/00-vision.md`, `02-world-and-layers.md` and `03-systems-catalogue.md`, then this. Do not start a unit whose dependencies are unmet.

**Before any of this begins:** Phase 3 ends with a hard stop for the owner's approval (brief §6). No unit below is started until that approval is given.

## Status, 2026-09-19

**The live status is the track table in `CLAUDE.md`** — this file stopped being the place to read
it on 2026-09-16, and the table below is kept as the record of that date rather than rewritten.
Since then: M3 has everything but stairs (`U44`); MS, WS1–WS3, RP and CL are done; the baseline
audit of 2026-09-19 (`docs/audit/2026-09-19-baseline.md`) added the **HT** hardening track below,
which is the next thing this plan schedules and which **waits for approval before any unit
starts**. Gates on 2026-09-19: fast tier 753 Sim + 449 Hud; Unity EditMode 1,872 total, 1,858
passed, 0 failed; PlayMode 82 total, 75–77 passed, 0 failed.

## Status, 2026-09-16

| Milestone | State |
|---|---|
| **M0** | **Done.** U08 CI landed today: two tiers on every push, the Unity tier on the owner's machine as a self-hosted runner, and branch protection requiring both. |
| **M1** | **Done in substance.** Grid, support solver, both worldgen paths, instanced rendering, slice camera, click-to-inspect and a generated play scene all run. The HUD arrived on top of it, which M1 never asked for. |
| **M2** | **Done, and reported.** Regions, pathfinding, needs, mood, skills, the job pipeline and the first jobs all run; a colony survives ten days headless on three seeds. U23/U24 characters and animation went further than planned — colonists walk, and swing an axe. `OQ-20` built the headless three-pawn demo and `OQ-47` gave it the storeys it needed to prove the layer claim; `docs/milestones/M2-report.md` closes the milestone (OQ-21). |
| **M3** | **Started ahead of the plan.** Designations, felling and stockpiles are in; mining is built on `claude/mines` and not yet merged. |

**Gates, 2026-09-16.** Fast tier 329 Sim and 29 Hud, Long tier 9. Unity gate 494 total, 492 passed, 0 failed (two `[Explicit]` benchmarks skipped). PlayMode 7. Wiki and label registries both current.

**Measured, and worth keeping in one place:** support solve 54 ms for 2.5M cells and 0.003 ms an edit; worldgen 215 ms for a full map; pathfinding 2.4x faster than naive with budget exhaustion down 91%; a ten-day soak about 1.1 s of wall time at 0.002 ms a tick; frame time 0.41 ms on the wooded meadow and 1.48 ms on the city; the dense HUD 0.488 ms against a 1.167 ms budget with zero allocation.

**What the slice now has that this plan never described.** A HUD (ADR 0003's Unity-free assembly plus a UI Toolkit shell), a naming registry generating labels from a CSV, a terrain surround, water, and a work-pose system that puts an axe in a colonist's hands. None of it was in M0-M3 as written; all of it is real and tested. The plan is behind the code rather than ahead of it, which is the honest reading of the table above.

**The order the remaining work actually falls in**, which is not the order below: take the seam work in the section after this one, then the research lane (`OQ-26` … `OQ-35`, none of which is written), then the measurement rows the milestone gates still want (`OQ-03`, `OQ-05`, `OQ-18`, `OQ-19`). M2 closed on 2026-09-16 (`OQ-20`, `OQ-21`) and mining merged as `5ed4573`.

## Where the seams are, and what the next feature will cost

Written 2026-09-16 from evidence rather than taste: the mining line on `claude/mines` is one feature, 73 files and 6,358 lines, and to add itself it had to edit six files that belong to everybody. **Revised 2026-09-17** — two of the six have been opened since, and the table says where each one stands rather than where it stood.

| Chokepoint | Why a feature has to touch it | What it should be, and where it stands |
|---|---|---|
| `Sim/Pawns/PawnContent.cs` | every item, job and work type was a C# constant and a table in code | **done for pawns** (`OQ-15`, `OQ-16`, then `OQ-48` on 2026-09-17). `PawnContent.Core()` and its 163 lines of hand-written tables are gone, so the XML under `Assets/Odyssey/Defs/Core/Pawns` is the only copy and a new item is written once. Every caller goes through `ContentPack.Pawns()`, which finds the pack by walking up to the repository root and caches the parse; a fingerprint guards the content in place of the old field-for-field oracle. **The world half followed the same day** (`OQ-49`): `WorldContent.Table` is the one twenty-one-row terrain table loaded from `Defs/Core/World/Terrain.xml`, both `BuildTerrain` methods are deleted, the core/natural dispatch branch went with them, and the `TODO(content)` in `WorldGenDefs.cs` is gone. ~~Mirrored by `Defs/Core/World/*.xml` that nothing but the oracle test reads~~ — **that was wrong**: everything reads those tables through `TerrainAt()`, which the grep behind the claim never looked for |
| `Sim/Pawns/JobSystem.cs` | `DefaultGivers()` was a hardcoded array, so a new job was an edit to a shared file | **done** (`OQ-44`). `WorkGiverRegistry` finds every concrete `WorkGiver` in the simulation assembly, so a giver joins the scan by existing; `SimWorldBuilder.AddWorkGiver` takes the ones declared outside it |
| `Sim.Contracts/Views.cs` | a new thing a pawn can do needed a new field on the published frame | **done** (`OQ-45`). A sparse `PawnAspect` row, `(PawnId, key, int)`, keyed by a name the feature mints for itself, published through the contributor seam that already existed for the frame. No new registration mechanism and no enum to edit. ADR 0004 amended 2026-09-17 |
| `Presentation/Bootstrap/OdysseyBootstrap.cs` | the composition root wires every system by hand | **half open. The simulation half is done** (`U34`, 2026-09-17): the scene no longer composes its own world. It had forty lines that were a copy of `ColonyWorld.Build`, and the copy had drifted — no connectors registered with the nav graph, no full support solve, and **no `SaveComponents` list, which is why the one world a player ran was the one world that could not be written to a file**. It now calls `ColonyWorld.Build(ColonyRequest)`; the inspector fields become the request. **The presentation half is still open**: every director is still wired by hand, and it is still the chokepoint with no queue row. 849 lines |
| `Presentation/Rendering/ChunkMesher.cs` | new terrain needs new meshing, and the file is on the queue's do-not-touch list | **done** (`OQ-46`, 2026-09-17). `EmitTerrain` was a chain of early returns; it is now a registered list of `ITerrainContributor`, first claim wins, and `AddTerrainContributor` inserts ahead of the plain block. **No output moved:** `ChunkMesherTests` passes unedited and OQ-03's pinned counts are identical — 512 buckets, 100,000 instances, before and after |
| `docs/design/icon-keys.csv` | a new name | **correct as it is.** This is the one seam that worked: a name added to the CSV reaches the screen with no code change |

The last row is still the point, and closing four of the others has not changed it. One of the six was a designed seam and cost nothing; the other five were files that grew a new branch of a switch. The work below is what is left of making them look like the last one — **one item, plus the unscheduled entry beneath it.**

**The order to do it in, cheapest and most load-bearing first.** All four of the original order are done bar the last, so what remains is renumbered.

1. ~~**The world tables, the last of content to Defs.**~~ **Done** (`OQ-49`, 2026-09-17). Content is now written once everywhere: pawns and world alike load from XML and no C# table mirrors them. The feared risk — **a terrain index sits in every cell of every save and every hash** — never materialised, because `WorldContent.TerrainOrder` builds the table by name in a fixed order and a test checks all twenty-one names against the constants. The risk that *did* materialise was the opposite one and nobody had named it: with both copies reduced to one, the oracle was comparing the XML against itself, and editing a terrain's work cost passed 448 tests silently. A fingerprint pins it now.
2. ~~**Mesh contributors** (`OQ-46`), the last item on this list, and the largest. Only the terrain features need it.~~ **Done 2026-09-17, and it closes the list.** All five chokepoints named after the mining line are open. What is left of the bootstrap row is the presentation half — every director still wired by hand — which has no queue row of its own.

**Scheduled now, because presentation grew.** `OdysseyBootstrap.cs` was the fifth chokepoint and the only one nothing was queued against, on the grounds that it would want a row "the moment two features want to add a director in the same week". A menu, a new-game screen and a colonist picker are that moment, so it has one: **`MS` below**, whose first two units are the simulation half (done) and the session half of this chokepoint. The presentation half — every director wired by hand — is still not scheduled and is still the honest description of the file.

**Closed since this section was written.** `OQ-44` made work givers register themselves; `OQ-15`/`OQ-16` moved the content tables to XML, `OQ-48` made the pawn half of that XML the only copy and `OQ-49` did the same for the world half, so content is written once everywhere; and `OQ-45` opened the per-pawn half of the read contract. None needed the feature that motivated it to be rewritten, which is the argument for doing seam work before features rather than after.

**What not to refactor.** The intent bus, the save sections, the snapshot contributors, the Def loader and the naming registry are all working seams that a feature has already gone through without touching shared code. The directors split in the HUD is the same shape and is holding. `NavGraph` and `PawnFigureDirector` are the two largest files in the project and neither is a chokepoint — they are large because the problems are, and nothing is queuing behind them.

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
| **U16 Cell inspection** ∥ | S | U15, U07 | Click a cell, read back terrain, floor, edifice, support value and region — through the snapshot, never by touching sim objects. **Its readback arrived 2026-09-17, two milestones late: the click plumbing had landed with M1 but the pane shipped the placeholder "cell readout arrives with cell inspection" until the owner reported rocks indistinguishable from grass, water silent about being water, and piles generic (ADR 0004 amendment 2). A sparse `CellDetail` row — terrain, edifice, floor stuff, support, work to clear, crossing cost in thousandths — is published for the one asked-about cell by `CellDetailContributor`, asked by a `QueryCell` intent that a paused world answers by republishing without spending a tick. Region is gone from the criteria with OQ-38. Same change: water owns its click in the picker (`SlicePicker`), and a pile resting on bare ground is selectable — the pick resolves to the block beneath it, so `SelectionDirector` looks one cell up.** |

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
| **U26 Build pipeline** | L | U25, U22 | Blueprint → materials hauled → frame → work applied → built thing, with a success roll at completion. Deconstruct refunds half. **Built 2026-09-17. Walls in wood and stone, ordered from the Build palette, played and accepted; design and test procedure `docs/design/15-building.md`. Deconstruct landed the same day on `claude/cancel-tool` and refunds half with a seeded flip on the odd unit — `docs/design/16-cancel-and-deconstruct.md`. Still not done: the success roll at completion.** |
| **U27 Materials** ∥ | M | U04, U26 | Two or three materials with `stat = base × factor + offset`. Quality tiers explicitly deferred. |
| **U45 Beds, the first furniture** | M | U26, U19 | A two-cell bed from the Build palette: rotatable ghost (R, the game's first context key — design 09 §6 case 9), one order per click, built through the pipeline, finished at a rolled quality of five tiers closing **U26's outstanding success roll**, ownable from its pane's first interactive row, and slept in at its tier's own rest rate. **Built 2026-09-17** on `claude/beds`, sim through drawing, 586 + 219 fast-tier green and a local C# 9 gate over the Presentation assembly; design, interview and departures `docs/design/20-beds.md`. Placeholder art is three scaled boxes until real two-tile art exists (the only asset in any pack is one cell wide); the room bonus is a recorded seam, and nobody has pressed Play. |
| **U28 Mining and salvage** | M | U25 | Three speeds by target: breach a slab, clear rubble, mine rock. Yields salvage into the world. **Clearing rubble landed with U29**, which is what first produces any at run time: `TerrainDef.clearable` is set by rubble alone and `CanMine` accepts it, so a heap on a floor is a Mine order although it is not a face to cut. |
| **U29 Roofs as floors** | L | U26, U10 | Building a slab creates the floor above; removing support collapses it, cascading, with rubble and fall damage. **This is the unit the whole project exists to prove** — test it hard, including the ruined-shell case where mining a wall orphans a pre-existing slab. **Built 2026-09-17**, design and the eleven owner decisions in `docs/design/17-floors-and-collapse.md`. A floor is `Building_Wall` with `slab = true` through the same pipeline; a bridge reaches as far as `S_max` and the next order is refused; the three "support is deliberately not marked dirty" omissions in `Raise`, `Demolish` and `MineCell` are closed together; a collapse drops what stood on it, leaves rubble that must be cleared, and cascades. **Fall damage is deferred, and said so**: `a-02` has the number and there is no health model to apply it to, so a colonist keeps a memory and no injury. Nobody has pressed Play on any of it. |
| **U30 Stockpiles** | M | U22, U04 | Zones **per layer** with priority and filter; stacking; haul-to-best by filter → space → priority → distance; named storage groups sharing one settings record across layers. Per `a-14-bills-stockpiles-inventory.md`. |
| **U31 Support preview** ∥ | S | U29, U15 | The build preview shows support values and highlights cells a deconstruction would orphan. Cheap now, and the thing that stops the collapse rule feeling arbitrary. |
| **U32 The ten-day run** | M | all of M3 | Five pawns, 600,000 ticks, unattended, headless, zero errors, reproducible from seed. Determinism and resume-equivalence gates pass. `docs/milestones/M3-report.md` written. |

---

## After the slice — additions the plan never scheduled

Units the slice did not ask for, added here when they land so the plan stays the index of what
exists. Each is presentation-side, self-contained in its own files, and independent of the M3
critical path.

| Unit | Size | Depends on | Done when |
|---|---|---|---|
| **U33 Environmental audio** | M | U23, U24 | The playback layer, per ADR 0010 and `d-12-audio.md`: one pooled-voice director serving the whole colony (work impacts from the stroke clock's `BlowLanded`, with distance culling, per-sound cooldown and pitch variance), camera-anchored ambience (water measured around the camera's focus from the terrain mirror, one bed per environment, layer-aware), day/night music crossfaded from the tick through `GameClock`, alerts with hysteresis off the published pawn list, five code buses in dB with a persisted settings stub (B17) and alert ducking. A generated catalogue and six synthesised placeholder clips (`AudioSetup`), so a clone without it runs silent; EditMode suites for the math, probe, clock, watcher and director, plus a PlayMode smoke test. Nothing in Sim or Hud gains a UnityEngine reference, and no sound enters the save or the hash. |

| **U42 Paving — built 2026-09-17** | M | U29 | A floor **covering** laid on ground that is already there — the thing "just build a floor" means, and not what U29 built. The mirror of the slab rule: the cell must *have* a floor and no covering yet, and there is no support check because a covering over ground is grounded by definition and can never fall. Stored as a fifth slab kind in `Floor[]`, so **no new save state and no hash change**; drawn by `FloorModule` with no catalogue row. It takes the wall's lift (`StandingOn`), not the slab's, and `WorkingLayer` must stay null for it. The three names already exist and are already published — `deckplate`, `grating`, `tile` — so no wiki content moves. **Scoped 2026-09-17 after the owner played U29 and found paving missing: `docs/design/18-paving.md`, with the measurement that within ten cells of the start only 21 of 441 cells will take a slab.** **Naming settled 2026-09-17: both say floor** — the rename to `Slab` was recommended and overruled, so the palette category and the tool descriptions carry the whole distinction and no key or label moves. Build **deck plate alone** first; grating is a see-through slab wearing the same category and wants something below worth seeing. **Paving is cosmetic until rooms are**, and that is said out loud in the doc rather than discovered. One session. **The z-fight risk was measured first and came back clean** (`PavingProbe`, 2026-09-17): the prefab's 0.10 m depth lifts the slab clear, so no mesher lift is needed. The same probe found the one thing the estimate missed — **grass grows through paving**, because the scatter is keyed off terrain and knows nothing about `Floor[]` — fixed where `EmitScatter` already refuses to draw under something solid. **Built:** `Building_DeckPlate` in wood or stone (steel was scoped and is not buildable at all — no item), five new tests, fast tier 578 + 193, EditMode 1,243. **Paving does nothing yet** and will not until rooms do. |

| **U43 The way up — built 2026-09-17** | M | U29 | A buildable **ladder**, because U29's second storeys were **decorative**: every slab measured walkable and *unreachable*, since vertical movement goes through a `Pathing.Connector` and connectors only ever came out of worldgen. `blocking = false` so the cell can be stood in, and one idempotent `RefreshLadder` called from all four places either end can change — the ladder up, the floor above it in, and either out. **No save-format change**: the connector is derived from the edifice list by `RebuildDerived`, exactly as support and the region graph are, and `NavGraph.OneCellConnectorAt` asks the graph rather than keeping a map that would be empty after a load. Six tests, reachability not edifice-existence, with the control measured unreachable first. **A hauler cannot climb a ladder** — `Connector`'s own rule, *"a hauler's bulky load ... rule a ladder out"* — so a colonist can get up and cannot carry material up, and **nothing can be built on an upper storey with a ladder alone**. |
| **U44 Stairs** | M | U43 | Two cells rising 1.5 m each, `ConnectorKind.Stair` with `AllMask`, so a **hauler** can use one. This is what makes an upper storey somewhere a colony can actually build, and U43's hauler exclusion is the argument for it. The two-cell footprint wants a placement rule a ladder did not need. |

---

## MS — The start flow (owner, 2026-09-17)

A main screen with **New game, Load, Options and Quit**; a new game that shows its seed and lets you
reroll it; three candidate colonists you can reroll individually and lock, each card showing a
portrait and a readable skill set; and a world you can enter, save, leave and load again.

**Why it is its own milestone and not part of M3.** M3 is the vertical slice and its gate is a
ten-day headless run; this is session lifecycle, persistence and a screen, and folding it in would
blur what that gate proves. It sits beside M3 rather than after it, by the owner's decision.

**Where it came from.** `B18 Game menu` has been in `10-ui-panel-catalogue.md` at milestone M0 since
the catalogue was written and is the one catalogued panel that is completely unbuilt — the Escape
unwind already wants it and substitutes the settings panel because it does not exist
(`09-ui-and-input.md` §6 case 6). The new-game and colonist-selection half was named once, in
`overnight-queue.md` under "Not scheduled and why", where the recorded blocker was *"it also needs a
start flow that does not exist"*. This is that flow.

**Two things it changes that were written down elsewhere.** Live portraits are refused by
`09-ui-and-input.md` §4.5, whose argument is about *fifty* of them in the roster bar at 15 Hz while
the world renders; three, rendered once on reroll with no world behind them, is a different case, and
§4.5 gets an explicit carve-out rather than a silent exception. And colonists start every skill at 0
experience — only passions are rolled — so there is nothing to choose between three candidates until
`U37` lands.

| Unit | Size | Depends on | Done when |
|---|---|---|---|
| **U34 The scene builds its world the way everything else does** | M | — | `OdysseyBootstrap` calls `ColonyWorld.Build(ColonyRequest)` instead of its own copy of the wiring, and holds the `SaveComponents` list. **Done 2026-09-17.** |
| **U35 The session seam** | M | U34 | World build and teardown are callable at runtime; `build → teardown → build` yields the same full hash as a fresh build, and a second world leaves no figures, materials or `PlayableGraph`s from the first. A development flag still lands straight in a world on Play, so every existing PlayMode test passes unedited. **Done 2026-09-17**, with one half deferred and said so: the hash half is proved against *three* worlds, and `buildOnPlay` keeps Play landing in a colony. The leak half **cannot be tested in the rig** — with no module catalogue the library resolves every module to a shared Unity primitive and bakes nothing, so meshes go 45 → 45, and pointing the rig at the real catalogue would make a test depend on the licensed packs. It ignores with that reason rather than passing. |
| **U36 Save format v2, and files on disk** | M | U34 | The header carries the generation recipe — map type, size, scenario, colony name, day — so the load screen lists a folder from headers alone without parsing a body. Version 1 files still load. Files at `Application.persistentDataPath/Saves`. |
| **U37 Starting skills** ∥ | S | — | A colonist spawns with rolled skill levels, deterministic on its own generation seed, saved and hashed. Moves every `Simulated` golden and no `Generated` one; re-baked deliberately with `ODYSSEY_REGOLDEN=1`. **Done 2026-09-17** (`StartingSkillsSystem`, commit `0baa37f`); this row carried no marker until 2026-09-17, which is the same staleness `U39`'s row had. |
| **U38 The menu shell (B18)** | L | U35, U36 | **Built 2026-09-17 on `claude/start-flow`; design and the owner's six decisions are `docs/design/17-start-flow.md`, and two of them overturn this row as it was written — read that file, not this cell.** It is **not an in-game menu**: the owner's ruling was *"there is already an in game menu/settings — reuse that"*, so Save, Load and Quit to main menu joined the **B17 settings panel** beside the exit row, the quit row did **not** move out of B17 as `10-ui-panel-catalogue.md` said it would, and **`SettingsDirector.Escape` gained no case at all** — the start screen is not reached by Escape. What was built is **the screen before the game**: New game, Load, Options, Quit, over a pickable full-viewport scrim, which makes `09` §6 case 5 true of something for the first time. `buildOnPlay` now defaults **false**, so Play lands there; the flag stays as the development loop and as what lets pre-existing PlayMode tests keep assuming a world. Quit to main menu tears the session down through U35. It grew from M to L because the owner asked for **Save and Load working end to end**, which brought U36's unbuilt disk half — the `Saves` folder, file naming, the header-only listing — into this unit. |
| **U39 New game and the seed** | S | U35, U36, U38 | **Done 2026-09-17** — design and the four decisions are `docs/design/17-start-flow.md` §11. A fourth screen of the same fixed box: a caption, the seed in the project's second text field, Reroll, and Start. `MenuScreen.NewGame`; the root's New game row **navigates instead of building**, which is the one behaviour U38 shipped that this changes. `SeedField` in `Odyssey.Hud` holds text, parsed seed and `Usable` in the `SavePrompt` idiom, so every rule is a fast-tier test; **a box that does not name a seed refuses to start** rather than silently building the last good number, and `MenuDirector.Start()` enforces that as well as drawing it. Entering the screen draws a fresh seed, because being dealt the same world twice reads as a reroll that does not work. The seed rides on `StartRequested(uint)` rather than being read back off the field. Size, map type and scenario stay defaults on the request, as this row always asked. **The seed logic had landed early** — `SeedEntry` in `Odyssey.Sim.Contracts`, 17 fast-tier tests — because that half needed no screen and would otherwise have been written inside a text field's callback where the fast tier could never reach it. ~~The screen itself is blocked on U38, which has not moved~~ — **that was stale when it was written**: U38 merged as PR #92 the same afternoon. Both controls were run: with the seed ignored, exactly `TheWorldIsBuiltFromTheSeedInTheBox` fails; with the guard removed, exactly `StartRefusesABoxThatNamesNoSeed` does. |
| **U40 Colonist select** | M | U37, U39 | **Done 2026-09-17** — design, the owner's three decisions and what building it changed are `docs/design/18-colonist-select.md`. Three cards on a screen of their own (`MenuScreen.Colonists`), reached by **Next** from the seed: the fixed box stays fixed, which is why it is not three more rows under the seed. **A lock rather than a per-slot reroll** — the two are the same control said twice, so Reroll is one row and you keep the ones you like. Skills do read through the `PawnAspect` seam and `Sim.Contracts` did not change; the seed a colonist was rolled from goes out the same way, which is what lets a name follow the roll. **The one real change underneath is `Pawn.RollSeed`**, defaulting to the world seed so no colony nobody chose moved, saved in a section of its own and hashed — all six goldens re-baked, and this time `Generated` moved too, because a seed is assigned at placement rather than on the first tick. A new game starts with **exactly the three chosen** (owner's decision); the headless ten-day gate keeps its own five-colonist scenario, so M3 is untouched. |
| **~~U41 Portraits~~ U41 Flat avatars** | M | U40 | **Done 2026-09-18 — design and the owner's seven decisions are `docs/design/20-avatars.md`, and this row as written is not what was built.** The owner chose **composed flat avatars** over live 3D portraits, because the same avatar has to serve the roster bar as well as the select screen: so there are no render textures, nothing to release, and the test this row asked for has nothing to count. **`09` §4.5 is therefore not amended at all** — it already *prescribes* composed flat avatars for M2–M7 and refused only the live kind, so choosing what it chose needs no exception written into it. That is the cheapest outcome available and it was not the one planned. Four sites, not three: the roster card (which had a People-coloured tile with the name's first letter on it), the inspect header (an outlined square on `ui.pawn.colonist`, a key `icon-map.csv` records as a gap — *"no sheet contains a human figure"*), and both halves of the world-setup page. `ColonistFace` in `Odyssey.Hud` is the recipe and `AvatarGlyph` draws it as four `Painter2D` layers; the colours come from `ColonistAppearance.Of`, **the same call the 3D figure is painted from**, which is why that class and its book moved down into `Odyssey.Hud` (eleven tests into the fast tier with them). The face follows `RollSeed` like the name, age and trade, so a reroll changes the person; `randomCastEachSession` is a development override now. Nothing saved, nothing hashed, **no golden moved**. `Logs/avatars.png` and `Logs/setup-page.png` are the pictures, and the sheet earned itself immediately: three shoulder widths that were one, and two crowns that were sideburns. **Then the owner played it — *"the colonists look nothing like their profile picture"* — and they were right**, so `PortraitStudio` renders the real character once per appearance and caches it (§10 of the design). **This row's original ask was closer to right than the unit that replaced it**: the leak test it wanted, which the flat-avatar unit said had nothing to count, counts **one** render texture for the whole game. §4.5 still needed no amendment, because it refused *live* portraits and named a cached atlas as the graduation path. The drawn avatar stays as the no-packs fallback. `Logs/portraits.png`. |

**Gate:** both tiers green, a world started from the menu survives save → quit-to-menu → load →
resume with matching full hashes headless, and `docs/milestones/MS-report.md` written.

---

## WS — Skill and condition become a rate (owner, 2026-09-17)

A colonist's standing changes how fast they work and how fast they walk. **Design:
`docs/design/17-rates-and-stats.md` — read it before starting any unit here**, and do not take the
reference's curves out of it without reading §3b.

**These units are `WS1`–`WS4`, renumbered 2026-09-18 — they were written as `U42`–`U45` and those
four numbers were already taken.** Paving, the ladder, stairs and beds hold `U42`–`U45` in the M3
table above, all but stairs built, so "U42 is done" was true and false at the same time depending on
which table you were reading. The built units keep their numbers because the journal records them
that way and the journal is not rewritten; these, being unbuilt, are the ones that moved. Entries in
`docs/journal.md` dated on or before 2026-09-17 still call them `U42`–`U45`.

**Where it came from.** Two owner questions in one conversation: chopping should be faster for a
skilled colonist and should visibly swing faster, with experience rising as they do it; and move
speed should vary between characters and depend on their condition and health. The first is
**half built** — experience is complete and correct (OQ-14) and **nothing reads a level back
out**; the second **cannot be built as the numbers stand**, because `movePerTick` is `1` against a
cell cost of `100` and the only speeds expressible are 1.5, 3.0 and 4.5 m/s.

**Why it is one milestone and not two features.** Both are the same missing thing — a per-pawn,
per-activity rate in thousandths, multiplied into an accumulator — and both need the same
one-off change to make integers work at all: **the accumulator scales by 1,000 and no authored
content number moves.** Doing them separately would pay that cost twice and risk two answers to
the same question.

**Why it is beside M3 and not inside it.** M3's gate is a ten-day headless run, and this changes
how fast every colonist does everything, so it moves that run's economy. It is sequenced **after**
M3's gate is taken, or M3's gate measures a moving target. `WS1` alone is safe to land at any time,
because its done criterion is that nothing changes.

**The standing risk, named once.** Every golden hash moves at `WS2` and again at `WS3`. That is
expected and deliberate; it is a re-bake with `ODYSSEY_REGOLDEN=1`, the reason written into
`Golden.cs` the way OQ-50 and the edifice-save change established. **`WS1` is what makes those
re-bakes readable**: it proves the mechanism moves nothing, so everything that moves afterwards is
tuning.

| Unit | Size | Depends on | Done when |
|---|---|---|---|
| **WS1 The rate seam** | M | — | `Pawn.WorkRatePerMille(workType)`, `Pawn.MoveRatePerMille()` and `Pawn.ConditionPerMille()` exist, virtual, and all return a constant 1,000. `_work[cell]` (designations and construction alike) and `Pawn.MoveProgress` count thousandths; every comparison reads `cost × 1,000`; **no authored number in `Terrain.xml`, `Jobs.xml`, `ConstructionContent` or `MoveCost` changes**, so both content fingerprints hold. **The scale stops at the contract** (design §2bb, audited against the code 2026-09-17): `CellDetail.WorkToClear` stays in ticks *because it is a `ushort` and 2,400 × 1,000 does not fit*; `SiteView.WorkDone`/`WorkTotal` stay in ticks or every "about 12s left" in the interface is multiplied by a thousand; `DesignationGrid.Fraction()` gains the `× 1,000` on its denominator or every progress bar fills a thousand times too fast; and `PawnRegistry`'s `movePercent = MoveProgress × 100 / MoveStepCost` is a **ratio**, so `MoveStepCost` scales with it or every figure teleports. **The done criterion is that nothing moves:** every golden hash, every path checksum, the one-day run and every HUD readout test identical before and after, and the save reads a v1/v2 file by scaling the old value. A control sets a rate to 500 and requires a job to take twice as long. |
| **WS2 Work speed from skill** ∥ | M | WS1 | `rate = base + slope × level`, floored, as three Def fields per work type, anchored per design §3b (eight proposed integers, INVENTED — the owner's call; hauling is flat 1.0, which is both the reference's answer and `15-skills.md` §6's). Felling, mining, building and deconstruction all pay at the pawn's rate; a level-20 colonist finishes a fixed cell in a ratio of ticks that equals the curve exactly; two colonists of different skill sharing one cell sum their contributions in one unit. `odyssey.pawn.rate.work` published as a `PawnAspect`, so `Sim.Contracts` does not change. **Presentation scales the stroke clock by it** (`PawnFigureDirector.cs:924`, one line) so a fast worker visibly swings faster. Goldens re-baked deliberately; the ten-day soak re-run on three seeds and **its economy compared against the previous run, not merely checked for errors**. |
| **WS3 Move speed, and condition on both rates** | M | WS1, WS2 | An innate factor rolled once from `(world seed, pawn id)` on its own `PawnPurpose`, **capped at ±15%** because the drawn walk cycle blends the run clip in above ~2 m/s (design §4b). **One shared `ConditionPerMille()` multiplying the work rate as well as the move rate** (owner, 2026-09-17: *"if exhausted, starving etc, all has an effect"*) — a single consciousness-like scalar that starvation offsets by −100/−200/−300, floored at 700 and **ceilinged at 1,000**, so neither rate ever learns that hunger exists and M4's capacities substitute for it rather than rewriting it. **Exhaustion is a collapse, not a number** (design §4c, from follow-up research: tiredness slows nothing in the reference, it drops you where you stand) — at zero rest a colonist sleeps where it is, with the control that a merely tired one still walks to a bed. `odyssey.pawn.rate.move` published as an aspect. **Terrain cost stays in the step cost and is not touched** (§4g) — a test asserts the planner's chosen route is unchanged by a pawn's rate. **The soak is the done criterion, not a formality:** three seeds, ten days, against a run with the condition factors disabled, both economies recorded, proving no starvation spiral. |
| **WS4 Running** | S | WS3 | **Held, not scheduled (owner, 2026-09-17: "not sure yet").** The capability is a multiplier on a rate `WS3` already produces, and the gait blend already turns it into a run above ~2 m/s with no new clip and no new state — so nothing is lost by waiting for a reason to run. **The standing rule while it is held: do not invent an urgency model.** When it is taken: a PlayMode test that the run clip's weight rises, with the control that at the ordinary rate it does not; nothing in the save or the hash. |

**Gate:** both tiers green; `WS1`'s "nothing moved" control passing on the same commit as `WS2`'s
re-bake, so the diff shows which change moved what; the ten-day run green on three seeds with its
economy written down beside the previous one; and the owner has played it and judged the anchor.

**Not in scope, and §6 of the design says why for each:** quality, yield, the passion mood buff,
traits, health capacities, carried load, and the skill-table reshuffle `15-skills.md` §6 leaves
open.

---

## HT — Hardening (baseline audit, 2026-09-19)

The audit (`docs/audit/2026-09-19-baseline.md`) measured the tick at the scale target under edits
for the first time, read the seven largest files, and ranked what would hurt when the board grows.
These are the units that came out of it, **in the order they should be taken**. HT1, HT2 and HT3
share no files and can run in parallel on separate branches. None starts until the owner approves
the audit's §8.

**The one number to hold in mind.** A tick that mines one cell at 250 × 250 × 40 costs **1.19 ms**
against **0.065 ms** at rest, all of it `NavGraph.Rebuild` recomputing every district (24,000
regions) for a change in one block. At speed 3 that is 3.6 ms of the frame before rendering.

| Unit | Size | Depends on | Done when |
|---|---|---|---|
| **HT1 The navigation rebuild is local** | M | — | First the measurement: `NavGraph.Rebuild` reports its segments — flood, portals, adjacency, districts, estimate — through the existing `PhaseSink`, and a busy arm of `TickBenchmarkTests` (one mined cell a tick, 50 colonists, scale target) prints them. Then districts are recomputed only for the components the affected zones touch, and district ids stop being renumbered from zero (nothing compares them for order; they are not saved or hashed). **Done when the edit-tick arm measures under 0.2 ms at the scale target** on the machine that measured 1.19, with every path checksum, every golden and `NavGraphStatisticsTests`' region counts unchanged, and the incremental result tested against a full rebuild as its oracle over random edit sequences — the support solver's pattern. Design section in `docs/design/05-ai-and-jobs.md` beside §6. |
| **HT2 Hygiene** ∥ | S | — | One PR. `tools/dotnet/Directory.Build.props` turns on the .NET analysers at `latest` with warnings as errors for Sim, Contracts and Hud, and an `.editorconfig` at the root carries the style rules; unused packages and built-in modules leave `Packages/manifest.json` (`ai.navigation`, `analytics`, `collab-proxy`, `multiplayer.center`, `purchasing`, `timeline`, `visualscripting`, `xr.legacyinputhelpers`, `2d.sprite`, `2d.tilemap`; modules `physics2d`, `cloth`, `vehicles`, `wind`, `vr`, `xr`, `terrain`, `terrainphysics` — `ugui` only after a grep proves nothing needs it); the ~40 `*Check` and `*Probe` tools move under `Assets/Editor/Odyssey/Probes/` with their own asmdef gated on `ODYSSEY_PROBES`, and `scripts/unity.sh shot` sets it; `docs/setup/local-dev.md` §1 already states the floor (3.11 or newer), so this unit only adds `python3 --version` to the setup check. Both tiers green, **and a player build smoke run**, because removing packages is build-shaped. |
| **HT3 The event seam** ∥ | M | — | A sim-side `EventLog` any system appends to (`tick, kind, cell, subject, value`), drained into the snapshot by a contributor as `EventView` rows keyed by a name the emitter mints — the `PawnAspect` shape, so `Sim.Contracts` gains one struct and no enum. **Not hashed, not saved.** ADR 0004 amended. The alert chime and `AlertModel` read events instead of inferring from the pawn list, with the control that the starvation chime fires from the event and does not fire without it. Design: `docs/design/05-ai-and-jobs.md` gains a §7, and the M6 storyteller row in `03-systems-catalogue.md` names it as the channel incidents will write into. |
| **HT4 The Burst decision** | S | — | A decision, not a build: ADR 0005 amended to say `Odyssey.Sim` stays UnityEngine-free, hot paths are declared as kernel interfaces in the Sim with the managed implementation beside them, and `Odyssey.Sim.Native` (referencing Burst, Collections, Mathematics) implements the same interfaces and is chosen by the composition root; a Unity-tier test asserts both kernels hash identically on the D1 workload. The assembly itself is created by the first unit that needs a kernel — on today's numbers M4's grid propagation, not pathfinding. The queue's *needs the owner* row closes. |
| **HT5 The monolith cuts** | M each | HT2 | One PR per file, behaviour-preserving, in the audit's order (§4d-i): **(a)** the bootstrap's overlay drawing → `WorldOverlays` and the renderer's fourteen primitives → `OverlayDrawer` together, plus the three per-frame string allocations fixed by comparing inputs; **(b)** `PawnFigureDirector.Pose` re-sectioned into its nine named steps, and the five per-figure aspect scans replaced by one walk of `PawnAspects` per `Sync`; **(c)** `HudShell`'s three cadence allocations removed, then `HudShell.Start` → `StartScreenView` with forwarding properties for the eleven test files; **(d)** `PlayScene`'s catalogue table and builders → `ModuleCatalogueBuilder`; **(e)** `ConstructionGrid`'s bed ownership → `BedRegistry`. Each: both tiers green, every golden identical, the screenshot probes that touch the file re-shot and identical, and the design doc that owns the line gains a line saying what moved where. |
| **HT6 The busy-colony arm** | S | HT1 | A third arm of `TickBenchmarkTests`: fifty colonists with standing mine, fell and haul orders for 60,000 ticks, printing think cost per pawn, hauls per day and nav rebuilds; its economy is the first `soak-runs.md` entry since WS2/WS3 re-baked the goldens. **The number decides whether per-region item listers are built**; if it does, they are one class beside `ColonyItems` keyed by `NavGraph.RegionOfCell`. |
| **HT7 The slice channel** | S | — | `GridMirrorContributor` writes the per-layer slice only when a subscriber has asked, the way `CellDetail` is asked for; the snapshot tests are unchanged and a control asserts the channel is empty when nobody asks. |
| **HT8 Frame time at the scale target** | S | Unity | A `FrameTimeTests` arm at 250 × 250 × 40 with fifty figures and three hundred standing orders, plus a draw-call count with `SubmitToGpu=false`. Its numbers decide the two behaviour-changing steps HT5 leaves optional — batching the per-order marks, and frustum culling in `ChunkRenderer.Render` — and either explain or retire the city's 0.88 → 1.56 ms note in `CLAUDE.md`. |
| **HT9 The input harness** | M | Unity | OQ-40: a PlayMode test presses a mouse button and the game sees it, proven by a control that fails when the input is withheld; `InputHarnessTests` and `FloorToolClickTests` un-ignore together. Three silent failures on this line are the argument for its size. |

**Gate:** both tiers green after each unit; HT1's number recorded in ADR 0005 beside OQ-19's; the
playtest queue gains no rows from HT1–HT4 and HT6–HT8 (nothing player-visible) and one per HT5 cut
(a re-shot probe is a picture, not a play).

**What this track does not touch.** Stairs (`U44`) and the ten-day gate close M3 exactly as
planned; WS4 stays held; every owner deferral in the section below stands.

---

## Deferred: the rest of the look (recorded 2026-09-16, owner deferred)

Pull request #50 landed the day/night cycle, the golden hour under it, the hill wood and the B17
settings stub. **The owner deferred the remainder rather than dropping it**, so it is written down
here in the order it should be picked up, with what each depends on.

The governing fact for all of it: **nobody has pressed Play.** Every judgement in #50, and every
number in it, comes from contact sheets shot by editor tools and from `FrameTimeTests`. Contact
sheets cannot show whether the light steps visibly at speed 3, whether night is genuinely playable
rather than merely pretty, whether the panel sits correctly over a live scene, or whether a
seventeen-minute day feels too fast. Those are cheap to find by playing and impossible to find any
other way, so the first item is not code.

| Item | Size | Depends on | Done when |
|---|---|---|---|
| **Play it** | S | — | The owner has run `Play.unity` through a full day at each speed and said what is wrong. Everything below may be re-tuned by what that finds, which is why it is first. |
| **The Low quality tier** | M | — | The interview committed to a tier that drops the expensive effects for the 2022 laptop, and nothing tiers today. A URP asset per quality level plus a generated `VolumeProfile` per tier, assigned from `QualitySettings.GetQualityLevel()`; Low differs in exactly three ways (no depth of field, quarter-resolution bloom, FXAA) and keeps an identical grade. **The trap is recorded in `d-12`: use `volume.profile`, never `sharedProfile`, or a runtime override is written to the asset on disk.** Independent of the verdict above, and the only answer to the budget question — every figure on record is from an RTX 5070 Ti at 640 × 480, where post costs a fraction of what it does at 1080p. |
| **The new levers in the settings panel** | S | Low tier | The panel exists precisely to compare looks and still offers only the four decoration switches it shipped with. Bloom, the grade, anti-aliasing and the day cycle all want rows, and the tier wants a preset control. Each new row is a `ui.settings.*` key in `icon-keys.csv` with the wiki and registry regenerated in the same commit. |
| **Sun shafts** | L | The framing experiment | **The case for these improved when the cycle landed and the plan should say why.** `d-13` blocked them because at a 48° pitch a fixed overhead sun sits about 120° off the view direction — behind the camera, where a radial blur has nothing to radiate from. The cycle now sweeps the sun from roughly east to west and holds it low at both ends of the day, so there are hours where it is plainly in frame. The experiment is unchanged and still comes first: two sliders in `Play.unity`, and if no framing works the fallback is billboard shafts, which is a different question. Design committed in `d-13`: half-res R8 mask from depth only, three 12-tap blur passes, full-res Screen composite at `AfterRenderingSkybox` so the glow sits under our outline. |
| **Tilt-shift** | M | — | `d-12` found URP's cheap depth of field blurs only the far field and cannot make a band at all, and Bokeh needs dishonest optics and a real pass. A fullscreen pass blurring by **screen Y** is exact, needs no depth texture, costs the same whatever the scene holds and tiers away trivially — and appears to be what the reference game does. Open question carried from the interview: whether the band should follow the active layer, which would make it a slice cue as well as a look. |
| **Marsh reads as a sandy bank** | S | — | Pre-dates this work (ADR 0009) and is still open: either a greener tint or a different name. An owner call, not a technical one. |

Two smaller corrections worth doing whenever the files are next open: `CLAUDE.md` still carries a
stale line claiming `AxeBladeRoll` is 270 when the code says 0 and is right, and the concept
renders' cyan-emissive night bar in `d-03-rendering.md` now differs from the shipped look, which
the owner should reconcile rather than either document quietly winning.

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
