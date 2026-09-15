# Lane A12 — Map generation: from biomes and ore to streets, shells and salvage

## Question

How does RimWorld's map generation work — biomes, terrain and fertility, rock and ore placement, geothermal vents, rivers and roads, ancient ruins and dangers, and starting resources — and how does each element translate to Odyssey's ruined-city equivalents: the street grid, shell templates, damage passes, salvage deposits, underground strata and utility tunnels?

Clean-room note: everything below is paraphrased from the public RimWorld wiki, the public modding tutorials and community discussion. No Def XML was copied and no decompiled source was read (searches surfaced decompile repositories; they were not opened).

## Findings

### Two levels of generation

RimWorld generates in two stages: a **planet** at game start, then a **local map** whenever a world tile is settled. The world stage decides everything the local stage will honour: the tile's biome, its hilliness class, which rivers and roads cross it and in which directions, its 2–3 stone kinds, and its elevation/temperature/rainfall. Odyssey's analogue is a light "city plan" stage (district field + arterial street skeleton) that fixes what each 250×250 map must honour before any cell is generated.

### World generation sequence (public wiki)

1. **Terrain step** — layered noise ("controlled randomness shaped by latitude and elevation") gives every world tile elevation, temperature, rainfall, hilliness (flat / small hills / large hills / mountains) and swampiness.
2. **Biome competition** — every biome computes a score for every tile through its worker class; the highest score wins the tile. Twelve vanilla biomes.
3. **Lakes** — small isolated ocean patches become lakes.
4. **Rivers** — traced downhill from wet regions to the coast; more frequent where rainfall is high.
5. **Ancient roads** — a light "ancient civilisation" pass scatters ancient sites and joins them with decayed asphalt roads.
6. **Settlements** — faction bases are placed (weighted by each biome's settlement-selection weight).
7. **Modern roads** — link settlements to the network; road layout is not derived from the world seed, so identical seeds can differ here.

### Local map generation: pass order

Map generation runs an ordered list of generation steps. Publicly documented structure: a map-generator definition holds a list of step definitions, each carrying an **integer order value** that fixes the sequence (the modding wiki's XML/C# tutorial shows a step with order 400 as its worked example); biomes can append extra steps or suppress standard ones. The vanilla sequence below is reconstructed from the wiki, mod documentation and community posts — the exact order numbers were not taken from game files:

1. **Elevation and fertility grids.** Two map-sized float grids are filled with layered Perlin-style noise. The world tile's hilliness scales the elevation grid — on mountainous tiles large regions exceed the rock threshold; on flat tiles almost none do.
2. **Caves.** Natural tunnel systems are carved through thick rock (they later receive harvestable mushrooms and sometimes dormant insectoid hives).
3. **Terrain assignment.** Cells whose elevation exceeds a threshold become solid natural rock (wall + rough rock floor). Everywhere else the fertility grid is mapped through the biome's **fertility→terrain bands** (e.g. low → sand or gravel, mid → soil, high → rich soil). Separate **Perlin-noise patch makers** owned by the biome overlay pockets of marsh, shallow water or rich soil. Rivers recorded on the world tile are carved across the map as water terrain; coastal tiles get beaches.
4. **Rock kinds and roof.** The tile's 2–3 stone types (granite, marble, limestone, etc., pure RNG unless the biome forces types) are assigned to the rock mass; thick rock is roofed with "overhead mountain" (impenetrable from above, collapses when unsupported, shelters insects).
5. **Scatter passes** (blob/cluster scatterers with per-map counts or per-area densities):
   - **Mineable ore lumps** — blobs of ore cells inside or beside rock. Compacted steel spawns in veins of roughly 30–40 cells; each block yields a base 40 of its material (2 for compacted machinery's components); a steel block has 1,500 HP. Seven vanilla ores: compacted steel, compacted plasteel, compacted machinery, silver, gold, jade, uranium.
   - **Steam geysers** — randomly scattered points; count and spacing are not documented publicly, but every map gets several; each accepts exactly one geothermal generator (3,600 W constant).
   - **Rock chunks** — loose stone debris scattered on open ground.
   - **Simple ruins** — small fragments of broken stone walls, unowned, free to cannibalise.
   - **Ancient shrines / ancient dangers** — sealed undamaged rooms containing cryptosleep caskets (hostile ancients, neutral ancients, megascarabs or nothing — opening one opens all), a guarding threat (mechanoids or insectoids), decorative sarcophagi and columns, and high-value loot (artefacts, luciferium 5–20 at ~35% chance, advanced components, bionics). Community-documented density: ~0.12–0.25 shrines per 10,000 cells, with a floor of about one per 83,000 cells; guaranteed (100%) on the starting map, ~25% on other maps, so a 250×250 map carries roughly one or two.
6. **Roads.** World roads crossing the tile are drawn across the local map as terrain strips (dirt path up to asphalt), bridging obstacles where needed.
7. **Life and weather state.** Plants are scattered using the biome's overall plant density and its weighted wild-plant list; animals likewise from the weighted wild-animal list and animal density; snow depth is set from season and temperature.
8. **Start.** A player start location is chosen; the **scenario system** — not the terrain generator — scatters starting resources near the spawn (the crash-landing scenario drops pods, packaged meals, medicine, steel and components as scattered items). Enclosed spaces are fogged until breached.

### Data shapes (structure only, paraphrased)

**Biome definition** — field groups, by purpose:
- *World behaviour*: worker class for tile scoring; flags for natural generation; settlement-selection weight; whether bases, roads, rivers and caravan passage are allowed.
- *Climate*: temperature/rainfall envelope (via the worker), optional forced constant outdoor temperature; weighted weather commonalities; disease mean-time-between days plus a weighted disease list.
- *Terrain*: ordered fertility→terrain bands; a list of noise patch makers (threshold + terrain each); optional forced rock types.
- *Life*: overall plant density and animal density scalars; weighted wild plant and wild animal lists (plus special-case lists: coastal animals, polluted-ground animals, fish); forageability scalar and forage food type.
- *Map-generation hooks*: extra generation steps to append, standard steps to suppress, ambient sound set.

**Terrain definition** — the properties that matter mechanically: fertility percentage (rich soil 140%, soil 100%, gravel/stony soil 70%, sand 10%, marsh non-arable at 0%), path cost (move speed = 13 / (13 + path cost), so soil ≈ 87%, marsh ≈ 30%), buildability, water/bridgeable flags, and whether it is natural rock floor versus constructed floor.

### The one big structural difference

RimWorld's generator makes a **wilderness with a few man-made intrusions** (ruin fragments, one shrine). Odyssey inverts the ratio: the map is almost entirely man-made, and "nature" (soil, water, minerals) is the intrusion — exposed where pavement is broken and dominant only underground. The pass architecture (ordered step list, float-grid noise passes first, then discrete scatter passes, all driven by data definitions) transfers unchanged; only the content of each pass flips.

## 3D/layer impact

RimWorld is a single layer with "underground" faked by overhead-mountain roof and deep-drill deposits. Odyssey has ~40 real layers. Proposed vertical budget for a 250×250 map, generated per column as depth-banded 3D noise (the direct analogue of the elevation/fertility grids, one band per stratum):

| Layers | Stratum | Content |
|---|---|---|
| L+1 … L+20 | Building floors | Stamped from shell templates; most shells 1–8 storeys, rare towers to ~20. |
| L0 | Street level | Street grid, plazas, ground floors, rubble fields, exposed soil in parks/breaks. |
| L−1 | Service stratum | Basements, utility tunnels (power, water, data), storm drains — engineered voids in structural slab. Densest man-made underground content. |
| L−2 … L−3 | Deep infrastructure | Metro tubes and stations, deep foundations, a buried older city seam — the richest salvage stratum. |
| L−4 … L−6 | Engineered fill → natural soil | Rubble fill grading into soil and gravel; buried salvage lumps thin out, natural terrain begins. |
| L−7 … L−19 | Rock strata | Natural stone (2–3 kinds per map, as RimWorld's rock pass), mineral lumps replacing salvage, caves, water-table pockets; geothermal taps at the deepest band. |

Each stratum gets its own scatter densities (salvage-rich above, mineral-rich below), so the RimWorld pattern "one density constant per scatter step" becomes "one density constant per scatter step **per depth band**".

## Ruined-city impact

Translation table — one line per RimWorld element:

| RimWorld element | Odyssey ruined-city translation |
|---|---|
| World biome + scoring competition | District types (residential, commercial, industrial, civic, park) scored per map region from city-scale noise (age/wealth/industry fields); a DistrictDef mirrors the BiomeDef shape. |
| World rivers/roads on the tile | The arterial street skeleton and any canal/rail line crossing the map are fixed at city-plan level, and the local generator must honour them. |
| Elevation grid + hilliness | A building-height/density field: the district's profile (low-rise vs tower block) plays the role of hilliness and drives which shell templates may stamp where. |
| Fertility grid + fertility→terrain bands | An **intactness grid**: bands map to intact pavement → cracked pavement → rubble → exposed soil; plant fertility exists only where the surface is broken (parks, craters, split slabs). |
| Perlin patch makers | Perlin patches of rubble fields, ash, flooding and overgrowth laid over districts. |
| Natural rock mass + overhead mountain | Wholly collapsed structures and rubble mounds (unstable roof analogue); underground, structural slab and foundations are the "rock" that must be breached. |
| Rock kinds (2–3 per tile) | A construction-material palette per district (concrete, steel, composite) constraining shell templates, debris types and salvage yields. |
| Caves | Utility and metro tunnels under the street grid (pre-carved voids at L−1…L−3); true natural caves only in deep rock strata. |
| Ore lump scatter | **Salvage deposits**: blob-scattered wreck clusters — vehicle graveyards, collapsed stockrooms, buried containers — vein sizes ~20–40 cells at street level, richer sealed caches underground; deep strata revert to mineral lumps. |
| Steam geysers | **Live utility taps**: still-functioning grid nodes (power conduit, heat-exchange main, water main) scattered with minimum spacing; build the matching machine on one to harvest, exactly one machine per tap. |
| Rock chunks | Debris chunks — concrete slabs, girders, panelling — haulable and usable as building material. |
| Simple ruins scatter | Inverted: the whole map is ruins, so this pass becomes the **damage pass** over stamped shells — wall knockouts, floor collapses, burn scars, crater punches — global decay noise plus discrete local events. |
| Ancient shrines / dangers | Sealed vaults, locked basements and dormant security floors (mechanoid-analogue garrisons, stasis pods, prime loot); density ~0.1–0.3 per 10,000 cells **per inhabited layer**, at least one guaranteed on the starting map, mostly placed underground or on upper tower floors. |
| Roads across the local map | The street grid itself is the generator's skeleton: arterials first, block subdivision, then template stamping into blocks — streets are generated first, not scattered last. |
| Plant/animal scatter | Overgrowth pass (weeds in cracks, vines on shells, park regrowth) plus urban fauna per district density. |
| Snow/weather state | Unchanged, plus dust/ash accumulation as a city-flavoured equivalent. |
| Scenario starting resources | Crash-site scatter of starting supplies near spawn, plus a guarantee of accessible near-spawn salvage and one intact-enough shell. |
| Fog on enclosed spaces | Works better in 3D: every sealed room, basement and upper floor stays fogged until breached — per-layer fog is the exploration loop. |

**Proposed Odyssey pass order** (the translated GenStep list, ordered integers exactly as RimWorld does it):

1. City plan: district field, arterial street skeleton, canal/rail lines, utility routing (map-level inputs).
2. Column strata grids: 3D depth-banded noise for fill depth, rock depth, water table, intactness.
3. Street grid detail + block subdivision.
4. Shell template stamping (with basements injected below and floors above; templates override strata).
5. Damage passes: global decay noise, then discrete events (collapse, burn, crater) with per-district tuning.
6. Scatter passes per depth band: salvage lumps, utility taps, debris chunks, sealed vaults/dangers, mineral lumps at depth.
7. Overgrowth and fauna.
8. Start spot, starting supplies, fog of all sealed volumes.

## Layer questions touched

**Q8 — what worldgen puts underground:** a service stratum at L−1 (utility tunnels, basements, storm drains), metro and deep foundations at L−2…L−3, buried salvage lumps in engineered fill at L−4…L−6, then natural soil/gravel/rock strata with mineral lumps, caves, water pockets and deep geothermal taps. Sealed vaults (the ancient-danger analogue) sit mostly underground. RimWorld's deep-drill deposits become literal deeper layers instead of an abstraction.

**Q11 — what digging means in a city:** near the surface, "mining" is really demolition — breaching engineered slab and foundations, high effort (ore-block-like HP, ~1,500 HP class) but high salvage yield; the cheap route underground is reusing pre-carved voids (metro, drains, basements) rather than tunnelling fresh; classic RimWorld mining (soil, gravel, stone, mineral veins) only exists in the deep natural strata. This gives digging a three-texture progression — breach, reuse, mine — that RimWorld's single layer cannot express.

## Sources

- https://rimworldwiki.com/wiki/World_generation
- https://rimworldwiki.com/wiki/Modding_Tutorials/Biomes
- https://rimworldwiki.com/wiki/Modding_Tutorials/Linking_XML_and_C
- https://rimworldwiki.com/wiki/Ancient_danger
- https://rimworldwiki.com/wiki/Ore
- https://rimworldwiki.com/wiki/Compacted_steel
- https://rimworldwiki.com/wiki/Terrain
- https://rimworldwiki.com/wiki/Steam_geyser
- https://rimworldwiki.com/wiki/Geothermal_generator
- https://steamcommunity.com/sharedfiles/filedetails/?id=2111424996 (Map Designer mod — documents which vanilla defs/GenSteps exist and that a MapGeneratorDef holds the player-base step list)
- https://steamcommunity.com/app/294100/discussions/0/591759862602403023/ (ancient danger density figures, via search excerpt)

## Confidence

**Medium overall.** High for the element inventory, the biome/terrain data shapes and the world-generation sequence (all on the public wiki and modding tutorials). Medium for the exact local-map pass order: the ordered-step architecture is confirmed by the modding tutorial, but the specific vanilla sequence was assembled from wiki fragments and mod documentation rather than a single authoritative page (the authoritative source is game data, off-limits under clean-room rules). Low-to-medium for several numbers: ore vein sizes beyond steel, geyser counts, and the shrine density (a forum figure, not wiki-verified).

## Could not be determined

- The exact vanilla GenStep order values and the complete step list (only obtainable from game files or decompiled source, both off-limits).
- Steam geyser count per map size and minimum spacing.
- Per-ore commonality weights, lump sizes for ores other than steel, and deep-drill deposit sizes (wiki pages are stubs).
- Noise specifics (octaves, frequencies, thresholds) beyond "layered Perlin-style noise".
- Exactly how river carving interacts with terrain and bridges on the local map.
