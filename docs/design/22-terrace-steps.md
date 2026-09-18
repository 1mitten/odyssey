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
