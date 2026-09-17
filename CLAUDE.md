# Odyssey — project guide for Claude Code

Read this first, then `docs/brief.md` (the governing brief) and `docs/research/INDEX.md` (what is known so far). Update the **Current status** section whenever it changes, and **append the reasoning to `docs/journal.md`** rather than growing this file — that is what the journal is for.

## What this is

A prototype colony sim in the RimWorld mould, in true 3D with discrete vertical layers, set in a ruined sci-fi city. Unity 6.3 LTS (6000.3.x), URP, C#. Art: Synty POLYGON Sci-Fi City. The owner does visual work in the editor on Pop!_OS; Claude Code does code, tests, research and documentation.

## Working agreement (from the brief; these hold everywhere)

- **Phases with hard stops.** Ground → Interview → Research → Plan → Execute. End the turn after each phase and wait. Never run a later phase on assumed answers. No gameplay code until Phase 4, and only after the plan is approved.
- **Research lives in subagents.** One subagent, one question, a hard cap ("stop after N searches / N reads"), fixed return format: Findings, Sources (URLs), Confidence (high/medium/low), Could not be determined. Write each result to `docs/research/<slug>.md` and keep `docs/research/INDEX.md` current (the naming scheme is in it).
- **Commit to recommendations.** Rank options and back one. If two are tied, name the observation that breaks the tie and the cheapest experiment that gets it.
- **Clean room.** Study RimWorld's mechanics, formulas, data shapes and design intent; never paste Def XML, decompiled code, art, audio, names or flavour text into this repo. Never decompile into the repo. Invent our own names.
- **Licensed assets stay licensed.** Synty content lives only under `Assets/Synty/`, which is gitignored. Never copy it elsewhere, never commit it, and never let the simulation or its tests depend on it (clones without the pack must still build and run headless).
- **Files outlive context.** Every phase produces files under `docs/`. Assume the next session knows nothing except what is written down.
- British English in documentation. No multiplayer, ever. *Ramble* (Godot) is reference only, no code reuse.

## The content wiki is a standing obligation

`docs/wiki/` is the naming reference for the whole game: every commodity, item, building, command,
work type, need, body part, alert and proper noun, with a stable key beside each. It exists so the
owner can read what is in the game and correct it, and so no session has to guess what something is
called. Hosted: [wiki](https://claude.ai/artifact/JsYRQk1vnza2wFWSNfpQwr) ·
[HUD mockup](https://claude.ai/artifact/PcHWoujGDvd4rzcH1LAwFG).

**The rule: any commit that changes game content updates the wiki in the same commit.** Game content
means anything a player could see named — a new commodity, a renamed building, a reworded
description, a new alert, a faction, a creature, a research project, a month of the calendar. Design
and mechanics are not wiki content; they stay in `docs/design/` and `docs/adr/`.

**Never hand-edit anything under `docs/wiki/`.** It is generated and your edit is silently
overwritten. Edit the source, then rebuild:

| Source | Holds |
|---|---|
| `docs/design/icon-keys.csv` | the name, namespace, milestone and description of every named thing |
| `docs/design/icon-map.csv` | whether the owner's pixel-art sheets can draw it |
| `docs/design/proper-nouns.csv` | people, places, factions, creatures, the calendar |

```
python3 tools/wiki/build_wiki.py            # rebuild docs/wiki
python3 tools/wiki/build_wiki.py --check    # exit 1 if stale; run before committing
python3 tools/wiki/emit_labels.py           # rebuild Assets/Odyssey/Hud/Registry.g.cs
python3 tools/wiki/emit_labels.py --check   # exit 1 if stale; run before committing
```

The HUD reads its labels from the same file: `emit_labels.py` generates `Registry.g.cs`
(`Registry.Label(key)`), `JobLabels` and `LedgerModel` name nothing themselves, and
`RegistryTests` fails the fast tier on any key the CSV does not know. A content commit runs both
checks.

Both `--check`s are the gate and belong in CI beside the test tiers. Two notes before extending
it. The registry is hand-authored **only until the Def set covers it**: then `icon-keys.csv` is generated
one way out of the Defs and committed, so the wiki and the build-gating icon tests share one
source. Do not create a second source of truth meanwhile. And the hosted copies are snapshots:
after a rebuild, republish `docs/wiki/artifact.html` and
`docs/reference/mockups/hud-v2.artifact.html` (regenerate the latter with
`python3 tools/mockups/artifact_body.py docs/reference/mockups/hud-v2.html`).

## Current status

**Read `docs/journal.md` for how any of this came to be.** It is the narrative record — every
decision, measurement and reversal behind the state below, and it is where this section's history
went on 2026-09-16. What follows is only what is true *now*.

### Where the project is

Phases 0–3 (ground, interview, research, design) are complete. **Phase 4, execution, is under way.**

- **M0 is closed.** CI runs two tiers on every push and pull request: a *fast tier* on
  GitHub-hosted Linux (Sim, Hud, Long, the content-registry check, the icon tooling) and a *Unity
  tier* on the owner's Windows machine as a self-hosted runner (EditMode, PlayMode, the headless
  one-day run), switched on by the repository variable `UNITY_RUNNER=1`.
- **M1 and M2 are done and reported** — `docs/milestones/M1-report.md`,
  `docs/milestones/M2-report.md`. Both went further than the plan asked.
- **M3 is under way:** designations, felling, stockpiles and mining are in, and **U26
  building landed 2026-09-17** — a wall can be ordered from the Build palette in wood or
  stone, and colonists carry the material and raise it. Design, the test procedure and what
  is still open are `docs/design/15-building.md`; **read that before touching this line.**
  It merged as PR #63 on `main` (it had gone conflicting against OQ-50's golden re-bake; the
  journal for 2026-09-17 records why the number moved and how the re-bake was checked rather
  than trusted). **Walls do go up** — the owner
  played it on 2026-09-17, so the earlier report of silent refusal was the composition fault and
  is closed. They went up **stepped**, which was not building's fault at all: `ChunkMesher`
  lifted every panel to one height taken at one point, so neighbours in a run differed by the
  drawn relief's slope across a cell (73 mm typical, 220 mm worst, against a 3 m wall). They are
  draped now, like the ground, the banks and the water, and the same seam measures 1.1 mm. **The
  rule that came out of it: anything fixed to the grid is draped; only what moves over it is
  lifted.** A hollow wall was filled and capped in the same round.
  **Cancelling is now reachable, and chopping is called chopping** (`claude/cancel-tool`,
  2026-09-17). The cancel tool was never missing — `DesignateTool.Cancel` has been on the **X** key
  with a complete, tested simulation half since the designate line landed; every *way of finding
  it* was missing. It has a chip on the Orders row now, **right-click puts any armed tool down**
  (input case 5 of `09-ui-and-input.md` §6, first half built; with nothing armed it is deliberately
  inert, reserved for the forced-order context menu), and the palette's chopping chip stopped
  saying "Harvest" — which is what had taught the owner to call it harvesting. Labels only: every
  key is unchanged, because `ui.status.felling` draws real art and `icon-map.csv` is keyed the same
  way. Design, what is still open and the by-hand procedure are
  `docs/design/16-cancel-and-deconstruct.md`.
  **The Build palette is now three layouts over one selection** (`claude/build-palette-layouts`,
  2026-09-17) — the owner's 4a *Rows* (the default), 4b *Rail* and 4c *Bar*, switchable from the
  panel header and from Options › Interface, with the choice stored. Design, the measurements and
  what nobody has judged are `docs/design/17-build-palette-layouts.md`; **read it before touching
  `HudShell.Build.cs`, `BuildPalette.cs` or the `bp__*` block of `Hud.uss`.** Three things a later
  session should not undo by tidying. **Orders, Zones and Salvage came off the palette** as the
  specification asked, but Orders held Mine and Chop — so both are **pinned in the header** beside
  Deconstruct and Cancel rather than lost to the `M` and `C` keys, which is the Cancel fault of the
  day before repeated exactly; `EveryLiveToolIsDrawnSomewhere` is the general form of that rule.
  **The panel is docked, not floating, and the default is a column** (owner: *"tight and flush to
  other elements to enable full use of space"*, then *"use the left hand side of the screen instead
  of the width"*) — the mockups were drawn over a bare board and would have covered the stores panel
  and the roster strip, and Rows spanning 1920 made seven very wide tiles out of the category row.
  Rows is 372 px and four tiles across, and its height is **fixed at the widest category's** so the
  control does not move up and down the screen as categories are opened; only Bar still spans the
  screen. **Nothing is armed until the player clicks** — the palette model is built when the HUD
  attaches, and its seeding pass used to arm a wall, so the game began in build mode with a wall on
  the cursor. The Build cap on the command bar is the indicator of that: outlined at rest, filled
  **only while a tool is actually held** — an open panel with nothing chosen is not build mode. The
  palette pins into the bottom-left corner and **closes the inspect pane when it opens**. And **thirty-seven icons are drawn** as `Painter2D` paths in `HudGlyph`'s
  existing box, because ADR 0007's pipeline covers no architecture key and the specification forbids
  the placeholder square here; materials keep the game's own sprites, which is the one tier whose
  art must not change. **Every PlayMode run writes `Logs/palette-{rows,rail,bar}.png`** — the only
  thing anybody has looked at, and what found the three faults the tests could not see.
  **Deconstruct landed the same day** and closes U26's last outstanding line but one. A colonist
  walks to one of our own walls, takes it apart and leaves **2 or 3 wood of the 5 it cost** — a
  seeded coin flip on the odd unit, keyed on cell *and tick* so a cell cannot become a permanently
  generous one. **Only what we built**: `PlacedEdifice.Built` is set by `ConstructionGrid.Raise`
  and nowhere else, so the ruined city stays Reclaim's and Salvage's. Getting there first required
  fixing the save and hash gap above, which was found by a test written to fail.
  **The build cursor and the drag gesture were then played and accepted** (owner, 2026-09-17): a
  build drag draws one closed wireframe box over the whole run, draped as the wall will be, and a
  box widens into a rectangle only after three cells clear across the run — narrowing again within
  one. The gesture took three rounds because the first two fixed the *number* and the fault was
  that the gate latched. What nobody has judged yet is the site marks, the blueprint readout and
  the computed hammer swing (`docs/design/15-building.md` §8).
  **Forced orders have their simulation half** (`claude/forced-orders-intent`, 2026-09-17): steps 1
  and 2 of that section's four. `ForceJob(cell, A = job, B = pawn)` is the first intent that names a
  colonist, and `Job.PlayerForced` — saved and hashed since the job record was written, and read by
  nothing until now — is what it sets. A forced order is **not a new job kind**: the same
  `BuildJobDriver`, with the scan bypassed and the job pushed onto the named pawn. The piece that
  mattered is the split: `BuildWorkGiver.CanBuild` answers "could this colonist build that" and
  **reserves nothing**, `JobSystem.CanForce` is what a menu asks, and `TryGiveJob` calls the same
  query — so the offered path and the forced path cannot come to disagree. **Steps 3 and 4 are not
  started** (the right-click/drag split, and the context-menu panel), so nothing in the running game
  can send one yet and nobody has pressed Play on it.
- **U29 is built — floors, roofs and collapse** (2026-09-17), the unit the plan calls the one the
  project exists to prove. A floor is `Building_Wall` with one field changed (`BuildingDef.slab`)
  through the same pipeline; a slab is written to `Floor[cell]` as `CoreContent.SlabBuilt`, which is
  `PlacedEdifice.Built`'s argument one level down and costs no new state. **The support physics was
  already built and switched off**: `SupportSolver` had computed collapses since M1 and the deferred
  lambda that should have acted on them was empty. It acts now — what stood on a falling slab drops
  to the first real floor below, keeps a memory of it, and the debris lands as rubble that must be
  cleared before anything is rebuilt. **An order that could not stand is refused**
  (`SupportIfSlabAt`), so a bridge reaches as far as `S_max` and no further, and the three
  "support is deliberately not marked dirty" comments in `Raise`, `Demolish` and `MineCell` are
  closed together as all three demanded. **Fall damage is deferred and said so**: `a-02` has the
  number and there is no health model to apply it to. Design and the eleven owner decisions:
  `docs/design/17-floors-and-collapse.md`.
  **In review the tool turned out to be armable, draggable and inert** (2026-09-17, measured):
  `SlicePicker` answers a click on a wall with the wall's *own* cell, a floor ordered there was
  `NotPermitted`, and the one cell that accepted an order — the cell above a wall — could not be
  named by pointing at anything. Every U29 test named its site in C# and so could not see it.
  `ConstructionGrid.StandingOver` is the fix: **a slab ordered at anything that fills a cell means
  the boundary on top of it**, which is what decision 4 of the design asked for in the first place
  (*"the same lift a wall order gets"*). The cursor was lifted by the same rule.
  **The picker-to-order seam is now tested as a seam** — `FloorToolReachTests` feeds the picker's
  answer straight into the order, in the one assembly that can see both halves.
  **Two cursor rules came out of the owner playing it (2026-09-17).** A floor cursor is a flat
  plate laid on the boundary the slab will occupy, not a cell-tall box — the cursor is the shape of
  the thing. And **the build cursor goes red when the simulation would refuse every cell of the
  drag**, asking `ConstructionGrid.Allows`, the same method the order calls. It had to: on the
  played meadow only **21 of the 441 cells within ten of the start** will take a slab, and the
  cursor was green over all of it.
  **A floor you lay on the ground is a different feature, and it is `U42`, built 2026-09-17**
  (`docs/design/18-paving.md`). `Building_DeckPlate` is `Building_Floor` with one more field:
  `covering` inverts exactly one question, so paving wants a cell that **has** a floor and **never
  asks the support rule**, because the ground holds it and it cannot fall. A fifth slab kind in
  `Floor[]`, so **no new save state and no hash change**; it takes the *wall's* lift and
  `WorkingLayer` stays null for it. Grass no longer grows through a paved cell.
  **Paving does nothing yet** — walking speed, cleanliness and beauty do not exist — so it is a
  surface that looks different and that is all.
  **The two are called `Slab` and `Floor`** (owner, 2026-09-17, reversing "both say floor" the same
  afternoon after three rounds of confusion). Keys did not move; only labels. **Paving is offered in
  `Floors` and in `Structure` both**, because a wall, its floor and the slab over it are one job —
  the slab stays in `Structure` alone, and a test pins all of it. Note the asymmetry:
  `BuildingHandle.Floor` is still the **slab** and `BuildingHandle.DeckPlate` is still paving, because
  handle values are a save contract and a swap would compile in silence.
  **`U43` is the way up, built 2026-09-17.** Before it, every slab in the game measured **walkable
  and unreachable** — U29 shipped floors and collapse and a colony could never stand on a second
  storey. Vertical movement goes through a `Pathing.Connector` and connectors only came from
  worldgen; `ConstructionGrid.RefreshLadder` registers one at run time, idempotently, from every
  place either end can change. **No save-format change**: the connector is derived from the edifice
  list by `RebuildDerived`, like support and the region graph. **A hauler cannot climb a ladder**
  (`Connector`'s own long-standing rule), so a colonist can get up but cannot carry material up —
  which is why **stairs are the next unit rather than a maybe**.
  **Nothing tests that a click reaches the game**, and that is why this line of work has had three
  silent failures: a PlayMode test cannot press a button (input update type `Editor`, so
  `wasPressedThisFrame` never fires), which `FloorToolClickTests` and `InputHarnessTests` both carry
  as ignored tests. Un-ignore them together the day the harness can.
- **Work reaches `main` only through a pull request** with both tiers green, one approving review
  and the branch up to date. Branch protection enforces it, agents included. There is no long-lived
  feature branch — `claude/*` branches are per-change and short-lived.

**Before more features, open the seams.** The mining line was 73 files and had to edit six shared
files to add itself, five of which should have been extension points. The table and the order are in
`docs/plans/vertical-slice.md`, "Where the seams are" — **audited against the code on 2026-09-17,
because it had gone stale and misled a session into recommending work that had already landed.**
All five chokepoints named after the mining line are now open:

- **Work givers register themselves** (OQ-44): a giver in the simulation assembly joins by existing.
- **Content is written once** (OQ-15/OQ-16, finished by OQ-48 and OQ-49 on 2026-09-17). The XML
  under `Assets/Odyssey/Defs/Core` is the only copy of both the pawn tuning and the world tables;
  `PawnContent.Core()` and both `BuildTerrain()` methods are deleted. Callers go through
  `ContentPack.Pawns()` and `WorldContent.Table`, which find the pack by walking up to the
  repository root and cache it. A built player would not find it — nothing builds one, and
  `ContentPack.UseRoot` is the tested seam for the day something does.
- **A feature can describe a pawn without widening `PawnView`** (OQ-45, ADR 0004 amended): a sparse
  `PawnAspect` row keyed by a name the feature mints for itself.
- **The world answers a question about a cell** (2026-09-17, ADR 0004 amendment 2): a sparse
  `CellDetail` row — terrain, edifice, floor stuff, support, work to clear, crossing cost in
  thousandths of a clear crossing — published for the one asked-about cell by a sim-side
  contributor every colony gets. The question is a `QueryCell` intent, and **a paused world answers
  it by republishing the view without spending a tick** — the first time "intents flush while the
  clock is paused" (ADR 0004's own Decision) has been true of anything but a speed change. This is
  U16's readback arriving two milestones late: the pane had shipped the placeholder "cell readout
  arrives with cell inspection" since M1, and the owner's reports (rocks indistinguishable from
  grass, water silent about being water, piles generic with no count) are what opened it. The same
  change made water own its click in the picker and a pile resting on bare ground selectable — the
  pick resolves to the block under a pile, so `SelectionDirector` looks one cell up.
- **The scene composes its world the way everything else does** (U34, 2026-09-17), which is the
  simulation half of the bootstrap chokepoint. It had forty lines that were a copy of
  `ColonyWorld.Build`, and the copy had drifted: no connectors reached the nav graph, no full
  support solve ran, and it assembled no `SaveComponents` — so **the one world a player ran was the
  one world in the project that could not be written to a file.** It now calls
  `ColonyWorld.Build(ColonyRequest)`, and `OdysseyBootstrap.Colony` is what a save is written from.
- **A terrain kind brings its own meshing** (OQ-46, 2026-09-17). `ChunkMesher.EmitTerrain` was a
  chain of early returns — water, surface, an exposure gate, stone, earth, a plain block — so every
  terrain feature was an edit to a file on the do-not-touch list, and both the water line and the
  mining line edited it anyway. It is now a registered list of `ITerrainContributor`, walked in the
  chain's order with the first claim winning, and `AddTerrainContributor` inserts ahead of the plain
  block. **No output moved:** `ChunkMesherTests` passes unedited and OQ-03's counts are identical,
  512 buckets and 100,000 instances before and after.

**Content values are pinned by fingerprints, and they earn their keep.** `PawnContentDefTests` and
`WorldContentDefTests` each fold their loaded table into one literal. This is not belt-and-braces:
the moment the game started reading the world XML, the old oracle was comparing it against itself,
and **editing rock's `workToClear` from 700 to 701 left all 448 tests green** — measured, not
supposed. A deliberate content change is one line; an accidental one now fails.

**All five chokepoints are now open.** What is left of the bootstrap row is the **presentation half
of `OdysseyBootstrap`** — every director still wired by hand. It has no queue row; the rest of that
file's story is `MS` below.

### MS, the start flow, is scheduled (owner, 2026-09-17)

A main screen with **New game, Load, Options and Quit**; a seed you can see and reroll; three
candidate colonists with per-slot reroll and locks, each card showing a portrait and a readable
skill set; and a world you can enter, save, leave and load again. Eight units, `U34`–`U41`, in
`docs/plans/vertical-slice.md` — **beside M3 rather than inside it**, because M3's gate is a ten-day
headless run and this is session lifecycle, persistence and a screen. Seam work first, then the
menu on top, by the owner's decision. **`U34` and `U35` are done.** `U35` (2026-09-17) made world
build and teardown callable at runtime — `BuildSession()`/`TeardownSession()` — with the full hash
(board included, since OQ-50) proved equal across build → teardown → build against a separately
built rig. One half is deferred rather than faked: the figure-leak check cannot run in the rig with
no module catalogue (everything resolves to a shared Unity primitive, so there is nothing to leak),
and giving it a real catalogue would make a test depend on the licensed packs — so it `Assert.Ignore`s
with that reason recorded. **`U36` (save format v2) is done, Sim-only, 2026-09-17.** The header now
carries a `SaveRecipe` beside the seed/size/tick it always had — map type, scenario, colony name and
day — so a load screen can describe a save from its header alone; `WorldSave.ReadHeaderOnly` reads
exactly that, with no world and no component list. `CurrentFormatVersion` is 2; a version 1 file
still loads and reads back `SaveRecipe.Unknown` (map type `MapType.Unknown`, a value added for
exactly this, not a reuse of either real one). Files go through `WorldSave.SaveToFile` /
`LoadFromFile`, both path-taking because Sim has no `UnityEngine.Application.persistentDataPath` to
read — the `Saves` folder itself is left to the caller that wires the menu on top (`U38`–`U40`). Day
is likewise handed in already computed: `GameClock`'s tick-to-calendar mapping lives in the Hud
assembly and Sim must not reference it, so nothing here re-implements that conversion.

**`U38` is done, 2026-09-17, and it is not the unit the plan described.** Read
`docs/design/17-start-flow.md` before touching this line; §2 holds the owner's six decisions and two
of them overturn the plan's own row. There is **no new in-game menu** — the owner's ruling was
*"there is already an in game menu/settings — reuse that"* — so Save, Load and Quit to main menu
joined the **B17 settings panel** beside its exit row, the quit row did **not** move out of B17 as
`10-ui-panel-catalogue.md` predicted, and **`SettingsDirector.Escape` gained no case at all**,
because the start screen is not reached by Escape.

What was built instead is **the screen before the game**. `buildOnPlay` now defaults **false**, so
pressing Play lands on New game / Load / Settings / Exit game and a colony exists only once somebody
asks for one; the flag stays as the development loop, and every PlayMode rig now sets it explicitly
rather than inheriting it. It is **the project's first true modal**, which makes `09` §6 case 5 true
of something for the first time — and **the swallow is a pickable full-viewport scrim, not a flag**:
`PointOverUi` then answers "the interface" everywhere, and the camera rig already declines a press it
is told belongs to the interface. The two always-on contrast scrims are explicitly *not* pickable,
for the mirror-image reason. `HudShell.Modal()` is the one place a modal is made and returns scrim
and panel as a pair, because a scrim left showing over a hidden panel is a screen that eats every
click, shows nothing and cannot be dismissed.

**Save and Load work end to end**, which brought U36's unbuilt disk half here: `Saves` under
`persistentDataPath`, names derived from the recipe, and a listing read from headers alone that
**keeps an unreadable file with its reason rather than hiding it** — a save that vanishes from the
list is a colony the player concludes is gone. The one exempt window in the game is this screen's
root: it has nothing behind it to close *to*, so it carries no X, and `HudGeometryTests` names the
exemption rather than loosening the rule.

**The row set is data, in one table.** `SessionCommands` names every session-level command with its
key, context, order and whether it asks twice, and both surfaces build from it — the bargain
`HudCommands` already makes for the command bar. `SettingsDirector` now *reads* that flag rather than
stating it: its exit row had the two clicks written into `RequestExit`, and three more destructive
rows beside it would have been two answers to one question. `HudDirectors` no longer builds the
settings and hotkey directors, it **takes** them — they are preferences about the machine, not facts
about a colony, and the start screen exists precisely when no colony does.

**The owner played it on 2026-09-17 and four things came back, all now in.** (1) A worktree has no
art until `Assets/Synty/` is **junctioned** — gitignored, so a fresh worktree draws everything as
primitives; the procedure is in `docs/lessons.md` and **the junction must be removed with `rmdir`
before the worktree is, or a recursive delete follows it and empties the real packs.** (2) **The
view is in the save now** — camera focus, yaw, pitch and distance, the slice layer, the selected
colonist and the game speed, as a `"view"` section that is `ISaveable` and deliberately **not**
`IStateHashable`. The rule that nothing in presentation reaches the save was aimed at determinism,
and determinism is the hash's business; where the camera points cannot affect a tick. `SliceCameraRig`
gained `RestorePose`, because yaw and distance are lerped toward *private* targets and assigning the
public fields alone holds for one frame and then swings back. (3) **Settings is a screen of the start
menu**, not a panel over it — both are centred, so one landed on the other. (4) **The panel is a
fixed box**, 420 × 384, because a centred panel that resizes moves every row under the pointer; the
load list's ceiling is derived from that box rather than written down beside it, and a save row now
carries the date and time, since a folder is mostly repeated attempts at the same colony.

**Saves are named, and Save overwrites** (owner, 2026-09-17: *"I notice you keep saving a new game
everytime … otherwise lots of saves will be created"* — a fault `17-start-flow.md` §10 had already
admitted). A session is **bound** to a file: the one it was loaded from, or the one it last saved
to. **Save** writes over that; **Save as** asks for a name and binds to the answer; the first save
of a colony has nothing bound, so it asks, defaulting to the colony's name. **A player-chosen name
is never disambiguated** — that is the point, since a name that maps to one file is what makes
saving again an overwrite — so naming an existing save *is* an overwrite and the prompt asks twice.
**The trap it opened:** the old scheme was accidentally safe because every stem carried `-day-N`,
which is what kept a colony called `con` or `com1` off a Windows reserved device name
(`con-day-4.odyssey` is creatable, `con.odyssey` is not); a typed name has no `-day-`, so that guard
is now deliberate. The naming prompt is the project's **first text field** and the **first modal
over a running colony**.

**Save format is 3.** The recipe gained `Barren` and `Wooded`, because U38's round-trip test found
that `MapType` says "Natural" for three genuinely different boards and a header could not rebuild
the one it was written on. **The state hash could not have caught it** — the load overwrites every
cell, so the hashes agreed; what differed was the start cell the camera frames a loaded colony on,
so a restored game opened on empty ground a third of the map away. Versions 1 and 2 still load.

**`U38` is the one thing blocking the rest of the chain, and it has not moved.** `HudShell.Bar.cs`
still carries the comment that the B18 menu "does not exist yet", and there is no menu panel
anywhere in the presentation assembly — so there is nowhere for `U39`'s screen to attach.
**Check the code, not the plan's table, before starting anything downstream**: that table listed
`U36` and `U37` as available while both were still open, and then both landed mid-afternoon on
2026-09-17 while a branch was in flight against the earlier reading.

**`U39`'s seed logic landed without its screen** (2026-09-17): `SeedEntry` in
`Odyssey.Sim.Contracts` draws a seed, rerolls to one guaranteed different, formats it as plain
decimal and reads back what the player typed — the half of `U39` that needs no interface, and the
half that would otherwise live in a text field's callback where the fast tier could never reach it.
A seed is decimal because that is already the form the project prints one in; **free-text seeds in
the Minecraft idiom were considered and not taken**, because they only work if the typed text is
kept beside the number — now a `SaveRecipe` field, so it is a live question rather than a blocked
one. It is the one deliberately non-deterministic code in the simulation assemblies, confined to
two methods and reachable from no tick path.

Three things a later session should not re-litigate. **Live portraits** are refused by
`09-ui-and-input.md` §4.5 — but that argument is about fifty of them in the roster bar at 15 Hz
while the world renders, and the select screen is three, rendered once, with no world behind them;
§4.5 gets an explicit carve-out in `U41` rather than a silent exception. **Colonists start every
skill at 0 experience** — only passions are rolled — so there is nothing to choose between three
candidates until `U37`.

### What runs today

**Simulation** (`Odyssey.Sim`, `Odyssey.Sim.Contracts`, both UnityEngine-free) — grid, support
solver, two worldgen paths, pathfinding, pawns, save/load, Def loader, subsystem schedule, and the
snapshot-read / intent-write seam. Colonists walk, chop, mine, haul, eat and sleep, with needs, mood
and skills. A colony survives ten headless days on three seeds, and a 60,000-tick day ends on the
same hash under both Mono and CoreCLR.

**The board is a wooded meadow**, 120 x 120 x 16: grass everywhere, woodland at the natural
generator's density with a clearing at the start, streams and ponds, real 3 m terrace risers, and
rock, ore and sealed caverns beneath. `NaturalMapGenDef.MakeWooded()`, chosen by
`OdysseyBootstrap.woodedMap`; it is a *cover* mode, not the barren board with trees put back. The
bare board (`MakeBarren()`) is the test baseline on which anything that is not grass is a bug. The
ruined-city generator is still present and still tested, but it is not what the scene loads.

**Movement is walk, stair, ladder and a one-block hop.** Climbing was removed as a mechanic (owner,
2026-09-16): one block up into the column next door is a jump (`MoveCost.JumpUp` 135), one block
down off it is a drop (`Drop` 50), and anything deeper wants a ladder, which is built. Every cell a
pawn can be in has something under it. A hop is three seams that must agree — the cell search
(`PathFinder.RelaxHop`), the region graph (`NavGraph.TryHopEdges`) and the mover's price
(`MovementSystem.StepCost`); **a price the planner and the mover disagree about fails silently.**
Ladders are still climbed, so the climb *pose* is live presentation code.

**That warning is now enforced rather than remembered (2026-09-17).** `NavGraph.HopCost` is the
only place the price of a hop is decided, and all three seams call it —
`HopPriceHasOneOwnerTests` fails the fast tier if any other file in `Odyssey.Sim` names
`MoveCost.JumpUp` or `MoveCost.Drop` in code, and checks the built region graph prices a hop at the
owner's number rather than reading it off the source. Nothing was disagreeing when this landed:
all three named the constants for themselves and agreed **by coincidence**, which survives exactly
until the price stops being a constant. No golden moved, because who computes the number changed
and the number did not.

**Presentation** — instanced chunk rendering (no GameObject per cell), a slice camera rig, the HUD,
audio, a day/night cycle and golden-hour grading. No pack contains a work animation, so the axe,
pick and hammer strokes are **computed** (`WorkSwing`, `WorkStyle`) and stand in for art we do not
have.

**Colonists are 61 Synty characters, recoloured — not dressed.** No modular body exists in any
pack (a character is one skinned mesh from scalp to boots with one material), so clothing, hair and
skin are repainted by rewriting the atlas swatch rectangles each vertex is already mapped to
(`Odyssey/Character`, `ColonistMaterials`). Appearance derives from the world seed and the pawn id,
so a world deals the same people every load. **`OdysseyBootstrap.randomCastEachSession` defaults
on** while the palette is being judged, which means pressing Play deals new faces each time; switch
it off for a stable cast.

**The floor above you is drawn, and the cut-away is opt-in** (owner, 2026-09-17). The active layer
used to be drawn roofless always, so a floor built one layer up was invisible and — because a
surface that is not drawn must not be a pointer target — unclickable with it. `GraphicsOption.CutAwayCeiling`
is the switch and is **the first option in the panel that starts off**: seeing what you have just
built is the commoner need, so the specialist one (watching colonists indoors without changing
depth) asks. An explicit `Full`, the exterior view, still refuses to cut away even with the option
on, which is the invariant the test now asserts deliberately rather than by accident.

**What the player can see is decided by how deep they are.** At or above the surface, every layer
above is drawn solid; below it, one layer above is x-rayed and every layer below is drawn. Anything
drawn solid is clickable at any depth; a ghost never is. Whatever hides a selected colonist fades to
a ghost while it stands there. **The landscape is never cut away:** the band below the slice reaches
down to the lowest ground on the board (`WorldRenderModel.LowestOutdoorLayer`) or `belowDepth`
layers, whichever is lower, because the surface is terraced and spans five layers while the depth
budget is a cue for looking *through* something. A pit somebody digs is still governed by the
budget; a hillside never was.

**Nothing in presentation is in a cell, a save or the hash** — grass tufts, ground relief, banks,
chips, the surrounding land, sound and the see-through fade are all drawn and none are simulated.
That rule is load-bearing; keep it.

### Tests and gates

- **Fast tier** (`scripts/test-fast.sh`, ~11 s, no Unity): **626 Sim + 293 Hud**; Long tier **20**.
  **It compiles neither Presentation nor Editor** — only the two mirror projects — so a unit that
  touches the composition root or the HUD shell is unproven until Unity has compiled it, however
  green the 11 seconds look (`docs/lessons.md`).
- **The two tiers do not run the same NUnit**, and the fast tier's is newer. `Assert.Multiple` does
  not exist under Unity (a compile error, so the whole batch aborts before a test runs) and
  `Has.Count` throws on an interface-typed collection. Both cost a Unity run each on 2026-09-17;
  `docs/lessons.md` has them.
- **A frame is not a tick.** Waiting one Unity frame for a simulation effect is a flake — the
  composition root steps the sim on accumulated real time, so a frame holds zero or more ticks.
  Wait for the effect with a bounded loop (`docs/lessons.md`).
- **Unity tier** (`scripts/unity.sh test editmode`, authoritative) plus PlayMode, which is the only
  place frame time is measured — never an editor `camera.Render()` loop.
- **Content gates:** `python3 tools/wiki/build_wiki.py --check` and
  `python3 tools/wiki/emit_labels.py --check`. Both must pass before a content commit.

Frame time under the real player loop, against a 5 ms budget: meadow ~0.99 ms, city ~1.56 ms on an
RTX 5070 Ti at 640 x 480. The city's move from 0.88 to 1.56 ms is **unexplained** and still open.

**The instancing is bounded by variety, not quantity, and that is now measured** (OQ-03,
2026-09-17, `ChunkBucketScaleTests`). 20,000 wall cells of four stuffs over 64 chunks give **512
buckets** — exactly chunks x kinds x tints — and **100,000 instances**, 195 per bucket. The played
meadow submits `1502 draw calls, 34,961 instances, 104 chunks`, steady submit **0.22 ms a frame**.
**One inconsistency found and left alone:** the mesher draws five instances per wall cell whatever
is beside it, while the grid has 79,600 exposed faces rather than 80,000 — the 400 missing point
off the edge of the board. `TheWorldBoundaryIsNotAnExposedFace` applies that rule to terrain and
not to edifice walls. It is 0.4% of instances and costs nothing; the test pins today's numbers so
OQ-46 has a before, and so a deliberate fix reads as deliberate.

### Fixed decisions

- **Cell size: 2.5 × 2.5 × 3.0 m** (ADR 0002), irreversible. Half-heights and slopes are a drawing
  offset, never a cell.
- **Architecture: plain C# structure-of-arrays with Burst on measured hot paths** (ADR 0005),
  decided by a benchmark in which both candidates produced the identical state hash.
- **Determinism before threads.** Single-threaded fixed-tick sim with tick groups.
- **No multiplayer, ever.**
- *Subsystems* are simulation-side; *directors* are presentation-side. Do not unify the two words
  (`01-architecture.md` §3a).

### Top technical risk

**Pathfinding cost, and it is smaller than this section used to say.** 65% of the tick was A-star
on the D1 spike; the once-recorded explanation — futile searches for unreachable targets — was
**falsified by its own follow-up experiment** (only 14% of budget exhaustions were unreachable,
under 1% on a structured map), and the fix that worked was hierarchical search plus a better
heuristic.

**The re-run the ADR asked for has happened** (OQ-19, 2026-09-17; `TickBenchmarkTests`, ADR 0005
addendum). Measured on the real `SimWorld.Tick` rather than on a spike that mirrored it: a colony
of 50 on a 250 × 250 × 40 board costs **0.025 ms a tick**, and the same world under D1's replan
rate — one long-range path per tick — costs **0.438 ms, p95 1.253, with Pawns at 97.1%**. That is
**half the 0.88 ms the ADR estimated**, and it reverses the margin table's verdict: three ticks
discounted 4× for the target laptop leave 11.3 ms of a 16.6 ms frame rather than nothing. For
scale, `OneDay` on the board the scene actually loads runs at 0.003 ms a tick. Pathfinding is still
where the tick goes under load, but it is no longer a threat to the frame budget.

**What the same run found instead: the tick allocated, and most of it was a defect** (attributed
2026-09-17, `PathAllocationTests`, ADR 0005 addendum). The at-rest cost was **not the colony**: an
empty world with no systems, no pawns and no contributors allocated 67.4 bytes a tick and adding a
whole colony added nothing. It was `Intents.Drain(HandleIntent)` — a method group converting to a
**fresh 64-byte delegate every tick**, for a handler that never changes, paid by every tick of every
game. Holding it in a field took the colony from **76.7 to 11.0 bytes a tick** (about 4.6 MB a day
down to 0.66 MB). The rest is the served path's cell array, now demonstrated rather than guessed:
allocation rises with path length at **exactly 4.00 bytes per extra cell**, so a request costs
`≈32 + 4 × cells`. That one is **kept on purpose** — pooling it would make `ServedPath.Cells` valid
only until the next `Serve()`, which is safe by inspection today and would be silently wrong for the
first consumer who held it.

**The graph that search would run on is now measured** (OQ-18, 2026-09-17;
`NavGraphStatisticsTests`, and `d-04-pathfinding.md` §"Measured 2026-09-17"). At 250 × 250 × 40 the
region graph holds **24,141 regions on the wilderness and 23,240 on the city** — inside d-04's
"low tens of thousands" budget — but **only 6.8% and 38.8% of them are walkable**; the rest is
impassable rock kept as a substrate for rooms and atmosphere, carrying no links and excluded from
the district flood. An abstract search is therefore cheaper than the totals suggest. A full rebuild
is 168 ms (wilderness) and 124 ms (city), which is the all-dirty worst case and not a per-tick cost.
**d-04's stated reason for the budget was wrong** — it credited all-solid chunks allocating nothing,
and all-solid is exactly what allocates here; what really bounds the count is that a region never
leaves its 10 × 10 block, now asserted over every cell of both boards along with the guarantee that
no region spans two layers.

### Waiting on the owner

- **Nobody has pressed Play on the look work.** Every judgement about the day cycle, the golden
  hour, the hill wood and the colonist palette comes from contact sheets and `FrameTimeTests`. A
  sheet cannot say whether night is playable or whether the light steps at speed 3.
- **The rest of the icon art.** **Nineteen keys draw real art** as of 2026-09-17: the four
  commodities, three activity icons on the roster card (`ui.status.felling`, `.mining`,
  `.building`), and twelve of the thirteen skills, cut from **sheet 06** by
  `tools/icons/icons.py export` — the first sheet in the repository and the first time that tool's
  output has been loadable. The rest still draw an outlined square. `IconArt` resolves a key to a
  texture and falls back, so the HUD is correct at every stage in between and one icon can be
  judged in the running game. The other seven sheets go in `art-source/icons/sheets/` (that
  folder's README names them). **Open, and the owner's call:** the HUD draws icons at 16, 17 and
  30 px while ADR 0007 says not to draw pixel art below 32 — measured, 30 px reads, 17 px loses the
  grooves, 16 px goes to noise, and a **framed** sheet-06 tile at 17 px is mostly frame
  (`Logs/skill-icons.png`).
- **Nobody has pressed Play on the cancel tool or on right-click.** Both tiers are green and
  neither can say whether right-click disarms when the hand expects it to, or whether the six-pixel
  threshold separating a right-*click* from a right-*drag* is the right number — an orbit is a
  deliberate sweep, so it may want to be larger than the left button's. One number, judged at the
  keyboard (`docs/design/16-cancel-and-deconstruct.md` §6).
- **Nobody has pressed Play on the interface work either.** The roster card, the docked bars, the
  popovers, the Skills tab and the settings panel's new Keys and Audio tabs are all measured and
  none of them has been looked at. The Keys tab is the tallest panel yet — at 150 per cent
  interface scale on a 1080p screen it is within pixels of the screen height and may want the
  first max-height-and-scroll any panel here has carried.
- **The 29 proposed proper nouns** in `docs/design/proper-nouns.csv` await approval or veto.
- **Marsh reads as a sandy bank** — re-tint it greener or rename it.
- **The audio listener is on the camera**, 32–160 m up, while the catalogue authors ranges as ground
  distances. Either move the listener to the camera's focus or re-author the ranges; it changes how
  the whole game sounds.

### Known gaps

~~Felled trees, mined cells and building sites are not in the save (the designation grid is not
saved; the construction grid is).~~ **That was stale and is struck rather than quietly edited
(found 2026-09-17 by U38's round-trip test, which had to check).** `DesignationGrid` is
`ITickable, IStateHashable, ISaveable, ISnapshotContributor` and it is in
`ColonyWorld.SaveComponents` beside the construction grid — so designations are saved, hashed and
round-trip. **Check a claim in this section against the code before repeating it**; that one
outlived its own fix and would have sent somebody to build a thing that exists.

**Mining collapses things** (U29, 2026-09-17) — this section said it did not, and the three places
that deliberately declined to mark support dirty are all wired now. What is still missing is the
*injury*: a colonist rides a floor down, keeps a memory of it and is otherwise unharmed, because
there is no health model for `a-02`'s fall-damage number to act on. There is no fog of war, so a
sealed cavern is visible if the player scrolls the layer down.

**The scenario table is written twice** (U38): `OdysseyBootstrap.ScenarioFor` and
`SessionRoundTripTests.ScenarioByName` each map two `defName`s to a `ScenarioDef` by hand, because
nothing in `Odyssey.Sim` turns a scenario name back into one. It wants a real lookup. It is not
urgent — a scenario acts only at tick zero, so a loaded world is unaffected by getting it wrong —
and both copies say so out loud.

**And it covers what stands on the world, since 2026-09-17** — one level down from OQ-50 and found
the same way, by a test written to fail. `List<PlacedEdifice>` was owned by worldgen and by nothing
else: not an `ISaveable`, so **a wall a colonist raised was never written to the save at all** (on a
bare board it took handle 0 and a reload restored an empty list, leaving the cell pointing at
nothing), and not an `IStateHashable`, so **a wooden wall and a stone wall in the same cell hashed
identically**. `CellGrid` hashes `Edifice[cell]`, which is only an *index into that list*.
`EdificeSaveSection` is now both, registered in `ColonyComposition.AddColony` so every colony gets
it, and handed to the save through `ConstructionGrid.Edifices` — the class that appends a building
at run time is the one that passes on the means of writing it down. **All six golden hashes moved**
and `Golden.cs` carries the sentence saying why. `PlacedEdifice.Built` joined the record in the same
change: it is what makes "deconstruct our own buildings, not the ruined city's" a question that can
be asked.

**The state hash covers the world** (OQ-50, ADR 0005 amended 2026-09-17). It did not until then —
`CellGrid` is neither a tickable nor a system, which were the only two lists `ComputeStateHash`
walked, so mining a cell or felling a tree moved no hash and `WorldRoundTripTests` proved a save
round-tripped "exactly" using numbers that could not see the map. Found by OQ-05's own control,
which should have failed and did not. `SimWorldBuilder.AddHashable` is the third list;
`StateHashCoverageTests` names each field and requires an edit to move the hash, and asserts
`Support` stays *out* because it is derived and rebuilt on load.

**It recomputes the whole grid per call, deliberately.** The cell arrays are public and written
directly from dozens of places, so an incrementally maintained hash would be silently wrong the
first time anyone assigned to `Terrain[i]` without telling it — and a hash that wrongly says two
worlds are the same is worse than the gap it replaced. **The cost lands on the hash trace:** a
traced tick on a 60 × 60 × 16 colony is 2,803 µs against a 3.34 µs plain tick, so trace a window
rather than a day. Nothing in an ordinary run asks for the hash.
`HashTraceTests.TheCostOfTracingIsMeasuredRatherThanAssumed` prints the figure every run.

Cross-runtime determinism is now a standing test rather than a one-off measurement: the golden table
is asserted under CoreCLR in the fast tier and Mono in the Unity tier, and passes in both.

## Read this before losing an hour

`docs/lessons.md` collects the operational lessons that have already cost time once: the Unity batch run that finishes without exiting and locks the project, assembly definitions silently dropping implicit package references, why filtering tests saves nothing, and the working-method rules for parallel agents. **Add to it whenever something takes more than about ten minutes to diagnose.**

The two that come up daily:

- **Tests:** `scripts/test-fast.sh` while working (~1.7 s, no Unity), `scripts/unity.sh test editmode` before committing (authoritative).
- **Fix issues before adding features** (owner rule, 2026-09-15), and re-run after a fix to check the output *means something* — a plausibly wrong result is worse than an obviously broken one.

## Repository layout

- `docs/lessons.md` operational lessons (read it) · `docs/journal.md` the narrative record of how the build got here (why a decision was made, what was measured, what was later falsified) · `docs/brief.md` governing brief · `docs/research/` research files and `INDEX.md` · `docs/reference/screenshots/` reference images and descriptions · `docs/setup/local-dev.md` dev-machine setup (§8 for Windows). Later phases add `docs/design/`, `docs/adr/`, `docs/plans/`, `docs/milestones/`.
- The Unity project lives at the **repository root** (`Assets/`, `Packages/`, `ProjectSettings/`), created 2026-09-15 on the Windows dev machine (Unity 6000.3.24f1, Universal 3D template).
- `Assets/Odyssey/` the game assemblies: `Sim.Contracts`, `Sim` (both UnityEngine-free), `Tests/Sim`. `Assets/Editor/Odyssey/` editor tooling: `SyntyInventory.cs`, `SyntyImport.cs`, `VisualBlockScene.cs`. `tools/dotnet/` mirror projects for the fast test tier. `Assets/Synty/` licensed packs, ignored by git.
- `scripts/unity.sh` headless Unity wrapper (`inventory`, `test`, `exec`, `shot`, `open`, `which`; `shot` takes an optional method, e.g. `shot Odyssey.EditorTools.ScatterSheet.Shoot` for a contact sheet of candidate props) and `scripts/test-fast.sh` the no-Unity test tier.
- `docs/wiki/` the generated content wiki (read it, never edit it — see the section above). `tools/wiki/build_wiki.py` builds it; `tools/icons/icons.py` is the icon pipeline (detect, contact, export, validate, emit-web) with 30 tests via `python3 -m unittest discover -s tools/icons -t tools/icons`; `tools/mockups/artifact_body.py` makes a mockup publishable. All three are standard library only, so they run in a container with no Unity.
- `art-source/` owner-owned source art kept **outside** `Assets/` so Unity does not import it. `art-source/icons/sheets/` is where the eight icon sheets go.

## Environment

- **Dev machines:** Pop!_OS (Unity Hub, RTX 5070 Ti) and Windows 11 (`D:\code\odyssey`, Unity CLI/Hub beta — see `docs/setup/local-dev.md` §8). Both run Unity 6000.3.x LTS; neither is the performance target (that is a 2022 mid-range laptop). Keep the project path free of spaces (a Unity-MCP constraint).
- **dotnet SDK 8.0.425** is installed on the Windows machine at `%USERPROFILE%/.dotnet` and powers `scripts/test-fast.sh`. A remote container without Unity can still run every Sim test through it, given an SDK.
- **Python 3.13.15** is installed on the Windows machine as of 2026-09-16 (`%LOCALAPPDATA%\Programs\Python\Python313`, ahead of `WindowsApps` in PATH, with a `python3.exe` copy beside `python.exe` because CPython ships none). The wiki, icon and mockup tooling therefore runs on **both** machines now. `PYTHONUTF8=1` is set for the user and is required: without it Windows Python reads the docs as cp1252 and `build_wiki.py --check` calls every file stale. See `docs/lessons.md`.
- **Blender (optional):** only for gaps no Synty asset fills (a stair or ladder variant at the cell size, UV or atlas fixes, rig or animation retargeting). Synty first. Blender-made pieces go under `Assets/Art/Custom/` and are committed; they must match the Synty style and snap to the cell grid.
- **Unity MCP:** IvanMurzak/Unity-MCP, installed per `docs/setup/local-dev.md`. Once connected, Claude Code can open scenes, run EditMode/PlayMode tests, read the console and execute editor C#. Prefer `scripts/unity.sh` for anything that must also work in CI.

## Conventions for code (apply from Phase 4 / M0 onwards)

- C# with nullable enabled and analysers on. Assembly definitions per layer: Sim (no UnityEngine dependency where possible), Presentation, Editor, Tests.
- **Pragmatic TDD** (Phase 1 Q5): test-first for every Sim system; a determinism harness (same seed → same state hash) and golden-master one-day headless runs are first-class tests; presentation/tooling get smoke tests; throwaway spikes exempt until kept.
- **Determinism before threads** (Phase 1 Q6): single-threaded fixed-tick sim with tick groups; Burst jobs behind clean boundaries only on benchmark-proven hot paths. Composition root, no scattered manager singletons.
- Sim classes public, unsealed and virtual where cheap, so Harmony-style patching stays possible. Data-driven Defs with inheritance and patch operations from day one.
- Every system that touches a cell is layer-aware (x, y, z) from its first commit. No 2D-first code, ever.
- Scenes and prefab variants are generated by editor scripts, not hand-authored, so they are reproducible.
- Tests run headless via `scripts/unity.sh test`. Each milestone gate is: tests pass, a headless one-day simulation runs with no errors, `docs/milestones/Mx-report.md` written, stop for review.
- Commits: small, one concern each, descriptive message. Never commit `Assets/Synty/`, `Library/`, logs or test results.
- Interface icons are referenced by symbolic key, never by filename, and are 64 px, point-filtered, uncompressed, no mips, displayed at 32 and 64 only (`docs/adr/0007-pixel-art-icon-pipeline.md`).
- Content changes carry their regenerated wiki and label registry: `python3 tools/wiki/build_wiki.py --check` and `python3 tools/wiki/emit_labels.py --check` both pass before the commit.

## Starting a local session

Run `claude` in the repository root; this file is read automatically. Useful first prompts:

- "Read CLAUDE.md and docs/research/INDEX.md. Run the procedure in docs/research/synty-import.md, fix SyntyInventory.cs if it does not compile, commit the inventory, and report the implied cell size."
- "Phase 1 answers are: … Record them in docs/research/phase1-answers.md, update CLAUDE.md status, then start the Phase 2 lanes that do not depend on the inventory."
