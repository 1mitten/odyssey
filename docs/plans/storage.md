# Storage: zones, units and the haul order

*Planned 2026-09-19 with the owner, on `claude/storage-stockpile-system-k1yg51`, **PR #137**.
Phase: plan. Nothing here is built. Interview answers are in §Decisions; the reasoning that
did not fit belongs in `docs/design/26-storage.md`, written as S1 starts.*

*__Revised 2026-09-20 (first pass)__ after merging `main`, which had moved on to carry the
baseline audit (PR #136).*

*__Reviewed and re-worked 2026-09-20 (second pass)__, against `main` at `18711ee`, with every
claim in the file checked against the code rather than against a status line. **Eleven of them
had gone stale in a day** and four of the design decisions do not survive contact with the code
they were written against. The corrections are §0; the implementation each one implies is §3
onward. Nothing is deleted — a withdrawn claim is struck through with the reason, because the
reason is the part worth keeping.*

---

## §0 — What this review changed, and what proves it

Every row was checked against the tree at `main` `18711ee`. The evidence column is the file and
line that settles it, not an argument.

| # | The plan said | It is actually | Evidence |
|---|---|---|---|
| 1 | Zones and beds are **saved but not hashed** — fault 1, "fixed on the way past" | **Already fixed**, on `main`, the day after the plan was written. `ContributeTo` walks the stockpiles (priority, cells, allow) and the bed list, with the comment that found it. Storage inherits the fix and must not "re-fix" it. | `Sim/Pawns/ColonyItems.cs:449–505` |
| 2 | Overlapping zones silently overwrite — fault 2 | **Still true.** `AddStockpile` writes `_stockpileAtCell[cell] = index` unconditionally, last writer wins, no rejection. | `ColonyItems.cs:145` |
| 3 | `Allow[]` length unguarded on load — fault 3 | **Still true, and the shape is worse than stated.** The array is *saved with its own length* and read back at that length, so nothing mismatches and nothing throws; `Accepts` simply returns false for every def index past the end. A silently-refusing filter, not a crash. The fix is not a guard, it is a **default for unknown defs** — see §3c. | `ColonyItems.cs:551–560`, `ColonyItems.cs:56` |
| 4 | S1 is **blocked on PR #119** (decision 13) | **Unblocked.** #119 merged to `main` as `a45b4ec`, and `main` has moved three merges past it. S1 can start today. | `git log origin/main` |
| 5 | Storage reuses **`DrawZoneCover`** (decision 15) | **`DrawZoneCover` does not exist.** It was replaced before #119 merged by a tint bit on the ground's own bucket: `ChunkMesher` grades the terrain quad when `IsZoned`, and `DrawnTerrain` swaps grass for bare earth. No mesh, no overlay, no per-cell draw. | `Presentation/Rendering/ChunkMesher.cs:170–185`, `Presentation/World/WorldRenderModel.cs:400–428`, `ChunkBatch.cs:162` |
| 6 | …and storage can therefore reuse it | **It does not generalise**, for two independent reasons. The tint is applied to *the ground cell below* and swaps its terrain to dirt — meaningless for storage — and a growing zone is **forbidden on a slab** (`SiteAllows` refuses `Floor[index] != 0`), while a storage zone's commonest home is a built floor indoors, where no terrain quad is drawn at all. §6 has the answer that does generalise. | `Sim/Growing/GrowingZones.cs:138–158`, `ChunkMesher.cs:560–575` |
| 7 | `BuildingHandle.Crate = 6` | **6 is `Door`** (PR #142). The crate is **7**, `Count` 7 → 8. Handle order is the save contract; append only. | `Sim.Contracts/Catalogue.cs:200–201` |
| 8 | A new `ZoneView { CellIndex, ZoneId, Priority }` | **The name is taken.** `ZoneView(int cellIndex, byte plant)` is the growing channel. A second struct of the same name is a compile error; a *rename* of the existing one touches the render mirror, the mesher and `GrowingRenderTests`. §5c names the new one `StoreView` and leaves the old alone. | `Sim.Contracts/Views.cs:589–602` |
| 9 | `PaletteTools.Pinned` gains a row, `HudTheme` gains a row | **The keys and the palette rows already exist.** `ui.arch.tool.stockpile` and `ui.arch.tool.dumping` are in `icon-keys.csv` (lines 149, 152) and are already listed under `ui.arch.category.zones` in the palette. What is missing is that the chip does nothing. | `docs/design/icon-keys.csv:149,152`, `Hud/PaletteTools.cs:157` |
| 10 | Baselines: fast tier 753 + 449; EditMode 1,872 / 1,858 | **806 Sim + 471 Hud**; EditMode **2,003 / 1,990 / 0**, PlayMode **85 / 80 / 0**. Save format 6 is still current, so v6 → v7 stands. | `CLAUDE.md` §Tests and gates, `SaveFormat.cs:233` |
| 11 | S3's `Emergency => true` means "everyone drops what they're doing" | **It does not.** `WorkThinkNode` loops the player's priorities 1–4 and only *within* a pass walks the sorted giver list, so `Emergency` re-orders givers inside one priority level and nothing more. A colonist with Mining at 1 and Haul at 4 mines all day while the urgent pile sits there. No giver overrides the flag today, so it has never been exercised. §8 has the fix. | `JobSystem.cs:699–709` (the 1–4 loop, in `WorkThinkNode` at 687), `JobSystem.cs:162–176` (`SortGivers`), `JobSystem.cs:40` |

And one correction that changes the shape of the work rather than a number:

| # | The plan said | It is actually |
|---|---|---|
| 12 | §Verification 8: the two per-tick loops to measure are the haul scan **and zone edits feeding `NavGraph.Rebuild`**, at 1.19 ms a one-cell edit | **A storage zone edit does not touch the navigation graph at all.** A stockpile changes no walkability, no floor and no edifice; `AddStockpile` writes a dictionary and re-buckets two lists. The 1.19 ms figure belongs to **S2**, where a crate is an edifice and placing one is an ordinary one-cell build. What a zone edit *does* cost is a **chunk remesh** — §6 — which nothing in the plan measured or even named. |

---

## Context

The colony has a working storage **simulation** and no storage **game**. `Stockpile`
(`ColonyItems.cs:37`) carries a priority, a cell set and a per-item-def `Allow[]`, and
`HaulWorkGiver` (`JobSystem.cs:780`) implements the destination rule from
`docs/research/a-14-bills-stockpiles-inventory.md` §3 — *accepts → has space → highest priority →
nearest* — including lower→higher re-stow, with reachability answered before pathing.

What does not exist is any way for a player to touch it. There is no intent, no tool and no panel,
and **nothing draws a stockpile**: the starting zone is placed by `ColonyScenario.cs:772` and is
invisible. `U30 Stockpiles` has sat in `docs/plans/vertical-slice.md:139` unbuilt because the
simulation half arrived early under other units.

Two latent faults are fixed on the way past — the third was fixed by somebody else first:

| Fault | Where | Why it matters |
|---|---|---|
| ~~Stockpiles and beds saved but not hashed~~ | ~~`ColonyItems.cs:420`~~ | **Closed on `main`, 2026-09-20.** Kept struck through because it is the review's own example: a plan that asserts a fault it has not re-checked will send somebody to fix it twice. |
| **Overlapping zones silently overwrite** | `_stockpileAtCell`, `ColonyItems.cs:145` | Last `AddStockpile` wins per cell, no rejection. §4's anchor rule removes it by construction rather than guarding against it. |
| **An unknown item def is silently refused** | `ColonyItems.cs:551` | A save written before a def existed reads a short `Allow[]` and `Accepts` answers false for everything past its end, for ever, with no error. The answer is an explicit default, not a guard: §3c. |

The intended outcome: a player can draw storage, see it, say what goes in it and how much it
matters; can build crates so they are not drawing zones for ever; and can say "that, now".

---

## Decisions taken in interview (2026-09-19)

| # | Decision | Status after review |
|---|---|---|
| 1 | **Three branches in order.** S1 zones, S2 storage units, S3 haul-urgently. Each playable alone. | Stands — with **S0** added in front of it (§2). |
| 2 | **A storage unit is a 1-cell crate holding 8 stacks** (≈600 wood). A larger tier is later, one Def row. | Stands. |
| 3 | **Deconstruct empties first, then spills.** Nothing is ever destroyed. | Stands, **with the hole named**: under one-stack-per-cell "nothing is destroyed" is not always satisfiable. §7d. |
| 4 | **Haul-urgently is a marking tool that clears when the item is stored.** | Stands; the machinery it needs is not the machinery the plan named. §8. |
| 5 | **Item categories land in S1**, with a two-level tri-state tree from the first commit. | **Questioned** — seven item defs, six categories, two of them empty. §5d. |
| 6 | ~~A zone draws a border always, a fill only when a zone tool is armed or the zone is selected.~~ | Withdrawn 2026-09-20. Superseded by 15, which is itself superseded by **16**. |
| 7 | **`StorageSettings` is a shareable record from the first commit; the group *UI* ships in S2.** | Stands, and becomes **the load-bearing idea**: §3a makes it a handle rather than an object, which is what makes S2's groups a repoint instead of a rewrite. |
| 8 | **Refrigeration is a design doc now, built later.** The food refusal ships in S2 as a Def flag. | Stands, **with a consequence to accept knowingly**: §7c. |
| 9 | **Drag adds cells; a drag touching an existing zone extends it; right-drag subtracts; a zone reduced to nothing is deleted.** | **Amended.** "Touching extends" cannot survive per-zone settings. §4. |
| 10 | **A crate shows a fill tell on the model, and the exact count in the inspect pane.** | Stands. |
| 11 | **Five priorities: Last, Low, Normal, Preferred, Urgent.** Named, not numbered. | Still awaiting veto. |
| 12 | **Filter presets at creation** (Everything / Materials / Food / Dumping / Nothing), defaulting to Everything. | Still awaiting veto. Note `ui.arch.tool.dumping` already exists as a palette chip, so "Dumping" is a preset *and* a second armed tool. |

Settled without a question, both following `a-14` and existing code: an item nothing accepts
**just sits where it fell** — no "haul it anywhere" fallback — and the build-delivery giver learns
to pull material **out of** a crate, because it already pulls out of stockpiles and a crate
construction cannot reach would be a trap.

## Decisions taken 2026-09-20 (first pass)

| # | Decision | Status after review |
|---|---|---|
| 13 | **Wait for PR #119.** | **Discharged** — #119 is in `main`. |
| 14 | **Extract a shared substrate in S1.** | **Amended:** the substrate is extracted, but it is the *container* and not the *policy*, and it is its own PR in front of S1, not a thing done inside it. §2, §3b. |
| 15 | ~~A storage zone is a tinted ground cover drawn by `DrawZoneCover`.~~ | **Withdrawn — the function does not exist**, and the mechanism that replaced it does not generalise. §0 rows 5–6, and decision 16. |

## Decisions this review proposes (16–21)

| # | Decision | Why |
|---|---|---|
| 16 | **A storage zone is a tint bit on whichever surface the cell already draws** — the terrain quad below it *or* the floor slab at its own boundary. `TintCode.StoredBase`, exactly the mechanism a growing zone uses, applied at both emit sites instead of one. Zero extra draw calls. | It is the only answer that works indoors on a slab, and `P10` in `docs/bug-patterns.md` rules out the per-cell alternative before it is written. §6. |
| 17 | **A zone's configuration is a handle, not an object.** `StorageSettingsTable` owns the records; a zone and a crate hold an `int SettingsId`. | It makes S2's storage groups a repoint of one integer, makes the save and the hash a single walk of one table, and makes "do these two zones share a configuration" a comparison rather than a deep equality. §3a. |
| 18 | **A drag never merges two existing zones. The anchor decides.** Start inside a zone and the whole drag extends *that* zone; start outside and the drag founds a new one, taking cells from any zone it crosses. | Merge-on-touch is safe for growing zones because a zone is identified by its plant and two touching same-plant zones are interchangeable. A storage zone carries settings, so merge-on-touch silently destroys one of two configurations. §4. |
| 19 | **`ColonyItem.ContainerId` lands in S1's save version even though nothing writes it until S2.** | One format bump instead of two, and the v6 → v7 reader is written once by the person who is already writing one. §5e. |
| 20 | **`Emergency` is made to mean what S3 needs it to mean**, by scanning emergency givers in a pass ahead of the player's 1–4 loop, gated on the pawn having the work type enabled at all. | Today the flag is inert and untested; S3's headline behaviour does not work without this. §8a. |
| 21 | **The category tree is deferred to the commit that gives it something to compress.** S1 ships presets + priority + seven per-item rows; `ItemDef.category` still lands in S1 as content. | Seven item defs and six categories means two empty branches and a tri-state roll-up over rows of one. §5d. Owner's call — decision 5 was taken in interview. |

---

## §1 — The load-bearing architectural choice, unchanged and re-verified

**A storage unit holds an inventory; it does not put extra stacks in a cell.**

The ground is strictly one stack per cell and every write path enforces it by throwing —
`ColonyItems.Spawn:125`, `Drop:207`, `MoveTo:243`, all through `Fits:312`. Making a cell
multi-slot would touch felling, mining, the deconstruct refund, falling, the construction refund
and the debug grant — six call sites, each with its own tests.

Instead a contained item is `Cell = -1, CarriedBy = 0, ContainerId = n`. **That is already how a
carried item is modelled**, so "not on the ground" is an existing state rather than a new one.

Re-verified, and with the bill attached: **`item.Cell < 0` is read in nine places** and each one
is now answering a question with three possible states instead of two. The table is §7b. This is
the single largest piece of hidden work in S2 and the plan did not cost it.

**And one destination rule, not two.** `BestStorageCell` (`JobSystem.cs:879`) generalises to
return a `StorageSlot { int Cell; int ContainerId; }`. It is not forked — the
`HopPriceHasOneOwnerTests` lesson applied here.

---

## §2 — S0: the zone container, extracted first

**Its own PR, before S1, behaviour-preserving, every golden identical.**

Decision 14 was right that one rule must not have two owners, and wrong about which thing is the
rule. Read `GrowingZones` and it is three things in one file:

| | What | Shared with storage? |
|---|---|---|
| a | the **container** — `int[] _zoneAt` dense, `List<int> _cells` sparse and sorted, per-zone sorted cell lists, `MergeInto`, `Dissolve` | **Yes, exactly.** And it carries two hard-won fixes: `MergeInto` re-sorts after a fold (a cancel would throw inside the intent drain without it) and `Dissolve` swaps the last zone into the vacated slot rather than shifting (found by the U50 field benchmark). |
| b | the **join policy** — eight-neighbour, same-plant, fold two zones into one | **No.** Storage must not fold; §4. |
| c | the **payload** — `_cropAt`, `_growthAt`, `SiteAllows`, sowing | No. |

So S0 extracts (a) and leaves (b) and (c) where they are:

```csharp
// Assets/Odyssey/Sim/World/ZoneGrid.cs — no policy, no payload, no intents.
public sealed class ZoneGrid
{
    readonly int[] _zoneAt;          // cell -> slot, or -1
    readonly List<int> _cells;       // every zoned cell, ascending
    readonly List<List<int>> _slots; // per slot, ascending
    readonly List<int> _tags;        // per slot: the owner's payload handle

    public int SlotAt(int cell);                 // -1 when unzoned
    public int TagOf(int slot);
    public int Found(int tag);                   // a new slot
    public void Join(int slot, int cell);
    public void Leave(int cell);                 // dissolves a slot that empties
    public void MergeInto(int home, int other);  // keeps both invariants above
    public IReadOnlyList<int> Cells { get; }
    public IReadOnlyList<int> CellsOf(int slot);
}
```

`GrowingZones` keeps its `Paint` (the eight-neighbour same-plant join) and calls `Found`/`Join`/
`MergeInto`; `StorageZones` calls `Found`/`Join`/`Leave` and never `MergeInto`. One test body —
`ZoneGridInvariantTests` — runs random paint/cancel sequences and asserts after every step that
every `_cells` entry is ascending, every slot's list is ascending, every cell's `_zoneAt` points
at a slot that contains it, and no slot is empty. Both of GrowingZones' historical bugs fail it.

**Gate for S0:** fast tier green, `GrowingZoneTests` and `GrowingZoneSaveTests` untouched and
green, EditMode green, and **all three goldens byte-identical** — a behaviour-preserving
extraction that moves a hash has not preserved behaviour.

**Why in front rather than inside S1.** One concern per commit, and because if the extraction
turns out to cost more than a day it can be abandoned without abandoning storage: `StorageZones`
written against its own copy of the container is a known, bounded, reviewable duplication, and
that decision is cheaper to take on day one than on day six. The cheaper alternative, stated so
nobody has to re-derive it: skip S0, write `StorageZones` with its own container, and open an
issue naming the duplication.

---

## §3 — The three new sim types

### 3a. `StorageSettings` and the table that owns them

`Assets/Odyssey/Sim/Storage/StorageSettings.cs`:

```csharp
public sealed class StorageSettings
{
    public int Priority;        // 0..4, a StoragePriority value
    public bool[] Allow;        // by item def index
    public bool AllowUnknown;   // what a def added after this record was written gets
    public string? Name;        // null until S2's groups; see §5c on strings in views

    public bool Accepts(int defIndex) =>
        (uint)defIndex < (uint)Allow.Length ? Allow[defIndex] : AllowUnknown;
}

public sealed class StorageSettingsTable : ISaveable, IStateHashable
{
    public int Create(int preset);          // mints a record, returns its id
    public StorageSettings this[int id] { get; }
    public int Count { get; }
}
```

Ids are **slot positions and are never reused** — the `PlacedEdifice` contract, for the same
reason: a zone or a crate holds one, and a reused id is a zone that silently adopts a stranger's
filter. A record with no referent is left as a tombstone; a colony mints tens of these, not
thousands.

**Why a table and not a field.** Decision 7 wants a shareable record. Shared by *reference* in C#
is invisible to the save file and to the hash: two zones pointing at one object serialise as two
records and come back as two, and the group quietly dissolves across a save. A handle serialises
as an integer that is equal on both sides. It is also the whole of S2's "Link": point both at one
id. And it makes `Accepts` one indirection instead of a copy per zone.

### 3b. `StorageZones`

`Assets/Odyssey/Sim/Storage/StorageZones.cs`, modelled line for line on `GrowingZones` and sitting
on S0's `ZoneGrid`:

```csharp
public sealed class StorageZones : ITickable, IStateHashable, ISaveable, ISnapshotContributor
{
    readonly CellGrid _grid;
    readonly ZoneGrid _zones;               // tag = settings id
    readonly StorageSettingsTable _settings;
    readonly ChunkGrid? _chunks;            // remesh marks; null headless
    readonly ColonyItems _items;            // to re-bucket loose/stored on a membership change
}
```

Four things it is, and each one is why it is a component rather than three methods bolted onto
`ColonyItems`:

- it **marks chunks dirty** when a cell joins or leaves a zone, which `ColonyItems` has no
  `ChunkGrid` to do (`GrowingZones` takes one for exactly this reason);
- it **publishes a snapshot channel**, which `ColonyItems` does not (`PawnRegistry` publishes
  things);
- it **hashes and saves its own section**, so the zone records stop riding in the middle of the
  items section where a layout change forces a format bump on items;
- and `ColonyItems`' own doc comment says it is "a placeholder for the things line of work … so
  that when the real thing arrives this can be deleted rather than migrated". Do not grow it.

`Stockpile`, `AddStockpile`, `StockpileAt` and `IsStockpileCell` move here. `ColonyItems` keeps
`_loose`/`_stored` and gains one seam:

```csharp
public IZoneMembership? Membership { get; set; }   // null = nothing is zoned
void Enlist(int cell, int i) => InsertInto(Membership?.IsStorage(cell) == true ? _stored : _loose, i);
public void Rebucket(int cell);                     // called by StorageZones on every join/leave
```

`Rebucket` is the half of `AddStockpile` that has never existed: a cell *leaving* a zone must move
its item from `_stored` back to `_loose`, and today nothing can take a cell out of a zone at all,
so the gap has never been reachable.

### 3c. What an unknown item def gets

The fault in §0 row 3 is not a length mismatch, it is a missing answer. Two saves need different
behaviour and the file cannot tell them apart today:

- a zone created with the **Everything** preset, saved, and a new commodity added — the player
  means the new thing to go in it;
- a zone created with **Nothing** and two boxes ticked — the player does not.

So the answer is stored, not inferred: `AllowUnknown`, written with the record and set by the
preset (`Everything` → true, everything else → false, and the per-item rows never change it).
`Load` constructs `new bool[Content.Items.Length]`, reads the saved length, copies
`min(saved, current)` and fills the tail with `AllowUnknown`. This is precisely the pattern
`PawnRegistry` already uses for `Needs`, `Skills` and `WorkPriorities` — save the length, copy what
fits, leave the rest at the constructed default (`PawnRegistry.cs:298–318, 380–412`) — and the one
thing it adds is that the default is authored rather than zero.

---

## §4 — The drag, and why "touching extends" cannot stand

Decision 9 says a drag touching an existing zone extends it. On the substrate that exists, that
means `GrowingZones.Paint`: a cell joins any eight-neighbouring zone, and a stroke across two
zones' corner **folds them into one**. For growing that is sound — a zone is identified by its
plant, two touching carrot zones are interchangeable, and the fold loses nothing. For storage it
destroys a configuration: fold a *Preferred, food only* zone into a *Last, dumping* zone and one
of the two filters is simply gone, with no message and nothing to undo it.

It is also not what a player means. Two stockpiles that happen to share an edge are two
stockpiles.

**The rule: the anchor decides, and two existing zones never merge.**

| The drag starts | What happens |
|---|---|
| inside storage zone `Z` | every acceptable cell in the box joins `Z` |
| anywhere else | the first acceptable cell founds a new zone `N` with a fresh settings record from the armed tool's preset; the rest join `N` |
| over cells belonging to another zone `Y` | those cells **transfer** — removed from `Y`, added to the target. `Y` dissolves if it empties. Never a merge, never an overlap. |
| right-drag (subtract) | each cell leaves whatever zone holds it; the anchor is irrelevant |

Overlap is impossible by construction, which closes fault 2 without a guard.

**The mechanism, and it is simpler than the drag id the plan proposed.** `Intent` is a flat value
struct with `Cell` and three integers, one intent per cell, and the whole rectangle is submitted in
one batch by `DesignatePresenter.Submit` (`DesignatePresenter.cs:298–350`) and drained
contiguously at step 1 of one tick. So the intent carries **the anchor's cell index**, which is
data with a meaning, rather than a minted id that means nothing outside the session:

```
EditStorageZone   Cell = this cell;  A = anchor cell index;  B = 0 add / 1 remove;  C = preset
```

and the handler keeps **one field**, not a map:

```csharp
(int tick, int anchor, int slot) _run;   // transient: not saved, not hashed
```

If `intent.A` and the current tick match `_run`, the cell joins `_run.slot`; otherwise the anchor
is resolved (`ZoneGrid.SlotAt(anchor)`, else found a new slot) and `_run` is rewritten. A drag that
straddles a tick boundary — which cannot happen through `Submit`, but could through a replay or a
future queued order — degrades into two zones rather than into a corrupt one, which is the right
failure.

The other three intents, all added to `PausedIntents.AppliesWhilePaused` (each is a player's order
over a cell, the stated test there):

| Intent | Payload |
|---|---|
| `SetStoragePriority` | `Cell` = any cell of the zone, or the crate's cell; `A` = 0–4 |
| `SetStorageFilter` | `Cell`; `A` = scope (0 def, 1 category, 2 preset); `B` = index; `C` = 0 off / 1 on |
| `RemoveStorageZone` | `Cell` |

`SetStoragePriority` and `SetStorageFilter` name a **cell** and not a zone, which is what lets the
same HUD model drive a crate in S2 without a second intent pair.

---

## §5 — S1 `U30`: zones, the control, and what it costs

### 5a. Sim, file by file

| File | Change |
|---|---|
| `Sim/World/ZoneGrid.cs` | **new**, S0 |
| `Sim/Storage/StorageSettings.cs` | **new** — the record, the table, `StoragePriority`, `StoragePresets` (the five masks as data) |
| `Sim/Storage/StorageZones.cs` | **new** — §3b, four intent handlers, hash, save, snapshot, `Attach(SimWorldBuilder)` in the `GrowingZones.Attach` shape |
| `Sim/Pawns/ColonyItems.cs` | `Stockpile` and the four stockpile members move out; `Membership` seam and `Rebucket` in; `ColonyItem.ContainerId` added (decision 19); v6 legacy zone read (§5e) |
| `Sim/Pawns/JobSystem.cs` | `BestStorageCell` → `BestStorageSlot`, and the band short-circuit (§5f) |
| `Sim/Pawns/PawnContext.cs` | a `Storage` reference beside `Growing` |
| `Sim/Pawns/ColonyComposition.cs`, `ColonyWorld.cs` | construct and register the two new components; `RebuildDerived` re-buckets |
| `Sim/Pawns/ColonyScenario.cs:772` | the starting zone is created through `StorageZones` with a settings record, not `AddStockpile` |
| `Sim/Pawns/PawnContent.cs:439` | `ItemDef.category` |
| `Sim/Saving/SaveFormat.cs:233` | 6 → 7, with the version note in the same doc comment style as 2–6 |

### 5b. The starting zone stops being invisible

`ColonyScenario` places a zone at tick zero and nothing has ever drawn it. The moment §6 lands,
**every existing save and every new colony shows a storage zone that the player did not draw**,
in whatever tint storage gets. That is correct and it is also the first thing the owner will
report if nobody says it here. It is one row in the *what changed* table of S1's handover.

### 5c. Contracts

```csharp
public readonly struct StoreView          // NOT ZoneView — that name is the growing channel's
{
    public readonly int CellIndex;
    public readonly int Slot;             // zone slot, so a pane can title "this zone"
    public readonly byte Priority;
}
```

Sparse, one per zone cell, whole-world, cell-index order — `OrderView`'s shape and the measured
argument at `DesignationGrid.Contribute`. `CellDetail` gains the zone slot under the selected cell
so the inspect pane can offer the panel.

**Zones are unnamed in S1.** The panel titles one from its priority and size. `Name` sits on the
settings record ready for S2's groups, and the check the plan asked for is done: **no snapshot view
carries a string today**, so naming a zone is a contract change and not a field. It stays in S2.

### 5d. HUD

- `DesignateTool.StorageZone = 7` (GrowZone is 6 and is last — append at merge time, PRs #143 and
  #145 are in flight), plus a `Subtract` flag on `DesignateDirector` driven by right-drag. The
  rectangle gesture is already there (`DesignateDirector.Commit:495`) and needs no new machinery.
  Note that `OnTheWorkingLayer` has a per-tool special case for `GrowZone` (+1 layer, because the
  zone is the air the sower stands in); **storage wants the same +1** for the same reason — the
  pointer names a surface, the stack sits in the cell above it — and getting this wrong will look
  like "the zone paints inside the hill".
- `PaletteTools.Pinned` gains `ui.arch.tool.stockpile`; `HudLayout.OrdersHeight` already sizes from
  `Pinned.Length`. The key and the palette row already exist (§0 row 9), so the content change is
  smaller than the plan thought.
- **`StorageSettingsModel`** — the reusable control: preset row, priority row, item rows. Emits the
  intents above **against a cell**, so S2 constructs the same model pointed at a crate. Named from
  `Registry.Label` throughout (`RegistryTests.NoPlayerFacingNameIsWrittenInCSharp`).
- The panel is a **popover raised from an interactive row in the inspect pane**, following the bed
  owner picker (`InspectModel`, `HudShell.Inspect.cs:752`) and `HudLayout.PopoverBottomFor`.

**On the category tree (decision 21).** The game has seven item defs: ration pack, salvage, wood,
stone, iron ore, coal, carrots. Against the six proposed categories, Textiles and Components have
no members at all and Food has two. A tri-state parent over a branch of one is a control that
costs a fortnight and compresses nothing. The recommendation is to ship `ItemDef.category` as
**content** in S1 — the wiki rows, the Def attribute, the registry keys — so nothing needs
re-touching, and ship the *tree* in the commit where the item table first exceeds about fifteen
defs. Until then the filter is seven rows and five presets, which fits in a popover without
scrolling. **Owner's call: decision 5 was taken in interview and this is a reversal of it.**

### 5e. Save and hash

- `SaveFormat.CurrentFormatVersion` **6 → 7**.
- Sections are keyed and length-prefixed (`SaveFormat.Save:257–276`), so **adding** the
  `odyssey.storage` and `odyssey.storage.settings` sections costs nothing and an old reader skips
  them. What forces the bump is that the **items section changes layout**: the stockpile records
  leave it and `ContainerId` joins the item record.
- **The v6 read.** `ColonyItems.Load` at `FormatVersion < 7` still reads the old pile records —
  the bytes are there and a sequential reader must consume them — and stashes them in a
  `PendingLegacyZones` list. `ColonyWorld.RebuildDerived` drains it into `StorageZones`, minting one
  settings record per old pile from its priority and `Allow[]` with `AllowUnknown = false`.
  Section order is the components list's order, so the stash-and-drain is what keeps this from
  depending on it. `SaveFormatV2Tests:48` pins the number and moves to 7; a v6 fixture with two
  overlapping piles is the test that proves the drain (the overlap is legal in v6 files and
  illegal after, so the drain must resolve it — last writer keeps the cell, matching what v6
  actually did).
- **Every golden re-bakes**, because the zone records move sections and the hash walks them in a
  new order. Per the WS2 precedent this must be **measured** to be the hash seeing the same state
  differently rather than the colony behaving differently: bake with the storage sections excluded
  from the hash first, confirm no golden moves, then include them and re-bake. A re-bake that is
  not measured is a re-bake that hides a behaviour change.

### 5f. `BestStorageSlot`, and the loop that is actually hot

Today (`JobSystem.cs:879`): for each candidate item, walk every stockpile, and for every cell of
every stockpile test space, reservation, `CanEnter` and `Reachable`. That is O(Σ zone cells) per
item considered, inside a scan over `LooseItems` and then `StoredItems`. A painted warehouse turns
a 40-cell scan into a 1,600-cell one and the plan's own §8 never measured it because the meadow
benchmark has one zone of a handful of cells.

Two changes, both cheap, both in S1 because S1 is what makes big zones possible:

1. **Walk priority bands, high to low, and stop at the first band that yields anything.** Priority
   dominates distance — nearest only breaks ties *inside* a band — so once a band has produced a
   reachable cell, no lower band can win and the walk ends. `StorageZones` keeps its slots indexed
   by priority (five buckets; priority changes are rare and re-bucket one zone).
2. **Only walk cells that have space.** Each zone keeps a sorted `Free` list maintained by the same
   `Enlist`/`Unlist`/`Rebucket` hooks that maintain `_loose`/`_stored`. A full warehouse then costs
   nothing to scan instead of costing its whole area.

Worst case falls from "every cell of every zone" to "the free cells of the best band". Both are
pure bookkeeping — no rule moves — so the goldens must not move, and that is the assertion.

While in there, one adjacent fault worth a line because it is on the same hot path and is the same
shape: `CriticalNeedsThinkNode.TryEat` (`JobSystem.cs:589`) walks **`Items` — every item ever
spawned, tombstones included** — once per hungry pawn per think. It is not storage's bug and it is
storage's neighbour; it belongs to **HT6**, whose own row already says its number "decides whether
per-region item listers are built".

---

## §6 — Drawing a zone, which is now an open piece of design rather than a reuse

The plan's answer was `DrawZoneCover`. It does not exist. What replaced it is better and does not
transfer:

```csharp
// ChunkMesher.EmitTerrain, line 184
if (_model.IsZoned(index)) tint = TintCode.Tilled(tint);
```

— a bit in the bucket key of the ground quad that is being drawn anyway. No mesh, no overlay, no
per-cell submission, and `TintCode.TilledBase`'s own comment carries the measurement that justifies
it. `WorldRenderModel.IsZoned` reads the cell **one layer up**, because a zone is painted on the
air a colonist stands in and the soil is beneath her feet.

Two reasons it cannot simply be copied:

1. `DrawnTerrain` **swaps grass for bare earth** — tilled soil is a different terrain, which is
   what makes a field seamless. A storage zone does not change what the ground is made of.
2. A growing zone is legal only on fertile soil and is **refused on a slab** (`SiteAllows`:
   `_grid.Floor[index] != 0` → false). A storage zone's commonest home is a wooden floor inside a
   building, where the terrain quad below is not drawn at all — the slab is, through `AddRoof`
   with `TintCode.Stuff(...)` (`ChunkMesher.cs:569`).

**Decision 16, in full:**

- `TintCode.StoredBase`, one bit, with `Stored(code)` / `IsStored(code)` beside `Tilled`.
- Applied in **two** emit sites: the terrain quad (as tilled is) *and* the floor slab. Both already
  take a tint; neither gains a draw call. A chunk holding a warehouse pays one extra bucket for the
  stored floor and one for the unstored, exactly as a field does.
- `ChunkRenderer.TintOf` (line ~708, where `IsTilled` grades) gains the storage grade — a wash
  toward the hue `OrderColours` gives the stockpile tool, so the ground and the chip agree. **The
  colour has one owner and it is `Odyssey.Hud.OrderColours`** (`CLAUDE.md`, standing rules); do not
  write a `Color` for it in Presentation.
- **Priority reads as strength within one hue**, three bits of the tint code, so five zones read as
  one feature at five strengths. Five extra buckets per chunk in the worst case.
- `WorldRenderModel` gains a `byte[] _storage` mirror (priority + 1, 0 = none) and an
  `UpdateStorage(ReadOnlySpan<StoreView>)` merge walk, the twin of `UpdateZones` (`:482`).

**And the cost nobody has named: the remesh.** `GrowingZones` calls `MarkCellAndSides` on every
painted cell. Painting a 20 × 20 storage zone dirties on the order of 400 cells across several
chunks in one drain, and every dirty chunk is re-meshed before the next frame. That, and not
`NavGraph.Rebuild`, is S1's scaling risk (§0 row 12). It is measurable before a line of HUD is
written: paint a 30 × 30 zone in one drag at the scale target and record the frame it lands in.
**Record the number whether or not it is comfortable.**

If it is not comfortable, the fix is known and is not a redesign: mark chunks rather than cells
(one `MarkDirty` per chunk touched by the box, not one per cell), which the box gesture makes
trivial because the rectangle is known before the first cell is painted.

---

## §7 — S2 `U51`: storage units

### 7a. The thing itself

- `BuildingHandle.Crate = 7`, `Count = 8` — append-only, handle order is the save contract.
- A `Building_Crate` Def in `Defs/Core/World/Buildings.xml`: one cell, wood or stone through the
  existing pipeline, `stackSlots = 8`, `refusesCategory = Food`.
- `StorageUnits` — a component, not a field on `PlacedEdifice`: the edifice record is a value
  struct and cannot hold a list, which is exactly why `Owner` (a scalar) rides it and this does
  not. One record per crate, keyed by **edifice handle**, which is stable by contract (removed
  slots are tombstoned, `PlacedEdifice.Removed`).

```csharp
sealed class StorageUnit { public int Edifice; public int SettingsId; public ThingId[] Slots; public bool Emptying; }
```

Its own save section `odyssey.storage.units` — a new section, so **no format bump** (§5e), which is
the whole reason decision 19 puts `ContainerId` in S1's v7.

### 7b. The nine readings of `item.Cell < 0`

Each one is a question that now has three answers — on the ground, in a pair of hands, in a box —
and each needs its own decision. This is the bill the plan did not have:

| Site | Reads it as | Under containers |
|---|---|---|
| `JobSystem.cs:814` haul scan | "not haulable now" | **Right by accident**: a contained item must not be hauled *unless* its crate is emptying or a better zone exists. Add the `ContainedItems` lister and scan it third, after loose and stored. |
| `JobSystem.cs:598` eat scan | "not edible now" | **Correct only while crates refuse food** (decision 8). If that refusal is ever lifted without this site, colonists starve beside a full crate. See §7c. |
| `BuildJob.cs:108`, `:300` delivery | "not deliverable" | **Must change.** Material in a crate is invisible to construction otherwise, and a crate the builders cannot reach into is the trap the interview already ruled out. |
| `JobDrivers.cs:22, 95` haul toils | "the thing moved or vanished" | Must accept "it is in the crate I am reaching into". |
| `PawnRegistry.cs:274` publishing | "do not draw it" | **Correct unchanged** — a contained item is drawn by the crate's fill tell, not as a pile. |
| `Falling.cs:122` floating sweep | "not on the ground, so not floating" | **Correct unchanged**, but see §7e: the crate itself can fall. |
| `ColonyItems.cs:573` load rebuild | "not at a cell" | Must file contained items into the container lister instead of skipping them. |

A test that no site is missed: a control that puts one stack of every def in a crate, runs a day,
and asserts the colony's total stack count is unchanged — the shape that catches an item that has
fallen out of every lister and become unreachable rather than lost.

### 7c. The food refusal, and what it costs to keep

Decision 8 ships `refusesCategory = Food` in S2 and refrigeration later. Two consequences worth
taking knowingly:

- it **keeps the eat scan simple** (§7b row 2), which is a real saving;
- and it means **the first container in the game refuses the only commodity with a consumer**. The
  S2 playtest cannot ask "does a pantry work", because there are no pantries; and the dimmed rows
  reading "needs cooling" advertise a mechanic that does not exist — spoilage is not built, so a
  meal in a crate and a meal on the floor are identical in every way the simulation can measure.

The alternative, for the owner: **crates accept food in S2**, and the refusal lands in the same
commit as spoilage, where it has something to be true about. It costs the eat-from-container path
(one scan site, one toil). Recommendation is to take it — a rule with no simulation behind it is
the "compatibility clause keeping the bug alive" pattern arriving early — but it is decision 8 and
therefore the owner's.

### 7d. Deconstruct, and the promise that cannot always be kept

The order sets `Emptying`, which makes the contents visible to the haul giver as if unstored, so
haulers drain the crate while the work waits. The destination scan must **exclude the crate being
emptied**, or it will re-stow into itself.

On completion, what is left spills through `NearestCellWithSpace` with a widening radius. And here
is the hole the plan's "nothing is ever destroyed, that is the assertion" does not survive: the
ground is one stack per cell, so eight stacks need eight free cells, and on a full or enclosed map
there may be none. The plan's fallback — "it stays as a pile on the unit's own cell" — is exactly
the state six write paths throw on.

Two rules, and they differ because the situations differ:

- **Deconstruct never destroys.** If the contents cannot be placed, the work **does not complete**:
  the crate stands, the designation stands, and an alert says "Crate cannot be emptied". A player's
  order that cannot be carried out is refused, not approximated.
- **Collapse may destroy.** A crate that falls out of a collapsing building spills what it can
  through the same widening search and loses the rest, exactly as a loose stack over void does
  today (`Falling.ItemsOutOf` despawns it). Losing things to a collapse is a consequence; losing
  things to a deconstruct is a bug.

### 7e. The rest of S2

- `ColonyItem.ContainerId` already exists from S1; a `ContainedItems` lister beside `_loose` and
  `_stored`, so a crate can be re-stowed *out of* into a better zone.
- `BestStorageSlot` returns cell or container. `ReservationTargetKind.Container = 2`
  (`ReservationManager.cs:8`), keyed on the edifice handle. **One hauler per crate at a time in
  v1**, which is the conservative reading: two haulers both aiming at the last free slot is exactly
  the race reservations exist to stop, and per-slot claims can come the day a crate is a bottleneck.
- `DeliverWorkGiver.NearestLoad` (`BuildJob.cs:91`) learns containers.
- **Groups** are now a repoint, because of decision 17: select two, press Link, both hold one
  `SettingsId`. `StorageSettings.Name` becomes visible, which is the contract change §5c flagged —
  the first string in a snapshot view.
- **Presentation:** a fill tell using `ItemHeap`'s existing ramp rather than a new tier system.
  Inspect reads `Wood × 400 of 600`, the `× n` form `docs/design/24-pile-reading.md` §2 settled on.
- **Watch the click plane.** A crate standing proud of the floor has the bed's bug — the picker
  resolves a cell at the floor plane while the thing stands 0.70 m up (`docs/bug-patterns.md`,
  `docs/design/20-beds.md` §13).
- **A crate is an edifice, so placing one *is* a `NavGraph.Rebuild`** — the 1.19 ms one-cell edit
  the audit measured. That is S2's scaling row, and it is HT1's to fix, not storage's.
- `docs/design/26-cold-storage.md`: the food rule, spoilage as a per-stack timer, how a cooled store
  hangs off M4's temperature work. Nothing built.

---

## §8 — S3 `U52`: haul urgently

### 8a. Making `Emergency` mean what the plan assumed

`WorkThinkNode.TryGiveJob` is four lines and they decide the whole of this:

```csharp
for (int priority = 1; priority <= 4; priority++)
    for (int i = 0; i < _givers.Length; i++)
        if (pawn.WorkPriority(_givers[i].WorkType) == priority && _givers[i].TryGiveJob(...)) return true;
```

`SortGivers` puts emergency givers first **in `_givers`**, which reorders them inside a single
priority pass and nothing more. Haul has `order = 3` in `WorkTypes.xml`; a colonist with Mining at
1 and Haul at 4 reaches the urgent giver on the fourth pass, after every mining order on the map.
"Everyone drops what they're doing" is not what the flag buys, and since no giver overrides it
today the flag has never been exercised by a single test.

**The change** — small, and in the think tree rather than in storage:

```csharp
// Ahead of the 1..4 loop: emergency givers, for any work type the pawn will do at all.
for (int i = 0; i < _givers.Length && _givers[i].Emergency; i++)
    if (pawn.WorkPriority(_givers[i].WorkType) > 0 && _givers[i].TryGiveJob(pawn, ctx, job)) return true;
```

The loop can break on the first non-emergency giver because `SortGivers` has already grouped them.
Priority 0 still means "never", so a colonist with hauling switched off is not conscripted — which
is the line between "urgent" and "forced", and a forced order has its own intent.

**This changes the think tree, so it moves goldens** unless no golden colony has an emergency
giver — and none can, because none exists until S3. Assert that: the golden re-bake for S3 should
be empty. If it is not, the pre-pass has changed something it should not have.

**The invited tuning stands**: if urgent hauling reads as chaos, the fallback is `IntraPriority`
inside Haul, which is a one-line retreat.

### 8b. The rest

- `DesignationKind.HaulUrgent = 4` (`DesignationGrid.cs:14`; None/Mine/Deconstruct/Fell are 0–3).
  Reuse the designation layer — dense byte plus sorted sparse list, saved, hashed, published as
  `OrderView`, with a drag tool already. Urgency is per cell and clears when the cell empties.
- One behaviour to name because it will be reported otherwise: the mark is on the **cell**, so a
  cell whose urgent item is taken and which later receives a different stack makes that stack
  urgent too. Either the driver clears the designation on pick-up (cheap, and what "clears when the
  item is stored" means literally) or the mark is on the thing. Cell is right for v1 — it is the
  layer that exists — and clearing on pick-up is the behaviour to implement.
- `UrgentHaulWorkGiver` — `WorkType = Haul`, `Emergency => true`, scans `designations.Cells` for
  the kind, reuses `BestStorageSlot` unchanged.
- `PaletteTools.Pinned` gains `HaulUrgent`; `ui.arch.tool.haulurgent` already exists
  (`icon-keys.csv:157`, "Jump this to the top of the haul list").
- **The "No storage" alert**: an `AlertModel` row for a colony with loose haulables and no zone
  that accepts them. The wording `AlertModel.cs:22` uses as its own doc-comment exemplar — "No
  stockpile — *salvage lies where it fell*" — becomes a real row and needs a registry key.

---

## §9 — Files that change

| Area | Files |
|---|---|
| New sim | `Sim/World/ZoneGrid.cs` (S0), `Sim/Storage/StorageSettings.cs`, `Sim/Storage/StorageZones.cs`, `Sim/Storage/StorageUnits.cs` (S2) |
| Sim edits | `Sim/Growing/GrowingZones.cs` (S0), `Pawns/ColonyItems.cs`, `JobSystem.cs`, `JobDrivers.cs`, `BuildJob.cs`, `ReservationManager.cs`, `PawnContent.cs`, `PawnContext.cs`, `ColonyWorld.cs`, `ColonyComposition.cs`, `ColonyScenario.cs`, `Designations/DesignationGrid.cs` (S3), `Saving/SaveFormat.cs` |
| Contracts | `Intents.cs`, `Views.cs`, `Catalogue.cs` |
| New HUD | `Hud/StorageSettingsModel.cs` |
| HUD edits | `DesignateDirector.cs`, `PaletteTools.cs`, `HudTheme.cs`, `InspectModel.cs`, `OrderColours.cs`, `AlertModel.cs` (S3) |
| Presentation | `Rendering/ChunkBatch.cs` (the tint code), `Rendering/ChunkMesher.cs` (two emit sites), `Rendering/ChunkRenderer.cs` (the grade), `World/WorldRenderModel.cs` (the mirror), `Bootstrap/DesignatePresenter.cs`, a crate module (S2) |
| Content | `Defs/Core/Pawns/Items.xml`, `Defs/Core/World/Buildings.xml` (S2), `docs/design/icon-keys.csv`, `icon-map.csv` |
| Docs | `docs/design/26-storage.md`, `26-cold-storage.md` (S2), `docs/journal.md`, `docs/bug-patterns.md`, `docs/plans/vertical-slice.md`, `docs/plans/playtest-queue.md`, `CLAUDE.md` status |

## §10 — Things not to undo by tidying

- **The ground stays one stack per cell.** The container inventory exists so that rule is never
  touched; a "simplification" that makes cells multi-slot re-opens six call sites.
- **One destination rule.** `BestStorageSlot` has one owner. Do not add a container-specific path.
- **The anchor decides, and two zones never merge.** A later reader will notice that
  `GrowingZones` merges on touch and "make them consistent". They are not the same rule: a growing
  zone is identified by its plant, a storage zone by a configuration that a merge would destroy.
- **A zone's settings are a handle.** Two zones sharing a record is a group; two zones holding
  equal copies is a bug waiting for a save.
- **Nothing presentation-side enters a cell, a save or the hash** — the storage tint included.
- **Do not answer `RegistryTests` by rewording a literal.** Call `Registry.Label(key)`.
- **Equal priority is not better** (`JobSystem.cs:771`): two piles at one priority are one
  warehouse in two places, and shuttling between them is the failure `a-14` §3D warns of.
- **Never hand-edit `docs/wiki/`.**

## §11 — Verification

Per branch, in this order:

1. `scripts/test-fast.sh` — baseline **806 Sim + 471 Hud**, Long 21. New suites: `ZoneGridInvariantTests`
   (S0), `StorageSettingsTests` (presets, the unknown-def default, priority), `StorageZoneEditTests`
   (anchor extends, anchor founds, transfer out of another zone, subtract, dissolve-when-empty,
   no-overlap-by-construction), `StorageLoadV6Tests` (the legacy drain, including an overlapping
   pair), `StorageUnitTests` (capacity, spill with nowhere free, the deconstruct refusal, food
   refusal beating a ticked filter), `HaulUrgentTests` (an urgent item beats an ordinary one **and**
   beats a mining job for a pawn who ranks mining first — the control that would have caught §0
   row 11), `RegistryTests` for the new keys.
2. `python3 tools/wiki/build_wiki.py --check` and `tools/wiki/emit_labels.py --check`.
3. `scripts/unity.sh test editmode` — authoritative, baseline EditMode **2,003 / 1,990 / 0**,
   PlayMode **85 / 80 / 0**. The fast tier compiles neither Presentation nor Editor, so the tint
   and the crate module are unproven until this runs.
4. `dotnet run --project tools/dotnet/Odyssey.SaveProbe` on a save holding two zones and a full
   crate, to confirm the round trip by inspection and not only by hash.
5. **The ten-day headless soak on three seeds**, with the economy compared against the previous run
   rather than merely checked for errors. This is where a re-stow loop or a reservation leak
   surfaces and a two-minute test does not.
6. `scripts/unity.sh build`, then `Build/Win64/Odyssey.exe -odyssey-newgame -logFile <path>`. Two
   green tiers say nothing about whether the player runs.
7. Golden re-bake in S1 only, **measured** as §5e describes. S0 and S3 must move no golden at all,
   and that is an assertion rather than an expectation.
8. **Scale, per `docs/process.md` §3, and the list is corrected.** The three numbers:
   - **the chunk remesh of a zone edit** — paint 30 × 30 in one drag at 250 × 250 × 40 and record
     the frame it lands in (S1; §6). *This replaces the plan's `NavGraph` claim, which was wrong.*
   - **the haul destination scan** — `BestStorageSlot` with a 1,600-cell warehouse and 200 loose
     items, before and after §5f's two changes (S1).
   - **`NavGraph.Rebuild` on a crate placement** — the audit's 1.19 ms one-cell edit, which is
     S2's and is HT1's to fix (S2).

Each branch ends with the handover `CLAUDE.md` requires: the full path, the branch, the PR, whether
Synty is junctioned, then the **what changed** table and the **what to test** table — the latter
holding only questions a person at the keyboard can answer, each with what a wrong answer looks
like. **Its playtest rows go into `docs/plans/playtest-queue.md`.**

## §12 — Merge order

| | What | State | Why this order |
|---|---|---|---|
| 1 | ~~**GR** growing zones, PR #119~~ | **merged** `a45b4ec` | Discharged. |
| 2 | **S0** the zone container | this plan, not started | Behaviour-preserving, and it is cheapest the moment after #119 lands and before a second zone kind exists. |
| 3 | **HT1** the navigation rebuild is local | plan written (PR #136 §HT), not started | **Not a block on S1** — §0 row 12 established that a zone edit never touches the nav graph. It **is** the thing S2 wants first, because a crate placement is exactly the 1.19 ms one-cell edit HT1 exists to fix. |
| 4 | **S1** zones and the storage control | this plan | |
| 5 | **S2** storage units | this plan | Wants HT1 in front of it; will proceed without and say so. |
| 6 | **HT6** the busy-colony arm | plan written, not started | Its number "decides whether per-region item listers are built", which is the same question §5f's measurement asks. Run it after S1 so it measures a colony that has storage. |
| 7 | **S3** haul urgently | this plan | Carries the think-tree change (§8a), so it wants a quiet week rather than a crowded one. |

Four other branches are in flight and two touch these surfaces: **#143** (roofing) edits the roof
rule `GrowingZones.SiteAllows` reads, and **#145** (the Work tab) edits work types and the HUD
shell. `DesignateTool` and `BuildingHandle` are append-only and their next free numbers must be
re-read at merge time, not taken from this file.

## §13 — Still open

- **Decisions 11 and 12** (the five priority names, the six item categories) await veto. Both
  become wiki content and both are read by the filter.
- **Decision 21** reverses decision 5 (the category tree in S1) and is the owner's to take.
- **§7c** asks whether crates should accept food in S2, which reverses part of decision 8.
- **The dumping zone.** `ui.arch.tool.dumping` is already a palette chip and decision 12 already
  has a Dumping *preset*. One of the two should go: a preset that arms a second tool is two ways to
  say one thing, and this file has now removed three of those.
