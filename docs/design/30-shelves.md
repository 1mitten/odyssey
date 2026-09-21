# Shelves: a store you build

*Built 2026-09-21 as **S2** of the storage line (`docs/plans/storage.md` §7). The plan holds the
interview and the rejected alternatives; this holds the decisions the code made, the numbers that
were measured, and the things not to undo by tidying.*

## 1. What S2 is

S1 gave the player storage they could **paint**. A warehouse was a rectangle of floor, and a player
who wanted goods off the floor and out of the way had nothing to build.

A shelf is one cell of furniture that holds **eight stacks** — 600 wood, or 160 meals — for the
bed's cost and the bed's work. It is passable, it takes no quality, it turns, and **it is driven by
the storage control the player already has**: the same five priority rungs, the same two presets,
the same row per commodity, reached the same way.

**The chip was already there.** `ui.arch.tool.shelf` has sat dim in the palette's Furniture row
since the palette was written, with a registry name, an icon-map row and a glyph. This unit makes it
live. No wiki content moved except one new alert key.

## 2. The architecture, in one sentence

**A shelf holds an inventory; it never puts a second stack in a cell.**

### 2a. A slot is a stack, not a kind

Eight slots of wood is **600 wood**, and that is the whole reason a shelf is worth building rather
than painting eight tiles of floor.

**It was written the other way for a day and the review caught it.** `PutIn` merged into *the* stack
of a def and `HasSpaceFor` refused a second, which capped a shelf at one stack per commodity — 75
wood, one tile's worth — and quietly turned a warehouse unit into a spice rack. Every number in the
decision, the handover and this document said 600; the code said 75; and nothing failed, because
every test put one stack of each kind in. `EightStacksOfOneCommodityFillAShelf` is the assertion
that was missing, and `StackWithRoomIn` is the fix: a load merges into a stack of its def **with
room**, and takes a fresh slot when there is none.

The knock-on is §8b: with several stacks of one kind, a commodity can no longer address a slot.

The ground stays strictly one stack per cell, and that is not a convenience — six write paths throw
on a second stack (felling, mining, the deconstruct refund, falling, the construction refund and the
debug grant), each with its own tests. A contained thing is `Cell = -1, CarriedBy = 0,
ContainerId = n`, which is **already how a carried thing is modelled**, so "not on the floor" stayed
one question instead of becoming two.

`ColonyItem.ContainerId` was put in the item record in S1, saved and hashed, written by nothing,
precisely so that this unit would not have to change the record's layout again. It did not: shelves
are a **new save section** and the format stays at 8.

## 3. The id is the edifice index, and there is not a second one

An edifice index is already stable by contract (removed slots are tombstoned, never reused), already
what `CellGrid.Edifice[cell]` names, and already what a reservation can be keyed on. Minting an id of
our own would have been a second thing to keep in step with it.

`ContainerId` is that index **plus one**, and only because 0 already means "in no container" while
edifice 0 is a real edifice.

## 4. One resolver, and it is what makes the control reuse true

Both storage intents — `SetStoragePriority` and `SetStorageFilter` — name a **cell** and go through
`StorageZones.SettingsAt`. Teaching that one method to answer for a shelf as well as a zone is the
whole of "reuse the controls". No second intent, no second model, no second panel.

The two can never both answer:

| Direction | What stops it |
|---|---|
| a shelf raised on a zoned cell | the cell leaves the zone at `Raise`, before the unit exists |
| a zone painted over a shelf | `SiteAllows` already refused a cell with an edifice |

**No cell is ever in two stores**, which keeps "what goes here" a question with one answer.

### 4a. The wiring was one-way, and the test is what said so

`StorageZones` held a `Units` property and the composition never set it. `SettingsAt` answered null
over a shelf, so the settings panel would have opened and closed again on the same frame — the press
doing nothing, visibly. That is the fault `docs/design/20-beds.md` §8 records as *"why Assign did
nothing, three times"*, and it would have made the whole claim of this unit false at the last inch.

The test that caught it asserts the record is **the shelf's own instance**, not merely non-null.

## 5. Nine readings of "not on the floor", and the three that mattered

Every scan that looks for a thing in the world read `item.Cell < 0` and could treat it as "in
somebody's hands, so unavailable". With a third home that reading is wrong in a way that reads as
right. `PawnContext.WhereIs` is the one owner of the expression now.

| Site | What it was | What it is |
|---|---|---|
| `CriticalNeedsThinkNode.TryEat` | walks the item list raw, skips `Cell < 0` | **a colonist eats out of a shelf**, or starves beside a full pantry |
| `DeliverWorkGiver.NearestLoad` | the same, with a comment saying `Cell >= 0` is "on the floor and nothing else" | **a builder pulls material out of a shelf**, or the colony tidies its timber away and can never build with it |
| `JobDriver.LiftToil` | both guards compare `item.Cell` to the pawn's cell | `AtHand`, or a colonist sent to a shelf bends over it for ever |
| `Falling.DropFloatingItems` | skips `Cell < 0` | **correct unchanged** — a contained thing is not floating; the shelf holds it |
| `PawnRegistry.Contribute` | skips `Cell < 0` | publishes it at the store's cell — §7 |

The lift is the one nobody predicted from reading. It was found by a test that required a wall to
rise from material that existed only on a shelf.

## 6. One destination rule

`BestStorageCell` became `TryBestStorageSlot` and answers with a cell **or** a store. It is one walk:
the same bands high to low, the same "first band that yields wins", the same three guards in the same
order — space for the whole load, then distance (two array reads), then the reservation and the nav
lookup, paid only by a candidate that could still win.

**It is not forked**, and the evidence is that `StockpileTests` and `StorageZoneTests` pass
**unedited**. A second path for shelves would have left those green too, and the two rules would have
drifted from the next commit onwards. That is the lesson `HopPriceHasOneOwnerTests` records one level
down, where the cell search, the region graph and the mover each priced a hop and a disagreement
failed silently.

### 6a. A new shelf is Preferred

A zone is Normal, and two stores at one rung never re-stow between them. A shelf built inside a
warehouse at Normal would do **nothing at all** until its priority was raised by hand, and would read
— quite reasonably — as a shelf that does not work. Preferred means building one visibly does
something.

### 6b. The claim is on the destination only

`ReservationTargetKind.Container`, keyed on the edifice, one hauler per shelf. Coarse and deliberate:
`ReservationManager.Reserve` already takes a `maxPawns` and a stack count that nothing uses, so
per-slot claims are a change of arguments on the day a shelf is measured to be a bottleneck.

A store being taken **out of** is left unclaimed. The item claim already stops two haulers lifting the
same stack, and a claim on the source would stop a second colonist putting something *into* a shelf
while this one empties it.

### 6c. The job record gained no field

Both ends of a haul are named by a **cell**, and a store at either end is named by the cell it stands
in. A cell holds at most one edifice, so that is unambiguous — and `Job` is written inside the pawns
section and read sequentially, so two more ints there would have been a save-format bump bought for
nothing.

## 7. Publishing what is on a shelf

**A contained thing is published as an ordinary `ThingView` at its store's cell, carrying the store's
id.** That one decision keeps every consumer of "what does the colony hold" correct
without being told anything about shelves:

| Consumer | Change needed |
|---|---|
| the stores panel | **none** — a ledger counts stacks, not piles |
| the Build palette's material stock | **none** |
| the Almanac's find-it | **none** — it jumps to a cell, and there is one |
| the renderer | draws it on the shelf rather than the floor |
| the picker | does not offer it as a click target |

A channel of its own for contained goods would have given the first three a **second place to look**,
and the one that was forgotten would have undercounted in silence — a player refused a wall they can
pay for. That is `docs/bug-patterns.md` P1, and the register calls it the commonest fault here.

`StockOf`'s summation moved into `Odyssey.Hud` as `ColonyStock.Of`, because the fast tier compiles
that assembly and does not compile Presentation, and **a correctness bug with no test in the tier
that runs in twenty seconds is a correctness bug nobody re-runs.**

## 8. Drawing it

Seven boxes from `ShelfShape`: **four posts, two decks and a back rail**, standing against the
**back** of the cell. 1.45 m overall and open at the front, because a shelf is passable and a thing
you walk through that is taller than you reads as a fault rather than as furniture.

The same class answers the mesher, the ghost and the selection bracket, so a shelf under the pointer
and a shelf on the board cannot disagree about where it stands — which would be visible here, because
it does not stand in the middle of its cell.

### 8a0. It was a crate, and the owner said so

The first cut was a solid carcass with one deck on top and a lip: three boxes, 1.24 m. The deck
overhung the carcass by 8 cm and a comment claimed that was what made it read as a shelf rather than
as a crate. At the play camera's 48°, 8 cm reads as nothing, and the report came back the first time
anybody looked at one (owner, 2026-09-21, with a photograph of a timber garage rack): *"shelve looks
like a storage unit"*, then *"maybe there should be 2 shelves and not 1 thick container … something
like this is more suitable than just 1 thick ledge"*.

**The name was never the problem, and renaming it would have buried the one that was.** The reply
that nearly went out was a costing of `Shelf` → `Storage unit`, which is one cell of
`icon-keys.csv`. What the picture actually says is that a rack is legible because of the daylight
through it: posts rather than sides, and a gap you can see between two loaded decks. A solid box
called a storage unit is still a solid box.

Three numbers are held deliberately and are the ones a tidy would undo:

| Held | Why |
|---|---|
| 1.45 m, not the cell's 3 m | the owner's *"maybe it takes up the entire wall unit"* was the one part not taken: the shelf is **passable**, and a full-height thing you walk through reads as a fault. 1.45 m is furniture and still well under a colonist |
| the upper deck is **half the depth** of the lower | the occlusion rule below. It is not a styling choice |
| the back rail | the silhouette's only front-to-back asymmetry, so the rotate key looks like it does something |

`ShelfShapeTests.ItIsARackAndNotACrate` is the guard: posts narrower than a fifth of a metre, decks
thinner than a fifth, and 0.4 m of air between them. A solid side or a single deck passes every
other test in that file.

**Photographed rather than argued about**: `scripts/unity.sh shot Odyssey.EditorTools.ShelfCheck.Run`
writes four views of a row of four racks holding nothing, two stacks, four and eight, which is the
progression that shows whether the goods read as the fill tell. It is how the two rounds of tuning
below were judged, and it costs the owner no playtest.

### 8a. Two things it does not copy from `BedShape`, both silent

- **The half-cell origin offset.** A bed spans two cells and is measured about the point between
  them; a shelf is one cell. Copying that line would push every shelf half a cell into its neighbour
  and nothing would complain. `ShelfShapeTests.AShelfStaysInsideItsOwnCell` is what fails.
- **The missing in-plane offset in `WorldBounds`.** A bed is symmetric about its origin and gets away
  with none; a shelf stands against the back of its cell, so a bracket centred on the cell would sit
  half over the empty floor in front of it — **on three of the four facings**, which is exactly the
  kind of fault nobody reproduces.

### 8b. The goods are the fill tell, and they cost no draw calls

`ItemHeap`'s own ramp and its own spiral, on the deck instead of the floor, with the spread tightened
to a slot's width (the recipe's spreads are sized for a 2.5 m cell and a slot is a fifth of that).

**Which slot a stack stands on is published**, as its place in the store's ordered contents — so a
store **re-packs** when something leaves it and the goods behind shift along. That is real motion on
screen and the honest price of never overlapping two heaps.

*Deriving it from the def was tried and is wrong*, and the reason is §2a: a store holds several
stacks of one kind, which is the ordinary case for anything bulky, so eight stacks of wood would all
have drawn in the same place. The contents index is the only number available here that never
collides.

`RenderThings` already groups by def and submits one instanced call per kind, so a shelf's contents
land in a bucket that was going to be submitted anyway: **forty shelves add matrices, not
submissions.** The alternative — a pass over the shelves — is `docs/bug-patterns.md` **P10**, which
cost the growing zone 2,065 draw calls and 3.67 ms of a 5 ms budget before it was deleted.

#### A bay is one or two bundles, not a heap

Tightening only the *spread* was half the job, and the half that was missing did not show until a
full shelf was photographed. `ItemHeap`'s counts and sizes are tuned for a 2.5 m cell floor, so
eight full slots of wood drew twenty-four bundles at 0.55 scale inside one cell's footprint and
**the rack disappeared under its own goods** — a heap of timber with a plank in it. `SlotLumps`
caps a bay at two and `GoodsScale` came down to 0.45, and the rack is legible loaded.

Losing the within-a-stack ramp costs nothing here: on the ground a heap's size is the only thing
that can say how much is in the cell, but a shelf meters itself in **bays taken**, which is what
the pane's "3 of 8 stacks" says too.

**Measured, 2026-09-21** (`FrameTimeTests.TheWarehouseCostsWhatItHolds`), with a control inside
one run: the same board bare, then with 320 full stacks of wood lying on the floor, then with the
same 320 stacks on forty shelves, seconds apart. Re-measured after the rack, on a machine with an
editor open beside it — so read the *deltas*, not the absolutes.

| Board | Frame | Over bare | Instances | Draw calls |
|---|---|---|---|---|
| bare meadow | 2.34 ms | — | 43,935 | — |
| 320 stacks on the floor | 2.74 ms | +0.40 | 45,175 | 1,128 |
| the same 320 stacks on 40 shelves | 2.72 ms | +0.38 | 44,855 | 1,128 |

**The shelf path costs what the floor path costs** — the 0.02 ms between them is inside the run's
own noise, and the sign has flipped between runs — and the whole warehouse is 0.4 ms.

The instance counts decompose exactly, which is the check that the numbers are describing what is
believed rather than agreeing by luck:

- floor: `1,240 = 40 × 7` frame parts `+ 320 × 3` lumps on the ground
- shelved: `920 = 40 × 7` frame parts `+ 320 × 2` lumps in a bay

**Draw calls are 1,128 either way**, which is the "matrices, not submissions" claim with a number
on it: the rack went from three boxes to seven and a warehouse submits exactly what it did before,
because every part is the same module in the same per-def bucket.

**It only runs where the packs are, and it says so rather than failing.** Without `Assets/Synty`
every stack takes `ChunkRenderer`'s stand-in marker path, which costs a draw call and no instance,
and the contained branch this measures is never reached — so the control *each stack is at least one
instance* was measuring a warehouse that had not been drawn, and turned the self-hosted runner red
at 34,827 against 35,027. The case now asks `ChunkRenderer.ItemArtResolved`, the item-side pair of
`PawnFigureDirector.Enabled` and `PortraitStudio.Available`, and ignores itself where the answer is
no. `docs/bug-patterns.md`, 2026-09-21.

The review that took the measurement also found the one allocation on the path: `SlotCentre` built
its two slot tables as locals, which was two heap allocations per stack per frame — 640 a frame for
this warehouse — for tables that never change. They are static now. Still unmeasured: a warehouse at
a play resolution on the target laptop, which is the renderer's standing open question rather than
this unit's.

### 8c. No `GroundRelief.Lift` on the goods

The loose-pile path lifts every rock, because the ground is a shallow field rather than a plane.
`ShelfShape.Root` is already **draped**, so lifting again would float the goods a few centimetres off
their own deck — and only on sloping ground, which is the kind of fault nobody reproduces.

## 9. Picking, and the seam that predicted this unit

`WorldRenderModel.StandHeight`'s own doc comment said, before a shelf existed: *"a bed is the only
thing that answers today… the next non-occluding thing that stands up adds a line here — and if it
does not, it will be a quarter cell out and nobody will know why."* A shelf is that thing.

It answers the **deck**, not the top: what a player aims at is the goods, and the lip is 0.26 m above
them, which at the play camera's 48° is about a tenth of a cell of drift in the same direction the
bed's own bug went.

**And `MarkHeight` now differs from it.** A shelf is the first thing in the game for which "where is
it picked" and "where does its order mark go" are not the same answer: a deconstruct mark at deck
height is buried under a full shelf. Two questions, two lines.

`ShelfShapeTests` aims at the **ends** of the thing in tenths of a cell, not its middle — the drift
is a quarter cell and a centre has half a cell of slack either side, so a centre-only check passes
before the fix and says nothing.

## 10. Coming apart

**A store with anything in it is never offered to a deconstructor**, only to haulers. The colony
empties it first and a deconstructor arrives to a store that is already empty.

**How it empties is not a rule of its own**, and that is the merge with `main` paying for itself.
`main` landed "a store empties itself of what it refuses" the same week: a thing a store will not
have is scanned in the **first** haul pass beside the loose things rather than in the tidying, and
goes to open ground when no store will take it. A store being *taken apart* is the same sentence —
it will not have its contents and waiting will not change that — so `Refused` answers true for
everything inside one, and the urgent pass and the clearance fallback both arrive for free.

Without that gate the refusal is a **loop rather than a rule**: the work is banked on the cell, so a
colonist would walk over, swing until the work was done, be refused, and be handed the same site
again on every think — until the think tree's circuit breaker parked her, at which point the fault
reads as *idleness*, which is a long way from "that shelf cannot be emptied".

The completion guard stays behind the gate for the case where something was put in during the last
swing: contents that cannot be placed mean the work does not complete, the shelf stands, the
designation stands, and **nothing is lost**.

### 10a. The deadlock the tightened test found

An emptying shelf's contents wanted to leave — but with no other store on the board the destination
scan found nothing and they stayed, **while the gate refused to take the shelf apart until they had
gone.** Nothing moved and nothing said why.

An emptying store gives its contents up to the **floor** when no store will take them. Hauled out
rather than spilled, deliberately: the same load ends on the same sort of cell either way, but a
colonist carries it, and that is the difference between a colony emptying a shelf and a shelf
emptying itself.

*Written first as a fallback of its own, and deleted on the merge* — `main`'s clearance rule already
said exactly this for a store that refuses a thing, so the fix is one word in `Refused` rather than
a block in the haul giver. The deadlock was real either way; only the size of the answer changed.

**The test that found it had been passing for the wrong reason.** It ran blind to the end and
asserted the shelf was empty — which it was, because the colonist had finished the deconstruct and
`Dissolve` had spilled the lot. It watches tick by tick now and requires the shelf to be empty
**while still standing**, which is the only form of the assertion a broken haul path fails.

### 10b. Collapse may still destroy

`Dissolve` spills what the board will take and loses the rest, exactly as a loose stack over void does
today. That it destroys is right *there* and refused one level up: losing things to a building coming
down is a consequence, losing them to your own tidying is a bug.

**Nothing in the game makes an edifice fall yet.** `SupportSolver.Collapse` erases floors only, so a
bed over a collapsed floor already hangs in the air and so will a shelf. `Dissolve` is written to the
decided rule and hung off `Demolish`, which is the only thing that removes an edifice, so the rule
exists and is tested the day an edifice-falls rule arrives.

## 11. The alert

A shelf whose contents have nowhere to go does not loop — it stands there with its order on it and
nothing happening, which is a silent stall. `ui.alert.storagestuck` breaks the silence.

`StorageUnitView` is its own channel rather than a bit on `StoreView`, which is one row per *cell* of
a painted zone and exists to tint ground. A shelf's ground is not tinted and its cells are one each.

**The simulation publishes the state; the alert bar decides when it is news** — ten seconds rather
than the idle alert's three, because a shelf ordered taken apart is full until a hauler has walked to
it, and saying it is stuck while somebody is on their way would be crying wolf. A wall-clock rule has
no business inside a fixed-tick tick.

## 12. The golden re-bake, measured

All six numbers moved, because a hashed component contributes its count on every board in the game
including the three goldens, none of which has a shelf on it.

**`GoldenColonyProbe` is committed rather than thrown away.** It prints what each colony is made of —
live things, per-def stacks, the sum of item cells, the two lister counts, the sum of pawn cells,
total food and rest, standing orders and zones — at generation and after the full run, and is written
against nothing newer than `main` on purpose so the same file runs on both branches. Run on each, the
two outputs **diff clean**: all three colonies identical in every number. The hash sees one more zero
and the colonies do not know it.

Earlier re-bakes used a throwaway probe and had to describe it afterwards. This one leaves the
instrument behind.

## 13. What S2 does not do

- **No storage groups.** Two stores sharing one filter is a repoint of one integer — the settings are
  already a shared handle — but it needs multi-select and a name on each store, neither of which
  exists. `docs/design/26-storage.md` §9a SZ4.
- **No second tier of store.** A bigger one is one Def row: `storageSlots` is a field for exactly
  that reason, and a locker is that row plus the bed's `Owner`, which `PlacedEdifice` already carries.
- **No spoilage**, so the "food goes stale in a sealed box" half of decision 24 is still a multiplier
  on a clock that does not exist.
- **No frame cost for drawn contents beyond the floor path's** — measured, §8b.

## 14. Things not to undo by tidying

- **The upper deck's depth.** It is half the lower deck's so that looking down at 48° does not
  hide the front row under it; `TheFrontRowIsNotHiddenUnderTheUpperDeck` asserts the margin off
  the drawn boxes. Squaring the two decks up "for symmetry" turns four of eight stacks dark and
  nothing else fails.
- **`SlotLumps`.** Capping a bay at two bundles is what keeps the frame visible under a full load.


- **The ground stays one stack per cell.** The inventory exists so that rule is never touched; a
  "simplification" that makes cells multi-slot re-opens six call sites that currently throw.
- **One destination rule.** `TryBestStorageSlot` has one owner. Do not add a container-specific path
  — and note that the stockpile tests passing unedited is the evidence, so do not edit them to make a
  fork fit.
- **`PickUp` is the one door for all three homes.** A second "take out of a container" entry beside
  it is how the lift toil grows two durations.
- **`Unlist` reads the container off the record**, so callers clear `ContainerId` *after* it, never
  before.
- **`PutIn` despawns the incoming thing on a merge**, exactly as `Drop` does. A caller holding the
  reference it passed in is holding a tombstone.
- **Emptying is derived from the deconstruct order**, not stored. A flag beside it is a second copy
  that can disagree with the order the player can see.
- **`StandHeight` and `MarkHeight` are two questions.** §9.
- **Nothing here enters a cell, a save or the hash on the presentation side** — the drawn goods
  included.
