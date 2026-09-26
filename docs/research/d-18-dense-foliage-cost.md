# d-18 — What dense foliage costs, and how to keep it cheap

> **Correction, 2026-09-24 (design 38 §2, §13).** The "drawn up to six times" finding holds for
> opaque shadow-casting foliage — the trees — and **not for the grass as the project draws it**:
> `ChunkRenderer.FoliageCastsShadows` is off and foliage is drawn in queue 2501, past the opaque
> range, so it is in neither the shadow pass nor the opaque-only DepthNormals prepass. Depth priming
> cannot reach it. Measured afterwards: grass costs about 1 ms at 3840 × 2160 on an RTX 5070 Ti,
> shipped or full cover. The findings below are left as written.

*Researched 2026-09-24 for the meadow overhaul (owner: the ground "completely full of grass"). Caps:
12 searches, 20 reads; 11 searches and 17 reads used, including four reads of this repository.*

## Question

The owner wants every cell of a 120 × 120 to 240 × 240 board of 2.5 m cells covered in grass,
drawn as low-poly Synty clump meshes with alpha-tested card textures through
`Graphics.RenderMeshInstanced`. It must hold **60 fps at 3840 × 2160 on an RTX 5070 Ti (Ultra)**
and **60 fps at 1080p on an RTX 3050/3060 laptop (Low)**. What does dense alpha-tested foliage cost
on the GPU, and what are the standard techniques that keep it cheap in URP 17.3 Forward+?

## Findings

Tags: **(a)** vendor or developer documentation / talk, **(b)** credible practitioner write-up or
measurement, **(c)** forum or tutorial, **(d)** engineering judgement from this project's numbers.

### 0. What the project already is, because it decides half the answers

Read from `Assets/Settings/` on `claude/meadow-overhaul` (2026-09-24):

| Setting | Value | Consequence for grass |
|---|---|---|
| Rendering path | Forward+ (`m_RenderingMode: 2`) | GPU Resident Drawer would be *allowed*; see §6 |
| Depth Priming Mode | **Disabled** (`0`) | the forward pass builds its own depth; a prepass does not save shading |
| MSAA | **off** (`m_MSAA: 1`), SMAA post | alpha-to-coverage has nothing to cover; see §1 |
| SSAO renderer feature | **on** | URP runs a **DepthNormals prepass** over every opaque and alpha-clipped draw |
| Shadows | 250 m, **4 cascades**, soft | a shadow-casting instance is drawn up to four more times |
| GPU Resident Drawer | off (`0`) | irrelevant to `RenderMeshInstanced` anyway (§6) |

And the frame it has to fit into (CLAUDE.md, PF row, §6c.5): at 3840 × 2160 on the 5070 Ti the
**GPU frame is 8.15–9.12 ms** against a vsync-paced ~16 ms frame. So there is roughly **7 ms of GPU
headroom at Ultra** before 60 fps is at risk, and none of it is measured on the laptop.

**So as the settings stand, one grass instance is drawn up to six times a frame:** DepthNormals
prepass, forward colour, and four shadow cascades. That count, not the triangle count, is the
first thing to cut.

### 1. Alpha clip vs alpha-to-coverage vs opaque blades

- **Alpha clip (`discard`/`clip`) with depth writes on** loses some early-Z, not all of it. **(b)**
  MJP measured it: "many GPUs are still able to perform some degree of early depth testing … even
  if it has to defer the depth write", but the late write lowers the cull rate for everything drawn
  after it (582,000 pixel-shader invocations against 440,640 without discard in his test). Alpha
  tested geometry also "tends to mess up z-buffer compression and hierarchical z", so it should be
  drawn **after** the ordinary opaques — which URP already does by putting alpha-clipped materials
  in the AlphaTest queue (2450). **(b, gamedev.net)**
- **The real cost of a card is the transparent part of it.** Every fragment inside the card's
  triangles runs the shader — texture fetch, then `clip` — whether or not it survives. A card
  whose silhouette covers 35 % of its quad pays for three pixels to draw one. Plus **quad
  overshading**: pixels are shaded in 2 × 2 quads, so thin or tiny triangles pay for up to four
  lanes per visible pixel. **(a, Unigine; b, selfshadow "Counting Quads")**
- **Alpha-to-coverage without MSAA is not an option.** With no MSAA samples it "degenerates to a
  simple alpha test" **(c, gamedev.net)**, and bgolus: "compared to alpha testing, alpha to
  coverage is *always* more expensive" **(c, Unity Discussions)**. The project's AA is SMAA post;
  A2C would cost more and look identical. It becomes worth revisiting only if an Ultra tier ever
  turns MSAA on, and URP's own performance guide says to reduce or disable MSAA for bandwidth.
  **(a)**
- **Opaque geometric blades** (no texture, no clip — what the closed-branch code grass did) keep
  full early-Z and hierarchical-Z and have no wasted transparent fragments. The one direct
  comparison found, in UE4: modelled blade clusters were **81 % faster overall than masked cards
  despite 47 % more draw calls**, and ~190 % faster with shadows off, because prepass and base
  pass shrank **(b, 80.lv)**. Its cost moves to vertices and to quad overshading on sub-pixel
  blades — which at our camera is real: at 4K, 160 m across the view is ~24 px a metre, so a 3 cm
  blade is under one pixel wide; at 1080p it is half that.
- **Ranking at 4K, cheapest first (d, from the above):** opaque blades ≈ alpha clip **inside a
  depth-primed pass** (§2) < alpha clip with ZWrite on (today's URP default) < alpha-to-coverage
  without MSAA. The middle two differ by how much of each card is empty; the owner wants the Synty
  card look, so the practical choice is alpha clip, with the card mesh cut close to its silhouette.

### 2. Depth prepass, DepthNormals and ZTest Equal

- **(a)** URP's *Depth Priming Mode* (URP asset → Rendering) renders the prepass into the camera's
  own depth attachment, and the opaque colour pass then tests `Equal` with writes off. *Auto* primes
  only when a prepass already runs — which it does here, for SSAO. Unity's performance guide says
  **Auto or Forced on PC and console, Disabled on mobile**. The project is on Disabled.
- **(b)** The payoff for foliage is exactly the case MJP and Wronski-era practice name: "only
  performing discard in a depth prepass followed by a full opaque pass with EQUAL depth testing …
  ensures the heavier opaque pixel shader doesn't execute for occluded pixels"; the depth pass
  with alpha test is cheap, and one report puts the EQUAL-tested second pass at four times faster
  for vegetation **(c, gamedev.net)**. Interplay of Light: foliage "can't be easily distance
  sorted", which is where a prepass earns most, but "YMMV — profiling will be needed". **(b)**
- **So today the grass is paid for twice and the second payment buys nothing.** With SSAO on and
  priming off, the DepthNormals prepass already runs the alpha test over every grass pixel, and
  then the forward pass runs it again against a fresh depth buffer with full lighting on every
  fragment that passes the depth test, overdraw and all. Turning priming to *Auto* makes the
  forward pass shade each screen pixel once, and **lets the grass's forward pass drop `clip`
  entirely** (ZWrite Off + ZTest Equal keeps early-Z whole).
- **Two traps before flipping it (d, from this repository):**
  1. **Any opaque shader without a `DepthOnly`/`DepthNormals` pass is absent from the primed depth
     and fails `ZTest Equal` — it vanishes.** `OdysseyCharacter.shader` has `DepthOnly` but no
     `DepthNormals`; `OdysseyPowerLine.shader` and `OdysseyWater.shader` (transparent, so safe)
     have neither. The characters and the power line are the ones to check.
  2. **Every vertex displacement must be bit-identical in the depth passes and the forward pass**
     — wind, the per-instance shrink of §4, any bend — or `Equal` fails in speckles. Put the
     displacement in one include used by all three passes.

### 3. Shadow casting

- **(b)** Disabling dynamic shadows on "tiny foliage like grass" is ordinary practice; contact or
  screen-space shadows are the usual substitute, and in the UE4 comparison shadows were the
  single largest line. With **four cascades out to 250 m** a casting grass instance is submitted
  up to four extra times, each an alpha-tested depth draw with the same transparent-fragment waste.
- **Usual choice: cast off, receive on.** The ground darkening under a clump comes from SSAO
  (already paid for) and from the grass receiving the trees' and buildings' shadows. Receiving
  costs a shadow-map lookup per shaded fragment — with soft shadows several taps — which is one more
  reason for priming (each pixel receives once, not once per overdraw layer). On Low, receive with
  hard shadows or not at all.

### 4. Density falloff, draw distance and clump size

- **Rank thinning.** Give every instance in a chunk a fixed random rank and store the chunk's
  matrices **sorted by rank**; thinning to a density *d* is then "submit the first *d* × *n*" — no
  per-frame per-instance work, no reallocation, and deterministic from the seed. Ghost of Tsushima
  drops "3 out of every 4 blades" approaching its far tiles, whose tiles are "twice as big but have
  the same number of blades" **(b, secondary account of GDC 2021)**. BotW's density is one global
  float **(b, `b-botw-grass.md` §5)**.
- **Fade by shrinking, not by dithering.** A dither fade adds discarded fragments (more cost, not
  less) and, with SMAA rather than TAA, the dither pattern stays visible as static. Scaling an
  instance's height to zero across its last few metres of rank costs nothing and reads naturally
  for grass. Use dither only where a shrink looks wrong. **(d)**
- **Draw distance is nearly free here.** At 48° over a ~160 m view the camera sees on the order of
  3,000–4,000 cells at once **(d)**; the far edge sits at the 20–30 % fog band (`d-14` §fog), which
  hides a density step. Aerial perspective is also how BotW hides its transitions.
- **Fewer bigger clumps vs many small.** Overdraw is set by **the total projected card area per
  screen pixel**, not by the instance count: halving the count and doubling each clump's area
  leaves overdraw the same while halving vertices and per-instance work. What bigger clumps must
  not do is carry more **empty** area — the waste is the transparent fraction (§1). Small clumps
  also get the quad-overshading penalty sooner at 1080p. **(d, from §1's sources)**
- **Counts (d).** 120² = 14,400 cells, 240² = 57,600. At 4–8 clumps a cell the board holds 58 k to
  460 k instances; **visible**, about 12–30 k. `RenderMeshInstanced` is capped per call by the
  constant buffer (~1,023 matrices) **(c)**, so that is 15–30 calls — trivial.

### 5. GPU-driven submission (RenderMeshIndirect + compute culling)

- **(c)** The standard examples (Colin Leung's URP grass, ellioman, MangoButtermilch) use
  `DrawMeshInstancedIndirect`/`RenderMeshIndirect` with a compute pass for per-instance frustum
  (and sometimes Hi-Z occlusion) culling. Colin Leung's demo draws **10 million instances at
  50–60 fps on an Adreno 612** — with "simple CPU cell frustum culling (not even a quadtree) →
  minimum compute GPU frustum culling", and "performance mainly affected by **visible** grass count
  on screen".
- **When it pays (d):** when the *visible* instance count is in the hundreds of thousands, when CPU
  submission shows up in the frame, or when density must vary per instance every frame. At 12–30 k
  visible instances in chunks that already exist, **CPU per-chunk culling (25 × 25-cell chunks, the
  `ChunkRenderer` grid) plus rank truncation does the same job with none of the compute, readback
  or indirect-args plumbing**. The per-chunk test is also the frustum test `ChunkRenderer.Render`
  still lacks (`claude/frustum-culling`) — one AABB test serves both.
- Revisit if the visible count passes ~100 k, or if the `Surround`/`World`-style submit line for
  grass exceeds ~0.3 ms at 4K.

### 6. Does the GPU Resident Drawer apply to `RenderMeshInstanced`?

**No — verified.** **(a)** Unity's URP manual: the GPU Resident Drawer "only works with GameObjects
that have a Mesh Renderer component"; it "automatically uses the BatchRendererGroup API to draw
GameObjects with GPU instancing". `Graphics.RenderMeshInstanced`/`RenderMeshIndirect` are separate
submission paths the drawer never sees. **(c)** One Unity Discussions report for grass: 153 fps with
`RenderMeshInstanced` against 43 fps through the drawer, attributed to the drawer culling every
instance per camera **and per shadow cascade**. The drawer is off in the project and should stay
off for this.

### 7. Numbers from shipped games and samples

| Source | Count | Cost | Notes |
|---|---|---|---|
| Ghost of Tsushima (GDC 2021, Wohllaib) | ~83,000 blades on screen | ~2.5 ms/frame (PS4) | 15-vertex near / 7-vertex far Bézier blades, GPU-generated per tile; far tiles twice the size, same blade count; ¾ dropped approaching the far LOD **(b)** |
| Breath of the Wild | unpublished | unpublished | one-triangle blades, one global density float, frustum-derived bulk culling, no grass billboard stage **(b, `b-botw-grass.md`)** |
| Colin Leung URP mobile grass | 10 M instances (visible far fewer) | 50–60 fps on Adreno 612, draw distance 125 | CPU cells → compute frustum cull → one indirect draw **(c)** |
| UE4 masked cards vs modelled blades | — | modelled 81 % faster overall, ~190 % with shadows off | 1–8 triangles a blade **(b)** |

**Recommended budget (d).** Treat grass as a **2.0 ms GPU line at 4K Ultra on the 5070 Ti**,
all passes included (prepass + forward, shadows off). That spends under a third of the ~7 ms
headroom, and is GoT's 2.5 ms on far weaker hardware. For **1080p Low on a 3050 laptop, ~1.5 ms**:
the laptop has roughly a fifth of the 5070 Ti's throughput and a quarter of the pixels, so the same
content costs ~1.3–1.5× the 4K figure — which is why Low must roughly **halve density and drop
shadow receiving to hard or none**, not just shorten the draw distance.

### 8. Measuring GPU time per pass in Unity

- **`FrameTimingManager.gpuFrameTime`** — the whole-frame GPU number, already on the developer
  overlay (§6c.3). Low overhead; the headline for an **A/B toggle inside one run** (grass off /
  on / priming Auto), which is this project's control-arm habit and the only comparison the
  machine supports.
- **`ProfilingSampler` + `ProfilerRecorder` with `ProfilerRecorderOptions.GpuRecorder`** (marker
  flag `SampleGpu`) or `Recorder.gpuElapsedNanoseconds` — per-marker GPU time, three frames late,
  gated on `SystemInfo.supportsGpuRecorder`. **(a)** But grass submitted with
  `RenderMeshInstanced` from `Update` is folded into URP's own *DepthNormals* and *DrawOpaqueObjects*
  passes, so there is no marker that isolates it. To get one, draw it from a small renderer feature
  (`AddRasterRenderPass`, as `OutlineFeature` does) wrapped in a sampler — that also gives explicit
  control over which passes grass enters.
- **Frame Debugger** — which passes each grass draw lands in, instance counts, keywords. No timings.
- **RenderDoc / NVIDIA Nsight Graphics (GPU Trace)** — per-draw GPU time, overdraw and quad
  overdraw views; Nsight is the one to answer "is it fragment-bound, and how much of it is
  discarded". Use on the 5070 Ti and, once it exists, on the target laptop.

## Recommendation

Ranked, committed:

1. **Shadows: grass casts none, receives them (hard on Low).** Removes up to four of six draws per
   instance for a loss SSAO mostly covers. Cheapest and largest win; do it from the first commit.
2. **Alpha mode: alpha clip (`clip`) in the depth passes, and the forward pass depth-primed —
   Depth Priming Mode → *Auto*, grass forward pass `ZTest Equal`, `ZWrite Off`, no `clip`.** Not
   alpha-to-coverage (no MSAA, always dearer). Keep opaque geometric blades as the named fallback
   if priming cannot be made safe. **The tie-breaker and its experiment:** flip *Auto* with a full
   grass field on and read `gpuFrameTime` in one run, three arms (grass off / grass + priming off /
   grass + priming Auto); first give `OdysseyCharacter` a `DepthNormals` pass and check the power
   line, or they vanish.
3. **Prepass: grass must be in the DepthNormals prepass** (it already runs for SSAO, and the
   outline and fog read depth); its depth passes share the forward pass's displacement include
   exactly.
4. **Clump size: medium clumps, mesh cut tight to the silhouette** — aim for a card whose opaque
   fraction is at least half its area; ~6 a cell at Ultra, ~3 at Low, with rank-sorted per-chunk
   matrices so thinning is a count, and a **shrink** fade at the thinning boundary rather than a
   dither.
5. **Culling tier: CPU per-chunk frustum culling on the existing 25 × 25 chunk grid,
   `RenderMeshInstanced` in ≤1,023 batches.** Not GPU-driven, not the GPU Resident Drawer (it does
   not apply). Escalate to `RenderMeshIndirect` + compute culling only past ~100 k visible
   instances or ~0.3 ms of grass submission.

Budget: **2.0 ms GPU at 4K Ultra, 1.5 ms at 1080p Low**, measured as a toggle arm, never inferred.

## Sources

- Unity Manual, Enable the GPU Resident Drawer in URP — https://docs.unity3d.com/6000.0/Documentation/Manual/urp/gpu-resident-drawer.html
- Unity Discussions, RenderMeshInstanced vs GPU Resident Drawer — https://discussions.unity.com/t/rendermeshinstanced-vs-gpu-resident-drawer-unity/1693669
- Unity Manual, Configure for better performance in URP — https://docs.unity3d.com/6000.1/Documentation/Manual/urp/configure-for-better-performance.html
- Unity Discussions, Depth Prepass/Priming with MSAA — https://discussions.unity.com/t/depth-prepass-priming-with-msaa/884039
- Unity Manual, Write a depth-only pass in URP — https://docs.unity3d.com/6000.3/Documentation/Manual/urp/writing-shaders-urp-depth-only.html
- MJP, To Early-Z, or Not To Early-Z — https://therealmjp.github.io/posts/to-earlyz-or-not-to-earlyz/
- Interplay of Light, To z-prepass or not to z-prepass — https://interplayoflight.wordpress.com/2020/12/21/to-z-prepass-or-not-to-z-prepass/
- gamedev.net, Performance of drawing vegetation; overdraw, alpha testing — https://gamedev.net/forums/topic/668085-performance-of-drawing-vegetation-overdraw-alpha-testing-alpha-cutout-model-generation/5226890/
- gamedev.net, Alpha-Test and Forward+ Rendering (Z-prepass) — https://www.gamedev.net/forums/topic/659682-alpha-test-and-forward-rendering-z-prepass-questions/
- gamedev.net, Alpha to Coverage w/o MSAA — https://gamedev.net/forums/topic/671912-alpha-to-coverage-wo-msaa/5253189/
- Unity Discussions, Alpha To Coverage Performance — https://discussions.unity.com/t/alpha-to-coverage-performance/862767
- 80.lv, Creating Next-Gen Grass in UE4 — https://80.lv/articles/creating-next-gen-grass-in-ue4
- Unigine, Analyzing Quad Overdraw — https://developer.unigine.com/en/docs/2.21/content/optimization/geometry/quad_overdraw/
- Self Shadow, Counting Quads — https://blog.selfshadow.com/2012/11/12/counting-quads/
- GDC Vault, Procedural Grass in Ghost of Tsushima — https://gdcvault.com/play/1027033/Advanced-Graphics-Summit-Procedural-Grass
- Tiger Abrodi, What we can learn from grass in Ghost of Tsushima — https://tigerabrodi.blog/what-we-can-learn-from-grass-in-ghost-of-tsushima-renders
- Colin Leung, UnityURP-MobileDrawMeshInstancedIndirectExample — https://github.com/ColinLeung-NiloCat/UnityURP-MobileDrawMeshInstancedIndirectExample
- ellioman, Indirect Rendering With Compute Shaders — https://github.com/ellioman/Indirect-Rendering-With-Compute-Shaders
- Unity blog, Detecting performance bottlenecks with the Frame Timing Manager — https://unity.com/blog/engine-platform/detecting-performance-bottlenecks-with-unity-frame-timing-manager
- Unity Scripting API, ProfilerRecorderOptions.GpuRecorder — https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorderOptions.GpuRecorder.html
- Unity Scripting API, Recorder.gpuElapsedNanoseconds — https://docs.unity3d.com/2020.1/Documentation/ScriptReference/Profiling.Recorder-gpuElapsedNanoseconds.html
- SRP Core, ProfilingSampler — https://docs.unity3d.com/Packages/com.unity.render-pipelines.core@17.0/api/UnityEngine.Rendering.ProfilingSampler.html
- Project: `docs/research/b-botw-grass.md` on `claude/illustrated-look`; `Assets/Settings/PC_RPAsset.asset`, `PC_Renderer.asset` and `Assets/Odyssey/Presentation/Shaders/*.shader` on `claude/meadow-overhaul`.

## Confidence

- **High:** GPU Resident Drawer does not apply to `RenderMeshInstanced`; alpha-to-coverage without
  MSAA is pointless; discard with depth writes weakens early-Z; the project is drawing grass in a
  DepthNormals prepass and a non-primed forward pass today; shadow casting is the largest avoidable
  multiplier.
- **Medium:** that depth priming *Auto* is a net win here (the literature says usually, and says
  measure); the Ghost of Tsushima figures (secondary account of the talk, not the slides).
- **Low:** the 2.0 / 1.5 ms budget and the laptop scaling factor (engineering judgement, no
  laptop measured); visible-instance estimates (from the camera geometry, not counted in a frame).

## Could not be determined

- **Any published millisecond figure for alpha-tested grass in URP** — Unity publishes none, as
  `d-12` found for post effects.
- The triangle count and **opaque fraction of the specific Synty clump cards** — measure them from
  the meshes before choosing the per-cell count.
- Whether URP 17.3's priming path handles a `RenderMeshInstanced` draw from `Update` identically to
  a renderer (it should, as both go through the same opaque filtering; unverified).
- Whether the Unity Profiler's GPU module is supported on the owner's DX12/Vulkan configuration in
  6000.3 — check `SystemInfo.supportsGpuRecorder` on the machine.
- The primary Ghost of Tsushima slides (the GDC Vault video was not readable within the cap).
