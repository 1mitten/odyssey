# The Work tab — one table: what they do, and when

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
  **This is the work half alone and it is no longer what the panel is**: §12 folded the day in
  beside it and §12b has the combined 1,756. The 1280 case §12b calls hypothetical is not — see
  §15b, which is what makes the panel narrower than the table it holds.

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

**The default is 3, not blank**, because `Pawn` initialises every priority to 3 — but *"the grid
opens on a colony of threes"* is **wrong**, and it was written into this document before the code
was checked. `ColonyScenario.AssignTrade` deals the first `miners` colonists **Mining 1 / Chopping
3** and everybody else the reverse, and its own comment says why: *"until the player can set
priorities from the interface the scenario has to do it, exactly as it has to give the first
orders."*

So **the Work tab opens on a visible division of labour**, not a blank slate, and that is a better
first screen than the one this design assumed: the player's first look at the panel shows them a
choice somebody already made on their behalf, in the two columns they can actually feel. The bare
test board has `miners = 0` and does open on threes, which is why a test that only ever saw `Bare()`
would have taught the next session the wrong thing;
`WorkPriorityTests.ThePlayedScenarioDealsADivisionOfLabourAndTheGridWillShowIt` pins the real one.

**And `AssignTrade` is now on notice.** It exists only because nothing could set a priority; this
panel is the thing it was waiting for. Removing it is not this unit's business — it would move the
state hash and change how every existing colony starts — but whoever does should know the panel
replaced its reason, and that a colony of identical colonists all walking to the same trees is what
it was written to prevent.

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

| Draw | `Assets/Odyssey/Presentation/Ui/HudShell.Work.cs` | the panel, built the way the debug menu is |
| Draw | `HudGlyphKind.Flame` | the one filled organic shape in the glyph set |
| Seam | `WorkDirector`, `HudDirectors.Work` | open, and which reading |
| Seam | `HotkeyAction.WorkTab`, `HudKey.F1`, `ui.keys.worktab` | the cap the bar has advertised since it was built, made real |
| Seam | `HudCommands` | Work loses its reason and goes live |

**The tab is reachable and functional as of 2026-09-20**: F1 or the bar item opens it, a click
cycles a priority, a right-click cycles back, shift sets the column, and every change is a
`SetWorkPriority` intent that lands while paused.

### 10a. Two things about how it is drawn

**Almost every style is inline rather than in `Hud.uss`, and that is a deviation with a reason.**
Half of this panel's appearance *is* data — the border is the skill band, the ink is the priority,
the fill is whether the cell is assigned — so it could never live in a stylesheet. Putting the other
half there would split one cell's appearance across two files, and it would put the untestable half
of this unit in the file that a parse error takes down wholesale. The colours are still never
literals: they come from `WorkBands` and `HudTokens`. If the panel settles, the static furniture is
worth lifting into `.work__*` rules.

**`HudKey` gained its first function key.** It was a closed set that deliberately excluded F1–F12,
with the comment *"the function keys the command bar has promised to panels"* — a promise nothing
had ever kept. F1 is bindable now because F1 has a panel; F2–F9 stay out until theirs arrive, so an
unbindable key is always one with nothing behind it. `HotkeyClashTests` carried the matching
assumption (*"F1 to F9, Esc: keys the map will not bind"*) and **failed correctly** when the
assumption went stale; its rule is now a table of commands whose cap is a real binding, which is
what the remaining eight rows join.

## 11. Open questions

- **OQ-W1 — should panel chrome be registry-owned?** *Simple*, *Detailed*, *interested*, *passion*,
  *incapable*, *will do*, *won't do* and the two footnotes are player-facing strings written in C#.
  Doing it properly needs a `ui.panel.*` namespace in the wiki builder. §7.
- **OQ-W2 — do twenty-two columns read, or do eighteen dead ones cost more than they teach?**
  The `SkillCatalogue` precedent says show them. That precedent was set at thirteen rows with three
  live, not twenty-two columns with four. Worth a look at the screen before it is settled.
- **OQ-W3 — hauling's borderless cell.** It is honest, but a column that is visibly different from
  its twenty-one neighbours may read as broken rather than as skill-less.
- **OQ-W5 — drag-paint is not built.** §8 lists it and the catalogue's §B2 asks for it; click,
  right-click and shift-click-column are in. A drag needs pointer capture across cells and it is the
  one gesture whose absence a player notices only when setting a whole colony at once, so it waits
  for a verdict on whether the other three are enough.
- **OQ-W4 — where does the panel go when the work list grows?** Twenty-two fits 940px. The
  catalogue budgets twenty-five, which is 1042px, still fine. Thirty is 1214px and wants the
  horizontal scroll the left column is already frozen against.


## 12. The schedule half (2026-09-20)

The owner supplied a second specification folding the Schedule tab into this one: **one tab, one
table, one frozen column of names** — the priority grid on the left, the twenty-four-hour band on
the right. Schedule leaves the command bar and does not go anywhere else.

### 12a. Why one table rather than two tabs

Two tabs are two answers to *what is this colonist doing*, read one after the other and held in the
head in between. The combined row removes that comparison: **one row is one colonist's whole day.**
That is the whole argument, and it is why the two halves must share a pitch rather than merely sit
beside each other.

### 12b. It fits, and only because our type is narrow

The supplied spec sizes everything at a 40px pitch with thirteen work columns. Ours are
twenty-two, and at 40px the combined table is **2,026px** — wider than the 1920 reference, so it
could not be drawn without either dropping columns or scrolling from the first frame.

At our existing **34px** it is **1,756px**:

```
192 name  +  22 × 34 work (748)  +  1 seam  +  24 × 34 hours (816)   =  1,757
```

which is 91% of the reference and leaves the horizontal scroller as a safety valve below it rather
than a permanent condition. `WorkGridLayout.HourPitch` is defined *as* `Pitch` rather than as 34, so
widening one half cannot silently desynchronise the two, and `ScheduleGridTests` asserts both the
equality and the total.

### 12c. What a colonist actually carries

| | |
|---|---|
| State | `Pawn.ScheduleHours`, a `byte[24]` of `ScheduleHandle` values |
| Default | sleep 0–5, anything 6–8, work 9–17, recreation 18–21, sleep 22–23 |
| Saved | yes — **save format 7**, read behind `FormatVersion >= 7` so older saves take the default day |
| Published | `odyssey.pawn.schedule.h00` … `h23`, one aspect an hour |
| Written by | `IntentKind.SetScheduleBlock` (`A` pawn, `B` hour, `C` block), applied while paused |
| **Hashed** | **no, deliberately — §12d** |

**Twenty-four aspects a colonist, not three packed ints.** That is eight times what any feature has
asked of this mechanism before, and the answer is the one `SkillAspects` already gave at one eighth
the size: packing would save twenty-one rows and cost the reader a decode it could get wrong. The
buffer is reused, so a steady-state publish still allocates nothing. If the scale target ever makes
41 rows a colonist matter, this is the first place to look and the packing is still available.

### 12d. Nothing obeys it yet, and that is why it is not in the hash

**The schedule is authored, saved, published, editable and drawn — and read by no system.** The hour
a colonist sleeps is still decided by their rest need.

It is therefore **deliberately outside the state hash**, on exactly the test the saved view passes:
*a value no system consults cannot affect a tick*. Hashing it now would move all six golden numbers
for a change that alters no behaviour, and the honest version of that trade is to leave it out until
it means something. `ScheduleTests.EditingTheDayDoesNotMoveTheStateHash` is the assertion, and it is
written so that **the day somebody makes the job system obey the schedule, that test fails** — which
is the signal to put it in the hash and re-bake the goldens once, with a sentence.

**The panel says so.** The footnote reads *"one row is one colonist's whole day · colonists do not
follow the schedule yet"*. A schedule you can paint that quietly does nothing is the worst outcome
here; a schedule you can paint that says it does nothing yet is a staged delivery.

**The default day is a real day rather than twenty-four grey hours.** Defaulting everything to
*Anything* would have been the literal truth of a schedule nothing reads — and a panel nobody could
learn to read, since every row would be one flat band. The default is the shape the colony will keep
when the schedule starts governing, so the picture is not a lie about the future, only about the
present, which the footnote states.

### 12e. The six blocks, and the HUD's first categorical scale

| Block | Colour | Where it comes from |
|---|---|---|
| Anything | `#4e4e50` | neutral, and the least interesting thing on the row: it is the absence of a decision |
| Work | `#e8b55c` | **`HudTheme.Warn`, unchanged** |
| Sleep | `#323fa0` | deep blue, saturated rather than dark — night is a third of the table and a near-black band reads as a hole in it |
| Recreation | `#4f9a63` | |
| Eat | `#d9782a` | |
| Meditate | `#7a4fa8` | |

**This is the HUD's first categorical colour scale**, and that is why it has its own file. Every
other colour here is a *signal* — good, bad, warn, accent — carrying meaning by intensity, and six
nominal categories cannot come out of four signal tokens without two colliding.

**Distinctness is measured, not assumed.** These sit edge to edge in an unbroken band, which is a
harder test than two chips in a legend. The first pass put **Anything 77 channel-points from
Sleep** — a flat grey against a dark indigo, which is precisely the pair a player has to separate at
a glance in a night row. Both moved; the closest pair is now **96**, and every block is at least
that far from `HudTheme.Accent`, which is drawn *over* them as the now-line.

### 12f. The now-line

A 2px `Accent` rule at the centre of the current hour's column, from `GameClock.HourOfDay` — a real
clock, not an invented one. **Positioned against the schedule container and never the panel**: the
spec warns about this and it is right, because measuring from the panel puts the line one frozen
name column out, landing it on a different hour and reading as a bug in the clock rather than in the
layout. `WorkGridLayout.NowLineCentre` is the one owner of that arithmetic and the tests pin it.

### 12g. One vocabulary across both halves

Click cycles, right-click cycles back, shift paints the column. The same three gestures answer a
priority cell and an hour block, so one row is one vocabulary rather than two.

**The one deliberate difference:** a work cell can be inert (incapable, or a column the simulation
does not run); **an hour never can.** Nobody is incapable of a time of day. Copying the work half's
guard across would have made a colonist who cannot mine also unable to be sent to bed, so
`EveryHourIsClickableBecauseNobodyIsIncapableOfATimeOfDay` pins it.

## 13. What is built, and what is still owed

| Layer | State |
|---|---|
| Both halves draw, share a pitch and a frozen name column | **in** |
| Schedule saved (format 7), published, editable, paused-safe | **in** |
| Schedule obeyed by the job system | **not built** — §12d, and it is the unit that re-bakes the goldens |
| Drag-paint across cells | **not built** — OQ-W5 |
| Schedule presets (a day shift, a night shift) | **not built** — OQ-W6 |

## 14. Open questions added by the schedule

- **OQ-W6 — presets.** Painting a night shift is twenty-four clicks, or one shift-click per hour.
  A row of preset buttons is the obvious answer and it is also the obvious thing to get wrong
  before anybody has used the grid in anger.
- **OQ-W7 — is 1,756px too wide to read?** It fits, which is not the same as being comfortable.
  If the answer is no, the live-four switch from the mockup is the cheapest lever.
- **OQ-W8 — does the default day mislead while nothing obeys it?** The footnote says the schedule
  is not followed yet, but a legible day is a more convincing lie than a grey one.

## 15. What the review found, 2026-09-20

The branch was reviewed on a worktree before its first play. Seven corrections; five of them are
faults a playtest would have reported, one is a fault that was already on `main`, and one is a rule
this panel was breaking without knowing there was a rule.

### 15a. Simple mode drew nothing at all

**The single worst thing in the branch, and no test could see it.** The tick and the cross were the
characters U+2713 and U+2715 in a `Label`. Archivo Narrow has neither in its `cmap` and IBM Plex
Mono has only the tick, so the legend drew two blanks and every *won't do* cell drew one. The whole
of Simple mode was empty boxes.

They are drawn now — `HudGlyphKind.Check` and `HudGlyphKind.Cross`, on the same 24-unit grid as
every other chrome icon, which is what this project does with icons anyway and should have been the
first answer. `WorkCell.Mark(mode)` is the model's decision and `WorkMark` is its name, so the
shell is told *which* mark rather than *which character*, and the tests assert the mark.

**The guard matters more than the fix.** `HudFontTests.EveryCharacterTheHudWritesExistsInBothFonts`
reads both `.ttf` cmap tables in the **fast tier** and fails on any non-ASCII character in a HUD
string literal that either face cannot draw. On its first run it found the same character already
shipped on `main`, in the bed-owner picker (`HudShell.Inspect.cs`, PR #141): the mark that says
*this is her bed* has been an empty column since it was written and had never been played. That one
is drawn now too. `docs/bug-patterns.md` P10.

### 15b. The panel hung off the right of any screen narrower than about 1,780

`CombinedWidthFor(22)` is 1,756 and the panel is **absolutely positioned from the left edge**, so
the width was not a request the layout could refuse — below roughly 1,780 the schedule half was
simply past the right of the window, out of reach of the horizontal scroller sitting inside the
panel for exactly this case. The first fix capped the panel at 96% of the screen and handed the
overflow to that scroller.

**That fix is gone and §16 is what replaced it.** The cap was the wrong shape of answer, and the
owner said so: a percentage cap makes the panel's width a function of the window, which is a
control that changes size under the player. Pagination fixes the width instead.

### 15c. Escape did not close it

*"Every window can be escaped"* (owner, 2026-09-17) is a rule about windows, not about the windows
that existed the day it was written, and the Work tab had joined the game without joining the rule:
an X and F1 shut it and nothing else, and Escape reached past it to open the settings panel over
the top of it. `EscapeAction.CloseWork` is now a rung on the ladder, between the palette and the
settings panel. `DirectorTests.EscapeClosesTheWorkTabToo`.

### 15d. Build and Work drew over each other

`OnWorkChanged` closed the Build palette when the tab opened; nothing closed the tab when the
palette opened, and the two dock in the same bottom-left corner. One direction of a two-way rule is
the harder half to notice, because the way you naturally test it is the way that works.
`SetBuildPalette` now puts the tab away, beside the line that already puts the Menu away for the
same reason.

### 15e. A colonist's row said nothing when you pointed at it

`WorkGridModel.Describe` — the sentence naming all four signals in words — was written, documented
as *"the sentence a cell says when hovered"*, asserted by a test, and **wired to nothing**. Only
the column headers had a tooltip. The border's skill band, the ink's priority and the flames' two
states were nameable nowhere but the legend. `PaintWorkCell` now sets it on every cell.

### 15f. The panel outlived its session

`HudShell.Attach` re-syncs the debug panel against the new session's director and did not re-sync
this one, so a tab left open in one colony stayed on the screen over the next, drawing the previous
colony's rows. One line, the same line the debug panel already had.

### 15g. The day had two owners

`WorkGridLayout.Hours` was the literal `24` under a doc comment saying it was
`ScheduleHandle.Hours`. That is P1 — one rule, two owners — in its smallest possible form, and the
form it always takes before it is a bug. It is now defined as that constant, and
`TheDayIsTwentyFourHoursInOnlyOnePlace` joins the third holder of the number, `GameClock`, to the
other two: this is the assembly where all three are in scope, so this is where they are tied.

### 15h. What was checked and left alone

- **The aspect publish grew by 32 rows a colonist** — eight for work, twenty-four for the day, on
  top of the twenty-seven skills already there. It is the shape `SkillAspects` settled and the
  buffer is reused; it is recorded here so that the next person to measure the publish knows the
  number tripled on this branch and why.
- **`WorkGridModel.Refresh` allocates a row and two lists a colonist per refresh** while the panel
  is open. The class comment's claim about not allocating is about the *visual elements*, which are
  genuinely only rebuilt when the roster changes. At a mid-bucket cadence and a colony of ten this
  is not worth the risk of pooling objects the tests hold references to; it is written down rather
  than changed.
- **The grid has no vertical scroll.** `maxHeight` is 80% and rows are 30px, so about twenty-four
  colonists fit before the last row is clipped with no way to reach it. Not fixed: the colony is
  three, and the fix is a scroller whose interaction with the frozen name column is a decision
  rather than a line.

## 16. Pagination, and the end of the scrollbar (2026-09-20)

**Owner:** *"Remove the scroll bars — this isn't a good interface — replace with pagination similar
to the roster pagination instead and keep a number that makes sense. This way the control never
needs to resize everything and pagination could be used. Please could you ensure performance."*

### 16a. What the scrollbar was actually costing

Two things, and the second is the one that was never going to show up in a screenshot.

It made **the panel's shape a function of the window**. The 96% cap of §15b meant the same panel
was a different width on a different screen, and the columns you could see depended on how you had
sized the game rather than on anything you had chosen. A control the player has learned the shape
of should not change shape.

And it built **everything, always**. A scroller clips what it shows; it does not decline to build
it. Twenty-two columns × every colonist alive were constructed as real elements whether or not one
of them was on screen, and the rows grew with the colony until they clipped, unreachable, at about
twenty-four. So the panel's cost grew with the colony and nothing capped it.

### 16b. The two numbers

| | Number | Why this one |
|---|---|---|
| Work columns a page | **11** | 22 divides by it exactly — two full pages, no ragged remainder. The owner's call over 8, which would have fitted a 1366 window and gathered all four live columns on page two, but left page one entirely dead and made three pages of it |
| Colonist rows a page | **12** | The panel stands 464px of grid at its fullest, and the grid is capped at twelve rows however large the colony grows |

The day is **not** paged. All twenty-four hours stay on the right of every page, because a row being
one colonist's whole day is the entire claim of folding Schedule into Work (§12a); a day split
across pages would be two answers to *and when* again. `ScheduleGridTests.OnePageOfWorkAndTheWholeDayFitTheReferenceScreen`
is what stops somebody reclaiming that 816px later.

**The panel is therefore one width, for ever:** `192 + 11×34 + 1 + 24×34 = 1,383`, plus two for the
frame. It clears a 1440-wide window and leaves a quarter of the reference screen showing the world.

**The cost of eleven**, stated plainly because it is a real one: the four live columns split two and
two across the pages, so you cannot see all the work the colony can actually do at once. That is a
fact about the *order* in `icon-keys.csv`, not about the number — Construction, Mining, Cutting and
Hauling sit at positions 9 to 14 because the list is ordered by urgency. Reordering the catalogue is
where that gets fixed, and the catalogue is the place a reordering belongs.

### 16c. Performance, which is the half that was asked for

A page builds what it shows. Element counts behind the grid, counting the slot, box, glyph, mark and
flames of a cell, the twenty-four hour blocks and the three of a name row:

| Colony | Before | After | |
|---|---|---|---|
| 3 | 477 | 279 | 1.7× fewer |
| 12 | 1,710 | 1,017 | 1.7× fewer |
| 25 | 3,491 | **1,017** | 3.4× fewer |
| 50 | 6,916 | **1,017** | 6.8× fewer |

**The number stops moving.** That is the point rather than the ratio: the panel's cost is now a
constant the colony cannot change, where before it was a line with no ceiling on it.

Three more things were done for the same reason and each is asserted:

- **Rows are recycled.** `WorkGridModel` keeps a pool and a refresh reuses it, so a panel left open
  allocates nothing after its first page — ADR 0003's flip condition F1. **A bound is what makes a
  pool worth having**: unbounded it would only have been a list that never shrank, which is why
  this was left alone at review time and taken now.
  `RefreshingReusesItsRowsRatherThanBuildingNewOnes`.
- **A page turn builds eleven header boxes and no cells.** The cells are slots, re-aimed at another
  column rather than rebuilt — `WorkCellView.Column` moves and `PaintWorkCell` does the rest.
- **The model still reads all twenty-two columns a row.** A `WorkCell` is a struct in a list that is
  the right length after the first refresh, so this costs nothing measurable and keeps a cell
  addressable by its catalogue index everywhere. Paging does not leak past the shell.

### 16d. The gestures

The roster's, because there is one way this game turns a page and this was not the place to invent a
second. Both pagers wear `roster-pager`'s own classes.

| | Where | Gesture |
|---|---|---|
| Columns | panel header, over the columns it moves | `‹ 1 / 2 ›`, **shift + wheel** |
| Colonists | the frozen name column's header, over the names | `‹ 1 / 3 ›`, **wheel** |

A wheel down a list of people means *down the people*, so the plain wheel is the rows; shift is the
platform's own horizontal modifier. Both pagers hide on a single page, exactly as the roster's does,
so a colony of three sees neither.

**Selecting a colonist brings their page up** (`EnsureRowPageFor`, `RosterModel`'s method and its
reason): a panel that answers a selection with a page the colonist is not on looks broken. It
happens on the frame the selection changes and not afterwards, or it would drag the page back while
you were reading another one.

**Shift-click still sets "the whole column", and that now means this page of colonists.** Reaching
people the player cannot see would be a gesture whose result is off-screen; the page is both what
they are looking at and what they can check afterwards.
