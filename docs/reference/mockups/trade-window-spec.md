# The trade window, as specified: mockups 28a Sell and 28b Buy

**2026-09-26.** The owner's specification for unit T6, returned from Claude Design against the brief
in `trade-window-brief.md`, and handed over with *"fit it into the style we have … and go ahead"*.
It supersedes the brief's five-state two-column ledger. Built as `Odyssey.Hud.TradeModel`,
`Odyssey.Hud.TradeLayout` and `HudShell.Trade.cs`. Where the build departs from this file and why
is design 57 §10.

## What it is

A pausing modal. It opens when the negotiating colonist reaches the trader. It has two modes,
**Sell** and **Buy**, and one deal spans both. Gold is an item and never a row: it is the balance.
The deal commits all at once on Confirm: sold goods leave the stores, and bought goods and any gold
owed are set down beside the trader. Prices come from design 57 §3; the view computes none.

**Not drawn, even greyed:** factions and goodwill, haggling, gifts, quests and caravans, condition
or rot, a world map.

## Tokens

Panel `#0c1014` (1 px border white 13 %, 5 px radius), rule white 7 %, control border white 26 %.
Text `#eef3f6`, meta white 66 %, dim white 50 %, accent `#6fd3e3`, on-accent `#0b1116`. Warn
`#e8b55c` for a disabled reason, Bad `#e06a5c` for an impossible balance. The scrim is the settings
window's, rgba(6,10,12,.58).

The category hues are the Inventory tab's. Quality inks: Poor `#e06a5c`, Normal white 66 %, Decent
`#e9e08c`, Uber `#45c7b0`, Epic `#b98ce8`.

Flat everywhere. Every string is ASCII, with minus, plus and close drawn as SVG. Words are in
Archivo Narrow and every figure in IBM Plex Mono 500 with tabular figures. There are six type steps.

## The window: 820 wide, centred, over the scrim

| Band | Height | Contents |
|---|---|---|
| Header | 64 | A 40 px face placeholder, "TRADE" (11/600 dim) and the trader's name (19/600), then "Trader · leaves in 14 h · Tom Hale negotiating" (12/400 meta, hours in mono). A 28 px close box on the right, which cancels. |
| Mode switch | 52 | Two joined segments, **Sell \| Buy**: 32 high, at least 110 wide, with the count of moving items in 12 mono. Active is an accent fill with on-accent ink; inactive is a control border. Opens on Sell; Tab switches. |
| Column header | 26 | Grid `28 / 1fr / 72 / 72 / 64 / 164 / 64`, gap 12: (icon) ITEM, QUALITY, IN STOCK (right), PRICE (right), QUANTITY (centre), TOTAL (right). |
| Category rows | 28 | Only the categories with rows in the current mode, in the game's order. A white 3 % fill, a 3 px inset rail in the hue, a 12 px glyph, and the name at 11/600 in the hue. |
| Item rows | 34 | See the list below. |
| Foot | 72 | Four cells: SELLING FOR, BUYING FOR, YOU PAY / YOU RECEIVE / BALANCE, and GOLD ("312 you 640 trader"). |
| Buttons | 56 | Reset, a spacer, the reason in Warn, Cancel (at least 120 wide), and Confirm (at least 140 wide, accent). |

**Item rows, left to right:**
1. **Icon**: a 28 px placeholder with a 2 px edge in the category hue.
2. **Item**: the bare name ("Pistol", never "Pistol (Decent)").
3. **Quality**: the tier's name in its ink, blank for none.
4. **In stock**: the count.
5. **Price**: what one sells or buys for.
6. **Quantity**: minus, the figure and plus (28 px each) and All (44 px), with a gap of 4.
7. **Total**: the row's gold, in accent, blank at nought.

**Row states.** Idle: the figure at nought in dim. Moving: the figure in accent in an accent 60 %
box, the row filled with accent at 6 %, and a 2 px accent rail. The *All* label turns accent at
in-stock. Minus and plus are neutral. Minus is at 30 % and disabled at nought, and plus the same at
in-stock.

**Confirm is off**, with its reason:
- *Nothing to trade*, while nothing is moving.
- *Not enough gold*, while the colony owes more than it holds; the balance and "you" turn Bad.
- *The trader cannot pay*, while the trader owes more than its purse; the balance and "trader" turn
  Bad.

**Height.** The worst case is 18 items and 4 headings, about 882 in all, which fits 1080. Never a
scrollbar: below 1,000 px of screen height the list pages at 20 lines.

## Around the window (mockup 27e)

- **The right-click menu** over the trader: one Trade row. When it cannot be done, the row is at
  50 % with its reason.
- **The trader's pane**: the standard 560 px pane, "Trader · waiting by the fire", with no tabs and
  no Draft. Its body is two 30 px rows, *Leaves in 14 h* and *Carrying 640 gold*.
- **The Events row**: "Trader", with the briefcase icon.

## Acceptance

- Each mode lists only its side's stock, and the deal survives switching.
- The quality column uses the quality colours.
- Moving rows show the accent figure, fill and rail, and minus and plus clamp.
- The foot is right for the sample deal: sell 120 wood and 40 carrots, 160; buy 3 medical supplies
  and a pistol, 294; you pay 134.
- The three disabled reasons show, with the impossible side in Bad.
- Every string is ASCII, and nothing leaves the lists above.
