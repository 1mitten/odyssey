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
| `StuffDef` | which `Stuff*` value, which `ItemHandle` it is carried as, `workFactorPerMille`, `workOffsetTicks`, `hitPointsFactorPerMille` | `StuffHandle` |

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

**Note added 2026-09-17.** Every tick figure below is **work at the standard rate**, and it has
been an exact statement of elapsed time only because every colonist works at that rate. `U42`–`U44`
(`17-rates-and-stats.md`) make it a rate-relative figure: a skilled builder beats these numbers and
a novice does not reach them. Nothing here changes — the costs are content and content does not
move — but the *reading* does. `minSkill` on `BuildingDef` is the other half and is already
enforced (`BuildJob.cs:213`), inert at 0 on everything shipped.

**Numbers, and where they came from.** A wall is 5 units and 135 ticks, both the reference's own
(a-04 §6). Stone is **1.7× the work and 1.5× the hit points** of wood (a-04 §3, read off its material
table) — the one number that makes the choice of material a decision rather than a colour, which is
why it is carried in ahead of the rest of the stat block. Factors are per-mille integers, for the
reason skill experience is stored in thousandths: a factor multiplied into a tick count must give the
same answer on every machine.

**The formula now has both of its terms (U27): `stat = base × factor + offset`.** Read off a-04 §3
directly rather than reinvented — the reference's own per-material table is `factor` and `offset`
columns beside each other, and it needed both because a factor alone can only ever *scale* the base
stat: it cannot express a cost that is the same whatever the building's own size is. `WorkFor`
applies the factor first, by integer division, then adds `workOffsetTicks`, and the floor of one
tick guards the sum of both rather than the factored term alone. **Stone carries the offset, wood
does not** — a stone block wants a flat dressing-and-fitting pass that a plank does not, so
`Stuff_Wood.workOffsetTicks` is 0 and `Stuff_Stone.workOffsetTicks` is 15, Odyssey's own number
rather than the reference's (whose stone rows carry a much larger flat offset, +140 against a base
of 135, because it is stacked across five or six stone materials each with their own steep factor).
15 ticks is proportionate to the one factor Odyssey chose: a tenth of the wall's own base, so a stone
wall now costs 244 ticks (229 factored, +15) rather than 229. **`hitPointsFactorPerMille` stays
factor-only.** U27 gave the work stat an offset because `WorkFor` already turns `workToBuild` into a
real number every tick; hit points has no base stat to add to yet — no `BuildingDef.maxHitPoints`
exists, and nothing gives a built wall a damage state — so an offset there would be a second field
with nothing to consume it. That is next when a durability stat lands, not invented ahead of it.
**Two materials, not three:** the plan allows "two or three", but the natural third is already
named above and deferred on purpose — one of the three reserved-but-unbuildable stuffs (concrete,
steel, composite) becomes buildable exactly when U28's salvage line gives one of them an `item`,
and a third material's own factor and offset will have more than one wall's worth of evidence to be
tuned against once U28 and U29 add more things to build. Adding one now would be inventing numbers
with nothing yet to differentiate them against.

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

## 4a. The last hammer blow is rolled

**A completed build can botch.** When the final tick of work lands, `BuildJobDriver` rolls against
the finishing builder's construction level: on success the wall goes up as before, on failure the
banked work is thrown away, half the delivered material is lost, and **the site stands — a blueprint
again**, waiting to be fed and built a second time.

| | |
|---|---|
| Chance | `WorkTypeDef.SuccessPerMille(level)` = `successBasePerMille + successSlopePerLevel × level`, clamped to 0…1000 |
| Shipped numbers | `Work_Construction`: base **850**, slope **50** a level — so a novice botches about one wall in seven, and a **level-3** builder never botches |
| Seed | `DeterministicRandom.ForTick(worldSeed, cell ^ tick, PawnPurpose.BuildSuccess)` |
| What a botch keeps | half the delivery, the odd unit by a second flip (`PawnPurpose.BuildBotchLoss`) |
| Where it lands | `ConstructionGrid.Botch(cell, keep)` — sets work to 0 and delivery to what is kept |

**The curve is re-anchored, not copied.** The reference's own numbers (a-04 §4) run 75% at skill 0
to a certain 100% at skill **8**, and 8 is where *its* colonists sit. Ours start at an average of
**1.16**, so a certainty ceiling at 8 would mean a colony that botches most of its walls for the
whole early game. Certainty sits at **3** here for exactly the reason it sits at 8 there — just above
where a starting colony actually is. This is the same re-anchoring rule the rates line follows
(`17-rates-and-stats.md` §3b); the integers are invented and are the owner's to tune.

**The finisher rolls, not the colonist who did the work.** A building records no author, so the level
consulted is whoever happened to land the last tick. That is the reference's shape and it is kept
**deliberately**, exploit and all: it is what lets a master rescue a novice's half-built wall by
walking over and finishing it, which reads as a sensible thing for a colony to do rather than as a
bug.

**A botch loses material because otherwise it costs nothing.** The lost fraction is Odyssey's own
number — the reference says only "some resources" and a-04 records that under *could not be
determined*. Half is the deconstruct refund's arithmetic pointed the other way, so a botch costs what
a demolition returns; and both ends want the same seeded flip on the odd unit rather than a rounding
rule, or a five-wood wall would always round the same way.

**Nothing in the world changes on a botch, and that is why `Botch` marks nothing dirty.** No wall
appeared, so no chunk, no walkability and no support moved. The only state that moved is the site's
two numbers, which the hash and the save already carry — so a botch replays from a seed, survives a
save, and survives a reload. Both givers simply ask their questions again on the next scan: the
deliverer first, because material is outstanding once more, then the builder. **A botch costs the
colony work and material and never the order.**

**Every other work type is certain.** `successBasePerMille` defaults to 1000 with a zero slope, which
is the behaviour that existed before the roll — construction is the only work type that *completes* a
thing, so it is the only one with a completion to roll.

### 4a.1 Two rolls at the same instant, and the order they go in

U26 left **two** rolls at the moment of completion and they arrived from two directions: the success
roll above, and the **quality tier** that came with the bed (`20-beds.md` §6). The bed's status line
recorded itself as "closing U26's outstanding success roll", which was a misreading corrected when
the two merged — quality is the second roll, not the first, and neither subsumes the other.

They compose in one order and it is not arbitrary:

1. **Does it stand at all?** `PawnPurpose.BuildSuccess`, every building.
2. **How well was it made?** `PawnPurpose.BuildQuality`, only where `BuildingDef.takesQuality` — a
   bed does, a wall never does.

**A botch never reaches the quality roll**, because a thing that was not built has no quality to
have. The two draw from **separate salts**, so asking the first does not move the second's answer,
and both read the *finishing* colonist's Construction level — the same exploitable property, kept
deliberately at both ends for the same reason.

**A botched bed is the one place the two features touch**, since a bed is the only thing that is both
quality-bearing and two cells. It is safe for a reason worth writing down rather than rediscovering:
a site's work and delivery live on the **head** cell only, the far cell being derived from the head's
footprint and facing, and `Botch` is handed exactly the cell `Raise` would have been. So a botched
bed keeps its item hold, stays one order, and still answers to either of its cells.
`ABotchedBedRollsNoQualityAndKeepsBothItsCells` is the test, and it was checked against a build
forced to *succeed* before being believed.

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

**The botch (§4a) is four more.** `TheChanceRisesWithSkillAndStopsAtCertainty` pins the curve's
*shape* without re-pinning its literals, which the content fingerprint already owns.
`AFrameCanBeBotched` forces failure to certainty and watches one site through the whole thing — fed,
botched, work thrown away, half the wood gone, **fed again** — because the claim is what a botch
does, not how often. `TheSameSeedBotchesTheSameWay` runs one seed twice and compares the histories.
`ABotchingColonyStillRaisesItsWall` runs the shipped curve with nothing overridden and proves the
retry is not a wedge.

**A test that retunes construction replaces the `WorkTypeDef`, never writes through it.** The Defs a
content record's arrays point at are shared by every record in the process (`ContentPack`'s rule), so
`WorkTypes[i].successBasePerMille = 1_000` silently retunes every test that runs afterwards. This was
found the expensive way: the first version of these tests wrote through, and the content fingerprint
pinned in `PawnContentDefTests` was then taken from the polluted database rather than from a clean
load.

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

What it will need, in the order it should be built. **Steps 1 and 2 landed on 2026-09-17**
(`claude/forced-orders-intent`); 3 and 4 are the presentation half and are not started.

1. ~~**An intent.**~~ **Built.** `ForceJob(cell, A = job, B = pawn)` — the pawn is named, because a
   forced order is about one colonist and the existing intents are all about a cell. It is handled by
   `JobSystem.HandleForceJob`, registered in `ColonyComposition.AddColony` beside the colony's other
   commands. It resolves the cell through `ConstructionGrid.SiteAt` — the same ground-to-site lift a
   build order and a cancellation get, extracted from `Cancel` rather than copied out of it — asks
   the legality query below, ends whatever the colonist is doing **as a failure** (the one ending
   that releases claims, and how a mental break already takes a job away), and starts the ordinary
   `BuildJobDriver` through `JobSystem.StartJob` with `Job.PlayerForced` set. It is therefore the
   first thing in the game that reads that field. An illegal order is a rejection with a reason and
   changes nothing: legality is asked *before* the current job is interrupted, so a refusal cannot
   leave a colonist idle or a cell claimed.
2. ~~**A legality query the menu can ask without side effects.**~~ **Built**, and it is the piece
   that mattered. `BuildWorkGiver.CanBuild(pawn, ctx, site, out stand)` answers "could this colonist
   build that, now" and reserves nothing; `JobSystem.CanForce(pawn, ctx, job, target)` is the entry
   point a menu asks, switching on the job def — one case today, defaulting to no, because a job
   nobody has decided the forced meaning of should not acquire one by omission. **It is the scan's
   own test, not a second opinion beside it:** `BuildWorkGiver.TryGiveJob` calls `CanBuild` for every
   candidate, so the offered path and the forced path cannot drift into disagreeing about what a
   colonist may build. What it deliberately does *not* answer is **why not** — a greyed command wants
   a reason, and "nowhere to stand" and "somebody else has it" are not `IntentRejection` values. That
   vocabulary belongs with A10, which is the first thing that can display one.
3. **Input case 5 of `09-ui-and-input.md` §6** — right-click is currently orbit-drag on the camera
   rig, so a right-*click* that never travelled has to be separated from a right-*drag*, the way the
   left button already separates a pick from a box. *(Half of this shipped with the cancel tool:
   right-click puts an armed tool down. The click/drag split is what is left, and with nothing armed
   right-click is deliberately inert, reserved for this menu — see
   `16-cancel-and-deconstruct.md`.)*
4. **The menu itself**, which is a HUD panel and should be a director plus a presenter like every
   other region (09 §3). Its model asks `JobSystem.CanForce` once per command it is about to draw
   and submits a `ForceJob` intent when one is picked; nothing else in the simulation is needed.

**How to test it**, written before the feature and now half kept. The Sim lines are built —
`ForcedOrderTests`, nine tests in the fast tier; the Hud line waits on step 4 and the by-hand line on
steps 3 and 4, so **nobody has pressed Play on a forced order.**

- *Fast tier, Hud:* the menu model offers exactly the forced jobs a colonist could legally take on a
  given target view, and no others — with a negative control that a colonist who could not reach the
  target is offered nothing. **Not written: there is no menu yet.**
- ✅ *Fast tier, Sim:* a `ForceJob` intent makes the named colonist take that job **on that target**,
  past a nearer one it would otherwise have chosen. That is the whole claim, and a test that only
  asserts the job started proves nothing, because the scan would have started one anyway. **The
  control is a second site nearer the colonist.** Both halves are built:
  `TheScanChoosesTheNearerSiteWhenNobodyForcesAnything` measures that the colonist really does prefer
  the near site unforced, and `AForcedOrderSendsTheColonistPastTheNearerSite` then forces the far one
  and asserts the far wall goes up **with the near one still standing** — the order the walls appear
  in, not merely that one did.
- ✅ *Fast tier, Sim:* a forced job that becomes illegal — the site cancelled underneath it — fails
  and releases its reservation, like any other job. `AForcedJobThatBecomesIllegalFailsAndReleasesItsClaim`
  asserts the claim is gone and not only that the job ended.
- ✅ *Fast tier, Sim, and not in the original list:* the legality query refuses on its own for each of
  the three reasons separately — unreachable (a site walled into a sealed pocket), already claimed by
  another colonist, and no site there at all — and in each case claims nothing and starts nothing.
  `AskingWhetherAColonistCouldBuildChangesNothing` is the one that pins the split itself: it asks
  twice and asserts the **state hash is unmoved**, because a query that claimed on the first call
  would answer no on the second and that difference is the only symptom there would be.
- *By hand:* order two walls, one across the board, right-click the far one with a colonist selected,
  and watch them walk past the near one. **Waits on steps 3 and 4.**

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

---

## A dragged run lands on one layer (2026-09-18)

**The owner's screenshot: a wooden deck with a 2×2 hole in it**, the storey below showing through,
reported as *"I had specified wooden slabs to be built only on that floor but then it constructed
stone/steel floor on the floor below"*.

**The lift is per cell, and it is conditional.** `WhereItWouldLand` → `StandingOver` lifts a slab
order on to whatever fills its cell, and leaves it where it was over open air. That is right for one
click and wrong for a drag, because a box dragged over a walled room is not uniform: the perimeter
cells sit over walls and lift, the interior cells sit over open air and do not. Measured by probe,
one drag, one box:

```
[22,22] L11->L12 None   [23,22] L11->L12 None   [24,22] L11->L12 None   [25,22] L11->L12 None
[22,23] L11->L12 None   [23,23] L11->L11 NotPermitted   [24,23] L11->L11 NotPermitted   ...
[22,24] L11->L12 None   [23,24] L11->L11 NotPermitted   [24,24] L11->L11 NotPermitted   ...
[22,25] L11->L12 None   [23,25] L11->L12 None   [24,25] L11->L12 None   [25,25] L11->L12 None
```

Twelve built a storey up; four were refused a storey down. **A ring, and a hole.**

**`ConstructionGrid.RunLayerFor` is the one owner of a run's layer**, and both the order
(`DesignatePresenter.Submit`) and the preview (`OdysseyBootstrap.DrawRunGhosts`) ask it. The preview
needed it as much as the order did: those ghosts were stamped at the box's own `Y` with no lift at
all, so the drawn run and the placed run were on different storeys *by construction* — the same
two-owners fault as the hop price, the ladder's face and `BuildShapes`, for the fourth time in three
days.

**The highest cell wins, and the alternative was worse.** Taking the *anchor*'s lift is more
predictable in principle, but a player who starts the drag in the middle of the room anchors on open
air, which lifts nowhere and would refuse the whole run instead of a quarter of it. Over flat ground
every cell agrees anyway, so the rule only bites where the box is mixed — which is the case it exists
for. The lift is idempotent, which is what makes it safe to hand lifted cells back to `Place`: a cell
already on the open layer holds neither terrain nor an edifice, so it lifts no further.
`AskingTheRunRuleTwiceGivesTheSameLayer` pins that.

Once the run is on one layer the interior is perfectly legal — the surrounding walls ground it — and
all sixteen cells build. `AFloorDraggedOverARoomRoofsAllOfIt` asserts on the count, because twelve of
sixteen is exactly what a ring looks like and any weaker assertion would pass on the bug.

**Every existing construction test places one cell by hand**, which is precisely why none of them
could see this. `FloorRunTests` is about a run, and `TheLiftOnItsOwnSendsOneBoxToTwoLayers` pins the
fault itself so the fix can never be mistaken for a no-op.

### What this does not explain

**The material.** The four tiles look grey-plated rather than wooden. The owner clicked one: **the
inspect pane calls it a wood floor.** Three things were then ruled out, and together they say a
wood-floored cell cannot draw the stone module:

- **The ids match.** `StuffHandle.Wood` is 4 and so is `NaturalContent.StuffWood`; Stone is 5 on
  both sides. `_slabByStuff` is keyed by the same number `RaiseSlab` writes.
- **Both slab arts resolve.** The committed catalogue has real prefabs for `odyssey.module.slab.wood`
  (`SM_Bld_Base_Floor_Combined_01`) and `.stone` (`SM_Env_Ground_Tile_Half_01`), so
  `SlabModuleFor`'s per-cell group fallback — the one thing that *could* draw one material two ways,
  since `_slot` varies cell by cell — never fires for a wood floor.
- **The pane and the mesher read one field.** `CellDetailContributor` reads `_grid.FloorStuff[cell]`
  and `WorldRenderModel.CopyCell` mirrors `_floor` and `_floorStuff` on adjacent lines. They cannot
  disagree about a cell's material.

Then the owner deconstructed those floors: **the wood came back, the simulation agreed the floor was
gone, and the grey stayed on screen.** That is the observation that settles it, and it admits two
explanations wanting opposite fixes — a stale render mirror, or a surface that was never removed
because it was never the one being removed.

**Measured, through the real mirror** (`StaleFloorProbe`, kept beside `StoneFloorProbe` because this
will be asked again):

```
before any floor   grid.Floor=0 stuff=0   mirror.FloorModule=0   stuff=0
wood floor built   grid.Floor=4 stuff=4   mirror.FloorModule=134 stuff=4
RemoveSlab -> True, gave back stuff 4
floor taken up     grid.Floor=0 stuff=0   mirror.FloorModule=0   stuff=0
```

**Nothing goes stale.** The module drops to 0 in the same refresh that clears the grid, so a floor
that has been taken up stops being drawn. Together with the three checks above — one field, matching
ids, both arts resolving — a removed wood floor cannot leave a grey one behind, and a wood floor
cannot draw as stone.

**So the grey is a different surface, one cell down**, seen through the gap the run-layer bug left in
the deck above and still there after the deck above is removed. There is no second bug to fix here.
What would overturn it is a grey tile on an *unbroken* deck, which the fix above should now make
impossible; that is the version worth a screenshot, because it rules out everything in this section.

### The screenshot arrived, and the grey is stone paving

**2026-09-18, and the answer was in the owner's save folder the whole time.** The overturning picture
came back: grey plates in an *unbroken* wood deck, coplanar with it, the whole run of them, with the
deck continuing past on every side. So the "surface one cell down" reading is dead, and so is the
hole it was seen through.

**The three checks above are all still true. The conclusion drawn from them was not**, because all
three answer one question — *can a wood floor draw as stone?* — and none asks the question that
mattered: **was it ever wood?** "A wood-floored cell cannot draw the stone module" is sound, and its
contrapositive is the whole answer: a cell drawing the stone module is not wood-floored.

**Measured, by loading the owner's own saves** (`tools/dotnet/Odyssey.SaveProbe`, written for this
and kept):

```
timmy-test.odyssey    day 2   Paved/Stone 11   Paved/Wood 20   Built/Wood 31
                              Paved/Stone at (72,57,L11) touches Paved/Wood
                              Paved/Stone at (73,57,L11) touches Paved/Wood   … and (74,57), (75,57)
timmy-buildy-test     day 2   Paved/Stone  4   Paved/Wood  4
the-lost-buckets-day-3 day 4  Paved/Wood  20   Built/Wood 90     — no stone anywhere
```

**The grey tiles are stone paving, laid on the same layer as the wood beside them.** Every mixed
deck in the folder mixes *materials*, never layers, and in every one of them the stone cells are
`SlabPaved` — the **Paving** tool — and never `SlabBuilt`. A run of four at one z, with wood at the
next z, is exactly the picture.

> **Wrong, and superseded the same day — see "The grey was never a floor" below.** Stone paving is
> real and those two saves do hold it, but it is not what the owner photographed. The save from
> *that* session holds **no stone anywhere on the board**. The grey was rubble *terrain*.

Three things follow, and they are separate faults:

- **Two tools both mean "floor" and they remember different materials.** `PaletteTools` files
  `Paving` (`ui.arch.tool.deckplate`) under *Structure* **and** *Floors*, beside a second tile called
  `Slab`, and its own header calls paving "what the player simply calls a floor".
  `BuildPalette._lastMaterial` is keyed **per sub-type**, so Paving and Slab each keep their own
  material and one can sit on stone while the other sits on wood, with nothing on either tile saying
  so. Where nothing is remembered, `ApplySubType` keeps `_designate.Stuff` — the material of the
  *last tool armed* — so it also carries over from the wall you just built.
- **The pane titles a floor by its material and never by its kind.** `InspectModel.DescribeCellAt`
  builds "Stone floor" from `FloorStuff` alone, so paving and a structural floor are the same
  sentence and the one word that would have ended this — *paving* — is never printed.
- **The two slab arts did not share a top surface.** Both catalogue rows carried `baseAtY: 0`, so
  neither was normalised to the cell's floor plane and each sat on whatever pivot convention its own
  artist used. **Fixed; see below.**

**Unverified, and the cheapest thing to check first.** The owner clicked a grey plate and the pane
said *Wood floor*, which on this reading means the pick landed on the wood cell *behind* it — the
expected consequence of drawing a slab below its own cell's floor plane, since the plate's image
slides down-screen out of the cell the picker marches through. It is stated here as the likely
reading rather than a measurement. It falls out on its own if the slab arts are levelled, and until
they are, clicking near the far edge of a grey plate should name the neighbour while clicking near
the front edge names the plate.

### Every slab's top face is at one height, 8 mm above the cell floor

**Measured first, and the measurement corrected the guess.** From the screenshots the stone plate
read as *recessed* into the deck. It is the opposite — `SlabHeightProbe`, against the real
`ModuleLibrary`, with y = 0 the plane a colonist stands on:

```
                             before                 after
odyssey.module.slab          top=+0.008  t 0.101    top=+0.008  t 0.101
odyssey.module.slab.concrete top=+0.008             top=+0.008
odyssey.module.slab.deck     top=+0.008             top=+0.008
odyssey.module.slab.wood     top=+0.008             top=+0.008
odyssey.module.slab.stone    top=+0.033  t 0.033    top=+0.008  t 0.033
```

**The street tile stood 25 mm *proud* of the plank deck**, not below it, and the sunken look is the
tile's own art — a raised cross inside a frame — rather than its placement. The deck's own +0.008
was not an error and is now the height they all share; see the clearance below for why it is not 0.

**The rule now has one owner.** `ModuleEntry.topAtY` places a piece by its **highest** point, which
is the right question for anything walked *on*; `baseAtY` asks the opposite and keeps precedence, so
every piece already placed correctly is untouched. `PlayScene.Slab` sets it, which covers the five
buildable slab ids and the five street surfaces.

#### And the clearance is load-bearing

**Levelling the slabs on to the plane *exactly* made the board flicker** (owner, 2026-09-18, within
the hour: *"there is all sorts of flickering happening to tiles now — maybe fighting layers?"*).
They were right about the cause. `CellMetrics.FloorCentre` for cell *y* is at `y * SizeY`, which is
**the top face of the terrain block filling cell y−1** — so a slab whose top face sits on the plane
is coplanar with the ground, and paving, whose entire purpose is to be laid on ground that is
already there, z-fights with it.

The +0.008 the plank deck happened to carry was never slop. It was clearance, and taking it away is
what the first fix did wrong.

**`CellMetrics.SlabLift` is that clearance, and `topAtY` applies it** — the top face lands `SlabLift`
*above* the placement height, never on it. It lives in the rule rather than in each row, or the next
walked-on piece has to remember it. 8 mm because that is the number that had already never
flickered, and 8 mm against a 3 m layer does not read as a kerb.

**The test now pins both halves**, because the first version would have passed the flicker straight
through: every slab agrees *and* they agree at a height that clears the ground. "All the slabs are
level" is necessary and is not sufficient — levelling them all on to the one plane the ground
already occupies satisfies it perfectly.

**The old spelling was `baseAtY = false`, and it was only ever right by luck.** Its comment already
said the intent exactly — *"the walking surface is the cell floor and the slab's own thickness hangs
below it"* — but "not base-at-Y" is not "top-at-Y": it is *no rule at all*, and it lands each piece
on its artist's pivot. One prefab happened to be near enough and the other was not.

**Pinned by `EveryFloorSlabPutsItsWalkingSurfaceOnTheCellFloor`**, which walks every `FloorSlab` row
in the committed catalogue and asserts the resolved bounds' top is `CellMetrics.SlabLift` ± 1 mm. It
asks the *resolved* module, not the row, so it walks the real arithmetic rather than re-reading the
flag; and it counts what it checked, because a rule whose loop never runs has quietly stopped being
one.

**Regenerating the catalogue takes two commands, not one.**
`PlayScene.RebuildCatalogue` rebuilds the rows and **drops the `appearance` block** — the 311 atlas
swatch rectangles that clothe the 61 colonists — so `CharacterSwatches.Classify` has to run after it
to put them back. `CharacterSwatches`'s own header says it writes only appearance and deliberately
does not rebuild; the inverse is just as true and was not written down anywhere. A rebuild alone
looks like it worked: it exits zero, keeps all 138 rows and every prefab reference.

```
scripts/unity.sh exec Odyssey.EditorTools.PlayScene.RebuildCatalogue
scripts/unity.sh exec Odyssey.EditorTools.CharacterSwatches.Classify   # or the colonists go bald
```

**What this does not fix**, and it is the part worth an eye: 25 mm is 1% of a cell, so levelling the
two slabs will *not* by itself make a stone floor read as part of a wooden deck. The grey plates are
`SM_Env_Ground_Tile_Half_01`, a **street** tile, and they read as ground because that is what they
are. If a built stone floor should look like a floor rather than like pavement, that is an art
choice and it is the owner's.

**The lesson is the one `docs/lessons.md` already holds and this line of work keeps paying for.**
Three sessions argued about this tile from stills and from reading, and produced three confident
wrong answers. The save files were on the same disk throughout. `Odyssey.SaveProbe` exists so that
the next report starts from the file.

### The grey was never a floor

**2026-09-18, fourth round, and the one that ends it.** The owner could not reproduce any of it in a
new game and asked the right question: *"could an old game cause problems?"*

`Odyssey.SaveProbe` on the save from the session that was photographed:

```
the-lost-buckets-day-3.odyssey  day 4
    Floor=Paved  Stuff=Wood   20
    Floor=Built  Stuff=Wood   95        <- no stone slab anywhere on the board
    terrain Rubble  8                   <- eight cells
```

**Eight rubble cells, and eight grey plates in the screenshot.** `PlayScene` registers
`Slab(ModuleIds.Terrain("Rubble"), "SM_Env_Ground_Tile_Half_03")`, so rubble *terrain* is drawn with
a grey street tile — the same family as the stone slab art, which is why three sessions kept
recognising it as one.

**A surface terrain and a floor slab can share a cell, and a collapse guarantees they will.**
`SupportSystem.Rubble` writes rubble into `FirstFloorAtOrBelow(cell)` — on purpose, so debris lands
on something rather than in mid-air, and on purpose not solid so it buries nothing. **The cell it
chooses therefore has a floor by definition.** `ChunkMesher.EmitFloor` draws the slab and
`SurfaceContributor` draws the surface tile, both at that cell's floor plane, and neither knows
about the other.

**This is why the pane and the picture disagreed, and the pane was right all along.**
`CellDetailContributor` reads `FloorStuff`, the floor really was wood, and "Wood floor" was the
truth. The grey on top of it was not a floor and has no `FloorStuff` to report. Every explanation
offered across three sessions was about the floor — a stale mirror, a hole one layer down, stone
paving — and the answer was a second thing drawn in the same place.

**The fix lifts the rubble on to the floor rather than hiding it.** Hiding was shorter and wrong:
rubble refuses to be built on until it is cleared (`TerrainDef.buildable`), so a cell that silently
rejects orders with nothing on screen to explain why is the "command that does nothing and says
nothing" this build has apologised for three times. It is a heap lying on a deck, so it is drawn as
one, `CellMetrics.SlabLift` above the slab — the same clearance a slab takes over the ground, taken
again over the slab.

**Pinned by `RubbleLyingOnAFloorIsDrawnAboveTheSlabAndNotInIt`**, which meshes the same cell twice,
once with a floor under the rubble and once without. An instance matrix carries its prefab's own
normalisation and the ground relief varies with x and z, so neither the two instances in one cell
nor a cell against its neighbour isolates the lift; the same cell in two worlds holds both constant.

**What the levelling work above was, in hindsight.** The 25 mm step between the slab arts was real
and is fixed and tested, and the clearance rule came out of it. But it was never what the owner
photographed, and levelling the arts on to the plane is what made the board z-fight. **Three wrong
answers in a row, all reached by reasoning about a screenshot.** The save file answered it in one
command, and `docs/bug-patterns.md` P6 is about exactly that.

**Rubble is worth a look of its own** (§8): a grey street tile is what "a heap of debris" draws as
today, and on a wooden deck it reads as somebody's paving rather than as a mess to clear. That is an
art call and the owner's.
