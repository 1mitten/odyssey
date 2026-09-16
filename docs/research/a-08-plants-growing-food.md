# Lane A8 — Plants, growing and food

## Question

How do RimWorld's plants, growing zones and food economy work — growth by light, temperature and fertility; sowing and harvest work; nutrition, hunger and spoilage; hydroponics — and, for the two numbers the code needs now, **what should a felled tree yield and how long should felling take** (today `Job_Fell.workTicks = 600` and `WoodPerTree = 20`, both marked ASSUMED in `Assets/Odyssey/Sim/Pawns/PawnContent.cs`), and **what does a ten-day run's food economy need at minimum** (the scenario starts with 240 meals and nothing in the slice makes food, OQ-39)? Clean-room: mechanics, numbers and design intent from the public wiki, paraphrased; no Def XML, no decompiled source, no names or flavour text.

Cap for this row: 15 page reads or 12 searches. Used: 15 reads (two of them 404s), 1 search.

## Findings

### 1. Growth is a product of three factors, on a thirteen-hour day

The wiki states plant growth per tick as the product of three growth-rate factors (GRF), each in 0..1, applied to the plant's base grow time:

- **Fertility.** `GRF(F) = (fertility − 1) × fertilitySensitivity + 1`, with fertility as a fraction of standard soil. Each species carries a *fertility sensitivity* (0–100%) that says how much it cares; food crops are typically 100%, trees 50%, some cacti and decoratives 0%. Each species also has a **minimum fertility** below which it will not be sown at all (rice 70%, oak and pine 70%; the general Plants page gives 50% for most crops and 30% for trees, so treat the per-species page as authoritative and the general statement as a rough guide).
- **Temperature.** Full rate from 6 °C to 42 °C; below 6 °C `GRF(T) = T / 6`; above 42 °C `GRF(T) = (58 − T) / 16`; zero growth outside 0–58 °C; plants die below about −10 °C (species thresholds between −18 °C and −10 °C). Trees shed their leaves at −2 °C (sturdier species at −10 °C).
- **Light.** `GRF(L) = (light − growMinGlow) / (growOptimalGlow − growMinGlow)`, with a minimum light of **51%** for crops and trees, 30% for decoratives, optimal at 100%. A fungus class needs 0% light and is killed by any non-fungal light.

Plants **rest from hour 19 to hour 5**, so of the 60,000 ticks in a day only 32,500 (13/24) are growing ticks. The wiki gives the resulting rule of thumb: *real growth days ≈ 1.846 × the listed grow days* at 100% fertility, temperature and light. Rice at "3 days" therefore takes 5.54 days; an oak at "30 days" takes 55.4.

A plant that is never harvested dies of old age at `growDays × lifespanFraction`, where the fraction is 5 for most species (up to 8 for some).

**Soil fertility ladder** (fractions of standard soil): sand 10%, stony soil 70%, standard soil 100%, rich soil 140%, hydroponics basin **280%**.

### 2. Sowing and harvest

Each species carries two work amounts and a minimum skill:

| Species | Grow days (real) | Sow work | Harvest work | Yield | Min fertility | Fertility sensitivity |
|---|---|---|---|---|---|---|
| Rice | 3 (5.54) | 170 ticks | 200 ticks | 6 | 70% | 100% |
| Pine | 20 (36.9) | 4,000 ticks | **800 ticks** | **27 wood** | 70% | 50% |
| Oak | 30 (55.4) | 4,000 ticks | **1,400 ticks** | **46 wood** | 70% | 50% |

Other tree yields from the same tables: birch, maple, poplar 27; teak and cypress 60; bamboo 10 (12 grow days); saguaro 15; a **stump gives 4** wood. Trees have 200 HP and 80% flammability.

Harvest yield is the base yield times: growth fraction (a crop cut at its minimum harvestable growth of 65% gives half; the tree page says a tree cut at **40% growth gives 50% of its yield**), times the plant's own health fraction if damaged, times the harvesting pawn's *plant harvest yield* stat, times a storyteller difficulty factor. Low skill also carries a chance to **waste the harvest outright**.

All of this work is scaled by the **plant work speed** stat: base 8% plus 11.5% per Plants level, floored at 10%, multiplied by manipulation (uncapped), sight (30% weight, capped at 100%) and global work speed. So level 0 works at 10%, level 5 at 65.5%, **level 8 at 100%**, level 10 at 123%, level 20 at 238%. The stat page states explicitly that it governs *sowing, harvesting, cutting plants and felling trees*.

### 3. What felling a tree should cost — the replacement for the two ASSUMED numbers

The wiki lists one work figure per tree, "work to harvest", and the plant-work-speed stat is stated to cover felling; no tree page lists a separate "cut" work. The reading that fits both facts is that **felling consumes the species' harvest-work figure, scaled by plant work speed** (medium confidence — inferred from the stat's stated scope, not from a sentence saying "felling costs the harvest work").

Translated to our units (we run the same 60 ticks/s and 2,500 ticks/hour as RimWorld, per `a-15-time-and-simulation.md`):

| | Base work | At Plants 0 (10%) | At Plants 5 (65.5%) | At Plants 8 (100%) | Yield |
|---|---|---|---|---|---|
| Pine-class tree | 800 ticks (0.32 h) | 8,000 ticks (3.2 h) | 1,221 ticks (0.49 h) | 800 ticks | 27 |
| Oak-class tree | 1,400 ticks (0.56 h) | 14,000 ticks (5.6 h) | 2,137 ticks (0.85 h) | 1,400 ticks | 46 |

Two design intents are visible in those numbers. Felling is **cheap in labour and expensive in time-to-regrow**: 27 wood for 13 seconds of real work, but 37 real days to grow the replacement. And wood stacks to **75** (mass 0.4 kg/unit, market value 1.2, deterioration 0.5 HP/day — "ten years to rot away"), so one pine-class tree is comfortably one stack and one haul, which is the property the current code relies on.

Wood is sunk into fuel as well as building: the fuelled stove burns up to 160 wood/day, so a colony that cooks on wood needs a standing supply, not a one-off felling.

### 4. Hunger, nutrition and what a colonist eats

- An adult human burns **1.6 nutrition per day**, flat — the wiki gives one rate, not a per-band rate. The food need holds a maximum of **1.0**.
- Thresholds: fed above 25%; **hungry** 12.5–25% (−6 mood); **ravenously hungry** 0–12.5% (−12); **malnourished** at 0 (−20 and a rising malnutrition condition). Pawns go looking for food at about **30%**.
- From full, a human is fed for 11.25 h, hungry for 3.75 h, ravenous for 7.5 h, then malnourished for **50 h until death** — **72.5 hours** from full to dead, about three days.
- Nutrition per item: **simple, fine and lavish meals all 0.9**; packaged survival meal 0.9; nutrient paste 0.9; **raw crops and pemmican 0.05 per unit**. So a colonist needs **1.78 meals per day**, or 32 units of raw food.
- A simple meal takes **0.5 nutrition of any raw food** (10 rice) and yields 0.9 — the wiki calls this **180% efficiency**; paste is 300%. Cooking work is **300 ticks at a stove, 600 at a campfire**, no minimum skill; a bulk bill makes four meals from 2.0 nutrition in proportionally longer. Eating raw food is allowed but carries a mood penalty (the Food page; magnitude not fetched).

### 5. Spoilage

- Days to start rot at room temperature: **meals 4**, raw meat 2, pemmican 70, **packaged survival meals never**; the wiki's growing guide says crops last **40 days** unfrozen, and organic corpses rot after 2.5 days.
- Temperature: rot runs at full speed above 10 °C, is slowed between 0 °C and 10 °C (the Food page states the factor as "1/temp in Celsius"; the wiki page that would state the exact curve, `Rot`/`Spoilage`, does not exist under those names, so the exact shape is unverified), and **stops entirely at or below 0 °C** — a freezer preserves food indefinitely with no penalty on eating.
- Rot progress is per stack and is averaged on merge (`a-14-bills-stockpiles-inventory.md` §4). A cooler set below 0 °C is the standard answer; refrigeration at 1–9 °C only slows it.

The design intent: **meals are a four-day buffer, not a store.** RimWorld's economy forces a choice between a freezer (power) and a steady cook (labour), and the only long-life food is either dried (pemmican, a bill) or packaged (a starting or traded good).

### 6. Hydroponics and artificial light

- A hydroponics basin is **1 × 4 cells**, 280% fertility, **70 W day and night**, 100 steel + 1 component, 2,800 ticks of construction at skill 4, −3 cleanliness, and the plants die if power fails. Grows rice, potatoes, strawberries, cotton, hemp and others; **not** corn, haygrass or the mushroom crop.
- A sun lamp supplies full light in its radius at **2,900 W for the 13 growing hours** and is the wiki's stated requirement for indoor growing; one lamp covers up to 24 basins.
- Rice in a basin: 280% fertility × 100% sensitivity gives 2.8× growth, so 1.98 real days per harvest.

### 7. What the ten-day run needs — the arithmetic

A note on one figure: the tool's extract of the rice page reported "0.175 nutrition per unit" and "1.08 per tile per day". Both contradict the Food page (0.05 per raw unit) and the Plants page arithmetic, and the page's own comparative claim ("rice is 105.4% of potatoes") only comes out with the derived numbers below, so the derived numbers are what this file uses.

**Per tile per day, at 100% fertility, temperature and light:** rice yields 6 × 0.05 = 0.3 nutrition every 5.54 real days = **0.054 nutrition/day**. In a hydroponics basin cell: 0.3 / 1.98 = **0.152/day**, so a four-cell basin is 0.61/day. (Potatoes by the same method: 11 × 0.05 over 5.8 × 1.846 = 10.7 days = 0.051/day; rice/potato = 105.4%, matching the wiki's stated ratio, which is the check.)

**Per colonist per day:** 1.6 nutrition eaten as meals is 1.78 meals, which is 0.89 nutrition of raw food (18 rice). Dividing by 0.054 gives **16.5 soil tiles of rice per colonist**, or **1.5 basins**. Five colonists need about **83 tiles** — a 9 × 9 field — plus roughly 45 minutes of a cook's day (5 × 1.78 × 300 ticks = 2,670 ticks, plus fetching) and, in soil, nothing at all comes in until day 6.

**What the current scenario does:** measured burn is 15 meals/day for five colonists, 3 per head, against RimWorld's 1.78 — because the starting `Item_Meal` restores 450 need units where the need itself falls at RimWorld's rate (the top band's 4 units per 150-tick interval is 1,600 units/day, which is exactly 1.6 nutrition/day if 1,000 units is 1.0). **The meal is half a RimWorld meal.** Setting `nutrition = 900` brings the burn to the vanilla 1.8 per head and ten days for five colonists to about **90 meals**; at the current 450 it stays at about 155 (the run's own measurement) and 240 is the right pantry. The other divergence, per-band fall rates on the food need where RimWorld's is flat, is small in effect and not worth changing on this evidence.

**Spoilage would break the pantry as designed:** 240 RimWorld meals rot on day 4. The starting food is labelled a *ration pack*, which is the packaged-survival-meal class, and that class **never rots** — so the scenario is internally consistent provided ration packs are declared rot-free, and any cooked meal added later is the thing that spoils.

## Recommendation

1. **Replace the two ASSUMED numbers with `workTicks = 800` and `WoodPerTree = 27`** — the pine/birch/poplar class, which is the modal vanilla tree and the wooded meadow's only species. State them as *base work at plant work speed 100%* and *base yield at full growth*; when OQ-14 lands skills, the felling driver multiplies work by the plant-work-speed curve (8% + 11.5%/level, floor 10%) and yield by a harvest-yield factor. One stack (75) still clears the tree in one haul. If a second species is ever drawn, oak-class is 1,400 / 46 and both fit the same two Def fields. Medium confidence on the 800, high on the 27.
2. **Make the ration pack worth 0.9 nutrition (900 units)** so the food economy is measured in vanilla units, and **declare it rot-free**, which the survival-meal precedent supports. Then re-measure the ten-day burn (expect ≈ 90 meals for five) and shrink the pantry to 120 with the surplus as the buffer, or keep 240 and say so.
3. **The minimum food economy for a self-feeding colony is the number to design M3 around:** 16.5 soil tiles or 1.5 four-cell basins of rice-class crop per colonist, a cooking station (campfire at 600 ticks per meal until a stove exists), and a six-day pantry to bridge the first sowing. The Farm pack's 2 × 2-cell field tiles (`e-03-other-packs.md`) make a 9 × 9 field a 5 × 5 stamp, which is a sensible growing-zone unit.
4. **Do not put growing in the ten-day soak.** That run proves stability; a food economy proof is a separate scenario with the field sown at tick 0 and the assertion "no colonist is hungry after day 6".

## 3D/layer impact

- **Light is the growing constraint in a layered city, not soil.** Crops need 51% light; nothing below an intact slab has it. So growing is rooftop, street-level clearing or artificial light, and the sun lamp's 2,900 W for 13 hours is the price of an underground farm — which is the reason `03-systems-catalogue.md` gives lower layers to want power. Soil is the second constraint: the city's ground is rubble and concrete (Q11), so rooftop soil must be *placed* (hauled soil or planters), which is a building, not terrain.
- **Fertility is a per-cell, per-layer property** and the growth formula wants three per-cell inputs (fertility, temperature, light). The zone answer from `a-14` (per layer, Q6) holds: a growing zone spanning layers would have no single light or fertility value.
- **Temperature per room (Q3)** feeds both growth (6–42 °C full rate) and rot (0 °C stops it). A deep cell is cool, and a sealed sub-level with no heating is the ruined city's natural cold store; that is a genuine reward for occupying the underground, on the same footing as the deterioration reward in `a-14`.
- **The trees on the meadow are the only renewable wood**, and they take 37 real days to regrow, so a 10-day slice never sees regrowth; felling in the slice is mining with a different animation, and the tree count on the map is the wood budget.

## Ruined-city impact

- A ruin has no arable soil at ground level and abundant flat roofs. **Rooftop growing on hauled soil** and **hydroponics under a working power tap** are the two paths, and both are gated on things the city map already makes scarce (soil, power), which is the right shape.
- Salvaged packaged food (the ration-pack class, indefinite life) is the ruin's natural early food, and its being rot-free is what lets a stockpile of it be a real objective rather than a four-day clock.
- Spoilage plus per-room temperature gives a reason to seal and cool a room before a freezer exists.

## Layer questions touched

- **Q4 (light to lower layers):** directly — 51% light is a hard floor on growing; skylights, shafts and broken slabs decide where a farm can be without a sun lamp.
- **Q3 (heat rise, rooms):** directly — growth and rot both read room temperature; cold sub-levels are the cold store.
- **Q6 (zones per layer):** confirms `a-14`'s per-layer answer from the growing side.
- **Q11 (digging in a city):** growing needs soil the city does not have; soil becomes a hauled material.
- **Q12 (what the slice proves):** the slice needs felling (numbers above), a food need with a rot-free ration, and can stub growing, cooking and spoilage; M3 needs one crop, one cooking station and rot on cooked meals.

## Sources

- https://rimworldwiki.com/wiki/Plants
- https://rimworldwiki.com/wiki/Tree
- https://rimworldwiki.com/wiki/Pine_tree
- https://rimworldwiki.com/wiki/Oak_tree
- https://rimworldwiki.com/wiki/Wood
- https://rimworldwiki.com/wiki/Plant_Work_Speed
- https://rimworldwiki.com/wiki/Plants_(skill)
- https://rimworldwiki.com/wiki/Rice_plant
- https://rimworldwiki.com/wiki/Food
- https://rimworldwiki.com/wiki/Saturation
- https://rimworldwiki.com/wiki/Simple_meal
- https://rimworldwiki.com/wiki/Hydroponics_basin
- https://rimworldwiki.com/wiki/Days_To_Start_Rot
- https://rimworldwiki.com/wiki/Temperature (search extract only)

## Confidence

**High** for: the three-factor growth formula and its constants; the 19:00–05:00 rest and the 1.846 real-days multiplier; the fertility ladder; per-species grow days, sow work, harvest work and yields for rice, pine and oak; tree yields by species and the stump's 4; wood's stack limit, mass and deterioration; the 1.6 nutrition/day rate, the 1.0 capacity, the hunger thresholds and the 72.5-hour starvation timeline; meal nutrition values and the 0.5-in/0.9-out simple meal; cooking work at stove and campfire; days-to-rot for meals, meat, pemmican and survival meals; freezing stopping rot; hydroponics basin and sun lamp figures; the plant-work-speed curve and its stated scope.

**Medium** for: felling consuming the species' harvest-work figure (inferred from the stat page's scope, not stated per tree); the per-tile nutrition rates in §7 (derived, and checked against the wiki's own rice/potato ratio); the 0–10 °C rot curve's exact shape; the "40% growth gives 50% yield" rule for trees being the general partial-growth curve rather than a floor.

**Low** for: the plant harvest yield stat's skill curve (only a "base factor 0.6" was extracted) and the harvest-waste chance by level.

The recommendations are ours, derived from the numbers above and the code in `PawnContent.cs` and `ColonyScenario.cs`.

## Could not be determined

- Whether felling has its own work figure distinct from harvest work, and whether felling a tree below 40% growth is allowed at all or simply yields nothing.
- The skill curve of the plant harvest yield stat and the exact harvest-failure chance at low skill; the two page extracts gave only a base factor.
- The exact rot-rate function between 0 °C and 10 °C (the Food page's "1/temp" wording is ambiguous and the dedicated pages returned 404).
- The raw-food mood penalty's magnitude.
- What RimWorld's standard start scenario gives as food (**ASSUMED**: a small number of packaged survival meals per colonist, which is why the ration-pack class is the right analogue); not fetched within the cap.
- The tick cadence on which growth and rot are applied (the wiki gives per-day rates only; `a-15` covers the buckets).
