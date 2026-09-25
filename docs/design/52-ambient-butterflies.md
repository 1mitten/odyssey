# 52 — Ambient butterflies, and the night they light

**Built 2026-09-25, not yet played.** Branch `claude/ambient-butterflies`, worktree
`D:\code\odyssey-butterflies`. The owner's request: *"decent looking and moving procedural generated
butterflies (if performant) - if it does hinder performance - make sure it's a graphic setting. Also
could you make them illuminate at night with varying colours to make night time look spectacular."*
Decided in `docs/research/butterflies-interview.md`; researched in `e-13-butterfly-flight.md`,
`d-24-butterfly-swarm-cost.md` and `e-14-butterfly-wings-and-glow.md`. This is design 38 §10's
*"ambient FX near the camera focus"* slot (M12), the first thing in it.

**Numbered 52** because 50 and 51 are held by branches in flight (`50-ambient-birds.md`,
`50-cover.md`, `51-traits-and-mental-health.md`), and research `d-23` and `e-12` are the birds'.

**Read first:** `50-ambient-birds.md` (on `claude/wizardly-ride-wely6j`, PR #230), whose shape this
follows on purpose; `43-weather.md` §7 (the rain pass this borrows its draw from);
`38-meadow-overhaul.md` §9 (the presets) and §17 (the flower dressing a butterfly lands on);
`06-rendering-and-camera.md` §6c (what a submission costs, and bloom's threshold).

## 1. What a player sees

| When | What |
|---|---|
| **By day, over grass** | Butterflies near wherever the camera looks — about 200 on the default rung over open meadow — in six kinds of colouring, every one patterned differently. They flutter in short, jinking legs, alternate bursts of beats with sinking glides, and come down on the grass (a flower where one is near) to rest with their wings closed upright; a third of them bask, opening and closing slowly. |
| **A colonist walks through** | Any butterfly within 2.5 m of a colonist or an animal goes up at once and flies off, climbing, for a second or three. |
| **Dusk into night** | As the sun goes down the pattern on each wing — its eyespots, band and marginal spots — begins to glow, each butterfly in its own hue (cyan, magenta, amber or violet, wandering slowly), breathing brighter and dimmer about every five seconds. A soft halo hangs round each one, and it throws a pool of its colour on the grass beneath it, and on a colonist or a wall that stands in it. Zoomed right out, the wings are too small to draw and the halos go on: a meadow of coloured lights. |
| **The year** | Fullest in Tansy, thinning through Glare, **none in Rime**. |
| **The weather** | Cloud thins them to six in ten. Rain sends them away — none at all once it rains at three-tenths — and they come back when it clears. |
| **Anywhere else** | None indoors, over a floor or roof, over water or rock, or in a growing zone. Looking down into a mine shows none of the surface's. |
| **Paused** | Every butterfly holds in the air where it is. |

Nothing a butterfly does is in a cell, a save, the state hash or the pawn registry, and the
simulation does not know they exist. They cannot be clicked, selected, hurt or caught.

## 2. The shape of the unit (after the birds)

| Piece | Where | What |
|---|---|---|
| `ButterflyMeadow` | `Odyssey.Hud` (engine-free) | The state machine and the window round the focus. The fast tier runs it (`ButterflyTests`). |
| `ButterflyPalette` | `Odyssey.Hud` | Every colour drawn: six species, four glows, the pulse and the glow ceilings. **The one owner** — the shader writes no colour of its own. |
| `IButterflyHabitat` | `Odyssey.Hud` | The seam: may a butterfly be here, how high is the ground, are there flowers. |
| `ButterflyHabitat` | `Presentation/World` | Answers the seam from the render mirror, column by column, forgetting a column when its chunk changes. |
| `ButterflyDirector` | `Presentation/World` | Steps the meadow, packs one structured buffer, sets the palette once, submits the calls. |
| `Odyssey/Butterfly` | `Presentation/Shaders` | Every vertex from the buffer by id; the flap, the pattern, the glow. |

The birds (design 50) set this pattern: an engine-free model behind a seam the mirror answers, a
frame section of its own, the shader kept alive for the player build. It is followed so the two
ambient passes read alike, and so the day petals, leaves or dust join design 38 §10's slot they have
a template.

## 3. How a butterfly moves

A small state machine, because a path computed from a seed and a clock cannot remember having been
startled (e-13). Every number is INVENTED inside e-13's envelope unless it cites one, and is the
owner's to tune.

| State | What it does | Numbers |
|---|---|---|
| **Flying** | Short legs, a new heading at the end of each — usually within ±1.2 rad, one leg in seven turning hard. Bursts of 3–8 beats alternate with 0.3–1 s glides that sink. Looks 1.8 m ahead one step in four and turns hard away from anything that is not grass or is outside the window. Drifts with the wind. | 0.8–1.6 m/s (e-13 §3); legs 0.3–1 s (§14); 0.6–2.6 m up; glide sinks 0.35 m/s |
| **Landing** | After 4–14 s aloft it tries six places within 6 m, taking the first with flowers (design 38's own dressing, `MeadowDressing.SmallPiece`) or else the first on grass, and makes for it, descending in the last metre and a half. | 8 s to arrive or it gives up |
| **Resting** | On the tufts, 0.3 m above the ground, wings closed upright (e-13 §7); one in three basks, opening slowly. | 2–20 s |
| **Fleeing** | A walker within 2.5 m horizontally and 3 m vertically: up and away at once, climbing to 3.5 m. | 2.6 m/s for 1.5–3 s (e-13 §8: ~3 m) |
| **Leaving** | Fading out over half a second: strayed past 1.15 × the window, or the meadow has more than it wants. | |

**The window.** The meadow lives round the camera's focus, not over the board: a radius of 0.75 ×
the camera distance, held between 18 and 80 m (36 m at the default 48 m, about the view's
half-height). How many live is

> rung × habitat share of the window × min(1, (radius / 36 m)²) × season × weather

so zooming in keeps the density and shows fewer, zooming out spreads the rung's count over more
ground, and a city block or a mine view has none. The habitat share is sampled, 24 points a frame
and 128 when the focus jumps. The first population is dealt whole, at rest and fading in; after that
a few a frame, so a pan refills the window over a handful of frames and nothing pops.

**Season** (`ButterflyMeadow.SeasonFor`): 0.8, 1.0, 0.85, 0.6, 0, 0 at the middle of Larkspur,
Tansy, Bramble, Ember, Hollow and Candle, blended across each month and forced to nought in Rime.
**Weather** (`WeatherFor`): `(1 − 0.4 × cloud) × (1 − rain / 0.3)`, clamped, read from
`WeatherLook`'s eased terms so a spell arriving thins them as it darkens the sky.

**The wind** is the grass's (`_OdysseyWind`), a quarter of its push as drift.

## 4. Where a butterfly may be

`ButterflyHabitat` walks a column down from the top of the drawn world to the first thing that stops
the sky. It is habitat when that is a **solid cell whose drawn terrain is grass with nothing built
on it** — the grass tufts' own rule (`ChunkMesher.EmitScatter`), asked of the top of a column. The
drawn terrain, so a growing zone (drawn as tilled earth) is not habitat; "built", so a tree or a
bush is meadow, as it is for the tufts. A floor, a roof, water or rock met first is not, and its
height is what a butterfly crossing it flies over.

The ground height adds `GroundRelief.HeightAt`, as a standing pawn's does (`PawnPose`).

**A column is walked once and remembered** until its chunk's version moves: `SyncDirty` is one
integer comparison per chunk a frame (the rain's `SkyHeightMap` trick), and a changed chunk forgets
its columns, walked again only when a butterfly asks. Nothing is walked that nobody flies over, so a
Huge board costs what the window costs.

## 5. The night

The glow rises with the sun's elevation (`Daylight.Sample(hour).SunElevation`), not with a clock
hour: nothing at 4° above the horizon, everything at 8° below — so it comes up with the night grade
(`ButterflyPalette.NightFor`).

**The palette** (owner: full spectrum, weighted to cyan, violet, magenta, amber; e-14 §5):

| Glow | Colour | Share | Gain |
|---|---|---|---|
| Cyan | `#5CF2FF` | 0.30 | 1.0 |
| Magenta | `#FF6EDC` | 0.25 | 1.15 |
| Amber | `#FFA61E` | 0.25 | 1.0 |
| Violet | `#8A4CFF` | 0.20 | 1.5 |

Violet carries half again because it sits 45° from the night's blue and has little luminance; magenta
was lifted from `#FF3CC8` when the colour-blind test found it only 17 Lab units from the night sky
under deuteranopia. Each butterfly's hue wanders ±25° over 30–60 s, and its brightness is dealt
between 0.8 and 1.2 so no two differ by hue alone (e-14 §6).
**`ButterflyTests.NoTwoGlowsLookAlikeToAColourBlindPlayer`** holds every pair at least 14 Lab units
apart and every glow 25 from the night sky, in normal vision and under all three dichromacies (the
`StorageThemeTests` simulation).

**The breath**: 0.15–0.25 Hz, a quick rise and a slow fall, **never below 55%** — a glow that goes dark
reads as blinking, a signal; one that only dims reads as alive (e-14 §7).

**Three tiers of light, and why the bright one is not on the wing.** e-14 wanted the pattern elements
at 2.5–4 × the colour, over bloom's 1.1 threshold. d-24 §7 found that a region a few pixels across,
halved again by bloom's half-resolution prefilter, crosses the threshold on some frames and not
others as it moves — the firefly artefact — and bloom's clamp is global and would dim the sun glints
bloom was adopted for. So:

1. **The wing** glows at night, the pattern elements fully and the ground faintly, the veins not at
   all, so it reads as stained glass — but never over `WingGlowCeiling`, 1.0.
2. **The halo** is drawn by the pass itself: a soft disc round each butterfly peaking at 1.6, never
   under 2.5 pixels in radius, so the prefilter always sees it whole. Hidden where the scene stands
   in front of the butterfly.
3. **The light** is the pool the owner asked for, and it is more than a pool: the same quad, 2.2 m
   in radius, reads the depth texture, rebuilds the surface behind each pixel and adds the
   butterfly's colour by its distance — so it falls on the grass, and on a colonist or a wall that
   stands in it. **Never a URP light**: Forward+ caps a camera at 256 and every light costs per
   pixel at a GPU-bound 4K (d-24 §6). It survives bloom being off.

## 6. The wings

**No mesh.** The body and four wing panels are tables in the shader and a vertex is picked by its
id: four wings of six fan triangles and a body of eight faces, 96 corners
(`ButterflyDirector.VertsPerButterfly`, `BUTTERFLY_VERTS`; `ButterflyDirectorTests` reads the shader
to hold them to one number). Real faceted geometry rather than alpha-clipped quads, because MSAA is
off and a clipped edge would shimmer (e-14 §4). Each butterfly's outline is its own within 8%.

**The flap** (e-13 §6): up to 62°, down to −48°, the downstroke 42% of the beat, the hindwing 7% of a
beat behind, each butterfly its own phase and 8.5–10 beats a second — **never over ten**, or it
strobes at sixty frames (e-13 §13; `ButterflyTests.TheWingbeatNeverStrobes`). A glide holds the
wings at 12°; a rest closes them to 86°, or for a basker opens and closes them between 86° and 8°
over five to nine seconds. The body rises on the downstroke and falls on the up (e-13 §4), banks
into a turn and noses up into a climb.

**The pattern** is the nymphalid ground plan (e-14 §1) in wing-polar coordinates — `r` out from the
root, `θ` across — interpolated exactly across each fan triangle by carrying `(r·θ, r)`. Five to
seven wing cells between veins; a band (dark, or in the accent for the red admiral); eyespots on the
cell midlines by chance, pale focus, coloured disc, dark ring; a dark margin, with pale spots for
four of the six kinds; a dark root; veins last, through everything. `step()` throughout, so it is
posterised in the Synty manner. The six kinds are e-14 §2's table: monarch, morpho, cabbage white,
swallowtail, red admiral, peacock — descriptive names in code only, never drawn.

**The size**: 0.36 m across (`ButterflyDirector.Span`), four to six times life, because a real 7 cm
wingspan is a pixel at the default camera (e-13 §12). It grows to 1.6× between 30 and 90 m of camera
distance, the birds' rule (design 50 §4), and shrinks out between 80 and 110 m — where the halos
carry the night on.

**Flat-shaded** from the screen-space derivative of the world position, lit two-sided by the sun and
the trilight ambient, fogged.

## 7. The clock

**Real seconds while the world runs, none on a pause.** The one place this unit departs from the
birds and the rain, which run on game time so that speed 3 rains three times as hard. A butterfly
beats ten times a second; at speed 3 that is thirty, which strobes at sixty frames (e-13 §13). So
the caller passes `Running ? Time.deltaTime : 0` — the blood's and the floaters' rule — and a pause
holds every wing mid-stroke. The hour, the season and the weather are the game's, so the night and
the year still follow the game's clock.

A step longer than a fifth of a second is not taken in one: at most four steps of 0.05 s, so a hitch
cannot fling a butterfly across the board.

## 8. The setting

**Graphics → Detail → Butterflies**: Off / Few / Many / Swarm, the rungs 0 / 80 / 200 / 500,
default **Many** (owner: *"lively, about 150–300"*). `GraphicsLadder.Butterflies`, appended last so
no stored rung moves, drawn in the Detail group under the two grass ladders, which the settings
window's fixed frame has room for (464 of 502 px; `SettingsLayoutTests`). A press resizes a few arrays,
once; it re-meshes nothing (`NeedsRedraw` false) and costs no hitch. Off steps and draws nothing.

**The presets own it**: Low → Few, Medium and High → Many (High is what ships, so a new machine still
reads as High), Ultra → Swarm. Provisional until §9's measurement.

The row's name and description are `ui.settings.butterflies` in `icon-keys.csv`, and the wiki was
regenerated with it.

## 9. Cost

Two calls whatever the count, one by day (P10): the buffer is written once a frame from the model's
own arrays and every vertex is built from it. CPU is the step — O(live × walkers / 4) — and the
upload of 64 bytes a butterfly.

**Measured:** *(the numbers from `FrameTimeTests.TheButterfliesAgainstTheFrame`, and the preset rungs
they set, go here.)*

## 10. The build

`Odyssey/Butterfly` is found by name, so it is in `ShaderInclusion.Required`, in the always-included
list (`ProjectSettings/GraphicsSettings.asset`) and has a keep-alive material in
`Assets/Resources/OdysseyKeepAlive/` — all three, or a player build draws no butterflies and says
nothing (the 2026-09-19 lesson). It reads a `StructuredBuffer`, so it is `#pragma target 4.5`.

## 11. Not to undo by tidying

- **The palette's one owner.** A colour written into the shader is a second copy the colour-blind
  test does not see.
- **The wing's ceiling at 1.0.** Raising it for "more glow" brings back the shimmer d-24 §7 found;
  the halo is where more glow goes.
- **Real seconds.** Moving the butterflies to game time makes speed 3 strobe.
- **The zero-target margin.** The surplus margin is nought when the target is nought — without that,
  two butterflies stayed out in every downpour for ever (found by `RainSendsThemAway`).

## 12. Not in this unit

- **Petals, leaves, dust** — design 38 §10's other ambient kinds. `FrameSection.Butterflies` is named
  for what it measures; a shared section is a rename when the second kind arrives.
- **Moths drawn to lamps** (interview answer 8, the option not taken).
- **A butterfly on a crop** — a growing zone is not habitat today; flowering crops are the case.
- **Birds taking butterflies**, or butterflies over the surround beyond the board.
