# Plan — the vertical slice (M0 → M3)

**Target:** five pawns survive ten in-game days (600,000 ticks) unattended in a headless run, with zero errors, reproducible from a seed. That is the whole definition of done; everything below serves it.

This file is written to be executed by a session with no other context. Read `CLAUDE.md`, then `docs/design/00-vision.md`, `02-world-and-layers.md` and `03-systems-catalogue.md`, then this. Do not start a unit whose dependencies are unmet.

**Before any of this begins:** Phase 3 ends with a hard stop for the owner's approval (brief §6). No unit below is started until that approval is given.

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
| **U26 Build pipeline** | L | U25, U22 | Blueprint → materials hauled → frame → work applied → built thing, with a success roll at completion. Deconstruct refunds half. **Built 2026-09-17. Walls in wood and stone, ordered from the Build palette, played and accepted; design and test procedure `docs/design/15-building.md`. Deconstruct landed the same day on `claude/cancel-tool` and refunds half with a seeded flip on the odd unit — `docs/design/16-cancel-and-deconstruct.md`. Still not done: the success roll at completion.** |
| **U27 Materials** ∥ | M | U04, U26 | Two or three materials with `stat = base × factor + offset`. Quality tiers explicitly deferred. |
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
| **U37 Starting skills** ∥ | S | — | A colonist spawns with rolled skill levels, deterministic on its own generation seed, saved and hashed. Moves every `Simulated` golden and no `Generated` one; re-baked deliberately with `ODYSSEY_REGOLDEN=1`. |
| **U38 The menu shell (B18)** | M | U35 | New game, Load, Save, Options, Quit. Escape unwinds into it through the one rule in `SettingsDirector.Escape` rather than a second one. The project's first true modal. Options reuses the B17 panel. |
| **U39 New game and the seed** | S | U35, U36, U38 | A seed you can read, type and reroll. The only exposed knob: size, map type and scenario stay defaults on the request so they are tunable later without new interface. **The seed logic landed early, 2026-09-17** — `SeedEntry` in `Odyssey.Sim.Contracts` draws, rerolls, formats and parses a seed, with 17 fast-tier tests, because that half needs no screen and would otherwise be written inside a text field's callback where the fast tier could never reach it. **The screen itself is blocked on U38**, which has not moved — `HudShell.Bar.cs` still says the B18 menu "does not exist yet" and no menu panel exists, so there is nowhere to attach it. (U36 and U37 were both open when that work started and both landed the same afternoon; check the code, not this table.) No standalone menu was invented to route around it. |
| **U40 Colonist select** | M | U37, U39 | Three cards, per-slot reroll and lock. Skills read through the existing `PawnAspect` seam, so `Sim.Contracts` does not change. |
| **U41 Portraits** | M | U40 | Three 3D portraits rendered once per roll and cached, released on leaving the screen, under a test that fails if more than a fixed number of render textures are alive at once. `09` §4.5 amended in the same commit. |

**Gate:** both tiers green, a world started from the menu survives save → quit-to-menu → load →
resume with matching full hashes headless, and `docs/milestones/MS-report.md` written.

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
