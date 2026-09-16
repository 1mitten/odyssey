# d-12 — The URP 17 post-processing stack: cost, configuration, and a golden-hour tilt-shift

**Question.** What do URP 17's built-in post-processing effects cost and how are they configured, for a warm
golden-hour look with a subtle tilt-shift depth of field on a 2022 mid-range laptop GPU at 1080p?

A note on numbers before anything else. **Unity publishes no millisecond figures for any post-processing
effect**, and eight searches and seven page reads turned up no third-party measurement of the URP post stack
that was both recent and trustworthy enough to quote. Everything below that reads like a cost is either (a) a
statement the manual itself makes about relative cost, (b) an architectural fact about how many passes and how
much bandwidth an effect needs, from which relative cost follows, or (c) explicitly marked **no measured figure
found**. Odyssey already has the right instrument for the real answer — `FrameTimeTests` under the real player
loop, never an editor `camera.Render()` loop — and the recommendation at the end is written to be measured that
way, one effect at a time.

## Findings

### 1. Bloom

**Properties** (URP 17.3 / Unity 6000.3 manual, *Bloom Volume Override reference*):

| Property | Default | What it does |
|---|---|---|
| Threshold | 0.9 | Pixels dimmer than this contribute nothing. In HDR grading mode this is a scene-linear value, so "0.9" is roughly "brighter than white". |
| Intensity | 0 | Strength. **0 disables the effect entirely** — a Bloom override with intensity left at the default is a no-op, but still an override the volume system evaluates. |
| Scatter | 0.7 | How far the fringe spreads, by weighting the upsample chain. Costs nothing: it changes lerp weights, not iteration count. |
| Tint | white | Colourises the bloom. Free. |
| Clamp | 65472 | Caps the value a pixel may contribute *to the bloom maths* (the pixel still renders at full brightness). This is the anti-fireflies lever. |
| High Quality Filtering | off | Bicubic instead of bilinear on the upsample chain. The manual: "reduces flickering but increases processing demands. Disable on lower-end hardware for performance gains." |
| Filter | Gaussian | **New in the 17.x line.** Gaussian (best quality), **Dual** (faster, for mobile), **Kawase** (fastest, lowest memory). The manual recommends Dual or Kawase on constrained platforms. |
| Downscale | Half | Resolution the pyramid starts at. **Quarter** "significantly decreases processing load". |
| Max Iterations | 6 | Levels in the blur pyramid. Lowering it "improves performance, especially on high-DPI screens". |
| Lens Dirt texture / intensity | none / 0 | A full-screen overlay multiplied by the bloom. Composited in the uber pass, so nearly free once bloom runs — but it costs a texture fetch and a large texture in memory. |

**What scales it.** Bloom is the only common effect whose cost is *bandwidth in a chain of render targets*, not one
pass: a prefilter pass, then `Max Iterations` downsample passes, then the same number of upsample passes, all at
`Downscale` resolution and below. So the cost scales with (i) screen resolution × downscale factor — quarter
downscale is a 4× reduction in every target in the chain against half; (ii) iteration count, though each further
level is a quarter the area of the previous one, so iterations 4–6 together cost about a third of iteration 1 —
**cutting iterations is a much weaker lever than cutting the downscale**; (iii) the filter mode; (iv) high-quality
filtering, which changes a 4-tap bilinear upsample into a bicubic one (13-ish taps) on every level. The final
composite is *not* a separate pass: bloom is applied inside UberPost.

URP 17.3's changelog records a CPU-side bloom optimisation and per-feature shader-variant stripping (LQ, LQ Dirt,
HQ, HQ Dirt), which matters for build size and first-frame hitches rather than steady-state GPU time.

**Measured figures: none found.** A Unity Discussions thread complains about bloom's draw-call count, which is
the pyramid described above, but quotes no timing.

### 2. Depth of field, and whether either mode gives a tilt-shift band

**The two modes** (URP 17.3 manual, *Depth of Field Volume Override reference*):

*Gaussian* — Start (distance at which far-field blur begins), End (distance at which it reaches maximum), Max
Radius (default 1; "values above 1 can introduce visual under-sampling artifacts"), High Quality Sampling (off by
default; "reduces smoothness issues but incurs performance cost"). The manual describes it as the fast mode for
lower-end platforms. **It blurs the far field only.** There is no near-field term at all.

*Bokeh* — Focus Distance, Focal Length (mm; larger = shallower), Aperture (f-number; smaller = shallower), Blade
Count, Blade Curvature, Blade Rotation. It blurs **both** sides of the focus plane and is explicitly the slower,
higher-quality mode.

**Which one gives a tilt-shift band: Bokeh, and only Bokeh.** This is the decisive finding. A tilt-shift is a
*band* — sharp in the middle, blurred above **and** below. On a 48°-pitched camera over a flat board, screen
height does map monotonically to distance, so a depth-based effect can stand in for a tilted focal plane: the
bottom of the frame is near, the top is far. But that is exactly why Gaussian cannot do it. Gaussian leaves
everything nearer than `Start` perfectly sharp, so the bottom half of the frame — the near half — never blurs.
What you get is sharp foreground, sharp band, blurred distance: a haze-to-the-horizon effect, not a tilt-shift.
It is a reasonable look in its own right and it is the cheap one, but it is not the thing that was asked for.

**Levers for a tilt-shift with Bokeh.** Focus Distance is the centre of the band: set it to the distance from the
camera to the ground at screen centre, which for a fixed rig pitch is computable in the camera director and
wants recomputing whenever the camera's height or pitch changes, or the band slides off the middle of the frame.
Band *width* is Aperture and Focal Length together. The trap is that URP's Bokeh is physically parameterised and
your subject is 50–200 m away, where a real lens has effectively infinite depth of field — the physically honest
settings give you no blur whatsoever. You have to lie: a long Focal Length (start around 150–300 mm) and a small
f-number (start around f/1.4–f/2.8) to force a shallow band at board distances. Blade Count / Curvature /
Rotation shape the highlight discs and cost nothing extra; at golden hour with specular glints on water they are
what makes the blur read as a lens rather than as a smear.

**Cost.** Bokeh is a gather over a disc of samples per pixel at (typically) half resolution, plus a coc
(circle-of-confusion) pass and a composite; the sample count rises with the blur radius, so **a wide band is
cheaper than a narrow one** (less of the frame is blurred, but the blurred parts blur further — the dominant
term is the maximum radius, so keep the blur subtle and the cost stays modest). It needs the depth texture,
which URP will enable for you; if nothing else in the frame requested depth, that is a whole extra depth prepass
you are now paying for, and on a forward renderer that is not free. It is also **resolution-dependent in
appearance**: a Unity Discussions thread reports the same volume settings giving visibly stronger bokeh circles
at lower resolution, so settings tuned at 1080p will not look the same under a resolution-scale quality tier.
No measured millisecond figure found for either mode. (The one "10–15× slower" thread that search surfaces is
about a quality preset in a different pipeline and should not be quoted.)

**The third option, which is cheaper and more controllable than either.** Because the effect wanted is
*geometric* — blur as a function of screen Y — a custom fullscreen renderer feature that blurs by screen-space Y
with a two-pass separable blur gives an exact tilt-shift, needs no depth texture, has a cost independent of the
scene, and is trivially switched off in a low tier. It is more code than a volume override and it is a genuine
new renderer feature to maintain. It is the right answer if Bokeh measures badly or if the band refuses to stay
put as the camera moves.

### 3. Grading, tonemapping and the uber pass: what is free and what is not

URP composites a large part of the post stack in **one fullscreen shader, UberPost**, and bakes the whole colour
pipeline into **one small LUT** in a separate, screen-resolution-independent pass (`LutBuilder`, default LUT size
32 on the URP asset).

**Folded into the LUT bake — cost is independent of resolution and effectively constant, so these are free once
any grading runs, and free *of each other*:**
- **Tonemapping.** *Neutral* is "range-remapping with minimal impact on color hue & saturation" — the right base
  when you intend to grade heavily, and the one that will let a warm sun stay warm. *ACES* is "a close
  approximation of the reference ACES tonemapper, for a more cinematic look": more contrast, and it actively
  moves colour values. Its well-known behaviour on warm saturated highlights is **hue-skew towards white/yellow**
  — a saturated orange sun desaturates and drifts as it clips. For a deliberately golden look, ACES fights you
  and Neutral does not. (ACES HDR tonemapping is unsupported on Adreno 300-series Android GPUs; irrelevant here.)
- **Color Adjustments** (Post Exposure, Contrast, Color Filter, Hue Shift, Saturation). Post Exposure is applied
  in HDR before tonemapping and is the correct lever for overall brightness; Color Filter is the correct lever
  for a warm cast because it multiplies in linear space.
- **White Balance** (Temperature, Tint). Physically motivated warmth; a better first move than Color Filter if
  you want the *light* warm rather than the *image* tinted.
- **Shadows Midtones Highlights** — three-band colour and range control. The classic golden-hour move (warm
  highlights, cool shadows) lives here.
- **Split Toning** — a cheaper, cruder version of the same idea; largely superseded by SMH.
- **Channel Mixer, Lift Gamma Gain, Color Curves** — same bake, same non-cost.
- **Color Lookup (external LUT)** — also folded in, and the cheapest way to ship a fixed look. Author it once
  from the settings above and collapse the whole stack into one texture.

**Per-pixel in UberPost — cheap, but they scale with screen resolution:**
- **Vignette** (a couple of instructions), **Film Grain**, **Dithering**, **Chromatic Aberration**, **Lens
  Distortion**, **Lens Dirt**, the **bloom composite**, and the LUT application itself. **FXAA also runs here.**

**Genuinely separate passes:**
- **Bloom** (the whole pyramid, §1), **Depth of Field**, **Motion Blur**, **Panini Projection**, **SMAA** (three
  passes), **TAA**.

This is corroborated from the other direction by Unity 6.3's on-tile post-processing work, which can keep
"colour adjustments, tonemapping, vignette, film grain and dithering-related work" on-tile precisely because
they are per-pixel, and cannot keep bloom, depth of field, motion blur or chromatic aberration there because
they need neighbouring pixels. (That feature targets tile-based mobile GPUs and buys nothing on a laptop
discrete GPU.)

**The practical consequence: the entire golden-hour grade is one LUT bake and costs the same whether you use one
override or eight.** The only two effects on the shopping list that cost real time are bloom and depth of field.

### 4. Anti-aliasing at 1080p, with thin post-process outlines

From the URP 17.3 *Anti-aliasing* manual page:

- **FXAA** — "the least resource intensive anti-aliasing technique in URP". Per-camera setting. It runs inside
  UberPost, so it is close to free once any post-processing is on. It is a blur that finds edges by luminance
  contrast, and **it is the worst choice for thin outlines**: a one-pixel dark line on a light background is
  exactly the pattern FXAA softens into a two-pixel grey smudge.
- **SMAA** — "much sharper results than FXAA", more demanding. Per-camera. Three extra full-screen passes (edge
  detection, blending-weight calculation, neighbourhood blending) plus two lookup textures. It is pattern-based
  rather than a blur, so it preserves thin features far better. At 1080p on a 2022 discrete laptop GPU this is
  the usual sweet spot and is where I would start.
- **TAA** — smooths over time, "can produce ghosting artifacts during fast movement", and is **incompatible with
  MSAA, camera stacking and dynamic resolution**. It needs motion vectors and jitter. Two specific problems
  here: a jittered camera makes a one-pixel outline shimmer and then smears it, and Odyssey's world has a lot of
  instanced geometry that must report motion vectors correctly or it ghosts. High risk for the benefit.
- **MSAA** — set on the **URP asset**, not the camera, so it is naturally a per-quality-tier setting. "More
  resource intensive than other forms" on most hardware, cheaper on tiled GPUs *when there is no post-processing*
  — which is not our case. It solves triangle-edge aliasing only and **does nothing at all for a post-process
  outline**, because the outline is drawn after resolve. For a low-poly scene whose aliasing is mostly hard
  silhouette edges it is genuinely good-looking, and it composes with FXAA/SMAA, but paying for MSAA *and* a
  post AA pass is paying twice for overlapping problems.

Note that if outlines are drawn as a post pass, none of the spatial AA methods see the geometry that generated
them; they only see the drawn line. That argues for SMAA (which will treat the line as a feature to preserve)
over FXAA (which will treat it as an artefact to remove).

### 5. HDR vs LDR colour grading mode

Set on the **URP asset**, Post-processing → Grading Mode, alongside LUT Size (default 32).

- **LDR** bakes the grading LUT over the 0–1 range. Everything is clamped to display range *before* grading, so
  tonemapping has nothing left to tonemap and a bright sun is already flat white by the time the grade sees it.
  Bloom still works (it runs on the HDR colour buffer, before the LUT) but its threshold is effectively
  operating in a clipped world, so the *shape* of what blooms is coarser.
- **HDR** bakes the LUT in log-encoded HDR space, applies tonemapping as part of that bake, and keeps values
  above 1 intact through the grade. This is what makes Neutral or ACES mean anything at all, what lets Post
  Exposure behave like exposure rather than like brightness, and what makes a bloom threshold of 0.9 a
  meaningful "brighter than diffuse white" rather than "nearly clipped". It requires an HDR colour buffer (URP
  asset → HDR on) and slightly more memory for the LUT.

**For a golden-hour look, HDR grading mode is not optional.** The whole effect is a sun that is genuinely
brighter than the scene, blooming and rolling off warmly; in LDR it is a yellow filter over a clipped image.

### 6. A quality tier, driven from code

Four mechanisms, in the order they should be reached for. Odyssey generates its settings from editor code, so
the useful framing is "what does the generator emit".

1. **A URP asset per quality level.** Project Settings → Quality has a per-level Render Pipeline Asset slot that
   overrides the Graphics default while that level is active. In code: generate one
   `UniversalRenderPipelineAsset` per tier as an asset, and assign it to the level. At runtime,
   `QualitySettings.SetQualityLevel(level)` selects the level and with it the asset. This is the switch for
   MSAA, HDR on/off, LUT size, render scale, shadow distance and cascades — everything that lives on the asset.
   Two cautions from the docs: changing quality settings or URP-asset properties at runtime "causes a temporary
   but significant performance impact", so do it at a loading screen or a menu and never mid-play; and a known
   failure is the level change not taking because something else has written `GraphicsSettings.renderPipelineAsset`
   directly — if a tier switch appears to do nothing, that is the first thing to check.
2. **A VolumeProfile per tier.** There is **no built-in per-quality-level volume profile**; you wire it
   yourself. The clean generated shape is: emit one `VolumeProfile` asset per tier from the editor code, and a
   small component on the global volume that in `Awake` (and on a quality-changed event) assigns
   `volume.sharedProfile = profiles[QualitySettings.GetQualityLevel()]`. This is the switch for "no depth of
   field below High" and "quarter-downscale bloom on Low", and it keeps each tier's look readable as a single
   asset the owner can open.
3. **Scripted overrides on one profile**, when you want to poke a single value rather than swap a look:
   `volume.profile.TryGet<Bloom>(out var bloom)` then `bloom.active = false;` or
   `bloom.intensity.overrideState = true; bloom.intensity.value = 0.3f;`. **Use `volume.profile`, not
   `volume.sharedProfile`** — `profile` returns an instantiated runtime copy, `sharedProfile` mutates the asset
   on disk and your edit survives into the next editor session and into git. This is the mechanism for anything
   that must respond to game state (time of day, an indoor slice) rather than to a tier.
4. **Renderer features**: `ScriptableRendererFeature.SetActive(bool)` turns one off at runtime, but the feature
   list belongs to the *renderer* asset, which is referenced by the URP asset — so if a custom outline or
   tilt-shift feature should vanish on Low, the tidier generated answer is a renderer asset per tier (mechanism
   1) rather than runtime toggling of a shared one.

Per-camera settings (anti-aliasing mode, post-processing on/off, depth texture) are **not** on the URP asset and
so are not covered by mechanism 1; they are camera fields the rig sets, which for Odyssey means the camera
director reads the quality level and sets `cameraData.antialiasing` itself.

## Recommendation

**Ship one grade, two real effects, and one tier switch.**

*Pipeline (URP asset, High tier):* HDR on, **Grading Mode: HDR**, LUT size 32, MSAA off.

*Camera:* post-processing on, **anti-aliasing SMAA**, quality High. FXAA is rejected on the outlines argument,
TAA on the jitter-plus-outline and motion-vector risk, MSAA because it cannot help a post-drawn outline and we
would then be paying for two AA solutions.

*Volume, in the order they should be authored:*
1. **Tonemapping: Neutral.** Not ACES. The look is a saturated warm sun and ACES desaturates and hue-skews
   exactly that.
2. **White Balance:** Temperature +15 to +25, Tint slightly green-negative (towards magenta) if the grass goes
   sickly. This is the main warmth, and it is physically the right place for it.
3. **Color Adjustments:** Post Exposure +0.2 to +0.5, Contrast +5 to +10, Saturation +5 to +15, Color Filter
   left white at first — reach for it only if White Balance cannot get there.
4. **Shadows Midtones Highlights:** warm the highlights a little further, cool the shadows towards blue. This is
   what makes it read as low sun rather than as an orange filter, and it costs nothing.
5. **Vignette:** intensity ~0.25, smoothness ~0.4, rounded off. Nearly free, and it is half of what sells a
   tilt-shift.
6. **Bloom:** Threshold 1.0–1.1 (meaningful because grading is HDR), Intensity 0.15–0.3 — *subtle*, Scatter
   0.7, Clamp reduced to around 10–20 to stop specular glints on water fireflying, **Downscale Half, Max
   Iterations 5, High Quality Filtering ON at High tier**, Filter Gaussian. Tint very slightly warm.
7. **Depth of Field: Bokeh.** Focus Distance driven from code as the camera-to-ground distance at screen centre,
   recomputed when the rig's height or pitch changes. Start at Focal Length 200 mm, Aperture f/2.0, Blade Count
   6, Blade Curvature 0.7, and then **reduce the effect until it is nearly subliminal** — a strong tilt-shift on
   a colony sim reads as a toy diorama, which may or may not be the intent, and the cost falls with the radius.

*Steps 1–5 are one LUT bake plus a handful of instructions and should not move the frame time measurably.*
Measure them together as a single change. **Then add bloom alone and measure. Then add depth of field alone and
measure.** Those two are where the entire budget goes, and each has a live fallback: bloom to Downscale Quarter
and Filter Kawase, depth of field either to Gaussian (accepting far-fade instead of a band) or to a custom
screen-Y blur feature. The board currently runs 0.99 ms of a 5 ms budget, so there is room, but the target
machine is a 2022 mid-range laptop and not the RTX 5070 Ti these figures come from.

*The tier switch:* generate **two VolumeProfile assets and two URP assets** from the editor code, High and Low,
and assign the URP assets to quality levels. **Low differs in exactly three ways**: no Depth of Field override,
Bloom at Downscale Quarter with High Quality Filtering off and Max Iterations 4, and camera anti-aliasing FXAA
instead of SMAA. The grade is *identical* in both, because it is free and because the game should not change
colour when the player changes a setting. A component on the global volume assigns the profile from
`QualitySettings.GetQualityLevel()`, and the camera director sets the AA mode from the same number.

## Sources

https://docs.unity3d.com/6000.3/Documentation/Manual/urp/post-processing-bloom.html
https://docs.unity3d.com/6000.3/Documentation/Manual/urp/depth-of-field-volume-override-reference.html
https://docs.unity3d.com/6000.3/Documentation/Manual/urp/anti-aliasing.html
https://docs.unity3d.com/6000.3/Documentation/Manual/urp/post-processing-tonemapping.html
https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.3/changelog/CHANGELOG.html
https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/manual/quality/quality-settings-through-code.html
https://docs.unity3d.com/Manual/class-QualitySettings.html
https://thegamedev.guru/taskforce/2026-05-on-tile-post-processing-mobile-xr/
https://discussions.unity.com/t/the-urp-post-processing-bloom-has-too-much-draw-calls/767496
https://discussions.unity.com/t/bokeh-dof-appearance-changes-under-different-game-resolutions/874682
https://discussions.unity.com/t/depth-of-field-bokeh-vs-gaussian-what-should-i-use/831732

## Confidence

**Medium-high on configuration, low on cost.** Every property, default and mode above comes from the URP 17.3 /
Unity 6000.3 manual pages themselves, and the Gaussian-cannot-do-a-band conclusion follows directly from the
manual's own statement that Gaussian "only does far-field blurring" — that part is high confidence. The
pass-structure claims (what folds into UberPost, what folds into the LUT bake) are high confidence and
corroborated by Unity's own on-tile feature description, but they are inferred from architecture rather than
quoted from a single page. **No measured millisecond figure for any URP post effect was found at all**, so every
cost statement is relative, and the recommendation is deliberately written as a measure-one-at-a-time sequence
rather than as a prediction.

## Could not be determined

- Any measured GPU millisecond cost, on any hardware, for URP 17 Bloom, Bokeh DoF, Gaussian DoF, SMAA or the
  uber pass at 1080p. Nothing quotable was found.
- The relative cost of the three new Bloom filter modes (Gaussian / Dual / Kawase) beyond the manual's ordering
  of them.
- Whether URP 17's Bokeh mode adapts its sample count to the blur radius or uses a fixed count, which decides
  whether a *subtle* tilt-shift is meaningfully cheaper than a strong one. Reading the shader source in the
  package would settle it in minutes and no search will.
- Exact Focal Length / Aperture values that produce a given band width at a given camera height and pitch. This
  is a tuning problem for a contact sheet (a `PostCheck` harness in the style of `WaterCheck` / `ReliefCheck`),
  not a research one.
- Whether the Bokeh focus distance can be left static as the slice camera moves, or must be driven per frame.
- Default values for the Bokeh properties; the manual's reference table does not state them.
