# Odyssey — project guide for Claude Code

Read this first, then `docs/brief.md` (the governing brief) and `docs/research/INDEX.md` (what is known so far). Update the **Current status** section whenever it changes.

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
```

`--check` is the gate and belongs in CI beside the test tiers. Two notes before extending it. The
registry is hand-authored **only until the Def set covers it**: then `icon-keys.csv` is generated
one way out of the Defs and committed, so the wiki and the build-gating icon tests share one
source. Do not create a second source of truth meanwhile. And the hosted copies are snapshots:
after a rebuild, republish `docs/wiki/artifact.html` and
`docs/reference/mockups/hud-v2.artifact.html` (regenerate the latter with
`python3 tools/mockups/artifact_body.py docs/reference/mockups/hud-v2.html`).

## Current status

- **Phase 0 (ground): complete 2026-09-15.** The import spike and asset inventory ran on the **Windows** dev machine: Unity 6000.3.24f1 LTS + URP 17.3.0 project at the repository root, five Synty packs imported headless under `Assets/Synty/` (7,222 assets, zero import errors). Record in `docs/research/synty-import.md`; measurements in `docs/research/synty-inventory.md`.
- **Phase 1 (interview): complete 2026-09-15** — all seven answers in `docs/research/phase1-answers.md`: packs supplied (Q1), cell confirmed (Q2), template stamping (Q3), IvanMurzak/Unity-MCP now (Q4), pragmatic TDD (Q5), two-way architecture benchmark with determinism-first threading (Q6), Windows machine primary (Q7).
- **Phase 2 (research): complete for the slice, 2026-09-15** — wave 1 (twelve files: Lane A pawns/jobs/building/mapgen/tick, Lane B Going Medieval, Lane C Cataclysm DDA, Lane E all four Synty files, Lane F prior art, which found a *Ramble* checkout at `D:\code\ramble`) plus wave 2 (A14 stockpiles, **D1 architecture benchmark**, D4 pathfinding, D6 save/load, D7 Defs, D8 CI). One-line results per file in `docs/research/INDEX.md`. Deferred until after the slice, by the Q8 fast-track: A2/A5–A11/A13/A16 and the remaining Lane B/C items. **Interface/UI research is owned by a separate agent session — do not duplicate it here** (`docs/design/ui-plan-reconciliation.md`).
- **Phase 4 (execution): M0 complete, M1 under way on branch `claude/m1-world`.** U01-U07 done, and the whole simulation stack runs: grid, support solver, two worldgen paths, pathfinding, pawns, save/load, Def loader, subsystem schedule, and the snapshot-read / intent-write seam. M1 presentation has instanced chunk rendering (no GameObject per cell), a slice camera rig, click-to-inspect and a generated play scene (`scripts/unity.sh exec Odyssey.EditorTools.PlayScene.Build`). **278 EditMode tests (276 green, 2 skipped) plus 2 PlayMode** (1.7 s fast tier, 37 s Unity gate, `unity.sh test playmode` for frame time). **Frame time is measured only by `FrameTimeTests` under the real player loop** — wooded meadow 0.41 ms, city 1.48 ms on the RTX 5070 Ti at 640 × 480 (2026-09-16, grass to the rim and trees) — never by an editor `camera.Render()` loop, which measures its own history (`docs/lessons.md`, "Benchmarking the renderer"). M1 report: `docs/milestones/M1-report.md`. Remaining in M0: **U08 CI**, which needs the owner to register a self-hosted runner. Plan: `docs/plans/vertical-slice.md`.
- **The starting map is a wooded meadow (owner decision 2026-09-16; barren before that):** 120 x 120 x 16, flat, grass in every cell, woodland at the natural generator's density with a clearing at the start, and no rock, ore or props — `NaturalMapGenDef.MakeWooded()`, chosen by `OdysseyBootstrap.woodedMap`. Trees are edifices that block nothing (a colonist walks through woodland) and felling them is the first job on the road to building. The bare board (`MakeBarren()`, `woodedMap` off) is kept as the test baseline on which anything that is not grass is a bug. The ruined-city generator is still present and still tested, but it is not what the scene loads. Tufts of grass are drawn over the ground as pure decoration by the mesher (`GroundScatter`, `OdysseyBootstrap.grassScatter`, default 120 per hundred cells, 0 to switch off); they are not simulation objects, block nothing and are not in the save.
- **Designations and the first job line, 2026-09-16.** The player's standing orders live in `Sim/Designations/DesignationGrid.cs` (one byte per cell; Mine, Deconstruct, Fell; validated on placement, hashed, saved, published as the snapshot's `Designations` channel one byte per cell of the active layer). Player commands reach the colony through the intent bus: `Designate(cell, A = kind)`, `CancelDesignation(cell)` and `SetForbidden(A = thing, B = on)` are handled by the components that own them via `SimWorldBuilder.AddIntentHandler`, and the colony is wired in one place, `ColonyComposition.AddColony`, used by `ColonyWorld`, the bootstrap and the screenshot harness alike. **Felling is the first job that edits the world:** `FellWorkGiver` (work type *cutting*, scanned before hauling) hands a marked, reachable tree to a colonist; `FellJobDriver` walks into the tree's cell, works `Job_Fell.workTicks` (600, **ASSUMED**), clears the order, and defers the edit to the structural phase, where the tree goes and `WoodPerTree` (20, **ASSUMED**) wood appears as one stack that the existing haul takes to the stockpile. What the colony starts with is a `ScenarioDef` (`Sim/Pawns/ColonyScenario.cs`: colonists, meal piles, meals per pile, beds, stockpile cells, salvage, `startingFellRadius`), built in code like `PawnContent.Core()`; the scene's `ScenarioDef.Playtest` marks every tree within 10 cells of the start before its first tick so the colony has work at once, `ScenarioDef.Bare` gives the same colony and no orders and is what headless runs and tests build on, and `OdysseyBootstrap.scenario` picks one by name — it flips to Bare when the UI line's drag-to-designate tool lands (that line owns the tool and reads the snapshot channel). Wood is `ItemIndex.Wood`, drawn as a log pile, named `ui.res.wood` in the registry with `ui.arch.tool.fell` for the order. Not yet: felled trees are not in the save (the grid itself is not saved, OQ-08), and stacks do not merge (OQ-24).
- **The HUD's first pass landed 2026-09-16,** the first built interface since the design docs: the Unity-free `Odyssey.Hud` assembly (roster, inspect, depth-ruler, ledger and calendar models behind ADR 0003's split, tested in `Odyssey.Tests.Hud` in both tiers) and a UI Toolkit shell `HudShell` styled by `Hud.uss` after the hud-v2 mockup, with every HUD region of the catalogue's screen map in place. **Live:** the colonist inspect pane (world click or roster card — needs, mood, tabs, commands), the roster bar, the clock, the speed buttons, Depth Ruler layer clicks, and the ledger's real rows. **Displayed for the look, disabled with a reason:** architect palette, main tabs, overlay toggles, alerts, cancel. Icons are deterministic placeholder badges keyed by icon key; the ADR 0007 pipeline replaces them when the sheets land. Clicks on HUD regions are gated from the world by `SliceCameraRig.PointerOverInterface`. **View-level behaviour is proven in the playmode gate** by `HudSmokeTests` — every region built, roster bound to the frame, selection answered by name, the player-loop half of experiment R4 in `g-02`; whether a panel also resolves under `-nographics` is still open. **The look itself still needs eyes: press Play in `Play.unity`** (regenerated with the HUD).
- **Sim vs UI vocabulary is deliberate:** simulation systems are *subsystems*, presentation-side coordinators are *directors* (`01-architecture.md` §3a). Do not unify the two words.
- **Phase 3 (design): complete 2026-09-15.** `docs/design/` 00, 01, 02, 03, 04, 05, 06, 07, 08; ADRs 0001, 0002, 0005; and the execution plan `docs/plans/vertical-slice.md` (32 units, M0→M3). **The Phase 3 → Phase 4 hard stop was cleared by the owner on 2026-09-15; execution is under way.**
- **Interface, icons and content naming (the UI line of work), 2026-09-15.** Design `09-ui-and-input.md`, `10-ui-panel-catalogue.md`, `11-icon-library.md`; ADRs 0003 UI framework, 0004 sim-to-UI contract, 0006 layer visibility, 0007 pixel-art icon pipeline; research `g-01`, `g-02`; mockups `hud-v1.html` (historical) and `hud-v2.html` (current). **Layer visibility decided:** x-ray by default with six modes shipped for playtest, amended by Lane B so that nothing above the active slice is ever a pointer target. **Icons:** 382 keys enumerated, 268 mapped to the owner's eight pixel-art sheets, 114 gaps listed in `11-icon-library.md` — the largest being people, since no sheet contains a human figure. **Names:** all 29 proper nouns proposed and awaiting the owner's veto, in `docs/design/proper-nouns.csv`.
- **Colonists are drawn from 61 Synty characters** across all four packs, chosen per pawn by id (`ColonistLook`), each with the locomotion clip set matching its name. The cast is the `Colonists` array in `PlayScene.cs` — strike a name and rebuild the catalogue to remove a face. Eight rigged-but-not-people prefabs (scarecrow, skeleton, robots, hologram, the two in underwear) are listed there as deliberately excluded.
- **Labels, not abbreviations (owner, 2026-09-16).** Until real icon art is in the build, every icon-bearing control draws its **full name** beside the icon; the two-to-four character badge is only a placeholder-tile and sub-32-pixel rendering rule, never the thing that names a control. Icon-only survives as a toggle and may become the default again on an owner screenshot call. Recorded in `09-ui-and-input.md` §7a (which closes open question D1), `10-ui-panel-catalogue.md`, ADR 0007; `hud-v2.html` now opens in *+ label* mode. Layouts are authored against the longest label, not the icon box.
- **Still needed from the owner for the UI line:** copy the eight icon sheets into `art-source/icons/sheets/` (that folder's README names them), and approve or strike the proposed names.
- **Cell size: FIXED at 2.5 × 2.5 × 3.0 m** (`docs/adr/0002-cell-size-and-layer-model.md`).
- **Architecture: FIXED — plain C# structure-of-arrays with Burst on measured hot paths** (`docs/adr/0005-simulation-architecture.md`), decided by a two-candidate benchmark in which both implementations produced the identical state hash. Plain 1.423 ms/tick vs ECS 2.446 ms at 250 × 250 × 40.
- **Top technical risk: pathfinding cost.** 65% of the measured tick is A-star. The explanation once recorded here — that these were futile searches for unreachable targets — was **falsified by its own follow-up experiment**: only 14% of budget exhaustions were unreachable, and under 1% on a structured map. `05-ai-and-jobs.md` and ADR 0005 were corrected. The fix is hierarchical search plus a better heuristic, with the district-id reachability check (`d-04-pathfinding.md`) measured first thing in M2. At a 3x hardware discount the tick leaves 3.8 ms of a frame for rendering; at 4x it leaves none.
- **Target: the playable MVP** (vertical slice, M0→M3) per `phase1-answers.md` Q8–Q9. Graphics bar: close to the concept renders (`d-03-rendering.md`). A look-check scene exists: `Assets/Scenes/Spikes/VisualBlock.unity` (regenerate via *Odyssey → Spikes*).
- Owner reference (2026-09-15): `github.com/RimWorldMods` for understanding RimWorld mechanics — clean-room rules apply, nothing is copied from it (note in `docs/research/INDEX.md`).

## Read this before losing an hour

`docs/lessons.md` collects the operational lessons that have already cost time once: the Unity batch run that finishes without exiting and locks the project, assembly definitions silently dropping implicit package references, why filtering tests saves nothing, and the working-method rules for parallel agents. **Add to it whenever something takes more than about ten minutes to diagnose.**

The two that come up daily:

- **Tests:** `scripts/test-fast.sh` while working (~1.7 s, no Unity), `scripts/unity.sh test editmode` before committing (authoritative).
- **Fix issues before adding features** (owner rule, 2026-09-15), and re-run after a fix to check the output *means something* — a plausibly wrong result is worse than an obviously broken one.

## Repository layout

- `docs/lessons.md` operational lessons (read it) · `docs/brief.md` governing brief · `docs/research/` research files and `INDEX.md` · `docs/reference/screenshots/` reference images and descriptions · `docs/setup/local-dev.md` dev-machine setup (§8 for Windows). Later phases add `docs/design/`, `docs/adr/`, `docs/plans/`, `docs/milestones/`.
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
- Content changes carry their regenerated wiki: `python3 tools/wiki/build_wiki.py --check` passes before the commit.

## Starting a local session

Run `claude` in the repository root; this file is read automatically. Useful first prompts:

- "Read CLAUDE.md and docs/research/INDEX.md. Run the procedure in docs/research/synty-import.md, fix SyntyInventory.cs if it does not compile, commit the inventory, and report the implied cell size."
- "Phase 1 answers are: … Record them in docs/research/phase1-answers.md, update CLAUDE.md status, then start the Phase 2 lanes that do not depend on the inventory."
