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
