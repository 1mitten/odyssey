# a-13: Factions and goodwill

**Question.** How do factions work in the reference?
- What categories of faction are there?
- What range does goodwill cover, where are its thresholds, and what moves it?
- What does each relation unlock?
- How do factions relate to each other and to the world map?
- How are a faction's pawns generated, and how does the storyteller use factions?
- What is the design intent?
- And how do other colony and strategy games solve the same problem?

**Asked** 2026-09-26 for the factions line (`docs/design/61-factions.md`, interview
`factions-interview.md`). This is item 13 of brief §5 Lane A ("research, factions, trade and world
map … scope lightly; late roadmap"), which had never been run.

**Method.** One subagent, capped at six fetches or searches.
- **Every page fetch was refused by the proxy** (rimworldwiki.com and the fandom mirror), so the
  findings come from search excerpts and from general knowledge of the game.
- Each finding is tagged **[search]** (an excerpt backs it) or **[prior]** (general knowledge,
  unchecked this session).
- The alternatives in §11 are the coordinating session's own knowledge, tagged **[prior]**.

**Clean room.** Mechanics, numbers, data shapes and intent only. No Def XML, names or flavour text
was fetched or is reproduced.

## Findings

### 1. Categories of faction

- **The player's faction:** the colony, including recruited prisoners. **[prior]**
- **Permanent enemies.**
  - Goodwill is locked at −100, and gifts and gestures do nothing.
  - The reference's pirate-type faction and its fiercest tribes are here. **[search]**
- **Variable factions.**
  - Each has a *natural* goodwill band and starts somewhere in it.
  - The player can move goodwill away from the band, and it then drifts back, so a relation takes
    upkeep. The fiercer ones start hostile but can be brought round. **[search]**
- **Special factions.**
  - Machine and insect factions are hostile to all with no diplomacy.
  - A hidden faction owns the pawns in ancient ruins.
  - Expansions add factions with their own currencies of favour. **[prior]**
- **Tech level is per faction**, and it bounds its pawns' weapons and apparel. The primitive
  factions are cheap per fighter. **[search]**

### 2. Goodwill: range and thresholds

- The range is **−100 to +100**. **[search]**
- **The thresholds have hysteresis**, so a relation does not flicker at a boundary. **[search]**

| Transition | At |
|---|---|
| Neutral → hostile | goodwill ≤ **−75** |
| Hostile → neutral | goodwill ≥ **0** |
| Neutral → ally | goodwill ≥ **+75** |
| Ally → neutral | goodwill falls to about **0** (excerpts disagree on ≤ 0 or < 0) |

- **Drift towards the natural band.** One excerpt quotes 0.4 a day for a fierce faction, with
  bands such as −100…−80 for the fierce and about −50…+50 for the friendly.
  - Low confidence that this survives in current versions. It may have been reworked around
    version 1.3. **[search]**

### 3. What moves goodwill

| Event | Change | Tag |
|---|---|---|
| A gift | scales with value; one excerpt: about +1 per 160 of market value. Repeated gifts probably diminish | [search] / [prior] |
| Returning a wounded member alive (tended, walks off the map) | about **+16** | [search] |
| Releasing a prisoner | positive; amount unknown | [prior] |
| Arresting a visitor | **set to −75** at once, which is hostile | [search] |
| Harvesting a member's organs without a medical reason | **−20** | [search] |
| Friendly fire on an ally's helpers | negative, probably scaling with harm | [search] / [prior] |
| Killing members; a prisoner of theirs dying; attacking a settlement | negative; attacking a settlement makes it hostile | [prior] |
| Quest outcomes | a common reward and penalty | [prior] |
| **Spending:** calling military aid | **−25** | [search] |
| **Spending:** requesting a trade caravan | **−15**, with a cooldown | [search] |

### 4. What each relation unlocks

- **Hostile:** raids and sieges from that faction; its settlements can be attacked. **[prior]**
- **Neutral or better:**
  - its traders, visitors and travellers arrive;
  - its settlements trade with a visiting caravan.
  - Trading needs goodwill at or above about 0. **[search]**
- **Ally:**
  - military aid on request, arriving at once, for −25;
  - a trade caravan on request, for −15;
  - unprompted help during a raid. **[search]** / **[prior]**

### 5. Factions and each other

- Relations between non-player factions are **static**.
  - Permanent enemies are hostile to all.
  - Machines and insects are hostile to all humans.
- The only way the player sees these relations is **factions fighting when they meet on the
  colony's map**. There is no simulated war on the world map. **[prior]**

### 6. Settlements and the world map

- Settlements are placed at world generation.
- A caravan can visit one to trade, gift or attack.
- Destroying a faction's last settlement defeats the faction. **[prior]**

### 7. Leaders

- Each human faction has a leader pawn.
- Killing the leader costs goodwill, and a successor is named. **[prior]**

### 8. How a faction's pawns are generated

- The storyteller computes **threat points** from colony wealth, colonists, difficulty and time.
- The chosen faction **spends** those points on its pawn kinds by combat power: roughly 35 for the
  cheapest fighter, 95 for an elite gunner, 200 for a heavy machine. **[search]**
- A faction carries **separate group templates** (combat, trader, settlement defence, peaceful
  visitor), each a weighted list of kinds. **[prior]**

### 9. How the storyteller uses factions

- **A raid** picks a hostile faction able to field a group at the current points; the weaker
  factions cover the low end. **[search]** / **[prior]**
- **Traders and visitors** draw from non-hostile factions. **[prior]**

### 10. Design intent

- **Goodwill is a currency.**
  - It is earned by generosity and mercy: gifts, returning people alive, releasing prisoners.
  - It is spent on help: aid and caravans.
  - Drift makes it a relationship that needs upkeep rather than a switch.
- **Moral choices get a price:** organs, arresting visitors, careless fire.
- **Two kinds of other people:** a floor of permanent danger, and a diplomatic path the player can
  cultivate. (This paragraph is inference.)

### 11. Alternatives in other games [prior]

| Game | Shape | What to take | What to avoid |
|---|---|---|---|
| **Stellaris / Crusader Kings** | Opinion is the sum of **dated modifiers**, each with a reason and a decay, and it is shown itemised | **Legibility**: the player sees *why*. Worth taking as a saved list of the last few reasons beside one scalar | The full sum is costly to save and hash, and hard to tune; one scalar plus a list gives the reading without the model |
| **Kenshi** | A relation per faction; towns and patrols meet and **fight each other** in the open world | **Faction against faction on the colony's map**: two raids arriving together, or a trader's guards meeting bandits | A simulated world of patrols is a milestone of its own |
| **Dwarf Fortress** | Civilisations with sites, histories, positions and wars generated before play | Settlements named at world creation, so the world has a past | Legends-mode depth; not scoped |
| **Going Medieval** | Raids only; no diplomacy | Evidence that a layered colony sim ships without diplomacy, so a thin slice is honest | Nothing |
| **Frostpunk** | Factions *inside* the colony as politics | Out of scope: our factions are outside the walls | — |
| **Mount & Blade** | Relations per lord and per faction; tribute to end a war | The **tribute** idea, which is the owner's racket | Per-person relations with every leader |

## Recommendation

1. **Take the reference's scalar and its hysteresis:**
   - an integer from −100 to +100;
   - hostile at ≤ −75, neutral again at ≥ 0, ally at ≥ +75, an ally falling back at ≤ 0.
   - Integers keep it in the hash with no float.
2. **Add a short saved reasons list per faction** (the last few changes with day, amount and a
   reason key). This is the Stellaris legibility without its model.
3. **Drift once a day towards a natural band per faction.**
   - This is the lever that turns the owner's **protection racket** into a mechanic: a payment lifts
     the bandits' goodwill, and the drift wears it back down, so the collector comes again.
4. **Faction against faction relations are static, from Defs.** Show them only as fights on the
   colony's map, as the reference and Kenshi do.
5. **The world is a list of settlements** generated at creation (the owner's ruling), each with a
   faction and a distance in days. Travel is timers.
6. **Threat points stay design 55's headcount-and-days function** until a value per commodity
   exists. The collector needs that value anyway, so one table serves both (design 61 §6).

## Sources

- https://rimworldwiki.com/wiki/Factions (search excerpts only; the fetch was refused)
- https://rimworld.fandom.com/wiki/Faction_Relationships (excerpts only)
- https://rimworldwiki.com/wiki/Raid_points (excerpts)
- https://rimworldwiki.com/wiki/Trade (excerpts)
- https://github.com/cluder/RimworldMod_AdjustableTradeCosts (the −15 and −25 defaults)
- https://steamcommunity.com/app/294100/discussions/0/3802776599343459724/ (natural goodwill)
- https://steamcommunity.com/app/294100/discussions/0/600786083350057671/ (allying a hostile faction)
- https://steamcommunity.com/app/294100/discussions/0/4038103329147092717/ (rescue goodwill in 1.4)

## Confidence

| Section | Confidence |
|---|---|
| §1 categories | medium |
| §2 thresholds | **high** for ±100, −75, 0, +75 · low for drift in current versions |
| §3 changes | medium for −75 arrest, −20 organs, the gift rate · low for rescue and release amounts |
| §4 unlocks | **high** for −25 and −15 · medium otherwise |
| §5 faction against faction | low |
| §6 settlements | low–medium |
| §7 leaders | low |
| §8 generation | medium |
| §9 storyteller | medium |
| §10 intent | low (inferred) |
| §11 alternatives | medium (general knowledge) |

## Could not be determined

- The text of the Factions, Goodwill, Settlement, Trade and Visitors pages.
- The full faction list with its numbers.
- Whether natural drift still exists after version 1.3, and at what rates and bands.
- Exact amounts for:
  - releasing a prisoner, killing a member, a prisoner dying, killing a leader;
  - attacking or destroying a settlement;
  - how friendly fire scales, trading, quests.
- How gift value diminishes.
- Cooldowns on aid and caravan requests; whether a caravan request needs ally or only neutral.
- The exact rule for choosing a raiding faction, including each faction's minimum points.
- How faction-to-faction relations are set up.
- The expansion factions' mechanics.

**The cheapest way to close these:** re-run this question from a machine whose network allows the
wiki. The design's numbers are proposals the owner tunes, so none of these gaps blocks F0–F2.

## Layer questions

- **Arrival by stratum (layer question 7).** The reference's factions arrive at the map edge or by
  drop pod. In a layered world a faction can have a **home stratum**:
  - the street edge for the bandits;
  - the road for the traders;
  - **below** for a people of the buried city.
  Design 61 §3 records it. It makes digging down a diplomatic act as well as a building one.
- **A settlement's distance** is flat days; no layer is involved, because the world list is off
  the map.
- **A collector's tribute pile** is a cell, so it is layer-aware from its first commit. It is
  placed on a surface the collector can reach from its edge (`SurfaceCensus.Edge`, design 30), not
  on the slice the player is looking at.
