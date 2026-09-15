# Lane B — How shipped 3D games read as hand-painted, and what of it we can afford

## Question

How do shipped 3D games that read as hand-painted or illustrated actually achieve that look, and
which of their techniques transfer to a top-down colony sim built from flat-shaded low-poly assets
drawn with `Graphics.RenderMeshInstanced` and no GameObject per cell?

Hard cap on this investigation: 12 searches, 10 page reads. Both were reached.

## Findings

### Guilty Gear Xrd — the only fully documented "3D that looks 2D" pipeline

Junya Christopher Motomura's GDC 2015 talk is published by Arc System Works as a full slide-by-slide
transcript, so this is first-hand and specific rather than reconstructed.

- **Outlines: inverted hull, deliberately not post-process.** "A second set of darker polygons are
  generated in the shader and are expanded in the normals direction." He states plainly that
  screen-space outlines were the common choice at the time and they rejected them, for two reasons:
  the hull previews correctly in the modelling viewport, and line *width* can be varied per vertex —
  including erased entirely — through the vertex shader, driven by a vertex-colour channel. That
  control is the whole point of the choice.
- **Inner lines: a UV trick, not a texture detail.** Interior lines (where one surface meets another)
  cannot come from a hull. They draw every line as an **axis-aligned beam** in the texture and lay
  the UV shell alongside it; how far the UV overlaps the beam sets the line's thickness. Axis-aligned
  pixel runs never alias, so the line stays crisp at any zoom. The cost is a wildly distorted UV
  layout, which they could accept "because we do not put any details on the texture."
- **Shading: a `step()` on three quantities, all of them hand-controlled.** Lit or unlit, nothing
  between. The only inputs are threshold, light vector and normal. Each one is seized: the
  **threshold** is offset by a vertex-colour channel (an artist-painted "this area occludes, so it
  shades early" mask — vertex colour, explicitly, because it is resolution-independent and gives
  instant viewport feedback); the **light vector** is per-character and hand-aimed at the idle pose,
  with no global lighting on characters at all; the **normals** are hand-edited on every major feature
  of the model, because "the slightest difference in the surface normal may end up as a huge blotch"
  under a hard threshold.
- **Colour: two flat lookup textures.** A base texture for the lit colour and a "tint" texture for how
  dark it goes when shaded; multiply for the shadow colour. Both are literally squares of solid colour
  used as lookups, with no image detail in them. Skin shadows get a red tint because light passes
  through flesh — the tint map encodes material translucency as a colour choice, not a BRDF.
- **Cost.** ~40,000 triangles per character, no normal maps at all, all meaningful data in vertex
  normals, vertex colours and UVs. This is affordable only because a fighting game draws two
  characters. The transcript contains no per-frame numbers.

### Sable — flat colour plus line, at open-world scale

- **Outlines with distance fade.** Objects are defined by thin black lines with "fading opacity" so
  lines dissolve with distance. The developers say this does two jobs at once: it restores the
  perspective cue that flat shading destroys, and it hides pop-in — "objects would just pop in, which
  was ugly and a real problem". This is the single most reusable idea in the whole survey.
- **Shading: essentially none.** Flat colour with occasional dotting, in the Mœbius comic idiom. They
  are explicit that this creates a depth-reading problem and that they solved it by *adding layers*
  rather than by adding shading: real light and shadow so the player can tell what sits on what
  ("having light and shadows helps players figure out where they sit on a surface"), a moonlight
  shadow specifically so the player character stays visible at night, and **per-biome distance fog**,
  which they call "really, really key" for mid-to-long range readability.
- **Ground.** Hand-drawn textures, made on iPads, plus a **noise-based shader** that automates rock
  scatter across the desert — cited as a major time-saver. Terrain itself came from MapMagic; roughly
  80% of environments were built in ProBuilder from modular pieces and the ProBuilder scripts were
  stripped from final assets to save memory. Colour, not geometry, carries mood: "we focused on using
  colors to create the moods".
- **Cost.** No published frame numbers.

### The Ghibli-in-UE4 breakdown (Kids With Sticks) — painterly without a single outline

The most useful negative result in the set. There are **no outlines at all**; the painterly read comes
entirely from material and lighting discipline:

- **Custom vertex normals** transferred from a proxy shape (Blender's DataTransfer modifier) so foliage
  clusters shade as one soft mass instead of as individual leaf cards. This is the same instinct as
  Xrd's normal editing, applied to vegetation.
- Fully **dynamic** lighting (movable sky light with distance-field AO, directional light with
  distance-field shadows) — nothing baked.
- **Highlight masking** for colour variation across vegetation, initially from a height mask.
- Deliberately **simplified leaf shapes**, not realistic ones; texture reuse planned into the UVs.
- Terrain used ordinary tileable blending — they explicitly relied on artist judgement rather than any
  procedural anti-tiling trick.
- **Colour grading** was done by putting frames side by side with Ghibli film references and correcting
  the value and colour differences. The painterly quality is asserted to come from material design,
  texture simplification and lighting, not from a dedicated NPR system.

### Screen-space edge detection (Alexander Ameye, URP) — the instancing-compatible outline

The reference implementation for outlines that do not touch geometry. A full-screen pass samples
depth, normals and scene colour, runs a **Roberts cross** operator over four diagonal neighbours, and
combines the three discontinuity signals with a max. Cost is one full-screen pass plus its prepass,
independent of scene complexity. Stated limitations: hard-coded thresholds, artefacts at grazing
angles, **no distance fade**, and **no per-object selection** — you cannot exclude an object from the
effect. The normals half requires a DepthNormals prepass that re-renders the world.

### Breaking up tiled ground (Inigo Quilez; Jason Booth)

Quilez ranks three anti-repetition methods by cost: per-tile random offset/mirror with border blending
(**4 fetches**); Voronoi-weighted stamping of randomly scaled and rotated copies, highest quality
(**9 fetches**); and indexing into eight virtual variants and interpolating between two using a
**low-frequency** index pattern (**2 fetches**). He singles out the last as the efficient one, and
specifically because the low-frequency lookup is cache-friendly. Jason Booth (author of MicroSplat)
writes the standard summary of Heitz & Deliot's 2019 tiling-and-blending stochastic texturing —
multiple samples blended with a histogram-preserving operator — which is what MicroSplat, Better Lit
Shader and Amplify implement; his article was behind a 403 and could not be read directly. The generic
production alternative, "macro/micro variation", is simply a second low-frequency noise multiplied
over the albedo, which costs one cheap sample and no structural change.

### Games with no usable published technique

- **Against the Storm.** Four searches produced nothing developer-authored about its rendering — only
  key art, a design review and business/scope postmortems. One search result attributed a render-texture
  pixelation plus chromatic-aberration comment to Eremite via an itch.io post; the attribution looks
  like a search-engine conflation with an unrelated project and I would not rely on it. Despite being
  the closest comparison in genre, it is not a technique source. Its *observable* look — heavy
  atmospheric fog, strong warm-key/cool-fill colour separation, and rain and mist volumes doing most of
  the mood work — is consistent with the Sable and Ghibli findings that fog and grading carry the load.
- **Timberborn** and **Dorfromantik.** Nothing beyond interviews and marketing; the Dorfromantik devlogs
  cover biomes and features, not shaders. Skipped per the brief.
- **Book of Travels.** Not investigated; budget exhausted.

## What transfers to Odyssey

**Does not transfer, and should be ruled out explicitly:**

- **Inverted-hull outlines.** A second, inflated draw of every object is exactly what a renderer built
  around `Graphics.RenderMeshInstanced` per (chunk, mesh, material) exists to avoid. Xrd could afford it
  for two characters; we cannot for tens of thousands of modules. This is already the reasoning recorded
  in `OdysseyOutline.shader`.
- **Per-vertex authored masks (Xrd's threshold offset, its line-width channel, the Ghibli normal
  transfer).** All of them are per-asset hand work on meshes we do not own and must not modify in
  place. Synty POLYGON meshes share one colour atlas with tiny UV swatches; there is no per-asset
  authoring budget and no one to do it.
- **Xrd's axis-aligned-beam UV trick.** It requires owning and distorting each asset's UV layout. Ours
  are fixed by the atlas — moving a UV shell moves it off its colour swatch.
- **Hand-painted per-asset textures (Sable's iPad art, the Ghibli leaf textures).** One shared atlas,
  no normal maps, and the licensed pack cannot be redrawn.
- **Stochastic texturing / texture bombing at 4–9 fetches per pixel.** Aimed at large tiling detail
  textures. Our ground is untextured flat colour per cell; there is no repeating *texture* to break up,
  only repeating *colour*. The expensive versions solve a problem we do not have.
- **Baked lightmaps (The Witness's approach, not read in detail here).** Ruled out by construction: the
  world is built and destroyed at runtime and drawn without GameObjects, so there is nothing stable to
  parameterise or bake.

**Transfers cleanly:**

1. **Screen-space outline with a distance fade.** Sable's fading opacity is the crucial addition to the
   textbook edge detect, and it is the one thing Ameye lists as missing from the standard
   implementation. The branch already has this: `OdysseyOutline.shader` is depth-only with
   `_FadeStart`/`_FadeEnd` in metres, and the shader's own header gives the right reason for skipping
   the normals buffer — a DepthNormals prepass depends on the *pack* shaders having that pass, which is
   not guaranteed for geometry submitted from a camera callback. The accepted cost is that two coplanar
   surfaces get no crease line; silhouettes, which are most of the look, all survive.
2. **Fog as a first-class art tool, tuned per biome/weather rather than set once.** Sable calls it "really,
   really key"; it is visibly the backbone of Against the Storm's mood. It costs nothing, it is the
   cheapest depth cue available to a flat-shaded scene, and it compounds with the outline fade — the
   line dies into the fog instead of stopping.
3. **Real shadows, kept, precisely because the shading is flat.** Both Sable and the Ghibli breakdown
   independently conclude that flat colour needs *more* honest light-and-shadow contact information, not
   less, so the viewer can tell what rests on what. In a top-down colony sim where the whole read is
   "which cell is this thing on", that argument is stronger for us than for either of them.
4. **Colour as the mood system, graded against reference.** Sable: colour makes the moods. Ghibli
   breakdown: put your frame next to the reference and correct the value and colour differences. Our
   equivalent is a LUT and a vignette in the URP volume, checked against `d-03-rendering.md`'s concept
   renders. One post pass, no per-object cost.
5. **Low-frequency macro variation over the ground — as per-instance colour, not as a texture sample.**
   This is Quilez's cheapest family of ideas (low-frequency index, cache-friendly, 2 fetches) reduced
   further to fit our pipeline: we already push a per-instance tint through `StuffPalette` /
   `MaterialCache`, so a value-noise field evaluated once per cell at mesh-build time costs **zero extra
   draws, zero extra texture samples and zero per-frame work**. Sable's "noise-based shader" for rock
   distribution and the Ghibli breakdown's "highlight masking" are the same move at the macro scale.
6. **Clumped scatter rather than uniform scatter.** Sable automated rock placement with noise and the
   Ghibli team deliberately simplified silhouettes; the shared lesson is that painted-looking ground is
   *clumped* — dense patches and bare patches — not evenly sprinkled. `GroundScatter.cs` is where this
   lives and it is a sim-side/mesh-build change, not a shader change.

**Deliberately not revisited:** banded/cel shading. Xrd shows what it costs to do properly (hand-edited
normals on every asset, per-object light vectors, no global lighting) and we can supply none of that.
The 2026-09-15 rejection in `06-rendering-and-camera.md` §1 stands, and this survey strengthens it: the
Ghibli breakdown reaches a painterly result with **no cel shading and no outlines at all**, purely
through lighting, colour and silhouette simplification.

## Ranked recommendation

**Back this one: low-frequency macro colour variation on the ground, delivered as per-instance tint.**

The outline (item 1) is already implemented on this branch, so it is not the *next* step. Of what
remains, macro ground variation is the highest value per unit of risk:

- The loudest non-painted signal in the current scene is 14,400 identically coloured grass cells per
  layer forming a visible lattice. No amount of outline, fog or grading fixes a flat field of one colour;
  they all make it more conspicuous, because the outline pass draws attention to the silhouette of a
  perfectly regular grid.
- It is the only item in the list that is free at runtime. It rides the per-instance tint path that
  already exists, so the draw count, the instance batching and the frame budget are all unchanged —
  which matters more here than anywhere, given that the sim already eats most of the frame (`ADR 0005`).
- It is the modern consensus answer across three independent sources (Quilez's low-frequency variant
  chosen for cache-friendliness, Sable's noise-driven distribution, the Ghibli breakdown's highlight
  masking), and the expensive members of that family are precisely the ones we do not need.

Ranked below it, in order: **(2) fog and LUT grading tuned together against the concept renders** —
larger perceptual jump than anything else, but it is art direction iteration rather than a technique to
prove, and it will read wrongly until the ground stops being uniform; **(3) clumped rather than uniform
`GroundScatter`** — same goal, more work, and it wants the macro noise field to exist first so the
clumps and the colour patches agree; **(4) crease lines from a normals buffer** — the remaining gap in
the outline, but it needs a DepthNormals prepass whose interaction with `RenderMeshInstanced` from a
camera callback is unproven, so it is a spike, not a step.

**Cheapest experiment that confirms it.** In `ChunkMesher`/`ChunkRenderer`, evaluate two or three
octaves of deterministic value noise per ground cell at a wavelength of roughly 8–20 cells, seeded from
the map seed, and fold the result into the existing per-instance ground tint as about ±8% value with a
small hue drift towards yellow-green on the highs and blue-green on the lows. Build the play scene,
screenshot from the standard three-quarter camera before and after, and put both next to the concept
render. Two things must hold: the lattice must stop being visible at normal play zoom, and the frame
time and instanced draw count must be **identical** to before — if either moves, the tint is not
travelling through the per-instance path and the approach is wrong. Half a day, no new passes, no new
assets, and it is trivially revertible because the noise field is one function.

## Sources

Read in full or in substance:

- <https://www.ggxrd.com/Motomura_Junya_GuiltyGearXrd.pdf> — Junya Christopher Motomura, "GuiltyGearXrd's
  Art Style: The X Factor Between 2D and 3D", GDC 2015, full speaker transcript (primary source).
- <https://www.arcsystemworks.com/guilty-gear-xrds-art-style-the-x-factor-between-2d-and-3d-talk-from-gdc-2015-is-now-available-online/>
  — Arc System Works' own release of the talk and handout.
- <https://www.gamedeveloper.com/marketing/how-shedworks-refined-the-art-of-sable-in-pursuit-of-readability>
  — Shedworks on outline opacity fade, flat-shading depth problems, shadows and per-biome fog.
- <https://unity.com/resources/shedworks-sable-modular-design-approach> — Sable's modular/ProBuilder
  workflow, hand-drawn textures, noise-based rock shader.
- <https://kidswithsticks.com/creating-stylized-art-inspired-by-ghibli-using-unreal-engine-4/> —
  custom vertex normals via DataTransfer, dynamic DFAO lighting, highlight masking, grading against
  film reference, no outlines.
- <https://ameye.dev/notes/edge-detection-outlines/> — Alexander Ameye, depth/normals/colour edge
  detection in URP with the Roberts cross; its stated limitations.
- <https://iquilezles.org/articles/texturerepetition/> — Inigo Quilez, three anti-repetition techniques
  with their fetch counts.
- <https://www.gamedeveloper.com/design/for-i-sable-i-developing-an-evocative-art-style-comes-first> —
  read, but design philosophy only; no implementation detail.

Consulted and found to contain no usable technique (listed so the next session does not repeat the
search): Eremite Games' site and the Against the Storm coverage on Game Developer and Wikipedia;
Mechanistry's Timberborn interviews; the Toukana Dorfromantik devlogs.

Referenced but not readable within the cap:

- <https://medium.com/@jasonbooth_86226/stochastic-texturing-3c2e58d76a14> — HTTP 403 on fetch;
  summarised from search results only, and Heitz & Deliot's underlying 2019 paper was not read.
- <https://shahriyarshahrabi.medium.com/creating-painterly-3d-scenes-preparing-assets-for-npr-8d6c726cc34f>
  — HTTP 403 on fetch; not used.
- <https://www.gdcvault.com/play/1022031/GuiltyGearXrd-s-Art-Style-The> — the talk video; the PDF
  transcript above was used instead.

Clean-room note: every technique above is described from published developer prose. No code, shader,
texture, mesh or other asset from any of these products has been copied into this repository, and none
should be.

## Confidence

**High** for Guilty Gear Xrd and for the screen-space edge-detection technique: both come from primary,
developer-authored material read in full, and the Xrd transcript states its reasoning and its rejected
alternatives explicitly. **Medium** for Sable — the two Game Developer pieces and the Unity case study
are developer interviews with real technique in them, but nobody has published Sable's actual shaders,
so the outline implementation is inferred from behaviour. **Medium** for the ground recommendation: the
principle (low-frequency variation beats uniform colour, and the cheap variants are the ones worth
having) is well sourced, but no shipped game in this survey published the specific per-instance-tint
form I am recommending — that adaptation is ours and is the reason the experiment exists.
**Low-to-none** for Against the Storm, the closest genre comparison, which has published nothing
technical at all.

## Could not be determined

- **Against the Storm's actual rendering.** Whether it uses screen-space outlines, baked or dynamic
  lighting, per-instance colour variation, or a render-texture downscale is unknown; no developer-authored
  material was found in four searches. The one specific claim encountered is probably misattributed.
- **Any per-frame cost figures.** None of the sources published millisecond costs, draw counts or
  target hardware for the techniques described — including the Xrd transcript, which gives triangle
  counts but no frame budget. Our own 2022 mid-range laptop target therefore has to be measured, not
  inferred.
- **The cost of a URP full-screen edge detect at our resolution on that laptop**, and in particular
  whether a DepthNormals prepass fires at all for geometry submitted via `Graphics.RenderMeshInstanced`
  from a camera callback. This is the specific unknown blocking crease lines (recommendation 4) and it
  needs a spike, not a search.
- **Timberborn, Dorfromantik and Book of Travels** — no published technique found or, for Book of
  Travels, not investigated before the cap was reached.
- **Whether Heitz & Deliot tiling-and-blending would help us at all** once the ground carries a texture
  rather than flat colour. Probably relevant later; not answerable now, and the source was unreadable.
