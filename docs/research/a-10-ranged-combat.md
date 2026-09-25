# Ranged combat in the reference: hit chance and the pistol numbers
*Lane A (RimWorld mechanics) · 2026-09-25 · capped at 12 searches and 10 reads (12 searches used; 0 reads returned content — 17 fetch attempts, every one refused by the egress proxy)*

> *Coordinator's note, 2026-09-25:* the container's proxy refused every content host this ran against, so the findings rest on search excerpts and the confidence line says so. After this ran the owner moved from "own layer only" to **shooting between layers** (design 47 §1); read the layer paragraph at the end with that in mind.

## Question

How does the reference decide whether a shot hits, what are the numbers for its basic pistols (and one rifle), what does the Shooting skill learn from, and how should its cell-based ranges be scaled to our 2.5 m cells?

## Findings

Every finding is from the reference wiki *as summarised by the search engine*, not from the page itself; the arithmetic checks are mine.

**The shape of the hit chance**

1. **A shot's hit chance is a product of independent factors with one additive term at the end** — the shooter's own per-cell accuracy raised to the distance, times the weapon's accuracy at that distance, times a weather factor, a smoke factor and a cover factor, plus a darkness term. [Weapons / Combat pages, via snippet]

2. **The shooter's accuracy is "the chance not to miss per cell of distance", applied as a power of the distance in cells.** The wiki's own example: a shooter at 99 % per cell has a 72.5 % base chance against a target 32 cells away; at 98 % it is 52.4 %. Both figures reproduce exactly as 0.99^32 and 0.98^32, which confirms the rule is a straight exponent, not a curve that merely resembles one. This is why a small change in the stat is a large change at range — it is the reference's stated design intent for the skill. [Shooting Accuracy page, via snippet; arithmetic mine]

3. **Four range bands, in cells: touch 3, short 12, medium 25, long 40.** A weapon carries one accuracy value per band, and between bands the value is **linearly interpolated**, so there are no steps in the curve. (Below 3 cells the touch value holds; my recollection is that beyond 40 the long value holds, but that clause was not recovered.) [Weapons page and the Accuracy (Close/Short/Medium/Long) stat pages, via snippet]

4. **A floor of 2 % is applied at the very end of the calculation.** No cap other than 100 % was mentioned. [Shooting Accuracy page, via snippet]

**The shooter's stat**

5. **A shooter with Shooting skill 0 has 89 % per cell.** That is the only skill row the snippets returned; the wiki holds a full 0–20 table (post-processed, per-cell, "assuming the pawn is healthy") with extra columns for the two traits, but the page could not be read. My unverified recollection is that the curve reaches 98 % at level 20 and is front-loaded (most of the gain by level 8). **Do not treat the interior of that curve as sourced.** [Shooting Accuracy / Skills pages, via snippet; the level-20 figure is recollection]

6. **Two traits shift the stat by the equivalent of ±5 skill levels**: a careless shooter −5, a careful one +5. The stat is also raised by manipulation (the arm/hand capacity) and by an implant; the snippet did not mention sight, though my recollection is that sight is a strong factor. There is a separate, higher stat for turrets. [Shooting Accuracy / Skills pages, via snippet]

**The other factors**

7. **Target size:** hit chance is **halved for targets of body size below 0.5 and doubled for body size above 2**; whether the middle is a flat 1.0 or a continuous scale was not recovered. Nothing was recovered about a downed target's *hit chance*; what was found is that a downed pawn still counts as cover for whoever is behind it while alive. [Combat / Weapons pages, via snippet]

8. **Weather** is a multiplier from **50 % in fog to 100 % clear**, with rain and snow between (values not recovered). **Smoke** in any cell between shooter and target multiplies by **30 %**. [Weapons page, via snippet]

9. **Darkness is additive**, not a multiplier: a target standing in darkness costs an ordinary shooter **−20 percentage points**, and a shooter with the DLC's darkness-loving belief gains **+25 points** instead. "Darkness" is a roofed cell at zero light or lit only by dark-light, or an unroofed cell at zero sunlight. [Weapons page, via snippet]

10. **Cover** is a multiplier from the object the target stands behind, depending on the object and on the angle the shot comes from. The owner has ruled partial cover out for now, so the values were not chased; a wall either blocks the line or it does not, which is the reference's own line-of-sight rule underneath the cover rule. [Combat / Cover pages, via snippet]

11. **Interception on the way** — the mechanic that makes real bullet flight matter: a pawn standing in the flight line other than the target intercepts the bullet with probability **40 % × its body size, clamped to 4 %–80 %**, and a downed interceptor is less likely to be hit. (The wiki's rule on where a *missed* shot lands was not recovered; my recollection is a random cell near the target, the radius growing with distance, and the bullet still hits whatever is there.) [Combat page, via snippet]

**The weapons** (bullet damage; AP = armour penetration; ticks are 60 a second in the reference as in ours, so they port unchanged)

12. **Autopistol:** 10 damage, AP 15 %, warm-up **18 ticks (0.3 s)**, cooldown **60 ticks (1.0 s)**, single shot, accuracy **80 / 70 / 40 / 30 %** (touch/short/medium/long), bullet speed 55. Its range was not in the snippet; the revolver's is 25.9 and my recollection is the autopistol's is the same. Market value not recovered. Described as quick to fire but short on stopping power and range. [Autopistol page, via snippet]

13. **Revolver:** 12 damage, AP 18 %, warm-up **18 ticks (0.3 s)**, cooldown **96 ticks (1.6 s)**, range **25.9 cells**, single shot, accuracy **80 / 75 / 55 / 40 %**, bullet speed 55, market value **135**. The medium and long values were raised in a balance pass at some point, so older sources will show 45 / 35. [Revolver page, via snippet]

14. **Bolt-action rifle (the contrast):** 18 damage, AP 27 %, warm-up **102 ticks (1.7 s)**, cooldown **90 ticks (1.5 s)**, range **36.9 cells**, single shot, accuracy **65 / 80 / 90 / 80 %**, bullet speed 70, stopping power 1.5, market value **255**, mass 3.5 kg, DPS **5.63**. [Bolt-action rifle page, via snippet]

15. **Warm-up is repeated before every burst.** The wiki's 5.63 DPS for the rifle is exactly 18 ÷ (1.7 + 1.5), so its cycle is *aim → fire the burst → cool down → aim again*; warm-up is not a one-off. On the same arithmetic the autopistol is 7.69 DPS and the revolver 6.32 — the autopistol trades three damage and worse accuracy at range for a cycle 0.6 s shorter. [arithmetic on the numbers in 12–14]

16. **What those numbers mean in play** (computed from 2, 3, 5 and 12–14 with the unverified 98 % at level 20; not from a source):

| Shooter, weapon | 3 cells | 12 cells | 25 cells |
|---|---|---|---|
| Level 0 (89 %), autopistol | 56 % | 17 % | 2.2 % (floor is 2) |
| Level 20 (98 %), autopistol | 75 % | 55 % | 24 % |
| Level 0, revolver | 56 % | 19 % | 3.0 % |
| Level 20, revolver | 75 % | 59 % | 33 % |
| Level 0, bolt-action | 46 % | 20 % | 4.9 % |
| Level 20, bolt-action | 61 % | 63 % | 54 % |

The reference's pistol is a close-quarters weapon whatever the skill; the skill's value is that it turns "medium" from hopeless into worthwhile.

**The skill**

17. **Shooting gains 240 XP per shot fired at a hostile pawn, hit or miss** — the roll does not matter to learning. A forum source adds that it is granted per burst, not per bullet, and that shooting inanimate targets (a wall, a dummy) yields only about 6 / 4 / 2 XP by passion (burning / interested / none). Those three do not sit exactly on the ×1.5 / ×1.0 / ×0.35 multipliers the project already uses, so treat them as a player's rounding of a small base value. The 240 is presumably before passion. [Shooting skill page and a forum thread, via snippet]

**Scale**

18. **The reference's cell is treated as about one metre.** Ranges are quoted in tiles, bullet speed is labelled m/s on the same wiki with no conversion, and a pawn of body size 1 fills one cell. So a pistol reaches ~26 m and a rifle ~37 m on a default 250-cell (≈250 m) map — a tenth of the board. (My unverified recollection of the engine's projectile step is `speed ÷ 100` cells per tick, which would make 55 ≈ 33 cells/s rather than 55; either way a 26-cell shot is in the air for well under a second.) [Weapons page, via snippet; the tick conversion is recollection]

19. **The `.9` on every range** (25.9, 36.9) is a design detail worth copying: a target at exactly 26 cells is out of range, and the fraction avoids a tie at an integer distance. [Revolver / Bolt-action pages, via snippet; interpretation mine]

## Recommendation

**The hit formula — three options, ranked:**

1. **The reference's shape, adapted** — `per-cell accuracy ^ distance-in-cells × weapon band accuracy (interpolated) × weather`, 2 % floor, rolled at the trigger (the end of warm-up) and applied when the bullet arrives, with interception at 40 % × body size (4–80 %) for anyone in the flight line. **Back this one.** It is the only shape in which a Shooting skill *means* something a flat melee-style curve cannot: skill buys reach, not just a better coin. It also fits what the project already has — the roll-early-apply-at-impact pattern is our melee's, weather is built (WE), and the day/night cycle can feed the darkness term later.
2. Our melee's flat three-point curve times the weapon's band accuracy. Cheaper, but with it a level-20 pistol at 25 cells is barely better than a level 0, and the skill collapses to a damage-per-second dial.
3. A pure metres formula that ignores cells. No — the sim's distances are cells.

For the skill's curve, only two points are sourced or near-sourced (89 % at 0; 98 % at 20 is recollection). Rather than guess the interior, **use our melee's convention** — three points at 0 / 10 / 20, linear between: 89 % / 96 % / 98 % — and record that the middle point is ours. Traits: none yet, but leave the stat as a per-mille the traits can shift by a skill-level equivalent, since that is how the reference expresses them.

**The pistol numbers:** port the autopistol as the basic pistol exactly — 10 damage, AP 15 %, 18 ticks warm-up, 60 ticks cooldown, one shot per cycle, 80 / 70 / 40 / 30, bullet speed 55 (whatever unit we settle on, it must give a sub-second flight at full range), warm-up repeated every cycle. Keep ±20 % damage as melee has. The revolver (12 / 18 % / 18 / 96 / 80-75-55-40) is the natural second pistol and the bolt-action (18 / 27 % / 102 / 90 / 65-80-90-80, range 37) the first rifle; both are already balanced against the pistol in the reference and cost nothing to carry in the Def.

**Scaling to 2.5 m cells — the two options are tied on the evidence:**

- **Keep the cell counts** (pistol 26 cells = 65 m, bands 3/12/25/40 cells, per-cell 89–98 %): the tuning ports untouched, but a pistol then covers 22 % of our 300 m board against 10 % in the reference, and the medium band starts 62 m out.
- **Keep the metres** (pistol ~10 cells, bands ~1/5/10/16 cells): the board proportion matches the reference (8.7 %), and the per-cell accuracy becomes the per-metre value to the power 2.5 — 0.747 at level 0, 0.951 at level 20 — so the *feel by distance in metres* is preserved exactly.

**The observation that breaks the tie is how long a charging melee attacker is under fire from maximum range**, because that ratio, not the metre or the cell, is what the reference's numbers were balanced against: ~26 cells at a pawn's ~4.6 cells/s is about 5–6 seconds, roughly four pistol cycles, roughly one and a half hits from a level-0 shooter at short-to-medium range. **The cheapest experiment** is one fast-tier Sim test: walk a pawn 26 cells and 10 cells at innate pace over open ground and print the ticks; whichever scaling lands near 300–360 ticks is the one to keep, and if neither does, set the pistol's range in cells to hit that count and derive the bands from it — which is a better answer than porting either number. Provisionally I back **keeping the metres**, because the board proportion is the thing a player will notice first and our cells are 2.5× the reference's.

## Sources

(All reached only as search-engine snippets; every direct fetch was refused by the proxy.)

- https://rimworldwiki.com/wiki/Shooting_Accuracy
- https://rimworldwiki.com/wiki/Weapons
- https://rimworldwiki.com/wiki/Combat
- https://rimworldwiki.com/wiki/Accuracy_(Close) · https://rimworldwiki.com/wiki/Accuracy_(Short) · https://rimworldwiki.com/wiki/Accuracy_(Medium) · https://rimworldwiki.com/wiki/Accuracy_(Long)
- https://rimworldwiki.com/wiki/Autopistol
- https://rimworldwiki.com/wiki/Revolver
- https://rimworldwiki.com/wiki/Bolt-action_rifle
- https://rimworldwiki.com/wiki/Shooting (the Skills page)
- https://rimworldwiki.com/wiki/Cover
- https://steamcommunity.com/app/294100/discussions/0/4694531508751319360/ (XP per shot, per burst; inanimate-target figures)
- https://steamcommunity.com/app/294100/discussions/0/154645214960919663/ (the 0-skill 89 % example, the ±5 trait equivalence)
- https://steamcommunity.com/sharedfiles/filedetails/?id=2127428910 and https://rimworldbase.com/z-levels-mod/ (the layer question)

## Confidence

**Medium for the formula's shape, the bands, the 2 % floor and the three weapons' numbers** (each was quoted verbatim in a snippet and the two arithmetic checks — the 0.99^32 example and the rifle's DPS — come out exact); **low for the skill table beyond level 0, the target-size shape, the weather interior, the miss-cell rule and the bullet-speed unit**, because no page could be read and some of that is recollection flagged as such.

## Could not be determined

- The Shooting Accuracy value at skill levels 1–20 (only level 0 = 89 % is sourced; 98 % at 20 is recollection).
- Whether the size factor is a step (×0.5 below 0.5, ×2 above 2, ×1 between) or a continuous scale, and whether a downed or sleeping target changes hit chance at all (only its role as cover was found).
- The weather values between fog (50 %) and clear (100 %); the exact light threshold for "darkness".
- The autopistol's range and market value (25.9 and the same class as the revolver by recollection only), and its burst count (single shot by recollection).
- Whether accuracy beyond 40 cells holds the long value.
- Where a missed bullet lands and how that radius scales with distance.
- The unit of bullet speed (the wiki labels it m/s; the engine's cells-per-tick conversion is recollection).
- Whether the 240 XP per shot is before or after the passion multiplier.

## Layer question 5: can you shoot up and down?

The reference is flat, so nothing in it prices a shot between heights. The only things found bear on it sideways: the community's original multi-level mod (now dead) let pawns shoot from a higher floor at a lower one and build guard towers on that basis, while listing "shooting between layers" as still planned at one point — so it was never a settled mechanic even there — and its maintained successor states outright that cross-level combat will *not* be implemented, citing performance and compatibility. So there is no reference number for a height advantage or a vertical range penalty; the owner's "own layer only" rule is not a departure from anything, and whatever is done later will be ours to define and measure.
