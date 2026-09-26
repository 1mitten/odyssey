# Brief for Claude Design — the World screen

**2026-09-26.** The prompt below goes to Claude Design verbatim, with the attachments listed at the
end.
- **The decisions behind it:** `docs/research/world-generation-interview.md` (owner, 2026-09-26) and
  `docs/design/57-world-generation.md`.
- **What comes back** is built as unit WG3 in `docs/plans/world-generation.md`. Its measurements
  become constants in code (`WorldLayout`) and its biome colours become `BiomeDef.mapColour`.

---

## The prompt

You are designing one screen for **Odyssey**, a colony-simulation prototype in the RimWorld mould,
rendered in 3D with discrete vertical layers. The interface is a dark, flat HUD, already built and
shipped. You are adding to it and you must match it exactly.

Attached are:
- the HUD's own mockup (`hud-v2.html`)
- the Work tab's mockup (`work-v1.html`)
- screenshots of the running game's **title screen** and **New game setup page**, which this screen
  sits between

**Everything you draw must look like it came from the same hand as those.**

### What the screen is for

Before a colony starts, the game generates a **planet** from a number, the *seed*. The player looks
at the planet and **picks where the colony lands**. The place picked decides:
- the colony's **climate**: how warm it is on average, and how hard its three seasons bite (the
  seasons are called *Wash*, *Glare* and *Rime*: a wet spring, a dry summer and a killing winter)
- **how hilly the ground is**: flat, rolling, hilly or mountainous

The planet is a **flat map of hexagonal tiles, 64 across and 32 down**. It **wraps east to west**:
walking off the right edge brings you in on the left. The top and bottom rows are the poles. Each
tile has a **biome** (what grows there), a **terrain** (how hilly), a mean **temperature**, a
**rainfall** and a **latitude**.

**Only one biome can be settled in this version: Meadow.** Every other biome is shown, because the
planet should read as a real world, but it cannot be picked yet. Its art has not been made. A tile
can also be refused for being open water or too steep.

The flow is: title screen → **New game** → **this screen** → *Next* → the existing setup page (the
colony's name, the board size, and three colonists to choose from) → *Start*.

### What is on the screen

1. **A title band**: *World*, at the panel-label step, with the usual close X for Back.
2. **The seed**: the seed field and its **Reroll** button, exactly as the setup page draws them
   today (attached screenshot). They have moved here from the setup page.
3. **The map**, as large as the screen allows. It has:
   - Each biome in its own flat colour (see *Colour*).
   - **Terrain shown on the map**, so mountains can be seen without selecting anything. Propose one
     treatment: hatching, a shade step, or a small drawn mark per tile. It must stay legible on
     every biome's colour.
   - **Tiles that cannot be settled** distinguishable at a glance from those that can. Propose how:
     dimmed, hatched, or outlined. Draw it.
   - **A hover state** (one tile under the pointer) and **a selected state** (the chosen site).
     Draw both, and make the selected tile findable at a glance on a map of 2,048 tiles.
   - **The poles**: the top and bottom rows are ice. Say whether the map needs a latitude scale or
     an equator line, and draw it if so.
   - **No text on the map** except, optionally, a latitude scale. No place names: the planet has
     none yet.
4. **A legend**: each biome's colour and name, and the terrain treatment. Compact, never scrolling.
5. **The site panel**, reading the selected tile, one line each:
   - **Biome**, as a name and a one-sentence description the game supplies
   - **Terrain**: Flat, Rolling, Hilly or Mountainous (or Sheer)
   - **Mean temperature**, as `9 °C`
   - **Seasons**: the three seasons' mean temperatures on one line, for example
     `Wash 17 °C   Glare 25 °C   Rime -3 °C`
   - **Rainfall**: `1,000 mm a year`
   - **Latitude**: `53° N`
   - **Depth**, only on a mountainous site: `24 layers`
   - **When the tile cannot be settled, why**, in the warn colour, in one of three forms: *Not yet
     available*, *Open water* or *Too steep to settle*

   Draw the panel for a settleable meadow, for a mountainous meadow, and for an unsettleable tile.
6. **Along the foot**: **Back**, **Random site** and **Next**, in the setup page's button style.
   Next is disabled while the selected tile cannot be settled; draw that state.

The screen **opens with a site already selected** (the game suggests one), so a player who does not
care can press Next at once.

### The setup page, after

The seed row leaves the setup page. In its place goes **a read-only site line**, for example
*Meadow, rolling, 53° N*, with *24 layers* added on a mountainous site. Draw the top band of the
setup page with that line, in one state. Nothing else on that page changes.

### The design system, verbatim

Do not introduce any colour, face, size or spacing not listed here, **except the biome colours this
brief asks you for**.

**Colour**

| Token | Value | Use |
|---|---|---|
| Panel fill | `#0c1014` at 100% | every panel and window |
| Bar fill | `#0c1014` at 90% | the command bar |
| Panel border | white at 13% | 1 px, every panel |
| Divider | white at a lower alpha than the border | row rules |
| Text primary | `#eef3f6` | names, row text |
| Text meta | white at 66% | meta lines, secondary values |
| Text dim | white at 50% | headers, hints |
| Accent | `#6fd3e3` | the selected thing, the active control |
| On accent | `#0b1116` | ink on an accent fill |
| Warn / Bad / Good / Info | `#e8b55c` / `#e06a5c` / `#7fc98c` / `#8fd0e3` | only where this brief invites one |

**The biome colours are yours to choose, and they are the one new palette on this screen.** Ten
biomes:

| Biome | What it is |
|---|---|
| Sea | open water, not land |
| Icefield | permanent ice at the poles |
| Frost barrens | cold and dry |
| Pinewood | cold, conifer forest |
| Moor | cold and wet |
| Scrubland | warm and dry |
| **Meadow** | temperate grassland and woods; **the only one you can settle** |
| Fen | temperate and waterlogged |
| Dust flats | hot and dry |
| Wildwood | hot and wet, dense forest |

The colours must:
- be muted enough to sit under the HUD's flat dark panels
- make **Meadow findable at a glance**
- keep **every neighbouring pair distinguishable under deuteranopia, protanopia and tritanopia**.
  The game tests this. State each colour as a hex value, and say which pairs you checked and how.
- keep the accent's selected mark clear against every one of them.

**Type**: six steps and no seventh. Words in **Archivo Narrow**; every figure (seed, temperatures,
rainfall, latitude, layers) in **IBM Plex Mono 500** with tabular figures.

| Step | Use |
|---|---|
| 11 / 600, tracked 0.14em, upper case | panel labels and headers: "WORLD", "SITE", "LEGEND" |
| 12 / 400 | meta lines, the reason a tile cannot be settled |
| 13 / 400 | body text, the biome's description |
| 14 / 500 | panel rows, button labels |
| 19 / 600 | the selected biome's name |
| 11 mono at 35% ink | a hotkey cap |

**Space**: rows are 30 px; panel padding 12 px; a 1 px border; gaps of 9 px. Design at 1920 × 1080
and say how the map scales down to 1280 × 720. **The map's aspect is fixed** by the hex grid: 64
pointy-top hexes across and 32 rows, offset rows shifted half a hex. State the hex size you chose
in pixels at 1920 × 1080.

**Keyboard**: the arrow keys move the selection one tile, Enter is Next, R is Random site, and
Escape is Back. Show the hotkey caps where the setup page shows them.

**Icons and marks**: the game's icons are pixel art at 32 px; use a flat placeholder square where
one would go. Any mark you draw (a terrain mark, a selection marker, a chevron) is **an SVG path**.
**No characters outside ASCII anywhere**: the two shipped fonts draw nothing else, so a tick, a
cross, an arrow or a dingbat would render as an empty box. The degree sign in `9 °C` is the one
exception, because the game already draws it.

### Deliverables

Static HTML, one file per state, 1920 × 1080. Load the fonts from Google Fonts exactly as
`hud-v2.html` does, and declare the tokens once as CSS variables at the top of each file, so the
file can be diffed against the shipped tokens.

1. `world-open.html`: the screen as it opens, with a suggested meadow site selected and one other
   tile hovered.
2. `world-mountain.html`: a mountainous meadow selected, with the depth line showing.
3. `world-refused.html`: an unsettleable tile selected (Pinewood, *Not yet available*), and Next
   disabled.
4. `world-legend.svg`, or inline in file 1: the ten biome swatches and the terrain treatment.
5. `setup-site-line.html`: the setup page's top band with the read-only site line.

At the top of each HTML file, in an HTML comment, **list every measurement you chose that the
system above did not fix**. That means:
- the hex size
- the map's placement and margins
- the panel widths
- the ten biome colours
- the terrain treatment's alpha or stroke
- the hover and selection marks

Those numbers become constants in code.

### What not to do

- No globe, no 3D, no perspective. The map is flat.
- No place names, faction marks, rivers or roads: the planet has none yet.
- No sliders (temperature, rainfall, planet size): the seed is the only input.
- No scrollbars. No rounded, glowing, translucent or gradient panels.
- Do not rename *World*, *Random site*, *Next*, *Back*, *Reroll* or the biome and terrain names
  above.
- Do not copy RimWorld's world screen: the layout is this game's setup page, carried one screen
  earlier.

---

## Attachments to send with the prompt

| File | Why |
|---|---|
| `docs/reference/mockups/hud-v2.html` (and `icon-map.js` beside it) | the HUD's own mockup: the tokens, the panels, the buttons |
| `docs/reference/mockups/work-v1.html` | a large docked table in the same system |
| a screenshot of the **title screen** | the dock this flow starts from (design 40) |
| a screenshot of the **New game setup page** | the seed field, Reroll, the button row and the page's proportions: the screen this one sits in front of |

The two screenshots are the owner's to take; nothing in the repository holds them yet.

## Answers recorded

Owner, 2026-09-26:
- seam, planet and screen
- a climate biome plus a ruin density (the ruin density is not drawn)
- a flat hex grid wrapping east–west
- one settleable biome, the rest shown and not settleable
- the tile drives climate and hills
- the seed only
- its own screen before the setup page
- mountainous sites are 24 layers
- a Claude Design brief first
- no factions yet

## Answered

**Claude Design answered on 2026-09-26 with a written specification** (mockups 26a and 26b). The
owner pasted it into the session, and it is kept verbatim in `world-screen-spec.md` beside this
brief. It drew six biomes rather than the ten asked for, gave the hill bands the reference's own
names, and added region names and zoom — the second and third against this brief's own *What not to
do*.

The owner ruled the same day: **the six biomes, our own hill names, and region names and zoom
built**. Design 57 §6 and §9a–§9c carry what was built and every departure.
