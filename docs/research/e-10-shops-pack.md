# e-10 — The POLYGON Shops pack for cooking

## Question

Which models in the licensed Synty **POLYGON Shops** pack (`POLYGON_Shops_Unity_2022_3_v1_6_6.unitypackage`)
could serve a cooking feature, how big they are against the 2.5 × 2.5 × 3.0 m cell, which shader and
texture atlas they use, and whether the pack is safe to import next to the packs already in
`Assets/Synty/`.

Method: the package (a gzipped tar of GUID folders) was streamed in a scratch folder outside every
repository. Nothing was imported into any project and nothing from the pack is in this repository;
this file holds names and measurements only. Prefab YAML was read for mesh references and child
transforms. The referenced FBX files were parsed with a small binary-FBX reader: vertices, the
`UnitScaleFactor` (1.0 = centimetres on every file), the `.meta` `globalScale` (1.0 everywhere) and
`useFileScale` (1 everywhere), and each model's `RotationPivot`, which Unity bakes out of the mesh.
**The pivot matters**: without subtracting it, multi-part prefabs such as the stove measured 2.53 m
wide and the drinks fridge 27.6 m deep, because a door's vertices are stored relative to the root
and its prefab transform then offsets it a second time. With it, the doors land on their hinges and
every figure below is a plausible object.

## Findings

### Sizes

Bounds are the whole prefab (every child mesh, under its prefab transform) in Unity metres at
import scale 1.0. W is Unity X, D is Z (the fronts face +Z), H is Y. Every mesh sits on its pivot
(minimum Y ≈ 0) unless noted.

| Prefab (`SM_Prop_…`) | Mesh (`Models/…fbx`) | W × D × H (m) | Fits one cell? | Suggested role |
|---|---|---|---|---|
| `Kitchen_Stove_Oven_01` | `SM_Prop_Kitchen_Stove_Oven_01` (5 parts: body, two doors, two trays) | 1.74 × 1.18 × 1.22 | yes | cooker |
| `Kitchen_Grill_01` | `SM_Prop_Kitchen_Grill_01` (7 parts) | 1.74 × 1.14 × 1.35 | yes | cooker (alternative) |
| `Kitchen_Pan_01` | `SM_Prop_Kitchen_Pan_01` | 0.49 × 0.76 × 0.11 | yes | pan |
| `Kitchen_Pot_01` | `SM_Prop_Kitchen_Pot_01` (2 parts) | 0.61 × 0.45 × 0.39 | yes | pan (pot) |
| `Market_Wall_Fridge_01` | `SM_Prop_Market_Wall_Fridge_01` | 1.54 × 1.04 × 2.44 | yes | fridge |
| `Market_Wall_Fridge_02` | `SM_Prop_Market_Wall_Fridge_02` | 4.66 × 1.04 × 2.44 | **no — two cells** | fridge (wide) |
| `Market_Deli_Fridge_01` | `SM_Prop_Market_Deli_Fridge_01` (3 parts) | 2.21 × 1.59 × 1.50 | yes | fridge (counter) |
| `Market_Drinks_Fridge_01` | `SM_Prop_Market_Drinks_Fridge_01` (3 parts) | 0.95 × 1.11 × 2.29 | yes | fridge |
| `Market_Freezer_01` | `SM_Prop_Market_Freezer_01` (5 parts) | 2.89 × 1.45 × 1.10 | **no — two cells** | fridge (chest freezer) |
| `Kitchen_Prep_Table_01` | `SM_Prop_Kitchen_Prep_Table_01` | 1.73 × 1.03 × 1.05 | yes | table (butcher's / prep bench) |
| `Cafe_Table_Small_01` | `SM_Prop_Cafe_Table_Small_01` | 0.95 × 0.95 × 0.86 | yes | table |
| `Cafe_Table_Large_01` | `SM_Prop_Cafe_Table_Large_01` | 1.99 × 1.16 × 1.10 | yes | table |
| `Cafe_Table_Folding_01` | `SM_Prop_Cafe_Table_Folding_01` | 0.71 × 0.77 × 0.72 | yes | table |
| `Shop_Table_01` | `SM_Prop_Shop_Table_01` | 1.71 × 1.71 × 1.05 | yes | table (tall, counter height) |
| `Cafe_Chair_01` | `SM_Prop_Cafe_Chair_01` | 0.52 × 0.58 × 0.98 | yes | chair |
| `Cafe_Chair_02` | `SM_Prop_Cafe_Chair_02` | 0.48 × 0.66 × 1.00 | yes | chair |
| `Shop_Chair_01` | `SM_Prop_Shop_Chair_01` (2 parts) | 0.66 × 0.68 × 0.93 | yes | chair |
| `Bar_Stool_01` | `SM_Prop_Bar_Stool_01` | 0.36 × 0.36 × 0.74 | yes | chair (stool) |
| `Food_Plate_01` | `SM_Prop_Food_Plate_01` | 0.44 × 0.44 × 0.03 | yes | plate |
| `Food_Bowl_01` | `SM_Prop_Food_Bowl_01` | 0.27 × 0.27 × 0.09 | yes | plate (bowl) |
| `Cafe_Table_Fork_01` | `SM_Prop_Cafe_Table_Fork_01` | 0.05 × 0.26 × 0.02 (pivot mid-height) | yes | cutlery |
| `Cafe_Table_Knife_01` | `SM_Prop_Cafe_Table_Knife_01` | 0.03 × 0.26 × 0.02 (pivot mid-height) | yes | cutlery |
| `Kitchen_Knife_Butcher_01` | `SM_Prop_Kitchen_Knife_Butcher_01` | 0.02 × 0.47 × 0.16 (pivot 0.14 above the blade's lowest point) | yes | butcher knife |
| `Kitchen_Knife_01` | `SM_Prop_Kitchen_Knife_01` | 0.02 × 0.60 × 0.08 | yes | butcher knife (alternative) |
| `Kitchen_Chopping_Board_01` | `SM_Prop_Kitchen_Chopping_Board_01` | 0.82 × 0.57 × 0.04 | yes | table dressing |
| `Kitchen_Spatula_01` | `SM_Prop_Kitchen_Spatula_01` | 0.16 × 0.63 × 0.18 | yes | cutlery (cook's tool) |
| `Food_Meat_Patty_Raw_01` / `_Cooked_01` / `_Burnt_01` | one FBX each, same names | 0.20 × 0.20 × 0.04 (cooked 0.05) | yes | food-raw / food-cooked / food-burnt |
| `Food_Sausage_Raw_01` / `_Cooked_01` / `_Burnt_01` | one FBX each | 0.09 × 0.31 × 0.08 | yes | food-raw / cooked / burnt |
| `Food_Ribs_Small_Raw_01` / `_Cooked_01` / `_Burnt_01` | one FBX each | 0.44 × 0.39 × 0.13 | yes | food-raw / cooked / burnt |
| `Food_Chicken_Leg_Raw_01` / `_Cooked_01` / `_Burnt_01` | one FBX each | 0.28 × 0.12 × 0.12 (pivot at mid-height) | yes | food-raw / cooked / burnt |
| `Food_Carrot_01` | `SM_Prop_Food_Carrot_01` | 0.09 × 0.34 × 0.08 (pivot at mid-height) | yes | food-raw (the crop we already grow) |
| `Food_Salad_01` | `SM_Prop_Food_Salad_01` | 0.44 × 0.33 × 0.12 | yes | food-cooked (a meal on no plate) |

All three variants in each Raw/Cooked/Burnt triplet share one footprint, so swapping the mesh on a
state change never moves the thing. The pack also has `Ribs_Large_*` triplets, `Oven_Tray_01_Ribs_*`
and `Plate_Ribs_01`, `Sausage_Cooked_02`, and two pizza ovens, not measured.

**Scale against the game.** Synty's furniture is already about 1.15× life (the small café table
stands 0.86 m against a real 0.75; the chair 0.98 against about 0.85) and its tableware about 1.6×
(a 0.44 m plate against a real 0.27). Odyssey draws a colonist at "half again life size", about
2.5 m (`FigureBuild.FallbackHeight`). So at import scale the tableware already matches a colonist
and the furniture reads slightly small. Nothing here is too big for a cell at scale 1.0 apart from
the two market units flagged above, and the stove still fits one cell at ×1.3 (2.27 m).

### Shaders and materials

- **59 of the pack's 62 materials use `PolygonGeneric/Shaders/Generic_Basic.shadergraph`**, a URP
  Shader Graph — the same shared Synty shader family the installed packs already use. The other
  three (sky, video screens, an FX circle) point at built-in shaders and play no part in cooking.
- **Every one of the 40 prefabs above uses `PolygonShops_Mat_01_A`**; the fridges and freezer add
  `PolygonShops_Glass_01` (URP transparent keywords) for their glass. `Mat_01_A` is alpha-tested
  (`_ALPHATEST_ON`) with an emission map.

### Texture atlas

**One shared atlas.** `PolygonShops_Mat_01_A` reads `PolygonShops_Texture_01_A.png` (4096 × 4096,
imported at a 2,048 default with a 4,096 platform override) and `PolygonShops_Texture_01_Emissive.png`.
All **298** prefabs under `Prefabs/Food/` use that single material. So every kitchen, furniture and
food model in the table shares one material, which is the good case for instancing: the whole
kitchen batches by mesh with no per-state material swap. `_B` and `_C` colourways of the same
atlas ship beside it.

### Is it safe to import?

- **The package ships `Assets/Synty/PolygonGeneric/**`** (1,300 entries) and
  `Assets/Synty/SyntyPackageHelper/**` (3 files) as well as `Assets/Synty/PolygonShops/**`
  (5,454 entries: 1,970 prefabs, 3,343 model files including collision assets, 67 materials,
  64 textures).
- **PolygonGeneric GUIDs are identical**: all 1,299 installed paths are present in the pack under
  the same GUID; no path exists on one side only; no GUID sits under a different path. (The same
  finding as the Battle Royale import, e-06.)
- **Content differs in 110 files.** Every shader, shader graph and subgraph is identical (same size;
  those under 400 KB compared byte for byte). **109 `.mat` files differ**, all by serialisation only:
  the pack's are the 2022.3 form (`version: 7`), the installed copies were re-serialised by Unity 6
  (`version: 10`, extra `_DstBlendAlpha`, `_SrcBlendAlpha`, `_USE_METALLIC_SMOOTHNESS_MAP`,
  `m_AllowLocking`, a `MOTIONVECTORS` disabled pass). One `.meta` differs:
  `Models/Base/SM_Bld_Base_Floor_Combined_01.fbx.meta`. Importing PolygonGeneric would roll those
  back to the older form, which Unity would upgrade again — churn for no gain.
- **`SyntyPackageHelper` is byte-identical to the installed copy** (same GUIDs). It is an
  `[InitializeOnLoad]` script that calls the Package Manager (`Client.Add`); since it is already
  present, the import adds no new behaviour.

So the pack is **safe to import provided only `PolygonShops/` is taken.**

### Steam and smoke

The Shops folder has **no steam or smoke of its own** (its `Prefabs/FX/` holds escalator and
fountain effects). The pack's PolygonGeneric carries `FX_Smoke_01` and `FX_Fire_*`, which are
already installed by GUID. The installed `Assets/Synty/PolygonParticleFX/Prefabs/` has the useful
ones: **`FX_Steam_01`, `FX_Steam_02`, `FX_Steam_03`**, `FX_Smoke_White_Small_01`,
`FX_Smoke_White_01`, `FX_Smoke_White_Large_01`, `FX_Smoke_Black_Small_01`, `FX_Smoke_Black_01`,
`FX_Smoke_Black_Large_01`, `FX_Smoke_Trail_Small_01`, `FX_Smoke_Trail_Large_01`, and
`FX_Fire_Small_01`–`03`.

## Recommendation

**One model per role:**

| Role | Model | Why |
|---|---|---|
| cooker | `Kitchen_Stove_Oven_01` | one cell, has doors and trays as separate parts to animate open; `Kitchen_Grill_01` is the same width if a second tier is wanted |
| fridge | `Market_Drinks_Fridge_01` | the only upright that reads as a domestic fridge: under a metre wide, 2.29 m under a 3.0 m storey |
| table | `Cafe_Table_Small_01` for dining, `Kitchen_Prep_Table_01` for the butcher's bench | one cell each, and the prep table is the work surface the knife goes on |
| chair | `Cafe_Chair_01` | |
| plate | `Food_Plate_01` | already at colonist scale |
| cutlery | `Cafe_Table_Fork_01`, `Cafe_Table_Knife_01` | |
| pan | `Kitchen_Pan_01` | |
| food raw / cooked / burnt | the `Food_Meat_Patty_*` triplet first, `Food_Chicken_Leg_*` second | identical footprints across states; the patty is the clearest silhouette from the play camera |
| butcher knife | `Kitchen_Knife_Butcher_01` | its pivot is not at the grip, so a hand attachment needs an offset |
| steam | `FX_Steam_01` (PolygonParticleFX, already installed) | the Shops pack has none |

**Scale:** import at 1.0 and draw at 1.0 first. Tableware is already at colonist scale; if the
furniture reads small beside a 2.5 m colonist, fit it to target metres the way `BedShape` does
(design 20) rather than scaling the import — roughly ×1.3 matches the colonist, and the stove still
fits its cell at that.

**Import procedure** (the owner, in the editor):

1. Close any editor on the worktree first, and import in the **main checkout `D:\code\odyssey`**,
   where `Assets/Synty/` holds the real packs. In a worktree (`D:\code\odyssey-cooking`) each pack
   under `Assets/Synty/` is a **junction** into the main checkout, so an import there would write
   PolygonGeneric *through* the junction into the real packs, and would leave PolygonShops as a
   real folder in that worktree alone.
2. *Assets → Import Package → Custom Package…*, choose the `.unitypackage`, and in the dialog
   **untick `PolygonGeneric` and `SyntyPackageHelper`**; leave only `PolygonShops` ticked.
3. Junction it into the worktree: `PolygonShops` and `PolygonShops.meta` from
   `D:\code\odyssey\Assets\Synty\` into `D:\code\odyssey-cooking\Assets\Synty\`, as for the other
   packs. Remember the rule in memory: unlink junctions before deleting a worktree.
4. `git status` must show nothing under `Assets/Synty/` (it is gitignored). Check
   `git diff HEAD -- ProjectSettings/ Assets/Settings/` afterwards; an import can re-serialise them.
5. The Shops materials arrive with GPU instancing as Synty ships it; whether the existing
   `SyntyInstancingKeepAlive` staging covers `Generic_Basic` for a player build is to be checked the
   first time a Shops prop is drawn instanced.

## Sources

- `C:\Users\timjo\Downloads\POLYGON_Shops_Unity_2022_3_v1_6_6.unitypackage` (read-only, streamed)
- In it: `Assets/Synty/PolygonShops/Prefabs/Props/*.prefab`, `Prefabs/Food/*.prefab`,
  `Models/*.fbx` and their `.meta`, `Materials/PolygonShops_Mat_01_A.mat`,
  `Materials/Misc/PolygonShops_Glass_01.mat`, `Textures/PolygonShops_Texture_01_A.png`,
  `Assets/Synty/PolygonGeneric/**`, `Assets/Synty/SyntyPackageHelper/**`
- `D:\code\odyssey\Assets\Synty\PolygonGeneric\**\*.meta` and files (read-only comparison)
- `D:\code\odyssey\Assets\Synty\PolygonParticleFX\Prefabs\` (file names)
- `D:\code\odyssey\Assets\Odyssey\Presentation\World\FigureBuild.cs` (`FallbackHeight`, colonist scale)
- `docs/design/20-beds.md` (`BedShape` in metres), `docs/research/e-06-modular-colonists.md`
  (the earlier identical-GUID finding)

## Confidence

**High** for the GUID comparison, the material and atlas assignments, and the bounds of single-mesh
prefabs (read straight from the vertex arrays). **Medium-high** for multi-part prefabs (stove,
grill, fridges, freezer, pot, shop chair): they depend on reproducing Unity's pivot baking, and the
check is that the stove's door pivots come out exactly at the prefab's door positions. **Medium**
for the scale judgement, which is arithmetic against the colonist's drawn height rather than a look
in the game.

## Could not be determined

- How the models look from the play camera beside a colonist — a screenshot after import settles it.
- Whether the Raw/Cooked/Burnt meshes share geometry and differ only in UVs (they are separate FBX
  files; heights differ by a few millimetres, so at least the patty's do not).
- Seat and work-surface heights inside each bound (only the whole-object box was measured).
- Whether the existing instancing keep-alive covers the Shops materials in a player build.
- The in-editor import time and `Library/` growth for 5,454 entries.
