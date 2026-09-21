# 28 — The pointer cursor: a real cursor, and where it actually points

**Status:** built 2026-09-21, branch `claude/pointer-cursor`. Not yet played.

The owner, 2026-09-21: *"The cursor doesn't seem super accurate but I noticed this issue and we
should use a proper cursor"*, against Unity's

```
Runtime cursors other than the default cursor need to be defined using a texture.
UnityEngine.UIElements.UIElementsRuntimeUtilityNative:RepaintPanels (bool)
```

Two faults in one sentence, and they are unrelated to each other. One is a stylesheet asking for
something a runtime panel cannot give. The other is arithmetic: **every pointer pick in the game was
resolved against the camera's transform from the previous frame.** This document holds both, and
the one thing deliberately *not* fixed.

---

## 1. The warning: eighteen declarations that never did anything

`Assets/Odyssey/Presentation/Ui/Hud.uss` carried eighteen `cursor:` declarations — fifteen
`cursor: link`, one `cursor: pointer`, one `cursor: default`, one `cursor: initial`. Keyword
cursors are an **Editor-only** feature of UI Toolkit. In a runtime panel — which is the only kind
the game has — they set nothing and log that line from `RepaintPanels`, so the cost was a log per
repaint and the benefit was zero. Every one of them had been written on the belief that it changed
the pointer, and none ever had; the game has run the bare OS arrow since the HUD was written,
because nothing in the project has ever called `Cursor.SetCursor`.

**They are deleted, not converted.** USS *can* take a texture cursor
(`cursor: url("...") 4 4`), and converting them would have worked. It was rejected for two
reasons.

The first is the rule this project already holds about order colours: one thing named in two places
drifts. Eighteen selectors each naming a cursor is eighteen owners, in a file that no test reads,
and the pointer is exactly the sort of thing that would end up meaning "clickable" on one row and
"draggable" on another without anybody deciding it.

The second is written in Hud.uss itself, at the bed-owner row (§ the `.inspect__row--pick` comment):
two rounds of hover-only affordances both failed to tell the owner that a row could be clicked, and
the conclusion recorded there is **hover is not an affordance** — a control has to look like one
while the pointer is somewhere else entirely. A cursor swap is by definition only visible once the
pointer has already arrived. So a per-widget cursor is the weakest possible signal, and the fifteen
`cursor: link` rules were buying nothing even in the world where they worked.

`PointerCursorTests.TheStylesheetAsksForNoKeywordCursor` reads the `.uss` file in the fast tier and
fails on any `cursor:` declaration that is not a `url(...)`. It is a source-reading test in the
same mould as `HudFontTests` and `RegistryTests`, for the same reason: nothing else can see it.
The compiler cannot, the fast tier has no text engine, and the Unity tier asserts no pixels — a
stylesheet asking for the impossible is a *log line*, and log lines are invisible in every gate the
project has.

## 2. The cursor set

Three states, one owner, `CursorDirector`.

| State | When | Art | Hotspot |
|---|---|---|---|
| `Default` | anywhere else | arrow | the tip, `(0, 0)` |
| `Interface` | the pointer is over the HUD | arrow | the tip, `(0, 0)` |
| `Tool` | an order tool is armed **and** the pointer is over the world | crosshair, tinted by the order's hue | the centre, `(15, 15)` |

`Interface` draws the same arrow as `Default` today and exists as a separate state rather than as
an absence, because the question it answers — *does this click reach the world?* — is one the HUD
already asks in three other places and the state is the seam where a distinct panel cursor goes if
one is ever wanted. It costs nothing to keep and cannot be recovered cheaply once the branch is
collapsed away.

**The tool crosshair is tinted by `OrderColours.Hue`, and that is not decoration.** The chip in the
orders strip, the palette header, the drag cursor and the mark on the board are already one hue per
tool, by a rule with a test walking every tool. The pointer is the fifth surface an armed order
appears on and it would have been the only one guessing at its own colour. Mine is mine-coloured
under the pointer.

**Over the HUD, an armed tool reverts to the arrow.** That is the one place the cursor genuinely
carries information a player cannot get otherwise: with a tool in hand and the pointer over a
panel, the world ghost is suppressed (`ToolHoverLost`) and until now nothing said why. The arrow
coming back is that sentence.

## 3. The art is baked in code, at 32 × 32, and both numbers are load-bearing

`CursorArt` generates the textures at runtime — `RGBA32`, `filterMode = Point`, no mips — rather
than importing PNGs. Three reasons, in order of weight.

**32 × 32 is the hardware cursor ceiling on Windows.** Above it, `CursorMode.Auto` silently falls
back to a *software* cursor, which Unity composites in the frame — so it lags the real pointer by
at least one frame and does not move at all while the game is hitched. Shipping a 64 px cursor to
fix an accuracy complaint would have made the complaint worse, and the failure would have been
invisible on this machine and reported from the owner's. The size is asserted
(`CursorArtTests.EveryCursorFitsTheHardwareCeiling`) rather than left to whoever authors the next
one.

**A tint per tool is seven textures, or one and a multiply.** Baking makes the tool crosshair a
function of `OrderColours.Hue(tool)`, cached per tool, and a new order tool inherits a cursor with
no art work at all — the same per-nothing property the carry path has.

**And ADR 0007 governs interface art**: 64 px, point-filtered, uncompressed, no mips, under
`Assets/Art/Ui/`. A cursor breaks the first of those by necessity. Rather than carve an exception
into an absolute rule — which is how the rule dies, per `IconArt`'s own note — the cursor is not
imported art at all. It is drawn, like `HudGlyph` draws every other shape the project needed and no
sheet had.

The hotspot is authored data beside each key, not a convention. A wrong hotspot *is* an inaccurate
cursor, it is invisible in a screenshot, and it is the first thing anybody would blame for the
report this work came from. `CursorArtTests.TheHotspotIsInsideTheArtAndOnAnOpaquePixel` fails a
hotspot that lands on transparent pixels, which is the shape the mistake takes.

## 4. The accuracy fault: a pick resolved against last frame's camera

Found by reading `SliceCameraRig.Update`, and it is arithmetic rather than a judgement call:

```
ReadKeyboard(dt);      // WASD writes _focus
ReadMouse(dt);         // scroll writes _targetDistance; CellAt() casts a ray through the camera
TakeJumpRequest();
ApplyTransform(false); // *now* the camera moves to _focus and _targetDistance
DrawSelection();
```

`CellAt` calls `Camera.ScreenPointToRay`, which reads the camera's **current** transform — and at
that point in the frame the transform is still the one written at the end of the *previous* frame.
The world is then drawn from the new one. So every hover, drag, box and click was resolved against
a camera one frame behind the picture the player was pointing at.

It is exact while the camera is still, which is why it survived. It is wrong the whole time the
camera is moving, and it never settles, because it is a constant one-frame lag rather than an
error that converges: a pan at 30 m/s at 60 fps is half a metre, a fifth of a cell, and
shift-boosted it is more. **Panning while placing is the commonest thing a player does with a tool
in hand.** That is the whole of "the cursor doesn't seem super accurate", and no amount of cursor
art would have touched it.

**The fix is ordering, and the ordering is the invariant.** `ReadMouse` no longer resolves
anything: it reads the pointer, the wheel and the buttons, advances the gesture state, and
*latches* what the frame decided into `_pending` — hover, dragging, drag, click, box, pick or
cancel, with the screen points it was decided at. `ApplyTransform` runs. Then `ResolvePointer`
turns those screen points into cells and fires the events. Nothing else moved: the gesture
arithmetic, the tool-armed question and every comment about which of them is asked when are
untouched, because they were right.

Applying the transform *before* `ReadMouse` would have been a smaller diff and was rejected: the
wheel and the orbit write their targets inside `ReadMouse`, so the camera would have answered the
player's zoom one frame late instead. That trades a visible misalignment for a different one.

`PointerCursorTests.ThePointerIsResolvedAfterTheCameraHasMoved` reads `SliceCameraRig.cs` and
fails if `ApplyTransform` does not appear before `ResolvePointer` inside `Update`. A source-reading
test again, and for the third time the reason is that nothing else can see it: the PlayMode
harness still cannot deliver a synthetic mouse (`InputHarnessTests` carries the ignored tests that
say so), so there is no test in this project that can press a button and look at where the
highlight landed. The invariant is the thing that can be defended, so the invariant is what is
asserted.

## 5. What is *not* fixed, and why it is recorded rather than guessed at

`SlicePicker` marches the render mirror's **cell boxes**. Several things are not drawn on their cell
box: a terrace ramp is drawn on a slope through its cell, a bank fills the cell at the foot of a
step, Synty wall panels are inset from the cell face, and a tree's canopy is wider than the cell it
stands in. At the play camera's 48°, pointing at a drawn ramp surface picks the flat cell under it,
and that reads as an inaccurate cursor too.

This is left alone deliberately. It is a second fault with a different cause and a different fix,
the terrace work already established that the drawn surface and the priced surface can be
reconciled (`22-terrace-steps.md` §4b), and doing both at once would make the playtest unable to
say which one it was judging. The measurement that would open it is one Play session with the
picked cell's box and the raw ray hit drawn together — if the offset persists after this branch
lands, that is the next unit.

## 6. Tests

| Test | Tier | Catches |
|---|---|---|
| `TheStylesheetAsksForNoKeywordCursor` | fast (Hud) | a keyword cursor reappearing in `Hud.uss` |
| `ThePointerIsResolvedAfterTheCameraHasMoved` | fast (Hud) | the pick sliding back before `ApplyTransform` |
| `EveryCursorFitsTheHardwareCeiling` | EditMode | a cursor over 32 px, i.e. a silent software cursor |
| `TheHotspotIsInsideTheArtAndOnAnOpaquePixel` | EditMode | a hotspot off the art, i.e. an inaccurate pointer |
| `EveryToolHasItsOwnHueAndIsCachedOnce` | EditMode | a per-frame texture allocation; a tool with no cursor |
| `TheCursorFollowsTheArmedToolAndTheInterface` | EditMode | the state machine, without a real mouse |
