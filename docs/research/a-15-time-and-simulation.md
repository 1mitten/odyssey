# Lane A15 — Time and simulation: ticks, speeds, tick groups, determinism, save

## Question

How does RimWorld's time and simulation model work — ticks per second/hour/day, game speeds and their tick multipliers, tick groups (every tick / rare tick / long tick) and how things are bucketed into them, determinism (or its absence) and what breaks it, and how a mid-tick world is saved and loaded?

## Findings

### Tick constants

The tick is the sole unit of simulation time. All durations in the game are stored in ticks; seconds shown in the UI are a display convention assuming normal speed.

| Quantity | Ticks | Real time at speed 1 |
|---|---:|---|
| 1 tick | 1 | 1/60 s |
| 1 in-game "second" | 60 | 1 s |
| 1 in-game hour | 2,500 | ~41.67 s |
| 1 in-game day (24 h) | 60,000 | 16 min 40 s |
| 1 quadrum (15 days) | 900,000 | 4 h 10 min |
| 1 year (4 quadrums, 60 days) | 3,600,000 | 16 h 40 s |
| 1 rare tick | 250 | ~4.17 s |
| 1 long tick | 2,000 | ~33.33 s |

The calendar: 4 quadrums of 15 days each; seasons map onto quadrums by hemisphere; time of day is longitude-dependent on the world map (two settlements at different longitudes have different local hours at the same tick). One unit of "work" (construction, crafting) is approximately 60 ticks of labour; UI work values are rounded for display but the engine uses raw ticks.

### Game speeds

Speeds are target tick-rate multipliers, not frame-rate changes:

| Speed | Multiplier | Target ticks per real second |
|---|---:|---:|
| Paused | ×0 | 0 |
| 1 (Normal) | ×1 | 60 |
| 2 (Fast) | ×3 | 180 |
| 3 (Superfast) | ×6 | 360 |
| 4 (Ultrafast, dev mode only) | ×15 | 900 |

Key design facts, from the wiki's own wording:

- The speeds are **targets**. The tick loop runs inside the engine's per-frame update and executes as many ticks as the frame budget allows; on a slow machine (or a heavy late-game colony) the game simply falls below target — game time slows relative to real time. There is no catch-up debt and no death spiral; rendering and UI keep running every frame regardless of how many ticks fit.
- The community habit of calling speeds "2x/3x" is wrong; the real multipliers are ×3 and ×6. Ultrafast (×15) is deliberately dev-only.
- Multiple community sources report the target rates are **doubled when every player-controlled pawn on the map is asleep** (a quality-of-life acceleration in recent versions). Reported consistently but not confirmed on a primary page — treat as medium confidence.
- Pausing zeroes the multiplier but the frame loop, UI and camera all still run: simulation update and presentation update are fully separated. Anything smooth (pawn movement between cells) is presentation-side interpolation over discrete cell positions.

### Tick groups and bucketing

Every thing declares a ticker type: **Never, Normal, Rare, or Long**. The tick manager keeps one tick list per cadence:

- **Normal** — callback every tick (pawns, fires, projectiles, anything with per-tick behaviour).
- **Rare** — callback every 250 ticks (~4 s of game time): plants and other slow-changing things; typical uses are growth, deterioration and periodic environmental checks.
- **Long** — callback every 2,000 ticks (~33 s): the slowest housekeeping.
- **Never** — most buildings and inert items do not tick at all. This is the single biggest scalability lever: cost scales with *ticking things*, not things, and certainly not cells.

Load-balancing: the well-documented modding idiom is the **hash interval** — a thing's periodic work fires when (current tick + a per-thing hash offset) modulo the interval is zero, so ten thousand plants do not all do their rare work on the same tick; each instance is phase-shifted by a hash of its identity, spreading the population evenly so roughly 1/250 of rare tickers (1/2,000 of long tickers) run on any given tick. The same pattern is used *inside* Normal ticks for sub-cadences (for example, an awake pawn's rest need falls in a check every 150 ticks). Modding documentation warns that even with hash offsets, hundreds of instances doing real work per interval degrade performance — the mitigation is coarser cadence or batching, not more offsetting.

Besides per-thing tickers, per-map, per-world and per-game components receive a tick callback every tick, which is where grid- and map-scale systems live. Temperature is computed per room/region, not per cell — a precedent for choosing coarse simulation units.

### Determinism

- **The simulation RNG is reseeded every tick** from the world seed combined with the current tick number. Given identical state and identical inputs, a tick is reproducible. World generation from a seed is famously reproducible.
- **But vanilla RimWorld is not lockstep-deterministic in practice**, because the same global RNG and mutable state are freely touched from non-simulation code. The Multiplayer mod's public documentation is the best map of the hazards it had to patch:
  - **Interface/simulation bleed** — UI code (tooltips, selectors, menu handlers) calling the RNG or mutating game state; the mod separates "in interface" from "ticking" code paths, wraps risky sections in an RNG push/pop pattern, and routes every state-changing UI action through synchronised commands scheduled onto a specific tick.
  - **Unseeded RNG** — anything using the platform RNG rather than the seeded game RNG in tick-level code.
  - **Unordered collections** — dictionary/hash-set iteration order diverging after different insert/delete histories; fix is ordering on stable keys wherever iteration affects outcomes.
  - **Real time in simulation** — wall-clock timers and time-based caches firing on one client and not another; fix is tick-based timing only.
  - **Asynchronous work** — async results injected into game state at nondeterministic times.
  - **Conditional branches on local state** — code that behaves differently depending on which map is being viewed or what is selected.
  - **Save/load asymmetry** — serialisation that does not round-trip, or transient state rebuilt nondeterministically after load, makes a freshly-loaded client diverge from a long-running one.
  - **Floating point** — acknowledged as a subtle hazard with no general fix (in practice same-binary/same-platform is stable).
- **Desync detection** — each client hashes the RNG state (world, per-map, command) after every tick and exchanges compact "sync opinions"; any mismatch flags a desync and dumps a trace. An "arbiter" (a headless spectator instance) breaks ties about which client diverged. This per-tick state-hash technique is directly reusable as a single-player determinism regression test.

### Save/load

- The entire game state serialises to a single XML tree (`.rws` file) through one virtual expose-data method implemented across the object graph; the same method runs in save and load, so every field is declared once. Loading runs in distinct passes: write (saving), read raw values, **resolve cross-references**, then **post-load initialisation**. Spawned objects are saved once and referenced elsewhere by unique load-IDs; the resolve pass rewires the pointers.
- Save structure (from the wiki's documented 1.2-era skeleton): metadata (version, mod list, order); game-level singletons (**tick manager — including the current tick count** — storyteller state, research, quests, history); the world (factions, world pawns, world grid, components); then per-map: reservation managers, designations, AI group ("lord") manager, zones, areas, roof/terrain/fog/snow grids, temperature cache, weather state and its decider, wild plant spawner, and finally the things list. Homogeneous natural fill (rock, etc.) is stored via a compressed grid rather than per-thing records.
- **Mid-job state persists.** A pawn's job tracker deep-saves the current job, the current job driver (which records how far through its toil sequence it is and its progress counters), the queued jobs and the pawn's posture. On load the driver resumes where it left off. Post-load initialisation validates the combination — a pawn with a job but no driver has the job ended with an error and cleaned up, which is also what protects saves against mods that changed a job's toil sequence. Reservations (which pawn has claimed which thing/cell) are saved at map level, so claims survive the round trip.
- **Saves are between-tick, not mid-tick.** Saving is triggered from the UI layer and executed as a long event outside the tick, so the snapshot is always at a tick boundary; "mid-tick world" really means "mid-job, mid-path, mid-fire world at a tick boundary". Pawn positions are integer cells; the smooth between-cell motion is presentation-only and is simply not part of the save.

### What this recommends for Odyssey

1. Copy the constants where they are proven: 60 ticks/s at ×1; ×3 and ×6 as the player speeds (our performance target "60 FPS at 3× speed" should be read as the ×6 multiplier, 360 ticks/s); 2,500 ticks/hour and 60,000/day are good defaults until our day length is designed.
2. Adopt the four-cadence ticker (Never/Normal/Rare/Long) with hash-offset phase spreading from the first commit, plus per-system sub-cadences inside Normal. Make Never the default ticker type.
3. Be deterministic on purpose, not by accident: seed the RNG per tick from (world seed, tick index), give each system its own named RNG stream, ban wall-clock time and unordered iteration in the Sim assembly, and keep UI strictly read-only against Sim state (mutations only via commands applied at tick boundaries). Add the Multiplayer mod's oracle as a test: run N ticks, hash state, save, load, run N more, compare against an unbroken run.
4. Save at tick boundaries via a single expose-style pass with load-ID reference resolution, and persist mid-job state (job + driver progress + queue + reservations) from the first job system commit — retrofitting it is what hurts.

## 3D/layer impact

Does 40× the cell count change the bucketing strategy? **No for things, yes for grids.**

- Tick-list cost scales with the number of *ticking things*, which is set by colony content (pawns, fires, machines, plants), not by map volume. 250×250×40 ≈ 2.5M cells versus RimWorld's 62.5K is irrelevant to the tick lists so long as empty cells and inert ruins cost nothing (ticker type Never).
- Per-map grids are the risk: RimWorld's per-cell byte grids (roof, snow, fog, terrain) are cheap at 62.5K cells; at 2.5M cells a byte grid is still only ~2.5 MB, so *storage* is fine, but any O(cells) per-tick sweep is banned. Grid systems must be event-driven or chunked (per layer, or per layer-region), with chunks phase-spread by hash exactly as things are.
- Bucketing can gain a layer dimension: hashing on (thing id, layer) keeps one layer's rare tickers from clumping onto the same phase after template stamping creates thousands of near-identical things at once. (Stamps assign consecutive IDs; a pure ID hash may still spread them adequately, but it is worth checking in the slice.)
- The between-tick save model is untouched by 3D; the compressed-homogeneous-grid trick becomes more valuable, not less (see below).

## Ruined-city impact

- A ruined city is mostly **inert mass**: building shells, rubble, dead infrastructure. The RimWorld lesson is that none of it should tick — ruins are ticker-type Never, with decay and collapse driven by events (damage, support removal) or at most Long-tick checks on the small "exposed/unstable" subset.
- Template stamping will create large numbers of identical things at generation time. RimWorld's save format compresses homogeneous natural fill into a grid rather than per-thing records; we should plan the same for uniform rubble/concrete fill or saves of a 40-layer city will balloon.
- The forced-slowdown/speed-cap behaviours around threats are storyteller-adjacent polish, not engine requirements; note them for Lane A11 rather than building them into the tick manager.

## Layer questions touched

- **Q10 (unit of simulation for gas, fire, water, sound):** RimWorld's precedent is *never the cell-per-tick*. Fire is a per-thing entity (attached to a flammable thing or cell) on the Normal ticker with hash-spread spread/consume checks; gas/smoke is a map grid updated at a fixed coarse cadence; temperature is per room/region, not per cell; sound is not simulated at all (instant radius/line checks at the moment of the event). For Odyssey the implied answer: fire = entity, gas and temperature = per-room/region *per layer* with vertical coupling as edges in the region graph (heat and gas rise through openings between layer-regions, a graph propagation, not a cell sweep), water = coarse cell-column or region volume at Rare cadence if simulated at all, sound = event-time queries. A per-cell fluid tick over 2.5M cells is off the table at 360 ticks/s.
- **Q12 (what the slice must prove / can stub):** the slice must prove (a) the tick-group scheduler with hash spreading holds 60 FPS at the ×6 multiplier with representative load — 50 colonists + 300 animals on Normal, several thousand Rare/Long tickers, everything else Never; (b) determinism: N ticks → save → load → N ticks produces a state hash identical to 2N unbroken ticks, using per-tick RNG/state hashing as the oracle; (c) mid-job persistence: a pawn saved mid-haul resumes correctly. It can stub: gas/water simulation, the sleep speed-up, forced slowdowns, and any multi-map/world-clock concerns (single map suffices).

## Sources

- https://rimworldwiki.com/wiki/Time
- https://rimworldwiki.com/wiki/Save_file
- https://rimworldwiki.com/wiki/Rest
- https://rimworldwiki.com/wiki/Template:Ticks/seconds/doc
- https://rimworldwiki.com/wiki/Modding_Tutorials/ThingComp
- https://github.com/UnlimitedHugs/RimworldHugsLib/wiki/Custom-Tick-Scheduling
- https://github.com/roxxploxx/RimWorldModGuide/wiki/Key-Points-of-Understanding
- https://github.com/Zetrith/Multiplayer/wiki/Desyncs
- https://deepwiki.com/rwmt/Multiplayer/7-determinism-and-desyncs
- https://github.com/Dev-Jahn/rimworld-modding-reference/blob/main/09-multiplayer-compatibility/debugging-desyncs.md
- https://spdskatr.github.io/RWModdingResources/saving-guide.html

## Confidence

**High** for the tick constants, speed multipliers, rare/long intervals, calendar, and the save-file structure — all read directly from the RimWorld wiki (primary source). **Medium** for the internals: hash-offset spreading and the three tick lists are confirmed by modding documentation, but the exact bucket-assignment mechanism inside the tick manager was not verified against a primary source (deliberately — the clean-room rule forbids reading decompiled game code). **Medium** for the determinism map: the per-tick reseed and the hazard list come from the Multiplayer mod's ecosystem documentation (its own wiki is thin; the detail comes from secondary syntheses of an open-source mod, which is design documentation rather than game code). **Medium** for the sleep-time doubling (multiply reported, never found on a primary page).

## Could not be determined

- The exact internal mechanics of tick-list bucket assignment (registration order vs ID hash) and whether a thing's rare/long phase is stable across save/load — knowable only from game source, which the clean-room rule excludes. For Odyssey this is moot: we define our own (persist the phase offset).
- Primary confirmation and version of the "double tick rate while all colonists sleep" behaviour.
- Whether the Multiplayer mod achieves cross-platform (Windows/Linux/macOS) float determinism or only same-platform.
- The precise field list a job driver serialises (toil index, per-toil tick counters) — the shape is confirmed (driver deep-saved with progress, resumes after load, errors out if invalid), the field names are not, and we do not need them.
- Exact autosave interval options and whether permadeath mode changes save timing.
