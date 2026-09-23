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

Two tabs since 2026-09-20 (owner: events want a tab of their own), in Settings' tab idiom.
`DebugDirector.Tab` holds which is showing and opens on Cheats, because the overlay toggle is the
row backtick was bound to for a day.

**Cheats:**

| Row | What it does | Backed by |
|---|---|---|
| Developer overlay | Toggles the frame-time/draw-call readout, drawn at `fontSize` 56 (roughly six times the original default) and anchored to the bottom of the screen rather than the top-left, so it clears the HUD's top-left ledger regardless of size | `SettingsDirector.DeveloperOverlay` (unchanged; only the row moved) |
| Spawn colonist | Adds one colonist near the camera, with no scenario and no starting kit | `IntentKind.SpawnPawn` → `PawnRegistry.HandleSpawnPawn` |
| Give wood / Give stone / Give food | Adds 50 units of the resource near the camera | `IntentKind.GiveResource` → `ColonyItems.HandleGiveResource` |

**Events:** one row per incident Def the open colony's content declares, named by its
`bulletinKey` through `IncidentLabels` and tooltipped by the Def's own `description`. The rows are
built when the panel opens, from the colony that is open, and rebuilt only when the content is a
different object — a new colony — so a second Def appears by existing and this file never learns
its name. Each fires through `IntentKind.InvokeIncident` → `Incidents.HandleInvoke` → the Def's
worker, ignoring the gates on purpose (design 23 §3). Today that is one row:

| Row | What it does | Backed by |
|---|---|---|
| Supply drop | A stack of meals falls from the sky somewhere on the board, an Events row appears, and the colony hauls it. Lands on the next tick, so unpause to see it | `SupplyDropWorker` |
| Skip one day | Spends one whole game day of ticks in one synchronous batch (~0.2 s), then hands the clock back. Works while paused | `OdysseyBootstrap.DebugSkipTicks` — the composition root's own batch tick, not an intent: ticking is the root's one job and the bus is drained *inside* a tick |
| Skip one month | The same, twelve days at a time (~2.4 s), so **the year can be walked through**. Added 2026-09-22 with temperature: Rime is month five of six, and at a day a press the season the whole thermal model exists for was sixty presses away — which is not a playtest anybody runs. Six presses now take you Wash → Glare → Rime and back | `DebugSkipTicks` again, sized `Content.DayTicks * Calendar.DaysPerMonth` — both read rather than written, so a retuned calendar cannot leave this row skipping some other amount |
| Skip to morning | Skips the night and hands the clock back at dawn, with a whole watchable day ahead — the harvest happens on screen, not inside the skip | `OdysseyBootstrap.DebugSkipToMorning` — the same batch tick, sized to the next dawn |
| Ripen crops | Brings every standing crop to ripeness at once, daylight window and all — the harvest half without the four-day wait | `IntentKind.DebugRipen` → `GrowingZones.RipenAll`, refused with AlreadyInThatState when nothing stands |

### "Near the camera" is a column, not a cell (corrected 2026-09-19)

`DebugAnchorCell` names an **(x, z) column** and a layer that is only a guess: the selected
colonist's cell, else the selected cell, else the cell under the camera's own focus at the active
slice layer. The simulation decides which cell in that column the command lands in —
`CellGrid.NearestWalkableInColumn` for a spawn, `CellGrid.FirstFloorAtOrBelow` for a grant. That
split is forced rather than stylistic: **the shell reads snapshots and has no access to the cell
grid at all**, so it cannot know what is standable, and the one place that does is the intent
handler.

It used to claim the middle of the active slice layer was "always in bounds, so the two action rows
never have a reason to refuse". In bounds it was; standable it was not. Over open ground the active
slice layer is the air several storeys above the terrain, so `HandleSpawnPawn` refused **every**
spawn the menu sent — and reported it as `OutOfBounds`, for a cell in the middle of the map. The
first crowd playtest could not add a single colonist. The layer is now the caller's guess and the
grid's decision, and a refusal means the column genuinely has nowhere to stand, which reports
truthfully as `NotPermitted`.

The middle of the map was wrong on its own account too, quite apart from the layer: it is a place
nobody is looking at, usually hundreds of metres from the colony, and the row says "near the
camera". It is the camera's focus now, so the row does what it says.

Both new intents wrap sim APIs that already existed and add no new mechanic: `PawnRegistry.Spawn`
already built a colonist from nothing but a cell, and `ColonyItems.Spawn` plus the existing
`NearestCellWithSpace` already grant an item near a point the way a delivery or a drop does. Both are
registered in `ColonyComposition.AddColony`, the fourth and fifth intent handlers there, beside
`ForceJob` — no new chokepoint opened for this.

## What is deliberately not on it

**A hand-written event list.** The Events tab is the content's, not this file's. The single
*Invoke event* row that preceded it stood disabled from 2026-09-17 to 2026-09-20 because there
was no event system for it to fire, and building one as a side effect of wanting a test button was
the wrong order; the event system is design 23 and every row goes through the same door a
storyteller will. A row ignores the Def's gates on purpose — a debug row exists to make the thing
happen — and honours only the worker's own "can this fire at all" answer, so a board with nowhere
to land a drop refuses with `NotPermitted` rather than pretending.

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
   wall. Do it once with nothing selected, once with a colonist selected, and once after scrolling
   the slice layer well above the ground — the three paths through `DebugAnchorCell`.
4. Hold the spawn row down for twenty colonists and watch the console. One refusal should print
   one line saying `1 x`; a four-figure count means the rejection list has stopped being cleared
   again.
5. Click the Events tab, then "Supply drop", unpause. Confirm an Events panel appears under the
   alerts with a "Supply drop" row, that clicking the row jumps the camera to a cell with a pad
   on it without moving the slice, and that a ration pack comes down on to it from above the top
   of the frame over six seconds (`23-events-and-storyteller.md` §10).
   Confirm the Cheats tab still holds the overlay toggle and the four grants, and that the tab
   you left the panel on is the one it reopens to.
6. Open Settings while the debug menu is open, and vice versa. Confirm each closes the other rather
5. Paint a growing zone, then press "Skip one day" four or five times (paused and unpaused
   both). Confirm each press is one hitch rather than a freeze, the crop's drawn stage moves
   once or twice per skipped day, the hour of the day is unchanged after each press, and the
   colonists harvest it through the ordinary work scan once it ripes.
6. Click "Ripen crops" on a field mid-growth. Confirm every standing crop jumps to its last
   stage and is harvested; click it again on an empty board and confirm the rejection reaches
   the rejections readout (or is otherwise visible as "did nothing") rather than passing
   silently.
7. Click "Invoke event". Confirm nothing happens and the tooltip explains why.
8. Open Settings while the debug menu is open, and vice versa. Confirm each closes the other rather
   than stacking.
9. Judge whether the debug menu wants to look different from Settings at all — right now it is
   visually indistinguishable except for its rows, which may or may not be desirable for something
   explicitly not meant to look like ordinary game UI.

`Logs/hud-debug.png` (from `HudShotTests.PhotographTheHud`) is the one picture anybody has taken of
it so far.
