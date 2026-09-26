# e-15 — SIMPLE Forest Animals, measured from its files

**Phase:** Ground, for bringing a licensed animal pack into the animal pipeline (design 29, 30).
**Status:** done 2026-09-26, read straight from the extracted `.unitypackage` (every GUID folder's
`pathname`, `asset`, `asset.meta`) with a standard-library binary-FBX reader, a PNG decoder and
YAML regexes. Nothing was imported into Unity; every number below is from the files, not from an
import, and the one that matters most (the drawn size) owes the same Unity measurement e-08 got.

## Question

What exactly is in the SIMPLE Forest Animals package — meshes, sizes, rigs, clips, variants,
palette, controllers, licence — and what does it take to bring it into our animal pipeline
(`AnimalImport`, the `ModuleEntry` animal rows in `PlayScene`, the figure mixer) under the rule
that licensed art lives only in the gitignored `Assets/Synty/` and nothing builds or tests
against it?

## Findings

### 1. The package

50 entries, all under **`Assets/SimpleForestAnimal/`** — *not* under `Assets/Synty/`:

| Folder | Contents |
|---|---|
| `Models/` | `Bear.fbx` 1.55 MB, `Boar.fbx` 1.12 MB, `Deer.fbx` 2.07 MB, `Fox.fbx` 2.57 MB, `Rabbit.fbx` 1.46 MB; five `SFA_Animal_<X>.controller` |
| `Prefabs/` | 31 prefabs (`Bear_01..03`, `Boar_01..03`, `Doe_01..02`, `Stag_01..03`, `Moose_Female_01..02`, `Moose_Male_01..03`, `Fox_01..03`, `Wolf_01..03`, `Raccoon_01..03`, `Skunk_01..03`, `Rabbit_01..03`) |
| `Materials/` | `SimpleForestAnimals.mat`, `Ground.mat` — both built-in **Standard** (`fileID 46, guid 0000000000000000f000000000000000`) |
| `Textures/` | `SimpleForestAnimalsTexture.png`, 10.5 KB |
| `Scenes/` | `Demo.unity`, `DemoSettings.lighting` |

Four FBX are FBX 7400 written by FBX SDK 2015.0; `Deer.fbx` is FBX 7700 from FBX SDK 2020.3 (the
file was re-exported later). All five: Y up, **UnitScaleFactor 1.0 (centimetres)**, TimeMode 11
(**24 fps**), one take `Take 001` split into clips by the `.meta`. The `.meta` files are
`ModelImporter serializedVersion 18` (Unity 5.x era, `timeCreated` January 2017),
`licenseType: Store`, `globalScale 1`, `useFileScale 1`, `animationType 2` (Generic),
`importMaterials 0` (the external `.mat` is used), `animationCompression 1`,
**`optimizeGameObjects 1`**, `motionNodeName` empty.

### 2. Meshes and sizes

Every mesh is a top-level node beside the rig, skinned to it, identity transform except the moose
(mesh node ×0.819) and the wolf (×0.786), both already folded into the bounds below. Unity at
`globalScale 1` with the file's centimetre unit imports 1 FBX unit as 0.01 m. Every prefab then
scales its root by a per-species factor, which is how the pack sets relative size.

| Species (file) | Verts | Tris | Imported W × H × L (m) | Mid-back (m) | Prefab root | Prefab H × L (m) | Real animal (shoulder / length) |
|---|---|---|---|---|---|---|---|
| Bear (Bear) | 536 | 874 | 0.19 × 0.34 × 0.49 | 0.28 | ×10 | 3.40 × 4.95 | 1.0–1.5 m / 1.5–2.8 m |
| Boar (Boar) | 384 | 668 | 0.11 × 0.21 × 0.39 | — | ×10 | 2.12 × 3.95 | 0.55–1.1 m / 0.9–2.0 m |
| Doe (Deer) | 458 | 790 | 0.11 × 0.36 × 0.39 | 0.23 | ×10 | 3.62 × 3.86 | ~1.05 m / 1.6–2.0 m |
| Stag (Deer) | 538 | 938 | 0.13 × 0.46 × 0.39 | 0.23 | ×11 | 5.01 × 4.24 (antlers) | 1.2–1.4 m / 1.8–2.3 m |
| Moose, female (Deer) | 464 | 802 | 0.12 × 0.37 × 0.40 | 0.23 | ×10 | 3.69 × 4.00 | 1.7–1.9 m / 2.4–3.0 m |
| Moose, male (Deer) | 588 | 1,042 | 0.17 × 0.46 × 0.42 | 0.23 | ×11 | 5.04 × 4.60 | 1.8–2.1 m / 2.5–3.2 m |
| Fox (Fox) | 552 | 968 | 0.06 × 0.20 × 0.31 | 0.14 | ×10 | 2.03 × 3.11 | 0.35–0.50 m / 0.9–1.4 m with tail |
| Wolf (Fox) | 489 | 846 | 0.07 × 0.19 × 0.27 | 0.14 | ×13 | 2.46 × 3.46 | 0.6–0.85 m / 1.5–2.0 m with tail |
| Raccoon (Fox) | 516 | 896 | 0.09 × 0.19 × 0.31 | 0.16 | ×7 | 1.34 × 2.18 | 0.23–0.30 m / 0.6–0.95 m with tail |
| Skunk (Fox) | 602 | 1,068 | 0.13 × 0.30 × 0.35 | (tail up) | ×5 | 1.48 × 1.74 | ~0.2 m / 0.55–0.75 m with tail |
| Rabbit (Rabbit) | 398 | 702 | 0.03 × 0.11 × 0.10 | 0.06 | ×10 | 1.08 × 1.03 (ears) | ~0.2 m / 0.35–0.50 m |

H is the top of the mesh (ears, antlers, a skunk's raised tail); L is nose to tail tip; mid-back is
the highest vertex in a 3 cm slab across the middle of the body (an approximation of the withers;
the boar's slab was empty). Real figures are typical adult ranges, not measured.

What that says:

- **At import every animal is a toy** — a 0.49 m bear, a 0.10 m rabbit. The pack relies on the
  prefab's ×5–×13 to size them, which makes them **two to four times life**: raccoon ~4×, fox
  ~3.4×, rabbit ~3×, bear ~2.4×, deer ~2.2×.
- **They are not to scale with each other.** Within one file the species share a rig and a body
  height: the doe, stag and both moose all stand 0.23 m at mid-back, and the fox and wolf 0.14 m.
  The prefabs differentiate them only by ×10/×11 and ×10/×13, so **the bull moose is barely bigger
  than the stag** (2.57 m against 2.55 m mid-back; life is ~1.6×) and **the wolf is 1.3× the fox**
  (life ~1.8×). The moose is *not* huge; the rabbit is *not* tiny.
- Proportions are cartoon: big heads, short legs, so a factor chosen by length and one chosen by
  height disagree by up to 2× (raccoon: 1.7 by height, 2.6 by length).
- **Cheap**: 668–1,068 triangles, against the rat's 4,004.

### 3. Rigs

One rig per file, Maya-style `SHJnt` naming, root chain `<Rig>` (null) → `<Rig>_SHJntGrp` (null)
→ `<Rig>_ROOTSHJnt` (the pelvis, e.g. 23.3 cm up on the bear). Every mesh in a file is skinned to
a subset of that one rig; bind poses are identical across the meshes.

| File | Rig | Joints | Skinned per mesh | Head / jaw | Tail |
|---|---|---|---|---|---|
| Bear | `CowRig` | 44 | 44 | `Neck_01..03`, `Head_JawSHJnt`, `Head_JawEndSHJnt`, `Head_TopSHJnt`, eyes | `Tail_01_01..05` |
| Boar | `PigRig2` | 32 | 32 | `Head_AuxSHJnt` and ears only — **no neck, no jaw** | `Tail_AuxSHJnt` |
| Deer | `Horse_Rig` | 42 | 36–39 | as bear | `Tail_01`, `Tail_02`, `Tail_Top` |
| Fox | `GoatRig` | 52 | 43 (raccoon) – 51 (skunk) | as bear | two chains: `GoatRig_Tail_01..05` and `SkunkTail_01..06` |
| Rabbit | `RabbitRig` | 38 | 38 | as bear | `Tail_01_01..02` |

Legs are `l_`/`r_` + `FrontLeg_`/`HindLeg_` + `Hip`, `Knee1`, `Knee2`, `Ankle`, `Ball`, `Toe`
(the boar's and rabbit's front legs have a single `Knee`), plus a `Clavicle_01_01` per side. The
naming is nothing like the Quaternius `FrontLeg.L` that `QuadrupedGait` binds to.

### 4. Clips

All 24 fps. All four clips per file have `loopTime 1`, `loopBlend 1`,
`keepOriginalPositionXZ 1`, `keepOriginalPositionY 1`; no `lockRoot*` keys.

| File | Idle | Walk | Run | Eat |
|---|---|---|---|---|
| Bear | 1–120, 4.96 s | 130–170, 1.67 s | 180–204, 1.00 s | 210–330, 5.00 s |
| Boar | 1–120, 4.96 s | 130–160, 1.25 s | 170–205, 1.46 s | 210–330, 5.00 s |
| Deer | 160–280, 5.00 s | 30–59, 1.21 s | 1–24, 0.96 s | 70–160, 3.75 s |
| Fox | 1–120, 4.96 s | 130–165, 1.46 s | 180–195, 0.63 s | 210–330, 5.00 s |
| Rabbit | 70–150, 3.33 s | 40–61, 0.88 s | 1–28, 1.13 s | 170–250, 3.33 s |

**No root motion.** The `ROOTSHJnt` X translation is 0 on every key of every file; Y and Z only
bob inside a clip (up to 2.3 cm on the boar's run, 3.7 cm on the rabbit's), and end minus start is
0 to within 0.03 cm. The clips are in place, so gait speeds must be **declared** on the row, as the
rat's are. The clip names Unity will show are the `.meta`'s (`Bear_Idle`, `Deer_Walk`, …), and
Doe, Stag and both moose share the Deer clips, Fox/Wolf/Raccoon/Skunk the Fox clips.

### 5. Variants and the palette

- **Geometry is identical within a species** (maximum vertex delta 0.0000) except `SM_Bear_01`,
  which differs from `_02`/`_03` by up to 0.69 cm (7 mm at import). The variants are **colour
  only**: each variant's UVs point into different swatches of the one palette.
- **Pack defect:** `SM_Raccoon_03`'s UVs are byte-identical to `SM_Raccoon_01`'s, so there are
  only two distinct raccoons.
- Each prefab holds **every** mesh of its FBX, with only its own `SM_<Variant>` active. The FBX's
  own model prefab has all of them active at once, overlaid.
- Palette: **1024 × 1024 RGB**, 684 distinct colours. The top half is two flat fills (`#6E6E6E`
  left, `#CA2828` right); the bottom half is rows of 32-px swatches, each row a ramp (row 512 black
  to white, row 768 greens, row 1023 purples). Texture import: mipmaps on, default (bilinear)
  filter, max 2048, default compression.

Dominant swatch per variant (the body colour):

| Species | _01 | _02 | _03 |
|---|---|---|---|
| Bear | `#824A35` red-brown | `#3F392B` near-black | `#775731` honey |
| Boar | `#211D16` black-brown | `#50422A` brown | `#6B3D2D` russet |
| Doe | `#827054` grey-tan | `#835E3C` brown | — |
| Stag | `#A46B55` red | `#827054` grey-tan | `#835E3C` brown |
| Moose, female | `#835E3C` | `#614F3C` | — |
| Moose, male | `#7D4D3F` | `#775731` | `#614F3C` |
| Fox | `#A55A31` orange | `#853E22` dark red | `#8E6D49` sandy |
| Wolf | `#939276` olive-grey | `#B1A797` pale | `#938365` tan |
| Raccoon | `#82796B` grey | `#3A3937` charcoal | = _01 |
| Skunk | `#36302C` black, `#9D9894` stripe | `#3F392B` | `#504841` |
| Rabbit | `#50483A` grey-brown | `#835E3C` brown | `#A2A083` sandy |

### 6. Controllers and prefabs

- Five controllers, one shape: default state **Locomotion**, a 1D blend tree on `Speed_f`
  (Idle 0, Walk 0.5, Run 1; the run at time scale 2 on the bear, 1.5 on the deer); an **Eat**
  state (speed 2, deer 1.5) entered when `Eat_b` is true and `Speed_f < 0.1`, left when `Eat_b`
  goes false. Parameters: `Speed_f` (float, **default 1**, so a dropped-in prefab runs on the
  spot) and `Eat_b` (bool).
- Prefabs: root with an `Animator` (the controller, the FBX's avatar, `applyRootMotion 1`), root
  scale as in §2. **We use none of this**: the figure mixer builds its own playables from the
  row's `locomotion` list, so the controllers and the Animator are inert to us.

### 7. Licence

Publisher **Synty Studios**, the SIMPLE line; 31 models, 11 species, Idle/Walk/Run/Eat, stated as
Built-in and URP, Unity 2020.3+ (Synty Store) / 2021.3.17, v1.0.2, 16 November 2023 (Asset
Store). The `.meta`'s `licenseType: Store` says this copy came through the Asset Store: the
**Standard Unity Asset Store EULA**, an *Extension Asset*, *Restricted Single Entity*. Synty's
own store sells it under its One-Time Purchase licence. Both allow use in a shipped game and
forbid redistributing the source assets. That is the same position as every other pack we hold:
**`Assets/Synty/` only, never committed, nothing may depend on it.**

### 8. Pipeline fit — what differs from the rat, the hog and the frog

| Concern | Our animals today (e-08, `AnimalImport`, `PlayScene` rows) | SIMPLE Forest Animals | What it takes |
|---|---|---|---|
| Where it lives | `Assets/Art/Custom/Animals`, CC0, committed, resolves on the runner | Installs to **`Assets/SimpleForestAnimal/`**, which is *not* gitignored | Must be relocated to `Assets/Synty/SimpleForestAnimal/` (below) |
| Material | Importer's own materials under URP, and the frog's `URP/Lit` paint | Built-in `Standard` + palette, **pink under URP**; smoothness 0.5 | `SyntyImport.UpgradeBuiltInMaterials` (scoped to `Assets/Synty`) converts it to `URP/Lit` with the palette as `_BaseMap`; drop the smoothness to near 0 to match the flat-shaded packs (a look to judge) |
| Scale | Per file `importer.globalScale` (pig ×0.105, rat ×0.09, frog ×0.24), row `scale = 1` | Four species in `Fox.fbx`, four in `Deer.fbx`, one importer each | Keep `globalScale 1` and set the size on the **row's `ModuleEntry.scale`**, per kind; `globalScale` cannot tell a wolf from a raccoon |
| Which mesh | One mesh per file | Every variant in the file, overlaid on the FBX's model prefab | Point the row at the pack **prefab** (`Wolf_01`) and cancel its root ×13 in the row scale, or have the figure builder switch every `SM_` off but one; check prefab-name ties (`PlayScene.LaterPacks`) |
| Bones | Exposed (`AnimalProbe` read them, `QuadrupedGait` drives them) | `optimizeGameObjects 1` **strips the bone transforms** | Set `optimizeGameObjects = false` in the import step, or no computed motion, head turn or measurement can reach a bone |
| Clips | Rat: Idle/Walk/Run/Jump/Attack/Death; hog: Idle only; frog: Idle/Jump | Idle/Walk/Run/Eat, all already looping, in place | Declared m/s per row as for the rat; `AnimalImport.Loops` would *un*-loop Eat (its name list is Idle/Walk/Run), so add Eat |
| Offline tests | `AnimalFigureTests` resolve on the runner because the art is committed | Will not resolve on the runner or a clean clone | Rows for these kinds must ask whether *their* art resolved (the `CanDrawColonists` pattern); none of the CC0 tests may move onto them |

**Behaviours with no clip in the pack** (would need computed motion, in the manner of
`WorkSwing` / the tree topple): lying down to sleep or rest at night; downed and dead (the health
unit's corpses); an attack or lunge (wolf, bear, boar as threats); turning on the spot. Not needed:
swimming and jumping (no animal does either), a hop (the rabbit has walk and run). **New**: Eat
has no counterpart in our mind yet — it is free grazing for the rest between legs.

**Headless import.** `SyntyImport.ImportAll -odysseyPackages <path>` imports synchronously, but
its guard only checks that `Assets/Synty` is non-empty, which it already is, so this pack would
land in `Assets/SimpleForestAnimal/` **and pass** — one `git add .` from committing licensed art.
Two ways to put it in the right place, both GUID-preserving: rewrite each `pathname` in the
extracted archive to `Assets/Synty/SimpleForestAnimal/…` and re-tar before import; or import and
then `AssetDatabase.MoveAsset("Assets/SimpleForestAnimal", "Assets/Synty/SimpleForestAnimal")`
in the same run. `Scenes/` and `Ground.mat` can be left out.

**GUID collisions: none.** None of the package's 50 GUIDs (the five FBX `bd230207…`, `443c31e3…`,
`3751d602…`, `f000ecb3…`, `0904c2f7…`; the texture `0e3dc26e…`; the materials `a47734c2…`,
`8d65acad…`; every prefab, controller and folder) appears as an asset GUID or as a reference in any
`.meta`, `.prefab`, `.asset` or `.mat` under `D:\code\odyssey\Assets`, Synty packs included.

## Recommendation

Bring it in, as a **licensed pack beside the colonists**, not as a replacement for the CC0 rat,
hog and frog, which stay the runner's proof that an animal draws.

1. Import headless and **move to `Assets/Synty/SimpleForestAnimal/`** in the same run; tighten
   `SyntyImport`'s guard to fail if anything new appears outside `Assets/Synty` (it would have
   passed this pack silently).
2. Run `UpgradeBuiltInMaterials`; in an import step for this folder set `optimizeGameObjects`
   false and leave `globalScale` at 1.
3. One row per kind pointing at the pack's prefab, with a **per-kind scale measured, not
   assumed** — extend `AnimalProbe` to photograph each at the cell. As a starting point, life-size
   from the imported mid-back is roughly bear ×4.3, doe ×4.5, stag ×5.6, moose ×8, fox ×3.0, wolf
   ×5.3, rabbit ×3.2 (divide by the prefab's own root scale if the prefab is used); then the same
   "is life-size too small beside a 2.49 m colonist" question e-08 left for the playtest.
4. Start with the **fox and the doe**: one file each, full clip sets, colours that read on grass;
   leave the moose until its size is decided, since the pack does not make it big.

## Sources

- `C:\Users\timjo\AppData\Local\Temp\claude\D--code-odyssey\efd61046-ca8d-4e3c-a440-0d05de7f8c71\scratchpad\pkg\` — the extracted package; probes `fbxprobe.py`, `probe2.py`, `probe3.py` beside it.
- `D:\code\odyssey-forest\docs\research\e-08-animal-fbx-inspection.md`, `docs\research\synty-import.md`, `docs\design\29-animals.md`.
- `D:\code\odyssey-forest\Assets\Editor\Odyssey\AnimalImport.cs`, `SyntyImport.cs`, `PlayScene.cs` (animal rows, ~line 1790), `.gitignore`.
- https://syntystore.com/products/simple-forest-animals-cartoon-assets
- https://assetstore.unity.com/packages/3d/characters/animals/simple-forest-animals-cartoon-assets-81692
- https://syntystore.com/pages/one-time-purchase-licence
- https://syntystore.com/pages/licences-overview

## Confidence

- **High** — contents, vertex and triangle counts, rig and bone names, clip ranges and fps, loop
  flags, absence of root motion, variant identity, the Raccoon_03 duplicate, controllers, prefab
  scales, materials, GUID absence: all read directly from the files.
- **High** — the imported size *in FBX units and at the prefab's scale*; **medium** for the metres
  Unity will actually draw, until an `AnimalProbe` shot confirms it (e-08's pig came in 100×
  off what the mesh said). The bind poses carry no hidden scale here, so a surprise is less likely.
- **Medium** — mid-back heights (a slab approximation) and hence the life-size factors.
- **Medium** — the licence reading (store pages; the EULA text itself was not read).

## Could not be determined

- The drawn size in Unity and how each species looks at the play camera — needs an import.
- Whether the Asset Store's current 1.0.2 ships a URP material the extracted copy lacks (the store
  says URP is supported; this copy has only Standard).
- Whether a pack prefab name (`Fox_01`, `Bear_01`, …) ties with one in another held pack.
- Whether the Eat clip reads as grazing from the play camera; whether the clips loop without a hitch.
