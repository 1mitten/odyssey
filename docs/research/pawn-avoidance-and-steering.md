# Pawn collision, dynamic path cost, and visual steering in grid colony sims

## Question
How do reference grid colony sims (RimWorld, Dwarf Fortress, Stonehearth, Going Medieval) handle multi-pawn cell occupancy, dynamic obstacle costs during pathfinding, and sub-tile visual lateral steering/manoeuvring around obstacles (other colonists, items, trees)?

## Findings

### 1. Multi-Pawn Tile Occupancy and Dynamic Path Cost in RimWorld & Prior Art

#### RimWorld: Mechanics and Architectural Trade-Offs
- **Soft Occupancy Outside Combat:** In vanilla RimWorld, moving pawns do not have mutual physical collision outside combat. Colonists pass completely through one another in corridors, doorways, and rooms without stopping, waiting, or re-routing. Multiple pawns can simultaneously occupy and traverse the same tile.
- **Combat State Collision:** Hard tile occupancy is activated conditionally during combat states (drafted colonists, hostile encounters, social fights). Stationary drafted pawns physically occupy their cell against hostiles, enabling the classic "melee block" (three colonists standing abreast behind a single-cell doorway to bottleneck multiple incoming enemies 1-on-1). Friendly pawns can still step through each other to reach their posts, but hostile enemies cannot enter or push through an occupied tile.
- **Absence of Dynamic Pawn Path Cost:** Vanilla pathfinding does **not** dynamically add path costs for cells occupied by other friendly moving pawns (e.g. there is no dynamic `PawnPathCost` modifying the A* grid per frame). Pathfinding evaluates only terrain move speed, static building path costs (e.g. doors, barricades, sandbags), fire hazards, and the static raider `AvoidanceGrid` (a coarse heat-map of player turrets/fire lines).
- **Trade-Offs of Omitting Dynamic Pawn Costs:**
  1. *CPU Scalability & Cache Coherence:* In colonies with 20–100 pawns, updating dynamic costs for every moving pawn would dirty the path grid every tick, invalidating region connectivity caches and preventing path re-use.
  2. *Avoidance Oscillation & Thrashing:* If occupied cells carried high dynamic cost, two pawns meeting in a narrow corridor would both attempt to detour into adjoining rooms, oscillating their paths when the other pawn moves.
  3. *Doorway Jamming:* Door traversal latency is amortised in vanilla because a door remains propped open briefly; without soft collision, pawns streaming through a door would back up exponentially.
- **Mod Ecosystem Evidence:** Popular mods such as *Clean Pathfinding 2*, *Path Avoid*, and *Pathfinding Framework* do not add dynamic pawn-on-pawn steering. Instead, they introduce *static or zoned* path-cost biases (e.g. assigning artificial costs to freezers, bedrooms, or unpaved dirt) specifically because real-time dynamic obstacle costs in A* degrade tick rates (TPS) severely.

#### Dwarf Fortress: Discrete In-Cell Conflict & Traffic Designations
- **In-Cell Crawling Penalty:** Dwarf Fortress permits multiple dwarves in a single 1×1×1 tile (~2 m × 2 m × 3 m), but imposes an in-cell interaction penalty. When two dwarves meet head-on in a 1-tile corridor, one dwarf is forced to "lie down" and crawl under/over the other. Crawling severely reduces movement speed.
- **Traffic Painting as Path Bias:** Rather than dynamic agent avoidance, Dwarf Fortress provides four static traffic priority tiers (High, Normal, Low, Restricted), which directly scale the A* edge weights (e.g. High = 1, Normal = 2, Low = 5, Restricted = 25). Players use this to designate dedicated two-lane highways or prevent dwarves from shortcutting through private bedrooms.

---

### 2. Sub-Tile Visual Steering and Lateral Manoeuvring in Corridors and Around Obstacles

#### How Shipped Colony Sims Handle Visual Pawn Interactions
- **RimWorld & Dwarf Fortress:** Neither game uses sub-tile steering, Bezier splines, or lateral offsets.
  - In RimWorld, pawn positions are strictly interpolated linearly from cell centre to cell centre (`drawPos = Lerp(from, to, t)`). When pawns pass in a 1-tile corridor, their sprites overlap directly, relying purely on depth/layer sorting.
  - Dwarf Fortress uses discrete tile glyphs/sprites with no sub-tile visual deviation.
- **Going Medieval & Timberborn (3D Grid Sims):**
  - Both maintain a discrete simulation grid for navigation and path validation, but decouple visual rendering.
  - **Corner Rounding (Filleting):** Rather than pivoting 90° instantly at cell centres, presentation models use waypoint corner filleting (typically quadratic Bezier or Catmull-Rom splines cutting corners by 15–25% of cell width).
  - **No Sub-Tile Physics Simulation:** Neither implements continuous local avoidance physics (such as RVO2 / ORCA or steering force vectors) inside the simulation tick. Doing so would destroy grid determinism, break cell reservations, complicate vertical traversals, and incur prohibitive collision-detection costs.

#### Techniques for Lateral Offsets in 3D Grid Presentation
1. **Presentation-Side Lateral Push (Right-Hand Rule):**
   - The simulation path remains strictly cell-to-cell.
   - When the presentation layer detects two opposing pawns within a proximity threshold (e.g. in adjacent cells or passing in the same cell), it applies an orthogonal lateral displacement $\vec{o} = \pm \delta \cdot \vec{d}_\perp$ (where $\vec{d}_\perp = (d_z, 0, -d_x)$ is the right normal to travel direction).
   - $\delta$ ramps smoothly up to ~0.4–0.6 m and back down using a cubic Hermite / smoothstep curve over the traversal.
2. **Sub-Tile Obstacle Avoidance (Trees, Items, Stumps):**
   - Trees and item piles are discrete entities occupying cells. When a cell has ground clutter (e.g. an item heap or tree base) that is passable but visually bulky, games apply a pre-calculated sub-tile visual anchor offset (e.g. keeping pawns 0.3–0.5 m off-centre towards the clear edge of the tile).

---

## Recommendation for Odyssey (2.5 m × 2.5 m × 3.0 m Grid)

Odyssey’s cell dimensions (2.5 m × 2.5 m) provide a distinct architectural advantage: a single corridor cell is physically wide enough for two or three adult humans to pass abreast without clipping.

1. **Simulation: Soft Avoidance with Small Dynamic Bias**
   - Pawns retain soft collision (they can share cells up to 4 passing, or 2 standing).
   - Avoid aggressive dynamic cost grid dirtying. A small transient path penalty (+20 to +40, compared to `MoveCost.Cardinal = 100`) can be applied during concrete A* expansion if another pawn is standing in that cell, gently guiding unconstrained path requests to take an adjacent open corridor without invalidating macro regions.
   - Connector occupancy remains discrete: ladders are single-occupant with FIFO reservation; stairs allow two occupants.

2. **Presentation: View-Side Lateral Passing in `PawnPose.Of`**
   - Implement lateral lane shifts entirely within `Odyssey.Presentation` (`PawnPose` / `PawnFigureDirector`).
   - When a pawn is moving, check for oncoming or standing pawns or static cell clutter (items, trees).
   - Apply a lateral offset: $\vec{x}_{\text{drawn}} = \vec{x}_{\text{interpolated}} + \text{smoothstep}(t) \cdot w \cdot \hat{n}_{\text{right}}$, where $w \approx 0.6\text{ m}$ (well within the $\pm 1.25\text{m}$ tile clearance).
   - Mutual right-hand rule ensures both oncoming pawns steer to their own right, passing each other cleanly without clipping.
   - Zero footprint in `Odyssey.Sim`, zero save/load dependencies, and zero impact on the state hash.

3. **Presentation: Corner Filleting on Paths**
   - When a pawn's upcoming path sequence makes a 90° turn ($C_{i-1} \to C_i \to C_{i+1}$), fillet the presentation waypoint across the corner by cutting toward the corner bisector by 0.4–0.6 m. This eliminates the mechanical "stop-and-pivot" look.

---

## Sources
- https://ludeon.com/blog/2013/07/reachability-at-last/ — Tynan Sylvester on RimWorld's region flood-fill, A* worst-case limitations, and spatial indexing.
- https://rimworldwiki.com/wiki/Rooms — RimWorld region chunking, building obstruction, and door handling.
- https://dwarffortresswiki.org/index.php/DF2014:Path — Dwarf Fortress A* implementation, traffic cost weights (High/Normal/Low/Restricted), and corridor congestion.
- https://dwarffortresswiki.org/index.php/DF2014:Tile — Dwarf Fortress physical cell dimensions (~2 m × 2 m × 3 m) and pass-through crawling behaviour.
- https://factorio.com/blog/post/fff-317 — Factorio's hierarchical pathfinding and static abstraction that deliberately ignores dynamic entities.
- https://steamcommunity.com/sharedfiles/filedetails/?id=2409562852 — *Clean Pathfinding 2* architecture and rationale for avoiding dynamic path costs.
- https://steamcommunity.com/sharedfiles/filedetails/?id=1180719857 — *Path Avoid* design overview (static traffic biases vs dynamic avoidance).
- https://www.gamedeveloper.com/programming/how-tarn-adams-upgraded-and-optimized-dwarf-fortress-for-its-official-steam-release — Tarn Adams on performance, representation vs per-agent search overhead, and pathfinding memory patterns.

---

## Confidence
- **High:** RimWorld's omission of dynamic pawn path costs in vanilla; soft-collision during non-combat movement; combat-state melee blocking; lack of visual lateral steering in RimWorld and Dwarf Fortress; Dwarf Fortress crawling mechanics and traffic cost weights; Factorio entity-free abstraction.
- **Medium-High:** The visual presentation techniques (right-hand rule lateral offsets and corner filleting) utilized in 3D grid colony sims (Going Medieval, Timberborn).
- **Medium:** The exact internal threshold values used by path-avoidance modders in RimWorld.

---

## Could Not Be Determined
- Whether RimWorld 1.6's Unity Burst pathfinding rewrite added any internal micro-optimisations for transient crowd repulsion. The public changelog indicates batched multithreading for the grid search without mentioning crowd avoidance.
- Specific internal math of Going Medieval's exact corner spline tangent weights (closed-source Unity commercial release; inferred from visual observation and standard navigation filleting practice).
