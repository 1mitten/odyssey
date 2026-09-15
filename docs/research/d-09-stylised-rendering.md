# d-09 — Stylised rendering: making low-poly 3D read as hand-drawn 2D

*Lane D, wave 3. Researched 2026-09-16. Caps: 14 web searches, 12 page reads. Both respected.*

## Question

Which rendering techniques turn low-poly 3D assets into something that reads as a hand-drawn or
painted 2D illustration, in Unity 6 URP 17, and what does each cost per frame — judged against
this project's three hard constraints: the world is drawn with `Graphics.RenderMeshInstanced`
(tens of thousands of instances, no GameObject per cell), the art is Synty POLYGON (flat-shaded
geometry wearing one shared colour atlas, no normal or roughness maps, no usable per-object UV
space), and the per-frame budget is a few milliseconds on a 2022 mid-range laptop.

## Findings

### 0. The target look decomposes into five separable effects

Reading the concept image against what a renderer can actually do, the "storybook panel" quality
is not one technique. It is five, and they are independent enough to be costed and shipped
separately:

| # | Element of the look | Rendering concept |
|---|---|---|
| 1 | Dark ink line on everything | Edge detection or silhouette extrusion |
| 2 | Flat shading, few gradients, hard terminator | Banded / ramp / cel lighting |
| 3 | Painterly ground: colour variation, clumping | World-space macro colour breakup |
| 4 | Soft darkening in creases and at contacts | Ambient occlusion, used as ink not as light |
| 5 | Warm saturated palette, paper feel | Colour grading (LUT) plus a grain overlay |

Ranking them by how much of the illustrated quality they carry per millisecond is what the
recommendation section does. The short version is that 1, 4 and 5 are screen-space and cheap;
2 is a shader change and is the most work; 3 is nearly free but only where we own the shader.

Note the repository's current position: `06-rendering-and-camera.md` §1 recorded a decision on
2026-09-15 to reject banded lighting, on the grounds that it would mean replacing the pack shaders
everywhere and reconciling bands with the depth-darkening that marks layers below the slice. The
first of those two objections is weaker than it looked at the time — see §2.2 below, the material
choke point already exists — but the second stands and is a real design problem, not a technical
one. This note does not overturn the decision; it prices it properly so the decision can be
revisited on evidence.

---

### 1. Ink outlines

#### 1.1 The four families, and why three of them are wrong here

Ameye's survey ("5 ways to draw an outline") is the clearest comparative source and matches what
the Unity forums report.

**Inverted hull / vertex extrusion.** Draw every object a second time, enlarged, back faces only,
in flat black, behind the first. Gives an authored, per-object, constant-width line that survives
at any distance and never disappears on thin geometry. **Disqualified here, explicitly.** It costs
a second draw of every object. In a renderer whose entire premise is one instanced call per
(mesh, material, chunk) bucket, this means roughly doubling the bucket count and the instance
submissions — the exact cost the presentation layer was built to avoid. It also needs smoothed
custom normals baked per mesh to avoid gaps at sharp corners, which would mean authoring changes
inside `Assets/Synty/`, which the licensing rules forbid. Two independent disqualifications.

**Geometry shader silhouette extraction.** Detect silhouette edges on the GPU and emit line
primitives. Costs a geometry-shader stage on every triangle in the world, is unsupported or
emulated on Metal and on mobile-class tile GPUs, and Unity has been steering away from geometry
shaders for years. Not viable at our instance counts.

**Blurred-buffer and jump-flood outlines.** Render silhouettes into a mask, expand it, composite.
These are the right answer for *selection highlights* — one or a handful of objects, wide soft
glow — and are worth remembering for the "selected pawn" and "blueprint under construction"
affordances in `09-ui-and-input.md`. They are the wrong answer for "a line on everything" because
the mask pass is itself a second draw of the outlined set. Jump flood is the cheaper of the two
when the line must be wide; a separable blur is O(2N) rather than O(N²) but still several passes.

**Screen-space edge detection.** One fullscreen pass, cost independent of scene complexity. This
is what the project already has (`Assets/Odyssey/Presentation/Shaders/OdysseyOutline.shader`,
`Assets/Odyssey/Presentation/Rendering/OutlineFeature.cs`) and it is the correct family. Everything
below is about improving it rather than replacing it.

#### 1.2 What an edge detector can key on, and what each source gives

The standard kernel is Roberts Cross (four diagonal taps, two differences) or Sobel (eight taps).
Roberts is cheaper and gives a thinner, more even, more ink-like line; Sobel is more robust to
noise and gives a slightly softer, thicker line. The existing shader already uses Roberts, which
is the right call for this look.

| Source buffer | Catches | Misses / artefacts | Extra cost |
|---|---|---|---|
| **Depth** | Silhouettes, object-against-background, anything at a different distance | Two coplanar surfaces meeting — no crease line. Grazing-angle floors trip it everywhere unless the threshold scales with depth. | Free: `_CameraDepthTexture` already exists |
| **Normals** | Creases: wall/roof joints, the fold between a roof plane and a wall of the same colour, the top face of a crate against its side | False lines on smooth curvature; nothing on a flat painted seam | A DepthNormals prepass **or** reconstruction from depth (§1.4) |
| **Colour / luminance** | Painted boundaries, texture-atlas swatch changes, decals | Very noisy on a busy atlas; will trace every swatch boundary inside one mesh; sensitive to lighting, so lines swim as the sun moves | ~4 extra taps of the colour texture already bound |
| **Object ID / rendering layers** | Perfect object-vs-object separation, even at identical depth and identical normal — two colonists overlapping, a crate against a wall it touches | None, it is the ideal source | Requires an ID buffer. In URP 17 this is painful — see §1.5 |

For the target look, **depth + normals is the combination that matters.** Depth gives the
silhouette (most of the ink). Normals give the interior creases that make an illustration read as
drawn rather than as a photograph with a border. Colour/luminance is the one to leave off: on a
shared colour atlas it is a noise generator.

#### 1.3 Thin geometry — the grass-tuft problem, and the five standard fixes

We have already hit this: at board distance a grass clump becomes a solid dark blot, because every
pixel of a five-pixel-wide tuft sits on a depth discontinuity. This is not a threshold being too
low. It is the geometry being smaller than the detector's kernel. The kernel cannot tell "thin
object" from "edge of large object"; at some distance every object is thin.

The five fixes, in increasing order of effort:

1. **Distance fade on the line.** Fade the edge term to zero between a near and a far distance.
   Already implemented (`_FadeStart` / `_FadeEnd`). It is also the honest artistic answer, since
   aerial perspective in the reference art does exactly this. Cost: two instructions.
2. **Depth-scaled threshold.** Make the tolerance proportional to eye depth, so a fixed
   screen-space step counts as a larger world step further away. Already implemented. Necessary
   but not sufficient, because a thin object's *actual* depth step does not shrink with distance.
3. **Grazing-angle / depth-plane-aware threshold.** Compare the measured depth gradient against the
   gradient that a flat plane at this pixel's normal and depth *would* produce, and only call it an
   edge if it exceeds that. This is the standard cure for "the ground plane outlines itself towards
   the horizon", and it needs a normal, so it arrives with §1.4. Cost: a dot product and a divide.
4. **Thickness modulation.** Shrink the kernel radius (`_Thickness`) with distance so the detector
   stays proportionally smaller than the objects it is drawing. Cheap, and it fights the blot
   directly. It does mean the line thins out, which is acceptable and arguably correct. Shipped
   outline products expose exactly this as min/max thickness by distance.
5. **Exclude thin classes from the line entirely.** Grass, leaves and small scatter do not need an
   ink outline; in the reference illustrations they read as texture, not as drawn objects.
   Excluding them properly requires a per-object mask, i.e. §1.5, which we cannot afford. A cheap
   approximation without an ID buffer is a stencil bit written by the scatter draws.

Recommendation on this specific artefact: fixes 1, 2 and 4 together, plus 3 once normals exist.
Do not reach for an ID buffer to solve grass.

#### 1.4 Getting normals without a prepass — the important finding

The current shader's header comment says a normals texture requires a prepass that re-renders the
world, and that it is unclear whether `Graphics.RenderMeshInstanced` reaches it. Two corrections
and one route around the problem:

**(a) The pack shaders do have a DepthNormals pass.** `Assets/Synty/PolygonGeneric/Shaders/
Generic_Basic.shadergraph` is a Shader Graph asset built on `UniversalLitSubTarget` with
`m_AlphaClip: true`. The URP Lit subtarget generates `DepthOnly`, `DepthNormals`, `ShadowCaster`,
`GBuffer` and `Meta` passes automatically. The fallback path in `MaterialCache.cs` and
`ModuleLibrary.cs` is `Universal Render Pipeline/Lit`, which likewise has one. So the *shader* half
of the objection does not hold.

**(b) Whether our draws reach a DrawRenderers-based prepass is the real open question,** and I
could not settle it from documentation. `RenderParams` exposes `layer`, `renderingLayerMask`,
`shadowCastingMode`, `receiveShadows` and `worldBounds` ("used to cull and sort the rendered
geometry"), which strongly implies these draws are collected into the culling results and drawn by
every `ScriptableRenderContext.DrawRenderers` call whose shader-tag filter matches — the same
mechanism that serves the forward pass, the shadow-caster pass and the DepthNormals prepass.
Unity's `Graphics.RenderMeshInstanced` documentation does not say so in those words, and the older
`CommandBuffer.DrawMeshInstanced` explicitly does *not* participate in per-pixel lighting or
shadows, which is a different API and a frequent source of confusion. Marked uncertain; the
recommendation section gives the five-minute experiment that settles it.

**(c) It may not matter, because normals can be reconstructed from the depth texture.** A
fullscreen pass can derive a per-pixel view-space normal from `_CameraDepthTexture` alone by taking
horizontal and vertical depth neighbours, reconstructing view-space positions and taking a cross
product. The naive three-tap version produces a visible seam at depth discontinuities — which for
an *outline* is harmless, because those pixels are already being drawn as a line. The improved
five-tap variant (sample both neighbours on each axis, keep the one whose depth is closer to the
centre) removes the seam for about four more taps. The depth texture is guaranteed to contain
exactly what was drawn, because URP produces it by copying the real depth buffer after opaques, so
this route sidesteps the prepass question entirely and adds no geometry submission at all.

**This is the single most valuable finding in this note.** It converts "normals-based creases" from
"needs a whole extra render of the world, and might not even work with our submission path" into
"about eight more taps inside a fullscreen pass we already run".

#### 1.5 Object-ID outlines in URP 17: effectively unavailable

URP does have a rendering-layers texture (`_CameraRenderingLayersTexture`, written by
`DrawObjectsAndRenderingLayersPass` as a second render target, exposed to a render pass as
`UniversalResourceData.renderingLayersTexture`). It would be the perfect outline source. Two
blockers as of URP 17:

- The renderer only generates it when the **Decal renderer feature** is enabled with "Use Rendering
  Layers" switched on. Turning on the pipeline asset's rendering-layers option alone does not
  produce it. So the price of an ID buffer is running the whole decal system.
- `ScriptableRendererFeature.RequireRenderingLayers()` — the call that would let a custom feature
  ask for the texture on its own — is `internal`. There is an open request to make it public.

The alternative, writing our own ID buffer with a `RenderObjects` feature and an override shader,
is a second `DrawRenderers` over the world: the disqualified cost from §1.1 wearing a different
hat. **Conclusion: do not plan on object-ID outlines.** Revisit if Unity makes
`RequireRenderingLayers` public.

#### 1.6 Render Graph status

Screen-space outlining is in good shape on Render Graph and the project's implementation is already
idiomatic: `RecordRenderGraph`, `frameData.Get<UniversalResourceData>()`,
`renderGraph.AddBlitPass` with `RenderGraphUtils.BlitMaterialParameters`,
`requiresIntermediateTexture = true`, `ConfigureInput(ScriptableRenderPassInput.Depth)`, and
reassigning `resources.cameraColor` to chain into the next pass.

**Almost every outline tutorial online predates this** and uses `Execute(ScriptableRenderContext,
ref RenderingData)`, `CommandBufferPool.Get`, `cmd.GetTemporaryRT` and `Blitter.BlitCameraTexture`
— the deprecated compatibility path. Treat any tutorial without `RecordRenderGraph` as a source of
*ideas about the shader*, never of pipeline code. Adding normals means one extra flag —
`ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal)` — if we take
the prepass route, or nothing at all if we reconstruct (§1.4c).

If we ever want this without C# at all, URP 17 ships the **Full Screen Pass Renderer Feature**
(properties: Pass Material; Injection Point ∈ {Before Rendering Transparents, Before Rendering Post
Processing, After Rendering Post Processing}; Requirements ∈ {None, Color, Depth, Normal, Motion,
Everything}; and an advanced Pass Index) driven by a **Fullscreen Shader Graph** (*Create → Shader
Graph → URP → Fullscreen Shader Graph*). Useful for letting the owner prototype a look in the
editor without a code round-trip. Note that "Requirements: Normal" is documented as *adding the
prepass*, so it carries the cost the reconstruction route avoids.

---

### 2. Banded / ramp / toon lighting

#### 2.1 What it actually takes in URP 17

URP has no switch for this. A cel look means replacing the lighting function: take `N·L` and the
main light's shadow attenuation, quantise it — either into N hard bands, or by using it as the `u`
coordinate into a small ramp texture ("ramp lighting") — and multiply by albedo. Ambient comes from
sampled spherical harmonics rather than a full GI term, which suits a flat look anyway.

Two routes:

- **Shader Graph with custom-lighting subgraphs.** Cyanilux's `URP_ShaderGraphCustomLighting` is the
  reference implementation and its current branch explicitly targets **URP 17.1+ / Unity 6000.1+**,
  with Main Light (direction, colour, shadows, cookies, layer test), Additional Lights (Forward+
  clustered path supported since 2022.2), per-pixel ambient SH, baked GI / shadowmask, fog, and a
  worked toon example using a ramp texture for the main light and banding for additional lights. It
  ships one known wrinkle: keywords declared in nested subgraphs must be copied onto the parent
  graph. Minions Art's Unity 6 update notes that the additional-light loop (`LIGHT_LOOP` macros)
  changed in Unity 6, so pre-Unity-6 toon shaders will not compile unmodified.
- **Hand-written HLSL.** More control, and the only way to get the slice depth-darkening into the
  same shader without fighting the graph. More maintenance.

#### 2.2 Applying it across the pack without editing thousands of materials

This is where the earlier objection softens. Three options, cheapest first:

1. **The material choke point we already own.** `MaterialCache.Get` in
   `Assets/Odyssey/Presentation/Rendering/MaterialCache.cs` already constructs every world material
   at runtime by copying a source material and setting properties, keyed by (base material, tint,
   emission, ghost). Nothing in `Assets/Synty/` is edited or forked. A toon variant means:
   construct the derived material from *our* shader instead of from the source, and carry over the
   properties that matter. The Synty graph's reference names are `_Albedo_Map` (the colour atlas),
   `_BaseColor`, `_Emission_Map`, `_Emission_Color`, `_Enable_Emission`, `_Alpha_Clip_Threshold`,
   `_Smoothness`, `_Metallic`, `_Normal_Map`, `_Normal_Amount`. Copying `_Albedo_Map` and
   `_BaseColor` preserves the art. **Cost: zero extra draws, zero extra passes, one shader to
   write.** This is the cheapest correct answer, and it is available because of a design decision
   already taken.
2. **`RenderObjects` renderer feature with Override Mode = Shader.** URP's Render Objects feature
   can replace the *shader* while keeping the material's properties (as opposed to Override Mode =
   Material, which replaces everything including the atlas). Documented caveat: shader override
   "sacrifices SRP Batcher compatibility" — irrelevant here, since instanced draws do not use the
   SRP Batcher path anyway. The real problem is that `RenderObjects` is an *additional*
   `DrawRenderers` pass, so unless it replaces the opaque pass it is a second draw of the world.
   And its filtering depends on the same unresolved question as §1.4b. **Not recommended.**
3. **Fullscreen posterise.** Quantise luminance after opaques. One cheap pass, no shader work — but
   it quantises *albedo* as well as lighting, so the atlas's colour swatches get crushed and
   re-quantised and adjacent swatches merge. It reads as a cheap filter, not as flat shading.
   **Rejected** except as a throwaway five-minute look-check.

#### 2.3 The design problem that remains

Banded lighting still collides with the depth-darkening used to show layers below the active slice
(ADR 0006, `06-rendering-and-camera.md`). If shading has four bands and the slice cue is its own
value multiplier, bands from different depths land on the same values and the slice cue stops
reading. The fix is to make the slice cue act on a different channel from the bands —
desaturation and a cool tint rather than a value multiply, or a fixed value offset applied *after*
quantisation so bands stay distinguishable. That is a design decision, not a research finding, and
it belongs in the ADR before any shader is written.

---

### 3. Painterly surface treatment

#### 3.1 Macro colour variation on the ground — cheap and high payoff

The ground in the reference reads as painted because its greens vary in hue and value over metres,
with visible clumping, rather than being one flat swatch repeated. The Synty atlas cannot supply
that: there is no UV space, each face points at a tiny swatch. The fix is to modulate albedo by a
**world-space** noise, which needs no UVs at all.

- For a *flat* ground — which ours is, by owner instruction, 120 × 120 grass — a single planar
  projection on world XZ is enough. One texture sample, or a few octaves of cheap value noise
  computed in the shader. Full triplanar (three samples blended by the surface normal) is only
  needed on walls and cliffs; it is 3× the sampling and can be reserved for terrain-like props.
- Two octaves at different world scales gives what reads as "clumping": a large slow one for
  patches of lighter and darker meadow, a small fast one for speckle. Tint-only modulation (multiply
  albedo by roughly 0.85–1.15, with a slight hue rotation towards yellow in the light patches) is
  enough; do not modulate normals, which would fight the flat-shaded look.
- **Cost: one to three texture taps inside the ground material's fragment shader.** Best
  value-per-millisecond item in the note. It can ship inside the toon shader from §2 at no
  additional pass cost. It only works where we own the shader, which after §2.2 option 1 is
  everywhere — so today it is gated behind that work rather than being free.

#### 3.2 Paper / canvas grain

A fullscreen multiply by a tiling grain texture, sampled in *screen* space, at low contrast. Costs
one tap in a pass we are already running. Caution: screen-space grain that does not move with the
camera reads as a dirty lens, and grain that moves with the world reads as texture on the object.
For a "comic panel" feel, screen-space and static is correct — it says "this is a printed page".
Keep the contrast at a few per cent or it becomes noise at high resolutions.

#### 3.3 Kuwahara filtering

The Kuwahara filter smooths while preserving edges; the anisotropic variant orients its kernel
along the local structure tensor and produces convincing brush strokes. It is the technique behind
most "oil painting" Unity and Godot demos, and Unity 6 URP render-feature ports exist.

**It is wrong for this project, twice over.**

- *Cost.* A useful anisotropic Kuwahara needs a structure-tensor pass, a blur of that tensor, and
  then a main pass sampling on the order of a hundred-plus texels per pixel across several sectors
  (implementations commonly fix the kernel size because the loop must unroll). Order-of-magnitude
  estimate at 1080p on a 2022 mid-range laptop: **several milliseconds**, against a total render
  budget of roughly 3.8 ms after the tick. Marked as an estimate — no published Unity benchmark
  was found.
- *It fights the ink line.* Kuwahara's whole job is to dissolve small-scale detail into flat painted
  regions. Applied before the outline it destroys the crispness the line depends on; applied after,
  it smears the line. The target look has *both* a hard ink edge and painted interiors, and
  Kuwahara delivers only the second while damaging the first.

Cross-hatching has the same structural problem in a different form: it wants per-object surface
parameterisation to keep hatch direction stable, which the atlas cannot provide, and screen-space
hatching swims as the camera moves. Both are shelved.

#### 3.4 Colour grading — nearly free, and underrated

Warm saturated greens and a unified palette come from grading, not from re-texturing. URP's Volume
system gives, at effectively no additional pass cost once post-processing is on (it folds into the
uber post pass): **Color Lookup** (a strip LUT authored in an image editor from a neutral
screenshot — the direct, artist-driven route to "match the concept image"), **Color Adjustments**
(post-exposure, contrast, hue shift, saturation), **Shadows Midtones Highlights**, **Split
Toning**, **White Balance**, **Tonemapping** (prefer Neutral over ACES for a stylised look; ACES
desaturates highlights in a way that fights a poster palette) and **Vignette**. Bloom should be
used sparingly or not at all — it is the main thing that makes a stylised scene read as "game
render" rather than "illustration".

**Consider doing this first, before any shader work.** It is the cheapest change in the note, it
moves the picture a surprising distance towards a concept render, and a LUT is something the owner
can author on Pop!_OS without touching code, which suits the division of labour.

---

### 4. Ambient occlusion as a stylisation tool

URP's SSAO renderer feature, read as an *ink* effect rather than a lighting effect, supplies the
soft darkening in creases and at contact points that makes objects sit on the ground in an
illustration. It is one of the fastest routes to "drawn" — the reference images have heavy contact
shading and almost no other gradient.

From the URP 17 reference page, with the settings that matter:

| Setting | Effect |
|---|---|
| **Source** | `DepthNormals` uses `_CameraNormalsTexture` from the DepthNormals prepass. **`Depth` reconstructs normals from the depth texture instead** — lower quality, and it **avoids the prepass**, which given §1.4b is the safe setting for us. |
| **Sample Count** | Going from 4 to 8 doubles the GPU load. Use 4. |
| **Radius** | Smaller is faster (better cache locality) *and* more stylised — a tight radius gives a contact line rather than general dimming. |
| **Downsample / Half Resolution** | Quarters the pixel count; documented as a very large saving. For a soft crease darkening, half resolution is visually free. |
| **After Opaque** | Documented as a large performance *cost* when enabled. Leave off unless measured otherwise. |
| **Intensity / Direct Lighting Strength** | Push intensity well past photographic values. This is ink, not occlusion. |

Estimated cost at 1080p on the target machine with Source = Depth, 4 samples, small radius, half
resolution: **roughly 0.3–0.6 ms** (estimate, not measured). Full resolution with 8 samples is
plausibly three to four times that and is not affordable.

One caveat worth checking on the day: SSAO with Source = DepthNormals requires the prepass, and if
our instanced geometry does not reach it, the world would receive no AO while GameObject props
would — a very visible bug. Source = Depth cannot have that failure mode, which is a second reason
to prefer it.

---

### 5. Instanced geometry and the prepass — the decision several choices hang on

- **Certain:** our instanced geometry writes to the depth buffer and therefore to
  `_CameraDepthTexture`. The existing depth-based outline works, and the grass artefact is proof
  that even ground scatter reaches the depth texture.
- **Certain:** the shaders involved (`Synty/Generic_Basic`, a Shader Graph URP Lit subtarget, and
  `Universal Render Pipeline/Lit`) contain `DepthOnly` and `DepthNormals` passes.
- **Uncertain:** whether `Graphics.RenderMeshInstanced` submissions are enumerated by
  `ScriptableRenderContext.DrawRenderers` and therefore drawn in the DepthNormals prepass, the
  shadow-caster pass, and any `RenderObjects` feature. The presence of `layer`,
  `renderingLayerMask`, `shadowCastingMode` and `worldBounds` on `RenderParams` argues yes, and the
  project already relies on `shadowCastingMode` working; but no documentation states it, and the
  older `CommandBuffer.DrawMeshInstanced` explicitly behaves differently.
- **Mitigation that makes the uncertainty not matter for the outline:** reconstruct normals from
  depth inside the fullscreen pass (§1.4c). Eight extra taps, no prepass, no dependency on how the
  geometry was submitted.

---

### 6. Cost summary

All figures at 1920 × 1080 on a 2022 mid-range laptop GPU. Everything marked (est.) is an
order-of-magnitude judgement, not a measurement; there is no published benchmark for most of these.
Total render budget after the ~1.4 ms simulation tick is roughly 3.8 ms at a 3× hardware discount,
so the affordable envelope for stylisation is on the order of **1.0–1.5 ms**.

| Technique | Passes added | Draw calls added | Cost (est.) | Payoff towards target |
|---|---|---|---|---|
| Colour grading / LUT via Volume | 0 (folds into uber post) | 0 | <0.1 ms once post is on | **High** |
| World-space noise on ground albedo | 0 | 0 | ~0.05 ms (1–3 taps in an existing shader) | **High** (ground only) |
| Depth-only edge detect *(shipped)* | 1 fullscreen | 0 | ~0.2–0.3 ms | High |
| + normals reconstructed from depth | 0 (same pass) | 0 | +0.1–0.2 ms | **High** — creases are what sells "drawn" |
| SSAO, Depth source, 4 samples, half-res | 1 (+ blur) | 0 | ~0.3–0.6 ms | **High** |
| Paper grain overlay | 0 (fold into outline pass) | 0 | ~0.05 ms | Medium |
| Banded / ramp lighting via MaterialCache | 0 | 0 | ~0 GPU; days of work; design conflict with the slice cue | **High**, highest effort |
| SSAO, DepthNormals source, 8 samples, full-res | 1 prepass + 1 + blur | **a full extra draw of the world** | 1.5–3 ms | High, unaffordable |
| `RenderObjects` override shader | 1 | **a full extra draw of the world** | scene-dependent | Medium, unaffordable |
| Inverted hull outline | 0 | **~2× every bucket** | scene-dependent, large | High, disqualified |
| Anisotropic Kuwahara | 3+ fullscreen | 0 | 3–8 ms | Medium *and it fights the ink line* |
| Screen-space cross-hatching | 1 | 0 | ~0.3 ms | Low — swims under camera motion |

---

## Ranked recommendation

Ranked by visual payoff towards the concept image divided by cost plus risk, under the three hard
constraints.

1. **Normals reconstructed from depth, added to the existing outline pass.** Zero new passes, zero
   new draws, no dependency on the prepass question, and it adds the one thing the current line
   cannot draw: the interior crease. A roof meeting a wall of the same colour at the same depth is
   currently invisible; in the reference it is a firm black line. It also unlocks the
   depth-plane-aware threshold (§1.3 fix 3), the principled cure for grazing-angle false lines.
2. **Colour grading via a Volume LUT.** Nearly free, no code, owner-authorable, and it is what
   actually delivers "warm saturated greens". Arguably should be done *first*, because it changes
   how every subsequent judgement is made — you cannot evaluate an ink colour against an ungraded
   picture.
3. **SSAO as ink:** Source = `Depth`, 4 samples, small radius, half resolution, intensity pushed
   past realism. Cheap, and contact shading is a large part of why illustrations read as solid.
4. **World-space noise on the ground albedo.** Highest payoff per millisecond, but it only affects
   the ground and it requires us to own the ground shader — which we do not yet, so it is gated
   behind item 6's groundwork rather than being free today.
5. **Paper grain overlay**, folded into the existing outline pass. Small, safe, and it is part of
   the difference between "3D render" and "printed page".
6. **Banded / ramp lighting.** The largest single step towards the target, and the largest effort
   and risk: a new lighting shader, the `MaterialCache` rewiring, and an unresolved design conflict
   with the slice-depth cue (§2.3). Worth doing; not worth doing next.
7. Everything else — inverted hull, object-ID outlines, Kuwahara, cross-hatching, `RenderObjects`
   override — is ruled out by the constraints, and §1.1, §1.5, §2.2 and §3.3 say why.

### Back one: build item 1, normals reconstructed from depth, inside `OdysseyOutline.shader`

Concretely: add a `ViewNormalFromDepth(float2 uv)` helper that takes the centre depth plus two
neighbours on each axis, keeps on each axis the neighbour whose depth is nearer the centre (the
five-tap variant, to kill the discontinuity seam), reconstructs view-space positions from depth and
the inverse projection, and returns the normalised cross product of the two differences. Run the
same Roberts Cross on that normal field, take `max(depthEdge, normalEdge)`, and expose
`_NormalThreshold` beside the existing `_DepthThreshold`. Keep `_FadeStart` / `_FadeEnd` and the
depth-scaled threshold; make `_Thickness` shrink with depth (§1.3 fix 4) so the grass blot is
attacked in the same change. No change to `OutlineFeature.cs` is needed beyond new property IDs —
the Render Graph plumbing already requests depth and already chains `resources.cameraColor`, which
is the correct URP 17 idiom.

### The tie, and the observation that breaks it

Items 1 and 2 are close: grading is cheaper and safer, creases carry more of the "hand-drawn"
signal. **The observation that breaks the tie is whether the current picture's failure is one of
*colour* or one of *line*.** Put the concept image and a current screenshot side by side,
desaturate both to greyscale, and compare. If the greyscale versions look similar, the gap is
colour and the LUT goes first. If the concept still reads as drawn in greyscale while ours reads as
a render, the gap is line and creases go first. Cost: one screenshot and two minutes in an image
editor, no code.

### The separate five-minute experiment worth running regardless

Settle §5's uncertainty once, because several future choices depend on it. Enable URP's SSAO
feature with Source = `DepthNormals`, open the Frame Debugger on the generated play scene, and look
at the DepthNormals prepass. If the chunk draws appear in it, `Graphics.RenderMeshInstanced`
participates in `DrawRenderers` passes, and both prepass-based normals and `RenderObjects`
filtering become available at their stated costs. If they do not appear, the world will be visibly
missing from `_CameraNormalsTexture` while GameObject props are present, and we are permanently on
the reconstruct-from-depth route. Either answer is useful; record it in this file.

## Sources

- https://ameye.dev/notes/rendering-outlines/ — five outline methods compared, with artefacts and draw-call implications
- https://ameye.dev/notes/edge-detection-outlines/ — Roberts Cross edge detection in URP, depth/normal/luminance thresholds, Render Graph vs the 2022 path
- https://docs.unity3d.com/6000.1/Documentation/Manual/urp/renderer-features/renderer-feature-render-objects.html — Render Objects feature: Override Mode Material/Shader, pass-name filtering, layer masks, event injection
- https://docs.unity3d.com/6000.1/Documentation/Manual/urp/renderer-features/renderer-feature-full-screen-pass.html — Full Screen Pass Renderer Feature properties, injection points, requirements
- https://docs.unity3d.com/ScriptReference/RenderParams.html — RenderParams fields: layer, renderingLayerMask, shadowCastingMode, receiveShadows, worldBounds
- https://docs.unity3d.com/6000.0/Documentation/Manual/urp/ssao-renderer-feature-reference.html — SSAO settings and their stated performance impact
- https://discussions.unity.com/t/requirerenderinglayers-camerarenderinglayerstexture/1705610 — rendering-layers texture gated behind the Decal feature; `RequireRenderingLayers()` is internal
- https://docs.unity3d.com/6000.1/Documentation/Manual/urp/features/rendering-layers.html — Rendering Layers overview
- https://github.com/Cyanilux/URP_ShaderGraphCustomLighting — custom lighting subgraphs for URP 17.1+ / Unity 6000.1+, toon ramp example, Forward+ support, subgraph keyword caveat
- https://github.com/GarrettGunnell/Post-Processing — anisotropic Kuwahara reference implementation
- https://github.com/TxN/UnityURP_Kuwahara — Kuwahara as a URP render feature
- https://flatkit.dustyroom.com/outlines/ — thickness modulation, depth masking, min/max depth and normal thresholds and distance fade as shipped product settings
- https://docs.unity3d.com/6000.0/Documentation/Manual/urp/whats-new/urp-whats-new.html — URP 17 Render Graph changes
- https://docs.unity3d.com/6000.1/Documentation/Manual/urp/writing-shaders-urp-depth-only.html — DepthOnly / DepthNormals pass requirements
- https://www.edraflame.com/blog/unity-graphics-drawmesh-drawmeshinstanced/ — Graphics.DrawMesh family behaviour (did not resolve the SRP pass question)
- https://www.patreon.com/posts/toon-shader-birp-59854502 — Minions Art toon lighting, noting the Unity 6 additional-light loop change

Local evidence used (not web): `Assets/Synty/PolygonGeneric/Shaders/Generic_Basic.shadergraph`
(UniversalLitSubTarget, alpha clip, property reference names), `Assets/Odyssey/Presentation/Rendering/MaterialCache.cs`,
`Assets/Odyssey/Presentation/Rendering/OutlineFeature.cs`,
`Assets/Odyssey/Presentation/Shaders/OdysseyOutline.shader`.

## Confidence

**Medium-high.** High on the qualitative architecture: the disqualification of inverted-hull and
per-object techniques follows directly from the instanced-submission constraint and is not
version-sensitive; the URP 17 API names, the Render Objects override semantics, the SSAO settings
and the rendering-layers-texture blocker all come from current Unity 6 documentation or a
Unity-hosted discussion; and the Shader Graph subtarget finding is read straight out of the asset in
this repository. Lower on the numbers: every per-frame cost in §6 is a reasoned estimate rather than
a measurement, because almost nobody publishes fullscreen-pass timings for a named GPU, and the only
figures Unity itself gives for SSAO are relative ("doubles the load", "very high impact"). The
recommendation does not depend on those estimates being right to better than a factor of two.

## Could not be determined

- **Whether `Graphics.RenderMeshInstanced` submissions are enumerated by
  `ScriptableRenderContext.DrawRenderers`,** and therefore whether they appear in the DepthNormals
  prepass and in `RenderObjects` features. Documentation is silent; the `RenderParams` fields argue
  yes. The Frame Debugger check above settles it in five minutes, and the recommendation is
  deliberately built not to depend on the answer.
- **Measured per-frame costs** for any of these passes on 2022 mid-range laptop hardware. No
  published benchmarks were found for URP fullscreen edge detection, URP SSAO at specific settings,
  or anisotropic Kuwahara in Unity. All §6 figures are estimates.
- **How Against the Storm, Book of Travels, Dorfromantik or Windbound achieve their looks.** No
  technical breakdown, conference talk or developer post-mortem surfaced for any of them — only
  art-side ArtStation pages. The techniques in this note are inferred from what the images show and
  from what is affordable, not from any of those teams' statements.
- **Whether URP 17 exposes any hook for replacing the lighting function globally** without either a
  second draw or per-material rewiring. Nothing found; §2.2 option 1 works around it by using the
  project's own material factory rather than a pipeline hook.
- **The exact cost of the Fullscreen Shader Graph "Requirements: Normal" option** relative to a
  hand-written prepass — the documentation says it adds the prepass but gives no figure.
