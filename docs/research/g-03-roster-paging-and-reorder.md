# Research: Roster Pagination, Colonist Reordering, and UI Toolkit Drag-and-Drop

## Question
How do RimWorld and comparable colony sims handle colonist bar reordering, overflow pagination, and selection synchronization; and what are the best practices for right-click drag-and-drop slot reordering and pagination within Unity 6 UI Toolkit without garbage allocation?

## Findings

### 1. Colony Sim Prior Art: Colonist Bar Reordering and Overflow Management
- **Right-click drag mechanics**: In RimWorld, manual colonist bar reordering is initiated via right-click press and drag on any colonist portrait. Left-click is strictly reserved for colonist selection (single click) and camera focusing (double-click snaps the camera to the pawn's current position). If right-click is released without moving past a minimum drag threshold (approx. 4–6 px), no reorder occurs.
- **Reordering models**: RimWorld performs insertion/splice on drag-and-drop, whereas players frequently request direct slot swapping (A ↔ B) when organizing designated squad/bed/schedule pairings. In our interview, direct slot swapping was selected as Odyssey's model.
- **Cross-bar behavior**: When colonists embark on caravans or visit other sites, RimWorld separates them into groups. Reordering is local within a roster.
- **50+ pawn management & failure modes**:
  - In vanilla RimWorld, the bar does not paginate; instead, it continuously compresses/scales down card width and portrait dimensions to fit all colonists into the screen width.
  - At 30–50+ pawns, cards shrink into unreadable 10–15 px slivers where mood bars, health icons, and portrait details become completely illegible.
  - Modded RimWorld (*Colonist Bar Adjuster*, *Colony Groups*) solves this using fixed-card-width constraints, multi-row configurations, and pagination controls.
  - For Odyssey, where HUD design tokens strictly enforce card dimensions (currently 96 × 89 px per PR #120 / journal 2026-09-18) clamped between the Stores panel (left) and the Clock/Alerts column (right), horizontal icon shrinking is unacceptable. Pagination with fixed-dimension cards is the clean architectural solution.
- **Selection synchronization**:
  - Left-click selects a single colonist and updates the Inspect pane.
  - Shift + Left-click toggles selection for multi-selection squads.
  - Drag-box selection in the 3D viewport immediately highlights corresponding cards on the top bar in the same frame.
  - When an alert is clicked (or selection jumps to a colonist on another page), the active roster page should automatically flip to reveal that colonist's card.

### 2. Unity 6 UI Toolkit: Right-Click Drag-and-Drop & Pagination
- **Pointer event handling (`button == 2`)**:
  - `PointerDownEvent`: Check `evt.button == 2` (0 = Left, 1 = Middle, 2 = Right). Record `dragStartPos = evt.position` (panel-space coordinates) and mark pending drag.
  - To prevent mouse movement jitter triggering accidental drags, only transition to active drag when `(evt.position - dragStartPos).sqrMagnitude >= DragThresholdSqr` (e.g. 16–25 px²).
- **Pointer capture (`CapturePointer`)**:
  - Once drag begins, call `target.CapturePointer(evt.pointerId)`.
  - Capturing the pointer ensures that all subsequent `PointerMoveEvent`, `PointerUpEvent`, and `PointerCancelEvent` instances are delivered directly to the captured element, even if the cursor moves outside the card, over other HUD panels, or outside the game window.
  - Call `target.ReleasePointer(evt.pointerId)` on `PointerUpEvent` or `PointerCancelEvent`.
- **Target hit-testing during drag**:
  - Direct coordinate arithmetic or slot geometric bounding avoids raycast/picking traps with floating drag ghosts.
  - Floating drag ghosts must use `pickingMode = PickingMode.Ignore`.
- **PointerUpEvent and PointerCancelEvent**:
  - On `PointerUpEvent (evt.button == 2)`:
    - Release pointer capture: `target.ReleasePointer(evt.pointerId)`.
    - If dragging, execute the slot swap: locate the source and target pawn IDs, swap their positions in the persistent display order list, and refresh the strip.
  - `PointerCancelEvent`: If the application loses focus or the drag is aborted, cancel the operation, hide ghost and target highlights, and release pointer capture without reordering.
- **Pagination and WheelEvent**:
  - Register `RegisterCallback<WheelEvent>` on the roster container.
  - Inspect `evt.delta.y`: if `evt.delta.y > 0`, advance page; if `evt.delta.y < 0`, retreat page. Clamp to `[0, MaxPages - 1]`.
  - Call `evt.StopPropagation()` to prevent scrolling from zooming the world camera.
  - Edge auto-paging: hovering near the pager buttons or strip boundaries while holding a drag flips pages so cards can be swapped across pages.

### 3. Zero / Low Allocation Steady-State UI Updates in UI Toolkit
1. **Card View Pooling**: Pre-instantiate a pool of card elements up to `_stripCapacity`. Toggle `DisplayStyle.None` or update existing card views rather than adding/removing elements from hierarchy on every frame.
2. **Persistent Order Storage**: Store custom display ordering as a `List<PawnId>` in presentation/roster state, and serialize into `ViewStateSection` (`SaveKey = "view"`).
3. **Dirty-Checking Scalar Properties**: Cache displayed values on card views and only update when values change.

## Recommendation
1. **Paging Architecture**:
   - Keep cards at fixed 96 × 89 px.
   - Pager control sits docked on the right side of the roster strip: `[ < ] 1 / 3 [ > ]`.
   - Hidden when total colonists `<= stripCapacity`.
   - Support mouse wheel over the strip and click on pager buttons.
   - Flip page automatically when an alert or camera focus targets a colonist on a different page.
2. **Reordering Architecture**:
   - Right-click drag (`button == 2`) with `CapturePointer`.
   - Drag threshold of 4 px to prevent accidental drags.
   - Floating drag ghost with `PickingMode.Ignore` and target slot highlight.
   - Direct swap (A ↔ B) of positions in `RosterModel`'s ordered `List<PawnId>`.
   - Persist in `ViewStateSection` so custom ordering survives save/load.

## Sources
- Unity Documentation: `PointerManipulator` & Pointer Events: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/UIElements.PointerManipulator.html
- Unity Documentation: `PointerCaptureHelper`: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/UIElements.PointerCaptureHelper.html
- Unity Documentation: `WheelEvent`: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/UIElements.WheelEvent.html
- RimWorld Wiki: Colonist Bar & Controls: https://rimworldwiki.com/wiki/Controls
- RimWorld Community Mod Prior Art: *[LTO] Colony Groups* & *Colonist Bar Adjuster* (Steam Workshop)

## Confidence
- High on RimWorld player-facing mechanics, mod landscape solutions, and failure modes.
- High on Unity 6 UI Toolkit pointer events, pointer capture semantics, and zero-allocation idioms.
- High on Odyssey HUD layout constraints (`docs/design/14-hud-layout.md`).

## Could Not Be Determined
- Internal proprietary method names or private field structures of RimWorld's colonist bar codebase (deliberately excluded under clean-room rules).
