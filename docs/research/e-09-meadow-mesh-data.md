# e-09 — Meadow Forest mesh and material data

## Question

What exactly do the Synty *Polygon Nature Biomes — Meadow Forest* vegetation prefabs contain, so that
we can draw them through our own instanced renderer (`Graphics.RenderMeshInstanced`) with our own
clean-room foliage shader and our own per-level LOD? Six sub-questions: the LODGroups, the
materials and textures, the mesh vertex data, how the pack's foliage shader drives wind, the terrain
textures, and anything that would stop instanced drawing.

Everything below was read from files on disk (Unity YAML for prefabs, materials, metas and terrain
layers; a throwaway binary-FBX reader for the models; the shader graph's JSON). Nothing was opened in
Unity and nothing was copied into the repository. The licensed files remain under
`Assets/Synty/`, which is gitignored. Numbers are facts about the files; the wind description is
written from the graph's structure in our own words.

## Findings

### 0. Conventions that hold for every asset inspected

| Fact | Value | Consequence for us |
|---|---|---|
| FBX version / units | 7500, `UnitScaleFactor` 1 (centimetres), imported with `useFileScale 1`, `globalScale 1` | vertices are ×0.01 to metres; the computed extents match each LODGroup's `m_Size` to the centimetre, so the scale reading is verified |
| Up axis | Y-up (`UpAxis 1`), every model node has zero rotation and unit scale | the importer only mirrors X; no baked -90° rotation anywhere |
| LOD child transforms | **identity in every prefab** (position 0, rotation identity, scale 1), and every FBX node's local translation/rotation/scale is 0/0/1 | **one instance matrix serves every renderer of every level** |
| Negative scale | none anywhere | no winding flip to handle |
| Sub-meshes | exactly **one material slot per mesh**, every mesh | one `RenderMeshInstanced` call per (mesh, material) |
| Skinning / animation | `animationType 0` (none) on all 66 FBX; `importBlendShapes 0` on 65 of 66 | static meshes only |
| Readability | `isReadable 0` on all 66 FBX | fine for drawing; **CPU cannot read vertex colours at runtime** — bake anything at editor time |
| Vertex welding | `weldVertices 0` on 61 of 66 | imported vertex count ≈ the FBX polygon-vertex count (e.g. Tree_Meadow_01 LOD0 ≈ 44.8 k vertices for 22 k triangles) |
| Index format | `indexFormat 0` (auto) | 32-bit only where a mesh passes 65 k vertices (the Wildflowers *patch* LOD0s) |
| LOD fading | every LODGroup has `m_FadeMode 0` (none), `m_AnimateCrossFading 0`, `m_LastLODIsBillboard 0`; the shader graph's URP target has `m_SupportsLODCrossFade false` | the pack switches levels with a hard pop; any cross-fade is ours to add |
| Material instancing flag | `m_EnableInstancingVariants 0` on every material | irrelevant — we use our own shader and enable instancing on our own materials |

### 1. LODGroups

`screenRelativeHeight` is the fraction of screen height below which the level hands over to the
next; the last value is the cull height. "Card" is a flat impostor mesh (below).

**Trees** (all prefabs authored directly, not variants; each also carries one `MeshCollider` from
`Models/Collision/`):

| Prefab | `m_Size` (m) | LOD0 | LOD1 | LOD2 | LOD3 | Renderers per level | Triangles per level (main + branches) |
|---|---|---|---|---|---|---|---|
| Tree_Birch_01 | 10.56 | 0.358 | 0.156 | 0.049 | 0.0066 (card) | 2 / 2 / 2 / 1 | 10,136+1,026 / 5,440+270 / 2,784+108 / 12 |
| Tree_Birch_02 | 9.86 | 0.348 | 0.141 | 0.045 | 0.0066 (card) | 2 / 2 / 1 / 1 | 10,462+1,118 / 5,682+132 / 2,831 / 12 |
| Tree_Birch_03 | 5.23 | 0.318 | 0.137 | 0.047 | 0.0063 (card) | 2 / 2 / 1 / 1 | 2,463+482 / 1,334+74 / 684 / 12 |
| Tree_Fruit_01 | 6.15 | 0.25 | 0.044 | 0.0059 (card) | — | 1 / 1 / 1 | 7,500 / 4,064 / 12 |
| Tree_Fruit_02 | 5.76 | 0.217 | 0.042 | 0.0056 (card) | — | 1 / 1 / 1 | 5,616 / 4,180 / 12 |
| Tree_Fruit_03 | 4.66 | 0.198 | 0.039 | 0.0066 (card) | — | 1 / 1 / 1 | 6,013 / 3,422 / 12 |
| Tree_Meadow_01 | 17.87 | 0.371 | 0.138 | 0.039 | 0.010 (card) | 2 / 2 / 2 / 1 | 22,162+3,116 / 11,688+1,012 / 6,070+492 / 12 |
| Tree_Meadow_02 | 10.14 | 0.309 | 0.125 | 0.039 | 0.0078 (card) | 2 / 2 / 2 / 1 | 10,940+932 / 6,084+380 / 3,284+216 / 12 |

Fade transition widths are 0.1 (one 0.05). The "branches" renderer is a separate mesh of
alpha-cut branch planes with its own material (`Branches_01/02`); the main renderer is trunk **and**
leaf cards in one mesh (see §2). The fruit trees have no branches renderer; the separate
`SM_Env_Tree_Fruit_01_Fruit_01.fbx` is not referenced by any prefab.

**The card level** is 6 quads (12 triangles, 20 control points) spanning the whole tree including
the part below the pivot, UVs covering roughly 0.05–0.95 of a 2048² card texture that has its own
normal map. It is fixed geometry (crossed planes), **not** a camera-facing billboard, and it is drawn
with the same foliage shader as the full tree.

**Bushes** (authored prefabs):

| Prefab | `m_Size` | Heights | Renderers | Triangles |
|---|---|---|---|---|
| Bush_01 | 2.88 | 0.089 / 0.010 | 1 / 1 | 2,276 / 1,366 |
| Bush_02 | 5.02 | 0.077 / 0.010 | 2 (branches + leaves) / 1 | 2,298+28 / 1,488 |
| Bush_03 | 5.92 | 0.103 / 0.044 / 0.010 | 2 / 1 / 1 | 5,866+28 / 2,914 / 1,456 |

No bush has a card level; the last level is a lighter mesh. Bush_02's LOD1 uses a different
material (`Tree_Mat_01_Small`) from its LOD0.

**Grass.** Two authoring styles:

- *Tall clumps 01–05* are authored prefabs with their own LODGroup.
- *Short/Med clumps, the three Planes and Grass_Bush_01* are **prefab variants of the FBX model
  prefab**: the importer builds the LODGroup from the `_LOD0/_LOD1/_LOD2` node names and the variant
  overrides the heights, the fade widths, `m_Size` (sometimes) and the material of every renderer.
  The renderer-to-level assignment in the variants is therefore inferred from the node names, not
  read directly (see *Could not be determined*).

| Prefab | Size (m, W×H×D) | Heights | Triangles per level |
|---|---|---|---|
| Grass_Short_Clump_01 / 02 / 03 | 1.0×0.25×0.9 / 1.8×0.27×1.5 / 1.7×0.27×1.7 | 0.046 / default · 0.046 / default · 0.049 / 0.022 / 0.0059 | 6/4 · 14/4 · 26/16/8 |
| Grass_Med_Clump_01 / 02 / 03 | same footprints, 0.5 m tall | 0.046 · 0.083 · 0.099 / 0.022 / 0.0059 | 6/4 · 14/4 · 26/16/8 |
| Grass_Tall_Clump_01 / 02 / 03 | 1.0 / 1.8 / 1.9 m wide, 1.0 m tall | 0.103/0.010 · 0.076/0.010 · 0.096/0.030/0.010 | 6/4 · 14/4 · 26/20/8 |
| Grass_Tall_Clump_04 | 4.1×1.0×4.6 | 0.233 / 0.091 / 0.015 | 182 / 140 / 56 |
| Grass_Tall_Clump_05 | 7.4×1.4×7.0 | 0.351 / 0.158 / 0.015 | 494 / 380 / 152 |
| Grass_Bush_01 | 2.3×1.2×2.3 | 0.049 / 0.022 / 0.0059 | 78 / 28 / 4 |
| Grass_Short/Med/Tall_Plane_01 | 1.0 wide, single quad | no LOD | 2 |
| Grass_Large_01–04 (not asked; for reference) | ~34×3–4×34 patch | 0.80 / 0.55 / 0.097 | ~17.6 k / ~13.6 k / ~5.4 k |

The last grass level is two crossed quads (4 triangles) or a star of quads (8 triangles).

**Flowers and ground plants:**

| Prefab | Size (m) | Heights | Triangles | Materials per level |
|---|---|---|---|---|
| Wildflowers_01 | 0.74 × 0.70 tall | 0.397 / 0.079 / 0.0052 | 1,966 / 766 / 6 (card) | atlas mesh / atlas mesh / `WildFlowers_01` card |
| Wildflowers_02 | 0.79 × 0.70 | 0.400 / 0.082 / 0.0056 | 2,272 / 953 / 6 | same pattern |
| Wildflowers_03 | 0.79 × 0.70 | 0.397 / 0.078 / 0.0052 | 3,188 / 1,182 / 12 | atlas (`Flowers_Field_Variation`) / same / `WildFlowers_03` card |
| Wildflowers_Patch_01–03 (not asked) | ~18 × 0.65 × 18 | 0.497 / 0.047 | **134 k–209 k** / 1–2 k | patch of the above |
| Flowers_Flat_01 / 02 / 03 | ~2.1 × 0.15 × 2.0 | **no LODGroup** | 18 / 18 / 8 | `Flowers_Flat_Mat_01` |
| Sunflower_01 | 0.8 × 1.6 tall | 0.25 / 0.052 / 0.010 | 2,091 / 707 / 4 (card) | atlas / atlas / `Card_Sunflower_01` |
| Ground_Cover_01 | 2.9 × 1.6 × 1.6 | 0.087 / 0.010 | 58 / 12 | `Ground_Cover_Mat_01` |
| Ground_Cover_02 | 1.9 × 0.57 × 1.9 | 0.091 / 0.030 / 0.0063 | 66 / 22 / 4 | same |
| Ground_Cover_03 | 2.1 × 1.6 × 1.6 | 0.083 / 0.031 / 0.0063 | 80 / 14 / 6 | same |
| Lillies_01 / 02 / 03 | 2.7 × 0.2 × 2.6 (flat) | 0.25 / 0.010 | 2,106 / 1,192 / 1,648, then a 2–3 triangle flat card | LOD0 `NoWind_Mat_01` (atlas), LOD1 `Mat_Lillies_01` |

**Rocks and mushrooms** (all FBX-variant prefabs, **no LODGroup**):

| Prefab | Size (m) | Triangles | Material |
|---|---|---|---|
| Rock_Pile_01–07 | 4.8–13.4 wide, 1.8–7.4 tall | 622–2,204 | `Rock_Grass_Triplanar_Meadow_01` (plus a MeshCollider) |
| Mushroom_01–06 | 0.07–0.20 | 54–316 | `PolygonNatureBiomesMeadow_Mat_01` (atlas) |
| Mushroom_Group_02–05 | 0.2–0.36 | 236–1,046 | same |
| Mushroom_Sparse_01–05 | 1.2–1.7 wide | 714–4,838 | same |

**Converting a height to a distance.** A level is shown while
`m_Size / (2 · d · tan(fov/2)) ≥ h`, so it hands over at `d = m_Size / (2 · h · tan(fov/2))`. At a
60° vertical field of view: Birch_01 goes LOD0→1 at ~26 m, 1→2 at ~59 m, 2→card at ~186 m and culls
at ~1.4 km; Fruit_01 goes to LOD1 at ~21 m and to the card at ~122 m; a tall grass clump drops to
4 triangles at ~8 m and culls at ~87 m; a wildflower leaves LOD0 at ~1.6 m and is a card from ~8 m.
At the game's viewing distances **the trees live in LOD1–LOD2 and every flower is a card**.

### 2. Materials and textures

Every vegetation material except the lilies, rocks and mushrooms uses one shader,
`PNB_Core/Shaders/Foliage.shadergraph` (URP and Built-in targets, opaque, alpha-clipped,
`RenderFace` both, `_Cull 0` = off, so **double-sided**; `_AlphaToMask 1`). The Foliage material
always has **two albedo slots — a leaf texture and a trunk texture — and the shader picks between
them per vertex by vertex colour B** (the leaf mask). A single mesh therefore carries both bark and
leaves under one material; the "trunk and leaves as separate materials" split only exists as the
separate *branches* renderer on some trees.

| Material | Used by | Leaf slot | Trunk slot | Normal maps | Clip threshold* |
|---|---|---|---|---|---|
| `Tree_Birch_Mat_01` | Birch LOD0–2 main | `Plants/leafPatch_01.tga` 512² RGBA | `Plants/Birch_Trunk_Texture.png` 2048² RGB | none | 0.25 |
| `Tree_Mat_01` | Fruit trees LOD0–1, Bush_01, Bush_02 LOD0 | `leafPatch_01.tga` 512² | `PolygonNatureBiomes_Meadow_Texture_01.png` 4096² RGBA **atlas** (imported at max 2048) | none | 0.25 |
| `Tree_Mat_01_Small` | Bush_02 LOD1 | `leafPatch_01.tga` | atlas | none | 0.25 |
| `Tree_Mat_03` | Meadow trees LOD0–2, Bush_03 | `leafPatch_04.tga` 512² | atlas | none | 0.25 |
| `Branches_01` / `Branches_02` | branch renderers (Meadow, Bush_02/03 / Birch) | `Plants/Branches_0N.tga` 1024² RGBA | same texture | none | 0.25 |
| `Card_Tree_*` (8), `Card_Sunflower_01` | card levels | `LOD_Cards/<name>.tga` 2048² RGBA | same texture | `<name>_Normals.tga` 2048² in both normal slots, enabled | 0.25 |
| `Grass_Short/Med/Tall_Mat_01` | grass | `Plants/Grass_Short_01`, `Grass_Mid_01`, `Grass_01` .tga 2048² RGBA | none | matching 2048² normal maps, enabled | 0.25 |
| `Ground_Cover_Mat_01` | ground cover | `GroundCover_01.tga` 1024² | none | 2048² normal | 0.25 |
| `Flowers_Flat_Mat_01` | flat flowers | `FlowersFlat_01.tga` 2048² | none | 2048² normal | 0.25 |
| `WildFlowers_01/02/03` | wildflower cards | `WildFlowers_0N.tga` 2048² | same | 2048² normal in both | 0.25 |
| `BASEMat_02_Saturated`, `Flowers_Field_Variation`, `Sunflowers_Mat_01` | wildflower and sunflower LOD0–1 | atlas (4096², imported 2048) | atlas | none | 0.5 / 0.25 / 0.5 |
| `NoWind_Mat_01`, `Mat_Lillies_01`, `PolygonNatureBiomesMeadow_Mat_01` | lily LOD0, lily card, mushrooms | a different Synty shader (GUID `0730dae3…`, not under the two folders scanned), albedo = atlas or `LillyPads_Medows_01.tga` 2048² (+ normal) | — | lilies only | 0.5 / cull back (`_Cull 2`) |
| `Rock_Grass_Triplanar_Meadow_01` | rock piles | triplanar shader (GUID `19e269a3…`, not in the scanned folders): top = `Terrain/Grass_Texture_01`, sides/bottom = `Terrain/RockWall_Texture_01`, each with a 2048² normal | — | yes | n/a |

\* The shader graph's clip input is `_Alpha_Clip_Threshold`; the `_Cutoff`/`_AlphaCutoff` values
(0.3–0.5) that also appear in the files are the Built-in/URP material-inspector copies and are not
what the graph reads.

Atlas versus unique: bark, flower heads, sunflowers, lilies and mushrooms sample the one **palette
atlas** (`PolygonNatureBiomes_Meadow_Texture_01.png`, 4096², with a `_Saturated` twin); leaves,
branches, grass, ground cover and cards use **unique** textures. Every albedo that is cut out
carries its mask in **alpha** (the leaf, branch, grass, flower and card textures are all 32-bit);
the birch bark and terrain textures have no alpha. Normal maps are imported as normal maps
(`textureType 1`). All textures are imported at `maxTextureSize 2048`, including the 4096 atlas.

The Foliage materials also carry colour controls the shader applies on top of the texture: a leaf
base colour, a small-scale and a large-scale "noise" colour mixed by world-position noise (so
neighbouring trees differ in hue), a trunk base and noise colour, an optional "frosting" colour on
upward-facing surfaces (on for the birch), and an emission mask (bioluminescent grass only). All of
this is optional look, not structure.

### 3. Mesh vertex data

- **UV sets:** exactly **one** (`map1`) on every mesh. No lightmap UV, no second channel.
- **Normals and tangents:** present in every FBX.
- **Vertex colour:** present on every tree, bush, grass, wildflower, sunflower and ground-cover
  mesh; **absent** on the flat flowers, lilies, rock piles and mushrooms. Mapping is per
  polygon-vertex, indexed. Measured channel behaviour (correlation with height across every
  vertex):

| Channel | Range | Behaviour | Meaning |
|---|---|---|---|
| R | 0 → ≈0.5 | correlates with height at 0.98–1.00 on every coloured mesh; 0 at the trunk base | **height gradient — the bend weight**. For trees and bushes it reaches ~0.5 at the crown whatever the height; for grass it tracks absolute blade height (0.13 at 0.25 m, 0.25 at 0.5 m, ~0.5 at 1 m) |
| G | 0 or 1 on tree/bush leaves; 0 → 1 root-to-tip on grass | 0 on every branch and trunk vertex | **leaf-tip gradient** (flutter weight at the free end of a leaf card or blade) |
| B | 0 or 1 | 1 on leaves, grass and flowers; 0 on bark, branch planes and the sunflower | **leaf mask** — selects leaf versus trunk texture and material values, and gates leaf flutter |
| A | 1 everywhere | — | unused ("all") |

The pack's own graph carries a note saying the same four things (height gradient, leaf-tip gradient,
leaf mask, all), so this reading is confirmed rather than inferred.

- **Pivot:** at ground level, centred on the trunk or clump. **Trees are sunk 0.4–2.1 m below the
  pivot** (Birch_01/02 and Meadow_02 by ~1.0 m, Meadow_01 by 2.1 m, fruit trees 0.6–0.9 m; the card
  level extends just as far), grass and flowers by 3–6 cm, bushes by up to 0.6 m, rock piles by
  0.1–1.3 m.
- **Size against the 2.5 m cell** (above ground, canopy width): Birch_01/02 ~9 m tall × 5 m wide
  (two cells); Birch_03 ~4.6 m × 2.7 m (one cell); fruit trees 3.9–5.2 m tall × 4–5 m (two cells);
  **Meadow_01 12.9 m tall × 17 m canopy (seven cells)**; Meadow_02 7.7 m × 9 m (four cells); bushes
  2.5–5.8 m wide; grass clumps 1–1.9 m (Tall_Clump_04/05 are 4–7 m mats); wildflowers 0.7 m; flat
  flowers ~2 m square; rock piles 5–13 m.

### 4. How the Foliage shader drives wind (mechanism, in our own words)

The graph has 359 nodes; 145 of them feed the vertex position. It is three independent motions
added together in **object space**, each behind its own on/off switch, then optionally scaled by two
global values when a "global weather controller" switch is set:

1. **Breeze** — a small horizontal (X and Z only) sway whose amount comes from a strength value and
   whose shape comes from a noise lookup keyed on world position and time, so the breeze travels
   across the field rather than every plant moving in step.
2. **Light wind (leaf flutter)** — a displacement along the **world-space normal**, driven by a
   sine of time, applied to leaf vertices only. Its weight is the leaf mask (B), faded towards the
   leaf tip by the tip gradient (G) when the "use leaf fade" switch is on, with a separate vertical
   strength and a vertical offset term.
3. **Strong wind / gale** — the whole plant bends in the wind direction (a world direction
   converted into object space) and oscillates: a sine of time × frequency, phase-shifted by the
   **object's world position** so each instance moves out of step, scaled by a gale strength and a
   bend amount. A **twist** term rotates the vertex about the vertical axis by a small angle. The
   blend from rest position to bent position is a lerp whose weight is the **height gradient (R)**,
   so the base stays pinned and the crown moves most.

Inputs, in total: vertex colour R, G and B; object-space and world-space vertex position; the world
normal; the object's world position (one value per instance); time; a world wind direction; and
about fifteen strength/frequency/switch parameters. **No UV channel and no texture feeds the vertex
stage.** The fragment stage uses B again (compare against a threshold, then branch) to choose the
leaf or trunk texture, normal map, smoothness, metallic and occlusion, and uses world position
noise for the colour variation. Back faces get their tangent normal flipped (an `IsFrontFace`
branch).

### 5. Terrain textures (`Terrain/`)

28 `.terrainlayer` files. **Every layer has a 2048² RGB albedo and a 2048² normal map** (normal
strength 1, Cobblestone 0.7); **none has a mask map**; albedos have no alpha, so there is no
smoothness in alpha. Smoothness and metallic are 0 except the two mud layers (0.5 / 0.3). Tile sizes
(metres per repeat): 2 m for most grass/dirt/leaf/moss layers; 4 m for Grass_01, Dirt_01, Dirt
Cracked Debris, Gravel and Mud_01; 3 m for Cobblestone, Footpath Tiles and Ruin Tiles; 12 m for
Mud Gravel and Rock Moss; 20 m for Rock_01. Several layers share a normal map (e.g. the two grass
layers use `Ground_Normals_01/02`). Note that `Grass_Texture_01.png` and `Grass_Texture_02.png` are
the smallest files in the folder (~200 KB at 2048²), i.e. low-frequency. The folder also holds
`Mat_*.mat` materials for the same surfaces, not inspected.

### 6. Anything that would stop `Graphics.RenderMeshInstanced`

Nothing structural:

- one sub-mesh per mesh, so `submeshIndex 0` always;
- static meshes, no skinning, no blend shapes in use, no animation;
- no negative scale and no child offsets, so one matrix per instance is exact;
- non-readable meshes draw fine (the only cost is that runtime code cannot read vertex data; mesh
  `bounds` remain available);
- the pack's own materials cannot be reused as they are (instancing variants off, a shader graph we
  do not own), which is expected — we bind the textures into our own instancing material.

Things to watch rather than blockers: the **Wildflowers_Patch** meshes are 134–209 k triangles at
LOD0 (and need 32-bit indices) — do not instance them; the **Grass_Large** patches are 17–18 k
triangles over ~34 m; the lily and rock/mushroom materials use shaders outside the Foliage graph;
and the trees' sunk trunks (up to 2.1 m) can poke out of a 3 m terrace bank face below a tree
planted near the edge.

## Recommendation

**Draw every species as a small set of (mesh, texture-set) buckets sharing one matrix array, with
at most three levels, using a single clean-room foliage shader that reads the vertex colour exactly
as the pack authored it.**

1. **Matrices.** Store one TRS per placed plant (the prefab root). Because every LOD child and every
   FBX node is identity with no negative scale, that one matrix is used unchanged for *every*
   renderer of *every* level. A level is a list of 1–2 (mesh, material) pairs drawn with the same
   matrix slice; the branches renderer is simply a second pair in the same level.

2. **Levels to keep.** Trees: **LOD0, the lowest non-card mesh (LOD2 for the four-level trees, LOD1
   for the fruit trees), and the card** — dropping the four-level trees' LOD1 saves a bucket per
   species where the draw-call census says buckets, not triangles, are the cost (the surround's
   woods were 230 batches at 17 trees a call, `06-rendering-and-camera.md` §6c.4). Bushes and
   grass: **LOD0 and the last level** (the last grass level is 4–8 triangles). Wildflowers and the
   sunflower: **LOD0 and the card** (from ~8 m every flower is a card anyway). Flat flowers, rocks and
   mushrooms: one level. Choose the level **per sector, not per instance**, by distance from the
   camera to the sector, using the pack's heights converted with the formula in §1 and one global
   bias; this keeps the matrix arrays stable between frames, which is what the surround's sector
   scheme already relies on. Keep the pack's cull height as the far limit. Add cross-fade later if
   the pops show; the pack has none.

   *Tie-break for dropping tree LOD1:* at ~25–60 m the choice is between LOD1 (~5.7 k triangles for
   Birch_01) and LOD2 (~2.9 k). The cheapest experiment is one screenshot pair of a birch stand at
   30 m in play light with each; if the owner cannot tell them apart, LOD1 stays dropped.

3. **Textures our shader needs.** Two albedo slots (**leaf** RGBA with the cut-out in alpha, **trunk**
   RGB/RGBA), an optional **leaf normal** and **trunk normal** (only the cards, grass, ground cover
   and flower cards have them — bind a flat normal otherwise), a clip threshold (0.25 for everything
   except the atlas-based flower meshes, 0.5), cull off with the normal flipped on back faces. Select
   leaf versus trunk per fragment with **vertex colour B > 0.5**. Tree bark and flower meshes point
   at the palette atlas; leaf cards at a 512² leaf patch; cards at their own 2048² texture. A leaf
   tint and a world-position hue variation belong in our shader as our own parameters, not copied
   values.

4. **Wind weight.** Use the colours as authored: **bend weight = saturate(2·R)** (0 at the base,
   1 at the crown), applied to a sway in a global wind direction phased by the instance's world
   position (free under instancing: it is the matrix's translation); **flutter weight = B·G**
   (leaves only, strongest at the tip), applied along the normal. Assets without vertex colour
   (flat flowers, lilies, rocks, mushrooms) get **no wind**. No bake step is needed, which matters
   because the meshes are not readable at runtime.

5. **Scope limits.** Do not instance the Wildflowers_Patch or Grass_Large meshes; draw single clumps
   instead. The lilies, rocks and mushrooms need a plain lit cut-out (lilies) and an opaque atlas or
   triplanar path, not the foliage shader.

## Sources

All under `D:\code\odyssey-meadow\Assets\Synty\` (licensed, gitignored, read only):

- `PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Tree_{Birch_01..03,Fruit_01..03,Meadow_01..02}.prefab`,
  `SM_Env_Bush_01..03.prefab`, `SM_Env_Grass_*.prefab` (all 21), `SM_Env_Wildflowers_01..03.prefab`,
  `SM_Env_Wildflowers_Patch_01..03.prefab`, `SM_Env_Flowers_Flat_01..03.prefab`,
  `SM_Env_Sunflower_01.prefab`, `SM_Env_Ground_Cover_01..03.prefab`, `SM_Env_Lillies_01..03.prefab`,
  `SM_Env_Rock_Pile_01..07.prefab`, `Prefabs/Props/SM_Prop_Mushroom_*.prefab` (15)
- `PolygonNatureBiomes/PNB_Meadow_Forest/Models/*.fbx` for each of the above (binary FBX parsed:
  geometry, layer elements, model transforms, global settings) and their `.fbx.meta` import settings
- `PolygonNatureBiomes/PNB_Meadow_Forest/Materials/Plants/*.mat`, `Materials/LOD_Cards/*.mat`,
  `Materials/BASEMat_02_Saturated.mat`, `NoWind_Mat_01.mat`, `Sunflowers_Mat_01.mat`,
  `PolygonNatureBiomesMeadow_Mat_01.mat`, `Rock_Grass_Triplanar_Meadow_01.mat`
- `PolygonNatureBiomes/PNB_Meadow_Forest/Textures/{Plants,Normals,LOD_Cards}/*` and
  `Textures/PolygonNatureBiomes_Meadow_Texture_01.png` (PNG/TGA headers and `.meta`)
- `PolygonNatureBiomes/PNB_Meadow_Forest/Terrain/*.terrainlayer` and the textures they reference
- `PNB_Core/Shaders/Foliage.shadergraph` (JSON: properties, targets, blocks, edge graph, notes)
- GUIDs resolved against every `.meta` under `PolygonNatureBiomes/` and `PNB_Core/`
- Scratch readers (not in the repository): `fbxlib.py`, `survey.py`, `mats_sg.py` in the session
  scratchpad

## Confidence

**High** for the LOD tables of the authored prefabs, transforms, triangle counts, sizes, materials,
texture sizes, terrain layers and the vertex-colour channel meanings (parsed directly and confirmed
by the graph's own note). **Medium** for the wind mechanism (read from the graph's structure — node
types, edges and which inputs reach the position output — not from running it or tracing every
arithmetic step), for the card geometry's exact layout, and for the renderer-to-level mapping inside
the FBX-variant grass prefabs.

## Could not be determined

- **Which FBX node each variant prefab's LOD renderer points at.** Variants refer to renderers by
  Unity-generated file IDs; the hashing scheme could not be reproduced offline (MD4 over the obvious
  name forms did not match), so the grass-variant level contents are inferred from the `_LODn` node
  names and the triangle counts, not read.
- **The exact wind formula** (constants, which sine feeds which axis, how the three motions are
  weighted against each other). Only the topology was traced; our shader should be tuned by eye, not
  matched.
- **The two non-Foliage shaders** (GUIDs `0730dae3…` for lilies and mushrooms, `19e269a3…` for the
  triplanar rocks) live outside `PolygonNatureBiomes/` and `PNB_Core/` and were not resolved.
- **Whether the global weather controller script exists** and what it writes; only the switch and
  the two global parameters are visible in the graph.
- **Imported vertex counts and tangent generation** as Unity actually builds them (inferred from
  `weldVertices 0` and the FBX polygon-vertex counts; `tangentImportMode` was not read).
- **Visual equivalence of LOD1 and LOD2** at mid distance — needs the screenshot pair in the
  recommendation.
