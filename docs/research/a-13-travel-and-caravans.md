# a-13 — Travel and caravans in the reference

**Lane A, item 13, second file** (brief §5: research, factions, trade and world map). This is the
travel half; `a-13-world-generation.md` is the planet half. Asked 2026-09-26 for design 64
(expeditions), with the owner noting the reference's caravans as *"purely for reference and I don't
think it works well"*.

**Clean room:** this file records mechanics, numbers and design intent. No text, XML, code or names
from the reference are reproduced, and design 64 invents its own names.

**Access.** One capped pass of 9 searches. Page fetches to the reference's wiki and Steam were
refused by the proxy, as they were for a-13's first file, so the findings come from search
extracts. Lines marked **[recall]** fill a gap from memory and are hypotheses.

## Findings (numbered, with numbers wherever found)

**1. Caravans**
1. **Forming a caravan.** The formation dialog has three tabs: pawns/animals, items, and travel supplies [recall]. While you pick, it shows total mass against capacity, speed in tiles per day, days of food, forage rate and visibility [recall]. Each human or pack animal can carry body size × 35 kg. After you confirm, the chosen pawns gather at a formation spot, haul the items in, then walk off the map edge [recall].
2. **Speed.** The base is a flat 2500 ticks per tile, which is one in-game hour [recall for the "hour"].
   - The slowest member's move speed sets the pace [recall].
   - Load changes speed: an empty caravan moves at 200% and a fully loaded one at 50%, scaled by mass carried ÷ capacity.
   - Clothing, armour, traits and health also change speed.
   - A "caravan riding speed" stat lets riders on ridable animals go faster.
3. **Terrain, roads and season.** These act as extra path cost:
   - Flat ground adds 0. Small hills add more, and cost rises up to a maximum on mountainous tiles. Commonly cited values are 0 / 0.5 / 1.5 / 3 [recall].
   - Biome adds 0 for desert and plains, highest for tundra and boreal forest.
   - Season adds cost in winter and removes some in summer.
   - Every road type halves the cost (50%).
4. **Night rest and foraging.** By default a caravan stops to rest at night [recall]. While stopped it forages faster, members can socialise, and it is less likely to be attacked. Forage yield per pawn uses a stat that gains +0.09 per Plants skill level and is affected by manipulation and sight. People eat automatically. Grazing animals refill their hunger instantly for free if the tile has grass; otherwise they eat the caravan's food.
5. **Encounters on the way.** Visibility depends on the mass/capacity ratio and affects the chance of an ambush. Ambushes are raids or manhunter packs; there are also demand events and meetings with trader caravans. Each of these generates a small temporary map [recall].
6. **Arriving.** On arrival you can enter the tile's map, visit a settlement (trade or give gifts without a map), or attack it, which generates a map. Other options: split a caravan with a dialog, merge caravans that share a tile, and reform a caravan instantly at the edge of a temporary map [recall].
7. **The colony left behind.** The home map keeps running in full. Raids still target it [recall]. The game ends only when every colonist is dead, not when the home is empty [recall].

**2. Sites and temporary maps**
8. A quest site or encounter creates its map when the caravan enters. Map size follows the world's map-size setting or a smaller fixed size; the exact number was not found [recall].
9. A temporary map vanishes for good when its event timer runs out or the caravan reforms and leaves. The tile is then free for new events or settling. Quest and encounter maps leave nothing behind once finished. A manually made camp instead leaves an "abandoned campsite" marker that blocks that tile.
10. **Multiple maps.** Every open map is simulated at full detail at the same time [recall]. You switch maps through the colonist bar, which groups pawns by map (clicking one jumps there), or through the world view [recall]. Players ask for ways to delete finished quest maps to get FPS back.

**3. Quests**
11. Quests arrive in a quest tab where you accept or decline them. Unaccepted offers expire after some days, and accepted sites have their own timeout [recall for the exact timers]. You pick one of about three rewards; usually two are items and the third is goodwill/relations with the faction that gave the quest.
12. **Site types.**
   - Prisoner rescue: time-limited; you kill the defenders; the rescued pawn gets a +18 mood buff for 30 days.
   - Bandit camp: times out, weaker than a real faction base; destroying it gives +8 goodwill and 2000–3000 silver-equivalent.
   - Also item stashes, ancient complexes, and work sites (Odyssey).

**4. Other travel**
13. **Transport pods.** Each pod holds 150 kg. Fuel is 2.25 chemfuel per tile (rounded down), and the maximum range is 66 tiles at 150 chemfuel. On landing you can attack, visit, or drop into a map [recall].
14. **Shuttles.** Royalty adds quest shuttles and permit shuttles that carry pawns without fuel logistics [recall; no numbers].
15. **Odyssey gravship.**
   - Minimum build: a grav engine, one small thruster, one small chemfuel tank, a pilot console, and gravship substructure connecting them all.
   - Fuel: 10 chemfuel per tile, minimum 50 per launch. A fuel optimizer cuts 3 per tile, and at most two apply, bringing it down to 4 per tile; the 50 minimum stays.
   - Range: 10 tiles with the small engine, 16 with the large one.
   - Landing on an empty tile founds a new colony at once. Landing on a temporary quest site (rescue, bandit camp, work site) enters that map, and you can settle it after clearing it.
   - Only what sits on the substructure travels; the old map is left behind [recall]. There is an engine cooldown after launch [recall].
16. **Orbit.** Orbit is a separate layer you reach through "view planet" and "view orbit" buttons. It is made of real map tiles you can land on and build in. Outdoors there it is always 100% vacuum at −75 °C, so living there needs oxygen, sealed rooms and anchors.
17. **Landmarks and tile mutators.** There are 45+ landmark types placed from the world seed. Tile mutators are per-tile modifiers that change map generation (rivers, lakes, caves and so on) [partly recall].
18. **Fishing** exists in 1.6/Odyssey as fishing zones on water with a limited fish population [recall; no numbers found].

**5. Player criticism and mods**
19. **Complaints.**
   - Loading "takes forever". Pawns stuck in formation don't eat, sleep on the floor, skip the toilet, and animals collapse.
   - Colonists sometimes loiter at the formation spot and never leave.
   - You can't sort or filter pawns by stats in the dialog. The common workaround is to add everyone, then remove people before departure.
   - Mass and food have to be micromanaged; travel is slow; the home is exposed while the caravan is away; running many maps at once hurts performance [partly recall].
20. **Mods and what they fix.**
   - CaravanOptions (+Continued): global speed multiplier, per-road speed, a speed bonus for light caravans, forced night travel. It does not affect Vehicle Framework caravans.
   - Caravan Formation Improvements: choose the formation spot and the exit spot.
   - Caravan Item Selection Enhanced: custom item tabs and subcategories in the dialog.
   - Billy's Caravan Formation: improves formation.
   - Vehicle Framework / Vanilla Vehicles Expanded: vehicles as caravans [recall].
   - Performance mods for multiple maps, such as Performance Fish, RocketMan and Dubs Performance Analyzer [recall].

## Sources (URLs)
- https://rimworldwiki.com/wiki/Caravan
- https://rimworld.fandom.com/wiki/Caravans
- https://rimworldwiki.com/wiki/Caravan_Riding_Speed
- https://rimworldwiki.com/wiki/Carrying_Capacity
- https://rimworldwiki.com/wiki/Pack_animal
- https://rimworldwiki.com/wiki/Foraged_Food_Amount
- https://steamcommunity.com/app/294100/discussions/0/3058492678373896284
- https://steamcommunity.com/app/294100/discussions/0/2250056952644694531
- https://steamcommunity.com/app/294100/discussions/0/2250055685746433500/
- https://steamcommunity.com/app/294100/discussions/0/1743358239844281896/
- https://rimworldwiki.com/wiki/Camping
- https://rimworldwiki.com/wiki/Quests
- https://rimworldwiki.com/wiki/Transport_pod
- https://rimworldwiki.com/wiki/Pod_launcher
- https://rimworldwiki.com/wiki/Gravship
- https://rimworldwiki.com/wiki/Fuel_optimizer
- https://hardcoregamer.com/rimworld-odyssey-beginners-guide/
- https://rimworldwiki.com/wiki/Orbit
- https://gamerblurb.com/articles/rimworld-odyssey-orbit-explained
- https://rimworldwiki.com/wiki/Landmarks
- https://www.destructoid.com/rimworld-odyssey-landmarks-explained/
- https://steamcommunity.com/sharedfiles/filedetails/?id=1501729394
- https://steamcommunity.com/sharedfiles/filedetails/?id=3697920307
- https://steamcommunity.com/workshop/filedetails/?id=2927335733
- https://steamcommunity.com/workshop/filedetails/?id=2854310627
- https://github.com/jopejope/BillysCaravanFormation

## Confidence (high/medium/low, per section)
- **1. Caravans:** medium-high for 2500 ticks per tile, the ×35 capacity, the 200%/50% mass curve and the 50% road cost. Low for the exact terrain and winter values and the night-rest hours.
- **2. Sites and temporary maps:** medium for how temporary maps are removed. Low for map sizes and the map-switch UI.
- **3. Quests:** medium for rewards and the bandit camp and prisoner numbers. Low for expiry timers.
- **4. Other travel:** high for the pod and gravship fuel and range numbers and for orbit conditions. Low for shuttles, fishing and the cooldown.
- **5. Criticism and mods:** medium. The complaints and the mods are sourced; the performance-mod list is recall.

## Could not be determined
Within the 9-search cap I could not confirm:
- Exact hill, mountain, biome and winter path-cost numbers.
- Night-rest hours.
- Ambush chance formula.
- Site and encounter map sizes.
- Quest offer and site expiry durations.
- Gravship cooldown length.
- Fishing yields.
- Shuttle capacity and range.
- Any hard benchmark numbers for how much performance multiple maps cost.

## What this means for us (design 64)

- **Keep:**
  - Real, simulated destination boards.
  - Home running in full.
  - A flat per-tile time with terrain multipliers.
  - Temporary boards that vanish on leaving.
- **Drop:**
  - The mass budget.
  - The formation spot.
  - The three-tab formation dialog.
  - The item-by-item food maths.

  Those four are what players and mods complain about and route around.
- **Improve:** switching between boards and alerts from the board you are not watching, which the
  reference leaves to the colonist bar.

## Layer questions touched

- **Q (vertical layers):** none directly. A site board is an ordinary board with its own layer
  count from its tile's hill band (design 59 §5).
