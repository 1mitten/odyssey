# Ranged combat — owner interview

**Phase:** Interview (feature-level, in the shape of `meadow-interview.md`).
**Date:** 2026-09-25. **Branch:** `claude/beautiful-cannon-pldakk` (documents only; no code was
written before or under this file).
**Conducted by:** Claude Code, ten questions in three rounds, after a read-only exploration of the
combat line, the figure director, the effect passes and the process.

The owner's brief: *"Now we have melee - I'd like to plan out guns or projectiles. We have synty
assets to use for the gun but I want to concentrate on the mechanics and also how the projectile and
animations look. Look carefully at rimworld but we need to fit this in here, start with a basic
pistol. A gun is wielded just like melee weapons by the side if possible or bigger weapons in future
could go around back. When someone is ready to fire - they will posture with two guns on wielding the
gun around the handle and bullets fired (recommend performant effective methods) and a visible bullet
would be hit and miss - hits must visibly connect with the target as misses can be near misses as
well. There is time between aiming and firing. Injuries from shooting to be decided in latest seam.
Ensure animations for pulling out gun aiming and holstering and also some recoil feedback as the gun
fires is key."* And, mid-interview: *"also take into account height because projectile weapons can
fire from many heights - but obviously get inaccurate with distance depending on the gun."*

**Read next:** `docs/design/47-ranged-combat.md` (the design these answers decide),
`docs/plans/ranged-combat.md`, and the six research files `a-10-ranged-combat`,
`a-10-projectile-path`, `c-3d-shot-line`, `d-21-projectile-rendering`,
`d-22-procedural-aim-and-recoil`, `e-10-gun-animation-packs`.

## 1. What the exploration found, put to the owner before the first question

- **Nothing ranged exists anywhere.** A weapon is an attack block on an item Def (damage, wind-up,
  cooldown, kind, style) with no range field; there is no line-of-sight query in the simulation, no
  fog of war, no Shooting index (the registry names `ui.skill.shooting`; `SkillIndex` stops at
  Melee), no projectile, no raid incident (bandits are debug spawns), no gun clip in any pack and no
  gunshot sound. The research lane for the reference's ranged rules had no file.
- **The melee line was built so a second kind of attack slots in.** A swing is decided when its
  wind-up starts (`AttackMeleeJobDriver.StartSwing`, `IMeleeRules.Resolve`), held on the pawn and
  saved, and applied at the impact tick through one method (`CombatSystem.LandOrLose` →
  `ApplySwing`) with four hooks. A load mid-swing resumes on the same tick (`LandTheStep`, design 33
  §21c). A bullet's flight is that wind-up extended.
- **Draw and holster already exist.** The simulation says whether the weapon is out
  (`WeaponDraw.IsDrawn` → `PawnFlags.Drawn`); presentation swaps the prop between the right hand and
  a measured hip fit and plays the sword pack's draw and sheathe clips, snapping without them. The
  prop is seated by a blade rule that would put a pistol's barrel along the forearm.
- **The gun pose was named two months ago and never started**: design 13 G8, *"the gun: aimed hold,
  off hand on the foregrip, recoil over the hold"*, and §7's *"does the gun want a muzzle flash —
  defer until combat asks"*. The flinch laid over a playing clip is the precedent for an impulse.
- **The supply drop is the precedent for a bullet's flight**: the simulation owns the launch and
  landing ticks and presentation lerps at tick plus alpha (`FallArc.Progress`).
- **Art**: Sci-Fi City ships `SM_Wep_Pistol_01` and a revolver; the Particle FX pack has gunshot,
  tracer and impact effects. **No pack on record has a single gun animation.**
- **Process**: the playtest queue is over its ten-row limit, so a feature needs the owner's say;
  this request is that say. The next free design number is 44.

Two readings were stated and not corrected: *"posture with two guns on wielding the gun around the
handle"* = both hands on the gun, around the grip; *"injuries from shooting to be decided in latest
seam"* = hit points through the existing damage seam, no body parts, no bleeding.

Defaults stated and not objected to: no ammunition; aim before every shot, then a cooldown; a wall
blocks the line or it does not (no partial cover value); a shooter beside an enemy fires point-blank
rather than pistol-whipping; the pistol holsters at the hip and long guns on the back is a recorded
seam; a document of its own rather than a section of design 33.

## 2. The answers

| # | Question | Answer |
|---|---|---|
| 1 | Does a drafted colonist with a pistol shoot hostiles in range without an order? | **Yes, fire at will.** An ordered target takes precedence; a per-colonist toggle is a later row. |
| 2 | Where does a missed bullet go, and can it hit somebody else? | **Scatter and intercept**: a miss lands near the target, the spread growing with distance; anything on the bullet's line (a colonist, a bandit, a wall) *can* take it. |
| 3 | Add a Shooting skill now, or reuse Melee? | **Add Shooting now** — a Def, a `SkillIndex` entry, save format 9 → 10, one golden re-bake, a fifth live row. |
| 4 | Who carries a pistol in this unit? | **Colonists, plus a debug pistol bandit.** Ordinary raids stay melee. |
| 5 | Is the bullet's flight real in the simulation? | **Real flight**: the fire tick rolls, the impact tick is fire + distance / speed, the impact re-checks who is on the line; a target can step out of a long shot; drawn tick-exact. |
| 6 | Where do draw, aim, recoil and holster come from? | **Both**: procedural now; a clip-driven path takes over if a pack's rows are filled. |
| 7 | What does a shot look like? | **Ballistic**: muzzle flash, a short bright tracer, dust or blood at the impact. The owner sources a gunshot sound. |
| 8 | Does the pistol shoot between layers in this unit? | First **own layer only** — superseded by 9 when the owner raised height. |
| 9 | How should height count? | **Shoot between layers now**: line of sight is three-dimensional from the first commit; a slab, a roof, a closed door, a wall or rock in the line blocks; distance is Euclidean in metres over 2.5 × 2.5 × 3 m cells, so height adds to the range and to the fall-off. |
| 10 | Does firing from above give an accuracy bonus? | **No bonus.** The advantage is the clear line and the reach; the factor slot stays at 1. |

## 3. Why the recommended answers were recommended

1. **Fire at will** is what the registry's own `ui.command.hold` row already promises (*"stay and
   shoot from here"*) and what the reference does; the alternative — a drafted line that stands and
   watches bandits walk up — is the thing a player reports first.
2. **Scatter and intercept** is the reference's own rule, and one line walk gives friendly fire by
   gunfire, the visible near miss and, later, cover, from the same cells. A miss that hits nothing
   makes gunfire safe for one's own side in a way melee is not.
3. **Shooting now** because accuracy by skill is the whole feel of the reference's shooting and the
   registry already names the row; the cost is one re-bake, which the contracts step pays once.
4. **Colonists plus a debug bandit** tests being shot at — the response rules under fire — without
   rebalancing every raid or reopening "should four colonists lose to three bandits".
5. **Real flight** is the melee's decide-then-apply shape with the flight where the wind-up was; it
   is what makes a hit visibly land on the body the numbers say, and it gives the owner's "a target
   can step out of a long shot".
6. **Both** because no pack on record has a gun clip and the clip-row fallback already exists
   (`PoseSheath` snaps without the sword clips); the seam costs four empty rows.
7. **Ballistic** because Sci-Fi City's pistol reads as a ballistic sidearm; the energy look belongs
   to the pack's laser-era rifles, later.
8. **Between layers now** because a colonist on a terrace who cannot shoot the bandit below is a
   day-one report on this board of 3 m risers, and the research found the 3D walk cheap and
   symmetric by construction.
9. **No height bonus** because there is no reference number to calibrate against; the slot stays at
   1 and a bonus is one factor later.

## 4. Tensions the owner chose into, recorded so they are not read as faults

1. **Real flight against the reference's trigger-decided hit.** In the reference a shot rolled as a
   hit lands wherever the target now stands and the flight is drawn only; here a rolled hit on a
   target that stepped off the line is a miss at the end cell. A report that *"my 76 % shot missed"*
   is this rule working, not a bug. Design 47 §2c narrows the re-check so a target that walked *into*
   the line is still hit.
2. **Three-dimensional sight from the first commit against its cost.** The walk is a few dozen
   integer reads a shot (`c-3d-shot-line`), but it is a new simulation query whose correctness only
   a property test can hold; symmetry and mirror invariance are its gate.
3. **Procedural against a pack.** No third-party clip has been checked on a Synty rig, and P11 says
   one may not stand on the floor; the retarget experiment gates any purchase, and the procedural
   stance is what ships in the first cut.
4. **Fire at will with a 5 m dead zone against friendly fire.** A colonist standing more than 5 m in
   front of another can be hit by her; the reference is balanced on exactly that, and R5 measures the
   count before it is called a number.

## 4a. After the interview (2026-09-25, on the PR)

Two further answers, given while the design was in review, recorded here so the decision table stays
whole:

| # | Question | Answer |
|---|---|---|
| 11 | Which pistol? | **POLYGON Battle Royale's** — *"can we confirm we can use the pistol from the synty battle royale as the prop for gun"*. Confirmed and measured: `SM_Wep_Pistol_Heavy_01` (design 47 §4a). Battle Royale ships no animation, so the prop is all it gives. |
| 12 | The gunshot sound | **Supplied** — `freesound_community-single-pistol-gunshot-33-37187.mp3`, *"blend this into the environment and process it for every gun shot (and give it some variance)"*. Baked by `tools/audio/bake_gunshot.sh` (design 47 §4c-bis). |

And one instruction about the plan itself: *"make sure all the animations, bullets / projectiles are
thought out in the plan before execution"* — which is why design 47 gained §2e, §4b's state table
and the flight rules in §4c on review.

## 5. What this does not settle

- The seven recommendations design 47 §8 puts to the owner: the exponent hit formula; the invented
  starting numbers (aim 30 ticks, 60 m/s, 26 m, the scatter constant); probabilistic interception
  with a downed pawn never taking a stray; no dodge or criticals against bullets; point-blank fire
  rather than pistol-whipping; the instanced tracer bucket; buildings under fire deferred to R6.
- **The gunshot sound** — supplied (§4a); the owner to confirm the Pixabay licence and listen to the bake.
- **Targeting up through a ghosted slice** — `PawnUnderRay` picks only pawns at or above the clicked
  cell's layer, so a bandit on a roof above the slice cannot be right-clicked until the slice is
  raised (fire at will still engages it). Admitting a ghosted layer for a target changes what a
  click on a storey above means everywhere.
- **Whether to buy a pack**, and which — after the three-rig retarget experiment in
  `e-10-gun-animation-packs`.
