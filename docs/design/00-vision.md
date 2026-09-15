# 00 — Vision

## The pitch

A colony sim in the RimWorld mould, in **true 3D with discrete vertical layers**. Colonists reclaim the shells of a ruined sci-fi city: they build up into its towers, dig down beneath its streets, roof one layer to floor the next, and the camera slices the world at any layer the way Dwarf Fortress and *Going Medieval* do.

Everything RimWorld does well — pawns with needs, moods, skills and relationships; a job and work-priority AI; storyteller-driven incidents; building, mining, farming, animals, research, power, temperature, combat, trade and factions; data-driven content with a modding layer — is in the plan (`03-systems-catalogue.md`). The twist is the third dimension, and the discipline is that every system is layer-aware from its first commit, because retrofitting *z* is the one mistake this project cannot afford.

The setting earns the mechanic. Salvage replaces ore, so a colony grows by emptying the city rather than tunnelling a mountain. Mining a wall for materials can drop the floor above it. Raids come up from the metro as readily as they come down the street.

## Agreed decisions

Settled before or during the interviews; see `docs/research/phase1-answers.md` and `docs/adr/`.

| Decision | Value |
|---|---|
| Purpose | Prototype to test the idea. Architect the world grid, layer model and Def system as if they will graduate; everything else may be throwaway, and cut corners are named when taken. |
| Engine | Unity 6.3 LTS (6000.3.24f1), URP, C#, nullable enabled. One planned upgrade, to 6.7 LTS. (ADR 0001) |
| Cell | 2.5 m × 2.5 m × 3.0 m, derived from the Synty modules, not chosen abstractly. (ADR 0002) |
| Layer model | Discrete cells, one cell of height per layer. No slopes, no half-heights. A built slab is the floor above. Unsupported spans collapse. |
| Simulation architecture | Decided by benchmark. (ADR 0005) |
| Threading | Deterministic single-threaded tick first; Burst jobs only on benchmark-proven hot paths. Never a free-threaded simulation. |
| Tests | Pragmatic TDD: test-first for Sim, with determinism and save-round-trip as first-class gates. |
| Setting | A sci-fi city-world. **Prototype map: empty natural wilderness** — grass, trees, stone, ore — and you build from nothing (ADR 0008). The ruined-city generator is kept as a later map type. |
| Content scope | Core game only. No belief systems, genes, titles or horror layer. |
| Modding | Data-driven Defs from day one with inheritance and patch operations. Scripting API and Workshop at M8. |
| Scale target | 250 × 250 × ~40, 50 colonists, 300 animals, 60 FPS at 3× speed on a 2022 mid-range laptop. |
| Art | Synty POLYGON packs (Sci-Fi City, Farm, Western Frontier, Particle FX, Base Locomotion), licensed, never committed. Target look: the concept renders — low-poly under URP, cyan emissive trim, roofless cut-away interiors. |
| UI | Owned by a separate design line; snapshot-read / intent-write is a constraint on the simulation, not an afterthought. |
| Multiplayer | None, ever. Design nothing for it. |

## The twelve layer questions, answered

The brief demands committed answers. Reasoning and evidence are in `03-systems-catalogue.md` and `02-world-and-layers.md`; the answers are:

1. **Vertical movement.** Only through connectors: stairs across two cells, ladders in one, holes downward-only as falls, lifts later. Each is a portal edge in the region graph. Two vertically adjacent open cells with an intact slab between them are *not* connected.
2. **Is a roof a floor?** Yes — one material slab at the boundary between layers, ceiling below and floor above. Support is an integer computed bottom-up: full strength on ground or a wall beneath, otherwise the best horizontal neighbour minus one, and zero collapses. Collapse cascades and leaves rubble. Pre-existing ruined shells begin supported-by-construction and are revalidated the moment anything beneath them changes.
3. **Do rooms span layers?** Regions are one substrate grouped two ways: **districts** group them for reachability, **rooms** group them for atmosphere, because walking-connected and air-connected are different questions in 3D. Whether an atmosphere grouping crosses a stairwell is an M4 tuning choice over that structure rather than an architectural commitment. The slice needs enclosure only. See `05-ai-and-jobs.md` §4.
4. **Light below.** Sunlight reaches a layer only through missing or glazed slabs. A broken floor is a light shaft, which makes roofing over a genuine trade-off.
5. **Shooting and sight in 3D.** Volumetric shadowcasting with floors as binary occluders and a capped vertical range, following Cataclysm DDA, which measured naïve 3D field of view at roughly eighty times the 2D cost. You can shoot through an opening, never through an intact slab.
6. **Zones and stockpiles.** **Per layer.** A zone is a set of cells on exactly one layer, because contiguity is meaningless across a stairwell and capacity belongs to a floor rather than a volume. The warehouse-across-three-floors case returns as named **storage groups** sharing one settings record across layers: geometry per layer, configuration grouped.
7. **Storyteller verticality.** Tunnelling raids from the buried-city seam, drops through open sky onto roofs, breaches from the service stratum. The new threat vocabulary is the main creative reward of the setting.
8. **Underground.** L−1 service tunnels and basements; L−2/−3 metro and buried-city seam with the richest salvage; L−4 to −6 engineered fill grading into soil; then natural rock, lumps and caves.
9. **Camera and depth UI.** A slice at the active layer with layers above ghosted and **non-interactive** — Going Medieval's top complaint is clicking something on the wrong floor, and it is free to avoid now.
10. **Unit of simulation for gas, fire, water and sound.** Per-cell, layer-aware, propagated over an active frontier rather than by sweeping the grid. Cost measured directly as phase 1 of the D1 benchmark.
11. **What digging means.** Three different actions by depth: breach a slab (quick), clear rubble and reuse a void (moderate), mine rock (slow). Salvage replaces ore.
12. **What the slice must prove.** Questions 1, 2, 3 (enclosure only), 6, 10 and 11. Questions 4, 5, 7 and 9 are stubbed or deferred; question 8 is generated but barely exercised.

## What would make this fail

Named now, so they are watched rather than discovered:

- **Retrofitting z.** Guarded by the layer-awareness rule and by the fact that every design document above is written in (x, z, y).
- **Non-determinism creeping in.** Guarded by making same-seed-same-hash and save-round-trip milestone gates rather than aspirations, with the RimWorld Multiplayer mod's desync hazard list as the checklist.
- **Pathfinding cost at 50 agents across 40 layers.** Guarded by answering reachability from a region graph rather than by running A-star per candidate, and by measuring it at full scale before writing it.
- **Scope.** Guarded by the catalogue: everything is listed, most of it is deferred in writing, and the slice's stub list is explicit.
