# 15 — Building: sites, materials, and the orders that fill them

**Status: built, played and accepted.** Written 2026-09-17 on branch `claude/build-pipeline`
(PR #63). U26 of `docs/plans/vertical-slice.md`.

The owner ordered walls in the running game on 2026-09-17 and the colony built them; the two faults
that playtest found — a stepped wall (§7) and a hollow one — were fixed, and the cursor and the
widening gate of §5a were judged in play and accepted on the same day. **The drape and the fill were
in that build and drew no complaint**, which is weaker than a judgement and is recorded as what it
is. What remains unlooked-at is listed in §8.

Read `03-systems-catalogue.md` §4 for the design intent and `docs/research/a-04-building-and-materials.md`
for what the reference does. This file is what was actually built, what it costs, how to test it,
and what is still open — including **forced orders**, which is the next piece and the reason this
file exists in advance of it.

---

## 1. What a build order is

```
  palette: pick a thing and a material
        │
        ▼
  PlaceBuilding(cell, A = BuildingHandle, B = StuffHandle)     ← an intent, like every player command
        │
        ▼
  ┌─────────────────────┐   material arrives    ┌──────────┐   work applied   ┌────────────┐
  │ SITE — a blueprint  │ ────────────────────► │  FRAME   │ ───────────────► │ EDIFICE    │
  │ delivered < cost    │                       │ delivered│                  │ a real wall│
  └─────────────────────┘ ◄──── CancelBuilding ─└─ = cost ─┘                  └────────────┘
            refunds whatever was delivered
```

**Blueprint and frame are not two records.** The reference keeps them as separate entities and has
to, because its blueprint is a thing in the world with its own def. Ours is a cell with four numbers
on it — def, stuff, delivered, work — so the distinction is arithmetic: material not all arrived is a
blueprint, all arrived is a frame. Nothing is created, destroyed or swapped at the moment the last
plank lands, and there is no state machine to get wrong. The two work givers simply ask different
questions of the same row.

## 2. Two tables, not one

`Sim/Construction/ConstructionContent.cs`, mirrored in `Defs/Core/World/Buildings.xml` with the
in-code table as the oracle (the arrangement OQ-16 settled for terrain).

| | Holds | Indexed by |
|---|---|---|
| `BuildingDef` | what it becomes (`edifice`), whether it blocks, `costCount`, `workToBuild`, `minSkill`, `iconKey` | `BuildingHandle` |
| `StuffDef` | which `Stuff*` value, which `ItemHandle` it is carried as, `workFactorPerMille`, `hitPointsFactorPerMille` | `StuffHandle` |

A wall is a wall whether it is wood or stone, and wood is wood whether it is a wall or a door.
Folding them into one table of "wooden wall, stone wall" looks simpler at two of each and multiplies
at six. It is also the shape `PlacedEdifice` has carried since the ruined city was written: a def and
a stuff, side by side.

**The join that did not exist.** Wood and stone were `ItemDef`s — things on the floor with a stack
limit. Walls were made of `CoreContent.Stuff*` values the generator stamped. Nothing anywhere said a
pile of wood could become a wooden wall, which is why nothing could be built although both halves had
been in the project for weeks. `StuffDef` is that bridge and `NaturalContent.StuffStone` joins
`StuffWood`, so the two things a colony can dig up are the two it can build with, with no refining
step between (owner, 2026-09-17).

**Only what a colonist can carry is buildable with.** `IsBuildable` is `item >= 0`. Concrete, steel
and composite are what the ruined city is *made of*: nothing produces them, nobody can pick one up,
and offering them would be offering an order that can never be filled. They keep their stuff indices
because every stamped wall carries one. A salvage line that turned rubble into steel would give one
an item and put it on the menu, with no other change anywhere.

**Numbers, and where they came from.** A wall is 5 units and 135 ticks, both the reference's own
(a-04 §6). Stone is **1.7× the work and 1.5× the hit points** of wood (a-04 §3, read off its material
table) — the one number that makes the choice of material a decision rather than a colour, which is
why it is carried in ahead of the rest of the stat block. Factors are per-mille integers, for the
reason skill experience is stored in thousandths: a factor multiplied into a tick count must give the
same answer on every machine.

## 3. Where the state lives

`Sim/Construction/ConstructionGrid.cs`, shaped after `DesignationGrid` throughout: per cell, sparse,
hashed, saved (`odyssey.construction`), published, and **validation in one place**.

A site may stand where the cell is inside the map, in open air, with a floor under it, nothing already
standing there, and no water. A tree counts as something standing there — fell it first, exactly as
the ground under a tree cannot be mined until the tree is down.

**An order named at solid ground means the cell standing on it** (`StandingOn`). The picker answers a
click on bare grass with the ground *block*, and a wall goes in the air above it. Without the lift
every cell of a wall dragged across a meadow is refused in silence. It is the same relation
`DesignationGrid.TreeAbove` handles from the other side, and `Cancel` mirrors it.

**A refund goes to the nearest cell with room, not to the site's own cell.** A cell with a site on it
may perfectly well have a stack of something else in it — the site is an order *about* the cell, not
an occupant of it — and a cell holds exactly one item record.

## 4. Who does the work

Both givers self-register by existing (`WorkGiverRegistry`, OQ-44). Work type
**Construction**, which scans **before** cutting, mining and hauling: a site is work already begun,
and a colony that wanders off to fell another tree with half a wall standing finishes nothing.

| Giver | Offers | Driver | Toils |
|---|---|---|---|
| `DeliverWorkGiver` | a site with `Outstanding > 0` and a reachable stack of the right material | `DeliverJobDriver` | walk to it, take it up, carry it, put it in |
| `BuildWorkGiver` | a site that `IsFrame` and a colonist at or above `minSkill` | `BuildJobDriver` | walk to a stance beside it, swing, settle |

**Delivery is construction work, not hauling.** A haul takes a thing to a stockpile because it is
tidier there; this takes a thing to a site because a wall cannot be built without it. If it were a
haul it would sort with the tidying, so a colony would sweep its floors while a half-ordered hut stood
empty — and the labour would train the porter rather than the builder, which is the less useful of the
two lies available.

**One deliverer per site**, claimed on the site's cell. Without it two colonists each fetch a full
stack for a wall that wants five. The builder claims the same key later, which is free: a site still
waiting for material is never offered to the build giver anyway.

Work is **banked on the cell**, so a builder who breaks off for a meal, a sleep or a mental break does
not throw the morning away — the same argument, and the same shape, as mining's.

Presentation: `WorkStyle.Building` (the hammer) is reached through `IndexForJob`, which is what that
style was written for and could not do until `JobHandle.Build` existed.

## 5. The contract

`SiteView` carries **real counts and real ticks**, where `OrderView.Progress` is a quantised byte.
That difference is deliberate, not an inconsistency: an order's progress is a *picture* — what reads
on screen is whether a face is barely scratched or nearly through — while a site is asked a
*question*. "Three of five wood" and "about nine seconds" cannot be recovered from a fraction, and the
alternative is the interface keeping a second copy of the cost table.

The Hud assembly cannot see `Odyssey.Sim` (ADR 0003), so `BuildLabels` is a table parallel to
`BuildingHandle` and `StuffHandle`, exactly as `JobLabels` is to the job indices. **It will not
survive the catalogue growing** — ten categories and eighty tools — at which point the registry key
belongs on the published view beside the handle, or the whole set comes out of the Defs.

## 5a. The cursor, and the gesture that draws it

Both of these are the owner's, from the first playtest that built anything (2026-09-17).

**The cursor is the wall, not the cells.** A build drag draws one closed wireframe box — all twelve
edges — spanning the whole run, where it used to draw the selection bracket's corner stubs once per
cell. Along six cells the stubs read as a dotted line and say "these are things you have picked",
which is the wrong sentence: what a player wants to see before letting go of the button is where
the wall starts, where it ends and how tall it will stand.

The box is **draped, not lifted** (`ChunkRenderer.DrawWireBox`), so it shears onto the tangent plane
of the drawn ground at its own centre exactly as the finished wall does — §7's rule, applied to the
thing that promises the wall as well as to the wall. Lifted, a fifteen-metre box would take one
height from one point and float or sink at its far end by the field's slope across the whole run.
Vertical edges stay vertical under a shear, so the cursor stands plumb and full height on a slope.

**A run that steps up a riser is one box per level.** A build order is lifted onto the cell standing
on solid ground, decided per column (`ConstructionGrid.StandingOn`), so a run crossing a terrace
stands on two layers at once and one box around all of it would be a box around neither.
`BuildPreview.Gather` is that split, in `Odyssey.Hud` and covered by the fast tier, with the lift
supplied as a function of the column because this assembly cannot see a grid.

**A build box does not widen by accident.** *"The building is a tad sensitive and by accident you
can build dual walls"*: a wall dragged along one axis with the pointer a single cell off the row
covered two rows, and two parallel walls were ordered, carried to and paid for out of a gesture that
meant one. At this camera, on a board drawn in perspective, one cell of wander is not a mistake a
player can simply stop making.

The rule is **hysteresis, not a snap to a line** (`DesignateDirector.DragTo`), so that a rectangle of
wall is still one gesture: the box widens when the drag has gone `WidenAcross` = **3** cells clear
across the run, and narrows again once it is back within `NarrowAcross` = **1**. Two thresholds
rather than one, because a single threshold makes the box flicker between one row and two while the
pointer rests on the boundary. The gated axis is whichever one has travelled less, decided afresh
each frame, so a drag that turns a corner is still one gesture.

**Both numbers were loosened on a second report** — still "too easy to create double walls" (owner,
2026-09-17, playing the first version). There were two faults, and the threshold was only one of
them. Two cells is five metres of board, which sounds like plenty until you draw twenty metres of
wall at a camera looking down a slope. The other was that the gate was **sticky**: re-arming only on
the anchor's exact row made a trip effectively permanent, because a pointer that has strayed rarely
comes back to precisely the row it left — so one wander anywhere in a long drag left the player
releasing over a rectangle, having never seen the moment it widened. Three up and one down fixes
both: a bigger deliberate movement to widen, and recovery as soon as the pointer is near the row
again.

**Build only.** Mine, fell and cancel keep every cell their box covers. The same slip does not cost
the same thing: one more cell marked to dig is a rounding error, and one more row of wall is a wall.

## 6. How to test it

### Headless, in the fast tier

```
scripts/test-fast.sh --filter Name~AnOrderedWall
```

`ConstructionTests.AnOrderedWallIsFedWorkedAndRaised` orders a wall on a real generated board, puts
twenty wood near it, ticks, and asserts a wall stands, is blocking, remembers its material, and cost
exactly five. Its negative control `AWallIsNotBuiltOutOfNothing` runs the same board and order with no
wood and asserts the site stays a blueprint for ever.

**Both halves matter.** A test that has not been seen to fail is not evidence, and the whole feature
once passed every test in the repository while doing nothing in the game (§7).

### By hand, in `Play.unity`

1. Let some trees come down — `ScenarioDef.Playtest` marks every tree within 10 cells before the first
   tick, and one tree is 27 wood, enough for five walls.
2. **B** opens the palette, **Wall**, then wood or stone.
3. Drag a run over open grass. A green box closes around the **whole run** while you drag — see
   §5a. Wander the pointer a cell off the row: the box should not widen. Take it two cells clear
   and it should.
4. **Click a blueprint.** The inspect pane says what it is, what of, and either
   "*2 of 5 wood delivered*" or "*about 2s left*".
5. **Esc** puts the tool down; the banner above the command bar says so while it is held.

### Read the console

**Every refused command now logs**, grouped and throttled, one line per (command, reason) per second.
Nothing read `Intents.Rejected` before 2026-09-17, which is why two playtests were spent inferring a
fault from an absence. The three outcomes and what each means:

| Line | Meaning | Where to look |
|---|---|---|
| `PlaceBuilding: UnknownIntent` | nothing in the colony handles the command — a **composition** fault, never a rule | `ColonyComposition.AddColony` |
| `PlaceBuilding: NotPermitted` | the order reached the simulation and was refused on the merits | `ConstructionGrid.Allows` |
| nothing logged, no wall | the orders are accepted and the **work** is not happening | the two givers, or `FellJobDriver.StandBeside` |

## 7. The fault this line has already had, and the lesson

The construction grid arrived as an optional parameter to `AddColony` defaulting to null. All twelve
call sites went on compiling, and eleven — including `OdysseyBootstrap`, the one the game runs —
silently built a colony with no handler for `PlaceBuilding`, no sites, and both givers answering no
for ever. **Every test passed**, because every test builds its world through `ColonyWorld`, the
twelfth call site, which did pass one.

`ColonyComposition`'s own class comment had predicted it in as many words. The fix is the signature,
not the call site: `AddColony` builds the grid itself and hands it back through an `out`, so
forgetting is not expressible. The standing guard is
`ConstructionTests.EveryIntentTheInterfaceCanSendIsAnsweredByTheColony`, which walks every
`IntentKind`. The general rule is in `docs/lessons.md`.

**A related one, same day:** adding two job indices left `JobLabels.IconKeys` two entries short, and
it is bounds-checked — so every builder and every porter in the game displayed as **idle**. Not a
compile error; a silent lie. `RegistryTests` holds the table's length to `JobHandle.Count` now.

**And a third, once walls finally went up: they went up stepped** (owner's screenshots, 2026-09-17).
Every panel of a finished wall sat a few centimetres above or below its neighbour, with a notch at
each cell join and at each corner. Nothing about building was wrong — the fault was a year older
than this line and belonged to the drawn ground.

`ChunkMesher` placed a wall panel with `GroundRelief.Lift`, which takes one height from one point.
Two panels in a run stand 2.5 m apart on a field of amplitude 2 m and period 150 m, so their
heights differ by the field's slope across a whole cell: **73 mm on average over this board and
220 mm at the worst of it**, measured, against a 3 m wall. Floors, doors, ladders, stairs and
pillars were placed the same way and had the same fault waiting.

The fix is `GroundRelief.Drape`, which the ground, the banks and the water already used: the piece
is sheared onto the tangent plane of the field at its own centre, so two neighbours are tangent
planes of one smooth surface and part company only by its curvature — **1.1 mm where the step had
been 147 mm**, on the same cells. The shear leaves vertical edges vertical, so a wall stays plumb
and a full 3 m tall; only its head and its foot rake with the ground, which is what a wall built
along a slope does.

**The rule, and it is the general one:** *anything fixed to the grid is draped; only what moves over
it is lifted.* A tuft, a dropped log, a colonist and a cursor stand at a point and share an edge
with nothing, so a lift is right for them. Anything that fills a cell or a face abuts an identical
neighbour, and a lift cannot close that seam. `EmitWater` had already made this argument in full,
having got it wrong twice; the built world was not reading it.

The guards are `ChunkMesherTests.AWallRunMeetsItselfAtOneHeightOnRollingGround`,
`.AFloorMeetsItselfAtOneHeightOnRollingGround` — which walk every drawn corner, pair the ones
standing over the same point of the board and hold their disagreement under 20 mm — and
`.ADrapedWallStaysVerticalAndFullHeight`, so flushness can never be bought by leaning the building
over.

## 8. Open

### The reported fault, now reproduced and gone

The owner reported walls still not going up after the composition fix. On the next run they went up
(screenshots, 2026-09-17), so the composition fix was the whole of it and the rejection log of §6
was never needed to settle it. It stays, because it costs nothing and the next silent refusal will
want it.

### What is still unlooked-at

The playtests covered ordering a wall, the material row, the cursor and the widening gate. Nobody
has yet judged **the site marks** (a mark plus a slab rising from the floor, §8 below), **the
inspect pane's blueprint readout** — "2 of 5 wood delivered", "about 2s left" — or **the hammer
swing** itself, which is computed rather than animated. They are all in the build and none has been
reported on either way.

### Forced orders and the context menu — the next piece

**This is what §6's test procedure is being written in advance of.** Today a colonist takes the
nearest site the scan offers, and a player who wants *that* wall built *now* has no way to say so.
Two designed surfaces answer it and they are not the same thing:

- **A10 command grid** (`10-ui-panel-catalogue.md`, milestone M3) — every legal action for the
  current selection, inside the inspect pane. This is the home for "Cancel", "Deconstruct" and
  eventually "Prioritise", and it reads a `CommandDef[]` filtered by selection class.
- **A right-click context menu on a target**, which is the reference's own idiom and what the owner
  asked for: with a colonist selected, right-click a site and pick "Build this now" from a menu of
  the forced jobs that colonist could legally take on that target.

**The simulation hook already exists and is already saved and hashed: `Job.PlayerForced`.** Nothing
reads it yet. A forced order is not a new job kind — it is the existing `BuildJobDriver` with the
scan bypassed, the reservation taken immediately, and the job pushed onto the pawn rather than
offered to it.

What it will need, in the order it should be built:

1. **An intent.** `ForceJob(cell, A = job, B = pawn)` — the pawn must be named, because a forced
   order is about one colonist and the existing intents are all about a cell.
2. **A legality query the menu can ask without side effects.** The menu has to be built *before* the
   player chooses, so "which forced jobs could this colonist take on this target" must be answerable
   without reserving anything. Today every giver decides legality and claims in one pass; splitting
   that is the real work of this unit, and it is worth doing because the A10 grid needs exactly the
   same split to grey a command with a reason.
3. **Input case 5 of `09-ui-and-input.md` §6** — right-click is currently orbit-drag on the camera
   rig, so a right-*click* that never travelled has to be separated from a right-*drag*, the way the
   left button already separates a pick from a box.
4. **The menu itself**, which is a HUD panel and should be a director plus a presenter like every
   other region (09 §3).

**How to test it when it lands**, so the procedure exists before the feature:

- *Fast tier, Hud:* the menu model offers exactly the forced jobs a colonist could legally take on a
  given target view, and no others — with a negative control that a colonist who could not reach the
  target is offered nothing.
- *Fast tier, Sim:* a `ForceJob` intent makes the named colonist take that job **on that target**,
  past a nearer one it would otherwise have chosen. That is the whole claim, and a test that only
  asserts the job started proves nothing, because the scan would have started one anyway. **The
  control is a second site nearer the colonist.**
- *Fast tier, Sim:* a forced job that becomes illegal — the site cancelled underneath it — fails and
  releases its reservation, like any other job.
- *By hand:* order two walls, one across the board, right-click the far one with a colonist selected,
  and watch them walk past the near one.

### A wall was hollow and open-topped; it is filled and capped now

Raised by the owner on 2026-09-17, looking at the first finished room. A wall cell is drawn as a
**panel on each exposed face** — that is what stops a one-cell wall reading as a 2.5 m slab, and it
is deliberate (`ChunkMesher`'s class comment). But a straight run puts two panels 2.5 m apart with
**2.25 m of nothing between them and nothing over them**, so from a high camera every wall has a
black slot down its middle, and a slice or an x-ray looks straight into it.

The owner's questions were three, and they separate:

1. **"Colonists always build from the outside so they don't get stuck."** Already true, and not a
   drawing question: both `DeliverWorkGiver` and `BuildWorkGiver` pick their stand cell with
   `FellJobDriver.StandBeside`, and the comment at the delivery site says why in as many words — the
   site is walkable right up to the moment the wall goes up in it, so standing *in* it would work
   for the delivery and be exactly wrong for the build that follows. Nothing to do.
2. **"Eventually build electricity through it."** A conduit in a wall is a second thing in the same
   cell, which is a simulation question about what a cell can hold, not about how the wall is drawn.
   Neither option below helps or hinders it.
3. **"Is it worth making the wall half the size of the cell and making it like a block?"** This is
   the real question, and it is two questions wearing one coat: *should the cell be filled* and
   *how thick should a wall look*.

**Filled, thickness left alone** (owner's call, 2026-09-17). The panels stay on the exposed faces,
because that is where the kit's art is — plaster outside, brick inside — and behind them the cell
carries one block, `ModuleIds.WallCore`, which is both the core and the cap. From outside the wall
is identical to before; from above it has a top; in a cut-away it is solid. No new art (it falls
back to the cell-shaped primitive and wears the wall's own stuff tint), no orientation logic, one
extra instance per wall cell, and it is presentation-only, so nothing enters a cell, a save or the
hash. It sits 1 cm below the panels' heads so that two opaque surfaces are never coplanar — a
z-fight along the head of every wall in the colony is a shimmer the camera cannot get away from,
and a centimetre is sub-pixel at the nearest the camera comes. A window keeps its hollow, which is
the one thing a window must not lose.

**A wall already occupied the full 2.5 m of its cell as drawn**, so the core changes nothing about
how thick a wall *reads* — it only removes the slot. That is worth saying because it is the usual
objection, and because it is what makes the thickness question separable.

**The half-cell wall is a bigger change and should wait for an eye on the capped one.** It buys a
wall that reads as a wall rather than as a rampart, and it costs: a per-cell run direction (which
the earth blocks already solve — `GroundMesh.CanonicalExposure` turns all sixteen neighbour
patterns into five meshes and a rotation, and a wall junction is the same problem), meshes for the
straight, the corner and the tee, and a decision about the 0.6 m of bare cell it would leave on
each side, which today's floor slab does not cover because the floor stops at the cell boundary.
**The cheapest experiment that settles it is the cap, and it is now built:** look at a room and say
whether a 2.5 m wall is a fortress or just a wall. If it is a fortress, the half-cell block is the
answer and the core is the only work thrown away.

Guards: `ChunkMesherTests.AWallCellIsFilledAndCapped` states the claim over the cell's own centre
line, which is exactly where a panel never reaches and a core always does, and
`.AWindowIsNotFilledIn` holds the exception.

### Also open

- **Deconstruct** is a `DesignationKind` that nothing acts on. The driver that builds can take apart,
  refunding half (a-04 §1). It is the cheapest remaining item in this line.
- **Support is not marked dirty when a wall goes up**, deliberately — nothing collapses yet and
  mining makes the same omission. U29 wires both, and neither should quietly acquire behaviour the
  other lacks.
- **A site draws as an outline and a rising slab, not a ghost of the thing.** A ghost wants the
  mesher to place a module it has not been asked for: the mesh-contributor seam, OQ-46.
- **A site that wants five wood and a stack of seventy-five** means the deliverer carries seventy back.
  Correct, and wasteful; splitting a stack where it lies cannot be done while a cell holds one item
  record.
- **Floors and roofs (U29)** are the unit the project exists to prove and are not started. A slab is
  the same pipeline with a different `BuildingDef`, which is the point of having the tables.
