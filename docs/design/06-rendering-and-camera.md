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

- **The meadow continues, thinning into haze.** Not a framing ring of hills and not a bare plane:
  the same ground and the same wood carried outwards until linear fog has closed over it.
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

## 7. Presentation is a reader

The rule that keeps this document honest, and the one that the architecture benchmark treats as a judged phase: **presentation never reads simulation objects and never mutates them.** It reads the immutable snapshot published by the simulation at tick end, keyed by stable handles, and it sends player actions back as intents on a queue consumed at a tick boundary (`docs/design/ui-plan-reconciliation.md`).

This is what makes the cut-away, the overlays and the instanced buckets all safe to rebuild on a frame boundary rather than a tick boundary, and it is what would let the tick move off the main thread later without rewriting the renderer.

## 8. Open questions

- Draw-call and frame-time numbers at full scale — the D3 performance pass, not yet run.
- GPU Resident Drawer versus explicit `RenderMeshInstanced` for chunk buckets.
- How many layers below the slice to draw by default, and the darkening curve.
- Whether ghosted-above should default to outline, transparent or hidden. A player setting either way; the default wants a real opinion from the look-check scene.
- Whether the fire and smoke effects from the Particle FX pack read well at this camera distance, and their cost per instance — 58 of their materials still use legacy particle shaders that need a URP check (`e-03-other-packs.md`).
