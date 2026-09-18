# Plan: Roster Top Bar Pagination, Auto-Page Selection, and Drag-and-Drop Slot Swapping

**Target:** Resolve the colonist bar overflow when colonies grow beyond the single-view strip capacity, enable seamless navigation and auto-selection synchronization, provide right-click drag-and-drop slot swapping (A ↔ B), and persist custom roster order across saves.

---

## 1. Architecture & Seams

### A. HUD Model Seam (`Odyssey.Hud`)
1. **`RosterModel` (`Assets/Odyssey/Hud/RosterModel.cs`)**:
   - Maintains a persistent display order list: `List<PawnId> _customOrder`.
   - **Order synchronization in `Refresh`**:
     - Culls IDs no longer present in `snapshot.Pawns`.
     - Appends any new IDs present in `snapshot.Pawns` that are not yet in `_customOrder`.
   - **Pagination**:
     - Properties: `int Page`, `int PageCapacity`, `int PageCount`, `int TotalCount`.
     - `SetPage(int page)`: Clamps `page` to `[0, Math.Max(0, PageCount - 1)]`.
     - `EnsurePageFor(PawnId pawnId)`: Finds `pawnId` in `_customOrder`. If found, computes `int targetPage = index / PageCapacity` and sets `Page = targetPage`.
     - In `Refresh(snapshot, selected)`:
       - Computes `PageCapacity = Math.Max(1, stripCapacity)`.
       - Computes `PageCount = (TotalCount + PageCapacity - 1) / PageCapacity`.
       - Clamps `Page` to `[0, Math.Max(0, PageCount - 1)]`.
       - Populates `Cards` with only the slice of colonists corresponding to `Page`:
         `from = Page * PageCapacity` to `to = Math.Min(from + PageCapacity, TotalCount)`.
   - **Reordering**:
     - `Swap(PawnId a, PawnId b)`: Swaps the exact positions of `a` and `b` in `_customOrder`.
     - `SwapIndices(int indexA, int indexB)`: Swaps positions in `_customOrder` by index.
   - **Serialization Support**:
     - `IReadOnlyList<PawnId> CustomOrder => _customOrder;`
     - `void LoadOrder(IEnumerable<PawnId> order, int page);`

2. **`HudLayout` (`Assets/Odyssey/Hud/HudLayout.cs`)**:
   - Add constants for the compact pager control:
     - `PagerBtnWidth = 18;`
     - `PagerGap = 4;`
     - `PagerLabelWidth = 36;`
     - Total pager width: `2 * PagerBtnWidth + PagerLabelWidth + 2 * PagerGap = 76 px`.
   - Update `StripRoom` and `ColonistStrip` bounds:
     - The pager is docked alongside the right edge of the colonist cards.
     - When multiple pages exist, the strip container bounds accommodate the cards plus the docked pager, preserving `StripClearance` (32 px) to the Clock and Stores panels and maintaining resting coverage <= 20.00%.

### B. UI Presentation Seam (`Odyssey.Presentation.Ui`)
1. **`HudGlyphKind` & `HudGlyph.cs`**:
   - Add `ChevronLeft` and `ChevronRight` to `HudGlyphKind`.
   - Add 2D vector drawing cases in `HudGlyph.GenerateVisualContent` for left/right chevrons, matching the existing `ChevronDown` and `ChevronUp` stroke geometry (1.8 px stroke).

2. **`HudShell.Panels.cs` & `HudShell.cs`**:
   - **Pager Control UI**:
     - Create `.roster-pager` element with `_prevPageBtn` (`HudGlyphKind.ChevronLeft`), `_pageLabel` (e.g. `"1 / 3"`), and `_nextPageBtn` (`HudGlyphKind.ChevronRight`).
     - Dock `.roster-pager` to the right of the card row inside `_strip`.
     - Wire clicks on `_prevPageBtn` to decrement page and `_nextPageBtn` to increment page.
     - Register `WheelEvent` on `_strip`:
       - If `evt.delta.y > 0`: `_roster.SetPage(_roster.Page + 1); RefreshStrip();`
       - If `evt.delta.y < 0`: `_roster.SetPage(_roster.Page - 1); RefreshStrip();`
       - `evt.StopPropagation();` prevents world camera zoom while hovering over the strip.
   - **Auto-Page Flipping on Selection**:
     - When `_directors.Selection.Dirty` is true (or when `ChooseColonist` is called from an alert or world click):
       - If a single colonist is selected and not on the current page, call `_roster.EnsurePageFor(selectedId)`.
       - If a multi-selection exists, stay on current page if any selected colonist is on it; otherwise jump to lead colonist's page.
   - **Right-Click Drag-and-Drop Slot Swapping**:
     - In `NewCard(int slotIndex)`:
       - On `PointerDownEvent` (`evt.button == 2`):
         - Record `_rightDragStartPos = evt.position`, `_draggedPawnId = model.Id`, `_draggedSlot = slotIndex`.
         - Set `_pendingRightDrag = true`.
       - On `PointerMoveEvent`:
         - If `_pendingRightDrag` and `(evt.position - _rightDragStartPos).sqrMagnitude >= 16`:
           - Set `_isRightDragging = true`, `_pendingRightDrag = false`.
           - `card.CapturePointer(evt.pointerId)`.
           - Create/show floating drag ghost (`PickingMode.Ignore`, semi-transparent portrait + name) following the cursor.
           - Apply `.card--dragging` (dimmed opacity) to source card.
         - If `_isRightDragging`:
           - Update ghost position: `ghost.style.left = evt.position.x - 26; ghost.style.top = evt.position.y - 26;`.
           - Hit-test target card under cursor via slot bounds or coordinate arithmetic.
           - Highlight hovered target card with `.card--drag-target`.
           - Edge & pager auto-paging: if cursor hovers over `_prevPageBtn` or `_nextPageBtn` for > 350 ms, advance/retreat page.
       - On `PointerUpEvent` (`evt.button == 2`):
         - `card.ReleasePointer(evt.pointerId)`.
         - If `_isRightDragging`:
           - Identify drop target card `targetPawnId`.
           - If `targetPawnId != PawnId.None && targetPawnId != _draggedPawnId`:
             - `_roster.Swap(_draggedPawnId, targetPawnId);`
           - Cleanup: hide ghost, clear drag classes, call `RefreshStrip()`.
           - `_isRightDragging = false`.
         - `_pendingRightDrag = false`.
       - On `PointerCancelEvent`:
         - Cleanup drag state, release pointer capture.

3. **`Hud.uss`**:
   - Add styles for `.roster-pager`, `.roster-pager__btn`, `.roster-pager__label`.
   - Add styles for drag states:
     - `.card--dragging`: `opacity: 0.35;`
     - `.card--drag-target`: `border-color: #6fd3e3; scale: 1.03;`
     - `.card-drag-ghost`: `position: absolute; opacity: 0.75; pointer-events: none;`

### C. Save/Load Persistence Seam (`ViewStateSection`)
1. **`ViewStateSection` (`Assets/Odyssey/Presentation/Bootstrap/ViewStateSection.cs`)**:
   - Bump `SectionVersion` or add optional roster data block:
     - Write: `RosterPage` (int), count of ordered pawns (int), and array of `PawnId` integers.
     - Read: If version matches / data available, read page and list of `PawnId`s.
   - Wire `Capture(HudShell shell)` to read `_roster.CustomOrder` and `_roster.Page`.
   - Wire `Apply(HudShell shell)` to restore `_roster.LoadOrder(order, page)` on load.

---

## 2. Step-by-Step Implementation Plan

### Step 1: `RosterModel` Pagination & Ordering Logic
- Update `Assets/Odyssey/Hud/RosterModel.cs`:
  - Implement `_customOrder`, `Page`, `PageCapacity`, `PageCount`, `TotalCount`.
  - Implement `SetPage`, `EnsurePageFor`, `Swap`, `LoadOrder`.
  - Refactor `Refresh` to paginate `Cards` slice and maintain `_customOrder`.
- Add unit tests in `Assets/Odyssey/Tests/Hud/HudModelTests.cs`:
  - Test pagination slicing (e.g. 50 cards with capacity 12 = 5 pages).
  - Test page clamping on out-of-bounds inputs.
  - Test `EnsurePageFor` correctly switches active page to target colonist.
  - Test `Swap` correctly swaps two colonists in place.
  - Test deceased pawns are culled and new pawns appended.

### Step 2: `HudLayout` Pager Geometry & Metrics
- Update `Assets/Odyssey/Hud/HudLayout.cs`:
  - Define `PagerBtnWidth`, `PagerLabelWidth`, `PagerGap`.
  - Ensure `StripRoom` and `ColonistStrip` bounding boxes cleanly include the pager without overlapping Clock or Stores.
- Verify tests in `Assets/Odyssey/Tests/Hud/HudLayoutTests.cs` (coverage <= 20%, panel spacing).

### Step 3: Vector Glyphs (`ChevronLeft`, `ChevronRight`)
- In `Assets/Odyssey/Presentation/Ui/HudGlyph.cs`:
  - Add `ChevronLeft` and `ChevronRight` to `HudGlyphKind`.
  - Add vector drawing paths using `Painter2D` in `GenerateVisualContent`.

### Step 4: UI Toolkit Stylesheet (`Hud.uss`)
- Add USS rules for `.roster-pager`, `.roster-pager__btn`, `.roster-pager__label`.
- Add USS rules for `.card--dragging`, `.card--drag-target`, `.card-drag-ghost`.

### Step 5: Presentation Wiring in `HudShell`
- In `Assets/Odyssey/Presentation/Ui/HudShell.Panels.cs`:
  - Build `_rosterPager` element and add to `_strip`.
  - Wire button clicks and wheel scroll events to `_roster.SetPage`.
  - Implement right-click pointer down/move/up drag handlers on `NewCard`.
  - Implement drag ghost rendering and target slot hit-testing.
  - Implement auto-page flipping on alert clicks / selection changes.
- In `Assets/Odyssey/Presentation/Ui/HudShell.cs`:
  - Handle cleanup in `Update()` if mouse right button released off-screen.

### Step 6: Save/Load Persistence in `ViewStateSection`
- In `Assets/Odyssey/Presentation/Bootstrap/ViewStateSection.cs`:
  - Serialize `_roster.CustomOrder` and `_roster.Page` into the `"view"` save section.
  - Deserialize and apply to `RosterModel` during save load.
- Add test in `Assets/Odyssey/Tests/PlayMode/ViewStateTests.cs` verifying roster order round-trip across save/load.

### Step 7: Verification & Gate Checks
- Fast-tier suite: `bash scripts/test-fast.sh` (Sim + Hud tests).
- Content gates: `python tools/wiki/build_wiki.py --check` and `python tools/wiki/emit_labels.py --check`.
- Unity EditMode suite: `scripts/unity.sh test editmode`.
- Unity PlayMode suite: `scripts/unity.sh test playmode`.
- Update `docs/journal.md` with complete rationale, measurements, and design decisions.
