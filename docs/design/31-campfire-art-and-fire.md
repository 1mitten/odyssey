# 31 — The campfire: Synty art, and a fire that reads as a fire

**Status: plan, not built.** Written 2026-09-23 on the owner's report — *"the campfire is a block.
We have campfire assets available using the western frontier synty pack and using an appropriate
smoke and fire shader."* Nothing here is implemented. It is grounded in what is actually on this
disk, and section 10 is the ordered work with its gates.

**Two decisions taken by the owner the same day, and both moved the plan.** *"campfire"* settles
the naming (§9) — `icon-map.csv`'s sheet-03 cell now points at `ui.arch.tool.campfire`, and the
brazier stays an unbuilt M3 idea with no art. And *"the fire lights the surroundings at night —
happy to use or create our own shaders if it's problematic"* pulls **light into scope**, where the
first draft had deferred it, and settles the shader question in favour of writing our own where the
pack's are awkward. Light is now §8, and it is the section with the good news in it.

Design 28 is the campfire's mechanics — heat, fuel, what it feeds. This is only its look.

---

## 1. What is true today

`Building_Campfire` draws as a plain block in the stuff's tint. That is not an oversight, it is a
recorded stand-in: `ModuleIds.Campfire`'s own comment says *"no catalogue row owed, the plain block
placeholder in the stuff's tint until real art lands — a ring of stones reads fine as a low block
… One row on this id upgrades every campfire when the art arrives."*

Two things in that sentence have turned out to be wrong, and they are why this document exists.

- **A ring of stones does not read fine as a low block.** It reads as a block. The owner's report
  is the measurement.
- **One row is the *mesh*, and the mesh is the lesser half.** A campfire that is a correct pile of
  logs and no flame is still not a fire. The thing that says "fire" is the moving part, and there
  is no row for that — no catalogue row can express it, because it is not a mesh.

So this splits cleanly in two, and they are genuinely independent: the **prop** goes through
machinery that already exists and is nearly free, and the **fire and smoke** are new and are where
every decision is.

## 2. What is on this disk

Checked, not assumed — every path below exists under `Assets/Synty/` on the Windows machine.
Dimensions are from `docs/research/synty-inventory.csv`; the research lane that catalogued these
packs is `docs/research/e-03-other-packs.md`, which already decided *"WF campfires … **Use**. The
pre-power settlement tier; campfire is the only cooker owned"*.

### The props

| Prefab | Size | Renderers | Tris | Notes |
|---|---|---|---|---|
| `SM_Prop_Campfire_01` | 3.28 × 2.36 × 3.21 m | **1** | 714 | A big communal ring. Over a 2.5 m cell by 0.78 m |
| `SM_Prop_Campfire_Small_01` | 1.29 × 1.58 × 1.19 m | **2** | 748 | Fits a cell — but the second renderer is `SM_Prop_Campfire_Pot_01`, a cooking pot on a tripod |

The small one's second renderer is the catch and it is not visible from the name.
`ModuleLibrary.FlattenPrefab` takes **every** `MeshFilter` under the prefab, so pointing a row at
`SM_Prop_Campfire_Small_01` puts a cooking pot on every campfire in the colony. There is no
per-part exclusion on `ModuleEntry` — the fields are `prefab`, `centreXZ`, `baseAtY`, `topAtY`,
`offset`, `yaw`, `scale`, `materialOnly` — so excluding the pot would mean new code.

A pot is also wrong on the merits today: there is no cooking in the game, no stove exists in any
owned pack (`e-03` §"Cooking"), and design 28 describes the campfire as *"kindling and a ring of
stones"* costing 3 wood.

### The effects

`PolygonParticleFX` is installed. `FX_Fire_Small_01/02/03`, `FX_Fire_01`, `FX_Smoke_White_Small_01`
and about 180 others. Western Frontier ships its own `FX_Fire_01.prefab` beside the campfire, and
PolygonGeneric ships `FX_Fire_Card_Small/Large/Huge_01`, which `e-03` calls *"cheap LOD card fire …
for many simultaneous burning cells"*.

**Two facts about those effects decide most of this document, and both were checked rather than
assumed.**

**The inventory knows nothing about them.** Every FX prefab in `synty-inventory.csv` reads
`0.00 × 0.00 × 0.00`, **0 renderers, 0 triangles, 0 materials**. The inventory counts `MeshFilter`s
and a `ParticleSystemRenderer` is not one. So there is no committed measurement of what any of them
costs or how big it is, and `e-03` says so in its own caveats: *"each FX prefab used by the slice
needs a one-off URP render check in the editor before being committed to"*, at **medium**
confidence, alongside *"2 errors loading `PolygonParticleFX/Scenes/Demo.unity`"*.

**Their materials hang off a licensed Shader Graph.** `FX_Emissive_Fire_01.mat` and `FX_Smoke_01.mat`
both point at shader guid `0730dae39bc73f34796280af9875ce14`, which is
`Assets/Synty/PolygonGeneric/Shaders/Generic_Basic.shadergraph` — a Shader Graph inside the
gitignored folder. Western Frontier's own `FX_Fire_01.mat` is different again: guid
`0000000000000000f000000000000000`, fileID 200, a Unity **built-in** shader, which is the legacy
particle family `e-03` warns about (*"58 particle materials use `Legacy Shaders/Particles/*`"*) and
which has no URP guarantee at all.

That first one is not a new problem, it is a **known one with a scar**. CLAUDE.md records
`SyntyInstancingKeepAlive`: the pack's own Shader Graph shaders *"which no `Shader.Find` ever names
and which arrive on prefabs with instancing off — staged for the build and deleted after, **because
a keep-alive for a licensed shader must never be committed**"*. Three separate faults had to be
fixed before the first player build drew anything, each invisible until the one before it was
fixed, and this is the same shape: a licensed Shader Graph reached only through a prefab reference,
which the player build strips.

## 3. The prop: one row, and which prop

**Recommendation: `SM_Prop_Campfire_01`, scaled ~0.70.**

3.28 m × 0.70 = 2.30 m, which sits inside a 2.5 m cell with a margin. One renderer, no pot, 714
triangles, and `ModuleEntry.scale` already exists so this is a **catalogue row and nothing else** —
exactly the "one row upgrades every campfire" the code promises.

The alternative is `SM_Prop_Campfire_Small_01`, which needs no scaling but brings the cooking pot
and would need either new part-exclusion code or a committed prefab variant. It is also *small*: at
1.29 m it occupies half a cell and will read as lost beside a 2.5 m bed.

**What breaks the tie if the scale looks wrong in the editor:** a Synty prop scaled to 70% keeps its
silhouette but its log thickness and stone size go with it, and this pack's style is chunky. If the
scaled ring reads as a toy, the fallback is the small one plus part exclusion — and part exclusion
is worth having anyway, because the pot problem will recur on any pack prop that ships an
accessory. That is one field on `ModuleEntry` (a names-to-skip list) and one `if` in
`FlattenPrefab`, so it is cheap; it is just not needed *first*.

**Not in scope:** a stove. The campfire is not a cooker in this game yet, and the day it is, the pot
is one row away.

## 4. The fire: three ways, and they are genuinely different

This is the decision the document exists for. All three produce a flame; they differ in what they
cost, what they drag into the build, and what happens on a machine without the packs.

### Option A — instantiate the pack's FX prefab per campfire

`Instantiate(FX_Fire_Small_01)` parented to a director, one per drawn campfire, pooled.

The fastest to a screenshot, and it looks like Synty because it *is* Synty. Against it:

- **It is a GameObject per cell**, which is the thing this renderer exists not to do. Not fatal —
  campfires are bounded, `needsClearCell` means one per cell and they are player-built — but each
  prefab is several nested `ParticleSystem`s, so draw calls scale with campfires and land in the
  frame budget as `P10`'s tick-side twin.
- **It drags a licensed Shader Graph into the player build**, with the `SyntyInstancingKeepAlive`
  scar above as the precedent for how that goes.
- **On the CI runner and any clone there is no fire at all** — which is correct and expected, but
  it also means no test can ever assert anything about it beyond "the art resolved".
- Pause, slice-hiding, warm-up and the daylight tint each need doing per instance.

### Option B — our own shared emitters, the `ChipDirector` idiom

`ChipDirector` is the house pattern and it is a good one. Read its class comment: **one**
`ParticleSystem` for the whole colony, world space, emit-on-demand, material built in code from
`Shader.Find` with fallbacks, `Warm()`ed on construction *"because the first draw of a particle
material compiles its shader variant … left to happen naturally that lands on the frame the first
axe hits a tree"*, and `Running` driving `simulationSpeed` to 0 rather than `Pause()` so a paused
game holds the particles where they are.

A `FireDirector` in that mould: two shared systems (flame, smoke), both world-space, emitting
continuously at each drawn campfire's position. One draw call each however many fires burn. Pause,
warm-up and slice-hiding each have exactly one owner. No licensed shader in the build.

Against it: it will not look like Synty's authored fire unless it is tuned to, and tuning a particle
system by hand is slower than using one somebody already tuned. It can use the pack's *textures*
(`PolygonParticles_Smoke_01.png`) on our own URP material, which gets most of the look back while
leaving the licensed **shader** out of it — and degrades to a plain soft particle where the pack is
absent.

### Option C — an instanced card and our own shader

`OdysseyFire.shader` beside the five we already write and commit
(`OdysseyWater`, `OdysseyTree`, `OdysseyCharacter`, `OdysseyGradientSky`, `OdysseyOutline`). A
billboard quad per fire, gathered and submitted by `ChunkRenderer` as **one instanced call for every
campfire on the board**, animated in the shader — the same trick `OdysseyWater` already plays for
the falls' *"downward-scrolling streaks and foam"*.

This is the cheapest at runtime by a distance, it is the only option with **no GameObject and no
licensed dependency at all**, and it is what the owner's own words point at ("an appropriate smoke
and fire shader"). Against it: a stylised flame that reads at a 48° camera is real shader work, and
smoke that drifts and disperses is much harder as a single card than as particles.

### Recommendation

**B for the flame and the smoke now, with C kept in view for the flame if fires ever become
common.** The tie-breaker is not the look, it is this: options A and B produce a screenshot in
about the same time once the URP render check in section 6 has run, and B is the only one of the two
that leaves nothing licensed in the player build. The `SyntyInstancingKeepAlive` history says that
bill is paid late and painfully, and a campfire is not worth re-opening it. The owner has settled
the licence half of that directly — *"happy to use or create our own shaders if it's problematic"* —
so the fallback is not merely available, it is sanctioned.

**One argument against B got weaker when light came into scope, and it is worth being honest about
that.** The objection to option A was a GameObject per cell. But a light *is* a positional object:
§8 puts one on every drawn campfire whatever else happens, so the director is keeping a pooled
per-fire object either way and A's cost in objects is no longer the thing that separates them. What
still separates them is the **draw calls** — shared emitters submit once for the whole colony where
per-campfire prefabs submit per campfire — and the licensed Shader Graph, which is the one that has
already cost this project three passes. B still wins, on narrower ground than the first draft
claimed.

C is genuinely better on cost and is the *right* answer at "a hundred burning cells", which is where
this goes the day fire spreads (design 28 §10 lists fire as a deferred seam). It is not the right
answer for "a handful of player-built campfires", because it spends real shader effort on a problem
nobody has yet. **If the playtest says the fire is the best thing on the board and it should be
everywhere, that is the observation that promotes C.**

## 5. What the fire must also do, which is easy to forget

Whatever option wins, the fire is presentation and must obey the standing rules:

- **Nothing in a cell, a save or the state hash.** `ChipDirector`'s comment is the statement of the
  rule; chips, tufts, relief, sound and tree colour are all already on that side of the line.
- **It hides with the slice.** `PawnFigureDirector` computes a drawn set against
  `slice.HighestVisibleLayer(activeLayer, size.SizeY)` and retires anything outside it. A campfire
  three layers down must not glow through the floor.
- **It holds when the game is paused**, by `simulationSpeed = 0`, for the reason `ChipDirector.Running`
  gives — Unity's particle clock knows nothing about the simulation clock.
- **It is warmed**, or the first campfire ever built costs a shader compile on the exact frame the
  player is watching the thing they just built appear. This project has already been bitten by the
  build-appearance delay once (`06-rendering-and-camera.md` §6c.3).
- **It survives the pack being absent.** `PortraitStudio.Available` and `PawnFigureDirector.Enabled`
  are the two right questions; a `catalogue == null` check is the wrong one and *"has now turned the
  runner red twice"*.

## 6. The unknowns, and the experiment that settles each

Nothing below should be guessed at. Each has a cheap check and the checks are the first unit of
work, because three of them can change the plan.

| Unknown | Why it matters | The experiment |
|---|---|---|
| Do the pack's FX prefabs render under URP 17.3 at all? | `e-03` is at **medium** confidence, 58 materials are legacy-shader, and the FX demo scene logs 2 load errors. If they render magenta, option A is dead outright | Drop `FX_Fire_Small_01` and `FX_Smoke_White_Small_01` into the play scene in the editor and look. Fifteen minutes, and it is the gate on everything else |
| Does `SM_Prop_Campfire_01` at 0.70 read as a campfire or as a toy? | Decides §3, and whether part-exclusion code is needed | Place one in the editor beside a bed and a colonist. A screenshot answers it |
| What does one fire cost, and what do twenty? | `P10`: a pass priced per thing, invisible to review | `FrameTimeTests` arm with 0, 1 and 20 campfires on the played board, draw calls and ms, **one run** — the frame numbers on this machine are only comparable within a run |
| What do twenty **lit** fires cost? | Forward+ says they should cluster cheaply (§8), but "should" is not a measurement and this is `P10`'s family | The same `FrameTimeTests` arm, with the light on and off, so the light's share is separated from the particles' |
| Does the player build keep whatever material the fire uses? | Two green tiers say nothing about whether the game runs; this is the exact family `ShaderInclusion`/`InstancingKeepAlive` live in | `scripts/unity.sh build` then `Build/Win64/Odyssey.exe -odyssey-newgame -logFile …`, and **look at a campfire** — a clean log from the main menu proves nothing |

## 7. The sound is already there

`SoundIds.Campfire` exists, the clips are in the library, and `AudioSetup.cs` says in as many words
that it is *"in the library, played by nothing … the row is here so the day fires arrive the sound
is already there"*. That day is this one. It is a looping positional sound on a built thing, which
the audio framework has no other example of yet, so it is its own small unit rather than a line —
and the listener is still on the camera 32–160 m up while the catalogue authors ranges as ground
distances (`playtest-queue.md`), which this will walk straight into.

## 8. The light

Owner, 2026-09-23: *"the fire lights the surroundings at night."* The first draft deferred this as
"a different problem". Having gone and looked, that was too cautious, and the reason is one line in
a settings file.

### The finding that makes it affordable

`Assets/Settings/PC_Renderer.asset` has **`m_RenderingMode: 2` — Forward+**.

`PC_RPAsset.asset` carries `m_AdditionalLightsPerObjectLimit: 4`, which reads alarming and is the
classic Forward-path ceiling: any one renderer lit by at most four local lights, and the symptom
when you exceed it is lights **popping** in and out per object as the camera moves. In Forward+
that limit is **not used**. URP 17.3 clusters local lights against the frustum instead, and the
practical ceiling becomes the per-frame visible-light budget rather than anything per object.

So "a point light on every campfire" is an ordinary thing to do here, not an extravagance. That is
the single fact that turns this section from a deferral into a unit of work — and it is a fact
about a committed asset, which means **it can be changed by somebody tuning graphics settings**.
`27-graphics-settings.md` is the document that now owns a lever that would break this, and a note
belongs there rather than only here.

### What the light is

One **point light per drawn campfire**, pooled on the same director and retired against the same
`Drawn` set as everything else in §5. Positional, so unlike the particles it cannot be shared.

- **Shadows off.** This is the decision that keeps it affordable and it should never be quietly
  reverted. `m_AdditionalLightShadowsSupported: 1` and the additional-light shadow atlas is a
  single 2048 map. A *point* light's shadow is **six** faces, so ten shadow-casting campfires ask
  for sixty faces out of one atlas — either tier resolutions collapse to nothing or the atlas
  thrashes, and every one of those faces re-renders the geometry around it. A campfire is a soft
  warm pool on the ground, which is what it looks like anyway; it is not a thing that should cast
  the walls of a hut across the floor.
- **A flicker, and this is the part that sells it.** A point light at a constant intensity reads as
  a lamp, not a fire. A small pseudo-random wobble on intensity with a slight warm-to-orange shift
  is a handful of lines and is the difference between "there is a light here" and "something is
  burning here". It must be driven off the *presentation* clock and must hold when the game is
  paused, exactly as the particles do — a fire flickering merrily in a paused game is the same
  fault `ChipDirector.Running` was written for.
- **No modulation by time of day.** A fire emits the same at noon as at midnight; what changes is
  how much it matters, and the daylight cycle already owns that by writing the global ambient. Two
  systems dimming the same thing is two owners for one rule, and the standing rules in CLAUDE.md
  are mostly scars from exactly that. The knob is there if the playtest wants it.
- **Range in metres, against a 2.5 m cell.** "Lights the surroundings" is two to four cells, so
  something like 6–10 m. Tuned against a screenshot, not guessed at here.

### The two constraints it must respect

**The slice, and this one is a bug waiting to happen.** Hiding a fire on a hidden layer is not
enough: a light does not know what the slice camera is doing, so a campfire three layers down would
go on lighting the floor above it from underneath. The light must be **disabled**, not merely the
flame hidden — and because those are two different operations on the same object it is exactly the
sort of pair where one gets done and the other does not.

**Portraits, which are safe by geometry rather than by a mask — so keep them that way.**
`PortraitStudio` renders on a rig at `Underworld = -5000f` with a 6 m far clip, and
`TakeOverEnvironment` switches off every other **directional** light, because a directional light
is global. It does *not* switch off point lights, and it does not need to: a finite-range light
attached to a cell on the board is five kilometres away. The rule that falls out, and the reason to
write it down, is that **the fire's light must stay attached to a board cell and must never be
directional** — the day one of those stops being true, portraits start being lit by a campfire and
the result is cached for the session (`20-avatars.md` §10.7).

## 9. Deliberately not in scope

- **Fuel.** Design 28 records it as a hook; v1 burns steadily and the art should not imply
  otherwise (no dying-down).
- **Fire as a hazard, spreading, burning buildings.** Design 28 §10's deferred seam. This document
  is about one building's appearance, and building the general case now would be inventing a
  mechanic.
- **Cooking.** No stove exists in any owned pack; the pot is one row away on the day it does.
- **The brazier — settled, 2026-09-23.** `docs/design/28-temperature.md` §13a-iii found the naming
  registry holding two heat sources with the pixel art mapped to the wrong one. The owner's answer
  is *"campfire"*: `icon-map.csv`'s sheet-03 cell (whose own description was always the word
  "campfire") now points at `ui.arch.tool.campfire`, and `ui.arch.tool.brazier` stays in
  `icon-keys.csv` as an unbuilt M3 idea with no art — a fuelled, indoor, riskier thing that can
  have its own cell the day it exists. Nothing in §3 needs re-reading.
- **Shadows from the fire**, for the reason §8 gives. Not a deferral so much as a decision.

## 10. The work, in order

Each unit ends where it can be judged. The first is a gate on the rest and is mostly looking.

| # | Unit | Done when |
|---|---|---|
| **C1** | The three checks in §6 that can change the plan: URP render check on two FX prefabs, the scaled prop beside a colonist, and a baseline frame measurement with no campfire | A screenshot of each and a paragraph. **Stop here and report** — if the FX prefabs do not render, §4 is re-decided before anything is built |
| **C2** | The prop: one `ModuleEntry` row on `ModuleIds.Campfire`, prefab `SM_Prop_Campfire_01`, `scale` from C1, `baseAtY`. Plus the test that asks whether the art *resolved* | A campfire in the meadow is a ring of logs, and is still a tinted block on a clone with no pack |
| **C3** | `FireDirector`: the flame, one shared world-space system in the `ChipDirector` mould — material in code, `Warm()`, `Running`, retired against `HighestVisibleLayer` | A built campfire burns, holds still when paused, and vanishes when the slice goes below it |
| **C4** | The smoke: a second system on the same director, slower, fewer, rising | Smoke reads as a column at the play camera's 48° rather than as a grey smudge |
| **C5** | **The light** (§8): a pooled point light per drawn fire, shadows off, flickering, disabled with the slice | A campfire at night throws a warm pool a few cells wide; a fire below the visible slice lights nothing through the floor; a paused game stops the flicker |
| **C6** | Cost: the `FrameTimeTests` arm from §6 at 0, 1 and 20 fires, **light on and off**, one run, numbers into this document | Measured, the draw calls counted rather than reasoned about, and the light's share separated from the particles' |
| **C7** | The looping sound on `SoundIds.Campfire` | A fire is audible near it and not across the map |
| **C8** | Player build smoke test, §6's last row, **at night** | `Odyssey.exe -odyssey-newgame` draws a burning, lighting campfire |
| **C9** | A note in `27-graphics-settings.md`: the render path is load-bearing for §8 | Somebody tuning graphics settings can see what a Forward+ → Forward change would cost |

**C1 is a hard stop.** It is cheap, it is mostly looking at things, and two of its three answers can
send §4 a different way. Nothing after it should start before it has been reported.

---

## 11. What this changes if it lands

Nothing in `Odyssey.Sim`. No save format, no state hash, no golden. `Building_Campfire`'s Def,
its heat, its cost and everything design 28 tuned are untouched — this is entirely
`Odyssey.Presentation`, one catalogue row, one new director and one test file, which is why it can
run beside anything else in flight.

The one thing that reaches outside presentation is the naming fix in §9, which is a content change:
`icon-map.csv` plus a wiki rebuild, both gates green, in the same commit as the rule requires.


## 14. What it cost, measured — 2026-09-23

The owner reported the frame going from **1.5 to 4.5 ms** with campfires on the board, and added
that the machine might have been under load. Both were worth taking seriously and only a control
inside one run can separate them: this machine drifted the city canary from 2.01 to 4.01 ms in an
afternoon on what a sibling worktree was doing, and three Unity editors were open when the report
came in.

`FrameTimeTests.TheCampfireSweepCostsWhatItVisits` is that control — no fire, one fire, then nine,
each timed twice with the lookup mode alternating, all in one run on the wooded board:

| Fires drawn | Frame | Draw calls |
|---|---|---|
| 0 | 2.305 ms | 1,358 |
| 1 | 2.329 ms | 1,359 |
| **9** | **2.302 ms** | **1,359** |

**Nine burning campfires cost nothing measurable.** The spread across all six rows is 0.027 ms,
which is less than the run-to-run drift of the same board minutes earlier in the same session
(2.56 → 2.30 ms on no change at all). **One** extra draw call carries all nine, which is the shared
emitters doing what they were built for. The report was the machine.

### 14a. But the sweep was a real bug, found while looking

`RefreshCells` caches which cells hold a fire against `WorldRenderModel.Version`, and its comment
claimed the board was therefore swept *"once per structural change rather than once a frame"*.
`RefreshDirty` bumps that version whenever **any chunk remeshes** — in a colony doing anything, most
frames. The sweep is 230,400 cells on the played board, to find at most a handful of fires, and it
runs **whether or not a campfire exists**: a colony that has never seen a fire was paying to look
for one.

It is the same fault as `TemperatureSystem`'s in §13a, one day apart and written by the same hand:
**a complexity claim in a doc comment is not a measurement.** `P10` in `bug-patterns.md` is its
drawing-side twin.

Fixed by walking the standing edifices instead — where a campfire actually lives, and a list two
orders of magnitude shorter: **1,242 records against 230,400 cells**, a 185× reduction per sweep.
The trigger is unchanged, so the cache refreshes exactly as often; what changed is what a refresh
costs. `FireDirector.Find.Cells` is kept as the control that priced it.

**And the honest part: it never showed up in the frame.** The measurement above records **1 rescan
in 180 frames**, because the test colony is idle and nothing remeshes. The bug is real, the fix is
right, and neither is why the owner saw 4.5 ms. Both facts belong here — quoting only the 185×
would imply a saving nobody has observed.

### 14b. The clock, the events column, and a budget that said no

The outdoor temperature needed room. `HudLayout.ClockWidth` sizes the clock *and* the alerts,
bulletins and toasts under it, so one number widens the whole right column together — which is what
the owner's *"including events — widen this up to match"* asks for.

**266 → 296, and not further, because the coverage budget is what was left.** Every pixel of width
is 91 px of area in the resting HUD, and `HudLayoutTests.TheStripIsAlwaysOneRowAndNoFurther` caps
that at 20% of a 1280 × 720 canvas. 300 came to **20.02%** and failed. Raising the ceiling is the
obvious alternative and was deliberately not taken: `CoverageCeiling`'s own history records that it
*"is the owner's to reverse"*, and that the colonist name pool nearly took it to 0.21 and **the
owner declined**. Wider than 296 is that decision again rather than a tweak.

The width is also **written twice** — `ClockWidth` in C# and `.column-right`'s `width` in
`Hud.uss` — because the model is what the fast tier reasons about and the stylesheet is what the
panel is laid out by. Nothing checks that they agree. Both carry a note saying so.

### 14c. The fire is quieter

`Volume` 0.75 → **0.45** (owner, 2026-09-23). It was 0.55 against the synthesised placeholder,
raised to 0.75 on the loudness arithmetic in §13's audio note, and cut on hearing the real thing.
An ear beats a calculation about loudness; the arithmetic is kept in `AudioSetup` because it still
explains why the clip is quieter than the placeholder it replaced.
