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

Four conditions, each with a test, each ruling out something that would look wrong:

- **Empty, and standing on ground**, or the bank hangs in the air.
- **The step is earth.** A mined face and a quarry wall stay sheer; a grassy ramp growing out of cut
  rock is a lie about what was done to it.
- **The top of the step is open**, or this is the wall of a tunnel rather than a terrace.
- **The cell is open to the sky**, which keeps banks on the outdoor hillside and the inside of a
  working sharp-edged.

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
| At or above the surface (the layer the game opens at) | **Every layer, drawn solid.** No depth cap, no fade | `belowDepth` layers, dimmed |
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
