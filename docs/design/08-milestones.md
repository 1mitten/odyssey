# 08 — Milestones and definition of done

The roadmap from brief §7, with a precise definition of done per milestone. M0–M3 are specified to be executed; M4–M8 carry their scope and their gate, and will be planned properly when they are next.

**Every milestone ends the same way** (brief §7): tests pass, a headless one-day simulation runs with no errors, `docs/milestones/Mx-report.md` is written, and work stops for review.

## The standing gate

A milestone is not done until all five hold:

1. `scripts/unity.sh test editmode` and `test playmode` pass, with no new warnings in the Sim assembly.
2. **Determinism:** the same seed produces the same world-state hash after N ticks, across two separate processes.
3. **Round trip:** save at tick N, load, run to N+K, and the state hash matches an uninterrupted run to N+K.
4. A headless one-day run (60,000 ticks) completes with zero errors and zero exceptions in the log.
5. The milestone report is written, and a clone **without** the gitignored Synty packs still builds and passes every Sim test.

Rule 5 is the licensed-asset boundary made executable: if a Sim test ever needs `Assets/Synty/`, the dependency has gone the wrong way.

---

## M0 — Foundations

**Goal:** the machinery everything else stands on, with nothing gameplay-shaped in it.

Already done in Phase 0 and carried in: the Unity 6000.3.24f1 URP project at the repository root, five Synty packs imported under the gitignored `Assets/Synty/`, `scripts/unity.sh` as the headless command surface, and the Unity MCP server connected.

Remaining scope:

| Unit | Done when |
|---|---|
| Assembly definitions | `Odyssey.Sim`, `Odyssey.Sim.Contracts`, `Odyssey.Ui.Core`, `Odyssey.Presentation`, `Odyssey.Editor`, three test assemblies. A test asserts Sim and Sim.Contracts reference no UnityEngine type. |
| Fixed-tick loop | Tick groups (every / rare / long) with hash-offset phase spreading; speeds as tick-rate multipliers; pause. Driven headless with no scene. |
| Determinism harness | A seeded world, an FNV-1a state hash over the sim state, and a test asserting two processes agree after 10,000 ticks. |
| Def loader | Parse, inherit, patch, deserialise, resolve references, validate — failing loudly with file and line. Fixture Defs loaded from a temp folder in tests. Shape per `d-07-data-pipeline.md`. |
| Save/load skeleton | Whole-state write and read with the round-trip test from the standing gate. Shape per `d-06-save-load.md`. |
| Composition root | One place that builds a world from Defs plus a seed and hands back a tickable object. No manager singletons. |
| CI | The workflow from `d-08-ci-tooling.md`, gating merges, running EditMode tests on every push. |
| The sim→UI seam | `WorldViewStore` double buffer and `IntentBus`, per `docs/design/ui-plan-reconciliation.md`, with the view build inside the tick budget the benchmark measured. |

**Deliberately absent:** pawns, cells with meaning, rendering. M0 can tick an empty world deterministically, save it, load it and prove it. That is the whole point.

## M1 — World

**Goal:** a ruined city exists, is visible, and can be inspected.

| Unit | Done when |
|---|---|
| Cell grid | SoA arrays at the fixed index convention (`02-world-and-layers.md` §1–2), 250 × 250 × 40 allocated and ticked within budget. |
| Chunks | 25 × 25 per-layer chunk grid with dirty tracking. |
| Worldgen | The ten passes: street grid, plots, stamped shells, damage, intactness, strata, salvage, taps, vaults, start. Ends with a full support solve that asserts consistency. |
| Support solver | Full-map and incremental paths, with the incremental path tested against the full solve as its oracle. |
| Rendering | Instanced Synty modules per chunk, the cut-away at the active layer, layers above ghosted and **non-interactive**. Frame budget from the D3 performance pass. |
| Camera and layer navigation | Slice control, jump to layer. UI surface owned by the UI session; this milestone provides `SliceDirector` state and the render behaviour. |
| Cell inspection | Click a cell, see its terrain, floor, edifice, support value and region. |

**Gate addition:** generating a full 250 × 250 × 40 map completes within a stated time budget and the support assertion passes for every shipped template.

## M2 — Pawns

**Goal:** colonists live in the ruin, move through it in three dimensions, and look right doing it.

| Unit | Done when |
|---|---|
| Region graph | Regions per layer with portal edges for stairs, ladders and holes; incremental rebuild on world edits, tested against a full rebuild. Per `d-04-pathfinding.md`. |
| Pathfinding | Layer-aware A-star over the region graph with deterministic tie-breaks; per-agent replan budget; paths invalidated by edits. |
| Needs and mood | Food, rest, minimal joy on the 150-tick cadence; mood from a small thought set with drift; one mental-break behaviour. |
| Skills | 0–20 with experience gain from work and a passion multiplier. |
| Jobs, first pass | Think tree, work givers, job drivers and toils, reservations. Haul and the needs-driven eat and sleep jobs. |
| Characters | Synty rig with the animation set from `e-02-characters-animation.md`: idle and walk from Base Locomotion, the six missing clips retargeted from Mixamo into `Assets/Art/` (never into `Assets/Synty/`). |
| Presentation | Pawns render, animate and are visible only on the drawn layers. |

**Milestone demo:** three pawns live in a ruined shell — they walk upstairs, sleep, eat from a store and haul items, unattended, for a day.

## M3 — Build and dig → **the vertical slice is complete**

| Unit | Done when |
|---|---|
| Designations | Mark for mine, deconstruct, build; cancel; forbid and allow. |
| Build pipeline | Blueprint → materials hauled → frame → work → built thing, with a completion roll. Deconstruct refunds half. |
| Materials | Two or three materials with the `base × factor + offset` stat rule. Quality tiers deferred. |
| Mining and salvage | Breach a slab, clear rubble, mine rock — three speeds by target, yielding salvage. |
| Roofs as floors | Build a slab; it becomes the floor above. Support and cascading collapse live, with rubble left behind. |
| Stockpiles | Zones with priority and a filter, stacking, haul-to-best-stockpile. Per `a-14-bills-stockpiles-inventory.md`. |
| Support preview | The build preview shows support values and the cells a deconstruction would orphan. |

### The vertical slice, defined precisely

The brief's bar (§2) made testable:

- **Map:** one generated ruined-city map, roughly 60 × 60 cells, five layers (one service layer below, street level, three storeys up).
- **Colonists:** five, generated from Defs with a fixed seed.
- **Systems live:** needs (food, rest, joy), mood and one break, skills, the job pipeline with priorities and reservations, layer-aware pathing through stairs and ladders, hauling, a stockpile, mining and salvage, the full build pipeline, roofs-as-floors with support and collapse.
- **Systems stubbed, by name:** health (alive / downed / dead), temperature (uniform), light (uniform), power (none), plants and cooking (a starting food store), animals (none), combat (none), storyteller (none), research and trade (none).
- **Done when:** five pawns survive **ten in-game days** (600,000 ticks) unattended in a headless run with **zero errors**, the determinism and round-trip gates pass, and the ten-day run is reproducible from its seed.

That last clause is the real test. Ten unattended days is long enough that a reservation leak, a pathfinding dead end, a needs-decay sign error or a collapse cascade bug will surface — which is precisely why the brief chose it.

---

## M4 — Environment

Rooms per layer with enclosure and volume, temperature with flow between vertically adjacent rooms, light including shafts through broken slabs, weather, seasons, fire. **Gate addition:** a fire started on one layer behaves plausibly and does not cross an intact slab.

## M5 — Sustenance

Growing zones and hydroponics, plants with light and fertility requirements, cooking bills, nutrition and spoilage, animals and taming. **Known blocker:** no animal art exists in any owned pack — either a pack purchase or primitive placeholders, the owner's call, recorded in `03-systems-catalogue.md`.

## M6 — Danger

Health with body parts and capacities, 3D line of sight with floors as occluders and a capped vertical range, cover, the first raid, storyteller v1 with threat points. **Gate addition:** a raid that arrives through the service stratum paths correctly to the colony.

## M7 — Depth

Research as reverse-engineered salvage, power nets connecting vertically, trade, factions, the world map, and the social and trait depth deferred from M2.

## M8 — Openness

Modding API and extension points chosen from what Lane A showed modders reach for, a content pass, a performance pass, and the decision point: graduate or shelve.

---

## Sequencing note

M0 and M1 have a hard dependency on the architecture decision (ADR 0005) and nothing else. M2 depends on M1 for the world and on `d-04-pathfinding.md` for its region graph. M3 depends on M2 for jobs. The three slice milestones are therefore strictly sequential, and the parallelism available inside each one is at the unit level — which is where subagents are worth using, and where the plan file (`docs/plans/vertical-slice.md`) assigns them.
