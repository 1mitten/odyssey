# B — Stonehearth and ACE

## Question

OQ-33, Lane B. Stonehearth was a voxel, multi-level colony sim that struggled badly with
performance and AI, was abandoned by its studio in 2018, and was then taken up by the community mod
ACE (Authorized Community Expansion). It is the nearest thing to a controlled experiment in the
failure mode Odyssey is trying to avoid. What exactly was its world representation, why did
performance and AI fail, what did ACE actually fix, and which of those failures are we structurally
immune to?

A note on evidence before the findings. Stonehearth's engine was never open-sourced; only the Lua
gameplay layer and the modding documentation are public. That means almost every public claim about
*why* the game was slow is either (a) a developer remark on a forum, (b) a reading of the public Lua
and the in-game profiler, or (c) fan speculation. This file marks which is which in every paragraph,
because the lore around this game is unusually polluted — one of the top search results for
"Stonehearth pathfinding optimisation" is a post the community publicly flagged as fabricated
link-bait (see Sources), and its invented claims are now being restated by search engines as fact.

## Findings

### 1. World representation

**Engine shape.** The core is C++11 with OpenGL, rendering through Horde3D with custom node types
and extensions. *All* gameplay and AI are Lua. The interface is HTML and JavaScript in an embedded
browser. It is an entity-component system in which a set of components live in C++ (position/mob,
collision region, destination, and so on) and are exposed to Lua, while everything behavioural is a
Lua component or controller. This split is the single most important architectural fact about the
game: the *data* is native and the *decisions* are scripted, and the boundary between them is
crossed constantly.

**Two voxel scales, not one.** Terrain and collision work in whole blocks; a hearthling occupies a
little more than one block. Art is authored ten times finer — a model block is 10 x 10 x 10 model
voxels to one terrain block. So the thing the simulation reasons about (a block) and the thing the
player sees carved and coloured (a model voxel) are different units. Odyssey has made the same
separation deliberately (2.5 x 2.5 x 3.0 m cells with drawing-only offsets), and Stonehearth is
evidence it holds up: no public complaint traces to that decision.

**Terrain generation is Lua-driven but C++-executed.** The world generation service is Lua; the
modding guide states plainly that the actual work happens on the C++ side. The overview map is
divided into tiles, and shaped noise assigns each 8 x 8 square of the overview map a terrain class
(plains, foothills, mountains). Terrain is fully destructible and the world is mutable at runtime —
players mine it, terrace it and build into it.

**Multi-level building.** Every voxel the player places is classified as a floor, a wall or a roof,
and buildings are multi-storey. The builder generates scaffolding and ladders as *real entities*
during construction, which are then removed. The builder is permissive by design: the modding
guidance concedes it "doesn't forbid doing things that will display badly". Ladders, which are the
vertical connectors, had their own long-lived class of bug — placement landing one voxel into the
surface below, for instance. Vertical connectors are fence-post problems.

**One more structural fact.** Even in single player the game runs as two processes, a client and a
server. Radiant's community manager confirmed on the forum that this is the client/server model
built for the multiplayer work, retained in solo play. The whole simulation nonetheless runs on one
CPU thread.

### 2. Why performance struggled

Ordered by how well the public record supports the claim.

**(a) One thread, and the team's own escape hatch was never built.** *Developer-sourced.* A Radiant
engineer (Chris) stated on the forum that "the bulk of performance cost is the AI which is
extremely, extremely difficult to multithread because it's hard to synchronise everything
consistently", and that multithreading AI was "a last resort if they can't get the game optimised
enough". The roadmap carried a "multithread pathfinding" task. Neither landed. This is the most
important finding in the whole file and it is not a performance fact — it is a *planning* fact. The
team believed a thread-based rescue was available later, so the per-entity cost model was never
forced to become cheap. The rescue was never affordable, because the simulation is one mutable
world in which everything can interact.

**(b) Item-driven cache invalidation — the largest single measured cost.** *Community-measured, with
numbers.* A 2026 third-party optimisation mod for ACE profiled a 23-hearthling town with the game's
own profiler and found the category `filter_cache_cb` consuming 15–18% of the per-tick budget. The
mechanism it names: Stonehearth finds items by running Lua filter predicates against a C++-side
filter cache, and that cache is invalidated *every time an item moves*, cascading across the
storage/filter pairs that could have matched. Cost therefore scales with **item movements x filters
x containers**, not with hearthling count. The mod's fixes are all cache discipline — remember
negative results per player and item type, de-duplicate the same entity within one tick, throttle a
storage/item pair to one invalidation per tick, coalesce "contents changed" to once a second instead
of once per item, and stop allocating a fresh table per entity. After: `filter_cache_cb` fell to
0.0–0.6%, and available CPU rose from 30–35% to about 42%. That is roughly a ten-percentage-point
recovery from one cache, eight years after release, from outside the engine.

**(c) Lua garbage collection.** *Community-measured; magnitude disputed.* Players consistently
report Lua pegging 85–90% of the profiler's budget at 17–20 hearthlings, and the garbage collector
being a large fraction of it — forum claims of 20–50% of frame time are common but are player
readings, not developer figures. The optimisation mod's own measurement is more sober and more
credible: `lua_gc` around 22%, and its adaptive incremental-GC change moved it only to about 21%.
So GC is real, is large, and is *not* where the recoverable win was. Radiant published Lua profiling
scripts publicly, which tells you they expected Lua to be the thing you profile.

**(d) Pathfinding.** *Developer-sourced in outline, thin in detail.* An Alpha 11 developer post
described the pathfinder as A* over a navigation grid, stepping voxel by voxel toward the
destination under a heuristic — "really fast" per step, with the stated downside that it "may take
many steps to find a path when the geometry of the world gets complex". The stated intent was to
move to a navigation **mesh** while keeping A*, so that a step is an abstract region rather than a
voxel. Alpha 15 notes mention navgrid and sensor optimisations. I could not confirm that the
navmesh ever shipped. Note the direction of causation here: *complex geometry* is the trigger, and
a player-built multi-level colony with ladders and narrow corridors is the most complex geometry the
game ever sees. The pathfinder got worse exactly as the player succeeded.

**(e) Memory.** *Player-reported, unverified.* 2 GB and upward is routine; reports of 8–14 GB past
30 hearthlings exist; restarting the game restores speed for ten to fifteen minutes. A leak is the
obvious reading, but no developer statement confirms one and the symptom is equally consistent with
GC pressure on a growing live set.

**(f) Rendering and terrain topology.** *Partially confirmed by ACE's changelog.* The profiler
carries a `topology` category distinct from `pathfinder` and `renderer` — the cost of keeping
navigation and collision topology current as the world is edited. ACE's changelog records fixing
"lag spikes in the topology category" and improving performance "when building large structures with
complicated colour patterns". So world mutation had its own budget line, and large or visually
complex buildings were the thing that blew it. Rendering is conspicuously absent from the
complaints; this was never a GPU problem.

### 3. Why the AI struggled

**The architecture.** *Documented.* Hearthlings run a hierarchical task network. An *activity* is an
abstract verb string; several *actions* compete to implement it. Actions are leaf (a Lua `run`),
compound (a chain of sub-activities with placeholder argument passing), or task-group. Every
implementing action "thinks" asynchronously on a coroutine and signals readiness with a think
output; whichever ready action has the highest utility (0.0 to 1.0) wins, and a running action gets
a sunk-cost boost (default +0.05) so it is not interrupted for trivia. Selection is local to each
activity, which keeps comparison cheap. Task groups — personal ("solo" ones for eating and
sleeping), town-wide ones for building and farming, ad hoc ones for combat parties — are the only
place different activities compete, and they remap utility ranges so that, for instance, healing at
0.87 outranks crafting at 0.81.

This is a good design. It is not a naive design. That is the point.

**Where it broke.** Three mechanisms, in descending order of confidence.

*Fan-out on task creation.* When a system creates a task, it is dispatched to the AI of **every**
member of the group. Every member then begins thinking about it. With N hearthlings and M pending
tasks, thinking is O(N x M) before anything is done.

*Thinking requires searching.* An action cannot report its utility without knowing whether its
target exists and is reachable — which means the think phase performs item queries and pathfinder
queries. So the fan-out above is not cheap thought; it is the multiplier on the two most expensive
subsystems in the game. This is the join that killed Stonehearth: **deciding costs what searching
costs, and deciding is done by everyone about everything.** Radiant's own mitigation is visible in
the record — the community manager explained that items are marked "busy" before hauling starts,
preventing redundant pathfinding checks among hearthlings, which is a reservation bolted on to
suppress a search storm that the design invited.

*Degradation is silent and looks like stupidity.* Radiant's own framing is that hearthlings are
programmed to do simple things when they cannot work out how to do complex things. Under CPU
starvation the thinking does not complete, so the fallback wins, and the player sees colonists going
idle beside available work, restocking in an endless loop, or standing about in conversation.
Players spent years reporting these as AI bugs. A share of them were the performance failure wearing
an AI costume. Forum threads asking for the AI to be "improved" and threads reporting pathfinder CPU
spikes are, in many cases, the same defect reported from two ends.

### 4. What ACE changed

**What ACE is.** A community expansion explicitly authorised by Team Radiant to continue development
*through an expansion* — that is, a mod. The repository is Lua, JSON and assets, with a
`monkey_patches` directory that overrides base-game Lua files. No public evidence of any C++ or
engine access exists, and the shape of the repository is strong circumstantial evidence there is
none. ACE's stated pillars are content, unmet promises and user experience; performance is not one
of them.

**What it fixed.**

- *Diagnosis.* ACE rebuilt the in-game CPU bar into a real-time graph with per-category filtering.
  It is worth sitting with the fact that the most durable performance contribution the community
  made in years was **a better profiler**.
- *Topology spikes* during construction, and construction of large buildings with complex colour
  patterns.
- *Restocking* — storage priority handling, what counts as restockable, and a "no restock" tag so
  that objects never meant to be shuffled stop generating haul tasks. This is task-supply reduction,
  and it attacks the fan-out from the only end Lua can reach.
- *Pathfinder tuning, not pathfinder algorithms* — for example reducing a pasture's pathfinder cost
  modifier so hearthlings would cross it rather than route around it.
- Mod load time, and data-shape changes for moddability (crafting moved from work units to effort;
  fields to arrays).

**What only fell later, and from outside ACE.** The filter-cache cascade in (2b) was still costing
15–18% of the tick in 2026 and was fixed by a separate third-party mod, in Lua, by caching negative
results and throttling invalidations. It was reachable all along. Nobody had measured it.

**What proved unfixable without an engine rewrite.** Single-threading. The pathfinder's algorithm
and its navgrid. The Lua garbage collector beyond marginal tuning (about one percentage point).
Long-session memory growth. The client/server process split in single player. None of these have a
Lua-level answer, and the engine is closed.

### 5. The transferable lesson for us

**Structurally immune.**

- *No Lua, no LuaJIT GC.* Their second-largest category simply does not exist for us, and our
  structure-of-arrays choice (ADR 0005) is the reason: the allocation pattern that fed their
  collector is the one we have already designed out.
- *No thread rescue to defer to.* Radiant kept an expensive per-entity cost model because they
  believed threads would eventually absolve it. Determinism-before-threads removes that belief. We
  cannot postpone the algorithmic fix, because there is no other fix. This constraint is an asset
  and should be stated as one.
- *The tick is explicit and budgeted.* Their degradation was invisible: work simply did not finish
  and colonists looked stupid. A fixed-tick simulation with a named budget per subsystem can say
  "the pathfinder overran" instead of shipping a colonist standing in a field.
- *Content is data, loaded once.* Their per-entity behaviour was script evaluated at runtime.

**Still exposed — and one of these is already our top recorded risk.**

- *Deciding costs what searching costs.* This is Stonehearth's fatal join and we have the same
  shape. A work giver that answers "is there work for this pawn?" by finding a target and pathing to
  it makes job selection cost a pathfind. We already measure 65% of the tick in A*. Stonehearth is
  the proof of where that ends.
- *Fan-out on offer.* Their task creation dispatched to every group member. If every idle pawn
  scans every giver against every designation, we are O(pawns x designations) per tick with a
  pathfind inside the inner loop. OQ-44's self-registering givers made adding givers cheap; it did
  nothing about evaluating them.
- *Invalidation on move.* Their single largest recoverable cost was a cache invalidated whenever an
  item moved. We do not have that cache yet. We will build it the day we want "nearest hauled item
  matching this filter", and we will build it because it is obviously the right optimisation.
  **Design its invalidation before its lookup.**
- *Vertical geometry degrades A-star specifically.* Their own admission is that step counts explode
  when geometry is complex, and ladders and narrow columns are the complex case. Our hop, stair and
  ladder graph has exactly that property: a Euclidean or Manhattan heuristic under-estimates badly
  when the only route is a vertical connector several cells sideways, and the search fans out over
  the whole floor before finding it. This is not a general A-star problem; it is a **verticality**
  tax, and we will pay more of it than a flat game would.
- *Mutation cost as its own budget line.* Their profiler needed a `topology` category separate from
  `pathfinder`. We continuously edit the world too — mining, felling, building — and we do not
  currently measure what a world edit costs the nav graph, only what it costs `ChunkMesher`.

## Recommendation

Five positions, each backed.

**1. Land the district-id reachability check before any further work givers.** This is the exact fix
Radiant identified (abstract regions instead of voxel stepping) and never shipped, and it is already
queued as `d-04-pathfinding.md`. Every additional giver that answers its question with an A-star
call increases the cost of the thing we know is 65% of the tick. Do not add the next feature first.

> **Editor's note, 2026-09-17.** That 65% is the D1 stress harness, not the running game. `OQ-19`
> measured the real tick the same day this file was written: a colony of fifty spends 37% of a
> 0.025 ms tick in the whole pawn phase, reaching 97% only under a deliberately applied replan rate.
> The recommendation is unaffected and arguably strengthened — the cost is small *now*, which is
> precisely when a structural boundary is cheap to impose. See `docs/adr/0005-simulation-architecture.md`,
> addendum 2026-09-17.

**2. Make it structurally impossible for a work giver to call full A-star.** Two tiers: a giver may
ask "reachable?" and "roughly how far?" and nothing else; an exact path is computed once, *after*
the job is assigned. Enforce it with a type boundary — the giver interface should not be handed
anything that can path. Radiant's "busy" flag was the same idea arriving too late and by hand.

**3. Report budget overruns instead of degrading quietly.** Stonehearth's defining failure was that
CPU starvation presented as AI stupidity, and it was misdiagnosed as an AI bug by players and, to a
degree, by the team, for years. If a subsystem exceeds its slice of the tick, the simulation should
say so — a counter, a log line, a test that fails. We are better placed to do this than they were
because our tick is fixed and our budget is explicit; the only thing needed is the will to make it
loud.

**4. Benchmark against item count, not pawn count.** The largest measured cost in Stonehearth scaled
with item movements and containers, not with hearthlings. Every benchmark we have scales pawns and
map size. Add one that pins pawns at a dozen and scales loose items into the thousands. We will
learn whether we have their disease before we have their symptoms.

**5. Keep a per-category cost readout in the running game.** ACE's most useful performance work in
years was making the profiler legible. Ours exists as tests; it should also exist where the owner
can see it while playing, with the same category discipline (pathfinder, world mutation, job
selection, rendering, other).

**The early warning sign to watch, named precisely.** Not frame time — frame time is a lagging
indicator and it is what everyone watched while Stonehearth died. Watch **pathfinder calls per job
assigned**. It is a pure number, cheap to count, and it is the ratio that encodes the fatal join: if
deciding costs searching, this number is large; if it climbs as pawn count climbs, the fan-out is
superlinear and we are on Stonehearth's curve. Two supporting counters: **idle pawns while
unclaimed designations exist** (their public symptom, and the one that got misfiled as an AI bug for
years), and **cache invalidations per world edit**, the day we have a cache.

The tie-breaking observation, if one is wanted: our recorded pathfinding explanation was already
falsified once by its own follow-up experiment. Calls-per-assignment is the metric that would have
caught that mistake immediately, because it does not depend on any theory of *why* the searches are
expensive — only on how many of them we are doing per unit of work achieved.

## Layer questions touched

What multi-level building cost Stonehearth, and what that implies for our layered world.

- **A separate profiler category exists for topology.** Keeping navigation and collision topology
  current under continuous player edits was expensive enough to need its own budget line, distinct
  from pathfinding and rendering. ACE's changelog records fixing lag spikes in it during
  construction. *We should know what a world edit costs our nav graph, and we currently do not.*
- **Scaffolding and ladders are real entities during construction.** Building load therefore scales
  with the surface area of the structure, not just its footprint, and disappears when the build
  completes. Our building line (`docs/design/15-building.md`) should decide early whether
  construction-time helpers are simulated or drawn. Presentation-only is the cheaper answer and
  matches our standing rule that nothing in presentation is in a cell, a save or the hash.
- **Vertical connectors are where both the search and the geometry go wrong.** Ladders made the
  voxel-stepping search bad (complex geometry, many steps), and ladder placement carried long-lived
  off-by-one bugs against the surface below. Our hop, drop, stair and ladder costs must agree across
  three seams already; the transferable point is that connectors are simultaneously the pathfinder's
  worst case *and* the geometry's most bug-prone case, so they deserve disproportionate test
  coverage.
- **Multi-storey building complexity fed the renderer too.** Large structures with complicated colour
  patterns were a named performance fix. Our equivalent is instanced chunk meshing under a building
  that spans layers; worth a contact-sheet-and-timing check before the building line grows.
- **A permissive builder is a design choice with a cost.** Stonehearth chose to let players build
  things that display badly rather than constrain them. That is defensible, but it pushed the
  failure into rendering and topology. We should make that choice consciously rather than inherit
  it.

## Sources

Marked **[read]** where I fetched and read the page, **[search]** where the claim comes from a search
engine's summary of the page and I could not open the primary text within the cap.

1. https://stonehearth.github.io/modding_guide/modding_guide/advanced/ai/index.html — **[read]** the
   official modding guide's AI chapter: hierarchical task network, activities and actions, leaf /
   compound / task-group actions, utility 0.0–1.0, the +0.05 sunk-cost boost, task-group dispatch to
   every member. The primary source for section 3.
2. https://discourse.stonehearth.net/t/game-performance/33469 — **[read]** contains the one clear
   developer statement on the record: a Radiant engineer saying the bulk of the cost is AI and that
   multithreading it is extremely difficult, with threading held as a last resort.
3. https://discourse.stonehearth.net/t/lua-performance/34536 — **[read]** the 17–20 hearthling
   cliff, Lua at 85–90%, memory reports, and Radiant's community manager confirming the client/server
   two-process split and the "busy" item flag that suppresses redundant pathfinder checks.
4. https://github.com/fatsan1975/stonehearth_performance_mod — **[read]** third-party optimisation
   mod's README: names the C++ filter cache invalidated on every item move as the top cost, lists six
   Lua-level mitigations, and gives before/after profiler figures on a 23-hearthling town
   (`filter_cache_cb` 15–18% to 0.0–0.6%; `lua_gc` ~22% to ~21%; free CPU 30–35% to ~42%).
5. https://discourse.stonehearth.net/t/ace-authorized-community-expansion-project/36671 — **[read]**
   ACE's opening post: authorised by Team Radiant, scope is content and user experience, no
   performance or pathfinding commitments made.
6. https://en.wikipedia.org/wiki/Stonehearth — **[read]** history: Kickstarter 2013 ($751,920 from
   22,844 backers), Early Access June 2015, 1.0 July 2018, development ended 2018 with Kickstarter
   features unimplemented, Linux port cancelled.
7. https://discourse.stonehearth.net/t/how-to-optimize-pathfinding-for-large-populations-in-stonehearth-mods/44230
   — **[read]** a *negative* source, cited as a caution: a plausible-looking technical post about
   Stonehearth pathfinding that the community publicly flagged as fabricated link-bait. Its invented
   claims are now being echoed by search engines. Do not cite Stonehearth pathfinding "facts" from
   secondary summaries.
8. https://github.com/StonehearthACE-team/stonehearth_ace — **[read]** ACE's repository: Lua, JSON
   and assets with a `monkey_patches` directory; the structural evidence that ACE cannot touch the
   engine.
9. https://steamcommunity.com/sharedfiles/filedetails/?id=1577375188 — **[search]** ACE's Workshop
   listing and changelog: the rebuilt real-time CPU graph with category filtering, the topology lag
   spike fix, building-with-complex-colour-patterns performance, crafting work units to effort.
10. https://www.stonehearth.net/dt-performance/ and
    https://www.stonehearth.net/desktop-tuesday-idle-musings/ — **[search]** the two developer posts
    that would be the primary sources for sections 2 and 3. Both refused connection when fetched
    (ECONNRESET), Steam's mirrors returned only page furniture, and web.archive.org is blocked for
    this tool. Their content reaches this file only through search-engine summaries.
11. https://stonehearth.github.io/modding_guide/modding_guide/essentials/programming_languages/index.html
    — **[search]** the engine stack: C++11 and OpenGL, Horde3D with custom node types, all gameplay
    and AI in Lua, interface in HTML and JavaScript.
12. https://stonehearth.github.io/modding_guide/files/CppReference.pdf — **[search]** the list of
    components that live in C++ and are exposed to Lua; the evidence for the native-data /
    scripted-decisions split.
13. https://stonehearth.github.io/modding_guide/modding_guide/intermediate/biomes/index.html —
    **[search]** world generation driven from Lua with the work done C++-side; 8 x 8 overview-map
    tiles classified by shaped noise into plains, foothills and mountains.
14. https://discourse.stonehearth.net/t/world-and-building-voxel-size/6264 — **[search]** the two
    voxel scales: a hearthling occupies a little over one terrain block; models are authored at
    10 x 10 x 10 model voxels per block.
15. https://github.com/stonehearth/lua_profiler_scripts — **[search]** Radiant published Lua
    profiling scripts for players, which is itself evidence of where they expected the cost to be.

## Confidence

**High.**

- The engine stack (C++11/OpenGL/Horde3D core, all gameplay and AI in Lua, HTML/JS interface) and
  the native-component / scripted-behaviour split.
- The AI architecture in section 3's first paragraph. This is the official modding guide describing
  its own system, read directly.
- That the whole simulation runs on one thread and that a client/server process split exists in
  single player.
- That ACE is an authorised **mod**, Lua and data only, with no engine access.
- That development ceased in 2018 with Kickstarter features unshipped.
- That the game's own profiler distinguishes `lua`, `lua_gc`, `pathfinder`, `topology`, `renderer`
  and idle — the category names are the vocabulary the whole community uses.

**Medium-high.**

- The filter-cache mechanism and its numbers (section 2b). These are a named mod author's
  measurements with the game's own profiler, method stated, reproducible in principle — but not
  developer-confirmed, and a single town on a single machine.
- That the AI's think phase performs item and pathfinder queries, making decision cost proportional
  to search cost. This is an inference from the documented architecture plus Radiant's own "busy"
  flag mitigation, not a direct statement. It is a strong inference; it is still an inference.

**Medium.**

- The pathfinder being A-star over a navigation grid stepping voxel by voxel, and the stated intent
  to move to a navigation mesh. This originates in a developer post I could not open; I have it only
  through a search summary. The claim is consistent and specific, but treat the wording as
  paraphrase.
- ACE's specific fixes (topology spikes, colour-pattern building, restocking, the pasture pathfinder
  modifier). Changelog-derived via search, not read in the primary.

**Low — treat as folklore.**

- "The garbage collector runs 20–50% of game time." Player reading of a bar; the one careful
  measurement found about 22%.
- The 8–14 GB memory figures and the memory-leak diagnosis. No developer confirmation, and GC
  pressure on a growing live set explains the same symptom.
- Any claim that AI was *the* bottleneck. The one developer statement says AI is the bulk of the
  cost, but it is a forum remark about threading difficulty, not a profile, and the best measurement
  available (filter cache at 15–18%) points at item queries rather than at decision-making as such —
  though on the reading in 2b those are the same wound seen from two sides.

**Explicitly not claimed.** Nothing in this file is a claim about Stonehearth's actual C++ source,
because it is not public and was not examined. No game data, code, art, audio, names or flavour text
has been reproduced.

## Could not be determined

- **The developers' own account of their optimisation work.** The two Desktop Tuesday posts that
  would settle sections 2 and 3 — "Performance++" and "Idle Musings" — could not be opened.
  stonehearth.net reset the connection on every attempt, Steam's mirrors returned only page
  furniture, and web.archive.org is blocked for this tool. **This is the single biggest hole in this
  file.** If someone can retrieve those two pages later, they should be read and this file amended;
  they are the difference between developer testimony and community inference for the whole of
  section 2.
- **Map dimensions.** Neither the horizontal extent in blocks nor — more relevant to us — the
  vertical extent. How many layers Stonehearth's terrain spanned, and whether that depth was a
  designed budget or an emergent limit, is unknown. For a Lane B file about verticality this is a
  real gap.
- **Whether the navigation mesh ever shipped**, or whether 1.0 released still stepping the navgrid
  voxel by voxel.
- **Any developer profile of the pathfinder's share of the tick.** We have the community's
  `filter_cache_cb` and `lua_gc` figures but no equivalent for `pathfinder`, so the claim
  "pathfinding was the problem" is asserted everywhere and measured nowhere in what I could reach.
- **Whether ACE ever had engine access.** Circumstantially no; no statement found either way.
- **ACE's own before/after performance numbers.** The only quantified figures found belong to a
  separate third-party mod, not to ACE.
- **The role of the Riot Games acquisition in the shutdown.** Not present in the Wikipedia text I
  read, and I did not spend cap on it. It is history rather than architecture, so the omission does
  not affect the recommendation.
- **Whether the idle-hearthling complaints were predominantly CPU starvation or predominantly AI
  logic bugs.** The two are entangled in every thread, and separating them would need the game and a
  profiler, not the public record.
