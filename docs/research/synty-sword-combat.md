# Synty Sword Combat animation pack — what it holds

**Question.** Which clips in the owner's Synty *Sword Combat* animation package (supplied
2026-09-23 as `ANIMATION_Sword_Combat_Unity_2021_1_v1_2_0.unitypackage`) are usable for Odyssey's
melee MVP, and on what terms?

## Findings

- **Installed 2026-09-23** into the canonical `D:\code\odyssey\Assets\Synty\AnimationSwordCombat`
  (every worktree junctions to it) by unpacking the `.unitypackage` the way Unity does — asset,
  `.meta` and path per GUID — so **Synty's GUIDs are preserved**. 308 of 383 entries written.
  **Deliberately left out:** `Assets/Synty/Tools/SyntyPropBoneTool` (runtime and editor C# that
  would compile into every junctioned checkout), the sample scenes, timelines and the sample
  locomotion controller. Licensed content: gitignored, never committed, never a dependency of the
  simulation or the tests. The runner does not have it.
- **Two rig sets.** `Animations/Polygon/` (the ~50-bone Polygon humanoid our colonists use,
  `docs/design/06-rendering-and-camera.md` §949) and `Animations/Sidekick/` (the 88-bone rig,
  rejected for colonists). **Only the Polygon set is used**, and the catalogue search must be
  restricted to it, because the two sets' clip names differ only by a `MOD_SWD` infix and a
  suffix. Each clip also ships a `_RootMotion` variant and many a `_ReturnToIdle`; **only the
  in-place, non-returning clips are used** — the simulation owns position.
- **Humanoid import.** 116 of the Polygon `.meta` files import as Humanoid (`animationType: 3`),
  so they retarget onto any of our 61 bodies. Two import as Generic (`animationType: 2`) and are
  unusable without a re-import we are not allowed to make inside `Assets/Synty`:
  `Dodge/A_Dodge_R_Sword` and `Idle/Base/A_Idle_Base_Sword`.
- **Every attack is already cut at its impact.** Each attack FBX carries three sub-clips —
  *WindUp*, *Hit*, *FollowThrough* — so the frame the blade lands is authored, not guessed. At
  the FBX's 30 fps:

| Clip (Polygon, in place) | Whole | Wind-up ends (impact) | Length at 30 fps |
|---|---|---|---|
| `A_Attack_LightCombo01A_Sword` | 1–25 | 11 | 0.83 s |
| `A_Attack_LightCombo01B_Sword` | 25–45 | 30 | 0.67 s |
| `A_Attack_LightCombo01C_Sword` | 45–67 | 56 | 0.73 s |
| `A_Attack_LightFencing01_Sword` | 1–37 | 15 | 1.23 s |
| `A_Attack_LightLeaping01_Sword` | 1–48 | 31 | 1.60 s |
| `A_Attack_HeavyCombo01A_Sword` | 1–62 | 30 | 2.07 s |
| `A_Attack_HeavyStab01_Sword` | 1–42 | 27 | 1.40 s |
| `A_Attack_HeavyFlourish_Sword` | 1–62 | 42 | 2.07 s |

- **Reactions.**
  - Hit react F/B/L/R: 1–26 (0.87 s).
  - Hit stagger F/B/L/R: 1–33 (1.1 s). This is the "knockback".
  - Dodge F/B/L: 1–18 (0.6 s). Dodge roll F/B/L/R: 1–36 (1.2 s).
  - Stun: begin 1–28, **loop 28–68**, end 68–105.
  - Knockdown: begin 1–22, **loop 22–59**, get up 59–120. This is "downed".
  - Block: begin/loop/end. Parry F/L/R, plus a counter-shove and a pommel strike.
- **Death.** Death F/B/L/R (1–44 / 1–52 / 1–35 / 1–35), each with a one-frame **`_Pose`** clip
  holding the final pose. That pose is the corpse.
- **Idles.**
  - Base sword idle (Generic, unusable), sheathed idle (loop).
  - Energetic stance and flourish fidgets, a menacing idle.
  - Draw and sheathe, masc and femn.
- **Missing from the pack:**
  - No unarmed punch, no two-handed axe swing.
  - Nothing for quadrupeds.
  - No locomotion beyond one sample sprint, which is left out.

## Recommendation

Map by role, not by name:
- **Light swing:** `LightCombo01A/B/C`, alternated.
- **Heavy swing:** `HeavyCombo01A`, `HeavyStab01`.
- **Hit react:** `Hit_*_React`, chosen by direction. **Big hit or stun:** `Hit_*_Stagger`.
- **Dodge:** `Dodge_F/B/L` (never `_R`). **Downed:** `KnockDown_Begin`, then `_Loop`.
- **Death:** `Death_*`, then its `_Pose`.

Play each at the speed that puts its wind-up end on the simulation's `windupTicks`. Every role
has a computed fallback, because a checkout without the pack must draw a fight too. The names
live only in the editor-side catalogue build (`Assets/Editor/Odyssey/PlayScene.cs`), never in
runtime code. Punch and the hog's bite are computed.

## Sources

- The package itself, `C:\Users\timjo\Downloads\ANIMATION_Sword_Combat_Unity_2021_1_v1_2_0 (1).unitypackage`,
  read entry by entry, and the installed `.fbx.meta` files, 2026-09-23.

## Confidence

**High.** Everything above was read from the files; frame numbers are the importer's own
sub-clip ranges. **Assumed:** 30 fps. It is Synty's standard; confirm on the first import.

## Could not be determined

- Whether the Polygon clips sit well on our *recoloured* bodies of every height. That is the first
  playtest question, not a desk question.
- Whether the sword grip in the clips suits a bat or a crowbar held the way `GripTool` fits them.
