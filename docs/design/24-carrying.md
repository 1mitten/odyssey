# 24 — Carrying

**Status:** designed, not built. Numbers below marked *proposed* are invited tuning.
**Owner interview:** 2026-09-19. **Branch:** `claude/carried-items`.

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

### 4a. Solved, not authored

`13-gestures.md` §3 draws the line: author the angles when the figure aims at something whose
position the arithmetic does not know; solve to a point when it must meet something it does. The
axe swing is the first kind. **The carry is the second** — the hands must arrive under a load whose
size we know exactly, so the pose is solved and is then correct on all sixty-one rigs without a
contact sheet per rig.

Concretely: a **cradle point** is computed in the figure's own frame, the load is placed with its
base on it, and both hands are driven to it by `Grasp` / `TwoBoneIk`, which is the same machinery
that already seats an axe haft in a fist.

### 4b. Everything is a fraction or an angle, never a metre

The packs differ in proportion and the director scales them besides. A 0.4 m cradle is a waist on
one colonist and a chest on another. So, as with `Gesture.Depth` and every constant in `SwimPose`:

| Number | *Proposed* | Reasoning |
|---|---|---|
| `CradleRise` | **1.10 × hip height** | The load rests just above the hip crest, which is where forearms naturally come to rest. Below the hip and it reads as dangling; at the sternum it is the hug the owner rejected |
| `CradleForward` | **0.55 × shoulder width**, forward of the chest | Far enough out that the prop clears the torso mesh at the widest of the 61 bodies. Too near and a wood bundle intersects the ribs; too far and the colonist is presenting it |
| `CradleSpan` | **0.60 × shoulder width** | How far apart the two palms sit. Hands inside this are under the load's middle and it looks pinched; wider and the elbows flare |
| `ElbowBend` | **−75°** | Forearms near horizontal. Not 90°: a right angle reads as a waiter's tray |
| `ShoulderPitch` | **−15°** from vertical | The upper arms hang, slightly forward. This is not a lift — the weight is on the forearms, not the shoulders |
| `SpineLean` | **6°** backward | The counter-lean of somebody carrying weight in front. Small on purpose: at the play camera's 48° a big lean reads as falling over |
| `EaseSeconds` | **0.20 s** | How long the arms take to reach the carry from wherever they were, and to release it. Matches `ClimbEaseSeconds`' order so nothing in the figure snaps |

**The load's base sits on the cradle point, not its centre.** Placing by centre buries half a wood
bundle in the forearms; the arms are meant to be *underneath*, which is the owner's whole word for
it.

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

**Decision, recorded as accepted rather than as right:** ship it with no special case, because the
owner asked for no special case, and because the alternatives both cost more than a first
playtest is worth —

- hold the load clear of the water with one arm and stroke with the other: a second solved pose;
- suppress the float while carrying: reverses a decision the owner already took on its own merits.

**This is the first thing in this document I expect to be reversed.** It goes in the playtest table
by name.

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

### 6b. Carrying

Nothing to drive. The cradle is derived from the arms each frame, so the load bobs with the walk,
turns with the yaw, tilts on the terrace relief and sinks in the stream, all for free and all
without a line of code that knows about any of those things. That is the argument for solving from
bones rather than authoring an offset.

### 6c. Putting down

| Case | What happens |
|---|---|
| **Into a stockpile** | `PutDown` already fires `PawnGesture.Stow`. The load lowers with the hands through the crouch and, on the frame it reaches the floor, becomes the ground heap. The reverse of §6a, and the two must share the handover instant so neither double-draws nor blinks |
| **Into a build site** | The material is consumed by the site. No stow gesture today — the delivery driver clears `Job.CarriedItem` at `BuildJob.cs:368`. **It should fire `Stow` too**, so the load lowers rather than vanishing from the hands. One line, one of the two sim-side exceptions in §7 |
| **Interrupted mid-haul** | `JobDriver.DropCarried` puts the load on the nearest cell with room. It must now fire `Stow` as well — the other exception |

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

Two lines, both of them a gesture report and neither of them state:

1. `JobDriver.DropCarried` fires `PawnGesture.Stow`.
2. `BuildJob`'s delivery fires `PawnGesture.Stow` when the material is consumed.

Gestures are **not saved and not hashed** (`Views.cs:18`), so neither changes the state hash and
**no golden needs re-baking**. That is worth stating explicitly, because the last change in this
area moved all three goldens and cost a session.

Everything else — the pose, the cradle, the placement, the two aspects — is presentation.

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

| Not done | Why |
|---|---|
| Carry capacity, or a load that slows you | WS4 is held, and the owner's rule is: do not invent an urgency model. The move rate is untouched |
| A shoulder carry for logs | The owner chose the scoop for everything. A second pose is a second thing to keep right |
| An armful that shows the amount | §3a |
| A distinct abandoning drop | §6d — deferred with its seam intact, not abandoned |
| Holding a load clear of the water | §5c — accepted as-is pending a playtest |
| A tool stowed on the back | Needs a back attachment seated per rig. A whole piece of work, and it would slip this one |
| Carrying up a ladder | A hauler cannot climb one at all. Stairs (`U44`) are the unit that opens it |

## 10. Tests

Everything in §4 and §5a is arithmetic over transforms and belongs in the fast tiers, not in a
screenshot.

| Test | Asserts |
|---|---|
| `CarryRigTests.CradleClearsTheTorso` | the cradle point is outside the chest bounds on every one of the 61 rigs — the "bundle through the ribs" fault |
| `CarryRigTests.CradleIsAWaistNotAChest` | the cradle is between hip height and sternum height, as a fraction, on every rig |
| `CarryRigTests.BothPalmsReachTheLoad` | residual from `Grasp` is under tolerance for both hands |
| `CarryRigTests.PlacementIsAbsolute` | running the pass twice leaves an identical matrix — §4c, the fault that made an axe spin |
| `CarryRigTests.TheLoadSitsOnTheForearmsNotThroughThem` | the prop's base, not its centre, is at the cradle |
| `CarryHandoverTests.TheLoadIsNeverInTwoPlaces` | across the grasp tick and the stow tick, the load is drawn in the hands **or** on the ground, never both and never neither |
| `CarryHandoverTests.TheLoadFollowsTheHandsThroughTheGesture` | at grasp + 1 tick the load is near the floor, not at the waist |
| `PawnAspectTests` (extend) | both keys published exactly while `Job.CarriedItem >= 0`, and absent otherwise |
| `RegistryTests` (extend) | the activity-line pattern and every item name resolve |
| Existing `PawnTests` lift assertions | unchanged — the thing is on the ground while bending, in the arms before the toil ends |

Fast tier compiles neither Presentation nor Editor, so `CarryRigTests` are Unity-tier: green
seconds prove nothing here.

## 11. What only a person at the keyboard can answer

Deliberately short. Anything a test can decide is in §10 instead.

1. **Does the scoop read as carrying, or as a shrug?** The cradle numbers in §4b are proposed and
   none has been looked at.
2. **Does a single wood bundle read at the play camera at true scale?** The owner chose true scale
   over an enlargement; this is the question that choice rests on.
3. **Is the amount missed?** Nothing on the board says how much any more.
4. **The stream** (§5c) — a hauler wading with both arms stroking. Expected to fail.
5. **Does the interrupted stow look like tidying up when it should look like abandoning?** The
   owner accepted this knowingly; the question is whether it is noticeable.
6. **Does the load leave the hands cleanly at a build site**, or does it look deleted?

---

## Cross-references

- `docs/design/13-gestures.md` — solved versus authored; the lift and the stow
- `docs/design/20-swimming-and-water.md` — the float, and the deep-water rules that are designed
  and not built
- `docs/design/21-ladders-and-climbing.md` — why a hauler cannot go up
- `docs/design/17-rates-and-stats.md` — WS, and why the move rate is untouched
- `docs/bug-patterns.md` — one rule with two owners, which §6d exists to avoid
