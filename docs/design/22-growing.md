# 22 — Growing zones, and the carrot

**Status:** designed 2026-09-18 on `claude/growing-zones` (worktree `D:\code\odyssey-growing`),
built across units U46–U50. The interview that settled the scope is summarised in §1; the clean
room it reads is `docs/research/a-08-plants-growing-food.md` (growth model) and its 2026-09-18
follow-up (the zone loop).

**Read this before touching** `PlantDef`, `GrowingZones`, `PlantGrowthSystem`, the sow or harvest
job drivers, `Defs/Core/World/Plants.xml`, or the carrot rows in `Items.xml` / `Jobs.xml` /
`WorkTypes.xml` / `Skills.xml`.

## 1. What ships, and what the owner chose

Growing is the colony's first renewable food: paint a zone on fertile ground, and colonists sow
it, the crop grows by itself, they harvest it, and the cell is sown again — forever, with no
order from anybody. One crop, eaten raw. The owner answered four questions and took every
recommendation:

| Question | Answer | The runner-up it beats |
|---|---|---|
| First crop | **Carrot** — a raw food eaten straight from the field | A cooked crop, which would have dragged cooking, bills and a station into a unit about plants |
| Zone loop | **Continuous** — re-sow after every harvest, no per-sowing orders | One-shot sowings, which make the player a sowing clerk |
| Growth rule | **Daylight window** — grows only during lit hours, sowing gated on terrain fertility | Full-time growth, which is invisible (nothing to watch) and season-less |
| Scope | **Minimal** — cooking, spoilage, seeds, watering, seasons, temperature, light and greenhouses all stay out (§8) | A vertical slice of the whole food economy |

## 2. The content, every number in one table

A new Def kind, `PlantDef`, joins `TerrainDef`, `OreKindDef`, `ItemDef`, `JobDef`, `WorkTypeDef`
and `SkillDef`. Registered through `WorldContent.Register` alongside terrain; loaded from
`Defs/Core/World/Plants.xml`; arrays filled **by defName**, never by table order, like every
other content table.

| `Plant_Carrot` | | why that number |
|---|---|---|
| `growTicks` | **130,000** growing-window ticks | Four calendar days at full daylight (§3). Rice-class is 3 listed days ≈ 5.5 real; the carrot sits just above it so the first harvest lands after the starting pantry has proved itself, and a quarter of the bar fills on day one so progress is *visible* |
| `sowWorkTicks` | **170** | The rice-class sow figure. Includes clearing the cell's grass: sow work is one number, terrain unchanged (the tilled rows are drawn, never simulated — §6) |
| `harvestWorkTicks` | **200** | The rice-class harvest figure |
| `yield` | **5** carrots | One sowing = 900 need units = exactly one meal-equivalent (§5) |
| `minFertility` | **70** | Admits Grass (100) and BareEarth (85); refuses Gravel (55), Marsh (40), Rubble (20), Sand (8), water and rock (0). The plan sketch said 50; **70 is the decision**, because at 50 the gravel under the terrace risers becomes plantable and carrots in gravel read as a bug. The rice precedent (70%) is the same number for the same reason |

`Item_Carrots` joins `Items.xml`: `nutrition` **180** (a colonist burns 1,600 units a day, so
one carrot is 0.1125 of a day and the numbers divide without remainders that matter), `stackLimit`
**40** (a harvest fits one cell's remainder beside any stack), haulable. Eating and hauling it
cost **zero new code** — the eater scans for nutrition and the hauler for haulability — but the
fingerprints in `WorldContentDefTests` and `PawnContentDefTests` are re-baked, because a new
commodity is exactly the accidental change they exist to catch.

The growth stages are thresholds of growth, not days: **stage 1 below ⅓ grown, stage 2 below ⅔,
stage 3 from ⅔**. Harvestable only at full growth (the reference's 65% early-harvest rule is a
hook, §8). Three drawn stages — sprout, half-grown, mature — map to the Farm pack's small,
medium and large carrot prefabs, with primitive fallbacks on pack-less checkouts.

## 3. Growth: a window, a counter, and three re-meshes

**The daylight window is ticks of day 15,000–47,499 inclusive** — hours 06:00 to 18:59 on the
60,000-tick day, 32,500 growing ticks, the reference's 13-hour growing day and its 1.846×
listed-days multiplier exactly. It is a property of the clock, not of the sky: v1 has no light
model, so the window stands in for one and refuses to pretend otherwise (§8, the light hook).

Growth is **an integer counter of accumulated growing-window ticks**, 0 at sowing, ripe at
`growTicks`. `PlantGrowthSystem` runs in the Rare tick group (every 250 ticks), and on each run
inside the window adds 250 to every planted cell's counter — integer, order-independent, no RNG,
nothing to salt. Out of the window it costs one comparison. The scan is over the sparse list of
planted cells only: a zone's cost is O(planted), not O(zone), and an empty zone costs nothing —
which is why the three standard ten-day seeds are unaffected by this unit's existence.

`ChunkGrid.MarkDirty` fires **only when a cell's stage bucket changes**: sowing, ⅓, ⅔ and the
harvest that empties the cell — three re-meshes across a crop's lifetime plus its two ends.
Growth inside a bucket re-meshes nothing, so a 2,000-cell field at noon re-draws exactly as much
as the meadow next to it.

## 4. Zones: authored state, merged on contact

`GrowingZones` copies `ColonyItems`' stockpile shape: a list of zone records — plant handle plus
a sorted cell list — with the per-cell crop state in parallel arrays beside them (sorted planted
cells, parallel growth counters). It is `ITickable` (never ticks; the growth is a system),
`IStateHashable`, `ISaveable` under `"odyssey.zones"`, and an `ISnapshotContributor`. **Crops are
saved and hashed**: a growing field is as much authored state as the zone it stands in, and a
save/load that reset the carrots to seedlings would be a silent four-day theft.

The intents, one per gesture:

- **`DesignateZone`** (A carries the plant handle) — a cell joins the neighbouring zone of the
  same plant (eight-neighbourhood) or founds a new one. Contact merges records, so a field is one
  record and the work givers scan one list; a second crop planted against the first stays a
  second record, which is what keeps the per-zone plant honest.
- **`CancelZone`** — removes the cell from its zone. The crop standing there goes with it: a
  crop exists only inside its zone, so one rubber stroke kills field and fence together. An
  emptied record dissolves.
- The siting gate at designation: walkable ground terrain, `fertility ≥ minFertility`, no
  edifice, not water, **not roofed**. The zone cell is the standing cell — the air above the
  turf, where the pawn and the crop and every tree are — and its own terrain is air, so the
  fertility question is asked of the solid cell *below*, exactly as the tree roots in ground
  it does not stand in. (Found by the first test: reading the standing cell refused every
  field on every board, because air is fertile nowhere.) The roof refusal is the light hook's v1 shape — until
  light is computed, a slab over a field would otherwise ripen carrots on schedule in the dark,
  and that lie is worse than a refused drag. When light lands, the refusal becomes a computation
  and the gate moves to the grower.

## 5. Work: two jobs, and the loop closes itself

`Work_Growing` (order 1, after Construction and ahead of Cutting — a zone's daylight window is
the one clock that will not wait), `Skill_Growing`, `Job_Sow` and `Job_Harvest`
(driver ids 10 and 11). Neither JobDef carries `workTicks`: like mining, the work is priced per
plant, the driver reading `sowWorkTicks` or `harvestWorkTicks` from the zone's `PlantDef` as it
swings. Skill and experience ride the def defaults. **Growing carries no rate curve** (2026-09-18,
recorded at the rates merge): every driver now pays work at the pawn's own rate —
`Pawn.WorkRatePerMille(WorkType)`, banked as milliwork per WS1's one-unit rule — and with no
`rateSkill` on `Work_Growing` that rate is the flat tuned speed, so a skill still buys nothing at
the hoe. §3a of `17-rates-and-stats.md` is where the plant-work curve lands.

Both drivers are `FellJobDriver`'s shape with one simplification: **the colonist stands in the
cell, not beside it.** A crop is ground, not an edifice — it blocks nothing, so the stand-beside
stance and its reach check have nothing to guard. Walk to the cell, work for the def's price,
and on the last tick defer the structural write (§ of the standing rules: anything structural
goes through the deferred phase): sowing marks the cell planted at growth 0 and marks the chunk;
harvesting spawns the yield at the cell — `NearestCellWithSpace` within three cells, the felling
precedent, so a full cell passes its carrots to a neighbour instead of losing them — and clears
the planting.

**The sow kneels rather than chops** (owner, 2026-09-18: *"the animation for sowing seeds should
not be chopping axe — reuse the pickup animation where the colonist goes to knees and holds for a
while, then comes to feet"*). `SowJobDriver` reports no work focus, so the computed tool swing —
which `IndexForJob` defaults every unknown job to, the axe — never plays and no tool appears in
the hands; instead the toil begins `PawnGesture.Sow`, the pickup's own solved kneel re-timed
(`Gesture.Sow`: down quickly, a hold stretched over most of the motion, up slowly) so the hold
*is* the work. The gesture runs on its own clock timed against the carrot's `sowWorkTicks` at the
tuned rate — the one approximation in the reuse, accepted "for now" with the owner's own words.
Harvest keeps its swing: cutting a ripe crop is a cut, and nothing was asked of it.

The continuous loop needs no code beyond that: a harvested cell is an unplanted cell in a zone,
which is exactly what `SowWorkGiver` scans for, so it re-enters the queue the same tick. Resow
is not a feature; it is the absence of one. The givers reserve the crop cell (the felling
convention), so two growers never swing at one square.

Eating: `CriticalNeedsThinkNode.TryEat` already scans for any item with nutrition, so a hungry
colonist walks to the field's edge pile and eats a carrot with no new thought node. **The
arithmetic that makes this a food economy, not a decoration:** one sowing yields 900 units; a
colonist burns 1,600 a day; one tile yields 225 a day; **~7 tiles feed one colonist**, so the
demo field is 40 cells for the five-colonist scenario with margin. That is better than the
reference's 16.5-tiles-per-colonist rule of thumb, and it is the number to retune first if
carrots ever feel like a free win.

Cancel: the cancel tool gains `CancelZone` as a third intent per cell — zone first, then
designation, then nothing. No new tool, no new mode; the rubber already in the player's hand.

## 6. Presentation

The crop draws through the module id indirection like everything else: the `PlantDef` carries a
module id per stage (`odyssey.module.carrot.s/m/l`), `ModuleCatalogue` resolves them to the Farm
pack's three carrot prefabs, and a pack-less checkout draws primitives — the same fallback the
trees ride, and the reason the Unity tier on a clone is a real test of this unit rather than a
formality. Emission is bucketed per species × stage like the tree buckets; a stage bucket change
is the only thing that re-meshes (§3).

**What U48 actually built, and the three decisions in it that the plan did not settle:**

- **The mirror is fed between ticks, not contributed.** The geometry mirror is filled by a
  snapshot contributor because it reads the cell grid; a crop is not in the grid, and the crop
  channel already rides the snapshot. `WorldRenderModel.UpdateCrops` restamps the per-cell crop
  bytes from it once a frame, before the renderer runs and after the tick has published, by a
  merge walk over the two sorted lists — O(planted), which is what a 2,000-cell field asks of a
  frame. The mirror raises **no** dirty marks: the simulation knows which stage transitions
  change what is drawn, and it marks.
- **The fallback is a pillow mound, sized per stage by one scale.** The plan said "primitive
  fallback" without a shape; the pillar's fallback box is a 3 m stake and the pillow's unit box
  reads its scale directly in metres, so one row scale sizes both the art and the primitive:
  0.5/0.75/1.0 leaves the real carrots near their authored sizes (0.31/0.44/0.63 m across,
  `synty-inventory.csv`) and leaves a pack-less clone a quarter/half/one-metre mound — the stages
  still read, and a mound is what a leafy plant actually is.
- **The remesh mark for sowing and uprooting moved into `GrowingZones`, and the cancel bug went
  with it.** U47 marked from the job drivers, which covered sowing and harvest but left the
  cancel path — whose intent handlers take a bare `Intent` and have no context to mark from —
  silently drawing a crop the simulation had taken out. Who changed the world owns the mark
  (the U29 settlement), so `Sow` and `Uproot` mark now, the drivers' copies are gone, and cancel
  is covered by the same line. `CrossingAStageDirties…` clears the fixture's sow mark before it
  reads, so the growth pass is still the thing under test.

The Unity tier also collected a debt U46 had left where only it could see: the new Carrots item
made `ItemIndex.Count` seven while presentation's item-module table stayed six, and
`ModuleIdTests` — written for exactly this drift — failed naming item def 6. The dropped harvest
now has its own module row (the mature crop's art at carry size), and the episode is the second
on this branch where green fast-tier seconds said nothing about a join only Unity compiles.

The **zone overlay, this unit, is a per-cell tint** through the standing-orders span path,
fed from the sparse snapshot channel, added to the colour-guard test (`ZoneTintColour`
`(0.45, 0.32, 0.17, 0.34)`). It was a field green `(0.24, 0.62, 0.28, 0.32)` until the owner
found it hard to see on the surface (2026-09-18) and asked for **earthy brown — worked soil**,
which no order shares and which reads on grass where the green vanished. Both hues are recorded
because the brown is the owner's call on a colour nobody had played. One deviation from the plan's wording: it draws a **cell mark, not a cell shade**.
`DrawCellShade` fills the cell's whole volume, which is right for a deconstruct order standing
in the wall it is taking apart; a zone cell is open air above the soil, so a shade would draw a
three-metre glass box over every row. `DrawCellMark`'s plate sits at the floor of that air cell,
which is the ground surface. Stockpiles have no overlay either, and both deserve the
crisp-bordered region shader `09-ui-and-input.md` §4.6 already specifies; that is its own unit,
covering both, and is deliberately not smuggled in here.

**The tilled ground** (2026-09-18, the owner's ask after finding a zone invisible on grass):
each zoned cell draws the farm pack's own dirt rows — `SM_Env_Dirt_Rows_01`, one patch per cell
at the terrain's floor — through the same channel-and-mirror path a crop takes: `UpdateZones`
merge-walks the zone channel into a `_zoned` mirror, the mesher emits the module for zoned
cells, and `Designate`/`Cancel` mark the chunk dirty because the ground under the cell is what
changed. Nothing of it is simulated — no terrain is written, and a pack-less checkout keeps its
grass and tint rather than losing the field (the Pillow fallback there is a low dirt mound,
pending a look). The crisp-bordered region shader unit (§6, below) still owns the final look;
the rows are the interim ground made real.

The **frame figure** (PlayMode `FrameTimeTests`, the real player loop, 640 × 480 on the RTX
5070 Ti): a field of **2,041 zone cells carrying 537 crops renders in 2.92 ms mean, 4.72 ms
worst**, against 1.48 ms for the bare meadow — inside the 5 ms budget with room, but with two
honest caveats attached. First, the measured field is **young**: five colonists cannot out-walk
the sowing queue across a board-wide band in ten days, so 537 of 2,041 cells carried a crop and
none had ripened when the frames were taken — the crop meshes and the tint are what was measured,
and a fully ripe field is the same three meshes at a later bucket, not a fourth thing. Second,
**crops cost about one draw call each** in the current renderer: the field sits at 2,630 draw
calls against the meadow's 1,540, and that per-crop cost, not the growth pass (O(planted) every
250 ticks, unmeasurable at 0.006 ms a tick in the soak), is the scaling thing to watch when the
first real farm goes in. Both caveats are recorded rather than papered over; the soak-run ledger
(`docs/milestones/soak-runs.md`, 2026-09-18) holds the sim-side numbers.

## 7. UI

The build palette gains its **Zones** category (the key `ui.arch.category.zones` and the tool
`ui.arch.tool.growzone` "Growing zone" predate this unit and were waiting for it); the plant
picker rides the palette's existing sub-type chooser, which today lists exactly one carrot and
is honest about it. The orders strip pins a fifth button; `G` arms the tool; the armed banner
says what `Registry.Label` says it says — no literals in the six locked namespaces, the rule
that has already caught two. New job and item keys go through `icon-keys.csv` in the same
commit, both `--check` gates with it.

**What landed (2026-09-18, `0904381` + `c2e3c5e`).** All of the above, plus the decisions the
code cannot show on its own:

- **The zone lives where the sower stands.** A drag on the surface orders the air cell one
  above each surface cell — the cell the crop occupies — and the lift belongs to the zone
  tool alone (`DesignateDirector.OnTheWorkingLayer`); every other tool keeps the
  anchor-layer rule it already had.
- **The plant rides the intent as `A + 1`.** A zone intent's payload is one-based, because a
  zero in `A` means "no payload" everywhere else; the presenter submits
  `Director.Plant + 1`, and `GrowingZones.HandleDesignate` is the only place that subtracts.
- **One rubber kills everything.** Cancel submits a third intent per cell, `CancelZone`, so
  the zone dies the way an order and a building die, and the ground under it is untouched.
- **The zone mode wears earthy brown** (olive, then brown the same day: the owner found green
  hard to see on the surface). `HudTheme.ZonesHue` colours both the Zones category tier and the
  pinned strip action, because a pinned action's hue has one owner each: `Good` already belongs
  to fell. The mode colour matching the category tile is the same value in both places, not a
  coincidence to keep in step.
- **The crop chip is drawn art, lit as a sub-type.** No pixel art exists for a crop and a
  placeholder square is forbidden in the palette, so the carrot is a vector glyph. The
  chosen crop wears the sub-type tier's own lit class — it is the zone tool's payload, not
  a material. The tier is built in all three layouts, shown only while the zone tool wants
  it, and Rows' PLANT heading dims rather than disappears so the panel never changes height.

## 8. Hooks: what is deliberately not here

Each of these was considered and deferred, and each has a named landing place rather than a
"maybe": cooking and meals (the meal defs and a station; carrots are the raw input they will
want); spoilage (rot clocks on stacks; the ration pack's rot-free declaration is already
consistent with it); seeds as items (sowing consumes nothing); watering and fertiliser;
seasons and the growing-period window; temperature (a-06's rooms are its substrate); **light as
a computed value** — the big one, which turns the roof refusal into a computation, makes the
daylight window a sky reading, and is the gate for indoor and underground growing; fertility as
a growth multiplier (GRF(F) — the gate exists, the multiplier is one multiply in
`PlantGrowthSystem` when it lands); the 65% early harvest and partial yields; plant lifespan and
death of age; blight, fire and grazing; the zone inspect pane's **species chooser** — the pane
itself arrived 2026-09-18 (a click on a zoned cell now says what grows there and how far along
it is, via `CellDetail`'s zone fields), but changing the crop from it waits for a second crop to
choose between, and allow-sow / allow-cut toggles and rename with it; cut-only auto-harvest zones and wild plants; tree
planting.

## 9. What nobody has judged

- **Nobody has pressed Play on any of it** — the zone tint, the three carrot stages, the swing,
  the banner. Contact sheets and tests are all it will have until the owner opens it.
- **Four calendar days to a harvest is a pacing guess.** Visible on day one, food on day five:
  if that reads as slow in play, `growTicks` is one number in one XML file.
- **The tint is interim** — green, then earthy brown at the owner's ask before anybody had
  played either. Whether brown reads as "growing here" or as a dirt patch is exactly the
  judgement the crisp-border unit is waiting to inherit, and the first playtest may send it back
  the other way.
- **~7 tiles per colonist is arithmetic, not play.** The reference ships 10+ as a rule of thumb;
  ours is fatter, and nobody has felt whether fat is right.
- **The frame figure belongs to a young field.** §6's 2.92 ms was measured with 537 crops standing
  and none ripe; whether a fully sown, fully ripe two-thousand-cell field holds the budget has not
  been watched, and the ~one-draw-call-per-crop line in §6 is the reason to watch it before the
  first farm the owner actually builds.
