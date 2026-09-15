# 03 — Systems catalogue

**This is the no-drop contract** (brief §2, non-negotiable 1). Every system that RimWorld has appears here with a milestone, even where the prototype stubs it. A system may be deferred; it may not be silently forgotten. If a system is cut, it is cut *here*, in writing, with a reason.

Depth follows the fast-track decision (`docs/research/phase1-answers.md` Q9): systems inside the vertical slice are specified properly; systems outside it carry a milestone tag, the shape of their 3D and ruined-city adaptation, and their open questions — enough that nothing is a surprise later, not so much that the slice waits for them.

Milestones are from brief §7: **M0** foundations · **M1** world · **M2** pawns · **M3** build and dig (*vertical slice complete*) · **M4** environment · **M5** sustenance · **M6** danger · **M7** depth · **M8** openness.

Cell = 2.5 × 2.5 m footprint × 3.0 m height (ADR 0002). Map = 250 × 250 cells × ~40 layers.

## Summary

| # | System | Milestone | In the slice? | Research |
|---|---|---|---|---|
| 1 | Pawn needs, mood, thoughts, traits, skills, social | M2 (needs/mood/skills), M7 (social depth) | Yes, partly | `a-01-pawns.md` |
| 2 | Health, injuries, medicine, surgery | M6 | No — stub: alive/downed/dead | — |
| 3 | Work, jobs, priorities, hauling, schedule | M2–M3 | Yes | `a-03-work-and-jobs.md` |
| 4 | Building, materials, quality, roofs, support | M3 | Yes | `a-04-building-and-materials.md` |
| 5 | Rooms, beauty, impressiveness, wealth | M4 (rooms at M3 for enclosure only) | Partly | — |
| 6 | Temperature, weather, light, seasons | M4 | No — stub: uniform comfortable | `b-going-medieval.md` |
| 7 | Power and networks | M7 (lights at M4) | No | — |
| 8 | Plants, growing, cooking, nutrition, spoilage | M5 | No — stub: a starting food store | `e-03-other-packs.md` |
| 9 | Animals and wildlife | M5 | No — **no animal assets exist** | `e-03-other-packs.md` |
| 10 | Combat, cover, line of sight, turrets | M6 | No | `c-cataclysm-dda.md` (3D FoV) |
| 11 | Storyteller, incidents, threat points | M6 | No — slice is unattended and peaceful | — |
| 12 | Map generation (ruined city) | M1 | Yes | `a-12-map-generation.md` |
| 13 | Research, factions, trade, world map | M7 | No | — |
| 14 | Bills, stockpiles, inventory | M3 (stockpiles), M5 (bills) | Stockpiles yes, bills no | `a-14-bills-stockpiles-inventory.md` |
| 15 | Time, ticks, speeds, determinism, save/load | M0 | Yes | `a-15-time-and-simulation.md`, `d-06-save-load.md` |
| 16 | Modding architecture: Defs and patching | M0 (Defs), M8 (API) | Defs yes, API no | `d-07-data-pipeline.md` |
| 17 | Layers, structural support and collapse | M3 | Yes | `a-04-building-and-materials.md`, `b-going-medieval.md` |
| 18 | Pathfinding and reachability across layers | M2 | Yes | `d-04-pathfinding.md` |
| 19 | Salvage and digging (replaces mining/ore) | M3 | Yes | `a-12-map-generation.md` |
| 20 | Camera slicing, overlays, HUD and panels | M1–M3 | Yes | **owned by the UI session** |

Two corrections to the table above are worth stating plainly rather than hiding in a cell: system 8 is stubbed in the slice with a starting food store, and system 9 has **no art in any owned pack**, so animals ship as primitive placeholders or wait for a pack purchase (`e-03-other-packs.md`).

---

## Systems inside the vertical slice

### 1. Pawns — needs, mood, skills

**What it does.** Each colonist carries needs that fall over time (food, rest, and in the slice only those two plus a minimal joy), a mood computed from thoughts, skills that gain experience from work, and a mental-break threshold that, when crossed, interrupts their work.

**Model** (from `a-01-pawns.md`): needs are 0–1 scalars updated on a 150-tick interval with per-band fall rates. Mood is a difficulty base plus the sum of active thought offsets, with the displayed value drifting toward the target rather than snapping. Thoughts are either situational (recomputed from the world) or memories (created once, expiring after a duration, stacking to a limit with a diminishing multiplier). Breaks roll as mean-time-between events while below their threshold, so a miserable colonist breaks *probably soon*, not *at a fixed moment*.

**3D adaptation.** Only three environment queries need layer awareness, which is the reassuring finding: the beauty scan around the pawn, the "am I under a roof" check, and room detection. All three become (x, y, z) queries against the layer the pawn stands on; none of them needs to see other layers in the slice.

**Ruined-city adaptation.** Thought sources are reskinned, not restructured: sleeping in rubble, eating salvaged rations, working in a derelict hall.

**Slice scope.** Food, rest, a minimal joy need; mood from a small thought set; skills with experience gain; one break behaviour (wander idly) so the ten-day unattended run exercises the threshold path. Traits, social relations, opinion and the full break taxonomy are M7.

**Open questions.** Whether memories fade linearly or hold until expiry (research could not determine); the exact stacking-multiplier compounding rule. Both are tuning, not architecture — we pick a defensible rule and expose it as a Def constant.

### 3. Work and jobs

**What it does.** Turns "a colonist is idle" into "a colonist is carrying a steel plate to a stockpile", every tick, for every pawn, without the player micromanaging.

**Model** (from `a-03-work-and-jobs.md`): a think tree is traversed depth-first and the **first valid job wins** — this is an ordered scan, not a global utility argmax, and that matters for both performance and predictability. Player work priorities (1–4) order a flat, pre-sorted list of work givers; each giver scans candidate things or cells and returns a job or nothing. A job is a driver plus a sequence of toils. A separate constant tree runs on a short cadence to force reflex interrupts.

**Reservations** are the only inter-pawn coordination: a claim of (claimant, job, target, maxPawns, stackCount), tested during the scan, claimed all-or-nothing before the toils run, and released whenever the job ends. Single-threaded check-then-claim — which our determinism-first tick gives us for free.

**3D adaptation.** This is where pathfinding cost lands. A work giver that scans "all hauling candidates" must not scan 40 layers of a 250 × 250 map. Reachability is answered by the region graph (system 18), never by running A-star per candidate, and scans are ordered by a cheap distance estimate that counts a layer change as a real cost.

**Slice scope.** Work types: haul, construct, mine/salvage, cook (stubbed to "eat from store"), plus rest and eat as needs-driven jobs. Priorities as a 1–4 grid. Reservations in full — they are cheap and their absence causes the exact bugs an unattended ten-day run would expose.

### 4 & 17. Building, materials, roofs, support and collapse

**What it does.** Designation → blueprint → materials hauled → frame → work applied → built thing. And, uniquely for us, the third dimension: what holds a floor up, and what happens when it does not.

**Model** (from `a-04-building-and-materials.md`): a single construction-success roll happens at completion (rising with skill), and quality is rolled once from the finisher's skill. Material properties follow `final stat = base stat × material factor + material offset`, per stat — verified against RimWorld's own wall table. Deconstruction refunds half.

**The layer adaptation is the biggest single design departure in the project.** RimWorld roofs are massless annotations on a cell, supported if a roof-holding edifice stands within 6 cells. That model cannot survive being walked on. Ours:

- **A roof is a floor is a slab.** One thing, built of a material, occupying the boundary between layer *n* and layer *n+1*. It is the ceiling of the lower cell and the floor of the upper one.
- **Support is computed bottom-up and recursively**, not by a fixed radius. A slab is supported if it rests on a wall or pillar below, or if it is within a material-dependent span of something that is. Going Medieval's legible integer model (`b-going-medieval.md`) is the right shape: ground contact is full strength, each step away costs one, zero means it cannot be placed.
- **Support values are visible.** Going Medieval hides them and players over-engineer blindly; our build preview shows the support number and the cells that would be orphaned.
- **Collapse cascades**, and everything that was standing on the slab falls with it, taking damage.
- **Pre-existing ruined shells** are the interesting case: worldgen stamps buildings that are already standing, so their slabs must be *marked supported by construction* and then re-validated the moment a colonist mines a load-bearing wall out from under them. That single rule turns reclaiming a ruin into a genuine engineering problem, which is the point of the setting.

**Slice scope.** Build and deconstruct walls, slabs, doors and stairs from one or two materials; mine rubble and salvage; support and collapse working across three layers. Quality rolls and the full stuff table are M3-light: one factor per material, no quality tiers yet.

### 12 & 19. Map generation, digging and salvage

**Model** (from `a-12-map-generation.md`): RimWorld runs an ordered list of generation steps — noise grids, terrain, rock, scatter passes, roads, life, start spot. We invert the wilderness ratio: **the street grid generates first** as the skeleton, blocks are subdivided into plots, and building shells are stamped from pre-authored templates (Phase 1 Q3) with a damage pass applied afterwards. Fertility becomes an **intactness** grid driving pavement → rubble → soil. Ore lumps become **salvage deposits**; geothermal vents become live utility taps; ancient dangers become sealed vaults.

**Underground** (the answer to layer question 8): L−1 service stratum of utility tunnels and basements; L−2/−3 metro and buried-city seam carrying the richest salvage; L−4 to −6 engineered fill grading into soil; below that natural rock with mineral lumps and caves.

**Digging** (layer question 11) has three distinct meanings, resolved by depth: breach a slab, clear rubble and reuse an existing void, or mine rock. Only the last is slow.

**Slice scope.** One small map (much less than 250 × 250 — see the slice plan), a street grid, two or three shell templates, a damage pass, salvage deposits, one service layer below and one storey above ground.

### 14. Stockpiles and inventory

**Slice scope.** Stockpile zones with priority and a filter, item stacking, haul-to-best-stockpile. Bills and cooking are M5. Zones are **per layer** (layer question 6), with named storage groups sharing one settings record across layers. A hauler picks a destination by filter, then space, then priority, then distance; stair cost belongs in the region-link weight rather than in a special same-layer rule, so priority ordering stays intact. Model from `a-14-bills-stockpiles-inventory.md`.

### 15. Time, ticks, determinism, save/load

**Model** (from `a-15-time-and-simulation.md`): a fixed tick, speeds as tick-rate multipliers, and three tick groups (every tick / rare / long) with hash-offset phase spreading so a fraction of each population runs per tick. Cost scales with *ticking things*, not with cell count — which is why 2.5 million cells is not frightening while 20,000 always-ticking things would be.

**Determinism is a gate, not an aspiration.** Same seed, same state hash after N ticks, every time; and save → load → replay must match an uninterrupted run. The hazard list is known (`a-15`): unordered dictionary iteration, unseeded or UI-touched RNG, wall-clock timers, float accumulation, save/load asymmetry.

**Slice scope.** All of it. This is M0 and everything else is built on it.

### 16. Modding: Defs

**Slice scope.** A Def loader with inheritance, patch operations and load-time validation (`d-07-data-pipeline.md`), because retrofitting data-driven content onto hard-coded content is exactly the kind of rework the brief forbids. The scripting API, Workshop and hot reload for mods are M8.

### 18. Pathfinding and reachability

**Slice scope.** Region-graph reachability plus A-star with stairs and ladders as portal edges (`d-04-pathfinding.md`), layer-aware from the first commit. The D1 benchmark measures its cost at full scale before any of it is written.

---

## Systems outside the slice

Each carries enough detail that its eventual arrival is not a surprise.

### 2. Health — M6
Body-part hierarchy with capacities derived from parts, injuries, bleeding, infection, tending quality, surgery and prosthetics. **3D impact:** low; health is per-pawn. **Ruined-city:** infection risk from filthy ruins; salvaged medicine of variable quality. **Slice stub:** a pawn is healthy, downed or dead. **Open:** whether to model per-part health at prototype fidelity or a simplified pool first — the Def shape must allow the upgrade.

### 5. Rooms and beauty — M4 (enclosure only at M3)
Flood-fill room detection, roles inferred from contents, impressiveness from beauty, cleanliness, space and wealth. **3D impact, and this is layer question 3:** rooms are **per layer**. A room is a flood-filled region within one layer bounded by walls and the map edge, with its ceiling slab making it "enclosed". A stairwell connects two rooms; it does not merge them. Heat rises *between* rooms through the stair opening as a flow, rather than by making one taller room — this keeps room detection a 2D flood fill per layer, which is cheap and comprehensible, and it matches Going Medieval, where per-room temperature by enclosed volume is the praised behaviour. **Open:** whether a large atrium spanning layers needs a special case (deferred until it exists).

### 6. Temperature, weather, light — M4
Heat sources, insulation, per-room equalisation, outdoor temperature by season, rain and snow stopped by roofs, light and glow. **3D impact:** heat flows between vertically adjacent rooms through openings; depth moderates temperature (a sealed lower layer trends to a stable underground temperature). **Layer question 4, light:** sunlight reaches layer *n* only through missing or glazed slabs; a broken floor becomes a light shaft, which gives ruins a natural beauty and makes "roof it over" a real trade-off. **Slice stub:** uniform comfortable temperature, uniform light. **Open:** whether light propagates as a cheap per-cell flood per layer or a shadowcast (Cataclysm measured 3D shadowcasting at roughly 80× the 2D cost, which argues for the cheap version until it looks wrong).

### 7. Power and networks — M7, lights at M4
Power nets as connected-cell graphs, generators, batteries, conduits, short circuits. **3D impact:** conduits must connect vertically; the generic "network of connected cells" pattern is written once, layer-aware, and reused for power, and later for water or atmosphere if they arrive. **Ruined-city:** the city grid is dead; utility taps found by worldgen are the early power source. **Assets:** no battery, solar panel or lamp exists in any owned pack (`e-01-module-mapping.md`) — those are the committed Blender pieces.

### 8. Plants, growing and food — M5
Growth by light, temperature and fertility; seasons; growing zones; cooking bills; nutrition; spoilage and refrigeration. **3D impact:** growing zones need light, so they are rooftop, street-level or under artificial light — which makes hydroponics the natural underground answer and gives lower layers a reason to want power. **Assets:** the Farm pack covers this generously (~20 staged crop species, field tiles at exactly 2 × 2 cells), but **there is no stove in any owned pack** — cooking is a campfire until a suitable appliance is chosen.

### 9. Animals — M5
Wildlife spawning, taming, training, bonding, predators, husbandry. **Ruined-city:** city wildlife and feral synths rather than muffalo. **Blocking fact:** no animal prefab exists in any of the five owned packs, so this milestone needs a pack purchase or primitive placeholders. Flagged for the owner rather than silently descoped.

### 10. Combat — M6
Hit chance from skill, range, cover, light and weather; per-cell cover values; damage and armour; melee; downing versus death; turrets and traps. **3D impact, and this is layer question 5:** line of sight in three dimensions. Cataclysm DDA extended recursive shadowcasting to volumes with floors as binary occluders, and capped the vertical range to make it affordable — we adopt both. Shooting between layers is possible only through an opening, which makes a broken floor a firing position and a sealed slab genuine cover. **Open:** whether height confers an accuracy bonus (a design choice, not a research finding).

### 11. Storyteller and incidents — M6
Threat points from wealth and population, difficulty curves, incident selection and cooldowns, raid strategies and arrival modes. **3D impact, layer question 7:** verticality is the storyteller's new toy — tunnelling raids from the metro seam, drops through open sky onto a roof, breaches from the service stratum. That is a genuinely novel threat vocabulary and is the main reason this setting is worth building. **Slice:** absent by design; the ten-day run is peaceful precisely so it tests the simulation rather than the drama.

### 13. Research, factions, trade, world map — M7
Research tree and costs, faction goodwill, traders, caravans, world travel. Scoped lightly, as the brief instructs. **Ruined-city:** research is reverse-engineering salvage, which fits the setting better than it fits RimWorld.

### 20. Camera, overlays, HUD and panels — M1 onwards
**Owned by the separate UI session.** Its architecture (snapshot-read, intent-write; director catalogue; performance budget) is adopted by this project as a constraint on the simulation architecture — see `docs/design/ui-plan-reconciliation.md` and the benchmark, which times the view build as a judged phase. Two decisions already settled here: layers above the slice render ghosted and **non-interactive** (Going Medieval's top complaint is clicking the wrong layer), and overlays are chunk meshes, never per-cell UI elements.

---

## The twelve layer questions, mapped

Brief §5 requires committed answers. Current state, with the answer or its owner:

| # | Question | Answer | Where |
|---|---|---|---|
| 1 | Vertical movement model | Stairs two cells, ladders one, as portal edges in the region graph | `d-04-pathfinding.md`, §4/17 above |
| 2 | Is a roof a floor? | **Yes** — one material slab, recursive bottom-up support, visible support values, cascading collapse | §4/17 above |
| 3 | Do rooms span layers? | **No** — per-layer flood fill; heat flows between them through openings | §5 above |
| 4 | Light to lower layers | Only through missing or glazed slabs; broken floors are light shafts | §6 above |
| 5 | Shooting and sight in 3D | Volumetric shadowcasting with floors as occluders and a capped vertical range | §10 above |
| 6 | Zones per layer or 3D? | **Per layer**, with cross-layer storage groups sharing one settings record | `a-14-bills-stockpiles-inventory.md` |
| 7 | Storyteller verticality | Tunnelling, sky drops, service-tunnel breaches | §11 above |
| 8 | What is underground | Service / metro / fill / rock strata | `a-12-map-generation.md` |
| 9 | Camera slice and depth UI | Ghosted, non-interactive above the slice | UI session |
| 10 | Unit of simulation for gas, fire, water, sound | Per-cell active-frontier propagation, layer-aware; cost measured by D1 phase 1 | `a-15`, D1 benchmark |
| 11 | What digging means | Breach slab / clear rubble / mine rock, by depth; salvage replaces ore | `a-12-map-generation.md` |
| 12 | What the slice must prove | Questions 1, 2, 3 (enclosure only), 6, 10, 11 — see `docs/plans/vertical-slice.md` | this document |
