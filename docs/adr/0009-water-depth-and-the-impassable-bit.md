# ADR 0009 — Water: two depths, a channel one layer down, and a flag of its own

Status: **accepted (owner decision by interview, 2026-09-16)**. `02-world-and-layers.md` §8 had deferred water entirely — *"not in the brief's core scope"* — so taking the deferral up is recorded here rather than edited quietly into the design documents.

## Context

The starting board was a uniform sheet of walkable grass. Every cell cost the same to cross, nothing was ever routed around, and the only obstacle a colonist had was distance. `docs/brief.md` lists rivers under map generation and asks what the unit of simulation for water is; `a-12-map-generation.md` records the properties a water terrain needs — path cost, buildability, water and bridgeable flags — and leaves open *"exactly how river carving interacts with terrain and bridges on the local map"*. Nothing answered any of it.

Water is the cheapest thing that makes the board an argument: a pond you walk around, a stream you wade, a river you resent until you can bridge it.

The owner settled the shape by interview: ponds, winding streams and marsh fringes on most maps with wide rivers as a rarer variant; shallow water walkable at a third speed and deep water impassable; a channel cut one layer down so banks are real geometry; every river guaranteed a ford; the wooded playtest board to get water with the start kept clear of it; and no bridge building yet.

## Decisions

### 1. Two depths as terrain, not a depth field

Water is two `TerrainDef` entries — shallow and deep — plus a third, marsh, which is ordinary ground that happens to be slow. Not a per-cell depth value.

A depth field would be a new dense per-cell array to hash, save, publish and mirror, for a quantity with two meaningful values. Terrain is already a per-cell def-table index that is hashed, saved, chunk-encoded and resolved to art, so water costs no new grid, no new save section and no new snapshot channel. The rejected alternative is on record because it will be proposed again the first time someone wants water that rises.

### 2. Deep water is impassable, not merely expensive

The pathfinder routes around a lake rather than pricing a swim. A very large cost still lets a desperate search buy a drowning, and "expensive" and "impossible" are different claims that a cost model cannot tell apart.

### 3. A new `CellFlags` bit, not a blocking edifice

Deep water must be neither standable-in nor standable-on, and **no existing flag can say that**. Solid terrain holds a colonist up on the cell above, so a solid lake is one people walk across. Non-solid terrain leaves the cell itself walkable, because the bed beneath it is a floor, so a non-solid lake is one people walk through. There is no combination of the two that works.

`CellFlags.ImpassableTerrain` (bit 5, driven by `TerrainDef.impassable`) is read in exactly two places: `CellGrid.IsWalkable` and `NavGrid.RefreshFrom`. The alternative that needs no engine change — a blocking edifice per deep-water cell — was rejected: it is thousands of `PlacedEdifice` records on a river map, and a "thing" in the cell that mining and deconstruction will one day try to remove.

### 4. A channel one layer down, and water one cell deep whatever its depth

Water sits in a channel cut one layer below the ground around it, so a bank is real geometry a bridge can later span and a colonist steps down to wade.

**Deep water is not cut deeper.** The bed *is* the column's surface, so a two-layer deep core would put neighbouring surface cells two layers apart and break the invariant that keeps the board walkable without ramps. Depth is told by colour and opacity instead — which is why both depths draw their surface at the same height inside the cell: shallow and deep sit side by side in one pond, and a height difference would put a step in the middle of the water.

This is not a loosening of ADR 0002. That ADR forbids slopes and half-heights *within a cell*; this adds nothing to a cell. The evidence is that not one of the generator's existing invariants had to be relaxed — the column rule, neighbouring surface cells being within one layer, and the surface being continuous with nothing floating all pass unmodified.

### 5. Two generator passes, either side of the strata

Where the water goes is a **column** decision and must happen before the strata are laid, so that the one full-grid loop builds a correct column under every bed by construction. What a cell is made of is a **cell** decision and can only happen after. So `WaterPlanPass` is pass 2 and `WaterFillPass` is pass 4, with the strata between them.

The rejected alternative — one pass afterwards that lowers columns and rewrites them — must hand-repair the terrain, the solid flags, the report counters and the stratum boundaries: a second, worse copy of the strata pass, and exactly the kind of fix-up that drifts the next time a stratum is added.

### 6. Banks are cut down to the water, in both directions

Two consequences were found by tests rather than by reasoning, and both are inherent rather than incidental.

A path crossing a terrace step lowers a column that was *already* a layer below its neighbour, leaving a two-layer cliff. And a channel cut into sloping ground can leave water perched above a lower neighbour, which would drain.

Both are fixed by relaxing the ground around a channel until **a dry bank stands exactly one layer above the bed it looks down on** — lowering whichever end of a disagreeing pair is too high, never raising. It terminates because every step lowers a column and none goes below its floor. It is also what a watercourse does to the ground it runs through, so the result reads as a shallow valley rather than a trench slotted into a hillside.

The relaxation is **symmetric**, and that is the subtle part: if only the wet end knows how to pull its bank down, a bank lowered by some other channel never tells the channel beside it to follow, and a stretch of the map keeps water hanging over open ground.

### 7. Fords are guaranteed; maps are never re-rolled

Every river gets crossings cut by construction, water is kept clear of the start clearing, and a post-generation reachability flood forces further fords where the river has separated the board. After a bounded number it throws, because a pond cannot sever a map and a river that still does is a bug in the shape code rather than an unlucky seed.

**Re-rolling was rejected.** It makes generation take unbounded time on an unlucky seed, and a generator handed a seed that quietly uses a different one is a determinism smell even when it is reproducible.

Measured: with fords cut by construction, **0 of 40 rivers** need the backstop. With them switched off and the river widened, it fires on **40 of 40** and still leaves the colony 80% of the board — which is how we know the backstop works rather than merely never failing.

### 8. Water has a shader, not a tint

What makes a surface read as water is the shading and nothing else: ripples, a moving glint, a reflection that strengthens as the surface turns away, and a shore that dissolves instead of ending in a line. None of it can be reached by tinting a ground tile blue, so `Odyssey/Water` replaces the art material the way the ghost material does.

It is arithmetic only — no texture, no extra pass, no render target, no second camera — and is drawn by the ordinary chunk machinery, one instanced draw per chunk like the ground beside it. The moment water needs a pass of its own it stops being a terrain and becomes a system. A clone without the licensed packs therefore draws exactly the same water as a machine with them.

**Water casts and receives no shadows.** It does not write depth and is drawn after the opaques, so it is already outside the shadowed world, and a tree shadow landing on a moving surface reads as dirt on it. Measured, this is also a third of the feature's cost: the wooded board ran 0.68 ms a frame before water, 1.28 ms with shadowed water and 0.99 ms without.

## What this costs

- **Frame time.** Measured under the real player loop at 640 × 480: wooded board 0.99 ms mean, ruined city 1.56 ms, against a 5 ms budget. The pre-water figure recorded for the board was 0.68 ms; the city figure on record (0.88 ms) was not reproducible in the same session and the discrepancy is **unexplained** — the city has no water on it, so it is more likely that the recorded numbers were taken under different conditions than that water cost it anything.
- **Generation time.** Two bounded floods and a column-order reachability flood, on a generator budgeted at 2,000 ms. Not separately measured.
- **Memory.** Two per-column `byte[]` on the generation context — 125 KB at the scale target, against a 5 MB terrain array.
- **One bit** of `CellFlags`, of which two of eight remain.

## What is deliberately not in it

Bridge building, because there is no build pipeline at all: `IntentKind` has no `Build` and nothing constructs anything at runtime. The *refusal* half is enforced now, and `TerrainDef.buildable` and `bridgeable` are written where U26 will find them, so the rule does not have to be rediscovered.

Also out: drinking water, fishing, drowning, freezing, water that spreads or flows, water at L−1 as a utility main, and weather that fills a pond. Layer question 10 — the unit of simulation for gas, fire, water and sound — is **not** answered by this ADR. Nothing here propagates; a water cell is a terrain, not a fluid.

## Open

- **Marsh is named for a bog and drawn as a bank.** With the dirt material and a bright sour tint it reads as a pale sandy margin, which looks right beside water but is not what "marsh" means. Either the tint goes greener or the name goes — an owner call.
- **Off-map neighbours do not seed the shallow flood**, so a river arrives and leaves through deep mouths. Seeding from off-map would make both mouths wadeable. A taste call for the next look-check.
- **A pond tangent to a river bank** could in principle seal a corner that the river alone would not, and the forced-ford logic only knows how to ford the river. Asserted and measured rather than handled.
- **Ponds require a level footprint** and are dropped otherwise, so raising `surfaceRelief` produces fewer ponds rather than lumpier ones. The knob to reach for then is `maxPondRadius`, never the level rule, which is what keeps the water surface level and the banks a single step high.
