# Stairs — U44, the way up that carries something

**Status:** design settled by owner interview 2026-09-20; implementation on
`claude/adoring-ptolemy-baq5te`. The last M3 unit before the ten-day run.
**Read first:** `21-ladders-and-climbing.md` (U43, which this copies almost line for line — the
idempotent refresh, the shaft rule, the arrival rule, and the hauler exclusion that is the argument
for this unit), `20-beds.md` §4 (the two-cell site, which is the shape a stair's footprint takes),
`02-world-and-layers.md` §5 (the connector table and the sentence about a stair's required footing),
`05-ai-and-jobs.md` §4–5 (why a connector buys reachability for free, and why its ends are data).

**Most of a stair already existed.** This unit is a buildable, a placement rule and a connector
refresh; everything under and around those was built by M1, M2 and U43 and is untouched.

## 1. What already existed

Grounding first, so the work is the gap and not the whole — and this time the gap is small. Every
row was checked against the code, because two of the status lines say the opposite:
`PawnPose.cs:483` and `HopArcTests.cs:529` both read *"stairs are not in the game yet"*, which is
true only of the build tool.

| Piece | State before this unit |
|---|---|
| `ConnectorKind.Stair`, its `AllMask`, `MoveCost.StairUp` 290 / `StairDown` 230 | **Built**, `Connector.cs:79-83`. The mask this unit's argument rests on was written years of commits ago |
| `NavFlags.ConnectorStair`; a multi-cell connector becoming several portal links | **Built**, `NavGraph.BuildConnectorZone` |
| `EdificeStairLower` = 5, `EdificeStairUpper` = 6 | **Built**, `WorldGenDefs.cs:339-340` |
| Worldgen stamping stairs, declaring their connectors, registering them after the damage pass | **Built**, `SurfacePasses.RecordConnectors`, `ConnectorRegistrar` |
| A template with an unpaired stair is a content error | **Built**, `ShellTemplate.cs:204-243` |
| Drawing both halves, the upper one lifted 1.5 m | **Built**, `ChunkMesher.EmitStair` |
| `SM_Bld_Base_Stairs_01` mapped for all three module ids | **Built**, `PlayScene.cs:1216-1226` |
| The palette chip, its drawn glyph, its registry label, its wiki row | **Built** — and the chip has been drawn **disabled** all this time for want of a def |
| A stairwell cannot be hopped up instead | **Built**, `NavGraph.UpperEndIsABlockTop` — written before U44 deliberately, *"because nothing would have failed when it did"* |
| `Building_Stair` | Missing, and `Buildings.xml:135` said so in as many words |
| Registering a connector when a colony builds one | Missing |
| The two-cell placement rule | Missing — the one thing the plan row named |

### Both halves are on one layer, and it matters

A stair's two cells are **adjacent on the same layer**, not stacked. `EmitStair` draws the upper
half lifted `CellMetrics.SizeY * 0.5` — 1.5 m — *inside its own cell*, and
`SurfacePasses.RecordConnectors` gathers both into `lower[]` and derives `upper[i] = lower[i] +
LayerStride`. So the footprint is the **bed's** shape and `EdificeFootprint` needs no vertical
variant at all.

This was worth checking rather than assuming: the two explorations of the code that preceded this
document disagreed about it, and the one that was wrong would have sent the unit off building a
vertical footprint helper nothing needed.

## 2. The decisions

Three questions, owner 2026-09-20. Every recommendation was taken.

| # | Question | Decision |
|---|---|---|
| 1 | Building material can already go up a ladder (§3). What does U44 do about it? | **Fix it inside U44**, so the capability is never removed without the replacement existing |
| 2 | Worldgen's stairs are two edifice values; a bed is one record over two cells. Which for a built stair? | **Two edifice values, one site** — what worldgen already produces |
| 3 | The golden re-bake U44 forces anyway | **Bundle the terrace-bank fix**, per the audit |

Settled here rather than asked, because the code answered them:

4. **A stair's shaft rule is the ladder's**, at the same reach. §5.
5. **Both ends or no connector** — `ConnectorRegistrar`'s recorded principle, applied to a built
   stair. §4.
6. **`BuildingDef` gains one field**, `secondEdifice`, rather than two helpers gaining a special
   case. §4.

## 3. A hauler could already carry a plank up a ladder

**This unit's stated justification was half false, and it is worth writing down why nobody caught
it.** `CLAUDE.md`'s known gaps, the `U43` and `U44` plan rows, `21-ladders-and-climbing.md` §5 and
`24-carrying.md` all say the same thing: *a hauler cannot climb a ladder, so material cannot be
carried up, so nothing can be built on an upper storey.* The first clause is true. The last does not
follow.

`job.Mode = TraverseMode.Hauler` is assigned in **exactly one place** — `HaulWorkGiver`,
`JobSystem.cs:730`. `DeliverWorkGiver`, the giver that carries building material to a site, never
sets it, so it runs at `Pawn.Mode`'s default of `Colonist` and its reachability scan
(`BuildJob.cs:115`) asks the colonist's question. A colonist can carry a plank up a ladder today and
build with it.

The claim was never tested. `LadderTests.AHaulerCannotClimbALadderSoNothingCanBeCarriedUpOne` asks
`Reachable(pawn, landing, TraverseMode.Hauler)` directly — it proves the *mode* is excluded and says
nothing about which jobs use that mode. That is the gap: **a test that asserts a rule is not a test
that asserts the rule is reached.**

So U44 closes it (§7), and it closes it *here* rather than earlier on the owner's instruction: the
day delivery stops going up a ladder is the day it starts going up a stair.

## 4. What a built stair is

One `BuildingDef`, appended to `BuildingOrder` — handle order is the save contract and a value
inserted in the middle would compile silently and mean something else in every save already written.

```xml
<BuildingDef>
  <defName>Building_Stair</defName>
  <label>stair</label>
  <edifice>5</edifice>          <!-- EdificeStairLower -->
  <secondEdifice>6</secondEdifice>
  <footprint>2</footprint>
  <rotates>true</rotates>
  <blocking>false</blocking>
  <costCount>6</costCount>
  <workToBuild>150</workToBuild>
  <minSkill>0</minSkill>
  <iconKey>ui.arch.tool.stair</iconKey>
</BuildingDef>
```

**`blocking` false**, for the ladder's reason exactly: a stair you cannot enter is a decoration.
**6 and 150** against a wall's 5 and 135 and a ladder's 4 and 90 — two cells of carpentry, and the
thing a colony saves up for rather than knocks together. **Neither number derives from anything and
nothing derives from them; they are the owner's to tune** (`15-building.md` §2).

### Two records, one site — and why that is not the bed's answer

`20-beds.md` §4 rejected paired records for a bed and named the stairs as the thing it was rejecting:
*"worldgen's stair halves are different defs that pair by convention; a bed's halves are the same
thing, and two records would have to agree on quality, owner and removal forever."*

Every word of that is right about a bed and none of it bites a stair. A stair's halves really **are**
different — one sits on the floor, one 1.5 m up, and they face opposite ways — and a stair
**takes no quality and nobody owns one**, so the only thing its two records must ever agree about is
existing and being torn out together. `Raise` and `Demolish` each do that in one call.

What the two-record shape buys is large and entirely negative work: `ChunkMesher.EmitStair`'s
partner scan, `EdificeLabels` (which already maps both indices to `ui.arch.tool.stair`),
`WorldRenderModel.EdificeModule` and the render mirror all keep working **unchanged**, and a stair a
colonist built is indistinguishable from one the generator stamped — which is the property
`WorldGenContext.PlacedEdifice.Built` exists to make deliberate rather than accidental.

### The one new content field

`EdificeFootprint.Cells` and `ConstructionContent.BuildingForEdifice` resolve a building from a
single `edifice` value, so `EdificeStairUpper` would answer `BuildingHandle.None` and report a
one-cell footprint — and the second cell would stop being claimed by its own site. `BuildingDef`
gains **`secondEdifice`**, a ushort defaulting to 0 and meaningful only with `footprint = 2`; both
lookups consult it. One field in the shape `slab` / `covering` / `footprint` already established,
rather than a special case written twice.

## 5. Where a stair may be ordered

Both cells must satisfy `Allows`, and there is **no `StandingOn` lift for a multi-cell order**
(`20-beds.md` §4): if either cell fails, the order fails.

**And the far cell is asked as the thing it is, which it was not before.** `Place` answered for the
second cell with the one-argument `Allows`, which asks a *wall's* question — all a bed's far half
has ever needed, both its halves being the same thing. A stair is the first buildable whose far half
carries a rule of its own: the shaft over that cell has to be open too, and nothing a wall is asked
would notice a slab there. The stricter question is put **only where the halves differ**
(`secondEdifice`), because asking it of the bed as well would newly demand a clear cell of the bed's
far half — a real change, to a different unit, riding in on this one.

Then three rules, all of them the ladder's with the word changed.

1. **A footing under both.** `02-world-and-layers.md` §5: *"The slab under a stair or ladder is
   required — a connector cannot hang in air — and deconstructing it is blocked while the connector
   stands."* The first half is `StandsOnSomething`; **the second half has never been enforced for
   either kind**, and is part of this unit.
2. **The shaft rule, both ways and at the ladder's reach.** The cells directly above a stair are its
   upper end, so they must be open — and a slab must be refused over a standing stair, or the rule
   is walked around in two moves. `ShaftRulePermits` already states this for ladders, already counts
   **blueprints** (the fix a playtest forced, `21-ladders-and-climbing.md` §8), and already reaches
   two cells down to protect the arrival cell from being roofed. Stairs join that method rather than
   getting a second copy of it: **one rule, one owner**, which `docs/bug-patterns.md` has as its
   first pattern.
3. **Arrival, at the connector and not at the order.** A landing is not demanded when the stair is
   placed — the ladder's reasoning holds unchanged, that a player builds a chain from the bottom and
   demanding a landing would refuse every stair but the last.

### Both ends open, one end arriving — and the first cut had this wrong

`ConnectorRegistrar` says of worldgen's own stairs that *"half a stairwell is not a narrower
stairwell, it is a portal whose far end is a hole"*, and the obvious reading of that is **both upper
cells must be arrivable**. It was written that way and it registered **nothing at all**.

**What the measurement showed.** You walk on to the lower half, climb to the upper, and step off at
the *top* of the flight. The cell over the **lower** half is passed through, not arrived in — so
asking it for a landing of its own refuses every stair that does not happen to run alongside a floor
for its whole length. Worldgen never met this because a stamped shell has a real floor over both
cells; the first board that ever had a stair standing in the open was a test written for this unit.

So: **both upper cells must be open** — solid rock, a blocking edifice or water over either half
stops the flight, and that half of the registrar's rule stands — **and at least one of them must be
somewhere to arrive**, by the ladder's own definition of arriving.

**It was caught by one test and nearly not caught at all.** Four of the eleven tests here use
`Assume` for their controls, in the project's own idiom, and a failed `Assume` is reported by NUnit
as **skipped, not failed** — so the tier stayed green while the unit did nothing. The one test with
no `Assume` in it, `EveryModeMayUseAStair`, is what failed. `docs/lessons.md` has the general
version.

## 6. Registering the connector

`RefreshStair` copies `RefreshLadder`'s shape exactly, and the shape is the point: compute `wanted`,
compare it against what the graph holds, add or remove. **Idempotent in both directions**, so it can
be called from every place either end can change without anybody tracking which change it was.

- `RefreshStairsAround` from `MarkNavAround`, beside the ladder's.
- `RebuildStairConnectors` from `ColonyWorld.RebuildDerived`. **This is what makes the unit cost no
  save format at all** — a connector is derived from the edifice list exactly as structural support
  and the region graph are, and `NavGraph`'s lookup is *asked, not remembered*, because a
  cell→connector map would be empty after a load.
- `NavGraph.OneCellConnectorAt` is hard-coded to `LowerCells.Length == 1`, so a sibling that matches
  a whole lower footprint goes beside it rather than widening it — a one-cell question and a
  two-cell question are different questions and a caller should have to say which it is asking.

**The cost keeps its single owner.** `Connector`'s constructor is the only place that names
`MoveCost.StairUp`, and nothing this unit adds may name it again. `HopPriceHasOneOwnerTests` exists
because three seams once disagreed about the price of a hop, and a stair is spared that only because
its connector carries its price as data.

## 7. Delivery becomes a hauling job

`DeliverWorkGiver` takes `TraverseMode.Hauler` for both its scan and its job, copying
`HaulWorkGiver`'s reasoning verbatim: *"the scan tests reachability in the same mode the job will
walk in. A scan that tested a laxer mode would hand out jobs that fail on their first step."*

Two consequences worth stating rather than discovering:

- **A ladder-only upper storey stops being buildable**, which is the whole point, and is why this
  lands in the same commit as the stair rather than before it.
- **`Job.Mode` is saved and hashed** (`Job.cs:68`), so a colony loaded mid-delivery comes back with
  the mode it was saved with. The round trip is a test, not an assumption.

## 8. Test procedure

Modelled on `LadderTests` — **reachability, not edifice-existence, and the control measured first**,
which is the pattern that caught what U43 actually did.

- **`AStairCarriesAHaulerWhereALadderCannot`** — the unit's thesis, with the ladder measured false in
  the same test so it cannot pass vacuously.
- **A colonist cannot deliver material up a ladder and can up a stair.** This is the test whose
  absence let §3's false claim stand in four documents; it asserts the rule is *reached*, not merely
  that it exists.
- A stair needs a footing under both cells; a slab is refused over a standing stair and a stair under
  a slab, **including the two-blueprint case**; roofing the top of a working stair is refused.
- `ABuiltStairIsTwoRecordsThatComeOutTogether` — the §4 claim, from both directions.
- `AWayUpSurvivesASaveAndALoad` — the derived-connector claim.
- `NavGraphStatisticsTests` prints `DistrictCount` per mode: **stairs collapse the Hauler count**,
  which is this unit in one number.
- `PathingTests` already carries a 2→2 stair fixture and a randomised incremental-versus-full
  rebuild oracle over stair connectors. Extend, never duplicate.
- `BedTests.cs:817` is pinned on *"the day stairs land, the same tests will hold one storey higher
  without a line changing."* Check it and say which way it went.

**And no golden moved, which the plan did not expect.** U44 was scoped expecting to re-bake
`Golden.City.Simulated` for the stair and the delivery mode together, and the Long tier came back
21 of 21 with every hash where it was. The reason is worth keeping: **nothing in any golden
scenario builds a stair, delivers building material, or lies down on a terrace**, so all three
changes are invisible to them. A re-bake that turned out not to be needed is a better answer than
one nobody questioned — and `Generated` was never in doubt either way.

**The M2 demo is what caught the real bug**, and it is worth knowing which tests did the work here:
the fast tier stayed green through a version of this unit that tore out **the generator's own
stairwells** on every colony edit. `ThreePawnsLiveInARuinedShellForADay` starved a colonist two
storeys under its food, and `TheStairsInTheDemoAreTheScenariosDoingAndNotTheMaps` fell from nine
stair steps in a day to three. `AColonyEditDoesNotDisturbTheGeneratorsOwnStairwells` now says the
same thing in a quarter of a second.

**By hand, and nobody has done it:** build a stair and watch a colonist carry stone up it; build a
second storey on top of one. **No stair has ever been photographed**, stamped or built — the
playtest queue has never held a stair row.

## 8b. The first play, and the two things it found

**2026-09-21, the owner:** *"I put one stairs to build - and it built two - also I couldn't click on
the stairs either to get any information ... could you check that deconstruct works with it as well
but the stairs seemed bugged. Pillar seemed ok."*

**The flight was drawn as two descending flights.** The heights were right — §4's arithmetic holds,
the art is a half-flight rising 1.50 m over a 2.5 m run, and the upper half is lifted by half a
layer — but `SM_Bld_Base_Stairs_01` ascends toward its own local **−Z**, so yawing each half by its
*climb* direction pointed both of them down it. The pieces diverged instead of meeting: one flight
on the ground and a second floating 1.5 m above and beyond it. `ModuleEntry.yaw = 180` on the three
stair rows corrects it, which is what that field is for, and it corrects worldgen's stamped
stairwells at the same time — they had the same fault and nobody had ever looked at one either.

**Nobody could click a stair.** It does not occlude, so the only surface it offered a ray was the
floor of its own cell, and it is drawn *climbing* — so at the play camera's 48° the whole flight sat
in front of the cells that answered for it. `docs/design/20-beds.md` had this exact fault in
September and `StandHeight` was written for it; its comment said *"the next non-occluding thing that
stands up adds a line here"*, and a stair is that thing. It is also **the first that is not flat**,
so it needs two numbers rather than one, and both the mesher and the picker now read them from
`StairShape`:

| | drawn from | drawn to | offers the picker |
|---|---|---|---|
| lower half | the cell floor | half a layer | its floor, and a plane at 1.5 m |
| upper half | half a layer | the next floor | a plane at 1.5 m, and one at 3.0 m |

**One plane cannot be a ramp**, and the measurement is worth keeping: with only the top of each run
offered, the far two thirds of the *upper* half stayed unclickable, because its floor is 1.5 m below
where its art starts — a ray crosses the top plane before the cell and the floor plane after it, and
falls through to the ground behind. `WorldRenderModel.StandFoot` is the second plane and the two
bracket the climb. `StairPickHeightTests` aims at every tenth of a drawn flight.

The deconstruct mark follows for nothing, because `MarkHeight` reads `StandHeight`: an order to take
a stair apart is painted on the flight rather than buried under it.

**Deconstruct was checked and was already right.** `EitherHalfTakesTheWholeStairApart` proved the
rule by calling `Demolish` directly; `AMarkedStairIsPulledDownWholeByAColonist` now proves the
gesture — mark either half, a colonist walks to it and finishes it, both records go, both marks
clear and the connector goes with them. One correction to the obvious assertion: **the landing does
not become unreachable**, and expecting it to was wrong. It is the top of a single wall, so a
colonist can get on to it with a one-block hop whether a stair was ever there or not. The claim
worth asserting is that the *portal* went, and it does.

## 8c. The second review, and the one thing it found

**`Demolish` took its neighbour down with a stamped stair.** U44 taught
`EdificeFootprint.Cells()` to answer 2 for either stair half *whoever* stamped it, which is right —
a stamped stair and a built one are deliberately the same thing to the mesher and the graph. But a
stamped stair carries **no facing**; `RefreshStair` says so in as many words and guards itself
against it, and `Demolish` did not. So `SecondCell` derived from Facing 0 named whatever lay north
of the stair, and `Demolish` cleared that cell and flagged its record `Removed`.

**Measured, because the first guard was half a fix.** Marking only the record was tried first and
the test still failed on the other assertion: `_grid.RemoveEdifice(second)` runs before any of it,
so the neighbouring wall was *gone from the world* while its record still said it stood — the worse
half of the two. The gate belongs at the derivation. `IsTheOtherHalfOf` answers it once: the same
record (a bed, whose two cells point at one handle), or a record whose def is the opposite stair
half. Anything else and `second` is -1 and the whole second-cell path is skipped.

**Nothing reaches it through the gesture today**, because `DesignationGrid.CanDeconstruct` refuses
anything the colony did not build — which is why no tier caught it and why the fix is a guard
rather than a bug fix. But nothing ties that refusal to this method, and
`DemolishingAStampedStairLeavesTheNeighbourItPointsAtStanding` does not go through the gesture,
deliberately: the claim is that `Demolish` is safe on its own terms. Same argument as the unchecked
index on the stair fan-out that this branch's first review found, and the same shape as
`docs/bug-patterns.md` P1 — one rule with two owners, where the second owner was three files away.

**And the probe was photographing eleven chunks.** Re-shot after the merge, `stair-play.png` showed
a meadow and no stair, while `stair-side.png` from the same run was perfect — `MeshBudgetPerFrame`
came in with `main` and a probe gets one `camera.Render()`. `PrimeAll` in the render hook fixes it,
and the empty-first-picture-full-last-picture progression is the tell rather than the camera.
`docs/lessons.md`. **Both pictures are now right**: one continuous flight, from the profile and from
the play camera's 48°.
## 9. Open

- **`EmitStair` infers facing by scanning for its partner**, which `20-beds.md` §93 says a built
  thing must never do — *"the stairs infer only because worldgen had nowhere to put an answer, and
  placing is exactly where the answer is known."* It is unambiguous while the two halves differ and
  ambiguous for two stairwells side by side. `WorldRenderModel.LadderFacing` is the precedent for a
  stored-facing branch. **Recorded, not fixed here.**
- **One stairwell per storey** is worldgen's recorded limitation (`SurfacePasses.cs:275-277`). A
  player building two stairs on one storey must get two connectors, not one four-celled thing — the
  ladder's "two ladders are two connectors" rule, applied. A built stair registers its own two-cell
  connector, so this holds; nothing tests two built stairs on one storey yet.
- **The generator's stairs are left entirely alone** (`RefreshStair`'s first line), because a
  stamped stair has no facing to derive its partner from. The consequence is that a colonist
  **cannot deconstruct a stamped stairwell and have the connector go with it** — Reclaim is the
  line that will have to answer that, and it is not this unit.
- **Deconstructing the slab under a standing connector** is blocked by this unit for stairs. The
  ladder has the same sentence in `02` §5 and the same gap; closing it for ladders too is a line,
  and it is not done here.
- **Fall damage** still has nothing to apply itself to, so stepping off the top of a stair into a
  shaft arrives unharmed, exactly as a ladder does.
- **No climb rate.** A stair costs 290 up and 230 down flat, like everything else; a rate belongs
  with WS if it is ever wanted, which is the answer `21-ladders-and-climbing.md` §5 already gave for
  the ladder.
