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

The names are ours: the **butcher** (`PawnKind_Butcher`, `Species_Butcher`), the **cleaver**
(`Item_Cleaver`), the **sweep** (`SweepDef`), the **fling**, the **slam**, the **brute's body**
(`Health_Brute`).

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
| `Item_Cleaver` | damage / wind-up / cooldown | **14 / 54 / 170** ticks | sharp; the wind-up is the telegraph, about 0.9 s |
| `SweepDef` | `flankCells` | **2** | the front arc of three: target plus two |
| | `knockbackPerMille` | **750** | "in most cases" |
| | `distance` | **2** | owner |
| | `slamDamage` | **8** points | blunt, through `Hurt`; to both on a pawn slam |
| | `knockedDownTicks` | **120** | about 2 s (owner) |
| | `immunityTicks` | **300** | from the landing: 2 s down plus about 3 s standing (owner) |

A colonist has 100 points and pain shock at 64 (design 43 §3).
- **A cleaver blow is 14 ± 20 %**, and a critical is 21. So **four or five landed blows down a
  colonist**, and a slam adds 8.
- **Four colonists with bats need about 70 landed blows** to down the butcher. The balance probe
  (§10) measures how it actually goes.

## 5. The sweep

**A property of the species, not of the weapon.** The cleaver is an ordinary heavy blade: it
drops when the butcher dies, and a colonist who picks it up gets a good weapon, not the butcher's
moves. This follows what a species already carries (`naturalAttack`, `meleeSkill`).

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

`KnockbackCell` is generalised to **`FlingCell(origin, target, distance)`**. It walks away from
`origin`, one step at a time, up to `distance`, and returns what stopped it. **The critical
knockback is its distance-1 call**, so there is one owner and `KnockbackTests` stays unedited.

Each step obeys today's one-step rule:
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
- It counts in `HasCombatState`, and is put back to nought when it runs out, as the other clocks
  are.

**The unstoppable butcher.**
- It is never stunned: a stun roll against it is dropped at `ApplySwing`.
- It is never knocked back.
- A drafted colonist's order on it still works.

## 8. How it is drawn

**The art** is `SM_Chr_BR_PigButcher_01` from POLYGON Fantasy Rivals, imported as its own folder
only (research `e-16`). It is licensed, gitignored, and absent on the runner.

**The row** is a catalogue row appended after the gang rows, out of the colonist lottery.
- Its prefab is pinned under `Assets/Synty/PolygonFantasyRivals`, a later pack that loses every
  name tie (`PlayScene.LaterPacks`).
- The Masc person clips drive it (idle, walk, run), with the Sword Combat `swing.heavy` role
  (`HeavyCombo01A/B/C`). The swing's impact is timed to the cleaver's 54-tick wind-up by the
  mechanism every swing already uses.
- **Scale is measured, not guessed.** The target is about 1.45× a colonist's drawn height. The
  measurement goes in §8a.
- The look is chosen **by kind**: `PawnOutfits` gains the butcher before its "hostile person →
  bandit" rule, so the butcher keeps the pack's own clothes, head and face. The hair, beard and
  headgear slots are off.

**The cleaver** in the hand is `SM_Wep_PigButcher_01`, seated as the cleaver item's row.

**The fling** is drawn by `KnockbackSlide`, whose refusal above 1.5 cells rises to **2.5**, with a
low arc. A fall past a ledge is drawn as today's one-step drop continued. The landing plays the
KnockDown Begin, Loop and End clips already mapped. A slam shows an impact floater.

**The telegraph.** For the length of the wind-up, the three cells of the arc carry a dim red plate:
presentation only, one instanced call, like the order marks.

**Past the figure cap** the butcher is not drawn at all, as an animal is (design 29 §8a), because
the baked far form would deal it a colonist's face. This is recorded as a gap.

**Without the art** the row resolves to nothing and the butcher is the capsule every missing
figure is. Tests that need the art ask whether **the butcher's row resolved**, never whether a
catalogue exists (`docs/lessons.md`).

### 8a. Measured

*(Filled in when the art is imported and measured.)*

## 9. Content and the registry

New keys, all in `icon-keys.csv` with an `icon-map.csv` gap row:
- `ui.pawn.butcher`, *Butcher*: "A hulking raider with a cleaver. Its swing sweeps three cells and
  flings whoever it lands on."
- `ui.item.cleaver`, *Cleaver*: "A heavy butcher's blade. Slow, and it cuts deep."
- `ui.debug.spawnbutcher`, *Spawn butcher*: the Spawn tab's row.

**Kind 6** is appended, since kind order is a save contract. **The species is 4** and the **item is
the next handle**.

The content fingerprint moves once. **No golden should move**: no golden fights, and the new
fields are nought for every pawn in them. Confirm with `GoldenColonyProbe`.

## 10. Tests

**Fast tier, test first:**
- `FlingTests`:
  - two cells straight back, orthogonal and diagonal;
  - one cell then a wall;
  - a wall at once, which slams without moving;
  - a pawn in the way slams both;
  - water stops with no slam;
  - a two-layer ledge falls and takes `Fall(2)`;
  - an immune pawn is struck but not moved;
  - the critical knockback is untouched, with `KnockbackTests` unedited.
- `SweepTests`:
  - both flanks struck, and behind and beside the butcher untouched;
  - a hostile in the arc spared;
  - a downed pawn spared;
  - each victim's roll independent of the others', by permuting ids;
  - nobody in the arc behaves as a single blow;
  - the facing survives a save taken mid-wind-up;
  - determinism across a lockstep twin.
- `ButcherTests`:
  - the kind's content (kind 6, species, mode `Animal`, the cleaver dealt);
  - `Health_Brute` downs at 320 and not at 64;
  - never stunned, never knocked back;
  - it breaks a closed door rather than opening it, and never takes a ladder.
- `CombatSection` layout 6 round trip, and a layout-5 save reads with no immunity.

**Long tier:** `ButcherBalanceProbe`, one butcher against four colonists with bats on the played
map. It logs downs, flings, slams and ticks-to-down, and asserts only invariants, not balance.

**Unity:** a PlayMode spawn test (intent → pawn → figure → swing clip), guarded on the butcher's
row resolving.

## 11. Open, recorded

- **A raid mix with the butcher.** It waits for the owner's first play (§2).
- **An overhead two-handed slam.** No owned pack has one. The one-handed heavy combo is the stand-in.
- **Past the figure cap it is not drawn** (§8).
- **Camera shake on a slam.** Nothing in the game shakes the camera yet.
- **A slam into a wall does not damage the wall.**
