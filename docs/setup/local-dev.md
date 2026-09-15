# Local development setup (Pop!_OS)

Goal: a machine where Claude Code can drive Unity end to end: create and open scenes, run tests, read the console, and run headless batch jobs. The remote Claude Code container cannot do any of this (no Unity, no dotnet SDK, no Synty), so the Unity-side work happens here.

## 1. Prerequisites

| Tool | Version | Notes |
|---|---|---|
| Unity Hub | current | Linux build from unity.com (AppImage or .deb). |
| Unity Editor | newest **6000.3.x LTS** | Install via Hub → Installs → LTS tab. No extra modules needed yet. |
| git | any recent | `git lfs` not needed while `Assets/Synty/` stays ignored. |
| Node.js | 18 or newer | Needed by Claude Code and by the Unity-MCP CLI. |
| Claude Code | current | `npm install -g @anthropic-ai/claude-code`, then `claude` once to log in. |
| dotnet SDK | optional, 8 or newer | Only for future pure-C# simulation tests outside Unity; not needed for Phase 0–3. |
| Blender | optional, 4.x | Gap-filling only: a module the Synty pack lacks (for example a ladder or a stair variant at the cell size), UV or atlas fixes, animation retargeting. Synty assets come first; anything made in Blender must match the Synty style and the cell grid, lives under `Assets/Art/Custom/`, and is ours to commit. |

Keep the repository path free of spaces (for example `~/src/odyssey`); Unity-MCP does not support paths with spaces.

## 2. Clone and branch

```
git clone https://github.com/1mitten/odyssey.git ~/src/odyssey
cd ~/src/odyssey
git checkout claude/cool-gates-5xkpgi   # or whichever branch the current phase uses
```

## 3. Create the Unity project and import Synty

Follow `docs/research/synty-import.md` steps 1–7. In short: create a Universal 3D project in a temporary folder, move `Assets/`, `Packages/`, `ProjectSettings/` into the repository root, open it from Hub, import POLYGON Sci-Fi City from *My Assets*, and make sure it sits under `Assets/Synty/`.

## 4. Run the inventory and the headless proof

```
scripts/unity.sh which        # prints the editor it found (or set UNITY_EDITOR=/path/to/Editor/Unity)
scripts/unity.sh inventory    # writes docs/research/synty-inventory.md and .csv, log in Logs/
```

Or from the editor menu: **Odyssey → Phase 0 → Run Synty inventory**. If `Assets/Editor/Odyssey/SyntyInventory.cs` fails to compile (it was written without a compiler), fix it and note the fix in `synty-import.md`. Commit the generated inventory files.

## 5. Install the Unity MCP server (IvanMurzak/Unity-MCP)

Why this one: explicit Linux binaries, Unity 6000.3 named in its own tooling, stdio and streamable-HTTP transports, EditMode/PlayMode test execution, console retrieval, and genuine editor C# execution via Roslyn, with no Python or Node needed on the editor side. Comparison and runner-up in `docs/research/unity-mcp-server.md`.

1. Install the package into the project. Either **OpenUPM** (`openupm add com.ivanmurzak.unity.mcp`, requires `npm i -g openupm-cli`) or Package Manager → *Add package from git URL* using the URL in the project's README at https://github.com/IvanMurzak/Unity-MCP.
2. Open the project once so the editor downloads the server binary into `Library/mcp-server/<platform>/` (this folder is gitignored via `Library/`).
3. Register the server with Claude Code. The project ships a CLI (`unity-mcp-cli` on npm) whose README documents a one-command Claude Code setup; follow the README's current "Claude Code" section rather than a command copied here, because the flags change between releases. The result should be an entry in `.mcp.json` at the repository root (project scope, committed) or in the user scope.
4. Verify:

```
claude mcp list                     # the Unity server should be listed and healthy while the editor is open
claude                              # then ask: "Using the Unity MCP tools, list the open scene's root objects and read the console."
```

Known constraints: the editor must be open and not compiling for tools to respond; the project path must contain no spaces; the server binary is re-downloaded on package updates.

If the server misbehaves on Pop!_OS, the runner-up is CoplayDev/unity-mcp (Python 3.10+ and `uv` required; menu-item execution but no arbitrary C#).

## 6. Test loop that also works in CI

```
scripts/unity.sh test editmode      # results in TestResults/EditMode.xml, log in Logs/
scripts/unity.sh test playmode      # PlayMode may need graphics; drop -nographics via UNITY_EXTRA_ARGS if it fails headless
scripts/unity.sh exec Namespace.Class.Method   # any static editor method, batchmode, quits after
```

There are no tests yet; the wrapper is here so that M0 has a stable command surface from its first commit. Licence activation for a non-interactive CI machine is a Lane D8 question and is not solved by this script.

## 7. Working with Claude Code locally

- `claude` in the repository root reads `CLAUDE.md` automatically.
- `.claude/settings.json` pre-approves read-only git commands and `scripts/unity.sh` so the agent is not interrupted for them. Edit it if you want more or less.
- Keep the phase discipline: the agent stops after each phase; you answer; it continues. Answers go into `docs/` so they survive `/clear`.

## 8. Measurements this repository owes but cannot take here

Added 2026-09-15 with the interface design. None of these can run in the remote Claude Code
container: it has no Unity and no dotnet SDK. Each has a number it must beat, taken from
`docs/design/09-ui-and-input.md` §4, and those numbers are budgets set from first principles
rather than observations. A budget met comfortably on the first attempt was set too loosely.

Full detail, including the other nine experiments, is in `docs/research/g-02-unity-ui-framework.md`.

| # | What to measure | Must beat | Why it matters |
|---|---|---|---|
| R11 | Is there a public runtime UXML parser in Unity 6.3? | — | Ten minutes. Decides whether our own `UiLayoutDef` is a parallel format or a thin wrapper. Do this one first |
| R12 | Dynamic atlas eligibility rules: size cap, compression, mips | — | Thirty minutes. Sets the icon authoring pipeline |
| R1 | A dense HUD: a 50 × 25 priority grid, a 50-card roster bar and a 10,000-row virtualised archive, all open, driven at 60 Hz | **3.5 ms** main thread and **zero** per-frame allocation after warm-up | The flip condition for `docs/adr/0001-ui-framework.md`. If it fails on framework internals rather than our code, reopen against uGUI |
| R3 | Pointer partitioning between the HUD and the world, over the eight enumerated cases in `09` §6 | all eight correct | The likeliest source of shipped bugs in this genre |
| R4 | Do view-level tests with a live panel run under `-batchmode -nographics`? | pass | Decides whether any view-level test can be in CI. The lowest-confidence assumption in the design |
| R6 | Dirty-chunk texture upload under worst-case churn, such as fire spreading across a layer | **4 ms** for a full slice rebuild, **0.5 ms** incremental | The overlay path, which is the difference between an overlay costing one draw call and costing the frame |
| R5 | Cost of building and publishing a world view at 3× speed | **0.3 ms** per publish | The **only** one of these that becomes runnable in the remote container once it has a dotnet SDK. A small argument for installing one |

Gate every result on the target machine, a 2022 mid-range laptop, not on the RTX 5070 Ti dev
box. Where only the dev box is available, hold results to 1.4× stricter than the figures above.
