# Local development setup (Pop!_OS and Windows)

Goal: a machine where Claude Code can drive Unity end to end: create and open scenes, run tests, read the console, and run headless batch jobs. The remote Claude Code container cannot do any of this (no Unity, no dotnet SDK, no Synty), so the Unity-side work happens here.

Two dev machines exist as of 2026-09-15: the Pop!_OS machine this file was written for, and a Windows 11 machine (`D:\code\odyssey`) where Phase 0 was actually run. Sections 1–7 apply to both unless marked; Windows specifics are in §8.

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

## 8. Windows dev machine notes

What differs from the Linux instructions above (verified on Windows 11, 2026-09-15):

- **Unity CLI instead of classic Hub.** The machine runs the Unity Hub beta with its `unity` CLI at `%LOCALAPPDATA%\Unity\bin\unity.exe`. Useful commands: `unity editors -r` (list installable versions), `unity install 6000.3.24f1 -y --non-interactive --accept-eula` (headless editor install — expect one UAC prompt for `C:\Program Files`), `unity env` (paths). Editors land in `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe`.
- **`scripts/unity.sh` works from Git Bash** (the shell Claude Code uses on Windows). It finds editors under `C:\Program Files\Unity\Hub\Editor` automatically, preferring the version in `ProjectSettings/ProjectVersion.txt`.
- **Headless Synty import.** With the `.unitypackage` files downloaded (never committed):

  ```
  "C:\Program Files\Unity\Hub\Editor\<ver>\Editor\Unity.exe" -batchmode -nographics -projectPath D:\code\odyssey ^
    -executeMethod Odyssey.EditorTools.SyntyImport.ImportAll ^
    -odysseyPackages "C:\path\pack1.unitypackage;C:\path\pack2.unitypackage" ^
    -logFile Logs\synty-import.log
  ```

  No `-quit` — the method exits the editor itself. `AssetDatabase.ImportPackage` merely queues imports under `-executeMethod` (a run can "succeed" having imported nothing), so `SyntyImport.cs` calls the editor's synchronous internal import; see the comment in that file.
- **Keep the project path free of spaces** here too (`D:\code\odyssey` is fine) — same Unity-MCP constraint.

## 9. Unity gotchas that cost real debugging time

Collected rather than rediscovered. The first two land on the Burst grid job, the third lands on the material-tint strategy in `docs/design/06-rendering-and-camera.md`. (Contributed by another Claude Code session on this machine working on an unrelated Unity project; each cost it a debugging cycle.)

- **`using var` on a `NativeArray` makes the local read-only**, so writing into it fails with **CS1654**. Declare it normally and dispose in a `finally`, or wrap it in a method that returns it.
- **`Allocator.Temp` cannot be handed to a job** — it is main-thread and single-frame. A job needs `TempJob` or `Persistent`. This is easy to miss because it compiles and then misbehaves.
- **Assigning `renderer.material` in edit mode instantiates a copy**, so setting properties on the original afterwards silently does nothing. Use `sharedMaterial` in editor scripts. This one matters a great deal to us: the committed tint strategy is *one cached material per stuff* with instanced draw buckets, and an accidental `.material` would quietly break the batching while looking almost right.
- **Unity MCP as installed (2026-09-15, plugin v0.90.0):** `npx --yes unity-mcp-cli install-plugin .` adds the package to `Packages/manifest.json`; the first *interactive* editor open downloads the server to `Library/mcp-server/win-x64/gamedev-mcp-server.exe` (a batch run does not). The committed project-scope `.mcp.json` starts it with `port=8080 client-transport=stdio` (relative path, resolved from the repo root; Linux uses `linux-x64`). First `claude` run in the repo asks to approve the project server — approve it, keep the editor open (and not compiling), then verify with `claude mcp list`.

## 10. The two test tiers

Both run headless. Use the fast one while working and the authoritative one before committing.

| | Command | Cycle | What it covers |
|---|---|---|---|
| **Fast** | `scripts/test-fast.sh` | **~1.7 s** warm | Everything in `Odyssey.Sim` and `Odyssey.Sim.Contracts`, which is all pure C# by design |
| **Authoritative** | `scripts/unity.sh test editmode` | **~37 s** | The same tests, plus assembly-definition boundaries, editor tooling and anything touching Unity |

The tests themselves take about 40 ms. The difference is entirely Unity booting, refreshing the asset database and reloading the script domain, so filtering which tests run saves nothing; avoiding Unity is the only thing that helps.

The fast tier builds the *same source files* through mirror projects in `tools/dotnet/`. There is one source of truth. Those projects target `netstandard2.1` to match Unity's API surface, so a .NET-only API that Unity could not compile fails in the fast tier first, and they reference each other in the same direction the assembly definitions do, so a stray `UnityEngine` dependency inside the simulation breaks the fast build immediately.

It needs a .NET SDK, which is *not* the runtime that ships with Unity. Install one without admin rights:

```
powershell -c "& ([scriptblock]::Create((irm https://dot.net/v1/dotnet-install.ps1))) -Channel 8.0"
```

The script finds it at `%USERPROFILE%\.dotnet`, on `PATH`, or wherever `DOTNET` points.

**Known Unity issue, and why the wrapper has a watchdog.** A `-runTests` batch run sometimes writes its results and then never exits, holding `Temp/UnityLockfile`; the next batch command then dies instantly with exit code 1 and a near-empty log. `unity.sh` now says so plainly, clears a genuinely stale lock, and treats the results file rather than the process exit code as the authority, terminating a lingering process after a grace period. `UNITY_TEST_TIMEOUT` and `UNITY_TEST_GRACE` tune it. This matters for CI, which must fail rather than hang.
