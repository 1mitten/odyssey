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

## 12. Review, 2026-09-21 — nine findings before the playtest

Reviewed on a worktree with `main` merged in (`D:\code\odyssey-review-164`, branch
`review-164`), against the code rather than the doc. Every finding below was **proved by a
probe test**, not read: `Tests/Sim/TemperatureReviewProbes.cs` is `[Explicit]` and each probe
fails on this branch as it stands. The fix for a finding turns its probe into an ordinary test.
Ranked by what a player meets first.

| # | Finding | Proof (probe) | Fix |
|---|---|---|---|
| F1 | **The severity bar fills in fourteen game-minutes, not four hours.** `severitySlopePerMille` 300 is applied per centi-degree, so a Candle night at −13 °C adds 300 an interval and the bar is full in four intervals (600 ticks, 0.24 h). `Temperature.xml` promises "about four hours", `TemperatureDef`'s comment says "about 4 an interval" and the pinning test's message says "three" while pinning 300. Recovery is 5 an interval, so twelve minutes outside costs half a day. | `ProbeF` | One number: 15–30, not 300 (30 is "3 per degree per interval", 15 is the four hours). Re-pin the fingerprint and rewrite the three comments to agree. |
| F2 | **A room roofed last is not a room until something else is built.** The fixed-point sweep re-solves only the layers the edit marked (y−1, y, y+1); a change on y that makes y−1 enclosable (the shaft rule reads the layer *above*) never re-solves y−2 and below. Build the cellar, the ground floor, then the roof: the ground floor is a room and the cellar is not; a fresh solve of the same board says it is. **A played world and a loaded one disagree — the exact divergence the sweep was written to end.** | `ProbeD` | When solving y changes its rooms, mark y−1 dirty and keep sweeping. Better: sweep *descending* so a fill reads a fresh layer above, and mark downward on change; the surface build (which looks down) then runs once per changed layer after the fills settle. |
| F3 | **Deconstructing a floor never tells the enclosure.** `ConstructionGrid.RemoveSlab` marks nav and structure and not the enclosure; `Demolish` and `MineJob` go through the mark. Take the roof off a warm room and it stays roofed and warm for the thermal model until an unrelated edit dirties those layers — and a load re-solves it, so again the two worlds disagree. | grep of every `Enclosure` caller in `Sim`; no probe, because it needs the job path | Route `RemoveSlab` through the same mark `Demolish` uses. |
| F4 | **A room that comes back comes back at its old temperature, however long it was open.** `ResolveInitial` returns a known key before it consults the ledger, and entries outlive their rooms. A fired room at 33.6 °C, opened to a −20 °C sky for a game day and re-sealed, reads 33.6 °C. And because only *live* rooms are saved, the same history on a loaded world reads −19.2 °C: **hash divergence after any re-seal.** | `ProbeC`, `ProbeC2` | Record self-votes in the ledger; drop the early return; a room whose ledger is entirely itself keeps its value exactly, anything else is the area mix with the outdoors for uncovered cells. Then prune (or save) orphans, the same on both sides of a save. |
| F5 | **Knocking through adopts the lower key's temperature.** A 16-cell cupboard at 33.6 °C joined to a 108-cell hall at −20 °C: the hall reads 33.1 °C the next pass. An area mix would be −13 °C. The same early return. | `ProbeB` | The F4 fix. |
| F6 | **A shared slab is charged to the sky as well.** `BuildRoom` counts any non-rock ceiling as `CeilingSkyCells`, including one that is another room's floor, and the room above records the slab link too. A fired cellar sits at 33.6 °C alone and 27.8 °C under a sealed loft: **building upstairs makes downstairs colder.** A shaft cell is charged the same way. | `ProbeA`, `ProbeA2` | In `BuildRoom`, a ceiling whose cell above is in a room is neither sky nor rock; the upper room's `SlabLink` or `Opening` is the whole contact. |
| F7 | **Body heat is zero in any room bigger than fifteen cells.** 15 centi-degree-cells a pass over 16 cells truncates to 0, so one colonist in a 4×4 bedroom never warms it. The design's "two dozen in a 6×6" works because 360/36 survives the division. | `ProbeE` | Carry a per-room remainder across passes, or accumulate sources before dividing. |
| F8 | **The cached ambient does not survive a load.** `Pawn.AmbientTempC` is unsaved and refreshed on the needs cadence, so for up to 150 ticks after a load a colonist at −20 °C works at 1000‰ where she was saved at 700‰. Milliwork is hashed: **a Rime save diverges from its own game.** Invisible to the round trip, which runs in Wash where nobody is outside the work band. | `ProbeG` | Refresh every pawn's ambient on the first needs tick after a load (or at `PawnRegistry.Load`, given the context). |
| F9 | **The solve costs more than the doc says and was never in the benchmark.** `TickBenchmarkTests`' edit arm marks the nav grid only, so the enclosure has never been in the edit tick — before this branch or after. Measured with `EnclosureCostProbe` (explicit, same file on both), same machine, one after the other: | see the table below | Add an enclosure-marking edit to the benchmark's edit arm (process §3 says so). Then the F2 restructure: one fill per dirty layer, descending, surfaces built once after; pool the room objects (five lists and a dictionary are allocated per room per solve). |

The cost, `EnclosureCostProbe.TheEnclosureSolveOnEveryOfferedBoard`, 200 random marks, then 240
whole ticks with one mark a tick:

| Board | Initial solve, main → branch | One edit, mean | One edit, worst | Whole tick with an edit a tick |
|---|---|---|---|---|
| Standard, wooded (120×120×16) | 5.7 → **37.7 ms** | 0.56 → **0.92 ms** | 2.1 → 3.7 ms | 0.61 → 1.03 ms |
| Huge, wooded (240×240×16) | 20.8 → **106.5 ms** | 2.58 → **4.26 ms** | 7.1 → 11.3 ms | 2.30 → 3.87 ms |
| Scale target, barren (250×250×40) | 28.9 → 33.3 ms | 1.23 → **1.96 ms** | 7.5 → 12.7 ms | 1.31 → 2.23 ms |

Two readings. The per-edit cost is up 1.6× and the worst single edit on Huge is 11 ms — one
hitch per built wall at speed 3. And **the enclosure was already the dominant cost of a real
edit on Huge before this branch** (2.3 ms of the tick against the 0.88 ms `28-map-size.md`
records for an edit that marks nav alone), which the benchmark could not see.

Lower, recorded and not ranked: the clock reads `OutdoorTempC` straight off the simulation
object rather than through a view field, which is fine while the curve is pure and stops being
fine the day `WeatherOffsetC` is set by an incident; a `CellDetail` built without a thermal
system prints "0.0 °C" instead of staying silent (`?? 0` where the field's own "nothing to
say" is `int.MinValue`); the lazy solve fired from a snapshot read or from `ContributeTo`
writes room identity and starting temperatures from outside the tick — deterministic today only
because nothing syncs `PawnContext.CurrentTick` before Order 30; a wall met from two sides by
one room is counted twice on that side of a `WallLink` and once on the other.

**Subsystems touched, and what was checked**

| Subsystem | Touch | State |
|---|---|---|
| Enclosure | room identity, surfaces, the shaft rule, the sweep | F2, F3, F6, F9 |
| Construction, mining, support | marks; the campfire (edifice 13, handle 7) | F3 (floors); walls, mining and collapse mark correctly |
| Doors | flag read per pass, never re-solves | correct |
| Needs, rates | ambient cache, mood bands, sleep factor, severity, work factor | F1, F7, F8 |
| Jobs | sleep memories on waking | correct |
| Growing | the 6/42/58 response scaling the interval | correct; crops stop below 0 °C by design |
| Save, hash | format 9, the `odyssey.temperature` section, the pawn field | F4, F8 (both are load divergences) |
| Calendar | moved to `Sim.Contracts`; `GameClock` delegates | correct |
| HUD, presentation | pane row, clock, palette, build shapes, campfire module | correct; the degree sign is in both fonts (`HudFontTests`) |
| Content | Defs, wiki, registry, fingerprints | correct after the merge; both `--check`s pass |
| Events | `WeatherOffsetC` seam, unset | correct |
| Falling items, storage, pathing | none | — |
