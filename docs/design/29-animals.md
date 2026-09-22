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

## 4. The third dimension

**Rats climb anything; hogs never take a ladder.** The species' `traverseMode` goes on the
job the animal is walking, exactly where the hauler's `Hauler` mode goes today, and
`Pawn.Mode` answers the species' mode when there is no job. The pathfinder already carries a
mode mask on every link and every portal edge, so a hog at the foot of a ladder is refused
the link by the mask that was there before this unit. The hog's mode is the pre-existing
`TraverseMode.Animal` (no ladders, no manipulable doors); the rat's is `Colonist`. Stairs
(`U44`) are not built; the day they are, `Animal` allows them and nothing here changes.

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
nearest the camera's column. Two rows, *Spawn midden hog* and *Spawn duct rat*, through the
registry. Worldgen scatter and an arrival incident are the recorded follow-ups.

## 8. Names

The hog was already named: `creature.scavenger` in `proper-nouns.csv` is the **midden hog**.
The rat is new: **duct rat**, `creature.vermin`, proposed in the same register (*midden hog*,
*girder cat*) for the owner to correct in the CSV. The interface labels are `ui.pawn.hog` and
`ui.pawn.rat` in `icon-keys.csv`, which is what `Registry.Label` reads; the proper-noun row
is the lore entry and says the same words.

## 9. Measurements

- **Import scale** (`e-08`): the bones said 11.39 m and 7.07 m; the baked mesh said a
  hundredth of that and was wrong; ×0.105 and ×0.09 stand them life-size on a 2.5 m cell.
- **Goldens**: re-baked once for the kind in the hash. *(Filled in when measured.)*
- **Fingerprint**: moved for `SpeciesDef` and `PawnKindDef.species`; taken from a freshly
  loaded pack.

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
