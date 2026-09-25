# Brief for Claude Design — the Health tab

**2026-09-25.** The prompt below is handed to Claude Design verbatim, with the attachments listed
at the end. Decisions behind it: `docs/research/health-interview.md` (owner: six body regions;
bleeding, tending and fall damage in scope; the Health tab only; documents before code) and
`docs/design/43-health.md` (the model the tab shows). The tab it replaces is the combat line's
Health tab as built (`docs/design/33-combat.md` §6C: "73 / 100", a Condition word, the weapon),
attached as a screenshot. It is built as unit H5 of `docs/plans/health.md` once the design is
agreed and the simulation units under it exist.

---

## The prompt

You are designing one tab of one panel for **Odyssey**, a colony-simulation prototype in the
RimWorld mould set in a ruined sci-fi city, rendered in 3D with discrete vertical layers. The
interface is a dark, flat HUD over the 3D board, already built and shipped; you are redesigning
one tab inside its fixed **inspect pane** and you must match the pane exactly. Attached are the
HUD's own mockup (`hud-v2.html`), the Work tab's mockup (`work-v1.html`) for the wider
vocabulary, and screenshots of the running game: the pane on its **Skills** tab, which is the
grid you are matching; the pane on today's Health tab with a hurt colonist; and a colonist lying
in a bed. **Everything you draw must look like it came from the same hand as those.**

### What the tab is

The **Health** tab of the colonist pane: the fixed panel at the bottom-left of the screen that
shows the selected colonist. The pane has a header (face, name, a state line), a strip of tabs
(Needs, Skills, **Health**, and four dimmed ones: Gear, Thoughts, Social, Log), and one body box
under the strip that every tab draws into. You are redesigning what the Health tab draws in that
box. The header, the strip and every other tab stay exactly as the screenshots show them.

The game is about to gain a body model. A colonist has **six regions** — head, torso, left arm,
right arm, left leg, right leg — each with its own hit points. Injuries sit on a region; a sharp
injury **bleeds** until somebody **tends** it; a colonist heals in a bed and faster when tended;
three **capacities** (consciousness, moving, manipulation) are derived from the regions and the
blood lost, and they decide whether she can stand. There are no other body parts, no diseases,
no infection, no surgery, no prosthetics and no medicine tiers, and you must not draw any of
them, not even greyed.

A **bandit** has no Health tab. An **animal** (a midden hog, a duct rat) has no regions: its
pane shows only its hit points, and one of your states shows that.

### Content

Names are the game's own and are never renamed. Some rows have no registered name yet; they are
marked *(needs a key)* and you should draw them with the words given.

**The header's state line** (not yours to redesign, but you must draw it correctly): it reads
the colonist's activity and mood, e.g. "Building · content". When she is hurt it gains the
Condition word the old tab carried — **Hurt**, **Stunned** or **Downed** — so the tab no longer
needs a Condition row.

**The body box**, two columns, matching the Skills tab's grid exactly: **seven rows a column,
each row 19 px, 4 px between rows, each column 256 px wide**. A row on the Skills tab is: a
17 px icon, a 9 px gap, a name column of 95 px, a 72 × 6 px bar with 8 px either side, a figure,
and an 18 px mark. Use that anatomy.

Left column, in this order:

| Row | Shows | Set in |
|---|---|---|
| Head | icon, "Head", a bar of hit points remaining, a mark | Archivo Narrow 14/500; bar |
| Torso | "Torso", the same | " |
| Left arm | "Left arm" *(needs a key: the registry has "Arm")* | " |
| Right arm | "Right arm" *(needs a key)* | " |
| Left leg | "Left leg" *(needs a key: the registry has "Leg")* | " |
| Right leg | "Right leg" *(needs a key)* | " |
| Pain | "Pain" *(needs a key)*, a percentage | name; IBM Plex Mono 14/500 for the figure |

The **mark** at the end of a region row is one of: nothing; **bleeding**; **tended**. Both are
drawn shapes (see Icons). A region that has been bled and then tended shows tended.

Right column, in this order:

| Row | Shows | Set in |
|---|---|---|
| Health | "Health", "73 / 100" and a bar — the whole-body hit points, exactly as today's tab draws it | mono figure; bar |
| Consciousness | "Consciousness" *(needs a key)*, "93%" | mono figure |
| Moving | "Moving" *(needs a key)*, "48%" | mono figure |
| Manipulation | "Manipulation" *(needs a key)*, "100%" | mono figure |
| Blood loss | "Blood loss" (registered), a bar of blood *lost*, "22%" | bar; mono figure |
| Bleeding | "Bleeding" (registered), "14 h to death", or the row empty | mono figure |
| Tended | "Tended" *(needs a key)*, "2 of 3" | mono figure |

**Bars** fill with the HUD's three-band colour by what remains: Good at or above 60 %, Warn
below 60 %, Bad below 40 % — the same bands the need bars and today's health bar use. The Blood
loss bar is the exception: it shows what is *lost*, so it is empty when she is well, and it
takes Warn at 15 % and Bad at 45 %.

**The weapon row is gone.** Today's tab shows the held weapon; that moves to the header line
and later to the Gear tab. Do not draw it.

**Clicking a region** is the one interaction in the tab: the right column is replaced by that
region's injuries, at most three rows — one per kind — each showing the kind's name (**Wound**,
**Bruise** *(needs a key)*, **Fracture**), its points ("12"), and the same bleeding or tended
mark, with the tended row also giving the tend's quality ("tended 70%"). The clicked region row
is shown selected in the accent colour with dark ink on it, and a second click, or a click on
another region, returns the right column. The left column never changes.

**No scrollbar, ever.** The box is 157 px tall today because seven rows fit it. Everything above
is designed to fit that. If you find it cannot, say the height you need in the top comment of
the file and nothing else about the layout changes — that number becomes a constant in code and
moves every tab, so it must be a decision and not a side effect.

### The states

Draw the pane in these states, the tab active in each, on the HUD with the board behind it:

1. **Unhurt.** Every region full, Pain 0%, Health "100 / 100", capacities 100%, Blood loss
   empty, Bleeding empty, Tended empty. This is what most colonists show most of the time, and
   it must read as calm, not as a wall of full bars.
2. **Bleeding.** Two wounds on the left leg (12 and 7 points) and a bruise on the torso (9),
   nothing tended: leg bar at 37 % (Bad), torso bar at 78 % (Good), the leg's mark *bleeding*,
   Pain 35%, Health "72 / 100", Moving 68%, Blood loss 22% (Warn), Bleeding "14 h to death",
   Tended "0 of 2". The header line reads "Fleeing · Hurt".
3. **Tended, in bed.** The same injuries a day later, tended at 70 %: the leg's mark *tended*,
   Bleeding empty, Blood loss 6%, Health "81 / 100", Tended "2 of 2". The header line reads
   "Sleeping · Hurt".
4. **Downed.** The right leg at 0 (a fracture of 30 and a bruise), the left leg at 40 %, Pain
   82%, Consciousness 41%, Moving 12%, Health "43 / 100". The header reads "Downed". Draw the
   whole pane dimmed as the game dims a downed colonist's pane, if the screenshot shows such a
   thing; otherwise leave it undimmed and say so.
5. **A region clicked.** State 2 with the left leg selected and the right column showing its
   two rows (Wound 12 · bleeding; Wound 7 · bleeding) — note: two wounds on one region merge in
   the game, so draw **one** row "Wound 19 · bleeding" and make the point in your comment that
   the game never shows two rows of one kind.
6. **An animal.** A midden hog selected: the pane's header for an animal (as the attached hog
   screenshot shows it — kind's name, "animal · L12 · 78, 59", its activity), **no tab strip**,
   and in the body only the Health row, "41 / 60" with its bar, and nothing else.

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
| Text meta | white at 66% | meta lines, secondary figures |
| Text dim | white at 50% | headers, hints, an empty row |
| Accent | `#6fd3e3` | the selected region row, the active tab |
| On accent | `#0b1116` | ink on an accent fill |
| Good / Warn / Bad | `#7fc98c` / `#e8b55c` / `#e06a5c` | bar fills by band; the bleeding mark is Bad |
| Info | `#8fd0e3` | not needed here; listed so you do not invent it |

Contrast comes from the scrim over the 3D board, not from the panels: panels are flat, no
gradients, no shadows, no glow, no rounded corners beyond what `hud-v2.html` uses.

**Type** — six steps and no seventh. Words in **Archivo Narrow**; every figure (points,
percentages, hours, counts) in **IBM Plex Mono 500** with tabular figures.

| Step | Use |
|---|---|
| 11 / 600, tracked 0.14em, upper case | panel labels and tab names |
| 12 / 400 | meta lines, the header's state line |
| 13 / 400 | body text |
| 14 / 500 | row names and row figures |
| 19 / 600 | the name of the selected colonist |
| 11 mono at 35% ink | a hotkey cap |

**Space** — the pane is **560 px wide**, 12 px padding, a 1 px border; the header is 60 px, the
tab strip 26 px with a 9 px gap under it, the body 157 px; rows 19 px with 4 px gaps in two
256 px columns with an 18 px gap between them; the bar 72 × 6; the icon 17 px with a 9 px gap;
the name column 95 px; the end mark 18 px. **Every one of these is a constant in code**; state
in your comment any you change and why.

**Docking** — the pane sits bottom-left, flush with the left screen edge, above the command
bar, exactly where the screenshots show it. It has the standard header and a close X at the
right. It never overlaps a docked tab (Work, Research, Inventory, Animals): the game closes one
when the other opens, so draw the pane alone.

**Icons** — pixel art, 32 px, point-filtered, drawn at 17 px in a row; use flat placeholder
squares where an icon goes and do not draw glyphs from a font. **Marks** (bleeding, tended,
the sort indicator) are drawn line art on a 24-unit box in the style of Lucide, 1.5 px stroke:
the HUD already has a hollow medical cross, a tick, a cross and a warning triangle. It has no
blood drop, bandage or prone figure; if you want one, draw it as a single SVG path and put the
path in your comment, because that is what the game will stroke. **No characters outside ASCII
anywhere**: the two shipped fonts draw nothing else, and a tick, a cross or a dingbat from a
font renders as an empty box.

### Deliverables

Static HTML, one file per state, 1920 × 1080, with the fonts loaded from Google Fonts exactly as
`hud-v2.html` does and the tokens declared once as CSS variables at the top of each file, so a
diff against the shipped tokens is possible:

1. `health-unhurt.html`
2. `health-bleeding.html`
3. `health-tended.html`
4. `health-downed.html`
5. `health-region.html`
6. `health-animal.html`

Below each file, in an HTML comment at the top, list every measurement you chose that the
system above did not fix, every measurement you changed, the height you need if it is not 157,
and the SVG path of any mark you drew — those numbers and paths become constants in code.

### What not to do

No body diagram, silhouette or figure with regions painted on it; no tree; no scrollbar; no
tooltips (nothing has verified they draw in the game). No infection, disease, immunity, surgery,
prosthetics, missing parts, scars, medicine tiers, doctors' names or bed assignment — none of it
exists. No new colours, faces or sizes. No rounded, glowing, translucent or gradient panels. No
renaming of any row. No copying of RimWorld's health tab: the shape is this game's Skills tab,
and the attachments show it.

---

## Attachments to send with the prompt

| File | Why |
|---|---|
| `docs/reference/mockups/hud-v2.html` (and `icon-map.js` beside it) | the HUD's own mockup: the base plate, the bar, the tokens |
| `docs/reference/mockups/work-v1.html` | the wider vocabulary of rows, columns and marks |
| a screenshot of the running game with a colonist selected on the **Skills** tab | the grid being matched, as it really draws |
| a screenshot on today's **Health** tab with a **hurt** colonist (the debug menu spawns a bandit; a fight makes one) | the tab being replaced |
| a screenshot of a colonist **downed or asleep in a bed**, with her pane open | the pane's dimmed state, and the bed |
| a screenshot with a **hog selected** and the inspect pane showing it | the animal pane, for state 6 |

## Answers recorded

Owner, 2026-09-25 (`docs/research/health-interview.md`): six regions (question 1); bleeding and
tending, and fall damage (2); the Health tab only (3); the design doc and this brief now, no
code (4). Every recommendation taken as offered.

## Built

Not yet. When the mockups come back: record the height chosen and any mark paths here, amend
`docs/design/43-health.md` §10 where the design departed from it, and build H5 of
`docs/plans/health.md`.
