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
| `sowWorkTicks` | **170** | The rice-class sow figure. Includes clearing the cell's grass: sow work is one number, terrain unchanged (a dirt-row ground pass is presentation, §6) |
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

`Work_Growing` (order 4, after Construction), `Skill_Growing`, `Job_Sow` and `Job_Harvest`
(driver ids 10 and 11). Neither JobDef carries `workTicks`: like mining, the work is priced per
plant, the driver reading `sowWorkTicks` or `harvestWorkTicks` from the zone's `PlantDef` as it
swings. Skill and experience ride the def defaults; WS rates are not built, and §3a of
`17-rates-and-stats.md` is where the plant-work curve lands when they are.

Both drivers are `FellJobDriver`'s shape with one simplification: **the colonist stands in the
cell, not beside it.** A crop is ground, not an edifice — it blocks nothing, so the stand-beside
stance and its reach check have nothing to guard. Walk to the cell, swing for the def's work,
and on the last swing defer the structural write (§ of the standing rules: anything structural
goes through the deferred phase): sowing marks the cell planted at growth 0 and marks the chunk;
harvesting spawns the yield at the cell — `NearestCellWithSpace` within three cells, the felling
precedent, so a full cell passes its carrots to a neighbour instead of losing them — and clears
the planting.

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

The **zone overlay, this unit, is a per-cell green tint** through the existing cell-shade span
path, fed from a sparse snapshot channel — the standing-orders pattern, added to the colour-guard
test. Stockpiles have no overlay either, and both deserve the crisp-bordered region shader
`09-ui-and-input.md` §4.6 already specifies; that is its own unit, covering both, and is
deliberately not smuggled in here.

## 7. UI

The build palette gains its **Zones** category (the key `ui.arch.category.zones` and the tool
`ui.arch.tool.growzone` "Growing zone" predate this unit and were waiting for it); the plant
picker rides the palette's existing sub-type chooser, which today lists exactly one carrot and
is honest about it. The orders strip pins a fifth button; `G` arms the tool; the armed banner
says what `Registry.Label` says it says — no literals in the six locked namespaces, the rule
that has already caught two. New job and item keys go through `icon-keys.csv` in the same
commit, both `--check` gates with it.

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
death of age; blight, fire and grazing; the zone inspect pane (species chooser, allow-sow /
allow-cut toggles, rename) — the palette chooser covers a one-crop world, and the pane arrives
when a second crop makes it earn its place; cut-only auto-harvest zones and wild plants; tree
planting.

## 9. What nobody has judged

- **Nobody has pressed Play on any of it** — the zone tint, the three carrot stages, the swing,
  the banner. Contact sheets and tests are all it will have until the owner opens it.
- **Four calendar days to a harvest is a pacing guess.** Visible on day one, food on day five:
  if that reads as slow in play, `growTicks` is one number in one XML file.
- **The green tint is interim.** Whether it reads as "growing here" or as a texture fault is
  exactly the judgement the crisp-border unit is waiting to inherit.
- **~7 tiles per colonist is arithmetic, not play.** The reference ships 10+ as a rule of thumb;
  ours is fatter, and nobody has felt whether fat is right.
