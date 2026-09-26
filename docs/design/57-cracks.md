# 57: Cracks — a struck wall and a face being mined, drawn broken

**Status (2026-09-26): played once; second round built, not yet played** — branch
`claude/sat-wall-damage-levels-5eq8cn`. The first build was played the same day and moved three
things (§1a): the cracks are drawn from an impact point now rather than as a tiling (§4), rock has
six levels (§2), and a cracked thing that comes down **breaks apart** (§7). The Hud half compiles
clean; the Presentation half compiles in the owner's editor, and **no tier has been run on this
round** (owner: *"no tests or checks"*).

**Numbered 57, not 56.** PR #241 holds design 56 (the wake into the world).

## 1. The ask, and the answers

The owner, 2026-09-26: *"create a few levels of damage on walls and other furniture/structure but at
least start with walls … a general effect for cracks to appear in the wall after there is so many hps
left"*, then *"could we also apply cracks to rocks when mining as well"*.

What already existed: every building's hit points (`EdificeDamage`, design 33 §13), published per
struck building (`EdificeDamageView`), with a bar over it (design 53 §7d); and a mining order's
progress banked on the cell and published (`OrderView.Progress`), drawn as a pale slab eating down
from the top (`ChunkRenderer.DrawCellCut`). Design 33 §13k and 53 §7d both listed "a damaged look —
cracks, or a darker tint" as owed.

The owner's rulings (interview, this session):

| Question | Answer |
|---|---|
| How is it drawn? | A crack overlay generated in a shader over the thing's own mesh, not decals or a mesh swap (§4) |
| Stages | **Three, at 25 / 50 / 75 % gone** — hairline, cracked, crumbling |
| A wall's last stage | **Cracks only**; the pack's `SM_Bld_Base_Wall_Destroyed_01/02` swap is a later unit |
| What cracks first | **Walls and rock**, then the rest once the look is right |
| Rock: replace the pale cut slab? | **Yes**, the cracks replace it |
| Rock: a cancelled order erased the cut | **"Keep its state."** (§3) |

### 1a. The first look, 2026-09-26

*"The more intense cracks need to look more natural instead of shapes — so sprawling random cracks of
varying sizes (can it be [at] least random or a different pattern just for effect). The mine is fine
enough but needs more stages. Could you actually animate when it breaks — i.e. it breaks in half into
quarters — but some kind of motion to indicate the breaking of the piece and also would be really
satisfying for mining."*

| Report | What it was | What changed |
|---|---|---|
| The heavy stages read as shapes | a Voronoi tiling closes every line into a cell, and a face covered in cells is paving | cracks burst from an impact point per cell — tapering rays that wander, with branches forking off them (§4) |
| Mining needs more stages | three, at 25/50/75 % | six for rock, from 10 % (§2); a wall keeps three |
| Animate the break | the cell vanished between two frames | shudder, halves, quarters, fall, sink (§7) |

## 2. The rule

One number feeds everything: **how much of the thing is gone, in thousandths.** For a building it is
`(pool − hit points) / pool`; for rock it is the cut's progress. `Odyssey.Hud.CrackModel` turns it
into a **level on one drawn ladder of six** (`CrackModel.Levels`):

- a **wall** has three stages — 0 intact, 1 at 250, 2 at 500, 3 at 750 — drawn as levels 2, 4 and 6
  (`LevelOfStage`), so a wall and a face at the same level look equally broken;
- **rock** climbs all six, at 100, 250, 400, 550, 700 and 850 thousandths (`RockLevelOf`) — the owner
  asked for more steps on the mine, and starting at a tenth means the face answers the first strokes.

`CrackModel.Gather` lists every cell to draw cracked from three sources:

- a struck **wall** (`EdificeHandle.Wall`) from `EdificeDamage`;
- rock under a **mining order** (`OrderView.Kind == 1`) from `Orders`;
- rock **started on and left** (§3) from `PartMined`.

**Shares, not hit points**, so a 300-point wall and a 55-point sandbag crack at the same fraction when
sandbags join. **Stages, not a slide**: a player notices a step and not a gradient, and a stage is a
material, so the renderer batches by it. Which buildings crack is `CrackModel.Cracks(edifice)` — one
line to widen.

## 3. Part-mined rock keeps its cut (a simulation change)

Until now `DesignationGrid.Set` zeroed a cell's ledger on every order change, so a cancelled cut
healed — invisible until the cut was drawn as cracks. Now:

- **A cancel or a replacing order** on a mining order with work done moves the work into
  `DesignationGrid.PartMined` (`PartMinedRock`), keyed by cell and **the terrain it was cut from**.
- **A new mining order** on that cell takes it back (`Designate`), so the face carries on.
- **An order carried out** goes through `Clear` and keeps nothing: the rock is gone.
- **A row whose cell now holds other terrain is stale**: never handed back, never published, and
  dropped whenever another row is kept (`DropStale`). Nothing but a mining order removes rock today,
  so this is a guard, not a path.
- Published as `PartMinedView` (cell, progress byte) for every row with no order on its cell.
- **Saved** as its own section `odyssey.partmined`, appended to `ColonyWorld.SaveComponents`, **no
  format bump**: an older save has none, which is what a cancel then left. **Hashed only while it has
  a row** (registered by `DesignationGrid.Attach`), so **no golden moved** — the full fast tier,
  goldens included, is green unchanged.

Not done: the tile pane says nothing about a part-mined cell with no order on it. Its cracks are the
sign; a line on the pane would be new wiki content and was not asked for.

## 4. The look: `Odyssey/Crack`

The cell's own meshes are drawn a second time in `Odyssey/Crack`, a **multiply** (`Blend DstColor
Zero`) over what is already on screen, so the wall keeps its material, tint, shadows and dusk and only
gains the damage.

- **The pattern is generated, not painted** (`OdysseyCrack.hlsl`, shared with the break's pieces).
  **The first build tiled it** — the borders of a Voronoi tiling — and the owner read the heavy stages
  as shapes: a tiling closes every line into a cell. **Now it bursts from a point.** Each cell hashes
  an **impact point** of its own, and from it run up to seven **rays**, each with its own length, width
  and sideways wander (a noise along the ray, zero at the root), wide at the root and tapering to a
  hair. **Branches** — the level set of a smooth noise, which runs as open meandering lines — show only
  near a ray, so they fork off it, until the late levels, when they web the whole face. A **crushed
  patch** at the point and a **grime** over the surface come in late. Projected three ways and blended
  by the normal; the cell a surface belongs to is found by stepping inward along its normal.
- **One number drives it**, `_Severity` = level / 6, and a higher level grows the *same* cracks further
  (more rays shown, longer, wider) rather than drawing new ones. So a level added to the ladder needs
  no tuning row; the curve is in the shader:

  | Severity | Reach from the point | Rays shown | Width at the root | Branches | Grime |
  |---|---|---|---|---|---|
  | 1/6 | 0.8 m | about 3 of 7 | 9 mm | none | 1 % |
  | 1/2 | 1.6 m | about 5 | 16 mm | near the rays | 5 % |
  | 1 | 2.8 m (the whole cell) | all 7 | 26 mm | the whole face | 20 %, and the crushed patch |

- **Far off:** a line thinner than a pixel fades rather than shimmers (the pixel's footprint in metres,
  `fwidth` of the world position, taken once outside every branch), and the grime is what still reads
  from a zoomed-out camera.
- Drawn with `ZTest LEqual` and `Offset -1, -1`, so the coincident copy wins against itself and
  never against anything in front. No depth write, no shadows.
- In `ShaderInclusion.Required`, so a player build keeps it. **Without the shader** the pass draws
  nothing and mining keeps its pale slab (`CracksAvailable`).

## 5. How it is drawn: `ChunkRenderer.Cracks.cs`

- **The thing as drawn.** Each cracked cell is meshed on its own by `ChunkMesher.MeshCell`, the
  selection highlight's route (design 44 §3), so a wall's core and panels, its walls-down stump and a
  rock's boulder and skin are cracked by one rule that cannot drift from the chunk.
- **Meshed once.** A cell keeps its scratch batch and meshes again only when its chunk's version moves
  or it turns from wall to ground. A cell no longer listed hands its batch to the break watch (§7),
  which gives it back within 45 frames unless the thing came down. A cell already gone from the
  mirror while still listed is **not** meshed again as nothing: its batch is the thing as it stood.
- **Batched (P10).** Instances are grouped by (mesh, submesh, level) and submitted as one instanced
  call each, so a run of cracked walls costs the calls of one wall. A mined face's ground skin is one
  call a cell; a colony mines a few at a time.
- Walls-down and the hidden upper storeys are honoured per cell, as the chunk honours them. Frustum
  culled on the cell's bounds. Filtered to the selectable band, as the order marks are: nothing
  x-rayed is cracked.
- Measurements: `CrackedCellsDrawn`, `CrackDrawCalls`, `CrackInstancesDrawn`, `CrackCellsMeshed`,
  `CrackCellsHeld`; `Cracks = false` switches the pass off for a bench.
- **Known limit:** a module drawn by level is cracked at its finest level, as the highlight is. Walls
  and rock are not drawn by level today.

## 6. Tests

- Sim, `PartMinedRockTests` (5): a cancelled cut is kept and restored; nothing is kept for no work or
  a finished cut; a kept cut is not handed to other terrain; it is published with no order and not
  twice; hashed only while non-empty, and it survives a save. **The control was run**: with the keep
  disabled three fail and the fourth goes inconclusive.
- Hud, `CrackModelTests` (6): the thresholds, a wall and a sandbag at one share, the progress byte,
  rock's six levels and a wall's stages as every other rung, what `Gather` lists (and the scratch,
  the door, the felling order and the untouched order it does not), and the layer band.
- Presentation (EditMode), `CrackPassTests` (11): a wall cracked as exactly its five parts; three
  walls cost one wall's calls; a mined face cracks; meshed once until its chunk changes; a cell let
  go; switched off draws nothing; **and the break** — a cracked wall that comes down breaks into four
  pieces at 24 calls; a wall that stops being cracked but stands, while its chunk changes for a
  neighbour, does not break and is let go; a face mined out breaks; a cell listed again while watched
  takes its batch back; and the motion script moves every piece away from the other half, drops the
  upper quarters and sinks all four below the floor. **None of this round has been run** (§status).

## 7. The break: `ChunkRenderer.Breaks.cs`, `Odyssey/Shard`

Owner, 2026-09-26: *"animate when it breaks — it breaks in half into quarters — some kind of motion to
indicate the breaking of the piece."* Drawn only, as the felled tree's topple is (design 45 §5): the
simulation removed the thing the instant it went.

- **It is the thing that was standing.** A cell that stops being cracked hands its batch — the cell's
  own meshes, already held by the crack pass — to a **watch**. If the mirror then says its rock is no
  longer solid, or its building is gone, it came down, and that batch breaks. Repaired, cancelled or
  kept, it never loses it and the watch gives the batch back. **Counting drawn parts was the first
  idea and is wrong**: a wall built beside a cracked one hides a panel, so it draws less and would fall
  apart for having a neighbour. A thing destroyed from whole in one blow was never cracked and simply
  goes, as before.
- **It waits for its chunk.** Gone from the mirror, it breaks only once its chunk is drawn without it
  (the meshing budget can hold a chunk back a frame or two), or the old wall and its pieces would
  stand together.
- **Four pieces, clipped, not cut.** Every mesh of the cell is drawn four times in `Odyssey/Shard`, each
  clipped to one quarter — a vertical plane halving the cell across the view (or along its length, if
  it is plainly longer one way), a horizontal plane at half height — measured in the *unmoved*
  position, so a piece keeps its share of the surface wherever it goes. Nothing is sliced on the CPU:
  the pack's meshes are not all readable in a player. Inside each piece is a **solid block** of the
  broken interior, a little inside the surface, and every back face is that colour too, so a shell of
  panels reads as a lump.
- **Lit by its own shader.** The art's shader cannot be asked to clip, so the art's texture and colour
  are copied on to a shard material (once per material) and lit by the main light and the ambient
  probe. A slight change of shading at the instant of the break is possible; the shudder is there to
  carry it. The pieces carry the cracks the cell had, measured in the unmoved position so the pattern
  rides with the piece.
- **The motion**, in seconds, `ChunkRenderer.PieceMotion` (public, so a test runs it):

  | From | To | What |
  |---|---|---|
  | 0 | 0.14 | shudders whole, a few centimetres, quickening |
  | 0.14 | 0.40 | the two halves lean apart, 6–9°, on the outer edges of their feet |
  | 0.40 | — | each upper quarter topples outward off its lower one and falls under gravity to the ground, turning 70–110°; each lower quarter slumps 12–20° and slides a little |
  | 0.95 | 1.60 | everything sinks out of sight through the floor |

  Rock is flung under half as far as a wall: the faces round a mined cell are usually rock too.
- **What it costs:** the cell's parts times four plus four blocks, as plain `RenderMesh` calls, for
  1.6 s — a wall is 24 calls. At most 12 cells break at once; a thirteenth replaces the oldest.
  `BreaksInFlight`, `BreakCellsWatched`, `BreakDrawCalls`; `Breaks = false` switches it off.
- **Without `Odyssey/Shard`** things vanish whole, as before. It is in `ShaderInclusion.Required`, and
  both crack shaders now have their instancing keep-alive material (`Assets/Resources/OdysseyKeepAlive`),
  which the first build of this branch had missed for `Odyssey/Crack` —
  `EveryKeptShaderAlsoHasAnInstancingKeepAliveMaterial` would have failed on it.

## 8. Next

- The owner's second look: do the cracks read as damage now, not a pattern; are six rock levels
  enough steps; does the break read as the thing breaking, and is it satisfying to mine?
- Doors, sandbags, then furniture (which may want scratches rather than cracks), by widening
  `CrackModel.Cracks`.
- Material-aware cracks (stone cracks, wood splits, metal dents): one shader property.
- The destroyed-wall mesh for a wall's last stage, if cracks alone do not sell it.
- Dust and chips at the crumbling stage; the crash and dust on `Demolished` design 33 §13k still owes.
