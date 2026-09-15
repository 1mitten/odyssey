# Brief: 3D layered colony sim in the RimWorld mould — Unity edition

> Usage: paste this whole file as your opening message in Claude Code, or save it as `docs/brief.md` and run `/scope docs/brief.md`.
> The interview has already been done; the answers are baked in below as **Agreed** decisions. Phase 1 is therefore short: ask only what §4 leaves open, then proceed.
> This is a research-first brief. Gameplay code is not written until Phase 4, and only after the plan is approved.

---

## 1. Role and working agreement

You are the lead engineer and research lead on a new game. Work in the order **Ground → Interview → Research → Plan → Execute** with a hard stop after each phase. End your turn at every stop and wait for my answer. Never run a later phase on assumed answers.

Rules that hold for the whole project:

- **Research lives in subagents.** One subagent, one question, a hard cap ("stop after N searches / N reads and report"), and a fixed return format: findings, sources (URLs), confidence (high/medium/low), what could not be determined. Only findings come back into the main thread. Write every result to `docs/research/<slug>.md` and keep `docs/research/INDEX.md` current.
- **Commit to recommendations.** Rank options and back one. If two are tied, name the observation that breaks the tie and the cheapest experiment that gets it. No menus of hedged choices.
- **Clean room.** RimWorld's code, Def XML text, art, audio, names and flavour text are Ludeon's copyright. We study *mechanics, formulas, data shapes and design intent* and re-implement from understanding. Never paste Def contents, decompiled code or asset files into this repo. Do not decompile assemblies into the repo. Invent our own names for creatures, storytellers, factions and items. Screenshots I supply are for analysing layout and information design, not for tracing.
- **Licensed assets stay licensed.** Synty packs live under `Assets/Synty/` and that folder is gitignored (or the repo stays private with LFS). Never copy Synty meshes or textures anywhere else, never commit them to a public remote.
- **Files outlive context.** Every phase produces files under `docs/`. Assume I will `/clear` between phases and that the next session knows nothing except what is written down.
- British English in all documentation.

---

## 2. Vision and agreed decisions

A colony sim that reproduces every pillar of RimWorld — pawns with needs, moods, skills, health and relationships; a job/work-priority AI; storyteller-driven incidents; building, mining, farming, animals, research, power, temperature, combat, trade and factions; data-driven content and a modding layer — in **true 3D with discrete vertical layers**. Colonists reclaim the shells of a ruined sci-fi city, build up into its towers, dig down beneath its streets, roof one layer to floor the next, and the camera slices the world at any layer the way Dwarf Fortress and *Going Medieval* do.

| Decision | Agreed |
|---|---|
| Purpose | **Prototype to test the idea; decide later.** Architect the world grid, layer model and Def system as if they will graduate; everything else may be throwaway. Say so when you cut a corner. |
| Engine | **Unity 6.3 LTS (latest 6000.3.x patch), URP, C#**, nullable enabled, analysers on. Personal licence. Not 2022.3: it is out of support and the Synty pack targets 2022.3*+*. Not a 6.4–6.6 Update release: each is only supported until the next one ships. Plan one upgrade, to **6.7 LTS** when it lands later in 2026, and no other. Phase 0 proves the Synty import on 6.3. |
| Simulation architecture | **Undecided — research and recommend** (see Lane D). Candidates: plain C# sim objects with no `MonoBehaviour` dependency plus Burst/Jobs on hot paths (RimWorld's model); full DOTS/ECS; classic `MonoBehaviour`-per-thing. Judge on: 250×250×40 tick cost, mod-friendliness, debuggability, Claude Code's ability to test it headless. |
| Layer model | **Discrete cells, one cell = one layer in height.** No slopes, no half-heights. Stairs occupy two cells, ladders one, lifts later. A built roof is the floor of the layer above. Unsupported spans collapse (*Going Medieval*-style integrity). |
| Setting | **A sci-fi city-world.** Prototype map type: **empty natural wilderness** — grass, trees, stone and ore, built up from nothing (**superseded the ruined city on 2026-09-15; see ADR 0008**). The ruined-city generator is built and kept as a selectable map type, alongside the other later types: outskirts/frontier, living district (needs city-population AI — far future). |
| Content scope | **Core game only.** No belief systems, genes, titles or horror layer. Those are expansions with their own roadmaps. |
| Modding | **Data-driven Defs from day one**, shaped like RimWorld's (inheritance, patch operations) so modders feel at home. Scripting API and Workshop deferred to M8. Keep sim classes public, unsealed and virtual where cheap so Harmony-style patching stays possible. |
| Scale target | 250×250 footprint, ~40 usable layers, 50 colonists, 300 animals, 60 FPS at 3× speed on a 2022 mid-range laptop. The RTX 5070 Ti dev machine is not the target. |
| Vertical slice | **Minimal:** one ruined-city map, five pawns, three layers, dig down and build up, haul, eat, sleep, needs and mood, a stockpile. Done when five pawns survive ten in-game days unattended with no errors in a headless run. |
| Art | **Synty POLYGON Sci-Fi City** (owned: 509 assets, modular building sections, 20 Mecanim-rigged characters, 9 vehicles, 18 weapons, URP shader-graph materials). Further owned Synty packs for terrain/plants/animals: *list to be supplied*. Animations from an owned Synty ANIMATION pack or Mixamo. |
| Claude Code workflow | **Code-first.** Scenes and prefab variants generated by editor scripts. EditMode/PlayMode tests and a headless one-day simulation run via `-batchmode -nographics` in CI. A Unity MCP server installed so Claude Code can open scenes, run tests and read the console. I do visual work in the editor myself on Pop!_OS. |
| Multiplayer | None, ever. Design nothing for it. |
| Prior project | *Ramble* (Godot 4, top-down) is reference only. No code reuse. |

Non-negotiables that follow:

1. Every RimWorld system in §5 exists in the systems catalogue with a milestone, even if the prototype stubs it. Nothing gets silently dropped.
2. Every system that touches a cell — pathing, light, temperature, roofs, zones, fire, gas, sound — is layer-aware from the first commit. Retrofitting z is the one mistake this project cannot afford.
3. The cell size is derived from the Synty modular pieces, not chosen abstractly. Nothing is built until Phase 0 reports it.

---

## 3. Phase 0 — Ground (at most eight tool calls, then Phase 1)

1. Inspect the working directory: empty, Unity project, or scaffold. Note dotnet SDK, Unity Hub/Editor version installed, git, CI, and whether a Unity MCP server is already configured.
2. **Synty inventory.** Locate `Assets/Synty/`. Report: pack versions present; the bounding dimensions of the modular wall, floor, roof, door, stair and pillar pieces (read the FBX/prefab bounds — do not guess); whether pieces snap to a consistent metre grid; character rig type and any included animations; which URP materials import with warnings. This report fixes the cell size.
3. **Import spike.** If the project is fresh: create the Unity 6.3 LTS URP project, import the Sci-Fi City package, resolve shader warnings, open the demo scene in batchmode and confirm it loads without errors. Record the outcome in `docs/research/synty-import.md`. If it fails irrecoverably, stop and report before doing anything else.
4. If a RimWorld install path is provided later, its `Data/Core/Defs` folder is a legitimate reference for *structure* (which Def types exist, what fields they carry). Report structure only, never contents.

---

## 4. Phase 1 — Interview (STOP after this)

Most questions are answered above. Ask only these, each with your assumption and why it matters:

- **Q1 — Which additional Synty packs are installed?** Assumption: none yet; the prototype uses primitives for terrain, plants and animals until they arrive. Why: decides whether M5 (sustenance) can be visually complete or must ship with placeholders.
- **Q2 — Cell size.** State the size the inventory implies (e.g. 2 m × 2 m × 3 m if walls are 2 m wide and 3 m tall) and ask for confirmation. Why: everything downstream snaps to it. **Answered 2026-09-15: 2.5 × 2.5 × 3.0 m, measured (ADR 0002). The 2 m above is an illustrative example in the question, not the answer.**
- **Q3 — Ruined-city generation.** Assumption: buildings are pre-authored shell templates (from Synty modules) stamped onto a street grid by worldgen, with random damage; not procedural buildings. Why: template stamping is a week; procedural building generation is a milestone.
- **Q4 — Unity MCP server choice.** Assumption: the most maintained open-source Unity MCP server that supports Linux and Unity 6; name it after a quick search. Why: it is the difference between autonomous iteration and asking you to click.

Anything else the scenario has settled — do not ask. Then stop.

---

## 5. Phase 2 — Research (only after answers)

Dispatch subagents in parallel across six lanes. Each produces one file in `docs/research/`. No subagent gets more than one system.

### Lane A — RimWorld mechanics, system by system

Sources, in order of authority: the RimWorld Wiki (rimworldwiki.com — mechanics pages cite exact formulas), the official modding tutorials on the same wiki, Ludeon forum design posts, Tynan Sylvester's *Designing Games* and his talks on storytelling AI, and the modding community's explanations of internals. For each system extract: **the formulas, the data shapes (what fields a Def carries), the state machines, the tick cadence (every tick / rare tick / long tick), and the tuning constants** — then write a "3D/layer impact" paragraph and a "ruined-city impact" paragraph.

1. **Pawns** — needs (food, rest, joy, comfort, beauty, outdoors, mood), thoughts and memories with decay, traits, skills with passions and learning curves, social relations and opinion, mental breaks and thresholds.
2. **Health** — body-part hierarchy, injuries, bleeding, infections, diseases, chronic conditions, capacities derived from parts, tending, medicine quality, surgery, prosthetics.
3. **Work and jobs** — work types and priorities, the ThinkTree → JobGiver → WorkGiver → Job → Toil structure, reservations, interruption and failure, hauling, forbid/allow, the daily schedule.
4. **Building and materials** — designations, blueprints → frames → built things, "stuff" materials with stat multipliers, construction skill and quality, deconstruction, roofs and support radius. *Layer impact is largest here: roofs as floors, support and collapse, reclaiming pre-existing shells.*
5. **Rooms and beauty** — room detection via flood fill, roles, impressiveness, beauty, cleanliness, space, wealth. *Decide whether rooms span layers.*
6. **Temperature and environment** — heat sources, insulation, room equalisation, outdoor temperature by season, rain/snow and roofing, weather, light and glow, time of day.
7. **Power and networks** — power nets, generators, batteries, conduits, short circuits; the general "network of connected cells" pattern. *Networks must connect vertically.*
8. **Plants, growing and food** — growth by light/temperature/fertility, seasons, zones, cooking bills, nutrition, spoilage and refrigeration. *In a ruined city: hydroponics, rooftop soil, salvaged food.*
9. **Animals** — wildlife spawning, taming, training, bonding, predators and manhunters, husbandry. *City wildlife and feral synths instead of muffalo.*
10. **Combat** — hit chance formula (skill, distance, cover, light, weather), cover as a per-cell value, damage and armour, melee, downing vs. death, turrets and traps. *Height advantage, shooting between layers, 3D line of sight.*
11. **Storyteller and incidents** — threat points from wealth and population, difficulty curves, incident selection and cooldowns, raid strategies and arrival modes, the rhythm of tension and relief. Capture design intent as much as maths.
12. **Map generation** — biomes, terrain and fertility, rock and ore, geothermal, rivers, roads, ruins, starting resources. *Becomes: street grid, building-shell templates, damage, salvage deposits, underground strata and utility tunnels.*
13. **Research, factions, trade and world map** — research tree and costs, faction goodwill, traders and caravans, world map travel. Scope lightly; late roadmap.
14. **Bills, stockpiles and inventory** — bill types and counting, stockpile priorities and filters, stacking, hauling to storage, deterioration.
15. **Time and simulation** — ticks per hour/day, game speeds, tick groups, determinism, save/load of a mid-tick world.
16. **Modding architecture** — how Defs load, inherit and patch; how Harmony patching works and what modders reach for most (this tells us where extension points go). Reference repositories to read for *architecture*, never to copy: `pardeike/Harmony`, `UnlimitedHugs/RimworldHugsLib`, `CombatExtended/CombatExtended`, `edbmods/EdBPrepareCarefully`, the Vanilla Expanded framework repositories, and the **Z-Levels** RimWorld mod (search GitHub for the current fork). Z-Levels is the single most relevant: it bolted vertical layers onto a 2D engine; document where it hurt so we design those pains away.

### Lane B — 3D-layer prior art (design)

One subagent each: world representation, camera slicing, cross-layer pathing, how the UI shows depth, what players praise and complain about (Steam reviews, forums, post-mortems, talks).

- **Going Medieval** — the closest analogue: 3D, multi-storey, RimWorld-derived. Structural integrity, cut-away camera, temperature by volume.
- **Dwarf Fortress** — z-levels, dig/build in every direction. Study its UI failures as much as its depth.
- **Timberborn** — 3D grid, vertical building, clean layer UI (its slopes are what we chose *not* to do; note why).
- **Stonehearth** / **ACE** — voxel multi-level colony sim; why it struggled with performance and AI.
- **Oxygen Not Included** — best-in-class cell-based gas, liquid, heat and pressure; generalises to 3D.
- **Odd Realm** and **Songs of Syx** — layered/large-scale scaling lessons.

### Lane C — Reference code (open source, permissively licensed, read for technique)

- **Cataclysm: Dark Days Ahead** — open-source z-levels, 3D field of view, cross-level pathfinding. The best readable reference for layer-aware sim logic.
- **Luanti (Minetest)** — chunked storage, meshing, streaming.
- **Goblin Camp** — open-source DF-like; jobs and stockpiles.
- Search GitHub for Unity colony-sim and RimWorld-like projects; report anything that already solves a hard piece, with licence.
- Utility-AI literature (Dave Mark, *Behavioral Mathematics for Game AI*): RimWorld's ThinkTree is closer to utility scoring than to pure behaviour trees.

### Lane D — Unity architecture and feasibility spikes

Each is a question with a measurable answer:

1. **The architecture decision.** Build a throwaway benchmark of the three candidates ticking a 250×250×40 cell grid with 50 agents: (a) plain C# sim objects + Burst jobs on the grid, (b) Entities/DOTS, (c) `MonoBehaviour`-per-thing. Report tick time, memory, GC pressure, and — critically — how each is tested headless and how a modder would patch it. **Recommend one** and write the ADR.
2. World storage: chunked `NativeArray` of cell structs vs. managed arrays — update and query cost at scale.
3. Rendering: `Graphics.RenderMeshInstanced` / GPU Resident Drawer with Synty modules, chunk-level culling, and a cut-away at layer N via clip plane or per-layer visibility. Draw calls and frame time.
4. Pathfinding: hierarchical A* across layers with stairs and ladders as portals, in Burst; cost of 50 agents replanning per second.
5. Threading: simulation ticks off the main thread and reconciliation with the scene without stalls.
6. Save/load: whole world plus mid-job pawn state; file size, load time, format (binary vs. JSON).
7. Data pipeline: XML Defs with inheritance and patch operations; validation at load; hot reload in the editor.
8. **Linux CI and tooling:** `-batchmode -nographics` test and headless-sim runs on Pop!_OS; the chosen Unity MCP server's capabilities and limits; licence activation in CI.

### Lane E — Synty asset fit

1. From the Phase 0 inventory: map every Sci-Fi City module to a game concept (wall, floor, roof, door, window, stair, pillar, furniture, power, light, salvage). Flag concepts with no asset — these get primitive placeholders, not scope cuts.
2. Characters: pipeline from Synty rigs to an animation set (owned ANIMATION pack or Mixamo); required clips for the vertical slice (idle, walk, carry, mine, build, sleep, eat, downed).
3. Which other Synty packs, if any, fill terrain/plants/animals; confirm URP compatibility and whether they share the Synty generics folder.
4. Material and colour atlas strategy so every building module can take a "stuff" tint at runtime without per-instance materials.

### Lane F — Does it already exist here?

Search this repo for prior art; skim *Ramble*'s design docs for any decision worth carrying forward. Report per item: reuse, adapt, or ignore.

### Lane G — UI and information design (added 2026-09-15, out of phase order)

Added at the owner's request, after Phase 1 but before Phase 2, because the interface's read
and write contract constrains the Lane D1 architecture decision and should be an input to it
rather than an output of it. Lane B keeps ownership of the depth-display prior art; Lane G
cites it and does not duplicate it.

1. **Reference UI taxonomy** — region by region, with a 3D-layer impact note and a ruined-city
   impact note for each, and the density techniques that make an icon-first HUD work.
   `g-01-ui-information-design.md`.
2. **Unity runtime UI framework** — UI Toolkit against uGUI against immediate mode, for a dense
   colony-sim HUD on the target laptop, with the experiments that would settle it.
   `g-02-unity-ui-framework.md`.

Note for whoever runs Lanes A and B: **`rimworldwiki.com` and `steamcommunity.com` are blocked
by the remote container's egress proxy.** Both lanes name them as primary sources. Either run
those subagents from the dev machine, or have the owner supply the pages.

### The twelve layer questions every research file must help answer

1. Vertical movement: stairs (two cells), ladders (one), lifts later — confirm the pathing model.
2. Is a roof a floor? What supports it, what collapses, and how do pre-existing ruined shells fit the support model?
3. Do rooms span layers? Does heat rise?
4. How do light and sun reach lower layers — shafts, skylights, broken floors?
5. Can you shoot up and down? What is line of sight in 3D?
6. Are zones, stockpiles and growing areas per layer or 3D volumes?
7. How does the storyteller use verticality — tunnelling raids, drops through open sky, breaches from utility tunnels?
8. What does worldgen put underground — strata, sewers, service tunnels, salvage, water?
9. How does the camera slice, and how does the UI show what is above and below?
10. What is the unit of simulation for gas, fire, water and sound across layers?
11. What does "digging" mean in a city — rubble, concrete, bedrock — and what replaces ore?
12. Which of the above the vertical slice must prove, and which it can stub.

---

## 6. Phase 3 — Design and plan (STOP after this for review)

Write, in this order:

- `docs/design/00-vision.md` — the pitch, agreed decisions, the twelve layer answers.
- `docs/design/01-architecture.md` — the Lane D recommendation as built: sim/presentation split, threading, tick groups, chunk layout, save format, test strategy.
- `docs/design/02-world-and-layers.md` — cell model at the confirmed cell size, layers, support and collapse, vertical connectors, ruined-city worldgen.
- `docs/design/03-systems-catalogue.md` — one row per RimWorld system: what it does, its 3D adaptation, its ruined-city adaptation, milestone, dependencies, open questions. This is the contract that nothing gets dropped.
- `docs/design/04-data-model.md` — the Def system: format, inheritance, patching, validation, hot reload, the first twenty Def types.
- `docs/design/05-ai-and-jobs.md` — needs, thoughts, the think/job/toil pipeline, reservations, priorities.
- `docs/design/06-rendering-and-camera.md` — cut-away, instanced modules, overlays (zones, temperature, beauty), depth cues, Synty tint strategy.
- `docs/design/07-modding.md` — extension points chosen because Lane A showed modders need them.
- `docs/design/08-milestones.md` — the roadmap in §7, definition of done per milestone, the vertical slice defined precisely.
- `docs/design/09-ui-and-input.md` — the interface architecture: directors, the sim/UI contract, performance budget, assembly layout, input, icons, modding seams. *(Added 2026-09-15; written ahead of Phase 3 because it constrains the Lane D1 ADR.)*
- `docs/design/10-ui-panel-catalogue.md` — every HUD region and panel specified: slot, contents, view fields read, intents emitted, cadence, owning director, layer-awareness, icon keys, milestone. *(Added 2026-09-15.)*
- `docs/adr/` — one ADR per irreversible decision: engine and version, architecture, cell size and layer model, data format, threading model, asset licensing boundary.
- `docs/plans/vertical-slice.md` — the first execution plan: ordered units, dependencies, size, done-criteria.

Report the file paths and the three things you are least sure about. Stop. Do not implement.

---

## 7. Phase 4 — Execution roadmap (each milestone is its own plan file and its own fresh context)

- **M0 Foundations** — Unity 6.3 URP project, Synty imported, assembly definitions, EditMode/PlayMode tests, Linux batchmode CI, Def loader with validation, sim/presentation split per the ADR, fixed-tick loop with speeds, save/load skeleton, Unity MCP verified end to end.
- **M1 World** — chunked 3D grid at the agreed cell size, ruined-city worldgen (street grid, shell templates, damage, underground strata), cut-away camera, layer navigation UI, cell inspection.
- **M2 Pawns** — Synty characters with animation set, needs, mood, skills, layer-aware pathfinding with stairs and ladders, basic hauling; three pawns living in a ruined shell.
- **M3 Build and dig** — designations, blueprints, frames, materials, mining/rubble clearance across layers, roofs-as-floors, support and collapse, stockpiles. **Vertical slice complete at end of M3** (with eat/sleep from M2).
- **M4 Environment** — rooms across layers, temperature, light, weather, seasons, fire.
- **M5 Sustenance** — growing zones and hydroponics, plants, cooking bills, animals and taming; placeholders where packs are missing.
- **M6 Danger** — health and injuries, 3D cover and line of sight, first raid, storyteller v1 with threat points.
- **M7 Depth** — research, power nets, trade, factions, world map.
- **M8 Openness** — modding API, content pass, performance pass, decision point: graduate or shelve.

Every milestone ends with: tests passing, a headless one-day simulation with no errors, a short `docs/milestones/Mx-report.md`, and a stop for review.

---

## 8. Screenshots

When I supply RimWorld or reference-game screenshots: save them under `docs/reference/screenshots/<game>/`, describe each in `docs/reference/screenshots/README.md`, and use them for **information-design analysis only** — what the UI communicates, how density is managed, which overlays exist. They inform our own UI; nothing is traced or copied.

---

## 9. Definition of done for this brief

- `docs/research/` has a file per lane item with sources and confidence, plus an index; `synty-import.md` and the asset inventory exist.
- The architecture ADR names one option and the benchmark numbers that chose it.
- `docs/design/` and `docs/adr/` exist as listed; the systems catalogue is complete with no RimWorld pillar missing.
- The twelve layer questions have committed answers with reasoning.
- `docs/plans/vertical-slice.md` is executable by a fresh session with no other context.
- You have told me the three biggest risks and the cheapest experiment for each.
