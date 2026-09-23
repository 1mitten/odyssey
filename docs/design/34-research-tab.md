# 34 — The Research tab (F3)

**Status, 2026-09-23: built, not yet played.** Branch `claude/research-tab`, worktree
`D:\code\odyssey-research`. It ships in one PR with the Inventory tab (design 35) because both
take the corner the Work tab docks in, and both are specified in the same visual language.

## 1. What was asked, and what was not

The owner handed over a visual spec (2026-09-23) for a Research window on F3 — category rail,
project table, detail pane, and a *Now* strip — with *"only include power for now but we'll
create the mechanism later - happy to just create the ui for now"*.

So this is **the interface and a placeholder state, not a research system**:

- There is no research Def, no research bench work and no simulation system. Nothing in `Sim`
  reads a project, a cost or a prerequisite.
- **Progress never moves on its own.** A project becomes done only through the debug menu's
  **Finish research** (Cheats tab), which completes the project in hand and starts the queue's
  head. `ResearchDirector.Advance(points)` is the seam a research bench will drive, and it is
  tested but not called.
- **The research state is session state on the interface side** (`ResearchDirector`): not saved,
  not hashed, and reset by a new session. No golden moved.
- **Unlocking does nothing.** The Build palette does not consult research. Wiring the palette to
  `ResearchDirector.IsDone` is a later unit, and it moves no hash either way.

## 2. The content

**Only what is in the game** (owner, 2026-09-23: *"There should be only research for what in
this game"*): two fields and four projects. Names and descriptions live in
`docs/design/icon-keys.csv` (`ui.research.category.*`, `ui.research.project.*`) and are read
through the registry; the project **description** is drawn from the CSV's description column,
which `emit_labels.py` now emits for the `ui.research.project` namespace only
(`Registry.Describe`). Costs, needs and unlocks are `ResearchCatalogue`, the only copy until a
Def replaces it.

| Field | Project | Cost | Needs | Unlocks |
|---|---|---:|---|---|
| Power | Electricity | 300 | none | nothing to build; it opens the two below |
| Power | Power lines | 400 | Electricity | Conduit |
| Power | Generator | 500 | Electricity | Generator |
| Furniture | Ladder | 200 | none | Ladder |

**Nothing starts done.** A new colony shows Electricity and Ladder available and the other two
locked. The first version had five Power projects with Wiring done, and the owner cut it to what
the game has. **The line is called *Conduit* in the palette** (the power branch, PR #173) and
*Power lines* here, as the owner names it; the unlock reads the palette's own word, so renaming
the tool is one row in `icon-keys.csv` when the power branch settles it.

## 3. The geometry

Every number is a constant in `ResearchLayout` and the arithmetic is asserted in
`ResearchTabTests`: 1100 wide, 34 + 30 + 420 + the frame high, a 200 rail, a 460 table
(`1fr 110 70`), a 438 detail pane, 30 px rows, **13 rows a page** and 12 when a category pages,
with the Animals tab's pager in a 30 px foot. Nothing scrolls.

## 4. Behaviour

- **Sort:** researching, available, done, locked; then cost, cheapest first; then catalogue order.
- **Exactly one category and one project are selected**, both with the 12% accent wash and a
  2 px rail drawn as a child, so it takes no width.
- **Research** starts an available project; one already in hand goes back to available with its
  progress kept. **Queue** appends; with nothing in hand it starts instead, since a queue with
  nothing ahead of it is the project in hand. **Pause** keeps the progress and the queue.
- A queued project is still *available* (the spec has four statuses); its secondary button reads
  **Unqueue**, a word the spec did not have, so the Queue button is never a press that does nothing.
- *Researching* is drawn as the spec draws it but is not pressable; Pause is the action.
- Opening the tab closes the Build palette, the menu, Work and Inventory, and clears the
  selection; a selection closes the tab. Escape closes it at the Work tab's rung.

## 5. Where the build departs from the spec, and why

| Spec | Built | Why |
|---|---|---|
| Command bar 44 high, 32 px items, 14 px icons | the shipped 48 px bar | Restyling the bar moves the dock line of every panel; it is its own unit if wanted. |
| Active item 12% accent | the bar's existing `.cmd--on` wash (18%) | One rule for the bar: Build and Menu already use it. |
| `--rule` at 7% | `HudTheme.Divider` at 9% | The existing token; a second rule colour would be a seventh grey. |
| NOW strip label | CURRENT | `HudGeometryTests.NoLabelIsAThreeLetterPlaceholder` holds the HUD to no two- or three-letter capitalised fragments, an acceptance criterion of the interface rebuild. |
| Window flat, 22 px close | as specced | Unlike Work and Animals, which keep the 5 px radius and 26 px close. Worth unifying one way or the other; the owner's call. |

## 5b. Measured

`DockedTabGeometryTests` (PlayMode) lays both windows out in a 1920 x 1080 panel and asserts the
specified size, the same size after the table re-sorts, and no descendant outside the frame. Its
first run used the batch runner's own small game view, where the panel scales to about 0.39: every
1 px border then rounds up to a whole physical pixel (2.57 layout units), the 34 px header to 36,
and the body pushed 5 px out of the bottom. That is the runner's screen and not the window, but it
is also what a **very small window** would do, since the heights are fixed and the frame clips.

## 6. Open

- The mechanism: a research Def, a bench, points per tick, save and hash. Then the palette reads it.
- A third field needs only a line in `ResearchCatalogue.Categories` and rows in the CSV.
