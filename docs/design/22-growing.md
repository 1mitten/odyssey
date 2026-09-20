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

## 3. Growth: a window, a counter, and four re-meshes

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
swings. Every driver pays work at the pawn's own rate — `Pawn.WorkRatePerMille(WorkType)`,
banked as milliwork per WS1's one-unit rule.

**Growing carries a rate curve, and this paragraph said the opposite until 2026-09-20.**
`Work_Growing` has `rateSkill 4` with cutting's own numbers — `600 + 100 x level`, so a novice
runs at six tenths of the tuned speed and the tuned speed sits around level four (owner,
2026-09-19: *"make sure the speed of the sowing and harvesting is determined by the relevant
skill"*). The curve was added with the work type and three separate comments went on denying it:
this one, the header of `WorkTypes.xml`, and `GrowingJob`'s own *"flat today"*. All three are
corrected. It matters beyond tidiness — **this is the first place in the game where a skill level
does something a player can feel**, which closes a gap CLAUDE.md had listed as open, and it is
the one thing on the playtest list that a still cannot show. §3a of `17-rates-and-stats.md`
still owns the shape of the curve itself.

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

**And the gesture's END is the driver's to author.** The gesture is sticky by contract — a
flag set for a single tick would be missed between frames — so a kneel nobody cleared leaked
through the whole walk to the next plot, and the seed specks, gated on the gesture, flashed
under the sower's feet on every fallow tile she crossed: the owner watched seeds "appear
immediately … then disappear — then it appears again" (2026-09-19). Both drivers now clear it
(`BeginGesture(None)`) on the same boundary the deferred plant record lands, so the specks
hand over to the planted cell's own without a gap, and no walker carries a kneel. The
displacement path (WalkBack) clears it too, for the same reason.

**The harvest kneels too** (owner, 2026-09-19: *"the colonists still use their axe to harvest
… use the same pose as sowing bending down"*). `HarvestJobDriver` reports no work focus either, so
the axe the computed swing would summon never appears, and the pull begins the same
`PawnGesture.Sow` kneel — pulling a carrot is the same reach at the same soil as seeding one, so
the pose is shared rather than forked. The gesture is timed against `sowWorkTicks` while the work
pays `harvestWorkTicks` (200 vs 170), which is the same "for now" approximation one line up
inherited by the second caller.

**The seed specks wait out half the kneel** (owner, 2026-09-19: *"it should have a delay so the
colonist is actually bent down for some time and seeds appear"*). The gate is the kneel gesture's
own age on the frame clock — `Gesture.SeedSpecksAfter`, half the motion, which is the middle of
the hold — measured by the serial-differs tracker the bootstrap keeps per pawn, the same test the
figure director uses to fire a pose. Nothing in the simulation stores "how long has she been
kneeling": the specks are a drawing of the kneel and owe their timing to the kneel's own clock,
which is why the delay lives on `Gesture` beside the timing it scales with.

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

### 6a. The zone itself is a bit on the ground's tint (2026-09-20)

A painted zone is not drawn. Its ground is, and it is drawn once: `DrawnTerrain` already swaps a
zoned cell's terrain to bare earth, and `TintCode.TilledBase` marks that terrain bucket as worked
soil, which `ChunkRenderer.ResolveColour` grades by `TilledGrade`. No second mesh, no overlay, no
per-cell work in the frame at all.

It arrived as a translucent cover — the ground's own module drawn again over itself — and that
design had both a look fault and a cost fault, which turned out to be the same fault.

- **The look.** The cover carried a 1.01 scale so neighbours overlapped rather than met, on the
  reasoning that an overlap of one tint is invisible. True of an opaque overlay and false of a
  translucent one: alpha blending is not idempotent, so at alpha 0.78 a doubly-covered band
  composited to 0.95 and the dirt showing through fell from 22% to 5%. That is a dark line on
  every **interior** edge of a field and none on its outside edge, which is exactly the shape
  the owner photographed. `MarkLift` then did the same thing again in miniature: lifting a cover
  raises a *box*, and its sides stand proud of the neighbouring soil by the lift.
- **The cost.** One `Graphics.RenderMesh` per zoned cell per frame — 2,065 draw calls and
  **3.67 ms of a 5 ms budget** on the benchmark's field, against 0.17 ms for the same field with
  the pass switched off. It was also the only thing in the feature standing outside the
  instanced-chunk architecture, and it incremented no counter, so `FrameTimeTests` had never
  counted one of them.

The colour is derived from the cover rather than re-chosen, so the field keeps what was agreed:
the cover composited as `0.78 x (0.06, 0.032, 0.012) + 0.22 x ground`, a scale plus a warm
pedestal, and `TilledGrade` is the per-channel multiply that reaches the same place. A uniform
0.25 was tried and read grey — the pedestal was carrying the warmth.

`ABiggerFieldAddsInstancesRatherThanDraws` is the guard: it fails the moment a zone costs draws
in proportion to its cells. That matters most for the storage zones coming next, which are
painted across a whole base rather than in a plot.

Seed specks went the same way on the same day: six submissions per sown cell of one cube in one
material, gathered now and drawn in a single `RenderMeshInstanced`.

### 6b. The crop

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

**The tilled ground** (2026-09-18, the owner's ask after finding a zone invisible on grass) is
**the terrain quad itself, re-looked**: a zoned cell's ground draws as bare earth — the same
dirt material and tint the board's own earth wears, `Mat_Dirt_01` — instead of grass, through a
`GroundLook` swap in `EmitTerrain` fed by the zone mirror. Two mesh variants were tried and
rejected first, both the owner's calls: the dirt **rows** read as mess ("all over the place"),
and the dirt **tile** (`SM_Env_Dirt_01`) would not sit clean on the cell — a mesh laid over the
ground can be proud of its tile however it is placed. The terrain swap is seamless and boolean
by construction (owner: "it has a brown tile or not"): one quad per cell, nothing over laps,
nothing stacks, an unzoned cell reverts to the grass it was, and the field costs not one extra
draw call. The grid's own terrain is untouched — drawn look only, nothing saved or hashed. `UpdateZones`
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
**a field's cost is buckets, not plants**: each crop species×stage×daylight is one instanced
bucket per chunk, so a plot drawing its five plants multiplies instances fivefold and adds no
draw calls — the 2,630 draws against the meadow's 1,540 was measured at one plant a cell and is
unchanged by the yield. What scales with a real farm is the instance count and the growth pass's
O(planted) every 250 ticks (0.006 ms a tick in the soak), neither of which has shown in a frame. Both caveats are recorded rather than papered over; the soak-run ledger
(`docs/milestones/soak-runs.md`, 2026-09-18) holds the sim-side numbers.

**What the first play day earned, 2026-09-19 (owner: *"single carrot is always displayed no
matter what"*):**

- **A published plant now carries the handle the contract promises — and this was the whole
  "one carrot" mystery.** `PlantView.Plant`'s contract says `PlantHandle` (nought-based); the
  contributor was publishing `_cropAt`, which is one-based so that nought can mean fallow, and
  `UpdateCrops` adds one of its own on arrival — so the carrot landed on slot two of a
  one-plant table, where `CropModule`'s bounds guard quietly answered nought and **a ripe field
  drew nothing at all**. Every render test fed the contract's nought-based bytes and passed
  while the game fed different ones; `APublishedPlantCarriesItsHandleAndNotTheCropSlot` now
  reads the real contributor back, which is the only test that can see a convention. The one
  carrot the owner saw standing was the *pile* (below) — the plot itself was bare.
- **The pile is a heap.** `ItemHeap` had no row for carrots, so a five-carrot harvest drew as
  one prop in the middle of the cell — exactly the "one carrot after harvest" of the playtest.
  The recipe's `Full` is the carrot's own `yieldCount`, which makes the ramp the identity up to
  a yield: five grew, five lie there. `CropCheck`'s pile photograph reads as five-plus scattered
  carrots with their tops on.
- **`CropCheck` photographs the field through the shipped catalogue** — stages one to three and
  the pile, from the play bearing and close up (`scripts/unity.sh shot
  Odyssey.EditorTools.CropCheck.Run`). It exists because every carrot judgement until now came
  from the owner playing the game, and twice what was being looked at was not what the code
  drew. Its own gap, recorded rather than hidden: the zone tint and the seed specks are the
  bootstrap's overlay draws and no sheet of the mesher's can show them.
- **The seed day is a stage of its own (nought)** (owner, 2026-09-19: *"the seeds should stay
  there at first — the sprouting should appear after a day rather than immediately"*). Below
  25% growth — exactly one daylight window, 32,500 of 130,000 ticks — `StageOfTicks` answers
  nought, the mesher draws no plant, and the specks are the crop. The count is still the
  yield; only the art waits. A seed sown at dusk waits the night out first.
- **The seeds fall from a bundle at the hand** (owner, 2026-09-19: *"an animation that
  basically starts from a bundle of seeds from a hand and then the seed fall onto their
  destinations"*). At the half-kneel threshold the specks draw clustered at hand height for a
  quarter second, then each falls to its hashed spot, staggered 40 ms apart and accelerating,
  and from landing they are the static handful the seed day draws. The drop's clock is the
  kneel age past its threshold, so no state is timed twice.
- **The tilled-earth swap was asking the wrong layer, and had never fired** (found 2026-09-19
  from the owner's flush screenshots). A zone is painted on the air the colonist stands in;
  the soil it tills is the cell beneath her feet — and `DrawnTerrain` asked the zone at the
  ground cell itself, so the answer was always no. Every brown tile the owner had ever seen
  was the translucent cover, the plots' own tufts never left, and the photo sheet had said
  "carrots growing out of grass" twice before anybody believed it. The swap and the tuft pull
  both ask one layer up now, and `TilledGroundAsksTheZoneOneLayerUp` pins it.
- **The cover is the ground's own mesh, drawn again over itself and tinted** (owner,
  2026-09-19, with the screenshots that prove it: *"not flush against the tile and
  constantly have thicker borders, have gaps, part missing and isn't uniform from different
  angles"*). The terrain quad is draped onto the relief field's tangent plane and rippled
  inside its own cell; the first cover was a flat plate at the cell centre's height, which
  sank into the ripple's convex corners and floated over the concave ones — gaps, thick
  borders and missing parts that changed with the bearing. `DrawZoneCover` places the drawn
  terrain's own module with the same drape and lifts it a mark's height along the drape's
  own up: identical geometry, one constant offset, flush from every angle by construction.
  Judged flush and uniform in the photo sheet.
- **The yield is laid off the soil, and nothing is sown where a yield still lies** (owner,
  2026-09-19: *"you cannot sow unless the tile has been harvested"… "harvested materials
  should not be laid on the soil and should look to be moved off it"*). The harvest hunts a
  cell outside every zone within three before it falls back to the felling argument, and the
  sowing scan refuses a cell that still carries a pile. The flow reads: pull → yield on the
  grass beside the plot → hauled to the stockpile → the tile re-sown.
- **A high number of carrots per tile is twelve drawn, seventy-five stacked** (owner:
  *"make a judgement call"*). The heap ramp stays the identity up to a dozen — five grew,
  five lie there — then holds at twelve so a store square is a proper heap without every
  extra carrot redrawing it; the stack limit rises to 75, the same as wood and stone, and
  `ItemHeap.Most` rises with it so rubble's full stacks grow the same way.
- **The cover is translucent unlit, and the brown is back** (owner, 2026-09-20: *"the dirt
  tile is black with no texture instead the brown that was before"*). The opaque switch
  killed the borders by killing the light and the texture together; the cover now carries
  the tint's own alpha through the unlit material (straight alpha, no premultiply), so the
  tilled earth shows through and the field is brown with texture — while staying one flat
  colour per tint, every tilt, every light, immune to shading, seams and bloom.
- **And the yield reaches grass from inside a big field** (owner, 2026-09-20: *"they picked
  up the items and put them on the next dirt tile — they should put it on the next free
  terrain tile that isn't dirt/soil"*). Both off-zone searches — the harvest yield's drop
  and the clearing fallback — ran out of ring at three and six cells, and a field six tiles
  across has nothing but dirt within three of its middle: the fallback then put things back
  on the plot. Both now reach twelve cells, which covers any field the player has painted.
- **The cover is opaque, flat and UNLIT** (owner, 2026-09-20: *"still borders on the tiles"*
  — after two geometric fixes). The bracket material is the lit terrain shader made
  transparent, and on rolled ground it shaded every differently-tilted cover differently:
  per-tile brightness steps that read as borders however seamless the geometry, invisible in
  a flat-lit sheet and plain under the play sun. The cover now clones URP's Unlit
  (`MaterialCache.UnlitBase`): one colour, every tilt, every light. It is opaque — the
  tilled earth beneath no longer shows through, and the field's texture is the seeds, the
  crops and the soil's own shape. An unlit flat colour cannot shade, cannot seam, and
  cannot be bloomed into a line; there is nothing left in the material to disagree with.
- **The sower clears her own field's blockers** (owner, 2026-09-20: *"the items were not
  picked up and removed from the dirt/garden tile"*). Growing scans at order one and hauling
  at four, so a busy field — endless sowing and reaping — starves the haul order, and the
  stone on its own tile waits forever even with the store empty and the bias in place. When
  the sow scan finds every tile sown or waiting on a thing, it hands out the haul of the
  blocker itself: clearing the dirt IS the sowing work, not something that happens to
  precede it.
- **The cover is the drawn ground's own mesh, variant and bearing, plus a hair of overlap**
  (owner, 2026-09-20, on the second round of screenshots: the grid survived the shared lift).
  Two faults were stacked: the earth resolves a **variant clump per cell** (and a face cut
  where sides show) while the cover drew the plain default block — a different shape, whose
  partings against the drawn clumps showed slivers of untinted earth as a grid over the whole
  field; and the photo sheet that had said "no seams" was **blind, because it ran on a board
  with no relief amplitude** — a flat board has no partings to show (CropCheck sets the
  played board's amplitude now, and restores it in its finally). The cover now reads the
  earth contributor's own choices — module, variant, exposure, yaw, drape — and scales a
  hair in the plane (`CoverOverlap`, 2.5 cm a side) so adjacent covers overlap rather than
  meet. The sheet, on real relief, reads one continuous patch inside the field; what remains
  at the outer edge is the boundary of two different grounds meeting, which is the field's
  own outline rather than a fault.
- **A field blocker with nowhere to be stored is cleared to the grass anyway** (owner,
  2026-09-20: *"the colonists didn't remove the stone from the dirt tile and didn't bother
  sowing and nothing happened"*). The stall was a deadlock in which every party behaved: the
  store was full, so the haul scan formed no job; the sow guard had blocked the tile, so no
  sowing; nobody was at fault and nothing moved. A thing on tilled soil that no store will
  take now goes to the nearest free cell outside every zone, becomes an ordinary pile there,
  and the stockpile can have it back when it has room.
- **The covers lift along the world's up, and the borders died** (owner, 2026-09-20: *"remove
  the borderlines from the dirt tiles so the grow areas would appear as one"*). Each cover
  had lifted along its own drape direction; two neighbours' drapes disagree by a few
  centimetres of slope across a shared edge, and the divergent lifts opened a hairline of
  untinted earth between every pair of tiles — a lighter grid over the whole field, widest
  where four cells met. A shared world-up lift leaves the covers' mutual seams exactly the
  terrain's own, which the ground already draws invisibly (each box's tinted side wall fills
  its step); the photo sheet reads one continuous field, no grid, no corner marks.
- **A thing on tilled soil is the field's blocker and is hauled first** (owner, 2026-09-20:
  *"all items should be removed by colonists first from the dirt before sowing to an
  appropriate place"*). The sowing scan already refuses a cell that carries a thing; now the
  haul scan prefers a thing standing on a zoned cell over every ordinary pile however near
  (`ClearanceBias`, fifty cells — the ordering, not the radius). The flow reads: drop,
  clear, sow.
- **The skill buys speed at the hoe** (owner, 2026-09-20: *"make sure the speed of the
  sowing and harvesting is determined by the relevant skill — another agent is addressing
  skills"*, so the seam is the whole change). `Work_Growing` carries `rateSkill` =
  `Skill_Growing` with cutting's own shape — a novice at six tenths of the tuned speed, a
  level a tenth more, the tuned speed at level four — and both drivers already paid at
  `WorkRatePerMille` (WS1's one-unit rule), so the def is the entire edit. The curve's own
  tuning (17-rates-and-stats §3a) belongs to the skills work when it lands; nothing here
  decides it.
- **The meadow is pulled off the plot's border** (owner, 2026-09-19: *"remove the grass
  graphics from the garden plots automatically … it makes it jarring to see with the grass
  graphics still appearing on the plots"*). A zoned cell's own tufts already died with the
  tilled-earth swap; what remained was the *neighbours'* — a grass clump's mesh is nearly
  two metres across and stands in a ring up to 0.44 of a cell out, so clumps from unzoned
  grass reached ~0.85 m over every border tile. `GroundScatter.PullInFromTilled` clamps a
  clump's offset to where its reach (a metre, `ClumpReach`) stays on grass, asked through
  the zone mirror at mesh time; and because a border cell's neighbours can live in another
  chunk, designation and cancel now mark the side neighbours' chunks too. The clamp only
  moves clumps that would have crossed — the ring everywhere else is where it always was,
  which the stability rule in `GroundScatterTests` still pins.
- **The pile lies down** (owner, 2026-09-19: *"the carrots hauled and piled should be
  horizontal on the floor and not stuck in the ground"*). `ItemHeap.Recipe` carries a
  `LyingDown` flag; the carrot row sets it, and `Place` tips each carrot 90° before its yaw
  and lifts it half a girth (`LyingLift`), because the mesh pivots at its base and a tip
  about that pivot buries the body. The armful inherits the lie. Verified by eye and by
  arithmetic in `CropCheck` — the pile shot frames the pile alone, after the first framing
  answered a question about orientation with a field of upright carrots behind it.
- **Carrying needed no change and now has a test.** The owner's "carried back like wood" is
  what already happens — the yield drops as a loose haulable pile exactly as a felled trunk
  drops one — and `TheYieldIsHauledToTheStockpileLikeWood` pins it, so a carrot that stops
  being haulable or a stockpile that stops accepting one fails a build instead of sitting in
  the field forever.
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
- **The second play day's fixes are sheet-judged only** — the harvest kneel, the half-kneel seed
  delay, the five-carrot ripe tile and the five-carrot pile are photographed (`CropCheck`) and
  none has been played. The ripe tile and the pile are arithmetic under the photo; the delay is
  the one that wants a wristwatch.
- **The frame figure belongs to a young field.** §6's 2.92 ms was measured with 537 crops standing
  and none ripe; whether a fully sown, fully ripe two-thousand-cell field holds the budget has not
  been watched, and the ~one-draw-call-per-crop line in §6 is the reason to watch it before the
  first farm the owner actually builds.
