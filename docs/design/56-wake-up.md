# 56: Waking into the world

**Status (2026-09-26): built, not yet run in Unity or played — PR #241, branch `claude/nice-einstein-cwicw1`,
stacked on PR #240, `claude/load-curtain` (merge that first).** The engine-free model and its setting are
proven in the fast tier (24 tests); the engine half — the veil, the blur, the grade, the filters,
the camera and the clock gate — was written in a container with no Unity and **has not been
compiled**. §11 is what is owed before it can merge.

## 1. The ask

The owner, 2026-09-26: *"Can you make the transition between the main menu and the game smoother as
there is a touch of loading up. Could you gently fade out of the main menu but then loading up the
world but when the in game world appears — apply a cinematic effect like a dream effect that is
blurred and the sound is closed and muted and distant and then everything eventually comes into
focus from a sleep effect almost but it would help load up the world with this kind of transition
and seem seamless."*

Asked and answered the same day:

| Question | Answer |
|---|---|
| How long, first blurred picture to fully in focus | **About 5 s** |
| Extras | **The camera settles**; **the game holds paused until the eyes are open**. Not eyelid blinks, not a breath or heartbeat |
| Which journeys | **New game and Load**, both from the menu |
| Mood | **Soft and warm** — a gentle glow, a little faded, fitting the golden hour |

Added without asking, because both are the practice everywhere this is done: **any key or click wakes
you at once** (a player sees this on every load), and **a switch turns the dream off** (blur is a
motion-sickness trigger for some players, and a tenth viewing is not a pleasure).

### 1a. What it replaced

Pressing Start built the world **in one synchronous frame** (0.4–1.5 s, longer on Huge) inside the
button's click handler, so the setup page sat **frozen** for that long — design 38 §25c had it as
owed. Then the starfield covered three frames (§25b's curtain) and the world appeared with a hard
cut. A **load** was not covered at all (fixed separately first, `docs/bug-patterns.md`, "An event
raised twice in one frame"), and the menu's bed **cut** rather than faded on a long build, because
its fade ran on the unclamped real-time delta.

### 1b. Does it exist elsewhere

Waking up is a well-worn opening: Skyrim's cart, Fallout 4's cryo pod, Half-Life 2, Far Cry. The
colony sims do not do it — RimWorld and Frostpunk fade in from black. The version that works has
the same parts everywhere: hide the load behind something that is not frozen; wake in layers
(sound before sight, focus last); let any input end it.

## 2. The passage

`Odyssey.Hud.WakeTransition` (engine-free, fast tier) owns every rule and curve;
`HudShell.Wake.cs` applies them.

| Phase | Length (`WakeTiming.Standard`) | On screen | Sound |
|---|---|---|---|
| **Closing** | 0.6 s | the warm-black veil rises over the menu | the bed fades; in the dream, the muffle closes with the veil |
| **Dark** | 2 frames | black | muffled |
| **Building** | 1 frame (the long one) | black; the world is built and the interface attaches, unseen | muffled; the audio thread keeps playing |
| **Covered** | 3 frames | black; the world's first submits, unseen | muffled |
| **Waking** | 5.0 s | the dream clears (below) | the muffle opens |
| **Done** | — | the game | the game |

A skip goes to **Rushing** (0.25 s, everything left falls to nought together). With the switch
**Off**, **Lifting** replaces Waking: the veil fades in 0.25 s and nothing else happens.

**The curves** (u = 0 to 1 of the five seconds; every output falls, every one is exactly nought at 1):

| Output | Clears by | Curve | Why |
|---|---|---|---|
| Veil | 0.8 s | smoothstep to u = 0.16 | the eyes open |
| Muffle | 3.5 s | smoothstep to 0.70 | hearing comes back before sight |
| Haze (the warm grade) | 4.25 s | smoothstep 0.05 → 0.85 | the glow lingers |
| Settle (the camera) | ~4 s to under 1% | (1 − u)³ | most of the drift under the veil and the blur, landing softly |
| **Blur** | **5.0 s** | smootherstep 0.10 → 1 | **focus arrives last** |

`WakeTransitionTests.HearingComesBackBeforeSightAndFocusArrivesLast` holds that order.

**The clock is clamped** to 50 ms a step: the build frame is a second long and the frame after it is
the GPU meeting the world; unclamped, either would eat a fifth of the dream in one jump.

## 3. The request moves, the hand-over does not

Design 38 §25b measured that the world must be handed over — the interface attached and laid out —
**in the build frame, behind an opaque cover**, because delaying the hand-over moved the first layout
onto the frame the player saw (a 43 ms reveal). That is kept exactly. What moved is the **request**:
the build is asked for only once the screen has been **drawn black twice**, so the long frame
freezes on black rather than on the menu.

**Two dark frames, not one** (`WakeTiming.DarkFrames`): the frame that finished closing may still be
being presented while the next frame's `Update` blocks on the build, and a build started on a frame
that never reached the screen freezes whatever was on it — which is the complaint.

The bootstrap is untouched: `BuildSession` and `LoadSession` are still synchronous and still called
exactly as before, from a closure `BeginWake` holds until `BuildDue`. The bench and the tests that
call them directly keep the older three-frame curtain (`CurtainFrames`), which still exists for that
path; `CurtainUp` is true for either.

## 4. The look

**The veil** is `_curtainPane`, now warm black (`rgb(6,5,4)`, `.curtain`) instead of a second copy of
the menu's starfield — the dark behind closed eyes. Its opacity is the veil; it is pickable while the
wake holds input, so a click on it is a skip and nothing else.

**The interface fades in with the hearing** (opacity = 1 − muffle). Opacity only: it is displayed
and laid out from the build frame, under the veil, as §25b requires.

**The grade** is `WakeVolume`, a runtime volume at priority 60 (above the storm's 50) weighted by
Haze. Targets, not offsets — at weight 0 it is exactly the golden hour:

| Override | Dream | Golden hour |
|---|---|---|
| Post exposure | 0.55 | 0.15 |
| Saturation | −30 | 4 |
| Contrast | −18 | 6 |
| Colour filter | (1.00, 0.90, 0.78) | (1, 0.97, 0.92) |
| White balance temperature | 22 | 7 |
| Bloom intensity / threshold / scatter | 2.4 / 0.6 / 0.8 | 0.9 / 1.1 / 0.65 |
| Vignette intensity / smoothness / colour | 0.45 / 0.8 / warm brown | 0.16 / 0.4 / black |

**Only the parameters that move are overridden.** `Add<T>(overrides: true)` would override every
one, and URP switches a parameter that cannot blend (bloom's dirt and downscale, the vignette's
shape) the moment the weight passes nought — the golden hour's choices would be swapped for URP's
defaults on the first frame of the dream. No new pass: all four run in the uber post pass already
paid for.

### 4a. The blur is ours, not URP's depth of field

The plan said URP's Bokeh depth of field, with a probe shot to decide against Gaussian. **URP's own
constants decided it without the probe**: Bokeh's radius is capped at `GetMaxBokehRadiusInPixels`,
twenty pixels of the screen's height, and Gaussian's `maxRadius` at 1.5 — at 4K either is "a soft
picture", not "asleep". Depth of field has also never been used in this project, so it would have
been stripped from a player build.

`WakeBlur` is a **dual-filter blur** (`Odyssey/WakeBlur`): halved down a chain and doubled back up,
then laid over the camera colour with alpha = √Blur. The chain's depth comes from the target's
height (`LevelsFor`: 480 lines → 3, 1080 → 5, 2160 → 6), so the blur covers the same share of the
screen at every resolution. The taps spread with the strength (0.6–1.6), so the blur **shrinks** as
it fades — a focus pull rather than a cross-fade between two pictures.

**Injected, not a renderer feature.** It enqueues its pass from
`RenderPipelineManager.beginCameraRendering` for the rig's camera only, and unsubscribes when the
wake ends, so nothing is added to `PC_Renderer.asset` and nothing runs — not even an early return —
once the player is awake. **Before post-processing**, so a blurred bright patch still blooms (the
dream's glow) and the golden hour grades the blur as it grades the world. The shader is in
`ShaderInclusion.Required`.

## 5. The sound

`WakeHearing` puts an `AudioLowPassFilter` and an `AudioReverbFilter` on the **listener's** game
object, which processes the whole mix — no source sets `bypassListenerEffects` — and lowers
`AudioListener.volume`, a global nothing else in the game touches:

| Muffle | Cutoff | Gain | Room |
|---|---|---|---|
| 1 | 500 Hz | 0.35 (−9 dB) | −600 mB |
| 0.5 | 3.3 kHz (the geometric mean) | 0.68 | −1200 mB |
| 0 | 22 kHz | 1 | −10000 mB (off) |

The cutoff is log-spaced because an ear hears octaves; the room falls 20 dB a decade of muffle, so it
drains away rather than switching off. The filter goes on **at the press**, so the menu's bed is
muffled as it leaves.

**The bed leaves at the press** (`OdysseyBootstrap.MenuLeaving`), not when the world exists: by the
build it has faded by about 15 %, and the rest crosses the outdoor bed's 4 s arrival under the muffle
— still "cross, don't queue" (design 17 §12). Its fade step is clamped to 0.1 s (PR A).

**ADR 0010 is amended, not replaced**: a filter over the whole mix for five seconds is master-bus DSP
through Unity's supported non-mixer route; the trigger for a mixer asset — a bus with DSP of its
own — still stands.

## 6. The camera settles

`SliceCameraRig.SetSettle` adds **+10° of pitch, −6° of yaw and +35 % of distance** at the start of
the dream, falling as (1 − u)³. **It is added to what is drawn and never to the rig's state** — the
targets, the smoothing, `FocusOn` and a load's `RestorePose` all go on meaning what they meant, and at
a settle of nought the camera is exactly where it would have been. Pitch is clamped to 20–85°.

## 7. The clock is held — a gate, never a speed

`OdysseyBootstrap.ClockHeld` makes the tick loop read speed 0 while it is set, from the build to the
end of the dream. **It never writes a speed**: no intent, no `SpeedControl.Resolve` or `Remember`. So
the hold cannot be saved, cannot be remembered as the player's pause, and cannot reorder against a
load's own speed restore — `RefreshAfterLoad`'s `SetGameSpeed` intent waits in the queue through the
hold and is applied by the first tick after it, so **a colony saved at triple speed wakes at triple**
(`WakeUpTests.ALoadWakesAtTheSpeedItWasSavedAt`).

Rejected: pausing with a `SetGameSpeed 0` intent and restoring it — it writes simulation state, moves
`SpeedControl`'s memory, reorders against the load's restore and could put a paused colony in an
autosave.

**Known and accepted:** the speed control shows the colony's own speed, not "paused", during the
hold. The veil and the blur already say "not yet".

## 8. Skipping

Any key, click or wheel while the wake holds input is a **skip and nothing else**. The veil takes
the pointer (a click on it never reaches the world); `HotkeyDirector.Suspended` closes the game's
keys; `SettingsPresenter` returns while `WakeHoldsInput`, so Escape does not open the settings over a
colony nobody can see. The keys come back only once the wake has let go **and no key is held**, so a
key held through the skip does not start a pan.

A skip while the menu is closing or the screen is dark is ignored — the press is committed and there
is nothing yet to wake into. Behind the cover it is kept and taken when the veil opens. In the dream
it releases the clock on that step and clears everything left in 0.25 s.

`anyKey` is read by name, the fourth unbindable read `HotkeyClashTests` allows: it names no key, so
it binds nothing and clashes with nothing.

## 9. The setting

**Settings → Interface → Camera → Wake-up**, On / Off, default **On** (`ui.settings.wake`, stored under
the same key, reset by *Reset Interface*). Not a `GraphicsOption`: those belong to the quality presets,
and this is not a quality lever. Read at the press, so switching it changes the next passage.

**Off still fixes the frozen menu**: fade out, black, build, cover, a 0.25 s fade in. No blur, no
grade, no filter, no settle, no hold.

`-odyssey-newgame` and `buildOnPlay` never go through the menu and so never wake: tests and the bench
stay immediate and measurable.

## 10. Cost

**Not yet measured.** The blur is 2 × levels + 1 full-screen blits, all but the last at half
resolution or smaller, for five seconds; the grade adds no pass. The hitch tour now enters the world
through the wake (`PlayerBench`, "New game pressed: the fade, the build behind the black, and the
wake", 7 s) and a second into the dream writes `Logs/wake-mid.png` and a line saying whether the blur
shader was found and how often its pass ran — **the proof, in a player build, that the shader
survived stripping**. The number goes here with its machine, resolution and date.

## 11. Owed before merge

1. **Compile it in Unity.** Written with no editor; the fast tier compiles neither Presentation nor
   the tests under `Tests/PlayMode`.
2. **Both Unity tiers**, alone on the machine. New: `WakeUpTests` (four), `StartScreenTests`'s curtain
   test rewritten for the deferred build and `ALoadedWorldIsCoveredLikeANewOne` (PR A).
3. **A player build and the hitch tour** at 4K: the mid-wake picture and the "available True" line.
4. **The owner's first look** — the playtest rows.

**2026-09-26.** Compiled and played in the editor — owner: *"perfect"*. The Unity tier's one
failure was `EveryKeptShaderAlsoHasAnInstancingKeepAliveMaterial`: `Odyssey/WakeBlur` went into
`ShaderInclusion.Required` without the keep-alive material every required shader carries, so
`InstancingKeepAlive.Apply` generated `Odyssey_WakeBlur.mat`.

The four `WakeUpTests` then failed on the runner, each with the passage still in `Closing`:
they waited 600 *frames* for a passage timed in real *seconds*, and a batch run with only the menu
on screen and no frame cap is through 600 frames before a 0.2 s close has run. They wait on a
fifteen-second real-time deadline now (`WakeUpTests.PassageSeconds`) and pass. The code was right;
the test counted the wrong unit — the same trap as "a frame is not a tick" in `docs/lessons.md`.

**The player build refused** until `Odyssey/WakeBlur` was in the committed always-included list
(`ProjectSettings/GraphicsSettings.asset`, by `ShaderInclusion.Apply`): the code's `Required` list and
the project setting are two things, and only a player build compares them. Built, the hitch tour at
3840 x 2160 from the title screen logs *blur shader available True, pass enqueued 162 times* and a
mid-wake picture that is fully blurred and warm. Its two slow frames are the build frame (742 ms,
behind the black, where it belongs) and the 4K screenshot itself. The capture had been written to a
relative `Logs/wake-mid.png`, which a player does not resolve against its working directory and
drops without a word; it goes beside the `-logFile` now. Every item above is closed.

## 12. What not to undo by tidying

- **Two dark frames**, and the build **requested** late but **handed over** in its own frame (§3).
- **The blur and the grade exist before the build**, so the first world frame is already the dream.
- **The settle is never written into the rig's state** (§6).
- **The hold is never a speed** (§7).
- **The interface is never hidden during the wake, only faded** (§4, design 38 §25b).
- **Only the moving volume parameters are overridden** (§4).
- **Everything is put back** on Done, on an abort (a build that throws or builds nothing), and on the
  shell being disabled or destroyed (`ReleaseWake`) — a muted listener, a held clock or shut keys
  outliving the wake would be the worst bug this could have. `WakeUpTests.AnythingLeftBehind` is the
  check.

## 13. Rejected

| Alternative | Why not |
|---|---|
| URP depth of field (Bokeh or Gaussian) | radius capped at 20 px / 1.5, so faint at 4K; stripped from a player build (§4a) |
| A screenshot of the frame, blurred and faded | freezes the colony that is meant to be arriving; no settle, no live glow |
| An AudioMixer with a snapshot | ADR 0010's trigger is per-bus DSP; a whole-mix filter needs no mixer |
| Pausing by speed | §7 |
| Hiding the interface until the end | §25b's 43 ms reveal |
| Eyelid blinks, a breath or heartbeat | declined by the owner |
| A loading screen with stages | nothing to report: the build is one frame (§25b) |

## 14. For the keyboard

| Test | Look for | A wrong answer looks like |
|---|---|---|
| New game from the menu | a fade to black, a beat, then waking — soft, warm, muffled — and in focus at about five seconds | the menu freezes before the black; or you reach for a key every time because five seconds is long |
| Load a save from the menu | the same, landing on the saved view at the saved speed | the camera jumps at the end, or the colony wakes paused or at the wrong speed |
| Watch the warmth | a warm glow that belongs with the golden hour | it reads as a filter, or overexposed, or orange |
| Listen through it | the menu drone gone under the muffle, the meadow opening as the blur clears | a gap of silence, or the two beds stacking loud |
| Click during the dream | clears in a blink, and the click does nothing else | a selection or an order from that click |
| Settings → Interface → Wake-up Off, then New game | a plain fade from black | any blur, muffle or camera drift |
