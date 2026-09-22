# 29 — Animals: a pawn with a kind

**Status: being built, 2026-09-22**, against `docs/plans/animals.md` (approved the same day).
Ground: `docs/research/e-08-animal-fbx-inspection.md`; interview: `animals-interview.md`;
research: `a-09-animals.md`, `c-procedural-quadruped-gait.md`. This document holds the
decisions, the measurements that made them, and what not to undo by tidying.

## 1. What an animal is

**An animal is a `Pawn` with a `Kind`.** The kind is an index into the content's kind table;
the colonist is kind 0 and every pawn that existed before this unit reads as kind 0. A kind
names a **species**, and the species is what walks: its traverse mode, its pace relative to
the colonist's, its wander radius and its rest between legs. The kind is what spawns; the
species is what it is (a-09 §1).

Two Defs, in `Assets/Odyssey/Defs/Core/Pawns/Species.xml`:

| Def | Holds | Today's rows |
|---|---|---|
| `SpeciesDef` | `labelKey`, `person`, `bodyLengthMm`, `movePerMille`, `traverseMode`, `wanderRadius`, `restTicksMin`/`Max`, `figureKey` | `Species_Person`, `Species_MiddenHog`, `Species_DuctRat` |
| `PawnKindDef` (existing) | the colonist's needs tuning, plus a new `species` reference | `PawnKind_Colonist`, `PawnKind_MiddenHog`, `PawnKind_DuctRat` |

**Kind order is a save contract** and is appended, never inserted, exactly as job and item
handles are: `PawnContent.FromDefs` lists the kinds by name and the list *is* the order.
Wildness, ecosystem weight and commonality are not added until something reads them.

**Why not a second class.** `Pawn` already carries the position, the path buffer, the job
slot, the hash plumbing and the save record an animal needs; `TraverseMode.Animal` was in the
nav graph before any animal was; the wander job and its driver already existed for the mental
break. A second class would have duplicated the pathing and the save code to avoid a
`person` flag.

## 2. What a person does that an animal does not

Every pawn-wide system asks `pawn.IsPerson` once at the top of its loop, and an animal has:

- **no needs tick, no mood, no mental break** — `NeedsSystem` skips it, so the values it was
  constructed with never move;
- **no skills** — `SkillSystem` and `StartingSkillsSystem` skip it, and nothing rolls
  passions for it;
- **no work and no schedule** — the think tree it consults is not the colonist's (§3), so it
  never sees a work giver;
- **no roster card, no Work tab row** — the roster reads the view's kind;
- **the same movement, doors, falling, eviction and trapped-pawn rules** as a colonist,
  because those are about being a body in a cell.

The published `PawnView` carries the kind so that presentation and the HUD can tell them
apart without a second channel; the skill, work and schedule aspects are not published for
an animal, and the move rate is, because the figure's gait speed comes from it.

## 3. The animal mind

`JobSystem.Think` hands an animal the **animal tree** instead of the colonist's: one node,
`AnimalIdleThinkNode`. On every think it rolls once on the pawn's own stream
(`PawnPurpose.AnimalMind`, keyed on world seed, tick and pawn id) and either **wanders** — a
reachable cell within the species' radius, through the same `WanderTarget` the mental break
uses, under the species' traverse mode — or **rests** for a jittered span between the
species' two rest bounds, which is a `Wait` job. Roughly two legs in five are walks. It scales
with the number of animals that are *between jobs* on a tick and with nothing else; a resting
animal costs one integer increment a tick.

The circuit breaker applies to animals as to anyone: an animal with nowhere reachable to go
falls into the stand-down, which is a rest by another name.

### 3a. A job ends on a cell, never between two

**An animal's job expires at the next cell boundary** (owner, 2026-09-22: a hog *"went past the
tree, then suddenly appeared before it again, snapped/teleported back and walked through it
again"*). Ending a job drops the step in progress — `ClearPath` zeroes the move progress — so
the simulation puts the pawn back on the cell it was leaving while its figure had been drawn
most of the way into the next: a snap of up to a cell, backwards. The wander's expiry is 1,200
ticks and a hog's eight-cell leg at its pace can be longer, so the expiry landed mid-step. The
snap detector (`AnimalProbe.Snaps`: a wooded colony, six animals, a hundred seconds with a frame
between every pair of ticks, every drawn position recorded) found four in a hundred seconds, all
at the tick a wander expired. `TickPawn` now lets an animal's expired job run on to the next
cell — at most one step late — and the detector finds none. `AnAnimalsJobNeverEndsMidStep`
holds the invariant: every job an animal starts begins with its move progress at nought.

Colonists keep the old rule for now, deliberately: their wander is the mental break's, and the
change would move every golden. **They have the same snap**, at the end of a break, and it is
recorded as a known gap.

## 4. The third dimension

**Rats climb anything; hogs never take a ladder; neither swims.** The species' `traverseMode`
goes on the job the animal is walking, exactly where the hauler's `Hauler` mode goes today, and
`Pawn.Mode` answers the species' mode when there is no job. The pathfinder already carries a
mode mask on every link and every portal edge, so a hog at the foot of a ladder is refused
the link by the mask that was there before this unit. The hog's mode is `TraverseMode.Animal`
(no ladders, no manipulable doors, and now no water); the rat's is the new `Climber` — as
`Colonist`, but no water. Stairs (`U44`) are not built; the day they are, both modes allow them
and nothing here changes.

**An animal hops only where a ramp is drawn** (owner, 2026-09-22: *"saw a pig climb a
stone/mine - guard them from climb up rocks/mines"*). The one-block hop is the whole of unaided
vertical movement, and a person takes it anywhere the upper end is a block top: up a mined face,
on to a rock, over the edge of a cut. An animal takes it only where the lower cell is the foot
of a terrace step — the cell `TerraceFoot` says a bank is drawn in, whose cost class is the
slope's — because that is the one place the ground is drawn as something four legs could climb.
`NavGraph.HopMask` is the one owner: the region link, the step check and the search all ask it.
Digging out a step's floor turns it into a cut face and closes it to animals in the same
rebuild; `AnAnimalHopsOnlyWhereARampIsDrawn` does exactly that, with a colonist as the control.

**No animal swims** (owner, 2026-09-22: *"animals can't swim by default, especially rats and
pigs"*). `TraverseModes.Swims` says which modes may enter shallow water — people wade (design
20), the two animal modes do not — and `NavGrid.CanEnter` refuses the cell. So that the refusal
is known to the district rather than found by a failed search, shallow water is a **region kind
of its own** (`RegionKind.Water`): the links into it are masked by the same `CanEnter`, so a
non-swimmer is told the far bank of a stream is unreachable and never sets off for it.
`NeitherAnimalWadesAndAColonistDoes` paints a stream across the board and asserts all three:
the person may wade it, neither animal may enter it, and over six thousand ticks neither ever
stands in it. Deep water was already impassable to everyone.

**An animal never ends a leg on the foot of a terrace step** (owner, 2026-09-22: they rested on
one, drawn on the ramp, and snapped to the lower floor when they set off). `WanderTarget.Fill`
refuses a cell whose cost class is the slope's as a *destination* when asked to
(`avoidSlopes`), which the animal mind always does; walking through one is unchanged. A rest is
taken where the last leg ended, so it cannot begin on one either — `AnAnimalNeverEndsALegOrRests
OnATerraceFoot` raises a step and watches twelve thousand ticks. Colonists' wander is left as it
was: it is the mental break's, and moving it moves every golden; the standing figure on a foot
cell is already a recorded gap for them.

## 5. Pace

`Pawn.MoveRatePerMille` gains one factor, the species' `movePerMille`, in the design-17
product after pace and condition. The person's is 1,000, which is exact in integer
arithmetic, so no colonist's speed moved. The hog's is 700 and the rat's 900: a hog
ambles, a rat is lively. Both are a playtest question and both are one number in the XML.

## 6. The save

**A section of its own, `odyssey.pawn.kinds`, keyed by pawn id — and no format bump.** The
plan said the format would move; the codebase's own precedent, `PawnSeedSection`, says why
it need not: a section is skippable in both directions, a save written before animals has
no section and every restored pawn keeps kind 0, which is what that colony was. The pawn
record's layout is untouched, so nothing forces a version. This also removes the collision
with the temperature branch's format 9.

**The kind is hashed**, in `Pawn.ContributeTo` beside the roll seed, for the same reason
the seed is: saved state that is not derived belongs in the hash. Every Simulated golden
moved by the hash seeing one more zero; the re-bake was measured to be that and nothing else
(§9).

## 7. Arrival

The debug menu only (owner). `IntentKind.SpawnPawn` gains a meaning for `A`: the kind. Zero
is the colonist it always was, so nothing that sends the intent today changed; an unknown
kind is `NotPermitted`. The column rule is unchanged — the animal lands on the walkable cell
nearest the camera's column. The debug menu gained a **Spawn** tab (owner, 2026-09-22) holding
*Spawn colonist*, *Spawn midden hog* and *Spawn duct rat*, through the registry.
`AnimalSpawnTests` (PlayMode) proves the whole path under the real bootstrap with the
catalogue attached: the intent, the tick, two pawns of the right kinds, and a figure for each. Worldgen scatter and an arrival incident are the recorded follow-ups.

## 8. Names

The hog was already named: `creature.scavenger` in `proper-nouns.csv` is the **midden hog**.
The rat is new: **duct rat**, `creature.vermin`, proposed in the same register (*midden hog*,
*girder cat*) for the owner to correct in the CSV. The interface labels are `ui.pawn.hog` and
`ui.pawn.rat` in `icon-keys.csv`, which is what `Registry.Label` reads; the proper-noun row
is the lore entry and says the same words.

## 8a. The figure

An animal is drawn through the colonists' figure director, not a director of its own. The
catalogue gains one row per kind under `ModuleIds.Animal(kind)` — `pawn.animal.hog`,
`pawn.animal.rat` — written by the same builder as the colonist rows and resolved from
`Assets/Art/Custom` rather than from the packs, so **these rows resolve on the runner**, which no
colonist row does; `AnimalFigureTests` is the first figure test CI can build. The director keeps
a second look table indexed by kind; a look index at or past the colonist table names an animal,
so the pool, the create, the repaint and the blend all key on one integer.

**The rat walks on its clips**: Idle, Walk and Run in the same mixer a colonist has, with the
walk and run speeds *declared* (0.9 and 2.2 m/s) because neither file carries a root-motion twin
to measure them from. **The hog trots on a computed gait**, `QuadrupedGait`: the row asks for it
and its locomotion is the idle alone; the gait binds the four legs by the rig's bone names at
build, **measures the leg** from shoulder or hip joint to sole, advances its phase once a frame
from the figure's measured speed, and lays forward-kinematic sines over the idle in the pose pass
— exactly where `WorkSwing` lays an axe stroke. A rig without the four legs gets no gait and
moves on its idle, as a colonist without bound arms swings no axe.

**Why a trot, and why the stride is measured** (owner, 2026-09-22: *"the pig walking looks
awful"*). The first version was a lateral-sequence walk cycling once per authored metre. The
probe's joint report says the rig's legs are **23 cm** from shoulder joint to sole on a 1.2 m
body; a 23 cm leg swinging 25° covers about 20 cm a cycle, so the feet slid over most of every
stride while the legs waved slowly, with a 2 cm bob at the same slow rate. A short-legged animal
at a metre a second does not walk — it trots, on diagonal pairs (left fore with right hind), with
quick short steps — so the gait is the trot, the stride is **derived** as twice the measured leg
times the sine of the 30° hip swing (the ground one leg covers in its stance), times a
`SlideFactor` of 2 that admits a model this squat must either scurry or slide and splits it:
**0.46 m a cycle**, about 2.2 cycles a second at the hog's pace. The knees are **signed by
anatomy** — a fore leg folds its carpus back under the body in the swing, a hind leg's hock
flexes the foot forward — and the idle clip underneath is **frozen as the gait fades in**, or its
weight-shifting reads as noise under the trot. Judged from a four-phase side-on strip
(`docs/reference/screenshots/2026-09-22-hog-trot-strip.png`); the numbers are still playtest
numbers, and `SlideFactor` is the one to move first. **Third look** (owner, 2026-09-22:
*"twisting in one spot when it should be taking steps with its legs"*): the hip swing went from
28° to 40° and the knee from 25° to 35°, with `SlideFactor` lowered from 2.5 to 1.6 so the
cadence stays at about 1.7 cycles a second — the limbs have to be seen to move from the play
camera, and ten centimetres of foot travel was not. What reads as twisting is also the turn
before each leg: a wander picks a new heading every few seconds and the figure turns on the spot
to face it before the legs carry it. A walk clip for the pig would still be better than any of
this.

**The legs are written from their rest, never pre-multiplied** (owner, 2026-09-22, second look:
*"the legs are spindles ... too thin"*, with a screenshot of legs drawn as rods longer than the
body). The first pass pitched each bone onto whatever rotation it already had, as `WorkSwing`
does for a colonist, which is safe only while the clip underneath rewrites every bone before
every pass; under the game's own loop, with the idle held at speed nought under the gait, the
pitches compounded and the legs wound into rods. The gait now captures each driven bone's rest
at bind and writes the pose absolutely, so nothing the clip did or did not write that frame can
reach it. `AnimalProbe.ShootMoving` is the instrument: a real colony, a real hog, the director's
own animator, three seconds of trot, leg lengths printed every twenty frames and a photograph at
the end (`docs/reference/screenshots/2026-09-22-hog-moving.png`). The lengths hold to the
millimetre. **"Way too fast"** in the same look moved the hog's pace from 700 to 600 per mille
and `SlideFactor` from 2 to 2.5: about 1.7 cycles a second at 0.9 m/s.

**The model is not the problem, and a walk clip would still be better.** The stills at rest show
the pig as it was made; every fault so far has been in what was laid over it. The supplied file
carries only Idle and Jump. If the pack it came from has a walk or a trot for the pig — the
author's animal sets usually ship one per animal, sometimes as a separate file — the catalogue row
takes it as a gait beside the idle and the computed trot is switched off by its one flag.

**A Generic rig gets everything a Humanoid one gets except the poses that need named human
bones**: no work stance, gesture, climb, carry, footing or gaze. It is measured in its own height
window (a rat is a quarter of a metre and the colonist window would call that a failed bake) and
does not move the contact sheets' maxima. No swatches: an animal is drawn in its own paint.

## 8b. The cursor and the click

An animal is bracketed and clicked as **its own drawn box**, not as the person-sized column a
colonist gets (owner, 2026-09-22: the column round a hog highlighted the whole tile). The box is
the union of the figure's renderer bounds in its own frame, measured once at build — a loose
box that does not breathe with the trot, which is what a cursor wants — and the bracket is drawn
turned the way the animal faces, with the item bracket's margin. The click box is the same box
axis-aligned at the longer of its two footprint sides. `PawnFigureDirector.TryGetAnimalBox` is
the one owner of both; a colonist's bracket is unchanged. The hog's box is 0.40 × 0.61 × 1.20 m,
the rat's 0.15 × 0.26 × 0.64 m with its tail; `AnAnimalsCursorBoxIsItsOwnSizeAndAColonistsIsNot`
pins both.

**The bake lied about these rigs, so the box does not use it.** `FigureBuild.Height` bakes the
posed mesh, which on these Blender "units scale" rigs reports a hundredth of the truth (the
register entry of 2026-09-22); the renderer bounds were the reading the picture agreed with, so
an animal's standing height comes from the box as well.

**Two deliberate gaps.** An animal past the figure cap is **not drawn at all** — the instanced
baked pass deals every pawn a colonist's face, and a hog wearing one would be worse than no hog
— so a baked animal pose is a later unit. And animals are **outside the crowd sidestep** on both
sides (P11 in `bug-patterns.md`'s frame-cost sense, the plan's decision 7).

## 9. Measurements

- **Import scale** (`e-08`, and the register entry of 2026-09-22 in `bug-patterns.md`): the bones
  said 11.39 m and 7.07 m; the baked mesh said a hundredth of that and was wrong; ×0.105 and
  ×0.09 stand them life-size on a 2.5 m cell (`docs/reference/screenshots/2026-09-22-animal-sheet.png`).
- **Goldens**: all six re-baked once for the kind in the hash. `GoldenColonyProbe` run on
  `main` and on the branch, same file, **diffs clean in every number** on all three boards.
- **Fingerprint**: moved for `SpeciesDef` and `PawnKindDef.species`; taken from a freshly
  loaded pack.
- **The ten-day gate**: twenty animals on the played board for ten days on three seeds, every
  one standing somewhere it can stand at the end and having done nothing but walk and rest
  (`AnimalTests.TwentyAnimalsSurviveTenDays`, Long tier, 77 s for the whole tier).
- **A day of one hog**: between twenty and six hundred job starts, bounded by the rest bounds
  (`ARestingAnimalCostsNoPathRequest`).

## 10. What not to undo by tidying

- Do not fold `SpeciesDef` into `PawnKindDef` because there are only three of each; the split
  is what lets a corpse, a hunt and a taming all point at one species later.
- Do not "simplify" the animal tree into a branch at the top of the colonist's: a node that
  returns false for a person is a node every colonist evaluates every think.
- Do not hash the kind only when it is non-zero to spare the goldens; a hash that reads a
  field conditionally is a hash with a blind spot.
- Do not give an animal needs "for later"; the day it eats is the day the loop is designed.

## 11. Later, named

Health model (next), hunting to meat, taming and pens, the vermin loop, the threat, flee,
worldgen scatter, the arrival incident, animal beds, the 300-animal scale test.
