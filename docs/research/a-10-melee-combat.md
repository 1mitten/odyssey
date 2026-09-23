# A10 — Drafting and melee combat

**Question.** How does RimWorld's drafting and melee combat work, in enough detail to design a
minimal clean-room equivalent for Odyssey? Researched 2026-09-23 by one subagent, capped at 12
searches and 10 fetches (9 and 10 used). Mechanics, formulas and design intent only; no Def XML,
no decompiled code, no names. Health itself is `a-02-health.md` and is not repeated here; how a
draft cuts across the think tree is `a-03-work-and-jobs.md` (lines 26, 31, 90).

## Findings

### Drafting

- Drafting **interrupts the current job whatever it is**, sleep included. Pausable work (a bill in
  progress) keeps its progress. The pawn equips its weapon and **drops what it was carrying**.
- A drafted pawn **stands where it is and does not look after its needs**: it will not eat or
  sleep. The needs keep falling, so a long draft ends in hunger, exhaustion, low mood and a break.
- **Auto-undraft** after **10,000 ticks (4 in-game hours)** with no threat and no order. Going
  down or breaking mentally also ends the draft.
- Controls: a toggle key and a command button. Right-click a cell to move (drag lines up a
  group; Shift queues). Right-click a hostile or a structure to attack.
- **Auto-engage**: any pawn capable of violence fights an enemy on an **adjacent** cell, even a
  pawn holding a gun. Ranged pawns only shoot unprompted under "fire at will".
- Chasing: melee pawns reportedly chase within about 6–8 cells (forum; unconfirmed).
- Melee attackers generally do not share a cell with each other.

### Hit, dodge, damage

- **Hit chance** is a curve over a score: Melee skill (+1 per level; +4 for skill-less pawns)
  plus sight and manipulation (weighted, capped). Points: **score 0 → 50 %, 10 → 80 %,
  20 → 90 %**, approaching ~96 % near 40.
- **Dodge** is a second, independent roll made only after a hit lands, on the **defender's**
  Melee skill: **0 → 0 %, 10 → 10 %, 20 → 30 %**. A nimble trait raises it. No dodge while
  aiming or firing a gun.
- A weapon has several **attacks** (edge, point, handle), each with a power and a cooldown.
  Choice weight = damage × (1 + armour penetration) × chance factor ÷ cooldown; attacks within
  95 % of the best are chosen 75 % of the time, those above 25 % are chosen 25 %, the rest never.
- Pacing is by **cooldown**; no melee warm-up is documented (unconfirmed).
- DPS = average damage × hit chance ÷ average cooldown.
- Default armour penetration is 1.5 % of damage.
- **Quality multiplies damage**: awful 0.8, poor 0.9, normal 1.0, masterwork 1.45, legendary
  1.65 (good ≈ 1.1 and excellent ≈ 1.2 from memory, unconfirmed). Material scales it too.
- **Armour** per layer: effective = armour − penetration; roll 0–100; under half deflects, under
  the whole halves; halvings multiply across layers; sharp that is reduced becomes blunt.
- **Damage types**: sharp (cut, stab, bite) bleeds; blunt does not unless it destroys a part, and
  has a chance to **stun** briefly. Pain 1.0–1.875 per point by type.
- **Unarmed human**: about **4.1 damage every 2 s** (~2 DPS). Animals use bites and scratches in
  the same system.

### Weapons (orders of magnitude)

- Melee weapons do **~4–10+ damage per hit** at **~1.5–2.5 s cooldown**: about 2–5.5 DPS at
  normal quality. A knife ≈ 7 / 1.6 s; a long blade ≈ 8 / 2.5 s; a top-tier blade ≈ 10 / 1.9 s.
- **One primary weapon slot**; equipping is a walk-to-and-pick-up job.

### Targets

- **Animals** roll a per-species "revenge on harm" chance each time they are hurt (a large
  herbivore ~10 %, an aggressive bird 100 %), **tripled for melee**. A vengeful animal attacks any
  human, not only its attacker, and calms after ≥ 10,000 ticks (~18,000 average), or on sleeping
  or going down.
- **Colonists**: killing one costs every colonist's opinion of the killer −10 for 60 days (120 for
  family). Social fights are short brawls ending in bruises.
- **Structures** can be ordered attacked by a drafted pawn and lose hit points.

### Skill

- Melee trains fast: players report hitting the 4,000 XP daily soft cap within a handful of
  attacks; past the cap XP is ×0.2. Passion multiplies it (`a-01-pawns.md`).

## Recommendation

Take the shape, not the whole system: **one HP pool per pawn, downed at zero, dead further
down**; one attack per weapon (no attack-choice weights); the hit and dodge curves at the points
above as Def tables; cooldown-paced swings with a short wind-up so the strike can be drawn on its
impact frame; blunt weapons stun, sharp weapons do not (bleeding waits for the body-part model);
per-species revenge chance; draft semantics exactly as above, including the 10,000-tick
auto-undraft and adjacency auto-engage. `docs/design/33-combat.md` records what was taken.

## Sources

- https://rimworldwiki.com/wiki/Drafted
- https://rimworldwiki.com/wiki/Drafting
- https://rimworldwiki.com/wiki/Melee
- https://rimworldwiki.com/wiki/Melee_hit_chance
- https://rimworldwiki.com/wiki/Melee_weapons
- https://rimworldwiki.com/wiki/Melee_DPS
- https://rimworldwiki.com/wiki/Combat
- https://rimworldwiki.com/wiki/Damage
- https://rimworldwiki.com/wiki/Apparel
- https://rimworldwiki.com/wiki/Manhunter
- https://rimworldwiki.com/wiki/Hunt
- https://rimworldwiki.com/wiki/Skills
- https://steamcommunity.com/app/294100/discussions/0/4032473436334160741/
- https://steamcommunity.com/app/294100/discussions/0/1652169858530344079/
- https://steamcommunity.com/app/294100/discussions/0/1743358239844821293/
- https://steamcommunity.com/app/294100/discussions/0/1735465524722386263/

## Confidence

**High** on the hit and dodge tables, the armour roll, auto-undraft, unarmed figures and the
found quality multipliers. **Medium** on attack choice, revenge chance, weapon DPS and chase
distance. **Low** on the missing quality tiers, melee warm-up and the equip job's detail.

## Could not be determined

- XP per melee hit or miss, and whether taking or dodging a hit trains.
- The exact hit-chance curve between the sampled points.
- Whether melee has a warm-up; whether manipulation changes cooldown.
- How a friendly is force-attacked, and any penalty for hurting one without killing.
- Whether melee does more or less to buildings, and how structure HP meets damage type.
- How a hit's body part is chosen (probably by coverage; see `a-02-health.md`).
- What a pawn does when an ordered target flees beyond chase range.
- How attacker stealth and distance move an animal's revenge chance.
