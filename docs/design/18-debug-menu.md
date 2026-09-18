# 18 — The debug menu

Owner, 2026-09-17: *"We need a debug menu. Currently \` is taken for the developer overlay. Instead
create a new menu in the style, and format of all the other menus. On this screen will be toggle
button for developer overlay (make the text much much much bigger for the developer overlay in game
as it cannot be read) and will include various things we can debug in game in future — like for
example, invoking an event."*

## What backtick does now

Backtick opens and closes a new panel, `DEBUG`, built the same way Settings is — a `Window`, the
same `.settings__row` and pip idioms, no scrim, the world keeps running behind it. It used to toggle
the developer overlay directly; that is now the panel's first row instead, moved there rather than
copied (Settings' Interface tab no longer draws it — a control drawn in two places is a cost this
project has already paid twice, see `EveryLiveToolIsDrawnSomewhere` in `docs/lessons.md`).

Opening the debug menu closes Settings and the Build menu, and opening Settings closes the debug
menu, the same mutual exclusion Settings and the Build menu already had. Both panels are centred at
the same spot on screen (`.debug` and `.settings` in `Hud.uss`), which costs nothing precisely
because the two cannot be open together.

## What is on it

| Row | What it does | Backed by |
|---|---|---|
| Developer overlay | Toggles the frame-time/draw-call readout, drawn at `fontSize` 56 (roughly six times the original default) and anchored to the bottom of the screen rather than the top-left, so it clears the HUD's top-left ledger regardless of size | `SettingsDirector.DeveloperOverlay` (unchanged; only the row moved) |
| Spawn colonist | Adds one colonist near the camera, with no scenario and no starting kit | `IntentKind.SpawnPawn` → `PawnRegistry.HandleSpawnPawn` |
| Give wood / Give stone / Give food | Adds 50 units of the resource near the camera | `IntentKind.GiveResource` → `ColonyItems.HandleGiveResource` |
| Skip one day | Spends one whole game day of ticks in one synchronous batch (~0.2 s), then hands the clock back. Works while paused | `OdysseyBootstrap.DebugSkipTicks` — the composition root's own batch tick, not an intent: ticking is the root's one job and the bus is drained *inside* a tick |
| Ripen crops | Brings every standing crop to ripeness at once, daylight window and all — the harvest half without the four-day wait | `IntentKind.DebugRipen` → `GrowingZones.RipenAll`, refused with AlreadyInThatState when nothing stands |

"Near the camera" is the selected colonist's cell if one is selected, else the middle of the active
slice layer (`HudShell.Debug.cs`, `DebugAnchorCell`) — always in bounds, so the two action rows never
have a reason to refuse on a debug menu's own account.

Both new intents wrap sim APIs that already existed and add no new mechanic: `PawnRegistry.Spawn`
already built a colonist from nothing but a cell, and `ColonyItems.Spawn` plus the existing
`NearestCellWithSpace` already grant an item near a point the way a delivery or a drop does. Both are
registered in `ColonyComposition.AddColony`, the fourth and fifth intent handlers there, beside
`ForceJob` — no new chokepoint opened for this.

## What is deliberately not on it

**A real "invoke event".** There is no repeatable, triggerable event system anywhere in the sim —
only `ScenarioDef`, which acts once at tick zero and never again. The strings this project already
has for a raid (`ui.alert.raid`, `ui.bulletin.raidincoming`) are unused HUD labels sitting in the
registry with nothing behind them. Building an event system is a feature with its own design
questions (what events exist, how they are weighted, whether they escalate) — not something a debug
menu should invent as a side effect of wanting a test button. The row stays visible and disabled so
the gap is legible rather than silently missing.

**Kill, damage or heal a colonist.** `Pawn` has no health, injury or downed model at all — Need_Food
hitting zero is a mood penalty, never a death. A debug "kill" today could only mean "delete the pawn
from the registry with no health model behind it", which is a different and smaller thing than what
"kill" would mean once health exists, and building the smaller thing first risks the debug menu
teaching a habit ("kill just despawns") that a later health system would have to unlearn.

**Build gating.** No release build exists yet, so the panel is reachable in every build the same way
every other panel is.

## By-hand test procedure

Fast tier and Unity EditMode/PlayMode are all green (`DebugIntentTests`, `DebugDirectorTests`,
`HudSmokeTests` updated for the thirteenth framed region). None of them can say whether the panel is
usable at the keyboard. On next Play:

1. Press backtick. Confirm the debug menu opens, not the raw overlay.
2. Click "Developer overlay". Confirm the readout appears, and judge whether 28 pt is actually
   readable, too big, or still needs a second pass — nobody has judged it yet.
3. Click "Spawn colonist", "Give wood", "Give stone", "Give food" in turn. Confirm each lands near
   the camera and does not, for instance, spawn a colonist stuck in rock or drop resources through a
   wall.
4. Paint a growing zone, then press "Skip one day" four or five times (paused and unpaused
   both). Confirm each press is one hitch rather than a freeze, the crop's drawn stage moves
   once or twice per skipped day, the hour of the day is unchanged after each press, and the
   colonists harvest it through the ordinary work scan once it ripes.
5. Click "Ripen crops" on a field mid-growth. Confirm every standing crop jumps to its last
   stage and is harvested; click it again on an empty board and confirm the rejection reaches
   the rejections readout (or is otherwise visible as "did nothing") rather than passing
   silently.
6. Click "Invoke event". Confirm nothing happens and the tooltip explains why.
7. Open Settings while the debug menu is open, and vice versa. Confirm each closes the other rather
   than stacking.
8. Judge whether the debug menu wants to look different from Settings at all — right now it is
   visually indistinguishable except for its rows, which may or may not be desirable for something
   explicitly not meant to look like ordinary game UI.

`Logs/hud-debug.png` (from `HudShotTests.PhotographTheHud`) is the one picture anybody has taken of
it so far.
