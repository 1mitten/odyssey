# 41 — The draw: balanced colonists, a gamble, and the reel machine

**Status: designed 2026-09-24; CD1–CD6 built the same day, awaiting the first play.** Branch `claude/character-draw`, worktree
`D:\code\odyssey-draw`. Numbered 41 because 37, 39 and 40 are taken on other branches
(`claude/medical-supplies`, `claude/weather-design`, `claude/stair-gait`).
**Read first:** `18-colonist-select.md` (the seam this extends: a candidate is a pure function of
its own seed and slot, rolled through the colony's own methods), `19-world-setup.md` §10 (typed
names), `17-rates-and-stats.md` §4b and §6 (the pace clamp and the "fifth factor"), `15-skills.md`
§8 (passion, and why traits were deferred). The Claude Design brief is `41-the-draw-brief.md`.

This document holds the decisions, the mechanisms and the units. Each unit adds its measurements
and its reversals here as it lands.

---

## 1. What the owner asked for

> *"Character selection like rimworld with the traits but make it balanced so everyone averages
> out. However you can choose a "gamble" mode where there is a much higher threshold. You only get
> to roll each colonist once but the threshold is more wild in terms of the stats, traits — you
> could get someone with an amazing ability or get someone really duff … that's the game."*

The presentation is a fruit machine after *Alternate Reality: The Dungeon* (1985, C64). Its stat
strip is a row of small boxed abbreviations (STA CHR STR INT WIS SKL HP) over a row of boxed
numbers that cycle while you stand in the gateway. The owner wants the same strip, with every
reel spinning through a random sequence at a speed of its own, the name and portrait spinning with
them, a white machine face, and a button to stop.

## 2. The owner's decisions (2026-09-24)

Taken in two rounds of questions; settled, not proposals.

| # | Decision | What it costs |
|---|---|---|
| 1 | **One STOP, cascade.** One press; the reels spin down left to right, each at its own rate, each with a clunk. | Nothing the player does after the press matters, so the suspense has to come from the cascade itself (§6.3). |
| 2 | **Rolled at the press.** The colonist is drawn the instant STOP is pressed, and the reels land on it. | The reels are a show, not a mechanism. That is what keeps timing from being a skill and keeps §4's tables honest. |
| 3 | **Gamble pays better on average**, and must carry a real benefit (*"if it's the same — what is the point"*). | Gamble is a deliberately generous mode. §4.3 says how much. |
| 4 | **Gamble is wide with a high ceiling.** Long-tailed per-skill tables to 15, where Standard never passes 8; extreme traits only here. | About three pulls in ten are worse than any Standard colonist, and about one in eleven is a real dud. |
| 5 | **No nudge.** One pull per colonist, locked. | No second chance at all, so a dud is carried. |
| 6 | **Pulls stick.** After the first gamble pull, Back and New game return the same machine until Start or quitting the program. | Closing the program still washes a pull. Nothing is saved to disk for it (§5.3). |
| 7 | **Ten reels:** FACE, NAME, the five skills that do something, SPD, TRAIT, TRAIT. | Hauling rolls quietly and shows only in the detail pane (§4.1). |
| 8 | **Traits are real and a small set**, each moving a number the game already has, **with a seam for full traits later.** | Traits reach the simulation, the save and the hash (§3). |

## 3. Traits

### 3.1 What a trait is

A `TraitDef` in `Assets/Odyssey/Defs/Core/Pawns/Traits.xml`, read by `ContentPack` like every other
pawn Def:

| Field | Meaning |
|---|---|
| `defName` | stable identity; also indexes the save |
| *(name)* | not on the Def: `TraitHandle.Keys` in `Sim.Contracts` holds each trait's registry key beside its handle, so the simulation's table order and the interface's names have one owner. Name and description live in `icon-keys.csv` |
| `worth` | signed balance value: mild good +2, mild bad −2, extreme ±4. Read by the Standard roll to pair traits and by the outcome judge (§6.4) |
| `pool` | `Mild` or `Extreme`. Standard draws only `Mild`; Gamble draws both |
| `commonality` | per-mille weight within its pool |
| `opposite` | the trait this one may never be dealt with. The first line of the conflict list, and **read now**: it stops Standard dealing *Diligent* beside *Idle*, which would be a pair that cancels out and a wasted slot |
| `effects` | a list of `{stat, factorPerMille}` or `{stat, offset}` |

`TraitStat` is closed and small: `WorkSpeed`, `MovePace`, `Learning`, `Mood`, `RestFall`,
`HungerFall`, `MeleeDamage`.

### 3.2 One owner for what traits add up to

`Pawn.TraitFactorPerMille(TraitStat)` and `Pawn.TraitOffset(TraitStat)` are **the only readers of
`Pawn.Traits`**. Every seam asks them; no seam walks the list itself. This is bug pattern P1 (one
rule, two owners) prevented rather than fixed: the day spectrum traits or age factors arrive, they
arrive in one function.

| Stat | Seam | How |
|---|---|---|
| `WorkSpeed` | `Pawn.WorkRatePerMille` (`Pawn.cs:625`) | a factor beside `ConditionPerMille`, before the def's floor. Design 17 §6's "fifth factor", at last |
| `MovePace` | `Pawn.MoveRatePerMille` (`Pawn.cs:651`) | multiplied into the innate pace, and **the product clamped to 850–1150**. The walk cycle cannot be sped past ±15% without looking wrong (17 §4b), and that limit is about the drawn stride, not about where the speed came from |
| `Learning` | `Pawn.LearningFactorPerMille` (`Pawn.cs:780`) | the seam has returned 1000 since M2; now it returns the product |
| `Mood` | `NeedsSystem.UpdateMood` | an offset added to the target beside the need bands and the temperature, situational and never stored, like both of those |
| `RestFall`, `HungerFall` | `Pawn.NeedFallPerInterval` (`Pawn.cs:567`) | a factor on `NeedIndex.Rest` / `NeedIndex.Food` |
| `MeleeDamage` | `MeleeRules.Resolve` (`MeleeRules.cs:68`) | a factor on the rolled damage, before the critical |

### 3.3 The starting set

Names are invented (clean room; checked against the reference's trait list, and none is a copy).
**They go on the wiki for the owner to correct; the table below is the proposal, not the canon.**

| Pool | Good | Effect | Bad (its opposite) | Effect |
|---|---|---|---|---|
| Mild | Diligent | work ×1.15 | Idle | work ×0.85 |
| Mild | Quick study | learning ×1.4 | Slow study | learning ×0.6 |
| Mild | Long stride | pace ×1.05 | Short stride | pace ×0.95 |
| Mild | Sunny | mood +60 (of 1,000) | Dour | mood −60 |
| Mild | Light eater | hunger ×0.8 | Big appetite | hunger ×1.2 |
| Mild | Scrapper | melee ×1.2 | Soft hands | melee ×0.8 |
| Extreme | Prodigy | learning ×2 | Wreck | work ×0.6 |
| Extreme | Tireless | rest fall ×0.5 | Bottomless | hunger ×1.6 |
| Extreme | Unshakeable | mood +120 | Butterfingers | work ×0.75, melee ×0.7 |

Extreme pairs are opposites for dealing purposes only; they are not mirrored effects.

### 3.4 The seam for full traits later

The reference's full trait (its research is `a-01-pawns.md` §Traits) also carries **degrees** (one
Def, several strengths), **skill offsets**, **work types it forbids**, forced thoughts and a
**conflict list** wider than one opposite. Every one of those is an *added field on the same Def*,
read by the same aggregator or by the roll. **None is declared now.** A field nothing reads is the
artefact §6b of design 18 warned about: it describes a game that does not exist, and the next
session trusts it. `opposite` is declared because the roll reads it today.

### 3.5 Save, hash, publish

- **Saved** in a new section `odyssey.pawn.traits`: `(pawn id, count, def index…)`. A save written
  before this unit has no such section and loads with nobody holding a trait, which is what it
  meant. **No format bump**: the `PawnSeedSection` precedent (design 18 §3).
- **Hashed.** It decides rates; it is real state.
- **Published** as a pawn aspect, so the in-game inspect pane lists a colonist's traits. A trait
  shown on the start screen and then never seen again is a promise the game breaks.

## 4. The two rolls

### 4.1 Which skills are in the budget

The five reel skills are **Chopping, Mining, Construction, Growing, Melee**: the live skills a
level buys something in. **Hauling is outside every budget** and rolls from the legacy table in
both modes. It is a work type rather than a skill by decision (`15-skills.md` §6.2) and its removal
is recorded as a known gap; putting it on a reel would make a jackpot out of a number the design
means to delete.

The reel abbreviation for Chopping is **CHP**, taken from its registry label, **not CUT** as the
owner's list had it. The skill is called *Chopping* on every other surface (`ui.skill.cutting`), and
a reel saying CUT beside a pane saying Chopping is the kind of disagreement the registry exists to
prevent. If the owner prefers CUT, the fix is renaming the skill, in one place.

### 4.2 Standard: everyone averages out

| | Rule |
|---|---|
| Skills | **Exactly 12 levels** across the five. Each skill draws a weight 1–6; the twelve points are dealt one at a time by weight, and a skill at **8** takes no more. Same total for everybody; specialists and generalists both come out of it |
| Passions | **Exactly one major and one minor**, on two different reel skills, the major drawn weighted by level + 1 so it tends to sit on what they are good at |
| Traits | **One mild good and one mild bad**, never each other's opposite. Worths are ±2, so the pair nets to zero |
| Pace | 950–1050, where the legacy roll is 850–1150 |

Simulated over 50,000 draws: the best skill averages **4.9**, a colonist has a 6+ skill **27%** of
the time, and nobody passes 8. **Standard is stronger than today's roll**: the legacy table
averages about 8.1 levels over these five skills, against Standard's fixed 12. That is deliberate
(the owner saw and chose "exactly 12"), and it is why every golden moves (§7).

### 4.3 Gamble: wide, with a ceiling Standard never reaches

| | Rule |
|---|---|
| Skills | Each reel skill drawn **independently** from 0–15, weights in tenths of a per cent: `300 120 100 90 82 72 62 52 42 32 22 12 7 4 2 1` (level 0 to 15) |
| Passions | Per skill, 25% major, 25% minor, 50% none: anywhere from none to five majors |
| Traits | 0 / 1 / 2 / 3 traits at 25 / 35 / 28 / 12%, from **both pools**, no duplicates, never both halves of an opposite pair, no pairing for balance |
| Pace | 850–1150 |

Simulated over 200,000 pulls (the table above is the one that met the owner's figures; the first
draft gave 12% stars and was thinned):

| Outcome | Share | The owner was shown |
|---|---|---|
| Skill total mean | **15.9** (Standard: 12, so about **+32%**) | about +35% |
| Median / p10 / p90 | 15 / 7 / 26 | |
| **Dud**: total ≤ 6 | **9.1%** | ~8% |
| **Worse** than Standard (< 12) | **29.5%** | ~30% |
| **Better** (> 12) | **65.4%** | ~60% |
| **Star**: any skill ≥ 12 | **6.8%** (Standard: never) | ~7% |
| Any skill ≥ 10 | 21.8% | |

**Measured, 2026-09-24**, by `DrawDistributionTests` (Long tier) over 100,000 pulls through
`ColonistDraw.Roll` itself: skill total mean **15.87** (+32%), dud **9.0%**, worse **29.2%**, better
**65.6%**, a 12+ skill **6.7%**, an extreme trait **37.2%** of pulls. Standard over 100,000: a 6+
skill **26.8%**, and every total exactly 12. The test holds these to bands around them, so a retune
that drifts outside what the owner chose fails.

Every number in §4.2 and §4.3 lives in `Colonist.xml` as `ASSUMED` tuning, beside the existing
`startingSkillLevelWeights`.

### 4.4 Roll profiles

`enum RollProfile : byte { Legacy, Standard, Gamble }`, one per pawn.

- **Legacy** is today's roll, untouched. It exists only so **a save written before this unit loads
  rolling exactly as it did**: pace is re-derived from the seed on load, and a Standard range would
  quietly retune every old colonist's walk.
- **Saved** in a new section `odyssey.pawn.profile`; a missing section means Legacy. **Hashed.**
- `RollPassions`, `RollStartingSkills`, a new `RollTraits` and `InnatePacePerMille` branch on it.
- **Every colonist the game makes itself rolls Standard**: scenario colonists, headless runs,
  debug spawns. The balance is the game's, not the start screen's.
- `ColonistDraw.Roll(seed, slot, profile)` calls those same methods on a throwaway pawn, so the
  card cannot disagree with the colonist (design 18 §4's `HopCost` rule). `ColonyRequest.Colonists`
  carries a profile beside each seed; `ColonyScenario.Place` sets both before any roll.

**A fix on the way:** `StartingSkillsSystem` fires only at tick 0, so a colonist debug-spawned later
has zero skills, while `PawnRegistry.cs:97` claims otherwise. With profiles the spawn rolls traits,
so it rolls skills in the same place, and the comment becomes true.

## 5. The select screen

### 5.1 Choosing the mode

The setup page gains a two-way choice above the three cards, **Standard | Gamble**
(`ui.newgame.mode.*`). It can be changed **only while no gamble pull has been made**; after the
first pull, Standard is disabled with the reason on its tooltip. That is decision "one or the
other" enforced by the model, not the view.

### 5.2 Standard

Today's page: three cards dealt, Keep and Reroll, rolled under the Standard profile. The detail
pane's Traits section is filled at last (the em dash at `HudShell.Start.cs:715` goes).

### 5.3 Gamble

- The three cards start blank (a face-down card: no name, a question-mark face). Reroll and Keep
  are hidden.
- A slot is `Unpulled → Spinning → Landed`. Clicking an unpulled card selects it and shows the
  machine in the detail pane with **PULL**. PULL starts the reels; the button becomes **STOP**.
  STOP draws the colonist (`SeedEntry.Draw`, Gamble profile) and the cascade runs.
- A landed card is **locked for good** and stamped. It can still be renamed: a name is not a stat
  (design 19 §10), and typing one is not a reroll.
- **Start is allowed only when all three have landed.**
- **Pulls stick.** `MenuDirector`'s New game entry (`MenuDirector.cs:551`) calls `Deal`, which today
  wipes the page. With a gamble of at least one pull it must not. The state is cleared by Start, or
  when the process ends; it is not written to disk, so quitting the program does wash a pull.
  Accepted (decision 6).

## 6. The machine

### 6.1 The reels

| # | Label | Symbols | Notes |
|---|---|---|---|
| 1 | FACE | flat avatars (`AvatarGlyph`) | cheap to spin; the real portrait replaces it on landing |
| 2 | NAME | names from the pool | the landed name is the one `ColonistNames.Rolled` gives |
| 3–7 | CHP MIN CON GRO MEL | 0–15 | a landed major or minor passion shows as a drawn flame (`HudGlyph`) in the window |
| 8 | SPD | 85–115 | pace in per cent |
| 9–10 | TRAIT | trait names, and a blank | a colonist with fewer than two traits lands on the blank |

Every reel's strip is a **seeded shuffle** of its symbols, so each runs "all the way round in a
random sequence" and no two strips read in the same order.

### 6.2 Motion

- **Speeds** range from about 6 to 22 symbols a second, one per reel, chosen **pairwise
  non-harmonic** (no ratio near a small fraction) so no two reels ever visibly lock step.
- **The cascade.** Reel *i* begins braking 0.25 s × *i* after STOP, plus a few tens of milliseconds
  of jitter. It decelerates on an ease-out curve sized so that it travels a whole number of symbols
  and **lands exactly on its value**, overshoots by about 8% of a symbol and settles back. Ten
  reels take about three seconds.
- **The model is pure.** `Odyssey.Hud.ReelMachine` is stepped by an unscaled `dt` passed in, the
  `ToastModel` and `MenuAmbience` pattern, so every property above is a fast-tier test. The view
  only reads positions.

### 6.3 The tease, which is where the fun is

A reel about to land **hot** brakes over **1.5 times as long**. Hot means a skill ≥ 10, a major
passion, or an extreme trait of either sign. It is the fruit-machine anticipation rule: the player
sees the reel still crawling and knows something is coming, but not whether it is a Prodigy or a
Wreck. Because the outcome is fixed at the press, the tease is honest; it never promises a value
that does not come.

### 6.4 Landing

| Outcome | Rule | Show |
|---|---|---|
| **Star** | any skill ≥ 12, or two hot reels | the frame lights gold, hot windows flash amber, a sting |
| **Dud** | skill total ≤ 6 and no good trait | the bulbs go out one by one, a flat womp |
| **Plain** | anything else | a clunk and the stamp |

Each reel landing raises `ReelLanded(index, symbol, heat)`; the last raises `AllLanded(outcome)`.
The view and the sound listen; neither computes anything.

### 6.5 On screen

- **A white machine face with black 2 px rules** inside the dark page: the header row of boxed
  abbreviations, and a white window per reel. Numerals are IBM Plex Mono at the `Clock` role (24,
  mono), labels at `PanelLabel`, **no new text role**. Every character is ASCII
  (`HudFontTests`).
- **Marquee bulbs** chase round the frame while anything spins.
- **Motion**: while a reel is fast its strip is stretched vertically and ghosted by its neighbours;
  on landing it bounces.
- **The white is new to the theme.** It becomes `HudTheme` tokens (`MachineFace`, `MachineRule`,
  `MachineHot`, `MachineStar`) mirrored in `Hud.uss` and pinned by `HudStyleSheetTests`, never a
  literal in C#.
- **No allocation per frame** (ADR 0003 F1). Each reel is a clipped container holding its strip of
  pre-built labels, moved by `style.translate`; nothing is re-texted while it spins.
- **A menu frame driver.** `HudShell.Update` returns early when there is no world (`HudShell.cs:869`),
  which is why the menu has never animated. A guarded branch before that return steps the machine
  with `Time.unscaledDeltaTime`.
- **The visual target is the Claude Design output** from `41-the-draw-brief.md`. The first build
  follows §6.5; the second follows whatever the owner approves from that.

### 6.6 Sound

The menu has no `AudioDirector` (it is built with a world, `OdysseyBootstrap.cs:1014`), so the
machine gets a small voice of its own, `MenuSfx`, modelled on `MenuAmbience` and reading the same
faders. `SoundIds`: `odyssey.sound.draw.spin` (a ticking loop), `.clunk` (variants), `.hot`,
`.star`, `.dud`. Clips follow `docs/reference/audio-sourcing.md`. **The machine runs silent until
they exist**, which is what an unknown clip already does, so sourcing is owed rather than blocking.

## 6.7 The Claude Design spec, and where the build departs from it (2026-09-24)

The owner brought back a Claude Design spec for CD5 with the instruction *"don't be super strict
here — ensure it fits in the style/format of the game first"*. It supersedes §6.1 and §6.5 where it
speaks: **Gamble is built into the existing New game screen**, not beside it — a Creation switch, a
reel window in place of each skill's figure, a machine frame with marquee bulbs round the detail
pane, and one Pull / Stop button in place of Keep and Reroll. Standard is unchanged. Six tokens
(`MachineFace`, `MachineInk`, `MachineHot`, `MachineStar`, `MachineCold`, `MachineTease`) went into
`HudTheme`, every size into `HudLayout` with a test (`DrawLayoutTests`, `HudStyleSheetTests`).

| The spec | What was built | Why |
|---|---|---|
| "Draft \| Gamble" | **Standard \| Gamble** | The design and the owner's questions called it Standard; one name. The label is in the registry to correct. |
| All fourteen skills are reels, 0–20 | **The five live skills spin**; the nine nothing simulates show a still `--` window | Decision 7: a jackpot on a skill that does nothing is a fake jackpot. Levels run 0–15, Gamble's range. |
| No pace | **A Pace window in the identity row** (85–115) | Decision 7 put SPD on the machine; the identity row had the room. "SPD" became **Pace** because the HUD forbids three-letter capitals (`NoLabelIsAThreeLetterPlaceholder`). |
| Two trait windows, 200 wide | **Three, 132 wide** | A gamble deals up to three; three and the button have to fit the grid the machine wraps (618 px, `DrawLayoutTests`). |
| Machine 980 wide, cards 400 × 84 | **The page's own geometry**: the detail pane framed at the grid's width, the cards as they were | "Fit the game first": Standard must look exactly as it did, and the cards are shared. |
| Skill rows 42 high | **38** (the 32 window plus three either side) | Enough air; the page is scaled from 1080 and 38 keeps the grid clear of the footer at 720. |
| Verdict tag 14/700 tracked caps | **Row (14/500), sentence case** | The stylesheet sets no type (`TheSheetSetsNoTypeAtAll`) and "DUD" in capitals is the three-letter shout the HUD forbids. |
| Dashed borders on a face-down card | **Solid, at the same 22% white** | UI Toolkit draws no dashed border. |
| Colonist generated before the spin | **The same** — drawn at PULL, not at STOP | Decision 2's intent, timing cannot matter, holds either way; drawing at PULL lets a hot reel know to tease. |
| Jackpot: 13+ with a major, or Prodigy. Dud: sum < 12, or Wreck | **Jackpot: 13+ with a major, or any extreme good trait. Dud: sum ≤ 6, or any extreme bad trait** | "Sum < 12" is *worse than Standard*, which would call three pulls in ten duds; the design's dud is one in eleven. Both are constants in `DrawVerdict`, as the spec asked. |
| Every reel 18 steps a second | **A speed per reel, 11–23 symbols a second, clear of every small ratio** | The owner's own words: "each stat going at a completely different speed". |
| Reels step discretely | **Reels scroll continuously**, three items and ghosts | A fruit machine's reel moves; the ghosts are the spec's own motion blur. |

**What the machine does on screen**, all of it `ReelMachine`'s numbers: PULL spins every reel up over
a quarter of a second; STOP lands them in reading order — portrait, name, pace, the left column of
skills, the right, the traits — each at least 120 ms after the last; a reel landing hot (a skill of
11 or more, or any extreme trait) teases over 0.9 s with its window pale amber and two pulsing bars;
a hot window warms to amber over 0.2 s once down, a flaw lands grey. The bulbs chase every third
while anything moves; a jackpot flashes them gold three times and holds, and turns the frame gold; a
dud puts them out right to left over 0.6 s, keeping three. The button reads Pull, Stop, *Stopping
n / N*, Next colonist, All pulled; Space does the same, never while a name is being typed.

**Sound** (CD6) is wired to all of it and silent until clips exist under `odyssey.sound.draw.*`.

## 7. What moves

- **Every golden moved, both numbers on all three, and the colonies behave differently.** Measured
  with `GoldenColonyProbe` against `main` (2026-09-24): item counts and cells, need totals and the
  headcount are unchanged or within a few points on every board; nobody died and no job failed.
  What moved is a more skilled, traited colony — experience 84M → 133M on the meadow, 62M → 164M in
  the city, 94M → 182M on the played board — and the job mix (meadow 48 → 42 jobs, city 79 → 92,
  played board 105 → 89). `Golden.cs` carries the sentence.
- **Mechanic tests that pinned exact numbers now ask for the old roll.** `CombatFixture` and one
  `SkillTests` case set `ScenarioDef.colonistProfile = Legacy`, because a Scrapper or a Quick study
  would move a swing's spread or a felling's experience and which one a seed deals is the draw's
  business. Measured first: with placement forced to Legacy all seven failing tests passed.
- **The ten-day headless gate passes** on the new roll (the Long tier, 43 tests, 2026-09-24). A
  colony that survived on the old roll was not proof that one survives with an *Idle* and a *Big
  appetite* in it, so it was run rather than assumed.
- **Save format stays where it is.** Two new sections; old saves load as Legacy with no traits.
- **The wiki moves**: the `ui.trait` namespace joins the Colonists page, and the machine's words
  and the mode names join the registry.

- **A bug older than the draw, found by it: no colonist in a played game had starting skills.**
  `StartingSkillsSystem` fired when the tick read zero; the played scene starts at noon. The draw's
  PlayMode test compared the colony with the landed card and printed *tick at start 30009, real xp
  all zero*. It now fires on the first tick it sees; a load fires it once more, which writes only
  into skills still at zero and so writes what was already there (bug pattern P19). No golden moved:
  they start at zero.

## 8. Units

Written in dependency order; one branch, one PR.

| Unit | What | Proven by |
|---|---|---|
| **CD0** | This document, the brief, the registry rows | the three content gates |
| **CD1** | Traits: Def, aggregator, the seven seams, save section, hash, aspect, inspect pane | fast tier per seam; save round trip; hash coverage |
| **CD2** | Roll profiles: Standard, Gamble, Legacy; `ColonistDraw`, `ColonyRequest`, `Place`; the debug-spawn fix | Standard is exactly 12 on every seed; the colony is the card, field for field, traits and pace included; `DrawDistributionTests` (Long); goldens and probe; ten-day gate |
| **CD3** | The select model: mode, the gamble slot states, pulls stick, Start gated | fast tier |
| **CD4** | `ReelMachine`: strips, speeds, cascade, tease, events | fast tier: lands exactly on target for every symbol and a spread of frame steps; non-harmonic speeds; order; tease length |
| **CD5** | The machine on screen, the frame driver, the effects | PlayMode: pull, stop, start, and the spawned pawn equals the landed card; `HudStressTests` flat while spinning |
| **CD6** | `MenuSfx` and the five sounds | EditMode; silent without clips |

## 9. Tests

| Claim | Where |
|---|---|
| each seam reads the aggregator and nothing else reads `Pawn.Traits` | `TraitTests`, fast tier |
| pace × trait never leaves 850–1150 | `TraitTests` |
| a Standard colonist has exactly 12 levels, one major, one minor, one good, one bad, on every seed | `RollProfileTests`, fast tier |
| a Gamble pull stays inside its tables | `RollProfileTests` |
| the promised bands hold over 100,000 draws | `DrawDistributionTests`, Long |
| a save without the new sections loads as Legacy, with no traits, rolling as before | `PawnProfileSaveTests` |
| traits and profile move the state hash | `StateHashCoverageTests` |
| **the colony's pawn is the card that was on screen**, now including traits, profile and pace | `ColonistDrawTests.TheColonyIsTheColonistsTheScreenShowed` |
| no mode change after a pull; Start waits for three; a pull survives Back and New game | `ColonistSelectTests`, `MenuDirectorTests` |
| every reel lands exactly on its value whatever the frame step | `ReelMachineTests`, fast tier |
| **a pulled colonist is the colonist in the game** | `StartScreenTests`, PlayMode |

## 10. Open, for the first play

- Is three seconds a pull the right length, and does the tease read as suspense or as the game
  being slow? A second press that hurries the remaining reels is the cheap answer if it is slow,
  and it would not change the outcome.
- Does a dud feel like the game or like a punishment?
- Does the white machine read as a machine on the dark page, or as a hole in it?
- Is Gamble tempting over Standard at +32%, or does it need more?
- CHP or CUT (§4.1).
