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
| **M9** | **Built 2026-09-24** (§20): the ground skin — ramps, flat tops, stream banks and an apron to the surround — on `claude/meadow-skin`. The ground keeps the `MeadowGround` shader. | **Third Play.** |
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

### 17c. The owner's first look, and three fixes (2026-09-24)

Branch `claude/meadow-look-fixes`, on the merged look. The owner, having played it: *"It's a step in
the right direction visually but a few things"* — the leaves still blocked the view of a colonist
behind a tree while the bark faded; the lines on the terraced tiles should go, *"as it's starting to
look more seamless"*; more variety in the leaf colour; and performance (a separate unit). Asked and
answered the same day: leaves **nearly invisible, about 15%**, over **every colonist**; ink off
**terrain only**; leaf colour **like #13, mixed stands**.

Photographs: `docs/reference/screenshots/look/fixes-before-wide.jpg` and `fixes-after-wide.jpg`, the
terrace crops `fixes-before-terrace-crop.jpg` / `fixes-after-terrace-crop.jpg`,
`fixes-after-colonists-crop.jpg` (the colonists and piles keep their ink), and the fade,
`fixes-after-wide-faded.jpg` (every crown drawn as though a colonist stood behind it).

**The leaves: a ghost, not a dither and not the stand-in.** The bark faded and the leaves did not
because a Meadow crown in a sight line was handed to the translucent stand-in — which draws each
leaf card as a whole pane, so forty overlapping panes at 22% added back up to a solid crown. The
first fix drove `Odyssey/Foliage`'s own `_Fade` dither at 15%, and photographed it did the opposite
wrong thing: a sparse 4 × 4 screen-door grid of dots, legible as nothing at the play camera. What
shipped is a **two-pass ghost**: the faded crown's material enables a depth-only pass
(`FoliageGhostDepth`, LightMode `SRPDefaultUnlit`, which URP draws before `UniversalForward` for the
same object) and blends its colour pass at `SightLeafFade` (0.15) over it, in the transparent queue.
The depth pass means only the front-most leaf card survives the depth test, so the crown fades to one
faint layer rather than forty. Every other foliage material has that pass switched off
(`Material.SetShaderPassEnabled`, inherited from the base material), so a solid crown pays nothing.
Trunk and leaves share the material, so a tree fades as one thing; its shadow still falls. Bushes
take the same path. `ChunkRenderer.FadeEveryTreeForAPhotograph` exists only for the photograph.

**No ink on terrain: a mark in the normals texture, not a stencil.** The outline is one full-screen
pass over the depth copy. A stencil would need the depth attachment the opaques drew with bound to
that pass and every terrain material to write a stencil reference; instead the ground writes a
**terrain mark into the spare alpha channel of the DepthNormals prepass** (which already runs every
frame for SSAO), and the outline returns the scene untouched where the centre pixel is marked. It is
one extra texture read a pixel and, on most of the screen, *less* work, because the rest of the
detector is skipped. It is correct for the reason the ink is one-sided: the line lands on the near
side of a step, and the near side of a terrace riser or a stream bank is the ground itself, while a
colonist standing on the ground overwrites the mark with its own 0.

Three things had to be true for that, and each is now asserted (`LookFixesTests`):

- **Every natural terrain is drawn by `Odyssey/MeadowGround`**, which gained a single-texture mode
  (`_Single`) — earth, gravel, mud, marsh, sand. Only this shader writes the mark; a sand bed left on
  the pack's material kept a black line along every step of a stream.
- **Stone is the exception, deliberately.** Through the ground shader a rock outcrop's chipped lumps
  drew a saturated blue (the stone texture is grey; the cause was not chased, because the decision
  below makes it moot), and an outcrop is a thing on the meadow rather than a step of it — its
  outline is what keeps it reading as rock. Rock, bedrock and the ore seams keep the pack's material
  and their ink.
- **Everything that keeps its ink must be in the prepass.** A colonist was not (`OdysseyCharacter` had
  no DepthNormals pass, which d-18 had already noticed), so it would have stood on the ground's mark
  and lost its line. It has one now.

**Leaf colour: dealt in the shader from where the tree stands.** No colour is uploaded per tree:
`FoliageStandColour` deals each instance a family from a ~50 m noise field (the stands) and the
tree's own hash — about 70% greens (two greens and a lime), 27% gold and orange, 3% red — and
`FoliageRecolour` moves the leaf to that hue while keeping half the art's own light and dark, so the
texture's detail survives. Photographed and tuned twice: at the art's full lightness an orange dealt
to a dark crown read as brown, and when the greens replaced 60% of the art's colour the fruit trees'
own orange disappeared and the meadow had *fewer* colours than before; the greens now take 30–45%,
autumn takes it all. Bushes take less than trees (`BushStandVariety` 0.45). `LeafVariety` mirrors the
rule in C# so the distribution can be counted, and a test reads the constants back out of the shader.

**Cost.** Draw calls are unchanged (Standard at the Meadow rung: 2,065 calls, 45,875 instances,
before and after). The ghost costs a depth pass for the faded instances only; the ink mark is one
read a pixel and skips the detector on terrain. **Neither was timed as its own arm** — the in-run
4K dressing arm is within the noise of the look's own, and the performance unit running beside this
one owns the frame.

**Owed.** A playtest of all three; whether 15% is too faint when a colonist is *inside* a canopy
rather than behind it; the blue stone through the ground shader, if stone is ever moved onto it.

## 18. Performance of the look (2026-09-24)

The owner, after the look pass: *"We need to make this as performant as possible … the compute is up
to 5 ms."* Research and measurement are `docs/research/d-19-submission-at-scale.md`; the owner
approved its recommendations 1–3 and recorded 4. Branch `claude/meadow-perf`, worktree
`D:\code\odyssey-cull`. All numbers are Standard, the played meadow, RTX 5070 Ti, Direct3D 11, and
only differences inside one run are quoted (§6c).

### 18a. The shadow margin sweeps towards the sun (built)

**What was wrong.** A chunk off screen is kept if its casters can shadow the view, and the cull
asked that by growing every chunk's box by the whole shadow distance **in every direction**. At the
pipeline's 250 m that kept every chunk of a Standard board — 104 submitted for 26 on screen — and
d-19 measured shadows as the largest term in the 4K frame, about 4.3 ms.

**What it is.** A caster can only darken what lies along the light's path from it. So a chunk's box
is **swept along the key light's direction** (`ChunkRenderer.ShadowLightDirection`, written by the
root from the key light the day moves) and kept only if the sweep reaches the frustum
(`ChunkRenderer.SweptInside`: outside a plane only if both ends of the sweep are). The sweep is as
long as the light can travel before it drops below the lowest drawn layer — the drop from the
chunk's tallest possible top over the sine of the sun's elevation — and never longer than the shadow
distance, the old bound, which a low sun therefore still reaches. With no key light the old shell
is kept.

**The finding the proof made.** The first version passed at noon and moved **80 pixels at 19.5 h**,
all at one screen edge: a Meadow crown up to 17 m wide overhangs its chunk's box by far more than the
box's 2 m padding, the old shell had covered the overhang by accident, and the sweep started from the
box. The box now grows sideways by the widest resolved module's reach
(`ChunkMesher.OverhangResolved`: turned, at its largest scale, jittered), for the picture as well as
the shadow.

| One run, 3840 × 2160 | shell | sweep |
|---|---|---|
| chunks submitted | 104 | **42** |
| draw calls | 2,852 | **1,175** |
| submit | 2.92 ms | **1.42 ms** |
| frame | 12.81 ms | **11.23 ms** |

At 640 × 480, where the frame is the CPU: 3.91 → 1.85 ms. The proof
(`FrameTimeTests.CullingDoesNotChangeThePicture`) now shoots noon **and a low sun at 19.5 h** (about
nine degrees, the longest shadows the day has, held by `OdysseyBootstrap.DaylightHourOverride`
because the root re-applies the hour every frame, P18): 0.00% against a 0.00% floor at both, blind
control 98.20%. `ShadowSweepTests` pins the geometry: up-sun kept, down-sun dropped, too short
dropped, straight through kept. `SweepShadowMargin` off gives the shell, for measuring.

### 18b. Indirect drawing: the tie-breaker (built, off by default)

**What it is.** `IndirectFoliage`: the grass tufts of every meshed chunk gathered into one GPU buffer
per (module, tint, layer), a compute pass (`Resources/OdysseyCompute/OdysseyIndirectCull.compute`)
keeping each clump whose bounding sphere is inside the frustum and within the grass distance, and one
`Graphics.RenderMeshIndirect` per part drawing what it kept. `Odyssey/Foliage` reads its matrix from
the buffer under a local keyword, `ODYSSEY_INDIRECT`, through two wrappers every transform in the
shader already goes through, so the two paths cannot drift. The material is the chunk path's own for
that layer's shade, cloned once with the keyword. Buffers are regathered when a chunk re-meshes.

**What it measured.** Picture-exact — `TheIndirectTuftsDoNotChangeThePicture`: 0.00% against a
0.00% floor, and taking the grass away moved 23.6%, so the grass really is in the shots. **And no
saving a stopwatch can see**, because after 18a the tufts are 67 of the 1,175 calls on screen:
1,175 → 1,123 calls (15 of them indirect), submit 1.42 → 1.30 → 1.61 ms across on / off / on again
at 640 × 480, i.e. inside the noise. d-19's "722 tuft buckets" had counted every foliage-tinted
bucket, and most of those are the dressing's grass stands, flowers and cover, not tufts. Another
session's batch run and the CI runner shared the machine; the 4K rows of that run are not quotable.

**So it ships off** (`ChunkRenderer.IndirectTufts`), and the tie-breaker is answered as far as tufts
can answer it: the mechanism works and costs nothing in the picture; whether it pays is a question
for the calls that are actually there. **Next:** the dressing's foliage-tinted kinds (grass stands,
wildflowers, ground cover, sunflowers) join the same path — same shader, never fade, never cast —
once `claude/meadow-look-fixes` (the leaf fade and colour) has landed; then trees, which need the
sight fade and per-instance colour carried in the buffer.

### 18e. The player benchmark: real GPU time, arm by arm (2026-09-24)

Every GPU figure before this was the frame standing in for the GPU, because `FrameTimingManager`
reads nothing in a batch editor on Direct3D 11. `PlayerBench` runs inside a **development player**
(`Build/Win64/Odyssey.exe -odyssey-newgame -odyssey-bench -screen-fullscreen 1 -screen-width 3840
-screen-height 2160 -logFile <path>`), builds the Standard meadow, and times arms in one run: the
GPU's own frame time, CPU submission, frame and draw calls, then writes the table and quits. **It
takes the screen**, so it is guarded three ways — every step under a try/catch that logs and quits,
a 3-minute watchdog, and a thread timer that kills the process after that. The first attempt had
none of them, died on an unsupported profiler option, and sat fullscreen over the owner's work for
six minutes; run it only with the owner's say-so.

One run, 2026-09-24, RTX 5070 Ti, DX11, 3840 × 2160, render scale 1, no MSAA, the player's stored
settings (shadow distance 40 m, four cascades, grass 60), after 18a. 66 s, quit on its own.

| arm | GPU ms | vs look | submit ms | draw calls |
|---|---|---|---|---|
| look as shipped | 8.69 | | 1.07 | 1,175 |
| look again (drift control) | 8.32 | | 1.04 | 1,175 |
| **no shadow casters** | 6.44 | **−2.1** | 0.79 | 871 |
| no dressing | 7.22 | −1.3 | 0.85 | 911 |
| dressing and tufts at their coarsest level | 7.51 | −1.0 | 1.03 | 1,175 |
| golden grade off | 7.64 | −0.9 | 1.05 | 1,175 |
| no tufts | 7.80 | −0.7 | 1.01 | 1,108 |
| no dressing or tufts | 7.45 | −1.0 | 0.79 | 844 |
| stock ground (new session) | 7.49 | −0.7 against the next row | 0.97 | 1,175 |
| look (new session) | 8.20 | | 0.97 | 1,175 |

"vs look" is against the mean of the two look rows (8.5 ms); the drift between them, **~0.4 ms**,
is the resolution of this table. The per-pass split was attempted and recorded nothing: the render
markers that accept a GPU recorder here are CPU-side names (`Shadows.*`, `CommandBuffer.*`), and
none returned a GPU time on DX11. A RenderDoc capture is the instrument for the pass split.

**What it ranks.**

1. **Shadows, ~2.1 ms of GPU** — the largest term even after 18a, and it is the casting itself now,
   not the margin (the calls fall only 1,175 → 871). The levers are the casters (the tree shadow
   proxy's level, bushes already off) and the cascades (four at a 40 m distance).
2. **The dressing's fill, ~1.0–1.3 ms** — and drawing it at its coarsest level recovers ~1.0 of
   that, so **levels of detail on the dressing (18c) are the next unit**, measured, not guessed.
3. **The golden grade, ~0.9 ms** and **the painted ground, ~0.7 ms** — real, and each a look
   decision the owner made; recorded, not touched.
4. **CPU submission is ~1 ms now**, not the ~2.5–3 of d-19: 18a took most of it. The dressing's
   share of submission is ~0.2 ms. **So converting the dressing and trees to indirect drawing (18b)
   would buy ~0.2 ms of CPU and nothing on the GPU**, where the frame is bound. It is not
   recommended ahead of 18c and the shadow casters.

### 18c. Levels of detail on the dressing (built; bushes were the wrong pack)

**The bushes were Battle Royale's.** The first sweep showed no bias moving a bush at all, and the
library said why: the three bush rows resolved with one level and no LOD group. The dressing rows
find their prefab by name, `SM_Env_Bush_01` is in Battle Royale, Western Frontier and Meadow Forest,
and the lookup took whichever path sorts first — `PolygonBattleRoyale`. The same took `Rock_01` and
`Rock_02`. A catalogue row now names its pack (`ModuleEntry.prefabUnder`; the Meadow dressing rows
say `PNB_Meadow_Forest`) and `PlayScene.FindSyntyPrefab` looks there first. The five references are
patched in `ModuleCatalogue.asset` by hand rather than by a rebuild, because a plain rebuild also
drops the probed `skin` swatches (≈2,800 lines). **So the meadow's bushes and stones are now
Meadow's own** — foliage bushes through `Odyssey/Foliage` — and that is a look change the owner
judges. `ResolvedModule.LevelNote` now says why any module that has a LOD group draws its finest
level only.

**The levels.** `DressingLevels` (on) and `DressingLodBias` for the grass stands, wildflowers,
sunflowers and ground cover; `BushLodBias` apart from the trees'. The tufts are left at their finest.
Tuned by `FrameTimeTests.TheDressingLevelsAtThePlayCamera`, which photographs the start, close and
wide framings, paused, at each bias against levels off:

| grass stands and flowers | start | close | wide |
|---|---|---|---|
| bias 8 | 0.00% | 0.12% | 0.21% |
| bias 4 | 0.29% | 0.40% | 0.44% |
| bias 2 | 0.29% | 0.40% | 6.44% |
| bias 1 | 2.13% | 1.02% | 9.89% |

At bias 4 the nearest flowers' stems visibly simplify; **bias 8** leaves the near meadow as it was
(`docs/reference/screenshots/look/2026-09-24-perf-lod-close-off-8-4.png`: off, 8, 4). Bushes:
**bias 1.5** moves 0.00–0.16% at every framing (0.75 moved 0.57% of the wide view). The frame is in
§18g.

### 18f. Three shadow reductions, the owner's to try (built, awaiting the owner's eye)

1. **Trees cast from their simplest level**, the card, rather than the last mesh before it
   (`ChunkRenderer.TreeShadowFromSimplest`, on).
2. **Two shadow cascades** instead of four, on the runtime copy of the pipeline asset
   (`DisplaySettingsApplier.ShadowCascades`); **`PC_RPAsset.asset` is untouched** and still says four.
3. **Nothing small casts**: grass stands, flowers and tufts never did (foliage), bushes did not
   (dressing); the Meadow stones did, and are dressing-tinted now so they do not.

`FrameTimeTests.TheShadowChangesAtThePlayCamera` photographs noon and 19.5 h at the start and wide
framings, shipped against each change undone and against all undone: noon 0.35–0.62% of pixels,
evening 0.00–0.04% — no seam and no visible difference at these framings
(`2026-09-24-perf-shadow-{wide-noon,start-evening}-{shipped,before}.png`). The owner judges.

### 18d. BatchRendererGroup (recorded, not built)

The long-term route if the whole world should become GPU-resident (d-19 §2a): persistent instance
data, SRP Batcher draws with no per-call C#, but every world shader needs a `DOTS_INSTANCING_ON`
variant, Project Settings must keep BRG variants and URP must stop stripping unused ones — the
setting whose flip once took one pass to 884,736 variants — and its DX11 player cost is unmeasured.
The owner's call: record it, do not build it.

### 18g. The bench after 18c and 18f (2026-09-24) — only half of it usable

One run at 4K, quit on its own in 89 s. **The bench did not pause the colony or hold the hour**, and
the run shows it: the two "look" arms a minute apart read 6.57 and 9.27 ms of GPU, the calls rose
1,169 → 1,366, and "no shadow casters" read slower than shipped. Only the first four arms, back to
back inside ~40 s, are worth quoting:

| arm | GPU ms | draw calls |
|---|---|---|
| look as shipped (18c, 18f on) | **6.57** | 1,169 |
| trees cast from the old proxy level | 6.90 | 1,200 |
| four cascades | 6.86 | 1,169 |
| **before 18c and 18f** (finest dressing, old proxy, four cascades) | **7.74** | 1,284 |

So 18c and 18f together are **about 1.2 ms of GPU at 4K**, the proxy and the cascades about 0.3 each,
and the rest the dressing's levels — to be confirmed. `PlayerBench` now pauses the world and holds
noon for every arm (`Still`); one more run is owed, with the owner's go.

## 19. Polish: flattened grass, bushes that fade, every colonist, and the wooded surround (2026-09-24)

The owner, after playing the look and its performance round: *"when someone has fallen to the ground
or place item, would it be ok to flatten grass so items can be seen clearer, also there is no trees
in the surrounding landscape, happy to put some random cheap trees for effect as it looks a touch
barer."* Built on `claude/meadow-look-polish` (from `claude/meadow-perf`).

### 19a. The grass lies flat round what is on the ground

- **An item's ring is its footprint plus half a metre** (`ChunkRenderer.ItemRing`, `ItemMargin`),
  never less than the old 0.55 m. A heap's footprint is its recipe's spread plus 0.3 m; a single
  prop's is its module's half-diagonal. A thing on a shelf keeps the small ring — it is off the ground.
- **A body on the ground clears 1.4 m** (`LyingClearance`): a colonist downed, asleep somewhere
  that is not a bed (`WorldRenderModel.BedHeadAt`), or dead (`snapshot.Corpses`). One rule for all
  three, `ChunkRenderer.LiesOnTheGround`, stamped by `StampLying` into the same clearance field.
- **A bush cannot lie flat, so it fades.** The mesher records each bush's disc on its chunk
  (`ChunkBatch.BushDiscs`), the renderer answers `UnderBush`, and the root gives what lies under one —
  a body, a corpse, a loose item — a line of sight, so the bush ghosts through the existing pass.
- Presentation only: nothing here is in a cell, a save or the hash.

### 19b. See-through for every colonist, and what it cost

The owner decided trees fade for *every* colonist (§17c); `UpdateSightLines` still drew lines to
the selection only. It now draws, in order: up to 8 selected colonists, then the **16 colonists
nearest the camera's focus**, then the **16 nearest things under a bush** — bounded, so the cost is
flat whatever the colony. Lines aim at mid height, so the turf under a body is never ghosted.

**The first cut cost 1.9 ms of `World` at 4K with fifty colonists**, almost all of it testing every
ground box in the touched chunks against thirty-two lines. `SightLines.Primary` now marks the
selected lines: only those fade walls, rock and storeys, as before; the rest fade **trees and bushes
only**, which is what they are for. Measured in one run (`TheWoodedSurroundAgainstTheFrame`, 50
colonists, 3840 x 2160): `World` 1.73–1.93 ms selected-only against 2.02–2.03 every colonist, so
**+0.1–0.3 ms**, 16 lines, the sight section 0.02 ms. Photographed: an unselected colonist behind a
birch stand is hidden before and plain after (`2026-09-24-polish-tree-*.png`).

### 19c. The surround

**It was not bare in our photographs.** At the camera's farthest pull over the board's corner the
surround already carried 3,184 near and 2,577 far trees (`2026-09-24-polish-horizon-before.png`). What
it lacked against the board was **undergrowth and depth**: trees on bare lawn, thinning to 15% by
90 m. So, cheaply:

- the near wood thins only to **45%** (was 15%), the far wood runs **50% → 10%** (was 30% → 7%);
- **a bush beside three trees in four** in the near wood, batched by place, never casting;
- the near wood draws **its third level of detail** and the far wood **the card**, which is design
  §3's "the surround's wood takes the card LODs", never built until now.

Measured in one run at 3840 x 2160 over the rim: surround calls **266 → 297**, trees 5,761 → 7,862
plus 1,427 bushes, the surround's CPU section **flat** (0.34–0.40 → 0.33–0.36 ms), and the frame no
worse within this machine's noise (two other sessions' Unity runs were live). The coarser levels pay
for the extra wood. **If the owner still sees a bare surround, the first thing to check is Settings →
Graphics → Surround**, which the Low preset switches off, and then a screenshot — our photographs do
not reproduce "no trees".

### 19d. What is owed

- The owner's eye on all three, especially whether 1.4 m is enough round a body and whether the
  surround now reads wooded at his camera.
- `LiesOnTheGround` decides "a bed" by the bed's head cell; a body in a bed's foot cell reads as
  lying on the ground and flattens a ring round the bed. Harmless (the ring is under the bed), noted.
- The brown line along the board's rim in the horizon shots is the ground skin's edge (M9), not
  this work.

## 20. M9: the ground skin, built (2026-09-24)

Branch `claude/meadow-skin`, worktree `D:\code\odyssey-meadow-skin`. §6 as designed, with the
deviations below, and three things §6 did not foresee: the draw path, the stream banks, and the rim.

### 20a. What it is

- **Ramps.** A terrace's foot cell draws a ramp in a per-chunk mesh (`GroundSkinMesh`, owned by its
  `ChunkBatch`), its corners set by one rule (`BankLayout.RampCorners`): a corner rises to the rim
  where any of the three cells meeting it is a step. The three old bank shapes are its special cases —
  straight lifts two corners, an inner corner three (max(u, v)), an outer corner one (min(u, v)) — and
  neighbouring ramps meet exactly, because they ask the same cells about the corner they share.
- **Flat tops.** Earth whose only visible face is its top, with nothing built on it (a tree is not
  built), leaves its instanced box for the same mesh. Anything with a side showing, under a slab or a
  building, or with a cave under it keeps its box: that is §6's "levelling under built things", taken
  as *keep the drape* rather than as new levelled vertices plus skirts — the box already matches the
  slab's drape to the millimetre, and a skin chord would part from it by up to ~15 mm.
- **Stream banks** (not in §6). A bank one layer above its bed — earth with air over it whose every
  open side is water — drops its water-side corners to just above the water line
  (`BankLayout.BankDips`, `WaterBankDrop` 0.79 m), drawn as skin with walls down to the bed under the
  water, so the meadow runs into the stream instead of ending in a square rim. **Air over it** is part
  of the rule: the bed under a cascade's upper stretch is earth beside water too, and dipping it pulled
  the water above it down (found by `SlicePickerBoardTests`).
- **The apron** (not in §6). Every skinned rim cell runs `ChunkMesher.ApronMetres` (5 m) past the
  board's edge, sloping from its own edge to the surround's ground at `TerrainSkirt.SurfaceLayer`,
  corners filled. The brown line round the board seen from far out was the surround's deep tiles'
  earth sides showing at a one-layer step; the rim boxes had hidden some of it and the skin, having
  removed them, showed all of it. Photographed: no line (`2026-09-24-skin-after-horizon.png`).
- **The one surface owner.** `BankLayout.RiseAt` reads the ramp or the dip back through the same
  corners and triangulation (`GroundCorners`). Grass tufts and dressing (a one-cell cache in the
  mesher), items and heaps (`ChunkRenderer.OnGround`), the picker (`SlicePicker`, at the cell's centre)
  and the ramp cursor (`OdysseyBootstrap`) now stand on it, beside the pawn pose and the water line that
  already asked it. `GroundSkinTests.TheHeightOfASlopeHasOneOwner` fails on a new reader of the old
  per-shape function outside the owner.
- **P15 fixed on the render side.** A cell edit re-meshes every chunk beneath it in its column
  (`WorldRenderModel.RefreshDirty`), because open-to-the-sky is a column question; chunks below the
  drawn band are never meshed, so it costs only what is on screen.

### 20b. What did not change, deliberately

- **The simulation, and every golden.** The cells that get a ramp are exactly the cells `BankLayout.At`
  and its sim twin `TerraceFoot` already mark. §6 expected `TerraceFoot` to be generalised and the
  goldens re-baked; neither was needed, because the corner rule changes a ramp's *shape*, never *which
  cells* have one.
- **A trench, a pit, a cell ringed by steps** — all four corners high — stays flat rather than being
  capped flush with the ground above (hiding a hole the simulation has); a skirt closes the edge beside
  it. It is still a foot cell to the simulation (the nav slope class and the tree guard), which is
  harmless.
- **`GroundSkin.Enabled` off** gives the boxes and bank wedges exactly as before: the measurement
  arm's control, and the path `BankMeshTests` and `SightFadeExemptionTests` now pin.

### 20c. A finding about the draw path

**The same material lights differently drawn with `Graphics.RenderMesh` than with
`RenderMeshInstanced`.** The first skin was drawn with `RenderMesh` and the meadow came out brighter and
yellower; a same-run photograph pair (`ODYSSEY_LOOK_BOXES=1` in the look harness) proved it was the
path, not the mesh. Drawn through `RenderMeshInstanced` with one identity instance it matches the boxes.
The likely cause is per-object lighting data (ambient probe) differing between the SRP Batcher path and
the instancing path; it was not chased further, and anything else this renderer draws with `RenderMesh`
is worth checking the same way.

### 20d. Measured

`FrameTimeTests.TheSkinAgainstTheBoxes`, played meadow, RTX 5070 Ti, one run on a quiet machine:

| | Standard boxes | Standard skin | Huge boxes | Huge skin |
|---|---|---|---|---|
| frame, 640 × 480 | 2.00 ms | 2.01 ms | 2.79 ms | 2.72 ms |
| frame, 3840 × 2160 | 9.89 ms | 10.34 ms | 10.88 ms | 10.40 ms |
| instances (4K) | 34,823 | 29,930 | 41,434 | 32,219 |
| draw calls (4K) | 1,185 | 1,132 | 1,751 | 1,616 |
| whole-board re-mesh, a chunk | 0.203 ms | **0.408 ms** | 0.273 ms | **0.439 ms** |

- **The frame is level within noise**; instances fall by 5–9k and calls by 50–135. The per-frame
  matrix upload for the turf the skin replaced is gone, which the GPU-bound 4K frame does not show.
- **Meshing a chunk costs about 0.2 ms more.** It was 0.49 before three cuts: relief sampled once per
  chunk corner instead of per triangle vertex, `BankDips` asked once a cell, and consecutive triangles
  skipping the group dictionary (its key comparison is a native Unity object equality). At the
  11-chunk budget a board-wide re-mesh burst now spends ~4.5 ms a frame against ~2.5. **Owed:** profile
  what remains (the mesh upload and the ramp checks are the suspects) or size the budget on the new
  number (§6c.7's arithmetic).

### 20e. What is owed

- **The per-column slice for M8's taller hills** (§7): the skin draws any relief the generator makes,
  but `SliceSettings.BelowSurface` still reads one surface layer.
- **The stream's zigzag** — a diagonal stream across a square grid — and the pale fringe terrain along
  it are the terrain's, not the skin's.
- **The foot IK and body lean in `PawnFigureDirector`** still read the relief only; a figure's feet
  on a ramp are placed by the pose, not by the ramp.
- **A rim lower than the surround** runs its apron *up* under the surround's tiles, which then hide it
  and show their own side above the rim; the apron fixes the (common) higher rim only. Meeting the rim
  column by column is M10's.
- Meshing cost, above.

## 21. Grass at distance, and bushes that stay (2026-09-24)

The owner, after M9: *"Walking through / past a bush shouldn't make it disappear, keep it there — it's
fine to walk through bushes. The biggest performance hit I can see is actually grass, especially when
full at distance … I see the biggest fps drop with the grass settings."* Decisions asked and answered
the same day: **bushes never fade**; for grass, **thin with distance**, **simpler far tufts** and
**solid blades far out** were approved; baked static grass was not.

### 21a. Bushes stay

`ChunkRenderer.NeverFades` takes the dressing flag, so bushes and stones are never partitioned into
the sight fade — for a colonist, selected or not. The sight lines to bodies and items under bushes
(§19a's second half) are removed with their switch (`seeThroughToGround`) and `UnderBush`; grass still
lies flat round an item or a body. Trees keep fading for colonists (§19b).

### 21b. Where Full's cost at distance actually is

`FrameTimeTests.TheGrassAtDistanceAgainstTheFrame` (Explicit, a measurement): 3840 × 2160, the world
paused, the wide (70 m) and farthest (140 m) framings, each condition taken twice and the lower kept —
the owner's editor was open on the same GPU throughout, and a single reading moved by more than the
thing measured. Standard is quoted; **Huge's readings were too noisy to use** (one kind alone measured
cheaper than no grass at all).

| Standard, 3840 × 2160 | frame @70 m | frame @140 m |
|---|---|---|
| no grass | 8.70 ms | 13.39 ms |
| tufts only (Meadow rung) | 9.42 | 14.66 |
| dressing only (Meadow rung) | 12.03 | 18.41 |
| Meadow, both, none of §21 | 12.55 | 19.93 |
| Full, none of §21 | 13.52 | 21.69 |

**The cost at distance is having the grass layer at all, and most of it is the dressing, not the
tufts.** At the farthest pull Meadow already costs about 6.5 ms and Full adds only ~1.8 more; the
dressing is ~5 of Meadow's cost, spread across its kinds (flowers ~+2.5, bushes ~+1.8, ground cover
~+1.6, stones ~+1.3, tall grass and sunflowers within noise) — each kind adding 100–170 draw calls at
that zoom, because every chunk carries its own bucket per kind and variant. So a rule that only
thinned the rungs above Meadow, which is what §21 first built, bought nothing where the drop was seen.

### 21c. What was built

- **Thinning with distance, at every rung** (`GrassThinning`, mirrored in `Odyssey/Foliage`'s
  `FoliageRank`/`FoliageThinScale`): every clump of grass — the tufts and the dressing that reads as
  meadow (tall grass, flowers, cover, sunflowers; never bushes, stones or crops) — has a rank in [0, 1)
  from an integer PCG hash of where it stands. Past `GrassThinNear` (70 m from the camera) the fraction
  kept falls as (near / distance)², the rate that holds clumps per pixel of screen roughly constant,
  never below `GrassThinFloor` (0.1). The CPU sorts each grass bucket by rank once when the chunk is
  meshed and submits only the prefix that can survive at the chunk's nearest point; the shader shrinks
  the rest clump by clump over the last 5% of the keep, so nothing pops at a chunk seam.
  `GrassThinningTests` pins the arithmetic and reads the HLSL mirror's constants out of the shader.
- **Tufts take coarser levels far out** (`TuftLevels`, `TuftLodBias` 8): measurable (more instances at
  a coarser level at the wide and far framings) but a small effect on its own.
- **Solid far blades: not built.** The measurement points at draw calls and instance counts per kind
  at far zoom, not at the see-through edge's overdraw, so a solid far stand-in was not the lever the
  evidence named. The switch that was sketched for it was removed rather than shipped empty.

### 21d. Measured, and seen

Photographs (`FrameTimeTests.TheGrassAtDistanceAtThePlayCamera`, Full grass, paused and stilled;
`docs/reference/screenshots/look/2026-09-24-grass-distance-{wide,far}-{none,shipped}.png`): the start
framing moves 1.37% of pixels (the top edge of the screen is past 70 m even there), the wide framing
10%, the farthest 36%. At the wide framing — the reference's own — the thinned meadow still reads
lush and the near field is untouched; at the farthest pull more painted ground shows through.

At the farthest pull on Standard, thinning saved about 0.8 ms at Meadow and about 1.6 ms at Full in
the batch arm (frame as the GPU's stand-in, noisy). **The real GPU numbers are owed from the player
bench** (`PlayerBench`, `-odyssey-bench-grass`), which needs the owner's go because it takes the screen.

### 21e. What is owed

- **The player bench's grass table**, which settles the numbers above on the GPU's own clock.
- **GPU-driven drawing for the dressing** — **done, §22.** (§18b's indirect path, built for the tufts and left off):
  the measured driver at distance is draw calls per kind per chunk, which is exactly what one indirect
  draw per kind removes. It was shelved on a measurement at the start camera, where it saved little;
  §21b says the far camera is where it pays. The owner's call.
- Huge re-measured on a quiet machine.

## 22. GPU-driven scenery (2026-09-24)

Owner: *"work on the scenery please now — to be included with this grass."* §21 had found the frame drop
zoomed out in the scenery's draw calls — every chunk submitting every kind — so the scenery now goes
the way §18b built for the tufts alone.

### 22a. What moved, and what did not

**On the indirect path** (`IndirectScenery`, `ChunkRenderer.UseIndirectScenery`, on by default): the
grass tufts, and every Meadow dressing kind drawn by `Odyssey/Foliage` — tall-grass stands, wildflowers,
sunflowers, ground cover, the grass bush — and the bushes. Each kind's instances on a layer live in one
GPU buffer; a compute pass (`OdysseyIndirectCull.compute`) culls them and one indirect draw per level
part draws what it kept. The foliage shader's DepthOnly and DepthNormals passes read the same instance
the forward pass reads, because bushes are opaque and reach the prepass.

**Left on the chunk path, deliberately:** trees (they fade for colonists through the ghost pass and cast
from a proxy level, and an indirect shadow caster must not be culled to the camera — the next lever, 618
of the 1,589 calls left at 140 m); stones (a pack shader that cannot read the buffers); crops; anything
on a ghosted layer; and any kind whose settings make it cast. Eligibility is asked per kind and
remembered (`ChunkRenderer.IndirectKindReport` lists the answers).

**A finding on the way:** the mesher tints the grass dressing as plain foliage — only bushes and stones
carry the dressing bit — so the first cut, which looked for the dressing bit, moved the tufts and bushes
and left every grass stand and flower on the chunk path. A per-kind call breakdown
(`ChunkRenderer.ChunkCallsByKind`) found it.

### 22b. Nothing the chunk path decides moved

The instances are gathered from the chunk buckets as segments (one chunk's bucket each). Every frame the
renderer decides per segment exactly what `DrawBuckets` decides per bucket — whether the walk drew the
chunk (frustum, the sun-side shadow sweep, the foliage draw distance), the level of detail from the
chunk's distance (one shared `LevelOf`), and the rank-sorted prefix the distance thinning keeps
(`IndirectScenery.CountBelow`, pinned against `GrassThinning.CountBelow`). The GPU only applies those
per instance, adds a per-instance frustum test (these kinds cast nothing, so an instance off screen is
never seen), and sorts survivors into one list per level.

**Proof** (`FrameTimeTests.TheIndirectSceneryDoesNotChangeThePicture`, paused and stilled, shots repeated
until two agree): **0.00% of pixels moved against a 0.00% floor at the start, 70 m and 140 m, at noon and
at 19.5 h**; taking the scenery away moved 22%, so it was in the shots.

### 22c. Regathered per chunk, in place

The first cut regathered a whole layer whenever any chunk on it re-meshed: **3.9 ms on Huge for one
chunk** — a stutter on every dig, build or growing crop. Each group now gives every chunk a slot with room
to spare; a re-meshed chunk rewrites only its own slots and uploads only those ranges; a slot that
outgrows its room moves to the end, and dead space is compacted once it is half the buffer
(`IndirectSceneryTests`). One chunk re-meshed: **0.16 ms on Huge, 0.11 on Standard**; a whole-board
re-mesh (a settings change): 0.58 and 0.71 ms.

### 22d. Measured

`FrameTimeTests.TheIndirectSceneryAgainstTheFrame`, one world per board, frame at 3840 × 2160 standing in
for the GPU (another session's batch run and the CI runner shared the machine; two runs):

| | chunk path | indirect | calls (chunk → indirect path) |
|---|---|---|---|
| Standard, start | 12.84 / 12.20 ms | 11.40 / 13.24 ms | 1,122 → 964 |
| Standard, 140 m | 20.93 / 19.09 ms | **16.33 / 15.27 ms** | 1,983 → 1,599 (173 indirect) |
| Huge, start | 13.09 / 13.32 ms | 11.88 / 11.67 ms | 1,560 → 1,236 |
| Huge, 140 m | 26.27 / 28.02 ms | **21.02 / 23.28 ms** | 3,363 → 2,573 (224 indirect) |

At 140 m the frame drops **4–5 ms at 4K** in both runs — more than the CPU submission it saves (Huge at
640 × 480: submit 5.06 → 3.59 ms), because the chunk walk keeps whole chunks inside the shadow sweep and
the indirect path culls each instance to the camera. At the start framing the gain is within this
machine's noise. **The GPU's own numbers are owed from the player bench**
(`-odyssey-bench -odyssey-bench-scenery`), which takes the screen and needs the owner's go.

### 22d-bis. In the player, and the variant stripping would have dropped

**The indirect variant needs a keep-alive of its own.** `ODYSSEY_INDIRECT` is switched on by a material
cloned in code, which stripping never sees used, so the player would have kept only the base variant —
which reads no buffer — and drawn every clump at the origin with a green log. `InstancingKeepAlive` now
makes a keep-alive per (shader, runtime keyword) (`Odyssey_Foliage_ODYSSEY_INDIRECT.mat`). The tufts-only
cut of §18b had the same gap and never met it because it shipped off.

**Smoke test** (player build, `-odyssey-newgame`, windowed 960 × 540): the session logs once which way
the scenery went — `[Scenery] Direct3D11: 151 indirect calls of 953, 14643 instances in the GPU buffers
(indirect on)`, the editor's counts exactly — and nothing throws. It found one fault older than this
work: the resolution dropdown threw on a window size the monitor does not list (a windowed player at a
size of its own), taking the settings panel down; the current size is now offered as a choice.

**A second fault the full tier found:** `TheFoliageShaderAgainstThePacks` switches Meadow foliage to the
pack's shader mid-session, and a kind already judged indirect then asked for a material that no longer
existed (`ArgumentNullException`). The remembered answers now follow that switch, and a missing material
draws nothing rather than throwing.

### 22e. What is owed

- The player bench's scenery table (the owner's go; it quits itself within 180 s).
- Trees on the indirect path — the largest block of calls left — with the sight fade kept on the chunk
  path for the chunks a colonist is behind, and an indirect shadow-caster draw culled by the sun-side
  sweep rather than the camera.
- Stones, if they move to a shader that reads the buffers.

### 22a. The real GPU, from the player (owner's run, 2026-09-24)

The development player at 3840 x 2160 on the RTX 5070 Ti, Direct3D 11, Standard, grass at Full, the
world paused and the hour held at noon (`-odyssey-bench -odyssey-bench-scenery`; it quit on its own,
exit code 0). Each distance timed with the scenery drawn from the GPU buffers and chunk by chunk, in
one run:

| Camera distance | GPU, chunk by chunk → GPU buffers | Frame | Draw calls |
|---|---|---|---|
| 32 m (the default) | 6.12 → **5.44 ms** | 6.20 → 5.88 ms | 1,161 → 975 |
| 70 m | 8.31 → **7.43 ms** | 8.37 → 7.90 ms | 1,796 → 1,377 |
| 140 m | 7.04 → **6.62 ms** | 7.12 → 7.14 ms | 2,099 → 1,627 |

**The whole Meadow look at Full runs at 5.9–7.9 ms a frame at 4K** — about 125–170 fps on this
machine, well inside the Ultra bar of 60 fps at 3840 x 2160. The GPU buffers save **0.4–0.9 ms of
real GPU** and 16–27% of the draw calls. That is smaller than the batch arm's 4–5 ms, and this is
the figure to quote: the batch arm runs inside the editor beside other sessions, which inflates every
frame, and it cannot read the GPU at all. The largest block of calls left is the trees (M5's work
and the next lever).

## 23. Trees: grouped, and simpler far away (2026-09-24)

**The question** (owner): would trees on the GPU-driven path be better for everyone? Measured first
(`FrameTimeTests.TheTreesAgainstTheFrame`, trees on and off in one run): trees cost the CPU 0.28 ms
(Standard, default zoom) to 0.94–1.33 ms (Huge at 140 m) at 640 × 480, and 25–42% of a 1080p frame,
most of it the fill of their leaf cards. A board's trees went out **four and a half to five a call**,
because every chunk submitted its own. So the CPU part was a batching problem, and the GPU part was a
level-of-detail problem that GPU-driven drawing would not have touched. The owner chose both fixes
below; full GPU-driven trees stay unbuilt.

**Grouped** (`ChunkRenderer.GroupTrees`). Every submission a board-tree bucket makes below the blended
queue — its crowns at the level chosen for the chunk and its shadow proxy — is gathered for the frame
and sent once per (material, mesh, submesh, shadow mode) at the end of the chunk walk. Every decision
stays per chunk (the frustum and sun-side cull, the level, the sight fade), so only the submission
changes. A faded crown's ghost blends (queue 3000) and keeps its own submission. **The first cut
grouped nothing in the game** and passed its EditMode test: it took only the opaque range (≤ 2500), and
the solid Meadow crowns sit in the foliage queue, 2501, like the grass. The picture proof caught it
(957 calls with grouping on and off), not the unit test, whose trees were primitives.

**Simpler far away** (`SimplerFarTrees`). Within `FarTreeNear` (60 m) of the camera a tree keeps
`TreeLodBias` (3) exactly; beyond it the bias eases to `FarTreeLodBias` (1) by `FarTreeFar` (120 m),
so levels coarsen gradually with distance rather than at a line. Not a preset lever: at every framing
the change is below what the eye separates (next table), so every preset has it.

**Proof** (`GroupingTheTreesDoesNotChangeThePicture`, paused, the hour held, per framing and hour):

| Framing | Floor | Grouping moved | Tree calls | Simpler far trees moved |
|---|---|---|---|---|
| start, noon / 19.5 h | 0.00% | **0.00%** | 274 → 55 / 277 → 57 | **0.00%** |
| 70 m | 0.00% | 0.00% | 463 → 87 / 393 → 84 | 0.52% / 0.32% |
| 140 m | 0.01% / 0.00% | 0.01% / 0.00% | 563 → 62 / 534 → 62 | 4.67% / 2.00% |

Taking the trees away moved 23.63%, so they are in the shots. The 70 m photographs with and without
simpler far trees (`TheLookAtThePlayCamera`, `ODYSSEY_LOOK_ALLFINE=1` for the before) cannot be told
apart by eye.

**Measured**, one run, before (chunk by chunk, every tree at today's detail) → after (grouped,
simpler far); frame in ms (editor batch, only the differences count; another session's batch run
was live):

| Board, zoom | Tree calls | CPU submit @640×480 | Frame @640×480 | @1080p | @4K |
|---|---|---|---|---|---|
| Standard, default | 276 → 55 | 1.43 → 1.35 | 1.93 → 1.81 | 8.66 → 7.35 | 8.93 → 7.71 |
| Standard, 140 m | 564–618 → 62 | 2.08 → 1.94 | 2.74 → 2.48 | 15.02 → 11.12 | 14.45 → 13.91 |
| Huge, default | 479–545 → 42–43 | 2.06 → 2.01 | 2.62 → 2.49 | 10.59 → 7.72 | 12.77 → 8.94 |
| Huge, 140 m | 1,008–1,169 → 41 | **3.62 → 2.95** | **4.52 → 3.53** | **20.12 → 13.78** | 21.32 → 19.35 |

Tree calls fall 80–96%; the most is won where the owner asked about it, big boards zoomed out. The
real GPU figure comes from the player: `-odyssey-bench -odyssey-bench-trees`
(`PlayerBench.TreeArms`: as shipped, chunk by chunk, grouped only, no trees, at 32 / 70 / 140 m).
