# 13. Gestures: what a figure does that is not walking and not a tool stroke

*Settled by interview with the owner on 2026-09-16 (three rounds). Sibling to
`12-work-poses-and-tools.md`, which generalised the felling swing into a `WorkStyle` and is the
document this one extends. §6a of `06-rendering-and-camera.md` remains the record of the felling
pose itself.*

**Status, 2026-09-16: designed, then built as far as the climb.** The note was written as design only
and §8 said why; the owner then asked for the work to proceed on the same day, and §9 records what
exists. It builds and its arithmetic is tested. Nothing in it has been photographed yet, which for
a pose is the only evidence that counts.

---

## 1. What was asked, and what was decided

The request was a pick-up motion — down to the ground with the knees bent, up again with the hands
forward — to serve hauling, gardening and harvesting; and the question of whether to build that one
motion or to take the wider set (a two-handed gun with recoil, a ladder climb) at the same time.

The owner's answers, in the order they were given:

| Question | Decision |
|---|---|
| One motion or a vocabulary? | **Widen first.** Establish the kinds of gesture, then land pick-up as the first instance. |
| Which motions are in the queue? | All four: pick-up/set-down, crouched sustained work, ladder and shaft climb, two-handed gun with recoil. |
| How far into the legs? | **Hips, knees and ankles. No ground IK.** A real crouch; feet stay where the gait left them. |
| Where does a pick-up's duration live? | **Presentation only.** The sim stays instantaneous; nothing in the save, the hash or the balance moves. |
| Is the carried item in the hands? | **No, not yet.** Hands stay empty; the held prop is a later attachment point. |
| The gun, with no combat in the game? | **Pose and check harness only.** Nothing in `Play.unity` plays it. |
| Crouched work, with no farming in the game? | **Harness only**, the same bargain. |
| Order | Pick-up, crouched work, climb, gun. |
| Name | **Gesture.** `WorkSwing`/`WorkStroke` keep their meaning; nothing is renamed. |
| Trigger | **A field on `PawnView`**, not a presentation-side guess. |

Two of those decisions are worth their own line because they are the ones that will be questioned
later.

**Hands stay empty, and that makes the lift a mime.** A bend and a rise with nothing appearing in
the fists reads correctly for gardening and harvesting, where there is nothing to hold until the
crop is in the basket, and reads slightly oddly for hauling, where a colonist stands up from a log
pile holding air. The owner took that trade knowingly. The design's obligation is therefore to make
the held prop an *attachment point* rather than a rewrite: the lift's hands already arrive at a
known world point (§4), which is exactly where a prop would be parented, so adding one later is the
same one row in the catalogue and the same parent call the axe already uses.

**Two of the four gestures have no consumer in the game.** Nothing farms and nothing shoots. They
are built because the *kinds* they represent — a sustained cycle that is not a tool stroke, and an
aimed pose that leaves the sagittal plane — are what the vocabulary is for, and a vocabulary
justified by one instance is not a vocabulary. They are exercised by a check harness and by tests,
and no scenario plays them.

---

## 2. What exists today, exactly

`PawnFigureDirector.ApplyWorkPose()` is the whole of the non-gait pose system, and it is two
hard-coded branches:

```
if (WorkWeight <= 0.001)        → if climbing, ApplyClimbPose(figure); continue
else                            → Strike(figure, style.Stroke.At(phase), style.Tilt)
```

Around that sit five things that are already general and should not be rebuilt:

- **`WorkSwing`** — three sagittal angles and a `Scaled`. Pure arithmetic, tested.
- **`WorkStroke`** — the *curve*: two poses, a duration, and where the raise ends and the blow
  lands. Added for mining, because six `const`s on `WorkSwing` meant the build could hold exactly
  one stroke.
- **`WorkStyle`** — a stroke, a tool module id, a chip recipe, a tilt, a grip and a blade roll,
  selected from `PawnView.JobDef`.
- **`ArmIk.Reach`** — the ordinary two-bone analytic solve with a pole target, run after the
  animation is written. It tries both spins and keeps the one that puts the elbow nearer the pole,
  rather than reasoning about handedness.
- **`WorkStance.StandAt`** — where a figure stands so that its measured strike offset ends inside
  the thing it is working on. Drawn place only; the pawn stays in its cell.

And three things that are bound, or rather are not:

```
Spine, RightUpperArm, RightLowerArm, LeftUpperArm, LeftLowerArm, LeftHand, RightHand
```

**No leg is bound, and no pelvis.** `ApplyClimbPose`'s own comment records this as a deliberate
limit rather than an oversight — "legs would be better and are not available without binding four
more bones, which is a piece of work rather than a tweak". This note is that piece of work.

---

## 3. The three kinds, and why the number is three

A gesture is a pose laid additively over the gait clip, the same way the swing is. What separates
one gesture from another is **where its phase comes from** and **what its pose is solved against**.
Those two questions, and not the motion's name, are what the vocabulary has to carve.

| Kind | Phase comes from | Pose solved against | Instances |
|---|---|---|---|
| **Cyclic** | a clock that runs while a state holds | authored angles | felling, mining, crouched work |
| **Tracked** | another quantity's own progress | authored angles | climbing (the step's progress up the shaft) |
| **One-shot** | a timer started by an event, run once, ended | **a world point** | pick-up, set-down |
| **Aimed** | held indefinitely, plus an impulse | a direction, plus a point for the off hand | the gun |

Four rows, three kinds: **tracked is cyclic with a different clock**, which is a field on the
gesture and not a kind of its own. The climb reads its phase from how far up the cell the pawn has
got, so that a colonist half way up a shaft has made half a reach; that is one function pointer
away from a stroke that reads its phase from seconds elapsed.

The useful cut is therefore **authored versus solved**, and it is the single most important
sentence in this note:

> **Author the angles when the figure is aiming at something whose position we do not know.
> Solve to a point when the figure has to meet something whose position we do.**

The axe is the first case and had to be: a stroke sweeps an arc, the contact is momentary, and no
amount of knowing where the trunk is tells you what the shoulder does eight-tenths of a second
earlier. That is why every angle in §6a had to be settled against photographs, why
`MeasuredBladeGap` exists, and why the whole apparatus of contact sheets was built.

The lift is the second case. We know precisely where the hands have to go: the ground in front of
the colonist's own boots, on terrain `GroundRelief` has already tilted, because the item is in the
pawn's own cell by construction (§5). What is solved is therefore the *body* rather than the hands
— the pelvis drops and the legs are solved back to the feet the gait put down — and the result is
right on all sixty-one faces without a photograph. Posed by authored angles instead it would need
one per rig, would be wrong on a slope, and would put a tall colonist's hands through the floor and
a short one's a foot above it.

**This is the thing the gesture system buys that a second `WorkStyle` would not.** `WorkStyle` is a
bundle of authored numbers. Half the queue is not authored numbers.

---

## 4. The crouch, which is the only genuinely new mechanic

Everything else in this note is composition. The crouch is arithmetic that does not exist yet, and
it has one trap in it that decides whether the whole thing works.

**A crouch is two angles and one translation, and the translation is what keeps the boots on the
ground.** Bend a knee by rotating the upper leg forward and the foot swings up off the floor; the
figure ends up treading air with its pelvis where it always was, which reads as sitting on an
invisible stool. The pelvis has to come *down* by however much the leg chain shortens, and the
ankle has to counter-rotate by the sum of the other two so that the sole stays flat.

The owner ruled out ground IK, which means the feet are not solved against the drawn terrain. It
does **not** mean the legs are authored angles, and they should not be:

- **A leg is two bones, and we own a two-bone solver.** `ArmIk.Reach` is generic already — it takes
  an upper, a lower, an end effector, a target and a pole. Solving a leg is the same call with the
  knee's pole in front instead of the elbow's behind. The only honest change is its name.
- **The rigs differ.** Sixty-one characters, each with its own limb proportions, and
  `PawnFigureDirector` already applies a per-look `Scale`. A drop of "0.4 m" authored once is a
  deep squat on one and a curtsey on another. Solved, the same target height is right on all of
  them.
- **Ground IK stays out and stays out cheaply.** The solve targets the foot positions *as the gait
  left them this frame*, not a raycast against the terrain. On a slope the feet are as right or as
  wrong as the walk cycle already was, which is the owner's decision honoured exactly.

So: capture the two foot positions, translate `Hips` down by the crouch depth, solve each leg back
to its captured foot, counter-rotate the ankles. The depth itself is the one authored number, and
it is authored as **a fraction of the figure's own standing hip height**, measured off the rig at
bind time, for the same reason.

Two traps, and neither throws:

- **`Hips` is the humanoid root.** Translating it moves the entire figure through the skeleton,
  which is what we want, but it is written every frame by the animation graph — so the offset is
  applied *after* the graph, in the same pass and under the same rule as every pitch, and is never
  accumulated across frames.
- **The skinned-mesh cache bites here too.** `forceMatrixRecalculationPerRender` is already set for
  the axe; a pelvis written after the animation update without it would move the bones and leave
  the mesh standing. This is in `docs/lessons.md` and is recorded here because a new bone is
  exactly when somebody rediscovers it.

---

## 5. How the simulation says a gesture happened

Picking a stack up takes no time in the simulation and the owner has ruled that it stays that way.
So there is no state for presentation to observe — `PawnView.Working` is a *sustained* bit, and a
lift is an *event*. The owner chose to put a field on the contract rather than have presentation
guess from snapshot deltas.

### The shape

```
enum PawnGesture : byte { None, Working, Lift, Stow, Climb }   // Sim.Contracts

PawnView.Gesture : PawnGesture   // what the pawn last did, sticky
PawnView.GestureSerial : byte    // bumped each time a gesture starts, wraps
```

**No cell is carried with it, and that was a mistake in the first draft of this note.** It proposed
reusing `WorkCell` to give the lift a direction to face. There is nothing to face: the haul's
pickup toil *fails* unless the item is in the pawn's own cell, and the drop happens at the cell the
pawn has just walked to. Both ends of the motion are therefore at the colonist's own feet, so the
gesture needs no target beyond the figure's own position and the hands reach down in front of its
boots. The same is true of the item's drawn place — asking `ItemHeap` where it scattered the pile
would couple the pose to the pile's own decoration for a difference of a few centimetres.

### Why a serial, and why sticky — this is the trap

**A one-tick flag is unobservable.** Presentation reads the latest snapshot once a frame. At speed
three the simulation runs several ticks between frames, so a `Gesture = Lift` set for the single
tick the item changes hands will, routinely, never be seen by anything. The pose would fire on slow
speeds, not fire on fast ones, and the bug would look like a rendering glitch.

The fix is that both fields are **sticky**: a pawn keeps reporting the last gesture it began and
the serial that began it, until the next one. Presentation fires when the serial it sees differs
from the serial it last recorded for that pawn — which is correct whether it missed no snapshots,
one, or forty. A byte wraps after 256 and that is harmless, because the test is *different*, not
*greater*.

Two consequences to write down before they are found the hard way:

- **A figure with no recorded serial must not fire.** Figures are built lazily, and a colonist who
  walks on screen, or a game that has just loaded, would otherwise play one spurious lift on its
  first frame. First sighting records the serial and poses nothing.
- **Neither field is saved and neither is hashed.** They are a report about what just happened, not
  state the simulation reasons from, and hashing them would make the look of the game part of its
  determinism contract. This is the same position `Working` already occupies and it must be taken
  deliberately: adding a field to `Pawn` that *is* hashed is a save-format change, and this one
  must not be one.

### What this does and does not cost the simulation

One enum, two bytes on a view, and one assignment in the haul driver where the item changes hands.
No ticks, no work, no throughput change, no golden moves. The whole of the duration lives in
presentation, where a lift takes about eight-tenths of a second of game time — **game** time, so
that it scales with the speed buttons and freezes with the pause, exactly as the swing does by
inferring the tick standing still.

The figure will therefore sometimes still be rising as its pawn sets off walking. That is accepted:
the alternative is either a sim-side duration (rejected, it is a balance change) or a figure that
lags its own cell (rejected, it desynchronises the drawn body from the thing the player clicks).

---

## 6. The composition, in code shape

No second director. `ApplyWorkPose`'s two branches become one table and one loop:

```
readonly struct Gesture                       // Presentation/World
    Kind        : GestureKind                 // Cyclic | OneShot | Aimed
    Clock       : GestureClock                // Seconds | TrackedProgress | OnceOverSeconds
    Seconds     : float                       // duration, or period
    Pose(phase, figure) : GesturePose         // authored angles and/or solve targets
    static Lift, static Stow, static Tend, static Climb, static Aim

readonly struct GesturePose
    Swing       : WorkSwing                   // the existing three angles, unchanged
    Crouch      : float                       // fraction of standing hip height, 0 = upright
    HandTarget  : Vector3?                    // solved, not authored, when present
    Recoil      : float
```

`WorkStyle` stays exactly what it is and keeps the tool strokes. A gesture is the wider thing: a
tool stroke is the cyclic gesture that happens to hold a prop, and `WorkStyle` is how it is
configured. Nothing existing is renamed and nothing existing changes meaning — which was the
owner's stated condition on the name.

**One invariant that must survive, and that a careless gesture would break.** `ApplyWorkPose` is
called at the end of *both* `Sync` and `Evaluate`, and the existing comment says this is "harmless
in the other: a second pass simply re-derives the same angles". That harmlessness rests on two
things at once: the pose is a pure function of state, and `Pitch` is *additive over the bone's
current rotation*, so two applications without an animation evaluation between them do not
re-derive an angle, they **double** it. Therefore:

> A gesture's clock is advanced in `Evaluate` and nowhere else. `ApplyWorkPose` reads phase and
> never moves it.

A one-shot gesture is precisely the thing that would be tempted to advance a timer inside the pose
function, and it would then run at double speed under the player loop and single speed in the
editor harness — a discrepancy that the check shots, which step the graph by hand, would not show.

**Amended in the build (2026-09-16): the clock advances in `Pose`, not in `Evaluate`.** The rule
above is the one that matters and it is unchanged — advance in exactly one place, and leave
`ApplyWorkPose` a pure re-derivation — but the place the codebase had already chosen is `Pose`,
which is where `SwingClock` has always been advanced, and which runs once per `Sync`. Putting the
gesture clock anywhere else would have made two clocks tick in two different passes.

There is a second thing resting on that same assumption and it is worth naming, because a
translation is more alarming to get wrong than a rotation. `Pitch` *adds* to a bone's current
rotation and the crouch *adds* to the pelvis's current position, so both would double if the pass
ran twice with no animation write between. It does not: were that untrue, the axe swing would have
been drawing at twice its angles since the day it landed. A doubled crouch, though, would put a
colonist's knees through the floor rather than merely overacting.

---

## 7. What to test, and where

The pattern from §6 of `12-work-poses-and-tools.md` holds: the arithmetic is testable, the
appearance is not, and the temptation to treat the first as though it proved the second is the
whole reason `MeasuredBladeGap` was written.

### Fast tier — no Unity

- `PawnGesture` round-trips through the snapshot publish; `GestureSerial` advances once per gesture
  start and not once per tick.
- A colony that hauls produces the same state hash with the gesture fields present as without —
  the proof that they are a report and not state.
- Save/load across a lift: the fields are absent from the file and a loaded pawn fires nothing.

### Unity EditMode gate

- **`TwoBoneIkTests`** on the generalised solver: reachable target hit within a millimetre,
  over-long target straightens and stops, missing bone leaves the pose untouched, pole chooses the
  near side. Mostly moved rather than written — `ArmIk` has these already and they should not stay
  named after an arm.
- **`CrouchTests`, the one that matters.** Across the full depth range and a spread of limb
  proportions, each foot ends within a centimetre of where it started. This is the test that
  catches the figure treading air, it needs no rig and no Synty, and it is the only automatable
  statement about whether a crouch is a crouch.
- **`GestureTests`**: each static gesture is monotone over [0, 1] where it claims to be; a one-shot
  reports itself finished exactly once; a tracked clock at half progress is at half phase.
- **`MeasuredHandGap`**, the lift's answer to `MeasuredBladeGap`: how far the fists finished from
  the point they were sent to, on the frame they should have arrived. A lift that misses by thirty
  centimetres is a mime of a mime, and no three-quarter photograph will settle it.
- `GestureSerial` handling in the director: a figure seen for the first time poses nothing; a
  serial that wraps past zero still fires.

### Not automatable, and the note would be dishonest not to say so

Whether a lift looks like lifting. `GestureCheck` — one editor entry per gesture under
`Odyssey → Presentation`, following `SwingCheck` — shoots the key frames **side on**, for the
reason §6a gives twice over. The owner judges; the constants are the crouch depth, the durations,
the hand targets' height above the item and the arc the hands take between them.

---

## 8. Why no code shipped with the first draft of this note

*Superseded on the same day: the owner asked for the work to proceed, the blocker below cleared
itself, and §9 now records what was built. Kept because the second paragraph is still live.*

The phase rule: design, hard stop, approval, then build. But there is a second reason, found while
checking the facts for §2, and it is a blocker rather than a formality.

**`WorkStyle`, `WorkStroke`, the climb pose and `ItemHeap` are uncommitted.** They exist as
modified and staged files in the working tree of `D:\code\odyssey` on `claude/mines-merge`, and not
in any commit — `304c0bd`, that branch's own head, does not contain them. This note is designed
against them, read-only, and every reference above to what "exists today" means *exists in that
working tree*. Nothing can be branched from them, and code written against them would be a second
source of truth for as long as the two lines run apart, which is the exact failure
`12-work-poses-and-tools.md` §7 declined to commit.

Related, and worth an owner decision on its own: **`12-work-poses-and-tools.md` is not in this
branch either.** Five places in the shipping code cite it by name — `PawnFigureDirector`,
`ModuleCatalogue`, `WorkSwingTests`, `PlayScene` and `mining-interview.md` — and the file lives
only on the unmerged `worktree-agent-a8e0c6ab1138f2fda`. It has not been copied here deliberately,
to avoid creating the duplicate this paragraph warns about; the mining merge should carry it. Note
also that `mining-interview.md` records at least one of its claims as falsified by what was
actually built ("the design note was wrong about this"), so it wants a dated banner when it lands,
not a silent restoration.

---

## 9. The plan, in the order that keeps each step judgeable

Steps G1 and G2 are refactors that change no picture; G3 upward each end in something the owner can
look at.

| # | Step | State |
|---|---|---|
| **G0** | Land the mining merge. | **Done** by the owner as `482ff0f` while this note was being written. `12-work-poses-and-tools.md` did *not* come with it and is still on `worktree-agent-a8e0c6ab1138f2fda` alone, cited by five shipping files. |
| **GH** | **The builder's hammer.** Added ahead of the queue because the owner asked for it and because it is pure data on machinery that already exists: a stroke, a style, a chip recipe, a catalogue row. | **Built, EditMode green, not yet photographed.** |
| **G1** | Bind `Hips`, both legs, both feet; measure each figure's own standing hip height. Rename `ArmIk` to `TwoBoneIk` — a leg is two bones. | **Built, EditMode green.** |
| **G2** | The crouch: pelvis translation, leg solve to the feet the gait left, sole restored. | **Built, EditMode green.** |
| **G3** | `Gesture` as a value with its own tests; the dispatch in `ApplyWorkPose` grows a third arm. Felling, mining and climbing are untouched. | **Built, EditMode green.** |
| **G4** | The contract: `PawnGesture`, the sticky pair on `PawnView`, the haul driver's two assignments, the first-sighting rule. | **Done and verified** — 7 new tests, 409 green in the fast tier. |
| **G5** | **The lift**, plus `GestureCheck` and `MeasuredCrouchDrop`. | **Built, EditMode green. Not yet photographed — wants the owner's eye.** |
| **G6** | Crouched sustained work. Cyclic, harness only, no job. | Not started. |
| **G7** | Legs on the climb, now that they are bound. | **Built, EditMode green, photographed.** `ClimbPose`, `ClimbCheck`, `MeasuredFootReach`. See §11. |
| **G8** | The gun: aimed hold, off hand on the foregrip, recoil over the hold. Harness only. | Not started. |

**What is verified, and what is not.** The fast tier is 409 Sim and 29 Hud; EditMode is **639 total,
629 passed, 0 failed**, run in the worktree, and the new work is 8 `GesturePoseTests`, 6
`TwoBoneIkTests` (renamed, not rewritten), 6 on the hammer and 7 on the contract. So it builds and
the arithmetic is self-consistent.

**None of that says whether it looks like anything**, and the temptation to treat it as though it
did is the whole reason `MeasuredBladeGap` was written and now `MeasuredCrouchDrop` beside it.
`GestureCheck` is the loop that closes the gap:

```
Odyssey → Presentation → Check the lift          # and Check the set-down
Odyssey → Presentation → Check the hammer swing  # beside the axe's and the pick's
```

A worktree has no `Assets/Synty` — the packs are gitignored and belong only to the real checkout —
so no figure can be drawn in one and the sheets have to be shot where the art is. (Copying the
1.5 GB in is a legitimate one-off; a junction is not, because the worktree's editor would then write
`.meta` files into the owner's own checkout while their editor has it open.)

---

## 10. Open questions

- **Does a lift need interrupting?** A colonist whose job is cancelled mid-lift will finish the
  rise, because the gesture is presentation-owned and the snapshot no longer has anything to say
  about it. Probably right; cheap to change if a screenshot disagrees.
- **When the held item lands, does it attach mid-lift or at the top?** The owner deferred the prop.
  The attachment point is the hand target, so the question is only timing.
- **Should gardening reuse the lift, or is a harvest its own gesture?** Picking a crop is a lift
  that ends with the hand at chest height rather than a carry; it may be one parameter.
- **Does the gun want a muzzle flash, and is that `ChipDirector`?** The recipe machinery is close
  to right — one system, per-emission parameters — but a flash is a light as much as a particle.
  Defer until combat asks.
- **Does anything ever need two gestures at once beyond the gun's hold-plus-recoil?** Carrying
  while climbing is the obvious candidate and there is no answer yet. The poses are additive, so
  the arithmetic permits it; legibility is the constraint, not the maths.

---

## 11. G7, the climb's legs, and the two things the plan did not foresee

*Built 2026-09-16. `ClimbPose`, `ClimbPoseTests`, `PawnFigureDirector.ApplyClimbLegs`, `ClimbCheck`.*

`ApplyClimbPose` posed two arms and nothing else, and its own comment said why: no leg was bound.
They are now, so this is the piece of work that comment named. Underneath the arms the gait mixer
was playing the **idle** — a purely vertical step has no ground speed — so a colonist went up a
shaft hauling on the wall with its boots together, standing to attention.

The pose follows §3's rule and is **solved, not authored**: the figure is turned to face its rock
and leaned against it before any of this runs, so the wall is a known plane in front of the hips and
the only question left is how far down it each boot goes. It is **contralateral** — the right hand
reaches with the left foot — which is one subtraction and is the whole of what separates a climb
from a frog's bound. Every number is a fraction of the figure's own thigh-plus-shin, measured at
bind time beside the hip height the crouch already measured, because a foothold given in metres is a
stride on one of the sixty-one characters and a stumble on the next.

Two things the design did not foresee, both found by looking rather than by reasoning.

**A wall is a plane, and the first version made it a cone.** The step was written as a reach and an
angle, shorter and further forward as the knee came up, which is how a leg feels and is not how a
wall is: the two boots finished at different distances from the rock, one of them some thirty
centimetres inside it. Neither a test of reach nor the contact sheet — which photographs a figure on
a face of clear air — would ever have said so. The fix is that the rock distance is **held fixed and
only the height varies**, and it is `ClimbLean`'s own arithmetic that supplies it (half a cell, less
the lean the body has already taken) rather than a second constant that would drift out of step. Of
the two compromises, the boot keeps the wall and the drop gives: a boot that has come up too high is
a small step, and a boot off the wall is the levitating colonist the lean was written to fix.

**The first numbers made a chair.** A drop of 0.46 of a leg with the wall 0.3 m away folds the knee
so far that the thigh comes up past horizontal, and against no wall at all it reads unmistakably as
sitting down — the same failure §6 warns about for the crouch, arrived at from the other direction.
0.92 and 0.68 read as a climber. That is taste and it is the owner's to overrule; it is recorded
because the two numbers are the sheet's only levers.

**The sole is left as the gait wrote it**, restored after the solve exactly as the crouch restores
it. A foot flat on a vertical face wants its toes pointing up, and which rotation that is depends on
the rig's own convention for a foot bone — settleable only by photographing sixty-one characters.
What the gait leaves is toes forward, and the figure faces the wall, so the boots address the rock
toes-first: edging, which is a real way to stand on rock rather than a placeholder pretending to be
one.

**Judged by `Odyssey → Presentation → Check the climb`** (`scripts/unity.sh shot
Odyssey.EditorTools.ClimbCheck.Run`), which forces the climb the way `GestureCheck` forces a lift
and walks the cycle by hand — a real one happens where a shaft has been dug, which on a wooded board
is nowhere, and is over in under a second when it does. Two things about that sheet are worth
knowing before reading it. It shoots at **facing + 90°** where `GestureCheck` shoots at the facing
itself, and that is not a disagreement: there the figure's facing is whatever the walk left and the
Synty mesh's own ninety-degree turn inside the prefab cancels the correction, where here the yaw is
driven straight off the forced wall direction. And **the boots stand in the grass**, because a
forced climber is a colonist on a meadow rather than a colonist half way between two layers — read
each boot against the hip, not against the ground. `MeasuredFootReach` is printed beside every
picture for the reason `MeasuredBladeGap` is: `TwoBoneIk` straightens towards a hold it cannot reach
and stops, which in a photograph is indistinguishable from a leg that arrived.

**What is not done.** The climb still has no hand *grip* — the fists are open and `Grasp` is now
exactly the machinery that would close them on a hold, which is the argument for doing it and the
reason it is listed rather than done. Nothing else in §9 moved: G6 and G8 are still open, and still
harness-only.
