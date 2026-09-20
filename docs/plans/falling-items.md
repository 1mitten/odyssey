# Plan: Falling Items and Mid-Air Safeguards

## Goal
Eliminate instances of loose commodities and logs floating in mid-air (from floor deconstruction, salvage spawning, drops on ladders, or mined ground), ensure dropped items fall directly to the nearest floor below in the simulation, and provide a smooth, gravity-accelerated downward visual falling motion with landing audio in presentation.

## Constraints & Working Agreement
- **Hard stops:** Ground → Interview → Research → Plan → Execute. This plan must be approved before gameplay code is written.
- **Clean room:** Pure C# in `Odyssey.Sim` (UnityEngine-free). Deterministic fixed-tick updates; zero state-hash drift.
- **Cell metrics:** Slabs are on the upper cell (`CellMetrics.FloorCentre(cell)` is $y \times 3.0\text{ m}$). Drop heights are integer multiples of 3.0 m.
- **Visual-only motion:** Presentation tracks vertical transitions between simulation snapshots and applies an accelerating drop offset; simulation updates `item.Cell` immediately upon the fall event to preserve clean saving, hauling reservations, and pathfinding.
- **Gates:** Fast tier (753 Sim + 449 Hud), EditMode test suite, and content checks (`build_wiki.py --check` and `emit_labels.py --check`) must be green.

---

## Technical Architecture

### 1. Simulation: Preventing & Resolving Mid-Air Items (`Odyssey.Sim`)

#### A. Floor Deconstruction (`ConstructionGrid.RemoveSlab`)
- When a floor slab is dismantled, `_grid.Floor[cell]` becomes `SlabNone`.
- Currently, `Falling.OutOf` was never called, leaving any colonist or item on that floor floating in mid-air.
- **Fix:** Call `Falling.OutOf(ctx, cell, thought: Falling.NoThought, tick: 0)`.
  - Colonists on the slab drop to `FirstFloorAtOrBelow(cell)` without distress (`NoThought`), matching the intentional behaviour of digging beneath oneself.
  - Items on the slab drop to `FirstFloorAtOrBelow(cell)`.

#### B. Deconstruction Salvage Placement (`DeconstructJob.DemolishAt`)
- When deconstructing a slab, materials were spawned at `cell` without checking whether `cell` still had a floor.
- **Fix:** Ensure the refund destination resolves to `ctx.Cells.FirstFloorAtOrBelow(cell)` before searching for space.

#### C. Floor Requirement for Item Placement (`ColonyItems.NearestCellWithSpace`)
- `NearestCellWithSpace(cells, origin, ...)` previously accepted `origin` if `CellHasSpace(origin)` was true, without verifying `cells.HasFloor(origin)`.
- **Fix:** In `NearestCellWithSpace`, verify `cells.HasFloor(origin)`. If `origin` lacks a floor, start search from `cells.FirstFloorAtOrBelow(origin)`.

#### D. Falling Item Overflow & Despawn (`Falling.ItemsOutOf`)
- Currently, `Falling.ItemsOutOf` searches only `maxRadius: 3`. If packed, `item.Cell` was left unchanged in mid-air.
- **Fix:** Search outward up to `maxRadius: 8`. If the entire 8-tile radius is completely occupied, despawn the falling item (`ctx.Items.Despawn(resting)`) so it never remains hovering.

#### E. Safety Net Sweep (`ColonyItems.DropFloatingItems`)
- Add a helper in `ColonyItems` / `PawnContext` that checks loose items and ensures none are resting on a cell without a floor (`!ctx.Cells.HasFloor(item.Cell)`).
- Hook into structural change resolution as a safety guard.

---

### 2. Presentation: Gravity-Accelerated Fall Animation (`Odyssey.Presentation`)

#### A. Item Falling Tracking (`ItemFallingDirector` or `ChunkRenderer`)
- Maintain a transient visual falling dictionary: `(int thingId) -> (Vector3 startPos, Vector3 targetPos, float elapsed, float duration)`.
- When an item's snapshot cell changes vertically downwards ($y_{\text{new}} < y_{\text{old}}$) or when a fall is detected:
  - Record the starting position at the upper layer and target landing position at the lower floor.
  - Calculate duration scaled with fall height $h = y_{\text{old}} - y_{\text{new}}$:
    $$t_{\text{fall}} = \text{clamp}(0.25\text{s} + 0.15\text{s} \times \sqrt{h / 3.0\text{ m}}, 0.35\text{s}, 0.85\text{s})$$

#### B. Quadratic Gravity Motion
- Similar to `CarryHandover.Fallen(elapsed)`:
  $$\text{phase} = \text{clamp01}(t / t_{\text{fall}}), \quad \alpha(t) = \text{phase}^2$$
  $$\vec{P}(t) = \text{Lerp}(\vec{P}_{\text{start}}, \vec{P}_{\text{land}}, \alpha(t))$$
- In `ChunkRenderer.RenderItems`, add the vertical falling offset $\vec{P}(t) - \vec{P}_{\text{land}}$ to the rendered matrices for the item.

#### C. Landing Impact Sound
- On the exact frame when `elapsed >= duration` (and `before < duration`), trigger `AudioDirector.PlayOneShot(SoundIds.CarryDrop, targetPos)` so the item makes a distinct thud on impact.

---

## Unit Breakdown

| Unit | Scope | Deliverables | Tests |
|---|---|---|---|
| **U1: Simulation Slab Deconstruction & Spawn Guards** | `Odyssey.Sim` | Wire `Falling.OutOf` into `RemoveSlab`; enforce `HasFloor` in `NearestCellWithSpace`; drop deconstruction refund to `FirstFloorAtOrBelow`. | `FloorsAndCollapseTests.cs` (assert item and pawn drop when floor slab is deconstructed; assert deconstruct refund lands on ground below). |
| **U2: Simulation Overflow & Safety Sweep** | `Odyssey.Sim` | Expand search radius in `Falling.ItemsOutOf` to 8; despawn if completely packed; add `DropFloatingItems` safety sweep. | `FallingTests.cs` (assert item despawns if radius 8 is completely packed; assert floating item sweep drops orphaned items). |
| **U3: Presentation Falling Curve & State** | `Odyssey.Presentation` | `ItemFallMotion.cs`: kinematics for gravity fall duration and quadratic easing curve; unit tests. | `ItemFallMotionTests.cs` (duration scaling with height, quadratic acceleration, start/end bounds). |
| **U4: Presentation Item Fall Tracking & Audio** | `Odyssey.Presentation` | Track falling items across frames in `ChunkRenderer`; apply offset to `ItemHeap` and single-prop placements; trigger `SoundIds.CarryDrop` on landing. | `ItemFallPresentationTests.cs` (matrix offset verification across frames, audio trigger on touchdown frame). |
| **U5: Regression & System Verification** | System | Run fast tier (Sim + Hud), EditMode tests, wiki check, label check; verify zero hash drift on baseline scenarios. | Fast tier green (753+ Sim, 449+ Hud), EditMode green, wiki check green. |

---

## Verification Plan
1. **Fast Tier:** `bash scripts/test-fast.sh` (proves all Sim & Hud units).
2. **EditMode Tests:** `bash scripts/unity.sh test editmode` (proves Presentation rendering, kinematics, and Audio integration).
3. **Content Gates:**
   - `$env:PYTHONUTF8="1"; python tools/wiki/build_wiki.py --check`
   - `$env:PYTHONUTF8="1"; python tools/wiki/emit_labels.py --check`
