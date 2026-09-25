# 43 — Weather: sky state the seasons roll, rain that roofs stop

**Numbered 43 on review (2026-09-24).** Written as 39; `main` has since given 39 to the settings
window, and 40–42 are held by branches in flight (the title screen and stair gait, the draw, walls
down). Every place that cited it moved with it.

**Reviewed 2026-09-24 against `origin/main` 3ca5098c** (`claude/weather-design-review`): the number,
the system order (§5), the roof rule and the canopy (§6), the save format (§3), and §7, which is
rewritten — the owner approved shared particle emitters, and the review proposes GPU-procedural
rain and a wet ground instead, **with pictures of both** (`claude/rain-look`, `RainCheck`,
`docs/research/d-20-rain-rendering.md`). Which one ships is the owner's call.

**Built 2026-09-25: `weather-core` and `weather-visuals`, together, on `claude/rain-look` (PR #203).**
The owner asked for a weather that "makes sense — some days rain, storm, sunny, depending on
season", and for one PR to merge. So the sky and its look landed together, and §8's first and third
steps are one PR. What is built:
- `WeatherSystem` (Order 35, 120-tick cadence) rolls Clear, Cloudy, Rain and Storm from the §4
  table in `Defs/Core/World/Weather.xml`.
- Each spell lasts its rolled hours and hands over across two game hours.
- It writes `WeatherOffsetC`, publishes a `WeatherView` for the drawing, is saved as
  `odyssey.weather` with no format bump, and is hashed.
- `DebugSetWeather` is the debug menu's command.
- The clock shows the sky as a glyph with the word in its tooltip. The row has no room for a word.

**Built 2026-09-25: `weather-world`, §8's second step** (`claude/weather-world`, worktree
`D:\code\odyssey-weather-world`). Rain slows whoever stands in it, waters the crops it reaches and
sends animals for cover, and all three ask one shelter rule that the drawing now asks too. What was
built, and the three places it departs from the letter of this design, are in §6a.

**Status as designed, 2026-09-24:** design only, nothing built. Branch `claude/weather-design`, worktree
`D:\code\odyssey-weather`. Ground: `main` at `3a39dd8d`. The owner approved the shape — three kinds
(Clear, Cloudy, Rain), rain drawn with shared emitters, three phased PRs — and asked for the design
as the PR.

**Read first:** `28-temperature.md` §5 (the outdoor curve this hangs off) and §10 (the seam this
fills — `WeatherOffsetC`, `DaylightDirector.ApplyHour`), `01-architecture.md` §3 (phase order),
`22-growing.md` §8 (the growth hooks), `31-campfire-art-and-fire.md` §4 (shared emitters vs
instanced cards, the decision this inherits), `03-systems-catalogue.md` §6 (the M4 no-drop contract:
*"rain and snow stopped by roofs"*). Research already on disk: `a-05` (shelter as booleans the roof
grid owns), `a-12` (weather commonality by climate), `a-15` (RimWorld keeps a weather decider per
map). RimWorld's own numbers (rain ×0.90 move, ×0.80 accuracy) are calibration anchors, not
contracts.

Every number in this file is INVENTED unless it names a source; the owner tunes them at the
keyboard, and the content fingerprints pin them once tuned.

## 1. What the owner asked for, in one table

| Topic | Decision |
|---|---|
| Kinds | **Clear, Cloudy, Rain, Storm.** Rain varies light → downpour by intensity. **Storm** is the rarer dim day (owner, 2026-09-25): heavy rain, the colour drained, a stronger wind. Snow and fog stay wiki words until a follow-up (§9). |
| How rain looks | **Rain keeps the colour of a clear day and only dims it** (owner, 2026-09-25: *"we want to be colourful when it rains"*). Cloudy and Storm are the grey days. Zoomed out, rain still reads (§7). `docs/research/rain-look-interview.md`. |
| Rain and shelter | **A cell the sky cannot reach stays dry** — under a roof slab, and under a tree canopy. Nothing spawns, nothing slows, nothing is watered there. |
| Rain and pace | Rain slows **walking and running alike** for pawns standing in it; a roof or a canopy gives the pace back. Apparel can buy it back later (§9). |
| Rain and crops | Rain **aids growth** on sky-exposed crops, scaled by intensity. |
| Rain and animals | Animals **head for cover** — trees and roofs — when rain passes a gate, and re-seek when the cover is destroyed. |
| Cloudy days | Mid temperatures, dimmer sun: a cool grey day, not a storm. |
| Sunny days | Brighter, warmer-leaning days in Glare; the existing curve plus a small clear-sky offset. |
| Any hour | Weather **does not know the hour**. Episodes roll and ramp on their own clocks; nothing is gated to day or night. |
| Delivery | Three phased PRs (§8): sim core, world effects, visuals. Each lands green and playable. |

## 2. Shape, in one paragraph

Weather is **one map-wide sky state, never per-cell** — the same call temperature made with its
per-room scalars. A `WeatherSystem` holds the active kind, an integer intensity in per-mille, and
the episode clock; when an episode ends it rolls the next kind from a table weighted by
`Calendar.SeasonOfYear` — *"what temperature and one day weather key off"* (`Calendar.cs`). Episodes
last game-hours and cross day boundaries, so the sky does not flip at midnight; intensity ramps in
and out over roughly two hours, integer-linear, so the first drop and the last are events and not
steps. The system's whole per-tick cost is O(1): evaluate the ramp, publish the blend. Temperature
is fed through the seam 28 §10 left open — `WeatherOffsetC` — and every other consumer (pace,
growth, animal minds, the HUD, the rain drawing) asks the weather system for a number, exactly as
they ask `EnclosureGrid` for indoors. Shelter has one owner (§6): `ShelteredFromSky(cell)`, one
query nobody restates (P1).

## 3. Units and representation

- **Intensity** in per-mille (`int`, 0–1000). Rain's whole character — drizzle to downpour — is
  this one number; every effect scales by it.
- **Temperature offsets** in centi-degrees, added to `WeatherOffsetC` — the same integer rule the
  thermal model already holds (28 §2).
- **Time** in ticks, durations authored in game hours (one hour = 2,500 ticks, `Calendar`).
- **Chance** in per-10,000, like the wildlife tables — a weight of 2,500 means "a quarter of
  rolls", not "a quarter of the time" (episodes have different lengths; the owner tunes lengths
  separately).
- **RNG** only through `DeterministicRandom.ForTick(seed, tick, purpose)` (the tick is an `int`)
  with a purpose of its own, so weather rolls can never collide with another system's draws. Same
  seed → same sky, on Mono and CoreCLR (01 §7).
- **State**: active kind, previous kind, both intensities, the blend window, the episode-end tick.
  `ISaveable` (`SaveKey "odyssey.weather"`, a new keyed section, so **no format bump**: storage (8)
  and temperature (9) settled that only a change to an existing record moves the number,
  `SaveFormat.cs`) and `IStateHashable` — anything a pass reads is saved and hashed, the
  `AmbientTempC` lesson. Goldens
  re-baked with a reason line in `Golden.cs`.

## 4. The roll and the episode

Content is a `WeatherDef` family in `Defs/Core/World/Weather.xml`, beside `Climate.xml` — numbers
in XML, curve shape in code (the Climate rule: *"the curve's shape is code, not content"*). Each
def carries: `tempOffsetC` (at full intensity), `cloudPerMille`, `moveFloorPerMille` (the pace at
intensity 1000; rain only), `growBonusPerMilleAtFull`, `gloomPerMille` (how far the kind drains
the day to grey: 0 for Clear and Rain, most of the way for Cloudy, all of it for Storm),
`windPerMille` (the wind's strength, 1000 ordinary), per-season weights (per-10,000, Wash · Glare ·
Rime), and duration bounds in game hours.

The invented first table:

| | Wash (spring) | Glare (summer) | Rime (winter) |
|---|---|---|---|
| Clear | 4,500 | 6,500 | 5,500 |
| Cloudy | 3,000 | 2,000 | 4,000 |
| Rain | 1,900 | 1,100 | 400 |
| Storm | 600 | 400 | 100 |

Storm took its weight out of Rain's, roughly one wet spell in four (the owner's *"then have dim
days"*, 2026-09-25). The chance of a wet spell in each season is unchanged. Storm lasts 4–12 h,
rolls its intensity between 800 and 1000, and blows at `windPerMille` 1300. Lightning is still a
seam (§9).

Durations: clear 16–40 h, cloudy 10–30 h, rain 6–24 h. Rain's intensity rolls between 250 and
1000 per episode. Rime's rain is the known compromise of shipping rain before snow — cold rain
near freezing — which is why its weight is lowest, and the owner may zero it; when snow lands
(§9) the same table grows a row and the kind gate becomes `OutdoorTempC < 0`.

**The blend.** On an episode switch the old kind's intensity eases to nought and the new kind's
to its roll, both over ≈2 game hours, integer-linear per evaluation. During the blend both kinds
contribute proportionally (offset = Σ def.tempOffsetC × share × intensity / 1000), so a rainy
spell arriving under cloud darkens and cools as one movement, and the HUD word changes when the
incoming kind's share crosses half — a readable event, not a flicker.

## 5. What weather feeds

| Consumer | The number it asks for | Where it lands |
|---|---|---|
| Temperature | `WeatherOffsetC = Σ def.tempOffsetC × share × intensity / 1000` | written each pass; `TemperatureSystem.OutdoorTempC` unchanged (28 §5) |
| The clock panel | kind + intensity word | design 10 A3's unbuilt half: the weather row beside date and season |
| Pawn pace (PR 2) | `WeatherPerMille()` = 1000 − (1000 − moveFloor) × rainI / 1000, **only when the pawn's cell is sky-exposed** | one more factor in `Pawn.MoveRatePerMille()`'s product (`Pawn.cs`, the documented home of pace modifiers) |
| Crop growth (PR 2) | × (1000 + intensity × growBonusAtFull / 1000) on sky-exposed crops | the one multiply in `PlantGrowthSystem`'s pass — 22 §8's promised hook, separate from fertility |
| Animal minds (PR 2) | rain gate (≥ 400 per-mille) + the shelter query | `AnimalShelterThinkNode`, ahead of `AnimalIdleThinkNode` (§6) |
| The drawing (PR 3) | `RainPerMille`, `CloudPerMille` | `WeatherDirector` + `DaylightDirector` (§7) |

**One writer for the offset (P1).** Only `WeatherSystem` writes `WeatherOffsetC`, each pass. When
incidents arrive with weather (§9), the cold snap commands the weather system, which holds the
field for the episode's duration — the incident never writes the field itself, so the offset
always has the one owner 28 §10 promised it would.

**Pace is a rate, never a path price.** The factor multiplies the pawn's progress; `NavGrid.EnterCost`
is untouched, so the planner and the mover keep agreeing (design 17's hop-price rule). A pawn
walks dry under a roof at full pace while the rain drums a cell away — the shelter rule answers
both, which is the point of owning it once.

**`WeatherSystem` order:** WorldSystems phase, **Order 35** — after Enclosure 30, before Growing 40,
`PowerGrid` 45 and Temperature 50, so every reader sees this pass's weather: the offset is current
when the thermal pass reads it, and growth reads this pass's rain rather than the last one's.
Cadence 120 ticks, matching the thermal pass. (The first draft said 45, which is `PowerGrid`'s,
on the same cadence — `PowerGrid.cs`.) `ctx.Weather` joins `PawnContext` beside `ctx.Temperature`,
null-tolerant in fixtures (the colony-world composition pattern).

## 6. Shelter has one owner: `ShelteredFromSky(cell)`

Two facts compose, and the composition is the query — nobody else re-derives either:

1. **A roof — asked of the whole column, not of the cell above.** `CellGrid.IsRoofed` is *not*
   this rule: it asks only whether the cell one layer up carries a slab (`CellGrid.cs`), ignores
   solid terrain, and nothing tests it. `EnclosureGrid.HasRoof` adds terrain but is still one layer.
   A two-storey hall, a roof on posts over a tall ramp and a cave mouth under an overhang are all
   wrongly wet under both. Rain needs the column, which is what the render side already walks
   (`WorldRenderModel.OpenToTheSky`). So the owner is a **per-column rain-stop height**, sim-side
   (`SkyColumns`, `Sim/World`): the top of the highest slab, solid cell or water surface over each
   column, and a canopy where one is higher (below). `ShelteredFromSky(cell)` is *this cell's floor
   is below its column's stop*. **One pure function computes a column**; the simulation's map and
   the render mirror's texture (§7) both call it, and a test holds the two to the same answer cell
   by cell on the played board — the `TerraceFoot`/`BankLayout` pattern (P1). It also closes the
   P15 gap 38 §6 names: `CanBank` walks whole columns while `MarkChunksAround` dirties 3 × 3 × 3.
2. **A tree canopy — derived, never counted.** Trees are placed edifices (`TreePass`) standing in
   the air cell above their ground. A trunk lifts the columns of its 3 × 3 to its canopy height,
   about 4.5 m above that ground, whenever those columns are recomputed. No per-cell count exists,
   so none can drift. That matters because **felling has two removal paths** — `JobDrivers.FellTree`
   and `Falling.TreesOutOf` (a floor collapsing under a tree) — both call `CellGrid.RemoveEdifice`
   directly and neither sets `PlacedEdifice.Removed`, so an incremental map would need both hooked,
   and a load-time rebuild from the edifice list would resurrect every felled tree. Recomputing the
   dirty columns from the grid has neither fault. The eviction test (chop it → the 3 × 3 is wet)
   stands as named.

   Cost: a column walk stops at the first thing it meets — two or three cells on the surface —
   and runs only for the columns an edit dirtied, plus the canopy reach around them.

### 6a. As built (2026-09-25, `weather-world`)

**The rule** is `SkyColumnRule.Compute` in `Sim/World/SkyColumns.cs`, a pure function over four
questions (`ISkyColumnSource`: solid, water, slab, tree). The simulation asks them of the cell grid
(`GridSkySource`), and `SkyHeightMap` asks them of the render mirror (`MirrorSkySource`). So the
rule has one owner, and the two readers can differ only in what they are told.
- **In layers, not metres.** A column's answer is a stop layer and a kind. Every cell below the
  stop layer is sheltered.
  - A solid cell's stop is the layer above it.
  - Water and a slab are landed *on*, so a swimmer and a colonist on a roof are in the rain.
  - A canopy covers the trunk's own layer and the one above it (`CanopyLayers` 2, `CanopyReach` 1).
  - `SkyHeightMap.Metres` turns the answer into the drawing's heights: a slab's lift, a pond's
    surface, and the crown 4.5 m above the trunk's floor.
- **One consequence of layers.** A pond one layer above a trunk and within its reach now counts as
  under the crown. Comparing metres had it above. The picture follows the simulation. The case
  needs a pond on the terrace above a tree.
- **Departure 1: it is built beside `SkyLanding`, not on it.** The two rules answer different
  questions. `SkyLanding` stops at the first edifice, and a tree is an edifice. It returns −1 for
  any column it cannot land in, and a pond is one. The column rule has to walk past a trunk and
  record it, and has to land on water. Building one on the other would have meant a flag on
  `SkyLanding` that changes what it means. So there are two functions, each named for its own
  question. §6's point stands: `IsRoofed` and `HasRoof` are not used.

**How the map hears about an edit.** Every edit path already tells the `ChunkGrid` which cell
changed, so the drawing re-meshes it. The chunk grid now also keeps the *columns* touched
(`TakeEditedColumns`), and `SkyColumns` reads them lazily on the next question. Each touched
column is widened by the canopy's reach and recomputed. So the sim map and the mirror are fresh
about exactly the same edits.
- **Departure 2: every colony now has a chunk grid**, a headless one included. `ColonyWorld.Build`
  makes one when no renderer hands one in, and `AddColony` does the same for hand-built
  fixtures. It was null headless, which would have left a headless run with a felled tree's shade
  for ever. `SupportSystem` falls back to the colony's chunk grid for a collapse, for the fixtures
  that build it first.
- A load rebuilds the whole map inside `RebuildDerived`, so the first tick does not pay for it.

**Measured** (`TickBenchmarkTests.TheSkyColumnsCostWhatAnEditTouches`, Long tier, played map, one
run on the Windows machine):

| Board | Board-wide build | One slab | Columns an edit recomputes |
|---|---|---|---|
| Standard 120 × 120 × 16 | 1.33 ms | 7.5 µs | 9 (25 for an order's 3 × 3 × 3 marking) |
| Large 180 × 180 × 24 | 3.16 ms | 8.7 µs | 9 (25) |
| Huge 240 × 240 × 16 | 5.59 ms | 7.9 µs | 9 (25) |

The column counts are asserted; the times are only printed. The first board build asked
`Compute` of every column, which walks nine columns each time: 13.8 ms on Standard and 53 ms on
Huge. `ComputeBoard` walks each column once, and both readers use it.

**Pace** is `Pawn.WeatherPerMille()`, one factor in `MoveRatePerMille`'s product, before urgency.
It is `WeatherSystem.PacePerMilleAt(cell)`: the sky's pace where the sky reaches, and exactly
1,000 under cover, on a dry day, or with no weather.
- The pace is each spell's `PaceOf(def, intensity)` blended across the hand-over, like every
  other term.
- A pawn reaches the weather through `Pawn.Context`, set by the registry on adopt and on load.
- The planner is untouched. `WeatherWorldTests.TheRainIsARateAndNeverAPathPrice` plans one path
  under a clear sky and a storm and gets one cost.

**Growth** is one multiply in `PlantGrowthSystem`, after temperature: `GrowthPerMilleAt(cell,
tick)`, 1,000 plus the blended `growBonusPerMilleAtFull × intensity / 1000` on an exposed crop.

**Animals.** `AnimalShelterThinkNode` sits between the combat node and the idle node.
- It acts past 400 per mille of rain, for an animal that is not leaving the board.
- Under cover, it waits 120 ticks, the weather's own cadence, and asks again. So a felled tree or
  a removed roof is noticed within one step of the sky.
- In the open, `ShelterTarget.Find` scans square rings outward to the species' wander radius. It
  takes the nearest sheltered cell by the travel estimate, first found on a tie, in the walkable
  cell nearest the animal's layer in each column. It keeps no list of cover.
- **Departure 3: an animal sheltering reads *Wandering* and then *Resting*.** Those are the
  statuses of the two jobs it uses. The registry has no `ui.status.sheltering` and no colonist
  "In the rain", so neither was invented; both are owed (§8).

**Content** (`Weather.xml`, invented for the owner to tune). Rain and Storm both have
`moveFloorPerMille` 900 and `growBonusPerMilleAtFull` 250. The first is RimWorld's ×0.90 anchor.
The second makes a downpour grow a crop a quarter faster.

**Goldens.** Only the ruined city's simulated hash moved, because it is the only golden board that
rains inside its window: intensity 459 all run. The meadow and the played board roll a cloudy
spell that lasts their whole windows. The reason, measured with each half switched off in turn, is
in `Golden.cs`: pace moves positions and move progress, shelter moves the waits, and nothing else
moved. **So no golden exercises rain on the played board.** A golden that does would need a
seed chosen for it.

`EnclosureGrid` keeps the indoors question — a tree is not a room, and cover grants no thermal
enclosure, no room temperature, nothing but dry. Growth deliberately does **not** read canopy as
shade: sky-cover and the light model are different hooks (22 §8 keeps light separate), and a
crop under a tree is both rained on and watered only if the rain reaches it — it does not, so it
stays dry and grows at its ordinary rate. That is the honest first version of "trees shelter
crops", and the moisture seam (§9) is where it deepens.

**Animals.** `AnimalShelterThinkNode` slots ahead of `AnimalIdleThinkNode` in `AnimalTree` — the
combat-ahead-of-idle pattern. Gate: rain past 400 per-mille and the animal standing sky-exposed.
Target: a deterministic fixed-probe scan for the nearest `ShelteredFromSky` cell within the
species' wander radius, the `FindFleeCell`/`FiresideTarget` shape — it asks the cover owner and
keeps no list of its own. The animal idles there while rain holds and re-seeks when the cover
goes (the chopped tree, the removed roof). Species that do not care (waterproof hide, a
nocturnal sleeper already under cover) arrive as a `SpeciesDef` flag when a playtest asks for
one.

## 7. The look

**Rewritten on review (2026-09-24), pending the owner's choice between the two in pictures**
(`claude/rain-look`: `RainCheck` photographs both through the real renderer and the real day,
with the same cover map; research `d-20-rain-rendering.md`). The first draft drew rain with shared
`ParticleSystem`s, the campfire's shape. That is right for nine fires and wrong for rain across a
160 m view, for four reasons:

- **the CPU pays per drop**, on the main thread, in a frame whose budget is CPU submission;
- **the per-burst column sampling is C# work** in the same frame;
- **each drop needs a lifetime** cut to end at its own column's ground;
- and from a 48° camera **falling streaks are the weakest sign of rain there is**. A streak shows
  0.67 of its side-on length and moves about 2 px a frame at 160 m (d-20 §D). The ground carries
  it.

**Streaks and splashes: GPU-procedural, two draws.** `Odyssey/Rain` draws N camera-facing quads
with `Graphics.RenderPrimitives`: no mesh, no particle state.
- **Placement.** Every drop is placed in the vertex shader from its instance id and the rain clock,
  on a lattice wrapped into a box around the rig's focus. The lattice is world-anchored, so panning
  moves the box over the rain rather than dragging the rain with it.
- **Width.** Streaks are held to at least a pixel wide with their alpha given back, so they never
  sparkle, and they slant with `_OdysseyWind`.
- **Splashes** are a second draw: short crowns on hard ground and roofs, longer rings on water.
- **Cost.** The CPU cost is two calls and four globals whatever the rain. The count is a quality
  rung: 24k streaks at Ultra, a quarter of that at Low.

**Cover: the column map (§6), on the GPU.** `_OdysseySkyTex` holds one half-float texel per column
(115 KB on Huge): R is the rain-stop height, G what the rain lands on.
- A streak whose head is below its column's stop is not drawn.
- A splash is placed on the stop, so rain drums on a roof, rings a pond and never falls through a
  canopy.
- The texture is re-uploaded for the columns a dirty chunk covers.

Because the shaders and the simulation read the same column rule, the picture and the pace penalty
cannot disagree about where a roof is.

**Wet ground: the larger half of the look.** A global wetness, presentation-side and not simulated,
lags the rain: it wets over tens of game minutes and dries over hours.
- It darkens and glosses every surface the sky reaches, masked by the same texture.
- `Odyssey/MeadowGround` (every natural terrain since 38 §17) and `Odyssey/Foliage` take it in
  their forward pass, through one include, `OdysseyWeather.hlsl`, of about sixty lines.
- Puddles gather in flat patches once the ground is soaked.
- Built things drawn by the pack's own Shader Graphs (walls, roofs, paving) stay dry in v1: we do
  not own those shaders (d-20).
- **Design 38's ground work keeps the two globals** (`_OdysseyRain`, `_OdysseySkyTex`) through any
  later rework of the ground shader, so that neither track builds half of the wetness.

**Rain keeps its colour** (owner, 2026-09-25). The grade has two terms. *Cover* dims the sun
and softens the shadows and leaves the colour, and it is the whole of an ordinary rainy day. The
kind's *gloom* drains to grey and weights the grey volume, and only Cloudy and Storm carry it.
Wet ground deepens rather than darkens. The owner is choosing by eye between richer-and-slightly-
darker and gloss only (both on the debug Weather tab).

**Zoomed out, rain still reads** (owner: *"When I zoomed out I couldn't really see any rain"*).
A screen-space streak layer is the third draw of `Odyssey/Rain`: one full-screen triangle, fading
in between 55 m and 95 m of camera distance, where the 3D drops shrink to a couple of pixels a
frame. It slants with the wind across the screen and is masked by the cover map at the depth
behind each pixel, so it is never drawn into a cut-away room.

**Overcast goes through `DaylightDirector`, as one pure function.** `Overcast.Grade(state, cover)`
is applied after the hour's own state, so the time of day shows through a grey day. At full cover:
- the sun dims to 30 % and its shadow strength to 25 %;
- the sun's colour and the ambient drain towards a cool grey, and the ambient lifts;
- the sky moves towards cloud grey;
- fog is ×2.4.

A weight-blended Overcast `Volume` beside the Golden Hour one (saturation and contrast down) is
the second lever, if the grade alone reads flat. Whether heavy cloud should also stop rendering the
shadow map, the largest single term in a 4K frame (d-19), is a measurement for this PR.

**Sound belongs in this PR, not in a seam.** Rain with no sound reads as a screensaver: a loop
scaled by intensity, and a drum under a roof from the same column map.

**Pause, speed, slice, cut-away.**
- The rain clock is game seconds, as `WindDirector`'s is, so a paused world holds its drops and
  speed 3 rains three times as fast.
- A view cut below the surface (`SliceSettings.BelowSurface`) draws no rain.
- Rain stops at a roof the player has cut away from view. That is honest, because the roof is
  there, and it is a picture in the sheet to judge.

**Budget, measured rather than inferred:** CPU ≤ 0.05 ms and ≤ 4 draw calls; GPU ≤ 0.5 ms at 4K
Ultra and ≤ 0.3 ms at 1080p Low. The PlayMode arm (`FrameTimeTests.TheRainAgainstTheFrame`) runs
off, zero intensity (the negative control, P18), the particle arm and the procedural arm in one
run. The 4K and laptop figures come from a Play session.

**First reading (2026-09-24, `claude/rain-look`, one run, RTX 5070 Ti, D3D11, one other editor
open).**

| Arm | 640 × 480 | 3840 × 2160 | Submit at 4K |
|---|---|---|---|
| off | 2.17 ms | 8.38 ms | 1.748 ms |
| zero (control) | 2.06 | 8.95 | 1.838 |
| wet ground only | 2.05 | 8.59 | 1.826 |
| particles, 0.7 (11,702 drops alive) | 2.11 | 9.49 | 1.969 |
| GPU, 0.7 (16,800 + 4,900) | 2.08 | 8.67 | 1.839 |
| GPU downpour (24,000 + 7,000) | 2.14 | 9.24 | 1.927 |

The controls differ by 0.11 ms at 640 × 480 and by 0.57 ms at 4K, and that spread is the floor.
- The GPU arm at 0.7 lands inside the floor.
- The downpour is 0.29 ms above the higher control.
- The particles are 0.54 ms above it, with 30 % fewer drops than their target.
- GPU time cannot be read in a batch run, so the 0.5 ms budget stays unproven.

Pictures: https://claude.ai/artifact/BXgdcC9mYZ6MQR3DpYWLJ3

**Rejected, and why.**
- **VFX Graph**: similar GPU cost, but it adds a package and compute passes, and its depth collision
  is weaker than a lookup the grid already owns.
- **Screen-space layers**: built for a level view, and they cannot keep a roof dry.
- **A depth render from above** (*Remember Me*, *Far Cry 6*): pays for a camera to learn what the
  grid already knows.
- **Synty's `FX_Rain_*` prefabs**: licensed Shader Graphs, a GameObject each, and invisible on a
  clone without the packs.

## 8. Order of work

Three PRs, each green on both tiers and playable at the keyboard:

1. **`claude/weather-core`** — `WeatherDef` + XML, `WeatherSystem` (roll, episodes, blend,
   `WeatherOffsetC`, save/hash), `ctx.Weather`, the HUD's weather row (A3's second half; three
   rain-intensity words join the eleven `ui.weather.*` keys, wiki rebuilt in the same commit),
   debug-menu force-weather rows, tests, regolden. *The sky exists, moves the thermometer, and
   reads in the corner.*
2. **`claude/weather-world`** — `ShelteredFromSky` (roof + canopy, with eviction), the pace
   factor, the growth multiply, `AnimalShelterThinkNode`, fixture tests, TickBenchmark rows.
   *Rain touches pawns, crops and animals.* **Built 2026-09-25 (§6a).** Still owed:
   - a colonist's "In the rain" and an animal's "Sheltering" on the inspect pane, each waiting on
     a registry key;
   - rain audio, which step 3's own paragraph in §7 places here and which nothing plays yet.
3. **`claude/weather-visuals`** — grown from `claude/rain-look`: `RainDirector` and
   `Odyssey/Rain` (streaks, splashes), the sky texture read off the column map, wetness in the
   ground and foliage shaders, `Overcast` in `DaylightDirector`, the rain loop, the density rung
   in `GraphicsLadder` and the quality presets, PlayMode tests, and the frame-time arm with its
   negative control (P18). *Rain is seen, heard and felt underfoot, and only where the sky
   reaches.*

## 9. Seams left open, on purpose

- **Snow** — precipitation below 0 °C falls as snow: one new `WeatherDef` row, white streak
  material, the `OutdoorTempC` gate; no accumulation, no shovelling, until a playtest asks.
- **Fog, storms, wind gusts, lightning** — the wiki already names them (`docs/wiki/world.md`'s
  weather table carries all eleven kinds; the sim catches up kind by kind).
- **Weather incidents** — the cold snap the almanac already promises (−25 °C, 36–72 h) arrives
  as an `IncidentWorker` when a storyteller exists, commanding the weather system for the
  episode's duration (§5's one-writer rule). Until then the debug menu is the only skywriter.
- **Moisture** — a soil accumulator (rain wets, sun dries, irrigation tops up) is the real
  watering model; the growth multiply is its stand-in and its seam.
- **Apparel** — a rain coat buys the pace factor back; the factor is a per-pawn multiply, so the
  garment is one more factor in the same product.
- **Deterioration** — ×5 outdoors in rain (`a-14`), when items can deteriorate.
- **Accuracy** — RimWorld's rain ×0.80; combat's hit formula (33) has no weather term yet.
- **Firewatcher** — rain chasing fire needs fire to spread first (28 §10's fire seam).
- **Cloud cards / a skydome** — art, and a shader + keep-alive pair, only when the grey day
  reads as missing something.

## 10. Tests

The fast tier carries the model; the Unity tier carries the picture.

- **Determinism** — same seed → the same episode sequence, hash-equal across the blend; different
  seeds differ. Golden-pinned.
- **The roll** — with the weight table pinned, a long run's kind counts match it exactly (fixed
  seed, so the "statistical" test is exact).
- **The coupling** — `OutdoorTempC` moves by exactly `tempOffsetC × share × intensity / 1000`;
  a negative control with the offset def set to 0 (P11: prove the test can see nothing).
- **The blend** — ramps are integer, monotone, and land exactly on their targets at the window's
  end.
- **Save** — round-trip mid-episode resumes the blend; the hash before equals the hash after.
- **Shelter** — the `RoomEnclosureTests` fixture pattern: build a roof → dry; remove it → wet;
  a roof two layers up over a tall room → still dry (the case `IsRoofed` gets wrong); a cave mouth
  under an overhang → dry; plant a tree → its 3×3 dry; chop it → wet again, and the same through a
  collapsing floor (`Falling.TreesOutOf`) — the eviction half, P14, is a named test for **both**
  removal paths.
- **One column rule, two readers** — on the played board, every column's sim stop height equals the
  render mirror's, cell by cell, with a negative control that edits one side.
- **Pace and growth** — an exposed pawn's rate falls by exactly the factor, the same pawn under
  a roof does not; an exposed crop's gain rises by exactly the multiply.
- **Animals** — rain crosses the gate → the animal relocates under the tree; already roofed → no
  relocation; the tree is chopped mid-rain → it re-seeks.
- **The picture** — zero draws at intensity 0 and below the surface; the sky texture's texel
  under a roofed fixture equals the roof's height; the frame-time arm (§7) with its negative
  control (P18), draw calls beside every number; and `RainCheck`'s sheet for what no test can
  judge.
- **Benchmarks** — a TickBenchmark row for `WeatherSystem` (O(1); noise-level beside an empty
  tick); every new loop states in its doc comment what it scales with (process.md's rule).

## 11. Scaling, stated once

`WeatherSystem` is O(1) per evaluation and evaluates on the thermal cadence. The column map is
O(dirty columns) per edit and nothing per tick. Rain is two draws of N instances whatever the
board, N a quality rung. The overcast grade is O(1) per frame. Nothing in this design walks the
board.
