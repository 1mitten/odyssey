# a-11 — Storyteller and incidents

**Question.** How does RimWorld classify, schedule and present incidents; how do its three
storytellers differ mechanically; what data does an incident definition carry; how do cargo pod
drops work; how do quests differ from incidents; and how do the owner's proposed cadence types
("regular / weekly / ad hoc / condition-based", "periodic / one-off") map on to it?

Asked 2026-09-20 for the events line of work (`docs/design/23-events-and-storyteller.md`). One
subagent, capped at 12 searches or 15 page reads; it used 9 and 13. Clean room: mechanics, formulas,
data shapes and design intent only. No decompiled source was fetched or quoted.

## Findings

### 1. Incident taxonomy

Every event is an *incident* with a *category*; the storyteller picks a category first, then an
incident inside it by weight.

| Category | Typical contents | Good / neutral / bad | What fires it |
|---|---|---|---|
| ThreatBig | raids, infestation, manhunter pack, psychic wave, crashed ship part, mech cluster | bad | on/off threat cycle, or a random weighted roll |
| ThreatSmall | a single mad animal, minor nuisances, some weather | bad, survivable | mean-time-between |
| Misc | cargo pods, wanderer joins, wild person, animals join, self-tame, ambrosia, party, aurora, herd migration, blight, beavers, eclipse, solar flare, heat wave, cold snap, flashstorm, toxic fallout, volcanic winter | mixed | mean-time-between, about 4.8 mean days |
| Disease | human and animal illnesses | bad | its own comp with its own interval, biome-sensitive |
| OrbitalVisitor | orbital trade ships | good / neutral | mean-time-between |
| AllyArrival | trade caravans, visitor groups | neutral-good | mean-time-between |
| AllyAssistance | friendly help raids | good | mean-time-between |
| ShipChunkDrop | ship chunks falling on the map | neutral-good | its own low-weight category |
| GiveQuest | quest offers | neutral, opt-in | a quest comp on a fixed interval |

Two axes matter more than the names. **Spawn-based versus condition-based:** raids, pods and
migrations spawn things; eclipse, solar flare, toxic fallout, volcanic winter, heat wave and cold
snap install a timed map-wide *condition* that ticks and expires and has no entities at all.
**Target type:** an incident declares whether it targets a map, the world or a caravan.

Cadences quoted on the wiki: psychic drone about a 15-day minimum refire, 0.75–1.75 days long;
flashstorm about 15-day; toxic fallout about 90-day, 2.5–10.5 days long; meteorite roughly annual;
manhunter pack lasts 24–54 h and is worth about 40 % more threat points than a raid.

### 2. The storyteller model

A storyteller is a difficulty curve plus a bag of independent *comps* (components). Each comp is a
small generator that, on each storyteller tick, decides whether to emit an incident of its category.
Comps run in parallel and are additive; that is the whole design idea.

| Comp archetype | Behaviour |
|---|---|
| Category mean-time-between | "fire something from category X about every N days"; Poisson-like, often gated by a minimum days passed and scaled by a curve over time |
| On/off cycle | "threat season": alternates on-days and off-days; in an on-phase fires 1–2 incidents of the category with a minimum spacing. The big-threat pacer |
| Random main | one roll on a short mean interval, then a category by weight. No cycle, no cooldown |
| Single mean-time-between | one specific incident on its own clock |
| Disease | its own interval, a disease scaled by biome and season, infects a fraction of the colony |
| Faction interaction | caravans, visitors, traders, tied to goodwill |
| Random quest | quest *offers* on a fixed interval |
| Journey offer | the late-game "you can leave" beat, gated on progress |
| Triggered | fires as a consequence of a player action (deep drilling), not on a clock |

| | Cassandra | Phoebe | Randy |
|---|---|---|---|
| Big-threat model | on/off cycle | on/off cycle | pure random |
| First threat cycle starts | day 11 | day 13 | — |
| On days / off days | 4.6 / 6.0 | 8 / 8 | — |
| Incidents per on-phase | 1–2 (about 50/50) | exactly 1 | — |
| Minimum spacing between big threats | 1.9 days | effectively 8–24 days apart | none |
| Misc mean interval | about 4.8 days | about 4.8 days | folded into the main roll |
| Main roll | — | — | about 1.35 mean days, checked every 1,000 ticks |
| Category weights | — | — | Misc 3.5, FactionArrival 2.4, ThreatBig 1.4, OrbitalVisitor 1.1, ThreatSmall 0.6, ShipChunkDrop 0.22 |
| Anti-drought rule | — | — | 13 quiet days force the next event to be a big threat |
| Points randomisation | none | none | × 0.5 to × 1.5 after everything else |

**Threat points.** `points = (wealthPoints + pawnPoints) × difficulty × startingFactor ×
adaptation`, clamped to 35–10,000. Wealth points are zero below about 14,000 storyteller wealth
(items + creatures + half of buildings), then about 161 wealth per point up to 400,000, to a hard
ceiling of 4,200 points at 1,000,000. Pawn points scale with wealth too: about 15 per colonist at
low wealth to about 200 above 400,000; slaves × 0.75, cryptosleep × 0.3, children by age, trained
attack animals about 8 % of combat power. The starting factor is 0.7 at day 10 rising to 1.0 by day
40. Adaptation runs 0.4–1.47, growing while the colony thrives and dropping sharply when colonists
are hurt or killed. Difficulty runs 0.10 to 2.20.

**Population intent** is a separate multiplier gating free-colonist events: a population curve
(about 8 at zero colonists, 2 at one, 0.35 at seven, about zero at eleven or more) times a
days-since-last-recruit curve (0 rising to 1 over eight days). Free colonists dry up around 11–13.

### 3. The incident definition, in plain words

*Identity and classification:* a name, a label, a category, a favourability flag (good / neutral /
bad) used for presentation and some storyteller logic, and a population-effect tag.

*Eligibility gates:* a base chance (a weight within the category, not a probability), minimum
refire days, earliest day, minimum threat points, minimum and maximum population, allowed biomes,
target tags, season or temperature windows, and whether the incident's intensity scales with the
points budget.

*Behaviour:* a worker class with two methods, and the split is the pattern worth taking:
`CanFireNow(parms)` is cheap and side-effect-free (is there a valid drop cell, a faction, is the
condition already active) and returning false just means the storyteller re-rolls;
`TryExecute(parms)` does the thing, spawns or installs, sends the letter, and returns whether it
happened. The parameters carry the target, the points budget and the faction, so the same worker
serves the storyteller, a quest and the debug menu.

*Letters:* the notification carries a kind that drives colour, sound, and whether the game
auto-pauses and jumps the camera: positive (blue), neutral (grey), negative (yellow), threat
small / big (orange to red; big threats force a pause and a jump). Letters carry look-targets, stack
until dismissed, and are archived. The History screen holds a Messages tab (the last about 200
letters and messages, pinnable), Graph tabs for wealth, population and mood over the last 30, 100
or 300 days, and a Statistics tab. The archive is what turns a stream of interruptions into a story.

### 4. Cargo pods

The resource-pod-crash incident drops one resource type in a random amount, drawn from a curated set
of storable goods to a modest value band largely independent of colony wealth: a pick-me-up, not a
scaling reward. It lands on a random valid cell, not aimed at the colony. It can crash through
roofs: the pods do no direct damage, but the roof collapse they cause damages what is under it.
Never under overhead mountain.

The animation is a *skyfaller*: a temporary entity spawns in the air above the target cell with a
shadow, descends over a short fixed tick window, then despawns and replaces itself with its
contents. The casing leaves slag chunks to haul and smelt, which turns an instantaneous gift into a
small hauling job. Siblings on the same machinery: ship chunk drop (inert wreckage to
deconstruct), transport pod crash (a pawn: an injured survivor, a refugee, sometimes a hostile),
crashed ship part (a hostile skyfaller installing a growing map-wide condition, guarded by dormant
mechanoids), meteorite (a mineral chunk to mine). Psychic drone is a pure condition, no skyfaller.

### 5. Quests versus incidents

Since 1.1 quests are a separate layer on top of incidents, not a kind of incident.

| | Incident | Quest |
|---|---|---|
| Trigger | a storyteller comp fires it; it is already happening | offered on a fixed interval (about 1 per 10 days; about 2 per 12 with the expansion) |
| Player agency | none | must be accepted (a few auto-accept) |
| Timers | an optional duration for conditions | an acceptance window, then a completion window that starts on acceptance |
| Scaling | points from the local map | points from total wealth across all colonies, × 1–3 stars, ± 30 % |
| Payload | a worker | a generated script: nodes over a shared slate emitting parts that listen for signals |
| Reward | implicit | explicit, chosen from a pool |
| Failure | not a concept | explicit success, failure and expiry, with goodwill consequences |

Quest parts *invoke incident workers*: incidents are the verbs, quests are a contract wrapper round
a sequence of verbs with a timer and a payout. Some quests are "accepting is the reward" (a
wanderer). The rule for our design: reward on completion means a quest; consequences with no
completion state mean an incident; a duration and a map-wide effect mean a condition.

### 6. The owner's cadence types, mapped

| Owner's term | Nearest mechanism | Verdict |
|---|---|---|
| Regular | category mean-time-between: a *mean*, sampled stochastically | good, if "regular" means "roughly every N days" |
| Weekly | **no equivalent.** Nothing fires on a calendar date; only growing seasons and temperature are calendar-linked | **mismatch.** A schedulable event lets the player prepare perfectly, which kills tension |
| Ad hoc | Randy's random-main model plus the anti-drought forcing rule | good; copy the anti-drought rule |
| Condition-based | two things: eligibility *gates* on every definition, and *triggered* comps that fire on a player action | good; keep the two apart |
| Periodic | the on/off cycle comp | good, and the single most valuable pacing idea |
| One-off | a single mean-time-between comp, or an earliest day plus a refire longer than the game | good |

The gaps in the proposal as stated: no points budget, no adaptation, no population intent, and no
distinction between an event that spawns and one that installs a timed condition.

## Recommendation

Copy the architecture, not the numbers. One storyteller object holding independent generators,
each with its own clock and category, all writing into one queue. Every event is a declarative
definition with its eligibility gates separated from a worker with the `CanFireNow` / `TryExecute`
split; that split alone buys cheap failure, safe re-rolling, and one worker for the storyteller,
a quest and the debug menu. Add a severity budget from a flattening wealth curve plus a per-colonist
term, with a start-of-game ramp and an adaptation factor, clamped both ends; add population intent
as a separate multiplier. Make the notification first-class: a kind (good / neutral / bad /
threat), a click-to-focus target, and an archive. Defer quests until incidents feel right, and
build them as a wrapper that calls the incident workers. Drop "weekly".

## Sources

- https://rimworldwiki.com/wiki/Events
- https://rimworldwiki.com/wiki/AI_Storytellers
- https://rimworldwiki.com/wiki/Cassandra_Classic
- https://rimworldwiki.com/wiki/Phoebe_Chillax
- https://rimworldwiki.com/wiki/Randy_Random
- https://rimworldwiki.com/wiki/Raid_points
- https://rimworldwiki.com/wiki/PopulationIntent
- https://rimworldwiki.com/wiki/Quests
- https://rimworldwiki.com/wiki/Modding_Tutorials/Quests
- https://rimworldwiki.com/wiki/User:Alistaire/Tag:IncidentDef
- https://rimworldwiki.com/wiki/Transport_pod
- https://rimworldwiki.com/wiki/Menus
- https://steamcommunity.com/app/294100/discussions/0/2217311444334859233/

## Confidence

| Question | Confidence |
|---|---|
| Taxonomy | medium-high: categories are well attested; a few assignments are inferred from the wiki's functional grouping |
| Storyteller model | high for the three storytellers' cycle numbers, Randy's weights and the points formula; medium for the comp taxonomy |
| Definition shape | medium-high; letter kinds and the History screen high |
| Cargo pods | medium: contents, roof behaviour, slag and the skyfaller are documented; value bands and tick counts are not |
| Quests versus incidents | high |
| Cadence mapping | high |

## Could not be determined

- The exact item pool and value band of a cargo pod.
- The skyfaller's descent in ticks, and whether the impact itself deals any damage beyond the roof collapse.
- Whether pods prefer cells near the colony; community reports suggest close to uniform over valid cells.
- Per-storyteller disease intervals, journey-offer gating, and the exact quest-comp parameters.
- The precise population at which free-colonist events cease (reports say 11–13).
- Whether the minimum-refire clock is per incident, per category, or both.

## Layer questions touched (brief §5)

**7. How does the storyteller use verticality?** Not answered by the reference, which is flat. The
supply drop is the first event and lands "through open sky" on the topmost walkable cell of its
column (a rooftop counts, a roofed room never does), which is the first verticality rule the
storyteller has. Tunnelling raids and breaches from the service stratum remain M6.
