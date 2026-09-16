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

## 4. Rendering: GPU frame time — green, after a retraction

**An earlier draft of this section reported a red result, and it was wrong.** It came from
`RenderBench`, an editor batch harness that drives `camera.Render()` in a loop into a
RenderTexture. That loop has no frame boundary — nothing Presents, and URP's per-frame
bookkeeping is never told a frame ended — so every render carries the debris of every render
before it. The draft claimed 14,400 instanced cubes cost 16 ms; the same row read 205 ms and
307 ms on later runs, a row of 26-triangle tufts cost five times a row of cubes with the same
material, and the decisive control — the identical *empty* render placed first and last in one
run — cost 2.65 ms and then 464 ms. The harness was measuring its own history. Every absolute
number it produced is void, and the post-mortem is in `docs/lessons.md` under "Benchmarking the
renderer". The two device-reset crashes the owner hit are therefore *not* explained by frame
cost; the leak and the LOD bugs fixed the same day remain the standing explanation, together
with script recompilation during Play.

**The real measurement** is `FrameTimeTests` — a PlayMode test, so a genuine player loop with a
Present every frame, 180 frames after 60 warm-up, the full play world with shadows, sky, grass
at 60/100 and the outline pass on, RTX 5070 Ti. It is also the first test the PlayMode gate has
ever contained.

| World | Mean | Worst | Draw calls | Instances | Chunks |
|---|---|---|---|---|---|
| Barren meadow (the scene as shipped) | **0.39 ms** | 0.58 ms | 118 | 23,175 | 25 |
| Ruined city, stamped shells | **1.46 ms** | 1.74 ms | 1,311 | 78,482 | 155 |

Against ADR 0005 — roughly 8.7 ms of a frame for all rendering on the target laptop after a 3×
discount — the city sits at about 4.4 ms discounted and the meadow at about 1.2 ms. **Within
budget, with one caveat that must be re-measured on the owner's monitor:** the batch game view
is 640 × 480, so per-pixel costs (the outline pass, the sky, overdraw) are under-represented
here. Draw submission, which dominates the city at 1,311 calls, does not scale with resolution.
The 1080p figure is one press of Play away — the bootstrap prints `frame … ms` top-left — and is
listed as owner work in the queue. The 20,000-wall Frame Debugger check remains owner work too.

What the failed benchmark did establish, and what stands: `Graphics.RenderMeshInstanced` throws
rather than silently drawing one instance per draw when a material lacks instancing (the
renderer's material cache enables it on every clone; the raw Synty foliage asset does not), and
the research in `d-11-instanced-submission.md` names BatchRendererGroup as the destination if
instance counts ever outgrow this path — with no urgency now that the path is measured inside
budget.

## 5. What was learned this session

Recorded in `docs/lessons.md`: choosing pack art by rendering it, not by its name; prefabs
carrying LOD groups; the serialised-value trap on renderer features and scene fields; "the
question is how wide a thing is on screen, not how far away"; a colour cast that was geometry;
and that a `Mesh` created in code is a GPU allocation nobody collects.

## 6. Stop

Per the plan, work stops here for review. Everything in the standing gate is green; two parts
are green on fixtures rather than on a real world (§2, rows OQ-05 and OQ-08/09) and the 1080p
frame time wants one look from the owner (§4). The recommended next step is M2, starting with
the `CellGrid` save section so the round-trip gate can hold on a real world.
