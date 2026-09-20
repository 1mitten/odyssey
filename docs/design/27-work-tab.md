# The Work tab — the priority assignment grid

**Status:** design settled from the owner's supplied mockup, 2026-09-20; branch `claude/happy-tesla-2onz0q`.
**Read first:** `10-ui-panel-catalogue.md` §B2 (which already specified this panel), `14-hud-layout.md` (the command bar and the regions), `09-ui-and-input.md` §7a (the placeholder-icon naming rule, which decides the column headers), `15-skills.md` (why hauling is not a skill), `17-rates-and-stats.md` (what a skill level buys).
**Mockup:** `docs/reference/mockups/work-v1.html`.

## 1. What this is, and what already existed

The owner supplied a full visual specification for a dense priority grid — colonists as rows, work
types as columns, four independent signals in each small cell — with the instruction that it is a
mockup and that **our own icons, type and tokens are pointed at it rather than its invented ones**.

Three things already existed and this design is bound by them rather than starting fresh:

| Already decided | Where | What it means here |
|---|---|---|
| The panel itself, down to the intent it emits | `10-ui-panel-catalogue.md` §B2 | `SetWorkPriority(pawn, workType, priority)` is the name. The catalogue also says 50 × 25, realised rows under forty, and *"column headers are work-type icons with a tooltip"* — which this design overturns, §4 |
| The priority model | `Pawn.WorkPriorities`, `JobSystem` | A `byte` per work type per pawn, 0–4, default **3**, saved and **hashed**, and the scan already runs `for priority in 1..4`. The mockup's "assume the priority model exists" is true, and 0 already means never |
| Passion, and the skill ramp behind it | `Pawn.Passions`, `Skills.xml` | Three states — none, minor, major — rolled once at spawn from the pawn's own `RollSeed`. The mockup's single and double flame map onto them exactly |
| The tab's slot and its key | `HudCommands.Order` | `ui.tab.work`, **F1**, currently carrying the reason *"the work grid arrives with M7"*. This design is what removes that reason |

**So nothing here invents a panel.** It draws one the catalogue specified in a paragraph, and
settles the dozen questions a paragraph cannot.

## 2. The re-pointing table

The mockup's tokens are not ours. Every one of them is replaced by the token this project already
owns; where the two disagree, ours wins, because a panel that carries its own palette is the fault
`OrderColours` was written to end.

| Mockup says | We use | Why |
|---|---|---|
| `rgba(0,0,0,.72)` fill | `HudTheme.PanelFill` — `#0c1014db` | One panel fill, and it is not black: the HUD's panels are a cool near-black, which sits differently on a green world |
| square corners | `HudTheme.PanelRadius` = **5** | Our panels are not square. The mockup's claim that square "matches every other panel in the game" is true of *its* game, not ours |
| `1px solid rgba(255,255,255,.35)` border | `HudTheme.PanelBorder` — `#ffffff21` | Ditto. `.35` is our `TextFaint`, which is an ink, not a border |
| `#7fd0e0` accent | `HudTheme.Accent` — `#6fd3e3` | Two cyans that differ by three units is exactly the drift the style-sheet parity test exists to catch |
| `rgba(127,208,224,.18)` active fill | `HudTheme.ActiveTabFill` — `Accent` at `.12` | Already the token for *the active one of a set* |
| 21 / 17 / 13 / 18 / 19 / 15 type | `HudType`'s six steps — 11 / 12 / 13 / 14 / 19 / 24 | The mockup invents six sizes; we have six, tested, and a seventh would be a second scale |
| Segoe UI / system sans | **Archivo Narrow**, and **IBM Plex Mono for the digit** | §2a. This is not cosmetic: the HUD's face is *narrow*, which is the whole reason twenty-two labelled columns fit at all |
| six tabs in a 70px bar | our **eleven**-item command bar at `HudCommands.ItemHeight` + padding = **48px** | The bar exists, is tested for overflow, and already holds Work on F1 |
| thirteen invented work types | our **twenty-two** `ui.work.*` | §4 |
| `#f0a020` flame | `HudTheme.Warn` — `#e8b55c` | The HUD has one amber |
| `#6fcf7f` ✓ / `#a8554f` ✕ | `HudTheme.Good` / `HudTheme.Bad` | Ditto, one green and one red |

### 2a. Type, role by role

| Mockup role | Its setting | Our role | Our setting |
|---|---|---|---|
| panel title "WORK" | 21 / 700 | `HudTextRole.Name` | 19 / 600 |
| subtitle | 17 / 400 | `HudTextRole.Meta` | 12 / 400 — it is literally *"the small qualifying line"*, which is what `Meta` is for |
| column header label | 13 / 400 | `HudTextRole.Body` | 13 / 400 — the one exact match |
| colonist name | 18 / 400 | `HudTextRole.Row` | 14 / 500 — this is a list row and our list rows are `Row` |
| priority digit | 19 / 700 | `HudTextRole.Name`, **forced mono** | 19 / 600, IBM Plex Mono with tabular figures |
| legend / footnote | 15 / 400 | `HudTextRole.Meta` | 12 / 400 |
| "COLONIST" header cell | 15 / 600, `.06em` | `HudTextRole.PanelLabel` | 11 / 600, letter-spaced, upper-cased — our existing panel-label idiom |

**The digit is mono and the mockup did not say so.** Four glyphs are compared down a column and
across a row, and a proportional `1` is narrower than a `4`, so in a proportional face the column of
digits is ragged and a scan down it reads as noise. `HudType.Mono` already exists for the clock for
the same reason.

## 3. Geometry, re-derived

The mockup's numbers were measured for its own 13-to-19px type. Ours is smaller and there are
twenty-two columns rather than thirteen, so every number is re-derived and none is copied.

```
column pitch       34px        cell            28 × 28, centred (3px each side)
row height         30px        portrait        24px square
header band       104px        left column    192px, fixed
panel width       192 + 22 × 34 = 940px
row divider       1px HudTheme.Divider          (#ffffff17)
section divider   1px HudTheme.PanelBorder      (#ffffff21)
```

- **Left column 192** = 10 pad + 24 portrait + 8 gap + 140 name + 10 pad. Derived, not chosen: 140px
  at `Row`'s 14px and `HudCommands.UiAdvance` (0.50) is twenty characters, which clears the longest
  name in `colonist-names.csv` plus a surname.
- **Row height 30** rather than `HudLayout.RowHeight`'s 29, because a 24px portrait wants an even
  3px above and below and 29 cannot give it. This is the one place a hairline of consistency is
  traded for arithmetic that closes; it is recorded rather than hidden.
- **Header band 104** = 72 label rise + 4 gap + 28 icon tile. The 72 is computed in §4.
- **940 wide** against the 1920 reference is 49% of the screen, docked bottom-left above the command
  bar. It never scrolls horizontally at the reference size — the horizontal scroll the mockup
  designs for is the 1280 case and the day the work list grows past twenty-two.

## 4. The column headers: twenty-two, rotated, labelled

### 4a. Twenty-two columns, not thirteen

`docs/design/icon-keys.csv` already carries **twenty-two** `ui.work.*` keys with registry names,
wiki entries and icon-map rows. They are canon. The mockup's thirteen are invented names for
another game and three of them (Farming, Cleaning, Animals) are not even spelled the way ours are
(Growing, Cleaning, Handling).

**Four of the twenty-two are simulated today**: `Work_Construction`, `Work_Cutting` (Chopping),
`Work_Mining` and `Work_Haul`. The other eighteen have no work giver and no Def.

This is exactly the situation `SkillCatalogue` met in 2026-09-17 and the owner's answer there
governs here: **show the design's list, and draw what the simulation cannot run as unavailable with
the reason beside it**, because *"a tab that hides the shape of the game until the last system
lands"* is worse. So all twenty-two columns are drawn and eighteen are **not built yet**.

**Not built yet is a third state and must not be drawn as the second.** An incapable cell (§6.1)
and a column that does not exist yet are different facts and greying both identically would teach
the player that eighteen of their colonists' columns are a disability. So:

- an **unbuilt column** dims its whole header — icon and label at `HudTheme.TextFaint` — and lays
  one `rgba(255,255,255,.03)` wash down the entire column, with the header's tooltip carrying the
  reason (*"salvaging arrives with M5"*). Its cells are empty: no border, no digit, no flame. It
  reads as *one* absent thing, top to bottom, which is what it is.
- an **incapable cell** is per-cell, bordered, and carries the em-dash. §6.1.

### 4b. Rotated labels, and why this overturns the catalogue

`10-ui-panel-catalogue.md` §B2 says the column headers are *"work-type icons with a tooltip"*. That
was written before the icon library was understood. **It is overturned here**, by a rule that
outranks it: `09-ui-and-input.md` §7a, the owner's decision of 2026-09-16, that **while the icons
are placeholders a control is named by its full word, never by an abbreviation**. An icon-only
header is the strongest form of abbreviation there is, and `ui.work.firefighting` today is *"spray
canister, read as an extinguisher"* at **low** confidence in `icon-map.csv`. Twenty-two low
confidence glyphs in a row, each the only thing naming a column, is a panel nobody can use.

So the header carries **both**, and the mockup's rotation is the mechanism that lets it:

```
   Firefighting                     the label, rotated, anchored bottom-centre
      ╲
       ▢                            a 28 × 28 icon tile at the foot of the band
```

The day the real sheets land, the label can be reviewed for removal. That is the density question
this panel exists to ask, and it is on the playtest queue as such.

### 4c. The angle is arithmetic, not taste

A rotated label's horizontal footprint is `textWidth × cos θ`. Two adjacent labels collide the
moment that exceeds the column pitch — and unlike most layout faults this one gets *worse* toward
the left of the panel, so it is invisible in a screenshot of the right-hand columns.

Our longest label is **Firefighting**, twelve characters. At `Body`'s 13px and
`HudCommands.UiAdvance` (0.50) that models **78px**:

| pitch | angle it demands | our angle | footprint | margin |
|---|---|---|---|---|
| 34px | > 64.2° | **−66°** | 31.7px | 2.3px |

The mockup's −62° would project **36.6px** at our pitch and overlap; its own −45° warning is right
in principle and wrong by 20° for our numbers. **The rise is `sin 66° × 78 = 71.3px`**, which is
where the 72 in the header band comes from.

`WorkGridLayout.LongestLabelFits` computes this and `WorkGridTests` asserts it, so a work type
named *"Firefighting and rescue"* fails the fast tier rather than the screenshot.

### 4d. Hue per column

The mockup gives each work type its own hue. **We already have that mechanism and it is not
per-work-type**: `HudTheme.CategoryOf(key)` maps a key to one of a handful of `HudCategory` values
and the rule from the interface spec is narrow on purpose — *category colour lives in the icon
stroke, never in a filled background*. Twenty-two bespoke hues would be a second colour system in a
project that has just finished merging two of them (`OrderColours`, 2026-09-20).

**So the icon stroke takes its category colour from `HudTheme.CategoryOf`, and no work type gets a
hue of its own.** The mockup's thirteen hex values are not carried over. If the owner wants the
columns individually hued after seeing it, that is a `HudCategory` decision and belongs in
`HudTheme` beside the others, not in this panel.

## 5. Rows

One per colonist, in `RosterModel`'s order, so that the grid and the roster strip agree about who is
third. Selecting a row selects that colonist everywhere (the roster strip pages to them, the inspect
pane opens) — the `RP` work already made that a two-way binding and this reuses it rather than
inventing a second selection.

Portrait at 24px is the cached appearance render, the same one the roster card and the setup card
draw. **A colonist is never identified by name alone in this game**, because the setup screen taught
that three names are three strings and three faces are three people.

## 6. The four signals in one cell

### 6.1 Capability — inert, and inert at the data layer

Nothing in the simulation models incapability today: there are no traits and no health model, so
**every capable-today answer is `true`** and the em-dash appears only in the mockup's sample data.
The channel is built anyway, because building it later means finding every place that assumed a
digit.

The mockup's rule is kept verbatim and it is the right one: **if `!capable`, priority and passion
render as empty strings at the model**, not as CSS opacity over a digit that is still there. A
capability expressed as styling is a capability that leaks — into a tooltip, into a screenshot,
into a copy-paste.

| | |
|---|---|
| fill | `rgba(255,255,255,.035)` |
| border | 1px `rgba(255,255,255,.07)` |
| glyph | `—` at `rgba(255,255,255,.35)` — **3.24:1**, deliberately under AA |
| hover | none. No cursor, no hit target |

**The em-dash is the one glyph in this panel that does not meet 4.5:1, and that is the design.** It
is not information to read; it is the absence of information, and it meets the 3:1 that a non-text
graphic owes. The digits meet 4.5:1 and §9 measures them.

### 6.2 Proficiency — the border, and only the border

A **2px border** on a five-step ramp. The mockup's ramp is replaced by one built out of our own two
accents, so that three of the five steps are tokens we already ship:

| Skill | Band | Colour | Where it comes from |
|---|---|---|---|
| 0–3 | novice | `#a8524a` | `HudTheme.Bad` darkened — the dull red end |
| 4–6 | apprentice | `#8f7a5e` | the muddy midpoint between Bad and Warn |
| 7–10 | competent | `#c7d0d6` | `HudTheme.TextPrimary` dimmed — neutral, the "no opinion" step |
| 11–14 | skilled | `#e8b55c` | **`HudTheme.Warn`, unchanged** |
| 15–20 | master | `#f5c94a` | `Warn` brightened — the one colour on the panel that is allowed to shout |

**Never tint the cell fill by skill.** Two things fighting for the same 28px square is how the
mockup's own warning puts it, and it is right: the fill belongs to the priority state.

**Hauling has no border ramp, ever, and that is a decision not a gap.** `WorkTypes.xml` gives
`Work_Haul` no `rateSkill` — *"hauling has no speed stat at all, here and in the reference"* — so
there is no level to read. Its cells take a 1px `HudTheme.TextFaint` border instead of a 2px ramp
one, and the legend says why in four words. A ramp colour invented for hauling would be a lie told
in a colour the player has learned to trust.

### 6.3 Priority

**Detailed.** A bold mono digit 1–4, brightness descending with importance:

| | ink | on the assigned fill |
|---|---|---|
| 1 | `#eef3f6` (`TextPrimary`) | 17.95:1 |
| 2 | `#d4dde3` | 14.57:1 |
| 3 | `#a4b0b8` | 9.06:1 |
| 4 | `#7c8990` | **5.58:1** |

An **assigned-but-blank** cell — capable, priority 0, "never" — is bordered so the skill still
reads, with a slightly *lighter* fill than an assigned one so that a blank column does not read as a
hole: `rgba(0,0,0,.42)` against `rgba(0,0,0,.55)`.

**The default is 3, not blank**, because `Pawn` initialises every priority to 3. So a new colony
opens this panel on a grid that is entirely `3`s in four columns, which is correct and is the thing
the player then edits.

**Simple.** The same cell swaps the digit for `✓` (`HudTheme.Good`) or `✕` (`HudTheme.Bad`).
Priority > 0 is a tick. Setting a tick writes **3**, the default, and clearing writes 0, so a
player who never opens Detailed can still use the panel and a player who switches back finds their
own numbers where they left them. **The mode is presentation state**: it is not saved into the
colony, not hashed, and lives in the view state beside the camera.

### 6.4 Passion

One or two `HudTheme.Warn` flames, 9 × 11px, in the **top-right corner**, from
`odyssey.pawn.skill.<skill>.passion` — minor is one, major is two, none is nothing.

**Deliberately independent of the border**, which is the mockup's own best line: a colonist can love
a job they are bad at, and that pair — dull red border, double flame — is the single most useful
cell on the panel, because it is the colonist to train.

Hauling shows no flame, for the reason in §6.2: no skill, no passion.

## 7. The header bar and the legend

**Header.** `Registry.Label("ui.tab.work")` — *"Work"* — at `Name`; the subtitle at `Meta` counts
the colonists and states the rule (*"higher priority runs first"*), because the one thing a
priority grid must not make a player guess is which end is urgent. Right: the word *Priorities*, the
two-state **Simple / Detailed** switch as one bordered box split in two, and the 32px close button.

**Legend**, along the foot, above the command bar: the five skill swatches with their ranges; one
flame *interested* and two *passion*; the incapable swatch; and the hauling note. Right-aligned:
*"1 highest · 4 lowest · blank means never"*. In Simple the first group is replaced by ✓ *will do*
and ✕ *won't do*, and the sentence *"border colour still shows skill · flames still show passion"*
is added — because the whole risk of a simple mode is that it looks like the other information went
away.

**None of these words are in the naming registry, and that is a deliberate gap, not an oversight.**
`icon-keys.csv`'s namespace is derived from the key's first two segments, so a
`ui.work.grid.simple` would file itself in the wiki as a *work type* and appear in the content wiki
beside Firefighting. Registry-owning panel chrome needs a `ui.panel.*` namespace added to
`tools/wiki/build_wiki.py`'s `SECTIONS`, which is a content decision the owner has not been asked.
**Open question OQ-W1**, §11.

## 8. Interaction

| Gesture | Does |
|---|---|
| Click | cycle 1 → 2 → 3 → 4 → blank → 1 (Detailed); toggle (Simple) |
| Right-click | cycle backwards |
| Drag | paint the cell you started on across everything the drag touches |
| Shift-click | set the whole column to that value |
| Hover | the cell's own sentence: *"Mira · Mining · priority 2 · level 7 · minor passion"* |

All four are `10-ui-panel-catalogue.md` §B2's own list except the right-click, which is added
because a five-state cycle you can only walk forwards is four clicks to undo a mistake.

**Every change is a `SetWorkPriority` intent** and lands while paused — it writes state the player
authored and needs no system to finish it, which is `PausedIntents`' own test.

## 9. Acceptance

The owner's list, answered against our numbers:

| Criterion | Answer |
|---|---|
| left edge at x = 0, bottom flush to the bar | **Adapted.** Our command bar is 48px, not 70, and our panels have a 5px radius and all four borders. The panel sits **on** the bar with no gap; it does not pretend to be part of it |
| no two header labels overlap at any resolution | `cos 66° × 78 = 31.7 < 34`. Asserted by `WorkGridTests.LabelsClearTheirNeighbours` |
| every label fully above its icon tile | the band is `72 + 4 + 28`; the label's box ends 4px above the tile |
| incapable cells contain exactly one glyph | asserted at the model: `!capable` ⇒ priority and passion are empty |
| all five tiers and both flame states in the sample | the mockup's sample data, and `WorkGridTests` walks the bands |
| switching mode changes only the glyph | the mode is read at the glyph and nowhere else; no geometry reads it |
| left column fixed under horizontal scroll | `LeftColumn` is outside the scroller |
| priority digits ≥ 4.5:1 | 17.95 / 14.57 / 9.06 / **5.58**. The "4" is the one to check and it passes |

Two criteria are **deliberately not met**: square corners and the removed borders, both of which
would make this the one panel in the game that is shaped differently from the others. §2.

## 10. What is built in this pass

| Layer | File | State |
|---|---|---|
| Design | this file | done |
| Mockup | `docs/reference/mockups/work-v1.html` | done — all twenty-two columns, the real icon mapping, both modes |
| Model | `Assets/Odyssey/Hud/WorkCatalogue.cs` | the twenty-two, four live, eighteen with reasons |
| Model | `Assets/Odyssey/Hud/WorkGridLayout.cs` | the geometry above, and the rotation constraint |
| Model | `Assets/Odyssey/Hud/WorkBands.cs` | the skill ramp and the priority inks |
| Model | `Assets/Odyssey/Hud/WorkGridModel.cs` | rows from the snapshot, the cycle, the intent payload |
| Seam | `Assets/Odyssey/Sim/Pawns/WorkAspects.cs` | priority and capability published per pawn per work type |
| Seam | `IntentKind.SetWorkPriority` and its handler | the write half |
| Tests | `Assets/Odyssey/Tests/Hud/WorkGridTests.cs` | the constraint, the bands, the cycle, the inert cell |

**Not built: the Presentation draw.** `HudShell.Work.cs` is the next unit and the mockup is its
specification. It is deliberately last because the fast tier does not compile Presentation, so it is
the half that cannot be proven without a Unity run on the owner's machine.

## 11. Open questions

- **OQ-W1 — should panel chrome be registry-owned?** *Simple*, *Detailed*, *interested*, *passion*,
  *incapable*, *will do*, *won't do* and the two footnotes are player-facing strings written in C#.
  Doing it properly needs a `ui.panel.*` namespace in the wiki builder. §7.
- **OQ-W2 — do twenty-two columns read, or do eighteen dead ones cost more than they teach?**
  The `SkillCatalogue` precedent says show them. That precedent was set at thirteen rows with three
  live, not twenty-two columns with four. Worth a look at the screen before it is settled.
- **OQ-W3 — hauling's borderless cell.** It is honest, but a column that is visibly different from
  its twenty-one neighbours may read as broken rather than as skill-less.
- **OQ-W4 — where does the panel go when the work list grows?** Twenty-two fits 940px. The
  catalogue budgets twenty-five, which is 1042px, still fine. Thirty is 1214px and wants the
  horizontal scroll the left column is already frozen against.
