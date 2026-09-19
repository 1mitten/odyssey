# Plan: 8-Directional Diagonal Movement and Navigation

**Governing Brief:** `docs/brief.md`  
**Worktree:** `D:\code\odyssey-diagonal` on branch `claude/diagonal-movement`  
**Fast Tier Baseline:** 736 Sim + 445 Hud passed (100% green)  

---

## 1. Context & Motivation

Colonists currently move exclusively along the 4 cardinal directions (North, South, East, West). When travelling diagonally across an open meadow or room, they must step in rigid 90° right angles or stair-step zigzags.

In the interview, the owner aligned on:
1. **Full 8-Directional Grid Simulation**: Pawns pathfind, navigate, and step in 8 directions (4 cardinal + 4 diagonals) with $\approx 1.41\times$ distance cost.
2. **Strict Corner-Cutting Prevention (RimWorld rule)**: Both flanking orthogonal cells must be passable. A colonist cannot cut diagonally around a solid wall or rock corner, eliminating visual clipping through buildings and preventing squeezing through diagonal wall cracks.
3. **Orthogonal Hops**: Vertical hops (jumping up +1 or dropping down -1 layer) remain cardinal into neighbouring columns; diagonal moves are strictly horizontal on the same layer.
4. **Action Stances**: Colonists can work diagonally at any adjacent tile (mining, felling, building, harvesting). When orthogonal and diagonal options are equidistant, square-on faces are preferred for neatness.
5. **Terrain & Penalty Scaling**: Base cost is 141 (vs 100), and terrain friction / penalties scale proportionally by $1.41\times$ to reflect 41% longer physical distance through the cell.
6. **Animation**: Colonists rotate smoothly towards the diagonal heading (45°) and stride forward with their natural walk/run cycle.

---

## 2. Unit Breakdown

### Unit 1: NavGrid & Movement Costs (`NavGrid.cs`, `MovementSystem.cs`)
- **Objectives:**
  1. Update `NavGrid.EnterCost(int index, TraverseMode mode, bool diagonal = false)`:
     - Base step cost: `MoveCost.Diagonal` (141) if diagonal, else `MoveCost.Orthogonal` (100).
     - Proportional scaling on terrain penalties: `(CostByClass[CostClass[index]] * MoveCost.Diagonal + 50) / MoveCost.Orthogonal`.
     - Proportional scaling on hazard/door penalties if diagonal.
  2. Update `MovementSystem.StepCost`:
     - Detect horizontal diagonal steps (`from.X != to.X && from.Z != to.Z` on the same layer) and pass `diagonal: true` to `EnterCost`.

### Unit 2: NavGraph Legal Steps & Corner Cutting (`NavGraph.cs`)
- **Objectives:**
  1. In `NavGraph.IsLegalStep(int from, int to, TraverseMode mode, bool allowFalls = false)`:
     - Allow same-layer step if `dx + dz == 1` (orthogonal).
     - Allow same-layer step if `dx == 1 && dz == 1` (diagonal), requiring:
       - `Grid.CanEnter(from, mode) && Grid.CanWalkInto(to, mode)`.
       - Strict corner-cutting: both orthogonal corner cells `c1 = (a.X, b.Z, a.Y)` and `c2 = (b.X, a.Z, a.Y)` must be walkable (`Grid.CanWalkInto(c1, mode) && Grid.CanWalkInto(c2, mode)`).
     - Reject all other horizontal spans.

### Unit 3: NavGraph Regions & District Connectivity (`NavGraph.cs`, `PathingTests.cs`)
- **Objectives:**
  1. In `NavGraph.FloodBlock`:
     - Allow 8-way flood propagation inside the 10x10 block, guarded by the strict corner-cutting check.
  2. In `BuildInteriorZone` & `BuildEdgeZone`:
     - Link diagonal neighbours across region boundaries (internal and edge) with strict corner-cutting checks.
  3. In `CollectAffectedZones`:
     - Ensure affected neighbouring blocks/zones are marked dirty to preserve incremental rebuild determinism.
  4. Update `NavWorld.FloodFromCell` oracle in `PathingTests.cs`:
     - Update the test oracle to explore 8 directions with strict corner cutting, asserting that district reachability matches the cell-level oracle 100%.

### Unit 4: PathFinder 8-Connected Search & Octile Heuristic (`PathFinder.cs`, `ColonyItems.cs`)
- **Objectives:**
  1. In `PathFinder.SearchCells`:
     - Expand all 8 horizontal neighbours (4 cardinal + 4 diagonal).
     - For diagonal candidates: verify in-bounds, verify corner-cutting (`CanWalkInto(c1) && CanWalkInto(c2)`), and relax via `EnterCost(n, mode, diagonal: true)`.
  2. In `CellHeuristic` and `Manhattan` (renamed / updated to `Octile`):
     - Compute octile distance:
       $$h = \min(dx, dz) \times 141 + (\max(dx, dz) - \min(dx, dz)) \times 100 + dy \times \text{LayerChangeHint}$$
  3. In `ColonyItems.Distance` and `PawnContext.Distance`:
     - Update travel estimation to octile distance for accurate job candidate ranking.

### Unit 5: Action Stances & Work Verification (`MineJob.cs`, `JobDrivers.cs`, `BuildJob.cs`)
- **Objectives:**
  1. Ensure `StandBeside` and job drivers support diagonal stances freely, ranking orthogonal faces first only when travel distance is equal.
  2. Verify felling, mining, construction delivery, and harvesting work cleanly when approaching and acting from diagonal stances.

### Unit 6: Test Suite & Authoritative Verification
- **Objectives:**
  1. Add comprehensive tests in `Assets/Odyssey/Tests/Sim/`:
     - Straight diagonal paths across open terrain (verifying path length, cost, and nodes).
     - Corner-cutting prevention around solid walls, closed doors, and rock faces.
     - Dynamic re-pathing and incremental rebuild determinism over randomised worlds.
     - Distance and heuristic optimality tests.
  2. Run fast test tier: `dotnet test tools/dotnet/Odyssey.Tests.Sim/` and `Odyssey.Tests.Hud/`.
  3. Run content checks: `python3 tools/wiki/build_wiki.py --check` and `emit_labels.py --check`.
  4. Append complete findings and rationale to `docs/journal.md`.
