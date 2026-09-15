# Lane E3 — Farm, Western Frontier and Particle FX: what they contribute

Written 2026-09-15. Method: local inspection only — prefab listings under `Assets/Synty/PolygonFarm/Prefabs`, `Assets/Synty/PolygonWesternFrontier/Prefabs`, `Assets/Synty/PolygonParticleFX/Prefabs` and `Assets/Synty/PolygonGeneric/Prefabs`, with dimensions taken from `docs/research/synty-inventory.csv`. No asset data copied; names and bounds only.

## Question

What do POLYGON Farm 1.7.3, POLYGON Western Frontier 1.7.2 and POLYGON Particle FX 1.4.1 contribute to the colony sim's concept needs — terrain, plants/crops, animals, furniture, tools, containers and effects — and where do they clash stylistically with the sci-fi city so badly they should not be used?

## Findings

### Crops and growable plants (Farm — the pack's core value)

`PolygonFarm/Prefabs/Plants` holds ~178 prefabs and is a near-complete growth-stage system. Most species come in `_S` / `_M` / `_L` size variants, which map directly onto sim growth stages, plus a harvested single item, a `_Group` pile and often a `Box_<crop>` crate.

- **Staged ground crops** (`_S/_M/_L`): Asparagus, Bean, Beetroot, Broccoli, Cabbage, Carrot, Chilli, Corn (`SM_Prop_Plant_Corn_01_S/M/L`), Cucumber, Eggplant, Hemp, Lettuce, Onion, Pepper, Potato (`SM_Prop_Plant_Potato_01_S/M/L`), Pumpkin (plain, Italian, White), Squash (Butternut, Delicata), Strawberry, Tomato, Watermelon. Generic filler bushes `SM_Prop_Plant_Ground_01–03` and `SM_Prop_Plant_Bush_01–03` also have S/M/L (some `_L2`) stages, with fruiting variants (`_Bean`, `_Pepper`, `_Tomato`, `_Chilli`).
- **Wheat**: `SM_Prop_Plant_Wheat_01–04` (0.19–0.47 m wide clumps, 0.85–1.14 m tall), `_Optimised_01–04` (cheaper meshes for mass planting) and `SM_Prop_Plant_Wheat_Cut_01/02` (stubble) — a full sow → grow → harvest → stubble sequence.
- **Harvested items**: single produce (`SM_Prop_Potato_01`, `SM_Prop_Tomato_01`, …), `_Group` piles, and produce crates `SM_Prop_Box_Apple/Apricot/Carrot/Cucumber/Eggplant/Onion/Peach/Plum/Potato_01` — ready-made item and stockpile visuals.
- **Orchard trees, two-stage**: `SM_Env_Tree_Apple/Apricot/Cherry/Lemon/Orange/Peach/Pear/Plum_01` each with a fruiting `_Grown_01` variant (~3.0 × 4.3 m — fits one cell footprint, breaches one 3 m layer by 1.3 m).
- **Field terrain**: `SM_Env_Dirt_Rows_01` and its `Center/End_Top/End_Bottom/Skirt/Mounds` variants are **5.00 × 5.00 m tiles = exactly 2 × 2 cells** at the 2.5 m pitch; `SM_Env_Dirt_01` is plain 5 × 5 dirt; `SM_Env_Vege_Rows_01–03` are pre-populated crop rows (~4.5 × 4.6 m). Farm's `Prefabs/Generic` adds ground planes, grass patches and a wheat-field ground (`SM_Generic_Ground_Flat_Wheat_01`).
- **Growing infrastructure**: `SM_Bld_Greenhouse_01` (5.57 × 4.99 × 7.71 m ≈ 2 × 3 cells), `SM_Bld_Greenhouse_Large_01` (12.87 × 6.77 × 32.46 m — set-piece only), `SM_Prop_PlantBox_Large_01` (4.20 × 0.56 × 7.69 m raised bed), `SM_Prop_Sprinkler_01` + `_Hose_01`, `SM_Prop_Pot_01/02`, `SM_Prop_SeedPacket_01/02`.

### Animals

**None. Zero animal prefabs in any owned pack.** A grep over every prefab under `Assets/Synty` for horse, cow, chicken, pig, sheep, goat, donkey, dog, cat, hen, rooster and animal returns nothing but false positives ("Box", "LetterBox", "CowboyHat"). Farm ships animal-adjacent props with no occupants: `SM_Prop_Chicken_Coop_01` (+ cage), `SM_Prop_Trough_01` (3.22 m feed trough), `SM_Prop_Beehive_01–06`, `SM_Prop_Horse_Jump_01–03`; Western Frontier has `SM_Prop_SkinRack_01`, `SM_Prop_WaterTrough_01`, fur rugs and `SM_Prop_ChestFur_01`. Livestock or wildlife requires a future pack purchase (e.g. a Synty animal pack) or Blender work — a Phase 1/owner decision, not something these packs cover.

### Furniture and interior props usable in a sci-fi colony

- **Farm**: `SM_Prop_Bed_01` (2.08 × 1.52 × 2.67 m — a 1 × 2-cell bed, wooden but plain), `SM_Prop_Table_01/02`, `SM_Prop_Chair_01`, `SM_Prop_Chair_Rocking_01`, `SM_Prop_Bench_01`, `SM_Prop_Rack_01`. All timber-styled; fine for a scrap-built colony interior, wrong for pristine sci-fi rooms.
- **Western Frontier**: `SM_Prop_CampBed_01` (early-game bedroll tier), `SM_Prop_Table_01` (1.09 × 0.62 × 1.63 m), `SM_Prop_Chair_01`, `SM_Prop_RockingChair_01`, `SM_Prop_Chest_01`, `SM_Prop_Cup_01`, `SM_Prop_Lantern_01` (pre-electric light source for un-powered cells).
- **PolygonGeneric (shared)**: `SM_Gen_Prop_Chair_01`, `SM_Gen_Prop_Table_01`, `SM_Gen_Prop_Shelf_01–03` (1.60 m shelving), `SM_Gen_Prop_Clock_01`, `SM_Gen_Prop_Light_Roof_01–03` / `_Light_Wall_01`, `SM_Gen_Prop_Screen_01`, `SM_Gen_Prop_Keypad_01`, `SM_Gen_Prop_Button/Lever/Switch` — plus **food items** `SM_Gen_Prop_Food_Bread_01/02`, `_Meat_01–06`, `_Vegetable_01–06`, with `Plate_01`, `Mug_01`, `Pot_01–05`: the meal-item visuals for the sustenance loop.

### Tools and salvageable props

- **Farm tools** (equippable-scale, 1.2–1.5 m): `SM_Prop_Tool_Axe/Bucket/Hayfork/Hoe/Pitchfork/Rake/Scythe/Spade_01`, `SM_Prop_Watering_Can_01`, `SM_Prop_ChainSaw_01`, `SM_Prop_ToolBox_01`, `SM_Prop_Anvil_01`, `SM_Prop_Vice_01`. PolygonGeneric duplicates axe/pickaxe/spade as `SM_Gen_Wep_*`.
- **Salvage/ruin dressing** (style-neutral, strong fit for a ruined city): Farm's `SM_Prop_Rubbish_01–09` and `SM_Prop_Rubbish_Pile_01–05`, `SM_Prop_Rusted_Drum_01`, `SM_Prop_Truck_Rusted_01`, `SM_Prop_Ute_Wreck_01`, `SM_Prop_Tyre_01`, `SM_Prop_GasCan_01`, `SM_Prop_Gastank_01`, `SM_Prop_Power_Pole_01` + `_Lines_01`, `SM_Prop_SpoolWheel_01`; WF's `SM_Prop_Pipes_01/02`, `SM_Prop_Quarry_Machine_01/02` (rusted industrial machinery), `SM_Prop_Wagon_Destroyed_01`.
- **Containers**: Farm `SM_Prop_Crate_01`, `SM_Prop_PalletCrate_01`, `SM_Prop_PlasticBin_01`, `SM_Prop_Barrel_01/02`, `SM_Prop_GrainBag_01` (+ open variants); WF `SM_Prop_Crate_01/02`, `SM_Prop_Basket_01`, `SM_Prop_WoodBox_01`, `SM_Prop_Chest_01`; Generic `SM_Gen_Prop_Cardboard_Box_01–05`, `_Crate_01–03`, `_Sack_01–05` (+ stacks), `_Barrel_Metal_01–03`, `_Barrel_Wood_01–03`. Between them, every stockpile tier from sack to pallet is covered.
- **Fences** (grid-friendly): Farm `SM_Prop_Fence_Wood_01` and `_Painted_01` are **exactly 2.50 m panels** (one cell edge); `_Fence_Wire_01` is 5.00 m (two edges); gates and poles included. WF adds `SM_Prop_PlankFence_01–03`, `SM_Prop_PikeFence_01/02` and `SM_Prop_Barricade_Sand/Wood_01–02` (defence-line visuals).
- **Explosives/mining** (WF): `SM_Prop_Tnt_Barrel/Box/Bundle/Detonator/Stick_01` — demolition and mining gameplay props; `SM_Prop_GoldPan_01`, `SM_Prop_Gold_Ingot/Nugget_01`.

### Western Frontier as "frontier shanty" in ruins

The pack is the mine/quarry/fort variant of the theme, not a saloon town — which helps. Pieces that read well as post-collapse improvisation inside a ruined sci-fi city:

- **Campfires**: `SM_Prop_Campfire_01` (3.28 m ring) and `SM_Prop_Campfire_Small_01` (1.29 m — one cell) — the **only cooking-capable props in all owned packs** (no stove, oven or grill exists anywhere; confirmed by grep).
- **Mine set** (the only underground kit among these packs): `SM_Env_Mine_Tunnel_*`, `SM_Env_Mine_Track_*` (straights, curves, crosses, pulleys), `SM_Env_Mine_Framing/Wall_*`, `SM_Env_Mine_Entrance_01` (+ blocked variant) — directly relevant to below-ground z-layers and collapsed-basement biomes.
- **Terrain**: `SM_Env_Rock*`, `SM_Env_Mound_*` (hills, slopes, ramps), `SM_Env_Quarry_Wall_*` (excavation pit walls — useful for crater/collapse edges), `SM_Env_River_*` segments.
- **Camp tier**: `SM_Bld_Tent_01/02` (7.51 × 2.65 × 4.11 m), `SM_Prop_CampBed_01`, `SM_Prop_Lantern_01`, `SM_Prop_LogSeat_01`, `SM_Prop_WoodPile_01/02`, `SM_Prop_LogPile_01` — the "night one" settlement tier before walls exist.
- **Timber structure**: `SM_Bld_Cabin_01` (8.63 × 6.19 × 8.65 m), `SM_Bld_Fort_Wall/Walkway/Tower_*` (palisade defences), `SM_Bld_Quarry_Framing_01–03`, `SM_Prop_Ladder_01/02`.

### Particle FX catalogue mapped to sim events

The dedicated pack (~180 FX prefabs) plus per-pack FX folders cover every M4/weather event:

| Sim event | Prefabs |
|---|---|
| Fire, per cell, staged | `FX_Fire_Small_01–03` → `FX_Fire_01` / `FX_Fire_Big_01–03` → `FX_Fire_Huge_01`; smoulder `FX_Embers_01`; cheap LOD card fire `FX_Fire_Card_Small/…/Huge_01` (PolygonGeneric) for many simultaneous burning cells; Farm `FX_Fire_01/02`, WF `FX_Fire_01`, Sci-Fi City `FX_Fire_01/02`, `FX_Flame_01` |
| Smoke | `FX_Smoke_Black_Small/─/Large_01`, `FX_Smoke_White_Small/─/Large_01`, `FX_Smoke_Trail_Small/Large_01`; WF `FX_Smoke_Tall_01` (chimney column); Sci-Fi City `FX_Smoke_Drift_01`, `FX_Smoke_Small_Light_01` |
| Dust from collapse | `FX_Dust_Big_01`, `FX_Dust_Small_01`, `FX_Dust_Blowing_Soft_01`, `FX_GroundCrack_Blast_01`, `FX_Impact_Stone/Dirt/Wood/Metal_01`; Farm `FX_Dust_Wind_01`, `FX_Vehicle_Dust_01`; WF `FX_Dust_Blowing_01` |
| Rain and weather | `FX_Rain_01` + `FX_Rain_Collision_01` (splash layer), `FX_Snow_01`, `FX_Blizzard_Snow_01`, `FX_Fog_Big/Small_01`, `FX_Wind_01/Hard/Soft`; Farm `FX_Rain`; Generic `FX_Rain_01`, `FX_Snow_01`, `FX_Fog_01` |
| Sparks from damaged power | `FX_Sparks_01`, `FX_Electricity_01/02` (also Sci-Fi City's own `FX_Electricity_01`); burst pipes `FX_Steam_01–03` |
| Explosions | `FX_Explosion_01`, `FX_Explosion_Large_Dark_01`, `FX_Fire_Explosion_01`, `FX_Grenade_Explosive/Flash/Smoke_01`; oversized set-piece `FX_Nuke_01/_Smaller_01` |
| Ambience and state cues | `FX_Flies_01` (rot/filth indicator), `FX_Fireflies_01`, `FX_WaterDrip_01` + `FX_WaterRipple_01` (leaking ruins), `FX_Leaves_Green/Blossom_01`, `FX_SunBeam_Small/Large_01` (light shafts through broken roofs), `FX_Waterfall_01` |
| Combat (later) | `FX_Gunshot_*` (9), `FX_Shell_Ejection_*` (6), `FX_BloodSplat_01/_Small_01`, `FX_LazerBeam_01`, `FX_Missile_01` |

Caveats: the inventory records **2 errors loading `PolygonParticleFX/Scenes/Demo.unity`**, and 58 particle materials use `Legacy Shaders/Particles/*` — each FX prefab used by the slice needs a one-off URP render check in the editor before being committed to.

### PolygonGeneric additions beyond Sci-Fi City's needs

The shared folder is a merged superset installed by all packs (SyntyPackageHelper holds no per-pack manifest, so attribution is not recoverable locally). Contents plainly serving the Farm/WF side rather than the city: food props (`SM_Gen_Prop_Food_*`), wooden barrels, sacks, ropes, chests, planks; nature set (`SM_Gen_Env_Ivy_01–13` + draped — **ideal for overgrown ruins**, `_Vines_01–05`, ferns, mushrooms, dead trees, cliffs, stalactites); plus `SM_Gen_Chr_Charred_01` (a burnt character — a ready-made fire-death visual) and `SM_Gen_Chr_Peasent/Prisoner/Jumpsuit/Street_*` civilians. Western Frontier's own `Prefabs/Generic` contributes the `SM_Gerneric_Grass_Patch_01–03` prefabs (the "Gerneric" typo family in the inventory).

## Style verdicts

| Family | Verdict | Reason |
|---|---|---|
| Farm crops, wheat, orchard trees, dirt/vege rows | **Use** | Plants are style-neutral; the whole growing loop depends on them |
| Farm greenhouses, plant boxes, sprinklers, silos, water tower | **Use** | Utilitarian agriculture reads fine in a ruined-city colony |
| Farm tools, rubbish piles, wrecked vehicles, drums, power poles | **Use** | Salvage/ruin dressing, effectively style-neutral |
| Farm wooden fences (2.5 m panels), gates | **Use** | Snap to cell edges; scrap fencing suits a rebuilding colony |
| Farm timber furniture (bed, tables, chairs, bench) | **Use sparingly** | Fine as low-tier/scrap furniture; do not furnish "high-tech" rooms with it |
| Farm hay bales, barns, farmhouses, windmill, weathervanes, scarecrow | **Use sparingly** | Rural Americana; acceptable in outskirts/farm districts, jarring in the city core |
| Farm letterboxes (17), ranch/produce signs | **Exclude** | English-text rural signage; breaks the invented-world fiction |
| WF campfires, lantern, camp bed, tents, log/wood piles | **Use** | The pre-power settlement tier; campfire is the only cooker owned |
| WF mine tunnels/tracks, quarry walls, rocks, mounds, rivers | **Use** | Only underground kit owned; quarry walls double as crater edges |
| WF barricades, plank fences, crates, chests, TNT | **Use** | Defence lines, storage and demolition; style-neutral timber/scrap |
| WF fort walls/towers, cabin | **Use sparingly** | Palisade-as-scrap-wall works; keep the "Wild West fort" silhouette rare |
| WF Mexican adobe buildings/walls | **Exclude** | Unmistakably Old-West Sonoran architecture; nothing in a sci-fi city explains it |
| WF teepees, totem poles, skull poles, wagons, photos, trumpet | **Exclude** | Strong period/cultural signifiers; wrong fiction entirely |
| WF characters (bandits, Native Americans, priest, soldiers) | **Exclude** | Period costume; Farm farmer characters are borderline — usable as generic settlers only without cowboy/scarecrow hats |
| WF period weapons (revolver, rifles, bayonet, sword, chain gun) | **Exclude** (slice) | Old-West arms clash with laser-era Sci-Fi City weapons; pickaxe/axe/spade/knife/hammer are fine as tools |
| Particle FX: fire, smoke, dust, rain, snow, steam, sparks, explosions, impacts, flies, drips, sunbeams | **Use** | Complete coverage of M4 and weather events |
| Particle FX: cartoony, magic, fairy, heal, portal, ritual, shards, sword slashes, fireworks, confetti, rainbow, level-up, pick-ups, money | **Exclude** (world sim) | Fantasy/arcade language; at most reconsider later as deliberate UI feedback, never as in-world effects |

The general rule that emerges: in a ruined sci-fi city, **weathered timber and scrap read as post-collapse improvisation and are on-theme**; what breaks the fiction is not the material but **period and cultural signifiers** (adobe missions, teepees, letterboxes, cowboy costume, English-text ranch signs).

## Slice picks

Committed prefab families for the vertical slice:

- **Growing (M5)**: field visual `SM_Env_Dirt_Rows_01` family (5 × 5 m = 2 × 2 cells; one tile serves four field cells). Primary crop **potato** — stages `SM_Prop_Plant_Potato_01_S/M/L`, harvest items `SM_Prop_Potato_01` / `_Group` / `SM_Prop_Box_Potato_01`. Secondary crop **wheat** — `SM_Prop_Plant_Wheat_Optimised_01–04` for the field, `SM_Prop_Plant_Wheat_Cut_01/02` after harvest. Indoor/raised growing later via `SM_Prop_PlantBox_Large_01` or `SM_Bld_Greenhouse_01`.
- **Cooking (M5)**: tier 1 `SM_Prop_Campfire_Small_01` (WF, one cell); tier 2 (powered stove) must come from the Sci-Fi City interior catalogue (Lane E2) as **no stove/oven exists in any owned pack**. Meal and ingredient items: `SM_Gen_Prop_Food_Vegetable_01–06`, `_Bread_01/02`, `_Meat_01–06` with `SM_Gen_Prop_Plate_01` and `_Pot_01–05` (PolygonGeneric).
- **Fire (M4)**: per-cell staged fire `FX_Fire_Small_01` → `FX_Fire_01` → `FX_Fire_Big_01` (`FX_Fire_Huge_01` for fully involved cells), smoulder/aftermath `FX_Embers_01` + `FX_Smoke_White_Small_01`, active-burn smoke `FX_Smoke_Black_01`, LOD tier `FX_Fire_Card_*` when many cells burn. Ignition sources: `FX_Sparks_01` / `FX_Electricity_01` on damaged conduits; `FX_Explosion_01` for fuel/battery cells. Fire-death visual: `SM_Gen_Chr_Charred_01`.
- **Storage**: `SM_Gen_Prop_Sack_01–05` → `SM_Gen_Prop_Crate_01–03` → `SM_Prop_PalletCrate_01` as stockpile tiers; produce crates `SM_Prop_Box_<crop>_01` for food stockpiles.

## Layer questions touched

- The WF mine tunnel/track/framing set is the only underground (below-ground z-layer) construction kit among the owned packs; its module dimensions were not measured here and must be checked against the 2.5 × 2.5 × 3.0 m cell before any basement/undercity biome is planned around it.
- Orchard trees (~4.3 m tall) breach one 3.0 m layer; multi-layer occupancy for trees needs a sim rule.
- `FX_Rain_01` is an area emitter; indoor cells under intact floors will need per-layer masking or emitter culling — a presentation-layer concern to record for Lane F.

## Sources

Local only (no web searches used):

- `D:\code\odyssey\docs\research\synty-inventory.md` (counts, scene-load errors, shader table)
- `D:\code\odyssey\docs\research\synty-inventory.csv` (all dimensions quoted above)
- Prefab listings under `D:\code\odyssey\Assets\Synty\PolygonFarm\Prefabs`, `...\PolygonWesternFrontier\Prefabs`, `...\PolygonParticleFX\Prefabs`, `...\PolygonGeneric\Prefabs`, `...\PolygonSciFiCity` (FX folder)

## Confidence

- **High** for all enumerations (crops, absence of animals, FX coverage, prop lists) — read directly from prefab names on disk and the generated inventory.
- **Medium** for style verdicts — aesthetic judgements from names and known Synty pack looks, not verified with in-editor screenshots; the owner should eyeball the excluded families before the verdicts are treated as final.
- **Medium** for Particle FX runtime behaviour in URP — 58 materials use legacy particle shaders and the FX demo scene logged 2 load errors; each slice-committed FX prefab needs a one-off render check.

## Could not be determined

- Which pack contributed which file to the shared `Assets/Synty/PolygonGeneric` folder — the packs merge into one folder and `SyntyPackageHelper` keeps no manifest; attribution above is inferred from content, not recorded facts.
- Whether the legacy particle-shader materials render correctly under URP 17.3 (and what the 2 ParticleFX demo-scene errors are) — needs an editor session.
- Dimensions of the WF mine tunnel/track modules against the cell grid — not measured in this pass.
- Any path to animals within the owned library — there is none; whether to buy an animal pack (or model in Blender) is an owner decision for Phase 1/interview follow-up.
