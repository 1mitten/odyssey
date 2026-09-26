# 28 — Map size

Which boards ship, what each costs, and how big a board this engine could realistically carry.
The cell and layer *model* is `02-world-and-layers.md`; the setup *page* is `19-world-setup.md`.
This document owns the list and the numbers behind it.

Written 2026-09-21, when the owner asked for a board twice the size of the standard one and for an
honest answer about the ceiling.

## 1. The boards

Named rather than numeric on the page — "120 × 120 × 16" is a fact about an array, "Standard" is a
choice about a game (`19-world-setup.md`). `MenuDirector.MapSizes` is the list; `MapSizes.Default`
is Standard and stays Standard.

| | Cells | Render chunks | Layer stride | Nav blocks | Metres across |
|---|---|---|---|---|---|
| Small 80 × 80 × 16 | 102,400 | 256 | 6,400 | 1,024 | 200 |
| **Standard 120 × 120 × 16** | 230,400 | 400 | 14,400 | 2,304 | 300 |
| Large 180 × 180 × 24 | 777,600 | 1,536 | 32,400 | 7,776 | 450 |
| **Huge 240 × 240 × 16** | 921,600 | 1,600 | 57,600 | 9,216 | 600 |
| *(scale target, ADR 0002)* 250 × 250 × 40 | 2,500,000 | 4,000 | 62,500 | 25,000 | 625 |

**"Four times Standard" is true of cells and of very little that costs.** Huge is 1.19× Large's
cells and 1.04× its render chunks, because a chunk is 25 × 25 within one layer and Large carries
eight more layers. The one axis where Huge clearly exceeds Large is the layer stride, 1.78× — and
that is `EnclosureGrid.SolveLayer` and the unread slice channel, and nothing else. Anyone quoting
"4×" at a frame number is quoting the wrong multiplier.

### Why 240 × 240 × 16

**Twice the ground, at Standard's depth.** The owner asked for twice in every direction; the depth
half was cut deliberately and the reason is worth keeping.

`NaturalMapGenDef.GroundLayerFor(size)` is `SizeY - 1 - headroomLayers - surfaceRelief`, which at
16 layers is `16 − 1 − 3 − 2 = 10` — **exactly Standard's**. So Huge's subsoil, rock band, coal
depth, bedrock and surface band are Standard's vertical profile, confirmed in the generation report
(`surface 7..12` on both). One variable moves instead of two, and every depth-shaped finding on
record still applies.

**240 is not a multiple of 25** (240/25 = 9.6), so the board runs ten chunks across with a 15-cell
edge column — the same partial-edge-chunk path 120 already uses, and one `GridSaveTests` already
exercises on deliberately awkward sizes. 250 × 250 × 16 would divide exactly *and* share the scale
target's horizontal dimensions, at 8% more cells than "twice as big". Recorded as a choice, not an
oversight.

## 2. What each board costs

**Every figure below was taken on 2026-09-21, on the Windows dev machine (.NET 8 CoreCLR), one run
per table.** Read the ratios; the absolutes belong to that machine. The target is a 2022 mid-range
laptop and there is not one in the building.

### Simulation

**Re-taken 2026-09-21 on the map the game builds** — see §2a; the first set was taken on the
unmodified default def.

| Board | Live regions | Links | Generation, median of 5 seeds | Rebuild per mined cell | World memory | Save |
|---|---|---|---|---|---|---|
| Standard | 2,110 | 1,477 | 24 ms | **0.278 ms** | 15.9 MiB (72.5 B/cell) | 137 KB |
| Large | 7,360 | 3,511 | 77 ms | **0.613 ms** | 52.2 MiB (70.3 B/cell) | 356 KB |
| **Huge** | **8,406** | **6,180** | **104 ms** | **0.865 ms** | **63.2 MiB (71.9 B/cell)** | **559 KB** |
| Scale target | 24,141 | 6,772 | 235 ms | 1.472 ms † | 164.8 MiB (69.1 B/cell) | — |

† The scale target's edit tick read 1.047 ms on the first pass and 1.472 on this one with an
**identical** region and link count, so the move is this machine rather than the map. It is the
reason every figure here is a ratio first and an absolute second.

Arms: `NaturalWorldgenTests.EveryOfferedBoardGenerates`,
`NavGraphStatisticsTests.EveryOfferedBoard` (which carries `TimePerEdit`), `BoardMemoryTests`,
`GridSaveTests.EveryOfferedBoardRoundTripsAndFitsTheBudget`. All print; only the pre-existing
budgets assert. All four build through `PlayedMap`, which calls `ColonyWorld.DefFor` — **the game's
own chooser, not a copy of it** (§2a).

### 2a. The first set of these numbers was taken on the wrong map

**Found from an owner's play log, not from a test**, which is the part worth remembering. Their
session on 120 × 120 × 16 reported `patches 0, trees 1598`; the arms were reporting `patches 2210,
trees 1222` for the same board.

The cause was two owners for one choice. The played scene sets `barrenMap: 1, woodedMap: 1`, so
`ColonyWorld.Build` applied `MakeWooded()` on top of the default def — while every arm reached for
`MapGenerator.DefaultDef` / `NaturalMapGenDef.For` and got the def **without** it.
`BoardMemoryTests` was further out still: `ColonyWorld.Build`'s `wooded` parameter defaults to
`false`, which is `MakeBarren()`, a board with no trees and no water at all.

**The fix was to remove the second owner**, not to copy the first: `ColonyWorld.DefFor` is now the
one place that decision is made, `Build` calls it, and `PlayedMap` calls it. `PlayedMapTests` holds
it to an observable property of `MakeWooded` — trees present, patches zero — rather than to itself,
with a negative control that fails if the helper ever becomes a synonym for the plain default.

**What it moved, and it is less than feared.** Live regions and links are **identical on every
board**, so the region counts, the edit tick and therefore the whole ceiling argument in §6 are
unaffected — trees and surface patches do not change how the region graph carves a block. What moved
is generation time (a wooded board carries more trees to place), the feature counts in §4, memory by
about 2 B/cell, and save size by a few per cent.

**What it cost was confidence, not conclusions**, and the general form is `docs/bug-patterns.md`
P18 — which was written one commit before this was found, in this same branch, and not applied to
the arms it was written about.

**A tick at rest is 0.065 ms on Standard and 0.068 ms on Large — the board does not appear in it at
all.** Everything that matters is in the edit column.

**Huge costs 2.96× Standard's edit tick.** At speed 3 that is about 2.6 ms of a 16.6 ms frame
against 0.9 ms today: real, visible in a profile, and nowhere near fatal.

**`docs/audit/2026-09-19-baseline.md` is corroborated.** It measured 0.449 ms (Standard) and
1.150 ms (scale target) on a 4-core Xeon container; the same shape reads 0.298 and 1.047 here.
Nothing about the audit's conclusion changes.

**Memory is 69.8 bytes per cell**, simulation side, across eight owners. The ninth is
`WorldRenderModel`, a second full mirror of the grid on the presentation side at roughly 18 B/cell —
so a played Huge board is about **78 MiB** of arrays, allocated in one burst when the world is
built. `CellGrid`'s docstring claiming "22 MB at 2.5M cells" is stale by a factor of 1.7; its six
arrays are 15 bytes wide.

### Frame

`FrameTimeTests.TheBoardSizeAgainstTheFrame`, 2026-09-20 23:46Z, 180 timed frames a board after
120 of warm-up, wooded map, ~900 standing orders on each, **640 × 480 on an RTX 5070 Ti**. One
Unity was live on another project throughout, so **read the ratios**; the timing tests in the same
run all passed, which is the tell that contention was not gross.

| Board | Frame | `World` | `Doors` | Draw calls | Instances | Chunks drawn | Surround batches |
|---|---|---|---|---|---|---|---|
| Standard | 3.18 ms | 2.146 ms | 0.000 | 1,475 | 42,821 | 104 | 266 |
| Large | 6.09 ms | 4.513 ms | 0.000 | 3,205 | 78,319 | 250 | 430 |
| **Huge** | **7.82 ms** | **5.950 ms** | 0.000 | **5,392** | **125,380** | **443** | 464 |

**Huge is 2.46× Standard's frame and 2.77× its `World` term, and it is over the 5 ms budget.**
Everything outside `World` is flat, as expected: `Figures` 0.062–0.068 ms, `Overlays` 0.234–0.251,
`Mirror`, `Sight`, `Audio` and `Actors` at or under 0.02. **The board shows up in exactly one term**,
and that term is draw submission — 5,392 calls at roughly 1.1 us each.

**A prior stated before this measurement was wrong and is worth keeping as the correction.** The
mark-pass work found `World` flat at 1.9–2.5 ms across a 48-fold *colony*, and this document
originally reasoned from that to "Huge is 4× a ~2 ms term, not 4× a 5 ms frame". `World` is flat in
the colony and **not** flat in the board — which is the whole point of §3.2 and should have been the
expectation. A term that does not move with what is happening on the board is exactly the term that
moves with the board.

**`Doors` reads 0.000 on every board, and that is a gap rather than a result.** `EnsureDoorList`
rescans only when `WorldRenderModel.Version` moves, and this arm designates and then lets the world
settle, so the 922k-cell scan is never exercised. The hazard in §3.2 is unmeasured, not absent.
**Measuring it needs an arm that keeps editing while it times**, which is the same shape as
`MineOneCell` and is the obvious next thing.

**Chunks drawn rise 4.26× where the frame rises 2.46×**, so the cost is sublinear in chunks and the
slice band is already doing real work — 443 of Huge's 1,600 chunks are drawn at all. But there is
**no frustum or distance test**, so all 443 are walked and submitted wherever the camera points; on
a 600 m board seen through a 160 m camera (§5) most of them are off-screen. **That makes frustum
culling in `ChunkRenderer.Render` the clear first fix, and it is now evidence rather than a
suspicion** — it is also the only behaviour change HT8 still had to decide.

The surround grows with the perimeter as expected: 266 → 464 batches, 1,330 → 2,095 near trees.
`TerrainSkirt.TreeSectorMetres = 400f` was tuned on a 300 m board and is the second lever if the
first is not enough (`06-rendering-and-camera.md` §6c).

## 3. What scales with the board, and what does not

Ordered by what it costs, not by how bad it looks.

### 3.1 The navigation rebuild — the only measured simulation cost that grows with the board

`NavGraph.Rebuild` floods only the dirty 10 × 10 × 1 blocks, which is correct and cheap, and then
runs four **unconditionally global** passes: every portal edge rebuilt and sorted, the whole region
adjacency CSR rebuilt, every district re-flooded once per traverse mode, and the layer-change
estimate recomputed. One edited cell costs the same as five, and the cost tracks live regions and
links rather than cells.

**HT1 makes this local** (`docs/plans/vertical-slice.md` §HT). After it, the simulation stops caring
about board size almost entirely: cost becomes proportional to the edit. The precedent is
RimWorld's, and it is worth naming because we already have the hard half — a region may never leave
its grid square, which is `NavGraph.BlockSize = 10` here and a 12 × 12 grid there, so a region is
bounded at 100 cells whatever the map is. What we lack is the incremental relink. HPA\* (Botea,
Müller, Schaeffer 2004) is the same idea formally: a topology change inside a cluster is answered by
recomputing that cluster and nothing else.

### 3.2 Per-frame work that scales with the board rather than the view

| Where | What it does | Standard | Huge |
|---|---|---|---|
| `DoorDirector.EnsureDoorList` | scans **every cell** for doors, per frame, whenever anything has been edited — it keys off the global `WorldRenderModel.Version` | 230k cells | **922k cells** |
| `ChunkRenderer.Render` | walks every chunk of every drawn layer; **no CPU frustum or distance test**, only an empty-chunk short circuit | 400 chunks | **1,600 chunks** |
| `ChunkRenderer.BatchFor` | `WorldRenderModel.Version` is one global counter, so any edit re-meshes every chunk the loop reaches, with no per-frame budget | 400 × 625 cells | **1,600 × 625 cells** |
| `EnclosureGrid.SolveLayer` | full-layer flood per dirty layer, and one wall dirties two | 14,400/layer | **57,600/layer** |
| `GridMirrorContributor.WriteSliceChannel` | one byte per column of the active layer every tick, to a channel nothing reads (HT7) | 14,400 B | **57,600 B** |

`process.md` §3 already states the rule these break — *presentation per-frame work scales with what
is visible, never with the board* — and all five predate it.

**Frustum culling in `ChunkRenderer.Render` is the one behaviour change HT8's numbers now decide.**
The other candidate, batching the per-cell order marks, was done and merged on 2026-09-20, and the
4.6 us-per-submission constant that made it look urgent turned out not to apply to a mark plate
(`06-rendering-and-camera.md` §6c.1).

### 3.3 Not board-shaped, and larger than anything above

`PawnPose.Of` scans every other pawn for the crowd sidestep, once per posed pawn, every frame:
**13.3 ms of a 22.5 ms frame at 384 colonists** against 0.02 ms at 64 (`06-rendering-and-camera.md`
§6c.2). It does not care how big the map is, and it is larger than every row in §3.2 put together.
Any measurement that varies the colony *and* the board will read this as a board-size effect — which
is why `TheBoardSizeAgainstTheFrame` holds the colony fixed.

### 3.4 Checked and safe — do not spend time here

`GridSize.Index` is a plain `int` multiply-add with no coordinate packing, and no cell id is narrower
than `int` anywhere. `TreeLook.Pack` packs 16 bits an axis, good to 65,535. `AlertModel`'s
dismiss-key hash is a sequential multiply-xor inside `unchecked` and survives well past these boards.
The index into `MapSizes.All` is **not persisted anywhere** — not `PlayerPrefs`, not
`ViewStateSection`, not `SaveRecipe`; a save carries `SaveHeader.Size` and `BuildSession` reads it —
so adding a board cannot invalidate a save or a preference. `.setup__size` is 140 px and every name
fits. Save chunking absorbs it: 400 chunk records against Standard's 100.

## 4. What the generator does *not* scale with the board

Feature densities are per 10,000 columns, so a bigger board gets proportionally more of them
automatically — Huge draws 6,248 trees, 403 ore deposits and 17 caverns against Standard's 1,413,
100 and 4. (A wooded board carries no bare patches at all: `MakeWooded` zeroes
`barePatchThreshold`.) **Three things are per-map absolutes and therefore get relatively scarcer as the board
grows:**

- `streamCount = 2` and `riverFords = 2`. Measured across the four boards: **2, 4, 7 and 5** water
  shapes for Small, Standard, Large and Huge. Huge has **fewer** than Large despite covering nearly
  twice the ground, so a Huge board is markedly drier per acre, and whether that reads as dry is an
  owner call.
- `maxForcedFords = 3` and `minReachablePercent = 80` are whole-map numbers while ponds are
  per-area, so Huge draws roughly four times the ponds against the same three-ford budget. This is
  the combination that can make `EnsureReachable` throw *"The water shapes have severed the map"*.
  **It did not, on twenty boards** (four sizes × five seeds, `EveryOfferedBoardGenerates`), and that
  arm is there to catch it if it starts.

**And every noise period is in cells, not fractions of the board** — `surfacePeriod = 34`,
`coverPeriod = 13`, `treeClumpPeriod = 11`, `pondEdgePeriod = 7`. A Huge board is *more map at the
same grain*, not the same map enlarged. Almost certainly what is wanted; a thing to look at rather
than to assume.

## 5. Two camera numbers a bigger board makes visible

Neither is changed here, and both belong to the owner.

- **`SliceCameraRig.maxDistance = 160f`.** `Frame()` asks for `max(X, Z) × 2.5 × 0.9` — 270 m on
  Standard, already clamped to 160, so "see the whole map" is *already* not true. On Huge it asks
  for 540 and still gets 160, showing about an eighth of the board.
- **`panSpeed = 26` m/s** is 23 seconds to cross 600 m at base speed.

A bigger board at the same zoom and the same pan speed is not a wider view — it is more scrolling.
That is the first thing a player will notice, and it is a feel decision rather than a bug.

## 6. So how big could we realistically go?

The ceiling is set by three things, in this order, and **only the first is about cells**.

1. **The four global navigation passes.** Today, board size costs roughly 0.3 ms of every edit-tick
   per 2,000 live regions. At the scale target that is 1.05 ms, or 3.1 ms of frame at speed 3.
   **After HT1 this term stops depending on the board.**
2. **Per-frame work that scales with the board rather than the view** (§3.2). All five rows are
   fixable to scale with what is visible; none is fixed.
3. **The colony, not the board.** Pawns, items, standing orders, designated cells — §3.3 is already
   the binding term at Standard and it does not care how big the map is.

**Measured, the answer splits.** In the tick, Huge is comfortable: 0.883 ms an edited cell and
nothing at rest. In the frame it is **not** — 7.82 ms against a 5 ms budget, at 640 × 480 on a
development GPU, before the target laptop is considered at all. **So today, Standard is the board
this renderer is sized for, Large (6.09 ms) is already over, and Huge ships as a choice a player
makes with that cost.**

**One fix is expected to change that, and it is named.** All 443 of Huge's drawn chunks are
submitted wherever the camera points, because `ChunkRenderer.Render` has no frustum or distance
test; on a 600 m board seen through a 160 m camera most of them are off-screen. Frustum culling is
HT8's remaining decision and is now backed by a number rather than by a suspicion. **Until it is
done, treat 7.82 ms as what Huge costs.**

**With HT1 and view-scaled rendering, the design target of 250 × 250 × 40 is reachable and the
board stops being the limiting number.** Memory puts a hard stop somewhere near 400 × 400 × 40
(6.4M cells, ~430 MiB simulation plus ~115 MiB mirror) on an 8 GB laptop.

The next step up in *depth* is the one to be careful of, because our region graph allocates a region
for solid rock too (`RegionKind.Impassable`, kept so rooms and atmosphere have a substrate). Only
6.8% of the wilderness's regions are walkable, so skipping all-impassable blocks would cut the
region count roughly tenfold and make depth cheap — at the cost of M4's substrate. **A design
decision, not an optimisation**, and the deferred chunk-uniform work `02-world-and-layers.md` §2
already names.

The convergent lesson from RimWorld, Dwarf Fortress, HPA\* and voxel-storage practice alike:
**what makes a large colony map expensive is connected, reachable, searchable area — not cells, not
bytes, not triangles.** Going from 16 to 32 layers of *solid, unconnected* rock is nearly free in
principle; it costs us today only because §3.1 and the impassable regions make it so.

## 7. What not to undo by tidying

- **Standard stays the default and stays the measurement baseline.** `Golden.PlayedBoard` bakes
  120 × 120 × 16 with two hash constants; if a golden moves while a board is being added, something
  else broke.
- **The measurement arms print and do not budget.** A threshold that trips on a busy machine teaches
  people to ignore red. What they assert is that the fixture measured something real —
  `MineOneCell.Mined` equals the tick count, a board drew chunks, the heap delta is not a collected
  world. Those controls are the point; do not remove them as noise.
- **`TickBenchmarkTests` does not build the game's world.** Its 11 × 11 room lattice with scattered
  rubble carries 19,606 regions at 120 × 120 × 16 against a generated map's 2,110, and 207,293 at
  the scale target against 24,141. Since rebuild cost tracks regions almost exactly, its edit arm
  reports about an order of magnitude more than the same edit costs in the game. It is a fair stress
  world and a bad way to price a board; **quote `NavGraphStatisticsTests` for that.**
- **`BoardSizes` mirrors `MapSizes` because Sim does not reference Hud**, and
  `MenuDirectorTests.TheMeasuredBoardsAreTheBoardsOffered` is the only thing stopping the two
  drifting. Change a board in one place and that test says so; do not answer it by deleting it.
- **`TheBoardSizeAgainstTheFrame` is one test and not three arms**, and it holds the colony fixed.
  Both are deliberate (§2, §3.3).

## 8. "A main area of activity, and a cheaper beyond"

Asked by the owner, 2026-09-21, alongside streaming chunks, load screens and a procedurally
generated outer zone. Written down because it is the obvious idea, it will be proposed again, and
**three of its four halves are already built** — so the answer is mostly a map of where to look
rather than a refusal.

### 8.1 On the simulation side it is already true, and measured

`TickGroup` is `Never / Normal / Rare (250) / Long (2000)` with hash-offset phase spreading, so
twenty thousand rare tickers cost eighty a tick rather than twenty thousand every two hundred and
fiftieth (`Sim/Ticking.cs`). **Cost scales with ticking things, not with cells.** The measurement:

| Board | Cells | Tick at rest, 50 colonists |
|---|---|---|
| Standard | 230,400 | 0.065 ms |
| Large | 777,600 | **0.068 ms** |

Three and a bit times the cells for four per cent of the cost. **An empty corner of the board
already costs essentially nothing**, so there is no per-tick work out there to switch off and an
activity radius would be switching off nothing.

### 8.2 The one sim cost that does scale is the one an activity radius must not touch

`NavGraph.Rebuild` (§3.1) genuinely tracks the board. But reachability is the single worst
candidate for a distance approximation: a hauler has to know it can reach a far stockpile and a
colonist has to know the route round the lake. Make that fuzzy past a radius and jobs fail
silently — which is precisely the fault the growing-zone review found, where one unreachable crop
cost 159 failed jobs in 2,000 ticks.

**HT1's local rebuild is correct everywhere *and* cheap.** An activity radius would be less correct
and more code. Not a close call.

### 8.3 On the presentation side the "main area" already has an exact definition

It is the camera. A frustum plus distance test is an activity radius that is precise, costs an
AABB test per chunk, and follows the player without being told. **An explicit radius would be a
guess at something the camera already knows exactly.**

And the coarsen-with-distance idea ships in three places already:

| Where | What it already does |
|---|---|
| `TerrainSkirt` / `SkirtLayout` | a never-simulated landscape **900 m past the board edge**, in rings of tiles coarsening outwards, trees thinning to haze. On Huge that is a ~2,400 m visual world around a 600 m played one, for **464 of 5,392 draw calls** |
| `PawnFigureDirector` | live animated figures capped at 64, **nearest kept**, the rest drawn as instanced stand-ins |
| `SliceSettings` | the drawn layer band, which already discards 72% of Huge's chunks before anything else looks at them |

So the outer-zone half of the question is built, shipping, and is not what costs.

### 8.4 Where the idea does pay, and it is worth taking

**The region graph allocates a region for solid rock.** Only **6.8%** of the wilderness's regions
are walkable; the rest are `RegionKind.Impassable`, kept so rooms and atmosphere have a substrate.
Short-circuiting all-impassable blocks would cut the live region count roughly **tenfold**, and
since the rebuild tracks regions almost exactly (§3.1), that is most of the one board-scaled
simulation cost gone — *and* it is what makes **depth** affordable, which is the axis a future
240 × 240 × 32 would need.

This is the owner's idea applied where it actually costs: the boring, uniform, nobody-goes-there
parts of the world stop being represented at all. **The price is M4's atmosphere losing its
substrate, so it is a design decision and not an optimisation**, and it is the deferred
chunk-uniform work `02-world-and-layers.md` §2 already names.

The second candidate is **coarser chunk meshes at distance**. Real, but larger, and worth measuring
only after culling — which may remove those chunks altogether and leave nothing to coarsen.

### 8.5 Rejected, with reasons

- **Streaming chunks in and out.** Solves memory, and memory is not the constraint — 61.3 MiB
  simulation plus ~17 of mirror at Huge (§2). Worse, a colony sim cannot unload simulation state:
  pawns walk, crops grow, incidents fire and stockpiles sit off-screen, and a colonist who stops
  existing because nobody is looking is a different game. If it ever *does* become necessary it is
  the **presentation mirror** that streams, keyed on the drawn band and the frustum — which makes
  culling step one of streaming anyway. Nothing is lost by doing culling first.
- **Load screens, or sub-areas within one map.** Breaks the one continuous colony the game is
  about, adds a modality the start-flow work is already trying to reduce, and **does not fix the
  frame**: you pay for what is visible, and what is visible does not change.
- **An explicit activity radius for rendering.** Strictly worse than the frustum, which is exact.
- **More variety in the outer band**, unless it obeys the façade rule. `CLAUDE.md`: a façade must
  never fill a cell the simulation could fill. Worldgen grew trees inside terrace banks on
  2026-09-18 for exactly this reason. Anything drawn where a colonist could stand needs a sim-side
  copy of the rule.

### 8.6 The recommendation

1. **Frustum and distance culling** in `ChunkRenderer.Render` — the camera is the activity radius,
   and it is exact.
2. **HT1** — correct everywhere beats a radius.
3. Re-measure. If the navigation rebuild is still the top term, **§8.4's uniform-block
   short-circuit is the "cheaper beyond"**, and it is the version of the idea that pays.

**The reason this keeps landing in the same place:** everywhere a main-area/beyond split would
help, the engine already has a *better* answer than a radius — one that is exact rather than
heuristic. The two places it does not are both already specced and both small.

### 8.7 Why the culling change is safe, which is not obvious

`batch.Bounds` is already passed as `RenderParams.worldBounds` at both submission sites
(`ChunkRenderer.cs:372, 403`), so **Unity is already culling against precisely this box on the
GPU**. A CPU-side test against the same box can therefore only skip submissions whose draws were
going to be rejected anyway: it changes what is *submitted*, never what is *seen*. The box is
padded (`ChunkMesher.BoundsPadding`, plus `ReliefReach()`) because modules overhang their cells and
the relief lifts and tilts them — and that padding is load-bearing for exactly this reason, so
**do not tighten it to make culling look better**.

The measurement and the change are the same code behind one flag, the way
`ChunkRenderer.InstanceCellPlates` already does it: `CullToFrustum` off counts what *would* be
skipped and skips nothing, on actually skips it. That is what lets before and after be taken inside
one run on a machine that cannot be trusted between runs.

## 9. What culling is worth, measured

Built 2026-09-21 on `claude/frustum-culling`, **off by default** and not yet merged. EditMode
2,285 / 2,262 / 0. The numbers below are from the same machine and the same caveats as §2 — read
the ratios, and read them against §2's uncut figures taken minutes earlier.

### 9.1 The figures that can be quoted

At the real shadow margin, which is `QualitySettings.shadowDistance` and measured **40 m** on this
project:

| Board | Chunks culled | Frame | Draw calls |
|---|---|---|---|
| Standard 120 × 120 × 16 | 33 of 104 (31.7%) | 3.02 → **2.63 ms** | 1,475 → 1,111 |
| **Huge 240 × 240 × 16** | **317 of 443 (71.6%)** | **8.43 → 4.57 ms** | **5,392 → 2,053** |

**Huge goes under the 5 ms budget**, and the shape is the one predicted in §8.3: Standard is
nearly all in view and saves little, Huge is mostly off-screen and saves a great deal. If the two
had saved alike, the test would have been measuring the machine rather than the board.

**The shadow margin costs most of the headline.** Without it 93.7% of Huge's chunks are outside the
frustum and the frame reads 3.35 ms — **and that figure is not shippable and must not be quoted as
the result**. It drops off-screen shadow casters, which is the regression §8.7 exists to prevent.
The honest saving is 71.6% and 4.57 ms.

### 9.2 Two instruments that were wrong, and how they were caught

Neither was found by a failing test. Both were found by reading output that looked plausible, which
is the habit this section exists to encourage.

**A reading that measured nothing.** The arm reported *"correct shadows cost 0.07 ms"*. They cost
nothing measurable there, because the third reading never happened: the composition root re-derives
`ShadowCasterMarginMetres` from `QualitySettings.shadowDistance` **every frame**, so a test that set
it on the renderer was overwritten before the first timed frame. **The tell was in the log** — both
readings reported the identical 2,053 draw calls, and a comparison in which the deterministic half
does not move is not a comparison. The arm now drives the *setting* instead, and asserts the margin
is non-zero so it can never again quietly price a cull that would not ship.

**A proof that proved nothing.** `CullingDoesNotChangeThePicture` failed on **its own control**: a
frustum admitting nothing moved 3.22% of pixels, which is not the difference between two pictures of
a world. The 2.58% beside it was therefore noise of the same size. **Had the control not been there
the test would have passed**, and the picture would have been reported as proven identical on the
strength of a blind comparison. That is the argument for the control in one sentence, and it is why
`docs/process.md` asks for the negative control every time.

The capture now writes `Logs/cull-{off,on,blind}.png` and logs chunks, instances and draw calls
taken *during* each shot, which separates "the cull is wrong" from "the camera never rendered the
board into the target". **Open, at the time of writing.**

### 9.3 What is not settled

- **Whether the picture is unchanged.** Until the control passes, the saving above is a number
  attached to an unproven claim. `CullToFrustum` stays off and the branch stays unmerged.
- **Play resolution and the target laptop**, as everywhere else in this document.
- **`FrameSection.Doors`**, still 0.000 because no arm edits the world while it times (§2).

## 10. The cull comes off hold (2026-09-23)

`CullToFrustum` shipped **off** for two days because `FrameTimeTests.CullingDoesNotChangeThePicture`
failed its own control, and nobody could say whether the cull was wrong or the test was blind. It
was the test, twice over, and both faults are worth more than the fix.

**One: the control never applied.** "A frustum admitting nothing" was imposed by assigning
`ChunkRenderer.Frustum` — a field `OdysseyBootstrap.LateUpdate` rewrites every frame — so the blind
shot was simply a second copy of the culled one. The tell was there in the diagnostics the previous
session had added and not read: **identical counts in both readings**, 126 chunks / 57,818 instances
/ 1,744 calls, where the blind one should have submitted nothing at all. `ChunkRenderer.FrustumOverride`
is the seam the root does not touch.

> **This is the second time in that one file that a test set a field the root re-derives per frame.**
> The first was `ShadowCasterMarginMetres`, off `QualitySettings.shadowDistance`, and the tell was
> the same both times. `docs/bug-patterns.md` P18.

It also refuted the obvious hypothesis, which is why the diagnostics were worth having: the shots
reported a **mean channel of 130.6**, so the capture was seeing the board perfectly well.

**Two: the scene was moving underneath the comparison.** With the control working, two shots of the
identical configuration still differed by **1.29%** of pixels against culling's 2.13% — a difference
that is supposed to be *nought*, asked to stand out against a floor most of its own size. Pausing the
simulation is not enough: it stops the ticks, so nobody walks, but the water still scrolls its
streaks, the figures still advance their animation graphs and the daylight rig still moves, because
those run on `Time.deltaTime` and the shaders on `_Time`. **`Time.timeScale = 0` is what stills the
shaders as well as the scripts.** And the floor is no longer assumed — the test takes a *repeat* of
the identical configuration and asserts on it, so the experiment has a control that must show a
difference and one that must not.

    the same shot twice           0.00%   (run alone)      0.04%   (in the full tier)
    culling                       0.00%                     0.02%
    a frustum admitting nothing  98.21%                    98.23%

**Both columns matter, and the second is why the test is calibrated rather than fixed.** Run on its
own the floor is nought; run inside the whole PlayMode tier on the same commit it was **0.77%** — a
busy run is still finishing shader variants, texture streaming and the post stack's first frames, and
eight frames between captures is not enough for that to be over. A 120-frame settle takes it to
0.04%. The acceptance is then `culled <= noise + 0.002`: the repeat shot is exactly what *doing
nothing* costs on this machine in this run, so that is what "culling is indistinguishable from doing
nothing" should be judged against. A constant is a guess at that number, and the guess was wrong in
both directions on the same day — 0.005 passed the isolated run and failed the tier.

> **This is not the loose tolerance `docs/lessons.md` warns about.** The bound is *measured in the
> same run*, not chosen; the positive control stays an absolute (>5%) and comes in at 98%, fifty
> times clear of even the noisy floor, so the comparison cannot go quietly blind.

### What it is worth, measured on `main` 2026-09-23

| Board | Chunks culled | Frame | Draw calls |
|---|---|---|---|
| Standard 120 x 120 x 16 | 33 of 104 (31.7%) | 2.53 -> **2.19 ms** | 1,360 -> 996 |
| Huge 240 x 240 x 16 | 317 of 443 (71.6%) | 6.63 -> **2.95 ms** | 5,083 -> 1,744 |

> **Re-taken after merging `main`, and the movement is worth reading.** The first measurement on
> this branch gave Standard 3.43 -> 2.86 and Huge 8.89 -> 3.84, so the *saving* has shrunk — Huge
> from 5.06 ms to 3.68. Nothing about the cull changed: **the chunk counts and the draw calls are
> identical to the digit**. What moved is everything else in the frame, because `main` now carries
> the crowd index and the aspect index (`25-pawn-steering.md` §9, `31-aspect-lookup.md`). A pass
> that removes a fixed amount of submission is worth proportionally less of a frame the faster the
> rest of it gets, and quoting the older, larger figure would be quoting a frame that no longer
> exists. **The deterministic half is the half to compare across runs**; the milliseconds are only
> comparable within one.

**The saving follows the player's shadow distance and is not a promise on every setting.**
`ShadowCasterMarginMetres` *is* the shadow distance, because a caster nearer than it may cast into
the frustum and so has to be submitted. At a 120 m setting Standard culls **nothing at all** (3.26 ms)
and Huge culls 74 of 443 (7.56 ms). That is the mechanism being correct rather than the measurement
being disappointing, but a number quoted without the shadow distance beside it is not a number.

`CullToFrustum` is therefore **on by default** from this date.

### 10.1 The cull is asked before the mesher (2026-09-24)

Found while planning the Meadow overhaul (`38-meadow-overhaul.md`) and fixed on merging `main` up to
this branch. The frustum test sat **after** `BatchFor`, so every stale chunk on the walk was meshed
first and rejected second. After a board-wide `Remesh` — which every graphics toggle does — the
eleven-chunk budget was spent in index order on chunks behind the camera, and the chunks on screen
waited behind them for as many frames as the board had off-screen chunks ahead of them.

The fix moves the test ahead of the mesher. It needs no new geometry: `ChunkMesher.BoundsOf` returns
exactly the box `Mesh` writes, which depends only on the chunk's footprint, so the cull answers the
same question as before and **the picture cannot move** — the proof in §10 still holds as written.
An off-screen chunk now simply stays stale until it is on screen, which is what deferral already
meant. `ChunksOutsideFrustum` still counts only chunks known to hold something.
`MeshBudgetTests.AnOffScreenChunkSpendsNoneOfTheBudget` is the guard: a whole-board re-mesh under a
frustum round one chunk defers nothing, and taking the frustum away meshes the rest.

## 11. Eight layers of hills, and how deep a board must be for them (2026-09-24)

The Meadow overhaul's owner decision is rolling hills of about eight layers — relief ±4 where the
game ships ±2 — with "make sure performance doesn't suffer" (`38-meadow-overhaul.md` §7). The ground
sits at `SizeY − 1 − headroomLayers − surfaceRelief`, so doubling the relief on a 16-layer board
takes two layers out of the mine. `BoardDepthTests.EightLayersOfHillsOnEveryBoard` (Long) prices
each option on every board through `ColonyWorld.DefFor` — the played wooded meadow, with a relief
seam (`surfaceRelief`, -1 for the def's own) — one test per board so its times compare with each
other. Generation is the median of seeds 1–5; everything else is seed 4242, as §2. .NET 8 CoreCLR on
the Windows dev machine; world memory is the simulation half only (add ~18 B/cell for the render
mirror, §2).

| Board | Config | Cells | Surface | Rock under lowest valley | Cliffs, 5 seeds | Generation | Regions / links | Edit tick | World MiB (B/cell) | Save KB |
|---|---|---|---|---|---|---|---|---|---|---|
| Small 80² | today ±2 @16 | 102,400 | 7..12 | 3 | 0 | 14 ms | 945 / 568 | 0.268 ms | 8.0 (81.9) | 58 |
| | ±4 @16 | 102,400 | 4..11 | **0** | 0 | 13 ms | 925 / 750 | 0.301 ms | 8.0 (81.9) | 66 |
| | ±4 @20 | 128,000 | 8..15 | 4 | 0 | 16 ms | 1,182 / 752 | 0.309 ms | 9.9 (80.9) | 70 |
| | ±4 @24 | 153,600 | 12..19 | 8 | 0 | 20 ms | 1,438 / 752 | 0.303 ms | 11.8 (80.3) | 70 |
| Standard 120² | today ±2 @16 | 230,400 | 7..12 | 3 | 0 | 41 ms | 2,110 / 1,477 | 0.627 ms | 17.8 (81.0) | 133 |
| | ±4 @16 | 230,400 | 4..12 | **0** | 0 | 39 ms | 2,008 / 1,931 | 0.553 ms | 17.7 (80.7) | 150 |
| | ±4 @20 | 288,000 | 8..16 | 4 | 0 | 47 ms | 2,590 / 1,946 | 0.569 ms | 21.9 (79.8) | 151 |
| | ±4 @24 | 345,600 | 12..20 | 8 | 0 | 53 ms | 3,166 / 1,946 | 0.510 ms | 26.2 (79.5) | 148 |
| Large 180² | today ±2 @24 | 777,600 | 15..20 | 11 | 0 | 102 ms | 7,360 / 3,511 | 0.836 ms | 58.4 (78.8) | 348 |
| | ±4 @16 | 518,400 | 4..12 | **0** | 0 | 77 ms | 4,486 / 4,301 | 0.875 ms | 39.9 (80.7) | 339 |
| | ±4 @20 | 648,000 | 7..16 | 3 | 0 | 88 ms | 5,790 / 4,327 | 0.975 ms | 49.5 (80.0) | 346 |
| | ±4 @24 | 777,600 | 11..20 | 7 | 0 | 84 ms | 7,086 / 4,327 | 1.108 ms | 58.7 (79.1) | 339 |
| Huge 240² | today ±2 @16 | 921,600 | 7..12 | 3 | 0 | 113 ms | 8,406 / 6,180 | 1.213 ms | 70.6 (80.4) | 546 |
| | ±4 @16 | 921,600 | 4..12 | **0** | 0 | 116 ms | 7,880 / 7,584 | 1.476 ms | 70.2 (79.9) | 620 |
| | ±4 @20 | 1,152,000 | 8..16 | 4 | 0 | 131 ms | 10,195 / 7,615 | 1.484 ms | 87.1 (79.3) | 632 |
| | ±4 @24 | 1,382,400 | 12..20 | 8 | 0 | 145 ms | 12,500 / 7,620 | 1.616 ms | 104.0 (78.9) | 606 |

**What it says.**

- **No cliffs.** Over five seeds on every board, ±4 with today's noise period (34) leaves no two
  neighbouring columns more than one layer apart. The ±2 tuning holds at ±4; M8 needs no slope
  spacing to keep the surface walkable, only to make it read as hills.
- **At 16 layers the mine is gone under the valleys.** The lowest valley has **no rock at all**
  between its subsoil and the bedrock, on every board, against three layers today. That rules out
  keeping 16.
- **At 20 the mine is back to today's depth and a layer more** (4 against 3), for **25% more cells**:
  Standard 17.8 → 21.9 MiB, Huge 70.6 → 87.1 MiB (~108 with the render mirror). Save +13–16%,
  generation +15%.
- **At 24 the mine is deeper than today's** (8), for **50% more cells**: Huge 104 MiB (~129 with the
  mirror). Large already ships at 24 and pays the same.
- **The edit tick moves with relief on Huge, not with depth**: 1.213 ms today, 1.476 at ±4 on 16
  layers, 1.484 at 20, 1.616 at 24 — relief adds terrace links (6,180 → ~7,600), depth adds only
  regions in rock. On Standard the three readings sit inside the run's noise (0.51–0.63 ms). A tick
  at rest is flat (~0.02 ms) on every depth.
- **Cavern and ore counts did not change with depth** (Standard 4 / 100 on every row). The arm counts
  deposits, not whether they sit where a colonist can reach them; that is not established.

**The frame** (`FrameTimeTests.TheBoardDepthAgainstTheFrame`, one run, RTX 5070 Ti on DX11, culling
on, played wooded meadow; another session's PlayMode run and the CI runner shared the machine, so
only differences inside the table are read):

| Board | Config | Batch frame (`World`) | 4K frame (`World`) | Calls at 4K | Chunks at 4K |
|---|---|---|---|---|---|
| Small | today ±2 @16 | 1.95 (0.531) | 10.36 (0.791) | 660 | 55 |
| | ±4 @16 / @20 / @24 | 2.47 / 2.43 / 2.26 (0.825 / 0.795 / 0.753) | 11.34 / 9.98 / 10.08 | 876 / 869 / 869 | 70 / 70 / 68 |
| Standard | today ±2 @16 | 2.30 (0.699) | 9.88 (0.882) | 877 | 76 |
| | ±4 @16 / @20 / @24 | 3.38 / 3.34 / 3.17 (1.443 / 1.435 / 1.348) | 15.77 / 12.08 / 12.06 | 1,520 / 1,489 / 1,478 | 126 / 123 / 118 |
| Large | today ±2 @24 | 2.46 (1.274) | 10.57 (1.358) | 1,092 | 108 |
| | ±4 @16 / @20 / @24 | 3.12 / 3.13 / 3.02 (1.828 / 1.826 / 1.774) | 13.68 / 14.46 / 15.98 | 1,678 / 1,662 / 1,652 | 143 / 139 / 136 |
| Huge | today ±2 @16 | 2.72 (1.504) | 10.59 (1.814) | 1,443 | 139 |
| | ±4 @16 / @20 / @24 | 3.45 / 3.49 / 3.41 (2.075 / 2.095 / 2.051) | 13.59 / 12.43 / 12.19 | 2,078 / 2,064 / 2,064 | 169 / 165 / 165 |

- **Depth costs the frame nothing.** From 16 to 20 to 24 layers the calls and chunks are identical
  or fall, and `World` moves inside the noise on every board. The camera draws the layers it can
  see, and extra rock under the valleys is not among them.
- **Relief costs the frame, and that is the finding.** ±2 → ±4 adds **~600 draw calls and 0.6–0.75
  ms of `World`** on Standard and Huge (Standard 877 → ~1,490 calls, `World` 0.70 → 1.44 ms at the
  batch view): taller hills put more layers of chunks in view. At 4K the frame moves 2–3 ms on
  Standard and Huge, a figure noisier than `World` because the frame is GPU-bound there. **"Performance
  doesn't suffer" is breached by the relief, not the depth**, and the lever is M9's ground skin, which
  replaces ~625 turf instances a surface chunk with one mesh — measure it against this table.

## 12. Thirty-two layers, and rock that costs nothing (2026-09-26, deep mining DM1–DM2)

Design 62 §2 asked for a deeper board on one condition: that the depth cost memory and generation
time and nothing else. Three depth-scaled costs were taken out first (DM1), then every offered
board went to 32 layers (DM2, `GridSize.OfferedLayers`).

**What changed.** Solid terrain belongs to no navigation region (`NavGrid.KindOf`; walls keep
theirs), so the rebuild's passes over every region stop counting rock. Underground, the drawn band
stops `SliceSettings.undergroundDepth` = 7 layers below the slice — where `ShadeBelow` floors at
0.08 and stops telling layers apart — instead of at bedrock. And `DoorDirector` rescans only the
chunks whose `ChunkVersion` moved (`Odyssey.Hud.ChunkedCellList`), not 1.8 million cells on every
edit. The nav graph was never in the world hash (`NavGraph.ContributeTo` has no caller), so no
golden moved for DM1 and `GoldenColonyProbe` is identical before and after.

**The machine.** A cloud container, Linux 6.18, 4 logical CPUs, .NET 8.0.31, Debug build through
`scripts/test-fast.sh`, **with other agents' test runs on the same CPUs** (load average 6–9 during
the run). The absolutes are two to three times the Windows dev machine's §2 and §11 figures and
are not comparable with them; **read each row against its control beside it**. The arm is
`DeepBoardProbe` (`[Explicit]`): the played map through `PlayedMap.Def`, seed 4242; the edit tick
is `NavGraphStatisticsTests`' (mean of 200 rebuilds, one random solid cell cleared before each).
The control is `NavGrid.SolidTerrainHasRegions`, which restores the old rule on one graph, run
before/after/before/after on identical grids; the repeat is the noise floor.

### 12.1 DM1: before and after, one run (2026-09-26 17:41Z)

| Board | Regions before → after | Links | Edit tick before (r1 / r2) | Edit tick after (r1 / r2) | Nav graph MiB before → after | World MiB (B/cell) |
|---|---|---|---|---|---|---|
| Standard 120² @16 | 2,110 → 392 | 1,496 | 2.23 † / 1.15 | 1.65 † / 0.77 | 2.12 → 1.94 | 18.7 (85.1) |
| Standard @24 | 3,262 → 392 | 1,497 | 1.02 / 1.16 | 0.64 / 0.63 | 3.08 → 2.90 | 27.3 (82.8) |
| **Standard @32** | 4,414 → 392 | 1,497 | 1.25 / 1.07 | **0.64 / 0.64** | 4.09 → 3.71 | 35.8 (81.4) |
| Huge 240² @16 | 8,406 → 1,501 | 6,257 | 2.73 / 2.95 | 2.02 / 2.15 | 8.48 → 7.77 | 73.7 (83.9) |
| Huge @24 | 13,018 → 1,506 | 6,269 | 3.18 / 2.96 | 2.23 / 2.04 | 12.39 → 11.66 | 108.4 (82.3) |
| **Huge @32** | 17,626 → 1,506 | 6,269 | 3.24 / 3.33 | **2.04 / 2.01** | 16.42 → 14.89 | 142.5 (81.1) |
| Scale target 250² @40 | 24,141 → 1,649 | 6,865 | 4.23 / 4.19 | 2.51 / 2.32 | 21.21 → 19.66 | 191.6 (80.4) |

† The first rows of the run carry the JIT's warm-up; r2 is the figure to read.

- **The gate holds.** The edit tick at 32 layers after is under today's at 16 on both boards:
  Standard 0.64 against 1.15, Huge 2.01–2.04 against 2.73–2.95.
- **Depth has left the edit tick.** After the change, regions do not move with depth at all (392 on
  Standard at 16, 24 and 32; 1,501 → 1,506 on Huge) and neither does the tick (Huge 2.02–2.15 at
  16, 2.01–2.04 at 32). Before it, regions grew 2.1× from 16 to 32 and the tick with them.
- **Regions fell 5.4–14.6×; the tick fell 30–45 %.** What is left of an edit is the links (flat in
  depth, about 1,500 and 6,300), the block flood and the portal and district passes over them — the
  cost of what is open, not of what is under it. The scale target's 1.15 ms of the 2026-09-19 audit
  is 4.2 → 2.3–2.5 ms here, on this machine.
- **Memory is the cells.** Removing rock regions saves 0.2–1.5 MiB of region arrays; the world is
  81–85 bytes a cell at every depth, so 32 layers is simply twice 16.
- **Not measured here, owed to a Unity session:** the frame with the camera ten layers down, before
  and after the seven-layer band, and `FrameSection.Doors` under an arm that keeps editing (§2's gap
  is still open; the rescan is now per chunk and `ChunkedCellListTests` and
  `DoorPresentationTests.ADoorRaisedRescansItsChunkNotTheBoard` hold its shape, not its cost).

### 12.2 DM2: every board at 32 layers, one run (2026-09-26 17:42Z)

| Board | Cells | Generation, median of 5 seeds | Regions / links | Edit tick | World MiB (B/cell) | Save KB |
|---|---|---|---|---|---|---|
| Small 80² @16 | 102,400 | 26 ms | 156 / 572 | 0.83 † | 8.4 (86.0) | 64 |
| **Small @32** | 204,800 | 42 ms | 156 / 572 | 0.29 | 16.0 (82.1) | 67 |
| Standard 120² @16 | 230,400 | 60 ms | 392 / 1,496 | 0.68 | 18.6 (84.7) | 147 |
| **Standard @32** | 460,800 | 99 ms | 392 / 1,497 | 0.68 | 35.8 (81.4) | 154 |
| Large 180² @24 | 777,600 | 174 ms | 884 / 3,565 | 1.24 | 60.9 (82.2) | 393 |
| **Large @32** | 1,036,800 | 216 ms | 884 / 3,565 | 1.27 | 80.1 (81.0) | 368 |
| Huge 240² @16 | 921,600 | 261 ms | 1,501 / 6,257 | 2.06 | 73.7 (83.9) | 601 |
| **Huge @32** | 1,843,200 | 422 ms | 1,506 / 6,269 | 2.02 | 142.5 (81.1) | 641 |

† Warm-up, as above.

- **32 layers costs memory and generation time and nothing else**, as design 62 §2c required: the
  edit tick is the same at 32 as at the old depth on every board, regions and links are unmoved,
  and a save grows by 0–7 % (the palette chunks compress uniform rock; Large's is smaller at 32
  than at 24 on this seed).
- **Memory doubles with the cells**: Standard 35.8 MiB, Large 80.1, Huge 142.5 of simulation, plus
  roughly 18–20 B/cell of render mirror (§2) — about 176 MiB on Huge in all. Design 62 §2d's
  estimate (~36 / ~80 / ~140) holds. Huge at 32 is the owner's call on the laptop (design 62 §11 Q2).
- **Generation is 1.6–1.9× slower**, 99 ms on Standard and 422 ms on Huge here, inside the loading
  screen.
- **The played-board golden moved to 32 layers** and `GoldenColonyProbe` says it is the same colony
  sixteen layers higher: every cell sum moved by exactly its count × 230,400 and every other number
  is identical (`Golden.cs`).

**One depth-scaled term found and not changed:** `NavGraph.RecomputeLayerChangeEstimate` divides
the board's portal count by `SizeY − 1`, so a deeper board (portals unchanged, more layer gaps of
rock) raises the path search's layer-change hint. Measured by the same arm (a repeat of §12.2 at
17:43Z, which also read Small 0.41 → 0.44 ms, Standard 0.64 → 0.60, Large 1.23 → 1.26 and Huge
2.01 → 2.08 for the edit tick, confirming the table): **Small 3,000 → 4,600, Standard 2,500 →
3,600, Large 3,000 → 3,500, Huge 2,500 → 3,600.** It moves search effort and possibly route choice,
not correctness — and on the played board it changed nothing the probe can see: the colony at 32
did everything the colony at 16 did (why is not established). Fixing it (count only the gaps that hold
a portal) would move the goldens; it wants its own unit and a measurement.
