# 35 — The Inventory tab (F2)

**Status, 2026-09-23: built, not yet played.** Same branch and PR as the Research tab (design 34).

## 1. What was asked

The owner's spec (2026-09-23): a window on **F2**, between Work and Research, listing every item
the colony holds grouped by category; select an item to see which places hold it; **Go** on any
place moves the camera there and opens that store's pane. Explicitly out of scope for now: stored
versus unstored, layer and cell columns, links into the Almanac.

## 2. What it reads, and the one contract change

Everything is read off the published frame by `InventoryModel` (Unity-free, fast tier):

- **A thing counts when it is in a store.** A contained thing is published at its shelf's cell
  (`ThingView.Container`), matched against `StorageUnits`; a loose thing counts when its cell is
  in `Stores`. Anything else is left out, as the spec asks.
- **Stores are named the way the inspect pane names them** — "Stockpile 3", "Shelf 2", one
  numbering across both kinds (`StorageZones.OrdinalOfCell`). That number was only published per
  selected cell (`CellDetail.StorageOrdinal`), so **`StoreView` and `StorageUnitView` now carry an
  `Ordinal`**, computed by the simulation once per zone per publish. Deriving it in the HUD would
  have been a second owner of the rule, and the tab and the pane could have called one store two
  things. Snapshot views are neither saved nor hashed: **no golden moved.**
  `StoreOrdinalTests` asserts every published number equals the one the pane gives.
- **Rebuilt only when the stock moves.** `Refresh` walks the frame for a signature at the HUD's
  middle cadence and rebuilds only when it changes.

## 3. The geometry

`InventoryLayout`: 1100 wide; 34 header, 38 toolbar, 420 body, plus the frame; a 640 item table
(`1fr 80 70`) and a 458 where-it-is pane (`1fr 70 52`); 30 px rows with the rule inside them;
**13 rows under the heading**, 12 when paged, and a category's heading repeats at the top of the
next page when its items run on. Category headings count as rows.

## 4. Behaviour

- Opens on the item with the **largest total**, at its **largest store**.
- The six storage categories in registry order, always; an empty one shows its name and no count.
  **A search hides the empty ones** and filters live; if it hides the selected item, the
  selection moves to the largest match.
- **Total** is the default sort, descending; Places sorts most-first, Item A-first. Headings
  never move. Exactly one header carries the drawn sort mark.
- **A second click on the selected item row is a Go** to its largest store.
- **Go** and the primary button close the tab, **move the slice to the store's layer**, select
  the store's cell and jump the camera. Unlike the Animals tab, the depth moves: a store may be
  underground, and the pane will not look through a floor. The camera lands on the store's
  **largest stack of that item**, not its corner. The selection is the cell rather than the pile
  in it (`SelectionDirector.ChooseCell`), because the pane leads with the store only when the
  cell is the subject.
- The header total is every unit in every store, with thousands separators.

## 5. Where the build departs from the spec, and why

| Spec | Built | Why |
|---|---|---|
| Category hues `#7fb85a #d95a6a #b0793f ...` | `HudTheme.ItemCategoryHues` | The spec copied the storage brief's originals; the pane shipped contrast-corrected ones (`StorageThemeTests`), and "reused from the stockpile pane" means those. |
| Go at 12/500 | 12/400 | 500 is not a step of the 12 px role. |
| Places listed without limit | **six rows** | What the pane's height holds under the identity band, the hint and the pinned button. A seventh store holding one item is not listed; recorded below. |
| Bar 44 high, 12% wash | as design 34 §5 | Same reasons. |

## 6. Open

- **More than six stores holding one item** are not listed. Paging the WHERE table is the fix if
  a colony ever reaches it.
- Loose and carried goods, when stored-versus-unstored comes into scope.
- The 32 px pixel icons drop into the 18 px and 32 px tiles; the tiles are placeholders.
