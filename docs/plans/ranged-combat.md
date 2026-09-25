# Ranged combat — the plan

**Designed 2026-09-25** after an interview of three rounds (`docs/research/ranged-interview.md`;
every answer is in the decision table of `docs/design/47-ranged-combat.md` §1). **Not yet approved
for code**: the next hard stop is the owner's reading of design 47, whose §8 lists seven
recommendations to confirm or overrule. This planning work is on `claude/beautiful-cannon-pldakk`,
**PR #220** (documents, plus the baked gunshot and its bake script). **Approved and built 2026-09-25** (R0–R4, H1, P1–P3 and R7 on `claude/ranged-combat`, PR #225; played three times and merged with `main`, design 47 §10–§13; R5, R6 and P4 not started). **Reviewed 2026-09-25**
(design 47 §9): renumbered 44 → 47, the prop moved to Battle Royale's pistol, the contracts corrected
against `main`, and the animation, flight and sound decisions the first draft left open are now in
the design, so no unit below has anything left to invent. The research is `a-10-ranged-combat`, `a-10-projectile-path`, `c-3d-shot-line`,
`d-21-projectile-rendering`, `d-22-procedural-aim-and-recoil` and `e-10-gun-animation-packs`.

## Units

`| Unit | What | Checkpoint | Goldens | State |`, the combat plan's form. R is the simulation, H the
Hud, P presentation. Sizes S / M / L. Branch per unit `claude/ranged-<unit>`.

| Unit | What | Checkpoint | Goldens | State |
|---|---|---|---|---|
| R0 (M) | Contracts and content, claimed once: every handle in design 47 §3a — `Job_AttackRanged`, `Item_Pistol`, `Skill_Shooting` **at the next free values when it merges** (23 / 11 / 6 on `main` today, raced by #213, #184 and #223), the four `PawnPurpose` salts K12/K13/K16/K17 re-grepped, `AttackStyle.Pistol`, `DamageKind.Bullet`, `CombatEventKind.Shot`, `PawnGesture.Fire`, four `PawnPurpose` salts, the `GridSize` millimetre constants; `RangedDef` / `AttackDef.ranged` / the `CombatDef` fields / `SpeciesDef.interceptPerMille`; `ProjectileView` + writer + store; save format 10 with the Shooting backfill; the CSV rows, wiki and label registry; `ModuleIds.ItemPistol` and the four empty clip rows; `JobLabels`, `ItemLabels`, `CombatOrders.IsWeapon`, `SkillCatalogue`; a stub driver that fails; the content fingerprint; **one golden re-bake, probe-diffed on both parents** (the first six skills identical on all three boards) | fast, Long, EditMode green; probe clean | **move once** | built 2026-09-25 (`claude/ranged-combat`); goldens re-baked, probe identical |
| R1 (M) | `LineOfSight` (3D integer supercover, symmetric, doors, slabs, the lenient corner) and `RangedGeometry`; `LineOfSightTests` incl. the terrace hit, the slab control, the symmetry and mirror properties | fast | none | built 2026-09-25 |
| R2 (M) | `Projectiles` (save / hash / publish), `IRangedRules` / `RangedRules`, `CombatSystem.LandBullet` and `FireOrLose`, `ApplySwing`'s `Pawn?` and skill line, `BuildingTargets` ×1, dodge 0 mid-aim; `RangedMathTests`, `ProjectileTests`, `BulletLandingTests` | fast | none, asserted | built 2026-09-25 |
| R3 (L) | `AttackRangedJobDriver`, `CombatJobs.AttackJobFor` / `CanBreakBuildings`, every simulation site in design 47 §3e, fire at will in `HoldTarget` / `SelfDefence`, `HandleOrderAttack` sight-or-reach, `Ranged.NearestTargetInSight`, the building refusal; `RangedDriverTests` incl. the two save tests | fast; **▶ playtest** with R4, P1–P3 | none, asserted | built 2026-09-25 (the unreachable-in-sight test owed, design 47 §10) |
| R4 (S) | Debug: `Spawn(cell, kind, weapon)`, `SpawnPawn.B`, *Spawn pistol* (was *sidearm*) and *Spawn pistol bandit*, the Arm-row test | fast + Hud | none | built 2026-09-25 |
| H1 (S) | Hud spill from R0, `CombatSoundTiming` / `FloatingText` for `Shot`, `SkillCatalogueTests` | Hud fast | none | built 2026-09-25 |
| P1 (M) | The pistol prop — **Battle Royale `SM_Wep_Pistol_Heavy_01`** (design 47 §4a, measured): `PlayScene` row pinned to `PolygonBattleRoyale`, `FitPistol`, the muzzle socket measured off the drawn mesh, the holster fit, `WeaponProfileBake` + regenerated table, `WeaponPropTests` / `WeaponSheathGapTests` arms | EditMode (art-dependent; ignores itself on the runner) | none | built 2026-09-25 |
| P2 (L) | Aim stance with **tracking**, recoil, the **slide**, **low ready**, computed draw (**0.45 s, inside the first aim; a shot mid-draw snaps to aimed**) / holster keyed by style, the draw/holster rows in `SheathRows` and aim/fire as `CombatRole`s, `PlaysWorkStroke`, design 47 §4b's state table as the checklist; `CombatDrawnTests` arms fed a scripted view and event stream so it does not wait for R3 | EditMode | none | built 2026-09-25 (draw and holster reuse the sword pack's cross-draw, design 47 §10) |
| P3 (L) | `ProjectileDirector` (our own `Odyssey/Tracer` bucket copying the cell-plate pattern, the flash, the cross-layer clip, **a hit ending on the drawn body, the 0.08 s afterimage**, queue `Transparent+60`), `ChipRecipe.Dust`, impact routing, blood `!= Blunt`; **the gunshot**: `SoundIds.CombatShot` / `CombatShotFar`, two `AudioSetup` rows with `normalize = false`, the 70 m split in `CombatFeedback.Handle(Shot)` (design 47 §4c-bis; clips already baked in #220); the look experiment against `FX_Gunshot_01`; `ProjectileDirectorTests`, `CellSizeHasOneOwnerTests` | EditMode | none | built 2026-09-25 |
| R5 (M) | The gate: `BanditSoakTests.TheGateWithGunmen` (Long), `TickBenchmarkTests.FiftyShootersAgainstTen` beside the machete arm, `rangedScanTicks` tuned from its number | Long green; numbers in design 47 §5 | none, asserted | after R3, R4 |
| P4 (S) | `FrameTimeTests.TheFrameWithGunfireInView` (peace / brawl / gunfight; the audio director's `VoiceStarved` / `CooldownSkipped` logged; the gunfight timed with `ProjectileDirector.Enabled = false` as the control; the pass's draw calls ≤ 2 whatever the count as the structural gate; timing under `Measurement`); a player build smoke run with a gunfight | PlayMode, alone on the machine | none | after P2, P3, R3 |
| R6 (M) | Buildings under fire: the ranged driver's `TickBuilding`, `OrderAttackBuilding` for a gun, the bandit gunman's base attack; lifts R3's refusal | fast | none | after R3; may follow the playtest |
| R7 (S) | Records: design 47's status, this table's state column, the journal, the `CLAUDE.md` row, the playtest-queue rows, the wiki republish | — | — | done 2026-09-25 at the merge with `main`, except the wiki republish, which waits for the merge so the hosted copy shows `main` |

## Lanes and merge order

The rules are the combat plan's (`docs/plans/combat.md`, "What decides the shape") and they hold
unchanged:

1. **R0 first and alone.** It edits every append-only table and re-bakes; two lanes appending a
   handle each is a save-contract conflict on every merge.
2. **No unit after R0 moves a golden.** The projectile registry hashes nothing while empty, the new
   job sits above `JobSystem.HashedAlways`, and no golden window holds a gun. A golden moving after
   R0 is a missed seam, not a re-bake.
3. **Lanes prove themselves in the fast tier; one integrator runs the Unity tiers**, one batch run
   at a time.
4. **The spine files are contended** (`JobSystem.cs`, `Pawn.cs`, `PawnRegistry.cs`,
   `Sim.Contracts/Views.cs`, `OdysseyBootstrap.cs`, `PawnFigureDirector*.cs`): a lane gets its own
   new files plus the seams R0 cut for it, and fills them without editing the spine.

Merge order: R0; then the simulation lanes {R1 → R2 → R3 → R5}, {R4}, {H1}; then the presentation
lanes {P1 → P2}, {P3}; P4 and R6 after the integration; R7 closes. **The playtest checkpoint is after
R3 + R4 + P1–P3 are integrated**, and its handover must say that a gun-holder ordered at a building
is refused until R6.

## Blocked on the owner

- **Approval of design 47**, and its §8: the exponent hit formula; the invented starting numbers
  (aim 30 ticks, 1,000 mm a tick, 26 m, the scatter constant 3); probabilistic interception with a
  5 m dead zone and a downed pawn never taking a stray; no dodge or criticals against bullets;
  point-blank fire rather than pistol-whipping; the instanced tracer bucket charged to Overlays;
  buildings under fire as R6.
- **The gunshot**: supplied and baked 2026-09-25 (`tools/audio/bake_gunshot.sh`). **Listen to the five
  takes** and confirm the Pixabay licence (`docs/reference/audio-sourcing.md`).
- **The pack experiment** before any purchase: one free pistol clip retargeted on to three Synty
  rigs at the height extremes, the sole read with `MeasureSole` (`e-10-gun-animation-packs`).
- **Whether a ghosted layer may be a right-click target** (design 47 §8).
- **Recommendations 8–11 added on review** (design 47 §8): the Battle Royale prop, aim by condition
  not skill, the animation and flight rules, the gunshot as baked.

Not this line's, found on the way: **Unity's importer is peak-normalising most of the game's mono
sounds** (`normalize: 1` in their metas; design 47 §4c-bis) — its own small PR with a listen.
