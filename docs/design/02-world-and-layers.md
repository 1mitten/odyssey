# 02 — World, cells and layers

The cell model, the layer model, structural support and collapse, vertical connectors, and ruined-city worldgen. Everything that touches a cell is defined here, and every system that touches a cell must be layer-aware from its first commit (brief §2, non-negotiable 2).

Fixed by ADR 0002: **one cell = 2.5 m × 2.5 m footprint × 3.0 m height**. Scale target: **250 × 250 cells × ~40 layers** = 2.5 million cells, a district roughly 625 m square.

## 1. Coordinates

A cell is addressed by integers `(x, z, y)`: `x` and `z` are the horizontal axes, **`y` is the layer**. Unity is a Y-up engine, so `y` maps to world height and `z` to world depth; the simulation never speaks in metres.

World position of a cell corner: `(x * 2.5, y * 3.0, z * 2.5)`.

Flat index, fixed everywhere including the benchmark and the save format:

```
index = (y * 250 + z) * 250 + x
```

This orders memory layer by layer, then row by row. It is chosen deliberately: almost every hot loop (room flood fill, region build, overlay meshing, light, the camera slice) works *within one layer*, and this layout makes a layer a contiguous 62,500-cell span.

Layer numbering: **layer 0 is street level.** Above ground is positive, below ground negative in *discussion* — but storage uses an unsigned index with a fixed offset, so `y = 0` in the array is the deepest layer. The offset lives in one constant; no system computes it twice.

## 2. What a cell holds

Structure-of-arrays, not an array of structs: systems touch one field across many cells far more often than many fields of one cell, and SoA keeps those scans in cache. It is also what makes a Burst job over a field straightforward.

| Field | Type | Meaning |
|---|---|---|
| `floor` | `ushort` | Def index of the slab at the cell's **bottom** boundary; 0 = open (a hole) |
| `floorMaterial` | `ushort` | Def index of the stuff the slab is built from |
| `edifice` | `int` | Handle of the wall, door or pillar occupying the cell; -1 = none |
| `terrain` | `ushort` | Def index of the natural material — rock, fill, soil, pavement, rubble |
| `flags` | `byte` | Bit field: reserved, forbidden, enclosed, supported-dirty, and so on |
| `region` | `ushort` | Region id for reachability (system 18) |
| `support` | `byte` | Cached structural support value (§4) |
| `temp` | `short` | Temperature — M4; present in the layout from the start so it is never retrofitted |

**A slab is stored once.** The ceiling of cell `(x, z, y)` *is* the floor of cell `(x, z, y+1)`. Storing it on the upper cell removes an entire class of "which of the two cells owns it" bugs, and makes "is this cell roofed?" a single lookup one layer up.

At eight to twelve bytes per cell that is 20–30 MB for the full map, which is nothing on the target hardware. Density beats cleverness here: a dense array has no lookup indirection and no allocation during play.

**Deferred but designed for:** Cataclysm DDA gets a large win from short-circuiting chunks that are uniform (`c-cataclysm-dda.md`), and most of a ruined-city map is uniform rock below and uniform air above. The chunk grid in §3 is where that optimisation would land; the slice does not need it, and the flat array must stay the fallback so correctness never depends on the optimisation.

## 3. Chunks

A **chunk is 25 × 25 cells within a single layer** — 100 chunks per layer, 4,000 for a full map. Chunks are not a storage division in the slice; they are the unit of *dirty tracking* and *work batching*:

- Rendering: one instanced draw batch per (chunk, mesh, material) — see `e-04-tint-strategy.md`.
- Overlays: one procedural mesh per chunk per layer, rebuilt when the chunk is dirty. Never one UI element per cell; 62,500 elements per layer would be fatal, and the UI session's plan says the same.
- Regions: region rebuilds are scoped to the dirty chunk and its neighbours.
- Saving: chunk-level dirty flags allow an incremental save later.

A chunk is single-layer on purpose. A vertical chunk would couple layers that are otherwise independent, and the slice camera only ever draws a handful of layers.

## 4. Structural support and collapse

This is the project's largest departure from RimWorld and the thing the slice most needs to prove.

RimWorld roofs are massless annotations: a roof cell is supported if a roof-holding edifice stands within 6 cells, and removing the last support collapses the section (`a-04-building-and-materials.md`). That model works because nothing ever stands on a roof. Ours must carry colonists, furniture and walls, so:

**A roof is a floor is a slab.** One thing, built of a material, at the boundary between two layers.

### The support rule

Support is an integer per cell, computed bottom-up, in the shape Going Medieval uses and players find legible (`b-going-medieval.md`):

1. A slab resting directly on solid ground, natural rock, or a wall or pillar in the cell below has support **S_max** (a material constant; 4 for early materials).
2. Otherwise a slab takes the **highest support among its four horizontal neighbours on the same boundary, minus one**.
3. Support 0 means unsupported: it cannot be built, and if it becomes 0 it collapses.

This yields the intuitive behaviour that a slab may span up to `S_max` cells from anything holding it up, that a pillar in the middle of a wide room extends the reachable area, and that the rule composes without anyone needing to understand it formally.

**Support values are visible.** Going Medieval hides them and its players over-engineer defensively; our build preview colours each cell by its support value and highlights the cells that a proposed deconstruction would orphan. That is a small UI cost for a large comprehension win, and it is the sort of thing that is nearly free to build now and expensive to retrofit.

### Collapse

When a slab reaches support 0 it collapses, and collapse **cascades**: every slab that depended on it is re-evaluated in the same tick, breadth-first, so pulling out the wrong pillar brings down a section rather than a single tile. Everything standing on a collapsing slab falls one layer and takes damage scaled by the fall. The cell below fills with rubble, which is then minable — so a collapse leaves a mess to clear, not a clean hole.

Recomputation is incremental. A build or mine marks the affected cells `supported-dirty`; a solver pass walks only the dirty frontier, in index order, and stops when values stop changing. A full-map recompute exists for load and for tests, and is the oracle the incremental path is tested against.

### Pre-existing ruined shells — the interesting case

Worldgen stamps buildings that are already standing, so their slabs must begin life supported. They are marked **supported by construction** at generation time and then validated by the ordinary rule the moment anything changes beneath them. The consequence is the setting's central engineering tension: a colonist mines a wall for salvage, and the floor above the next room quietly loses its last support. That is the ruined city being genuinely dangerous, and it falls out of the model rather than being scripted.

Worldgen must also guarantee that every stamped shell is *initially* consistent, or the first tick collapses the map. The generation pass ends with a full support solve and an assertion; a template that fails it is a content bug caught in a test, not at runtime.

## 5. Vertical connectors

Vertical movement happens **only** through connectors. There are no slopes, no half-heights and no free climbing.

| Connector | Footprint | Connects | Notes |
|---|---|---|---|
| Stair | two adjacent cells on layer *y* | `(x, z, y)` ↔ `(x', z', y+1)` | The Synty kit is natively this shape: each `SM_Bld_Base_Stairs` piece rises 1.5 m over one cell, so two pieces climb exactly one 3.0 m layer (`e-01-module-mapping.md`). Free to traverse; the default. |
| Ladder | one cell | `(x, z, y)` ↔ `(x, z, y+1)` | Slower, and carrying a large stack costs extra. Cheap to build, so it is the early-game answer. |
| Lift | one cell, multi-layer | any two connected layers | Post-slice. Requires power. |
| Hole | one cell | `(x, z, y)` ↔ `(x, z, y+1)` | A missing slab. Passable **downward only**, as a fall, with damage. Ruins are full of these and they are how a colonist gets into trouble. |

A connector occupies its cells and registers a **portal edge** in the region graph (`d-04-pathfinding.md`). Nothing else creates vertical adjacency: two vertically adjacent open cells with an intact slab between them are not connected, and with no slab between them they are connected downward only.

The slab under a stair or ladder is **required** — a connector cannot hang in air — and deconstructing it is blocked while the connector stands.

## 6. Worldgen: the ruined city

Ordered generation passes, in the shape RimWorld uses but with the wilderness ratio inverted: the city is the skeleton and nature is the intrusion (`a-12-map-generation.md`).

1. **District and street grid.** Lay a street grid first, with block sizes drawn from a small distribution. Streets are the map's circulation and the player's mental map.
2. **Plots.** Subdivide blocks into plots and choose a building template per plot, respecting plot size.
3. **Stamp shells.** Instantiate templates: walls, slabs, stairs, doors, windows, interior partitions. Templates are authored in cells, not metres, and carry their own vertical extent.
4. **Damage pass.** Apply damage by a per-district intensity: remove wall segments, punch holes in slabs, convert cells to rubble, topple sections. This pass is what makes each ruin unique from a handful of templates, and it is the cheapest variety in the whole generator.
5. **Intactness grid.** A noise field driving surface material: intact pavement, cracked pavement, rubble, soil breaking through. This is RimWorld's fertility grid doing a different job — and it is what later decides where anything can be grown.
6. **Underground strata.** L−1 service stratum (utility tunnels, basements, drains); L−2 and L−3 metro and buried-city seam, carrying the richest salvage; L−4 to L−6 engineered fill grading into soil; below that natural rock with mineral lumps and caves.
7. **Salvage deposits.** Scatter lumps in the shape RimWorld scatters ore — wrecks, buried containers, collapsed machine rooms — weighted toward the buried-city seam.
8. **Utility taps.** A small number of still-live connection points, the early power source, in place of geothermal vents.
9. **Sealed vaults.** A rare, high-value, high-risk pre-placed danger in place of ancient dangers. Post-slice content, generated but inert in the slice.
10. **Start.** Choose a start location, scatter starting resources, run the **full support solve** and assert consistency.

**What digging means** (layer question 11) is resolved by what is being dug: breaching a slab is quick, clearing rubble and reusing an existing void is moderate, mining natural rock is slow. Salvage replaces ore as the material reward, so a colony expands by *emptying the city*, not by tunnelling into a mountain — which is the setting's whole economic argument.

## 7. Map size in the slice

The slice runs a **much smaller map than the scale target**: roughly 60 × 60 cells across five layers (one service layer below, street level, three storeys up). The architecture is sized for 250 × 250 × 40 and the benchmark proves it at that size, but the slice has nothing to gain from a large map and everything to gain from a ten-day unattended run finishing quickly. The map size is a Def constant, not a compile-time assumption, and one test runs the generator at full scale to keep the large path honest.

## 8. What this document leaves open

- **Zones and stockpiles: per layer, or 3D volumes?** Layer question 6, answered in `a-14-bills-stockpiles-inventory.md`; the answer lands in `05-ai-and-jobs.md` rather than here.
- **`S_max` per material, fall-damage scaling, and rubble yields.** Tuning constants, to be set from play and exposed as Defs.
- **Whether the chunk-uniform optimisation is needed at all.** Deferred until a profile says so.
- **Water.** Not in the brief's core scope; the strata model leaves room for it at L−1 without committing.
