# B — Dwarf Fortress

## Question

OQ-31, Lane B. Dwarf Fortress solved discrete verticality twenty years before we tried to. What is
its world representation, what can the player order in three dimensions, how does it path between
levels, **how does it show a player what is above and below the slice they are looking at**, and
what do players praise and complain about — reading the interface failures as carefully as the
depth successes, including what the 2022 Steam release changed and whether it worked.

Clean room: mechanics, data shapes and design intent only. No game data, no names, no flavour text,
no decompilation. Everything below is described in our own words, and anything we would adopt is
given an Odyssey name.

## Findings

### 1. World representation

**The shape.** The map is a regular three-dimensional grid of cells, presented as a stack of
horizontal slices. The camera is fixed top-down and orthogonal: there is no rotation, no tilt and
no elevation view. A player never sees a cross-section of their own fortress — only one slice at a
time, with what shows through the holes in it.

**The cell is about the size of ours.** The wiki puts a cell at roughly 2 m × 2 m in plan and 3 m
tall, with fall physics using 2.8 m and movement-speed derivations implying nearer 2.4 m in plan.
Odyssey's 2.5 × 2.5 × 3.0 m (ADR 0002) is within a few per cent of the same physical scale, arrived
at independently. Twenty years of play has not found that scale wanting, which is quiet
corroboration of a decision we have already made irreversible.

**The load-bearing idea: a cell has two parts.** Every cell carries a *wall part* — the solid
volume filling the cell — and a *floor part* — the membrane at its bottom, which is also the
ceiling of the cell below. This is not presentation; it is the data shape, and every vertical verb
in the game is an edit to one of the two:

- Tunnelling horizontally clears the wall part and leaves the floor part, so you get a corridor.
- Cutting downward clears **both**, which opens the boundary between this cell and the one below.
- Stairs and ramps are each a specific combination of edits to the two parts across two cells.

The boundary between two levels is therefore a *field of a cell*, not a separate entity, and it is
what stops creatures, items, liquids and smells falling through. Once pierced it stays pierced
until something is built to reseal it, and a built floor is exactly that reseal. Digging down and
building up are the same operation on the same field, in opposite directions.

**Storage.** The map is chunked as 16 × 16 × 1 groups of cells, with many per-cell details stored
at the group level for space efficiency. Note the shape: the chunk is a **slice, not a cube**. DF
chunks horizontally and never vertically, which follows from a view that only ever renders one
level plus what shows through it.

**Sizes.** A fortress map is a rectangle of embark parcels, each 48 × 48 cells. Recommended play is
2 × 2 or 3 × 3 parcels (96 × 96 or 144 × 144 cells); the maximum selectable is 16 × 16 parcels —
768 × 768 cells. Depth: default worldgen yields roughly 50 levels of land plus about 15 of empty
sky above the highest ground; mountainous embarks exceed 100; worldgen parameters push the total
from a floor of 6 to well over 600.

**The asymmetry that matters to us.** DF's horizontal extent is comparable to ours — 144 × 144
typical against our 120 × 120, and its 768 against our 250 ceiling. Its *vertical* extent is an
order of magnitude larger: 50 to 200-plus against our 16 to 40. Almost every DF problem in the rest
of this file — pathing cost, depth illegibility, "how deep is this map?" confusion — is
proportional to that number. **Our 16 is not a limitation to be apologised for; it is the thing
that makes a legible depth interface possible.**

### 2. Dig and build in every direction

What the player can order, described as verbs:

| Verb | What it edits | Result |
|---|---|---|
| Tunnel | wall part of the target | a corridor on one level; floor stays |
| Cut down (channel) | wall **and** floor part | a hole; you stand above open space, and a solid cell below is converted into an up-ramp |
| Up stair | wall part only | useless unless the cell above has a matching down stair |
| Down stair | wall part, plus a connection in the floor part | pierces downward |
| Up-down stair | both parts | the middle rung of a shaft |
| Up ramp | wall part of the target **and both parts of the cell above** | one order, two cells edited |
| Remove ramp / stair / construction | restores or clears | — |
| Build wall / floor / ramp / stair / support | authors the parts directly | this is how you go **up** |

Three things are worth naming.

**Building up is symmetrical with digging down**, because both are edits to the same two fields.
Build a floor over open space, then build on it. The game needed no second system for towers.

**A shaft is not an object.** A stairwell is a column of independently designated cells that the
player must keep mutually consistent by hand — an up stair with nothing above it is simply useless,
and the game says nothing. Twenty years on this is still how it works, and it is a permanent tax on
every player who has ever dug a staircase.

**The one genuinely three-dimensional gesture is excellent.** A designation rectangle may have its
two corners on *different levels*: drag a box, change level, click, and you have designated a
volume. It is cheap, it is loved, and it is the only authoring verb in the game that is not
slice-local.

**The expression failure.** A ramp is only usable when four conditions hold at once: the ramp cell
itself, open space directly above it, a solid adjacent wall on its own level (diagonals count), and
a walkable cell directly above that wall. Three of the four live in cells other than the one the
player clicked. Ramps must be *offset* level to level, never stacked. Nothing validates this at
order time; the player discovers the mistake when a creature refuses to path. This is the canonical
Dwarf Fortress vertical-construction failure, and it is a feedback failure, not a simulation one —
the rule is perfectly reasonable, and the interface simply never tells you that you have broken it.

### 3. Cross-level pathing

**Algorithm.** A\* with an admissible heuristic, so the route found is optimal. The wiki is candid
that in the worst case the search visits every cell on the map, which is why the community's
standard performance advice is *wall off the parts of your fortress you are not using* — a
player-side workaround for the absence of a reachability pre-check at a granularity players can
exploit.

**Connectivity is maintained, and it drifts.** There is a connectivity-graph generator, and on very
large embarks its queue of pending edges can overflow the stack and silently drop edges, leaving
gaps in the graph. Separately, long-running forts have recurring bugs in which cells fall out of
the walkable group and become permanently unpathable, blocking jobs and buildings. So DF has
essentially our district-id idea, and **its recurring bug class is the cached connectivity
disagreeing with the map.** That is precisely the failure mode our own reachability work must be
tested against, not merely implemented alongside.

**How creatures cross levels.** Stairs move vertically and then horizontally: going down-and-north
is two steps. A ramp moves diagonally across levels in a single step, and moving up or down a ramp
costs the same as walking on the flat. Ramps are therefore strictly faster, and the community
builds ramp helices rather than stairwells wherever traffic matters. The lesson generalises: **the
price of a vertical move is a design lever with enormous consequences.** In DF, a one-step-versus-
two-step difference alone decides the shape of fortresses. Odyssey has already priced a hop at 135
and a drop at 50; those numbers will shape our colonies the same way.

**Player-facing path costs are a footgun.** The player can paint per-cell traffic costs
(high/normal/low/restricted). The advice is counter-intuitive: painting a *restriction* on a cell
that creatures must cross makes performance worse, because the search explores alternatives at
length before conceding. Giving a player a cost brush when they cannot see the search is giving
them a way to hurt themselves quietly.

**What pathing actually costs.** This is the finding that most directly contradicts our own
situation. Both the wiki and community instrumentation put pathfinding at **under 10% of unit
processing in large fortresses, around 6% of a tick**, while creatures taking their turns is over
60% and creature-to-creature line-of-sight and proximity checks are around 20%. A game with 200
levels and hundreds of creatures does not pay 65% for A\*. **Our 65% figure is therefore a
statement about our map, our heuristic and our search budget — not a law of the genre.** It should
be treated as a bug with a cause, not a cost of doing business.

> **Editor's note, 2026-09-17 — the two numbers are not measuring the same thing, and the gap is
> smaller than it looks.** Our 65% comes from the D1 spike, a stress harness issuing roughly 1.2
> long-range replans every tick with every agent re-pathing constantly; DF's ~6% is a running
> fortress. `OQ-19` measured the real tick the same day this file was written: a colony of fifty on
> a 250 × 250 × 40 board spends **37% of a 0.025 ms tick** in the pawn phase, of which pathing is
> only part, and reaches 97% only when D1's replan rate is deliberately laid over it. So the honest
> comparison is DF's fortress against our colony, not against our harness. The advice above still
> stands — the number deserves investigating and is not a genre constant — but a session should not
> go hunting a 65% defect in the running game, because the running game does not spend 65% there.
> See `docs/adr/0005-simulation-architecture.md`, addendum 2026-09-17.

**The author's own optimisation advice** is representational rather than algorithmic: do not
allocate a node object per cell as the textbooks show; flag the cell or store the value in a
structure you already have. Keep buffers "dirty" and clear them on a counter rather than per
search, when clearing is expensive. Add a cheap boolean to objects to short-circuit expensive
checks. That is a structure-of-arrays argument from the author of the canonical z-level game, and
it is the argument ADR 0005 already accepted.

### 4. How the UI shows depth

**The whole apparatus, honestly small:**

1. **One slice at a time**, top-down, no rotation, no cross-section, ever.
2. **Levels *below* show through.** Where a cell has no floor part, you see down, and lower levels
   are drawn darkened by a semi-transparent wash that deepens with distance — a depth fog.
3. **Levels *above* are not drawn at all.** There is no ceiling cue. You cannot tell from the slice
   whether you are indoors, under a floor you built, or under open sky, except by reading per-cell
   detail or scrolling up and back down.
4. **A depth ribbon** down the right-hand margin: a one-column-wide vertical bar in which one
   colour means above ground, another below ground, and a bright mark shows the current level. It
   is the only persistent, at-a-glance "where am I vertically" affordance in the game, and it is
   genuinely good.
5. **Two numbers**: absolute level relative to the bottom of the map, and a second figure giving
   levels relative to the surface **at the centre of the screen** — which changes as you scroll
   horizontally over uneven ground, so the reading is position-dependent and easy to misread.
6. **Navigation** by two keys, or the mouse wheel since the Steam release.

**The nameable failures:**

- **The depth fog was tuned on the surface and is wrong underground.** The default wash is
  sky-coloured, which reads as atmosphere outdoors and as spilled paint in a mine. Among the
  most-subscribed items in the first days of the Steam workshop were mods that do nothing but
  recolour it dark or thin its opacity — three separate ones in the search results alone. A depth
  cue whose first community response is *please turn it down* was validated on the wrong scene.
- **Depth is unpredictable, so the player cannot plan.** How far the rock goes down varies by
  embark and worldgen, the game never says, and the standard advice on the forums is "dig a test
  shaft before you commit to anything." Players compare notes against guides, find their map is a
  quarter the depth described, and conclude something is broken.
- **No vertical preview for vertical constructions.** A shaft is a column of unshared per-cell
  orders; a ramp has four conditions, three of which live elsewhere; neither is presented as one
  object and neither is validated on order.
- **Depth questions are answered by scrolling and remembering.** "Is this room sealed?", "what is
  holding this up?", "where is the water coming from?" have no view that answers them.
- **The Steam release moved level navigation to the mouse wheel** and the twenty-year-old key pair
  stopped working, so long-time players had to ask on the forums where the controls had gone. A
  self-inflicted wound, and a cheap one to avoid.

### 5. What players praise and complain about

**Praised — and nobody asks for it to be simplified:**

- Verticality itself is the game's signature. Digging down through strata, hitting water, a cavern,
  or magma; building towers; the three-dimensional consequences of liquids, collapses and falls.
- Cut-down-and-ramp digging is expressive once learned.
- The two-corners-on-different-levels designation box.
- Since the 2022 Steam release: mouse-driven navigation, clickable tabs, scrollbars, text filters
  in long lists, pixel-art item icons, category tabs, highlighting of the selected subcategory, and
  interface scaling that survives widescreen and small windows. Reviewers agree menus became
  "considerably less fiddly" and that finding a thing in a list is now possible at all.

**Complained about — the specific, nameable failures, which is what we came for:**

1. **Menu placement follows no spatial logic.** The most damaging review criticism of the Steam
   version is not that it is complex: it is that more things became clickable *without any rule
   governing where they sit*, while the old keyboard interface had a reliable, memorisable
   structure and kept the map area cleanly separated from menu information. The named regression is
   **visual noise in a busy fortress** — the playspace and the interface stopped being distinct.
2. **Alerts became a transient popup instead of a persistent feed.** The most-cited post-release
   complaint in player threads: you learn about a siege or a failed workshop from a popup, and
   unless you clear announcements constantly you cannot tell which are new. A colony sim's core job
   is telling you what changed, and that got harder while the menus got prettier.
3. **Bulk and repeat orders regressed.** "Make five of these" had no discoverable path; players who
   knew the old interface could not find how to queue a batch and asked publicly.
4. **Keyboard parity was lost.** Long-standing bindings moved or stopped working, and the new
   interface is mouse-first, which made routine work *slower for experienced players* even as it
   made the game possible for new ones. Storage-zone setup is the usual example of a task that got
   worse.
5. **The labour and jobs screen remains unreadable** — described as inscrutable, with the standard
   answer still being a third-party tool.
6. **Legibility at scale:** text too small at some resolutions even after adjusting the settings.
7. **The deepest complaint is about state, not commands.** Not "I cannot find the button" but "it
   is harder to assess what is going on in my fortress." Discoverability of *commands* improved;
   discoverability of *situation* did not.

**Did the 2022 changes work?** Commercially and in reach, unambiguously. Structurally, **half**:
the release fixed *input* — mouse, lists, filters, icons, scaling — and did not fix *information
architecture* — where things live, how you are told what changed, how you read the state of the
place. The author was explicit that the graphical interface was not a fork of the codebase but a
glyph swap over the same grid, which is exactly why the underlying organisation was untouched.
Depth presentation was essentially not revisited at all beyond adding the fog and moving level
navigation to the wheel.

**One more thing, and it is the most important sentence in this file for us.** The author names a
"map rewrite" as the outstanding structural debt — a second pass on the 2008–09 conversion from two
dimensions to three — because the current three-dimensional map limits how dense and modular
procedurally generated content can be, and blocks content that needs location types the
representation cannot express. **Sixteen years on, the 3D representation chosen early is the thing
constraining the game.**

## Recommendation

### Copy

1. **Make the seal between two cells an explicit, addressable field — not an implication.** DF's
   floor part is the single best idea in its data model, because dig-down, build-up, falling,
   liquids, cave-ins and "is this roofed?" all become one question about one field. Odyssey should
   name it **the deck**: the surface at the bottom of a cell, which a cut-down order removes and a
   built floor restores. We currently infer it from the terrain of the cell below; that inference
   will not survive the first time we want a hole in a floor that is not a hole in the rock.
2. **The volume designation gesture:** drag a rectangle, change layer, click. It is the only 3D
   authoring verb DF has, it is universally liked, and it is cheap.
3. **Ramps priced below stairs** — a diagonal move across a layer for the price of a flat step is
   what makes DF fortresses vertical rather than nominally vertical. We have the equivalent levers
   already (`JumpUp` 135, `Drop` 50, ladders); the recommendation is to treat those numbers as
   design, revisit them deliberately, and keep the planner and the mover agreeing.
4. **A persistent depth ribbon**, and make ours better than theirs. DF's one-column bar is the most
   effective depth affordance in the game *despite* having 200 rungs to compress. With 16 layers we
   can draw a rung per layer, mark the surface, mark the current slice, and mark every layer where
   the player has something — a colonist, a designation, a built thing. DF cannot do that legibly;
   we can. **This is the single highest-value thing in this file.**
5. **A connectivity graph with a test that it cannot drift.** DF has the structure and its recurring
   bug is the cache disagreeing with the map. Ship the district-id work (`d-04-pathfinding.md`)
   with a debug assertion that rebuilds the graph from scratch and compares, and a test that digs,
   builds and seals and checks the graph followed.
6. **The author's optimisation advice**, which we already follow and should keep following: no
   per-node allocation, flags in structures that already exist, dirty buffers cleared on a counter.

### Avoid

1. **Never let the player order a vertical structure that cannot work, in silence.** The unusable
   ramp is DF's archetypal failure. Every vertical order in Odyssey — ladder, stair, hop route —
   must be validated at order time and drawn as invalid in the ghost, with the reason named.
2. **Do not make a vertical structure a column of independent per-cell orders.** A ladder or stair
   shaft should be one object, ordered once, with a top and a bottom, that maintains its own cells.
   DF's per-cell stairs are a twenty-year tax.
3. **Do not tune the depth fade on the surface only.** DF's fog was judged outdoors and is the first
   thing players modded. Ours must be judged in a pit as well as on a terrace, and the honest form
   of this recommendation is: **put depth-fade strength in the settings panel.** DF's community
   shipped that feature on the developer's behalf; we can ship it ourselves for the cost of a
   slider.
4. **Do not lose the alert feed while polishing panels.** The most-cited post-Steam regression. A
   persistent, scrollable, unread-marked event log earns its place in our HUD before another
   popover does.
5. **Do not let mouse-first mean keyboard-never.** DF broke long-standing bindings and made expert
   play slower while making novice play possible. Every command wants a key; the Keys tab already
   in the settings panel is the right instinct and should be treated as a requirement, not a
   courtesy.
6. **Do not spend vertical extent we do not need.** DF's 50–200 layers are the root of its pathing
   cost, its depth illegibility and its "how deep is this map?" confusion. Our 16 is a feature.
   Resist growing towards 40 until a mechanic demands it and the ribbon still reads.
7. **Do not give the player a path-cost brush.** DF's traffic painting hurts as often as it helps
   because the player cannot see the search. Steer pathing with doors, zones and built routes that
   have visible meaning instead.
8. **Do not assume our own pathing number is normal.** DF pays ~6% of a tick for A\* on far bigger,
   far deeper maps. Our 65% is an outlier and should be investigated as one.

### The strategic point

The author of the definitive z-level game names the map representation as the debt he cannot pay,
and it was fixed in 2008. **We are at that moment now.** Cell size is settled and irreversible; the
questions of the same weight still open are the deck/seal field, the chunk shape (DF chunks
16 × 16 × 1 *slices*, not cubes — relevant to OQ-46's mesh contributors) and whether a cell can hold
more than one occupant. Those are worth settling before M4, not after.

**If only one thing is taken from this file,** take the validated single-object vertical structure
plus the volume designation box — not the fog. **Cheapest experiment that moves the depth
question:** build the depth ribbon against the existing `WorldRenderModel` — no simulation change,
no save change, no hash change — and put a fade-strength slider beside it. Then judge both in a dug
pit and on the terraced hillside, which are the two scenes DF's fog fails to serve at once.

## Layer questions touched

- **How depth is shown to a player.** Our rule — layers above ghosted, layers below drawn, never
  cut the landscape — is strictly more informative than DF, which draws nothing above at all. But
  it costs us something DF gets free: DF's slice is unambiguous, because everything on it is on it.
  **The risk to watch is that "ghosted" and "solid" must be unmistakably different, or the player
  loses which way is up.** DF avoids that problem by not having it.
- **The depth ribbon**, and the fact that our low layer count makes a per-layer rung possible.
- **Depth-fade strength as a player setting**, next to the existing Keys and Audio tabs.
- **Vertical structures as single objects** — directly relevant to the ladder and to the building
  line in `docs/design/15-building.md`.
- **Move-cost parity between planner and mover** — already a known seam (`PathFinder.RelaxHop`,
  `NavGraph.TryHopEdges`, `MovementSystem.StepCost`); DF shows how much the *value* matters, not
  just the agreement.
- **Connectivity cache correctness** — district ids, `d-04-pathfinding.md`.
- **Our 65% A\* share** — DF's ~6% makes it a defect rather than a genre constant.
- **Chunk shape for `ChunkMesher` (OQ-46)** — DF chunks slices, not cubes.
- **An alert feed in the HUD**, ahead of further popovers.
- **A deck/seal field in `CellGrid`**, with the hash consequences that implies (OQ-50, ADR 0005).

## Sources

- https://dwarffortresswiki.org/index.php/DF2014:Z-level — the z-level model, level counts by
  worldgen (6 to 600-plus, ~50 typical plus ~15 of sky), navigation keys, the depth readouts, and
  the note that fewer cavern levels raise framerate.
- https://dwarffortresswiki.org/index.php/DF2014:Z-axis — the boundary-between-levels model, what
  pierces it (ramps upward, down stairs and cut-downs downward), what reseals it, and the
  right-margin depth ribbon.
- https://dwarffortresswiki.org/index.php/DF2014:Tile — the wall-part / floor-part cell model and
  the physical scale of a cell (~2 × 2 × 3 m).
- https://dwarffortresswiki.org/index.php/DF2014:Mining — the full designation set and exactly which
  parts of which cells each one edits, plus the two-corners-on-different-levels designation box.
- https://dwarffortresswiki.org/index.php/DF2014:Ramp — the four conditions for a usable ramp, the
  one-step diagonal cross-level move, ramp cost parity with flat ground, and the
  do-not-stack-ramps gotcha.
- https://dwarffortresswiki.org/index.php/DF2014:Path — A\* with an admissible heuristic, the
  worst-case whole-map search, traffic-cost painting, and the "seal off unused areas" advice.
- https://dwarffortresswiki.org/index.php/Maximizing_framerate — the cost breakdown: pathing under
  10% of unit processing and ~6% of a tick, unit turns over 60%, creature proximity checks ~20%.
- https://docs.dfhack.org/en/stable/docs/api/Maps.html — the map is chunked as 16 × 16 × 1 groups
  with many details held at group level for space.
- https://dwarffortressbugtracker.com/view.php?id=12894 — connectivity graph drift: cells falling
  out of the walkable group and becoming permanently unpathable in long-running forts.
- https://www.gamedeveloper.com/programming/how-tarn-adams-upgraded-and-optimized-dwarf-fortress-for-its-official-steam-release
  — pathfinding optimisation by representation rather than algorithm, dirty buffers, cheap flags,
  the graphical interface as a glyph swap over the same grid, and the "map rewrite" named as the
  outstanding structural debt.
- https://www.pcgamer.com/dwarf-fortress-review/ — the review that names the specific Steam-version
  regression: more clickable, no logic to placement, visual noise, loss of the clean separation
  between playspace and menus. *(Full text did not render for us; see Could not be determined.)*
- https://www.pcgamesn.com/dwarf-fortress/menus — the concrete list of what the Steam interface
  added: mouse navigation, scrollbars, clickable tabs, pixel-art item icons, subcategory
  highlighting, scaling to widescreen and small windows.
- https://steamcommunity.com/app/975370/discussions/0/3709307511570113977/ — player thread naming
  the specific failures: the alert popup versus a feed, lost keybindings, no discoverable batch
  orders, the level-navigation keys that stopped working, the unreadable jobs screen.
- https://steamcommunity.com/app/975370/discussions/0/3771239049947871573/ — players on reading
  depth: unpredictable map depth, "dig a test shaft before you commit", difficulty telling which
  stratum you are in.
- https://steamcommunity.com/sharedfiles/filedetails/?id=2898829756 and
  https://steamcommunity.com/sharedfiles/filedetails/?id=2899395329 — early, heavily subscribed
  workshop items that exist solely to thin or recolour the depth fog, which is the evidence that
  the default was tuned on the wrong scene.

## Confidence

**High** — the wall-part / floor-part cell model and the designation verbs that edit it; the four
conditions for a usable ramp and the one-step diagonal move; map sizes and depth ranges; the
16 × 16 × 1 chunk shape; the slice-only camera and the absence of any view of what is above; the
right-margin depth ribbon; the existence and community reception of the depth fog; the specific
list of Steam-release improvements and the specific list of regressions.

**Medium** — the pathfinding cost shares (community instrumentation reported through the wiki, not
measured by us, and probably fortress-dependent); the connectivity-graph internals, which are
inferred from bug reports and tooling documentation rather than from source; the claim that
information architecture specifically was *not* improved, which is a synthesis across a review and
player threads rather than a single authoritative statement.

**Low** — the precise behaviour of the depth fog (how many levels remain visible, its opacity
curve, whether it is configurable in vanilla settings); whether DF's connectivity structure is
maintained per level or fully in three dimensions.

## Could not be determined

- **The per-cell memory layout**, and which fields are held per chunk versus per cell. The tooling
  documentation states the chunk size and then explicitly says it is only an overview.
- **Whether the connectivity structure is per-level or fully 3D**, how it is repaired after a dig,
  and whether it is consulted before every search or only for reachability. The bug reports prove
  it exists and that it drifts; they do not describe it.
- **The depth fog's parameters** — visible depth, opacity ramp, whether vanilla exposes any control.
  Both workshop pages failed to render text through the fetcher, so the finding rests on the mod
  titles and descriptions surfaced in search results.
- **Whether the Steam interface changes improved retention**, as opposed to sales. No data found.
- **The full text of the PC Gamer review.** The page returned truncated; the criticisms attributed
  to it here came through a search summary that quoted it directly, and are corroborated by the
  player threads, but we have not read the article end to end.
- **Whether any post-2022 update addressed the alert feed or the depth fog.** Not searched — the
  cap was reached.
- **Frame-time or memory figures for DF at any specific map size.** The community discusses
  framerate constantly and measures it almost never.
