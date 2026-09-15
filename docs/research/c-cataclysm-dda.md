# Lane C — Cataclysm: DDA: z-level storage, 3D FoV, cross-level pathing

## Question

How does Cataclysm: Dark Days Ahead (CDDA) implement z-levels — map storage across vertical levels, 3D field of view and line of sight, cross-level pathfinding, vertical movement (stairs, ladders, falling) — and what does its long z-level retrofit teach a project designing layers in from day one? Studied for technique only; CDDA is CC-BY-SA and no code is copied.

## Findings

### Map storage: a 3D grid of 2D chunks, with heavy per-level caching

- The active world (the "reality bubble") is a 3D grid of **submaps**: 11 × 11 submaps horizontally (`MAPSIZE = 11`) × 21 vertically (`OVERMAP_LAYERS = 1 + OVERMAP_DEPTH + OVERMAP_HEIGHT`, z from −10 to +10). Each submap is 12 × 12 tiles (`SEEX`/`SEEY`), so the bubble is 132 × 132 tiles × 21 levels (`src/map_scale_constants.h`).
- The map object holds a single flat vector of submap pointers indexed by (x, y, z); submaps are the unit of load and save. When the player crosses a submap boundary the grid pointers are shifted to re-centre the bubble rather than reloading everything.
- **The z-axis never scales.** Horizontal coordinates convert between four scales (map square → submap → overmap terrain → overmap) but z stays −10…+10 at every scale. After years of coordinate-mixing bugs the project built a strongly typed coordinate system that encodes *origin and scale in the type* (e.g. absolute vs bubble-local map squares), documented in `POINTS_COORDINATES` — a direct response to 2D/3D and local/global confusion.
- **Per-z-level `level_cache`** (lazily allocated from a free pool, one per occupied level): lightmap (four values per tile, one per wall-facing quadrant), transparency cache, *floor cache* (does this tile have a floor — the vertical occluder for vision and rain), outside/roof cache, seen cache, camera cache, visibility result cache, a per-submap field-dirty bitset, vehicle caches, and dirty flags for each. Two cheap short-circuits matter at scale: a per-level `no_floor_gaps` flag (entire level floored → nothing below is visible through it) and a cached "highest z-level above which all levels are uniform" so empty sky costs nothing.
- **Per-z-level `pathfinding_cache`**: one 32-bit flag word per tile (ground, obstacle, bashable, door, climbable, **stairs up, stairs down, ramp up, ramp down**, air, dangerous field/trap, size restrictions…), rebuilt from dirty points. Pathfinding reads these packed flags, not the terrain objects.
- Above the bubble, the **overmap** is a stack of single-level 24 × 24-tile "overmap terrain" (OMT) tiles: a building is a *pile of separate one-storey OMTs* (ground floor, first floor, roof, basement each a distinct OMT id linked vertically). This is the root of most of their mapgen pain (below).

### How much is simulated actively

- All 21 levels of the reality bubble are loaded and live at once: creatures act, fields (fire, smoke, gas) process, vehicles exist on every loaded level. Affordability comes from the lazy per-level caches, dirty flags and uniform-level short-circuits, not from freezing other levels.
- Exceptions that never got the retrofit: the **scent map is still a single 2D array** at bubble size, tracked around the player's level with a reach of one level (`SCENT_MAP_Z_REACH = 1` in `src/scent_map.h`) — smell effectively does not exist for most of the vertical world. Outside the bubble only overmap-scale abstractions (hordes, missions) tick.

### 3D field of view: recursive shadowcasting, extended to volumes

- The algorithm family is **recursive shadowcasting**, both in 2D (per-level `castLight`, run per quadrant with Beer–Lambert light attenuation) and in 3D (`cast_zlight`), which casts beams through a *stack of per-level 2D transparency and floor grids*, splitting each beam where a floor or ceiling clips it, upward and downward from the origin. Floors are binary occluders (an explicit TODO in `src/shadowcasting.h` notes semi-transparent floors are unsupported).
- A Bresenham-raycast alternative was prototyped and **rejected with data** (issue #6821): rays to the perimeter miss some cells entirely, revisit near cells up to ~46 times, and are cache-hostile. Kevin Granade: "I don't know of a solution other than shadowcasting that will work for this problem."
- **The cost story is brutal**: a 2016 benchmark in the same thread measured `cast_zlight` at ~81× the cost of one 2D `castLight` pass (~4× per tile across ~20× the tiles, worse once each level got its own arrays and cache pressure rose). Beam-splitting bugs caused pathological slowdowns (issue #24748); entering cities with 3D FoV on was a known performance cliff (issue #31952).
- Mitigations that stuck: a **`fov_3d_z_range` option capping how many levels up/down vision is computed**, the floor-cache and uniform-level short-circuits, and per-level seen caches only rebuilt when dirty. Even so, 3D FoV shipped as an *experimental toggle in 2015 and only became the default in January 2024* (PR #71306) — nearly a decade of dual vision systems, with a long trail of see-through-floor and cross-level targeting bugs (#14908, #30300, #66040, #29089).

### Cross-level pathfinding: one A*, per-level scratch layers, stairs as searched portals

- Creature routing is a single A* over 3D points within the bubble. The pathfinder keeps **per-z-level scratch layers** (open/closed bitsets, g-scores, parent pointers), lazily allocated the first time a level is touched; the heuristic is a scaled 3D grid distance.
- Horizontal expansion happens level-by-level against the packed flag cache. **Vertical expansion is special-cased, not a plain neighbour step**: when the search reaches a stairs-down tile it calls the same stair-resolution routine the player uses (`find_or_make_stairs`) on the level below, then searches *nearest-first within a one-submap radius (12 tiles)* for a matching stairs-up tile, and adds that as the successor. Ramps connect a tile to the eight surrounding tiles one level up or down; open-air tiles let the search consider climbing/dropping down. Per-creature `pathfinding_settings` gate all of it (can it climb stairs, open doors, bash, tolerate size restrictions).
- The route validity check explicitly tolerates discontinuities of one z-level — the comment in `src/pathfinding.cpp` reads "Jumps are acceptable on 1 z-level changes / This is because stairs teleport the player too". **Stairs are teleport portals whose far end is discovered at run time**, a direct consequence of levels having been generated independently so staircases on adjacent levels need not align.
- Consequences: NPC and monster vertical navigation has been degraded or broken repeatedly and for long stretches (issue #80421 is from 2025, a decade in), and zone/activity logic that must reason across levels (hauling, sorting) still misbehaves (#52549, #85073).

### Vertical movement: stairs, making a way, and falling

- Player vertical movement (`game::vertical_move`) is its own subsystem, not a grid move: find stairs near your column on the destination level; if a creature stands on them, negotiate; if no stairs exist, offer to *make* a way — rope ladder, web rappel, climbing, jumping down — with terrain-specific prompts (lava, sheer drops). Movement then *shifts the whole bubble's z* and re-centres.
- Falling is supported world physics: the map tracks support, marks tiles dirty when support is removed (`support_dirty`), processes falling terrain/items each turn (`process_falling`) and drops creatures standing on nothing (`drop_creature`). Vehicles change level via ramps — it was bridge/ramp work that finally forced z-levels to become mandatory.

### The retrofit timeline (why it took a decade)

2014: z-level master ticket #6822 opened with bounties. 2015: experimental z-levels land (vision, movement, vehicles). 2016: 3D FoV algorithm fight and 81× benchmark. 2020: PR #41707 removes the off switch — "z-levels are stable, and [the bridges PR] doesn't work with z-levels disabled and I refuse to maintain two different versions of bridges" (mlangsdorf); PR #41740 then strips the dual-mode special cases the option had forced ("lots of subtle and weird bugs that show up depending whether z-levels are on or off"). 2024: 3D FoV becomes default (PR #71306); mapgen finally gains real cross-level generation (PR #74169). 2025: vertical NPC navigation bugs still being filed (#80421).

## Retrofit pains to design away   (ranked)

1. **The dual-mode era.** Z-levels were optional for five years (2015–2020), so every map system carried 2D and 3D code paths; the maintainers describe the result as complicated code with "subtle and weird bugs that show up depending whether z-levels are on or off", and killing the option took its own PRs. Never ship a flat mode alongside a 3D one.
2. **Per-storey 2D mapgen.** Each overmap-terrain tile generates one level independently, so buildings are stacks of unrelated storeys: every roof in the game had to be hand-added years later (issue #28294 and the JSON roof-mapgen guide — "since the roof project, all buildings are now multi-tile across z levels"), and mapgen only gained genuine cross-level generation in mid-2024 (PR #74169), still with generation-order complications. Generate structures as 3D wholes from day one.
3. **Stairs as searched teleports.** Because adjacent levels were generated independently, a staircase's far end must be *found* at run time (nearest match within 12 tiles) or improvised — and that search leaks into pathfinding, NPC AI and player movement, and still breaks. Author vertical connectors as exact, aligned cell-to-cell edges.
4. **3D vision bolted on late.** Two vision systems coexisted for nine years; cross-level sight cost ~80× naïvely and needed caps (`fov_3d_z_range`), floor caches and uniform-level short-circuits before it could be default; see-through-floor bugs recurred throughout. Budget 3D FoV and its occlusion caches into the first design.
5. **Systems that never became 3D.** Scent is still a one-level 2D array; zones, stockpile sorting, aiming and NPC activities across levels each had (or still have) multi-year bug tails. Every system that touches a cell must be born z-aware — the ambient 2D assumption reasserts itself in every new feature otherwise.
6. **Coordinate chaos.** Mixing 2D/3D points and local/global frames generated enough bugs that the project retro-fitted an entire strongly typed coordinate system (origin and scale encoded in the type). Cheap to do on day one, expensive on day three thousand.

## Design lessons for Odyssey   (committed one-liners)

- One world representation, no flat fallback: a 3D chunk grid from the first commit, with per-layer caches (transparency, floor/occlusion, light, seen, path flags) and dirty flags as the performance backbone.
- Uniform-layer short-circuits are the cheapest big win: a "this whole layer is air/solid/floored" flag per layer makes 40 layers affordable the way CDDA's 21 are.
- Vertical connectors are first-class graph edges between exactly-aligned cells — a stair occupies known cells on both layers at mapgen time; no run-time matching, no teleports.
- Generate buildings and ruins as volumes (template stamping in 3D), never as independently generated storeys; roofs and floors are part of the template, not an afterthought.
- Use recursive shadowcasting per layer with a floor-occlusion cache for cross-layer sight, and cap the vision z-range (CDDA's `fov_3d_z_range` equivalent) — expect roughly an order of magnitude over 2D cost and design the caps in, not on.
- One A* over (x, y, z) with per-layer scratch buffers and packed per-cell path flags (walkable, stair-up/down, ramp, climbable, hazard) rebuilt incrementally — vertical steps cost like horizontal ones.
- Strongly type coordinates (cell vs chunk vs world vs layer) in the Sim assembly from day one.
- Any new system (smell-analogue, gas, sound, zones, AI sensing) ships z-aware or does not ship — CDDA's 2D scent map is the cautionary fossil.

## Layer questions touched   (1, 4, 5, 6, 10)

- **Q1 (vertical movement/pathing model):** CDDA proves one A* over 3D points with per-layer scratch state works, and proves that stairs-as-searched-portals is the wrong model — Odyssey's two-cell stairs and one-cell ladders should be pre-linked edges in the nav data at build/stamp time.
- **Q4 (light to lower layers):** their answer is a per-layer floor/occlusion cache consulted by volume shadowcasting — sun and light reach down exactly where floor cells are absent (shafts, broken floors); a per-layer "no gaps" flag skips whole layers.
- **Q5 (shoot up/down, 3D LOS):** yes, via the same 3D shadowcasting; the lesson is that LOS, targeting and rendering must share one visibility result per layer or see-through-floor bugs breed; cap the z-range of sight for both cost and design control.
- **Q6 (zones per layer or volumes):** CDDA's per-level zones produced years of cross-level hauling/sorting bugs — decide 3D-volume zones (or explicit per-layer with 3D-aware jobs) up front.
- **Q10 (unit of simulation for gas/fire/sound across layers):** CDDA processes fields on every loaded layer with per-chunk dirty bitsets, and its never-upgraded 2D scent map shows what happens when one medium is left flat; Odyssey's gas/fire/sound should adopt the per-layer-grid-plus-vertical-exchange pattern from the start.

## Sources   (URLs only — files, PRs, issues, docs)

- https://github.com/CleverRaven/Cataclysm-DDA/blob/master/src/map.h
- https://github.com/CleverRaven/Cataclysm-DDA/blob/master/src/map_scale_constants.h
- https://github.com/CleverRaven/Cataclysm-DDA/blob/master/src/level_cache.h
- https://github.com/CleverRaven/Cataclysm-DDA/blob/master/src/shadowcasting.h
- https://github.com/CleverRaven/Cataclysm-DDA/blob/master/src/pathfinding.h
- https://github.com/CleverRaven/Cataclysm-DDA/blob/master/src/pathfinding.cpp
- https://github.com/CleverRaven/Cataclysm-DDA/blob/master/src/scent_map.h
- https://github.com/CleverRaven/Cataclysm-DDA/blob/master/src/game.cpp
- https://docs.cataclysmdda.org/c++/POINTS_COORDINATES.html
- https://github.com/CleverRaven/Cataclysm-DDA/issues/6822 (z-level master ticket, 2014)
- https://github.com/CleverRaven/Cataclysm-DDA/issues/6821 (3D FoV design + 81× benchmark)
- https://github.com/CleverRaven/Cataclysm-DDA/pull/41707 (z-levels made mandatory, 2020)
- https://github.com/CleverRaven/Cataclysm-DDA/pull/41740 (dual-mode special cases removed)
- https://github.com/CleverRaven/Cataclysm-DDA/pull/71306 (3D FoV default, 2024)
- https://github.com/CleverRaven/Cataclysm-DDA/pull/74169 (3D mapgen, 2024)
- https://github.com/CleverRaven/Cataclysm-DDA/issues/28294 (roof project)
- https://github.com/CleverRaven/Cataclysm-DDA/blob/master/doc/JSON/JSON_Mapping_Guides/JSON_ROOF_MAPGEN.md
- https://github.com/CleverRaven/Cataclysm-DDA/issues/24748 (cast_zlight beam-splitting slowdown)
- https://github.com/CleverRaven/Cataclysm-DDA/issues/31952 (3D FoV city performance)
- https://github.com/CleverRaven/Cataclysm-DDA/issues/14908 · https://github.com/CleverRaven/Cataclysm-DDA/issues/30300 · https://github.com/CleverRaven/Cataclysm-DDA/issues/66040 (see-through-floor bugs)
- https://github.com/CleverRaven/Cataclysm-DDA/issues/80421 (NPC z-navigation, 2025)
- https://github.com/CleverRaven/Cataclysm-DDA/issues/52549 · https://github.com/CleverRaven/Cataclysm-DDA/issues/85073 (zones across z)
- https://discourse.cataclysmdda.org/t/turning-off-zlevels-option-on-the-latest-experimental-is-gone/24088/7

## Confidence   (high/medium/low, with why)

**High** for storage layout, per-level caches, pathfinding mechanics, stair resolution and the FoV algorithm family: read directly from current master source (`map.h`, `level_cache.h`, `shadowcasting.h`, `pathfinding.cpp`, `game.cpp`, `scent_map.h`, `map_scale_constants.h`). **High** for the retrofit timeline and rationale: taken from the maintainers' own PRs, issues and statements. **Medium** for the exact FoV cost figures (the 81× number is one contributor's 2016 benchmark on since-changed code; treat as an order-of-magnitude signal) and for "all bubble levels fully active" (inferred from structure — flat 3D grid, per-level field bitsets, 3D creature tracking — rather than from a single authoritative statement).

## Could not be determined

- Current, measured cost of `cast_zlight` on modern hardware after the 2024 optimisations (no recent public benchmark found within the search cap).
- The default value of `fov_3d_z_range` (the option and its performance role are confirmed; the shipped default was not verified in source).
- Whether any level-of-detail scheme reduces simulation fidelity for bubble levels far from the player (none was found; processing appears uniform across loaded levels with dirty-flag skipping only).
- The precise content of the deleted historical `doc/ZLEVELS.md` status document (referenced in community discussion; not present at the tags checked).
