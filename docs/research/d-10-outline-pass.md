# d-10 — Screen-space outlines in URP 17 (Render Graph), and the thin-geometry problem

## Question

How do you implement a high-quality screen-space outline pass in Unity 6 URP 17 with the Render
Graph API, and specifically how do you stop thin or distant geometry from turning into a solid
block of outline?

Asked against the outline that already exists on `claude/m1-world`:
`Assets/Odyssey/Presentation/Rendering/OutlineFeature.cs` and
`Assets/Odyssey/Presentation/Shaders/OdysseyOutline.shader`.

## Findings

Everything marked **verified locally** was read out of the packages this project actually resolves,
under `Library/PackageCache/com.unity.render-pipelines.core@0bb36005e9ba/` and
`…universal@a8b4b2fc3560/`, not from a web page about a different version.

### 1. The Render Graph idiom for a fullscreen pass

**The resource flow in `OutlineFeature.cs` is right; the pass type is not.**

*Right:* create a destination from `renderGraph.GetTextureDesc(activeColorTexture)`, write into it,
then assign `resources.cameraColor = destination`. The URP manual names this explicitly as the way
to avoid a ping-pong: you cannot read and write the camera colour texture in the same pass, and
rather than blitting out and back, "you can update the frame data to point to the destination
texture instead, so you only blit once". `requiresIntermediateTexture = true` is also right and is
what makes the reassignment meaningful — against the back buffer there is nothing to reassign, and
a pass that reads and writes the back buffer is undefined behaviour.

*Not right:* `renderGraph.AddBlitPass(new BlitMaterialParameters(...))` is **not** a raster pass.
Verified locally in `core/Runtime/RenderGraph/RenderGraphUtilsBlit.cs`:

- the *copy* overload (line ~205) takes `AddRasterRenderPass`, with `SetInputAttachment(source, 0)`
  and `SetRenderAttachment(destination, 0, Write)` — merge-friendly, framebuffer-fetch friendly;
- **every material overload** (lines ~436 and ~1001) takes `AddUnsafePass`, with
  `UseTexture(source)` / `UseTexture(destination, Write)` and a manual `SetRenderTarget` inside the
  render function.

An unsafe pass is opaque to the graph's resource tracking, so it cannot be merged with its
neighbours and it blocks framebuffer fetch and memory reuse across the boundary.

The in-package proof of the preferred idiom is URP's own `FullScreenPassRendererFeature.cs`
(verified locally). When `input != ScriptableRenderPassInput.None` — i.e. exactly our case, we ask
for depth — it *abandons* `AddBlitPass` and instead opens
`renderGraph.AddRasterRenderPass<MainPassData>(...)`, calls `builder.UseTexture(...)` on
`resourcesData.cameraDepthTexture` and `resourcesData.cameraNormalsTexture` as required, calls
`builder.SetRenderAttachment(destination, 0, AccessFlags.Write)`, and in the render function calls
`Blitter.BlitTexture(cmd, source, scaleBias, material, passIndex)`. There is even a comment at line
250 explaining that `AddBlitPass` is dropped when the builder it returns is not capable enough.

**The dependency gotcha this exposes.** `ConfigureInput(ScriptableRenderPassInput.Depth)` makes URP
*produce* `_CameraDepthTexture` and bind it as a global. But a global texture is invisible to the
render graph's dependency tracking: our pass never declares that it reads the depth texture, so the
graph does not know the copy-depth pass must precede it, and nothing stops the depth resource being
aliased out from under us. Today it works because `ConfigureInput` also pins the pass ordering; it
is nevertheless an undeclared dependency. `AddRasterRenderPass` lets you fix it with
`builder.UseTexture(resourceData.cameraDepthTexture)` (or the blunt
`builder.UseAllGlobalTextures(true)`, which exists — verified locally in `RenderGraphBuilders.cs`
line 341). `AddBlitPass` does not give you a builder capable of that without `returnBuilder: true`,
and even then you are decorating an unsafe pass.

**Two further defects in the current descriptor.** `GetTextureDesc(activeColorTexture)` copies the
source's MSAA sample count, so with MSAA on we allocate an MSAA destination for a resolve-style
blit. Set `desc.msaaSamples = MSAASamples.None` and `desc.bindTextureMS = false`. (Separately: MSAA
and a screen-space depth edge detect are a poor pairing anyway — the depth texture the detector
reads is the resolved one, so the line is aliased whatever MSAA does to the geometry under it.)

`_BlitTexture_TexelSize` **is** declared (`core/Runtime/Utilities/Blit.hlsl` line 20) and **is**
populated — URP's own `Bloom.shader`, `UberPost.shader`, `BokehDepthOfField.shader` and
`TemporalAA.hlsl` all use it. So the existing shader is fine on that point. Note only that it is the
*texture's* texel size, not the viewport's, so under render scale or dynamic resolution the line
width drifts slightly from the intended pixel count.

### 2. Would `_CameraNormalsTexture` contain our geometry?

**Yes — but the shader's comment reaches the right conclusion from the wrong premise, and the real
reason to stay depth-only is cost, not correctness.**

First, a factual correction to the comment at the top of `OdysseyOutline.shader`: our geometry is
*not* submitted from a camera-rendering callback. `OdysseyBootstrap.LateUpdate` (line 262) calls
`ChunkRenderer.Render`, which calls `Graphics.RenderMeshInstanced` (ChunkRenderer.cs lines 201, 471)
and `Graphics.RenderMesh` (lines 485, 499). That distinction matters, because the
`Graphics.RenderMesh*` family is not an immediate draw at all: Unity registers the draw for the
frame, calculates bounds for it, and — in the scripting docs' own words — "uses the bounds to cull
and sort all the instances of this Mesh as a single entity, relative to other rendered Meshes in the
scene". They enter the SRP's culling results and are emitted into RendererLists like any
`MeshRenderer`, which means they are drawn in *every* pass whose `LightMode` tag their shader
provides: `DepthOnly`, `DepthNormals`, `ShadowCaster`, `UniversalForward`. (This is unlike
`Graphics.DrawMeshNow` or a raw `CommandBuffer.DrawMesh`, which bypass culling and appear only where
you issue them.) Submitting from `LateUpdate` is if anything safer than a `beginCameraRendering`
callback, because it is unambiguously before culling for every camera in the frame.

So the DepthNormals prepass will pick our geometry up, **provided the material's shader has a
`DepthNormals` (forward) or `DepthNormalsOnly` (deferred) pass**. `MaterialCache` clones the pack
material and falls back to `Universal Render Pipeline/Lit` (MaterialCache.cs line 145), which has
one. Synty pack materials are ordinarily URP/Lit or URP/SimpleLit, which also have one — but this is
worth one check rather than an assumption, because the failure is silent and ugly: an object missing
from the prepass leaves stale or background normals in its pixels, which the edge detector then
reads as a *false* edge exactly on the silhouette you were trying to draw. Unity's own guidance is
blunt — add a DepthNormals pass "otherwise, if you enable features like SSAO, Unity might not render
objects correctly". The cheap in-editor test is to switch SSAO on in DepthNormals mode and look for
objects with no ambient occlusion.

Two things to know before using normals:

- **URP 17's `_CameraNormalsTexture` is world space, not view space.** Verified locally in
  `universal/ShaderLibrary/DeclareNormalsTexture.hlsl`: `SampleSceneNormals` unpacks
  `_GBUFFER_NORMALS_OCT` into `octNormalWS`. Roystan's grazing-angle formula (below) is written for
  view-space normals from the built-in pipeline and must be adapted, or you must transform.
- **`ConfigureInput(ScriptableRenderPassInput.Normal)` forces a DepthNormals prepass for the whole
  frame, which is a full extra geometry submission over the entire visible world.** For this project
  that is the single most expensive line of code in the whole outline question. We are draw-call
  bound by construction — the chunk renderer exists to hold submission down — and a prepass doubles
  it. The existing shader's decision to stay depth-only is therefore correct, but the justification
  in its comment ("whether that reaches the prepass depends on the pack shaders having the pass at
  all") should be replaced with the real one: a normals prepass costs a second pass over every
  instance, and we cannot afford it for a crease line.

### 3. Why thin and distant geometry becomes a solid block, and the fixes

The diagnosis in the current shader's comment is exactly right and worth keeping: *the geometry is
smaller than the detector*. A tuft 0.5 m across at 60 m is perhaps three pixels wide; the Roberts
cross at `_Thickness = 1.2` compares pixels two apart; every pixel of the tuft therefore has a
neighbour on the background, so every pixel is an edge, so the tuft is filled. No threshold tuning
fixes this, because the depth step is genuine — it is the same step a building's silhouette makes.

The five standard remedies, in the order they occurred to the literature:

**(a) Distance fade.** `edge *= 1 - smoothstep(fadeStart, fadeEnd, eyeDepth)`. This is what the
shader now does. It is the blunt instrument: it is cheap (zero extra taps), it matches aerial
perspective so it looks defensible, and it is what the commercial edge-detection assets ship as
"Fade In Distance" with a start point and a steepness. Its cost is that it cannot distinguish a
distant tuft from a distant *building*, and in a top-down colony sim at a zoomed-out camera the
whole board is "distant", so the setting that kills the grass also kills the outline that is the
point of the effect.

**(b) Depth thresholds that scale with distance.** Already present (`_DepthThreshold * centre`).
Necessary — a fixed tolerance makes the far half of a flat field trip everywhere, because one pixel
spans more metres the further away it is — but not sufficient, and it is not the thin-geometry fix.

**(c) Grazing-angle / normal-aware thresholds.** The ground plane seen at a shallow angle has a
large depth delta between adjacent pixels even though it is flat, so it self-outlines. Roystan's
formulation, which is the one everybody copies:

```
NdotV              = 1 - dot(viewNormal, -viewSpaceDir)
normalThreshold01  = saturate((NdotV - _DepthNormalThreshold) / (1 - _DepthNormalThreshold))
normalThreshold    = normalThreshold01 * _DepthNormalThresholdScale + 1
depthThreshold     = _DepthThreshold * centreDepth * normalThreshold
```

with `_DepthNormalThreshold ≈ 0.5` and `_DepthNormalThresholdScale ≈ 7`. Surfaces facing the camera
keep a tight threshold; surfaces at an oblique angle get up to 7× slack. It needs the normals
texture (see §2) and it is written for view-space normals.

The stricter variant is the **depth-plane test**: reconstruct the centre pixel's world position `P0`
and its normal `N0`, reconstruct each neighbour's world position `Pn`, and measure
`abs(dot(Pn - P0, N0))` — the neighbour's distance from the centre's tangent plane. A flat floor at
any angle gives zero; a real silhouette gives metres. This is the "reconstruct the expected depth
from the neighbour's normal" trick stated properly, it removes the magic 7, and it still needs a
normal for `N0`. It costs one inverse-view-projection transform per tap.

**(d) Silhouette-only (one-sided) edges.** A Roberts cross fires on *both* sides of a depth step, so
every silhouette is two pixels of ink — one on the object, one on the background. Restricting the
line to the nearer side halves the width and, more importantly for us, puts every inked pixel *on*
the object, where a size test can then reach it. This is what "outline-only rendering creates
outlines only around the silhouette where mesh edges meet the background" means in practice: compare
the centre's depth against the furthest neighbour, and keep the pixel only if the centre is in
front.

**(e) Screen-space size gating, and object-ID masks.** Fading by *screen-space feature size* rather
than by distance is the correct generalisation of (a): it suppresses a three-pixel tuft at 60 m and
a three-pixel railing at 6 m alike, while leaving a distant building fully outlined. I could not
find a named, citable implementation of this — the commercial edge-detection documentation offers
only distance fade and object IDs — so the formulation in the recommendation below is mine, and
should be treated as a proposal to be measured rather than a received technique. The alternative
that *is* standard is a mesh/object-ID buffer (the AquaPostOutline project does depth + normal +
mesh ID; Linework exposes "Object ID" rendering with "rendering layer(s)" assignment), which
sidesteps the question by letting you exclude grass by name.

### 4. Excluding specific objects from a screen-space outline

A screen-space pass has no idea what an object is, so exclusion always means getting a per-pixel
mask into the pass somehow. Four ways, cheapest first *for this codebase specifically*:

1. **Rendering layer mask plus a mask pass.** `RenderParams.renderingLayerMask` is a per-draw field,
   and `ChunkRenderer` constructs a fresh `RenderParams` for every submission (lines 188, 459, 481,
   494), so *tagging* the scatter costs one assignment. Turning the tag into pixels costs a
   `RenderObjects`-style pass filtered by rendering layer, drawing the tagged geometry into an R8
   target with a trivial unlit shader, which the outline then multiplies against. The cost is one
   extra geometry pass over *exactly the set you want to exclude* — and our excluded set, the grass,
   is the largest instance count in the scene. That is the wrong geometry to draw twice.
   (Note: `RenderParams`' default `renderingLayerMask` comes from
   `GraphicsSettings.defaultRenderingLayerMask`; worth confirming in the editor rather than
   assuming.)
2. **Stencil.** URP reserves most of the stencil buffer; bits 0–3 are documented as available for
   custom effects. Writing a stencil ref needs either a `Stencil` block in the material's shader —
   which means forking the Synty shaders, which the licensing rules make unattractive — or a
   `RenderObjects` feature with a stencil override, which again redraws the geometry. It also
   requires **Depth Priming Mode set to Disabled** on the renderer asset, or the stencil contents
   are not what you expect. Same geometry cost as (1), plus a project-wide setting change.
3. **An object/mesh-ID buffer.** The highest-quality outline source there is — ID discontinuities
   give you clean edges between two objects at the same depth and orientation, which neither depth
   nor normals can — and it subsumes exclusion (reserve ID 0 for "never outline"). The cost is a
   full extra render target and a pass to fill it, i.e. strictly more than (1) or (2).
4. **Don't exclude anything; fix the detector.** Free. See the recommendation.

### 5. `RenderPassEvent` ordering

The enum values, verified locally in `universal/Runtime/Passes/ScriptableRenderPass.cs`:
`BeforeRenderingPrePasses = 150`, `AfterRenderingPrePasses = 200`, `BeforeRenderingOpaques = 250`,
`AfterRenderingOpaques = 300`, `BeforeRenderingSkybox = 350`, `AfterRenderingSkybox = 400`,
`BeforeRenderingTransparents = 450`, `AfterRenderingTransparents = 500`,
`BeforeRenderingPostProcessing = 550`, `AfterRenderingPostProcessing = 600`.

Constraints, in order of how much they bind:

- **The depth texture must exist.** `CopyDepthMode` (verified locally in `UniversalRendererData.cs`)
  defaults to `AfterOpaques`. So the earliest an outline can read `_CameraDepthTexture` is
  `AfterRenderingOpaques` (300). Anything earlier reads stale or empty depth.
- **The colour under the line must exist.** At 300 the skybox has not been drawn, so background
  pixels still hold the clear colour and any line that strays onto them is laid over nothing. The
  earliest genuinely safe slot is therefore **`AfterRenderingSkybox` (400)**.
- **SSAO is not a factor.** SSAO runs as a prepass-adjacent pass before opaques and is consumed
  during opaque shading, so by 400 it is already baked into the colour. (Its exact injection event I
  did not verify; the ordering conclusion does not depend on it.)
- **Transparents are the real decision.** With `CopyDepthMode.AfterOpaques`, the depth texture
  contains *opaques only*. An outline at 550 (where the feature sits today) therefore draws ink from
  opaque silhouettes straight over any glass, water or particle in front of them. Running at **400,
  before transparents**, makes transparents composite over the ink, which is almost always what you
  want. The counter-argument for 550 is that you also want ink on *top* of transparents, in which
  case set `CopyDepthMode.AfterTransparents` so the detector can see them — at the cost of moving
  the depth copy later for everyone.
- **Post-processing.** 550 keeps the line out of bloom, which is the right call for an ink look and
  is what the current tooltip says. It is *before* upscaling and TAA, though: with render scale
  below 1, or FSR/STP enabled, a one-pixel ink line is drawn at render resolution and then blurred
  and thickened by the upscaler. Moving to 600 would give a crisp display-resolution line, but at
  600 the active target is typically the back buffer, where reassigning `resources.cameraColor` does
  nothing and read-modify-write of the target is invalid. **This tension is real and I have not
  resolved it**; it only bites once upscaling is turned on.

## Ranked recommendation

**1 (back this). Make the outline one-sided, then gate it on screen-space feature width — a
"sliver test" — and keep the distance fade only as a long-range fallback.**

The insight is that the current fade answers "how far away is this?" when the question is "how wide
is this on screen?". A width test answers the real question, costs four extra depth taps, needs no
normals prepass, no extra render target, no per-object tagging, and does the right thing for a
distant building (outlined) and a near railing (not) alike.

It only works if the ink is one-sided first. A Roberts cross fires on both sides of a depth step, so
half the ink around a tuft sits on the *background* pixels, where a width test on the tuft cannot
reach it. Restricting the line to the nearer side fixes that and independently gives a crisper line.

Replace the tail of `Frag` with this. I am confident in the maths; the constants are starting points
to be tuned on screen.

```hlsl
// --- unchanged: Roberts cross on eye depth, threshold scaled by distance ---
float centre    = EyeDepth(uv);
float gradient  = sqrt((d1 - d0) * (d1 - d0) + (d3 - d2) * (d3 - d2));
float threshold = max(_DepthThreshold * centre, 1e-4);
float edge      = saturate((gradient - threshold) / threshold);

// --- new (i): one-sided. Keep the ink only on the near side of the step. ---
// If the centre is the *background* of a silhouette, its furthest neighbour is no further
// than it is, so dMax - centre is about zero and the pixel is dropped. If the centre is on
// the object, dMax is the background depth and the pixel is kept.
float dMax  = max(max(d0, d1), max(d2, d3));
float inner = saturate((dMax - centre) / threshold);
edge *= inner;

// --- new (ii): sliver test. Drop ink on features narrower than 2 * _SliverRadius pixels. ---
// Sample at a radius wider than the detector. If BOTH opposite neighbours on an axis are
// clearly behind the centre, the centre belongs to a feature thinner than the sample
// diameter -- a tuft, a wire, the tip of a spike -- and no legible line can be drawn on it.
float2 r   = _BlitTexture_TexelSize.xy * _SliverRadius;   // try 3.0 px
float  tol = max(_SliverTolerance * centre, 1e-4);        // try 0.02, i.e. 2% of distance

float bL = saturate((EyeDepth(uv - float2(r.x, 0.0)) - centre) / tol - 1.0);
float bR = saturate((EyeDepth(uv + float2(r.x, 0.0)) - centre) / tol - 1.0);
float bD = saturate((EyeDepth(uv - float2(0.0, r.y)) - centre) / tol - 1.0);
float bU = saturate((EyeDepth(uv + float2(0.0, r.y)) - centre) / tol - 1.0);

// min() is "both sides are behind" (this axis is a sliver); max() is "either axis is".
float sliver = max(min(bL, bR), min(bD, bU));
edge *= 1.0 - sliver;

// --- keep the distance fade, but push it out to where nothing is legible at all ---
edge *= 1.0 - smoothstep(_FadeStart, _FadeEnd, centre);

scene.rgb = lerp(scene.rgb, _OutlineColour.rgb, edge * _OutlineColour.a);
```

Why each piece behaves:

- `saturate(x / tol - 1)` is zero while the neighbour is within one tolerance of the centre's depth
  and ramps to one over the next tolerance, so the test is smooth rather than a stair-step. Scaling
  `tol` by `centre` keeps it in "fraction of distance" units, the same trick the existing threshold
  uses and for the same reason.
- Because the ramp is signed by construction (`neighbour - centre`), a neighbour *nearer* than the
  centre yields a negative value and `saturate` clamps it to zero — so a pixel is never called a
  sliver because something is in front of it.
- At a screen edge the depth sample clamps and returns the centre's own depth, giving zero, so no
  suppression: the border behaves, and no extra UV clamping is needed.
- A convex corner of a large building is safe: one neighbour is background, the opposite neighbour
  is still the building, so `min()` is zero and the ink stays.
- As the camera zooms in and a tuft grows past about 6 px, its interior pixels stop having
  background on both sides and the outline returns by itself. That is the behaviour we want and is
  precisely what a distance fade cannot give.

**Cost.** Four extra `SampleSceneDepth` taps, taking the pass from five to nine, plus about a dozen
scalar ops. All taps are point samples of `_CameraDepthTexture` within a three-pixel radius, so they
are cache-coherent and this is a bandwidth-trivial change. I would *guess* well under 0.3 ms at
1080p on the 2022 mid-range laptop target, but that is a guess and **must be measured**, not least
because the tick budget in ADR 0005 leaves only 3.8 ms of a frame for all rendering at a 3×
hardware discount. The Render Graph Viewer plus a frame capture on the target class of machine is
the check. If it proves too expensive, drop to two taps (the horizontal axis only) — grass clumps
are roughly isotropic, so the horizontal test alone catches most of them.

**Do this at the same time, since it is free:** switch `AddBlitPass` for `AddRasterRenderPass` +
`builder.UseTexture(resourceData.cameraDepthTexture)` +
`builder.SetRenderAttachment(destination, 0, Write)` + `Blitter.BlitTexture` in the render function,
following `FullScreenPassRendererFeature.AddFullscreenRenderPassInputPass`. Keep
`requiresIntermediateTexture = true` and the `resources.cameraColor = destination` reassignment. And
set `desc.msaaSamples = MSAASamples.None`. That removes an unsafe pass from the graph and turns an
undeclared global-texture dependency into a declared one; it changes no pixels.

**2. Rendering-layer mask on the scatter plus a mask pass.** The certain fix — grass is excluded
because you said so, not because a heuristic caught it. Reach for it if (1) is measured and its
constants cannot be tuned to keep both "no grass blobs" and "buildings outlined at zoom-out". Cost:
one assignment in `ChunkRenderer.SubmitInstances`, one new R8 target, and one extra geometry pass
over the grass — the largest instance count in the scene, which is why it ranks second.

**3. Move the pass from 550 to `AfterRenderingSkybox` (400).** Independent of the thin-geometry
problem; do it when transparents start appearing, so they composite over the ink rather than under
it. Free.

**4. Normals.** Enables crease lines and Roystan's grazing-angle threshold, and would make the
depth-plane test possible. Costs a full DepthNormals prepass over the whole world. Do not do this
for the vertical slice.

**The tie-break, if (1) and (2) look close:** the observation that decides it is whether, at the
fully zoomed-out camera, a *building* silhouette survives the sliver radius that kills the grass.
The cheapest experiment is to expose `_SliverRadius` and `_SliverTolerance` on the feature, stand at
the zoomed-out framing over a meadow with one structure in it, and sweep the radius from 1 to 6 px.
If some radius gives clean building edges and no grass, take (1) and stop. If the grass only goes
when the buildings go too, the heuristic cannot separate them here and (2) is the honest answer.

## Sources

- https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph-blit.html — URP 17 blitting
  with the render graph API; "you cannot read and write the camera color texture in the same pass";
  the `frameData.cameraColor = destination` idiom; `returnBuilder`.
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Graphics.RenderMeshInstanced.html —
  bounds are used "to cull and sort all the instances of this Mesh as a single entity, relative to
  other rendered Meshes in the scene"; must be reissued every frame.
- https://gamedevllm.com/en/unity6-urp-render-graph-pass-types-en/ — safe vs unsafe pass taxonomy;
  `AddBlitPass` as unsafe and non-mergeable; back-buffer read/write and
  `requiresIntermediateTexture`. (Corroborated against the shipped package source below, which is
  the stronger evidence.)
- https://roystan.net/articles/outline-shader/ — Roberts cross on depth and normals; the
  grazing-angle threshold formula (`NdotV`, `_DepthNormalThreshold`, `_DepthNormalThresholdScale`)
  quoted in §3(c).
- https://ameye.dev/notes/edge-detection-outlines/ — discontinuity sources (depth, normals,
  luminance); the Roberts cross formulation; luminance weights.
- https://ameye.dev/notes/rendering-outlines/ — comparison of rim, vertex extrusion, blurred buffer,
  jump flood and edge detection; depth non-linearity and threshold modulation by depth.
- https://linework.ameye.dev/edge-detection/ — a shipping edge-detection feature's surface area:
  Depth / Normals / Luminance / Combined sources, rendering-layer selection, "Object ID" rendering,
  "Fade In Distance", thickness in pixels with resolution scaling.
- https://github.com/DumoeDss/AquaPostOutline — edge detection from depth, normal *and* mesh ID.
- https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/post-processing-ssao.html
  — the DepthNormals pass block generates `_CameraNormalsTexture`; add it or SSAO "might not render
  objects correctly"; the depth-only reconstruction fallback.
- https://docs.unity3d.com/Manual/urp/features/rendering-layers-introduction.html — rendering layer
  masks as filters on components and render passes.
- https://docs.unity3d.com/Manual/SL-Stencil.html and
  https://www.theslidefactory.com/post/see-through-objects-with-stencil-buffers-using-unity-urp —
  stencil bits 0–3 available in URP; Depth Priming Mode must be Disabled.
- https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.universal/Runtime/RendererFeatures/FullScreenPassRendererFeature.cs
  — the in-package reference implementation (read locally from the resolved package, see below).

Read out of the packages this project resolves, which is the authoritative version for us:

- `Library/PackageCache/com.unity.render-pipelines.core@0bb36005e9ba/Runtime/RenderGraph/RenderGraphUtilsBlit.cs`
  — copy path uses `AddRasterRenderPass` (~205); both material paths use `AddUnsafePass` (~436,
  ~1001).
- `…core@…/Runtime/RenderGraph/RenderGraphBuilders.cs` — `UseGlobalTexture` (326),
  `UseAllGlobalTextures` (341).
- `…core@…/Runtime/Utilities/Blit.hlsl` — `_BlitTexture_TexelSize` declared (20).
- `…universal@a8b4b2fc3560/Runtime/RendererFeatures/FullScreenPassRendererFeature.cs` — the
  `AddRasterRenderPass` + `UseTexture` + `SetRenderAttachment` idiom and the comment at 250.
- `…universal@…/ShaderLibrary/DeclareNormalsTexture.hlsl` — `SampleSceneNormals` returns world-space
  normals (`octNormalWS`).
- `…universal@…/Runtime/Passes/ScriptableRenderPass.cs` — `RenderPassEvent` values.
- `…universal@…/Runtime/UniversalRendererData.cs` — `CopyDepthMode` defaults to `AfterOpaques`.

## Confidence

**High** — that the material overloads of `AddBlitPass` are unsafe passes and that
`AddRasterRenderPass` + `SetRenderAttachment` is the preferred idiom (read in the shipped package,
and confirmed by URP's own feature choosing it whenever it needs depth or normals); that reassigning
`resources.cameraColor` is the documented pattern and that read-and-write of camera colour in one
pass is forbidden; that `_CameraNormalsTexture` is world space in URP 17; that
`Graphics.RenderMesh*` draws go through SRP culling and appear in every matching `LightMode` pass;
that the `RenderPassEvent` values and the `AfterOpaques` depth-copy default are as stated; that the
grazing-angle threshold formula is correctly transcribed.

**Medium** — that the sliver test behaves as described on our actual grass. The maths is sound and
each clause above is reasoned from first principles, but I have not run it, and the useful range for
`_SliverRadius` against the tuft meshes at our camera framing is unknown. Also medium: that Synty
pack shaders all carry a `DepthNormals` pass (likely, unverified); and that
`RenderParams.renderingLayerMask` defaults from `GraphicsSettings.defaultRenderingLayerMask`.

**Low** — the 0.3 ms cost estimate for four extra depth taps at 1080p on the target laptop class.
That is an informed guess with no measurement behind it, and ADR 0005's frame budget is tight enough
that it should not be trusted until profiled.

## Could not be determined

- **A citable precedent for screen-space feature-width gating.** Every source I read offers distance
  fade or object IDs; none described a width test. The formulation in the recommendation is mine and
  should be treated as a proposal to be measured, not as an established technique. If it fails, the
  fallback is recommendation 2.
- **Whether `Graphics.RenderMeshInstanced` draws are in the DepthNormals prepass, stated in so many
  words by Unity.** The scripting documentation describes culling and sorting but does not enumerate
  which passes are executed, and I found no issue-tracker entry either way. The conclusion in §2 is
  inference from the documented culling behaviour plus the general SRP RendererList model. It is
  cheap to settle empirically — enable SSAO in DepthNormals mode and look for chunk geometry with no
  ambient occlusion — and that check should be run before anyone builds on it.
- **SSAO's exact `RenderPassEvent`.** I could not find it set in `ScreenSpaceAmbientOcclusion.cs` by
  grep and did not spend a read on it. The ordering conclusion in §5 does not depend on the answer.
- **How to get a crisp, display-resolution ink line when upscaling (FSR/STP) or TAA is on.** At 550
  the line is drawn pre-upscale and gets blurred; at 600 the target is usually the back buffer,
  where the `cameraColor` reassignment does not apply. There is presumably an accepted answer and I
  did not find it within the search budget. It does not bite until upscaling is enabled.
- **Whether MSAA and this pass interact badly beyond the descriptor fix.** The depth texture the
  detector reads is the resolved one, so the line will alias regardless; whether URP does anything
  further on our behalf, I did not establish.
