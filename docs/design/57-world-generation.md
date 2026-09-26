# 57 — World generation: a planet of sites that drive the colony board

**Status: approved 2026-09-26 (owner: *"happy to go"*, then *"ok go"*); Claude Design's specification received the same day (§9a). Being built.** Branch `claude/sharp-euler-a6xtci`.
Units WG1–WG3 are in `docs/plans/world-generation.md`.

**Read first:**
- `docs/research/world-generation-interview.md`: the owner's eleven answers
- `docs/research/a-13-world-generation.md`: how the reference and the genre do it
- `docs/reference/mockups/world-map-brief.md`: the Claude Design prompt
- design 19 (the setup page this screen goes in front of)
- design 28 §5 (the climate curve this derives from)
- design 28-map-size §11 and design 38 §13 (the measurement behind deeper mountains)

## 1. The request

Owner, 2026-09-26: *"We also need to introduce world generation (this could be a large seam) but
we can keep it simple for now with seams."*

A planet is generated from the seed. The player picks where on it the colony lands, and **what that
site is decides the board**: its climate and seasons, and how hilly it is. Everything else a world
could carry is a named seam: factions, rivers, roads, coast, rainfall-driven vegetation, travel.

## 2. The interview (owner, 2026-09-26)

The full table is in the interview file. In one line each:
- Seam, then a headless planet, then a World screen.
- A tile has a climate biome and a ruin density.
- A flat hex grid, wrapping east–west.
- **Only the meadow can be settled**, because it is the only biome with art. The others are shown
  and say *not yet available*.
- A tile drives **climate and seasons, and hilliness**, and nothing else yet.
- The seed is the only input.
- Its own screen before the setup page.
- **Mountainous sites are 24 layers deep.**
- A Claude Design brief comes first.
- No factions on the map yet.

## 3. The shape

```
World seed ──► PlanetGenerator ──► Planet (64 × 32 hex tiles, not saved, rebuilt from the seed)
                                        │ the player picks a tile
                                        ▼
                                   SiteTile (a handful of integers, saved in the header)
                                        │
             ┌──────────────────────────┼───────────────────────────┐
             ▼                          ▼                           ▼
   SiteSeed.For(world, tile)   SiteBoard: hills → relief,   SiteClimate: tile → ClimateDef,
   = the board's seed          outcrops, depth              rainfall → wet-weather weight
             └──────────────────────────┴───────────────────────────┘
                                        ▼
                    ColonyRequest { Seed, Site } ──► ColonyWorld.Build (unchanged below)
```

**The one rule that makes this a seam rather than a rewrite: a request with no site builds exactly
today's board, climate and weather.**
- The goldens, every test world, the debug scenes and every save written before format 11 take that
  path. None of them moves.
- It is asserted field for field, not assumed (§11).

## 4. The planet

### 4a. The grid

- **Hexes in offset rows (odd rows shifted half a hex right), pointy-top.**
- **64 wide by 32 tall, 2,048 tiles**, from `PlanetDef`.
- **The width must be even**, so the offset pattern survives the wrap.
- **Wraps east–west**: column −1 is column 63. North and south are edges, and the poles are the top
  and bottom rows.
- **Every tile has six neighbours**, except on the pole rows, which have four.
- **A tile's index is `row * width + column`.** It is the tile's identity, and it goes into the save
  and into the board's seed.
- **Latitude is derived from the row, never stored**, in signed per-mille of a quarter-turn:
  `LatitudePerMille(row) = 1000 − (2·row + 1) · 1000 / height`. Row 0 is +969 (the far north) and
  row 31 is −969. Displayed as degrees (`× 90 / 1000`), with N or S.

**Why not a globe:**
- A sphere of hexes needs twelve pentagons, picking by ray and a camera of its own.
- A flat wrapped map gives longitude and latitude, draws as **one texture in one draw call**, and
  picks with arithmetic the fast tier can test.
- The reference's globe is its look, not its mechanics.

### 4b. The passes

Every pass is **integer arithmetic on its own sub-stream**, `seed ^ purpose`, exactly as
`NaturalGenContext` derives its streams. So adding or retuning one pass never moves another. **No
floating point and no trigonometry.** A world must be the same tile for tile under Mono and CoreCLR,
because its tile decides a board.

| # | Pass | What it computes |
|---|---|---|
| 1 | **Elevation** | Fractal value noise on a **wrapped lattice**: the lattice coordinate is taken modulo `width / period`, so the east edge meets the west with no seam and no cylinder maths. Needs `width` divisible by every octave's period (16, 8, 4 at the defaults). **Sea level is cut by rank**: the lowest `oceanPerMille` of tiles are sea, ties broken by index, so every world has the same share of sea. Land elevation runs 0 to `peakM` (3,000 m); sea tiles carry a depth. |
| 2 | **Temperature** | `MeanTempC = equatorC − (equatorC − poleC) · lat² / 1,000,000 − lapseCPer1000m · max(0, ElevationM) / 1000 + noise(±tempNoiseC)`. Squared latitude keeps the tropics broad and the cold near the poles, as a real planet's is. |
| 3 | **Rainfall** | Noise from 0 to `rainMaxMm`, plus **latitude belts** (a wet equator, dry belts about 30°, wet about 60°, dry poles: four integer bands), plus a coast bonus falling off over three hexes from the sea (breadth-first distance). Clamped at 0. |
| 4 | **Hilliness** | Elevation blended with a ridge noise, then **cut by rank over land tiles** into five bands at fixed shares (§5). By rank rather than by threshold, so no seed can deal a world with no flat ground or no mountains. |
| 5 | **Biome** | The first `BiomeDef` in priority order whose bands contain the tile's temperature and rainfall. Sea is decided by pass 1, not by a band (§6). |
| 6 | **Ruin density** | Noise, 0–1000 per mille. **Recorded, drawn nowhere and read by nothing** until the city board is played (owner, question 2). |
| 7 | **Coastal** | Whether any neighbour is sea. Recorded for the coast seam; read by nothing. |

**The guarantee: every world has at least one settleable tile.** If one does not, it is rebuilt on
`seed ^ retry` up to eight times, and a fast-tier test holds the bound over 1,000 seeds.

**The planet is not saved.** It is rebuilt from the world seed whenever the World screen needs it.
The colony needs only its own tile, which the save carries whole (§8).

### 4c. `PlanetDef` (XML, `Defs/Core/World/Planet.xml`)

| Field | Default | Meaning |
|---|---|---|
| `width`, `height` | 64, 32 | tiles; width even and divisible by the coarsest noise period |
| `oceanPerMille` | 450 | share of tiles that are sea: less than a real planet's, so there is land to look at |
| `peakM` | 3000 | the highest land |
| `equatorC`, `poleC` | 2700, −2500 | mean temperature at the equator and the pole, before height and noise |
| `lapseCPer1000m` | 650 | 6.5 °C colder per 1,000 m (the reference's own figure, a-13 §5) |
| `tempNoiseC` | 300 | ±3 °C of local variation |
| `rainMaxMm` | 2400 | noise ceiling before belts and coast |
| `hillSharesPerMille` | 300, 300, 220, 130, 50 | Flat, Rolling, Hilly, Mountainous, Sheer, over land |

Every default is a proposal. Tuning them is a content change, and a fingerprint test pins them as
it pins the rest of the content.

## 5. Hilliness, and what it does to the board

| Band | Registry key | Settleable | `surfaceRelief` | Outcrops per 10,000 columns | Caverns per 10,000 | Board depth |
|---|---|---|---|---|---|---|
| **Flat** | `ui.hills.flat` | yes | 1 | 8 | 3 | the chosen size's |
| **Rolling** | `ui.hills.rolling` | yes | **2 (today's)** | **16 (today's)** | **3 (today's)** | the chosen size's |
| **Hilly** | `ui.hills.hilly` | yes | 3 | 24 | 4 | the chosen size's |
| **Mountainous** | `ui.hills.mountainous` | yes | 4 | 40 | 6 | **max(size, 24)** (owner, question 9) |
| **Sheer** | `ui.hills.sheer` | **no** | — | — | — | — |

- **Rolling is today's board**, so a Rolling meadow at the reference climate (§7) reproduces the
  played board's generator settings exactly.
- **Depth is decided in `OdysseyBootstrap.BuildSession`, the one owner of board size**, before the
  chunk grid and the render model are built from it. Two numbers for one board is the fault that
  standing rule exists for.
- The header already stores the size with its depth, so a mountainous save reloads at 24 without
  asking the site.
- **Owed measurements (WG1)**, in the terms of design 28-map-size, because a 24-layer Huge board is
  new:
  - memory: `BoardMemoryTests`, one arm per band
  - the tick edit cost
  - on the owner's machine, the frame (`FrameTimeTests`)

  The arithmetic says 240 × 240 × 24 is about 99 MB before the mirror. **Should that prove too
  much, capping a Huge mountainous site at 20 is the owner's call, not a quiet clamp.**
- **Whether Mountainous reads as mountains is a playtest question.** The board is terraces plus
  outcrops. The reference's mountain is a roofed rock mass, which our generator does not make.

## 6. Biomes

**Six, by the owner's ruling of 2026-09-26** on Claude Design's specification
(`docs/reference/mockups/world-screen-spec.md`). The first draft proposed ten; the specification
drew six with finished colour ramps, and the owner took the six.

`BiomeDef` is the **first map-generation content authored in XML** (`Defs/Core/World/Biomes.xml`),
registered in `WorldContent.Register` beside `ClimateDef`. Its fields:
- `defName`, and `label` as a registry key (`ui.biome.*`)
- `settleable`
- `water`, true for Ocean only
- `priority`
- `bands`, a list of half-open bands: `tempMinC` / `tempMaxC` and `rainMinMm` / `rainMaxMm`
- `rampFrom` / `rampTo`, the map ramp's two stops as hex (§9b)
- `ramp`: `Ocean`, `Land` or `Ice`, which rule sets the ramp's t
- `board`, which names the board preset. Only `Meadow` exists, and it means `MakeWooded()`.

Bands are in centi-degrees of mean temperature and millimetres of rain a year, **checked in
priority order**. The last band is the rest, so the table is total by construction.

| Priority | Key | Name | Band | Settleable now |
|---|---|---|---|---|
| — | `ui.biome.ocean` | Ocean | pass 1 (below sea level) | no, ever |
| 1 | `ui.biome.ice` | Ice | T < −1000, **on land or sea**: polar sea freezes and stays water | no |
| 2 | `ui.biome.coldsteppe` | Cold steppe | T < 300 | no |
| 3 | `ui.biome.marsh` | Marsh | rain ≥ 1500 | no |
| 4 | `ui.biome.dryscrub` | Dry scrub | rain < 500, **or** T ≥ 1700 | no |
| 5 | **`ui.biome.meadow`** | **Meadow** | **the rest: 300 ≤ T < 1700 and 500 ≤ rain < 1500** | **yes** |

- **Ice is checked before the sea is coloured.** A sea tile colder than −1000 is Ice: it is still
  water and still cannot be settled, and it takes the spec's sea-ice ramp.
- **Why a band table and not the reference's scoring** (a-13 §6):
  - it reads in XML
  - it is total: every land tile lands somewhere, and a test proves it
  - it is exact on every runtime

  Scoring can replace it later without touching the tile record, because the biome is a result, not
  an input.
- **Turning a biome on when its art pack arrives is one field** (`settleable`) and a `board` preset.
  Anything the preset needs beyond `NaturalMapGenDef`'s existing fields is that unit's design, not
  this one's.
- **Ruin density is not a biome.** It is the second axis, and it waits for the city board.

## 7. Climate from the site

`SiteClimate.For(SiteTile tile, ClimateDef baseClimate)` returns a **new** `ClimateDef`. It never
writes through the shared one, the same rule as the one that forbids a test writing through a Def.

| Field | Derived as |
|---|---|
| `annualMeanC` | the tile's `MeanTempC` |
| `monthlyOffsetC[i]` | `base[i] · Seasonality(|lat|) / 1000`, where `Seasonality = clamp(150 + |lat| · 850 / 590, 150, 1600)` |
| `dailyAmplitudeC` | `clamp(base · (2000 − RainfallMm) / 1000, 300, 900)`: dry air swings more between noon and night |
| `groundOneLayerDampingPerMille` | the base's, unchanged |

- **The reference latitude is 590 per mille (53°), with 900 centi-degrees mean and 1,000 mm of
  rain.** A tile with exactly those values yields `Climate_Temperate` field for field. Test:
  `TheReferenceSiteIsTodaysClimate`.
- **Across the planet, seasonality runs from ×0.15 at the equator to ×1.6 near the pole.**
- **The settleable band narrows that, and the arithmetic says by how much.**
  - A meadow's mean temperature is 3–17 °C.
  - At sea level that band sits between about 39° and 61° (438–679 per mille), which is seasons of
    ×0.78 to ×1.13.
  - Only a meadow high enough to be cool near the equator gets down towards ×0.15, and it is ±2–3 °C
    round its mean all year.
  - **So between two meadows the mean moves more than the swing does.** A 3 °C meadow's Candle
    averages about −16 °C against today's −8, while its seasons are barely wider.
  - The poles' hard cold is Icefield, which cannot be settled.
- **Weather.** `WeatherSystem` gains a `wetPerMille` multiplier on the season weights of every
  weather that rains (`Rain`, `Storm`), `clamp(RainfallMm, 400, 1600)`:
  - 1,000 mm is ×1, which is today.
  - The draw is already by weight sum (`WeatherSystem`, the season total), so no other weight needs
    to absorb the difference.
  - **No site means 1000.**
- **No hemisphere flip. The calendar's seasons are the planet's** (proposal, for veto).
  - Wash, Glare and Rime are named, their weather weights are keyed by season, and the almanac
    promises them.
  - A southern colony whose Glare was cold would contradict every one of those.
  - Lore: Carrow's seasons are orbital rather than axial, the same everywhere, with latitude
    deciding how hard they bite.
- **Wiring.** `ColonyComposition` builds `TemperatureSystem` and `WeatherSystem` from the request's
  climate and wet factor instead of from `WorldContent.Climate` and a bare `WorldContent.Weathers`.
  The composition takes them from `ColonyRequest`, and a request with no site hands over exactly the
  global ones.

## 8. Seeds and the save

- **The seed on the World screen is the world seed.** The board's seed is
  **`SiteSeed.For(worldSeed, tileIndex)`**, a 32-bit integer mix. It is the one owner of that
  derivation, and it is never 0 if `SeedEntry` refuses 0.
  - `ColonyRequest.Seed` stays "the one number the board comes from". It is simply derived now.
  - The same world seed and the same tile always give the same colony.
- **What changes for a player:** a seed typed into the old setup page named a board, and now it
  names a planet. **Saves are unaffected**, because they carry their board seed. Seeds shared as
  text before format 11 give a different board on the World screen, which is the right trade for a
  prototype and is said once in the journal.
- **`SaveRecipe` grows, and the format goes 10 → 11.** The header gains:
  - `WorldSeed` (uint)
  - `TileIndex` (int, **−1 = no world**)
  - **the tile's own fields**: biome `defName` (a string, so reordering the XML cannot change a
    save), hills, latitude, mean temperature, rainfall, elevation, ruin density and coastal
- **Why the tile is stored and not rebuilt:** a save regenerated from the world seed would change
  its colony's climate the first time anybody retuned `PlanetDef`. The header carries what the board
  was built from, which is the existing rule for barren and wooded (`SaveRecipe` §Barren).
- **A format-10 save reads as `TileIndex = −1`** and rebuilds exactly as it does today. Test:
  `AFormatTenSaveLoadsWithNoSite`.
- **Coordinate the bump on merge.** Other branches move the format too (ranged combat took 9 → 10),
  and the higher number goes to whichever lands second.
- **No `odyssey.world` section yet.** Nothing mutable exists at planet scale until factions (M7),
  and an empty section is a format promise with nothing behind it.

## 9. The flow

```
Title ─ New game ─► World ─ Next ─► Setup page (name, size, colonists) ─ Start ─► colony
                     ▲                    │
                     └────── Back ────────┘
```

- **World** is `MenuScreen.World`, between `Root` and `NewGame`. It holds:
  - the seed field (moved here from the setup page: `SeedField`, with the same Reroll)
  - the map
  - the selected site's stats
  - **Random site**
  - Back and Next
- **It opens on a suggested site**: the settleable tile nearest the reference climate, preferring
  Rolling, ties broken by index. It is derived from the seed, so two players with one seed see the
  same suggestion, and **New game → Next → Start is still three presses**.
- **Random site** picks another settleable tile. The choice is the interface's (a presentation-side
  random), because only the tile chosen reaches the simulation.
- **A tile that cannot be settled can be selected and read, but not taken.** Next is disabled, and
  the stats say why:
  - *Not yet available*, for a biome with no art
  - *Open water*
  - *Too steep to settle*, for Sheer
- **The setup page loses its seed row** and gains a read-only site line (biome, terrain and latitude,
  and the depth when it is 24). Back returns to the map **with the three candidates kept**. Hunting
  for a site must not cost you a colonist you liked, which is design 19 §2.7's own rule carried
  forward.
- **Escape backs out one level, and needs no new rung.** The start screen's existing `MenuBack`
  rung calls `MenuDirector.Back()` (`SettingsPresenter`), and `Back()` becomes hierarchical: from
  NewGame to World, and from World to Root. Before this it always went to Root.
- **Keyboard** (the specification's, §9c):
  - the arrow keys move the selection one hex at 1×, and pan when zoomed
  - `+` / `-` zoom and `0` fits
  - Enter is Next and R is Random site
- **Its look is Claude Design's** (§9a). The constraints that are not negotiable:
  - the map is **one texture painted when the seed changes**, one draw (P10)
  - every word goes through `Registry.Label`
  - every mark is a drawn path, never a character (the font rule)
  - no scrollbar

## 9a. The look: Claude Design's specification, and where the build departs from it

The specification is kept verbatim in `docs/reference/mockups/world-screen-spec.md`, and its
constants live in `Odyssey.Hud.WorldLayout`. **Departures:**

| The specification says | Built as | Why |
|---|---|---|
| Hill bands Flat, Small hills, Large hills, Mountainous, Impassable | **Flat, Rolling, Hilly, Mountainous, Sheer**; the bump is Hilly and the tall peak is Sheer | owner, 2026-09-26: the specification's names are the reference's own labels (clean room) |
| `colourLow` / `colourHigh` | **`rampFrom` (t = 0) / `rampTo` (t = 1)** | the specification's "high" and "low" name lightness on the land ramps and depth on the ocean's, so the same field meant opposite ends; from/to names the end of t and nothing else |
| texture about 1187 × 515 | **painted at 2× (2374 × 1030)**, with the geometry in 1× units | at 4× zoom a 1× texture is a blur; the hex outlines, marks and labels are drawn at 1× units either way. Paint time is §9d's number. |
| the sea labels in italic | **upright** | neither shipped face has an italic cut; the tracking and ink are kept, as the specification itself allows |
| the land labels' 3 px white halo | a **1 px outline** (UI Toolkit's `-unity-text-outline`) | the specification's own fallback, since UI Toolkit has no text shadow blur |
| "drawn with `SvgPath`" (selection and hover) | drawn with `Painter2D` in the map overlay's `generateVisualContent`, from `WorldMapGeometry.Outline` | the outline is a hexagon computed per tile, not a fixed path string; the dashed ring uses `DashedOutline`'s hand-dashed loop, since `SvgPath` has no dashes |

## 9b. The map colours

`BiomeDef.rampFrom` is the colour at t = 0 and `rampTo` at t = 1:

| Biome | `rampFrom` | `rampTo` | t |
|---|---|---|---|
| Ocean | `#0b1d2c` (deep) | `#2f6f8c` (coast) | `(e / sea)^2.2` |
| Meadow | `#a3cf72` (lowland) | `#4f8f45` (upland) | `landHeight × 1.1 + jitter` |
| Cold steppe | `#cdc79c` | `#8c8766` | as Meadow |
| Dry scrub | `#e0b577` | `#a8743f` | as Meadow |
| Marsh | `#5c9c8c` | `#2c6660` | as Meadow |
| Ice | `#b9c9d2` | `#f4f8fa` | on sea, `0.6 + (e / sea) × 0.4`; on land, as Meadow |

- `e` is the elevation noise over its range, `sea` the sea level on the same scale, and
  `landHeight = (e − sea) / (1 − sea)`.
- The jitter is `(hash(col, row, 7) − 0.5) × 0.12`, from the tile, so a repaint is identical.
- The painter is presentation, so it may use floating point: nothing it computes reaches a cell, a
  save or the hash.

## 9c. Region names, zoom and pan (owner, 2026-09-26: built now)

**Region names** are **drawn only**. They are regenerated from the world seed every time, never
saved and never in the hash.
- Land regions are connected land over the six neighbours, with the wrap. The seven largest of at
  least 14 tiles get a label at their centroid, using a circular mean for x.
- Up to five sea labels go in the largest ocean, on tiles whose six neighbours are all ocean, away
  from the four polar rows, at least 230 px apart at 1×.
- A name is `S1 + S2 + S3` from the syllable tables, placed in a frame. The tables are code in
  `RegionNames`.
  - **`proper-nouns.csv`'s `world.regionnames` row lists the same three tables**, and
    `RegionNamesTests` fails if the two differ. That is one list with a guard, not two sources.
  - The frames (*Reach*, *The … Downs*, *Hold*, *Greater*, *Sea of*, *Deep*, *The … Shelf*,
    *Sound*, *Gulf of*) are registry labels, `ui.world.region.*` with a `{name}` placeholder.
  - The bare word and the *-ia* form are not frames.

**Zoom and pan** are `WorldMapView`, engine-free:
- zoom 1× to 4× in steps of ×1.5, eased over 180 ms
- centred with no pan at 1×
- y clamped, and x wrapping when zoomed in
- `ScreenToTile` composes the fit, the zoom, the pan and the wrap, and is tested through all four

The page draws three copies of the one texture side by side, so a pan across the seam shows the
planet continuing.

## 9d. Measurements

Taken as each unit lands, with the machine and date.

| What | Number |
|---|---|
| planet generation (64 × 32) | **2.1 ms median, 2.45 worst**, over 20 seeds (fast tier, CoreCLR, the build container, 2026-09-26): a twenty-fifth of the 50 ms budget. Two sample planets hold 13–17 % Meadow, 19–21 % Ice and 37 % Ocean, with a dry-scrub belt round the equator, since the six biomes have no tropical forest. |
| map paint at 2× | *WG3a* |
| board memory per hill band | *WG4* |

## 10. Seams recorded, not built

| Seam | Where it will attach |
|---|---|
| Faction settlements (the Tithe, the Cartage, the Kindred) | `Planet.Settlements`, an empty list now; placement with spacing and an `odyssey.world` section in M7; the start rule "not beside a settlement" joins `CanSettle` |
| Rivers and roads on the planet | two more passes after hilliness; a river tile would set the board's `riverChancePerMille` to 1000 |
| Coast on the board | `SiteTile.Coastal` is already recorded; a sea edge is new generator work |
| Rainfall → vegetation and wildlife | `NaturalMapGenDef` tree, bush and wildlife densities from `RainfallMm` (the owner left it out of this cut) |
| More biomes settleable | `BiomeDef.settleable` and a `board` preset, when the second art pack lands |
| Ruin density → the city board | `SiteTile.RuinPerMille` is already recorded |
| Temperature and rainfall shifts, planet size | `PlanetDef` fields are already there; the sliders are interface |
| A World tab in play (F-key), travel, caravans | M7 |
| The load list naming the site | the header already carries it |

## 11. How it is tested

Every test comes **first**, and every test has a **negative control**.

- **The seam (WG1):**
  - `DefFor` with no site equals today's def field for field (a fingerprint over every
    `NaturalMapGenDef` field).
  - A Rolling meadow site equals it too.
  - Each band orders relief and outcrops.
  - Mountainous gives 24 layers and has rock under its lowest column; the negative control is the
    same board at 16, which does not.
  - `TheReferenceSiteIsTodaysClimate`.
  - Equator seasonality is less than the pole's.
  - No site gives the global climate and a wet factor of 1000.
  - `SiteSeed` is stable, and different tiles give different seeds.
  - Format-11 round trip, and a format-10 load.
- **The goldens run and do not move.** If one moves, that is a fault to find, not a re-bake.
- **The planet (WG2):**
  - determinism (the same seed gives the same tile hash)
  - wrap continuity (the east neighbour of column 63 is column 0)
  - mean temperature falls with |latitude|
  - the sea share is exact
  - the hill shares are exact
  - every land tile has a biome (the table is total)
  - every biome appears across 200 seeds
  - the settleable guarantee holds over 1,000 seeds
  - generation is **timed, and the number recorded here**, against a 50 ms budget
- **The screen (WG3):**
  - `WorldChoiceTests` (select, `CanSettle` reasons, the suggested site, Next disabled on an
    unsettleable tile, candidates kept across Back)
  - hex picking from a pixel, including both edges of the wrap and the offset rows
  - the Escape rung
  - `RegistryTests` and `HudFontTests` over the new literals
  - a PlayMode `WorldScreenTests`: New game → World → Next → setup → Start builds a colony whose
    header carries the tile

## 12. Deliberately not in this unit

- Factions.
- Rivers, roads and coast.
- Rainfall-driven vegetation and wildlife.
- Sliders and planet size.
- A globe.
- Travel.
- Any biome but the meadow being settled.
- Ruin density doing anything.
- A world tab during play.
