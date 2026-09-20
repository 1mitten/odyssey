# Falling items and mid-air safeguards

**Status:** design settled by owner interview 2026-09-20; worktree `claude/falling-items`.
**Read first:** `17-floors-and-collapse.md` (the support, slab and collapse rules), `24-carrying.md` (item pickup and drop handovers), `Falling.cs`.

## 1. Grounding and context

Loose items (felled logs, mined stone, raw rations, deconstruction salvage) can currently appear or remain suspended in mid-air in several scenarios:

1. **Floor deconstruction (`RemoveSlab`):** When a floor slab is taken down by a colonist (`ConstructionGrid.RemoveSlab`), `_grid.Floor[cell]` is cleared to `SlabNone`. Unlike structural collapse and mining, `Falling.OutOf` was never wired into `RemoveSlab`. Any items or colonists resting on that floor remain hovering in the air on that layer.
2. **Deconstruction salvage placement (`DeconstructJob`):** When deconstructing a floor slab, the salvaged materials are spawned into `cell` via `NearestCellWithSpace`. Because the slab was just removed and `NearestCellWithSpace` did not check whether `origin` has a floor, the refund stack spawns directly in the open air where the floor used to be.
3. **Dropping items without a floor underfoot:** `ColonyItems.NearestCellWithSpace` only verified `cells.IsWalkable(candidate)` for neighbours at `radius >= 1`. For `origin` itself, it accepted any cell where `CellHasSpace` returned true without verifying `cells.HasFloor(origin)`. Consequently, a colonist dropping an item whilst standing on a ladder or open gap (`Job.DropCarried`) places the item directly into mid-air.
4. **Instant visual teleportation:** When an item does fall in simulation (such as via mining or structural collapse calling `Falling.ItemsOutOf`), `item.Cell` is instantly updated to the landing cell. Presentation renders the item at the new position on the next frame with no falling motion or impact sound, causing an abrupt pop.

## 2. Agreed decisions (interview 2026-09-20)

| # | Question | Decision |
|---|---|---|
| 1 | Simulation vs presentation split | **Instant simulation cell update with presentation drop motion.** Simulation immediately moves `item.Cell` to the landing floor, preserving determinism, save states, and immediate hauler pathfinding. Presentation tracks the downward transition and animates a smooth, physics-based accelerating drop onto the floor below. |
| 2 | Mid-air safeguards | **Fix all removal and spawn sites AND add an ongoing simulation check.** Wire `Falling.OutOf` into `RemoveSlab`, drop deconstruction refunds to the floor below, enforce `HasFloor` in `NearestCellWithSpace`, and add a simulation sweep so any loose item found without a floor drops automatically. |
| 3 | Falling motion and audio | **Gravity-accelerated fall scaling with drop height + landing sound.** Items accelerate downward ($t \propto \sqrt{\text{height}}$, e.g. ~0.4s for 1 layer, scaling up to ~0.8s for multi-storey drops) and trigger `SoundIds.CarryDrop` when touching down. |
| 4 | Colonists on deconstructed floors | **Step/drop down without a negative thought (`NoThought`).** Dismantling a floor underfoot is an intentional task, matching the behaviour of digging out ground under oneself. |
| 5 | Landing overflow | **Search outward up to radius 8; despawn if completely packed.** If the landing area within radius 8 has no capacity, despawn the excess rather than leaving it in mid-air. |

## 3. Technical specification

### Simulation (`Odyssey.Sim`)

1. **`ConstructionGrid.RemoveSlab`:**
   - After clearing `Floor[cell]`, invoke `Falling.OutOf(ctx, cell, thought: Falling.NoThought, tick: 0)`.
   - Any colonist standing on the slab steps down to the landing floor below without distress.
   - Any items on the slab drop to the landing floor below.

2. **`DeconstructJob.DemolishAt`:**
   - When calculating the spawn cell for the deconstruction refund, evaluate `ctx.Cells.FirstFloorAtOrBelow(cell)` so that refunds never spawn in mid-air.

3. **`ColonyItems.NearestCellWithSpace`:**
   - Require `cells.HasFloor(origin)` before accepting `origin` as a viable placement cell. If `origin` lacks a floor, start search from `cells.FirstFloorAtOrBelow(origin)`.

4. **`Falling.ItemsOutOf`:**
   - Increase `maxRadius` from 3 to 8 when looking for space on the landing floor.
   - If no cell within radius 8 can accept the stack, despawn the item via `ctx.Items.Despawn(resting)` rather than leaving it suspended.

5. **Safety sweep (`ColonyItems` / `PawnContext`):**
   - Provide a method (e.g. `DropFloatingItems(PawnContext ctx)`) that checks loose items and drops any item where `!ctx.Cells.HasFloor(item.Cell)`.
   - Run this check during structural modifications and as a periodic fallback.

### Presentation (`Odyssey.Presentation`)

1. **Item fall tracker (e.g. in `ChunkRenderer` or an `ItemFallingDirector`):**
   - Track items whose altitude drops between snapshot updates.
   - Calculate falling trajectory:
     $$\Delta y(t) = y_{\text{start}} - (y_{\text{start}} - y_{\text{land}}) \cdot \text{Fallen}(t)$$
   - Calculate duration from drop height $h$:
     $$t_{\text{fall}} = \text{clamp}(0.25\text{s} + 0.15\text{s} \times \sqrt{h / 3.0}, 0.35\text{s}, 0.85\text{s})$$
   - Use quadratic easing ($t^2$) to reflect gravitational acceleration.
   - Fire `AudioDirector.PlayOneShot(SoundIds.CarryDrop, landingPosition)` on the frame of touchdown.
