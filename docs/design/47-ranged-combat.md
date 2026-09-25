# 47 — Ranged combat: the pistol

**Status (2026-09-25): designed, nothing built.** Ground, interview, research and this document are
the whole of the work so far. The plan is `docs/plans/ranged-combat.md`, the interview
`docs/research/ranged-interview.md`, the research `a-10-ranged-combat.md`, `a-10-projectile-path.md`,
`c-3d-shot-line.md`, `d-21-projectile-rendering.md`, `d-22-procedural-aim-and-recoil.md` and
`e-10-gun-animation-packs.md`. **The next hard stop is the owner's approval of this document** — §8
lists the recommendations to confirm or overrule — and no gameplay code is written before it.
Branch `claude/beautiful-cannon-pldakk`, **PR #220** (documents, the baked gunshot and its bake
script; no code).

**Renumbered 44 → 47 on 2026-09-25**: `main` took 44 for the selection highlight while this was
written, and 45 (nature) and 46 (jumping) are taken on open branches.

**Reviewed 2026-09-25** against `main` after merging it, every code claim checked (§9). The review
moved: the pistol to **POLYGON Battle Royale's `SM_Wep_Pistol_Heavy_01`** at the owner's request,
measured (§4a); the four `PawnPurpose` salts, three of which were already taken (§3a); handles 23 /
11 / 6, which three open PRs also claim (§3a); the aim's rate, which named a constant (§2d); blood,
described as already reading `!= Blunt` (§2c); the draw and holster clip rows, which live in
`SheathRows` (§4b); the tracer's reuse claims (§4c); and it **added** what the first draft left open
about animation and flight — the low-ready carry, a draw that finishes inside the first aim, aim
tracking, a hit that ends on the drawn body, a minimum visible streak, the states a gun cannot be
fired from (§4b, §4c, §2e) — and **the gunshot sound**, supplied, baked and levelled (§4c-bis).

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
| Skill | **Shooting**, added now: a Def, a `SkillIndex` entry, save format 9 → 10, one golden re-bake, a live row in the pane (the sixth; the first draft said fifth) |
| Who has guns | colonists (from the ground and the debug Arm row) and a debug *Spawn pistol bandit*; ordinary raids stay melee |
| The flight | **real**: the fire tick rolls, the impact tick is fire + distance / speed, the impact re-checks who is on the line; a target can step out of a long shot; drawn tick-exact so a hit lands on the body the numbers say |
| Animation | **both**: procedural draw, aim, recoil and holster now; a clip-driven path takes over when a pack's rows are filled |
| The look | **ballistic**: muzzle flash, a short bright tracer, dust or blood at the impact; the owner sources a gunshot sound — **supplied 2026-09-25** (§4c-bis) |
| The prop | **POLYGON Battle Royale's pistol** (owner, 2026-09-25, after the interview): `SM_Wep_Pistol_Heavy_01` (§4a) |
| The shot's sound | the owner's recording, *"blend this into the environment and process it for every gun shot (and give it some variance)"* (§4c-bis) |
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
  save mid-flight lands on the same tick. **Unlike `Skyfallers`, deliberately not `ITickable`**
  (`Skyfallers` is one, `Skyfallers.cs:26`): `CombatSystem.Tick` lands the bullets
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
  (no experience, no `React`). **That is three signatures, not one**: `ApplySwing`'s parameter,
  `SwingReport.Attacker` (`CombatHooks.cs:45`, non-null today, so every `ISwingListener` must
  handle null) and `StrikeBuilding`'s attacker (`CombatSystem.Buildings.cs:29`);
  `DamageReport.Attacker` is already `Pawn?`. Experience goes to Shooting when the armament is
  ranged, else Melee (`Apply.cs:29`, one line). Friendly fire needs nothing new:
  `FriendlyFireListener.SwingResolved(in SwingReport)` gives `Thought_AttackedByColonist` to a
  colonist hit or missed by a colonist exactly as for a blow. A struck colonist retaliates through
  `React` — a charge on foot, or a return of fire if she holds a gun; a hog rolls its revenge.
  **Blood must change to read `damageKind != Blunt`**: today `CombatFeedback.BloodSidesOf` and its
  two neighbours test `== Sharp` (`CombatFeedback.cs:252, 259, 262`), so a bullet would not bleed
  until P3 changes them. `BuildingTargets` prices Bullet at 1,000 ‰ (today `Sharp ? … : blunt`
  would price it as blunt). **`RangedRules` never rolls knockback**: `MeleeRules.cs:92-95` gives
  every non-blunt weapon the sharp knockback chance, which is harmless only because a bullet never
  goes through `MeleeRules`. **The health line (PR #213) maps `Sharp ? Wound : Bruise`**, so on
  whichever merges second a bullet must be added to the wound side, or it bruises.
- **Saved** in its own keyed section, `odyssey.projectiles`, skipped when absent — no format number
  of its own. **Hashed only while non-empty**, the corpse registry's rule, so the registry's
  existence moves no golden; the R0 re-bake is the skill's alone.
- **Published** as `ProjectileView { Shooter, Target, Start, End, FireTick, ImpactTick, Weapon }`
  (`Target` added on review, 0 for none, so a hit's streak can end on the drawn body, §4c) through
  `SnapshotWriter.AddProjectile` → `WorldSnapshot.Projectiles`, the `Falling` pattern. **The hit, the
  damage and the end cell are the simulation's; the muzzle, the streak and the flash are
  presentation's.**

### 2d. The driver, and fire at will

`AttackRangedJobDriver` (`Job_AttackRanged`, handle 23) is the melee driver's shape with the
range-and-sight test where the reach test was. Toils `Approach = 0` and `Aim = 1`; `ToilProgress`
counts the aim in milliwork. **Corrected on review**: the first draft said "`Rates.Scale` a tick,
so a slow colonist aims slowly", but `Rates.Scale` is the constant 1,000 (`Rates.cs:32`) and the
melee wind-up adds it flat (`AttackMeleeJobDriver.cs:105`), so every colonist would aim alike. The
aim advances by the pawn's **condition pace** — WS3's innate pace with starvation and rest on it,
the thing that already slows a hungry colonist's walk — and **not** by a skill curve: Shooting buys
accuracy (§2a), not speed, as the reference has it. `Pawn.WorkRatePerMille` is the wrong accessor
(it is keyed on a work type, and there is no shooting work type); R3 names the condition-only one,
and `RangedDriverTests` holds a starving colonist's aim to longer than a fed one's. Everything it
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
  call it (`CombatJobs`, `AttackJobFor`, `CanBreakBuildings` and `Ranged` are **new**; nothing by
  those names exists today). `Ranged.NearestTargetInSight(ctx, me, armament)` is one pass over the
  pawns — `Melee.IsThreatTo` for a colonist, standing colonists for a bandit — within `rangeMm²`, nearest by
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

### 2e. When a gun cannot be fired (added on review)

Melee has no rule for any of these (`AttackMeleeJobDriver`, `Melee`, `CombatSystem`, `Draft` and
`WeaponDraw` never mention water, ladders, jumps or loads), because a blow needs only reach. A gun
needs both hands and a steady stance, so the ranged driver refuses to **start** an aim in each of
them. An aim already running breaks in the same way as a lost line: back to `Approach`, no
cooldown spent. Each is one predicate in `StartAim` and one test in `RangedDriverTests`.

| State | Rule | Why |
|---|---|---|
| **Swimming** (the swim predicate design 20 uses; not a wading cell) | no aim; the chase walks on toward the first dry boundary with a line | both hands are in the water, and the float pose (design 20) has nowhere to put a gun |
| **On a ladder or mid-jump** (a step in flight) | already covered: an aim starts only at a step boundary (§2d) | stated so nobody "fixes" it by letting the aim start mid-step |
| **Carrying a load** | the load goes down first — the carry's own drop (`DropCarried`), then the aim | both hands; the carry (design 24) already owns putting a load down |
| **Downed, asleep, stunned** | the job ends (downed, asleep) or the aim is lost (stunned, design 33 §5j) — melee's rules unchanged | |
| **The target** swimming, on a ladder, mid-jump | can be shot: the line is walked to the cell it is in | only the shooter needs a stance |

## 3. Contracts

### 3a. Handles, claimed once (R0)

Every table below is append-only and a save contract, so all of them are extended in one commit,
the combat line's rule.

**The numbers are the next free values on `main` on 2026-09-25, and three open PRs want the same
ones** (checked on review): #213 (health) claims `JobHandle` 23 `Tend`, `ItemHandle` 11 `Medkit`,
`SkillIndex` 6 `Medicine` and hash bit 22; #184 (medical supplies) claims `Treat` 23, `Patient` 24,
`MedicalSupplies` 11, `Medicine` 6 and hash bit 22; #223 (forage) claims `Forage` 23, `Berries` 11
and `Mushrooms` 12; #215 (traits) claims hash bits 22 and 23. None bumps the save format. **So R0
takes the next free value at the moment it merges, grepped then, not the number written here** —
whichever of those lands first moves this line's numbers up, and the table below is corrected in
R0's own commit. The names do not move.

| Table | Append | Where |
|---|---|---|
| `JobHandle` / `JobIndex` / `Jobs.xml` / `BuildDrivers` | 23 `AttackRanged` (`Job_AttackRanged`); above `JobSystem.HashedAlways`, so no golden moves until it runs | `Sim.Contracts/Catalogue.cs`, `PawnContent.cs`, `PawnRegistry.cs` |
| `ItemHandle` | 11 `Pistol` (`Item_Pistol`, category Weapons, stack 1); the label is the registry's, `ui.item.pistol` = **Sidearm** — code says `Pistol`, every screen says what the CSV says | `Catalogue.cs`, `Items.xml`, `Hud/ItemLabels.cs` |
| `SkillIndex` | 6 `Shooting` (`Skill_Shooting`); `SkillIndex.Names` gains `"shooting"`; `CombatContractTests` skill count 6 → 7, **and its `JobHandle.Count` 23 → 24 and `ItemHandle.Count` 11 → 12** (`CombatContractTests.cs:77, 79, 84`) | `PawnContent.cs`, `Skills.xml` |
| `AttackStyle` / `DamageKind` | `Pistol` = 4 / `Bullet` = 2 | `CombatDef.cs` |
| `CombatEventKind` / `PawnGesture` | `Shot` = 13 / `Fire` = 5 | `Sim.Contracts/Views.cs` |
| `PawnPurpose` | `RangedHit`, `RangedDamage`, `RangedScatter`, `RangedIntercept`. **Corrected on review**: the first draft said "the next four SHA-256 round constants after `Knockback`", but `Knockback` (K9) is not the last — `Jump` (K10, `PawnContent.cs:1624`) and `WeatherSystem`'s two (K14, K15, `WeatherSystem.cs:13, 16`) are taken, and #213 claims K11. The free ones today are **K12 `0x72BE_5D74`, K13 `0x80DE_B1FE`, K16 `0xE49B_69C1`, K17 `0xEFBE_4786`**; grep every branch again at R0. (#213's own `HitRegion = 0x2431_85BE` collides with `main`'s `Jump` — reported to that line, not ours to fix.) | `PawnContent.cs` |
| `GridSize` | `CellSizeXZMm = 2500`, `CellSizeYMm = 3000` — the first simulation-side owner of ADR 0002's numbers; `CellSizeHasOneOwnerTests` holds `CellMetrics` to them (the `HopPriceHasOneOwnerTests` pattern) | `Sim.Contracts` |
| Save format | **9 → 10.** The skill arrays are length-prefixed and the loader clamps, so a seventh skill needs no bump for layout; the bump guards one thing — `if (FormatVersion < 10 && pawn.IsPerson) RollStartingSkills()` on load, so a colonist from an older file is dealt her Shooting level once. `RollStartingSkills` draws in index order and skips a skill already holding experience (`Pawn.cs:925-952`), so the first six deals are unchanged and the re-deal is idempotent. **Passions are not re-dealt on load**, so a colonist from an older save has no Shooting passion; accepted — dealing one would need its own guarded roll, and a passion is a thing a player notices appearing. **Written into `SaveFormat`'s remark so nobody tidies the guard away and re-rolls on every load** | `SaveFormat.cs`, `PawnRegistry.Load` |
| Save section | `odyssey.projectiles`, keyed and skipped when absent; hashed only while non-empty | `ColonyWorld.SaveComponents`, `ColonyComposition` |
| Registry CSVs | `ui.status.shooting`, `ui.debug.spawnpistol` ("Spawn sidearm"), `ui.debug.spawngunman` ("Spawn pistol bandit"); icon-map gap rows; wiki and `Registry.g.cs` rebuilt in the same commit. `ui.item.pistol` and `ui.skill.shooting` already exist | `docs/design/icon-keys.csv`, `icon-map.csv` |
| Presentation catalogue | `ModuleIds.ItemPistol` appended to `ItemModules` (order = `ItemIndex`; `ModuleIdTests` holds it to `ItemIndex.Count`); four **empty** clip rows. **Corrected on review**: they are not all `CombatRows`. `CombatPistolAim` and `CombatPistolFire` are `CombatRows`, with two new `CombatRole` values and their `CombatPose.RowOf` entries (`CombatPose.cs:197`); `CombatPistolDraw` and `CombatPistolHolster` go in **`SheathRows`** beside the sword's draw and sheathe (`ModuleCatalogue.cs:789-807`, which says so), picked by `SheathClipFor` (`Sheath.cs:139`) by style (§4b) | `ModuleCatalogue.cs`, `PlayScene.cs`, `CombatPose.cs`, `Sheath.cs` |

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
`HoldTarget:116-153` · `CombatThinkNodes.AttackJob.Fill:33-59`, `SelfDefence:109` (and `:101`) ·
`HostilityResponse.cs:91, 116, 132` · `WeaponDraw.TargetNear:57-63` · `MeleeRules.cs:71` (Bullet
never stuns; dodge 0 for a defender mid-aim) · `BuildingTargets.cs:143` (Bullet ×1) · `Draft.cs:42-51`
· `JobSystem.Attack.cs:40-63` (accepted when in range and sight **or** reachable; the building
refusal) · `JobSystem.Attack.cs:83-104` `OrderAttackBuilding` (hard-codes melee at `:89` and `:100`;
the R3 refusal and R6's lift both land here) · `Pawn.UrgencyPerMille:712-713` (a shooter runs) ·
`PawnRegistry.OrderCellOf:653`, `OrderTargetOf:673` · `PawnRegistry.Spawn(cell, kind, weaponDef)` — a
**new overload** beside `Spawn(cell)` `:57` and `Spawn(cell, kind)` `:67` — + `HandleSpawnPawn` (`:134`) reading
`intent.B` as `weaponDef + 1` (0 = the kind's own table) — the debug pistol bandit with no new kind:
`PawnOutfit.Bandit` is keyed on `Person | Hostile`, so it keeps its helmet and vest · `PawnContext`
(`Projectiles`, `RangedRules`) · `ColonyComposition` (build, hash, contribute) · `SaveFormat` and
`PawnRegistry.Load` (the guard) · the debug Arm row deals the pistol already — every item with a
weapon block is in its lottery — a test, not a change.

### 3f. Presentation seams

`PawnFigureDirector.Combat.cs` `PlaysWorkStroke` (excludes the new job) · `CombatPose.SwingRole` /
`StyleFor` (`Pistol` → the aim and fire roles) · `CombatFeedback.BloodSidesOf` (`!= Blunt`) and
`Handle(Shot)` (the sound from the shooter's feet) · `SoundIds.ForCombat(Shot)` · `CombatSoundTiming`
(a shot schedules no cue; it plays on its frame) · `SoundIds.CombatShot` / `CombatShotFar` and their
two `AudioSetup` rows, plus `normalize = false` on the gunshot's import (§4c-bis) ·
`CombatOrders.IsWeapon(Pistol)` · `JobLabels` row
23 · `SkillCatalogue` live row (the **sixth**: construction, mining, growing, cutting and melee are live
today, and `ui.skill.shooting` is marked `NotSimulated, "no combat"` at `SkillCatalogue.cs:114-129`) ·
`DebugDirector` two rows · `WeaponProfileBake.Rows` · new files
`PawnFigureDirector.Aim.cs` (or a partial beside `Combat.cs`) and `Presentation/World/ProjectileDirector.cs`.

## 4. Presentation

### 4a. The prop (P1)

**POLYGON Battle Royale's `SM_Wep_Pistol_Heavy_01`** (owner, 2026-09-25: *"can we confirm we can
use the pistol from the synty battle royale as the prop for gun"* — **confirmed**, measured below).
The first draft named Sci-Fi City's `SM_Wep_Pistol_01` and said to pin it because "Battle Royale
sorts first on a shared name"; that was false — Battle Royale ships **no** `SM_Wep_Pistol_01`
(checked on review).

**What Battle Royale offers**, read from the FBX geometry on 2026-09-25 (the reader calibrated
against Sci-Fi City's pistol, which the committed inventory measures at 0.06 × 0.28 × 0.38 m and the
reader at 0.062 × 0.281 × 0.378; Battle Royale was imported on 2026-09-22 and is not in
`synty-inventory.csv` yet):

| Prefab | Size (w × h × l) | Parts | Reads as |
|---|---|---|---|
| **`SM_Wep_Pistol_Heavy_01`** | 0.046 × 0.21 × 0.39 m | 5: frame, **slide**, magazine, trigger, hammer | a semi-automatic sidearm — **the pick** |
| `SM_Wep_Pistol_Revolver_01` | 0.055 × 0.21 × 0.47 m | 11 (cylinder and six chambers) | a long revolver — the revolver of §6, later |
| `SM_Wep_Revolver_Snub_01` | 0.055 × 0.18 × 0.26 m | 11 | a snub revolver |
| `SM_Wep_Pistol_Flare_01` | — | — | a flare gun, not a weapon |

The pick's facts, which the fit and the tracer depend on:

- **Axes and pivot are the Sci-Fi pistol's convention**, so `FitPistol` is written once for both:
  barrel along **+Z** (−0.094 to +0.296 m), grip down **−Y** (to −0.109 m), origin at the grip
  behind the trigger, width along X (±0.023 m). The **muzzle** is at about **(0, 0.07, 0.296) m** in
  the prefab's frame — the slide's front face at the bore's height, read off the slide mesh
  (y 0.036–0.101, z to 0.250) and the frame (z to 0.296). P1 measures it off the drawn mesh as a
  socket rather than trusting this number (P11), and the tracer and flash start there.
- **One material, `PolygonBattleRoyale_Wep_01`**, on all five parts: the same Shader Graph
  (`PolygonGeneric`'s `Generic_Basic`) as the melee weapons' `PolygonBattleRoyale_01_A`, so
  `SyntyInstancingKeepAlive` already keeps its instanced variant for the player build. It is a
  second material — the weapons atlas, `Textures/Weapons/Wep_Skin_01..05` — so a pistol on the
  ground is one more instanced bucket, not a new shader.
- **Five renderers in the held prop**, so a held pistol is five draws. Bounded by the 64-figure
  ceiling (320 at most, and far fewer in any real fight); the far form draws no weapon. If P4 finds
  it matters, the parts are baked into one mesh at load (they share the material).
- **The slide is its own part**, `SM_Wep_Pistol_Heavy_Slide_01` — which buys a detail for nothing:
  the slide cycles back ~2 cm and returns over 60 ms on `Fire` (§4b). Found by its name, so it falls
  back to no cycling on any other prop.
- **The ground scale is the four weapons' ×1.5**, and `prefabName` is unique across every pack, so
  no `prefabUnder` is needed; it is pinned to `PolygonBattleRoyale` anyway so a future pack with the
  same name cannot move it (the prefix match `MeadowFolder` already uses, `PlayScene.cs:1338`). The
  same row is the ground item and the held prop.
- **Licensed, so the usual rule**: the simulation and its tests never see it; without the pack the
  pistol draws as the placeholder every item has, and `WeaponPropTests`' pistol arm ignores itself.

**`FitPistol`**, chosen when `WeaponStyles[def] == Pistol`: the
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
  pistol rows are empty, so a computed draw plays: right hand to the holster over **0.15 s**, the
  prop re-parented at the grasp instant (the sword draw's "hand on hilt" moment), then the arc up
  over **0.30 s** — **0.45 s** in all; holster the reverse at **0.8 s**, slower because nothing
  waits on it. The gun changes parent at the grasp instant and at no other time. Without this
  keying a checkout with the sword pack plays `A_Draw_Sword` on a pistol.
  **Corrected on review — the draw must finish inside the first aim.** The first draft's 0.6 s draw
  was longer than the 0.5 s aim (30 ticks), and a colonist who draws *because* a target came near
  (`WeaponDraw.TargetNear`) starts her aim on the same tick, so her first shot would have fired
  with the gun still at her hip. So: the draw is 0.45 s; the arc's last beat **is** the aim's
  ease-in (the aim weight starts rising at the grasp, not after the draw), so the two are one
  motion; and **a `Fire` arriving mid-draw snaps the pose to aimed on that frame** — the flash
  comes from wherever the muzzle is, and a gun is never seen firing from the holster. At 2× and 3×
  the 0.5 s aim is 0.25 s and 0.17 s of real time and the draw (real time) cannot keep up; the snap
  is what makes that honest rather than broken. `CombatDrawnTests` holds a `Fire` mid-draw to
  "aimed that frame".
- **Carrying the gun drawn, not aiming** (added on review — the first draft had a draw, an aim and
  a holster but no pose for the time between). `PawnFlags.Drawn` and not in `Aim`: walking the
  chase, standing through the cooldown with no line, holding position. **Low ready**: the pistol in
  the right hand only, the barrel 40° down and forward, the forearm across the body, over the
  graph's gait — the pose the melee weapons' drawn carry already has a slot for (`FitWeapon`'s
  held seat, with `FitPistol` deciding the angle). The off hand is free. From 48° above it reads
  as *armed, not shooting*, which is what the player needs to tell apart. Between two shots at
  the same target, with the line still open, she **stays in the aim stance** through the cooldown
  (`WorkFocus` holds, §2d); low ready is only for when she has nothing to shoot at.
- **Tracking a moving target** (added on review). The aim point is `WorkCell`, which the driver
  moves cell to cell as the target walks (§2d), so aimed straight at it the figure would snap
  2.5 m at a time. The aim point is **the target figure's drawn chest** when it has a figure (the
  director can resolve the target: `WorkCell` names the cell, and the figure standing on or
  stepping out of it is the one), else the cell centre at chest height, **followed through the
  same critically damped spring as the recoil at 120 ms**, so the arms swing smoothly after a
  walking target and settle on a standing one.
- **The slide** (added on review; the Battle Royale pistol's own part, §4a): on `Fire` the slide
  child moves back 2 cm along the barrel and returns over 60 ms. Visible only zoomed in (10–20 m),
  free, and falls back to nothing on a prop without a part of that name.
- **Hit, down and death** need nothing new: a bullet's `Hit` goes through the same `CombatEvent`
  the blow does, so the flinch, the hit reaction, the down and the corpse all play as for melee.
  **No knockback** (stun 0, §2c) and **no critical**, so the knockback and critical reactions
  never fire for a bullet. The struck figure's reaction is driven by the event, so it lands on the
  impact tick, after the flight — not on the shot.
- **The clip seam** (the owner's "both"). `ModuleIds.CombatPistolAim / Fire` are declared in
  `ModuleIds.CombatRows` and `ModuleIds.CombatPistolDraw / Holster` in `SheathRows` (§3a), all with
  no clips; `CombatClip(CombatRole, variant)` and `SheathClipFor` already return null for an empty
  row, so the computed path takes over exactly as `PoseSheath` snaps without the sword clips today.
  **Battle Royale ships no animation at all** ("characters set up with Mecanim with no animations
  included", `e-10` finding 3), so the pistol's pack gives the prop and nothing to move it. When a pack arrives, filling the rows is a `PlayScene` edit and a
  catalogue rebuild (then `CharacterSwatches.Classify`, `docs/lessons.md`). **Before any purchase**,
  the cheapest experiment from `e-10`: one free pistol clip retargeted on to three Synty rigs at the
  height extremes, the sole read with `MeasureSole` — P11 says a third-party Humanoid clip may not
  stand on the floor. Third-party FBX goes under a gitignored folder on `Assets/Synty`'s terms.
- **Far form** (past the 64-figure ceiling): no weapon and no stance, as today; the tracer and the
  flash still say who is shooting.
- **Every state, one table** (so nothing is left for the implementer to invent):

| Sim state | Figure pose | Prop |
|---|---|---|
| undrafted, no target near | the graph's own | holstered at the left hip (§4a) |
| drafted / target near, drawing | computed draw, 0.45 s | hip → right hand at the grasp |
| drawn, nothing to shoot | **low ready**, over the gait | right hand |
| `Aim` toil | isosceles aim, easing in over the aim, tracking | right hand, left cupping |
| `Fire` (gesture / `Shot`) | recoil spring 250 ms; slide 60 ms | right hand, left cupping |
| between shots, line open | the aim stance held | right hand, left cupping |
| undrafted, or 2 s with no target | computed holster, 0.8 s | right hand → hip at the grasp |
| struck, downed, dead | melee's reactions, unchanged | whatever a held sword does in the same state today — P2 copies it, does not decide it |
| swimming, carrying | the swim / carry pose; no aim (§2e) | whatever a sword does in the same state today |
| far form | none | none; tracer and flash only |

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
  `ZTest` on, `ZWrite` off. **One matrix per bullet into one bucket, one instanced draw per frame**,
  counted in `DrawCalls` and split at `MaxInstancesPerCall` (511); no allocation after the first
  frame. **Corrected on review**: the first draft said "through the `GatherCellPlate` machinery —
  already written". It is not reusable: `ChunkRenderer.GatherCellPlate` (`ChunkRenderer.cs:3953`)
  is private, keyed by colour, and draws `UnitCube` with the opaque `BracketMaterial`. The
  director **copies its pattern** (a pooled matrix array per material, one
  `RenderMeshInstanced` per bucket) on its own quad and its own additive shader,
  `Odyssey/Tracer`, committed, so it draws on a checkout with no pack. **The alternative**, ranked
  first by `d-21` for the general case, was "the rain pass's own pattern — `RenderPrimitives` from
  a segment buffer". **The rain pass has no segment buffer** (`RainDirector.cs:158, 163`: each drop
  is placed in the vertex shader from its instance id and a clock), so that alternative is a new
  buffer path of its own, not a reuse; with at most one bullet per armed pawn in flight (below) the
  bucket is the cheaper thing to write and the same one call. **Explicit render-queue values** so water, tracers and rain are never compared by distance
  (P17): a batch sorts as one object by one bounds, and "let URP sort it" is the tie waiting to
  happen.
  Rain is `Transparent+50` (`OdysseyRain.shader:44`); the tracer and flash take `Transparent+60`,
  above it, so a tracer through rain is never hidden by the rain batch's single sort distance.
- **A hit ends on the drawn body** (added on review — the owner's *"hits must visibly connect with
  the target"*). The end cell's centre is where the numbers say the target is, but a walking
  figure is drawn up to a cell off it, so a streak ending at the cell centre would stop in the air
  beside the body. `ProjectileView` therefore carries **`Target`** (0 for none) beside the end
  cell. While the target's figure stands on or is stepping out of `End`, the streak's end point
  is that figure's **drawn chest, re-read every frame**, so the streak arrives at the body. On the
  impact tick the simulation's `Hit` or `Miss` decides the rest: a `Hit` stops the streak there
  and puts blood on that body; a `Miss` (the target stepped off the line, §2c) lets the streak run
  on to the end cell, past the body — which is the near miss the owner asked to see.
- **A streak is never shorter-lived than the eye** (added on review). A point-blank shot at
  1,000 mm a tick flies 3 ticks — 50 ms at 1× and 17 ms, **one frame**, at 3×. The streak at the
  moment of impact is kept as an **afterimage** at its final position, fading over **0.08 s of real
  time** after the impact tick, so every shot is seen at every speed. It never extends the bullet:
  it is drawn from the same bucket and holds no simulation state.
- **Paused, the bullet hangs.** The lerp is `FallArc.Progress(tick, alpha, …)`, and alpha stops
  with the clock, so a paused world shows every bullet mid-air — a free "bullet time" the player
  can inspect. The flash (4 ticks) hangs with it; the afterimage fades in real time, so it
  finishes even when paused.
- **How many at once.** One shooter can have at most one bullet in flight: the longest flight is
  26 m / 1,000 mm = 26 ticks and a pistol fires at most every 60 ticks (the cooldown counts from
  the aim's start, §2d). So the bucket is
  bounded by the armed pawns — well under one `MaxInstancesPerCall` for any colony there is.
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
  scales with pixels at 4K; measure ten lit against ten unlit before adding it. **The packs' own
  effects were looked at and not taken for the first cut** (review): Battle Royale ships
  `Bullet_Trail_FX`, `GunShot_Smoke_FX`, `Bullet_Shell_FX` and `BloodSplat_FX`, and Particle FX
  ships `FX_Gunshot_01` and `FX_Gunshot_BarrelSmoke_01`. Each is a `ParticleSystem` on a
  GameObject — a pooled instance per shot, a draw per system, and a licensed dependency the art-free
  checkout must fall back from — where the bucket is one call and committed. They are the P3 look
  experiment: one contact shot of our flash against `FX_Gunshot_01` at 20 m and 60 m, the owner
  picks, and a pack effect, if chosen, plays **only** within the near-sound distance (§4c-bis) with
  the bucket as the fallback.
- **Impact.** On a `Hit` or `Miss` whose weapon is a gun: ground → `ChipDirector.Throw` with a new
  `ChipRecipe.Dust` (many, tiny, short, earth-coloured); a body → the existing blood spurt, its
  direction from the shooter's feet, which `CombatFeedback` already computes for any distance; a
  wall → the stone or timber chips the building-hit path already picks. **The near miss is three
  things and no new pass**: the streak visibly continues *past* the target to the scatter cell, the
  dust lands there rather than on the body, and the sound at that cell is the "bullet by" rather than
  the thud. A hit is the streak *stopping* on the body plus blood plus the hit sound; hit and miss
  diverge on the tick the simulation decided them. **No shell**: under a pixel at this camera.
- **Sound** — §4c-bis. The impact keeps the thud (body, building) and `CombatMiss` (ground) until
  the owner asks for a ricochet; a near miss's "bullet by" is `CombatMiss` at the scatter cell,
  which is silent today, as it is for a dodged blow.

### 4c-bis. The gunshot (added on review, 2026-09-25)

The owner supplied the recording on 2026-09-25 — `freesound_community-single-pistol-gunshot-33-37187.mp3`
(Pixabay, Pixabay Content License, the owner to confirm, as for every clip since the draft) — with
*"blend this into the environment and process it for every gun shot (and give it some variance)"*.
**It is baked and committed in this PR**; wiring it to the `Shot` event is P3, after approval, and
until then nothing plays it.

**The bake**, `tools/audio/bake_gunshot.sh`, every step measured and argued in its header:

- **The source is brick-walled**: −6 LUFS integrated, +4.3 dBTP, a 200 ms plateau at 0 dBFS RMS and
  4,082 samples at or over full scale. It is taken down 12 dB in float before anything else, so
  nothing clips it twice; high-passed at 60 Hz (a DC offset and rumble); cut hard 2 ms before the
  onset, so the report starts **within 7 ms of the first sample** on every near take and plays on
  the `Shot` frame with no scheduling. The loudest 10 ms is *not* a timing constant (it wanders
  across the plateau take to take), unlike the melee sounds; `CombatSoundTiming` schedules nothing
  for a shot.
- **Blended into the environment.** The recording is dry and close — a sound effect over the
  world rather than a shot fired in it. Each take is the dry report plus an **outdoor space**: a
  diffuse tail convolved from a synthesised impulse (pink noise decaying ~48 dB a second, 180 Hz –
  3.2 kHz, 22 ms pre-delay for the ground) and one soft **slapback at 140 ms**, a treeline or a
  terrace riser ~24 m off. No room: the colony is outdoors.
- **Two distances, as two sounds**, because the director has **no per-voice filter** and a rolloff
  curve can only make a sound quieter, never duller:

  | Sound | Takes | What it is | Level (max momentary) |
  |---|---|---|---|
  | `combat-shot` | 3, at 1.00 / 0.95 / 1.06 speed | the dry crack forward, the space 13 dB under it | −14 LUFS, −3 dBFS peak (limited) |
  | `combat-shot-far` | 2, at 1.00 / 0.96 | low-passed at 1.6 kHz (air takes a report's top within 100 m), the space only 3 dB under it, blooming 0.10–0.15 s after the onset | −20 LUFS |

  −14 is the loudest thing in the game on purpose: 4.5 dB over the melee thud, 1 dB over the
  critical slice's target.
- **Variance, three layers**: pitch baked into the takes (pitch is most of what tells two reports
  apart — the pick's and the swim's trick); **a different impulse per take**, so the tails differ
  too and three shots are three shots, not one sample three times; and the director's per-play
  pitch and volume variance on top.

**The wiring (P3)**:

| | `CombatShot` (`"combat.shot"`) | `CombatShotFar` (`"combat.shot-far"`) |
|---|---|---|
| Played when | the shooter is **within 70 m** of the listener | beyond 70 m |
| Clips | `Variants("combat-shot")` | `Variants("combat-shot-far")` |
| Volume / variance | 0.85, ±0.12, pitch ±0.04 | 0.8, ±0.10, pitch ±0.03 |
| Min / max distance | 20 / 150 m | 50 / 400 m |
| Priority | 90 — above every blow (100–140), below the alerts | 120 |
| Cooldown | 0.03 s: two shots on one frame are one report; a volley a frame apart is a volley | 0.06 s |
| Import | PCM, decompress on load, mono, **`normalize = false`** | same |

`CombatFeedback.Handle` special-cases `Shot`: it measures the shooter's distance to the listener
once and offers one of the two. **70 m** is the camera's own middle: it starts at 48 m and zooms
10–160 m, so a player zoomed in on a fight hears the crack and one zoomed out over the colony hears
the thump. A hard switch is honest here because each shot is a new sound. Nothing is faded
between them.

**The voice budget.** Sixteen pooled voices serve the colony (`AudioDirector.VoiceCount`). Ten
shooters firing once a second with a 1.4 s tail keep about fourteen reports sounding at once. The
director already steals by priority, and the gunshot's 90 means a fight steals from the chop
and the carry rather than the other way round. **P4 counts `VoiceStarved` and `CooldownSkipped`
in the gunfight arm** before anybody calls sixteen enough.

**Two seams, recorded, not built**: a shot **under a roof** wants a room, not a meadow — the
shelter rule (`SkyColumns`, design 43 §6a) already answers "is this column covered", and a third
take set is one more bake with a short, bright impulse. And **nothing ducks for it or under it**:
no bed is lowered for a shot and the rain does not muffle one — a fight in a storm is a playtest
question, not a rule written in advance.

**Found on the way, and not this unit's to fix: Unity is peak-normalising most of the game's
mono sounds.** Every clip `.meta` except the six carry sounds and the menu bed has
`normalize: 1`, `AudioSetup.Apply` never sets `importer.normalize`, and Unity's importer
normalises the downmix when `forceToMono` is on, which `AudioSetup`'s own comment on the alerts
already says "would throw away the loudness match the bake exists to produce". So the combat
sounds' careful −21 / −18.5 / −16.8 LUFS ladder (`bake_combat.sh`), the swim stroke's −24 and the
draft's level are each raised to a 0 dBFS peak on import. The gunshot's metas are committed with
`normalize: 0`. The one-line fix for the rest (`importer.normalize = false` in `Apply`) **changes
how loud every chop, blow and stroke is** and wants a listen, so it is its own small PR, not this
one. **Unverified in Unity**: the evidence is the metas and the importer's documented behaviour;
the check is `AudioClip.GetData` on `combat-hit` against the WAV's own −3.0 dBFS peak.

### 4c-ter. As built (P3, 2026-09-25)

`Presentation/World/ProjectileDirector.cs`, wired in `OdysseyBootstrap.LateUpdate` straight after
the blood's draw, inside `FrameSection.Overlays`, with the blood's band (`LowestDrawnLayer` to
`HighestVisibleLayer`). `CombatFeedback` hands it every `Shot` and every gun's `Hit` and `Miss`
before it draws, so the director follows a bullet from its `Shot` even when no snapshot ever
carried it — the 3× point-blank case — and learns where it ended from the impact's event. Where the
build departs from, or had to settle, what §4c and §4c-bis say:

- **The streak's shape is in its matrix.** `Odyssey/Tracer` reads the tail from the translation, the
  line from the z column, the width and the fade from the lengths of the x and y columns (built
  square to the line, so the matrix stays invertible). That is what lets the afterimage's fade ride
  in the same bucket with no per-instance property to keep alive in a player. It follows that the
  shader has **no fallback**: any other shader would draw the matrix as a transform. It is in
  `ShaderInclusion.Required`, the always-included list and the keep-alive folder.
- **The draw is counted through the renderer** (`ChunkRenderer.DrawOverlayInstances`, gated on
  `SubmitToGpu`), as the blood's is, with an explicit world box, because a shader that places its own
  vertices cannot be culled from the quad's bounds. Streaks and afterimages are one call, flashes a
  second.
- **"Stands on or is stepping out of the end cell"** is taken as `Cell == End || NextCell == End`:
  a figure stepping *into* the end cell is drawn half on it, and ending the streak at the cell centre
  there is the "stops in the air beside the body" the rule exists to prevent.
- **A walls-down storey's clip plane is its own floor**, not the band's ceiling: the band still
  reaches above it (walls-down hides stacked storeys, not layers), so the plane a bullet from it
  crosses is the ceiling of what is drawn beneath.
- **The muzzle is used only while `PawnFlags.Drawn` is set**; a holstered or absent prop starts the
  streak at the shooter's drawn chest, else the start cell at chest height. It is latched on the
  frame the bullet is first seen; the flash re-reads it each frame, so it rides the recoil.
- **"A wall → the stone or timber chips the building-hit path already picks" — no such path
  exists.** Nothing in presentation throws chips for a blow on a building; chips come only from the
  work stroke. A bullet that strikes a building (a `Hit` with no target) throws the same dust as one
  into the ground, until a unit gives building hits their material's chips.
- **`importer.normalize` does not exist in the scripting API.** `AudioImporter` exposes
  `forceToMono`, `loadInBackground` and the sample settings, and not the normalise switch, which is
  why `AudioSetup` never set it. It is written through the importer's serialised form
  (`AudioSetup.SetNormalize`), for the two gunshot families only, and the build throws if the field
  cannot be found. The fix for every other clip stays its own PR.
- **The shot's sound is chosen in `CombatFeedback.ShotSoundFor`**, at `ShotNearMetres` (70 m) from
  `AudioDirector.ListenerPosition`, and plays from the shooter's drawn feet on the `Shot`'s frame;
  `CombatSoundTiming` is untouched. Blood reads `damageKind != Blunt`, so a bullet bleeds as an edge.

### 4d. Interface

Right-click on a hostile from a gun-holder already routes through `CombatOrders.Route` →
`OrderAttack`; `HandleOrderAttack` accepts a target in sight *or* reachable. The lock-on ring and the
order line work unchanged once `OrderTargetOf` / `OrderCellOf` know the job. The skills pane gets
its sixth live row through `SkillCatalogue` (all fourteen rows are listed whatever is live, so no
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
ignored without the art); **a `Fire` mid-draw snaps the pose to aimed on that frame**; **drawn and
not aiming is low ready, not the aim**; **the aim point follows a walking target without a 2.5 m
snap** (the spring, fed a scripted target path); **the slide cycles and returns within 60 ms, and a
prop with no slide part does nothing**; `CellSizeHasOneOwnerTests`; `ProjectileDirectorTests` — N
bullets are one draw call, the cross-layer clip (a bullet from a hidden storey starts at the band's
ceiling; one to an undrawn cellar ends at its floor; a bullet with neither end drawn draws
nothing), **a hit's streak ends on the target's drawn chest while it is stepping out of the end
cell** (control: no target, the cell centre), **a one-tick flight still leaves a streak on screen
for 0.08 s**, **a paused bullet does not move**. `AudioCatalogueTests` (or its equivalent): both
shot rows resolve three and two clips, and **every gunshot clip imports with `normalize` off**.

**PlayMode**: `FrameTimeTests.TheFrameWithGunfireInView` — peace, the brawl (existing) and a
gunfight (ten pistol colonists drafted against ten bandits) in one run; the gunfight timed twice,
once with `ProjectileDirector.Enabled = false` as the control (the `InstanceCellPlates` pattern);
**the pass's draw calls ≤ 2 whatever the shooter count** is the structural gate that stays in the
tier; the timing is `Category("Measurement")`. The same arm logs the audio director's
`VoiceStarved` and `CooldownSkipped` over the gunfight (§4c-bis's voice budget).

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
- **The draw is shorter than the first aim, and a shot mid-draw snaps to aimed.** Lengthening the
  draw "because it looks better" puts the first shot out of the holster.
- **A hit's streak ends on the drawn body, not the cell centre.** The owner's one hard requirement
  for the look.
- **The streak's afterimage.** Without it a point-blank shot at 3× is invisible.
- **The gunshot imports with `normalize` off.** Turning it on raises every take to a 0 dBFS peak
  and throws away the level the bake set.
- **Two gunshot sounds by distance.** Collapsing them into one with a longer rolloff makes a
  distant shot a quiet crack instead of a thump.

## 8. Open, and recommended for the owner to confirm

Recommended, for the owner to confirm or overrule before R0:

1. The exponent hit formula (§2a), not the flat curve.
2. The invented starting numbers: aim 30 ticks (ref 18), 1,000 mm a tick, 26 m, the scatter constant
   3, the per-cell curve's middle point.
3. Interception probabilistic with a 5 m dead zone; a downed pawn never takes a stray; no 50 % stray
   coin.
4. No dodge against bullets; no dodge for a pawn mid-aim; no criticals for bullets.
5. Point-blank fire, no pistol-whip (the reference melees with the gun).
6. The tracer as one instanced bucket on our own shader, charged to Overlays; a new
   `RenderPrimitives` buffer path as the alternative (the rain pass has none to share, §4c).
7. Buildings under fire as R6, after the first playtest; until then a gun-holder ordered at a door
   is refused.
8. *(Added on review.)* **Battle Royale's `SM_Wep_Pistol_Heavy_01` as the prop** (§4a) — the
   owner asked for Battle Royale; of its three pistols this is the only semi-automatic.
9. *(Added on review.)* The aim advances by **condition pace, not skill**: Shooting buys accuracy,
   not speed (§2d).
10. *(Added on review.)* **Low ready** between shots with nothing to shoot, the **0.45 s** draw
    finishing inside the first aim, the **afterimage**, and the **states a gun cannot fire from**
    (§2e, §4b, §4c).
11. *(Added on review.)* **The gunshot as baked** (§4c-bis): outdoor space, two distances split at
    70 m, three near and two far takes, −14 LUFS near. **Listen before approving**: the bake cannot
    be judged from its numbers.

Open, not settled from the code:

- **Targeting up through a ghosted slice** (§4d).
- **The gunshot's licence** — Pixabay Content License, the owner to confirm as for every clip since
  the draft; whether a ground impact wants its own sound.
- **Gun animations**: none in any pack (Battle Royale included); the computed stance ships; the pack
  experiment gates a purchase (§4b).
- **Unity's importer normalising the existing mono sounds** (§4c-bis): its own small PR, with a
  listen.
- **Handle numbers 23 / 11 / 6** race three open PRs (§3a); R0 takes what is free at its merge.
- **The fire-at-will cost**: bounded on paper; R5 measures it beside the melee control before the
  cadence is called a number.
- **`PawnFlags` is full**: any later "aiming" or "reloading" bit widens the flags word, which is a
  `PawnView` constructor change every hand-built view in the tests touches.
- **The debug pistol bandit's weapon deal bypasses `WeaponFor`** (design 42's hash of the pawn); an
  eventual gunman kind restores the one owner.

## 9. Review, 2026-09-25

Reviewed on PR #220 after merging `main` (25 commits behind). Every code claim in §2–§4 was
checked against the tree — about ninety symbols and twenty cited lines. Most held. What did not,
and where it is now corrected:

| Claim | Was | Is | Where |
|---|---|---|---|
| Design number | 44 | **47** — `main` took 44 (selection highlight); 45 and 46 are on open branches | title |
| The prop | Sci-Fi City `SM_Wep_Pistol_01`, pinned because "Battle Royale sorts first on a shared name" | **Battle Royale `SM_Wep_Pistol_Heavy_01`**, measured; BR has no `SM_Wep_Pistol_01`, so the reason was false | §4a |
| `PawnPurpose` salts | "the next four after `Knockback`" | K12, K13, K16, K17 — K10 is `Jump`, K14/K15 are the weather's, K11 is claimed by #213 | §3a |
| Handles 23 / 11 / 6 | stated as free | free on `main`, claimed by #213, #184 and #223; R0 takes what is free at its merge | §3a |
| `CombatContractTests` | skill count 6 → 7 | also `JobHandle.Count` 23 → 24 and `ItemHandle.Count` 11 → 12 | §3a |
| The aim's rate | "`Rates.Scale` a tick, so a slow colonist aims slowly" | `Rates.Scale` is a constant; condition pace, not skill | §2d |
| Blood for a bullet | "reads `!= Blunt`" (present tense) | reads `== Sharp` today; P3 changes three sites; #213's wound mapping needs the same | §2c |
| `attacker` nullable | one parameter | three: `ApplySwing`, `SwingReport.Attacker`, `StrikeBuilding` | §2c |
| Draw and holster clip rows | in `CombatRows` | in `SheathRows`; aim and fire need `CombatRole` values | §3a, §4b |
| The tracer | "through the `GatherCellPlate` machinery — already written" | private, colour-keyed, opaque cubes; copy the pattern | §4c |
| The alternative tracer | "the rain pass's segment buffer" | the rain has no segment buffer | §4c |
| Skills pane | "fifth live row" | sixth | §1, §3f, §4d |
| Stale lines | `SelfDefence:108`, `UrgencyPerMille:678`, `OrderCellOf:650`, `OrderTargetOf:670` | 109, 712–713, 653, 673; `OrderAttackBuilding` added | §3e |

**Added, because the owner asked that animation, bullets and projectiles be thought through
before execution**: the states a gun cannot fire from (§2e); the draw finishing inside the first
aim and the snap on a shot mid-draw, low ready, tracking a moving target, the slide, hit reactions,
and one table of every state's pose and prop (§4b); a hit that ends on the drawn body, the
afterimage for short flights, the paused bullet, the in-flight bound, the render queue against the
rain, and the packs' particle effects as a look experiment rather than a dependency (§4c); and the
gunshot, supplied, baked and levelled, with its wiring and voice budget (§4c-bis).

## 10. Built, 2026-09-25

Approved by the owner the same day (*"approved - execute"*) and built on
`claude/ranged-combat`, stacked on PR #220. Units R0–R4, H1 and P1–P3 are in; R5 (the soak and
the benchmark), R6 (buildings) and P4 (the frame measurement) are not, and neither is a player
build. What the build did differently from the text above, each for a reason found in the code:

| Where | The design said | Built | Why |
|---|---|---|---|
| §2c landing | the intended target on any crossed cell is a certain hit | **only on a shot aimed true**; a shot that missed passes its own target | a miss's line to its scatter cell usually crosses the target's cell, so "any crossed cell" would have turned most misses into hits and the hit chance into fiction; the near miss the owner asked to see is this rule |
| §2c reaction | a struck colonist retaliates through `React` | **not when she was a bystander hit by a colonist's stray** — she remembers it (`Thought_AttackedByColonist`), she does not turn on the shooter | a colonist in the way is not attacked; retaliating would start a fight inside the firing line every time a shot went astray |
| §2c experience | Shooting trains in `ApplySwing` | **at the shot**, in `CombatSystem.Fire` | a bullet that strikes a wall never reaches `ApplySwing`, and every shot trains, hit or miss |
| §2d aim pace | the condition pace, accessor named at R3 | `Pawn.ConditionPerMille()` — hunger and temperature, floored at 700 | the one condition-only rate on the pawn; `WorkRatePerMille` needs a work type |
| §2e swimming | the swim predicate design 20 uses | the shallow-water cost class on the shooter's cell | the swim is drawn by presentation off the water line; the simulation's only water fact is the cell's cost class |
| §2e carrying | the load goes down first | nothing to do | every driver drops its load in its own cleanup, so no attack job ever starts carrying |
| §3a clip rows | four empty pistol rows | **none declared** | see the draw below; a row is added the day a pistol clip exists, rather than four rows nobody reads |
| §4b draw and holster | a computed 0.45 s arc, the sword rows keyed off | **the sword pack's draw and sheathe**, cut short to the hand by a shot | the pistol holsters at the left hip, and the sword's draw from the left hip is a cross-draw — which is what a left-hip holster is drawn with. The authored clip, with its grasp moment already measured, beats a computed arc. Without the pack, it snaps, as a sword does |
| §4b aim | spine, chest and upper chest at 0.3 / 0.4 / 0.3 | spine and chest at 0.45 / 0.55 (yaw), 0.4 / 0.6 (pitch) | the figure binds no upper-chest bone |
| §4b recoil | 12 % of the pitch into the shoulders | 25 % of it into the chest, backwards | the chest is the bone the pose pass already moves; tune it first, as §4b says |
| §5 driver test | an unreachable target in sight is accepted | **not yet tested**: needs a terrace fixture where a target stands out of reach but in sight | a pen of walls with a gap in it is reachable; the test was rewritten to what it could honestly check, and this case is owed |

### 10a. The owner's first play, 2026-09-25

*"it's really decent and everything seemed to work well - but seemed to miss a lot from just a
height up. Also some of the shots were way off like the projectile went down or not even in a place
a gun would fire to so keep it more accurate. Also make sure shots that hit actually connect with
the target directly"* — and *"rename "sidearm" to "Pistol""*.

| Report | Cause | Change |
|---|---|---|
| misses a lot from a height | a height gives clear lines to far targets, fire at will takes them, and the reference's curve left a level-0 colonist at 16 % at 12.5 m and 2 % at 25 m | **the curve raised**: per cell 876 / 943 / 983 (was 747 / 903 / 951), the pistol's bands 95 / 85 / 65 / 45 % (was 80 / 70 / 40 / 30). 1, 5 and 10 cells: level 0 83 / 43 / 17 %, level 10 90 / 63 / 36 %, level 20 93 / 77 / 54 %. Skill still buys the reach |
| shots way off, going down | a miss drew a cell from a 7 × 7 box round the target in its layer — from a height, often inside the terrace or off to one side — and the tracer was drawn all the way there | **a miss carries on past its target** along the line of fire, one to three cells by how bad the shot was (`RangedRules.MissCell`), and **ends where that line first stops** (`LineOfSight.StopCell`). Its streak passes 0.6 m beside the body and goes into the ground there (`ProjectileDirector.MissPoint`) |
| hits not connecting | a shot aimed true was walked to the cell the target stood in *when fired*; a walking target had usually stepped on, so the bullet missed and the tracer ended at the old cell | **a shot aimed true lands on its target wherever it stands at the impact** — cover it stepped behind, or a body that stepped in front, still takes it, and nothing else does. `ProjectileView.Aimed` is published and the streak follows the target's drawn chest the whole flight. This reverses §2c's "a target can step out of a long shot", at the owner's word |
| "Sidearm" | — | **Pistol** on every screen (`ui.item.pistol`, the debug rows) |

## 11. Weapon quality, 2026-09-25

The owner, the same day: *"can you give the guns quality like you do with beds (and apply this to
all weapons) depending on the spawn/who crafted them - make sure this is included"*.

**The beds' system, not a second one.** The five tiers are `QualityHandle`'s — Poor, Normal, Decent,
Uber, Epic — and the roll is `QualityContent.Roll`, which centres a tier on the maker's skill so a
novice never makes Epic and a master never makes Poor. A tier's `QualityDef` gains two numbers:

| Tier | Damage | Hit chance |
|---|---|---|
| Poor | ×0.90 | ×0.90 |
| Normal | ×1.00 | ×1.00 |
| Decent | ×1.10 | ×1.05 |
| Uber | ×1.20 | ×1.10 |
| Epic | ×1.35 | ×1.15 |

The reference's shape, a tenth a step and more at the top. INVENTED values in `Quality.xml`,
tunable there. **Every weapon** takes it: the hit chance of a swing (`MeleeRules`) and of a shot
(`RangedRules`), and the damage of both, and of a blow at a building.

**Who made it.** Nothing is crafted yet, so every weapon is a find, and the maker's skill stands in
for how good a find is (`WeaponQuality`):

| How it arrived | Maker's skill | Rolls mostly |
|---|---|---|
| the debug menu's grant, the Arm row | 6 (`FoundSkill`) | Normal, sometimes Poor or Decent |
| a bandit's own gear, the pistol bandit's included | 2 (`BanditSkill`) | Poor, sometimes Normal |
| crafted, the day there is a bench | the crafter's skill | as a bed is, by its builder |

**Where it lives.** `ColonyItem.Quality`, rolled once when the item is made (on its own stream,
`PawnPurpose.WeaponQuality`, salted by the thing's id) and kept for life. It is saved in the item
record from format 10, the unshipped bump this line already makes, and **hashed only when set**, so
no golden moved. It is published on `ThingView.Quality` and, for the held weapon, as
`odyssey.pawn.weapon.quality`, and carried into a fight on `Armament.Quality`. A weapon with no tier
(from an older save, or a path that never rolled) fights as Normal. **The interface names it**:
*Pistol (Decent)* on the item's pane, the colonist's weapon row and the gear row.

**Not built**: quality changing what a weapon looks like, and crafting. Deterioration and a
quality floor for traders are the reference's and are not in any plan.
