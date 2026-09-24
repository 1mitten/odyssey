# d-17 — Shading the ground skin

Research lane, 2026-09-24. Cap: 12 searches, 20 reads; used 7 searches and 19 reads (web and local
together). Nothing in the repository was changed except this file.

## Question

The per-cell instanced ground boxes are being replaced by a continuous heightfield **skin**: one
CPU-built mesh per 25 x 25-cell chunk, re-meshed when a cell is dug, textured with the Synty
*POLYGON Nature Biomes — Meadow Forest* terrain textures. What is the best way to shade it in URP
17.3? Specifically: (1) Texture2DArray splatting against a per-vertex material index plus weights,
how many layers a pixel can afford, height-based blending; (2) building the array at runtime from
the imported textures with `Graphics.CopyTexture`, and what it costs in memory at 2048, 1024 and 512
as a *terrain detail* setting; (3) triplanar against world-XZ projection on ramps up to 50°;
(4) anti-tiling at a 48° camera over 300–600 m boards; (5) what Unity's own URP TerrainLit does
with splats, holes and the basemap, and whether a far basemap is worth having; (6) how the
renderer's per-submesh tint keys (daylit, tilled, stored) multiply in.

## Findings

### What the pack actually gives us (measured on disk)

- `PNB_Meadow_Forest/Terrain/` holds **34 colour textures and 29 normal maps, every one a
  2048 x 2048 RGB PNG with no alpha** (`file` on all 61). The grass variants share `Ground_*`
  normals, so there are fewer normals than colours.
- Import settings are the defaults: colour textures `textureType: 0`, sRGB, *Automatic*
  compression, 2048 max, not readable, no mip streaming; normals `textureType: 1`. On desktop that
  resolves to **BC1 (DXT1) sRGB for colour** (no alpha channel to keep) and **DXT5nm (BC3) for
  normals**, Unity's default desktop normal encoding (BC5 is available since 2022 but is not the
  default). Because every source is the same size and imported the same way, **every colour texture
  lands in one format and every normal in another** — the precondition for copying them into two
  arrays. This should still be asserted at build time (`src.format == array.format`), because one
  texture re-imported with alpha would become BC3 and fail the copy.
- The pack ships `.terrainlayer` assets with **`m_TileSize` 4 x 4 m and no mask map** — so there is
  no authored height or smoothness channel; height-based blending must derive a height from what is
  there.
- The current ground tiles the texture **twice across a 2.5 m face**, a 1.25 m repeat
  (`docs/lessons.md`, "Seamlessness is why it works at cell scale"). That is a third of the pack's
  own repeat, and a divisor of the cell, so every repeat lines up with a cell edge — which is the
  grid the skin exists to hide.

### (1) Splatting: where the weights come from, and how many layers

Two ways to tell a pixel which layers it is made of:

| | Per-vertex indices + weights | Per-pixel lookup of a cell-ID map |
|---|---|---|
| Data | 4 layer indices (UV1) and blend coordinates on the mesh the chunk already rebuilds | a 27 x 27 R8 texture per chunk (25 cells + a border), world XZ → cell → 4 neighbour IDs |
| Per-pixel cost | the array samples only | the array samples **plus four loads** |
| Binding | one material for every chunk, nothing per chunk | a texture per chunk (per-draw property or an array slice index) |
| Dig | already re-meshed | re-mesh *and* re-upload a texel |
| Blend shape | fixed by mesh topology (see below) | free per pixel |

**The interpolation trap with per-vertex indices.** An index cannot be interpolated: if the three
vertices of a triangle disagree about which layer is "index 0", the pixel in between samples a
meaningless in-between slice. The indices must be **constant across each primitive**, which rules out
a mesh whose vertices are shared between cells of different materials.

**The layout that makes it exact: the quarter-cell ("dual") quad.** Split each cell into four
quarter quads, each running from the cell's centre to one of its corners. Every point of a quarter
quad is influenced by exactly four cells — its own, the two edge neighbours on that side, and the
diagonal one — so the quad carries those **four indices, identical on its four unshared vertices**,
and a local `(u, v)` from 0 at the cell centre to 1 at the corner. The shader turns `(u, v)` into
bilinear weights: 1 for the own cell at the centre, ½ / ½ at an edge midpoint, ¼ each at the corner.
Cost: 4 unshared vertices per quarter, 16 per cell, 10,000 per 25 x 25 chunk — trivial for a
CPU mesher, and a dug cell only rewrites its own and its neighbours' quarters.

**So four layers per pixel is not a budget, it is the geometry's maximum.** On a cell grid whose
blends reach at most half a cell from a boundary, no point can see more than four cells. TerrainLit
blends four per pass for a different reason (four channels in a control texture); ours falls out of
the grid.

**Height-based blending** is what turns the soft bilinear diamond into a believable edge (grass
growing over dirt, gravel showing between stones). TerrainLit's `HeightBasedSplatModify`
(`TerrainLitPasses.hlsl`, read from the local URP package) is the whole algorithm in eight lines:
multiply each layer's height by its weight, find the maximum, keep only layers within a
`_HeightTransition` of it, renormalise. It reads height from the mask map's blue channel. **The
Synty layers have no mask map**, so the height is taken from the **luminance of the albedo already
fetched** plus a per-layer bias and scale held in the material constants — zero extra samples. A
dedicated height array (an R8 render-texture array filled by one blit per layer at load, 512 px,
about 3.3 MiB for ten layers with mips) is the upgrade if luminance reads wrong on some layer; it
costs one more sample per blended layer. TerrainLit also has a *density* mode driven by albedo alpha,
which is unavailable here (BC1, no alpha).

**Keeping the common case cheap.** Most of a meadow is one material. When all four indices on a
quad are equal (a per-vertex, therefore per-triangle, condition), branch to a **single-layer path**.
The branch is coherent across whole triangles, so it costs the GPU nothing in divergence except on
the boundary triangles that take the full path anyway.

### (2) Building the arrays at runtime

- `Graphics.CopyTexture` copies **on the GPU with no CPU readback when either texture is not
  readable** — "CopyTexture becomes one of the fastest ways to copy a texture". It does no format
  conversion: formats must match (or be of a compatible bit width), and for block formats the
  region must be a multiple of the 4-pixel block. **So yes: BC1 into a BC1 array and DXT5nm into a
  DXT5 array, compressed, element by element and mip by mip, with no readback and no re-encode.**
  What it cannot do is convert — uncompressed into compressed, or BC1 into BC7.
- Create the destination with the overload that takes `createUninitialized: true` and an explicit
  `mipCount`, `linear: false` for colour and `true` for normals. Do not call `Apply()` after the
  copies — it would upload the (empty) CPU side over the GPU data. The documentation notes a runtime
  array with a **mipmap limit needs a readable CPU copy**, so make the array's mip count the quality
  decision (below) rather than using a mipmap-limit group.
- **"Mip arguments refer to the currently loaded mip levels."** With the global *Texture Quality*
  below full resolution, the source's mip 0 is no longer 2048. Read the source's active mip limit
  and offset the copy; there is (at least) one historical issue titled *CopyTexture does not work
  with Texture2DArray when Texture Quality is not Full Res* (its page no longer resolves). Test the
  build path with the quality lowered.
- **The quality setting is a mip offset, not a resample.** An array at 1024 is filled from the
  sources' mip 1 onward, at 512 from mip 2. No pixels are touched on the CPU, the result is exactly
  what the GPU would have sampled at that distance anyway, and switching the setting is rebuilding
  two arrays.
- **Memory, ten layers, full mip chain** (BC1 = 0.5 B/texel, BC3/BC5 = 1 B/texel, mips add a third):

  | Array size | Colour (BC1) | Normals (DXT5nm/BC5) | Both |
  |---|---|---|---|
  | 2048 | 26.7 MiB | 53.3 MiB | **80 MiB** |
  | 1024 | 6.7 MiB | 13.3 MiB | **20 MiB** |
  | 512 | 1.7 MiB | 3.3 MiB | **5 MiB** |

  The source `Texture2D`s are resident as well while anything references them, so drop the
  references (and let the pack's materials go) once the arrays are built, or the 2048 rung is
  160 MiB rather than 80.
- **What the screen can use.** At the default 48° pitch the camera is 119 m up and sees ground 50–224
  m away (`06-rendering-and-camera.md` §2b). At 4K that is of the order of 25–40 screen pixels per
  metre of ground; a 4 m repeat at 2048 is 512 texels per metre, at 1024 it is 256, at 512 it is 128.
  So **at the default zoom even the 512 rung is oversampled three- to fivefold** and the hardware is
  already reading mip 3 or 4 of the 2048 texture. The top rungs only show at the closest zoom.
- **Licensing.** The arrays live in memory only, built from the textures under `Assets/Synty/`, so
  nothing derived from the pack is committed. When the pack is absent (CI runner, a fresh clone),
  build the same arrays from 4 x 4 flat-colour placeholders so the shader, the tests and headless
  runs are unchanged; ask whether the textures *resolved*, not whether a catalogue exists (the
  runner lesson in `CLAUDE.md`).

### (3) Projection on ramps

- World-XZ projection stretches texels along the fall line by 1/cos θ: **1.56 x at 50°**, 1.31 x at
  40°, 1.15 x at 30°. On a heightfield there are no vertical faces, so there is no smear to the point
  of streaks, only a mild stretch on the steepest cells.
- Triplanar costs **three samples per texture per layer** — with four layers and a normal map that
  is 24 array samples on every pixel, to fix a stretch that only terrace ramps show. Biplanar
  (Quilez) cuts it to two samples with manual gradients and "the look of a triplanar mapping with an
  aggressive blending factor".
- The skin's only steep ground is the terrace ramp at a step foot, a small fraction of any frame at a
  48° camera.
- **Normal maps under XZ projection** need a tangent frame: world X and Z projected onto the surface
  give it analytically, and the sampled normal is reoriented onto the skin normal (the whiteout / RNM
  blends in the standard treatments of triplanar normal mapping).

### (4) Tiling

| Technique | Extra samples | Notes |
|---|---|---|
| Longer repeat, not a divisor of the cell (the pack's own 4 m) | 0 | removes the cell-aligned grid; the single largest improvement |
| Macro variation: a low-frequency noise at two scales (e.g. ~40 m and ~150 m) modulating albedo value and hue by ±10–15% | 1–2 per pixel, **shared by every layer** | breaks the repeat at the 50–224 m viewing distance, where individual tiles blur into a repeated tone rather than a repeated pattern |
| Quilez technique 3 (variation index from a low-frequency pattern, two virtual tiles blended) | +1 per texture per layer, plus one cheap noise fetch | cache-friendly and mip-safe with shared gradients |
| Hex tiling (Mikkelsen 2022, after Heitz–Neyret) | x3 per texture per layer | best quality; with four layers and normals it is 24 samples — too dear on the blend path |
| Quilez technique 1 / 2 | x4 / x9 | ruled out |

The noise for macro variation is either a small tileable texture sampled at two scales or two octaves
of value noise in ALU; either is shared across layers, which is why it is the cheapest per unit of
visible improvement. At 48° over hundreds of metres, **repetition is read as a regular tonal lattice**,
which is exactly what macro variation breaks; per-texture stochastic tiling mostly pays off close up.

### (5) What URP's TerrainLit does (read from the local URP package)

- **Splats:** four layers per pass from one RGBA control texture (`SplatmapMix`), four separate
  `_Splat0..3` textures sharing one sampler; beyond four layers it draws **additive passes**
  (`TERRAIN_SPLAT_ADDPASS`, `Blend One One`, clipping pixels whose weight is under 0.005). Up to eight
  layers per material; height blending is switched off above four.
- **Height blend:** `HeightBasedSplatModify`, described above, from the mask map's blue channel.
- **Holes:** a separate holes texture and a `clip()` (`ClipHoles`, with an epsilon because a
  compressed 0 is not 0). A `clip` disables early depth rejection for the whole draw. **We do not
  need it**: the skin is re-meshed on a dig, so a hole is simply absent geometry.
- **Basemap:** beyond *Base Map Distance* the terrain switches to a *Base Pass* shader sampling a
  pre-blended colour texture generated by a *Basemap Gen* shader (`Dependency "BaseMapShader"` /
  `"BaseMapGenShader"` in `TerrainLit.shader`). It exists because a Unity terrain can be kilometres
  deep in view and has up to eight layers in two passes.
- **Is a basemap worth it here?** Not now. The board in view is 50–224 m away at the default pitch;
  the far land to 1,100 m is the surround, a separate and already cheap pass. The single-layer path
  above already makes most pixels a basemap-like cost. The one case that could change this is the
  20° minimum pitch, where board ground reaches the horizon; the GPU readout at that pitch is the
  evidence to wait for. A cheaper hedge than a baked basemap: past a distance, skip the normal map
  and the anti-tiling fetch.

### (6) Tints

- A tint multiplies the **blended albedo, in linear space, before lighting**, so light, shadow and fog
  act on it as they act on the texture: `albedo = Blend(...) * _Tint.rgb`. Doing it after lighting
  would tint shadows and fog too. The linear-space warning in `docs/lessons.md` applies: a tint
  carried as a `Color` arrives linearised, so 0.68 on paper is about 0.42 in the shader.
- **Per-submesh tint keys multiply the draw count.** Today a tint key is a submesh (a bucket) and so
  a draw; on a skin, a chunk with daylit, tilled and stored cells would be three draws with three
  index buffers. The cell-varying keys (tilled, stored) are better carried as a **vertex colour on
  the quarter-cell quads**: a quarter quad lies wholly inside one cell, so the tint is crisp at the
  cell edge (the look a painted zone wants) and the chunk stays **one draw, one material**. The
  whole-chunk or global factor (daylit, the one-storey-down dim) stays a per-draw constant or a
  global. *Tilled* in particular may read better as a **layer swap** (the dirt or mud index) than as
  a tint, which the index layout gives for free.

### Per-pixel sample budget

| Path | Colour | Normal | Shared noise | Total |
|---|---|---|---|---|
| Uniform (one layer), XZ projection | 1 | 1 | 1 | **3** |
| Blend (up to four layers) | 4 | 4 | 1 | **9** |
| Blend + Quilez-3 per layer | 8 | 8 | 1 | 17 |
| Blend + triplanar | 12 | 12 | 1 | 25 |
| Blend + hex tiling | 12 | 12 | 0 | 24 |

No timing was measured in this lane; the GPU readout (`06-rendering-and-camera.md` §6c.5, 8.15–9.12
ms at 4K on the RTX 5070 Ti) is the instrument, and the target is a 2022 mid-range laptop.

## Recommendation

**A hand-written HLSL URP shader, one material for every chunk, sampling two Texture2DArrays
(colour BC1 sRGB, normals DXT5nm) filled at runtime by `Graphics.CopyTexture`, with four layer
indices per quarter-cell quad and weights computed from a local coordinate, height-blended on albedo
luminance, world-XZ projected, and broken up by shared macro-variation noise.** Hand-written rather
than Shader Graph because it needs coherent dynamic branching, `SAMPLE_TEXTURE2D_ARRAY_GRAD` and
exact control of the interpolators, and because the project's other world shaders are already HLSL.
It needs `UniversalForward`, `DepthOnly` and `DepthNormals` (the outline pass reads depth) and **no
`ShadowCaster`** (ground receives shadows and never casts them, `docs/lessons.md`). Light it with
`UniversalFragmentPBR` at the settings the current ground material uses, so the skin does not
change tone against the strata and walls beside it.

Ranked, for each decision:

1. **Weights:** per-vertex indices on quarter-cell quads (1st); per-pixel cell-ID map (2nd — four
   extra loads and a per-chunk binding, and it only wins if blends ever need to reach further than
   half a cell); a per-chunk RGBA splat texture in TerrainLit's style (3rd — four layers per chunk is
   not four per pixel, and more than four needs extra passes).
2. **Layers per pixel:** four on the blend path, one on the uniform path. Nothing more is reachable.
3. **Array detail setting:** 1024 as the default (20 MiB), 512 as the low rung (5 MiB), 2048 as the
   top (80 MiB), filled by mip offset. 512 is probably indistinguishable at the default zoom; the
   default stays 1024 until someone has looked at the closest zoom.
4. **Projection:** world-XZ everywhere (1st); a slope-gated second projection only where
   `normal.y < ~0.77` (2nd, only if the ramps are reported); triplanar (last).
5. **Tiling:** a 4 m repeat and two-scale macro variation from day one (1st); Quilez-3 on the
   uniform path only, behind the detail setting (2nd); hex tiling (last).
6. **Basemap:** none. **Holes:** none — they are geometry.
7. **Tints:** blended albedo x tint in linear before lighting; tilled and stored as vertex colour on
   the quarter quads; daylit as a per-draw or global factor.

**The one close call** is 512 against 1024 as the default. The cheapest experiment that settles it:
build both arrays and photograph the same board at the default pitch and the closest zoom at the
owner's 4K resolution (the `ReliefCheck` shot pattern), and read the GPU figure beside each.

## Sources

- Unity, `Graphics.CopyTexture` (6000.3): https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Graphics.CopyTexture.html
- Unity, `Texture2DArray` constructor (6000.3): https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Texture2DArray-ctor.html
- Unity, `Texture2DArray.Apply`: https://docs.unity3d.com/ScriptReference/Texture2DArray.Apply.html
- Unity Issue Tracker (titles only; pages no longer resolve): https://issuetracker.unity3d.com/issues/graphics-dot-copytexture-does-not-work-with-texture2darray-when-texture-quality-is-not-full-res and https://issuetracker.unity3d.com/issues/graphics-dot-copytexture-fails-to-copy-textures-into-a-texture2darray-for-certain-textureformats
- Unity, Terrain Lit shader, URP 16: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@16.0/manual/shader-terrain-lit.html
- URP 17.3 source, local package cache: `Library/PackageCache/com.unity.render-pipelines.universal@a8b4b2fc3560/Shaders/Terrain/TerrainLitPasses.hlsl` (`SplatmapMix`, `HeightBasedSplatModify`), `TerrainLitInput.hlsl` (`ClipHoles`), `TerrainLit.shader` (basemap dependencies)
- Unity, `NormalMapEncoding.DXT5nm`: https://docs.unity3d.com/ScriptReference/NormalMapEncoding.DXT5nm.html
- Inigo Quilez, texture repetition: https://iquilezles.org/articles/texturerepetition/
- Inigo Quilez, biplanar mapping: https://iquilezles.org/articles/biplanar/
- Mikkelsen, *Practical Real-Time Hex-Tiling*, JCGT 11(3), 2022: https://jcgt.org/published/0011/03/05/ and https://github.com/mmikk/hextile-demo
- Ben Golus, *Normal Mapping for a Triplanar Shader* (403 when fetched; cited for the whiteout/RNM reorientation from prior knowledge): https://bgolus.medium.com/normal-mapping-for-a-triplanar-shader-10bf39dca05a
- Project: `docs/lessons.md` (terrain materials, seamlessness, shadows, linear tints); `docs/design/06-rendering-and-camera.md` §2b; `Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Terrain/` (texture sizes, import settings, `.terrainlayer` tile size)

## Confidence

- **High**: the CopyTexture rules (GPU copy when non-readable, no conversion, block alignment); the
  pack's sizes, formats and 4 m tile size (measured); TerrainLit's splat, height, hole and basemap
  mechanics (read from the source); the four-layer maximum and the quarter-quad layout (geometry);
  the memory arithmetic.
- **Medium**: that the normals import as DXT5nm rather than BC5 on this project (the default, not
  read from the imported asset — log `texture.format` on first build); that the mip-offset copy
  behaves under a lowered Texture Quality; the screen-texel estimate (depends on the field of view,
  which was not read).
- **Low**: every cost statement in milliseconds — none was measured, and the ranking of macro
  variation over stochastic tiling is a judgement about what reads at 50–224 m that only a
  screenshot can confirm.

## Could not be determined

- The current status of the two CopyTexture-into-array issues (their tracker pages 404 after the
  migration); whether either still affects 6000.3.
- Whether `Texture2DArray.Apply(makeNoLongerReadable: true)` frees the CPU heap in 6000.3 (an old
  issue says it did not); avoided by never calling `Apply` and creating uninitialised.
- The actual GPU cost of the blend path on the target laptop, and the fraction of screen pixels that
  take the blend path on a played meadow (it decides whether the uniform branch is the win it
  appears to be). Both want a PlayMode timing with the path forced each way, read on the GPU figure.
- Whether albedo luminance is a usable height for every layer (grass over dirt should be; a dark
  moss over bright gravel may invert). A look at the boundaries decides it.
