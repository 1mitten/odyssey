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
  working figure at a fixed distance from what it is working on, eased in by the same weight that
  eases in the swing, so it reads as setting oneself. Nothing else moves: the pawn is still in its
  cell for picking, for the cursor and for the whole simulation.
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

The axe itself is `ModuleIds.ToolAxe`, an ordinary catalogue row parented to the right hand for as
long as the work lasts. A clone without the packs resolves it to null and colonists fell trees
bare-handed, which is the same fallback every other piece of pack art has.

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
