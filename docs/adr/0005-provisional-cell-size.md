# ADR 0005 — A provisional cell of 2 m x 2 m x 3 m, behind one constant

- **Status:** provisional, pending the Synty inventory. The number may change; nothing else may
- **Date:** 2026-09-15
- **Deciders:** owner, choosing to unblock the world rather than wait
- **Supersedes:** nothing. It **suspends** part of brief §2 non-negotiable 3
- **Related:** `docs/research/synty-import.md`, `docs/design/09-ui-and-input.md` §9 D7, brief §5 Lane E and the twelve layer questions

## Context

Brief §2 is explicit: "The cell size is derived from the Synty modular pieces, not chosen
abstractly. Nothing is built until Phase 0 reports it." That rule is right, and the reason is
sound: a grid that does not match the art produces buildings that never quite line up, and
retrofitting the metre is as expensive as retrofitting the z axis.

The rule also assumed Phase 0 would take an afternoon. It has not, because the measurement needs a
Unity editor and the licensed pack, and neither exists in the remote container. `SyntyInventory.cs`
is written and waiting; `docs/research/synty-import.md` has the procedure. Until someone runs them,
the rule blocks every part of M1: the chunked grid, worldgen, the camera's zoom bands, pathfinding
step costs, and the whole world-and-layers design document.

So the choice is between an idle project and a provisional number. The owner chose the number.

## Decision

**The working cell is 2 m across, 2 m deep and 3 m tall. One layer is therefore 3 m.**

**It lives in exactly one place.** A `WorldMetrics` type holds `CellSize = 2f` and `LayerHeight =
3f`, and **nothing else in the codebase may contain a metre literal.** Not a prefab offset, not a
camera distance, not a path cost, not a test fixture. Any value in metres is derived from
`WorldMetrics` at the point of use. That is what makes the number provisional rather than load
bearing: when the inventory disagrees, one constant changes and the world re-derives.

An architecture test enforces it from M0: no float literal that represents a distance appears
outside `WorldMetrics`. Cheap to write now, impossible to add later.

## Rationale

1. **250 x 250 cells at 2 m is a 500 m square.** That is a plausible city district: a dozen blocks,
   walkable in a few minutes, large enough that the far corner is a decision. At 1 m it would be a
   250 m plot, which is a car park rather than a district, and the scale target in the brief is
   fixed in cells, so the choice costs nothing in performance either way.
2. **3 m is a storey.** It carries a doorway, a light fitting above head height and a service void.
   Two metres would look like a crawlspace; four would make a forty-layer world 160 m tall and the
   cut-away camera would spend most of its range on air.
3. **It makes the stair geometry work.** The brief fixes stairs at two cells of footprint rising one
   layer: 4 m of run for 3 m of rise, a 37 degree stair. That is a real staircase. At a 2 m layer
   height it would be a ramp; at 4 m, a ladder with pretensions.
4. **Forty layers is 120 m.** A thirty-storey tower plus basements and utility tunnels, which is
   the silhouette the setting promises.
5. **Two metres is a generous human cell.** A person occupies well under a metre, so a 2 m cell
   allows two colonists to pass in a one-cell corridor, and a one-cell doorway reads as a doorway
   rather than a hatch. It also means a single cell can carry a piece of furniture without the
   furniture needing a sub-cell grid, which is a whole system avoided.

## Consequences

**The grid is not isotropic, and that is the consequence to watch.** A step sideways is 2 m and a
step up is 3 m. Anything that measures distance must work in metres through `WorldMetrics`, never in
cell counts: pathfinding costs, sound falloff, light attenuation, throwing and shooting ranges,
temperature exchange between cells. A cell count masquerading as a distance will look correct on one
layer and wrong the moment it crosses layers, which is the hardest class of bug to notice. The
alternative, a cubic 2 m cell, was rejected because a 2 m storey is not a room.

**What changes if the inventory disagrees.** The constant, the prefab import scale, and any
authored content expressed in cells whose proportions assumed 2:3. What does not change: the chunk
layout, the save format, the Def schema, the pathfinding algorithm, or any system's logic. That
asymmetry is the point of the decision.

**What this does not license.** Building world content at scale before the inventory runs. A
provisional metre is fine for code and for the design documents; hand-authored building-shell
templates measured in cells are the one thing worth deferring, because they are the part that would
have to be redrawn.

## Alternatives considered

| Option | Verdict |
|---|---|
| Wait for the inventory | Rejected by the owner. Correct in principle, and it has already cost the project its critical path |
| Cubic 2 m x 2 m x 2 m | Rejected. Simpler maths and an isotropic grid, but a 2 m storey is a crawlspace and the stair geometry collapses |
| 1 m x 1 m x 3 m | Rejected. Finer placement, but a 250 m map, four times the cells for the same area, and sub-metre pawns on a metre grid |
| Choose the grid and rescale the art | Rejected for now. It inverts the brief's rule on the strength of an unrun measurement. Revisit only if the inventory shows the pieces cannot be reconciled |

## Reconciliation, when the inventory runs

1. Run `scripts/unity.sh inventory` and read the grid-pitch test in `docs/research/synty-inventory.md`.
2. If the implied footprint and height pitch are 2 m and 3 m, promote this ADR from provisional to
   accepted and say so in `docs/research/synty-import.md`.
3. If they differ, change `WorldMetrics`, re-run the tests, and amend this ADR with the measured
   figures and what had to move. The ADR keeps its number and its history.
4. If the pieces snap to no consistent pitch at all, that is a finding about the pack rather than
   about us, and the fourth alternative above comes back onto the table.
