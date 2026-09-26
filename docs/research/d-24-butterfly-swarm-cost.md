# d-24 — The cheapest way to draw a few hundred glowing ambient creatures

**Question.** What is the cheapest architecture in Unity 6000.3 / URP 17.3 for drawing 150–500
individually behaving, animated, glowing butterflies, and what does each option cost? Also: the
cheapest correct way to put a pool of coloured light on the ground under each one at night, and how
HDR emission above the 1.1 bloom threshold behaves on small, thin, moving objects. Read against
`PowerLinePass.cs`, `OdysseyFoliage.shader`, `GoldenHour.cs`, `FireDirector.cs` and the URP assets.
Cap: 8 searches and 8 reads (subagent, 2026-09-25).

## Findings

1. **The pipeline is Forward+** (`PC_Renderer.asset` `m_RenderingMode: 2`). `PC_RPAsset.asset`:
   HDR on, the SRP Batcher on, per-pixel additional lights, MSAA off in the asset, the depth and
   opaque textures on. Bloom is threshold 1.1, intensity 0.9, scatter 0.65, half-resolution,
   high-quality filtering off (`GoldenHour.cs` 155–168).
2. **A. CPU state and one instanced draw.** Stepping 300 small structs is about 0.01–0.03 ms under
   Mono (estimate; a Burst job steps 5,000 particles in 0.16 ms). A Unity 6000.3 URP measurement on
   an RTX 3060 puts `RenderMeshInstanced` at about 0.09 µs an instance, so 300 is about 0.03 ms
   plus the call; each call carries at most 511 instances with two matrices. **The buffer variant**
   — one procedural call reading a structured buffer by instance id, the vertex shader building the
   transform — issued 90,000 instances in about 0.26 ms, so 300 is negligible, and has no
   511-instance split. The SRP Batcher does not apply to either path, which does not matter for one
   draw. One call for the wings and one for the glow. Landing and fleeing are fully supported.
3. **B. Fully stateless** (position from seed and time in the vertex shader): about 0.005 ms CPU
   and one draw, deterministic for free — but **it cannot remember being startled**, so fleeing is
   impossible, and landing needs a height texture. Right for aimless drift, wrong for this brief.
4. **C. A code-created `ParticleSystem`.** Individual behaviour needs `GetParticles`/`SetParticles`
   every frame, which is option A with extra managed copies (0.05–0.1 ms estimated at 300). It runs
   on scaled `deltaTime`, so its `simulationSpeed` must be kept in step with the game. No advantage.
5. **D. VFX Graph.** A new package and compute: a new build-stripping surface in a project that has
   already needed four keep-alive fixes. A fixed GPU cost per system; fleeing still needs walker
   positions passed in. Overkill for 500. Unmeasured.
6. **The glow pool.**
   - **Real lights: reject.** Forward+ has no per-object limit but a **per-camera limit of 256 on
     desktop** (32 on mobile), so 300 butterflies exceed it and lights pop. Every light adds
     per-pixel cost in the tiles it covers, and at 4K the frame is already GPU-bound.
   - **Decal projectors: reject.** They need the Decal renderer feature and an extra pass.
   - **An additive quad in the transparent queue, ZWrite off**, from the same buffer, faded by the
     daylight factor: one draw. With the depth texture on, a soft depth fade stops it slicing into a
     terrace riser.
7. **Bloom on thin moving HDR.** Sub-pixel regions above the threshold "pop in and out of
   existence during movement" — the firefly artefact. A butterfly wing a few pixels across, halved
   again by the half-resolution prefilter, would shimmer. Bloom's **clamp** is the wrong fix
   because it is global and would dim the sun glints bloom was adopted for. Better: keep the wings
   at or below 1.0 and carry the glow in a small soft sprite a little over the threshold, or draw
   the halo yourself — which is stable and survives bloom being off.

## Recommendation

**Option A through a structured buffer and one procedural call** (`Graphics.RenderPrimitives`, the
precedent `RainDirector` set): presentation-side CPU state, a local seed, no cell, save or hash. A
second call from the same buffer draws the halos and pools additively, only at night. The glow is a
self-drawn halo, not bloom on the wing. Expected: under 0.1 ms CPU at 300, two draw calls, GPU cost
near zero but for the pool's overdraw.

If `RenderMeshInstanced` is preferred to stay close to `PowerLinePass`, it ties on cost at 300. The
tie-breaker is whether the count ever exceeds 511 (the buffer path costs nothing there); the
cheapest experiment is to time both at 300 and 1,000 inside one `FrameTimeTests` run.

## Sources

- https://www.proceduralpixels.com/blog/rendering-90000-particles-in-unity (Unity 6000.3.11 URP, RTX 3060)
- https://docs.unity3d.com/6000.0/Documentation/Manual/urp/lighting/light-limits-in-urp.html
- https://docs.unity3d.com/6000.0/Documentation/Manual/urp/rendering/forward-rendering-paths.html
- https://docs.unity3d.com/6000.0/Documentation/Manual/urp/rendering/forward-plus-rendering-path-limitations.html
- https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@14.0/manual/rendering/forward-plus-rendering-path.html
- https://docs.unity3d.com/ScriptReference/Graphics.RenderMeshInstanced.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.RenderMeshPrimitives.html
- https://catlikecoding.com/unity/tutorials/custom-srp/hdr/
- https://docs.unity3d.com/6000.0/Documentation/Manual/urp/post-processing-bloom.html
- https://docs.unity3d.com/Manual/PartSysInstancing.html
- Repo: `Assets/Settings/PC_Renderer.asset`, `Assets/Settings/PC_RPAsset.asset`,
  `Assets/Odyssey/Presentation/Rendering/PowerLinePass.cs`, `WindDirector.cs`,
  `Assets/Odyssey/Presentation/World/FireDirector.cs`, `Assets/Editor/Odyssey/GoldenHour.cs`

## Confidence

1. **High** (read from the files).
2. **Medium** — the per-instance cost is extrapolated from a 90,000-instance measurement.
3. **High.**
4. **Medium.**
5. **Low** — no measurement.
6. **High** on the 256 limit and the per-object rule; **medium** on the cost ranking.
7. **Medium** — whether URP's prefilter already suppresses fireflies was not verified.

## Could not be determined

- A measured VFX Graph fixed cost, or a measured `SetParticles` cost.
- What a Forward+ light tile costs at 4K.
- Which two renderer features are on, and so whether decals are already available.
- Whether the post anti-aliasing at the owner's settings dampens the shimmer.
