# Lane D4 — Layer-aware pathfinding and reachability

## Question

What is the right pathfinding and reachability architecture for a 250 × 250 × 40 cell world (2.5 M cells) with 50 colonists plus 300 animals replanning continuously, where the world is constantly edited by digging and building and vertical movement happens only through discrete connectors (stairs, ladders, later lifts) treated as portals? Slice-critical for M2. Studied clean-room: mechanics, data shapes and published technique only; no RimWorld code, Def contents or decompiled source was read or copied.

## Findings

### 1. Reachability is a different problem from pathing, and must be answered first

The founding observation is Tynan Sylvester's own, from the 2013 development blog, and it is still the load-bearing idea in RimWorld:

> "One of the classic problems with the A* pathfinding algorithm that RimWorld (and nearly every other remotely similar game) uses for pathfinding is its behavior when there is no path from source to destination. It tries to find a path to the destination, and just keeps trying, scanning square after square, until it has covered the entire map and finally realizes there is nowhere else to go."

His fix was not a faster A*, it was to stop calling A* at all for that question:

> "Every walkable square in the map is marked with a zone index number. We use an optimized flood-fill algorithm to go over the map and fill out each walkable region with the same zone index. As long as we keep our region indices updated, all a colonist has to do is check that the region index where they're standing matches the one where they want to go. If they don't, don't try to path."

Two numbers from the same post are useful for sizing: a full regeneration of the connectivity indices took **~5.5 ms on a 200 × 200 map** on 2013 hardware, and he immediately concluded that even that was "not good enough to do every frame" — the maintenance had to become incremental, driven by "changed conditions like walls being built or blown up". That is the whole design in one paragraph: a cheap connectivity structure, kept up to date locally, consulted before any search.

A failed A* is not merely slow, it is the **worst case** of the search: it expands the entire connected component. In our world that is potentially ~2.5 M cells for one query. With 50 agents each asking "is there any steel I can reach?" over hundreds of candidate targets per second, the only affordable answer is an O(1) integer comparison. Everything else in this document is secondary to that.

### 2. What regions actually are (RimWorld's shape, from public documentation)

The RimWorld wiki documents the region rules at mechanic level, and they are worth copying almost exactly:

- "Upon map generation, the map is divided into chunks of **12 × 12 squares** called map regions. Each region requires a **contiguous** area and will be subdivided otherwise. **Under no circumstance will a region extend beyond the initial 12 × 12 grid.**"
- "Impassable buildings (both natural and artificial) are not considered as part of any map region … and will subdivide existing map regions accordingly. Regions will fuse when possible."
- "Impassable terrain creates map regions confined to its extension. These regions don't interact with the regular ones."
- "Passable buildings, such as fences and barricades, create map regions along their extension. These 'fence' regions won't interact with other kinds."
- "**Doors have unique behavior.** Single tile doors create a 3 tile map region centered on the door's tile, following the door's orientation, that overlaps with other map regions … The map regions created by doors don't interact with any other, not even each other."
- Regions are debuggable: "Map regions are used for several calculations and can be seen in game via the debug option 'Draw Regions'."
- Rooms are built *on top of* regions, and the cap leaks into gameplay: "The maximum size of a room is **36 map regions** … The maximum area possible per room is 5,184 tiles (72 × 72)", and above that the space "will be treated as a non-room indoors space."

Four design lessons fall out of this:

1. **The chunk grid caps region size.** A region never spans chunks, so a rebuild is always bounded by one chunk's cell count no matter how open the map is. This is the property that makes incremental maintenance cheap and predictable.
2. **Regions are typed.** Walkable regions, impassable regions, fence regions and door regions exist side by side and do not merge with each other. Typed regions are how special movement rules get expressed structurally rather than as special cases inside the search.
3. **Doors get their own region.** A door is the thing most likely to change state, so isolating it means a door opening or closing perturbs one tiny region and its links, never a room-sized flood.
4. **Regions carry more than connectivity.** RimWorld's per-region indices of what is present are what let a job giver find "the nearest haulable steel" by breadth-first region traversal with early exit, instead of scanning cells. The wiki's crafting-bench folklore ("materials in a different region of the same room are searched region by region") is the player-visible shadow of that mechanism.

Above regions sit two grouping tiers: a **movement grouping** (which regions are mutually reachable — the modern descendant of the 2013 zone index) and a **room grouping** (an enclosed space for temperature, beauty, roof). RimWorld treats them as one family of structures; in a 3D world they must be separated, because "I can walk there" and "the air mixes" stop being the same relation the moment you introduce ladders, hatches and holes in floors.

Two further data points on cost and direction of travel:

- The reachability answer is cached, not recomputed: RimWorld holds a memoised result for group-pair queries under a given set of traversal parameters and drops it when the topology changes. Its error logs surface `RegionTraverser.BreadthFirstTraverse` and `TraverseParms` as the public-facing names, confirming the shape: BFS over the region graph, parameterised by the agent's traversal capabilities.
- In update 1.6 (2025) Ludeon "rewrote [the] pathfinder to be entirely **multithreaded and batched, using Unity's Burst technology**", alongside a Burst rewrite of the glow grid, for a claimed 20–30 % TPS gain in late-game colonies. The region/reachability layer was *not* what got parallelised; the per-request grid search was. That is a direct steer on what belongs in a Burst job and what does not.

### 3. Factorio: the same architecture, arrived at independently, with the dynamic-world problem solved

Factorio's 2019 pathfinder rewrite (FFF #317) is the closest published analogue to our constraints, because Factorio's world is also edited constantly:

- The map is simplified by dividing each **32 × 32 chunk** into *components*: "a component is an area of tiles where a unit can go from any tile within the component to any other within the same component". Each component becomes **one abstract node**.
- Only the perimeter matters: "it is enough to remember the components for the tiles on the perimeter of each chunk", since inter-chunk connectivity depends only on edge tiles.
- Maintenance is local: rather than recalculating the whole simplification, "only … tiles affected by modifications" are reconsidered.
- **Two cooperating searches.** A base (cell-level) A* computes the real route; an abstract (component-level) A* supplies the heuristic. "When [the base pathfinder] creates a new base node, it calls the abstract pathfinder to get the estimate on the distance to the goal." The abstract search runs **backwards from the goal**, "jumping from one chunk's component to another".
- **Reverse Resumable A\***: the abstract search's nodes are kept in memory; "if we need a distance estimate for a base node located in a chunk not yet covered by the abstract search: we resume the abstract search from the nodes we kept." One abstract search therefore amortises across many base-node queries and across agents heading to the same place.
- Their abstraction deliberately **ignores entities and considers only terrain**, "so we don't have to recalculate the simplification each time an entity is added or removed" — a clean statement of the principle that the abstract layer should depend only on the slowest-changing, cheapest-to-verify facts.
- FFF #121 documents the companion caching: a **positive path cache** that allows partial reuse ("go from A to the middle of a previously-found path, follow this pre-existing path for a bit, and then depart it and go to B"), eviction weighted by how expensive the path was to compute, and a **negative cache** for destinations already proven unreachable.
- FFF #425 adds a refinement worth stealing: at chunk level, non-traversable perimeter tiles should also record the distance to the nearest traversable tile, so the abstract layer degrades gracefully near blocked edges.

The convergence is the finding: two shipped colony/factory sims with constantly edited grids, independently, both landed on *chunk-local connected components + boundary links + an abstract search that feeds the concrete one*.

### 4. The hierarchical-pathfinding options, costed

**HPA\* (Botea, Müller, Schaeffer 2004).** Abstracts the map into linked local clusters; at the local level "the optimal distances for crossing each cluster are pre-computed and cached"; at the global level "clusters are traversed in a single big step". Published result: **up to 10× faster than a highly optimised A*, with paths within 1 % of optimal**. Start and goal are inserted into (and removed from) the abstract graph per query. The hierarchy can be extended beyond two levels.

Cost on a constantly edited grid: an edit dirties one cluster, and repairing it means recomputing that cluster's entrances and its intra-cluster all-pairs crossing distances — O(E²) small searches for E entrances, each bounded by the cluster's cell count. **With small clusters this is cheap**; with the 10 × 10 × 1 cluster recommended below and ≤ 8 entrances, that is ≤ 64 searches over ≤ 100 cells, i.e. microseconds. With Dragon-Age-sized 40 × 40 clusters it would not be. The literature's complaint that HPA* "struggles with dynamic obstacles requiring frequent graph updates", and the existence of DHPA*/SHPA* specifically to avoid "dynamically rebuilding clusters", are both really complaints about *large* clusters. **Cluster size is the dial that decides whether HPA* is viable in a destructible world.**

**Portal / region graphs** (what RimWorld and Factorio use) are HPA* with the intra-cluster distance cache made optional and the cluster reused as an indexing structure for everything else (contents, rooms, temperature, sound). The maintenance story is strictly better because the only mandatory per-edit work is a flood-fill of one chunk plus relinking its boundary.

**JPS / JPS+.** JPS prunes symmetric paths on **uniform-cost, 8-connected grids**. Two disqualifiers for us: our terrain is not uniform cost (rubble, mud, damaged flooring all carry different costs, and cost variation is the point of a ruined city), and the pruning rules are defined for grid neighbourhoods — they do not extend to portal edges that jump a layer, so every vertical transition would be a special case that breaks the jumping invariant. JPS+ is worse still: it is an "extreme A* speed optimisation for **static** uniform cost grids" by its own title, its all-pairs precomputation "[takes] over 50 hours for the largest map sets", and the general observation from the JPS literature is that "online approaches are still preferable in dynamic environments … preprocessing-based approaches rely on precomputed auxiliary data structures, which have to be rebuilt or repaired when the map changes". **Reject JPS+ outright; JPS is at best an optional inner-loop optimisation inside a single uniform-cost region, and not for the MVP.**

**Incremental replanning (D\* Lite and family).** Genuinely faster than replanning from scratch when changes are few, localised and far from the goal; the standard caveats are that "replanning is more expensive when changes occur in the nodes closer to the goal" and that "if changes were significant and the graph was considerably altered, planning from scratch might be much more efficient", so implementations commonly abort repair and restart. For us there are two further, decisive objections: D* Lite keeps a **per-agent search tree over the whole graph** (350 agents × a 2.5 M-cell search state is untenable), and persistent per-agent search state is an additional determinism surface that must be saved, loaded and hashed. **Reject D\* Lite.** Repair belongs at the abstract level (patch the region graph); concrete paths are re-planned from scratch, which is cheap precisely because the abstract corridor makes the concrete search local.

### 5. Cross-layer: what Cataclysm DDA proves you must not do

`docs/research/c-cataclysm-dda.md` (Lane C) is the cautionary case, and it is specific. CDDA's levels were generated independently, so staircases on adjacent levels need not align; consequently the pathfinder, on reaching a stairs-down cell, calls the same stair-resolution routine the player uses and then **searches nearest-first within a one-submap radius (12 tiles) for a matching stairs-up cell**, adding that as the successor. Route validation then has to tolerate discontinuities, with the source comment: *"Jumps are acceptable on 1 z-level changes / This is because stairs teleport the player too."* Results, a decade on: NPC and monster vertical navigation has broken repeatedly (issue #80421 is from 2025), and cross-level zone and hauling logic still misbehaves.

The three failure modes to design away, in order:

1. **Run-time connector matching.** If the far end of a stair is *discovered* rather than *declared*, the discovery leaks into pathfinding, AI and player movement, and every bug in it looks like an AI bug.
2. **Tolerated discontinuities in path validation.** Once "a jump of one layer is fine" is in the validator, genuinely broken paths stop being detectable.
3. **Per-storey generation.** Independently generated levels are the root cause of (1). Volume stamping (already our Phase 1 Q3 answer) removes it at source.

What CDDA gets right and we should copy: **packed per-cell path flags** rebuilt from dirty points (ground, obstacle, bashable, door, climbable, stairs-up, stairs-down, ramp-up, ramp-down, air, dangerous) — the pathfinder reads a flag word, never a terrain object; **per-layer scratch buffers** lazily allocated; and **per-layer uniform short-circuits** so empty sky and solid rock cost nothing.

Oxygen Not Included supplies one more concrete pattern, visible through its error messages and modding surface: a **nav grid of explicit per-cell links with a typed transition system** (`NavType` for floor / ladder / pole / tube, `NavGrid.Transition` between them) and a **fixed maximum number of links per cell** — its crash text reads "Out of nav links on grid 'minionnavgrid' at cell 21,152. Needed 49 but maxlinkspercell is 48". The lesson is that typed, pre-built movement links with a hard capacity bound are a shipped, workable model for ladder/pole vertical movement; the accompanying lesson from its bug tracker is that agents will prefer a nearby ladder over an objectively faster tube route unless the cost model is tuned, so **connector costs are gameplay-visible and must be tunable data, not constants in code**.

### 6. Determinism

The formal statement (from a study of game-engine determinism for autonomous-vehicle verification) is precise and matches the folklore: "A* heuristics may not guarantee a uniquely preferred node in the frontier, so ties may be found during selection and then broken arbitrarily, meaning that an implementation of A* can be non-deterministic." The fix is equally precise: "A priority queue is stable if it breaks ties based on insertion order … an unstable priority queue … is still deterministic if it always breaks ties in the same way (based on heap order). An implementation of A* is deterministic if it uses either a stable priority queue or an unstable but deterministic priority queue." Their experiments confirmed that runs with deterministic tie-breaking reproduced and runs without it did not.

The other three practical sources of divergence are floating-point cost arithmetic (the standard lockstep-RTS answer is fixed-point), iteration over unordered containers, and any budget expressed in wall-clock time. On floats specifically, Unity's Burst offers `FloatMode.Deterministic` — "ensure that floating point calculations are deterministic (64-bit only)" — with `Strict` (no floating-point optimisations) as the default and `Fast` explicitly permitting "algebraically equivalent optimizations (which can alter the results of calculations)". The 64-bit-only restriction and the documented non-guarantee of bit-identical NaN payloads are reason enough to keep floats out of the pathfinder entirely rather than to rely on the mode.

## Recommendation

**Build a three-tier, single-abstract-level region-portal graph over a chunked 3D flag grid: chunk-local single-layer regions, explicit typed links (horizontal spans and vertical portals), per-traverse-mode district ids for O(1) reachability, and a two-stage search (region A\* → corridor-constrained cell A\*) with integer costs and node-counted budgets.** In one line: RimWorld's reachability model, Factorio's abstract/base search pairing, HPA*'s cached crossing distances at a deliberately small cluster size, and none of CDDA's run-time stair matching.

### Tier 0 — the cell flag grid

- `NavFlags`: one 16-bit word per cell, in chunked storage. Bits: `Walkable`, `HasFloor`, `Solid`, `Door`, `DoorOpen`, `ConnectorStair`, `ConnectorLadder`, `ConnectorLift`, `Climbable`, `Hazard`, `Roofed`, spare. Plus a parallel byte per cell giving the **terrain move-cost class** (an index into a Def-driven cost table).
- Storage: 2.5 M cells × 3 bytes ≈ 7.5 MB, chunked so that uniform chunks (all air, all solid) reference a shared singleton and allocate nothing. A ruined city is mostly uniform; expect only a small fraction of chunks to be live.
- Nothing in the pathfinder ever reads a building or thing object. Flags are the only input, rebuilt from dirty cells at the start of the tick.

### Tier 1 — regions

- **Chunk = 10 × 10 × 1 cells.** 250 divides exactly by 10, giving **25 × 25 chunks per layer, 25,000 chunks** for 40 layers. A chunk never spans layers.
- A **region** is a maximal connected set of same-kind cells within one chunk. Region kinds: `Walkable`, `Door` (one region per door cell), `Connector` (one region per connector footprint), `Impassable` (kept for room and atmosphere purposes), `Hazard`. **Kinds never merge with each other**, exactly as RimWorld's fence, door and impassable regions do not.
- Region record: id, chunk index, layer, kind, cell bitmask (**100 cells = 2 × `ulong`, exactly**), bounding box, link list, room id, version counter, and a **contents index** (counts and short lists of things bucketed by category, maintained as things move between regions).
- Isolating doors and connectors into their own regions means a door opening or closing, or a ladder being built, perturbs a singleton region and its links, never a chunk-sized flood.
- Expected live region count: 1–4 per non-empty chunk, tens of thousands at worst — three orders of magnitude below the cell count. That ratio is the whole point.

### Tier 2 — links

- **Horizontal link**: a maximal contiguous run of mutually walkable cell pairs along a shared chunk boundary, owned by the boundary (a link database keyed by (edge, span) so both sides find the same object).
- **Vertical portal link**: created **only** by a connector object, which declares its cells explicitly — a stair Def names its two lower cells and its two upper cells; a ladder names one lower and one upper; a lift names its shaft cell per served layer. **There is no run-time search for the far end, ever.** Build validation refuses to place a connector whose declared counterpart cells are obstructed, and mapgen template stamping registers connectors as part of the volume, pre-linked.
- **Fall link**: a one-way downward edge from a walkable cell with no floor to the first supporting cell below. Present in the graph so agents can be made to *avoid* holes (huge cost, forbidden for all normal modes) and so falling and fleeing behaviour is expressible rather than emergent.
- Each link stores, per traverse mode, an `allowed` bit and a traversal cost. Cap links per region (ONI's `maxlinkspercell` lesson: a hard bound, asserted, is better than an unbounded list that surprises you in year three).

### Tier 3 — districts and rooms (two separate groupings)

- **District** = connected component of the region graph **under one traverse mode**. Stored as `int districtId[mode][regionId]` — a handful of arrays of ~50 k ints, well under 1 MB in total.
- **Traverse modes** are a small closed enum fixed at design time (target ≤ 8): `Colonist` (floors, stairs, ladders, doors it may open), `Hauler` (as Colonist, but bulky loads forbid ladders), `Animal` (no ladders, no manipulable doors), `Wild` (additionally respects player doors), `IgnoreDoors` (raiders and bashers), plus reserved slots for flying and mechanical later.
- **`Reachable(from, to, mode)` is `districtId[mode][regionOf(from)] == districtId[mode][regionOf(to)]`.** One array lookup each side and an integer compare. No allocation, no search, no A*. This is the single most important API in the simulation and every job giver must be built on it.
- **Room** = a separate grouping for atmosphere, temperature, light and beauty: an air-connected volume that may span layers where a floor is missing, and that is *not* the same set as a district (a closed hatch you can open passes pawns but not air; an open shaft passes air but not pawns without a ladder). Keep the two groupings in separate arrays over the same regions. This is the 3D divergence CDDA never made and paid for.
- **Reachability cache** for the harder variants (forbidden areas, locked doors, "can reach while carrying X"): a memoised map keyed by (districtA, districtB, mode, variant) → bool, **cleared wholesale** on any topology change. Cheap, because the common case never reaches it.

### When the graph rebuilds (fixed tick order)

The sim tick runs these groups in this order, always:

1. **Edit commit** — apply all built, mined and destroyed cells queued during the previous tick; update `NavFlags`; mark chunks dirty. Edits are applied in a deterministic order (ascending cell index).
2. **Nav rebuild** — for each dirty chunk in ascending chunk index: re-flood its regions, assign ids deterministically (by layer, chunk index, then lowest member cell index), rebuild its boundary links, revalidate portal links whose endpoint cells changed, bump region version counters, invalidate the affected crossing-distance caches, and update contents indices.
3. **District recompute** — if any region or link changed, recompute district ids by BFS over the region graph, starting from the lowest region id, per mode. A full recompute over ~50 k nodes is a graph flood, not a cell flood: expect **hundreds of microseconds**, against Tynan's 5.5 ms for a full *cell* flood on a 200 × 200 map in 2013. Do it at most once per tick, and clear the reachability cache.
4. **AI and job giving** — uses `Reachable` and region BFS scans only.
5. **Path service** — serves queued requests within the node budget.
6. **Movement** — agents consume paths.

No agent ever observes a half-rebuilt graph, and the rebuild cost is paid once per tick regardless of how many edits landed. If a rebuild ever exceeds its budget, the correct response is to **defer the remaining edits to the next tick**, never to skip the rebuild — a stale flag grid is a correctness bug, a one-tick-late wall is not.

### What the pathfinder does per call

1. **O(1) reject**: different district for this mode → fail immediately, no search. Also fail immediately if the goal cell is not enterable for the mode and no adjacency ("touch") goal was requested.
2. **Abstract search**: A* over regions, from the goal backwards (Factorio's reverse-resumable form), edge cost = the cached intra-region crossing distance (the HPA* level-1 edge weight, recomputed only for dirty regions — at most 64 searches over at most 100 cells each) plus the link's own cost. Output: (a) the corridor of regions, (b) a per-region distance-to-goal used as the heuristic below. **Keep the abstract search's node set alive** so other agents heading to the same goal, and later resumes by the same agent, reuse it.
3. **Concrete search**: A* over cells, restricted to the corridor regions plus one region of slack, with `h` = the abstract distance-to-goal of the cell's region plus the in-region offset. If the corridor is exhausted without reaching the goal, widen once; if that fails, fall back to an unconstrained search with the abstract heuristic and a hard node cap.
4. No smoothing for the MVP (movement is cell to cell). The output is a cell list plus the ids and version counters of the regions it passes through.

The abstract heuristic is **knowingly inadmissible by a bounded amount** — paths are near-optimal, not optimal. HPA*'s published figure of within 1 % of optimal at up to 10× the speed is the trade we are accepting, and it is the right trade for a colony sim. It is also the *only* affordable way to get a sane heuristic across layers: a straight 3D distance heuristic is catastrophically misleading when the goal is directly below you and the nearest stair is sixty cells away, and it is exactly that case that a ruined multi-storey city produces constantly.

### Cost model (integers only)

**One cost unit = 1/100 of the time an unencumbered colonist takes to cross one flat, clear cell orthogonally.** Every cost is an `int`; there is no float anywhere in the pathfinder.

| Move | Geometry | Cost (default, Def-tunable) |
|---|---|---|
| Orthogonal step | 2.5 m | 100 |
| Diagonal step | 3.54 m | 141 (forbidden when both flanking cells block) |
| Terrain modifiers | — | additive: rubble +30, mud +60, damaged floor +15 |
| Terrain modifiers, **as built** (2026-09-16) | — | marsh +40, shallow water +200; deep water is impassable, not priced |
| Stair, up | 2 cells + 3.0 m rise | ~290 |
| Stair, down | 2 cells + 3.0 m drop | ~230 |
| Ladder, up | 1 cell + 3.0 m rise | ~540 (includes a discomfort premium so stairs win when both exist) |
| Ladder, down | 1 cell + 3.0 m drop | ~400 |
| Door (openable) | 1 cell | 100 + opening time |
| Fall edge | downward, one-way | 100000 (effectively forbidden outside flee and collapse modes) |
| Lift | shaft | wait + travel, **quantised once per tick** |

Rules: derive the connector numbers from climb speed and height so they stay comparable to terrain costs when either is tuned; expose them as Def data (ONI's ladder-versus-tube bug is a cost-tuning bug, and it must be fixable without a recompile); make the lift's dynamic cost a **per-tick snapshot**, read-only during pathing, so every agent in a tick sees the same value.

### Budgets

Assume 60 sim ticks per second at 1×, 180/s at 3×, 60 FPS on the 2022 mid-range laptop target: roughly 5.5 ms of wall clock per tick for *everything*. Allocate:

- **Path service ≈ 0.6 ms/tick**, expressed not in milliseconds but as a **node budget**: 20,000 cell-node expansions per tick across all requests, with any single request capped at 6,000 expansions before it is suspended and resumed next tick (its search state is kept; the agent keeps walking its old path or waits). Abstract searches are budgeted separately at 2,000 region-node expansions per tick.
- **Requests form a FIFO queue, drained in insertion order, ties broken by ascending agent id.** Budgets are integer counters, never timers. This is non-negotiable for determinism and it also makes the frame cost flat rather than spiky.
- **Nav rebuild ≈ 0.5 ms/tick** for dirty chunks plus one district recompute.
- **Replans are event-driven, never periodic-per-tick.** Triggers: the next cell became unwalkable; the goal was invalidated or claimed by someone else; any region on my stored corridor bumped its version counter (one integer compare per region on the path, done lazily as the agent crosses into a new region); plus a failsafe revalidation every 120 ticks.
- **Sharing.** (a) Reverse-resumable abstract searches are cached per (goal region, mode) with an LRU and invalidated on topology change — twenty haulers bound for one stockpile share one abstract search. (b) A concrete positive path cache keyed by (mode, start region, goal cell), with partial reuse in Factorio's style: join an existing path in the middle. (c) Negative caching is free — the district compare *is* the negative cache.
- **Job scanning never iterates cells**: `TryFindNearestReachable(predicate, mode, maxRegionsVisited)` does a BFS over regions from the agent's region, consulting each region's contents index, stopping at the first region with a candidate (plus one ring of slack for correctness of "nearest"), with a hard region-visit cap. This is what makes 50 agents scanning continuously affordable, and it is the second most important API after `Reachable`.

### Burst versus plain C#

RimWorld 1.6's split is the right one and we should copy it: **the per-request grid search was parallelised and Bursted; the region and reachability layer was not.**

- **Plain C#, single-threaded, in the tick**: region rebuild scheduling, district recompute, all reachability queries, region BFS content scans, request scheduling, path consumption — everything AI-visible. These are pointer-chasing graph operations over tens of thousands of nodes; they are already cheap, and they are where determinism bugs would be most expensive.
- **Candidates for Burst, only once the D1 benchmark or a profile proves the need**: (1) the concrete cell A* inner loop over the packed flag array, as a **batched fork-join** — gather up to K independent requests, run `IJobParallelFor`, complete within the same tick, write each result into its own slot, apply results in **request-index order**; (2) per-chunk region re-flood, one job per dirty chunk, merged in ascending chunk order. Both are embarrassingly parallel with no shared mutable state, which is the only kind of parallelism that survives a determinism gate.
- **Never in a job**: anything that reads or writes the district arrays, the reachability cache, or region contents.
- Burst rules: integer costs only; fixed-capacity `NativeArray`s and no reliance on `NativeParallelHashMap` iteration order; no `Unity.Mathematics` transcendentals in the cost model; if a float ever appears, `FloatMode.Strict` (the default) or `Deterministic`, never `Fast`.

### Determinism rules (the checklist the M2 gate tests)

1. **Integer costs everywhere.** No floats in flags, costs, heuristics or budgets.
2. **A total order in the open list.** Compare `(f, then h, then cellIndex)`. Never rely on heap internals or insertion order to break a tie; never leave a tie unbroken.
3. **Fixed neighbour order.** A compile-time array of offsets — eight horizontal, then the portal edges of the current cell in ascending link id. Never iterate a hash container to generate successors.
4. **Deterministic ids.** Region ids assigned by (layer, chunk index, lowest member cell index); recycled ids taken lowest-first; district floods start from the lowest region id and proceed in ascending order.
5. **Budgets in nodes, never in milliseconds.** A wall-clock budget makes the simulation depend on the machine.
6. **Fixed request order.** FIFO, ties by ascending agent id; results applied in request-index order; parallelism only as fork-join over independent requests.
7. **Stateless tie-break jitter.** If cost jitter is wanted to break up conga lines, derive it from `hash(cellIndex, worldSeed)`, not from a stateful RNG draw whose order depends on how many agents happened to path this tick.
8. **Snapshot dynamic costs.** Lift positions, congestion and door states are read from a per-tick snapshot taken before the path service runs.
9. **Tests.** Same-seed/same-hash over 10,000 ticks with 50 agents pathing; a replayed edit stream (build and mine script) producing an identical region-graph hash; a golden-master headless day; and a **path checksum** (sum of cell indices) folded into the state hash, so a divergent path is caught at the tick it happens rather than ten thousand ticks later.

### Rejected, with reasons

- **JPS / JPS+** — needs uniform costs, does not extend to portal edges, and JPS+ precomputation is explicitly unsuited to destructible worlds.
- **D\* Lite / incremental per-agent repair** — per-agent search state over 2.5 M cells × 350 agents, an extra determinism surface to save and hash, and the literature's own caveat that large changes make from-scratch replanning faster. Repair at the abstract level instead.
- **Multi-level HPA\* (three or more tiers)** — 25 × 25 chunks per layer is small enough that one abstract level suffices; add a second level only if the abstract search is ever measured to be the bottleneck.
- **Flow fields over cells** — the right answer for hundreds of units converging on one goal in an RTS, the wrong shape here: our agents have many different goals, and a 2.5 M-cell field per goal is unaffordable. The region-level distance field from the cached abstract search gives most of the benefit for a thousandth of the memory.
- **A single global district id (no traverse modes)** — tempting and wrong: animals that cannot climb ladders, and haulers who cannot carry a bulky load up one, are exactly the interesting cases in a vertical colony.

### Build order for M2

1. `NavFlags` chunked grid, deterministic cell and chunk indexing, strongly typed coordinates (CDDA's coordinate-chaos lesson). Tests: flag rebuild from edits; chunk uniformity short-circuit.
2. Region flood per chunk, typed regions, deterministic ids. Tests: golden region layout for a fixed stamped map; rebuild-after-edit equals rebuild-from-scratch.
3. Links — horizontal spans, then portals, then fall edges — and connector Defs that declare their cells. Tests: a stamped building's stair core links on stamp; building or destroying a ladder adds or removes exactly one link pair.
4. Districts per mode, `Reachable`, the reachability cache. Tests: a sealed room is unreachable; removing one wall cell makes it reachable within the same tick; a ladder-only shaft is reachable for `Colonist` and not for `Animal`.
5. Region contents index and `TryFindNearestReachable`. Tests: nearest-candidate correctness against a brute-force scan on a small map.
6. Abstract A*, corridor-constrained concrete A*, and the budgeted request service. Tests: path validity (every consecutive pair is a real graph edge — **no tolerated discontinuities**), near-optimality within a bound against brute-force A* on small maps, the determinism harness.
7. Caches (resumable abstract, positive path, LRU) — last, and only with the determinism harness already green, because caches are where reproducibility goes to die.

## 3D/layer impact

- **Regions are strictly single-layer.** Vertical connectivity exists *only* as portal links. This is the structural guarantee the brief asks for, and it has a large practical payoff: a layer's regions rebuild without touching any other layer, except for revalidating the handful of portal links that terminate in the edited chunk.
- **The abstract search earns its keep in 3D specifically.** Horizontal A* with a Euclidean heuristic degrades gracefully; vertical A* with a 3D heuristic does not, because the heuristic points straight through a floor at a goal only reachable via a stair on the far side of the building. The region-level search discovers the stair before the cell-level search wastes a single expansion.
- **Forty layers is only affordable with uniformity short-circuits.** Most chunks in a ruined-city column are all-air or all-solid; those allocate no regions at all and are skipped by the district flood. Budget for a live region count in the low tens of thousands, not the theoretical 50 k-plus.
- **Rooms must be 3D volumes while districts are movement components.** Do not let one structure serve both, or the first mezzanine or broken floor will force the retrofit CDDA spent a decade on.
- **Falls are graph edges, not physics surprises.** A missing floor produces an explicit one-way downward edge with a prohibitive cost. Agents therefore route around holes by construction, and "flee off the edge" or "the collapse drops you" become deliberate mode flags rather than emergent bugs.
- **Portal capacity is a 3D-only problem.** Ladders are one cell wide: two agents meeting on one is a deadlock with no horizontal equivalent (horizontally you can step around). Ladders are **single-occupancy reservable segments with a deterministic queue**; stairs, being two cells, permit two occupants. Congestion enters the cost model from a previous-tick occupancy snapshot.
- **Lifts, later, are portals with a dynamic cost**, not a new mechanism: a shaft registers a portal link per served layer, and the cost is (wait for car + travel), quantised once per tick. Designing the portal edge to carry a per-tick cost now means lifts need no new architecture in M3 and beyond.

## Ruined-city impact

- **Digging and building are the normal case, not the exception.** The architecture is chosen for exactly that: the mandatory per-edit cost is one 100-cell flood plus boundary relinking, and everything expensive (crossing-distance caches, abstract search nodes, path caches) is derived data that is invalidated rather than maintained.
- **Rubble is cost, not obstacle.** A ruined city's interest comes from cost variation, which is precisely what disqualifies JPS-family pruning and what makes the integer additive cost table a first-class piece of design data.
- **Collapsed floors are the signature feature**: they create fall edges, light shafts (Lane D3's concern) and air connections between rooms, all from the same `HasFloor` bit. One flag, three systems — provided rooms and districts are separate groupings from the start.
- **Stamped ruin templates arrive pre-linked.** Because volumes are stamped whole (Phase 1 Q3), a freshly generated block already has a valid portal graph — no post-stamp stair-matching pass, which is precisely the pass CDDA had to write and still fights.
- **Mining a floor does not create a portal.** It creates a hole: a fall edge down and no way up. The colonist must then build a ladder, which registers the portal. That is a good gameplay loop, and it falls out of the data model rather than needing rules.
- **Sealed-off areas are common** (collapsed stairwells, blocked doors), which is exactly the scenario that makes the O(1) reachability answer mandatory rather than merely nice: a ruined city is full of places that look close and are not reachable at all.

## Layer questions touched

**Question 1 — the vertical movement model (answered, committed):**

- Stairs occupy two cells on the lower layer and two on the upper, declared explicitly by the Def; ladders occupy one and one; lifts occupy a shaft cell per served layer. **The connector declares both ends. Nothing is matched at run time.**
- Each connector registers a **typed portal link** between the region containing its lower cells and the region containing its upper cells. Regions never span layers; portals are the only vertical edges, alongside one-way fall edges.
- Traversal is **per traverse mode**: colonists use stairs and ladders; haulers with bulky loads use stairs only; animals use stairs only (or neither, by species); later flying and mechanical modes get their own bits on the same links. The mode's district array answers "can this creature get up there?" in O(1).
- Costs are integers derived from climb time (stairs ~290 up / ~230 down, ladders ~540 / ~400, plus a discomfort premium so stairs win where both exist), Def-tunable, and comparable with terrain costs.
- **Occupancy**: ladders are single-occupancy with a deterministic queue; stairs allow two. Congestion is priced from a per-tick occupancy snapshot.
- **Validation forbids discontinuity**: a path step must be a real graph edge. There is no "a one-layer jump is acceptable" tolerance, anywhere, ever.
- Lifts need no new architecture — a portal link whose cost is recomputed once per tick.

**Questions 4 (light to lower layers) and 5 (3D line of sight)** are touched only through the shared `HasFloor` bit: the same flag that makes a cell a fall edge makes it a light and sight aperture. Keep one authoritative floor bit per cell, consumed by nav, light and LOS alike.

**Question 6 (zones per layer or volumes)**: the room/district separation recommended here is the mechanism that lets zones be 3D volumes without breaking movement connectivity. CDDA's per-level zones caused years of cross-level hauling bugs; our zones should be sets of cells validated against district reachability at assignment time.

**Question 10 (the propagation unit for gas, fire and sound)**: rooms — the atmosphere grouping, not the district grouping — are the natural propagation unit, with portals and missing floors as the exchange channels. That grouping has to exist for this architecture anyway, so the marginal cost of doing it right is small.

## Sources

Read directly:

- https://ludeon.com/blog/2013/07/reachability-at-last/ — Tynan Sylvester's own account of why A* cannot answer reachability, the flood-filled zone index, and the 5.5 ms / 200 × 200 full-regeneration figure.
- https://rimworldwiki.com/wiki/Rooms — "Map regions" section: 12 × 12 chunking, contiguity, subdivision by impassables, fusing, typed fence and impassable regions, the door-region special case, the 36-region room cap, the "Draw Regions" debug view.
- https://factorio.com/blog/post/fff-317 — chunk components as abstract nodes, perimeter-only connectivity, local re-simplification on change, base/abstract pathfinder pairing, reverse resumable A*, an abstraction that ignores entities.
- https://factorio.com/blog/post/fff-121 — positive path cache with mid-path joining and difficulty-weighted eviction; negative caching of unreachable destinations.
- https://docs.google.com/document/d/e/2PACX-1vRCjqVtPQDFGu4POiKTUd_8o3U2Asdhx99SOvcgU66ABdYtk3Cgndd53yJ6BC4tZX530pp_m6lf4Z9P/pub — RimWorld 1.6 public changelog: "Rewrote pathfinder to be entirely multithreaded and batched, using Unity's burst technology."
- https://docs.unity3d.com/Packages/com.unity.burst@1.8/api/Unity.Burst.FloatMode.html — `Default` / `Strict` / `Deterministic` (64-bit only) / `Fast` semantics.
- `docs/research/c-cataclysm-dda.md` (this repo, Lane C) — run-time stair matching, the 12-tile nearest-match search, the tolerated one-z-level discontinuity, packed per-cell path flags, per-level scratch layers, and the resulting decade of vertical-navigation bugs.

Consulted via search-result summaries only (claims attributed, not independently read in full):

- https://cdn.aaai.org/ojs/12397/12397-52-15925-1-2-20201228.pdf and https://ojs.aaai.org/index.php/AIIDE/article/download/12397/12256/15925 — DHPA* and SHPA*, motivated by avoiding dynamic cluster rebuilds in HPA*. (The PDF would not text-extract within the read cap.)
- https://www.semanticscholar.org/paper/Near-Optimal-Hierarchical-Path-Finding-Botea-M%C3%BCller/b0f0432ba69e4d730b93a75e3d19c8e9d811efac and https://citeseerx.ist.psu.edu/document?doi=b0f0432ba69e4d730b93a75e3d19c8e9d811efac — Botea, Müller & Schaeffer, *Near Optimal Hierarchical Path-Finding*: clusters, pre-computed intra-cluster crossing distances, up to 10× faster than optimised A* with paths within 1 % of optimal.
- http://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter14_JPS_Plus_An_Extreme_A_Star_Speed_Optimization_for_Static_Uniform_Cost_Grids.pdf — JPS+ speed-up range (up to two orders of magnitude on open maps, around 2.5× on maze-like maps) and its explicit "static uniform cost grids" scope.
- https://arxiv.org/html/2306.15928 — "online approaches are still preferable in dynamic environments … preprocessing-based approaches rely on precomputed auxiliary data structures, which have to be rebuilt or repaired when the map changes"; precomputation of over 50 hours for the largest map sets.
- https://en.wikipedia.org/wiki/Jump_point_search
- https://cdn.aaai.org/AAAI/2002/AAAI02-072.pdf (Koenig & Likhachev, D* Lite) and https://arxiv.org/html/2606.03735v1 — incremental replanning gains, and the standard caveat that from-scratch replanning wins when changes are large or near the goal.
- https://arxiv.org/pdf/2104.06262 — A* non-determinism from arbitrary tie-breaking; the stable-or-deterministic priority queue rule; experimental confirmation.
- https://forum.arongranberg.com/t/pathfinding-for-deterministic-simulation/53 — practitioner statement that floats are unsuitable for deterministic lockstep simulation, and that fixed-point types are used instead.
- https://forums.kleientertainment.com/klei-bug-tracker/oni/about-the-middle-of-the-cycle-my-game-crashes-i-have-encounterred-out-of-nav-links-on-grid-minionnavgrid-at-cell-21152-needed-49-but-maxlinkspercell-is-48-i-disabled-all-mods-and-i-still-get-a-crash-at-the-same-point-in-the-cycle-just-before-half-r53161 — ONI's per-cell nav-link array with a hard `maxlinkspercell` bound.
- https://forums.kleientertainment.com/klei-bug-tracker/oni/transit-tube-pathfinding-r21382/ and https://steamcommunity.com/app/457140/discussions/0/1290691937717736978/ — ONI's typed nav transitions (ladder, pole, tube) and agents preferring a nearby ladder over a faster route, i.e. connector costs are a tuning problem.
- https://factorio.com/blog/post/fff-425 — non-traversable perimeter tiles recording the distance to the nearest traversable tile in chunk-level abstraction.
- https://ludeon.com/blog/2025/06/announcing-odyssey-and-update-1-6/ — the 20–30 % late-game TPS claim accompanying the 1.6 multithreading work.

## Confidence

**High** on the core recommendation — reachability answered by a cached connectivity grouping rather than by A*, regions as chunk-local connected components with typed links, and a two-stage abstract-then-concrete search. Two shipped games with constantly edited grids converged on it independently, one of them (RimWorld) with the developer's own published rationale, and the structure follows from first principles about A*'s failure case.

**High** on the RimWorld region rules quoted (12 × 12 chunking, contiguity, typed regions, door regions, the 36-region room cap): the wiki documents them at mechanic level and they are player-observable via the in-game debug view. **High** on the Factorio mechanism: read directly from the developers' own posts.

**High** on the determinism rules: they follow from a published formal statement of the tie-breaking problem plus Unity's own documentation of Burst float modes, and they are cheap to enforce and directly testable.

**Medium** on the specific numbers in this document — the 10 × 10 × 1 chunk size, the 20,000-node tick budget, the connector costs, the sub-millisecond district-recompute estimate. These are derived from the map dimensions, the cited precedents and back-of-envelope reasoning, not measured. They are starting points for the D1 benchmark and the M2 gate to correct, and the architecture does not depend on any of them being right. **The region-count half of that list was corrected on 2026-09-17** — see `## Measured 2026-09-17`, where the 10 × 10 chunk size turns out to be the thing that bounds the count and the uniformity argument turns out not to be. The node budget, the connector costs and the district-recompute estimate remain unmeasured.

**Medium** on the claim that HPA*-style cached intra-region crossing distances remain affordable under constant editing: the reasoning (small clusters bound the repair cost) is sound and the literature's objections are about large clusters, but no published measurement at a 10 × 10 cluster size on an actively edited 3D grid was found. This is why the build order puts that cache last, behind the determinism harness.

**Low to medium** on the exact internal naming and tiering of RimWorld's modern grouping structures above regions. The 2013 connectivity index and the region/room relationship are documented publicly; the precise present-day split between the movement grouping and the room grouping was not confirmable from a clean-room-permissible source, and the clean-room rule forbade reading the obvious one. This does not affect the recommendation, which argues for separating the two groupings on 3D grounds regardless of what RimWorld does.

## Could not be determined

- The exact structure and invalidation policy of RimWorld's present-day reachability cache, and the tiering above regions, from clean-room-permissible sources. Error-log fragments confirm BFS region traversal parameterised by traversal capabilities, but not the cache's key, size or eviction policy.
- The measured cost of RimWorld's region and reachability maintenance on a large late-game colony. The 2013 figure (5.5 ms for a full cell-level regeneration on 200 × 200) is the only public number found, and it predates the incremental system it motivated.
- Quantitative results from the DHPA*/SHPA* paper: the PDF would not text-extract within the read cap, so its measured update costs and path-quality figures are unverified here. Worth one targeted follow-up if a second abstract level is ever considered.
- Whether RimWorld 1.6's batched Burst pathfinder preserves cross-run reproducibility (RimWorld has no determinism gate, so the question may simply not arise for them). Our fork-join-with-ordered-application design does not depend on the answer.
- ~~The real distribution of live (non-uniform) chunks in a stamped ruined-city map at our dimensions — needed to turn the region-count estimate from an upper bound into a budget. This is measurable as soon as the mapgen slice exists and should be recorded in the M2 report.~~ **Answered 2026-09-17 (`OQ-18`), and it corrected the claim it was meant to confirm** — see `## Measured 2026-09-17`. The city is 35.0% live blocks and the natural map 92.1%, because all-solid rock allocates an impassable region per block rather than nothing. The region budget holds on both maps; the uniformity short-circuit credited for it does not exist on the wilderness map.
- Whether a Burst-compiled cell A* is needed at all at our agent count, or whether plain C# with the abstract heuristic and node budgets suffices. That is precisely a D1 benchmark question; the architecture deliberately keeps the search behind a boundary so the answer can change without redesign.

## Measured 2026-09-17

The numbers in this file were reasoned from the map dimensions, not measured, and the Confidence
section says so. Both generators and the region graph now exist, so `OQ-18` measured them.
`NavGraphStatisticsTests` (fast tier, `Category("Long")`) builds each map at the scale target and
walks the graph through its public API. Seed 4242, 250 x 250 x 40, .NET 8 CoreCLR on an AMD Ryzen 7
9800X3D. Every figure below is printed by that test on every run, so this section can be checked
rather than trusted.

| | natural | ruined city |
|---|---|---|
| live regions | 24,141 | 23,240 |
| — walkable | 1,649 | 8,103 |
| — connector | 0 | 903 |
| — impassable | 22,492 | 14,234 |
| links | 4,820 | 20,761 |
| — span / portal / fall | 1,769 / 1,495 / 1,556 | 6,758 / 2,595 / 11,408 |
| districts (colonist) | 37 | 3,291 |
| districts (hauler, animal) | 37 | 3,628 |
| connectors registered | 0 of 0 | 465 of 733 |
| blocks live of 25,000 | 23,031 (92.1%) | 8,746 (35.0%) |
| cells inside a region | 90.4% | 28.6% |
| mean / largest region | 93.6 / 100 | 30.8 / 100 |
| generation | 212 ms | 289 ms |
| full nav rebuild | 168 ms | 124 ms |

**The budget was right and the reason given for it was wrong.** "Budget for a live region count in
the low tens of thousands, not the theoretical 50 k-plus" holds on both maps: 24,141 and 23,240.
But the mechanism credited above — "Most chunks in a ruined-city column are all-air or all-solid;
those allocate no regions at all" — is true of the city (65% of blocks are uniform) and **false of
the natural map, where 92.1% of blocks are live.** All-solid is precisely the case that *does*
allocate here, because `RegionKind.Impassable` regions are kept on purpose so rooms and atmosphere
have a substrate. Underground rock therefore fills every block with exactly one region, and the
natural map's region count is very nearly its live-block count.

**What actually bounds the count is structural, not statistical.** A region is a connected part of
one 10 x 10 block of one layer, so the largest region on either map is exactly 100 cells, and the
floor is one region per live block. The worst case is therefore blocks x layers — 625 x 40 = 25,000
— plus however many extra components complex geometry splits out, and both maps land near it from
below. The estimate would have been right whatever the generators did, which is not the same as
having been right for the stated reason.

**The search space is much smaller than the graph.** Impassable regions carry no links and are
excluded from the district flood, so what an abstract search actually traverses is the walkable and
connector regions: **1,649 of 24,141 on the natural map (6.8%) and 9,006 of 23,240 on the city
(38.8%).** The hierarchical search this file recommends is therefore cheaper than the region totals
suggest — worth knowing before the top technical risk is attacked, since 65% of the measured tick
is A-star.

**Falls are the city's dominant edge, by a wide margin**: 11,408 of 20,761 links, 55%. "Collapsed
floors are the signature feature" now has a number behind it. The natural map's link graph is a
quarter the size and evenly split between the three kinds.

**Traverse mode changes connectivity on the city and not on the wilderness.** Colonists and
door-ignoring raiders see 3,291 districts; haulers and animals, which may not use ladders, see
3,628 — **337 more components, one per place reachable only by ladder.** On the natural map all
four modes see 37, because it has no ladders and no doors. The per-mode district array is doing
real work rather than holding four copies of one answer.

**A full rebuild at the scale target costs 168 ms (natural) and 124 ms (city).** That is the
worst case — every block dirty — and not a per-tick figure; `NavigationSystem` maintains the graph
incrementally inside the tick. It sits against the only public RimWorld number found (5.5 ms for a
full cell-level regeneration on 200 x 200 in 2013) on a board with 40 times the cells.

**The wilderness is not one connected place**: 37 districts on a map with no buildings, which is
the sealed caverns and isolated ledges the natural generator makes. Worth remembering when a
colonist is asked to reach an ore seam.

**Two structural guarantees are now checked rather than asserted in prose.** Over all 2.5 million
cells of each generated board, no region spans two layers, and no region outgrows its block. The
first is the guarantee every layer-local claim in this file rests on.
