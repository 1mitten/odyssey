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

**Not guarded yet: sleeping.** The owner has seen a colonist sleep at the foot of a step and vanish
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

- **Up is four strides up the bank.** The first cut was a solved parabola that left the ground,
  cleared the lip by 0.35 m and landed — and the owner's answer to it was immediate: *"when going up
  hill it looks like they jump a bit and not flat with the terrain, which they should be … would it
  be possible they take actual steps up the terrain in a few motions."* Quite right: the board shows
  a **ramp** (`BankMesh.HeightAt` is a plane from the lower floor to the upper rim), so there is a
  walkable surface the whole way and a body arcing over it is a body ignoring the ground it is on.
  The figure now steps: the drawn height holds while the ramp catches up, then pushes on to the next
  tread over a third of a stride. The stride count comes out of the height — `PreferredTread` is
  0.4 m, so a terrace's 1.5 m is four strides of 0.375 m — rather than being fixed, so a small step
  and a tall one are not drawn in the same number of motions.
- **It leads the slope by up to two thirds of a tread, and that is the stride.** Your hips go up
  when your foot does. It is never drawn below the ground, and — the assertion that separates this
  from the arc — **never above the ground it is climbing on to**.
- **A sheer face gets a plain climb.** A bank is refused against rock, inside a working and under a
  roof, and there the ground under the walker is flat for half the step and then jumps a whole layer
  at the midpoint. Strides taken off that would draw a colonist standing still and then teleporting
  three metres, so the straight chord sits underneath as a floor: where there is a ramp the strides
  are always above it and the figure treads; where there is none the chord carries it.
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
