# Lane B — Going Medieval: layers, integrity, cut-away camera, lessons

## Question

How does Going Medieval (Foxy Voxel, Unity; early access 2021-06-01, 1.0 released 2026-03-17) — the closest analogue to Odyssey: true 3D, multi-storey, RimWorld-derived — handle world representation, camera slicing, cross-layer pathfinding, structural integrity, temperature by volume, and depth in the UI? And what do players praise and complain about regarding its verticality?

## Findings

### World representation

- Built in **Unity** with a **voxel-based 3D world**: uniform cubic cells, diggable terrain, buildable multi-level structures above and below ground (Wikipedia; Screen Rant interview). The developers call the voxel-based building mechanic their favourite system and say the voxel grid is what made building the game's main mechanic.
- The world is organised into **discrete z-levels**, but the camera steps through them in **0.5-level increments** — a half-step shows half-height walls of the current level (Slyther Games; Steam threads). The level indicator in the top-left shows a number such as "6.5".
- Digging bottoms out at bedrock ("at 3.0 you can't dig any deeper" — Slyther Games), so the playable vertical range is a few underground levels plus a soft build-height guideline of roughly eight storeys above ground (Fandom wiki, via search).
- The simulation is **CPU-bound, not GPU-bound** (dev blog MMT62). Notable optimisations the developers described in 2025: eliminating temporary garbage allocations, fixing event-listener leaks, and replacing a flood-fill perception scan with **edge/boundary detection** — "instead of checking everything within the area, we only look at the certain radius".

### Structural integrity

- **Integer stability model.** A structure placed on the ground has a stability value of **4**; each structure attached further from support has **−1**; nothing can be placed at stability **0** (Fandom wiki "Stability"). Support therefore radiates a small, fixed distance from grounded walls and pillars.
- **Beams extend support.** A beam placed in a straight unblocked line between two walls supports the floor above; players report beams span up to ~10 tiles, and a stable large room is four corner pillars plus beams along the long sides (Steam mining thread).
- **Mined (natural) ceilings follow the same idea**: unsupported spans of roughly 3–5 tiles from a wall are the practical limit; the community heuristic is "count 2 from each wall to be safe" and keep corridors ≤ 4 tiles wide (Steam mining thread).
- **Collapse is punitive and, underground, irreversible**: "if this is underground, you can never restore it, only plug it" (Steam mining thread). Whole buildings can cascade if under-supported.
- **Merged-load behaviour is not published anywhere we found**; players cannot see stability numbers in-game, so they respond by over-engineering — redundant pillars and beams well inside the theoretical limits.
- **Reliability matters as much as the rules**: a long-standing player report is that "sometimes reloading a save, some buildings have the beams vanish and floors collapse" — integrity state that changes across save/load destroys trust in the whole system.

### Cut-away camera and controls

- Level navigation: **up/down layer buttons with a numeric readout in the top-left**; keys (Z/X or PageUp/PageDown, rebindable); **Ctrl + mouse wheel** steps levels quickly in 0.5 increments; **Ctrl + click snaps the view to the clicked object's layer**; **C toggles roof visibility**; a separate "room view" overlay exists (Fandom "Camera Controls" via search; Slyther Games; Magic Game World).
- Free camera otherwise: WASD pan, wheel zoom, middle-mouse tilt/rotate, Num5 reset.
- The cut-away hides levels above the active one, **but not cleanly**: at half-levels the floor of the level above is drawn, and objects on other levels remain visible as **semi-transparent "shadows" that are still clickable**. There is no view that shows a whole room at full wall height without a sliver of the floor above (Steam threads, multiple).
- **Selection is not locked to the active layer**: the raycast happily selects hidden or background objects on other z-levels — the single most complained-about interaction in the game (see below).

### Cross-layer pathfinding

- Vertical movement is via **stairs** (need a run of tiles — three mined tiles in a line before a staircase fits), **ladders** (one cell) and **ramps**. Settlers and animals path freely across levels through them.
- Characteristic failures players report: settlers **deconstruct stairs/ladders from the middle and trap themselves** in pits; animals get stuck on ramps and stairs; settlers stall on staircases until a "starving settler" warning fires; job-position selection sometimes insists on one specific (unreachable or self-blocking) side of a build/deconstruct target (Steam bug threads).
- Shipped mitigations: an **automatic unstuck function teleports trapped settlers to the surface**; drafting a settler frees it manually. In late 2025 (dev blog MMT67) the developers reworked job ordering: settlers now **choose work positions that avoid creating enclosed regions and defer "blocking" jobs until no non-blocking ones remain** — explicitly credited to a RimWorld mod as inspiration — so that "they first destroy the foundation and then the walls just fall".
- Lesson embedded in that history: cross-layer pathfinding itself was never the hard part; **job placement interacting with a mutable 3D world** (dig/build orders that sever your own path) produced years of trap bugs.

### Temperature by volume

- Temperature is computed **per enclosed room**, not per cell. Nearly everything contributes heat: "humans, animals, piles, trees, even ground" (Steam cold-cellar guide).
- **Materials carry insulation values** (dirt walls 0.95, clay 0.75, wood flooring 0.75, limestone/clay tile 0.8, wicker grate 0.05); mixing materials stacks with diminishing returns; a single material contributes only up to a cap. Counter-intuitively, building walls underground is worse than leaving bare soil, because soil insulates best.
- **Volume matters**: "heat gets trapped in smaller rooms causing the room to stay warmer"; larger cellars run measurably colder (a 28-tile room held 3.7 °C).
- **Depth barely matters** since patch 1.1: one buried level 5.8 °C, three levels 5.5 °C — 0.3 °C total difference. Enclosure and materials dominate, not depth.
- **Doors gate heat flow**: solid door 7.3 °C vs grated door 9.0 °C in the same hall; an unsealed opening makes the room count as exterior. Well-built cellars hold ~2 °C through a 32 °C summer, below the ~1.5 °C food-spoilage threshold.

### How the UI communicates depth

Numeric layer indicator plus up/down buttons (top-left); semi-transparent ghosting of adjacent layers; roof-visibility toggle; room overlay; Ctrl+click layer snap. The ghosting doubles as the depth cue and as the game's biggest interaction flaw, because ghosted objects stay interactive.

## What players praise / complain about

### Complaints (ranked)

1. **Cross-layer selection bleed-through** — objects on other z-levels are visible as ghosts and remain clickable: "you can still click things on floor 2 and 3 while seeing the 'shadows' of floor 2"; "it's too easy to accidentally select something on a different z-level"; "there is no reason to see furniture of the second floor and being able to click it, while you are operating on the first floor". One player lost production buildings to misclick deconstruction. (Steam: "Raging on the floor management", "Building Z-levels: display makes it too hard".)
2. **The half-level display** — "when the camera setting in the top corner says 6.5, it should show half-height walls on level 6… and it shouldn't show stuff on level 7 at all!"; half-z-levels "show things from the floor above, making them extremely hard to use". (Steam: "Building Z-levels…".)
3. **No clean per-floor isolation view** — players want "a clean cut between the floors so you can get a good image of the room without the see through stuff from the floor above it"; "I can never see my entire builds without something being in the way"; for one player it "puts me off of an otherwise great game". Still unresolved as of March 2025. (Steam: "View between layers/floors/building levels?!".)
4. **Pathfinding traps** — settlers deconstructing their own escape route from the middle, animals and settlers stuck on stairs/ramps to the point of starvation warnings; needed an auto-teleport "unstuck" as a safety net. (Steam bug forum, experimental-branch forum.)
5. **Combat across floors** — "combat is clumsy, especially when everything starts happening across multiple floors, stairs, and narrow passages" (virus.hr, 1.0 review).
6. **Integrity opacity and reliability** — no in-game stability readout, so players over-engineer; beams vanishing on save reload can collapse underground rooms irreversibly. (Steam mining thread.)
7. **General camera/UI friction at scale** — "managing multiple vertical layers can become cumbersome, particularly in larger settlements"; the UI is "dense, layered, and somewhat cluttered, especially while you are still learning" (reviews).

### Praise (ranked)

1. **Verticality as a real mechanic, not decoration** — "its third dimension is a real mechanic": a cellar that actually cools food, a castle with floors, walls with tactical value (virus.hr). Reviews repeatedly note placement affects temperature, storage, defence and workflow at once.
2. **Diggable voxel terrain** — where similar games "trap you on a flat plane", this one "hands you a shovel and says 'dig'": carve hillsides, excavate storerooms, stack settlements skyward (1.0-era reviews).
3. **Being pushed to think vertically** — "the game constantly pushes you to think vertically: where will the storage go, what goes underground, where do the archers have best angle" (virus.hr).
4. **Expressive, gradual multi-storey building** — building "rewards not just optimization but also imagination"; watching a settlement evolve into a multi-floor stone fortress is called one of the most satisfying parts of the game (Output Lag / aggregate reviews).
5. **The developers agree** — the voxel building mechanic is their stated favourite; "verticality brings a completely new way to play and strategize" (Screen Rant interview). Reception: Metacritic 76, OpenCritic 83% recommended (Wikipedia).

The pattern is stark: **the fantasy of verticality is the game's most-praised feature and the interface to verticality is its most-complained-about flaw.**

## Design lessons for Odyssey

- **Cut-away slice: adopt, but harder than Going Medieval's** — render nothing above the active layer and, critically, **lock raycast/selection to the active layer**; their number-one complaint is ghosted objects staying clickable.
- **Half-level camera steps: avoid** — slice on whole layers only; Odyssey's roof-is-the-floor-above model makes half-steps meaningless anyway.
- **Layer snap and stepping: adopt** — Ctrl+wheel layer stepping, Ctrl+click "jump to this object's layer", and an always-visible numeric layer indicator are cheap and players rely on all three.
- **Adjacent-layer context: adapt** — show below-layer context and the slice plane; if anything above is shown at all, make it a non-interactive outline behind a toggle.
- **Integrity model: adapt** — the integer support-radius model (ground contact = N, −1 per step, beams/pillars extend it) is legible, cheap and proven; but **surface the numbers as a build-mode overlay** so players stop over-engineering blind, and give pre-existing ruined shells grandfathered support values so they stand until disturbed.
- **Collapse: adopt deterministic collapse, avoid silent irreversibility** — warn before placement makes something unstable, and never let save/load or nondeterminism change integrity state (their vanished-beam bug is the cautionary tale).
- **Job placement in a mutable world: adopt their late fix from day one** — work positions must avoid creating enclosed regions; blocking jobs (dig/deconstruct that severs paths) run last; deconstruction of stairs/ladders happens from the surviving side. Add a logged last-resort unstuck rather than letting pawns starve on a staircase.
- **Temperature: adopt per-enclosed-room with material insulation and a volume term** — it demonstrably creates loved gameplay (working cellars); keep depth a minor factor and make doors/openings the flow gates; treat "everything emits heat" with caution as it made their system hard to read.
- **Combat and LoS across floors: plan early** — multi-floor fights are where their verticality stops being fun; do not bolt 3D line of sight on late.

## Layer questions touched

- **Q1 (vertical movement / pathing model)** — Going Medieval: stairs need a multi-tile run, ladders one cell, ramps exist; traversal works, but job placement around mutable connectors is where the bugs lived.
- **Q2 (is a roof a floor; supports and collapse)** — integer stability radiating from ground contact, beams/pillars extend it, collapse is cascading and (underground) irreversible; rules are invisible in-game, which drives over-engineering.
- **Q3 (rooms across layers; heat)** — temperature is per enclosed room with material insulation and a room-size term; bigger rooms run cooler; depth is nearly irrelevant post-1.1; doors gate flow.
- **Q9 (camera slice and depth UI)** — 0.5-step slicing with ghosted, still-interactive other layers is the game's biggest failure; numeric indicator, layer snap and roof toggle are its successes.
- Touched in passing: **Q5** (combat across floors is their acknowledged weak point) and **Q10** (temperature is room-granular, not cell-granular — a workable stub level for a vertical slice).

## Sources

- https://en.wikipedia.org/wiki/Going_Medieval
- https://screenrant.com/foxy-voxel-interview-going-medieval-early-access/
- https://foxyvoxel.io/2025/06/02/mmt62/
- https://foxyvoxel.io/2025/10/06/mmt67/
- https://goingmedieval.fandom.com/wiki/Stability
- https://goingmedieval.fandom.com/wiki/Camera_Controls
- https://www.slythergames.com/2021/06/04/going-medieval-how-to-build-underground/
- https://www.magicgameworld.com/going-medieval-pc-keyboard-controls-and-shortcuts/
- https://steamcommunity.com/sharedfiles/filedetails/?id=2992452919
- https://steamcommunity.com/app/1029780/discussions/0/3055111535922874430/
- https://steamcommunity.com/app/1029780/discussions/0/3092263995638507724/
- https://steamcommunity.com/app/1029780/discussions/0/4361250086034818336/
- https://steamcommunity.com/app/1029780/discussions/0/5296777170372992327/
- https://steamcommunity.com/app/1029780/discussions/0/3055111535922602464/
- https://steamcommunity.com/app/1029780/discussions/1/4032476115584296431/
- https://steamcommunity.com/app/1029780/discussions/3/5875531492222021528/
- https://virus.hr/en/reviews/going-medieval-rimworld-in-3d-but-finally-distinct-enough
- https://outputlag.com/game-reviews/going-medieval-review/
- https://www.metacritic.com/game/going-medieval/

## Confidence

**Medium-high overall.** High for the camera controls, cut-away complaints, pathfinding failure modes and temperature behaviour: these come from many independent player threads, a hands-on guide with measured numbers, and post-1.0 reviews that agree with each other. Medium for the integrity numbers (stability 4/−1, beam span ~10, safe span 3–5): sourced from the community wiki via search snippets and player experiments, not developer statements, and the fandom wiki itself could not be fetched directly (HTTP 402). Low for engine internals: the developers' public blogs discuss optimisation, not world-representation architecture.

## Could not be determined

- The exact internal voxel/chunk data structure, chunk size, and number of z-levels per map (developer blogs cover optimisation, not architecture).
- Precise merged-load rules: how stability combines when a cell is supported from several directions, and whether load is per-structure mass or purely the distance-decay value.
- The exact pathfinding algorithm (A* variant, hierarchical or not) and its cross-layer cost model.
- The formal temperature formula (the insulation values and volume effect are player-measured, not developer-published).
- Whether 1.0 (March 2026) shipped any fix for cross-layer selection bleed-through; complaint threads run to at least March 2025 with no documented developer response.
