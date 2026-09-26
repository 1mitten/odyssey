# World generation — owner interview

**Phase:** Interview (feature-level, in the shape of `home-area-interview.md`).
**Date:** 2026-09-26. **Branch:** `claude/sharp-euler-a6xtci`. This file and the documents it
names were written before any code; no code was written under it.
**Conducted by:** Claude Code. Eleven questions in three rounds, asked after a read-only look at the
board generator, the climate and weather Defs, the setup page, the save header and the brief.

The owner's brief: *"We also need to introduce world generation (this could be a large seam) but we
can keep it simple for now with seams. https://rimworldwiki.com/wiki/World_generation — how can we
introduce this to Odyssey. Explore, plan and investigate what will work and ask me all the questions
to clarify."*

**Read next:**
- `docs/design/57-world-generation.md`, the design these answers decide
- `docs/research/a-13-world-generation.md`, how the reference does it
- `docs/reference/mockups/world-map-brief.md`, the Claude Design prompt
- `docs/plans/world-generation.md`, the units

## 1. What the exploration found, put to the owner before the first question

- **There is no world layer.** A board is built from one seed, a size, a map type and two flags.
  The path is `ColonyWorld.DefFor`, then `MapGenerator`, then the eleven passes of
  `NaturalMapGenerator`.
  - No biome, tile, planet, settlement or region type exists anywhere in the code.
  - `Faction` is a three-value enum on pawn kinds.
- **The seam is mostly already there.** Nearly everything a world tile would set is already a field
  on `NaturalMapGenDef`:
  - relief, which is the hills
  - tree and bush density
  - sand and gravel thresholds
  - rivers, ponds and marsh
  - outcrops, ore and caverns
  - the wildlife table

  These are set in code; none of them is XML yet.
- **Climate and weather are global.** One `ClimateDef` (`Climate_Temperate`) is hard-wired through
  `WorldContent.Climate`, although its own comment says "one Def per map type". The weather's
  season weights are one table.
- **The setup page chooses** a colony name, a seed, a size and three colonists. It does not choose a
  site, a biome or a board type (design 19 §2.8).
- **The save header already carries the recipe for rebuilding the board** (`SaveRecipe`). A site
  would go there too, as a format bump.
- **The names are waiting.** `proper-nouns.csv` proposes the planet Carrow (a city-world), the city
  Ashgate, and the factions Reclaimers, the Tithe, the Cartage and the Kindred.
- **The brief put the world map in M7**, "scope lightly". This pulls it forward.
- **Alternatives in the genre:**
  - The reference uses a globe of tiles: elevation, then temperature from latitude, then rainfall,
    biome, hilliness, rivers, roads and factions. You click a start tile.
  - Oxygen Not Included picks a preset world with traits and has no map.
  - Dwarf Fortress has you choose a rectangle on a generated map.
  - Stonehearth has a small regional map you land on.
  - Kenshi and Going Medieval have no overworld.

## 2. The answers

| # | Question | Answer |
|---|---|---|
| 1 | How far this first piece goes before a playtest | **Seam and map screen** (recommended): W1 a tile record and biome Def drive the board; W2 a seeded planet generated headless; W3 a World screen where a start site is clicked. Factions, rivers and roads stay recorded seams. |
| 2 | What a world tile is, given a city-world lore and a meadow board | **A climate biome plus a ruin density** (recommended): two axes. Ruin density is recorded and does nothing until the city board is played. |
| 3 | The map's shape | **A flat hex grid wrapping east–west** (recommended), about 64 × 32, latitude north–south. |
| 4 | Biome art | **"There will be another pack later but 1 for now."** Only the meadow is dressed; other biomes wait for the pack. |
| 5 | What the other biomes do on the map | **Shown, not settleable** (recommended): the map shows the planet's real biomes; only meadow tiles can be picked; the rest say *not yet available*. One flag per biome turns one on when its art arrives. |
| 6 | What a tile drives on the board in this cut | **Climate and seasons, and hilliness.** Water (river, coast) and rainfall-driven vegetation and wildlife were **not** chosen. |
| 7 | What the player sets when a world is made | **The seed only** (recommended). The site derives the board's own seed. |
| 8 | Where the map sits in the new-game flow | **Its own screen before the setup page** (recommended): New game → World → setup page → Start; Back returns to the map. |
| 9 | Mountainous sites and the 16-layer board | **Mountainous adds layers** (recommended): flat to hilly keep 16; a mountainous site goes to 24 so rock survives under the valleys (design 38 §13, 28 §11). |
| 10 | How the map should look | **A Claude Design brief first** (recommended), as for the Animals, Assign and Health tabs; the build follows the mockup. |
| 11 | Faction settlements on the map | **Not yet, a seam only** (recommended): an empty settlement list; the factions arrive with M7 and raids still walk on from the board's edge. |

## 3. What the answers settle, and what they leave to the design

**Settled:**
- The unit order: seam, then planet, then screen.
- A tile has two axes.
- One biome can be settled.
- The tile drives climate and hills only.
- The seed is the only input.
- The flow gains one screen.
- Mountains are deeper.
- Claude Design draws the screen.
- There are no factions yet.

**Left to design 57, and put to the owner there as proposals:**
- **The biome and terrain names.** They are proposed for veto in the registry, as the occupations
  were (design 19 §6).
- **How latitude becomes a season's swing**, and where the meadow's reference latitude sits so that
  one site reproduces today's climate exactly.
- **What the save stores.** The design stores the tile's own fields beside the world seed, so a later
  retune of the planet generator cannot change a saved colony's board.
- **Which site the World screen opens on.** A suggested site derived from the seed, so the old quick
  path (New game, Next, Start) is still three presses.
