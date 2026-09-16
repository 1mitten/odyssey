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

## 7. Presentation is a reader

The rule that keeps this document honest, and the one that the architecture benchmark treats as a judged phase: **presentation never reads simulation objects and never mutates them.** It reads the immutable snapshot published by the simulation at tick end, keyed by stable handles, and it sends player actions back as intents on a queue consumed at a tick boundary (`docs/design/ui-plan-reconciliation.md`).

This is what makes the cut-away, the overlays and the instanced buckets all safe to rebuild on a frame boundary rather than a tick boundary, and it is what would let the tick move off the main thread later without rewriting the renderer.

## 8. Open questions

- Draw-call and frame-time numbers at full scale — the D3 performance pass, not yet run.
- GPU Resident Drawer versus explicit `RenderMeshInstanced` for chunk buckets.
- How many layers below the slice to draw by default, and the darkening curve.
- Whether ghosted-above should default to outline, transparent or hidden. A player setting either way; the default wants a real opinion from the look-check scene.
- Whether the fire and smoke effects from the Particle FX pack read well at this camera distance, and their cost per instance — 58 of their materials still use legacy particle shaders that need a URP check (`e-03-other-packs.md`).
