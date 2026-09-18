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
| **MS** the start flow | **Done**, `U34`–`U41`: a main screen, seed entry and reroll, three-candidate colonist select, save/load with a named binding, and flat avatars. Ran beside M3 because it is session lifecycle rather than colony mechanics. |
| **WS** rates | **Designed and planned; nothing is built.** `WS1`–`WS4` (renumbered from `U42`–`U45`, which were taken). A skill level currently buys nothing a player can feel. |

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
| Beds, furniture, quality tiers | `docs/design/20-beds.md` |
| The debug menu | `docs/design/18-debug-menu.md` |
| The start screen, saving, loading | `docs/design/17-start-flow.md` |
| Colonist select | `docs/design/18-colonist-select.md` |
| Avatars and portraits | `docs/design/20-avatars.md` |
| Tree colour | `docs/design/21-tree-colours.md` |
| Water, swimming, the float | `docs/design/20-swimming-and-water.md` |
| Work and move rates (WS) | `docs/design/17-rates-and-stats.md` |
| HUD regions, the orders strip, coverage | `docs/design/14-hud-layout.md` |
| The build cursor and its drag gesture | `docs/design/19-build-cursor.md` |
| Input cases, modality, live portraits | `docs/design/09-ui-and-input.md` |
| Panels | `docs/design/10-ui-panel-catalogue.md` |
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
graph and the mover must agree and a disagreement fails silently.

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

- **Fast tier** (`scripts/test-fast.sh`, ~20 s, no Unity): **681 Sim + 406 Hud**; Long tier **20**.
  **It compiles neither Presentation nor Editor**, so a unit touching the composition root or the
  HUD shell is unproven until Unity has compiled it, however green the seconds look.
- **Unity tier** (`scripts/unity.sh test editmode`, authoritative), last run 2026-09-18 on the fixed
  inspect pane, the dimmed build categories and the palette that closes on a selection: EditMode
  **1645 total, 1632 passed, 0 failed**. The remainder are `[Explicit]` or ignored.
- **PlayMode, last run the same day: 77 total, 72 passed, 0 failed — but before the two palette
  changes.** It could not be re-run: an editor was open on the worktree and `unity.sh` refuses to
  batch against a locked project, which is the right refusal and not a failure. PlayMode is the
  only place frame time is measured — never an editor `camera.Render()` loop.
  **Its previously recorded 74 was wrong, not superseded**: nothing under
  `Assets/Odyssey/Tests/PlayMode` has changed since the commit it was recorded against, no
  PlayMode test is parameterised, and this branch adds none. Three cases were miscounted or
  mis-transcribed into this file; 77 is measured. EditMode's 1638 was right and rose by exactly the
  seven tests this branch adds.
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
- **A skill level buys nothing a player can feel** — experience is complete and no rate reads it.
  That is the whole of WS.
- **Nothing tests that a click reaches the game.** A PlayMode test cannot press a button (input
  update type `Editor`, so `wasPressedThisFrame` never fires); `FloorToolClickTests` and
  `InputHarnessTests` carry ignored tests. Un-ignore them together the day the harness can. This is
  why this line of work has had three silent failures.
- **A hauler cannot climb a ladder**, so material cannot be carried up. Stairs (`U44`) are the next
  unit rather than a maybe.
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
