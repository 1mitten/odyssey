# Lane E15 — Facial emotion and talking: what the owned rig can show, and at what size

Researched 2026-09-26, from two owner questions asked the same day: *can the faceplates be
implemented in game to give the characters emotion — can we do different emotions and animations
(talking)?*, and then *can we look into some talking emotion at least?*

**This file was first written from a byte search and was wrong in two places**; the corrections are
§"What the first version got wrong" at the bottom. Everything above it is measured.

## Question

What can a colonist's face show with the art we own — an expression, a blink, a mouth that talks —
and can a player see it at the sizes the game actually draws a face?

## Findings

### The faceplates pack is not a facial-emotion system

`PolygonAdultFacePlates` is two static meshes (`SM_Chr_Faceplate_Adult_Female/Male.fbx`, ~30 KB),
one prefab each. No `Deformer` node in either file, so nothing to morph; no variants. The file's
embedded source path (`...\PolygonKids\_Working\adultFaces\...`) places it as a side asset of the
Polygon Kids line — an adult face for a child-proportioned body. At most it is a second static face
shape, worn like the hair props `MC` already hangs on the head bone. It cannot carry an expression.

### No owned pack has blend shapes or a talking clip

Confirms `e-05`: one skinned mesh per character, nothing separable below the neck. Sidekick is the
only Synty product with facial blend shapes and the owner declined it (`e-05`). Every clip under
`AnimationBaseLocomotion` and `AnimationSwordCombat` was censused by name: locomotion, transitions,
in-air, and the `Lean`/`HeadLook`/`BodyLook`/`TPose` additives. No talk, greet, wave, emote or viseme.

### The rig: two facial bones that move geometry, and a jaw that moves no colonist

Parsed with a binary-FBX reader (node type, parent, and every skin cluster's vertex indices and
weights), on the two rigs the 29-body colonist pool is drawn from:

| Bone | Type, parent | Geometry it moves on a colonist body (weight ≥ 0.5) |
|---|---|---|
| `Eyes` | `LimbNode` under `Head` | 16 vertices on every male, 20 on every female: the two eyes, one bone for both, a box ~11–13 cm wide and 2–3 cm tall at 1.65–1.68 m |
| `Eyebrows` | `LimbNode` under `Head` | 14 vertices (male), 30 (female): both brows, one bone, 1.66–1.71 m |
| `Jaw` | `LimbNode` under `Head`, **`PolygonGeneric` only** | **none.** Its only full weights are on the skeleton body (62); hair pieces carry fractional weights on it, and one prisoner has 6 stray vertices at hip height. Battle Royale has no `Jaw` at all |

Same numbers on every body of a gender in both packs; the ghillie suit, the charred body and the
skeleton weight nothing to `Eyes`/`Eyebrows`. **One bone per feature means both brows move together**:
raise, lower, roll as a pair (one up, one down). The angry V and the sad inverted V need a left and a
right, which the rig does not have.

### The mouth exists, and cannot be opened

- **Women have lips**: 7–8 polygons in a colour cell of their own, 5 cm wide and 2 cm tall at
  1.58–1.60 m. **Men have a mouth-shaped gap**: 2 skin-coloured polygons (7 × 3.4 cm) inside a
  7-polygon cell the painter makes the beard colour.
- **Every corner of the mouth polygons is shared with a face polygon** (21 of 21, 10 of 10, 6 of 6 —
  the last column of `fbxlips.py`), and all of them are weighted to `Head`. Unity splits a vertex
  where its UV differs, so the lips arrive as their own vertices sitting exactly on the face's. Any
  displacement keyed on the lip cell — a bone, a vertex shader — pulls them off the face and **opens
  a crack into the inside of a back-face-culled head**, which would show the background straight
  through (reasoned from the shared corners, not photographed).
- So a moving mouth needs **new geometry of our own**: a small dark shape hung on the `Head` bone the
  way `MC` hangs hair, shown and scaled only while a colonist talks, positioned per body from the lip
  cell measured at catalogue-build time (the `CharacterSwatches` pattern). Nothing owned does it.

### What a player can see — measured by photograph, not by arithmetic

`Assets/Editor/Odyssey/FaceSheet.cs` (`scripts/unity.sh shot Odyssey.EditorTools.FaceSheet.Shoot`)
poses the two bones into seven candidate expressions — neutral, brows up 15 mm, alarmed (brows up
20 mm, eyes ×1.2), stern (brows down 8 mm, eyes 60 %), sceptical (brows rolled 12°), tired (brows
down 4 mm, eyes 45 %), blink (eyes 8 %) — on two men and two women from both packs, and photographs
them where a face is seen: the portrait as `PortraitStudio` frames it (165°, orthographic,
`stature × 0.135`), at its cached 128 px and at the 60 px the roster draws; and the play camera
(40° field, 48° pitch, figures at 1.4), square on, cropped and enlarged without filtering. Taken
2026-09-26 on the Windows dev machine (RTX 5070 Ti), 4× MSAA.

| Where | Size of a face | What reads |
|---|---|---|
| Portrait, 128 px (≈ the roster at 4K) | eyes ~7 px | **all seven, on everyone** — the women best |
| Portrait, 60 px (the roster at 1080p) | eyes ~3 px | raise, stern, roll, tired and blink on the women; on the men only the eyes appearing and disappearing |
| Play camera, 1080p, 10 m (closest zoom) | head ~50 px, brow move 2–4 px | the women's changes, if you are looking for them; **the men's eyes are hidden under the brow band** by the 48° pitch until the brows rise |
| Play camera, 1080p, 20 m | head ~25 px | nothing distinguishable |
| Play camera, 4K, 10 m | head ~100 px | clearly, on the women; modestly on the men |

The sheets are `Logs/faces-portrait-128.png`, `faces-portrait-60.png`, `faces-play-1080p-10m.png`,
`faces-play-1080p-20m.png` and `faces-play-2160p-10m.png` (gitignored; rerun the probe).

**Hair**: `faces-hair.png` puts all fifteen hair pieces on a man and a woman, brows neutral and
raised. Most sit clear of the brows; **three or four fringes (the swept and banged cuts) swallow a
raised brow**, so on those colonists an expression built on the brows is partly hidden.

## Recommendation

**Carry talking and mood in the world with the body and a marker, and use the face where the face is
big enough: up close and in the portrait.** Concretely, as one presentation-only unit (nothing in a
cell, a save or the hash):

1. **Talking** = a short performance on two colonists near each other: head nods and turns toward the
   partner (the gaze system already turns heads, design 23), brow bobs in rhythm, and a small speech
   marker over each speaker that reads at every zoom. **No mouth in the first cut**; our own mouth
   piece is a separate, cheap experiment to photograph with the same probe once the rest reads.
2. **Emotion** = the brows and eyes set from what the colonist already publishes: the mood band
   (`MoodBands`, 600 / 350) and tiredness (the rest need). Content → neutral or slightly raised;
   strained → stern; breaking → stern and narrowed; tired → half-lidded. Plus a **blink** every few
   seconds on every live figure, because a face that never blinks reads as dead the moment you notice.
3. **Portraits follow mood**: a portrait is cached per appearance today; the key gains the mood band,
   so a colony renders at most three portraits per look, once each.

Why this ranking: the photographs show the face alone cannot carry either at play distance —
nothing reads at 20 m at 1080p, which is where the game is mostly played — while a head movement is
the whole head (tens of pixels) and a marker is any size we choose. The face is still worth doing: it
is nearly free (two bone writes per live figure, at most 64, in the pass that already turns the head)
and it is what a player sees when they zoom in on someone or open their pane.

Rejected: **the faceplates** (no expressions in them); **Sidekick** (declined, e-05); **opening the
owned lips** (tears the head); **a mouth-only talk** (2–3 px at the closest zoom, and hidden on men
under a beard block).

## Sources

- Local, measured: binary-FBX parse of `PolygonGeneric/Models/Generic_Characters.fbx` and
  `PolygonBattleRoyale/Models/Characters/Characters.fbx` (Model types and parents, cluster indices and
  weights, polygon UV cells and shared corners) by `tools/fbx/fbxface.py` (the bones) and
  `tools/fbx/fbxlips.py` (the mouth), standard library only; the photographs from `FaceSheet.cs`;
  `PolygonAdultFacePlates/Models/*.fbx`; the clip census of both animation packs.
- `docs/research/e-05-character-customisation.md`, `e-06-modular-colonists.md`, `e-02-characters-animation.md`;
  `docs/design/20-avatars.md` §10 (portrait framing and cache), `23-head-turning-and-gaze.md`;
  `Odyssey.Hud.MoodBands`; `SliceCameraRig` (40°, 48°, 10–160 m).

## Confidence

**High** on the bone census, the jaw weighting nothing on any colonist, the mouth's shared corners,
the faceplates, and the clip census — parsed, not inferred. **High** on what reads where — photographed
at the game's own framings. **Medium** on the play-camera verdict at a real playing distance: the probe
is square on in a T-pose under sheet lighting, which is the *best* case; a colonist turned away, in
shadow or mid-stride shows less.

## Could not be determined

- Whether the owner finds a mouthless talk convincing, or wants the mouth piece — a look, so theirs.
- Whether an animated figure's clips write `Eyes`/`Eyebrows`. They are not humanoid-mapped bones, so
  humanoid clips should leave them alone; a build must confirm it before relying on it.
- How the men's baked beard block reads at all — every male body photographed here carries it, and
  it hides the eyes from above. It is outside this question but it is what most of the colony looks
  like up close.

## What the first version got wrong

Written earlier the same day from a byte search, and corrected by the parse and the photographs:

1. **It recommended a jaw flap for talking.** The `Jaw` bone exists on `PolygonGeneric` but moves no
   colonist's face, and Battle Royale has none. There is no bone-driven mouth on any colonist.
2. **It listed `Eyes`/`Eyebrows` as bones without checking** — the name census could not tell a bone
   from a mesh. They are bones, and they move real geometry; that part survived.
3. **Its first draft of the mouth said there was none**, from a vertex dump truncated before the
   lip rows. The women have lips and the men a mouth gap; neither can be opened.
4. **It said only a person in the editor could check the bones.** A batch shot could, and did.
