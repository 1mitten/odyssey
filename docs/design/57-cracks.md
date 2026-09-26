# 57: Cracks — a struck wall and a face being mined, drawn broken

**Status (2026-09-26): built, not yet compiled in Unity or played** — branch
`claude/sat-wall-damage-levels-5eq8cn`. The Sim and Hud halves are through the fast tier; the
Presentation half (the pass, the shader, the wiring and `CrackPassTests`) was written in a container
with no editor and is **uncompiled** until the next Unity run.

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

## 2. The rule

One number feeds everything: **how much of the thing is gone, in thousandths.** For a building it is
`(pool − hit points) / pool`; for rock it is the cut's progress. `Odyssey.Hud.CrackModel` turns it
into a stage — 0 intact, 1 at 250, 2 at 500, 3 at 750 — and `CrackModel.Gather` lists every cell to
draw cracked from three sources:

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

- **The pattern is generated, not painted.** Crack lines are the borders of a Voronoi tiling in world
  space (the exact border distance, so a line has a width in metres), warped by value noise so they run
  jagged, projected three ways and blended by the normal. Two cracked walls side by side share one
  network across the joint.
- **Coverage grows, the pattern does not change.** A slow noise decides which stretches show; a higher
  stage lets the same cracks run further, so a wall visibly worsens rather than swapping patterns.
- **Per stage** (`ChunkRenderer.StageLook`, tuned by eye and owed the owner's first look):

  | Stage | Coverage | Width | Crack multiply | Whole-surface darkening | Fine network |
  |---|---|---|---|---|---|
  | 1 hairline | 0.35 | 12 mm | 0.45 | none | none |
  | 2 cracked | 0.70 | 22 mm | 0.30 | 8 % | 40 % |
  | 3 crumbling | 1.00 | 35 mm | 0.18 | 16 % | all |

- **Far off:** a line thinner than a pixel fades rather than shimmers (`fwidth`), and the surface
  darkening is what still reads from a zoomed-out camera.
- Drawn with `ZTest LEqual` and `Offset -1, -1`, so the coincident copy wins against itself and
  never against anything in front. No depth write, no shadows.
- In `ShaderInclusion.Required`, so a player build keeps it. **Without the shader** the pass draws
  nothing and mining keeps its pale slab (`CracksAvailable`).

## 5. How it is drawn: `ChunkRenderer.Cracks.cs`

- **The thing as drawn.** Each cracked cell is meshed on its own by `ChunkMesher.MeshCell`, the
  selection highlight's route (design 44 §3), so a wall's core and panels, its walls-down stump and a
  rock's boulder and skin are cracked by one rule that cannot drift from the chunk.
- **Meshed once.** A cell keeps its scratch batch and meshes again only when its chunk's version moves
  or it turns from wall to ground. A cell no longer listed gives its batch back that frame.
- **Batched (P10).** Instances are grouped by (mesh, submesh, stage) and submitted as one instanced
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
- Hud, `CrackModelTests` (5): the thresholds, a wall and a sandbag at one share, the progress byte,
  what `Gather` lists (and the scratch, the door, the felling order and the untouched order it does
  not), and the layer band.
- Presentation (EditMode, **uncompiled here**), `CrackPassTests` (6): a wall cracked as exactly its
  five parts; three walls cost one wall's calls; a mined face cracks; meshed once until its chunk
  changes; a cell let go; switched off draws nothing.

## 7. Next

- The owner's first look: are the stages readable, are the widths right at play zoom, and does rock
  read as cracked rock?
- Doors, sandbags, then furniture (which may want scratches rather than cracks), by widening
  `CrackModel.Cracks`.
- Material-aware cracks (stone cracks, wood splits, metal dents): one shader property.
- The destroyed-wall mesh for a wall's last stage, if cracks alone do not sell it.
- Dust and chips at the crumbling stage; the crash and dust on `Demolished` design 33 §13k still owes.
