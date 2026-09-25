# d-21 — Ambient birds: technique and prior art

## Question

What is the best way to put ambient birds flying around the environment in this game — procedural
flocks (boids), GPU-instanced vertex-animated low-poly birds, billboards/sprites, particle systems —
and what do comparable games do? Covers: the Unity/URP techniques and their cost; prior art in
colony and city sims; open-source implementations and their licences (reference only); readability
from a high camera and the pitfalls; and which seams in this repository a bird director would reuse.

Budget spent: 12 web searches, 15 fetches (six of them refused by the egress proxy: fandom, Paradox
forum, ludeon.com, rimworldwiki.com, pcgamesn.com, halisavakis.com), 15 repository reads/greps.

## Findings

**Techniques**

1. **Motion: there are three tiers, and the count decides which.** (a) CPU boids in managed code,
   one object per bird, all-pairs: Shinao's measured table on a GTX 980 Ti is 20 fps at 1k boids and
   3 fps at 4k — the all-pairs neighbour search is the whole cost. (b) Burst/Jobs (Unity's ECS boids
   sample, schools of fish avoiding sharks): community figures put 10k boids at 60+ fps and ~150k
   before slowdown on a 9700K/2070 Super. (c) Compute-shader boids: Shinao's GPU flock is >1,000 fps
   at 4k and 93 fps at 32k, and a brute-force loop beat the "clever" bitonic-sort and multilateration
   variants at those sizes. **None of that matters below a few hundred birds**: ambient flocks are
   5–30 birds, and a boid only needs to see its own flock, so the neighbour work is n² *within a
   flock* — 30 birds is 900 pair tests, microseconds in plain C#. The published CPU numbers are all
   one flock of everything.
2. **Real birds read as a leader on a path plus loose boids, not pure boids.** Pure boids wander and
   clump without a goal; an ambient flock needs somewhere to go (a circuit, a tree line, a roost). The
   usual shape is a steered target point per flock (a smooth noise or spline path between perches)
   with separation/cohesion/alignment as offsets around it. This is also what makes landing and
   scattering controllable, which the particle and pure-compute routes are poor at.
3. **Wing flap: a vertex shader is enough for a bird.** The standard technique is a rotation or
   vertical displacement of the wing vertices by a sine of (time × rate + per-instance phase),
   weighted by a mask stored in vertex colour or UV (0 at the body, 1 at the wing tip), optionally
   with a second, lagged sine at the tip so the wing bends. It is GPU-instancing-friendly and costs
   nothing on the CPU. Commercial "wing flap" shaders and the RealtimeVFX bird-particle thread use
   exactly this (mask → sine → displacement).
4. **Vertex animation textures (VAT)** bake a skinned clip's vertex positions per frame into a
   texture; the instanced shader samples it. The CPU cost is tiny (one widely cited system transfers
   76 bytes per character: 12 of animation state plus the 64-byte matrix), but clips cannot blend
   and it needs a bake pipeline and a skinned source. It earns its place only for clips a sine
   cannot fake — landing, hopping, pecking. **There is no bird in any Synty pack this project holds**
   (`docs/research/synty-inventory.csv` has a birdhouse and scarecrows only), so there is nothing to
   bake from without a new asset.
5. **Skinned meshes** are the wrong tool: `SkinnedMeshRenderer` does not instance, so every bird is
   its own draw — the problem VAT exists to solve, and the reason this project caps live figures at
   64 (`PawnFigureDirector.FigureCeiling`).
6. **Particle systems** (Shuriken mesh particles with the flap shader) are quick to stand up, but
   per-particle behaviour — perching, fleeing a colonist, landing on the roof that is actually there —
   is awkward, and they run on their own clock unless driven by hand (this project's rule is that
   ambient motion runs on game time, so a pause holds it: `WindDirector`, `RainDirector.Clock`).
7. **Billboards/sprites** suit a fixed orthographic top-down view. With a free-yaw, 20–80° pitch
   perspective camera and low-poly 3D art, a card turning to face the camera reads as flat and off
   style. At the very far zoom a 3–6 px bird is indistinguishable either way, so a card gains nothing.
8. **Drawing the flock**: one instanced mesh of 20–60 triangles, all birds in one call —
   `Graphics.RenderMeshInstanced` with a matrix array (CPU-driven, ≤1,023 per call in the classic
   API) or `Graphics.RenderMeshIndirect` reading a structured buffer (the path §22 of design 38
   already uses for the scenery). Per bird per frame: one matrix write (64 bytes) and a flap phase.
   Casting a real shadow doubles the geometry work and, per design 38 §22a, an indirect shadow caster
   **must not** be culled to the camera frustum.

**Prior art**

9. **RimWorld**: birds were ordinary ground-walking animals for years; the 2025 1.6 update and the
   Odyssey expansion made birds actually fly (40+ flying species, flamingos and herons wading) — but
   as **simulated animal pawns**, not ambient decoration. Before that, flying birds came only from
   mods (Birds of the World, BIRD UP!, More Birds). There is no vanilla decorative flock layer.
10. **Townscaper**: birds are **decorative** — low-poly white seagulls with black outlines that land
    on roofs and scaffolding (never balconies), shuffle at random, and **fly off when the structure
    under them is altered or destroyed**. A community mod exposes the bird count and lets the player
    make them fly one by one or all together, i.e. the perch/flee state is a clean, small model. This
    is the closest match to what this game wants.
11. **Cities: Skylines**: park seagulls are **simulated as citizen instances** and count against the
    65,535 citizen-instance cap; players complained loudly (a forum thread titled "please kill all
    those seagulls"), and at least two widely used mods (No Seagulls, [ARIS] Remove Seagulls) plus a
    sound-tuner mod exist to remove or silence them. Two lessons: never let decoration consume a
    simulation budget, and bird sound that does not scale down becomes the complaint.
12. **Flock Around** (a birding game) gives a behaviour vocabulary worth borrowing: most species perch
    on branches, waterbirds sit on the water or the shore, pigeons hop on the ground, and birds flee
    when approached or when there is noise.
13. Manor Lords, Timberborn, Going Medieval, Against the Storm, Frostpunk, Banished and Anno: no
    source inside the cap described their ambient birds (see Could not be determined).

**Open-source implementations (reference only; no code taken)**

14. `Shinao/Unity-GPU-Boids` — CPU, hybrid and compute flocks, skinned-to-GPU vertex frame
    interpolation, with the measured table above. **MIT.**
15. `unity3d-jp/BoidComputeShader` — compute shader plus VFX Graph. **MIT.**
16. `Unity-Technologies/EntityComponentSystemSamples` (Boids) — ECS + Jobs + Burst.
    **Unity Companion License** (use restricted to Unity-dependent projects).
17. `keijiro/Boids` — an old CPU flocking demo with a Blender folder for its bird; licence not
    readable within the cap. Others seen: `adrwes/GPU-boids`, `chenjd/Unity-Boids-Behavior-on-GPGPU`,
    `dootiedoot/Boids-using-Unity-Job-System`; VAT tools `codewriter-packages/Mesh-Animation`,
    `joeante/Unity.GPUAnimation` (licences not checked).

**Readability from this camera**

18. The play camera's vertical field of view is **40°** (`Assets/Scenes/Play.unity`; the rig itself sets
    none, and `ChunkRenderer.ViewerFieldOfView` defaults to the same 40). At 1080p that is about
    **1,480 / d pixels per metre** at distance *d*. At the 160 m maximum a real songbird (0.25–0.3 m
    span) is about 3 px and a crow (1 m) about 9 px (18 at 4K); at 60 m a crow is ~25 px.
    *(Corrected by the coordinating session: this line first assumed Unity's default 60°.)*
    So: **scale birds to about 1.5–2× life and favour crow/rook/gull-sized silhouettes**, and thin
    or drop them at the far zoom rather than drawing specks. Birds at 10–30 m altitude sit well below
    the camera at every zoom but the closest (at 10 m and 48° the camera is ~7.4 m up), so the flight
    ceiling must clamp to the camera or birds will fly through the lens.
19. **Contrast carries it**: dark birds against the green meadow, light ones against dark ground or
    water; Townscaper outlines its gulls for the same reason. A slow flap of ~3–5 Hz with glides reads
    at any zoom; a songbird's real 10–20 Hz is a blur and aliases at 60 fps.
20. **A ground shadow is the cue that sells height from above** — a flock's shadow crossing the
    meadow tells the eye the birds are up in the air rather than sliding on the ground. It is also a
    second geometry pass; a projected blob is the cheap alternative.
21. **Pitfalls.** Motion pulls the eye, so a flock crossing the screen competes with alerts and
    colonists — keep flocks few and sparse, and keep them out of the way of whatever is selected.
    Clicks must never land on a bird (here that is free: see How it would fit). One draw per bird, or
    birds as GameObjects, is the P10 fault in a new costume. And the Cities: Skylines lesson (11).

## Recommendation

Ranked:

1. **Back this one: a presentation-only `BirdDirector` — a few small flocks moved on the CPU by
   leader-plus-boids steering, drawn as one GPU-instanced procedural mesh with the wing flap in the
   vertex shader.** Two to five flocks of 5–25 birds (≤ ~120 birds), each a steered target on a
   smooth path between perches, members held by separation/cohesion/alignment within their own
   flock only. States: *flying*, *landing*, *perched* (on a tree top or a roof), *scattering*.
   Perched birds take off when a colonist comes within a few metres, when their tree is felled or
   their roof changes (Townscaper's rule), and at dusk; they fly to roost at night and thin under
   rain in step with the audio. The mesh is **generated in C#** (a 20–40 triangle bird with the wing
   mask in vertex colour), so there is no licensed or new art dependency and the runner can test it;
   an authored Blender bird under `Assets/Art/Custom/` can replace it later. One draw call, plain
   C# (no Burst) — at this count the CPU work is microseconds. No shadow in the first cut; a blob or
   a real caster is the first thing to try if the owner says the birds float.
2. **Compute-shader boids** (Shinao / unity3d-jp shape). Right answer only if the owner wants
   murmurations of hundreds to thousands. Costs a compute pass, and perching/scatter logic then needs
   either GPU-side knowledge of pawns and perches or a readback.
3. **VAT on top of (1)** for perched idles (hop, peck, preen) — a later refinement, needs a bird
   asset and a bake step.
4. **Particle system with the flap shader** — fastest to prototype, weakest at perching and scatter,
   and has to be pinned to game time by hand.
5. **Billboards** — off-style at this camera; no gain at distance.
6. **Skinned birds** — one draw each; rejected by the same reasoning as the figure ceiling.

**The tie between 1 and 2** is broken by one observation: *how many birds the owner wants on screen
at once.* The cheapest experiment is to build (1) with the bird count as a setting and time it in a
`FrameTimeTests`-style arm at 64 / 256 / 1,024 birds under its own frame section; if the CPU side
passes ~0.1 ms at the count the owner picks by eye, move the steering to Burst or a compute pass —
the instanced drawing and the shader do not change.

## How it would fit

- **It is decoration by the standing rule**: nothing in a cell, a save or the hash, and deliberately
  *not* an animal pawn (AN/WL animals are simulated `Pawn`s with a `Kind`; ambient birds must never
  enter `PawnRegistry` — the Cities: Skylines seagull lesson). Seed the flocks from the world seed
  and the tick so contact sheets are reproducible, as `WindDirector` does.
- **Clock**: game time from the tick, the `WindDirector` / `RainDirector.Clock` rule — a pause holds
  the birds mid-wingbeat, speed 3 flies them three times as fast.
- **Time of day**: `Daylight.HourOf(tick)` / `DaylightDirector.Hour` for dawn activity, dusk roosting
  and none at night — matching the audio, where `SoundIds.AmbienceOutdoorDay` carries the birds and
  the night bed is "a different world", not a quieter one.
- **Weather**: `WeatherLook` (`Rain`, `Cloud`, `Wind`, from `WeatherView`) for density and drift.
  Take the rain hush from `RainMix` (its `OutdoorGain`, which already "steps the birds back") rather
  than writing a second curve, or the eye and ear will disagree — the one-rule-two-owners pattern in
  `docs/bug-patterns.md`. `_OdysseyWind` can bias headings.
- **Perches**: `SkyHeightMap.StopAt(x, z)` / `KindAt(x, z)` already give, per column, the height of
  and kind of thing rain lands on — roof, canopy or ground — which is exactly a landing height;
  `NaturalContent.IsTree(model.EdificeDef(index))` names tree tops. A perch's chunk moving
  (`ChunkVersion`, the per-chunk invalidation from BA) is the "structure under it changed" trigger.
- **Scatter on approach**: `PawnCrowdIndex` already buckets every pawn on a 3 m grid once a frame and
  is shared by three passes; a bird asking "is anyone within 4 m of my perch" is one more reader.
- **Drawing**: the `IndirectScenery` / `Odyssey/Foliage` pattern (instances from a buffer, one
  indirect draw) or plain `RenderMeshInstanced`; shaders are found at runtime, so the new one must be
  added to `ShaderInclusion` and its `INSTANCING_ON` variant kept alive (`InstancingKeepAlive`), or
  the player build draws nothing (CLAUDE.md, Player build).
- **Picking is safe by construction**: `SlicePicker` walks the grid and the render model, not physics
  colliders, so an unregistered bird can never be clicked. Keep it out of the selection highlight's
  mask for the same reason.
- **Visibility**: draw only when the slice is at or above the surface, the outdoor-ambience rule;
  scale density with `SliceCameraRig` distance; take the count from the quality presets (M6) the way
  `RainDirector.MaxStreaks` does.
- **Measurement**: a `FrameSection` of its own (the P10 rule — no uncounted pass) and a timing arm.
- **Where it would live**: `Assets/Odyssey/Presentation/Rendering/` beside `WindDirector` and
  `RainDirector`, built and synced by the bootstrap (or folded into `WeatherLook`, which exists so the
  bootstrap gains four lines, not forty).

## Sources

- https://github.com/Shinao/Unity-GPU-Boids
- https://github.com/unity3d-jp/BoidComputeShader
- https://github.com/Unity-Technologies/EntityComponentSystemSamples/blob/master/EntitiesSamples/Assets/Boids/README.md
- https://github.com/Unity-Technologies/EntityComponentSystemSamples/blob/master/LICENSE.md
- https://github.com/keijiro/Boids
- https://github.com/adrwes/GPU-boids
- https://github.com/dootiedoot/Boids-using-Unity-Job-System
- https://realerichu.medium.com/improve-performance-with-c-job-system-and-burst-compiler-in-unity-eecd2a69dbc8
- https://www.sebaslab.com/porting-a-boid-simulation-from-unityecs-to-svelto-ecs/
- https://realtimevfx.com/t/bird-particle-with-vertex-animation/5610
- https://www.cyanilux.com/tutorials/vertex-displacement/
- https://halisavakis.com/my-take-on-shaders-butterflies-and-fish-shader/ (search snippet only; fetch refused)
- https://tuqiri.gumroad.com/l/wing-flap-shader
- https://github.com/codewriter-packages/Mesh-Animation
- https://github.com/joeante/Unity.GPUAnimation
- https://freder.github.io/UnityGraphicsProgrammingBook1/html-translated/vol3/Chapter%201%20_%20Baking%20Skinned%20Animation%20to%20Texture.html
- https://discussions.unity.com/t/gpu-instancing-for-skinnedmeshrenderer-realtime-not-baked-legacy-animaton/915301
- https://townscaper.fandom.com/wiki/Birds (search snippet only; fetch refused)
- https://github.com/mokojm/Townscaper-CustomBirds
- https://forum.paradoxplaza.com/forum/threads/developers-please-kill-all-those-seagulls.858890/ (search snippet only)
- https://steamcommunity.com/sharedfiles/filedetails/?id=421041154
- https://steamcommunity.com/workshop/filedetails/?id=564141599
- https://ludeon.com/blog/2025/06/announcing-odyssey-and-update-1-6/ (search snippet only)
- https://rimworldwiki.com/wiki/Odyssey_animals (search snippet only)
- https://steamcommunity.com/sharedfiles/filedetails/?id=3245417989
- https://flockaround.wiki.gg/wiki/Birds (search snippet only)

## Confidence

- **Technique ranking and costs (1–8)**: high for the shape (instanced + vertex-shader flap is the
  standard answer; skinned meshes do not instance; VAT trades blending for cost); medium for the
  specific frame-rate figures, which are single-machine community numbers.
- **Within-flock n² being negligible at ≤120 birds**: high (arithmetic), but unmeasured here — the
  experiment in the Recommendation settles it.
- **RimWorld (9)**: medium — from search summaries of the Ludeon announcement and press, the pages
  themselves were refused.
- **Townscaper (10)**: medium-high — wiki summary and a mod README agree.
- **Cities: Skylines (11)**: high — the mod pages state the citizen-instance mechanism directly.
- **Licences (14–16)**: high for MIT ×2 and the Unity Companion License; keijiro/Boids unknown.
- **Readability numbers (18–19)**: high for the pixel arithmetic (40° read from the scene) / medium
  for wingbeat rates, which are general knowledge, not sourced here.
- **Repository seams**: high — each named type and member was read or grepped in this session.

## Could not be determined

- How Manor Lords, Timberborn, Going Medieval, Against the Storm, Frostpunk, Banished and Anno draw
  ambient birds (decorative or simulated, whether they scatter, perch, or follow weather and night):
  no source inside the cap described them, and several likely sources were refused by the proxy.
- The exact mechanics of RimWorld 1.6 flight (when a bird takes off, whether it lands on
  structures).
- The licence of `keijiro/Boids` and of the VAT libraries listed.
- The real cost of a bird ground shadow on this project's pipeline, and whether the camera's
  effective field of view differs from Unity's default at runtime — both are one measurement each.
- Whether the owner wants murmuration-scale flocks, which is the observation that decides between
  options 1 and 2.

## Sketch (2026-09-25)

The recommendation drawn: `docs/reference/mockups/birds/meadow-birds.html` (hosted:
https://claude.ai/artifact/7Y3abzdRug1KTkjU1xFiTZ), with twelve stills beside it, shot in headless
Chromium through the game's own camera (48° pitch, 40° field of view). It shows four species (rook,
buzzard, wood pigeon, swallow) at 28–32 triangles each, flat-shaded, wings flapped in the vertex
shader by a per-vertex weight, one instanced call per species plus a ground-shadow call. The scenery
is placeholder three.js. Every behaviour number in it is invented and is there to be judged, not
kept. What the stills settle is readability: at ×1.75 a rook flock still shows as dark marks at
160 m (`06-game-160m.png`), and at life size it has almost disappeared (`07-…-life-size.png`). The
ground-feeding pigeon reads as a dart at 16 m (`03-game-20m-pigeons.png`). That is the case for a
modelled pigeon if ground birds are wanted.
