# Brief for Claude Design — prisoners: the cell, the prisoner's pane, and the ways in

**2026-09-26.** The prompt below goes to Claude Design verbatim, with the attachments listed at the
end. The decisions behind it are the owner's (`docs/research/prisoner-interview.md`) and
`docs/design/58-prisoners.md`. §15 of that design is what is built now. The rules underneath are
**built and tested**. The interface is a first pass: rows on the pane and a toggle row on the bed,
made to prove the rules rather than to be looked at. What comes back replaces that first pass, and
its measurements become constants in code (`InspectModel`, `HudShell.Inspect`).

---

## The prompt

You are designing the interface for **prisoners** in **Odyssey**, a colony-simulation prototype in
the RimWorld mould, set in a ruined sci-fi city and rendered in 3D with discrete vertical layers.
The interface is a dark, flat HUD over the 3D board. It is already built and shipped; you are adding
to it, and you must match it exactly. Attached are the HUD's own mockup (`hud-v2.html`), the house
glyph drawn for the last brief (`home-glyph.svg`), which shows the weight a drawn glyph must have,
and screenshots of the running game. **Everything you draw must look like it came from the same
hand as those.**

### What prisoners are, in one paragraph

The player marks a **bed for prisoners**. If that bed stands in a walled, roofed room, the room
becomes a **cell**, and every bed in it is a prison bed. If it stands in the open, whoever sleeps
there is **shackled** to it. People come in four ways:

- **captured** — a downed enemy is carried in by a *warden*, a colonist with Warden work switched on;
- **surrendered** — a badly hurt raider gives up and walks in herself;
- **arrested** — one of the colony's own is taken;
- **the debug menu.**

A prisoner cannot open a door. She walks her cell by day and sleeps in her prison bed. The player
sets what the colony means to do with her, her **mode**:

- **Hold** — keep her, fed and tended.
- **Recruit** — a warden talks to her every six hours until she is willing to join.
- **Release** — let her go.
- **Exile** — send her away for good.
- **Ransom** — reserved; it needs factions, which the game does not have yet.

Recruiting fills a **willingness bar**. It is not a hidden roll: the game knows exactly how many
hours are left, and exactly what is slowing it. A prisoner also has a visible **escape risk**, a
percentage a day, with its reasons. When she breaks out she bashes the cell door with her fists and
runs for the edge of the map, and once she is brought down she is a prisoner again. **The
improvement over the reference is that every number is shown, with its reasons.** Design for that.

### What you are designing

1. **The bed's pane** — marking a bed for prisoners, and the three states it can be in.
2. **The prisoner's pane** — her mode, her willingness and when she will join, what is slowing it,
   her escape risk and why, and the states she passes through.
3. **The right-click menu on a downed enemy** — *Capture* or *Finish off*.
4. **The Arrest command** on a colonist's pane.
5. **The cell on the board** — how a cell reads when you are looking at one.
6. **The prison jumpsuit colour.**

### 1. The bed's pane

Selecting a bed opens the **inspect pane**. The pane is fixed at the bottom-left, 560 px wide, and
is described below. For a bed it holds a column of *tile rows*, each a label and a value: quality,
owner, material, temperature. The owner row is already a control: it opens a picker of who may own
the bed.

Draw a control that says what the bed is **for**, and switches it. There are three states:

| State | What it means |
|---|---|
| **Colony** | an ordinary bed |
| **Prisoners, in a cell** | the room is a cell; every bed in it is for prisoners |
| **Prisoners, shackled** | a prison bed with no room around it; whoever sleeps there is shackled to it |

Marking one bed changes every bed in its room, and the control must say so without a sentence of
explanation: the player should see that the room, not the bed, became a cell. Show all three states.
In the prisoners states, the **owner picker lists prisoners, not colonists**; show the picker open
in that state with two prisoners and *Nobody*.

### 2. The prisoner's pane

A prisoner's pane is the same 560 px pane. Its header is a 60 px portrait, her **name** at the 19
step, the word **Prisoner** as a meta line under it, and what she is doing (*Wandering*, *Sleeping*,
*Eating*). It has **no** colonist body: no needs, no skills, no tab strip, no Draft button. Below the
header, design:

- **The mode.** The five modes above, one chosen. **Ransom** is drawn but cannot be chosen, with the
  reason *needs factions*. Choosing is one click. Today the mode is a single row that cycles when it
  is pressed; propose something better and say why (chips, or a segmented control).
- **Willingness**, only in Recruit mode. It runs from 0 to 100 %. Show:
  - the time left: *joins in 2 days 6 h*, or *nobody to talk to her* when no colonist has Warden
    work;
  - **what is slowing it**, from this closed list: *no warden*, *hungry*, *untended*, *shackled*,
    *low mood*, *low Social* (the best warden's Social skill is low).
  
  The player reads this to decide what to fix, so a slowing reason must look like something to act
  on, not like decoration. When nothing slows her, say nothing.
- **Escape risk**, in every mode: a percentage a day to one decimal place (*2.1% a day*), with its
  **reasons**. Some reasons raise it and some lower it, and the reader must tell which at a glance:

  | Raises it | Lowers it |
  |---|---|
  | *miserable* ×3 | *content* ×0.5 |
  | *unhappy* ×1.5 | *hurt* ×0.5 |
  | *door open* ×2 | *well kept* ×0.7 |
  | *no walls* (shackled) ×2 | |
  | *unwatched* (no colonist within ten cells) ×1.5 | |
  | *unhurt* ×1.5 | |

  Show the multipliers or not, as you judge. The risk is divided by the square root of how many
  prisoners are held; decide whether that belongs on the pane. At 5 % a day or more the figure is in
  the warn colour.
- **Shackled**, when her bed is a shackle bed.

Draw the pane in these states:

| # | State |
|---|---|
| a | **Hold**, in a cell, content and well kept: low risk, nothing else |
| b | **Recruit**, 37 %, *joins in 2 days 6 h*, slowed by *hungry* and *low mood*, risk *6.3% a day* in warn |
| c | **Recruit** with nobody to talk to her: *no warden* |
| d | **Shackled**, Hold |
| e | **Breaking out**: nothing can be set; one line says she is escaping, in the bad colour |
| f | **Let go** (Release or Exile chosen, and a warden has opened the door): walking to the edge; one line says so |

**The pane grows upward from the bottom.** It is anchored to the bottom-left and a taller pane
covers more of the board, so keep every state to the height of the tallest (b) and say what that
height is. A row must never jump when a value changes: *joins in* moving from hours to days, or a
second slowing reason appearing, must not push the rows below it down.

### 3. The right-click menu on a downed enemy

A right-click on a downed enemy used to kill her with no question asked. It now opens the game's
**context menu**, a small popover at the pointer with one row per choice and *Cancel* last. The
attached screenshot shows it with *Equip* on a weapon. The rows are:

- **Capture** — the first colonist in the selection carries her to a prison bed.
- **Finish off** — the drafted colonists in the selection kill her where she lies. It is dimmed,
  with the reason *Draft someone first*, when nobody selected is drafted.
- **Cancel.**

Draw the menu twice: with a drafted colonist selected, and with an undrafted one. Finish off is the
one irreversible row in the game's menus; say whether it should look different, and how, within the
tokens below. Draw a third, one-row state: **a downed prisoner outside her bed** is offered
*Capture* alone.

### 4. The Arrest command

A colonist's pane has a row of **command buttons** under its header. Today they are *Inspect*,
*Prioritise*, *Draft* and the colonist's *response to danger*, and *Arrest* has just been added at
the end. Pressing Arrest sends the nearest colonist to take her. She may resist, and every colonist's
mood drops.

- Draw the button row with Arrest in it.
- Arrest is not an everyday command; say whether it should sit apart from the others.
- Draw a disabled state with the reason *no free prison bed*.
- **It stays a button on her pane.** A right-click on a colonist is a move order in this game and
  must stay one.

### 5. The cell on the board

Draw over the attached screenshot of a small colony with a cell built in it: a walled, roofed room,
a door and a bed. **While a prison bed or a prisoner is selected**, the cell should read as a cell.
Propose one look:

- an **edge** round the room's floor;
- a **wash** over it; or
- **a mark over the door**, since the door is what holds a prisoner in and is what an escapee
  attacks.

Say why. The look must be distinguishable from a **stockpile** (a pale wash with a thin edge in the
stockpile's colour) and from the **home area** (an accent edge, attached). A shackle bed in the open
has no room; show how it reads.

### 6. The prison jumpsuit

A prisoner wears a jumpsuit over her own face and hair. A colonist wears the issued suit, `#E8EDF6`
with `#A8B2C2` trim; a bandit wears a red vest and black trousers. The first build uses `#D9772E`
with `#7A4524` trim.

Show the three outfits as flat swatches side by side at the size a figure is seen at play zoom
(about 40 px tall), and recommend a prisoner colour. It must be:

- unmistakable against both of the others;
- readable against grass (`#5d7a3a` to `#8aa05a`) and against dusk;
- distinguishable by a player with deuteranopia from the bandit's red.

State the two hex values.

### The design system, verbatim

Do not introduce any colour, face, size or spacing not listed here.

**Colour**

| Token | Value | Use |
|---|---|---|
| Panel fill | `#0c1014` at 100% | every panel and window |
| Panel border | white at 13% | 1 px, every panel |
| Divider | white at a lower alpha than the border | row rules |
| Text primary | `#eef3f6` | names, values |
| Text meta | white at 66% | meta lines, secondary values |
| Text dim | white at 50% | labels, hints |
| Accent | `#6fd3e3` | a chosen control, the selection |
| On accent | `#0b1116` | ink on an accent fill |
| Warn / Bad / Good / Info | `#e8b55c` / `#e06a5c` / `#7fc98c` / `#8fd0e3` | only where this brief invites one |

Contrast comes from the scrim over the 3D board, not from the panels: panels are flat, with no
gradients, no shadows, no glow and no rounded corners beyond what `hud-v2.html` uses.

**Type.** There are six steps and no seventh. Words are set in **Archivo Narrow**. Every figure
(percentages, hours, a hotkey cap) is set in **IBM Plex Mono 500** with tabular figures.

| Step | Use |
|---|---|
| 11 / 600, tracked 0.14em, upper case | panel labels and section headings: "MODE", "ESCAPE RISK" |
| 12 / 400 | meta lines, reasons |
| 13 / 400 | body text |
| 14 / 500 | row values, menu rows, button labels |
| 19 / 600 | the name of the selected thing |
| 11 mono at 35% ink | a hotkey cap |

**Space.** Tile rows are 19 px, list rows 30 px, panel padding 12 px, a 1 px border, and gaps of
9 px. A bar (the skills' experience bar is the precedent) is 6 px tall with 8 px either side, in the
good colour.

**Icons.** The game's icons are pixel art at 32 px; where one goes, use a flat placeholder square.
Any glyph you draw (a shackle, a cell, a door) is **one SVG path** on a 24 × 24 viewBox, in one
colour, legible at 17 px. **Use no characters outside ASCII anywhere**: the two shipped fonts draw
nothing else, so a tick, a cross, a chain, an arrow or a lock character would render as an empty
box. Draw chevrons and bars; do not type them.

**Names.** Use these, as the game's own name pool deals them. Prisoners: **Orson, Maya, Gareth, Nyx**.
Colonists: **Trent, Lyra, Evelyn, Logan**.

**Words fixed by the game.** Do not rename these: *Prisoner*, *Hold*, *Recruit*, *Release*, *Exile*,
*Ransom*, *Capture*, *Finish off*, *Arrest*, *Draft someone first*, *Cancel*, and the reason words
listed above. You may propose wording for labels this brief does not fix (a section heading, *joins
in*). List each proposal, because every name in this game lives in one table, which the owner
corrects.

### Deliverables

Deliver static HTML, one file per state, at 1920 × 1080. Load the fonts from Google Fonts exactly as
`hud-v2.html` does, and declare the tokens once as CSS variables at the top of each file, so a diff
against the shipped tokens is possible.

1. `bed-colony.html`, `bed-cell.html`, `bed-shackled.html` — the bed's pane in its three states;
   `bed-picker.html` — the owner picker open on a prison bed.
2. `prisoner-a.html` to `prisoner-f.html` — the prisoner's pane in the six states of §2.
3. `menu-drafted.html`, `menu-undrafted.html`, `menu-prisoner.html` — the right-click menu.
4. `arrest.html` — a colonist's command row with Arrest, enabled and disabled.
5. `cell-on-board.html` — the board with a cell and a shackle bed in your look, with a prison bed
   selected.
6. `jumpsuit.html` — the three outfits as swatches, your recommendation marked.
7. Any glyph you drew, as its own `.svg`.

At the top of each HTML file, in an HTML comment, list every measurement you chose that the system
above did not fix, because those numbers become constants in code:

- the pane's height in state (b);
- the mode control's segment widths;
- the bar's length;
- the reason chips' padding;
- the cell look's width and alpha.

### What not to do

- No new colours, faces or sizes.
- No scrollbars. No rounded, glowing, translucent or gradient panels.
- No tab strip on the prisoner's pane.
- **No hidden numbers**: if the game knows it, the pane may show it, and a chance is never a mystery.
- No prison-labour, execution, ransom-negotiation or faction screens. Those are later units; design
  so they could be added, but do not draw them, not even greyed.
- No copying of RimWorld's prisoner tab. This pane's shape is this game's inspect pane, and the
  attachments show it.

---

## Attachments to send with the prompt

| File | Why |
|---|---|
| `docs/reference/mockups/hud-v2.html` (and `icon-map.js` beside it) | the HUD's own mockup: the base plate, the bar, the tokens |
| `docs/reference/mockups/home-glyph.svg` | the weight a drawn glyph must have |
| a screenshot of **a bed selected**, its pane showing the owner row | the tile rows the purpose control joins |
| a screenshot of **a bandit selected** | the bare pane a prisoner's is built on |
| a screenshot of **a colonist selected** | the command row Arrest joins |
| a screenshot of **the right-click menu on a weapon** (*Equip*) | the context menu's look |
| a screenshot of **a small colony with a walled, roofed room and a bed**, and a stockpile in view, with the **Home** view on | the board the cell look is drawn over, and the two looks it must not be confused with |
| a screenshot of **a colonist and a bandit side by side** at play zoom | the two outfits the jumpsuit must stand apart from |

The screenshots of the running game are the owner's to take; nothing in the repository holds them
yet. The branch that has prisoners in it is `claude/prisoner-bed-assignment-98afc0`; the debug
menu's Spawn tab has *Imprison nearest* for a prisoner to photograph.
