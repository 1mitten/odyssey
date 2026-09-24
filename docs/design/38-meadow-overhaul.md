# 38 — The Meadow overhaul: lush grass, Meadow trees, a landscape instead of terraces

*Numbered 38. Written as 36; by the time it merged, `main` had given 36 to radiant heat and
`claude/medical-supplies` had 37, so it moved, with every place that cited it (2026-09-24).*

**Status: designed 2026-09-24; M1 measured (§13), M2 built (§14) and M3 built (§16) the same day.** Branch `claude/meadow-overhaul`, worktree
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
- **Grass is drawn once a frame, not six times.** d-18 predicted that every foliage instance is
  drawn up to six times — the SSAO DepthNormals prepass, the forward pass and four shadow cascades —
  and this document first built its order of work on that. Read against the code it does not hold
  for grass: `ChunkRenderer.FoliageCastsShadows` is off, and foliage is drawn in queue 2501
  (`MaterialCache.FoliageQueue`), just past the opaque range so the outline never inks it, which
  also keeps it out of the opaque-only prepass. **It does hold for trees**, which cast shadows and
  are opaque. §13 has the measurement that replaced the prediction.
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
- **Alpha:** `clip`, in queue 2501 like today's foliage, so grass stays out of the prepass and the
  outline. **Depth priming is dropped** (§13): it only reaches the opaque range, and moving grass
  there bought 0.2 ms at 4K at the price of inked grass. Not alpha-to-coverage.
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
- **Grass casts no shadows and receives them**, as today's foliage already does
  (`ChunkRenderer.FoliageCastsShadows` off); `Odyssey/Foliage` keeps it that way.
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
| **M1** | **Measured 2026-09-24** (§13): what grass costs at 640 × 480 and 3840 × 2160, none / shipped / full cover / full cover in the opaque queue. | Done; the owner's GPU reading agrees (§13). |
| **M2** | **Built 2026-09-24** (§14): instanced LOD, off by default. The tallest-bound constant moves with M5, when a tree first exceeds it. | Done. |
| **M3** | **Built 2026-09-24** (§16): `Odyssey/Foliage` draws the Meadow grass — tick wind, clearance round items and marks, a spring grade. | Awaiting the owner's first look. |
| **M4** | Lush grass and flowers on today's ground (§5). | **First Play.** 2.0 ms at 4K. |
| **M5** | Meadow trees and bushes as sim species; the topple; goldens measured; wiki. Confirm fruit-bearing. | **Second Play.** |
| **M6** | **Built 2026-09-24** (§15): quality presets, the grass ladders, grass shadows; preferences now reach a new session. | Unity tiers owed (disk); owner's look at 4K and on a laptop. |
| **M7** | The board-depth measurement (§7). | **Owner picks.** |
| **M8** | Hills worldgen and the per-column slice. | Goldens measured. |
| **M9** | The ground skin and its shader (§6). | **Third Play.** |
| **M10** | The surround continues the skin (§7). | Seam-free at the rim. |
| **M11** | Loading and warm-up (§8). | No compile after hand-over. |
| **M12** | Atmosphere (§10). | Play. |
| **M13** | Loose rocks, mushrooms, berry bushes. Fruit later. | Goldens; wiki. |

M1 is new against the approved plan and comes before any art moves. It was written to test d-18's
six-draws prediction and found the prediction does not apply to grass (§2, §13); what it measured
instead is the headroom every later unit spends.

## 12. What not to undo by tidying

- **The simulation's layers.** Everything smooth is drawing; `TerraceFoot` is the one sim-side copy,
  and it is checked cell by cell against the skin.
- **One matrix per plant.** If a future pack moves an LOD child off its origin, the load-time
  assertion falls back; do not remove the assertion to make a pack load.
- **Wind on the tick**, in one include shared by every pass.
- **No licensed file committed**: the Meadow post-processing profile, a traced state collection that
  names pack shaders, and the pack's textures all stay out.

## 13. M1: what grass costs, measured (2026-09-24)

`FrameTimeTests.TheGrassAgainstTheFrame`, on the played wooded meadow (Standard, seed 1, culling
on), RTX 5070 Ti on Direct3D 11. One world, eight readings a run: no grass, the shipped density
(60 tufts per hundred grass cells), full cover (300, which is `GroundScatter.MaxPerCell` on every
cell and the most the scatter can place today), and full cover moved into the opaque alpha-test
queue — each at the batch view and with the camera drawing into a 3840 × 2160 target. The run
asserts that every control applied: the instance count moved, a foliage material was re-queued
(`MaterialCache.RequeueFoliage`, returning its count, P18), the camera drew at 4K, and no reading
was taken while the board re-meshed.

| 3840 × 2160, frame vs no grass | Run 1 | Run 2 | Run 3 (inside the full tier) |
|---|---|---|---|
| no grass | 7.39 ms | 7.88 ms | 10.81 ms |
| shipped (~4,800 clumps) | **+1.24** | **+1.11** | **+1.27** |
| full cover (~24,100 clumps) | +1.78 | +0.83 | +1.93 |
| full cover, opaque queue 2450 | +1.60 | +0.64 | −0.58 |

At 640 × 480 the same arms cost +0.12 to +0.31 ms, all of it submission.

**What it says.**

- **The shipped grass costs 1.1–1.3 ms at 4K, steadily, across three runs; full cover costs
  0.8–1.9.** Five times the clumps moved the frame by −0.3 to +0.7 ms over the shipped density, so
  most of the cost is having grass at all, and full cover at today's clump size sits inside d-18's
  2.0 ms budget. "Lush on every tile" is affordable at this size.
- **The frame is GPU-bound at 4K** — submission is 1.6–2.0 ms of a 7.4–9.2 ms frame — so the frame
  time stands in for the GPU time here. `GpuFrameMs` reads **unavailable** in a batch run on
  Direct3D 11, which is why the column is the frame and why one reading is owed from Play (the
  developer overlay does read it; the owner's 4K shots on 2026-09-21 had it at 8–9 ms).
- **The opaque queue was cheaper all three times — by 0.18, 0.19 and 2.51 ms**, the last inside
  the busier full tier. That is the back-to-front sort of the transparent range showing, and its
  size is not settled: small on a quiet machine, large on a busy one. Moving grass there wholesale
  would put it into the DepthNormals prepass, under SSAO and under the ink the queue exists to
  avoid, so **the queue stays at 2501 for now and depth priming is dropped** (it only ever reached
  the opaque range). **If the GPU reading confirms the gap, the lever is to keep grass out of the
  outline by a rendering-layer mask and draw it opaque, front to back** — an M3 decision, taken
  with the timer rather than with this noise.
- **The noise floor is about half a millisecond** on this machine with two other batch runs going
  (a sibling worktree and the CI runner, both runs). Anything finer than that — taller clumps, the
  next rung of density — is decided with the GPU timer in a Play session, not by another batch run.

**The GPU reading, from Play (owner, 2026-09-24, 3840 × 2160, the shipped density).** *"6–7 ms on
gpu (sometimes bit lower) without grass tufts. On — 7 ish — spikes up to 8 moving around — this is
an approximation."* So the shipped grass is **about 0.5–1 ms of GPU, peaking near 1.5 while the
camera moves**, which agrees with the batch arm's frame-time stand-in and sits well under the 2 ms
line the playtest row set. M4 plans for full cover. The opaque-queue question stays with M3: a
reading by eye off a smoothed overlay cannot resolve a 0.2 ms difference.

**What it does not say.** Nothing about Meadow's own clumps drawn by `Odyssey/Foliage` (M3–M4):
taller and broader cards are more fill per instance, which is exactly what this measured as cheap
at today's size. M4 re-runs this arm with its clumps before the first Play, and the arm is built to
take that without change.

## 14. M2: levels of detail, built (2026-09-24)

§3 as designed, with three differences worth recording.

- **Every level the art ships is kept, not three.** e-09 proposed dropping the four-level trees'
  LOD1 to save a bucket, but a bucket here is chosen per chunk and only the chosen level is
  submitted, so a kept level costs memory the prefab reference already holds and not one draw.
- **The bucket is the first part's, not a new kind.** A module drawn by level
  (`ResolvedModule.DrawsByLevel`) is emitted into one bucket keyed as its part 0 always was; since
  every part of every level shares that part's local transform (checked at load,
  `ModuleLibrary.CoarserLevels`), its matrices serve every part of whichever level is drawn.
  `DrawBuckets` loops the level's parts over it. Nothing else that reads buckets had to change.
- **`UseLods` ships off.** The pack's switch heights assume a camera near the ground: at this
  camera's 40° a 1.9 m grass clump leaves its finest level about 25 m out, and the play camera is
  60–160 m away. The arm below confirms it — switched on, 4,403 of the ~4,800 tufts on screen at
  640 × 480 and 5,021 at 4K were drawn at a coarser level. Each unit that brings art turns levels on
  with `LodBias` set against that art and a person looking at it (M4, M5).

**Measured** (`FrameTimeTests.TheLevelsOfDetailAgainstTheFrame`, played meadow, one run):

| | 640 × 480 off / on | 3840 × 2160 off / on |
|---|---|---|
| frame | 2.08 / 2.03 ms | 7.92 / 8.00 ms |
| draw calls | 803 / 803 | 877 / 877 |
| instances at a coarser level | 0 / 4,403 | 0 / 5,021 |

Three modules on today's board resolve with levels — the three Meadow grass tufts; the
PolygonGeneric trees have none. **With levels off the counts are M1's shipped counts to the
instance** (803 calls, 34,428 instances), which is the evidence that drawing a module from one
bucket by level changed nothing about today's picture. The frame does not move either way, as §13
predicts for grass; the saving levels exist for is the Meadow trees in M5.

Guards: `InstancedLodTests` (EditMode) — a prefab resolves into every level placed as the finest,
a level off the origin falls back to the finest alone, a prefab without a group has one level, the
pick follows the screen height by the pack's rule and never culls, and levels are off by default.

## 15. M6: presets and the grass ladders, built (2026-09-24)

`27-graphics-settings.md` §10 has the tab and the table. The decisions:

- **Built from the levers `main` has today.** LOD bias arrives with M4/M5, wind with M3 and terrain
  detail with M9; each joins `SettingsDirector.PresetLadders` (or `PresetOptions`) and the preset
  table when it lands, rather than as a dead row now.
- **The grass-tufts toggle is gone**, replaced by the Grass ladder's Off rung — one owner for how
  grassy the board is.
- **The preset table is derived, not tuned.** High is what ships. Ultra adds the one step up that is
  measured — full cover, 0.8–1.9 ms at 4K (§13). Medium and Low only take things away, so they cannot
  break a bar the tier above holds. Anti-aliasing and grass shadows stay off on every preset because
  nothing has measured them.
- **Owed:** a measured frame per preset at 3840 × 2160 on the owner's machine and at 1080p on an
  RTX 3050/3060 laptop — the Low bar cannot be checked on this machine at all.

## 16. M3: `Odyssey/Foliage`, built (2026-09-24)

*§15 is M6's, written on its own branch at the same time.*

§4 as designed. What is on screen now: **the three Meadow grass tufts on the board are drawn by our
shader**, from the art's own leaf texture, graded lighter and yellower (`FoliageLook.LeafGrade`,
a linear (1.9, 2.1, 1.3) on an art that averages sRGB (88, 112, 48) — a first setting, one number,
for the owner's eye); **they sway on the game clock**, so they hold still on pause and hurry at
speed 3 (`WindDirector`, restored from the closed grass branch, applied beside the daylight); and
**they shrink away round a dropped item (0.55 m) and an order mark (1.1 m)** through the clearance
field (`GrassClearance`, restored), whose window now follows the rig's *focus* rather than the camera.

Decisions worth keeping:

- **Routed by the art, not by the tint.** A material is drawn by ours when it has a filled
  `_Leaf_Texture` slot — the pack foliage shader's own input. The foliage tint also marks crops,
  which are PolygonFarm art with no such slot; keying on the tint drew every carrot as grass once
  already (#166). `MaterialCache.OwnFoliageShader` is the switch, on in the game.
- **Degrades, never vanishes.** If a player strips `Odyssey/Foliage` the cache logs once and the
  pack's own material draws as before M3. It is on `ShaderInclusion.Required`, always-included and
  has a keep-alive, and the player build's log carries no warning.
- **Pawns do not clear grass yet.** The closed branch stamped colonists too; that replaced a static
  bare hole the scatter kept in every cell, which is a scatter change and belongs with M4's density.
- **The canopy fade is a property, not yet wired.** `_Fade` dithers the clip; nothing sets it
  until trees move to this shader in M5, which is where the see-through machinery meets it.
- **The distance shrink ships off** (`_ShrinkStart`/`_ShrinkEnd` zero); M4 turns it on with the
  rank thinning.

**Measured** (`FrameTimeTests.TheFoliageShaderAgainstThePacks`, one run, the played meadow, the
pack's material against ours on the same meshes, clones dropped between arms and asserted dropped):

| | 640 × 480 pack / ours | 3840 × 2160 pack / ours |
|---|---|---|
| shipped density (60) | 2.35 / 2.43 ms | **9.53 / 8.86 ms** |
| full cover (300) | 2.36 / 2.48 ms | **9.68 / 9.16 ms** |

**Ours is 0.5–0.7 ms cheaper at 4K** and within 0.1 ms at the batch view, where the frame is
submission rather than fill. Draw calls and instances are identical in every pair, as they must be.
The pack's graph computes three noise colours and a frosting term per pixel; ours samples one
texture and does the wind in the vertex stage. Same caveat as §13: other batch runs shared the
machine, so only the in-run pairs mean anything.

## 17. The look pass (owner, 2026-09-24)

The owner, shown M3 and M6: *"I said the grass to be lush — and I expected it to look the
screenshots from the synty pack … as currently it looks nothing like it."* Decisions the same day:
the target is Synty's screenshot #13 **at the play camera**; scenery first, simulation after;
**Synty's own colours**; Ultra matches the screenshot and High holds 60 fps at 4K. The pass is two
halves on two branches, integrated into one: *dressing* (what stands on the ground) and *ground and
light*. `FrameTimeTests.TheLookAtThePlayCamera` photographs the played meadow for both.

### 17a. Ground and light (`claude/meadow-look-ground`)

**What the ground was.** One instanced box per cell wearing `Mat_Grass_Textures_01`, one texture
repeat per 2.5 m cell, multiplied by `StuffPalette`'s grass lift (1.04, 1.30, 1.55): a single olive
texture pushed towards a turquoise-lime it was never painted as, tiled into a visible lattice.

**What it is.** `Odyssey/MeadowGround`, a clean-room URP shader, draws every grass cell. It blends six
of the pack's terrain textures — two grasses, clover, flowers, leaf litter, and earth on faces that
stand up — by value noise in **world space**, at a 4 m texture repeat (the pack's own terrain-layer
tiling) and patches tens of metres across, with a slow drift of brightness and warmth. Neighbouring
cells are continuous by construction: they sample the same world. The coarser layers branch, and
their gradients are taken outside the branches (d-17 §4). Nothing about it is simulated — no cell, no
save, no hash — and the renderer's tints (depth shade, tilled, stored, the zone washes) still
multiply in through `_BaseColor`. The grass lift is dropped over it (`StuffPalette.TerrainTint`).

**Where the art comes from.** `MeadowLook`, a committed `Resources` asset of GUID references into the
gitignored packs, rebuilt by `MeadowLookBuilder` (menu, or `scripts/unity.sh exec
Odyssey.EditorTools.MeadowLookBuilder.Build`), which refuses to write without the packs. A clone
without them resolves nothing and draws the stock ground exactly as before.

**The light, and why it had to move.** The first painted frame was *darker* than the stock one: the
pack paints its terrain muted olive, and its screenshots are bright because of how the demo scene
lights them — an orange key at intensity 3 over a heavy trilight ambient (sky 0.62/0.84/1.30,
equator 0.71/0.84/0.88, ground 0.69/0.60/0.42) at an ambient intensity of 1.6. Our noon had a
near-white key at 2.15 over an ambient a third as bright. `Daylight.Meadow` moves the sampled state
towards the demo's **by daylight only** — the weight is the sun's elevation over 30°, so dawn, dusk
and night keep the palettes the owner judged — warming the key and scaling it ×1.35, and taking the
three ambient terms 85 % of the way to the demo's (its 1.6 folded in as ×1.5). It is a transform of
the sampled state, not new keys, so the table and `DaylightTests` are untouched, and
`Daylight.MeadowLight` off gives exactly the old light. On by default.

**The grade: ours, not the demo's.** The demo's own URP profile is loaded at runtime from
`MeadowLook.grade` for comparison only. It pulls the grass yellower, which is towards #13, but its
bloom (threshold 0.81, intensity 2.19, a lens-dirt texture at 8.07) haloes every white thing — the
colonists and stone glow — and it costs **~8 ms at 4K**. The golden-hour grade stays. (The pack's
`Meadows_Post_Processing_01` is Post Processing v2, built-in pipeline only, and cannot be used.)

**Measured** (`FrameTimeTests.TheMeadowGroundAgainstTheFrame`, played meadow, one run, RTX 5070 Ti):

| | 640 × 480 | 3840 × 2160 |
|---|---|---|
| stock ground, golden hour | 2.34 ms | 13.26 ms |
| painted ground, golden hour | 2.43 ms | **11.92 ms** |
| painted ground, demo grade | 3.69 ms | 20.00 ms |

The painted ground is **no dearer within the noise** (a single run's noise here is about a
millisecond, and it came in under the stock ground). No `Texture2DArray` was needed for the first cut:
six slots, at most five samples on a patch edge and two on plain grass.

**Pictures**: `docs/reference/screenshots/look/ground-before-start.jpg` (stock ground, the Play
scene's grade), `ground-after-start.jpg`, `ground-after-close.jpg`, and
`ground-after-close-demo-grade.jpg` for the grade comparison.

**Owed.**
- **Leaf litter under trees** is on noise, not on where trees stand: the mirror knows the tree
  cells, and a cheap signal (a per-cell weight in vertex colour or a small board texture) would put
  the litter where the canopy is. Not built, recorded.
- **A terrain-detail rung** in M6's presets waits for texture arrays, which the first cut did not need.
- **The dirt, gravel and marsh terrains** keep their tiled textures; they sit oddly beside a painted
  meadow only where they meet it, which is the M9 skin's business.
- **The day's other hours** under the Meadow light are unphotographed: the harness shoots noon.
- The teal tufts in every picture are the M3 grass shader's grade on one tuft variant, not the
  ground — the dressing half owns them.

### 17b. Dressing (`claude/meadow-look-dressing`)

**Why.** The owner's first look at M3's grass: *"I said the grass to be lush — and I expected it to
look [like] the screenshots from the Synty pack … as currently it looks nothing like it."* The
reference is Synty's own screenshot of the Meadow demo from above (their #13), and read carefully it
is not a grass setting: it is **bushes everywhere, Meadow trees in stands, grass in tall mats rather
than tufts, flowers in sweeps, stones, painted ground, and warm light**. Decided with the owner the
same day: target #13 at the play camera; **scenery first, simulation after** (bushes, flowers and
stones drawn only; the two tree species keep their simulation and wear Meadow art); **Synty's own
colours**; **Ultra is the screenshot**, High holds 60 fps at 4K. This section is the half that stands
on the ground; the ground's paint and the light are the other half (`claude/meadow-look-ground`).

**The instrument came first.** `FrameTimeTests.TheLookAtThePlayCamera` (explicit, never in a tier)
photographs the played meadow at 1920 × 1080 from the camera a new game opens with, closer in, and
pulled back to about the reference's framing (`Logs/look/start|close|wide.png`). Every decision
below was taken by looking at those, not by reasoning about a shader. Before and after are in
`docs/reference/screenshots/look/`.

**What changed, and why each.**

- **The teal tufts** were two of the three foliage tints — red ×0.55/0.45, blue ×2.2/1.8, tuned
  against the pack's own straw-coloured shader — multiplied straight onto the art by
  `Odyssey/Foliage`. The tints are neutral now, a whisper of variety and no more.
- **The art's own colour, read at runtime.** The pack does not colour a leaf from its texture: with
  its flat-colour switch on (grass and most trees), a leaf takes a base colour at the root rising to
  two world-noise colours at the tip, with a frosting colour on sunlit tops. `FoliageLook` now reads
  those values off the art material at runtime — exactly as it reads the textures, never into this
  repository — and `Odyssey/Foliage` applies them with its own noise, keyed on the vertex colour's
  height gradient. The birches' autumn is the art's own. M3's lime grade is neutral (`LeafGrade`).
- **Meadow trees.** The conifer slot is Birch 01–03, the broadleaf slot Meadow_02 and Fruit 01–03 with
  the fifteen-metre Meadow_01 as a rare fifth (one in forty), chosen per cell by hash with a turn and
  a size of their own (`ChunkMesher.EmitTree`, `MeadowDressing.TreeVariant`). They are drawn by level
  (M2) on a bias of their own (`TreeLevels`, `TreeLodBias` 3), by our shader, **late in the foliage
  queue**: in the opaque queue the outline inked every cut-out leaf and a crown read as a black
  scribble. Their normals are only lightly pulled up (`TreeNormalUp` 0.2) so a crown has a lit and a
  shaded side. `ChunkRenderer.TallestModuleMetres` is measured from what resolved, never under 12.
- **The dressing** (`MeadowDressing`, `ChunkMesher.EmitDressing`): tall-grass mats (Tall_Clump_04/05)
  on a checkerboard, bushes (Bush_01–03) on one cell in four and gathered at wood edges, wildflowers,
  ground cover, the odd sunflower, and stones gathered by rock — each kind dense where its own
  low-frequency noise field is high, so it reads as stands and sweeps rather than confetti. Pure
  functions of the cell, drawn only, in no save or hash. Never on a cell with a floor, a building, a
  zone or anything solid on it; the big pieces also need a clear ring of neighbours; bushes and
  stones keep out of a four-cell clearing round where the colony started. All of it scales with M6's
  grass ladder: Off is none, Meadow the shipped rung, Full is Ultra.
- **Grass parts round what lies in it, per blade.** M3 cleared a clump by sampling its root; a
  six-metre mat hid a log two metres off its root. The clearance is now asked where each vertex is,
  and blades near an item or a mark lie flat.
- **Tried and dropped, by looking:** the flat flower cards (read as lilac pebbles from above) and
  the small pebble piles (lilac confetti).

**Measured** (`FrameTimeTests.TheDressingAgainstTheFrame`, 3840 × 2160, RTX 5070 Ti, the URP asset's
own 250 m shadow distance — M6's High preset uses 120 m — with a CI runner and an idle editor on the
machine; in-run differences only):

| Standard | Frame | Calls |
|---|---|---|
| grass Off (Meadow trees, no dressing) | 11.40 ms | 1,394 |
| **Meadow, the shipped rung (High)** | **13.36 ms** | 2,065 |
| — casting from the finest level | 14.84 ms | 1,793 |
| — with the bushes casting | 13.70 ms | 2,065 |
| — no shadow casters at all | 9.96 ms | 770 |
| Full (Ultra) | 14.58 ms | 2,135 |
| **Huge**: Off / Meadow / Full | 14.27 / **19.10** / 19.04 ms | 2,277 / 3,489 / 3,627 |

- **Shadows were the money.** An earlier, noisier run put the Meadow rung 7 ms over "no casters";
  the Meadow trees and bushes were drawing their finest meshes into four cascades. **A tree now casts
  from a proxy** — the last mesh before its card, shadows only (`TreeShadowProxy`) — which is worth
  1.5 ms here, and **bushes cast no shadow** (`DressingCastsShadows`, a further 0.3 ms). Tree level of
  detail for the *drawn* mesh barely moved the frame (−0.1 ms at bias 1 against 3).
- **The dressing costs about 2 ms at High and 3.2 at Full on Standard**; the Meadow trees themselves
  about 3.5 over M1's old-tree baseline. **Standard High is under the 16.7 ms line; Huge High is over
  it in this run**, at 250 m of shadow and with other Unity processes on the machine. The owner's
  GPU reading at 4K on High decides it (playtest queue).

**Owed.**

- **Colour balance with the light.** Under today's golden-hour sun the art's colours read dark and
  olive against a bright lime ground; the reference is lit by a warm sun at intensity 3 with a
  contrast and saturation lift. That is the ground-and-light half, judged together at integration.
- **Bushes are scenery**: colonists walk through them. They fade when they stand between the camera
  and a colonist (the tree path's sight fade), but they are not obstacles; making them simulated
  things, with berries, is the owner's later unit, with loose stones and mushrooms (§1).
- **The clearing is where the colony started**, not where it has since built; bushes and stones
  are kept off anything built, zoned or floored, but not off an order mark or a stockpile's
  neighbourhood beyond one cell.
- **Huge at High** needs the owner's GPU reading before the High preset is signed off.
