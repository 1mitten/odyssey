# Roofs — RF1: roofing a building, and seeing under it

**Status:** design settled by owner interview 2026-09-20; merged with `main` 2026-09-26, **§4 dropped
on the merge (§4a)**; implementation on `claude/adoring-ptolemy-baq5te`, PR #143. **This was design 27
until the merge**; `main` had given 27 to the Work tab and the graphics settings.
**Read first:** `02-world-and-layers.md` §4 (*"a roof is a floor is a slab"*, and the support rule
this unit does not change), `17-floors-and-collapse.md` (the unit that built roofs, whose §3
amendment matrix RF1a extends by exactly one row), `15-building.md` §2 (the two tables a new
buildable joins, and why the numbers below are nobody's to derive from),
`a-05-rooms-and-beauty.md` (the enclosure model this unit deliberately does **not** start).

**A roof is not a new thing to build.** It has existed since U29 and the interface already calls it
a Slab. What has never existed is roofing as something a player *does*: you cannot roof the room you
are standing in, you cannot see what you have roofed, and you cannot roof anything wider than six
cells. This unit is those three, and nothing else.

## 1. What already existed

Grounding first, so the work is the gap and not the whole. **Every row here was checked against the
code, not against a status line** — `CLAUDE.md` says to, and this unit is the case for it: three
separate things that sounded like work turned out to be built.

| Piece | State before this unit |
|---|---|
| A roof *is* a slab — one thing, of a material, at a layer boundary, stored on the upper cell | **Decided** in `02` §4 and `03` layer question 2; **built** as U29 |
| The Slab tool, its key `ui.arch.tool.roof`, its def `Building_Floor` | **Built.** The key has said "roof" since the catalogue was written; only the label has moved |
| Roofing a whole room in one drag | **Built** — `ConstructionGrid.RunLayerFor` lands a dragged run on one layer, `FloorRunTests.AFloorDraggedOverARoomRoofsAllOfIt` |
| Ordering a floor onto a wall top | **Built** — `ConstructionGrid.StandingOver`, U29's review amendment |
| Taking the layer from the slice so a room's *interior* can be named | **Built** — decision 12, `DesignateDirector.WorkingLayer` |
| `CoreContent.SlabRoof`, stamped on shell templates by worldgen | **Built** |
| `CellGrid.IsRoofed(index)` — the one-lookup-up query `02` §2 designed the storage for | **Built, and has no caller at all** |
| `ChunkBatch.Roof`, a separate draw bucket so the cut-away drops the lid without remeshing | **Built** — ADR 0006's "separably cullable" requirement, met |
| `AboveMode.RoofsOff`, `GraphicsOption.CutAwayCeiling`, `SliceSettings.SuppressCeilingAt` | **Built.** `RoofsOff` is unreachable while `followDepth` is on; `CutAwayCeiling` drops one layer only |
| `WorldRenderModel.OpenToTheSky` — "is this roofed", presentation side | **Built**, and drives only the daylight tint bit |
| A pillar as a thing the support solver trusts | **Built and never noticed.** `SupportSolver.IsGrounded` returns true for *any* edifice below, so a pillar already grounds the slab over it at `S_max` |
| `CoreContent.EdificePillar`, `ModuleShape.Pillar`, `SM_Bld_Base_Pillar_01`, `ui.arch.tool.pillar` | **All four exist.** The def, the handle and the palette row are what is missing |
| Enclosure, indoors, rooms, weather, temperature, per-cell light | **None of it exists**, in either assembly. M4 |

So the unit is small, and that is the finding rather than a disappointment.

## 2. The decisions

Six questions, owner 2026-09-20. Every recommendation was taken.

| # | Question | Decision |
|---|---|---|
| 1 | What is RF1 for? | Ergonomics and seeing in. Not the look, not enclosure |
| 2 | A separate Roof tool beside the Slab tool? | **No.** One tool. A slab floors the layer it is in and roofs the one below; it is already both |
| 3 | If a pitched roof is drawn, what happens on top of it? | **The cap makes the roof non-walkable.** Decided now, built in RF2 |
| 4 | How does the player see back in? | **Drop every roof above the slice**, not only the one overhead |
| 5 | The support pillar, in or out? | **In** |
| 6 | The roofs overlay, in or out? | **Out** — RF3 |

Four more were settled here rather than asked, because a measurement answered them:

7. **The lift has one owner, and it is `ConstructionGrid.WhereItWouldLand`.** `WorkingLayer` stays
   the slice pin. See §3.
8. **The lift keys on a slab, not on "anything you could stand on"** — the looser rule puts a slab
   in the sky, measured. The cost is that RF1 fixes the upper storey and not the ground floor. §3.
9. **A pillar blocks its cell.** §5.
10. **The `steps >= 2` drop is unconditional, not gated on `CutAwayCeiling`, and takes slabs only.**
    §4.

**Decision 4 as asked was "drop every roof above the slice", and what shipped is every roof from
two layers up.** The storey *directly* overhead is left exactly as it is, because the setting that
governs it was turned off by the owner on 2026-09-17 for a reason that still holds — *"I expected
to see and be able to build at least floor above from my current height"* — and that reason is
about one layer. Taking it as asked would have reversed that decision as a side effect of a
different one. §4.

### Why decision 2 is not the obvious answer

A "Roof" chip that aims one layer up from the click would read better to somebody meeting the game,
and the registry key is *already called roof*. It was refused because the two tools would differ by
nothing the simulation can see — one `BuildingDef`, one handle, one `Raise` path, one slab in the
cell — and a rule with two owners is the first pattern in `docs/bug-patterns.md`. The friction being
complained about is not "there is no roof tool"; it is that the slab tool cannot name the cell over
your head. That is §3, and it is one condition.

### Why decision 3 is recorded here and built later

Pitched roofs are RF2, but the question *"what happens on top of one"* had to be answered before
the cap could be scoped, and the answer changes the simulation rather than the drawing: a capped
roof stops being a deck. Recording it now stops RF2 re-opening it. §7 has the art.

## 3. Roofing the room you are standing in

**The fault.** Stand at layer `L` inside a walled room, arm the Slab tool, click the floor. The
picker names the room's own floor cell at `L` — correctly; its contract is to stop the ray at the
first surface, and *"a pointer cannot name a cell of open air"* (`17-floors-and-collapse.md` §3).
`WorkingLayer` is `L`, so it does not lift. `StandingOver` asks whether the cell is *filled* — it is
not, it is a room you are standing in — so it does not lift either. `AllowsSlab(L)` then refuses,
because `Floor[L] != SlabNone`: there is already a floor there, the one under your feet.

Nothing is logged and nothing is drawn. It is exactly the silent refusal `15-building.md` §6 is
about, and the player's workaround is to raise the depth rail one storey first — which is decision
12 working as designed and is a step nobody should have to know about.

**The rule.** A slab ordered at a cell that **already holds a slab** means the boundary one layer up.

That is `StandingOver`'s existing sentence with one clause added. It was *"a slab ordered at
anything that fills a cell — solid terrain or an edifice — means the boundary on top of it"*; it is
now that, **or at anything that already carries a slab**. The same idea from two sides: you cannot
put a slab where a slab is, so the order means the next boundary up.

**A slab, and deliberately not `HasFloor` — this was measured and it is the whole shape of the
rule.** The obvious spelling is *anything you could stand on*, which would also catch a ground-floor
room, whose floor is terrain rather than a slab. It cannot be had. On open meadow the air cell over
the ground answers `HasFloor` true, so the looser test would lift it again — and one layer above
*that*, the support rule returns **3** whenever a wall stands beside it, because the slab over the
wall's head is grounded. A click on the grass beside a wall would order a slab in the sky, accepted
and built. `Floor[index]` cannot reach that cell, because a cell holding a slab is never the air
over a meadow. `RoofsTests.BareGrassStillRefusesAndNeverPutsASlabInTheSky` keeps the measurement,
`SupportIfSlabAt` reading 3 and all.

**So RF1 fixes the upper storey and not the ground floor, and that is a recorded limit.** Telling
"standing inside a hut" from "standing on the meadow outside it" is enclosure — M4, and
`a-05-rooms-and-beauty.md` warns against reducing it to one boolean. **The ground floor already has
its gesture:** drag a box over the whole hut, walls included, and `RunLayerFor` takes the highest
layer any cell of the run reaches, which is the boundary above the wall tops. Measured, that works
and always has (`FloorRunTests.AFloorDraggedOverARoomRoofsAllOfIt`). What stays refused in silence
is a drag over the *interior only*, or a single click inside it. §8.

**It still lifts exactly one step**, and that is what keeps the already-roofed case honest: the step
lands on the roof, `AllowsSlab` refuses it, and the refusal is reported at the cell the player
clicked.

`ConstructionGrid.WhereItWouldLand` stays the **single owner of the lift**. `WorkingLayer`
(`DesignateDirector`, set by `DesignatePresenter.TellTheDirectorWhichLayerItIsWorkingOn`) is a
*pin*, not a second lift: it raises the named cell to the slice layer and only ever raises, then
`StandingOver` takes at most one step from there. Do not add a lift anywhere else, and do not
"simplify" the two into one — they answer different questions, and `HopPriceHasOneOwnerTests` exists
because a price with two owners fails silently.

### The matrix, extended by one row

`17-floors-and-collapse.md` §3 measured this table rather than arguing it. One row changes.

| What the player points at | Cell the picker returns | Before RF1 | After |
|---|---|---|---|
| A wall's top face | the wall's **own** cell | site one cell up | unchanged |
| Bare grass | the ground **block** | `NotPermitted` | unchanged — the air above already has a floor |
| Bare grass **beside a wall** | the ground **block** | `NotPermitted` | unchanged, and this is the control: `HasFloor` would have made it a slab in the sky |
| **An upper storey's floor, slice on that storey** | **that slab's own cell** | **`NotPermitted`, in silence** | **site one cell up: that storey is roofed** |
| A ground-floor room's interior, one cell | the ground block under it | `NotPermitted` | unchanged — its floor is terrain, not a slab. §8 |
| A room's floor, slice one storey up | that floor's cell | site on the slice layer | unchanged (decision 12) |
| An already-roofed cell | that slab's own cell | `NotPermitted` | `NotPermitted` — one step lands on the roof |

**The lift is conditional, which is the other half of why the sky case stays shut.** `StandingOver`
moves the order only if the cell above actually `Allows` it, so where nothing could hold a roof up
the order does not move and the refusal is still reported where the player clicked. That guard is
U29's and is untouched; `TheFloorOfAnUpperStoreyIsRoofedByPointingAtIt` asserts both sides of it.

**Paving and walls are untouched, and not by accident.** `Building_DeckPlate` is a `covering`, so
`WhereItWouldLand` routes it through `StandingOn` and never reaches this rule — which is the same
reason U42 had to be kept out of decision 12. A wall is not a slab and takes `StandingOn` too.

## 4. Seeing under a roof

**The fault.** Above the surface `SliceSettings.AboveAt` returns `AboveMode.Full`: every layer above
is drawn solid. `SuppressCeilingAt` drops only `steps == 1`, and `GraphicsOption.CutAwayCeiling`
defaults **off** — the owner turned it off on 2026-09-17 (*"I expected to see and be able to build
at least floor above from my current height"*). So roofing a building and then working two storeys
below it leaves the colony under a plate, and the roofing this unit has just made easy is the thing
that causes it.

**The rule.** A built roof two or more layers above the slice is never drawn.

```
if (above && aboveMode == AboveMode.RoofsOff) drawRoof = false;
else if (steps >= 2) drawRoof = false;                                          // new
else if (steps == 1 && slice.SuppressCeilingAt(activeLayer)) drawRoof = false;   // unchanged
```

`steps >= 2` is **unconditional**, not gated on the setting, and that is the point: the storey
directly above stays visible and clickable, so the owner's 2026-09-17 complaint stays answered and
you can still build the next floor from below — but nothing higher can ever hide you. The setting
keeps its exact present meaning for the one layer it was turned off about.

The predicate lives in `SliceSettings` beside `SuppressCeilingAt`, and `ChunkRenderer` and
`SlicePicker` both call it. **Never two copies**: those two classes are in different assemblies,
neither test tier sees both, and that is precisely how they came to disagree before
`FloorToolReachTests` was written.

### It must take the slabs and leave the ground

`ChunkBatch.Roof` does not hold roofs. It holds *everything the cut-away drops*, and
`SurfaceContributor` puts **ground surfaces** in it alongside built slabs. Dropping the whole list
two layers up would therefore delete a higher terrace's grass, and `CLAUDE.md` is flat about it:
**"The landscape is never cut away."**

**That sharing is deliberate and is not a bug to fix.** `SurfaceContributor` says so in its own
comment — *"in the roof list so that a storey above the slice can drop its ground"* — and it is
right for the two rules that already exist, because both mean *let me see into the storey directly
overhead* and its ground is exactly what is in the way. RF1's rule reaches further, where the thing
over your head is as likely to be a hillside. So the existing rules are **left exactly as they
are**, and only the new one is slab-only:

| Rule | Reaches | Drops |
|---|---|---|
| `AboveMode.RoofsOff` | every layer above | the whole list, ground included — unchanged |
| `SuppressCeilingAt`, `steps == 1` | one layer | the whole list, ground included — unchanged |
| **`RoofIsAlwaysDropped`, `steps >= 2`** | **everything higher** | **slabs only** |

**Told apart by the tint, not by a fourth bucket list.** Every terrain contribution carries
`TintCode.TerrainBase` and a slab is tinted `TintCode.Stuff(...)`, which does not — and water keeps
the terrain bit (`Water(t) = WaterBase + TerrainBase + t`), so a pond two storeys up survives. So
the renderer skips a bucket when `!TintCode.IsTerrain(bucket.Tint)`, which is the same mechanism
`ChunkRenderer.NeverFades` already uses to keep the sight fade off banks and marsh.

**A third bucket list was written first and reverted**, and the reason is worth keeping. It is the
tidier model, and it reshapes a structure that **seven Unity-tier test files read directly** —
`DaylightTintTests`, `WaterFaceTests`, `ChunkBucketScaleTests`, `TreeBucketTests`,
`MeshContributorTests`, `ChunkMesherTests` and `SlicePickerTests`, eleven sites between them. None
of them can run in a container without Unity. A change that cannot be proven is a change that gets
pushed on hope, and this line of work has had three silent failures already. **The tint test buys
the same behaviour with no test-shape change at all**; the bucket split stays available for a
session that has the editor in front of it.

### And the picker's half is per cell, not per layer

`SlicePicker` must refuse exactly what the renderer skipped. Its existing `floors` flag is per
*layer* and gates the horizontal pick for every cell alike — a hillside's included — so switching
it off two layers up would have left that terrace **drawn and unclickable**: the same
renderer-and-picker disagreement `FloorToolReachTests` was written about, in the other direction.
So RF1 passes a second flag and asks it of the cell: `slabsDropped && model.Floor(index) != 0`. A
cell carrying a slab offers nothing; the grass beside it still does.

### 4a. Dropped on the merge with `main`, 2026-09-26 — walls-down answers it

Nothing in this section is built. **Walls-down** (`42-walls-down.md`, PR #197) reached `main` while
this sat in review and answers the same complaint more broadly: *"it is hard to see your colonists
inside your own building."* It hides every **stacked** storey above the slice — built on something
built rather than on the ground, which is exactly what a roof is — keeps the landscape, and is on by
default. With it on, `RoofIsAlwaysDropped` changes nothing; with it off, the player has asked to see
the building whole.

Keeping both would give the renderer and the picker two owners for one question — *what above the
slice is hidden* — each with its own per-cell test, and they would disagree the first time either was
corrected. That is P1, and this project has paid for it before. §4 had never been played, so dropping
it costs no verdict. `ChunkRenderer` and `SliceSettings` are `main`'s; the picker keeps only the
stair's foot plane (`60-stairs.md` §8b), which is a different question.

**If a roof two storeys up still hides a ground floor in play**, it will be in build mode, which
walls-down deliberately ignores. The answer then belongs in `WallsView`, beside the rule that already
owns the question, not in a second predicate here.

The pillar gained **200 hit points** on the merge (two thirds of a wall's; invented), and
`BuildingHandle.Pillar` is **14** (written as 7). The city's stamped pillars share edifice 4, so they
now have hit points too, as its stamped walls always have.

## 5. The support pillar

**Why it is in a unit about ergonomics.** `S_max` is 4 and support decays one per cell, so a slab
stands at most three cells from anything holding it up. Past a certain width the middle of a roof is
refused, per cell, in silence — the player gets a roof with holes in it and no explanation.

**Measured rather than derived, and the arithmetic was wrong.** A first pass reasoned the limit to a
six-cell interior. The probe disagreed, because a sweep is not a static span: a cell ordered early
supports the cells ordered after it through `SupportedByWhatIsPlanned`, so the answer depends on the
order a drag visits cells in, which no closed form sees. Sweeping a roof on as a drag does:

| Room, cells on a side | Interior | Cells refused |
|---|---|---|
| 6 × 6 | 4 | **0** |
| 8 × 8 | 6 | 1 |
| 10 × 10 | 8 | 9 |
| 12 × 12 | 10 | 25 |

**A hut roofs completely and the holes then grow faster than the room does.** That is the argument
for the piece, and it is stronger than the one the arithmetic gave. One pillar in the middle of a
10 × 10 closes all nine — `RoofsTests.AHallTooWideToRoofIsRoofedWholeOnceAPillarStandsInIt`, which
carries the un-pillared count as its own control so it cannot pass vacuously.

**The solver does not change.** `SupportSolver.IsGrounded` already ends with
`return _grid.Edifice[below] >= 0` — *any* edifice underneath grounds the slab at `S_max`. A pillar
has therefore worked for as long as the solver has existed; nothing could build one.

| Piece | State |
|---|---|
| `CoreContent.EdificePillar = 4` | exists |
| `WorldRenderModel.OccludesFace` lists it | exists |
| `ModuleShape.Pillar`, `SM_Bld_Base_Pillar_01` in the catalogue | exists |
| `ui.arch.tool.pillar` — *"Support pillar / Extends how far a roof can span"*, mapped to sheet 04 | exists in both CSVs |
| `Building_Pillar`, `BuildingHandle.Pillar`, a palette row | **the unit** |

```xml
<BuildingDef>
  <defName>Building_Pillar</defName>
  <label>support pillar</label>
  <edifice>4</edifice>
  <slab>false</slab>
  <blocking>true</blocking>
  <costCount>3</costCount>
  <workToBuild>90</workToBuild>
  <minSkill>0</minSkill>
  <iconKey>ui.arch.tool.pillar</iconKey>
</BuildingDef>
```

**`blocking` true, and it is a decision rather than a detail.** A pillar fills its 2.5 m cell, so it
costs a cell of floor to buy span — you cannot walk through a column, and the roof you wanted is
paid for in the room you wanted it over. The alternative is a non-blocking pillar like a door, which
would make a pillared hall strictly better than an unpillared one and the choice free. Either works
for the solver, which reads `Edifice[below] >= 0` and not the flag.

**3 stuff and 90 ticks**, against a wall's 5 and 135 and a slab's 4 and 120: less material than a
3 m wall panel, dearer per cell than a slab. **Neither number is derived from anything and nothing
derives from them; they are the owner's to tune**, exactly as `15-building.md` §2 says of the others.

`BuildingHandle.Pillar` is **appended** to `ConstructionContent.BuildingOrder`. Handle order is the
save contract; a value inserted in the middle would compile silently and mean something else in
every save already written.

## 6. Test procedure

Every one of these is a claim that can fail. `Assets/Odyssey/Tests/Sim/RoofsTests.cs`, eleven tests,
all green in the fast tier.

**The lift (§3).** `TheFloorOfAnUpperStoreyIsRoofedByPointingAtIt` carries both sides of the
conditional lift — with no second storey the order does not move off the cell clicked, with walls it
lands one layer up and is taken. `AWallTopStillTakesTheSlabOnTopOfIt` and
`PavingIsUntouchedByTheSlabLift` are the unchanged rows, and without them the new clause could
swallow a case it must not. `AnAlreadyRoofedCellRefusesRatherThanRoofingTheStoreyAbove` pins the
one step.

**`BareGrassStillRefusesAndNeverPutsASlabInTheSky` is the one to keep.** It asserts the two facts
that shaped the rule before it asserts the rule: that the cell `HasFloor` would have admitted really
does answer true, and that the support rule really does accept the sky cell above it. Then that the
order is refused anyway, and that nothing was built up there. Delete the first two and it still
passes while proving nothing.

**The pillar (§5).** `APillarGroundsTheSlabAboveItAsFullyAsAWallDoes` is the claim that the solver
needed no change. `AHallIsTooWideToRoofAndTheHolesGrowFasterThanTheRoom` is the measurement, as
counts rather than a bare failure, because the shape of the failure is the argument.
`AHallTooWideToRoofIsRoofedWholeOnceAPillarStandsInIt` carries the un-pillared count as its own
control, so it cannot pass vacuously. Then
`TakingThePillarOutBringsDownWhatItWasHolding`, `APillarFillsItsCellSoNothingElseGoesThere` and
`APillarIsHashedAndSurvivesASaveAndReload`.

**Nothing leaks.** The goldens do not move, and that is checked rather than assumed:
`WhereItWouldLand` changes where an *order* lands, not what a raised slab is, and the Long-tier runs
name their cells in C#. **A golden that moves in this unit is a bug in this unit**, not a re-bake.

### What this unit owes, and why it could not be paid here

The session that built RF1 had **no Unity and no Windows**, so the authoritative tier never ran. The
fast tier compiles neither `Odyssey.Presentation` nor the editor, which is the whole of §4.

| Owed | Why it matters |
|---|---|
| ~~`scripts/unity.sh test editmode`~~ — **run 2026-09-21** on the tree merged with main: 2,395 / 2,374 / 0 | §4 is compiled and green. **PlayMode is still owed**, and is where the drawing is |
| A `ChunkMesherTests`/`SlicePickerTests` pair for the drop | a roof two layers up is not drawn, and the picker refuses exactly what the renderer skipped — one test each, and they must read the same `SliceSettings` predicate |
| A `FloorToolReachTests` case for §3 | the picker-to-order seam for the upper-storey click. That file exists *because* those two halves once disagreed in silence |
| A probe photographing three storeys from each of its layers | the picture that says §4 works. `FloorCheck` is the model |
| A terrace two layers above the slice, looked at | the landscape must still be there. The tint test says it is; nobody has seen it |

**By hand, and nobody has done it:** roof an upper storey by pointing at its floor; roof a hall with
one pillar in it; stand on the ground floor of a three-storey shell and check it is still lit.

## 7. The art

RF1 needs none: a pillar's module is already in the catalogue, and a roof still draws as the slab it
is. The rest is RF2's, recorded here so it is not researched a third time.

| Use | Prefab | Size | Verdict |
|---|---|---|---|
| Flat roof, walkable | `SM_Bld_Base_Floor_Combined_01` | 2.50 × 0.10 × 2.50 | **this is what a roof is today** |
| Underside skin, read from below | `SM_Bld_Base_Ceiling_01` | 2.50 × 0.00 × 2.50 | unused |
| Pitched cap: straight, inner and outer corner | `SM_Bld_Base_Roof_Straight_01`, `_Corner_In_01`, `_Corner_Out_01` | 2.50 × **3.25** × 2.50 | cap only — **a quarter of a metre over a layer** |
| Pitched fillers | `SM_Bld_Base_Roof_Half_01` / `_02`, `_Quarter_01` | 1.25–2.50 | cap only |
| Ridge, hip and end caps; eaves trim | `SM_Bld_Base_Roof_Cap_*`, `_Trim_*` | 0.15–0.20 tall | cap only, and the eaves trim is also the candidate fascia for the paper-lip problem |
| Decorative | `SM_Bld_Roof_Pagoda_01` / `_02` | 6.8 / 4.9 m | stamped ruins only, no cell fit |
| Rooftop dressing | `SM_Gen_Prop_Aircon_Roof_01`–`03`, `SM_Gen_Prop_Light_Roof_01`–`03` | props | RF2 or later |

The pitched set is a **complete one-piece-per-cell autotile kit on our exact 2.5 m pitch** —
straight, both corners, halves, quarter, ridge, hip, end, trim — which is what makes RF2 cheap. It
is also 3.25 m tall in a 3.00 m layer, which is why `e-01-module-mapping.md` and
`17-floors-and-collapse.md` §10 both rule it decoration and never a floor. **No curved roof exists
in any owned pack**; one would be Blender work and would not tile with this set.

## 8. Open

- **A ground-floor room's interior cannot be roofed by pointing at one cell of it.** Its floor is
  terrain, not a slab, so §3's clause does not reach it — and the looser clause that would is the
  one that puts a slab in the sky. What distinguishes standing inside a hut from standing on the
  meadow outside it is **enclosure**, which is M4. The whole-hut drag already works and is the
  common gesture, so this is a recorded limit rather than a blocker; what stays silently refused is
  a drag over the interior only, or a single click inside it. The cheap half-measure, if it is ever
  worth one before M4, is not a wider lift but **feedback**: the build cursor tinting the cells it
  is about to refuse, which is a cursor question and reuses the preview path.
- **Nothing consumes `CellGrid.IsRoofed`.** It is the query `02` §2 shaped the storage for and it
  has never had a caller. Its first one is M4's `IsSheltered`; `a-05-rooms-and-beauty.md` rejects a
  single `IsEnclosed` boolean and says to expect a third.
- **Pitched caps** — RF2. Decision 3 is taken; the geometry and the non-walkable rule are not built.
- **A roof draws as a floor.** `WorldRenderModel.FloorModule` ignores the slab kind, so `SlabRoof`
  and `SlabBuilt` are the same grey plate. One line, and it belongs with RF2.
- **The roofs overlay** — RF3. `ui.overlay.roofs` is named, keyed, listed in `HudShell.Bar.cs` and
  dead. It is the answer to *"did I roof all of it"*, which RF1 leaves to the eye.
- **Nothing warns.** `ui.alert.collapse` and `ui.alert.unsupported` are keyed and unimplemented, and
  felling a tree can still drop a roof on somebody in silence. U31, the support preview, is what
  makes the span rule fair — until it lands, the only way to find an unsupported cell is to order
  one.
- **A roof lip over open air reads as paper** (`CellMetrics.FloorSheet`, already in the playtest
  queue). The named fix is a fascia on the face, not a thicker plate, and the eaves trim in §7 is
  the piece.
- **`FloorCheck` roofs 28 of its 6 × 5 room's 30 cells**, and the two it misses are in the middle.
  The interior is 4 × 3, so **the span rule does not explain them** — every interior cell is within
  two of a wall. Settle it when the probe is re-shot; do not assume it is §5.
- ~~A second storey cannot actually be built yet~~ — **closed by `U44`, in this same branch.** The
  sentence was right and the reason was not: a hauler could not climb a ladder, but building
  *delivery* had never asked for the hauling mode, so a plank went up one regardless
  (`60-stairs.md` §3). U44 makes delivery a hauling job *and* makes a stair buildable in the same
  commit, so the claim and its consequence are now both true and both answered, and RF1's roof over
  an upper storey is a roof over somewhere a colony can actually build.
