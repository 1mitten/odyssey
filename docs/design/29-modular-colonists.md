# 29 — Modular colonists: a body, hair and a beard

**Status: ground, interview and research complete, 2026-09-22. Pack imported. No plan yet, no
gameplay code.** This file is the record of what the packs actually contain and what the owner
decided about them, written at the phase boundary so the next session starts from fact rather than
from the pack's marketing copy.

**The measurements are `docs/research/e-06-modular-colonists.md`** and they moved two things in §4
below. Read that file before planning; it is where the numbers live.

Read first: `20-avatars.md` (the portrait and the flat fallback), `06-rendering-and-camera.md` §6c
(what a figure costs), and the `ColonistLook` class comment, which states the rule this unit is most
likely to break.

---

## 1. What POLYGON Battle Royale actually is

Read out of `POLYGON_BattleRoyale_Unity_2022_3_v1_9_2.unitypackage` without importing it, by
unpacking the archive and reading the prefab YAML directly.

**It is not swappable limbs.** `Character_SportyMale_01.prefab` is a single rig carrying **twenty-one
skinned meshes as children** — all fifteen whole-body outfits, six armour overlays, plus `Eyes` and
`Eyebrows` — with exactly one body active and the rest disabled. Every character prefab in the pack
is the same object with a different child enabled.

"Modular" therefore means three things, and only these three:

| Mechanism | How it works |
|---|---|
| **Body outfit** | Enable one of fifteen sibling `SkinnedMeshRenderer`s on the shared rig. |
| **Armour overlay** | Enable one of six more, worn over the body. Male and female sets of three. |
| **Bone-parented props** | Hair, beards, hats, masks, bags. `SM_Chr_Attach_Beard_02.prefab` is a plain `MeshFilter` + `MeshRenderer` with **no skinning at all** — a rigid prop that hangs off `Head`. |

What the pack offers, by slot:

- **15 bodies** — 8 male-shaped, 6 female-shaped, 1 ghillie suit.
- **7 male hair, 2 female hair** (`Female_Hair_01`, `Female_Hair_Pigtails_01`), plus a
  `Male_Default_Hair_01` / `Female_Default_Hair_01` pair that are almost certainly scalp caps.
- **6 beards** (`Beard_02` … `Beard_07`).
- **~35 hats**, 3 helmets, 3 facemasks, a gas mask, eyepatch, glasses, earmuffs, 2 scarves.
- 3 bags, 8 pouches, 8 patches, 3 armour pieces per sex.

`Models/Characters/Characters.fbx` imports as **Humanoid** (`animationType: 3`, 40 bones mapped), so
the existing `AnimationBaseLocomotion` clips retarget onto it with no extra work.

## 2. The finding that reframes the request

**The packs already on disk work the same way, and only half the mechanism is wired up.**

`SM_Gen_Chr_Street_Male_02.prefab` — already installed, already a catalogue row — contains all twelve
PolygonGeneric bodies *plus* `Hair_01`, `Hair_01_alt`, `Hair_02`, `Hair_03`, `Hat_01` and `Hood_01`
as children of one rig. PolygonGeneric also ships loose attachment prefabs: eleven hairs, a bun, a
ponytail, two beards, chops, a moustache, hats, headsets and sunglasses.

A catalogue row today is **one whole body and nothing else**:

```
- moduleId: odyssey.module.pawn.colonist
  prefabName: SM_Gen_Chr_Business_Female_01
  scale: {x: 1.4, y: 1.4, z: 1.4}
  locomotion: [ A_Idle_Standing_Femn, A_Walk_F_Femn, A_Run_F_Femn ]
  appearance: { skin: [1 rect], hair: [2 rects], cloth: [1], cloth2: [1] }
```

So the attachment half has never been built. **The modular system is worth building whether or not
Battle Royale arrives**, and it can be proven against art already on the disk. Battle Royale widens
the pool and brings the beards; it is not the mechanism.

## 3. What the owner decided (interview, 2026-09-22)

1. **The colonist pool is Battle Royale plus PolygonGeneric only** — the two packs that ship
   attachments. Farm, Sci-Fi City and Western Frontier bodies leave the colonist lottery.
2. **Those rows stay in the catalogue, flagged non-colonist.** The art remains resolvable for the
   city's inhabitants, traders and raiders; the lottery reads only the flagged rows.
3. **Three slots vary per colonist: body outfit, hair, beard.** Nothing else.
4. **All headgear is out for now** — hats, helmets, masks, glasses, the novelty set. They become
   **equipment items** later, worn rather than dealt. The head socket is built now and left empty,
   so that unit is content rather than surgery.
5. **Gender picks the body and the hair pool.** `colonist-names.csv` already carries gender. A woman
   gets a female-shaped body and the female hairs; a man gets a male-shaped body, the male hairs and
   the beard roll. The card and the colonist agree.
6. **Three BR bodies are excluded**: `Character_GhillieSuit_01` (no visible head, so hair and beard
   are invisible on it), `Character_ToplessMale_01` and `Character_SportsBraFemale_01` (register, and
   the least cloth area for the colour roll). That leaves **6 male and 6 female** BR bodies.
   *By the same rule, PolygonGeneric's `Underwear_Male_01` and `Underwear_Female_01` should go too —
   recorded as an inference, not as something the owner said.*
7. **Hair and beard follow a colonist into the baked instanced far form.** One person looks like one
   person wherever the camera is and however large the colony. *The cost was feared to be a baked
   mesh variant per (body, hair, beard); the research found it need not be — see §4.*
8. **One hair colour; the beard takes it a shade off; colonists grey with age.** Baldness is a legal
   outcome for men and becomes likelier with age. Age then reads on the body as well as on the card.
   *Owner decision to revisit: the measurement in §4 makes an **identical** beard free and a shade
   off the expensive option. Worth one sentence back before the plan is written.*
9. **Every outfit is still repainted from the colour palette**, BR's authored camouflage and business
   suit included. A contact sheet is judged before this ships — a two-tone camouflage pattern in a
   rolled colour mostly reads as workwear, but "mostly" is not a thing to find out in a playtest.
10. **No build or height variation.** One scale, 1.4, as today. Every measured length — bed fit, the
    carry cradle, hand grips, the 2.49 m sleeper — is taken off the rig and already fragile.
11. **This is the next unit, ahead of M3 stairs, the PF frame fix and the storage panel.**
12. **Importing `PolygonBattleRoyale` into the shared `Assets/Synty` is authorised** — that folder
    only, leaving the installed PolygonGeneric untouched.

## 4. What is already known to be in the way

- **The package ships its own copy of PolygonGeneric, with identical GUIDs.** Verified on
  `SM_Gen_Chr_Street_Male_02` (`0f895ab6fa2a7e9419ef17d25c18bf9c` in both). A full import would
  overwrite the installed pack with BR's revision of it, silently. Import the `PolygonBattleRoyale`
  folder and nothing else — and check for a running editor first
  (`Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"`), because that folder is the one real
  copy every worktree junctions to.
- ~~Bone naming differs between the packs~~ — **checked and harmless.** The two humanoid bone maps
  are identical, 40 bones each, and the project resolves bones only through
  `Animator.GetBoneTransform`. Every bone the figure director and the tool fitting ask for resolves
  on a BR rig. The transform names still differ, so the rule stands for any *new* code: PolygonGeneric uses `Finger_01_L` / `Finger_01_R`;
  Battle Royale uses `Finger_01` and `Finger_01 1` — duplicate names, no side suffix. Anything that
  resolves a bone by name rather than through the Humanoid avatar (tool grips, `HandGrip`,
  `Grasp`) has to be checked against a BR rig before it is trusted. The mapped humanoid bones are
  fine. PolygonGeneric has a `Jaw` bone; Battle Royale does not.
- **Cost is unmeasured and there is an open frame problem in the same system.** Hair plus beard is up
  to two extra renderers per live figure against a ceiling of 64, while `PawnPose.Of` already costs
  13.3 ms of a 22.5 ms frame at 384 colonists (`PF`, `docs/design/06-rendering-and-camera.md` §6c.2).
  Whatever this unit adds must be measured with a control in the same run, per §6c.1 — the 4.6 µs
  constant is not a per-call toll and must not be used to condemn or to excuse a pass here.
- ~~Hair colour stops being an atlas rectangle~~ — **falsified by measurement, 2026-09-22.** Most
  hair and beard meshes map *every vertex* to the single atlas texel the scalp already uses
  (u ≈ 0.050, v ≈ 0.198). Repainting the hair rectangle recolours scalp, hair prop and beard
  together, inseparably. `ColonistMaterials` needs no second mechanism and no widened key, and
  "the beard matches the hair" is the art's own behaviour rather than a feature. **The reverse is
  now the expensive option**: a beard a shade off the hair would need a second rectangle and a
  second colour. Kept struck through because it is a clean example of a plausible mechanism
  argument that the measurement reversed. `e-06` §6.
- **Six hair and beard pieces do not recolour**, because they span real texture rather than one
  swatch — and they include **both** of Battle Royale's named female hairs. Female variety
  therefore rests on curating PolygonGeneric's ungendered set by hand. `e-06` §7.
- **The far form must not bake the combination.** `ChunkRenderer.ColonistModule` is one baked mesh
  per face; baking hair and beard in multiplies that by hair × beard. They are rigid props on a bone
  and the far pose is fixed, so they draw as two more instanced modules keyed on (mesh, colour).
  `e-06` §8.
- **Eyebrows are a separate renderer on the rig** and should take the hair colour, including the
  greying. Nobody has looked at whether they currently do.
- **The flat fallback avatar has no beard.** `ColonistFace` draws eight hair crowns and three builds
  for the no-art path, and picks its crown independently of the 3D hair — the same "invented their
  person" fault `PortraitStudio` was written to fix, surviving in the path nobody looks at. Cheap to
  give it a beard; worth deciding whether the crown should follow the real hair while we are there.
- **Nothing here touches the save format or the state hash.** An appearance is a pure function of
  `(world seed, pawn id)` and is neither saved nor hashed. Widening the pool re-deals every existing
  colony's faces, which the owner has accepted; the format itself need not move and no golden should.
  **If a golden moves, that is the finding** — something reached into the simulation that should not
  have.

## 5. What the research settled, and what it left

**Settled** (`e-06-modular-colonists.md`, all measured): the prefabs import clean and every needed
bone resolves; the heights match to 0.3% so `scale 1.4` is untouched; the swatch classifier reports
`Full` on fourteen of fifteen bodies with no change to its hard-coded columns; the attachments are
rigid and authored in head-bone space, in **both** packs, so one mechanism serves both; and the
colour question reversed itself in our favour.

**Still open, and deliberately left to the unit rather than guessed at:**

- **What any of it costs per frame.** Two extra rigid renderers per live figure against a ceiling of
  64, and two extra instanced buckets in the far form. Measured with a control in the same run, per
  `06-rendering-and-camera.md` §6c.1 — never argued from the 4.6 µs constant.
- **Whether the locomotion clips retarget cleanly on a BR body in motion.** The bones resolve, which
  is necessary and not sufficient. Nobody has watched the walk.
- **The UV extent of `Bun_01`, `Ponytail_01`, `Chops_01` and `Moustache_01`** — the probe's name
  filter missed all four. They must be measured before they enter the pool.
- **How PolygonGeneric's eight ungendered hairs split by gender**, and what the two `Default_Hair`
  pieces actually depict. A contact sheet and the owner's eye, not a probe.


---

## 6. The plan

Owner, 2026-09-22: *"do what you recommend"*, against the two questions §5 sent back. So: **the beard
is the hair colour exactly** — identical is free, a shade off costs a second rectangle and a second
colour, and identical can never desync — and **I pick the gender split for PolygonGeneric's eight
ungendered hairs and bring a contact sheet for correction** rather than asking first.

Two facts found while planning, both of which move work earlier:

- **Gender does not reach the code.** `colonist-names.csv` has a `gender` column with three values,
  `m`, `f` and `n`, and `emit_labels.py` deliberately does not emit it: *"nothing in the game reads
  either yet, and a generated constant nothing reads is the artefact that misleads the next
  session."* That was right when it was written and is now the first unit.
- **A neutral name needs a rule.** `n` is a real value in the CSV. A neutral-named colonist draws
  their body from **both** pools, dealt from their own seed like everything else. Recorded here
  because silence would become an accident.

### The units

| Unit | What it does | Tier |
|---|---|---|
| **MC1** | `emit_labels.py` emits a `Genders` array beside `Names`, and `ColonistNames.GenderOf`. Both `--check` gates cover it. | fast |
| **MC2** | The catalogue learns which rows are colonists and which sex each body is. Battle Royale's twelve keepers join the colonist family; Farm, Sci-Fi and Western rows stay resolvable but leave the lottery. | Unity |
| **MC3** | `ColonistAppearance` gains a hair and a beard index, dealt from their own mixing streams off the same seed, with the pool chosen by gender. Greying is a function of age over the hair colour. | fast |
| **MC4** | The hair and beard pieces become content: key, prefab, slot, sex, and whether the piece recolours. The six that span real texture are excluded here, in data, not in code. | Unity |
| **MC5** | A live figure wears them — one rigid prop per slot, parented to `HumanBodyBones.Head`, re-set on every lease exactly as the material already is. **This is the unit that makes it visible.** | Unity |
| **MC6** | The far form wears them, as two instanced modules at the baked head transform. Not needed for a playtest — a colony under 64 is all live figures — but needed before the colony grows. | Unity |
| **MC7** | The flat no-art fallback gains a beard, and its crown follows the real hair instead of inventing one. | fast |
| **MC8** | The contact sheet, and the frame cost measured with a control in the same run. | Unity |

### The order, and why

MC1 → MC3 are Unity-free and fast-tier tested, so they are the spine and they are cheap to get wrong
and fix. MC2 and MC4 are content. **MC5 is the first unit a player can see**, and it is where the
first playtest row is owed. MC6 can follow MC5 safely because nothing below 64 colonists uses the
baked form — but it must land before the colony can grow past the cap, or a colonist loses their
beard by being in a crowd, which is the exact failure `ColonistLook` was written to prevent.

### What must not be broken on the way

- **The look index space is the catalogue family index, always.** `ColonistAppearanceBook` says so
  at length and `ColonistLookAgreementTests` pins it. Gendered pools are a *filter over family
  indices*, never a re-indexing — the lottery picks from a list of legal family indices and returns
  the family index. Compacting the survivors is precisely the bug the comment records.
- **Nothing enters the save or the state hash.** An appearance stays a pure function of the pawn's
  own roll seed. **If a golden moves, that is the finding**, not something to re-bake.
- **Greying makes the appearance a function of age**, which is new: it was a function of seed and id
  alone. `ColonistAppearanceBook`'s per-pawn cache is keyed on the seed and must gain the age, or a
  colonist keeps the hair they were born with for ever. `PortraitStudio` needs nothing — it is keyed
  on the appearance, so a greyed colonist is simply a new appearance and a new photograph.
- **A clone without the packs must still build and run headless.** Every row degrades on its own;
  a missing attachment drops that slot, never the colonist.

---

## 7. What was built, 2026-09-22

MC1–MC5 are in. MC6–MC8 are not started.

| Unit | State |
|---|---|
| **MC1** gender reaches the code | **Done.** `emit_labels.py` emits `Genders` beside `Names`; `ColonistNames.GenderOf` reads it through the same `PoolIndex` the name derivation uses, so a name and a gender cannot disagree. Gender follows the rolled name, never the displayed one. |
| **MC2** the catalogue | **Done.** `ModuleEntry` gains `colonistPool`, `sex` and `recolours`; twelve Battle Royale bodies joined the family; Farm, Sci-Fi and Western rows stay resolvable and left the lottery. 179 rows. |
| **MC3** the appearance | **Done.** `ColonistCastPools`, `HairPiece`, `BeardPiece`, greying, baldness. The body lottery reuses `ColonistLook.For` over the pool array, so a full pool deals exactly what it dealt before. |
| **MC4** the content tables | **Done.** Fifteen hairs and nine beards, every one measured to recolour. |
| **MC5** the figure wears them | **Built, and one fault open.** See below. |
| **MC6** the far form | **Not started.** Nothing under 64 colonists needs it, but it must land before a colony grows past the cap or a colonist loses their beard by being in a crowd. |
| **MC7** the flat fallback's beard | **Not started.** |
| **MC8** contact sheet and frame cost | **Sheet shot** (`Logs/cast-portraits.png`); **cost not measured.** |

### The open fault

The attachments draw. `ColonistAttachments` is the one owner and both the live figures and
`PortraitStudio` wear them through it, so the roster card and the board cannot disagree. Hair reads
correctly on the contact sheet.

**The beard does not.** It renders as a dark bar across the eyes rather than as a beard on the jaw.

What is already known, so the next session does not re-derive it:

- **The placement is correct and measured** (`BattleRoyaleProbe` §7). On `Character_SportyMale_01`,
  whose crown is at world y 1.788, `Male_Hair_01` lands at 1.597–1.835 and `Beard_02` at
  1.521–1.697 with its centre 0.068 forward. Those are the scalp and the jaw. The head bone's world
  rotation is identity, so an identity local rotation on the piece is right.
- **It is the beard and not the hair.** On the sheet the bar appears on men and never on women, and
  women are never dealt a beard. Bald men with no beard have no bar.
- So the fault is in **what is drawn, not where**: the beard's visible band is its upper edge, with
  the rest of it either inside the head mesh or not reaching the chin on this body.

**The next move is to halve the search, not to reason about it:** shoot the sheet twice, once with
only the hair slot enabled and once with only the beard slot, and compare. A single portrait of one
known body wearing one known beard, at a known scale, settles it. `docs/bug-patterns.md`'s runbook
for "a tile that looks wrong" is the same discipline — the save that settles it is already on disk.

### Also owed

- **The military bodies barely recolour.** `Character_MilitaryMale_01` classifies `Full` but its
  cloth slot is 5% of its vertices, and the female one 4%. The camouflage is painted from many
  swatches and only one is repainted, so the colour roll will hardly show on them. Worth the
  owner's eye on the contact sheet before deciding whether they stay in the pool.
- **The female hair pool has nothing authored for it.** Battle Royale's two named female hairs, the
  bun and the ponytail all span real texture and are excluded, so a woman draws from Battle Royale's
  default scalp plus PolygonGeneric's eight ungendered pieces — all currently marked `Either`
  because nobody has looked at them. That split is a contact sheet and the owner's eye (MC8).
- **`Character_Space_Male_01`** is in the pool and may be helmeted, in which case hair is invisible
  on it and it should go the way the ghillie suit did. Unchecked.


---

## 8. The setup screen and the colony disagreed

**Owner, 2026-09-22:** *"what you see on the character generation/selection is not what you see when
you start the game — there is a disconnect."*

They were right, and it was this unit's doing.

**What happened.** `OdysseyBootstrap` assigns the real appearance book in `BuildSession` — but the
setup screen runs *before* a session exists, so `PortraitStudio.Appearances` was null and its
fallback answered instead:

```csharp
Appearances ??= new ColonistAppearanceBook(0u, Rows.Count);   // every row, ungendered, no hair
```

That was correct for as long as a book was only a seed and a face count. `PawnFigureDirector` even
said so out loud: *"two books with the same seed and the same face count give the same answers."*
**MC3 made that false.** A book now carries the gendered pools of bodies, hair and beards, so the
fallback dealt from all 73 rows with no gender, no hair and no beard, while the session dealt from
29 gendered rows with both. Pressing Start replaced the book, `Portraits.Clear()` wiped the cache,
and the person you chose was replaced by somebody else.

**The fix is that a book is built from the catalogue or not at all.** Both fallbacks now call
`AppearanceBooks.For(0u, catalogue)`, which is the same factory the session uses, so the two cannot
carry different pools. The seed still differs — 0 against the world's — and that is harmless and
deliberate: a colonist is dealt from *their own* roll seed, and the book's seed is only the fallback
for a save written before pawns carried one (`20-avatars.md` §5).

**Two tests in `ColonistLookAgreementTests` pin it**, in the Unity tier because they name a
catalogue: `TwoBooksBuiltFromTheSameCatalogueDealTheSamePerson` walks forty pawns through a
pre-session book and a session book and demands the identical appearance, and
`ABookBuiltFromTheCatalogueOnlyDealsBodiesInThePool` asserts the other half — a book built from a
row count deals bodies the colony would never give you.

**The lesson, which generalises past this unit.** The failure was not in either book; it was that
*a type grew a field and a second construction site was not told*. `AppearanceBooks.For` existed
precisely to be the one owner and a `??=` quietly forked it. Any `??=` that constructs a
configured object is a second owner waiting to drift — `docs/bug-patterns.md`.


---

## 9. The uniform, and clothing as equipment

**Owner, 2026-09-22:** *"could we make the colonists' clothes the same at the beginning but use the
assets as items for clothing — start with a basic clean space uniform"*, and *"stick with a white
uniform with a slight blue tint, go with the jumpsuit"*.

### 9a. Why this is not a garment system, and what it is instead

**Clothing is not separable from the body in any Synty pack.** A body is one skinned mesh: the person
and their outfit together, with no seam between them. That was `e-05`'s conclusion and Battle Royale
confirmed it — fifteen complete outfits on one rig with exactly one enabled
(`e-06-modular-colonists.md` §1).

So a clothing *item* cannot be a garment layered onto a colonist. It has to be **a swap of which body
mesh is active** — which is what the `Look` index already does. The machinery is built. What changes
is who decides it: today a lottery, tomorrow an inventory.

That reframing is the whole design. It also means the work splits cleanly in two, and the halves have
very different costs.

### 9b. What was built now: the issued uniform

Every colonist wears **PolygonGeneric's jumpsuit** — `SM_Gen_Chr_Jumpsuit_Male_01` and
`_Female_01`, the one matched male/female pair in either pack that reads as issued kit rather than as
somebody's own clothes. It is flagged in the catalogue (`ModuleEntry.uniform`), read into
`ColonistCastPools`, and applied in `ColonistAppearance.Of`.

**Colour: `#E8EDF6` cloth, `#A8B2C2` trim.** White with a slight blue tint, not pure white — pure
white has nowhere to go under the golden-hour grading and reads as a hole in the frame rather than as
cloth. The trim is fixed rather than rolled, because a uniform whose collar varied per colonist is
not a uniform.

**While a uniform is issued, the body lottery does not run.** Every colonist is the same two bodies
and tells themselves apart by face, hair and beard — which is exactly what MC1–MC5 built, and is the
argument for doing this at all. The twenty-nine-body pool is *kept and still correct*; it is what the
clothing system will draw from, which is why it is flagged rather than emptied.

**The uniform is applied after the rolls, never instead of them.** Every stream is consumed in the
same order either way, so taking the uniform off later gives back exactly the cast that would have
been dealt rather than a re-shuffled one.
`TakingTheUniformOffGivesBackTheCastThatWouldHaveBeenDealt` is the assertion, and it is what makes
9c safe to build on top.

### 9c. What is planned: clothing as equipment

**The cost that matters is not the meshes — it is that the outfit stops being drawing.**

Today a colonist's appearance is a pure function of their roll seed: never saved, never hashed,
outside the simulation entirely. That is load-bearing, and it is why none of MC1–MC5 moved a golden.
**The moment clothing is a thing a colonist wears, what they are wearing is simulation state** —
saved, hashed, and part of the tick. A save-format bump and a golden re-bake, not a drawing change.

What the packs actually offer, counted:

| Kind | What exists |
|---|---|
| **Both-sex garments** | **Twelve matched pairs** — jumpsuit, business, peasant, prisoner, four street variants, mercenary, military, and two sporty |
| **Single-sex garments** | Five — the space suit, redneck and Battle Royale's business male; the 70s and goth women |
| **Layered armour** | Six overlays, three per sex — the only genuinely *additive* clothing in either pack, worn over a body rather than replacing it |
| **Headgear** | ~35 hats, three helmets, masks, glasses — already excluded from the lottery and waiting on this seam (§3 decision 4) |

The units, in the order they should run:

| Unit | What it does |
|---|---|
| **CL1** | An `ApparelDef`: a garment's key, its male and female body rows, and what it is worth. The twelve pairs and the five single-sex ones become content, not code. |
| **CL2** | A worn slot on the pawn, in the save and in the state hash. This is the golden re-bake, and it should land on its own so the re-bake is *measured* to be the hash seeing more rather than the colony doing anything different. |
| **CL3** | The drawers read the worn garment instead of the uniform. `ColonistAppearance` gains an override the same way `ColonistAppearanceBook.Override` was left for the appearance panel — the seam is already there. |
| **CL4** | Armour as a second, layered slot, using Battle Royale's six overlays. Additive, so it does not fight CL3. |
| **CL5** | Headgear, on the head socket `ColonistAttachments` already builds and leaves empty. Content, not surgery — which was the whole point of building the socket in MC5. |
| **CL6** | Where clothing comes from: the starting kit, a trader, a crafting bill. Out of scope until CL1–CL3 exist. |

**Two things to decide before CL1 is written**, neither of which is mine to guess:

- **Does a garment have a quality and a condition?** Beds already have quality tiers
  (`20-beds.md`), so the vocabulary exists. A worn-out jumpsuit is a mood thought and a reason to
  make a new one; it is also a whole durability system nobody has asked for.
- **Is the uniform an item, or the absence of one?** Cheapest is the absence: a colonist wearing
  nothing draws the uniform, which is what today's code already does and costs no content. The
  alternative — the uniform as a real garment the colony starts with a stack of — is more honest and
  more work.


---

## 10. The hair colour paints the face, and it always has

**Not introduced by this work, and not fixed by it.** Recorded here because the uniform made it
impossible to ignore.

**The symptom.** Most colonists have a bar across their eyes in their hair colour. With the brown
hair the old cast mostly drew it read as shadow or sunglasses and nobody reported it. With a plain
white uniform and a palette that includes teal (`#2E6B7A`) and plum (`#7A3B5E`), a colony looks like
it is wearing coloured goggles.

**The measurement.** Shot with the hair colour forced to pure green, the hair rectangle covers the
scalp, the eyebrows, a band across the eyes, the jaw and the lips — most of the face.
`ModularProbe` shoots that sheet; set `Hair` to `0x00FF00` and re-run.

**It is pre-existing.** Before changing anything, the swatch rectangles in the rebuilt catalogue were
compared against those committed before this branch, for `SM_Gen_Chr_Business_Female_01` and
`SM_Gen_Chr_Street_Male_01`: **identical, byte for byte**. `CharacterSwatches` allows up to two hair
clusters and the second one is picking up a face cell. This is how colonists have always been
painted.

**Three ways out, cheapest first.** None chosen; it is the owner's call.

| Option | What it costs | What it risks |
|---|---|---|
| **Narrow the palette** to natural hair colours — drop teal and plum | one line of content | does not fix it, only stops it shouting. The bar is still there in brown |
| **Take the second hair cluster** only when it is head-dominant *and* adjacent to the first | a rule in `CharacterSwatches`, re-classify, judge a contact sheet | re-classification touches all 73 bodies; some genuinely have two hair cells and would lose one |
| **Exclude the eye and lip clusters explicitly**, the way eyes are already excluded from *being* the hair slot | a second rule beside `MaxEyeVertices`, which exists for exactly this family of mistake | the clusters have to be identified reliably across four packs, which is the work |

The middle option is the one I would back, and the cheapest experiment that decides it is the green
sheet again after the change: if the green retreats to scalp and brows on every body, it is right.


---

## 11. A neutral name is dealt a sex, once

**Owner, 2026-09-22:** *"is there a way to assign more female/male sounding names to the appropriate
character?"*

**The data was already right.** `colonist-names.csv` carries a `gender` column and it decides the
body, so a name and a body can only disagree if the column is wrong. It is not: 120 `m`, 90 `f`, and
30 `n` — and the thirty are genuinely unisex (Avery, Riley, Rowan, Wren, Greer, Blythe, Emory,
Marlo, Sable, Fen) or nicknames with no sex at all (Weasel, Spudgun, Treacle, Flower, Holiday).

**What was wrong was what this unit did with `n`.** It was handled slot by slot as "draw from both
pools", which produced an *incoherent* colonist — a female body that could still be dealt a beard,
because `CanGrowABeard` only excluded `f`. And once §9's uniform landed it was worse:
`UniformFor('n')` returned the male cut, so **all thirty unisex names were men, in every colony, for
ever.** The code said in as many words that this was a placeholder.

**The fix is one coin, flipped once.** `ColonistAppearance.SexOf` resolves `n` to `m` or `f` from the
same seed everything else about that colonist comes from, at the top of the derivation, and every
slot below reads the resolved sex rather than the name. Rowan is a man in one colony and a woman in
another, is the same person on the setup card, on the board and after a reload, and is never both at
once. At the population level it is still "both pools" — which is all it was ever for.

Four tests in `ColonistCastPoolTests` hold it: a neutral name's body and beard agree with each other,
both sexes turn up across a colony, a name that carries a sex keeps it, and the flip is pure in the
pair.

**The evidence is a contact sheet.** `ModularProbe` labels `colony.png` with the name, the gender its
CSV row carries and the sex actually dealt — `Holiday [name n -> m]`, `Pleb [name n -> f]` — so
"does the name match the character" is a thing to look at rather than reason about.

### Still the owner's to decide

- **The pool skews male**: 120 to 90, and the neutrals split evenly, so a colony runs about 57% men.
  One column of one CSV; the wiki lists it so it can be corrected without reading any code.
- **Nicknames are dealt a sex too.** Spudgun and Treacle become a man or a woman like anyone else.
  That reads as a person with a nickname, which seems right — but if a nickname should sit on top of
  a real name rather than replace it, that is a naming decision and not this unit's.


---

## 12. Why every portrait went magenta

**Owner, 2026-09-22, with a screenshot:** *"the portraits on the character selection are displaying
[incorrectly] — completely pink."*

Flat magenta with the right silhouette: meshes and attachments resolved, the material did not. Full
account and the check it earns are in `docs/bug-patterns.md` (P14). In short:

`PortraitStudio` caches the subject GameObject and reuses it while the look is unchanged. Ending a
colony disposes `ColonistMaterials` — destroying every material it cloned — and sets the studio's
`Materials` to null. **The subject was not part of that teardown**, so it survived wearing destroyed
materials, and `Paint` then returned early because `Materials` was null and never reassigned them.

**§9's uniform is why it became visible.** With seventy-three bodies the subject was nearly always
rebuilt by the next colonist and picked up fresh materials; with two, it is reused almost every time.

`Materials` is now a property whose setter drops the subject and clears the pictures, and
`OdysseyBootstrap.Portraits` hands the studio live materials back when it is asked for one after a
colony has ended instead of leaving it unpainted for the rest of the session.


---

## 13. MC6 — the far form wears them

A colonist past the sixty-four-figure cap is drawn from a baked mesh instanced by placement matrix.
It now wears the same hair and beard the live figure does.

### What was built

**Two more instanced buckets, never a bake.** Baking hair and beard into the body mesh would
multiply the mesh variants by hair × beard and turn a bounded set of a few dozen into one bounded
only by colony size. They are rigid props on a bone and the baked pose is fixed, so **the head is a
constant per body** and a piece is one mesh instanced across everyone wearing it. The bucket key is
the piece, not the person (`e-06` §8).

**The submit loop iterates pieces, not colonists**, so the pass costs **at most 24 draw calls** —
fifteen hairs and nine beards — whatever the colony is. Each increments `ChunkRenderer.DrawCalls`,
which is the thing P10 was written about: a pass that does not count itself is a pass nobody can see.

**The head is captured at bake time, because there is nothing to ask afterwards.** A baked module is
a mesh and a matrix; the rig it came from is instantiated, posed, measured and destroyed inside
`ModuleLibrary.CollectSkinned`. The head bone is read there, *after* the pose is sampled, and pushed
through the same `place` normalisation every part gets — so it inherits the pivot convention and the
1.4 scale rather than having them applied a second time by a caller who might get one of them wrong.
`ResolvedModule.Head` and `HasHead` carry it.

**The bake bares the head too.** `CollectSkinned` takes every *active* skinned renderer, and
PolygonGeneric ships hair, hats and hoods as active children — so without
`ColonistAttachments.BareTheHead` in the bake, a colonist past the cap wore the pack's own hair while
the same colonist in front of the camera wore ours. Two drawers, two answers, which is the fault
`ColonistLook`'s header exists to warn about.

**The far body is not recoloured per colonist and the hair matches that.** `SubmitInstances` draws
the baked body through `MaterialCache` with the pack's colours and no tint; a head that was tinted
would be the one part of a distant figure that varied.

### What is measured, and what is not

`FarColonistHeadTests` runs against the **real art** and asserts what could silently be wrong: every
body in the pool bakes with a head bone, that head sits in the top 40% of the body's own bounds and
within 0.35 m of its centre in plan, and a PolygonGeneric body bakes with no `_Attach_` mesh in it.
Both tests `Assert.Ignore` when the art did not resolve — the right question is whether the art
resolved, never whether there is a catalogue, because the catalogue is committed and its references
point into the gitignored folder.

### Measured, 2026-09-23

`FrameTimeTests.TheFrameAgainstColonySize` already sweeps a colony from 8 to 384 on the real board,
which spans the 64-figure cap — so **the control for "what the hair and beard pass costs" is the same
run at 64 figures**, where nobody is in the far form at all. It now logs draw calls beside the frame.

| Colony | Figures | Frame | Draw calls |
|---|---|---|---|
| 8 | 8 | 2.41 ms | 1,125 |
| 32 | 32 | 3.03 ms | 1,125 |
| **64** | **64** | **4.04 ms** | **1,125** ← nobody in the far form |
| 96 | 64 | 5.20 ms | 1,147 |
| 128 | 64 | 6.47 ms | 1,149 |
| 192 | 64 | 10.36 ms | 1,151 |
| 256 | 64 | 15.67 ms | 1,151 |
| 384 | 64 | 30.35 ms | 1,152 |

**The draw-call claim is now measured rather than structural.** The whole far-form colonist pass —
bodies, hair and beards together — adds **27 draw calls at most**, and going from 96 to 384 colonists,
a fourfold colony, adds **five**. Draws scale with the number of distinct *pieces* in use, not with
the number of people, which is what MC6 was designed to guarantee and what P10 exists to catch.

**The frame does not, and that is not this pass.** The actor pass goes 0.027 ms at 64 figures to
17.21 ms at 384 — super-linear, and the shape matches the open `PF` finding exactly: `PawnPose.Of`
scans every other pawn for the crowd sidestep, once per posed pawn, every frame
(`06-rendering-and-camera.md` §6c.2). Per far colonist this unit adds one matrix multiply, two array
indices and two array writes — O(1), against roughly **15.6 µs per far pawn** the actor pass already
costs at that scale. It is noise on top of the thing that actually needs fixing.

**What this means in practice.** At the colony sizes this game is played at, the far form barely
exists: the figure cap is 64 and the audit's scale target is fifty, so a normal colony never enters
this path at all. Where it does, it costs draws in pieces and its per-person work is constant.

### The control, 2026-09-23

The draw-call table above says the far form is bounded, but it is not the case that matters: a colony
of fifty or sixty is **all live figures** and never enters that path. So the pass was measured the way
§6c.1 insists — **against the same colony with it switched off, in the same run**.
`ColonistAttachments.Enabled` is the control, exactly as `ChunkRenderer.SubmitToGpu` is for
submission, and `FrameTimeTests.TheAttachmentsCostWhatTheyDraw` alternates on/off/on/off at two
colony sizes so a drift falling between the halves cannot be mistaken for the pass.

| Colony | Figures | On | Off | **Cost** |
|---|---|---|---|---|
| 64 (all live figures) | 64 | 3.743 ms (3.738 / 3.748) | 3.694 ms (3.697 / 3.692) | **+0.049 ms** |
| 192 (128 in the far form) | 64 | 9.179 ms (9.258 / 9.099) | 9.127 ms (9.114 / 9.140) | **+0.051 ms** |

**The whole feature costs about 0.05 ms, and it does not grow.** Tripling the colony — adding 128
colonists in the far form on top of 64 wearing live renderers — moves it by 0.002 ms, which is inside
the repeat spread. Against the 5 ms budget that is **one per cent**, and against the 3.7 ms the same
board costs without it, 1.3%.

The repeats are the reason to believe it: 3.738/3.748 against 3.697/3.692 is a gap of 0.049 ms with a
spread of 0.010, so the difference is five times the noise rather than lost in it.

**What it is not.** None of the frame's growth with colony size belongs here: at 192 the pass costs
the same 0.05 ms it does at 64, while the frame itself goes 3.7 → 9.1 ms. That growth is the open
`PF` finding, `PawnPose.Of`'s per-pawn crowd scan, and this sweep is further evidence for it.

## 13a. The far form wears the uniform (2026-09-24)

**The report** (owner, 2026-09-23): past a certain number of colonists, some "spawned in an orange
suit", textures "kept switching", and the session "got buggy".

**The number is the figure cap, and the orange is the pack's own jumpsuit.** Past 64, the colonists
furthest from the camera are drawn in the baked far form (§13). That form draws each body through
`MaterialCache` in the pack's own paint, one material per body, and §13 says so on purpose: a far
body is never recoloured per colonist. But the issued uniform (§9b) *is* a recolour. The live figure
paints the jumpsuit `#E8EDF6`, and nothing painted the far one. PolygonGeneric paints
`SM_Gen_Chr_Jumpsuit_*_01` **burnt orange**: `#B06F24` cloth and `#BD8436` trim, sampled off
`Generic_01_A.png` at the catalogue row's own cloth rectangles. The unflipped sample lands on a
neutral grey, which rules out a UV-orientation mistake. So everyone beyond the nearest 64 was in
orange, and the nearest-64 set moves with the camera, so colonists changed clothes as it panned.
That is the "switching".

**The first investigation looked at the wrong orange** (`docs/journal.md`, 2026-09-23). The actor
stand-in cube is also orange, and a sweep correctly found zero stand-ins at 384 colonists. But the
question was not whether a stand-in was drawn. It was what colour the far body is. That sweep ran on
a barren board and counted stand-ins, so it could not have seen this.

**The fix is the uniform, not a far recolour.** `ColonistAppearance.IssuedCloth(pools, look)` says
whether *everybody* in a body wears the same cloth. That is true only for the uniform's two bodies
while a uniform is issued, and false for every rolled colour. It sits beside `Of`, which applies
the same two constants on the same condition. `ChunkRenderer.FarMaterials` asks it once per body and,
for those two, draws the far body through the shared `ColonistMaterials` with **the cloth rectangles
only**. Skin and hair are empty rectangles, so they keep the pack's paint exactly as before. It is
still one material per body, so §13's rule holds and the draw calls do not move: the bucket is still
the body. `OdysseyBootstrap` hands the renderer the same `ColonistMaterials` the figures use. One
thing does change on the GPU. A painted far body draws through `Odyssey/Character`, so it now gets
that shader's ink hull, as the live figure always has. That is a second pass per uniform body, not
a new draw call, and it is the same outline the person had before they crossed the cap.

**What still changes across the cap, and is left:** skin tone and hair colour. The far form keeps the
pack's skin, and its hair is drawn unrecoloured (§13). Both are rolled per colonist, so matching them
means buckets per (body, skin, hair) rather than per body. That multiplies the far form's draw calls
by the palette, and it is a decision, not a fix. The suit was the thing a player notices from across
the board. Skin and hair are a few pixels at the distance the far form is drawn.

**Do not undo by tidying.** Do not widen `IssuedCloth` to "whatever the colonist wears": the far
form would then need a material per colonist, which is exactly what §13 rules out. And do not paint
the skin and hair slots with a default. An empty rectangle is what keeps the pack's paint.

**The checks.** Fast tier: `TheBodyAColonistIsIssuedSaysTheColourTheyWear` (every uniformed colonist
wears what the body says) and `NoBodyButTheUniformClaimsAColour` (the negative control). Unity tier:
`FarColonistUniformTests` resolves the real uniform rows, asserts a near-white cloth colour with skin
and hair switched off, and asserts every other body still draws in its own paint. It ignores itself
where the packs are absent.
