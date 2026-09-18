# Bug patterns

**What this is for.** `docs/lessons.md` records operational lessons — the things that waste an hour
of *tooling* time. This file records the **bugs themselves**: what the symptom looked like, what it
actually was, how it was found, and the check that would catch the next one of its kind. It exists
because this project keeps meeting the *same three or four faults wearing different clothes*, and a
session that recognises the pattern fixes in an hour what took a day the first time.

**Read the patterns first, then the register.** If a report matches a pattern, go straight to the
measurement that pattern names — do not start by reading code.

**Add to it whenever a bug is fixed.** One row in the register, and a new pattern only when the fault
genuinely does not fit an existing one.

---

## The patterns

### P1 — One rule, two owners

**By far the most common fault in this codebase.** Two pieces of code answer the same question,
agree today, and drift apart tomorrow. It always fails *silently*, because each copy is correct on
its own terms and no test that exercises one exercises the other.

Every occurrence so far:

| The question | The two owners | How it showed |
|---|---|---|
| What does a hop cost? | cell search, region graph, mover | pinned by `HopPriceHasOneOwnerTests` |
| Which face is a ladder on? | `ChunkMesher.EmitLadder` (fell back north), `TryWallBeside` (fell back to nothing) | figure climbed through the air |
| Does this thing rotate? | the Defs, and `BuildShapes.Rotates` | R did the other of its two jobs, silently |
| Which cells does a bed claim? | the simulation's guard, and the ghost | ghost drew legal, click did nothing |
| What layer does a run land on? | `Place`'s per-cell lift, and `DrawRunGhosts` (no lift at all) | a deck with a hole in it |
| **Does a floor hide what is beneath it?** | `ChunkMesher.EmitScatter` (asks), `SurfaceContributor` (never asked) | **rubble drawn through a wooden deck, chased for three sessions** |

**The fix is always the same**: name one owner, make every other site *ask* it, and write a test that
walks both. Never restate the rule "just here"; never answer a disagreement by changing one copy.

**The trap:** an assembly boundary (ADR 0003) forces some tables to be duplicated — `BuildShapes`,
`BuildLabels`. Those are allowed, but only with a test that walks the copy against the original
(`BuildShapesAgreementTests`, `RegistryTests`).

### P2 — The rule asks the built world, and misses the order

A rule that reads `_grid.Edifice` / `_grid.Floor` sees only what **exists**. Sites — blueprints —
are invisible to it, so two individually legal orders combine into an illegal building.

**The tell is the word "sometimes"** in a report. If the outcome depends on which job a colonist
happened to pick up, suspect this immediately.

**The fix**: the rule consults sites as well as built things (`LadderHereOrOrdered`,
`FloorHereOrOrdered`), *and* is asked again at the moment of truth in `Raise`, because a rule that
spans two separately-ordered cells can be invalidated after it was checked.

**Do not re-ask the whole of `Allows` at `Raise`** — a site with its own material hauled to it fails
`needsClearCell`, so that would refuse every bed whose wood had been delivered. Re-ask the one rule
that can go stale.

### P3 — A compatibility clause that keeps the bug alive

A clause kept so old data still works ("a real floor still counts") silently preserves exactly the
behaviour a new rule was written to forbid. New ones cannot be made; the old ones go on misbehaving.

**Ask what it is actually holding up, by measurement, before paying to migrate.** The ladder clause
was believed to require worldgen changes, a golden re-bake and a save break. Measured: **all three
golden masters were byte-identical**, because worldgen's ladders reach the nav as `StampedConnector`s
and never consult the rule at all. The expensive migration did not exist.

### P4 — A conditional rule applied per cell across a dragged run

A lift, a snap or a default that is *conditional* will resolve differently for different cells of one
drag, splitting a single gesture across two layers or two states. The player gave one order and got
two outcomes.

**The fix**: resolve it **once per run** and apply that answer to every cell, and make the preview ask
the same owner. See `ConstructionGrid.RunLayerFor`.

**Every existing construction test placed one cell by hand**, which is exactly why none of them could
see it. When a mechanic has a drag gesture, test the *drag*.

### P5 — The symptom is in presentation, the cause is one layer away

"It looks wrong" reports are ambiguous between the cell, the data and the drawing. Before hunting a
renderer bug, establish **which of the three** is wrong, because the fix lives in a different file for
each.

The cheap discriminators, in order:

1. **Do the pane and the renderer read the same field?** If yes, they cannot disagree about a cell,
   and the thing on screen is a *different cell* than the one being described.
2. **Does the mirror track the grid?** Drive `WorldRenderModel` directly and print grid-vs-mirror
   after each edit (`StaleFloorProbe`). Staleness is then proven or excluded in one run.
3. **Does the art resolve?** A per-cell fallback (`SlabModuleFor` → group slab) can draw one material
   two ways. Check the catalogue has real art for the material before blaming anything else.

**And when all three come back clean, read what they proved.** They exclude the *drawing*; they say
nothing about what the player actually ordered. Three sessions ran these, got three clean answers,
and invented a renderer fault anyway (P6).

### P6 — Excluding every way it could go wrong, without asking whether it went wrong

A report is assumed and then defended. Every mechanism that could corrupt the thing is excluded, one
by one, correctly — and the conclusion drawn is that the mechanism must be more exotic, rather than
that **the thing was never what the report called it**.

The tell is a clean exclusion that nobody turns around. "A wood floor cannot draw as stone" is also
"a cell drawing as stone is not a wood floor", and the second reading ends the hunt in a minute. The
grey tile cost three sessions to the first reading.

**The fix is a habit, not a check: name the player's action, and go and read it.** The owner's save
files hold the order, the material and the kind of every cell, and they were on the same disk for the
whole of that hunt. `tools/dotnet/Odyssey.SaveProbe` loads one without Unity and prints them.

**Reach for the file before the code** whenever a report says a thing "is" something — a wood floor,
a stone tile, the top level. That is the player's name for what they see, not a reading of state.

### P7 — The fix satisfies the test and breaks the thing the old value was quietly doing

A number is found to be inconsistent and is normalised to the clean value — zero, the plane, the
default. The inconsistency goes; so does a margin nobody had written down, because the old value was
doing *two* jobs and only one of them was named.

It slips through because the test written alongside the fix pins the property that was wrong and not
the property that was right. Every slab's top face at +0.008 and +0.033 is a real fault, and
levelling them all on to 0.000 fixes it — and satisfies "they all agree" perfectly while making
every one of them coplanar with the ground beneath. The board flickered inside an hour.

**The tell is a constant that becomes round.** When a fix moves a number to 0, to the plane, to
flush, to exactly — ask what the old, unround value was keeping apart. Clearances, epsilons and
biases look like sloppiness and are usually the only thing standing between two surfaces.

**The fix**: give the margin a name and an owner (`CellMetrics.SlabLift`), put it in the rule rather
than in each row, and **assert both halves** — that the values agree, *and* that what they agree on
is still clear of whatever the old margin was clearing.

---

## The register

Newest first. Every row: what was reported, what it actually was, and what now stops it.

### 2026-09-18 — The outer slabs were built first and fell (P1)

*"The slab was placed on the top level but then the colonists tried to build the most outer slabs
first which then landed a stone/steel looking tile 1 height below instead of where it was … could be
silently resorting to a failure."*

**The owner's first message, and every word of it was right.** Three sessions went on the *drawing*
of the grey tile below before anybody measured the collapse that put it there. The row below is the
drawing; this is the cause.

**Cause (P1).** The support rule has two halves that disagree about *time*. `AllowsSlab` accepts a
cell held up by slabs merely **ordered** around it (`SupportedByWhatIsPlanned`) — a promise about the
*finished* roof, and the whole of why a roof can be dragged in one gesture. Nothing enforced an
order of construction that honours it, and `BuildWorkGiver` hands out the **nearest** site. So the
far end of a bridge was raised while its supports were still blueprints, stood on nothing, was taken
by the solver, and `SupportSystem.Rubble` left debris in the floored cell below.

**The owner's rule:** rubble is for construction that was *destroyed*, never for construction that
never happened. A slab that cannot stand is not built yet.

**Fix:** `ConstructionGrid.SlabWouldStand` — would it stand *now*, asked of the built world with no
plans in it. `BuildWorkGiver.CanBuild` **defers** the site; `Raise` **keeps** it rather than
cancelling, because unlike the shaft rule beside it this is a race and not a permanent illegality.
`TheDraggedRunStillFinishes_BuiltFromTheWallOutward` guards the over-correction: the gate must defer
and never refuse, or the far half of every dragged roof disappears.

**The lesson is the expensive one.** The reporter described the mechanism correctly in their first
sentence — outer slabs first, nothing built, tile appears below — and three sessions were spent
explaining the *symptom's appearance* instead. **Read the report as a causal claim and test that
claim first.**

### 2026-09-18 — The grey tile was rubble terrain drawn on top of a wood floor (P1, P5, P6)

*"I can't seem to recreate this in a new game — could an old game cause problems?"*

**The fourth answer, and the one that holds.** The owner's own question was the right one. The save
from the photographed session:

```
the-lost-buckets-day-3.odyssey   Floor=Built/Wood 95, Paved/Wood 20   <- no stone on the board
                                 terrain Rubble 8                      <- eight cells
```

Eight rubble cells; eight grey plates in the screenshot. `PlayScene` registers
`Slab(ModuleIds.Terrain("Rubble"), "SM_Env_Ground_Tile_Half_03")`, so rubble **terrain** draws as a
grey street tile — the same art family as the stone slab, which is why three sessions kept
recognising it as one.

**Cause (P1).** "A floor hides what is beneath it" has one owner and a second site that never asks
it. `ChunkMesher.EmitScatter` obeys it — *"a built floor, a stamped deck and a deck plate all
equally hide what is beneath"* — and `SurfaceContributor` does not, so a cell holding both a surface
terrain and a floor slab draws both, coplanar. **A collapse guarantees that cell exists**:
`SupportSystem.Rubble` writes into `FirstFloorAtOrBelow`, which has a floor by definition.

**Why the pane and the picture disagreed, and the pane was right.** `CellDetailContributor` reads
`FloorStuff`; the floor really was wood. The grey on top of it was not a floor and has no
`FloorStuff` to report. Three sessions explained a *floor* — stale mirror, hole one layer down,
stone paving — and the answer was a second thing drawn in the same place.

**Fix:** rubble on a floor is lifted `CellMetrics.SlabLift` on to it rather than hidden. Hiding was
shorter and wrong: rubble refuses to be built on until cleared, so an invisible one is a cell that
rejects orders with nothing on screen to say why. Pinned by
`RubbleLyingOnAFloorIsDrawnAboveTheSlabAndNotInIt`, which meshes the same cell twice — with and
without a floor under the rubble — because an instance matrix carries its prefab's normalisation and
the relief varies with x and z, so nothing else isolates the lift.

**Caught next time by:** asking `Odyssey.SaveProbe` first. It reports terrain as well as floors now,
for exactly this reason.

### 2026-09-18 — The grey tile was stone paving all along (P5, P6) — *wrong, superseded*

> The row above is the real cause. Stone paving is real and two older saves hold it, but the board
> the owner photographed has no stone slab on it at all. Kept because the reasoning below is a clean
> example of P6 and was still not enough.

*"The slab was placed on the top level but then the colonists tried to build the most outer slabs
first which then landed a stone/steel looking tile 1 height below instead of where it was … you can
see it thinks this stone slab is a wood floor as well."*

**This is the third report of the same tile and it overturns the previous two rows**, which is the
row worth reading. The screenshot the last one asked for arrived: grey plates **coplanar with an
unbroken wood deck**, which the "surface one cell down" answer said was impossible.

**Cause:** the grey plates are **stone paving**. Measured by loading the owner's own saves
(`tools/dotnet/Odyssey.SaveProbe`): `timmy-test` holds 11 `Paved`/Stone beside 20 `Paved`/Wood and 31
`Built`/Wood, with stone at (72–75, 57, L11) touching wood at the same z. Every mixed deck in the
folder mixes *materials*, never layers, and the stone cells are always `SlabPaved` — the **Paving**
tool — and never `SlabBuilt`.

**Why three sessions missed it.** The three checks in the previous row are all *true*, and all three
answer one question — *can a wood floor draw as stone?* Not one asks **was it ever wood?** The
contrapositive was the whole answer and was sitting in the same paragraph.

**Three separate faults, none of them the renderer** (`15-building.md`):

- Two palette tiles both mean "floor" — `Paving` (filed under *Structure* **and** *Floors*) and
  `Slab` — and `BuildPalette._lastMaterial` is keyed **per sub-type**, so they remember different
  materials and nothing on either tile says which.
- `InspectModel.DescribeCellAt` titles a floor by `FloorStuff` alone, so paving and a structural
  floor are the same sentence and the word *paving* is never printed.
- Both slab catalogue rows carried `baseAtY: 0`, which is *no* placement rule rather than the one
  its own comment described, so each art landed on its artist's pivot. Measured: the street tile's
  top 25 mm **proud** of the plank deck's — the opposite of what the screenshots looked like.

**Fixed:** `ModuleEntry.topAtY` places a walked-on piece by its highest point, so every slab's top
face lands at one height, pinned by `EveryFloorSlabPutsItsWalkingSurfaceOnTheCellFloor`. The other
two are reported and not fixed: both are design calls. 25 mm will not on its own make a street tile
read as decking.

**And the first fix was wrong, in a way worth its own line (P7).** Levelling the slabs on to the
floor plane *exactly* made the owner's board z-fight within the hour — `FloorCentre` for cell y is
also the top face of the block filling cell y−1, so a slab flush with the plane is coplanar with the
ground paving is laid on. The plank deck's +0.008 was never slop; it was clearance.
`CellMetrics.SlabLift` is that clearance and `topAtY` applies it, so the rule owns it rather than
each row. **The first version of the test would have passed the flicker through**: it asserted the
slabs agreed, and levelling them all on to the ground's own plane satisfies that perfectly. It now
asserts they agree *and* that the shared height clears the ground.

**Caught next time by:** `Odyssey.SaveProbe`. Load the file before arguing about the picture.

**And a trap found on the way out:** `PlayScene.RebuildCatalogue` silently drops the `appearance`
block — 311 atlas swatch rectangles clothing the 61 colonists — so `CharacterSwatches.Classify` must
run after it. The rebuild exits zero and keeps all 138 rows and every prefab reference, so the loss
shows up in nothing but a line count.

### 2026-09-18 — A dragged floor built a ring and left a hole (P1, P4)

*"I specified wooden slabs to be built only on that floor but then it constructed stone/steel floor
on the floor below."*

**Cause:** the slab lift is per cell and conditional. Over a walled room the perimeter cells sit over
walls and lift to the storey above; the interior cells sit over open air and do not. Measured: twelve
cells `None` at L12 and four `NotPermitted` at L11, **from one box**. And the drag's ghosts were
stamped at the box's own layer with no lift at all, so the preview could never have shown it.

**Fix:** `ConstructionGrid.RunLayerFor` owns a run's layer; the order and the preview both ask it.
The highest cell of the run wins — anchoring on the first cell touched would refuse the whole run for
a player who starts the drag in mid-air. The lift is idempotent, so handing lifted cells back to
`Place` is safe, and a test pins that.

**Caught next time by:** `FloorRunTests`, which is about a *run*, and which pins the two-layer fault
itself so the fix cannot be mistaken for a no-op.

### 2026-09-18 — The grey floor that would not deconstruct (P5) — *not a bug*

*"It claims it to be a wood floor for the one that looks like a stone floor"*, then *"I deconstructed
them, wood appeared, the game thought they were gone, and the steel/stone floors were still
visible."*

**Cause:** nothing. Three checks excluded every candidate — the pane and the mesher read one field
(`_grid.FloorStuff`, mirrored on adjacent lines by `CopyCell`); the stuff ids match on both sides
(wood 4, stone 5); both slab arts resolve to real prefabs so the per-cell group fallback never fires.
`StaleFloorProbe` then measured the mirror: module 0 → 134 on build, **134 → 0 on removal**, in the
same refresh. Nothing goes stale.

**So the grey was a different surface one cell down**, seen through the hole the bug above left — and
still there after the floor above it was removed, because it was never the floor being removed.

> **Superseded, 2026-09-18.** The conclusion was wrong: the grey was stone paving on the *same*
> layer. See the row at the top of this register. The three exclusions above all still hold; what
> was missing was the question *was it ever wood?*, and the answer was in the save file.

**Worth keeping** because two separate reports and a deconstruction test all pointed at a renderer
fault that did not exist. The probe is committed; next time this is one command.

### 2026-09-18 — Two legal orders made an illegal ladder (P2, P3)

*"Sometimes the colonists climb up the ladder where there is wall or slab directly above."*

**Cause:** the shaft rule asked the built world. Order the ladder, order the floor above it — each
legal on its own because neither exists yet — and both get built. The prime suspect (the P3
compatibility clause) was **wrong**, and one number killed it: the played meadow generates *no*
ladders and no connectors at all, on three seeds.

**Fix:** `ShaftRulePermits` owns the whole question, sees sites, and is asked again at `Raise`.

**Two defects fell out of the hunt, neither reported:**

- **A shaft could only ever be one storey.** A ladder is `blocking false` so a colonist can stand in
  it, and `SomethingUnderfoot` wants a *blocking* edifice — so the second ladder of a chain was
  refused and `LadderArrivesAt`'s own chain clause was unreachable. The first fix was not enough:
  the connector was gated on `CellGrid.IsWalkable`, which cannot see that *a connector is its own
  floor*, so the chain built and the upper ladder silently had none (`StandsOnAFooting`). And the
  fan-out had to reach **upwards**, or pulling the bottom ladder out leaves the upper one on air.
- **Roofing over a working shaft closed it silently.** Refused now.

**Caught next time by:** `LadderTests` — the blueprint race in both orders, the chain, the chain's
teardown, and the roofed shaft.

### 2026-09-18 — A bed built through a wall (P1)

`Raise` derived the far cell from `_facing[cell]`, called `Clear(cell)`, then read the facing *again*
out of the slot it had just zeroed. Every rotatable thing was recorded facing north.

**It hid because only the drawing was wrong** — the cells were derived before the clear and were
always right, so the footprint guard still worked and no simulation test could see it.

**Three tests had a clear shot and all three missed**: one passed on the difference between two beds'
*cells* rather than their facings; one tested the refusal path, which was never broken; and every
other bed test places facing 0, which is also what a lost facing looks like.

---

## The method, which is the real lesson

**Measure, do not read.** Reading the code has been wrong on every hard bug in this project, and
wrong in a specific way: each part *is* correct, and the fault is in the gap between two of them.
Three sessions read the bed placement path end to end and concluded correctly about every line; ten
lines of throwaway test printing *asked → got* found it on the first run.

The probe is the tool. Write it, run it, delete it — or keep it beside `StoneFloorProbe` and
`StaleFloorProbe` if the question will be asked again.

**Pin the fault, not just the fix.** A test that only asserts the correct outcome passes on a board
where the bug cannot arise. `TheLiftOnItsOwnSendsOneBoxToTwoLayers` asserts the *two layers*, so the
fix cannot be mistaken for a no-op.

**One number can kill a theory.** "The meadow has no connectors at all" ended a hunt that a day of
reading would not have.

**And check the fix is even in the player's build** before hunting a second cause.
