# Keeping the board readable under a low raking sun (and what URP 17 shadows cost)

**Question.** How do top-down, isometric and three-quarter games with a low raking sun keep the
playable surface readable, and what are the URP 17 shadow settings and costs for covering the
visible range (ground 50–224 m from the camera at a 48-degree pitch, further when tilted to 15–20)?

## Findings

### 1. Readability techniques

**The first number to write down is shadow length, because it decides everything else.** A shadow is
`height / tan(elevation)` long. At 35 degrees that is x1.43, at 30 degrees x1.73, at 25 degrees
x2.14. So on this board: a 3 m layer of wall throws 4.3–6.4 m (two cells or more), a 12 m tree
throws 17–26 m (seven to ten cells), and a 1.8 m colonist throws 2.6–3.9 m — **a colonist's own
shadow covers more than a whole cell**, which is the readability problem in one sentence. At the
owner's 25–35 degree range the board is going to be mostly shadow at any tree density above sparse,
so the shadows must be *light* rather than *few*.

**Shadow strength below 1 is the main lever and the cheapest.** Unity's directional light Strength
multiplies the shadow's occlusion, so 0.6 means a shadowed fragment still receives 40% of the key
light. This is physically a lie and is exactly what stylised builders do: it keeps the hue of the
shadowed grass the same as the lit grass (so terrain still reads as terrain) instead of letting the
shadow fall back to whatever the ambient happens to be, which is where the muddy-blue-slab look
comes from. It costs nothing — it is a multiply in the shading. **This is the single change that
most directly answers "the previous 72-degree sun was chosen because a raking sun darkened the
readable ground".** The old choice fixed the symptom (fewer, shorter shadows) at the cost of the
look; strength fixes the cause (shadows too dark) and keeps the look.

**Warm key, cool fill, and the fill must be directional in colour if not in fact.** The standard
exterior setup (Chaos/V-Ray's exterior-lighting guidance and the classic three-point reading of it)
is a warm low key plus a large cool ambient standing in for sky. In Unity the free version of this is
**Environment Lighting → Gradient**: sky cool and desaturated, equator a mid neutral, ground warm —
the ground colour is doing the job of bounce off the meadow and is what stops shadowed grass going
blue. Gradient ambient is folded into spherical harmonics once per frame; it is free at draw time,
unlike a second light.

**Do not let the shadows point at the viewer.** With the sun roughly behind the camera, shadows fall
*away* and hide behind their casters — readable, but the board goes flat and the relief work (the
0.68 ms of hills) stops showing. With the sun in front of the camera, every shadow lies across the
ground between the player and the thing they are reading, which is the worst case. The usable setup
is a three-quarter back-side sun: azimuth roughly 30–50 degrees off the camera's forward axis, to
one side, so shadows rake *across* the board. Long shadows then act as relief cues rather than as
occluders of the cells in front of buildings.

**If the camera yaws, the sun must not.** The clearest player-facing evidence found is a Timberborn
suggestion thread titled "Shadows should not rotate with camera, change light source to global sun",
i.e. players notice and dislike a camera-relative key. Whatever azimuth is chosen has to be
world-fixed, and the azimuth therefore has to be acceptable from all four cardinal yaws — which in
practice means it cannot be tuned to one screenshot.

**Post-processing: do not crush the toe.** ACES tonemapping (Unity's default choice in many URP
templates) rolls the shadows down hard and is the wrong curve for a board that must stay legible in
shadow; **Neutral** tonemapping, or none, with Color Adjustments contrast pulled slightly *down* and
a small Lift, keeps the shadowed cells separable. Split Toning (warm highlights, cool shadows) buys
the golden-hour read back without darkening anything. Screen-space ambient occlusion is a trap here:
it adds a second, unlit darkening on top of an already long shadow.

**Game examples — weak evidence, stated honestly.** Seven searches turned up no developer statement
from Timberborn, Against the Storm, Cities: Skylines, Anno, Station to Station or Foundation about
sun elevation or shadow strength; the only primary source found is the Timberborn camera-relative
shadow complaint above. What can be said from observation rather than citation is that these games
run a *mid* sun most of the time (roughly 45–60 degrees), reserve the low raking sun for dawn/dusk
minutes of a cycle or for marketing shots, and pair it with strong ambient and a lifted shadow.
Station to Station is the closest to a permanently low sun and is also the one with the least
per-cell information to read — it is a diorama, not a colony sim with stacks and designations on the
ground. **Treat "permanent golden hour" as a deliberate departure that has to be paid for with
shadow strength and ambient, not as something the reference games have already solved.**

### 2. URP 17 main-light shadow settings

- **Max Distance.** The shadow map is spread over this distance, so texel density is inversely
  proportional to it. Unity's own worked example: dropping Max Distance 40 → 10 let a 1024 map
  replace a 2048 one at equal quality. It is clamped to the camera far plane (ours is 1,800 m, so no
  clamp). Cost is not the distance itself but the casters it pulls into the shadow frusta.
- **Cascade Count (0, 2 or 4) and splits.** More cascades = less perspective aliasing, but "increasing
  the number of shadow cascades increases the number of draw calls in the shadow rendering pass".
  URP's stock open-world-ish default is 4 cascades over 150 m. Split values are fractions of Max
  Distance.
- **Resolution.** One atlas for all directional shadows. The atlas is subdivided per cascade — with 4
  cascades a 2048 atlas gives four 1024 tiles. **Worth verifying in the Frame Debugger, but the
  consequence if true is important: 2 cascades at 2048 also yields 1024 tiles, so dropping from 4 to
  2 cascades buys draw calls and buys no texel density at all — it spends the same tile on twice the
  range.** Do not "optimise" 4 → 2 expecting sharper near shadows.
- **Texel density, the rule of thumb.** `texel size = cascade sphere diameter / tile resolution`,
  where the sphere diameter for a 40-degree FOV is roughly 1.3–1.5x the cascade's far distance. The
  optimality criterion is that a shadow texel should project to about one display pixel; closer than
  that you want more, further you are wasting atlas.
- **Soft Shadows.** URP exposes Off / Low / Medium / High (increasing PCF tap counts). "Might have a
  significant performance impact on platforms that use tile-based rendering, such as mobile" — on
  desktop the step from Low to High is small. Soft shadows buy apparent resolution, which is the
  documented way to keep a lower map.
- **Depth Bias / Normal Bias.** Set in the URP Asset as defaults and overridable per light. See §4.
- **Shadow fade / Last Border.** Fades shadows out at the end of the last cascade so they do not pop.
  With a long shadow this fade is visible *along* a shadow (the far end dissolves), so the border
  wants to coincide with something else that hides it.

### 3. Concretely, for this camera

**The finding that matters most: the first cascade is currently being thrown away.** Cascade splits
are fractions of camera *distance*, and the nearest visible ground on this board is **50 m** away. A
stock 4-cascade split of 0.07 / 0.18 / 0.42 / 1.0 over 250 m puts cascade 0 at 0–17.5 m and cascade 1
at 17.5–45 m — **half the atlas covering empty air in front of the camera**. Any split table for this
project must start after the near ground.

Worked, with a 2048 atlas and 4 cascades (1024 tiles), sphere diameter taken as 1.4x far distance:

| Split (of 250 m) | Far | Sphere ø | Texel | Texels per 2.5 m cell |
|---|---|---|---|---|
| 0.30 | 75 m | 105 m | 10.3 cm | 24 |
| 0.48 | 120 m | 168 m | 16.4 cm | 15 |
| 0.70 | 175 m | 245 m | 23.9 cm | 10 |
| 1.00 | 250 m | 350 m | 34.2 cm | 7 |

A colonist is about 0.5 m across, so her shadow is ~5 texels wide in cascade 0 and ~1.5 in cascade 3.
That is the honest picture: **at this camera distance a 2048 map cannot give a crisp person-sized
shadow anywhere on the board, and it does not need to** — a soft blob at the right place reads as a
colonist's shadow, and the shadows that carry the picture are trees and walls, which are 2.5–12 m
across and therefore 10–100 texels even in the far cascade. This is also why Soft Shadows Medium is
not optional here: it hides the far-cascade stair-stepping that a low sun makes long and obvious.

**Tilted down to 15–20 degrees** the visible range runs past 400 m, but the fog is opaque at 1,100 m
and the skirt already desaturates on a ramp from the rim. Do **not** chase the tilted view with
shadow distance; set it at 250 m and let the last-border fade land inside the desaturation ramp,
where a dissolving shadow is indistinguishable from atmospheric loss. Extending to 400 m to satisfy
the rare grazing tilt would cost every frame at the default pitch.

**Cost of 50 m → 250 m.** No published figure was found and this is an estimate to be measured, not
a citation. The shadow pass re-renders casters once per cascade; caster count grows roughly with the
*area* of the covered region, so 50 → 250 m is nominally a 25x increase in candidate casters, cut
back sharply by per-cascade culling (each cascade only draws its own band) and by GPU instancing,
which makes a thousand identical trees a handful of draw calls rather than a thousand. For an
instanced low-poly scene on an RTX 5070 Ti at 640x480 the realistic expectation is **+0.3 to +0.8 ms**
on top of the current 0.99 ms meadow, with the 4th cascade the cheapest of the four to add (few
casters, coarse tile) and the *vertex* cost of the tree meshes being the real driver. **Measure it
with `FrameTimeTests` under the real player loop, never an editor render loop** — the existing lesson
applies unchanged, and a shadow pass is exactly the kind of thing whose cost an editor loop
mis-attributes.

### 4. Long-shadow artefacts

**Acne gets worse as the sun grazes**, because acne is a depth-slope problem: at a low elevation a
single shadow texel spans a large depth range across the receiver, so the surface fails the depth
test against its own map. On a 120x120 board of flat-ish instanced ground this appears as **banding
stripes marching across the meadow**, and it will appear the moment the sun drops, on geometry that
was clean at 72 degrees.

**Normal Bias is the right lever and Depth Bias is the wrong one.** Normal bias offsets the sample
along the surface normal scaled by texel size, which is precisely the grazing-angle correction;
depth bias pushes the whole shadow back along the light, which at a low sun produces **very visible
peter-panning** — a detached shadow slides a long way when the light is shallow, so a tree's shadow
visibly starts a metre from its trunk. The community guidance found is consistent with this: keep
depth bias small and take the correction in normal bias. Practical recipe: set normal bias to 0,
raise depth bias until the acne just disappears, note the value, then halve it and raise normal bias
until the acne is gone again.

**Cascade seams** show as a sudden change in blur radius and bias across a ring on the ground. Three
things reduce them: a shadow strength below 1 (a lighter shadow has a smaller step to make), soft
shadows (the two sides blur towards each other), and URP's per-cascade blend control in the cascade
widget. With a low sun the seam is *more* visible than usual because a single long shadow can span
two cascades and change character halfway along its own length — another argument for pushing the
splits out so the near cascades are not wasted and the bands are wide.

**Ground relief interacts.** The sheared ground tilts real normals, so normal bias is doing useful
work there; but the shear is a drawing offset, and the shadow caster and the shadow receiver see the
same drawn geometry, so no special case is needed. Grass tufts and chips are decoration — they
should be excluded from shadow casting (`ShadowCastingMode.Off`) or they multiply the shadow pass's
caster count for no picture.

### 5. Should there be a second, dimmer fill directional light?

**No — start without one.** In Forward+ every additional light is per-pixel by definition (Forward+
ignores the Main Light per-vertex option), so a second directional light is an extra per-pixel
evaluation on every lit fragment on screen: not free, roughly 0.1–0.3 ms at 1080p on the mid-range
2022 laptop that is the actual target, and it doubles the lighting cost of the water shader and
every instanced chunk. Directional lights are the cheapest real-time light and a shadowless one is
cheaper still, so it is affordable — but it buys nothing that **gradient ambient plus shadow
strength below 1 does not buy for free**, because a shadowless fill light and an ambient gradient
differ only in whether the fill has a direction. On a three-quarter board with a fixed camera,
directional fill mostly shows on vertical faces, and those already read from the key.

The case for adding one later is specific: if vertical surfaces facing away from the sun go flat and
shapeless, a very dim cool directional from roughly opposite the key, shadows off, intensity ~0.15–
0.25 of the key, is the fix. Add it only against a screenshot that shows the problem.

## Recommendation

Start from these and tune against `WaterCheck` / `ReliefCheck` style contact sheets at 48, 20 and 14
degrees, then measure with `FrameTimeTests`.

- **Sun elevation 30 degrees** (settle within 28–32). It gives x1.73 shadows — long enough to read as
  golden hour, short enough that a 12 m tree's shadow is seven cells rather than ten. Go to 25 only
  after the readability settings below are in and judged.
- **Sun azimuth world-fixed, 40 degrees off the default camera forward, on one side**, so shadows
  rake across the board rather than towards the viewer, and check it at all four yaws — never
  camera-relative.
- **Shadow Strength 0.6.** This is the change that makes a low sun survivable and it costs nothing.
- **Ambient: Gradient**, sky cool desaturated blue, equator neutral mid, **ground warm** (bounce off
  the meadow). Target: shadowed grass at roughly half the luminance of lit grass, same hue family.
- **Post: Neutral tonemapping, not ACES**; contrast slightly down, a small Lift, optional Split
  Toning warm/cool. No SSAO.
- **Shadow Distance 250 m**, so the far cascade dissolves inside the skirt's desaturation ramp.
- **4 cascades, splits 0.30 / 0.48 / 0.70 / 1.00** (75 / 120 / 175 / 250 m) — the key departure from
  the defaults, because nothing is visible in the first 50 m and the stock splits spend half the
  atlas there. Cascade blend on, small.
- **Resolution 2048, Soft Shadows Medium.** Do not go to 4096; do not drop to 2 cascades expecting
  sharper near shadows (same tile size, twice the range).
- **Depth Bias 0.5, Normal Bias 1.4** as a starting pair, then tune by the halve-depth/raise-normal
  recipe in §4. Peter-panning is the artefact to fear at a low sun, not acne.
- **No second directional light.** Revisit only if a screenshot shows flat away-facing walls.
- **Grass tufts, chips and any other pure decoration: shadow casting off.**

If the measured cost of 250 m is unacceptable, the cheapest retreat is **160 m with splits
0.35 / 0.55 / 0.75 / 1.00**, not a cascade reduction and not a resolution drop.

## Sources

https://docs.unity3d.com/6000.3/Documentation/Manual/shadows-optimization.html
https://docs.unity3d.com/6000.3/Documentation/Manual/urp/shadow-resolution-urp.html
https://docs.unity3d.com/6000.0/Documentation/Manual/urp/shadows-troubleshooting-urp.html
https://docs.unity3d.com/6000.0/Documentation/Manual/shadow-cascades-performance.html
https://docs.unity3d.com/Manual/shadow-cascades.html
https://docs.unity3d.com/6000.2/Documentation/Manual/shadow-cascades-use.html
https://docs.unity3d.com/6000.3/Documentation/Manual/urp/lighting/light-limits-in-urp.html
https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/rendering/forward-plus-rendering-path.html
https://docs.unity3d.com/6000.3/Documentation/Manual/urp/configure-for-better-performance.html
https://catlikecoding.com/unity/tutorials/custom-srp/directional-shadows/
https://unity-trouble-atlas.7colorsgame.com/en/article/unity-urp-shadow-acne-bias-adjustment/
https://discussions.unity.com/t/long-distance-shadows-in-the-urp/938996
https://timberborn.featureupvote.com/suggestions/203478/shadows-just-dont-make-sense-shadows-rotate-with-camera
https://blog.chaos.com/exterior-architectural-rendering-lighting-setups
https://www.blog.radiator.debacle.us/2015/07/lighting-theory-for-3d-games-part-4.html
https://ludeon.com/blog/2013/08/sun-shadows/
https://developer.android.com/games/optimize/lighting-for-mobile-games-with-unity

## Confidence

**Medium.** The URP mechanics (cascades cost draw calls, max distance trades texel density, normal
bias is the grazing-angle lever, Forward+ makes every extra light per-pixel) are from Unity's own
6000.x documentation and are high confidence; the split-ratio table, the texel arithmetic and the
0.3–0.8 ms estimate are my own derivation from that documentation and are untested on this project;
the game-readability claims are largely craft knowledge with only one primary source found.

## Could not be determined

- Any developer statement from Timberborn, Against the Storm, Cities: Skylines, Anno, Station to
  Station or Foundation on sun elevation, shadow strength or ambient scheme. Seven searches found
  one player-facing Timberborn thread and nothing technical. The §1 game claims are observation, not
  citation.
- Whether URP 17 really allocates `resolution/2` tiles for a 2-cascade atlas (the claim that 4
  cascades are strictly better than 2 at the same resolution rests on this). **Verify in the Frame
  Debugger before acting on it.**
- Any measured figure for the cost of raising URP shadow distance 50 m → 250 m in an instanced
  low-poly scene. The estimate is reasoning, not measurement.
- URP 17's exact numeric meaning and units for Depth Bias and Normal Bias (documented only
  qualitatively), so the 0.5 / 1.4 pair is a starting point to tune from, not a derived value.
- Whether URP 17 still exposes a per-cascade blend slider and "Last Border" under the same names.
