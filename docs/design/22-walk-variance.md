# 22. Walk variance: build, head-look and the sideways bow

*Built 2026-09-18 on `claude/natural-movement`, the second half of the owner's report that
colonists look "a bit square in movement". The first half is `21-diagonal-movement.md`, which is
about the route; this is about the figures on it.*

**Status: built, fast tier and Unity EditMode green. Nobody has pressed Play.** Every number here
is taste and every one is the owner's to overrule — and unlike the diagonal, *none* of it can be
settled by a measurement. `MeasuredLookYaw` says a head moved; it cannot say whether a colonist
looks alive.

---

## 1. What was actually missing

The owner asked for "variation in the stride", "movement of the head looking around when walking"
and "body language". Going in, presentation had **almost none of it**:

| Varies per colonist today | Where |
|---|---|
| Which of 61 bodies, and its four colours | `ColonistLook`, `ColonistAppearance` |
| The gait clip's **start phase** | `PawnFigureDirector.Desynchronise` |
| The work stroke's **period**, ±18% | `WorkStroke.PeriodFor` |

That is the whole list. Every colonist was drawn at exactly `scale 1.4`, took exactly the same
stride, and held its head exactly still. Five colonists on the same errand were five copies of one
walk, in single file, down one line.

**No head bone was bound at all** — `BindWorkBones` bound spine, arms, hands, hips, legs and feet,
and nothing above the shoulders.

## 2. Three dials, and the one that was already planned

| Dial | What it does | Default |
|---|---|---|
| `WalkVariance.Build` | per-colonist size, and therefore stride length | **±3%** (owner, 2026-09-18) |
| `LookAbout` | head and neck turning off the line of travel | ±38° yaw, ±9° pitch |
| `WalkVariance.Bow` | sideways drift off the straight line between cell centres | 0.25 m |

**Per-colonist move *speed* is deliberately not here.** It is `U44` of the rates line
(`17-rates-and-stats.md` §4b), capped at **±15%** because the drawn walk cycle covers about 2 m/s
and anything faster blends the run clip in — and it is blocked on `U42` raising the movement
accumulator to thousandths. This unit does not touch it, does not duplicate it, and will compose
with it: a colonist that is 3% larger and 10% quicker is two independent facts about one person.

Every dial has an off switch that restores the previous behaviour **exactly**, and a test that says
so (`ZeroVarianceIsTheOldBehaviourExactly`, `ZeroBowIsTheOldStraightLineExactly`,
`ZeroedAnglesAreTheOldStillHeadExactly`). That is the owner's escape hatch if any of this reads
wrong on the board.

## 3. Build, and the one line that stops feet skating

A taller colonist takes a longer stride, so the dial is **scale**, applied per figure at lease.
That is the honest lever: playing the same clip faster on the same skeleton is what foot-skating
*is*.

`GroundSpeeds` already folds the catalogue scale into each look's gait speeds — that correction
exists because the cast is drawn at 1.4 and "a scaled figure takes scaled strides". Those speeds
are cached **per look**, not per figure, so per-colonist scale makes the cached table wrong for
everyone by their own few per cent.

A per-figure array is unnecessary. Blending measured speed against `Speeds × k` is identical to
blending `speed / k` against `Speeds`, so it is **one divide** in `Blend`:

```
if (figure.StrideScale > 0.01f) speed /= figure.StrideScale;
```

Leave it out and the blend picks a gait too slow for the speed and then plays it too fast, which is
the skating `GroundSpeeds`' own comment warns about — and it would read as an animation fault
rather than an arithmetic one.

**±3% is about five centimetres** on a 1.8 m figure at the drawn scale. ±6% was offered and
declined: at this camera the tallest and shortest side by side start to read as different ages.

## 4. Head-look: procedural, and why not the clips that exist

The pack **does** ship additive look clips — `A_HeadLook_Additive_Neut` and
`A_BodyLook_Additive_Neut` under `AnimationBaseLocomotion/Animations/Polygon/Neutral/Additive/Look/`
— and they were considered and **not used**.

Using them means an additive layer mixer over the gait, a pose-grid convention nobody here has
written down, and a second animation path to keep in step with the four procedural poses that
already exist. The whole of what they buy is two angles. Two angles are arithmetic: right on all
sixty-one rigs without authoring, testable with no Unity, and one more instance of the pattern
`SwimPose`, `ClimbPose`, `Gesture` and `Footing` already share. `13-gestures.md` §3 draws the line
and this falls on the same side as the axe.

**The neck takes a third and the head the rest**, or the motion reads as a skull swivelling on a
fixed body. **World-space axes** — yaw about world up, pitch about the figure's own right — for the
reason stated at every other pose: sixty-one characters from four packs make no promise about a
bone's local axes, and a colonist on a slope should look level with the world rather than level
with the hill.

**Two sine terms per axis at incommensurable rates**, and the pitch at a different rate again from
the yaw. One term each would trace a line or a circle and read as clockwork; `ItWandersRatherThanSweeping`
pins that by counting turning points.

**It is last in the pose ladder and runs only in the `else`.** Swimming, climbing, a gesture and
work all write the spine or the arms and all of them mean something. Looking about is the pose of a
figure with nothing else to say with its body. The weight **eases** rather than switching, so a
colonist taking up an axe lets its head come back to centre while the swing takes over.

## 5. The bow, and why it needs no walkability query

Colonists walk the exact chord between two cell centres, so five on the same errand walk it in
single file down one line. The bow is a small per-colonist sideways drift off that chord.

**It cannot put a figure inside a wall, and that is arithmetic rather than luck.** A cell is 2.5 m,
so its centre line is 1.25 m from the next cell's far side; a colonist is about half a metre
across. At a 0.25 m cap the figure plus its own width stays half a metre clear of anything solid,
whatever is beside it — so there is no walkability query, no fade near walls and no corridor case.
On a diagonal it is safer still: `21-diagonal-movement.md`'s corner rule will not permit the step
unless **both** flanking cells are open, so all four cells around the chord are clear. Raising the
cap past about 0.6 m would end that argument and want the query.

**Three things about it are load-bearing.**

**The phase is distance, not time.** A clock would drift a colonist sideways while it stood still
working. Distance is also continuous across a step boundary by construction, where anything keyed
to which cell the pawn is in is not.

**It is taken off the eased yaw, never the heading.** The heading is the raw step vector and changes
between one frame and the next at a corner. A quarter of a metre of offset swung through ninety
degrees in one frame is a third of a metre of teleport — an order of magnitude more than the 25 mm
an honest frame of walking carries, and the exact shape of the jolt `WalkOnReliefTests` was written
for. `figure.Yaw` is already eased at `TurnDegreesPerSecond`.

**It is faded out four ways**, each closing a way it could look wrong: with **speed**, because a
colonist standing at a workbench a quarter of a metre off its own cell is just misplaced; with
**turning**, belt and braces over the eased yaw and true besides — nobody wanders while cornering;
with **work**, because `WorkStance.StandAt` has already moved the figure deliberately and two
offsets arguing is how a woodcutter ends up beside its tree instead of at it; and it does not
accumulate while the world is **paused**, or a paused colonist slides sideways on a stale speed.
That last one was found by reading the code rather than by a test, and is recorded here because
nothing on screen would have explained it.

**It is added before the position is written**, so the footing below samples the slope where the
figure actually ends up rather than a quarter of a metre away from it.

Nothing here is in a cell, a save or the hash. The pawn is on its cell throughout — for picking,
for the cursor and for every rule — exactly as a working figure is while `WorkStance.StandAt` steps
it 0.8 m off that cell to swing an axe.

## 6. Streams, not one number taken three ways

Each dial gets its own mixing constant over the pawn id, for the reason `ColonistAppearance` gives
about colour: a shortcut that reuses one hash correlates the slots — everyone who is tall also
bows the same way — and it is invisible until fifty colonists are on screen at once.
`TheDialsAreIndependentOfEachOther` asserts build does not predict bow over 400 ids.

**The world seed is deliberately left out.** Appearance mixes it in so a world deals the same faces
every load; how a person walks is not something a save has an opinion about, and leaving it out
keeps the arithmetic callable from places that have no seed to hand.

## 7. What it costs: nothing measurable

The owner asked for this to be as fast as possible. Per figure per frame it adds two sines for the
bow, four for the head-look, four `Pitch` calls over two bones, and one divide in `Blend` — at the
64-figure ceiling, a few hundred trig calls, which is reasoning rather than a measurement.

**Measured anyway, and the measurement is the interesting part.** `FrameTimeTests` in PlayMode, the
same session, the only change being the dials set to zero (which *is* the old behaviour exactly, by
§2's escape hatches):

| | dials on | dials off |
|---|---|---|
| meadow, mean | 2.31 ms | 2.47 ms |
| city, mean | 3.07 ms | 3.32 ms |

**"Off" came out slower than "on", which is the useful result**: the difference is entirely machine
noise and this work is free at colony scale. Both are inside the 5 ms budget and the assertion
passed either way.

**But do not read those absolute numbers as the game's frame time.** `CLAUDE.md` records meadow
0.99 ms and city 1.56 ms on the same GPU at the same resolution. These runs were taken with **two
editor GUIs, the self-hosted CI runner and another worktree's batch Unity all competing** — one of
the first runs reported a 31 ms worst frame, which is the contention showing. The A/B above is
valid because both arms were equally contended; the absolutes are not comparable to anything, and
the recorded baseline wants re-taking on a quiet machine before anybody trusts a comparison.

## 8. What only the owner can settle

- Whether ±3% reads as different people or as nothing at all.
- Whether a head that wanders ±38° every nine seconds reads as alive or as distracted.
- Whether the bow reads as natural or as drunk — and whether 0.25 m is too much at this camera.
- Whether `LookAbout.CyclesPerSecond` at 0.11 is too slow to notice or already too busy.

The levers are all `public static` properties with `Reset()`, so they can be turned at the keyboard
without a recompile of anything but the harness.
