# Storage: zones, what they accept, and where a load goes

*Built 2026-09-20 as **S1** of the storage line (`docs/plans/storage.md`). The plan holds the
interview, the three branches and the rejected alternatives; this holds the decisions the code
made, the numbers that were measured, and the things not to undo by tidying.*

## 1. What S1 is

The colony had a working storage **simulation** and no storage **game**: `Stockpile` carried a
priority, a cell set and a filter, `HaulWorkGiver` implemented *accepts → space → priority →
nearest*, and nothing in the interface could touch any of it. The scenario placed a nine-cell zone
at tick zero that was **invisible** and uneditable — every colony ever played had one and nobody
had seen it.

S1 makes it a thing a player draws, sees and owns: a **stockpile tool** in the orders strip, a
zone painted by a drag, drawn on the ground it covers, saved, hashed, and carrying a priority and
a filter that the simulation reads on every haul.

**And a colony now starts with no store at all** (owner, 2026-09-20, on seeing the first build:
*"there shouldn't be a default stockpile zone"*). `ScenarioDef.stockpileCells` is nought. That is
the same call as `ScenarioDef.Playtest` giving no starting orders in 2026-09-17, and it only became
askable because S1 made the zone visible: a default nobody could see was a default nobody could
object to. The machinery stays — a scenario may ask for a store and a "prepared site" start
certainly will — so it keeps its test, with a scenario that asks.

## 2. The anchor decides, and two zones never merge

The plan's decision 9 said *a drag touching an existing zone extends it*. On the substrate that
exists — `ZoneGrid`, extracted in **S0** — that would have meant `GrowingZones.Paint`: a cell joins
any eight-touching zone, and a stroke across two zones' corner **folds them into one**.

That is sound for growing and wrong for storage, and the difference is what a zone *is*. A growing
zone is identified by its crop, so two touching carrot fields are interchangeable and a fold loses
nothing. A storage zone carries a configuration, so a fold destroys one of two filters with nothing
said and nothing to undo it. It is also not what a player means: two stockpiles that happen to
share an edge are two stockpiles.

**The rule:**

| The drag starts | What happens |
|---|---|
| inside storage zone `Z` | every acceptable cell in the box joins `Z`, at `Z`'s own settings |
| anywhere else | the first acceptable cell founds a new zone with a fresh settings record; the rest join it |
| over cells of another zone | those cells **transfer** — removed from the old, added to the target. The old zone dissolves if it empties. |
| right-drag, or the cancel tool | each cell leaves whatever zone holds it |

Two existing zones never merge. **Overlap is then impossible by construction**, which closes a
fault `ColonyItems.AddStockpile` had carried since it was written: it wrote `_stockpileAtCell[cell]
= index` unconditionally, so the last zone to claim a cell won and nothing was rejected.

### 2a. How the anchor reaches the simulation

`Intent` is a flat value struct and the rectangle is submitted one intent per cell, so the anchor
rides in the payload: `DesignateStorage(cell, A = anchor cell index, B = preset)`. A cell index is
data with a meaning — the handler resolves it against the zones it has — where a minted drag id
would mean nothing outside the session.

**`DesignateDirector.LastAnchor`, not `cells[0]`.** The committed cells come back in grid order, so
the first of them is the box's minimum corner and is the *head* whenever the drag ran up or left.
Using a corner would make which zone you extend depend on which direction you happened to drag in.

The handler keeps **one field**, not a map: `(Anchor, Slot)`. In the ordinary drag the anchor cell
is itself painted, so from the second cell onward the grid answers and the memo is never read. It
earns its place only when the anchor is *refused* — a box begun on water, a wall, a tree — and some
later cell has to found the zone the rest join. The case it cannot tell apart is two separate drags
begun from the same refused cell, which become one zone: a worse answer than "two zones" and a much
better one than "one corrupt zone".

## 2b. Which cell a store actually lives in

**A player can only ever click a surface, and a store does not live on one.** Things rest in the
walkable cell a colonist stands in. Over open ground that cell is the **air above** the solid grass
the pointer hit; over a built floor it is the cell whose lower boundary the slab is, which *is* the
cell the pointer named.

So the rule is not a lift, it is a question — `StorageZones.StoreCellOf`: **solid terrain answers
for the cell above it, everything else answers for itself.** Every way in goes through it:
designate, cancel, set-priority, set-filter, and the cell-detail publisher. One owner, because the
pane must not say a cell is a store that the tool would refuse.

**This was reported the day S1 landed** (owner: *"I used the stockpile order and was able to
highlight but then let go to place, nothing happened"*). Every cell of every drag on open ground
arrived as the solid grass cell, `SiteAllows` answered "not walkable", and the whole rectangle was
refused one cell at a time — visible only as a wall of `NotPermitted` warnings in the log.

**Why it could not be solved in the tool, where the growing zone solves it.**
`DesignateDirector.OnTheWorkingLayer` lifts a grow-zone cell by one *unconditionally*, and that is
right because nothing grows through a slab, so the pointed cell is always soil. A store's commonest
home is a wooden floor indoors, where that same lift would put the zone in the air a storey up. The
question needs the grid, and the tool is deliberately Unity-free and grid-free.

**The one-step-up rule now exists in three places** — the grow tool, the cell-detail publisher and
here — and this was the third time it was needed and the first time it was missing. That is the
shape `docs/bug-patterns.md` catalogues first, and it is worth a fourth reader asking whether the
three should become one before adding a fifth.

## 3. The siting gate

A store is a place to put something down, so the question is only whether something *can* be put
down there. Far looser than a growing zone's gate: bare rock, a wooden floor, a paved street and
the inside of a room all qualify. Refused are **water** (a stack in a stream is not stored, and
wadeable water is walkable, which is why it is asked apart from `IsWalkable`) and **a cell with
something standing in it**.

**That last one moved a golden before the default zone was taken out, and the finding is worth
keeping.** Of the nine cells the scenario used to hand the starting zone on the played board, cell
180436 at (76, 63, L12) has a **tree** standing in it — walkable, not water, edifice 753.
`AddStockpile` asked nothing of a cell, so that cell was in the zone and a starting item that
landed on it counted as *stored* in a place nothing could ever be stored. It is moot now that no
colony starts with a store, and it is recorded because it is exactly what a player painting over a
tree will meet: the cell is refused, and the zone has a notch in it until the tree is felled.

## 4. A configuration is a handle, not an object

`StorageSettingsTable` owns the records; a zone holds an `int SettingsId`.

Sharing by C# reference is invisible to the save file and to the hash — two zones pointing at one
object serialise as two records and come back as two, so a storage group would quietly dissolve
across a save. An id serialises as an integer that is equal on both sides, which makes **S2's
"Link" a repoint of one integer** rather than a rewrite.

Ids are slot positions and are **never reused**, the `PlacedEdifice` contract for the same reason:
a reused id is a zone that silently adopts a stranger's filter.

### 4a. What a commodity added later gets

A filter is `bool[]` by item def index, saved with its own length. A save written before a
commodity existed reads short, and the old code answered **false** for every def past the end, for
ever, silently. The two cases a file cannot tell apart are a zone created with *Everything* — where
the player means the new thing to go in — and one created with *Nothing* and two rows ticked, where
they do not.

So the answer is **authored**: `StorageSettings.AllowUnknown`, written with the record and set by
the preset. `Load` builds the array at the current def count, copies what fits, and fills the tail
with it. A per-def tick does not move it; a preset does, because a preset is a statement about
kinds of thing rather than about the seven defs that exist this week.

## 5. Where a load goes

`BestStorageCell` walks **priority bands, high to low, and stops at the first that yields**.
Priority dominates distance — nearest only breaks ties *inside* a band — so once a band has
produced a reachable cell, no lower band can win.

That matters because S1 is what makes big zones possible. The old scan was O(every cell of every
zone) per candidate item, and it was never measured, because the meadow benchmark has one zone of a
handful of cells. A painted warehouse turns a 40-cell scan into a 1,600-cell one on every think.

Two guards inside the band, in this order: the cell must have space for the **whole** load, then
the distance is computed (two array reads) and compared, and only a cell that could still win pays
for the reservation, the nav flag and the region lookup.

**The floor is `int.MinValue`** — the implicit "not stored at all" rank `a-14` §1 infers — so the
loop needs `priority >= 0` as well as `> abovePriority`, or it counts down two billion times. That
was a real hang, found by the test suite timing out rather than failing.

## 6. Drawing it

**A bit in the bucket key of a surface that is already being drawn** — `TintCode.StoredBase`, the
same mechanism `TilledBase` uses for a field. Zero extra draw calls; the cost is buckets, not
submissions. `docs/bug-patterns.md` P10 is why the alternative was never written: a pass that draws
once per cell cost the growing zone 2,065 draw calls and 3.67 ms of a 5 ms budget before it was
replaced.

**It could not simply be copied, for two reasons.** The tilled bit is applied to the *terrain quad
of the cell below*, and a growing zone is refused on a slab — so `EmitTerrain` was the only site it
ever needed. A storage zone's commonest home is a wooden floor indoors, where no terrain quad is
drawn at all. So the bit is set at **two** emit sites, asking two different questions:

| Site | Asks | Because |
|---|---|---|
| `EmitTerrain` | `IsStoredAbove(index)` | a solid ground cell's quad is drawn for the cell *below* the one a pawn walks in |
| `EmitFloor` | `IsStoredHere(index)` | a slab is drawn at the lower boundary of the walked cell itself |

And it **grades rather than swapping**. `DrawnTerrain` answers bare earth for a growing zone,
because a field is soil somebody turned over; a store changes nothing about what the ground is, so
the wash sits over the surface and leaves stone reading as stone and planks as planks —
`StoredWash` 0.33 towards `StoredGrade`.

### 6a. The order's colour and the result's are the same one, here

A growing zone's chip is the Zones brown and its committed ground is tilled earth, deliberately:
the field is the *result*, exactly as a built wall is not the blue of its blueprint. A store has no
result — the ground under a warehouse is the ground it always was — so the wash **is** the standing
order, still in force, and wears the tool's own `OrderColours.StoreHue`.

Desaturated on purpose. A mine order is worked off and a field becomes a crop, but a warehouse
floor is a warehouse floor for the rest of the colony's life, and a saturated hue over fifty cells
for a hundred hours is a screen the player stops seeing past.

## 7. The golden re-bake, measured

Every golden moved. The hash sees a different shape — an item record gained `ContainerId`, and the
zones left `ColonyItems` for two sections of their own — and that alone moves `Generated` before a
tick runs. The question the WS2 precedent forces is whether the **colony** changed.

A throwaway probe printed what each colony does — live things, per-def stacks, the sum of item
cells, the loose and stored lister counts, the sum of pawn cells, total food and rest, standing
orders — on this branch and on `main`, at generation and after the full run:

| Case | Verdict |
|---|---|
| meadow, 5,000 ticks | **identical in every number**, generated and simulated |
| ruined city, 10,000 ticks | **identical in every number** |
| played board, 10,000 ticks | `loose=18 stored=2` against `main`'s `loose=17 stored=3` |

The one difference is §3's tree, and the meadow and the city are unchanged precisely because
neither has a tree in its starting zone. `Golden.cs` carries the same paragraph where a re-baker
will read it.

**They were then re-baked again the same day, and that one is a rule change rather than a hash
change.** With no default store (§1) all three golden colonies have **nowhere to haul anything
to**: they fell, mine, eat and sleep as before and leave what they cut where it fell. A different
colony, and rightly a different number.

The rest of the starting kit was measured either side of that too: 5 colonists, 12 meals, 5 beds
and 8 salvage on the wooded board, identical. The ruined city places **7** salvage rather than 8,
because the scatter retries once per spot in the pool and the pool is nine spots shorter — a retry
artefact on the tighter board, not a space problem, and not worth engineering around for one piece
of scrap.

**Every soak and round-trip fixture whose subject is a working colony now asks for nine cells of
storage**, because a colony with nowhere to put anything never hauls and those tests measure
hauling. `SessionRoundTripTests` goes further and **draws** its store through the intent the tool
sends: it is the one test in the suite that exercises a painted store end to end — the player's
drag, the colony's haul, the save, and the same colony read back.

## 8. Things not to undo by tidying

- **The anchor decides, and two zones never merge.** A later reader will notice `GrowingZones`
  merges on touch and "make them consistent". They are not the same rule: §2.
- **A zone's settings are a handle.** Two zones sharing a record is a group; two zones holding
  equal copies is a bug waiting for a save.
- **`AllowUnknown` is authored.** Inferring it from the array is the fault it closes.
- **The band loop needs its zero floor.** §5.
- **Two emit sites, two questions.** §6. They are a layer apart and swapping them is invisible
  until somebody paints a store indoors.
- **Nothing here enters a cell, a save or the hash on the presentation side** — the wash included.
- **Do not answer `RegistryTests` by rewording a literal.** It fired on this branch, on the word
  "Food" in `HudShell.Inspect.cs`, the moment `ui.res.category.food` existed; the answer was to
  take the name from the registry, which the need rows should have been doing anyway.

## 9. The zone as an object — decided 2026-09-20, not yet built

The owner used the first build and asked for stockpiles to be *"treated as groups of tiles so you
don't have to click on individual tiles to change something"*, for the priority and the item types
to sit under *"their own distinct headers"*, and for a searchable, scrollable, categorised item
list — all of it *"more RimWorld"*.

**The first of those is already true and that is the finding.** A zone is one settings record shared
by every cell: changing the priority on any tile changes the zone, and `StorageZoneTests`
`ThePriorityAndFilterIntentsNameACellAndNotAZone` sets it on one cell and reads it back on another.
What is missing is that nothing *says* so. You click a tile, the pane is titled by the tile, the
panel hangs off a tile row, and two touching zones wash the same colour so you cannot see which one
you are editing. **It behaves like a group and reads like a tile**, and the fix is selection rather
than storage.

Four answers, taken 2026-09-20:

| # | Decision |
|---|---|
| 33 | **Clicking a store selects the zone**, not the tile. The pane titles it, the zone's cells brighten so its extent is visible, and the tile's own facts move to a second tab. |
| 34 | **The settings live in a tab of the inspect pane**, not in a popover: *Priority*, then *Accepts* with Allow all / Clear all, then the list. A popover is right for picking one of five colonists and wrong for twenty rows and a scrollbar. |
| 35 | **The list is categories that expand to items** — six tri-state category rows, an arrow to open one, individual commodities inside. This **overturns decisions 21 and 31**, which deferred the tree because four of six categories are empty; the reason to have it is structural and the owner has said so. |
| 36 | **The priority ladder stays at five.** Re-asked against the reference's six and re-confirmed: Last, Low, Normal, Preferred, Urgent. |

### 9a. The units that follow, in order

**SZ1 — the zone is what you select.** `InspectSubject` gains a fifth value; the picker resolves a
click inside a store to the zone; the pane titles it (`Store — 24 tiles`) with the tile's facts on a
second tab.

**The selected zone's cells brighten, and the cost of that is the interesting part.** The obvious
implementation — outline the zone — is a draw per cell, which is `docs/bug-patterns.md` P10 at about
4.6 µs a submission and is exactly the fault the growing zone's cover was deleted for. The answer is
the mechanism §6 already uses: **one more tint level on a surface that is being drawn anyway**, so a
selected zone costs one extra bucket and no extra draws. The price moves to a **re-mesh of the
zone's chunks when the selection changes** — which is a click, at human rate, bounded by the zone —
and that is the number to measure before SZ1 is called done.

**SZ2 — the Storage tab.** The pane's tab machinery exists and only colonists use it, so the work is
making a non-colonist subject carry tabs, plus the headers and the two buttons.

**SZ3 — the tree, the scroll and the search.** Expandable categories over a scroll view.
`StorageSettingsModel.CategoryRow` already carries the three-way state, so the model barely moves;
this is layout. **Search is built and hidden until the list is longer than the panel** — over seven
commodities a search field is furniture, and a control that appears when it starts earning its place
explains itself.

**SZ4 — rename, and copy.** A named zone needs a string in a snapshot view, which nothing carries
today, so it is a deliberate contract change rather than a smuggled field. **Copy and Link are two
verbs, not one**: Copy duplicates the values into another zone, Link points both at one settings
record — which is S2's storage group, already the reason settings are a handle (§4). Copy is S; Link
folds into S2.

## 10. What S1 does not do

- **No panel yet.** The four intents exist, are applied while paused and are tested, and
  `StorageSettingsModel` is the whole control — the rungs, the two presets, the six category rows,
  one row per commodity, and what every press means — Unity-free and covered by the fast tier. What
  is not built is the **popover in `HudShell`** that raises it from the inspect pane's storage row.
  Until it lands, a painted zone accepts everything at Normal, which is what a new zone is anyway
  (decision 22), and the pane says which rung it is on.
- **No containers.** `ColonyItem.ContainerId` exists and nothing writes it: it is in S1 so the item
  record's byte layout changes once rather than twice. Crates are S2.
- **No category tree.** Four of the six categories have no commodity in them, so the rows are flat
  (decisions 21 and 31). `CategoryRow.State` already carries the three-way answer, so the tree is a
  layout change when it comes rather than a model change.
- **No "no storage" alert**, and no haul-urgently: S3.
