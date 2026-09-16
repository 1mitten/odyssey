# 14 — The HUD design system: tokens, type, space and layout

**Status: built, 2026-09-16.** This describes the rebuilt in-game HUD — the one the player looks at,
not the panel catalogue it draws from. `10-ui-panel-catalogue.md` still says what each region *is*
and what it will eventually hold; `09-ui-and-input.md` still fixes the architecture, the contract
and the input model. This says how the thing **looks and measures**, and it is the document that
owns every number on the screen.

**`docs/reference/mockups/hud-v2.html` is historical from here on.** It was the owner-approved
look for the first pass and the built HUD no longer follows it; nothing in that file was updated to
this specification, so it should be read as the record of a previous decision rather than as a
description of the screen.

It exists because the first HUD pass proved the catalogue and then could not be judged. It put every
region on screen, which was the right thing to prove. It also covered about a third of the viewport
doing nothing at all, printed its own debug codes at the player ("A1 · RESOURCES"), carried three
development notes as if they were game text, put two or three letters of an icon key in a coloured
tile wherever a picture was missing, and had accumulated **fourteen font sizes between 7 px and
16 px**, none chosen against the others, because every region picked its own as it was written.

---

## 1. The governing idea: contrast comes from the scrim, not from the panel

A panel dark enough to hold 13 px text over bright terrain has to be nearly opaque. A screen of
nearly opaque panels is a screen with a third of the board hidden behind it, and that is not a
styling problem — it is the direct cause of the coverage figure.

So the HUD carries **two always-on scrims**: a 170 px gradient down from the top and a 200 px
gradient up from the bottom, both fading from `rgba(8,11,14,.72)` and `.78` to nothing. They are
never pointer targets, they cost no panel area, and with them behind it the panel fill can drop to
`rgba(12,16,20,.86)` and the panels can stop being walls.

`HudContrast` measures the result rather than asserting it by eye. Body ink over a panel over the
**brightest thing the world can draw** — taken as pure white, so the figure holds against every
terrain and cannot go stale when a new biome lands:

| ink | ratio |
|---|---|
| `#eef3f6` primary | 11.6 : 1 |
| `rgba(255,255,255,.66)` meta | 6.6 : 1 |
| `rgba(255,255,255,.50)` dim | 4.5 : 1 |

`rgba(255,255,255,.35)` faint does *not* clear 4.5:1 and is therefore used only for hotkey caps,
which are a legend rather than body text.

**One thing the specification asks for that this engine cannot do.** The panel fill is specified
with `backdrop-filter: blur(6px)`. UI Toolkit has no backdrop filter and no way to sample what is
behind an element, so the blur is absent. What is lost is the softening of bright terrain seen
through a panel; the contrast that mattered is carried by the scrims, and the table above is
measured without any blur at all.

---

## 2. Type: six steps, two faces, and no seventh

| step | use |
|---|---|
| 11 / 600, tracked .14em, upper case | panel label — "STORES", "ALERTS", "DEPTH" |
| 12 / 400 | meta lines — "colonist · L12 · 78, 59" |
| 13 / 400 | body text, alert text |
| 14 / 500 | list rows, command-bar labels |
| 19 / 600 | the name of the selected thing |
| 24 / 500 mono | the clock, and nothing else |

Plus one legend style named separately by the specification: **11 px mono at `.35` ink** for a
hotkey cap.

**Numbers are a variant, not a step.** Every figure on screen — a stores count, the clock, a
coordinate, a percentage, a seed, a hotkey cap — is set in IBM Plex Mono at weight 500, so a column
of numbers is a column and a value that changes does not shuffle the text beside it. That is a
change of family at the same step, which is why it is a flag on `HudType.Of` rather than six more
roles.

**Faces.** Archivo Narrow for words, IBM Plex Mono for figures, both SIL Open Font Licence, both
committed under `Assets/Odyssey/Presentation/Ui/Fonts/` with their licences beside them. A narrow
face is not a preference here: the command bar carries eleven labelled items across the bottom of
the screen and the acceptance criteria forbid both an abbreviation and an item running off the edge,
so the words have to be narrow or there is no bar.

**Weights are approximated, and it is worth knowing where.** Google ships Archivo Narrow only as a
variable font and Unity's TrueType importer takes its default instance — weight 400. So 400 and 500
both draw as the regular face, and 600 and 700 are handed to the text renderer as bold for it to
synthesise. `HudType.BoldFrom` is the single place that split lives, so a real set of static faces
later changes one constant and one loader.

**Where this departs from the written specification.** The type section fixes six sizes and says
"no others"; the layout section then asks for 15/600 colonist names, 11/400 job lines, 10/400 rail
hints, a 9 px rail number, a 15 px alert icon and 13/500 and 12/500 labels. Those are snapped to the
nearest step, because a scale with six exceptions in it is not being enforced by anything and every
difference is one or two pixels. The 11 px mono hotkey cap is kept, because the type section itself
named it.

### 2.1 Interface scale (owner, 2026-09-16)

The type is authored in 1080p pixels and the panel scales with the screen, so on a 4K monitor it
subtends the same angle as on a 1080p one. The owner's report was nonetheless that it **reads too
small there**, and that is not a contradiction: physically identical is not perceptually identical
at arm's length from a large panel, and the HUD this replaced was drawn against a 1200 x 800
reference, which made every glyph on it 1.6 times larger than this one.

So the scale is a setting, which is what `09-ui-and-input.md` §9 D4 planned for all along — "a user
slider from 80 to 150 per cent". It is a **ladder rather than a slider** (80, 90, 100, 110, 125,
150), because a HUD at a fractional scale is a HUD whose one-pixel hairlines land between pixels.

**It works by dividing the reference canvas**: at 125% the panel is told its reference is
1536 x 864, so everything drawn against these numbers comes out an eighth larger. Nothing else has
to know. The layout is anchored rather than sized, so the smaller canvas is a case it already
handles — the colonist strip re-clamps to what fits, the command bar moves its tail into Menu, and
`HudLayoutTests` already tested a literal 1280 x 720, which is exactly the canvas 150% produces.
`UiScaleTests` walks every rung and asserts no two panels overlap at any of them.

**The default depends on the screen**: 100% below 1440p, 110% at 1440p, 125% at 4K and above.
**125 at 4K is arithmetic rather than taste.** The old HUD set body text at 11 px against a
1200 x 800 canvas, and on a 3840 x 2160 screen Unity's match-0.5 scaling is the geometric mean of
3.2 and 2.7 — so that text landed at about **32 physical pixels**. This one sets body text at 13 px,
and at 125% the canvas is 1536 x 864, so the scale is exactly 2.5 and the text lands at **32.5
physical pixels**. The default restores the size that was being read before the rebuild, which is
the size the report was about. The
coverage ceiling is stated at 100%, and the defaults are checked against it — at 125% the HUD
covers about 17%, still inside. **Above that the player is trading board for legibility** and the
panel says so on each rung: at 150% the HUD occupies about a quarter of the screen. That is a
choice worth offering and not one to make silently on somebody's behalf.

**The panel writes to a copy of its `PanelSettings`, never the asset.** It is a file on disk, and
writing to it from play mode in the editor leaves the change behind after the session ends —
permanently, in a committed asset. This project met that exact trap once already with the sky
material.

**The sheet sets no type at all.** `Hud.uss` contains not one `font-size`, `letter-spacing` or
`-unity-font-style`, and a test asserts that. Type comes from `HudType` through `HudText`, in the
Unity-free assembly, because "six sizes and no others" is an acceptance criterion and a size written
in a stylesheet can only be tested by parsing the stylesheet. Two sources for one decision is how the
HUD this replaces came to have fourteen sizes in it.

---

## 3. Space and tokens

4 px base unit, with the specification's own exceptions kept (7 px card gaps, 9 px panel gaps, 29 px
rows, 17 px icons, 3 px need bars) because they are the numbers the layout was drawn at.

```
screen edge margin     20        panel fill        rgba(12,16,20,.86)
gap between panels      9        bar fill          rgba(12,16,20,.90)
panel padding          12        panel border      rgba(255,255,255,.13)
list row height        29        divider           rgba(255,255,255,.09)
icon in a row          17        panel radius       5   bar 6, control 4, chip 3
icon-to-label gap       9
command bar item       38 high, 12 side pad (primary 14)
avatar (selected)      30        card avatar       26
```

Palette: `#eef3f6` primary, `.66` meta, `.50` dim, `.35` faint; accent `#6fd3e3` with `#0b1116` ink
on it; warn `#e8b55c`, bad `#e06a5c`, good `#7fc98c`, info `#8fd0e3`.

All of it lives in `HudTheme` and `HudLayout`, in `Odyssey.Hud`, which is compiled without
UnityEngine. `HudStyleSheetTests` parses `Hud.uss` and compares thirty colour declarations and
forty length declarations against those constants, and fails the fast tier when the two disagree.
The sheet keeps its literals — it stays legible as plain text, which has always been this file's
position — but it can no longer keep a literal nobody agreed to.

---

## 4. Icons

Three sizes and no others: **17 px** in a list row, **16 px** in the command bar, **30 px** for the
selected thing's avatar.

**Chrome is drawn, not imported.** `HudGlyph` renders ten shapes with `Painter2D` at Lucide's own
proportions — a 24-unit box, a 2-unit stroke, round caps and joins: pause, play, forward,
fast-forward, chevron up and down, close, hamburger, alert triangle, info. One path each, no
texture, no atlas, no licence. Chrome is the part that cannot wait for an art pipeline; a HUD with no
play button is not a HUD.

**Game glyphs are a square until the sheets land.** The old placeholder put two or three characters
of the key in a hashed colour tile — MEA, WOO, SCR. Both halves of that are struck out: the letters
read as truncated data rather than as a deliberate stand-in, and a filled colour tile behind a value
outshouts the value. What is left is a single-colour outlined square at a 1.6 px stroke. **The key
is still the contract**: when the owner's sheets land this element becomes a sprite lookup on the
same key, at the same three sizes, and nothing about the layout moves (ADR 0007).

**Category colour is a stroke, and it lives in two places only.** Stores and the command bar. Nine
categories — sustenance, organic, metal, mineral, fluid, medical, work, people, record — each
resolving to a colour that is already a palette token, because a screen with a hue per commodity is
a screen with no colour code at all. Everywhere else an icon takes the ink of the text beside it.

---

## 5. Layout

Reference canvas **1920 × 1080**, panel scaling with the screen at a match of 0.5. Every region is
anchored to a screen edge or centred; nothing is placed at a computed offset.

| region | anchor | notes |
|---|---|---|
| **Stores** | `left 20, top 20, w 288` | header carries `n / total` and a disclosure; zero-stock rows folded away |
| **Colonist strip** | centred, `top 20` | 106 × 63 cards (132 × 86 until 2026-09-17), 7 px apart; clamped to what fits between the two corners |
| **Clock + speed** | `right 76, top 20, w 266` | one panel — the merge *is* the fix for A3/A4 overlapping |
| **Alerts** | under the clock in the same column, 9 px gap | hidden outright when empty |
| **Depth rail** | `right 20, top 20, w 44` | one 26 × 16 cell a layer; shrinks rather than overflowing |
| **Inspect** | `left 20, bottom 84, w 560` | 43 px tall with nothing selected, 164 with a colonist |
| **Command bar** | centred, `bottom 20` | 38 px items, one row, overflow into Menu |

**The right-hand column is a column, and that is a scar.** The clock and the alerts were two
absolutely positioned panels in the same corner with hand-picked tops; the clock grew past the
122 px the alerts panel's fixed top allowed it and buried the speed buttons. Twice more the same
shape of accident happened elsewhere in this file's history — the overlay strip on the tab bar's
right end ate the last tab, and the inspect pane cleared a bar of two rows by assuming a height for
it. Each fix was correct and local, and none of them could answer the question *"do any two panels
overlap at this resolution"*, because the answer lived in a layout engine that only runs inside
Unity with a panel attached.

`HudLayout` is that answer. It is the same arithmetic the sheet applies, written where the fast tier
can run it, and `HudLayoutTests` asks the question directly at every resolution the acceptance
criteria name. `HudGeometryTests` then lays the real HUD out in a real panel and measures the boxes
UI Toolkit actually produced, asserting that each sits **inside** its modelled box. Containment
rather than equality: a conservative model is safe in both directions that matter, and a panel
*larger* than modelled is the failure the pair exists to catch.

### 5.1 The one place the renderer's metrics reach the arithmetic

Every row in this layout declares its own height, which is what makes the model exact — the stores
panel and the clock measured to the pixel on the first run. The text left to size itself came out
**twice the point size**: a 13 px label measures a 26 px line box, not the ~17 the point size
suggests, and most of the difference is leading.

That showed up twice. In the model, the empty inspect pane came out nine pixels taller than
`HudLayout` had allowed it. On the screen, it was worse and only a photograph could have found it:
two labels stacked inside a 38 px header claimed 62 px between them, so the colonist's job line
printed straight through the tab strip under it.

The rule that follows: **every label sharing a column with another declares a `height` and centres
its text in it** with `-unity-text-align: middle-*`, which puts the ink where you asked and lets
the leading fall outside the box. A label alone in a row whose height something else sets needs
nothing. `HudText.LineBoxFactor` records the measured ratio where a future reader will find it, and
1.35 — the reasonable guess — would have been wrong by half a line.

### 5.2 Three things the model got wrong, and the tests found

- **The colonist strip is centred on the screen, not in the gap.** The panels either side are
  different widths — 288 of stores against 322 of rail and clock — so the free span's middle sits
  seventeen pixels left of the screen's middle at 1920. A strip sized to the whole span and then
  centred pokes into the clock column by seventeen pixels however careful the arithmetic looked.
  What it may actually have is twice the smaller of its two clearances.
- **The depth rail is the one region the world sizes, so it is the one that has to give.** Sixteen
  layers at 26 × 16 fits anything; thirty-two at 720p does not. The cells shrink in proportion
  rather than the rail overflowing — and above all rather than the rail *silently losing its last
  layers*, which was a real playtest report on 2026-09-16 and is what
  `HudSmokeTests.EveryLayerHasAStepThatCanBeHit` is there to insist on.
- **The rail runs down to the command bar, not to the bottom of the screen.** The bar is centred and
  wide enough at every resolution this game runs at to reach under the right-hand column.

### 5.3 Coverage

Measured on the logical canvas, which is the honest place to measure it: the panel scales with the
screen, so a panel covers the same *fraction* of a 720p monitor as of a 4K one, and Unity's
match-0.5 scaling keeps the canvas *area* almost constant across aspect ratios too (1663 × 1247 for
4:3 against 1920 × 1080 for 16:9 — 2.073 million square pixels against 2.074). One figure therefore
answers the criterion for every screen.

**Measured at 10.8 to 11.0% at rest** — by `HudGeometryTests`, on the real panel at all three
resolutions — against an 18% ceiling. The "roughly 31% before" figure quoted throughout this
document is the **specification's own**, not a measurement made here: the old HUD had no geometry
model and no test that could produce one, which is most of why this pass exists. The reduction is
therefore real but its size is the specification's claim. The scrims are excluded and
that is a decision rather than an oversight: they are 370 rows between them, they are transparent
gradients whose whole job is to carry text contrast so the panels can stay small, and counting them
would make the target unreachable by construction while measuring the opposite of what the criterion
is about — how much of the board the interface hides.

---

## 6. The command bar

One row. Build filled in accent with dark ink, then Work, Schedule, Research, Colonists, Animals,
Wildlife, Bills, Factions, History as transparent items with a category-outlined chip, then a
hairline and Menu.

**It may not wrap and may not overflow.** Anything that does not fit at the current width moves into
the Menu popup, from the right, so the order on screen never reshuffles as the window is resized —
only its tail shortens. Menu is never dropped, because it is where everything dropped goes. This is
what lets the inspect pane above it stop guessing at the bar's height; the old sheet carried that
coupling in a comment admitting it was one.

The reflow uses the widths UI Toolkit actually laid out, measured once while every item is present
and invisible. Hiding with `visibility` rather than `display` is what makes that possible: a
`display:none` element has no width to read, so the bar would have to be drawn overflowing for one
frame in order to find out that it overflows.

**Every item shows a hotkey, and the panels are on F1 to F9.** That was not the first answer. The
first pass gave each item a letter — W for Work, S for Schedule, A for Animals — and every one of
those is already a camera key: WASD pans, Q and E turn, B cycled the below-slice mode, V the above
one, M, C and X arm the designate tools, R and F move the slice, Space and 1–3 are the clock, Home
recentres. **Five of the eleven clashed and the test written to prevent exactly that passed
anyway**, because its list of reserved keys was written from memory and was missing five of them.

`HotkeyClashTests` now reads the reserved set **out of the source** — every `keys.somethingKey` in
the Presentation assembly — and allows a command's key only in the one file that reads it on its
behalf. It is an unusual shape of test and it is the only shape that can answer the question
without a running game and a person pressing keys.

Function keys are unclaimed, are the convention for top-level panels, and are narrow, which the
overflow budget appreciates. **Build keeps B**, because it is the one item that does something
today and the one a player reaches for without looking; the below-slice cycle gave the letter up
and moved to **shift-V**, beside the above-slice cycle it belongs with. **Escape** opens Menu,
which is the panel Escape already opened.

**A12, the overlay toggles, moved into Menu.** They were ten unlabelled icon buttons at the right end
of the bottom bar, sitting on the tab row and eating its last tab; the acceptance criteria strike out
"overflowing colour chips at the right end of the command bar" by name. They are not information a
player can act on until a channel renders (M4), so they are listed in Menu with their full names and
their reason, which is the catalogue's "disabled with a reason" rule. An unlabelled chip on the bar
was neither disabled nor labelled.

---

## 7. What the panels say now that they did not

- **Stores** counts `n / total`, folds zero-stock rows behind a disclosure, and marks a **falling**
  stock with an amber down-chevron and a `rgba(232,181,92,.09)` row tint. Falling is measured against
  a baseline resampled every ten seconds, not against the previous refresh: the panel refreshes four
  times a second and a hauler picking a stack up momentarily lowers every count on screen, so a
  frame-to-frame comparison would make the whole panel flicker amber all day.
- **Alerts** is a real panel. It raises three conditions the published frame can actually support —
  a colonist starving, a colonist close to breaking, a colony standing idle — leads with the
  actionable clause and trails the detail in dimmer ink, and **hides outright when it has nothing to
  say**. Each condition has hysteresis, for the reason `AlertWatch` already gives about chimes: a
  need sitting on its threshold flaps either side of it for hours of game time. Idle is sustained
  rather than latched, because it is momentary rather than a level.
- **The inspect pane with nothing selected is one dim line**, "Nothing selected", and 41 px tall
  instead of 302. The three sentences it replaces — "click a colonist, an item, or the ground", the
  colony summary, "salvage lies where it fell" — were the interface talking about itself, and they
  were most of why an empty HUD covered a third of the screen.
- **The depth rail** says above-ground in pale and below-ground in dark, which is the one thing a
  depth readout has to say at a glance, and lights the live layer with its number in it.
- **The colonist card** lost its L12 label, because the rail states the layer once and the inspect
  pane states it again for whoever is selected. It gained a rest bar, so all three needs are on it.
  - **Amended 2026-09-17 (owner):** the activity line leads with an icon, so the card answers
    "what is this one doing" as a picture as well as a word. The line is 17 px rather than 16,
    because the row is now sized by the icon rather than by the text, and the gap is 6 px rather
    than a list row's 9, because the card is narrow and the word has to stay a whole word.
    The slot is occupied whatever the job: a key with no art draws the outlined square, as it does
    everywhere else in the HUD, and hiding it would move the word sideways every time a colonist
    changed job.
  - **Amended again the same day (owner): the three need bars come off, and the card is sized by
    what is left.** "Remove the bars from the roster icons and then we can shorten and tighten
    them so we can carry many more — accommodate the longest name possible." A card is now two
    rows, identity and activity, at **106 × 63** against 132 × 86: **1.24× as many colonists on
    the same bar**, and the strip's footprint falls by 47%. The width is not chosen — it is the
    wider of the two rows, measured. `TheCardIsWideEnoughForItsRowsAndNoWider` asks the text
    engine what the longest name the pool can deal and the longest `ui.status` word really draw,
    in the real face at the real size, and bounds the constant on both sides with a figure
    attached. Food, rest and mood are still on the inspect pane for whoever is selected, and the
    alerts panel still raises starving and close-to-breaking off the whole colony — which is the
    argument for taking them off the card: three 3 px bars at a glance told you a colonist existed
    and not much else, and the two regions that answer the same question properly were both built
    after the card was.
  - **And the two bars are docked to the screen edges (owner, same day).** `StripTop` and
    `BarBottom` are 0 where both were `Edge`: the strip and the command bar *bound* the view where
    the stores panel, clock and rail sit *in* it, and a bar with a strip of world under it reads as
    floating rather than as the edge of the screen. The command bar's bottom corners are squared
    for the same reason. `InspectBottom` is derived from the bar's position now rather than written
    down, so the pane follows it.
  - **The strip may run to two rows, where the screen can afford one.** `StripHeightShare` (0.14)
    caps the strip against the viewport's height: **one row at 720p, two at 1080p and above**, and
    it drops back to one at a raised interface scale, because that shrinks the logical canvas. The
    cap is not taste — two full rows are 8.1% of a 720p screen and took the resting HUD to 21.7%,
    over §4's 18% ceiling. The clamp is the part that matters: the strip is the only region with no
    ceiling of its own, so unbounded rows would make every other guarantee here true only for the
    colony sizes somebody happened to try.

---

## 7b. The command bar and its popovers (owner, 2026-09-17)

**The bar is the full width of the screen and sits on its bottom edge.** It was a centred pill as
wide as its items. Full width for the same reason it is docked: it is the edge of the screen rather
than a panel floating near it. Three of its four sides are off the screen, so no corner is rounded
and only the top hairline is drawn (`HudTheme.BarRadius` is 0, `HudLayout.BarFrame` is one border
rather than two). The overflow arithmetic now measures against the bar's own padding rather than
the screen margin, since the bar no longer has a margin — which puts two more items on the bar
before anything goes into Menu.

**One rule for every panel the player opens, rather than three panels each doing their own thing.**

- **A *window*** is a panel you deliberately opened and are looking at, as against a board panel
  you read while watching the world. Every window is **less transparent** than a board panel
  (`HudTheme.PopoverFill`, 0.96 against 0.86) and carries a **close X in its top right**, the
  inspect pane's own control lifted out rather than reinvented. The reason for the fill is not
  taste: the two scrims carry a board panel's text contrast so it can stay light enough to see
  terrain through, but the board behind a window is not being read, and showing it through a list
  of rows is noise on the one surface the player is attending to.
- **A *popover*** is a window raised from a button on the command bar. It is additionally
  **anchored to the left edge of that button** and sits **flush on the bar with no gap**, with its
  bottom corners squared where it meets it. Left-aligned rather than centred, because a menu whose
  left edge lines up with its control reads as belonging to it — and because centring puts a wide
  popover off the screen for the leftmost button and has to clamp anyway, at which point it is
  neither centred nor aligned. The clamp is to the screen rather than to a margin, so a popover
  raised by the rightmost button ends flush with the right edge, like the bar under it.
- **Only one popover is open at a time.** Two raised from the same bar would overlap each other
  over the buttons that raised them, and the player would have no way to tell which of the two the
  Escape they are about to press belongs to.
- **Every window can be escaped.** The Menu popover was the one that could not — it had no place
  in the Escape order and no X, so the only way to shut it was to press the button that opened it.
  The order is now tool → bar popover → settings → open settings.

Build and Menu are popovers. The settings panel is a window but not a popover: it is reached from
Menu *and* from Escape, so there is no one button it belongs over, and it keeps the centring a
settings panel wants.

Where a popover sits is written from code rather than the stylesheet, because it is a fact about
the laid-out bar — the reflow moves buttons as items go into Menu, and the interface scale moves
them again. The arithmetic is `HudLayout.PopoverLeft` and `PopoverBottom`, in the assembly the fast
tier can read; `EveryBarPopoverOpensOverItsOwnButtonAndFlushWithTheBar` measures the realised boxes
under the player loop, and `EveryWindowHasAWayOutThatIsNotTheKeyboard` holds the X rule.

### 7b.1 The rest of the HUD docks too (owner, same day)

**`HudLayout.Edge` is 0 where it was 20.** The strip and the bar were docked the day before, so the
clock sat twenty pixels below a strip beside it that started at zero, and the two read as
misaligned because they were. Stores, the clock column, the depth rail and the inspect pane all sit
on their edges now, and **the corners that lie on a screen edge are squared** — the same rule the
command bar got, for the same reason: a rounded corner against the edge shows a notch of world
through it.

**The breathing room did not go anywhere; it moved inside.** A panel's `Pad` is still twelve on all
four sides, so no text is nearer the screen edge than it was. What is gone is the strip of world
between a panel's border and the edge, which carried no information and cost every corner of the
screen twenty pixels in both directions. It is also forty pixels of strip room, because
`StripRoom` measures from the panels either side.

### 7b.2 The Build palette's two groups

**The cap is the fix, not the width.** The category list was `flex-grow: 1`, so it took every pixel
the panel had and the tools got the remainder — the group the player is actually reaching into
shrank as the group above it grew. `BuildCatRows` is 2 and `BuildCatHeight` caps the group; anything
past two rows scrolls, which is what the scroll view was put there for and never reached. A chip has
an explicit 30 px height, because a count of rows is meaningless if a row's height is
content-driven.

**Wider is the cheap half.** `BuildWidth` 420 → 560: ten category chips with their full words come
to about eight hundred pixels of chip, so at 420 they wrapped to five rows. It costs nothing now
that a popover is anchored to its button rather than centred on the screen.

**And the palette opens on its first category**, rather than on an empty second group that only
fills once the player has guessed the chips above are clickable.

---

## 7a. The camera keys that changed with this pass

- **Q and E rotate freely while held** (owner, 2026-09-16), at `rotateSpeed` 90 degrees a second,
  multiplied by shift like every other camera speed. They used to add or subtract ninety degrees on
  the frame they were pressed, which is the genre's convention and assumes a board that reads the
  same from four sides. This one does not: it is layered, the slice is cut at an angle, and a wall
  or an outcrop hides different things at fifty degrees than at ninety. A held key lets the player
  stop wherever the view is actually clearest. The target angle is driven rather than the yaw
  itself, so the same smoothing that carries a mouse orbit carries this and the two cannot fight
  over who owns the angle.
- **Shift-V cycles the below-slice mode**, which was B. See §6.

## 8. Escape, and the panels that open over the board

The unwind order is one rule in one place — `SettingsDirector.Escape`, decided in the fast tier:

1. the tool in the hand,
2. **the Build palette**,
3. the settings panel,
4. otherwise, open the menu.

The palette is new to that order. It used to be a column pinned to the left edge and permanently
open, competing with the stores panel for a column that could not hold both; as a transient panel
over the board it costs nothing when it is shut, which is most of the time.

---

## 9. What is not done

- **Nobody has looked at it.** No test can say whether this reads well, and the only way to find out
  is to press Play in `Play.unity`. Every number above is an argument until then.
- **No real icon art**, so every game glyph is an outlined square. That is ADR 0007's pipeline and
  the owner's eight sheets, and it is the single biggest visual gap.
- **No backdrop blur**, because UI Toolkit has none (§1).
- **True 500/600/700 weights** wait on static Archivo Narrow files, which Google's repository does
  not publish (§2).
- **The interface scale is a ladder of six rungs, not a slider**, and there is no live preview of
  what a rung costs in coverage beyond the tooltip's wording. Both are fine for six rungs and would
  not be for a continuous control.
- **The build palette's tools, the inspect pane's tabs beyond Needs, and nine of the eleven
  command-bar items** are still disabled with a reason, as they were. This pass changed how the HUD
  looks and measures, not what the game can do.
