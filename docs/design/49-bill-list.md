# 49 — The bill list: one control for everything that takes orders

**Status:** built 2026-09-25, not yet played. Branch `claude/bills-pane`, stacked on
`claude/cooking` (PR #227), which is where bills came from.

**Brief:** the owner's mockup *20a*, "rebuild the workbench inspect pane (bills)", with the
instruction to make it **composite** — cooking now, crafting later, anything that takes orders
uses the same control — and to stay consistent with the HUD as it is rather than follow every
detail, **especially the tile part**.

## 1. What it is

`BillList` (`Assets/Odyssey/Presentation/Ui/BillList.cs`) is a `VisualElement` that draws a
`BillsModel` and sends that model's intents. The inspect pane hosts it (`HudShell.Bills`, now 50
lines) under a tile's header; nothing in the control knows what a meal is.

| Piece | Owner | What it holds |
|---|---|---|
| Which buildings take bills, whether they need power, what *Add a bill* adds | `BillsModel.Stations` | one `BillStation` row per kind |
| A recipe's name and the category of what it makes | `BillsModel.RecipeKeys`, `RecipeCategories` | one entry per `RecipeHandle` |
| Every rule the pane shows (status line and its ink, stepper limits, progress, the strip, the switch) | `BillsModel` | fast tier, `BillsModelTests` |
| Every number (widths, heights, gaps, glyph sizes, opacities) | `BillsLayout` | fast tier, `BillsLayoutTests` |
| The paths of the glyphs | `HudIcons` | drawn by `PathGlyph`, never typed |

**A crafting bench is two rows**: one in `Stations`, and its recipes in the recipe tables. It then
gets the whole pane — strip, rows, stepper, reorder, Add — with no view code.

## 2. The pane

- **Width 680** (`.inspect--bench`, `BillsLayout.PaneWidth`, held to each other by
  `HudStyleSheetTests`). Any tile whose edifice is a station gets it; a store stays 560, a bare tile
  280. It was 280 for the cooker until now — the cooking branch's rows did not fit it.
- **The header and the tile facts are the pane's own**, unchanged. The mockup redraws both (a 36 px
  icon tile, the meta line under the name, a two-column fact grid, `minable` renamed); the owner
  said not to worry about the tile part and to stay consistent, so they are left for a pass over
  every tile pane at once rather than made different on this one.
- **The status strip** (48 high, warn wash, 3 px warn bar): the bolt struck through, **"No power"**
  and nothing under it, then the station's switch — black fill, green border and ink, "Switch off"
  or "Switch on" by what pressing it will do. It is up only while a station that needs power has
  none (`BillsModel.HasProblem`). **While it is up the tile's own Switch row is hidden**, because
  it is the same button; it comes back when the strip goes.
- **What a product takes, and how much there is** (merged from #227, owner: *"I didn't know what
  ingredients I needed"*): one 30 px line under the Bills strip, the station's needs on the left
  (`BillStation.NeedsKey`, so a campfire says its wood) and the supply on the right, in warn when
  the map holds no raw food.
- **No gaps between sections.** The list runs to the pane's edges (negative margin of the pane's
  padding), and each section opens on a strip and closes on a 1 px rule.

## 3. The row

`12 | 28 | name | 132 | 92 | 64 | 92`, gap 9, 48 high. The name column is what is left — **180 px**
— and `BillsLayoutTests.TheRowsColumnsFitThePaneWithRoomForTheName` fails if a column widens past
leaving it 140.

| Column | Shows | Notes |
|---|---|---|
| Index | 1, 2, 3 in the faint ink | mono |
| Product | a 28 px tile, the category glyph, a 2 px edge in the category hue | food's green for a meal; the storage pane's own glyphs |
| Name | the recipe, and under it the status line | see below |
| Mode | the mode's word and the cycle mark, press to go round | the registry's words: *Until you have*, *Make*, *Forever* |
| Stepper | minus (bad), the target (mono, centred), plus (good) | shift for ten, as before; minus off at one; both off and `--` for Forever |
| Progress | "3 / 10" over a 4 px accent track | `--` and an empty track for Forever; the spaces drop past seven characters ("100/120") so the count never overflows its 64 px |
| Actions | reorder pair, pause/resume, remove | see below |

**The status line** is, in order: *Waiting for power* in warn at a dark station (a paused bill
there too — it will still be waiting once resumed); *Paused* in dim; *Done* in meta when the count
is met; otherwise **nothing**, and the name centres. The mockup's *Anyone can do this* is a worker
rule the game does not have, and a line that names a setting the player cannot find is the thing
the brief said not to add.

**Reorder stays.** The mockup's actions are pause and remove; the game can also move a bill up and
down (the top one is worked first, design 48 §5), and the brief says existing handlers stay wired.
So the actions column is three controls, not two: the pair stacked in one 28 px box, then pause,
then remove. Minus is still the only red control in a row; the bin is neutral.

**A paused row** draws columns 2–6 at 55 %; the index and the actions stay whole so it can be
resumed.

## 4. Decisions that reverse the cooking branch

- **The list's height is its content**, not a fixed five rows. The earlier rule (design 48 §14)
  borrowed the storage pane's lesson that a pane growing upward moves controls under the pointer.
  Here the growth is *above* Add a bill: the pane is docked to the bottom, so pressing Add, and
  everything below it, stays still while rows arrive over it. A fixed block would have been 240 px
  of empty pane under a single bill. Removing a bill does move the rows above the one removed down
  by a row; that is the one case left to watch in play.
- **Labels**: `ui.bill.suspended` reads **Paused** (the button is a pause button),
  `ui.bill.none` reads **No bills yet**, and `ui.bill.waiting` (*Waiting for power*) is new. The
  mode words are unchanged — the mockup's are the reference game's own strings, which the clean-room
  rule keeps out.

## 5. Where the build departs from the mockup, and why

| Mockup | Built | Why |
|---|---|---|
| Header: 36 px icon tile, meta line under the name | the pane's existing header | owner: stay consistent, not the tile part |
| Tile facts: two-column grid, `Deconstruct` for `minable` | the existing fact rows | the same |
| Actions: pause, delete | reorder pair, pause, delete | reorder exists in the game |
| Status line: worker rule | nothing, the name centred | no such setting |
| Mode order *Do X times > Until you have > Forever* | *Until you have > Make > Forever* | the sim's order and the registry's words; the mockup's are the reference's strings |
| Add a bill opens the recipe chooser | adds the station's one recipe | there is no chooser; one recipe exists. The chooser is owed when a station has two |
| Every figure mono 500 | mono through `HudText` (`numeric: true`) | the same face; weight 500 draws as the regular instance (`HudType`) |
| 14/600 words | the renderer's bold, as `HudType.BoldFrom` does everywhere | one variable font file |

## 6. Tests

- Fast tier: `BillsModelTests` (18, of which 10 new) and `BillsLayoutTests` (5, new).
- PlayMode: `HudShotTests.PhotographTheBillList` puts an unpowered cooker and two campfires by the
  start, gives two of them three bills in three modes with one paused, selects each, asserts the
  list is shown on a pane at 680, and writes `Logs/bills-nopower.png`, `bills-campfire.png` and
  `bills-empty.png`.
- Not tested and cannot be: whether a click reaches a button (`docs/bug-patterns.md`, the known
  gap). The buttons send the same intents the old ones did and `BillsModelTests` pins each one.

## 7. The second station (DM8, 2026-09-26)

The smelter (design 62 §9) is a row in `BillsModel.Stations`, as §2 promised a bench would be. What
the row needed that the cooker's did not, each an optional field on `BillStation`:

- **Two recipes** (`Recipes`). *Add a bill* adds the station's recipe the list holds fewest bills
  of, the first on a tie — iron, then copper — and **pressing a row's name** goes round what it
  makes (`PressRecipe`, `BillEdit.SetRecipe`), keeping its mode and target. That is the chooser
  §5 said was owed, without a menu; a station with one recipe ignores the press.
- **A hopper** (`Burns`). The supply line carries the fuel too — *Ore for 3 batches · Fuel for 12*
  — from `StationView.FuelBatches`, and the warn strip says **No fuel** (and the rows *Waiting for
  fuel*) when the hopper cannot pay for the next batch, in the place a cooker says *No power*. No
  switch is offered: there is nothing to switch on.
- **Its own supply words and hint** (`SupplyKey`, `NoSupplyKey`, `SupplyHint`): the empty-line
  tooltip was a literal in `BillList` and is the model's now.

`BillList` changed in two lines: the supply tooltip reads `SupplyHint`, and the name column is a
button that sends `PressRecipe`. Tests: `SmelterBillsTests` (fast tier).
