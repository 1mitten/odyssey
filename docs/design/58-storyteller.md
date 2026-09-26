# 58 — The storyteller

**Written 2026-09-26**, from the owner's request and a five-round interview the same day. Branch
`claude/sweet-cerf-wzfxgs`. **Designed, nothing built.** Interview:
`docs/research/storyteller-interview.md`. Research: `a-11-storyteller-incidents.md` (the taxonomy)
and `a-11-storyteller-pacing.md` (the numbers). Plan: `docs/plans/storyteller.md`.

> *"We should look into the storytelling aspect - we can decide on names later but base it around
> this. https://rimworldwiki.com/wiki/AI_Storytellers - we'll invite our own story tellers later and
> refine it for us, refine, explore and plan."* — the owner, 2026-09-26

Numbered 58 because the ride-along mode reached `main` as 57 on the same day. 51 and 54 are held by
open PRs.

## 1. What a storyteller is

A storyteller is **the thing that decides when an incident fires and how hard it hits**. It is not
a world simulation. It is a pacer: tension, then release.

In this game it is three separable parts. Each has one owner.

| Part | Decides | Owner |
|---|---|---|
| **The pacer** | *when*, and *which category* | the storyteller Def's generators (§3) |
| **The budget** | *how hard* a threat is | `ThreatBudget`, from colony strength (§4), tension (§5) and difficulty (§6) |
| **The pick** | *which incident* in the category | the gates already on every `IncidentDef` (design 23 §4) |

Everything fires through `Incidents.TryFire`, the one door design 23 built. **A storyteller's raid
and a debug raid at *Auto* are the same raid** (design 23 §3: earned and forced behave the same).

## 2. The owner's rulings (2026-09-26)

1. **The threat budget is strength-based, not wealth.** It follows what the colony can fight with.
2. **Strength counts people and weapons only.** Fortifications and animals never count. Otherwise
   building a defence summons a bigger raid, and that is the reference's wealth meta again.
3. **Adaptation is kept and made visible**: a tension gauge by the clock, five bands, and a tooltip
   naming the last thing that moved it.
4. **Storyteller and difficulty are separate axes.** Both are picked on the setup page and both can
   be changed mid-colony from Settings.
5. **Difficulty is presets plus Custom.**
6. **Three storytellers ship, as Defs on one kit**: Steady, Calm, Chaotic. These are placeholder
   names. The owner will name them, and *inviting our own storytellers* means writing a Def and a
   registry row.
7. **Persona is a portrait and a blurb** at setup and in Settings. Nothing speaks during play.
8. **The storyteller gives no warning.** The raid's gather phase (design 55 §3) is the telegraph.
9. **A big threat pauses and jumps the camera**, behind a Settings > Gameplay toggle, on by default.
10. **Pacer only.** It fires the incidents that exist. Each new incident is its own unit.
11. **Population intent is designed now and goes live with the first joiner** (ST7).
12. **Grace is per storyteller**, stretched by difficulty.
13. **Steady aims for about three big threats a 24-day season** at normal. Calm aims for about half.
    Chaotic aims for the same mean as Steady, but lumpy.
14. **Good and neutral events come about every 5–6 days.**
15. **A save from before the storyteller loads with none.**
16. **The raid's `minRefireDays` falls from 4 to 2.** The storyteller paces; the Def is only a
    safety floor.

## 3. The pacer: one kit, three shapes

A `StorytellerDef` is a grace period, a list of **generators**, a population-intent curve and a
tension profile. A generator is a nested block, following `<raid>`'s precedent, of one of three
kinds. A fourth kind, `scheduled`, stays reserved (design 23 §2).

| Kind | Behaviour | Fields |
|---|---|---|
| `OnOffCycle` | alternates on-days and off-days; in each on-phase fires *n* incidents of its category, a minimum spacing apart, at random ticks | `category`, `onDays`, `offDays`, `firesMin`, `firesMax`, `minSpacingDays`, `startDay` |
| `MeanTimeBetween` | about every *N* days, sampled stochastically | `category`, `favourability` filter, `meanDays`, `startDay` |
| `RandomBag` | one roll on a mean interval, then a category by weight; *N* days without a big threat force the next roll to be one; multiplies its threats' budget by a random factor | `meanDays`, `weights` (category → weight), `droughtDays`, `budgetMinPerMille`, `budgetMaxPerMille` |

**Plans are drawn ahead, not rolled every tick.** On arming, a generator draws its next fire tick
(or, for a cycle, the whole on-phase's fire ticks) and saves them. The system checks once a game
hour (2,500 ticks) whether any planned tick has passed. So the cost scales with the number of
generators, not the board, and a save mid-phase resumes onto the same plan.

**The decision is a pure function**: `Decide(state, tick, rng, canFire) → (category, def)?`. It
takes no world. The live system and the fast-tier tuning harness (§9) both call it, so the thing
tuned is the thing shipped.

**A category with nothing that can fire loses its roll**; it is not redistributed to another
category. So adding a small-threat incident later cannot change how often raids come. A cycle's
fire that finds nothing fireable is **deferred by an hour**, up to the end of its on-phase, and then
dropped.

### 3a. Picking within a category

These are the gates design 23 wrote and nothing has read until now:

- `IncidentWorker.Fireable`, so records never fire;
- `earliestDay`, counted from the colony's start tick;
- `minRefireDays`, from `IncidentLedger.LastFiredTick`;
- `minColonists` and `maxFires`;
- difficulty's "big threats allowed";
- the worker's own `CanFireNow`.

The survivors are picked by `weight` × population factor. The population factor is 1000 per mille
for everything except incidents marked `populationGain`, so it is 1000 per mille for everything
until ST7.

### 3b. The three shapes, starting numbers

**These numbers are invented.** The soak (§9) tunes them against the owner's targets. The reference's
day numbers are scaled ×1.2 to a 72-day year.

| | Steady | Calm | Chaotic |
|---|---|---|---|
| Grace (first big threat) | day 12 | day 15 | day 8 |
| Big threats | `OnOffCycle` ThreatBig: on 4, off 7, fires 1–2, spacing 2 days | `OnOffCycle` ThreatBig: on 8, off 8, fires exactly 1 | inside the bag |
| Target a season (24 days) | about 3 | about 1.5 | about 3, higher variance |
| Good and neutral | `MeanTimeBetween` Misc, Good or Neutral, 5.5 days | the same | inside the bag |
| Bag | — | — | mean 1.6 days; weights Misc 3.5, ThreatBig 1.1, ThreatSmall 0.6, Arrival 0.8; drought 14 days; budget ×0.5–×1.5 |

### 3c. Population intent (the curve now, live at ST7)

The curve maps colonist count to per mille: 0 → 8,000; 1 → 2,000; 3 → 1,000; 7 → 350; 11 and
above → 0. It multiplies the weight of any incident marked `populationGain`. It is on the Def, saved
with the Def index, and read by `Decide`. Until a joiner exists it multiplies nothing, and a test
with a stub incident is its only proof. The reference also has a second, days-since-last-recruit
curve; that is recorded, not planned.

## 4. The budget: colony strength

### 4a. What counts

For each **standing colonist** (`IsColonist` and `Melee.IsStanding`), fighting power is:

`power = health × weapon × skill`

- **health** = the fraction of hit points left × consciousness (`Vitals.Of`). A downed colonist
  counts 0.
- **weapon** = the armament's damage per second (`IWeaponRules.ArmamentOf`: damage ×
  60 ÷ cooldown ticks), × `WeaponQuality` damage, × `WeaponQuality` accuracy for a ranged weapon.
  Fists count as an armament.
- **skill** = 600 + 40 × the level of the matching skill (Melee or Shooting), per mille. That is
  ×0.6 at level 0 and ×1.4 at level 20. This is **invented**.

**Colony strength** is the sum. **Never read**: buildings, sandbags, walls, doors, turrets (when they
come), animals, prisoners, stockpiled weapons. Each of these has a negative-control test.

### 4b. From strength to a raid

`size = clamp(round(colonyStrength × ramp × scale ÷ meanRaiderPower(mix)), minSize, maxAutoSize)`

- **meanRaiderPower(mix)** is the same formula applied to each kind in the mix, at its expected
  skill and its dealt weapon, weighted by the mix's per mille.
- **ramp** runs from 700 per mille at grace to 1,000 per mille by day 48 (two seasons). After that it
  adds 50 per mille a season, up to 1,500. The owner asked for a slow ramp over days; this is
  invented.
- **scale** = difficulty threat scale × tension (§5) × the bag's random factor (Chaotic only), all
  per mille.
- It is still refused, not trimmed, past `PawnRegistry.PawnCeiling` (design 55 §9).

This replaces `RaidBudget.AutoSize`'s input, and **its callers do not change**. `IncidentParms.Points`
stays what design 55 made it, *a size*, where 0 means Auto. The storyteller always sends 0. The
debug slider still forces a size.

### 4c. The inverse meta, and the remembered peak

A budget read at the instant of firing invites the obvious exploit: stow the guns and the raid
shrinks. So the strength the budget reads is a **remembered peak**. Each game hour it is set to
`max(current, previous × decay)`, where decay loses about a tenth a day. Disarming before a raid
buys nothing for several days, and a real loss (a death, a broken weapon) still shows within a week.
The peak is saved and hashed with the storyteller's state.

## 5. Tension (adaptation, visible)

Tension is a per mille number from **400 to 1,500**, starting at 1,000.

| Event | Change (at normal) |
|---|---|
| A colonist dies | −250 × min(1, 5 ÷ colonists) |
| A colonist is downed in combat | −60 × min(1, 5 ÷ colonists) |
| A quiet day (no colonist death or down) | +25 below 1,000; +10 at or above 1,000 |

It is fed by `CombatHooks.RaiseDied` and `RaiseDowned`, **colonists only**. A raider dying moves
nothing. The last cause and its tick are saved beside it. Difficulty's adaptation strength
multiplies every change, and the Peaceful rung sets it to 0. **Tension multiplies the budget** (§4b)
and nothing else. It does not move the pacer's clock.

### 5a. The gauge

The five bands, with placeholder names the owner will replace, are keys under `ui.tension.band.*`.

| Band | Tension |
|---|---|
| Reeling | under 600 |
| Easing | 600–849 |
| Even | 850–1,099 |
| Building | 1,100–1,299 |
| Peak | 1,300 and over |

The gauge is **a glyph on the clock's own line**, beside the weather glyph. It is not a second line,
because the strip has a height budget (`HudLayoutTests`). Its tooltip reads the band, then the last
cause and how long ago: *"A colonist died, 3 days ago: easing off."* **The band is decided in the
simulation and published**; the interface derives nothing. There is no number on screen; that was
the owner's ruling (bands + cause, not the multiplier).

## 6. Difficulty

A `DifficultyDef` rung sets four things. Custom exposes the four as numbers and saves them.

| Rung (placeholder) | Threat scale | Big threats | Adaptation strength | Grace stretch |
|---|---|---|---|---|
| Peaceful | 100 | **off** | 0 | — |
| Gentle | 300 | on | 1,500 | ×1.5 |
| Easy | 600 | on | 1,250 | ×1.25 |
| **Normal** | 1,000 | on | 1,000 | ×1.0 |
| Hard | 1,500 | on | 750 | ×0.85 |
| Brutal | 2,200 | on | 500 | ×0.7 |
| Custom | 0–5,000 | on or off | 0–2,000 | ×0.5–×2 |

Adaptation strength above 1,000 makes losses ease threats *more*, and quiet days restore tension
*faster*: the easy rungs forgive, the hard ones remember. The threat scales follow the reference's
presets. Everything else here is invented.

## 7. Where the choice lives

- **Both choices are saved in the colony**, in a new section `odyssey.storyteller`, not in the
  machine's settings. Settings (`ISettingsStore`, over PlayerPrefs) is machine-wide, and two colonies
  on one machine can have different storytellers. They change only by intent: `SetStoryteller`,
  `SetDifficulty` and `SetDifficultyValue`, appended to `IntentKind`.
- **The setup page** (design 19) gains a storyteller picker, showing portrait and blurb, and a
  difficulty ladder. The default is Steady at Normal.
- **Settings > Gameplay** gains the same two rows. They read the snapshot, send intents, and are
  greyed with no colony loaded. The one machine preference is **Pause on big threats** (on by
  default, reset with the tab).
- **Changing storyteller mid-colony** keeps tension, the remembered peak and the ledger. The
  generators are re-armed from the switch tick, and the grace is not re-applied once it has passed.
  **Changing difficulty** applies from the next decision.
- **A save from before this section** loads with *no storyteller* (index −1), exactly as it played.
  The interface shows one toast, *No storyteller: choose one in Settings*. It is presentation-only:
  nothing is written to the ledger or the hash.

## 8. Save and hash

- `odyssey.storyteller` is appended with **no format bump**. It holds the storyteller and difficulty
  indices, the Custom values, the colony start tick, each generator's plan, the tension and its
  last cause, and the remembered peak.
- It is **hashed only while a storyteller is set** (the pattern of `Projectiles` and `RaidSystem`).
  `ColonyRequest.Storyteller` defaults to −1, and the goldens and soaks build with the default, so
  **no golden moves**. A new game from the setup page sets it explicitly.
- The RNG purposes are new constants on unused SHA-256 round constants, one per generator kind plus
  the pick. Draws happen only on the hourly check.

## 9. The instrument

**The tuning harness (fast tier).** It runs `Decide` for 72 days × 200 seeds per storyteller, with
an oracle that always answers "can fire". It asserts big threats a season: Steady 3 ± 0.5, Calm
1.5 ± 0.5, Chaotic 3 ± 0.7 with a variance above Steady's. It prints the table, and the table is what
tuning moves.

**The soak (Long tier).** `StorytellerSoakTests` runs a real headless 72 days per storyteller at
Normal, with raids and drops actually landing. It asserts the colony's invariants every hour, counts
ThreatBig per season from the ledger, and prints the same table beside the harness's. **If the two
tables disagree**, the gates or `CanFireNow` are refusing fires the harness assumed possible. That is
the first thing to check.

## 10. Not to undo by tidying

- **Points is a size.** Do not repurpose `IncidentParms.Points` as a points budget. The debug slider
  and the storyteller would then mean different things by the same field.
- **A category with nothing fireable loses its roll.** Redistributing it would make raid frequency
  depend on how many small-threat incidents exist.
- **Fortifications never count towards strength.** Adding a "defence factor" recreates the wealth
  meta the owner rejected.
- **The strength read is a remembered peak**, not the current value (§4c).
- **Tension is published, not derived in the interface.**
- **Storyteller and difficulty are colony state, not settings.**

## 11. Later, and recorded

- **ST7, someone joins**, which makes population intent live.
- **A reactive director**: a fourth Def that reads mood, injuries and stores. Left 4 Dead's idea,
  novel for the genre. It is the same kit plus one generator kind.
- **Conditions** (timed, map-wide, no entities), for which weather's cold snap is waiting
  (design 43).
- **Quests** (reward on completion), which wrap incident workers (design 23 §8).
- **The History screen (F9)**, with tension graphed beside population and mood.
- **Verticality in threats**: arrivals from a lower stratum, drops through open sky (design 03 §11).
- A days-since-last-recruit curve beside population intent.
