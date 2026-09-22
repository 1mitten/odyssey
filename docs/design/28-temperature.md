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

Because a layer's fills read the layer above's room table, **identity is solved top-down and
surfaces second** (the review's F2 and F9, §12). A fill on layer y reads only its own walls, the
floors of y+1 and the room table of y+1, so descending from the highest dirty layer every fill
reads a layer above that is already final; a fill that changed any cell's room marks the layer
below, whose roof and shaft rule read it, and the descent carries on. There is no fixed-point
sweep: one fill per dirty layer. Then the surfaces — which look both up (ceilings) and down
(slabs, openings) — are built once for every layer whose rooms or neighbours moved, over room
tables that are all settled. The first version swept ascending to a fixed point over the layers
the edit had marked, which cost five to six times a solve on a wooded board and still stopped
one layer short: a house roofed last had a cellar that was a room after a load and not before.
The lazy accessors run the same solve, so a world asked about before its first tick cannot
disagree with one asked after.

The surface build caches each room's surfaces, because the thermal pass must cost
O(rooms + surfaces), never O(cells):

| Surface | Far side | Conductance (‰/cell/pass) |
|---|---|---|
| Wall (edifice) | open air beyond | 4 × the material's own factor (§6) |
| Wall (edifice) | solid terrain | 4 (ground contact; material irrelevant — earth is the boundary) |
| Wall (edifice) | another room | 4 × material factor (wall between two rooms) |
| Roof slab | open air above | 6 |
| Ceiling into rock | solid terrain above | 4 (ground) |
| Floor slab onto ground | solid below | 4 (ground) |
| Slab between rooms | room above/below | 2 — recorded by the upper room only; the lower room's ceiling is neither sky nor rock there (F6) |
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

**Hysteresis** — when a fill rebuilds a room, the new room's temperature is the area-weighted
mix of what its cells were: every cell of every old room on the layer votes for the new room
that contains it, **a room that came through unchanged voting for itself with every cell**, and
a cell that was nobody's votes for the outdoor curve. So an unchanged room keeps its temperature
to the unit, a split carries the heat to both halves, a knocked-through wall mixes the two rooms
by area, and a room that stood open to the sky for a day comes back at the sky's temperature.
Sealing a room must not reset it to the outdoor answer — the fault Going Medieval's own players
report — and it does not, because its cells were in the old room. The first version returned any
known key untouched before consulting the ledger, so a re-sealed room came back at the
temperature it had a season ago and a cold hall took its cupboard's temperature (F4, F5). A save
needs none of this: rooms recompute identically from the same seed, so saved (key, temp) pairs
reattach exactly — a room from a layer's *first ever* fill keeps whatever the ledger already
holds for its key, which is the one place a known key is trusted. Entries for rooms that no
longer exist are pruned each pass, so what is held is what is saved.

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
- **Every unit of energy is spent.** The pass divides centi-degree-cells by the room's cell count
  and keeps the remainder on the room (`residual`, 0 ≤ r < cells) for the next pass, so a source
  smaller than the room still warms it and no room has a dead band around equilibrium the size of
  its own cell count. Before this one colonist's 15 in a 16-cell bedroom was 0 every pass (F7).

## 8. What temperature feeds

| Consumer | Rule | Where |
|---|---|---|
| Mood | situational banded offset: cool/warm −10, cold/hot −50, freezing/sweltering −120 | `TemperatureDef`, `NeedsSystem.UpdateMood` |
| Sleep | rest effectiveness ×1000 / ×900 / ×750 / ×550 by the same bands | `NeedsSystem.RestEffectiveness` |
| Work | ×0.70 outside 10–35 °C | `Pawn.WorkRatePerMille` |
| Condition | `TemperatureSeverity`, signed −1000…+1000 (negative hypothermia, positive heatstroke); gains (distance beyond the safe bound, in centi-degrees, × 15/1000) per needs interval — 15 at a Candle night's −13 °C, a full bar in four game-hours (F1), recovers 5 per interval in comfort; bands \|sev\| ≥ 250/500/750 → −100/−200/−300 per mille, floored at 700 — the starvation pattern exactly, real lethality arriving with M6 health | `NeedsSystem`, `Pawn` |
| Sleep memories | `Thought_SleptCold` / `Thought_SleptHot` on waking outside the band | `JobDrivers` wake sites |
| Growing | `PlantDef` min/optimal/max grow temps (6/42/58 defaults); growth gain scaled by the linear response — slowed below 6 °C, stopped at 0 by the integer floor, dead of heat at 58 | `PlantGrowthSystem` |
| The pane | `CellDetail.AmbientTempC`, one row, hot/cold tint, on the same frame as the click | `CellDetailContributor`, `InspectModel` |
| The clock | outdoor temperature beside the date, labelled outdoor (panel A3) | `HudShell.RefreshClock` |

`Pawn.AmbientTempC` is refreshed on the needs interval and read by the rates between refreshes —
and because it is a *sample* (the cell she stood in, at her last interval) and not something the
world can re-derive, it is **saved and hashed** beside `TemperatureSeverity` (pawn record, format
9). The first version left it out as "the `MoveStepCost` pattern", and a colonist saved at 700‰
ran at 1000‰ for up to an interval after a load, into hashed milliwork (F8).

## 9. Save and hash

Section `odyssey.temperature`: count, then (roomKey, tempC, residual) per live room in gathered
order. Hash the same triples through `IStateHashable`. Format 8 → 9: the pawn record grew
`TemperatureSeverity` and `AmbientTempC`, read behind a `FormatVersion >= 9` guard; an old file's
zero severity is correct — nobody in it had ever been cold — and its ambient is mid-comfort until
the first interval.

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

### 12a. The fixes, 2026-09-21

All nine landed the same day, on the branch, each one turning its probe into a test in
`Tests/Sim/TemperatureRegressionTests.cs` (fifteen tests: the nine, plus the other side of
each rule where one exists — a loaded room keeps the file's temperature, an unchanged room keeps
its own to the unit, a three-deep shaft lives from one roof, the residual rides the save).

| # | What changed | Where |
|---|---|---|
| F1 | `severitySlopePerMille` 300 → **15**; the three comments now agree with each other and the number | `Temperature.xml`, `TemperatureDef` |
| F2, F9 | The solve is two phases: identity top-down with downward propagation, then surfaces once per touched layer; no fixed-point sweep; a room keeps its boundary records so its surfaces rebuild without a refill | `EnclosureGrid`, `ThermalRoom.Boundary`, §3 |
| F3 | `RemoveSlab` marks the enclosure, as `Demolish` does | `ConstructionGrid` |
| F4, F5 | Self-votes in the ledger; the early return on a known key is gone except for a layer's first ever fill (a save reattaching); orphaned entries pruned each pass | `EnclosureGrid.FillLayer`, `TemperatureSystem.Inherited`, `ThermalRoom.FirstSolve`, §5 |
| F6 | A ceiling whose cell above is in a room is neither sky nor rock | `EnclosureGrid.ClassifyCells` |
| F7 | A per-room residual in centi-degree-cells, saved and hashed; the quarter clamp is applied before the division | `TemperatureSystem`, §7, §9 |
| F8 | `AmbientTempC` saved and hashed in the pawn record | `PawnRegistry`, `Pawn`, §8, §9 |
| lower | A detail with no thermal system stays silent; the benchmark's edit arm marks the enclosure | `CellDetailContributor`, `TickBenchmarkTests.MineOneCell` |

**The cost after the fixes**, `EnclosureCostProbe` again, same machine, same method as the table
above (main → reviewed branch → fixed):

| Board | Initial solve | One edit, mean | One edit, worst | Whole tick with an edit a tick |
|---|---|---|---|---|
| Standard, wooded (120×120×16) | 5.7 → 37.7 → **9.9 ms** | 0.56 → 0.92 → **0.79 ms** | 2.1 → 3.7 → 3.7 ms | 0.61 → 1.03 → **0.87 ms** |
| Huge, wooded (240×240×16) | 20.8 → 106.5 → **30.8 ms** | 2.58 → 4.26 → **3.85 ms** | 7.1 → 11.3 → 13.3 ms | 2.30 → 3.87 → **3.43 ms** |
| Scale target, barren (250×250×40) | 28.9 → 33.3 → 43.5 ms | 1.23 → 1.96 → **1.81 ms** | 7.5 → 12.7 → 11.8 ms | 1.31 → 2.23 → **1.96 ms** |

The initial solve is back to one and a half times main's on the wooded boards rather than five
to six; the per-edit cost is under the reviewed branch's everywhere and about 1.4× main's, which
is the surface build for the three layers around the edit and the room objects it allocates. The
worst single edit is the fill of a large layer, as it was on main, plus those surfaces. The
scale-target initial solve read higher on this run than the last and the machine had an editor
and a PlayMode batch open beside it, so that one number is noise until it is taken again alone;
its per-edit figures moved the right way.

**The goldens moved, and it was measured.** Two fields entered the hash (F7's residual, F8's
ambient), which is the whole of the tick-zero move on all three; with those two lines disabled
every `Generated` came back to the committed value. `Simulated` moved on the two boards that
have caverns and not on the barren one, and hashing each golden world component by component —
cells, pawns, edifices, the thermal section — before and after the fixes, only the thermal
section differs: a cavern now converges instead of stopping a cell count short, and a cavern
under a cavern is no longer charged to the sky. Nothing a colonist did changed. `Golden.cs`
carries the sentence.

Still open from the review's lower list: the clock reads `OutdoorTempC` off the simulation
object; the lazy solve from a snapshot read writes room state outside the tick, deterministic by
the current sync order; a wall met from two sides by one room counts twice on that side.

**The edit tick, with the enclosure in it for the first time.** `TickBenchmarkTests.TheEditTickOnEveryOfferedBoard`
on Standard, same run conditions, the miner marking nav alone and then nav and the enclosure:
**3.86 → 5.76 ms per tick** (WorldSystems 3.79 → 5.08, and 0.6 ms landing in Pawns, where the
lazy solve runs when a needs interval asks a room's temperature before the next tick's own
solve). Note that `28-map-size.md`'s 0.298 ms for Standard is the *generated* board, and this
arm is the lattice world with nine times its regions, so the two were never one measurement;
the arm's own note says so and now the number in each column names its world.


## 13. The merge with `main`, and three more before the playtest — 2026-09-22

`main` moved twice under this branch while it was in review (the shelf, PR #158; floating crops,
PR #163), and the merge was not a formality: **both branches appended at the same two slots.**
The shelf reached `main` first and took edifice 13 and `BuildingHandle` 7, so the campfire moves
to **14 and 8** — the rule `BuildingHandle.Bed` already records against the floor, the deck plate
and the ladder, and the reason handle order is spelled out as a save contract in both places. No
save written with a campfire in it has ever left this branch, which is the only thing that makes
a renumber safe.

Eighteen files conflicted and seventeen of them were a union: one branch appending a row, the
other appending a different row to the same table. **The tables that did *not* conflict are the
ones worth naming**, because git merged them in silence and one of them was wrong:

- `BuildShapes.Cells` — both branches added a `1` to the same list, so git took one of them and
  the campfire had no row at all. `RegistryTests.EveryBuildableHasAShapeOfItsOwn` caught it, which
  is exactly the failure that test was written for after the bed's handle moved from 2 to 5 and
  the bed silently became a one-cell thing that could not be turned. It has now earned its keep
  twice.
- `EdificeHandle.Count` 13 → **15** and `BuildingHandle.Count` 8 → **9**, neither of which any
  conflict marker pointed at.

The building fingerprint and all six goldens were re-baked, because a merge of two branches that
each moved them leaves *neither* side's number right for the merged code — taking either would
have committed a number nothing had produced. **Measured rather than asserted**, with
`GoldenColonyProbe` run on the merged branch, on this branch's head and on `main`, and the three
outputs diff **clean**: every one of the nine census numbers — live things, per-def stacks, item
cells, the two lister counts, pawn cells, total food, total rest, standing orders, zones —
is identical on all three boards across all three commits. The hash sees more; no colony does
anything different.

### 13a. Three findings

**F10 — the pass is O(standing edifices), and its own summary said it was not.**
`TemperatureSystem`'s class comment claimed *"O(rooms + surfaces), never O(cells)"*. The room half
is true and the sentence is still wrong, because the sweep for heat sources visits **every
standing edifice** to find the ones that are warm. Measured with the edifice count printed beside
the time (`TemperatureCostProbe`, new, `[Explicit]` like every other probe here), the shape came
out the opposite way round from the claim:

| Board | Cells | Rooms | Standing edifices | One pass |
|---|---|---|---|---|
| Scale target, barren (250×250×40) | 2.5 M | 0 | 5 | 0.0054 ms |
| Standard, wooded (120×120×16) | 230 k | 22 | 1,656 | 0.0411 ms |
| Huge, wooded (240×240×16) | 921 k | 69 | 6,311 | 0.1714 ms |

Nothing in that column is the cell count, and a board with 2.5 M cells and five edifices is the
cheapest of the three. A wooded board is mostly trees, so whatever the sweep does *per edifice* is
what the pass costs — and it was calling `ConstructionContent.BuildingForEdifice`, a **linear scan
of the building table**, which is a scan inside a sweep.

Fixed by precomputing `heatPerPass` by edifice id once, in the constructor, so the inner step is
an array read: **0.1714 → 0.0508 ms** on the huge board and **0.0411 → 0.0132** on the played one,
both arms in one run so the ratio is this machine's own. The term itself is still there and the
summary now says so with the numbers beside it. A source list maintained as things are raised and
pulled down would remove it altogether; that is the next move **if** it is ever the reason for a
number, and at one pass in 120 ticks and 0.0004 ms a tick amortised on the largest board offered,
it is not today.

**F11 — the form of a temperature had two owners.** The pane's tile row and the clock's outdoor
reading each carried their own copy of *centi-degrees to one signed decimal with the unit* — the
same four operations spelled out in two assemblies, agreeing by luck. That is `bug-patterns.md`
P1, the same fault as the two order-colour tables that disagreed about deconstruct for months, and
nothing was wrong with either copy on the day it was written: the cost arrives the first time
somebody is asked to show whole degrees and corrects one of them. `TemperatureLabels.Describe` is
the one owner now, beside `HudTheme.Temperature` which owns the colour, and
`TemperatureLabelsTests.TheUnitIsWrittenInExactlyOnePlace` **reads the C# files** to keep it that
way — the idiom `RegistryTests` and `HudFontTests` already use, and the only one that can catch
two copies of a rule that happen to agree.

It found one exemption worth recording rather than tidying away: `AlmanacCatalogue` carries fixed
encyclopedia prose (*"14 days at 20°C"*, *"−25°C below seasonal average"*) which is authored text
about content, not a rendering of a reading, and a literal cannot drift from a rule it never
applied. The two consequently use different house styles for the same unit — the pane says
**20.0 °C** and the almanac says **20°C**. That is a content question for whoever owns the
almanac's voice, and the test says so in as many words.

**F12 — the headline claim was unreachable, which is a playtest finding rather than a code one.**
The work's own sentence is *"Rime kills"*, and Rime is months four and five of six. The debug menu
offered **Skip one day**, so reaching the season the whole model was built for was *sixty presses*
— and the branch's own "still owed" note asked only whether Wash's chill reads as mild, which is
what a question looks like when the interesting one cannot be asked. **Skip one month** is a new
row on the Cheats tab, the same mechanism as the day (the calendar's own `DaysPerMonth` days of
ticks, read rather than written, so a retuned calendar does not leave it skipping some other
amount). Six presses walk the year. That is also the better test: seasons are only worth having
if the turn between them is worth watching.

### 13a-ii. F13 — the campfire's chip drew the placeholder square, and only one tier could see it

`HudGeometryTests.EveryPaletteKeyHasItsOwnShape` fails on `ui.arch.tool.campfire`: *"is on the
Build palette and has no drawn shape, so it would draw the placeholder square that the
specification forbids."* The tool went live on the palette without a glyph.

**It had been true for as long as the campfire had existed, and nothing had looked.** That test is
**PlayMode only** — `PaletteGlyphs` lives in `Odyssey.Presentation`, which the fast tier does not
compile at all, and EditMode does not carry the test. The branch reported *"Unity EditMode 2,280
total, 0 failed"* and no PlayMode figure. Three green tiers on top of a fourth nobody ran is
exactly the shape `docs/lessons.md` already records for the Long tier turning `main` red after
PR #145 merged clean.

Fixed by drawing one: `HudGlyphKind.ToolCampfire`, a flame over two crossed logs.
**Drawn rather than borrowed**, unlike the bed, which wears the bunk's shape with a recorded
reason — a bunk *is* a bed and the lie is only about which kind, whereas there is nothing on this
palette a fire could borrow from without saying something false. The two logs carry the reading at
17 px more than the flame does (a flame on its own is a leaf), so they are the wider, more
separated pair of strokes and the flame sits clear above them.

### 13a-iii. F14 — the naming registry has two heat sources, and the art is on the other one

Not fixed, because it is the owner's call and not a code question.
`docs/design/icon-keys.csv` carries **both**:

| Key | Name | Milestone | Description |
|---|---|---|---|
| `ui.arch.tool.brazier` | Brazier | M3 | "Heat and light, no power, some risk" |
| `ui.arch.tool.campfire` | Campfire | M4 | "A fire: warmth you can build" |

The thing that now exists in the game is the campfire. **The pixel art is mapped to the
brazier** — `icon-map.csv` row 239, sheet 03, and the cell's own description is the word
*"campfire"*. So the owner's sheet has a drawing of the thing that is in the game, filed under the
name of a thing that is not.

Three ways out, and the choice belongs to whoever owns the naming: remap the `icon-map.csv` row to
`ui.arch.tool.campfire` and let the brazier stay an unmapped M3 idea; keep both and accept that a
brazier is a *different* later building (fuelled, indoor, riskier) that will want its own cell;
or decide the campfire was always the brazier and rename the content. Nothing is broken today —
the campfire has a vector glyph as of F13 and the wiki and the screen agree — but two registry
entries for one concept is precisely what the wiki exists to surface, and it has surfaced it.

### 13b. Still open, and deliberately not touched

`WeatherOffsetC` is a settable seam with nothing setting it, so a cold snap's −25 °C — the thing
the almanac already promises and the arithmetic in `Temperature.xml` is tuned against — cannot be
reached at all, in any season. A debug row for it would be one line and was **not** written: the
incident that owns weather is §10's deferred work, and a debug switch that sets a field an
incident is supposed to own is how a seam quietly becomes an interface. If the playtest comes back
wanting the cold snap before the incident does, that is the moment to reconsider, and the reason
will be on record rather than assumed.

Also unchanged from §12a's lower list: the clock reads `OutdoorTempC` off the simulation object;
the lazy solve from a snapshot read writes room state outside the tick; a wall met from two sides
by one room counts twice on that side.
