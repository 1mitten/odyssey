# 66 — The forest animals' art: import, forms, scale, clips and the far form

**Status: designed 2026-09-26, nothing built.** Branch `claude/forest-animals`, worktree
`D:\code\odyssey-forest`. The presentation half of the forest animals unit
(`docs/research/forest-animals-interview.md`): **FA1** brings the pack in and draws nine species
walking at life size; **FA2** adds the far form so no animal vanishes past the figure ceiling.
The behaviour is designed elsewhere (temperament, signatures); this document owns only what a
player sees of an animal's body and what it costs to draw.

Grounding: `docs/research/e-15-simple-forest-animals.md` (the pack, measured from its files),
`e-08-animal-fbx-inspection.md` (the same questions asked of the rat and pig), design 29 §8a (the
figure and the far-form gap), design 30 §8 (the frog: a row, a paint, a gait flag), and design 62
on `origin/claude/pig-butcher` (the precedent for licensed Synty art on a pawn kind).

## 1. What we are drawing

Synty's **SIMPLE Forest Animals**, Asset Store EULA: usable in the shipped game, not
redistributable. **It is licensed art under the same rule as every Synty pack** — it lives under
the gitignored `Assets/Synty/`, it is never committed, and the game and its tests build and run
without it.

| FBX (rig, joints) | Species → forms | Colourway prefabs | Tris |
|---|---|---|---|
| `Rabbit.fbx` (RabbitRig, 38) | rabbit | `Rabbit_01..03` | 702 |
| `Deer.fbx` (Horse_Rig, 42) | deer → doe, stag; moose → cow, bull | `Doe_01..02`, `Stag_01..03`, `Moose_Female_01..02`, `Moose_Male_01..03` | 790–1,042 |
| `Fox.fbx` (GoatRig, 52) | fox, raccoon, skunk, wolf | `Fox_01..03`, `Raccoon_01..02`\*, `Skunk_01..03`, `Wolf_01..03` | 846–1,068 |
| `Boar.fbx` (PigRig2, 32) | boar | `Boar_01..03` | 668 |
| `Bear.fbx` (CowRig, 44) | bear | `Bear_01..03` | 874 |

\* `Raccoon_03` is `Raccoon_01`'s colourway again (a pack defect), so the raccoon has two.

Three facts shape everything below:

- **Every prefab carries every mesh of its FBX**, with one active and the rest switched off as
  *inactive GameObjects* (their renderers stay enabled). A fox prefab holds twelve skinned meshes.
  The baker already gathers `includeInactive: false` (`ModuleLibrary.cs:855`), so a baked fox is
  one fox; anything new that walks the renderers with `includeInactive: true` (the figure's
  `forceMatrixRecalculationPerRender` sweep does, harmlessly) must not *measure* from them.
- **A colourway is a different mesh, not a different material.** The geometry is the same per
  species but the UVs move over one 1024² palette, so each colourway is its own module and its
  own instanced bucket. There is nothing to tint.
- **The pack is not to scale with itself** (species on one rig share a back height; the prefabs
  scale their roots ×5 to ×13 and stand two to four times life, and the wolf's mesh node carries a
  further ×0.786). **Our scale is per form, on our row, measured** (§5) — never inherited.

## 2. Import (FA1)

**Where it lands.** The package installs to `Assets/SimpleForestAnimal/`, which is not ignored.
`SyntyImport.ImportAll`'s only guard is that `Assets/Synty` is non-empty afterwards, which it
already is, so the existing route would leave licensed art one `git add .` from a commit. The
import therefore **moves the folder in the same run**:

1. `SyntyImport.ImportAll` imports the package through `ImportPackageImmediately`, exactly as the
   other packs went in (`docs/research/synty-import.md`).
2. A new step, **`SyntyImport.Relocate`**, runs after every package in the same method: for each
   top-level folder the run created outside `Assets/Synty` (a known list — `SimpleForestAnimal` —
   plus a before/after diff of `Assets/`'s top-level folders), `AssetDatabase.MoveAsset` it to
   `Assets/Synty/<name>`. `MoveAsset` keeps the GUIDs, so the prefabs' references into their FBX
   and material survive.
3. **The run fails** (exit 1, the reason logged) if `Assets/SimpleForestAnimal/` — or any other
   folder the diff found — still exists afterwards.
4. **A standing guard beside it**, because the owner may one day import through the Package
   Manager dialog, which no script sees: the catalogue build (`PlayScene.BuildCatalogueAsset`)
   refuses to run while `Assets/SimpleForestAnimal/` exists, naming the folder and the fix, and
   `.gitignore` gains `Assets/SimpleForestAnimal/` so the worst case is an ignored folder rather
   than a commit.

Invocation, as for the other packs:
`Unity -batchmode -nographics -projectPath . -executeMethod Odyssey.EditorTools.SyntyImport.ImportAll -odysseyPackages "C:\Users\timjo\Downloads\SIMPLE_Forest_Animals_Unity_2020_3_v1_02.unitypackage" -logFile Logs/synty-import.log`
(through `scripts/unity.sh exec` if it passes extra arguments on; otherwise the raw line).

**The junction.** Every worktree's `Assets/Synty` is a junction to
`D:\code\odyssey\Assets\Synty`, so **one import writes the pack for every checkout on the
machine**. Run it once, in the main checkout, after checking
`Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"` for an open editor on it; every other
worktree picks the files up on its next refresh (five small FBX, a quick reimport). The runner and
any fresh clone have none of it, which is the case §7 is written for.

The pack's `Scenes/Demo.unity` and `Materials/Ground.mat` come along and are left alone: nothing
references them and they are inside the ignored folder.

## 3. Materials

The pack ships `SimpleForestAnimals.mat` on the built-in **Standard** shader, magenta under URP.
**`SyntyImport.UpgradeBuiltInMaterials` already fixes exactly this** for every material under
`Assets/Synty` — Standard → URP Lit, `_MainTex` → `_BaseMap` — and runs over the packs only, not
the project (the 2026-09-26 lesson about `OdysseyKeepAlive/Standard.mat`). It is run straight after
the import. No project material and no `AnimalImport.Paints` entry is needed: `Paints` exists to
*recolour* a CC0 model whose own colour failed (the frog in the grass), and the SIMPLE palette is
the art's colour. If the first look finds a species lost against the meadow the way the frog was,
a paint is the remedy then and not before.

Two checks the FA1 build owes, both cheap:

- **Instancing.** The far form (§8) submits the colourway meshes instanced, so the upgraded
  material needs `enableInstancing`. Set it in the same step, and let the player build's existing
  keep-alive path (`InstancingKeepAlive`, URP Lit being ours rather than a pack shader) cover the
  variant; the smoke test with `-odyssey-newgame` is the proof.
- **The prefab's own `Animator`** carries the pack's `SFA_Animal_*.controller`. The figure director
  drives an `AnimationMixerPlayable` graph on the animator it finds; the build must confirm the
  graph wins and the controller's blend tree plays nothing (clear `runtimeAnimatorController` on the
  instance if not).

## 4. Forms and colourways — which mesh a given animal wears

**A form is a sex and belongs to the simulation.** The doe and the stag are one species, deer; the
cow and the bull are one species, moose. The rut reads which one an animal is and moves behaviour,
so the form must be deterministic and hashed: it is **derived from the pawn's saved roll seed**, not
a new saved field (interview §7), and **published as a pawn aspect** so presentation never
re-derives it. The field and the aspect are owned by the simulation's design (temperament); this
document only consumes them:

- `SpeciesDef.forms` — how many forms the species has (1 for most, 2 for deer and moose), and which
  form is the *rut* form.
- A published aspect, **`form`**, `0` or `1`, present only on species with two forms.

**A colourway is presentation alone.** Nothing in the simulation may read it. It is dealt by
**one engine-free function** in `Odyssey.Hud` (so the fast tier tests it), `AnimalLooks.Colourway(PawnId,
count)`, a stable integer hash of the pawn id (the project's deterministic mixer, never
`string.GetHashCode`), modulo the form's colourway count. The live figure, the far form (§8) and
any future portrait all call it, so an animal does not change coat on crossing the figure ceiling —
the fault `ColonistLook`'s header warns about, and which the colonists' far form avoided by sharing
one object.

**Catalogue shape.** Today one row per kind, `ModuleIds.Animal(kind)` = `pawn.animal.<name>`, and
one look per kind in `PawnFigureDirector._animalLooks`. The forest species need a row per **form ×
colourway**:

- `ModuleIds.Animal(kind, variant)` where `variant = form × colourways + colourway`. Variant 0 keeps
  the bare id (`pawn.animal.hog`) so the hog, rat and frog rows do not move; later variants are
  `pawn.animal.deer.1`, `.2`, … and are found with `FindFamily`, as colonists are.
- `_animalLooks` becomes a jagged table, a `Look?[]` per kind, and `AnimalLookIndex(kind, variant)`
  a base offset per kind plus the variant. The figure pool, the create and the blend already key on
  one integer look index, so pooling per variant is free.
- The variant for a drawn animal is computed once where the look is chosen (`LookFor`), from the
  published `form` and `AnimalLooks.Colourway`.

Thirty rows at most: rabbit 3, deer 2 + 3, fox 3, raccoon 2, skunk 3, boar 3, moose 2 + 3, wolf 3,
bear 3.

## 5. Scale — life size against a colonist

The owner chose **life size against a colonist**. A colonist is **drawn half again as large as
life** — the cast measures 2.49 m (`FigureBuild.FallbackHeight`, `GroundSpeeds`' comment) — because
a true-to-scale figure is a few pixels from the play camera. "Life size against a colonist" is
therefore **life × the colonists' draw factor**, or a bear stands knee-high to the person beside
it. The factor is **measured once** — the cast's mean drawn height over a real person's 1.75 m,
about **1.42** — and written as one constant beside the rows, not per species.

| Form | Life shoulder | Drawn shoulder (×1.42) |
|---|---|---|
| Rabbit | 0.25 m | 0.36 m |
| Skunk | 0.25 m | 0.36 m |
| Raccoon | 0.30 m | 0.43 m |
| Fox | 0.40 m | 0.57 m |
| Wolf | 0.80 m | 1.14 m |
| Boar | 0.90 m | 1.28 m |
| Doe | 1.00 m | 1.42 m |
| Bear (on all fours) | 1.10 m | 1.56 m |
| Stag | 1.20 m | 1.70 m |
| Moose cow | 1.80 m | 2.56 m |
| Moose bull | 1.90 m | 2.70 m |

**Measured, not reasoned.** Shoulder height is the withers bone's height in the baked pose, read by
the probe the hog and frog were sized with: `AnimalProbe` gains a **`ShootForest`** arm that bakes
each form's prefab at the row's scale, reports sole, withers and crown, and photographs the eleven
forms in a line on the game's grass beside a colonist and a 1 m cube (`Logs/forest-sheet.png`). The
row's `scale` is then *target ÷ measured-at-scale-1*, written as a literal with the probe reading in
the comment beside it, the way the frog's ×0.24 is. The imported FBX stays at `globalScale 1`;
**the scale lives on the row** because two FBX carry four species each and one importer scale
cannot serve a rabbit-sized skunk and a wolf.

The midden hog (1.20 m long, unscaled life) and the rat keep their sizes in this unit; whether they
take the same factor is a question for their first look beside a boar (§12).

## 6. Clips

**Locomotion stays a speed blend.** Idle at 0, Walk and Run at declared metres a second, blended by
`Gaits` as the rat's are. The SIMPLE clips have **no root motion** (the root's X never moves; e-15),
so, as with the rat, the speeds cannot be read off a translation — but unlike the rat's guess they
can be **measured**: `ShootForest` samples a fore-foot bone through the Walk and Run clips at the
row's scale, finds the stance phase (the foot moving backward relative to the root), and reports the
stance speed, which is the ground speed at which the feet do not slide. That number is the row's
`metresPerSecond`. Durations at 24 fps, from the pack's frame ranges:

| Rig | Idle | Walk | Run | Eat |
|---|---|---|---|---|
| Rabbit | 3.33 s | 0.88 s | 1.13 s | 3.33 s |
| Deer (doe, stag, moose) | 5.00 s | 1.21 s | 0.96 s | 3.75 s |
| Fox (fox, raccoon, skunk, wolf) | 4.96 s | 1.46 s | 0.63 s | 5.00 s |
| Boar | 4.96 s | 1.25 s | 1.46 s | 5.00 s |
| Bear | 4.96 s | 1.67 s | 1.00 s | 5.00 s |

Every clip is already `loopTime: 1` in the pack's meta. `AnimalImport` touches only
`Assets/Art/Custom/Animals` and does not reach these files; nothing in FA1 changes that (the pack
needs no reimport setting — Generic rig, loops set, scale on the row).

**Eat is the first per-job clip an animal has.** Today clip choice is purely speed; there is no
driver for anything else. The precedent is the colonists' **seat**: the mixer has one input per gait
*and the seat after them* (design 31 §18d), cross-faded in when the pawn sits. Eat is the same shape:

- The row gains `eatClipName` (resolved like `sitClipName`), one more mixer input.
- The simulation publishes **`grazing`** at 1 on an animal whose current job is grazing, exactly as
  it publishes `sheltering` today (`JobSystem.IsSheltering`, design 43 §6a). The pane's *Grazing*
  status and the figure read the same aspect, so the words and the head-down cannot disagree.
- The figure cross-fades Eat in over a quarter-second while `grazing` is set and the animal is still.

Grazing itself — when an animal grazes, and grazing a crop — is FA3's; FA1 can ship the input with
the rest-time graze (a resting herbivore grazes rather than stands) so the clip is proven before the
crops are.

**Computed motions the behaviour work will need** — listed, not designed here. Each is a pose laid
over the clip in the pose pass, where `WorkSwing` lays an axe stroke and `QuadrupedGait` a trot, and
each is driven by a published aspect or combat event, never by the figure guessing:

| Motion | Who | Needed by | Precedent |
|---|---|---|---|
| Warning stamp (a fore leg struck down, head low) | stag, bull moose, skunk | FA2 warnings | `QuadrupedGait` leg binding |
| Rear up | bear | FA2 warnings | `CombatPose` |
| Tail up and turn away | skunk | FA3 spray | tail joint rotation, as the head turn |
| Lunge (strike) | every animal that fights | FA2 (animals have no combat layer today, `PawnFigureDirector.Combat.cs:363`) | `CombatPose`, the colonists' swing |
| Charge and stop short (the bluff) | bear, moose, stag | FA2 | the drawn position's surge, as `HopSurge` |
| Lying down (sleep, downed) | all | FA2/FA3 | the corpse's roll onto the flank (`CombatPose.cs:450`) |
| Hibernating (out of sight) | bear | FA3 | none — hidden, not posed |

The pack's rigs bind to none of `QuadrupedGait`'s bone names (`FrontLeg`/`BackLeg` `.L`/`.R`), and
they do not need its trot — they have a Walk. Any computed limb motion binds by the SIMPLE rigs' own
names (`*_l_Clavicle*`, `*Head*`, `*Tail*` on each of the five rigs), listed by `ShootForest`.

## 7. Runner safety

The runner and any clone have no `Assets/Synty`, so **every SIMPLE row resolves to nothing there**.
Today an animal whose row did not resolve is drawn nowhere (`CanDraw` false, and the baked pass
skips animals). Two rules:

- **A test that needs a forest animal asks whether *that kind's* art resolved** —
  `PawnFigureDirector.CanDrawKind(kind)`, true when at least one variant of the kind has a look.
  Never `Enabled` (true on the runner because the CC0 rows resolve) and never "is there a
  catalogue" (the catalogue is committed and loads with every Synty reference null). This is the
  lesson in `CLAUDE.md`'s tests section, met five times. **The butcher branch (PR #248) adds a
  public `CanDrawKind` with the same meaning**; whichever merges second adopts the other's and keeps
  one method, and FA1 must not add a second name for it.
- **The hog, rat and frog remain the runner's proof** that an animal draws at all, and after FA2
  that the far form draws animals. Their rows stay in `Assets/Art/Custom` and their tests keep
  running there; nothing in this unit moves them onto the pack.

**What a clone without the pack sees.** A colonist without art gets a stand-in box; an animal gets
nothing, so a clone would have invisible, unclickable wildlife. FA2 gives an unresolved animal kind
the same kind of stand-in in the far pass: one low box per animal sized from `bodyLengthMm`, one
instanced call for all of them. It is never seen on a machine with the pack.

## 8. The far form (FA2)

**Today.** The figure director draws the `MaxFigures` (at most 64) pawns nearest the camera as live
animated figures (`ChooseTheNearest`). Everyone else goes through `ChunkRenderer`'s actor pass
(`ChunkRenderer.cs:2215–2330`): each colonist's face resolves to a **baked module** — the prefab
instantiated once, posed by its row's `poseClip` at `poseClipTime`, its skinned meshes baked
(`ModuleLibrary.cs:838–870`) — and every colonist wearing that face is appended to one placement
bucket and submitted with **one instanced call per face in use** (`SubmitInstances`), plus one per
hair, beard and helmet piece in use. Faces resolve lazily, on first use. **An animal is skipped**
(`ChunkRenderer.cs:2236`), because the pass dealt every pawn a colonist's face and a hog wearing one
was worse than a hog not drawn. That is design 29 §8a's gap, and with 48–80 animals a board it is no
longer an edge case: at a zoomed-out view, herds would blink in and out as the nearest-64 set is
re-chosen.

**The change.** The same machinery, one more bucket family:

- In the loop, an animal no longer `continue`s. It asks for its look variant (§4) and appends its
  placement — `PawnPose.Of` position, `FacingOf` heading, identity scale times the row's scale — to
  the bucket for **`Animal(kind, variant)`**, resolved lazily through `ModuleLibrary.Resolve` the
  first time that variant is seen. The row's `poseClip` is its Idle at a fixed time, so a far
  animal is a frozen idle, as a far colonist is a frozen pose.
- Buckets are submitted after the colonists', through `SubmitInstances` with the module's own
  materials (no recolour: the colourway *is* the mesh) and the colonists' shadow setting.
- An animal whose variant did not resolve (no pack) goes to the stand-in bucket (§7).
- The same walls-down and layer-band tests apply before it (they already run before the skip).
- It closes the gap **for the hog, rat and frog too**, and the hog's baked idle is its whole look
  anyway (its gait is computed over the idle).

**Budget.** At most thirty forest buckets plus three CC0 and one stand-in; a real board uses
perhaps a dozen at once. **Draw calls: at most one per variant in use, doubled if the colonists'
far form casts shadows**; triangles are ~1,000 an animal, 80,000 for Huge's 80, which is nothing
beside the terrain. The CPU cost is one placement append per far animal plus the pose; the target is
**under 0.1 ms for the animal part of the actor pass at 80 animals**, and it is measured, not
assumed (§9).

**Not in FA2:** a second baked pose (a mid-stride frame for moving far animals). A far herd in
flight slides in its idle, as a far colonist walks in her standing pose. If the first look at a
fleeing herd from the far zoom finds that wrong, a moving bucket is one more baked pose per variant.

## 9. Measurements

Design 29 planned a *"`FrameTimeTests` with 64 animals walking"* and it was never built, so no
number exists for what an animal costs the frame. FA1 and FA2 each owe one, taken **inside one run**
with a control, per the project's rule that a frame number compares only with one from the same run:

- **`FrameTimeTests.TheAnimalsAgainstTheFrame`** (PlayMode, `Category("Measurement")`, so it runs
  nightly or on `ci:perf`): the played meadow at 640 × 480, the same world timed with **0, 48 and 80
  animals** walking (Standard's and Huge's ceilings), reporting frame, `FrameSection.Figures`,
  `FrameSection.Actors` and draw calls per arm. In FA1 it measures the live figures (animals inside
  the 64); in FA2 it adds an arm with `MaxFigures = 0` so every animal is far, which is the far form's
  whole cost against a control of the same animals as figures.
- It **ignores itself** unless `CanDrawKind` is true for the kinds it spawns, and uses the CC0 kinds
  to fill the count on the runner, so the far-form arm still measures something there.
- A **4K reading** is a Play-session line in the owner's handover (the overlay's `gpu` and submit
  split), not a test: this machine's tiers cannot run at the owner's resolution.
- **`ShootForest`** (§5, §6) is the size and speed instrument; its text report is kept in
  `Logs/forest-probe.txt` and quoted into this document when FA1 is built.

## 10. The rows

Kinds are appended after the butcher's (kinds 6–9) if PR #248 merges first, and after the frog
(kind 5) if not; the order below is proposed and owned by the plan. Speeds are **to be measured**
(§6); scales are **target ÷ probe** (§5).

| Kind | Name (`AnimalNames`) | Form | Prefabs (colourways) | Target drawn shoulder | Clips |
|---|---|---|---|---|---|
| +0 | `rabbit` | — | `Rabbit_01`, `_02`, `_03` | 0.36 m | `Rabbit_Idle/Walk/Run/Eat` |
| +1 | `deer` | 0 doe | `Doe_01`, `_02` | 1.42 m | `Deer_Idle/Walk/Run/Eat` |
| | | 1 stag | `Stag_01`, `_02`, `_03` | 1.70 m | the same |
| +2 | `fox` | — | `Fox_01`, `_02`, `_03` | 0.57 m | `Fox_Idle/Walk/Run/Eat` |
| +3 | `raccoon` | — | `Raccoon_01`, `_02` | 0.43 m | the fox rig's |
| +4 | `skunk` | — | `Skunk_01`, `_02`, `_03` | 0.36 m | the fox rig's |
| +5 | `boar` | — | `Boar_01`, `_02`, `_03` | 1.28 m | `Boar_Idle/Walk/Run/Eat` |
| +6 | `moose` | 0 cow | `Moose_Female_01`, `_02` | 2.56 m | the deer rig's |
| | | 1 bull | `Moose_Male_01`, `_02`, `_03` | 2.70 m | the deer rig's |
| +7 | `wolf` | — | `Wolf_01`, `_02`, `_03` | 1.14 m | the fox rig's |
| +8 | `bear` | — | `Bear_01`, `_02`, `_03` | 1.56 m | `Bear_Idle/Walk/Run/Eat` |

Every row asks for its prefab with **`prefabUnder = "Assets/Synty/SimpleForestAnimal"`**. No other
pack under `Assets/Synty` has a `Fox_01` or `Bear_01` today (checked 2026-09-26), but `Doe_01` and
`Wolf_01` are exactly the names a later pack will reuse, and design 48 §14's tie rule is the lesson.
Clip names are the pack's own (`Fox_Walk` and so on); `FindSyntyClip` resolves them, and the four
species on the fox rig share one clip set, so the resolution cache pays once.

## 11. What FA1 and FA2 touch

**FA1 (art, with the roster):**

- `Assets/Editor/Odyssey/SyntyImport.cs` — `Relocate` and its failure; the upgrade run's
  instancing flag.
- `Assets/Editor/Odyssey/PlayScene.cs` — the thirty rows, `prefabUnder`, the Eat clip name, the
  refusal while `Assets/SimpleForestAnimal/` exists; then rebuild `ModuleCatalogue.asset` on the
  owner's machine (the committed asset's Synty references are what the runner loads as null).
- `Assets/Editor/Odyssey/AnimalProbe.cs` — `ShootForest`.
- `Assets/Odyssey/Presentation/Rendering/ModuleCatalogue.cs` — `AnimalNames` padded and extended,
  `ModuleIds.Animal(kind, variant)`, `eatClip` on `ModuleEntry`.
- `Assets/Odyssey/Presentation/World/PawnFigureDirector.cs` — the jagged look table,
  `AnimalLookIndex(kind, variant)`, `LookFor` reading `form` and the colourway, `CanDrawKind`, the
  Eat input in the mixer.
- `Assets/Odyssey/Hud/AnimalLooks.cs` (new, engine-free) — `Colourway`, with fast-tier tests.
- `.gitignore` — `Assets/SimpleForestAnimal/`.
- Tests: `AnimalFigureTests` (the unknown kind moves past the new ones — it is 9 on `main` and 99 on
  the butcher branch; take 99), a new `ForestAnimalFigureTests` that assumes `CanDrawKind` per kind
  and checks the drawn shoulder against §5 within 5 %, and `AnimalLooksTests` in the fast tier (the
  colourway is stable, in range, spread, and the raccoon has two).

**FA2 (the far form):**

- `Assets/Odyssey/Presentation/Rendering/ChunkRenderer.cs` — the animal buckets, lazy resolution,
  the stand-in bucket; the skip at 2236 goes.
- `Assets/Odyssey/Presentation/Tests/` — a far-form test on the **hog** (it resolves on the runner)
  that a pawn past the ceiling is submitted; `FrameTimeTests.TheAnimalsAgainstTheFrame`'s far arm.
- Design 29 §8a and `CLAUDE.md`'s known gap *"An animal past the figure cap is not drawn at all"*
  are closed in the same PR.

## 12. Open for the owner

1. **The size factor.** Recommended: forest animals at life × the colonists' draw factor (~1.42),
   so they are to scale with the person beside them (§5). The alternative is literal life metres,
   which puts a bull moose's shoulder at the colonist's chest. And the **hog and rat**, today at
   literal life: bring them to the same factor when the boar stands beside them? Recommended: look
   first — the boar at 1.28 m against the hog at 1.20 m long is the photograph that decides it, and
   the probe sheet will show both.
2. **The smallest three at the play camera.** A 0.36 m rabbit or skunk may be a few pixels from the
   default zoom, which is what the frog's ×0.24 was asked for. Recommended: ship at the factor and
   decide on the first look from `forest-sheet.png` and one Play session; enlarge the three
   together, never one.

## 13. What this does not cover

The species' behaviour, stats, temperament, forms' effect on behaviour (the rut), grazing as a
job, predators, the kill window, colonists heeding warnings — the temperament and signatures
designs. The wildlife table, habitats and the population per board. Names, the wiki rows, the
Almanac and the debug Spawn rows (FA1's roster half). Portraits on the Animals tab (the 22 px slot
is still empty). Hunting, corpses as meat and butchering (K3). The computed motions' own design
(§6 lists them). A moving far pose. Whether a far animal can be clicked: whatever makes a far
colonist pickable is to be checked in FA2 and applied the same way, not assumed here.

## 14. As built (FA1, 2026-09-26)

FA1 is on `claude/forest-animals` (commits `d4e28db9` and `3257593f`). Three departures from §2–§10,
each forced by what the files turned out to be, and the numbers the design could only predict.

### 14a. The import actually used

No Unity: `tools/synty/unpack.py extract <pkg> . --remap Assets/SimpleForestAnimal=Assets/Synty/SimpleForestAnimal`
writes each asset and its `.meta` straight from the package with the GUIDs kept, and without
`--allow-outside` refuses to write anything outside `Assets/Synty/` — the §2 guard, placed where the
art lands rather than after it. Then **one catalogue build**, which applies the import setting and the
material upgrade below. The catalogue build also refuses while `Assets/SimpleForestAnimal/` exists
(`LicensedArtGuard`), and `.gitignore` carries that path. In the worktree the pack is a real folder
beside per-pack junctions to the main checkout's other packs; the owner's checkout still needs the
one `unpack.py` line and a catalogue build.

### 14b. The three departures

1. **Rows draw from each rig's FBX plus a `meshName`, not from the pack's prefabs.** The prefabs were
   saved from an optimised import and carry no bones at all, so nothing could measure a foot or lay a
   computed motion over a leg. A row names its FBX by full path and the skinned mesh to show
   (`SM_Stag_02`), which is also what puts a doe and a stag on one rig.
2. **`AnimalImport.ApplyForest` turns `optimizeGameObjects` off on the five FBX.** Without the bone
   objects there is no withers to measure (§5) and nothing for FA2's stamp, rear and lunge to drive.
   It is idempotent and runs from the catalogue build and the probe.
3. **The FBX carry no material, so each row names `SimpleForestAnimals.mat`** and the figure paints
   it. `SyntyImport.UpgradeForestMaterials` moves it to URP Lit with instancing on, **scoped to the
   pack's own folder** so it never writes through the other packs' junctions.

Also found: **the pack's own Animator re-poses an edit-mode instance at render time**, so the probe
strips it before a shot; the live figures drive their own playable graph and clear its controller,
so the game is not affected.

**The catalogue was spliced, not rebuilt.** A full `PlayScene.RebuildCatalogue` drops the colonists'
appearance block (`docs/lessons.md`), so the 30 forest rows (eleven forms by colourway) went in after
the frog: 2,220 insertions, 0 deletions in `ModuleCatalogue.asset`.

### 14c. Measured

Scale by form from `AnimalProbe.ShootForest`; every drawn shoulder is within 1% of life × the
colonists' 1.42 draw factor, and `EachFormIsDrawnAtItsShoulder` holds the live figures to 5%. Walk and
run are the clip's planted-foot speed at scale 1 (the clips have no root motion).

| Form | Scale | Drawn shoulder | Walk / run at scale 1 (m/s) |
|---|---|---|---|
| Rabbit | 5.72 | 0.355 m | 0.126 / 0.216 |
| Doe | 5.50 | 1.421 m | 0.183 / 0.756 |
| Stag | 6.60 | 1.705 m | 0.183 / 0.756 |
| Fox | 3.50 | 0.567 m | 0.126 / 0.425 |
| Raccoon | 2.75 | 0.426 m | 0.126 / 0.425 |
| Skunk | 2.13 | 0.354 m | 0.126 / 0.425 |
| Boar | 6.04 | 1.277 m | 0.137 / 0.189 |
| Moose cow | 7.40 | 2.559 m | 0.183 / 0.756 |
| Moose bull | 7.80 | 2.697 m | 0.183 / 0.756 |
| Wolf | 5.88 | 1.136 m | 0.126 / 0.425 |
| Bear | 5.06 | 1.563 m | 0.135 / 0.301 |

**The boar's clips are as authored and are thin**: its run is only 1.4× its walk, and its Eat loop
barely moves the head (17 cm drawn, against the deer's 1.63 m → 0.39 m). A computed head dip is the
fix if grazing boar read as standing still; nothing is done about it in FA1.

The probe sheet is `docs/reference/screenshots/2026-09-26-forest-animals-sheet.png` (and `-near.png`),
beside the butcher's sheets: every form textured, the right mesh, to scale beside a colonist and a
1 m cube.

**The frame** (`FrameTimeTests.TheAnimalsAgainstTheFrame`, 640 × 480, RTX 5070 Ti, one run, the
seeded wildlife cleared first so 0 is a true control):

| Animals | Frame | `Figures` | `Actors` | Figures drawn |
|---|---|---|---|---|
| 0 | 1.84 ms | 0.071 ms | 0.078 ms | 5 (colonists) |
| 48 | 2.31 ms | 0.277 ms | 0.081 ms | 53 |
| 80 | 2.37 ms | 0.339 ms | 0.080 ms | 64 |

At 80, **21 animals are past the 64-figure ceiling and not drawn**; that is FA2's far form (§8), and
these numbers are its baseline.
