# Lane E1 — Synty module → game concept mapping at 2.5 × 2.5 × 3.0 m

## Question

Map every relevant POLYGON Sci-Fi City and POLYGON Generic building module to a game concept — wall, floor, roof, door, window, stair, ladder, pillar, railing, furniture, power, light, storage, salvage — at the confirmed cell size (2.5 × 2.5 m footprint × 3.0 m height; stairs two cells, ladders one; a built roof is the floor above), and flag every concept with no fitting asset.

## Findings

**Headline: the entire buildable modular kit lives in `PolygonGeneric/Prefabs/Base` (`SM_Bld_Base_*`), not in Sci-Fi City.** Sci-Fi City contributes 5 m (two-cell) facade "Section" shells, whole kit-bashed buildings, and props. The Generic Base kit is built on exactly our pitch: walls 2.50 wide × 3.01 high, floors and ceilings 2.50 × 2.50, pillars 3.02 high, stairs rising 1.50 m per 2.5 m run. Snap tolerance below is ±0.02 m against integer multiples of 2.5 (footprint) and 3.0 (height); wall thicknesses (0.23–0.29 m) straddle the cell boundary (±0.11 around the edge) and are not tested against the pitch.

All sizes are X × Y(height) × Z in metres from `synty-inventory.csv` (root-relative axis-aligned bounds). "Gen" = PolygonGeneric, "SFC" = PolygonSciFiCity, "Farm" = PolygonFarm.

### Wall — verdict: fits

| Prefab | Size | Snaps? | Note |
|---|---|---|---|
| Gen `SM_Bld_Base_Wall_01` | 2.50 × 3.01 × 0.23 | yes | canonical one-cell wall; base pivot at one end |
| Gen `SM_Bld_Base_Wall_Corner_01` | 2.61 × 3.01 × 2.61 | yes (+0.11 trim) | corner piece; overhang is the wall-thickness trim |
| Gen `SM_Bld_Base_Wall_Half_01` | 2.50 × 1.51 × 0.23 | half-height | parapet / balustrade wall |
| Gen `SM_Bld_Base_Wall_Half_02` | 1.25 × 3.01 × 0.23 | half-cell | filler |
| Gen `SM_Bld_Base_Wall_Destroyed_01` / `_02` | 1.53 / 0.59 × 3.01 × 0.23 | partial by design | ready-made ruin states for damaged walls |
| Gen `SM_Bld_Base_Wall_Round_01`, `_Angle_01/02`, `_Thin_*` | 2.50–2.61 × 3.01 | yes | curved, angled and zero-thickness variants |
| SFC `SM_Bld_Section_Wall_01`–`05` | 5.00 × 3.00 × 5.00–5.44 | yes (2 × 2 cells) | facade shells for stamped ruin templates |
| SFC `SM_Bld_Advanced_01` / `_02` | 5.00 / 10.00 × 3.00 × 0.71 | yes (2 / 4 cells) | flat facade strips |

### Floor — verdict: fits

| Prefab | Size | Snaps? | Note |
|---|---|---|---|
| Gen `SM_Bld_Base_Floor_01` | 2.50 × 0.00 × 2.50 | yes | zero-thickness plane, corner pivot |
| Gen `SM_Bld_Base_Floor_Combined_01` | 2.50 × 0.10 × 2.50 | yes | 0.10 slab, walking surface ≈ pivot height |
| Gen `SM_Bld_Base_Floor_Hole_01` | 2.50 × 0.10 × 2.50 | yes | slab with cut-out — ladder/stair penetration |
| Gen `SM_Bld_Base_Floor_Half_01` / `_Quarter_Combined_01` | 2.50 × 1.25 / 1.25 × 1.25 | half/quarter | fillers |
| SFC `SM_Env_Ground_Tile_Half_01`–`05` | 2.50 × ≤0.27 × 2.50 | yes | exact one-cell street/terrain tiles |
| SFC `SM_Env_Ground_Tile_01`–`09` | 5.00 × ≤0.05 × 5.00 | yes (2 × 2) | street tiles; `_Base_01`–`04` add kerb depth below y=0 |

### Roof — verdict: flat fits; pitched fits-with-offset

"A built roof is the floor above" is directly supported: stamp `SM_Bld_Base_Floor_Combined_01` as the walkable roof surface and `SM_Bld_Base_Ceiling_01` (2.50 × 0.00 × 2.50) as the underside skin.

| Prefab | Size | Snaps? | Note |
|---|---|---|---|
| Gen `SM_Bld_Base_Ceiling_01` / `_Half_01` / `_Quarter_01` / `_45_01` | 2.50 × 0.00 × 2.50 (and halves) | yes | ceiling/roof underside |
| Gen `SM_Bld_Base_Floor_Combined_01` | 2.50 × 0.10 × 2.50 | yes | walkable flat roof |
| Gen `SM_Bld_Base_Roof_Straight_01`, `_Corner_In_01`, `_Corner_Out_01` | 2.50 × 3.25 × 2.50 | fits-with-offset | pitched; 0.25 m taller than the layer — topmost decoration only |
| Gen `SM_Bld_Base_Roof_Half_01` / `_Quarter_01`, caps and trims | 1.25–2.50 | fits-with-offset | pitched-roof fillers and edge caps |
| SFC `SM_Bld_Roof_Pagoda_01` / `_02` | 6.82 × 3.96 × 7.09 / 4.93 × 2.27 × 4.01 | no | set dressing on stamped buildings only |

### Door — verdict: fits

| Prefab | Size | Snaps? | Note |
|---|---|---|---|
| Gen `SM_Bld_Base_Wall_Door_01` / `_Large` / `_Double` / `_Double_Large` | 2.50 × 3.01 × 0.29 | yes | one-cell wall with doorway |
| Gen `SM_Bld_Base_Door_01` / `_Large_01` | 1.00 × 1.97 / 1.13 × 2.47 | leaf | matching swing leafs for the above |
| SFC `SM_Prop_Door_01`–`05` | 1.16–1.92 wide × 2.36–2.59 | leaf | sci-fi leafs (sliding-door look) for the same openings |
| SFC `SM_Bld_Section_Door_01`–`07` | 5.00 × 3.00 × ~5 | yes (2 × 2) | facade entrance shells for stamped templates |

### Window — verdict: fits

| Prefab | Size | Snaps? | Note |
|---|---|---|---|
| Gen `SM_Bld_Base_Wall_Window_01` / `_Double_01` | 2.50 × 3.01 × 0.26 | yes | one-cell window wall |
| Gen `SM_Bld_Base_Wall_Window_Half_01` / `_02` | 2.50 × 1.51 × 0.26 | half-height | sill-height variants |
| SFC `SM_Bld_Section_Window_01`–`04` | 5.00 × 3.00–6.00 × ~5 | yes (2 × 2) | facade shells |

### Stair — verdict: fits (with a placement offset)

The confirmed rule "stairs occupy two cells" is exactly what the kit builds: `SM_Bld_Base_Stairs_01` rises precisely 1.50 m over a 2.5 m run, so two chained pieces climb one full layer (3.0 m) across two cells. Both stair pieces carry a 0.33 m skirt below y=0 (bounds minY = −0.33) that must sink into the floor slab — a fixed per-family placement offset in the stamping script.

| Prefab | Size | Snaps? | Note |
|---|---|---|---|
| Gen `SM_Bld_Base_Stairs_01` | 2.50 × 1.83 × 2.50 (rise 1.50) | yes | half-flight; two per layer climb |
| Gen `SM_Bld_Base_Stairs_02` | 2.50 × 3.33 × 2.50 (rise 3.00) | yes | steep full-layer stair in one cell (optional variant) |
| Gen `SM_Bld_Base_Stair_Half_01` / `_Quarter_01` | 2.50 × 1.25 / 0.62 run | fillers | landings and part-flights |
| Gen `SM_Bld_Base_Stairwell_Wall_01` | 2.50 × 1.50 × 0.23 | yes | raked side wall for the flight |
| WF `SM_Bld_Fort_Stairs_01` | 1.64 × 4.69 × 5.58 | no | wooden, off-grid — ignore |

Sci-Fi City itself contains **no stair pieces at all**.

### Ladder — verdict: fits

| Prefab | Size | Snaps? | Note |
|---|---|---|---|
| Gen `SM_Gen_Bld_Ladder_01` | 0.60 × 3.00 × 0.10 | yes — exactly one layer | corner+base pivot; the only grid ladder in any pack |
| WF `SM_Prop_Ladder_01` / `_02` | 0.71 × 5.55 / 3.35 | no | lean-to wooden ladders — ignore |

One 3.0 m ladder is sufficient (one cell per layer, stackable). Note from `e-02`: no climb animation exists in any pack.

### Pillar — verdict: fits

| Prefab | Size | Snaps? | Note |
|---|---|---|---|
| Gen `SM_Bld_Base_Pillar_01`–`05` | 0.11–0.43 × 3.00–3.02 | yes (height within 0.02) | five thicknesses/styles |
| Gen `SM_Bld_Base_Pillar_Half_01`–`05` | 0.11–0.39 × 1.51 | half-height | |
| Gen `SM_Gen_Bld_Beam_01`–`03` | 0.37 × 0.36 × 5.00 | yes (2-cell horizontal) | exposed I-beams, good for ruins |
| SFC `SM_Env_Railing_01_Pillar` / `_02_Pillar` | 0.40–0.70 × ~1.3 | posts | railing terminators |

### Railing — verdict: fits-with-offset

| Prefab | Size | Snaps? | Note |
|---|---|---|---|
| SFC `SM_Env_Railing_01` / `_02` | 5.00 × ~1.2 × 0.3–0.4 | yes (2-cell run) | the only grid-pitched railings |
| SFC `SM_Prop_Barrier_01`–`03`, `_Plastic_01`–`03` | 1.99–2.29 long | no | free-standing street barriers (props, not edge railings) |
| SFC `SM_Prop_Fence_01` | 1.03 × 2.82 × 2.12 | no | chain-link fence panel |

Native pieces cover even (two-cell) runs; a single-cell run uses `SM_Env_Railing_01` at X-scale 0.5 (a pure extruded profile, scales cleanly) with the matching `_Pillar` posts at cell corners.

### Furniture — verdict: fits, except beds (see Gaps)

| Prefab | Size | Snaps? | Note |
|---|---|---|---|
| SFC `SM_Prop_Bench_01` | 2.50 × 0.46 × 0.60 | yes — exact cell width | |
| SFC `SM_Prop_MarketTable_02` (also `_01`–`05`) | 2.50 × 0.76 × 0.94 | yes | market/work tables |
| SFC `SM_Prop_Couch_01` / `SM_Prop_StripClub_Sofa_01` | 2.42 / 3.05 long | within cell / 1.2 cells | |
| SFC `SM_Prop_BarChair_01`, `SM_Prop_Office_Chair_01`, `SM_Prop_StripClub_Chair_01` | ≤0.7 footprint | sub-cell | seats |
| SFC `SM_Prop_Table_Small_01` | 0.84 × 0.54 × 1.60 | sub-cell | side table |
| SFC `SM_Prop_Medical_Chair_01` / `_Table_01` / `_Machine_01` | up to 1.68 × 2.96 × 2.70 | within cell | hospital bed stand-in + med machine |
| SFC `SM_Prop_Monitor_01`–`03`, `SM_Prop_Security_Monitor_01` | ~1.45 wide | sub-cell | work stations / research bench dressing |
| SFC `SM_Prop_VendingMachine_01` / `_Soda_01` | 0.76–1.91 wide | sub-cell | dispensers |
| Farm `SM_Prop_Bed_01` | 2.08 × 1.52 × 2.67 | fits-with-offset | only real bed in any pack; 0.17 m over the cell tucks into the 0.23 m wall band |

Sci-Fi City has **no bed of any kind** (nearest is the medical chair); Western Frontier's `SM_Prop_CampBed_01` (1.11 × 0.70 × 3.05) is longer than a cell and wooden.

### Power — verdict: infrastructure fits; device scale is a gap

| Prefab | Size | Snaps? | Note |
|---|---|---|---|
| SFC `SM_Bld_Power_01` / `_02` / `_03` | 7.59 × 5.01 × 4.92 → 6.55 × 6.79 × 6.55 | ≈3 × 2 cells, 2 layers | power-station buildings — usable as the colony generator building |
| SFC `SM_Bld_Power_04` | 1.54 × 5.46 × 1.54 | 1 cell × 2 layers | slim exhaust/pylon stack |
| SFC `SM_Prop_Powerpoll_01` / `_02` | ~1.6 × 5.07 | sub-cell base | street power poles |
| SFC `SM_Prop_Cables_01`–`03`, `SM_Prop_Cables_Ceiling_01` | 7.31 / 4.75 spans | fits-with-offset | catenary and ceiling cable dressing |
| Gen `SM_Gen_Bld_Pipe_Straight_01`–`03`, corners, T, cross, valve | 2.53 × 0.38 × 0.38 | fits-with-offset (+0.03 socket) | conduit visual; the 0.03 m is a joint socket overlap |

No generator, battery, solar panel or wind turbine prop exists at cell scale in any owned pack (Farm's `SM_Prop_Windmill_01` and `SM_Bld_WaterTower_01` are rustic and off-grid).

### Light — verdict: gap for placeable lamps

| Prefab | Size | Snaps? | Note |
|---|---|---|---|
| SFC `SM_Prop_LightBar_01` | 1.00 × 0.20 × 5.00 | yes (2-cell, top pivot) | ceiling-hung strip light — the main interior light |
| SFC `SM_Prop_MarketLights_01` / `_02` | 2.58 / 1.80 wide | ≈cell | string lights |
| SFC `SM_Prop_Paper_Lantern_01` / `_02` | 0.38 × 1.02 | hanging | ambience |
| SFC `SM_Sign_Neon_01`–`09` (+ `_Flat`, `_Bulb`) | 0.84–7.50 wide | wall-mounted | emissive signage, decorative glow |

No standing lamp, street light, wall lamp or work light exists in **any** owned pack. For a colony sim's buildable light this is a real hole.

### Storage — verdict: fits

| Prefab | Size | Snaps? | Note |
|---|---|---|---|
| SFC `SM_Prop_Crate_01` / `_02` | 0.85 × 0.73 × 0.85 | sub-cell (3×3 per cell) | canonical stack crate |
| SFC `SM_Prop_Crate_03` / `_04` | 1.46 cube / 2.35 × 1.15 | within cell | large crates |
| SFC `SM_Prop_Crate_Military_01`–`03`, `SM_Prop_Crate_Large_01` | ~1.1 × 0.4 | sub-cell | footlockers |
| SFC `SM_Prop_Box_Empty_01` / `_Supplies_01` / `_RobotParts_01` | ~0.8 | sub-cell | loose stock |
| SFC `SM_Prop_Shelf_01` / `_Preset_01` | 3.28 × 2.22–2.30 × 0.75 | fits-with-offset (1.3 cells) | shelving unit against a 2-cell wall |
| SFC `SM_Prop_Shelf_Wall_*` | 2.03 wide | wall-mounted | |
| SFC `SM_Prop_Dumpster_01` | 2.48 × 1.51 × 1.55 | yes — within cell | |

Stockpile zones need no mesh; crates/boxes are the stack visuals.

### Salvage — verdict: fits

| Prefab | Size | Snaps? | Note |
|---|---|---|---|
| SFC `SM_Prop_RubbishPile_01` | 2.72 × 0.98 × 1.76 | fits-with-offset (≈1 cell) | the cell-scale debris mound |
| SFC `SM_Prop_RubbishPile_02` / `_03` | ≤2.0 | within cell | scatter |
| SFC `SM_Prop_TrashBag_01`–`03`, `SM_Prop_TrashCan_01`, `SM_Prop_Sidewalk_Rubble_01` | ≤0.9 | sub-cell | scatter |
| Gen `SM_Bld_Base_Wall_Destroyed_01` / `_02` | 1.53 / 0.59 × 3.01 | partial by design | ruined-wall salvage states |
| SFC vehicle prefabs (`SM_Veh_*`, 33 pieces) | various | multi-cell | wrecks as salvage nodes |

## Gaps and committed fixes

- **Bed** — use Farm `SM_Prop_Bed_01` (2.08 × 1.52 × 2.67): the only real bed owned, the scavenged-domestic look suits a ruined-city colony, and the 0.17 m overrun hides in the 0.23 m wall band.
- **Standing/wall lamp** — Blender: one 3.0 m lamp post and one small ceiling/wall puck, emissive, mapped to the Synty atlas; no candidate exists in any owned pack.
- **Battery** — Blender: a 1 × 1-cell emissive cabinet; nothing battery-like exists at any scale.
- **Solar panel** — Blender: a flat 2.5 × 2.5 panel (trivially grid-true); no pack has one.
- **Generator** — Synty `SM_Bld_Power_01` as a 3 × 2-cell, two-layer generator building; Synty-first beats modelling one.
- **Single-cell railing** — runtime X-scale 0.5 of `SM_Env_Railing_01` plus its `_Pillar` posts; extruded profile scales cleanly, no new asset needed.
- **Power conduit visual** — Generic pipe set (`SM_Gen_Bld_Pipe_*`, 2.53 m); the 0.03 m socket overlap is absorbed at joints; sim-side conduits stay invisible.

## Layer questions touched

1. **Stairs two cells / ladder one — do the pieces support this?** Yes, exactly. `SM_Bld_Base_Stairs_01` rises precisely 1.50 m over one 2.5 m cell, so two chained pieces climb one 3.0 m layer across two cells — the confirmed rule is the kit's native geometry. `SM_Bld_Base_Stairs_02` additionally offers a steep 3.0 m rise in a single cell if a compact variant is ever wanted. Both carry a 0.33 m skirt below y=0 that must sink into the floor slab (fixed placement offset). `SM_Gen_Bld_Ladder_01` is exactly 3.00 m tall with a base-corner pivot: one cell per layer, stackable.
2. **Roof pieces as floors?** Yes for flat roofs: the pack's flat "roof" is simply `SM_Bld_Base_Floor_Combined_01` (walkable 0.10 slab) over `SM_Bld_Base_Ceiling_01` (underside skin), which matches "a built roof is the floor above" one-to-one, with `SM_Bld_Base_Floor_Hole_01` for ladder/stair penetrations. The pitched `SM_Bld_Base_Roof_*` pieces are 3.25 m tall (0.25 m over a layer) and are not floors — reserve them for decorative topmost caps on stamped ruins.

## Sources

- `D:\code\odyssey\docs\research\synty-inventory.md` (measured report, 2026-09-15)
- `D:\code\odyssey\docs\research\synty-inventory.csv` (per-prefab bounds; all dimensions above)
- Prefab paths cited per row, under `Assets/Synty/PolygonGeneric/Prefabs/{Base,Building,Environment}`, `Assets/Synty/PolygonSciFiCity/Prefabs/{Buildings,Environments,Props,Signs,Vehicles}`, `Assets/Synty/PolygonFarm/Prefabs/Props`, `Assets/Synty/PolygonWesternFrontier/Prefabs/{Buildings,Props}`
- No web sources used (0 of 4 searches; naming was unambiguous)

## Confidence

**High** for every dimension and verdict quoted (taken from the measured CSV, not from names). **Medium** for the negative claims ("no bed / lamp / battery / solar anywhere"): they rest on name-pattern searches of all 2,138 prefabs, and an oddly named prefab could have been missed.

## Could not be determined

- Whether the SFC door leafs (`SM_Prop_Door_01`–`05`) contain articulated child transforms for open/close animation — the CSV records bounds only; check in the editor before building the door Def.
- Whether `SM_Prop_LightBar_01`, lanterns and neon signs ship with actual Unity `Light` components or emissive material only (affects the lighting Def).
- Editor-eye style verdict on Farm `SM_Prop_Bed_01` next to Sci-Fi City interiors — the owner should eyeball it before it becomes the bed.
