# Research index

Every research result lives in this folder as one file per question, in the fixed format: Question, Findings, Recommendation, Sources, Confidence, Could not be determined. Findings only; sources are URLs. British English.

## Files

| File | Phase / lane | Status | Confidence | One-line result |
|---|---|---|---|---|
| `phase0-ground.md` | Phase 0 | done | high | Empty repository; no Unity, dotnet, git-lfs, CI, MCP or Synty content in the remote container. Unity-side spikes must run on a dev machine. |
| `synty-import.md` | Phase 0 | **done 2026-09-15** (Windows dev machine) | high | Pass with notes: Unity 6000.3.24f1 + URP 17.3.0, five packs imported headless under `Assets/Synty/` (7,222 assets), zero import errors, shaders SRP-native. Record and deviations in the file. |
| `synty-inventory.md`, `synty-inventory.csv` | Phase 0 | **generated 2026-09-15** by `Assets/Editor/Odyssey/SyntyInventory.cs` | high | 2,138 prefabs measured. Base walls 2.5 × 3.01 × 0.23 m, floors 2.5 × 2.5 m, Sections at exact 5 m multiples → **implied cell 2.5 × 2.5 × 3.0 m**, awaiting owner confirmation. |
| `phase1-answers.md` | Phase 1 | **complete 2026-09-15** (interview conducted) | high | Cell 2.5 × 2.5 × 3.0 m confirmed; template stamping; IvanMurzak MCP now; pragmatic TDD; two-way architecture benchmark, determinism first; Windows primary. ADRs 0001–0002 in `docs/adr/`. |
| `unity-mcp-server.md` | Phase 1, Q4 | done | medium | Recommend IvanMurzak/Unity-MCP (Linux binaries, Unity 6000.3 named, tests, console, Roslyn C#). Runner-up CoplayDev/unity-mcp. |
| `a-01-pawns.md` · `a-03-work-and-jobs.md` · `a-04-building-and-materials.md` · `a-12-map-generation.md` · `a-15-time-and-simulation.md` | Phase 2, Lane A wave 1 | dispatched 2026-09-15 | – | RimWorld mechanics for the vertical slice: needs/mood, jobs pipeline, building/materials, mapgen, tick model. |
| `b-going-medieval.md` · `c-cataclysm-dda.md` | Phase 2, Lanes B/C wave 1 | dispatched 2026-09-15 | – | Closest 3D-layer analogue; best open-source z-level reference. |
| `e-01-module-mapping.md` · `e-02-characters-animation.md` · `e-03-other-packs.md` · `e-04-tint-strategy.md` | Phase 2, Lane E wave 1 | dispatched 2026-09-15 | – | Synty fit at the confirmed 2.5 × 2.5 × 3.0 m cell, from the Phase 0 inventory. |
| `f-prior-art-here.md` | Phase 2, Lane F wave 1 | dispatched 2026-09-15 | – | Prior art in this repo and carry-forwards from *Ramble* (reference only). |

Related, outside this folder: `docs/brief.md` (the governing brief), `docs/reference/screenshots/README.md` (reference-image descriptions).

## Naming for Phase 2 files (so a fresh session can predict paths)

- Lane A, RimWorld mechanics: `a-01-pawns.md` … `a-16-modding-architecture.md`, numbered as in brief §5.
- Lane B, prior art: `b-going-medieval.md`, `b-dwarf-fortress.md`, `b-timberborn.md`, `b-stonehearth.md`, `b-oxygen-not-included.md`, `b-odd-realm-songs-of-syx.md`.
- Lane C, reference code: `c-cataclysm-dda.md`, `c-luanti.md`, `c-goblin-camp.md`, `c-unity-colony-sims.md`, `c-utility-ai.md`.
- Lane D, Unity spikes: `d-01-architecture.md` … `d-08-linux-ci-tooling.md`.
- Lane E, Synty fit: `e-01-module-mapping.md`, `e-02-characters-animation.md`, `e-03-other-packs.md`, `e-04-tint-strategy.md`.
- Lane F: `f-prior-art-here.md`.

Owner's note for Lane A (2026-09-15): the GitHub organisation `github.com/RimWorldMods` is supplied as a reference for understanding how RimWorld works — **understanding only, nothing is used from it**. The clean-room rule holds: mechanics, formulas, data shapes and design intent may be learned; no code, Def XML, names or flavour text may be copied, and any decompiled game source found there must never enter this repo (brief §1).

Owner's note for Lane E (2026-09-15): Blender is available as a gap-filler. Prefer Synty assets; use Blender only for modules missing at the cell size, UV or atlas fixes, and animation retargeting. `e-01-module-mapping.md` should list each gap with the chosen fix: Synty piece, Blender-made piece, or primitive placeholder.

Each Phase 2 file ends with a short section answering whichever of the twelve layer questions (brief §5) it touches.
