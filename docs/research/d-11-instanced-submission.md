# Lane D11 — Instanced submission cost and the sanctioned fast path

## Question

Why would `Graphics.RenderMeshInstanced` cost about a microsecond per instance and grow superlinearly with total instance count in Unity 6 (6000.3.x) / URP 17.3 with Render Graph on D3D11, and what is the sanctioned fast path for tens of thousands of static instances per frame?

Prompted by `Assets/Editor/Odyssey/RenderBench.cs` on an RTX 5070 Ti, 1920 × 1080, editor batchmode, `camera.Render()` into a RenderTexture, one GPU sync per 40 frames: 0.8 ms/frame empty; 16 ms at 14,400 cubes (~29 calls of ≤511); 92 ms at 23,156 instances (grass tufts, second material); 610 ms at 31,747. Per-instance cost climbs from ~1 µs to ~19 µs.

Method: 12 web searches, 10 page reads (the cap), plus reading the bench, `ChunkRenderer.cs`, `MaterialCache.cs` and the URP assets in `Assets/Settings/`. Where a finding is inferred rather than documented it is marked as such.

## Findings

### 1. What `RenderMeshInstanced` is documented to do, and what it is not

The scripting reference (6000.3) states the load-bearing facts and is silent on the rest:

- It "submits" instances; the mesh is rendered "as part of the normal rendering process", i.e. it is a per-frame immediate-mode renderer, not a retained object. The examples call it every frame from `Update`. Nothing is cached between frames.
- Hard limits: "You can only render a maximum of 1023 instances at once", and "by default, Unity uses an objectToWorld matrix and a worldToObject matrix for each instance, which means you can render a maximum of 511 instances at once". The `worldToObject` half is derived by Unity from the matrices you pass, so **each call inverts every matrix on the CPU, every frame** (inference from the wording; the reference does not say where the inverse is computed, but the caller only supplies `objectToWorld`). `#pragma instancing_options assumeuniformscaling` removes it, which requires editing the shader; that is not possible for Synty's Shader Graph materials and not done for the bench's URP Lit either.
- Culling: "Unity treats all the instances of this Mesh as a single entity, relative to other rendered Meshes", using `RenderParams.worldBounds` or auto-computed bounds (auto-computation is another per-instance CPU walk). The bench sets a 2000 m cube, so nothing is ever culled; the chunk renderer sets per-batch bounds.
- Failure mode is loud, not silent: "Unity throws InvalidOperationException if the Material doesn't have Material.enableInstancing set to true", if the platform lacks instancing, or if `SystemInfo.supportsInstancing` is false. There is **no documented silent fall-back to one draw per instance** for this API; that behaviour belongs to the MeshRenderer/material-checkbox path.
- The reference says nothing about whether `instanceData` is copied, about allocations, or about editor behaviour.

What the community measured (no Unity staff reply in the thread): `RenderMeshInstanced` at ~0.25 ms and 9.3 KB GC per frame against `DrawMeshInstanced` at ~0.10 ms and 0 KB for the same work; the garbage came from `RenderInstancedDataLayout..ctor → Marshal.OffsetOf` being re-run per call. The issue tracker records that allocation as fixed in **2023.1.0f1** (UUM-35032), so it should not be present in 6000.3, but the "twice the time of `DrawMeshInstanced`" per-call overhead was never explained or declared fixed. Per-call overhead is however tens of microseconds, and the bench makes 29–116 calls; that accounts for at most a few milliseconds and cannot explain 610 ms.

An older but still-current mechanism (forum thread on `DrawMesh`/`DrawMeshInstanced` being CPU-bound): each call "internally add[s] an ImmediateRenderer node to a render queue used each frame" which is cleared at the end of the frame. Every URP pass that iterates renderers (shadow cascades, depth pre-pass, opaque, depth-normals) visits every such node, and each node carries its own instance data. That is per-call and per-pass work, linear in calls, and again not per-instance.

**Conclusion on Q1:** documented behaviour predicts a *linear* cost of roughly copy + invert + upload per instance per frame (order 0.5–1 µs on a non-threaded main thread is plausible) plus a small per-call overhead. Nothing in Unity's documentation or engineering posts predicts superlinear growth in instance count. Superlinearity therefore points at something the harness or the driver does, not at the API's design (see §5).

### 2. When an instanced draw is silently not instanced

The 6000.3 GPU-instancing manual (introduction and "choose a method" pages) is explicit and, for URP, discouraging:

- "GPU instancing works with custom shaders only if you disable the Scriptable Render Pipeline (SRP) Batcher or make a shader incompatible with the SRP Batcher." The SRP Batcher takes priority. This sentence is about **MeshRenderer** draws: the SRP Batcher claims any SRP-compatible material first, and the instancing checkbox is then ignored for those renderers. It does not describe `RenderMeshInstanced`, which issues an explicitly instanced draw and never enters the SRP Batcher's per-object path.
- The comparison table in "Choose a method for optimising draw calls" lists **GPU Instancing (material checkbox) as Built-In only — avoid in modern pipelines**, and says of BatchRendererGroup "prefer GPU Resident Drawer instead". The sanctioned order of processing is: static batching → SRP Batcher + GPU Resident Drawer/BRG for compatible shaders → GPU instancing for what remains → dynamic batching.
- "Meshes you render in a script using APIs that support GPU instancing in prebuilt materials, such as Graphics.RenderMeshInstanced, are supported", and Shader Graph materials support instancing in URP/HDRP (not in Built-In).

Conditions that can turn the bench's draw non-instanced or multiply its passes, with what is and is not established:

| Condition | Effect | Status |
|---|---|---|
| `Material.enableInstancing == false` | `InvalidOperationException`, not a fall-back | Documented. Both `MaterialCache` and the bench set it true. |
| Shader lacks `multi_compile_instancing` / `INSTANCING_ON` variant stripped | Draw runs the non-instanced variant: every instance gets the *same* matrix (they pile up at one transform) rather than one draw each | Inference from how the macros work; URP Lit ships the variant. If cubes render in distinct places the variant is live. |
| SRP Batcher on (`m_UseSRPBatcher: 1`) | No conflict for `RenderMeshInstanced`; the two do not share a path | Inference (see above). Not documented either way for this API. |
| `RenderParams.lightProbeUsage = BlendProbes` | Per-instance probe blending on the CPU each frame | Documented cost; bench leaves the default. Default value not confirmed. |
| `RenderParams.motionVectorMode` (default `Camera`) with URP Lit's MotionVectors pass | Could add a per-object motion pass when a feature requests motion vectors (TAA, motion blur) | Uncertain; nothing in the project requests them. |
| `receiveShadows = true` with main-light shadows on | Adds shadow-receive keywords, no extra geometry pass | Documented behaviour of URP; no cost multiplier. |

So the first local measurement is right: read `UnityEditor.UnityStats.drawCalls` and `UnityStats.instancedBatches` (or a RenderDoc capture) after a frame. If `drawCalls` ≈ calls made (29 / 41 / 116) the instancing is live and §5 applies; if it ≈ instance count, the variant is missing and the fix is on the shader side.

### 3. How many times URP 17 draws each submission in this project

From `Assets/Settings/PC_RPAsset.asset` and `PC_Renderer.asset`: Rendering Mode **Forward+** (`m_RenderingMode: 2`), **Depth Priming Disabled**, **Copy Depth Mode After Opaques**, `m_RequireDepthTexture: 1`, `m_RequireOpaqueTexture: 1`, HDR on, MSAA off, main-light shadows on with **4 cascades**, additional-light shadows on, Native RenderPass on, SRP Batcher on, **GPU Resident Drawer off** (`m_GPUResidentDrawerMode: 0`).

The Universal Renderer reference (6000.3) states: Depth Texture Mode "After Opaques" copies depth after the opaque pass rather than generating it with a pre-pass; a depth pre-pass exists only when "Force Prepass" is chosen, when Depth Priming is Forced (or Auto with a pre-pass already required), or when a renderer feature needs the camera normals texture (SSAO, decals), in which case a `DepthNormals` pre-pass is added. Forward+ changes light culling, not geometry passes.

Applied to the bench: shadows are off on the cubes, no pre-pass is configured, no normals-requiring feature is known to be on. **Each submission should be drawn once per frame**, followed by one full-screen depth copy and one opaque copy, which sit in the 0.8 ms floor. Two caveats, both uncertain: the editor's scene-view path forces a depth pre-pass, which does not apply to a batchmode `camera.Render()`; and I could not read the URP 17 source within the cap to confirm that Forward+ never requests a pre-pass on its own. The Frame Debugger (or the RenderDoc pass list) settles it in minutes.

For the real chunk renderer the multiplier is different: walls, objects and (optionally) foliage cast into up to four cascades, so a casting instance is drawn up to five times. Terrain and grass are already excluded from casting in `ChunkRenderer.cs`, which is the right call.

### 4. The sanctioned fast paths

**GPU Resident Drawer (Unity 6, URP/HDRP).** The sanctioned zero-code path: "automatically uses the BatchRendererGroup API to reduce the number of draw calls". Requirements per the manual: Forward+ (we have), SRP Batcher on (we have), Project Settings → Graphics → BatchRendererGroup Variants = **Keep All**, GPU Resident Drawer = Instanced Drawing on the pipeline asset, objects with a **Mesh Renderer** component, static GI only, no Light Probe Proxy Volume, no `MaterialPropertyBlock`, no per-camera movement, shaders that support DOTS instancing (Unity's URP shaders and Shader Graph do). It adds GPU occlusion culling as an option. The performance page claims CPU wins, warns it "slightly increases GPU workload", warns of overdraw from merged instanced draws on desktop GPUs, and recommends Depth Priming Auto/Forced under Forward+ to counter that. It gives **no instance-per-millisecond figure**; the marketing figure is "up to 50 % less CPU". The catch for Odyssey: it only sees **MeshRenderer components**, so it contradicts the "no GameObject per cell" design unless we accept one static GameObject per drawn surface cell.

**BatchRendererGroup (BRG).** "An API for high-performance custom rendering in projects that use a Scriptable Render Pipeline (SRP) and the SRP Batcher." Instance data lives in a **persistent `GraphicsBuffer`** you own (`unity_DOTSInstanceData`); you register batches of metadata (offsets into that buffer), and each frame Unity calls your culling callback, which writes draw commands (mesh, material, instance-index ranges). Shaders must support DOTS instancing: "every shader that BRG uses must support DOTS Instancing", which "is a special fast path of the SRP Batcher that only does a minimal amount of work for each draw call". Unity's statement (search snippet from the DOTS-instancing page, not read in full) is that "Shader Graphs and shaders that Unity provides in URP and HDRP support DOTS Instancing", with the practical proviso from the forums that the `DOTS_INSTANCING_ON` variant must not be stripped (BRG Variants = Keep All; do not strip unused variants in URP Global Settings). The 6000.x manual carries a full BRG section for URP ("Set up your project", "Creating a renderer", "Create batches", "Writing custom shaders"), i.e. it is supported on the Render Graph pipeline — GPU Resident Drawer is built on it. This is the path that maps one-to-one onto cached per-chunk matrix arrays: one batch per chunk, matrices uploaded once when the chunk is re-meshed, nothing re-sent per frame. **Uncertain:** whether the `worldToObject` inverse must be supplied by us (BRG's default property set includes `unity_WorldToObject`; it is our data to write, so yes in practice, computed at chunk build time rather than per frame).

**`Graphics.RenderMeshIndirect`.** Draw arguments come from an `IndirectDrawIndexedArgs` GraphicsBuffer; per-instance data must be read by the shader from a `StructuredBuffer` via `UnityIndirect.cginc` (`InitIndirectDrawArgs`, `GetIndirectInstanceID`). Culling/sorting is by `RenderParams.worldBounds` as a single entity. Requires compute-shader support (D3D11 qualifies). It removes the 511 limit and the per-frame matrix upload, and the toqoz write-up reports ~100,000 instances "with little performance impact" on a Ryzen 1700 / 1080 Ti — but it needs a **custom shader** to read the buffer, which rules out Synty's Shader Graph materials without rebuilding them, and it gives up URP's shadow and probe integration unless re-implemented. No Unity document offers an instances-per-millisecond figure for any of these APIs.

**Unity's own ranking** (choose-method page): GPU Resident Drawer over BRG over the material checkbox in URP. That ranking assumes GameObjects; for a GameObject-free renderer BRG is the intended API.

### 5. Editor batchmode specifics, and the best available explanation of the superlinearity

- The exec log shows `kGfxThreadingModeNonThreaded`; the interactive editor log shows `kGfxThreadingModeThreaded`. In the non-threaded mode "the main game thread [does] everything, converting Unity scenes into graphics API calls" (Unity Learn, rendering-mode description; `RenderingThreadingMode` reference). Every draw's driver cost lands on the timed thread, so the batchmode figure is CPU submission + D3D11 driver + whatever the GPU forces the CPU to wait on. A player build defaults to multithreaded rendering and would move the driver share off the main thread. This inflates the level, not the shape of the curve.
- `camera.Render()` in a loop never calls Present. Transient-resource renaming in D3D11 drivers (the pool behind `MAP_DISCARD` for the per-call instance constant buffers, 64 KB each for 511 instances) is normally recycled at frame boundaries. With no Present, each timed block of 40 frames is one enormous "frame" from the driver's point of view: 31,747 instances × 128 B × 40 frames ≈ 160 MB of discarded constant-buffer versions before the sync. When the rename pool is exhausted the driver stalls the CPU until the GPU releases older versions, and it does so more often the more bytes each frame discards. **This is a hypothesis, not a documented behaviour**, but it is the only mechanism found that is (a) per-byte-of-instance-data rather than per-call, (b) specific to a no-Present harness, and (c) superlinear. The cheapest test is a `GL.Flush()`/`ScriptableRenderContext.Submit()` boundary or a real swapchain (player build, or `-batchmode` with a Game view) and re-running the same three rows.
- Synchronous shader compilation on first variant use is real in the editor but is absorbed by the warm-up frames; it does not affect the timed block.
- 1920 × 1080 with HDR, depth copy and opaque copy is a fixed per-frame cost in the 0.8 ms floor and is not instance-dependent.
- `beginCameraRendering` fires "once per frame for each Scene view or Game view that is visible ... and once per frame for each manual call to Camera.Render", and the hook filters on camera identity, so in batchmode the submission is not being replayed for other cameras. Not a factor here.

Taken together: the documented model says linear; the per-call overhead and pass multipliers are small in this configuration; the shape of the curve is most likely the harness measuring driver back-pressure, and the level is inflated by non-threaded submission. Neither changes the strategic answer, because even the linear ~1 µs/instance/frame on the main thread is 30 ms for 30 k instances on a 2022 laptop at a 3× discount, which is the whole frame.

## Ranked recommendation

1. **Move the chunk renderer to `BatchRendererGroup`** (backed). One persistent `GraphicsBuffer` of per-instance `objectToWorld`/`worldToObject` written when a chunk is (re)meshed; one BRG batch per chunk; a Burst culling job that emits one draw command per (mesh, submesh, material, chunk) with the chunk's instance range. Materials stay as they are: URP Lit and Synty's Shader Graph shaders already carry the DOTS-instancing variant, so no shader work. Project setting change: BatchRendererGroup Variants = Keep All. Keep `RenderMeshInstanced` behind the existing `SubmitToGpu` switch as the fallback and as the control row in the bench.
   *Cost:* two to three days for one submitter class (~400 lines), a culling job, buffer lifetime and re-upload on chunk change, plus two bench rows. Longer build times from keeping all BRG variants.
   *Measure afterwards:* the same three bench rows (14 k / 23 k / 32 k) in batchmode and in a player build; per-instance cost should be flat and the main-thread cost per frame should no longer scale with instance count at all (only with draw commands). Also `UnityStats.drawCalls` per row, and the frame time on the laptop-class target.
   *Gate, first hour, can cancel the step:* before writing BRG code, (a) log `UnityStats.drawCalls` and `instancedBatches` per bench frame to confirm the draws are instanced, and (b) re-run the three rows with a Present-equivalent boundary each frame (player build or Game view). If (b) makes the curve linear at ~1 µs/instance, the bench was measuring the driver and BRG is still the right destination but is no longer urgent for M1; if it stays superlinear, BRG goes in now.
2. **GPU Resident Drawer with one static MeshRenderer per drawn cell.** Zero rendering code, GPU occlusion culling for free, Unity's preferred path. Rejected as the next step because it reintroduces a GameObject per cell (transform hierarchy, memory, scene churn on every dig or build) which the design forbids. Worth a one-hour bench row purely as a reference ceiling: spawn 14,400 static renderers, toggle the drawer, read the frame time.
3. **`RenderMeshIndirect` with a StructuredBuffer of matrices.** Fastest raw path and fewest moving parts, but needs custom shaders for every material, which means rebuilding Synty's Shader Graph shaders and re-implementing shadow casting and probe lighting. Right for a dedicated grass or particle renderer later, wrong for the general chunk path.
4. **Stay on `RenderMeshInstanced` and tune** (fewer calls, `assumeuniformscaling` where the shader is ours). Bounded gain: the per-frame re-upload and inverse remain, and the 511 ceiling stays.

If 1 and 2 seem tied after the gate: the observation that breaks the tie is whether world edits are frequent enough that per-cell GameObject churn shows in the profiler; the cheapest experiment is the reference row in option 2 with a dig-and-fill loop of 100 cells per second.

## Sources

Read in full:

- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Graphics.RenderMeshInstanced.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Graphics.RenderMeshIndirect.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/GPUInstancing.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/optimizing-draw-calls-choose-method.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/urp/reduce-rendering-work-on-cpu.html
- https://docs.unity3d.com/6000.4/Documentation/Manual/urp/gpu-resident-drawer-performance.html
- https://docs.unity3d.com/6000.4/Documentation/Manual/batch-renderer-group.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/dots-instancing-shaders.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/urp/urp-universal-renderer.html
- https://discussions.unity.com/t/graphics-rendermeshinstanced-generates-gc-and-is-slower-than-graphics-drawmeshinstanced/901625

Search-result snippets only (not opened; treat as secondary):

- https://issuetracker.unity3d.com/issues/gc-alloc-when-using-graphics-dot-rendermeshinstanced (fixed 2023.1.0f1, UUM-35032)
- https://forum.unity.com/threads/graphics-drawmesh-drawmeshinstanced-fundamentally-bottlenecked-by-the-cpu.429120/ (ImmediateRenderer per call, per frame)
- https://docs.unity3d.com/6000.0/Documentation/Manual/urp/gpu-resident-drawer.html (enable steps and GameObject requirements)
- https://docs.unity3d.com/6000.0/Documentation/Manual/batch-renderer-group-getting-started.html and https://discussions.unity.com/t/batchrenderergroup-complains-that-shadergraph-shader-lacks-dots_instancing_on-variant/948344 (Shader Graph DOTS variant, Keep All)
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Rendering.RenderPipeline.BeginCameraRendering.html (once per visible view and per manual `Camera.Render`)
- https://docs.unity3d.com/ScriptReference/Rendering.RenderingThreadingMode.html and https://learn.unity.com/tutorial/optimizing-graphics-in-unity (threading modes)
- https://toqoz.fyi/thousands-of-meshes.html (100 k instances via indirect on a 1080 Ti)

Local: `Assets/Editor/Odyssey/RenderBench.cs`, `Assets/Odyssey/Presentation/Rendering/ChunkRenderer.cs`, `Assets/Odyssey/Presentation/Rendering/MaterialCache.cs`, `Assets/Settings/PC_RPAsset.asset`, `Assets/Settings/PC_Renderer.asset`, `Logs/exec.log`.

## Confidence

- That `RenderMeshInstanced` re-uploads and re-processes every matrix every frame, with a 511 ceiling from the CPU-derived inverse: **high** (documented).
- That Unity's sanctioned path for large static instance counts in URP is GPU Resident Drawer / BatchRendererGroup, that BRG works on URP 17 Render Graph, and that URP Lit and Shader Graph materials carry the DOTS-instancing variant: **high** for the first two, **medium** for Shader Graph (from Unity's page as quoted in search results plus forum confirmation, not read in full).
- That the project configuration draws each non-casting submission once per frame: **medium** (from the renderer reference; Forward+ source not read).
- That the superlinearity is a no-Present harness artefact rather than an API property: **low to medium**; it is the only mechanism found that fits, and it is untested.
- That non-threaded batchmode inflates the absolute figure: **medium**.

## Could not be determined

- The engine-side implementation of `RenderMeshInstanced` in Unity 6: whether it copies the array, where the inverse is computed, and whether any per-type layout parsing survives the 2023.1 fix. No Unity source or engineering post was found within the cap.
- Any Unity-published instances-per-millisecond figure for `RenderMeshInstanced`, `RenderMeshIndirect`, BRG or the GPU Resident Drawer.
- Whether URP 17's Forward+ path ever adds a depth pre-pass on its own with Depth Priming disabled and Copy Depth after opaques.
- The default values of `RenderParams.lightProbeUsage`, `motionVectorMode` and `reflectionProbeUsage` when constructed from a material, and whether the MotionVectors pass in URP Lit is ever invoked for `RenderMeshInstanced` draws.
- Whether `RenderMeshInstanced` draws with SRP Batcher on take the `INSTANCING_ON` variant of URP Lit in all cases (inferred yes; verify with `UnityStats.drawCalls` or a RenderDoc capture).
- Whether the D3D11 driver's transient-buffer renaming stalls on a no-Present loop; the mechanism is proposed from driver behaviour in general, not from any Unity or vendor document.
