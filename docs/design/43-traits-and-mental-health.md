# 43 — Traits and mental health: who a colonist is, and what breaks them

Branch `claude/peaceful-lamport-58fozw`. Interview 2026-09-25 (`docs/research/traits-interview.md`),
research the same day (`a-18-traits.md`, `a-19-mental-breaks.md`, `b-mental-health-models.md`,
`g-04-mood-and-traits-ui.md`), built the same day on the owner's *"yes implement"*.

**Status.** Built 2026-09-25, `TM0`–`TM6` (plan `docs/plans/traits-and-mental-health.md`),
**not yet run in Unity and not yet played**. The trait list is a **placeholder set of our own,
every number INVENTED**, waiting for the owner's table in the interview file (§4c).

## 1. Why

The brief's first pillar (§5 item 1) is pawns with thoughts, traits and mental breaks. M2 shipped
the minimum — an integer mood drifting toward a target, eight memories, one threshold, one break
(wander) — and deferred the rest to M7. Two things made it worth doing now:

- **Combat added thoughts nobody can see.** A colonist struck by another loses 8 points for a day
  and nothing on screen says why (design 33 §12b: *"the Thoughts tab that would name it is still
  disabled"*).
- **Colonist select has had an empty Traits section since U40**, and the owner said the screen
  becomes worth playing *when traits do*.

## 2. What the owner decided

| Topic | Decision |
|---|---|
| Scope | **Traits; the three break tiers and a small taxonomy; the Thoughts tab.** Not the passion mood buff (still `17-rates-and-stats.md` §6's hook). Not social or opinion. |
| Old saves | **New colonies only.** A colonist from an older save has no traits and behaves exactly as before. |
| First breaks | **Sulk (minor), binge (major), tantrum (major), berserk (extreme)**, beside wander (minor). |
| What a trait may change | **Permanent mood, the break threshold, learning speed, work speed, incapable of a work type.** Walk speed held. |
| How many | **Two or three**, from the colonist's own seed. |
| Where shown | **Colonist select's detail pane**, rerolled with the card, and **the inspect pane**. Not on the candidate card. |
| Supply | **A table the owner fills in** (the interview file). Numbers are ours and INVENTED. |
| Resting mood | **Fix first: a fed, rested colonist with nothing on her mind is Content.** |

Our calls, not the owner's, each argued below: a trait gates nothing and weights nothing yet
(§5d); cascades run through ordinary memories (§5e); the list of thoughts is published for every
colonist rather than on a query (§4d); the break's duration is one draw, not a recovery clock (§5c).

## 3. What moves, and what does not

- **Saved:** a new section, `odyssey.pawn.mind`, holding each pawn's traits and the kind of break
  she is in. Skippable both ways, like `odyssey.pawn.seeds` and `odyssey.pawn.kinds`, so **no
  format bump**: a save from before this has no section and loads traitless and wandering, which
  is what it was.
- **Hashed:** the traits and the break kind, each **only while there is one** (bits 22 and 23 of
  the pawn's kind word, which combat left free for this). A colony with no traits and no breaks
  other than wander hashes exactly as it did.
- **Goldens:** traits are dealt on the first tick, as starting skills are, so **no `Generated`
  golden moves**. Every `Simulated` golden moves once, for the hash seeing the traits.
  `GoldenColonyProbe` diffed before and after says what else moved (§8).
- **Random streams:** one new purpose, `PawnPurpose.Traits`, drawn from (roll seed, pawn id). The
  break's kind and duration are drawn from the existing `MentalBreak` stream, after the draw that
  decided the break, so whether a colonist breaks is unchanged.
- **Content fingerprint** moves once (`PawnContentDefTests`): the new Defs and the break table.

## 4. Contracts

### 4a. Handles, claimed once, appended only

| Table | New | Where |
|---|---|---|
| `TraitHandle` | 0–12, the placeholder set (§4c) | `Sim.Contracts/Catalogue.cs`, `PawnContent.Traits`, `Hud/TraitCatalogue.cs` |
| `BreakHandle` | Wander 0, Sulk 1, Binge 2, Tantrum 3, Berserk 4 | `Sim.Contracts/Catalogue.cs`, `PawnContent.Breaks`, `Hud/MindCatalogue.cs` |
| `ThoughtHandle` | the eight memories 0–7, as `ThoughtIndex` already numbers them | `Sim.Contracts/Catalogue.cs`, `Hud/MindCatalogue.cs` |
| `IncidentHandle.MentalBreak` | 4 | a recorded incident, never fired, like `Theft` |
| Registry | `ui.trait.*` (13), `ui.thought.*` (8 memories, 4 situational), `ui.break.*` (5), `ui.bulletin.mentalbreak`, `ui.inspect.traits` | `docs/design/icon-keys.csv` |

A test on each side holds each list to its count, because nothing in either assembly can.

### 4b. Numbers

| Def | Field | Value | Source |
|---|---|---|---|
| `MoodDef` | `breakThreshold` (the minor line) | 350 | unchanged |
| | major line | minor × 4 / 7 = 200 | a-19 (the reference's ratio) |
| | extreme line | minor / 7 = 50 | a-19 |
| | `breakMtbTicks` minor / major / extreme | 240,000 / 48,000 / 30,000 (4 / 0.8 / 0.5 days) | a-19's correction of a-01; was 600,000 |
| | `strainMargin` | 100 above the minor line | INVENTED |
| | the minor line after traits, clamped | 100 to 500 | INVENTED |
| `MentalBreakDef` | Wander | minor, weight 10, 5,000–10,000 ticks | wander's 7,500 kept as the middle |
| | Sulk | minor, weight 10, 10,000–30,000 | a-19 (4–8 h at our clock, shortened) |
| | Binge | major, weight 10, 5,000–15,000 | a-19 |
| | Tantrum | major, weight 10, 2,500–7,500 | a-19 |
| | Berserk | extreme, weight 10, 7,500–15,000 | a-19, shortened because a downing ends it |
| | tantrum reach | 1,500 (15 cells) | a-19 |
| | berserk reach | 2,000 (20 cells) | a-19 |
| `PawnKindDef` | `thirdTraitPerCent` | 30 | INVENTED |

### 4c. The placeholder traits — ours, INVENTED, waiting for the owner's table

A spectrum holds one degree per colonist. Commonality is a plain weight: common 30, normal 20, rare 8.
Mood and threshold are thousandths; the interface shows them as points out of a hundred.

| Trait | Spectrum | Effect | Commonality | Conflicts |
|---|---|---|---|---|
| Tireless | diligence | work ×1.35 | rare | Soft hands |
| Diligent | diligence | work ×1.20 | normal | |
| Unhurried | diligence | work ×0.80 | normal | |
| Cheerful | outlook | mood +60 | normal | |
| Sunny | outlook | mood +120 | rare | |
| Gloomy | outlook | mood −60 | normal | |
| Steady | nerve | minor line −90 | normal | |
| Jumpy | nerve | minor line +80 | normal | |
| Quick study | learning | learning ×1.75 | normal | |
| Slow study | learning | learning ×0.40 | normal | |
| Soft hands | — | cannot mine | common | Tireless |
| Black thumb | — | cannot grow | common | |
| Ham-fisted | — | cannot build | common | |

Every effect kind the owner allowed is exercised by at least one row. When the owner's table
arrives, their traits are appended (or these renamed in place before any colony keeps them), and
this table is replaced.

### 4d. What the interface reads

All as pawn aspects, integers, **not saved and not hashed**, published every tick for every
colonist, and sparse: a row is written only when it is not the default.

| Aspect | Value |
|---|---|
| `odyssey.pawn.mood.band` | 0 content, 1 strained, 2 breaking (minor), 3 breaking (major), 4 breaking (extreme), 5 broken |
| `odyssey.pawn.mood.target` | the target the mood drifts to |
| `odyssey.pawn.break` | the break kind, while broken |
| `odyssey.pawn.trait.<slot>` | the trait's handle, slots 0–2 |
| `odyssey.pawn.trait.<slot>.mood` / `.nerve` / `.learn` / `.work` / `.cannot` | the effects, so the interface derives no number the simulation knows; `.cannot` is a work-type bit mask |
| `odyssey.pawn.thought.<name>` | a memory's contribution, stack included |
| `odyssey.pawn.thought.<name>.left` / `.count` | ticks until its oldest copy lapses; copies held |
| `odyssey.pawn.mood.need.<need>` / `.temperature` | the situational offsets |

**Her three lines and the base are not published** (changed during the build, §6): nothing draws
them yet, and a channel is published only when something reads it (process §3). Threshold notches
on the mood bar are the reader that would bring them back.

**Published for every colonist, not on a query.** The alternative is a `QueryPawn` intent beside
`QueryCell`, which needs presentation wiring the fast tier cannot compile. The rows are bounded by
the thought, need and trait counts, typically four to eight a colonist against the fifty-seven
already published, and the aspect lookup is O(1) since design 31. Recorded as the lever if a
measurement ever says otherwise.

### 4e. Seams filled, and seams left

- `Pawn.LearningFactorPerMille()` — the product of the traits' learning factors. The seam existed
  for this.
- `Pawn.WorkRatePerMille()` — multiplied by the traits' work factor after temperature, before the
  floor, so no trait can price work at nothing.
- `Pawn.WorkPriority()` — answers 0 for a work type a trait disables. It has one caller, the work
  scan, so a disabled type is never offered. `WorkAspects.Capable` publishes the 0 the Work tab was
  already written to grey.
- `Pawn.CanMentalBreak()` and the tier — read the colonist's own lines.
- **Left:** walk speed (`MoveRatePerMille`), a trait weighting a break, a trait forcing or blocking
  a thought, skill offsets at the roll.

## 5. The mechanism

### 5a. TM1 — the band, published (fix first)

The interface copied the sim's threshold as a constant and called 600 content, so a colonist at the
resting target of 500 read *strained*. The band is now the simulation's answer: content at or above
the minor line plus the strain margin (450 for an untraited colonist), strained within the margin,
breaking below the minor line (the tier named), broken while in a break. The roster, the inspect
pane and the alert all read it; `MoodBands` keeps no threshold. **The alert latches at breaking and
clears at content**, the same hysteresis it had, now in band terms.

### 5b. TM2 — the Thoughts tab

Enabled. A heading line (the mood, the target, the direction it is drifting), then two groups:
**Now** (need bands, temperature, the traits' permanent offsets) and **Memories** (each thought
with its stack count and the time until its oldest copy lapses), each worst first. Offsets in
points out of a hundred, ASCII signs (P13). Paged at the body's height rather than scrolled. A
thought is named by `ui.thought.*` and described there, so the wiki lists all twelve.

### 5c. TM5 — tiers and the taxonomy

- **The roll.** Below the minor line a break is rolled on the needs cadence with the MTB of the
  deepest line she is under. The existing `MentalBreak` draw decides *whether*; two more draws on
  the same stream decide *which* and *how long*.
- **Which.** A weighted pick among the breaks of that tier whose requirement holds, falling through
  to the next shallower tier when none does. Binge requires food she can reach; tantrum requires a
  building in reach; sulk, wander and berserk require nothing.
- **How long.** One uniform draw between the break's minimum and maximum, written into the
  existing `BreakTicksLeft`. The reference's recovery clock (a minimum, then a mean time to
  recover, then a maximum) is recorded rather than built: one counter already saves, hashes and
  ends a break, and a second clock would buy a distribution the player cannot see.
- **What each does**, as the think node's one branch:
  - **Wander**: as before.
  - **Sulk**: walk to her own bed and stand at it, doing nothing; with no bed, stand where she is.
  - **Binge**: eat the nearest food she can reach, again and again, whatever her hunger. Nothing to
    eat: wander.
  - **Tantrum**: strike the nearest colony building within 15 cells, beds excepted (the bandit's
    rule, design 33 §16). Nothing in reach: wander.
  - **Berserk**: strike the nearest standing pawn within 20 cells, colonist or animal. The attack
    driver ends a fight nobody ordered when the target goes down, so **a berserker downs and does
    not kill**. Nobody in reach: wander.
- **The jobs a break may run** are the break's own (wander and wait for a sulk; eat and wander for
  a binge; attack and wander for the other two). Anything else is failed, as it always was.
- **Ends** at the counter's zero with catharsis, or on being downed with none (design 33 §5j).
- **Shown**: a row on the Events panel naming the colonist and the break, and the alert naming the
  tier. `ui.mood.broken` finally has a reader.

### 5d. A trait gates nothing and weights nothing, yet

`b-mental-health-models.md` recommends that traits weight breaks and never choose them, and that
is the direction. The owner's list of effects does not include it, so the weight is per break and
fixed. The field is not added until something reads it (the `PlantDef.yields` lesson).

### 5e. Cascades

A break's consequences land as the memories they already are: a berserker's blow gives her target
the friendly-fire memory; a tantrum's broken wall is a broken wall. No "saw a break" memory yet.

### 5f. TM3/TM4 — traits

- **Dealt** on the first tick for every colonist placed at generation (the starting-skills
  system), on the spot for a debug-spawned colonist, and by `ColonistDraw` for the select screen —
  all from (roll seed, pawn id, `PawnPurpose.Traits`), so the person on the card is the person who
  walks. Two, and a third on 30 per cent; each pick weighted by commonality among the traits not
  held, not in a held spectrum and not conflicting either way.
- **Only colonists.** A bandit and an animal have none.
- **A scenario can turn them off** (`ScenarioDef.traits`, on by default; decided during the build).
  Fourteen existing tests about floors, crops, forced orders, work rates and experience failed the
  day traits arrived, because their seeds dealt *Ham-fisted*, *Black thumb* or a diligence degree to
  the colonist the test was watching. Each of those fixtures now turns traits off with a one-line
  reason, and the goldens keep them on, so the hashed runs still exercise traits.
- **Shown** on the inspect pane's Needs tab under the three bars (the tab's slack; the pane stays
  one height, `HudLayoutTests`), and on the select screen's detail pane under the skills: the name,
  then the effect summary (`Work +20%`, `Mood +6`, `Breaks later`, `Learns x1.75`,
  `Cannot: Mining`), with the registry's description as the tooltip.

## 6. Cost

Everything runs on the existing 150-tick needs interval, per pawn: a trait is at most three
additions to the mood target and three multiplications on a rate. The break choice runs once per
break, and its requirement scans (items, edifice records) are the ones the eat and bandit givers
already make.

**Measured, 2026-09-25, in the container this was built in** (Linux, .NET 8, no Unity), with
`TickBenchmarkTests` at 250 x 250 x 40 and fifty pawns, this branch and `main` (f0b7133) run
alternately so each pair shares the machine's state:

| Arm | Phase | `main` | this branch |
|---|---|---|---|
| At rest | snapshot | 0.090 – 0.108 ms | 0.128 – 0.133 ms |
| At rest | whole tick | 0.130 – 0.156 ms | 0.173 – 0.181 ms |
| D1 replan rate | snapshot | 0.099 – 0.106 ms | 0.131 – 0.140 ms |
| D1 replan rate | whole tick | 0.999 – 1.188 ms | 1.093 – 1.151 ms |

**The cost is the publish, about 0.03 ms a tick at fifty colonists**, and none of it is in the
pawn phase. With the thoughts' rows switched off the snapshot read 0.115 – 0.118 ms, so the
thoughts are about half of it and the band, target, trait slots and capability rows the other
half. **No allocation was added**: the heap grew 16.4 bytes a tick on both sides. The replan arm's
whole tick overlaps between the two sides; its noise is wider than the difference. The base and
the three break lines were dropped from the publish after the first reading, because nothing read
them. **The lever, if a later measurement asks for one**, is a `QueryPawn` intent beside
`QueryCell`, so the thoughts and trait slots are published for the inspected colonist only.

## 7. Out of scope, recorded

Social and opinion; inspirations; a difficulty ladder for the base mood; walk-speed, damage and
beauty traits; trait-weighted and trait-gated breaks; fire-starting; the insult spree; give up and
leave, catatonic breakdown and targeted tantrum (a-19's next three); a "saw a break" memory; the
reference's recovery clock; the passion mood buff; mood faces (no art); threshold notches drawn on
the mood bar (published, not drawn).

## 8. Verified, 2026-09-25

Branch `claude/peaceful-lamport-58fozw`, one commit per unit. **Run in a container with no Unity**:
the fast tier and the Long tier only. Neither compiles `Odyssey.Presentation`, so the three shell
files this line touches (`HudShell.Mind.cs`, `HudShell.Inspect.cs`, `HudShell.Start.cs`) are
unproven until Unity compiles them, and no player build was made.

| Tier | Result |
|---|---|
| Fast, Sim | 1,521 passed, 0 failed (1,480 before this line) |
| Fast, Hud | 1,110 passed, 0 failed (1,090 before) |
| Long | 52 passed, 0 failed (48 before; the gate adds 4, `MindSoakTests`) |
| Unity EditMode, PlayMode, player build | **not run** |

**Goldens.** The three `Simulated` moved once, in TM3, and **no `Generated` moved**, as §3
predicted. `GoldenColonyProbe` before and after differs in **total mood alone**, by the outlook
traits the seeds dealt (+60 meadow, −120 city, −120 played board); every position, job,
experience and progress figure is identical. TM1, TM2, TM4 and TM5 moved no golden.

**The gate** (`MindSoakTests.TenDaysThroughEveryBreakLine`, three seeds, 60 x 60 x 16, five
colonists with traits, the mood base pushed through the three lines from day three on faster
clocks): 26 to 29 breaks a seed, **all five kinds seen on every seed**, nobody dead, the lockstep
twin identical every hour, and a save taken mid-break resuming to the same hash a day later. About
25 s a seed for the run and its twin.

| Test | Proves |
|---|---|
| `MindAspectTests` | the band falls at her own lines; a rested colonist is content; thoughts sum to the memory offset; names agree across the seam |
| `ThoughtsTabTests` | the friendly-fire memory is named with its -8 and its time left; Now before Memories, worst first; the cap; a break named on the pane and the Events row |
| `TraitTests` | two or three from the seed; no spectrum twice, no conflict; no passion or skill moved; the card's traits are the walker's; first tick only; each of the five effects against a traitless control; the save and the hash |
| `TraitRowsTests` | the summary words and tint; the pane's rows; a trait's mood under Now; the card's lines; the fit under the needs |
| `MentalBreakTests` | the tier's clock and the fall-through; each of the five breaks when forced; the job filter; catharsis at the end and none on a downing; the Events record; a save mid-break |
| `MindSoakTests` | the gate above |

**Found on the way, not fixed** (outside this line): `PawnPurpose.AnimalMind` and
`PawnPurpose.DeconstructRefund` are the same salt, `0x165667B1`, so those two streams agree —
the fault `StoneYield`'s own comment warns against.

## 9. Do not undo by tidying

- **The band is the simulation's.** Putting a threshold back in `RosterModel` is two owners of one
  number (P1), and the first trait that moves a line makes them disagree.
- **Traits roll on the first tick, not at placement.** At placement they would move every
  `Generated` golden for no behaviour, exactly the reason starting skills moved.
- **The trait stream is its own purpose.** Drawing traits from the passion or skill stream would
  change every existing colonist's passions and skills for every seed.
- **Handles are appended.** A trait's index rides every save that holds it.
- **The break's extra draws come after the one that decides it.** Drawn first, they would change
  which colonists break on every seed.
