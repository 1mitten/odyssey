# 64: Animal temperament

**Status (2026-09-26): designed, nothing built.** The non-predator half of **FA2** in the forest
animals unit, branch `claude/forest-animals`. The interview is `docs/research/forest-animals-interview.md`
(answers 5, 12 and 16 are this document's), the research `docs/research/a-20-wild-animal-temperament.md`
and `b-wild-animal-behaviour.md`. **Predators** — hunger, prey, man-eaters and the kill window — are
**design 65**; the per-species signatures (skunk spray, the rut, grazing, raids, hours) are **design
67**; the art, the computed poses and the far form are **design 66**. This document owns what every
animal does when a person comes near or strikes it, and what a colonist does about a warning.

It sits on **design 29** (the animal is a pawn), **design 30** (wildlife), **design 33** (combat: the
revenge roll, `Job_Flee`, downs not deaths) and **design 47** (a bullet is a blow).

## 1. What is being built

The owner, 2026-09-26: *"recommend how we should stat them — how aggressive they get (if at all) and
their behaviours"*, and on the interview's question 5, **five rungs, visible**.

- **Five rungs** — Timid, Skittish, Defensive, Territorial, Predator — written in words on the
  inspect pane and in the Almanac, so a player never learns a species by losing a colonist to it.
- **A rung is not a switch.** It is the *name* of a combination of independent integer fields on
  `SpeciesDef` (§3), the lesson from Dwarf Fortress in `b-wild-animal-behaviour.md`: startle
  radius and response, flee distance, a warning display with a bluff chance, territory, herd
  radius, revenge, rage. The word on the pane is **derived from the fields** (§3c), so the two can
  never disagree.
- **Proximity**: an animal notices a person inside its startle radius and answers by its response —
  bolt, freeze then bolt, stand, or display (§4). Today nothing is proactive; this is the new part.
- **The warning display**, with a bluff: most charges stop short (§5).
- **The herd**: a group answers as the one that was startled or struck answers (§6).
- **Revenge re-shaped**: the existing per-blow roll gains the ×3-in-melee rule and a rolled rage
  of 10,000–26,000 ticks, and a cornered Skittish animal fights (§7).
- **A colonist heeds a warning** (owner, question 12): undrafted, she backs off and paths round;
  drafted, she does as she is ordered (§8).

## 2. What is true today (`origin/main`, `b3d3128d`)

- The animal tree is `DownedThinkNode → AnimalCombatThinkNode → AnimalShelterThinkNode →
  AnimalIdleThinkNode` (`JobSystem.cs:523`). **It is consulted only between jobs**: a resting
  animal costs one integer increment a tick and thinks when its rest ends.
- **Revenge and flight are built**, in `CombatSystem.React` (`CombatSystem.Apply.cs:217`): every
  blow that lands on a standing animal rolls `SpeciesDef.revengePerMille`; turning, it sets
  `RetaliateAgainst` for `CombatDef.revengeTicks` (10,000); not turning, it starts `Job_Flee` to
  `FleeJobDriver.FindFleeCell` at `CombatDef.fleeCells` (12). **A bullet is a blow** (design 47):
  a ranged hit reaches `React` by the same path, so melee and range roll the same chance today.
- Nowhere to run is no flight: `Flee` returns and the animal carries on.
- `Flee` and every attack already run at `MovementDef.draftedPacePerMille` (`Pawn.UrgencyPerMille`).
- **Nothing is proactive**: no startle, no warning, no herd, no territory. Design 30 §7's "nothing
  flees" is stale since combat and is corrected by FA1.
- The pather has **one soft per-cell cost seam**, `PathFinder.Occupancy` (`OccupiedBias` 30), set by
  nothing in the game, and one hard-coded detour class, `NavFlags.BuildSite` (`MoveCost.SiteDetour`
  120), which is a nav flag and **marks the chunk dirty** when it changes.

## 3. The fields

### 3a. On `SpeciesDef`

Every field is an integer (or a small enum) and defaults to what an animal does today, so the hog,
rat and frog load unchanged until their rows are edited. Cells are Chebyshev on the ground, on the
animal's own layer and one either side, as `CombatDef.helpRadiusCells` is.

| Field | Meaning | Default |
|---|---|---|
| `startleCells` | a person this near is noticed while the animal is walking or grazing | 0 (never) |
| `territoryCells` | the same, while the animal **rests** — a bear surprised at its bed. 0 means use `startleCells` | 0 |
| `approach` | what it does on noticing: `None`, `Bolt`, `FreezeThenBolt`, `Stand`, `Display` | `None` |
| `freezeTicks` | `FreezeThenBolt`: how long it holds still before it runs, if the person is still inside | 0 |
| `fleeCells` | how far it runs, from a startle or a blow. 0 means `CombatDef.fleeCells` | 0 |
| `runPerMille` | its pace while fleeing or charging. 0 means `MovementDef.draftedPacePerMille` | 0 |
| `warnTicks` | `Display`: how long the warning lasts before it acts | 0 |
| `chargeCells` | `Display`: a person closing to this is charged at once, warning or not | 0 |
| `bluffPerMille` | `Display`: the chance a charge is a **bluff** that stops short | 0 |
| `chargeTicks` | a real charge's window: how long it presses the attack before breaking off | 0 |
| `herdCells` | same-kind animals this near answer as the group (§6). 0 = solitary | 0 |
| `herdJoinTicks` | how long an unstruck member keeps up a fight it joined | 0 |
| `revengePerMille` | **existing**; now the chance **at range**. In melee it is tripled, capped at 1,000 (§7a) | as now |
| `rageTicksMin`, `rageTicksMax` | how long a turned animal hunts its attacker, rolled once when it turns | 10,000 / 10,000 |
| `fightsCornered` | struck or startled with nowhere to run, it turns instead of cowering | false |

`CombatDef.revengeTicks` stays as the fallback when a species leaves the rage fields at nought,
so no existing tuning line moves. `CombatDef` gains one constant, `meleeRevengeFactor = 3`.

Predators add their own fields in design 65 (a predator flag, hunger, prey size); the signatures in
design 67 modify some of these seasonally. **Both go through §3d's one reader**; neither writes a
second copy of these numbers.

### 3b. The nine forest species, and where the old three sit

Starting values from `a-20` §Recommendation and `b-wild-animal-behaviour.md`, converted to our cells
(2.5 m) and ticks (60 a second at ×1, 2,500 an hour). **INVENTED where marked**; every number is a
playtest number and is tuned in play, not argued in advance.

| Species | Rung | startle / territory | approach | flee | warn / charge / bluff | herd / join | revenge ‰ (melee) | rage | cornered |
|---|---|---|---|---|---|---|---|---|---|
| Rabbit | Timid | 5 / — | FreezeThenBolt, 90 ticks | 14 | — | 6 / — | 0 (0) | — | cowers |
| Deer (doe, stag) | Timid | 8 / — | Bolt | 16 | — | 10 / — | 0 (0) | — | cowers |
| Fox | Skittish | 5 / — | Bolt | 10 | — | — | 60 (180) | 10–18 k | fights |
| Raccoon | Skittish | 4 / — | Bolt | 8 | — | 4 / 1,200 | 80 (240) | 10–18 k | fights |
| Skunk | Skittish | 3 / — | **Display**, 240 / 1 / 0 (the spray is design 67's end to the display) | 6 | 240 / 1 / 0 | — | 100 (300) | 10–18 k | fights (sprays) |
| Boar | Defensive | 0 / — | Stand | 12 | — | 8 / 1,800 | 250 (750) | 10–26 k | fights |
| Moose (cow, bull) | Defensive | 4 / — | **Display** | 12 | 180 / 2 / 800 | 8 / 2,400 | 330 (990) | 10–26 k | fights |
| Wolf | Predator | 7 / — | Bolt (shy of people unless design 65 says otherwise) | 14 | — | 12 / 3,600 | 1,000 (1,000) | 10–26 k | fights |
| Bear | Territorial | 3 / **6** | **Display** | — | 250 / 2 / 700 | — | 500 (1,000) | 10–26 k | fights |
| *Midden hog* (existing) | Defensive | 0 / — | Stand | 12 | — | 6 / 1,800 | 700 (1,000) — **unchanged** | 10 k (as now) | fights |
| *Duct rat* (existing) | Skittish | 3 / — | Bolt | 8 | — | — | 50 (150) — unchanged | 10 k (as now) | fights |
| *Culvert frog* (existing) | Timid | 3 / — | Bolt (it does not swim, design 30 §8b) | 6 | — | 4 / — | 0 (0) — unchanged | — | cowers |

Reading it:

- **The deer's startle radius, 8 cells (20 m), is the longest in the forest** — the "one snort and
  the herd is gone" animal. A rabbit lets you closer (5) and holds still first. A wolf keeps 7 cells
  off a person, which is what makes a wolf rarely seen rather than a threat, until design 65.
- **The boar is the hog's own rung**: it ignores you (`Stand`, startle 0) until struck, and then it
  turns more often in melee than at range, and so does the sounder.
- **The moose and the bear both warn**; the moose warns closer (4) and bluffs more (800), the bear
  warns sooner at rest (territory 6) and charges for real three times in ten.
- **The midden hog's and the rat's revenge numbers do not move.** The melee factor makes the hog a
  sure fighter at arm's length (700 × 3, capped) — which is the reference's "a struck animal fights
  back in melee" and what the combat gate already assumed of it.

### 3c. The rung is derived

`Temperament.RungOf(SpeciesDef)` names the rung from the fields — **one owner, no stored enum** —
first match wins:

1. **Predator** — design 65's predator flag.
2. **Territorial** — `approach == Display` and `territoryCells > startleCells`.
3. **Defensive** — `approach` is `Stand` or `Display`, or its melee revenge reaches 1,000 (the
   group stands and fights).
4. **Skittish** — it runs on approach and `fightsCornered`.
5. **Timid** — everything else that runs.

An animal with `startleCells` 0 and `approach` `None` and no revenge — a species nobody has written a
temperament for — reads **Timid**; the moose's display with a low territory reads **Defensive**, which
is what the owner was shown. `TemperamentTests.EveryForestSpeciesDerivesTheRungTheDesignNames` pins
§3b's rung column against the derivation, so tuning a number that moves a word fails a test rather
than the pane.

### 3d. One reader for the numbers in force

`Temperament.Of(Pawn, PawnContext)` returns a small struct of the numbers **in force now** —
`SpeciesDef`'s, then design 67's season (the rut raises the stag's and bull moose's approach to
`Display` and lowers their bluff) and design 65's hunger. Every reader in this document asks it; no
node reads `SpeciesDef` temperament fields directly. That is the pattern the hop price (`NavGraph.HopCost`)
and cover (`Cover.BaseAt`) already follow, and the test is the same shape:
`TemperamentHasOneReaderTests` reads the Sim sources and fails on a temperament field read outside
`Temperament.cs`.

## 4. Noticing a person

### 4a. Where the scan lives

**A new subsystem, `AnimalAwarenessSystem`**, `TickGroup.Normal`, in the pawn phase **after
movement** (so it sees where everybody stepped this tick) and before `JobSystem` thinks.

It cannot live in the think tree: an animal thinks only between jobs, and a resting deer must bolt
*while* it rests. The scan's answer is therefore an **interrupt plus a job started there and then** —
exactly how `CombatSystem.Flee` already starts `Job_Flee` from inside the blow.

**Staggered**: an animal is scanned on the tick where `(tick + id) % AwarenessInterval == 0`, with
`AwarenessInterval = 30` (half a second at ×1, INVENTED). A person walking at the colonist pace
covers under two cells in that time, inside every radius in §3b but the skunk's, whose `Display` fires
on the next scan instead.

For each due animal that is wild, standing, not leaving, not already in a temperament job
(§4b) and not retaliating: take the nearest **standing person** — colonist or hostile alike (see
§12, open question 1) — within `territoryCells` if it is resting, else `startleCells`, on its own
layer or one either side, **and reachable to it** (`ctx.CanTravel` under the animal's own mode is not
asked; the ground-distance test is enough to be startled across a stream). Ties on distance break on
the lower pawn id, so the answer is a function of the state.

### 4b. The answers

| `approach` | Job started | Ends |
|---|---|---|
| `None` | nothing | — |
| `Stand` | nothing; the pane says *Watching* while a person is inside (derived, not a job) | — |
| `Bolt` | **`Job_Flee`** from the person's cell, `fleeCells` away (the existing driver), and the herd (§6) | on arrival, as now |
| `FreezeThenBolt` | **`Job_Freeze`**: a wait facing the person, `freezeTicks` long | the person leaves the radius → idle; the person comes within half the radius, or the freeze runs out with the person still inside → `Job_Flee` |
| `Display` | **`Job_Display`** (§5) | §5 |

The person being answered is `Pawn.CombatTarget`, which is **already saved and hashed** (combat
section, layout 2): the freeze and the display face it, the flight runs from it, and nothing new is
written to the pawn record.

### 4c. What it costs

Per tick the scan visits `A / 30` animals, each testing every person: at the owner's population of
48 animals (interview answer 13) and 50 colonists plus 20 bandits, that is **1.6 × 70 ≈ 112**
Chebyshev tests a tick — two index-to-coordinate divisions and three subtractions each, well under a
microsecond. A startle adds one `FindFleeCell` (at most 25 column searches) and, for a herd, a pass
over the animals (48). **Budget for the whole wildlife layer** — this scan, `WildlifeSystem`, the
animal thinks and the herd — **under 0.05 ms a tick** at 48 animals and 50 colonists on the played
map, measured, not assumed:

- `WildlifeLayerCostTests.TheWildlifeLayerAtFortyEightAnimalsAndFiftyColonists` — **Long tier**,
  `Category("Measurement")`, on `PlayedMap`, a colony of 50 walking through a board seeded to 48,
  timing `AnimalAwarenessSystem` and the animal share of `JobSystem` with `PhaseTrace`'s segments
  beside a control arm with the scan switched off, both inside one run. It **logs** the number and
  asserts only a generous ceiling (0.5 ms), because a wall-clock gate in the Long tier has turned
  `main` red once (`docs/lessons.md`); the budget lives here.
- If it misses, the lever is a per-layer bucket of persons built once a tick (the crowd index's shape,
  `PawnCrowdIndex`), not a longer interval — a longer interval is a slower deer.

## 5. The warning display

`Job_Display` is a wait facing `CombatTarget`, `warnTicks` long, drawn with a computed pose (design
66: the stamp, the rear, the tail up) and a *Warning* floater over the animal. It ends one of four
ways, checked each tick by its driver:

| Happens | Outcome |
|---|---|
| The person leaves the radius (plus one cell, so a person on the edge does not flicker it) | **Stands down** — back to idle. The warning worked. |
| The person closes to `chargeCells` | **Charges now**, whatever is left of the warning |
| The warning runs out with the person still inside | **Charges** |
| The animal is struck | `React` as today (§7) — a blow ends every warning |

**A charge rolls its bluff once, when it starts** (`PawnPurpose.Bluff`, a new stream keyed on the
animal's id and the tick):

- **A bluff** is `Job_BluffCharge`: a run at `runPerMille` to within two cells of the person, a
  stop, then a stand-down. No blow is struck. It is the bear's usual answer (700) and the moose's
  (800).
- **A real charge** sets `RetaliateAgainst` to the person for `chargeTicks` — not the full rage —
  and hands over to the existing `AnimalCombatThinkNode`, so the attack is combat's own job, pace,
  reach and clip. **An unordered fight ends in downs** (design 33): a Territorial or Defensive animal
  stops at a down. Only a predator may go on (design 65; the owner's answer 7).

A display is the **animal's** state and the colonist's back-off (§8) is a reaction to it; the two
share nothing but the animal's cell and radius.

## 6. The herd

**The herd follows the one that answered.** There is no herd identity, no leader and nothing saved:
a herd is *the same kind within `herdCells`* at the instant, as the reference's same-species radius
is (`a-20` §2). Solitary species have `herdCells` 0 and skip this section.

- **A flight spreads.** When an animal bolts (startled or struck), every standing, non-fleeing
  same-kind animal within `herdCells` starts `Job_Flee` **from the same threat cell** — the deer's
  flagged tail and the rabbits scattering, as one rule. It is not recursive: those that join do not
  spread it further, so one pass over the animals, and a scattered herd does not chain across the
  board.
- **A fight spreads.** When an animal turns (revenge, a real charge, or cornered), every standing
  same-kind animal within `herdCells` turns on the same attacker for `herdJoinTicks` — **shorter
  than the struck one's rage**, so the unstruck lose interest first (Don't Starve's beefalo, `b-` §1).
  A member already retaliating keeps its own longer window.
- A member downed, leaving, or in a temperament job of its own is skipped.

The herd pass runs where the answer is made — in `React` for a blow, in the awareness scan for a
startle — so it costs one walk over the animals **per event**, never per tick.

## 7. Revenge, rage and the cornered

### 7a. The melee factor

`React` gains one rule: the chance is `revengePerMille` for a blow from range and
`min(1000, revengePerMille × meleeRevengeFactor)` for a blow from within reach — **whether the
attacker is within melee reach at the instant**, not whether the weapon is a gun, so a colonist
clubbing with her pistol (design 47 §12) counts as melee and one shooting from twelve cells does not.
The roll stays **one per blow**, which is one per shot while our guns fire single rounds; `a-20`'s
"roll per shot, not per pellet" is recorded here for the day a burst weapon arrives.

### 7b. Rage

A turning animal draws its rage from `[rageTicksMin, rageTicksMax]` on the same deterministic stream
as the roll (the second draw), and it ends as today — on its expiry, on the attacker being downed,
dead or gone, or on the animal being downed. 10,000–26,000 is uniform with the reference's mean of
18,000 (`a-20` §2); a turned bear hunts its attacker for between four and ten game hours.

### 7c. Cornered

Today `Flee` with nowhere to go does nothing. After this unit, with `fightsCornered` it **turns** as a
successful roll would; without, it **cowers** — a `Job_Freeze` facing the threat for `freezeTicks`
(or 120 if nought), then it tries again. A rabbit walled into a corner is caught; a fox bites.

## 8. A colonist heeds a warning

The owner's answer 12: **back off and path round**; **drafted, she does as she is ordered.**

### 8a. Who heeds

An undrafted colonist, standing, not downed, not carried, **not on a player-forced job** (a
right-click order is the player's hand as much as a draft is). A bandit does not heed (§12, open
question 1).

### 8b. Backing off

A **warning zone** is a displaying or bluff-charging animal's cell and its radius at that instant
(`territoryCells` if it was resting when it began, else `startleCells`), plus one. `WarningZones`
(Sim, `Pawns/Wildlife`) derives the list from the animals whose job is `Job_Display` or
`Job_BluffCharge`, **once a tick, before movement** — it is **not saved**, because both jobs and
`CombatTarget` are; it is rebuilt on load for nothing.

When a heeding colonist is **inside** a zone at the awareness scan, she is interrupted and given
**`Job_GiveWay`**: the flee driver's own walk (`FleeJobDriver.FindFleeCell`, from the animal, to just
outside the zone), at her own walking pace — she backs off, she does not run. Her activity line reads
**Giving way** and the pane's detail names the animal (*Giving way to a Quarry bear*). When she gets
there she thinks again, and the path she is given goes round.

### 8c. Pathing round — a soft cost in the cell search, not a nav flag

**Recommended: a per-request avoidance cost in the cell stage of `PathFinder`, fed by `WarningZones`.**

- **Not a nav flag.** `NavFlags.BuildSite` is the precedent for "a cell passers-by avoid", but a
  flag change calls `MarkDirty` and moves with the animal every step — a district rebuild per bear
  step (`NavGraph.Rebuild` is 0.87 ms per edited cell on Huge, design 28 §2). A flag is for things
  that stand still.
- **The seam already exists.** `PathFinder` carries `Occupancy`, a per-cell predicate adding
  `MoveCost.OccupiedBias` in every relax, set by nothing in the game. It becomes a general
  **extra-cost** callback — `Func<int, int>?`, the occupancy bias being one caller — and the warning
  zones are another: **`MoveCost.WarningZone = 400`** a cell inside a zone (four orthogonal steps;
  INVENTED), so a six-cell detour round a bear is cheaper than three cells through its reach, and a
  zone that fills a corridor is still crossed rather than making the target unreachable.
- **Per request.** `PathRequest` gains a `HeedsWarnings` bit, set when the requester would heed (§8a).
  An animal's, a drafted colonist's and a bandit's requests pay nothing. With no zones on the board —
  nearly every tick — the callback is null and the search is unchanged, **byte for byte**, which is
  what keeps a quiet golden from moving on this line.
- **Only the cell search prices it.** The region graph and the mover do not, which is deliberate and
  is the one place this departs from the hop price's three-owners rule: it is a *preference*, like
  occupancy, never a reachability fact. The abstract corridor is regions (10 × 10 cells), so a detour
  of a few cells fits inside it; where it does not, the path crosses the zone at the price, and she
  gives way again when she is inside. `WarningZoneTests.TheSearchAloneReadsTheZone` holds that the
  region graph and `MoveCost` tables are untouched.
- **Paths already walking.** A colonist whose path was made before the zone appeared is not
  re-planned by the zone; she walks until the awareness scan finds her inside it (§8b) and gives way.
  That costs her the half-second the zone was invisible to her, and saves a re-plan of every path on
  the board each time a bear stands up.

**What it costs the colony, stated**: a job whose cell is inside a resting bear's territory waits
until the bear moves. The job is not failed or unassigned; the work giver's `Reachable` is unchanged,
so she goes, gives way, thinks, and — the zone still standing — takes other work if the target is
the only way. That is the owner's choice over "work comes first" (question 12), and the activity line
says why she is not felling that tree.

## 9. What the player sees

- **The pane**: a new line on an animal's inspect pane, **Temperament** — *Timid*, *Skittish*,
  *Defensive*, *Territorial*, *Predator* — with the rung's description as its tooltip. For a turned
  animal a second chip, **Enraged**, while `RetaliateAgainst` holds. Published as two pawn aspects
  (the rung index, derived from the species, and the enraged flag), both derived, neither hashed.
- **The status line** (the Animals tab and the pane): *Watching* (a freeze, or `Stand` with a person
  inside), *Warning*, *Charging* (a charge, bluff or real — the player cannot tell until it stops,
  which is the point), *Fleeing* (existing), *Fighting* (existing).
- **A floater**, *Warning*, over an animal when its display begins, **only when the person it faces
  is a colonist** (§12, open question 2).
- **The Almanac**: each Fauna entry gains a *Temperament* fact row with the word and one line of
  what it means for a colonist — *"Warns, then charges. Most charges stop short."*
- **A colonist**: *Giving way* on her activity line (§8b).

### 9a. Proposed registry keys

For FA's content commit (this document does not edit the CSV). The `ui.status.*` ones sit in a
namespace `RegistryTests.NoPlayerFacingNameIsWrittenInCSharp` guards, so no literal may carry them.

| Key | Label | Description |
|---|---|---|
| `ui.temperament.label` | Temperament | How an animal answers a person who comes near |
| `ui.temperament.timid` | Timid | Runs from anyone who comes near. Never fights, even cornered |
| `ui.temperament.skittish` | Skittish | Keeps its distance. Fights if cornered or hurt |
| `ui.temperament.defensive` | Defensive | Stands its ground. Hurt one and the group may turn on you |
| `ui.temperament.territorial` | Territorial | Warns anyone who comes near, then charges. Most charges stop short |
| `ui.temperament.predator` | Predator | Hunts other animals when hungry (design 65 words the rest) |
| `ui.status.watching` | Watching | An animal holding still and watching somebody near it |
| `ui.status.warning` | Warning | An animal warning somebody off before it charges |
| `ui.status.charging` | Charging | An animal running at somebody. It may stop short |
| `ui.status.enraged` | Enraged | An animal hunting whoever hurt it |
| `ui.status.givingway` | Giving way | A colonist backing off from an animal's warning |
| `ui.floater.warning` | Warning | Over an animal that has begun to warn a colonist off (namespace to match the *Cover* floater's) |

## 10. Save, hash, goldens

- **No new pawn field, no new section, no format bump.** The four new jobs — `Job_Freeze`,
  `Job_Display`, `Job_BluffCharge`, `Job_GiveWay` — are job defs; the job in hand is saved and hashed
  as every job is (`Job.ContributeTo`), and the person answered is `CombatTarget`, already in the
  combat section at layout 2. The charge's window and the rage are `RetaliateAgainst` /
  `RetaliateUntilTick`, already saved and hashed. `WarningZones` and the rung are derived. A save
  mid-display resumes the same display: `TemperamentSaveTests.ASaveMidWarningResumesTheSameWarning`.
- **The new random draws** are `PawnPurpose.Bluff` and a second draw on the revenge stream for the
  rage. Keyed on id and tick, never on order.
- **Every golden moves**, for two reasons, and both are measured rather than accepted: four new job
  defs shift the job counters (the rule in `content-lives-in-defs`), and on the played board
  colonists now **startle animals**, so animal positions move. The probe (`GoldenColonyProbe`) should
  show the colony's economy the same within noise and the animals' positions different; a colony
  number that moves by more than noise is a finding, not a re-bake. The bare board has no animals and
  its golden should move by the counters alone.
- **Content fingerprints** (`PawnContentDefTests`) move for the new `SpeciesDef` fields.

## 11. Tests to write first

In the house order (`docs/process.md`): a failing test for each rule before its code. Fast tier
unless marked.

| Test | Holds |
|---|---|
| `TemperamentTests.EveryForestSpeciesDerivesTheRungTheDesignNames` | §3c's derivation against §3b's rung column, and the hog, rat and frog |
| `TemperamentHasOneReaderTests.NoFieldIsReadOutsideTemperament` | §3d, by reading the Sim sources |
| `StartleTests.ADeerBoltsFromAColonistInsideItsRadiusAndNotOneCellOutside` | the radius, both sides of the edge, on the same layer and one above |
| `StartleTests.ARestingAnimalIsStartledWithoutWaitingForItsRestToEnd` | the scan interrupts; the think tree alone would not |
| `StartleTests.ARabbitFreezesThenBolts` / `...StandsDownWhenThePersonLeaves` | `FreezeThenBolt` both ways |
| `StartleTests.TheScanIsStaggeredByIdAndVisitsEachAnimalOnceAnInterval` | the cost model in §4c |
| `DisplayTests.ABearWarnsAndStandsDownWhenTheColonistWalksAway` | the warning worked |
| `DisplayTests.AColonistWithinChargeCellsIsChargedAtOnce` | the warning is cut short |
| `DisplayTests.MostChargesStopShort` | over 200 seeds, the bear's real-charge fraction within 300 ± 60 per mille, and a bluff strikes nothing |
| `DisplayTests.ARealChargeEndsAtADown` | design 33's rule holds for a non-predator |
| `HerdTests.TheHerdBoltsTogetherFromTheSameThreat` / `...AndDoesNotChainAcrossTheBoard` | §6's flight, one pass |
| `HerdTests.TheSounderTurnsWhenOneTurns` / `TheUnstruckLoseInterestFirst` | §6's fight and `herdJoinTicks` |
| `RevengeTests.MeleeTriplesTheChanceAndCapsItAtAThousand` / `APistolWhipIsMeleeAndAShotIsNot` | §7a |
| `RevengeTests.RageIsDrawnBetweenItsBounds` | §7b |
| `CorneredTests.ASkittishAnimalWithNowhereToRunTurns` / `ATimidOneCowers` | §7c |
| `GiveWayTests.AnUndraftedColonistBacksOffAndPathsRound` | §8b–c end to end: she arrives, by a longer path that does not enter the zone |
| `GiveWayTests.ADraftedColonistWalksStraightThrough` / `AForcedJobWalksStraightThrough` / `ABanditDoesNotGiveWay` | §8a |
| `WarningZoneTests.TheSearchAloneReadsTheZone` / `NoZoneIsTheSearchUnchanged` | §8c: the region graph and `MoveCost` untouched; with no zone, the same path and checksum as before |
| `TemperamentSaveTests.ASaveMidWarningResumesTheSameWarning` / `ALockstepTwinHashesTheSameThroughAStartle` | §10 |
| `WildlifeLayerCostTests.TheWildlifeLayerAtFortyEightAnimalsAndFiftyColonists` | **Long, Measurement**: §4c's budget, logged |
| `AnimalsModelTests.TheTemperamentLineSaysTheRungInWords` (Hud) | §9, through `Registry.Label` |

**Unity tier**: nothing new for the rules. The poses, the floater and the Almanac row are drawn and
are design 66's and the HUD's to prove.

## 12. Open for the owner

Only the decisions this document could not take from the interview.

1. **Do bandits startle animals, and can an animal charge a bandit?** *Recommended: yes to both, and
   a bandit does not give way.* A person is a person to a deer, and a bear that charges a raider
   walking past its bed is the woods taking a side for a moment — cheap, and it reads. The
   alternative (animals notice colonists only) is one line in the scan and makes the woods blind to
   raids.
2. **The *Warning* floater: every display, or only one facing a colonist?** *Recommended: only a
   colonist's*, so a raid through the woods does not paper the screen with warnings the player can do
   nothing about. The status line says it either way.

## 13. What this does not cover

Predators, hunger, prey, a colonist stalked or killed (design 65); the skunk's spray, the rut, crop
grazing, the raccoon's raid and the activity hours (design 67); the computed poses for the freeze,
the stamp, the rear and the charge, and the far form (design 66); taming and wildness (a later unit;
`a-20`'s wildness is not read here); hunting, the Hunt order and what a hunted animal does beyond
§7 (cooking's K3); young, mothers and protective aggression (no breeding exists); rabies; animals
fleeing from gunfire that did not hit them (the reference's only proximity flee — recorded as a
later rule, one line in the projectile's landing); animals startling one another across species
(a deer does not bolt from a boar; the predator's prey is design 65's).
