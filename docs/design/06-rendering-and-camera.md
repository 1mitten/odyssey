# 06 — Rendering, camera and overlays

Scope: how the world is drawn, how the camera slices layers, how overlays are rendered, and how Synty modules take a runtime material tint. **Not** in scope: the HUD, panels, input bindings and icons, which belong to `09-ui-and-input.md` and `10-ui-panel-catalogue.md`, owned by the separate UI design line. The seam between the two is the `SliceDirector` state and the overlay toggles: this document says what the renderer does with them, that one says how the player sets them.

## 1. The target look

The bar is the owner's two concept renders (`docs/reference/screenshots/README.md`): Synty low-poly under URP, **cyan emissive trim** doing the night readability, interiors shown **roofless or cut open**, a three-quarter mid-height camera, floating panels that never hide the world.

Two of those are already confirmed available rather than hoped for:

- The emissive channel exists on the pack shaders — `Synty/Generic_Basic` exposes `_Emission_Map`, `_Emission_Color` and `_Enable_Emission` (`e-04-tint-strategy.md`), so night readability is a material setting, not a lighting-system project.
- The modules assemble on our grid with no per-piece fixups, proven by the generated look-check scene `Assets/Scenes/Spikes/VisualBlock.unity` (`d-03-rendering.md`).

The concept renders show the *outskirts* map, which is a later map type. The art direction carries over; the prototype map stays the ruined city.

**Cel shading was raised and rejected on 2026-09-15.** Worth recording so it is not reopened: the concept renders are *not* cel-shaded. They are flat-lit low-poly with a colour atlas and emissive trim, which is the native Synty look and what the imported packs already give us. True cel shading means banded lighting and usually hard outlines, which would be a deliberate step *past* the renders rather than a way of matching them. It would also cost an own-authored shader, an outline pass inside an already-tight render budget, and a fix for banded lighting fighting the depth-darkening cue on layers below the slice. **The target is the screenshots as they are.** Owner decision: stick to the plan and the screenshots.

## 2. Drawing the world

**Instanced, per chunk, per layer.** The unit of drawing matches the unit of dirty tracking from `02-world-and-layers.md` §3: a 25 × 25-cell chunk within one layer. For each (chunk, mesh, material) bucket the renderer issues one `RenderMeshInstanced` call with the transforms of every module instance in it. Nothing in the world is a `GameObject` per cell; at 2.5 million cells that is not a close call.

Instance data per chunk is rebuilt only when the chunk is dirty, and the sim signals that through the snapshot the UI seam already provides — the renderer is a reader, never a poller of simulation objects.

**What gets drawn each frame** is decided by the slice (§3), so the renderer never submits the whole map. A five-layer slice of a 60 × 60 slice-sized map is a few hundred buckets; the full 250 × 250 map at a five-layer depth is a few thousand, which is what the D3 performance pass exists to measure.

**Still to be measured** (`d-03-rendering.md`, performance half): draw calls and frame time at full scale, with emissive materials, real-time shadows and the cut-away all enabled, plus whether the GPU Resident Drawer beats explicit `RenderMeshInstanced` for this shape. Matching the look is part of the budget, not something to switch off to hit a number.

## 2a. The land beyond the board

The board is finite and the simulation has no cells past it. Until 2026-09-16 that was exactly what
it looked like: the meadow stopped at the rim in mid-air over the sky gradient, and the whole thing
read as a board game on a table rather than as a clearing in a landscape. **The surround is
decoration that fixes the framing and nothing else** (`TerrainSkirt`, `SkirtLayout`).

What it is not: it is not a second world. Nothing out there is a cell, so nothing is pathable,
selectable, buildable, fellable or in the save. `SlicePicker` cannot return a cell that does not
exist, which is why the surround needs no defence against being clicked.

Four decisions, all taken with the owner on 2026-09-16:

- **The meadow continues, thinning into haze.** ~~Not a framing ring of hills~~ and not a bare
  plane: the same ground and the same wood carried outwards until linear fog has closed over it.
  **Amended 2026-09-16 by the owner, and the strike-through is deliberate** — the surround *does*
  carry hills now, and the sentence is left visible so that nobody re-derives the old position from
  a half-remembered reading of this section. What is unchanged is the reason it was written: the
  surround must not read as a *ring*, a bowl drawn round the board to frame it. The hills come from
  the same field the board rolls on, at zero amplitude at the rim, so they are the land continuing
  rather than a wall built around the garden. See §2b.
- **A fixed skirt, built once, reaching 1,220 m past the rim.** Concentric rings of instanced
  ground tiles, one cell per tile at the seam and coarsening outwards, so 1.2 km of ground costs
  about 7,900 instances rather than about 200,000. Each ring is covered by four strips, each split
  into a whole number of equal tiles that fills it exactly — so the rings cannot gap (sky showing
  through) or overlap (coplanar ground z-fighting), and the board's dimensions need not divide
  anything. Fog is opaque by 1,100 m, so the skirt ends where nothing can see it end. The camera's
  far plane went from 600 m to 1,800 m to contain it.
- **The playable area still reads as bounded**, but softly: the surround desaturates towards its own
  luminance, ramping from nothing at the rim to full strength 18 m out. A step change at the join
  is the exact tell the feature exists to remove, so the change is a ramp and never a line.
- **Below ground level the surround is not drawn.** A sheet of landscape sitting over an open mine
  would bury the thing the player went down to look at. The ground is drawn from the surface layer
  upwards and the trees a layer higher again, which is the ordinary slice rule applied to a thing
  that has no layers of its own.

**The grass goes with it.** Tufts that stopped at the boundary drew a straight line three hundred
metres long between a field of grass and bare ground — a better advertisement for where the board
ends than the mid-air edge ever was. The first ring is strewn by the same hash of the same
coordinates the mesher uses inside the board, at the same density, fading to nothing 20 m out. A
clone without the licensed packs gets bare ground out there, exactly as it does on the board.

**There is a second wood, on the hills (owner request, 2026-09-16).** The near wood stops 90 m past
the rim, and by the amendment below the hills only begin to rise there — the ramp gives them about
six of their fifty metres at that distance. So every hill in the background was bare ground, and a
bare hillside is unreadable: there is nothing on it of known size, so the eye cannot tell how far
away it is or how big it is, and it flattens into a green backdrop. Trees are the cheapest scale
reference there is, and they are the whole of what makes the distance read as distance.

The far wood runs from the edge of the near wood out to **900 m**, which is chosen against two
numbers that already exist rather than by eye: the hills reach full height by 700 m, so everything
inside that is the landform the trees describe, and fog closes at 1,100 m, so a tree past that is
drawn into an opaque wall. It is scattered on a **15 m lattice with a jitter**, not the 2.5 m cell
grid — the band is some four million square metres, and walking it at cell resolution would be
670,000 samples to place a couple of thousand trees. The jitter is what stops a lattice reading as
an orchard, and a test holds it.

**Its cost is batches, not triangles, and that had to be measured.** A batch key is (sector,
variant, part). At the near wood's 80 m sectors and all sixteen tree kinds, the meadow drew **1,154
surround batches against 72** before the hill wood existed. Widening the sector to 800 m and capping
the far wood to **four kinds** attacks both factors: at 300 m a tree is a few dozen pixels and
nobody can tell one conifer from another, so the variety was buying nothing and costing a multiple.
Coarse sectors cull worse, and out here that is the right trade — there are only a few thousand far
trees, so submitting them all costs less than the draw calls fine culling would take.

Measured under the real player loop, meadow, RTX 5070 Ti at 640 × 480, with the city as a control
because it has no surround trees at all:

| | hill wood off | hill wood on |
|---|---|---|
| mean | 1.45 ms | 1.38 ms |
| worst | 2.13 ms | 1.81 ms |
| draw calls | 1,270 | 1,322 |
| instances | 39,929 | 42,506 |
| surround batches | 734 | 786 |

So 2,577 trees for 52 draw calls, against a 5 ms budget. The mean moved by less than the run-to-run
noise: the city, which gains nothing from this change, moved 1.70 → 1.84 ms between the same two
runs, so ±0.14 ms is the floor of what can be claimed and the hill wood's −0.07 ms is inside it.
**The honest statement is that it has no measurable cost, not that it made anything faster.**
`OdysseyBootstrap.skirtHillTrees` switches it off for the comparison, and `skirtTreeDensity` scales
it along with the near wood.

**The surround measures the board rather than being configured.** The surface level, the terrain and
its tint, which trees grow and how thickly are all read off the generated map, so a bare board gets
bare ground, the wooded meadow gets woodland at its own density, and the ruined city gets whatever
the ruined city has. Nothing here needs changing when a new map type is added, and the surround can
never disagree with the board about what the board is.

Cost, and where the lever is. The ground is cheap and fixed; the trees are the only part with a real
vertex cost — about 3,500 of them on the 120-cell wooded board, roughly what the board itself
carries. `OdysseyBootstrap.skirtTreeDensity` scales that as a percentage for a machine that cannot
afford it, and `terrainSkirt` turns the whole thing off. Batches are split by strip (ground) and by
an 80 m sector grid (trees) so that the half of the surround behind the camera is culled rather than
drawn, and only trees within 10 m of the rim are in the shadow pass — the sun is 72 degrees
overhead, so a tree casts a couple of metres and a generous range would buy a thousand extra casters
and no visible shadow.


## 2b. The ground has a shape, and none of it is a cell

Added 2026-09-16. The board read as a carpet of blocks, for two reasons that compound: the wooded
board sets the generator's own `surfaceRelief` to zero, and a ground cell is drawn as one instanced
2.5 x 3.0 x 2.5 cube placed by a bare translate, so every top face is a flat quad at exactly the
layer height.

**The relief is a facade, in the same sense the grass tufts and the axe chips are.** It is drawn and
nothing else: no cell knows about it, it is not pathable, not in the save, not in the state hash,
and `surfaceRelief` is still zero. ADR 0002's "no slopes, no half-heights" is a rule about cells, and
this adds nothing to a cell. `GroundRelief` holds the whole of it.

**The governing rule: relief is a drawing offset, never a position.** `CellMetrics.FloorCentre` is
untouched, because it is the one place metres exist and every system agrees through it. The lift is
applied explicitly at each draw site instead. The single exception is picking, and it is not really
an exception: a click has to land on what the player can see, so `SlicePicker` tests each cell's own
tilted floor rather than one flat plane per layer. Without that a click lands most of a cell away at
a shallow pitch, which is exactly the misclicking complaint the picker's own remarks cite Going
Medieval for.

**Ground is sheared; everything standing on it is lifted.** A per-cell vertical offset alone would
give plateaus with little steps, which is still blocky. Each ground cell instead takes the tangent
plane of the field at its centre, which is affine and so fits in the instance matrix the cell
already had: no extra instance, no extra draw call, no new mesh, no shader change. Vertical edges
stay vertical so neighbours cannot gap, the determinant is one so nothing flips to a backface, and
the top normal tilts under the ordinary inverse-transpose so the lighting is free. A colonist, a
tuft, a log pile and a cursor are lifted and never sheared — ground lies along a slope, but a person
standing on a hillside stands up.

**Two layers, one field.** The board rolls 2 m over a 150 m wavelength; the surround adds hills of
50 m over 1,000 m, ramped from zero at the rim to full height by 700 m out and held. Amplitude and
wavelength together decide a slope, which is why the hills cannot simply be the board's field turned
up: 50 m over 150 m stands at sixty degrees. Both layers are continuous, so the join at the rim is
continuous by construction rather than by tuning.

**Sized to be seen, and only where it can be.** Two measurements drove the numbers more than taste
did. The lit value barely moves for a gentle slope — the sun is at 72 degrees over a strong trilight
ambient, so a 1.7-degree roll changes it by under one per cent, and a cautious 0.35 m amplitude
would have shipped the whole system with nothing visible. And the far land is mostly not on screen:
fog is opaque at 1,100 m from the camera, and at the default 48-degree pitch only ground between
roughly 50 m and 220 m is in frame at all, with the horizon appearing only below about 25 degrees.
So the board's own roll is what improves the default view, and the hills are what reward tilting
down.

| Pitch | Camera height | Ground visible |
|---|---|---|
| 48 (default) | 119 m | 50–224 m |
| 35 | 92 m | 64–342 m |
| 25 | 68 m | 68–773 m |
| 20 (minimum) | 55 m | 65 m to the horizon |

**What the tile size costs.** Each piece of ground is one tilted plane, so two neighbours part
company across their shared edge as the square of the tile width. On the board, at 2.5 m, that is
about 41 mm at worst and is filled by the lower neighbour's own side face. On the surround it was
the deciding constraint: at the old 120 m tiles it came to twenty metres and read as long diagonal
cracks scored across the hillsides, so the outer bands went to 20 m and 60 m. Surround tiles are
also sunk deep enough to reach below whatever their neighbours can do, or the join between two of
them is an open trench to the sky.

**Levers.** `OdysseyBootstrap.groundRelief` and `groundReliefPeriod`; 0 is the old flat board
exactly. `GroundRelief.HillAmplitude`, `HillPeriod` and `HillRampMetres` for the surround. Judge it
with *Odyssey → Presentation → Check the ground relief*
(`scripts/unity.sh shot Odyssey.EditorTools.ReliefCheck.Run`), which photographs the board with the
relief off and on at all three pitches above — the whole feature is a judgement about how something
looks, and reasoning about a renderer from its source is guesswork.

**Cost, measured under the real player loop** (`FrameTimeTests`, never `RenderBench`): the wooded
meadow went from 0.41 ms to 0.68 ms mean, 1.10 ms worst, at 640 x 480 on the RTX 5070 Ti against a
5 ms submission budget. The shear itself is free — identical draw calls and instances with the
relief off and on. The 0.27 ms is the finer surround tiles: 30,665 instances against 23,156, at the
same number of draw calls.

**The real terracing arrived, and this section did not notice.** The paragraph that stood here said
`surfaceRelief` was one line away in `MakeWooded` and deliberately not taken. That line was taken by
the mining work, which made `MakeWooded` a *cover* mode rather than `MakeBarren` with the trees put
back — so it stopped zeroing anything, and the played board has run at the def's own `surfaceRelief`
of 2 ever since. `WoodedMapTests.TheDrySurfaceIsTerracedAndCoveredInGrass` requires it. The board
therefore carries real 3 m risers **and** a 2 m drawn roll over the top of them, and the smoothing
this section promised for that day was never written. §2c is that work.

## 2c. Earth has a surface, and a step has a way up

Added 2026-09-16, after the owner asked whether the terrain could carry "a facade of geometry with
slopes and uneven grass", whether the characters could answer it, and whether the height difference
— "one big block and then a completely straight wall" — could be "spanned out to half or quarter
blocks".

**Where the half and quarter steps go.** Three places were possible and only one is affordable.

| Where | What it costs | Verdict |
|---|---|---|
| The cell model — quarter-height cells | ADR 0002 is "effectively irreversible": the state hash, every save, the pathfinder, the support solver, mining and every golden | No |
| A drawn height field derived from the terrace map | Drawn ground detaches from layer height everywhere, dragging picking, item drops, built walls and the strata behind it; a cell straddling a step shears fifty degrees | Not taken |
| **The mesh** | A variant costs one instancing *bucket*, not one instance per cell, and four bearings are free because a square footprint is unchanged by a quarter turn | **Taken** |

So the detail the cell model cannot carry is carried by geometry, and §2b's governing rule is
untouched: **relief is a drawing offset, never a position.** `CellMetrics.FloorCentre` is not
touched, no cell knows about any of this, and nothing enters the save or the state hash.

**`GroundMesh` is `RockMesh` for earth, with one rule stone does not have.** The middle of a ground
cell is where everything stands — a colonist, a stack of wood, a tree and the cursor are all drawn
at `FloorCentre`. Rock may chip its whole top face down, because whatever stands on a rock stands on
the cell *above* it; a dished meadow would hover every colonist on it. So the centre vertex is
pinned exactly and only the rim moves. The rim may also rise, which stone forbids: a rim that can
only drop gives every cell the same shallow bowl, and a field of identical bowls is a waffle — a
more obviously artificial pattern than the flat quads it replaced. Two neighbours disagreeing across
a shared edge leave a few centimetres of side face showing, never a hole, because a cell carries all
four walls whether or not anything can see them.

**Two meshes, and it is a performance decision.** Ground is the largest instance population in the
world. On flat ground a surface cell's four same-layer neighbours are solid too, so only its top is
ever seen; sides appear at terrace risers, at mined faces and at outcrops, which are a small
fraction of what is drawn. *Turf* is the cheap common case at 32 triangles against a cube's 12;
*Face*, at 80, has its walls in courses that step out — never in, because an inward step recedes
past geometry the mesher may have culled for being buried, which is a hole through the world to the
sky. `GroundMeshTests` asserts that ordering rather than trusting it: if the cheap case ever stops
being cheap, the cost lands on every cell of the board at once.

**`BankMesh` is the answer to the straight wall.** The simulation already says a colonist walks up a
one-block step — `MoveCost.JumpUp`, a hop into the column next door — so the board was showing a
wall where the game had a path. A bank is one smooth slope, the full width of its cell, drawn in the
*empty cell* beside the step.

**It was a stepped staircase first, and that was wrong.** The first version cut the slope into three
treads and jittered the tread positions per cell so a long run would not repeat. The owner named
every part of the fault: the jitter meant neighbouring cells put their treads in different places,
so a run came out as a ridge of misaligned bars rather than one flight; the taper that stopped the
run ends being bare walls turned them into wedges; and the whole thing read as built furniture
dropped onto the ground rather than as ground. **Simple and continuous beats varied and broken** —
the same lesson the rim ripple taught, arriving again by a different road.

**Three shapes, and they tile.** A bank's surface is a height field over its own cell, and the three
cases are the three simplest functions there are, with local `+z` and `+x` pointing at the steps:

| Shape | Surface | Where |
|---|---|---|
| Straight | `y = z` | one step against one side |
| Inner | `y = max(x, z)` | a notch, with steps on two adjacent sides |
| Outer | `y = min(x, z)` | a hip, with a step only on the diagonal |

**They agree exactly where they meet**, which is the whole reason for choosing them. Along the edge
it shares with a straight neighbour, `max(x, z)` is `z` and so is `min(x, z)` — the same value the
straight piece has there. So a run of banks around a terrace, corners and all, is one continuous
surface: **the width is matched by construction rather than by hand.** The `Outer` piece is what
fixes corners rather than merely surviving them — a cell diagonally outside a convex corner touches
no step orthogonally, so it used to get nothing and every corner had a square bite out of it.

Nothing is varied and nothing is jittered: one mesh per shape, four bearings from the instance
matrix, no per-cell choice at all. Three modules cover every bank on the board, which is fewer than
the stepped version needed for one.

The bank belongs to the empty cell rather than to the block it climbs, and that is what makes it
cheap to decide: it stands at the same layer as the riser, on the top of the lower terrace, reaching
the top of the riser. Everything the decision needs is on one layer plus the cell directly below, so
a single-layer chunk pass sees all of it.

Five conditions, each with a test, each ruling out something that would look wrong:

- **Empty, and standing on ground**, or the bank hangs in the air.
- **The step is earth.** A mined face and a quarry wall stay sheer; a grassy ramp growing out of cut
  rock is a lie about what was done to it.
- **The top of the step is open**, or this is the wall of a tunnel rather than a terrace.
- **The cell is open to the sky**, which keeps banks on the outdoor hillside and the inside of a
  working sharp-edged.
- **Neither the cell's floor nor the step beside it is a face the colony cut.** See below: this one
  arrived as a reported bug rather than as a condition thought of in advance.

**Nothing grows inside a working (owner report, 2026-09-16: "you can't see the colonists").** The
"step is earth" rule reads as though it had already covered this, and it does not: grass, bare earth
and subsoil are all mineable — 60, 60 and 160 ticks to clear — so a quarry sunk into the meadow is a
hole whose walls are earth with open tops, which is every condition a terrace step has. A bank
therefore grew **in the cell that had just been cut**, filling it from its floor to the rim, and the
miner standing in it to cut the next face was drawn up to the chest in ground. Measured on the played
board: a 3 × 3 pit one layer deep grew 8 banks and swallowed one of the five colonists whole.

The mark presentation reads is `CellFlags.Discovered`, taken for its *other* meaning. The two are
coextensive rather than merely similar — the flag is set by `CellGrid.RevealAround`, which is called
from exactly one place, `MineJobDriver.MineCell`, and worldgen sets it on nothing at all — so a solid
cell carries it if and only if a colonist has taken the cell next to it out of the world. The
coupling is stated in `WorldRenderModel.IsCutFace`, which is the one place that would change if a
deep scanner ever revealed rock nobody had touched.

**It has to be asked of the floor and not only of the sides**, and the shortfall is invisible in the
obvious case. Mining a cell reveals all six of its solid neighbours, so a cut cell's four sides are
all cut faces — `StepsAround` comes back empty, and the hip branch then went looking at the
*diagonal* neighbours, which nothing reveals, because a colonist who cuts past the corner of a seam
has not seen into it. The hole filled with a hip piece instead of a corner one: a single cut cell
still drew 1 bank and a four-cell bench still drew 2. A mined cell's floor is revealed too, being one
of the six, so asking the floor catches every shape of working at once.

**A figure stands on a bank, not in one.** A bank fills its cell from the floor to the rim and a
pawn is drawn at the middle of its cell, so the fault above has a second home on the outdoor
hillside — where the ramp is the picture of a hop and must stay, so the answer is the opposite one.
Measured on the played board before the fix: **three of five colonists** standing at the foot of a
terrace, every one of them 1.500 m through the slope; afterwards, 0.000 m.

The decision therefore left the mesher. `BankLayout` holds all of it — whether a bank stands in a
cell, which of the three shapes it is, what it is made of, and `RiseAt`, how far its surface stands
above its own floor at a point. `ChunkMesher` turns that into an instance and `PawnPose` stands a
figure on it, so the two cannot drift; two copies of this arithmetic would look identical until the
day they disagreed, and the symptom would be blamed on the animation. The levers went static with
it (`BankLayout.Enabled`, `InWorkings`, `LiftFigures`), because a per-renderer lever would let banks
be off while colonists still hovered 1.5 m over the meadow.

**The tiling is what makes it safe.** `BankMesh`'s three shapes were built to agree where they
meet — a straight piece's open edge sits at its floor, a hip's does too, and neighbouring pieces
match along the shared edge — and those same tests are what make the surface continuous for a
walker. Nothing extra was needed for a run of bank, for leaving one onto flat ground, or for walking
onto one.

**Going up, take the higher of chord and ground; going down, fade the lift out.** The asymmetry is
two different faults, not untidiness. Climbing, the chord between two cell centres runs *below* the
ground for the second half of the step — a hop passes a metre and a half inside the block being
climbed, which it did before banks existed — so the figure is pushed up onto the surface, and the
surface is continuous so the maximum is too. Descending, the drawn ground is a step function: taking
the maximum would hold the figure flat to the edge and then drop it 1.5 m in one frame. The rise is
faded out over the step instead, **at both ends** — the first version faded out only the cell being
left and forgot the one being entered, which is fine dropping off a bank onto flat ground and a
metre and a half of teleport dropping off a step *into* one, and a terrace has banks at the bottom
of it by definition. `BankFootingTests` samples each case 400 times across the step and asserts no
frame-to-frame jump over 5 cm.

**Open: whether the feet need the bank's own gradient.** `Footing` leans the root toward the ground
normal and plants both boots with `TwoBoneIk`, off `GroundRelief.SlopeAt` — the rolling field, which
is eight degrees where a bank is fifty. A stance 0.3 m across therefore spans about 0.36 m of slope
the feet know nothing about. The sheet could not settle it: the colonist the harness picked wears a
full-length skirt, so her legs are not in the picture at all. It is a fifth of a metre at the boots,
usually occluded by the slope itself, and §2b's standing rule — ground lies along the slope, people
stand up on it — predicts the lift alone is enough. Judge it from `bank-on-boots.png` on a run that
picks a colonist in trousers.

Two of those carry the argument. *Flat ground grows none* is the cost claim — the meadow is nearly
all flat, and a bank on flat ground would put an instance on every cell of the board. *A two-layer
face grows none* is the honesty claim: the simulation refuses it
(`VerticalMovementTests.ATwoBlockFaceIsAWall`), and drawing a bank up one would promise a route that
does not exist, which is worse than drawing a cliff.

**Water is included as the low side deliberately.** A channel is cut one layer down (ADR 0009), so
every stream and pond bank is one of these steps and had been a 3 m vertical ditch wall.

**The landscape is one colour, whatever height it sits at.** The board is terraced across five
layers and only one of them is ever the active one, so the depth shade was multiplying every other
step by `belowFalloff` once per layer of drop: grass two terraces down drew at 0.46 of its colour,
and one meadow came out in three greens. The fix is not "terrain never dims", which would take the
cue off the rock in a mine, where it is the whole readout of a working. The shade says how far you
are peering *through* the world, and nothing is over an outdoor surface — so `TintCode.DaylitBase`
marks a cell with no slab and no solid cell anywhere above it, and `ResolveColour` ignores the shade
for one. A tree is not a roof, so grass in woodland is lit like the grass beside it. Ground, water
and tufts all ask; a tuft that kept dimming while the terrace under it stopped would be the same
fault, a layer smaller and much harder to see.

### The black lines between tiles, and the lip of a step

The owner reported black lines where the ground tiles are not flush — subtle on `main`, obvious with
the rim ripple on. **They were never holes.** A gap would show the skybox and the skybox is pale
blue. Every cell is its own box sheared onto the tangent plane of the relief field, so where two
tangent planes disagree the taller cell's own side wall fills the step; that wall is vertical, the
sun is at 72° and terrain casts no shadows, so it receives almost nothing.

`GroundSeamTests` weighed the two contributions rather than guessing between them:

| Source | Worst step between neighbours |
|---|---|
| The relief field's own second-order parting | **14.7 mm** (the earlier "about 41 mm" was conservative) |
| The rim ripple at 3.6 cm | **72 mm** on top of it |

So the ripple was five times the artefact, and **it ships at zero**. It survives as a lever, and
`GroundSeamTests` asserts the default so that turning it back on has to come with a fresh sheet. The
top surface was never the complaint; adding per-cell noise to the part that was working traded a
good surface for a bad one while every test passed.

What is left is the 14.7 mm the relief cannot avoid while each cell is its own box — and it does not
have to be avoided, only lit. **Side faces carry shading normals tilted up towards the sky**
(`SideNormalTiltDegrees`, 38°), so the sliver that fills a seam shades like the ground it sits
between. It costs nothing: no vertex, no triangle, no draw call, no shader change, and 1,312 draw
calls measured with it and without. It is a lever because it trades against readability — the same
tilt lifts a riser towards the colour of the ground either side of it.

**The lip is cut back** (`ChamferMetres`, 22 cm), answering "round off the edges of these steps on
the corner". The rule that makes it affordable is that the chamfer goes on the sides that are
actually open and no others: cut all four and every riser cell opens a groove against the flat
ground behind it, which is the rim ripple's mistake again. So there is a mesh per pattern of exposed
sides — **five**, because turning one is free and sixteen patterns fold onto five — and the cell
spends its bearing orienting the pattern instead of varying the look. A corner drops if either side
meeting there is open, since a corner is one point and cannot be at two heights.

**The cost is buckets, not triangles.** On the wooded board, earth geometry takes 1,061 draw calls
to 1,312 with instances unchanged, and 137 of that 251 is the pattern split. With the chamfer at
zero all five patterns build the identical mesh, so the family collapses to one — otherwise turning
the lever off would cost more than leaving it on.

**Levers and the instrument.** `ChunkRenderer.EarthGeometry` and `ChunkRenderer.Banks`, both off
being exactly the ground as it was drawn before; `GroundMesh.SideNormalTiltDegrees`,
`GroundMesh.ChamferMetres` and `GroundMesh.MaxRipple`. The mesh levers are static and the meshes are
held by reference inside a `ModuleLibrary`, so moving one needs an explicit `GroundMesh.Invalidate()`
and a fresh library, or the ground draws against destroyed meshes and silently disappears. Judge it with *Odyssey → Presentation → Check the
slopes and banks* (`scripts/unity.sh shot Odyssey.EditorTools.SlopeCheck.Run`), which finds the
longest run of one-layer step on the board rather than being told where one is — a hardcoded
position is a terrace on one seed and open meadow on the next — and shoots it plain, with earth and
with banks. **Side on and low is the shot that answers the question**, for the same reason
`SwingCheck` photographs the axe across the line to the tree: in the three-quarter view a step and
the ground in front of it sit at different depths, and the profile of a bank reads as anything you
like.

A quarry has its own instrument, because a terrace and a working are different pictures and
`SlopeCheck` frames a terrace by construction: *Odyssey → Presentation → Check a quarry*
(`scripts/unity.sh shot Odyssey.EditorTools.QuarryCheck.Run`) cuts a 3 × 3 pit **under a colonist's
feet** — `MineJobDriver.MineCell` steps whoever was standing on a cell down onto the floor it just
cut, so she ends up in the hole exactly as the game puts her there — and shoots it with
`ChunkRenderer.BanksInWorkings` on and then off. That lever exists only so the fault can be
photographed rather than remembered. One trap it records: an instance matrix is
`placement * part.Local` and a module's local transform puts the mesh's origin at the middle of its
cell, not at its floor, so matching a bank to the cell it stands in by an exact position finds
nothing and reports a clean board.

And the figure on a bank has its own: *Odyssey → Presentation → Check a bank underfoot*
(`scripts/unity.sh shot Odyssey.EditorTools.BankCheck.Run`). It **builds** its step rather than
finding one — a block of earth laid on the columns beside a colonist, which is a natural step and
not a cut one, so it grows a bank where `SlopeCheck`'s hunt for the longest terrace run would put
the camera somewhere nobody is standing. It shoots with `BankLayout.LiftFigures` off and then on and
prints `MeasuredFootGap` for **every** colonist in a bank, not just the subject, because a figure
sunk into a ramp and a figure standing behind one look the same from every bearing. Two traps it
records: the first version measured the gap through the same call the lever gates, so it reported a
perfect 0.000 m in both conditions — an instrument wired to the thing it is measuring — and the
board is wooded at the generator's own density, so two sheets running put a trunk between the camera
and the subject and the harness now clears the trees around it.

## 2d. The day

**Amended 2026-09-16, the same day it landed: the fixed golden hour is now a cycle.** The owner saw
the fixed hour lit and asked for blue by day, orange at dawn and dusk, and dark at night — which
overturns question 2 of the look interview. That is a change of mind rather than a misreading, and
it is recorded as one because the interview file will otherwise look wrong to the next session.

**Everything below survives the change.** The identity between fog colour and the sky's horizon, the
lifted shadows, the haze that crosses the board, the grade, the anti-aliasing, the shadow-cascade
fix: all of it still holds, and all of it is now twelve numbers per key instead of twelve numbers
once. What the cycle adds is `Daylight`, a keyed table in the *Presentation* assembly rather than
the editor one, because a running game has to sample it and editor code cannot be called from a
player.

- **A table, not a formula.** A physical sun model would give an elevation for an hour and a
  latitude, and it could not say that dawn should be held orange longer than dusk, or that the sky
  should stay bright a little past sunset because that is when a colony looks best. Those are art
  directions, and a table is their honest shape. It is also the thing the owner can edit.
- **Midnight is both the first key and the last**, so the wrap needs no special case anywhere and
  cannot be got wrong by a caller. A test walks the whole day at five-minute steps and fails on any
  jump, including across the seam.
- **Night is a readability floor, not realism.** The sun goes below the horizon but is not switched
  off: a directional light at zero flattens every face to one value and the board reads as a paper
  cut-out. A weak cool key still separates a wall from the ground it stands on, which is the
  difference between night and nothing. Every reference game cheats this the same way.
- **The sky material is copied, never edited.** `RenderSettings.skybox` points at an asset on disk,
  so writing colours into it at runtime in the editor edits the asset — a play session would leave
  the sky wherever the clock stopped, permanently, and it would surface as an unexplained diff days
  later.
- **The ambient probe is the only real cost**, and it is throttled to a tenth of a game hour, which
  at speed 1 is about one re-integration every four seconds. The sun, the ambient colours and the
  fog are not throttled, because stepping those is visible in the shadows.
- **Nothing here is simulation.** The light is a pure function of the tick: not saved, not hashed,
  and unreadable from the simulation, so a colonist at midnight is not blind. If darkness is ever to
  matter to work or sight, that is a simulation feature with its own grid and its own tests.

Judge it with **`Odyssey → Presentation → Check the daylight`**
(`scripts/unity.sh shot Odyssey.EditorTools.DaylightCheck.Run`), which photographs eight hours at
two pitches — the board pitch, where the day is shadows swinging across the ground, and a low pitch,
where the sky is, since the default view never shows it. The hours deliberately straddle the keys
rather than landing on them, so a crease at a key is visible in the sheet.


Owner request 2026-09-16, against six reference screenshots of *Station to Station* now in
`docs/reference/screenshots/station-to-station/`. Interview in `docs/research/look-interview.md`;
five research files behind it (`d-12-urp-post-stack`, `d-13-light-shafts`, `d-14-aerial-perspective`,
`b-station-to-station`, `b-low-sun-readability`). **One file owns the whole palette**,
`Assets/Editor/Odyssey/GoldenHour.cs`, because the effect rests on an identity that is invisible if
its two halves live apart: *the colour the distance fades to is the colour the sky is at the
horizon*. Split across two files those are two numbers that happen to match, and the next person to
warm the sky leaves the fog behind.

**The grounding finding was that there was no post-processing at all.** Not "untuned" — absent. The
pipeline asset pointed its default volume profile at a GUID that resolved to nothing, and the one
profile in the repository was referenced by nothing and held stray editor-test components. So the
project had never had tonemapping, colour grading, bloom, a vignette or any anti-aliasing. Most of
the warmth in the references is exactly that, and none of it is a shader.

**Two recorded decisions are deliberately overturned, and must not be re-derived from the old
comments**, which are rewritten in place rather than deleted so the reasoning is still legible.

- **The sun comes down from 72 degrees to 30.** The old comment rejected a raking sun for darkening
  the ground and throwing long shadows across the surface the player reads. It was right about the
  symptom and wrong about the cause: the darkness was the *shadow strength*, not the angle. A
  shadowed fragment at full strength falls back to ambient alone and loses the key light's hue, so a
  board mostly in shadow goes mostly grey. At strength 0.6 it keeps the warmth and only darkens, and
  that costs nothing. The lost brightness — light on flat ground goes as the sine of elevation, 0.50
  at 30 degrees against 0.95 at 72 — is made up by intensity and ambient.
- **Bloom is adopted**, against `d-09-stylised-rendering.md` §3.4. That note was written for a
  painted look; this is a different target and the references bloom plainly. The threshold sits
  above 1 so only what the sun has actually blown out blooms, rather than the whole frame.

**Shadows had a real bug, independent of taste.** Cascade splits are fractions of distance *from the
camera*, and this camera is tens of metres in the air and never sees ground nearer than about 50 m.
The stock 0.07/0.18/0.42 therefore spent its first two cascades — half the shadow atlas — on empty
air in front of the lens. Splits now start at 0.30, and the distance goes 50 m to 250 m because at
30 degrees the shadows are eight cells long and were stopping a third of the way into the view.
Normal bias, not depth bias, is the grazing-angle lever: acne at a shallow sun is a depth-slope
problem, and depth bias answers it by sliding the shadow along the light, which at this elevation
detaches it from the foot of whatever cast it.

**The haze moved onto the board on purpose.** It was linear from 460 m to 1,100 m, deliberately past
the far corner of a 300 m board, so it never touched the playfield — which is why the board had no
depth in it. It is now exponential-squared at a density chosen by arithmetic: 1% at 50 m, 20% at
224 m, about a third at the rim, 97% by 900 m. Warm haze does not obscure the distance, it places
it. Height fog and a fullscreen fog pass were both considered and rejected in `d-14`: the camera
geometry suppresses what height fog adds, and the pass costs about a third of the frame to do it.

**Anti-aliasing arrives with it, and it has to be SMAA.** Bloom and a warm grade on an aliased image
look worse than either alone, since a stair-stepped edge is what a bloom threshold catches. FXAA is
ruled out by our own outline — it finds edges by luminance contrast and softens them, and a
one-pixel post-drawn ink line is the exact pattern it destroys. TAA would jitter the same line and
wants motion vectors we do not produce for instanced geometry; MSAA cannot help, because the outline
is drawn after the resolve.

**Two faults were found by photograph and fixed, and one of them had been silently true for
months.** URP keeps post-processing *per camera* and defaults it to false, so a camera built in
script renders no volume at all — every contact sheet this project has ever taken was of an ungraded
image. That did not matter while there was nothing to apply and matters entirely now, so `Shoot`
switches it on for every photograph. And the first ambient values put the woodland in near
silhouette: a low sun reaches very little of a tree's crown, so out of direct light a tree is lit by
ambient and nothing else. Ambient is the only lever that reaches the crowns without also blowing out
the ground the sun is already striking.

**Not built, and deliberately not:** the sun shafts and the tilt-shift blur, which are the two
effects the owner also asked for. Both wait on measurements rather than on effort. `d-13` found that
at a 48-degree downward pitch the sun can sit some 120 degrees off the view direction — behind the
camera, where a radial blur has nothing to radiate from — so a framing experiment comes before a
line of shader. And `d-12` found that URP's cheap depth-of-field blurs only the far field, so it
cannot make a band at all; a pass that blurs by screen height is exact, scene-independent and is
probably what the reference game does.

**Cost, and a measurement that was quietly worthless until it was fixed.** `FrameTimeTests` builds
its own camera, and URP defaults post-processing to off per camera — so the first run reported the
golden hour as very nearly free, which was true of the frame it measured and false of the frame the
player gets. It now switches post on, attaches the profile and uses the raking sun, because shadow
length is height over the tangent of elevation and measuring a 72-degree sun would understate the
shadow pass by most of its cost.

| meadow, RTX 5070 Ti, 640 x 480 | mean | worst |
|---|---|---|
| before the golden hour | 1.38 ms | 1.81 ms |
| with it | 1.66 ms | 2.11 ms |
| and with the day/night cycle | 1.71 ms | 2.31 ms |

The cycle itself is all but free once it stops writing values that have not changed: driven
straight from `Update` it cost **0.43 ms a frame** setting `RenderSettings` sixty times a
second to what it already held, which the frame-time test caught and a guard on the hour
having moved recovered in full. About 0.3 ms for the whole thing — grade, bloom, vignette, SMAA, and shadows reaching five times as
far — against a 5 ms budget. **That figure is a floor, not the laptop's number, and the reason is
resolution**: everything in the post stack costs per pixel, and 1080p is nearly seven times the
pixels this measures at, while the shadow and geometry work is unchanged by resolution. The laptop
figure has to be taken on a laptop, and the quality tier the interview committed to is what answers
it if it is bad.


Judge it with `Odyssey → Presentation → Check the ground relief` and `Check the meadow at range`,
both of which now photograph the graded image. **It still wants the owner's eye in `Play.unity`.**


## 3. The camera and the slice

**The camera** is a three-quarter orbit at a constrained pitch, matching the concept renders: pan across x/z, zoom, rotate in 90° steps or freely, and a vertical control that changes the **active layer** rather than the camera height.

**Holding shift moves further** (owner ask, 2026-09-16). The board is 300 m across and the pan speed
is chosen for looking at one colony, so crossing it takes a while. Shift multiplies every camera
*translation* — WASD or the arrows, the middle-drag pan, the scroll zoom, and the slice step, which
covers `fastLayerStep` storeys instead of one. It scales **on top of** the existing distance
scaling rather than replacing it, so a fast pan zoomed out is still faster than a fast pan zoomed
in, which is what keeps the two feeling like one control.

Orbiting is deliberately excluded. It is already a direct mouse-delta mapping, and three times a
mouse delta is not a fast orbit but an uncontrollable one. The levers are
`SliceCameraRig.fastMultiplier` (3) and `fastLayerStep` (4); a multiplier below one turns shift into
a precision modifier instead, which is allowed on purpose.

**The slice model**, which is the whole point of the project:

| Layer relative to the slice | Rendered | Interactive |
|---|---|---|
| Above the active layer | Ghosted outline, heavily transparent, or hidden entirely (a player setting) | **Never** (amended — see §3c: solid is clickable, a ghost never is) |
| The active layer | Fully, and with its ceiling slab suppressed so interiors are visible | Yes |
| 1 to N layers below | Fully, progressively darkened with depth | No (amended — see §3c) |
| Deeper than N below | Not drawn | No |

Three decisions inside that table are deliberate:

1. **Layers above are never interactive.** Going Medieval's single most-reported complaint is misclicking something on another floor, with players reporting buildings deconstructed by accident (`b-going-medieval.md`). Ghosted geometry is a depth cue and nothing else; selection and designation raycasts stop at the active layer. This costs nothing to decide now and is unpleasant to retrofit. **Amended 2026-09-16 by §3c, and the amendment keeps the sentence that was doing the work: a *ghost* is never a pointer target. What changed is that above the surface nothing above the slice is a ghost any more.**
2. **The active layer is drawn roofless.** The ceiling slab of the active layer is suppressed — exactly what the concept renders show, and the only way interiors read at all. The slab is still *there* in the simulation; this is purely a render decision.
3. **Layers below stay visible and darkened.** This is the depth cue that makes a hole in the floor legible as a hole rather than a black square, and it is what makes building above an occupied room comprehensible. N and the darkening curve are settings, because the right value is a matter of taste and screen size.

### 3a. The depth chooses the treatment (owner, 2026-09-16)

The table above is what a *mode* does. What a player actually gets, before they have chosen a mode,
now depends on how deep the slice is — `SliceSettings.followDepth`, on by default.

| Where the slice is | Above it | Below it |
|---|---|---|
| At or above the surface (the layer the game opens at) | **Every layer, drawn solid.** No depth cap, no fade | `belowDepth` layers, dimmed — **or the bottom of the landscape, whichever is lower** (§3b) |
| Below the surface | **One layer**, x-rayed — a ceiling, not a survey | **Every layer, to the floor**, dimmed with depth |

The rule came out of a playtest report with a one-line diagnosis: *"I couldn't see another person
mining above me."* Two faults, one sentence.

**The first was a bug and not a policy.** The layers above the slice were being x-rayed exactly as
this section says, so the rock was drawn — but every *actor* in them was culled outright, by
`PawnFigureDirector.Sync` and `ChunkRenderer.RenderActors` alike, both testing `cell.Y >
activeLayer`. Items too. A colonist working a storey up did not exist on screen. The cull is now
against `SliceSettings.HighestVisibleLayer`, so **a figure is drawn on every layer the world is
drawn on and on no other** — actors solid at full opacity, which is the owner's call: a figure faded
to match the rock around it is invisible within two layers, and being sure who is overhead beats
being sure how far overhead they are.

**The second is the policy.** Above ground the player is outside looking in and wants the whole
stack; underground the question reverses. What is overhead is a ceiling and one layer of it is all
the context that helps, while what is *under* you is the shape of the working — and a base three
storeys deep is unreadable through a three-layer cap. So the cap comes off downwards and goes on
upwards. The owner's reason is worth keeping verbatim: *"you need to be able to see within the
environment — if there was ever digging introduced into the game or underground base."*

**Solid above the surface, and that was a correction.** The rule first shipped drawing everything
above x-rayed, which is what `xray` had always done — and the owner's reply was *"this includes
everything buildings, stones, rocks and everything, as I noticed the mining rocks were
transparent"*. An outcrop standing two cells above the surface is a rock, not a hint of one. So
above ground the treatment is `full`: solid, no fade, no cap.

**It is `full` for opacity and not for the lid.** `full` is documented as the exterior and
screenshot view, the control case with no cut-away anywhere — and §3 point 2 above says the active
layer is drawn roofless, which is a separate standing decision that a default has no business
quietly reversing. `SliceSettings.SuppressCeilingAt` splits them: the depth-following default keeps
the active layer roofless, an explicitly chosen `full` keeps its lid. Nothing differs outdoors,
where there is no slab overhead; it differs the moment anything is built, which is why it is
settled now rather than discovered then.

**Underground, where the treatment is translucent, "every layer above" would be bounded by the fade
rather than by a count** — except that underground is capped at one layer anyway. The bound still
exists and still matters for any explicitly chosen `xray`: at the shipped tuning (`ghostAlpha` 0.38,
`ghostFalloff` 0.72) the alpha ramp runs 0.380, 0.274, 0.197, 0.142, 0.102 … and crosses the 0.012
cutoff after eleven layers. `HighestVisibleLayer` returns that layer, and it is the same constant
the chunk loop skips on, so the two cannot drift apart.

**Solid has no fade to stop it, so the top is capped by the geometry instead.**
`ChunkRenderer.BatchFor` *meshes* a chunk the first time it is asked for and again after every
version bump, so an unbounded loop would mesh a dozen layers of empty sky on every edit of a tall
map, for nothing. `WorldRenderModel.HighestOccupiedLayer` is the top of the geometry plus the layer
a colonist standing on it occupies, raised as cells are copied into the mirror and lowered only by a
full refresh. A high-water mark is the safe direction: the worst it does is draw a few empty layers
after a demolition, where the other way round it would hide a roof somebody had just built.

**Measured cost, which is why the cap could come off at all.** On the 16-layer prototype board with
the surface at L11, "every layer above" is L12–L15 — four layers, which is exactly what the old
`aboveDepth` of 4 already drew. Underground at L6 the old range was L3–L10 and the new one is
L0–L7: eight layers either way. Deeper it gets *cheaper* — at L2 the old range was seven layers and
the new one is four. Nothing on the board being played pays anything at all.

The six ADR 0006 modes are untouched and still ship. Switching `followDepth` off obeys every field
exactly as before, which is what choosing a mode explicitly does: the V key's first press pins
whatever is on screen and hands over control, and cycling past the last mode gives the default back.

### 3b. The landscape is never cut away (owner, 2026-09-16)

The owner's report, with a screenshot: on the low ground under the trees *"there appears to be no
ground texture or grass"*. The picture showed a flat, untextured, un-tufted plane with woodland
standing on it and a hard diagonal edge where the green meadow stopped. Nothing was wrong with the
ground. It was not being drawn at all, and what showed through the hole was the sky.

**The surface is terraced and the depth budget is not.** `surfaceRelief` of two gives a five-step
surface, so the outdoor ground of a wooded board spans five layers and only one of them is ever the
active one; the row above cut the drawn band off `belowDepth` (three) layers under the slice. Stand
on a high terrace and the low ones fall out of it. The trees survived the cut because a tree lives in
the air cell one layer *above* the ground it grows from — so the wood was inside the band while its
ground was not, which is exactly the picture that arrived.

Measured on the board that is played, 120 × 120 × 16, seed 1: the ground runs L8 to L12 with
outcrops standing to L14, and the colony opens on L12. Move the slice one layer up, to L13, and the
budget alone draws from L10 — where **6,140 of 14,400 columns have no ground at all**. At the
opening layer itself it is 383 columns, which is small enough to be missed and was. Seeds 2 and 3
both open on L11 and lose 726 and 427 columns a layer above that.

**So the band reaches down to the bottom of the landscape**, and the argument is
`TintCode.DaylitBase`'s, arriving one step earlier. That bit exists because the depth *shade* was
dimming a lower terrace to 0.46 and the meadow came out in three greens: the shade is a cue for
looking **through** something, and there is nothing over an outdoor surface for it to describe.
The same is true of the cut. A lower terrace is not ground you are peering through something at, it
is ground. There the fix was that it must not be dim; here it is that it must exist.

**The floor is measured off the generated board and never moves after.**
`WorldRenderModel.LowestOutdoorLayer` walks every column from the sky down to the first solid cell
or floor slab in it and keeps the lowest answer — four or five reads a column, taken once before the
first frame. It is deliberately *not* maintained as the world is edited, which is the opposite
choice to `HighestOccupiedLayer` and for the opposite reason: a roof somebody builds has to appear,
whereas a pit somebody digs is precisely the "looking through a hole" case the depth budget is for.
Without that, a shaft sunk to bedrock would force every cavern in the map to be drawn while the
player stood in a meadow.

It is a floor and not an override: a landscape that stops inside the budget does not shrink the
band, `BelowMode.Hide` still hides everything below the slice, and underground — where the cap is
already off downwards — the floor has nothing to add and is not consulted. Switching `followDepth`
off hands every field back exactly as before, the floor included.

**Everything that reads the band reads the same one.** `ChunkRenderer.Render` and `RenderActors`,
`PawnFigureDirector.Sync`, `SlicePicker.Band` and `SliceCameraRig.LowestSelectableLayer` all pass the
model's floor in, because a colonist walking a low terrace was being culled along with the terrace,
and because a terrace the player can see is a terrace they can mark for mining — "selectable" and
"drawn solid" must not drift apart (§3c).

The cost is the terraces themselves and nothing else. Buried cells are face-culled before they reach
a bucket, so the two extra layers a slice at L13 now walks contribute the ground that was missing and
no instances anywhere else, and the span is bounded by the generator's own relief at
`surfaceRelief × 2 + 1` layers.

### 3c. What can be clicked is what is drawn solid (owner, 2026-09-16)

§3 point 1 said selection and designation rays stop at the active layer. Once §3a made the whole
stack above the surface draw **solid**, that rule started costing the player the world it had just
been given:

> *"On my default depth level I can only select objects/things on my level — I couldn't select the
> stones for mining, for example. I should be able to click on an object in 3D space."*

An outcrop standing two cells proud of the meadow is now drawn as a rock, at full opacity, and it
could be looked at and not marked. So the rule is restated rather than dropped:

| Layer, relative to the slice | Drawn | A pointer target |
|---|---|---|
| Above, solid (`full` / `roofs-off`, and the depth-following default above ground) | Yes | **Yes** |
| Above, translucent (`ghost`, `xray`, `xray-min` — which is what underground gets) | Yes | **No** |
| Above, hidden (`hide`) | No | No |
| The active layer | Yes | Yes |
| Below, within `belowDepth` | Yes, dimmed | **Yes** |
| Below, beyond `belowDepth` | No | No |

**The misclick complaint is answered by the second row, not by the first.** What makes a misclick a
misclick is clicking a *cue* — a translucent hint of a wall, drawn to tell you something is there
rather than to be operated on. Underground, where the layer overhead is x-rayed precisely so you can
see through it, a click still cannot leave the active layer upwards and the old behaviour is
unchanged, which is the case the Going Medieval reports are actually about. Dimmed is not ghosted: a
layer below is opaque, and a ray only reaches one where nothing above it occludes — down a shaft,
over a cliff, through a stairwell — which is exactly where a player means to click.

**What a click selects: the thing you clicked, or nothing.** The first attempt kept the picker's
old convention — a floor crossing returns the *air* cell whose floor it crossed — and the owner
rejected it: *"I still wanted to select the tile below it or not at all."* That convention was never
chosen; on one layer the air cell was the only cell on offer. Carried up and down a stack it reads
as clicking a rock and selecting the sky above it. So:

| What the ray meets | What is selected |
|---|---|
| An occluding cell — wall, door, pillar, solid strata | that cell |
| The floor of a cell holding an **edifice** — a tree, and later a bed or a workbench | that cell: the edifice is the thing you clicked |
| The floor of a cell holding a **built floor slab** | that cell: the slab is drawn in it |
| The floor of a cell with nothing but solid terrain beneath | **the block beneath**, whose top face that is |
| The floor of a cell with nothing beneath | nothing. A hole is a hole |

A solid cell's top face and the floor of the air cell above it are one surface at one distance, so
two layers bid at the same ray parameter — and under this rule they resolve to the same block, which
is what makes the tie harmless. Where they disagree is a tree: the tree's cell and the ground under
it offer the same face, and **a thing beats bare ground**, failing which the layer nearer the slice
wins.

**The edifice row is not an exception, and leaving it out would have broken felling outright.** A
tree is an edifice that blocks nothing, standing in the walkable cell. "Select the tile below" taken
literally hands back the ground under every tree and the Fell order can never be given again. What
the player is looking at there is the tree.

**And the order had to be lifted to match, for a drag.** A click on a tree names the tree, but a
fell box begun on open grass anchors a layer too low and every cell of it would be refused in
silence — the tool swept across a wood and nothing happening. `DesignationGrid.Designate` therefore
reads a Fell order named at solid ground as the tree standing on it, and `Cancel` mirrors it. Only
felling: mining means the block itself, which is exactly what the click now gives, and it is the
same relation `CanMine` already knew from the other side — that the ground under a standing tree is
not diggable while the tree is up.

**A surface that is not drawn is not clickable, and that has a case of its own.** The active layer's
ceiling is the slab of the layer *above* it, and §3 point 2 meshes it away. So that one layer offers
only what occludes: the rock over your head stays pickable, the dropped slab does not.

**`SlicePicker` walks the band rather than raycasting the scene.** There are still no colliders —
the world is instanced geometry with no GameObjects — so it clips the ray analytically to each
candidate layer's slab and marches it, taking the nearest hit. The band is a `SliceSettings`
question (`HighestSelectableLayer` / `LowestSelectableLayer`), so "selectable" and "drawn solid"
come from the same object that decides what is drawn and cannot drift apart. Colonists follow the
same band: `SelectionPresenter` culls the figure hit-test and the drag box against it, with one
extra rule for a ray — a pawn must be at or above the layer of the cell the ray ended on, because
the pick ray descends, so everything it met before the ground is at or above it. That is the cheap
stand-in for comparing ray distances and it is exact for the only camera this game has.

**Cost:** one cell march per candidate layer, on a click and on the hover that draws the cursor.
Four layers above and three below on the prototype board is eight marches of at most a few hundred
array reads. The band is capped at `WorldRenderModel.HighestOccupiedLayer`, the same cap the
renderer's own loop uses, so a tall map does not march through empty sky.

**Implementation:** per-layer visibility on the instanced batches, not a clipping plane. The renderer simply does not submit buckets outside the drawn range, which is cheaper than submitting and clipping, keeps shadow casters honest, and makes the ghosted layer a different material rather than a shader branch. A clip plane remains the fallback if a single mesh ever needs to be cut mid-cell, which the discrete-cell model is specifically designed to avoid.

### 3d. Whatever hides a selected colonist is drawn as a ghost (owner, 2026-09-16)

The report: trees getting in the way of being able to see the colonist. The board is a wood, the
camera orbits rather than cuts, and a selected colonist who walks under a canopy is simply gone —
the only remedy was to spin the camera until a gap opened, which loses the bearing the player had
and has to be done again the moment they move.

**The rule.** Anything standing on the line from the eye to a selected colonist is drawn ghosted
instead of solid, for as long as it stands there. Nothing else changes: the geometry is still
submitted, still occludes what is behind *it*, and is still clickable. It is the ghost material
§3a already uses for an x-rayed storey, at a lower alpha (`ChunkRenderer.SightFadeAlpha`, 0.22),
which is deliberate — a hint of what is there reads better than a hole, and it is one idiom for
"you are seeing through this" rather than two.

**It is a segment test against real geometry, not a cell march, and that is the load-bearing
decision.** A tree's cell is the one its trunk stands in, while what actually hides a colonist is
its crown, six metres up and a cell or two nearer the camera. Asking which cells the line passes
through fades the wrong ones. Asking whether the line passes through an instance's own world
bounds gets a tall thing right for the same reason it gets a wall right, and it needs nothing from
the mesher: the bounds come from the module and the placement from the matrix that was going to be
drawn anyway.

**The verdict is taken on the module, not on the part.** A tree is a trunk bucket and a crown
bucket; asked separately, the crown fades and the trunk stays, which reads as a rendering fault
rather than as a courtesy. A bucket stores `placement * part.Local`, so the placement is recovered
once per bucket and the module's own bounds — the same bounds the selection cursor fits to a thing
— is placed by it.

**The line stops at the chest**, which is the point the bracket is drawn at and the point the
hit-test aims at. Two things follow and both are the reason for it: geometry beyond the colonist
hides nothing, and the floor they are standing on is more than a beam-radius below the end of the
line, so the fade cannot open a hole under the person the player just selected.

**Cost is confined by two grains.** `SightLines.Touches` is asked once per chunk and answers no for
all but a handful; only inside those does the per-instance test run. Grass is skipped outright: a
tuft hides nobody and tufts are most of the instances on the board. A chunk's bounds are one layer
high, so the coarse test is given an allowance for geometry taller than its own layer
(`ChunkRenderer.TallestModuleMetres`, 12 m) — without it the coarse test rejects the very chunk
holding the crown that is doing the hiding and the whole feature silently does nothing while every
per-instance test still passes. The number of lines is capped at eight, because a box selection can
hold the whole colony and the cost of this must not grow with the size of the selection.

**Measured, at the board camera's own 48° pitch:** a nine-metre tree occludes a colonist only
within about eight metres of them, because the sight line rises 1.11 m for every metre of ground.
That is not a limitation, it is the geometry — a tree twenty metres further back is twenty-two
metres below the line and never hid anybody. The beam radius (0.9 m) stands for the width of the
person, not the thickness of the line; the occluder's own size is already accounted for by testing
its bounds.

**Not faded:** other colonists, items and the live animated figures. Fading a skinned figure means
swapping materials on its renderers rather than partitioning an instance array, and a person
standing in front of a person is a much rarer complaint than a wood is.

**Nor grass, water, banks or marsh** (`ChunkRenderer.NeverFades`). Grass was exempt from the first
day on cost grounds — a tuft hides nobody and tufts are most of the instances on the board — and the
other three joined it on 2026-09-18, on two reports from the owner: *"it shouldn't do it on the
artificial façade terrain on the height edges and in water/around water"*, and then *"sometimes it
hides marsh as well — omit this."* The argument is the same one all three times, and it is not cost.
**They are surfaces rather than objects, so half of one is not a view through it — it is a hole.** A
pond is drawn as a body of faces, and fading the faces a beam crosses opens a window into the bed of
the stream with a ragged edge where the beam stops; a bank is a sheet leaning on a terrace step that
*no cell in the simulation contains at all*, so fading it cuts a gap in a hillside that has no gap in
it; marsh is the wet fringe of the same pond, so fading it punched a hole in the shore right beside
water that stayed whole. None of them hides anybody in the way a wall or an outcrop does: a colonist
in the water or the bog is standing in it, and one at the top of a step is above the bank rather than
behind it. The ground either side of them still fades, which is the feature.

Water keeps the marker it already had. The bank and the bog share one bit named for the **rule**
rather than for either of them — `TintCode.WholeBase`, "a surface drawn whole" — which is what makes
this two lines rather than a bit per noun, and what the next surface that wants it will use. **Marsh
is the awkward case and the reason the name matters:** it is an ordinary solid ground cell, walkable
and in `NaturalContent.IsGround`, so it fell through every exemption there was. The bit is set in
`ChunkMesher` (`DrawnWhole`) rather than named in the content tables, because nothing in the
simulation is different about a bog for this reason. It costs no extra bucket worth counting and
changes no colour: a bank and a bog are both still terrain, still tinted as terrain.

`SightFadeExemptionTests` pins all of it, and each render test carries a **control**, because an
exemption is the easiest thing in the world to assert vacuously: the water and marsh tests show the
same beam through the same cell still ghosts rock, and the bank test draws the same board with banks
switched off and shows the count does not move. All of them were checked by removing the exemption
and watching them fail.

**On by default** (owner, 2026-09-16), which has three separate homes and needs saying in all
three: the field initialiser, `SettingsDirector`'s own starting state, and the serialised scene. A
field absent from the scene YAML falls back to the initialiser, so the scene was quietly right
without ever saying so — which is the arrangement that breaks silently the next time somebody
rebuilds it. `Play.unity` names the lever now and `PlaySceneContentsTests` guards it, in the same
shape as the `followDepth` guard beside it.

**Levers:** `OdysseyBootstrap.seeThroughToSelection`, `seeThroughRadius`, `seeThroughAlpha`, and a
switch in the settings panel's **Graphics** tab (B17) — Graphics rather than Interface because the
tabs divide on how the HUD is drawn against how the *world* is, and this is the world. It is a free
lever, read as the frame is submitted, so it needs no remesh. Judge
it with **`Odyssey → Presentation → Check the see-through fade`**
(`scripts/unity.sh shot Odyssey.EditorTools.SeeThroughCheck.Run`), which finds the colonist on the
board with the most geometry in the way at four bearings rather than being told where one is, and
shoots the pair off and on.

## 4. Overlays

Zone, roof, power, temperature, beauty, light and salvage overlays are **chunk meshes, never UI elements**. One procedural mesh per chunk per layer, with per-cell colour in a vertex stream, drawn instanced, for the active slice plus at most one ghosted neighbour.

The arithmetic that forces this: one layer is 62,500 cells. One UI element per cell is fatal on the target hardware, and the UI session's plan independently reached the same conclusion, which is a good sign. Budgets carried from that plan: a full slice rebuild under 4 ms, an incremental chunk rebuild under 0.5 ms.

Overlay state (which overlay is on) lives in the UI session's `OverlayDirector`; this document owns the meshing and the draw.

## 5. Material tint for "stuff"

RimWorld's building materials colour the thing built from them. With tens of thousands of instanced modules, per-instance materials are not an option.

**Committed strategy** (`e-04-tint-strategy.md`), which is pleasantly boring because the packs already support it:

- `Synty/Generic_Basic` multiplies `_BaseColor` over the albedo atlas, and every property sits in the `UnityPerMaterial` CBUFFER, so it is SRP Batcher-compatible.
- Therefore: **one cached material per stuff**, created once, `_BaseColor` set once. Render buckets are keyed by (mesh, stuff). Stock Synty shaders are used untouched — which also keeps the licensed-asset boundary clean, since nothing is forked out of `Assets/Synty/`.

**Fallback**, only if bucket counts explode or a per-instance overlay tint must combine with the stuff tint: an own-authored URP graph declaring the tint as a Hybrid Per Instance property, drawn through a `BatchRendererGroup`. Note what does *not* work and should not be attempted: per-renderer `MaterialPropertyBlock` colour, which breaks SRP batching and does not combine with Shader Graph properties as people expect.

**Validation:** a spike rendering 20,000 walls across several materials, checked in the Frame Debugger for batch counts, before M1 commits to the bucket scheme.

## 6. Characters

Pawns are `SkinnedMeshRenderer` GameObjects — 50 colonists and a few hundred animals is well inside what Unity handles conventionally, and they need Mecanim anyway. The rig is the Polygon ~50-bone humanoid with one shared controller (`e-02-characters-animation.md`); the 88-bone Sidekick rig was rejected as roughly double the cost for no benefit at this count.

Pawns are visible only on the drawn layers and are culled with the slice, so a colonist three floors down does not draw.

## 6a. Work poses, where no clip exists

None of the packs contains a work animation. `AnimationBaseLocomotion` ships idle, walk, run,
sprint, crouch, in-air, turns and transitions plus additive lean and look, and the character packs
ship no clips at all. There is no swing, no strike, no lift and no carry anywhere in 7,222 imported
assets. A colonist felling a tree therefore stood in the standing idle, breathing, for the ten
seconds the job takes, and then the tree fell over — the single least readable thing on the board,
because the one moment the player most wants to see is the moment work happens.

**The decision (2026-09-16): compute the pose rather than author the clip.** Every character in the
packs is a Humanoid rig, so the arm and torso angles can be asked for by *role* —
`HumanBodyBones.RightUpperArm` and its four neighbours — and one set of angles drives all 61 faces
without touching a single character. `WorkSwing` holds the timing and the angles as pure arithmetic
with a test against it; `PawnFigureDirector` lays the result over whatever the gait mixer produced,
in the same frame, just before the figure is drawn.

Four things about it are deliberate and each was a way of getting it wrong:

- **The pitch is applied about the *figure's* right-hand axis, not the bone's local axis.** Which
  way a bone's local axes point is a decision made by whoever rigged the character. The plane an
  axe swings in is a fact about the figure, and is the same on every rig the packs will ever hold.
- **The stroke is not a sine.** It is three unequal parts — a long eased raise, a short
  accelerating strike, and a dwell with the blade in the wood. A symmetric swing reads as a
  metronome and a swing with no dwell reads as waving; neither reads as work.
- **The pose is laid over the clip, not in place of it.** The colonist goes on breathing — except
  while the game is paused, for which see §6b.
- **The three angles are independent, which took work.** The spine carries the shoulders through the
  skeleton, so folding the back further into the blow also swung both arms, and no amount of tuning
  could settle one without moving the other. The director subtracts the spine's own pitch back out
  of the shoulders, after which `Shoulder` means the upper arm's pitch against the world — which is
  what anybody judging a photograph is actually looking at.
- **The figure steps up to the tree, and that is presentation's business.** A cell is 2.5 m and a
  person is half of one, so a colonist drawn on the cell centre is either inside the trunk or
  shoulder against it, and in neither is there room for an axe to travel. `WorkStance` draws a
  working figure wherever puts its blade in the wood, eased in by the same weight that eases in the
  swing, so it reads as setting oneself. Nothing else moves: the pawn is still in its cell for
  picking, for the cursor and for the whole simulation.
- **The stand is solved from the whole strike offset, not from a reach.** The first version stood
  the figure at the length of the line from its feet to its edge. That is right for a swing that
  comes straight down in front and wrong for one that comes over the shoulder: of 1.68 m of
  measured strike, **1.12 m is sideways**, so a figure stood at 1.68 m puts its axe a metre beside
  the tree. Reach is not a scalar once the swing is diagonal. The figure now stores the whole
  offset in its own frame and the stand is wherever puts that offset's far end in the trunk.
- **Contact is measured, not photographed.** `PawnFigureDirector.MeasuredBladeGap` reports how far
  the edge finished from the middle of what it was aimed at, on the frame that was drawn — 0.19 m
  against a trunk about 0.6 m through, so the blade is in the wood. Twice the swing was judged to
  be missing from a three-quarter photograph when it was not; the woodcutter and her tree sit at
  different depths in that view and the gap can be read as anything.
- **Both hands are on the haft, and the off hand reaches for it.** Angles pose the hand that holds
  the tool and can never pose the hand that has to meet it: shoulders are the better part of half a
  metre apart, so a left arm given a fraction of the right arm's angles ends up in a plausible
  attitude holding nothing. `ArmIk` is the ordinary two-bone analytic solve, run in the same pass
  as the rest of the work pose, and the same call will hold the other end of a stretcher or a
  carried crate later.
- **The axe is gripped by measurement, not by three Euler numbers.** Which way a prop's haft runs in
  its own space is a decision made by whoever modelled it. So the haft is found — the long axis of
  the combined mesh bounds — the head end is found, and the tool is laid along the forearm with the
  grip in the palm. One tunable is left, `AxeBladeRoll`, because which way the edge faces is taste.
  A fixed rotation tuned against one prefab hung the axe head-down by the hip, which reads as
  carrying a hatchet rather than using one, and would have been wrong again for the next tool.

The simulation's half of this is one signal: `PawnView.Working` and `PawnView.WorkCell`, published
from `JobDriver.WorkFocus`. It is a cell rather than a flag because the pose needs a direction — a
pawn that has stopped walking has no heading left to read, so without the work cell a colonist
would swing at whatever they happened to be facing when they arrived. `WorkFocus` defaults to -1,
so mining and building inherit the swing by overriding one expression.

Two things about it are not obvious and both cost time on the way in. A `SkinnedMeshRenderer`
caches the bone matrices handed to it by the animation update, so a bone written *after* that update
moves anything parented to it and leaves the mesh where it was — the axe swung through a perfect arc
while the colonist stood still. `forceMatrixRecalculationPerRender` on the figure's skinned
renderers is the fix. And the sign of a limb rotation cannot be reasoned out from the axis name: a
limb that hangs down travels forward under a *negative* pitch, while a spine, which starts upright,
does the opposite, so one convention read off the axis gets one of the two wrong whichever way you
read it. Both are in `docs/lessons.md`.

A note on the harness itself, because it decides what can be settled. `SwingCheck` shoots **side on
to the line between the woodcutter and her tree**, not from the board camera's three-quarter
bearing. In three-quarter the two sit at different depths, and the one measurement that matters —
whether the blade arrives at the trunk with room to have travelled — can be read as anything you
like. It was, twice.

**The pose itself is the owner's, settled by interview on 2026-09-16** rather than invented: the
edge meets the wood angled about forty-five degrees down and in, cutting a felling scarf; the axe
travels up past one shoulder and down diagonally across the body; it lands at waist height with the
blade just into the bark; and both fists grip together at the butt of the haft. The roll of the
blade follows from the first of those and is computed rather than dialled in — the bit is turned to
face the way the head is travelling, so the edge bites at whatever angle the haft has reached.

The axe itself is `ModuleIds.ToolAxe`, an ordinary catalogue row parented to the right hand for as
long as the work lasts. A clone without the packs resolves it to null and colonists fell trees
bare-handed, which is the same fallback every other piece of pack art has.

**Everything a figure does that is not walking and not a tool stroke** — the lift, a crouch, the
climb, an aimed weapon — is designed in `13-gestures.md`, which generalises this section's two
hard-coded pose branches into a small vocabulary and adds the legs the climb pose does without.
This section stays the record of the felling pose itself.

### Debris: one director, one system, a recipe per material

A blow with nothing coming off it reads as a colonist waving an axe near a tree, so a few chips fly
on the frame the blade lands. `ChipDirector` owns that, beside `PawnFigureDirector` and disposed
with it, and the design is meant to carry mining and everything after it:

- **One particle system for the whole colony**, simulated in world space, so a single emitter
  throws from wherever a blade happened to be. A system per figure would multiply draw calls by the
  number of workers for a handful of quads.
- **The material is a `ChipRecipe`**, not a director. Colour, size, speed, lifetime, count and
  spread are all settable *per particle* at the moment of emission, so wood off an axe and stone
  off a pick share the system, the material and the draw call. `ChipRecipe.Stone` is already
  written. Adding a material costs a preset and a call, nothing else.
- **Gravity is the one thing a recipe cannot have**, because it belongs to the system and applies
  to everything in flight. Heavier debris is expressed by leaving the cut faster, smaller and dying
  sooner, which at board-camera height reads the same.
- **It is warmed on construction.** The first draw of a particle material compiles its shader
  variant and allocates the system's buffers; left alone that lands on the frame the first axe
  hits, which is the one frame anybody is watching. The warm throws eight transparent, zero-size
  chips far below the board and steps the system once. It is a warm and not a guarantee — a
  pipeline that defers compilation until a material is genuinely visible still pays once, and
  doing better means a shader variant collection, which is a build-time job.
- **The hard part is not the particles, it is knowing when.** For felling that is
  `WorkSwing.Lands(previous, current)`: whether the stroke phase crossed the strike, which is three
  questions rather than one — the ordinary crossing, the wrap that must not fire twice for one
  blow, and a dropped frame that must still fire. It is pure arithmetic with its own tests, and it
  is the part worth copying for mining rather than the particle setup.

Chips are decoration in the same sense the grass tufts are: not simulation objects, not in a cell,
not in the save, not in the state hash.

**What this is not.** It is not a substitute for authored clips. When work animations exist — bought,
or made in Blender against this rig — they replace the computed pose and `WorkSwing` goes. Until
then this is the cheapest thing that makes the colony look like it is doing something, and it cost
no art.

## 6b. A pause holds the frame it is on

Pausing should stop the board dead and starting again should carry on from there. It did neither,
and the owner reported both halves: figures reset to a standing pose, and some carried on for a
moment first.

**Pause is now a fact the snapshot carries** — `WorldSnapshot.GameSpeed`, exactly
`SimWorld.GameSpeed`, with `Running` beside it. It used to be inferred from the tick standing
still, which cannot be done without a delay: a quarter of a second had to pass before a stopped
tick could be told from a slow frame. Measured against the axe's 1.15 s stroke that grace is 24% of
a swing running on after the player pressed space, and it is exactly what "some even carry on for a
moment" describes. A paused world publishes one more frame — the tick `OdysseyBootstrap` spends
letting the speed change through — and that frame already says paused, so the lag is one frame
instead of fifteen. It defaults to 1, so a snapshot nobody has written reads as a running world and
every pose harness keeps working.

**Nothing eases while the world is stopped.** The inference had only ever gated the swing, so every
other ease in `PawnFigureDirector.Pose` went on running. The one that shows is the gait: the pawn
stops moving, the measured speed is nought, and the figure's own speed is smoothed towards it at
0.35 a frame — which against the real gait speeds carries a walking colonist from 73.5% walk weight
to 99% idle in **ten frames, 0.167 s**. That is not a reset and is indistinguishable from one.
`Pose` now runs every ease on one clock that is real frame time while the world runs and exactly
nothing while it does not, so `MoveTowards` with a step of zero holds the value it had; and
`ObserveSpeed` treats a frame with no time in it as *no answer* rather than as a measured nought,
so the stride is still there when the world moves again. Placement is deliberately not on that
clock: a figure leased because the player scrolled the slice while paused still has to be put
somewhere.

**And the clips themselves stop.** The graph is played with `DirectorUpdateMode.GameTime` and
nothing in this game touches `Time.timeScale`, so Unity went on evaluating every figure on
wall-clock frames whatever the simulation was doing — a paused colony breathed and shifted its
weight. `Blend` sets the clip rate to zero, which is the same call it already makes on every clip
on every frame: the clip time stops, the pose the graph writes is the pose it wrote last frame, and
when the rate comes back the clip *continues* rather than restarting. Stopping the graph would also
have left the bones unwritten, and the work pose is laid over what the graph writes. The chips stop
the same way, at `simulationSpeed` zero, since they are simulated in world space by Unity and know
nothing about the tick.

## 6c. What the frame costs, and the one number that explains it

Measured 2026-09-20 on the played meadow, an RTX 5070 Ti at 640 x 480, under the real player loop
(`FrameTimeTests`; never an editor render loop — see `docs/lessons.md`).

### The number

**A draw submission costs about 4.6 us whatever is in it.** That is the single most useful figure
for this renderer. It is not triangles and at this resolution it is not fill: three passes were
submitting once per cell, and all three were expensive for that reason alone and for no other.

> **Qualified a day later, and the qualification matters.** The mark pass was measured with a
> control in the same run and its submissions cost about **0.1 us**, not 4.6 (§6c.1). Every
> reading the constant came from submitted a real mesh; a mark is a unit cube in a cached
> material. Treat 4.6 us as what a *loaded* submission costs — the number to reach for when a
> pass is slow and you are looking for the reason — and not as a price that condemns any
> per-cell loop before it is measured.

    meadow 5.04 -> 2.59 ms      field (2,065 zone cells) 6.95 -> 4.09 ms
    field draw calls 3,847 -> 1,300

### Where it went

| Pass | Was | Now |
|---|---|---|
| Growing-zone cover | one `RenderMesh` per zoned cell per frame — 2,065 calls, 3.67 ms | a bit on the terrain bucket's tint, no draws at all (§22-growing 6a) |
| The surround wood | 760 instanced batches, 3.65 ms | 272 batches, same 4,169 trees |
| Seed specks | six `RenderMesh` per sown cell | one `RenderMeshInstanced` a frame |

**The surround is the instructive one, because the obvious answer was wrong.** Dropping all 2,577
hill trees changed nothing (5.37 ms against 5.04); dropping the 1,592 near trees took the meadow
to 2.26. But the cost tracked the *batch* count, not the tree count — 760 batches 3.5 ms, 438
batches 2.1 ms. A batch key carries a spatial sector, and at 80 m the ring outside a 300 m board
fell into hundreds of near-empty batches. Coarsening the sector to 400 m rides the same trees in
272 batches and the wood is untouched. Thinning was measured too — 30 per cent took 1,592 trees to
435 and saved 1.5 ms — and is strictly worse: it costs the look and buys less.

The trade in that: a coarser sector is a looser `worldBounds`, so less of the wood frustum-culls.
Measured, that is the right way round — the draw the culling saves is cheaper than the per-batch
cost of being able to save it. `TerrainSkirt.TreeSectorMetres` is the number to turn if a weaker
machine ever reverses it, and `NearWoodDensityPercent` is the second.

### The rule this leaves

**Anything fixed to the grid belongs in the chunk mesher, not in a per-frame draw.** A bucket is
one instanced call of one (module, part, tint) in one chunk, and it inherits frustum culling,
slice culling and the dirty-chunk rebuild for nothing — so the per-cell work happens when the
world changes rather than sixty times a second. The crop was always drawn this way and was always
free; the zone cover was not, and was the whole cost of a field.
`GrowingRenderTests.ABiggerFieldAddsInstancesRatherThanDraws` is the guard: it fails the moment a
zone costs draws in proportion to its cells.

### 6c.1 The mark pass, measured — and the constant's limit

**2026-09-20, the next day.** `DrawCellMark` and `DrawCellSlab` drew standing orders one
`Graphics.RenderMesh` per designated cell and incremented no counter: the last instance of P10 on
the list above. It is fixed — gathered by colour and flushed as one `RenderMeshInstanced` per
colour, the same shape the seed specks use — and **the measurement that justified it says
something the constant above did not predict.**

The instrument is `FrameTimeTests.TheMarkPassCostsWhatItSubmits`: one meadow, timed three times
seconds apart — bare, then with 901 mine orders standing, then with the same 901 plates instanced.
`ChunkRenderer.InstanceCellPlates` is the control, and it is a control rather than a second code
path: the geometry has one owner in `GatherCellPlate` and only the submission changes.

| 901 standing orders | frame | submit | tick | draw calls |
|---|---|---|---|---|
| none | 3.04 ms | 2.322 ms | 0.012 ms | 1,243 |
| one submission a cell | 3.44 ms | 2.701 ms | 0.017 ms | 2,144 |
| instanced by colour | 3.35 ms | 2.614 ms | 0.016 ms | 1,245 |

**So the whole pass is 0.40 ms with 901 orders on the board, and dropping 899 submissions
recovers 0.09 ms of it — about 0.1 us a submission.** The control understates the old path
slightly, because it reuses one `RenderParams` per colour where the old code built one and looked
up a material per cell; that difference was not measured separately and is bounded by a
dictionary lookup times 901.

**What that does to the 4.6 us constant.** It is not a toll on `Graphics.RenderMesh`. Both
readings it came from — the zone cover at 2,065 calls for 3.67 ms and the surround at 2.9 us a
batch — submitted a **real mesh**: the ground module drawn over itself, translucent and full
tile, and a batch of trees. A mark is a twelve-triangle cube in a cached material, and it costs
a twentieth of that. Read the constant as *what a loaded submission costs*, an upper bound worth
reaching for when a pass is slow, and not as a per-call price that makes any per-cell loop
expensive by arithmetic. The zone cover's 3.67 ms is therefore **most likely its translucent
full-tile fill rather than its call count**, and that has not been measured — the pass no longer
exists to measure.

**The batching is kept even so**, for three reasons that are not the 0.09 ms: the pass is now
*counted*, which was the half of P10 that hid it (a field reported 1,782 draw calls while
issuing 3,847); the cost stays flat as a player marks more, where the old one grew with the
board; and a quarry of five thousand cells is the same code.

**And the first attempt at this measurement read the pass as free, because it never ran.**
The order case walked the board row-major from `z = 1` and designated the topmost solid cell of
each column — 901 orders placed, published and counted, all of them outside the band
`DrawStandingOrders` filters to (the drawn slice was 8..15; that strip of ground is lower). It
measured 4.85 ms against the bare meadow's 4.86 and looked exactly like a pass that costs
nothing. `CellPlatesDrawn` exists for that reason: the case asserts that at least one plate was
drawn before it believes its own difference.

### 6c.2 What a colony costs, and the O(N squared) in the crowd

**2026-09-20, from a Play report.** The owner watched the developer overlay while spawning
colonists: *"it seemed to hover 1.7 ms no matter the colony size but then frames dropped after so
many colonists. I think at pretty high numbers."* Flat then a knee has several possible causes and
none of them was worth guessing, so `FrameTimeTests.TheFrameAgainstColonySize` measures eight
colony sizes in one world, seconds apart, with the draw block split by section.

| pawns | figures | frame | tick | submit | World | Figures | Actors |
|---|---|---|---|---|---|---|---|
| 8 | 8 | 2.65 | 0.009 | 2.023 | 1.909 | 0.088 | 0.017 |
| 32 | 32 | 3.14 | 0.013 | 2.368 | 1.905 | 0.435 | 0.019 |
| 64 | 64 | 4.08 | 0.021 | 3.139 | 1.935 | 1.174 | 0.019 |
| 96 | 64 | 4.84 | 0.032 | 3.842 | 1.993 | 1.429 | 0.409 |
| 128 | 64 | 5.70 | 0.041 | 4.692 | 1.985 | 1.710 | 0.985 |
| 192 | 64 | 8.56 | 0.081 | 7.361 | 2.201 | 2.369 | 2.777 |
| 256 | 64 | 11.98 | 0.131 | 10.672 | 2.180 | 3.058 | 5.416 |
| 384 | 64 | 22.45 | 0.310 | 20.691 | 2.486 | 4.908 | 13.275 |

Draw calls across that whole range: **1,243 to 1,324.** Everything else in the frame — Mirror,
Sight, Audio, Doors, Overlays — stays under 0.02 ms throughout.

**Three things fall out, and the first two are the owner's observation explained.**

- **`World` is flat.** The board, its chunks and the surround cost 1.9 to 2.5 ms whatever the
  colony is doing. That is the number that hovers.
- **It is not the simulation and it is not the submissions.** The tick is **0.31 ms at 384 pawns**,
  1.4% of the frame, which confirms OQ-19 at four times its colony size. Draw calls move by 7%
  across a 48-fold colony.
- **It is `Actors` and `Figures`, and they share one cause.** Both call `PawnPose.Of`, and
  `PawnPose.Of` **scans every other pawn** to find who to sidestep (`SteeringCurve.Proximity`
  against each). Figures are capped at `FigureCeiling` = 64, so that pass is 64 x N — linear, and
  it measures linear. Actors is every pawn without a figure, so it is (N-64) x N — quadratic, and
  it measures quadratic: 147,456 pairs at 384 pawns, each with a `Vector3.Distance`, which at
  about 90 ns a pair is 13 ms against the 13.275 measured.
- **Confirmed again 2026-09-23, from a sweep that was measuring something else** (PR #168, the
  modular colonists). `Actors` went 0.027 ms at 64 figures to **17.208 ms at 384**, with the frame at
  4.04 and 30.35 ms — while draw calls moved 1,125 → 1,152 and the hair-and-beard pass beside it,
  measured against a control in the same run, cost a flat **0.05 ms at both 64 and 192**. So the
  growth is neither submission nor the newest per-pawn pass, and the quadratic model above holds at
  ~117 ns a pair on a busier machine. **The prompt for whoever picks this up is
  `docs/plans/pf-crowd-scan.md`**, including the reason the fix can be exact: `CrowdFarRadius` is
  3.0 m, `Proximity` returns zero beyond it, so every pair the scan discards contributes nothing and
  a 3 m cull is bit-identical.

**The knee the owner saw is the figure ceiling**, not because the ceiling is wrong but because
crossing it is where the quadratic term starts: below 64 there are no stand-ins and the only crowd
scan is the linear one.

**The fix is exact, not an approximation, and that is the point worth carrying.**
`SteeringCurve.CrowdFarRadius` is 3.0 m and a cell is 2.5 m, so `Proximity` returns **zero** for
any pawn more than about one cell away — every one of those 147,000 pairs contributes nothing and
is computed anyway. A cell-bucketed index over the pawn span, built once a frame and shared by
both callers, makes the pass O(N x k) and **cannot change a single drawn position**, so the
sidestep the owner has already judged (`25-pawn-steering.md`) is not up for re-judgement. It is
not done: it is the next unit, and the sweep above is its before.

**At the scale target it is not yet a problem.** Fifty colonists is under the ceiling and the
whole frame is about 3.5 ms. This is a ceiling on how big a colony may get, discovered four years
before it binds, and worth fixing because the fix is cheap and provably invisible.

> **Fixed 2026-09-23 — §6c.9 below, and `docs/design/25-pawn-steering.md` §9.** The sweep in this
> table stands as the before it was taken as, but note it was measured beside two other editors;
> §9a is the same sweep on a clear machine and is the number to quote.

### 6c.3 The decoration, measured — the surround is 45 per cent of the meadow and the tufts are 7

**2026-09-21.** The owner, watching the game rather than a test: *"it seems that grass tufts and
surrounding land have some impact of the FPS — is there anything we can explore investigate to
improve or handle performance, any pre warming of shaders, caching or something that would help."*

Half of that was already answerable and the project could not answer it, which is the finding
behind the finding. **The surround was charged to `FrameSection.World` along with the chunk
buckets**, because it is submitted from inside `ChunkRenderer.Render` — deliberately, so the depth
buffer can reject it before the board is drawn — and nothing bracketed it. So the one pass §6c
spent a day cutting from 3.65 ms to about 2.2 was the one pass no instrument could name
afterwards. The tufts were worse off again: they are meshed into the chunks, so they have never had
a number of their own at all.

**Four instruments, and they are the deliverable.**

| Instrument | What it answers |
|---|---|
| `FrameSection.Surround` | the land beyond the board, split out of `World` |
| `OdysseyBootstrap.GpuFrameMs` / `CpuFrameMs` | is this frame CPU-bound or GPU-bound — see below |
| Two overlay lines: `cpu … gpu … <resolution>` and `submit split:` | both of the above, at the owner's own resolution, in a real session |
| `FrameTimeTests.TheDecorationAgainstTheFrame` | one world timed four ways, controls inside one run |

### The numbers

Played meadow, 120 × 120 × 16, no orders, 640 × 480 on an RTX 5070 Ti, four readings of the **same
built world** in one run — the only way a figure from this machine is worth anything (§6c).

| Case | Frame | Draw calls | Instances |
|---|---|---|---|
| As shipped | **2.71 ms** | 1,473 | 41,913 |
| Tufts off | 2.52 ms (**−0.18**) | 1,279 | 35,421 |
| Surround off | 1.48 ms (**−1.23**) | 1,197 | 23,983 |
| Neither | 1.29 ms (**−1.41**) | 1,003 | 17,491 |

**The surround is 45 per cent of the frame on the meadow and the tufts are 7.** Together they are
more than half of it. The owner named both and one of them is nearly seven times the other.

Three things follow the split across the other arms in the same run, and each is worth stating:

- **The surround is a flat tax, not a scaling one.** It reads 1.14–1.24 ms at every colony size
  from 8 pawns to 384, where `Figures` goes 0.09 → 8.65 and `Actors` 0.02 → 15.41. It is the
  largest single item in the draw block on the standard board until about thirty colonists.
- **It barely grows with the board**: 1.07 ms on standard, 1.78 on large, 2.05 on huge, against
  `World` going 0.92 → 2.17 → 4.08 over the same three. It scales with the *ring*, which is why
  a bigger board does not buy proportionally more of it.
- **The city pays 0.058 ms**, because the ruined city grows no wood outside it. The surround's cost
  is the trees and nothing else, which §6c had already established by subtraction and this now
  shows directly.

### Where this measurement stops, and it stops early

Every figure above is **a stopwatch around CPU submission at 640 × 480**. It cannot see fill,
overdraw or the shadow pass. That matters more here than anywhere else in this document, because
both suspects are alpha-tested foliage covering the horizon — precisely the geometry §6c predicted
would be free at 307k pixels and dominant at 1080p. **A tuft reading of "7 per cent" is a statement
about submission and may be wrong by an order of magnitude about what the owner is actually
watching.** The ranking could invert at play resolution: the surround is CPU-side batch overhead,
which barely moves with resolution, while the tufts are pixels, which move with its square.

That is what `GpuFrameMs` is for, and why `enableFrameTimingStats` is on in the player settings
since this date. **The rule for reading the overlay: if the GPU figure is at or above the frame
time, the frame is fill-bound and no amount of batching will move it. If it is well under, the cost
is on this side of the bus and `submit split:` says which pass.**

### What to do, ranked

Nothing here is started; the phase gate holds. In order of what the measurement supports:

1. **Decide which side of the bus the loss is on, at the owner's resolution.** One Play session,
   backtick for the overlay, read `cpu` against `gpu`. Then the render-scale rung in the Graphics
   tab is the confirming experiment: if halving it recovers the frame, it is fill; if it does not,
   it is submission. Both switches already ship. **This costs no code and it decides everything
   below**, so nothing below should be built first.
2. **If it is fill:** the tufts want a draw distance, and the hook is already there —
   `ChunkRenderer.FoliageDrawDistance` is a per-chunk cutoff against the viewer that already
   works, was already measured at 110 m (116 draw calls and 23,000 instances down to 89 and
   20,400, `MeadowCheck`, 2026-09-16) and ships as `PositiveInfinity` because the fault it was
   written for turned out to be the outline shader. It is a lever waiting to be turned, not a
   feature to build. A tuft is sub-pixel at forty metres and there is no reason to shade one. The surround's near wood wants the same
   treatment or impostors, and `NearWoodDensityPercent` is the crude version of it that is already
   wired.
3. **If it is submission:** the surround is near its measured floor. §6c found the cost tracked the
   *batch* count and coarsening `TreeSectorMetres` from 80 m to 400 m took it 3.65 → 2.2 ms, and
   that past 400 m it saturates at about 204 batches because the variants, themes, mute steps and
   parts cannot merge. The remaining honest lever is **fewer kinds of tree far away** — the same
   argument `FarTreeVariants` already makes at 4 — applied to the near ring, where sixteen kinds
   are being told apart by nobody.
4. **The tufts' 194 draw calls are worth a look whichever way it goes.** They are 13 per cent of
   the meadow's calls for 7 per cent of its frame, and they are per (chunk, variant, tint). Fewer
   tuft *variants* would collapse them; that is a look decision, not a performance one, and it
   should be made by eye.

### On pre-warming shaders, which was asked and is a different question

**Shader warm-up fixes hitches, not frame rate.** A variant compiles the first time it is drawn;
what that costs is one stalled frame, once, and then nothing for the rest of the session. It cannot
be what a steady-state readout is showing, and warming every variant at load would move the stall
into the loading screen rather than remove it. Worth doing for the stutter on its own terms — this
project has already been bitten three times by the neighbouring problem of variants being *stripped*
(`ShaderInclusion`, `InstancingKeepAlive`, `SyntyInstancingKeepAlive`, `docs/lessons.md`), and a
`ShaderVariantCollection` is the natural companion to those — but it is not on the path to this
report and should not be sold as if it were.

**Caching is already done, and that is worth saying plainly rather than re-proposing it.** The
surround is built once at startup and submitted unchanged every frame; its instance arrays are
filled and never refilled, and nothing can dirty them because no simulation stands behind them. The
tufts are baked into chunk meshes and re-meshed only when the mirror says a chunk changed. There is
no per-frame rebuild anywhere in either pass to eliminate. **What is left is the submission itself
and the pixels it costs**, which is why the two numbers above are the ones that matter.

### 6c.4 The surround, halved — and the constant that was guarding the wrong factor

**2026-09-21, the same day, after the owner said to focus on it.** §6c.3 found the surround was 45
per cent of the meadow's frame. This is what was done about it: **1.08 ms → 0.58 ms with every one
of the 3,907 trees still standing**, and the meadow's whole frame 2.71 → 2.14 ms.

### The census, which is what made it findable

§6c cut this pass once, from 760 batches to 266, by coarsening the spatial half of the batch key
from 80 m to 400 m. It then recorded that the ladder saturates past 400 m and that what was left
was "the variants, themes, mute steps and parts, which no sector size can merge". **The first half
was right; the second was believed rather than measured, and it named the right factor for the
wrong reason.**

A census of the three batch lists on the played meadow:

| List | Batches | Instances | Mean | Thin (<32) |
|---|---|---|---|---|
| Ground | 24 | 12,832 | 534.7 | 0 |
| Tufts | 12 | 1,191 | 99.3 | 3 |
| **Trees** | **230** | **3,907** | **17.0** | **192** |

The wood was 230 of the 266 batches, at seventeen trees a draw call, with five in six of them
holding fewer than thirty-two. And the key those 230 came from was
**115 sectors × 4 mutes × 1 part × 2 tints × 16 themes**. Four mute steps, two tints, one part: the
three factors the earlier note blamed were not splitting anything. **`SectorOf` folds the variant
into the sector number**, so the 115 "sectors" are spatial cells multiplied by tree kinds, and a
sixteen-kind wood cannot fall below sixteen batches per spatial cell however coarse the cells get.
That is why the sector ladder saturated, and it is the whole explanation.

### The sweep

One built world, rebuilt only in the skirt, six readings in one run
(`FrameTimeTests.TheSurroundSectorSweep`) — the rule every frame number off this machine is subject
to. Surround section in milliseconds:

| Sectors (near/far) | Kinds | Batches | Surround | Frame |
|---|---|---|---|---|
| 400 / 800 (shipped to today) | 16 | 266 | 1.080 | 2.71 |
| 800 / 1600 | 16 | 230 | 0.952 | 2.62 |
| 1600 / 3200 | 16 | 230 | 0.935 | 2.57 |
| **800 / 1600 (shipped)** | **8** | **151** | **0.576** | **2.14** |
| 800 / 1600 | 6 | 129 | 0.483 | 2.04 |
| 800 / 1600 | 4 | 104 | 0.371 | 1.92 |

**Space was the cheap half and it is now spent**: one step from 400 to 800 m takes all of it, and
1600 m and a single 100 km sector both measure identically to 800. **The kinds are where the rest
is**, and they go on paying all the way down. The cost tracks the batch count throughout — 4.06 µs
a batch at 266 and 3.81 at 104 — which is the 4.6 µs constant behaving exactly as §6c says a
*loaded* submission does.

### What shipped, and why 8 rather than 4

`TerrainSkirt.DefaultTreeSectorMetres` 400 → **800**, `DefaultFarTreeSectorMetres` 800 → **1600**,
`DefaultTreeVariantSlots` 16 → **8**.

| Board | Surround was | Surround is | Frame was | Frame is |
|---|---|---|---|---|
| Standard 120² | 1.08 ms | **0.58** | 2.71 ms | **2.14** |
| Large 180² | 1.78 ms | **0.60** | 5.57 ms | **3.92** |
| Huge 240² | 2.05 ms | **0.63** | 8.07 ms | **6.09** |

The surround is now **flat at about 0.6 ms on every board**, where it used to grow with the ring.
Huge gains the most in absolute terms, which matters because Huge is the board §28 measured as over
budget.

**Four is available, measured and cheaper again, and was not taken.** The reason is what a slot
actually is. A slot is a (module, theme) pair sampled from the board's own wood by frequency, and
the census finds the meadow's surround using **2 tints and 16 themes** — so sixteen slots were
buying sixteen colour palettes over two silhouettes, not sixteen kinds of tree. Halving them halves
the palettes and leaves the silhouettes alone, which is a change that ought to be invisible.
Quartering them might not be. That is a judgement for an eye on the horizon, not another reading,
so 8 ships and 4 waits for a verdict.

### The guard

`SurroundCostTests.HalvingTheVariantsHalvesTheWoodsBatchesAndNotTheWood` builds one board twice,
differing in the slot count alone, and fails if the wood changes or the batches do not. It guards
the *factor*, not the number: what it catches is somebody taking the variant back out of the key's
cost, and that saving would otherwise go silently, because **nothing else in either tier can see a
batch count**. `TerrainSkirt.CensusOf` and `KeySpreadOf` are the instruments behind it and are
worth reaching for before any further guess about this pass.

### What this does not answer

The same caveat as §6c.3, undiminished: all of it is CPU submission at 640 × 480. Halving the
batch count halves per-call overhead and does nothing whatever for fill, so if the owner's report
turns out to be GPU-bound at play resolution this work will have moved a number they were not
watching. **It was still worth doing unconditionally** — 0.5 ms off every board, on the CPU side,
costs nothing and is nobody's trade — but the Play session with the GPU readout is still the next
step and still decides what comes after.

### 6c.5 4K, measured on the owner's machine — and the answer to the question §6c left open

**2026-09-21, three screenshots from a real Play session.** Every number in §6c, §6c.3 and §6c.4 is
640 × 480 in a batch runner, and each of them says so and says it may not transfer. This is the
first reading at a play resolution, and it is **3840 × 2160 — 27 times the pixels**.

| | Shot 1 | Shot 2 | Shot 3 |
|---|---|---|---|
| frame | 16.79 ms (60 fps) | 15.66 ms (64) | ~17.5 ms (57) |
| **gpu** | **8.40 ms** | **8.15** | **9.12** |
| submit | 5.55 ms | 5.11 | 5.74 |
| tick | 0.19 ms | 0.20 | 0.20 |
| World | 4.46 ms | 4.09 | 4.66 |
| **Surround** | **0.98 ms** | **0.91** | **0.97** |
| draw calls | 3,747 | 3,747 | 3,748 |
| chunks | 413 | 413 | 413 |

### What it settles

**The surround is no longer the problem, and the §6c.4 work is why.** It is about **0.95 ms of a
16 ms frame — six per cent** — where before that work it was 45 per cent of a 2.7 ms frame. It does
not scale with resolution, which is the expected shape: it is per-call overhead and there are the
same number of calls whatever the pixels.

**The GPU is now the largest single item: about 8.5 ms.** §6c predicted this and could not test it
— "1080p is 6.75× the pixels; the alpha-tested foliage that covers the horizon is exactly the kind
of geometry whose cost is invisible at 640 × 480 and dominant at 1080p." At 4K it is 8.5 ms against
5.5 of CPU submission. **The prediction was right and the axis is real**, so the tuft question
§6c.3 could not answer is still live and is now the one worth answering: the tufts were 7 per cent
of a CPU frame and they are pixels, not calls.

**And `World` is the CPU term that is left**: 4.1–4.7 ms of the 5.1–5.7 ms submit, four to five
times the surround, across 3,747 draw calls and 413 chunks. Whatever comes next on this side of the
bus is the chunk buckets, not the decoration. `claude/frustum-culling` already exists and is
measured (`docs/design/`, `odyssey-bigmaps`), which is the obvious first thing to weigh against it.

### Read the frame time with vsync in view, or do not read it at all

8.40 + 5.55 does not make 16.79, and the gap is the point. A frame sitting at 16.7 ms with 8.4 ms
of GPU and 5.5 ms of submit inside it is a frame that is **waiting**, and 60/64/57 fps across three
shots is the shape of a frame paced by a display rather than by work. **So the fps number in these
shots is not evidence of headroom in either direction** — it hides how much is spare and it hides
what the work actually costs. The overlay now prints `vsync` and the frame `cap` beside the GPU
figure for exactly this reason; a reading taken without them is not comparable with anything.

### The CPU figure was wrong and has been removed

`CpuFrameMs`, added the same day off `FrameTiming.cpuFrameTime`, **failed in its first real
session**: 16.81 ms beside a 16.79 ms frame in shot 1, which is right, then **296.32** in shot 2 and
**17,898.04** in shot 3 — climbing over about twenty-five seconds, so a stream of bad samples rather
than one spike decaying out of an average. It is deleted rather than repaired, because nothing is
lost: `frame` and `submit` are this class's own stopwatches, they agree with each other, and
between them they say what a CPU figure would have. `GpuFrameMs` is kept — it is the one number
nothing else here can get, and the same three shots show it steady and plausible — and it is now
guarded against implausible samples.

**The lesson is the general one and is worth more than the figure was.** A number the platform
hands over is not a measurement until it has been seen beside a number taken independently. This
one shipped on the strength of looking plausible in a batch run at 640 × 480, in the very document
that warns that 640 × 480 proves nothing.

### 6c.6 The stutter, found: chunk meshing has no per-frame budget

**2026-09-21, from a traced player session on the Huge board at 3840 x 2160.** The owner had been
reporting hitches all day. They are **`ChunkRenderer.BatchFor` meshing every stale chunk it meets,
in one frame, however many that is.**

```csharp
if (batch.Version != _model.Version)
{
    _mesher.Mesh(batch, chunkIndex);      // no budget, no deferral, no limit
    ChunksMeshedThisFrame++;
}
```

### The measurement

Eighty-seven seconds of play, per-second rows, stalls counted as frames over 100 ms:

| chunks meshed in the second | seconds | stalls | stalls a second |
|---|---|---|---|
| **0** | 56 | **0** | 0.00 |
| 150+ | 11 | 36 | 3.3 |
| 1,725 | 1 | 22 | — |
| 2,507 | 1 | 38 | — |

**Fifty-six seconds with no meshing produced no stall at all.** Every stall in the session fell in a
second where meshing ran, and the rate tracks the meshing rate. The frame-level confirmation is a
single record: **a 180 ms frame that meshed 900 chunks**.

`submit_max` reached **200 ms** — the draw block itself, which is where meshing happens.

### Why it took four wrong answers to get here

Recorded because the route matters more than the destination. The stalls were blamed on the editor,
then the collector, then shader compilation, then the tick, and each survived longer than it should
because **the trace was reporting summaries of events** (`docs/bug-patterns.md` P14): `remeshed` was
last-seen so it read 0 through a second that meshed 800; the tick had a median and no maximum; the
phases had a mean and a p95 and no maximum. Each fix to the instrument moved the answer.

Two eliminations that now stand on good evidence, and are worth keeping:

- **Not the simulation.** `tick_max` 4.90 ms and the worst tick phase 3.58 ms across the whole
  session, against frames of 180–439 ms. Game speed alone provokes nothing: a stretch at 3x with the
  camera still produced **zero** stalls.
- **Not the collector.** Zero of the captured stalls had a collection on their frame, in three
  separate sessions, including one where 21 collections a second coincided with a worst frame of
  19 ms.

### What to do about it

**A per-frame meshing budget**: mesh at most N chunks — or M milliseconds — a frame, nearest to the
camera first, and let the rest arrive over the following frames. A chunk one frame late while
panning is invisible; a 200 ms stall is not.

It is the project's own standing rule being broken: *presentation per-frame work scales with what is
visible*, and this scales with **what became stale**, which a camera sweep or any `Model.Remesh()`
makes unbounded. A full re-mesh — which every non-ladder graphics toggle triggers — dirties all 800
chunks of a Huge board at once.

**Not built.** It is a renderer change with a visible trade (briefly unmeshed chunks while panning)
and wants its own unit, its own design note and the owner's eye.

### 6c.7 The meshing budget — the fix for §6c.6

**Decided 2026-09-21.** §6c.6 found the stutter: `ChunkRenderer.BatchFor` meshes every stale chunk
the draw loop touches, in that frame, unbudgeted. This is what is done about it.

### The rule

**A frame meshes at most `MeshBudgetPerFrame` chunks. A chunk that misses the budget draws whatever
geometry it already has and is retried next frame.**

The retry needs no queue. A deferred chunk still has `batch.Version != _model.Version`, so the next
frame's walk finds it again — the staleness *is* the queue, and adding a second list of owed chunks
would be a copy of state the batch already holds.

### What a deferred chunk looks like

| Case | Geometry it has | What the player sees |
|---|---|---|
| Re-meshed (a `Model.Remesh()`, an edit nearby) | the previous mesh, still valid | the world one to three frames out of date |
| Never meshed (panned into unseen map) | none — `InstanceCount == 0`, so the draw loop skips it | the chunk arrives a frame or two late |

Both are invisible at 150 fps and neither is a 165 ms freeze. **That is the whole trade**, and it is
the right way round: a chunk two frames late is 13 ms of being slightly wrong; the alternative is a
sixth of a second of nothing at all.

### The one exception: building a world

On a new game every chunk is never-meshed, and budgeting that would dribble the board in over
several hundred frames while the player watches. `PrimeAll()` meshes the lot without a budget, and
the composition root calls it once while the loading screen is up — **where a freeze is expected and
where §6c.6 already measured 14.7 seconds of one**. Keep the stall where the player is already
waiting; budget everything after it.

### Choosing the number

§6c.6 measured a 900-chunk re-mesh at about 165 ms, so a chunk costs roughly **0.18 ms**. Against
the 5 ms frame budget, a meshing frame should not spend more than about 2 ms on it, which is **11
chunks**. The number ships as a measured constant rather than a guess: `MeshBudgetTests` asserts the
budget is obeyed, and a frame measurement sets the value.

A full board re-mesh then costs about 82 frames — **half a second at 150 fps**, spread, against 165
ms in one lump. The total work is unchanged; only its distribution is.

### Measured, with a control in one run

`FrameTimeTests.TheMeshBudgetKeepsAWholeBoardRemeshOutOfOneFrame` makes the same Huge board
re-mesh twice, once with the budget off and once with it on, and quotes both:

    unbudgeted  156.1 ms  (900 chunks)
    budgeted      8.8 ms  (11 chunks, cap 11; 889 deferred)

**156 ms to 9.** It reproduces the exact signature the player session caught — 900 chunks, about
160 ms — which is the best evidence that the arm is measuring the real fault and not a proxy for it.
No absolute threshold is asserted, for the reason §6c gives: a frame number off this machine is only
comparable with one taken in the same run.

### Played, and judged (2026-09-21)

The owner played the rebuilt player on Huge at 3840 x 2160. **116 seconds, 14,112 frames after the
load, 35,678 chunks meshed:**

| | seconds | frames | over 33 ms | share | over 50 ms |
|---|---|---|---|---|---|
| meshing | 47 | 4,958 | 1 | **0.02%** | **0** |
| quiet | 65 | 9,154 | 2 | **0.02%** | 1 |

**Zero frames over 100 ms after the load**, p50 7.05 ms, and the busiest second meshed **1,186
chunks** for a worst frame of 12.7. The relationship that defined the fault — meshing seconds stall,
quiet seconds do not — is gone: the two bands are identical, and the session's only frame over 50 ms
fell in a *quiet* second, so it is not this at all. Against the same board before the fix: 3.77 per
cent of frames over 33 ms in meshing seconds, against 0.00 in quiet ones.

**And the trade was judged and cost nothing.** The one thing no measurement could answer was whether
a player can see the board arriving eleven chunks at a time. The owner's verdict: *"The look is
fine."* So eleven stands, and the number is now a judged figure rather than an arithmetic one.

### What it does not fix

- **Worldgen.** The 439 ms frame at session start is the world being built, and `PrimeAll` keeps it
  there deliberately.
- **The cost of meshing itself.** This spreads it; it does not make a chunk cheaper. If 0.18 ms a
  chunk ever becomes the complaint, that is a mesher change and a different unit.

### Still outstanding

**Nothing in this section is measured at the resolution the game will be played at.** See below.

### What has NOT been measured, which is most of the question

Every number above is **640 x 480 on an RTX 5070 Ti**. The stated target is a 2022 mid-range
laptop at a real resolution. So these findings are sound about *what this renderer does wrong*
and close to worthless as a prediction of what it will cost a player.

- **Play resolution.** 640 x 480 is 307k pixels. That is precisely why "a submission costs
  4.6 us and fill costs nothing" was the right diagnosis *there* — and it is the reason the
  conclusion may not transfer. 1080p is 6.75x the pixels; the alpha-tested foliage that covers
  the horizon is exactly the kind of geometry whose cost is invisible at 640 x 480 and dominant
  at 1080p. An attempt to measure it in batch (2026-09-20) failed and is worth recording so
  nobody repeats it: the batch game view ignores `-screen-width`, and forcing a camera
  `targetTexture` instead costs so much itself that the control case went from 2.0 ms to 16 ms.
  The measurement has to come from the on-screen readout in a real Play session.
- **Target hardware.** Per-draw overhead is largely CPU and driver, so a weaker CPU makes the
  batching work matter *more*; fill makes a weaker GPU matter more at the same time. The two
  pull in opposite directions and neither has been measured.
- **Colony scale.** The frame cases run the scenario's own small colony. Pawns, items, buildings
  and standing orders all add per-frame work, and the order marks add it per designated cell
  (see above).
- **A long session.** 180 frames is a snapshot. Nothing here says what an hour of play does.

The cheapest way to turn all four into knowledge is the developer overlay in a real Play session
at the owner's resolution. Until then the honest statement is: the faults that were found are
fixed and guarded, and the budget is unverified on the hardware it was written for.

### And the budget is not enforced

`FrameTimeTests` asserts only that a frame is under a 30 Hz tick. The 5 ms budget lives in the
documents, so **a green PlayMode run says nothing about it** — read the printed numbers, not the
pass. That has always been true and is not a growing-zones matter.

### 6c.8 One edit re-meshed the whole board, and the seconds that are still unexplained

The owner reported *"about a second or 3 delay when the object appears when it's built, IE door,
walls etc — sometimes a little glitch and it appears"*. `BuildAppearanceTests` was written to
measure it in a real player loop rather than reason about the seam, and it found two things.

**The publish seam is innocent.** A wall raised in a live session is in the render mirror on the
**next tick's publish** — one tick, a few milliseconds — and drawn on the frame after that. The
tick, the dirty chunk marks, the snapshot contributor and the mesher together cost one tick and one
frame. **In ticks, not frames**, which cost a red run to learn: the mirror is written by a snapshot
contributor, so a rig running frames faster than the fixed tick sees the wall arrive several frames
later without anything being slower. The same commit read 0 frames alone and 13 frames in a full
PlayMode run, with one tick of delay both times. Nothing there can account for
seconds, and nothing there has been changed.

**`WorldRenderModel.Version` was one number for the entire board.** Every `ChunkBatch` compared its
own version against it, so *any* cell changing anywhere invalidated *every* batch, and all 45 drawn
chunks of the meadow were re-meshed on the next frame. **This is the other half of §6c.6–6c.7 and
not a duplicate of it**: the budget caps how many chunks may be re-meshed in one frame, and this
caps how many are invalidated at all. With the budget alone a single wall still dirties 45 chunks
and spreads them over four frames of stale geometry; with both, it dirties three and they are
re-meshed inside one frame, well under the budget of eleven. Measured, with the contrast taken inside one
run, which is the only way a frame number on this machine means anything:

| | Chunks re-meshed by one wall | The frame after the raise | The frames either side of it |
|---|---|---|---|
| Before | 45 | **12.53 ms** | 0.68–0.85 ms |
| After | 3 | **1.73 ms** | 0.38–0.43 ms |

**Measured again with both in** (PlayMode on the merged branch, 2026-09-21): one wall is *in the
mirror one tick and 12.4 ms after the raise, drawn on the next frame, 3 chunks re-meshed*, and the
twelve frames following it are flat at 0.33–0.43 ms. The spike is not smaller, it is gone.

The fix is a version *per chunk* (`WorldRenderModel.ChunkVersion`), stamped by `RefreshDirty` on the
chunks it actually copied, with `RefreshAll` and `Remesh` writing the new version into every entry so
that "re-mesh everything" is still expressible. `Version` itself stays and still means "something
changed", which is what `DoorDirector` reads it for.

**What this makes load-bearing.** A global version quietly covered under-marking: a system that
dirtied too few chunks still got the right picture, because everything was re-meshed anyway. Now
**a cell edit must dirty every chunk whose mesh depends on it** — which for terrain means the 3×3×3
neighbourhood, because a face is drawn against what is beside it. `ConstructionGrid` and `MineJob`
both do (`MarkChunksAround`); the per-cell marks that remain — a felled tree, a crop's stage, a
zone's tint — are all things drawn inside their own cell. The symptom of getting this wrong is a
stale face at a chunk boundary that corrects itself the next time anything near it changes.

**The seconds are not explained by this**, and the leading candidate is outside the game: the editor
compiles shader variants asynchronously (`ProjectSettings/EditorSettings.asset`,
`m_AsyncShaderCompilation: 1`) and a wall is the first thing of its material a meadow ever draws.
A batch run cannot reproduce it — `ShaderUtil.allowAsyncCompilation` is false in batch mode, and the
probe recorded zero frames of compilation — so the next move is the owner flipping that one toggle
in a real editor session and saying whether the delay becomes a brief hitch.

### 6c.9 The crowd scan, fixed — and what the control says

**2026-09-23.** The O(N squared) above is closed. `PawnCrowdIndex` buckets every pawn's position
once a frame on a 3 m grid and `PawnPose.Of` asks the neighbourhood instead of the colony; the cull
is exact, so no drawn position moved and the sidestep the owner judged is untouched. The shape, the
alternatives and what not to undo are `docs/design/25-pawn-steering.md` §9; the pattern is P12 in
`docs/bug-patterns.md`, now marked fixed, and the testing lesson that fell out of it is the new P16.

**Two things in the measurement are worth carrying beyond this unit.**

**The control was three-valued, not a bool.** The plan (`docs/plans/pf-crowd-scan.md`) named two
candidates — hoisting `SteeringCurve.WhereItIsNow` out of the inner loop, and a spatial index — and
warned against building the second on top of an unmeasured first. Building the index subsumes the
hoist, so after the fact the two cannot be told apart. `CrowdScan.Cached` exists purely to keep them
apart in one run: it is the hoist alone. That is the shape to copy whenever an optimisation contains
a cheaper one.

**And the numbers here were taken on a clear machine, which took three attempts to get.** The first
queue raced its own EditMode run — a batch run that has printed its results can still be shutting
down (`docs/lessons.md`) — and the sweep quoted in §6c.2 above was taken beside two other editors.
The `Actors` figure at 384 moved from 13.3 ms (that sweep) to 15.8 ms (clear machine, unmodified
code, same commit family). **Neither is a baseline for the other**, and the only reason the two can
be read together at all is that the per-pair cost they imply — ~117 ns and ~128 ns — agrees.

## 7. Presentation is a reader

The rule that keeps this document honest, and the one that the architecture benchmark treats as a judged phase: **presentation never reads simulation objects and never mutates them.** It reads the immutable snapshot published by the simulation at tick end, keyed by stable handles, and it sends player actions back as intents on a queue consumed at a tick boundary (`docs/design/ui-plan-reconciliation.md`).

This is what makes the cut-away, the overlays and the instanced buckets all safe to rebuild on a frame boundary rather than a tick boundary, and it is what would let the tick move off the main thread later without rewriting the renderer.

## 8. Open questions

- Draw-call and frame-time numbers at full scale — the D3 performance pass, not yet run.
- GPU Resident Drawer versus explicit `RenderMeshInstanced` for chunk buckets.
- How many layers below the slice to draw by default, and the darkening curve.
- Whether ghosted-above should default to outline, transparent or hidden. A player setting either way; the default wants a real opinion from the look-check scene.
- Whether the fire and smoke effects from the Particle FX pack read well at this camera distance, and their cost per instance — 58 of their materials still use legacy particle shaders that need a URP check (`e-03-other-packs.md`).
