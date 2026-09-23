# Brief for Claude Design — the Animals tab

**2026-09-23.** The prompt below is handed to Claude Design verbatim, with the attachments listed
at the end. Decisions behind it: `docs/research/animals-tab-interview.md` (owner: one tab called
*Animals*, wildlife and, later, tamed animals under it; the tamed half deferred entirely; the
Work tab's table shape; the inspect pane included; static HTML back). The panel it replaces is
the Wildlife panel as built on F6 (`docs/design/30-wildlife.md` §6), which moves to F5 and takes
the name *Animals* as a follow-up code unit once the design is agreed.

---

## The prompt

You are designing one panel for **Odyssey**, a colony-simulation prototype in the RimWorld mould
set in a ruined sci-fi city, rendered in 3D with discrete vertical layers. The interface is a
dark, flat HUD over the 3D board, already built and shipped; you are adding one tab to it and
you must match it exactly. Attached are the HUD's own mockup (`hud-v2.html`), the Work tab's
mockup (`work-v1.html`) which is the closest precedent for a table of rows, and screenshots of
the running game. **Everything you draw must look like it came from the same hand as those.**

### What the tab is

The **Animals** tab, opened by **F5** or by the *Animals* item on the command bar. It lists
every animal on the board — for now all of them are **wild**; there is no taming, no training,
no health and no ownership in the game yet, and you must not draw controls for those. Design the
table so that columns for a tamed animal (master, area, training, slaughter) could be added to
the right later without moving what is there, but do not draw them, not even greyed.

The tab replaces a narrower panel that is already built and is attached as a screenshot. Keep
its content and improve its form: it is currently three columns in a small card, and it should
be a proper table in the Work tab's shape.

### Content

The animals in the game today, with the names the game uses (never rename them):

| Kind | What it is | Where it lives |
|---|---|---|
| **Midden hog** | Pig-descended, thrives on refuse heaps. Wild for now; the reliable meat animal and "the one you meet first". Ambles in family groups of three to five | woodland, clearings |
| **Duct rat** | The rat of the ruin: lives in the ducts and the caverns, climbs anything, out at night. Alone | beside rock, rubble, cavern mouths |

Three more are named but not in the game and must not appear: the girder cat, the loper, the
dray hog.

Each row is one animal and shows, in this order:

| Column | Content | Set in |
|---|---|---|
| Portrait | a small square avatar of the kind (draw a placeholder tile; the game's own icons are pixel art at 32 px) | — |
| Kind | "Midden hog" or "Duct rat" | Archivo Narrow, 14/500 |
| Doing | "Wandering" or "Resting" — the only two states a wild animal has | Archivo Narrow, 14/500 |
| Layer | the vertical layer it stands on, e.g. "L12" | IBM Plex Mono, 14/500 |
| Away | how many cells it is from the colony, e.g. "38" | IBM Plex Mono, 14/500 |

Above the rows, a **count strip**: one entry per kind with its count, e.g. "Midden hog 6 ·
Duct rat 4". Rows are sorted by kind and then by distance, nearest first. Column headers are
clickable to sort (show one header in its sorted state with a small indicator). A click on a row
**selects that animal and jumps the camera to it**; show one row selected — the game paints a
selected row in the accent colour with dark ink on it.

The board carries at most 24 animals; typical is nine or ten. The table shows **twelve rows a
page** with a pager ("1 / 2", chevrons) at the foot, and **never a scrollbar** — this is a hard
rule of this HUD. Show the pager only when there is more than one page.

The **empty state** (no animals on the board) is one quiet line of meta text in the table
body; write it as "No animals on the board" and mark it in a note as needing a registry key.

### The inspect pane for an animal

When an animal is selected, the fixed **inspect pane** at the bottom-left of the screen shows
it, exactly as it shows a colonist but with less: a header with the kind's name at 19/600 and a
meta line "animal · L12 · 78, 59" (kind, layer, coordinates), then the line "Wandering" or
"Resting" with its icon, and **no tabs, no needs, no skills, no commands** — a wild animal has
none. Draw this pane in the state where the Animals tab is also open, so the two can be seen
together, and make sure they do not overlap: the tab docks over the command bar in the same
bottom-left corner and the game closes whichever of them was open when the other opens, so
draw the pane **alone** in one state and the tab **alone** in another, never both.

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
| Text meta | white at 66% | meta lines, numbers in secondary columns |
| Text dim | white at 50% | headers, hints |
| Accent | `#6fd3e3` | the selected row, the active tab, the sort indicator |
| On accent | `#0b1116` | ink on an accent fill |
| Warn / Bad / Good / Info | `#e8b55c` / `#e06a5c` / `#7fc98c` / `#8fd0e3` | not needed here; listed so you do not invent them |

Contrast comes from the scrim over the 3D board, not from the panels: panels are flat, no
gradients, no shadows, no glow, no rounded corners beyond what `hud-v2.html` uses.

**Type** — six steps and no seventh. Words in **Archivo Narrow**; every figure (counts, layers,
coordinates, page numbers, hotkey caps) in **IBM Plex Mono 500** with tabular figures.

| Step | Use |
|---|---|
| 11 / 600, tracked 0.14em, upper case | panel labels and column headers: "KIND", "DOING" |
| 12 / 400 | meta lines |
| 13 / 400 | body text |
| 14 / 500 | list rows, command-bar labels |
| 19 / 600 | the name of the selected thing |
| 11 mono at 35% ink | a hotkey cap |

**Space** — rows are 30 px; panel padding 12 px; a 1 px border; gaps of 9 px. The Work tab's
table is 1,385 px wide with a 192 px frozen name column and 34 px column pitch: follow its
proportions, but this table needs far less width — pick a constant width that fits the five
columns comfortably (around 560 px) and state it. **The width is constant** whatever the
board carries; nothing in the panel may make it otherwise.

**Docking** — the tab is a window docked **bottom-left**, flush with the left screen edge,
sitting on top of the command bar (the bar is 11 items across the foot of the screen; the tab
sits immediately above it). It has the standard window header: the title *Animals* at the panel
label step and a close X at the right. Escape closes it.

**The command bar** — eleven items in this order with these caps: Build B · Work F1 · Research
F3 · Colonists F4 · **Animals F5** · Wildlife F6 · Bills F7 · Factions F8 · Almanac F9 · Menu
Esc. Draw Animals as the active item (accent fill at 12%); Research, Colonists, Wildlife, Bills
and Factions are dead items and are drawn dimmed as `hud-v2.html` draws them.

**Icons** — pixel art, 32 px, point-filtered; use flat placeholder squares where an icon goes
and do not draw glyphs from a font. **No characters outside ASCII anywhere**: the two shipped
fonts draw nothing else, and a tick, a cross or a dingbat would render as an empty box.

### Deliverables

Static HTML, one file per state, 1920 × 1080, with the fonts loaded from Google Fonts exactly
as `hud-v2.html` does and the tokens declared once as CSS variables at the top of each file, so
a diff against the shipped tokens is possible:

1. `animals-closed.html` — the HUD with the board behind it (a flat dark placeholder is fine)
   and the command bar, Animals **not** active.
2. `animals-open.html` — the tab open with ten animals (six hogs, four rats) across a page,
   one column sorted, no row selected, no pager.
3. `animals-open-paged.html` — the same with fourteen animals, so the pager shows "1 / 2".
4. `animals-selected.html` — the tab open with one row selected in the accent colour.
5. `animals-inspect.html` — the tab closed and the inspect pane showing the selected animal.
6. `animals-empty.html` — the tab open on an empty board.

Below each file, in an HTML comment at the top, list every measurement you chose that the
system above did not fix (the table width, column widths, the count strip's spacing), because
those numbers become constants in code.

### What not to do

No taming, training, health, needs, owners, areas, slaughter, hunting or sex and age — none of
it exists. No new colours, faces or sizes. No scrollbars. No rounded, glowing, translucent or
gradient panels. No renaming of the two animals or their two states. No copying of RimWorld's
layout: the shape is this game's Work tab, and the attachments show it.

---

## Attachments to send with the prompt

| File | Why |
|---|---|
| `docs/reference/mockups/hud-v2.html` (and `icon-map.js` beside it) | the HUD's own mockup: the base plate, the bar, the tokens |
| `docs/reference/mockups/work-v1.html` | the Work tab: the table precedent |
| a screenshot of the running game with the **Wildlife panel** open (F6) | the panel being replaced, as it really draws |
| a screenshot with the **Work tab** open (F1) | the docking and the table in the real HUD |
| a screenshot with a **hog selected** and the inspect pane showing it | the pane as it really draws |

## Answers recorded

Owner, 2026-09-23: *"Lets have animals/wildlife under one tab - for now - just call it animals.
Defer tamed animals for now"* (questions 2–5); the Work tab's shape (6) and the inspect pane
(7): yes; the recommendations for inputs (8) and deliverables (9).

## Built

The brief came back from Claude Design as a specification (owner, 2026-09-23: "remove the
wildlife tab and make this animal tab and hook up what is necessary") and was built the same
day: `AnimalsDirector`, `AnimalsModel`, `AnimalsLayout`, `HudShell.Animals.cs`; F5 in the
binding map; Wildlife on F6 a dead item; design 30 §6 rewritten to it.
