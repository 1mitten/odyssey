# d-14 — Warm aerial perspective in URP 17

**Question.** What is the best way in URP 17 to get warm aerial perspective — height/distance fog that
matches the sky horizon — across Lit materials, our custom water shader and the skybox, and what does
each option cost?

Written against the scene as it stands: a 300 m board at a 48-degree pitch, a decorative surround to
1,220 m, `Odyssey/GradientSky` (three colours, two falloffs, no sun disc), `Odyssey/Water`
(transparent, `multi_compile_fog` + `ComputeFogFactor`/`MixFog`), linear fog 460 m → 1,100 m kept
deliberately off the board, and a measured frame of 0.99 ms meadow / 1.56 ms city against a 5 ms
budget.

## Findings

### 1. Built-in `RenderSettings` fog — how it works, what it cannot do, and why exp2 is the right curve

**How it is applied.** URP has no fog pass. Fog is a *per-material* term compiled into every forward
shader that declares `#pragma multi_compile_fog`, which expands the keyword set
`_ FOG_LINEAR FOG_EXP FOG_EXP2`. The shader calls `ComputeFogFactor(positionCS.z)` in the **vertex**
stage, interpolates the scalar, and `MixFog(colour, fogFactor)` in the fragment stage lerps the shaded
colour towards `unity_FogColor`. The mode and colour come from
*Window → Rendering → Lighting → Environment*, i.e. from `RenderSettings.fogMode`, `fogColor`,
`fogDensity`, `fogStartDistance`, `fogEndDistance`. Unity 6 additionally lets the fog keyword set be
marked `dynamic_branch` in *Project Settings → Graphics → Shader Build Settings* to cut the variant
count, at a small cost on old mobile GPUs.

This is why `Odyssey/Water` already gets fog for free and why nothing needs doing to it: it declares
the pragma and calls both functions. It is equally why **anything that does not declare the pragma
will not be fogged and will pop out of the haze** — the chunk mesher's ground/water materials, the
`GroundScatter` grass tufts, the `ChipRecipe` particle material and the `TerrainSkirt` tiles must each
be checked. A Lit or Unlit URP material is fine; a hand-written shader is not unless it says so.

**What it cannot do**, all four relevant to the target look:

- **No height term.** Density is uniform in y. There is no base height, falloff or maximum height.
- **No sun-direction tint.** One flat `fogColor`; no inscatter lobe brightening towards the light, no
  second colour for the away-hemisphere.
- **The skybox is never fogged.** URP's skybox pass does not apply fog, and our
  `OdysseyGradientSky.shader` does not include `multi_compile_fog` at all. This is *correct* for our
  case rather than a limitation to work around — see §3.
- **Depth is clip-space z, not radial distance.** Fog is a function of distance along the camera's
  forward axis, so a pixel in the screen corner is slightly under-fogged relative to one in the
  centre. At a board camera's modest FOV this is a few per cent and invisible; it is listed because
  it is the first thing a fullscreen pass fixes.

Two further mechanical notes. The factor is computed **per vertex**, so a strongly non-linear curve is
sampled at vertex density and linearly interpolated across the triangle. On 2.5 m cell cubes that is
exact enough to ignore; on the **surround's 20 m and 60 m tiles** it is the one place banding could
show, and it is worth a look in a screenshot rather than an argument. And `unity_FogParams` packs
`(density/sqrt(ln2), density/ln2, -1/(end-start), end/(end-start))`, so linear mode consumes the
start/end pair and the exponential modes consume the density and *ignore* start and end entirely.

**Linear versus exp2 for "a haze that starts gently".** This is decidable with arithmetic rather than
taste, because the brief fixes both ends: the near cells (from the recorded measurement, ground
**50 m to 224 m** away is what is in frame at the default 48-degree pitch) must stay unwashed, and the
surround must be gone into the horizon colour by the time fog was previously opaque (~900–1,100 m).

- **Linear** gives exactly the two-point control that made 460 → 1,100 possible, but its onset is a
  slope discontinuity — a visible ring on the ground at the start distance. Worse, a single ramp that
  saturates at 1,100 m and starts inside the board is nearly flat across the board: start at 50 m,
  end at 1,100 m, and the far rim at 300 m has only **24%** fog while 224 m has 17% — a weak,
  linear-looking wash rather than aerial perspective.
- **Exponential** `f = 1 − exp(−d·z)`: to reach 97% at 900 m needs `d = 0.0039`, which puts **17.6%**
  fog on the nearest cells at 50 m. That is precisely the "washing out the near cells" failure.
- **Exponential squared** `f = 1 − exp(−(d·z)²)`: to reach 97% at 900 m needs `d ≈ 0.00208`. Then

  | distance | 50 m | 100 m | 224 m | 300 m | 600 m | 900 m |
  |---|---|---|---|---|---|---|
  | fog | 1.1% | 4.2% | 19.6% | 32% | 79% | 97% |

  Near cells essentially untouched, a real and *accelerating* lift across the board's visible depth,
  and the surround dissolved. The quadratic near-field is exactly the "starts gently" property, and it
  costs nothing to obtain because it needs no start distance at all.

Exp2 therefore does the whole distance job on its own, and the instinct to keep fog off the board with
a 460 m start was an artefact of having chosen linear.

**Cost: zero.** The keywords are already compiled, the arithmetic is two vertex instructions and a
fragment `lerp`, and the mode is a per-scene setting.

### 2. A fullscreen depth-based fog renderer feature

**Shape.** In URP 17 this is either the stock **Full Screen Pass Renderer Feature** driving a
Fullscreen Shader Graph, or a hand-written `ScriptableRendererFeature` on the Render Graph API. Either
way it declares a depth requirement, samples `_CameraDepthTexture`, reconstructs world position from
the depth and the screen ray, and composites a fog colour computed from distance, height and
optionally a sun lobe. Unity 6000.0–6000.3 support both Render Graph and Compatibility Mode; 6000.4+
is Render Graph only, so anything written now should be written on Render Graph.

**What it buys over §1:** per-pixel evaluation (no vertex interpolation of a non-linear curve), true
radial distance, a height term with base height and falloff, an inscatter tint towards the sun applied
to the *ground* as well as the sky, and a max-fog-distance cap so the surround can be stopped short of
total.

**What it covers and what it misses.** Injected at `BeforeRenderingSkybox` it fogs everything opaque
and leaves the sky alone. Injected at `AfterRenderingSkybox` it also fogs the sky, and then a max-fog
or cutoff distance is *mandatory*, because sky pixels sit at the far plane and will otherwise come out
as flat fog colour, destroying the gradient. **Transparents are missed** at either point: they are
drawn afterwards, so our water would be the one unfogged surface on the board. Catching them means
injecting at `AfterRenderingTransparents` — and then water's own `MixFog` must be removed or the water
is fogged twice (§4). The reference implementation surveyed treats transparent support as
experimental and solves it with a camera stack, which is a materially larger change than it sounds.

**Two Render Graph specifics that cost real time.** A fullscreen pass cannot sample and write the same
colour attachment, so reading the scene colour requires the feature's "Fetch Color Buffer" option,
which copies the active colour target — a full-screen copy on top of the fog pass itself. And the
depth texture must actually be enabled on the URP asset; on a board camera it already is, because the
outline feature and the water shore fade both ask for it, so that part is free for us.

**Cost.** The one published measurement found (`meryuhi/URPFog`, fullscreen shader graph via the Full
Screen Pass feature) is **~0.6 ms at 2560 × 1440 without noise, ~0.8 ms with procedural noise**. GPU
unstated. Scaling by pixel count, 1440p → 1080p is ×0.56, giving **~0.34 ms at 1080p** for the
no-noise case, before the colour copy. Against our current 0.99 ms meadow frame that is a **~35%
increase in frame time** for a feature the camera can barely see (§7 below).

### 3. Making the skybox agree

**The horizon band must simply *be* the fog colour.** `OdysseyGradientSky` already lerps
`_HorizonColour → _SkyColour` by `pow(saturate(dir.y), 1/_HorizonFalloff)`, so setting
`_HorizonColour` equal to `RenderSettings.fogColor` makes ground and sky meet at an identical value
and the join becomes invisible by construction. The only requirement is that **one place in C# writes
both**, or they will drift. Raising `_HorizonFalloff` from 2.2 towards ~3.0 tightens the pale band
against the horizon, which is what a hazy golden hour looks like and also narrows the region where a
mismatch could show.

**Should the sky be fogged at all? No.** At the horizon the sky is already exactly the fog colour, so
fogging it is a no-op there; away from the horizon it would only drag the zenith towards the fog colour
and flatten the gradient that gives the sky its depth. The correct model for a painted sky is: the sky
*is* the fog at infinity, so it does not need the fog applied to it. This is also why URP's skybox pass
not applying fog is convenient rather than a gap.

**A cheap analytic sun lobe.** A warm glow towards the light is four instructions in the sky fragment
and no new pass: take `s = saturate(dot(normalize(direction), -mainLightDirection))`, raise it to a
`_SunGlowExponent`, multiply by `_SunGlowColour * _SunGlowStrength`, and — importantly — weight it by
the *horizon* term so the glow sits in the haze band rather than floating as a disc in the upper sky.
That is an analytic wash, not a scattering model, and it is the right register for a shader whose
stated design is "three colours and two falloffs, and nothing else". It stays consistent with "no sun
disc" as long as the exponent is low (≈8, a broad lobe) rather than high (≈256, a disc).

The sky is drawn once per frame with `ZWrite Off` in the Background queue and touches every unoccluded
pixel exactly once; adding four ALU to it is free at any resolution we care about.

**Caveat worth stating:** at the default 48-degree pitch the horizon is not in frame at all (recorded
in the status notes alongside the hill-amplitude measurement). The sun lobe and most of the sky work
only pays at the shallow pitches — the 20-degree and 14-degree shots that `ReliefCheck` already takes.
Judge it there.

### 4. Consistency: not fogging anything twice

**If a fullscreen pass is ever adopted, built-in fog must be switched off** — `RenderSettings.fog =
false`. This is the clean way to do it rather than editing shaders: with fog disabled the keyword set
resolves to `_`, `ComputeFogFactor` returns 0 and `MixFog` becomes an identity, so every material in
the project — ours, Synty's, URP's Lit — silently stops applying its own fog with no per-shader
change. The failure mode if this is forgotten is not subtle: the board is fogged towards the fog colour
twice, so mid-distance goes visibly milkier than either curve intends, and it will read as "the fog
parameters are wrong" rather than as double application.

**Water specifically.** `Odyssey/Water` is the interesting case because it is transparent and applies
its own `MixFog`.

- Under the recommended built-in route it needs **no change at all**. It already declares
  `multi_compile_fog` and computes the factor per vertex, so switching `fogMode` to
  `ExponentialSquared` reaches it automatically. The fog is applied to the water's own shaded colour
  and the bed behind it was fogged in its own pass, so the alpha blend composites two correctly
  fogged surfaces. That is the standard and correct behaviour for a translucent sheet.
- Under a fullscreen pass injected before transparents, water would be the single unfogged object on
  the board — conspicuous, because it is the brightest and most saturated surface there.
- Under a fullscreen pass injected after transparents, water's `MixFog` line must be deleted (and the
  pragma with it), or it is fogged twice while everything else is fogged once.

One more consistency trap: water does not write depth (deliberately, so the outline pass reads the
bed). A fullscreen depth fog therefore fogs the **bed's** distance at water pixels, not the water
surface's, which for a one-cell-deep channel is a difference of about 2 m and does not matter — but on
a wide river seen at a grazing angle the error grows with the view angle.

### 5. How other engines parameterise height fog — a model for our own parameters

**HDRP's Fog volume override** (the closest sibling, same company, same maths conventions):

| Parameter | Meaning |
|---|---|
| Fog Attenuation Distance | Density at the base, expressed as the distance at which 63% (1 − 1/e) of background light has been absorbed and out-scattered. Density stated as a *distance*, which is far more authorable than a raw coefficient. |
| Base Height | The altitude below which fog is homogeneous; above it, density falls off exponentially. |
| Maximum Height | The height at which the falloff has reduced the base density by 63%. Again, a falloff rate expressed as a height. |
| Max Fog Distance | The distance at which fog is applied to the skybox/background — i.e. the cap that stops the sky becoming flat fog colour. |
| Colour Mode | *Constant Colour* (a picked colour) or *Sky Colour* (sampled from the sky cubemap, so fog matches the sky automatically). |
| Tint | HDR colour multiplied with the sampled sky colour, in Sky Colour mode. |
| Mip Fog Near / Far / Max Mip | Which mip of the sky cubemap distant fog samples, so far fog uses a blurrier sky. |
| Volumetric block | Albedo, anisotropy (−1…1 phase asymmetry), multiple scattering, denoising, slice distribution — all raymarched, all out of scope for us. |

**Unreal's Exponential Height Fog:**

| Parameter | Meaning |
|---|---|
| Fog Density | Global density factor. |
| Fog Height Falloff | How fast density increases as height decreases; smaller values stretch the transition over a greater vertical range. |
| Fog Cutoff Distance | Beyond this, no fog is applied — explicitly recommended for excluding skyboxes that already have fog painted into them. |
| Two fog colours | One for the hemisphere facing the dominant directional light, one for the opposite hemisphere. This is the whole of "sun-direction inscatter" in its cheap form and is exactly the right model for us. |
| Directional inscattering exponent/start distance | A separate, tighter lobe around the sun direction layered on top. |
| Volumetric scattering distribution | Phase asymmetry, 0 isotropic, 0.9 strongly forward. |

**The lessons for our parameter set,** if we ever build the fullscreen version: state density as a
*distance* not a coefficient; state height falloff as a *height* not a rate; keep a max/cutoff distance
as a first-class parameter, because the sky is a special case in every engine surveyed; and take
Unreal's two-hemisphere colour rather than a physical phase function — two colours and a `dot` reads as
golden-hour inscatter at a hundredth of the cost.

Note, though, that both engines' height parameters are aimed at worlds tens or hundreds of metres tall.
Our board is 16 layers × 3 m = **48 m**, and the visible ground band is 50–224 m away. A height falloff
over 48 m against a 900 m distance falloff changes almost nothing the camera can see; it would matter
only for the surround's 50 m hills, which sit at ≥700 m where distance fog has already saturated.
**The height term is the cheapest thing to want and the least visible thing to get, in this scene.**

### 6. Measured cost of a fullscreen fog pass on a mid-range 2022 laptop GPU

Not found. The single measurement located is `meryuhi/URPFog` at ~0.6 ms (2560 × 1440, no noise) and
~0.8 ms with procedural noise, on an unstated GPU. Extrapolating to the stated performance target —
a 2022 mid-range laptop — is not defensible from that figure alone: a fullscreen fog pass is almost
purely bandwidth and fill bound, so it scales with memory bandwidth rather than with the shader
throughput our own 0.99 ms figure was measured against on an RTX 5070 Ti. The honest statement is:
**expect 0.3–0.5 ms at 1080p on a desktop GPU and treat a laptop figure as unmeasured**, and if the
pass is ever built, measure it with `FrameTimeTests` under the real player loop rather than an editor
render loop (the standing lesson in `docs/lessons.md`).

### 7. Why the fullscreen pass is not worth it *here*, specifically

Collecting the arithmetic above: at the default pitch the camera sees ground from 50 m to 224 m and no
horizon; the board is 48 m tall; the surround beyond 700 m is already at 85%+ fog from the distance
term alone. Of the four things a fullscreen pass adds — per-pixel evaluation, radial distance, a
height term and a sun lobe on the ground — the first two are corrections of a few per cent, the third
is invisible at this camera, and the fourth can be had on the *sky* for four instructions. The pass
would cost roughly a third of the current frame to deliver corrections the camera geometry suppresses.

## Recommendation

**Take option 1 plus option 3: built-in exponential-squared fog, with the gradient sky rewritten to
agree with it and given a cheap sun lobe. Do not build the fullscreen pass yet.** It is free, it needs
no change to the water shader, it reaches every Lit material and every Synty prop automatically, and
the exp2 curve is arithmetically the correct shape for "starts inside the board without washing out
the near cells".

Parameters to start from — every one of these is a starting point to be judged in a screenshot, not a
measurement:

**Fog** (set from one place in C#, beside the sky material's colours):

```
RenderSettings.fog              = true
RenderSettings.fogMode          = FogMode.ExponentialSquared
RenderSettings.fogDensity       = 0.0021      // 1% @50 m, 20% @224 m, 32% @300 m, 97% @900 m
RenderSettings.fogColor         = warm amber horizon, e.g. (0.86, 0.68, 0.48)
```

`fogStartDistance` / `fogEndDistance` become dead parameters and the existing 460 → 1,100 pair should
be deleted rather than left to mislead. Density is the single lever: halving it to 0.001 moves the 97%
point out to ~1,870 m and drops the rim to 9%; doubling it to 0.004 puts 6% on the nearest cells and
is where washing-out begins.

**Sky** (`OdysseyGradientSky`):

```
_HorizonColour   = exactly RenderSettings.fogColor        // the join, by construction
_SkyColour       = a dusty blue, e.g. (0.30, 0.44, 0.62)  // cooler and deeper than today's (0.36,0.60,0.86)
_HorizonFalloff  = 3.0   (from 2.2)                       // tighter band against the horizon
_GroundColour, _GroundFalloff  unchanged
```

**New sky properties** for the warm lobe, ~4 ALU in the Background pass:

```
_SunGlowColour    = (1.00, 0.82, 0.55)
_SunGlowStrength  = 0.6
_SunGlowExponent  = 8      // broad lobe, deliberately not a disc
```

applied as `glow = pow(saturate(dot(dir, -mainLightDirection)), _SunGlowExponent)` weighted by the
horizon term, added after the two existing lerps. Feed the light direction from the same C# that sets
the fog colour rather than reading `_MainLightPosition` in a Background-queue pass.

Two things to do in the same change, both cheap and both bugs if skipped:

1. **Audit every custom material for `multi_compile_fog` + `MixFog`** — the chunk mesher's ground and
   skirt shaders, `GroundScatter`, and the `ChipRecipe` particle material. Anything missing it stays
   fully saturated at 900 m while the world around it has gone to amber, which is a far more obvious
   artefact than a slightly wrong density. `Odyssey/Water` is already correct and needs nothing.
2. **Look at the surround tiles for fog banding.** Per-vertex evaluation of a quadratic curve on 20 m
   and 60 m tiles is the one predictable artefact of this route; the recorded diagonal-crack lesson
   from the relief work is the precedent for taking tile-size interpolation errors seriously. Judge it
   at 20 and 14 degrees, where the far ground is in frame.

**Revisit the fullscreen pass only on one of these triggers**, each of which is an observation rather
than a preference: the vertex banding in (2) turns out to be visible and cannot be fixed by tile size;
the camera gains a pitch shallow enough to put the horizon in frame routinely, so ground inscatter
towards the sun starts to matter; or a design need appears for locally dense fog (valley mist, a
cavern mouth) that a uniform term cannot express. If it is built, build it on Render Graph, inject at
`BeforeRenderingSkybox`, set `RenderSettings.fog = false` in the same commit, and parameterise it as
HDRP does — attenuation *distance*, base *height*, maximum *height*, max fog distance, two hemisphere
colours.

## Sources

https://docs.unity3d.com/6000.3/Documentation/Manual/urp/shader-stripping-fog.html
https://docs.unity3d.com/6000.1/Documentation/Manual/urp/renderer-features/renderer-feature-full-screen-pass.html
https://docs.unity3d.com/6000.3/Documentation/Manual/urp/whats-new/urp-whats-new.html
https://github.com/meryuhi/URPFog
https://github.com/WeaverGames/UnityURPSkyFog
https://kronnect.com/company/blog/fog-in-unity-urp-complete-guide/
https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.2/manual/fog-volume-override-reference.html
https://dev.epicgames.com/documentation/en-us/unreal-engine/exponential-height-fog-in-unreal-engine
https://catlikecoding.com/unity/tutorials/rendering/part-14/

## Confidence

**Medium-high.** The fog curve arithmetic in §1 is computed directly from the standard exp/exp2
formulae and is the load-bearing part of the recommendation; the URP application mechanism, keyword
set and HDRP/Unreal parameter tables are documented. Medium rather than high because two supporting
claims rest on knowledge of URP's shader library rather than on a cited page — that the fog factor is
computed per vertex and interpolated, and the exact `unity_FogParams` packing — and because the one
cost figure for a fullscreen pass has no GPU attached to it.

## Could not be determined

- Any fullscreen-fog cost figure measured on a mid-range 2022 laptop GPU, or on any *stated* GPU.
- Whether URP 17 offers a per-fragment fog option (a `_FOG_FRAGMENT`-style variant) that would remove
  the vertex-interpolation concern without a fullscreen pass. Not confirmed either way; check
  `ShaderVariablesFunctions.hlsl` in the installed package before assuming the per-vertex claim.
- Whether the surround's 20 m and 60 m tiles actually band under exp2 at our density — this is a
  screenshot question and no amount of reading settles it.
- The project's colour space and whether the suggested fog/sky swatches should be authored as linear
  or gamma values; the numbers above are given as linear and will look wrong if pasted into a gamma
  field.
- Whether the sun lobe can read `_MainLightPosition` reliably from a Background-queue pass in URP 17,
  which is why the recommendation feeds the direction from C# instead.
