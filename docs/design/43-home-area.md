# 43 — The home area, and keeping a colonist home

**Status: approved 2026-09-25 (owner: *"approved start"*). H1 (the mask), H2 (the setting and the
gate), HH (the hearth, §3f) and HP (its pane row and alerts, §3g) are built. H3 and H4 wait on the Claude Design brief** (`docs/reference/mockups/home-area-brief.md`). Branch
`claude/sleepy-cannon-9d0evw`. Units H1–H5 in `docs/plans/home-area.md`.
**Read first:** the interview `docs/research/home-area-interview.md`; the reference
`docs/research/a-18-home-and-allowed-areas.md`; design 33 §18 (the combat response, whose shape the
setting copies); design 32 §9 and §14 (the power view, whose shape the Home view copies);
design 26 §2 (why a store is a zone and home is not).

## 1. The request

Owner, 2026-09-24: *"Could you plan out the home zone, make it another element to the toolbar that
contains power. This will help you visualise your home (use a home icon). Your home area consists of
the most outer region building, stockpile, path etc - plus a perimeter of 5 … So this comes on to the
next part to be able to order colonists (new tab I think) so we can allow them everywhere or home for
now - so it allows them to keep to home for safety."*

Three things, in the order they can be built: **the home area** (a region the simulation works out
from what the colony has placed), **a switch that shows it** (on the views strip, beside Power), and
**a per-colonist setting** — *Anywhere* or *Home* — in a new tab.

## 2. The interview (owner, 2026-09-24; every recommendation taken)

| # | Question | Answer |
|---|---|---|
| 1 | How is home decided? | **Derived automatically**, recomputed when something is placed or removed. Nothing painted, nothing saved. |
| 2 | Perimeter | **5 cells, square** (Chebyshev), 12.5 m. |
| 3 | What counts | **Placed things and sites** (§3a). **Not** felling or mining marks. |
| 4 | Vertically | **Per layer, one layer of margin** above and below. |
| 5 | Where the setting lives | **A new Assign tab on F4**: Area (Anywhere / Home) and Response (Fight back / Defend / Flee). |
| 6 | What Home stops | **No work outside; a draft overrides it.** She may walk through; idle outside, she walks home. |
| 7 | The brief | Claude Design draws **the tab, the home glyph and the board look**. |
| 8 | Flee towards home | **Not in this unit.** |

**The hearth, round three (owner, 2026-09-25; every recommendation taken).** One campfire is the
centre of home: any campfire can be it, and there is one at most; the first raised takes the title;
*Make this the hearth* moves it; home is only the piece of the footprint joined to it, joined when
the grown areas touch; taking it down leaves no hearth and so no home; it is called the **Hearth**;
it wears a house mark while the Home view is on; and nothing else hangs on it yet. The table is in
the interview, §6. **It changes §3's answer to "what is home" and closes the far-site gap H2 found.**

**Assumptions written as decisions**, each for the owner to overturn: the view is called **Home**
and the setting **Area**, with values **Anywhere** and **Home**; the tab is **Assign**; the view is
**off** by default and not remembered between sessions (Power's rule); **an empty home restricts
nobody** (§4d); **eating and sleeping are gated like work, except that a starving colonist may eat
outside** (§4c).

## 3. The home area

### 3a. What counts as placed, and where each is placed

Home is grown from **every cell the colony has put something on**. The one owner of that list is
`HomeArea.SeedLayer`. Every place a source changes calls `ColonyFootprint.Touch(cell)`, and the list
below is the checklist: a placement path that does not touch is a home that silently fails to grow.

| Source | What counts | Where it changes (`Touch` goes here) |
|---|---|---|
| Edifices | `PlacedEdifice.Built && !Removed`: walls, doors, windows, pillars, ladders, beds, shelves, campfires, generators, heaters | `ConstructionGrid.Raise` (the record's `Built = true`), `ConstructionGrid.Demolish` (`Removed = true`) |
| Floors and paving | `CellGrid.Floor[c]` is `SlabBuilt` or `SlabPaved` | the floor write in `Raise`; the floor clear |
| Build sites | every site not yet raised | `ConstructionGrid.Place`; the site's removal (cancel, or raised) |
| Stockpiles and growing zones | every cell of either | `ZoneGrid.Join` / `Leave` / `Clear` — the one container both zone owners go through |
| Power lines | every line and every line order | `PowerGrid`'s line add and remove, its site add and remove |

- **Edifices and floors are read off the grid, not off the record list.** A collapse takes a floor
  without telling anybody, so the grid is what is actually standing; a cell counts when its floor is
  ours or its edifice handle names a record that is `Built` and not `Removed`. The collapse touches
  the footprint only for a floor of ours, so a cavern falling in costs the home nothing.
- **Shelves are edifices**, so they are counted once, as edifices. Counting `StorageUnits` as well
  would be a second owner of the same cell.
- **Worldgen ruins are not ours.** A city's walls are placed with `Built == false` and never count.
  A future *Claim* order would make them ours (a-04 line 40); that is the day they join.
- **Felling and mining marks never count** (answer 3). The reason to keep someone home is that the
  work outside is where the danger is; counting the marks would move the danger inside.
- **Items never count.** A pile of wood is not a placement, and a supply drop anywhere on the board
  must not drag home out to it.
- **`PowerGrid.Version` is not the signal.** It moves on net state as well (a generator running dry),
  which is not a change of footprint.

### 3b. The shape

For each layer:

1. **Seed**: the cells of §3a on that layer.
2. **Grow by five, square**: every cell within five steps orthogonally or diagonally of a seed. It is
   separable, so it is two one-dimensional passes of an eleven-cell window, along x then along z.
3. **One layer of margin**: a cell is home if it is grown-home on its own layer or on the layer
   directly above or below.

Home may be several pieces (an outpost) and may reach over water, up a terrace or into rock. It is a
square around what was built, not a flood of where a colonist can walk (interview §4, tension 4).

### 3c. When it is worked out: lazily, per dirty layer

A `Touch` marks its layer dirty. **The first question about a dirty layer rebuilds it and the two
beside it**, then answers. Nothing runs on a clock.

**A cadence was the first design and is rejected.** A rebuild every sixty ticks has a phase. Save a
world between a placement and the next rebuild, load it, and `RebuildDerived` rebuilds at once — so
the loaded world's mask, and therefore its job choices, differ from the running twin's until the
cadence catches up. The lockstep twin in the combat gate and `WorldRoundTripTests` would catch it,
and the only repair would be to save the pending dirty set, which makes derived state saved state.
**A lazy rebuild is always the pure function of the hashed world**, whenever it is asked.

`ColonyWorld.RebuildDerived` marks every layer dirty after a load.

### 3d. What it scales with

- **At rest: nothing.** A query of a clean layer is a bit test.
- **After a placement:** three layers, each an O(layer) seed-and-grow. The seed includes one scan of
  the layer's floors, because there is no sparse list of built floors; that term scales with the
  board's footprint, not with the colony, and runs only on a rebuild.
- **Mining, felling, walking and fighting never touch the footprint**, so the busy arm of the tick
  benchmark (one mined cell a tick) pays nothing for home.

Measured in H1 by `TickBenchmarkTests.WhatOneHomeRebuildCosts` on Standard, Huge and the scale target
(§7).

### 3f. The hearth: only the piece joined to it is home (round three)

Home is everything placed, grown as §3b says, **and then only the piece that contains the hearth**.

- **The hearth** is `Sim/World/Hearth`: one cell, -1 for none. Any campfire of ours can be it
  (`Built`, not `Removed`, `EdificeCampfire`); a ruin's fire cannot. Campfires stay unlimited,
  because they heat rooms and Rime needs one per sealed room.
- **The first campfire raised while there is none becomes it** (`ConstructionGrid.RaiseEdifice` asks
  `Hearth.OfferRaised`). The player moves it with `SetHearth(cell)` from a campfire's pane: refused
  unless a campfire of ours stands there, `AlreadyInThatState` for the hearth itself, applied while
  paused.
- **Taking it down leaves none.** `ConstructionGrid.Demolish` tells it, and deconstruction and a
  bandit's blows both end there. **Nothing is promoted**: another campfire standing stays a
  campfire, and the next one raised, or the one the player marks, becomes the hearth. No hearth, no
  home, and nobody is restricted (§4d).
- **Joined** means a flood from the hearth's cell over the grown cells, four ways on a layer and one
  up and one down. Two pieces join when their grown squares touch, which is buildings about eleven
  cells apart; there is no second number to tune. An outpost joins the moment the base grows out to
  meet it.
- **Saved and hashed only while set** (`odyssey.hearth`, layout 1, no format bump). Unset it hashes
  nothing, and no scenario or golden builds a campfire, so **no golden moved**.
- **Published** as `WorldSnapshot.HearthCell`, one number, always.
- **Every change of hearth marks every layer dirty**, because which piece is home depends on it
  everywhere. It is rare, and costs one whole rebuild (§7a).

**It closes the gap H2's tests found**: a build site counts as placed, so a far site used to make an
island of home and a colonist kept home walked out to it. A far site is an outpost now, and a forced
build there is refused.

### 3g. The hearth on the pane and in the alerts (HP)

- **The pane** (rebuilt to the spec, §6a). A campfire's pane is **wide**, as a store's is. On the
  hearth the header carries a line under the name: the house at 12 px in the accent and *Hearth*
  (`ui.home.hearth`) in the meta ink. On any other campfire, one `.action` button nine under the
  header, *Make this the hearth* (`ui.command.sethearth`), which submits `SetHearth` on the pane's
  cell. Both are header facts on `InspectModel` (`IsCampfire`, `IsHearth`, `OffersHearth`, `IsWide`),
  set **above** the rows' early return and carried in the shell's rebuild signature, so the header
  follows the hearth while the pane is held. The first build's readout row (*Home is centred here*,
  `ui.home.centred`) is gone.
- **No hearth** (`ui.alert.nohearth`, Warning) while somebody is kept home and there is no hearth:
  home does not exist, so she is kept nowhere. **Narrowed from the plan**, which also raised it while a
  campfire stands: the snapshot carries no list of campfires, and with nobody kept home no hearth is
  only a colony that has not marked one.
- **Hearth marked to come down** (`ui.alert.hearthdown`, Warning) while a deconstruct order stands on
  the hearth; a click goes to it. This is the "warned first" of round three's answer 3. Its dismissal
  key is made from the cell it points at, as the row's own is, so a dismissal sticks until the order
  goes.
- **The interface reads the area setting** as `odyssey.pawn.area` through `Hud/AreaAspectNames`, a
  string pinned on both sides as the combat names are.

### 3e. Not saved, not hashed

Home is a pure function of hashed state: the edifices, floors, sites, zones and lines are all saved
and hashed. So `HomeArea` implements neither `ISaveable` nor `IStateHashable`, and **no golden moves
in H1**. If one does, a `Touch` has landed in a hashed path by mistake.

## 4. Keeping a colonist home

### 4a. The setting

`Pawn.Area`, `enum PawnArea { Anywhere = 0, Home = 1 }`, on the pattern of the combat response
(design 33 §18c):

- **Saved** in a new keyed section, `odyssey.assign` (`Sim/Saving/AssignSection.cs`), layout 1,
  keyed by pawn id, written only for a colonist not at *Anywhere*. **No format bump**: a reader skips
  a section it does not know, and an older save loads with everyone at *Anywhere*. It is not put in
  `odyssey.combat` because it is not combat.
- **Hashed** as **bit 26** of the kind word in `Pawn.ContributeTo`, nought at the default. Bits 22–23
  are held for the thief line and 24–25 are the response. A colony nobody restricts hashes exactly as
  before.
- **Published** as the sparse aspect `odyssey.pawn.area`, 1 at *Home*, absent at the default. The
  57-row pin in `AspectScaleTests` holds because it is absent.
- **Set by `SetPawnArea(A = pawn, B = area)`**, applied while paused. Refused for anybody who is not a
  colonist and for a value outside the two; `AlreadyInThatState` for a no-op. Handled on the job
  system, as the response is, because it can end a job (§4f).

### 4b. The gate, and its one owner

`PawnContext` gains two names for what `Reachable` asks today:

```csharp
public bool Reachable(Pawn pawn, int cell, TraverseMode mode) =>
    CanTravel(pawn, cell, mode) && MayWork(pawn, cell);

public bool CanTravel(Pawn pawn, int cell, TraverseMode mode) =>   // the old body
    (uint)cell < (uint)Size.CellCount && Nav.Grid.CanEnter(cell, mode) && Nav.Reachable(pawn.Cell, cell, mode);

public bool MayWork(Pawn pawn, int cell) =>
    Home == null || Home.IsEmpty || !pawn.IsColonist || pawn.Drafted
    || pawn.Area == PawnArea.Anywhere || Home.Contains(cell);
```

- **`Reachable` stays the question every giver asks**, so a giver written next month is gated
  without knowing it: growing, building, mining, felling, deconstruction, power work, hauling's
  source, rescue, equip, the fireside and the wander.
- **`MayWork` is the only reader of `Pawn.Area`** apart from the walk home (§4e). A second copy of
  the draft override anywhere is pattern P1, one rule with two owners.
- **`CanTravel` is for physical questions**: the fight's own files (self-defence, retaliation, the
  flee cell, the attack driver's reach) and the walk toil in `Job.cs`, which walks a job already
  started to its end because the giver gated the start. Those call sites move to `CanTravel` in H2,
  so the fight behaves exactly as before for a Home colonist.
- **Animals and bandits** short-circuit on `!pawn.IsColonist`; no hostile or animal file changes.
- **The one exception is food at starvation** (§4c). `CriticalNeedsThinkNode`'s food search asks
  `CanTravel` instead of `Reachable` while `StarvationSeverity > 0`, and nowhere else does. It is the
  only caller that chooses between the two by the pawn's state, and its doc comment says so.

### 4c. What is gated

| | Gated at *Home* | Why |
|---|---|---|
| Every work giver's target and the cell she would stand on | yes | answer 6; the reference's rule (a-03) |
| Hauling *from* a cell | yes | answer 6 |
| Hauling *to* a store or shelf | yes: a store at an outpost is passed over for the best one inside | a store counts as placed, but since the hearth (§3f) one at an outpost is not home. The store search asks `MayWork` beside the reach, so she stores inside rather than dropping the haul because the best store in the colony was one she may not use (review, 2026-09-25) |
| Hauling to open ground (a refused thing's fallback) | yes | the fallback asks `MayWork` too |
| A right-click forced order | yes, refused with `NotPermitted` | answer 6. A forced build at an outpost is refused, and one beside the base is taken. Before the hearth (§3f) every site was home by itself, so no forced build could be refused; H2's tests found it |
| Sleeping, the fireside, the idle wander | yes | an assumption (§2): a Home colonist stays home. Beds are inside by construction |
| Eating | yes, **until she is starving** | the reference and Dwarf Fortress both gate food and both open the gate at starvation, because gating it outright is the genre's known trap (a-18 finding 9). Starving means `Pawn.StarvationSeverity > 0` (WS3), so there is no second threshold |
| Rescue | yes | it is a work giver; say if a downed colonist outside should still be fetched |
| Walking through outside cells | never | the path is not constrained |
| Self-defence, Defend, Flee, retaliation | never | they ask `CanTravel`; Flee towards home is answer 8's hook |
| Anything while drafted | never | answer 6 |

### 4d. An empty home restricts nobody

The played scenario starts with **no beds, no stockpile and no campfire** (`ScenarioDef.Playtest`,
owner 2026-09-20), so a new colony has no home until a campfire is raised (§3f). **No hearth, no
home**, whatever else is built. A colonist set to
*Home* then would have nothing she may do at all. `MayWork` answers yes while home is empty, and the
Assign tab's Area column says **"No hearth yet"** in its header while that is true (`ui.assign.nohearth`).

### 4e. Walking home

In `IdleThinkNode.TryGiveJob`, before the fireside roll: a colonist at *Home*, undrafted, standing
outside a non-empty home gets a walk to the **nearest home cell she can travel to, on any layer**
(`HomeArea.Cells`); a tie keeps the lower cell index, so the choice is deterministic. It runs only
for a colonist standing outside, which is rare, and scales with the home's cells. The fireside and the wander need no change: both ask the gated
`Reachable`, so their targets are inside home already.

**The first build searched only her own layer and the two beside it, and that was a bug** (review,
2026-09-25). Home reaches one layer past what was built, so a colonist two layers down a quarry, or
two terraces up, has no home cell within one layer of her — and everything else she might do is
gated too, so she stood still for good. `HomeWalkBackTests` digs a quarry one, two and three deep;
two and three failed before the fix, and a colonist at *Anywhere* at the bottom is the control.

### 4f. A new setting answers at once

Setting a colonist to *Home* while her job's target or stand is outside ends that job as a failure,
keeping her step in progress, the way a new response does (design 33 §18c,
`HostilityResponses.Started`). She thinks the same tick and gets something inside, or walks home.
Setting her to *Anywhere* interrupts nothing.

### 4g. A home that shrinks under her

Home is derived, so demolishing an outpost can leave a colonist outside it. She finishes nothing
outside and walks home when she next thinks. A report of "she walked off in the middle of it" after
a demolition is this rule working.

## 5. Seeing it

Built to **Claude Design's specification of 2026-09-25** (the owner's message, "Task: add Home, the
hearth and the Assign tab"; the brief it answered is `docs/reference/mockups/home-area-brief.md`).

### 5-rulings. Where the specification and the shipped interface disagreed (owner, 2026-09-25)

| The spec said | Ruling | Why |
|---|---|---|
| An "assist" menu, or Assign? | **The Assign tab on F4**, as designed | no Assist menu exists; F4 was the dead Colonists slot |
| Command bar 32 high, 14 px icons, active 12 %, dead 40 % | **Keep the shipped bar** (38, 16 px, 18 %, 42 %); only Assign goes live | the numbers came from the old mockup, and every panel sits on the bar |
| Home button 36, "matching Power exactly"; hearth button 30 high | **Match what ships**: 34 like Power; the colonist pane's `.action`, 26 | the spec's own rule is "match exactly" |
| The edge lifted 0.02 m | **Part the grass along the edge** while Home is on | the meadow's grass is 1.1 m and would hide it |
| The campfire pane 560 (it shipped at 280) | **Campfires get the 560 pane**, as stores do | the button does not fit 280 |

The Menu row's house is drawn at the Menu's own 16 px (`IconBadge.BarSize`) rather than the spec's 20,
on the same "match what ships" ruling: every other row's icon is 16.

### 5a. The switch

`OverlayDirector.HomeVisible` / `ToggleHome` / `SetHome`; `HudViews.Home = "ui.overlay.home"` after
Power in `HudViews.Keys`. Off by default and not remembered between sessions, as Power is. The views
strip's button is the same 34 px box and fills as Power's, the house drawn from the spec's path
(`HudIcons.Home`, shipped as `docs/reference/mockups/home-glyph.svg`, registered under the icon key
`home`) at 17 px, filled, the accent at 80 % while off and the text colour while on.

**The Menu's Overlays rows are generalised**: `HudViews.MenuOverlays` is the one list, a row is live
exactly when its key is in `HudViews.Keys` (`HudViews.IsLive`), the live rows are one dictionary, and
`MarkViews` lights the strip and the Menu together. `PowerOverlayKey` and the power special case are
gone.

### 5b. The edge and the hearth mark

**An edge and no wash.** `HomeEdgePass` (shaped like `PowerLinePass`) draws a flat strip
**0.25 m wide** along every side of a published border cell that faces a cell which is not home,
**0.02 m** above the draped floor plane — the accent at **70 %** on the active layer and **30 %** on the
drawn layers below it, **nothing above** the active layer.

- **Two meshes, two draw calls, whatever the size of home**: every strip on a tier is in one dynamic
  mesh (16-bit indices to 65,000 vertices, 32-bit past), rebuilt only when `HomeVersion`, the active
  layer or the lowest drawn layer moves, or after the view was hidden.
- **Inset, so no corner is covered twice**: a strip lies inside its own cell against its side, as a
  stockpile's edge does; a cell's south and north strips stop short of its west and east ones. At 70 %
  a doubled corner would be a darker dot at every turn.
- **Draped** by `GroundRelief.Drape` about the cell's floor centre, so it follows the roll of the
  ground over bare earth and a built floor alike. The ramp at the foot of a terrace step is drawn by
  the ground skin above that plane, and a strip there is hidden under it rather than laid on it —
  recorded, not fixed.
- **URP Unlit, transparent, depth-tested, no depth write**, queue 3000: what stands in front of the line
  hides it, and it hides nothing. URP/Unlit is already in `ShaderInclusion`.
- **The grass parts along the active line** (the owner's ruling): three stamps a side into the same
  clearance field items and marks use (`GrassClearance`), radius 0.6 m, only while the view is on and
  only on the active layer.

**The hearth mark** (`HearthMarkView`, shaped like `CombatFloaterView`): the house, 28 px, filled in
the accent at 70 %, 1.2 m above the hearth's ground, always facing the screen; shown only while the
view is on, there is a hearth, and it is on the active layer.

### 5c. The channel

The home is published **only while the view is on** (process §3), through `WatchHome`, handled by the
world as `WatchPower` is and applied while paused. While watched, `HomeArea` (an `ISnapshotContributor`)
publishes one `HomeCellView { CellIndex, Edges }` per border cell — edge bits 1 west, 2 east, 4 south,
8 north — **and only cells a colonist could stand in**: not solid, and a floor or solid ground under
them. That is what keeps the one layer of margin above and below a base — empty air and earth — from
drawing two more outlines a storey apart.

`HomeVersion` moves **exactly when the rows change**. The rows depend on the home *and* on the
terrain (a dug cell stops being standable without the home moving), so they are worked out again when
`HomeArea.Version` or the nav graph's version moves, and the version is bumped only if they came out
different: a mine dug outside home costs one pass over the border and moves nothing a reader caches.
**`HomeVersion` also moves whenever watching starts**, whether or not the rows did. The frame the
switch is pressed on carries no rows, and the edge pass rebuilds on showing — against that frame's
version. If the next frame's rows came with the same version the pass would never build again, and
every switch-on after the first drew nothing (review, 2026-09-25;
`WatchHomeTests.WatchingAgainMovesTheVersionSoAReaderBuildsTheRows`). The first switch-on had worked
only because the rows were new then.

The switch goes out on the frame it is pressed and is answered on the next publish; the composition
root never republishes mid-frame, which would swap the snapshot the rest of the frame is drawing.

## 6. The Assign tab (F4)

### 6a. As built

The dead *Colonists* item on F4 is **Assign** (`ui.tab.assign`); `ui.tab.colonists` stays in the
registry and lends the bar item its art (`HudCommands.IconOf`).

- **Geometry** (`AssignLayout`, the spec's numbers): **536** wide = the 510 grid + 2 × (12 padding +
  1 border), because a UI Toolkit width is a border box. Columns **192 / 150 / 150**, gaps **9**. Header
  **34** with "ASSIGN (14)" — the count in 11 mono, 6 after the word — and a drawn X in a 22 box.
  Column headings **30** (COLONIST · AREA · RESPONSE, 11 / 600 in the dim ink), "No hearth yet"
  (12 / 400, meta ink) 9 after AREA while there is no hearth. Rows **30**, **12 a page**; a 20 px
  portrait, the name at 14 / 500; two setting cells **24** high, 9 in each side, the value and an
  **11 px** cycle mark (`HudIcons.Cycle`, stroked at **2.4**). The pager shows only past twelve: two
  22 × 22 drawn chevrons with a 1 px border, the one at an end dimmed to 40 %, "1 / 2" in 12 mono.
  **Height follows the page**, width never moves.
- **The four states of a cell**: normal (the control border, text ink, dim mark); **cautious** — Home,
  or Flee — border in the warning at 50 %, ink and mark in the warning; hover — accent border, accent
  12 % fill, accent mark; and the **selected row** in the accent, its ink and mark on-accent, the warning
  dropped.
- **Model** (`AssignModel`, fast tier): colonists only, in the **roster's order**; area from
  `odyssey.pawn.area`, response from `odyssey.pawn.response`; a press sends `SetPawnArea` or
  `SetHostilityResponse` with the next value round the list, for that colonist alone.
- **Behaviour**: F4 or the bar toggles it; Escape (its own rung, `EscapeAction.CloseAssign`) and the X
  close it; opening it closes the inspect pane and the other docked tabs, and they close it. **Pressing
  a name selects that colonist and takes the camera to her, and the tab stays open** — the Work tab's
  rule, because the job of the tab is setting several people in a row. **The inspect pane waits while
  the tab is open** (`HudShell.SyncInspectShown`) and comes up for whoever was chosen when it closes:
  both dock bottom-left above the bar, and the first build drew the pane over the tab (review,
  2026-09-25; `DockedTabGeometryTests`). The Work tab could keep its rule because it is not docked in
  that corner.
- **Keys**: `HudKey.F4` between F3 and F5 (bindings are stored by name, so nothing shifts);
  `HotkeyAction.AssignTab` appended; on the Settings Keys page's Interface group.
- **Words**: `ui.tab.assign`, `ui.keys.assign`, `ui.assign.colonist` / `area` / `response` /
  `anywhere` / `home` / `nohearth`; the responses reuse `ui.command.fightback` / `defend` / `flee`.

The grid is left open to the right for a fourth column.

## 7. What it costs (to be measured)

| Arm | Where | Taken in |
|---|---|---|
| One home rebuild, all layers and one layer, Standard / Huge / scale target | `TickBenchmarkTests.WhatOneHomeRebuildCosts` | H1/HH, measured below |
| The view off against on, over a hearth and walls every nine cells across the board, one run; draw calls 0 off and at most 2 on (the gate) | `FrameTimeTests.TheHomeViewCostsWhatItSubmits` (PlayMode; milliseconds logged, not asserted) | **owed**: needs the Unity tier on the owner's machine |
| The edge pass alone: two calls whatever the size, a still frame rebuilds nothing | `HomeEdgePassTests` (EditMode) | **owed**: the same |

Each number goes here with its machine and date.

### 7a. The rebuild (H1, 2026-09-25)

A base of floors written straight into the grid — a 40 x 40 block and 200 cells scattered over the
whole surface layer — then timed: every layer rebuilt, which is what a load costs, and one placement,
which rebuilds the placement's layer and composes the three it reaches. Machine: the cloud container
this session ran in, a 4-core Intel Xeon at 2.10 GHz, .NET 8 on Linux. **Not the owner's machine and
not the target laptop**; the owner's Windows machine will read lower.

| Board | Home cells | Every layer, Debug | One placement, Debug | Every layer, Release | One placement, Release |
|---|---|---|---|---|---|
| Standard 120 x 120 x 16 | 37,551 | 2.17 ms | 0.37 ms | 1.34 ms | **0.23 ms** |
| Huge 240 x 240 x 16 | 65,646 | 9.19 ms | 1.41 ms | 4.29 ms | **0.70 ms** |
| Scale target 250 x 250 x 40 | 63,759 | 25.12 ms | 1.76 ms | 8.79 ms | **0.96 ms** |

**What it says.** A placement is O(layer), so it follows the board's width and not the colony: 0.23
ms on the board the game ships with, three times that on Huge. A stockpile drag of a hundred cells is one rebuild, and it is never paid at rest, never for felling,
mining or walking. **It is not strictly once per tick**: a rebuild happens on the first question after
a placement, so placements and questions interleaved within one tick — two builders finishing in the
same tick with a colonist kept home thinking between them — rebuild twice. Nobody asks at all unless
somebody is kept home or the Home view is on, because `MayWork` reads the setting first. The load's cost is paid once, inside the
loading screen.

**With the hearth (HH, the same machine, three runs each).** The base now carries a campfire, and
only the piece joined to it is home, so the flood from the hearth is part of every rebuild.

| Board | Home cells | Every layer, Release | One placement, Release |
|---|---|---|---|
| Standard | 37,551 | 2.20–2.51 ms | **0.67–0.71 ms** |
| Huge | 8,859 | 3.57–5.19 ms | 0.67–0.98 ms |
| Scale target | 12,744 | 8.25–9.54 ms | 0.73–0.86 ms |

**The flood costs what home holds.** On Standard the 200 scattered cells all join the block, so the
whole board is one base of 37,551 cells and a placement went from 0.23 to 0.67 ms — about 0.45 ms of
flood. On the bigger boards most scattered cells are now outposts, so home is smaller and the flood
cheaper. This arm is the worst case for the flood: a real base of a 40 x 40 block is about 7,500 home
cells over three layers, a fifth of this. The lever if a placement ever shows in a frame is an
incremental join — reflood only when a placement could split or join pieces — named, not built.

**The lever, not pulled.** The scattered cells make the seeded area the whole layer, which is the
worst case. A real base is compact, and limiting the growth and the composition to the seeds'
bounding box plus the perimeter would make a placement follow the base rather than the board. It is
recorded rather than built because the shipped board's number does not need it; Huge is where to
measure again if a placement ever shows in a frame.

## 8. Do not undo by tidying

- **The gate reads `MayWork`, and only `MayWork` reads the setting.** A giver that checks
  `pawn.Area` itself is a second owner.
- **The fight asks `CanTravel`.** Moving a combat call back to `Reachable` quietly stops a Home
  colonist defending herself at the edge of home.
- **The rebuild is lazy.** A cadence makes a loaded world differ from its twin (§3c).
- **Home is not hashed and not saved.** Hashing it would make every golden depend on a derived rule.
- **Every placement path touches the footprint.** §3a is the checklist; a new kind of placement adds a
  row and a `Touch`.
- **Home is an overlay, not a tint.** Baking it would re-mesh chunks on every placement (§5d).
- **An empty home restricts nobody.**
- **Home is only the piece joined to the hearth.** Dropping the flood would bring back the far-site
  island H2 found.
- **Nothing is promoted when the hearth is lost.** The owner's answer; a campfire picked for the
  player would be a hearth they did not choose.
- **The hearth is hashed only while set.** Hashing a -1 would move every golden.
- **Only standable border cells are published.** Publishing every home cell's edge draws three
  outlines a storey apart — the margin layers above and below are home too.
- **`HomeVersion` follows the rows, not the home.** Keyed on the home alone, a dig under the border
  leaves the edge drawn over a hole; bumped on every dig, a mine re-meshes the edge for nothing.
- **The edge is two meshes, not instances.** A mesh a tier is two calls however big home grows; an
  instanced strip is a call per 1,023.
- **The grass parts along the edge.** Take the stamps out and the meadow hides the line.

## 9. Open for the owner, and follow-ons

- **Flee towards home**: once home exists, a Flee colonist could prefer a flee cell inside it, or
  run to the hearth (answer 8's hook, design 33 §18d; round three, answer 7).
- **Bandits going for the hearth** as the raid's target, and **idlers preferring it** among fires
  (round three, answer 7).
- **Hand paint-over**: an add-and-exclude brush over the derived home, the reference's second half —
  one saved, hashed mask OR-ed and masked in `SeedLayer`'s output.
- **Eating and sleeping** (§4c): gated, with the starvation escape hatch, is the recommendation; say
  if either should be exempt outright. And **rescue** outside home.
- **An animal's area**, deferred with the tamed half (animals-tab interview Q4).
- **What else home should mean**: free repair inside it, and the cleaning, firefighting and roofing
  radius, which the reference hangs on the same mask (a-18 finding 2). **Not** forbidding things
  dropped outside it: a-18 finds that is a per-item flag in the reference, not a home rule, which
  corrects a-03 line 108.
- **One docked tab at a time** is written in every tab's change handler (six of them now, Assign
  included). Folding it into `HudDirectors` is recorded, not done.
- **The edge on a terrace ramp** is hidden under the ground skin (§5b). Laying it on the ramp needs
  the skin's height function beside `GroundRelief`'s.
- **More areas than Home** — the reference's named allowed areas. The Area column is a cycle of two
  today and becomes a picker the day there is a third.

## 10. Tests, and the controls

| Unit | Test | Its negative control |
|---|---|---|
| H1 | a lone bed makes an 11 × 11 square | the cell six away is outside (square, not diamond) |
| H1 | the layers above and below are home | two layers away is not |
| H1 | a stockpile, a growing zone, a site, a line order each count | a felling mark and a worldgen wall do not |
| H1 | demolishing shrinks it | — |
| H1 | only the touched layer and its neighbours rebuild | a query with nothing dirty rebuilds nothing |
| H1 | the mask after a load is the mask before it | — |
| H2 | the intent sets it, applies while paused, refuses a bandit, a hog and a value of 2 | — |
| H2 | saved and hashed only when not the default; a lockstep twin agrees for 300 ticks after a load | set and set back hashes byte for byte as before |
| H2 | a Home colonist leaves a marked tree outside standing | an Anywhere colonist fells it |
| H2 | she hauls from inside, not from outside | — |
| H2 | a forced build outside is refused | inside is taken |
| H2 | drafted she walks outside | released, she walks home |
| H2 | idle outside she walks home | an Anywhere colonist stays |
| H2 | she paths through outside cells between two pieces of home | — |
| H2 | a new setting ends an outside job the same tick | *Anywhere* interrupts nothing |
| H2 | Flee and self-defence are unchanged at Home | — |
| H2 | an empty home restricts nobody | a one-bed home does |
| HV | nothing is published unwatched (`WatchHomeTests`) | the same colony watched has rows; switched off, none |
| HV | only the standable layer is outlined: 40 rows for the hearth's square | three layers are home (the margin control) |
| HV | the version stays on a still colony and moves with a wall | — |
| HV | a dig under the border moves it | a dig outside home, and under the middle, do not |
| HV | the Home view is its own switch (`HomeViewHudTests`) | toggling it leaves Power alone |
| HV | every key in `HudViews.Keys` has a live Menu row | every other overlay row is dim |
| HE | any home is at most two calls (`HomeEdgePassTests`, EditMode) | a bigger home is still two |
| HE | a still frame rebuilds nothing | a new version or slice rebuilds |
| HE | nothing above the active layer; the band decides the lower ones | layer 1 drawn once the band reaches it |
| HE | no corner covered twice; the ring's area exactly | — |
| HE | off submits nothing, on at most two (`FrameTimeTests.TheHomeViewCostsWhatItSubmits`) | the view drew something |
| HA-A | the rows are the colonists in the roster's order (`AssignModelTests`) | a bandit and a hog are not rows |
| HA-A | a press sends the next value for that colonist alone, and wraps | an animal and a bandit are refused |
| HA-A | twelve a page and no pager at twelve; a shrunk colony goes back a page | — |
| HA-A | "No hearth yet" with no hearth | none with one |
| HA-A | 536 = 510 + chrome; Escape's rung; F4 live and bound | `HotkeyClashTests`, `RegistryTests`, `HudFontTests` |
| HA-A | the window is 536 and the page's height, a row 30; choosing a colonist leaves it open (`DockedTabGeometryTests`, PlayMode) | — |
| HA-P | a campfire offers, the hearth says so, a wall neither (`HearthHudTests`) | neither is a readout row any more |
| HA-P | a campfire's pane is wide | a wall's is narrow |

| HH | the first campfire raised becomes the hearth | a second does not; a wall never does |
| HH | `SetHearth` moves it, and home moves with it | refused on a wall, open ground and a ruin's fire |
| HH | taking the hearth down leaves none and no home | a standing campfire is not promoted; the next raised is |
| HH | walls with no hearth make no home | a campfire beside them does |
| HH | an outpost sixteen cells out is not home | it joins when a wall at eight reaches it |
| HH | a forced build at an outpost is refused | one beside the base is taken |
| HH | saved, loaded and hashed; a twin agrees 300 ticks after a load | — |
| review | kept home at the bottom of a quarry 1, 2 and 3 deep, she walks back (`HomeWalkBackTests`) | at *Anywhere* she stays at the bottom |
| review | watched again, the version moves and the rows arrive (`WatchHomeTests`) | a still, watched colony keeps its version |
| review | kept home, she stores inside when the best store is at an outpost (`PawnAreaTests`) | at *Anywhere* she uses the outpost; with the gate taken out of the store search she never stores it |
| review | the No-hearth count and the hearth-down cell follow the colony (`HearthHudTests`) | — |
| review | a name pressed in Assign leaves the pane hidden; closing Assign shows it (`DockedTabGeometryTests`, PlayMode) | — |

Breakages seen to fail: no flood (outposts counted), no automatic hearth, demolish keeping the hearth.

Goldens: none should move in H1–H4. A moved golden is a finding. None moved in HH.

## 11. Alternatives rejected

- **Painted only.** Never follows the colony; answer 1.
- **A zone on `ZoneGrid`.** A zone is a partition — one slot per cell — and home overlaps stores and
  fields by definition. Areas are masks (a-14, layer question 6).
- **A navigation mode per area.** Every mode costs a district flood on every rebuild; the reference's
  rule needs only the target filtered.
- **A hard fence, even drafted.** Needs a per-colonist path constraint that does not exist, and a
  drafted squad could not leave.
- **A rebuild on a cadence.** §3c.
- **The Area and Response columns in the Work tab.** The Work tab pages twenty-two columns and is
  about priorities; the catalogue reserved Assign (design 10 B4).
