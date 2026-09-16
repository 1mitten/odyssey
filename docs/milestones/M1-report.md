# M1 report — World

Written 2026-09-16 on the Windows dev machine (AMD Ryzen 7 9800X3D, NVIDIA RTX 5070 Ti, driver
32.0.16.1656, Unity 6000.3.24f1, URP 17.3.0), branch `claude/m1-world` at `53bd5bd` plus the
benchmark commit that follows this report. **This is a stop-for-review report, and it contains
one red result.** Read §4 before anything else.

## 1. What M1 delivered

Everything U09–U16 in `docs/plans/vertical-slice.md` exists and is tested: the SoA cell grid and
chunk grid, the support solver with its incremental-versus-full oracle test, both worldgen paths
(ruined city and natural), shell templates authored in cells, instanced per-chunk rendering with
no GameObject per cell, the slice camera, and click-to-inspect through the snapshot. Beyond the
plan, the owner directed a look-and-feel pass this session — items as art, walking colonists
from a cast of 61, scattered grass, a screen-space ink outline, a gradient sky, a bracket cursor
sized to what it selects — which is committed and shown in `Logs/shot-*.png`.

## 2. The standing gate (08-milestones.md)

| Part | Requirement | Result |
|---|---|---|
| 1 | EditMode and PlayMode pass, no new Sim warnings | **Green.** EditMode 278 tests, 276 passed, 2 skipped. PlayMode passes vacuously — no PlayMode test assembly exists. Zero `warning CS` under `Assets/Odyssey/Sim/`. |
| 2 | Same seed, same hash, two processes | **Green in-process, proven cross-process once.** `HeadlessRunTests.TheSameSeedGivesTheSameWorldAfterASixthOfADay` (10,000 ticks, two worlds, equal hash; a third test proves different seeds differ). Cross-process equality was proven by the D1 benchmark (`c7d0d7f512c0feca` from two Unity invocations) and is not yet a standing test — queue row OQ-05. |
| 3 | Save at N, load, run to N+K equals uninterrupted | **Green on the fixture, not yet on a real world.** `SaveTests.RoundTripPreservesTheStateHash` passes; `CellGrid` is not `ISaveable`, so a full-world round trip cannot yet be written — queue rows OQ-08/09. |
| 4 | Headless one-day run, zero errors | **Green.** `OneDay.Run`: 120 × 120 × 16, seed 1, 60,000 ticks, 0 errors, 5 colonists at the end, hash `7312d77652c0cf84`. Mean tick **0.003 ms**, worst 6.53 ms (the first tick, building the nav graph), one tick over 5 ms. |
| 5 | Report written; clone without Synty passes Sim tests | **Green.** This file. The fast tier (`scripts/test-fast.sh`, no Unity, no Synty) runs all 227 Sim tests green. |

**M1 gate addition** — full 250 × 250 × 40 generation inside a stated budget, support assertion on
every template: **green.** `GeneratingTheScaleTargetMapIsReasonable` (city, under 20 s; measured
about 215 ms) and `EveryPassRunsAndTheConsistencyAssertionPasses` (five seeds). The 20 s budget is
generous by two orders of magnitude and should be stated as a real number — OQ-02.

## 3. Rendering: draw calls and instances, headless (exact)

`PlayScene.Measure` / `MeasureCity`, renderer in dry-run mode, 120 × 120 × 16, seed 1, the
layer the scene opens on.

| Map | Draw calls | Instances | Chunks drawn | Materials | Steady CPU submit | Full re-mesh, 200 chunks |
|---|---|---|---|---|---|---|
| Barren meadow, grass 60/100 | 116 | 23,156 | 25 | 4 | 0.02 ms | 9.0 ms (0.045 ms/chunk) |
| Ruined city, stamped shells | 1,309 | 78,464 | 155 | 42 | 0.23 ms | 11.8 ms (0.059 ms/chunk) |

A 25-chunk slice re-meshes in about 1.5 ms against design 06's 4 ms budget. The city figure is
the headless half of U14's "20,000 walls across several materials" validation: 78,464 instances
over 42 materials. The Frame Debugger batch count — the other half — needs an editor window and
is owner work.

## 4. Rendering: GPU frame time — RED

`RenderBench.Run`, 1920 × 1080, 40 frames after 8 warm-up, GPU synced once per run, on the
RTX 5070 Ti. Each row differs from the last by one decision.

| Variant | Board camera | Close camera |
|---|---|---|
| Nothing submitted (harness floor) | 0.81 ms | 1.80 ms |
| Bare ground: 14,400 instanced cubes, one material | **16.06 ms** | 16.28 ms |
| + grass at 60/100 (8,756 tufts, 50 tris each) | 91.7 ms | 53.5 ms |
| + grass casting shadows | +8.3 ms | +16.6 ms |
| + outline pass | −22 ms (noise) | −15 ms (noise) |
| Grass at 120/100 (17,347 tufts) | **610.9 ms** | 317.6 ms |

**These numbers fail the plan.** ADR 0005 leaves roughly 8.7 ms of a frame for all rendering on
the target laptop after a 3× discount; on a card several times faster than that laptop, bare
ground alone takes 16 ms. This is not the harness (the floor is under a millisecond) and it is
not triangle count: fourteen thousand cubes are a few hundred thousand triangles, which this
card draws in a fraction of a millisecond. The cost is about a microsecond per instance and
grows *superlinearly* with instance count — doubling the grass costs seven times as much — which
points at the submission path, not the geometry.

**This is also the best candidate for the two `DXGI_ERROR_DEVICE_REMOVED` crashes** the owner hit
in the editor this session. A 610 ms frame is within a factor of three of Windows' two-second GPU
timeout; with the Scene view rendering the same world alongside the Game view, it would cross it.
The two earlier fixes in that area (drawing every LOD level at once; leaking every baked mesh)
were real bugs, but they are not this.

**Not yet diagnosed.** The candidates, in the order they should be tested with the Frame
Debugger or a micro-benchmark on bare ground (the simplest reproducer):
1. The draws are not actually instancing — each instance becoming its own draw would give
   exactly one microsecond per instance. Check whether the material clones in `MaterialCache`
   really carry `enableInstancing` through to the URP Lit variant, and what the Frame Debugger
   reports per `RenderMeshInstanced` call.
2. `Graphics.RenderMeshInstanced` re-uploading every matrix array every frame through a path
   that allocates, which would explain superlinear growth.
3. The Synty terrain material's shader variant (alpha clip, fog, shadows) rather than the URP
   Lit fallback — separable by drawing the same cubes with a plain Lit material.

Until this is resolved the renderer is **not** within the plan's budget and M1 should not be
called closed. It is the top row of `docs/plans/overnight-queue.md` for the Windows machine.

## 5. What was learned this session

Recorded in `docs/lessons.md`: choosing pack art by rendering it, not by its name; prefabs
carrying LOD groups; the serialised-value trap on renderer features and scene fields; "the
question is how wide a thing is on screen, not how far away"; a colour cast that was geometry;
and that a `Mesh` created in code is a GPU allocation nobody collects.

## 6. Stop

Per the plan, work stops here for review. The recommended next step is the diagnosis in §4,
before any M2 unit, because a renderer that cannot draw an empty meadow inside budget will not
draw a city with pawns in it.
