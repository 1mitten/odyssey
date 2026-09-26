# 67: Animal signatures and activity hours

**Status (2026-09-26): designed, nothing built — branch `claude/forest-animals`, worktree
`D:\code\odyssey-forest`.** This is **FA3**, the third of the forest animals unit's three PRs
(`docs/research/forest-animals-interview.md` §6, answer 16). It is built after FA2 is played.

It sits on **design 64** (temperament: the five rungs, the startle radius, the warning display, herds,
colonists backing off) and **design 65** (predators and hunger). Neither is restated here; where this
document needs one of their mechanisms it names it. The research is `a-20-wild-animal-temperament.md`
and `b-wild-animal-behaviour.md`; the owner's answers are the interview's §6.

**Read this before touching** `SpeciesDef`'s activity fields, `AnimalIdleThinkNode`'s active test,
`WildlifeSystem`'s arrivals, `Thoughts.xml`, the graze or raid drivers, or `Theft`.

## 1. What is being built

The owner's answers, 2026-09-26: all four signatures (*"Skunk spray, The rut, Crop grazing, Raccoon
raids stores"*), and activity hours with winter sleep (*"Night, day, dusk + winter sleep"*).

- **Activity hours.** `nocturnal` becomes a pattern — day, night, or dawn-and-dusk — and the bear
  is away for the winter. This comes first, because grazing and raiding are defined by it.
- **Skunk spray.** A skunk that is crowded warns (design 64's display) and, if the colonist is still
  there when the warning ends, sprays: **no damage**, a day's mood penalty, and a smell her neighbours
  notice.
- **The rut.** Stags and bull moose are *In rut* for the month before winter, and for that month they
  play one rung harder: they warn, then charge, a colonist who crowds them.
- **Crop grazing.** Rabbits and deer, at dawn and dusk, walk into a growing zone and eat a crop, one
  cell at a time.
- **Raccoon raids.** At night a raccoon walks into the stores, takes a mouthful of food and carries
  it off the board.

Every one of these is **one rule with one owner**, read by the existing single animal think tree. No
new tick system is added.

## 2. Activity hours

### 2a. The rule

Today (`JobSystem.cs` `AnimalIdleThinkNode`) an animal is *active* when `IsNight(ctx) ==
species.nocturnal`, night being 20:00 up to 06:00; off its hours it takes a third as many legs and
rests three times as long (`OffHoursFactor = 3`). Nothing else reads the flag.

The flag becomes a field, `SpeciesDef.activeHours`, one of four values:

| Value | Active | Hours (on the 24-hour board clock) |
|---|---|---|
| `Day` | by day | 06:00 up to 20:00 — exactly `!IsNight`, today's non-nocturnal |
| `Night` | by night | 20:00 up to 06:00 — exactly today's nocturnal |
| `Twilight` | at dawn and dusk | 04:00 up to 08:00, and 17:00 up to 21:00 |
| `Always` | every hour | — (nothing today; a slot for the frog if play wants one) |

The dusk window is set against the drawn sky: the daylight cycle's dusk sits at about 19:30
(`PlayerBench`'s "dusk (19.5 h)" step), so 17:00–21:00 brackets the orange light, and dawn mirrors it
about the growing window's 06:00 start.

**One owner:** `ActivityHours.IsActive(SpeciesDef, PawnContext)`, a static in the sim beside
`AnimalIdleThinkNode.IsNight`. The idle node, grazing (§6), raiding (§7) and the pane all ask it;
nobody else compares hours.

**`nocturnal` is kept in the XML loader as a synonym for one release** — `nocturnal="true"` reads as
`Night`, absent as `Day` — so the hog, rat and frog Defs need no edit to stay identical, and a test
holds that they are (§12). Then the field is deleted in the same commit as its three uses.

### 2b. Winter away

The owner chose that the bear *"sleeps through winter out of sight"*. The cheapest reading of that
which is also correct in play: **the bear leaves the board for Rime and comes back in Wash.**

- `SpeciesDef.wintersAway` (bool). On the first rare tick of **Hollow** (Rime's first month,
  `Calendar.MonthOfYear == 4`) every bear on the board is marked `Leaving` — the field departures
  already use, already saved and hashed — and walks to the nearest reachable edge as any leaver
  does. A bear in a fight finishes it first: `Leaving` is read in the idle node, below the combat
  node, so a raging or downed bear leaves when it can.
- `WildlifeSystem` admits **no arrival** of a `wintersAway` kind in Rime, and `WildlifeSeeder` places
  none at tick zero in Rime. From Larkspur the level-keeper's ordinary top-up brings bears back by
  their table weight. No bear is remembered across the winter; animals carry no name.
- No den, no sleeping bear on the board, no new saved field. A den you could stumble on is a better
  story and a later unit (§14).

### 2c. The per-species table

| Species | `activeHours` | `wintersAway` | Note |
|---|---|---|---|
| Midden hog | Day | — | unchanged (today not nocturnal) |
| Duct rat | Night | — | unchanged (today nocturnal) |
| Culvert frog | Day | — | unchanged |
| Rabbit | **Twilight** | — | grazes only in these windows (§6) |
| Deer | **Twilight** | — | grazes only in these windows (§6) |
| Fox | Night | — | |
| Raccoon | Night | — | raids only by night (§7) |
| Skunk | Night | — | sprays at any hour it is crowded |
| Boar | Night | — | |
| Moose | Day | — | |
| Wolf | Night | — | design 65's hunts are not gated by hours: hunger is |
| Bear | Day | **yes** | away through Hollow and Candle |

Off its hours an animal still **reacts** — startle, warning, retaliation and design 65's hunger are
all above the idle node in the tree. Hours govern what it chooses to do, never what it does when
disturbed.

## 3. Skunk spray

### 3a. The rule

The skunk's rung is Skittish (design 64) with one difference, set by a field:
`SpeciesDef.warnEnds = Spray` where every other warning species' display ends in `Charge`. Design
64 owns the display — the freeze, the stamp, the raised tail, its duration and the colonist's backing
off. This document owns only **what happens when the display runs out and somebody is still there**:

1. Every **person** — colonist, bandit or gunman — within **2 cells** of the skunk (4.5 m at 2.5 m a
   cell, the striped skunk's real reach, `b-wild-animal-behaviour.md`), on its own layer or one either
   side, with an open line from the skunk (`LineOfSight`, design 47, so a wall between is a shield),
   is **sprayed**.
2. The skunk then flees (design 64's flee), its warning spent.

**No damage, no hit points, no bleed.** A spray never enters `CombatSystem.Hurt`.

There is **no cooldown**. Colonists back off from a warning (the owner's answer 12), so a spray
happens only to somebody who did not: a drafted colonist ordered to stand, one whose job is at the
skunk's feet, or one cornered. Spraying the same colonist twice renews her memory rather than
stacking it (§3b), so repetition costs nothing new and needs no saved field.

### 3b. The thought

A memory thought, appended to `Thoughts.xml` and `ThoughtIndex` (append only; a thought index rides
every saved memory):

| `Thought_Sprayed` | | why |
|---|---|---|
| `moodOffset` | **−45** | between a night on the ground (−40, a quarter day) and a friendly-fire blow (−80, a day): unpleasant, humiliating, not a grievance |
| `durationTicks` | **60,000** | the owner's "a day" |
| `stackLimit` | 1 | |
| `renewsOnRepeat` | **true** | a second spray restarts the day, as design 33 §14e's friendly-fire memory does |

**The smell is situational, not stored.** Any colonist within 2 cells of a colonist who carries an
unexpired `Thought_Sprayed` gets **−10** while she is there, added in `NeedsSystem.UpdateMood` beside
the temperature's offset, which is situational for the same reason. It is read off the sprayed
colonist's saved memory, so it needs no field, and a load restores it for free. The cost is a scan
of the colonists who carry the memory (almost always none) per mood update. The sprayed colonist does
not smell herself; she has the −45.

Bandits and gunmen are sprayed but have no mood; the spray only sends them the skunk's way less often
(nothing: raiders do not path round skunks). That is recorded rather than built.

### 3c. What is drawn

Presentation only, nothing in a cell:

- a puff at the skunk's tail, yellow-green, one short particle burst, **one draw call**;
- a floater, *Sprayed*, over each person sprayed (the combat floater channel, as *Cover* is);
- while a colonist carries the memory, a faint rising haze over her, read from a **derived,
  unhashed aspect** published from her memories (`PawnAspect.Reeks`), exactly as crouching behind
  cover is derived (design 53);
- a hiss (`SoundIds.SkunkSpray`, unsourced until the owner supplies one).

## 4. The rut

### 4a. The rule

There is no autumn in this calendar: a year is six months of twelve days in three seasons, Wash,
Glare and Rime (`Calendar`, `proper-nouns.csv`). **The rut is Ember**, Glare's second month and the
month before Rime (`MonthOfYear == 3`) — the reference animals' "late summer into autumn, before the
winter", and twelve days, long enough to meet and short enough to be an event.

`SpeciesDef.rut`: a small record, present on the deer and the moose only.

| Field | Deer | Moose | Meaning |
|---|---|---|---|
| `month` | 3 | 3 | Ember |
| `sex` | Male | Male | only stags and bulls |
| `rung` | Territorial | Territorial | the rung it plays for the month (design 64) |
| `territoryRadius` | 3 | 4 | cells; design 64's field, overridden for the month |

A stag is Timid for five months and Territorial in Ember; a bull moose is Defensive for five and
Territorial in Ember. Design 64's Territorial rung does the rest: a colonist who stays within the
radius is warned, and charged if she stays. Colonists back off (answer 12), so **the usual cost of
the rut is a detour**, and the danger falls on a drafted colonist or a worker whose job is beside the
animal — the same people the skunk sprays.

**Derived, never saved.** `InRut(pawn, tick)` = the species has a rut, the pawn's sex matches, and the
month is the rut's month. Sex is derived from the pawn's own roll seed (design 64's decision), and the
month from the tick, both of which are already saved, so the rut needs no field and no hash
contribution of its own. What it *causes* — a warning, a charge, a colonist's detour — is state, and is
hashed where it lands.

### 4b. What shows

- The inspect pane's temperament line reads *Territorial · In rut* for the month, where it reads
  *Timid* the rest of the year (design 64 owns the line; this adds the tag).
- The Almanac entry for each says when: *"The stag is dangerous in Ember."*
- On the animal: while in rut and resting, a computed head-toss every few seconds (a neck-bone
  rotation laid over Idle, in the manner of `QuadrupedGait`), so a rutting stag reads as one before
  anybody clicks it.

Bulls fighting each other is recorded, not built (§15).

## 5. The order the signatures are asked

All four ride the one animal tree. After this unit it reads, top to bottom:

`Downed → AnimalCombat (revenge) → Startle/Warn (design 64) → Hunt (design 65) → Shelter (rain) →
AnimalIdle`

and **AnimalIdle** gains, in this order after the leaving and bank rules and only while
`ActivityHours.IsActive`:

1. **Raid** (§7) — raccoons;
2. **Graze** (§6) — rabbits and deer;
3. the wander-or-rest roll, unchanged.

A raid or a graze is a *choice of what to do next*, so it lives in the idle node rather than a node of
its own; a startled grazer or raider is interrupted by design 64's node above it, because that is a
reaction.

## 6. Crop grazing

### 6a. The rule

A rabbit or deer in its active window, on a think, rolls `grazePerMille`. On success it looks for the
**nearest planted cell within `grazeRadius`** whose crop is at least **a third grown** (stage 2 or 3 —
a sprout is not worth the walk), that it can reserve and reach. Found, it takes `Job_Graze`:

- **Reserve the cell** with the key the sower and harvester already use,
  `ReservationManager.Key(ReservationTargetKind.Cell, cell)`. So a harvester never walks to a cell a
  deer is eating, and a deer never walks to a cell a harvester has claimed — the reservation is the
  whole of the conflict rule, because both work givers already call `CanReserve` on that key.
- **Walk to it** in its own mode.
- **Graze** for `grazeTicks`, the pack's Eat clip playing (§6c).
- **`GrowingZones.Uproot(cell)`** — the crop is gone, the cell is fallow, and the zone's own loop has
  a colonist re-sow it. No new zone code: `Uproot` is how a harvest ends and already marks the
  re-mesh.

It **does not** eat the harvest: nothing lands on the ground and no animal hunger is fed. Grazing is
behaviour, not a need. (If design 65's hunger ever spreads beyond predators, a graze is where a
grazer's hunger would be fed — the seam is the end of the driver.)

**Uproot, rather than setting the growth back,** because it is legible: a bare cell in a green row is
unmistakable, where a crop one stage shorter is not; and because it uses the one path every crop
already leaves by. The price to the colony is the sow work and the growth — a real loss, and the
owner's stated purpose: *"A reason to wall a field."*

### 6b. What protects a field

Nothing new — the defences are rules that already exist or arrive in FA2:

- **A colonist nearby.** Design 64's startle radius (rabbit 5, deer 7) fires above the idle node, so a
  grazer with a colonist inside its radius flees, drops the job, releases the reservation, and **the
  crop survives** (the uproot is the last step, not the first).
- **A wall and a closed door.** Rabbits and deer are `TraverseMode.Animal`, which never opens a door
  (`TraverseModes.OpensDoors`), so a walled field with a door is unreachable and never chosen —
  the reachability test does it.
- **Predators** keep grazers moving (design 65), which is a real, emergent protection.

### 6c. Numbers

| | Rabbit | Deer | Note |
|---|---|---|---|
| `grazePerMille` | 200 | 100 | per active think with a crop in range. INVENTED |
| `grazeRadius` | 10 | 14 | cells, board distance |
| `grazeTicks` | 1,250 | 2,500 | half an hour and an hour on the board clock |

With four hours in each window and the graze itself this long, one rabbit takes at most about **three
cells a day** and a deer herd of four about **six**, unstartled. These are play numbers:
`GrazingTests.AHerdLeftAloneTakesAboutSixCellsADay` prints the measured figure on a known field so
tuning is a number, not a feeling.

**The Eat clip** is the first per-job clip an animal plays. The figure path today blends locomotion by
speed only; the art document (FA1) looped `Eat` on import, and FA3 asks the figure director to play a
kind's `Eat` row while the pawn's published activity is *Grazing*, falling back to Idle for a kind
with none. Presentation only.

### 6d. Cost

A graze think walks `GrowingZones`' ascending `_planted` list once, testing board distance, and asks
reachability of **at most four** candidates nearest-first before giving up. At 300 planted cells and
~15 grazers thinking about every 600 ticks, that is under ten distance tests a tick on average.

## 7. Raccoon store raids

### 7a. The rule

A raccoon in its active window (night), on a think, rolls `raidPerMille`. On success it asks
**`Theft.NearestLoot`, filtered to food** — an item whose `ItemDef.nutrition` is above zero — across
loose, stored and contained stacks (a stockpile cell, a shelf, food left on the ground). The
bandits' loot rule is reused, not forked: `NearestLoot` gains a predicate parameter, and the bandit
passes "anything".

Found, it takes the bandit's **`Job_Steal`** with its target and the nearest reachable edge, with one
difference set by the thief being an animal:

- **It takes a portion, not the stack.** Up to `raidTakeNutrition` **500** nutrition — the cook's
  fetch, three carrots — taken with `ColonyItems.SplitOff`, as the cook and the doctor already split
  (at least one unit, so it takes one meal whole). The rest of the stack stays where it was.
- **It does not look for a fight** on the walk. `StealJobDriver` re-chooses on `rechooseTicks` for a
  colonist to go back and fight; for an animal that branch is skipped. Its reactions are design 64's
  (Skittish: it flees a colonist in its startle radius), above the job in the tree.
- **Startled, it drops what it carries** where it stands (the ordinary drop, `CellHasSpace`), and
  flees. So a colonist who wakes and walks past gets the food back without a fight.
- **At the edge it leaves the board with the food** — `Theft.Leave`, the bandit's exit, through
  `PawnRegistry.Despawn`. The food is lost. The level-keeper's top-up brings raccoons back, so the
  thief returns on another night.

### 7b. What keeps it out

- **A closed door.** The raccoon must be **`TraverseMode.Animal`**, not `Climber`: the rat's mode opens
  doors (`OpensDoors` is everyone but `Animal` and `Bandit`), and a raccoon that opened doors would
  make a locked pantry pointless. **Design 64/66 set the raccoon's mode; this document requires it be
  `Animal`.** So a store indoors behind a door is safe, and an outdoor stockpile or shelf is fair game
  — which is the decision the raccoon exists to create.
- **A colonist awake nearby** (design 64's startle).
- **Being killed** by a drafted colonist: a raccoon has 25 hit points.

### 7c. Numbers

| | Raccoon | Note |
|---|---|---|
| `raidPerMille` | 40 | per active think with food reachable. A night is ten hours, ~40 thinks, so about one raid a night per raccoon. INVENTED |
| `raidTakeNutrition` | 500 | three carrots, or one meal |

### 7d. How the colony hears of it

- The raccoon's activity line reads **Raiding** while it carries food (derived at publish from the job
  and the pawn being an animal, rather than a second job def — the bandit's reads *Stealing*).
- When it leaves the board with food, the ledger records a **recorded incident**, `Incident_AnimalRaid`,
  through the same `RecordedIncidentWorker` a bandit's theft uses, so the **Events panel** says it —
  *Raided: a gutter raccoon took 3 carrots* — with its own ink and chime, and it is in the History.
  `Theft.Leave` takes the incident index as a parameter instead of naming `Incident_Theft`.
- **No pinned alert.** Alerts are for danger that is still happening and wants a response; a raccoon is
  gone before anyone could answer one. The Events line is the whole of the warning, and the lesson —
  put a door on the pantry — is one a player learns from it.

What it carries is drawn at the rig's jaw joint (every SIMPLE rig has `Head_JawSHJnt`), small; a
kind with no jaw joint hides the load, as a swimmer's is hidden.

### 7e. Cost

`NearestLoot` is linear in the board's item stacks with a reachability test for each nearer candidate,
and is asked at most once per raccoon think at night after the 40‰ roll: at two raccoons, 200 stacks
and a think every ~600 ticks, **well under one stack test a tick** on average.

## 8. Every field this adds to `SpeciesDef`

| Field | Type | Default | Read by |
|---|---|---|---|
| `activeHours` | enum Day/Night/Twilight/Always | Day | `ActivityHours.IsActive` |
| `wintersAway` | bool | false | `WildlifeSystem`, `WildlifeSeeder` |
| `warnEnds` | enum Charge/Spray | Charge | design 64's warning, this §3 |
| `rut` | record or absent | absent | `InRut`, design 64's rung lookup |
| `grazePerMille`, `grazeRadius`, `grazeTicks` | int | 0 | the graze choice (0 = never grazes) |
| `raidPerMille`, `raidTakeNutrition` | int | 0 | the raid choice (0 = never raids) |

All integers; nothing in floating point enters the sim. `nocturnal` is read for one release (§2a) and
then removed.

## 9. Save, hash and goldens

| Change | Saved | Hashed | Goldens |
|---|---|---|---|
| Activity hours | no (Def) | no | **none move** for the conversion: hog, rat and frog map to identical windows, asserted |
| Winter away | `Leaving`, already | already | only a golden that crosses into Rime with a bear; none does (the one-day goldens start in Larkspur) |
| Skunk spray | the memory, already | already | only where a skunk is crowded |
| The smell | no, derived | no (mood target is hashed, and moves) | as spray |
| The rut | no, derived | no | only in Ember |
| Grazing | new `Job_Graze`, **appended** | the job index, the crop state, already | **every golden moves** — a new job def moves the job counters (the standing lesson: a new job moves every golden). Re-bake and measure with `GoldenColonyProbe`: the colony should differ in job counters and, on the played board, in crop cells a grazer took |
| Raids | `Job_Steal` reused; `Incident_AnimalRaid` **appended** to the incident table | the ledger, already | as above, and on any board with raccoons and stores |

Append-only indexes this touches — `ThoughtIndex`, `JobIndex`, the incident names — are also appended
by other open branches (traits, faces, prisoners, trading). **Take the next free number on merge, not
at design**, and re-bake after.

## 10. Cost against the wildlife budget

The unit's target (interview answer 13) is the **whole wildlife layer under 0.05 ms a tick** at 48
animals and 50 colonists. This document's share is small by construction:

| Rule | Asked | Worst case per ask |
|---|---|---|
| Active hours | every animal think | one integer compare |
| Winter away | one rare tick at Hollow's start | one pass over the pawns |
| Spray | at a warning's end | people within 2 cells (from design 64's scan) + a line test each |
| The smell | per colonist mood update | the colonists carrying the memory (usually 0) |
| Rut | where design 64 reads the rung | one compare |
| Graze | an active grazer's think, after a 100–200‰ roll | planted cells (distance) + ≤ 4 reachability tests |
| Raid | a raccoon's night think, after a 40‰ roll | item stacks + reachability for nearer ones |

**Measured, not argued**: `SignatureCostTests.TheSignaturesFitTheirShareOfTheWildlifeBudget` (category
`Measurement`) runs the played board with 48 animals of the forest mix, 50 colonists, a 300-cell field
and 200 stacks for a game day with the signatures on and again with every `*PerMille` at zero, in one
run, and logs the difference; the assertion is **under 0.01 ms a tick**, a fifth of the layer's budget.

## 11. What a player reads — registry keys

Proposals for `icon-keys.csv`, milestone `FA`, for the owner to correct in the wiki. None exists yet.

| Key | Label | Tooltip seed |
|---|---|---|
| `ui.status.grazing` | Grazing | An animal eating a crop in a growing zone |
| `ui.status.raiding` | Raiding | An animal carrying food out of the stores |
| `ui.animals.inrut` | In rut | Dangerous this month: it warns, then charges, anyone who crowds it |
| `ui.animals.hours.day` | Out by day | Active from morning to evening |
| `ui.animals.hours.night` | Out at night | Active from evening to morning |
| `ui.animals.hours.twilight` | Out at dawn and dusk | Active in the half-light, and resting otherwise |
| `ui.animals.wintersaway` | Away in Rime | Leaves the board for the winter and comes back in Wash |
| `ui.thought.sprayed` | Sprayed by a skunk | *The first key in a `ui.thought` namespace — see below* |
| `ui.thought.reek` | Someone reeks | Standing near a colonist who was sprayed |
| `ui.combat.sprayed` | Sprayed | The floater over whoever a skunk caught |
| `ui.bulletin.animalraid` | Raided | An animal carried food off the board |

**`ui.thought` is a new namespace.** No thought has a registry label yet — the Thoughts tab is still an
open item (design 33) — so these two are the first. The plan should either add the namespace with
labels for all twelve existing thoughts in the same commit, or leave the spray's two unlabelled until
the tab arrives; recommended the former, since it is twelve rows and the tab will need them.

## 12. Tests to write first

In the fast tier unless marked.

**`ActivityHoursTests`**
- `TheOldNocturnalFlagMeansNightAndItsAbsenceMeansDay` — the hog, rat and frog: a day run before and
  after the conversion ends on the same hash.
- `ATwilightAnimalIsActiveAtDawnAndDuskAndNotAtNoonOrMidnight`
- `ABearIsMarkedLeavingOnTheFirstRareTickOfHollow`
- `NoBearArrivesOrIsSeededInRime` and `BearsArriveAgainInLarkspur`
- `ARagingBearFinishesItsFightBeforeItLeaves`

**`SkunkSprayTests`**
- `ASkunkSpraysAColonistWhoIsStillThereWhenItsWarningEnds`
- `AColonistWhoBacksOffIsNotSprayed`
- `ASprayDoesNoDamage` — hit points and the health ledger unchanged.
- `AWallBetweenIsAShield` and `ThreeCellsAwayIsOutOfReach`
- `ASecondSprayRenewsTheDayRatherThanStacking`
- `ANeighbourFeelsTheSmellAndNothingIsStoredForIt` — mood target moves, no memory on the neighbour.

**`RutTests`**
- `AStagIsInRutInEmberAndInNoOtherMonth` and `ADoeIsNeverInRut`
- `InRutAStagWarnsAColonistWhoCrowdsItAndOutOfRutItFlees`
- `TheRutSurvivesASaveMidEmberWithNoFieldOfItsOwn` — save, load, same behaviour, same hash.

**`GrazingTests`**
- `ARabbitAtDuskGrazesAHalfGrownCropAndTheCellIsFallow`
- `TheZoneResowsAGrazedCell`
- `ASproutIsNotGrazed` and `ANoonRabbitDoesNotGraze`
- `AGrazerNeverTakesACellAHarvesterHasReserved` and the converse.
- `AColonistInTheStartleRadiusSavesTheCrop` — the grazer flees, the reservation is released, the crop stands.
- `AFieldBehindAClosedDoorIsNeverChosen`
- `AHerdLeftAloneTakesAboutSixCellsADay` — calibration: prints the figure, asserts only a wide band.

**`RaccoonRaidTests`**
- `ARaccoonAtNightTakesFoodFromAStockpileAndLeavesTheBoard`
- `ItTakesAPortionAndLeavesTheRestOfTheStack`
- `ItTakesFromAShelf` and `ItTakesFoodLeftOnTheGround`
- `ItNeverTakesAnythingThatIsNotFood`
- `AClosedDoorKeepsItOut`
- `AStartledRaccoonDropsWhatItCarries`
- `ARaidThatLeavesIsOnTheLedgerAndABanditTheftIsStillATheft`
- `ARaccoonNeverRaidsByDay`

**Unity tier**: `SignatureCostTests.TheSignaturesFitTheirShareOfTheWildlifeBudget` (PlayMode,
`Measurement`); `AnimalFigureTests.AGrazingKindPlaysItsEatClip`, which asks whether *that kind's* row
resolved before asserting (the SIMPLE art is not on the runner).

## 13. Build order inside FA3

1. **Activity hours**, conversion first with the identity test, then Twilight, then winter away.
2. **Skunk spray** (needs FA2's warning display).
3. **The rut** (needs FA2's Territorial rung).
4. **Grazing**, then the Eat clip.
5. **Raids**, then the ledger line and the jaw-carried load.
6. Registry rows, wiki rebuild, the Almanac lines, the goldens re-baked and probed, the cost test.

Each step is a commit; the PR is played as a whole (answer 16).

## 14. Open for the owner

1. **The rut month.** The calendar has no autumn. **Recommend Ember** (the month before Rime), the
   nearest thing to late summer into autumn; the alternative is Hollow, Rime's first month, which
   stacks the rut on the cold and makes the winter's opening fortnight the dangerous one.
2. **Winter away or a den.** **Recommend away** (off the board for Rime, back in Wash): no new state, and
   it is what "out of sight" says. A den — a bear asleep under cover on the board for the winter, which
   a colonist can wake — is the better story and costs a saved den cell, a sleeping pose and a
   disturbance rule; recommended as its own later unit if the first winter feels empty.
3. **Graze: uproot or set back.** **Recommend uproot** (the cell goes fallow and is re-sown): legible
   and one existing path. Setting growth back a stage is gentler and nearly invisible.
4. **The smell on neighbours.** **Recommend yes, at −10 within 2 cells**: cheap, derived, and it makes
   the sprayed colonist eat alone for a day, which is the joke. Say no and the spray is her problem only.
5. **A raccoon's haul leaves the board.** **Recommend yes** (the food is lost; the raccoon is gone
   until the level-keeper tops one up). The alternative — it carries the food to cover and eats it,
   staying on the board — keeps the thief around and loses the food just the same, at the cost of a
   second job.

## 15. What this does not cover

- **Bulls clashing in the rut**, young and **protective mothers** (no breeding exists), and the
  **den** (§14.2).
- **Hunger for grazers** — grazing feeds nothing; design 65's hunger is predators only.
- **Bears raiding stores** (the reference animals do; the raccoon is the store thief here).
- **Rabid animals**, **pack boldness in winter** and **scavenging corpses** — `b-wild-animal-behaviour.md`
  ranks them later.
- **Raiders avoiding skunks** — a bandit is sprayed and does not care; it has no mood.
- **Temperature for animals** — Rime does not harm an animal; the bear leaves for a behavioural reason,
  not a thermal one.
- **The Thoughts tab** — this document adds two thoughts and their keys; showing a colonist's thoughts
  is design 33's open item.
- **Hunting what grazes** — K3 (cooking), which reads the yields design 66 puts on the species.
