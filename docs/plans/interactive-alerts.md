# Interactive Alerts: Subject-Target Selection, Dismissals, and Alignment Refactor

**Target:** Make the HUD alerts panel actionable, concise, and interactive:
1. Replace verbose sentences and trailing detail clauses with direct subject-action phrasing (e.g. **Wrenn** is close to breaking).
2. Highlight the subject name in bold severity colour; clicking it (or the alert text) selects the colonist and jumps the camera to them via `HudDirectors.ChooseColonist`.
3. Vertically middle-align the alert symbol with the text row.
4. Add an aligned dismiss 'X' button to every alert row to suppress that alert until the condition clears and re-occurs.
5. Add a "Clear all alerts" 'X' button in the top-right corner of the Alerts header panel.

---

## 1. Architecture & Seams

### Simulation Seam
- No simulation changes required. The simulation already publishes `PawnView` with `Id`, `Cell`, `Food`, `Mood`, `JobDef`.

### HUD Model Seam (`Odyssey.Hud`)
- **`ColonistNames`**: Already provides `ColonistNames.Of(snapshot, pawn.Id)` (e.g. "Wrenn").
- **`AlertRow`**: Refactored to carry target information and a dismiss token:
  ```csharp
  public readonly struct AlertRow
  {
      public readonly string Key;
      public readonly string TargetName;    // "Wrenn"
      public readonly string TargetSuffix;  // " is close to breaking"
      public readonly string TargetPrefix;  // "" (for prefix cases if any)
      public readonly string Lead;          // "Wrenn is close to breaking"
      public readonly AlertSeverity Severity;
      public readonly PawnId Pawn;          // PawnId.None if colony-wide
      public readonly CellRef Cell;         // For cell-based alerts
      public readonly int DismissKey;       // Stable token for dismiss tracking
  }
  ```
- **`AlertModel`**:
  - Emits one row per individual colonist for personal alerts (`StarveKey`, `BreakKey`, individual idle).
  - Colony-wide idle emits when all colonists have been idle for `IdleSustain` seconds.
  - Maintains `HashSet<int> _dismissed` tracking dismissed alerts by `DismissKey`.
  - Clears a `DismissKey` when the underlying condition clears (e.g. food climbs above `StarveClearAt` or mood above `BreakClearAt`), so that when the colonist starves/breaks again, the alert re-arms and fires afresh.
  - Provides `Dismiss(int dismissKey)` and `DismissAll()`.
- **`HudLayout`**:
  - Update `AlertHeight` from `52` (two-line legacy) to `26` (one 13 px text line at 26 px box height).
  - Update `AlertGap` and `AlertsHeight` calculation.

### Presentation Seam (`Odyssey.Presentation.Ui`)
- **`HudShell.Panels.cs`**:
  - `BuildPanels()`: In `_alertsPanel` header, add `CloseButton(header, "all alerts", () => { _alerts.DismissAll(); RefreshAlerts(); })` placed at the top-right corner opposite "Alerts".
  - `NewAlertRow()`:
    - Root `.alert`: `align-items: center`, `flex-direction: row`, `justify-content: space-between`.
    - Icon `.alert__icon`: middle-aligned vertically.
    - Text `.alert__text`: contains `Label TargetLabel` and `Label MessageLabel`.
    - Register click on `TargetLabel` / `Text`: calls `_directors?.ChooseColonist(view.TargetPawn, _boot.World.Views.Current)`.
    - Dismiss button `.alert__dismiss`: 18x18 px, carries `HudGlyph(HudGlyphKind.Close, 11f, HudTokens.TextDim)`. Clicking it calls `_alerts.Dismiss(view.DismissKey)` and refreshes.
- **`Hud.uss`**:
  - Update `.alert` to `align-items: center; min-height: 26px; justify-content: space-between;`.
  - Add `.alert__target` (bold, severity colour, cursor pointer, hover underline).
  - Add `.alert__dismiss` styling (right-aligned in a column, hover effect).

---

## 2. Step-by-Step Execution Plan

### Step 1: `AlertModel` and `AlertRow` Refactor
- Update `AlertRow` struct in `Assets/Odyssey/Hud/AlertModel.cs` with target and dismiss fields.
- Update `AlertModel.Refresh`:
  - Enumerate starving colonists -> emit an `AlertRow` for each with `TargetName = ColonistNames.Of(snapshot, pawn.Id)` and `TargetSuffix = " is starving"`.
  - Enumerate breaking colonists -> emit an `AlertRow` for each with `TargetSuffix = " is close to breaking"`.
  - Idle -> if single colonist: `"[Name] is idle"`; if colony: `"Colony is idle"`.
  - Filter out any rows whose `DismissKey` is in `_dismissed`.
  - Clean up `_dismissed` when conditions recover past their clear thresholds.
  - Implement `Dismiss(int dismissKey)` and `DismissAll()`.

### Step 2: `HudLayout` Metric Alignment
- In `Assets/Odyssey/Hud/HudLayout.cs`:
  - Set `AlertHeight = 26;`
  - Ensure `AlertsHeight(int count)` computes total height based on single-line rows.

### Step 3: UI Toolkit Stylesheet & Layout (`Hud.uss`)
- Update `.alert`, `.alert__icon`, `.alert__text` in `Assets/Odyssey/Presentation/Ui/Hud.uss`.
- Add `.alert__target`, `.alert__message`, `.alert__dismiss` with hover styles.
- Keep dismiss 'X' aligned at the right in an exact column across all rows.

### Step 4: Presentation Wiring (`HudShell.Panels.cs` & `HudShell.cs`)
- Update `AlertRowView` in `HudShell.cs` to hold target labels, dismiss element, `PawnId`, `DismissKey`.
- Update `NewAlertRow()` to build the new hierarchy: Icon -> Text (Target + Suffix) -> Dismiss 'X'.
- Wire click events:
  - Text click -> `_directors?.ChooseColonist(view.TargetPawn, _boot.World.Views.Current)`.
  - Dismiss 'X' click -> `_alerts.Dismiss(view.DismissKey); RefreshAlerts();` (with `evt.StopPropagation()`).
- Add Clear All button in `_alertsPanel` header using `CloseButton`.

### Step 5: Test Suite Updates & Verification
- Update `Assets/Odyssey/Tests/Hud/HudPanelTests.cs`:
  - Update `StarvationLeadsWithTheActionableClause` to check `TargetName`, `TargetSuffix`, and single-line format.
  - Replace `SeveralSubjectsBecomeOneLineWithACount` with tests verifying individual rows per colonist.
  - Add test for `Dismiss(dismissKey)` verifying dismissal until need recovers.
  - Add test for `DismissAll()`.
- Update `Assets/Odyssey/Tests/Hud/HudStyleSheetTests.cs` to verify `.alert` min-height equals `HudLayout.AlertHeight`.
- Verify fast test tier (`scripts/test-fast.sh`).
- Verify wiki checks (`python3 tools/wiki/build_wiki.py --check` and `python3 tools/wiki/emit_labels.py --check`).
- Verify EditMode tests via Unity wrapper.
