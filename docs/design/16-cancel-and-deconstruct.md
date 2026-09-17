# 16 — Cancelling orders, chopping trees, and taking things apart

**Status: PR 1 built and unplayed; PR 2 planned, not built.** Written 2026-09-17 on
`claude/cancel-tool`. The owner's decisions are §3 and they are settled, not proposals. This
completes **U26**, whose row in `docs/plans/vertical-slice.md` still carries "Deconstruct refunds
half" as outstanding; that row changes when PR 2 lands, not before.

**Nobody has pressed Play on PR 1.** Both tiers are green and both gates pass, and neither can say
whether right-click disarms when the hand expects it to or whether six pixels is the right number.
The by-hand procedure is §6.

Read `15-building.md` first for the order pipeline this cancels, and `09-ui-and-input.md` §6 for
the input rules the right-click change lands in.

---

## 1. The thing that was already there

The owner asked for "a cancel button ... so we can deselect the build". **A cancel tool is built,
wired and tested**, and has been since the designate line landed. It is armed by **X**
(`HotkeyDirector` defaults), drags a box like mine and fell, and sends two intents per cell —
`CancelDesignation` and `CancelBuilding` — so one rubber means *whatever order is here, stop*
(`DesignatePresenter.OnToolDrag`). A cell cannot carry both, so exactly one of the pair does
anything and the other is refused with `AlreadyInThatState`.

The simulation half is complete and has its negative controls:

| Claim | Test |
|---|---|
| cancelling a fell order stops the chopper mid-swing | `FellJobTests.ACancelledOrderStopsTheJob` |
| cancelling a site refunds every unit delivered | `ConstructionTests.CancellingASiteGivesBackWhatWasCarriedToIt` |
| cancelling at the ground takes off the site standing on it | `ConstructionTests.CancellingAtTheGroundTakesOffTheSiteStandingOnIt` |
| a cancelled order stops being published | `DesignationProgressTests.ACancelledOrderStopsBeingPublished` |

A porter already carrying material to a cancelled site drops what it holds
(`BuildJob.cs`, `Cleanup → DropCarried`).

**So the mechanic was never missing. Every way of finding it was.** That is this unit's problem
statement, and it is worth saying plainly, because most of the work here is putting a thing that
exists on the screen rather than building a thing that does not.

## 2. The five gaps found

1. **No Cancel chip on the palette.** The Orders category lists mine, deconstruct, harvest, forbid
   and clear rubble, and not cancel (`HudShell.Bar.cs`, `BuildCategories`). Meanwhile
   `ui.arch.tool.cancel` — *"Remove designations and orders"*, M3 — has been in `icon-keys.csv`
   all along. The X binding appears nowhere on screen.
2. **The chopping chip is labelled "Harvest".** `ui.arch.tool.harvest` ("Take the crop") is wired
   to `DesignateTool.Fell`, while `ui.arch.tool.fell` sits in the registry unused. The owner's own
   word for chopping in this conversation was "harvesting", which is the label teaching them the
   wrong name for the tool.
3. **No per-object cancel.** Select a blueprint and the inspect pane says what it is and what it is
   waiting for, and offers no command. A10's grid fills only for a colonist, with three greyed
   placeholders.
4. **Right-click does not put a tool down.** Escape does, through the unwind rule
   (`SettingsDirector.Escape`). Right-click is the camera rig's orbit-drag, and separating a
   right-*click* from a right-*drag* is input case 5 of `09` §6 — designed, unbuilt, and the same
   separation the forced-order context menu will need.
5. **Cancel cannot reach a finished wall.** That is `DesignationKind.Deconstruct`, which exists as
   a value and which nothing acts on.

## 3. What the owner decided (2026-09-17)

- **Surfaces: the palette chip and right-click.** Not the A10 command on the selected thing — that
  waits for the command grid and the legality split forced orders needs, so it is built once
  rather than twice.
- **Right-click disarms, and does nothing else.** With nothing armed it stays inert *on purpose*:
  the slot is reserved for the "build this now" context menu (`15-building.md` §8).
- **Chopping a tree is "Chop trees"**, not "Fell trees" and not "Harvest". Harvest is freed for
  taking a crop, which arrives with growing zones.
- **Cancel reaches orders only — and Deconstruct is built in the same round.** Two tools, both
  reachable from the palette. A cancel that also demolished would mean one wide drag could pull a
  colony's house down.
- **Deconstruct points at our own buildings only.** The ruined city's stamped structures stay for
  Reclaim and Salvage, which are separate designed tools with better yields
  (`a-04-building-and-materials.md`). Staged, not a permanent rule.
- **A 5-unit wall refunds 2 or 3, by a seeded coin flip.** The reference's own behaviour (a-04 §1:
  50%, stochastic rounding on odd amounts). It **cannot** use `Random`: the refund lands in the
  state hash, so the draw comes from `DeterministicRandom` and replays identically. The flip rather
  than a rounding rule is what stops a wall being either a lossless store of material or a
  predictably lossy one.

## 4. Two things I read and did not prove — and they go first

Both were found by reading and are stated here as **predictions with the experiment attached**,
because reading the code has been wrong every time it mattered on this project.

**(a) A wall a colonist built is probably not in the save.** `List<PlacedEdifice>` implements no
`ISaveable` — the six save keys are grid, designations, construction, items, jobs and pawns — and
`CellGrid.Edifice[cell]` saves only an *index* into a list that worldgen rebuilds from the seed.
`ConstructionGrid.Raise` appends to that list at run time, so after a reload the index should point
past the end of a shorter list, `TryEdificeDef` should bounds-check it away, and the wall should
lose both what it is and what it is made of.

**(b) Which means the edifice record is probably not in the state hash either.** `CellGrid`
contributes `Edifice[i]`, the handle. Nothing appears to contribute `Def`, `Stuff` or `Removed`.
Two colonies whose walls are made of different materials would hash the same. This is the same
shape as the gap OQ-50 closed, found the same way.

**The experiment, before any other line of this unit is written:** build a wall in a headless
colony, record its stuff and the state hash, round-trip the save, and ask again. If the predictions
hold, both are in scope here — deconstruct reads `Def` for the work and `Stuff` for the refund, so
a deconstruct after a reload would refund the wrong thing or nothing at all, and that is not a bug
to discover from a playtest. If the predictions do *not* hold, this section is deleted and says so.

**This is a control, not a formality.** OQ-05's control should have failed and did not, which is
how the hash came to be blind to the whole world for a year.

## 5. The plan: two pull requests

**Split deliberately.** The first is HUD-only, lands in an afternoon and makes a cancel tool the
owner already paid for reachable in the running game. The second is a simulation feature with a
save-format question underneath it. Shipping them together would hold a five-minute fix behind a
week's work, and the owner would have nothing to play meanwhile.

### PR 1 — put the tools on the screen, and fix the names — **built**

Everything below landed as described, with three things worth recording that the plan did not
foresee.

**`PaletteTools` took the whole palette, not just the live rows.** The categories moved out of the
shell too, for the reason `HudCommands` already lives in `Odyssey.Hud`: the palette is data, and
the fast tier can see that assembly and cannot see a `MonoBehaviour`. That move is what made the
rest testable — **`RegistryTests.EveryPaletteKeyIsARegisteredName` could not have been written
before it**, and the palette had no registry coverage at all until now.

**The armed banner was naming things in C#.** It read "Felling" in a `switch`, which disagreed with
the chip the moment the chip became "Chop trees" — the exact failure the naming registry exists to
prevent, found by the rename rather than by a test. Mine and Chop now read
`Registry.Label("ui.status.*")`; those keys were already phrased as the thing being done, so the
voice is unchanged and the next rename carries. Cancel keeps its own words, because there is no
activity key for it: no colonist is ever *cancelling*.

**`PressGesture` caught a trap on its first run**, which is the argument for the lift stated as an
incident. `new PressGesture()` runs the implicit all-zeroes struct constructor rather than one
declared with optional parameters, so a threshold held as a **field** was zero exactly where it was
used, and every press read as a drag. Three tests failed. The threshold is a constant now. Inside
the rig this would have shipped, and the symptom — "right-drag puts my tool down" — would have been
diagnosed as a number that wanted tuning.

**1. The names.** A content commit: `docs/design/icon-keys.csv`, then both generators.

| Key | Was | Becomes |
|---|---|---|
| `ui.arch.tool.fell` | Fell trees | **Chop trees** |
| `ui.keys.fell` | Fell tool / "Arm the fell trees designation" | **Chop tool** / "Arm the chop trees designation" |
| `ui.status.felling` | Felling | **Chopping** |
| `ui.item.axe` | "Cutting, and felling" | "Cutting, and chopping" |

`ui.status.felling` is my recommendation rather than the owner's instruction, and the reason is
that the palette and the roster card must not use two words for one job: a player who arms **Chop
trees** and then reads **Felling** on a colonist's card has to work out that they are the same
thing. It is one CSV line to reverse at approval.

**Every key stays exactly as it is.** `ui.status.felling` is one of the nineteen keys that draws
real art, cut from sheet 06, and `icon-map.csv` is keyed the same way — renaming a key would
silently drop an icon back to an outlined square. The key is the stable identity; the label is the
thing the owner corrects. That is what the registry is for.

**The C# names do not change.** `DesignateTool.Fell`, `DesignationKind.Fell`, `FellJobDriver`,
`JobHandle.Fell` are internal and appear in about twenty files. Renaming them buys the player
nothing and costs a diff that hides the real change. Recorded as a decision so the next session
does not "tidy" it.

**2. Cancel on the palette.** Add `ui.arch.tool.cancel` to the Orders category and to `LiveTools`;
swap that category's `ui.arch.tool.harvest` chip for `ui.arch.tool.fell`.

**3. `MarkArmedTool` stops being a ternary.** It is today a hard-coded three-way expression that
must be edited for every new live tool, and it will be silently wrong — a chip that never lights —
the moment the fourth one lands, which is this PR. A live tool becomes **one row**: the key, how to
arm it, and how to ask whether it is armed. `LiveTools` already has the first two. This is the
seam, not a tidy-up, and it is the same argument the work-giver registry settled in OQ-44.

**4. Right-click puts the tool down.** `SliceCameraRig` mirrors, for the right button, the
click-versus-drag split the left button has had all along: record the press position, and on
release decide by `DragThresholdPixels` whether it travelled. Travelled means it was an orbit and
nothing else happens; not travelled means a click, and the rig raises it. Gated on `overInterface`
at the press, per input case 2.

`DesignatePresenter` subscribes, abandons any drag in flight and calls `PutToolAway()`. It is
**not** routed through `SettingsDirector.Escape`, and the comment must say why: right-click has
exactly one meaning and must not close a panel or open the menu. Escape unwinds; right-click
disarms. Two rules, deliberately not one.

**The decision comes out of the rig.** "Did this press travel far enough to be a drag" is
arithmetic, and the fast tier cannot see it inside a `MonoBehaviour` — the PlayMode harness still
cannot deliver a synthetic mouse. It goes into `Odyssey.Hud` as a small press-tracker with its own
test, the same lift that made `DesignateDirector` testable, leaving only the wiring untested.

### PR 2 — Deconstruct

Ordered so that each step is provable before the next is written.

1. **Step 0's answer.** If the edifice record is unsaved and unhashed, it becomes a saveable and a
   hashable here, with `StateHashCoverageTests` extended field by field. Nothing below is correct
   without it.
2. **`PlacedEdifice.Built`.** One bool, set only by `ConstructionGrid.Raise`, false for everything
   the generator stamps. Today a colonist's wall and the city's wall are deliberately
   indistinguishable — `Raise`'s own comment says so in as many words — so **"ours only" is not
   expressible without this field.** It is also exactly the bit Reclaim will flip when it lands
   ("adopt existing ruined structure as ours"), which is why it is a flag on the record rather
   than a proxy. *The proxy considered and rejected:* our stuffs are the buildable ones and the
   city's are not, so `IsBuildable(stuff)` would work today for free — and would quietly start
   including city walls the day a salvage line gives steel an item.
3. **The order.** `DesignationKind.Deconstruct` already exists; `DesignationGrid.Allows` learns it:
   a cell holding an edifice that is `Built` and is not a tree. No new `IntentKind` — this rides
   `Designate(cell, A = kind)` like mine and fell.
4. **The work.** `DeconstructWorkGiver` and `DeconstructJobDriver`, self-registering by existing.
   Work type Construction. Stands beside the cell (`FellJobDriver.StandBeside`), banks progress on
   the cell like mining's and building's, and swings `WorkStyle.Building` — the hammer stands in
   until there is a better stroke, exactly as it does for building. Work is `workToBuild` clamped
   to [20, 3000] ticks (a-04's own figures); with one building in the game the clamp is
   unobservable today and is still the right shape to write down.
5. **The completion**, deferred like every structural edit: `RemoveEdifice`, chunks and nav marked
   dirty around the cell and the cell above — `Raise`'s list, inverted — then the refund spawned
   through `ColonyItems.NearestCellWithSpace`, which is how felled wood and mined stone already
   land. Support stays undirtied, the same deliberate omission `Raise` and `MineCell` make, to be
   wired with U29 and not before.
6. **The coin flip.**
   `DeterministicRandom.ForTick(ctx.Seed, cell ^ tick, PawnPurpose.DeconstructRefund)`.
   **Keyed on the tick as well as the cell**, unlike `StoneYield`, and that difference is the
   point: a rock's yield is a property of the rock and should not change, while a refund keyed on
   the cell alone would make every cell permanently a "2" cell or a "3" cell — findable, and then
   farmable by rebuilding the good ones.
7. **`JobHandle.Deconstruct = 9`, `Count = 10` — and `JobLabels.IconKeys` grows in the same
   commit.** This exact omission made every builder and porter in the game read as **idle** on
   2026-09-17. `RegistryTests` holds the array to `JobHandle.Count` now and will fail the fast
   tier, so this is a reminder rather than a risk; it is written down because the guard was written
   after the fault, not before it.
8. **Content.** The job def goes in `Assets/Odyssey/Defs/Core` with its in-code oracle in the same
   commit, and the fingerprint test moves by one line. That is the rule content earns its keep by.
9. **The palette.** `DesignateTool.Deconstruct`, and the already-drawn-but-greyed
   `ui.arch.tool.deconstruct` chip in Orders becomes live — one row in the table PR 1 built.

## 6. How to test it, written before the code

### Fast tier, Hud

- Every live palette chip has an arm action **and** an armed predicate, and arming any one of them
  lights exactly one chip. The negative control is a chip that is not live: it registers no
  callback and never lights.
- A press that never travels is a click; one that travels past the threshold is a drag. Both
  directions, and the boundary.

### Fast tier, Sim

- `DeconstructTests.AWallIsTakenApartAndHalfComesBack` — build a wall on a real generated board,
  order it deconstructed, tick: the wall is gone, the cell is walkable again, and 2 or 3 wood is on
  the floor within reach.
- `ACityWallIsNotOursToTakeApart` — **the control that makes the previous test mean something.** A
  generator-stamped wall on the same board is refused. Without it, a deconstruct that took anything
  apart would pass.
- `TheRefundIsWorthLessThanTheWall` — over many walls, both 2 and 3 occur, and the mean is under
  the 5 it cost. A test that only asserts "some wood came back" would pass a 100% refund.
- `TheSameSeedRefundsTheSameAmount` — two runs, identical. And the same cell rebuilt and taken
  apart on a different tick can differ, which is §5's farming argument stated as a test.
- `ACancelledDeconstructStopsTheJob` — the mirror of `FellJobTests.ACancelledOrderStopsTheJob`.
- `AWallRemembersWhatItIsMadeOfAcrossASave` — §4's prediction, as a standing test.

### By hand, in `Play.unity`

1. **B**, Orders. The row reads Mine, Deconstruct, **Chop trees**, Cancel, Forbid, Clear rubble.
2. Arm **Chop trees**, drag over the wood, watch the marks appear. Arm **Cancel**, drag back over
   them, watch them go — and watch a colonist already swinging give up and walk away.
3. Build a wall. While it is still a blueprint, cancel it, and check the wood comes back.
4. **Right-drag to orbit the camera with a tool armed.** The tool must still be armed at the end —
   this is the fault the threshold exists to prevent.
5. **Right-click without moving.** The banner above the command bar goes.
6. Let a wall finish. Arm **Deconstruct**, drag over it, and watch it come down into a small pile.
7. Point Deconstruct at the ruined city (load the city map) and watch nothing happen. Check the
   console: `Designate: NotPermitted`, not silence.

### Read the console

Per `15-building.md` §6 — every refused command logs, grouped and throttled. `NotPermitted` means
the rule refused it; `UnknownIntent` means a composition fault; silence with no effect means the
orders are landing and the **work** is not happening.

### Gates

`scripts/test-fast.sh`, then `scripts/unity.sh test editmode`, then both content checks —
`python3 tools/wiki/build_wiki.py --check` and `python3 tools/wiki/emit_labels.py --check` — before
either commit, because both PRs change named content.

## 7. What could make this bigger than it looks

- **The save gap (§4) is the one real unknown.** Handles into the edifice list are described in
  `WorldGenContext` as "part of the determinism contract ... assigned in generation order". Runtime
  appends followed by a reload are precisely where that contract is not kept, and fixing it
  properly may mean the list becomes a saved, hashed component in its own right. That is the right
  fix and it is bigger than a bool. It is why step 0 is step 0.
- **The right-button threshold may want to be larger than the left's.** Six pixels is tuned for
  "did the player mean to draw a box"; an orbit is a deliberate sweep, and a click that jitters
  should still disarm. One number, judged in play, not from a contact sheet.
- **Nothing here is testable by looking at a screenshot.** Both PRs are gestures. They need the
  owner at the keyboard, which is the standing note in `CLAUDE.md` about the interface work.

## 8. Deliberately not in this unit

- **The A10 command grid**, and a Cancel command on the selected thing. It wants the
  legality-without-side-effects split that forced orders needs; the two share it and should be
  built together (`15-building.md` §8).
- **Right-click as a context menu.** The slot is reserved, not filled.
- **Salvage and Reclaim** — the city's own loop, with its own yields.
- **Collapse.** Taking a wall out does not bring anything down, because nothing collapses yet.
  U29 wires mining, building and deconstruction to the support solver in one go, and none of the
  three should quietly acquire behaviour the other two lack.
- **A success roll at completion**, still outstanding on U26's row and independent of this.
