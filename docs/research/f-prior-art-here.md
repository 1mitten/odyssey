# Lane F — Prior art in this repository

## Question

Does anything already in this repository — or referenced by it — constitute prior art worth carrying into Phase 3 design, and is there anything from the owner's previous project *Ramble* (Godot 4; reference only, no code reuse ever) available to learn from?

## Findings

Everything in this repository was surveyed (all of `docs/`, `Assets/Editor/`, `scripts/`, `.claude/`, root files, and the git history — ten commits, two merged PRs). Verdicts:

### In this repository

| Item | Verdict | Reason |
|---|---|---|
| `docs/brief.md` | **REUSE** | The governing brief; Phase 3 writes exactly the documents it lists in §6, no re-derivation needed. |
| Research-file format + `docs/research/INDEX.md` | **REUSE** | Fixed sections (Question/Findings/Sources/Confidence/Could-not-be-determined), one file per question, predictable slugs — proven across Phase 0–2; keep verbatim for Phase 3 outputs. |
| `docs/research/phase0-ground.md` | **REUSE** | Its §3 consequence is a standing architecture requirement: clones without `Assets/Synty/` must build and run headless, so the sim can never reference Synty assets — carry into `01-architecture.md` as a hard constraint, not a preference. |
| `docs/research/synty-import.md` | **REUSE** | Repeatable import procedure plus two hard-won gotchas: `AssetDatabase.ImportPackage` only *queues* under `-executeMethod`, and URP 17.3.0's `Converters.RunInBatchMode` throws `MissingMethodException` — both will bite again at the 6.7 upgrade. |
| `docs/research/synty-inventory.md` + `.csv` | **REUSE** (data) / **ADAPT** (one summary) | The measured dimensions, pivots, grid-pitch histograms and rig/clip audit are Lane E's raw input. One blemish: the "Implied cell" line prints `0.00 × 0.00 × 0.00 m` because no pitch reached the script's 80% snap threshold; the histograms carried the decision anyway. Fix or drop that summary line in `SyntyInventory.cs` before the next regeneration. |
| `docs/research/phase1-answers.md` | **REUSE** | Binding decisions: cell 2.5 × 2.5 × 3.0 m, template stamping, IvanMurzak MCP, pragmatic TDD, two-way architecture benchmark with determinism-first threading, Windows primary. Phase 3 inherits all seven answers as-is. |
| `docs/adr/0001` and `0002` | **REUSE** | Accepted ADRs and the ADR format itself; Phase 3 adds sibling ADRs (data format, architecture, threading) in the same shape. |
| `docs/setup/local-dev.md` | **REUSE** | Working dual-platform setup, including the Windows headless-import invocation verified on 2026-09-15. |
| `docs/reference/screenshots/README.md` | **REUSE** | The describe-every-image record already feeds design: it names the missing layer control and depth cues for `06-rendering-and-camera.md`. Note the image files themselves are still absent (`concept/` holds only `.gitkeep`); the owner has not yet copied them in. |
| `scripts/unity.sh` | **REUSE** | The CI command surface (`which`/`inventory`/`test`/`exec`/`open`), editor discovery from `ProjectVersion.txt` on Linux and Windows Git Bash, `UNITY_EXTRA_ARGS` for CI licence flags. Extend with new subcommands; never replace. |
| `Assets/Editor/Odyssey/SyntyInventory.cs` | **REUSE** (as pattern) | The template for all future editor tooling: dual menu + batchmode entry, `EditorApplication.Exit` codes, writes documentation as output. The scene/prefab generators the conventions require in Phase 4 should copy this shape. |
| `Assets/Editor/Odyssey/SyntyImport.cs` | **REUSE** (as pattern) | Same discipline, plus the synchronous `ImportPackageImmediately` reflection workaround with a documented fallback — keep for pack updates and the 6.7 reimport. |
| `.claude/settings.json` | **REUSE** | Pre-approves read-only git and `scripts/unity.sh`; grow the allowlist as tooling grows. |
| `CLAUDE.md` | **ADAPT** | Structure is right, but its **Current status** section is stale: it predates the import spike, the inventory, the Phase 1 answers and both ADRs (all landed 2026-09-15). The main thread should refresh it — flagged here, not edited by this subagent. |
| `README.md` | **ADAPT** | Same staleness: the status line still says Phase 1 is awaiting answers. |
| Unity project at root (`Assets/`, `Packages/`, `ProjectSettings/`) | **REUSE** | Created per the recorded procedure on 6000.3.24f1 + URP 17.3.0; it is the project, not prior art to re-evaluate. |

### Referenced by the repository: *Ramble*

Reference only — design intent may be learned; no GDScript, scenes or assets ever cross over.

| Ramble item | Verdict | Reason |
|---|---|---|
| Determinism law + seeded purity (`CLAUDE.md` laws, `Determinism.chunk_rng`) | **ADAPT** | "World state is a pure function of (seed, coord); deterministic iteration order; append-only ids" is exactly the discipline Odyssey's Q5/Q6 answers chose — Ramble proves it survives contact with a real game for a year. |
| Record-store build architecture (`docs/BUILD_MODE.md`) | **ADAPT** | Pure-static rules brain over a record store, with world nodes *re-derived* from records, so persistence and reload come free and a placed piece is byte-identical after reload. Strong prior art for the sim/presentation split in `01-architecture.md` and `04-data-model.md`. |
| One piece-def × Stuff × Quality ladder (`stuff.gd`, `BuildPieceDef`) | **ADAPT** | Ramble already runs a RimWorld-style "stuff" system: one def per piece, material tier and quality resolve stats multiplicatively, wood-vs-stone is never a separate def. Carry the shape (not the code) into the Def system. |
| Blueprint capture/stamp as pure relative data (`BlueprintData`) | **ADAPT** | Anchor-relative piece lists with no world coordinates, rotatable, stamped all-or-nothing — precisely the template-stamping model Q3 chose for ruined-city shells. |
| Frame → built lifecycle (`BuildFrame` → `BuiltPiece`) | **ADAPT** | Progress lives in the record, the node is a view; upgrading FRAME→BUILD mirrors RimWorld's blueprint→frame→built pipeline Odyssey plans in M3. |
| Headless-gate taxonomy (`docs/HEADLESS_ROUTINE.md`) | **ADAPT** | The check/test gates are headless; screenshot/soak/perf/playtest need GPU or the owner; a task is agent-completable only if a passing test (not a PNG) proves it. Map onto `scripts/unity.sh` and the milestone gates. |
| Mistakes log (`docs/AGENT_MISTAKES.md`) | **ADAPT** (adopt the practice) | A running log of agent errors with the generalisable rule each bought — notably "prove a new test fails" and "check the fixture contains the condition you assert". Start Odyssey's equivalent at M0. |
| Threaded compute/commit split (`docs/VOXEL_MINING_PLAN.md`, `ChunkManager`) | **ADAPT** | Worker threads compute pure payloads (no scene tree), the main thread commits under a budget; digs are sparse delta records on the same persistence seam as builds. Directly informs Odyssey's threading stance and what "digging" writes. |
| Asset registries + generated catalogue (`docs/ASSET_FRAMEWORK.md`) | **ADAPT** | One render-side-only mesh loader that can never affect determinism; "what's integrated" is generated from code registries, never hand-maintained — the same philosophy as `SyntyInventory.cs`. |
| GDScript/scene code itself | **IGNORE** | The no-code-reuse rule is absolute, and Godot idioms would not transfer anyway. |
| Co-op/networking docs (`COOP_*`, ENet/Steam) | **IGNORE** | Odyssey is single-player, ever; even Ramble's "seams cost" lessons buy nothing we may use. |
| Vehicles, Ramstruction, Steam release, world events | **IGNORE** | Out of genre and scope. |
| Godot-native Synty packs under `ramble/assets/` (City, Town, Battle Royale) | **IGNORE** | Different engine builds; Odyssey's licensing boundary (gitignored `Assets/Synty/`) governs — note Ramble *tracks* its glTF sets in-repo, a posture Odyssey deliberately does not share. |

## Ramble

**Present, in full, and active.** The brief and Phase 0 assumed Ramble was unavailable ("not in this repository and not on this machine" — true of the remote container). On this Windows dev machine it exists as a complete working checkout at `D:\code\ramble` (last commit 2026-09-14), with ~20 sibling `ramble-*` worktree/branch folders under `D:\code\`. It is a Godot 4.7 (GDScript, Forward+) **first-person co-op survival** game on an infinite deterministic world — the brief's "top-down" description matches only its Phase-1 origin as a walker spine. Its `docs/` folder holds 128 markdown files including a design wiki; seven of the most transferable were skimmed for this lane (listed above). Nothing from it enters this repository; the carry-forwards are design intent only.

## Layer questions touched

None, as expected — this lane is about process and prior art, not layer mechanics. (Ramble's underground is a heightfield-plus-voxel hybrid, not discrete layers; its patterns inform *how* to build systems, not the twelve answers themselves. The blueprint-as-relative-data pattern is the nearest brush, feeding the Q8/worldgen template design indirectly.)

## Sources

In-repo (all read in full or in relevant part): `D:\code\odyssey\docs\brief.md` · `docs\research\INDEX.md` · `docs\research\phase0-ground.md` · `docs\research\synty-import.md` · `docs\research\synty-inventory.md` · `docs\research\phase1-answers.md` · `docs\research\unity-mcp-server.md` · `docs\adr\0001-engine-and-version.md` · `docs\adr\0002-cell-size-and-layer-model.md` · `docs\setup\local-dev.md` · `docs\reference\screenshots\README.md` · `scripts\unity.sh` · `Assets\Editor\Odyssey\SyntyInventory.cs` · `Assets\Editor\Odyssey\SyntyImport.cs` · `.claude\settings.json` · `README.md` · `CLAUDE.md` · git log.

Ramble (skimmed, reference only): `D:\code\ramble\README.md` · `docs\wiki\Home.md` · `docs\ASSET_FRAMEWORK.md` · `docs\HEADLESS_ROUTINE.md` · `docs\AGENT_MISTAKES.md` · `docs\BUILD_MODE.md` · `docs\VOXEL_MINING_PLAN.md` · git log.

## Confidence

**High** for the in-repo verdicts (every item read directly) and for Ramble's presence and nature. **Medium** for the Ramble carry-forward list: 7 of 128 docs were skimmed under the read cap; the picks were chosen by title relevance, and a fuller pass (notably `docs\wiki\Building-and-Homestead.md`, `BLUEPRINTS_PLAN.md`, `SCALABILITY.md`, `PROGRESSION.md`) could surface two or three more patterns if the owner wants a second wave.

## Could not be determined

- Whether `D:\code\ramble` is the canonical remote-backed clone or a local-only copy (its remotes were not inspected).
- Whether any Ramble doc records a *post-mortem* on top-down vs first-person camera — the one Ramble lesson that would bear directly on Odyssey's camera design was not found in the files skimmed.
- Which of the ~20 `ramble-*` sibling folders differ from the main checkout (assumed worktrees/branch copies; not examined).
