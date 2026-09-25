# Ranged combat — the plan

**Designed 2026-09-25** after an interview of three rounds (`docs/research/ranged-interview.md`;
every answer is in the decision table of `docs/design/44-ranged-combat.md` §1). **Not yet approved
for code**: the next hard stop is the owner's reading of design 44, whose §8 lists seven
recommendations to confirm or overrule. This planning work is on `claude/beautiful-cannon-pldakk`,
**PR #220** (documents only). The research is `a-10-ranged-combat`, `a-10-projectile-path`, `c-3d-shot-line`,
`d-21-projectile-rendering`, `d-22-procedural-aim-and-recoil` and `e-10-gun-animation-packs`.

## Units

`| Unit | What | Checkpoint | Goldens | State |`, the combat plan's form. R is the simulation, H the
Hud, P presentation. Sizes S / M / L. Branch per unit `claude/ranged-<unit>`.

| Unit | What | Checkpoint | Goldens | State |
|---|---|---|---|---|
| R0 (M) | Contracts and content, claimed once: every handle in design 44 §3a — `Job_AttackRanged` 23, `Item_Pistol` 11, `Skill_Shooting` 6, `AttackStyle.Pistol`, `DamageKind.Bullet`, `CombatEventKind.Shot`, `PawnGesture.Fire`, four `PawnPurpose` salts, the `GridSize` millimetre constants; `RangedDef` / `AttackDef.ranged` / the `CombatDef` fields / `SpeciesDef.interceptPerMille`; `ProjectileView` + writer + store; save format 10 with the Shooting backfill; the CSV rows, wiki and label registry; `ModuleIds.ItemPistol` and the four empty clip rows; `JobLabels`, `ItemLabels`, `CombatOrders.IsWeapon`, `SkillCatalogue`; a stub driver that fails; the content fingerprint; **one golden re-bake, probe-diffed on both parents** (the first six skills identical on all three boards) | fast, Long, EditMode green; probe clean | **move once** | not started |
| R1 (M) | `LineOfSight` (3D integer supercover, symmetric, doors, slabs, the lenient corner) and `RangedGeometry`; `LineOfSightTests` incl. the terrace hit, the slab control, the symmetry and mirror properties | fast | none | not started; parallel with R0 (new files only) |
| R2 (M) | `Projectiles` (save / hash / publish), `IRangedRules` / `RangedRules`, `CombatSystem.LandBullet` and `FireOrLose`, `ApplySwing`'s `Pawn?` and skill line, `BuildingTargets` ×1, dodge 0 mid-aim; `RangedMathTests`, `ProjectileTests`, `BulletLandingTests` | fast | none, asserted | after R0, R1 |
| R3 (L) | `AttackRangedJobDriver`, `CombatJobs.AttackJobFor` / `CanBreakBuildings`, every simulation site in design 44 §3e, fire at will in `HoldTarget` / `SelfDefence`, `HandleOrderAttack` sight-or-reach, `Ranged.NearestTargetInSight`, the building refusal; `RangedDriverTests` incl. the two save tests | fast; **▶ playtest** with R4, P1–P3 | none, asserted | after R2 |
| R4 (S) | Debug: `Spawn(cell, kind, weapon)`, `SpawnPawn.B`, *Spawn sidearm* and *Spawn pistol bandit*, the Arm-row test | fast + Hud | none | after R0, own lane |
| H1 (S) | Hud spill from R0, `CombatSoundTiming` / `FloatingText` for `Shot`, `SkillCatalogueTests` | Hud fast | none | after R0, own lane |
| P1 (M) | The pistol prop: `PlayScene` row, `FitPistol`, the holster fit, `WeaponProfileBake` + regenerated table, `WeaponPropTests` / `WeaponSheathGapTests` arms | EditMode (art-dependent; ignores itself on the runner) | none | after R0, own lane |
| P2 (L) | Aim stance, recoil, computed draw / holster keyed by style, `PlaysWorkStroke`; `CombatDrawnTests` arms fed a scripted view and event stream so it does not wait for R3 | EditMode | none | after P1 |
| P3 (L) | `ProjectileDirector` (the bucket, the flash, the cross-layer clip), `ChipRecipe.Dust`, impact routing, `SoundIds.CombatShot` + row, `CombatFeedback.Handle(Shot)`; `ProjectileDirectorTests`, `CellSizeHasOneOwnerTests` | EditMode | none | after R0 (the view shape); the sound row needs the owner's clip |
| R5 (M) | The gate: `BanditSoakTests.TheGateWithGunmen` (Long), `TickBenchmarkTests.FiftyShootersAgainstTen` beside the machete arm, `rangedScanTicks` tuned from its number | Long green; numbers in design 44 §5 | none, asserted | after R3, R4 |
| P4 (S) | `FrameTimeTests.TheFrameWithGunfireInView` (peace / brawl / gunfight; the gunfight timed with `ProjectileDirector.Enabled = false` as the control; the pass's draw calls ≤ 2 whatever the count as the structural gate; timing under `Measurement`); a player build smoke run with a gunfight | PlayMode, alone on the machine | none | after P2, P3, R3 |
| R6 (M) | Buildings under fire: the ranged driver's `TickBuilding`, `OrderAttackBuilding` for a gun, the bandit gunman's base attack; lifts R3's refusal | fast | none | after R3; may follow the playtest |
| R7 (S) | Records: design 44's status, this table's state column, the journal, the `CLAUDE.md` row, the playtest-queue rows, the wiki republish | — | — | last |

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

- **Approval of design 44**, and its §8: the exponent hit formula; the invented starting numbers
  (aim 30 ticks, 1,000 mm a tick, 26 m, the scatter constant 3); probabilistic interception with a
  5 m dead zone and a downed pawn never taking a stray; no dodge or criticals against bullets;
  point-blank fire rather than pistol-whipping; the instanced tracer bucket charged to Overlays;
  buildings under fire as R6.
- **The gunshot clip** and its licence (`docs/reference/audio-sourcing.md`); the row ships silent
  until then, as `CombatMiss` does today.
- **The pack experiment** before any purchase: one free pistol clip retargeted on to three Synty
  rigs at the height extremes, the sole read with `MeasureSole` (`e-10-gun-animation-packs`).
- **Whether a ghosted layer may be a right-click target** (design 44 §8).
