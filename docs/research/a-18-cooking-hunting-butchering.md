# a-18 — Cooking, hunting and butchering

## Question

How does the reference game carry food from a live animal to a colonist's stomach, in numbers?
Six topics: (1) hunting — the designation, who takes it, the weapon rule, the approach, retaliation,
a downed hunter; (2) butchering — where, work, the yield formula, leather, rot; (3) cooking — the
simple meal, the three stations and what they burn, cook speed and its skill curve, the poisoning
curve (which Odyssey replaces with a *burnt meal*), meal tiers; (4) bills — modes, radius,
pause/unpause, who takes one, how ingredients are fetched; (5) eating — preference order, the
raw-food and no-table thoughts, finding a table and chair, carrying the food there; (6) nutrition
numbers.

Clean room: mechanics, formulas and intent from the public wiki and community discussion,
paraphrased. No Def XML, no decompiled code, and the reference's own labels for thoughts are
described rather than reused. Cap: 12 searches and 12 page reads; **used 12 and 12** (one read a
404).

**Already established elsewhere and not repeated here:**
`a-08-plants-growing-food.md` §4–§5 — 1.6 nutrition/day, the 0–1 need and its hunger bands, the
72.5-hour starvation clock, meals 0.9, raw crops 0.05/unit, the simple meal as 0.5 raw → 0.9
(180 %), 300 ticks at a stove and 600 at a campfire, meals rot in 4 days, raw meat in 2, corpses in
2.5, and freezing stops rot. `a-14-bills-stockpiles-inventory.md` §5 — the bill record, the three
repeat modes, pause-when-satisfied and the unpause threshold, the top-down bill scan and its skip
reasons, *do until you have X* counting stored items only, and the product-destination choice.
`a-07-power-and-networks.md` §4 — the electric stove's 350 W, 3 × 1 footprint and 3 heat/s.

## Findings

### 1. Hunting

| Point | Reference behaviour |
|---|---|
| Designation | A per-animal **hunt** order placed on a wild animal; it stays until the animal is dead. |
| Who | Colonists with the **Hunting** work type enabled, once every higher-priority job is done — an ordinary work-giver, not a draft. |
| Weapon | **A ranged weapon is required.** An unarmed or melee-armed colonist never takes a hunt job. The stated reason: **an animal always fights back against a melee attacker, even a species whose revenge chance is 0 %**, so melee hunting is a fight, not a hunt. |
| Approach | The hunter **shoots from its weapon's maximum range**, explicitly to buy time if the animal turns. Once the animal is downed, the hunter walks up and **finishes it at point-blank range with a throat cut** (no further shots). |
| After the kill | If a stockpile accepts the corpse and has room, **the hunter carries it home** before taking the next hunt. Cooks butcher it later. |
| Retaliation | Each time a hunted animal is **injured**, a revenge roll is made; success turns it manhunter (it attacks nearby people). When one turns, **every animal of the same species within 25 tiles** that can path to the attacker may turn too. The per-hit chance is the species' *revenge chance* × the hunter's **stealth factor**, and community testing says it is **about three times higher at close range**. With no meaningful stealth the effective chance is quoted at about **20 %**; the reference's wild boar page lists **0 %** (the page may be the domestic boar's; see *Could not be determined*). |
| Hunting stealth | Shooting 5 %/level + Animals 5 %/level, then a curve through (0 → 0), (0.1 → 0.5), (0.2 → 0.75), (1.0 → 0.9) — **capped at 90 %**. So a 5/5 hunter is already at ~90 % of the cap. |
| Downed hunter | **Not documented.** |

The design intent is visible: hunting is *meant* to be the safe way to fight an animal, and the
safety comes from range and skill, not from the animal's temperament. Melee takes both levers away.

### 2. Butchering

| Point | Reference value |
|---|---|
| Where | A **butcher spot** (free, instant, outdoors or in) or a **butcher table**. The corpse is **carried to the station**; there is no in-place butchery. |
| Work | **450 ticks** per corpse, divided by the butcher's **butchery speed**; a table's speed is raised **+6 % per adjacent tool cabinet, up to two**. |
| Butchery speed | Cooking skill: 40 % at level 0, **+6 %/level**, so 100 % at level 10 — the same shape as cooking speed below. × global work speed. |
| Station efficiency | **Spot 70 %**, table 100 %. The spot's only other difference is no cleanliness penalty. |
| Butchery efficiency | Cooking skill: **75 % + 2.5 %/level**; manipulation 90 % weight, sight 40 % weight (capped at 100 %); **capped at 150 %**. Level 0 75 %, 5 87.5 %, **10 100 %**, 14 110 %, 20 125 %. Applies to **meat and leather alike**. |
| Meat amount | A stat with **base 140**, × body size. Check: the wild boar is body size 0.85 → **119 meat**, which is exactly the page's figure. |
| Yield formula | `meat = 140 × bodySize × butcheryEfficiency × stationEfficiency × (0.66 if the corpse is damaged) × (fraction of body parts remaining) × maturity factor` |
| Leather | Per species (wild boar **36**), scaled by the same efficiency factors. |
| Rot | **Only a fresh corpse can be butchered.** A rotten one is refused outright (and rotten corpses are inedible to anyone). Corpses start to rot at 2.5 days (`a-08` §5). |
| Mood | Butchering a *person* costs everyone −6 for 6 days and the butcher a further −6; animals cost nothing. |

Worked for the boar (undamaged, adult, 100 % manipulation):

| Cooking level | Efficiency | At a table | At a spot (×0.7) |
|---|---|---|---|
| 0 | 75 % | 89 | 62 |
| 5 | 87.5 % | 104 | 73 |
| 10 | 100 % | 119 | 83 |
| 14 | 110 % | 131 | 92 |
| 20 | 125 % | 149 | 104 |

Raw meat: **0.05 nutrition/unit**, stack 75, 2 days to rot, 2 % poisoning if eaten raw, and eating
it raw costs the same −7 for a day as any raw food. So one boar at a spot by an average butcher is
83 × 0.05 = **4.2 nutrition**: 8 simple meals, or about 4.7 colonist-days.

### 3. Cooking

**Meal tiers** (all **0.9 nutrition**):

| Tier | Ingredients (nutrition) | Work | Min. Cooking | Mood for a day | Stations |
|---|---|---|---|---|---|
| Simple | 0.5 of **any** raw food | 300 ticks | none | none | campfire, fuelled stove, electric stove |
| Fine | 0.25 vegetable + 0.25 meat/animal product | 450 | 6 | +5 | stoves only |
| Lavish | 0.5 vegetable + 0.5 meat/animal product | 800 | 8 | +12 | stoves only |

**Stations:**

| Station | Speed | What it burns | Notes |
|---|---|---|---|
| Campfire | **half** (simple meal 600 ticks) | **10 wood/day, all the time it is lit**, whether or not anyone cooks; 46/day if unroofed in rain or snow | Simple meals only (plus two preserved goods). Heats and lights; is a default gathering spot. |
| Fuelled stove | full | wood, **50** capacity, **160 wood per day of burning — only while cooking** | 3 × 1, 80 steel. One simple meal (300 ticks = 1/200 day) burns **0.8 wood**. |
| Electric stove | full | **350 W**; no idle draw is listed | 3 × 1, 80 steel + 2 components, 2,000 ticks to build, Construction 4, needs the Electricity project. |

The community's own reading of that table: the campfire is *wood-efficient* for a colony that cooks
a lot in a short spell (10 wood feeds many meals), and the stove is efficient for a colony that
cooks little.

**Cooking speed** (governs meal work): a score = Cooking level (+ manipulation and sight
offsets), then a curve — `40 % + 1.5 %/point` below zero (floor 10 %), `40 % + 6 %/point` from 0 to
20, capped at 160 %; × global work speed.

| Cooking level | 0 | 5 | 8 | 10 | 14 | 20 |
|---|---|---|---|---|---|---|
| Cooking speed | 40 % | 70 % | 88 % | **100 %** | 124 % | 160 % |
| Simple meal, stove (ticks) | 750 | 429 | 341 | 300 | 242 | 188 |
| Simple meal, campfire (ticks) | 1,500 | 857 | 682 | 600 | 484 | 375 |

**Poisoning by the cook's skill** — the curve Odyssey reshapes into a burnt meal:

| Level | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9–20 |
|---|---|---|---|---|---|---|---|---|---|---|
| Chance | 5 % | 4 % | 3 % | 2 % | 1.5 % | 1 % | 0.5 % | 0.25 % | 0.15 % | **0.1 % floor** |

Its shape: steep and **linear for the first three levels** (−1 point per level), then halving
roughly every level, then a floor it **never leaves** — even a master cook poisons one meal in a
thousand. A **second, independent roll** comes from the kitchen: `(cleanliness + 2) × 0.05 / 3`,
clamped 0–5 %, which is zero at cleanliness above −2. The consequence is severe: **24 hours**, the
middle 16 of them at +40 % pain and consciousness halved. Raw food carries its own chance
(corpses 5 %, most raw food ~2 %, range 1–4 %).

### 4. Bills (beyond `a-14` §5)

- **Default ingredient radius is 999 — the whole map.** The community reports it as the value a new
  bill is created with, and the complaint that follows from it: a cook walks to a field for three
  berries while a freezer sits beside the stove, and a mod exists solely to change the default.
- **Which ingredients are taken within the radius is not nearest-first** in players' experience
  ("randomly from places in range"); fresh food is used while food about to rot sits beside it, and
  nothing prefers the spoiling stack. That is a standing complaint, not a design.
- **Who**: any colonist with the bench's work type enabled (Cooking for stoves and butchery), subject
  to the bill's worker restriction and skill range; bills are scanned top-down (`a-14`).
- **Fetching**: the cook walks to the ingredients and back to the bench, which players describe as
  **often several trips** when the ingredients are spread out. The exact carry rule (one kind per
  trip, topping up from nearby stacks of the same kind) was not confirmed within the cap.
- **Butchery bills** are the canonical *forever* bill (`a-14`); a corpse is an ingredient like any
  other and is fetched to the spot or table.

### 5. Eating

- **Preference**: a colonist eats **the best food it may eat**, lavish down to raw; **quality
  dominates distance** — a fine meal at the map edge beats a simple meal next door — but distance
  does enter a single optimality score, so two options of near-equal mood cost (paste and raw
  food, for instance) are chosen by nearness.
- **Starving**: a **malnourished** colonist (food need at 0) abandons preference and eats anything
  permitted — meals, raw food, even a fresh corpse. **Rotten corpses are never edible.**
- **Thoughts** (one day each unless noted):

| Eaten | Mood |
|---|---|
| Raw food (vegetable or meat alike) | **−7** |
| No table | **−3** |
| Fine meal | +5 |
| Lavish meal | +12 |
| Raw human meat / cooked | −20 / −15 (inverted for the cannibal trait) |
| Paste | −4 |

- **Finding a table**: at the instant it decides to eat, a colonist uses a table **only if it is
  within 31 tiles of a table-adjacent chair**; otherwise it eats where it stands and takes the −3.
  **A chair is required** — a table with no seat beside it is not a place to eat. The diner **sits
  on the chair cell**; a table seats as many as chairs fit round it. Table sizes: 1 × 2, 2 × 2,
  2 × 4, 3 × 3. Tables and campfires are default gathering spots.
- **Carrying**: the meal is picked up and eaten at the table, which the table rule implies; no page
  states the carry explicitly.

### 6. Nutrition numbers

| Quantity | Reference | In Odyssey units (1.0 = 1,000; 900 = one meal) |
|---|---|---|
| Daily burn, adult | 1.6 | **1,600 / day** |
| Any meal (simple, fine, lavish, ration) | 0.9 | 900 |
| Raw meat, per unit | 0.05 | **50** |
| Raw vegetable, per unit | 0.05 | 50 |
| Simple meal input | 0.5 (10 units) | 500 (10 units) |
| Wild boar, whole | 119 meat = 5.95 | 5,950: 6.6 meals' nutrition eaten raw, or 11 meals cooked |

## Recommendation

Against the owner's decisions, in the order the food moves.

1. **Hunting takes the reference's structure and changes one rule on purpose.** A `Hunting` work
   type, a per-animal hunt designation that persists until death, the hunter carrying nothing home
   (see 2). The reference demands a *ranged* weapon; Odyssey's is **any equipped weapon**, and the
   reference itself says what follows: **a hog struck in melee always fights back.** Adopt that
   rule outright rather than inventing a revenge chance — it makes every hunt a short fight through
   the combat system that already exists (`33-combat.md`), and the hog's own attack is the price of
   meat. Keep a revenge-chance hook on `SpeciesDef` at 0 for later ranged weapons, where the
   reference's range-and-stealth model applies. **A downed hunter**: the reference is silent, so
   follow design 33's standing rule that an unordered fight ends in downs — the hog disengages and
   returns to wandering, the designation stays, and the next hunter takes it. The approach is
   walk-to-adjacent then attack; the finishing blow on a downed hog is the ordinary attack, not a
   separate throat-cut job.

2. **Butcher where it fell, at the spot's efficiency.** No station and no carry: a
   `Butcher` job on the carcass cell, **450 ticks ÷ butchery speed**, yield
   `140 × bodySize × efficiency × 0.7` (the spot's 70 % is the right analogue for field work),
   ×0.66 if the body is damaged — which after a melee kill it always is, so either drop that
   factor or accept it as the price of melee; **recommend dropping it**, because in Odyssey every
   hunt is melee and the factor would be a constant. Refuse a rotten carcass (rot at 2.5 days, as
   `a-08`). Take the hog at **body size 0.85**: at level 1 (77.5 %) that is **64 meat**, at level 10
   **83**, each unit 50 need. The meat drops as ordinary stacks on the cell and is hauled by the
   existing storage rules. Leather is a hook, not a commodity yet.

3. **One recipe: 10 units of any raw food → one meal (500 → 900).** It is the reference's simple
   meal exactly, and it already takes "any raw", so meat and carrots mix. **300 ticks at the
   electric cooker, 600 at the campfire**, each ÷ cooking speed. Reuse the project's rebased skill
   curve seam (`17-rates-and-stats.md` §3b) rather than the reference's 40 %-at-0 curve verbatim,
   since ours start at level 1.

4. **Burnt meal, falling to 0 by level 14.** The reference's curve is steep-then-halving with a
   floor that never reaches zero; the owner wants a zero. A burnt meal is far milder than 24 hours
   of poisoning, so it can afford to be commoner at the bottom and still be fair. Recommended
   per-mille table (piecewise linear, integer, the reference's shape stretched to end at 14):

   | Level | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14+ |
   |---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
   | Burnt ‰ | 150 | 120 | 95 | 75 | 60 | 47 | 36 | 27 | 19 | 13 | 8 | 5 | 3 | 1 | **0** |

   A burnt meal keeps its **900** nutrition and costs **−4 mood for a day** — worse than a ration
   (no thought), better than raw (−7), which is exactly the owner's order. If a new colonist
   burning one meal in eight reads as punishing, the tie-breaker is one play session at level 1
   with a single cook; the cheaper alternative curve is the reference's own ×3 (15 % at 0, 0.3 %
   at 9) truncated to 0 at 14. Drop the kitchen-cleanliness roll until rooms carry cleanliness.

5. **The two cookers.** Electric cooker **350 W, drawn only while a cook is working it** — on the
   1,000 W generator that burns in proportion to load (`a-07`, design 32) a meal costs about
   0.04 wood, which is the point of power. It cooks only when its net is lit; a net going dark
   mid-meal pauses the job rather than losing ingredients. **The campfire cooks at half speed** and
   keeps whatever fuel rule the temperature campfire already has; if it has none, the reference's
   10 wood/day while lit is the number, and "lit whether or not anyone cooks" is what makes it the
   worse choice once power exists.

6. **Bills on the cooker pane: until / times / forever**, top-down, as `a-14`. *Until X* counts
   **stored** meals only. **Default radius unlimited but path-priced** — choose the ingredient by
   path cost from the cooker (nearest first, with a spoil-soonest tie-break), which fixes the
   reference's two loudest bill complaints at no cost, rather than its unordered pick within a
   radius. Default the until-mode unpause threshold to target − 1 (the reference's default was not
   found). Fetch **one kind per trip**, topping up from nearby stacks of that kind until the recipe
   has its 500; a meal of mixed carrot and meat is two trips.

7. **Eating.** Tiers strictly **meal > ration > burnt > raw vegetable**, and within a tier the
   nearest by path. **Raw meat only when the food need is at 0** — the reference's malnourished
   rule, narrowed to one commodity. A table (1 × 2) counts only with a chair beside it; the
   colonist carries the food to a free chair **within 31 cells of path** when it decides to eat,
   sits on the chair cell facing the table, and otherwise eats where it stands with **−3 for a
   day**. Raw food of either kind costs **−7 for a day**. Note that 31 reference tiles is 31 of our
   2.5 m cells, 77.5 m: if that feels too far in play, the constant is the one to move.

## Sources

- https://rimworldwiki.com/wiki/Hunt
- https://rimworldwiki.com/wiki/Hunting_Stealth
- https://rimworldwiki.com/wiki/Wild_boar
- https://rimworldwiki.com/wiki/Butcher_spot
- https://rimworldwiki.com/wiki/Butchery_Efficiency
- https://rimworldwiki.com/wiki/Butchery_Speed (search extract)
- https://rimworldwiki.com/wiki/Butcher_table (search extract)
- https://rimworldwiki.com/wiki/Meat
- https://rimworldwiki.com/wiki/Meat_Amount (search extract)
- https://rimworldwiki.com/wiki/Food_poisoning
- https://rimworldwiki.com/wiki/Cooking_Speed
- https://rimworldwiki.com/wiki/Electric_stove
- https://rimworldwiki.com/wiki/Fueled_stove
- https://rimworldwiki.com/wiki/Campfire (search extract)
- https://rimworldwiki.com/wiki/Simple_meal (search extract)
- https://rimworldwiki.com/wiki/Fine_meal (search extract)
- https://rimworldwiki.com/wiki/Lavish_meal (search extract)
- https://rimworldwiki.com/wiki/Table
- https://rimworldwiki.com/wiki/Raw_food (search extract)
- https://rimworldwiki.com/wiki/Human_meat (search extract)
- https://rimworldwiki.com/wiki/Malnutrition (search extract)
- https://rimworldwiki.com/wiki/Food (search extract)
- https://steamcommunity.com/app/294100/discussions/0/3113646913574288250/ (revenge ×3 at close range, ~20 % without stealth)
- https://steamcommunity.com/app/294100/discussions/0/2265817017316532080 (preference versus distance)
- https://github.com/emipa606/DefaultIngredientRadius (default radius 999)
- https://steamcommunity.com/sharedfiles/filedetails/?id=2909706985 (ingredient-radius complaints)

## Confidence

- **Hunting — medium.** The ranged-only rule, the melee-always-retaliates rule, max-range shooting,
  the finishing cut, the corpse haul and the 25-tile herd spread are the wiki's own words (high).
  The per-hit revenge arithmetic (×3 close, ~20 % base) is community testing (low); the boar's 0 %
  may be a different animal's page (low); the downed-hunter case is undocumented.
- **Butchering — high** on 450 ticks, spot 70 %, the efficiency curve and cap, base 140 × body size
  (checked against 119), fresh-only. **Medium** on the 0.66 damaged factor (one page's wording) and
  on butchery speed (search extract only).
- **Cooking — high** on meal tiers, work, skill gates, moods, the poisoning table, the cooking-speed
  curve, the stove figures and the campfire's 10 wood/day and half speed.
- **Bills — medium.** Radius 999 is well attested by community sources; ingredient choice and trip
  count are players' descriptions, not rules.
- **Eating — high** on −7, −3, the 31-tile rule, chairs required and the malnourished override;
  **medium** on quality-over-distance (community, consistent across threads) and on carrying the
  meal to the table (implied, not stated).
- **Nutrition — high** (and consistent with `a-08`).

## Could not be determined

- What the reference does when a hunter is downed mid-hunt — whether the animal keeps attacking a
  downed hunter, and whether the job is released or retried.
- The exact revenge formula (how stealth and range combine per hit) and the wild boar's true
  revenge chance; the fetched page may be the domestic boar.
- Whether butchery work scales with body size (only the flat 450 ticks was found), and the
  maturity factor's curve.
- The fuelled stove's idle draw and the electric stove's idle draw (none listed; `a-07` agrees).
- The default unpause threshold for *do until you have X*, and the precise ingredient-carry rule
  (one kind per trip or several kinds in one).
- The food-optimality score's exact distance weighting; only its existence and that quality
  usually dominates were found. A thought for eating rotten food was not found.
