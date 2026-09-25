# 45 — Nature: the scenery becomes real things

**Status: designed and built 2026-09-25; not yet played.** Branch `claude/meadow-nature`, worktree
`D:\code\odyssey-meadow-nature`. Two stacked pull requests: **M5** (tree species, bushes, the
topple) and **M13** (loose rocks, mushrooms, berries). The Meadow plan is
`38-meadow-overhaul.md` §10 (the M rows); the dressing this replaces is §17–§19 there.
**Read first:** `38-meadow-overhaul.md` §17 (the dressing and its rules), §19 (the bush that
never fades); `22-growing.md` (the harvest this borrows from); `22-terrace-steps.md` §3 (the
façade rule, which bushes obey); `15-building.md` §6 (what a site refuses).

## 1. The request

The Meadow look pass (design 38) strewed the ground with **drawn** things: tall grass, flowers,
ground cover, bushes and loose stones, placed by a hash of the cell and never simulated. A player
can see a bush and cannot do anything with it, and a colonist walks through a boulder. The owner
decided on 2026-09-25 which of those become real:

| Question | Answer (owner, 2026-09-25) |
|---|---|
| What becomes real | **Bushes and loose rocks**, at the dressing's own spots and density, and **mushrooms** under trees. Grass, flowers and ground cover stay drawn. **A berry bush is a kind of bush.** |
| Bushes | Walked **through**, more slowly. A building, floor or zone on a bush cell needs it **cleared first**, with an order like felling. Clearing yields nothing (a little wood at most). |
| Food | **Berry bushes** carry berries the player **orders harvested** (raw food), regrowing over a few days. **Mushrooms** appear under trees, are foraged once, and come back slowly somewhere else. |
| Trees | **By species, and the species is the simulation's.** Wood and work scale with size: birch small and quick, the meadow tree medium, **the giant** a lot of wood and slow and rare, fruit trees medium with the fruit deferred. A felled tree **topples** — drawn only, never in the hash. |
| Loose rocks | **Hauled for stone.** No mining. |

## 2. What is real, and what it is made of

| Thing | Sim shape | Where it comes from | What it gives | Unit |
|---|---|---|---|---|
| Birch | edifice 10 (was *conifer*) | the tree pass | 22 wood, 650 work | M5 |
| Meadow tree | edifice 11 (was *broadleaf*) | the tree pass | 32 wood, 900 work | M5 |
| Fruit tree | edifice **17** | the tree pass | 28 wood, 800 work; fruit deferred | M5 |
| Giant | edifice **18** | the tree pass, one broadleaf in forty | 100 wood, 2,400 work | M5 |
| Bush | edifice **19**, not blocking, `CellFlags.Undergrowth` | the undergrowth pass | nothing; 250 work to clear | M5 |
| Berry bush | edifice **20** (ripe), **21** (picked) | the undergrowth pass, one bush in six | 8 berries a picking, 3 days to regrow; 250 to clear | M5 place, M13 pick |
| Loose rock | an `Item_Stone` stack of 3–7 | the undergrowth pass records the spots, the colony spawns them | the stone itself | M13 |
| Mushrooms | an `Item_Mushrooms` stack of 3–5 | beside trees at the start, and regrown | food, 70 a mushroom | M13 |
| Berries | `Item_Berries` | a picked berry bush | food, 60 a berry | M13 |

Every number in the table is **invented**, inside the owner's envelope, to be tuned after a play.
The wood is set so the average tree yields 25.6 against the 27 every tree yielded before, and the
species mix is the one the art already showed (§3).

**The ids.** Edifice ids are one space shared with the buildings (bed 12 … heater 16), so the new
natural ids continue after the last one rather than beside the trees. `NaturalContent.IsTree`,
`IsBush` and `IsNatural` are the predicates; **nothing may compare an edifice id against a range**
any more, because the natural ids are no longer contiguous. Four sites in `WorldRenderModel` did,
and the shelf's comment there is the record of what that costs (it once drew every shelf as a
conifer).

**Birch and meadow keep 10 and 11** rather than taking new ids: an old save's conifers are
birches in the art already, and its broadleaves load as meadow trees. Those are the only things
an old save can hold, so **no save format moves**. The constants are renamed to what they are
(`EdificeTreeBirch`, `EdificeTreeMeadow`); the catalogue's two module ids
(`odyssey.module.tree.conifer`/`.broadleaf`) are **art families**, not species, and keep their
names because the committed catalogue asset is keyed on them.

**Content.** Each of the six kinds is a `WildPlantDef` in `Defs/Core/World/WildPlants.xml`: the
work to clear it, what clearing yields and how many, and for a fruiting kind what a picking yields,
its work and its regrowth. `WorldContent.WildPlantOrder` maps a def name to an edifice id, and is a
save contract like the plant and terrain orders. The code knows which ids are trees and which are
bushes; the numbers live in the Defs, once.

## 3. Species, and the art agreeing with them

The tree pass already draws a `species` roll per column, used today for conifer-or-broadleaf. The
species split is **derived from that same roll**, so no new random draw is taken and **every tree
stands exactly where it stood** — the goldens move by what a tree yields and costs, never by where
the woods are:

- `species >= broadleafChance` (58 %) → **birch**.
- Otherwise the roll is rescaled to 0–999 across the broadleaf band: below **25** the **giant**
  (one in forty, the look pass's own rarity), below **270** the **meadow tree**, else a **fruit
  tree**. That is the mixture `MeadowDressing.TreeVariant` drew by hash — meadow one in four of the
  rest, fruit three in four — so the woods look as they did.

Presentation stops choosing the species. `TreeArt.VariantFor(def, x, z, variants)` picks within the
species' own rows: a birch any of the three birches by hash, a meadow tree `Meadow_02`, a fruit
tree one of the three fruit trees by hash, the giant `Meadow_01`. The palette family is unchanged
(birch the conifer palette, the other three the broadleaf one), so the stand themes, the far LOD,
the surround's census and every material table are untouched.

## 4. Bushes

**Placed by the undergrowth pass** (order 11, after Start, its own random purpose
`NaturalGenPurpose.Undergrowth = 10`), which ports `MeadowDressing.BigPiece`'s bush rule to the
simulation at the shipped density: even-even lattice cells, the value-noise field at 14 cells over
a 0.45 threshold, raised to 0.7 beside a tree, times 0.4. In integers: the field is the same
smoothstepped value noise in 16.16 fixed point over an FNV hash keyed on the world seed, so two
worlds do not share their bushes (the dressing's hash had no seed at all). **Not where**:

- off grass, in or beside water, or where the ground is not the column's top;
- in a tree's cell;
- **at the foot of a terrace step** — the façade rule (CLAUDE.md, `22-terrace-steps.md`): the bank
  fills that cell, so a bush there would be sheared by it exactly as trees were;
- within 4 cells of the start (the dressing's own clearing radius), so the landing site is clear.

**Movement.** `NavGrid.ClassAt` asks the cell's `Undergrowth` flag before the terrain: cost class
**`CostClassBush = 4`, +50** (150 a cell, a third slower than grass and a little worse than marsh).
The flag is set where the bush is placed and cleared by `CellGrid.RemoveEdifice`, so the one place
a bush can leave the world is the one place the flag goes, and navigation already re-reads a cell's
class when its edifice changes. A bush is not an obstacle for the sidestep (`HasObstacle` is
trees only): you push through a bush, you walk round a trunk.

**Clearing.** The Fell order becomes **chop and clear**: `DesignationKind.Fell` is allowed on any
tree or bush (`DesignationGrid.IsFellable`), the same work giver finds both, and the driver reads
the work and the yield from the plant's Def. The palette chip keeps its key (`ui.arch.tool.fell`)
and changes its words to *Chop and clear*. Skill and work type are chopping's.

**The build and zone guard is already there.** A site, a growing zone and a stockpile each refuse a
cell with any edifice in it (`ConstructionGrid.Allows`, `GrowingZones.SiteAllows`,
`StorageZones`), which is how trees have always worked. A bush is an edifice, so it is refused with
no new code, and the refusal reads the same as a tree's.

**Drawn** by the dressing's own path: the mesher draws a bush edifice with the Meadow bush art and
`MeadowDressing.Placement`, as a `Dressing` tint on the broadleaf palette — so it takes the
indirect path, casts no shadow and **never fades** (owner, design 38 §19). It is drawn whatever
the grass ladder says, because it is a thing and not decoration: turning the grass off must not
hide something that slows a colonist. The dressing no longer strews bushes or stones of its own.
A **berry bush** is the same art with the fruit tint (M13 draws its berries).

## 5. The topple (presentation only)

When a tree edifice leaves the render mirror between two publishes, `WorldRenderModel` records it
(cell, species) and `TreeToppleDirector` draws that tree — the same art, yaw and scale the chunk
drew, from `TreeArt` — falling about its base, away from the nearest colonist, over about a second
and a half, easing in like a weight going over, then sinking into the ground. It is not in a cell,
a save or the hash. A tree removed by a collapse topples too; a world being loaded does not,
because a full refresh records nothing.

## 6. Berries, mushrooms and loose rocks (M13)

**Berries.** A new designation, **Harvest** (`DesignationKind.Harvest = 4`), allowed on a ripe berry
bush; a new job, `Job_Forage`, under **Growing** (it is picking a crop), trains growing. The driver
walks beside the bush, works the Def's picking work, drops the Def's berries beside it (off the
bush), and swaps the edifice to the **picked** kind. `NatureSystem` keeps one sorted list of
*(ripe-at tick, cell)* for picked bushes and ripens the earliest when its tick comes, swapping the
edifice back. That list is the only new state: saved under its own key (`odyssey.nature`, so no
format bump — an old save has none and every berry bush in it is ripe) and hashed. The tool is
`ui.arch.tool.harvest` — a key the registry already had for exactly this — beside Chop and clear on
the palette.

**Mushrooms.** At the start, stacks beside trees, one in about forty trees. `NatureSystem` regrows
them: every 2,500 ticks, if the board holds fewer mushroom stacks than a cap (one per forty trees),
one stack appears beside a tree chosen by the tick's own random stream. There is no order to
forage: mushrooms are loose food, so a hauler brings them in and a hungry colonist eats them where
they lie, which is what *foraged once* means here. A stack that is eaten is gone; the next one grows
somewhere else.

**Loose rocks.** The undergrowth pass records the dressing's stone spots — slot two of
`SmallPiece`: 18 % of cells beside rock, 1.2 % elsewhere — and `ColonyWorld.Build` spawns a stack
of stone at each. A stack is drawn by `ItemHeap` as the lumps it holds, which is what a loose rock
now looks like. They are hauled like any stone; nothing marks them.

## 7. Save and hash

| State | Saved | Hashed | Format |
|---|---|---|---|
| Species and bush edifice ids | yes, in the edifice list | yes | none: new values of an existing field |
| `CellFlags.Undergrowth` (bit 7) | yes, in the grid's flags | yes | none |
| Picked berry bushes (M13) | `odyssey.nature` | yes | none: a new keyed section |
| Mushrooms, rocks, berries | as items | as items | none |
| The topple | no | no | — |

All three goldens move in M5, because trees yield and cost differently and bushes are in the
cells. **What moved is measured, not assumed** — `GoldenColonyProbe` before and after (§9).

## 8. Names, icons, the inspect pane

New registry rows (`docs/design/icon-keys.csv`, the source of the wiki): the four tree species
(`ui.terrain.tree.birch`/`.meadow`/`.fruit`/`.giant`, replacing *conifer* and *broadleaf*), the
bush and the berry bush (`ui.terrain.bush`, `ui.terrain.bush.berry`, `ui.terrain.bush.picked`),
the two foods (`ui.res.berries`, `ui.res.mushrooms`), the picking status (`ui.status.foraging`).
`EdificeLabels` names every new edifice, so clicking a bush titles the pane with it. Icons: none of
the new keys has a sheet cell, and each is recorded as a gap in `icon-map.csv`.

## 9. What is measured

- **Goldens**, re-baked and diffed with `GoldenColonyProbe` on all three colonies.
- **The tick**, flat (P12): the undergrowth adds no per-tick work, and `NatureSystem` is an O(1)
  peek a tick plus one scan every 2,500.
- **The frame at 4K** on Standard and Huge against `main`, in one run each: the bushes move from
  the dressing's buckets to the edifice pass and should cost the same.
- **The look**: `FrameTimeTests.TheLookAtThePlayCamera` before and after.

## 10. Left out

- **Fruit on the fruit trees**, deferred by the owner.
- **Bush regrowth.** A cleared bush is gone for good; the meadow does not re-seed.
- **A bush's own wood.** Clearing yields nothing (the Def can say otherwise in one line).
- **Seasons**, **spoilage** and **cooking** of berries and mushrooms — the growing line's hooks.
- **Things dropped into a bush cell** are drawn inside the bush; nothing keeps a hauler from
  putting a load there. Recorded, not fixed.
- **Animals eating berries** — no animal eats anything yet.

## 11. As built (2026-09-25)

Two stacked branches: `claude/meadow-nature` (M5, §2–§5) and `claude/meadow-forage` on top of it
(M13, §6). What differs from the plan above, and what was measured:

- **The Harvest chip is pinned** beside Chop and clear, in its own berry hue (`OrderColours.Forage`),
  with a drawn glyph (`HudGlyphKind.ToolHarvest`). That makes the orders strip seven long;
  `PaletteToolsTests.ThePinnedRowStaysShort` carries the paragraph its rule asks for, and the
  paragraph says it is an **owner decision to confirm** — if seven reads long, Harvest is the one
  to move.
- **A ripe berry bush wears berries**: seven clusters of the berry item's own art round its
  shoulder, drawn by the mesher beside the bush; a picked bush is the same bush without them. The
  swap is an edifice id (20 ↔ 21) in place, so the handle, the flag and the inspect pane follow.
- **Loose stones are drawn by `ItemHeap`** as the stone lumps any stack of stone is, not with the
  dressing's Meadow boulder art. Whether that reads as a stone lying in the grass is a playtest item.
- **`NatureSystem` hashes nothing while nothing is picked**, so registering it moved no golden, and
  the mushroom regrowth keeps no state (it counts the loose stacks every 6,000 ticks).
- **The starting placement is unchanged, measured**: `ScenarioDefTests` pins the scenario's own
  items, and with the map's items (after `ColonyWorld.FirstNaturalItem`) left out the signature is
  the one it has always been.
- **Goldens.** M5 moved the played board only; the probe (`GoldenColonyProbe`, diffed against the
  base commit) shows the generated census identical and the colonists making 95 wanders where they
  made 105 in 10,000 ticks, because a bush costs +50 to cross. M13 moved it again: 680 stone in
  136 stacks and 89 mushrooms in 22 on the generated board, one more mushroom stack after the run,
  and every colonist number identical to M5's. The bare meadow and the city never moved. The water
  test's six dry-map hashes re-based; all six barren ones are byte-identical.
- **The frame, 4K, back to back against the base commit** (`FrameTimeTests.TheNatureAgainstTheFrame`,
  RTX 5070 Ti): Standard 6.98 -> 6.71 ms, Huge 6.62 -> 6.83 ms; batch 1.81 -> 1.82 and
  2.49 -> 2.34. `World` flat (1.007 -> 0.979, 1.710 -> 1.697), so the bushes drawn from edifices
  cost what the dressing's did. The tick 0.007 -> 0.008 ms on Standard and 0.021 -> 0.020 on Huge
  (P12). **The first reading was not flat**: the actor pass drew every thing on the board with no
  view test, and the map's stones and mushrooms took `Actors` from 0.089 to 1.421 ms on Huge. It
  culls to the frustum now (`docs/bug-patterns.md`, 2026-09-25).

## 12. First play: clicking a bush, and berries on the bush (2026-09-25)

Owner, playing #222: *"I couldn't click on some of the berry bushes properly"* and *"the berries
were never flush against the bush … they appear to be floating away from the bush."*

**The click, measured first** (`BushPickTests`, through the rig's own pick path at the screen
position of each drawn bush on screen, plain, ripe and picked): **14 of 18 clicks on the middle of
a bush and 56 of 73 on its crown named something else**. A bush is 1.9–2.9 m tall and its cell was
claimed only where the ray crossed the cell's floor; a ray aimed at the crown meets the ground a
metre or two further on, and in its own column the solid-ground rule claimed the ground block under
the bush first, so the pane named the grass. **Fix**: a bush's cell is a box to its drawn crown —
`SlicePicker` claims it wherever the ray is inside that column below the crown, at the point the ray
entered it — and the crown's height is the drawn one, noted per cell by the mesher
(`WorldRenderModel.BushTop`). After: **0 of 18 and 1 of 68** (the one a rim point), the pane names
every bush clicked, and **no ground in front of a bush is taken by it**; ground behind a bush, which
the bush hides, is the bush's. Resizing or re-centring the bushes was measured and bought nothing,
so their look is unchanged.

**The berries.** They were set on a ring at a fraction of the footprint's half-diagonal, at heights
off the bush's top, with neither the bush's turn nor the crown's shape — so most hung in the air
round it. They are placed through the bush's own drawn matrix now (`ChunkMesher.TryBushPlacement`,
the one owner of where a bush is drawn), on the crown's upper dome as the bounds describe it,
set 15 % inside so they sit in the leaves. The meshes are not readable at run time and nothing of
the art is copied: the bounds are the resolved module's. **Left out**: the berries do not sway with
the bush's wind bow (0.18 of the grass's); set into the leaves the difference is a few centimetres.
`ChunkMesher.BerriesOnTheCrown = false` draws them as first built, for the before photograph.

## 13. Placing over growth (2026-09-25)

Owner, playing #222: *"the grass/bushes/foliage is getting in the way of placing any orders — I
can't see the blueprint and that needs to be clear so I know where I'm placing these blueprints."*

While **any build or order tool is armed** the footprint it would act on — the cell under the
pointer, the dragged box, or a build's boxes where it steps up a riser — is cleared, and nothing is
cleared while no tool is armed (`PlacementClearing`, `OdysseyBootstrap.ClearForPlacement`):

- **The grass lies flat** under the footprint and 0.8 m round it: one soft rectangle per footprint
  box stamped into the clearance field the tufts, stands, flowers and cover already read
  (`GrassClearance.StampRect`), updated every frame as the pointer or the drag moves.
- **Bushes, stones and trees standing over the footprint fade**, through the see-through
  partition the colonists use. A bush near a placement comes off the indirect path for those
  frames so it can. **This is the one exception to "bushes never fade"**: a colonist walking through
  a bush still does not fade it.
- **A waiting site keeps its grass flat** until it is built, as an order's mark and an item do.

**Measured** (`BushPickTests.PlacingOverABushAndTallGrass`, a 24 × 24 wall box armed, three arms in
one world): 2.57 ms cleared, 2.10 ms armed without the clearing, 2.11 ms with nothing armed. The
first version cost 2.4 ms, all of it a square root per texel of the stamp; the inside of the
rectangle is written flat now and only the margin pays for a distance. Photographs:
`placing-before.png` and `placing-after.png` from the same test.
