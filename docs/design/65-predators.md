# 65 — Predators: hunger, the hunt, the man-eater and the kill window

**Status: designed 2026-09-26, nothing built.** The predator half of FA2 in the forest animals unit
(`docs/research/forest-animals-interview.md`, answers 6, 7, 8 and 11). Design 64 owns the
temperament rungs, the startle, the warning display, herds and a colonist heeding a warning; this
document owns what makes a **Predator** rung animal different from every other: it gets hungry, it
hunts, it eats what it kills, and — only when starving and only against a colonist on her own — it
can kill a person. Research: `a-20-wild-animal-temperament.md` (predator section),
`b-wild-animal-behaviour.md` (§ wolves). Read on `origin/main` at `b3d3128d`.

## 1. What is being built

| Part | What | New |
|---|---|---|
| **Hunger** | a food need that falls on the needs cadence for a predator alone | a species field and one branch in `NeedsSystem` |
| **The hunt** | a hungry predator picks the nearest reachable smaller animal and kills it; a pack joins | one think node, the existing melee driver |
| **Feeding** | the predator eats at the kill; the corpse stays, part eaten, for K3 | `Job_Feed`, one field on the corpse |
| **The man-eater** | starving, a predator stalks a colonist who is alone, then charges | `Job_Stalk`, a world rule |
| **The kill window** | a downed colonist under a predator dies at the end of one game hour unless it is driven off | `Job_Maul` — the owner's exception to design 33 |
| **Alerts** | *Stalked* and *Mauled*, pinned, jumping to the predator | two `ui.alert.*` keys, two published aspects |

Which species are predators: **the wolf and the fox.** The bear is Territorial (design 64) and an
omnivore; it is given **no hunger in FA2**. Its foraging and its store raids are later units
(recorded in §11), and a bear with a hunger clock and nothing it may eat would only ever starve.

## 2. Hunger

### 2a. Where it lives: the food need every pawn already has

Every pawn is built with `Needs = new int[NeedIndex.Count]` (`Pawn.cs:84`), and those numbers are
**already saved** (`PawnRegistry.cs:923`, for every pawn) and **already hashed** (`Pawn.cs:1191`,
unconditionally). An animal's never move: `NeedsSystem.Tick` skips any pawn whose `NeedsTick` is
false (`NeedsSystem.cs:53-57`), and `NeedsTick => IsColonist && !Downed` (`Pawn.cs:234`).

**So an animal's hunger is `Needs[NeedIndex.Food]`, reused, not a new field.** No save change, no
new hash field — the hash simply starts to see a number move. `StarvationSeverity` (saved,
`PawnRegistry.cs:989`) is reused the same way for §2d.

### 2b. The tick

A second branch in `NeedsSystem.UpdatePawn`'s caller, before the `NeedsTick` skip:

```
if (pawn.IsAnimal && pawn.Species.hungerPerInterval > 0 && !pawn.Downed)
    on the pawn's own phase of the 150-tick cadence: AnimalHunger(pawn)
```

The same phase-spread by id as a colonist (`(tick + id) % interval`), so a hundred animals cost what
a hundred colonists do divided by 150. `AnimalHunger` lowers food by `hungerPerInterval`, raises or
drains `StarvationSeverity` at the kind's `starvationPerInterval` exactly as a colonist's does, and
nothing else — **no mood, no thoughts, no break**. It is a separate method, not the colonist's
`UpdatePawn`, because every other line of that method is about people.

**Per interval, not per day**, because 400 intervals is a day and a per-day rate would truncate
(450 a day is 1.125 an interval). Integers at this grain are coarse and honest:

| Species | Body size ‰ | `hungerPerInterval` | Full → hunts (330) | Full → starving (0) |
|---|---|---|---|---|
| Wolf | 850 | **1** (400 a day) | 1.7 days | 2.5 days |
| Fox | 550 | **2** (800 a day) | 0.8 day | 1.25 days |

A small animal burns faster, which is also what makes the fox the busier hunter on screen.

**Seeded hunger is staggered.** A world's predators start between **500 and 1,000** food, dealt by a
hash of the pawn's roll seed, so a pack is not in lockstep and the first hunt is not at the same
instant on every seed. Arrivals at the edge are dealt the same way.

### 2c. Thresholds

| Food ‰ | State | What the predator does |
|---|---|---|
| > 330 | **Fed** | wanders and rests as design 64's Predator rung says; it hunts nothing |
| 1–330 | **Hungry** | hunts animals (§3) |
| 0 | **Starving** | hunts animals, and, where the world rule allows, a colonist who is alone (§5) |

330 is `a-20`'s reading of the reference (hunts at "about 30–35 % food"). Fields:
`SpeciesDef.huntBelowPerMille` (330), so a later species can differ.

### 2d. Never fed

A predator at 0 food grows `StarvationSeverity` at its kind's `starvationPerInterval`. **At 1,000
it leaves the board** — it takes the existing departure (walk to the nearest edge,
`PawnRegistry.Despawn`, `WildlifeSystem` tops the board up). It does **not** die of hunger:
a board strewn with starved wolves is clutter nobody asked for, and a predator that has failed to
eat for days is exactly the animal that moves on. With `starvationPerInterval` at 5 that is 200
intervals — **half a day starving**, which is long enough to be a man-eater in and short enough that
a board with no prey is not besieged for ever. Eating anything drains the bar as a colonist's does.

## 3. The hunt

### 3a. Choosing prey

On a think, a hungry predator with no job of higher priority (revenge, flight, design 64's warning)
takes **the nearest animal that is all of**:

- not of its own species;
- `bodySizePerMille ≤` the predator's `preyMaxBodySizePerMille`;
- within `huntRadius` cells (**30**), measured as design 64 measures its radii (cells, 3-D);
- reachable under the predator's own `TraverseMode` (`ctx.CanTravel`);
- **alive** — a downed animal is the easiest prey there is, and is taken.

Nearest by integer distance, **ties to the lower pawn id** — no roll, so nothing new draws on
`DeterministicRandom`. The scan walks the pawn list once, keeps the candidates within the radius
sorted by distance (at most the board's 80 animals), and asks `CanTravel` in that order, stopping at
the first yes. `CanTravel` is the costly half, and it is asked only of a hungry predator at a think.

| Predator | `preyMaxBodySizePerMille` | Takes |
|---|---|---|
| Wolf | **1,300** | deer (1,200), boar (850), hog (850), fox (550), raccoon (400), skunk (300), rabbit (200), rat, frog — **never** a moose (2,100) or a bear (2,150) |
| Fox | **250** | rabbit (200), rat (100), frog (50) |

`a-20` backed 1,000 for the wolf, which excludes the deer; real wolves take deer and the deer is the
roster's whole reason for a pack, so it is **1,300 here, and that is ours**. `bodySizePerMille`
becomes a `SpeciesDef` field (the existing `bodyLengthMm` is drawing only): rabbit 200, deer 1,200,
fox 550, raccoon 400, skunk 300, boar 850, moose 2,100, wolf 850, bear 2,150; the existing hog 850,
rat 100, frog 50. The skunk is taken by a wolf in FA2; FA3's spray may make it the one animal a
predator leaves alone, and that is FA3's to decide.

**Nothing hunts people as prey in this search.** Colonists are §5's, under its own conditions.

### 3b. The chase is the existing melee driver

A hunt is **`Job_AttackMelee` with `Pawn.CombatTarget` set to the prey and `Job.DestCell` =
`AttackMeleeJobDriver.ToTheDeath`** — the job a drafted colonist's order on a downed bandit already
uses (`JobSystem.Attack.cs`, `AttackMeleeJobDriver.cs:101`), filled by `AttackJob.Fill` as
`AnimalCombatThinkNode` fills a revenge. Reasons, each an existing property rather than a new one:

- **It runs.** `Pawn.UrgencyPerMille` answers `Job_AttackMelee` and `Job_Flee` at the draft's pace
  (design 33 §2h), so predator and prey both run, and the pace table decides who wins: a rabbit at
  1,300 outruns a wolf at 1,100 in the open and is caught only when cornered — **a hunt can fail,
  which is the point.**
- **It re-plans against a moving target** every `chaseRepathTicks` (60) and picks sides so a pack
  does not stand on one tile (design 33 §7c).
- **It gives up.** An unordered attack ends after `rechooseTicks` (300) at a step boundary, which is
  a chase lost; the predator then takes a **long rest** (twice its `restTicksMax`) before hunting
  again. The rest job is saved, so the cooldown needs no field.
- **It is saved** mid-swing and resumes on the same ticks (design 33 §21c's lockstep).

The prey's side is design 64's: a predator within an animal's startle radius is a threat like a
person, and the prey's flight is the ordinary `Job_Flee`. **Design 64 must list a predator as a
startle source** — noted here so the two documents agree. Struck, the prey rolls its revenge as
today (`CombatSystem.React`): a boar sounder turning on a wolf is design 64's herd rule and is
exactly the fight the owner's "big defenders" were chosen for.

### 3c. The pack

When a wolf starts a hunt, every other **wolf within the herd radius (12, design 64)** that is
standing, is not fleeing and is not in a revenge **joins on the same target** with the same job —
hungry or not, because a pack hunts as one (owner's answer 1: *"the wolf is a pack predator"*).
Pack membership is design 64's herd rule (same species, within the radius), not a saved group.
Nobody else joins: a fox hunts alone.

## 4. Feeding, and the kill that is left

### 4a. The corpse stays

`Kill` writes a corpse (`CombatSystem.Apply.cs`, `CorpseRegistry.Add`) drawn from its kind's figure
on its flank. **The owner's answer is "the kill is left"**, and K3's hunting will later salvage
corpses, so a kill must **not** vanish. It is **not shrunk** either: a shrinking corpse needs art
nobody has, and a half-size deer reads as a small deer.

**The corpse gains one number, `EatenPerMille`**, raised as predators feed and read by K3 as a
smaller meat yield (`meat × (1,000 − eaten) / 1,000`). It is drawn exactly as today. `Corpse` is a
readonly struct, so feeding replaces the record in place; `odyssey.corpses` gains a layout number
(the pattern of the combat section's layout 4) and the field is hashed. **No game-wide save bump.**
`CorpseRegistry` still has no `Remove`: rot and removal remain K2/K3's.

### 4b. `Job_Feed`

A predator whose target died goes to the corpse's cell (or beside it) and eats: `Job_Feed`, a new
animal job def, **600 ticks** of standing still (the pack's **Eat** clip, the art design's to drive).
It takes `min(its need, what is left)`:

```
meat on a corpse      = the prey's bodySizePerMille × (1,000 − EatenPerMille) / 1,000
food a predator gains = meat taken × 1,000 / the predator's bodySizePerMille
```

So a fox eating a rabbit gains 363 food and a wolf gains 235 — a rabbit is a snack to a wolf; a
deer (1,200) takes a pack of three wolves from hungry to fed and is mostly eaten. **Every hungry
packmate feeds on the one corpse**, each taking its share in pawn-id order at the moment it starts,
so the arithmetic is deterministic. A fed predator leaves the kill; nothing guards it.

A predator eats **only a corpse it or its pack made** in FA2 — scavenging an old corpse is a
recorded later behaviour (`b-wild-animal-behaviour.md` §8), and it would also have predators
feeding on colonists' dead bandits.

## 5. The man-eater

### 5a. When

A predator may make **a colonist** its target only when **all** hold (owner's answer 6):

1. it is **Starving** (food 0);
2. the **world rule** `predatorsHuntPeople` is on (§5c);
3. the colonist is **alone**: no *other* standing colonist within the herd radius (12 cells) of her;
4. she is reachable, within `huntRadius`, and standing.

It prefers animal prey: the §3a search runs first and a person is considered only when it finds
nothing. A starving wolf beside a deer takes the deer.

### 5b. The stalk, then the charge

A colonist cannot be taken by surprise without the player being told, so the man-eater does not
charge from the think. It **stalks**: `Job_Stalk`, a new animal job, walks (not runs) to within
**`chargeCells` (5)** of her, re-planning every `chaseRepathTicks`, and **re-tests "alone" at every
re-plan**. The moment another colonist is within 12 cells of her, or she comes within the home area's
hearth ring (design 43, the home area — a starving wolf does not walk into the colony), the stalk ends
and the predator backs off to rest.

At `chargeCells` and still alone, it **charges**: the §3b melee job, the colonist as target, packmates
joining as §3c. **From the charge on, "alone" is no longer tested** — a committed attack that
evaporated whenever somebody appeared would teach the player that walking over is enough, when the
owner's rule is that it must be *driven off* (§6c). The colonist answers by her Response (fight back,
defend, flee) as she answers a bandit; colonists on *Defend* come to her because a predator attacking
her is `Melee.ColonistUnderAttackBy` like any attacker.

The stalk is at walking pace deliberately: it is the time the *Stalked* alert (§7) gives the player.
At a wolf's walk from the 30-cell radius that is about half a game hour.

### 5c. The world rule

There is no world-rules object today: `ColonyRequest` carries seed, size, name, scenario and board
flags, and the only saved per-world choices are the ones inside the map. **Add `WorldRules`**, one
small class on `ColonyRequest` and its own save section **`odyssey.rules`**, hashed because a tick
reads it. One field in FA2: `predatorsHuntPeople`, **default on** (the owner chose the rule, with a
switch to turn it off, not an opt-in).

- **Where it is set:** the New Game page (design 19 §3) gains one switch in the board row, *Predators
  hunt people*, amended into design 19 when built. It is a world's rule, so it is **not** in Settings,
  which are the player's.
- **Changed later:** the debug menu's Cheats tab carries the same switch, writing through an intent
  so it is recorded like any other state change.
- **An old save** loads with the rule on — the section is absent, the default applies.

If the storyteller branch (design 59) lands a difficulty object first, this field moves into it and
`odyssey.rules` is not created; the rule and its default are the same either way.

## 6. The kill window — the owner's exception to design 33

### 6a. The rule

Design 33 §3, "Death takes the player": *nobody's self-defence strikes a body on the ground — so an
unordered fight ends in downs, never in corpses.* The owner's answer 7 makes **one** exception:
**a predator may kill a downed colonist**, and answer 11 sets the terms: **she dies at the end of one
game hour (2,500 ticks) unless the predator is driven off.**

This covers a person downed by a predator *and* a person already down (from a fall, a raid, a bleed)
whom a starving predator finds alone: the §5a conditions are tested against the downed colonist like
a standing one, except that "standing" is dropped. **Animal prey is not in the window** — the hunt's
`ToTheDeath` kills an animal at the ordinary rate of blows.

### 6b. `Job_Maul`

When a predator's target is a **person** and goes down, the melee job ends (as it does today,
`AttackMeleeJobDriver.cs:101`, because a hunt on a person is **not** `ToTheDeath`) and the predator
takes **`Job_Maul`** on her: it stands beside her and, on the needs cadence, takes her towards the
death line on a schedule rather than by blows:

```
started   = Job.WorkTicks            (the tick the maul began; saved with the job)
left      = maulWindowTicks − (now − started)            (2,500 at the start)
remaining = HpMilli − DeathAtMilli                      (what is left above the death line)
this bite = remaining × interval / max(left, interval)
```

Each bite goes through **`CombatSystem.Hurt`** — the one owner of damage (design 43 §7) — with the
predator as attacker and its natural attack's kind, so it lands on a region, opens a wound, bleeds,
raises `DamageApplied`, and **the bite that crosses the line calls `Kill` and she is mourned** like
any death. Because each bite divides what is left by the time left, **she dies on the last interval
of the window whatever her pool was when she went down** — pain shock downs a person at 64 points
(design 43), a blow at nought, and both get the same hour. Her own bleeding runs meanwhile and
can only make it shorter, which is correct and is the doctor's cue.

**One mauler a victim.** `Job_Maul` reserves her; a packmate arriving stands beside her and waits
(an idle wait, not a second maul), so **a pack does not shorten the hour** — the window is the
victim's, not the predator's.

### 6c. Driven off

The maul ends, and the victim lives, when any of these is true:

| | Because |
|---|---|
| **The mauler is struck**, by a blow or a bullet | a predator's revenge is 1,000 (`a-20`), so `CombatSystem.React` already turns it on its attacker and interrupts the maul — **no new rule**. The rescuer now has a wolf to fight, which is what "drive it off" means. |
| **Its own pool falls below half** | it flees (`Job_Flee`) and takes the long rest. A predator is not a bandit; hurt, it runs. `fleeBelowPerMille` 500 on the species. |
| **She is lifted** (`CarriedBy != 0`) | a rescue (design 33 C4) has happened under its nose; it loses the kill and backs off to rest. See §10, Q2. |
| **She is dead** | the window ran out; the predator feeds (§10, Q1) and leaves. |

A colonist merely **standing near** does **not** end it — the owner chose the rescue window over
"any colonist nearby scares it off" — so walking over is not enough, but any blow is. The maul
counts as attacking her for `Melee.IsAttacking` and `ColonistUnderAttackBy`, so colonists on
*Defend* come of their own accord; that one line matters more than any other in this section.

### 6d. After

A colonist driven off her mauler is a downed patient with many bitten wounds, bleeding: rescue to a
bed (design 33 C4) and treatment (design 37 medical supplies, design 43's one doctor's job) take it
from there with no change. A predator that has been driven off does not return to the same victim
for its long rest.

### 6e. What changes in design 33

§3's bullet becomes, word for word:

> **Death takes the player — with one exception.** Only the blow that crosses −50 % of the pool
> kills, a bandit hunts only colonists who are standing, and nobody's self-defence strikes a body on
> the ground — so an unordered fight ends in downs, never in corpses, **except that a starving
> predator may maul a downed colonist it finds alone, and she dies at the end of one game hour unless
> it is driven off (design 65 §6, the owner's rule of 2026-09-26).**

The `CLAUDE.md` CM row's "**An unordered fight ends in downs, never deaths.**" gains "— save a
predator's maul (design 65)". `FightGuardTests` keeps its assertion for every kind but a predator,
and gains the one that says so (§9).

## 7. The alerts

Two keys, both new, both raised by `AlertModel` from published aspects:

| Key | Label | Severity | Lead | Detail | Jumps to |
|---|---|---|---|---|---|
| `ui.alert.stalked` | Stalked | **Warning** | *Ada* is being stalked | a starving *ridge wolf* is following her — bring her in or send company | the predator |
| `ui.alert.mauled` | Mauled | **Danger** | *Ada* is being mauled | *43 min* to drive it off | the predator |

- **Pinned**: neither can be dismissed while it holds (no `DismissKey`), because dismissing a
  colonist's death sentence is not a choice the panel should offer.
- **Jumps to the predator** (the row's pawn is the predator; the victim's name is the target text),
  since the predator is what the player must act on; the victim is beside it.
- **The countdown is derived**: the predator publishes the maul's start tick, and the panel prints
  the minutes left. Nothing saved for it.
- **Sounds**: severity first (design 24 §2), so *Stalked* chimes `alert.negative` and *Mauled*
  `alert.negative` on the day they appear. *Mauled* earns an **override row** of its own —
  a snarl under the sting — but no clip exists; it is a named hook (`alert-maul`) for the owner to
  source, and until then it is not the raid horn, which would tell the player the wrong thing.
- **No alert for animals hunting animals.** That is the woods going about its business.

Published on the predator, sparse (only while set, like the combat aspects): `pawn.hunt.victim`
(pawn id), `pawn.hunt.phase` (1 stalk, 2 maul), `pawn.hunt.since` (the maul's start). The activity
line (design 17 §5a's derived statuses) reads *Hunting*, *Stalking*, *Mauling*, *Feeding*; a
predator's pane shows its **Hunger** as a bar with *Fed / Hungry / Starving*, the rung's visible-in-
words promise (answer 5) applied to the one state that makes a wolf dangerous.

Wiki keys, in the content commit: `ui.alert.stalked`, `ui.alert.mauled`, `ui.status.hunting`,
`ui.status.stalking`, `ui.status.mauling`, `ui.status.feeding`, `ui.animal.hunger` with its three
states, `ui.setup.predatorshuntpeople`, `ui.debug.predatorshuntpeople`.

## 8. Determinism, save, hash, goldens, cost

- **Deterministic by construction**: prey choice is nearest-then-id, the pack is a radius test, the
  maul is arithmetic on saved ticks. The only rolls are the ones that exist (revenge, the flee cell).
  No new `PawnPurpose` stream.
- **Saved**: hunger and starvation already are (§2a). New: three animal job defs (`Job_Stalk`,
  `Job_Maul`, `Job_Feed`, appended — their start tick rides `Job.WorkTicks`, as `BuildJobDriver`
  set the precedent), `Corpse.EatenPerMille` (section layout +1), `odyssey.rules`. **No save format
  bump.**
- **Hashed**: all of the above. Three job defs appended **move every golden by their counters**
  (the content-lives-in-Defs lesson), and hunger moving moves the wooded meadow's. Re-bake and
  **measure with `GoldenColonyProbe`**: the colony's own numbers must be identical on all three
  boards, because in one golden day no predator can starve (seeded ≥ 500, a wolf starves in ≥ 1.25
  days) and so none can go near a colonist. Animal-on-animal kills may appear in day one (a fox at
  500 hunts within a quarter-day); they are expected and the probe's animal census will show them.
- **Cost**, against design 64's budget of **< 0.05 ms a tick for the whole wildlife layer at 48
  animals and 50 colonists**: hunger is one integer per animal every 150 ticks. The prey scan runs
  only for a hungry predator at a think (a handful of predators, a think every few hundred ticks),
  over at most 80 animals with `CanTravel` asked in distance order. The "alone" test is 50 distance
  checks at a re-plan every 60 ticks, only while stalking. The maul is one `Hurt` an interval. The
  measurement is `WildlifeCostTests.ThePredatorLayerAgainstTheBudget` (§9) — timed, not estimated.

## 9. Tests, written first

Fast tier unless marked. Names are the contract.

| Test | Proves |
|---|---|
| `PredatorHungerTests.AWolfsFoodFallsOnTheNeedsCadence` / `APreyAnimalsFoodNeverMoves` | §2b, only predators tick |
| `PredatorHungerTests.SeededHungerIsStaggeredAndDeterministic` | §2b |
| `PredatorHungerTests.AStarvingPredatorLeavesTheBoardAndDoesNotDie` | §2d |
| `PreyChoiceTests.TheNearestReachableSmallerAnimalIsTaken` / `NeverLargerThanItsLimit` / `NeverItsOwnSpecies` / `TiesGoToTheLowerId` / `AnUnreachableRabbitIsSkipped` | §3a |
| `PreyChoiceTests.AFedWolfHuntsNothing` | §2c |
| `PackHuntTests.WolvesWithinTheHerdRadiusJoinAndFoxesDoNot` | §3c |
| `HuntTests.ARabbitInTheOpenOutrunsAWolf` / `ACorneredRabbitIsCaught` | §3b, the pace table decides |
| `HuntTests.ALostChaseEndsInALongRest` | §3b |
| `FeedingTests.AKillLeavesACorpseAndFeedsTheEater` / `APackSharesOneCorpseInIdOrder` / `EatenPerMilleSurvivesALoad` | §4 |
| `ManEaterTests.OnlyAStarvingPredatorConsidersAColonist` / `OnlyWhenSheIsAlone` / `NeverWithTheRuleOff` / `AnAnimalIsAlwaysPreferred` | §5a |
| `ManEaterTests.ASecondColonistCallsOffTheStalk` / `TheChargeIsNotCalledOff` / `ItNeverStalksIntoTheHearthRing` | §5b |
| `WorldRulesTests.TheRuleIsSavedHashedAndDefaultsOnForAnOldSave` | §5c |
| `MaulTests.SheDiesOnTheLastIntervalOfTheWindow` (from a blow down and from pain shock) | §6b, the window is the victim's |
| `MaulTests.APackDoesNotShortenTheHour` / `StrikingTheMaulerEndsIt` / `LiftingHerEndsIt` / `AHurtPredatorFlees` / `StandingNearbyDoesNot` | §6b–c |
| `MaulTests.DefendersComeToAMauledColonist` | §6c, the one line that matters |
| `MaulTests.AMaulResumesTheSameAcrossASave` (lockstep twin, hashing every interval) | §8 |
| `FightGuardTests.TheMaulIsTheOnlyUnorderedKill` — the combat soak with predators and the rule **off** ends in downs, never deaths | §6e |
| Hud: `AlertModelTests.AStalkRaisesStalkedAndAMaulRaisesMauled` / `NeitherCanBeDismissed` / `TheCountdownIsDerived` | §7 |
| Long: `PredatorSoakTests.TenDaysWithWolvesOnThreeSeeds` — invariants hourly, kills counted, a lockstep twin | the gate |
| PlayMode, `Measurement`: `WildlifeCostTests.ThePredatorLayerAgainstTheBudget` at 48 animals and 50 colonists | §8, the number |

## 10. Open for the owner

1. **Does a man-eater feed on the colonist it kills?** Recommend **yes, and nothing about her body
   changes**: its hunger is restored (so it leaves, rather than killing again the next morning) and
   the corpse is drawn and treated exactly as any colonist's death. `EatenPerMille` is not raised on
   a person's corpse. The alternative — it kills and goes hungry — makes a man-eater a serial killer
   by arithmetic, which is darker than the owner's rule asked for.
2. **Does lifting her count as driving it off?** Recommend **yes** (§6c): a colonist who reaches her
   and carries her away has saved her, and the predator backs off. The alternative — it turns on the
   carrier, who cannot fight while carrying — makes every lone rescue a second victim, and the owner
   chose a window that is *winnable*.
3. **The wolf's prey limit of 1,300**, so wolves take deer (§3a). `a-20` backed 1,000 from the
   reference. Recommend 1,300: a pack that can never take a deer has nothing to be a pack for.

## 11. What this does not cover

Bear foraging and bear store raids (the bear has no hunger in FA2); scavenging old corpses; a
predator taking tamed animals or livestock (there are none); dens and cubs; winter boldness and
rabies (`b-wild-animal-behaviour.md` §8, later); corpse rot and removal (K2/K3); the hunt
designation and butchering (K3, which reads `EatenPerMille`); predators hunting bandits (a bandit is
a person but not a colonist, and §5 is about colonists — a starving wolf leaves bandits alone);
drawing a part-eaten corpse; the *Mauled* snarl clip itself (a hook, §7); gunfire startling a
predator off a maul (design 64's startle, if it has one). The temperament rungs, the startle, the
warning display and the herd rule are design 64's; the art, the **Eat** clip's driver and the far
form are the art design's.
