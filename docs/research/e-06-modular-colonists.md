# e-06 — Modular colonists: hair and beards from Battle Royale and Generic

**Phase 4 research, 2026-09-22.** Follows `e-05-character-customisation.md`, which concluded that
nothing below the neck is separable on a Synty body and that the atlas swatch is the only lever.
This asks what changes now that a pack with bone-parented hair and beards is on the disk.

Everything below was **measured**, not read: the pack was unpacked and its prefab YAML read directly
before import, and `Assets/Editor/Odyssey/BattleRoyaleProbe.cs` measured the rest inside the editor.
The raw output is `Logs/battle-royale-probe.txt` (gitignored; regenerate with
`scripts/unity.sh exec Odyssey.EditorTools.BattleRoyaleProbe.Run`).

---

## Question

Can POLYGON Battle Royale's characters replace the colonist cast with per-colonist random hair and
beards, and what does that cost?

## Findings

### 1. "Modular" means three things, and none of them is a separable limb

`Character_SportyMale_01.prefab` is one rig carrying twenty-one skinned meshes as children — all
fifteen whole-body outfits, six armour overlays, `Eyes` and `Eyebrows` — with exactly one body
active. Every character prefab in the pack is the same object with a different child enabled.

Hair and beards are **rigid props**, not skinned: `MeshFilter` + `MeshRenderer`, no bones. They hang
off the `Head` bone.

### 2. The pack imports clean, and the rig is the rig we already use

All fifteen prefabs load with resolved meshes despite being authored in the pre-2018.3 prefab format,
so the editor's upgrade works. The avatar is `CharactersAvatar`, Humanoid, and **every bone the
figure director and the tool fitting ask for resolves** — hips, spine, chest, neck, head, both hands,
all three fitted fingers on the right hand, both upper legs, both toes.

The two packs' humanoid bone maps are **identical, 40 bones each**, with no bone in one missing from
the other. The transform-name difference found before import (`Finger_01_L` in Generic against
`Finger_01` and `Finger_01 1` in Battle Royale) is therefore harmless: the project resolves bones
only through `Animator.GetBoneTransform(HumanBodyBones.*)` and never by name.

### 3. The heights match to within 0.3%, so `scale 1.4` carries over untouched

| Body | Height (m, unscaled) |
|---|---|
| Battle Royale bodies, all twelve keepers | 1.791 – 1.795 |
| `Character_GhillieSuit_01` (excluded) | 1.859 |
| Tallest body in the cast today | 1.797 |

Vertex counts are comparable too — BR bodies are 2,763 to 4,453 against the current cast's ~3,400 —
**except the ghillie suit at 15,347**, four times any other body and a third reason it is out.

### 4. The attachments are authored in head-bone space

Hair and beard mesh bounds centre between −0.084 and +0.198 in their own space, and the head bone
sits at root-space y 1.569. They are therefore authored **relative to the head**, so an attachment
parents to `HumanBodyBones.Head` with an identity transform. There is no offset to measure, no
per-body fitting, and no per-pack special case.

**PolygonGeneric ships them the same way** — twenty-two loose attachment prefabs, every one rigid
(`skinned = no`), every one centred between 0.004 and 0.210, every one on a single material
`Generic_01_A`. **One mechanism serves both packs.**

### 5. The recolour mechanism already works on the new pack, unchanged

`CharacterSwatches.Classify` was written against four other packs with hard-coded skin columns. Run
against Battle Royale it reports **`Full` on fourteen of fifteen bodies** — every slot found, skin
included — with only the ghillie suit coming back `NoSkin`, correctly, because none is showing. That
is a better rate than the current cast's 55 of 61.

### 6. The decisive finding: hair, beard and scalp share one atlas texel

Most hair and beard meshes map **every vertex to a single point** of the atlas, at u ≈ 0.050,
v ≈ 0.198 — which is the same cell the bodies' own hair swatch occupies.

| Pack | Meshes measured | One flat swatch cell | Spanning several regions |
|---|---|---|---|
| Battle Royale | 17 | 12 (extent ≤ 0.0015) | 5 |
| PolygonGeneric | 11 | 10 (extent ≤ 0.0012) | 1 |

The consequence is large and free: **repainting the hair rectangle recolours the scalp, the hair prop
and the beard together, inseparably.** The owner's decision that "the beard takes the hair colour"
is not a feature to build — it is what the art does. It also means the reverse is now the expensive
option: a beard *a shade off* the hair would need a second rectangle and a second colour, and should
be dropped unless it is wanted for its own sake.

This **falsifies** what `29-modular-colonists.md` §4 recorded before the measurement — that hair
colour would stop being an atlas rectangle and become a per-attachment material property. It does
not. `ColonistMaterials` needs no second mechanism and no widened key.

### 7. But five hair pieces do not recolour, and they are the wrong five

The exceptions span real texture rather than one swatch, so repainting them would throw art away:

| Mesh | UV extent | Consequence |
|---|---|---|
| `SM_Chr_Attach_Female_Hair_01` | 0.701 | excluded |
| `SM_Chr_Attach_Male_Hair_05` | 0.301 | excluded |
| `SM_Chr_Attach_Female_Hair_Pigtails_01` | 0.229 | excluded |
| `SM_Chr_Attach_Male_Hair_04` | 0.216 | excluded |
| `SM_Chr_Attach_Beard_04` | 0.127 | excluded |
| `SM_Gen_Chr_Attach_Hair_05` | 0.099 | excluded |

**Both of Battle Royale's named female hairs are in that list.** The pack's only recolouring female
hair is `Female_Default_Hair_01`. Female hair variety therefore depends entirely on curating
PolygonGeneric's set, which is not gender-labelled and will need a hand-authored split.

What survives, measured:

| Slot | Recolouring options |
|---|---|
| Male hair | 6 from BR (`Default`, `01`, `02`, `03`, `06`, `07`) + a share of Generic's 8 |
| Female hair | 1 from BR (`Default`) + a share of Generic's 8 |
| Generic hair, ungendered | `Hair_04`, `06`, `07`, `08`, `09`, `09_alt`, `10`, `11` |
| Beards | 5 from BR (`02`, `03`, `05`, `06`, `07`) + 2 from Generic |

Two Generic pieces are heavy enough to question on cost alone: `Hair_07` at 3,542 vertices is as
large as a whole body, and `Headset_01_alt` at 2,388.

### 8. The far form must not bake the combination

A colonist past the 64-figure ceiling is drawn from `ChunkRenderer.ColonistModule(variant)` — **one
baked mesh per face**, resolved lazily and instanced by placement matrix, with colour carried by the
material. Baking hair and beard *into* that mesh would multiply the mesh count by hair × beard and
turn a bounded set of ~61 into one bounded only by colony size.

It does not have to. Because the attachments are rigid props on a bone and the far form's pose is
fixed, hair and beard can be **two more instanced modules** at a constant head transform per body.
The bucket key is (mesh, colour), which is at most ~14 meshes × 9 hair colours and shared across
every colonist wearing them — not per person. The owner's decision that hair and beard follow a
colonist into the far form is satisfied without the combinatorial bake.

## Recommendation

**Build it, on both packs, with one attachment mechanism.** Every technical risk named before the
measurement came back clear: the prefabs import, the rig is identical, the heights match, the swatch
classifier works unmodified, and the colour mechanism not only survives but hands us matching beards
for nothing.

Three things the plan must carry, in order of how much they would cost if missed:

1. **Curate the hair list in content, not in code** — six BR hairs and eight Generic ones are legal;
   six pieces must be excluded because they do not recolour, and the Generic set needs a hand-authored
   gender split. This is a table, and it belongs beside the other content tables.
2. **Draw the far-form attachments as instanced modules, never baked into the body mesh.**
3. **Measure the live-figure cost with a control in the same run**, per
   `06-rendering-and-camera.md` §6c.1 — two extra rigid renderers per figure against a ceiling of 64,
   while `PawnPose.Of` already costs 13.3 ms of a 22.5 ms frame at 384 colonists.

And one thing to take back to the owner: **a beard "a shade off" the hair is now the expensive
option**, not the cheap one. Identical is free.

## Sources

Measured on the disk and in the editor; no external sources.

- `POLYGON_BattleRoyale_Unity_2022_3_v1_9_2.unitypackage`, read as a tar archive before import.
- `Assets/Editor/Odyssey/BattleRoyaleProbe.cs` → `Logs/battle-royale-probe.txt`.
- `Assets/Editor/Odyssey/CharacterSwatches.cs` (`Classify`), `SwatchProbe.cs`.
- `Assets/Odyssey/Presentation/Rendering/ChunkRenderer.cs` (`ColonistModule`, `EnsureColonistModules`).
- Pack product page: https://syntystore.com/en-gb/products/polygon-battle-royale-pack

## Confidence

**High** on 1–8. Every claim is a number out of a probe run, and the two that contradicted a prior
belief (the bone naming, and hair colour leaving the atlas) were both wrong in the safe direction.

**Medium** on the far-form recommendation in 8: the mechanism is right and the bucket arithmetic is
sound, but no frame has been measured with it.

## Could not be determined

- **The UV extent of `Bun_01`, `Ponytail_01`, `Chops_01` and `Moustache_01`.** The probe filtered on
  names containing "Hair" or "Beard" and these four contain neither. They are almost certainly flat
  swatch cells like their neighbours, but that is an inference and they must be measured before they
  enter the pool.
- **What the two `Default_Hair` prefabs actually depict.** They recolour (extent 0.0000) and are 320
  and 438 vertices, so they are real hair rather than a bald scalp cap — but whether "bald" should be
  the absence of any prop is a judgement to make against a contact sheet, not a measurement.
- **Whether the locomotion clips retarget cleanly onto a BR body in motion.** The bones all resolve,
  which is necessary and not sufficient; the walk has not been watched.
- **What any of this costs per frame.** Not measured, deliberately — it belongs to the unit, with a
  control in the same run.
- **Whether PolygonGeneric's ungendered hair splits cleanly by gender**, and how many pieces each side
  gets. That is a contact sheet and an owner's eye, not a probe.
