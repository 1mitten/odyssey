# 29 — The illustrated look

**Status:** owner decision 2026-09-22; branch `claude/illustrated-look`, worktree
`D:\code\odyssey-look`. §2 is **built**. §3 and §4 are **designed and not built**. §5 is
**deferred by the owner** and exists so nobody starts it from a tutorial.
**Read first:** `06-rendering-and-camera.md` §1 (which this amends) and §6c (the frame budget),
`docs/research/d-09-stylised-rendering.md`, `docs/research/look-interview.md`,
`docs/research/b-painted-look-games.md`.

## 1. Why, and the decision

The owner supplied four reference images and asked whether we could get grass and visuals like
them, and whether we could move towards *Dungeons of Hinterberg*.

**Three of the four are this project's own concept renders** — the images
`docs/reference/screenshots/README.md` has been asking somebody to copy into `concept/` since
2026-09-15 and nobody ever did. Looking at them rather than at the description of them settles
something this repository has had wrong for a year. `06-rendering-and-camera.md` §1 says:

> Cel shading was raised and rejected on 2026-09-15 … the concept renders are *not* cel-shaded.
> They are flat-lit low-poly with a colour atlas and emissive trim … **The target is the
> screenshots as they are.**

They are ink-outlined, flat-shaded comic art. Every building carries a black line, the shading is
two or three values with a hard terminator, the palette is saturated and the distance is a flat
blue wash. That is not "flat-lit low-poly with emissive trim"; it is the thing the sentence says it
is not. Hinterberg is therefore not a departure from the concept renders — **it is closer to them
than the shipped build is.**

Meanwhile the build went the other way, and well. The 2026-09-16 look interview
(`look-interview.md`) committed to a *Station to Station* golden hour: a raking sun at 30°, lifted
shadows, warm exponential haze, bloom above 1.1, a vignette and SMAA. That is a **photographic**
look. The concept renders and Hinterberg are **graphic** looks. Both are defensible, both are
already half-built — there is a screen-space ink outline on the renderer and an inverted-hull ink
pass on the colonists — and **they are not reconcilable in one image**.

### The decision (owner, 2026-09-22)

> *Both, as a switchable preset.*

`Graphics ▸ Look ▸ [ Lit | Illustrated ]`, judged by pressing Play on one save and flicking between
them. Nothing is reversed until the owner has seen the two side by side, which is the right shape
for this project specifically: the fixed golden hour became a full day/night cycle *the same day it
landed*, because seeing it lit changed the answer.

The owner also ranked what matters and deferred the expensive part:

| Rank | | Where |
|---|---|---|
| 1 | Tall grass you can see | §2, **built** |
| 2 | The sky and clouds | §3, designed |
| 3 | The ink line | §4, designed |
| — | Flat banded colour — *"leave 2 for later as it is more work"* | §5, held |

### What the research changed, and two comments in the source that are now wrong

- **A DepthNormals prepass already runs every frame.** `PC_Renderer.asset` configures SSAO with
  `Source: 1` (`DepthNormals`), and `PC_RPAsset.asset` keeps `m_PrefilterSSAODepthNormals: 0` while
  stripping the three depth-source variants. So `_CameraNormalsTexture` exists today at **zero
  incremental cost**. `OdysseyOutline.shader` lines 8–14 decline normals on the grounds that they
  "come from a prepass that re-renders the world" and that it is unclear whether our instanced
  draws reach it; the prepass it is avoiding has been running the whole time. This also answers the
  open question `d-09-stylised-rendering.md` §5 and "Could not be determined" both hang on.
- **Grass was licensed art, and did not exist without it.** See §2.
- **Unity 6.3 removed URP Compatibility Mode entirely**, so every renderer-feature tutorial written
  against `Execute(ScriptableRenderContext, ref RenderingData)` is dead code. `OutlineFeature.cs` is
  already idiomatic Render Graph and is the pattern to copy.
- **Forward+ makes `GetAdditionalLightsCount()` return 0.** Relevant to §5 and recorded there.

---

## 2. Grass (built)

### 2.1 What it was

Three Synty foliage prefabs (`ModuleIds.GrassTuftA/B/C`), scattered 60 per hundred grass cells,
resolved through `ChunkMesher.EnsureScatterModules` — which **dropped any variant that did not
resolve to real art**, because the alternative was fourteen thousand grey cubes strewn across a
meadow. Three consequences, and the third is the one that matters:

1. The silhouette was the pack's and not ours to change.
2. The blades were an alpha-clipped cut-out, which at a camera looking down a meadow at 48° is the
   worst kind of overdraw: every pixel of every card behind every other card shaded and discarded.
3. **A clone without `Assets/Synty` rendered bare ground** — and that includes the build runner. The
   largest visible thing in the game was the one thing no test could see.

### 2.2 What it is

**`GrassMesh`** builds the clumps in code: three variants of four to six tapered blades, five
vertices and three triangles each, authored **in metres with the roots at y = 0**. There is no
texture and no alpha clip anywhere in the feature.

- **`ModuleShape.GrassClump`'s fallback box is the untouched unit box.** Every other code-built mesh
  spans a cell and is scaled into place; a clump's shape *is* its size, so the mesher's
  `Matrix4x4.TRS(surface, yaw, 0.7–1.2)` places it with no constant to look up. `GrassTests`
  asserts the roots sit at zero, because a mesh that got this wrong would float or sink uniformly
  across the whole board and read as a terrain bug.
- **Vertex colour red is the height along the blade**, black at the root and full at the tip; green
  is a per-blade constant. One channel drives the colour ramp, the wind bend and the camera lean.
  Our code-built meshes carried positions, normals and UV0 only, so `COLOR` was free. It is also the
  convention the commercial stylised-grass shaders use (§2.5), which keeps a door open for nothing.
- **The shading normals lean most of the way towards up.** Everywhere else in this renderer normals
  are hard and per-face and the flat-lit look depends on it; a clump shaded that way is a dozen
  differently-angled facets and reads as a spiky grey star. The geometry is still hard — nothing is
  smoothed across a seam. **Do not "fix" this with `RecalculateNormals`.**

**`Odyssey/Grass`** is a hand-written URP shader in the house idiom (`Odyssey/Tree` is the model),
with four passes: `UniversalForward`, `ShadowCaster`, `DepthOnly` and **`DepthNormals`**. The last
is not optional now that §1 has established the prepass runs every frame: anything missing from it
is a hole in the ambient occlusion and, once §4 lands, a hole in the ink.

- **The colour is a root-to-tip ramp**, with `_RampBias` for how much of the blade the dark root
  keeps and `_Banding` for how hard the step is. At `_Banding = 0` it is a gradient; at 1 it is two
  flat bands with a line between them. **One shader serves both rungs of the Look**, which is why
  the banding is a parameter and not a keyword.
- **Wind is in the vertex shader** and costs nothing on the CPU: a travelling wave over world x and
  z so a gust crosses the field rather than every clump breathing in time, offset per blade by the
  green channel so a clump does not sway as a rigid body, bent by height so the root stays planted.
- **The lean towards the camera is the one non-obvious idea, and it decides whether this works.** A
  blade is a thin upright card and a camera looking down sees its top edge. Leaning the tip
  horizontally towards the camera turns the blade's face up towards the lens. Without it a meadow
  at our pitch reads as grey fuzz. It is `_FaceCamera`, default 0.3.
- **Every pass deforms identically**, through one `GrassDeform` in the shared `HLSLINCLUDE`, or the
  depth and normals buffers would describe grass standing somewhere the picture does not show it.
  The **one deliberate exception** is the shadow caster, which runs from the light: there
  `_WorldSpaceCameraPos` is the light's position and the camera lean would be nonsense, so it is
  passed zero.
- **The mesh bounds are grown by `GrassMesh.MaxSway`**, because a bucket is culled as one bounding
  box and nothing that culls knows about a vertex shader. Without it a clump at the edge of the view
  pops out whole at the moment it bends.

### 2.3 What did not change, and why that is the point

`ChunkMesher.EmitScatter` keeps its shape exactly: the same `GroundScatter.Placement` hash, the same
`ScatterDensity`, the same `AddBody` into the same buckets. A clump is still *one more instance of
one more module in the chunk it stands in*, so it inherits chunk culling, the slice, the depth
shade, the dirty-chunk rebuild and **one instanced submission per bucket**.
`GrassTests.AThickerMeadowAddsInstancesRatherThanDraws` is the guard, in the shape of
`GrowingRenderTests` and `CellPlateTests`, because the same fault has been found four times in this
renderer and cost 3.67 ms of a 5 ms budget once (`docs/bug-patterns.md` P10).

Also unchanged and deliberately so:

- **The foliage render queue.** `MaterialCache.FoliageQueue` puts grass after the depth copy the
  outline reads, so grass is never inked. That was the fix for "the far meadow went dark" and a
  wrong diagnosis shipped before it; the only record is the `MeadowCheck` contact sheets.
- **Grass casts no shadows** (`ChunkRenderer.FoliageCastsShadows`). Seventeen thousand clumps each
  casting a few centimetres onto grass of its own colour is the cost with none of the benefit.
- **`GroundScatter.ClumpReach` stays at 1.0 m**, the figure the growing-zone pull-in is set from.
  `GrassMesh.Reach` is 0.62 m, so the pull-in is now conservative rather than exact. Tightening it
  would move where clumps stand beside a tilled tile, which is a look change with an owner
  attached, not a tidy-up.

### 2.4 Two things that were nearly bugs

- **The material swap is keyed on the module's shape, not on the foliage tint.** The obvious wiring
  — `foliage: true ⇒ draw with Odyssey/Grass`, the way `water: true ⇒ Odyssey/Water` already works —
  is wrong, because `ChunkMesher.EmitCrop` also carries `TintCode.Foliage(0)`: a carrot wants the
  late queue that keeps it out of the ink, and it is not a blade of grass. Keying off that bit drew
  every crop as grass. `MaterialCache.Get` therefore takes a separate `grass` flag and both callers
  ask `resolved.Shape == ModuleShape.GrassClump`.
- **`StuffPalette.FoliageTints` had to be retuned, and the old numbers say why.** They were
  multipliers over Synty's straw-coloured cut-out, which is why two of the three pushed blue past
  2.0 — the only way to get green out of straw is to multiply what little blue it has. Over a shader
  that is already green they make a blue meadow. They are gentle now and vary value and warmth
  rather than hue; the hue is the shader's, in one place, for every clump.

### 2.5 On the Asset Store package

The owner asked about Staggart Creations' *Stylized Grass Shader*. The linked listing (id 143830,
"for Unity 2021-2023", v1.4.7) is the legacy line and will never be ported to Render Graph; there
**is** a Unity 6 successor, id 357954, v2.1.1, €36.80, URP 17, minimum editor 6000.0.68f1. It was
not bought, for three reasons that are about our architecture rather than its quality:

- **It ships no placement or rendering system.** Its three documented routes are Unity Terrain
  details (no LOD, no shadows, no slope alignment), plain GameObjects with MeshRenderers ("definitely
  not recommended for large… dense grass"), or *a second paid indirect renderer*. We already own the
  expensive half and none of its support reaches it.
- **Its two headline features assume scene GameObjects we do not have.** The Color Map Renderer bakes
  terrain colour by photographing existing renderers — ours is `RenderMeshInstanced` from code, so
  there is nothing to photograph, and we can build that map straight out of the grid. The Grass
  Render Feature wants a GUID on the renderer asset.
- **The licence makes the clean-clone rule expensive.** It is an *Extension Asset*: one seat per
  person with it installed. Gitignoring it, which our rules require, breaks a clean clone in three
  places — a missing-script entry on the URP renderer, materials resolving to the error shader, and,
  fatally, any C# touching its namespace failing to compile and taking the whole build with it.

**The one idea worth taking from it was taken**: perspective correction (§2.2), plus its mesh
convention of the vertical gradient in vertex colour red.

### 2.6 Cost, measured

`FrameTimeTests.TheMeadowCostsWhatItGrows`, one barren meadow, three timings seconds apart in one
session so that whatever else the machine is doing cancels out. RTX 5070 Ti at 640 x 480,
2026-09-22, under the real player loop with post-processing on. At the shipped density of 140:

| | frame | draw calls | instances |
|---|---|---|---|
| no grass | 2.67 ms | 914 | 34,740 |
| **shipped, 140** | **2.93 ms** (+0.26) | 1,130 (+216) | 55,958 (+21,218) |
| 280 | 2.78 ms (+0.11) | 1,139 (+225) | 77,095 (+42,355) |

**Doubling the meadow again cost nine draw calls and twenty-one thousand instances**, which is the
P10 invariant about as plainly as it can be stated. The 216 calls the first tranche costs are the
chunks that had no clump in them at all before: a one-off, not a rate.

**And doubling it came out *faster* than not doubling it**, which is the honest headline. 2.78
against 2.93 is not grass getting cheaper; it is the run-to-run spread being larger than the thing
being measured. At these counts the meadow is inside the noise on this machine, and there is room
to go further if the owner wants it.

**This corrects the previous reading, and the correction is the point.** Measured at density 60 the
first tranche of 9,195 instances cost 0.06 ms and a second 8,977 cost 0.86 ms, and this document
recorded that as "not linear and not explained" with a note that some of it was probably noise. It
was all noise: the same test at 140 and 280 puts four times as much grass on the board for a tenth
of that difference, and the worst-frame column across the two runs (24-35 ms then, 3.6-12.5 ms now)
says which reading was disturbed. **A single paired measurement is not enough when the effect is
smaller than the spread** — the pairing cancels what the machine is doing between the two halves,
not what it does during them.

**What this still does not measure.** Every number here is 640 x 480 on a very fast GPU. Grass at a
camera looking down a field is an overdraw problem before it is anything else, overdraw scales with
pixels, 1080p is nearly seven times as many, and the blades were made thicker on 2026-09-22, which
makes overdraw worse rather than better. Roughly half the cost that does show up is CPU submission
(the World section of the split went 1.088 to 1.603 ms at the earlier density), and that half does
not scale with resolution — but the other half does. **The density ladder in §3.1 is the answer for
the laptop, and the laptop figure has to be taken on the laptop.**

### 2.7 The owner's first look, 2026-09-22

Played once, on the branch. Pause holds the meadow still and zooming out reads well, which were two
of the four questions the playtest row asked. Three changes came back and all three are tuning
rather than design:

| Asked | Was | Is |
|---|---|---|
| lighter green | root (0.18, 0.29, 0.14), tip (0.48, 0.65, 0.24) | root (0.29, 0.42, 0.21), tip (0.62, 0.78, 0.38) |
| thicker | blades 45-75 mm, shoulder 0.55 of the root | 75-115 mm, shoulder 0.62 |
| more of it | 60 per hundred cells | **140** - every grass cell gets one, two in five get a second |

**The density comment said 60 was chosen because 120 was wrong, and that judgement was about
different geometry.** It read: *sparse enough that the meadow reads as a field with grass on it
rather than as grass with a field somewhere underneath, which is what 120 did at board distance.*
True of the Synty cut-outs, which were wide painted cards - two in a cell closed the ground over.
Thin blades do not, so the judgement does not carry across, and the owner overruled it having seen
them. It is rewritten in place rather than deleted.

**The density had five owners and now has one.** `ChunkMesher`'s default, `TerrainSkirt.TuftDensity`,
`OdysseyBootstrap.grassScatter`, `SettingsPresenter`'s fallback and two frame-time harnesses each
wrote the literal 60, and every one meant "the density the game ships with". Raising it only where
the owner would see it would have left the benchmarks timing a meadow nobody plays - a performance
number quietly about the wrong world. They all read `ChunkMesher.DefaultScatterDensity` now, and
`GrassTests.TheMeadowAndTheSurroundAgreeOnHowThickTheGrassIs` guards the pair inside this assembly,
because a density difference at the board rim is a straight line across the view.

---

## 3. The sky and clouds (designed, not built)

Extend `Odyssey/GradientSky` rather than replacing it: its header already argues against a
photographic sky and for a flat wash, so clouds are what it was missing rather than a change of
direction.

- Two panned layers over one cloud field, at different scales and speeds, so the sky has parallax
  with no geometry.
- **The field is generated in code** — one small R8 texture built once at boot from seeded value
  noise — so there is no asset, no import-settings trap and nothing to license, and the owner tunes
  it by numbers. Leave an override slot: painted alpha reads more hand-drawn than noise, and that is
  a cheap upgrade once the shape is right.
- **Two thresholds, not one.** The first gives the flat cloud body; a second, slightly eroded one
  gives the **dark rim**, which is what makes a cloud read as drawn rather than as a blob.
  `_Posterise` drives how hard both are, so the Lit rung gets soft edges and the Illustrated rung
  gets a comic panel out of one shader.
- **Blend the horizon to `RenderSettings.fogColor` inside the sky shader.** URP fog does not apply to
  the skybox, and this project's whole haze design rests on the identity *the colour the distance
  fades to is the colour the sky is at the horizon* (`GoldenHour.cs` owns it for that reason).
- **Cloud colour is a `Daylight` key, not a constant**: add cover and tint to `DaylightState` and
  write them in `DaylightDirector.ApplyHour`, inside the existing `ApplyEpsilonHours` early-out —
  removing that guard once cost 0.43 ms a frame. The sky material is already instantiated into a
  runtime copy and handed back on `Dispose`; clouds must be written into the **copy**.

### 3.1 The switch

`GraphicsLadder.Look`, two rungs, Lit (0) and Illustrated (1). A ladder rather than a toggle so a
third look needs no migration. `DisplaySettingsApplier` is today the only subscriber to
`SettingsDirector.LadderChanged` and it owns the runtime pipeline copy — display levers only — so
the Look belongs in a small `LookDirector` beside `DaylightDirector`, not in the display applier.
`GraphicsLadder.GrassDensity` (Off / Sparse / Normal / Lush, writing `ChunkRenderer.ScatterDensity`
and forcing a redraw) goes in the same commit: it is the only honest answer to the budget question
on a 2022 laptop.

| | Lit | Illustrated |
|---|---|---|
| Sky | soft gradient, soft-edged cloud | hard two-band cloud with an ink rim |
| Ink | today's values | thicker, darker, creases on |
| Grass | `_Banding` 0 | `_Banding` 1 |
| Grade | today's `OdysseyGoldenHour` | saturation up, bloom down, vignette down |

---

## 4. The ink line and the crease (designed, not built)

The line is good; what it cannot draw is the interior crease, and a roof meeting a wall of the same
colour at the same depth is currently invisible. That is the difference between a render with a
border and a drawing.

- **Read the normals texture we are already paying for.** `ConfigureInput(Depth | Normal)` in
  `OutlinePass`, `resources.cameraNormalsTexture` from `UniversalResourceData`, `SampleSceneNormals()`
  in the shader so the encoding is not hand-decoded. Requesting it explicitly also makes the pass
  robust if SSAO is ever switched off. Roberts cross on the normal field, `max(depthEdge,
  normalEdge)`, a new `_NormalThreshold` beside `_DepthThreshold`.
- **`Odyssey/Character` has no `DepthNormals` pass** — add one, copied from `OdysseyTree.shader`.
  Colonists are already inked by their own hull because skinned meshes are absent from the depth
  texture, so they lose nothing, but they gain occlusion and creases.
- **`Odyssey/Water` has neither `DepthOnly` nor `DepthNormals`, and that is deliberate**: it is
  queued Transparent with `ZWrite Off` so the depth texture holds the bed and not the surface, "for
  the same reason grass is not [inked]". **Do not add the pass as a tidy-up.**
- **Thickness should scale with zoom** so a line keeps a constant *world* weight. Zoomed out, a
  constant-pixel line is what turns a colony into ink mush.
- **Keep the sliver test.** It is the reason the meadow is not a field of dark smudges, and a normals
  term will fire on exactly the thin geometry it exists to protect.
- **The character hull moves with the feature.** `ColonistMaterials.AdoptInkFrom` copies colour and
  thickness off the live `OutlineFeature` and `InkConsistencyTests` pins the defaults together.
- **`RenderSetup.Configure` rewrites every serialised field on every run**, deliberately, so new
  outline fields go in `Configure()` in the same commit or they silently never take.

---

## 5. Banded colour (deferred by the owner, 2026-09-22)

Not started. Written down so that whoever starts it does not start from a tutorial.

- **Forward+ makes `GetAdditionalLightsCount()` return 0.** Every pre-Unity-6 toon shader loops on it
  and silently drops every additional light. For a colony sim with campfires and lamps —
  `Building_Campfire` landed with the temperature work — that is the whole feature vanishing. The
  `LIGHT_LOOP_BEGIN` / `LIGHT_LOOP_END` macros in `RealtimeLights.hlsl` are the fix, and the exact
  spelling must be read out of the installed package.
- **Band `NdotL * shadowAttenuation` as one product**, not `NdotL` banded and then multiplied, or the
  shadow edge is a second differently-shaped hard edge that cross-hatches the first. Antialias with
  `smoothstep(t - fwidth(x), t + fwidth(x), x)`; a raw `step` aliases viciously under a flat palette.
- **Flatten the SH ambient**, or its smooth directional gradient un-flattens every band.
- **The conflict that is the real reason this is hard.** Layers below the slice are darkened by a
  per-layer brightness multiply baked into the instance tint (`SliceSettings`,
  `ChunkRenderer.ResolveColour`). With four bands and a value-multiply slice cue, bands from
  different depths land on the same values and the cue stops reading. The fix is to make the slice
  cue act on a **different channel** — desaturation and a cool tint, or a fixed offset applied
  *after* quantisation. That is an ADR, not a shader edit.
- `docs/research/b-painted-look-games.md` is the counterweight and should be read alongside: Guilty
  Gear Xrd reached a banded look with hand-edited normals on every asset and per-object light
  vectors, none of which we can supply, and the Ghibli breakdown reached a painterly result with no
  banding and no outlines at all.
