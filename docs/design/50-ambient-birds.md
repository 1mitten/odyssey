# 50 — Ambient birds

**Built 2026-09-25, not yet played**, branch `claude/wizardly-ride-wely6j`. The owner's request
("could we procedurally generate some birds flying around … or even a low poly bird"), researched
in `docs/research/d-23-ambient-birds.md` and `e-12-bird-models.md`, sketched in
`docs/reference/mockups/birds/` (hosted: https://claude.ai/artifact/7Y3abzdRug1KTkjU1xFiTZ), and
decided in `docs/research/birds-interview.md`: **birds grow with zoom, they fly and perch only, and
the first two are rooks and the buzzard.**

## 1. What a player sees

| Bird | Where | What it does |
|---|---|---|
| **Rook** | One to five flocks of 7–14, by board area (two on Standard). | Circles over the board in a loose flock, 10–20 m up, drifting from one part of the board to another. Every 20–60 s it comes down on treetops (or a roof if no tree is near), sits 12–35 s with its wings folded, and goes up again. A colonist or an animal within 8 m, or the tree or roof under it changing, puts the whole flock up at once. At 19:00 every flock makes for the **rookery**, a stand of trees chosen once per board, and sits there until 06:00. |
| **Buzzard** | One to three, by board area (one on Standard). | Circles in slow wide loops 30–40 m up, almost never flapping. Out 08:00–17:30, in clear or cloudy weather only. Otherwise it glides off the edge of the board and comes back when the day allows. |

Rain keeps rooks down longer (a perched spell is up to three times as long in a downpour). In a storm
every rook goes down and stays down. A flock that cannot find a perch (a board without trees or
roofs) keeps flying, and at dusk leaves by the nearest edge instead of roosting.

Nothing a bird does is in a cell, a save, the state hash or the pawn registry. Birds cannot be
clicked, selected, hurt or hunted, and they never block anything. The simulation does not know they
exist (d-23 §11: Cities: Skylines' seagulls took a citizen budget, which is the warning).

## 2. The shape

**Built in code, so there is no model file and no licence** (interview answer 2). `BirdShape`
(Odyssey.Hud, so the fast tier checks it) returns flat-shaded triangles for a bird of **unit
wingspan**, nose along +Z, up +Y, right wing +X:

- a six-sided spindle body with a beak and a tail fan;
- each wing as an **inner and an outer panel** meeting at an elbow, the buzzard with an extra
  fingered tip;
- every vertex carrying a **wing weight** (0 at the shoulder, 0.45 at the elbow, 1 at the tip) and a
  colour index (body, wing, underside, beak).

| | Rook | Buzzard |
|---|---|---|
| Triangles | 28 | 32 |
| Real span | 0.90 m | 1.25 m |
| Colours | body `#23242C`, wing `#2B2E3A`, under `#1B1C22`, beak `#8D8F93` | body `#6A4A32`, wing `#5D402B`, under `#CDB690`, beak `#3A3530` |
| Wingbeat | 3.2 a second | 2.2 a second |
| Share of time gliding | 35 % | 90 % |

`BirdShapeTests` holds the counts, the mirror symmetry, the weights and that no triangle is
degenerate.

## 3. The flap is the shader's

`Odyssey/Bird` lifts each vertex by `sin(clock × 2π × beats + phase) × amplitude × weight^1.4 ×
flap`, so the outer panel travels further than the inner one and the wing bends at the elbow. A
perched bird's **fold** pulls its wings in along the body. The clock is **game time**, the rain's
rule: paused, a wing holds where it is; at speed 3 it beats three times as fast.

Per bird the shader gets one matrix and one vector (phase, flap, fold). Each species is **one
`Graphics.RenderMeshInstanced` call**, and its shadows are URP's own shadow pass on the same call.
There are two species, so two calls. The pass also has `DepthOnly` and `DepthNormals` so a bird
takes the outline every solid thing has, and the depth pass cannot drop it.

Flat shading comes from the screen-space derivative of the world position rather than from mesh
normals, so a bent wing shades as bent.

## 4. The size grows with zoom

Interview answer 1. `BirdScale.For(cameraDistance)` is **1.0 at 25 m and closer, 1.75 at 140 m and
further**, smoothstepped between. At the 40° camera and 1080p a rook is about 9 px at 160 m drawn
×1.75, where life size would be about 5 px (`07-game-160m-life-size.png`). Close in, it is its real
size next to a colonist. Only the drawn mesh scales. Where a bird flies and perches is the same at
every zoom.

## 5. The flock is a model the fast tier runs

`BirdSky` (Odyssey.Hud, engine-free) owns every bird and steps them on game seconds. It is told the
hour, the weather, who is walking where, which parts of the board changed, and it asks an
`IBirdPerches` for places to land. That is the seam: the Unity side answers perches from the render
mirror, and the tests answer them from a fake.

- **A flock** is a leader point, not a bird: a centre drifting at 2 m/s towards a waypoint on the
  board, with the leader circling it at 18–28 m radius. Each bird steers towards the leader plus
  its own slot offset, with a separation push from its own flock only. So the work is n² within a
  flock of at most 14, never across the sky. **At most 73 birds** on Huge (`BirdSky.MaxBirds` 80),
  and the sketch's numbers stand: microseconds a frame.
- **Landing**: the flock asks for up to three perches within 30 m of its centre. Birds are dealt
  round them and spread over a crown's top, slow on the approach, flap hard in the last 6 m, and
  fold on arrival.
- **Scatter**: a walker within 8 m and 10 m vertically, a changed region under a perch, or a perch
  the model is told has gone (§6). The birds leave at once with an upward kick away from the cause,
  climb for three seconds, and rejoin the flock in flight.
- **Deterministic from a seed**, so a screenshot tool gets the same sky twice. The seed is fixed per
  board size. The birds are no part of the hash, so there is nothing for determinism to protect
  beyond that.
- A step longer than 0.1 s is split, and the model never takes more than five steps a frame, so a
  hitch cannot fling a bird across the board.

## 6. Where a bird can land

`BirdPerches` (Presentation) answers from the render mirror, through the sky column rule the rain
already uses (`SkyColumnRule.Walk`, design 43 §6). **A tree** is a column whose walk met a trunk. The
perch is the **top of the drawn crown**: `TreeArt.CrownTop` chooses the variant and the stance
exactly as `ChunkMesher.EmitTree` does, and reads the art's bounds. So a rook sits on the leaves of
the tree it is drawn on, not on a guessed height. `TreeArt.VariantsOf` is now the one owner of which
art rows a tree family has, and the mesher calls it (P1). Without the packs the tree is a stand-in
and the crown is `FallbackCrown` 6 m. **A roof** is a column whose walk landed on a slab.

A perch is dropped (and its birds scatter) when:

- its chunk's version moves (`WorldRenderModel.ChunkVersion`, the `SkyHeightMap.SyncDirty` pattern);
- its layer is above the highest layer the slice draws (`SliceSettings.HighestVisibleLayer`), so a
  rook is never left sitting on a roof that has been cut away.

## 7. What is drawn when

- **Below the surface** (`SliceSettings.BelowSurface`), no birds are drawn, as with rain. The model
  keeps running, so they are where they should be when the camera comes back up.
- Birds on the ground are **not a case**: every perch is a crown or a roof.
- Birds **Away** off the board are not drawn. A bird leaving flies to 30 m beyond the nearest edge
  before it goes, which is past the surround's near ring, so nobody sees it vanish.

## 8. Cost

- **Draw calls**: two, plus URP's shadow and depth passes on the same two meshes.
- **CPU**: `FrameSection.Birds`, its own line on the developer overlay and in the trace. The step is
  73 birds' steering and the matrix fill. Perch queries run only when a flock lands (a column walk
  each, three to thirty columns).
- **Not measured yet on the machine.** The first Play session reads `Birds` off the overlay. A
  number over 0.1 ms is the cue for the compute path d-23 ranked second; the mesh and shader do not
  change.

## 9. The build

`Odyssey/Bird` is found by name, so it is in `ShaderInclusion.Required` and the always-included list,
and it has a keep-alive material in `Assets/Resources/OdysseyKeepAlive/`. Without all three a player
build draws no birds and says nothing (the 2026-09-19 lesson).

## 10. Not in this unit

- **Birds on the ground** (answer 2). A modelled pigeon is the path if it is ever wanted (e-12).
- **Wood pigeons and swallows** (answer 3). Both are in the sketch. A species is a row in
  `BirdSpecies` and a flock rule.
- **Sound**: the day ambience already carries birdsong. A rook's call on scatter is the obvious next
  step, and it needs a recording.
- **Birds reacting to fire, gunfire or combat.** The scatter takes a list of disturbances, so a shot
  is one more entry when the ranged unit wants it.
- **A settings switch.** They are cheap, so there isn't one yet. If the owner wants one, it belongs
  with Detail in Settings → Graphics.
