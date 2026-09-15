# Unity MCP server — options and recommendation

## Question

Which open-source Unity MCP (Model Context Protocol) server should Claude Code use to drive a Unity 6.3 LTS (6000.3.x) editor on Linux (Pop!_OS)? "Best" means: most actively maintained in 2026, explicit Unity 6 / 6000.x support, works on Linux, and can (a) open scenes, (b) run EditMode/PlayMode tests, (c) read the editor console/logs, and ideally (d) execute editor C# or menu items.

## Findings

| | CoplayDev/unity-mcp ("MCP for Unity") | IvanMurzak/Unity-MCP | CoderGamester/mcp-unity | Unity official (com.unity.ai.assistant MCP server) |
|---|---|---|---|---|
| Licence | MIT | Open source (licence not restated in the README fetched; verify on the repo page) | MIT | Proprietary Unity package — not open source |
| Latest release / activity | v10.2.0 on 1 Sept 2026 (v9.6.8 on 27 Apr 2026); most frequent releases of the set | v0.51.4 current; README references Claude Code skills, Antigravity and installing 6000.3.1f1 via its CLI, which places activity in 2026; release date not verified | Last commit c. 4 July 2026 (third-party report); 1.5.x series, deliberately rejects 1.4.x Node bridges | 2.x pre-release channel (latest seen 2.18.0-pre.2), updated through 2026 |
| Unity versions | 2021.3 LTS → 6.x | Broad; CLI explicitly installs 6000.3.1f1 | Unity 6 or later | Unity 6 (6000.0+) |
| Linux | Not stated explicitly in README; Python server + C# plugin, no OS-specific bits found | Yes — self-contained server binaries for linux-x64 and linux-arm64 auto-downloaded to `Library/mcp-server/{platform}/` | Yes — Windows, macOS, Linux stated | Yes — bridge uses Unix sockets on macOS/Linux; relay binary at `~/.unity/relay/` |
| Transport | stdio (Python server launched by the MCP client); session isolation for multiple editors | stdio and streamable HTTP; Docker image | stdio to client; WebSocket between Node bridge and editor | stdio relay ↔ in-editor bridge (IPC) |
| (a) Open scenes | `manage_scene` | Scene/hierarchy tools (24) | `load_scene`, `create_scene`, `save_scene`, `unload_scene`, `get_scene_info` | Scene management stated |
| (b) Run tests | `run_tests` (start job / retrieve results) | Yes — EditMode and PlayMode with detailed results | `run_tests` + `unity://tests/{testMode}` resource | Not confirmed |
| (c) Console/logs | `read_console` | Yes — console log retrieval with filtering | `get_console_logs` + `unity://logs` resource | Console access stated |
| (d) Execute C# / menu items | `execute_menu_item`; no arbitrary C# eval found (custom C# tools via attributes instead) | Yes — Roslyn C# execution plus reflection-based discovery/invocation of any method (incl. private); dedicated menu-item tool not confirmed | `execute_menu_item`, `recompile_scripts`, `batch_execute`; no arbitrary C# | Not confirmed; custom tools can be registered |
| Install | UPM git URL `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main` or OpenUPM `com.coplaydev.unity-mcp` | unitypackage installer, OpenUPM `com.ivanmurzak.unity.mcp`, npm `unity-mcp-cli`, git URL | UPM git URL `https://github.com/CoderGamester/mcp-unity.git` | Unity Package Manager (Unity registry) |
| Prerequisites | Python 3.10+ and `uv` on PATH | None beyond Unity for the editor side (self-contained .NET binary); Node only for the optional CLI | Node.js 18+, npm 9+ | Unity 6 editor; client approval in Project Settings > AI > Unity MCP |
| Known limitations | Linux support implicit only; Python/`uv` toolchain must be discoverable by the editor's auto-installer; compile-time behaviour and telemetry not verified | Project path must not contain spaces; server binary auto-downloaded at editor load; optional ai-game.dev cloud OAuth | Domain reload during PlayMode tests drops the connection (workaround: disable Reload Domain); package install off by default; path-with-spaces issues; Unity and Node halves must be updated together | Pre-release; not open source; account/AI-entitlement requirements unverified |

All three open-source candidates are alive in 2026, target Unity 6, and cover (a)–(c). They differ on (d) and on runtime baggage. CoplayDev has the highest release cadence and the largest tool surface (47 entrypoints, hosted tool reference), but drags in a Python 3.10+/`uv` toolchain and offers menu-item execution rather than arbitrary C#. IvanMurzak is the only one that names Linux binaries and 6000.3 explicitly, ships both stdio and streamable-HTTP transports, needs no Python or Node on the editor side, and is the only one with true C# execution (Roslyn) and reflection-based method invocation, which makes it the strongest for an autonomous edit-test loop. CoderGamester is solid and MIT-licensed but is the least active (July 2026), needs Node, and documents a PlayMode/domain-reload disconnect that matters for (b).

Unity's own MCP server (inside `com.unity.ai.assistant`) is worth knowing about: it is Linux-capable via Unix sockets, starts automatically with the editor, and exposes scene, asset, script and console tools. It is, however, proprietary, still pre-release, and its test-running and code-execution abilities could not be confirmed because docs.unity3d.com and unity.com were unreachable from this sandbox. It is not an answer to the "open-source" question but is a reasonable fallback to trial alongside the winner.

## Recommendation

**IvanMurzak/Unity-MCP.** It is the only candidate that satisfies every criterion with explicit evidence: Linux binaries by name (linux-x64/arm64), Unity 6000.3 named in its CLI, stdio plus streamable HTTP, EditMode/PlayMode test execution with detailed results, filtered console retrieval, and genuine editor C# execution via Roslyn — with no Python or Node runtime required for the editor side and a CLI that configures Claude Code without opening the editor. Mind the two documented constraints: the project path must contain no spaces (`/home/user/odyssey` is fine) and the server binary is auto-downloaded at editor load.

**Runner-up: CoplayDev/unity-mcp.** Most frequent releases (v10.2.0 on 1 September 2026), MIT, 47 tools covering (a)–(c) and menu items, good hosted documentation. It loses on (d) (no arbitrary C#) and on the Python 3.10+/`uv` dependency with only implicit Linux support.

**Single observation that would flip the choice:** the release pages. If IvanMurzak's latest release turns out to be older than about three months (i.e. before June 2026) while CoplayDev continues shipping monthly — or if the auto-downloaded Linux binary fails to start on Pop!_OS — take CoplayDev.

## Sources

https://github.com/CoplayDev/unity-mcp
https://raw.githubusercontent.com/CoplayDev/unity-mcp/main/README.md
https://github.com/CoplayDev/unity-mcp/releases
https://coplaydev.github.io/unity-mcp/reference/tools
https://coplaydev.github.io/unity-mcp/getting-started
https://github.com/IvanMurzak/Unity-MCP
https://raw.githubusercontent.com/IvanMurzak/Unity-MCP/main/README.md
https://github.com/IvanMurzak/Unity-MCP/releases
https://github.com/IvanMurzak/Unity-MCP/blob/main/cli/README.md
https://github.com/CoderGamester/mcp-unity
https://raw.githubusercontent.com/CoderGamester/mcp-unity/main/README.md
https://github.com/CoderGamester/mcp-unity/releases
https://www.blog.brightcoding.dev/2026/09/14/codergamestermcp-unity-ai-agent-control-for-unity-editor-via-mcp
https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.18/manual/integration/unity-mcp-overview.html
https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.18/manual/integration/unity-mcp-get-started.html
https://unity.com/blog/unity-ai-mcp-how-to-get-started

## Confidence

Medium — the three READMEs were read directly, but the GitHub API, docs.unity3d.com and unity.com were unreachable from this sandbox, so release dates for IvanMurzak and CoderGamester and the official package's tool list rest on search excerpts and a third-party write-up rather than the primary release pages.

## Could not be determined

- Exact publication date of IvanMurzak v0.51.4 and CoderGamester's latest tag (GitHub API blocked by the proxy).
- IvanMurzak's licence text (the fetched README did not restate it).
- An explicit Linux statement, HTTP-transport option and telemetry policy for CoplayDev/unity-mcp.
- Whether Unity's official MCP server can run tests or execute C#/menu items, and whether it needs a Unity account or AI entitlement.
- Behaviour of each server while scripts are compiling / during domain reload (only CoderGamester documents it).
