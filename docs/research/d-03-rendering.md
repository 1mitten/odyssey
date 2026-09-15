# Lane D3 — Rendering: Synty modules on the grid (visual half done; performance half pending)

## Question

Can the Synty modules produce the game's look on the confirmed 2.5 × 2.5 × 3.0 m grid — and (pending) what do instancing, chunk culling and the layer cut-away cost at city scale?

## Status

**Visual half done 2026-09-15.** `Assets/Editor/Odyssey/VisualBlockScene.cs` generates `Assets/Scenes/Spikes/VisualBlock.unity` (menu: *Odyssey → Spikes → Build visual block scene*, or `scripts/unity.sh exec Odyssey.EditorTools.VisualBlockScene.Build`): an intact three-storey shell and a half-collapsed shell from the `SM_Bld_Base_*` kit at exact cell coordinates, facade Sections and background towers as skyline, street props, vehicles, posed characters for scale, and fire/smoke FX on the ruin. Placement is bounds-snapped, so mixed prefab pivots land on the grid; missing prefab names are logged, not fatal (currently only the two rubble name guesses missed). The scene file holds GUID references only — no licensed content is committed; without the packs it generates as bare ground.

**Findings from building it:**

- The `SM_Bld_Base_*` kit assembles on the grid exactly as `e-01-module-mapping.md` predicted; walls, doors, windows, floors, ceilings and pillars need no per-piece fixups beyond bounds-snapping.
- The full-height stair (`SM_Bld_Base_Stairs_02`) needs raw-pivot placement (its skirt dips below the pivot; bounds-snapping misaligns it) — worldgen stamping must special-case stair pivots.
- `Wall_Destroyed_01/02` plus skipped segments and checkerboard-missing floors read convincingly as ruin without any bespoke damage assets.

**Performance half pending** (the measurable answers brief §5 D3 asks for): `RenderMeshInstanced`/GPU Resident Drawer over module meshes, chunk-level culling, cut-away at layer N (per-layer visibility vs clip plane), draw calls and frame time at 250 × 250 × 40 scale. Constraint imported from the UI session's plan: overlays render as per-chunk procedural meshes, never per-cell UI elements. Owner note: the cut-away must leave hidden layers **non-interactive** (Going Medieval's top complaint, `b-going-medieval.md`).

## Graphics target (owner directive, 2026-09-15)

The rendered game should sit **similar or close to the two concept renders** described in `docs/reference/screenshots/README.md`: Synty low-poly under URP with **cyan emissive trim** carrying night readability (confirmed available — `Synty/Generic_Basic` exposes `_Emission_Map`/`_Emission_Color`/`_Enable_Emission`, per `e-04-tint-strategy.md`), roofless/cut-open interiors at the current slice, a three-quarter mid-height camera, and floating panels that never hide the world. The performance half of this spike must therefore measure with emissive materials, real-time shadows and the cut-away enabled — matching the look is part of the budget, not an afterthought. (The concept's desert-outskirts setting is the *later* map type; the prototype map stays the ruined city.)

## Layer questions touched

Q9 (how the camera slices): the spike scene stages the geometry for judging slice/ghosting by eye; the display policy recommendation is ghosted-and-non-interactive above the slice.

## Sources

- `Assets/Editor/Odyssey/VisualBlockScene.cs`, `Assets/Scenes/Spikes/VisualBlock.unity`
- `docs/research/e-01-module-mapping.md`, `b-going-medieval.md`

## Confidence

High for the visual findings (built and saved headless, zero errors); the performance half is unmeasured.

## Could not be determined

Frame cost of anything, yet — that is the pending half. Whether the fire/smoke FX read well needs an eyeball in the editor (they were placed unsimulated).
