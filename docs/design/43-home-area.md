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

- **The pane.** A campfire's pane carries a **hearth** row (`InspectModel.HearthRow`): on the hearth
  it reads *Home is centred here* in the accent (`ui.home.centred`); on any other campfire it is a
  press, *Make this the hearth* (`ui.command.sethearth`), which the shell turns into `SetHearth` on
  the pane's cell, exactly as it turns the switch row into `SetPowerSwitch`. The press flag is set
  **above** the rows' early return and the row's state is in the rebuild guard, so it neither dies on
  the second refresh nor goes on offering after the fire became the hearth; both were seen to fail.
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
| Hauling *to* a store or shelf | never matters | a store is home by construction (§3a) |
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
outside a non-empty home gets a walk to the **nearest home cell she can travel to**, searched on her
own layer first and then the layers beside it. The home cells of a layer are held sorted, so the
choice is deterministic. It runs only for a colonist standing outside, which is rare, and scales with
the home cells on one layer. The fireside and the wander need no change: both ask the gated
`Reachable`, so their targets are inside home already.

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

### 5a. The view

`OverlayDirector.HomeVisible`, `ToggleHome`, `SetHome`, and `HudViews.Home = "ui.overlay.home"` after
Power in `HudViews.Keys`, with its `IsOn` and `Toggle` cases. The strip's height already follows
`Keys`. The button's glyph is a house, drawn from the SVG path the brief returns (`SvgPath` /
`PathGlyph`), because no shipped font has one and the project draws its own icons (P13).

### 5b. The Menu row, generalised

`HudShell.BuildMenuPopup` lights exactly one overlay row today, through an
`if (key == PowerOverlayKey)` special case, and `ViewButton` re-lights only the power row after a
press. That becomes: **a row is live if its key is in `HudViews.Keys`**, the live rows are held in one
dictionary, and `MarkViews` lights the strip and the Menu from `HudViews.IsOn`. `PowerOverlayKey` is
deleted. Without this, the second view is the second owner of "which overlays are live".

### 5c. The channel

The home is published **only while the view is on** (process §3: a channel only to a subscriber),
through a `WatchHome` intent handled like `WatchPower` (`SimWorld`, `WorldViewStore.WatchHome`).
While watched, a contributor publishes one `HomeCellView { CellIndex, Edges }` per home cell — the
edge bits say which of its four sides border a cell that is not home — and a `HomeVersion` on the
snapshot, cached against `HomeArea.Version` so an unchanged home republishes the rows it already has.
Presentation filters to the drawn layers and never recomputes the growth (process §3: presentation
never derives a number the simulation knows).

### 5d. The draw

**Not baked into the chunk meshes.** A store's wash is a tint bit baked at mesh time, because a store
changes only when painted. Home's edge moves five cells every time a wall goes up, and baking it would
dirty every chunk the edge crosses on every placement. So it is an overlay, submitted only while the
view is on, inside `FrameSection.Overlays`:

- **An edge look** is a pass on `PowerLinePass`'s model: matrices rebuilt only when `HomeVersion` or
  the drawn band moves, one instanced bucket.
- **A wash look** rides the cell-plate path (`ChunkRenderer.DrawCellMark`, gathered before
  `FlushCellPlates`), one colour, one instanced call per 1,023 plates.

**The hearth wears a house mark while the Home view is on**, and looks like any campfire otherwise
(round three, answer 8); the mark's shape and colour come from the brief with the rest.

Which of the two, the colours and the thickness come from the brief and live in one Presentation
constant set, `HomeLook` — never in `OrderColours`, which is the orders' table.

## 6. The Assign tab (F4)

The dead *Colonists* item on F4 becomes **Assign** (`ui.tab.assign`). `ui.tab.colonists` stays in the
registry, as `ui.tab.schedule` and Wildlife did.

- **Shape**: the Work tab's. A frozen name column, one row per colonist in the roster's order, then
  **Area** (Anywhere / Home) and **Response** (Fight back / Defend / Flee). A press on a cell moves it
  to the next value, as the pane's Response button does. Twelve rows a page, a pager, never a
  scrollbar. Width, column widths and row height are constants in `AssignLayout`, **taken from the
  brief's returned HTML comment**; the window width adds its own padding and border
  (`WorkGridLayout.PanelOuterWidth`'s rule).
- **Model**: `Hud/AssignModel.cs`, Unity-free, reads `odyssey.pawn.area` and `odyssey.pawn.response`
  and writes `SetPawnArea` and `SetHostilityResponse` the way `WorkGridModel` writes a priority.
  **Colonists only**: a bandit or an animal in the snapshot is never a row.
- **Director**: `Hud/AssignDirector.cs` on `AnimalsDirector`'s shape, in `HudDirectors`.
- **Keys**: `HudKey.F4` between F3 and F5 (bindings are stored by name, so the enum's order is free);
  `HotkeyAction.AssignTab` **appended**; a `Defaults` row; `HotkeyUnity` both ways; the Settings keys
  page's Interface group.
- **One docked tab at a time** has one owner per tab today — each tab's change handler closes the
  others. Assign adds itself to the four handlers; folding the rule into `HudDirectors` is recorded,
  not done here.
- **Words**, all registry rows and all ASCII: `ui.tab.assign` *Assign*, `ui.keys.assign`,
  `ui.assign.area` *Area*, `ui.assign.response` *Response*, `ui.assign.anywhere` *Anywhere*,
  `ui.assign.home` *Home*, `ui.assign.nohearth` *No hearth yet*. The response values reuse
  `ui.command.fightback`, `ui.command.defend`, `ui.command.flee`.

## 7. What it costs (to be measured)

| Arm | Where | Taken in |
|---|---|---|
| One home rebuild, all layers and one layer, Standard / Huge / scale target | `TickBenchmarkTests.WhatOneHomeRebuildCosts` | H1, measured below |
| The busy arm unchanged with a home present | `TickBenchmarkTests`' existing busy arm | H1 |
| The view off, on, and on over a forty-building base, one run | `FrameTimeTests.TheHomeViewAgainstTheFrame`, `Category("Measurement")` | H3 |
| Draw calls with the view on | the same, structural half kept in the PR gate | H3 |

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
ms on the board the game ships with, three times that on Huge. It is paid once per tick in which
anything was placed however many cells were — a stockpile drag of a hundred cells is one rebuild —
and never at rest, never for felling, mining or walking. The load's cost is paid once, inside the
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
| H3 | nothing is published with the view off | rows and a version appear when watched |
| H3 | the version moves with a bed | and not with a felled tree |
| H3 | the Home view is its own switch | toggling it leaves Power alone |
| H3 | every key in `HudViews.Keys` has a live Menu row | every other overlay row is dim |
| H4 | the rows are the colonists | a bandit and a hog are not rows |
| H4 | a press emits the next value's intent for that colonist | — |
| H4 | a pager at thirteen colonists | none at twelve |
| H4 | F4 is bound and Colonists is off the bar | `HotkeyClashTests`, `RegistryTests`, `HudFontTests` |

| HH | the first campfire raised becomes the hearth | a second does not; a wall never does |
| HH | `SetHearth` moves it, and home moves with it | refused on a wall, open ground and a ruin's fire |
| HH | taking the hearth down leaves none and no home | a standing campfire is not promoted; the next raised is |
| HH | walls with no hearth make no home | a campfire beside them does |
| HH | an outpost sixteen cells out is not home | it joins when a wall at eight reaches it |
| HH | a forced build at an outpost is refused | one beside the base is taken |
| HH | saved, loaded and hashed; a twin agrees 300 ticks after a load | — |

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
