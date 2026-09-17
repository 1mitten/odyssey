# 17. Rates and stats — how a colonist's standing becomes a speed

**Status: design, 2026-09-17. Nothing here is built.** Written from two owner questions asked in
the same conversation:

> "chopping/cutting (animation and game) — dependent on your skill level determines how fast you
> swing and how quickly you chop wood, but every time you do, your experience slowly goes up. Is
> there anything that determines this in the plan yet?"

> "We should also have a move speed (to determine walking and running) that varies between
> characters and also is dependent on their condition/health."

The answer to the first is **half**: experience is fully built and nothing reads a level back out.
The answer to the second is **no, and it cannot be built as the numbers stand** — the movement
accumulator has no resolution to vary within. Both are the same missing thing, which is why they
are one document: **a rate**, per pawn, per activity, computed in one place and multiplied into an
accumulator.

This document is the design. `docs/plans/vertical-slice.md` §WS carries the units and
`docs/plans/overnight-queue.md` carries the rows.

**How closely this follows the reference** (owner, 2026-09-17: *"use RimWorld as a rough reference
to how this could work well"*). The rule applied throughout, and the one a later session should
hold this document to:

- **Shape, structure and design intent: taken.** The linear curve with no diminishing returns; the
  work counter incremented per tick rather than a total divided up front; the composition order
  (base, additive offsets, multiplicative factors, clamp); capacity weighting where a job's
  dependence on a faculty decides how much that faculty matters; the relative character of the
  skills — mining a specialist's steep curve, construction shallow because skill buys quality
  there instead, hauling flat. Where the reference has made a decision and *unmade* it, that is
  taken too: §3e records the work-speed bonus it removed and why.
- **Constants: re-anchored, and only where our own numbers differ.** The one place this departs is
  §3b, and it departs for a stated reason rather than taste — every reference curve is anchored on
  a level-8 colonist and ours start at level 1, so importing the constants unchanged would put a
  5–6× brake on the whole game. The curves keep their shape and their ordering; the two integers
  that position them move.
- **Never: names, text, Def XML or code.** Clean room, per the brief. Everything the reference
  contributes here arrives through `docs/research/`, paraphrased, with the pages cited.

---

## 1. Where rates stand today, measured rather than remembered

Every claim in this table was read off the code on 2026-09-17.

| | What is true now | Where |
|---|---|---|
| **Experience** | Fully built. A driver calls `Work(ctx)` on the ticks that are work and not on the walk to it; the pawn earns `experiencePerWorkTick` (110 thousandths for every working job, ASSUMED) × learning factor × passion (×0.35 / ×1.0 / ×1.5) × 0.2 once the day's soft cap is passed. | `Job.cs:264`, `Pawn.cs:263` |
| **Level** | Read off experience by a Def table, never stored; decays above level 10 on the Long cadence. Saved and hashed. | `PawnContent.cs` `SkillDef`, `SkillSystem.cs` |
| **Starting level** | Rolled once on the first tick from a weighted table, deterministic on `(world seed, pawn id)`. **Mean level 1.16**; levels 6–7 together are a 2% roll. | `StartingSkillsSystem.cs`, `Colonist.xml:80` |
| **Work rate** | **Does not exist.** Every work tick adds exactly **1**, whoever is working and whatever they know. | `MineJob.cs:342`, `BuildJob.cs:403`, `DeconstructJob.cs:124`, `JobDrivers.cs:281` |
| **Move rate** | A single content constant shared by every colonist: `movePerTick = 1` against a flat crossing of 100, i.e. 1.5 m/s. | `Pawn.cs:226`, `Colonist.xml:43` |
| **Condition** | Needs, mood and thoughts all run. **Health does not exist** — a pawn cannot be hurt, so there are no capacities to read. | `NeedsSystem.cs`; `a-02-health.md` is research only |
| **The swing** | Presentation, and decorative. `figure.SwingClock += deltaTime` against a fixed `StrokeSeconds` per tool, jittered per pawn so a work gang does not beat in unison. It is not derived from, and does not affect, the work being done. | `PawnFigureDirector.cs:924`, `WorkStyle.cs:138` |

**So the owner's reading of the game is exactly right and the gap is one-sided.** A colonist does
get better at chopping — the number goes up, it is saved, it survives a reload, and `U40`'s
candidate cards will show it. It buys nothing. A level-20 miner and a level-0 miner clear the same
rock in the same 700 ticks, swinging at the same rate, and the only thing a skill has ever changed
in this game is how fast the *next* point of that skill arrives.

**It was foreseen and then not done.** `a-08-plants-growing-food.md` §"Recommendation" already
says: *"when OQ-14 lands skills, the felling driver multiplies work by the plant-work-speed
curve"*. OQ-14 landed. The multiplication did not, and nothing since has been written down that
would have caught it — `15-skills.md` §1 states the gap plainly ("Nothing reads a skill level yet.
Not work speed, not yield, not quality") and leaves it there, because that document was about
icons.

---

## 2. The decision: a rate is per mille, and the accumulator scales, not the content

One mechanism serves both asks.

> **A rate is an integer in thousandths, where 1,000 means "the speed everything is tuned at
> today". A rate never changes what a thing costs; it changes how fast a pawn pays for it.**

Three consequences follow, and the third is the one that makes this cheap.

### 2a. Cost belongs to the world, rate belongs to the pawn

A tree costs 800. A granite cell costs 700. A wall costs `workToBuild × stuffFactor + stuffOffset`
— **which is already `base × factor + offset`**, the `a-04` stuff formula, live at
`ConstructionContent.cs:173`. None of those numbers move. What moves is how much of that cost one
tick of one colonist's labour discharges.

This is forced rather than chosen, and the code already decided it: **work is banked on the cell,
not on the job** (`DesignationGrid.cs`, "On the cell, not on the job, and that is the point" — a
miner who breaks for a meal must not lose the morning's work, and the next colonist must be able to
finish the cell). A cell worked by two colonists of different skill therefore has to accumulate in
a unit that means the same thing to both of them. **That single fact rules out the obvious
alternative** — dividing the job's total by the worker's speed at job start, the way one might
naively read the reference — because the total would then depend on who happened to pick the job up.

### 2b. Scale the accumulator, not the content

A rate of 1,000 per mille adding "one tick of work" per tick is exact. A rate of 600 adding
"0.6 ticks" is not: in integers it is 0 or 1, which is either a colonist who never finishes or one
who works at full speed. Three ways out, ranked:

1. **Scale the accumulator by 1,000 and leave every authored number alone.** `_work[cell]` counts
   **milliwork**; a tick adds the rate; the comparison reads `cost × 1,000`. At rate 1,000 the
   behaviour is bit-identical to today. **Backed.**
2. Re-author every cost in thousandths. Same arithmetic, but it moves `Terrain.xml`,
   `Jobs.xml` and `ConstructionContent`, so it moves the content fingerprints
   (`WorldContentDefTests`, `PawnContentDefTests`), the wiki, and every number a human reads while
   tuning. All cost, no benefit.
3. Carry a fractional remainder per pawn per job. New saved, hashed, per-pawn state that exists
   only to hold a rounding error, and it would have to be reset on every job change or it leaks
   work between activities. Rejected.

**The same trick, and the same ranking, applies to movement — and there it is not an optimisation
but the only thing that makes the feature possible at all.** `movePerTick` is **1** against a cell
cost of 100. The only speeds the current shape can express are 1.5 m/s, 3.0 m/s, 4.5 m/s. There is
no room between them for "this colonist is a little quicker than that one", and `Colonist.xml:36`
had already diagnosed it in a comment: *"A pace between these integers wants the cost scale raised,
not a fraction stored."*

**Raise the accumulator, not the cost scale.** `MoveProgress` counts thousandths of a cost unit and
the step comparison reads `StepCost × 1,000`. This is deliberately **not** a change to
`MoveCost.Walk/JumpUp/Drop`, `NavGraph.HopCost` or the terrain cost classes: the planner's prices
are untouched, so no path changes, no golden path checksum moves, and
`HopPriceHasOneOwnerTests` — which fails the fast tier if any file but `NavGraph` names the hop
constants — stays green and is not fought with. **Two saved integers change scale, `_work[cell]`
and `MoveProgress`, and nothing else does.**

### 2c. Integers, one place, fixed order

Nothing here is a float; every factor is per mille and every combination is
`value * factor / 1_000` in a stated order, so it hashes and saves with no rounding question —
the idiom `Pawn.GainExperience` and `ConstructionContent` already use. Three virtual methods are
the whole public surface:

```
Pawn.WorkRatePerMille(int workType)   // 1_000 == today
Pawn.MoveRatePerMille()               // 1_000 == today
Pawn.ConditionPerMille()              // 1_000 == well; shared by both, §4c
```

**`ConditionPerMille` is one method serving both rates, by the owner's answer of 2026-09-17**
("if exhausted, starving etc — all has an effect"). One computation, one floor, two consumers: a
colonist who is in a bad way is slower at walking *and* slower at working, and there is exactly one
place to look when asking why.

`Pawn.MovePerTick()` is already virtual and already the single place movement speed is decided, so
one of the two seams is half-cut. **Virtual, because Sim classes are (`CLAUDE.md`), and because a
test needs to pin a rate without constructing a whole colonist to do it.**

---

## 3. Work speed

### 3a. The curve

`rate = base + slope × level`, clamped to a floor, per work type, all three fields on the Def.
**The shape is not in doubt: every work-speed stat in the reference is dead linear in the level,
with no diminishing returns anywhere** (`work-speed-and-stats.md`, 2026-09-17). The diminishing
returns are all on *acquiring* levels — the daily soft cap and the decay above ten — which is
machinery we already have.

| Stat | Reference curve | Level 0 | Level 8 | Level 20 | L20 ÷ L0 |
|---|---|---|---|---|---|
| **Mining** | 4% + 12%/level | 4% | 100% | 244% | 61× |
| **Plant work** (covers felling) | 8% + 11.5%/level, floor 10% | 10% | 100% | 238% | 24× |
| **Construction** | 30% + 8.75%/level | 30% | 100% | 205% | 6.8× |
| **General labour** (hauling) | flat, **no skill term** | 100% | 100% | 100% | 1× |

**The slopes are deliberately unequal and the reason is design, not arithmetic.** Mining is the
steepest curve in the reference — a specialist's job where skill is the whole story. Construction
is the shallowest, because there skill buys *quality and a lower failure rate* instead of
throughput. Hauling has no speed stat at all; it is move speed and carrying capacity. The rule the
reference makes explicit through its general-labour stat: **a skill drives either rate or quality,
rarely both.** That is worth keeping, and it is the reason `U26`'s still-open success roll is a
different question rather than a piece of this one.

**Every one of those slopes is chosen so that level 8 lands on exactly 100%.** That is the
reference stating who its average colonist is, and it is the sentence §3b turns on.

**A correction to our own research, recorded rather than quietly fixed.**
`a-04-building-and-materials.md` §4 says construction speed is *"50% at skill 0, +15 percentage
points per level (skill 8 → 170%, skill 20 → 350%)"*. That is contradicted by
`work-speed-and-stats.md`, which reads 30% + 8.75%/level, and **the contradiction is decidable
without a third source**: a-04's figures put level 8 at 170%, breaking the level-8-equals-100%
invariant that holds across all four stats. The newer reading is taken; a-04's line should be
struck through when someone next touches that file. It changes nothing we have built, because
nothing has ever read a construction speed.

Two further findings that bear on the design and are taken as read below: **deconstruction** is
construction speed applied to work derived from the build cost and clamped (never instant, never a
day), and **roof work** is construction speed × 1.7 — both of which our `WorkToDeconstruct` and
`U29` can adopt without new mechanism.

### 3b. Taking those curves verbatim would make this game four times slower, and that is the finding

**The reference anchors its curves on a level-8 colonist. Ours start at level 1.** Every slope in
the table above is picked so that level 8 reads 100%, which is a statement about who an average
colonist is — and our roll has **mean level 1.16** (`Colonist.xml:80`, INVENTED, weighted low on
the reasoning that "a colonist's life before the crash was mostly not this particular trade").

Read our starting colony off the reference's curves and it is crippled:

| At level 1 | Reference rate | A job that costs | would take |
|---|---|---|---|
| Felling | 19.5% | 800 ticks | ~4,100 ticks |
| Mining granite | 16% | 700 ticks | ~4,400 ticks |
| Building a wall | 38.75% | 135 ticks × stuff | ~350 ticks |

Everything downstream is calibrated on the current speed: the wood economy, the ten-day soak's food
budget, `a-08`'s "five colonists eat 15 rations a day", the `OneDay` golden, the demo. **Dropping a
5× brake on every colonist is not a balance tweak, it is a different game**, and it would be
discovered as a failing soak rather than as a decision.

Two ways to fix it, and they are not equivalent:

- **Re-weight the starting roll upward** so that a starting colonist sits where the reference's
  does (level 4–8). This changes who a colonist *is* — it makes every arrival a tradesman — and it
  lands squarely in `U40`'s colonist-select screen, which is about telling three candidates apart.
- **Anchor the curve on the colony we actually have.** Keep the shape, choose `base` and `slope`
  so that a *starting* colonist works at about today's speed and a mastered one is properly fast.

**Backed: anchor the curve on our own average colonist, and keep the reference's relative
character.** The starting roll is the owner's characterisation of the setting and should not be
bent to fit an imported constant; anchoring keeps every existing tuning number, golden and soak
result meaningful. But the *ordering* of the slopes is a design statement worth importing whole —
mining steepest, construction shallowest, hauling flat — so this is a re-anchoring, not a
flattening. Proposed, all per mille, all **INVENTED**, because no reference number survives the
move from a level-8 anchor to a level-1 one:

| Work type | base | slope | L0 | **L1 (modal)** | L5 | L10 | L20 | L20 ÷ L0 |
|---|---|---|---|---|---|---|---|---|
| **Mining** | 550 | 105 | 0.55× | 0.66× | 1.08× | 1.60× | 2.65× | 4.8× |
| **Cutting** (felling) | 600 | 100 | 0.60× | 0.70× | 1.10× | 1.60× | 2.60× | 4.3× |
| **Construction** | 700 | 75 | 0.70× | 0.78× | 1.08× | 1.45× | 2.20× | 3.1× |
| **Hauling** | 1000 | 0 | 1.00× | 1.00× | 1.00× | 1.00× | 1.00× | 1× |

A novice is meaningfully slow, a mean starting colonist is 20–30% slower than the game is today, a
colonist who has worked a season is faster than it has ever been, and mastery is about 2.5× rather
than the reference's 2–2.4× *over its own average* (which is 60× over its own novice).

**Hauling at a flat 1.0 is not laziness, it is the reference's answer and ours agreeing.** The
reference's general-labour stat carries no skill term, and `15-skills.md` §6 independently
concluded — from the icon sheet, of all things — that **hauling is a work type and not a skill**.
Two lines of reasoning arriving at the same place is the strongest evidence in this document, and
it means this work neither needs nor blocks the skill-table reshuffle that §6 leaves open.

**Accepted by the owner, 2026-09-17** — *"sure we start somewhere"*. They are a starting point and
eight integers in a Def file; the judgement that matters comes at the keyboard, not here.

**The full work rate is the curve times condition:**

```
WorkRatePerMille(workType) = clamp( curve(level) × ConditionPerMille() / 1000 , floor, ceiling )
```

Condition is §4c and is shared with movement.

### 3c. What it costs, stated before it is spent

- **Every golden hash moves** (work banked at a different scale, and colonists finishing at
  different ticks). One deliberate re-bake with `ODYSSEY_REGOLDEN=1`, and `Golden.cs` carries the
  sentence saying why — the procedure OQ-50 and the edifice-save change already established.
- **The ten-day soak must be re-run on three seeds** and its economy re-checked. At a mean rate of
  0.7× the early colony is slower at everything; if it starves where it used to survive, that is
  the feature working and the tuning being wrong, and the fix is the anchor, not the mechanism.
- **The save format changes** (two integers at a new scale). It is a v2 addition in the same family
  as `SaveRecipe`; a v1 or v2 file must load, and the sensible reading of an old value is `× 1,000`.

### 3d. The swing, which is the other half of what the owner asked for

**Presentation scales the stroke clock by the published rate. The simulation does not know what a
swing is, and must not learn.**

```
figure.SwingClock += deltaTime * rate / 1000     // PawnFigureDirector.cs:924
```

One line. A fast worker visibly swings faster, a novice labours, and because `BlowLanded` fires off
the stroke phase, the chips and the impact audio follow for free — audio already has a per-sound
cooldown, so a 2.6× miner does not machine-gun the mixer.

**Why not the tighter design — one blow, one quantum of work?** It is the obvious idea and it
couples the wrong two things. The stroke cadence is a drawing constant (`WorkStyle.StrokeSeconds`,
different per tool, jittered per pawn so a gang does not beat in unison); making it the unit of
work would put a presentation number in the tick, in the save and in the hash. The project has
already refused this exact trade once, in `JobDef.settleTicks`: *"a simulation constant chosen to
cover a drawing constant is an uncomfortable coupling and it is the lesser one — the alternative is
presentation reaching into job timing."* Here the lesser evil is the other way round: **work stays
continuous per tick, the swing is scaled to match it, and the two agree in aggregate without either
owning the other.** The visible consequence is that a blow can land on a tick where no work
completes, which no player can see.

**The rate reaches presentation as a `PawnAspect`** — `odyssey.pawn.rate.work`, minted the way
`SkillAspects` mints `odyssey.pawn.skill.mining.level` — so `Sim.Contracts` does not change and
`PawnView` does not widen. That seam (OQ-45, ADR 0004) exists for exactly this.

### 3e. Three things the reference does that we take, and two we refuse

**Taken: the composition order.** Base, then every additive offset, then every multiplicative
factor, then the clamp. Ours is the same order with fewer terms, and writing it down now is what
stops the fifth factor being inserted wherever it happens to be convenient.

**Taken: the counter model.** Work is a counter denominated in ticks-at-standard-rate, incremented
by the finalised rate each tick — **not** a total divided by the worker's speed when the job
starts. That is what lets a rate change mid-job land correctly (a colonist who grows tired halfway
through a rock slows down for the rest of it), and it is the model our `workToClear = 700` already
is. §2a reaches the same conclusion from our own constraint that work is banked on the cell.

**Taken: capacity weighting, when health arrives.** A capacity does not multiply the rate raw; it
is weighted by how much that job depends on it, and the weights differ per stat — manipulation
matters fully and is uncapped, sight matters partially and is capped at 100% so good eyes never
make you faster. §4e's seam should adopt that shape rather than a flat multiply.

**Refused: mining as rock hit points.** The reference mines by damaging the rock — a fixed damage
per strike at an interval that shortens with speed — which is a second mechanism with its own
state, save and hash, reaching the same place our work counter already reaches. There is no design
gain in it for us, and it would make mining the one job that works differently from every other.

**Refused, but recorded because it is a real argument against §4c: mood does not affect work speed
in the reference, and that is deliberate.** The mood-driven work-speed bonus existed and was
*removed*, on the reasoning that mood should produce visible events rather than an invisible
percentage tax on everything. §4c proposes exactly such a tax for movement, driven by exhaustion
and starvation. **The difference this design relies on is that ours will be visible** — the
inspect pane says what a colonist's move rate is and why — and if that turns out not to be enough
to make it legible in play, this paragraph is the reason to drop it rather than tune it.

**Not yet, but named: darkness.** The reference scales work speed with light, from ×0.3 in the dark
to ×1.0 at 30% light. We have a day/night cycle that is **presentation only** and nothing in the
simulation knows whether a cell is lit. It is the natural fifth factor and the first one that would
make the clock mean something to a colonist; it needs a light model, which is M4's business.

---

## 4. Move speed

### 4a. What composes into it

```
MoveRatePerMille = 1000
    × innate    / 1000      // who this colonist is            §4b
    × condition / 1000      // how they are right now          §4c, shared with work
    × load      / 1000      // what they are carrying          (deferred, §4d)
    × health    / 1000      // what is wrong with them         (M4, §4e)
```

Fixed order, stated once, so that adding the fourth factor later is not a re-litigation of the
first three. **`innate` is movement's alone** — a colonist who walks quickly is not thereby a
quicker carpenter — while **`condition`, `load` and `health` are common to both rates**, which is
why `ConditionPerMille` is its own method (§2c) and why `load` and `health` are described here
rather than twice.

### 4b. Innate: varies between characters, and has a hard ceiling nobody would guess

Rolled once from `(world seed, pawn id)` on a new `PawnPurpose`, exactly as passions and starting
skills are, so a seed deals the same people every load and adding a roll elsewhere cannot shift it.

**The band is not free taste, it is bounded by an animation threshold.** `movePerTick = 1` is
1.5 m/s, and `Colonist.xml:36` records that the drawn walk cycle covers about 2 m/s — *"anything
faster blends the run clip in"*. A colonist 33% quicker than the baseline would therefore visibly
jog everywhere while ostensibly walking. **So innate variation is capped at ±15%** (0.85–1.15,
1.28–1.72 m/s), which is also about the true spread of human walking pace, and **everything above
2 m/s is reserved for a deliberate run.**

### 4c. Condition — decided 2026-09-17, and it applies to both rates

The owner asked for condition *and* health, and when asked whether condition should bite before
health exists answered: *"Yes — if exhausted, starving etc, all has an effect."* **So condition
multiplies both the work rate and the move rate**, from one shared `ConditionPerMille()` (§2c),
rather than movement only as this design first proposed.

Health is M4 and does not exist. **Needs do**, and they are the honest half of "condition"
available now. Proposed, all INVENTED, all on the same shape as every other need effect:

| Condition | Factor |
|---|---|
| Rest below 15% | ×0.85 |
| Food below 10% (starving) | ×0.80 |
| Floor on the product of all condition factors | **×0.70, never lower** |

**The floor is the whole safety argument, and applying condition to work as well makes it matter
more, not less.** A slower colonist eats later, which makes it hungrier, which makes it slower — and
now also chops its firewood and cooks its meal more slowly, so the spiral has a second turn in it
that the movement-only version did not. A floor of 0.70 keeps the compounded worst case at 0.70 on
both rates rather than 0.49 on the pair, which is the difference between a colonist having a bad
day and a colonist who cannot recover. **This must be proved on the soak before it is believed**,
on the same three seeds, against a run with the condition factors disabled — and that comparison
is `U44`'s done criterion rather than a nice-to-have.

**The counter-argument is real and is recorded in §3e**: the reference removed its mood-driven
work-speed tax precisely because an invisible percentage on everything is bad design. The bet this
takes is that ours is visible — the inspect pane says what a colonist's rate is and why. If it is
not legible in play, drop it rather than tune it.

### 4d. Load, deferred on purpose

Carrying a full stack ought to slow a colonist. It is one more factor in the same product and it is
deliberately **not** in the first unit: hauling is the single most common job in the game, the
stockpile throughput numbers in the soak all assume the current speed, and it should be introduced
against a baseline that has already absorbed the other three changes. Named here so it is not
rediscovered as an omission.

### 4e. Health, and the seam it will arrive through

`a-02-health.md` has the reference model: eleven capacities derived from a body-part tree, of which
**moving** governs move speed and **manipulation** governs work speed, with consciousness
multiplying both. When M4 builds it, it multiplies in at `health` in §4a and at the end of
`WorkRatePerMille`, and **nothing else in either feature changes.** That is the point of fixing the
composition order now, while there is only one caller.

### 4f. Running

**A run needs no new animation work and no new gait code** — the figure's gait blend reads actual
speed and blends the run clip in above ~2 m/s, which is already how the 3 m/s experiment looked
before `movePerTick` was reduced to 1 (`Colonist.xml:36`). A run is therefore a *rate*, not a
state machine: something multiplies the rate above 1.33× and the colonist is visibly running.

What is missing is the *reason* to run, and that is a simulation question the slice has not
reached: fleeing, a mental break, an emergency job, a drafted order.

**Parked by the owner, 2026-09-17** — asked what should make a colonist run, the answer was *"not
sure yet"*, which is the right answer to a question about a game that has no danger in it yet.
**So `U45` is held rather than scheduled**, and the rule that holds while it is held: **do not
invent an urgency model.** The capability costs nothing to keep available — it is a multiplier on a
rate that will already exist after `U44`, and the gait blend already does the rest — so nothing is
lost by waiting until there is something worth running from.

### 4g. The trap: do not double-count terrain

Terrain cost is already live and belongs to the **cell being entered** — clear ground 0, marsh +40,
shallow water +200, so wading is a third of walking (ADR 0009, `05-ai-and-jobs.md` §"Terrain cost
is live"). **That is a property of the ground, not of the pawn, and it must stay in the step cost.**
A "this colonist is slow in water" factor would be counted twice, once by the planner choosing the
route and once by the mover paying for it, and the planner's route would then disagree with the
mover's price — which `HopPriceHasOneOwnerTests` exists to make impossible for hops and which
nothing guards for terrain. **Rate multiplies the pawn's progress; cost prices the cell. Never the
reverse.**

---

## 5. What reaches the interface

- **Two new aspects**, `odyssey.pawn.rate.work` and `odyssey.pawn.rate.move`, per mille, published
  by `PawnRegistry` beside the nine skill rows it already publishes. Two more rows a colonist.
- **The inspect pane's Skills tab** gains the thing that makes it worth opening: a skill row can
  say what its level is *worth* ("mining · 6 · 1.20×"), which is the first time a level in this
  game has had a consequence to state.
- **Names are content, and content obliges the wiki.** Any label this introduces — a stat name, a
  tooltip, "work speed", "move speed" — is a key in `docs/design/icon-keys.csv`, and the commit
  that adds it runs `build_wiki.py --check` and `emit_labels.py --check`. Proposed keys:
  `ui.stat.workSpeed`, `ui.stat.moveSpeed`. **Nothing in this document adds a key yet**, because
  adding one now would make the wiki describe a game that does not have it.

---

## 6. What this deliberately does not do

Named so a later session does not read them into the scope: **quality** (a built thing is a built
thing; `U26`'s success roll is still open and is a different question), **yield** (a better miner
does not get more ore), **the passion mood buff** (`a-01`: about +8 / +14 while working a passion
skill — real, cheap, and a `NeedsSystem` change rather than a rate one), **traits** (`a-01`'s
work-drive spectrum, −35% to +35% global work speed, is the fifth factor and wants traits first),
**health** (§4e), **load** (§4d), and **the skill-table reshuffle** that `15-skills.md` §6 leaves
open — hauling ceasing to be a skill, cutting feeding growing. This work must neither depend on
that reshuffle nor block it: it reads `SkillIndex` as it stands, and if the table changes the
curves move with it.

---

## 7. Answered by the owner, 2026-09-17

1. **The anchor** (§3b) — *"sure we start somewhere"*. The eight proposed integers are taken as the
   starting point: a novice at 0.55–0.7×, a master at about 2.5×, mining steeper than building.
   They are INVENTED and they are Def fields; the real judgement is at the keyboard, and changing
   them later costs a re-bake, not a rewrite.
2. **Condition bites** (§4c) — *"if exhausted, starving etc, all has an effect"*. Taken further
   than this design first proposed: condition multiplies **both** the work rate and the move rate,
   from one shared method. The soak comparison is `U44`'s done criterion, because that is where a
   starvation spiral would show up.
3. **Running is parked** (§4f) — *"not sure yet"*. `U45` is held, and the standing rule while it is
   held is that nobody invents an urgency model to fill the gap.

### Still open

- **Whether mining should feel different from building at all.** The reference says yes and says it
  loudly — 61× novice to master on mining, 6.8× on construction. The proposal keeps that ordering
  at a quarter of the spread. Not a blocker: it is the same eight integers as question 1, and the
  answer will come from playing `U43`.
- **Starting colonists** (§3b). Anchoring the curve is the alternative to re-weighting the roll; if
  `U40`'s three candidate cards turn out to look samey, the roll is the knob, not the curve.

---

## 8. How it will be proved

Test-first, per the standing convention. The tests that must exist before the code:

- **Rate 1,000 is bit-identical to today.** With every curve flattened to a constant 1,000, the
  goldens do not move. This is the control that separates "the mechanism" from "the tuning", and it
  is what makes the later re-bake readable as deliberate.
- **A level-20 colonist finishes a fixed cell in provably fewer ticks than a level-0 one**, and the
  ratio equals the curve to the tick.
- **Two colonists of different skill on one cell.** Work banked on the cell is in the same unit for
  both; the cell completes when their contributions sum to the cost, in whatever order they arrive
  and however many times they swap.
- **A rate of zero cannot exist**, and a rate below the floor clamps rather than stalling. A
  colonist who can never finish a job is an infinite loop in the job system.
- **Condition reaches both rates from one place.** A colonist driven to the condition floor is at
  ×0.70 on work and ×0.70 on movement, and the test asserts the floor holds when both the rest and
  the food factor apply at once — the compounded case is the one that would otherwise reach 0.68
  and keep falling as factors are added.
- **The planner and the mover still agree**, in the shape of `HopPriceHasOneOwnerTests`: no path
  changes, so every existing path checksum holds with the rate at 1,000.
- **No allocation added to the tick.** `PathAllocationTests` is the precedent — the tick is at
  11.0 bytes and a rate computed per tick per pawn must not move it.
- **The soak**, three seeds, ten days, with the economy compared against the last run rather than
  merely "no errors".
