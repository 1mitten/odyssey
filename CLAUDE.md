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

- **Phase 0 (ground): done remotely on 2026-09-15**, with two items blocked until run on the dev machine: the Synty import spike and the asset inventory. See `docs/research/phase0-ground.md` and `docs/research/synty-import.md`.
- **Phase 1 (interview): asked, unanswered.** Q1 additional Synty packs (assumed none). Q2 cell size (needs the inventory). Q3 ruined-city generation (assumed template stamping). Q4 Unity MCP server (recommended IvanMurzak/Unity-MCP, see `docs/research/unity-mcp-server.md`).
- **Phase 2 (research): not started.** Lanes A–F in brief §5. Lanes E and D3 wait for the inventory; everything else can start once Phase 1 is answered.
- **Cell size: not fixed.** Nothing is built until `docs/research/synty-inventory.md` exists and the owner confirms the size.

## Repository layout

- `docs/brief.md` governing brief · `docs/research/` research files and `INDEX.md` · `docs/reference/screenshots/` reference images and descriptions · `docs/setup/local-dev.md` dev-machine setup. Later phases add `docs/design/`, `docs/adr/`, `docs/plans/`, `docs/milestones/`.
- The Unity project lives at the **repository root** (`Assets/`, `Packages/`, `ProjectSettings/`), created on the dev machine per `docs/research/synty-import.md`.
- `Assets/Editor/Odyssey/` editor tooling (currently `SyntyInventory.cs`, uncompiled until first run). `Assets/Synty/` licensed packs, ignored by git.
- `scripts/unity.sh` headless Unity wrapper: `inventory`, `test editmode|playmode`, `exec <Namespace.Class.Method>`, `open`, `which`.

## Environment

- **Dev machine:** Pop!_OS, Unity Hub, Unity 6000.3.x LTS, RTX 5070 Ti (not the performance target; the target is a 2022 mid-range laptop). Keep the project path free of spaces (a Unity-MCP constraint).
- **Remote Claude Code container:** no Unity, no dotnet SDK, no Synty. Use it for research and documentation only, until the environment gains a dotnet SDK for pure-C# simulation tests.
- **Blender (optional):** only for gaps no Synty asset fills (a stair or ladder variant at the cell size, UV or atlas fixes, rig or animation retargeting). Synty first. Blender-made pieces go under `Assets/Art/Custom/` and are committed; they must match the Synty style and snap to the cell grid.
- **Unity MCP:** IvanMurzak/Unity-MCP, installed per `docs/setup/local-dev.md`. Once connected, Claude Code can open scenes, run EditMode/PlayMode tests, read the console and execute editor C#. Prefer `scripts/unity.sh` for anything that must also work in CI.

## Conventions for code (apply from Phase 4 / M0 onwards)

- C# with nullable enabled and analysers on. Assembly definitions per layer: Sim (no UnityEngine dependency where possible), Presentation, Editor, Tests.
- Sim classes public, unsealed and virtual where cheap, so Harmony-style patching stays possible. Data-driven Defs with inheritance and patch operations from day one.
- Every system that touches a cell is layer-aware (x, y, z) from its first commit. No 2D-first code, ever.
- Scenes and prefab variants are generated by editor scripts, not hand-authored, so they are reproducible.
- Tests run headless via `scripts/unity.sh test`. Each milestone gate is: tests pass, a headless one-day simulation runs with no errors, `docs/milestones/Mx-report.md` written, stop for review.
- Commits: small, one concern each, descriptive message. Never commit `Assets/Synty/`, `Library/`, logs or test results.

## Starting a local session

Run `claude` in the repository root; this file is read automatically. Useful first prompts:

- "Read CLAUDE.md and docs/research/INDEX.md. Run the procedure in docs/research/synty-import.md, fix SyntyInventory.cs if it does not compile, commit the inventory, and report the implied cell size."
- "Phase 1 answers are: … Record them in docs/research/phase1-answers.md, update CLAUDE.md status, then start the Phase 2 lanes that do not depend on the inventory."
