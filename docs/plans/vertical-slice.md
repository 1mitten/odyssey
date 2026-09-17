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
| `Sim/Pawns/PawnContent.cs` | every item, job and work type was a C# constant and a table in code | **done for pawns** (`OQ-15`, `OQ-16`, then `OQ-48` on 2026-09-17). `PawnContent.Core()` and its 163 lines of hand-written tables are gone, so the XML under `Assets/Odyssey/Defs/Core/Pawns` is the only copy and a new item is written once. Every caller goes through `ContentPack.Pawns()`, which finds the pack by walking up to the repository root and caches the parse; a fingerprint guards the content in place of the old field-for-field oracle. **The world half is still open** — `CoreContent.Terrain` and `NaturalContent.Terrain` are in-code tables mirrored by `Defs/Core/World/*.xml` that nothing but the oracle test reads, and the `TODO(content)` in `WorldGenDefs.cs:254` names the spot |
| `Sim/Pawns/JobSystem.cs` | `DefaultGivers()` was a hardcoded array, so a new job was an edit to a shared file | **done** (`OQ-44`). `WorkGiverRegistry` finds every concrete `WorkGiver` in the simulation assembly, so a giver joins the scan by existing; `SimWorldBuilder.AddWorkGiver` takes the ones declared outside it |
| `Sim.Contracts/Views.cs` | a new thing a pawn can do needed a new field on the published frame | **done** (`OQ-45`). A sparse `PawnAspect` row, `(PawnId, key, int)`, keyed by a name the feature mints for itself, published through the contributor seam that already existed for the frame. No new registration mechanism and no enum to edit. ADR 0004 amended 2026-09-17 |
| `Presentation/Bootstrap/OdysseyBootstrap.cs` | the composition root wires every system by hand | **open, and the one chokepoint with no queue row.** It already delegates the colony to `ColonyComposition.AddColony`; presentation wants the same treatment. 851 lines on 2026-09-17 |
| `Presentation/Rendering/ChunkMesher.cs` | new terrain needs new meshing, and the file is on the queue's do-not-touch list | a mesh contributor per stuff kind, registered rather than switched on. `OQ-46`, open |
| `docs/design/icon-keys.csv` | a new name | **correct as it is.** This is the one seam that worked: a name added to the CSV reaches the screen with no code change |

The last row is still the point, and closing two of the others has not changed it. One of the six was a designed seam and cost nothing; the other five were files that grew a new branch of a switch. The work below is what is left of making them look like the last one.

**The order to do it in, cheapest and most load-bearing first.** Three of the original four are done, so what remains is renumbered.

1. **The world tables, the last of content to Defs** (`OQ-49`). The pawn half is done (`OQ-48`): `PawnContent.Core()` is deleted and a new item is written once. Terrain and ores are where the pawn tables were a day ago — loaded from `Defs/Core/World/*.xml` by a test, mirrored by `CoreContent.Terrain` and `NaturalContent.Terrain` in code, and it is the in-code arrays the generators read. Smaller than the pawn half and subtler: **a terrain index sits in every cell of every save and every hash**, so the risk is the *order* rather than the values, and `WorldContent.TerrainOrder` already exists to pin it.
2. **Mesh contributors** (`OQ-46`) last, because it is the largest and only the terrain features need it.

**Unscheduled, and worth a row when presentation next grows.** `OdysseyBootstrap.cs` is the fifth chokepoint and the only one nothing is queued against. It is not urgent while presentation is stable; it will be the moment two features want to add a director in the same week.

**Closed since this section was written.** `OQ-44` made work givers register themselves, `OQ-15`/`OQ-16` moved the content tables to XML and `OQ-48` made the pawn half of that XML the only copy, and `OQ-45` opened the per-pawn half of the read contract. None needed the feature that motivated it to be rewritten, which is the argument for doing seam work before features rather than after.

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
| **U26 Build pipeline** | L | U25, U22 | Blueprint → materials hauled → frame → work applied → built thing, with a success roll at completion. Deconstruct refunds half. **First slice built 2026-09-17 on `claude/build-pipeline` (PR #63): walls, wood and stone, ordered from the Build palette. Design and the test procedure are `docs/design/15-building.md`. Not done: the success roll, deconstruct, and the owner reports it still does not build in the running game — read the rejection log first (§6).** |
| **U27 Materials** ∥ | M | U04, U26 | Two or three materials with `stat = base × factor + offset`. Quality tiers explicitly deferred. |
| **U28 Mining and salvage** | M | U25 | Three speeds by target: breach a slab, clear rubble, mine rock. Yields salvage into the world. |
| **U29 Roofs as floors** | L | U26, U10 | Building a slab creates the floor above; removing support collapses it, cascading, with rubble and fall damage. **This is the unit the whole project exists to prove** — test it hard, including the ruined-shell case where mining a wall orphans a pre-existing slab. |
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
