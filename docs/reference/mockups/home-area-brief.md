# Brief for Claude Design — the home area, its switch, and the Assign tab

**2026-09-25.** The prompt below goes to Claude Design verbatim, with the attachments listed at the
end. Decisions behind it: `docs/research/home-area-interview.md` (owner, 2026-09-24, every
recommendation taken) and `docs/design/43-home-area.md`. What comes back is built as units H3 (the
switch and the board look) and H4 (the tab) in `docs/plans/home-area.md`; its measurements become
constants in code (`HomeLook`, `AssignLayout`).

---

## The prompt

You are designing three small pieces for **Odyssey**, a colony-simulation prototype in the RimWorld
mould, set in a ruined sci-fi city and rendered in 3D with discrete vertical layers. The interface
is a dark, flat HUD over the 3D board, already built and shipped; you are adding to it and you must
match it exactly. Attached are the HUD's own mockup (`hud-v2.html`), the Work tab's mockup
(`work-v1.html`), which is the closest precedent for a table of rows, and screenshots of the running
game. **Everything you draw must look like it came from the same hand as those.**

### What the three pieces are for

A colony's **home** is the ground it has built on: every wall, door, floor, bed, shelf, generator,
power line, stockpile and field the player has placed, **plus five cells (12.5 m) around it**, as a
square. It is worked out by the game, never painted by the player, and it grows as the colony builds.
Each colonist can be told to keep to it: set to **Home**, she takes no work outside it and walks back
in when idle; set to **Anywhere**, she goes where the work is. The point is safety — keep the
vulnerable ones in while bandits or animals are about.

You are designing:

1. **The Home switch** — a button on the *views strip* that shows the home area on the board.
2. **The look of the home area on the board** while the switch is on.
3. **The Assign tab** — a table, one row per colonist, where each colonist's *Area* and *Response*
   are set.

### 1. The Home switch

The **views strip** is a short column of square toggle buttons in the right-hand gutter, directly
under the orders strip (attached screenshot). Today it holds one button, **Power**, whose glyph is a
drawn lightning-bolt-in-a-tile. **Home** goes directly under it, the same size and treatment.

- Draw the button **off** (fill: accent at 6%, border: accent at 30%) and **on** (fill: accent at
  30%, border: accent at 100%) — the two states Power already has.
- The glyph is a **simple house**: a pitched roof over a square body with a door, drawn as strokes
  and fills in the Power glyph's weight, legible at **17 px** inside the button and at **20 px** as a
  row icon in the Menu popover's *Overlays* list (where the same switch also appears, labelled
  "Home"). Deliver it as **one SVG path** (a `d` string) on a 24 x 24 viewBox, single colour, no
  text, no gradients, no strokes that go below 1.5 px at 17 px.
- The tooltip text is supplied by the game; do not write one.

### 2. The home area on the board

Draw the board look over a **screenshot of the running game** (attached: a small colony on the
meadow with a campfire, seen from the play camera at 48 degrees). The home area is a set of whole cells on the
ground, 2.5 m square each, and it can be several separate pieces. It must:

- read at a glance as "this is the base" at play zoom, **without hiding** grass, items, colonists or
  the marks the player has made (felling crosses, build plates, stockpile washes, which are already on
  the board in the colours in the attached screenshot);
- be distinguishable from a **stockpile**, which is already drawn as a pale wash with a 0.15 m edge
  strip in the stockpile's colour;
- work on **one layer at a time**: the board is sliced by height, and the player looks at one layer
  with the layers below drawn under it. Say how a lower layer's home should look beneath the active
  one — dimmer, or not shown at all.

Propose **one** look and say why, choosing between: an **edge only** (a line along the outer boundary
of home, on the ground), a **wash only** (every home cell tinted), or **both** (a faint wash and a
stronger edge). Use only colours from the tokens below, stated as a token plus an alpha. State the
edge's width in metres or as a fraction of a 2.5 m cell, and its height above the ground if it
stands proud of it.

### 2b. The hearth

Home is centred on one campfire, the **hearth**: the colony's home is only the part of what it
has built that is joined to it. Any campfire can be made the hearth; there is one at most.

- **On the board**, while the Home switch is on, draw a small **house mark** floating over the hearth
  campfire, in the same colour as the home look. With the switch off it is an ordinary campfire and
  carries no mark. Use the same house glyph as the switch, simplified if it must be.
- **On the campfire's inspect pane** (the fixed pane at the bottom-left), draw two states: a campfire
  that **is** the hearth, which says *Hearth* as a meta line under its name with the house glyph
  beside it; and one that is not, which carries a single button, **Make this the hearth**, in the
  style of the colonist pane's command buttons. Nothing else on the pane changes.
- **No hearth yet** replaces "No home yet" as the Area column's note below.

### 3. The Assign tab

The **Assign** tab is opened by **F4** or the *Assign* item on the command bar. It lists every
colonist and shows, per colonist, two settings the player changes by clicking:

| Column | Values | Set in |
|---|---|---|
| Colonist | the colonist's name, with the small square portrait the roster uses | Archivo Narrow, 14/500 |
| Area | **Anywhere** or **Home** | Archivo Narrow, 14/500 |
| Response | **Fight back**, **Defend** or **Flee** | Archivo Narrow, 14/500 |

- **A click on an Area or Response cell moves it to the next value**, round the list. Draw the cells
  so they read as a setting you can change, not as plain text and not as a dropdown. Draw one Area
  cell in its **hover** state.
- **Home** and **Flee** are the two cautious values; say whether they should look different from
  their neighbours (for example the warn colour) or the same, and why.
- **When the colony has no hearth yet** (no campfire built), *Home* restricts nobody. The Area column
  header then carries a short meta note, **"No hearth yet"**. Draw it in one state.
- Column headers are **not** sortable in this version. Clicking a colonist's name **selects that
  colonist** (the game paints a selected row in the accent colour with dark ink on it); show one row
  selected.
- **Twelve rows a page**, a pager ("1 / 2", chevrons) at the foot, shown only when there is more than
  one page. **Never a scrollbar** — a hard rule of this HUD.
- Design the table so a third setting column (a named area, a food policy) could be added to the
  right later without moving what is there, but **do not draw one, not even greyed**.
- The names to use, in this order: **Trent, Benjamin, Lyra, Charles, Orson, Evelyn, Eleanor, Logan,
  Gareth, Christian, Maya, Nyx, Naomi, Liam**. Ten for the one-page states, all fourteen for the paged
  state. Mix the values: about a third at Home, two at Defend, one at Flee.

### The design system, verbatim

Do not introduce any colour, face, size or spacing not listed here.

**Colour**

| Token | Value | Use |
|---|---|---|
| Panel fill | `#0c1014` at 100% | every panel and window |
| Bar fill | `#0c1014` at 90% | the command bar |
| Panel border | white at 13% | 1 px, every panel |
| Divider | white at a lower alpha than the border | row rules |
| Text primary | `#eef3f6` | names, row text |
| Text meta | white at 66% | meta lines, secondary values |
| Text dim | white at 50% | headers, hints |
| Accent | `#6fd3e3` | the selected row, the active tab, a switch that is on |
| On accent | `#0b1116` | ink on an accent fill |
| Warn / Bad / Good / Info | `#e8b55c` / `#e06a5c` / `#7fc98c` / `#8fd0e3` | only where this brief invites one |

Contrast comes from the scrim over the 3D board, not from the panels: panels are flat, no
gradients, no shadows, no glow, no rounded corners beyond what `hud-v2.html` uses.

**Type** — six steps and no seventh. Words in **Archivo Narrow**; every figure (page numbers, hotkey
caps) in **IBM Plex Mono 500** with tabular figures.

| Step | Use |
|---|---|
| 11 / 600, tracked 0.14em, upper case | panel labels and column headers: "COLONIST", "AREA", "RESPONSE" |
| 12 / 400 | meta lines, "No hearth yet" |
| 13 / 400 | body text |
| 14 / 500 | list rows, command-bar labels |
| 19 / 600 | the name of the selected thing |
| 11 mono at 35% ink | a hotkey cap |

**Space** — rows are 30 px; panel padding 12 px; a 1 px border; gaps of 9 px. The Work tab's table
is 1,385 px wide with a 192 px frozen name column: follow its proportions, but this table needs far
less width. Pick a constant width that fits three columns comfortably and state it. **The width is
constant** whatever the colony holds.

**Docking** — the tab is a window docked **bottom-left**, flush with the left screen edge, sitting on
top of the command bar. It has the standard window header: the title *Assign* at the panel label step
and a close X at the right. Escape closes it.

**The command bar**, in this order with these caps: Build B · Work F1 · Inventory F2 · Research F3 ·
**Assign F4** · Animals F5 · Bills F7 · Factions F8 · Almanac F9 · Menu Esc. Draw Assign as the
active item (accent fill at 12%) where the tab is open; Bills and Factions are dead items, drawn
dimmed as `hud-v2.html` draws them.

**Icons** — the game's icons are pixel art at 32 px; where a portrait or an icon goes in the table,
use a flat placeholder square. The one icon you draw is the house glyph, as an SVG path. **No
characters outside ASCII anywhere**: the two shipped fonts draw nothing else, and a tick, a cross, an
arrow or a dingbat would render as an empty box. Chevrons are drawn, not typed.

### Deliverables

Static HTML, one file per state, 1920 x 1080, with the fonts loaded from Google Fonts exactly as
`hud-v2.html` does and the tokens declared once as CSS variables at the top of each file, so a diff
against the shipped tokens is possible:

1. `home-view-off.html` — the board screenshot with the HUD, the views strip showing Power and Home,
   both off.
2. `home-view-on.html` — the same with Home on, the home area drawn on the board in your look and
   the house mark over the hearth; show one small outpost outside home, not marked.
3. `home-view-layer.html` — Home on, the player looking at a layer above the base, so the lower
   layer's home shows (or does not) beneath it.
4. `assign-open.html` — the Assign tab open with ten colonists, one row selected, one Area cell in
   its hover state, no pager.
5. `assign-paged.html` — fourteen colonists, the pager showing "1 / 2".
6. `assign-nohearth.html` — the tab with the "No hearth yet" note in the Area header.
7. `home-glyph.svg` — the house, one path, 24 x 24 viewBox.
8. `hearth-pane.html` — the campfire's inspect pane twice, side by side: the hearth, and a campfire
   with the *Make this the hearth* button.

At the top of each HTML file, in an HTML comment, list every measurement you chose that the system
above did not fix — the table width, column widths, the Area and Response cell padding, the edge's
width and alpha, the wash's alpha — because those numbers become constants in code.

### What not to do

No painting tool, no brush, no area names, no area picker: home is worked out by the game from the
hearth, and the only choices are Anywhere or Home, and which campfire is the hearth. No new colours, faces or sizes. No scrollbars. No rounded, glowing,
translucent or gradient panels. No other colonist columns (skills, needs, mood, schedule). Do not
rename *Home*, *Anywhere*, *Fight back*, *Defend*, *Flee* or *Assign*. No copying of RimWorld's
layout: the table's shape is this game's Work tab, and the attachments show it.

---

## Attachments to send with the prompt

| File | Why |
|---|---|
| `docs/reference/mockups/hud-v2.html` (and `icon-map.js` beside it) | the HUD's own mockup: the base plate, the bar, the tokens |
| `docs/reference/mockups/work-v1.html` | the Work tab: the table precedent |
| a screenshot of the right-hand gutter with **Power on** in the views strip | the strip and the button the Home switch sits under; it postdates `hud-v2.html` |
| a screenshot of **a small colony on the meadow** at play zoom: a hut, a campfire, a stockpile, a field, a marked tree | the board the look is drawn over, with the marks and the stockpile wash it must not be confused with, and the fire the hearth mark sits over |
| a screenshot with the **Work tab** open (F1) | the docking and the table in the real HUD |
| a screenshot of a **colonist's pane with the Response button** | the setting the Response column mirrors |

The four screenshots of the running game are the owner's to take; nothing in the repository holds
them yet.

## Answers recorded

Owner, 2026-09-24: the home is derived; five cells, square; placed things and sites, not marks; per
layer with one layer of margin; an Assign tab on F4 with Area and Response; no work outside and the
draft overrides it; Claude Design draws all three pieces; Flee towards home later.

## Built

Nothing yet. When the files come back they go in this folder beside this brief, and design 43 §5d
and §6 are amended with the look and the constants chosen.
