# Phase 1 — interview answers

Status: **partially answered** (updated 2026-09-15). Q1 is answered by the owner supplying the packages; Q2 has the inventory's implied size and awaits confirmation; Q3 and Q4 remain on their stated assumptions.

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

## Q2 — Cell size. **Proposed: 2.5 × 2.5 × 3.0 m — awaiting owner confirmation**

The inventory (`docs/research/synty-inventory.md`, generated from the imported packs) measures the base building modules at **2.5 m wide × 3.0 m tall**: walls 2.50 × 3.01 × 0.23 m (97% base pivots), floors 2.50 × 2.50 × 0.10 m, stairs on a 2.5 m footprint, the ladder 3.0 m tall, and the larger `Section` pieces at exactly 5 m (two cells). Half-height (1.5 m) and half/quarter-width trim variants exist but the load-bearing pitch is unambiguous. Recommendation: **one cell = 2.5 m × 2.5 m footprint, 3.0 m layer height**. Nothing is built until the owner confirms.

## Q3 — Ruined-city generation. **Unanswered; assumption stands**

Assumption: pre-authored shell templates stamped onto a street grid with random damage, not procedural buildings.

## Q4 — Unity MCP server. **Unanswered; recommendation stands**

Recommend IvanMurzak/Unity-MCP (`docs/research/unity-mcp-server.md`). Not yet installed on either dev machine.

## Environment note (affects docs, not decisions)

Phase 0/1 work on 2026-09-15 ran on a **Windows 11** dev machine (`D:\code\odyssey`, Unity CLI/Hub beta, editors under `C:\Program Files\Unity\Hub\Editor`), not the Pop!_OS machine the setup docs describe. `scripts/unity.sh` was extended to find the editor on Windows Git Bash. Whether Windows replaces or complements Pop!_OS is for the owner to say; the docs treat both as dev machines.
