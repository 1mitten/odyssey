# Drawing bullets: tracers, flashes and impacts in URP
*Lane D (Unity architecture and feasibility) · 2026-09-25 · capped at 12 searches and 10 reads (12 searches and 10 fetch attempts used; 2 fetches returned a page, 8 were blocked by the container's egress proxy, so those findings rest on search excerpts of the page rather than the page)*

> *Coordinator's note, 2026-09-25:* eight of ten page reads were refused by the container's proxy; the confidence line says which findings rest on excerpts. Design 47 §4 takes the instanced bucket first and names this file's procedural pass as the alternative.

## Question

What is the cheapest way in Unity 6 URP to draw many tick-exact bullet tracers, muzzle flashes and impact puffs so that a hit visibly connects with its target and a miss reads as a near miss — given a 60 Hz simulation that owns each bullet's fire tick, impact tick, start and end point; a presentation side that knows tick + alpha; a 48° slice camera at up to 4K; and a 5 ms frame budget about half spent. Existing passes to reuse: colour-bucketed instanced ground quads, a GPU-procedural rain pass on `Graphics.RenderPrimitives` with no per-frame allocation, a pooled point light per fire under Forward+, and one shared world-space `ParticleSystem` fed by `EmitParams`. Forbidden: one submission per thing (P10), a per-frame allocation on a hot path, and two translucent draws tied on sort key (P17).

## Findings

**Tracer techniques**

1. **`Graphics.RenderPrimitives` with a structured buffer is the documented replacement for `DrawProcedural` for non-indexed geometry.** The shader fetches or computes vertex data from `SV_VertexID`; a `StructuredBuffer` bound through a `MaterialPropertyBlock` is readable from an ordinary vertex/fragment shader, not only from compute, and needs shader model 4.5. A procedural quad is built per vertex by dividing and taking the modulus of `SV_VertexID` to find which quad and which corner it is. [Unity scripting reference `Graphics.RenderPrimitives`, `Graphics.DrawProcedural`; Unity Discussions "Using Graphics.RenderPrimitives with Shader Graph"; Ronja's tutorial 051 — all via search excerpts]

2. **The procedural-quad route has a measured ceiling far above anything a firefight needs:** Refsa's ProceduralGPUQuads renders 16 million quads (100 million generated vertices a frame) at 60 fps on an RTX 2070 from structured buffers with no mesh. A tracer pass of a few hundred segments is therefore priced by its fixed cost — one draw call and one buffer upload on change — not by its count. [github.com/Refsa/ProceduralGPUQuads, via search excerpt]

3. **`Graphics.RenderMeshInstanced` takes a `NativeArray` of per-instance data (a `Matrix4x4` or a custom struct) and the API reads it by unsafe pointer without marshalling**, so a reused `NativeArray` is allocation-free per frame. Unity computes one bounds for the whole batch unless `RenderParams.worldBounds` is set, and **culls and sorts all instances as a single entity** — which bears on sorting below. The material must have GPU instancing enabled. [Unity scripting reference `Graphics.RenderMeshInstanced`, `RenderParams`; UnityCsReference `Graphics.cs` — via search excerpts]

4. **A data-oriented projectile pool over `DrawMeshInstanced` is measured at about six times the throughput of a well-written GameObject pool**: a struct array of projectiles, a `Matrix4x4[]` filled each frame, one submission per projectile type, 900+ fps at 3–5 draw calls on Unity 2019.3, with multi-mesh projectiles pre-baked into one mesh to hold the call count. [github.com/ShilohGames/InstancingPoolDemo, read]

5. **Fewes' VolumetricTracer draws a soft volumetric tracer from a unit cube and one material, instanced, with soft scene blending and camera intersection**; size is the cube's scale. It is the reference for a tracer that has no visible edge from any angle, at the cost of a per-pixel volumetric shader; no cost figure is published. [github.com/Fewes/VolumetricTracer, read]

6. **`TrailRenderer` / `LineRenderer` cost one draw call per effect and cannot be batched**, so "10–20 missiles with trails" is already a draw-call problem; the community answer is one `DrawMeshInstancedIndirect` per projectile type. Bullet-hell posts converge on the same ladder: one instanced draw per bullet manager, no GameObjects, and Jobs + Burst for 10,000+. [Unity Discussions "Bullet Trail Renderer", "Optimizing Trail and Line renderers"; itch.io Bullet Hell Jam thread; github.com/MPozek/Unity-Bullet-Hell — via search excerpts]. Under P10 this rules (c) out on its own.

7. **A `ParticleSystem` in Stretched Billboard mode aligns each particle to the camera and to its velocity; `Velocity Scale` stretches with speed and `Length Scale > 1` keeps a particle always longer than wide.** A published tracer recipe is the default particle material, Stretched Billboard, Length Scale 20, particle size 0.05 — a thin line about 1 m long. Two caveats from the tracker and forum: a bug report that stretched length is scaled twice under Speed Scale, and a thread on stretched billboards having a minimum length. [Unity manual "Renderer module"; github.com/DancingPhoenix88/particles-training; Unity issue tracker "Particles length is scaled twice…"; Unity Discussions "stretched billboard minimum particle length" — via search excerpts]. Note (own inference, not sourced): the stretch direction comes from the particle's *velocity*, and a particle's position is advanced by the system's own clock, so to be tick-exact every live tracer particle must be rewritten each frame through `GetParticles`/`SetParticles` with a fake velocity — a CPU pass over a reused array, which the procedural route does not need.

**What a tracer is for, and how it reads**

8. **Game tracers are illusions of direction and speed, not bullets.** The common recipe is an effect spawned between muzzle and hit point that travels over one or two frames as a line renderer, mesh streak or VFX Graph ribbon; the design advice is restraint — tracers should help the player read combat, many games show them only occasionally or only for enemy fire, and trails should be **short and thin because over-long trails look messy**. Pooling flashes, impacts and tracers is named as the allocation fix. [animost.com "Shooting VFX in Unity", via search excerpt; page blocked]

9. **Projectile trails are a design tool for showing how a shot travels through space** — Halo Infinite's spread tracers are the cited example of VFX telling the player where fire is going. [gamedeveloper.com "VFX as game design tools", via search excerpt; page blocked]

10. **A near miss is sold by sound as much as by picture.** Sound libraries and player communities separate the "bullet by" — the whizz or snap of a round passing near the listener, often heard apart from the gunshot — from the shot itself, and treat the crack as the readable alternative to a constant whizz. The impact-feel literature finds the coherence of audio feedback critical and that without a distinct marker "the boundary between missing and hitting is indistinct". [add.app gun sound effects; Steam Workshop "Occasional Bullet Crack"; arXiv 2208.06155 "What Features Influence Impact Feel?" — via search excerpts]

**Muzzle flash**

11. **Muzzle-flash sprite sheets for games run 8–12 frames at 45–95 ms a frame; film compositing uses a flash of 2–3 frames.** The game sheets are for a first-person camera; at a 48° camera tens of metres away the film figure — two or three frames, 50–100 ms, three to six ticks — is the one to reach for, because the flash's job here is to say *who fired*, not to look like fire. [nuhemugames.itch.io VFX pack; mycreativefx.com muzzle flash download — via search excerpts]

12. **Forward+ in URP lifts the eight-lights-per-object limit to 16, 32 or 256 lights per camera by hardware and API, and community measurement puts its break-even at about five or six real-time lights**, below which the clustering costs more than plain Forward. A community GPU-driven Forward+ for URP quotes 64 visible lights with "predictable GPU-based light culling". [URP 14 manual "Forward+ Rendering Path"; thegamedev.guru "Forward+ in URP 14+"; Unity Discussions "GPUDrivenForwardPlus" — via search excerpts; the first two pages blocked]

13. **Order-of-magnitude figures for clustered lighting (not Unity):** 1,024 lights of radius 10 at 1080p ran at 89.9 fps under Forward+ against 1.7 fps under Forward; an RTX 4070 held 60 fps with 2,000 lights at 1080p and 300–500 at 4K; a cluster-build target under 0.3 ms at 1080p. The lesson that transfers is that a short-range point light touches few clusters and its marginal cost is small once the fixed cluster pass is being paid — but the project already pays that pass, and the 4K figure says the per-light term is real at four times the pixels. [Forward-Plus-Renderer (shakesoda), CIS 565 project 5, lobinuxsoft/kooch issue 485 — via search excerpts]

**Shell ejection**

14. **Nothing found prices an ejected shell; what exists is implementation trouble** — top-down 2D threads on making casings fly and drop, and the note that particle casings do not land flat because a particle's collider is a sphere. [Unity Discussions "Top down 2d shooter shell ejection", "Shell ejection and rigidbody question"; GameDev.net "bullet shell ejection system" — via search excerpts]. At a 48° camera from 20–160 m a 9 mm case is under a pixel; it is a first-person garnish.

**Sorting**

15. **URP sorts transparents within one queue by distance from the camera, back to front; a particle material's `Priority` (render queue) and a water shader's queue are the two levers, and the water's `ZWrite` decides whether a transparent can intersect it.** Crest renders its ocean at `Transparent − 100` and writes depth so later transparents sort against it; Stylized Water's troubleshooting says that with `ZWrite` off a transparent is sorted by position, order-in-layer and queue and lands wholly behind or wholly in front of the water. [crest.readthedocs.io "Rendering"; alexander-ameye.gitbook.io Stylized Water troubleshooting; staggart.xyz Stylized Water 2 FAQ; NedMakesGames URP transparency part 3 — via search excerpts]

16. Combining 3 and 15 (own inference): a whole instanced or procedural batch carries **one** bounds and is sorted as one object, so a batch of tracers spread across the board sorts against the water or the rain by a single centre that moves every frame — which is exactly the tie P17 forbids. The fix is not a better centre; it is a distinct render queue value for the tracer pass, the rain pass and the water so that no two of them are ever compared by distance.

**What a 4K, 48° frame needs (arithmetic, not a source)**

17. At 60 ticks a second a bullet at 50–80 m/s moves **0.83–1.33 m a tick**, so a 20 m shot lasts 15–24 ticks (250–400 ms) and a 40 m shot twice that: the flight is long enough to be watched, and a head that advances one third to one half of a cell a frame will be seen as motion, not as a flicker. A streak two to three ticks long (2–4 m, about one cell) with the head at the lerped position matches the "short and thin" advice in 8 and the 1 m recipe in 7. Width: with the camera's 160 m reach spanning 3,840 px the scale at full zoom-out is about 24 px/m, so a 0.10 m streak is ~2.4 px before bloom at the far zoom and ~10 px at a 40 m framing; below 2 px an additive line disappears under anti-aliasing, which argues for a width floor in *pixels* in the vertex shader (the rain pass already does screen-space sizing) rather than a fixed metre width.

## Recommendation

**Tracer — build it into the rain's pattern: `Graphics.RenderPrimitives`, one structured buffer of segments, the quad made in the vertex shader (ranked 1).** Each bullet is one record — start, end, fire tick, impact tick, colour — written once when it is fired and removed when it lands; the shader takes the frame's tick + alpha as a uniform, lerps the head, trails a fixed 2–4 m tail behind it clamped to the muzzle, orients a camera-facing quad, and enforces a pixel-width floor. One draw call for every bullet on the board, no per-frame CPU pass, no per-frame allocation, and tick-exact by construction because the shader reads the simulation's own numbers. Additive, HDR intensity about 3–6 so URP's bloom lifts a hot core, `ZTest` on, `ZWrite` off. Ranked 2: `RenderMeshInstanced` of a unit quad from a reused `NativeArray` of custom per-instance structs — the same picture at the cost of a CPU loop that rewrites every live instance's matrix each frame, and the same single-bounds sorting hazard; take it only if the shader-side lerp proves awkward. Ranked 3: the shared `ParticleSystem` on Stretched Billboard — adequate, but every live particle must be re-positioned through `SetParticles` each frame with a faked velocity to stay tick-exact, and the double-scaling and minimum-length quirks are real. Ranked 4: `TrailRenderer`/`LineRenderer` — one draw call per bullet, excluded by P10. **The tie between 1 and 2 breaks on one observation: whether the rain pass's head-position uniform and buffer layout can be shared.** If the tracer buffer can be a second segment type in the rain shader's own path, 1 costs nothing new; if it needs its own shader anyway, 2 is the same draw-call count with a familiar API. The cheapest experiment is a PlayMode arm in `FrameTimeTests` that draws 500 static segments both ways in one run and reads `FrameSection` for each — an afternoon, and it answers the width and intensity questions in the same shot if it screenshots at noon and at dusk.

**Muzzle flash — a zero-length record in the same buffer, drawn as a wider camera-facing quad for 3–6 ticks with a two-or-three-frame flipbook by age (ranked 1); the pooled point light only while the camera is within a measured distance (ranked 2 as a companion, not an alternative); a small mesh (ranked 3, nothing at this camera justifies it).** At 20–160 m the gun is a few pixels: the flash is the thing that says *who fired* before the tracer has left the muzzle, so it is needed, but 50–100 ms is enough. The light is the one term that scales with pixels at 4K (finding 13), so gate it on zoom and measure the frame with ten flashes lit against ten unlit before keeping it on at all zooms.

**Impact — the existing shared world-space `ParticleSystem` via `EmitParams` (ranked 1): three to six dust particles on a ground hit, a chip on a wall, red particles on a body, 300–500 ms, no new draw call.** A second shared system for blood only if one material cannot carry both tints. Ranked 2: a procedural puff in the tracer buffer — possible, but the particle system already exists and already costs one call. **The near miss is three things and none of them is a new pass:** the tracer's end point is a cell or two past the target on the ground, so the streak visibly *continues* through the target's cell; the dust puff lands there rather than on the body; and the sound at the target's cell is the whizz or crack (the "bullet by") rather than the thud. A hit is the streak *stopping* on the body plus blood plus the hit sound. The impact tick is the frame the puff is emitted, so hit and miss diverge on the same tick the simulation decided them.

**Shell — do not draw it at this camera.** If the owner wants the gesture, one particle from the shared system, no collision, half a second: the cost is nil and so is the readability.

**Sorting — assign explicit render queue values so the water, the rain and the tracers are never in one queue**: water lowest with depth written (Crest's `Transparent − 100` pattern), tracers next, rain last, each a distinct integer. A batch sorts as one object by one bounds, so "just let URP sort it" is the P17 tie waiting to happen; a fixed queue order is the whole fix and costs nothing.

## Sources

- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.RenderPrimitives.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Graphics.DrawProcedural.html
- https://discussions.unity.com/t/using-graphics-renderprimitives-with-shader-graph/1527483
- https://www.ronja-tutorials.com/post/051-draw-procedural/
- https://github.com/Refsa/ProceduralGPUQuads
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.RenderMeshInstanced.html
- https://docs.unity3d.com/ScriptReference/RenderParams.html
- https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Graphics/Graphics.cs
- https://github.com/ShilohGames/InstancingPoolDemo
- https://github.com/Fewes/VolumetricTracer
- https://discussions.unity.com/t/bullet-trail-renderer/1655162
- https://discussions.unity.com/t/optimizing-trail-and-line-renderers/468916
- https://itch.io/jam/bullet-jam-2021/topic/1312025/20k-bullets-in-unity-optimised-bullet-spawning-system-free-for-you-guys-to-use
- https://github.com/MPozek/Unity-Bullet-Hell
- https://docs.unity3d.com/Manual/PartSysRendererModule.html
- https://github.com/DancingPhoenix88/particles-training
- https://issuetracker.unity3d.com/issues/particles-with-render-mode-stretched-billboard-scales-incorrectly
- https://discussions.unity.com/t/particle-systems-stretched-billboard-minimum-particle-length/623655
- https://animost.com/ideas-inspirations/shooting-vfx-unity/
- https://www.gamedeveloper.com/design/vfx-as-game-design-tools-the-ludology-of-vfx-in-god-of-war-ragnarok-and-halo-infinite
- https://add.app/sound-effects/gun-sound-effects/
- https://steamcommunity.com/sharedfiles/filedetails/?id=895869067
- https://arxiv.org/pdf/2208.06155
- https://nuhemugames.itch.io/pixel-art-vfx-pack-vol01-24-effects-256-frames
- https://mycreativefx.com/blog/357-free-muzzle-flash-vfx-download-4k-gun-effects-for-video-editing
- https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/rendering/forward-plus-rendering-path.html
- https://thegamedev.guru/unity-gpu-performance/forward-plus/
- https://discussions.unity.com/t/gpudrivenforwardplus/1711158
- https://github.com/shakesoda/Forward-Plus-Renderer
- https://github.com/JerryYan97/Project5-WebGL-Forward-Plus-and-Clustered-Deferred
- https://github.com/lobinuxsoft/kooch/issues/485
- https://forum.unity.com/threads/top-down-2d-shooter-shell-ejection.481900/
- https://forum.unity.com/threads/shell-ejection-and-rigidbody-question.130063/
- https://www.gamedev.net/forums/topic/684419-how-to-implement-a-bullet-shell-ejection-system/
- https://crest.readthedocs.io/en/4.16/user/rendering.html
- https://alexander-ameye.gitbook.io/stylized-water/support/troubleshooting
- https://staggart.xyz/unity/stylized-water/sws-2-docs/?section=troubleshooting-10
- https://nedmakesgames.medium.com/transparent-and-crystal-clear-writing-unity-urp-shaders-with-code-part-3-f6ccd6686507

## Confidence

**Medium.** The technique ranking (procedural buffer > instanced quads > particle system > trail renderer) is supported by several independent sources and by the API's own documentation, and it agrees with what the project has already measured on its rain and mark passes; but eight of ten page reads were refused by the proxy, so the muzzle-flash timings, the Forward+ break-even, the sorting levers and the "short and thin" advice rest on search excerpts rather than full pages, the width and intensity figures for a 48° camera at 4K are arithmetic of my own, and no source measured a tracer pass in milliseconds.

## Could not be determined

- Any measured cost in ms or draw calls for a tracer pass in Unity by any of the four techniques; the only figures are throughput ceilings (Refsa's 16 M quads, InstancingPoolDemo's 900 fps) and non-Unity clustered-lighting benchmarks.
- The exact semantics and sign of `ParticleSystemRenderer.sortingFudge` and its interaction with a `RenderPrimitives` pass — the Unity manual page and the Toca Boca sorting article were both blocked.
- Whether URP's bloom threshold in the project's volume profile will lift a streak at intensity 3–6 without also lifting the meadow's specular; only a screenshot answers it.
- Whether additive survives a noon meadow at full zoom-out, or a premultiplied-alpha streak with a dark rim reads better; the RealtimeVFX practitioner thread on semi-realistic bullet trails was blocked.
- The marginal 4K cost of one short-range pooled point light under URP Forward+ specifically (thegamedev.guru's measurements were blocked); only the non-Unity order-of-magnitude survives.
- Any source on what tracers look like from a 48° tactical camera (RimWorld, Door Kickers, XCOM); the search returned first-person material only.

## Layer question 5: can you shoot up and down?

Nothing found in sources. One observation follows from the recommendation rather than from a page: a segment record is a straight world-space line between two points and is indifferent to the layers its ends sit on, so a shot from a rooftop to the street draws correctly for free — what is not free is the slice. A tracer whose far end is above the drawn ceiling or below the drawn floor must be clipped by the same rule the rain pass already applies against roofs and the slice height, or the streak will draw through a storey the player cannot see; the clip belongs in the tracer's vertex or fragment shader beside the rain's, and it is the one thing the between-layers case adds.
