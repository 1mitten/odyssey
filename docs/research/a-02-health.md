# Lane A2 — Health

## Question

How does RimWorld's health model work — the body-part hierarchy, capacities, pain, bleeding, healing, tending, infection, and the line between *downed* and *dead* — and, for the number M3 needs now, **what should a fall do to a colonist** when U29's collapse rule drops the floor from under them (`docs/plans/vertical-slice.md` U29; `02-world-and-layers.md` §4 says "takes damage scaled by the fall" and lists the scaling as an unset tuning constant)? Clean-room: mechanics and numbers from the public wiki plus two outside sources for falls, paraphrased; no Def XML, no decompiled source, no names or flavour text.

Cap for this row: 15 page reads or 12 searches. Used: 14 reads, 5 searches.

## Findings

### 1. The body is a tree of parts, each with hit points and a coverage share

A human is a hierarchy of parts. Each part has its own HP, a parent, and a list of the **capacities** it contributes to. Damage hits a part chosen by coverage (outer parts shield inner ones), reduces that part's HP, and reduces the linked capacities in proportion to HP lost and the part's weight in that capacity. The human tree, with HP:

- **Head 25** (lethal if destroyed) → skull 25 (indestructible) → **brain 10** (consciousness; damage always scars); eyes 10 × 2 (sight, −25% each); ears 12 × 2 (hearing, −25% each); nose 10 (cosmetic); jaw 20 (eating, talking) → tongue 10.
- **Torso 40** (lethal) → **neck 25** (lethal; eating, talking, breathing); spine 25 (moving; total paralysis if destroyed); pelvis 25 (indestructible; moving); sternum 20 and ribcage 30 (indestructible; breathing, −48% max each); lungs 15 × 2 (breathing −50% each; both lost is death); stomach 20 (digestion −50%); **liver 20** (lethal; digestion, blood filtration); **heart 15** (lethal; blood pumping); kidneys 15 × 2 (filtration −50% each; both lost is death).
- **Shoulders 30 × 2** → clavicle 25 (indestructible) → **arm 30** → humerus 25, radius 20 → **hand 20** → fingers 8 × 5 each. All manipulation.
- **Legs 30 × 2** → femur 25, tibia 25 → **foot 25** → toes 8 × 5 each. All moving.

The wiki's worked example of the proportionality: 1 HP off one lung of a healthy human is −3.4% breathing. Destroying a part removes its contribution entirely and stops its pain (damaged parts hurt, destroyed parts do not). Excess damage on a destroyed external part passes to the parent.

**The eleven capacities** and their sources: consciousness (brain), moving, manipulation, breathing, blood pumping, blood filtration, sight, hearing, talking, eating, digestion. Consciousness is itself derived from the others; the wiki gives the shape:

`consciousness = base × (1 − clamp((pain − 0.1) × 4/9, 0, 0.4)) × (1 − 0.2 × Δbloodpumping) × (1 − 0.2 × Δbreathing) × (1 − 0.1 × Δfiltration)`

— so pain above 10% starts costing consciousness, up to −40% at 100% pain, and each lost fraction of the three vital capacities takes a further slice. Consciousness then multiplies most other capacities in turn. That chain is the mechanism by which a bad leg wound ends in unconsciousness: injury → pain → consciousness → everything.

### 2. Pain

`pain = damage × damageTypeFactor`, with 1.25 for most injury types (1.25% pain per HP lost), scaled by the pawn's health scale. Destroyed parts contribute nothing. **Pain shock threshold** is 80% for a normal human (30% for a *wimp* trait); at or above it the pawn is downed. Since 80% ÷ 1.25% = 64 HP of live injuries, the wiki's practical figure of "typically downed around 80 HP" reflects some damage landing on parts that are destroyed (and stop hurting) or on parts with lower factors.

### 3. Bleeding and blood loss

- Every bleeding injury has a **bleed rate = damage × body-size factor** (as a percentage), with **heart wounds ×5 and neck ×4**; a lost limb bleeds as if at `2 × its max HP`. The health tab totals bleed rate and shows time to death.
- Blood loss is a condition with stages: **minor ≥15%** (−10% consciousness), **moderate ≥30%** (−20%), **severe ≥45%** (−40%), **extreme ≥60%** (−40% and consciousness capped at 10%, i.e. downed), **death at 100%**.
- **Any tend stops all bleeding immediately, regardless of quality.** That is the single most important fact for a slice: a bandage from a skill-0 colonist saves a life.
- Recovery is a flat **33.3% of blood per day** once bleeding has stopped, for every pawn.

### 4. Healing and tending

- Every 600 ticks, `healRate × 0.01` HP is healed from one randomly chosen non-permanent wound. Heal rate per day is additive: **base 8**, +4 on a sleeping spot or the ground, **+8 in a bed**, +14 in a hospital bed, +4 for a healing enhancer, and **tending adds +4 at 0% quality up to +12 at 100%**. A bed-rested, well-tended wound heals at 28 HP/day; an untended pawn on the floor at 8.
- **Tend quality** = medical tend quality (skill: **20% at level 0, 70% at 5, 110% at 10, 135% at 15, 155% at 20**) × **medicine potency** (none 0.3, herbal 0.6, industrial 1.0, glitterworld 1.6), plus +0.1 for a hospital bed and +0.07 for a vitals monitor, ×0.7 if self-tending, then × a random 0.75–1.25, then **clamped by the medicine used: 70% without or with herbal, 100% industrial, 130% glitterworld**. Tend speed also scales with skill (40% at 0, 100% at 10, 160% at 20).
- **Infection**: a bleeding wound rolls for infection once after a random 15,000–45,000 ticks (4–12.5 h). Base chance 10% (25% for bites, burns and frostbite), multiplied by a tend factor from 85% at 0% quality down to 5% at 100%, and by room cleanliness (50% at cleanliness 0). Tend quality "slows illnesses more" as well as lowering the roll.

### 5. Downed versus dead

**Downed** (incapacitated) when any of:
1. **consciousness below 30%** (but above 0);
2. **pain at or above the pain-shock threshold** (80%);
3. **moving at 15% or below**.

A downed pawn drops what it carries, keeps its apparel, is mostly ignored by enemies, and, if it has manipulation, eventually crawls toward a bed or away from danger. Rescue is a haul to a bed; tending can be done in place.

**Dead** when any of:
1. a **vital part is destroyed**: torso, head, brain, neck, heart, liver; or both lungs; or both kidneys;
2. **consciousness, breathing, blood pumping, blood filtration or digestion reaches 0%**;
3. **blood loss reaches 100%**;
4. **total injury damage reaches 150 HP** for an adult human (150 × health scale for others) — the *lethal damage threshold*, which pain shock normally pre-empts at about 80 HP;
5. a condition (malnutrition, hypothermia, heatstroke, toxicity and others) reaches 100% severity.

Additionally, an **enemy** downed by anything *except blood loss* rolls an instant-death chance set by the storyteller's population settings; mechanoids always die on downing. The wiki is explicit that this is a population-control lever, not a physiological rule, and that the blood-loss exemption exists so that capture remains possible. For our colonists none of this applies: a colonist dies by rules 1–5 only.

### 6. Blunt damage — the shape a fall must reuse

- A blunt hit on an external part gives 80–90% of the damage to that part and, with **40% probability, an extra strike of 20–35% of the base damage to an internal part inside it**. Excess from a destroyed external part transfers to its parent.
- Crush and blunt share the *blunt* armour class, a 40–100% overkill destruction range and the same injury kinds (bruise, crush, crack).
- Blunt to the torso has a stun chance scaled by damage fraction; blunt to the brain stuns up to 100% at half the brain's HP.

### 7. Vanilla has no falling pawns — but it does have things landing on them

RimWorld never drops a pawn a level; the only vertical damage is the roof coming down:

- **Thin (constructed) roof collapse: 15–30 crush damage, 0% armour penetration**, applied to the *top-facing* parts (neck and all its external children, so the head and its parts), leaving rubble.
- **Thick (mountain) roof collapse: 99,999 damage at 999% penetration** — obliteration, no corpse, no gear.

`a-04-building-and-materials.md` records the same two figures.

### 8. Precedents for a fall from other games and from the ground truth

- **The Z-Levels mod** (the one serious attempt to bolt layers onto RimWorld) lists "adjust damage when a pawn falls through a layer" under *planned*, never implemented — there is no number to borrow. `a-16` (OQ-26) covers the mod itself.
- **Dwarf Fortress** models falls physically: acceleration 0.032 z/tick², terminal velocity 1.8 z/step reached after 52 levels (longer falls do the same damage), the impact resolved as a hit from a large blunt object, landing material density mattering, water decelerating. The wiki's observed outcomes: **falls of 1–2 levels are unlikely to cause significant injury; 100% mortality at about 25 levels; 30+ levels "explode"**. A DF z-level is a nominal storey.
- **Going Medieval** (the nearest voxel colony sim) has floor collapse but no documented fall-damage figure; the search found only stability rules (`b-going-medieval.md`).
- **Real falls**, for calibration of *our* 3 m storey: the trauma literature puts the height at which half of victims die (LD50) between about 12 m and 15 m — 4–5 storeys at 3 m — with historical estimates as low as 6.6 m; a large registry study (8,699 adults) found mortality of 2.5% under 1 m, 3.5% at 1–6 m and 5.5% above 6 m *among those who reached hospital*, with >6 m a statistically distinct risk class. Survival from 18 m is documented. So: one storey hurts and rarely kills; two to three injure seriously; four to five is a coin toss; beyond that, death.

## Recommendation

### The fall rule for U29

Our layer is 3.0 m (ADR 0002), which is a real storey, so the real-world curve transfers directly and DF's "1–2 safe, 25 lethal" is too gentle (its z-level is not a fixed height and its damage is a physical simulation we are not building). Anchor the curve on three RimWorld constants instead: **a thin-roof collapse is 15–30 crush**, **pain shock downs at about 64–80 HP of injuries**, and **150 HP is lethal**. A fall of one storey should feel like a roof falling on you; three storeys should down; five should usually kill.

**`fallDamage(n) = round(15 × n^1.5)` blunt, 0% armour penetration, for a fall of `n` layers:**

| Layers | Damage | Pain (×1.25) | Outcome on an unhurt colonist |
|---|---|---|---|
| 1 (3 m) | 15 | 19% | bruised, walks away; matches the thin-roof minimum |
| 2 (6 m) | 42 | 53% | badly hurt, a leg part likely destroyed, not downed |
| 3 (9 m) | 78 | 97% | **downed** by pain shock |
| 4 (12 m) | 120 | — | downed; death if a vital part is hit |
| 5 (15 m) | 168 | — | over the 150 lethal threshold: **dead** |

That reproduces the ground truth (LD50 at four to five storeys, one storey rarely fatal) and DF's safe band, using no constants that are not already in the model. Apply it **as blunt damage in RimWorld's shape**: split into 2–4 hits chosen from the *bottom-facing* parts (feet, legs, pelvis, torso — the mirror of the roof rule's top-facing set), each with the 40% internal follow-through, and ±20% random spread so identical falls do not give identical injuries. Damage overflowing a destroyed foot or leg climbs the tree to the torso, which is what makes the 5-layer case lethal without a special rule.

**Until body parts exist** (the slice has a single HP pool), use the same table against a 150-point pool with "downed at ≥ 64 damage": one layer 10%, two 28%, three 52% and downed, five dead. The number is a Def field (`FallDamageBase = 15`, `FallDamageExponent = 1.5`), and `02-world-and-layers.md` §4's "fall-damage scaling" line points here.

Two consequences worth stating. A cascade collapse of an upper slab drops a colonist **one layer at a time** through successive holes, and the damage must be summed per landing, not per total height, or a three-layer cascade is a survivable 3 × 15 instead of a downing 78 — so the rule takes the height of the *uninterrupted* fall, which is the number of consecutive missing slabs. And **items and furniture on the slab** take the same value against their HP, which is enough to smash furniture on a two-layer fall and destroy a stack on a three-layer one.

### The health model for the slice

Port the downed/dead rules exactly, because they are what makes a collapse legible: **downed = consciousness < 30% or pain ≥ 80% or moving ≤ 15%; dead = vital part gone, a vital capacity at 0, blood loss 100%, or 150 HP of damage**. Port bleeding with "any tend stops it" and the 33%/day recovery, the additive heal rate (8 base, +8 bed, +4–12 tend), and tend quality as skill × potency clamped by medicine. Stub infection, self-tend, surgery and the enemy death-on-down roll.

## 3D/layer impact

- **Falls are the ruin's native hazard** and the health model's job is to make them *readable*: the one-layer bruise, the three-layer downing and the five-layer death are three distinct stories the player can learn from, and the table above is chosen so each layer count has a different outcome.
- **Rescue is a haul across layers.** A colonist downed at the bottom of a shaft must be reachable; `a-14`'s stair cost and `d-04`'s district reachability decide whether a rescue job can even be offered. A downed colonist in an unreachable cell is a slow death by the 72.5-hour starvation clock (`a-08`), which is a fair and legible consequence.
- **Crawling** (downed pawns with manipulation crawl toward beds) needs a path rule of its own: crawlers cannot use stairs, ladders or climb out of holes, so a hole is one-way for them.
- **Roof collapse becomes fall damage.** In our model the roof is the slab above, so "roof falls on you" and "floor falls from under you" are the same event seen from two layers; the top-facing crush rule (15–30, neck and head) applies to the layer below the failed slab, the bottom-facing fall rule to the pawn that was standing on it. Both land in the same tick.
- **Blood loss and bed rest are the reason to build a hospital room on the same layer as the work**, and the 33%/day recovery makes a bad fall a three-day absence — long enough to matter in a ten-day run.

## Ruined-city impact

- Holes in slabs (`02-world-and-layers.md` §5) are the map's commonest connector and are "passable downward only, as a fall, with damage"; this file gives that damage. A colonist should never *path* through a hole voluntarily at 2+ layers, and at 1 layer only when the pathfinder's cost for the 15 damage is paid — which argues for a hole being a connector only for deliberate player orders, never for the autonomous job search.
- Rubble under a collapsed slab (mined out later) is DF's "landing material" question answered the cheap way: no material term, the rule is height only.
- Salvaging a wall that orphans a slab is the setting's central trap (`02` §4). With this table, the colonist on that slab is bruised, not killed, at one layer, so the trap teaches rather than punishes on its first appearance.

## Layer questions touched

- **Q2 (is a roof a floor / collapse):** directly — supplies the fall-damage rule and the rule that roof-crush and floor-fall are one event.
- **Q1 (vertical movement):** holes as downward-only connectors get a cost and a "never autonomously" rule; crawling pawns cannot use connectors.
- **Q9 (UI):** a fall's outcome must be inspectable — the health tab's part tree is the vanilla answer and the inspect pane already has the tab.
- **Q12 (what the slice proves):** the slice needs the single-pool version of the fall table, downed/dead by pool thresholds, and rescue; M3 needs parts, bleeding and tending.

## Sources

- https://rimworldwiki.com/wiki/Body_parts
- https://rimworldwiki.com/wiki/Health
- https://rimworldwiki.com/wiki/Injury
- https://rimworldwiki.com/wiki/Blood_loss
- https://rimworldwiki.com/wiki/Downed
- https://rimworldwiki.com/wiki/Death
- https://rimworldwiki.com/wiki/Doctoring
- https://rimworldwiki.com/wiki/Medicine
- https://rimworldwiki.com/wiki/Damage_Types
- https://rimworldwiki.com/wiki/Roof
- https://dwarffortresswiki.org/index.php/DF2014:Gravity
- https://steamcommunity.com/sharedfiles/filedetails/?id=2127428910 (Z-Levels Beta)
- https://pmc.ncbi.nlm.nih.gov/articles/PMC7312001/ (fall height and mortality, 8,699 adults)
- https://journals.sagepub.com/doi/10.1177/1460408616689807 (LD50 re-examined; search extract only)

## Confidence

**High** for: the body-part tree with HP and capacity links; the eleven capacities and the consciousness formula's shape; pain at 1.25%/HP and the 80% shock threshold; bleed-rate multipliers, blood-loss stages, tend-stops-bleeding and 33%/day recovery; the additive heal-rate table and the 600-tick cadence; the medical skill table, potency values, offsets and clamps; infection timing and factors; the three downed conditions and five death conditions including the 150 HP lethal threshold; the blunt 40%-internal rule; the thin- and thick-roof collapse figures; DF's stated safe and lethal bands.

**Medium** for: the "downed at about 80 HP" practical figure (a wiki generalisation); the exact set of parts a fall should strike (ours, mirrored from the roof rule); the real-world LD50 range (the literature disagrees by a factor of two and is biased toward hospital arrivals).

**Low** for: nothing in the findings. The fall table itself is a **recommendation**, ours, calibrated to the constants above; it is defensible, not measured, and should be tuned from play.

## Could not be determined

- **Tend duration** — how long a tend lasts before an injury needs re-tending, and the tick cost of the tend action. Two wiki pages and a targeted search gave skill-scaled *tend speed* only.
- The conversion from bleed rate to blood-loss severity per day (the wiki shows time-to-death in game but does not state the formula).
- The exact coverage percentages by which a hit picks a part; the hierarchy and HP are stated, coverage shares are not.
- Going Medieval's fall damage, if it has any.
- Whether RimWorld's 1.5 Anomaly content added any pawn-falling mechanic; nothing in the fetched pages suggests it.
- Body-part HP scaling with body size for non-humans (only the health-scale multiplier on the lethal threshold was stated).
