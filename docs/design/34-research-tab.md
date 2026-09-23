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

One field, **Power**, and five projects. Names and descriptions live in
`docs/design/icon-keys.csv` (`ui.research.category.*`, `ui.research.project.*`) and are read
through the registry; the project **description** is drawn from the CSV's description column,
which `emit_labels.py` now emits for the `ui.research.project` namespace only
(`Registry.Describe`). Costs, needs and unlocks are `ResearchCatalogue`, the only copy until a
Def replaces it.

| Project | Cost | Needs | Unlocks |
|---|---:|---|---|
| Wiring | 300 | none | Conduit |
| Generators | 500 | Wiring | Generator |
| Electric light | 400 | Wiring | Lamp |
| Batteries | 700 | Generators | Battery |
| Solar arrays | 1200 | Batteries | Solar array |

**Wiring starts done**, because the power branch (PR #173) lets a colony lay conduit with no
research, and a tab calling it undiscovered would contradict the palette. So a new colony shows
all four statuses but *researching*, and pressing **Research** on Generators shows the fourth.

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
| Active item 12% accent | the bar's existing `.cmd--on` wash (18%) | One rule for every bar item: Build, Menu and Work already use it. |
| `--rule` at 7% | `HudTheme.Divider` at 9% | The existing token; a second rule colour would be a seventh grey. |
| Window flat, 22 px close | as specced | Unlike Work and Animals, which keep the 5 px radius and 26 px close. Worth unifying one way or the other; the owner's call. |

## 6. Open

- The mechanism: a research Def, a bench, points per tick, save and hash. Then the palette reads it.
- A second field needs only a line in `ResearchCatalogue.Categories` and rows in the CSV.
