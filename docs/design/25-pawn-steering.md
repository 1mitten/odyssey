# 25 — Pawn steering: the sub-tile sidestep

*Written 2026-09-19, after the first crowd playtest. The plan this grew out of is
`docs/plans/pawn-avoidance-and-steering.md`; this holds what was decided, what was measured, and
what not to undo.*

## 1. What it is

The simulation is discrete and stays discrete: a colonist occupies one cell and retires integer cost
units towards the next. Determinism requires it and it is not negotiable. **Steering is a drawing
offset laid over that path** — a sub-tile sidestep that takes a colonist round a tree trunk, round a
colleague standing in a doorway, and past somebody coming the other way. Nothing here is in a cell,
a save or the hash.

It lives in `PawnPose.Of` because both things that draw a colonist need the same answer: the
instanced pass that draws a distant crowd as baked meshes, and the live figures that walk. A pawn
crosses between the two as the camera moves, and two copies of this arithmetic would look identical
until the day they disagreed.

## 2. The one rule: every input is read continuously

**This is the whole design, and it was learned the hard way.** The first implementation was a set of
boolean gates, and the owner reported colonists that *"vibrate quickly — as if it's fighting
something or a indecision"*.

Measured on a ticking colony of twenty over fifty seconds of play, the lateral offset moved **more
than five centimetres in a single tick 85 times**, the worst of them the full `0.600 m` envelope
inside one sixtieth of a second. Every one of those came from a threshold:

| The gate | What it did |
|---|---|
| `dot < -0.5` decided whether a colonist counted as oncoming | the entire sidestep switched on and off as the other one turned |
| `swappingCells \|\| sharingNext \|\| sharingCell` forced the weight to `1.0` | two colonists three metres apart who happened to share a destination snapped sideways, and the flag flickered as they re-planned |
| distance measured in x and z only | a colonist on the terrace above was **nought metres away** — on a board whose entire surface is 3 m terrace risers |
| the longest candidate offset won | two candidates of nearly equal length pointing opposite ways swap winner on any tick where a distance twitches, and the figure crosses the full envelope and back |

So the rule, and the thing to check before adding anything here: **if a new input can change between
one tick and the next, it must enter through a ramp, not an `if`.** `SteeringContinuityTests` fails
on each of the four above.

## 3. The shape

Everything is a **signed lateral scalar**, positive to the colonist's own right, summed and then
clamped once at the end.

- **The envelope** is `SteeringCurve.Bell(s)` over the step, `16·s²(1−s)²`. Value and first
  derivative are zero at both ends, so a sidestep is fully paid back by the cell boundary and no
  step hands a discontinuity to the next one.
- **Proximity** (`CrowdFarRadius` 3.0 m → `CrowdNearRadius` 1.5 m, smoothstepped) is how much room
  another colonist gets. Distance is **three-dimensional** and between *interpolated* positions, so
  it is continuous through the other one's step boundaries and a storey up is simply out of range.
- **In the way** is how much that colonist counts at all. Standing still is wholly in the way;
  moving counts to the extent the two paths converge, ramping from `ConvergingFrom` (just before
  square-on) to `OpposedBy`. Crossing traffic therefore gets about a sixth of the envelope and
  head-on gets all of it — crossing was given **nothing** before, and it is the case a player is
  most likely to be looking at, two colonists converging on one doorway.
- **Obstacles** add their own term: a trunk in the cell being crossed pushes right, and on a
  diagonal step a trunk in either corner cell pushes away from that corner. Corner pushes are
  **lateral only**. The first version pushed along the whole vector from the midpoint to the corner,
  which has a component along the path as well as across it, so the figure slowed into the corner
  and accelerated out of it — a hesitation, not a sidestep.

**Summed, not chosen.** Two obstacles on opposite sides now hold the colonist in the middle, which
is what a person does. A `max` has a winner, and a winner can be swapped.

## 4. The sway, and where it may not be applied

One input genuinely cannot be made continuous: **another colonist stopping or setting off** is a step
change in whether it is in the way, and the published frame has no notion of its speed. So the drawn
sidestep eases towards the asked-for one at `PawnFigureDirector.SwayRate`, 1.2 m/s — the full 0.6 m
envelope in half a second, about the time a person takes to lean out of somebody's way, and
deliberately slower than a walk because a sidestep that arrives faster than the colonist is
travelling reads as a flinch.

**It is the sidestep that is eased, never the whole drawn position.** An earlier pass rate-limited
the position itself at 5.5 m/s to satisfy the same owner rule, and that is a different thing
entirely: it damps the colonist's own walking, so a figure that cannot keep up with its own
locomotion lags behind the gait its legs are playing and then surges to catch up. Anything added
here that wants damping gets it on its own term, not on the position.

## 5. The gait is solved from walking, not from swerving

`PawnPose.Of` hands the sidestep back separately (`out Vector3 steer`) for one reason:
`PawnFigureDirector` takes it out again before `ObserveSpeed`.

A 0.6 m sidestep inside a tenth of a second is six metres a second — past the fastest gait this cast
owns (2.60 m/s) — so giving way to somebody threw the legs into a run and back. That is the "gait
blend flicker" an earlier pass went looking for in the blend and did not find, because it was never
in the blend.

## 6. Measured

Twenty colonists, wooded meadow, 3,000 ticks, 59,303 moving-pawn samples:

| | Before | After |
|---|---|---|
| Sidestep asked for: jumps over 5 cm in a tick | 85 | 52 |
| **Drawn sidestep: jumps over 5 cm in a tick** | **85** | **0** |
| Drawn sidestep: worst single tick | 0.600 m | **0.020 m** |
| Drawn sidestep: mean lag behind the asked-for one | — | 0.0016 m |

The 52 that remain are the genuinely discrete events of §4 — somebody stopping, setting off or
turning — and the sway is what they are for. The drawn figure cannot move sideways faster than
1.2 m/s under any input, and tracks within two millimetres on average, so the damper is invisible in
ordinary play and only does work where an input really steps.

## 6b. The sub-tick term, and why the simulation has to publish the rate

*Added 2026-09-19, second playtest: "it happens sometimes when colonists are walking, particularly
where there is a terrain step tile it starts to vibrate and move oddly ... mostly at the beginning
... when going up."*

**This was not the steering at all.** Bisecting the steering out of the measurement changed nothing,
to the frame.

A frame that lands between two ticks carries the figure on rather than waiting, which is what keeps
a walk smooth on a display faster than the tick. To carry it on you need the rate, and presentation
was **inferring** it: a global `movePerTick` out of the Defs, added to a percentage as though every
step cost `MoveCost.Orthogonal`. That is wrong three ways.

1. `MoveCost.Orthogonal` is 100, so a cost unit is a percent — for an orthogonal step and nothing
   else. A diagonal is 141, a hop up a terrace is 240, a ladder 540.
2. The rate is the colonist's own: pace and condition scale it (design 17 §4a).
3. **The step's price includes the terrain being entered**, and no amount of looking at the two
   cells recovers that. This is the one that makes the whole approach unfixable in presentation.

**The symptom of an over-estimate is walking backwards.** When the guess ran ahead of what the tick
retired, the next frame — taking the newly published figure — drew the colonist behind where the
last frame had put her. On a terrace bank that backward travel is also *downward* travel, which is
why a step tile is where it shows and flat ground is where it hides.

So `PawnView.MoveDeltaPerMille` is published: how much of *this* step *this* colonist retires in one
tick, computed where both numbers are known. **Truncated down, deliberately** — an under-estimate
makes the frame after a tick jump very slightly forward, an over-estimate makes it go backwards, and
only one of those can be seen.

`WalkContinuityTests` runs a real colony over a real generated board for this, because every cheaper
fixture missed it: a hand-built world grows no banks, and a hand-built `PawnView` publishes no rate,
so it takes the fallback path rather than the one the game takes.

Measured on the wooded meadow, twelve colonists, 2,500 ticks, two frames to the tick:

| | Before | After |
|---|---|---|
| frames drawing a colonist **backwards** along her own step | 3,172 of 59,000 | **0** |
| worst backward frame | 10.9 mm | **0** |
| frames reversing vertically, walking on the flat | 1,406 (2.5%) | **0** |
| frames reversing vertically, climbing | 2 | **0** |

## 7. Left alone on purpose

- **The simulation-side path bias is not wired.** `PathFinder.Occupancy` and
  `MoveCost.OccupiedBias` exist, are tested by `AvoidancePathingTests`, and **nothing sets them** —
  `PathService.Occupancy` has no caller in the build. Turning it on changes planned routes and
  therefore every golden hash, so it is a deliberate decision with a re-bake attached rather than
  something to switch on in passing.
- **Steering does not feed back into the path.** A colonist steers around what is in front of it and
  still arrives at the cell the simulation sent it to. That is what keeps all of this free of the
  hash.

## 8. What only a person at the keyboard can answer

- Whether 0.6 m is enough room, or whether colonists still read as brushing each other.
- Whether half a second is the right time to lean out of the way, or whether it reads as sluggish
  now that nothing snaps.
- Whether crossing traffic getting a sixth of the envelope reads as courtesy or as a twitch.

## What the crowd scan costs

**Measured 2026-09-20** (`docs/design/06-rendering-and-camera.md` §6c.2). `PawnPose.Of` walks the
whole pawn span for every pawn it poses, so the crowd term is **O(N squared)** across a colony:
13.3 ms of a 22.5 ms frame at 384 colonists, against 0.02 ms at 64. It is the largest single cost
the renderer has, and it appears only above `PawnFigureDirector.FigureCeiling`, because below the
ceiling every pawn has a figure and the scan is the capped 64 x N one.

**Nothing about the look is in question.** `CrowdFarRadius` is 3.0 m against a 2.5 m cell, so
`Proximity` is exactly zero beyond roughly one cell: a cell-bucketed index over the pawn span
would skip only pairs that contribute nothing, and the offsets it computes would be identical to
the last bit. The sidestep as judged stays as judged; what changes is how many pairs are asked
about.

> **Done, 2026-09-23 — see §9 below**, which supersedes this paragraph. The index is built, the
> pose is pinned bit-for-bit, and the frame at 384 colonists went 27.81 ms to 14.99 ms on a clear
> machine. Two things in §9 are worth reading even if the fix is not in question: **44% of the cost
> was the `WhereItIsNow` recompute rather than the quadratic**, which is why the plan's instruction
> to measure the cheap candidate alone earned its keep; and the cull uncovered **a second O(N²) in
> the same pass** which is reported and not fixed.

## 9. Making the scan stop walking the colony

*Written 2026-09-23. The prompt and the evidence that started it are `docs/plans/pf-crowd-scan.md`;
the frame context is `docs/design/06-rendering-and-camera.md` §6c.2. **Nothing about the sidestep
changed.** §1–§8 above stand exactly as they were, and that is the constraint this section is
written under rather than a claim it makes in passing.*

### 9a. What it cost, measured here

The `Actors` and `Figures` passes both call `PawnPose.Of`, and `PawnPose.Of` walked the whole pawn
span for every pawn it posed. The earlier readings (§6c.2, and the plan's) were taken on a machine
running several editors; this is the same sweep taken on a clear machine, and it is the *before* the
rest of this section is measured against. `FrameTimeTests.TheFrameAgainstColonySize`, barren natural
board, 640 × 480, RTX 5070 Ti, 2026-09-23.

| pawns | figures | frame | `Figures` | `Actors` | draw calls |
|---|---|---|---|---|---|
| 8 | 8 | 2.08 ms | 0.091 | 0.017 | 1,125 |
| 32 | 32 | 2.59 ms | 0.499 | 0.020 | 1,125 |
| 64 | 64 | 3.72 ms | 1.425 | 0.023 | 1,125 |
| 96 | 64 | 4.65 ms | 1.870 | 0.467 | 1,147 |
| 128 | 64 | 5.90 ms | 2.361 | 1.180 | 1,149 |
| 192 | 64 | 9.35 ms | 3.583 | 3.315 | 1,151 |
| 256 | 64 | 14.09 ms | 5.020 | 6.433 | 1,151 |
| 384 | 64 | **27.81 ms** | **8.646** | **15.785** | 1,152 |

**The causal model predicts this to within a few per cent, which is what licenced going straight at
it.** `Actors` poses every pawn without a figure against every pawn, so its pair count is
`(N − 64) × N`:

| pawns | pairs | `Actors` | ns a pair |
|---|---|---|---|
| 96 | 3,072 | 0.467 ms | 152 |
| 128 | 8,192 | 1.180 ms | 144 |
| 192 | 24,576 | 3.315 ms | 135 |
| 256 | 49,152 | 6.433 ms | 131 |
| 384 | 122,880 | 15.785 ms | **128** |

A flat ~130 ns a pair across a forty-fold range of pair counts. It is the scan and nothing else.

> **One correction to the plan while it is in view.** `docs/plans/pf-crowd-scan.md` gives 147,456
> pairs at 384 and calls it `(N − 64) × N`. 147,456 is `384²`; `(N − 64) × N` is **122,880**. The
> shape of the argument is untouched and the per-pair figure moves from ~117 ns to ~128 ns, which is
> if anything a better fit. Recorded because the next person to check the model against a measurement
> will otherwise find it 20% out and go looking for a second effect that is not there.

**And the open question the plan asked to answer on the way is answered.** It asked why `Actors` is
0.027 ms at 64 figures when `Figures` is already 1.5 ms and climbing. Both pay the scan; the figure
cap is what separates them. Below 64 every pawn is a live figure, so `Actors` poses nobody at all and
`Figures` pays the `min(64, N) × N` linear scan on its own. Above it `Figures` is pinned at 64 posed
pawns and grows only through the span it walks — from 1.425 ms at 64 pawns (4,096 pairs) to 8.646 ms
at 384 (24,576 pairs), which is **353 ns a pair against `Actors`' 128**. So the live side pays the
same scan at a worse constant, and one fix serves both call sites. What the extra 225 ns is has not
been isolated and is not claimed here.

### 9b. The shape chosen, and why it is allowed to exist

**`PawnCrowdIndex`: every pawn's position computed once a frame and bucketed on a 3 m grid, built in
the composition root and shared by every pass that poses a pawn.**

The licence for it is that the cull is **exact, not approximate**. `SteeringCurve.Proximity` is
`SmoothStep((3.0 − d) / 1.5)`, which returns exactly `0f` at and beyond `CrowdFarRadius`, and the
loop already discarded a zero. So every pair the index skips is a pair the old loop visited and threw
away, and the reduction is a `max`, which does not care in what order it sees its arguments. The
result is the same pose, not a similar one — the bar the plan set, and the thing
`PawnCrowdIndexTests.EveryScanModeDrawsTheIdenticalPose` pins component by component with no
tolerance anywhere in it.

Three decisions inside that are worth not undoing:

- **The bucket is the radius, not the cell.** 3.0 m, which is `CrowdFarRadius` and also exactly one
  cell layer, against a cell 2.5 m square. At bucket size `B ≥ r` the 3 × 3 × 3 block around a query
  point provably holds everybody within `r`: for `p ∈ [Bb, Bb+B)` and `|q − p| ≤ r ≤ B`, `floor(q/B)`
  lies in `[b−1, b+1]`. Keying on the cell instead would need a **fourth** neighbour in x and z,
  because 2.5 m buckets do not reach 3 m in one step — which is the off-by-one this note exists to
  stop somebody reintroducing while tidying the bucket size to match `CellMetrics`.
- **The vertical is a real axis.** Distance is 3D on purpose (§2: a colonist on the terrace above was
  nought metres away under the old x/z measure, on a board whose whole surface is 3 m risers). A
  3.0 m bucket in y keeps that exactly, and `SomebodyJustInsideTheRadiusOverheadIsStillReached` is
  the guard.
- **The table is sized to the colony, not to the board.** Open addressing with a generation stamp, so
  a rebuild clears nothing. A dense grid over 120 × 120 × 16 cells would be 160,000 buckets to clear
  every frame for a colony of fifty, which is the per-board-rather-than-per-thing cost the audit's
  scaling rules exist to catch (`docs/process.md` §3).

**One index, built once, in the composition root.** Three passes pose a pawn against its neighbours —
the live figures, the baked far form and the stand-in load a carrier holds. Building it in any of
them would build it two or three times and leave them able to disagree, which is the exact fault
`PawnPose` itself exists to prevent (§1). It is rebuilt from `Views.Current`, the same snapshot all
three read, so it cannot be a frame out of step with what is drawn. It has its own `FrameSection`
rather than a charge on `Figures`, because billing a shared O(N) rebuild to the first of its three
consumers would flatter `Actors` by exactly the amount it hid.

**Nothing here is in a cell, a save or the state hash**, and no golden moved.

### 9c. Three arms, because the plan warned against two

The plan named hoisting `SteeringCurve.WhereItIsNow` out of the inner loop as the cheap candidate —
N² calls for N distinct answers — and said to **measure it alone** before building a spatial index on
top of an unmeasured constant factor. Building the index subsumes that hoist, so after the fact the
two cannot be told apart.

So the control is three-valued rather than a bool. `CrowdScan.Span` is what shipped; `Cached` is the
hoist and nothing else, the same N² visit reading positions from the frame's cache; `Bucketed` adds
the cull. All three reach one shared `PawnPose.CrowdWeight`, so they are three ways of finding the
survivors and one way of weighing them — which is what makes the exactness structural rather than a
coincidence a later edit could break.

**`FrameTimeTests.TheCrowdScanCostsWhatItVisits`**, each arm timed twice, alternating, in one run on
a clear machine. Barren natural board, 640 × 480, RTX 5070 Ti, 2026-09-23.

| colony | scan | frame | `Figures` | `Actors` | `Crowd` |
|---|---|---|---|---|---|
| 64 | Span | 4.508 ms | 1.734 | 0.029 | 0.008 |
| 64 | Cached | 3.967 ms | 1.378 | 0.027 | 0.007 |
| 64 | Bucketed | **3.867 ms** | 1.231 | 0.028 | 0.007 |
| 192 | Span | 10.578 ms | 4.034 | 3.717 | 0.022 |
| 192 | Cached | 7.747 ms | 3.096 | 2.054 | 0.020 |
| 192 | Bucketed | **6.219 ms** | 2.598 | 1.076 | 0.021 |
| 384 | Span | 30.104 ms | 9.388 | 17.259 | 0.042 |
| 384 | Cached | 20.846 ms | 7.942 | 9.710 | 0.041 |
| 384 | Bucketed | **16.027 ms** | 7.290 | 5.303 | 0.042 |

**The plan's warning was right, and it is the most useful thing in this table.** At 384 colonists the
hoist *alone* takes `Actors` from 17.259 ms to 9.710 — **44% of the cost was one line**, recomputing
N distinct positions N times. The cull takes it on to 5.303. Had the index been built and measured
only against `Span`, it would have been credited with all 11.96 ms when 7.55 of it belonged to a
change that needs no data structure at all. **Measure the cheap candidate alone, even when you
intend to build the expensive one.**

**The index itself costs 0.042 ms at 384 colonists** — one O(N) pass, serving all three consumers.

**Taken three times on a clear machine, and the three agree.** `Actors` at 384 colonists, for
Span / Cached / Bucketed:

| run | Span | Cached | Bucketed |
|---|---|---|---|
| 1 (control alone) | 17.259 ms | 9.710 ms | 5.303 ms |
| 2 (control alone, repeat) | 16.466 ms | 9.696 ms | 4.865 ms |
| 3 (inside the full PlayMode tier) | 17.703 ms | 9.710 ms | 5.186 ms |

**The `Cached` arm landed on 9.710, 9.696, 9.710.** Three independent runs, one of them inside the
whole PlayMode tier rather than alone, agreeing to about a part in a thousand. **No single run is
quoted as "the number"** — what is being offered is the same three ratios three times, which is the
most this machine supports. The frame at 384 was 14.494 to 17.137 ms Bucketed against 28.657 to
33.809 Span, and the spread there is the machine rather than the pass: it is why the `Actors` split
is the figure to read and the whole frame is not.

### 9d. It uncovered a second O(N squared), which is not fixed

**`Actors` is still quadratic after the cull, and the arithmetic says so plainly.** Against the
`(N − 64) × N` pair count: 1.076 ms over 24,576 at 192 is **43.8 ns** a pair, and 5.303 ms over
122,880 at 384 is **43.2 ns**. Flat across a five-fold range of pair counts is the same signature
that identified the crowd scan, at a third of the cost.

It is a different pass. The far-form loop asks `Cast.LookFor(snapshot, pawns[i].Id)` for each pawn
it draws, which is `ColonistNames.RollSeedOf` → **`WorldSnapshot.TryGetPawnAspect`, a linear scan
over every published aspect** (`Sim.Contracts/Views.cs`). The aspect count grows with the colony, so
one lookup per far-form pawn per frame is `(N − 64) × kN`.

**Left for its own unit, deliberately.** This one was asked to make `PawnPose.Of` stop scanning the
colony, and it does; the remaining term is in the snapshot contract rather than in steering, and
changing how aspects are read is a wider blast radius than a presentation-side mirror.

> **Done the same day — `docs/design/31-aspect-lookup.md`.** A colonist publishes **57** aspect rows
> a tick, so the published set is 57 × colonists and `TryGetPawnAspect` scanned it. A lazy index
> built on the first lookup of each frame took `Actors` at 384 colonists from 4.59 ms to **0.83**,
> and `Figures` from 6.39 to **0.74** — the prediction in the next paragraph, confirmed. The whole
> frame at 384 is **4.82 ms**, against 27.81 before this line of work began, and **the knee is
> gone**.

**And `Figures` is now linear**, which is the other half of the plan's open question. With the figure
count pinned at 64, it goes 1.231 ms at 64 pawns to 7.290 at 384 — 5.9× for a 6× colony. That is
`64 × kN`: the same aspect lookup, once per drawn figure rather than once per pair, and linear in the
colony rather than quadratic.

### 9e. The whole sweep, before and after

`FrameTimeTests.TheFrameAgainstColonySize`, the same eight colony sizes. Before is the clear-machine
baseline in §9a; after is the same test in the run that took the control above.

| pawns | frame before | frame after | `Actors` before | `Actors` after | `Figures` before | `Figures` after |
|---|---|---|---|---|---|---|
| 8 | 2.08 ms | 2.49 ms | 0.017 | 0.022 | 0.091 | 0.104 |
| 32 | 2.59 ms | 2.99 ms | 0.020 | 0.024 | 0.499 | 0.476 |
| 64 | 3.72 ms | 3.68 ms | 0.023 | 0.026 | 1.425 | 1.173 |
| 96 | 4.65 ms | 4.88 ms | 0.467 | 0.259 | 1.870 | 1.615 |
| 128 | 5.90 ms | 4.82 ms | 1.180 | 0.504 | 2.361 | 1.774 |
| 192 | 9.35 ms | 9.17 ms | 3.315 | 1.532 | 3.583 | 3.561 |
| 256 | 14.09 ms | 8.60 ms | 6.433 | 2.179 | 5.020 | 3.733 |
| 384 | **27.81 ms** | **14.99 ms** | **15.785** | **4.937** | 8.646 | 6.835 |

The confirming run's sweep is cleaner still and monotonic throughout —
2.26 / 2.70 / 3.59 / 4.08 / 4.71 / 6.22 / 8.48 / **14.41 ms** across the same eight sizes — which is
the shape to trust where the two disagree in the middle of the table.

**Read the small sizes as noise, not as a regression.** At 8 and 32 pawns the two runs differ by
0.4 ms in a frame where `World` and `Surround` alone moved by 0.13 ms between them, and the passes
being changed cost 0.02 ms there. The `Crowd` rebuild is 0.001 ms at 8 pawns. Nothing at the scale
target moved, which is the honest summary of the top half of this table.

**At the scale target — fifty colonists — this changes nothing perceptible, and that was known before
it was built** (§6c.2: "a ceiling on how big a colony may get, discovered four years before it
binds"). What it buys is that the ceiling is no longer where it was.

### 9f. What was not done


- **The figure ceiling is untouched.** 64 is a hard ceiling (owner, 2026-09-20) and
  `FigureCeilingTests` still fails anything that raises it. Making the scan cheap is not a licence to
  raise it; that remains a frame measurement.
- **The sidestep is untouched.** §1–§8 stand. The open questions in §8 are still open and still only
  a person at the keyboard can answer them.
- **The second quadratic found on the way is reported, not fixed** — the aspect scan behind
  `Cast.LookFor`, §9d. It is now the largest per-frame quadratic left.
