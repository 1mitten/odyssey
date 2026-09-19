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
