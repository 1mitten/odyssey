# Brief for Claude Design — gear: the Gear tab, loadouts, and the gear orders

**2026-09-25.** The prompt below goes to Claude Design verbatim, with the attachments listed at the
end. Decisions behind it: `docs/research/gear-interview.md` (owner, 2026-09-25, twenty-two answers
in six rounds). It designs the **end state once**; the build is staged (`docs/plans/gear.md`), and
the first unit, G1, builds only the weapon half of the Gear tab and draws every other slot empty
with its reason. What comes back becomes constants in code (`GearLayout`, and the Assign tab's
`AssignDirector.TabWidth` and column widths).

---

## The prompt

You are designing the **gear** screens for **Odyssey**, a colony-simulation prototype in the
RimWorld mould, set in a ruined sci-fi city and rendered in 3D with discrete vertical layers. The
interface is a dark, flat HUD over the 3D board, already built and shipped; you are adding to it
and you must match it exactly. Attached are the HUD's own mockup (`hud-v2.html`), the Work tab's
mockup (`work-v1.html`), and screenshots of the running game, including the colonist's inspect pane
and the Assign tab. **Everything you draw must look like it came from the same hand as those.**

### What gear is

Every colonist holds things in four places, and each place has its own rule:

- **In the hand: one weapon.** A bat, a crowbar, a machete or an arc blade, or the bare hands. It
  is carried **at the hip** and **drawn** when fighting.
- **Worn: five slots.** **Head** (a cap, a hood, a helmet), **Face** (glasses, a mask, a gas mask),
  **Body** (one garment: work clothes, a coat, a rain coat), **Armour** (a vest worn over the
  garment) and **Back** (a pack). A garment replaces the whole outfit, so there is exactly one; with
  nothing in the Body slot a colonist wears the colony's **issued jumpsuit**, which is not an item
  and cannot be taken off. Nobody is ever naked.
- **Kit: small things kept on the colonist.** Two **belt** slots always, and **four more** while a
  pack is worn on the back. A slot holds one kind of thing, a small stack of it: up to 5 medical
  supplies, 3 rations, 1 flashlight. Big things never go in the kit.
- **In the arms: the load being hauled.** Logs, stone, ore, scrap, a pile of meals. This is **not
  gear** and is not on the Gear tab; it appears on the pane's activity line, as it does today.

What a colonist wears **does something**, and the player needs to see the sum at a glance:

| Effect | Comes from | Read as |
|---|---|---|
| Armour | helmet, vest, some garments | a percentage of a blow turned aside |
| Warmth | garment, headgear | the range of temperatures the colonist is comfortable in, e.g. "4 to 26 C" (the bare jumpsuit is 16 to 26 C) |
| Rain | rain coat, hood | how much of the rain's slowdown it buys back |
| Look | the garment's quality | a small mood lift or none |

Every worn item and weapon has a **quality**: Poor, Normal, Decent, Uber, Epic. There is **no
wear or durability**: nothing tears, nothing has a condition bar.

A **loadout** is a named rule a colonist follows: what to wear and what to keep in the kit (for
example *Doctor*: 4 medical supplies, 1 ration; *Winter*: a coat and a cap). A colonist with a
loadout fetches what it asks for from the stores on their own; a colonist with **none** keeps
whatever they have and changes nothing by themselves.

You are designing three pieces:

1. **The Gear tab** on the colonist's inspect pane.
2. **The Loadout column** on the Assign tab, and **the loadout editor** it opens.
3. **The gear rows** in the right-click menu on the board.

### 1. The Gear tab

The **inspect pane** is the fixed panel at the **bottom-left**, **560 px wide**, docked on top of
the command bar; it **grows upward** from the bar. Its header is the portrait (a square), the name
at 19/600 and two meta lines; under it a **tab strip** (26 px high): *Needs, Skills, Gear,
Thoughts, Social, Health, Log*. Gear sits third and is dimmed today. Under the strip is the **tab
body**, which is **one fixed height for every tab** so that switching tabs never moves the header
under the pointer. Today that height is **157 px** (the Skills tab: seven rows of 19 with 4 px
gaps). **The Gear tab may need more: say how tall its body must be, and design the tab to that
height**; it becomes the body height for every tab, so spend it carefully.

Draw the tab as a **paper doll**:

- **The figure**: the colonist's portrait or a silhouette in the middle of the body. Use a flat
  placeholder of the portrait tile; do not draw a person.
- **Six slot tiles around it**: *Head*, *Face*, *Body*, *Armour*, *Back* placed where they sit on a
  body, and *Weapon* at the hand. A tile holds the item's **32 px icon** (a flat placeholder
  square), and beside or under it the item's name at 14/500 and one dimmed meta word: the quality,
  in its quality colour, and for the weapon **At the hip** or **Drawn**.
- **An empty tile** says what it lacks in a dim word ("Nothing worn", "Bare hands"); the empty
  **Body** tile says **Issued jumpsuit**, as a real entry, because that is what is worn.
- **The kit strip**: six small tiles in a row. The first two are the belt and always open; the other
  four are **locked** until a pack is worn and say so once ("Wear a pack for 4 more"). A filled kit
  tile shows its icon and a count in IBM Plex Mono ("4"); an empty one is an outlined square.
- **The effects line**: one line at the bottom of the body, in meta type, e.g.
  `Armour 24%   Warmth 4 to 26 C   Rain 50%   Kit 3 of 6`. A value that is the bare default
  may be omitted or dimmed; say which and why. Separate the values with space or a drawn rule, not
  a middle dot (see the icon rules below).
- **Actions on a slot.** Clicking a filled tile opens a small popover beside it with the item's
  name, quality, its effects in one or two meta lines and **one or two buttons**: *Remove* (it goes
  to the ground or the stores) and, for a kit tile, *Drop*. The weapon's are *Unequip* and *Drop*.
  Clicking an empty tile opens **Pick from stores**: a short list of what the colony's stores hold
  that fits that slot, a row each (icon, name, quality, the store it is in), and clicking a row
  orders the colonist to fetch and wear it. At most eight rows and a pager if more; **never a
  scrollbar**.
- **A loadout line**: the colonist's loadout named once in the tab ("Loadout: Doctor", or
  "Loadout: none"), clickable, opening the loadout picker from piece 2.

Draw these states:

- **Today** (what the first build will show): bare hands, the issued jumpsuit, every other slot
  empty, both belt slots empty, the pack slots locked, no loadout.
- **Armed only**: a machete at the hip, otherwise as today.
- **Fully kitted**: a *Wool cap* (Normal), *Gas mask* (Decent), *Field coat* (Uber), *Padded vest*
  (Normal), *Pack* (Poor), a *Crowbar* drawn; kit holds 4 *Medical supplies*, 2 *Rations*, 1
  *Flashlight*, 1 *Canteen*; loadout *Doctor*. (These item names are placeholders for the mock; the
  game's names live in its registry and will be corrected there.)
- **A slot's popover open** on the Field coat.
- **Pick from stores open** on the empty Head slot, five rows.
- **A downed colonist**: the same tab, everything still worn (a downed or dead colonist keeps their
  gear); the buttons are disabled with a one-line reason, because nobody can change a downed
  colonist's clothes except by the **Strip** order below.

### 2. The Loadout column and the loadout editor

The **Assign** tab (F4) is shipped: a window docked **bottom-left** on the command bar, **536 px
wide**, a row per colonist in roster order, twelve rows a page with a pager at the foot. Its
columns today are **Colonist** (the square portrait and the name), **Area** (*Anywhere* / *Home*)
and **Response** (*Fight back* / *Defend* / *Flee*); a click on a setting cell moves it to the next
value. It was designed so a third setting column could be added **to the right without moving what
is there**. Add **Loadout**:

- A Loadout cell shows the loadout's name, or **None** dimmed. It does **not** cycle: a click opens
  the **loadout picker**, a small list anchored to the cell: *None*, each named loadout, and at the
  foot *Edit loadouts...*. Say the tab's new constant width.
- The **loadout editor** opens from *Edit loadouts...* (and from the Gear tab's loadout line). It
  is a docked window of its own, beside the Assign tab or replacing it; choose and say why. It has:
  a list of loadouts on the left (name, how many colonists use it, *New*, *Rename*, *Delete*); and
  for the selected loadout on the right, **Wear**: one row per worn slot (Head, Face, Body, Armour,
  Back) naming what to wear there, or *Anything*, or *Leave as is*; and **Kit**: up to six rows of
  item and count ("Medical supplies  4"), with the counts changed by drawn minus and plus buttons.
  Nothing in the editor scrolls.
- Draw: the Assign tab with the Loadout column (ten colonists, four with *Doctor*, two with
  *Winter*, the rest *None*), the picker open on one cell, and the editor with *Doctor* selected.
- The names to use, in this order: **Trent, Benjamin, Lyra, Charles, Orson, Evelyn, Eleanor,
  Logan, Gareth, Christian**. Keep the Area and Response values mixed as in the attached screenshot.

### 3. The gear rows in the right-click menu

The game already has a **right-click context menu** on the board: a small list of rows, each an
icon and a verb, e.g. **Equip Machete** on a weapon lying on the ground. Gear adds rows, one verb
per row, in this order where they apply:

| On | Rows |
|---|---|
| a weapon on the ground or in a store | Equip *\<weapon\>* |
| a garment, headgear, mask, vest or pack | Wear *\<item\>* |
| a small item (medical supplies, a ration, a flashlight) | Take into kit *\<item\>* |
| a downed or dead person (a bandit) | Strip *\<name\>* — everything they wear and carry is dropped beside them to be hauled |

A row that cannot be done now is shown **disabled with its reason** on the same line, dimmed (e.g.
*Take into kit Ration (kit full)*). Draw one menu over a garment with two rows (Wear, and a disabled
Take into kit that says why), and one over a downed bandit with Strip.

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
| Text dim | white at 50% | headers, hints, empty slots |
| Accent | `#6fd3e3` | the selected row, the active tab, a switch that is on |
| On accent | `#0b1116` | ink on an accent fill |
| Warn / Bad / Good / Info | `#e8b55c` / `#e06a5c` / `#7fc98c` / `#8fd0e3` | only where this brief invites one |
| Quality | Poor = Bad `#e06a5c`; Normal = text meta (no colour); Decent `#e9e08c`; Uber `#45c7b0`; Epic `#b98ce8` | the quality word only, never a tile fill |

Contrast comes from the scrim over the 3D board, not from the panels: panels are flat, no
gradients, no shadows, no glow, no rounded corners beyond what `hud-v2.html` uses.

**Type** — six steps and no seventh. Words in **Archivo Narrow**; every figure (counts, percentages,
temperatures, page numbers, hotkey caps) in **IBM Plex Mono 500** with tabular figures.

| Step | Use |
|---|---|
| 11 / 600, tracked 0.14em, upper case | panel labels, column headers, slot labels: "HEAD", "KIT", "LOADOUT" |
| 12 / 400 | meta lines, quality words, the effects line, reasons |
| 13 / 400 | body text |
| 14 / 500 | list rows, item names, menu rows |
| 19 / 600 | the name of the selected thing |
| 11 mono at 35% ink | a hotkey cap |

**Space** — rows are 30 px; panel padding 12 px; a 1 px border; gaps of 9 px. **Icons are 32 px or
64 px and nothing in between**; a slot tile is sized round a 32 px icon. A panel's stated width
includes its 12 px padding and 1 px border on both sides.

**Docking** — the inspect pane and the Assign tab are bottom-left on the command bar; a new window
docks the same way, with the standard header (title at the panel-label step, a close X at the
right); Escape closes it.

**The command bar**, in this order with these caps: Build B · Work F1 · Inventory F2 · Research F3 ·
**Assign F4** · Animals F5 · Bills F7 · Factions F8 · Almanac F9 · Menu Esc. Bills and Factions are
dead items, drawn dimmed as `hud-v2.html` draws them.

**Icons** — the game's icons are pixel art; wherever an item, a slot or a portrait goes, draw a
**flat placeholder square**. Draw as SVG paths (24 x 24 viewBox, one colour, no text) only: the
**lock** on a pack slot, the **minus** and **plus** of the editor, and the **pager chevrons**. **No
characters outside ASCII anywhere**: the two shipped fonts draw nothing else, so a tick, a cross, an
arrow, a middle dot, a degree sign or a dingbat would render as an empty box. Write temperatures
as "26 C".

### Deliverables

Static HTML, one file per state, 1920 x 1080, with the fonts loaded from Google Fonts exactly as
`hud-v2.html` does and the tokens declared once as CSS variables at the top of each file:

1. `gear-today.html` — the Gear tab as the first build shows it.
2. `gear-armed.html` — a machete at the hip.
3. `gear-kitted.html` — fully kitted.
4. `gear-popover.html` — the Field coat's popover open.
5. `gear-pick.html` — Pick from stores on the Head slot.
6. `gear-downed.html` — a downed colonist's tab.
7. `assign-loadout.html` — the Assign tab with the Loadout column, one picker open.
8. `loadout-editor.html` — the editor with *Doctor* selected.
9. `gear-menu.html` — the two right-click menus, over a board screenshot.
10. `gear-glyphs.svg` — lock, minus, plus, chevron, each one path.

At the top of each HTML file, in an HTML comment, **list every measurement you chose that the
system above did not fix** — the tab body height, the slot tile size and positions, the kit tile
size, the popover and picker widths, the Assign tab's new width and the Loadout column's width, the
editor's width and height — because those numbers become constants in code. If you answer in
writing rather than HTML, give the same list.

### What not to do

- No **drag and drop**: every change is a click on a tile, a row or a button.
- No **tools** (axe, pickaxe, hammer) as gear, no **durability** or condition bar, no **weight** or
  kilograms anywhere: the kit's limit is its slots.
- No hands, feet, gloves or boots slots; no second weapon slot; no off-hand or shield.
- No **naked** state: an empty Body is the issued jumpsuit.
- The load in the arms is **not** on the Gear tab.
- No new colours, faces or sizes. No scrollbars. No rounded, glowing, translucent or gradient
  panels. Do not rename *Gear*, *Kit*, *Loadout*, *Head*, *Face*, *Body*, *Armour*, *Back*,
  *Weapon*, *Equip*, *Wear*, *Take into kit*, *Strip*, *Assign*.
- No copying of RimWorld's layout or wording; the precedents are this game's own attached panels.

---

## Attachments to send with the prompt

| File | Why |
|---|---|
| `docs/reference/mockups/hud-v2.html` (and `icon-map.js` beside it) | the HUD's own mockup: the base plate, the bar, the tokens |
| `docs/reference/mockups/work-v1.html` | the table precedent |
| a screenshot of a **colonist's inspect pane on the Skills tab** | the pane, its header, the tab strip with Gear dimmed, the 157 px body |
| a screenshot of the **Health tab** | the weapon row as it reads today |
| a screenshot of the **Assign tab (F4)** open | the table the Loadout column joins |
| a screenshot of the **right-click menu** with *Equip* on a weapon | the menu the gear rows join |
| a screenshot of a **storage pane** | the precedent for a list of items with icons and counts |

The five screenshots of the running game are the owner's to take; nothing in the repository holds
them yet.

## Answers recorded

Owner, 2026-09-25 (`docs/research/gear-interview.md`): slots and a size class, not weight; a trip
cap per commodity for heavy loads; the interface and the weapon verbs first; clothes, hats, armour,
kit consumables and utility items, not tools; warmth, armour, rain and look; quality without wear;
the jumpsuit is the empty Body slot; loadouts plus manual orders; Head, Face, Body, Armour, Back;
belt 2 and pack 4; a paper doll inside the pane; a Loadout column on Assign with an editor; one
weapon; sources are drops, bandit loot and crafting; context menu and tab buttons, no drag and drop;
all worn slots drawn on the figure; the corpse keeps its gear and a Strip order drops it; no loadout
means keep what you have; the end state designed once; the names Gear, Kit, Loadout.

## Built

**Claude Design answered on 2026-09-25 with a written specification of the Gear tab** (states 21a–21f,
target 21c), pasted into the session by the owner, who then asked for *"all of it"*: the whole tab,
the hand real and everything else a debug-menu preview. It was built the same day on
`claude/vigilant-bardeen-8idplc`; `docs/design/47-gear-tab.md` carries every constant, the preview
and the six places the build departs from the specification (§5). The Loadout column, its editor
and the Strip row of the right-click menu (pieces 2 and 3) were not specified back and are units
G6 and G7.
