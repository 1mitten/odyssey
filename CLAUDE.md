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

## Current status

- **Phase 0 (ground): complete 2026-09-15.** The import spike and asset inventory ran on the **Windows** dev machine: Unity 6000.3.24f1 LTS + URP 17.3.0 project at the repository root, five Synty packs imported headless under `Assets/Synty/` (7,222 assets, zero import errors). Record in `docs/research/synty-import.md`; measurements in `docs/research/synty-inventory.md`.
- **Phase 1 (interview): complete 2026-09-15** — all seven answers in `docs/research/phase1-answers.md`: packs supplied (Q1), cell confirmed (Q2), template stamping (Q3), IvanMurzak/Unity-MCP now (Q4), pragmatic TDD (Q5), two-way architecture benchmark with determinism-first threading (Q6), Windows machine primary (Q7).
- **Phase 2 (research): complete for the slice, 2026-09-15** — wave 1 (twelve files: Lane A pawns/jobs/building/mapgen/tick, Lane B Going Medieval, Lane C Cataclysm DDA, Lane E all four Synty files, Lane F prior art, which found a *Ramble* checkout at `D:\code\ramble`) plus wave 2 (A14 stockpiles, **D1 architecture benchmark**, D4 pathfinding, D6 save/load, D7 Defs, D8 CI). One-line results per file in `docs/research/INDEX.md`. Deferred until after the slice, by the Q8 fast-track: A2/A5–A11/A13/A16 and the remaining Lane B/C items. **Interface/UI research is owned by a separate agent session — do not duplicate it here** (`docs/design/ui-plan-reconciliation.md`).
- **Phase 4 (execution): M0 nearly done.** U01 assemblies, U02 tick loop, U03 determinism harness, U04 Def loader, U05 save/load, U06 composition root, U07 sim-to-UI seam — **61 tests green** (1.7 s fast tier, 37 s Unity gate). Remaining in M0: **U08 CI**, which needs the owner to register a self-hosted runner. Then M1. Plan: `docs/plans/vertical-slice.md`.
- **Phase 3 (design): complete 2026-09-15.** `docs/design/` 00, 01, 02, 03, 04, 05, 06, 07, 08; ADRs 0001, 0002, 0005; and the execution plan `docs/plans/vertical-slice.md` (32 units, M0→M3). **The Phase 3 → Phase 4 hard stop is in force: no gameplay code until the owner approves.**
- **Cell size: FIXED at 2.5 × 2.5 × 3.0 m** (`docs/adr/0002-cell-size-and-layer-model.md`).
- **Architecture: FIXED — plain C# structure-of-arrays with Burst on measured hot paths** (`docs/adr/0005-simulation-architecture.md`), decided by a two-candidate benchmark in which both implementations produced the identical state hash. Plain 1.423 ms/tick vs ECS 2.446 ms at 250 × 250 × 40.
- **Top technical risk: pathfinding cost.** 65% of the measured tick is A-star, and most of that was futile searches for unreachable targets. The fix is the district-id reachability design (`d-04-pathfinding.md`), measured first thing in M2. At a 3× hardware discount the tick leaves 3.8 ms of a frame for rendering; at 4× it leaves none.
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
- `scripts/unity.sh` headless Unity wrapper (`inventory`, `test`, `exec`, `open`, `which`) and `scripts/test-fast.sh` the no-Unity test tier.

## Environment

- **Dev machines:** Pop!_OS (Unity Hub, RTX 5070 Ti) and Windows 11 (`D:\code\odyssey`, Unity CLI/Hub beta — see `docs/setup/local-dev.md` §8). Both run Unity 6000.3.x LTS; neither is the performance target (that is a 2022 mid-range laptop). Keep the project path free of spaces (a Unity-MCP constraint).
- **dotnet SDK 8.0.425** is installed on the Windows machine at `%USERPROFILE%/.dotnet` and powers `scripts/test-fast.sh`. A remote container without Unity can still run every Sim test through it, given an SDK.
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

## Starting a local session

Run `claude` in the repository root; this file is read automatically. Useful first prompts:

- "Read CLAUDE.md and docs/research/INDEX.md. Run the procedure in docs/research/synty-import.md, fix SyntyInventory.cs if it does not compile, commit the inventory, and report the implied cell size."
- "Phase 1 answers are: … Record them in docs/research/phase1-answers.md, update CLAUDE.md status, then start the Phase 2 lanes that do not depend on the inventory."
