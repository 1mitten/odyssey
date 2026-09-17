# B — Timberborn

## Question

OQ-32, Lane B. Timberborn is a modern, well-reviewed colony builder that solved verticality in a
true 3D grid — the same interface problem Odyssey has. What does it represent, how does it let the
player build and look at height, how does its signature water system sit on the grid, and does its
treatment of slopes make a case against our fixed decision ADR 0002 (cells are 2.5 × 2.5 × 3.0 m;
half-heights and slopes are a drawing offset, never a cell)?

Clean room: everything below is mechanics, data shapes and design intent described in my own words
from public wikis, developer articles and the studio's own request board. Nothing was decompiled and
no game data, art or flavour text was copied.

## Findings

### 1. World representation

**The terrain unit is a plain cube.** One tile wide, one tile deep, one tile tall, with no variant
geometry: no half-heights, no wedges, no bevelled corners. Terrain height is modelled by stacking
cubes, and nothing else. Completed cubes are terrain in every sense the simulation cares about —
they hold water, they count as ground for ground-only buildings, and crops grow on them.

**Map sizes are modest and the vertical budget is very small.** The editor allows anything from
4 × 4 up to 256 × 256, rectangular permitted. Terrain height in the editor is capped at 16 levels;
in play the world is 22 levels tall, with the extra headroom above the editor's 16 reserved for
player construction. Community mods raise the width cap (a figure of 384 circulates) and the height
cap, and a demonstration map an order of magnitude larger than the shipping limit took minutes to
load and could barely sustain normal speed. So the shipping envelope is roughly 65,000 columns and
22 levels — about 1.4 million potential cells, against Odyssey's current board at 120 × 120 × 16
(230,000) and our stated ceiling of 250 × 250 × 40 (2.5 million). We are aiming past a shipped,
optimised, five-year-old title's limit.

**For most of its life the terrain was not a volume at all — it was a heightmap.** The save file is
a zip around a JSON world description, and the terrain is recorded as a single array of per-column
heights. One number per column: the surface, and solid rock implied below it. That is not a 3D grid,
it is a 2.5D one, and it held from early access in 2021 until Update 7 in May 2025, when terrain
gained sideways overhangs, caves and blocks placed on building roofs — at which point one height per
column can no longer describe the world. The studio's own framing of Update 7 is telling: the
terrain "has always been made of little 3D cubes", but until then they could only go one atop
another. Four years and a headline update were spent converting a heightmap into a volume.

**What a cell carries** is small: solid or not, floodable or not, a moisture/irrigation value, a
contamination value, and whatever object occupies it. Water is explicitly *not* in the cell (§4). A
transition piece — the thing the game calls a slope — is a separate 1 × 1 × 1 *object* occupying a
cell, marked non-solid and floodable, not a shape of the terrain (§5).

### 2. Vertical building on a 3D grid

**Support is a declared property, not a solve.** Any building whose top surface is flagged solid can
carry another building. Platforms are one-tile pieces whose only job is to raise the build surface a
level, and the rule is stated plainly: a platform sits on ground, on another platform, or on a
building that supports stacking, and never in mid-air. There is no stress model, no load
propagation, no material strength. Removing something underneath destroys everything it carried, in
a cascade. This is a much cheaper mechanism than Odyssey's support solver, and it is sufficient for
buildings because the constraint is *contact*, not load.

**Terrain overhangs are a counted rule, also not a solve.** A terrain cube may be attached to the
side of another terrain cube, up to three unsupported cubes protruding sideways; the next level up
may then run another three. The result is caves, tunnels dug horizontally into a cliff, and
planting on roofs. A single integer limit does all the work that a physical model would.

**All vertical movement is placed content.** The inhabitants cannot jump and cannot climb. Every
change of level is an object someone built: single-tile stairs (which need clearance at both the
lower and upper end), spiral stairs (one tile, one level, stackable into a shaft, the compact
answer), bridges across gaps, and two faction-specific long-distance systems added in Update 7 — an
overhead cable transport built from stations, pylons and beams that handles large elevation
differences, and a modular tube network of one-tile vertical and horizontal pieces that is immune to
flooding. Pathing weights climbing above horizontal travel, so a single tall shaft beats several
short scattered staircases.

This is the deliberate opposite of Odyssey's free one-block hop. Because innate vertical mobility is
zero, a colony's circulation is entirely visible: if something can get up there, an object put it
there, and that object can be selected, priced, flooded and demolished. It also turns vertical
transport into a design space the studio has now mined twice.

**Placement at height has no height control.** There is no "build on level N" mode. The ghost snaps
to the surface under the cursor and the level falls out of whatever the ray hit; the preview is
green when legal and red when not. The player reaches a level by building a platform to it and then
pointing at the platform. A wrinkle players complain about: the height cap for terrain cubes and the
height cap for structures are different, so there is a band where a building may be placed and a
terrain cube may not, with no explanation given in the interface.

### 3. The layer UI

**What it is.** A layer control sits in the top-right corner, under the clock. It peels away
everything at and above a chosen level so the player can see and work at a level that is otherwise
buried. It is a *visibility filter* over the whole world, not a camera slice.

**It was built up in three passes, and the order is instructive.** Originally it hid buildings
only. Update 6 taught it to hide water as well. Update 7 taught it to hide *terrain*, which is what
finally made caves, tunnels and complicated stacked structures inspectable — and, in the same
update, added the ability to select a layer by clicking any tile on it. That last one is the good
idea: the player points at the thing they care about and the interface infers the level, instead of
hunting for a number.

**The failure modes are documented by the studio's own public request board**, which is the most
useful part of this research for us:

- *The filter is modal, sticky and silent.* A standing request asks for an on-screen warning while
  layer visibility is active, because buildings disappear and the tops of platforms become
  invisible, and players conclude the game is broken rather than that a filter is on.
- *There is no elevation readout under the cursor.* Another standing request: show the terrain
  height at the mouse. Building a long bridge or a dam at a consistent height currently means
  counting cubes by eye.
- *"Blocked" does not say what blocks it.* A request asks for the obstruction to be highlighted or
  named when a placement is refused; players report confusion over collision footprints that are
  larger than the visible model.
- *The interface is in the way of the work.* The build palette sits bottom-centre, exactly where the
  player is trying to place, and the selected-building panel occupies the middle of the screen, so
  the camera has to be shuffled around the UI.

Odyssey already does better than Timberborn on one axis the board does not raise: our rule that
anything drawn solid is clickable and a ghost never is, plus the fade of whatever hides a selected
colonist, ties visibility to interactivity. Timberborn's filter hides things without a comparable
story about what is now clickable, which is why "the platform top went invisible" reads as a bug to
its players.

### 4. Water and terrain deformation

**Water is a 2D simulation in a 3D game, and the studio says so openly.** Each column carries a
single depth value and is connected to its four neighbours by virtual pipes; the solver pushes water
towards level. It is adapted from the Mei/Decaudin/Hu fast hydraulic erosion model. Current and
momentum are not simulated — they are derived by comparing how much moved between ticks, and ticks
run a few times a second, independent of frame rate. Visually it is one continuous sheet lifted to
ground height plus depth, with flow painted on as texture. The choice was explicitly accuracy versus
cost: they wanted dams, pumps and terraforming to matter without paying for a volumetric fluid.

Contamination rides the same flow and mixes by volume ratio. Evaporation removes a fixed small
amount per exposed surface tile per day, which makes deep narrow reservoirs strictly better than
wide shallow ones and gives sealed tanks a purpose. Irrigation is a *separate* and cheaper field:
moisture spills outward from wet tiles with a falloff, and the falloff is much steeper uphill, which
is a deliberate fake that stops hillside irrigation being free.

**Terrain deformation is the economy.** Soil is a commodity; a terrain cube costs a fixed amount of
it and is built by ordinary builders from a construction site, with arrows on the site showing build
order so the builders do not seal themselves in behind their own work. Removal is by explosive
charges of three strengths or by an excavator building. There is an indestructible bottom layer, and
fluid sources cannot be moved or added. Once placed, a built cube is indistinguishable from natural
terrain for irrigation, water, growth and building.

The studio is candid that a cheap sim leaks: a crater plus a water-dumping building outperformed the
intended irrigation building, and a pump feeding a water wheel that powered the pump was a
perpetual-motion machine, patched by giving wheels an innate resistance rather than by reworking the
model. They shipped both rather than redesign.

**The unresolved tension:** water on a *column* and terrain in a *volume* are incompatible premises,
and Update 7 introduced the volume. What happens to water under an overhang, or in a cave with
terrain above it, is not described in any source I read.

### 5. Why its slopes are what we chose not to do

**First, the honest correction: Timberborn is not a counter-example to ADR 0002.** Its *terrain* is
exactly cubes, with no sloped geometry, no half-heights and no wedges — the same position we took.
The difference is narrower and more interesting than "they did slopes and we did not".

**What they actually did** is make the transition piece a **first-class object occupying a whole
cell**. It is 1 × 1 × 1, non-solid, floodable, traversable like a staircase, placed by the map
generator, present on most maps, and demolishable by builders (it stopped being an instant delete in
Update 6). It is content with a definition, not a shape of the ground and not a rendering trick.

**What that buys:**

- *Traversal is legible and selectable.* If a creature can walk up there, an object is there. The
  player can point at it, see it, cost it, and destroy it. "Why can it get up here" is answered by
  clicking.
- *The transition participates in the simulation.* Being floodable and non-solid means water, and by
  extension contamination and irrigation, treat a ramp differently from solid rock. A drawing offset
  can do none of that.
- *One place holds the truth.* Because the ramp is an object in a cell, the pathfinder, the mover
  and the renderer all read the same row. Odyssey's hop is priced in three separate seams —
  `PathFinder.RelaxHop`, `NavGraph.TryHopEdges` and `MovementSystem.StepCost` — and our own notes
  say a disagreement between them fails silently. An entity in a cell would collapse that to one.
- *It is tunable content.* The update history lists slopes among buildable structures around Update
  4, while the current reference says the player may remove one but not build one, and Update 7
  reduced the range effect slopes and stairs project onto nearby placement. Whatever the precise
  path, the studio has repeatedly turned the transition piece up and down as *content*. A drawing
  offset cannot be tuned; it can only be redrawn.

**What it costs:** an extra object type, an extra pass in worldgen, a save and hash footprint for
something that is mostly decoration, and a cell consumed by a thing that is neither ground nor
building. Timberborn also pays for it socially — the fact that ramps cannot be built is one of the
more upvoted complaints on its board, so the entity approach creates an expectation that the terrain
system then refuses.

**The strongest case against ADR 0002, put as strongly as I can make it.** The decision has two
halves, and only one of them survives contact with this game.

The half that survives: *no cell is half a cell.* Timberborn agrees. Cube terrain with a small
integer overhang rule is what a shipped, well-reviewed 3D colony builder converged on after four
years, and it is what makes their rules cheap to state and cheap to check. Half-height cells would
have bought them nothing.

The half that does not: *"slopes are a drawing offset, never a cell"* conflates two claims. It is
right that a slope is not a cell *shape*. It does not follow that a slope cannot be a *thing in a
cell* — and Timberborn demonstrates that the thing-in-a-cell version is the one that earns its keep,
because it is the version the player can click, the water can flood and the pathfinder can cite.

And there is a scale argument that is specific to us and cuts harder. Timberborn's cube is roughly
isotropic in feel; its terraces read as landscape. **Our cell is taller than it is wide — 3.0 m up
against 2.5 m across.** A 3 m step is not a step, it is a storey. A purely cubic world at our
proportions makes every natural hillside a sheer wall, which is precisely why our surface already
needs "real 3 m terrace risers" drawn with banks and relief, and why we have a one-block hop priced
at 135 that lets a pawn ascend 3 m unaided. The harder the drawing works to disguise the riser, the
further the picture drifts from the simulation — and **the picture is what the player clicks.** That
drift is the real cost of ADR 0002's second half, and it is not hypothetical: we already have a
free, invisible, physically implausible 3 m hop compensating for it, replicated across three seams
that must agree.

**Verdict, committed.** ADR 0002 should stand, unamended in its geometry and amended in its scope.
Keep "no cell is half a cell" — it is right and Timberborn confirms it. Stop reading the second
clause as "a transition may only ever be art". The cheapest change that captures the whole of
Timberborn's benefit without touching the cell size, the support solver or the mesher is to give the
transition a def and a cell, exactly as we already do for a ladder: a ramp object occupying one full
cell, non-solid, walkable at a stated cost, emitted by worldgen wherever a terrace riser meets
walkable ground on both sides, saved, hashed, selectable and removable. That is not a slope cell. It
is a cell with a slope in it, and ADR 0002 never forbade it.

## Recommendation

**Copy, in priority order:**

1. **Click a tile to set the slice.** Their Update 7 keybinding is the single best idea in their
   layer interface. Pointing at the thing you care about beats hunting for a number, and it composes
   with our existing rule that solid things are clickable.
2. **Show the cursor's z, always.** A persistent elevation readout under the mouse is their
   longest-standing unmet vertical-UI request, it is cheap for us, and every complaint about
   building bridges and dams at the wrong height traces back to its absence.
3. **Make slice state loudly legible.** Their top layer complaint is a forgotten filter that reads
   as a bug. Ours hides more aggressively than theirs, so we need a persistent, unmissable indicator
   of the current depth and of the fact that anything is being hidden at all.
4. **Name the blocker.** When a placement is refused, say what refused it, in the ghost's tooltip.
   Do not ship a red square.
5. **Keep the build palette and the selection panel out of the centre-bottom.** That is where the
   ghost goes. They put both there and their players have been asking them to move for years.
6. **Prefer the cheap rule to the solve where contact is the real constraint.** A declared
   "surface: solid" flag on a building def plus cascade-on-removal handles building-on-building
   without extending our support solver; keep the solver for terrain and structural spans, and cap
   overhangs with an integer rather than a stress model.
7. **Give the transition a def and a cell** (§5). One object, one price, one truth for the planner,
   the mover and the renderer.
8. **If water ever arrives:** per-column depth with four virtual pipes and derived momentum, at a
   coarse tick, rendered as a lifted sheet. It is the right trade. But decide *once*, knowing that
   this premise is what their Update 7 broke, and that we are a volume from day one.

**Avoid:**

1. **Never ship a heightmap and retrofit a volume.** It cost them four years and a flagship update.
   We are already a volume — the danger is a *subsystem* quietly assuming one surface per column.
   Anything shaped like moisture, irrigation, light, weather or a cached "ground height" is exactly
   that trap; audit any such cache before it has dependants.
2. **Do not let the depth control become a pure visibility filter.** Ours ties visibility to
   clickability and fades what hides a selection; that is better than theirs and it is the thing
   their board is implicitly asking for. Keep it, and do not add a second, unrelated "hide layers"
   toggle beside it.
3. **Do not run two different vertical caps.** Their terrain cap and structure cap differ, unexplained,
   and it confuses players. One ceiling, stated.
4. **Do not make zero innate vertical mobility the model** — but do not leave our hop invisible
   either. Their strictness works because their inhabitants are a logistics fluid; ours are
   characters who must never strand. Keep the hop, but surface it: a hop edge should be inspectable
   in the UI, and the three seams that price it need a test asserting they agree, since our own notes
   say a disagreement fails silently today.

## Layer questions touched

- **The layer/slice UI (the core of this lane).** Confirms our depth-budget design is sound and
  supplies four concrete gaps to close before anyone plays: click-to-set-slice, a cursor elevation
  readout, a loud slice indicator, and a named blocker on refused placement. Their three-update
  march from hiding buildings, to hiding water, to hiding terrain suggests the useful test is
  whether *every* category of thing obeys the slice, not just the ones that were easy.
- **ADR 0002 (fixed).** Unchallenged on geometry; challenged on scope. Recommendation above: keep
  the cell, promote the transition from a drawing offset to a def'd object in a cell.
- **Movement set (walk, stair, ladder, hop, drop).** Their model is the strict opposite and it is a
  real alternative we have already rejected for good reasons; the transferable part is making
  vertical connections visible and inspectable rather than implicit.
- **Save coverage (known gap).** Their built terrain is saved and is indistinguishable from natural
  terrain afterwards. Our felled trees, mined cells and building sites are not in the save. Their
  design makes it obvious why that matters: once terrain is a thing the player *spends resources on*,
  it is unarguably state.
- **State hash covering the world (OQ-50).** Same argument, and already settled our way.
- **Support (OQ-46 and the mesher).** Their overhang-by-integer rule is a hint that mesh contributors
  should not need to know about structural reasoning at all.

## Sources

- https://timberborn.wiki.gg/wiki/Terrain_Block — the cubic terrain unit, what a completed block
  counts as, the three-block sideways overhang limit, and building cost in soil.
- https://timberborn.wiki.gg/wiki/Terraforming — removal by explosives/tunnels/excavator, the
  indestructible bottom layer, immovable fluid sources, and the 16-in-editor / 22-in-play height caps.
- https://timberborn.wiki.gg/wiki/Slope — the transition piece as a 1 × 1 × 1 non-solid, floodable
  object that the player may remove but not build; the basis for the whole of §5.
- https://timberborn.wiki.gg/wiki/Update_7 — the move to true 3D terrain, terrain on roofs and
  overhangs, tunnels, the layer tool learning to hide terrain, click-a-tile layer selection, and the
  two long-distance vertical transport systems.
- https://timberborn.wiki.gg/wiki/Functional_Update_History — the timeline that shows verticality was
  built up over four years, including when the layer tool learned about water and then terrain.
- https://www.gamedeveloper.com/design/deep-dive-timberborn-s-water-mechanics — the developers'
  own account of the virtual-pipe per-column water model, the hybrid 2D/3D framing, the separate
  moisture spill field, and the exploits they chose to live with.
- https://timberborn.wiki.gg/wiki/Map_Editor — map size range 4 × 4 to 256 × 256, rectangular
  allowed, editor height cap.
- https://timberborn.featureupvote.com/suggestions/221063/add-onscreen-warning-when-layer-visibility-is-active-building-disappear-platform
  — the request for a warning while the layer filter is active; the "buildings vanished, platform
  tops invisible" failure mode, stated by players.
- https://timberborn.featureupvote.com/suggestions/216247/add-landscape-elevation-on-screen-to-show-terrain-height-under-mouse
  — the standing request for a cursor elevation readout, motivated by bridges and dams.
- https://timberborn.featureupvote.com/suggestions/474289/incorrect-building-placement-show-what-blocks-construction-location-collision-hi
  — the request that a refused placement name or highlight what blocks it.
- https://timberborn.org/articles/vertical-building-stacking-guide — platform support rules,
  cascading demolition, stairs versus spiral stairs, and platforms blocking water flow.
- https://timberborn.wiki.gg/wiki/Version_1/Two_Days_to_Launch_-_Building_the_Beaver_Way — the
  studio's own framing of vertical building as a feature that early players pushed into a pillar,
  and the layer tool's position under the clock.
- https://timberborn.fandom.com/wiki/Game_Save_File — the save is a zip around a JSON world, with
  terrain stored as a single array of per-column heights: the evidence that it began as a heightmap.
- https://mechanistry.com/press/ziplines-tubeways-timberborn-update-7-adds-3d-terrain-unique-modular-transport-for-each-faction-more
  — the studio's press framing of Update 7: cubes existed all along, but only stacked one atop
  another until then.

## Confidence

- **High** on the world representation (cube terrain, no sloped geometry, stack-only until 2025),
  the building support rules (declared solid surfaces, no floating, cascade on removal, three-block
  overhang cap), the fact that vertical movement is entirely placed content, and the water model
  (per-column depth, four virtual pipes, derived momentum, coarse tick, rendered as a lifted sheet) —
  these are stated consistently by the developers and the official wiki.
- **High** on the layer tool's *problems*, because they come from the studio's own public request
  board in the players' words, not from a reviewer's impression.
- **Medium** on the layer tool's exact form and controls. I did not see it running. "Top-right,
  under the clock", "peels away levels", "click a tile to select its layer" are reported; whether it
  is a slider, a stack of buttons or a scroll-wheel modifier, and what the keybinding is, are not.
- **Medium** on map size and height caps: the 4 × 4 – 256 × 256 range and the 16/22 split are
  reported consistently, but the larger numbers that appear alongside them (a 384 width, mod-raised
  heights) are community figures and may be mod ceilings rather than engine ones.
- **Medium-low** on the history of buildable transition pieces. The update history lists slopes among
  Update 4's buildable structures while the current reference says the player cannot build one; I
  could not reconcile these in the budget and have flagged it below rather than lean on it.
- **High** on the §5 verdict, which does not depend on any of the uncertain points: it rests on the
  uncontested fact that their transition piece is a def'd object in a cell rather than terrain
  geometry or a rendering offset.

## Could not be determined

- **The real-world scale of a terrain cube.** No source states a metre size. Every comparison here
  with our 2.5 × 2.5 × 3.0 m cell is by proportion and feel, and the "a 3 m step is a storey"
  argument in §5 is an argument about *our* cell, not a measured contrast with theirs. If that
  argument matters to a decision, measure it in our own game rather than inheriting it from here.
- **How water works after Update 7.** A per-column depth value cannot describe water under an
  overhang or in a cave with terrain above it. Whether a column can now carry more than one body,
  whether water is simply forbidden under overhangs, or whether it is handled some third way, is
  described nowhere I read. This is the most important open item, because it is the exact seam where
  a 2.5D subsystem met a 3D world — which is the failure mode we are trying to avoid.
- **Whether the save format still stores one height per column.** The heightmap array is documented
  from before Update 7. Its post-Update-7 replacement (runs? sparse voxels? a separate overhang
  list?) I did not find, and it would be the single most useful data point for our own persistence
  design.
- **The exact widget and keybinding of the layer tool**, and whether selecting a layer by clicking a
  tile also changes what is clickable or only what is visible.
- **Whether the transition piece was ever player-buildable**, and if so when and why that was
  withdrawn. The two official pages appear to disagree.
- **Any published performance figures** — tick cost, memory, or the cost of the terrain rebuild in
  Update 7. The only quantitative signal found is anecdotal: a map sixteen times the shipping area
  loads in minutes and cannot hold normal speed.
