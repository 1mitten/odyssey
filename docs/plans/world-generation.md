# Plan — world generation: the seam, the planet, the World screen

**Phase 3, 2026-09-26.**
- Interview: `docs/research/world-generation-interview.md` (every recommendation taken except the
  biome art, which is "one for now").
- Design: `docs/design/57-world-generation.md`.
- Brief: `docs/reference/mockups/world-map-brief.md`.
- Reference: `docs/research/a-13-world-generation.md`.

**Built 2026-09-26 (WG0b, WG1, WG2, WG3a, WG3b, WG4 as commits on the branch); not yet played; the World page owes both Unity tiers and a player build. Approved 2026-09-26 (owner: *"ok go"*). Claude Design's specification is `docs/reference/mockups/world-screen-spec.md`; the owner's rulings on it (six biomes, our hill names, region names and zoom built) are in design 57 §6 and §9a.**

Every unit lands on `claude/sharp-euler-a6xtci` as its own commit, because this session may push
only there. A PR is opened when the owner asks.

## Units

WG1 and WG2 need only design 57 approved. WG3's look takes its constants from what Claude Design
returns; its engine-free model can be built before that.

| Unit | What | Where the decision lives | Gate |
|---|---|---|---|
| **WG1** the seam | `SiteTile`, `HillBand`, `BiomeDef` and `Biomes.xml` (registered in `WorldContent`), `SiteBoard` (the hill table), `SiteClimate`, `SiteSeed`. `ColonyRequest.Site`, `ColonyWorld.DefFor(…, site)`. `ColonyComposition` takes the climate and the wet factor from the request, and `WeatherSystem` gains `wetPerMille`. `BuildSession` decides a mountainous site's depth. `SaveRecipe` grows, and the format goes 10 → 11. | §5, §7, §8 | fast tier and the Long tier with **no golden moved**; `BoardMemoryTests` arms per band; content gates |
| **WG2** the planet | `PlanetDef` and `Planet.xml`, `PlanetGrid` (offset hex rows, east–west wrap, six neighbours, latitude), `PlanetGenerator` (the seven passes of §4b, a wrapped-lattice noise over `ValueNoise`, rank cuts for sea and hills, the settleable guarantee), `Planet.SuggestedSite`. Not saved. | §4, §6 | fast tier; generation timed and recorded in §4 |
| **WG3a** the model | `MenuScreen.World`, `WorldChoice` (seed via `SeedField`, `Select`, `CanSettle` with a reason key, `SuggestedSite`, `RandomSite`, the stats lines through `TemperatureLabels`, `Back` / `Next`), `HexPick` (pixel → tile, wrap-aware), `NewGameChoice` gaining `WorldSeed` and `Tile`, the Escape rung, and the candidates kept across Back. Registry rows `ui.world.*`. | §9 | fast tier; content gates |
| **WG3b** the screen | `HudShell.World.cs` (`BuildWorldPage`): the map as one `Texture2D` painted per seed, hover and selection, the legend, the site panel, the foot buttons, and the keyboard. The setup page loses its seed row and gains the site line. `OnStartNewGame` hands the site to `BuildSession`. `WorldLayout` holds the brief's constants. | §9, the brief | Unity tiers (the owner's machine); PlayMode `WorldScreenTests`; a player-build smoke run |
| **WG4** close | The measurements into design 57 with machine and date; a `CLAUDE.md` status row kept short; a playtest-queue row; the journal; the wiki artifact republished. | — | the three content gates |

## What the owner is asked for

- **Approval of design 57**, and vetoes of the proposed biome and terrain names (§5, §6) and of the
  "no hemisphere flip" rule (§7).
- **The two screenshots** the brief attaches: the title screen and the New game setup page.
- **Running the brief through Claude Design**, and pasting back what it returns.
- After WG1: whether a 24-layer Huge mountainous board costs too much memory (design 57 §5).
