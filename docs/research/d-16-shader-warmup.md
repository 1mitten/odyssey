# d-16 — Shader and PSO warm-up behind a loading screen

*Research lane, 2026-09-24. Cap: 12 searches, 20 reads; used 8 searches and 20 reads (four of them
project files).*

## Question

In Unity 6.3 LTS with URP 17.3, on Windows, what is the reliable way to remove first-use shader and
pipeline-state (PSO) compilation hitches during play, for a game that draws almost everything with
`Graphics.RenderMeshInstanced` using hand-written HLSL URP shaders and a few Shader Graph shaders,
behind a loading screen that may take as long as it needs? Covering `GraphicsStateCollection`,
`ShaderVariantCollection.WarmUp`, the editor's asynchronous compilation, stripping, a practical
recipe, how to verify it, and the texture and mesh uploads a loading screen should also pre-touch.

## Findings

### 0. A project fact that changes the question: the Windows player runs Direct3D 11 first

`ProjectSettings/ProjectSettings.asset` lists the Windows standalone graphics APIs as
`m_APIs: 0200000012000000`, `m_Automatic: 0` — that is `GraphicsDeviceType` **2 (Direct3D11) then
18 (Direct3D12)**, no Vulkan. So `Build/Win64/Odyssey.exe` starts on **DX11** unless DX11 is
unavailable. The brief of this lane said "DX12/Vulkan"; the build says otherwise. This matters
because every warm-up API behaves differently on DX11 (below): on DX11 there is no application-
visible PSO, and Unity documents the old warm-up path as fully supported there.

### 1. `GraphicsStateCollection` (Unity 6 PSO tracing and warm-up)

- **What it is.** "Collection of shader variants and associated graphics states"
  (`UnityEngine.Experimental.Rendering`). Unity's manual: "Unity and the graphics driver need to
  compile the shader variant, and create a pipeline state object (PSO) with the compiled shader code
  and its related GPU state." Modern APIs compile shaders as part of PSO creation, so the collection
  records the variant **and** the graphics states it was drawn with.
- **Status.** Still marked **experimental** in the 6000.3 manual and scripting reference: "might be
  changed or removed in the future." It shipped in 6000.0; the automatic legacy fallback arrived in
  6000.1.
- **APIs.** Full PSO warm-up on **DX12, Vulkan and Metal**. On **DX11, OpenGL, GLES and WebGL** it
  "automatically fall[s] back to shader warm up": `WarmUp` falls back to
  `ShaderVariantCollection.WarmUp`, and `WarmUpProgressively` does too when
  `SystemInfo.supportsParallelPSOCreation` is false, with `count` then meaning shader variants rather
  than state permutations. Compute and ray-tracing shaders cannot be traced.
- **Recording.** `new GraphicsStateCollection()`, `BeginTrace()` at application/scene start,
  `EndTrace()` at the end, then `SaveToFile(path)` (a `.graphicsstate` file) or `SendToEditor()`
  over PlayerConnection (needs Development Build and Autoconnect Profiler). **Tracing works only in a
  development player, not a release build and not in editor Play mode** — Unity staff: "players will
  encounter additional PSOs that are not rendered in the Editor." Trace **one collection per graphics
  API and platform** ("GPU states can vary per API"); the collection carries `graphicsDeviceType`,
  `runtimePlatform` and `qualityLevelName` so the right one can be picked at run time. Collections
  are **not** per GPU vendor. A collection can be topped up by tracing again into the same object
  around the part of the game that produced the misses, and the API has `AddVariant`,
  `AddGraphicsStateForVariant`, `CopyGraphicsStatesForVariant` and friends for hand-editing.
- **Shipping.** The scripting example holds it as a serialised field
  (`public GraphicsStateCollection graphicsStateCollection;`), i.e. a `.graphicsstate` file in
  `Assets/` imports as an asset and ships by reference; `LoadFromFile(path)` is the alternative (for
  instance from `StreamingAssets`). Unity's URP 3D Sample ships three scripts to copy the shape of:
  `GraphicsStateCollectionManager` (trace or warm, holds a list), `…Stripper` (a build step that
  keeps only the collections matching the target platform, API and quality level) and `…Combiner`
  (merges matching traces).
- **Warming.** `WarmUp()` schedules every PSO; `WarmUpProgressively(count)` schedules `count` per
  call. Both return a `JobHandle`, so the loading screen can call `WarmUpProgressively` each frame
  and show `completedWarmupCount / totalGraphicsStateCount` until `isWarmedUp`. Manual: "The
  recommended best practice is to prewarm your collection during application or scene loading
  sequences." Created PSOs are then cached to disk by the driver, so the second launch is cheaper.
- **Does it see `RenderMeshInstanced`?** Nothing found states it either way. Tracing is described as
  recording states when the player "render[s] new shader variants", i.e. at PSO creation below the
  draw API, which would include instanced draws and runtime-built materials (the trace stores the
  variant's keywords and states, not the `Material`). **Inferred, medium confidence** — the
  verification step below settles it for this project in one run.
- **Keyword and render-state coverage.** A trace holds exactly what was drawn during the trace, with
  the keywords URP had set *at that moment* and the render-target formats, MSAA, blend/depth and
  vertex layout of that session. Anything that changes a keyword (shadows on/off, cascades,
  additional-lights mode, SSAO, fog) or a target format (MSAA count, HDR, render scale does not but
  AA mode does) makes new PSOs. Our Graphics tab exposes anti-aliasing and shadow distance, so a
  player changing those can meet PSOs the trace never saw. Collections go stale when shaders change:
  "You need to capture again every time anything changes" (forum user; consistent with the design).

### 2. `ShaderVariantCollection.WarmUp` and `Shader.WarmupAllShaders`

- Both warm **variants** — load and hand the compiled program to the driver — not PSOs. Manual
  (6000.3/6000.4): both work on "DirectX 11, OpenGL, OpenGLES, and WebGL" and "aren't recommended on
  Vulkan, DirectX 12, and Metal because they can create the wrong PSOs due to missing graphics
  states". `WarmupAllShaders`: on DX12/Vulkan/Metal "the graphics driver might still need to perform
  work if the vertex layout and/or the render target setup is different from the data used to
  prewarm it", which "can … still leave visible stalls".
- **Neither is deprecated** in 6000.3. `WarmupAllShaders` warns of "long load times and high memory
  usage" because it warms every variant of every loaded shader.
- `ShaderVariantCollection.WarmUp` can also run automatically from Graphics Settings → Preloaded
  Shaders.
- On **DX11** — this project's first API — these are the documented, fully supported path, and they
  are exactly what `GraphicsStateCollection` falls back to there.

### 3. The editor's asynchronous shader compilation

- `EditorSettings` has `m_AsyncShaderCompilation: 1` in this project. In the editor, the first time a
  variant is met it is queued on a job thread and the geometry is drawn with a **cyan placeholder**
  until it is ready; `DrawProcedural` geometry is **skipped** instead of drawn cyan; `Blit` is never
  async. `ShaderUtil.allowAsyncCompilation` turns it off for an immediate scope, and
  `CommandBuffer.SetAsyncCompilation` for a buffer.
- This is **editor-only**. A player never compiles HLSL at run time: variants are compiled at build
  time, so the player's first-use cost is loading the variant plus driver compilation (DX11) or PSO
  creation (DX12) — tens of milliseconds at worst, a hitch, not seconds of a missing wall.
- The editor keeps compiled variants in its shader cache, so the delay happens once per variant per
  cache and then disappears — which is why it looks intermittent.
- **Why a batch test cannot reproduce it.** The project measured `ShaderUtil.allowAsyncCompilation`
  false in batch mode (`06-rendering-and-camera.md` §6c.8), so batch runs compile synchronously and a
  PlayMode test sees no placeholder frames. It also runs from a warm shader cache after the first
  run. The owner's "one to three seconds before a wall appears" is therefore most plausibly the
  editor, and **says nothing about the player**. Whether an *instanced* draw is drawn cyan or skipped
  while pending (the report says the wall was absent, not cyan) was not found.

### 4. Stripping interactions

- Warm-up can only warm variants that are **in the build**. A traced variant that stripping removed
  is simply not there to warm (and was not there to draw either — the project's empty-player fault).
  So `ShaderInclusion`, `InstancingKeepAlive` and `SyntyInstancingKeepAlive` stay necessary; a
  collection does not replace them.
- Whether Unity *keeps* variants because a `GraphicsStateCollection` asset names them was not found.
  A third-party tool (`wotakuro/StrippingByVariantCollection`) uses SVCs and GSCs as a whitelist for
  its own stripping, which suggests Unity does not do this by itself. Treat the collection as a
  warm-up list, not an inclusion list.
- The keep-alive material route stays the right one for inclusion, for the reason its own header
  gives: the keyword set a runtime material ends up with is URP's business. The trace is the one
  place that set is actually *observed* rather than guessed, which is why it is the right source
  for the warm-up list.

### 5. Texture and mesh uploads

- Assets loaded **asynchronously** go through the Async Upload Pipeline, time-sliced on the render
  thread (`QualitySettings.asyncUploadTimeSlice`, `asyncUploadBufferSize`). Synchronous loads upload
  header and data in one frame.
- For this project the likely first-use costs besides shaders are: runtime-built meshes (chunk
  meshes, the surround) uploaded on first draw; runtime textures uploaded at `Texture2D.Apply`;
  URP render targets allocated on first use (shadow atlas, colour/depth, post-process targets) and
  reallocated when render scale, MSAA or resolution change; and the `PortraitStudio` render, which
  has its own camera, targets and variants.
- The general answer is the same as for PSOs: **draw it once behind the curtain**. A frame or two of
  the real camera rendering the real board under an opaque loading overlay uploads every mesh and
  texture in view and allocates every URP target at the real resolution and settings.

## Recommendation

**Measure first, then warm with a traced collection, backed by a curtain frame.** Ranked:

1. **Precondition — find out whether the player hitches at all (cheapest experiment, one run).**
   Build a *development* player, switch on Graphics Settings → **Log Shader Compilation**
   (`GraphicsSettings.logWhenShaderIsCompiled`, logs each variant uploaded to the driver), play ten
   minutes through building walls, floors, night, zones, the Work tab and a settings change, with
   the PT perf trace running. Count log lines that land **after** the loading screen and match them
   against frames the trace captured over 50 ms. Also look for `Shader.CreateGPUProgram` and
   `CreateGraphicsGraphicsPipelineImpl` in the Profiler. If nothing lines up, the seconds-long wall
   delay was the editor (§3) and the only change owed is noting it; do not build warm-up machinery
   for a hitch that does not exist.

2. **If there are hitches: `GraphicsStateCollection`, traced once per API and committed.**
   - A small `ShaderWarmup` component, off by default, with two modes: **Trace** (development builds
     only; `BeginTrace` on boot, `EndTrace` + `SaveToFile` on quit) and **Warm**.
   - Trace a scripted session on **DX11 and on DX12** separately (`-force-d3d12`), because the
     build lists both. Copy the two `.graphicsstate` files into `Assets/Odyssey/Rendering/Warmup/`
     and **commit them** — they are our shader names and states, not licensed content; but traces
     that include Synty Shader Graph variants must be checked for anything that names pack assets
     before committing (the variants belong to shaders under the gitignored folder, so a clone
     without the packs will carry dangling entries; warm-up of a missing shader must be tolerated,
     and the tests must not depend on it).
   - Warm in the loading screen: pick the collection whose `graphicsDeviceType` matches
     `SystemInfo.graphicsDeviceType`, call `WarmUpProgressively(n)` each frame, drive the progress
     bar from `completedWarmupCount / totalGraphicsStateCount`, hand over when `isWarmedUp`. On DX11
     this is `ShaderVariantCollection.WarmUp` underneath and is the documented supported path.
   - Re-trace whenever a shader, a URP asset setting or a Graphics-tab default changes; make the
     player log a miss count (variants compiled after hand-over, from the step-1 logging) so a stale
     collection shows itself.

3. **Always: one curtain frame.** Before the loading overlay lifts, render two or three frames of
   the real camera on the real board at the real resolution. It costs nothing, covers the exact
   PSOs for the opening view whatever the trace missed, and does the mesh, texture and render-target
   uploads of §5. The particle system already does a small version of this (§6c, "warmed on
   construction").

**Not recommended:** `Shader.WarmupAllShaders` (warms everything loaded, including every Synty
variant, at a memory cost, and still wrong on DX12); a hand-written `ShaderVariantCollection`
(guesses the keyword set, which is the mistake `InstancingKeepAlive` documents); turning off
stripping.

**Tie between 2 and 3 alone?** If step 1 finds hitches only in the opening seconds, the curtain
frame alone may be enough; if it finds them later (the first wall, the first night, the first zone)
only the trace covers them. Step 1's log already says which.

## Sources

- https://docs.unity3d.com/6000.3/Documentation/Manual/shader-prewarm.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/shader-pso-trace.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/shader-pso-introduction.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/shader-pso-example.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/shader-pso-trace-warming.html
- https://docs.unity3d.com/6000.4/Documentation/Manual/shader-prewarm-other.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Experimental.Rendering.GraphicsStateCollection.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Experimental.Rendering.GraphicsStateCollection.WarmUpProgressively.html
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Shader.WarmupAllShaders.html
- https://docs.unity3d.com/6000.3/Documentation/Manual/AsynchronousShaderCompilation-introduction.html
- https://docs.unity3d.com/6000.0/Documentation/Manual/AsynchronousShaderCompilation-enable-or-disable.html
- https://discussions.unity.com/t/graphicsstatecollection-tracing-and-warmup-in-unity-6/951031
- https://discussions.unity.com/t/graphicsstatecollection-seems-to-not-working-shaders-recompiling-at-runtime-again/1543401
- https://github.com/wotakuro/StrippingByVariantCollection
- https://docs.unity3d.com/6000.3/Documentation/Manual/configure-asynchronous-upload-pipeline.html
- https://unity.com/blog/engine-platform/understanding-the-async-upload-pipeline
- Project: `ProjectSettings/ProjectSettings.asset` (`m_BuildTargetGraphicsAPIs`),
  `ProjectSettings/EditorSettings.asset`, `docs/design/06-rendering-and-camera.md` §6c.3 and §6c.8,
  `Assets/Editor/Odyssey/InstancingKeepAlive.cs`.

## Confidence

- **High:** the API surface, dev-build-only tracing, per-API collections, DX11/GL fallback to
  `ShaderVariantCollection.WarmUp`, SVC/`WarmupAllShaders` being wrong for DX12/Vulkan/Metal, async
  compilation being editor-only with a cyan placeholder, and the Windows player listing DX11 first.
- **Medium:** that traces capture `RenderMeshInstanced` draws and runtime-built materials (inferred
  from where tracing hooks in); that the owner's wall delay is the editor's compiler; the curtain
  frame covering uploads.
- **Low:** anything about how a collection interacts with stripping.

## Could not be determined

- An explicit statement that tracing records `Graphics.RenderMeshInstanced` / `RenderParams` draws
  (step 1 settles it empirically).
- Whether a `GraphicsStateCollection` asset in the build prevents its variants being stripped.
- Whether an instanced draw is drawn cyan or skipped while its variant compiles asynchronously in
  the editor.
- The content and fix version of Unity issue "Warnings when warming up shaders with
  GraphicsStateCollection" (the tracker page would not render).
- Whether the traced file is portable across driver versions or needs re-tracing per Unity patch
  release beyond "re-trace when anything changes".
- Whether `GraphicsSettings.logWhenShaderIsCompiled` is still the exact setting name in 6000.3 (it
  was not re-checked within the cap; the Profiler markers are the documented fallback).
