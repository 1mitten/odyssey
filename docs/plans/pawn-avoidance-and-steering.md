# Plan: Pawn Soft Avoidance and Sub-Tile Visual Steering

## Goal
Implement soft crowd avoidance in pathfinding and fluid sub-tile lateral steering in presentation so colonists manoeuvre naturally around other colonists, item heaps, and trees rather than phasing straight through cell centres.

## Constraints & Working Agreement
- **Hard stops:** Ground → Interview → Research → Plan → Execute. This plan must be approved before execution begins.
- **Clean room:** Pure C# in `Odyssey.Sim`, zero UnityEngine dependencies, full determinism preserved.
- **Tile bounds:** Cell width is 2.5 m (ADR 0002). All lateral offsets remain strictly within the cell footprint ($\pm 0.6$ m to $\pm 0.75$ m from centre, well within the $\pm 1.25$ m tile half-width).
- **Zero Region Graph Dirtying:** Dynamic pawn costs must not dirty macro-regions or portal edges (D4 architecture); path bias is evaluated locally during concrete A* expansion.
- **Tests & Gates:** Fast tier (Sim + Hud), Unity EditMode, and content checks green.

---

## Technical Architecture

### 1. Simulation: Soft Path Bias (`Odyssey.Sim`)
- **Pawn Occupancy Tracking:** A lightweight lookup in `PawnContext` or `PawnStore` tracking which cell indices are currently occupied by standing/working pawns.
- **A* Local Expansion Penalty:** In `PathFinder.ExpandNeighbours`:
  - When evaluating candidate neighbour cell $N$, if $N$ contains a standing/working pawn, add a transient penalty `MoveCost.OccupiedBias = 30` (relative to `MoveCost.Cardinal = 100`).
  - This gently steers unconstrained walkers to take an adjacent open corridor or path if available, but allows crossing if it is the only route.
  - No allocation, zero region-graph invalidation.

### 2. Presentation: Smooth Hermite Steering Curve (`Odyssey.Presentation`)
- **Lateral Vector:** For travel direction $\vec{d} = (dx, 0, dz)$, right normal is $\hat{n}_{\text{right}} = (dz, 0, -dx)$.
- **Hermite Envelope:**
  - Standard smooth bell curve: $S(t) = 4t(1 - t)$ or cubic Hermite spline $H(t)$.
  - Max displacement $W_{\text{lateral}} = 0.65\text{ m}$ (leaves 0.6 m buffer from tile wall).
  - Offset $\vec{\Delta}_{\text{lateral}} = \text{sign} \cdot W_{\text{lateral}} \cdot S(t) \cdot \hat{n}_{\text{right}}$.
- **Anticipatory Lead-in:**
  - If the upcoming cell $C_{next}$ contains an obstacle, the deflection curve begins ramping up during the second half of the preceding cell ($t \in [0.5, 1.0]$), achieving a continuous S-curve that anticipates the obstacle before reaching it.

### 3. Mutual Right-Hand Passing Rule
- When two moving pawns are on colliding or opposing paths ($\vec{d}_1 \cdot \vec{d}_2 < -0.5$ within distance $< 3.0\text{ m}$):
  - Both pawns assign $\text{sign} = +1$ (steer to their own right).
  - Both deflect laterally by $+0.6\text{ m}$ relative to their own forward heading.
  - Total lateral clearance between them becomes $1.2\text{ m}$—cleanly passing abreast down a single 2.5 m hallway without clipping.

### 4. Static In-Cell Clutter (Trees & Items)
- Cells with tree trunks or stockpiled commodity heaps (`ItemHeap`):
  - Default lateral bias veers around the centre obstacle.
  - The figure glides smoothly to the open side of the tile.

---

## Unit Breakdown

| Unit | Scope | Deliverable | Tests |
|---|---|---|---|
| **U1: Sim Dynamic Bias** | `Odyssey.Sim` | Add `OccupiedBias` to concrete A* expansion in `PathFinder.cs` | `AvoidancePathingTests.cs` (verifies alternate corridor chosen when occupied; verifies single corridor is passed through) |
| **U2: Lateral Steering Kinematics** | `Odyssey.Presentation` | `SteeringCurve.cs`: Hermite lateral envelope, bounds clamping ($\le 0.75\text{ m}$) | `SteeringCurveTests.cs` (verifies $C^1$ continuity at edges, peak offset at $t=0.5$, bounds clamping) |
| **U3: Mutual Passing in `PawnPose`** | `Odyssey.Presentation` | Extend `PawnPose.Of` to calculate lateral separation for oncoming pawns using right-hand rule | `PawnPassingTests.cs` (verifies two opposing pawns maintain $\ge 1.0\text{ m}$ separation throughout passing) |
| **U4: In-Cell Obstacle Avoidance** | `Odyssey.Presentation` | Detect trees/item stacks in path and apply anticipatory lateral curve | `ObstacleSteeringTests.cs` (verifies figure veers around cell-centre tree trunk) |
| **U5: Full Verification** | System | Fast tier tests, EditMode test run, re-baking goldens if simulated hashes move | Fast tier green, EditMode green, content checks green |

---

## Verification Plan
1. `scripts/test-fast.sh`: Ensure all Sim and Hud tests pass without regression.
2. `scripts/unity.sh test editmode`: Verify Presentation tests and EditMode suite.
3. Content checks: `python tools/wiki/build_wiki.py --check` and `emit_labels.py --check`.
4. Visual proof: Verify pawns passing in 1-tile wide hallway in PlayMode or screenshot harness.
