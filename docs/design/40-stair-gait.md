# 40 — The stair gait: banks crossed as steps, not escalators

**Status: designed and built 2026-09-24, from the owner's plan for "stair-gait animation for
terraced terrain" (reviewed against the code first — see §2).** Branch `claude/stair-gait`,
worktree `D:\code\odyssey-stairgait`. Ground: `main` at `3a39dd8d`. Numbered 40 because 37 is
`claude/medical-supplies`, 38 is meadow and 39 is weather, all in flight.

**Read first:** `HopArc.cs`'s header (the two rejections this must not repeat),
`28-temperature.md` nothing — rather `PawnPose.cs` `OnTheDrawnGround` (the branch this lives
in), `31-campfire` nothing — rather `BankFootingTests` (the smoothness budget this must fit).
The plan's own RimWorld-flavoured background does not repeat here; what repeats is what the
code actually says.

## 1. What the owner asked for, in one table

| Topic | Decision |
|---|---|
| The fault | A figure crossing a bank glides up the ramp like an escalator — no step, no tread, no weight. The owner's plan: make it read as stair-climbing. |
| The curve | A **smoothed sawtooth added to the bank surface** — C1 cosine treads, zero at both ends of each half-step and at every tread boundary, never below the ramp. |
| Scope | **Banks only.** Sheer faces keep today's behaviour: climbs finish by the cell boundary (hauling onto a ledge), drops keep `HopArc`'s gravity fall. |
| Descent | **Controlled step-down on banks** — level treads with smooth lowerings, the mirror of the climb; gravity remains for sheer edges. |
| Intensity | Plan's numbers to start: **0.5 m treads** (~6 steps a one-layer terrace, split across its two sim steps), lead over the ramp up to 0.3 m. Every number a live tunable. |
| Feet | A **knee lift** for the swing foot while climbing, faded in over the band where the footing planter fades out. |
| Rejected by review | A separate **body bob** (no walk-cycle phase exists anywhere to drive it, and the lift already provides the vertical rhythm) and extra **forward lean** (`Footing` reads only the `GroundRelief` field, which excludes banks; figures are upright on banks today and that has never been reported as a fault). Both recorded as seams (§6). |

## 2. What the review corrected in the plan

The plan's pipeline reading was accurate (verified: `PawnPose.Of` → `StepPace` →
`OnTheDrawnGround`; `HopArc`'s header documents the parabola and the strides). Three things it
got wrong or underspecified, fixed here:

1. **The curve must phase to the climbing half of the step, not to the step.** A bank sits in
   one cell — the foot cell of the terrace — so a hop up climbs over its *first* half and walks
   flat over the second (and a walk onto the bank's foot is flat-then-climb). The plan's
   `s`-keyed sawtooth would have bobbed the figure across the flat approach: the parabola's
   fault again from the other side. `StepPace` now publishes each half's rise and drop, and the
   stair is computed per half (`RiseFirstHalf`/`RiseSecondHalf`/`DropFirstHalf`/
   `DropSecondHalf`).
2. **The banked descent should not be a third code path.** The plan bolted a reverse staircase
   beside `HopArc.Fall`. Instead, a banked arrival with the stair on simply falls through to
   the surface-following branch the climb already uses, with `Hold` added where the half
   descends — one branch, symmetric up and down, and the same-layer walk off a bank's lower
   half gets the stair for free instead of being a special case nobody asked about.
3. **`WalkCyclePhase` does not exist.** The plan's body bob keyed on it; there is no walk-cycle
   phase anywhere in the director (the clips play through a Playables mixer, and `ObserveSpeed`
   deliberately holds across hops). Rather than invent a phase clock for a 3 cm bob on top of a
   curve that already moves the body vertically, the bob is cut — recorded, not lost (§6).

## 3. The curve

`StairGait` (`Assets/Odyssey/Presentation/Rendering/StairGait.cs`) owns the arithmetic, pure
and static in the `Footing`/`BankLayout` tradition — decisions here, transforms in the
director. Two functions:

- **`Lift(rise, u)`** — how far *ahead of the ramp* a climbing figure is drawn: per tread, a
  cosine half-wave rise over the first `MoveShare` (0.4) of the tread, then level, minus the
  linear climb. ≥ 0 always; 0 at u = 0, u = 1 and every tread boundary between. Its steepest
  descent under a hold is exactly the ramp's climb cancelled — the level tread — so
  *surface + lift* never moves downhill on a climb, which is the property
  `TheClimbNeverOnceMovesDownhill` pins.
- **`Hold(drop, u)`** — the mirror for descent: level hold, then a cosine lower over the last
  `MoveShare`, expressed as how far above the descending ramp the figure is carried. ≥ 0
  always; *surface + hold* never moves uphill on a descent.

Treads are `round(rise / TreadHeight)`, so the lead over the ramp is a fixed fraction of one
tread — ≈ **0.62 × TreadHeight**, 0.31 m at the shipped numbers — *whatever the rise*: a taller
bank is more steps, not bigger ones (`TallerBanksGetMoreTreadsNotTallerOnes`). One honest
imperfection, bounded: each cosine opens (the lift) or closes (the hold) with zero slope while
the ramp does not, so for a sliver of each tread the pure curve sits under the linear climb —
up to 8 mm at the lift's start, about 1 mm at the hold's end. The ground clamp in
`OnTheDrawnGround` turns both into landings, and
`NeitherCurveDipsBelowTheRampByMoreThanASole` keeps them a sole's width.

**Why this one gets to stay when two predecessors did not.** The parabola lifted where the
ground did not ("jump a bit"); the strides hold rectangular holds with velocity discontinuities
("four deliberate jolts a climb"). The sawtooth is C1 by construction — the cosine begins and
ends at zero slope and each tread begins where the last ended — and it only ever *adds* to the
ramp, so between treads the drawn position is exactly the glide the owner tuned in 2026-09-19
("motions exactly just above the terrace surface"). Measured against the budgets that caught
the strides: worst per-mille height step ≈ 12 mm climbing, ≈ 24 mm lowering, against
`BankFootingTests`' 50 mm; worst **frame** (per tick of a 240-tick hop) ≈ 38 mm, inside the same
50 mm teleport ceiling (`TheStairsWorstFrameStaysInsideTheTeleportBudget`). What the stair
deliberately gives up is the glide's *variation* bound — `HopArcTests.AClimbIsSmoothFrameToFrameAllTheWayUp`
("a rhythm rather than a steady climb") forbids any stair by design, so that test and
`AClimbIsDrawnOnTheRampAndNowhereElse` now pin the **stair-off** glide and stay exactly as
strict as they were.

**One owner for "is this a stair climb" (P1).** `StairGait.IsClimbing(world, pawn)` answers it
for both `PawnPose` (where the stair draws) and `PawnFigureDirector` (whether a foot may lift),
so the knees can never lift for a glide.

## 4. Where it lands

| Site | Change |
|---|---|
| `PawnPose.StepPace` | publishes each half's rise and drop (four read-only properties; the three heights were already there) |
| `PawnPose.OnTheDrawnGround` | the falling branch lets a banked arrival through to surface-following when the stair is on; the surface branch adds `Lift`/`Hold` per half, ramp crossings only |
| `PawnFigureDirector.Pose` | sets `figure.StairTread` from `StairGait.IsClimbing` — the footing pass's whole gate |
| `PawnFigureDirector.Poses.ApplyFooting` | the swing foot's target is raised by `StairGait.KneeLift`, faded in over exactly the band where `Footing.Correction` fades out (inside half the reach the planter owns the foot; beyond it, the knee comes up for the tread). The hips drop on the corrections alone — a lifting knee is a bending leg, not a sinking body. |

Nothing simulated moves. No `MovementSystem`, `NavGrid`, `CellGrid` or snapshot field is
touched, and a pawn on a bank pays exactly the price it paid last week.

## 5. Tunables (all live, all `StairGait.Reset()`-able)

| Knob | Shipped | What it buys at the keyboard |
|---|---|---|
| `Enabled` | true | the glide back, in one switch — and the A/B every test here compares against |
| `TreadHeight` | 0.5 m | smaller treads = quicker, shallower steps |
| `MoveShare` | 0.4 | the movement fraction of each tread; smaller is snappier, and the rejects are the limit at zero |
| `KneeLiftShare` | 0.5 | how much of a tread the swing foot lifts; 0 kills the knee lift alone |

## 6. Seams left open, on purpose

- **Body bob** — wants a walk-cycle phase clock the director does not have; if the stair
  reading needs more weight shift, the phase source is the thing to build first, and it wants
  building for itself (it would also let footfall audio sync, which nothing can do today
  either).
- **Forward lean on a bank** — `Footing.LeanTo` reads only the `GroundRelief` field, so it
  cannot do this today; a bank-pitch would be a new term beside `CombatBodyPitch` in `Pose`,
  and upright-on-bank has never been reported as a fault. Here, not built, when a playtest
  asks.
- **Sheer faces** — climbing one already "finishes by the boundary", which reads as hauling
  onto a ledge; a stair there would need the `pace.GroundAt` model path to grow the same
  modulation, and nobody has asked.
- **Real stair clips** — the owner's plan offered procedural-now vs clips-later; this is the
  procedural now. The `StairTread` gate and the curve are both cheap to drive from a clip
  weight instead, if a pack ever brings a stair climb.

## 7. Tests

`StairGaitTests` (EditMode, beside `BankFootingTests`, same terrace fixture):

- The curve, directly: zero at the ends and every tread boundary; never negative; amplitude is
  `(1 − MoveShare) × TreadHeight` for every rise; a flat half draws nothing; the knee lift
  fades in over exactly the planting band, monotonic, zero for planted and buried feet.
- The pose, through `PawnPose`, each point compared stair-on against stair-off: never inside
  the ramp and visibly above it on a climb; never downhill on a climb; never uphill on a
  descent and held above the ramp on one; flat walking and the no-world arithmetic fixtures
  bit-for-bit unchanged; both crossings land exactly on the height `Standing` gives.
- The owner of the climb question: the hop up and the step onto a bank's foot are climbs; the
  descent, flat ground, a standing pawn and a missing mirror are not.

Unchanged and load-bearing: `BankFootingTests` (worst-jump smoothness, now with the stair on),
`WalkContinuityTests` (no frame draws a colonist backwards — the stair moves no-one
horizontally), `HopArcTests` (the gravity fall, still the sheer edge's own), `PawnPoseTests`,
`ShorelineWalkTests`, `WalkOnReliefTests`.
