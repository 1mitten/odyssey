# a-19 — Food rot

## Question

How should food rot work in Odyssey, and how does the reference game do it? Specifically: the rot
clock per stack, how temperature scales it, the stages, what becomes of rotten food, how rot
combines when two stacks merge, and what a refrigerator is for. Then, in this repository: every
place a stack is created, split or merged, and what saving and hashing a rot field would touch.

Owner decisions already made (not re-opened here): raw meat rots in about **2 days** and meals in
about **4 days** at room temperature; a **powered fridge** — a one-cell storage container like the
shelf, living in `StorageUnits` — **stops rot entirely while powered**; ration packs and carrots
**never** rot; rotten food is **inedible and hauled out**. Unpowered containers keep decision 24's
"faster" rule unless there is a strong reason not to.

Caps held: 7 web searches, 7 page fetches (two refused — 404 and 429), 9 repository file reads
plus greps. `docs/research/INDEX.md` is not updated here (parallel lanes write it); add the row.

---

## Findings

### A. The reference game

**1. The clock is per stack, in days at full rate.** Each perishable thing carries a single rot
progress figure and a per-kind "days to start rot". Measured in the wiki's own numbers: raw meat
**2 days**, simple meal **4 days** (the meal class generally), pemmican **70**, raw crops in the tens
of days (the growing guide's "about 40", berries around 14), organic corpses **2.5**, and the
packaged survival meal **never** — though it still *deteriorates* if left outside. `a-08` §5 had
the same table and is corroborated.

**2. Temperature scales the rate, and the curve is linear between 0 and 10 °C.** The Temperature
page states it outright: at or below **0 °C** "food spoiling stopped"; at 1 °C the factor is
**0.1**, at 5 °C **0.5**, at 9 °C **0.9**; at 10 °C and above it is the full rate. So the factor
is `clamp(T / 10 °C, 0, 1)`. This settles `a-08`'s open item ("the Food page's '1/temp' wording is
ambiguous") — it is linear, not reciprocal. There is no speed-up above 10 °C: a hot room rots food
no faster than a warm one.

**3. Rot and deterioration are separate mechanics.** The same page: *"Rotting isn't influenced by
and has no effect on an item's hit points"*; deterioration is hit-point loss outdoors, regardless
of temperature. A shelf in the reference protects against **deterioration**, not rot (`a-14` §6).
This is why decision 24's "a container spoils faster" does not contradict the reference so much as
occupy a space it leaves empty.

**4. Stages.** The player sees a countdown ("spoils in X days") and, in filters, an "allow
rotten" switch. **Food has no persistent rotten state in the base game**: when its clock runs out
it is simply deleted ("rotted away") — meat, vegetables, meals, milk, eggs alike. The only things
that linger as "rotting" are **corpses**, which pass fresh → rotting → dessicated and are what the
"rotten" filter switches actually govern. So the owner's "rotten food is inedible and hauled out"
**departs from the reference**, which never has to haul rotten food because there is none. That
departure is what creates Odyssey's one real design problem (§C3 below).

**5. Merging averages rot, weighted by count.** Two sources agree on the rule: `a-14` §4 records
that "per-item state is collapsed to a single averaged value — hit points and rot progress are
averaged across the merged stack", and a search-result summary of a community decompilation
(clean room: **the page was not opened and nothing from it is copied**) describes the absorb step
as a linear interpolation weighted by the incoming count over the combined count — i.e. a
**count-weighted mean**. One wiki page (Corpse) claims instead that "each individual piece of food
tracks its deterioration separately"; that contradicts both, would need per-item state the data
shape does not have, and is taken to be wrong. **"Most rotten wins" is not the reference rule.**

**6. Update cadence.** Rot is a per-day rate applied on an infrequent tick — the reference's "rare"
interval is **250 ticks** (60 ticks per second, 60,000 per day), and `a-14` §7 already notes that
per-stack state lets such things be applied "once per stack per infrequent tick rather than per
item". That rot specifically rides the rare tick is inferred, not read (medium confidence).

**7. Refrigeration is a room, not an object, in vanilla.** There is no fridge. The genre answer is
a sealed room with coolers set **below 0 °C** (hot side vented outdoors, double walls, an airlock
of doors, two coolers at 0 and −2 °C in hot biomes). A 1–9 °C "refrigerated" room only slows rot.
**The mods fill exactly the gap Odyssey's owner has chosen**: *RimFridge* (and its "Now with
Shelves" fork) and *Simple Utilities: Fridge* add **powered storage furniture** — 1×1, 2×1, 2×2
and wall variants, several stacks per cell "just like shelves", accepting any rottable category —
implemented as a component bolted on to an ordinary storage building that has power. That is
structurally the same as "a shelf with a `refrigerated` flag and a power connection", which is
what the owner asked for.

### B. The repository (read at `D:\code\odyssey-cooking`, `main` at 3a39dd8d)

**The item record** — `Assets/Odyssey/Sim/Pawns/ColonyItems.cs:10–39`: `Id`, `DefIndex`, `Cell`
(−1 when carried or contained), `Stack`, `Forbidden`, `CarriedBy`, `ContainerId`, `Despawned`. No
rot field, no Def entry, nothing anywhere (`docs/plans/storage.md:121` says the same and is still
true). The `ItemDef` (`Sim/Pawns/PawnContent.cs:622`) has `nutrition`, `stackLimit`, `category`,
`healPerUnit`; no perishability.

**Today's food** (`Defs/Core/Pawns/Items.xml`): `Item_Meal` (label "ration pack", 900 nutrition,
stack 20) and `Item_Carrots` (180, stack 75). **Both are declared rot-free by the owner**, so no
item in the shipped content would rot — which matters for the goldens (§Recommendation 8).

**Every site that creates, splits, merges or shrinks a stack.**

*The primitives — all in `ColonyItems.cs`:*

| Line | Method | What happens to a stack | What a rot field needs |
|---|---|---|---|
| 258–276 | `Spawn` | **Merge** at 260–267 into a resident of the same def (`here.Stack += stack`); **create** at 270 otherwise | Spawn takes a rot (default 0 = fresh); the merge averages |
| 311–327 | `PickUp` | whole stack into hands; no arithmetic | nothing (the record travels) |
| 336–352 | `SplitOff` | **split**: source shrinks at 344, **new record** at 345–351 | **copy** the source's rot to `taken` |
| 366–391 | `PutIn` | **merge** into a container stack at 371–377 (`resident.Stack += item.Stack`, incoming despawned) | average |
| 397–406 | `TakeOutTo` | routes to `Drop` | inherits `Drop` |
| 415–441 | `Drop` | **merge** into a cell's resident at 423–433; else place | average |
| 457–472 | `MoveTo` | routes to `Drop` | inherits `Drop` |
| 474–485 | `Despawn` | tombstone | nothing |
| 533–534 | `Fits` | the one merge-compatibility test (same def, room under `stackLimit`) — behind `CellHasSpace`, `StackWithRoomIn`, `Spawn` and `Drop` | untouched if rotten is a separate def (§C3) |
| 595–614 | `HandleGiveResource` | debug grant → `Spawn` | fresh |
| 688–716 | `ContributeTo` | hashes all eight fields of every record, tombstones included | add the rot field |
| 725–747 / 749–831 | `Save` / `Load` | writes and reads the same eight fields per record; `Load` creates records at 767 | add the field behind a format check |

So there are exactly **three merge sites** (`Spawn` 266, `PutIn` 374, `Drop` 429 — `MoveTo` and
`TakeOutTo` both land in `Drop`), **one split site** (`SplitOff` 344–351) and **two record
constructors outside load** (270, 345). `storage.md:132` cites `Drop:207` and `MoveTo:243`; the
file has grown since and those numbers are stale.

*Callers that create stacks (`Spawn`):* `Events/Skyfallers.cs:97` (supply drop — ration packs),
`Construction/ConstructionGrid.cs:918`, `Pawns/GrowingJob.cs:375` (carrots),
`Pawns/JobDrivers.cs:444` (wood), `Pawns/MineJob.cs:444`, `Pawns/ColonyScenario.cs:805` (the
starting ration packs), `:885`, `:926`, `:957`, `Pawns/DeconstructJob.cs:200`,
`Pawns/PawnRegistry.cs:184`, `Pawns/PowerJobs.cs:292`, `Pawns/Combat/WeaponRules.cs:66`. **Every
future food source — butchering, cooking — will be a new `Spawn` caller**, and the natural default
is fresh.

*Callers that merge (`Drop` / `PutIn` / `MoveTo` / `TakeOutTo`):* `Pawns/Job.cs:260`
(`DropCarried`, the failure path, which re-searches with `NearestCellWithSpace` first),
`Pawns/Job.cs:411` (`PutDown`, the haul's deliberate put-down, which does **not** re-check),
`Pawns/Job.cs:444` → `Storage/StorageUnits.cs:209` (`PutIn`), `Storage/StorageUnits.cs:291`,
`:330` (`TakeOutTo`, a shelf emptied or coming down), `Pawns/Falling.cs:123` (`MoveTo`),
`Pawns/Combat/WeaponHand.cs:82`.

*Callers that split:* `Pawns/Medical.cs:490` (`SplitOff`, one box of supplies).

*In-place shrinks (no rot change needed, but listed for completeness):*
`Pawns/JobDrivers.cs:177` (**eating** — one unit off the pile, `nutrition` read off `DefIndex` at
171 at the moment of eating), `Pawns/BuildJob.cs:379`, `Pawns/PowerJobs.cs:165`, `:451`.

**Save and hash.** `SaveFormat.CurrentFormatVersion = 9` (`Sim/Saving/SaveFormat.cs:264`). The
item section `odyssey.items` writes a fixed record per item **including every tombstone**, and
`_items` grows by one tombstone per merge — every hauled load that lands on a stack of its kind
leaves one. `ContainerId` is the precedent for a field added to this record: added in format 8
behind `reader.FormatVersion >= 8` (`ColonyItems.cs:761, 776`), hashed unconditionally at 700.
The other precedent is power's **new keyed section with no format bump**
(`odyssey.construction.parts`), because sections are length-prefixed and skipped when unknown.

**What the fridge and the temperature pass offer.** `TemperatureSystem.CellTemp(cell, tick)`
(`Sim/Temperature/TemperatureSystem.cs:255`) is the named seam (`docs/design/28-temperature.md`
§10: *"CellTemp is the query a refrigerator will ask (M5)"*); it returns the room scalar or the
outdoor curve **plus radiance**, in hundredths of a degree (the Defs' `comfortMinC = 1_600` is
16 °C). `RoomTempC` (:274) is the air alone. The temperature pass and the power solve both run
every **120 ticks** (`TemperatureSystem.IntervalTicks`, :49; `PowerGrid.IntervalTicks`,
`Sim/Power/PowerGrid.cs:40`). `PowerGrid.IsPowered(int edifice)` (:782) answers the fridge's
question. A contained item's store is `StorageUnits.EdificeOf(item.ContainerId)` (:98), and
`StorageUnits.Tick` is empty (:366).

**The earlier decisions that bind this.** `docs/plans/storage.md` decision **24** (food may be
stored in a container, *and it spoils quicker inside one*), the three open questions under it at
lines 128–134 (**a** the merge rule, **b** the ration-pack pantry, **c** the inversion against the
genre), and decision **27** at line 807: *"The spoilage inversion is kept — a sealed crate is warm
and stale — and the wiki description says so."* Decision 32: spoilage is its own unit. The
registry already holds `ui.alert.spoilage` → "Spoiling".

### C. What the two halves imply

**C1. The merge rule is a count-weighted mean, and it must round against freshness.** Integer:
`rot = ceil((a.Rot·a.Stack + b.Rot·b.Stack) / (a.Stack + b.Stack))`, computed in `long`. Rounding
up means a merge can never make food fresher than both parts; rounding down would let a
split-and-merge loop shave a tick a time. Equal inputs give the same output either way, so the
ordinary case (a stack topped up with its own kind) is exact.

**C2. Most-rotten-wins is worse for a player than the reference's mean.** One stale meal hauled on
to nineteen fresh ones would age the whole twenty — the pantry would get *older* every time a
colonist tidied it. The mean is what the genre teaches and what the data shape already supports.

**C3. "Rotten but still present" breaks the merge rule unless rotten is a different kind of
thing.** The reference never meets this, because rotten food is deleted. Odyssey keeps it, so:

- if "rotten" were a stage of the same def, a rotten stack and a fresh one of the same kind would
  pass `Fits` and **average** — a hauler could launder a rotten pile back to half-fresh by dropping
  meat on it. `Fits` would need the incoming stage, and it is called from ten places with a
  `(def, count)` signature.
- if rotting **converts the stack in place to one "rotten food" def**, `Fits` already refuses the
  merge (different def), the storage filter sees a distinct commodity, the eat scan skips it on
  `nutrition = 0`, the wiki gets a name for it, and no signature moves.

**C4. A resident that changes kind mid-haul is new, and `Drop` throws on it.** Either design has
it: a hauler reserves a cell holding fresh meat, the meat rots on the way, and `PutDown`
(`Job.cs:411`) calls `Drop`, which throws "cannot take" (`ColonyItems.cs:426–428`). Nothing today
changes a resident's def, so no driver guards against it. `DropCarried` is safe (it re-searches);
`PutDown` and `PutInto` need the same re-check — or the rot pass must skip converting a stack that
is a reserved haul destination until the reservation clears. **This is the one fault this unit is
most likely to ship.** The eat driver has the mirror case: a meal that rots between reservation
and the last bite would be "eaten" for `nutrition = 0` at `JobDrivers.cs:171`; it should fail
instead.

---

## Recommendation

**1. The field.** One `int RotTicks` on `ColonyItem`: rot progress in **ticks at the full rate**.
The def carries `ticksToRot` (0 = never rots): raw meat **120,000** (2 days), meals **240,000**
(4 days), ration packs and carrots **0**. A stack is rotten when `RotTicks >= ticksToRot`. Keep it
an integer count of rate-scaled ticks rather than per-mille ticks: a per-mille unit overflows
`int` at 35 days, and a pemmican-like clock would already exceed it.

**2. The rate, per mille, composed of three factors — and temperature is in this slice.**

```
rate‰ = tempFactor‰ × storeFactor‰ / 1000
tempFactor‰  = clamp(CellTemp / 10, 0, 1000)      // CellTemp in hundredths of a degree
storeFactor‰ = 1000 on the ground or in hands
             = def.spoilPerMille in an unpowered container   (shelf: 1500, ASSUMED)
             = 0 in a store whose def is refrigerated and IsPowered(edifice)
```

`CellTemp / 10` is exactly the reference curve in this unit (0 at ≤ 0 °C, 500 at 5 °C, 1,000 at
10 °C and above). **Take temperature now, not only the fridge flag**, for three reasons: the seam
exists and was named for this (design 28 §10); the owner's own figures are stated *"at room
temperature"*, which presupposes a temperature; and without it meat left outside in a **Rime**
winter at −20 °C rots in two days, which a player from the genre reads as a bug. Use `CellTemp`,
not `RoomTempC`, so the number the tile's pane shows is the number the food uses. The cell is the
item's own, its store's cell for a contained thing, or its carrier's cell for a carried one. **The
tie, if the owner wants fridge-only**: the thing that breaks it is whether cold rooms and cellars
should be a strategy before the fridge exists — the cheapest experiment is one headless Rime year
with a meat stack on the surface and one in a cellar, counting rotted stacks under each rule.

**3. Keep decision 24's "faster in a container", unpowered fridge included.** No strong reason was
found to reverse it: the reference's shelf protects against *deterioration*, a different mechanic;
the owner has already confirmed the inversion (decision 27) with the wiki warning attached. It
also gives a power failure teeth — a dead fridge is a sealed box and rots **faster** than the
floor, which is a clear lesson and makes the *Power failure* alert matter. Two cautions for the
interview rather than reasons to reverse: the shelf is **open-fronted** on screen, so "sealed crate
is warm and stale" is harder to read off a shelf than off a crate; and a colony that keeps its meals
on shelves by the kitchen will lose some to it. The multiplier is a Def field on the store
(`spoilPerMille`), so it is one number to tune.

**4. The update: its own `FoodRotSystem`, every 120 ticks, over a perishable lister.**
`ColonyItems` keeps a fourth ascending lister, `_perishable`, maintained at the two record
constructors (270, 345), `Despawn`, `Load`, and the def conversion — **never a sweep of `_items`**,
which carries every tombstone the colony has ever made. Every 120 ticks, after the temperature and
power passes in the same tick, it adds `120 × rate‰ / 1000` to each stack's `RotTicks` (truncation
loses under one tick a pass, always towards slower, < 1 % of the clock) and converts the ones that
cross. **It scales with perishable stacks — changing things — and states so in its doc comment**,
per `docs/process.md` §3. Estimated cost at 2,000 perishable stacks: one location resolve, one
`CellTemp` (a dictionary probe plus radiance) and at most one `IsPowered` each — a few tenths of a
millisecond once every 120 ticks. **That is an estimate, not a measurement**; the unit owes a row in
`TickBenchmarkTests` with the stack count beside it, and if the one-tick spike shows, stagger by
`id % 120` into buckets rather than cursor-walking a list that mutates. A deadline-based lazy clock
(store the tick it will rot; recompute only on a change) was considered and rejected: room
temperature moves every pass, so every deadline would be recomputed every pass anyway.

**5. Stages, all derived — no second field.** *Fresh*; *Spoiling* once the last quarter of the
clock is reached (display and the existing `ui.alert.spoilage` "Spoiling" key); *Rotten* is the
conversion. Nothing is stored but `RotTicks`.

**6. Rotten is its own def, converted in place.** When a stack crosses, its `DefIndex` becomes one
`Item_RottenFood` (all foods share it, so rotten meat and rotten meals merge — they are all refuse),
`RotTicks` resets to 0 and is reused as the **decay-away clock** under that def's own `ticksToRot`
(one day, ASSUMED), at the end of which the stack despawns — the reference's deletion, deferred long
enough to be seen and hauled. Rotten food: `nutrition 0`, refused by **every** store regardless of
filter, so the existing refusal pass (`docs/design/26-storage.md` §11) carries it out of stores and
fridges to open ground (`OpenGroundFor`) with no new job. Append the def at the end of the item
list so no existing `DefIndex` moves, add its `ui.res.*` key to `icon-keys.csv`, and rebuild the
wiki in the same commit.

**7. The merge and split rules in code.** `Spawn` gains a `rot` parameter (default 0, fresh) and
averages on its merge at 266; `PutIn` (374) and `Drop` (429) average with the ceiling mean of C1;
`SplitOff` copies. `Fits` does not change. **Guard C4 before merging**: `PutDown` and `PutInto`
re-check the destination and fall back to `DropCarried`'s search when the resident has changed
kind, and the eat driver fails on a stack that rotted mid-meal. One test each — a hauler arriving at
a stack that rotted on the way, and a meal that rots between reservation and the last bite.

**8. Save format and goldens.** Put `RotTicks` **on the record**, saved and hashed by
`ColonyItems` beside `ContainerId`, and **bump the format 9 → 10** behind
`reader.FormatVersion >= 10` (older files read 0 — fresh — which is true, since nothing rotted
before). A side section saved by `FoodRotSystem` would avoid the bump and the four bytes per
tombstone, but it gives one value two owners (merged by `ColonyItems`, persisted elsewhere) and
needs a load-order stash like `PendingLegacyZones` — the first pattern in `docs/bug-patterns.md`.
The bump is the cheaper fault. **Renumber on merge if another branch takes 10 first**, as storage
did with 7 → 8. Hash the field unconditionally, as `ContainerId` is: **every golden moves once**,
and since no shipped item rots, the re-bake must be **measured** with `GoldenColonyProbe` to show
every census number identical — the hash seeing more, not the colony doing anything different. Do
that re-bake **separately from** the one cooking itself will cause, so each has its own evidence.

---

## Sources

- https://rimworldwiki.com/wiki/Temperature — the 0/1/5/9/10 °C rot factors; cooler freezer
  design; rot versus deterioration.
- https://rimworldwiki.com/wiki/Meat — days to start rot 2; deterioration rate 6.
- https://rimworldwiki.com/wiki/Simple_meal — days to start rot 4; deterioration rate 10.
- https://rimworldwiki.com/wiki/Packaged_survival_meal — never rots, still deteriorates outside.
- https://rimworldwiki.com/wiki/Pemmican — 70 days.
- https://rimworldwiki.com/wiki/Corpse — corpses rot after 2.5 days above 0 °C; the
  "each piece tracks separately" claim taken to be wrong (Findings A5).
- https://steamcommunity.com/app/294100/discussions/0/1729828401669717089/ and
  https://steamcommunity.com/app/294100/discussions/0/144512526678074211/ — food is deleted when
  it rots; no persistent rotten food state; "allow rotten" governs corpses.
- https://github.com/roxxploxx/RimWorldModGuide/wiki/Key-Points-of-Understanding — the rare tick
  is 250 ticks.
- https://github.com/KiameV/rimworld-rimfridge ,
  https://steamcommunity.com/sharedfiles/filedetails/?id=2898411376 ,
  https://steamcommunity.com/sharedfiles/filedetails/?id=2645100914 ,
  https://rimworldbase.com/rimfridge-mod/ — powered refrigerated racks, 1×1 / 2×1 / 2×2 and wall
  variants, stacks per cell like shelves, a component on any powered storage building.
- The count-weighted merge came from a **search-engine summary** of a community decompilation
  repository. Its URL is deliberately not recorded here and it was not opened (clean room);
  `a-14` §4 independently records the averaging.
- Repository: the files and lines cited in Findings B.

## Confidence

- **High:** the rot days for meat, meals, pemmican and the survival meal; the 0 °C stop and the
  linear 0–10 °C factor; rot and deterioration being separate; food being deleted rather than kept
  rotten in the reference; every repository site and line listed.
- **Medium:** the merge being count-weighted (two sources, one of them a summary of a page not
  read); rot running on the 250-tick rare interval; the recommended cost estimate (reasoned, not
  measured).
- **Low:** the ASSUMED numbers — the shelf's ×1.5, the one-day decay of rotten food, the
  last-quarter "Spoiling" band.

## Could not be determined

- The fridge mods' mechanics beyond the store pages: whether RimFridge holds a target temperature
  or simply stops rot, its power draw, whether it heats the room, and what it does on a power cut
  (the Steam page returned 429; the GitHub and mirror pages do not state them).
- Whether the reference's rot tick is exactly the rare tick (inferred from the general rule).
- What the owner wants done with rotten food after it is hauled out — decay away (recommended),
  a dumping zone, or burning — and whether a colonist should ever be *told* food is spoiling in a
  store (an alert) or only see it on the pane. Both are interview questions.
- How long each future food keeps: only meat and meals have owner numbers; butchered, cooked and
  foraged kinds will each need a `ticksToRot` as they arrive.
- Whether an unpowered fridge should rot at the shelf's rate or at its own — recommended the
  shelf's, via the same Def field, but the owner has not said.
