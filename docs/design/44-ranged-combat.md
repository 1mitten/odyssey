# 44 — Ranged combat: the pistol

**Status (2026-09-25): designed, nothing built.** Ground, interview, research and this document are
the whole of the work so far. The plan is `docs/plans/ranged-combat.md`, the interview
`docs/research/ranged-interview.md`, the research `a-10-ranged-combat.md`, `a-10-projectile-path.md`,
`c-3d-shot-line.md`, `d-21-projectile-rendering.md`, `d-22-procedural-aim-and-recoil.md` and
`e-10-gun-animation-packs.md`. **The next hard stop is the owner's approval of this document** — §8
lists the seven recommendations to confirm or overrule — and no gameplay code is written before it.
Branch `claude/beautiful-cannon-pldakk` (documents only).

It sits on design 33 (draft, melee, health, weapons) and changes none of its decisions: a gun is a
second kind of attack on the seams the melee line left — decide at the wind-up's start, apply at the
impact, one method a hit point is lost through — and everything melee does, it still does.

## 1. What is being built

The owner's ask (2026-09-25): *"Now we have melee - I'd like to plan out guns or projectiles … start
with a basic pistol"* — wielded like a melee weapon, by the side (bigger weapons on the back later);
a two-handed posture when ready to fire; a visible bullet whose hits connect with the body and whose
misses can be near misses; time between aiming and firing; injuries through the current seam; draw,
aim, holster and recoil animation; and, mid-turn, *"take into account height because projectile
weapons can fire from many heights - but obviously get inaccurate with distance depending on the
gun"*.

Every decision below was the owner's in the interview of 2026-09-25 unless it says otherwise.

| Topic | Decision |
|---|---|
| Fire at will | a drafted colonist with a gun shoots the nearest hostile in range and sight without an order; an ordered target takes precedence; a per-colonist toggle is a later row |
| A miss | scatters to a cell near the target, the spread growing with how bad the shot was; anything on the bullet's line — a colonist, a bandit, a wall — *can* take it |
| Skill | **Shooting**, added now: a Def, a `SkillIndex` entry, save format 9 → 10, one golden re-bake, a fifth live row in the pane |
| Who has guns | colonists (from the ground and the debug Arm row) and a debug *Spawn pistol bandit*; ordinary raids stay melee |
| The flight | **real**: the fire tick rolls, the impact tick is fire + distance / speed, the impact re-checks who is on the line; a target can step out of a long shot; drawn tick-exact so a hit lands on the body the numbers say |
| Animation | **both**: procedural draw, aim, recoil and holster now; a clip-driven path takes over when a pack's rows are filled |
| The look | **ballistic**: muzzle flash, a short bright tracer, dust or blood at the impact; the owner sources a gunshot sound |
| Height | **shoot between layers now**: line of sight is three-dimensional from the first commit; a slab, a roof, a closed door, a wall or rock in the line blocks; distance is Euclidean in metres over 2.5 × 2.5 × 3 m cells, so height adds to the range and to the fall-off, which is per gun |
| Height bonus | **none**: the advantage of height is the clear line and the reach; the factor slot stays at 1 |
| Readings taken, not corrected | *"two guns on … around the handle"* = both hands on the gun, around the grip; *"injuries … in latest seam"* = hit points through `CombatSystem.ApplySwing`, no body parts, no bleeding |
| Defaults stated, not objected to | no ammunition; aim before every shot, then a cooldown; no partial cover value; a shooter beside an enemy fires point-blank (the reference melees with the gun — a later unit, §6); the pistol holsters at the hip and long guns on the back is a seam; a document of its own rather than a section of 33 |

## 2. The model

### 2a. The hit

`hit‰ = pow(shootingPerCell(level), distanceCells) × accuracyByDistance(distanceMm) / 1000 × coverSlot / 1000`, floored at `hitFloorPerMille`.

- **`shootingPerCell`** is the shooter's chance not to miss **per 2.5 m cell**, by Shooting level:
  0 → 747 ‰, 10 → 903 ‰, 20 → 951 ‰, linear between and flat past the ends (`CombatDef.Evaluate`'s
  arithmetic). The reference's stat is 89 % per metre at level 0 and about 98 % at 20
  (`a-10-ranged-combat` findings 2 and 5; the 98 is recollected), raised to the power 2.5 for our
  cell; **the middle point is ours**.
- **`pow`** is an integer loop over whole cells with the fractional cell interpolated linearly:
  `a^k × (1000 − f + f × a / 1000) / 1000`, `k = distanceMm / 2500`, `f` the remainder as per-mille
  of a cell. No float anywhere in it.
- **`accuracyByDistance`** is the gun's own: for the pistol 3,000 mm → 800, 12,000 → 700,
  25,000 → 400, 40,000 → 300 ‰ — the reference autopistol's four bands (touch, short, medium, long),
  **kept in metres** — linear between, flat past the ends. This is where *"inaccurate with distance
  depending on the gun"* lives: a rifle carries a different table.
- **`distanceMm`** is the integer square root of `(Δx × 2500)² + (Δz × 2500)² + (Δy × 3000)²`
  (`RangedGeometry`, a Newton root; the simulation never calls `Math.Sqrt`). **Height adds to the
  distance and therefore to the fall-off, and nothing else.**
- **`coverSlot`** is 1000: no partial cover yet. A wall blocks the line (§2b) or it does not.
- **Floor** 20 ‰ (the reference's 2 %).

What it gives, standing target, clear line:

| Distance | Level 0 | Level 10 | Level 20 |
|---|---|---|---|
| 1 cell (2.5 m) | 60 % | 72 % | 76 % |
| 5 cells (12.5 m) | 16 % | 41 % | 54 % |
| 10 cells (25 m) | 2 % | 14 % | 24 % |

The pistol is a close weapon at any skill, and the skill turns the middle distance from hopeless
into worthwhile — the reference's own shape (`a-10-ranged-combat` finding 16).

**Rejected: the melee-shaped flat curve** (0 → 550, 10 → 850, 20 → 950 ‰) times the band accuracy.
Cheaper, but a level-20 pistol at ten cells is then barely better than a level 0, and the skill
collapses into a damage-per-second dial. The exponent is what makes Shooting *buy reach*, which is
the reference's stated intent for the stat.

**No dodge roll against a bullet** — stepping out during the flight is the dodge (§2c). **A pawn
mid-aim does not dodge a blow** (the reference's rule): `MeleeRules.Resolve` returns dodge 0 for a
defender whose driver is in `Aim`. **No critical for a bullet**: the critical is authored for a blow
(a knockback three times in four for blunt, a slice sound for sharp) and a headshot is a body-parts
question — a recorded seam (§6).

### 2b. Line of sight, in three dimensions

`LineOfSight.Clear(ctx, fromCell, toCell)` and `LineOfSight.Walk(ctx, from, to, scratch)` — a
static class beside `Melee`, new. `Walk` returns, in order, every cell a straight segment between
the two cell centres passes through; `Clear` is `Walk` with the blocking tests and an early out
(`c-3d-shot-line`).

- **An integer supercover** (Amanatides–Woo in exact rationals): cell coordinates doubled so a
  centre is odd and a face even; one "next crossing" numerator per axis over that axis's extent;
  step the axis whose fraction is least, compared by cross-multiplication in `long`; no division, no
  float. **Anisotropy does not change which cells a centre-to-centre segment crosses** — an axis
  scaling maps straight lines to straight lines and faces to faces — so the walk is in index space
  and the metres enter only the distance.
- **Symmetric by construction**: the set of cells a segment touches does not depend on which end
  you start from, and the corner rule below examines the same cells either way.
  `LineOfSightTests` asserts `Clear(a, b) == Clear(b, a)` as a property over random pairs on the
  played map, and mirror invariance beside it (mirror the board in x: the answers mirror).
- **Both endpoints at the cell centre, mid-height** — one rule for shooter and target, standing or
  lying, so a same-layer shot is a horizontal line that can never graze a slab, and neither end
  needs a metre.
- **What blocks, on entering cell `n`**: `IsSolidTerrain(n)`; `IsBlockedByEdifice(n)` — a wall, a
  pillar, a closed door — unless the nav grid marks the cell `Door | DoorOpen`, so an open door
  passes; **the two end cells are never tested** (a pawn in a doorway is hittable). **Crossing from
  layer `y` to `y + 1` inside column `(x, z)` is blocked when `Floor[(x, z, y + 1)] != 0`** — the
  slab of the upper cell is the roof of the lower, which is literally how `CellGrid` stores it; one
  read is the whole slab-and-roof rule, and it applies when the crossing is the last step to the
  target too.
- **At an exact edge or vertex tie the shot passes if either monotone route round the corner is
  open** — Cataclysm-DDA's shipped `map::sees` rule — each route being its intermediate cell's entry
  test plus its own slab test. The strict rule, every touched cell open, **blocks a colonist firing
  down off the lip of her own terrace**, which is our commonest case: the centre-to-centre line
  passes exactly through the edge shared by her cell, the air above the target, the target and the
  rock under her feet. Two wall corners touching diagonally still block, because both routes are
  shut; a terrace two cells wide still blocks a shot at its foot, because the line dips into the
  rock before it clears the rim.
- **Trees block nothing** (they block no path). A canopy, a window flag on the edifice Def and a
  cover value are seams (§6).
- **No cache.** A 25 m shot is about fourteen reads. R5 counts walks per tick beside the melee
  control; the day it reads in the hundreds, the symmetric canonical key makes a per-tick memo a
  ten-line change.

### 2c. The bullet

`Projectiles` — a simulation registry, new, modelled on `Skyfallers`: the flight is simulated; only
the drawing is not.

`Entry { Shooter, Target (0 = none), Weapon, StartCell, EndCell, FireTick, ImpactTick, Aimed, DamageMilli }`

- **`ImpactTick = FireTick + max(1, ceil(distanceMm / speedMmPerTick))`**, decided at launch, so a
  save mid-flight lands on the same tick. Not `ITickable`: `CombatSystem.Tick` lands the bullets
  due at the top of its pass — Pawns phase, order 25, after the jobs and before movement, the same
  reason a swing lands there.
- **The fire tick** (`RangedRules.Resolve`, the `IMeleeRules` shape, settable on `PawnContext`;
  decides, never applies): the distance; the hit roll on `RangedHit ^ shooter`; the damage on
  `RangedDamage ^ shooter` (`MeleeRules.DamageMilli`'s ±20 %) **for hit and miss alike**, because a
  stray bullet still carries its weight; on a miss the scatter cell on `RangedScatter ^ shooter`.
  `EndCell` is the target's cell on a hit and the scatter cell on a miss. `CombatEventKind.Shot` is
  reported — attacker, target, **cell = the end cell** so the flash's direction is known, amount =
  the flight in ticks, weapon — and `PawnGesture.Fire` goes on the serial.
- **The scatter** is a cell drawn uniformly from the `(2r + 1)²` box round the target's cell, the
  centre excluded, **in the target's own layer**, clamped to the board;
  `r = min(3, 1 + (1000 − hit‰) × 3 / 1000)` cells, so a 76 % shooter's miss lands within one cell
  and a 2 % one within three. A cell inside rock is fine: the bullet stops where the walk stops. The
  reference's radius "grows with how bad the shot was"; **the constant 3 is INVENTED**. A sphere was
  rejected — it puts miss cells in the air or under a floor — and seen from an oblique shooter the
  disc is foreshortened along the line of fire, which is what a shot from above does.
- **The impact tick** (`CombatSystem.LandBullet`): `Walk(Start, End)`, then the cells in order, and
  **the first of these takes the bullet**:
  1. **a blocker** — rock, wall, closed door, slab; a door may have closed mid-flight —
     `StrikeBuilding` at the Bullet multiplier for a building with hit points, else `Miss` reported
     at that cell for the chips;
  2. **the intended target** standing on any crossed cell (it walked *into* the line), or lying on
     the end cell under a `ToTheDeath` order — a certain hit;
  3. **a bystander** — each standing pawn on a crossed cell other than the shooter rolls
     `interceptPerMille(species) × deadZoneRamp(distance from shooter)` on
     `RangedIntercept ^ shooter ^ cell`, and the first success is hit. The dead zone is 0 at 5 m from
     the shooter rising linearly to full at 12 m (the reference's 5 and 12 tiles, kept in metres), so
     a colonist shoots over the shoulder of the one in front of her. **A downed pawn never takes a
     stray bullet** — the one rule that keeps design 33 §3's invariant, *an unordered fight ends in
     downs, never deaths*, true under gunfire; a `ToTheDeath` order still finishes a downed bandit
     at the end cell;
  4. **the ground** at the end cell — `Miss` reported with the intended target and the end cell, so
     the floater and the dust land where the bullet did, and `SwingResolved(target, shooter, Miss)`
     raised so a shot at a colonist counts as an attack whether or not it lands (design 33 §12's
     parity).
- **Rejected:** *"the first pawn on the line always takes it"* — the owner's phrase read literally.
  It makes a second rank of shooters impossible and every doorway a killing box for one's own side;
  the owner's word was *can*. **Recorded, not taken:** the reference's 50 % coin on whether a stray
  may hit anyone at all — one `CombatDef` field if strays prove too deadly in play.
- **Damage** goes through `CombatSystem.ApplySwing` — the one method a hit point is lost through,
  every hook — with `attacker` becoming `Pawn?`: a shooter killed in flight still lands her bullet
  (no experience, no `React`). Experience goes to Shooting when the armament is ranged, else Melee
  (one line). Friendly fire needs nothing new: `FriendlyFireListener.SwingResolved` gives
  `Thought_AttackedByColonist` to a colonist hit or missed by a colonist exactly as for a blow. A
  struck colonist retaliates through `React` — a charge on foot, or a return of fire if she holds a
  gun; a hog rolls its revenge. Blood reads `damageKind != Blunt`, so a bullet bleeds as sharp.
  `BuildingTargets` prices Bullet at 1,000 ‰ (today `Sharp ? … : blunt` would price it as blunt).
- **Saved** in its own keyed section, `odyssey.projectiles`, skipped when absent — no format number
  of its own. **Hashed only while non-empty**, the corpse registry's rule, so the registry's
  existence moves no golden; the R0 re-bake is the skill's alone.
- **Published** as `ProjectileView { Shooter, Start, End, FireTick, ImpactTick, Weapon }` through
  `SnapshotWriter.AddProjectile` → `WorldSnapshot.Projectiles`, the `Falling` pattern. **The hit, the
  damage and the end cell are the simulation's; the muzzle, the streak and the flash are
  presentation's.**

### 2d. The driver, and fire at will

`AttackRangedJobDriver` (`Job_AttackRanged`, handle 23) is the melee driver's shape with the
range-and-sight test where the reach test was. Toils `Approach = 0` and `Aim = 1`; `ToilProgress`
counts the aim in milliwork (`Rates.Scale` a tick, so a slow colonist aims slowly). Everything it
decides with is saved — the toil and its progress, `Job.TargetCell`, `Job.WorkTicks`,
`Job.DestCell` (`ToTheDeath` / `Joining` / −1, the melee constants), and on the pawn `CombatTarget`,
`NextSwingTick`, `Destination`, `MoveProgress` — and it never reads a path; `LandTheStep` is copied
verbatim (design 33 §21c). Nothing new is held on the pawn: the bullet in flight lives in
`Projectiles`, not on either pawn.

- **In `Aim`**: progress; `Job.TargetCell = target.Cell`; **the aim breaks with no cooldown spent**
  if the target leaves range or sight — back to `Approach`; the shot never happened. The shot itself
  is fired by `CombatSystem.FireOrLose` on the tick the aim completes — lost if the shooter is
  stunned, down or dead (design 33 §5j's rule) — which puts the driver back to `Approach`. Symmetric
  with `LandOrLose`.
- **In `Approach`**: in range and sight at a step boundary, on a cell no fighter holds → stop (path
  cleared, `Destination = −1`) and, when `NextSwingTick` allows, `StartAim`: `NextSwingTick = tick +
  cooldownTicks` (the cooldown counts from the aim's start and lives on the pawn, as the swing clock
  does), the draft's quiet clock restarted (a shot is activity), `ToilIndex = Aim`. **No minimum
  range**: an adjacent enemy is fired at point-blank. The **hold** (drafted, unforced, not joining)
  never walks: out of sight, she holds again. A chase walks toward the target's cell with the melee
  chase's cadence (`chaseRepathTicks`, at a boundary, when the target moved) and **stops at the
  first boundary where the line opens** — no side is chosen; a shooter's place is wherever the line
  first opens, and because a shooter counts in `Melee.IsInAnAttack` the melee fighters treat her
  cell as held and never stop on it. Unreachable *and* out of sight → `Failed` (a target on a roof
  she can neither see nor reach). Unordered attacks re-choose after `rechooseTicks`, as melee.
- **`WorkFocus`** is the target's cell while aiming or standing engaged between shots
  (`Destination < 0`), so the figure faces and holds its stance through the cooldown; −1 while
  walking.
- **Fire at will.** One owner of "which attack job": `CombatJobs.AttackJobFor(pawn, ctx)` returns
  `AttackRanged` when `ArmamentOf(pawn).Attack.ranged != null`, else `AttackMelee`; `AttackJob.Fill`,
  `HostilityResponses.Fill`, `DraftedThinkNode`, `HandleOrderAttack` and the knockback re-issue all
  call it. `Ranged.NearestTargetInSight(ctx, me, armament)` is one pass over the pawns —
  `Melee.IsThreatTo` for a colonist, standing colonists for a bandit — within `rangeMm²`, nearest by
  squared millimetres, ties to the lower id, **the line walked only for a candidate nearer than the
  best so far** (the `DangerTo` bound), so the walks are bounded by the hostiles in range.
  `Melee.HoldTarget` for a gun-holder: (1) a threat in reach, unchanged, fired at point-blank; (2)
  the nearest threat in range and sight — she shoots from where she stands; (3) another colonist's
  fight nearby (`Joining`, unchanged — with a gun she walks only until the line opens). The reach
  scan is every tick as now; the sight scan on the `rangedScanTicks` cadence, phase-spread by id
  like the healing. **An ordered target takes precedence for free**: an order is a forced job and
  the hold thinks only between jobs. `SelfDefence`, Defend and the bandit hunt pick the job by
  armament, so a pistol bandit closes to range and shoots; Flee is unchanged (danger is who
  threatens her, not what she holds).
- **Buildings.** In R3 a gun-holder is refused a building attack: `CombatJobs.CanBreakBuildings`
  is false for a ranged armament, so `OrderAttack` on a door returns `NotPermitted` — the handover
  must say so, because the pane shows nothing happening — and a pistol bandit with nobody to reach
  falls through to theft rather than pistol-whipping a wall. **R6** gives the driver a
  `TickBuilding` — stand in range with a line to the struck cell, bullet to the anchor,
  `StrikeBuilding` at impact — and lifts the refusal.

## 3. Contracts

### 3a. Handles, claimed once (R0)

Every table below is append-only and a save contract, so all of them are extended in one commit,
the combat line's rule:

| Table | Append | Where |
|---|---|---|
| `JobHandle` / `JobIndex` / `Jobs.xml` / `BuildDrivers` | 23 `AttackRanged` (`Job_AttackRanged`); above `JobSystem.HashedAlways`, so no golden moves until it runs | `Sim.Contracts/Catalogue.cs`, `PawnContent.cs`, `PawnRegistry.cs` |
| `ItemHandle` | 11 `Pistol` (`Item_Pistol`, category Weapons, stack 1); the label is the registry's, `ui.item.pistol` = **Sidearm** — code says `Pistol`, every screen says what the CSV says | `Catalogue.cs`, `Items.xml`, `Hud/ItemLabels.cs` |
| `SkillIndex` | 6 `Shooting` (`Skill_Shooting`); `SkillIndex.Names` gains `"shooting"`; `CombatContractTests` 6 → 7 | `PawnContent.cs`, `Skills.xml` |
| `AttackStyle` / `DamageKind` | `Pistol` = 4 / `Bullet` = 2 | `CombatDef.cs` |
| `CombatEventKind` / `PawnGesture` | `Shot` = 13 / `Fire` = 5 | `Sim.Contracts/Views.cs` |
| `PawnPurpose` | `RangedHit`, `RangedDamage`, `RangedScatter`, `RangedIntercept` — the next four SHA-256 round constants after `Knockback`; grep before claiming | `PawnContent.cs` |
| `GridSize` | `CellSizeXZMm = 2500`, `CellSizeYMm = 3000` — the first simulation-side owner of ADR 0002's numbers; `CellSizeHasOneOwnerTests` holds `CellMetrics` to them (the `HopPriceHasOneOwnerTests` pattern) | `Sim.Contracts` |
| Save format | **9 → 10.** The skill arrays are length-prefixed and the loader clamps, so a seventh skill needs no bump for layout; the bump guards one thing — `if (FormatVersion < 10 && pawn.IsPerson) RollStartingSkills()` on load, so a colonist from an older file is dealt her Shooting level once. `RollStartingSkills` draws in index order and skips a skill already holding experience, so the first six deals are unchanged and the re-deal is idempotent. **Written into `SaveFormat`'s remark so nobody tidies the guard away and re-rolls on every load** | `SaveFormat.cs`, `PawnRegistry.Load` |
| Save section | `odyssey.projectiles`, keyed and skipped when absent; hashed only while non-empty | `ColonyWorld.SaveComponents`, `ColonyComposition` |
| Registry CSVs | `ui.status.shooting`, `ui.debug.spawnpistol` ("Spawn sidearm"), `ui.debug.spawngunman` ("Spawn pistol bandit"); icon-map gap rows; wiki and `Registry.g.cs` rebuilt in the same commit. `ui.item.pistol` and `ui.skill.shooting` already exist | `docs/design/icon-keys.csv`, `icon-map.csv` |
| Presentation catalogue | `ModuleIds.ItemPistol` appended to `ItemModules` (order = `ItemIndex`; `ModuleIdTests` holds it to `ItemIndex.Count`); four **empty** clip rows `CombatPistolAim`, `CombatPistolFire`, `CombatPistolDraw`, `CombatPistolHolster` | `ModuleCatalogue.cs`, `PlayScene.cs` |

### 3b. Defs and every number

`AttackDef` gains one nullable block, `ranged : RangedDef?`, so `Armament`, `WeaponRules.ArmamentOf`,
`CanEquip`, `DebugArm` and every `weapon != null` test are untouched. For a gun **`windupTicks` is
the aim and `cooldownTicks` the cooldown** — the melee fields under the melee names, so one wind-up
clock serves both.

| Def | Field | Value | Source |
|---|---|---|---|
| `ItemDef` `Item_Pistol` | `damage` | 10 (±20 % as melee) | ref autopistol |
| | `windupTicks` (the aim) | **30** (0.5 s) | INVENTED; ref 18 — raised so the aim is *seen*; the owner tunes |
| | `cooldownTicks` | 60 (1 s) | ref |
| | `damageKind` / `style` / `stunPerMille` / `experiencePerSwing` | Bullet / Pistol / 0 / 50,000 per shot, hit or miss | ours (ref: 240 XP a shot, hit or miss) |
| `RangedDef` | `rangeMm` | 26,000 (10.4 cells) | ref 25.9 cells, kept in metres |
| | `speedMmPerTick` | **1,000** (60 m/s; a 25 m shot flies 25 ticks, 0.42 s) | INVENTED; 1,500 was considered — slower is more visible and more steppable |
| | `accuracyByDistance` (mm → ‰) | 3,000 → 800; 12,000 → 700; 25,000 → 400; 40,000 → 300 | ref bands, in metres |
| `CombatDef` | `shootingPerCell` (level → ‰ per cell) | 0 → **747**; 10 → **903**; 20 → **951** | ref 89 %/m and 98 %/m to the power 2.5; the middle point ours |
| | `hitFloorPerMille` | 20 | ref 2 % |
| | `interceptDeadZoneMm` / `interceptFullMm` | 5,000 / 12,000 | ref 5 and 12 tiles, in metres |
| | `scatterMaxCells` / `scatterPerMissPerMille` | 3 / 3 (the `× 3 / 1000` in §2c) | INVENTED |
| | `rangedScanTicks` | 15 | INVENTED; R5 measures it |
| | cover factor slot | 1000 | owner: no partial cover |
| `SpeciesDef` | `interceptPerMille` | person 400, hog 500, rat 40 | ref 40 % × body size, clamped 4–80 % |
| `SkillDef` `Skill_Shooting` | `ParentName="SkillBase"`, label from `ui.skill.shooting` | — | — |

Every value marked INVENTED is in `Combat.xml` and `Items.xml` with that word beside it, the
convention every Def file follows. `PawnContentDefTests.ContentFingerprint` moves **once, in R0**,
with its dated paragraph.

### 3c. State on the pawn

Nothing new. The cooldown is `NextSwingTick`; the target `CombatTarget`; the aim's progress the
job's `ToilProgress`; the bullet is in `Projectiles`. **`PawnFlags` is a full byte** — no aiming
flag; the stance is derived from `Working` + `JobDef` (§4b). Hash bits 22–23 stay free.

### 3d. What presentation reads

`WorldSnapshot.Projectiles`; the `Shot` event and the `Fire` gesture; `Working`, `WorkCell` and
`JobDef == JobHandle.AttackRanged`; the weapon aspect with `WeaponStyles[def] == AttackStyle.Pistol`
(the composition root already fills the map from the content); `PawnFlags.Drawn` (a ranged attack
draws the weapon at any distance — `WeaponDraw.TargetNear` changes; `Reach = 2` stays melee's);
`odyssey.pawn.order.target` / `order.cell`, extended to the new job so the lock-on ring and the
order line mean an order for a shooter too.

### 3e. Simulation seams — every site hard-coded to melee, one line each

`CombatSystem.Tick:69` (the `InAim` branch → `FireOrLose`; `LandDue` before the loop) · `:75-77`
(the swing clock survives an aim) · `Apply.cs:29-30` (skill by armament; `Pawn?`) · `Apply.cs:242`
`FightingBeside` (a ranged job with its target in sight) · `Knockback.cs:141, 159, 171` (re-issue by
`AttackJobFor`) · `Melee.IsAttacking:47`, `IsInAnAttack:237`, `ColonistUnderAttackBy`,
`HoldTarget:116-153` · `CombatThinkNodes.AttackJob.Fill:33-59`, `SelfDefence:108` ·
`HostilityResponse.cs:91, 116, 132` · `WeaponDraw.TargetNear:57-63` · `MeleeRules.cs:71` (Bullet
never stuns; dodge 0 for a defender mid-aim) · `BuildingTargets.cs:143` (Bullet ×1) · `Draft.cs:42-51`
· `JobSystem.Attack.cs:40-63` (accepted when in range and sight **or** reachable; the building
refusal) · `Pawn.UrgencyPerMille:678` (a shooter runs) · `PawnRegistry.OrderCellOf:650`,
`OrderTargetOf:670` · `PawnRegistry.Spawn(cell, kind, weaponDef)` + `HandleSpawnPawn` reading
`intent.B` as `weaponDef + 1` (0 = the kind's own table) — the debug pistol bandit with no new kind:
`PawnOutfit.Bandit` is keyed on `Person | Hostile`, so it keeps its helmet and vest · `PawnContext`
(`Projectiles`, `RangedRules`) · `ColonyComposition` (build, hash, contribute) · `SaveFormat` and
`PawnRegistry.Load` (the guard) · the debug Arm row deals the pistol already — every item with a
weapon block is in its lottery — a test, not a change.

### 3f. Presentation seams

`PawnFigureDirector.Combat.cs` `PlaysWorkStroke` (excludes the new job) · `CombatPose.SwingRole` /
`StyleFor` (`Pistol` → the aim and fire roles) · `CombatFeedback.BloodSidesOf` (`!= Blunt`) and
`Handle(Shot)` (the sound from the shooter's feet) · `SoundIds.ForCombat(Shot)` · `CombatSoundTiming`
(a shot schedules no cue; it plays on its frame) · `CombatOrders.IsWeapon(Pistol)` · `JobLabels` row
23 · `SkillCatalogue` live row · `DebugDirector` two rows · `WeaponProfileBake.Rows` · new files
`PawnFigureDirector.Aim.cs` (or a partial beside `Combat.cs`) and `Presentation/World/ProjectileDirector.cs`.

## 4. Presentation

### 4a. The prop (P1)

`SM_Wep_Pistol_01` on the pistol's own catalogue row, `prefabUnder` pinned to the Sci-Fi City
folder (Battle Royale sorts first on a shared name), ground scale as the four weapons; the same row
is the ground item and the held prop. **`FitPistol`**, chosen when `WeaponStyles[def] == Pistol`: the
barrel is the long bounds axis pointed where the fist points, the grip is the second axis turned
down the forearm's up, the palm at `WeaponGripFraction` along the **grip** rather than the barrel.
`FitWeapon`'s blade rule would seat the barrel along the forearm and the palm on the slide.
`FitWeaponBothWays` and the `SheathSurface` fit take it as they take a machete, with the profile
regenerated by `WeaponProfileBake` (`WeaponProfileTests` fails until it is): a short long axis hangs
grip-at-hip, barrel down the thigh — a holster at the left hip, one rule; a right-hip holster is a
seam. Arms in `WeaponPropTests` (barrel within 10° of the forearm, grip within 20° of its down, palm
within 3 cm) and `WeaponSheathGapTests` (four bodies, 0.8–3.0 cm).

### 4b. Stance, recoil, draw and holster (P2)

Procedural, in the pose pass after the graph (the `ApplyCombatPose` slot in `ApplyWorkPose`'s
else-if order), held while `pawn.Working && pawn.JobDef == JobHandle.AttackRanged`
(`d-22-procedural-aim-and-recoil`). A pose may add to a bone the graph owns; the prop is placed
absolutely each frame (`Tools.cs`'s rule).

- **Aim.** The target point is `CellMetrics.Centre(WorkCell)` at chest height — **three-dimensional**,
  so a shooter above pitches down and one below pitches up. Yaw split 0.3 / 0.4 / 0.3 over spine,
  chest and upper chest with a total clamp of **±60° yaw and ±45° pitch**; the head to ±80° through
  the gaze pass; beyond the yaw clamp the pawn turns, which it already does to `WorkCell`. **The gun
  is the truth**: placed on the aim line in front of the chest at **0.85 × that rig's own arm
  length** — measured off the drawn mesh as the sole and crown are, never from a bone name (P11) —
  barrel along the line; the right arm solved to the grip with `TwoBoneIk.Reach`, the left cupping
  under it with `Grasp.One` on a `Hold.Bar` (the axe's off-hand code). An **isosceles** wedge, both
  arms near-straight, because from 48° above it is a symmetric arrow at the target; the Weaver's
  bent support elbow collapses into the torso from above. The aim weight eases in over the aim time
  (minimum 0.12 s) so the posture is *seen* to settle before the shot, and out on the frame `Working`
  drops.
- **Recoil.** On `PawnGesture.Fire` — or the `Shot` event, whichever the frame sees first, the
  `BeginStrike` / `TimeSwing` rule — a **critically damped spring derived from one duration,
  250 ms**: the gun +6° pitch and 4 cm back, with 12 % of the pitch leaked into both shoulders as an
  additive `CombatShape` (the flinch precedent, `CombatShape.Plus`). At distance the shoulders rising
  and falling is what reads; that is the number to tune first, not the muzzle. All INVENTED
  starting values.
- **Draw and holster.** `WeaponSheath.Step` and `PoseSheath` drive the pistol as a sword — the
  two-second hold, the draft's instant put-away — but **the sheath rows are chosen by style**: the
  pistol rows are empty, so a computed draw plays: right hand to the holster over 0.25 s, the prop
  re-parented at the grasp instant (the sword draw's "hand on hilt" moment), then the arc up into
  the aim over 0.35 s — **0.6 s** in all; holster the reverse at **0.8 s**, slower because nothing
  waits on it. The gun changes parent at the grasp instant and at no other time. Without this
  keying a checkout with the sword pack plays `A_Draw_Sword` on a pistol.
- **The clip seam** (the owner's "both"). `ModuleIds.CombatPistolAim / Fire / Draw / Holster` are
  declared in `ModuleIds.CombatRows` and `AddCombatRows` with no clips; `CombatClip(role)` already
  returns null for an empty row, so the computed path takes over exactly as `PoseSheath` snaps
  without the sword clips today. When a pack arrives, filling the rows is a `PlayScene` edit and a
  catalogue rebuild (then `CharacterSwatches.Classify`, `docs/lessons.md`). **Before any purchase**,
  the cheapest experiment from `e-10`: one free pistol clip retargeted on to three Synty rigs at the
  height extremes, the sole read with `MeasureSole` — P11 says a third-party Humanoid clip may not
  stand on the floor. Third-party FBX goes under a gitignored folder on `Assets/Synty`'s terms.
- **Far form** (past the 64-figure ceiling): no weapon and no stance, as today; the tracer still
  says who is shooting.

### 4c. The tracer, the flash, the impact and the sound (P3)

`Presentation/World/ProjectileDirector.cs`, new, called in `LateUpdate`'s Overlays block beside the
combat marks and the floaters, and **charged to `FrameSection.Overlays`** — a new section would add
a column to every perf-trace header; this sentence is why nobody reads Overlays' number without
knowing what is in it (`d-21-projectile-rendering`).

- **Tracer.** Per `ProjectileView`, `t = FallArc.Progress(tick, alpha, FireTick, ImpactTick)`
  (reused as is); start = the muzzle of the figure's prop if it has one (`WeaponOf`), else the start
  cell at chest height; end = the end cell at chest height. A streak from `p(t − tail)` to `p(t)`,
  the tail 0.15 of the flight and never under 2 m, width 0.08 m with a **2 px floor** in the vertex
  shader (a thinner additive line vanishes under anti-aliasing at the far zoom), emissive additive,
  `ZTest` on, `ZWrite` off. **One matrix per bullet into one bucket, one instanced draw per frame**
  through the `GatherCellPlate` machinery — already written, already counted in `DrawCalls`, split
  at `MaxInstancesPerCall`; no allocation after the first frame. **Alternative, ranked first by
  `d-21` for the general case:** the rain pass's own pattern — `RenderPrimitives` from a segment
  buffer with the lerp in the vertex shader; the same one call, taken only if the rain shader's
  buffer layout can carry a second segment type, else the bucket is the same picture on code that
  exists. **Explicit render-queue values** so water, tracers and rain are never compared by distance
  (P17): a batch sorts as one object by one bounds, and "let URP sort it" is the tie waiting to
  happen.
- **Cross-layer rule.** A bullet is drawn when **either** end's layer is inside the drawn band, and
  the streak is **clipped to the band's vertical extent**: a bullet from a hidden storey enters at
  the band's ceiling plane; one going into an undrawn cellar leaves at its floor plane. A ghosted
  layer counts as drawn; a storey hidden by walls-down (`SliceSettings.HidesStackedOn`) does not.
  The flash and the impact play only when their own cell's layer is drawn (the floaters' rule);
  blood on every layer and sound on every layer, as today. Cataclysm's one shipped bug here was a
  targeting view driven by the shooter's level rather than the target's (`c-3d-shot-line`
  finding 14): the impact is placed by the end cell's layer.
- **Muzzle flash.** On the `Shot` event, a camera-facing quad at the barrel end (the chest centre
  without a figure) for **4 ticks**, in the same bucket with a second material; a `FlashOf(pawn,
  until)` list on the director, bounded by figures. **No light in the first cut** — the one term that
  scales with pixels at 4K; measure ten lit against ten unlit before adding it.
- **Impact.** On a `Hit` or `Miss` whose weapon is a gun: ground → `ChipDirector.Throw` with a new
  `ChipRecipe.Dust` (many, tiny, short, earth-coloured); a body → the existing blood spurt, its
  direction from the shooter's feet, which `CombatFeedback` already computes for any distance; a
  wall → the stone or timber chips the building-hit path already picks. **The near miss is three
  things and no new pass**: the streak visibly continues *past* the target to the scatter cell, the
  dust lands there rather than on the body, and the sound at that cell is the "bullet by" rather than
  the thud. A hit is the streak *stopping* on the body plus blood plus the hit sound; hit and miss
  diverge on the tick the simulation decided them. **No shell**: under a pixel at this camera.
- **Sound.** `SoundIds.CombatShot = "combat.shot"` and an `AudioSetup` row, played on the `Shot`
  frame from the shooter's feet (`Handle` special-cases `Shot`); the impact keeps the thud (body,
  building) and `CombatMiss` (ground) until the owner asks for a ricochet. **The clip is the owner's
  to source and license** (`docs/reference/audio-sourcing.md`); the row ships silent until then, as
  `CombatMiss` does today. `CombatSoundTests`' loudest-sample assertion extends to it.

### 4d. Interface

Right-click on a hostile from a gun-holder already routes through `CombatOrders.Route` →
`OrderAttack`; `HandleOrderAttack` accepts a target in sight *or* reachable. The lock-on ring and the
order line work unchanged once `OrderTargetOf` / `OrderCellOf` know the job. The skills pane gets
its fifth live row through `SkillCatalogue` (all fourteen rows are listed whatever is live, so no
layout moves). Debug: *Spawn sidearm*, *Spawn pistol bandit*; the Arm row already deals it.
**Recorded limitation**: `PawnUnderRay` picks only pawns at or above the clicked cell's layer, so a
bandit on a roof above the slice cannot be right-clicked until the slice is raised (fire at will
still engages it); admitting a ghosted layer for a *target* changes what a click on a storey above
means everywhere — the owner's call, not this unit's (§8).

## 5. Tests, goldens and measurements

Test-first; every claim seen to fail with its rule withheld; the negative control always written.

**Simulation, fast tier.**
- `LineOfSightTests` (new): open ground clear; a wall blocks; a closed door blocks and the same
  door opened passes; a diagonal past two touching wall corners blocks (control: one corner removed,
  clear — the lenient rule); **a shooter on a terrace top sees a target on the ground below**
  (`CombatFixture` with a raised column); **negative control: a slab on the layer between them
  blocks**; solid rock between two cellars blocks; **symmetry as a property** over 500 random pairs
  on the played map, and mirror invariance; `Walk` visits both cells at an exact tie; `DistanceMm2`
  on the three axes and the integer root exact on perfect squares.
- `RangedMathTests`: the per-cell curve's three points; band accuracy interpolated and flat past
  the ends; `pow`'s fractional cell; the measured hit rate over 4,000 shots at 1, 5 and 10 cells
  within ±3 % of §2a's table; the scatter never picks the target's cell, stays inside `r`, stays in
  the target's layer, and `r` is 1 at 76 % and 3 at 2 %; each roll on its own stream (the
  `EachRollIsItsOwnStream` control).
- `ProjectileTests`: impact tick = fire + ceil(d / speed), never under 1; a bullet lands on the same
  tick after a save at flight midpoint (the `ASaveTakenMidStepInReachResumesTheSame` shape); the
  section round-trips; **hashed only while non-empty** (a colony with no bullet in flight hashes as
  before — the golden assertion); a bullet outlives its shooter.
- `BulletLandingTests`: a rolled hit on a target that stepped off the line is a `Miss` at the end
  cell; a target that walked into the line is hit; a bystander on the line takes the bullet at its
  chance and the intended target does not (`SwingResolved` for the bystander;
  `Thought_AttackedByColonist` when both are colonists — control: the bystander a bandit, no
  memory); a bystander inside the 5 m dead zone is never hit (control: at 12 m, the full chance); a
  door closed mid-flight takes the bullet at ×1 exactly (against the sharp and blunt controls); **a
  downed pawn on the line is passed over** (control: standing, hit); a `ToTheDeath` order kills a
  downed bandit at the end cell; a scatter into rock reports `Miss` at the blocker; experience into
  Shooting, not Melee; a defender mid-aim does not dodge a blow (control: not aiming, the curve).
- `RangedDriverTests`: a drafted colonist with a pistol shoots the nearest hostile in range and
  sight **without an order** (controls: the hostile behind a wall, no shot; the same colonist with a
  machete, no shot); an ordered target beats a nearer unordered one; point-blank fires with no
  minimum range; an unreachable target in sight is accepted by `OrderAttack` and shot (control: out
  of sight *and* unreachable, `NotPermitted`); the hold never walks; the chase stops at the first
  boundary with a line; **the aim breaks and restarts when the target ducks behind a wall, spending
  no cooldown**; the cooldown is on the pawn and survives a re-order; a stunned shooter's aim is
  lost; **a save mid-aim resumes on the same hash** and one mid-step-in-sight walks on (§21c); a
  colonist shot by a colonist retaliates (on foot; with her own gun, a return of fire); a bandit shot
  from range turns on the shooter; a hog shot rolls revenge; a gun-holder's building order is
  refused (R3) and lifted (R6); `UrgencyPerMille` runs the approach.
- `StartingSkillsTests`: the seventh deal leaves the first six unchanged for the same seed (values
  pinned before the change); a format-9 save loads with Shooting dealt; a format-10 save with
  trained Shooting is not re-rolled.
- `DebugArmColonistsTests`: over 60 colonists the Arm row deals at least one pistol (control: the
  pistol's weapon block withheld by a replaced Def, none); `SpawnPawn` with `B = Pistol + 1` spawns a
  bandit holding a pistol whose flags are still `Person | Hostile`; `B = 0` deals from the kind's
  table as before.
- `CombatContractTests`, `PawnContentDefTests` (the fingerprint), `RegistryTests`, `Golden` — in R0.
- **Long**: `BanditSoakTests.TheGateWithGunmen` — ten days on three seeds, the raids alternating
  melee and pistol bandits, the colony armed by the Arm row on day one and drafted at each raid, the
  lockstep twin every hour, a save mid-raid with bullets in flight resumed equal a day later; the
  existing invariants plus: no bullet's impact tick in the past, every bullet's shooter existed when
  it was fired, **nobody dies in an unordered fight**.
- **Benchmark**: `TickBenchmarkTests.FiftyShootersAgainstTen` on the scale target, beside the
  fifty-with-machetes arm as the control in one run, reporting the tick, the Pawns phase and
  `LineOfSight.Walks` per tick, so the scan cadence has a number before it is called one.

**Hud, fast tier**: `SkillCatalogueTests` live rows; `JobLabels` length; `CombatOrders.IsWeapon`;
the debug rows resolve their keys; `CombatSoundTiming` — `Shot` schedules no cue; `FloatingText(Shot)`
empty; `HudFontTests` on the new literals.

**Presentation, EditMode**: the `WeaponPropTests` pistol arm; `WeaponSheathGapTests` on four bodies;
`WeaponProfileTests`; `ModuleIdTests` (twelve items); `CombatDrawnTests` — the pistol rows resolve or
fall back, `StyleFor(Pistol)` → the aim role, a `Fire` gesture starts the recoil and it decays to
rest within 0.3 s, **the aim pitches down for a target a layer below** (a bone check on a rig,
ignored without the art); `CellSizeHasOneOwnerTests`; `ProjectileDirectorTests` — N bullets are one
draw call, the cross-layer clip (a bullet from a hidden storey starts at the band's ceiling; one to
an undrawn cellar ends at its floor; a bullet with neither end drawn draws nothing).

**PlayMode**: `FrameTimeTests.TheFrameWithGunfireInView` — peace, the brawl (existing) and a
gunfight (ten pistol colonists drafted against ten bandits) in one run; the gunfight timed twice,
once with `ProjectileDirector.Enabled = false` as the control (the `InstanceCellPlates` pattern);
**the pass's draw calls ≤ 2 whatever the shooter count** is the structural gate that stays in the
tier; the timing is `Category("Measurement")`.

**Goldens**: all six move **once, in R0**, and the reason is the hash seeing more — a seventh skill,
passion and daily-gain slot per colonist, a twelfth item allow-list slot, and each colonist's dealt
Shooting level. `GoldenColonyProbe` widened to seven skills and run on both parents: the first six
levels identical on all three boards. **No unit after R0 moves a golden**: the projectile registry
hashes nothing while empty, `Job_AttackRanged` is above `HashedAlways`, and no golden window holds a
gun. A golden moving after R0 is a missed seam, not a re-bake.

## 6. Later units, named

Long guns on the back · the rifle (18 damage, 102-tick aim, 90 cooldown, 65 / 80 / 90 / 80 %,
37 m — ref, already balanced against the pistol) and the revolver (12 / 18 / 96 / 80-75-55-40) ·
partial cover and a cover value (the slot at 1), including trees, sandbags (`ui.arch.tool.sandbag`
exists) and a window flag on the edifice Def · the lean round a corner (needs a pose) · melee with
the gun when adjacent · a fire-at-will toggle per colonist · a pistol bandit *kind* with its own
weapons table, restoring `PawnContent.WeaponFor`'s one owner · hunting with a gun · ammunition
(`ui.res.ammo` exists) · darkness and weather terms on the hit (weather exists; the slot is there) ·
criticals and body parts · a height bonus (the slot at 1) · a right-hip holster · a muzzle light
gated by zoom · a ricochet sound · targeting up through a ghosted slice · the reference's 50 % stray
coin · a walk cache behind the symmetric key.

## 7. Do not undo by tidying

- **The lenient corner rule** (§2b). Making the tie strict "for safety" blocks every shot down off a
  terrace lip; `LineOfSightTests` holds the terrace case and the two-corner control together.
- **The slab test reads the upper cell's `Floor`.** That is where `CellGrid` keeps the roof of the
  lower cell; a "roof" flag on the lower cell would be a second owner.
- **Both endpoints at mid-height, in index space.** Offsetting an end by a metre re-opens
  anisotropy and symmetry at once.
- **The aim breaks with no cooldown spent.** Charging a cooldown for a shot that never happened
  makes a colonist behind a wall a free kill.
- **A downed pawn never takes a stray bullet.** The C7 invariant stands on it.
- **Hashed only while non-empty; the new job above `HashedAlways`.** That is what lets every unit
  after R0 assert the goldens unchanged.
- **The format-10 guard is the bump's whole meaning.** Removing it re-rolls Shooting on every load.
- **The sheath rows are chosen by style.** Choosing by "has a clip" plays the sword draw on a pistol.
- **`attacker` is nullable in `ApplySwing`.** A bullet outlives its shooter.
- **The render-queue values are explicit.** A batch sorts as one object by one bounds.
- **The tracer is charged to Overlays, and this document says so.**
- **No minimum range.** A gunner with an enemy on the next cell fires; she is not disarmed by
  adjacency.

## 8. Open, and recommended for the owner to confirm

Recommended, for the owner to confirm or overrule before R0:

1. The exponent hit formula (§2a), not the flat curve.
2. The invented starting numbers: aim 30 ticks (ref 18), 1,000 mm a tick, 26 m, the scatter constant
   3, the per-cell curve's middle point.
3. Interception probabilistic with a 5 m dead zone; a downed pawn never takes a stray; no 50 % stray
   coin.
4. No dodge against bullets; no dodge for a pawn mid-aim; no criticals for bullets.
5. Point-blank fire, no pistol-whip (the reference melees with the gun).
6. The tracer as one instanced bucket charged to Overlays; the rain-pattern procedural pass as the
   alternative.
7. Buildings under fire as R6, after the first playtest; until then a gun-holder ordered at a door
   is refused.

Open, not settled from the code:

- **Targeting up through a ghosted slice** (§4d).
- **The gunshot clip** and its licence; whether a ground impact wants its own sound.
- **Gun animations**: none in any pack; the computed stance ships; the pack experiment gates a
  purchase (§4b).
- **The fire-at-will cost**: bounded on paper; R5 measures it beside the melee control before the
  cadence is called a number.
- **`PawnFlags` is full**: any later "aiming" or "reloading" bit widens the flags word, which is a
  `PawnView` constructor change every hand-built view in the tests touches.
- **The debug pistol bandit's weapon deal bypasses `WeaponFor`** (design 42's hash of the pawn); an
  eventual gunman kind restores the one owner.
