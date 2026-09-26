# b — Sea voyages, and sites that arrive with a situation

**Lane B, prior art.** Asked 2026-09-26 for design 64 (expeditions). There are two linked questions:

- how games make an ocean crossing more than dead time;
- how they compose a destination out of authored and procedural parts, with a situation, a reward
  and choices.

The owner asked for *"big vast oceans"* and for *"authored areas … that pop up with a situation or
a random place to go and explore"*.

**Clean room:** mechanics, data shapes and design intent only. The data shape proposed at the end
uses invented names.

**Access.** One capped pass of 10 searches. Lines marked **[recall]** are hypotheses.

## Findings A (sea voyages)

- **The resource that makes distance matter.** Most of these games use two or three meters that drain at different rates. Sunless Sea has fuel, supplies and a Terror meter that runs 0-100 and rises as you sail; hitting 100 is a crisis state. Sailing faster burns more fuel but less food, so speed itself is a choice. A community guide recommends buying fuel and supplies at about 3:2 before a trip. FTL charges one fuel per jump between beacons, and every jump (or wait) moves a pursuing enemy fleet forward. That fleet takes over beacons, which turns running out of time into a second cost of distance. Dredge's panic builds at night whenever you are away from docks or lights, and time only passes while you move or act, so the cost is tied to time rather than distance. RimWorld Odyssey's gravship costs fuel per tile with a minimum per launch, so short hops are proportionally expensive.
- **Fog and discovery.** Sunless Sea keeps its map fogged and partly procedural: you uncover islands by sailing past them [recall: some tiles are reshuffled each new captain]. Dredge brings fog in at night, which shrinks what you can see and makes exploring feel tense. FTL shows the node graph but hides what each beacon holds until you arrive [recall: sensors reveal more]. Civilization blocks ocean crossing until you research embarking or ships, and the coast gets revealed step by step [recall].
- **Event pacing.** FTL gives about one event per jump across a sector of 19-24 beacons, so a few hand-picked stops fill each leg. Sunless Sea's encounters at sea are sparse, and most of the content is text events at ports and islands. Dredge splits the day into a calm half and a dangerous night half, and you choose when to be out.
- **Islands and ports as sites.** In Sunless Sea, ports are where the story happens (choose-your-own-adventure events), and the quests on one island need items or information from another. That cross-island dependency is what makes voyages matter. In Odyssey, landing on an ordinary tile starts a new colony. Landing on a quest site (bandit camp, work site, rescue) loads a temporary map you can settle once it is cleared. Orbit is a separate zone with different rules: vacuum, cold, airlocks.
- **What players find tedious.** Sunless Sea reviews repeatedly complain about slow, eventless sailing ("90% of the time waiting"), re-doing routes, and grinding money for fuel. The lesson: the travel itself needs a decision or a threat. Otherwise, shrink it (auto-travel, or FTL-style one-click jumps) and spend the time budget at the destinations.

## Findings B (sites that pop up with a situation)

- **RimWorld quests.** The generator runs a tree of script nodes. Each node reads and writes a shared key-value blackboard that holds pawns, the target map, threat points and similar values. Sites are built by picking "site parts" by tag and faction, so one site can combine, say, a hostile camp with a loot cache. From recall: rewards are chosen to fit a value budget derived from quest points and colony wealth, and the player often picks one of two or three reward bundles (items, faction goodwill or royal honor, or recruits). Offers expire if not accepted, and accepted sites have a timer and vanish or fail if ignored [recall]. Threat size scales with points.
- **Caves of Qud.** Every village is generated with its own faction, history, architecture, local resource, storytelling tradition, signature dish and skill, NPCs and quests. Much of that is derived from a generated world history (the sultans). Buildings such as huts, crypts and lairs use wave function collapse. Repetition is avoided because each site's flavour comes from world-history data rather than from the layout. [recall] Quests often send you to find a location or item, and the rewards include map secrets, reputation and items.
- **Wildermyth.** Events are authored scripts with named roles. Each role is a filter such as "a bookish hero" or "their lover", and the game casts real characters into those roles when the event fires. Writers treat this as casting actors for a play. Other target kinds include a map tile, the acting character itself, or an entity passed in from an earlier event. Content is authored once and feels fresh because the cast is different each time, and choices change character traits and relationships. Its inputs and outputs are documented.
- **Cataclysm DDA overmap specials.** A special is a group of map chunks with relative positions, a min-max count per overmap (or a percent chance if unique), and placement rules. It can link itself to the road, subway or sewer network. Each chunk points to a map generator, which can be fixed or have random variants.
- **Dwarf Fortress / Kenshi / No Man's Sky** [recall, not verified this session]. Dwarf Fortress sites come out of simulated history, so ruins record real events. Kenshi mixes hand-placed towns and ruins with dynamic squads, and some places change state (for example, towns overrun). No Man's Sky shows points of interest through scanners, signal boosters and planetary charts, which is discovery on request.
- **Discovery vs. offer.** Games use both. Found sites (Qud, Sunless, CDDA) reward exploring and stay put. Offered sites (RimWorld quests, No Man's Sky signals) come with a timer and a stated reward, which creates urgency and a trade-off against distance.
- **Reward types seen:** items and silver, recruits or prisoners, faction standing, research or knowledge (Qud secrets, No Man's Sky blueprints), map reveals, and a site that can be settled (Odyssey).

## A recommended data shape for a "site template"

- **SiteBlueprint** (authored once, reused many times):
  - `id`, `tags` (biome, coastal, hostile, faction types), `rarity`, `minDistance` or `maxDistance` from home, `uniquePerWorld` flag.
  - `pieces`: a list of **SiteFragment** references, each with a weight and a count range. A fragment is something like "raider camp", "sealed cache", "stranded survivor" or "anomaly". Fragments are what make combinations possible.
  - `roles`: named slots filled at spawn time, each with a filter. Examples: `captive` (a pawn from a faction with a relation below X), `holder` (a hostile faction near the tile), `boon` (an item category). This is the Wildermyth-style casting idea.
  - `mapRecipe`: a small real-map generator key plus its parameters (size, terrain from the planet tile, whether it has a coastline).
  - `budget`: a threat budget that scales with colony strength and distance, plus a reward budget equal to the threat budget times a multiplier.
- **SiteInstance** (made at spawn time): the tile, the resolved roles, the chosen fragments, the rolled threat and reward values, a `revealMode` (found or offered), an `offerExpiresAt` and a `siteExpiresAt`, and a `state` (hidden, known, active, resolved or abandoned).
- **RewardOptions**: two or three bundles, each spending the reward budget on a different kind of reward: goods, a recruit, a research or knowledge unlock, a map reveal (nearby hidden sites become known), faction standing, or a settle right. The player chooses one on accepting or on completing.
- **Situation hooks**: `onArrive`, `onCleared` and `onTimerLapse`, each pointing to authored text in which role names are substituted. Keep the choices short, and tag them so the same choice can't repeat within N days.
- **For sea travel later**:
  - **VoyageLeg**: from and to tiles, fuel and food cost, a `strain` meter (the equivalent of Terror), and 0-2 event rolls per leg drawn from an ocean event deck weighted by region.
  - **Islands** are coastal SiteBlueprints that are only discoverable from the sea, and some blueprints require an item obtained at another site (Sunless-style cross-dependency).

## Sources (URLs)

- https://steamcommunity.com/sharedfiles/filedetails/?id=1397427536
- https://sunlesssea.fandom.com/wiki/Basic_Strategies
- https://www.gamespot.com/reviews/sunless-sea-review/1900-6416086/
- https://community.failbettergames.com/t/im-rather-disappointed-by-this-game/13601
- https://ftl.fandom.com/wiki/Beacons
- https://ftl.fandom.com/wiki/Rebel_Fleet
- https://en.wikipedia.org/wiki/Dredge_(video_game)
- https://dredge.fandom.com/wiki/Panic
- https://www.gamedeveloper.com/production/leveraging-the-unseen-to-turn-players-worst-fears-against-them-in-dredge
- https://rimworldwiki.com/wiki/Modding_Tutorials/Quests
- https://rimworldwiki.com/wiki/Quests (fetch blocked by proxy)
- https://ludeon.com/blog/2025/06/odyssey-preview-2-gravships-and-space/
- https://rimworldwiki.com/wiki/Gravship
- https://colonysimgames.com/article/rimworld-gravship/
- https://media.gdcvault.com/gdc2019/presentations/Grinblat_Jason_End-to-End_Procedural_Generation.pdf
- https://wiki.cavesofqud.com/wiki/World_generation
- https://wildermyth.com/wiki/Modding_Guide
- https://wildermyth.com/wiki/Story_Inputs_and_Outputs
- https://docs.cataclysmdda.org/JSON/OVERMAP.html

## Confidence

- **High:** Sunless Sea's three meters and the complaints about slow sailing; FTL's fuel-per-jump cost and advancing fleet; Dredge's night panic and time that only passes when you act; Odyssey's fuel-per-tile cost and landing rules; RimWorld's script nodes, shared blackboard and tag-selected site parts; Wildermyth's role casting; CDDA's occurrence counts and network links; Qud's village fields.
- **Medium [recall]:** how RimWorld's reward budget is calculated and its bundle choice, offer expiry, Sunless Sea's partial map shuffle, and Civilization's embark research.
- **Low [recall]:** Dwarf Fortress, Kenshi, No Man's Sky, Pirates!, Windward and Raft. None of these were searched.

## Could not be determined

- RimWorld's exact reward-value formula and default expiry durations. The wiki fetch was blocked by the proxy.
- Event rates per leg in Sunless Sea or Sunless Skies.
- Anything verified about Sid Meier's Pirates!, Windward, Sea Salt, Raft, Kenshi, Dwarf Fortress or No Man's Sky. The 10-search limit was used up first.
- Whether Odyssey adds any events during a gravship flight itself; the sources describe only fuel cost and the orbit zone.

## Layer questions touched

None directly. A situation's board recipe inherits the tile's layer count (design 59 §5).
