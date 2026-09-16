# The golden-hour look — owner interview

**Phase:** Interview (feature-level, in the shape of `mining-interview.md`). **Date:** 2026-09-16.
**Branch:** `main` (documents only; no code was written before or under this file).
**Conducted by:** Claude Code, ten questions in three rounds.

The owner's brief: *"Will it be possible to use at least the same lighting effects, rays, depth of
field techniques used in the game Station to Station? In particular how the light shines onto the
environment and the way the skyboxes look with warmth that blend into this environment."*

**Short answer: yes.** Every effect in the references is reachable on the pipeline we already run.
Grading, tonemapping, bloom, vignette and depth of field are URP's built-in post stack, which this
project has never switched on. Warm sky-into-fog is a colour and curve change to a sky shader and
a fog rig that already exist. Light shafts are the one thing URP does not ship and need a renderer
feature of our own, for which the outline pass is the pattern. The unknowns are cost at play
resolution and the exact Render Graph plumbing, which is what the research files answer.

## 1. What the code already had

Grounding first (survey of `PlayScene.cs`, `PC_RPAsset.asset`, `PC_Renderer.asset`, the three
custom shaders and the rendering docs).

| Piece | State on `main` at `f4b9df4` |
|---|---|
| Light rig | Built in code, `PlayScene.BuildLighting`; the screenshot harness uses the same builder. One directional sun at **72° elevation**, intensity 1.35, colour (1.0, 0.97, 0.90), soft shadows. A comment records that a 50° raking sun was tried and rejected for darkening the ground. |
| Ambient | Trilight, high-key (sky 0.56/0.61/0.68, equator 0.46/0.48/0.52, ground 0.28/0.28/0.30). No probes, no baked GI, no reflection probe in the scene. |
| Sky | Our own gradient shader, `Odyssey/GradientSky` on `Assets/Settings/OdysseySky.mat`: sky (0.36, 0.60, 0.86), horizon (0.76, 0.86, 0.91), no sun disc, no scattering. Colours rewritten on every scene build. |
| Fog | Linear, colour = the sky's horizon colour, **460 m → 1,100 m**, deliberately past the far corner of the 300 m board so it never touches the playfield; it exists to close the terrain skirt. |
| Camera | FOV 40, near 0.3, far 1,800 m. Default pitch 48°, at which the horizon is never in frame. |
| Post-processing | **None in effect.** No `Volume` in any scene or prefab. The URP asset's default volume profile GUID resolves to nothing in the repository, and `Assets/Settings/DefaultVolumeProfile.asset` is referenced by nothing and holds stray editor-test components. So: no tonemapping, no grading, no bloom, no DoF, no vignette. |
| Anti-aliasing | None. MSAA off, no FXAA/SMAA/TAA. |
| Renderer features | Forward+, SSAO (added by hand; `RenderSetup` does not reproduce it) and our outline pass (`OutlineFeature`, whose queue was chosen to stay out of a bloom that was never added). |
| Shaders | Water is fully URP-lit with per-material fog; ground and props are URP Lit or Synty's own, so post applies uniformly. Sky and outline are unlit. |
| Shadows | Distance 50 m, 4 cascades, 2048 map, soft quality high. |
| Prior research | `d-09-stylised-rendering.md` §3.4: grading first, Neutral tonemapping over ACES, bloom sparingly. `b-painted-look-games.md`: fog as the mood's backbone, one LUT-plus-vignette volume. |
| Budget | 5 ms submission budget; the meadow measures 0.99 ms with water, at 640 × 480, CPU submission time. No GPU figure at play resolution exists. |

So the honest position: the *lighting* is a considered daylight rig and the *post-processing* is
absent, not tuned. Most of the look is switching on and colouring what is already there.

## 2. The answers

| # | Question | Answer |
|---|---|---|
| 1 | Which of the layered effects matter? | **All four:** warm sky blending into fog; sun rays / light shafts; tilt-shift depth of field; bloom, grading and vignette. |
| 2 | Fixed lighting state or time of day? | **Fixed golden hour, always on.** No cycle, no weather presets for now. — **Reversed by the owner on 2026-09-16**, the same day it was built: seeing the fixed hour lit, they asked for a full cycle instead — blue by day, orange at dawn and dusk, dark at night. Not a misreading of the question; a decision made better by seeing the alternative. `Daylight` in the Presentation assembly is the keyed table, and every other answer in this file still stands. |
| 3 | When does depth of field apply? | **Subtle, always on** — a wide sharp band around the focus, blur above and below, readability kept. |
| 4 | Budget for the look | **Up to about 2 ms more, quality-tiered:** a Low tier drops rays and DoF on the 2022 laptop; the RTX machine gets the full look. |
| 5 | References | Six screenshots supplied from the owner's downloads, now in `docs/reference/screenshots/station-to-station/`, described in that folder's README. |
| 6 | The sky is not in frame at 48° pitch. Where does it show? | **Let the rig pitch down to about 15–20°** so the horizon and rays are earned at that angle; the default view gets the haze only. |
| 7 | How are the rays made? | **Write our own** screen-space light-shaft renderer feature. No store asset. |
| 8 | Low raking sun versus the recorded 72° decision | **Low sun, accept long shadows.** Overrides the comment in `PlayScene.BuildLighting`; shadows are to be lifted by ambient and grading rather than avoided. |
| 9 | Bloom versus `d-09` §3.4's caution | **Bloom as in the references.** `d-09` is amended with the reason when the design lands. |
| 10 | Phases | **Full phases with stops:** research files, then a design section and plan for approval, then execution. |

## 3. What the references show

Read from the six images (see the README for each one):

- The sun sits at roughly **25–35° elevation**. Shadows are long and soft-edged; trees and
  buildings throw shadows several times their footprint across the ground.
- **Lit faces are pushed warm and slightly over-exposed.** Bloom bleeds off sun-facing roofs,
  the sky at the horizon and the pale rock; it is visible, not subtle.
- **Shadows are lifted, never black,** and lean cool against the warm key. The playable ground
  stays readable under them.
- **A warm amber haze** lifts and desaturates everything with distance, and the sky at the
  horizon is that same colour, so hills and sky dissolve into each other. One image is almost
  entirely amber fog and still reads.
- **Light shafts** come through tree lines as crisp radial streaks from the sun's screen position.
- **A horizontal tilt-shift band**: sharp through the middle of the frame, blurred at top and
  bottom, which is what makes the world read as a model.
- A mild **vignette**, high **saturation**, and ambient occlusion under foliage and eaves.

## 4. Two decisions this overrides, and why

1. **Sun elevation.** `PlayScene.BuildLighting` chose 72° because a raking sun darkened the ground
   and threw long shadows across the readable surface. The owner has seen the references and
   chosen the raking sun anyway, on the grounds that the look *is* the sun angle; the darkening is
   to be solved by ambient lift, shadow strength and grading rather than by the sun. Readability
   under long shadows is a research question (`b-low-sun-readability.md`), not an assumption.
2. **Bloom.** `d-09-stylised-rendering.md` §3.4 advised bloom sparingly or not at all so the scene
   reads as illustration rather than game render. The references use it plainly and the owner
   wants that. The note stands as advice for a painted look; this look is a different target.

## 5. Open items carried to the Plan phase

- **Sun azimuth** relative to the default camera heading: backlit gives rays and rim light,
  front-lit gives warm faces; the references mix both.
- Whether the **tilt-shift band follows the active layer** (`SliceDirector`): focus at the
  slice's height would make DoF a layer cue as well as a look.
- The concept renders' **cyan-emissive night** bar in `d-03-rendering.md` is untouched by a
  fixed golden hour; the two bars now differ and the owner reconciles them later.
- The **dangling default volume profile** and the orphaned `DefaultVolumeProfile.asset`: the plan
  should replace both with one generated profile written by `RenderSetup`, never hand-edited, so
  the look is reproducible like the scenes. SSAO should be brought under the same command.
- **Measurement.** `FrameTimeTests` measures CPU submission at 640 × 480; post-processing cost
  scales with resolution, so a full-resolution GPU timing is needed before any budget claim.
- **Anti-aliasing** was never chosen. Bloom and DoF on an aliased image look worse than either
  alone; the plan should pick one (the research file weighs them).

## 6. Research commissioned

| File | Question |
|---|---|
| `d-12-urp-post-stack.md` | URP 17 post effects: cost, settings, tilt-shift via DoF, tonemapping, AA, quality-tier switching |
| `d-13-light-shafts.md` | Screen-space sun shafts as a Render Graph renderer feature: technique, plumbing, off-screen sun, cost |
| `d-14-aerial-perspective.md` | Warm distance/height fog matching the sky across Lit, water and skybox |
| `b-station-to-station.md` | How that game composes its look, from public sources |
| `b-low-sun-readability.md` | Keeping the ground readable under a raking sun; shadow distance and cascades |

All five came back on 2026-09-16 and are summarised a line each in `INDEX.md`. Four results change
what the plan can assume, and two of them change an answer above:

1. **The rays may be impossible at the default framing, and that is a measurement, not an opinion.**
   At a 48° downward pitch with a sun anywhere near overhead, the sun sits about 120° off the view
   direction — behind the camera, where a radial blur has nothing to radiate from. Bringing the sun
   down to 25–35° is necessary but may not be sufficient; the azimuth has to bring it towards the
   view. So **the sun azimuth is no longer an open aesthetic question, it is a constraint**, and the
   first thing the plan does is a two-slider framing experiment in `Play.unity` before a line of
   shader is written. If no framing works, billboard shafts are a different question.
2. **The tilt-shift is probably not depth of field.** URP's Gaussian blurs the far field only, so
   the near half of a pitched frame never blurs; Bokeh can make a band but needs dishonest optics
   and costs a real pass. A fullscreen pass that blurs by **screen Y** is exact, needs no depth
   texture, costs the same whatever the scene holds and tiers away trivially — and it is what the
   reference game itself appears to do. The plan should weigh it as the first option, not the
   fallback.
3. **The low sun was never the real problem; dark shadows were.** Shadow Strength below 1 keeps the
   key light's hue in shadow and costs nothing, which is the lever the 72° decision never tried.
   With gradient ambient and contrast slightly down, the raking sun is affordable. But the cascade
   splits are wrong for this camera today — they are fractions of camera distance and the nearest
   visible ground is 50 m away, so half the shadow atlas is spent on empty air.
4. **The haze is a curve change, not a new pass.** Exponential-squared fog at a computed density is
   gentle on the near cells and dissolved by the surround, the sky agrees by being given the fog
   colour from one place in C#, and a fullscreen fog pass would cost a third of the frame to add
   what this camera cannot see. Height fog is ruled out by the camera geometry.
5. One caution the reference research raises: **ground relief measured its amplitude as invisible
   against the 72° sun.** Relief and the sun angle are the same feature, so the cheapest experiment
   is one `ReliefCheck` pair at 72° and 20°. The board may read very differently once the light
   rakes it.
6. And a correction to the premise of question 7: **that game is probably not Unity**, and there is
   no public technical account of its rendering at all. What survives is design intent — a physical
   diorama, tilt-shift by name, and a hard readability rule — and our own arithmetic. Nothing in
   the plan may cite it as evidence about an engine.

Next phase: a design section in `06-rendering-and-camera.md`, an ADR, execution units, and a
`Odyssey → Presentation → Check the light` contact sheet in the style of `ReliefCheck`. It waits
for the owner's go.
