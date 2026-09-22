# 29 — Modular colonists: a body, hair and a beard

**Status: ground and interview complete, 2026-09-22. No code, no import, no plan yet.** This file is
the record of what the packs actually contain and what the owner decided about them, written at the
phase boundary so the next session starts from fact rather than from the pack's marketing copy.

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
   person wherever the camera is and however large the colony. The cost of that — a baked mesh
   variant per (body, hair, beard) actually in use — is measured before it is spent, not after.
8. **One hair colour; the beard takes it a shade off; colonists grey with age.** Baldness is a legal
   outcome for men and becomes likelier with age. Age then reads on the body as well as on the card.
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
- **Bone naming differs between the packs.** PolygonGeneric uses `Finger_01_L` / `Finger_01_R`;
  Battle Royale uses `Finger_01` and `Finger_01 1` — duplicate names, no side suffix. Anything that
  resolves a bone by name rather than through the Humanoid avatar (tool grips, `HandGrip`,
  `Grasp`) has to be checked against a BR rig before it is trusted. The mapped humanoid bones are
  fine. PolygonGeneric has a `Jaw` bone; Battle Royale does not.
- **Cost is unmeasured and there is an open frame problem in the same system.** Hair plus beard is up
  to two extra renderers per live figure against a ceiling of 64, while `PawnPose.Of` already costs
  13.3 ms of a 22.5 ms frame at 384 colonists (`PF`, `docs/design/06-rendering-and-camera.md` §6c.2).
  Whatever this unit adds must be measured with a control in the same run, per §6c.1 — the 4.6 µs
  constant is not a per-call toll and must not be used to condemn or to excuse a pass here.
- **Hair colour stops being an atlas rectangle.** Today `CharacterSwatches` classifies UV clusters
  and `ColonistMaterials` repaints rectangles of the pack atlas. A hair or beard *attachment* is its
  own renderer with its own material, so its colour is a material property rather than a rectangle —
  cleaner, but a second mechanism that must agree with the first, or a colonist gets one hair colour
  on the scalp and another in the beard. `ColonistMaterials`' key widens; it does not fork.
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

## 5. Still open, for the research phase

- What a baked (body, hair, beard) variant costs, and how many distinct ones a colony of fifty
  actually produces.
- Whether BR's rig retargets the existing locomotion clips cleanly, and whether the tool grips land
  in the right hand on a BR body.
- Whether BR bodies want a scale other than 1.4 to stand the same height as the current cast.
- What `Male_Default_Hair_01` and `Female_Default_Hair_01` actually are, and whether "bald" is one of
  them or the absence of any hair prop.
- Whether the swatch classifier finds usable skin, hair and cloth clusters in BR's four atlases.
