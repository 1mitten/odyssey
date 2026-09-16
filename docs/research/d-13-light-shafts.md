# d-13 — Screen-space sun light shafts ("god rays") as a URP 17 Render Graph feature

**Question.** How should a screen-space sun light-shaft effect be built as a URP 17 renderer feature
under the Render Graph API, and what does it cost?

Asked against the feature we already have: `Assets/Odyssey/Presentation/Rendering/OutlineFeature.cs`,
and the Render Graph findings in `docs/research/d-10-outline-pass.md`.

## Findings

### 1. The technique, and the classic formulation

The whole effect is a **radial blur of a brightness mask, centred on the sun's screen position**. It
is Kenny Mitchell's *Volumetric Light Scattering as a Post-Process*, GPU Gems 3 chapter 13. The
shader is short enough to quote in full, and everything since is a variation on it:

```hlsl
float4 main(float2 texCoord : TEXCOORD0) : COLOR0
{
  half2 deltaTexCoord = (texCoord - ScreenLightPos.xy);
  deltaTexCoord *= 1.0f / NUM_SAMPLES * Density;
  half3 color = tex2D(frameSampler, texCoord);
  half illuminationDecay = 1.0f;
  for (int i = 0; i < NUM_SAMPLES; i++)
  {
    texCoord -= deltaTexCoord;
    half3 sample = tex2D(frameSampler, texCoord);
    sample *= illuminationDecay * Weight;
    color += sample;
    illuminationDecay *= Decay;
  }
  return float4(color * Exposure, 1);
}
```

The four knobs: **Density** sets the step length along the ray from the pixel to the sun (how far the
march reaches, and therefore how gappy it is); **Weight** is the per-tap gain; **Decay** is the
per-tap exponential falloff, in [0, 1], which is what makes the shaft dim with distance from the
sun; **Exposure** is the overall scale. `NUM_SAMPLES` was the thing that forced Shader Model 3.0 in
2007 and is free now.

**The mask.** The chapter gives three ways to obtain the occlusion image: a pre-pass that draws
occluders black against the lit sky, a stencil bit marking emissive regions, or simply reducing the
contrast of the scene image through fog. For us the pre-pass is the only honest one, and the modern
form of it is **depth, not colour**: a pixel is "sky" when its depth is at (or within epsilon of) the
far plane, and everything else is an occluder. That is one depth tap per pixel and needs no second
geometry submission — the depth texture already exists (see §2). Weighting the mask by the sun colour
is a multiply at the end; it can equally be done at composite time, which lets the mask be a scalar
R8 texture instead of a colour one.

**Two or three passes rather than one.** Unity's own legacy `SunShafts` (the Standard Assets image
effect, which is the most-copied implementation in this engine) does not run one big loop. It runs
`radialBlurIterations`, clamped to **1–4**, and each iteration is **2 × 6 = 12 samples**, with the
step offset growing between iterations. Because each pass blurs an already-blurred image with a
longer stride, three 12-tap passes reach roughly as far as a 12³-tap single pass at 36 taps of cost.
This is the standard trick and is why published implementations quote "2–3 passes of 8–16 taps".

**Composite.** Legacy SunShafts offers *Screen* (`1 - (1-a)(1-b)`) and *Add*. Screen is the subtler
of the two and cannot blow past white; Add is the literal reading of the GPU Gems shader, which
accumulates into the frame colour. Both are one multiply-add in a fullscreen pass.

**Kodeco's URP port** (pre-Render-Graph, but the structure is the reference most URP people have
read) follows exactly this: an off-screen "occluders map" painted with the light colour and the scene
in black, a radial blur from the light origin, a `resolutionScale` in [0.1, 1] on the off-screen
texture, and `intensity` / `blurWidth` at composite.

### 2. Render Graph plumbing in URP 17

**d-10's central finding holds and applies unchanged.** Every *material* overload of
`renderGraph.AddBlitPass` is an `AddUnsafePass` internally (verified in d-10 against the shipped
`RenderGraphUtilsBlit.cs`), which opts the pass out of the graph's resource tracking, merging and
memory aliasing. The pattern is `AddRasterRenderPass<PassData>` + `builder.UseTexture(...)` +
`builder.SetRenderAttachment(dest, 0, Write)` + `Blitter.BlitTexture(...)` in the render function —
precisely what `OutlineFeature.OutlinePass.RecordRenderGraph` already does, and what URP's own
`FullScreenPassRendererFeature` switches to whenever a pass declares an input. **Copy that file.**
One community taxonomy claims `AddUnsafePass` is needed "for different-size blits"; that is not so
for our case — a raster pass whose render attachment is a half-resolution texture simply rasterises
at half resolution, and `Blitter.BlitTexture` with a `(1,1,0,0)` scale-bias fills it. The unsafe
advice belongs to blits between targets of differing size *within one pass*, which we never need.

**Creating the half-resolution textures.** URP 17's manual is explicit: get a descriptor from an
existing handle and call `renderGraph.CreateTexture`, and **"avoid using
`UniversalRenderer.CreateRenderGraphTexture`, because it can introduce subtle bugs."** That is a
change from the advice the question assumes. The shape is:

```csharp
TextureDesc desc = resourceData.activeColorTexture.GetDescriptor(renderGraph);
desc.width  = Mathf.Max(1, desc.width  / 2);
desc.height = Mathf.Max(1, desc.height / 2);
desc.msaaSamples    = MSAASamples.None;   // d-10: GetTextureDesc inherits the source's sample count
desc.bindTextureMS  = false;
desc.depthBufferBits = DepthBits.None;
desc.clearBuffer     = false;
desc.format = GraphicsFormat.R8_UNorm;    // scalar mask; tint by sun colour at composite
desc.name = "Odyssey shaft mask";
TextureHandle mask = renderGraph.CreateTexture(desc);
```

(`renderGraph.GetTextureDesc(handle)`, which `OutlineFeature` uses, is the same thing by another
spelling; both appear in the shipped API. Lifetimes are the graph's problem, not ours.)

**Resources.** `frameData.Get<UniversalResourceData>()` gives `activeColorTexture`,
`cameraDepthTexture` and the `isActiveTargetBackBuffer` guard; `frameData.Get<UniversalCameraData>()`
gives `camera`, `GetViewMatrix()`, `GetProjectionMatrix()` and `cameraTargetDescriptor`. As in
`OutlineFeature`, set `requiresIntermediateTexture = true` and `ConfigureInput(
ScriptableRenderPassInput.Depth)`, declare the depth read with `builder.UseTexture(depth)` so the
dependency is visible to the graph rather than arriving as an untracked global, and finish by
assigning `resourceData.cameraColor = destination` so the next pass reads what we wrote.

**Passing the material's properties.** Static tuning constants (density, decay, weight, exposure,
tint) can be set on the `Material` in `AddRenderPasses`, as `OutlineFeature` does. **Per-camera
values must not be**: `AddRenderPasses` runs for every camera before the graph executes, so a second
camera overwrites the first camera's sun position on a shared material. Put those in `PassData` and
issue them from inside the render function with `context.cmd.SetGlobalVector(...)` (or a
`MaterialPropertyBlock` handed to a `DrawProcedural`), which is command-buffer-ordered and therefore
correct per pass.

**Injection point.** From d-10's verified enum values and the `CopyDepthMode.AfterOpaques` default:
the depth texture does not exist before `AfterRenderingOpaques` (300), and at 300 the skybox has not
been drawn, so there is no sky colour under the shafts and (for a colour-based mask) nothing bright
to smear. The earliest correct slot is **`AfterRenderingSkybox` (400)**. That is also before
transparents, so water and foliage composite over the glow rather than being washed by it — and it
is before our own outline, which sits at `BeforeRenderingTransparents` (450), so the ink is drawn
crisply *on top* of the shafts, which is the right order for an illustrated look. Running at
`BeforeRenderingPostProcessing` (550) instead would let bloom pick the shafts up, which some people
want; it would also put the glow over transparents.

### 3. When the sun is off screen or behind the camera

Two distinct failures, and they need different answers.

**Behind the camera is a hard cut.** Projecting a point behind the eye gives a *back-projected*
screen position — mirrored through the centre — and the blur then streaks towards a sun that is not
there. Unity's legacy `SunShafts` does exactly the right thing in one line:

```csharp
Vector3 v = camera.WorldToViewportPoint(sunTransform.position);
if (v.z >= 0.0f) { /* run the effect, _SunPosition = v */ }
else             { sunShaftsMaterial.SetVector("_SunColor", Vector4.zero); } // no backprojection!
```

For a *directional* light there is no position, so either take a point far along the ray
(`camera.transform.position - sun.transform.forward * farDistance`, then `WorldToViewportPoint`), or
project the direction itself: `clip = VP * float4(-L, 0)`; the sign of `clip.w` is the in-front test
and `uv = clip.xy / clip.w * 0.5 + 0.5` is the screen position. The direction form is the better one
— it has no arbitrary distance in it and `w` is exactly `dot(viewForward, -L)` up to scale, so the
test and the fade come from the same number.

**Off screen but in front is a guard band, not a clamp.** GPU Gems names the failure: as the sun
approaches the perpendicular the screen position "can tend toward infinity and therefore lead to
large separation between samples", and the suggested mitigations are clamping to a guard-band region
or rendering extended screen margins. Two consequences:

- the step `(uv - sunUV) / N * Density` grows without bound, so the 12 taps spread out, undersample
  the mask and produce visible banding long before the sun leaves the guard band;
- **hard-clamping the position onto the screen edge is worse than fading**, because the rays then
  pivot along the border as the camera turns, which reads as a bug.

The standard rule, and the one to use: allow the sun up to about half a viewport outside the frame
(so shafts genuinely enter from off screen), fade with a `smoothstep` over the next half viewport,
and cut to zero when `w <= 0`. A `saturate(dot(camForward, sunDirection))` term is often written
instead; it is the same quantity viewed through the camera's FOV, so use one or the other, not both.

**Project-specific, and this is the finding that matters most.** Odyssey's board camera sits at a
default **48° downward pitch** and the sun in the relief work is quoted at **72°** elevation
(`CLAUDE.md`, ground-relief entry, which also records that at that pitch the horizon is not in frame
at all — only ground 50–224 m away is). A sun 72° up and a camera looking 48° down means the sun is
roughly 120° off the view direction: **behind the camera, `w <= 0`, effect off**. Screen-space light
shafts are an effect for a camera looking towards a low sun. Before any of this is built, the cheap
experiment is to drop the sun's elevation and raise the camera pitch in `Play.unity` and see whether
a framing exists in which the sun is within the guard band *and* the board still reads. If no such
framing exists, the whole effect is unbuildable as specified and the shafts would have to come from
a different mechanism (billboard light-shaft quads through window openings, as in Cyanilux's
breakdown, which is geometry rather than post-processing and has no sun-on-screen requirement).

### 4. Fog, and a gradient skybox with no sun disc

**Fog is a reason to build the mask from depth rather than from the frame colour.** GPU Gems'
cheapest occlusion option is "reduce the contrast of the scene through fog", i.e. let distance fog
make far geometry bright so the blur smears it. That is the failure mode in disguise: with distance
fog on, a distant occluder converges to the fog colour, stops being dark, and stops occluding — so
the shafts leak through buildings at range. A binary `depth == far` sky test is immune to fog by
construction, costs one tap, and needs no colour texture at all. Composite additively over the fogged
picture afterwards; brightening fog near the sun is the physically right result and comes free.

**A gradient skybox with no sun disc leaves the mask with no bright source.** If "sky" is a uniform 1
across the whole visible sky, the radial blur produces an even wash with shafts only where occluders
cut it — which does read as light shafts, but it also smears the entire horizon and has no falloff
tied to where the sun actually is. Two fixes:

1. **Draw a sun disc in the sky shader.** Correct, and the player gets to see the sun. It costs a
   change to the skybox material, ties the effect's falloff to art rather than to a parameter, and
   for a camera that rarely has the horizon in frame, buys little.
2. **Synthesise the highlight in the mask pass.** `mask = skyTest * smoothstep(outer, inner,
   distance(uv, sunUV) * aspect)`. Two constants, no art dependency, no change to anything outside
   the feature, and it gives the effect an explicit falloff radius that can be tuned independently
   of how the sky is painted.

(2) is the one to build; (1) remains worth doing later for its own sake, and the two compose — a
drawn disc simply makes the synthetic highlight redundant in the pixels it covers.

### 5. Cost

**What is published.** Legacy SunShafts ran at divider 1 / 2 / 4 (full, half, quarter resolution) with
1–4 iterations of 12 samples, and shipped as a default-on effect on 2010-era hardware. The general
claim for the class is that because the result is soft and low-frequency, half-resolution costs
"4× or more" less with minimal quality loss.

**What I can estimate, and this is an estimate, not a measurement.** At 1080p, half resolution is
0.5 MPix. A depth-tap mask pass at that size is bandwidth-trivial, well under 0.1 ms anywhere.
Three blur iterations × 12 taps × 0.5 MPix ≈ 18 M texture fetches of an R8 texture with good
locality. On a desktop mid-range part that is of the order of **0.2–0.5 ms**; on the 2022 mid-range
*laptop* that is the stated performance target, **0.8–1.5 ms** is the honest range, and quarter
resolution roughly quarters the blur term. The full-resolution composite adds perhaps 0.1–0.2 ms.
So a half-res 3-pass effect plausibly fits a 2 ms allowance on the laptop and is comfortable on the
5070 Ti — **but no figure here is measured** and ADR 0005 leaves only about 3.8 ms of frame for all
rendering at a 3× hardware discount, with the board already at 0.99 ms. Measure with
`FrameTimeTests` under the real player loop, never with an editor render loop (`docs/lessons.md`,
"Benchmarking the renderer").

**The alternative, and why it is out.** True volumetric lighting — a froxel grid raymarched against
the shadow map, Frostbite-style, as URP asset-store products like BEAM and the MIT
`Unity-URP-Volumetric-Light` package do — is a different order of cost. Published figures: Unreal's
volumetric fog at High is ~1 ms on a PS4 and ~3 ms on a GTX 970 at Epic; a modern 4-pass froxel
pipeline targets under 1.5 ms at 1080p on an **RX 9070 XT**, i.e. a current high-end card. Scaled to
a 2022 mid-range laptop that is several milliseconds, it scales with froxel resolution *and* view
distance (and the froxel grid "can dominate the GPU frame budget if set too fine"), and the MIT
package's own README warns that cost climbs sharply with additional lights. Against a 2 ms allowance
and a 3.8 ms whole-rendering budget, froxel volumetrics is out. It buys things the screen-space
effect cannot do — shafts from point lights, shafts with no sun on screen, correct occlusion in
depth — and none of those are worth the frame.

### 6. Open-source implementations worth reading (clean room: we write our own)

- **`ryanslikesocool/URP-Sun-Shafts`** — MIT, a straight URP port of Unity's classic Standard Assets
  sun shaft effect. Archived December 2022, so it is **pre-Render-Graph** and its pass plumbing is
  obsolete for us; its value is the effect itself: resolution High/Normal/Low (full, ¼-size, 1⁄16-size),
  radial blur iterations, depth threshold, sun threshold, blur radius, Screen vs Add blend, and the
  `v.z >= 0` back-projection guard inherited from the original.
- **`CristianQiu/Unity-URP-Volumetric-Light`** — MIT, explicitly "render graph and compatibility mode
  support for Unity 6 and above". A *different* technique (shadow-map volumetrics, not radial blur),
  so read it for the Render Graph plumbing of a downsampled multi-pass effect, not for the maths.
- **`math-araujo/screen-space-godrays`** — not Unity (OpenGL), but a clean unencumbered reference for
  the occlusion pre-pass → radial blur → blend pipeline.
- **Kodeco, "Volumetric Light Scattering as a Custom Renderer Feature in URP"** — pre-Render-Graph
  tutorial, but the occluders-map / resolution-scale / intensity / blur-width structure is the one
  most URP implementations of this effect share.
- **URP's own `FullScreenPassRendererFeature.cs`** in the resolved package — the in-repo authority
  for the `AddRasterRenderPass` + `UseTexture` + `SetRenderAttachment` + `Blitter.BlitTexture` idiom,
  as established in d-10.

Clean-room rule stands: these are read for structure and for the names of the knobs. No code is
copied, MIT or otherwise.

## Recommendation

**Build it as one feature, `LightShaftFeature`, with four passes at half resolution, injected at
`AfterRenderingSkybox` (400) — but only after the sun-angle experiment in §3 says there is a framing
in which the sun is in front of the camera.** That experiment comes first because it can kill the
whole feature for the cost of dragging two sliders in `Play.unity`.

The design, committed to:

- **Resolution: half (÷2 in each axis).** Quarter is the fallback if measurement demands it; the
  effect is low-frequency enough to survive it, and legacy SunShafts shipped quarter as its "Normal".
  Not full: the blur is the whole cost and it scales with pixels.
- **Mask format: `R8_UNorm`, scalar.** Sky test from depth only (`Linear01Depth >= 1 - eps`),
  multiplied by a synthetic radial highlight centred on the sun (§4 option 2). Sun colour and
  intensity are applied at composite, so the mask carries one byte per pixel and no fog, no scene
  colour and no skybox art can get into it.
- **Blur: 3 passes of 12 taps** (2 × 6, the legacy layout), ping-ponging two half-res R8 textures,
  with the stride growing between passes so three passes reach far further than 36 taps would
  suggest. The GPU Gems loop with Density / Decay / Weight / Exposure, all four exposed on the
  feature.
- **Composite: a fourth full-resolution raster pass**, Screen blend by default with Add as an option,
  reading `activeColorTexture` and the final mask, writing a new texture, and assigning
  `resourceData.cameraColor = destination`. `requiresIntermediateTexture = true`, guarded by
  `isActiveTargetBackBuffer`, and `CameraType.Game` only — all exactly as `OutlineFeature` does.
- **Pass type: `AddRasterRenderPass` throughout**, never a material `AddBlitPass`; declare the depth
  read with `builder.UseTexture`; build descriptors from
  `activeColorTexture.GetDescriptor(renderGraph)` with `msaaSamples = None`, not from
  `UniversalRenderer.CreateRenderGraphTexture`, which the URP 17 manual tells you to avoid.
- **Fade rule, in one place:** project the light direction, `clip = VP * float4(-sunDirection, 0)`.
  If `clip.w <= 0`, record no passes at all — a sun behind the camera costs zero, not a black blit.
  Otherwise `sunUV = clip.xy / clip.w * 0.5 + 0.5`, and
  `intensity *= 1 - smoothstep(0.5, 1.0, maxOffscreenDistance(sunUV))` where the distance is measured
  in viewport units outside [0,1]. **Guard band, never a clamp.** The per-camera `sunUV` and
  intensity go through `cmd.SetGlobalVector` inside the render function, not onto the shared
  material.
- **Ordering:** 400 puts the glow under the outline (450) and under transparents, so ink stays crisp
  and water composites over the shafts. Do not move it to 550 unless the shafts are wanted in bloom.
- **Switched by `OdysseyBootstrap`**, off by default until measured, and measured by `FrameTimeTests`
  under the real player loop on both the meadow and the city.

**If the §3 experiment fails** — if no playable camera pitch puts the sun in front — do not build
this. The fallback is geometric shafts (billboard quads through openings, Cyanilux's method), which
is a different research question and does not depend on the sun being on screen.

## Sources

https://developer.nvidia.com/gpugems/gpugems3/part-ii-light-and-shadows/chapter-13-volumetric-light-scattering-post-process
https://www.kodeco.com/22027819-volumetric-light-scattering-as-a-custom-renderer-feature-in-urp
https://github.com/ryanslikesocool/URP-Sun-Shafts
https://github.com/CristianQiu/Unity-URP-Volumetric-Light
https://github.com/math-araujo/screen-space-godrays
https://github.com/h33p/Unity-Graphics-Demo/blob/master/Assets/Standard%20Assets/Effects/ImageEffects/Scripts/SunShafts.cs
https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph-create-a-texture.html
https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph-blit.html
https://www.cyanilux.com/tutorials/god-rays-shader-breakdown/
https://www.pinwheelstud.io/product/beam
https://irendering.net/exploring-volumetric-fog-in-unreal-engine/
https://gamedevllm.com/en/unity6-urp-render-graph-pass-types-en/
https://lousodrome.net/blog/light/2014/06/01/volumetric-light-scattering/

## Confidence

**High** on the algorithm and its parameters (quoted verbatim from GPU Gems 3 ch. 13), on the legacy
SunShafts structure and its `v.z >= 0` back-projection guard and 1–4 × 12-sample iteration scheme
(read from the shipped source), on the Render Graph pass-type rule (d-10 verified it against the
resolved package and nothing here contradicts it), and on the URP 17 manual's instruction to build
descriptors from `GetDescriptor` rather than `UniversalRenderer.CreateRenderGraphTexture`.

**Medium** on the injection point (reasoned from d-10's verified `RenderPassEvent` values and the
`CopyDepthMode.AfterOpaques` default, not from a shaft-specific source), on the guard-band-not-clamp
rule (GPU Gems names the problem and the mitigation but does not give the formula; the `smoothstep`
above is mine), and on the synthetic-highlight mask, which I found no published precedent for.

**Low** on every millisecond figure. The half-res 3-pass estimate is arithmetic on tap counts with no
measurement behind it, and the froxel comparison rests on Unreal and asset-store numbers from
different engines and different hardware.

## Could not be determined

- **Whether a usable sun framing exists for Odyssey's camera at all.** §3 argues it probably does
  not at the default 48° pitch with a 72° sun. This is the gating question and it is an experiment,
  not a search.
- **Any measured cost for this effect in URP specifically.** No source gave a millisecond figure for
  a URP sun-shaft feature at any resolution; the asset-store listings quote features, not timings.
- **Whether `ryanslikesocool/URP-Sun-Shafts` has a Render Graph branch.** Archived read-only in
  December 2022, well before Unity 6, so almost certainly not — but I did not read the source.
- **The exact fade curve used by shipping implementations.** Legacy SunShafts cuts hard on `v.z` and
  I did not establish whether it fades at all as the sun leaves the frame, only that it clamps
  nothing.
- **Whether the mask should be weighted by sun colour before the blur rather than at composite.**
  They differ only if the sun tint varies across the mask, which it does not for a single directional
  light; I found no source that cares, and picked the cheaper one.
- **How this interacts with URP fog on the skybox specifically** (whether URP fogs the skybox at all
  in 17.3). The depth-only mask makes the question moot for the mask, but not for how the composite
  reads against a fogged horizon.
