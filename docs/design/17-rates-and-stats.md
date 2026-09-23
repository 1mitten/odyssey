# 17. Rates and stats — how a colonist's standing becomes a speed

**Status: design, 2026-09-17. Nothing here is built.** Written from two owner questions asked in
the same conversation:

> "chopping/cutting (animation and game) — dependent on your skill level determines how fast you
> swing and how quickly you chop wood, but every time you do, your experience slowly goes up. Is
> there anything that determines this in the plan yet?"

> "We should also have a move speed (to determine walking and running) that varies between
> characters and also is dependent on their condition/health."

The answer to the first is **half**: experience is fully built and **no rate reads a level back out**.
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
| **Experience** | Fully built. A driver calls `Work(ctx)` on the ticks that are work and not on the walk to it; the pawn earns `experiencePerWorkTick` (110 thousandths for every working job, ASSUMED) × learning factor × passion (×0.35 / ×1.0 / ×1.5) × 0.2 once the day's soft cap is passed. | `Job.cs:264`, `Pawn.cs:284` |
| **Level** | Read off experience by a Def table, never stored; decays above level 10 on the Long cadence. Saved and hashed. | `PawnContent.cs` `SkillDef`, `SkillSystem.cs` |
| **Starting level** | Rolled once on the first tick from a weighted table, deterministic on `(world seed, pawn id)`. **Mean level 1.16**; levels 6–7 together are a 2% roll. | `StartingSkillsSystem.cs`, `Colonist.xml:80` |
| **Work rate** | **Does not exist.** Every work tick adds exactly **1**, whoever is working and whatever they know. | `MineJob.cs:342`, `BuildJob.cs:442`, `DeconstructJob.cs:123`, `JobDrivers.cs:283` |
| **Skill as a gate** | **A level is read, but never as a rate.** `BuildWorkGiver.CanBuild` refuses to offer a site to a colonist below the building's `minSkill` — a thing you cannot make is not offered rather than offered and botched. **Inert today**, because every shipped building is `minSkill = 0`. Found 2026-09-17 by auditing this document against the code, and it corrects this document's own first draft, which said nothing read a level at all. | `BuildJob.cs:213` |
| **Skill as a number on a card** | `U40`'s colonist-select screen reads every candidate's level to print it. A *display*, not a consequence — but it is the screen that makes the absence of a consequence visible, because three candidates differing only in numbers that do nothing is a choice without stakes. | `HudShell.Start.cs:109` |
| **Move rate** | A single content constant shared by every colonist: `movePerTick = 1` against a flat crossing of 100, i.e. 1.5 m/s. | `Pawn.cs:247`, `Colonist.xml:43` |
| **Condition** | Needs, mood and thoughts all run. **Health does not exist** — a pawn cannot be hurt, so there are no capacities to read. | `NeedsSystem.cs`; `a-02-health.md` is research only |
| **The swing** | Presentation, and decorative. `figure.SwingClock += deltaTime` against a fixed `StrokeSeconds` per tool, jittered per pawn so a work gang does not beat in unison. It is not derived from, and does not affect, the work being done. | `PawnFigureDirector.cs:924`, `WorkStyle.cs:138` |

**So the owner's reading of the game is exactly right and the gap is one-sided.** A colonist does
get better at chopping — the number goes up, it is saved, it survives a reload, and since `U40`
landed the candidate cards print it. It buys nothing that can be felt. A level-20 miner and a level-0
miner clear the same rock in the same 700 ticks, swinging at the same rate.

**The one exception is worth keeping in view, because it is the shape the rest should follow.**
`minSkill` gates *eligibility*, not speed: below the bar the job is never offered. That is the
reference's own division — **a skill drives either your rate or what you are allowed to attempt,
and the two are separate mechanisms** — and it means this work is adding the missing half rather
than the only half. It also means the hook for "a novice may not attempt the reactor" already
exists and needs nothing from this design.

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
`ConstructionContent.cs:220`. None of those numbers move. What moves is how much of that cost one
tick of one colonist's labour discharges.

This is forced rather than chosen, and the code already decided it: **work is banked on the cell,
not on the job** (`DesignationGrid.cs`, "On the cell, not on the job, and that is the point" — a
miner who breaks for a meal must not lose the morning's work, and the next colonist must be able to
finish the cell). A cell worked by two colonists of different skill therefore has to accumulate in
a unit that means the same thing to both of them. **That single fact rules out the obvious
alternative** — dividing the job's total by the worker's speed at job start, the way one might
naively read the reference — because the total would then depend on who happened to pick the job up.

**Re-checked against `main` on 2026-09-17, after `U29`'s floors and collapse landed.** A slab is a
new buildable with its own `workToBuild`, and it added **no new work call site** — it banks through
`sites.AddWork(cell, 1)` like everything else. So the three callers this design has to change are
still three, and a feature built between the writing of this document and the doing of it went
through the seam rather than around it. That is the cheapest evidence available that the seam is in
the right place.

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

### 2bb. Where the scale stops — and four places that would break if it did not

**Audited against the code on 2026-09-17**, after this design was first written and found to have
named none of them. The rule that settles all four:

> **The scale is internal. `_work[cell]` and `MoveProgress` count thousandths; everything that
> crosses the sim→UI contract, and everything a human reads, stays in ticks-at-standard-rate.**

Without that rule, in the order a session would hit them:

1. **`CellDetail.WorkToClear` is a `ushort`.** The dearest terrain in `Terrain.xml` costs 2,400,
   and 2,400 × 1,000 does not fit in sixteen bits. The published field is the terrain's *cost*, not
   banked work, so it stays in ticks and the overflow never happens — but only because the rule
   above is followed, and it would be a silent wrap if it were not.
2. **`SiteView.WorkDone` / `WorkTotal` are documented as "real ticks"** in `Views.cs`, and
   `InspectModel` turns them into *"about 12s left"*. Publishing milliwork through them would
   multiply every estimate in the interface by a thousand. They are divided back at publish.
3. **`DesignationGrid.Fraction()`** divides banked work by `WorkFor()`. One side scales and the
   other does not, so the denominator gains the `× 1,000` or every progress bar fills a thousand
   times too fast. This is the one that is easiest to miss, because the wrongness is invisible
   until something is half dug.
4. **`PawnRegistry` publishes `movePercent = MoveProgress × 100 / MoveStepCost`**, which is how
   presentation glides a figure across a step. It is a ratio, so it is only correct if **both**
   sides scale — and `MoveStepCost` is assigned the raw nav cost and defaults to
   `MoveCost.Orthogonal`. Scale `MoveProgress` alone and every colonist teleports.

**The consequence for the interface is a wording question, not a bug** (§5): once a rate exists,
*"about 12s of work"* means *for a colonist working at the standard rate*, and a good miner will
beat it. It has been an exact statement of fact since the readout shipped and it stops being one.

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
| **Growing** | 600 | 100 | 0.60× | 0.70× | 1.10× | 1.60× | 2.60× | 4.3× |
| **Hauling** | 1000 | 0 | 1.00× | 1.00× | 1.00× | 1.00× | 1.00× | 1× |

A novice is meaningfully slow, a mean starting colonist is 20–30% slower than the game is today, a
colonist who has worked a season is faster than it has ever been, and mastery is about 2.5× rather
than the reference's 2–2.4× *over its own average* (which is 60× over its own novice).

**Hauling at a flat 1.0 is not laziness, it is the reference's answer and ours agreeing.** The
reference's general-labour stat carries no skill term, and `15-skills.md` §6 independently
concluded — from the icon sheet, of all things — that **hauling is a work type and not a skill**.
Two lines of reasoning arriving at the same place is the strongest evidence in this document, and
it means this work neither needs nor blocks the skill-table reshuffle that §6 leaves open.

**Growing arrived on 2026-09-19 with the growing work itself and took cutting's row verbatim**,
which is why the two lines are identical. It is the "plant-work curve" `22-growing.md` §5 named as
pending: growing shipped with `Work_Growing` carrying no `rateSkill` at all, so a master grower
sowed at exactly a novice's speed and the whole Growing skill bought nothing. The reasoning for copying rather than inventing is
that **they are the two plant work types**, and the design had felling training Growing outright until
2026-09-18 (`15-skills.md` §6.2) — so a difference between them is not a default, it is a claim, and
there is no measurement to support one. It scales the labour of sowing and reaping only; a plant's
`growTicks` is untouched, so a skilled grower works a field faster without hurrying the season.

**Accepted by the owner, 2026-09-17** — *"sure we start somewhere"*. They are a starting point and
ten integers in a Def file; the judgement that matters comes at the keyboard, not here.

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

**Taken, and it is the sharpest thing in this document: a condition either does nothing to your
rate or it produces a visible, discrete event. Never an invisible percentage tax.** The
mood-driven work-speed bonus existed in the reference and was *removed* on exactly that reasoning,
and the follow-up research found the same rule applied again where nobody would expect it —
**tiredness affects no work or movement stat at all**; at zero rest the colonist collapses instead.
The one condition allowed to slow you is starvation, and only because it has crossed out of being
a *need* and become an *injury* with a name and a severity bar.

This is what shaped §4c, which originally proposed a graded slowdown for both hunger and
exhaustion. Hunger keeps its slowdown and gains the reference's mechanism; **exhaustion loses its
slowdown and gains a collapse**, which is a better answer to "exhaustion should have an effect"
than a number nobody can see.

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

### 4c. Condition — decided 2026-09-17, and the research then split it in two

The owner asked for condition *and* health, and when asked whether condition should bite before
health exists answered: *"Yes — if exhausted, starving etc, all has an effect."* **So condition
multiplies both the work rate and the move rate**, from one shared `ConditionPerMille()` (§2c),
rather than movement only as this design first proposed.

**Then the follow-up research came back and said the two halves of that sentence work completely
differently in the reference** (`work-speed-and-stats.md`, follow-up section). The finding comes
before the design here, because the design is a consequence of it.

- **Hunger slows you, by exactly the path this design guessed.** Starvation is not a need effect at
  all: at 0% food it becomes a *condition with a severity bar* — an injury, in effect — and its
  whole mechanical action is an **offset to consciousness**, −10% minor, −20% moderate, −30%
  severe. Consciousness feeds **moving** (which drives move speed) and **manipulation** (which
  every work-speed stat applies at full weight, uncapped). One offset, both rates. The mood hit
  rides alongside and causes none of it.
- **Exhaustion does not slow you at all.** Tiredness affects no work or combat stat in the
  reference — the rest bands do mood and disease immunity and nothing else. What it does instead is
  **stop you**: at zero rest a colonist collapses and sleeps where they stand. Full output, full
  output, full output, then a body on the floor.

**That is one philosophy applied twice, and it is the same one behind the removed mood tax
(§3e): a condition either does nothing to your rate or it produces a visible, discrete event.**
Hunger is the exception that proves it, and it is allowed to slow you only because it has stopped
being a need and become an injury with a name and a bar.

#### What this design takes

**Hunger: the path, not just the outcome.** `ConditionPerMille()` is a single scalar — the
stand-in for consciousness until M4 builds capacities — and starvation offsets *it*, not the two
rates separately. Neither the work rate nor the move rate ever learns that hunger exists, which is
the property that makes M4's arrival a substitution rather than a rewrite.

| | Effect on `ConditionPerMille()` |
|---|---|
| Food at 0% (starving), by severity | **−100, −200, −300** |
| Ceiling | **1,000. Nothing may raise it above baseline** — being dulled slows you, being alert never speeds you up. That asymmetry is the reference's and is worth keeping |
| Floor | **700, never lower** |

**The compounding is now evidence rather than a worry.** Because one scalar reaches both rates, a
starving colonist walks at 0.7 *and* works at 0.7, so a walk-then-work round trip runs at about
**0.49 of its throughput** — which is what the reference does at severe malnutrition too. The floor
at 700 is ours and is a deliberate departure: the reference has no soft landing, it has
*thresholds* — consciousness below 30% and the colonist is unconscious. **We have no downed state
to fall through**, so a floor stands in for one, and it should give way to a threshold on the day
health exists.

**Exhaustion: not a rate penalty. Collapse.** This is a departure from the literal reading of the
owner's answer and it is flagged as such in §7 — a graded slowdown for tiredness is invisible and
unattributable, where a colonist face-down in the mud with an alert is legible at a glance, and the
reference reached that conclusion deliberately rather than by omission. It is also a *smaller*
change than it sounds: `SleepJobDriver` already sets `Pawn.Asleep`, and nothing in the game can
currently collapse — a tired colonist walks to a bed — so the whole of it is "at zero rest, sleep
here instead of walking there".

**The soak proves it or the tuning is wrong.** Three seeds, ten days, against a run with condition
disabled, both economies recorded. The spiral is real in the reference and is braked by three
things: a long runway, symmetric recovery with no point of no return, and collapse itself handing
the colonist to somebody else to feed. **We have the runway** (`a-08`: 72.5 hours from full to
starvation). The other two want checking rather than assuming, and the second of them is another
argument for collapse being a behaviour rather than a number.

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
**So `WS4` is held rather than scheduled**, and the rule that holds while it is held: **do not
invent an urgency model.** The capability costs nothing to keep available — it is a multiplier on a
rate that will already exist after `WS3`, and the gait blend already does the rest — so nothing is
lost by waiting until there is something worth running from.

**The first reason arrived on 2026-09-23: a drafted colonist runs** (owner, after the first draft
playtest: *"when you are drafted you should walk faster/run as this would make sense with the
urgency"*). It is exactly the rate this section describes — `Pawn.UrgencyPerMille`, the last factor
in `MoveRatePerMille`, 2,000 per mille while drafted and 1,000 otherwise — and nothing more: fleeing,
breaks and emergency jobs are still unanswered, and they answer through the same method when they
come. `docs/design/33-combat.md` §2h.

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
- **"Walk speed" is already taken, and by the other thing.** The tile readout has said
  `walk speed = 100%` since cell inspection shipped, and it is a fact about the **cell** — the
  terrain's crossing cost — not about anybody standing on it. A pawn move rate arriving under the
  same words would put two different meanings of "walk speed" in one interface, which is §4g's
  double-counting trap in its user-facing form. **The cell keeps the phrase** (it was there first
  and it is the more surprising fact), and the pawn's own figure wants different words — *pace*,
  or the stat name from `ui.stat.moveSpeed`. Decide it when `WS3` writes the row, and do not let
  the two meet unlabelled.
- **Two work estimates stop being exact** (§2bb): *"minable — about 12s of work"* on a tile and
  *"about 12s left"* on a build site. Both are computed from a work total in ticks and have been
  precisely true; once colonists differ they become *"for a standard colonist"*. Either say so, or
  compute the estimate against the selected colonist's rate when there is one. The second is
  nicer and is not free — it makes a tile's readout depend on the selection, which is a change to
  what the pane is.
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
   from one shared method. The soak comparison is `WS3`'s done criterion, because that is where a
   starvation spiral would show up.

   **One part of this needs the owner's veto rather than their approval.** Follow-up research
   found that in the reference **exhaustion does not slow anybody down** — tiredness touches no
   work or movement stat; at zero rest a colonist collapses and sleeps where they stand. §4c
   therefore gives starvation a slowdown and gives exhaustion **a collapse instead of a number**,
   on the argument that a colonist face-down in the mud is a visible effect where a hidden −15% is
   not. That is still "exhaustion has an effect", but it is not the effect the answer literally
   asked for, so: **say if you want the slowdown as well and it goes back in** — it is one row of
   a table either way.
3. **Running is parked** (§4f) — *"not sure yet"*. `WS4` is held, and the standing rule while it is
   held is that nobody invents an urgency model to fill the gap.

### Still open

- **Whether mining should feel different from building at all.** The reference says yes and says it
  loudly — 61× novice to master on mining, 6.8× on construction. The proposal keeps that ordering
  at a quarter of the spread. Not a blocker: it is the same eight integers as question 1, and the
  answer will come from playing `WS2`.
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
- **Condition reaches both rates from one place.** A starving colonist is at ×0.70 on work and
  ×0.70 on movement from a single scalar, and the test asserts the ceiling as well as the floor —
  nothing may push `ConditionPerMille` above 1,000, which is the asymmetry that stops a future
  "well fed" bonus quietly becoming a speed boost.
- **A colonist at zero rest collapses where it stands** rather than walking to a bed, and wakes
  where it fell. The control is that a merely tired colonist still walks to the bed, because the
  failure mode of this feature is every colonist sleeping in the mud.
- **The planner and the mover still agree**, in the shape of `HopPriceHasOneOwnerTests`: no path
  changes, so every existing path checksum holds with the rate at 1,000.
- **No allocation added to the tick.** `PathAllocationTests` is the precedent — the tick is at
  11.0 bytes and a rate computed per tick per pawn must not move it.
- **The soak**, three seeds, ten days, with the economy compared against the last run rather than
  merely "no errors".
