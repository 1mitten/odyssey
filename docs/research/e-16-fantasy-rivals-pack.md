# e-16 — POLYGON Fantasy Rivals: what it holds for the Pig Butcher

**Question.** The owner supplied `POLYGON_Fantasy_Rivals_Unity_2022_3_v1_4_2.unitypackage`
(2026-09-26) and wants its **Pig Butcher** as a large melee enemy with animation. What is in the
package, what must and must not be imported, and can our existing clips drive the character?

## Findings

- **1,496 entries in three folders.**
  - `PolygonFantasyRivals/`: 188 entries, the pack's own content.
  - `PolygonGeneric/`: 1,299 entries.
  - `SyntyPackageHelper/Editor`: 3 entries.
- **Every one of the 1,299 PolygonGeneric entries is already in the project, under the same path
  and the same GUID.** They were compared against each installed `.meta` and none was missing. So
  the package is unpacked with its own folder only, as Battle Royale and Shops were
  (`docs/setup/local-dev.md` §8a). Importing it whole would overwrite the shared copy that four
  other packs use.
- **Installed 2026-09-26** into the canonical `D:\code\odyssey\Assets\Synty\PolygonFantasyRivals`
  with `tools/synty/unpack.py extract … --only Assets/Synty/PolygonFantasyRivals --skip
  Assets/Synty/PolygonFantasyRivals/Scenes`: 188 matched, 188 written. The sample scene and its
  volume profile are left out. It is licensed content: gitignored, never committed, never a
  dependency of the simulation or its tests. The CI runner does not have it.
- **The character.** `Prefabs/Characters/SM_Chr_BR_PigButcher_01.prefab` holds all eleven "BR"
  giants as children of one skinned hierarchy from `Models/Characters_BR.fbx` (3.1 MB). Only
  `Character_Pig_Butcher_01` is active; the Barbarian Giant, Big Ork, Dwarf, Elemental, Fort and
  Mechanical Golems, Mutant Guy, Red Demon, Slayer and Troll are inactive siblings.
  - The FBX imports as **Humanoid** (`animationType: 3`), with 40 human bones mapped and
    `hasExtraRoot: 1`.
  - The bone names are the Battle Royale set (`Root`, `Hips`, `Spine_01…03`, `Clavicle_L`,
    `Shoulder_L`, `Elbow_L`, `Hand_L`, `IndexFinger_01…04`, `Finger_01…04`, `Thumb_01…03`,
    `UpperLeg_L`, `LowerLeg_L`, `Ankle_L`, `Ball_L`, `Toes_L`, `Neck`, `Head`, `Eyes`,
    `Eyebrows`). Finger names carry **no side suffix**, as in Battle Royale (design 29), so any
    name-based finger lookup must go through the Animator's human bones.
  - `importAnimation: 0`. The prefab's Animator names the FBX's avatar and **no controller**.
  - Local scale 1 throughout; `globalScale 1`, `useFileScale 1`.
- **The weapon.** `Prefabs/Weapons/SM_Wep_PigButcher_01.prefab` over `Models/SM_Wep_PigButcher_01.fbx`
  (49 KB). It is a separate prop, not parented in the character prefab, so it can take our weapon
  path (`PawnFigureDirector.SwapWeapon`), which seats a ground-item prefab in the right hand.
- **Materials.** Four atlases × four colour variants (`FantasyRivals_01…04_A…D`), each on
  `PolygonGeneric/Shaders/Generic_Basic.shadergraph`, which is already installed. There are also
  emissive maps and a few FX materials (runes, smoke, glow) that the butcher does not use.
- **No animation clips.** The only `.anim` files are `Misc/Runes_Inner.anim` and
  `Misc/Runes_Outer.anim`, which spin a rune decal. So the butcher is animated by the packs already
  installed:
  - **Base Locomotion**: idle, walk and run.
  - **Sword Combat's Polygon set** (`docs/research/synty-sword-combat.md`): the heavy swings
    `A_Attack_HeavyCombo01A/B/C_Sword` and `HeavyFlourish01`, stagger, knock-down and death. These
    are one-handed and suit a cleaver.
  - All of these are Humanoid and retarget onto any Humanoid avatar. Neither pack has a
    two-handed or overhead slam.

## Recommendation

Import `PolygonFantasyRivals/` alone, which is done, and add it to `PlayScene.LaterPacks` so that
none of its prefab names wins a tie against a pack already in use. Build the butcher's figure row
from `SM_Chr_BR_PigButcher_01`, pinned under the Rivals folder, and drive it with the person clip
rows (Masc) and the Sword Combat heavy swing. Seat `SM_Wep_PigButcher_01` as the item row of its
cleaver. Measure the drawn height with `FigureBuild.Height` before choosing its scale; do not guess
it from the file.

## Sources

- The package, `C:\Users\timjo\Downloads\POLYGON_Fantasy_Rivals_Unity_2022_3_v1_4_2.unitypackage`,
  read entry by entry with Python's `tarfile`, 2026-09-26: every `pathname`, the
  `Characters_BR.fbx.meta` import settings, and the prefab's GameObjects and references.
- The installed `Assets/Synty/PolygonGeneric/**/*.meta` files, compared GUID by GUID.

## Confidence

**High.** Everything above was read from the files.

## Could not be determined

- **The butcher's drawn height and proportions.** They need the FBX imported and measured in Unity
  (`FigureBuild.Height`).
- **Whether the cleaver's grip suits the Sword Combat swings**, which were authored for a sword.
  That is a first-play question.
- **Whether the character's Humanoid avatar needs `hasExtraRoot` handled** differently from Battle
  Royale's. Both set it, so it is expected to behave the same.
