# a-13 — World generation in the reference (and the genre)

**Lane A, item 13** (brief §5: research, factions, trade and world map), world generation only;
factions and trade are later files under the same number. Asked 2026-09-26 for design 59.
**Clean room:** this file records mechanics, data shapes, numbers and design intent. No XML, code,
names or flavour text from the reference are reproduced, and design 59 invents its own names.

**Access, stated first because it bounds everything below.** Two capped research passes (8 and 6
tool calls) were run. **Every page fetch was refused by the proxy**: rimworldwiki.com,
rimworld.fandom.com, steamcommunity.com, web.archive.org and koth-87.github.io. Web search worked,
so the findings come from search-result extracts. Where a line fills a gap from recall rather than
an extract it says **[recall]**. Treat those lines as hypotheses. The owner's own copy of the game
can confirm them by measuring.

## Findings

### 1. What the player sets

- **A text seed.**
- **Planet coverage** (extract): three choices, roughly 30, 50 or 100 per cent. A lower value gives
  more isolated, island-like land.
- **Overall rainfall and overall temperature** (extract): planet-wide shifts applied on top of the
  generated fields. [recall] About seven steps each, from very low to very high.
- [recall] **A population setting** scales how many settlements each faction gets, and later
  versions add pollution. A search found no extract confirming the population setting.
- **Deferred to site selection:** the map size and the starting season.

### 2. The order of generation [recall; the extracts confirm only the land → rivers → settlements → roads order]

1. The tile mesh: a geodesic sphere, clipped to the coverage.
2. Per-tile fields from noise:
   - elevation, with a sea level
   - hilliness
   - temperature, from latitude and elevation
   - rainfall
   - swampiness
   - then the biome
3. Lakes, then rivers flowing downhill from high, wet sources, growing as they gather flow.
4. Factions and their settlements on habitable tiles.
5. Roads between settlements, ancient roads first.
6. Named features (ranges, seas, bays), then special sites.

### 3. What a tile stores

- **Confirmed by extract:** elevation, average temperature and rainfall (mm a year).
- **[recall]:**
  - hilliness, swampiness and the biome
  - river and road links to neighbours, each with a size or kind
  - a feature reference
  - rock types
- **Latitude is derived from position, not stored**, and it sets both the temperature range and the
  day length (extract).

### 4. Hilliness, and what it does to the local map

- **Five bands** (extract): flat, small hills, large hills, mountainous, impassable.
- **Flat** is the most common land: much building space and little stone (extract).
- **Mountainous** gives rock cover and natural chokepoints (extract).
- **Caves** appear on half of mountainous tiles and a quarter of large-hill tiles (extract).
- **Share of the map that is mountain, per band [recall]:** flat about none, small hills 5–15 %,
  large hills 20–35 %, mountainous 40–60 %, usually around a roofed rock core. The band scales the
  local elevation noise, and cells above a cut-off become rock.
- **Impassable cannot be settled** [recall].

### 5. Temperature and seasons

- **Base temperature follows a latitude curve**, hot at the equator and cold at the poles. It is
  lowered by altitude at about **6.5 °C per 1,000 m** (extract, from a mod guide describing
  vanilla), then shifted by the overall setting.
- **Near the equator the seasons barely differ**, "permanent summer" (extract). Outdoor temperature
  varies with latitude, time of day, day of year and biome (extract).
- **[recall]** The seasonal swing grows with absolute latitude: a few degrees at the equator, tens
  of degrees near the poles. The daily swing is a separate, smaller sine. The hemisphere decides
  which half of the year is summer.

### 6. How a biome is chosen

- **By scoring, not by a lookup table** (medium–high: an extract of the modding tutorial shows a
  per-biome worker whose score reads water coverage and temperature thresholds).
- **[recall]** Every biome scores every tile and the highest wins; an inapplicable biome returns a
  large negative score. Sea and lake are scored like any other biome. The extremes (ice) come from
  very low temperature.
- The standard world uses about a dozen base biomes, and all of them appear on a normal world
  (extract).

### 7. Where a colony may start

- The player clicks any settleable tile, and a panel shows its fields.
- A random-site button tends to pick temperate tiles.
- **You cannot land directly beside a faction's base** (extract). About five tiles or more avoids
  a crowding penalty with that faction (extract).
- **[recall]** Water tiles and impassable tiles cannot be settled, and some biomes are flagged
  unsettleable.
- The map size and the starting season are chosen here.

### 8. How the tile drives the local map

- **Biome** [recall] sets:
  - the ground types and where they fall, through a fertility noise
  - which wild plants grow, and how densely
  - which animals appear, and how densely
  - the weather frequencies
  - the diseases
- **Hilliness** sets how much of the map is rock (§4). Caves appear only in the rock.
- **Latitude and temperature** set the yearly curve and its daily swing, and so the growing window.
- **River, road and coast** carve a river, a paved strip or a sea edge into the map (extract names
  mountain, caves, coast and river as the base-game map features).

## Alternatives in the genre

| Game | Shape | Note |
|---|---|---|
| Dwarf Fortress | a full world with simulated history; the player chooses a rectangle of chosen size | a site finder filters by elevation, water, savagery and similar as soft preferences (dwarffortresswiki, read) |
| Oxygen Not Included | a preset world (asteroid) with random traits; no map | [recall] the cheapest shape that still gives choice; fits a sci-fi setting |
| Stonehearth | a generated regional map, a few biome options, click a landing spot | medium-low confidence |
| Kenshi | one hand-built persistent world, no procedural overworld | [recall] |
| Going Medieval | a map type and a seed; no overworld | low confidence |

## What this means for Odyssey (feeds design 59)

- **The minimum that gives the most:**
  - a seeded planet grid
  - three fields (elevation with a sea level, temperature, rainfall)
  - a biome picked from the fields
  - hilliness bands
  - a start picker that shows the tile's fields and refuses water and sheer ground
  - a board generator that reads the tile's climate and hills

  Rivers, roads, factions, coverage and the sliders are later and optional.
- **A flat hex grid in place of a sphere** keeps every neighbour equidistant and draws as one
  texture. Wrapping east–west gives longitude without the poles' distortion problem of a globe.
- **The reference scores biomes. A band table with priorities is recommended here instead**, as the
  simpler first form:
  - it can be read in XML and gives the same answer on every runtime
  - scoring can replace it later without changing the tile record, because the biome is a result,
    not an input
- **The seasonal swing should grow with absolute latitude.** The one number to fix is the latitude
  at which today's temperate curve is reproduced exactly, so a default site changes nothing.

## Sources

**Read in full:**
- https://dwarffortresswiki.org/index.php/Site_finder
- https://dwarffortresswiki.org/index.php/Embark

**Search extracts only (page fetch refused):**
- https://rimworldwiki.com/wiki/World_generation
- https://rimworldwiki.com/wiki/Biomes
- https://www.rimworldwiki.com/wiki/Modding_Tutorials/Biomes
- https://rimworldwiki.com/wiki/Temperature
- https://rimworldwiki.com/wiki/Landmarks
- https://rimworldaccess.com/world/world-map/
- https://koth-87.github.io/RP2-Guide/ (a mod's guide; vanilla behaviour inferred)
- https://steamcommunity.com/sharedfiles/filedetails/?id=878424364
- https://steamcommunity.com/app/294100/discussions/0/361798516965345581/
- https://en.wikipedia.org/wiki/Going_Medieval

**An unread lead** (it may carry the temperature-by-latitude numbers):
https://neitsa.github.io/games/rimworld/preparelanding/temperature_tab.html

## Confidence

| Finding | Confidence |
|---|---|
| Player inputs (seed, coverage, rainfall and temperature shifts) | medium |
| Step counts, population, pollution | low |
| Order of generation | low (medium for land → rivers → settlements → roads) |
| Tile fields | medium |
| Five hilliness bands, cave odds | medium |
| Mountain share per band | low |
| Latitude curve and lapse rate | medium |
| Size of the seasonal swing | low |
| Biome by scoring | medium–high |
| Settlement spacing, unsettleable tiles | low–medium |
| **Overall** | **medium** |

## Could not be determined

- Noise parameters and elevation cut-offs per hilliness band.
- The share of mountain on the local map per band.
- Seasonal amplitude as a function of latitude, in degrees.
- Each biome's score formula.
- The minimum settlement distance, and settlements per faction.
- Whether the population setting exists, and its steps.
- The exact list of unsettleable biomes.

None of these blocks design 59. Every number it needs is **ours to tune and measure**, and none is
copied.
