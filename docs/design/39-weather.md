# 39 — Weather: sky state the seasons roll, rain that roofs stop

**Status: designed 2026-09-24; design only, nothing built.** Branch `claude/weather-design`, worktree
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
| Kinds | **Clear, Cloudy, Rain.** Rain varies light → downpour by intensity. Snow, fog and storms stay wiki words until a follow-up (§9). |
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
- **RNG** only through `DeterministicRandom.ForTick(seed, tick, purpose)` with a purpose of its
  own, so weather rolls can never collide with another system's draws. Same seed → same sky,
  on Mono and CoreCLR (01 §7).
- **State**: active kind, previous kind, both intensities, the blend window, the episode-end tick.
  `ISaveable` (`SaveKey "odyssey.weather"`; save format 9 → 10 when it lands) and
  `IStateHashable` — anything a pass reads is saved and hashed, the `AmbientTempC` lesson. Goldens
  re-baked with a reason line in `Golden.cs`.

## 4. The roll and the episode

Content is a `WeatherDef` family in `Defs/Core/World/Weather.xml`, beside `Climate.xml` — numbers
in XML, curve shape in code (the Climate rule: *"the curve's shape is code, not content"*). Each
def carries: `tempOffsetC` (at full intensity), `cloudPerMille`, `moveFloorPerMille` (the pace at
intensity 1000; rain only), `growBonusPerMilleAtFull`, per-season weights (per-10,000, Wash ·
Glare · Rime), and duration bounds in game hours.

The invented first table:

| | Wash (spring) | Glare (summer) | Rime (winter) |
|---|---|---|---|
| Clear | 4,500 | 6,500 | 5,500 |
| Cloudy | 3,000 | 2,000 | 4,000 |
| Rain | 2,500 | 1,500 | 500 |

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

**`WeatherSystem` order:** WorldSystems phase, **Order 45** — after Enclosure 30 and Growing 40,
before Temperature 50, so the offset is current the same tick the thermal pass reads it. Cadence
120 ticks, matching the thermal pass. `ctx.Weather` joins `PawnContext` beside `ctx.Temperature`,
null-tolerant in fixtures (the colony-world composition pattern).

## 6. Shelter has one owner: `ShelteredFromSky(cell)`

Two facts compose, and the composition is the query — nobody else re-derives either:

1. **A roof.** `CellGrid.IsRoofed(index)` — the slab stored on the cell above, or solid terrain
   above. This answer already exists and is already tested; weather only asks it. This is design
   03 §6's no-drop contract, owned at last.
2. **A tree canopy.** Trees are placed edifices (`TreePass`); a trunk marks a 3×3 of ground cells
   as canopied. A sparse cover map (dictionary: cell → canopy count) is incremented when a tree
   lands and decremented when it goes — including when a colonist chops it, which is P14's
   eviction half, tested (§10). No sweep, no per-cell state over the board; cost scales with
   trees, like `GrowingZones.Planted`.

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

**Rain — two shared world-space `ParticleSystem`s, the `FireDirector` pattern exactly** (31 §4's
option B): one emitter for streaks, one for splashes, the whole colony's rain in two draw calls.
Fixed-cadence `Emit()` (streaks ≈ 0.05 s), materials built in code from the particle shaders
already on the keep-alive list — **no new shader**, `ShaderInclusion.cs` untouched, the P13 trap
never opened. Synty's rain textures (`PNB_Core` `FX_Rain_*`, `PolygonParticleFX`) may dress the
material later; their Shader Graphs never load. `Warm()` on boot, `simulationSpeed = 0` pause-hold,
slice-aware: a view sliced below the top layer shows no rain, the same discipline blood follows.

**Dry cells are an emission rule, not a clip.** Each cadence burst samples a handful of columns
near the camera; a column emits only where the sky reaches it — the presentation mirror answers
with the same two facts §6 composes (no floor above, no canopy). Nothing is spawned over a roofed
or canopied cell, so nothing has to be hidden there, and the splash cells and the streak columns
agree by construction. Cost scales with emission bursts, never with cells (P10). Intensity sets
the burst count and the streak length: drizzle is few and short, a downpour is many and long —
one number, read from the snapshot each frame.

**Overcast — through the one hook.** `DaylightDirector.ApplyHour` gains a weather term: sun
intensity scaled by (1 − cloud), sky colours eased toward a grey, ambient desaturated, fog
density raised — the fog-stays-horizon-colour identity preserved. A cloudy Wash day reads mid
and dim; clear Glare reads bright. No post-processing stack, no new volume: the day table is
already the owner of every one of those numbers (28 §10 named this the hook for a reason).

**Wind.** `WindDirector` (new on `main`) already sways foliage; rain streaks slanting with it is
a one-line read when the emitters land, noted here so it is not rediscovered.

**The upgrade path stays open.** If rain ever needs to scale past shared emitters — a storm
system with per-column volumes — 31 §4's option C is the documented road: instanced cards
through the chunk machinery, the `OdysseyWater` trick, frustum-culled for free. Shared emitters
first, because the campfire measured them and the campfire is many fires.

## 8. Order of work

Three PRs, each green on both tiers and playable at the keyboard:

1. **`claude/weather-core`** — `WeatherDef` + XML, `WeatherSystem` (roll, episodes, blend,
   `WeatherOffsetC`, save/hash), `ctx.Weather`, the HUD's weather row (A3's second half; three
   rain-intensity words join the eleven `ui.weather.*` keys, wiki rebuilt in the same commit),
   debug-menu force-weather rows, tests, regolden. *The sky exists, moves the thermometer, and
   reads in the corner.*
2. **`claude/weather-world`** — `ShelteredFromSky` (roof + canopy, with eviction), the pace
   factor, the growth multiply, `AnimalShelterThinkNode`, fixture tests, TickBenchmark rows.
   *Rain touches pawns, crops and animals.*
3. **`claude/weather-visuals`** — `WeatherDirector` (streaks, splashes, emission masking),
   the overcast grade, PlayMode tests, a FrameTime row with a negative control (P18). *Rain is
   seen, and only where the sky reaches.*

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
- **Rain ambience** — the audio framework's probe already reads wet terrain; a rain loop is a
  director away.
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
  plant a tree → its 3×3 dry; chop it → wet again (the eviction half, P14, is a named test).
- **Pace and growth** — an exposed pawn's rate falls by exactly the factor, the same pawn under
  a roof does not; an exposed crop's gain rises by exactly the multiply.
- **Animals** — rain crosses the gate → the animal relocates under the tree; already roofed → no
  relocation; the tree is chopped mid-rain → it re-seeks.
- **The picture** — zero emissions at intensity 0; a roofed fixture world emits nothing under
  the roof (asserted against the sampled columns); a FrameTime row for rain on/off with its
  negative control (P18).
- **Benchmarks** — a TickBenchmark row for `WeatherSystem` (O(1); noise-level beside an empty
  tick); every new loop states in its doc comment what it scales with (process.md's rule).

## 11. Scaling, stated once

`WeatherSystem` is O(1) per evaluation and evaluates on the thermal cadence. The canopy map is
O(trees). Emission is O(bursts) — bursts scale with intensity, never with board size. The
overcast grade is O(1) per frame. Nothing in this design walks the board.
