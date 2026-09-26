# 24 — Carrying

**Status:** built, played once, three faults found and fixed (§4a-bis, §6a-bis, §6b), owner happy
to merge. Numbers marked *proposed* remain invited tuning.
**Owner interview:** 2026-09-19. **Branch:** `claude/carried-items`, PR #129.
**Measured after the fixes:** fast tier **736 Sim + 445 Hud**; EditMode **1834 total, 1820 passed,
0 failed**; PlayMode **82 total, 77 passed, 0 failed**. Both content gates pass with no CSV change
— this adds no named thing.

---

## 1. The report

> "Currently when a colonist picks up an object it disappears and they walk off. Are we able to
> say when carrying rocks/branches etc. Bending down to pick them up would motion the object into
> their hands and arms to hold when walking along with the good — it hands and arms is scooped
> underneath the object as if they were holding it and to keep the object moving along with the
> colonist in their arms — so it never disappears."

Two of the three beats the owner describes already exist. The bend exists, and it costs time. What
is missing is the middle one: the load itself, from the instant it leaves the ground to the instant
it reaches the pile.

## 2. What is already here

This matters because the obvious reading of the symptom — "nothing about carrying is built" — is
wrong, and a session that believed it would rewrite the stoop.

| Beat | State |
|---|---|
| Walk to the thing | Built |
| **Bend, grasp, straighten** | **Built, and timed.** `JobDriver.LiftToil` (`Job.cs:326`) is a whole toil of `PawnContent.LiftTicks` = 48 ticks (0.8 s). `PawnGesture.Lift` fires on its first tick; presentation draws a solved crouch a third of hip height deep (`Gesture.Lift`) |
| **The transfer** | **Built.** `LiftGraspTicks` = 24, the middle of the gesture's floor hold (phase 0.4–0.55, ticks 19–26). The pile shrinks while the hands are on it |
| Carry it | **The gap.** `ColonyItems.PickUp` sets `item.Cell = -1` and delists it, so it leaves the view feed entirely and nothing downstream could draw it |
| Set it down | Half built. `PawnGesture.Stow` exists and is drawn; the item still teleports to the floor |

So the simulation is finished and correct. **Everything in this document is presentation**, with
two small exceptions named in §7.

`LiftTicks` earned itself: the pickup was one tick until 2026-09-17 (owner: *"there should be time
spent motion down, picking up object and standing up"*), and a colonist set off walking while its
figure was still straightening. The golden runs moved a fifth time to pay for it. Nothing here
should disturb that number.

## 3. What the owner decided

| Question | Decision |
|---|---|
| What is drawn | **The real ground prop.** A log is the wood bundle; stone is the same small boulders `ItemHeap` scatters on the floor |
| Whether the armful shows the amount | **No.** Always the same full armful, whatever the stack |
| The hold | **Both arms at waist, forearms underneath, scooped** |
| The walk | Arms locked to the carry, **plus a slight backward spine lean**. No change to move rate |
| The tool | **Gone while carrying** |
| Arriving at a stockpile | **Stow crouch**, load lowers with the hands and becomes the ground heap |
| Arriving at a build site | **Absorbed** into the site |
| An interrupted haul | **Reuse the stow crouch** — set it down and walk off |
| Scale | **True scale**, identical to the ground prop |
| In water | Carried as normal, half-submerged — no special case |
| The amount, instead | **In the activity line** |
| Delivery | Design doc, then build, one PR |

### 3a. Why the amount left the arms

The ground heap says how much by **count** — `ItemHeap` draws two rocks for a small stack and seven
for a full one, and that decision was the owner's (*"a bigger pile should look bigger"*). The arms
deliberately do not copy it. Two reasons, and they pull the same way.

A 75-stack of stone cannot be held in two arms at true scale, so any count that scaled would have
to cap somewhere well below seven — and the range left, one to three, is not a range a player reads
at the play camera. Worse is the bottom of it: a colonist walking forty metres holding **one
pebble** looks like a bug, not like a small load.

So the armful is constant and the number moved to words. That is a real trade and it is worth
naming: **after this change, nothing on the board tells you how much a colonist is carrying.** You
must select them. If that proves wrong in play the cheapest fix is not a scaling armful but a count
on the activity line, which is where it now lives anyway.

## 4. The carry pose

### 4a. Authored angles, measured cradle — and the draft had this backwards

`13-gestures.md` §3 draws the line: author the angles when the figure aims at something whose
position the arithmetic does not know; solve to a point when it must meet something it does. The
first draft of this document put the carry on the solved side — compute a cradle from hip height,
drive both hands to it with `Grasp`/`TwoBoneIk` — and that is wrong, for a reason the line does
not cover.

**Arm length varies across the sixty-one rigs by more than the cradle does.** A solved point is
the same place on every body, so a short-armed colonist reaches it at full stretch with its elbows
flared while a long-armed one holds it folded against its chest. Two colonists then carry the same
log in visibly different postures. Authoring the shoulder and the elbow gives every rig the same
*posture* — which is the thing a viewer actually reads at this camera — and lets the load sit
wherever that rig's arms genuinely are.

So: the angles are authored, the palms are then asked where they ended up, and the load's base
goes at their midpoint. `HandGrip.Palm` supplies the palms, because a humanoid hand bone is the
wrist and a load seated on two wrists sits a hand's breadth behind where the arms hold it — the
same measurement the tool grip already needed.

**One thing is still solved**, along the one axis where an authored angle can fail outright rather
than merely look wrong: `Clearance`. A rig whose arms are short enough to bring the load inside its
own chest gets it pushed forward until it is clear. A wood bundle drawn through a colonist's ribs
is not a pose that wants tuning.

### 4a-bis. Stiff, because it has taken weight

**Owner, 2026-09-19, on the first build:** *"arms and hands should be much stiffer and static held
under the item rather than motioned because it's taken weight it's holding, so it's mostly static
with little movement."*

The first version was additive — `Pitch` adds a world-space rotation to whatever the walk clip put
on the bone, which is right for a gesture laid over a gait and **wrong for a stance**. Added to a
swinging arm, a scoop is a scoop that swings; and because the load follows the palms, the load
swung with it. The load was doing exactly what the arms were doing, which is why the fault read as
being about the load.

So the carry takes the four arm bones **off the clip first**, back to the rest the rig itself was
authored in, and builds the scoop from there. What is left moving is the torso carrying them,
which is what a person holding a weight in front of them looks like.

The rest pose cannot be a constant — sixty-one rigs have sixty-one bind poses — so it is read off
each rig once in `BindWorkBones`, before any clip has been evaluated against it, and never again.

**It is written, not blended, and that is what makes it safe.** `ApplyWorkPose` runs at the end of
both `Sync` and `Evaluate` and only one of them re-evaluates the graph first (§4c), so anything
that eased *towards* a rest from wherever the bone happened to be would be integrated rather than
recomputed. Assigning a constant is the identity on the second pass. The ease therefore lives
entirely in the angles.

### 4b. Everything is a fraction or an angle, never a metre

The packs differ in proportion and the director scales them besides. So, as with `Gesture.Depth`
and every constant in `SwimPose` — all of these live in `CarryPose` and are settable, so a harness
can sweep them for a contact sheet without a recompile:

| Number | *Proposed* | Reasoning |
|---|---|---|
| `ElbowBend` | **−75°** | Forearms up towards horizontal. Not 90°: a right angle is a waiter's tray, load balanced rather than held. Fifteen under it puts the hands slightly above the elbows, which is what stops a load sliding off the front and what makes the arms read as *under* it |
| `ShoulderPitch` | **−15°** | The upper arms hang, barely forward. The weight of a scooped load is on the forearms, not the shoulders; past about thirty the colonist is presenting the thing to somebody |
| `SpineLean` | **6°** backward | The counter-lean of somebody carrying weight in front. Far less than life, because the play camera looks down at 48°: the lean foreshortens to almost nothing while the same angle on a walking figure reads as falling over backwards |
| `Clearance` | **0.50 × shoulder width** | The fault guard of §4a, not a taste. Half a shoulder width clears a barrel-chested rig with a little to spare |
| `EaseSeconds` | **0.20 s** | How long the arms take to fold in and let go. The order of `ClimbEaseSeconds`, so nothing about the figure arrives on one frame — it matters most coming out of the lift |
| `HideAfloatAbove` | **0.5** | Where the load stops being drawn in water. A placeholder, §5c |
| `ItemHeap.ArmfulRocks` | **3** | Rocks in an armful, constant. §3a |
| `ItemHeap.ArmfulSpread` | **0.17 m** | Tighter than the floor heap's 0.55–0.66, or the rocks orbit her |
| `ItemHeap.ArmfulStagger` | **0.055 m** | Each rock above the last, so an armful is a heap held rather than three rocks on an invisible shelf |

**The load's base sits on the cradle point, not its centre.** Every item prop has its origin on the
floor — that is how `ItemHeap` places a rock at a cell's floor centre and has it lie on the ground
— so a matrix built at the palms puts the bottom of the load on them. Placing by centre buries half
a wood bundle in the forearms.

### 4c. The rule this pose must not break

From `PawnFigureDirector.Tools.cs`, written after an axe spun for a day:

> **A pose may add to a bone, because the graph rewrites bones; it may never add to anything the
> graph does not own.**

`ApplyWorkPose` runs at the end of both `Sync` and `Evaluate`, and only one of those re-evaluates
the animation graph first — so any pose that *integrates* rather than *recomputes* winds up twice
per frame, in the game only, invisible to a contact sheet. **The carried load is a prop and the
graph has never heard of it.** Its placement must therefore be computed absolutely from the bones
every frame, never nudged from where it was. Running the pass twice must do nothing the second
time.

### 4d. Pose order

The poses write the same bones and are applied in one order, for ever
(`PawnFigureDirector.Poses.cs`). The carry slots in as follows:

1. **Sleep** — wins over everything. A sleeping colonist is not carrying anything visibly; the load
   was stowed before the bed (§6c).
2. **Swim** — wins over the carry's arms. See §5c, which is the one decision in this document that
   is likely to be reversed after a playtest.
3. **Climb** — wins. A hauler cannot climb a ladder today (a known gap), so this is a hook.
4. **Work stroke** — cannot coincide: a colonist swinging an axe has put its load down.
5. **Carry** — here.
6. **Gesture** (lift, stow) — the carry is *blended out* across the gesture rather than overridden
   by it, because the load has to travel with the hands. See §6.

## 5. Drawing the load

### 5a. The seam

`ChunkRenderer.RenderThings` walks `snapshot.Things`, groups by def into a per-frame accumulator via
`AppendItem(def, placement)`, and submits **one instanced batch per item kind** at the end
(`ChunkRenderer.cs:818`). Rubble goes through `ItemHeap.Place` into `_heapPlacements` first.

A carried load should join that same accumulator. That gives it, free:

- the same mesh, the same material, the same lighting and the same size as the ground prop, which
  is exactly the "true scale" decision, enforced by construction rather than by a matching constant;
- **no extra draw call** — a carried rock is one more matrix in a buffer that was going to be
  submitted anyway;
- the stand-in marker for missing art, so a new item with no prop fails visibly rather than
  invisibly.

The recommended shape is a small `CarryRig` that reads a posed figure's two forearms, produces the
cradle transform, and publishes a placement the renderer appends before its submit loop. Pure
arithmetic over transforms, so it is checkable by test rather than by looking — the bargain
`Footing`, `WorkSwing`, `WaterLine` and `Gesture` all make.

**Not a GameObject parented to the hand**, which is how the axe is done. The tool is a GameObject
because the grasp IK has to solve *to* it and it must persist between frames; the load is solved
*from* the arms and has no state, so pooling one prefab per figure per item kind would be
machinery bought for nothing.

### 5b. Telling presentation what is being carried

`PawnView` does not carry the load, and should not be widened for it. `PawnAspect` exists for
exactly this — a sparse, int-valued, feature-owned publication, and its own documentation offers
`"odyssey.pawn.carrying"` as the example. Two keys:

| Key | Value |
|---|---|
| `odyssey.pawn.carrying` | the item def index |
| `odyssey.pawn.carrying.stack` | how many |

Sparse costs nothing when nobody is carrying anything, which is most pawns most of the time. Not
saved and not hashed — the state that belongs in the save is `Job.CarriedItem`, which already is.

The stack is published even though the arms ignore it, because the activity line needs it (§8).

### 5c. Water — the premise was wrong, and the decision needs re-taking

The interview asked what happens when a colonist **swims** with a load, and the answer was "carried
as normal, half-submerged". Checking afterwards, the question was built on a false premise in one
direction and an understated one in the other.

**Deep water is impassable** (`NaturalContent.TerrainDeepWater`) — the pathfinder routes round a
lake rather than pricing a swim nobody survives. So no hauler ever crosses deep water.

But **shallow water floats the figure anyway**. `WaterLine.Weight` takes the swim weight to 1 in
wadeable water, blended across the step, because of the owner's own 2026-09-17 decision: *"it's
meant to float when this shallow if possible"*, *"float is how it looks; shallow stays crossable"*.
And `SwimPose` strokes **both arms**.

So the case is not hypothetical — it is the common one. A colonist hauling wood across a stream
will stroke both arms while holding a bundle in them, and the load will swing about with the
hands. Taking the owner's answer literally (no special case, the load rides the hands) produces
that.

**Decision (owner, 2026-09-19, once the premise was corrected):** *"Make the item disappear for now
when swimming for ease and decide later."* So the load is hidden above `CarryPose.HideAfloatAbove`
= 0.5 swim weight, and `CarryPose.Drawn` is the single place that decides, so a grep for the name
finds every caller on the day it is settled properly.

It is a hard cut and it will pop mid-step, which is the "for ease" part. The two real answers,
both deferred:

- hold the load clear of the water with one arm and stroke with the other — a second solved pose;
- suppress the float while carrying — reverses a decision the owner already took on its own merits.

Note what hiding does **not** do: the simulation is untouched, so the load is still in her arms and
still arrives. This is a drawing decision only, which is the same bargain `WaterLine` itself makes.

## 6. The three beats, end to end

### 6a. Picking up — 48 ticks, 0.8 s

| Tick | Sim | Drawn |
|---|---|---|
| 0 | `LiftToil` begins, `PawnGesture.Lift` fires | Crouch starts. Held tool already hidden — `ShowHeldTool` activates a tool only while `working` |
| 0–19 | thing still on the ground | figure descending, hands falling toward the floor. **The pile is still there**, and `PawnTests` asserts it |
| 19–26 | — | hands held at the floor |
| **24** | **`Items.PickUp`** — cell goes to −1, delisted | **the load leaves the ground list and appears in the hands, at the hands' current position.** No jump: the hands are *at the floor* on this tick, which is the whole reason the grasp is 24 |
| 26–48 | carried | the rise. The load travels up with the hands, and the arms ease from the gesture into the cradle over `EaseSeconds` |
| 48 | `NextToil` | figure upright, load at the waist, walking |

The grasp instant already exists and is already defended by a test. This design's only requirement
of it is that the load's placement follows the *hands* rather than the cradle until the gesture
finishes — otherwise the object teleports to waist height while the colonist is still bent double,
which is the magic-acquisition fault in a new costume.

### 6a-bis. The two hand-overs — and the snap the draft said would not happen

**Owner, 2026-09-19:** *"There needs to be a motion when you pickup and drop — instead of snapping
to position it should fall it drop into position, and also be picked/raised out of pick up."*

The draft asserted in §6a that the grasp instant gave a continuous pickup for free, because the
hands are at the floor on that tick. That is true **vertically** and misses the rest: the pile is
at the middle of the cell and the palms are a third of a metre in front of the colonist, so the
load still crossed that gap in no time at all. The drop was worse and the draft did not consider
it — `PutDown` moves the item to its cell in the same instant it begins the stow, so the thing
appeared at the cell centre on the frame the hands let go and the crouch then played over empty
hands.

`CarryHandover` draws both. It is **drawing and nothing else**: the thing is in the hands, or in
the cell, on the tick the simulation says so, exactly as before, and this governs only where it is
drawn for the fraction of a second either side — the same bargain `WaterLine` makes for the float.

| | Seconds | Curve | Why |
|---|---|---|---|
| **Raise** | 0.30 | eased **out** — quick off the floor, slowing into the cradle | A thing lifted by somebody straightening up, whose hands are fastest in the middle of the rise. Capped well under 0.4 s, which is what is left of the crouch after the grasp: longer and the load is still travelling after she has set off walking, which is the fault `LiftTicks` was introduced to fix |
| **Fall** | 0.36 | eased **in** — slow out of the hands, quickest at the floor | A load really is falling. Longer than the raise, because a load is lowered under control and released where it is taken up in one movement — and `Gesture.Stow` is the slower crouch for the same reason |

Reverse the two curves and a colonist appears to place something delicately and then snatch it off
the floor.

**The fall needs the item's id**, which is why there is a third aspect
(`odyssey.pawn.carrying.thing`). The moment a load is set down it stops being a colonist's load
and becomes an item in a cell, drawn from an entirely different list by a renderer that has no
idea whose hands it was in. The def and the stack cannot make the match — a stockpile of wood is
full of loads that agree on both.

**The whole heap falls as one**, not rock by rock: a settling offset is applied to the cell, so
the cluster keeps its shape and lands together. Per-rock would be a load coming apart in mid-air.

### 6b. Carrying

The cradle is derived from the arms each frame, so the load bobs with the walk, tilts on the
terrace relief and sinks in the stream for free.

**It does not turn for free, and the first build did not turn at all** (owner, 2026-09-19: *"when
you turn a direction the logs don't turn with you and they should"*). The cause was one line: the
yaw came from `ChunkRenderer.FacingOf`, which is the renderer's memory of the last heading it drew
a **stand-in** at — and the colonist loop `continue`s past anyone who has a live figure, so it
never records one for them. Every load on every real colonist was drawn at a yaw of exactly
nought, and a log pointed north for ever.

The yaw now comes off the figure (`Figure.CarryYaw`), which is the thing that actually knows. Two
layers had the same fault: an armful of rubble is laid out on the world axes by `ItemHeap`, so it
is turned **about the cradle as one cluster** rather than each rock about itself — spinning them
in place would keep the shape and leave the shape pointing north, which is the identical bug one
level down.

### 6c. Putting down

| Case | What happens |
|---|---|
| **Into a stockpile** | `PutDown` already fires `PawnGesture.Stow`. The load lowers with the hands through the crouch and, on the frame it reaches the floor, becomes the ground heap. The reverse of §6a, and the two must share the handover instant so neither double-draws nor blinks |
| **Into a build site** | The material is consumed by the site. **Already reports the stow** — `BuildJob.cs:363`, on the argument that "the stoop is the same motion whether the load goes on the floor or into a frame". The design draft claimed this was missing; it was not, and nothing needed doing |
| **Interrupted mid-haul** | `JobDriver.DropCarried` puts the load on the nearest cell with room. **This is the one sim-side change** — it must now fire `Stow`, where it deliberately said nothing |

A partial delivery reports the stow twice on one tick: once at `BuildJob.cs:363` and again through
`Cleanup`'s `DropCarried` for the remainder. **That is harmless and worth knowing why**, because
it looks like a double motion and is not: the snapshot publishes once per tick, so presentation
never sees the intermediate serial and fires exactly one stow. It is a property of reporting a
gesture as a field read once rather than as an event queue, and anything that changed the publish
cadence would break it.

### 6d. The comment that has to change

`Job.DropCarried` (`Job.cs:215`) says, deliberately and at length:

> "It says nothing to presentation, deliberately: this is a colonist dropping what it is holding,
> not stowing it on purpose, and that is a different motion which nothing draws yet."

That was right when nothing was drawn. It is wrong the moment the load is visible, because the
alternative to a stow is the load teleporting out of the arms. The owner's decision is to reuse the
stow while there is only one motion.

**So edit that paragraph; do not work around it.** A future session that meets a silent
`DropCarried` and a visible load will otherwise add a second drop path, and there will be two
owners of one rule — which `docs/bug-patterns.md` lists as this project's commonest fault. Record
in it that the distinction is *deferred*, not *abandoned*: an abandoning drop is still a different
motion, and when there is a second one to draw, this is where it goes.

## 7. What touches the simulation

Less than the draft expected. **One gesture report and two published aspects**, none of them
state:

1. `JobDriver.DropCarried` fires `PawnGesture.Stow`, where it deliberately said nothing (§6d).
2. `PawnRegistry` publishes `odyssey.pawn.carrying` and `.stack` while a job holds a load.

Gestures and aspects are both **not saved and not hashed** (`Views.cs:18`, `Views.cs:298`), so
none of this changes the state hash and **no golden needs re-baking** — confirmed, not assumed:
the fast tier's 735 Sim tests, goldens included, passed unchanged. Worth stating explicitly,
because the last change in this area moved all three goldens and cost a session.

Everything else — the pose, the cradle, the placement, the armful, the activity line — is
presentation.

## 8. The activity line

The amount now lives here, since it left the arms (§3a).

`JobLabels.Label(jobDef)` returns `Registry.Label("ui.status.hauling")` and knows nothing else.
Composing "Hauling · Wood × 8" needs the item's own name, which is a registry key
(`ui.res.wood`), and a pattern.

**The pattern is a registry key too.** `RegistryTests.NoPlayerFacingNameIsWrittenInCSharp` would
not catch a format string, but the rule behind it applies: a player-facing string written in C# is
a string the wiki cannot correct. This is a content change and carries its CSV rows and both
`--check` rebuilds in the same commit.

## 9. What deliberately is not done

### 9a. What a new commodity inherits

**Owner, 2026-09-19: "stones should get the same treatment and future big items."** They do, and
nothing about the carry path is per-commodity — this is worth stating because it is the kind of
thing a later session would otherwise re-implement for the next item.

| | How a new item gets it |
|---|---|
| Held in the arms at all | Automatic. The aspect carries a def index; the renderer resolves a module for it |
| Turned with its carrier | Automatic, both paths — a single prop takes the yaw, an armful is turned about the cradle as a cluster |
| Raised out of the ground, and dropped back into it | Automatic. `CarryHandover` is keyed on the thing's id and knows nothing about what kind of thing it is |
| Drawn as an armful of several rather than one prop | **Opt in**: add a row to `ItemHeap.Recipes`. A null row means one prop in the middle, which is what everything did before heaps existed |
| A sane armful for a new heap | `EveryHeapCommodityCanBeCarriedAsAnArmful` walks every recipe, so a bad one fails the Unity tier rather than appearing as rocks a metre from somebody's hands |

The one thing a genuinely **big** item would want is its own hold — a girder is not scooped in two
arms at the waist. Nothing in the game is that size yet, and the decision belongs with whatever
introduces one; the seam for it is `CarryPose`, not a special case in the renderer.

| Not done | Why |
|---|---|
| Carry capacity, or a load that slows you | WS4 is held, and the owner's rule is: do not invent an urgency model. The move rate is untouched |
| A shoulder carry for logs | The owner chose the scoop for everything. A second pose is a second thing to keep right |
| An armful that shows the amount | §3a |
| A distinct abandoning drop | §6d — deferred with its seam intact, not abandoned |
| Holding a load clear of the water | §5c — accepted as-is pending a playtest |
| A tool stowed on the back | Needs a back attachment seated per rig. A whole piece of work, and it would slip this one |
| Carrying up a ladder | A hauler cannot climb one at all. Stairs (`U44`) are the unit that opens it — and until U44 this was true of stockpile hauling only, because construction delivery never asked for the hauling mode (`60-stairs.md` §3) |

## 10. Tests

Everything in §4 and §5a is arithmetic over transforms and belongs in the fast tiers, not in a
screenshot.

As built, in three files.

**`CarryPoseTests`** (Unity tier — the fast tier compiles neither Presentation nor Editor, so
green seconds prove nothing here):

| Test | Asserts |
|---|---|
| `TheLoadSitsBetweenTheHands` | two arms holding one thing hold it between them |
| `ALoadHeldCloseIsPushedClearOfTheChest` | the "bundle through the ribs" fault, §4a |
| `ALoadAlreadyClearIsLeftExactlyWhereTheHandsPutIt` | the clearance is a floor, not a target — otherwise "carried" becomes "held at arm's length" |
| `TheClearanceIsMeasuredAlongTheFacingAndNotAsADistance` | fails only for a colonist reaching sideways, which is exactly why it is written down |
| `TheCradleIsPushedAlongTheFacingWhicheverWayTheColonistIsTurned` | never a world axis, never a bone's |
| `ARigWithNoShouldersIsLeftAlone` | a non-Humanoid prefab goes on walking, as it already does for arms and legs |
| `ThePlacementIsAbsolute` | feeding the answer back in changes nothing — §4c, the fault that made an axe spin |
| `TheStanceEasesOnAndOffInTheTimeItSays` | and `APausedWorldHoldsTheStanceWhereItIs`: delta zero is the identity |
| `ALoadGoesUnderWaterWithItsCarrier` | the §5c placeholder, stated so it cannot be removed silently |
| four armful tests | constant count, tighter than the floor heap, staggered not flat, and stable frame to frame |

**`CarryTests`** (fast tier, Sim):

| Test | Asserts |
|---|---|
| `TheCarryAspectsAreSpeltTheWayPresentationSpellsThem` | load-bearing: the two assemblies agree by string and nothing else |
| `AnEmptyHandedColonistPublishesNoLoad` | absence is the answer, which is what makes it free |
| `TheLoadIsPublishedFromTheGraspAndNotBefore` | both ends are faults and they are different ones |
| `TheLoadIsNeverInTwoPlacesAndNeverInNone` | every tick of a whole haul: on the floor **xor** in the arms |
| `AJobThatEndsMidCarryReportsPuttingTheLoadDown` | §6d, including that the serial moved |

**`CarryLabelTests`** (fast tier, Hud): the spelling twin, the composition with and without a load,
`OneOfSomethingIsNotCountedAtYou`, `EveryWordOnTheLineComesFromTheRegistry` (no letter on the line
was written in C#), and `TheLineIsNotRebuiltWhileNothingAboutItChanges` — reference identity, ADR
0003 F1.

The existing `PawnTests` lift assertions are unchanged and still pass: the thing is on the ground
while she bends, in her arms before the toil ends.

## 11. What only a person at the keyboard can answer

Deliberately short. Anything a test can decide is in §10 instead.

1. **Does the scoop read as carrying, or as a shrug?** The cradle numbers in §4b are proposed and
   none has been looked at.
2. **Does a single wood bundle read at the play camera at true scale?** The owner chose true scale
   over an enlargement; this is the question that choice rests on.
3. **Is the amount missed?** Nothing on the board says how much any more.
4. **The stream** (§5c) — the load now vanishes as she wades in and reappears as she comes out.
   Whether that pop is worse than the swinging bundle it replaces is the whole question, and it is
   the one thing here built as a placeholder rather than as an answer.
5. **Does the interrupted stow look like tidying up when it should look like abandoning?** The
   owner accepted this knowingly; the question is whether it is noticeable.
6. **Does the load leave the hands cleanly at a build site**, or does it look deleted?
7. **Does an armful of three rocks read as "some stone"** at true scale, or as three pebbles?

---

## 12. The sound of a carry

*Added 2026-09-19 on `claude/alert-sounds`, from one recording the owner supplied
(`item-pick.mp3`).*

### What it is

A soft mid-range scuff — material being handled, most of its energy between 250 and 800 Hz with a
second lobe up to 2.5 kHz. Not a click, not a thud with a transient; closer to a bag of stones
being shifted. **It is deliberately the quietest placed sound in the game**, because of when it
plays: every leg of every haul, and a colony of six haulers is a near-continuous stream of it.

### One recording, two sounds

Played unchanged at both ends of a carry it would say *something happened* twice and never say
which — which is the whole of what a player needs to hear from a hauler they are not watching. So
each end is **resampled rather than pitch-shifted**, which moves pitch and length together:

| | Rate | Result | Colour |
|---|---|---|---|
| **lift** | ×1.14 | up ~2½ semitones, an eighth shorter — 0.38 s | high-pass 170 Hz, +2.5 dB shelf at 3 kHz |
| **drop** | ×0.82 | down ~3½ semitones, a fifth longer — 0.53 s | +3 dB shelf at 220 Hz, low-pass 5.5 kHz |

**Resampling is right here precisely because it is not a clean pitch shift.** A bigger, heavier
object really does sound both lower *and* longer, so the artefact is the effect. A
formant-preserving shift would give two sounds of the same size at different pitches, which reads
as one sample played twice. The EQ then seals it: brightness for a thing leaving the ground, body
and a dulled top for a thing arriving on it.

**Three takes of each**, at rates spread ±4% around those centres, and the catalogue adds ±7%
per-play pitch on top. One sample is recognisable as a sample within three or four plays, and this
one gets more plays than anything else in the game.

### Where they fire

| Sound | Moment | Point |
|---|---|---|
| `carry.lift` | the edge the simulation hands the thing over — the middle of the lift crouch, the frame the hands are on the pile | where the pile was lying |
| `carry.drop` | **the end of the fall**, not the start | the colonist's feet — at most a cell from where it landed, and the sound's full-volume radius is 14 m |

The drop is the one with a rule worth naming. `CarryHandover.FallFinished` is true forever after
the landing, so anything that polls it turns one thud into a buzz; `CarryHandover.FallLanded`
takes both sides of the frame step and is true exactly once, including when a frame is longer than
the whole fall (a stall, a load screen, a step taken the instant a paused game resumes).

Both are published as events from `PawnFigureDirector` — `LoadLifted`, `LoadSet` — for the reason
`BlowLanded` is: the one place that knows when a load changes hands should not have to know what
follows.

**Firing the drop when the hands open would be wrong**, not merely early. The load is still
visibly in the air for another third of a second, so the sound would read as the colonist dropping
something they are still holding.

### Blending it into the environment

This is the part that is tuning rather than plumbing, and all of it is in the catalogue:

| | Carry | The axe, for comparison |
|---|---|---|
| Volume | **0.40** | 0.85 |
| Max distance | **120 m** | 200 m |
| Priority (0 = first served) | **150** | 120 |
| Cooldown | 0.10 s | 0.15 s |

Under the work rather than beside it. The short range is as much a performance decision as a mix
one — the director culls by range *before* it spends a voice, so a distant stockpile run costs
nothing at all. The low priority means an axe, a chime or anything else wins the voice when the
pool is full, which is the right way round: a hauler is the background of a colony and the thing
being built is the foreground.

### Processing notes

The source is very quiet — −44.6 LUFS, peaks at −28 dBFS. Two things follow.

**Gain first, then denoise, then trim.** The whole signal sits below the level a denoiser takes
for noise and below the level a silence trim takes for silence; run either on the raw file and the
clip comes out empty. It is lifted 24 dB into a working range first.

**Levelling is peak-ceilinged RMS, not EBU R128.** The alert chimes use two-pass `loudnorm` and
the obvious move was to reuse it. It cannot be: R128's integrated loudness is gated in 400 ms
blocks, these clips are under half a second, and `loudnorm` duly reports −inf and refuses the
second pass. Peak-ceilinged RMS is what a one-shot wants anyway — what matters about an impact is
how hard it hits, not how loud it is over time. All six land at −3.0 dBFS peak, mean −21.8 to
−22.7.

**And the level is measured on the written file, not on the filter chain.** Measuring through
`-f null` is a pass cheaper and gave an answer 3.5 dB out, because the stereo-to-mono downmix
lands differently on the null muxer than on a WAV; every clip came back pinned at 0 dBFS before
that was found. The script measures the bytes that will ship.

`normalize: 0` in the import metas, against Unity's default of 1. With `forceToMono` set the
importer peak-normalises the downmix, which would throw away the levelling. The files are already
mono so it is inert today, and it is set anyway because the day somebody drops a stereo take in
beside these is not the day to rediscover it.

### Open, for a person at the keyboard

- **Whether 0.40 is the right level**, and whether the answer changes with six haulers rather than
  one. This is the number most likely to be wrong, and it is one slider.
- **Whether lift and drop are actually distinguishable in play**, with the colonist small on
  screen and the camera at its default height. They are plainly different side by side; that is
  not the same test.
- **Whether the drop wants to be lower still.** ×0.82 was chosen to stay recognisably the same
  material. ×0.7 would read as heavier and less like its sibling.
- **Whether three takes is enough** once a stockpile run is watched end to end.

## Cross-references

- `docs/design/13-gestures.md` — solved versus authored; the lift and the stow
- `docs/design/20-swimming-and-water.md` — the float, and the deep-water rules that are designed
  and not built
- `docs/design/21-ladders-and-climbing.md` — why a hauler cannot go up
- `docs/design/17-rates-and-stats.md` — WS, and why the move rate is untouched
- `docs/bug-patterns.md` — one rule with two owners, which §6d exists to avoid
- `docs/design/24-alert-sounds.md` — the other half of the 2026-09-19 audio work, and
  `tools/audio/bake_alerts.sh`, whose levelling this one deliberately does not share
