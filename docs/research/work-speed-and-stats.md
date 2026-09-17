# Work speed and stats — how a skill level becomes a rate

**Question:** In RimWorld, how does a colonist's skill level become a *rate* of work — the per-skill
speed curves (Mining above all, plus Construction, Plant Work, and whether hauling has one at all),
what governs deconstruction and roof work, how the stat composes out of skill, global work speed and
health capacities, how the resulting rate is actually applied to a job tick by tick, and where the
caps, floors and diminishing returns sit?

**Answered:** 2026-09-17 · one subagent, cap 10 searches / 10 reads · clean room (mechanics only; no XML, code, names or flavour text)

## Findings

### Summary table of the curves

Every work-speed stat is **linear in skill level**: `base% + per-level% × level`. The slopes are
chosen so that **level 8 lands on exactly 100%** — an average colonist is the reference worker, and
everything else is read as a multiple of them.

| Stat | Formula | L0 | L8 | L20 | L20 ÷ L0 | Capacity weights |
|---|---|---|---|---|---|---|
| Mining Speed | 4% + 12%/level | **4%** | **100%** | **244%** | 61× | Manipulation 100%, no max · Sight 50%, capped at 100% |
| Construction Speed | 30% + 8.75%/level | **30%** | **100%** | **205%** | 6.8× | Manipulation 100%, no max · Sight 20%, capped at 100% |
| Plant Work Speed | 8% + 11.5%/level (floor 10%) | **10%** (8% before the floor) | **100%** | **238%** | ~24× | Manipulation 100%, no max · Sight 30%, capped at 100% |
| General Labor Speed | flat 100%, **no skill term** | 100% | 100% | 100% | 1× | Manipulation 100%, no max · Sight 50%, capped at 100% |
| Global Work Speed | flat 100%, multiplies all of the above | 100% | 100% | 100% | 1× | — |

The important shape here is that **the slopes are deliberately not the same**. Mining is the steepest
curve in the game: an unskilled miner works at a **twenty-fifth** of an average one, and a master at
almost two and a half times. Construction is the shallowest: a beginner is already at 30% and a
master only doubles the average. The design intent is legible — mining is a job you assign to a
*specialist* and where skill is the whole story, whereas construction is a job anybody can be put on
and where skill mostly buys you *quality and a lower failure rate* rather than throughput.

### 1. The per-skill work-speed curves

**Mining Speed** — `4% + 12% per Mining level`, so 4% / 100% / 244% at levels 0 / 8 / 20. Modified
by Manipulation at full weight with no ceiling (so bionic arms genuinely make a faster miner), and by
Sight at half weight but **capped at 100%**, meaning good eyes never help and bad eyes hurt at half
rate. Implant and role offsets exist in the DLCs (a prosthetic mining arm is worth +160 percentage
points per arm; a mining specialist role +70), which is worth noting only as evidence that these are
*additive offsets* applied before the multiplicative factors. The result is then multiplied by Global
Work Speed.

**Construction Speed** — `30% + 8.75% per Construction level`, so 30% / 100% / 205%. Manipulation at
full weight with no ceiling; Sight at only 20% weight, capped at 100%. This stat covers both
*constructing and repairing* buildings. Role offsets (+50 for a production specialist, +40 while a
production command is active) are additive.

A separate **material factor** multiplies the build speed by the stuff the building is made of:
stone blocks of every type are ×80% (i.e. stone is 25% slower to build with), the default is 100%,
and the minimum allowed is 10%. Wood is described as fast and stone as slow, but the wiki does not
print wood's number.

**Plant Work Speed** — `8% + 11.5% per Plants level`, with a hard **floor of 10%**, so the printed
values are 10% / 100% / 238%. This one is valuable because the wiki states the composed formula
outright:

> `(100% + offsets) × skill modifier × manipulation factor × sight factor × global work speed`, floored at 10%

with the sight factor spelled out as `min(100%, 100% + (Sight − 100%) × 30%)`. That is the general
shape for every stat in this family; the other pages simply do not write it down. It covers sowing
and harvesting.

**Hauling has no skill-driven speed.** There is no hauling speed stat. Haul throughput is governed by
**Move Speed** and **Carrying Capacity**, not by a work stat — which is correct, because a haul job is
almost entirely walking. One wiki summary does claim hauling falls under General Labor Speed, but the
General Labor Speed page's own list of covered jobs is all production work (stonecutting, chemfuel,
tailoring, art, smithing, smelting) and names neither hauling nor cleaning. Treat "hauling is
unaffected by any skill" as the answer.

**General Labor Speed** is the interesting negative case: it is flat 100% with **no skill term at
all**, and its stated purpose is to cover work that either involves no skill *or where the skill
changes the quality of the product instead of the speed of making it*. That is an explicit design
rule — a skill drives **either** rate **or** quality, rarely both.

### 2. Deconstruction and roof work

**Deconstruction is governed by Construction Speed**, with no special multiplier. The *amount* of work
is derived from the building's Work To Build, but **clamped to between 20 ticks (0.33 s) and 3,000
ticks (50 s)**. The clamp is the design point: taking something apart is never instant and never a
day's job, however trivial or however monumental the thing was to build. The exact fraction of Work
To Build used before clamping is not stated by the wiki.

**Roof work is also Construction Speed, at ×1.7.** A constructed roof costs **65 ticks (1.08 s)** of
work to build *or* to remove, modified by `Construction Speed × 1.7`. So an average builder raises or
strips a roof cell in about 38 ticks. Roofs need no materials in either direction.

### 3. How the stat composes

The order of operations, as the stat system documents it:

1. **Base value** of the stat (100% for all of these; the skill curve is expressed as a skill
   *factor*, see below).
2. **Additive offsets**, in this order: skill offsets, capacity offsets, traits, hediffs (injuries,
   implants, drugs), ideology precepts, genes, age offsets, then worn apparel and carried equipment.
3. **Multiplicative factors**: trait factors, hediff factors, precept factors, gene factors, age
   factors, quality and stuff factors, ability factors, thing components, stat factors, **skill
   factors**, **capacity factors**, inspiration bonuses.
4. **Finalisation**: stat parts, post-process curves, post-process stat factors, the scenario
   multiplier, rounding, then **min/max clamping**.

So: everything additive lands first, everything multiplicative second, and the clamp is last.

**Health capacities are weighted, not raw multipliers.** The capacity contribution is applied as

> `value = value + ((value × capacityFactor) − value) × weight`, with `weight` clamped to 0..1

which is a linear interpolation between "capacity ignored" and "capacity fully multiplied". The
weight is what the wiki prints as **importance**. Manipulation is at importance 100% (a full
multiplier) for every work stat here and has **no maximum**, so it is the one capacity that can push
a colonist above 100%. Sight is at 50% (mining, general labour), 30% (plants) or 20% (construction)
importance and is **capped at 100%**, so sharp eyes never make you faster — only poor eyes make you
slower, and only partly. Moving capacity does not enter these stats at all; it affects Move Speed,
which is what makes the *walking* part of a job slow.

**Global Work Speed** is the single multiplier over the lot. Base 100%, **minimum 30%**, effectively
limited to 958.5%, and it applies to sixteen work-speed stats including all the above. Its inputs:

- **Traits**: industrious +35% or +20%, neurotic +40% or +20%, lazy −20% to −35% (additive offsets).
- **Chemicals and abilities**: a wake-up high +50%, a work-drive ability +50%; a work-frenzy
  inspiration is a ×1.8 *factor*, a neurosis pulse ×1.5.
- **Status**: slaves ×0.85, mechanoids ×0.5.
- **Light**: between 0% and 30% light the factor scales from ×0.3 to ×1.0 — working in the dark is a
  penalty of up to 70%, and above 30% light there is no bonus for more.

**Mood does not affect work speed, and that is deliberate.** The old high-mood bonus to Global Work
Speed was **removed in Beta 18**. Mood's consequences are breaks, thoughts and social behaviour, not
a continuous invisible tax or bonus on throughput. This is a design decision Odyssey should copy
rather than reinvent: a player can *see* a colonist go on a break, and cannot see a 7% output tax.

### 4. How the rate is applied mechanically

There are **two different mechanisms** in the game, and the distinction matters for us.

**(a) The work-counter model — everything except mining.** Work is denominated in **ticks-at-100%**:
a building's Work To Build, a recipe's Work To Make, a roof's 65, are all "how many ticks this takes
a worker running at exactly 100%". At 100% speed a worker adds **one unit of work per tick**; 60 work
= 1 in-game second, 1,000 work = one in-game day of uninterrupted labour. The per-tick increment is
the finalised speed stat, so the counter **falls faster for a fast worker** — the total is *not*
divided up front. This matters because the speed stat can change mid-job (a mood drug wears off, the
lights go out, an arm is injured) and the counter model absorbs that correctly; a pre-divided
duration would not.

Worked example, a roof cell: 65 work, Construction Speed ×1.7. At Construction 0 (30%) that is
65 / (0.3 × 1.7) ≈ 127 ticks; at level 8 ≈ 38 ticks; at level 20 (205%) ≈ 19 ticks.

Worked example, deconstruction: whatever the derived work is, it is clamped to 20..3,000 ticks, and
then divided by Construction Speed, so a level-0 builder takes 3.3× as long as an average one and a
level-20 builder about half.

**(b) The strike/damage model — mining.** Mining does **not** accumulate work against a target
number; it **deals damage to the rock's hit points**. An unmodified pawn does **80 damage per mining
action**, and **at Mining 8 an action takes 120 ticks (2 s)**. Natural stone hit points differ by
type — sandstone 400, slate 500, granite 900 — so the *number of strikes* depends on the rock and the
*interval between strikes* depends on the miner.

This answers the cadence question directly: **for mining, the strike cadence itself changes with
speed** — the damage per blow is fixed and the time to the next blow is what shrinks. Whether the
implementation shortens the interval or accumulates a fraction of a strike per tick is an
implementation detail with identical results; the observable behaviour is a visibly faster pick.
(ASSUMED: that the interval is exactly `120 ÷ MiningSpeed` ticks. The wiki states 80 damage per
action and 120 ticks per action at Mining 8 — i.e. at 100% — but does not write the division out.)

Worked example, a granite cell at 900 HP → 12 strikes (the last one overkills):

| Mining level | Speed | Ticks per strike | Total |
|---|---|---|---|
| 0 | 4% | 3,000 | ~36,000 ticks (~10 min real time at normal speed) |
| 8 | 100% | 120 | 1,440 ticks (24 s) |
| 20 | 244% | ~49 | ~590 ticks (~10 s) |

Sandstone at 400 HP is 5 strikes, so 15,000 / 600 / ~246 ticks at the same three levels.

**Floors.** Plant Work Speed has an explicit **10% minimum**. Global Work Speed has a **30%
minimum**, and the material factor a 10% minimum, so even a stacked-against-you case bottoms out
rather than going to zero. Mining Speed and Construction Speed minima are **not stated** by the wiki;
their skill curves floor naturally at 4% and 30%.

### 5. Diminishing returns, caps and a maximum useful level

**There are no diminishing returns in the speed curves at all.** Every one is dead linear in level,
right up to the cap. A level going from 19 to 20 is worth exactly as much throughput as one going
from 0 to 1. The cap is simply that **20 is the highest level**.

The diminishing returns live entirely on the **acquisition** side:

- XP required per level rises with level, so equal XP buys fewer levels as you climb.
- A **soft cap of 4,000 net XP per skill per day**; XP past it is multiplied by **20%**.
- **Skills above level 10 decay continuously**: XP drains away, and a level is only lost after
  reaching 0 XP *and then losing a further 1,000*, which is a deliberate hysteresis band to stop a
  colonist flickering across a level boundary.
- XP gained = `Global Learning Factor × passion multiplier × base XP`, default learning factor 100%.

So "maximum useful skill level" is 20 for every one of these jobs, and the pressure against reaching
it is the cost of getting there and the cost of holding it, not a flattening of the reward.

## Recommendation

**Take the work-counter model for everything, including mining, and do not copy the damage model.**
Our world table already denominates rock as `workToClear` (700), which *is* the counter model, and
our felling, building and deconstruct lines all read naturally as counters. The damage model buys
RimWorld nothing our design needs (it exists mostly so that explosives and the same code path can
also destroy rock) and it costs us a second mechanism to save, hash and test. Rank: (1) one counter,
work decremented per tick by the finalised speed — **backed**; (2) HP-and-damage for mining only —
rejected, second mechanism, no gain; (3) divide the total by speed at job start — rejected outright,
it silently ignores a speed that changes mid-job, which ours will (light, injury, mood-driven
hediffs later).

**Denominate work in ticks-at-100% and make one unit per tick the definition of an average worker.**
It gives designers an intuition that survives — 1,000 work is one colonist-day — and it makes
`workToClear = 700` immediately readable as "about two-thirds of a day for an average colonist".

**Copy the differentiated slopes, not a single shared curve.** Back the shape `base + perLevel ×
level` with **level 8 pinned at 100%** for every work stat, and give each job its own steepness:
steep for mining (skill is the whole story), shallow for building (anyone can be put on it, and skill
should buy quality and a lower failure rate instead). Our exact numbers should be ours — RimWorld's
61× spread from mining 0 to 20 is more extreme than we probably want at three colonists, where losing
your only miner would stop the colony dead. A first cut of **20% + 10%/level** for mining (20% / 100%
/ 220%) and **40% + 7.5%/level** for building (40% / 100% / 190%) keeps the intent and softens the
early cliff; mark both **ASSUMED**, because they are our design choice and not a sourced figure.

**Adopt the composition order as stated**: additive offsets first, multiplicative factors second,
clamp last — and write one `WorkSpeed(pawn, job)` function that is the only place it happens, the way
`NavGraph.HopCost` is now the only place a hop is priced. This family of stats is exactly the kind of
number three seams will otherwise compute for themselves and agree with by coincidence.

**Adopt the weighted-capacity rule with sight capped at 100%.** It is a cheap, legible asymmetry:
good eyes never make you faster, bad eyes make you slower in proportion to how much the job needs
them. Manipulation uncapped at full weight is what leaves room for a future prosthetic to matter.

**Adopt a single global work speed multiplier with a 30% floor, and keep mood out of it.** The Beta
18 removal is the strongest design-intent signal in this whole area: mood should produce visible
events, not an invisible percentage. Our darkness penalty, if we want one, has a sourced shape to
copy (×0.3 at no light rising to ×1.0 at 30% light, flat above).

**Clamp deconstruct work the way RimWorld does** — a floor and a ceiling, so no building is
instantaneous to take apart and none takes a day. Our 20..3,000-tick equivalents want scaling to our
own numbers; mark them ASSUMED.

**Tie `WorkSwing`'s cadence to the speed factor.** This is the one finding that lands directly on
code we already have. RimWorld's fast miner visibly strikes faster, and because our axe, pick and
hammer strokes are *computed* rather than animated, we can do this for free and get the strongest
readable signal of skill in the game — and it costs nothing in the simulation, because the swing is
presentation and not in a cell, a save or the hash.

## Sources

https://rimworldwiki.com/wiki/Mining_Speed — mining curve 4%+12%/level, capacity weights, 80 damage per action, 120 ticks per action at Mining 8
https://rimworldwiki.com/wiki/Construction_Speed — construction curve 30%+8.75%/level, capacity weights, covers repair
https://rimworldwiki.com/wiki/Plant_Work_Speed — plants curve 8%+11.5%/level, the fully composed formula and the 10% floor, the sight-factor expression
https://rimworldwiki.com/wiki/Global_Work_Speed — base 100%, min 30%, cap 958.5%, trait/drug/status/light modifiers, mood bonus removed in Beta 18
https://rimworldwiki.com/wiki/General_Labor_Speed — no skill term; the rate-or-quality design rule; what it does and does not cover
https://rimworldwiki.com/wiki/Stat — the order of operations, offsets vs factors, the weighted capacity formula, the list of work-speed stats
https://rimworldwiki.com/wiki/Work_To_Build — work measured in ticks; the 20..3,000-tick deconstruct clamp
https://rimworldwiki.com/wiki/Work_To_Make — 60 work = 1 second, 1,000 work = one in-game day at 100%
https://rimworldwiki.com/wiki/Construction_Speed_(Material_Factor) — stone blocks ×80%, default 100%, minimum 10%
https://rimworldwiki.com/wiki/Roof — 65 ticks to build or remove, modified by Construction Speed × 1.7
https://rimworldwiki.com/wiki/Deconstruct — deconstruct work comes from Work To Build, speed from Construction Speed
https://rimworldwiki.com/wiki/Skills — level cap 20, XP soft cap 4,000/day then ×20%, decay above level 10 with a 1,000-XP hysteresis band, learning-factor formula
https://rimworldwiki.com/wiki/Granite — natural stone hit points (granite 900, slate 500, sandstone 400)

No decompiled source, Def XML or asset was opened; none of the above is a source-code mirror.

## Confidence

- **The four curves and their capacity weights: high.** All four are stated as explicit formulas on
  their own stat pages, and three of them independently land on exactly 100% at level 8, which is a
  strong internal check.
- **Composition order and the weighted-capacity rule: high.** The stat page states the order
  step by step, and the plants page's composed formula agrees with it.
- **Global Work Speed's value, floor and inputs, and the mood removal: high.**
- **Roof work (65 ticks, ×1.7) and the deconstruct clamp (20..3,000 ticks): high** for the numbers as
  printed; **medium** that nothing else modifies them, since neither page gives a worked example.
- **The work-counter mechanism (per-tick increment rather than up-front division): medium-high.**
  The units are unambiguous — work is denominated in ticks-at-100% and 1,000 work is a colonist-day —
  and that only makes sense as a counter, but no page says "the counter is incremented by the speed
  stat each tick" in those words.
- **The mining strike model: medium.** That mining is HP damage and not a work counter, at 80 damage
  a strike with a 120-tick strike at Mining 8, is stated plainly. That the interval is exactly
  `120 ÷ speed` is my inference and is marked ASSUMED above.
- **Hauling having no skill-driven speed: medium.** There is no hauling speed stat and the General
  Labor Speed page does not list hauling; but one wiki summary claims it does, and I could not settle
  the contradiction within the cap.
- **No diminishing returns in the speed curves: high.** Every curve is linear and the tables confirm
  it level by level.

## Could not be determined

- **The exact fraction of Work To Build that becomes deconstruct work** before the 20..3,000-tick
  clamp is applied. The clamp is documented; the fraction is not.
- **Whether Mining Speed and Construction Speed carry explicit minimum values** the way Plant Work
  Speed (10%) and Global Work Speed (30%) do. Their pages do not print one.
- **Whether the mining strike interval is `120 ÷ speed` exactly**, or whether speed instead scales
  damage per strike. Total time is identical either way; only the visible cadence differs.
- **The material factor for wood, steel and metals.** Only the stone blocks are tabulated (all ×80%);
  wood is described as fast but its number is not given.
- **The precise order in which the material factor enters** relative to Global Work Speed and the
  capacity factors. Both the construction page and the material-factor page are flagged as stubs
  asking for exactly this worked example.
- **Whether hauling or cleaning fall under General Labor Speed.** Two wiki pages disagree; the
  General Labor Speed page's own list is the stronger source and excludes both.
- **How mining speed interacts with the "action" boundary when a strike would overkill the rock** —
  whether the final partial strike is prorated or the cell simply dies early. Our counter model makes
  the question moot, which is a further small argument for it.
