# 61 — The inspect pane's header actions

**Status: built 2026-09-26, not yet played.** Branch `claude/inspect-header-actions`, worktree
`D:\code\odyssey-header`. The brief is Claude Design's mockup 24c, pasted by the owner with *"we
just need to clean up the icons ... adjust according to the current style/format"*.

## 1. What changed

A colonist's header carried four checkbox-style buttons: Draft, the response (Fight back / Defend /
Flee), First Person and a dimmed Inspect placeholder, then a bordered (i) and a bordered X. It
carries **two large icon toggles, Draft and First Person, and a compact Close over Info** on the
right. The portrait, name, meta lines, tab strip and body are untouched.

- **The response left the header.** It is a standing setting rather than an action, and the Assign
  tab's Response column (design 43 §6) already sets it for every colonist from one table. Design 33
  §18e's pane button is superseded by that column.
- **The dead Inspect placeholder left too.** The shell had been skipping it by comparing its label to
  a literal; the model no longer lists it.
- **Prioritise** had already gone with First Person (design 57 §6).

## 2. The toggles

One element, `Presentation/Ui/InspectToggle`: a square tile with the icon centred, a hotkey cap in
its bottom-right corner, and a label under it.

| | Off | On |
|---|---|---|
| Tile | 1 px `ControlBorder` (white 26 %), no fill | 1 px hue border, hue at 12 % |
| Icon | stroked in `TextDim` (white 50 %), no fill | **the same path**, filled and stroked in the hue |
| Hover | border `TextPrimary` at 40 %, icon stroke `TextMeta` | unchanged |

- **Draft is `Bad` red** (`#e06a5c`), because drafting is the risky state: she stops working and
  waits for orders. Its label reads **Draft** off and **Drafted** on (`ui.command.draft`,
  `ui.status.drafted`).
- **First Person is `Accent` cyan** (`#6fd3e3`), and always reads **First Person**. The mockup wrote
  *First person*; the owner's own casing (design 57, 2026-09-26) is kept.
- **The icon never swaps.** `PathGlyph.Solid` fills every closed subpath in one path, even-odd, then
  strokes the outline over it, so the eye keeps its pupil as a hole when filled.
- Every mark is an SVG path in `HudIcons` (`Shield`, `Eye`, `Info`, `Close`), as the mockup gives
  them, and `SettingsLayoutTests.EveryIconParsesAndStaysInItsBox` parses them.
- **The tooltip is the game's**: the command's label and reason, as before.

## 3. Behaviour

- **Draft** toggles every selected colonist through `OrderModel.ToggleDraft`, the same rule the key
  uses. Its face is `OrderModel.DraftFaceOf`, over the whole selection:
  - **On** only if every selected colonist is drafted;
  - **Mixed** if some are: the off tile with a 2 px red line along its foot, inside the border. A
    press on a mixed selection drafts the rest, which is what `ToggleDraft` already did.
  - Animals are passed over by both, so the face and the press cannot disagree.
- **First Person** rides with the selection's **one** colonist (`HudDirectors.TryFirstPersonSubject`)
  and is at 40 % opacity otherwise. A second press, or Escape, leaves and returns the view
  (`HudDirectors.ToggleFirstPerson`). While riding the whole interface is the strip (design 57 §6),
  so the on face is drawn but only seen for the frame the ride begins.
- **Keys.** The caps read the binding map, so a rebind shows at once.
  - Draft stays on **T**. The mockup's R is slice-up here, which the owner kept (2026-09-23).
  - First Person is a new action, **`HotkeyAction.FirstPerson`, on Z**, rebindable in Settings >
    Keys beside Draft. The mockup's V is the cutaway cycle; Z is on the same row beside it. This
    settles design 57 §7's "owed: the key". Riding holds the game's keys, so `HudShell.ReadBarKeys`
    reads this one past that gate to leave, but never over a rebind or a text field.
- **The header no longer rebuilds on a draft.** The old button changed its face by rebuilding the
  whole header (the draft was in the rebuild signature). The toggles are built once per subject and
  set in place every refresh; `InspectToggle.Set` compares before it writes.

## 4. Where it departs from the mockup, and why

| Mockup | Built | Why |
|---|---|---|
| 88 px header, 64 px portrait | **60 and 60**, unchanged | "Don't change the rest of the pane." |
| 44 px tile, 5 px gap | **40 px tile, 4 px gap**, 16 px label line | 44 + 5 + a label is 65 in a 60 px header and would push the tab strip down. `HudLayout.InspectToggleTile` is derived from the header, and `HudLayoutTests.TheHeaderActionsFitTheHeader` holds the sum. |
| 12 px gap between every column | 12 from the text to the toggles and between them, 15 to the stack; **9** from the portrait to the name | The portrait-to-name gap is the rest of the pane. |
| Hotkeys R and V | **T and Z** | Both placeholders; R and V are taken (§3). |
| Label weight 500 | the `Meta` role, 12 / 400 | Archivo Narrow ships as one variable file and 500 draws as 400 anyway (`HudType`). |
| Close/Info stacked | stacked, **but a row in the 38 px tile readout** | A tile or a pile keeps the short header, which cannot hold two 26 px buttons one over the other. The icons and colours are the same there. |

Every colour is an existing `HudTheme` token: the mockup's list is the interface's own.

## 5. Tests

Fast tier, Hud:

- `OrderModelTests`: the face across off, on and both mixed cases, animals ignored, a mixed press
  drafting the rest, and the two labels.
- `RideTests`: exactly one colonist (and a bandit or an animal beside her not counting), the toggle
  going in and coming out, nothing without one colonist, and the key on Z.
- `HudModelTests`, `ResponseModelTests`: the header carries Draft and First Person only.
- `HudLayoutTests.TheHeaderActionsFitTheHeader`, and fourteen new `HudStyleSheetTests` rows holding
  the stylesheet's numbers to `HudLayout`.
- `HotkeyDirectorTests` used Z as its unbound spare key; it uses J now.

Nothing tests that a click reaches the game (CLAUDE.md, known gaps), so the tiles' presses and the
hover are the playtest.
