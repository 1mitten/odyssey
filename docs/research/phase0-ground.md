# Phase 0 — Ground report

Date: 2026-09-15. Environment: Claude Code remote session (ephemeral Linux container), repository `1mitten/odyssey`, branch `claude/cool-gates-5xkpgi`.

## 1. Working directory

| Item | Found |
|---|---|
| Contents | Empty scaffold: a one-line `README.md` and nothing else. No `Assets/`, `Packages/` or `ProjectSettings/`. |
| Git | Initialised, one commit (`Initial commit`), remote `origin` = `github.com/1mitten/odyssey`. Branch `claude/cool-gates-5xkpgi` exists locally and on origin. |
| `.gitignore` | Was absent. Added in this phase: Unity standard ignores plus `Assets/Synty/` and `*.unitypackage`. |
| CI | None (`.github/workflows` absent). |
| Unity MCP server | Not configured (no `.mcp.json`, no Claude project settings). Options researched in `unity-mcp-server.md`. |
| Prior project (*Ramble*) | Not in this repository and not on this machine. Lane F needs a path, a copy of its design docs, or a repo added to the session. |

## 2. Toolchain in this container

| Tool | Status |
|---|---|
| git | 2.43.0 |
| git-lfs | not installed |
| dotnet SDK | not installed |
| Unity Hub / Unity Editor | not installed |
| node / python | node 22, python 3.11 (incidental, not used) |
| Hardware | 4 vCPU, 15 GB RAM, x86_64 Linux |

Consequence: this container cannot compile C#, run Unity, or import a `.unitypackage`. It is fine for research and documentation. The Unity-side spikes (import, inventory, batchmode proof, MCP verification) must run on the Pop!_OS machine. For later milestones the remote environment will need a dotnet SDK at minimum, and the Unity editor if CI-style headless runs are to happen here rather than on a self-hosted runner.

## 3. Synty inventory — blocked here

The whole filesystem was searched for anything named `*synty*` or `*.unitypackage`: nothing. The POLYGON Sci-Fi City pack is licensed content in the owner's Asset Store account and is not, and must not be, in this repository.

Therefore **the cell size cannot be fixed from data in this session**, and by non-negotiable 3 nothing is built until it is. What was done instead:

- `Assets/Editor/Odyssey/SyntyInventory.cs`: an editor script that produces the inventory the brief asks for. It reads the bounds of every Synty prefab, classifies pieces by family and role (wall, floor, roof, door, window, stair, pillar, railing, ladder), builds size histograms per role, tests which metre pitch the pieces snap to, reports pivot placement, rig type and bone counts, animation clips, the material and shader audit, texture sizes, and opens every Synty scene headless to count errors. It writes `docs/research/synty-inventory.md` and `docs/research/synty-inventory.csv`. It runs from the menu or via `-executeMethod`. It has **not been compiled** (no editor here), so expect to fix a line or two on first run.
- `docs/research/synty-import.md`: the import-spike procedure and a record table to fill in.

A consequence worth stating now, because it shapes the architecture: since `Assets/Synty/` is never committed, any clone without the pack (CI, this remote environment) will have missing-asset references in anything that points at Synty content. The simulation, its tests and the headless run must therefore never depend on Synty assets. That is one more reason for a strict sim/presentation split, and Lane D should treat it as a requirement rather than a preference.

## 4. Import spike — not run

See `docs/research/synty-import.md`. Blocked for the same reason as the inventory.

## 5. RimWorld Def structure reference

No install path was provided. Nothing done. If one is provided later, report Def types and field names only, never contents.

## 6. What this means for the phase order

Phase 1 questions are asked in the session. Phase 2 research does not depend on the inventory except Lane E (asset fit) and Lane D3 (rendering with Synty modules); those two wait for `synty-inventory.md`. Everything else can start once Phase 1 is answered. The cell size can be confirmed whenever the inventory lands, in parallel with the rest of Phase 2, as long as it lands before Phase 3 (the world-and-layers design and its ADR).

## 7. Tool-call count and extra outputs

Phase 0 used two inspection calls (one repository and toolchain sweep, one background research subagent for the MCP question). The remaining calls were file writes and the commit. Beyond the brief's listed outputs, this phase also produced, at the owner's mid-phase request, what a local Claude Code session needs to take over on the dev machine: `CLAUDE.md`, `docs/setup/local-dev.md`, `scripts/unity.sh` and `.claude/settings.json`. The reference images supplied in chat are described in `docs/reference/screenshots/README.md`; the image files themselves must be added by the owner.
