# 38 — The Meadow overhaul: lush grass, Meadow trees, a landscape instead of terraces

*Numbered 38. Written as 36; by the time it merged, `main` had given 36 to radiant heat and
`claude/medical-supplies` had 37, so it moved, with every place that cited it (2026-09-24).*

**Status: designed 2026-09-24, nothing built.** Branch `claude/meadow-overhaul`, worktree
`D:\code\odyssey-meadow`. Interview: `docs/research/meadow-interview.md` (twenty answers, the
source of every decision here). Research: `e-09-meadow-mesh-data.md` (what the pack's meshes carry),
`d-16-shader-warmup.md`, `d-17-terrain-skin-shading.md`, `d-18-dense-foliage-cost.md`.
**Read first:** `06-rendering-and-camera.md` §6c (the frame budget and how it is measured),
`22-terrace-steps.md` (banks and `TerraceFoot`, which §5 generalises), `28-map-size.md` §8–§10
(frustum culling, PR #174).

This document holds the decisions, the mechanisms and the units. Each unit adds its measurements
and its reversals here as it lands, in the project's usual way.

## 1. What the owner asked for, in one table

| Topic | Decision |
|---|---|
| Landscape | **A smooth skin** over the unchanged simulation layers; every earth step drawn as a slope. Rock, dug and two-layer faces stay sheer. |
| Heights | **Rolling hills, ~8 layers (~24 m)**, performance held. Board height **measured first** (§7). |
| Grass | **Meadow meshes, our own shader.** Lush on **every** tile; never hides an item; spring/lime; ~1.1 m. |
| Flowers | Drawn only, placed by a rule the simulation could later own. |
| Trees | **All Meadow, more species**: birch, the giant meadow tree, fruit trees (shape only; fruit later — confirm at M5), bushes as sim things. |
| Canopies | Fade over pawns, the cursor and the selection; topple when felled; wind freezes on pause. |
| Sim props | Loose rocks, mushrooms, berry bushes. |
| Surround | The skin continues past the rim; Meadow wood, card LODs in the distance. |
| Atmosphere | Meadow post-processing (as an option), Meadow water, ambient FX. Not the Meadow sky. |
| Occlusion | **Not built.** Frustum (PR #174), LOD, distance and the layer slice. |
| Slopes | The ground levels under anything built (drawn only). |
| Settings | Quality presets Low/Medium/High/Ultra/Custom; vegetation density and distance; LOD bias, foliage shadows, wind; terrain detail. |
| Perf bar | **Ultra: 60 fps at 3840 × 2160** on the RTX 5070 Ti, Standard and Huge. **Low: 60 fps at 1080p** on an RTX 3050/3060 laptop. |
| Loading | As long as it needs, if play never hitches. |
| Order | Lush grass and Meadow trees first, on today's ground; then the landscape; then settings, loading, atmosphere. |

## 2. What is true before any of it

- **The pack is already imported and half in use.** Today's tufts (`SM_Env_Grass_Med_Clump_02/03`,
  `Tall_Clump_03`) and the grass ground (`Mat_Grass_Textures_01`) are Meadow assets; the trees are
  PolygonGeneric (`PlayScene.cs:1539`). Nothing is imported, so the package's own `PolygonGeneric`
  (the GUID trap) is never touched.
- **Every Meadow piece sits on its prefab origin** — every LOD child and every FBX node is identity
  with no negative scale (e-09 §1). **One matrix per plant draws every part of every level.** That
  is the fact the whole of §3 rests on.
- **The world has no GameObjects**, so a LODGroup does nothing: `ModuleLibrary.HighestDetail` keeps
  LOD0. Instancing on the pack's own materials is off, and its Foliage shader runs its wind on
  `_Time`.
- **As the project is set up, anything drawn is drawn up to six times** (d-18 §2): the SSAO
  DepthNormals prepass, the forward pass, and four shadow cascades over a 250 m shadow distance,
  with depth priming off, so the forward pass gains nothing from the prepass. For full-cover grass
  this is the whole problem.
- **The player starts on Direct3D 11**, then DX12 (`ProjectSettings.asset`). That decides which
  warm-up exists (§8).
- **Frustum culling is built and not on `main`.** PR #174 is green, and since 2026-09-24 it conflicts
  with `main`.

## 3. Instanced LOD

`ResolvedModule` gains `Lods[] { LodPart[] Parts; float ScreenHeight }`; `Parts` stays an alias for
LOD0 so nothing that reads it today moves. A level is a list of one or two (mesh, material) pairs —
trunk and branches — **drawn against the same matrix array**. A new bucket kind keyed
`(module, tint)` stores the **bare placement** rather than `placement × part.Local`, and
`DrawBuckets` loops over the chosen level's parts. `Partition`'s un-place becomes the identity.

- **Levels kept** (e-09 Recommendation 2): trees keep LOD0, the lowest mesh before the card, and the
  card — the four-level trees drop their LOD1, saving a bucket where buckets are the cost; bushes and
  grass keep LOD0 and their last level; wildflowers and the sunflower keep LOD0 and the card; flat
  flowers, rocks and mushrooms have one level. *Tie-break:* a screenshot pair of a birch stand at
  30 m in play light, LOD1 against LOD2; if the owner cannot tell them apart, LOD1 stays dropped.
- **The level is picked per chunk, not per instance**, once a frame in `ChunkRenderer.Render`
  straight after the frustum test, from `batch.Bounds.SqrDistance(ViewerPosition)`. The pack's
  screen heights are converted to metres against the camera's field of view and multiplied by the
  LOD-bias setting, with hysteresis held on `ChunkBatch`. A 62.5 m chunk is decided by its nearest
  corner, so the saving shows at wide zoom. The surround picks its level when a batch is built: near
  wood LOD2, far wood the card.
- **Every level reuses LOD0's `place` normalisation.** Load asserts the parts' `Local`s agree and
  falls back to per-level lists if a future pack breaks the rule.
- **`TallestModuleMetres = 12` is a hand constant** read by the sight and frustum tests. It becomes
  the largest resolved bound, or the 15 m giant is culled with its crown on screen.
- **Anything the composition root writes every frame** (`ViewerPosition`, the level and density
  inputs) gets an override hook for tests, the `FrustumOverride` pattern (P17).

## 4. `Odyssey/Foliage` — our shader, their textures

A clean-room hand-written URP shader in the house idiom (`Odyssey/Tree`, the closed branch's
`Odyssey/Grass`). It reads the Meadow textures through materials built at runtime. **No Synty file
is copied, modified or committed**; a clone without the packs draws primitives, as today.

- **Inputs** (e-09 Recommendation 3): a leaf albedo with the cut-out in alpha, a trunk albedo,
  optional leaf and trunk normals (a flat normal otherwise), and a clip threshold (0.25, or 0.5 for
  the atlas flowers). Cull off with the normal flipped on back faces. **Leaf or trunk is chosen by
  vertex colour B > 0.5.**
- **Wind** from the vertex colours as authored: **bend = saturate(2R)** (base 0, crown 1), a sway
  along the global wind direction phased by the instance's translation; **flutter = B × G** along
  the normal. **The phase is the tick, not `_Time`** (`WindDirector` from the closed branch, reused
  unchanged), so a paused world holds still. Meshes without vertex colour do not move.
- **Passes:** `UniversalForward`, `DepthOnly`, `DepthNormals`, `ShadowCaster`. **Every pass shares
  one displacement include**, or the prepass and the forward pass disagree and the grass shimmers.
- **Alpha** (d-18 Recommendation 2): `clip` in the depth passes; the forward pass is **depth-primed**
  (`ZTest Equal`, `ZWrite Off`, no `clip`) once priming is on (§6). Not alpha-to-coverage.
- **The clearance field** (from the closed branch): a small top-down texture over a window **centred
  on the rig's focus, not the camera** — the closed branch centred it on `ViewerPosition`, which
  leaves the far half of a 48° view outside it. Items and order marks stamp a ~0.5 m ring. Buildings
  are cleared by the mesher, which can see them.
- **The canopy fade** for the see-through rule: a dithered fade driven by the existing
  `SeeThrough` machinery, over a colonist, the cursor or the selection.
- **The tree recolour is retired for Meadow trees.** `TreeSwatches` repaints rectangles of the
  PolygonGeneric atlas and means nothing on Meadow's textures. A per-instance hue shift from world
  position keeps a stand from tiling.
- A keep-alive material under `Resources/OdysseyKeepAlive` (ours, so committed), or the player strips
  `INSTANCING_ON`.

## 5. Lush grass on every tile

- **Every grass cell is scattered**, meshed at the densest rung (`MaxPerCell` 6 at Ultra, ~3 at Low),
  from medium clumps cut close to their silhouette (d-18 Recommendation 4). Meadow's grass, clover
  and flower terrain textures under it (§6) make whatever the blades miss read green.
- **Thinning is a count.** Each chunk's grass matrices are sorted once, at the end of `Mesh()`, by a
  full avalanche hash (`Array.Sort(keys, Matrices, 0, Count)`, no allocation). Drawing the first N
  thins the grass evenly, and `Submit` already takes a count. N is picked per chunk from distance and
  the density setting. The multiply-and-shift hash is out: its low bits are unusable (`lessons.md`).
- **No pop at a chunk seam.** The shader **shrinks** each clump by the same rank against distance, so
  thinning is continuous across the 62.5 m boundary the count steps at. Shrink, not dither (d-18).
- **Grass casts no shadows and receives them** (hard shadows on Low). That alone removes up to four
  of six draws per instance, and it is in from the first commit.
- **Memory:** meshing at the densest rung is about 3,750 matrices per surface chunk, 20+ MB on the
  250² board. The price of density being a draw-time setting.
- **Culling:** CPU per chunk on the existing grid. Not GPU-driven: 12–30k instances are on screen,
  and indirect drawing pays past ~100k (d-18 Recommendation 5). The GPU Resident Drawer does not
  apply to `RenderMeshInstanced`.
- **Budget: 2.0 ms of GPU at 4K Ultra, 1.5 ms at 1080p Low**, measured as a toggle arm, never
  inferred.
- **Flowers** are drawn by the same path from their own rule — terrain, the world seed and a cell
  hash — which the simulation could later own. The 134–209k-triangle `Wildflowers_Patch` and
  `Grass_Large` meshes are never instanced; single clumps only.

## 6. The ground skin

**One continuous mesh per chunk-layer replaces the turf box per cell** for natural earth. The
simulation does not change. Validated against the code (planning agent, 2026-09-24).

- **Corners.** A corner rises to the upper layer where any of its four cells is a valid step: earth,
  uncut, open top, exactly one layer up. That reproduces today's straight (two corners high), inner
  (three) and outer (one) banks exactly.
- **Saddles** (two diagonal corners high) split on a fixed diagonal chosen by rule.
- **A cell with three or more step sides stays sheer.** Otherwise all four corners rise and the skin
  caps a pit the simulation can put things in — the façade rule in `CLAUDE.md`.
- **Ownership.** Flat tops belong to chunk L; ramps to chunk L+1, where banks are emitted today. Both
  go in `Body`, never `Roof`, or RoofsOff deletes hillsides.
- **Levelling under what is built.** A cell with a floor or a blocker above it gets its own levelled
  vertices, with a vertical skirt wherever neighbouring edges disagree. This is required, not
  cosmetic: a draped floor and a chord-following skin part by up to ~41 mm a cell (`lessons.md`).
- **One surface owner.** `GroundSkin.HeightAt` and `SlopeAt`, using the mesh's own triangulation,
  replace relief + `BankLayout.RiseAt` in **every** stand-height reader. There are about fifteen
  today, and they already disagree on banks: `PawnPose`, `StepPace`, `WaterLine`, the foot IK and
  lean in `PawnFigureDirector`, `SlicePicker.FloorHeightAt` (which today hits the flat floor 1.5 m
  under a ramp), items and plates in `ChunkRenderer`, `EmitScatter`, `EmitCrop`, `BedShape`,
  `ShelfShape`, `DoorDirector`, the ghosts, and `WorldRenderModel.StandHeight`/`MarkHeight`. A test
  reads the sources and fails on a new reader that bypasses it (P3).
- **`TerraceFoot` mirrors the corner rule** cell by cell, for the nav slope class and the tree
  guard. `TerraceFootTests` is extended to cover saddles, pits and the three corner cases.
- **P15, a gap that gets worse.** `CanBank` and `TerraceFoot` ask whether a column is open to the sky
  by walking the whole column, while `MarkChunksAround` dirties 3 × 3 × 3. A slab five layers up
  never re-meshes the ramp below it. Either make "roofed" local or dirty the column.
- **Shading** (d-17): one hand-written material for every chunk, reading two `Texture2DArray`s
  (colour and normals) filled at runtime by `Graphics.CopyTexture` on the GPU. Four layer indices
  ride each quarter-cell quad, with weights from a local coordinate — four layers is the most a cell
  grid can ever touch, and uniform quads take a one-layer path. Height blending uses the albedo's own
  luminance; the texture is projected on world XZ and repeats every 4 m, broken up by macro noise.
  Tints multiply in linear space before lighting; tilled and stored ride in vertex colour. No
  basemap, no hole texture. Terrain detail: 512 / 1024 (default) / 2048, filled by mip offset.
- **Draw calls roughly unchanged; instances fall** by about 625 per surface chunk. Riser faces stay
  instances. Every `Mesh` is owned and disposed by its `ChunkBatch` (the device-removed lesson).
- **A dug column** reads 3 × 3 cells and ±1 layer, inside `MarkChunksAround`: about three chunks
  under the budget. Re-measure `MeshBudgetPerFrame` once the mesh upload is real.

## 7. Hills, the slice and the board's depth

- Worldgen relief goes to **±4 layers**, spaced so a hillside reads as a slope rather than a
  staircase. That changes `SurfaceY`, so the goldens re-bake.
- **Measured 2026-09-24 (M7, `28-map-size.md` §11).** ±4 relief with today's noise leaves **no
  cliffs** on any board over five seeds. At 16 layers the lowest valley has **no rock** above the
  bedrock (three today); at 20 it has four, for 25% more cells (Huge 70.6 → 87.1 MiB); at 24, eight,
  for 50% more (Huge 104 MiB). The Huge edit tick follows the relief, not the depth (1.21 → 1.48 ms
  at 20, 1.62 at 24).
  **Recommendation, ranked: 20 layers, then 24, never 16.** Twenty gives the mine back with a layer
  to spare at the smallest cost that does; 24 buys a deeper mine the design has not asked for with
  another 25% of cells; 16 deletes the mine under every valley. **The tie-breaker between 20 and 24
  is the frame at 4K on Huge** — whether four more layers show up in `World` when the camera looks
  at hills — and `FrameTimeTests.TheBoardDepthAgainstTheFrame` is the experiment, written and waiting
  on disk space to run. If 24 costs the frame nothing measurable, the deeper mine is free and 24 wins.
- **The board's height is measured before it is chosen.** 8 layers of relief on a 16-layer board
  leaves ground at `SizeY − 1 − 3 − 4`. The arms are 16, 20 and 24 tall on all four boards, for tick,
  memory, meshing and frame. The owner picks.
- **`SliceSettings.BelowSurface` becomes per column.** It reads one `surfaceLayer`; with 8 layers of
  relief, a low meadow counts as underground and x-rays the hills beside it away.
- **The surround meets the rim column by column**, not at one `SurfaceLayer`, or the rim shows
  cliffs and gaps. Its wood takes the card LODs (§3).

## 8. Loading, and warm-up

d-16, ranked:

1. **Measure first.** A development player with *Log Shader Compilation* on and the perf trace
   running, ten minutes of play. If no compile lands on a frame over 50 ms after the loading screen,
   no warm-up machinery is built. The editor's cyan placeholder is editor-only and says nothing
   about the player.
2. **If there are hitches:** a `GraphicsStateCollection` traced once on DX11 and once on DX12 and
   committed (check first that nothing in it names licensed assets). It is warmed progressively in
   the loading screen with a progress bar. On DX11 it falls back to `ShaderVariantCollection.WarmUp`,
   which is the supported path there.
3. **Always:** two or three **curtain frames** of the real camera behind the loading screen before it
   lifts, which also uploads the first view's meshes, textures and targets.

The loading screen itself gets named stages and progress over `PrimeAll`, the terrain arrays and the
vegetation meshing. `BuildSession` already stalls there; this makes the wait legible.

## 9. Settings

One owner, `GraphicsLadder`, as `27-graphics-settings.md` requires. New levers: **vegetation
density** (the grass count rung), **grass distance**, **LOD bias**, **foliage shadows**, **wind**,
**terrain detail** (the array size), and **Quality presets** (Low/Medium/High/Ultra, becoming Custom
the moment one lever moves). Each preset is defined by its measured frame at the bar in §1, not by
taste. Nothing polls per frame.

## 10. Atmosphere

- **Meadow water:** Synty's water shader where the pack resolves (so the Synty build keep-alive
  applies), `OdysseyWater` otherwise, re-tuned for the 48° camera (`20-swimming-and-water.md`: at
  that angle Fresnel returns 2.2%).
- **Meadow post-processing** as a Look option, loaded at runtime when present and **never committed**
  (it is licensed), judged against the golden hour by flicking between them.
- **Ambient FX** near the camera focus (butterflies, petals, leaves, dust), drawn only, never in a
  cell, a save or the hash.

## 11. Units

Each unit is its own `claude/*` branch and PR off `main`, ends in a handover, and adds its
measurements here.

| Unit | What | Gate |
|---|---|---|
| **M0** | This document, the interview, four research files. | Owner reads. |
| **M1** | **The six-draws measurement** before any art moves: one run, grass off / on with priming off / on with priming *Auto*, no grass shadows, at 640 × 480 and 3840 × 2160. First give `OdysseyCharacter` a `DepthNormals` pass and check `OdysseyPowerLine`, or priming makes them vanish (d-18). **PR #174 merged first.** | Numbers. |
| **M2** | Instanced LOD (§3) and the derived tallest bound. | Tests; LOD on/off arm. |
| **M3** | `Odyssey/Foliage` (§4). | Tests; keep-alive in a player build. |
| **M4** | Lush grass and flowers on today's ground (§5). | **First Play.** 2.0 ms at 4K. |
| **M5** | Meadow trees and bushes as sim species; the topple; goldens measured; wiki. Confirm fruit-bearing. | **Second Play.** |
| **M6** | Settings and presets (§9). | Each preset measured. |
| **M7** | **Measured 2026-09-24** (§7, `28-map-size.md` §11): no cliffs at ±4; 20 layers recommended over 24, never 16. Frame arm written, not run (disk full). | **Owner picks.** |
| **M8** | Hills worldgen and the per-column slice. | Goldens measured. |
| **M9** | The ground skin and its shader (§6). | **Third Play.** |
| **M10** | The surround continues the skin (§7). | Seam-free at the rim. |
| **M11** | Loading and warm-up (§8). | No compile after hand-over. |
| **M12** | Atmosphere (§10). | Play. |
| **M13** | Loose rocks, mushrooms, berry bushes. Fruit later. | Goldens; wiki. |

M1 is new against the approved plan and comes before any art moves. d-18 found that the project
draws every foliage instance up to six times, so "completely full of grass" is decided by depth
priming and grass shadows before it is decided by clump counts. That measurement is the headroom
every later unit spends.

## 12. What not to undo by tidying

- **The simulation's layers.** Everything smooth is drawing; `TerraceFoot` is the one sim-side copy,
  and it is checked cell by cell against the skin.
- **One matrix per plant.** If a future pack moves an LOD child off its origin, the load-time
  assertion falls back; do not remove the assertion to make a pack load.
- **Wind on the tick**, in one include shared by every pass.
- **No licensed file committed**: the Meadow post-processing profile, a traced state collection that
  names pack shaders, and the pack's textures all stay out.
