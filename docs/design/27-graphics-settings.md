# 27 — Graphics settings: pacing the frame

*2026-09-20. Owner's request: "we need to add a vsync option (and what others you recommend which
are basic graphics settings)", with the standing constraint, said twice: "make sure to use Unity's
recommended way and be performant".*

## 1. What was missing

The Graphics tab had six rows and all six answered yes or no — shadows, the surround, grass tufts,
ground relief, see-through, the ceiling cut-away. Every one is a decision about *what the board is
made of*. There was nothing anywhere in the game about *what the frame costs*: no VSync, no cap, no
render scale, no resolution, no display mode.

That gap matters more here than it looks. The performance target is a 2022 mid-range laptop
(`CLAUDE.md`, Environment), neither dev machine is that machine, and an uncapped renderer on a
laptop spends its battery drawing frames nobody asked for. A colony sim is also the genre where a
player leaves the window open for hours, which is exactly when a fan that never stops becomes the
thing they remember about the game.

## 2. The seven levers, and why these seven

| Lever | Rungs | Default | Unity |
|---|---|---|---|
| VSync | Off / On / Half | **On** | `QualitySettings.vSyncCount` = 0 / 1 / 2 |
| Frame rate cap | 30 / 60 / 120 / 144 / Uncapped | **Uncapped** | `Application.targetFrameRate` |
| Render scale | 70% / 85% / 100% | **100%** | `UniversalRenderPipelineAsset.renderScale` |
| Anti-aliasing | Off / 2× / 4× / 8× | **Off** | `.msaaSampleCount` |
| Shadow distance | 30 m / 60 m / 120 m | **the asset's own** | `.shadowDistance` |
| Display mode | Fullscreen / Borderless / Windowed | **Borderless** | `Screen.fullScreenMode` |
| Resolution | whatever the screen offers | **the window's own** | `Screen.SetResolution` |

**VSync on.** A colony sim is not won by a millisecond and tearing is the one artefact a player
cannot unsee. **The cap uncapped behind it**, because the cap is what a player reaches for *after*
turning VSync off, so it starts where it does no work. **Anti-aliasing off**: the board is mostly
flat colour and MSAA is the most expensive thing on the page, so it is a choice rather than a
baseline. **Borderless** because it alt-tabs without a mode switch, which matters for a game people
play beside a browser.

**Render scale is the lever that earns its place most.** It is the only one that cuts the expensive
part — the world — while leaving the HUD sharp, because UI Toolkit draws after the upscale. Below
100 the applier names FSR explicitly rather than leaving the upscaler on Auto; the pipeline asset
already carries an FSR sharpness of 0.92, and FSR is the whole reason 85% is worth offering.

**What is deliberately absent.** No texture quality (one atlas). No LOD bias (no LODs). No quality
*preset*: a Low/Medium/High row is worth building once there is evidence which of these actually
moves frame time on the target machine, and inventing the mapping first would be three numbers
nobody measured. `MaximizedWindow` is not offered — it is a macOS idiom and would buy nothing here.

## 3. One ladder, not seven copies

`GraphicsOption` stays exactly as it was: booleans, with pips. The numbers are a second enum,
`GraphicsLadder`, with one table per question — rungs, default, registry key, rung label, rung
tooltip, and whether it costs a hitch.

The alternative was the shape already in the file. The interface scale and the camera speed each
arrived as their own `int[]`, property, setter and event, and by the third it was plainly a copy.
Seven more would have been seven owners of one rule that has to behave identically in all of them:
snap to a rung, write through, raise once. That is the fault `docs/bug-patterns.md` opens with, and
it had already started here — the three ladders in the HUD disagreed about whether a rung is set in
the mono face or the reading one, and nothing said which was the rule.

**Every rung is an `int`, and wherever Unity has a number of its own the rung *is* that number.**
`vSyncCount`, `msaaSampleCount` and `FullScreenMode` are stored verbatim, so the presenter casts
rather than translates and no table here can drift from the API it feeds. The two exceptions say so
out loud: the render scale is a percentage because a ladder of `0.7` would be the only fractional
thing in a file of whole ones, and the cap keeps `0` for uncapped because Unity's `-1` would sort
before 30.

## 4. The one rule that is not a number: VSync eats the cap

Unity ignores `Application.targetFrameRate` whenever `vSyncCount` is above zero. A player who sets
144 behind VSync gets 60 and no explanation, and the panel would be telling a lie it could easily
have avoided.

So `SettingsDirector.FrameCapIsLive` is a property on the director, where the fast tier holds it,
and the HUD greys the cap row and says *paced by VSync*. The applier still writes
`targetFrameRate` while VSync is on — deliberately — because that is what makes turning VSync off
restore the player's own cap without them touching the row again.

## 5. The URP copy, and the trap it avoids

Three of the levers live on the pipeline asset. A `UniversalRenderPipelineAsset` is a
ScriptableObject, and **in the editor the live one is the committed
`Assets/Settings/PC_RPAsset.asset`**. Writing `renderScale` straight on to it means pressing a row
in the settings panel shows up in `git status`, and a render scale of 70% committed by accident
would then ship to everyone.

The project had already met this shape once and answered it: `HudShell.EnsurePanelCopy`
instantiates the `PanelSettings`, marks the copy `HideFlags.HideAndDontSave` and uses that, so
changing the interface scale never writes the asset. `DisplaySettingsApplier` is the same answer
for the pipeline.

Two details that are easy to get wrong:

- **Read the quality level first, the graphics default second.** A quality level may override
  `GraphicsSettings.defaultRenderPipeline`. Copying the default while the level held another asset
  would leave every lever here silently doing nothing *while the panel still lit the rung* — the
  worst failure available, because it looks like it worked.
- **Give the original back and destroy the copy on teardown**, or the editor keeps one copy per
  play session, each holding the renderer and its shaders alive until the next domain reload.

**MSAA is honoured because the renderer is Forward+** (`PC_Renderer.asset`, `m_RenderingMode: 2`).
Under Deferred, URP ignores `msaaSampleCount` and the row would be a lie. If the renderer ever moves
to Deferred, that row has to go or say so.

## 6. Nothing here runs per frame

Every lever is applied on the event that moved it and is never polled. There is no `Update`.

This is not frugality for its own sake. A settings object that reasserted itself every frame would
fight anything else setting the same Unity property, and the fault would present as *a setting that
won't stay changed* — which is a bug hunted in the wrong file.

Two further costs were taken out on purpose:

- **Every rung's label is built once, as its row is constructed.** ADR 0003's flip condition F1,
  asserted by `HudStressTests`, is that the HUD allocates nothing per frame in steady state, and
  `ToString`, interpolation and `+` all allocate. The change handlers only flip a USS class on a
  label that already exists.
- **Boot applies once, not once per stored lever.** The applier stays inert until `ApplyAll`,
  because `UseStore` lays the stored preferences over the seeds one at a time and each one raises.
  Letting those through would rebuild the render targets repeatedly on the way into a session, to
  arrive exactly where a single pass puts them.

Render scale and anti-aliasing *do* cost a visible hitch on the frame they change, because the
render targets are reallocated. The panel says so in the row's tooltip, in the same idiom the
toggles already use for `NeedsRedraw`: it is a fact about what the lever costs, not about what it
does, so it belongs on hover.

## 7. The resolution is the odd one out

Its rungs are the machine's, not ours, so they cannot be a table in an assembly compiled without
UnityEngine. The presenter reads `Screen.resolutions` and seeds them; everything about *choosing*
one is decided on the director and runs in the fast tier:

- **De-duplicated by area.** `Screen.resolutions` returns one entry per refresh rate, so a monitor
  that does 60, 120 and 144 Hz reports 1920×1080 three times. Refresh rate is the screen's business
  and not something this panel offers.
- **A stored size the machine no longer offers falls back to the nearest by pixel count**, and the
  fallback is written back. Unplugging a second monitor must not leave the row blank and the button
  dead: the commonest way a preference like this goes wrong is the one where nothing happens and
  nothing says why.
- **An empty list draws no rank at all**, because an X11 session can report very little.

Because the panel is built when the shell is and the directors attach afterwards, the rank has a
slot in the Display group from the start and its contents when there is something to put in them.

## 8. The editor cannot answer two of these

`Screen.SetResolution` and `Screen.fullScreenMode` do not mean anything against the Game view — it
is not a window the game owns. Both rows are drawn but greyed with `.settings__row--off` and say
*only a built game can change this*, because a control that silently does nothing is worse than one
that admits it cannot.

**They can therefore only be proven by the player build**, not by either test tier. That is a
playtest item by construction, not an oversight.

## 9. Still owed

- No quality preset — see §2.
- No refresh-rate choice. It is dropped with the duplicate resolutions and would need its own row.
- The Unity tier has not run against this change; there is no Unity in the container this was
  written in.
