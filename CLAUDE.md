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

## Every finished piece of work ends with a handover

**Owner rule, 2026-09-18:** *"could you put into each prompt — create a table of things to test
after completing work, stipulate a distinct table to explain changes made and how to test as I keep
losing track, and remind of the full folder."*

The owner is the only person who can press Play, and they are usually holding several branches at
once. A reply that ends in prose leaves them to work out where to go and what to look at. **So the
last thing in any reply that finishes a piece of work is a handover: the folder, then two tables.**
Not a summary of the conversation — the smallest thing somebody can act on cold.

### 1. Where

One line, before the tables: the **full path**, the branch, the PR, and whether the art is there.

> **`D:\code\odyssey-review-111`** — branch `claude/colonist-card-skills`, PR #114. Synty
> junctioned, packs imported. Press Play → New game.

Always the absolute path. There are a dozen worktrees on that machine (`git worktree list`) and
"the worktree" names none of them. Say if `Assets/Synty` is **not** junctioned, because without it
everything draws as untextured primitives and the first report back will be about the art.

### 2. What changed

One row per change a player could notice. **What it was, what it is, and where the decision lives**
— not the implementation.

| Change | Was | Is | Where |
|---|---|---|---|
| Selection outline stands clear of the face | border drawn hard against the portrait | 8 px pad, card 65 → 80 | `18-colonist-select.md` §6b |

Leave out anything invisible. A refactor with no player-facing effect belongs in the commit message,
not in this table — it is one of the things that makes the list too long to read.

### 3. What to test

One row per question **only a person at the keyboard can answer**, with what a wrong answer would
look like. This is the table that earns its keep: a test already says whether the geometry is right,
so do not ask for that again.

| Test | Look for | A wrong answer looks like |
|---|---|---|
| Pick a colonist without clicking each card | the three cards readable side by side | you still open each one to decide, so two skills is not enough |
| Reroll | name, face and skills all change together on an unkept card | one of the three lags, or a kept card moves |

Two rules for this table:

- **Never ask for something a test proves.** If the fast tier or the Unity tier can answer it, it is
  not a playtest item, and putting it there teaches the owner the list is padding.
- **State what failure looks like.** *"Check the cards read well"* is not actionable; *"if you still
  click each one to decide, two skills is not enough"* tells them what they are deciding and what to
  say back.

### The rest of the reply

Say what is **still owed** and what is **blocked on them** — an unshot screenshot, a Unity run that
cannot start because the editor is open, a merge waiting on review. And where more than one branch
is in flight, give the **merge order and the reason**, because that is the thing most easily lost
between sessions.

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
| `docs/design/colonist-names.csv` | the 244 colonist given names, with register and gender |

```
python3 tools/wiki/build_wiki.py            # rebuild docs/wiki
python3 tools/wiki/build_wiki.py --check    # exit 1 if stale; run before committing
python3 tools/wiki/emit_labels.py           # rebuild Assets/Odyssey/Hud/Registry.g.cs
python3 tools/wiki/emit_labels.py --check   # exit 1 if stale; run before committing
```

`emit_labels.py` generates **two** files from two CSVs — `Registry.g.cs` from the icon keys and
`ColonistNames.g.cs` from the name pool — so one script and one `--check` cover both. Adding a
third generated file goes in there rather than in a script of its own.

The HUD reads its labels from the same file: `emit_labels.py` generates `Registry.g.cs`
(`Registry.Label(key)`), `JobLabels` and `LedgerModel` name nothing themselves, and
`RegistryTests` fails the fast tier on any key the CSV does not know. A content commit runs both
checks.

**And the reverse is enforced too, since 2026-09-17** (owner: *"keep the consistent in the wiki and
the language and UI … ensure that consistency can be enforced using a centralised place"*).
`RegistryTests.NoPlayerFacingNameIsWrittenInCSharp` reads every C# file in `Odyssey.Hud` and
`Odyssey.Presentation` and fails on a string literal that equals a registry name in the six
namespaces where one thing is named on several surfaces at once (`ui.arch.tool.*`,
`ui.arch.category.*`, `ui.status.*`, `ui.res.*`, `ui.alert.*`, `ui.job.*`). **Do not answer it by
rewording the literal** — call `Registry.Label(key)`, or the wiki and the screen will disagree the
first time somebody corrects one of the two copies. It found two on the day it was written: the
seven Build category labels, and the armed banner's `"Building"`.

Both `--check`s are the gate and belong in CI beside the test tiers. Two notes before extending
it. The registry is hand-authored **only until the Def set covers it**: then `icon-keys.csv` is generated
one way out of the Defs and committed, so the wiki and the build-gating icon tests share one
source. Do not create a second source of truth meanwhile. And the hosted copies are snapshots:
after a rebuild, republish `docs/wiki/artifact.html` and
`docs/reference/mockups/hud-v2.artifact.html` (regenerate the latter with
`python3 tools/mockups/artifact_body.py docs/reference/mockups/hud-v2.html`).

## Current status

**This section is only what is true *now*.** The reasoning — every decision, measurement and
reversal — is `docs/journal.md`, and the mechanics are `docs/design/`. When something changes here,
write the *why* there and keep this short. It reached 982 lines once, 89% of a file that is read
into every session; that is the failure mode to watch for.

**Check the code before you trust any status line, here or in a plan.** `docs/plans/vertical-slice.md`
has misled three sessions into rebuilding work that had already landed, and a "known gap" in this
file outlived its own fix. A one-line grep is cheaper than a wasted session.

### Where the project is

Phases 0–3 (ground, interview, research, design) are complete. **Phase 4, execution, is under way.**

| Track | State |
|---|---|
| **M0** foundations | **Closed.** CI runs two tiers per push and PR: a *fast tier* on GitHub-hosted Linux (Sim, Hud, Long, both content checks) and a *Unity tier* on the owner's Windows machine as a self-hosted runner, switched on by the repository variable `UNITY_RUNNER=1`. |
| **M1** world, **M2** pawns | **Done and reported** — `docs/milestones/M1-report.md`, `M2-report.md`. Both went further than the plan asked. |
| **M3** build and dig | **Under way.** Designations, felling, stockpiles, mining, walls, deconstruction, floors and collapse, paving, ladders and beds are all in. Remaining: stairs (`U44`). The gate is a ten-day headless run. |
| **MS** the start flow | **Done**, `U34`–`U41`: a main screen, seed entry and reroll, three-candidate colonist select, save/load with a named binding, and flat avatars. Ran beside M3 because it is session lifecycle rather than colony mechanics. **The candidate card was re-derived 2026-09-18** (`18-colonist-select.md` §6b): it kept 47 px when the avatar doubled to 60, so the three faces overlapped, and its skills line had been squeezed out by the occupation — so the one screen whose job is telling three people apart showed nothing that varied by ability. The card is identity alone — name, age, occupation — at 76 px, which is the face plus its padding on both sides, and **a card is now asserted to clear its own avatar by that padding**; the skills live in the detail pane beside it, two columns and a heading. |
| **TS** terrace steps | **In review — PR #126**, branch `claude/terrace-foot-guard`. Nothing generates at the foot of a step any more (`TerraceFoot`, a sim-side copy of the bank rule checked cell-by-cell against `BankLayout`), and crossing one is priced and drawn against the path it is *drawn* along rather than against a flat cell: the foot cell is a **slope** costing what the hop out of it costs, `PawnPose.StepPace` spends each step's time where its climbing is, and a climbing figure is drawn on the ramp surface itself. Came out of four owner reports in two days; the arithmetic and every rejected alternative are in `docs/design/22-terrace-steps.md` §4b–4c. Three faults older than the work fell out of it: a 1.51 m teleport climbing a sheer face, its 657 mm mirror on a sheer drop, and a two-frame hitch at the start of every step costing more than a flat cell. |
| **WS** rates | **`WS1`–`WS3` in** (`WS1`–`WS4` renumbered from `U42`–`U45`, which were taken): the per-mille seam, work speed from the skill curve with the stroke clock scaled by it, and innate pace with starvation on both rates and collapse at zero rest. `WS4` running is **held** — do not invent an urgency model. Save format 6. **Reviewed and fixed 2026-09-18** (`docs/journal.md`): `ToilProgress` counts milliwork in **every** driver including the rate-free ones, or one saved and hashed field carries two units; the four accumulators are hashed **whole**, not divided back; `starvationPerInterval` was four times faster than its own comment (the needs cadence is 400 intervals a day, not 200); `RollSeed` is a property whose setter drops the cached pace; and arrival beats collapse, so a colonist cannot go down on her own bed and be told she slept on the ground. All three goldens re-baked — **measured** to be the hash seeing more rather than the colony doing anything different. |
| **RP** roster paging | **Done.** Overflow pagination with right-docked toolbar widget (`<` / `>`), mouse wheel page cycling, selection synchronization on 3D click/alerts, right-click drag-and-drop slot swapping (A ↔ B) with drag ghost and edge-paging, and view persistence in `ViewStateSection` v2. |
| **CL** the carried load | **Built, played once, three faults fixed; PR #129 ready to merge** (`docs/design/24-carrying.md`). A load no longer vanishes when it is picked up: it rides the palms, so it travels up out of the lift's crouch with the hands and lowers again on the stow. The stoop and the grasp instant were already there and are untouched. The arms take an authored scoop and the cradle is **measured off the palms**, not solved to a point — arm length varies across the 61 rigs by more than the cradle does. Sim side is one gesture report (`DropCarried` reports the stow, reversing a deliberate silence) and two sparse aspects; neither is saved or hashed, so **no golden moved**. The armful is constant whatever the stack, so the amount now lives on the activity line and nowhere else. **In water the load is hidden**, a placeholder the owner asked for by name. The carry path is **per-nothing**: a new commodity inherits the hold, the turn and both hand-overs, and only opts in to being drawn as an armful (§9a). |

**Work reaches `main` only through a pull request** with both tiers green, one approving review and
the branch up to date. Branch protection enforces it, agents included. `claude/*` branches are
per-change and short-lived; there is no long-lived feature branch.

### Read this before touching that line

Every live line has a design document that holds its decisions, its measurements and the things not
to undo by tidying. **Read the document before changing the code**, and add to it rather than to
this file.

| If you are touching | Read |
|---|---|
| Walls, sites, materials, the build botch | `docs/design/15-building.md` |
| Cancel, deconstruct | `docs/design/16-cancel-and-deconstruct.md` |
| The Build palette's three layouts | `docs/design/17-build-palette-layouts.md` |
| Floors, slabs, support, collapse | `docs/design/17-floors-and-collapse.md` |
| Paving | `docs/design/18-paving.md` |
| Beds, furniture, quality tiers, who owns one | `docs/design/20-beds.md` |
| How a pile on the ground says its size | `docs/design/24-pile-reading.md` |
| The debug menu | `docs/design/18-debug-menu.md` |
| The start screen, saving, loading | `docs/design/17-start-flow.md` |
| Colonist select | `docs/design/18-colonist-select.md` |
| Naming a colonist, the setup page | `docs/design/19-world-setup.md` §10 |
| What a colony starts with | `docs/design/22-starting-kit.md` |
| Text entry taking the keyboard | `docs/design/09-ui-and-input.md` §6a |
| Avatars and portraits | `docs/design/20-avatars.md` |
| Ladders, the shaft rule, the climb | `docs/design/21-ladders-and-climbing.md` |
| Tree colour | `docs/design/21-tree-colours.md` |
| Terrace steps, banks, what may stand at the foot of one | `docs/design/22-terrace-steps.md` |
| Ladders, the shaft rule, the climb pose, what a click may land on | `docs/design/21-ladders-and-climbing.md` |
| Water, swimming, the float | `docs/design/20-swimming-and-water.md` |
| Picking up, carrying, putting down, the armful | `docs/design/24-carrying.md` |
| Work and move rates (WS) | `docs/design/17-rates-and-stats.md` |
| HUD regions, the orders strip, coverage | `docs/design/14-hud-layout.md` |
| The build cursor and its drag gesture | `docs/design/19-build-cursor.md` |
| The white selection cursor sitting flush | `docs/design/23-flush-selection-cursor.md` |
| Input cases, modality, live portraits | `docs/design/09-ui-and-input.md` |
| Panels | `docs/design/10-ui-panel-catalogue.md` |
| Alert chimes, and what picks one | `docs/design/24-alert-sounds.md` |
| The carry sounds, and their mix | `docs/design/24-carrying.md` §12 |
| The title screen's bed, and the hand-over into a world | `docs/design/17-start-flow.md` §12 |
| The audio framework itself | ADR 0010, `docs/reference/audio-sourcing.md` |
| Icons | `docs/design/11-icon-library.md`, ADR 0007 |

### What runs today

**Simulation** (`Odyssey.Sim`, `Odyssey.Sim.Contracts`, both UnityEngine-free) — grid, support
solver, two worldgen paths, pathfinding, pawns, save/load, Def loader, subsystem schedule, and the
snapshot-read / intent-write seam. Colonists walk, chop, mine, haul, build, deconstruct, eat and
sleep, with needs, mood and skills. A colony survives ten headless days on three seeds, and a
60,000-tick day ends on the same hash under both Mono and CoreCLR.

**The board is a wooded meadow**, 120 × 120 × 16 — grass, woodland with a clearing at the start,
streams and ponds, 3 m terrace risers, and rock, ore and sealed caverns beneath
(`NaturalMapGenDef.MakeWooded()`). The bare board (`MakeBarren()`) is the test baseline on which
anything that is not grass is a bug. The ruined-city generator is still present and still tested,
but it is not what the scene loads.

**Movement is walk, stair, ladder and a one-block hop.** Climbing was removed as a mechanic (owner,
2026-09-16). Every cell a pawn can be in has something under it. **A hop's price has one owner** —
`NavGraph.HopCost`, enforced by `HopPriceHasOneOwnerTests`, because the cell search, the region
graph and the mover must agree and a disagreement fails silently. **Every step is priced against the
path it is drawn along, not against a flat cell** (2026-09-18/19). A terrace climb is one ramp
charged as two steps — the walk into the foot cell and the hop out of it — so the foot cell carries
a **slope** cost class worth `JumpUp − Orthogonal`, and both halves cost 240; `PawnPose.StepPace`
then spends each step's time where its climbing is, so the flats are walked and the ramp is climbed
at one speed. Walking *along* a terrace foot is slow too, and colonists prefer the flat line one
cell out: that is a decision, not a side effect. `docs/design/22-terrace-steps.md` §4b–4c.

**Presentation** — instanced chunk rendering (no GameObject per cell), a slice camera rig, the HUD,
audio, a day/night cycle and golden-hour grading. No pack contains a work animation, so the axe,
pick and hammer strokes are **computed** (`WorkSwing`, `WorkStyle`).

**Colonists are 61 Synty characters, recoloured — not dressed.** No modular body exists in any pack,
so clothing, hair and skin are repainted by rewriting atlas swatch rectangles. Appearance derives
from the pawn's own `RollSeed`, so the person on the setup card is the person who walks around. A
portrait is the actual character rendered once at 128 px and cached on the appearance, not the pawn.

**What the player sees is decided by how deep they are.** At or above the surface every layer above
is drawn solid; below it, one layer above is x-rayed and every layer below is drawn. Anything drawn
solid is clickable; a ghost never is. **The landscape is never cut away.** The cut-away ceiling is
opt-in (`GraphicsOption.CutAwayCeiling`), because seeing what you just built is the commoner need.

**Water is a body, not a lid.** A water cell emits a face wherever the thing beside it is not water
at the same level, never between two water cells. Falls carry downward-scrolling streaks and foam,
because at the play camera's 48° the Fresnel returns 2.2% and anything carried by the normal is
invisible where the game is played.

### Standing rules that are load-bearing

- **Anything fixed to the grid is draped; only what moves over it is lifted.** Came out of walls
  going up stepped: `ChunkMesher` lifted every panel to one height sampled at one point.
- **Nothing in presentation is in a cell, a save or the hash** — grass tufts, ground relief, banks,
  chips, the surrounding land, sound, tree colour and the see-through fade are all drawn and none
  are simulated. The one deliberate exception is the saved **view** (camera, slice, selection,
  speed), which is `ISaveable` and pointedly *not* `IStateHashable`: determinism is the hash's
  business and where the camera points cannot affect a tick.
- **But a façade that fills a cell the simulation can fill is a bug waiting to be reported.** The
  bank at the foot of a terrace step fills its cell floor to rim, and worldgen grew trees inside it
  (2026-09-18). Every other façade is drawn on ground that stays empty. Ask it of any new one: *can
  the simulation put something where this is drawn?* If it can, the rule needs a sim-side copy —
  `TerraceFoot`, checked cell-by-cell against `BankLayout` — and the guard goes where the thing is
  placed. `docs/design/22-terrace-steps.md`.
- **Content is written once.** The XML under `Assets/Odyssey/Defs/Core` is the only copy of the pawn
  tuning and the world tables. Callers go through `ContentPack.Pawns()` and `WorldContent.Table`.
- **Content values are pinned by fingerprints, and they earn their keep.** Editing rock's
  `workToClear` from 700 to 701 once left all 448 tests green. A deliberate content change is one
  line; an accidental one now fails.
- **A test that retunes content replaces the Def, never writes through it.** The Defs a record's
  arrays point at are shared by every record in the process, so a write-through silently retunes
  every test that runs afterwards.
- **Never hand-edit `docs/wiki/`** — it is generated. Edit the CSVs and rebuild; both `--check`
  gates must pass before a content commit.
- **Do not answer `RegistryTests` by rewording a literal** — call `Registry.Label(key)`, or the wiki
  and the screen will disagree the first time somebody corrects one of the two copies.
- *Subsystems* are simulation-side; *directors* are presentation-side. Do not unify the two words.

### Fixed decisions

- **Cell size: 2.5 × 2.5 × 3.0 m** (ADR 0002), irreversible. Half-heights and slopes are a drawing
  offset, never a cell.
- **Architecture: plain C# structure-of-arrays with Burst on measured hot paths** (ADR 0005),
  decided by a benchmark in which both candidates produced the identical state hash.
- **Determinism before threads.** Single-threaded fixed-tick sim with tick groups.
- **No multiplayer, ever.**

### Tests and gates

- **Fast tier** (`scripts/test-fast.sh`, ~20 s, no Unity): **741 Sim + 445 Hud**; Long tier **21**.
  **It compiles neither Presentation nor Editor**, so a unit touching the composition root or the
  HUD shell is unproven until Unity has compiled it, however green the seconds look.
- **Unity tier** (`scripts/unity.sh test editmode`, authoritative), last run 2026-09-19 on the
  alert chimes, the carry sounds and the title bed: EditMode **1857 total, 1843 passed, 0 failed**.
  PlayMode, the same day: **82 total, 77 passed, 0 failed**.
  `TheRosterOrderAndPageSurviveAStreamAndRestore` on save/load persistence and
  `TheRosterBarFollowsTheColonyIntoANewSession`.
  PlayMode is the only place frame time is measured — never an editor `camera.Render()` loop.
  **Its previously recorded 74 was wrong, not superseded**: nothing under
  `Assets/Odyssey/Tests/PlayMode` had changed since the commit it was recorded against, no PlayMode
  test is parameterised, and this branch added none at the point it was re-measured at 77. Three
  cases were miscounted or mis-transcribed into this file. EditMode's 1638 was right.
- **An editor GUI appears on the project moments after a batch run finishes**, twice on 2026-09-18
  (09:25:52 and 09:47:19, against runs ending 09:25:19 and 09:47:13), and it locks the project
  against the next `unity.sh` command. The cause is unestablished — Hub, the licensing IPC, or a
  person — so check `Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"` before concluding a
  batch run failed, and do not kill a process that might be somebody's open editor.
- **Player build** (`scripts/unity.sh build`, ~15 s, 386 MB into gitignored `Build/`): the only
  thing that compiles the *player* assembly set and the only thing that can fail on a stripped
  shader or a path under `Assets/` read at runtime. **Two green tiers say nothing about whether
  the game runs** — both compile and run in the editor's domain, where every shader and every
  variant exists always. Smoke-test it with `Build/Win64/Odyssey.exe -odyssey-newgame -logFile <path>`,
  which boots straight into a colony; a clean log from the main menu proves nothing, and that
  mistake cost three passes on 2026-09-19. Three separate faults had to be fixed before the first
  player drew anything: `ShaderInclusion` (runtime-found shaders), `ContentPackBuild` (the Defs,
  via `StreamingAssets`), `InstancingKeepAlive` (the `INSTANCING_ON` variant, which
  always-included does *not* keep) and `SyntyInstancingKeepAlive` (the same variant for the
  **pack's own** Shader Graph shaders, which no `Shader.Find` ever names and which arrive on
  prefabs with instancing off — staged for the build and deleted after, because a keep-alive for a
  licensed shader must never be committed). Each was invisible until the one before it was fixed.
- **Before diagnosing anything build-shaped, `git diff HEAD -- ProjectSettings/ Assets/Settings/`.**
  An uncommitted flip of URP's `m_StripUnusedVariants` to `0` once took one shader pass from 64
  variants to 884,736 and the build from 12 seconds to an estimated day and a half.
- **Content gates:** `python3 tools/wiki/build_wiki.py --check` and
  `python3 tools/wiki/emit_labels.py --check`. Both must pass before a content commit.
- The two tiers **do not run the same NUnit**, and the fast tier's is newer; **a frame is not a
  tick**. Both traps are in `docs/lessons.md` and both have cost a Unity run.

Frame time under the real player loop, against a 5 ms budget: meadow ~0.99 ms, city ~1.56 ms on an
RTX 5070 Ti at 640 × 480. The city's move from 0.88 to 1.56 ms is **unexplained** and still open.

**Pathfinding is where the tick goes under load, and it is no longer a threat to the frame budget**
(OQ-19, measured on the real `SimWorld.Tick`): a colony of 50 on 250 × 250 × 40 costs 0.025 ms a
tick, and 0.438 ms under D1's replan rate — half what ADR 0005 estimated. The once-recorded
"futile searches for unreachable targets" explanation was **falsified by its own follow-up**.

### Waiting on the owner

- **Nobody has pressed Play on the pile and bed clarity of 2026-09-19** (`claude/pile-and-bed-clarity`,
  `docs/design/24-pile-reading.md` and `20-beds.md` §13). A wood tile now draws one, two or three
  log bundles as it fills instead of one bundle for ever; the inspect title reads `Wood × 27`
  rather than saying the count in the pane's smallest line; the bed picker marks who sleeps here
  (`✓`), who sleeps elsewhere (`•`) and who has nowhere (blank); and a colonist who reaches an
  unowned bed claims it, **unless claiming it would leave a bedless colonist without one**.
  A **second** click on a cell now looks past what is lying in it and shows the tile, and a third
  comes back round to the thing. And a bed is clickable **where it is drawn**: the picker resolved
  a non-occluding cell at its floor plane while the bed stands 0.70 m up, which at 48° put the
  clickable bed a quarter of a cell behind the drawn one (`docs/bug-patterns.md`).
  Open questions a picture cannot answer: whether three bundles read as a full tile or merely as
  "some wood", whether the title is findable where the state line was not, whether the wordless
  mark column reads or wants its words back, and whether the second click reads as a cycle or as
  the game ignoring the first one. Unity EditMode **1871 total, 1857 passed, 0 failed**; PlayMode
  **82 total, 77 passed, 0 failed**.

- **The carried load has had one playtest and passed** (2026-09-19, `docs/design/24-carrying.md`).
  Three faults found and fixed — swinging arms, a load that would not turn, and both hand-overs
  snapping — and the owner is happy to merge. What is still unjudged: the seven `CarryPose`
  angles; whether a single wood bundle reads at true scale, which is the choice made over
  enlarging it; and whether losing the amount from the arms is missed now that only the activity
  line carries it. **The water case is a knowing placeholder** — the load vanishes as she wades in
  and returns as she climbs out, and whether that pop is worse than the swinging bundle it
  replaces is the question it exists to ask.
- **Nobody has played the new starting kit** (2026-09-18, `docs/design/22-starting-kit.md`): 36
  meals, no scrap, 150 each of stone and wood. Five integers in one method with nothing deriving
  from them, and explicitly invited tuning. The question a test cannot answer is whether three or
  four days of food reads as tension or as anxiety — and if the colony is starving before anybody
  has built anything, the answer is more meals rather than faster growing.
- **Nobody has renamed a colonist at the keyboard** (`19-world-setup.md` §10). Whether clicking the
  name is a discoverable way to rename somebody without a pencil or a caption, and whether sixteen
  characters is the right ceiling — it was picked for the roster strip, which is the narrowest place
  a name is drawn, not for the card where it is typed.
- **Nobody has pressed Play on the look work.** Every judgement about the day cycle, the golden
  hour, the hill wood and the colonist palette comes from contact sheets and `FrameTimeTests`.
- **The avatars and portraits are photographed, not played** — whether a 128 px render reads at
  26 px on a roster card, whether head-bone framing suits all 61 bodies, whether the one key light
  wants a fill.
- **Nobody has pressed Play on the build botch**, and its two integers are invited tuning: a novice
  botches about one wall in seven, a level-3 builder never does. Nothing announces a botch, so a
  wall that takes twice as long looks like a slow colonist.
- **Nobody has pressed Play on the orders strip, the armed banner, the cancel tool, right-click, the
  debug menu, or the interface work** (roster card, docked bars, popovers, Skills tab, Keys and
  Audio tabs). All are measured; none has been looked at. Open questions a picture cannot answer:
  whether a 34 px button is the right size, whether the strip wants to sit lower, whether the
  six-pixel right-click threshold is right, and the **20% coverage ceiling**, which is the owner's
  to reverse.
- **A floor is drawn as a sheet now, and the lip is the thing to look at.** The dotted line along
  every floor seam was the tile's rim tying with its neighbour's top face on depth
  (`docs/bug-patterns.md` P8), and it is gone — measured, 470 → 16 artefact pixels at the play
  camera. The price is that a floor **over open air** has lost its 101 mm of drawn thickness, so a
  balcony or a roof lip with no wall under it may read as paper seen edge-on. A floor on the ground
  had 93 of those millimetres buried and is unchanged. If the lip is wrong, the fix is a fascia on
  the face rather than a thicker plate.
- **Nobody has pressed Play on the three HUD fixes of 2026-09-18.** The colonist pane is one height
  on every tab now, so Needs sits in a box sized for Skills with about ninety-eight pixels of slack
  below it — whether that reads as stable or as broken is the question, and if it is broken the
  answer is more needs rather than a shorter box. The four empty Build categories are dimmed:
  whether they read as "coming later" or as broken tiles. And the palette now closes the instant a
  selection is made, which no still can tell you is decisive rather than startling — if it startles,
  the cheapest alternative is closing it only when the pane would actually overlap.
- **The coloured wood has had two playtests; the rounds since have not been played** — the cherry
  and flame canopies read as scarlet at the play camera and are the first to veto, and the measured
  tenth-of-a-stop the new shader costs was deliberately not papered over with a gain.
- **The climb has been drawn three ways in two days and only the third is unseen.** A parabola over
  the lip read as jumping; strides up the treads read as jolting; it is now the ramp surface itself,
  sampled where the figure stands, at 9.9–12.3 mm a frame (`docs/design/22-terrace-steps.md` §4b).
  If anything still jitters, the one junction left is where the ramp's 0.62 m/s meets the flat top's
  1.5 m/s — one 30 mm frame — and the honest fix there is a slower flat, not a smoother curve.
- **The whole terrace climb is now eight seconds and nobody has watched one.** Two steps of 240:
  flat ground at a walk, 3.9 m of ramp at 0.62 m/s in four strides, then the top at a walk again
  (`docs/design/22-terrace-steps.md` §4c). The lever is `MoveCost.JumpUp` — the slope cost, the
  pacing weight and the stride count are all derived from it. Also unwatched: colonists preferring
  a flat detour to walking along the foot of a terrace, which is the deliberate consequence of
  pricing that cell as a slope.
- **The new hop wants the same look the old one just failed.** `MoveCost.JumpUp` went 135 → 240 and
  the motion became an arc (`docs/design/22-terrace-steps.md` §4b) because a colonist climbed a
  terrace at 1.74 m/s against a walk's 1.50. The open questions a still cannot answer: whether 4.0 s
  to get up one block now reads as effort or as **stuck** — the exact failure of the 270 this
  replaces — whether 0.35 m over the lip is a hop or a hurdle, and whether holding the gait through
  the step shows as the feet sliding during the half-second gather. If it reads as stuck, the pose
  is the thing to look at before the price.
- **Nobody has seen a colonist climb a ladder since the pose was written for one.** Four angles
  branch on a ladder against a rock face and all four are invited tuning
  (`docs/design/21-ladders-and-climbing.md` §3): whether they read as a ladder rather than a shrug,
  whether a step of 0.46 of a leg is too big at the play camera, and whether arriving in an open
  shaft cell and stepping sideways looks like arriving or like hovering.
- **Nobody has heard the alert chimes.** Five of the owner's recordings replaced the synthesised
  two-note sine on 2026-09-19, loudness-matched to −18 LUFS. Two of them play today: `alert-normal`
  when an idle-colonists row appears, `alert-negative` when a starving or breaking one does.
  Questions a measurement cannot answer: whether −18 LUFS is right in a quiet room against the
  ambience bed and the work sounds, and whether the raid siren at 9.54 s and the joining fanfare at
  5.77 s are alerts or cutscene stings — the bake deliberately did not shorten them
  (`docs/design/24-alert-sounds.md` §6). **The chime had effectively never fired before this**: the
  audio side carried a starvation threshold on a scale a hundred times out, so there is no prior
  impression to compare against.
- **Nobody has heard the title screen.** A 151-second loop fades in over eight seconds on the
  main screens, fades out over four when a world arrives, crosses with the outdoor bed's own
  four-second arrival, and never plays in a colony. It is a **sub-bass drone** — almost everything
  below 500 Hz — so it will read completely differently on laptop speakers from headphones, and
  that is the first question to ask if the level seems wrong. Open: whether Volume 0.18 survives
  real speakers, whether eight seconds of arrival is patient or broken, and whether the four-second
  hand-over is seamless or a hole (`docs/design/17-start-flow.md` §12).
- **Nobody has heard a colonist pick anything up.** One recording became two sounds on
  2026-09-19 — `carry-lift` resampled up and brightened, `carry-drop` down and dulled, three takes
  each — and they fire on every leg of every haul, which makes the mix the whole question. They
  sit at Volume 0.40 against the axe's 0.85 and die at 120 m against its 200. Open: whether 0.40
  survives six haulers rather than one; whether lift and drop are actually told apart at the
  default camera height, which is not the same test as telling them apart side by side; and
  whether the drop wants to be heavier still (`docs/design/24-carrying.md` §12).
- **Nobody has seen the falls move.** Whether the streaks read as falling water or as a pattern
  sliding down a pane cannot be judged in a still, and stills are all anybody has looked at.
- **The shallow stream reads pale at the play camera.** Raising the alpha is the obvious fix;
  darkening the submerged bed is the better one. It changes water that has already been judged.
- **The shoreline jitter is unconfirmed either way.** The obvious explanation was falsified by
  measurement and the remaining candidate — the footing hand-over — is now continuous, so the
  report stands until somebody walks a colonist along a shore and looks
  (`docs/design/20-swimming-and-water.md`).
- **The bank ramps draw as large diagonal sheets standing proud of the meadow** — the owner's
  "diagonal wedge", proved to be `BankLayout`/`BankMesh`'s own design and not water. The most
  visible thing on the board, left unfixed on purpose because it wants its own look at.
- **Marsh reads as a sandy bank** — re-tint it greener or rename it.
- **The audio listener is on the camera**, 32–160 m up, while the catalogue authors ranges as ground
  distances. Either move the listener to the camera's focus or re-author the ranges.
- **Icon art:** nineteen keys draw real art; the rest draw an outlined square. The HUD draws icons
  at 16, 17 and 30 px while ADR 0007 says not to draw pixel art below 32 — measured, 30 px reads,
  17 px loses the grooves, 16 px goes to noise.
- **The 29 proposed proper nouns** in `docs/design/proper-nouns.csv` await approval or veto.
- **The Synty junction chain wants inverting.** The only real copy of the licensed packs sits inside
  `D:\code\odyssey-audio`, a worktree on a merged branch; the main checkout junctions to it. See
  `docs/lessons.md` — do not prune a worktree without checking.

### Known gaps

- **No health model**, so fall damage is designed with a number and nothing to apply it to, a
  colonist rides a collapsing floor down unharmed, and the debug menu has no kill or heal.
- **No fog of war**, so a sealed cavern is visible if the player scrolls the layer down.
- **A colonist can still lie down inside a terrace bank.** Trees are guarded out of those cells at
  generation (`TerraceFoot`, `docs/design/22-terrace-steps.md`), and a walking figure is lifted onto
  the ramp, but a body lying down is not: sleep on the ground at the foot of a step and the façade
  hides you. §4 of that document holds the two candidate fixes and why neither was guessed at — both
  move the state hash. An item dropped in one has the same problem and is unreported.
- **A skill level buys nothing a player can feel** — experience is complete and no rate reads it.
  That is the whole of WS.
- **Nothing tests that a click reaches the game.** A PlayMode test cannot press a button (input
  update type `Editor`, so `wasPressedThisFrame` never fires); `FloorToolClickTests` and
  `InputHarnessTests` carry ignored tests. Un-ignore them together the day the harness can. This is
  why this line of work has had three silent failures.
- **A hauler cannot climb a ladder**, so material cannot be carried up. Stairs (`U44`) are the next
  unit rather than a maybe.
- **A ladder now needs a hole left in the floor above it** (2026-09-18,
  `docs/design/21-ladders-and-climbing.md`). A ladder under an unbroken slab is refused at the order,
  and so is a slab poured over a standing ladder: the shaft cell stays open and the colonist steps
  off sideways on to the landing beside it. Nothing migrates — a real floor still counts, so old
  saves and the city's own ladders are untouched — but a player who builds a full upper floor first
  must deconstruct one slab before the ladder will go in.
- **The scenario table is written twice** — `OdysseyBootstrap.ScenarioFor` and
  `SessionRoundTripTests.ScenarioByName` each map two `defName`s by hand. Not urgent (a scenario
  acts only at tick zero) and both copies say so.
- **Forced orders have their simulation half only** — steps 3 and 4, the right-click/drag split and
  the context-menu panel, are not started, so nothing in the running game can send one.
- **The presentation half of `OdysseyBootstrap`** is still wired by hand. The simulation half was
  opened by `U34`; this is what is left of that chokepoint.
- **The design documents collide on numbers.** Four pairs share a `15-`/`17-`/`18-`/`19-`/`20-`
  prefix, and the unit numbers `U42`–`U45` meant two different things until 2026-09-18. Renaming
  files would break every cross-reference; it is recorded rather than fixed.

## Read this before losing an hour

`docs/lessons.md` collects the operational lessons that have already cost time once: the Unity batch run that finishes without exiting and locks the project, assembly definitions silently dropping implicit package references, why filtering tests saves nothing, and the working-method rules for parallel agents. **Add to it whenever something takes more than about ten minutes to diagnose.**

**`docs/bug-patterns.md` is the companion for the bugs themselves** — the symptom, the real cause, the
measurement that found it, and the check that catches the next one of its kind. **Read its patterns
before debugging a report**, because this project keeps meeting the same four faults in different
clothes: one rule with two owners; a rule that asks the built world and misses the order; a
compatibility clause keeping the bug alive; and a conditional rule applied per cell across a drag.
**Add a row whenever a bug is fixed.**

**For a report about how something *looks*, start at that file's runbook, "a tile that looks wrong".**
One grey tile cost four rounds, three of which produced confident wrong answers reasoned from
screenshots while the save that settled it sat on the same disk. The first move is
`dotnet run --project tools/dotnet/Odyssey.SaveProbe` — it prints every floor, item and terrain in a
save with no Unity. **A cell can hold more than one drawable thing**, and the report will name only
the one the player recognises: that tile was a wood floor *and* rubble terrain, and the pane calling
it "Wood floor" was telling the truth.

The two that come up daily:

- **Tests:** `scripts/test-fast.sh` while working (~1.7 s, no Unity), `scripts/unity.sh test editmode` before committing (authoritative).
- **Fix issues before adding features** (owner rule, 2026-09-15), and re-run after a fix to check the output *means something* — a plausibly wrong result is worse than an obviously broken one.

## Repository layout

- `docs/lessons.md` operational lessons (read it) · `docs/bug-patterns.md` the bug-pattern catalogue and fix register (read it before debugging a report) · `docs/journal.md` the narrative record of how the build got here (why a decision was made, what was measured, what was later falsified) · `docs/brief.md` governing brief · `docs/research/` research files and `INDEX.md` · `docs/reference/screenshots/` reference images and descriptions · `docs/setup/local-dev.md` dev-machine setup (§8 for Windows). Later phases add `docs/design/`, `docs/adr/`, `docs/plans/`, `docs/milestones/`.
- The Unity project lives at the **repository root** (`Assets/`, `Packages/`, `ProjectSettings/`), created 2026-09-15 on the Windows dev machine (Unity 6000.3.24f1, Universal 3D template).
- `Assets/Odyssey/` the game assemblies: `Sim.Contracts`, `Sim` (both UnityEngine-free), `Tests/Sim`. `Assets/Editor/Odyssey/` editor tooling: `SyntyInventory.cs`, `SyntyImport.cs`, `VisualBlockScene.cs`. `tools/dotnet/` mirror projects for the fast test tier. `Assets/Synty/` licensed packs, ignored by git.
- `scripts/unity.sh` headless Unity wrapper (`inventory`, `test`, `build`, `exec`, `shot`, `open`, `which`; `shot` takes an optional method, e.g. `shot Odyssey.EditorTools.ScatterSheet.Shoot` for a contact sheet of candidate props) and `scripts/test-fast.sh` the no-Unity test tier.
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
