# 40 — The title screen: a dock on the left

**Status:** built 2026-09-24 on `claude/settings-frame`, beside the settings window (design 39),
not yet played. The approved design is the owner's docked-left title screen brief. This records
what was built, the one measurement it needed, and where it departs from the brief.

## 1. What changed

The title screen was a 420 x 384 card centred on the starfield, reading ODYSSEY over four rows,
each with the empty placeholder square in front of it (the same `IconBadge` fault design 39 §1
fixed in Settings). It is a **dock**: full height, flush left, `TitleLayout.DockWidth` = 560 px at
every resolution, with a 1 px right edge, and the starfield to its right with **no wash over it**.
The start screen's scrim is still there and still takes the pointer, but it is clear
(`#start-scrim`); the dock carries its own contrast.

From the top: the **Strata mark** (five slabs, the middle one lit in the accent, 70 x 60) and the
**wordmark** on one row; a rule; the four buttons; and at the foot the build line
(`prototype <Application.version>`) and the screen's resolution, in 11 mono at `--cap`.

## 2. Numbers

`TitleLayout` (Unity-free) holds them; `HudStyleSheetTests` pins each USS length to it.

| | |
|---|---|
| Dock | 560 wide, 48 padding either side, so 464 of content |
| Logo top | 18.5% of the screen's height, never under 96 (200 px at 1080). Set from C# on a geometry change, because USS has no `vh` |
| Mark / gap / wordmark | 70 x 60, 22, Archivo Narrow 600 at 64 px |
| Divider | 40 above, 28 below |
| Buttons | 64 high, 6 apart, 14 padding, icon 22, 16 to the words, name 19/600, description 12/400 6 below |
| Footer | pinned with `margin-top: auto`, 28 from the bottom |

**The wordmark does not fit at .3em.** Measured in PlayMode (`StartScreenTests.TheTitleScreenIsADockFlushLeft`
logs it): the brief's estimate of 330 px for the tracked word was short, and with the mark and the
gap the row came past 464. The brief's own fallback applies: `LayOutTitle` measures the word after
layout and drops the tracking to **.26em** if it would overflow, never the size. At .26em the word
is **370 px and the logo 460 px**, inside the 464. The fallback is automatic, so a font change that
makes .3em fit will take it back.

## 3. The buttons are the Settings rail's

New game (good), Load (info), Settings (violet), Exit game (bad, its name red at rest). Load and
Exit draw `SettingsLayout.ActionIcon` and `SettingsLayout.Ink`; Settings draws
`SettingsLayout.GearIcon`, which the window's header uses too — one source each, and
`TitleLayoutTests` holds them to it with `Is.SameAs`. The violet is `HudTheme.Violet`, the same
token as the window's Graphics hue. The mark is five `VisualElement` boxes, not a font glyph.

**States** are the rail's: hover and focus fill the button's colour at 14%, show a 3 px edge in it on
the left and colour the name; pressed is 22% (`:active`). Every colour is a stylesheet rule per
tone (`.title__btn--good` and the rest), so a hover costs no C#.

## 4. Keyboard and focus

New game has focus when the screen first shows, so Enter starts a game. Up and Down move between the
four; Enter and Space press; Escape does nothing here (`SettingsDirector.Escape` already answered
Nothing on the root screen). Keyboard focus also shows a 2 px ring in the text colour, 2 px off —
one ring for the dock, as in design 39 §7, because UI Toolkit has no `outline`. Focus from a mouse
press is given back on release, so a mouse player never sees the ring.

**Coming back puts focus where you left.** Returning from Settings focuses the Settings button,
and from the load list the Load button. It is derived from the screen being left rather than the
button pressed, so it holds whether the screen was opened by a click, by the keyboard or by the
director.

## 5. Settings and Exit

- **Settings opens over the dock**, centred as design 39 fixes it, with the window's own scrim
  dimming the dock beneath. This reverses the 2026-09-17 rule that the settings panel stood *in
  place of* the start card: that rule was about two centred boxes stacking, and a flush-left dock
  under a centred window is not that.
- **Exit game goes through the confirm dialog.** It used to arm on the first press and quit on
  the second. It raises the leave prompt now, in a no-colony form (`LeavePrompt.AskToExit`): the
  title "Exit game?", no save answer, no note, and the leave answer named "Exit game". Focus lands
  on Cancel, Escape cancels. `SessionCommands` stops the row arming (`AsksTwice` false on the main
  screen), so the prompt is the second press, as it is in game. The four `MenuDirectorTests` that
  used Quit as their example of arming went with it; the arming itself stays in `MenuDirector` for
  the next row that needs it.

## 6. The load list

The brief keeps the load flow and removes the card it lived in, so the list moved into the dock,
under the rule where the buttons were, with Back beneath it and the footer still at the foot. Back
lost its placeholder square too.

## 7. The exported marks

`art-source/brand/`, outside `Assets/` so Unity does not import them:
`odyssey-mark.svg` (the five slabs), `odyssey-mark-16.svg` (three slabs for tiny sizes) and
`odyssey-icon.svg` (a 64 px accent tile with the mark in the on-accent ink at 44 x 38, centred).

## 8. Where the build departs from the brief

| Brief | Built | Why |
|---|---|---|
| Wordmark tracked .3em | .26em, chosen at runtime | .3em overflows the 464 px measured; the brief's own fallback (§2) |
| Hover inset shadow 3 px | a 3 px edge element | UI Toolkit has no box-shadow |
| Keyboard outline via CSS | one drawn ring | UI Toolkit has no outline |
| Resolution line "if easily available" | shown, `Screen.width x Screen.height`, refreshed on a geometry change | it was |
| Version from "the existing version source" | `Application.version` (0.1.0), as "prototype 0.1.0" | the project's only version source is the player setting |
| Nothing said about the load list | in the dock under the rule | the card it lived in is gone |

## 9. Not verified

The dock's width and edge were measured on the test runner's own game view only, which has a panel
scale near 0.39; the brief's three resolutions are not set up for a no-colony screen in PlayMode.
The dock is a constant width in panel units, so a resolution changes nothing but that rounding.
