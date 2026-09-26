# 63 — Clouds

**Built 2026-09-26, not yet played**, branch `claude/clouds`, worktree `D:\code\odyssey-clouds`.
The owner's request: *"do any of the synty packs contain clouds or can use toon volumetric cloud —
happy to use what synty has if effective from their packs — as I notice the clear sky at least
looks bare … keep it performant."* Four answers settled the shape before any code: **the Meadow
pack's own `Clouds` shader graph on its cloud rings**, not flat cloud props and not a shader of our
own; **cover follows the weather**; **a slow drift**; and **the birds' budget**, under 0.1 ms.

## 1. What a player sees

A band of toon cumulus round the horizon, 1° to about 15° up, drawn by the Meadow demo's two cloud
rings. Under a clear sky it is a low, flat band; as the sky clouds over it stands up into banks, and
rain stands it taller and heavier without taking the day's colour, and a storm makes it a slate deck
darker than the sky. It turns slowly on game time: paused, the sky holds; at speed 3 it turns three
times as fast; in a storm the wind drives it harder. Its colours are the day's: white at noon with
blue-grey undersides, warm cream at dawn and gold at dusk. **By night there are none**: after the
golden hour they sink and dissolve into the sky, and they come back before dawn's (§4c).

**Where it is seen.** The colony camera at its usual 48° sees no sky at all (its top edge looks 24°
down), and at its lowest pitch, 20°, the top edge is level with the horizon and the surround's haze
fills it. **The sky is the ride camera's** (design 57): 60° of lens at eye height, looking a few
degrees down by default and up to 40° up. That is where the bare sky was, and where the clouds are.

Nothing here is in a cell, a save, the state hash or the picker.

## 2. What the packs had

A census of `Assets/Synty` on 2026-09-26, every cloud and sky asset in the eleven packs:

| Kind | Where | Why not / why |
|---|---|---|
| **`Clouds.shadergraph`** + `Synty_Clouds_Meadows.mat` | `PNB_Core`, `PNB_Meadow_Forest` | **Chosen.** A toon two-tone cloud shader with a rim, built for the rings below. |
| **Cloud rings**: `Env_CloudRing_Larger_01_Smooth_03` (Meadow), `SM_Env_Cloud_Ring_01/02`, `Env_CloudRing_Larger_02` (Core) | `PolygonNatureBiomes` | **Chosen**: the Meadow demo's own pair. |
| Flat cloud props, `SM_Generic_Cloud_0N` / `SM_Env_Cloud_0N` | Battle Royale, Farm, Generic, Sci-Fi City, Shops, Western | Owner chose the rings. Single puffs, a draw each unless instanced. |
| Skydomes (`SM_Env_Skydome_01`, `SkyDome.fbx`, `SkyDome.shadergraph`) | Core, Generic, Sci-Fi City, Shops | A gradient on a dome; our `Odyssey/GradientSky` already is one. |
| `WeatherControl.cs` | `PNB_Core` | Read for intent only; drives the demo's own scene. |

The Meadow demo places **both rings with the Meadow material**: the Core ring at 106 m, scale 8.8;
the Meadow ring at 58 m, scale about 10 × 7.2 × 10. Measured by `MeadowLookBuilder` in their own
units:

| Ring | Size (m, scale 1) | Inner radius | Triangles | Vertices |
|---|---|---|---|---|
| Meadow low ring | 308 × 54 × 291 | 82.3 of 153.9 | 18,556 | 16,464 |
| Core high ring | 277 × 26 × 259 | 75.0 of 138.7 | 14,456 | 18,378 |

## 3. Placement (`CloudDeck`, engine-free)

**Round the camera.** Both rings are centred on the camera's own x and z every frame, so nobody
reaches the edge of the sky, and their height is the world's, so an eye on the ground looks up and a
camera over the board looks across. The mesh's own middle and lowest point are translated onto the
camera's axis and the base, so a ring's off-centre pivot does not show.

| | Low ring | High ring |
|---|---|---|
| Horizontal scale | 10 (radius ~1,540 m) | 8.8 (~1,220 m) |
| Base above the board's top | 20 m | 68 m |
| Height scale, clear → full cover | 3.0 → 7.2 | 4.4 → 8.8 |
| Cover they stand to | the sky's cloud + 0.35 × rain | the same |
| At the last of the night fade | flat, at the eye's height | the same |
| Drift | 0.05° a game second | 0.6 of that |

**Within the far clip.** The play camera's far plane is 1,800 m. The low ring's furthest point is
about 1,600 m from the eye at full height, so nothing is clipped; a larger spread would be.

**Cover is height, not opacity.** The pack's shader is opaque, and its "Cloud Strength" is a
vertical wobble (`y += sin(x + z + time·speed)·strength`), not a density. So cover is the rings'
vertical scale, eased by smoothstep: continuous, never a pop, and nothing new in the shader. Clear is
deliberately not zero, because the owner's report was the clear sky.

## 4. Colour (`CloudColours`)

The pack's material is painted for one pink sunset; ours has a whole day. So the three colours the
shader takes are worked out from the `DaylightState` the frame is lit by, **after** `Overcast` has
graded it, so a grey day greys the clouds with everything else:

- **Top** (the faces turned to the sun): white tinted by the sun, more at low sun; as the light
  goes, the horizon lifted ×1.3 and half-drained to grey, moonlight rather than a blue lamp.
- **Underside**: the horizon leaning 35% towards the zenith; never brighter than the top.
- **Rain**: the top to 75% and the underside to 55% of their dry brightness at full rain, hue kept.
- **Storm**: the top to 85% and the underside to 58% of the sky's own brightness, cool greys (§4d).
- **Haze**: both 30% towards the horizon, the air in front of a ring at one range (§4b); a fifth of
  that at full gloom, or the storm's greys haze back up to the sky.
- **Rim**: halfway from the top to the horizon when calm; halfway from the top to the underside in a
  storm.
- **Presence**: every colour eased towards the sky behind the band as the night comes (§4c).

**The sky behind the band** is `Horizon` leaning 38% towards `Zenith`: the band's middle is about 7°
up, and `Odyssey/GradientSky` weights the zenith there by sin(7°)^(1/2.2) with the sky material's
falloff of 2.2.

`DaylightDirector` exposes the graded `State` and a `Version` that moves when it is written, so the
clouds rewrite their colours only when the light moved: a dozen times a second at most, one compare
otherwise. A board without the day cycle is coloured by the baked hour through the same grade.

### 4a. The pack's scattering is off

It blends towards its colour by `0.4 + sunDirection·fresnel`, **a vector used per channel**. With a
high sun the green channel's weight goes negative and red and blue rise against it: the first noon
sheet came out lilac (cloud pixel 202, 187, 202 against a sky of 145, 167, 195). The demo's sun
never moves; ours does.

### 4b. The pack's fog is off, and the haze is in the colours

It lerps towards the fog colour by `remap(_Fog_Density) + density` — 0.18 plus the distance fog, and
our exponential-squared haze is about 1 at a ring's range. So the lerp **overshoots past** the fog
colour by 18%, inverting the shading, and at night it drove red and green below nought: the night
clouds sampled at (0, 0, 29), pure blue. With the term off and a constant haze in the colours they
sample at (113, 120, 140). A constant is exact because a ring stands at one range.

URP 17's unlit Shader Graph applies **no distance fog of its own** (`UnlitPass.hlsl` sets
`fogCoord = 0`), which is why a ring 1.5 km out is visible at all in a haze that hides terrain by
800 m.

### 4c. Gone by night

Owner, 2026-09-26, on the first build: *"make the clouds faded or not there at night as we want to
darken things up."* Asked, they chose gone rather than dark silhouettes or faint ghosts, and the fade
after the golden hour rather than with the sun. `CloudDeck.Presence(hour)` is 1 from 06:30 to 19:30
and 0 from 21:00 to 05:00, smoothstepped between, so dusk's gold (19:00) and dawn's (07:00) keep
their clouds whole. As it falls the rings **sink to the eye line and flatten** and every colour
**converges on the sky behind them**. An opaque shader cannot turn transparent, so these two
together are what fading means here (§4e for why they sink all the way). At nought **nothing is submitted**, so a night sky costs
nothing. The hour is the daylight director's, so the bench's held noon and a board without the cycle
both keep their clouds.

### 4e. No snap at either end

Owner, after the night fade: *"I see the clouds snap in from night to evening — slowly fade it in
if possible."* Two causes, both fixed:

- **The fade did not reach nothing.** The rings kept a quarter of their height to the last, coloured
  like the band's sky, and a uniform colour cannot match the gradient sky behind it, so at 21:00 a
  visible band vanished in one frame and at 05:00 one appeared. Now the rings **sink to the eye's
  own height and flatten to nothing** (`CloudDeck.BaseAt`, `Sink`), which is edge-on — a line of no
  thickness on the horizon — and the colour they converge on slides with them from the band's sky to
  the horizon's. Measured with a pixel diff against clouds-off at 20:54 (2% left): **identical**
  looking away from the sun, 4 of 1,440,000 pixels differing towards it, 39 looking up.
- **A jump in the hour snapped.** A skip, a load or a debug change moved the presence in one frame.
  The shown presence now follows the clock's by at most a whole fade per 4 real seconds
  (`CloudDeck.EasePresence`, `JumpFadeSeconds`), on unscaled time so it fades on a pause too. The
  dusk fade's steepest rate at speed 3 is under that limit (`TheLimitNeverSlowsAnOrdinaryDuskEven
  AtSpeedThree`), so an ordinary day is paced by the clock alone. A session's first frame takes the
  hour as it is. The colours follow the eased presence between the daylight's own writes, and are
  rewritten when it moves 1%, counted from the last rewrite, not the last frame, or a slow fade
  creeping under the step each frame would never add up to one.

### 4d. Moodier weather

Same day, same ask: *"stormy/rainy need moodier clouds."* Chosen:

- **Rain heavier, colour kept.** The rule of 2026-09-25 (design 43) holds: rain keeps a clear day's
  colour, so the clouds dim and stand taller but are not greyed.
- **Storm as dark slate, darker than the sky.** The first attempt darkened only the undersides, to
  (95, 99, 109) against a sky of (143, 155, 171), and the storm still read light. **The faces a
  player sees with the sun behind them are the lit tops**, still (186, 191, 197). So the tops go to
  85% of the sky's brightness as well and the deck reads heavier than the sky, which the logged
  colours now show: top (126, 132, 145), underside (89, 93, 103), sky (143, 155, 171).
  `CloudTests.AStormsUndersideIsDarkerThanTheSkyBehindIt` holds the ordering at three hours.

The sheet logs every picture's material colours beside the sky's, which is how the above was found
rather than argued.

## 5. Time

The pack billows its clouds on the wall clock (`_Cloud_Speed` × `_Time`), which would move them on a
paused world and cannot be rescaled mid-phase without a jump, so the copy sets it to nought and the
wobble is a still shape. The drift is a turn of each ring about the vertical, advanced by game
seconds (frame seconds × game speed) times the eased wind (`WeatherLook.Wind`), kept in [0, 360).
The two rings turn at different rates, which is what gives the sky depth.

## 6. Nothing drawn that cannot be seen

`CloudDeck.HighestElevation` is the highest ray in the view — the top edge's middle when it looks
above the horizon, its corners when below — for a camera with no roll, which neither camera ever
has. `LowestElevation` is the ring's lowest point from the eye (its far side when the base is above
the eye, its inside when below; the inside is measured at 0.535 and 0.54 of the radius and taken as
0.5, which draws more often, never less). **When the one is more than 2° below the other, nothing is submitted**: no colour pass,
no depth, no SSAO normals. The colony camera at 48° is 24° below the horizon at its highest ray, so
normal play costs the clouds nothing. Below the surface nothing is drawn either, the birds' rule.

When they are drawn, each ring is one `Graphics.RenderMesh`: the game camera only, no shadow, no
probes, no motion vectors, and **queue 2450**, after the board's opaque geometry, so the terrain in
front of a ring has filled the depth and the ring's hidden pixels are never shaded. The pack's
shader casts shadows by default; a ring whose bounds hold the camera would otherwise be drawn into
every cascade every frame.

The ink outline fades out by 90 m and SSAO by 100 m, so neither touches a ring.

## 7. The licence and the player

**The material is copied, never written** — the sky's rule (`DaylightDirector`): writing colours
into the asset at runtime in the editor would edit the licensed file on disk.
`CloudTests.ThePacksMaterialIsCopiedAndNeverWritten` holds it.

The rings and the material are three more references in `MeadowLook.asset`, the committed asset of
GUIDs into the gitignored packs that `Resources` carries into a player, rebuilt by
*Odyssey > Presentation > Rebuild meadow look*. On a clone without the packs they resolve to null,
`HasClouds` is false and there are no clouds; nothing else changes. If the art resolves and its
shader cannot run, the director says so once in the log. **No keep-alive is needed**: the plan
expected one, but the material reaches the player as a reference from `MeadowLook.asset`, so its
shader goes with it, and the rings are not instanced, so the `INSTANCING_ON` variant
`SyntyInstancingKeepAlive` exists for is never asked for.

## 8. What it costs

Measured 2026-09-26 by `FrameTimeTests.TheCloudsAgainstTheFrame`, the played meadow on Standard, off
/ on / off / on in one run, RTX 5070 Ti:

| View | Resolution | `FrameSection.Clouds` | Calls | Frame, on − off | Noise |
|---|---|---|---|---|---|
| Colony camera, 48° | 640 × 480 | 0.002 ms | 0 | −0.04 ms | 0.03 |
| Colony camera, 48° | 3840 × 2160 | 0.005 ms | 0 | +3.04 ms | 4.14 |
| Eye on the board, ride lens | 640 × 480 | 0.007 ms | 2 | −0.08 ms | 0.49 |
| Eye on the board, ride lens | 3840 × 2160 | 0.010 ms | 2 | −1.70 ms | 3.87 |

**The CPU side is settled**: a hundredth of a millisecond at worst, a tenth of the birds' budget.
The editor's 4K frame could not resolve the GPU side: that run shared the machine with six editor
windows and two batch runs from other worktrees, and the colony view "gained" 3 ms while submitting
nothing, which is the noise showing itself.

### 8a. The GPU, in a player

So the GPU was measured where it can be: `PlayerBench` in a development player, which reads the
GPU's own time (`-odyssey-newgame -odyssey-bench -odyssey-bench-clouds`, windowed 3840 × 2160,
world paused, noon held, RTX 5070 Ti, Direct3D 11):

| Arm | GPU ms | Submit ms | Cloud calls |
|---|---|---|---|
| Colony camera, clouds on (first arm of the run) | 7.18 | 1.35 | 0 |
| Colony camera, clouds off | 4.65 | 1.31 | — |
| Eye at the horizon, clouds on | 5.92 | 1.66 | 2 |
| Eye at the horizon, clouds off | 5.88 | 1.59 | — |
| Eye at the horizon, clouds on again (drift control) | 5.91 | 1.65 | 2 |

**The clouds cost about 0.03 ms of GPU at 4K with the sky filling half the screen**, inside the
spread of the two "on" arms. The colony pair is not a cloud figure: its "on" arm submitted no cloud
call at all and was the first arm of the run, so its 2.5 ms is the player settling. The player's
own picture of the eye's view (`clouds-eye.png`, beside the bench's log) shows the pack's shader
drawing as it does in the editor.

### 8b. The first frame in a fresh editor

The first picture the pack's shader drew in a new editor process came out black on the faces turned
from the sun, and never again: not the colours (logged set), not the fog (off, still black), not the
scattering. `CloudCheck` throws one picture away first. Whether a player sees one black frame of
cloud at the start of an editor session is unknown; a player build compiles its shaders ahead.

### 8c. A player could not be built from `main` that morning

The first `scripts/unity.sh build` on this branch was refused before it began:
`Odyssey/Crack` and `Odyssey/Shard` (design 58, merged earlier the same day) are found at runtime
and were not in the always-included list. `ShaderInclusion.Apply` added them — two lines of
`GraphicsSettings.asset`, kept as a change of its own so it can go to `main` separately.

## 9. Open

- **Overhead stays clear.** A ring is a horizon band; looking up 30° in the ride camera shows open
  sky above it. Cards overhead would be a second technique and a second cost.
- **Cloud shadows on the ground**: `SkyHeightMap`'s texture is the hook if they are ever wanted.
- **A Quality rung.** The clouds are one switch (`CloudDirector.Enabled`) with no setting; at two
  calls and nothing in normal play there is nothing yet for Low to save.
- **How moody is moody enough** is four constants in `CloudColours` (`RainTop`, `RainUnder`,
  `StormTop`, `StormSlate`) and the fade's four hours in `CloudDeck`; the first play decides them.
- **Lightning** would light a storm deck from inside; not asked for.

## 10. Where it lives

| File | What |
|---|---|
| `Assets/Odyssey/Hud/CloudDeck.cs` | Placement, cover, drift, the visibility rule. Fast tier: `CloudDeckTests` (19). |
| `Assets/Odyssey/Presentation/Rendering/CloudColours.cs` | The colours from the graded day. |
| `Assets/Odyssey/Presentation/World/CloudDirector.cs` | The material copy and the two draws. `CloudTests` (EditMode). |
| `Assets/Odyssey/Presentation/Rendering/MeadowLook.cs`, `Resources/OdysseyLook/MeadowLook.asset` | The three references. |
| `Assets/Editor/Odyssey/CloudCheck.cs` | The sheet: `scripts/unity.sh shot Odyssey.EditorTools.CloudCheck.Run` writes 36 pictures to `Logs/clouds-*.png`. |
| `OdysseyBootstrap` | Built with the session, synced after the butterflies under `FrameSection.Clouds`. |
| `FrameTimeTests.TheCloudsAgainstTheFrame` | The editor arm (`Measurement`). |
| `PlayerBench` (`-odyssey-bench-clouds`) | The GPU arm, in a development player. |
