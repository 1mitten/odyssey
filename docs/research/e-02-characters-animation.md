# Lane E2 — Characters and animation: rigs, Base Locomotion coverage, gap plan

## Question

Which Synty rig should pawns use, do the owned ANIMATION Base Locomotion clips retarget onto it, and which of the vertical slice's required clips (idle, walk, carry, mine, build, sleep, eat, downed) exist versus need Mixamo or Blender work?

## Findings

**Rigs available.** Three humanoid rig families exist in the project, all with valid Mecanim humanoid avatars (per `docs/research/synty-inventory.md`):

- **Polygon standard rig** — `PolygonSyntyCharacter.fbx` (50 bones, 4 896 tris, Base Locomotion pack), `Generic_Characters.fbx` (50 bones, 796 tris, PolygonGeneric) and the pack `Characters.fbx` rigs (49 bones, 1 891–2 601 tris; Sci-Fi City, Farm, Western Frontier). This is the common ~50-bone Synty skeleton.
- **Sidekick rig** — `SidekickSyntyCharacter.fbx` (88 bones, 6 628 tris, Base Locomotion pack). Heavier; the full Sidekick modular tool is not owned.
- Character prefabs: 20 in `Assets/Synty/PolygonSciFiCity/Prefabs/Characters/` (`SM_Chr_Cop_01`, `SM_Chr_Garbage_Male_01`, `SM_Chr_Medical_Male_01`, aliens, androids, etc.) and 22 in `Assets/Synty/PolygonGeneric/Prefabs/Characters/` (`SM_Gen_Chr_Jumpsuit_*`, `SM_Gen_Chr_Street_*`, `SM_Gen_Chr_Underwear_*`, etc.).

**Base Locomotion coverage.** All 693 clips in the project come from `Assets/Synty/AnimationBaseLocomotion/`; the four art packs ship **no** animations of their own. The clip set is locomotion only, organised as `Animations/{Polygon|Sidekick}/{Masculine|Feminine|Neutral}/…`:

- Idle: standing, crouching.
- Walk / Run / Sprint / Crouch: forward plus full 8-way strafe sets; slope variants `Up25F` / `Down25F` (25° incline) for walk, run and sprint.
- Shuffle (4-way, standing and crouching), Turn 90°/180° L/R (standing and crouching).
- InAir: jump (idle/walk/run/sprint take-offs), fall short/large, land soft/medium/hard.
- Transitions: idle→walk/run (F/90/180), walk/run→idle, stand↔crouch, sprint→crouch.
- Additive: head-look, lean, T-pose.
- Every clip exists in Masc + Femn, in-place + RootMotion, and duplicated for the Sidekick rig (`A_MOD_BL_*`), which is why the count is large; the distinct motion vocabulary is small.

Because the clips are humanoid and every pack character has a valid humanoid avatar, the Polygon Base Locomotion clips retarget onto every `SM_Chr_*` / `SM_Gen_Chr_*` prefab through Mecanim with no per-character work — one AnimatorController serves all pawn meshes. Ready-made single-clip controllers exist under `Samples/Animations/` and two full controllers per rig (`AC_Polygon_Masculine/Feminine.controller`).

**Task clips are absent.** A name search across all of `Assets/Synty` for carry, mine, build, hammer, swing, sleep, lie, eat, sit, death, die, downed, crawl, climb and ladder found **no animation clips** — only environment meshes (`SM_Env_Mine_*` tunnels) and slope clips (`Down25F`). All six task/rest clips for the slice are missing.

**Supporting assets for task visuals exist as static props**: `SM_Gen_Wep_Pickaxe_01` (PolygonGeneric), `SM_Wep_Hammer_01` and `SM_Wep_Pickacxe_01` [sic] (Western Frontier) for hand-socketing; `SM_Gen_Chr_Attach_*` (hats, hair, beards, headsets — 20+ pieces in PolygonGeneric) and `SM_Chr_Attach_Wheat_01` (Farm) for pawn customisation on the Polygon rig.

**Mixamo viability confirmed.** Retargeting Mixamo clips onto Synty Polygon characters via Unity's humanoid avatar system is a well-trodden, officially demonstrated path — Synty publish their own tutorial for it, and community workflows and ready-made retargeting kits exist. Known caveats are minor foot-slide and occasional leg stretch, which are corrected in humanoid import settings and are in any case invisible at colony-sim camera distance; in-place clips plus code-driven root movement (our model anyway) sidestep most of it.

## Slice clip coverage table

| Clip | Status | Source |
|---|---|---|
| idle | **Covered** | `A_Idle_Standing_Masc/Femn` (Base Locomotion) |
| walk | **Covered** | `A_Walk_F_*` + 8-way strafes + `Up25F`/`Down25F` slope variants, in-place and root-motion |
| carry | **Missing** | Mixamo box-carry walk + carry idle, humanoid retarget; interim: walk clip + upper-body avatar mask holding pose |
| mine | **Missing** | Mixamo pickaxe/overhead-swing loop, retargeted; socket `SM_Gen_Wep_Pickaxe_01` to the hand bone |
| build | **Missing** | Mixamo hammering loop; interim: reuse the mine swing with `SM_Wep_Hammer_01` socketed |
| sleep | **Missing** | Mixamo lying/sleeping idle (or a one-pose clip); align to bed prop in code |
| eat | **Missing** | Mixamo sitting or standing eating loop; interim: idle + additive hand-to-mouth upper-body layer |
| downed | **Missing** | Mixamo knocked-down/death-fall into a lying idle loop (no ragdoll for the slice) |

Coverage: **2 of 8 covered, 6 missing** — exactly the locomotion/task split expected of a locomotion pack.

## Recommendation

**Rig: the Polygon standard ~50-bone skeleton** — pawns are the existing `SM_Chr_*` (Sci-Fi City) and `SM_Gen_Chr_*` (PolygonGeneric) prefabs, all driven by one shared AnimatorController; the Sidekick rig is not used. The Polygon rig is Mecanim-humanoid valid on every owned character mesh, so the Base Locomotion Polygon clip set and any Mixamo clip play on all of them through a single controller with zero per-character setup. Sidekick's 88 bones nearly double per-pawn skinning and animator cost at the 50-colonist target while adding nothing the slice needs, and the full Sidekick customisation tool is not owned. PolygonGeneric's `SM_Gen_Chr_Attach_*` pieces and the pickaxe/hammer props already fit the Polygon rig, giving pawn customisation and per-job hand tools for free.

**Retarget pipeline: Mixamo, committed as the single sourcing route for all six missing clips.** Download each Mixamo clip on the Y-bot (in-place where offered), import into Unity as Humanoid, map to the clip's own auto-generated avatar, and let Mecanim retarget at runtime — no Blender step required. Blender is reserved strictly for pose fixes that Mixamo cannot express (e.g. conforming the sleep pose to a specific bed prop), per the Synty-first rule in `CLAUDE.md`. Interim substitutions (mine swing reused for build; masked carry pose over walk) keep the slice unblocked while proper clips are retargeted. Mixamo-derived clips are saved outside `Assets/Synty/` (e.g. `Assets/Art/AnimationsThirdParty/`) so the simulation never depends on the licensed folder.

## Layer questions touched

**Stairs:** Base Locomotion ships 25° slope clips (`A_Walk_Up25F`, `A_Walk_Down25F`, plus run and sprint variants). The pack's one-cell stair `SM_Bld_Base_Stairs_02` rises 3.0 m over 2.5 m (~50°), so the 25° clip will read as "leaning uphill" rather than matching foot placement — acceptable at sim camera distance for the slice; revisit with a Blender-adjusted variant only if it grates.

**Ladders:** no climb clip exists anywhere in the owned packs (`SM_Gen_Bld_Ladder_01` is a mesh only). Source a Mixamo ladder-climb loop through the same retarget pipeline; for the earliest slice a masked climb pose translated vertically in code is an acceptable stand-in.

## Sources

- `D:\code\odyssey\docs\research\synty-inventory.md` (rig table, clip count, prefab counts)
- File-name surveys under `D:\code\odyssey\Assets\Synty\AnimationBaseLocomotion\Animations\`, `…\Samples\`, `…\PolygonSciFiCity\Prefabs\Characters\`, `…\PolygonGeneric\Prefabs\Characters\`, `…\PolygonGeneric\Models\` (names and counts only)
- [Synty Studios — How to animate a character with Mixamo for Unity (official tutorial)](https://www.youtube.com/watch?v=9H0aJhKSlEQ)
- [Unity Manual — Retarget humanoid animation](https://docs.unity3d.com/Manual/Retargeting.html)
- [Motion Forge Pictures — Retarget animations to Polygon/Synty characters](https://www.motionforgepictures.com/retarget-animations-to-polygon-synty-characters/)
- [GameDev.tv community — Working with Mixamo, getting perfect retargeting results](https://community.gamedev.tv/t/working-with-mixamo-getting-perfect-retargeting-results/205909)
- [Unity forum — Mixamo Synty studio model animations problem](https://forum.unity.com/threads/mixamo-synty-studio-model-animations-problem.845503/)

## Confidence

**High** for what exists in the project (direct file-name evidence: full clip taxonomy enumerated, task-clip absence verified by keyword sweep across all packs) and for the Mixamo pipeline being viable (official Synty tutorial plus multiple independent workflows). **Medium** on the exact visual quality of 25° slope clips on 50° stairs and of retargeted lying poses against bed props — both need a five-minute look in the editor, not further research.

## Could not be determined

- Whether the 49-bone Sci-Fi City rig and the 50-bone Generic rig differ by anything more than one twist/attachment bone (Mecanim makes this moot for playback, but it matters if we ever share skinned attachments across packs); needs an editor bone-list dump.
- Licence terms of individual Mixamo clips were not re-verified this session (Adobe's standard Mixamo licence permits use in games; confirm nothing has changed before shipping).
- Whether Base Locomotion's sample third-person controller scripts are worth reusing — irrelevant for a job-driven colony sim, so not assessed.
