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

| Board | Live regions | Links | Generation, median of 5 seeds | Rebuild per mined cell | World memory | Save |
|---|---|---|---|---|---|---|
| Standard | 2,110 | 1,477 | 23 ms | **0.298 ms** | 15.5 MiB (70.4 B/cell) | 143 KB |
| Large | 7,360 | 3,511 | 69 ms | **0.587 ms** | 51.1 MiB (68.9 B/cell) | 359 KB |
| **Huge** | **8,406** | **6,180** | **90 ms** | **0.883 ms** | **61.3 MiB (69.8 B/cell)** | **581 KB** |
| Scale target | 24,141 | 6,772 | 189 ms | **1.047 ms** | 161.8 MiB (67.9 B/cell) | — |

Arms: `NaturalWorldgenTests.EveryOfferedBoardGenerates`,
`NavGraphStatisticsTests.EveryOfferedBoard` (which carries `TimePerEdit`), `BoardMemoryTests`,
`GridSaveTests.EveryOfferedBoardRoundTripsAndFitsTheBudget`. All print; only the pre-existing
budgets assert.

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

**Owed.** `FrameTimeTests.TheBoardSizeAgainstTheFrame` exists and times all three boards inside one
run, but a Unity editor was open on this machine throughout, and `CLAUDE.md` is explicit that a
frame number taken beside a sibling Unity is worthless — the city canary drifted 2.01 → 4.01 ms on
nothing but that. The arm is written so its answer is a *ratio* taken seconds apart, which survives
a noisy machine; the absolutes will not.

The two terms to read when it runs are `FrameSection.World` (chunk buckets and the surround — the
term that scales with the board) and `FrameSection.Doors` (`DoorDirector`, §3.2). Everything else
should be flat across all three boards.

**The prior, stated before the measurement so the result reads as confirmation or surprise rather
than as whatever it happens to be:** `World` is flat at 1.9–2.5 ms across a 48-fold colony at
Standard (`06-rendering-and-camera.md` §6c.2). Huge is 4× a ~2 ms term, not 4× a 5 ms frame.

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
automatically — Huge draws 5,264 trees, 403 ore deposits and 17 caverns against Standard's 1,222,
100 and 4. **Three things are per-map absolutes and therefore get relatively scarcer as the board
grows:**

- `streamCount = 2` and `riverFords = 2`. Measured: Standard puts down 4 water bodies on 300 m, Huge
  5 on 600 m. A Huge board is drier per acre, and whether that reads as dry is an owner call.
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

**With HT1 and view-scaled rendering, the design target of 250 × 250 × 40 is reachable and the board
stops being the limiting number.** Memory puts a hard stop somewhere near 400 × 400 × 40 (6.4M
cells, ~430 MiB simulation plus ~115 MiB mirror) on an 8 GB laptop. **Without either,
240 × 240 × 16 is comfortable**, which is what the measurements above say.

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
