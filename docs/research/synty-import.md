# Synty import spike — Unity 6.3 LTS + POLYGON Sci-Fi City

Status: **not run** (2026-09-15). This session runs in a remote Linux container with no Unity editor, no licence and no access to the Asset Store account that owns the pack. The spike must be run on the Pop!_OS dev machine. The procedure below is written to take about thirty minutes and to produce the files Phase 0 needs.

## Assumptions made here (say if any is wrong)

- The Unity project lives at the **repository root** (`Assets/`, `Packages/`, `ProjectSettings/` beside `docs/`). One project, ever; simplest CI paths; Unity ignores the other folders.
- Template: **Universal 3D** (URP). Version: the newest **6000.3.x** patch shown on Unity Hub's LTS tab.
- Synty content goes under `Assets/Synty/` and nowhere else. `.gitignore` already excludes that folder and any `*.unitypackage`.

## Procedure

1. Unity Hub → Installs → Install Editor → newest 6000.3.x LTS. Modules: none needed for the spike. (Add *Linux Build Support (Mono)* later for headless player builds if wanted.)
2. Hub → New project → **Universal 3D** → name `odyssey-tmp`, any location. Let it open once so the URP template finishes its setup, then quit.
3. Move `Assets/`, `Packages/` and `ProjectSettings/` from `odyssey-tmp` into the repository root, then delete `odyssey-tmp`. Hub refuses to create a project in a non-empty folder; this is the workaround. Hub → Add → the repository root; open it.
4. Window → Package Manager → *My Assets* → POLYGON Sci-Fi City → Download → Import (everything). Note the version string shown in the Package Manager.
5. If the pack did not land under `Assets/Synty/…`, drag its folder into `Assets/Synty/` **inside the Project window**. That preserves GUIDs; moving it in a file manager breaks every prefab reference.
6. Read the Console: record every distinct warning and error from the import. Synty packs ship URP shader-graph materials. If any material renders magenta, look for a URP sub-package (`.unitypackage`) or README inside the pack and follow it; record what was needed.
7. Open the pack's demo scene(s) under `Assets/Synty/…/Scenes/`. Record: whether it loads, errors, warnings, and the Game-view frame rate at 1080p.
8. Run the inventory from the menu **Odyssey → Phase 0 → Run Synty inventory**. It writes `docs/research/synty-inventory.md` and `docs/research/synty-inventory.csv`. If the script fails to compile, fix it (it was written without a compiler) and note the fix in the record below.
9. Headless proof, from the repository root with the editor closed:

   ```
   ~/Unity/Hub/Editor/6000.3.<patch>/Editor/Unity -batchmode -nographics -projectPath . \
     -executeMethod Odyssey.EditorTools.SyntyInventory.Run -logFile Logs/synty-inventory.log -quit
   echo "exit: $?"
   ```

   Exit code 0 and a regenerated `synty-inventory.md` prove that batchmode works on this machine and that every Synty scene opens headless. The script counts errors per scene while opening them.
10. Commit: `Assets/**` (the ignored `Assets/Synty/` stays out automatically), `Packages/`, `ProjectSettings/`, both `synty-inventory.*` files, and this file with the record filled in.

## Record (fill in)

| Item | Value |
|---|---|
| Unity version | |
| URP package version | |
| Pack version (Package Manager) | |
| Import warnings (count, and the distinct messages) | |
| Materials that imported magenta or with error shaders | |
| Extra step needed for URP | |
| Demo scene loads in editor (errors / warnings / FPS) | |
| Demo scene loads in batchmode (exit code, errors) | |
| Inventory script compiled first time? Fixes needed | |
| Time taken | |
| Verdict | pass / pass with notes / fail |

## If it fails irrecoverably

Stop and report before anything else (brief §3.3). Likely failure modes: the pack's shader graphs targeting an older URP version (usually fixed by reimporting the shader-graph assets), or a Hub or editor regression on Linux in the newest 6000.3 patch (fall back one patch).
