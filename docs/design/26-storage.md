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

## 9b. The design brief, 2026-09-21 — the four questions, and five colours

The owner commissioned a full interface specification for the pane and handed it back. It asks
four questions before building, and they are answered here rather than left open.

| | Question | Answer |
|---|---|---|
| 1 | *Allow all / Clear all and Everything / Nothing are the same two actions under two sets of words.* | **Agreed: the preset chips are gone.** The header buttons win because they sit where the list they act on begins, and because a chip row costs 26 px of a pane with a 640 ceiling to say a second time what two text buttons already say. `StoragePreset` stays in the simulation — every zone is founded at *Everything* — and stops being drawn. |
| 2 | Can the six categories grow? | **No.** `ItemCategory` is an enum in `Sim.Contracts`, fixed at six by decision 23. The category strip needs no scroll of its own and the sticky-header rule is not load-bearing. |
| 3 | What happens when a store is full, and does priority affect **retrieval**? | **Priority is deposit-only, and that is checkable rather than asserted**: it is read in exactly two places, `BestStorageCell` (which destination) and `StoredPriority` (what a re-stow must beat). Nothing consults it when *taking* — the eat scan and the build-delivery giver choose by distance. A full zone simply stops offering cells (`CellHasSpace` fails per cell), the load goes to the next band down, and with no band left it lies where it fell. The pane needs to show neither today; the "No storage" alert is S3. |
| 4 | Can a zone ever appear in the 280 px pane? | **No.** 280 is the bare-tile variant: a tile click with no zone keeps it, and a zone always opens the 560. |

### 9c. Five of the brief's colours failed its own acceptance criteria

The brief sets two floors — **4.5:1** for a label against the panel, and *distinguishable with the
labels masked*. Its palette was measured against both rather than trusted, by
`StorageThemeTests`, and five values did not clear them:

| Value | Was | Measured | Is | Why |
|---|---|---|---|---|
| Last | `#6b737a` | **2.71:1** | `#c6c9cb` | The hue is a *label* colour when a rung is selected, so it has to read as text. Lightening it towards slate then left the two 41 points apart — under the 60 the order hues are held to, and they are adjacent rows — so it went the other way: a pale neutral, far from slate, reading as inactive, which is what the bottom rung means. |
| Low | `#7f9ab0` | **4.45:1** | `#8ca6bb` | Near enough to pass by eye; not near enough to pass. |
| Medicine | `#d95a6a` | **3.50:1** | `#f086a8` | Dark saturated reds are the hardest thing to read as a label on a dark panel, and these are labels. Pushed pink rather than lighter red, because lightening alone put it 51 points from Weapons. |
| Materials | `#b0793f` | **4.08:1**, and 55 from Weapons | `#c4a05a` | Failed both floors at once. |
| Weapons | `#c85a3f` | **4.03:1** | `#e88d66` | |

Food `#7fb85a`, Books lightened to `#bb94dd` for the same reason, Normal, Preferred and Urgent
stand as written. **The test is the record**: every pair of categories and every pair of rungs is
held to 60 channel-points, and all eleven to the contrast floor.

**And one of the brief's claims about its own palette is not true.** *"Cool to warm as urgency
rises"* does not hold step by step — Normal's cyan is cooler than Low's slate by red-minus-blue,
because cyan gets its presence from brightness rather than from warmth. Rather than bend five
colours to satisfy a metric nobody looks at, the test asserts what is both true and meaningful:
**the two urgent rungs are warmer than all three unurgent ones**, and the split falls exactly
where the meaning does.

### 9d. What is built, and what is not

**Built:** the whole decision layer — `StorageSettingsModel` — and the palette. Registry order for
categories and alphabetical order inside them, capitalised display labels, the tri-state cycle with
the **remembered mixture** (a mis-click must not destroy a hand-built selection), Allow all / Clear
all with Clear dimming at nothing-accepted, the search that turns itself on above twenty flattened
rows and marks matches **at their real offset**, the match count, the no-match copy, the footer, the
warning band, and empty categories that keep a live box and grow no caret. Twenty-seven fast-tier
tests, which is where most of the brief's acceptance list can actually be checked. Plus
`HudGlyphKind.TriState`, the one mark the set did not have.

**Written before the pane was built, and left standing for two days after it was** — the frame,
the title row, the tab strip with **Tile** second, the scrolling viewport, the search field and the
collapsed state all landed in `e575ce32` and `079b1c05` on this same branch, and the popover they
replaced is gone. What is still owed: **zone selection** (`InspectSubject` gains a fifth value), the eight-hue rotation with no
two touching zones alike, the name plate, and the selection tint at 34% — none of which may cost a
per-cell draw (§6, and the brief agrees).

A zone's title is `Stockpile N` by **cell order** — how many zones begin at a lower cell — so it is
the same answer on both sides of a save with nothing written down, at the price of renumbering when
an earlier zone is deleted. A typed name is the fix and it is the unit after the pane.

## 10. What S1 does not do

- ~~**No panel yet.**~~ **Built** — the Storage tab of the inspect pane, on this branch
  (`e575ce32`), and the popover it was going to be is gone. Struck through rather than deleted
  because this list and §9d both said "not built" for two days after it was, which is the failure
  mode `CLAUDE.md` warns about at the top of its status section.
- **No containers.** `ColonyItem.ContainerId` exists and nothing writes it: it is in S1 so the item
  record's byte layout changes once rather than twice. Crates are S2.
- ~~**No category tree.**~~ **Built** — decision 35 overturned 21 and 31, and the six category
  rows expand to their commodities. `CategoryRow.State` carried the three-way answer already, so it
  was the layout change this bullet predicted.
- **No "no storage" alert**, and no haul-urgently: S3.

## 11. What a store does with what it refuses — 2026-09-21

The filter was a rule about what could be carried **in** and said nothing at all about what was
already lying there. The owner painted a store, set it to meals, and reported both halves of the
consequence in one sentence: *"the colonists left the rocks already there and left the meals not
hauled out in another place … I expect the colonists to ensure that all those tiles are occupied
by meals or nothing, not leave rocks in there."*

**They are one fault, not two.** A cell holding a rock has no space for a meal
(`ColonyItems.CellHasSpace` requires the same def), so every cell the refused things squat in is a
cell the store cannot use. Fill a small store with rocks it will not take and it accepts nothing
ever again. Measured on the bare fixture before the fix: a two-cell meals-only store with a rock
in each took **0 hauls in 10,000 ticks** and the meal on the grass never moved.

### 11a. Why it survived a filter that already knew the answer

`HaulWorkGiver.StoredPriority` has said since it was written that *"a thing lying in a pile whose
filter no longer accepts it is not stored at all, only in the way"*, and it returns the implicit
unstored rank for one. That was enough to let a refused thing move to a store that **would** have
it, and not enough for anything else, because of where it was asked:

| | |
|---|---|
| **The lister it was in** | `ColonyItems` buckets loose against stored by whether the cell is inside a zone — the question it can answer in one array read. A refused thing is bucketed *stored*. |
| **The pass that walks that lister** | the re-stow, and `TryGiveJob` runs it only when the loose pass has found nothing, because *tidying is the lowest job there is*. A colony that is felling or mining always has something loose. |
| **What happened when no store would take it** | `dest < 0`, `continue`. There was no third answer. |

So the thing was scanned last, if at all, and when it was scanned the scan had nowhere to send it.

### 11b. The fix, in two halves

**A thing its own store refuses is scanned with the loose things.** Not re-bucketed — that was the
other candidate and it is the worse one, because `ColonyItems`' buckets would come to depend on the
filter table, so ticking one commodity would have to walk a zone's items and the save would have to
agree about which lister each thing was in. Instead the first pass walks both listers and takes
from the stored one only what is refused; the re-stow pass takes only what is accepted. One extra
branch in a loop that was already there, and `Refused` is two array reads.

**And when no store will have it, it is carried out to open ground** — the clause that already
existed for a thing standing on tilled soil, now reached by both cases through one predicate,
`HaulWorkGiver.InTheWay`. It goes to the nearest cell within `ClearanceRadius` (12, one constant
for both cases now) that neither wants to be empty nor refuses it, and becomes an ordinary loose
pile there: the stockpile's business again the moment one has room.

### 11c. The second fault, which was the same fault from the other end

`PawnContext.NotZoned` was the predicate that clearance searched through, and it knew only about
**growing** zones — while both of its call sites said in their own comments that they wanted ground
*"outside every zone"*. A rock lifted off a field could therefore be set down inside a meals-only
stockpile, where nothing would ever pick it up again. One line of code, reachable without the
player ever narrowing a filter.

It is `PawnContext.OpenGroundFor(defIndex)` now, and it asks about the **thing** as well as the
cell: a store that *accepts* the thing is not excluded, because that is a home rather than an
obstruction — and the destination scan would have chosen it first anyway.
`StockpileTests.OpenGroundIsNotAStoreThatRefusesTheThingButMayBeOneThatWantsIt` is the rule on its
own; `GrowingJobTests.AFieldBlockerIsNotClearedIntoAStoreThatRefusesIt` is it end to end.

### 11d. What is deliberately not done

- **Open ground is the last answer, not the first.** A store that accepts the thing wins, and
  `AThingAStoreRefusesGoesToAStoreThatWantsItRatherThanToTheGround` is the guard — a rock evicted
  onto the grass beside a rock store is the obvious way to get this wrong.
- **Nothing is re-checked on a filter edit.** The haul scan asks the filter afresh every think, so
  narrowing a store is what sets its contents moving with no notification anywhere.
  `NarrowingAStoresFilterIsWhatSetsItsContentsMoving` asserts exactly that, because a future
  optimisation that caches the answer would break it silently.
- **Where even twelve cells finds nowhere, the thing stays.** A board packed that solid is one
  nothing in the game can produce, and the alternative is a hauler walking the map for a rock.
- **The clearance fallback does not check reservations**, unlike `BestStorageCell`. Two haulers
  evicting at once can pick the same cell; the loser fails its reservation and re-scans, and by
  then the winner's load is down and the search moves on. Transient, not a livelock — but it is
  why the unfixed predicate produced *no* clearance rather than a wrong one when the store cells
  were all claimed by meal hauls, which is how the regression test was proved to bite.

### 11e. No golden moved, and that is a gap rather than a reassurance

The full fast tier (922 Sim, 625 Hud) and the Long tier (23) are green with no re-bake. That is
correct — the behaviour changes only where a store is holding something it refuses, and **every
zone in every golden is founded at *Everything***, so the state never arises. It also means the
goldens do not cover this at all, and the six tests in `StockpileTests` under *what a store
refuses* are the whole of the coverage.

## 12. The pane's first look — 2026-09-21

Four things from one session at the keyboard, and a fifth the first of them uncovered.

### 12a. A message about what you just did must not move what you did it with

> *"When I clicked off all the categories a message appeared about colonists ignoring the zone —
> but this moved the controls/components — these should stay fixed — make the error message appear
> below the stockpile component."*

The two warning notes were pushed in as the first children of the scrolling list, so the moment the
last category came off, every category row dropped by the height of the band — under a cursor that
was working down them. **Moving it below the list was not enough on its own, and the test is what
said so.** The inspect panel is anchored to the *bottom* of the screen and grows upward, so a band
added at the end pushed every control **up by 90 px** — the same fault in the other direction, and
a worse one, because the rows moved further and the way nobody expects.

So the band's height comes out of the **list** rather than out of the screen:
`StorageWarningHeight` is both the height of the band and the height the scroll view gives up to
make room for it, and the pane is the same height whether the band is there or not. The list's top
edge does not move, the rows in it do not move, and the only thing that changes is how much of the
list you can see at once — which is what a scroll view is for.

**92, measured rather than reckoned.** Two sentences and the rule above them come to 77.1 px at the
pane's width, plus 12 px of the band's own padding. A first guess of 64 clipped the hint, and
`ZoneInspectTests.ClearingEveryCategoryDoesNotMoveTheRowsThatDidIt` is what caught it — the same
test asserts the band's contents still fit, so the constant cannot quietly become a clip when
somebody rewords the sentence.

### 12b. One action should not have two names

> *"Nothing and everything is the same as allow all and clear all — so remove nothing and
> everything if this makes sense."*

It makes sense, and §9b Q1 had already decided it — *"Agreed: the preset chips are gone"* — and the
build did not follow. Worse, the code **knew**: the chip handler carried a comment saying the two
were the same two actions as the header buttons and routed through the same two model calls "rather
than a third path that could drift from them". A comment explaining why a duplicate is safe is a
duplicate nobody re-examined.

The chips are gone. `StoragePreset` and `StorageSettingsModel.PresetKeys` stay: a zone is still
*founded* at Everything and the simulation still names the presets. Nothing draws them.

### 12c. Two closes and a square that does nothing

> *"The x button appears twice in the control — keep the one in the very top right, the square icon
> next to it does nothing."*

A store added two header buttons of its own: a disabled Rename, drawn as a placeholder square to
hold a place for named stores, and a Close — six pixels from the Close every pane already ends
with. Both are gone. **An affordance for something that does not exist yet is worse than a gap**:
the square told its story in a tooltip nobody hovers and read as a broken button, and the second
Close asked the player to choose between two identical things. Named stores bring their own control
when they bring the name (§9a, SZ4).

### 12d. The one number on a row was the smallest thing on it

> *"Make the numbers bigger in the stock control component."*

The member count was `Meta` — 12 px, the step for a qualifying aside, and the smallest text in the
pane — sitting beside a 14/600 heading, so the row's only figure read as a footnote to its own row.
It is `Row` now, 14/500, the same step as the heading it answers to, with `numeric` set so it takes
the mono face and tabular figures and a column of counts lines up whatever the digits are. Both
come from the shared scale; no size is written in the pane.

### 12e. And the one the test found: every press was an action stale

`SendStorageCommand` submits an intent and then refills the rows on the spot, on the strength of a
comment saying a storage intent *"applies while paused, so the answer is already true by the time
the next frame draws"*. **True while paused, false the rest of the time.** Unpaused, the intent
queues for the next tick, so the synchronous refill reads the state the player has just changed
away from — and nothing refilled the pane again, because nothing else ever did.

Measured by asking both sides after a press of Clear all with the game running: **the simulation
accepted 0 of 7 commodities and the pane was still showing all seven ticked, with no warning.** A
press that makes no visible difference is indistinguishable from a button that does not work.

`SyncStoragePanel` covers the other half, hung off the refresh that already runs fifteen times a
second, and rebuilds only when a signature — the zone, its rung, its cell count and its filter —
has actually moved. Not an unconditional refill: thirteen elements of garbage a frame for a panel
that changes when a person presses something is the fault the Work tab was pooled to avoid. The
synchronous refill stays, because on a paused board no tick is coming to catch it.
