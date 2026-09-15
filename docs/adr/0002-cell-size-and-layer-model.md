# ADR 0002 — Cell size 2.5 × 2.5 × 3.0 m; discrete one-cell layers

Status: accepted (owner confirmed 2026-09-15; measurement basis in `docs/research/synty-inventory.md`).

## Context

The brief (§2) fixes the layer model — discrete cells, one cell of height per layer, no slopes or half-heights, roofs are the floor above — and mandates that the cell size be derived from the Synty modular pieces, not chosen abstractly. The Phase 0 inventory measured 2,138 prefabs across the five owned packs: base walls 2.50 × 3.01 × 0.23 m with 97% base pivots, floors 2.50 × 2.50 × 0.10 m, stairs on a 2.5 m footprint, the ladder 3.0 m tall, and the multi-cell `Section` pieces at exactly 5 m. A 1.25 m fine grid (matching the half/quarter trim pieces) was considered and rejected: 4× the cells per layer with matching pathfinding and simulation cost, while buildings still author at the 2.5 m pitch.

## Decision

**One cell = 2.5 m × 2.5 m footprint × 3.0 m height.** All grid-touching systems (pathing, light, temperature, roofs, zones, fire, gas, sound) address cells as integer (x, y, z) at this pitch from their first commit. Stairs occupy two cells, ladders one. The scale target remains 250 × 250 footprint × ~40 layers (a ~625 m square district).

## Consequences

- Synty base modules snap natively; half-height (1.5 m) and half/quarter-width pieces are visual trim inside a cell, never a simulation unit.
- Pawns (~1.8 m) fit one cell with headroom; vehicles and large props may occupy multiple cells as things, not as a finer grid.
- Pieces that do not snap (43 % of wall widths are non-multiples — mostly props, fort pieces and backgrounds) are placed as free-standing things within cells, or excluded from the buildable set (Lane E1 decides per module).
- Changing this later invalidates worldgen templates, save format, pathfinding and every Def that names a footprint — treat as effectively irreversible.
