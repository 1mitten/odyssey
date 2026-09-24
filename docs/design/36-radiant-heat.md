# 32 — Radiant heat: a hot tile and a warm ring, with no per-cell field

**Built 2026-09-23**, on the owner's ask: *"The temperature of the campfire tile should indicate red
in the temperature on the colony stat tile and set at a high temperature, colonists will avoid this
tile unless fallen onto, directly told to etc (this can be done later). Also the surrounding tiles
will get heat benefit (maybe amber to indicate more passive heat instead of dangerous heat) — please
make recommendations."*

The recommendation was written first, as §15 of `31-campfire-art-and-fire.md` on the campfire
branch; this is the built thing, and it lives on its own because it changes the **temperature
model** rather than the campfire's art. Design 28 is the model it sits on top of.

---

## 1. The obstacle, which is design 28's best decision

Design 28's core choice is *"every enclosed room is one integer scalar … per-room, **never**
per-cell"*, and that is the only reason the thermal pass is affordable beside a 2.5 M cell board.
`TemperatureSystem.CellTemp` read it straight: the room's air where the cell is in a room, the
outdoor curve where it is not.

So **every cell in a room read the same number**. A hot tile with a warm ring around it is a
per-cell gradient, which is exactly what the model refuses to store.

## 2. It does not have to be stored

A gradient that is a **pure function of distance to a source** needs no per-cell array, no save and
no extra pass — only a handful of sources to measure against. The thermal pass already walks the
standing edifices every 120 ticks looking for `heatPerPass`; it now also keeps the warm ones in a
small list as it goes, and `CellTemp` measures against that list on the way out.

**Design 28 models the air; this models radiance**, and the two are named apart on purpose. A fire
warms the room by heating its air — slowly, shared by the whole room, and the same campfire is an
oven in a cupboard and a warm corner in a hall. It warms *you* by shining on you — immediately,
locally, and no differently in a cupboard than in a hall. Folding one into the other is the
simplification to refuse.

## 3. The shape

| Thing | Where | Value |
|---|---|---|
| `BuildingDef.radiantC` | content, per building | campfire **2600** centi-degrees at its own cell |
| `TemperatureConductance.RadiantFalloffPerMille` | structural | **550** — a little over half survives each cell |
| `TemperatureConductance.RadiantRangeCells` | structural | **2** |

`radiantC` is content because how hot a thing is, is a fact about that thing. The falloff is
structural because how fast radiance falls with distance is a fact about **distance** — two fires
tuned to different falloffs would warm their neighbours differently at the same temperature, which
nobody could read.

**Chebyshev distance, so the ring is square.** The player is looking at a grid; a round falloff puts
the diagonal neighbours in a different colour band from the orthogonal ones at the same apparent
distance, which reads as a bug rather than as physics. It is also the cheaper measure.

**Walls stop it, and the test is one comparison**: a source only reaches cells in its own room.
Right for a fire behind a wall, and no ray. Outdoors both rooms are 0, so the test passes and
distance alone decides — also right, since a fire in a field does warm the grass beside it.

**It does not cross layers.** A campfire is not underfloor heating, and the rooms above and below
already exchange through the slab, which is design 28's business. Letting radiance through as well
would count it twice.

## 4. `CellTemp` and `RoomTempC` are now two questions

This was not planned and is the useful part. Four existing tests of the **air** model failed the
moment radiance arrived — not because the air had changed, but because they read a cell with a fire
in it and got the fire as well. That is the right answer to *"what is it like to stand here"* and
the wrong answer to *"what is this room's air"*, and the two had been one method.

- **`CellTemp`** — what it is like to **stand** here. The pane, the needs system, the growth pass.
- **`RoomTempC`** — the **air**. Design 28's own model, and what a test of that model should ask.

## 5. The colours needed no new code

`HudTheme.Temperature` already bands red above 35 °C and amber above 30 °C, so the ask lands in the
table that exists:

| Where | Wash daytime (~19 °C air) | Band |
|---|---|---|
| the fire's own cell | ~45 °C | **red** |
| one cell out | ~33 °C | **amber** |
| two cells out | ~27 °C | none |
| three cells out | 19 °C | none |

**In deep Rime the fire's own cell reads about 13 °C and is not red, and that is correct rather than
a miss.** The colour tracks whether standing there is *dangerous*; a fire in freezing air is the
opposite of dangerous, it is the warm spot. The owner's own distinction — red for danger, amber for
passive benefit — falls out of absolute temperature without a second rule.

## 6. The avoidance that was deferred, mostly arriving on its own

`Temperature.xml` already sets `heatstrokeC` and `workMaxC` to **3500**, the same 35 °C the red band
starts at. So with no new mechanic, a colonist standing in the fire now works at **×0.7** and builds
`TemperatureSeverity` that drains when they leave. Real pressure not to stand there, from tuning
rather than from code.

What is **not** built, deliberately, is the pathfinder refusing the cell. That puts a per-cell lookup
inside the hottest loop in the tick for the sake of a handful of cells. When avoidance proper is
wanted, the cheaper shapes in order of cost are:

1. refuse the fire's cell as a **destination** for idle wander;
2. gate it out of job targets the way `ctx.Reachable` already gates givers;
3. a flee behaviour — which wants a health model to flee for, and a design of its own.

## 7. No golden moved, and that is the tell

A content change that reaches the state hash normally re-bakes all six goldens. **None moved.**
Radiance is inert without a source and no golden board has a campfire on it.
`RadiantHeatTests.ABoardWithNoFireReadsExactlyTheAir` is the assertion of that — and it is also the
cheap-path guarantee, because `CellTemp` is asked **per growing cell** by the growth pass over fields
of two thousand, and on a board with nothing burning it pays one `Count == 0` test.

Only `BuildingFingerprint` moved, for the new field.

## 8. The tests

| Test | Guards |
|---|---|
| `TheFiresOwnCellIsHotterThanTheRoom` | the bump exists and carries the whole of `radiantC` |
| `ItFallsOffWithDistanceAndStops` | the shape — hot, warm, nothing — as an **ordering**, so retuning does not falsify it |
| `TheRingIsSquare` | Chebyshev, so diagonals match orthogonals and the grid shows no cross |
| `AWallStopsIt` | same-room gating; a colonist cannot feel next door's fire |
| `ItDoesNotReachThroughAFloor` | a campfire is not underfloor heating |
| `ABoardWithNoFireReadsExactlyTheAir` | the inert case, which is every board until somebody builds one |

Fast **1,074 + 752**, Long **38**, EditMode **2,674 / 2,647 / 0**, PlayMode **109 / 104 / 0**.

## 9. Still open

**The tuning is one XML line and wants a playtest.** 2600 was chosen to put the fire's own cell past
35 °C in Wash and the first ring inside the amber band. Whether that reads as *"do not stand in the
fire"* rather than *"the fire is broken"* is a question for somebody at the keyboard, and so is
whether a two-cell reach feels like a hearth or like a bonfire.

**A second heat source will test the falloff being structural.** A stove or a brazier at a different
`radiantC` should warm its neighbours in the same *shape* as the campfire, just more or less. If one
ever wants a different shape, that is the moment the falloff stops being structural — and a reason
should be written here before it moves.
