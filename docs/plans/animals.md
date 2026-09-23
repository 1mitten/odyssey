# Animals — the plan

**Status: written 2026-09-22, awaiting the owner's approval. No gameplay code has been
written.** Phases 0–2 are on disk: `docs/research/e-08-animal-fbx-inspection.md` (the two
models, and what Unity made of them), `animals-interview.md` (the eight answers),
`a-09-animals.md` (the data shape and the wild think tree), `c-procedural-quadruped-gait.md`
(the computed walk). Branch `claude/animals`, worktree `D:\code\odyssey-animals`.

## 1. What is being built

The owner's MVP, in their words: **spawn, wander, draw, click.** Two animals — a pig and a
rat — exist on the board as pawns of a new kind, walk the nav graph under per-species
movement rules, are drawn through the figure path the colonists use, and show a pane when
clicked. They arrive only from the debug menu. Nothing dies, nothing is eaten, nothing is
tamed. Everything after that is a named later unit (§7), and the next of them is the health
model.

## 2. What the ground settled

| Fact | Consequence |
|---|---|
| Both models are CC0 (owner) and are **already committed** at `Assets/Art/Custom/Animals/` with the probe that measured them (`Assets/Editor/Odyssey/AnimalProbe.cs`). | The sim, the tests and CI may depend on them. **They resolve on the runner**, unlike every Synty figure, so the first figure test that runs on CI can be an animal's. |
| They imported at **11.4 m** and **7.1 m** long (bone positions, not the bounding box, which lied by a hundred). The importer now carries **×0.105** and **×0.09**, which is a 1.20 m pig and a 0.64 m rat with its tail. Photographed on 2.5 m cells: `docs/reference/screenshots/2026-09-22-animal-sheet.png`. | The size is decided in the importer's `.meta`, once. **But a colonist here stands 2.49 m** (`20-beds.md` §7b), so life size is two thirds of the proportion a player expects. The first playtest question is whether the pig reads small; the fix is one number in `AnimalProbe.ImportScale`. |
| The rat has Idle, Walk (1.33 s), Run (0.50 s), Jump, Attack and Death. The pig has Idle (6.25 s) and Jump. No clip has root motion; none is flagged to loop. | The rat's Walk and Run go into the same gait mixer the colonists use. The pig walks on a **computed gait**. Loop flags are set at import. |
| `Pawn` is documented as a colonist; there is no species. But `PawnKindDef` already exists (it carries the colonist's needs tuning), `WanderJobDriver` and `WanderTarget` already exist (the mental break uses them), and `TraverseMode.Animal` — *no ladders, no manipulable doors* — **already exists in the nav graph** beside Colonist, Hauler and IgnoreDoors, with a mask on every link. | An animal is a pawn with a kind. The pig's movement rule is one existing enum value. The rat's is `Colonist`. The wander is a job the system already drives. |
| The debug menu already has *Spawn colonist* (`IntentKind.SpawnPawn`), which finds the walkable cell nearest the camera's column. | *Spawn pig* and *Spawn rat* are the same intent with a kind. |
| The figure director builds one `AnimationMixerPlayable` per figure over the catalogue's `locomotion` entries, each with a measured `metresPerSecond`, and keeps the nearest 64 alive (`FigureCeiling`, a hard ceiling). | A species is a catalogue entry with a prefab and gaits. Animals count against the same 64. |
| **`PawnPose.Of` scans every other pawn for the crowd sidestep, once per posed pawn per frame** (P11, open, 13 ms at 384 colonists). | Animals are pawns and would join that scan on both sides. **Either P11 lands first or animals are excluded from the crowd scan** — the plan takes the second, because a pig sidestepping a colonist is not a behaviour anyone asked for. |
| The names exist. `proper-nouns.csv` already calls the pig the **midden hog** (`creature.scavenger`: "pig-descended, thrives on refuse heaps, tameable; the one you meet first") and `ui.pawn.animal` is "Animal". The rat has no entry. | The wiki row for the rat is content this unit adds. Proposed, for the owner to correct in the CSV: **duct rat** (`creature.vermin`), register matching *midden hog* and *girder cat*. |
| Save format is **8 on `main`** and **9 on the temperature branch** in review. | This unit bumps the format *after* temperature merges: **rebase on to `main` once TE is in and take 9 → 10.** Merging first would collide on the number. |

## 3. Design decisions the design doc must record

`docs/design/29-animals.md` is written at the start of execution, per `docs/process.md`, and
holds these. They are stated here so approval covers them.

1. **An animal is a `Pawn` with a `Kind`.** `Pawn.Kind` is an index into the kind table; the
   colonist is kind 0 and every existing save reads as kind 0. It is saved and **hashed**,
   which moves every golden by the hash seeing one more field; the re-bake is measured to be
   that and nothing else, as the rates work did.
2. **Two Defs, following a-09.** A `SpeciesDef` says what the animal *is*: label key, body
   length in millimetres, move rate per mille of the colonist's, its `TraverseMode`, its wander
   radius in cells, its rest between legs in ticks, and its figure key. `PawnKindDef` gains a
   `species` reference; the colonist's species is a `Person` with the `Colonist` mode, so
   nothing about a colonist changes. The kind is what spawns; the species is what walks.
   Wildness, ecosystem weight and commonality are **not** added until something reads them.
3. **The animal think is not the colonist think.** `JobSystem` skips any pawn whose species
   is not a person for work, needs, mood, skills and schedule — they have none — and gives it
   an `AnimalMind` instead: rest for the species' interval (jittered off the pawn's own seed),
   pick a reachable cell within the radius through `WanderTarget`, walk it with
   `WanderJobDriver` under the species' `TraverseMode`, repeat. Per `process.md` §3 it scales
   with the number of *idle animals* on a cadence, never with the board.
4. **Rats climb, pigs do not.** The species' mode is passed on the `Job`, exactly where the
   hauler's is today. A pig at the foot of a ladder is refused the link by the mask that is
   already on it. Stairs (`U44`) are not built; when they are, the `Animal` mode allows them
   and nothing here changes.
5. **The figure is the colonists' figure path with a species entry.** The catalogue gains
   an `animals` list: prefab, gaits with measured metres per second, a scale (1, since the
   importer owns it), the bone names the computed gait drives. The director resolves an
   animal's entry by species key instead of by appearance seed, and builds the same mixer.
   **No portrait**, no recolour, no swatches.
6. **The pig's walk is `QuadrupedGait`**, a presentation class in the manner of `WorkSwing`:
   lateral-sequence phases (0°, 90°, 180°, 270°), duty factor 0.65, hip swing ±25°, knee
   ±35° peaking mid-swing, bob ±2–3 cm at twice stride rate, cycle time from the pawn's
   actual metres per second so the feet do not slide. It writes bone rotations in
   `LateUpdate` after the mixer has played Idle, so the idle breathing survives underneath.
   Judged by playing it beside the rat's authored Walk.
7. **Animals are outside the crowd sidestep.** `PawnPose` neither steers an animal around a
   colonist nor a colonist around an animal in this unit. Recorded against P11.
8. **The pane** is the colonist pane's shape with fewer rows: the species label from the
   registry, the activity (`Resting` / `Wandering`), the layer. Selecting an animal does not
   move the roster's selection. No alerts, no roster entry, no Work tab row.
9. **Nothing in presentation is in a cell, a save or the hash** — the gait, the scale and the
   figure budget are drawn, never simulated. The species Def is content and is fingerprinted.

## 4. The units

| Unit | Builds | Tier that proves it | Scale rule |
|---|---|---|---|
| **AN1 species and kind** | `SpeciesDef`, `Pawn.Kind`, the person species, `Species.xml` beside `Colonist.xml`, content fingerprints, save format bump, the hash. | Fast: Def loads and fingerprints; a kind-0 pawn hashes as before except for the new field; round-trip a save with a pig and a rat; **every golden re-baked and the diff explained**. | none per tick |
| **AN2 the animal mind** | `AnimalMind`, wander under the species mode, rest cadence, `JobSystem` skipping animals for everything else. | Fast: a rat reaches a cell up a ladder and a pig at the same start does not (`TraverseMode` on the request); a wander never leaves the radius or the reachable set; 200 animals at rest cost a bounded number of path requests a day; same seed → same hash with animals on the board; **the ten-day headless run with 20 animals ends clean on three seeds.** | idle animals, on a cadence |
| **AN3 spawn and names** | `IntentKind.SpawnAnimal(kind)`, two debug rows through `Registry.Label`, `icon-keys.csv` and `proper-nouns.csv` rows for the rat, both `--check` gates, wiki republished. | Fast: `RegistryTests` for the keys; Hud test that the rows raise the intent; the two content checks. | — |
| **AN4 the figure** | Catalogue `animals` entries (an editor script writes them, as every catalogue entry is), URP materials for the two flat-colour meshes, the mixer from Idle/Walk/Run, `QuadrupedGait` for the pig, animals under the 64 cap, out of the crowd scan. | Unity EditMode: both species resolve on this machine **and on the runner**; the gait's feet never cross the floor at the walking speed; PlayMode: `FrameTimeTests` with 64 animals walking against the same-run control. | figures ≤ 64 |
| **AN5 click and pane** | Pick an animal, the pane, the registry labels for its two states. | Hud: the pane model from a snapshot; `NoPlayerFacingNameIsWrittenInCSharp`. Playtest: the click lands. | — |

AN1–AN3 are sim and HUD and are proven by the fast tier in a session; AN4 needs Unity and is
the session after; AN5 is small and rides with AN4. Nothing is complete until the handover
tables are written and a row is on `docs/plans/playtest-queue.md`.

## 5. Gates before the PR

- Fast tier, Long tier, both content checks, `emit_labels.py --check`.
- Unity EditMode on this machine; PlayMode alone, with no other Unity batch running.
- The player build boots into a colony with a pig spawned (`-odyssey-newgame`) and draws it —
  the flat-colour materials are the kind of thing a stripped shader loses silently.
- Goldens: re-baked once, at AN1, with the diff explained in the journal.

## 6. What the first playtest decides

| Question | A wrong answer looks like |
|---|---|
| Scale against a 2.49 m colonist | the pig reads as a piglet, the rat is invisible at play height; the fix is one number each in `AnimalProbe.ImportScale` |
| The computed walk beside the authored one | the pig slides, bounces or paddles; the rat is the control |
| Flat colour beside the Synty atlas | they read as a different game; the answer is a tint pass or a pack purchase, not a rebuild |
| A rat up a ladder, a pig refused at the foot | the pig waits at the ladder for ever rather than picking another target |
| A wander that reads as living | the animal stands still for minutes or paces a corner |

## 7. Later units, named so they are not forgotten

In the order the interview implied: **health model** (alive / downed / dead for every pawn;
next), **hunting to meat** (hunt order, carcass item, meat commodity), **taming and pens**
(tame order, animal food, a fenced zone, ownership), **the vermin loop** (rats path to a
stockpile and eat), **the threat** (a rat bites; the Attack clip is there), **flee** (the
override branch a-09 describes), **worldgen scatter** and the **arrival incident** (the
`IncidentDef` seam is ready), **animal beds**, and a **scale test** at 300 animals against the
figure budget and P11.

## 8. Merge order

1. **Temperature** (`claude/temperature-core`, format 9) merges first — it is in review now.
2. **Animals** rebases on to it and takes format 10.
3. The P11 sidestep fix is independent; if it lands before AN4, decision 7 becomes a
   one-line inclusion instead of an exclusion.
