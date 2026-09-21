# 28 — Temperature

The model a-06 recommends (`docs/research/a-06-temperature-environment.md`), made concrete. Every
number in this file is INVENTED unless it names a-06 as its source; the owner tunes them at the
keyboard, and the fingerprints pin them once tuned.

## 1. Shape, in one paragraph

Every enclosed per-layer room is one scalar — centi-degrees, an `int`, no float anywhere. Every
120 ticks each room takes one explicit-Euler step toward its neighbours and its boundaries through
cached, typed surfaces; unenclosed air is never integrated and simply reads the outdoor curve.
Heat sources push energy, not temperature, so a brazier in a broom cupboard is an oven. Warm air
climbs through a stairwell because the conductance of a vertical opening depends on the sign of
the difference (k_up : k_down = 4 : 1), which is the whole of buoyancy as far as a player can
tell. Digging down means something because the ground, not the outdoors, is the boundary under
and around buried rooms, and the ground's seasonal swing dies off with depth.

## 2. Units and representation

- **Centi-degrees** (`int`): −27,000…100,000 covers the modelled range. One unit is 0.01 °C, so
  a pass that moves a room 0.05 °C still moves it.
- **Conductance** in per-mille of the temperature difference, per surface cell, per pass. A wall
  cell of the standard material (stone, 1000) moves a 1×1 room 0.4% of the difference per pass —
  the same order as the reference's measured 1.69% for all four walls of one.
- **Energy** in centi-degree-cells: what a source pushes, divided by cell count to become a
  temperature change. Every cell is the same volume by construction, which is what makes that
  division honest.

## 3. Rooms and surfaces

`EnclosureGrid` keeps its tested indoors/outdoors answer and grows, beside it, room identity:
`_roomAt[cell]` names the room a cell belongs to, **keyed by the region's minimum cell index** —
a property of the region, not of the iteration that found it, so a room's key is deterministic
and stable for as long as its cell set is. "Indoors" means what it meant — bounded by
walls/doors/rock, off the map edge, ≤ 2,500 cells, roofed — with **one amendment the round trip
forced**: a cell with no slab above is still roofed when what it opens into is *the room above*
(the shaft rule). A stairwell, a ladder shaft or a hatch is carried by the thermal model as an
`Opening` surface with conductance of its own; without the amendment, a cellar with a way up
failed the 100%-roof test and read the outdoor curve, and the buoyancy feature would have been
dead on arrival — the cellar is the whole point. A hole to the *sky* still breaks enclosure, and
the reference's softer ≥25%-unroofed rule remains a recorded refinement.

Because a layer's fills read the layer above's room table, the sweep runs **ascending until a
sweep changes nothing** — each sweep pulls one more layer of a shaft chain to life, and stopping
early left a played world and a loaded one disagreeing about which rooms exist (found as a
round-trip hash divergence over caverns three layers deep). The lazy accessors run the same
convergent sweep, so a world asked about before its first tick cannot disagree with one asked
after.

The same layer solve caches each room's surfaces, because the thermal pass must cost
O(rooms + surfaces), never O(cells):

| Surface | Far side | Conductance (‰/cell/pass) |
|---|---|---|
| Wall (edifice) | open air beyond | 4 × the material's own factor (§6) |
| Wall (edifice) | solid terrain | 4 (ground contact; material irrelevant — earth is the boundary) |
| Wall (edifice) | another room | 4 × material factor (wall between two rooms) |
| Roof slab | open air above | 6 |
| Ceiling into rock | solid terrain above | 4 (ground) |
| Floor slab onto ground | solid below | 4 (ground) |
| Slab between rooms | room above/below | 2 |
| **Vertical opening** (no slab, room below) | the room below | **k_up 400 / k_down 100** |
| Floor missing, open air below | outdoors below | 250 (near-open) |
| Door, closed | its far room or outdoors | 8 |
| Door, open (`NavFlags.DoorOpen`) | its far room or outdoors | 350 (near-merge) |

The roof term is per-area and so size-independent while the wall term shrinks with
perimeter-over-area — the asymmetry the reference hard-codes and we derive, by summing real
surface cells. Door conductance is applied per pass from the nav flag, because a door swinging
open changes nothing structural and must not re-solve the layer.

## 4. The pass

`TemperatureSystem` — WorldSystems phase, Order 50 (after Enclosure 30 and growing 40), gated to
`CurrentTick % 120`. One pass:

1. Solve any dirty enclosure layers (Enclosure's own Order-30 tick already did; the gate is a
   belt).
2. Compute every room's Δ from the *old* temperatures — Jacobi, not Gauss-Seidel — then apply
   them all. Room iteration order provably cannot matter.
3. Clamp the *exchange* half of each room's Δ to a quarter of the largest single driving
   difference it faces (ONI's rule): the cheapest known guarantee that a coarse explicit
   integrator cannot oscillate. Sources are clamped by nothing — the quarter rule bounds how fast
   two temperatures may approach each other, and a fire pushing a room away from equilibrium is
   not an approach. Strangling the source with it was the first cut's fault: a fired room at one
   with the outdoors could never warm, its drive being zero.
4. Outdoor cells need no integration — `CellTemp` reads the curve directly.

**Vertical openings are asymmetric on the sign of the difference, not on which room is which**:
`k = (T_lower > T_upper) ? k_up : k_down`. A two-storey hall with a fire below reads warm above
and cold at the foot of the stairs — the sensation a vertical game owes the player, and the
tie-breaker a-06 names for the whole per-layer design.

## 5. The curves

**Outdoor** — a pure function of the tick, from `ClimateDef` (`Defs/Core/World/Climate.xml`):

> T_out(t) = annualMean + monthlyOffset[month] + dailyAmplitude × shape[hour]

with shape a fixed 24-point cosine peaking at 14h (code, not content — it is math). The shipped
temperate table: mean 9 °C; monthly offsets +2, +5, +12, +17, −8, −18 (Larkspur…Candle); daily
±6 °C. So Wash days run 11–15 °C mean, Glare 21–26, and Rime falls from +1 to −9 — nights at
−15 in Candle, which is exposure, and a future cold snap at −25 below seasonal is lethal, exactly
what the almanac already promises. `WeatherOffsetC` (0 today) is the one field an incident adds
to; that is the whole weather seam.

**Ground** — `T_ground(layer) = annualMean + monthlyOffset[month] × exp(−depth(layer)/δ)`, δ = 3
layers (9 m), recomputed on the daily boundary. At three layers down the seasonal swing is 37%
of the surface's, at six it is 14%, at nine it is gone — a cellar lags the season, a deep mine
holds the annual mean, and neither needs rock to have a temperature. Depth is counted from the
board's top layer; on the terraced meadow that overstates depth by a terrace or two where the
swing is already negligible. New rooms start at the outdoor curve and relax toward the ground —
a freshly dug cellar is cold at first, which is true.

**Hysteresis** — when a rebuild changes a room's cell set, the new room inherits the
area-weighted temperature of the old rooms it overlaps (their cells are still in memory at
rebuild time). Sealing a room must not reset it to the outdoor answer — the fault Going
Medieval's own players report. A save needs none of this: rooms recompute identically from the
same seed, so saved (key, temp) pairs reattach exactly; unknown keys default to the curve.

## 6. Materials matter

`StuffDef.thermalConductancePerMille`: wood 600, stone 1000, composite 800, concrete 1100, steel
1400. The reference's wall *material* does nothing — a log cabin and a granite bunker hold heat
identically — and our Def-driven stuff table makes the choice a decision instead of a colour.
Double-thick walls (the reference's one insulation trick) are a recorded hook, not v1.

## 7. Sources

- `BuildingDef.heatPerPass` in centi-degree-cells. The campfire ships 1,200: in deepest Rime it
  holds a 6×6 room at roughly +38 °C over the outdoors (comfortable), and in Wash the same fire
  in the same room overshoots toward 49 °C — a brazier in a broom cupboard is an oven, and the
  lesson "don't burn fires in summer" is free. A temperature *cap* on a source is a recorded
  hook, not v1.
- **Pawn body heat**: 15 centi-degree-cells per pawn per pass, gated off at ≥ 40 °C ambient —
  the reference's own trick for stopping a crowded room running away. Two dozen colonists in a
  sealed 6×6 is half a campfire.

## 8. What temperature feeds

| Consumer | Rule | Where |
|---|---|---|
| Mood | situational banded offset: cool/warm −10, cold/hot −50, freezing/sweltering −120 | `TemperatureDef`, `NeedsSystem.UpdateMood` |
| Sleep | rest effectiveness ×1000 / ×900 / ×750 / ×550 by the same bands | `NeedsSystem.RestEffectiveness` |
| Work | ×0.70 outside 10–35 °C | `Pawn.WorkRatePerMille` |
| Condition | `TemperatureSeverity`, signed −1000…+1000 (negative hypothermia, positive heatstroke); gains (distance beyond the safe bound × 3/10) per needs interval, recovers 5 per interval in comfort; bands \|sev\| ≥ 250/500/750 → −100/−200/−300 per mille, floored at 700 — the starvation pattern exactly, real lethality arriving with M6 health | `NeedsSystem`, `Pawn` |
| Sleep memories | `Thought_SleptCold` / `Thought_SleptHot` on waking outside the band | `JobDrivers` wake sites |
| Growing | `PlantDef` min/optimal/max grow temps (6/42/58 defaults); growth gain scaled by the linear response — slowed below 6 °C, stopped at 0 by the integer floor, dead of heat at 58 | `PlantGrowthSystem` |
| The pane | `CellDetail.AmbientTempC`, one row, hot/cold tint, on the same frame as the click | `CellDetailContributor`, `InspectModel` |
| The clock | outdoor temperature beside the date, labelled outdoor (panel A3) | `HudShell.RefreshClock` |

`Pawn.AmbientTempC` is a derived cache refreshed on the needs interval and deliberately unsaved
and unhashed — the `MoveStepCost` pattern. `TemperatureSeverity` is saved (pawn record, format 9)
and hashed.

## 9. Save and hash

Section `odyssey.temperature`: count, then (roomKey, tempC) pairs in room-key order. Hash the
same pairs through `IStateHashable`. Format 8 → 9: the pawn record grew `TemperatureSeverity`,
read behind a `FormatVersion >= 9` guard; an old file's zero is correct — nobody in it had ever
been cold.

## 10. Seams left open, on purpose

- **Overlay** — the disabled `ui.overlay.temperature` menu row arms in a follow-up: one
  single-channel texture per layer, 256-entry LUT, `OverlayDirector` state (design 09 §4.6).
  The bulk query the renderer will want is `TemperatureSystem` itself; nothing here hides it.
- **Weather** — `WeatherOffsetC`; an incident sets it and the curve moves. Rain/snow, wind
  chill, and the visuals (`DaylightDirector.ApplyHour` is the one hook) arrive with weather.
- **Fire** — the model already carries 235 °C without complaint; ignition is fire's to build.
- **Spoilage** — `CellTemp` is the query a refrigerator will ask (M5).
- **Light** — separate M4 slice; the campfire will emit through it.
- **Campfire fuel** — v1 burns steadily. Fuel is a hauling bill away.
- **Partial roofing** — the ≥25%-unroofed relaxation, when a playtest asks for it.

## 11. Tests

Fast tier (`Tests/Sim/TemperatureTests.cs` and friends): room-key stability under iteration;
surface classification; a sealed room's approach to equilibrium and its time constant; the a-06
buoyancy experiment (stacked 6×6 rooms, one opening, source below — warmer above at 4:1, level
at 1:1); the quarter clamp under a near-merge door; the ground curve's damping; hysteresis
across a wall-build split; the outdoor curve by season and hour; save round-trip; severity,
rest, work and growth effects; content fingerprints. Goldens re-bake — three things move them
(the new hashed pawn field, the hashed temperature section, growth now reading the curve) and
the commit message says so. Soak: a sheltered colony with a campfire survives a Rime month.
