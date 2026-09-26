# Brief for Claude Design — the trade window

**2026-09-26.** The prompt below is handed to Claude Design verbatim, with the attachments listed at
the end. Decisions behind it: `docs/research/trading-interview.md` (owner: gold as a real item,
goods sold out of stores and bought goods set down beside the trader, a colonist sent by
right-click, a pausing modal) and `docs/design/57-trading.md` (the model the window shows; the
prices in the table are its §3 formula). It is built as unit T6 of `docs/plans/trading.md` once the
design is agreed and the simulation units under it exist.

---


## The prompt

You are designing one modal window, plus three small touches around it, for **Odyssey**. Odyssey
is a colony-simulation prototype in the RimWorld mould, set in a ruined sci-fi city and rendered in
3D with discrete vertical layers. The interface is a dark, flat HUD over the 3D board. It is already
built and shipped, and you must match it exactly.

Attached are:
- the HUD's own mockup (`hud-v2.html`);
- the Work tab's mockup (`work-v1.html`), for the wider vocabulary of rows, columns and marks;
- screenshots of the running game: the **Inventory** tab (its category headings are the ones you
  reuse), a station's **bill list** (its stepper and row anatomy), the **settings window** (the
  game's other modal, with its scrim), the **right-click menu** offering *Equip*, and a selected
  **bandit's** inspect pane.

**Everything you draw must look like it came from the same hand as those.**

### What trading is

A **trader** walks in from the edge of the map and waits by the colony's campfire for about a game
day. The player selects a colonist, right-clicks the trader and chooses **Trade**. The colonist
walks over, and when they arrive the **trade window** opens and the game pauses. The player moves
goods across in both directions, watches a running **balance** in **gold**, and presses
**Confirm**. The deal then happens all at once. Goods the colony buys, and any gold it is owed, are
set down beside the trader for the colonists to haul away.

**Gold is the only currency.** It is an item the colony stores like any other, and it is never a
row in the ledger; it is the balance. The trader carries a limited **purse** and a small rolled
**stock**. The trader always sells dearer than they buy.

Nothing else exists, and you must not draw it, not even greyed:
- factions, goodwill or relations;
- haggling or a negotiator's skill;
- gifts, quests or caravans;
- item condition, rot, or quality affecting the price;
- a world map;
- tabs inside the window.

### The window: content

Names are the game's own and are never renamed. Rows marked *(needs a key)* have no registered name
yet; draw them with the words given.

**Header.** "TRADE" as the panel label, the trader's name as the title (e.g. "Ines Varga"), and a
meta line "Trader · leaves in 14 h" *(needs a key)*. There is a close X at the right, which
cancels.

**Two column heads**, left and right, over the list:
- left: "Colony" *(needs a key)*, with the negotiating colonist's name under it in meta
  ("Tom Hale negotiating");
- right: the trader's name.

**The list.** One row per item that **either side** holds. Items are grouped under the game's own
storage category headings (**Food**, **Medicine**, **Materials**, **Weapons**; the empty *Books*
and *Items* never show), drawn exactly as the Inventory tab draws them: a tinted row, the
category's glyph, and its name in its hue. Within a group, the order is the game's item order.

A row, left to right:

| Part | Shows | Set in |
|---|---|---|
| icon | the item's 32 px pixel-art icon drawn at 28 | placeholder square |
| name | "Medical supplies"; a weapon carries its quality: "Crowbar (Normal)" | Archivo Narrow 14/500 |
| colony qty | how many the colony can trade, e.g. "6"; blank if none | IBM Plex Mono 14/500 |
| our price | the gold the trader pays for one, e.g. "12" | mono, meta ink |
| **transfer** | the control: see below | mono |
| their price | the gold one costs to buy, e.g. "28" | mono, meta ink |
| trader qty | how many the trader has, e.g. "8"; blank if none | mono |

**The transfer control** is the heart of the window. The figure is **signed from the colony's
side**: **+3** means three come to us and **-120** means 120 go to the trader; the empty state is
**0** in dim ink. It is changed by:
- one press of either side button (a unit, or ten with Shift);
- a "move all" press at each end.

The direction must read at a glance. Use the HUD's own **chevrons** (it has single and double
chevrons as drawn paths), pointing at the side the goods are going to. **Do not use the bill
list's red minus and green plus**: here neither direction is good or bad. A row with anything
moving is marked, e.g. with the accent on its figure; propose how. A side that holds none of an
item cannot give it, so that half of the control is disabled.

**The foot**, one band under the list:
- left: "Colony gold" *(needs a key)* "312";
- centre: **the balance**. "You pay 134" or "You receive 86" *(both need keys)*, in large mono. It
  reads "Balance 0" when nothing is moving;
- right: "Trader gold" *(needs a key)* "640".

Each gold figure carries the gold icon: a 32 px pixel-art coin, drawn as a placeholder square.
Then three buttons: **Reset** (clears every row), **Cancel** (closes; the colonist walks away) and
**Confirm**. Confirm is the accent button, and when it is disabled a reason sits beside it in Warn:
"Not enough gold", "The trader cannot pay", "Nothing to trade" *(each needs a key)*.

**Prices are whole gold.** The numbers below are what the game computes. Do not change them:

| Item | Colony | Our price | Their price | Trader |
|---|---|---|---|---|
| Carrots | 90 | 1 | 2 | — |
| Berries | 25 | 1 | 2 | — |
| Rations | 12 | 3 | 9 | 30 |
| Medical supplies | 6 | 12 | 28 | 8 |
| Wood | 240 | 1 | 2 | — |
| Stone | 180 | 1 | 2 | — |
| Iron ore | — | 1 | 3 | 60 |
| Scrap metal | — | 1 | 5 | 40 |
| Bat (Normal) | 2 | 15 | 35 | — |
| Crowbar (Normal) | 1 | 18 | 42 | 1 |
| Pistol (Decent) | — | 90 | 210 | 2 |

That is eleven item rows under four headings. The game has eighteen items at most, so **design for
up to eighteen rows**.

**No scrollbar, ever.** If eighteen rows and four headings do not fit your height, **page** the
list with the game's pager (`<` `>` and "1 / 2", as the Work tab and the roster do), and say in
your comment how many rows a page holds.

### The three touches around it

1. **The right-click menu.** A colonist is selected and the pointer is over the trader. The game's
   existing context menu (see the *Equip* screenshot) offers one row, **Trade**, with the coin
   icon. Draw it enabled, and draw it disabled with the reason "Downed" as the game already draws
   a disabled row.
2. **The trader's inspect pane**, when the trader is clicked. It uses the pane's header as a
   bandit's pane does, with a face, the name, and a state line "Trader · waiting by the fire". It
   has **no tab strip** and no Draft button. Its body is one short block: "Leaves in 14 h" and
   "Carrying 640 gold" *(both need keys)*. Draw it at the pane's exact size and dock.
3. **The arrival row** in the Events panel under the alerts: "Trader", with the briefcase icon and
   "A trader has come to us" (both registered), drawn as the panel draws its existing rows (the
   supply drop's row, attached).

### The states

Draw these, at 1920 × 1080, over the HUD with the board behind the scrim:

1. **Opened.** Every transfer at 0, the balance "Balance 0", and Confirm disabled with "Nothing to
   trade".
2. **A deal.** The colony sells 120 wood and 40 carrots (+160) and buys 3 medical supplies and one
   pistol (-294). The balance reads "You pay 134"; colony gold is 312 and trader gold 640. Confirm
   is enabled.
3. **The colony cannot pay.** State 2 with a second pistol (-210 more), so the balance is
   "You pay 344" against 312 of gold. Confirm is disabled with "Not enough gold", and the balance
   is in Bad.
4. **The trader cannot pay.** The colony sells all 240 wood, all 180 stone and both bats (+450)
   while buying nothing, and the trader's purse is only 300. The balance reads "You receive 450"
   in Bad, with "The trader cannot pay".
5. **The touches.** The board with the trader by the campfire, the right-click menu open over them
   (enabled row), the trader's inspect pane docked, and the Events panel carrying the arrival row.
   These are three things in one frame; show the disabled menu row as an inset.

### The design system, verbatim

Do not introduce any colour, face, size or spacing not listed here.

**Colour**

| Token | Value | Use |
|---|---|---|
| Panel fill | `#0c1014` at 100% | every panel and window |
| Panel border | white at 13% | 1 px, every panel |
| Divider | white at a lower alpha than the border | row rules |
| Scrim | as the settings window's | behind a modal |
| Text primary | `#eef3f6` | names, figures |
| Text meta | white at 66% | meta lines, prices |
| Text dim | white at 50% | headers, hints, a 0 |
| Accent | `#6fd3e3` | Confirm, a moving row's figure, focus |
| On accent | `#0b1116` | ink on an accent fill |
| Good / Warn / Bad | `#7fc98c` / `#e8b55c` / `#e06a5c` | a disabled reason is Warn; an impossible balance is Bad |

The category heading hues are the Inventory tab's, as the screenshot shows them. They were tuned
for colour-blind players, so take them from the screenshot exactly and do not invent your own.
Panels are flat: no gradients, no shadows, no glow, and no rounded corners beyond what `hud-v2.html`
uses.

**Type**: six steps and no seventh. Words are in **Archivo Narrow**. Every figure (quantities,
prices, gold, the balance) is in **IBM Plex Mono 500** with tabular figures.

| Step | Use |
|---|---|
| 11 / 600, tracked 0.14em, upper case | panel labels, column heads |
| 12 / 400 | meta lines |
| 13 / 400 | body text |
| 14 / 500 | row names and row figures |
| 19 / 600 | the trader's name in the title; the balance, in mono |
| 11 mono at 35% ink | a hotkey cap |

**Space**
- Panel padding 12 px with a 1 px border.
- Controls are 28 px square, as the bill list's are.
- A list row is between 28 and 36 px; choose one.
- The category heading is the Inventory tab's height.
- **Choose the window's width and height and state them.** The settings window is 1240 × 720 and
  the leave prompt 440 wide, for scale. The window must also fit a 1280 × 720 screen.
- **Every measurement becomes a constant in code.** In the game, a panel's width includes its
  padding and border.

**Icons** are pixel art at 32 px, point-filtered, drawn at 28 in a row. Use flat placeholder
squares and do not draw glyphs from a font.

**Marks** (the chevrons, the close X, a pager arrow) are drawn line art on a 24-unit box in the
style of Lucide, with a 1.5 px stroke. The HUD already has single and double chevrons, a tick, a
cross, a minus, a plus and a warning triangle. If you want any other mark, draw it as a single SVG
path and put the path in your comment.

**No characters outside ASCII anywhere**: the two shipped fonts draw nothing else, and an arrow,
a tick or a dingbat from a font renders as an empty box. Write "-120", not a Unicode minus.

### Deliverables

Static HTML, one file per state, at 1920 × 1080. Load the fonts from Google Fonts exactly as
`hud-v2.html` does, and declare the tokens once as CSS variables at the top of each file:

1. `trade-opened.html`
2. `trade-deal.html`
3. `trade-cannot-pay.html`
4. `trade-trader-short.html`
5. `trade-touches.html`

At the top of each file, in an HTML comment, list:
- every measurement you chose that the system above did not fix;
- the window's width and height;
- the rows per page, if you paged;
- the SVG path of any mark you drew.

Those become constants in code.

### What not to do

- No scrollbar and no tooltips (nothing has verified that they draw in the game).
- No tabs, filters or search box in the window.
- No goodwill, faction, haggle, gift, caravan or quality-price anything.
- No new colours, faces or sizes.
- No rounded, glowing, translucent or gradient panels.
- No renaming of any item.
- **No copying of RimWorld's trade dialog.** The shape is this game's Inventory tab and bill list,
  and the attachments show them.

---

## Attachments to send with the prompt

| File | Why |
|---|---|
| `docs/reference/mockups/hud-v2.html` (with `icon-map.js`) | the tokens and the base plate |
| `docs/reference/mockups/work-v1.html` | rows, columns, marks and the pager |
| a screenshot of the **Inventory** tab (F2) with a stocked colony | the category headings to reuse |
| a screenshot of a station's **bill list** | the 28 px controls and the row anatomy |
| a screenshot of the **settings window** open | the modal and its scrim |
| a screenshot of the right-click menu offering **Equip** | the menu to extend |
| a screenshot of a selected **bandit's** pane (debug menu → spawn a bandit) | the no-tabs header |
| a screenshot of the **Events panel** with a supply-drop row | the arrival row's shape |
