# Stairs (U44)

Written 2026-09-26, before the code, as the prerequisite for deep mining (design 62, plan
`docs/plans/deep-mining.md`). **There was no interview for stairs.** Every default below is chosen
to agree with the ladder (design 21) and the bed (design 20), and each one is listed again in §10 as
a question for the owner. Read design 21 first: most of this document is that one's rules asked
again of a thing two cells long.

## 1. Why now

A ladder's connector carries a mode mask without the hauler in it (`Connector.cs`, the
`ConnectorKind.Ladder` case): *"a hauler's bulky load and an animal's lack of hands both rule a
ladder out."* So a colony can build an upper storey and walk up to it, and can never carry a plank,
a meal or a block of ore up or down. Deep mining is worthless without the second half. The stair is
the answer the vertical slice named for it (`U44`), and the navigation half has existed since M1:
`ConnectorKind.Stair` allows every traverse mode (`TraverseModes.AllMask`) at `MoveCost.StairUp` 290
and `StairDown` 230.

What was missing is the building: `Buildings.xml` said *"Stairs are deliberately NOT here yet"*.

## 2. What a stair is on our grid

A cell is 2.5 × 2.5 × 3.0 m (ADR 0002). **A stair is two cells in a line on one layer and climbs
exactly one layer**: 3 m of rise over 5 m of run, 1.5 m per cell, which is a 31° flight — steep for
a house, ordinary for a service stair, and the geometry the ruined city's stairs and their module
(`ModuleShape.StairFlight`) were drawn for.

| Part | Cell | What it is |
|---|---|---|
| **Foot** | the cell the order names (the head) | the lower half of the flight, 0 → 1.5 m |
| **Upper half** | the next cell along the facing | 1.5 → 3 m |
| **Top** | the cell directly above the upper half, one layer up | where the climb arrives |
| **Landing** | any orthogonal neighbour of the top with a floor | where you step off |

**One record, two cells, rotatable — the bed's pattern exactly** (design 20 §4). `footprint 2`,
`rotates true`; the second cell is derived by `EdificeFootprint.SecondCell` from the facing and never
stored; both cells' `CellGrid.Edifice` point at the one record. The facing is the **direction of
climb**: foot to upper half. R turns the ghost.

**A new edifice id, not the city's two.** The ruined city stamps a stair as two records — `5`
(lower) and `6` (upper) — found again by adjacency. Reusing `5` with a footprint of two would change
what every stamped city stair means to `EdificeFootprint`, the demolish path and the item blocks.
The built stair is **edifice 24** (`CoreContent.EdificeStair`, `EdificeHandle.Stair`) and
**`BuildingHandle` 14**, the next free of each: sandbags took 23 and 13, and the barricade that held
24 and 14 for an afternoon was taken out again (design 53 §13). Both halves carry the key the
palette already had, `ui.arch.tool.stair`.

## 3. How it joins two layers — the connector

**The same connector kind, price, mode mask and registration as the city's, declared foot → top.**

- One `ConnectorKind.Stair` connector per stair, registered through `NavGraph.AddConnector` exactly
  as the ladder's is. No new kind, no new cost, no new mask.
- `LowerCells = { foot }`, `UpperCells = { top }`. The portal edge runs from the foot cell on layer
  *y* to the top cell on layer *y + 1*: **one cell across and one layer up**.

**Why not the city's "directly above" pairing.** `SurfacePasses.RecordConnectors` declares a city
stair's upper end as the cells directly above its own two, so every portal edge is strictly vertical.
The figure director poses any strictly vertical step as a **ladder climb** (`PawnFigureDirector`,
the `straightUp` test), so a colonist on a city stair would be drawn hauling herself up a ladder
that is not there. And the presentation was already written for the other shape: its own comment
says *"a stairwell moves across as well as up"*, and `PawnPose.IsDrawnAsAHop` and `HopArcTests`
were written ahead of this unit to tell a stair step (one across, one up, **no solid block under the
top**) from a hop. The foot → top declaration is the step those three expect. The city's stairs are
untouched and keep their pairing; nobody plays the city (owner: *"forget the city, it is
redundant"*).

**It is not a hop.** `NavGraph.IsHop` is geometry, and a stair step has a hop's shape; the rule
`UpperEndIsABlockTop` separates them — the cell under the top is the stair's own upper half, never
solid terrain — so the search and the mover both fall through to the portal edge and charge the
connector's price.

**Connector lookup is by kind.** `NavGraph.OneCellConnectorAt` found "the one-cell connector whose
lower cell is this one" whatever it was — which a built stair's one-cell ends would have matched,
and `RefreshLadder` would have torn the stair's connector out the first time it refreshed the foot.
It now takes the kind it is asked about (ladder by default), and the stair asks for its own.

## 4. The stairwell — the ladder's shaft rule, two cells long

**Both cells above the flight are open**, mirroring design 21 §4 and §8:

1. **A stair is refused under a floor.** Neither the cell above the foot nor the cell above the
   upper half may hold a slab, a covering or solid rock — **built or ordered**. Headroom over the
   foot is marginal (1.5 m of tread plus a person reaches 3.3 m at the top of the lower half) and
   over the upper half there is none at all; one rule for both is simpler to say and to see.
2. **And the same rule from the other side:** a slab or covering is refused directly over either
   half of a stair, built or ordered.
3. **Both are asked again in `Raise`**, the shaft rule's own place, because the two orders are given
   separately and the other can arrive later (design 21 §8).
4. **The connector's own second line of defence:** a stair whose top has a floor, a blocking
   edifice, solid rock or water, whose stairwell over the foot is floored, or whose top has no
   landing beside it, registers no connector. It stands, and opens nothing, like a ladder to nowhere.

**The landing is not asked for at the order** — design 21 §4's argument, unchanged: the player may
build the stair first or the landing first, and `RefreshStair` answers again whenever either end
changes. The fan-out (`RefreshStairsAround`) refreshes any stair with a half in the edited cell, one
layer above or below it, or beside it on either layer — which covers the foot's floor, the stairwell,
the top and every landing cell.

**What a player does:** leave a two-cell hole in the upper floor over the flight, or deconstruct two
slabs. The red ghost is the refusal, as it is for the ladder.

## 5. Placement

On top of everything `Allows` asks of any edifice (open air, not water, not rubble, nothing standing
there), for **both** cells:

- **something underfoot** (`SomethingUnderfoot`: a floor, or a blocking edifice below) — a stair
  may stand on a slab, so a stairwell can go on up from a landing;
- **a clear cell** (`needsClearCell`, the bed's rule): a stack of logs standing through the treads
  is the meals-through-the-mattress fault again;
- the stairwell open (§4).

The cursor asks the same question through one public method, `ConstructionGrid.AllowsFootprint`, so
the ghost cannot turn red for a different reason from the click (design 19 §6; P1).

**A stair does not stand on a stair.** The top of one flight is an open cell, so the next flight has
nothing underfoot there; a stairwell of several storeys goes up flight, landing, flight. That is
what real stairs do, and it needs no rule of its own.

## 6. Cost and work

| | Stuff | Work | Hit points |
|---|---|---|---|
| Ladder (1 cell) | 4 | 90 | 80 |
| Bed (2 cells) | 5 | 180 | 120 |
| **Stair (2 cells)** | **8** | **180** | **160** |

Wood or stone, like the ladder. Eight is the ladder's four a cell; 180 is the ladder's work a cell,
which is also the bed's two-cell joinery; 160 is the ladder's hit points a cell. **No quality**: a
stair is structure, not furniture (a-04 §3, design 20 §2). All INVENTED; the owner's to tune.

## 7. Using it

- **Everyone may use it, carrying or not**: colonists, haulers, bandits, animals (the mask is
  `AllMask`). A hauler carrying anything walks it at the same price.
- **The price has one owner, the connector.** `Connector`'s constructor maps `ConnectorKind.Stair` to
  `MoveCost.StairUp`/`StairDown`; the region graph copies it onto the link, the cell search reads it
  off the portal edge, and the mover (`MovementSystem.StepCost`) reads the same edge.
  `HopPriceHasOneOwnerTests` now fails on any other simulation file naming `StairUp` or `StairDown`
  in code, with one sanctioned exception — `NavGraph`'s layer-change *estimate* for the search
  heuristic, which is a lower bound and never a charge. A test also measures the built stair's link
  and the mover's charge against the connector.
- **Walking pace**: 290 up (2.9 walked cells, and it covers one across and one up) and 230 down,
  against the ladder's 540 and 400 and a hop's 240. Then an ordinary step off onto the landing. A
  one-layer terrace is still hopped rather than stepped up by stair when a hop is legal, because a
  hop is cheaper; a stair's use is a built storey or a pit.
- **Who may stand on it: anybody, anywhere on it.** It is `blocking false` like the bed and the
  ladder. The foot and upper half are walkable cells at the lower layer; the top is walkable through
  `NavGrid.RefreshFrom`'s *"a connector is its own floor"*, exactly as a ladder's open shaft cell is.
  Walking through the upper half at the lower layer is walking under the flight; it is not priced
  (§10).
- **Nobody is built into it** (design 30): a stair is not blocking, so `MakeRoom` has nothing to do
  and the build-site detour is not set, as for the bed.

## 8. Deconstruction

Ours (`Built = true`), so the ordinary Deconstruct order takes it: either cell names it, both cells
go from the record, half the material comes back, and `MarkNavAround` on both cells runs the stair
fan-out, which finds no stair and removes the connector. A colonist standing on the open top at that
instant is left in a cell with no floor — exactly the ladder's case when its ladder comes down, and
no better or worse answered; it is not tested here (§10).

**Save and load:** the edifice is saved (it is a `PlacedEdifice`); the connector is derived and
rebuilt on load (`RebuildStairConnectors`, beside `RebuildLadderConnectors`), which is design 21's
argument and needs no save-format change.

## 9. Drawing

Presentation only; nothing here is in a cell, a save or the hash beyond the edifice record.

- **The built stair is the city's flight, half a cell at a time**: each cell draws the stair module
  (`group.Stair`, `ModuleShape.StairFlight`) turned to the climb, the upper half lifted 1.5 m — the
  city's own `EmitStair` arithmetic, read from the record's facing and head flag rather than from a
  neighbour search.
- **Without the licensed packs** the module resolves to the library's built-in `StairFlight`
  primitive, so a clone with no `Assets/Synty` draws a stair and the simulation never asks.
- **The ghost** draws the same two halves from the same helper (`StairShape`), so the cursor and the
  board cannot disagree about which way it climbs.
- **The climbing figure** walks the step as a walk rising one layer — no climb pose (it is not
  straight up) and no hop arc (the cell under the top is not solid). How well that reads on the
  flight's surface is unjudged (§10).

## 10. What the owner decides

No interview was held; each of these is a default chosen to agree with the ladder or the bed.

1. **Size.** Two cells, one layer (a 31° flight). Would you rather a three-cell, gentler flight, or
   a one-cell steep one?
2. **Stairwell.** Both cells above the flight must be open (no floor), like the ladder's shaft. The
   cell above the foot has marginal headroom; should a floor be allowed over the foot half?
3. **Cost.** 8 stuff, 180 work, 160 hit points, wood or stone, no quality. Right order of magnitude?
4. **Price.** Up 290, down 230 (the city's numbers, unchanged). A hop is 240, so a one-layer terrace
   is hopped past a stair beside it. Should a stair beat a hop?
5. **Carrying.** Haulers use it at the same price. Should a load slow the climb?
6. **Walking under the flight.** The upper half's cell is walkable at the lower layer at no extra
   cost, so colonists cut under the high end. Price it, forbid it, or leave it?
7. **Stacking.** Flights do not stack directly; each storey needs a landing between flights. Is a
   switchback with landings what you want, or a continuous stairwell?
8. **Standing.** A colonist may stop, idle or sleep on any cell of a stair, including the open top.
   Forbid resting on it?
9. **Animals.** Every animal may use a stair (the city's mask). Should the midden hog, who never
   takes a ladder, take stairs?
10. **Taking it down under somebody.** A colonist on the open top when the stair is deconstructed
    is in a cell with nothing under her, as on a ladder's shaft. Should the deconstructor wait?
11. **The look.** Whether the figure walking up reads as climbing the treads, and whether the pack's
    flight reads at the play camera. A Play session's question, not a test's.

## 11. As built (2026-09-26)

- **Content.** `Building_Stair` (handle 14) → edifice 24, in `Buildings.xml` and the code oracle
  together; `BuildingFingerprint` moved for the appended row alone. The Hud's parallel tables
  (`BuildShapes`, `BuildLabels`, `EdificeLabels`) gained the row and the dim Structure chip went
  live. The icon key's description now says a load can be carried up.
- **Simulation.** `ConstructionGrid`: `ShaftRulePermits` knows the stair and `StairHereOrOrdered`;
  `FarHalfAllows` puts the far half under the stair's own rule; `AllowsFootprint` is the cursor's
  question; `RefreshStair` / `RefreshStairsAround` / `RebuildStairConnectors` keep the connector
  true, from `MarkNavAround` **and from `RemoveSlab`**, which refreshed only ladders — a landing
  taken away left the stair opening on to nothing. `NavGraph.OneCellConnectorAt` takes a kind.
- **Proven, with negative controls.** `StairTests` (16): with the kind test removed from
  `OneCellConnectorAt`, `AnEditBesideTheFootLeavesTheStairWorking` fails (the connector is torn out
  and re-registered under a new id); with `RemoveSlab`'s stair refresh removed,
  `TakingTheLandingAwayClosesTheStair` fails. The price test is in `HopPriceHasOneOwnerTests`. The
  meadow golden (fast tier) and the played-board and city goldens (Long tier) did not move: no
  golden builds a stair, and the refresh finds nothing on a board without one.
- **Unproven until Unity compiles it.** `StairShape`, `ChunkMesher.EmitBuiltStair`, the module in
  `WorldRenderModel.ModuleForEdificeAt`, the ghost in `OdysseyBootstrap.DrawThingGhost` and the
  cursor's `Refused` asking `AllowsFootprint`. **Picking** is left as the city's stairs have it: a
  stair offers the floor to a click (`StandHeight` 0), not the treads.
