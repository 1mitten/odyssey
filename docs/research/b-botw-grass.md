# Lane B — What makes Breath of the Wild's grass look like that, and what of it reaches a top-down camera

*Researched 2026-09-22, at the owner's request ("can it be more like the grass you see in Zelda
breath of the wild, tears of the kingdom — it looks fine though but nothing special"). Caps: 10
searches, 8 page reads; both respected.*

## Question

What actually produces the look of the grass in *Breath of the Wild* and *Tears of the Kingdom*,
and which of those techniques still read from a camera looking **down at 48° from tens of metres**,
where one blade covers one or two pixels?

## Findings

Sources are tagged **(a)** published by the developer, **(b)** credible reverse-engineering or
datamining, **(c)** community reconstruction or tutorial, **(d)** engineering judgement.

### 0. The headline, and it is a sourcing headline

**Nintendo has published nothing about the grass.** The one graphics talk that exists — CEDEC 2017,
*The Layered World of Breath of the Wild* — is about layering sky, fog, light and depth, and
contains no blade geometry, no wind function and no foliage shading. Nearly everything online
labelled "BotW grass shader" is **(c)**: a Unity or Godot tutorial tuned by eye against
screenshots. Several of its most-copied features are **not in the game**.

### 1. The blade is one triangle, and it is not curved

- **(b)** Close-range observation on hardware: *"each blade of grass is exactly 1 tri … to simulate
  the wind they just push the top vertex into the direction the wind is blowing … vertex colors to
  either darken the two vertices at the bottom, or lighten up the vertex that's the tip"*. One
  observer, self-flagged as unconfirmed, but consistent with the Wii U-era budget.
- **(c)** Every popular reconstruction uses a curved, tapered, 5–15 vertex Bézier blade with a
  rounded tip. **That is prettier than BotW and is not what BotW does.** It is tutorial invention
  for a ground-level camera.
- **(a, different game)** *Procedural Grass in Ghost of Tsushima* (GDC 2021) is the only real talk
  in this space: cubic Bézier blades with tilt and twist, and normals tilted outward to fake a
  round cross-section. Far higher budget.

### 2. Wind: a real global vector, tip translation, and gusts from noise

- **(b)** BotW simulates a **global wind direction** as gameplay — it drives fire spread, the
  paraglider, smoke and clouds, and the grass reads the same vector.
- **(b)** The bend is reported as **translating the tip**, weighted by the vertex-colour ramp, not
  rotating the blade about its root. On a one-triangle blade that stretches it, and the game
  visibly does not care.
- **(a, Ghost of Tsushima)** The best-documented account of *gust fronts sweeping a field* is a
  unified wind field from 2D Perlin noise sampled by both CPU and GPU, with **constant direction
  and time-varying magnitude**. The gust comes from modulating **magnitude** over a low-frequency
  noise field, not from turning the wind.

### 3. Shading: much less than the tutorials assume

- **(b)** Base-to-tip darkening baked into vertex colour — an AO substitute and a gradient in one,
  for zero instructions. This is the only shading feature with evidentiary support.
- **(d)** Backlit foliage in BotW is most credibly the **global material model**, not a bespoke
  grass subsurface term. No grass pixel shader has ever been dumped or analysed.
- **Per-blade specular sheen is not evidenced.** At BotW's blade size it would mostly alias.

### 4. Colour is a painted patch map — the strongest finding, and not what the tutorials say

**(b, high confidence, direct format documentation.)** Every terrain area with grass carries a
`.grass.extm` file: a **64 × 64 grid, four bytes per vertex — blade height, then R, G, B**, whose
values "are capable of producing all colors", referenced from the terrain scene binary.

So BotW's grass colour *and height* are **authored per patch as a low-resolution map painted over
the terrain** and sampled per blade. Not per-blade hash noise. Not a procedural noise in the
shader. Hue **and** value both vary, which is why a single vista shifts from yellow-green through
blue-green to olive, and why the colour tracks the biome rather than looking like static.

### 5. Density and LOD: one scalar, bulk culling, no billboards

- **(b, from Cemu graphic-pack binary patches)** Grass density is **a single float** at one address.
  Trees have a separate billboard-transition knob; **grass has no billboard stage of its own.**
- **(b)** The ultrawide patch must scale a `grassCulling` multiplier by the aspect ratio or grass is
  culled at the screen edges — so culling is **frustum-derived and done in bulk**, not per blade.
- **(b)** Polygons near, masked cards far, "depending on the LOD". Distances unpublished; the
  transition is hidden by the game's heavy aerial perspective rather than by a clever dither.

### 6. Interaction: the famous part is not in the game

**(b, medium-high)** BotW's and TotK's **terrain grass does not bend around Link** — he clips
through it. What reacts is the *tall cuttable grass*, which is a separate placed actor. **The
interactive parting everybody associates with "BotW grass" is tutorial invention.**

The cheap ways to do it anyway, ascending: a few `float4` globals for one hero unit; a small
top-down **bender render texture** splatted per bender and sampled in the vertex shader; the same
with slow decay, which gives worn paths.

---

## What transfers to *our* camera

The governing fact, and it decides everything below: **at one or two pixels a blade, we are not
drawing blades, we are drawing a statistical field.** Anything whose signal lives inside one blade
is gone. Anything whose signal lives across metres survives.

Ranked:

1. **Per-patch colour and height, sampled from a map.** §4's mechanism is *exactly* the right scale
   for us: at 40 m up the visible field is tens of metres across, so a colour map at one to four
   metres a texel is the dominant signal. It is also what `b-painted-look-games.md` ranked first a
   year ago and which was never built.
2. **Spatially coherent gusts.** Uncorrelated per-blade phase reads as **shimmer**; correlated phase
   reads as **wind**. Keep a per-blade phase but as a *small perturbation* on a world-space term,
   and modulate magnitude by low-frequency noise for gust fronts. Big legible motion across metres
   is arguably the only blade-shader feature that reads at all from our camera.
3. **Blade width and density against distance.** A performance matter, not an aesthetic one:
   one-pixel triangles waste four to eight times over on 2 × 2 quad shading, and near-vertical
   blades foreshorten under a 48° pitch. Widen blades with distance while thinning the count, so
   coverage holds — this is where a 5 ms budget is won or lost.
4. **A ground texture beyond the geometry ring**, matched to the field's average colour from the
   same map as (1), so the transition needs no fade.
5. **A benders render texture.** Not a BotW technique, but our camera is *already* top-down so the
   texture axis-aligns with the view for free, and trampled paths are high-information in a colony
   sim — they show traffic and settlement wear.
6. **Keep the root-to-tip ramp but retune it.** From above we see tips and the ground between
   blades, hardly any base, so the tip colour should *be* the field colour and the contrast should
   not be spent on a base darkening nobody sees. Put the occlusion on the **ground** instead.
7. **Atmosphere over blades.** The one real Nintendo talk is about layering. At this distance more
   of "the look" comes from sky-tinted ambient, aerial perspective and tonemapping than from
   anything in the blade shader.

### Wasted effort at this camera, stated bluntly

- **Blade curvature, taper shaping, rounded tips, Bézier tilt and twist.** Sub-pixel, and BotW does
  not do them either. Spend the vertex budget on more blades, not better blades.
- **Per-blade specular sheen**, and **per-blade backlight**: at one or two pixels a moving highlight
  is an aliasing generator, and at 48° looking down we are almost never viewing blades against the
  sun. Do it as a broad field-level term if at all.
- **Length-preserving root rotation instead of tip translation.** The stretch is a sub-pixel length
  error. Rotation only earns its cost when blades are tens of pixels tall.
- **Per-blade hue jitter.** High-frequency chroma noise that crawls and aliases. Put it at patch
  scale, which is also what the shipped game does.

## Sources

- https://zeldamods.org/wiki/Grass.extm — the 64×64 height + RGB patch map (b)
- https://zeldamods.org/wiki/TSCB — terrain scene binary, which references it (b)
- https://deepwiki.com/cemu-project/cemu_graphic_packs/4.1-breath-of-the-wild — density as one float, `grassCulling` and the aspect fix (b)
- https://www.resetera.com/threads/zelda-breath-of-the-wild-the-technical-analysis.8197/page-2 — the one-triangle blade and vertex-colour observation (b, single observer)
- https://www.thefamicast.com/2017/12/cedec-talks-translated-making-of-breath.html — index of the CEDEC 2017 translations (a; verified to contain no grass detail)
- https://gdcvault.com/play/1027033/Advanced-Graphics-Summit-Procedural-Grass — Ghost of Tsushima's procedural grass (a, different game)
- https://www.gamedeveloper.com/design/using-vorticles-to-simulate-wind-in-i-ghost-of-tsushima-i- — vorticles wind simulation (a, different game)
- https://danielilett.com/2021-08-24-tut5-17-stylised-grass/ — the URP tutorial the owner's video belongs to the family of (c)
- https://godotshaders.com/shader/gdquest-botw-grass-shader-gradient-tweaks/ (c)
- https://smythdesign.com/blog/stylized-grass-webgl/ (c)
- https://github.com/zeldaret/botw — decompilation: game logic, no shaders (b, negative result)

## Confidence

**High** that Nintendo published nothing, that the colour and height come from a painted 64×64
patch map, and that the global wind is a simulated gameplay vector. **Medium** on the
one-triangle blade and tip-translation wind, both resting on one careful observer. **Low, and
probably false,** that curved Bézier blades are "the BotW look" — that is tutorial provenance.
**High as a technique, low as attribution** for gust fronts from noise-modulated magnitude: that
is Ghost of Tsushima's published method, not Nintendo's.

The ranking of what transfers to our camera is **(d)**, engineering judgement, resting on the one
hard fact that a blade covers one or two pixels here.

## Could not be determined

- Any BotW grass vertex or pixel shader source. It lives in `.sharcfb` archives; Cemu dumps
  shaders but no published analysis of the grass shader exists.
- Vertex or triangle counts beyond the single "1 tri" observation; no LOD tiers, no instancing
  scheme.
- Grass draw distance in metres, the LOD transition distance, and the fade method at the
  geometry-to-texture boundary.
- The world size of a terrain area, so the metres-per-texel of the patch map is unknown.
- Whether the patch map's RGB is a direct tint, a ramp lookup or a blend weight.
- Whether any per-blade colour jitter sits on top of the patch map.
- Whether TotK changed the grass system at all.
