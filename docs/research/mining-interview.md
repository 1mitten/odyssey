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

## 7. Risks

1. **Golden tests re-base twice** — once for the raised ground, once for terracing. Both are
   legitimate, and both must be re-based with the mechanism named, not by accepting new numbers.
2. **The free ladder** (§4f) is a design debt, recorded above so it is paid rather than forgotten.
3. **Headroom** (§3, A7) is the one decision most likely to be reversed after a playtest.
4. **Mining does not collapse anything** (answer 12), so a mined-out cell under a slab is currently
   structurally dishonest. That is U29's work and is deliberately not done here.
5. **`NaturalContent`'s coupling to `CoreContent`** — no index may be renumbered. Nothing in this
   plan adds a terrain index, which is the cheapest way to keep that true.
