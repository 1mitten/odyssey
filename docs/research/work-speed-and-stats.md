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

## Follow-up 2026-09-17 — does condition slow a colonist, and by what path

**Question:** do exhaustion (very low rest) and hunger/starvation (very low food) actually slow a
colonist — work speed and move speed both — and if so by what path: mood, or health capacities?

**Answered:** 2026-09-17 · same subagent, cap 5 searches / 5 reads · clean room

### Findings

**The hypothesis is half right, and the half that is wrong is the more interesting one.**

**Hunger: yes, and through the body exactly as predicted.** Starvation is modelled as a
**malnutrition condition**, not as a mood effect. It appears **the moment food saturation hits 0%**
and then climbs at **1.51%–2.27% severity per hour**. Its effect is an **offset to the Consciousness
capacity**, by stage:

| Stage | Severity | Consciousness | Hunger rate | Mood |
|---|---|---|---|---|
| Trivial | 0.00–0.19 | 0 to −5% | +50% | −20 |
| Minor | 0.20–0.39 | **−10%** | +60% | −26 |
| Moderate | 0.40–0.59 | **−20%** | +100% (×2.5 overall) | −32 |
| Severe | 0.60–0.79 | **−30%** | +200% (×3.0 overall) | −38 |
| Extreme | 0.80–0.99 | **capped at 10%** — a *limit*, not an offset | — | −44 |
| — | 1.00 | **Death** | — | — |

The mood hit is there too, but it is *parallel decoration*: the mood number changes how the colonist
feels and behaves, and the consciousness number is what changes their output. They are two separate
effects of one condition, not one causing the other.

**The chain from consciousness to speed is short and doubles up.** Consciousness feeds both of the
capacities this matters through:

- **Moving** = `min(Consciousness, 100%) × [1 + (BloodPumping − 1) × 0.2] × [1 + (Breathing − 1) × 0.2] × LegEfficiency + offsets`, then post-factors. Consciousness is at **100% importance but capped at 100%** — the same asymmetry as Sight in part 1: being extra alert never makes you walk faster, being dulled always makes you slower, at full rate. Moving then drives **Move Speed at weight 1**.
- **Manipulation** is likewise **directly driven by Consciousness at 100% importance** (plus arms at 50% each and fingers at 8% each). And Manipulation is, from part 1, the capacity every work-speed stat applies at **100% importance with no ceiling**.

So one consciousness offset lands on *both* halves of a job. At **severe** malnutrition and an
otherwise healthy body, a colonist walks at **70%** and mines, builds and farms at **70%** — and
since a typical job is walk-then-work, the round-trip throughput is nearer **0.7 × 0.7 ≈ 49%**. At
**moderate** it is 80% and 80% (≈64% throughput); at **minor**, 90% and 90% (≈81%).

**Exhaustion: no. Tiredness does not slow anybody down at all.** This is the surprise, and it is
stated flatly: sleep deprivation does not affect any work- or combat-related stat. The rest need's
four bands do two things only — **mood and disease immunity**:

| Band | Rest | Mood | Immunity gain |
|---|---|---|---|
| Rested | ≥28% | — | 100% |
| Drowsy | ≥14% and <28% | −6 | ×96% |
| Tired | ≥1% and <14% | −12 | ×92% |
| Exhausted | ≤1% | −18 | ×80% |

What tiredness does instead is **stop them outright**: at 0% rest a colonist **may collapse from
exhaustion**, lying down and sleeping wherever they happen to be standing. Drafting wakes them
immediately. There is no glide path from "a bit tired" to "working at 60%" — you get full output,
full output, full output, then a body on the floor.

**That is the same design philosophy as the Beta 18 mood removal, applied twice.** RimWorld's rule
appears to be: *a condition either does nothing to your rate or it produces a visible, discrete
event*. It systematically refuses the invisible percentage tax. Hunger is the exception that proves
it — and hunger gets to slow you only because it has crossed out of "need" into "injury": there is a
named condition on the health tab with a severity bar you can point at. The need itself, at 5% food,
does nothing to your speed; the hediff that starts at 0% food does.

**Floors: yes, several, and they are stopping points rather than soft landings.**

- **Consciousness below 30% → unconscious**; at 0% → **dead**. So the Extreme stage's cap of 10% is
  not "works very slowly", it is "collapsed on the floor", one band before death.
- **Moving at 15% or below → downed**, no walking, crawling only.
- **Move Speed has a hard minimum of 0.15 cells/second.**
- **Manipulation at 0% → incapable** of whole classes of work rather than slow at them.
- The work-speed stats' own floors from part 1 still apply last (Plant Work Speed 10%, Global Work
  Speed 30%).

So the answer to "can a starving, exhausted colonist reach 0 speed" is: **not by degradation — by
threshold.** They stay proportionally slower until a capacity crosses a line, and then they stop
being a worker at all. Nothing grinds asymptotically towards zero.

**The death spiral is real, is deliberately shaped, and has four brakes.** The accelerator is
vicious: malnutrition **raises the hunger rate by 50% to 200%**, so being starved makes you starve
faster; wounds **do not heal at all** while malnourished; and falling consciousness slows the walk to
the food. The brakes:

1. **A long runway.** From 100% saturation a human survives up to **72.5 hours** before dying — about
   three in-game days of visible, alarmed warning before anything is irreversible.
2. **Symmetric recovery.** Once fed, severity falls at the same 1.51%–2.27% per hour it rose at. There
   is no permanent scar and no point of no return short of 100%.
3. **A price for the rescue, not a penalty.** Recovery costs **50%–60% more food** than normal for the
   duration, so a famine you survive still costs you the stores — the spiral is paid for in resources
   rather than in a lost colonist.
4. **Collapse hands the problem to somebody else.** Once downed, the pawn stops spending nutrition on
   work and becomes a patient another colonist feeds in bed. The threshold that looks like the worst
   outcome is in fact the mechanism that breaks the loop.

### Recommendation

**Copy the path, not just the outcome.** Model "starving colonists are slower" as a **condition on
the body that offsets one capacity**, and let the capacity do the rest — never as a direct penalty
written onto work speed, and never through mood. One offset on our equivalent of consciousness then
correctly slows walking *and* working without either system knowing about hunger, which is the same
argument as putting the price of a hop in `NavGraph.HopCost`.

**Take the two-capacity chain: condition → consciousness → {moving, manipulation} → {move speed, work
speed}.** It is three cheap numbers and it gets the compounding right for free.

**Cap consciousness at 100% inside moving, exactly as they do.** The asymmetry — dulled makes you
slower, alert never makes you faster — is the same rule we already recommended for sight, and having
one rule for both is worth more than either.

**Do not make tiredness a rate penalty. Rank: (1) collapse at zero rest, backed; (2) a graded
slowdown, rejected.** This is the finding most likely to be overridden by intuition later, so it is
worth writing down why: a slowdown is invisible, unattributable and un-actionable — the player sees
less output and cannot tell whether it is the weather, the wall's stuff factor or a skill. A collapse
is a colonist lying in the mud with an alert. Our project already prefers the legible event; this is
the reference agreeing.

**Keep the thresholds as thresholds.** Downed at 15% moving, unconscious below 30% consciousness,
dead at 0%. A hard floor on move speed (their 0.15 c/s) is worth having for the same reason ours
should never divide by a speed of zero.

**If we build hunger before we build medicine, build the brakes in the same commit.** The three-day
runway, symmetric recovery and the extra food cost of recovering are what make the spiral a crisis
rather than a colony-ender, and a version with the accelerator and none of the brakes would read as a
bug.

### Sources

https://rimworldwiki.com/wiki/Malnutrition — threshold at 0% saturation, the five severity stages with their consciousness offsets and hunger-rate multipliers, 1.51–2.27%/hour rise and fall, death at 1.0, 72.5-hour survival, 50–60% recovery food cost, wounds do not heal
https://rimworldwiki.com/wiki/Rest — the four rest bands with mood and immunity figures; the explicit statement that sleep affects no work or combat stat; collapse from exhaustion at 0%
https://rimworldwiki.com/wiki/Consciousness — what it feeds (moving, manipulation, talking, eating), unconscious below 30%, death at 0%, composition shape
https://rimworldwiki.com/wiki/Moving — the moving formula, consciousness at 100% importance capped at 100%, downed and crawling at 15% or below
https://rimworldwiki.com/wiki/Manipulation — consciousness at 100% importance; arms 50% each, fingers 8% each; incapable at 0%
https://rimworldwiki.com/wiki/Move_Speed — minimum allowed value 0.15 c/s

### Confidence

- **That hunger slows a colonist via a health condition offsetting consciousness, and not via mood:
  high.** Stated directly, with the stage table printing the consciousness numbers.
- **That tiredness does *not* slow work or movement: high.** The rest page says so in terms, and the
  band table lists only mood and immunity. This is the one I went looking to confirm because it
  contradicts common intuition, and the source is unambiguous.
- **The consciousness → moving → move speed chain and its 100% cap: high.** The formula is printed.
- **The consciousness → manipulation → work speed chain: medium-high.** Consciousness at 100%
  importance for manipulation is stated, and manipulation at 100% importance for the work stats was
  established in part 1; I am joining two sourced links rather than reading one page that joins them.
- **The worked throughput figures (70% × 70% ≈ 49% at severe): medium.** The multiplication follows
  from the two sourced weights, but no page prints a worked example, and it assumes an otherwise
  perfect body.
- **The thresholds and floors: high.** All four are printed numbers.
- **The four brakes on the death spiral: medium-high** as mechanics (each is sourced); **medium** as
  *design intent*, since calling them deliberate brakes is my reading rather than a stated one.

### Could not be determined

- **Whether malnutrition offsets anything besides consciousness** — the stage table lists no separate
  manipulation or moving entry, so the whole effect appears to route through consciousness, but the
  page does not say "and nothing else".
- **Whether consciousness is capped at 100% inside *manipulation* too**, as it demonstrably is inside
  moving. If it is not, a stimulant could in principle make a worker faster than baseline while not
  making them walk faster — an asymmetry worth knowing before we copy it.
- **What decides whether an exhausted pawn collapses** — the source says a colonist at 0% rest *may*
  collapse, which implies a chance or a delay rather than a certainty, and the probability is not given.
- **Whether a drafted or player-forced colonist ignores the collapse** beyond the stated fact that
  drafting wakes an already-collapsed one.
- **The exact interaction of the extreme stage's consciousness *limit* with offsets** — whether it
  clamps after everything else (making implants and drugs useless at that stage) or participates in
  the ordinary ordering.
