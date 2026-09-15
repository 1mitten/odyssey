# Phase 1 — interview answers

Status: **complete** (interview conducted 2026-09-15, owner answering directly in-session). Q1 was answered by the supplied packages; Q2–Q4 and three process questions (Q5–Q7) were answered in the interview. Phase 2 is cleared to start.

## Q1 — Which additional Synty packs are installed? **Answered**

On 2026-09-15 the owner supplied five `.unitypackage` files (Windows dev machine, `C:\Users\timjo\Downloads\`) with the instruction to hook them up and analyse them:

| Package file | Pack | Version | Contents summary |
|---|---|---|---|
| `POLYGON_SciFi_City_Unity_2022_3_v1_3_3` | POLYGON Sci-Fi City | 1.3.3 | The core pack: 1,127 prefabs, 1,075 FBX, demo + overview scenes |
| `POLYGON_Farm_Unity_2022_3_v1_7_3` | POLYGON Farm | 1.7.3 | 993 prefabs — crops, tools, farm structures (Lane E terrain/plants gap) |
| `POLYGON_Western_Frontier_Unity_2022_3_v1_7_2` | POLYGON Western Frontier | 1.7.2 | 824 prefabs — frontier-era buildings and props |
| `POLYGON_Particle_FX_Unity_2022_3_v1_4_1` | POLYGON Particle FX | 1.4.1 | 675 prefabs of effects (fire, smoke, sparks…) |
| `ANIMATION_Base_Locomotion_Unity_2021_1_v1_1_3` | ANIMATION Base Locomotion | 1.1.3 | 781 animation FBX files + Input System sample controller — the pawn animation source |

All five self-install under `Assets/Synty/…` (verified by reading the package path tables before import), so the licensed-asset boundary in `.gitignore` holds without moving anything. The four POLYGON packs share a common `Assets/Synty/PolygonGeneric` base (Shader Graph shaders, shared textures) plus `Assets/Synty/SyntyPackageHelper` (editor helper). Shaders are `.shadergraph` (SRP-native, URP-compatible) with built-in-pipeline versions under `Shaders/Legacy/` — these are the 2022.3-era SRP rebuilds, not the old built-in packs.

Owner guidance recorded from the same message: if suitable shaders/characters matching the reference screenshots cannot be found, that is acceptable — visual fidelity to the screenshots is not a blocker for getting the assets hooked up.

## Q2 — Cell size. **Confirmed: 2.5 × 2.5 × 3.0 m**

The inventory (`docs/research/synty-inventory.md`, generated from the imported packs) measures the base building modules at **2.5 m wide × 3.0 m tall**: walls 2.50 × 3.01 × 0.23 m (97% base pivots), floors 2.50 × 2.50 × 0.10 m, stairs on a 2.5 m footprint, the ladder 3.0 m tall, and the larger `Section` pieces at exactly 5 m (two cells). Half-height (1.5 m) and half/quarter-width trim variants exist but the load-bearing pitch is unambiguous. **Owner confirmed 2026-09-15: one cell = 2.5 m × 2.5 m footprint, 3.0 m layer height.** ADR: `docs/adr/0002-cell-size-and-layer-model.md`.

## Q3 — Ruined-city generation. **Answered: template stamping**

Pre-authored shell templates built from Synty modules, stamped onto a street grid by worldgen with random damage. Not procedural building generation.

## Q4 — Unity MCP server. **Answered: IvanMurzak/Unity-MCP, installed now on the Windows machine**

Per the recommendation in `docs/research/unity-mcp-server.md`; installation per `docs/setup/local-dev.md` §5 (Windows notes in §8).

## Q5 — Test strategy (owner-raised). **Answered: pragmatic TDD**

Test-first for every Sim system (grid, pathfinding, needs, jobs, save/load) plus a determinism harness (same seed → same world-state hash after N ticks) and golden-master one-day headless runs. Presentation and editor tooling get smoke tests (headless scene loads); throwaway spikes are exempt until kept. The Sim assembly stays pure C# (no UnityEngine dependency) so its tests can also run under a plain dotnet SDK.

## Q6 — Architecture selection and threading (owner-raised). **Answered: two-way benchmark; determinism first**

Lane D1 narrows to a **two-way benchmark**: plain C# structs + Burst jobs versus DOTS/ECS, ticking a 250×250×40 grid with 50 agents; MonoBehaviour-per-thing is dropped as a known non-starter at this scale. Judged on tick cost, memory, GC pressure, headless testability and mod-patchability; the ADR records the numbers. Threading stance regardless of winner: **deterministic single-threaded tick first**; Burst jobs behind clean boundaries only on proven hot paths (pathfinding, light/temperature propagation). No free-threaded simulation. Patterns generally: sim/presentation split, composition root (no scattered manager singletons), data-driven Defs from day one; the storyteller-as-director arrives at M6.

## Q7 — Primary dev machine (owner-raised). **Answered: this Windows machine**

Windows 11 (`D:\code\odyssey`, Unity 6000.3.24f1, all five packs imported) is primary; the Pop!_OS instructions in `docs/setup/local-dev.md` remain as a secondary reference. `scripts/unity.sh` works on both (Git Bash on Windows).
