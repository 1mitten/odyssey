# d-20 — Rendering rain for a high strategy camera

**Question.** What is the most performant *and* readable way to draw rain for Odyssey's camera: a
pitch of about 48° down, 10–160 m from its focus, FOV 40, in URP Forward+, at 3840 × 2160 on an
RTX 5070 Ti and 1920 × 1080 on an RTX 3050 laptop? The question covers falling streaks
(GPU-procedural, CPU `ParticleSystem`, VFX Graph or screen-space layers), keeping covered ground
(roofs, canopies) dry, wet surfaces and ripples, and what makes rain read from a high camera. The
budget is **CPU ≤ 0.05 ms and GPU ≤ 0.5 ms at 4K**. Search cap: 8 searches and 8 reads (9 searches
were used, one over the cap; two of the 8 reads returned HTTP 403).

## Findings

**A. How falling rain is drawn**

1. **Particles scale with intensity; texture layers do not.** Lagarde's survey sets it out:
   particle rain moves and takes wind realistically, but the cost grows with how hard it rains.
   Full-screen or camera-attached texture layers cost the same at any intensity but have no real
   depth. ATI's ToyShop drew rain as a post-process: several scrolling layers at different speeds
   in one full-screen pass, which gives parallax. *Flight Simulator 2004* used a double cone
   around the camera carrying four animated layers.
2. ***Remember Me* (shipped, PS3/360, 1280 × 720)** used four concentric half-cylinder/half-cone
   layers tied to the camera. All four share one texture of raindrops with the motion blur already
   painted in, and each layer scrolls and scales at its own speed. The raindrops cost **0.40 +
   1.29 ms on PS3** in two passes. All of the rain together (occlusion depth, drops, ripples,
   splashes, lens droplets) came to about **2.8 ms**, and the author calls dynamic rain "a costly
   feature". The layers assume a roughly horizontal view: they are a cylinder *around* a
   first-person camera.
3. ***Far Cry 6* (shipped)** draws rain streaks as **GPU particles recycled around the player**:
   transparent streak textures with no refraction, jittered by a 3D noise texture and blown by the
   weather's wind. It lights them from **a few spherical-harmonic probes** instead of per pixel,
   and uses a *rain shadow map* to include or exclude drops indoors. It also spawns extra splash
   particles near the player. The talk summary gives no counts, resolutions or timings.
4. **A small Shuriken set-up in URP** (Cyanilux): stretched billboards (speed scale 0.1), **2,500
   particles a second** from an 8 × 1 × 8 box above and in front of the camera, simulated in world
   space. The same author recommends **VFX Graph over Shuriken** because it simulates on the GPU.
   This set-up is sized for a close camera. Covering what Odyssey's camera sees from 160 m takes
   one to two orders of magnitude more particles.
5. **Stateless procedural drawing in Unity:** `Graphics.RenderPrimitives` / `DrawProcedural`
   issue one draw of N instances. The vertex shader builds each quad from `SV_VertexID` and
   `SV_InstanceID`, with no mesh and no per-vertex inputs, and the docs advise against declaring
   any because it adds overhead. The draw **skips Unity's frustum culling**, so the caller supplies
   the bounds. A wrapped volume that is always on screen loses nothing by this.
6. **Top-down RTS precedent (Byzen: Embers of Revolution devlog).** Only the search snippets were
   readable; the page returned 403. The camera sits 18 m up, tilted 52°. The developer writes that
   the camera looks "roughly along the fall direction, so a drop presents as a dot rather than a
   streak". Their first pass made 70 % of evenly spaced columns active, with each streak centred
   in its column, and it "read as a comb". Three changes fixed it: **fewer active columns, a random
   horizontal offset per column, and shorter tails**. On impact a drop spawns a **splash crown, a
   ripple in standing water, or occasionally a puddle seed**. The game also has per-material
   wetness, a **shelter map so eaves stay dry**, and **a colour grade that drains the world**.
7. **The genre (Frostpunk, Timberborn, Farthest Frontier, Manor Lords, Cities: Skylines, Anno):**
   none of them documents its rain technique. What turned up: *Cities: Skylines* added rain as
   cosmetic weather in the Snowfall DLC, and players call it poor-looking. *Timberborn* has no rain,
   and players have asked for it as an aesthetic storm.

**B. Keeping covered ground dry**

8. ***Remember Me*'s occlusion** was a **256 × 256 depth map rendered orthographically from above
   over 20 × 20 m, which is 7.8 cm a texel**. It cost **0.32 ms on PS3 and 0.20 on 360** to render,
   plus 0.036 ms to copy. Only the two nearest layers test against it, at low resolution, and the
   far layers use the scene depth buffer. The same map is read back on the CPU to place splashes
   on geometry instead of ray-casting. The known failure: a soft depth test misses **transparent**
   occluders, so rain shows in front of glass.
9. Cyanilux reaches the same design for many occluders ("an orthographic camera pointing
   downwards into a Render Texture"). Far Cry 6's "rain shadow map" and Byzen's "shelter map" are
   the same idea, so it is standard practice.
10. **Derived for Odyssey, not from a source:** the simulation already knows the highest solid
    cell in every column. A CPU-built **height texture of one R16 per column** gives exact cover
    for anything built on the grid, and costs nothing on the GPU. That is 120 × 120 = 28 KB on
    Standard and 240 × 240 on Huge, rebuilt only for the columns an edit touches. Two things a
    grid height misses: canopies wider than a cell (stamp a disc per tree), and drawn-only façades
    (banks, eaves overhang). The second hardly matters for rain.

**C. Wet surfaces, puddles and ripples**

11. **How much wet surfaces darken (Lagarde 3a/3b):** only **porous** dielectrics darken. Metal
    and smooth plastic keep their albedo and gain only a thin film of water. Mid-range albedos
    darken most, with a Lekner–Dorf floor of about **×0.68**, and the measured database spans
    ×0.2–1.0 (worn asphalt: 0.12 → 0.08). The practical rules offered are: a diffuse factor that
    lerps from 1 to **0.2** with porosity, scaled by (1 − metalness); porosity inferred from gloss
    when there is no porosity map; and **gloss pushed towards 1 by half the wetness level**. The
    *Remember Me* demo shader used **diffuse ×0.3 and gloss up to ×2.5**. Cyanilux's wet materials
    use **smoothness 0.8–0.9**.
12. **Puddles:** normals blend to flat, smoothness goes to 1 and specular to water's **F0 0.02**.
    Lagarde grades a surface through four states (dry, wet, drenched with a slight smoothing and a
    specular boost, puddle), driven by painted accumulation or a height map (low parts fill
    first). **Drying:** specular vanishes much faster than the darkening, and for games he suggests
    a simple lerp back to dry over **about a minute**. Rain streaks running down walls are called
    unrealistic, so wetting is masked by the normal.
13. **Ripples:** a tiling texture of non-overlapping circles, one channel per piece of data (edge
    falloff, direction, a random time offset). It is sampled in **four layers at different offsets
    and rates, switched on at intensities 0, 0.25, 0.5 and 0.75**, with each ring a damped wave.
    Generating the 256² ripple normal map took **0.14 ms on PS3**. Cyanilux instead bakes Voronoi
    rings into a **16-frame normal flipbook**, which is cheaper than running them procedurally, at
    a normal strength of about 0.2. **At a distance the ripples mip away to nothing**, and Lagarde
    found no good fix.
14. **A deferred G-buffer trick does not port.** Procedural Pixels wets the scene in Unity 6
    Render Graph by blending into GBuffer0/2/3 (albedo multiply, **smoothness +0.7**, extra
    occlusion), and reports **bloom flicker** from the raised smoothness. Forward+ has no G-buffer,
    so the wetness has to live in the lit shaders: a global wetness uniform plus the cover height
    map. *My own knowledge, unverified here:* the alternative is URP's DBuffer decals, which can
    write smoothness in Forward+ but need a depth prepass and only reach shaders that support
    decals.

**D. Foreshortening and what reads from above**

15. **Derived geometry (my arithmetic):** a vertical streak seen at pitch θ projects to L·cos θ.
    At the screen centre that is **0.67 of its side-on length at 48°** and 0.62 at Byzen's 52°. So
    the streaks are shorter but not dots, and Byzen's remark is perception rather than geometry.
    Take a drop falling at 9 m/s (the upper end of Lagarde's 3–9 m/s), with 3,094 px/rad at 4K and
    FOV 40. It crosses the screen at **about 620 px/s at 30 m (10 px a frame at 60 Hz) and about
    115 px/s at 160 m (2 px a frame)**. A 1 mm drop is far below a pixel at every zoom. **At the
    far zoom, falling streaks barely register**, so rain there has to come from the ground and the
    air.
16. **Fresnel at this pitch:** the project's own water work measured about **2.2 %** reflectance
    at the play camera's 48° (CLAUDE.md, *Water*). The raised smoothness of wet ground is weak from
    above except in highlights from the sun and lamps, so the **darkening** and the **ripples and
    splashes** carry the reading. Byzen's evidence says the same: splashes, ripples, puddles,
    shelter and a draining colour grade.

## Recommendation

Ranked for this camera and budget:

1. **Stateless GPU-procedural rain. This is the one I recommend.** One `RenderPrimitives` draw of
   a fixed count (start at about 16k) of camera-facing quads:
   - **Placement:** each quad's position is a hash of its instance id plus time × velocity,
     wrapped (`frac`) inside a box **anchored to the focus point in world space**, so drops stay
     put when the camera pans.
   - **Scale with zoom:** the box's footprint grows with zoom distance, which keeps the density on
     screen roughly constant.
   - **Readable at every zoom:** a **minimum width of about 1 px**, with alpha scaled down as
     coverage drops, stops thin streaks aliasing into sparkle. **Short tails** and a random
     **wind slant** (a slant restores the length the view foreshortens) avoid Byzen's comb, and
     the streaks fade out at the near and far ends of the box.
   - **Cover:** the vertex shader samples the **CPU-built cover height texture** and collapses any
     streak below the roof over it. There is no per-pixel occlusion cost and no extra camera.
   - **Splashes:** a second draw of the same kind, a few thousand short-lived crowns hashed onto
     columns and lifted to the height map's top surface. No CPU spawning and no read-back.

   **Expected cost:** CPU is a few globals and one draw, about **0.01 ms**. The GPU cost is my
   estimate, not a measurement: 16k streaks × about 2 × 30 px is roughly 1 M blended pixels (12 %
   of a 4K frame) of trivial ALU, which should come to **0.1–0.3 ms at 4K**. The quads are
   additive or unsorted alpha, so nothing needs sorting.
2. **VFX Graph.** The GPU cost is similar, and it adds compute dispatches, a package dependency
   and authoring in the editor. Its extra features (collision against the depth buffer, events on
   impact) are weaker here than a lookup in a height map the simulation already owns. Choose it
   only if the owner wants to author the look in a graph.
3. **CPU `ParticleSystem`.** Tens of thousands of live particles across a 160 m view overrun
   0.05 ms of CPU. The documented set-ups are small boxes for close cameras.
4. **Screen-space layers.** The cheapest pass at constant cost, but they are built for a
   horizontal view, cannot keep a roof dry without a depth compare, and read as a pane over the
   world from 48°. At most, a faint storm overlay.

**The ground carries more than the air**, so budget for it as well:

- **Wetness:** a global `_Wetness` (0→1 while it rains, back to 0 over about a game minute),
  masked by the cover texture. It darkens porous albedo towards ×0.7 (a stronger ×0.3 only on
  earth and grass), raises smoothness towards 0.8–0.9, and leaves metal alone.
- **Ripples on water:** baked flipbook rings, and on puddles where these exist, faded out by
  distance before they mip away.
- **Colour:** a grade that lowers saturation and contrast.

Two cases need a decision rather than a guess. **(a)** Whether the far zoom needs streaks at all:
the cheapest experiment is one Play session with a toggle, comparing the far zoom with and without
streaks. **(b)** Whether wetness can reach the Synty materials: grep which shaders the chunk,
foliage and figure passes actually use before designing the wetness plumbing.

## Sources

- https://seblagarde.wordpress.com/2012/12/27/water-drop-2a-dynamic-rain-and-its-effects/
- https://seblagarde.wordpress.com/2013/01/03/water-drop-2b-dynamic-rain-and-its-effects/
- https://seblagarde.wordpress.com/2013/03/19/water-drop-3a-physically-based-wet-surfaces/ (search summary only)
- https://seblagarde.wordpress.com/2013/04/14/water-drop-3b-physically-based-wet-surfaces/
- https://advances.realtimerendering.com/s2006/Chapter3-Artist-Directable_Real-Time_Rain_Rendering_in_City_Environments.pdf (search summary only)
- https://delosadajoaquin.wixsite.com/gdctalkstotext/post/talk-title-simulating-tropical-weather-in-far-cry-6-part-2
- https://www.cyanilux.com/tutorials/rain-effects-breakdown/
- https://www.moddb.com/games/byzen-embers-of-revolution/news/weather-devlog-building-rain-for-a-camera-that-looks-straight-down (403; search snippets only)
- https://www.proceduralpixels.com/blog/render-graph-creating-rain-atmosphere
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.RenderPrimitives.html
- https://docs.unity3d.com/ScriptReference/Graphics.DrawProcedural.html
- https://timberborn.featureupvote.com/suggestions/204554/add-weather-features-rain
- https://gamerant.com/best-city-builders-dynamic-weather-systems/

## Confidence

- **A, how rain is drawn: medium.** The shipped techniques and their timings are first-hand
  (Lagarde, and the Far Cry 6 summary is second-hand). The genre survey is **low**: nothing is
  documented. Byzen comes from snippets only.
- **B, occlusion: high** that a top-down cover map is standard practice. **Medium** that a CPU
  grid height map is enough, which is my derivation.
- **C, wetness: high** for the physical rules and published factors. **Medium** for the shader
  factors, which are one author's tuning. **Medium** for the DBuffer remark, which is unverified.
- **D, foreshortening: high** for the arithmetic. **Medium** for what reads, which rests on one
  devlog and the project's Fresnel measurement.
- **The GPU estimate of 0.1–0.3 ms: low** until measured. It is arithmetic, not a timing.

## Could not be determined

- How any of Frostpunk, Timberborn, Farthest Frontier, Manor Lords, Cities: Skylines or Anno draws
  rain, or what it costs.
- The full Byzen devlog (403): its streak counts, tail lengths, shelter map resolution and cost.
- Modern GPU timings for any rain technique. Every number here is PS3/360 at 720p.
- Whether the Synty pack's Shader Graph materials can take a global wetness term without being
  edited. They are licensed and gitignored, so edits cannot be committed.
