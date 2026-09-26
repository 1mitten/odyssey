# Brief for Claude Design — the storyteller: its picker, its settings and the tension gauge

**2026-09-26.** The prompt below goes to Claude Design verbatim, with the attachments listed at the
end. The decisions behind it are in `docs/research/storyteller-interview.md` (owner, 2026-09-26,
every recommendation taken) and `docs/design/58-storyteller.md`. What comes back is built as ST5
(the setup page and Settings) and ST6 (the gauge) in `docs/plans/storyteller.md`, and its
measurements become constants in code.

---

## The prompt

You are designing four small pieces for **Odyssey**, a colony-simulation prototype in the RimWorld
mould. It is set in a ruined sci-fi city and rendered in 3D with discrete vertical layers. The
interface is a dark, flat HUD over the 3D board. It is already built and shipped; you are adding to
it, and you must match it exactly. Attached are the HUD's own mockup (`hud-v2.html`) and screenshots
of the running game. **Everything you draw must look as if it came from the same hand as those.**

### What a storyteller is

A **storyteller** decides *when* things happen to the colony (raids, supply drops, later visitors
and newcomers) and *how hard* the threats hit. It does not simulate a world. It paces the game,
building tension and then releasing it. The player picks one when starting a colony and can change
it later. Separately, the player picks a **difficulty**, which scales how hard threats are and how
forgiving the game is after a disaster.

There are three storytellers. **Their names are placeholders** and will change, so design for a
name of 4 to 14 characters:

| Placeholder | How it paces | Blurb (placeholder copy, use it as written) |
|---|---|---|
| **Steady** | a rhythm: a threat about every week, a breather between, slowly rising | *Pressure rises and falls in a rhythm you can learn. A threat every week or so, a breather between.* |
| **Calm** | long quiet stretches, then one hard test | *Long quiet stretches to build in, then one hard test. For colonies that want time.* |
| **Chaotic** | random: anything, any time, but never quiet for too long | *Anything, any time. Two raids in a day, or a fortnight of nothing.* |

The six difficulty rungs, plus Custom: **Peaceful** (no big threats at all), **Gentle**, **Easy**,
**Normal** (the default), **Hard**, **Brutal**, **Custom**. Custom exposes four numbers: *Threat
scale* (0 to 500%), *Big threats* (on or off), *Adaptation* (0 to 200%), and *Grace* (x0.5 to x2,
how long before the first big threat).

The game also keeps a number called **tension**, and unusually for the genre it is **shown to the player**. It
drops sharply when a colonist dies or is downed, so the next threats are smaller, and it climbs back
slowly through quiet days. The player sees it as one of **five bands** (placeholder names):
**Reeling, Easing, Even, Building, Peak**. They also see **the last thing that moved it**, for
example *"A colonist died, 3 days ago: easing off."* The number itself is never shown; showing it
would invite optimising a number rather than playing.

### You are designing

1. **The storyteller and difficulty picker on the New game page.**
2. **The same two choices on the Settings window's Gameplay tab**, plus one switch.
3. **The tension gauge** on the clock.
4. **A placeholder portrait and a rhythm strip** for each storyteller.

### 1. The New game page

The New game page is a full-screen panel inset 24 px from every edge (padding 32/40). The attached
screenshot shows it today, top to bottom:

- **a board row**: Colony name field, Seed field and a Reroll button, and a Size picker;
- **the people**: three colonist candidates in a left column, the selected one's detail and skills
  grid on the right (two columns of skills, capped at 636 px wide, which leaves the right-hand part
  of the page empty);
- **a foot row**: Back, and **Start**, the only green button in the game.

Add **Storyteller** (pick one of three) and **Difficulty** (pick one of seven) to this page.

- Propose **one** placement and say why. Choose between: **(a)** a *Story* row under the board row,
  across the page; **(b)** a block in the empty right-hand part beside the skills; or **(c)**
  something better you can defend. **The page must not become a second step**: it is one page, and
  that was a decision.
- A storyteller choice shows its **portrait**, its **name**, its **blurb** and its **rhythm strip**
  (piece 4). Say whether all three are shown at once as cards, or one at a time with the other two as
  smaller choices. One at a time is compact; all three at once makes them comparable, which is what
  a choice needs.
- Difficulty is a **ladder of seven**. When **Custom** is picked, its four controls appear. Say where
  they appear, and make sure nothing else on the page moves when they do. A control that shifts under
  the pointer is a fault this project has fixed before.
- The defaults are **Steady** and **Normal**.
- **The whole page must fit at 1280 x 720 without scrolling** (never a scrollbar: a hard rule of this
  HUD) as well as at 1920 x 1080. Say what gives way at 1280 if anything must.
- Every clickable thing on this page wears a 1 px border at white 13%, so what can be pressed is
  visible without hovering. Keep that.

### 2. The Settings window, Gameplay tab

The Settings window is a fixed 1240 x 720 modal, the same frame on every tab (attached screenshot):

- a 52 px header;
- a 240 px left rail of five tabs (Interface, Graphics, Audio, Keys, Gameplay), with Save / Save as /
  Load / Quit / Exit pinned at its foot;
- a 72 px title band with a 38 px tile in the tab's hue;
- **two equal columns**, 20/24 padding, 40 px between them, 22 px between sections, 38 px rows;
- a 52 px footer with *Reset Gameplay to defaults*.

Gameplay's hue is the accent. Today its left column holds one section, **Saving** (the autosave
ladder), and its right column is empty.

- Add a **Story** section with a **Storyteller** row and a **Difficulty** row, and Custom's four
  controls beneath them when Custom is picked. Use the window's own controls only:
  - a **segmented** control (joined segments, the lit one in the accent);
  - a **switch** (the word On or Off in mono 12, then a 38 x 22 track with a 14 px square knob);
  - a **slider** (220 x 4 track, 10 x 18 thumb, the figure after it);
  - a **select** (190 x 28 with a drawn triangle).

  Say which control each row uses and why. Seven difficulty rungs may not fit one segmented row;
  measure it.
- Add **Pause on big threats**, a switch, default **On**. Put it in a section of its own or in
  Saving, and say which.
- Draw the tab **twice**: over a running colony, and **with no colony loaded**, opened from the title
  screen. With no colony the Story rows are **greyed and not pressable**, with a one-line meta note
  such as *"Chosen when a colony starts"*. Pause on big threats is a machine preference and stays
  live.
- The storyteller's portrait and blurb **do not** need to appear here. Show them on hover as a
  tooltip if you think it earns its place, and say.
- **Nothing on the tab may change the frame.** The columns have 502 px of height. Show that Custom
  open still fits.

### 3. The tension gauge on the clock

The clock is one panel docked to the top edge in the right-hand column, **271 px wide**. Its single
**28 px** line holds, left to right:

- the time, `14:00`, in mono;
- the date, `Day 2 · Tansy · Wash` (day, month, season), in white at 66%;
- a **16 px weather glyph** (a drawn sun, cloud, rain or storm);
- the outdoor temperature in a soft yellow (`rgba(240,214,122,.92)`).

The speed buttons sit under that line.

- The gauge is a **glyph on this line**, no more than 16 px, beside the weather glyph. **It must not
  add a second line**: the strip has a height budget that a test holds. Measure the line and show it
  fits at 271 px with the longest dates, `Day 12 · Larkspur · Wash` and `Day 12 · Bramble · Glare`. If it cannot fit, say what
  gives way; the date is the likeliest candidate.
- Draw it in **all five bands**. The band must read at a glance and must not be mistaken for an
  alert, so **do not use the Bad red for any band**. Say how the five differ: fill level, shape, hue
  within the tokens below, or a combination. It must be readable by a colour-blind player: the five
  must differ by more than hue alone.
- Draw its **tooltip** (the HUD's existing tooltip: panel fill, 1 px border, body text):
  - line 1, the band name at 14/500;
  - line 2, the last cause and when, at 12/400 meta: *"A colonist died, 3 days ago"*, *"A colonist was
    downed, 1 day ago"*, *"Nine quiet days"*.
- With **no storyteller** (an old save), the gauge is not drawn at all. Show that state too.
- Deliver the five states as **SVG paths** (`d` strings) on a 16 x 16 viewBox, or as one
  parameterised shape with the rule for each state written out. Single colour per state, no text, no
  gradients, and no stroke below 1.5 px.

### 4. Portraits and rhythm strips

Each storyteller will have a real portrait later, the owner's to commission. For now:

- a **placeholder portrait**: a flat square (state its size, between 64 and 128 px), dark, with a
  simple drawn emblem per storyteller. Abstract, not a face, no text.
- a **rhythm strip**: a small drawn chart of **a typical 24-day season** under that storyteller,
  with threats as marks along a line and height as their size. It shows the *mechanic*, so a player
  can see the difference between a rhythm, long calm, and chaos without reading. Use these seasons:
  - **Steady**: threats on days 3, 10, 12, 20, rising a little each time;
  - **Calm**: one threat on day 14, large;
  - **Chaotic**: threats on days 2, 3, 11, 22, sizes uneven.

  Draw it as SVG. State its size. It must read at the page's scale and must not look like live data
  (it is illustrative, not this colony's history).

### The design system, verbatim

Do not introduce any colour, face, size or spacing not listed here.

**Colour**

| Token | Value | Use |
|---|---|---|
| Panel fill | `#0c1014`, at 100% on HUD panels and 96% on windows and the New game page | every panel and window; 5 px corner radius |
| Panel border | white at 13% | 1 px, every panel and every pressable row on the New game page |
| Control border | white at 26% | inputs, segmented controls |
| Row rule | white at 7% | row dividers |
| Switch off track | white at 10% | a switch that is off |
| Text primary | `#eef3f6` | names, row text |
| Text meta | white at 66% | meta lines, secondary values |
| Text dim | white at 50% | headers, hints, greyed rows |
| Accent | `#6fd3e3` | the selected thing, a lit segment, a switch that is on, Gameplay's hue |
| On accent | `#0b1116` | ink on an accent fill |
| Warn / Bad / Good / Info | `#e8b55c` / `#e06a5c` / `#7fc98c` / `#8fd0e3` | only where this brief invites one; **Good is Start's colour**; **Bad is never a tension band** |
| Settings scrim | `rgba(6,10,12,.58)` | behind the Settings window |

Contrast comes from the scrim over the 3D board, not from the panels. Panels are flat: no gradients,
no shadows, no glow, and no rounded corners beyond what `hud-v2.html` uses.

**Type.** Six steps and no seventh. Words are set in **Archivo Narrow**; every figure (the time,
percentages, page numbers, hotkey caps) is set in **IBM Plex Mono 500** with tabular figures.

| Step | Use |
|---|---|
| 11 / 600, tracked 0.14em, upper case | section labels and headers: "STORY", "DIFFICULTY" |
| 12 / 400 | meta lines, the tooltip's cause line, "Chosen when a colony starts" |
| 13 / 400 | body text, the blurbs |
| 14 / 500 | rows, choices, the tooltip's band name |
| 19 / 600 | the name of the selected storyteller, the tab title |
| 11 mono at 35% ink | a hotkey cap |

**Space.** Rows are 38 px in Settings and 30 px elsewhere; panel padding is 12 px; borders are 1 px;
gaps are 9 px. The frame sizes above are fixed.

**Icons.** The game's icons are pixel art at 32 px. Where a game icon would go, use a flat
placeholder square. The things you draw (the gauge, the emblems, the rhythm strips) are SVG. **No
characters outside ASCII anywhere**: the two shipped fonts draw nothing else, and a tick, a cross,
an arrow, a bullet or a dingbat renders as an empty box. The middle dot in the date is the one
exception already shipped. Chevrons and marks are drawn, not typed.

### Deliverables

Static HTML, one file per state, at 1920 x 1080 unless stated. Load the fonts from Google Fonts
exactly as `hud-v2.html` does, and declare the tokens once as CSS variables at the top of each file,
so the file can be diffed against the shipped tokens.

1. `newgame-default.html`: the New game page with Steady and Normal picked.
2. `newgame-custom.html`: Chaotic and Custom picked, Custom's controls showing, and nothing else
   moved compared with file 1.
3. `newgame-1280.html`: file 1 at **1280 x 720**.
4. `settings-gameplay.html`: the Gameplay tab over a running colony, Steady / Normal.
5. `settings-gameplay-custom.html`: Custom open, showing that it fits the 502 px column.
6. `settings-gameplay-nocolony.html`: opened from the title screen, the Story rows greyed.
7. `clock-gauge.html`: the clock panel five times, one per band, at 2x zoom and at 1x, with the
   tooltip open on one; plus the no-storyteller clock.
8. `tension-glyphs.svg`: the five gauge states, each one path on a 16 x 16 viewBox, with an id per
   band.
9. `storyteller-emblems.svg` and `rhythm-strips.svg`: the three placeholder emblems and the three
   strips.

At the top of each HTML file, in an HTML comment, list every measurement you chose that the system
above did not fix: the Story block's width and height, card sizes, the portrait size, the strip's
size, the gauge's margin on the clock line, which control each Settings row uses and its width. Those
numbers become constants in code.

### What not to do

- No storyteller speech, commentary, dialogue or character during play. The persona is the portrait
  and the blurb, at setup and nowhere else.
- No number on the gauge, no bar with a scale, no forecast of the next threat. The game gives no
  warning; the raid's gathering at the map edge is the warning.
- No wealth, value or points readout anywhere.
- No second setup step, no wizard, no scrollbars.
- No new colours, faces or sizes. No rounded, glowing, translucent or gradient panels.
- Do not rename anything; the placeholder names are the owner's to change.
- No copying of RimWorld's storyteller screen, portraits or text: the layout is this game's New game
  page and Settings window, and the attachments show them.

---

## Attachments to send with the prompt

| File | Why |
|---|---|
| `docs/reference/mockups/hud-v2.html` (and `icon-map.js` beside it) | the HUD's own mockup: the tokens, the base plate, the clock |
| a screenshot of the **New game page** at 1920 x 1080, a candidate selected | the page the picker goes on, and its empty right-hand part |
| a screenshot of **Settings > Gameplay** over a running colony | the frame, the rail and the empty right column |
| a screenshot of **Settings > Graphics** | the segmented controls, switches and ladders in use |
| a close crop of the **clock panel** at the top right, in rain, with the temperature showing | the 28 px line the gauge joins, with the weather glyph beside it |
| a screenshot of a **HUD tooltip** (hover the weather glyph) | the tooltip the gauge's reuses |

The screenshots are the owner's to take; nothing in the repository holds them. The mockups 14a–14e
that the Settings window was built from were pasted into a session, not committed.

## Answers recorded

Owner, 2026-09-26: a strength-based budget; visible adaptation as five bands and a cause, gauge by
the clock; storyteller and difficulty as separate axes on the setup page and changeable in Settings;
three shapes, names later; portrait and blurb only; no warning beyond the raid's gather; pause and
jump on a big threat behind a setting, default on; presets plus Custom.
