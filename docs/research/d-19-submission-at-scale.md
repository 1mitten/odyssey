# d-19 — Where the look pass's frame goes, and how to make submission cheap at scale

> **Built and measured, 2026-09-24 (design 38 §18).** Recommendation 1 (the sun-ward margin): 104 →
> 42 chunks, submit 2.92 → 1.42 ms at 4K, proof 0.00% at noon and at dusk. Recommendation 2's
> tie-breaker: picture-exact but unmeasurable, because the census's "722 tuft buckets" counted every
> foliage-tinted bucket — the tufts proper are 67 of 1,175 calls after the sweep; the dressing's
> grass and flowers are where the calls are. Recommendation 4 (BatchRendererGroup) is recorded as
> the long-term route, not built (owner).

**Lane:** D (Unity), Meadow overhaul, the look pass (`docs/design/38-meadow-overhaul.md` §17).
**Date:** 2026-09-24. **Branch:** `claude/meadow-perf-research` (measurement arm only; nothing in the
renderer changed).

## Question

The owner, after the look pass: *"We need to make this as performant as possible … the compute is up
to 5 ms"* — Standard board, 3840 × 2160, default preset; the overlay figure was ~1.4–1.5 ms before
the pass. Accepted trade-offs: **baked chunk meshes** and **GPU-driven drawing**; not distance-fading
the dressing, not earlier flat far trees. Where does the time go, is it CPU submission of the extra
draw calls, and which technique buys the most for this renderer (`ChunkRenderer` submitting
`Graphics.RenderMeshInstanced` per chunk × mesh × material, non-readable Synty meshes, per-instance
colour arrays, the sight fade, per-chunk frustum culling, URP 17 Forward+, Unity 6000.3, a DX11
player first)?

## Findings

### 1. The measurement (`FrameTimeTests.TheLookAgainstTheSubmission`, Explicit)

One played meadow (Standard, seed 1, culling on), RTX 5070 Ti, Direct3D 11, taken twice. The arms:
the look as shipped; the same with `SubmitToGpu` off (the renderer does all its own bookkeeping and
hands Unity nothing, so the difference is what Unity spends drawing the submissions); dressing and
tufts off (`ScatterDensity` 0, the nearest in-run stand-in for the board before the pass); that with
nothing handed to Unity; the look with no shadow casters; and two arms at the High preset's 120 m
shadow distance on a runtime copy of the pipeline asset. Second run, with the shadow margin at the
pipeline's 250 m (104 chunks drawn):

| 3840 × 2160 | frame | submit | World | draw calls | instances |
|---|---|---|---|---|---|
| look | **11.40 ms** | 2.52 | 2.198 | 2,852 | 55,978 |
| look, nothing handed to Unity | 1.98 | 0.85 | 0.716 | (2,852) | |
| no dressing or tufts | 8.14 | 1.67 | 1.333 | 1,886 | 41,133 |
| no dressing or tufts, nothing handed | 1.77 | 0.63 | 0.460 | | |
| look, no shadow casters (26 chunks) | 7.13 | 0.87 | 0.499 | 770 | 29,069 |
| look, 120 m shadows | 11.52 | 2.68 | 2.268 | 2,852 | |

| 640 × 480 (CPU-bound) | frame | submit |
|---|---|---|
| look | 3.44 | 2.50 |
| look, nothing handed to Unity | 1.24 | 0.90 |
| no dressing or tufts | 2.45 | 1.93 |
| look, no shadow casters | 1.50 | 0.88 |

The first run, with the default 40 m shadow margin (76 chunks at 4K), read the same way: look 12.56 /
no dressing 8.86 / nothing handed 1.86 / no casters 7.28 ms at 4K; submit 2.18 against 1.31.

Census of the meshed buckets (a bucket is one draw per part): terrain 621 buckets (25 instances
each), **tufts and dressing 722 (10.6 a chunk, 19.5 instances each)**, **trees 461 (5.8 a chunk, 4.5
instances each)**, water 31, other 107.

**What it says.**

- **The hypothesis is only a third right.** The look adds **3.3–3.7 ms at 4K**, but only **0.85 ms**
  of that is our submission (`submit` 1.67 → 2.52). The rest is the GPU: the frame is GPU-bound at 4K
  (everything handed to Unity is ~9.4 ms of a 11.4 ms frame, against 1.98 with nothing handed), and
  the dressing's alpha-tested cards cost ~2.4 ms of fill.
- **Per draw call, Unity's side costs ~0.77 µs of CPU and ours ~0.3 µs** (640 × 480: 2,852 calls,
  3.44 against 1.24 ms). At ~2,900 calls that is **~2.2 ms of Unity CPU plus ~0.9 ms of ours** —
  real, but a CPU cost the 4K frame hides behind the GPU, and exactly the figure the owner's editor
  overlay reads as "submit" when the editor's own overhead is added.
- **Shadows are the largest single term: ~4.3 ms of the 4K frame** (look 11.40 against 7.13 with no
  casters) and 1.65 ms of submit. Half of it is the casters' own pass; half is the *margin*: the cull
  keeps every chunk within the shadow distance in **every** direction (26 chunks → 104), so a
  shadowed frame submits four times the board it shows.
- **The shadow distance setting barely matters** here (250 m against 120 m: 11.40 against 11.52) —
  both margins already take the whole Standard board.
- **Tree buckets are thin**: 4.5 instances a bucket, because each chunk splits its trees by variant
  and part. Dressing buckets hold ~20.

### 2. The options

**(a) BatchRendererGroup (BRG).** Unity's API for exactly this kind of renderer: instance data lives
persistently in a GPU buffer, the renderer supplies a culling callback that emits draw commands per
frame (typically from a Burst job), and Unity builds SRP-Batcher draws with no per-call C# overhead.
Requirements: the SRP Batcher; shaders with a `DOTS_INSTANCING_ON` variant — hand-written URP shaders
need `#pragma multi_compile _ DOTS_INSTANCING_ON` and DOTS-instanced property declarations; Shader
Graph shaders get the variant generated; Project Settings → Graphics → **BatchRendererGroup variants:
Keep all**, and URP's **Strip Unused Variants off** (the setting whose accidental flip once took one
pass from 64 variants to 884,736 — `CLAUDE.md`); unsafe code; **no built-in frustum culling**, which
we already do per chunk. Saves most of the ~3 ms of per-call CPU at full margin; GPU unchanged;
per-instance colours become DOTS-instanced properties (a natural fit for tree colour variety);
the sight fade needs a per-instance fade value rather than splitting buckets. **Work: large** — every
world shader, the bucket model, the fade, levels of detail, and a variant-stripping measurement.
DX11: supported (the SRP Batcher runs on DX11), but the variant cost in a DX11 build is the unknown.

**(b) `Graphics.RenderMeshIndirect` + compute culling.** Instances for a kind (mesh × material ×
level) in one `GraphicsBuffer` for the whole board or a region; a compute pass culls them against the
frustum (and, for trees, picks the level) and writes the indirect arguments; **one draw per kind**
instead of one per chunk. Shaders read per-instance data with `UNITY_INDIRECT_DRAW_ARGS` /
`GetIndirectInstanceID()` — our own shaders (`Odyssey/Foliage`, `Odyssey/Tree`) can, the Synty Shader
Graph ones cannot without a custom function node. Requires compute (DX11 has it). For the 722
dressing and tuft buckets that is **~20–40 draws**, and for trees ~16 per level. Saves ~1.5–2 ms of
CPU at full margin; GPU adds a small compute pass; the sight fade and colour variety go per instance
in the buffer. **Work: medium**, and contained to what our own shaders draw.

**(c) Baked chunk meshes.** Combining a chunk's static dressing per material into one mesh. The Synty
meshes are imported with Read/Write off, so `Mesh.CombineMeshes` returns an empty mesh (already
guarded in `ModuleLibrary.Mergeable`), and the GPU route is closed too: **setting
`vertexBufferTarget |= Raw` on a non-readable mesh breaks its rendering**. The legal route is an
`AssetPostprocessor` in `Assets/Editor` (our code, committed) that turns Read/Write on for the models
the catalogue names — the licensed files never leave `Assets/Synty`, but every machine re-imports,
and the CPU copy roughly doubles those meshes' memory (a few MB). Then each chunk's dressing becomes
one mesh per material: 722 buckets → ~70 draws. Costs: vertex memory (every clump duplicated into its
chunk's mesh — tens of MB on Standard, ~4× on Huge), a combine on every dig or build inside the
meshing budget, and no per-instance culling, fade or level inside a chunk. **Work: medium**; the
fade and levels of detail get harder, not easier.

**(d) GPU Resident Drawer.** Works only for GameObjects with a `MeshRenderer`; nothing we submit
through `RenderMeshInstanced` is touched by it. Converting the world to MeshRenderers would put tens
of thousands of GameObjects under an edit-heavy simulation — the thing the renderer was built to
avoid. Not a candidate.

**(e) Cheap wins inside today's path.** The shadow margin (below) is the one that matters.
Otherwise: merge the thin tree buckets (4.5 instances each) by submitting trees per 2 × 2 chunk
region; the 511-instance cap never binds (buckets hold 5–25); `RenderParams` reuse and property-block
sharing are micro-savings.

### 3. The shadow-caster margin

The cull keeps a chunk if its box, grown by the shadow distance in every direction, touches the
frustum. A caster can only shadow the view if the view lies *down-sun* of it, so the box needs
growing **towards the sun** only: extrude the frustum along the light direction by the shadow
distance (or test each chunk's box swept along the light direction). With a sun at 30° the sweep is
mostly horizontal in one direction, so roughly half the margin's chunks — the ones on the far side
from the sun — can go. Expected: **~1–2 ms of the ~4.3 ms shadow term**, CPU and GPU both, for a few
dozen lines in `ChunkRenderer.InFrustum`, and the picture proof (`CullingDoesNotChangeThePicture`)
already exists to guard it.

## Recommendation

Ranked, committed:

1. **Tighten the shadow-caster margin to the sun-ward sweep** (§3). The largest term, the smallest
   change, and guarded by an existing proof. Measure with `TheLookAgainstTheSubmission` before and
   after.
2. **Move the dressing and the tufts to `RenderMeshIndirect` with a compute cull** (b), then the
   trees. They are drawn by our own shaders, so the change is contained; it removes about three
   quarters of the draw calls; and per-instance fade and colour fall out of it (the owner's leaf
   colour variety and see-through leaves want exactly that). Baking (c) is not recommended: the
   readability workaround, the memory, and the loss of per-instance fade and levels of detail cost
   more than it saves once (b) exists.
3. **Take the dressing's GPU cost on directly** — levels of detail on for bushes and grass stands
   with a bias tuned at the play camera, and fewer overlapping cards per clump. That is where ~2.4 ms
   of the 4K frame is, and no submission change touches it.
4. **BRG** (a) is the long-term path if the whole world (terrain, walls, floors) should become
   GPU-resident. It is not needed for the look pass's cost, and its variant bill must be measured in
   a DX11 build first.

**Tie between (b) and (a) for the dressing:** the cheapest experiment is one kind — the tufts —
through `RenderMeshIndirect` in a branch, timed with `TheLookAgainstTheSubmission` at 640 × 480
(CPU-bound) against today's path in the same run. If one kind saves ~0.3–0.5 ms of CPU, (b) carries
the rest; if it saves nothing, the per-call cost is not where the arithmetic puts it and BRG would
not save it either.

## Sources

- https://docs.unity3d.com/6000.1/Documentation/Manual/batch-renderer-group.html — BRG overview.
- https://docs.unity3d.com/6000.0/Documentation/Manual/batch-renderer-group-how.html — draw
  commands, filter settings, "Unity doesn't perform frustum culling … you must provide your own".
- https://docs.unity3d.com/6000.0/Documentation/Manual/batch-renderer-group-getting-started.html —
  SRP Batcher, "BatchRendererGroup variants: Keep all", URP strip-unused-variants off, unsafe code.
- https://docs.unity3d.com/6000.3/Documentation/Manual/dots-instancing-shaders-declare.html and
  https://docs.unity3d.com/6000.2/Documentation/Manual/dots-instancing-shaders-support.html —
  `DOTS_INSTANCING_ON` in custom URP shaders.
- https://discussions.unity.com/t/batchrenderergroup-complains-that-shadergraph-shader-lacks-dots_instancing_on-variant/948344
  — the variant requirement in practice.
- https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Graphics.RenderMeshIndirect.html —
  indirect arguments, `UNITY_INDIRECT_DRAW_ARGS`, `GetIndirectInstanceID()`, compute required.
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh-vertexBufferTarget.html and
  https://discussions.unity.com/t/setting-vertexbuffertarget-on-a-mesh-breaks-in-build-but-not-editor/860671
  — raw vertex buffers; non-readable meshes break when made raw.
- https://docs.unity3d.com/6000.0/Documentation/Manual/urp/gpu-resident-drawer.html — MeshRenderer
  GameObjects only.
- https://unity.com/blog/engine-platform/srp-batcher-speed-up-your-rendering — the SRP Batcher on
  DX11.

## Confidence

- **High** for the measurement's direction (two runs agree; every arm's control applied) and for the
  shadow term being the largest.
- **Medium** for the per-call figures (0.77 µs Unity, ~0.3 µs ours): one machine with other editors
  open; the difference arms are in-run, but the absolute values are not portable.
- **Medium** for the expected savings of (b) and §3 — arithmetic from the census, not a prototype.
- **Low** for BRG's variant cost in a DX11 player.

## Could not be determined

- The GPU split by pass: `GpuFrameMs` reads unavailable in a batch run on DX11, so the 4K frame
  stands in for GPU time. A RenderDoc or Nsight capture of one 4K frame would split shadow pass,
  opaque, foliage and post.
- What the owner's overlay "5 ms" was exactly (the owner was unsure); the batch `submit` at full
  margin is 2.5 ms, and the editor's own overhead plausibly doubles it.
- Whether Synty's Shader Graph materials drawn by BRG keep their look under DOTS instancing without
  per-material work.
- How much of the dressing's ~2.4 ms of fill levels of detail recover at the play camera — the next
  measurement once they are tuned.
