# 39 — The settings window: one fixed, centred frame

**Status:** built 2026-09-24 on `claude/settings-frame`, not yet played. The approved design is the
**rail layout** of mockups 14a–14e (owner's brief, pasted into the session that built it). This
document records what was built, the numbers, and the places the build departs from the brief.

## 1. What was wrong

1. The window changed width and height on every tab and was centred on its own size, so every tab
   moved the box and every row in it.
2. Save, Save as, Load, Quit to main menu and Exit game were repeated under every tab. That was
   most of the height change.
3. Every row started with an empty square. It was `IconBadge`'s placeholder glyph: no settings key
   has art, so every row drew the "no art yet" box, which read as a checkbox that did nothing.
4. Every heading had the same weight, so nothing marked where a tab or a section began.
5. Some strings used characters the shipped fonts may not draw: the multiplication sign in "2×",
   "0.6×" and "3840×2160", the dash on an unbound key and the ellipsis on a listening one.

## 2. The frame

`SettingsLayout` (Unity-free, `Assets/Odyssey/Hud/SettingsLayout.cs`) holds every number; the USS
repeats them and `HudStyleSheetTests` pins each USS length to its constant.

| Part | Size |
|---|---|
| Window | **1240 x 720**, `left: 50%; top: 50%; margin: -360px 0 0 -620px` (USS has no `calc`) |
| Header | 52, gear 18 in the accent, "SETTINGS" 13/600 at .16em, close button 30 with a drawn X |
| Rail | 240, tab rows 40, action rows 38, the Game group pinned with `margin-top: auto` |
| Title band | 72, a 38 tile in the tab's hue at 16% with a 50% border, name 19/600, subtitle 12/400 |
| Columns | equal, 20/24 padding, 40 between, 22 between sections, rows 38 |
| Footer | 52, "Reset \<tab\> to defaults" left, the Keys hint right |

**Nothing on a tab may change the frame.** A short tab leaves space. The fit is checked twice:
`SettingsLayoutTests.TheKeysTabFitsTheFrameWithNoScrolling` does the arithmetic (the tallest Keys
column is 450 px against 502 px of room), and
`HudGeometryTests.TheSettingsWindowIsOneCentredBoxOnEveryTab` measures the real window on every tab
at 1280 x 720, 1920 x 1080 and 2560 x 1440: the same `worldBound` on all five tabs, 1240 x 720 in
panel units, centred to a pixel, and no column's last section running past the column.

**It scales with the interface scale**, because it lives in the HUD panel whose reference
resolution the scale sets. At 150% the canvas is 1280 x 720 and the window fills its height exactly;
that is the largest scale offered.

**It is a modal now.** A scrim (`HudTheme.SettingsScrim`, rgba 6,10,12 at .58) sits behind it and
swallows the pointer, which is how every modal here works. The old panel was deliberately not one —
it was a narrow box you watched the board through — but a window this size over a clickable board is
a misclick waiting to happen, and the brief asks for the scrim. On the main screen the menu's own
scrim is already up, so the window adds none.

**It is raised to the front every time it opens** (`BringToFront` on the scrim and the window). The
main screen's scrim is built after it and is pickable, so on the first play (2026-09-24) Settings
opened from the main menu *showed* but every click landed on that scrim and did nothing. Measured
with a negative control: without the raise, `StartScreenTests.OptionsOpensTheSettingsPanelWithNoColonyRunning`
picks `start-scrim` at the window's centre; with it, the window. The two prompts the rail can raise
both close the window first, so raising it never covers one of them.

## 3. Colour

Only the brief's tokens. The new ones went into `HudTheme` beside the old: `ControlBorder` (.26),
`RowRule` (.07), `SwitchOffTrack` (.10), `EmptySlot` (.22) and `SettingsScrim`. The five tab hues are
`SettingsLayout.Hue`: Interface `#8fb3d9`, Graphics `#b9a8e0`, Audio the good green, Keys the warn
amber, Gameplay the accent. A hue marks the tab's icon, its selected rail row (14% fill and a 3 px
inset bar), the title tile and the section squares. Flat throughout: no radius, shadow or gradient.

## 4. Icons are paths

Every icon is the brief's SVG path on a 24-unit grid, stroked at 1.8 with round caps and joins, by
`PathGlyph`. `SvgPath` flattens a path once at construction (M L H V C S Q T A Z in both cases; arcs
by the SVG 1.1 endpoint-to-centre conversion), so a repaint builds nothing. The select's triangle is
a filled path and the unbound key slot is `DashedOutline`, painted, because UI Toolkit has no dashed
border. `SettingsLayoutTests` parses every icon and holds every point inside its box. **No mark on
the window is a font glyph**, and every string it writes is ASCII (tested).

## 5. What moved where

| Tab | Columns | Sections |
|---|---|---|
| Interface | 2 | Scale, Camera \| Build palette |
| Graphics | 2 | Display (mode, resolution, VSync, frame cap), Performance \| Detail (six switches) |
| Audio | 2 | Volume (master, music, ambience) \| Cues (effects, alerts) |
| Keys | 3 | Camera \| View, Tools \| Time, Interface |
| Gameplay | 2 | Saving \| (empty) |

The section headings are registry names (`ui.settings.group.*`), so they are in the wiki.

**Reset \<tab\> to defaults** is new on four tabs. `SettingsDirector.ResetTab` goes through the same
setters a press uses, so each lever that moves raises its own event and writes its own preference,
and one already at its default says nothing. The interface scale goes back to the screen's own
default (`DefaultUiScale`, seeded from `DefaultScaleFor(Screen.height)`), not to 100. The resolution
is left alone: its choices are the machine's. Keys keeps the hotkey director's own reset.

## 6. Controls

- **Segmented**: joined by a -1 px overlap. The lit segment "sits above" its neighbours by giving
  the next segment's shared left border the accent (`sw__seg--after-on`), because UI Toolkit gives
  siblings no z-order. A figure is set in the mono face and a word in the reading face, per segment:
  "Off" beside "2x" is still a word.
- **Switch**: the word On or Off (mono 12), then a 38 x 22 track with a 14 px square knob. The whole
  row still toggles on a click, as before.
- **Slider**: the built-in `Slider`, restyled to a 220 x 4 track with the accent up to the thumb, a
  10 x 18 thumb and the figure after it. **Unity stays seated at the centre with its mark** (owner,
  2026-09-17); the brief's fill runs from the left edge to the thumb.
- **Select**: the built-in `DropdownField`, 190 x 28, with its texture arrow hidden and a drawn
  triangle in its place. Values read "3840 x 2160".
- **Key chips**: two per action. An unbound slot is an empty chip in dashes. A listening chip says
  "..." in the accent. **Backspace clears the slot that is listening** (`HotkeyDirector.ClearListening`)
  — new, because the footer's hint promises it and nothing did it.

- **Wake-up** (2026-09-26, design 56 §9): Interface → Camera, a two-segment On / Off under the
  camera speed. A segmented control rather than a switch because every row on the Interface tab is
  one.

## 7. The keyboard

Tab walks the window; Up and Down step between rows; Left and Right move along a segmented control
(picking as they go) or between chips; Enter and Space press; Escape closes. A fader answers its own
arrows. The focused control gets a 2 px ring in the text colour, 2 px off it.

- **One ring for the window**, moved to the focused control, drawn last. UI Toolkit has no
  `outline`, and a ring owned by each control would be half hidden by the sibling drawn after it.
- **While a control here has focus the game's keys sit out**, through the same gate a text field
  takes (`HotkeyDirector.BeginTyping`, with the window as the token). Otherwise Space on a switch
  would also pause the game and the arrows would pan the camera.
- **Focus from the mouse is given back on release**, so a player who only clicks keeps the camera
  keys and never sees the ring.
- **A chip opened from the keyboard waits 120 ms before listening** and gives up focus while it
  listens, so the Enter or Space that opened it is not bound and Escape reaches the rebind rather than
  the window. Focus comes back to it when the wait ends.

## 8. The leave prompt

Restyled to the brief rather than rebuilt: `LeavePrompt` already asked save-and-leave, leave without
saving, or stay. It is 440 wide with 20 padding, the title is a 19/600 question ("Quit to main
menu?", "Exit game?"), the note leads with "Unsaved progress since the last save will be lost."
and keeps the line naming the file, the save answer is outlined and washed in the good green, the
leave answer in the bad red, and **focus lands on Cancel**. Escape still cancels.

## 9. Where the build departs from the brief

| Brief | Built | Why |
|---|---|---|
| Rows "Layout", "See through selection" | "Build palette layout", "See through to selection" | the brief also says rename nothing; the registry names stand |
| Resolution disabled unless Fullscreen | done, **and also disabled in the editor** ("Built game only") | the Game view is not a window the game owns; that was already true |
| Title "Exit the game?" | "Exit game?" | the title is the registry's name for the action plus a question mark, so the two cannot drift |
| Confirm body is one sentence | the sentence, then the existing line naming the save file | "Save and quit" is a promise about a file; the player is owed which one |
| Slider fill from the left | from the left, with the centre mark kept | the owner asked for unity at the centre, marked |
| Focus outline via CSS `outline` | one drawn ring | UI Toolkit has no outline |

## 10. Not done

- **Arrow-key navigation is ours, not the engine's.** If Unity's runtime panel also moves focus on
  an arrow (a `NavigationMoveEvent` default), a step could happen twice. It has not been seen; it is
  the first thing to check at the keyboard.
- **Tab from nothing focused** goes wherever the panel's focus ring starts, which should be the
  window because nothing else focusable is on screen while it is open. Not tested by a key press:
  nothing here can press a key in a PlayMode test (`CLAUDE.md`, known gaps).

## 11. On merging main (2026-09-24)

`main` brought the Meadow look's settings (design 38 §9) into the old Graphics tab code, which this
window replaced, so they were ported rather than merged: the **Quality** row (Low, Medium, High,
Ultra, and Custom when the levers match none), full width in a band across the top of Graphics
because a preset sets levers in both columns; the **Grass** and **Grass distance** ladders leading
Detail, beside the new **Grass shadows** switch; the "redraws the board" tooltip; and the resolution
select offering the current size when the monitor does not list it (it threw at 960 x 540). The
tallest Graphics column is 390 px of 502. `SettingsLayoutTests` now holds Display and Performance to
the director's display ladders, so a ladder added there cannot fall off the page.

Read from the photographs before merge (`HudShotTests`, which had silently skipped the settings
pictures since the rebuild — it looked the window up by a class it no longer carries):

- **A note now sits under its label, not beside it.** Beside it, a greyed row with five segments cut
  both short: "Frame rate cap" / "Paced by VSync" drew as "Frame ...". A 38 px row holds both lines.
  This departs from the brief's "same baseline".
- **A key chip is a box holding its key, not a label holding its outline.** A label with a child is
  no longer sized by its text, so every chip shrank to its 36 px floor and "Space", "PgUp" and "Home"
  ran into their edges. `HudGeometryTests` now asserts every key fits inside its chip's padding.
