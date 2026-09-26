# 62 — Deep mining

**Status: designed 2026-09-26, nothing built; the plan waits for the owner's approval.** Branch
`claude/intelligent-davinci-rbnjm7`. Interview `docs/research/deep-mining-interview.md` (sixteen
answers), research `a-12-ore-by-depth.md` and `a-04-cave-ins-and-prospecting.md`, plan
`docs/plans/deep-mining.md`. It builds on `docs/research/mining-interview.md` (the 2026-09-16 mining
MVP), design 17 (floors and collapse), design 58 (cracks) and `28-map-size.md` §2, §8.5, §11.

## 1. What this is

Mining made worth doing and worth going deep for, without the depth costing the frame or the tick:

- a **32-layer board**, with the rock nobody has opened costing nothing to process;
- **copper, gold, gems and Emberquartz** beside iron and coal, placed in depth bands, with a **deep
  stone** that is slower to cut and **larger cave systems** in the deep bands;
- **fog**: a sealed cavern and undiscovered ore are drawn, picked and described as plain rock;
- a **prospecting job** that reveals ore a few cells into a face;
- **cave-ins** that warn first and down but never kill, held off by uncut rock, walls and a **mine
  prop**;
- a **smelter** that burns coal or wood and turns iron and copper ore into bars the colony builds with;
- soft ground reads **Dig**, and a Mine drag that starts on rock marks only rock.

**Stairs (U44) are the prerequisite for any of it to pay**, because a hauler cannot climb a ladder
(`Connector.cs:84-90`). They are their own unit and their own design.

## 2. The performance model: why depth can be nearly free

### 2a. What depth costs today (measured, `28-map-size.md` §11)

| Board | 16 layers | 20 | 24 |
|---|---|---|---|
| Standard memory | 17.8 MiB | 21.9 | 26.2 |
| Standard regions / links | 2,008 / 1,931 | 2,590 / 1,946 | 3,166 / 1,946 |
| Standard edit tick | 0.553 ms | 0.569 | 0.510 |
| Huge memory | 70.2 MiB | 87.1 | 104.0 |
| Huge regions | 7,880 | 10,195 | 12,500 |
| Huge edit tick | 1.476 ms | 1.484 | 1.616 |

The tick at rest is ~0.02 ms at every depth; save size is flat (the 25 × 25 × 5 palette chunks
compress uniform rock); the frame's draw calls and chunks are identical or falling with depth. Relief
costs the frame, depth does not. **Links are flat while regions grow**: the extra regions are rock.

### 2b. Where the depth-scaled cost is

`NavGraph.FloodBlock` (`NavGraph.cs:482-543`) gives every non-air cell a region, so a solid 10 × 10
block of rock becomes one `RegionKind.Impassable` region (`NavGrid.KindOf`, `NavGrid.cs:506-520`).
Those regions are never linked (`TryPair`, `TryJumpEdge`, `TryHopEdge`, `TryFallEdge` all refuse them)
and never seed a district, but they sit inside every O(regions) pass `Rebuild` runs on each edit
(portal edges, the CSR adjacency, districts per traverse mode, the layer-change estimate). At the scale
target (250 × 250 × 40) that is 24,141 regions and 1.15 ms a mined cell.

**Every consumer already copes with a cell that has no region**, because open air has always had
none: the link builders test `NoRegion` first, `Reachable`/`DistrictOfCell` return false or −1,
`PathFinder` refuses a negative, and the "rooms and atmosphere substrate" the enum comment names has no
consumer in `Sim`.

### 2c. The rule

**Rock nobody has opened is never touched.** Not unloaded — the simulation cannot unload state, and
§8.5 of design 28 rejected streaming — but no per-tick or per-edit pass visits it:

| System | Scales with | After this design |
|---|---|---|
| Navigation rebuild | regions, today including rock | **solid terrain gets no region** (DM1) |
| Drawing underground | every layer down to 0 (`SliceSettings.LowestDrawnLayer` returns 0 below the surface) | **a fixed depth below the slice** (DM1) |
| Door list | every cell on any version move (`DoorDirector.EnsureDoorList`) | **the chunks whose version moved** (DM1) |
| Support | the dirty frontier | unchanged; cave-ins extend the same frontier (§8) |
| Sky columns | a 3 × 3 patch per edited column, stopping at the first solid cell | unchanged |
| Enclosure | per dirty layer; rock is a boundary skipped cheaply | unchanged |
| Reveal | nothing today | a flood fill **on a breach only** (§6); a radius on a prospect (§7) |
| Temperature | standing edifices | unchanged |

So after DM1, **a 32-layer board should cost memory and generation time and nothing else**, and DM1's
gate is that it does: the edit tick at 32 layers is no worse than today's at 16.

### 2d. Memory

The simulation is ~70–84 bytes a cell across its owners, plus ~18–20 for the render mirror. At 32
layers: Standard ~36 MiB, Large ~80, Huge ~140 MiB of simulation (about 190 with the mirror) — under
the ~400 × 400 × 40 ceiling `28-map-size.md` set for an 8 GB laptop. **Measured, not taken from this
paragraph, in DM2.** The owner may cap Huge lower if the laptop budget asks for it (§11).

### 2e. Alternatives considered

- **A separate deep level generated on first breach.** The clear "threshold" moment the brief floated,
  but it needs a second world or lazily materialised layers, a second save section and travel between
  them — and it buys nothing §2c does not, because unopened rock is already nearly free once it has no
  region. Rejected by the owner (Q1).
- **A deep drill building** (the reference game's answer). Adds no digging; recorded as the later way
  to reach ore **below bedrock** once the dug layers run out.
- **Skipping only all-solid blocks.** Proposed in `28-map-size.md`; skipping every solid cell is
  simpler (one test in `FloodBlock`) and deterministic per block. DM1 starts with solid *terrain*
  only and leaves edifices (`NavFlags.Blocked`) as they are unless the audit shows nothing reads their
  regions.

## 3. Code seams (read-only survey, 2026-09-26)

| Concern | Where | Note |
|---|---|---|
| Region flood | `Sim/Pathing/NavGraph.cs:500-512` | `if (kind == RegionKind.None) continue;` is the line that grows to skip solid terrain; `NavGraphStatisticsTests` (tally by kind, :270) and the hash (`ContributeTo`) move |
| Underground draw depth | `SliceSettings.cs:256-258` | return `max(0, active − undergroundDepth)`, ~7 (0.68⁷ ≈ 0.07, where `ShadeBelow` floors at 0.08); every director reads it |
| Door rescan | `DoorDirector.cs:227-241` | keep the last version scanned; rescan chunks with a newer `ChunkVersion`; `RefreshAll` still rescans once |
| Board layers | `Hud/MenuDirector.cs:151-157`, `Sim.Contracts/Sites.cs:144,183`, `OdysseyBootstrap.cs:61,875-879` | three places, no single constant |
| Ground layer | `NaturalMapGenDef.cs:436` | `SizeY − 1 − headroom − relief`; extra layers go underneath, no change |
| Strata | `NaturalSurfacePasses.cs:61-65, 135-152` | a deep-stone boundary beside `BedrockTopY` |
| Rock tests | `CavernPass.CanHollow` (:239), `OrePass.RockBeside` (:351), `GrowBlob` (:369) | `== TerrainRock` becomes a rock-like test |
| Ore table | `NaturalContent.OreTable` (:470-474) **and** `Ores.xml` + `WorldContent.OreOrder` | two owners; collapse to the XML |
| Yield | `MineJob.cs:464-494` | one branch per kind today; becomes a table |
| Terrain order | `WorldContent.TerrainOrder`, `NaturalContent.TerrainCount` | append new terrains after Marsh (index 21) |
| Discovered | `CellGrid.cs:84-100, 309-332`; `WorldRenderModel.cs:1652-1666` | one-way latch; presentation already draws undiscovered ore as Rock |
| **Leak** | `CellDetailContributor.cs:64,98,227` → `InspectModel.cs:1051` | the inspect pane publishes undiscovered ore's real terrain and work — **a bug today** |
| Flags | `CellFlags` bits 0–7 all taken; the mirror stores a byte (`WorldRenderModel.cs:1608`) | an unseen marker needs its own storage (§6) |
| Support | `SupportSolver.cs:40` (`DefaultMaxSupport = 4`), :474 (slabs only drop), :569-578 (`Collapse`) | rock already carries a correct `Support` value; only the drop test and a rock constant are missing |
| Damage | `CombatSystem.Health.cs:26` (`Hurt`), `HealthDef.cs:37-41` (`HitSet`: Melee, Fall) | a new top-of-body set |
| Bills | `Hud/BillsModel.cs:121-131`, `Sim/Cooking/Kitchen.cs`, `RecipeDef` (`PawnContent.cs:740+`) | food-shaped; a smelter needs ingredient lists |
| Stairs | `Connector.cs:79-83` (`ConnectorKind.Stair` allows haulers); `Buildings.xml:148` ("deliberately NOT here yet") | the nav half exists, the building does not |

## 4. Dig and Mine (DM4)

- A grass tile is a whole 3 m block of soil with grass on its top face; digging it removes the block.
  It stays diggable: it is the only way to sink a shaft on flat meadow. Ore only replaces rock, at
  least three layers down under two of subsoil, so no deposit can be a soft tile.
- **Words**: an order on soft ground (grass, bare earth, sand, gravel, subsoil — anything solid that
  is not rock-like) reads **Dig** in the cursor and on the pane; on rock or ore it reads **Mine**. The
  rule and the job are unchanged; only the label differs. New registry keys, labels through
  `Registry.Label`.
- **The drag**: a Mine drag whose **start cell** is rock-like marks only rock-like cells; one that
  starts on soft ground marks everything the tool can. Decided once for the drag, never per cell
  (`docs/bug-patterns.md` P4). A single click marks the cell clicked.

## 5. Strata and minerals (DM2, DM3)

### 5a. The column

On a 32-layer board, top to bottom: 3 layers of sky headroom, the surface (±relief), 2 of subsoil,
**rock**, then **deep stone** from about 14 layers below the local surface, then 2 of bedrock. About
20 layers of rock and deep stone under a valley.

### 5b. Deep stone

A new terrain, **working name "deep stone"** (the owner names it in the wiki), about **2× rock's
work** (rock 700, so ~1,400; bedrock stays 2,400 and unmineable). It yields stone like rock. It is the
"harder with depth" answer (Q8), and it makes the mining skill's steep curve matter down there.

### 5c. The bands

"Band" is layers below the local surface. Tuned in DM3 against the census, not fixed here.

| Mineral | Band | Shape | Cells | Per 10k columns | Yield a cell |
|---|---|---|---|---|---|
| Copper ore | 1–8 | round blobs, may cross 2 layers | 10–30 | 60 | 15 |
| Iron ore | 3–13 | winding veins 1–2 wide | 15–40 | 45 | 15 |
| Coal | 7–20 | flat single-layer seams | 25–60 | 35 | 12 |
| Gold ore | 12–20 | small clusters, favouring deep stone | 2–8 | 12 | 6 |
| Gems | 10–20 | pockets, favouring cave walls | 1–4 | 6 | 2 |
| Emberquartz | 18–20 only | one oval cluster | 20–40 | 2–3 | 20 |

- About 5 % of deposits placed outside their band.
- 30–50 % of gold and gem deposits touch a cave wall, so a cave is worth opening.
- Emberquartz **glows** once discovered, as the existing ore glow does, in its own warm hue.
- Every change to the weights reshuffles every seeded map, so the goldens re-bake once, in DM3, with
  `GoldenColonyProbe` showing what moved.

### 5d. Caverns

The 6–20-cell sealed pockets become **multi-layer chambers of 300–1,500 cells, 3–5 per 10k columns,
in two deep bands** (around 11–12 and 17–18 below the surface), their depth drawn inside the band
rather than always mid-rock (`CavernPass` :168-171). The walk is changed so chambers are round rather
than stringy. **Every ceiling cell must be within the cave-in span of a support** when generated
(pillars left standing), or the first full support solve would collapse them (§8). Still sealed, no
entrance, no creatures (Q7).

### 5e. Content

Terrains, items and names go through the usual gates: `icon-keys.csv` rows for the new `ui.res.*`
(copper ore, gold ore, gems, Emberquartz, iron bar, copper bar), `ui.terrain.*` (the ore terrains,
deep stone), `ui.arch.tool.*` (prospect, the prop, the smelter) and the Dig/Mine labels;
`icon-map.csv` gap rows; the wiki and `Registry.g.cs` rebuilt; the three `--check` gates.

## 6. Fog (DM5)

- **Drawn as plain rock** (Q10). Undiscovered ore is already drawn as rock
  (`WorldRenderModel.cs:1652-1666`). What is missing is the sealed cavern: its air cells are drawn as
  air today, so scrolling down shows it.
- **Storage**: `CellFlags` has no free bit and widening it costs a byte a cell. An **Unseen bitset**,
  one bit a cell (1/8 byte), set by `CavernPass` for every carved cell, saved as its own section and
  hashed while non-empty. A board with no caverns carries nothing.
- **Reveal on breach**: when a cut opens a cell next to an unseen one, flood-fill the connected unseen
  cells, clear them, discover their solid neighbours, and dirty their chunks — once per cavern, the
  reference game's reveal-on-breach.
- **Drawing**: `CopyCell` draws an unseen cell as rock (terrain rock, `SolidTerrain` set in the
  mirror), so the mesher, the picker and the landscape measure all agree without each learning the
  rule.
- **The inspect pane leak is fixed here**: an undiscovered ore cell and an unseen cell are described
  as rock, with rock's work (`CellDetailContributor`). A test holds the pane to never naming
  undiscovered ore.
- **Designation**: an order on an undiscovered or unseen cell is allowed (the player sees rock) and
  behaves as mining that cell; nothing about the order leaks what it is.

## 7. Prospecting (DM6)

- A **Prospect** order on an exposed face (a cell with `Discovered` set, i.e. a cut face). A miner
  walks to it and works a few seconds (Mining skill, trains it lightly).
- On completion every solid cell within **radius 3** (Chebyshev, the same layer and one above and
  below) is marked `Discovered`; the radius grows by one per skill band (tuned in the unit). Revealed
  ore shows its glow; unseen cavern cells in the radius are **not** revealed (a prospect reads rock,
  not voids — the breach does that).
- Cost: a bounded radius, once per job. Nothing per tick.
- **Later**: a powered, operated scanner revealing ore in a wider radius over time, into the same
  latch; and a "follow the vein" order.

## 8. Cave-ins (DM7)

- **Rule**: the existing frontier. Rock already counts as load-bearing (`IsMedium`) and grounded rock
  as a source (`IsGrounded`). Add a **rock-specific maximum** so a dug ceiling holds within **about 6
  cells** of a support (the slab maximum of 4 would drop anything over 3), and extend the drop test at
  :474 to "solid rock, not grounded, support 0". `Collapse` clears the cell and re-queues it so a
  cascade resolves on the next tick. Supports are uncut rock reaching the ground, walls, and the
  **mine prop**.
- **Rubble lands and counts as support**, so a collapse cannot chain across a mine.
- **Warning** (Q11): a cell one step from failing is **unsafe**, published and drawn with a tint, with
  an alert naming it; colonists prefer not to stand under it. The Mine order's cursor previews the
  cells a cut would make unsafe.
- **Harm**: through `CombatSystem.Hurt` only, a new top-of-body `HitSet`, **capped so it downs and
  never kills** — the combat rule (design 33 §3). Things standing on the fallen cell go through
  `Falling.OutOf` as today.
- **Mine prop**: a one-cell, cheap timber or stone building that counts as a support. Passable or not
  is decided in the unit (a prop you walk round is the reference's shape; a passable one is kinder).
- **Existing saves**: a colony that dug a wide room before this rule would cave in on its first load.
  **Recommendation**: the full solve on load marks such cells unsafe and warns, but only a *new* edit
  can drop them. The owner confirms (§11).

## 9. The smelter (DM8)

- A station with a **fuel hopper taking coal or wood** (coal the better: fewer units a bar), on the
  generator's hopper pattern; bills through the existing bill list (`BillsModel.Stations` gets a row).
- `RecipeDef` is food-shaped (nutrition, meat, burn chance) and `Kitchen` is built round it. The
  smelter gets **ingredient lists on a general recipe and a sibling work giver**, rather than bending
  the kitchen.
- Recipes: iron ore → **iron bar**, copper ore → **copper bar**. Gold, gems and Emberquartz are not
  smelted in this build; they are stored (Q13).
- **Where the bars go** is decided in the unit with the owner: the current candidates are copper bars
  for power lines, iron bars for generators, heaters and the smelter itself, each alongside the scrap
  metal they take today.
- A Smelting work type, or Crafting if one exists by then; the skill is decided with the work type.

## 10. Recorded, not built

A mine lift (Q5), the powered scanner (Q9), cave creatures (Q7), heat at depth (Q8), uses for gold,
gems and Emberquartz (Q13), soil as a yield (Q12), a deep drill for ore below bedrock (Q1), and a
"follow the vein" order.

## 11. For the owner

1. The deep stone's name.
2. Whether Huge goes to 32 layers or stays shallower for the laptop's memory (DM2 measures it).
3. Whether an old save's wide rooms are grandfathered (§8, recommended) or cave in on load.
4. Where iron and copper bars are spent (§9).
5. Whether the mine prop is passable.

## 12. Measurements owed

Each with a control in the same run, per `docs/process.md`:

- DM1: regions, edit tick and memory at 16, 24 and 32 layers on Standard and Huge, before and after;
  the frame with the camera ten layers down, before and after.
- DM2: memory, generation time and the frame on all four boards at 32.
- DM3: the ore census per band per seed against §5c.
- DM7: the support pass on a mine of 1,000 dug cells against the same mine with the rule off.
