# Brief for Claude Design: the Factions tab, the tithe prompt and the faction glyphs

**2026-09-26.** The prompt below is handed to Claude Design verbatim, with the attachments listed at
the end.

The decisions behind it:
- `docs/research/factions-interview.md` (the owner's interview, three rounds, 2026-09-26);
- `docs/design/61-factions.md` (§2c for the relation thresholds, §4a for the collector, §8 for the
  tab).

What comes back is built in two units of `docs/plans/factions.md`:
- **F1:** the tab and the glyphs;
- **F2:** the prompt and the alert.

Its measurements become constants in code.

---

## The prompt

You are designing three small pieces for **Odyssey**, a colony-simulation prototype in the RimWorld
mould, set in a ruined sci-fi city and rendered in 3D with discrete vertical layers. The interface
is a dark, flat HUD over the 3D board, already built and shipped; you are adding to it and you must
match it exactly. Attached are:
- the HUD's own mockup (`hud-v2.html`);
- the Work tab's mockup (`work-v1.html`), the closest precedent for a table of rows;
- screenshots of the running game.

**Everything you draw must look like it came from the same hand as those.**

### What factions are

The colony is not alone. Other peoples live beyond the map, and each remembers how the colony has
treated it as one number, **goodwill**, from **-100 to +100**. Goodwill decides the **relation**:

- **Hostile**: their people fight ours on sight and raid us.
- **Neutral**: they leave us be, and their traders and visitors come.
- **Ally**: they will help.

The relation changes at thresholds **with a gap between them**, so it does not flicker:

- a Neutral faction turns **Hostile at -75 or below**, and a Hostile one is **Neutral again only at
  0 or above**;
- a Neutral faction becomes an **Ally at +75**, and an Ally falls back to **Neutral at 0**.

So the same goodwill, say -40, can be Hostile or Neutral, depending on which way it got there.
Goodwill also **drifts** a few points a day back towards each faction's natural level. Every change
has a **reason** the game keeps (the last five per faction).

The factions, with example state to draw:

| Faction | Relation | Goodwill | Natural level | Last reasons (newest first) | Settlements |
|---|---|---|---|---|---|
| **Bandits** | Hostile | -62, rising | -100 to -80 (they drift back to Hostile) | +20 Tithe paid (Tansy 4) · -30 Tithe refused (Larkspur 9) · -50 Collector attacked (Larkspur 2) | Cinder Yard 3 days · Pelham Cross 5 days |
| **The Orc Army** | Hostile, **permanently**: goodwill is fixed at -100 and nothing moves it | -100 | none | none: "No dealings" | none |
| **The Cartage** | Neutral | +14, flat | 0 to +30 | +2 Trade (Tansy 6) · +2 Trade (Tansy 1) | Wharf Nine 2 days · Lower Tanning 4 days · Mill End 6 days |
| **The Kindred** | Neutral | +31, falling | 0 to +40 | +15 Prisoner released (Tansy 3) · -20 Prisoner executed (Larkspur 11) | Ashby Holding 1 day · Greave 3 days |

- The settlement names are **placeholders** for this mockup; the game will supply its own.
- Dates are in the game's calendar: six months of twelve days (Larkspur, Tansy, Bramble, Ember,
  Hollow, Candle).

You are designing:

1. **The Factions tab**: a table of the factions, and the detail of one.
2. **The tithe prompt**: the Bandits' collector demanding a share of the colony's stores.
3. **Four faction glyphs**: one small mark per faction.

### 1. The Factions tab

It opens with **F8** or the *Factions* item on the command bar. There is **one row per faction**, in
the order above. Each row carries:

- the faction's **glyph** (piece 3) in its livery colour;
- its **name**;
- the **relation word**;
- a **goodwill bar** from -100 to +100, with its current value as a figure;
- a **trend** mark for the last day (rising, falling or flat), drawn, not typed.

**The goodwill bar is the heart of the tab.** It must show the player **how far it is to the next
change of relation from where they stand**:
- for a Hostile faction, that is the 0 line;
- for a Neutral one, it is both -75 and +75.

Propose **one** way to draw the thresholds and say why:
- **all three marks always**, with the relevant one emphasised;
- **only the relevant marks** for the current relation;
- or **a band** behind the bar, the stretch where the current relation holds.

**The Orc Army's bar** is fixed at -100. It must read as *nothing will move this*, not as a faction
at its worst. Draw it differently and say how.

**Relation colours:**
- Hostile in Bad, Ally in Good, Neutral in text meta.
- **The word always carries the relation too**, so a colour-blind player loses nothing.
- The game has already re-tuned its category hues for deuteranopia, and a relation must never be
  shown by colour alone.

**Clicking a row selects it** (accent fill with dark ink on it, as the Assign tab does) and shows its
**detail**:
- the last five reasons, each as amount, reason and date;
- its settlements, each with a distance in days;
- one meta line saying where its goodwill drifts to ("Drifts back towards hostile", "Drifts towards
  0 to +30").

Propose **one** place for the detail and say why:
- **inline**, expanding under the selected row;
- or **a detail pane** to the right of the table, inside the same window.

**Whichever you choose, the window's size must not change when the selection changes.**

Also:
- **There are no actions in this version.** Gift, request help and declare war come later.
  - **Do not draw them, not even greyed.**
  - Leave room at the foot of the detail where a row of buttons would go without moving anything
    above it.
- There are four factions today and at most **eight**. Size the table so eight fit with no pager.
  **Never a scrollbar**: that is a hard rule of this HUD.
- The width and height are **constant**. State them.

### 2. The tithe prompt

Every so often the Bandits send a **collector** with an escort.
- They stop just inside the edge of the map and demand a share of the colony's stores: about **15%
  of the value of everything in store**.
- The collector chooses from the most valuable goods first.
- The game pauses and asks.

Draw it as a **modal prompt** in the style of the game's leave prompt (attached screenshot): a
centred panel over a scrim, a title, body text, buttons on the foot, and focus on the safe choice.
It holds:

- **Title:** *The Bandits have come for their tithe*.
- **The demand**, as a short list. Each line has an item placeholder square, the goods' name, the
  quantity and the value (a figure in mono). Use:

  | Goods | Quantity | Value |
  |---|---|---|
  | Meals | 24 | 480 |
  | Medical supplies | 6 | 360 |
  | Scrap metal | 40 | 200 |
  | Wood | 120 | 120 |

  Then a total line, **1,160**, and beside it the share it represents: *15% of your stores*.
- **What each answer does**, one line each, in meta:
  - *Pay: haulers carry the goods to the tribute spot. They leave you be.*
  - *Refuse: they leave. In two days, a raid.*
- **The two buttons, Pay and Refuse.**
  - Say which one takes focus, and why.
  - Neither is destructive in the leave prompt's sense, and the game has so far put focus on the
    choice that loses nothing.

Also draw the state **after a refusal**:
- The **alert** that stands in the alerts column (attached screenshot) reads *Tithe refused: raid in
  1d 14h*, with the countdown as a mono figure.
- The alert can be clicked to pay late. That opens the same prompt with the title *Pay the tithe
  late?* and the same list.
- Draw the alert only; the game supplies its behaviour.

### 3. The faction glyphs

One mark per faction, used in the tab's rows, in the detail, and later on the prompt.
- **Each is one SVG path** (a `d` string) on a 24 x 24 viewBox.
- Single colour, no text, no gradients.
- Legible at **17 px** and at **32 px**, in the weight of the attached house glyph
  (`home-glyph.svg`).

The four factions:

- **Bandits:** a gang that runs a protection racket. Their people wear welding helmets and red
  vests.
- **The Orc Army:** a war horde; loud, heavy, no talking.
- **The Cartage:** a hauliers' combine that runs the routes between settlements.
- **The Kindred:** scattered survivor holdings that look out for each other.

Propose a **livery colour** for each, either from the tokens below or as a stated new hex.
- Any new hex must keep a 3:1 contrast against the panel fill, and stay distinguishable from the
  other three under deuteranopia. Say how you checked.
- The Bandits' colour should sit near the red of their vests without being the Bad token, which
  means "hostile" in this tab.

### The design system, verbatim

Do not introduce any colour, face, size or spacing not listed here, except the four livery hexes
invited above.

**Colour**

| Token | Value | Use |
|---|---|---|
| Panel fill | `#0c1014` at 100% | every panel and window |
| Bar fill | `#0c1014` at 90% | the command bar |
| Panel border | white at 13% | 1 px, every panel |
| Divider | white at a lower alpha than the border | row rules |
| Text primary | `#eef3f6` | names, row text |
| Text meta | white at 66% | meta lines, secondary values, the Neutral relation |
| Text dim | white at 50% | headers, hints |
| Accent | `#6fd3e3` | the selected row, the active tab |
| On accent | `#0b1116` | ink on an accent fill |
| Warn / Bad / Good / Info | `#e8b55c` / `#e06a5c` / `#7fc98c` / `#8fd0e3` | Bad for Hostile, Good for Ally; Warn for the tithe alert |

Contrast comes from the scrim over the 3D board, not from the panels. Panels are flat: no gradients,
no shadows, no glow, and no rounded corners beyond what `hud-v2.html` uses.

**Type.** There are six steps and no seventh.
- Words are set in **Archivo Narrow**.
- Every figure is set in **IBM Plex Mono 500** with tabular figures: goodwill values, reason amounts,
  days, quantities, values, the countdown and hotkey caps.

| Step | Use |
|---|---|
| 11 / 600, tracked 0.14em, upper case | panel labels and column headers: "FACTION", "RELATION", "GOODWILL" |
| 12 / 400 | meta lines, the drift line, the prompt's consequence lines |
| 13 / 400 | body text |
| 14 / 500 | list rows, command-bar labels |
| 19 / 600 | the name of the selected faction, the prompt's title |
| 11 mono at 35% ink | a hotkey cap |

**The minus sign.** Write goodwill with an ASCII hyphen-minus (`-62`). The shipped fonts are checked
for every non-ASCII character, and a true minus may not be drawn.

**Space.**
- Rows are 30 px, panel padding is 12 px, borders are 1 px, and gaps are 9 px.
- The Work tab's table is 1,385 px wide with a 192 px frozen name column. Follow its proportions;
  this table needs far less width.
- Pick a constant width and height that fit eight rows plus the detail, and state them.

**Docking.**
- The tab is a window docked **bottom-left**, flush with the left screen edge, sitting on top of the
  command bar.
- It has the standard window header: the title *Factions* at the panel-label step and a close X at
  the right. Escape closes it.
- The prompt is centred over a scrim.

**The command bar**, in this order with these caps:

> Build B · Work F1 · Inventory F2 · Research F3 · Assign F4 · Animals F5 · Bills F7 ·
> **Factions F8** · Almanac F9 · Menu Esc

Draw Factions as the active item (accent fill at 12%) where the tab is open. Bills is a dead item,
drawn dimmed as `hud-v2.html` draws it.

**Icons.**
- The game's item icons are pixel art at 32 px. Where one goes in the prompt's list, use a flat
  placeholder square.
- The only icons you draw are the four faction glyphs, as SVG paths.
- **No characters outside ASCII anywhere.** The two shipped fonts draw nothing else, and a tick, a
  cross, an arrow, a true minus or a dingbat would render as an empty box.
- Trend marks and chevrons are drawn, not typed.

### Deliverables

Static HTML, one file per state, at 1920 x 1080.
- Load the fonts from Google Fonts exactly as `hud-v2.html` does.
- Declare the tokens once, as CSS variables at the top of each file, so a diff against the shipped
  tokens is possible.

The files:

1. `factions-bandits.html`: the tab open, the Bandits selected, their detail showing.
2. `factions-cartage.html`: the Cartage selected (a Neutral faction, so both thresholds are
   relevant).
3. `factions-orcs.html`: the Orc Army selected ("No dealings").
4. `tithe-prompt.html`: the prompt over the paused board.
5. `tithe-refused.html`: the board with the alert standing, and beside it the late-payment prompt.
6. `faction-glyphs.svg`: the four paths, each labelled in a comment with its livery hex.

At the top of each HTML file, in an HTML comment, list every measurement you chose that the system
above did not fix, because those numbers become constants in code:
- the window's width and height, and the column widths;
- the bar's length and thickness, and the threshold marks' size;
- the detail's placement and size;
- the prompt's width;
- the livery hexes.

### What not to do

- No diplomatic buttons (gift, help, war), not even greyed.
- No world map, and no map of settlements: they are a list.
- No portraits of faction leaders.
- No new colours beyond the four liveries; no new faces or sizes.
- No scrollbars.
- No rounded, glowing, translucent or gradient panels.
- No characters outside ASCII.
- Do not rename *Hostile*, *Neutral*, *Ally*, *Tithe*, *Pay*, *Refuse*, *Factions* or any faction.
- Do not copy RimWorld's faction screen. The table's shape is this game's Assign and Work tabs, and
  the attachments show them.

---

## Attachments to send with the prompt

| File | Why |
|---|---|
| `docs/reference/mockups/hud-v2.html` (and `icon-map.js` beside it) | the HUD's own mockup: the base plate, the bar, the tokens |
| `docs/reference/mockups/work-v1.html` | the Work tab, the table precedent |
| `docs/reference/mockups/home-glyph.svg` | the weight the faction glyphs must match |
| a screenshot with the **Assign tab open** (F4) | the docked table as built: header, rows, selection |
| a screenshot of the **leave prompt** (Esc → Quit to main menu) | the modal the tithe prompt follows |
| a screenshot of the **alerts column** with an alert standing | where the *Tithe refused* alert sits |
| a screenshot of the **command bar** | the dead Factions F8 item that goes live |

The four screenshots of the running game are the owner's to take; nothing in the repository holds
them yet.

## Answers recorded

From the owner, 2026-09-26 (`factions-interview.md`):
- the bandits, as they are, run a protection racket;
- the demand is a value-weighted share of the stores;
- refusal brings a warning and then a raid;
- the Orc Army is a permanently hostile war horde;
- the Cartage and the Kindred are on the roster;
- the world is a list of settlements.

Two things the brief leaves to the owner's later word:
- **The Sump** is not in it, because the owner has not chosen it (design 61 §3a).
- **The word *tithe*** is used as design 61 §3 proposes: the name of the demand, not of the faction.
