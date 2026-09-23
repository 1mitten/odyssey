# 31 — The campfire: Synty art, and a fire that reads as a fire

**Status: plan, not built.** Written 2026-09-23 on the owner's report — *"the campfire is a block.
We have campfire assets available using the western frontier synty pack and using an appropriate
smoke and fire shader."* Nothing here is implemented. It is grounded in what is actually on this
disk, and section 9 is the ordered work with its gates.

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
bill is paid late and painfully, and a campfire is not worth re-opening it.

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
| Does the flame need a light? | A fire that does not light anything at night is half a fire, and a light per campfire is a real cost | Deferred on purpose — see §8. Ask it of the playtest, not of the code |
| Does the player build keep whatever material the fire uses? | Two green tiers say nothing about whether the game runs; this is the exact family `ShaderInclusion`/`InstancingKeepAlive` live in | `scripts/unity.sh build` then `Build/Win64/Odyssey.exe -odyssey-newgame -logFile …`, and **look at a campfire** — a clean log from the main menu proves nothing |

## 7. The sound is already there

`SoundIds.Campfire` exists, the clips are in the library, and `AudioSetup.cs` says in as many words
that it is *"in the library, played by nothing … the row is here so the day fires arrive the sound
is already there"*. That day is this one. It is a looping positional sound on a built thing, which
the audio framework has no other example of yet, so it is its own small unit rather than a line —
and the listener is still on the camera 32–160 m up while the catalogue authors ranges as ground
distances (`playtest-queue.md`), which this will walk straight into.

## 8. Deliberately not in scope

- **Light from the fire.** It is the obvious next thing and it is a different problem: a real-time
  point light per campfire against a daylight cycle that owns the global ambient, plus
  `PortraitStudio` which takes over the whole environment for the instant of its render. Worth
  doing, worth doing on its own, and worth asking the playtest about first.
- **Fuel.** Design 28 records it as a hook; v1 burns steadily and the art should not imply
  otherwise (no dying-down).
- **Fire as a hazard, spreading, burning buildings.** Design 28 §10's deferred seam. This document
  is about one building's appearance, and building the general case now would be inventing a
  mechanic.
- **Cooking.** No stove exists in any owned pack; the pot is one row away on the day it does.
- **The brazier.** `docs/design/28-temperature.md` §13a-iii: the naming registry has two heat
  sources and the pixel art is mapped to the wrong one. That is an open owner decision and this
  plan does not depend on which way it goes — but if the answer is "it was always a brazier", the
  prop choice in §3 should be re-read first.

## 9. The work, in order

Each unit ends where it can be judged. The first is a gate on the rest and is mostly looking.

| # | Unit | Done when |
|---|---|---|
| **C1** | The three checks in §6 that can change the plan: URP render check on two FX prefabs, the scaled prop beside a colonist, and a baseline frame measurement with no campfire | A screenshot of each and a paragraph. **Stop here and report** — if the FX prefabs do not render, §4 is re-decided before anything is built |
| **C2** | The prop: one `ModuleEntry` row on `ModuleIds.Campfire`, prefab `SM_Prop_Campfire_01`, `scale` from C1, `baseAtY`. Plus the test that asks whether the art *resolved* | A campfire in the meadow is a ring of logs, and is still a tinted block on a clone with no pack |
| **C3** | `FireDirector`: the flame, one shared world-space system in the `ChipDirector` mould — material in code, `Warm()`, `Running`, retired against `HighestVisibleLayer` | A built campfire burns, holds still when paused, and vanishes when the slice goes below it |
| **C4** | The smoke: a second system on the same director, slower, fewer, rising | Smoke reads as a column at the play camera's 48° rather than as a grey smudge |
| **C5** | Cost: the `FrameTimeTests` arm from §6 at 0, 1 and 20 fires, one run, numbers into this document | Measured, and the draw calls are counted rather than reasoned about |
| **C6** | The looping sound on `SoundIds.Campfire` | A fire is audible near it and not across the map |
| **C7** | Player build smoke test, §6's last row | `Odyssey.exe -odyssey-newgame` draws a burning campfire |

**C1 is a hard stop.** It is cheap, it is mostly looking at things, and two of its three answers can
send §4 a different way. Nothing after it should start before it has been reported.

---

## 10. What this changes if it lands

Nothing in `Odyssey.Sim`. No save format, no state hash, no golden. `Building_Campfire`'s Def,
its heat, its cost and everything design 28 tuned are untouched — this is entirely
`Odyssey.Presentation`, one catalogue row, one new director and one test file, which is why it can
run beside anything else in flight.
