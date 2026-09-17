# A6 — temperature and environment

## Question

OQ-30. How do colony sims model temperature and environment: heat sources and sinks, insulation,
how temperature equalises between enclosed spaces and with the outdoors (the update model *and its
cadence*, not just the concept), outdoor temperature by season and latitude, and light and glow
(sources, falloff, how "lit" is decided, and what light feeds).

Then the second half of layer question 3: **does heat rise?** Our standing position — which this
file tests rather than defends — is that rooms are per layer and heat moves between layers as a
*flow through an opening* rather than by merging two layers into one taller room, so that room
detection stays a cheap 2D flood fill per layer. Going Medieval is the nearest precedent and is
checked against. What must a layered game model for heat to feel right: does it need buoyancy at
all, is a per-opening flow enough, and what does underground thermal mass do to the model?

## Findings

### 1. There are only two families of temperature model, and they differ by orders of magnitude in cost

**Per-cell diffusion.** Oxygen Not Included and Dwarf Fortress both give every tile (and, in Dwarf
Fortress, every *item*) its own temperature and run a finite-difference exchange with neighbours.

- ONI's exchange per tick is `q = ΔT × Δt × k × multiplier`, with `Δt` always one tick of 0.2 s
  (5 ticks per second), `k` the thermal conductivity in DTU/m/s/°C, and the multiplier depending
  on how the two materials meet. The two conductivities are combined by one of four rules
  depending on the interaction — minimum, geometric mean, arithmetic mean, or half the product.
  Insulated building components divide transfer by 20; doubling thickness halves transfer.
- ONI clamps each tick so that **no object changes by more than a quarter of the temperature
  difference**: with a 40 °C difference each side may move at most 10 °C in a tick. Formally
  `q_max ≤ min((T1−T2)/(4·m1·c1), (T1−T2)/(4·m2·c2))` DTU per tick. Backwall cells use a stricter
  1/100 limit. This clamp is what keeps an explicit Euler integrator from oscillating.
- Dwarf Fortress stores temperature as a 16-bit unsigned value (0–60,000 °U) and, every tick, moves
  an exposed item by `(T_environment − T_item) / specificHeat`. Lignite (specific heat 409) next to
  magma (12,000 °U) gains 4.85 °U in the first tick and needs about 517 ticks to reach ignition.
  Tiles adjacent to magma are pinned at 10,075 °U.

The cost is not theoretical. Dwarf Fortress lets you **switch temperature off entirely** in
`d_init.txt` as a framerate measure, and DFHack ships `fix/stable-temp` purely because a
one-degree rounding error between an item and its container makes both recompute forever. Moving
or pumped magma is a documented major FPS drop. ONI's heat sim is effectively the game's frame
budget, on a 2D map of roughly 256 × 384 cells.

**Per-room scalar.** RimWorld and Going Medieval give each enclosed region a single temperature and
exchange across the envelope. RimWorld runs the whole thing at **one pass per 120 ticks** — two
seconds of game time at speed 1 — on a few hundred rooms. That is the only reason temperature is
affordable at all next to everything else RimWorld does per tick.

For us this is not a close call. A 250 × 250 × 40 board is 2.5 M cells. Per-cell diffusion at even
1 Hz is out of the question when the tick budget is 5 ms and A* already eats 65% of it. Everything
below assumes the per-room family.

### 2. RimWorld's model, in numbers

**Rooms.** A room is a flood fill of cells fully enclosed by impassable things — walls, doors (open
or closed), vents, natural rock, coolers. Corners need not be filled. There is a size ceiling: a
room may span at most 36 map regions, in practice about a 50 × 50 empty square, beyond which the
space is "indoors but not a room". Three separate outdoor tests exist and they deliberately do not
agree with each other:

| Test | Trigger | Feeds |
|---|---|---|
| uses outdoor temperature | at least 25% unroofed, or open to the map edge | temperature |
| outdoors for work | more than 25% unroofed, or more than 100 unroofed cells | work and roofed checks |
| psychologically outdoors | 300 or more unroofed cells, or edge-open criteria | mood |

A room that uses outdoor temperature does not relax toward the outdoors — it **snaps to it on the
next tick**. There is no gradual case.

**Cadence.** Wall and roof equalisation runs once per 120 ticks. Doors are on their own clock:
**375 ticks when closed, 22 ticks when open**. Vents, coolers and open doors are described as
equalising "at a very high rate" — effectively merging the two rooms.

**The envelope law.** The wiki publishes measurements at an absurd driving difference of
−9,555.208 °C so that the linearity is visible:

| Surface | Room | Change per 120 ticks | As a fraction of the difference |
|---|---|---|---|
| single wall | 1 × 1 | −161.470 °C | 1.690% |
| single wall | 5 × 5 | −32.494 °C | 0.340% |
| double wall | 1 × 1 | −81.234 °C | 0.850% |
| double wall | 5 × 5 | −16.247 °C | 0.170% |
| thin roof | size unstated | −57.331 °C | 0.600% |

Two things fall straight out, and I flag them as **inference from the published measurements, not
from source**:

- Exchange is a **fixed fraction of the difference** — exponential relaxation, not a fixed rate.
- The fraction falls with room size exactly as perimeter-over-area would predict. A 1 × 1 room has
  perimeter 4 over area 1; a 5 × 5 room has 20 over 25. The ratio 4 ÷ 0.8 = 5 matches the measured
  1.690 ÷ 0.340 = 4.97. A single coefficient of about **0.42% per 120 ticks per unit of
  perimeter/area** reproduces both rows. The wiki's separate remark that a square room loses heat
  more slowly than a thin rectangular hallway is the same law seen from the side.
- The **roof** fraction does *not* shrink with size, because roof area scales with floor area — so
  the roof term is size-independent while the wall term is not. That is why roofing matters far
  more than wall thickness in a large room.

**Insulation is close to binary.** Wall *material* does not matter at all. A double wall halves the
wall term; a third layer adds nothing. Double doors behave like double walls. A thick rock roof
equalises at the same rate as a thin roof but adds a small extra cooling pull when the room is at or
above 15 °C — a cheap stand-in for earth thermal mass rather than a real one.

**Active devices push energy, not temperature.** A heater targets about 20 °C and is described as
raising a single square by roughly 1,800 K but only about 36 K in a 50-square room: the device adds
a fixed energy per equalisation tick and the temperature change is that energy divided by cell
count. A cooler is a heat pump with a hot side and a cold side (20 W idle, no cooling). A campfire
caps at 30 °C and burns wood; a passive cooler pulls to 17 °C and lasts about five days.

Incidental heat, published per second: electric crematorium 12, electric smelter 9, fuelled stove 4,
electric stove 3. Pawn body heat is `0.3 × bodySize × 4.1666665` — about 1.25 × body size for a
human — and is **only applied while ambient is below 40 °C**, which is a neat trick for stopping a
crowded room running away.

**What temperature feeds.**

- Pawns: comfortable 16–26 °C unclothed; more than 10 °C outside that band starts hypothermia or
  heatstroke; 150 °C above comfort starts burn damage.
- Workbenches: comfortable 10–35 °C; outside that, **work speed × 0.70**.
- Food: spoilage rate is `T / 10` between 0 and 10 °C (so 0.5× at 5 °C) and stops at 0 °C.
- Plants: slowed below 6 °C and above 42 °C, stopped at 58 °C, most die around −10 °C.
- Fire: items and pawns spontaneously ignite at 235 °C and above. Game maximum is 1000 °C; the
  internal range is about −270 to 1000 °C.

**Outdoor temperature.** Varies with latitude, biome, day of the year and time of day. The year is
60 days: four quadrums of 15 days, subdivided into twelve 5-day "twelfths", which are the unit the
game's own forecasting works in. Hemispheres are opposed; equatorial tiles stay temperate all year.
Events (cold snap, heat wave, volcanic winter) offset the curve. **The actual function is not
published** — the wiki says outright that the relationship between season, latitude and day length
is unknown — though every tile exposes an annual average, a summer figure and a winter figure, which
strongly implies a mean plus a seasonal term plus a daily term.

### 3. Light and glow

The developer's own account is unusually direct: *there are no "real" lights in the entire game.*

- **Propagation.** Each point light floods the grid with **Dijkstra's algorithm on path distance**,
  so light bends around corners and is blocked by walls without any ray casting, and attenuates
  along the path it actually took.
- **Falloff** is a mixed function combining a linear and a quadratic term.
- **Rendering.** Glow values are written as **vertex colours on a tessellated sheet laid over the
  whole map**, with a shader that multiplies whatever is beneath it by the vertex colour, or by the
  global sunlight colour where there is none. The sheet is cut into pieces so it can be updated in
  parts rather than wholesale.
- **Bands.** Dark 0–29%, lit 30–89%, brightly lit 90–100%.
- **Radii** (cells): torch lamp 6.48, standing lamp 8.30, sun lamp 11.72, flood light 17.46.
  Ordinary lamps only reach **50% glow**, deliberately below the crop threshold, which is the whole
  reason a sun lamp exists as a separate building.
- **What light feeds.** Move speed and work speed both fall smoothly to **80% at 0% glow**. A
  sustained-darkness mood thought applies while awake but not while asleep. Surgery wants 50% or
  better. Plants need 51% or more (ornamentals 30%, mushrooms exactly 0% and they die in light),
  and growth scales **linearly between a per-plant minimum and optimal glow**:
  `GRF(L) = (light − growMinGlow) / (growOptimalGlow − growMinGlow)`.
- **Daylight by season and latitude** is a stepped curve, not a smooth sine: at central latitudes a
  day runs roughly 4 h lit, 7 h brightly lit, 17 h lit, 20 h dark. Extreme latitudes lose the
  full-dark or full-bright portions entirely.
- Plant growth overall:
  `growTime = 60,000 × growDays / (32,500 × GRF(fert) × GRF(temp) × GRF(light))`, where 32,500 is
  the growing ticks per day — plants rest from hour 19 to hour 5, so only 13 of 24 hours count.
  Temperature enters as `T/6` below 6 °C, 1 between 6 and 42 °C, and `(58 − T)/16` above 42 °C.

### 4. Going Medieval — the layered precedent, and it answers our question the other way

Going Medieval is per-room, layered, and the closest thing to our problem that has shipped.

- Temperature is computed **independently per room**; heat sources affect only the room they sit in.
- **Volume and materials, not depth, are the levers.** Smaller rooms stay warmer. Floors and walls
  carry insulation ratings (wicker grated floor 0.05, wood floor 0.75). A single material type
  contributes only up to a cap, so *mixing* materials raises the total rating — an unusual rule that
  exists to push players into layered construction.
- **Stairs do not separate rooms.** Community reports are consistent: dig down without a door and
  the two levels count as **one room, with the temperature averaged across floors**. Players also
  report that a three-storey space "loses" its heat while two storeys is fine.
- **Underground tracks the outdoors, damped, not pinned.** A cellar was reported at 2 °C with 32 °C
  outside. Dirt above a room insulates better than a wooden floor above it.
- Two player-observed weaknesses are worth learning from: heat transfer *between* rooms is only
  weakly modelled, and the model appears not to be hysteretic — sealing a room recomputes an
  equilibrium from current factors rather than carrying the temperature the space already had.
  Players notice both.

So Going Medieval's answer to "does heat rise" is **no — it merges and averages**. And its own
players report the failure mode that follows: a tall stack loses the sense that the top is warm and
the bottom is cold, which is precisely the fantasy a vertical game is selling.

### 5. Underground thermal mass

Three shipped answers, in increasing fidelity and cost:

1. **Dwarf Fortress:** underground is pinned at a constant (about 10,015 °U) regardless of season.
   Free, legible, and it completely kills seasonal cellar play.
2. **RimWorld:** a thick rock roof adds a small cooling pull above 15 °C. Barely a model; it exists
   so that mountain bases are not ovens.
3. **Going Medieval:** earth above a room is simply more insulation, so a cellar lags the surface.

The physics all three approximate is well behaved and cheap to express: below roughly 5–10 m, ground
temperature converges to the **annual mean** air temperature, with the seasonal swing damped by
about `exp(−depth/δ)` and lagged in phase. At our 3.0 m per layer, two layers down is 6 m and the
annual swing is already mostly gone. That is a gift: a **depth-indexed ground temperature curve** —
one array of 40 floats, recomputed once per game day — is both physically defensible and effectively
free, and it means **rock never needs a per-cell temperature at all.** The rock *is* the boundary
condition.

## Recommendation

**Ranked options.**

1. **Per-layer rooms, explicit per-opening conductances, and a buoyancy-asymmetric conductance on
   vertical openings, over a depth-indexed ground boundary.** ← backed.
2. Merge vertically into one thermal volume wherever a stair opening exists (the Going Medieval
   answer). Rejected: it breaks the cheap 2D-per-layer flood fill — every stair built or mined
   re-keys rooms across layers — and it deletes the one sensation a vertical game owes the player,
   the cold cellar under the warm hall. Going Medieval's own players report exactly that loss on
   tall builds.
3. Per-cell diffusion on a coarse lattice (say 2 × 2 × 1 blocks) underground only. Keep in the back
   pocket for a magma or geothermal feature; not now.
4. Full per-cell diffusion. Reject outright — 2.5 M cells, and both games that do it pay for it
   visibly.

**The proposed model.**

- A `ThermalCell` (our name, one per enclosed per-layer region) holds one scalar air temperature and
  a cell count. Heat capacity is just `cellCount` — every cell is the same volume by construction,
  which is a real advantage of a fixed cell size. Reuse whatever per-layer regions the existing
  flood fill already produces; do not build a second partition.
- An unenclosed region is **not integrated at all**. It reads the outdoor curve. Adopt RimWorld's
  threshold in shape: at least 25% unroofed, or touching the map edge, means outdoor, and it snaps
  rather than relaxes. The gradual case is not worth having.
- One pass every **120 ticks**, matching RimWorld's cadence and our existing tick groups. Rooms are
  visited in **stable id order** — this is the determinism risk, since rooms are rebuilt by flood
  fill and their ids must not depend on iteration accidents. Temperature is simulation state, so it
  belongs in the save and in the state hash.
- Each pass is explicit Euler over cached surfaces:
  `ΔT_room = Σ_surfaces conductance × (T_other − T_room) / cellCount`
  with surfaces cached per room and invalidated on build or mine — the same invalidation the region
  rebuild already needs. Cost is O(rooms + surface runs), not O(cells).
- **Surfaces are typed, and their areas scale differently, which is the whole behaviour.** Wall
  surfaces scale with perimeter, so their effect shrinks in a big room; roof and floor surfaces
  scale with area, so their effect is size-independent. That asymmetry comes out for free if we sum
  real surface cells, and it is what RimWorld hard-codes. We should *derive* it, not copy constants.
- **Conductance is per surface-cell and lives on the material Def.** Unlike RimWorld, let material
  matter — we already have a Def-driven material system and "stone wall versus wood wall" is a
  legible decision. Take Going Medieval's idea that flooring insulates, but drop its
  cap-per-material-type rule, which is arbitrary and is why its players cannot predict it.
- **Vertical openings — the buoyancy answer.** A stair well, ladder well or hole in the floor is a
  surface between two rooms on adjacent layers with an **asymmetric conductance**: `k_up` applies
  when the *lower* room is warmer (warm air climbing), `k_down` when the *upper* room is warmer.
  Start at **k_up : k_down = 4 : 1**, both on the Def. That is one branch and one multiply per
  opening per pass, and it is the entire buoyancy model.
- **Ground contact is a boundary, not a room.** Add `GroundTemperature(z)`:
  `T_ground(z) = T_annualMean + (T_outdoorSeasonal − T_annualMean) × exp(−depth(z)/δ)`, with δ tuned
  so the swing is about 10% by 8–10 m — three or four of our layers. Buried wall, floor and ceiling
  cells exchange with `T_ground(z)` instead of with the outdoors. This is what buys cellars, a
  survivable deep mine and a reason to dig, for one 40-float array recomputed daily.
- **Sources push energy, not temperature.** `heatPerSecond` on the Def; `ΔT = energy / cellCount`.
  This reproduces the "a brazier in a broom cupboard is an oven" feel with no extra machinery. Pawn
  body heat as a small per-pawn term, gated off above a hot threshold, exactly as RimWorld does.
- **Clamp every pass to a quarter of the driving difference** — ONI's rule. It is the cheapest known
  guarantee that an explicit integrator on a coarse cadence will not oscillate or overshoot, and it
  is deterministic.
- **Light**, per layer: a Dijkstra flood on path distance with a mixed linear and quadratic falloff,
  so walls block and corners bleed, with no ray casting. Add **one vertical term we need and
  RimWorld does not**: an open floor cell (a stair hole, a mined-through ceiling) passes glow —
  daylight or lamp — one layer down at a reduced factor, seeded as a glow source on the layer below.
  Bands at 30% and 90%. Feeds: work speed down to 0.8 at pitch dark, plant growth linear from
  minimum to optimal glow, and a darkness mood thought while awake. **Do not let light feed
  pathfinding** — that is a second grid dependency on the hot path we cannot afford.

**If options 1 and 2 look tied, the tie-breaker is one observation:** can a player build a
two-storey hall in which the upper floor is measurably warmer than the lower? If not, the asymmetry
is decoration and we should take Going Medieval's cheaper merge.

**Cheapest experiment to get it** (headless, no art, a few minutes of runtime): a 6 × 6 room on
layer 0 with a fixed source, a 6 × 6 room directly above on layer 1, joined by a single-cell stair
opening and nothing else. Run one simulated day and print both temperatures at every pass. Run it
three times — `k_up = k_down` (a plain flow), `k_up : k_down = 4 : 1` (the proposal), and with the
two rooms merged and averaged (Going Medieval). If the 4:1 curve is not visibly separated from the
1:1 curve, the asymmetry is not earning its branch. The same harness also measures the pass cost,
which is the other number we need.

## Layer questions touched

**Layer question 3, second half — does heat rise?**

**Yes, but it does not need buoyancy.** It needs *one asymmetric number on vertical openings.*

Our standing position survives contact with the precedent, and the precedent strengthens it. Going
Medieval — the only shipped layered colony sim with per-room temperature — chose the other branch: a
stair opening merges the levels into one room and averages them. Its own players report the
consequence, that a tall stack "loses" its heat and that upper and lower floors read the same. That
is the design smell. In a game whose whole proposition is the vertical axis, the cellar being cold
while the loft is warm is not a detail; it is what the axis is *for*.

Concretely, and to be implemented as written unless the experiment above says otherwise:

- Rooms stay **per layer**. Room detection stays a 2D flood fill per layer. No vertical merging,
  ever. This is load-bearing both for cost and for the rebuild path when somebody mines a stair.
- A vertical opening — stair well, ladder well, hatch, hole — is an ordinary surface between two
  `ThermalCell`s on adjacent layers, contributing `conductance × ΔT / cellCount` to each.
- Its conductance is **direction-dependent on the sign of the difference, not on which room is
  which**: `k = (T_lower > T_upper) ? k_up : k_down`, with `k_up / k_down` about 4. Warm air climbs
  freely; cold does not fall as fast. That is the whole of buoyancy as far as a player can tell.
- No vertical velocity, no momentum, no per-cell air, no stratification within a room. A room is one
  number. If we ever want a stratified room we will want per-cell, and we will not want that.
- **Underground thermal mass changes the model more than buoyancy does.** A depth-indexed ground
  temperature that damps toward the annual mean removes the need for rock to have a temperature at
  all, and it is what makes digging down mean something — cool storage in summer, a survivable
  workspace in winter, and an actual reason the deep mine is not the same as the surface. Get this
  in before touching the buoyancy ratio; it is cheaper and it is worth more.
- A practical consequence for the seams work: the thermal pass needs the *surfaces* of a region
  (wall runs, roof cells, floor cells, opening cells), not just its cells. Whatever produces regions
  should produce surface runs in the same pass, cached and invalidated together. Worth knowing
  before the region code is touched again.

## Sources

- <https://rimworldwiki.com/wiki/Temperature> — the 120-tick cadence, the 375/22-tick door clocks,
  the measured wall and roof equalisation table, heat outputs per second, comfort and spoilage bands.
- <https://rimworldwiki.com/wiki/Rooms> — room enclosure rules, the 36-region size ceiling, and the
  three separate outdoor tests (temperature at 25% unroofed, work, psychological).
- <https://rimworldwiki.com/wiki/Light> — the 30% and 90% light bands, source radii, the
  80%-at-darkness work and move penalty, per-plant minimum glow.
- <https://rimworldwiki.com/wiki/Plants> — the growth-rate formula and the linear light and
  temperature response curves, plus the 13-hour growing day.
- <https://ludeon.com/blog/2013/08/sun-shadows/> — the developer on Dijkstra glow flooding, mixed
  linear and quadratic falloff, the vertex-colour glow sheet, and "no real lights in the entire game".
- <https://oxygennotincluded.wiki.gg/wiki/Thermal_Conductivity> — `q = ΔT × Δt × k × multiplier`, the
  four conductivity combination rules, and the quarter-of-the-difference per-tick clamp.
- <https://oxygennotincluded.wiki.gg/wiki/Guide/Heat_Transfer> — conductivity taken as the minimum of
  the pair, insulated components dividing transfer by 20, and the thickness-halving rule.
- <https://dwarffortresswiki.org/index.php/DF2014:Temperature> — per-item temperature,
  `ΔT / specificHeat` per tick, the constant underground temperature, and disabling temperature for
  framerate.
- <https://docs.dfhack.org/en/latest/docs/tools/fix/stable-temp.html> — a tool that exists solely
  because per-item temperature recomputation is a standing framerate drain.
- <https://goingmedieval.fandom.com/wiki/Thermal_Insulation> — per-room temperature, material
  insulation ratings, the cap-per-material-type rule. *Returned HTTP 402 to a direct fetch; the
  content used here came from the search index summary, so treat its specific numbers as
  indicative.*
- <https://steamcommunity.com/app/1029780/discussions/0/3163209341708851792/> — Going Medieval
  players on per-room calculation, doors, and underground tracking the outdoors.
- <https://neitsa.github.io/games/rimworld/preparelanding/temperature_tab.html> — the 60-day year in
  twelve 5-day "twelfths", per-tile average/summer/winter temperatures, the −270 to 1000 °C range.

## Confidence

- **High** on RimWorld's published cadences and measured equalisation table, on the ONI transfer
  equation and its quarter-difference clamp, on Dwarf Fortress's per-item model and its documented
  cost, and on RimWorld's light bands, radii and effects.
- **High** on the recommendation's shape. Per-room over per-cell is not a close call at 2.5 M cells,
  and the cost evidence from the two games that chose per-cell is unambiguous.
- **Medium** on the inferred perimeter-over-area law for wall equalisation. It reproduces both
  published rows to within about 1% and matches the wiki's own qualitative remark about hallways,
  but it is arithmetic on two data points, not a reading of the rule.
- **Medium-low** on Going Medieval's specifics. Its wiki was unreachable directly, and the
  load-bearing claim — that stairs merge rooms and temperatures average across floors — is
  community-reported and may have changed between versions. It is consistent across several
  independent reports, which is why I am willing to reason from it, but the experiment above should
  confirm the *design* lesson rather than the *implementation* claim.
- **Low** on anything about RimWorld's outdoor temperature function. Nothing quantitative is public.

## Could not be determined

- **RimWorld's outdoor temperature function** of latitude, season and time of day. The wiki states
  explicitly that the relationship between season, latitude and day length is unknown. Only the
  per-tile average/summer/winter triple is exposed. We will have to author our own curve; that
  triple is a reasonable shape to author *to*.
- **The actual equalisation coefficients in RimWorld's code.** A decompiled copy of the relevant
  file is indexed and was returned by a search. **I did not open it**, under the clean-room rule.
  Everything numeric above comes from the public wiki's black-box measurements. If we ever want
  exact constants we should measure them in-game ourselves, not read them.
- **The rate for open doors, vents and coolers.** Described only as "a very high rate". Whether it
  is a true merge or a large finite conductance is not published.
- **The room size used in the wiki's roof equalisation measurement**, which is why the roof row is
  given as a bare fraction above rather than folded into the perimeter-over-area fit.
- **Whether Going Medieval models any buoyancy at all.** No developer statement was found either
  way; the merge-and-average behaviour is inferred from player reports.
- **Heat output in watts for most RimWorld generators and machines.** Only a handful of production
  buildings publish a per-second figure; generators are described only as producing "a
  non-negligible amount".
- **How ONI's four conductivity combination rules are assigned** to particular interaction types.
  The rules are listed; the mapping is not.
