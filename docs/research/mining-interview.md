# Mining, rock and caverns — owner interview

**Phase:** Interview (feature-level, in the shape of `phase1-answers.md`). **Date:** 2026-09-16.
**Worktree:** `D:\code\odyssey-mines`, branch `claude/mines`, off `main` at `c1b5c21`.
**Conducted by:** Claude Code, sixteen questions in five rounds. No code was written before this file.

The owner's brief: *"We now need to include mines/rocks for generation. Clarify what constitutes a
mine plot — what materials it contains, what colour — and can create caves etc."* Animations are
explicitly out of scope for this piece.

## 1. What the code already had

Grounding first, so the interview asked about real gaps rather than solved ones.

| Piece | State on `main` at `c1b5c21` |
|---|---|
| Rock, bedrock, subsoil, iron ore, coal-seam terrain defs | **Exist** — `NaturalContent`, work-to-clear 700 / 2400 / 160 / 900 / 760 |
| Rock-outcrop pass (pass 5), ore-blob pass (pass 6) | **Exist, tested, deterministic** — `NaturalFeaturePasses.cs`, ore depth-banded |
| Colours for every one of them | **Exist** — `StuffPalette.TerrainSolids` indices 15–17 |
| Caves | Only in the **ruined-city** generator — `DepthPasses.cs`, 3D value noise, threshold 790. The natural generator has none. |
| `Mine` designation | **Exists** — validated, hashed, saved, published as a snapshot channel |
| A mine *job* | **Missing.** `Fell` is the only job that edits the world. U28 in `vertical-slice.md`. |
| A stone or ore *item* | **Missing.** Meal, salvage, wood are the whole item table. |
| Any of it on the board that is played | **No.** `MakeWooded()` explicitly zeroes `outcropsPer10000Columns` and `oreDepositsPer10000Columns`. |

So the honest position: generation is roughly 70% built and switched off, and nothing downstream of
it exists.

## 2. The answers

| # | Question | Answer |
|---|---|---|
| 1 | What is a "mine plot"? | **Strata + outcrops, everywhere.** No discrete mine site, no city quarry plot. Rock below the subsoil across the whole map, outcrops scattered on the surface, ore grown inside the rock. |
| 2 | Which materials? | **Plain stone, iron ore, coal.** No exotic mineral in the MVP. |
| 3 | Caves? | **A connected tunnel system** was the first answer; refined at Q14 to *a few small sealed chambers*. |
| 4 | How does it read on screen? | **Flat palette colours, and ore glows** — the cyan emissive trim the concept renders use for salvage seams. |
| 5 | Scope | **The whole loop, end to end** — generation, work giver, job driver, items, hauling. |
| 6 | Which board? | **The wooded board that is played now.** `MakeWooded()` gains rock, ore and caverns; `MakeBarren()` stays the clean baseline. |
| 7 | Caverns: how reachable? | **No mouth — sealed caverns only.** You find one by digging into it. |
| 8 | Ore sight | **Only on an exposed face.** A seam is invisible until a neighbouring cell is open. |
| 9 | Yield | **Ore always, stone sometimes.** No rubble stage. |
| 10 | Bedrock | **Not minable at all** — a hard floor, refused at designation with a reason. |
| 11 | Stone's use | **A hauled pile, nothing more.** No building material, no reserved stuff index. |
| 12 | Collapse | **Not yet.** Mining edits terrain and marks navigation dirty; nothing falls. U29 keeps collapse. |
| 13 | Depth | **Raise the ground, same 16 layers** — not a deeper board. |
| 14 | Cavern size | **A few small chambers**, three to five, a handful of cells each. |
| 15 | Descent | **A mined shaft leaves a ladder** — a downward dig registers a Ladder connector. |
| 16 | Surface relief | **Gentle terracing** — `surfaceRelief` 2, a five-step surface. |

Questions 13 and 15 were forced by findings in the code rather than offered cold; §3 and §4f record why.

## 3. The finding that changed the plan: the board is too shallow

`120 × 120 × 16` with `NaturalMapGenDef.For` puts `groundLayer` at `min(14, 16 × 2/5) = 6`. Below the
surface that leaves six layers: two of bedrock at the bottom, two of subsoil under the grass, and
therefore **exactly two layers of rock**. Iron's band (3–13 cells below the local surface) barely
reaches it; coal's band starts 7 cells down, so **coal could not generate on this board at all**, and
a sealed cavern had nowhere to be.

The owner chose to raise the ground rather than deepen the board. That buys depth for free — not one
extra cell, so no frame-time number moves — and spends headroom above ground.

**Consequence the owner should see in the playtest.** Raising the ground *and* adding terracing both
eat the same budget, because a terrace rises above `groundLayer`. The proposal below sets
`groundLayer = 10` with `surfaceRelief = 2`, which gives surfaces at y8–12 and leaves **three layers,
9 m, above the highest terrace**. That is three storeys, and more than the colony has ever built, but
it is noticeably less sky than the board has today. If it reads as cramped, the fix is the 32-layer
board that was declined here, and it is a one-field change.

## 4. The MVP, derived

### 4a. The board

| Parameter | Today | Proposed | Why |
|---|---|---|---|
| `groundLayer` | 6 | **10** | Ten layers below the surface, so both ore bands and the caverns fit |
| `surfaceRelief` (wooded) | 0 | **2** | A five-step surface; exposes natural rock faces at terrace edges |
| `outcropsPer10000Columns` (wooded) | 0 | **16** | ≈23 outcrops on a 14,400-column board, radius 1–3, height 1–3 |
| `oreDepositsPer10000Columns` (wooded) | 0 | **70** | ≈100 deposits of 5–20 cells ≈ 1.2% of the rock |
| Map extent | 120 × 120 × 16 | **unchanged** | |

Strata for a column whose surface sits at `Ys`: grass at `Ys`; subsoil at `Ys-1, Ys-2`; rock from
`y = 2` to `Ys-3`; bedrock at `y = 0, 1`.

`MakeWooded()` stops zeroing the two feature dials and stops flattening the surface, exactly as it
already makes an exception for trees. `MakeBarren()` is untouched — anything that is not grass on the
bare board remains a bug.

### 4b. Materials and colour — the question as asked

Every colour below already exists in `StuffPalette.TerrainSolids`; none is invented here.

> **Correction, made after rendering the board (2026-09-16).** These RGB values are the
> **no-art fallback** — what a clone without the licensed packs draws. On a machine with the packs,
> `Rock` and `Bedrock` are textured (`Mat_Rock_01`, `Mat_Rock_Rough_01`) and their tint is white, so
> a surface outcrop actually renders **brown**, not blue-grey. That is a pre-existing catalogue
> choice, not something the mining work changed, and it is a look call for the owner: on screen an
> outcrop reads more like earth than stone. **Iron ore and coal are unaffected** — they are
> deliberately left untextured (`Block(...)`, with the comment "an ore seam has to stay findable at
> a glance"), so they keep exactly the flat colours below plus the new emissive trim.

| Material | Index | Colour (RGB) | Reads as | Work to clear | Found | Yields |
|---|---|---|---|---|---|---|
| Rock | 7 | `0.24, 0.25, 0.28` | dark blue-grey | 700 | Everywhere below the subsoil | Stone, **sometimes** |
| Bedrock | 15 | `0.20, 0.20, 0.22` | near-black, flatter than rock | 2400 | The bottom two layers | Nothing — **cannot be mined** |
| Iron ore | 16 | `0.46, 0.32, 0.22` | rust-brown, + cyan emissive | 900 | 3–13 cells below the local surface | Iron ore, always |
| Coal seam | 17 | `0.13, 0.13, 0.15` | near-black, + cyan emissive | 760 | 7–22 cells down — deeper than iron, on purpose | Coal, always |
| Subsoil | 14 | `0.31, 0.24, 0.17` | brown | 160 | Two cells under the grass | Nothing |

**The emissive is what makes coal visible.** Coal at `0.13` grey against rock at `0.25` is nearly
invisible in an unlit shaft; the cyan trim the concept renders already use for salvage seams is what
separates them. It is applied **only to a discovered seam** (§4d), so it never gives away ore the
colony has not exposed.

### 4c. Caverns

Three to five sealed voids per map, each a blob of roughly 6–20 cells, carved in the rock band only:
never touching bedrock, never touching the subsoil, so none of them can breach the surface. You find
one by mining into it. A cavern cell is walkable the moment it is opened — it has rock beneath it, and
`CellGrid.HasFloor` already treats solid terrain below as a floor.

Ore is concentrated around cavern walls, so breaking into one is worth something beyond the space.

### 4d. Ore sight

A new `CellFlags` bit, `Discovered = 1 << 5` (bit 5 is free). An ore cell is discovered when any of
its six orthogonal neighbours is non-solid. It is **derived, not authored**: computed after
generation and updated when a cell is dug, rebuilt on load, and therefore excluded from the state
hash exactly as `Support` and `Region` are. It is published on the snapshot so the renderer can draw
an undiscovered seam in rock's own colour.

### 4e. The loop

Following the pattern `Fell` set, which is the point of having built it first:

1. `Work_Mining`, a new work type. Order: **cutting 0, mining 1, hauling 2** — the orders that make
   work exist scan before the order that tidies it up.
2. `MineWorkGiver` scans `DesignationGrid.Cells` for `Mine`, filtered by reachability before any path
   is computed.
3. `MineJobDriver` walks to a cell beside the target, works `TerrainAt(terrain).workToClear` ticks —
   the per-material number the defs already carry, rather than one constant for all rock — then
   defers the edit to the structural phase.
4. The edit: terrain becomes air, the solid flag clears, the chunk is marked dirty, navigation is
   marked dirty, the designation is cleared, the six neighbours are re-tested for discovery, and the
   yield is spawned by `FreeCellNear` for the existing haul to collect.

**Where a miner may stand:** the eight neighbours on the same layer, *or* the cell directly above.
The second is what makes a downward shaft possible, and it is what §4f exists to make survivable.

### 4f. Descent — the hard problem

**Vertical movement in this codebase is only ever a declared connector.** `Connector.cs` refuses an
end that skips a layer; `NavGrid` has no free one-layer step. That was a deliberate decision, taken
against the decade-old CDDA stair-matching bug documented in `c-cataclysm-dda.md`. Nothing builds
ladders yet, because the build job does not exist.

So without a rule, a colonist who mines straight down is **stranded at the bottom of the shaft** and
hauling breaks silently.

The rule: **mining the cell directly below an open one registers a `ConnectorKind.Ladder` between the
two and places an `EdificeLadder` in the shaft.** A vertical shaft is climbable the moment it is dug.
The connector declares both ends, so the architecture's rule is kept — nothing is inferred from cell
contents at run time. The dig already marks navigation dirty, which is the rebuild the registrar's
own docstring requires.

**This is a fiction and should be recorded as one.** A free ladder appearing in a mined shaft is a
placeholder for a built ladder with a wood cost, and it should become one the moment the build job
lands. It is here because it is the smallest change that makes depth reachable at all.

## 5. Assumptions awaiting the owner's veto

Marked `ASSUMED` in code, per the convention `WoodPerTree` already follows.

| # | Assumption | Value | Note |
|---|---|---|---|
| A1 | Stone drops from a plain rock cell | **1 cell in 4** | Derived from a pure hash of (seed, cell index), **never a live RNG draw**, so replay, save/load and mining order all agree |
| A2 | Stone per drop | **10** | Stack limit 75, so a full stack is four hauls' worth |
| A3 | Iron ore per cell | **15** | |
| A4 | Coal per cell | **15** | |
| A5 | Caverns per map | **4** (range 3–5) | |
| A6 | Cavern size | **6–20 cells** | |
| A7 | Headroom above the highest terrace | **3 layers, 9 m** | The consequence flagged in §3 |
| A8 | Item names for the wiki | `ui.res.stone` "Stone" · `ui.res.ironore` "Iron ore" · `ui.res.coal` "Coal" | `ui.arch.tool.mine` and `ui.status.mining` already exist and need no change |

## 6. Build plan

Eight steps, each independently testable, in dependency order.

| Step | What | Gate |
|---|---|---|
| **1** | Board and strata: `groundLayer` 10, `surfaceRelief` 2, `MakeWooded()` re-enables outcrops and ore | Strata test per column; golden re-base; determinism |
| **2** | Cavern pass: 3–5 sealed chambers in the rock band | Never breaches surface, never touches bedrock; byte-identical from seed |
| **3** | Ore sight: `Discovered` derivation, snapshot channel, renderer + emissive | Undiscovered seam draws as rock; rebuild-on-load equals generate |
| **4** | Bedrock refuses `Mine`, with a rejection reason | Designation test |
| **5** | The mine job line: work type, work giver, driver, structural edit | Headless colony mines a marked cell |
| **6** | Yields: stone, iron ore and coal items, the drop rule, hauling; **wiki rows in the same commit** | `build_wiki.py --check` passes; stack reaches the stockpile |
| **7** | The shaft ladder: connector registration on a downward dig | A colonist descends five layers and returns |
| **8** | Gate: `unity.sh test editmode`, headless one-day run, milestone note | The standing five-part gate |

## 6a. What was actually built, and where it departed from the plan

Built on branch `claude/mines` on 2026-09-16, eight steps, one commit each. Where the code and this
plan disagree, **the code is right and this section says why** — a plan that quietly stops matching
what shipped is worse than no plan.

### The board, as it now generates

`120 × 120 × 16`, ground at layer 10, surface terraced across **y8–12** with three layers of sky above
the highest terrace. Per seed: **23 outcrops** (≈330 cells), **4 caverns** (52 cells), **100 ore
deposits** (≈1,145 cells: iron ≈667, coal ≈478) inside ≈83,000 cells of rock.

### Six deliberate departures

1. **Ore sight is authored, not derived** (§4d said derived). It is a `CellFlags.Discovered` bit,
   hashed and saved with the other flags. The reason is that it is a **one-way latch**: a seam the
   colony has seen and then walled back up is still known, and a derived bit would forget it the
   moment the wall went up and remember it when the wall came down. Knowledge is history, so it is
   state.
2. **Nothing at all is discovered at generation** — stronger than "hidden until a face is exposed".
   The ore lining a cavern wall is adjacent to open air from the moment it generates, so the literal
   rule would have revealed every chamber's treasure on a board nobody had dug into: the sealed
   caverns would have been a treasure map. Discovery now latches only on a dig.
3. **A connector is its own floor** — a change to `NavGrid.RefreshFrom`, outside the planned scope.
   A shaft's middle cells rest on nothing, so they failed the floor test, so they were not walkable,
   so the portal links at both ends of every ladder had nothing to join: a shaft laddered top to
   bottom that nothing could climb. A cell carrying a declared stair, ladder or lift footprint now
   counts as having something to stand in, because that is what those things are. **Found by the
   test, not by reasoning.**
4. **A colonist steps down into the cell it just dug.** Cutting a shaft means mining downward, and
   mining downward means standing on the cell being cut away — there is no other stance. The
   alternative was a pawn standing on nothing.
5. **The ground under a standing tree refuses the Mine order**, which the interview never covered.
   Digging it away would leave the tree rooted in mid-air; felling it first is the answer, and the
   order becomes available the moment the tree is gone.
6. **Iron outweighs coal by about 4 to 3**, not dramatically. Coal's band only overlaps the bottom
   of the rock, so on the lowest terraces it is pulled up into whatever rock exists. It is the
   deeper find, not a rare one.
7. **A marked stack is worked from the top down.** Cutting out the bottom of one first leaves the
   rock above it hanging in the air, and nothing catches that — the generator's column check runs
   at generation only and collapse is U29's work. A tapering outcrop turns out to be safe by its
   own geometry (the ring below a peak has rock on every side until the peak goes, so nobody can
   reach the lower cell), but a **terrace step** is not: a two-cell face whose bottom can be cut
   from the side is exactly what the starting order marks.
8. **The colony starts with an outcrop marked for mining**, because there is still no tool to give
   the order with — the same reason `startingFellRadius` exists. Checked over five seeds against
   the board the scene actually loads, not the 60-cell test fixture.

### Two things only the Unity gate could catch

The fast tier does not compile the Presentation assembly, so neither of these could fail until the
authoritative gate ran, and both are worth remembering:

- **`ModuleIdTests` pins the item module table against `ItemIndex`.** Three new item defs with no
  module id would have drawn as the orange stand-in marker for ever — no compile error, no
  exception. No pack contains ore, so all three are rock, and the catalogue rows exist to make them
  three *different* rocks (a cairn, a boulder, a flat scatter) chosen by silhouette, because items
  are drawn with no per-item tint and colour cannot separate them.
- **A piped gate hides its exit code.** `unity.sh test editmode | tail` reports the failures in its
  own output and then exits 0, because the pipeline takes `tail`'s status. The script itself is
  correct; the invocation was not.

### Assumptions as shipped

A1–A8 all shipped at their proposed values. The 1-in-4 stone roll was verified at **3 of 12** cells
in the end-to-end run, and the roll is drawn from (world seed, **cell index**), never the tick.

### Known limitations, named rather than discovered later

- **A sealed cavern is still visible if the player scrolls the layer down.** There is no fog of war,
  and building one was not in this MVP. What the ore-sight rule buys is that the *treasure* stays
  hidden: a chamber reads as an empty void until somebody cuts a face near it. If the caverns should
  be genuinely unfindable, that is a fog-of-war feature and a separate decision.
- **Mined cells and shaft ladders are not saved.** The grid is not in the save (OQ-08), so a world
  reloaded mid-dig comes back undug and its shafts unclimbable. Both halves are fixed by the same
  piece of work, and the ladder's placeholder status (§4f) should be paid off at the same time.
- **Mining still collapses nothing** (answer 12, deliberate). A cell mined out under a slab is
  structurally dishonest until U29.

## 6b. Second interview: what spoil looks like, and digging downward

Two things the owner watched and did not like, and the three decisions that came out of asking.

**"The rocks looked odd when they are mined — just a few gray rocks would be fine and not a weird
pile."** Dropped stone drew as `SM_Prop_StonePile_01`, a cairn **1.48 m tall**, so eight stone
knocked off a face appeared as a chest-high monument standing in the cell.

- **Decision (owner): two or three chunky boulders.** Stone is now `SM_Gen_Env_Rock_03` at 0.35 —
  a lump 0.48 × 0.36 × 0.40 m, about shin high. Iron ore moved to `SM_Gen_Env_Rock_08` at 0.16
  (taller than wide: shards off a seam) and coal to `SM_Gen_Env_Rock_Pebbles_02` at 0.5 (the
  flattest of the three). Silhouette is still the only axis available — items carry no per-item
  tint — so the three were kept deliberately different in proportion.
- **Decision (owner): a bigger pile looks bigger.** `ItemHeap` draws a rubble item as *several*
  rocks, the count running from 2 at a fresh drop to 7 at a full stack of 75. The mechanism is
  **count rather than size**, which is a small departure from the way the option was worded: eight
  stone scaled to a quarter of a boulder reads as one small rock, where three rocks read as three
  rocks — and "two or three boulders" is a count in the first place. Size still varies ±20% per
  rock, for variety and not for quantity.
- It costs nothing to draw. Items were already grouped by def and submitted instanced, so seven
  rocks are seven matrices in a buffer that was going to be submitted anyway.
- The layout is a **sunflower spiral** (golden angle, √index radius) turned by the item's own id.
  Independent random offsets pass every "inside the cell" check and still put two boulders in the
  same place often enough to be seen, and two boulders in one place read as one bad boulder.
- Only what a mine leaves is a heap. Rations come in a crate and wood in a bundle, and both draw
  one prop exactly as before.

**"When you mine on stone below the current height, the animation needs to swing down into the
stone."** The miner was standing on top of the cell it was cutting and swinging horizontally at
1.34 m — a metre and a third of clear air above the rock.

- **Decision (owner): step to the edge and swing in.** `MineWorkGiver.StandToMine` now tries three
  stances in order: beside it on its own layer, **on the rim a layer up**, and only then on top of
  it. The rim stance costs the colonist nothing, because the floor that goes is not the one it is
  standing on.
- The rim is offered **only where the rock's own ceiling is open**. Without that test the giver
  hands out stances at buried cells, where a colonist stood "on the rim" would be swinging at the
  cell above — the floor it is standing on. This was not reasoned out in advance: it was caught by
  `MinedStoneIsHauledToTheStockpile` going red, a dozen orders having been accepted on rock nobody
  could get near.
- Standing on top survives for the case that cannot be designed away: a one-cell-wide shaft has
  solid rock on all eight sides of its bottom and of the cell below that, so the only floor within
  reach of the next cut is the one standing on it.
- **`WorkStyle.Dip`** aims the whole stroke down by 45° whenever the work is a layer below the
  feet, and the aim point rises from the work cell's floor to its **top face** — which is the
  surface a miner on a rim actually strikes. The 45° is derived, not dialled: the edge lands about
  1.34 m up and 1.73 m in front of a chain pivoting near the base of the spine, and bringing that
  point down to a metre below the pivot is some 46°.
- The dip goes on the **spine and the shoulder together** and not on the shoulder alone. The
  director gives the back `Spine` and the arm `Shoulder - Spine`, so adding the same angle to both
  leaves the arm's angle against the chest alone and lowers the pair together — a person leaning
  over a hole rather than a person pointing at the floor.
- The **reach is measured a second time** in the dipped pose (`MeasuredDippedBladeHeight`,
  `MeasuredDippedReach`, both printed by `DescribeTools`). A figure bent 45° reaches about a
  quarter of a metre less far in front of itself; solving the stand against the upright reach would
  stand the miner that far back from its own hole, which is the same class of mistake as writing
  the reach down instead of measuring it.
- **The design note was wrong about this.** `12-work-poses-and-tools.md` states that no vertical
  aiming is needed now or for mining. Its reasoning holds for felling — a tree and the colonist
  cutting it stand on the same floor — and it assumed a miner would too. A miner does not.
- **Still owed:** the 45° has been derived and bracketed by a test, not photographed. The number
  that settles it is `MeasuredDippedBladeHeight`, which wants to be near nought, and `SwingCheck`
  is still exhausting render textures in batch mode.

## 6c. Third interview: mid-air colonists, drop speed, and things that fall

The owner photographed a colonist standing in mid-air over a worked face, asked for drops to be
quicker, and asked whether spoil could fall into the layer below so that more of it could be picked
up at once.

**The first diagnosis was wrong, and the measurement is why it did not ship.** A photograph cannot
tell a pawn left unsupported by a dig from a pawn drawn part way through a very expensive step from
a pawn standing on a ladder nothing draws. `Assets/Editor/Odyssey/FootingProbe.cs` was written to
settle it and ran the playtest colony for 40,000 ticks:

| | before | after |
|---|---|---|
| colonists ever standing on nothing | **0** | 0 |
| pawn-ticks spent standing on a ladder | 34,004 (~1/6 of all colonist time) | unchanged, now drawn |
| stacks of spoil on the ground with no floor | **26 of 107** | **0 of 81** |

So there was never a gravity bug for colonists. There were three other things.

- **The ladder was never drawn.** `EnsureLadder` laid a navigation connector and flagged the cells,
  and that was all: the def (`CoreContent.EdificeLadder`), the module id, the catalogue row with a
  real prop on it, `ModuleShape.Ladder` and `ChunkMesher.EmitLadder` all existed already and were
  never reached. A colonist climbing one therefore hung in mid-air over the hole with no terrain and
  no rock under it, which is exactly the photograph. Mining now places the edifice. **In the shaft
  cell only** — a ladder fills the hole it is in and you step off at its top, so a rung in the upper
  cell as well draws a ladder six metres tall with half of it standing proud of flat grass. That was
  the first attempt and the picture of it is unmistakable.
- **The drawn glide finished a quarter of the way through a ladder step.** The published move
  percentage was the raw progress clamped to 100, which is exact for a flat cell at 100 units and
  wrong for everything dearer. A ladder down costs 400, so the figure reached the bottom in the
  first quarter of the step and then stood frozen for the other three — six and a half seconds a
  rung. It is now a true fraction of the step's own cost (`Pawn.MoveStepCost`), so the glide takes
  exactly as long as the step does.
- **Items had no support rule at all.** `NearestCellWithSpace` only ever searched the miner's own
  layer, so spoil was dropped into the cut cell whether or not that cell had a floor.

**Decisions (owner):**

- *Never leave a colonist on nothing, and catch any that slip.* The causes are fixed. The safety net
  was **not** built, and this is a deliberate departure: the probe shows nothing is ever unsupported,
  so a per-tick pass over every pawn would be a system that does nothing. The net is a test instead —
  `FallingTests` pins the landing rule, and the probe re-measures the claim on demand.
- *A fall is near-instant; a climb stays deliberate.* `LadderDown` stays at 400 ticks, now glided
  across properly and with a ladder to climb. An item's fall is instantaneous, resolved in the same
  deferred phase as the dig. If a 6.7 s climb still reads slowly once it is played, `MoveCost.LadderDown`
  is the one number to change.
- *Items fall to the first solid floor.* `CellGrid.FirstFloorAtOrBelow` is the bottom of the fall and
  not the length of it — three layers and one layer finish in the same place, because nothing bounces.
  It clamps at layer nought rather than returning -1, so no caller has to guard it.
- *Merge up to the normal stack, spill the rest nearby.* `ColonyItems.MoveTo` reuses `Drop`'s landing
  rules exactly, so a stack that falls down a shaft and a stack a colonist carries down it end up in
  the same state. Measured effect: the same stone came out as **81 stacks where it had been 107**,
  which is 26 fewer hauling trips.

**Still owed:** `EnsureLadder` remains free (§4f) — a colonist gets a ladder for nothing, it costs no
materials and no work, and it is now a visible thing in the world rather than an invisible edge,
which makes the debt easier to see and no smaller. Mined cells, ladders and fallen stacks are still
not saved (OQ-08).

## 6d. Fourth round: the ladder comes out again, and the jolt is explained

The owner: *"these ladders shouldn't be visible and the animation jolts around when descending by
them — can we have them just fall/climb down or just make the ladder invisible (and have it
climbing)."*

**The ladder prop is gone.** It lived for one commit. It was the right diagnosis of the mid-air
colonist and the wrong fix: a shaft dug with a pick does not come with a ladder in it, so drawing
one put a free, unbuilt fixture in every pit on the board. The navigation connector stays — it is
what lets a miner out of its own shaft — and `PawnContext.Edifices`, `MapGenOutcome.EdificeList` and
`MineJobDriver.PlaceLadder` were all taken out with it rather than left as unused plumbing.

**The jolt had nothing to do with ladders and affected every vertical step in the game.** Two
separate bugs, both in presentation, both found by reading the code the complaint pointed at:

- `PawnPose.Of` handed back `to - from` as the heading. A step that only changes layer travels
  `(0, ±3, 0)`, which is not a zero vector, so it was passed on as a bearing — and the yaw of it is
  `Atan2(0, 0)`, which is not "no bearing", it is **zero, which is due north**. Every colonist
  entering a shaft turned slowly to face north and turned back on the way out. The heading is now
  horizontal only; zero length already meant "keep facing wherever you were", which is exactly right
  for a climb.
- The gait blend took its speed from the full 3-D displacement, so a colonist covering three metres
  straight up was walking at the blend's reckoning. It is ground speed now, which leaves a climber
  at nought and blends to the idle.

**And there is a climb pose**, because an idle rising through a hole is better than a walk cycle and
still is not climbing. `ApplyClimbPose` lays alternating overhead reaches on both arms, two per cell
climbed, phased from the step's own progress so the reach matches the height gained and the pose
holds still while the game is paused. Arms only: the rig's left and right arms are both bound
already (the off-hand IK solve needed them) and no leg is, so legs are a piece of work rather than a
tweak. `shot-climb.png` is the frame.

**Vertical movement is repriced, asymmetrically.** `LadderDown` 400 → **100**, the same as walking a
flat cell, because dropping a layer is letting go. `LadderUp` 540 → **270**, still the dearest
ordinary step there is, because climbing out of a shaft with a load should make a colonist prefer a
ramp and the pathfinder only learns that from the price. Measured over the same 40,000 ticks: layer
steps fell from **25,372 to 9,880** and time spent on a connector from **34,004 to 24,107**
pawn-ticks.

**Still owed:** legs in the climb pose, and the free connector (§4f) — a colonist still gets its way
out of a shaft for no materials and no work. Making that a built thing is the building line's job,
and it will place its own edifice when it comes.

## 6e. Fifth round: the ladder is eliminated, and a climb needs something to climb

The owner, in three messages: *"the models need to fall twice as quick"*; *"I did see a colonist
climb back up in mid air with nothing there that made no sense — a climb can only happen if there is
a tile in front of you, a height block above and you climb against the edge of that block"*;
*"eliminate the ladder and use climbing instead because it's causing bugs"*; *"ladders are used in
game not for mining"*.

**Ladders and climbs are now different things.** `ConnectorKind.Climb` is appended to the enum (no
existing value renumbered — these are part of the save contract), with its own footprint flag
`NavFlags.ConnectorClimb` and its own costs. Built ladders keep `ConnectorKind.Ladder` and their
original `LadderUp`/`LadderDown` of 540/400: the generator puts them in buildings and they had no
business being repriced to fix mining. `NavGraph.EnsureLadder` became `EnsureClimb` and is the
mining path's alone.

Modelling a mined shaft as a ladder had quietly claimed three untrue things — that somebody built
it, that it costs what a built ladder costs, and that it is a fixture with geometry. The third one
reached the screen, as §6d records.

**A climb requires a block beside it, and keeps requiring one.** Checking at creation was not
enough and the measurement said so: with the creation test in place, **1,890 of 9,880** climbing
pawn-ticks still had no wall, because a climb laid against rock that is *later* mined away goes on
insisting there is a face to hold. `MineJobDriver.RetireClimbsThatLostTheirWall` drops it when the
cut takes its last face, and the number went to **0 of 9,828**. Four faces and not the diagonals: a
corner is not something you can get your weight against.

**And that created a live cause for the safety net §6c had declined to build.** Retiring a climb
takes away the footing of whoever was on it — a climb cell counts as standable to navigation, which
is the whole point of it — and the probe duly found two of five colonists standing still in mid-air
for the last 12,600 ticks of the run. `DropAnyoneStandingIn` now lands them on the first real floor,
the same `CellGrid.FirstFloorAtOrBelow` the items use, and `StepDownOntoTheFloorJustCut` was
rewritten in terms of it: it used to move a colonist exactly one layer, which is wrong over a
two-deep hole and left them in the middle of it.

**Falling is twice as quick again.** `ClimbDown` 100 → **50**, about five sixths of a second for
three metres. `ClimbUp` stays 270.

**The climb pose eases in four times faster.** It first borrowed `WorkEaseSeconds` at 0.44 s, which
is right for setting yourself in front of a tree and far too slow here: a drop takes 0.83 s, so the
reach was measured **41% arrived** at the moment it was photographed and the arms had barely left
the figure's sides. `ClimbEaseSeconds` is 0.15 s — reaching for a hold is a grab, not a
settling-in. `PawnFigureDirector.DescribeClimb` prints phase, weight and which way the wall is, so
"the arms are down" can be told apart from "there is no wall" and "there is no world to ask".

| measured over 40,000 ticks | §6d | now |
|---|---|---|
| climbing pawn-ticks with no wall beside them | 1,890 of 9,880 | **0 of 9,828** |
| colonists unsupported at the end | 0 | 0 |
| spoil stacks with no floor | 0 of 81 | 0 of 84 |

## 6f. Sixth round: walking on air, and where a climb actually ends

The owner: *"colonists were still climbing mid air with no block/tile there and then walking across
air with no tile beneath them after they mined those rocks."*

**The previous round's probe said zero because it was asking the wrong question.** It counted a cell
carrying a connector footprint as somewhere to stand — which is what the navigation grid believes,
and the belief is the bug. Asked properly, the board was full of it:

```
10,180 of 69,013 sideways pawn-ticks stepped into a cell with NO floor
 6,317 pawn-ticks stood still on one
```

Every one of them carried a climb footprint. `NavGrid.Refresh` grants `Walkable` to any connector
cell, so a row of them is a bridge and colonists walked out over their own quarry.

**`NavFlags.ClimbOnly` splits the two questions `Walkable` was answering at once.** "May a region
form here" must stay yes, or the shaft drops out of the region graph and the miner in it becomes
unreachable. "May somebody walk in here" must be no. `NavGrid.CanWalkInto` is the second question;
`CanEnter` remains the first. The asymmetry is deliberate and getting it the tidy-looking way round
strands everybody: **you may step off a rock face onto ground, and not onto one from ground.**

**And that exposed the deeper fault: a climb was ending in the wrong place.** It went from the floor
of a pit to the cell directly above — open air whose floor had just been dug away. That only ever
worked because a colonist could stand there. Ban it and a pit seals itself: the single cell joining
it to the world is a cell nobody may enter. Measured: mining fell to **24 cells** from a baseline of
63, with **40** standing orders that had ground beside them and could not be reached.

A climb now ends **on top of the block beside the hole**, which is where a person actually ends up
and is real ground. The vertical climb survives as a fallback for the bottom of a shaft two or more
cells deep, where the block beside you is taller than you can reach past; those intermediate cells
are the only floorless standable cells left, and they are entered and left by climbing alone.
`EnsureClimb` now refuses a second way out of a cell whatever direction it is asked in — with four
possible landings the old "both ends already flagged" guard would have let a cell collect a climb in
every direction.

**Nobody hangs on a rock face doing nothing.** A pawn whose path ends or fails on one used to stay
there, idle, in mid-air. `MovementSystem` lets it go to the first real floor.

| measured over 40,000 ticks | before | strict rule only | now |
|---|---|---|---|
| sideways steps onto a floorless cell | 10,180 of 69,013 | 0 | **1 of 68,222** |
| pawn-ticks standing still on one | 6,317 | 0 | **1** |
| cells mined | 63 | 24 | **58** |
| standing orders that cannot be reached | 17 of 45 | 57 of 84 | **16 of 50** |
| climbs with no wall beside them | 0 | 0 | **0** |
| colonists unsupported | 0 | 0 | **0** |

The two remaining 1s are single-tick transients — the tick before the let-go rule fires, and a step
begun in the tick the world changed under it.

**A lesson worth keeping.** Three rounds of this were spent fixing what a photograph appeared to
show. What settled it every time was a probe, and the probe was wrong twice before it was right:
first it counted a connector as a floor, then it only asked about *climbing* when the complaint was
about *walking*. A measurement that agrees with you is worth no more than a screenshot until you
have checked what it is actually counting.

## 6g. Reconciled with main, 2026-09-16

Main had moved 49 commits under this branch, adding water to the wilderness, ground relief to the
drawing, and a Build palette to the interface. Nine files conflicted. Three of the resolutions were
decisions rather than edits and are recorded because getting any of them wrong is silent.

**Two numbering collisions, and both went main's way.** Numbers that are hashed, saved or used to
seed a random stream cannot be held by two meanings at once, and the branch that landed first keeps
the number it shipped with:

| collision | main | this branch |
|---|---|---|
| `CellFlags` bit 5 | `ImpassableTerrain` (deep water) — **keeps it** | `Discovered` → **bit 6** |
| `NaturalGenPurpose` 8 | `Water` — **keeps it** | `Caverns` → **9** |

The second is the one that would have hurt: two purposes on one value means two passes drawing from
the same stream, which is exactly the leak that enum exists to prevent. It is the same mistake as
the `PawnPurpose.Passion` / `StoneYield` collision caught in §6a, found the same way — by reading the
conflict rather than by taking either side.

**The pass list is ten passes now**, water at 2 and 4, caverns at 8, ore at 9, start at 10.

**The wooded board changed shape under main's feet.** The mining work stopped `MakeWooded` routing
through `MakeBarren`, so the board gained terracing, outcrops and ore; the water work was written
against what it believed was still a flat table, and `WoodedMapTests` carried both beliefs. The
water *code* is per column and did not care, but its tests compared every wet column against a
single board-wide dry level. They are stated per column now: a channel sits one step below its own
dry neighbours, wherever that column's ground happens to be.

**Two goldens re-based, with the mechanism named** (`WaterTests.BeforeWater`). Both the dry and the
*barren* hashes moved. The dry ones moved because caverns are a new pass that carves rock. The
barren ones moved because headroom relocated the ground layer on every natural map — and they are
still seed-invariant, one flat board per size, which is the check that it was the height and not the
contents that changed.

**One test widened rather than deleted.** `AMarkedStackIsWorkedFromTheTopDown` needs a cell with
solid rock directly above it *that a colonist can still reach*, and that shape is rarer now: the rim
stance refuses a rock whose ceiling is closed and the on-top stance needs the cell above to be open,
so only a same-layer stance will do. Seed 1 of the played board no longer offers one, which says
nothing about the rule. It walks eight seeds now.

**Measured after the merge**, on the same 40,000-tick probe:

| | before the merge | after |
|---|---|---|
| cells mined | 58 | **65** |
| standing orders that cannot be reached | 16 of 50 | **0 of 0** — the colony finished its work |
| items with no floor | 0 | 0 |
| sideways steps onto a floorless cell | 1 of 68,222 | 1 of 66,811 |

## 6h. Two things the owner saw in the merged build

**"There seems to be faint selection over every tree and stone — is there any reason for this now?"**

Yes, and it was the wrong reason. Standing orders were drawn with `ChunkRenderer.DrawCellHighlight`,
which is the **selection cursor**: the corner-stub bracket. The starting scenario marks every tree
within ten cells (`startingFellRadius = 10`) and three outcrops of stone, so the game opened with a
selection cursor around something like a hundred things at once. Nothing was broken; the wrong word
was being used. A selection is the one thing the player is looking at and an order is a job on a
list, and if the two look alike then neither means anything.

Orders now have `DrawCellMark` — a thin translucent plate laid on the face the order is read from:
the top of solid rock for a mine order, the floor for a fell order. Inset from the cell edges so a
run of marked cells reads as a run rather than one sheet, flat so it never competes with the thing
it marks, and at 0.42 alpha rather than the bracket's 0.55.

**"If you are set at the height of the current stone you should be able to see the stones above it."**

The mechanism was already there and the numbers made it useless. `AboveMode.Xray` is the default and
`aboveDepth` is 4, but `ghostAlpha` was 0.20 with a falloff of 0.55, so the four layers it draws
came out at **0.20, 0.11, 0.06, 0.03** — the last within a whisker of the 0.012 cutoff that drops a
layer entirely. The cut-away exists so a player can see what is over their head; at those numbers it
only proved that something was. Now 0.38 and 0.72, giving **0.38, 0.27, 0.20, 0.14**, each still
plainly subordinate to the solid layer being worked.

`SliceSettings` is serialised into `Play.unity`, so the scene carried its own copy of both numbers
and a class default alone would never have reached the game. Both were changed.

**Owed: neither of these has been photographed.** The editor was open, so no batch command could run
— `scripts/unity.sh` refuses to share a project. The simulation gate is green (381/381) and both the
presentation and editor assemblies compile against the Unity assemblies offline, but the Unity
EditMode gate and the pictures are owed. `PlayScene` now has the shot that takes them
(`shot-xray.png`, the tallest rock viewed from two layers below its top) and the hook it needed:
the slice layer is a variable the render hook reads, because `Shoot` calls `camera.Render`, which
re-runs the hook — so rendering a different slice before shooting achieved nothing.

## 7. Risks

1. **Golden tests re-base twice** — once for the raised ground, once for terracing. Both are
   legitimate, and both must be re-based with the mechanism named, not by accepting new numbers.
2. **The free ladder** (§4f) is a design debt, recorded above so it is paid rather than forgotten.
3. **Headroom** (§3, A7) is the one decision most likely to be reversed after a playtest.
4. **Mining does not collapse anything** (answer 12), so a mined-out cell under a slab is currently
   structurally dishonest. That is U29's work and is deliberately not done here.
5. **`NaturalContent`'s coupling to `CoreContent`** — no index may be renumbered. Nothing in this
   plan adds a terrain index, which is the cheapest way to keep that true.
