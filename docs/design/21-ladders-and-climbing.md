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
answered north — what the mesher has always drawn — so a free-standing ladder has a face to hug
instead of no wall to find. (**That fallback was replaced the same day**: north is arbitrary and the
owner saw it. A ladder rotates now and the fallback is the player's own answer — §7.) `TryLadderBeside` asks it before the solid scan: a ladder is what you
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

> **§4 was rewritten on 2026-09-18 by the playtest below — read §8 before trusting the two
> "decisions worth knowing about" that follow.** A blueprint was invisible to both halves of the
> rule, so two legal orders still produced a ladder under a floor; and "nothing migrates" was
> reversed on the owner's instruction.

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

  > **Half of that was false until 2026-09-20, and nothing tested it.** `job.Mode = Hauler` was set
  > in exactly one place in the simulation — `HaulWorkGiver` — so the exclusion covered stockpile
  > hauling and **not** construction delivery: `DeliverWorkGiver` ran as a colonist and a plank went
  > up a ladder happily. The sentence above, and the same sentence in `CLAUDE.md`, the `U43` and
  > `U44` plan rows and `24-carrying.md`, all concluded that nothing could be built on an upper
  > storey, and a player could. `U44` gives delivery the hauling mode, in the same commit as the
  > stair so the capability is never taken away without its replacement. `docs/design/60-stairs.md`
  > §3.
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

---

## 7. The playtest, 2026-09-18 (second round)

Four reports. Three are answered below; the bed's is in `docs/design/20-beds.md` territory and is
recorded here only because it arrived with the others.

### The roster bar's pictures vanished on start and on load

**And the colonist card kept them, which is what named the fault.** A roster card is a *slot*: it
re-reads itself when the colonist in it changes, so it asks for a portrait once, keyed on the pawn
id. Building or loading a colony calls `PortraitStudio.Clear`, which **destroys** every texture —
and a new colony's pawn ids start at the same small numbers, so no slot's id had changed, nothing
re-read, and every card was left pointing at a texture that no longer existed. The inspect pane asks
afresh each time it is opened, which is exactly why it kept working.

An id cannot answer *does this picture still exist*. `PortraitStudio.Generation` can, it costs one
integer, and the card now refreshes when **either** the colonist or the generation changes.

### A built ladder on the wrong side of its cell

`LadderFacing` fell back to north wherever nothing occluded — an arbitrary answer the player could
neither predict nor change, and the owner's own example was a ladder "standing in mid-air". So
**`Building_Ladder` rotates now** (`rotates` in the def; `BuildingFingerprint` moved with it), R
turns the ghost, and the stored facing is what a free-standing ladder uses.

**The wall still wins wherever there is one**, and that is the rule rather than an exception to it:
which side of a wall a ladder is bolted to is physics, not preference. One function still owns the
whole answer, and it now has three readers — the mesher, the figure director, and the build cursor,
which asks the `chosen` overload so the ghost stands where the built ladder will.

Two things had to follow. `RaiseEdifice` kept a facing **only for two-cell things** — reasonable
while a bed was the only thing that rotated, and it would have thrown the player's rotation away
silently between the order and the built ladder; it reads the def's `rotates` now. And a one-cell
ghost was drawn without a facing at all, so R would have turned nothing the player could see.

### The climb jolted, stalled, and kept its arms up at the top

One fault with three symptoms. The climb weight's target was a flat yes-or-no on whether a face had
been found, so it held at 1 for the whole step and began easing out on the frame the step **ended** —
by which time the colonist was standing on the ledge. What followed was 0.15 s of a figure on solid
floor with its arms overhead while most of a metre of lean unwound underneath it: the raised arms,
the jolt and the apparent stall, in that order, all after the climbing was over.

`ToppingOut` makes letting go part of the climb: over the last quarter of the rise the weight runs
down to nought, which brings the arms down, unwinds the lean and puts the figure in the middle of its
cell exactly as it arrives. It reads the step's **direction**, because going down the top of the
ladder is the *start* of the step — a phase-only rule would have a colonist let go at the bottom of
every descent, which is the one place it needs to hold on.

### The bed built through a wall — found, third time of asking

**`Raise` read the facing out of the site slot it had just wiped.** The far cell was derived from
`_facing[cell]` at the top of the method; then `Clear(cell)` zeroed the site, facing included; then
the facing was read *again*, out of the cleared slot, on its way to the record. Every rotatable
thing was built facing **north**, whatever the player chose.

**It hid because only the drawing was wrong.** The cells were derived before the clear and were
always right — so the footprint guard still refused a bed whose far half was in a wall, nothing was
ever built anywhere illegal, and no simulation test could see it. The bed's record said north, so
the mesher drew it extending north from its head cell, **into whatever was north, walls included**,
while the two cells it actually occupied were the ones the player asked for. That is exactly the
pair of screenshots: ghosts laid along the room, built beds at a quarter turn to them, one of them
lying through a wall it does not occupy.

And the sleeper came with it. `AimSleep` lays a colonist out along `BedFacing`, so a bed placed
along the room and recorded as north put its sleeper across the bed — the "half way up the bed …
legs much further up and the colonist hanging off" from the same report. One line, all of it.

**Three tests had a clear shot at this and all three missed**, which is the part worth keeping:

- `ABedsFacingIsInTheStateHash` compares a bed placed north against one placed east and passes —
  on the difference between the two beds' **cells**, not their facings. It was written to pin the
  facing and pinned something else that happened to move with it.
- `ABedIsRefusedWhenItsFarCellIsAWall` (written the day before, to reproduce this very report)
  passes, because the guard it tests really does work: the refusal path was never the broken one.
- Every other bed test places facing **0**, and a lost facing is also 0.

`TheFacingItWasPlacedAtReachesTheRecord` now walks every facing the board can take and refuses to
pass on fewer than two, because north alone proves nothing.

**The method that found it was a probe, not a reading.** Three sessions of reading the placement
path end to end — the intent seam, the gesture, the shared rotate key, the mesher's yaw, the drawn
bed's extent — concluded correctly that each part was right, and the fault was in none of them: it
was two correct lines with a `Clear` between them. Ten lines of throwaway test printing *asked → got*
found it in one run. `docs/lessons.md` already says measure rather than read; this is the third time
it has been the difference.

### What the second hunt did find

The owner reported it unchanged, so the placement path was walked end to end: the intent seam
(`A`=building, `B`=stuff, `C`=facing, and `HandlePlace` passes them in that order), the gesture
(`Commit` returns the anchor for a single placement, and `TryPreview` builds that same cell's
footprint, so the ghost and the order agree for every facing), the rotate key's shared binding (the
rig skips slice-up exactly when `RotatableArmed`), the mesher's yaw, and the drawn bed's own extent
— 4.6 m inside a 5 m footprint, 0.2 m clear at each end. All correct. **The bed still has not been
reproduced.**

Two real defects fell out of the hunt anyway, and one of them was an hour old:

- **`BuildShapes` had not been told the ladder rotates.** The HUD keeps a parallel table of
  footprints and rotatability because it cannot see `Odyssey.Sim.Construction` (ADR 0003), and
  `DesignateDirector.RotatableArmed` reads *that* table to decide whether R turns the ghost or
  raises the slice. The def alone would have left R doing the other job and the rotation silently
  discarded, with every simulation test green — the consequence is not in the simulation.
  `BuildShapesAgreementTests` now walks every handle against the Defs; the fixture that existed
  checked the two arrays' lengths and spot-checked the wall and the bed.
- **The footprint guard was in the simulation and not on the screen.** `Place` refuses a bed whose
  far cell is occupied — correct, tested, invisible. The ghost asked only about the cell under the
  pointer, so the order drew in its own material like any legal one and the click then did nothing
  at all. A click that silently does nothing is indistinguishable from a click that missed, which is
  one way "it doesn't respect where I placed it" gets reported. The ghost now asks about every cell
  the thing would claim, deriving them with `EdificeFootprint` rather than restating the rule.

---

## 8. The playtest, 2026-09-18 (third round)

Three reports, all about ladders. Beds were confirmed fixed in the same session. **One of the three
needed no code at all, one had a cause nobody had guessed, and the fix for it uncovered two further
defects that nothing had ever exercised.**

### "Is it possible to use R to rotate a ladder … or display red outline?" — already done

Rotation, the turning ghost and the red refusal all landed in the merge the owner was playing.
`Building_Ladder` carries `rotates`, `BuildShapes.Rotates` agrees (and `BuildShapesAgreementTests`
walks both), and `OdysseyBootstrap.Refused` paints the ghost `PreviewRefusedColour` by asking
`ConstructionGrid.Allows` — the same rule the click will apply, so the colour cannot come to
disagree with the outcome. Nothing was built for this report. It is recorded because "verify before
building" is what saved the work.

### "I can't place the ladder underneath a slab and it has to be against the wall" — one cause, not two

The first half is the rule the owner asked for the day before, and it stands: **refuse, and the red
ghost is enough of a reason** (owner's answer, asked directly). The alternatives — auto-queueing a
deconstruct of the slab above, or allowing an inert ladder — were both put and both declined.

**The second half was the same rule wearing a disguise.** Nothing in the code has ever asked for a
neighbouring wall. Inside a roofed room *every* cell is under a slab, so the only cells that accept
a ladder are the ones past the slab's edge — which are the ones beside the wall. The owner confirmed
it: *"it wouldn't snap or even place itself on tiles with slabs above"*. No second defect.

### "Sometimes the colonists climb up the ladder where there is wall or slab directly above"

**The prime suspect was wrong, and a probe said so in one run.** The standing theory was §4's
compatibility clause — `LadderArrivesAt` returning true on a real floor, kept so old and generated
ladders would go on working. It cannot be the cause on the board the owner played: **the wooded
meadow generates no ladders and no connectors at all**, measured on three seeds, so every ladder
there is one the player built, and a player could no longer build one under a floor.

The real cause, reproduced on the meadow in both orders:

> **The shaft rule asked the built world, and a blueprint is not built.** `IsLadder` reads
> `_grid.Edifice` and the floor test read `_grid.Floor`, so an order still waiting to be carried out
> was invisible to both halves of the rule. Order the ladder, order the floor above it: each is
> legal *on its own*, because neither exists yet. Both then get built.

That is the whole of "**sometimes**" — it depended on which job a colonist happened to pick up.

**The fix is that the rule now sees sites, and is asked twice.** `ShaftRulePermits` is the one owner
of the whole question, for both sides and both orders; `LadderHereOrOrdered` and `FloorHereOrOrdered`
are the built-or-ordered tests it asks. And `Raise` asks it **again at the moment of truth**, which
is the shape the two-cell guard beside it already had: everything else `Place` checks is checked once
because a site holds its own cell, but the shaft rule spans two cells that are ordered separately, so
the other order can legitimately arrive later. Re-asking *only this rule* rather than the whole of
`Allows` is deliberate — a site with its material hauled to it fails `needsClearCell`, so re-asking
everything would refuse every bed whose own wood had been delivered.

**And the compatibility clause went anyway**, on the owner's instruction (*"accept the save break"*),
as the second line of defence rather than the fix: a ladder that somehow ends up under a floor now
opens nothing. What that cost, measured rather than assumed: **nothing on any generated board.** All
three golden masters are byte-identical, city included — because worldgen's ladders reach the nav as
`StampedConnector`s (`SurfacePasses.RecordConnectors`) and never consult `LadderArrivesAt` at all.
The city has 20-odd ladder connectors a seed, 13 to 18 of them with a floored top, and dropping the
clause moved none of them. The planned worldgen hole-stamping and city re-bake were therefore never
needed, and the owner's *"forget the city, it is redundant"* closed the question for good. **The save
break is still real** for an actual saved colony holding a ladder under a floor; nothing in the test
suite covers that, because nothing can build one to save.

### Two defects the hunt found, neither of them reported

**A shaft could only ever be one storey.** A ladder is `blocking false` so a colonist can stand in
it, and `SomethingUnderfoot` wants a floor or a *blocking* edifice — so the second ladder of a chain
was refused, and `LadderArrivesAt`'s "another ladder in it" clause, written for exactly this case,
was unreachable for anything a player built. Every test in `LadderTests` builds one ladder, so
nothing had ever asked. Fixed on the owner's instruction (*"fix it here"*): `StandsOnSomething` lets
a ladder stand on a ladder, and **only a ladder** — folding it into `SomethingUnderfoot` would let a
*wall* stand on a ladder and cap the shaft with something the rule above cannot see.

That fix did not work on its own, and the reason is worth keeping. `RefreshLadder` gated the
connector on `CellGrid.IsWalkable` at the ladder's foot, which needs a real floor — but the foot of
an upper ladder is the open shaft cell, standable only through the lower ladder's *own connector*,
and `CellGrid` cannot see connectors (`NavGrid.RefreshFrom`'s rule that a connector is its own
floor). The chain built and the upper ladder silently had none. `StandsOnAFooting` is the connector's
own version of the question, and it counts only a **built** ladder below: a blueprint may let you
place the next ladder of a chain, but it must never open a way up nobody has built. The fan-out then
had to reach **upwards** too — `RefreshLaddersAround` refreshes the cell above now — or pulling the
bottom ladder out leaves the upper one with a connector whose foot is mid-air.

**Roofing over a working shaft closed it silently.** A slab over the open cell a ladder arrives in is
not capping the ladder, so nothing refused it; the ladder simply stopped having anywhere to arrive
and the way up vanished with nothing said. Refused now, by the owner's answer, for the same reason
the slab-over-a-ladder rule exists — that is the `BelowOf(BelowOf(...))` reach in `ShaftRulePermits`:
a slab is refused with a ladder one cell under it or two.

### What is still not covered

`Raise`'s re-check **cannot be reached from `Place` any more**, which is the point of it but leaves
it untested by construction: with sites visible on both sides there is no legal pair of orders that
combines into an illegal building. It earns its place because `_sites` are saved — a save written
before this rule can carry exactly that pair — and because it is the guard for anything that edits
the board without going through `Place`. The same is true of the dropped compatibility clause: with
the placement rule closed, nothing in the game can build a ladder under a floor for it to refuse.

**And nobody has pressed Play on any of it.** The climb pose numbers from §3 are still unjudged in
motion, which is the one thing worth doing while a ladder is in front of you.
