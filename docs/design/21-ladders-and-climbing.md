# Ladders, the climb, and the site that could not be cancelled

Built 2026-09-18 from three owner reports of the same play session. Read this before touching a
ladder's placement rule, its connector, the climb pose, or what a click may land on.

## The reports

1. *"When I went up to the same depth as the slab was sitting on, I tried to use the cancel tool to
   cancel a slab being built — but it didn't work."* Clarified: nothing on that layer highlighted or
   selected at all, the slab was **over open air**, and only **blueprints** were dead — built things
   and colonists still answered.
2. *"When a colonist goes up a ladder they seem to levitate"*, and they are not flush against it.
3. *"A ladder shouldn't be able to be built if there is a slab directly above because colonists go
   through the floor."*

Owner's answers, the same day: refuse the placement (not auto-hatch, not silently dead); a legal
ladder has **a slab beside its top to step on to**; the climb is **presentation only**; and two
reference photographs — five climbers from behind, one from the side.

---

## 1. A waiting order is one of the things in the world

**It was never the cancel tool.** `DesignatePresenter.Submit` already sends `CancelDesignation` and
`CancelBuilding` for every cell in the box and lets the simulation refuse whichever is wrong. It
never ran, because no cell reached it: `SliceCameraRig.CellAt` misses, and on a miss the rig raises
no hover, no click and no drag at all.

`SlicePicker.Owner` knew four things — an edifice, a built floor, water, the solid block below — and
a **site was none of them**. Sites live in `ConstructionGrid`, reach presentation as
`WorldSnapshot.Sites`, and are drawn by `OdysseyBootstrap.DrawBuildingSites` straight into the
renderer, outside the mirror the picker walks. So:

| Where the site is | What the ray found | What the player saw |
|---|---|---|
| **Over open air** | nothing in the whole column | the layer dead to every tool |
| **Over ground** | the block underneath, one layer down | the order cancelled from the layer *below* |

**The fix: sites are in the mirror.** `WorldRenderModel.SetSites` takes the frame's sites once per
frame (`OdysseyBootstrap.LateUpdate`, before anything reads the mirror), and `Owner` answers "a site
standing here" — **above** the floor rule and above the block-below rule. Above the floor rule
because a covering is laid on a cell that already has one; above the block-below rule because a
structural slab ordered into open air has nothing under it at all.

It is a set beside the arrays rather than a column in them: everything else there is geometry,
mirrored per chunk off the `CellGrid` and driven by its dirty flags, and a site is not geometry — it
appears when an order is given, its progress changes every tick, and it vanishes when it becomes a
real wall.

**The ghost rule is intact.** ADR 0006's argument is that a translucent *hint* must not be a pointer
target. The build cursor's ghost is still untouchable. A site has a cell, a material, a progress and
a save record, and a player pointing at one is pointing at the order they gave.

The alternative — a second pass in the rig testing the snapshot's site list — was rejected because it
is a second implementation of "what is in this cell", and the forced-order context menu
(`15-building.md` §8) needs to right-click a site too, so it would have been written twice.

## 2. The climb face had two owners, and one of them gave up

`ClimbPhase` was already set for any strictly vertical step, so a ladder always reached the pose. But
every joint the pose moves is multiplied by `ClimbWeight`, and `ClimbWeight` only rises while
`ClimbFace` is non-zero — and the face came from a scan for a **solid** neighbour. A ladder against
slabs rather than rock, or run up through an open storey, found nothing: weight decayed to nought,
the arms and legs returned to the idle, and the figure rode it straight up through the air.

The code's own comment said the simulation refuses to lay a connector where there is no block. That
is true of a **mined shaft** and was never true of a **built ladder** — `RefreshLadder` asks for no
wall at all.

Underneath it, the drawn ladder and the climber disagreed by construction:

| Who | Rule | Fallback |
|---|---|---|
| `ChunkMesher.EmitLadder` | first occluding neighbour | **north** |
| `TryWallBeside` | first solid neighbour, different order | **nothing** |

**`WorldRenderModel.LadderFacing` is now the one owner**, and both call it. Where nothing occludes it
answers north — what the mesher has always drawn — so a free-standing ladder has a face to hug
instead of no wall to find. `TryLadderBeside` asks it before the solid scan: a ladder is what you
climb, and the solid scan stays underneath as the rule for a shaft cut out of rock. Pinned by
`LadderFaceTests`, which measures the yaw out of the mesher's own instance matrices rather than
reading the source. It is the `HopPriceHasOneOwnerTests` lesson in another place.

## 3. A ladder is not a rock face

From the owner's references: on a ladder **both hands are on rungs above the head** — the trailing
one about chin height, the leading one at nearly full stretch — and the **stepped knee comes right
up** while the pushing leg stays nearly straight. On stone the lower hand ends near the hip and the
step is a modest one.

So the pose branches on `Figure.OnLadder`, latched beside `LastClimbFace` so it outlives the face
while the weight eases out:

| | rock | ladder |
|---|---|---|
| reaching arm | −150° | −162° |
| trailing arm | −35° | −104° |
| elbow | −30° | −46° |
| stepped foot, as a fraction of the leg | `SteppedDrop` 0.68 | `LadderSteppedDrop` 0.46 |

The pushing end of the cycle is deliberately identical: full stretch is full stretch on either
surface, and lifting both feet would read as hanging. The contralateral cycle (`ClimbPose.StepsFrom`)
is unchanged and is what makes it read as climbing at all.

**These numbers are invited tuning.** Nothing else depends on them.

**Flushness needed no new number.** `ClimbLean` already carries the body to 0.30 m off the cell face,
which is about a body's depth from the rungs. It was never applied to a ladder because the face was
zero — fixing the face fixed the lean with it.

Presentation only: no cell, no save, no hash.

## 4. The shaft rule

**The configuration the owner reported as a bug was the configuration the rule demanded.**
`RefreshLadder` registered a connector only when both ends were `CellGrid.IsWalkable`, and that needs
`HasFloor` — so the only ladder that ever worked was one with a slab directly above it, climbed
through the deck. Refusing the placement alone would not have tightened ladders; it would have
deleted them.

The system now:

1. **The shaft cell is open.** `AllowsLadder` refuses a ladder whose cell above holds a slab or solid
   rock. That is report 3, as a refusal — `IntentRejection.NotPermitted`, no new player-facing name
   and so no wiki change.
2. **And the same rule from the other side.** A slab or covering is refused directly over a ladder,
   because otherwise the first rule is walked around in two moves: build the ladder, pour the floor.
3. **A ladder makes its own top standable.** `LadderArrivesAt` replaces the walkability test: the top
   must be open, and must have either **a landing beside it** (an orthogonal neighbour with a real
   floor — the owner's answer), **another ladder in it** (the shaft continues), **or a real floor**.
   Standing in the open shaft cell is granted by `NavGrid.RefreshFrom`'s existing rule that *a
   connector is its own floor*, so the step sideways on to the landing is an ordinary walk.
4. **The fan-out.** `RefreshLaddersAround` refreshes the ladder in this cell, the one under it, and
   the one under each of its four neighbours — because a slab laid here is the landing for those
   four, and taking it away strands them. Missing that is the class of fault that leaves two
   identical-looking ladders behaving differently depending on the order they were built in.

### Two decisions worth knowing about

**A landing is not required at the order.** The owner's answer was "no landing anywhere = refuse",
and it is enforced at the connector instead. Only the top ladder of a chain needs a landing, and a
player builds a chain from the bottom, so demanding one at placement would refuse every ladder in a
shaft but the last, in the only order they can be built in. The ladder is buildable; it opens nothing
until the landing arrives, which is the behaviour `RefreshLadder` already documented and which
`ItDoesNotMatterWhetherTheLadderOrTheLandingComesFirst` pins.

**Nothing migrates, because a real floor still counts.** `LadderArrivesAt` is a *superset* of the old
test, so every ladder the ruined city stamps and every ladder in an old save keeps working exactly as
it did. The plan had proposed stamping holes in worldgen and accepting a save break; it turned out
not to be needed, and the irreversible choice was avoided. What the new placement rule stops is any
**more** of them being made.

**What the player does now:** leave a hole in the upper floor (or deconstruct one slab) and build the
ladder under it. A ladder under a finished, unbroken floor is refused at the order.

### The golden master moved, and said why

`Golden.City.Simulated` was re-baked. The failure named its own cause — *"the board generated
identically and the colony then ran to a different state"* — which is the ladder rule and nothing
else: city ladders standing under an open cell used to be dead and now work. `Generated` is
untouched, which is the evidence no generator pass changed. The meadow and the played board are
unmoved, because neither has a ladder on it.

## 5. What this did not touch

- **A hauler still cannot climb a ladder** (`Connector`'s mode mask). Material cannot be carried up;
  stairs (`U44`) remain the answer.
- **Climb cost is unchanged** — `MoveCost.LadderUp` 540 / `LadderDown` 400. Presentation only was the
  owner's choice; a climb rate belongs with WS if it is ever wanted.
- **Fall damage** still has nothing to apply itself to, so a colonist who steps into an open shaft
  arrives unharmed. The shaft cell is now a place a colonist can legitimately be, which makes that
  gap slightly more visible than it was.

## 6. Nobody has pressed Play on any of it

The climb pose and the flushness are photographs' business and nothing here has been photographed.
Open questions a test cannot answer: whether the two arm angles read as a ladder rather than a
shrug, whether 0.46 of a leg is too big a step at the play camera, and whether a colonist arriving in
an open shaft cell and stepping sideways looks like arriving or like hovering.
