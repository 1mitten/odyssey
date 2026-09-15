# Synty import spike — Unity 6.3 LTS + POLYGON Sci-Fi City

Status: **run and passed (with notes) on 2026-09-15**, on the Windows dev machine rather than Pop!_OS, and with five packs rather than one (the owner supplied four additional packages — see `phase1-answers.md`). The record and the deviations from the written procedure are at the bottom. The procedure below remains the reference for repeating the import on Pop!_OS; the Windows equivalents are in `docs/setup/local-dev.md` §8.

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

## Record (filled in 2026-09-15, Windows dev machine)

| Item | Value |
|---|---|
| Unity version | **6000.3.24f1** (newest 6000.3.x LTS), installed headless via the Unity CLI. The machine had only 6000.6.0f1, which the brief rules out (§2: not a 6.4–6.6 Update release). |
| URP package version | **17.3.0** (resolved from the Universal 3D template's manifest). Linear colour space; active RP asset `PC_RPAsset`. |
| Pack versions (from package filenames; imported from `.unitypackage` files, not the Package Manager) | Sci-Fi City 1.3.3 · Farm 1.7.3 · Western Frontier 1.7.2 · Particle FX 1.4.1 · ANIMATION Base Locomotion 1.1.3 — all "Unity 2022.3" builds |
| Import warnings (count, and the distinct messages) | None that matter: 0 C# warnings, 0 failed asset imports, 0 exceptions in an 18,900-line log. Only repeated `I/O warning: failed to load external entity "Temp/UnityTempFile-…/templates/.template"` — internal shader-compiler temp-file noise, harmless. |
| Materials that imported magenta or with error shaders | 0 null/error-shader materials. The packs are Shader Graph native (`Synty/Generic_*` etc.). Stragglers targeting Built-in: 7 `Standard` materials — upgraded headless to `Universal Render Pipeline/Lit` via `SyntyImport.UpgradeBuiltInMaterials` (the public `MaterialUpgrader` API; note URP 17.3.0's `Converters.RunInBatchMode` is broken — `MissingMethodException` on `Base2DMaterialUpgrader`). 58 `Legacy Shaders/Particles/*` materials remain: unlit blend shaders that render correctly under URP; replacing them with the pack's `Synty/Generic_Particles*` graphs is a Lane E4 decision, not a blocker. |
| Extra step needed for URP | None for the Synty shader graphs — no URP sub-package needed (the 2022.3-era packs are SRP-native, with Built-in versions under `Shaders/Legacy/`). Only the converter run above for the straggler materials. |
| Demo scene loads in editor (errors / warnings / FPS) | **Not measured** — headless session; the owner should open `Assets/Synty/PolygonSciFiCity/Scenes/Demo.unity` once and note FPS at 1080p. |
| Demo scene loads in batchmode (exit code, errors) | All 13 Synty scenes open headless, exit 0. Overview scenes: 0 errors, 0 warnings. Demo scenes with baked lighting/probes log `RenderTexture.Create failed` under `-nographics` (no graphics device) — an artefact of headless runs, not an asset problem. |
| Inventory script compiled first time? Fixes needed | Compiled first time on 6000.3.24f1, no syntax fixes. One functional fix: `ClassifyFamily` now skips the `Gen` token the 2022.3-era shared packs insert (`SM_Gen_Bld_Ladder_01`), otherwise generic building modules were excluded from the grid test. New `SyntyImport.cs` added for headless package import — note that `AssetDatabase.ImportPackage(path, false)` only *queues* under `-executeMethod` (a run can exit "successfully" having imported nothing); the script calls the editor's synchronous internal import instead. |
| Time taken | ≈ 50 minutes end to end: ~13 min editor download+install, ~10 min template + five-pack import (7,222 assets, 1.03 GB under `Assets/Synty/`), ~4 min inventory, the rest analysis. A first attempt on 6000.6.0f1 was discarded when the brief's version rule was checked. |
| Verdict | **Pass with notes** — notes being the headless `RenderTexture` noise, the straggler Built-in materials, and the FPS check deferred to the owner. |

## Deviations from the written procedure (all recorded, none change the outcome)

1. **Machine and OS.** Run on Windows 11 (`D:\code\odyssey`), not Pop!_OS. Windows specifics are documented in `docs/setup/local-dev.md` §8.
2. **Project creation.** Instead of the Hub temp-project dance (step 2–3), the Universal 3D template payload (`com.unity.template.3d-cross-platform-17.0.14.tgz`, shipped inside the editor) was extracted directly into the repository root — same result as the Hub flow, scriptable and reproducible.
3. **Import path.** Packs were imported headless from `.unitypackage` files via `SyntyImport.cs` (the owner's files, downloaded from the Asset Store outside Unity), not through the Package Manager UI (step 4). Five packs, not one.
4. **No relocation needed** (step 5): the 2022.3-era packs self-install under `Assets/Synty/<PackName>`, with a shared `PolygonGeneric` base and a `SyntyPackageHelper` editor helper.
5. **Cell size result** (what Phase 0 exists for): base building modules are **2.5 m wide × 3.0 m tall** — walls 2.50 × 3.01 × 0.23 m with 97% base pivots, floors 2.50 × 2.50 × 0.10 m, stairs on a 2.5 m footprint, the ladder 3.0 m tall, and the larger `Section` pieces exactly 5 m (2-cell multiples). **Implied cell: 2.5 × 2.5 × 3.0 m** — recommendation, awaiting the owner's confirmation (brief §4 Q2). Half-height (1.5 m) and half/quarter-width variants exist for finer trim but the load-bearing pitch is unambiguous.

## If it fails irrecoverably

Stop and report before anything else (brief §3.3). Likely failure modes: the pack's shader graphs targeting an older URP version (usually fixed by reimporting the shader-graph assets), or a Hub or editor regression on Linux in the newest 6000.3 patch (fall back one patch).
