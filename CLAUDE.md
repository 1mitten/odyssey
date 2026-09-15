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
called. Hosted copies: [wiki](https://claude.ai/artifact/JsYRQk1vnza2wFWSNfpQwr) ·
[HUD mockup](https://claude.ai/artifact/PcHWoujGDvd4rzcH1LAwFG).

**The rule: any commit that changes game content updates the wiki in the same commit.** Game content
means anything a player could see named — a new commodity, a renamed building, a reworded
description, a new alert, a faction, a creature, a research project, a month of the calendar. Design
and mechanics are not wiki content; they stay in `docs/design/` and `docs/adr/`, and the wiki
cross-references them rather than duplicating them.

**Never hand-edit anything under `docs/wiki/`.** It is generated and your edit will be silently
overwritten. Edit the source, then rebuild:

| Source | Holds |
|---|---|
| `docs/design/icon-keys.csv` | the name, namespace, milestone and description of every named thing |
| `docs/design/icon-map.csv` | whether the owner's art can draw it |
| `docs/design/proper-nouns.csv` | people, places, factions, creatures, the calendar |

```
python3 tools/wiki/build_wiki.py            # rebuild docs/wiki
python3 tools/wiki/build_wiki.py --check    # exit 1 if docs/wiki is stale; run before committing
```

`--check` is the gate. It must pass before any commit that touched the three sources above, and it
belongs in CI the moment CI exists. A content reference nobody trusts is worse than none.

Two things to know before extending it. The registry is hand-authored **only until M0**: once the Def
loader exists, `icon-keys.csv` is generated one way out of the Def set by an editor command and
committed, so the wiki and the build-gating icon tests share one source. Do not create a second
source of truth in the meantime. And the hosted copies are snapshots: after a rebuild, republish
`docs/wiki/artifact.html` and `docs/reference/mockups/hud-v2.artifact.html` (regenerate the latter
with `python3 tools/mockups/artifact_body.py docs/reference/mockups/hud-v2.html`) to the URLs above.

## Current status

- **Phase 0 (ground): done remotely on 2026-09-15**, with two items blocked until run on the dev machine: the Synty import spike and the asset inventory. See `docs/research/phase0-ground.md` and `docs/research/synty-import.md`.
- **Phase 1 (interview): asked, unanswered.** Q1 additional Synty packs (assumed none). Q2 cell size (needs the inventory). Q3 ruined-city generation (assumed template stamping). Q4 Unity MCP server (recommended IvanMurzak/Unity-MCP, see `docs/research/unity-mcp-server.md`).
- **Phase 2 (research): not started.** Lanes A–F in brief §5. Lanes E and D3 wait for the inventory; everything else can start once Phase 1 is answered.
- **Cell size: not fixed.** Nothing is built until `docs/research/synty-inventory.md` exists and the owner confirms the size.
- **Lane G (UI) done out of phase, 2026-09-15.** At the owner's request. The reason it did not wait: the interface's read and write contract constrains the Lane D1 architecture decision, so it is an input to that ADR rather than an output. It also eliminates one of the three Lane D1 candidates. See `docs/design/09-ui-and-input.md` §2.5 and `docs/adr/0002-sim-ui-contract.md`. Still documentation only; no code was written.
- **Layer visibility decided, 2026-09-15.** X-ray by default, with six above-slice modes, a depth cap and a below-slice treatment all shipped so it can be playtested. `docs/adr/0003-layer-visibility-policy.md`. This answers half of brief layer question 9; the rendering mechanism is still Lane D3's to benchmark, and the ADR records why a hard clip plane is now ruled out.
- **Icon library mapped, 2026-09-15.** The owner's eight pixel-art sheets are the prototype's real HUD art. 382 icon keys enumerated in `docs/design/icon-keys.csv`, 268 mapped to sheet cells in `docs/design/icon-map.csv`, 114 gaps listed in `docs/design/11-icon-library.md`. `docs/adr/0004-pixel-art-icon-pipeline.md` replaces the 128-pixel authoring rule with 64, point-filtered, after finding that 128 was not atlas-eligible at all. **Two things still need the owner:** copy the eight PNGs into `art-source/icons/sheets/`, and supply art for the biggest gap, which is people — no sheet contains a human figure.
- **Egress limits here.** `rimworldwiki.com` and `steamcommunity.com` are blocked by this container's proxy. Brief §5 names both as primary sources, so Lanes A and B need the dev machine or owner-supplied pages.

## Repository layout

- `docs/brief.md` governing brief · `docs/research/` research files and `INDEX.md` · `docs/reference/screenshots/` reference images and descriptions · `docs/reference/mockups/` clickable interface mockups · `docs/setup/local-dev.md` dev-machine setup.
- `docs/design/` design documents (`09-ui-and-input.md`, `10-ui-panel-catalogue.md`, `11-icon-library.md` exist; `00`–`08` come with Phase 3), plus the icon data `icon-keys.csv` and `icon-map.csv` · `docs/adr/` decision records (`0001` UI framework, `0002` sim/UI contract, `0003` layer visibility, `0004` pixel-art icon pipeline). Later phases add `docs/plans/`, `docs/milestones/`.
- `art-source/` owner-owned source art, outside `Assets/` so Unity does not import it. `art-source/icons/sheets/` is where the eight icon sheets go.
- `tools/icons/icons.py` the icon pipeline: detect, contact, export, validate, emit-web. Standard library only, so it runs here and in CI. `python3 -m unittest discover -s tools/icons -t tools/icons` runs its 30 tests and needs no art.
- `tools/wiki/build_wiki.py` builds `docs/wiki/` from the design data. `tools/mockups/artifact_body.py` makes a mockup publishable as an Artifact. Both standard library only.
- `docs/wiki/` the generated content wiki. Read it, do not edit it. See the section above.
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
- Interface icons are referenced by symbolic key, never by filename, and are 64 px, point-filtered, uncompressed, no mips, displayed at 32 and 64 only. ADR 0004.
- Commits: small, one concern each, descriptive message. Never commit `Assets/Synty/`, `Library/`, logs or test results.
- Content changes carry their regenerated wiki. `python3 tools/wiki/build_wiki.py --check` passes before the commit.

## Starting a local session

Run `claude` in the repository root; this file is read automatically. Useful first prompts:

- "Read CLAUDE.md and docs/research/INDEX.md. Run the procedure in docs/research/synty-import.md, fix SyntyInventory.cs if it does not compile, commit the inventory, and report the implied cell size."
- "Phase 1 answers are: … Record them in docs/research/phase1-answers.md, update CLAUDE.md status, then start the Phase 2 lanes that do not depend on the inventory."
