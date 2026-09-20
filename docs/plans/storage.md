# Storage: zones, units and the haul order

*Planned 2026-09-19 with the owner, on `claude/storage-stockpile-system-k1yg51`, **PR #137**.
Phase: plan. Nothing here is built. Interview answers are in §Decisions; the reasoning that
did not fit belongs in `docs/design/26-storage.md`, written as S1 starts.*

*__Revised 2026-09-20 after merging `main`__, which had moved on to carry the baseline audit
(PR #136). Two corrections, both material and both found by the merge rather than by the plan:
the unit numbers this asked for are taken, and **most of the zone machinery S1 proposed to build
already exists** on the unmerged growing-zones branch. See §What `main` changed under this plan.*

## Context

The colony has a working storage **simulation** and no storage **game**. `Stockpile`
(`Assets/Odyssey/Sim/Pawns/ColonyItems.cs:37`) already carries a priority, a cell set and a
per-item-def `Allow[]`, and `HaulWorkGiver` (`Assets/Odyssey/Sim/Pawns/JobSystem.cs:678`) already
implements the destination rule from `docs/research/a-14-bills-stockpiles-inventory.md` §3 —
*accepts → has space → highest priority → nearest* — including lower→higher re-stow, with
reachability answered before pathing.

What does not exist is any way for a player to touch it. There is no intent, no tool, no panel,
and **nothing draws a stockpile at all**: the starting zone is placed by `ColonyScenario.cs:660`
and is invisible on screen. `U30 Stockpiles` has sat in `docs/plans/vertical-slice.md:129` unbuilt
because the simulation half arrived early under other units.

Three faults were found by reading and are fixed on the way past:

| Fault | Where | Why it matters |
|---|---|---|
| Stockpiles and beds are **saved but not hashed** | `ColonyItems.cs:420` | Breaks the project's own "hashed therefore saved" pairing (`JobSystem.cs:237`). Player-authored zones *must* be in the hash or a desync is invisible. |
| **Overlapping zones silently overwrite** | `_stockpileAtCell`, `ColonyItems.cs:145` | Last `AddStockpile` wins per cell, no rejection. The "drag extends" gesture removes this by construction. |
| **`Allow[]` length is unguarded on load** | `ColonyItems.cs:507` | A save from an older def table reads short and `Accepts` silently returns false for every new item. Needs the refuse-to-guess guard `JobSystem.cs:278` already uses for def counts. |

The intended outcome: a player can draw storage, see it, say what goes in it and how much it
matters; can build crates so they are not drawing zones for ever; and can say "that, now".

## Decisions taken in interview (2026-09-19)

| # | Decision |
|---|---|
| 1 | **Three branches in order.** S1 zones, S2 storage units, S3 haul-urgently. Each playable alone. |
| 2 | **A storage unit is a 1-cell crate holding 8 stacks** (≈600 wood). A larger tier is later, one Def row. |
| 3 | **Deconstruct empties first, then spills.** The order marks contents for hauling out; anything left on completion goes to the nearest free cells. Nothing is ever destroyed. |
| 4 | **Haul-urgently is a marking tool that clears when the item is stored.** |
| 5 | **Item categories land in S1**, with a two-level tri-state tree filter from the first commit. Proposed set: Materials, Components, Food, Textiles, Consumables, Junk. |
| 6 | ~~A zone draws a border always, a fill only when a zone tool is armed or the zone is selected.~~ **Withdrawn 2026-09-20 — see decision 15.** Kept struck through rather than deleted, because the reason it was wrong is the useful part: it was decided before this plan knew `DrawZoneCover` existed. |
| 7 | **`StorageSettings` is a shareable record from the first commit; the group *UI* ships in S2.** |
| 8 | **Refrigeration is a design doc now, built later.** The food refusal itself ships in S2 as a Def flag. |
| 9 | **Drag adds cells; a drag touching an existing zone extends it; right-drag subtracts; a zone reduced to nothing is deleted.** |
| 10 | **A crate shows a fill tell on the model, and the exact count in the inspect pane.** |
| 11 | **Five priorities: Last, Low, Normal, Preferred, Urgent.** Named, not numbered. |
| 12 | **Filter presets at creation** (Everything / Materials / Food / Dumping / Nothing), defaulting to Everything. |

Settled without a question, both following `a-14` and existing code: an item nothing accepts
**just sits where it fell** — no "haul it anywhere" fallback — and the build-delivery giver
learns to pull material **out of** a crate, because it already pulls out of stockpiles and a crate
construction cannot reach would be a trap.

Note on decision 5: it supersedes the staging option's text, which had put categories in S3. The
tri-state tree needs them to exist, so they move forward.

---

## What `main` changed under this plan (2026-09-20)

Merging `main` into this branch was clean, but it surfaced two things the plan had wrong. Both are
the failure `CLAUDE.md` warns about — *check the code before you trust any status line* — caught
because the check was run, on a `CLAUDE.md` that was four days stale on this branch.

**1. The unit numbers are taken.** `U46`–`U50` belong to the **GR growing-zones** line (PR #119,
branch `claude/growing-zones`). Storage is therefore **`U30` → `U51` → `U52`**, not `U30`/`U46`/`U47`.

**2. The zone substrate exists, is played, and is not in `main` yet.** `GrowingZones`
(`Assets/Odyssey/Sim/Growing/GrowingZones.cs`, 536 lines, on PR #119) is already the thing S1
proposed to write:

| S1 proposed | Already built on PR #119 |
|---|---|
| a per-layer cell set with a stable zone id | `Zone { Id, byte Plant, List<int> Cells }` over a dense `_zoneAt[]` and a sorted sparse `_cells` |
| "a drag touching a zone extends it" | *Join a cell into whatever same-plant ground touches it, or found a zone of its own*, plus *fold the other zone into the survivor* |
| `EditStorageZone` / `RemoveStorageZone` intents | `DesignateZone` / `CancelZone`, already in `Sim.Contracts/Intents.cs` |
| zone state saved, hashed and published | `GrowingZones : ITickable, IStateHashable, ISaveable, ISnapshotContributor` |
| a paint-a-zone tool with a rider | `DesignateTool.GrowZone = 6` with a `Plant` rider, exactly parallel to `Build`'s `Building`/`Stuff` |
| a zone overlay | `DrawZoneCover` in the render model |

So **S1 depends on PR #119 landing**, and its job changes from *build a zone system* to *put a
second kind of zone on the one that exists*. `StorageSettings` sits where `Zone.Plant` sits.
`DesignateTool.StorageZone = 7`. The sim-side storage work — the filter, the priority ladder, the
categories, the destination rule, the three latent faults — is untouched by this and is still the
bulk of S1.

**The zone-look decision is superseded and needs re-deciding.** Decision 6 said *border always,
fill when relevant*. But a growing zone is drawn by `DrawZoneCover` as **the ground's own mesh
redrawn over itself, draped to the relief field and lifted a mark's height**, and that mechanism
cost two rounds of owner screenshots to arrive at — a flat plate at cell-centre height *"isn't
flush… thicker borders, gaps, part missing… not uniform from different angles"*. A storage zone
that draws itself a different way would be two answers to one question, which is the fault
`docs/bug-patterns.md` catalogues first. **Recommendation: storage reuses `DrawZoneCover` with its
own tint, and decision 6 is withdrawn.**

**One rule, two owners — the open question.** Zone membership, merge-on-touch and split-on-remove
would exist twice: once in `GrowingZones`, once in `StorageZones`. That is the exact shape of the
catalogue's commonest fault. Extracting a shared substrate is the right answer and it edits a
feature that has just merged and just been played. Owner's call; see §Still to decide.

## The load-bearing architectural choice

**A storage unit holds an inventory; it does not put extra stacks in a cell.**

The ground is strictly one-stack-per-cell and every write path enforces it by throwing —
`ColonyItems.Spawn:125`, `Drop:202`, `MoveTo:238`, all via `Fits:307`. Making a cell multi-slot
would touch felling (`JobDrivers.cs:388`), mining (`MineJob.cs:442`), the deconstruct refund
(`DeconstructJob.cs:163`), falling (`Falling.cs:99`), the construction refund
(`ConstructionGrid.cs:814`) and the debug grant — six call sites, each with its own tests.

Instead a contained item is `Cell = -1, CarriedBy = 0, ContainerId = n`. **That is already how a
carried item is modelled**, so "not on the ground" is an existing state rather than a new one.
Eight stacks in one tile, and the ground model is untouched.

**And one destination rule, not two.** `BestStorageCell` (`JobSystem.cs:750`) generalises to
return a `StorageSlot { int Cell; int ContainerId; }`. It is not forked for containers — that is
the `HopPriceHasOneOwnerTests` lesson applied here: two owners of one rule disagree silently.

---

## S1 — `U30` Zones and the storage control

**Blocked on PR #119 merging.** See above.

### Simulation

New file `Assets/Odyssey/Sim/Pawns/StorageSettings.cs`:

- `StorageSettings` — `Priority` (0–4), `bool[] Allow` by item def index, `ApplyPreset(int)`,
  `SetCategory(int, bool)`, `Accepts(int defIndex)`. Shareable by reference, which is what makes
  S2's groups a reference change rather than a rewrite. This is also the record
  `docs/design/04-data-model.md` §8 calls `StorageSettingsDef` and says bill ingredients will
  share (`RecipeDef`, type 21) — so it is written once and not re-invented at M5.
- `interface IStorageContainer { StorageSettings Settings; bool Accepts(int defIndex); }`.
  `Stockpile` implements it in S1; `StorageUnit` implements it in S2.
- `StoragePresets` — the five preset masks, as data.

`ColonyItems.cs` changes:

- `Stockpile` holds a `StorageSettings` instead of a bare `Priority` + `Allow`, gains a stable
  `Id`, and gains `AddCells` / `RemoveCells` keeping `Cells` sorted.
- New API: `CreateZone(cells, settings) → id`, `ExtendZone(id, cells)`, `ShrinkZone(id, cells)`,
  `RemoveZone(id)`. **`ExtendZone` removes each cell from any other zone first**, which is what
  makes overlap impossible rather than merely discouraged.
- Every cell change re-buckets `_loose` / `_stored` — `AddStockpile:145` already does half of this
  and the other half (a cell leaving a zone) has never existed.
- `ContributeTo:420` gains zones (id, priority, cells, allow) **and beds**, closing fault 1.
- `Load` gains the `Allow.Length != ItemIndex.Count` guard, closing fault 3.

New intents in `Assets/Odyssey/Sim.Contracts/Intents.cs`, all added to
`PausedIntents.AppliesWhilePaused` (each is a player's order over a cell, the stated test there):

| Intent | Payload |
|---|---|
| `EditStorageZone` | `Cell`; `A` = drag id; `B` = 0 add / 1 remove |
| `SetStoragePriority` | `Cell` = any cell of the zone; `A` = priority 0–4 |
| `SetStorageFilter` | `Cell`; `A` = scope (0 def, 1 category, 2 preset); `B` = index; `C` = 0 off / 1 on |
| `RemoveStorageZone` | `Cell` |

**The drag id is how a rectangle becomes one zone.** `Intent` is a flat value struct
(`Intents.cs:150`) and one intent per cell is the existing `Designate` pattern, so the handler
keeps a transient `dragId → zoneId` map for the duration of one drain: the first cell of a drag
decides the target (the zone it touches, else a new one) and the rest join it. The whole rectangle
is submitted in one batch by `DesignateDirector.Commit()`, so this never straddles a tick. The map
is not saved and not hashed.

### Contracts

- `ZoneView { int CellIndex; int ZoneId; byte Priority; }` — **sparse, one per zone cell**,
  following `OrderView` and the measured argument against a dense layer at
  `DesignationGrid.cs:499`.
- `CellDetail` gains the zone id under the selected cell, so the inspect pane can offer the panel.
- **Zones are unnamed in S1.** The panel titles one from its priority and size. Naming arrives in
  S2 with groups; check first whether any snapshot view may carry a string, because none does today.

### HUD (`Odyssey.Hud`, Unity-free, fast-tier tested)

- `DesignateTool.Stockpile`, plus a `Subtract` flag on `DesignateDirector` driven by right-drag.
  The rectangle gesture itself is already there (`DesignateDirector.cs:233`) and needs no new
  machinery.
- `PaletteTools.Pinned` gains `Stockpile` — one row there and one in `HudTheme.PinnedActionHue:540`.
  `HudLayout.OrdersHeight:521` already sizes from `Pinned.Length`, so the strip grows itself.
  It goes in the strip and **not** back on the Build palette: the palette answers "what to put
  down" and the Zones category was taken off it in 2026-09-17 for exactly that reason
  (`PaletteTools.cs:109`).
- **`StorageSettingsModel`** — the reusable control the brief asks for. Holds the preset row, the
  priority row and the category tree with tri-state parents, and emits the intents above against
  *a cell*, not against a zone type. S2 constructs the same model pointed at a crate. Named from
  `Registry.Label` throughout; nothing player-facing written in C#
  (`RegistryTests.NoPlayerFacingNameIsWrittenInCSharp`).
- The panel is a **popover raised from an interactive row in the inspect pane**, following the bed
  owner picker (`InspectModel.cs:619`) and using `HudLayout.PopoverBottomFor:1611`, which exists
  precisely for a popover raised by a row inside a panel.

### Presentation

- **`DrawZoneCover` with a storage tint** — the growing zone's own mechanism, unchanged: the drawn
  terrain's module placed with the same drape and lifted a mark's height along the drape's own up,
  so it is flush from every angle by construction. Do not write a second cover.
- Priority ramps the tint within one hue family, so five zones read as one feature at five
  strengths rather than as five features.
- Standing rule held: nothing in a cell, a save or the hash.
- Read `docs/design/22-growing.md` (arrives with PR #119) before touching any of this. Its zone
  section records two rejected covers and the screenshots that rejected them.

### Content and wiki (same commit, both gates)

- `docs/design/icon-keys.csv`: six `ui.res.category.*` rows, five `ui.storage.priority.*` rows.
- `docs/design/icon-map.csv` rows for the same.
- `Assets/Odyssey/Defs/Core/Pawns/Items.xml`: a `category` on each of the six items;
  `ItemDef.category` added at `PawnContent.cs:428`.
- `python3 tools/wiki/build_wiki.py` and `tools/wiki/emit_labels.py`, then both `--check`s.
- Republish `docs/wiki/artifact.html`.

### Save and hash

`ColonyItems`'s byte layout changes, so **`SaveFormat.CurrentFormatVersion` 6 → 7**
(`Saving/SaveFormat.cs:233`), reading a v6 section by building a `StorageSettings` from the old
priority/allow pair. **Every golden re-bakes**, because zones and beds join the hash. Per the WS2
precedent, that must be *measured* to be the hash seeing more rather than the colony behaving
differently: bake with zones excluded first, confirm no golden moves, then include them.

### Design doc

`docs/design/26-storage.md` — the interview, the twelve decisions, the drag-extends rule, the
priority ladder, the preset masks, and the rejected alternatives.

---

## S2 — `U51` Storage units

- `BuildingHandle.Crate = 6` (`Sim.Contracts/Catalogue.cs:122`) — append-only, handle order is a
  save contract, per the note at `Catalogue.cs:152`.
- A `Building_Crate` Def in `Defs/Core/World/Buildings.xml`: one cell, buildable in wood or stone
  through the existing pipeline (`docs/design/15-building.md`), `stackSlots = 8`,
  `refusesCategory = Food`.
- `StorageUnit` — id, cell, `StorageSettings`, contents. Implements `IStorageContainer`.
- `ColonyItem.ContainerId`; a `ContainedItems` lister beside `_loose` and `_stored`, so a crate can
  be re-stowed *out of* into a better zone and so delivery can find material in one.
- `BestStorageCell` → `BestStorageSlot`, returning cell or container. `ReservationTargetKind` gains
  `ContainerSlot` (`ReservationManager.cs:8`). `HaulJobDriver`'s final toil puts down into either.
- `DeliverWorkGiver.NearestLoad` (`BuildJob.cs:89`) learns containers.
- **Deconstruct:** the order sets an `emptying` flag on the unit, which makes its contents visible
  to the haul giver as if unstored, so haulers drain it while the work waits. On completion,
  anything still inside spills through `NearestCellWithSpace:315` with a widening radius; if
  nothing is free, it stays as a pile on the unit's own cell. Nothing is destroyed, ever — that is
  the assertion.
- **Food refusal** is a hard container-class rule, not a filter setting: `Accepts` refuses
  regardless of what is ticked, and the filter draws those rows dimmed reading "needs cooling".
  Precedent: `a-14` §4, a shelf refusing chunks.
- **Groups:** `StorageGroup { id, name, settings }`; containers and zones hold a group reference
  instead of their own settings. Select two, press Link. This is the `a-14` §Q6 answer to "one
  warehouse across three floors" — geometry per layer, configuration grouped.
- **Presentation:** a fill tell using `ItemHeap`'s existing ramp mechanism
  (`Presentation/Rendering/ItemHeap.cs`) rather than a new tier system. Inspect pane reads
  `Wood × 400 of 600`, the `× n` form `docs/design/24-pile-reading.md` §2 settled on.
- **Watch the click plane.** A crate standing proud of the floor has the bed's bug: the picker
  resolved a cell at the floor plane while the thing stands 0.70 m up, putting the clickable object
  a quarter of a cell behind the drawn one (`docs/bug-patterns.md`, `docs/design/20-beds.md` §13).
- `docs/design/26-cold-storage.md` written here: the food rule, spoilage as a per-stack timer, and
  how a cooled store hangs off M4's temperature work. Nothing built.

---

## S3 — `U52` Haul urgently

- `DesignationKind.HaulUrgent = 4` in `Designations/DesignationGrid.cs:14`. **Reuse the
  designation layer** — it already has the dense-byte + sorted-sparse-list shape, saves, hashes,
  publishes `OrderView` and has a drag tool. Urgency is per cell and clears when the cell empties,
  which is the behaviour wanted anyway.
- `UrgentHaulWorkGiver` — `WorkType = Haul`, **`Emergency => true`**. `IntraPriority` alone would
  not do it: Haul has `order = 3` in `WorkTypes.xml`, so an urgent haul would still lose to
  cutting, mining and construction. `SortGivers` (`JobSystem.cs:162`) puts emergency givers first,
  which is what "everyone drops what they're doing" means. **Invited tuning** — if it reads as
  chaos, the fallback is `IntraPriority` within Haul.
- Scans `designations.Cells` for the kind, reuses `BestStorageSlot` unchanged. The driver clears
  the designation on arrival.
- `PaletteTools.Pinned` gains `HaulUrgent` — the key `ui.arch.tool.haulurgent` already exists in
  `icon-keys.csv:154`, described as "Jump this to the top of the haul list".
- **The "No storage" alert** lands here: an `AlertModel` row for a colony with loose haulables and
  no zone that accepts them. The wording `AlertModel.cs:22` uses as its doc-comment exemplar —
  "No stockpile — *salvage lies where it fell*" — becomes a real row, and needs a registry key.

---

## Files that change

| Area | Files |
|---|---|
| New sim | `Assets/Odyssey/Sim/Pawns/StorageSettings.cs`, `StorageUnit.cs` (S2) |
| Sim edits | `Sim/Pawns/ColonyItems.cs`, `JobSystem.cs`, `JobDrivers.cs`, `BuildJob.cs`, `ReservationManager.cs`, `PawnContent.cs`, `Designations/DesignationGrid.cs` (S3), `Saving/SaveFormat.cs`, `Pawns/ColonyWorld.cs`, `Pawns/ColonyComposition.cs` |
| Contracts | `Sim.Contracts/Intents.cs`, `Views.cs`, `Catalogue.cs` |
| New HUD | `Hud/StorageSettingsModel.cs` |
| HUD edits | `Hud/DesignateDirector.cs`, `PaletteTools.cs`, `HudTheme.cs`, `InspectModel.cs`, `AlertModel.cs` (S3) |
| Presentation | a zone overlay director under `Presentation/Rendering`; a crate module (S2) |
| Content | `Defs/Core/Pawns/Items.xml`, `Defs/Core/World/Buildings.xml` (S2), `docs/design/icon-keys.csv`, `icon-map.csv` |
| Docs | `docs/design/26-storage.md`, `26-cold-storage.md` (S2), `docs/journal.md`, `docs/bug-patterns.md`, `docs/plans/vertical-slice.md`, `CLAUDE.md` status |

## Things not to undo by tidying

- **The ground stays one stack per cell.** The container inventory exists so that rule is never
  touched; a "simplification" that makes cells multi-slot re-opens six call sites.
- **One destination rule.** `BestStorageSlot` has one owner. Do not add a container-specific path.
- **Nothing presentation-side enters a cell, a save or the hash** — the zone overlay included.
- **Do not answer `RegistryTests` by rewording a literal.** Call `Registry.Label(key)`.
- **Equal priority is not better** (`JobSystem.cs:671`): two piles at one priority are one
  warehouse in two places, and shuttling between them is the failure `a-14` §3D warns of.
- **Never hand-edit `docs/wiki/`.**

## Verification

Per branch, in this order:

1. `scripts/test-fast.sh` — currently 753 Sim + 449 Hud. New suites: `StorageSettingsTests`
   (presets, tri-state roll-up, category toggles), `StorageZoneEditTests` (extend, subtract,
   no-overlap-by-construction, delete-when-empty, load guard), `StockpileTests` extended,
   `StorageUnitTests` (capacity, spill with nowhere free, food refusal beating a ticked filter),
   `HaulUrgentTests` (an urgent item beats an ordinary one *and* beats a felling job),
   `RegistryTests` for the new keys.
2. `python3 tools/wiki/build_wiki.py --check` and `tools/wiki/emit_labels.py --check`.
3. `scripts/unity.sh test editmode` — authoritative. Last recorded 1,872 total, 1,858 passed,
   0 failed. The fast tier compiles neither Presentation nor Editor, so the zone cover and the
   crate module are unproven until this runs.
4. `dotnet run --project tools/dotnet/Odyssey.SaveProbe` on a save holding zones and a full crate,
   to confirm round-trip by inspection and not only by hash.
5. **The ten-day headless soak on three seeds**, which is M3's own gate — and with the economy
   compared against the previous run, not merely checked for errors. This is where a re-stow loop
   or a reservation leak surfaces and a two-minute test does not.
6. `scripts/unity.sh build` then `Build/Win64/Odyssey.exe -odyssey-newgame -logFile <path>`. Two
   green tiers say nothing about whether the player runs.
7. Golden re-bake in S1 only, measured as described above.

8. **Scale, per `docs/process.md` §3.** This work adds two per-tick loops and neither may be
   measured on the meadow alone: the haul scan over `LooseItems` / `StoredItems` /
   `ContainedItems`, and zone edits feeding `NavGraph.Rebuild` — which the baseline audit already
   measures at **1.19 ms for a one-cell edit on 250 × 250 × 40**, against 0.065 ms at rest. A
   storage zone edit paints a whole rectangle. Measure it at the scale target before S1 is called
   done, and record the number whether or not it is comfortable.

Each branch ends with the handover `CLAUDE.md` requires: the full path, the branch, the PR, whether
Synty is junctioned, then the **what changed** table and the **what to test** table — the latter
holding only questions a person at the keyboard can answer, each with what a wrong answer looks
like. **Its playtest rows go into `docs/plans/playtest-queue.md`**, which is where that list lives
now, not into `CLAUDE.md`'s status section.

## Decided 2026-09-20, after the merge

| # | Decision |
|---|---|
| 13 | **Wait for PR #119.** S1 starts once growing-zones merges and builds on its zone code. It has been played and fixed six times; storage writing a second zone system would guarantee drift and make #119's rebase painful. |
| 14 | **Extract a shared substrate in S1.** Pull the zone geometry out of `GrowingZones` into a `CellZones` both kinds sit on, immediately after #119 merges — one owner for merge-on-touch. Safe to do because that code arrives with its own ~400-line suite (`GrowingZoneTests`, `GrowingZoneSaveTests`). |
| 15 | **Decision 6 is withdrawn.** A storage zone is a tinted ground cover drawn by `DrawZoneCover`, exactly like a growing zone, differing only in colour. One way of drawing a zone in the whole game, and it inherits the flush-from-every-angle fix instead of re-discovering it. |

**Decisions 11 and 12 (the five priority names, the six item categories) are still awaiting veto.**
Both become wiki content and both are read by the filter tree.

## Three things are in flight at once

`main` now carries the baseline audit, and two other plans are waiting for approval alongside this
one. The merge order matters and is the thing most easily lost between sessions:

| | What | State | Why this order |
|---|---|---|---|
| 1 | **GR** growing zones, PR #119 | in review, played, six fixes in | Storage S1 is blocked on its zone substrate. |
| 2 | **HT** hardening, PR #136 | plan written, no unit started | The audit measured a one-cell edit at 1.19 ms on 250 × 250 × 40, nearly all of it `NavGraph.Rebuild`. Storage adds zone edits to that same path, so hardening first means S1 is measured against a fixed baseline rather than a moving one. **Not a hard block** — if HT is slow to start, S1 proceeds and its scaling is measured against whatever the baseline then is, and said so. |
| 3 | **ST** storage, this plan, PR #137 | plan written, awaiting approval | |

## Process, as of `main`'s current tip

Three things moved under this plan and it should follow them rather than the copies it was written
against:

- **`docs/process.md` is the cycle** a unit goes through — ground, decide in a design doc, test
  first, measure, hand over, merge, play, record. S1/S2/S3 each run it once.
- **Its §3 scaling rules apply to every per-tick loop**, and this work adds two: the haul scan over
  `LooseItems` / `StoredItems` / `ContainedItems`, and zone edits feeding `NavGraph.Rebuild`.
  Measure both at the scale target, not at the meadow's 120 × 120 × 16.
- **Playtest items go in `docs/plans/playtest-queue.md`**, not into `CLAUDE.md`'s status section.
  Each branch's handover adds rows there and a verdict closes them.

Current tier baselines to compare against: fast tier **753 Sim + 449 Hud**, Long **21**; Unity
EditMode **1,872 total, 1,858 passed, 0 failed**; PlayMode **82 total, 75 passed** (75 rather than
77 because that run had no Synty junction, so two `AvatarSheetTests` art cases skipped).

*This file and `docs/plans/storage.md` diverge from here until the next commit syncs them.*
