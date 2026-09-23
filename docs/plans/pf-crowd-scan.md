# PF — the O(N²) crowd scan: a prompt for the next agent

**Written 2026-09-23 from PR #168's frame sweep**, which was measuring something else and produced
the clearest evidence yet for this. Paste the section below into a fresh session.

---

## The prompt

> Read `CLAUDE.md`, then `docs/design/06-rendering-and-camera.md` §6c and §6c.2, then
> `docs/design/25-pawn-steering.md` — including its closing section, *What the crowd scan costs*.
> Then read `docs/plans/pf-crowd-scan.md` (this file) for the measurements taken on 2026-09-23.
>
> **The job: make `PawnPose.Of` stop scanning every other pawn, without changing what a single
> colonist does.**
>
> Work the project's phases — ground, then a design note, then test-first, then measure, then hand
> over. **Do not start on the fix until you have reproduced the cost on your own machine in one run**,
> because the numbers below were taken on a machine running several editors and are only comparable
> within their own run.

### What is known, and measured

`PawnPose.Of` takes a `ReadOnlySpan<PawnView> otherPawns` and, for every **moving** pawn, walks the
whole span to find the nearest one worth sidestepping
(`Assets/Odyssey/Presentation/Rendering/PawnPose.cs`, the loop at `for (int i = 0; i < otherPawns.Length; i++)`).
That is O(N²) across the colony, and it is paid **twice** — `ChunkRenderer` calls it for the baked far
form (`ChunkRenderer.cs:918`) and `PawnFigureDirector` for the live figures
(`PawnFigureDirector.cs:1034`).

**Measured 2026-09-23** on the barren natural board, 640 × 480, RTX 5070 Ti, by
`FrameTimeTests.TheFrameAgainstColonySize`:

| Colony | Figures | Frame | `Actors` submit | Draw calls |
|---|---|---|---|---|
| 8 | 8 | 2.41 ms | 0.020 ms | 1,125 |
| 32 | 32 | 3.03 ms | 0.024 ms | 1,125 |
| 64 | 64 | 4.04 ms | 0.027 ms | 1,125 |
| 96 | 64 | 5.20 ms | 0.528 ms | 1,147 |
| 128 | 64 | 6.47 ms | 1.301 ms | 1,149 |
| 192 | 64 | 10.36 ms | 3.644 ms | 1,151 |
| 256 | 64 | 15.67 ms | 7.107 ms | 1,151 |
| 384 | 64 | **30.35 ms** | **17.208 ms** | 1,152 |

**Read the table carefully, because it rules things out.**

- **It is not draw calls.** 1,125 → 1,152 across a forty-eight-fold colony. Submission is flat.
- **It is not the tick.** 0.010 ms at 8, 0.047 ms at 96 — the simulation is nowhere near this.
- **It is not the attachments.** PR #168 measured hair and beards against a control in the same run:
  **+0.049 ms at 64 and +0.051 ms at 192** — flat, and 0.3% of the frame at 384.
- **The growth is super-linear in the number of far pawns.** Far pawns are `colony − 64`: 32, 64,
  128, 192, 320 against `Actors` of 0.53, 1.30, 3.64, 7.11, 17.21 ms. Doubling the far pawns roughly
  triples the cost, which is the signature of a quadratic with a linear part under it.

**§6c.2 already has the causal model, and it predicts well.** Figures are capped at 64, so that pass
is 64 × N — linear. Actors is every pawn without a figure, so it is (N − 64) × N — quadratic:
**147,456 pairs at 384 pawns**, each with a `Vector3.Distance`, at about 90 ns a pair ≈ 13 ms against
the 13.275 ms measured then. Against the 17.21 ms measured here the same model gives ~117 ns a pair,
which is the same model on a busier machine rather than a different effect.

**Use that model as the check on your fix**: an exact 3 m cull should cut the *pair count*, and the
new timing should land where pairs × ~100 ns predicts. If it does not, something else is in there too.

The earlier record — *13.3 ms of a 22.5 ms frame at 384* — was taken on a different branch and a
differently loaded machine. **Both readings agree on the shape; neither is a baseline for the other.**
Take your own.

### The hard constraint, and why the fix is exact

**The sidestep itself is judged and settled. Do not re-open it.** The owner has played it and
`25-pawn-steering.md` records what was tuned and what was deliberately left alone.

The optimisation is available *because* it can be exact: `SteeringCurve.Proximity` returns **zero at
or beyond `CrowdFarRadius`, which is 3.0 m**, against a cell 2.5 m square. A pawn further than 3 m
away contributes `near <= 0` and is `continue`d. So **every pair the scan currently visits and
discards contributes nothing**, and a spatial cull that visits only pawns within 3 m produces
**bit-identical output**.

That is the bar: *the same pose, not a similar one*. A test that pins it is worth more than the
optimisation.

### Two candidates, cheapest first

1. **Hoist `SteeringCurve.WhereItIsNow(in other)` out of the inner loop.** It is recomputed for every
   *other* pawn, for every *posed* pawn — N² calls for N distinct answers. Computing each pawn's
   current position once per frame into a scratch array is a pure win with no behaviour change and no
   new data structure. **Do this first and measure it alone**: it may be most of the cost, and it
   would be embarrassing to build a spatial index on top of an unmeasured constant factor.
2. **A broadphase over the 3 m neighbourhood.** With 2.5 m cells, 3 m reaches at most the 3 × 3 block
   of cells around a pawn (and one layer up and down — the distance is 3D on purpose, because a board
   of 3 m terrace risers once gave a full sidestep to somebody a storey below). A per-frame bucket
   keyed on cell, rebuilt once per frame in O(N), turns the scan into a handful of candidates.
   Rebuilding it is presentation-side and per frame; **nothing about it may enter the save or the
   state hash**.

### How to measure it

**With a control, in the same run** — `06-rendering-and-camera.md` §6c.1. Two precedents to copy:
`ChunkRenderer.SubmitToGpu` and `ColonistAttachments.Enabled`, the latter added in PR #168 with
`FrameTimeTests.TheAttachmentsCostWhatTheyDraw`, which alternates on/off/on/off at two colony sizes
so a drift landing between the halves cannot be mistaken for the pass. **Copy that test's shape.**

Traps this project has already paid for:

- **This machine runs several editors at once.** A frame number is comparable only with one from the
  same run; the city canary drifted 2.01 → 4.01 ms in an afternoon on what a sibling worktree was
  doing. Check `Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"` first.
- **Do not run the PlayMode tier beside another Unity batch run**, and wait for
  `TestResults/PlayMode.xml` to be *newer* than the run you started.
- **"A submission costs about 4.6 µs" is not a per-call toll** and must not be used to condemn a loop
  before it is measured (§6c.1).

### What done looks like

- A design note in `docs/design/` recording the shape chosen, what was rejected and why.
- A test that the pose is **unchanged**, pawn for pawn, across a crowded board — the exactness claim,
  pinned.
- A before/after from **one run**, with the control, at 64 / 192 / 384.
- The `Actors` and `Figures` splits from `OdysseyBootstrap.FrameSectionMs`, not just the frame.
- §6c.2 updated with the result, and `docs/bug-patterns.md` if a pattern falls out.
- **`FigureCeilingTests` still passes.** 64 is a hard ceiling (owner, 2026-09-20) and moving it is a
  frame measurement, not an edit. This work does not licence raising it.

### One open question worth answering on the way

**Why is `Actors` 0.027 ms at 64 figures?** At 64 every pawn is a live figure, so the far-form gather
loop runs over zero pawns — yet `Figures` at 64 is 1.526 ms and climbs from 0.102 ms at 8. The live
side pays the same scan. Splitting the measurement so `Figures` and `Actors` are attributed
separately would say whether one fix serves both call sites or whether they need different ones.
