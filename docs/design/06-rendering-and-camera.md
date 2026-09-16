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

**If the real terracing is ever wanted**, `surfaceRelief` is one line away in `MakeWooded`, and the
facade would then smooth its 3 m risers into slopes. It is deliberately not taken: it changes
worldgen output, the goldens, pathing and the start cell, and the look should be judged on the
change that risks nothing first.

## 3. The camera and the slice

**The camera** is a three-quarter orbit at a constrained pitch, matching the concept renders: pan across x/z, zoom, rotate in 90° steps or freely, and a vertical control that changes the **active layer** rather than the camera height.

**The slice model**, which is the whole point of the project:

| Layer relative to the slice | Rendered | Interactive |
|---|---|---|
| Above the active layer | Ghosted outline, heavily transparent, or hidden entirely (a player setting) | **Never** |
| The active layer | Fully, and with its ceiling slab suppressed so interiors are visible | Yes |
| 1 to N layers below | Fully, progressively darkened with depth | No |
| Deeper than N below | Not drawn | No |

Three decisions inside that table are deliberate:

1. **Layers above are never interactive.** Going Medieval's single most-reported complaint is misclicking something on another floor, with players reporting buildings deconstructed by accident (`b-going-medieval.md`). Ghosted geometry is a depth cue and nothing else; selection and designation raycasts stop at the active layer. This costs nothing to decide now and is unpleasant to retrofit.
2. **The active layer is drawn roofless.** The ceiling slab of the active layer is suppressed — exactly what the concept renders show, and the only way interiors read at all. The slab is still *there* in the simulation; this is purely a render decision.
3. **Layers below stay visible and darkened.** This is the depth cue that makes a hole in the floor legible as a hole rather than a black square, and it is what makes building above an occupied room comprehensible. N and the darkening curve are settings, because the right value is a matter of taste and screen size.

### 3a. The depth chooses the treatment (owner, 2026-09-16)

The table above is what a *mode* does. What a player actually gets, before they have chosen a mode,
now depends on how deep the slice is — `SliceSettings.followDepth`, on by default.

| Where the slice is | Above it | Below it |
|---|---|---|
| At or above the surface (the layer the game opens at) | **Every layer**, x-rayed and fading. No depth cap | `belowDepth` layers, dimmed |
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

**"Every layer above" is bounded by the fade, not by a count.** At the shipped tuning
(`ghostAlpha` 0.38, `ghostFalloff` 0.72) the alpha ramp runs 0.380, 0.274, 0.197, 0.142, 0.102 …
and crosses the 0.012 cutoff after eleven layers. `HighestVisibleLayer` returns that layer, and it
is the same constant the chunk loop skips on, so the two cannot drift apart.

**Measured cost, which is why the cap could come off at all.** On the 16-layer prototype board with
the surface at L11, "every layer above" is L12–L15 — four layers, which is exactly what the old
`aboveDepth` of 4 already drew. Underground at L6 the old range was L3–L10 and the new one is
L0–L7: eight layers either way. Deeper it gets *cheaper* — at L2 the old range was seven layers and
the new one is four. The change only costs anything on a map tall enough for the eleven-layer fade
bound to bite, and nothing on the board being played.

The six ADR 0006 modes are untouched and still ship. Switching `followDepth` off obeys every field
exactly as before, which is what choosing a mode explicitly does: the V key's first press pins
whatever is on screen and hands over control, and cycling past the last mode gives the default back.

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
- **The pose is laid over the clip, not in place of it.** The colonist goes on breathing.
- **It freezes when the game is paused.** There is no pause signal in the snapshot, so the director
  infers it from the tick standing still. Without that, a swinging colonist would be the only thing
  moving on a paused board, since a pawn that has stopped moving settles into the idle by itself.
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

## 7. Presentation is a reader

The rule that keeps this document honest, and the one that the architecture benchmark treats as a judged phase: **presentation never reads simulation objects and never mutates them.** It reads the immutable snapshot published by the simulation at tick end, keyed by stable handles, and it sends player actions back as intents on a queue consumed at a tick boundary (`docs/design/ui-plan-reconciliation.md`).

This is what makes the cut-away, the overlays and the instanced buckets all safe to rebuild on a frame boundary rather than a tick boundary, and it is what would let the tick move off the main thread later without rewriting the renderer.

## 8. Open questions

- Draw-call and frame-time numbers at full scale — the D3 performance pass, not yet run.
- GPU Resident Drawer versus explicit `RenderMeshInstanced` for chunk buckets.
- How many layers below the slice to draw by default, and the darkening curve.
- Whether ghosted-above should default to outline, transparent or hidden. A player setting either way; the default wants a real opinion from the look-check scene.
- Whether the fire and smoke effects from the Particle FX pack read well at this camera distance, and their cost per instance — 58 of their materials still use legacy particle shaders that need a URP check (`e-03-other-packs.md`).
