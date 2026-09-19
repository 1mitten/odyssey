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
| Is this cell the foot of a terrace step? | `BankLayout` (render mirror), `TerraceFoot` (cell grid) | allowed on purpose: different data, pinned cell-by-cell by `TerraceFootTests` |

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

### P8 — A per-cell tile's own edge, and the four fixes that are all placement

The board is drawn one instanced piece per cell, so every seam in the game is two pieces meeting on
a line. When something shows along that line, the reflex is to reach for the **placement**: drape it,
level it, lift it, overlap it, stagger it. Every one of those was tried on the floor seam of
2026-09-18 and measured, and not one of them moved it, because the thing being drawn was the piece's
own rim and the rim goes wherever the piece goes.

**The tell is that the artefact tracks the cell pitch and survives the board going flat.** Relief off
and it is still there; the ground taken out from under it and it is still there; the piece grown to
overlap its neighbour by 200 mm and it is still there, on the same pitch.

**Ask what is *at* the line, not where the line is.** A plate has a rim; a rim ends in the plane of
the neighbour's top face; two surfaces at one depth is a tie and a tie is drawn by whoever wins. The
answer was to stop drawing the rim, not to move it — the same answer `ResolveTerrain` had already
written down for water: *"a slab has sides and an underside that water cannot afford to draw."*

**And measure in pixels, because this class of fault is invisible in metres.** Counting pixels much
darker than all four of their neighbours turned four rounds of argument about screenshots into one
number per experiment, and it was that number, not the pictures, that killed the three wrong fixes.

### P9 — A shared buffer whose first use fixes its shape for every use after it

A cache, a scratch list or a property block reused across callers is normally a pure saving. It stops
being one the moment the API remembers something about the *first* call and applies it to the rest.
`MaterialPropertyBlock.SetVectorArray` is exactly that: the length of an array is fixed by the first
set and every later set is **capped to it**, with a warning and no exception. One block reused across
buckets of different sizes therefore serves the first bucket correctly and every larger one with
colours that were never written for it.

**The tell is that the symptom depends on which thing was drawn first, not on the thing that looks
wrong.** The trees repainted themselves; nothing about the trees had changed. It also comes and goes
with a toggle that has no colour in it at all — see-through only decides *whether the shared block is
used*, and that was enough to make a tree-colour bug out of a buffer-reuse bug.

**Ask what the first call taught the object.** An array length, a capacity, a format, a keyword set,
a texture size: anything the object latches. If the answer is "something", the shared thing must be
used at one fixed shape — pad to it — or not be shared.

**And treat a Unity warning in a render path as a bug report.** This one named the property, both
sizes and the exact line, and it had been printing for as long as the feature existed.

---

## The register

Newest first. Every row: what was reported, what it actually was, and what now stops it.

### 2026-09-19 — Every dear step began with the figure standing still (P5)

Found by a smoothness test, never reported, and in the game since any step cost more than 100.

`MovePercent` is a whole percent of the step. A 240-tick hop therefore spends its **first 2.4 ticks
at nought percent**, and every reader gated on `MovePercent > 0` — the pose, the climb phase, the
gait hold — treated the pawn as standing still through them, then caught it up in one frame: **30 mm
against the 10 mm a frame that climb moves**. On a flat cell, which costs exactly 100, it cannot
happen at all, which is why five sessions of watching colonists walk never showed it.

**A new shape worth naming: a threshold that is exact for the common case and wrong for every other
one.** `MovePercent > 0` is not a test for "moving", it is a test for "moving *and* the step is cheap
enough that a percent has ticked over". Ask of any threshold on a published number: *what does the
number do on the case this was not written for?*

**Stopped by** `PawnView.Moving`, which asks the per-mille progress, and by
`HopArcTests.AClimbIsSmoothFrameToFrameAllTheWayUp` — which measures the **variation** between
frames rather than a maximum, because a hitch and a rhythm both pass a maximum comfortably.

### 2026-09-19 — One ramp, two steps, two different prices (P1)

*"The slowness needs to start happening much earlier when entering the beginning of the tile … you
slow down and then you seem to still go slow on the flat so it's out of sync."*

**A seam, not a curve.** The bank spans one cell; a step spans two half-cells. So the drawn ramp was
split down the middle of the foot cell between two steps priced for different things — the walk in at
flat-grass price (drawn at 1.9 m/s, faster than walking) and the hop out at 240 (0.62 m/s, and it
kept charging that across the flat top). Both halves of the report are that one seam, seen from
either side.

**It is P1 wearing geometry.** One rule — what a slope costs — with two owners, and here the two
owners are two *steps* rather than two files. The question to ask of any cost: **does the thing it
prices line up with the thing that is drawn?** A step is charged between cell centres; art is drawn
between cell edges; those are half a cell apart, and anything whose appearance spans a cell will be
charged by two steps that know nothing of each other.

**Stopped by** giving the cell its own cost (`NaturalContent.CostClassSlope`, worth
`MoveCost.SlopeExtra` = `JumpUp − Orthogonal`, stated as a subtraction so the pair cannot drift) and
by `PawnPose.StepPace`, which spends each step's time where its climbing is. `TerraceSlopeCostTests`
asserts the two prices are equal; `HopArcTests.TheFlatsAreWalkedAndTheRampIsClimbed` asserts the ramp
is one speed from bottom to top with a walk either side.

**And a caution about goldens.** None of the three moved, which was luck rather than inertness: the
golden windows are a flat meadow, a start clearing chosen for being flat, and a city of pavement.
A cost change that moves no hash wants a direct test, not a shrug.

### 2026-09-18 — Every fixture had a bank in it, so nobody saw the 1.5 m teleport

Found while measuring a new climb, not reported by anybody.

**A hop up a step with no bank — against rock, inside a working, under a roof — snapped the figure
1.51 m in a single frame**, and had done since hops were first drawn. The cause is the ground clamp:
the cell a walker is *over* switches at the midpoint of a step, so with no ramp the ground under it
is flat for the first half and a whole layer higher for the second. Any height curve timed across
the whole step reached half its height and was then clamped the rest of the way at once. The mirror
of it, on a sheer drop, was 657 mm.

**A new shape worth naming: the fixture chose the case.** `BankFootingTests` measures exactly this,
five ways across a terrace, four hundred samples a step — and every one of its worlds is a *terrace*,
which by construction has a bank in it. The bank's ramp made the ground continuous and the fault
invisible. It is the same shape as `GroundRelief.Reset()` hiding the relief snap two days earlier:
a thorough test suite whose fixtures all share one convenient property.

**Ask of any fixture: what does it always have that the game does not?**

**Stopped by** `HopArcTests.ASheerFaceIsClimbedSmoothlyRatherThanInStrides` and
`ADropDownASheerFaceIsAsFastAsTheGeometryAllows`, which build a step of bare rock and assert the
fixture has no bank before measuring anything. The climb now completes by the midpoint and the fall
starts there.

### 2026-09-18 — A stride could not be drawn because progress was published as a whole percent (P5)

A climb drawn in strides stuttered: 134 mm in one frame where 50 is the budget.

**The cause was a layer away from the motion.** `PawnView.MovePercent` is a whole percent, and the
sub-tick term cannot smooth it — at 60 frames and 60 ticks a second there is about one frame to a
tick. A flat cell costs 100, so a percent a tick is exact and the quantisation has never been
visible; a 240-tick hop advances a whole point every 2.4 ticks, so the figure stands still for two
frames and then jumps a hundredth of the step. On the flat that jump is 25 mm. Concentrated into a
stride it was 134.

**Two wrong probes before the right one**, both worth remembering. The first sampled only at
`tickAlpha = 0`, so the two arms of the comparison were identical and the sub-tick term looked
innocent. The second sampled per percent — the very granularity under suspicion — so it reported the
quantisation as the motion.

**Stopped by** `PawnView.MovePerMille`, ten times the resolution, published beside the percent rather
than instead of it, and by `BankFootingTests.WorstJump` sampling per mille: an instrument coarser
than what it measures reports quantisation as a teleport.

### 2026-09-18 — Trees grew inside the hillside at the top of every terrace step

*"The flat side of the terrain where the height changes … things generate in those tiles … Trees
shouldn't be generated in those spots because they get clipped by this façaded terrain."*

**A new shape, and the one to watch for next: a façade with nothing behind it, in a cell the
simulation is free to fill.** A bank — the ramp drawn up a terrace step — fills the empty cell at
the foot of the step from floor to rim, and `Odyssey.Sim` does not know it exists, by design. Every
other façade in this project is drawn *on* ground that stays empty (relief, tufts, chips, banks of
the surrounding land); this one occupies a cell that worldgen scatters into and colonists walk
through. Ask of any new façade: **can the simulation put something where this is drawn?**

**It read as an art fault because the commonest case is handled.** `PawnPose` lifts a walking figure
onto the bank's surface, so colonists look right; only things that are never lifted — a generated
tree, a body lying down — are swallowed.

**Stopped by** `TerraceFoot.IsFoot`, the simulation's copy of the rule, which `TreePass` asks before
placing a tree. Two owners (P1) and allowed, because worldgen has no render mirror to ask — so
`TerraceFootTests` requires the two to agree in every cell of seven boards, four of which are cases
where the right answer is *no bank*. `docs/design/22-terrace-steps.md` §4 records what is still
unguarded: a colonist lying down in one, and an item dropped in one.

### 2026-09-18 — Typing a save name drove the game behind the dialog

*"When you type in during a save game the in game controls still work and can cause confusion."*
Typing `sss` panned the camera three cells south; a digit changed the game speed; a `c` armed the
Cancel tool behind the modal.

**A new shape, and worth naming: a gate in one input system cannot govern another that never sees
the event.** Every in-game key is *polled* out of `Keyboard.current` in six components' `Update`;
the text field is a UI Toolkit control that only ever sees what the panel routes to it. Focus is
real and does its job — it just has no bearing on a poll. Two systems, one keyboard, neither aware
of the other.

**It survived because it was half-fixed already.** The modal's scrim takes the *pointer*, so the
dialog behaved like a modal in the one dimension anybody checks, and the keyboard half looked like
it must be handled too.

**Stopped by** `HotkeyDirector.GameKeysLive`, which every poller now asks instead of keeping its own
copy of the guard (P: one rule with six owners — the second reason to sit a frame out would have had
to be written six times). The holder is a **token, not a flag**: focus moves as a blur and a focus,
in no promised order, so a bool is cleared by the field being *left* after the field being *entered*
set it. `HotkeyDirectorTests` holds both that and the mirror failure — a gate stuck *shut* is a game
that has silently stopped answering its keys.

### 2026-09-18 — Selecting a colonist repainted the whole wood (P9, P5)

*"A bug with the trees occurs when See through to selection/occlusion is set on. When I select a
colonist the trees change colours all around, but when I come away or switch the setting off it goes
back to the original colour."* With the console warning: `Property (_LeafDeepColour) exceeds previous
array size (31 vs 25). Cap to previous size.` from `ChunkRenderer.SolidProps`.

**It is one property block reused at two sizes.** A tree carries its four colours as per-instance
data rather than as a tint, so a bucket's colours ride in a `MaterialPropertyBlock` beside its
matrices. With see-through off, every bucket draws from **its own** block, written once at its own
size — right, always. With it on, a bucket standing in a sight line is partitioned into a ghosted
half and a solid half, and the solid half's colours are gathered into **one block shared by every
bucket on the board**. That block's array length was fixed by the first partitioned bucket of the
session, so from then on any bucket with more solid trees than that had its colours capped: the
trees past the cap drew whatever was left in the array. Move the selection and a different bucket
goes first, so the wood repaints itself. Nothing about trees, colour or the palette was involved.

**Fixed:** `ChunkRenderer.WritePadded` pads every write to a shared block out to
`MaxInstancesPerCall`, so the array is the same length every time and is never capped. The padding is
never read — a draw of *n* instances indexes the first *n* entries. It is the fix
`TreeMaterials.UniformProps` already used for the neighbouring reason (a block's array is indexed
from zero by every draw call, not from the instance offset). The per-bucket blocks of
`ChunkRenderer.PropsOf` are deliberately **not** padded: each is written once at its own size, they
are not shared, and every tree on the board would pay the wider upload every frame.

**Caught next time by:** `TreeColourBlockTests.ABiggerBucketAfterASmallerOneKeepsItsOwnColours`,
which writes 25 then 31 through one block — the reported sizes — and asserts the array length does
not move as well as that the colours come back. Asserting only the colours would pass on a block
that had simply not been reused yet.

### 2026-09-18 — Every floor seam drew a dotted line, and it was the tile's own rim (P8)

*"You can see these slabs leave small artifacts/lines or gaps that don't even up … you can notice
this when you look at the ground from certain angles — you see a slight issue with not being fully
flush."* Two screenshots: a wood slab field on the meadow at night, and a roof deck.

**It is the tile's rim winning a depth tie.** A floor is drawn one instanced plate per cell, and the
slab art is a plain box — measured, 2.5000 m across, its top face flat to the micrometre and the
full width of the piece. So two tiles meet exactly, and the top edge of one tile's **rim** lies
exactly in the plane of its neighbour's top face. Equal depth, so the rasteriser keeps whichever
fragment it likes; a rim takes almost no light under a 72° sun, so where it wins it draws a dot of
wood at four tenths the brightness of the deck. That is a dotted line along every seam and a dotted
grid over every floor, worst at a low camera pitch.

**Fixed:** `CellMetrics.FloorTile` draws a floor as a **sheet** rather than a plate — the rim
squashed to a tenth of a millimetre about the walking surface, so it is degenerate on screen and
generates no fragments to win with — and grows it 3 mm past its own cell
(`CellMetrics.FloorKnit`) so two neighbours overlap rather than share an edge. Both the built floor
(`ChunkMesher.EmitFloor`) and paving (`SurfaceContributor`) go through the one matrix. Measured on
the meadow, counting pixels much darker than all four of their neighbours: at the play camera's 48°
**470 → 16**, at a grazing 25° **884 → 35**, and on a floating deck at 14 m **73 → 3**.

**What it costs, and it is real:** a floor over open air loses its 101 mm of drawn thickness and its
lip reads as paper seen edge-on. A floor laid on the ground had 93 of those millimetres buried in
the block beneath it, so nothing changes there. The fix that keeps the lip is to draw a floor's edge
as a **fascia on the face**, the way a wall is already drawn on faces rather than as a cell; that
wants a panel module of its own and is not done.

**Caught next time by:** `ChunkMesherTests.AFloorTileIsDrawnAsASheetAtTheHeightAColonistWalksOn` and
`.TwoNeighbouringFloorTilesOverlap`. The first asserts *both* halves on purpose (see P7): flat, and
still a clearance above the cell floor plane, because squashing about zero instead is just as flat
and drops every floor back on to the ground it z-fights.

**Reproduced by `SlabFlushProbe`** (`scripts/unity.sh shot Odyssey.EditorTools.SlabFlushProbe.Run`)
— a wood floor laid on the meadow, shot at 48° and 25°, relief on and off. `SlabTopFaceProbe`
measures the art itself, turning Read/Write on for the one model and back off again.

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

---

## Runbook: a tile that looks wrong

**Written 2026-09-18, after one grey tile cost four rounds.** Three of those rounds produced
confident wrong answers reasoned from screenshots — a stale render mirror, a hole one layer down,
stone paving — while the file that answered it sat on the same disk throughout. This is the order to
work in when the next "that tile looks wrong" arrives.

**Do not start by reading the renderer.** That is what was done three times.

### 1. Get the save, not the screenshot

Ask for a save, by name. A screenshot is an argument about pixels; a save is the state.

```
dotnet run --project tools/dotnet/Odyssey.SaveProbe                  # the usual folder
dotnet run --project tools/dotnet/Odyssey.SaveProbe -- "<path>"      # one file
```

No Unity, about a second, and it prints every **floor** (kind and material), every **item** (by kind
and layer) and every **terrain** on the board. Saves live at
`%USERPROFILE%\AppData\LocalLow\Unity Technologies\com_unity_template_urp-blank\Saves`.

### 2. Ask what is *in* the cell before asking why it is *drawn* that way

A cell can hold more than one drawable thing at once, and the report will name only the one the
player recognises. The grey tile was **a wood floor and rubble terrain in the same cell**: the pane
said "Wood floor" and was telling the truth, and the grey on top of it was not a floor at all and had
no material to report.

| The report says | Check, in this order |
|---|---|
| a floor is the wrong material | `Floor=` and `Stuff=` for that cell — **and** `terrain` for the same cell |
| something is at the wrong height | both things drawn there, before any placement code |
| something appears/disappears/flickers | two things at one height: coplanar geometry, not a shader |
| it only happens in an old game | what the old game has that a new one does not — `terrain` and items are where that lives |

### 3. Turn the clean exclusions around

"A wood floor cannot draw as stone" is also "**a cell drawing as stone is not a wood floor**". Three
sessions proved the first and never read the second. When every mechanism is excluded and the
symptom persists, the thing is not what the report calls it (**P6**).

### 4. Read the reporter's causal claim and test *that* first

The owner's first sentence was *"the colonists tried to build the most outer slabs first which then
landed a stone/steel looking tile 1 height below"*. That is the mechanism, exactly, and it was
testable headlessly in the fast tier from the first minute. Four rounds went on the tile's
appearance instead.

### 5. Known-good facts, so they are not re-derived

- **Grey cross-hatched plate** = `SM_Env_Ground_Tile_Half_01/02/03`, used by `slab.stone` **and** by
  the Pavement / CrackedPavement / **Rubble** terrains. Seeing one does not mean a stone floor.
- **Rubble comes from a collapse** (`SupportSystem.Rubble`) and lands in a cell that **has a floor by
  definition**, so floor-plus-terrain in one cell is normal, not corruption.
- **Rubble is clearable** — `clearable: true`, 90 ticks, cleared with the **Mine** order. Rubble
  already in a save stays until somebody clears it; a fix stops new ones and does not tidy old ones.
- **The pane titles a floor by `FloorStuff` alone** — it never says "paving", and it says nothing at
  all about anything drawn on top of the floor.
- **Every slab's top face is `CellMetrics.SlabLift` above the cell floor plane.** Two things at the
  same height z-fight; the clearance is why. Do not "tidy" it to zero.
- **A floor is drawn as a sheet, not as a plate** (`CellMetrics.FloorTile`, `FloorSheet`,
  `FloorKnit`). The plate's rim tied with its neighbour's top face and dotted every seam in the
  colony. Do not "restore" the thickness without reading P8 — and if a floor's lip over open air
  needs to look solid, that is a fascia on the face, not a thicker plate.
- **The slab art itself is exact** — 2.5000 m across, flat on top to the micrometre, the top face
  the full width of the piece, 40 vertices. Measured 2026-09-18 by `SlabTopFaceProbe`. A seam
  artefact is not the art's size, shape or flatness.

## A symptom that names a missing feature usually names a missing half of one

**2026-09-19, the carried load.** "A colonist picks something up and it disappears" reads as
*carrying is not built*. Two of its three beats were: the stoop is a timed toil, the grasp lands
in the middle of the drawn crouch, and the stow was already reported at a stockpile and at a build
site. The one missing piece was that `ColonyItems.PickUp` delists the item, so it leaves the view
feed. Had the symptom been believed, the fix would have rewritten `LiftToil` — and `LiftTicks`
moved all three goldens the day it landed.

- **The check:** before building what a report asks for, grep for the half that already exists. A
  one-line grep is cheaper than a wasted session, and this is the third time that sentence has
  been written in this repo.
- **Sibling of "a rule that asks the built world and misses the order":** both are cases of the
  observable state hiding work that has already happened.

## A pose solved to a point is not more robust than one authored as angles

**2026-09-19, the carry stance.** The rule in `13-gestures.md` §3 — solve when the figure must
meet something whose position we know — is about *reach*, and reading it as "solving is always
safer across rigs" gets the carry backwards. Arm length varies across the 61 rigs by more than the
cradle does, so a cradle computed from hip height and solved to puts a short-armed colonist at
full stretch and a long-armed one folded against its chest: same point, two postures. Authored
angles give the same *posture* and let the point fall where each rig's arms are.

- **The check:** ask which of the two a viewer actually reads. For a reach it is the point; for a
  stance it is the posture. Solve only the axis where an authored angle fails outright rather than
  merely looks wrong — here, a load inside the colonist's own chest.

## Two assemblies that cannot see each other agree by spelling and nothing else

**Standing, tightened 2026-09-19.** `Odyssey.Hud` may not reference `Odyssey.Sim`, so a pawn
aspect's name is a string literal on each side. Nothing links them: change one and the feature
silently stops working, with no compile error and no failing test unless one was written for it.

- **The check:** every aspect key gets a test on **both** sides — the Sim side asserting
  `SomeAspects.Key == AspectKey.Of("the.literal")`, the Hud side asserting its own constant equals
  the same literal. `ColonistNames.RollSeedAspect` established the pattern; `CarryAspects` follows
  it. A key with only one of the two tests is a key with no guarantee.

## A pose that is additive over a clip cannot be a stance

**2026-09-19, the carry arms.** Every pose in `PawnFigureDirector` adds a world-space `Pitch` to
whatever the walk clip put on the bone, which is right for a gesture laid over a gait. A carry is
not a gesture: it is held for as long as a state holds, so added to a swinging arm it is a scoop
that swings — and because the load follows the palms, the load swung with it. The owner reported
it as being about the load.

- **The check:** ask whether the pose is an *event* or a *state*. A state has to take the bones
  off the clip first, back to a rest read off that rig at bind time, because sixty-one rigs have
  sixty-one bind poses and a constant would be wrong on sixty of them.
- **Write the rest, never blend to it.** `ApplyWorkPose` runs twice a frame and only one pass
  starts from a freshly evaluated graph, so easing *towards* a rest integrates instead of
  recomputing. Assigning a constant is the identity on the second pass; put the ease in the
  angles.

## A remembered value is only remembered for the path that writes it

**2026-09-19, the load that would not turn.** A carried prop was drawn at
`ChunkRenderer.FacingOf(pawnId)`, which looks like "which way is this colonist facing" and is
actually "the last heading this loop drew a **stand-in** at". The same loop `continue`s past every
pawn that has a live animated figure, so it never writes an entry for one — and every load on
every real colonist was drawn at a yaw of exactly nought. A log pointed north for ever.

- **The check:** before reading a cache, find the write. If the writer skips the cases the reader
  cares about, the reader gets the default and the default is plausible — nought is a real yaw,
  so nothing looks broken until something asymmetric (a log) is held in it.
- **It repeated one layer down.** `ItemHeap` lays its sunflower out on the world axes, so the
  armful had to be turned about the cradle as a cluster; rotating each rock in place would have
  kept the shape and left the shape pointing north. The same fault twice in one feature.

## An instant transfer drawn literally is a teleport

**2026-09-19, the pickup and the drop.** The simulation moves a thing between a cell and a pair of
hands in one tick, because there is nothing sensible in between. Drawn literally that is a jump of
about a third of a metre in no time, at both ends — and the design draft asserted the pickup was
continuous "for free" because the hands are at the floor on the grasp tick. True vertically, and
it misses the lateral gap between the middle of a cell and a pair of palms. The drop was worse and
the draft did not consider it at all.

- **The check:** when presentation shows a state the simulation changes instantaneously, ask what
  the two endpoints are *in world space*, not whether the tick is right. A continuous quantity on
  one axis says nothing about the others.
- **Matching the two halves needs an identity, not a description.** The falling item is drawn from
  a different list by code that never saw the hands; the def and the stack cannot pick it out of a
  stockpile of the same commodity. Publish the id.

## A shader found at runtime is a shader the build throws away

**2026-09-19, the empty world.** Owner: *"there is no terrain — no graphics, terrain etc, apart
from characters."* Only in a player build; the editor was perfect.

Everything in the world is drawn with `Graphics.RenderMeshInstanced` using materials created **at
runtime** from `Shader.Find(...)` with `enableInstancing = true` — `ChunkRenderer`,
`MaterialCache`, `ModuleLibrary`, `TreeMaterials`, `ColonistMaterials`. A runtime-created material
is not an asset, so the build's shader collector never sees it and Unity ships only what assets
reference. Measured in the built player: `Odyssey/Water`, `Odyssey/Tree`, `Odyssey/Character`,
`Odyssey/Outline` and `Odyssey/GradientSky` were **absent altogether**; `Universal Render
Pipeline/Lit` was present only in the non-instanced variants Synty's prefab materials use.

Characters were the one visible thing because they alone are GameObjects wearing real material
assets — and they drew in the pack's own colours, because the shader that recolours them had gone
with the rest. *"Everything is missing except the one thing that is a GameObject"* is the
signature of this fault.

- **The check:** every `Shader.Find` name belongs in `ShaderInclusion.Required`, which forces the
  shader and all its variants into the build. `ShaderInclusionTests` derives the list from the
  source, so the two cannot drift — it found two names the hand-written list had missed on its
  very first run.
- **Measured cost of always-including URP/Lit: none.** 385 MB and 11 s before and after. The fear
  that all-variants would be ruinous was worth testing rather than designing around.
- **No test in this repository could have caught it.** The editor has every shader and every
  variant, always, and both tiers run in the editor's own domain. Only a player build fails on
  this, and until that day nothing had ever made one.

## One rule, two places, and only one of them knew — the null guard edition

**2026-09-19, `SettingsPresenter`.** A `NullReferenceException` every frame in a player build,
never in the editor. `Attach` dereferenced `_bootstrap.Directors.Overlays` while its own comment
three lines above explains that attaching deliberately does **not** wait for `Directors` to
exist. `OnDestroy` had guarded that exact dereference since it was written.

- **The tell was already in the file**: the same expression guarded in one method and bare in
  another. When you find that, the bare one is the bug, not the guarded one.
- **The build had been saying so**: `CS8602: Dereference of a possibly null reference` at that
  line, in every build log, unread.
- The fix is not a guard but a second waiter — `HookDeveloperOverlay`, which waits on `Directors`
  where `Attach` waits on `Preferences` — because the two genuinely wait on different things.

## The content pack does not exist in a player, and the code said so years before it mattered

**2026-09-19, the empty world — the real cause.** After a shader fix that was a genuine but
*different* fault, the owner reported again: *"No graphics came up in the build… apart from the
characters."*

`ContentPack.FindRoot` locates the Defs by walking up for a directory holding both `Assets` and
`ProjectSettings`. A built player has neither, so it throws, world generation never runs, and the
scene is empty but for the figures the start flow had already made. Its own remarks predicted
this in full: *"A built player has neither directory and would land in the throw below, which is
deliberate. Nothing in CI or scripts/ builds a player, so shipping the pack is not solved here
rather than solved wrongly here."* `UseRoot` exists for exactly this and had never been called.

- **The check:** anything the game reads from a path under `Assets/` at runtime is absent from a
  player. `ContentPackBuild` stages the pack into `StreamingAssets` for the build and removes it
  after, so the repository keeps one copy; the composition root calls `UseRoot` outside the
  editor.
- **A deliberate limitation outlives the sentence that made it deliberate.** "Nothing builds a
  player" was true when written and stopped being true the hour a build command was added. When
  you write *"X is not solved because nobody does Y"*, the note has to be found by whoever first
  does Y — a grep for `StreamingAssets` found it, but only after two wrong answers.

## A clean log from a program sitting on its main menu proves nothing

**Same day, and it cost two wrong diagnoses.** Twice I ran the player from a terminal, saw a
clean log, and reported a fix. Both times the player had stopped at the main screen, where
nothing loads the content pack, nothing generates a world and nothing draws terrain — the entire
subsystem under suspicion had not run.

- **The check:** before believing a smoke test, confirm the code under suspicion actually
  executed. The log that mattered says `world 120x120x16 seed 1 generated in 61 ms`; the two that
  did not say anything of the sort, and their silence read as success.
- **The fix is to make it reachable**: `-odyssey-newgame` boots a player straight into a colony,
  so a build can be smoke-tested without a person clicking. A check nobody can run from a
  terminal is a check that will be skipped.
