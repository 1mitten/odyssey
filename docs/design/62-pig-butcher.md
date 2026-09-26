# 62 — The Pig Butcher

**Written 2026-09-26**, from the owner's request and a one-round interview the same day. Branch
`claude/pig-butcher`, worktree `D:\code\odyssey-butcher`. Research: `docs/research/e-16-fantasy-rivals-pack.md`
(the art) and `docs/research/b-large-enemies-and-knockback.md` (prior art).

> *"we are interesting in implementing the "Pig Butcher" with animation. We want this to be part of
> raid/enemy - so because this is large - is it suitable to occupy more than 1 tile in someway? can
> we make this really difficult, hit area bigger - so many more hits land - maybe it was a swing
> animation to attack that in most cases will knock back colonists."* — the owner, 2026-09-26

## 1. What it is

A hulking hostile person with a cleaver: a boss that a colony fights **together**. Its threat is
not a bigger number on one blow. It is a **swing that sweeps three cells** and usually **flings
whoever it lands on two cells back**, into a wall, into a neighbour or off a ledge. Its size is in
how it fights and how it is drawn, **not in how many cells it fills**.

The names are ours: the **butcher** (`PawnKind_Butcher`, `Species_Butcher`), the **cleaver** (its
species' natural attack), the **sweep** (`SweepDef`, `SweepArc`), the **fling** (`FlingCell`,
`FlingStop`), the **slam**, the **brute's body** (`Health_Brute`).

**What existed and is reused, not rebuilt:**
- the hostile person and its mind (design 33 §5, `HostileThinkNode`), including breaking a closed
  door it cannot pass (§14b, §16b);
- the one-tile critical knockback (§9b), `CombatSystem.KnockbackCell` and `KnockBack`, which this
  generalises rather than forks;
- the blow decided at the wind-up (§9g);
- `CombatSystem.Hurt`, the one owner of damage, and `CombatSystem.Fall` (design 43 §7);
- `TraverseMode.Animal`: no ladders, no door it must open, no water;
- the Sword Combat heavy swings, stagger, knock-down and death clips (research `synty-sword-combat`);
- the weapon-in-the-hand path (`PawnFigureDirector.SwapWeapon`) and the debug menu's Spawn tab.

## 2. The owner's rulings (2026-09-26)

| Question | Ruling |
|---|---|
| Size | **One simulation cell, drawn huge, bulky**: it takes no ladder, and a closed door is a wall it breaks. **Not** a 2×2 footprint (§3). |
| What a swing hits | **The front arc of three**: the target's cell and the two cells either side of it, in front of the butcher. Behind it is safe. |
| Knockback | **Two cells**, usually. **Blocked is a slam**: the victim stops and takes extra damage. **Over an edge it falls.** About 2 s down, then brief immunity so nobody is knocked twice in a row. |
| Arrival | **Debug spawn only** until it has been played. No raid mix, no storyteller. |

## 3. Why one cell

Every colony sim we studied keeps even its giants to one cell (RimWorld, Dwarf Fortress). The one
series with true 2×2 units (XCOM 2) has them smash through walls, because a one-wide door cannot
admit them.

A footprint here would need:
- a clearance value per cell per layer, kept on every edit;
- a second region graph, or regions annotated with the largest size that fits;
- four-cell occupancy, reservations and eviction;
- a rule for a collapse under part of it;
- a vertical rule.

Every system that reads `Pawn.Cell` assumes one cell. The bulk the owner wants — it cannot follow
you up a ladder, and a door does not stop it but costs it time — comes from the traverse mode
alone, at no navigation cost. **Reusing `TraverseMode.Animal` adds no sixth district flood**
(design 33 §16b).

**What "bulky" costs the butcher:** it does not wade either, since the mode forbids water. On the
meadow a stream it cannot jump is a wall to it, and it goes round by a crossing. That is a feature
of a heavy thing, not a fault; the first play says whether it strands one.

## 4. The numbers (first tuning, INVENTED unless stated)

| Where | Field | Value | Why |
|---|---|---|---|
| `Species_Butcher` | `healthPoints` | **500** | five colonists' worth; downed at 0, dead at −250 |
| | `meleeSkill` | **14** | a veteran: ~88 % to hit on the curve, and hard to dodge against |
| | `movePerMille` | **800** | slower than a person, faster than a hog |
| | `interceptPerMille` | **800** | the reference's ceiling: a bullet crossing its cell almost always takes it |
| | `bodyLengthMm` | **3600** | drawing only; the figure's scale is measured (§8) |
| | `health` | `Health_Brute` | its own body (§6) |
| | `unstoppable` | **true** | never stunned, never knocked back (§7) |
| | `naturalAttack` (the cleaver) | damage **14**, wind-up **54**, cooldown **170** ticks | sharp, Heavy; the wind-up is the telegraph, about 0.9 s |
| `SweepDef` | `knockbackPerMille` | **750** | "in most cases" |
| | `distance` | **2** | owner |
| | `slamDamage` | **8** points | blunt, through `Hurt`; to both on a pawn slam |
| | `knockedDownTicks` | **120** | about 2 s (owner) |
| | `immunityTicks` | **300** | from the landing: 2 s down plus about 3 s standing (owner) |

A colonist has 100 points and pain shock at 64 (design 43 §3).
- **A cleaver blow is 14 ± 20 %**, and a critical is 21. So **four or five landed blows down a
  colonist**, and a slam adds 8.
- **Four colonists with bats need about 70 landed blows** to down the butcher. The balance probe
  (§10) measures how it actually goes, and §4a has what it found.

### 4a. Measured: one butcher against four bats (2026-09-26)

`ButcherBalanceProbe.OneButcherAgainstFourBats`: four colonists drafted, armed with bats, ordered
on to it from eight cells away, 5,400 ticks (a game minute and a half), a lockstep twin beside each
run. The simulation's own numbers, before any play:

| Seed | Its swings | Blows landed (target + flanks) | Flings | Slams | Colonists down | Butcher left |
|---|---|---|---|---|---|---|
| 1 | 18 | 18 | 7 | 0 | **4 of 4** | 326 / 500 |
| 2 | 17 | 20 | 12 | 0 | **4 of 4** | 291 / 500 |
| 3 | 13 | 19 | 9 | 0 | **4 of 4** | 384 / 500 |

**The butcher wins, every time, with two-thirds of its pool left.** Nobody died: an ordered fight
may kill, and this one ends in downs. That is the owner's "really difficult" taken literally, and
it is the number to tune against at the first play: a pool of 300, a cooldown of 200, or a lower
fling chance are each one XML line (§4). Four bats are not the colony's best answer. Pistols from
range (the butcher's intercept is the reference's ceiling) and a line of sandbags are, and neither
was in this probe.

## 5. The sweep

**A property of the species, not of the weapon.** The sweep, the fling and the cleaver itself are
the butcher's, carried as a species carries a hog's tusks (`naturalAttack`, `meleeSkill`, `sweep`,
`unstoppable` on `SpeciesDef`).

**The cleaver is not an item** (a departure from the plan, 2026-09-26). An item would have been a
new item handle (a save contract), a row in every store's filter, an Inventory entry, an icon and
a Spawn row, all to hold a weapon nobody else should swing. As the species' natural attack it does
not drop, nobody picks it up, and `WeaponRules.ArmamentOf` already reaches for it when the hand is
empty. Presentation draws it in the fist as the row's `HandProp` (§8).

**The primary blow is unchanged.** At the wind-up the target's blow is decided exactly as now
(design 33 §9g) and kept on the pawn. **The facing is kept with it**: the direction from the
butcher's cell to the target's, one of eight. It is packed into the spare bits 10–13 of
`Pawn.PendingSwing`, a word already saved and hashed while a swing is in the air, so **no save
layout and no hash bit moves for it**.

**At the impact** the primary lands through `LandOrLose`, as every blow does. Then the **flanks**:
- **Which cells.** The two cells either side of the target's cell, as seen from the butcher.
  - Facing east, they are north-east and south-east.
  - Facing north-east, they are north and east: the three cells of a quarter-turn arc centred on
    the target direction.
  - Each is a cell a knockback could reach: the butcher's own layer, beside it.
- **Who is struck.** Every standing pawn there that is **not of the butcher's faction**.
  - A colonist, a wild animal and a downed-but-not-yet-dead pawn are all possible victims; the
    downed one is spared, because an unordered fight ends in downs.
  - **Other hostiles are spared**, since the gang does not cleave its own.
- **How each is rolled.** Its own blow, **rolled at the impact**: hit, dodge, damage, critical, then
  the sweep's knockback. It uses the same rules but its own streams: `PawnPurpose.Sweep`
  (SHA-256's 33rd round constant, `0x27B7_0A85`) mixed with the victim's id, so no two victims and
  no primary share a roll.
  - The flanks are not known at the wind-up, because nobody stood there yet. So they cannot be
    §9g's; the primary's critical sound is unaffected.
- **Nobody in the arc** is a swing at one target, which is the whole of today's rule.

**The sweep's knockback** is rolled on `PawnPurpose.SweepKnock` (the 34th, `0x2E1B_2138`) at
`knockbackPerMille` for **every** landed blow of a sweeping species, primary and flanks alike, in
place of the critical's knockback roll. A critical still does half as much again.

## 6. The brute's body

A person's body downs at about 64 points of live injury whatever the pool (pain shock at 800 ‰,
design 43 §3), so a 500-point butcher on `Health_Person` would go down no sooner than a colonist.

`Health_Brute` has the same six regions at **five times** the hit points. Pain is **2.5 per mille a
point**, so shock is at **320**, and the bleed is **12 per mille of blood a day per point**, a fifth
of a person's.

It bleeds, and a bleeding butcher is a win on a clock, but a slow one. The rest (tending, blood
stages, falls) is the person's.

## 7. The fling

**`CombatSystem.FlingCell(ctx, attacker, target, distance)`** walks straight away from the attacker,
one step at a time, up to `distance`, and returns a `FlingPath`: where it lands and what stopped it
(`FlingStop`). **What moves the pawn is shared**: `KnockBack`'s body became
`CombatSystem.Displace` — end the job, move, knock down, re-issue a player's order, report
`KnockedBack` — and the critical knockback and the fling both go through it, so the two cannot
disagree about what being knocked down is.

**The one-tile rule itself was not rewritten** (a departure from the plan). `KnockbackCell` is
untouched and `KnockbackTests` pass unedited. The two rules differ on purpose:
- the critical lays its target beside a bystander, where the fling slams into one;
- the critical refuses a two-layer drop, where the fling falls down it.

A shared step with flags for both would have been two rules in one method. The fling reuses the
knockback's pieces instead: `IsWater`, `Passable`, `Melee.Holds` and `OnWhoItFights`.

Each step obeys the one-step rule's ground:
- a step the target could take, or one terrace step down;
- never water;
- never a cell another fighter holds;
- never the tile of the one it is fighting.

What stops a step is the **outcome**:

| The next step is | Result |
|---|---|
| legal | take it, and try the next |
| **a wall, a closed door, a rise, or its corner** | stop; **slam**: `slamDamage`, blunt, through `Hurt` |
| **a pawn standing there** (or a fighter's claim) | stop; **slam** to the victim **and** to that pawn |
| water | stop, and nothing more; the owner ruled out knocking into water |
| **air with nothing to stand on for two layers or more** | **fall**: land on the first standable cell below, within four layers, then `Fall(layers)`. Deeper than that is a wall. |
| off the board | a wall |

**Slam and fall apply only to a sweep's fling.** A critical knockback that finds no room still does
nothing at all, exactly as before (§9b), so no bandit fight changes.

A victim whose first step is refused **does not move** but is still knocked down: it has been hit
hard enough to fly, and something stopped it.

**Immunity.**
- A pawn flung by a sweep carries `KnockbackImmuneUntilTick` = landing + `immunityTicks`.
- While it runs, no knockback of any kind moves it; a blow still lands. Critical knockbacks read it
  but never set it, so bandit fights are unchanged.
- It lives on the pawn, is saved in `CombatSection` **layout 6**, and is hashed only while set.
  Hashing is under bit 28 of the pawn word, since bits 28–31 were free.
- The random purposes are SHA-256's 33rd and 34th round constants, `PawnPurpose.Sweep` and
  `SweepKnock`. The 25th to the 32nd were already taken on `main` or on branches in flight: raids,
  weather, prisons and trade.
- It counts in `HasCombatState`, and is put back to nought when it runs out, as the other clocks
  are.

**The unstoppable butcher.**
- It is never stunned: a stun roll against it is dropped at `ApplySwing`.
- It is never knocked back.
- A drafted colonist's order on it still works.

## 8. How it is drawn

**The art** is `SM_Chr_BR_PigButcher_01` from POLYGON Fantasy Rivals, imported as its own folder
only (research `e-16`). It is licensed, gitignored, and absent on the runner.

**The row** is `odyssey.module.pawn.hostile.butcher` (`ModuleIds.Hostile(6)`), with the cleaver
beside it at `….butcher.weapon`. It is out of the colonist family, so there is no lottery, no
swatches, and no look index of anybody else's moves.
- Its prefab is pinned under `Assets/Synty/PolygonFantasyRivals`, now a later pack that loses every
  name tie (`PlayScene.LaterPacks`).
- The Masc person clips drive it (idle, walk, run), with the Sword Combat `swing.heavy` role
  (`HeavyCombo01A/B/C`). The swing's impact is timed to the cleaver's 54-tick wind-up by the
  mechanism every swing already uses.
- **Its swing is heavy because its species' attack is**: `CombatPose.StyleFor` takes the kind's
  natural style (`PawnFigureDirector.KindStyles`, built from the content) when the hand holds no
  item. Before this, a person with no item always punched.
- **Scale 2.0, measured** (§8a).

**The look is chosen by kind.** The figure director's per-kind looks — the animals' table until now
— hold the butcher too (`HasKindRow`, `HostileLookFrom`). The look is a person's, so it gets the
combat layer, the work bones and a person's height window, and it is marked **`OwnPaint`**:
- the head is not bared;
- no overlay is switched on;
- there are no hair, beard or headgear slots;
- `Repaint` puts the art's own materials back rather than dressing it.

A corpse wears the same row.

**Its own box.** The butcher carries a drawn box as an animal does. `PawnFigureDirector.HasOwnBox`
is the one question the health bar, the selection ring and bracket, the click box, the wound
height and the floater height ask, so all of them stand at 3.6 m and not at a colonist's 2.5.

**The cleaver** is the row's `HandProp`, seated in the right fist by the weapon code's own fitting
(`NaturalWeaponDef`, −2). It is **never sheathed**, since the sheath rule is for items and the
simulation publishes no hand for a natural attack, and it is hidden only lying down.

**The fling** is drawn by `KnockbackSlide`.
- Its refusal rises from 1.5 cells to 3.5 across and 4.5 layers down.
- A slide further than the one-tile knockback can go is a fling (`KnockbackSlide.IsFling`). It is
  drawn over **0.45 s** instead of 0.25, **lifted 0.6 m** at its middle, and thrown rather than
  shoved.
- The landing plays the KnockDown Begin, Loop and End clips already mapped.

**A slam** raises a *Slam −8* floater (`CombatEventKind.Slam`, appended as 15), in the bad red.

**The telegraph.** While a sweep is in the air, the simulation publishes its facing as
`odyssey.pawn.sweep.facing`. It is derived from the swing already saved on the pawn, and neither
saved nor hashed itself. `OdysseyBootstrap.DrawSweepTelegraphs` turns that facing into three dim
red cell plates, gathered with the order marks into their one instanced call. It shows in a paused
frame and after a load with nothing of its own to keep.

**Past the figure cap** the butcher is not drawn at all, as an animal is not (design 29 §8a),
because the baked far form would deal it a colonist's face (`ChunkRenderer`). This is recorded as a
gap. With one butcher and the nearest-first cap it is on screen whenever it is near.

**Without the art** the row resolves to nothing and the butcher is the capsule every missing
figure is. Tests that need the art ask whether **the butcher's row resolved**, never whether a
catalogue exists (`docs/lessons.md`).

### 8a. Measured (2026-09-26, `ButcherProbe`)

| | Sole to crown, scale 1 | Width × depth (as posed) |
|---|---|---|
| `SM_Gen_Chr_Street_Male_02` (a colonist body) | 1.791 m | 2.048 × 0.307 m |
| `SM_Chr_BR_PigButcher_01` | **1.822 m** | 2.889 × **0.767** m |
| `SM_Wep_PigButcher_01` (cleaver) | longest side **1.503 m** | |

**The pack's giant is a colonist's height with two and a half times the girth.** At a colonist's
1.4 it would stand 2.55 m, a stout colonist. **Scale 2.0** stands it **3.64 m**, 1.45 times a
colonist's 2.51 m, which is the design's target, and the cleaver in its fist is about 3 m long.
3.64 m is taller than a 3 m storey: indoors, under a roof, its head goes through the ceiling. That
is the first play's question, and the lever is the one constant `PlayScene.ButcherScale`.

The catalogue was rebuilt with `RebuildCatalogue` and `CharacterSwatches.Classify`, then **spliced**
(docs/lessons.md): the rebuild reordered four item rows and wrote default `hopGait` fields, so only
the two new rows were lifted into the committed asset. The diff is 128 lines added and none
removed.

### 8b. What the PlayMode test and the first photographs found (2026-09-26)

`ButcherSpawnTests` found **three faults, each invisible to every tier before it**:
- **The cleaver was never drawn.** `SwapWeapon` returned for any negative weapon def, and the
  natural weapon is −2.
- **The box measured eleven giants.** The Fantasy Rivals prefab carries all eleven of its giants as
  inactive siblings, and a figure gathers skins including inactive ones (a bandit's vest needs
  that), so the butcher was measured 6.2 × 4.1 m. A body in its own paint now drops them at
  `Create`.
- **The box was in the wrong frame.** It was read in the figure's own frame, which at scale 2 is
  not metres. It is now the baked 3.60 m, by a square of the narrower side (1.85 m), and the test
  holds it between 3.3 and 4.0 m tall and under 3 m across.

`TheButcherInAFightPhotographed` (explicit, `Logs/look/`) shot the wind-up and a fling at the play
camera; `docs/reference/screenshots/2026-09-26-butcher-windup.png` is the first.
- The butcher reads as huge beside three colonists, its bar over its head.
- The three plates lie in front of it, under the colonists it is about to strike.
- **At 42 per cent the telegraph read brown**, because red over the meadow is brown. It is now 68
  per cent, and reads the orange-red the draft's own marks read in this light.
- **The fling does not show in a still.** Whether it reads as a throw is the first play's question.

## 9. Content and the registry

New keys in `icon-keys.csv`, none with art, so they fall through to a generated placeholder as
most keys do:
- `ui.pawn.butcher`, *Butcher*;
- `ui.debug.spawnbutcher`, *Spawn butcher*: the Spawn tab's row, under Hostiles;
- `ui.combat.slam`, *Slam*: the floater a slam raises, with its damage.

**Kind 6** is appended, since kind order is a save contract, and **the species is 4**. There is
**no item**: the cleaver is the species' natural attack (§5).

The content fingerprint moves once. **No golden should move**: no golden fights, and the new
fields are nought for every pawn in them. Confirm with `GoldenColonyProbe`.

## 10. Tests

**Fast tier** (`ButcherTests`, 30 cases; written first, and the sweep's control run to prove it:
with the flank call taken out, `InAFightItsSwingReachesMoreThanOne` fails):
- **what it is**: kind 6, species 4, a hostile person, `Animal` mode, `Health_Brute`, a sharp
  natural attack, and nobody else sweeping or unstoppable; its pool, its cleaver and its fixed level
  14, with a colonist's level still her skill;
- **the body**: seventy points down a colonist and not the butcher;
- **unstoppable**: a critical blunt blow neither stuns nor moves it;
- **the fling**:
  - two cells straight back in six directions;
  - a roll that did not fling;
  - a wall behind, which slams where she stands, still knocked down;
  - one cell then a wall;
  - a body in the way, which slams both;
  - water, which stops short with no slam;
  - a two-layer ledge falls; a one-layer step does not;
  - an immune pawn is struck but not moved, by a fling or by a critical;
  - the immunity runs back to nought;
- **the sweep**:
  - flanks struck, and target, behind, beside and two out not struck by it;
  - the eight facings and their flanks;
  - a bandit and a downed colonist spared;
  - each victim's dice its own;
  - three in four landed blows fling;
  - a bandit's swing keeps no facing;
- **the real fight**: its own mind's first swing reaches at least two colonists;
- **save and hash**: the facing and the immunity survive a save mid-wind-up, the two worlds hash
  the same after the swing lands, and the hash sees the immunity.

**Also in the fast tier:**
- `BanditDoorTests.TheButcherBreaksTheDoorDownToo`;
- the immunity in `CombatContractTests`' hash and round trip;
- both spellings of `odyssey.pawn.sweep.facing`.

`KnockbackTests` pass unedited.

**Long tier:** `ButcherBalanceProbe`, three seeds, each with a lockstep twin (§4a).

**Unity:** `ButcherSpawnTests` (PlayMode) takes the intent through the real bootstrap to a figure,
its own box over 3 m tall, and the cleaver in its hand. It ignores itself where the butcher's row
resolved to no art. `ButcherProbe` is the editor measurement behind §8a.

## 11. Open, recorded

- **A raid mix with the butcher.** It waits for the owner's first play (§2).
- **An overhead two-handed slam.** No owned pack has one. The one-handed heavy combo is the stand-in.
- **Past the figure cap it is not drawn** (§8).
- **Taller than a storey** (§8a): 3.64 m under a 3 m roof puts its head through the ceiling.
- **The balance** (§4a): as tuned, it beats four bats every time.
- **Camera shake on a slam.** Nothing in the game shakes the camera yet.
- **A slam into a wall does not damage the wall.**
