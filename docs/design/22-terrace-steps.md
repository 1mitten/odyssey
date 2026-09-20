# 22 — Terrace steps, and what may stand at the foot of one

*Owner report, 2026-09-18:* "The flat side of the terrain where the height changes, we created a
façade of terrain but the problem is that things generate in those tiles and we should look to guard
it. Trees shouldn't be generated in those spots because they get clipped by this façaded terrain.
Also I've seen colonists sleep in this square … Maybe certain things in future can spawn in these
spots — but for now — guard it from generation on these tiles."

## 1. What the façade is

The board is terraced: the surface steps up and down in whole layers, and a layer is 3.0 m
(ADR 0002, irreversible). The simulation lets a colonist hop straight up one of those risers —
`MoveCost.JumpUp` — so a bare 3 m wall would show a cliff where the game has a path. The **bank** is
the picture of that path: a wedge of hillside drawn spilling down from the ground above into the
empty cell at the **foot** of the step, in three shapes — straight, inside corner, outside corner —
so that a run of them around a terrace is one continuous surface (`docs/design/06-rendering-and-camera.md`,
`BankMesh`, `BankLayout`).

It is a façade in the strict sense. Nothing in `Odyssey.Sim` knows a bank exists: it is not pathable,
not selectable, not saved and not hashed, exactly like ground relief and grass tufts. **That is the
whole of the problem.** The bank fills its cell from the floor to the rim of the step above, so
anything the simulation puts in that cell is inside what looks like solid hillside. A walking
colonist is fine — `PawnPose` lifts a figure onto the bank's surface, which is why the ramp reads as
somewhere you can walk up — but anything that does not get lifted is swallowed.

## 2. The rule

**A terrace foot is an empty cell, standing on something, under open sky, on ground nobody has cut,
with natural soil exactly one layer higher against one of its eight sides.**

Eight, not four: a bank stands against an orthogonal step (straight and inside-corner pieces) or,
where there is none, against a diagonal one — the outside-corner piece that wraps a convex corner.
That diagonal cell is the one a hand-written guard would miss, and it is why the rule is written
down once rather than open-coded at each caller.

Each clause earns its place, and each is a case where **no** bank is drawn and therefore nothing
should be guarded:

| Clause | Why | What it excludes |
|---|---|---|
| exactly one layer higher | the hop is one layer | a two-layer riser is a cliff, not a step |
| natural soil | earth spills, stone does not | a rock outcrop, a sheer cut rock face |
| the step's top is open | otherwise it is a tunnel wall | a wall inside a mine |
| under open sky | a bank is an outdoor thing | ground under a slab |
| uncut floor, uncut step | nothing spills into a hole the colony dug | a quarry, a bench, a shaft |

## 3. Two owners, held together by a test

The rule is stated twice, deliberately:

- `BankLayout.At(model, x, z, y).Exists` — presentation, over the render mirror. Owns the *picture*:
  which of the three shapes, which bearing, what it is made of.
- `TerraceFoot.IsFoot(grid, x, z, y)` — `Odyssey.Sim.Worldgen`, over the cell grid. Owns the
  *question*, for anything in the simulation that has to keep out of a bank's way.

They could not be one function: worldgen has a `CellGrid` and no render model, presentation has a
render model and no grid. This project has been bitten by a rule with two owners often enough to
have a pattern for it (`docs/bug-patterns.md`), so the two are not trusted to agree — they are
**required** to. `TerraceFootTests` (Unity tier) walks every cell of seven boards and fails if the
two answers ever differ: a one-layer step, flat ground, a two-layer riser, a rock face, a plateau
corner with both kinds of corner in it, a notch, a quarry, and ground under a roof. Change one copy
and that test names the other.

One list moved as part of this. "Which terrains are earth" was `GroundLook.IsEarth`, a presentation
judgement about how ground draws; it is now `NaturalContent.IsEarth` and `GroundLook` calls it,
because a second copy would have been wrong the first time a soil was added, and the symptom would
have been a tree standing in a bank.

## 4. What is guarded, and what is not

**Guarded: trees, at generation.** `TreePass` refuses a tree whose cell is a terrace foot and counts
the refusals in `NaturalGenReport.TreesRefusedOnTerraceSteps`. Measured on the played board
(120 × 120 × 16, wooded):

| Seed | Trees standing | Refused on steps |
|---|---|---|
| 1 | 1,489 | 122 |
| 7 | 1,408 | 118 |
| 42 | 1,612 | 122 |

About one would-be tree in thirteen, all of them along terrace edges. The density roll is still
drawn for every column whether or not the column can hold a tree, so the guard **thins** the wood
along steps and does not reshuffle it: everywhere else the same trees stand as before.

**Not guarded: walking.** The cell is walkable and stays walkable. It is the take-off cell for the
hop, and the bank is drawn there precisely to make that hop legible. Nothing about this belongs in
`NavGraph`.

**Guarded since 2026-09-20: sleeping, and building a bed. See §4a below — the owner's answer to
the choice this paragraph poses was "both". The paragraph stands as written because it is the record
of what was known when it was, including a forecast about the state hash that turned out to be
wrong.**

**Not guarded when this was written: sleeping.** The owner has seen a colonist sleep at the foot of a step and vanish
into the bank. A standing figure is lifted onto the bank's surface; a body lying down is not, and it
spans the cell the ramp rises across. Two candidate fixes, neither taken here:

1. *A tired colonist with no bed does not lie down in a terrace foot* — one clause in
   `JobSystem.TrySleep`, choosing a neighbouring cell instead. Cheap, and it does not touch the
   collapse-at-zero-rest rule (WS3: a body that has run out goes down where it stands, bank or no
   bank, and that is deliberate).
2. *A bed cannot be built in a terrace foot* — a placement refusal. Permanent rather than transient,
   but it takes a cell away from the player for a reason the player cannot see.

Both change the state hash, so both want re-baked goldens and a decision rather than a guess. The
predicate is in place for whichever is chosen.

### 4a. Both guards, taken (2026-09-20)

§4 above left sleeping unguarded and named two candidate fixes, saying *"both change the state hash,
so both want re-baked goldens and a decision rather than a guess."* The owner's decision was **both,
in that order**. This is what they came to.

#### 1. A tired colonist steps out of a bank to lie down

`CriticalNeedsThinkNode.GroundSpot` — consulted only on the branch where she has no bed at all. If
the cell she is standing in is a terrace foot, she is sent to the first of its eight neighbours that
is not one, is reachable, is unreserved and has no bed standing in it. Otherwise, and everywhere
else on the board, the answer is `-1` and she lies down exactly where she stands as she always has.

Three things it deliberately does not do.

- **It never touches the collapse.** Rest that reaches nought drops a colonist where she is, bank or
  no bank (WS3, design 17 §4c). That is the control that stops "go somewhere better" becoming "never
  sleep rough", and the guard is reached only when she is merely tired and has somewhere to walk.
- **It gives up rather than keeps her awake.** Where all eight neighbours are banks or taken it
  returns `-1` and she lies in the hillside as before. A colonist who cannot sleep is a worse bug
  than one who sleeps somewhere that looks wrong.
- **It does not walk her into a bed.** She is sleeping rough; arriving on a free mattress would hand
  her its rest rate and, through `TryClaimForSleeper`, its ownership.

**And it needed one thing fixed on the way.** `SleptOnGround` was added when `Job.TargetCell < 0`,
which had been a fair statement of "she has no bed" only while a colonist with no bed was never
given anywhere to walk to. It reads the cell she is actually lying in now —
`ColonyItems.HasBed(pawn.Cell)` — which is the statement `NeedsSystem.RestEffectiveness` has always
made about the rate she recovers at. The two could not disagree before and cannot now; the binary
search they both use lives in `ColonyItems` instead of being written out twice.

#### 2. A bed cannot be built into one

`BuildingDef.refusedInTerraceFoot`, set on the bed, checked in `ConstructionGrid.Allows`. A field
rather than a test for the bed by name, because it is the same fact about shape that
`needsClearCell` already states, pointed at the hillside instead of at a stack of meals: a wall
fills its own cell and stands out of the ramp, a bed is broad and low and open and is buried by it.

**The cell stays walkable and stays buildable for everything that fills it.** It is the take-off
cell for the hop and the whole reason a bank is drawn there is to make that hop legible; a rule that
took it away from the player entirely would be a worse trade than the bug.

#### What this turned up on the way

**The far half of a bed was being checked as though it were a wall.** `Place` validated the second
cell with `Allows(second)` — the one-argument overload, which answers on behalf of
`BuildingHandle.Wall`. So *every* rule that depends on what is being built applied to a bed's head
cell and silently skipped its foot, and the bank rule would have skipped it too. Corrected to
`Allows(second, building)`, which immediately refused an order that had always been allowed: a bed
whose far half lands on a stack of logs, which is the exact thing `needsClearCell` was added for
(owner, 2026-09-18, *"you can see some meals poking through the bed, this is invalid"*) and which
had never applied to half of the bed. `BedTests.ABedsSecondCellMustBeAbleToTakeItToo` caught it by
failing, having previously asserted the two halves agreed while asking each a different question.

#### The hash did not move, and the forecast was wrong

§4 predicted both changes would move the state hash. **Neither did** — the whole fast tier, all six
golden values included, passed untouched at the first run.

The forecast was not silly. One of the three golden boards is the wooded one and it is covered in
terraces. But a colonist reaches the sleeping guard only when she has no bed at all, and the golden
colonies have beds; in ten thousand ticks not one of them ever tried to sleep rough at a step. The
placement guard is reached only by an order, and nothing in a golden run orders a bed.

**A change that shifts no hash is either inert or untested**, and the two are told apart by
asserting the rule directly rather than inferring it from a colony that never met one —
`TerraceSleepTests`, on the same hand-built terrace `TerraceSlopeCostTests` prices its hop on, for
exactly the same reason.

## 4b. Crossing a step: the price and the motion

*Owner, 2026-09-18, after the first look in play:* "Would it be possible to make the terrace step, if
going down the terrace step, you go a bit faster and if you going up, you go a bit slower … ensure
the animation/motion adjusts accordingly." Then, having watched one: *"The colonists looked too fast
going up definitely — I saw that … should be much slower."*

The asymmetry already existed. What did not exist was any reason to believe it, and the climb was
wrong in the other direction from the one the code claimed.

### The measurement that decided it

A hop is **drawn** along the slope from one cell centre to the next. A cell is 2.5 m across and a
layer is 3.0 m (ADR 0002), so that path is **3.91 m**. Cost is duration — a pawn retires one unit a
tick at 60 ticks a second — so:

| | Cost | Duration | Drawn along the slope |
|---|---|---|---|
| Walking a flat cell | 100 | 1.67 s | 1.50 m/s |
| Up a step, **before** | 135 | 2.25 s | **1.74 m/s** |
| Up a step, **now** | **240** | **4.00 s** | **0.98 m/s** |
| Down a step | 50 | 0.83 s | 4.71 m/s |

A colonist climbing a terrace was drawn moving **16% faster than one strolling beside it**. That is
what the owner saw, and no amount of pose work hides it.

### Why 240, and not a number somebody liked

Two bounds, and the choice sits between them:

- **Floor, 156.** Below that the climb is drawn faster than a walk. `HopArcTests.AClimbIsNeverDrawnFasterThanAWalk`
  is that bound, stated as an assertion so the fault cannot come back.
- **Ceiling, 290** — `MoveCost.StairUp`. Past it a colonist walks to a stair rather than hopping one
  block, and a hop has to stay the cheapest way up one block or a terraced board stops being
  crossable ground.

240 is 0.98 m/s along the slope, about two thirds of a walking pace: a visible labour, and still 11%
quicker than the 270 that read as **stuck** when this constant was last retuned (2026-09-16). The
difference this time is that the motion carries the duration.

### The motion

`HopArc` owns the shape; `PawnPose` places it; nothing is simulated.

**The two directions are not the same kind of thing.** Going up there is a ramp underfoot the whole
way, so the height is a function of *the ground under the walker*. Going down there is nothing
underfoot past the edge, so it is a function of *time*.

- **Up is nothing at all: the figure is drawn on the ramp, sampled where it stands.** Two
  inventions were tried and both were reported. A solved parabola that cleared the lip by 0.35 m —
  *"when going up hill it looks like they jump a bit and not flat with the terrain, which they should
  be"*. Then strides, a hold-and-push rhythm up the treads — *"it jolts and jitters the colonists at
  certain points; smoother is preferred and predictable"* (2026-09-19). Both were answers to a
  question the board had already answered: `BankMesh.HeightAt` is a plane from the lower floor to the
  upper rim, so there is a walkable surface the whole way, and the right height for a climbing figure
  is **that surface**. Nothing computes a climb any more.
- **Reading the surface cannot disagree with the surface**, which a model of it can. The three
  heights `StepPace` is built from are exact on a straight bank and wrong at a corner, where the
  surface is two planes (`HeightAt` is a max or a min there). The clamp used to arbitrate between
  the two; now there is only one.
- **A sheer face gets a plain climb, and fixing it found a fault that predates all of this.** A
  bank is refused against rock, inside a working and under a roof. There the ground under the walker
  is flat for the first half of the step and then jumps a whole layer at the midpoint, because that
  is when the cell it is over changes — so a climb timed across the whole step reached half its
  height and was then **snapped 1.51 m in a single frame**. That has been true since hops were drawn
  at all; no test saw it because every fixture had a bank in it. A sheer climb now hauls itself up
  over the first half of the step and walks forward along the top over the second, which is what
  climbing onto a ledge looks like anyway, and peaks at 38 mm a frame.
- **The same fault, mirrored, on a sheer drop — 657 mm.** The clamp holds the figure on the upper
  floor until the boundary, so a fall timed across the whole step was already 66 cm below the ledge
  when the clamp let go. The fall is now timed into the second half, which makes the release
  continuous. What is left is 250 mm a frame, and that is the geometry rather than the curve: three
  metres inside the 25 ticks that half of `MoveCost.Drop` buys. Pinned by a test so that changing
  the drop's price moves the number and somebody reads the paragraph.
- **Nothing here is faster than a stride may be.** 50 mm a frame is the budget — twice what an
  honest frame of walking moves — and it is why `Push` is a half rather than a third: concentrating
  a climb's 1.5 m into a fraction *P* of each stride multiplies the 25 mm glide by 1.5/*P*.
- **Down** is a square, because that is what falling is. `MoveCost.Drop` is 0.83 s and a 3.0 m free
  fall takes 0.78 s; the 0.05 s difference is the step off the edge. The implied acceleration is
  **9.78 m/s²**, and `HopArcTests.AFallIsAtTheSpeedOfGravity` pins it — retune the drop and that
  test fails rather than colonists quietly falling at the wrong speed.
- **The clamp does the rest, in one rule for both halves.** The arc is allowed to pass below the
  bank; the drawn ground pushes it back up. So a body running off a slope stays on the slope until
  the slope falls away faster than it does, and is airborne after that. The hand-faded lift the
  descent used to need is gone.
- **The gait is held through a hop.** It is solved from horizontal speed, and a hop is not ground
  locomotion: measured at the old price, a drop crossed a cell at 3.0 m/s, past the fastest gait
  this cast owns (2.60 m/s), so stepping off a terrace pinned the run cycle and rate-stretched it
  for eight tenths of a second. A climb at 240 is the opposite fault — 0.63 m/s across the cell,
  which blends a third of the idle in and reads as a dawdle, which is exactly the "stuck" look.
  Holding the stride the figure arrived with covers both. **The open cost of this is that the
  cadence does not answer to the strides**: the body pushes up on to each tread and the legs keep
  the rhythm they arrived with. If the feet read as sliding up the bank, the honest fix is a climb
  pose — which no pack we own contains, so it would be computed like `WorkSwing` — and not solving
  the gait from a speed that swings between a push and a plant.

### What this is not

Not a slope model. There is no cost for walking *along* a terrace edge, no encumbrance term, and no
urgency — WS4 is still held. If a hauler should one day pay more to climb with 150 stone on her
back, that belongs on the pawn's **rate** and not on `HopCost`: design 17 §4g, cost prices the cell
and rate scales the pawn, and the two must never swap jobs.

## 4c. The ramp is one slope, and it is charged as two steps

*Owner, 2026-09-18, third look:* "The slowness needs to start happening much earlier when entering
the beginning of the tile while going up and then reaching the top back to normal — it seems to be
doing it 75% up — you slow down and then you seem to still go slow on the flat so it's out of sync."

Exactly right, and the cause is a seam rather than a curve. **The bank spans one cell; a step spans
two half-cells.** So the drawn ramp is split down the middle of the foot cell between two steps that
were priced for different things:

| Part of the climb | Which step paid | Drawn speed before |
|---|---|---|
| Bottom half of the ramp | the walk **into** the foot cell, priced as flat grass, 100 | **1.9 m/s** — faster than walking |
| Top half of the ramp | the hop **out** of it, 240 | 0.62 m/s |
| The flat top | still the hop, because a step ends at the next cell's centre | 0.62 m/s — and it should be a walk |

Two things were therefore wrong at once: the slowness began half way up (read as ~75%), and it ran
a half-cell past the top.

### What changed

**The simulation now knows a slope is a slope.** A terrace foot carries
`NaturalContent.CostClassSlope`, worth `MoveCost.SlopeExtra` — stated as `JumpUp − Orthogonal`, in
`NavGrid.cs` beside the hop price, so that entering the cell costs exactly what hopping out of it
does. If the two ever differ, a colonist changes speed half way up a slope that does not change.

That cost is direction-blind, because a cell carries one cost: **walking along the foot of a terrace
is slow too**, which is honest — the figure is drawn part way up a tilted surface the whole way — and
colonists now prefer the flat line one cell out. That is the intended consequence and
`TerraceSlopeCostTests` states it as one.

**Presentation spends each step's time where the climbing is.** `PawnPose.StepPace` models a step as
three heights — the leaving cell's surface, the boundary, the arriving cell's surface — and gives the
two halves time in proportion to what they cost to cross, where a metre of rise counts
`HopArc.ClimbWeight` metres of ground. That constant is derived from the prices (480 ticks for the
pair, less 200 for the 5 m of ground, leaves 280 for 3 m of climb: 2.3), so it cannot drift from
them. On flat ground both halves weigh the same and the pacing is the identity, so an ordinary walk
is untouched.

The boundary height takes **the higher of the two sides**, which is what makes a sheer face work: with
no ramp the lower cell's surface is its floor and the upper cell's is a layer higher, and a figure
that had not finished climbing by the time it crossed would be inside the block.

### The result, measured

| | Before | Now |
|---|---|---|
| Flat ground approaching the bank | 1.5 m/s | 1.5 m/s |
| Bottom half of the ramp | 1.9 m/s | **0.62 m/s** |
| Top half of the ramp | 0.62 m/s | **0.62 m/s** |
| The flat top | 0.62 m/s | **1.5 m/s** |
| Whole climb, flat ground to the step | 5.7 s | 8.0 s |
| Frame-to-frame on the ramp | — | **9.9 – 12.3 mm**, the variation being per-mille rounding |
| Worst single frame in the step | — | 30 mm, where the ramp's speed meets the flat top's |

`HopArcTests.TheFlatsAreWalkedAndTheRampIsClimbed` asserts all four, including that the ramp's two
halves agree with each other — which is the thing the owner was actually looking at.

**Eight seconds is the number to argue with if this still reads wrong.** It is two steps of 240, and
the lever is `MoveCost.JumpUp`: everything else — the slope cost, the pacing weight, the stride
count — is derived from it and follows automatically.

### The hitch at the start of every dear step

Found by the smoothness test rather than by anybody looking, and it had been there since steps cost
anything other than 100. `MovePercent` is a whole percent, so a 240-tick hop spends its **first 2.4
ticks at nought percent** — and every reader gated on `MovePercent > 0` drew the figure standing
still through them and then caught it up in one frame: 30 mm against the 10 mm a frame of that climb
moves. `PawnView.Moving` asks the per-mille progress instead, and is what the pose, the climb phase
and the gait hold now read.

### What is not paced

- **A fall.** A body in the air does not spend longer over the steep part, so a descending hop keeps
  the raw clock and `HopArc.Fall` owns its height.
- **A water crossing.** Its vertical profile is a curve of its own that has been played twice; water
  is level, so there is no slope in it to spread time over, and pacing it would only move a judged
  shape.
- **A ladder.** A vertical step has no ground distance to weigh against its rise; it climbs at the
  rate its connector's price sets, which is what a ladder is.

### Still quantised: the ladder climb

`PawnFigureDirector` drives the ladder climb pose from `MovePercent * 0.01`, which is the same whole
percent that caused the hitch above. A ladder costs 540, so its pose advances in steps of a 5.4-tick
percent — the identical fault, in the one animation nobody has watched since it was written
(`21-ladders-and-climbing.md`). The fix is the same one line, reading `MovePerMille`; it is left
alone here because it changes a pose that has never been looked at, and that wants its own playtest
rather than a ride on this one.

## 5. Recorded hooks

Things deliberately left for later, so the next session does not re-derive them:

- **What may stand here in future.** The owner expects some things to be allowed back — a boulder, a
  shrub, a scatter prop, anything short and draped rather than tall and upright. The guard is one
  call at one site, so letting a kind of thing back in is a per-kind decision, not a rewrite.
- **Items dropped in one.** A hauled stack dropped at a terrace foot has the same problem and no
  guard: it is not generated, it is dropped by a colonist. Unreported so far, and `ItemHeap` draws
  low enough that it may never be.
- **The other way round.** Nothing stops the bank being suppressed where a cell holds something
  instead — but the bank is baked into a chunk mesh, so anything that moves (a colonist) cannot be
  answered that way without remeshing, and a run of banks with a cell missing reads as a bite out of
  the terrace.

## 6. Where the decisions live

| Thing | Where |
|---|---|
| The shapes, the bearings, the corner z-fighting fix | `BankMesh`, `BankLayout` |
| Standing a figure on a bank | `BankLayout.RiseAt`, `PawnPose` |
| The simulation's copy of the rule | `Assets/Odyssey/Sim/Worldgen/TerraceFoot.cs` |
| That the two agree | `Assets/Odyssey/Presentation/Tests/TerraceFootTests.cs` |
| The tree guard and its count | `TreePass`, `NaturalGenReport.TreesRefusedOnTerraceSteps` |
| No tree at a foot on a generated board | `WoodedMapTests.NoTreeStandsAtTheFootOfATerraceStep` |
| What crossing a step costs | `MoveCost.JumpUp`, `MoveCost.Drop`, priced once by `NavGraph.HopCost` |
| The shape of the climb and the fall | `Assets/Odyssey/Presentation/Rendering/HopArc.cs` |
| Which steps are drawn as hops | `PawnPose.IsDrawnAsAHop` — geometry *and* a block top, so a stair is not one |
| That the price and the drawn speed agree | `HopArcTests.AClimbIsNeverDrawnFasterThanAWalk` |
| How fine the drawn progress is | `PawnView.MovePerMille`, published by `PawnRegistry` |
| What a frame may move a figure | `BankFootingTests.Smooth`, 50 mm, sampled per mille |
