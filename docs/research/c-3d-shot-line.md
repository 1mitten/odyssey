# A shot line through layers: walking a 3D grid deterministically
*Lane C (open-source reference code) · 2026-09-25 · capped at 10 searches and 8 reads (9 searches used, one of them a GitHub code search; 8 pages read, plus 7 fetch attempts refused by the egress proxy — RogueBasin, the RimWorld wiki, Sam Driver, Steam, Ludeon, the DF wiki and Red Blob Games — so the roguelike-community and RimWorld material below is from search snippets only and is marked as such)*

> *Coordinator's note, 2026-09-25:* the Cataclysm-DDA source was read directly; the roguelike-community and reference-wiki material is from snippets. Design 47 §2 adopts the supercover walk, the slab read and the lenient corner rule as written here.

## Question

How should a single straight shot line be walked through Odyssey's grid of 2.5 × 2.5 × 3.0 m cells so that a bullet fired between layers is deterministic, symmetric (A→B visits what B→A visits) and cheap, in integer arithmetic only — and what do open-source layered games test in each visited cell, in particular the floor slab when the line crosses from one layer to the next?

## Findings

1. **Anisotropy does not touch the walk if both endpoints are cell centres.** An axis-aligned scaling maps the cell lattice to itself and a segment to a segment, so *which cells a centre-to-centre segment passes through* is the same whether the cells are cubes or 2.5 × 2.5 × 3.0. The cell size enters only (a) the Euclidean distance in metres, (b) any endpoint offset given in metres (an eye height, a chest height) and (c) the miss radius in metres. Amanatides–Woo's own overview handles unequal voxel sizes by dividing the position by the per-axis size when indexing and multiplying the per-axis step by it — a per-axis scale factor, nothing structural. [own derivation; cgyurgyik overview of Amanatides–Woo]

2. **Three families of line, and which cells each visits.** (i) *3D Bresenham* (Cataclysm-DDA `line.cpp`, `bresenham(tripoint…)`): integer, slopes doubled "to avoid rounding errors", one loop over the dominant axis with one error term per minor axis (`t`, `t2`); in the branch where z dominates, x and y may each step *in the same iteration* as z, so one step can change all three coordinates. It is 26-connected: exactly one cell per step, and the "corner" cells a diagonal step cuts past are never visited. (ii) *Amanatides–Woo voxel traversal*: keeps a "next face crossing" parameter per axis and steps the axis whose crossing comes first; it visits *every* cell the segment passes through (6-connected supercover), one cell per face crossing; ties at an edge or vertex fall to whichever comparison is written first (`if tMaxX < tMaxY`), with no tie rule described. (iii) *2D line with z interpolated per step* is what (i) degenerates to when z is a minor axis, and shares its corner-skipping. A separate line of work (Liu 2004, "An integer one-pass algorithm for voxel traversal") shows the supercover can be done in integers only, "visiting 1–3 voxels per iteration". [CDDA `src/line.cpp`; cgyurgyik overview; Liu 2004 abstract via search]

3. **The integer form of the supercover is small.** Double the cell coordinates so a centre is an odd integer and a face an even one; the parameter at which the segment crosses the k-th face on an axis is then an integer numerator over the axis's doubled extent, and "which face comes next" is one cross-multiplication. On Huge (240 cells) the products are below 250,000, comfortably `int`. Endpoints off-centre in decimetres (cell = 25 × 25 × 30) still fit `int` (products under 4 × 10⁷); centimetres would not. [own derivation from the Amanatides–Woo structure; the CDDA doubled-slope trick]

4. **Cataclysm-DDA's rule for a step that changes layer — the slab question, answered by a shipped game.** In `map::sees` (3D branch) every consecutive pair of visited cells is compared. Same z: the new cell must be transparent. Different z: with `max_z` the higher of the two, the line is **blocked only if both detours are shut** — detour one is *across then up*: blocked if the column of the new point has a floor at `max_z` **or** the cell at (new xy, old z) is opaque; detour two is *up then across*: blocked if the column of the old point has a floor at `max_z` **or** the cell at (old xy, new z) is opaque. So a 3D-diagonal step is allowed when *either* corner route is open, and the slab checked is always the floor of the upper cell in the column where the line rises or falls. The comment above it reads "TODO: Allow transparent floors (and cache them!)" — floors are opaque without exception. The target cell itself is exempt from the opacity test ("it's still visible even if opaque") **unless** the last step is a vertical transition, in which case the slab test still applies. [CDDA `src/map.cpp`, `map::sees`, lines ~8339–8367 on master]

5. **The same two-route idea is easy to get wrong.** CDDA's movement-clearance variant (`clear_path`, lines ~8701–8748) applies the same corner rule to move cost, and its second branch tests `has_floor_or_support` where the first tests its negation — it reads as an inverted test that nobody has noticed because it is rarely reached. A rule with two symmetric halves needs a test that exercises both halves and a mirror. [CDDA `src/map.cpp`]

6. **A floor rule with no test leaks.** CDDA issue #14908: with 3D vision on, a player three storeys up could target and kill monsters on lower floors "through the floor" despite walls and doors between. Issue #30300 is a second z-level LOS exploit. Both are evidence that the slab test is the one that gets bypassed by a second code path (the targeting UI, a radar) rather than by the line itself. [CDDA #14908; #30300 via search]

7. **Symmetry: the asymmetry is Bresenham's, and the shipped fixes are three.** Bresenham is not symmetric: with an obstruction in a particular cell "you can see a monster but it cannot see you", or the reverse (RogueBasin, snippet). The known remedies: the TranThong symmetric line; checking both directions; or a rule symmetric by construction — symmetric shadowcasting "maps exactly to line of sight with Bresenham's algorithm: if you can draw an unobstructed line between two floor tiles, they are guaranteed to be in each other's field of view", and SymmetricPCVT defines LOS(a,b) as true iff each is in the other's FOV. CDDA's history is the cautionary one: issue #11422 documents "you can see it but can never hit it from here" because vision used a broader test than the single Bresenham the projectile walks; the Bresenham overhaul (PR #13161) factored one line routine, made monster→player visibility symmetric with player→monster, and left the shadowcasting-vs-Bresenham disagreement open ("I'm just not ready to dive into that morass again"). Its projectile fix is `find_clear_path`, which tries a family of Bresenham start offsets and returns the first clear line — a fairness patch, not symmetry. [search snippets from RogueBasin, Albert Ford, SymmetricPCVT; CDDA #11422, PR #13161, `map::find_clear_path`]

8. **A supercover between cell centres is symmetric by construction**, because the set of cells a segment touches does not depend on which end you start from; the only order-dependent part is the tie at an edge or vertex, and CDDA's two-route rule (finding 4) examines the same two cells whichever way the step is taken, so it is order-independent too. Bresenham can be *made* symmetric by canonicalising the endpoint order before walking (CDDA already canonicalises its `sees` cache key "so the cache is reflexive", but not the walk), at the price of a consistent one-sided bias that a mirrored board exposes. [own derivation; CDDA `sees_cache_key`]

9. **The strict supercover ("every touched cell must be open") fails Odyssey's commonest case.** A colonist at the edge of a terrace top, firing at the ground cell diagonally below: the centre-to-centre line passes exactly through the edge shared by the shooter's cell, the air above the target, the target, and the rock under the shooter's feet. Strict blocks it on the rock's corner. CDDA's lenient rule passes it via *across then down*, and correctly blocks the same shot when the terrace is two cells wide, because the line dips below the terrace top while still over rock — the rim intercepts. [own worked examples against the rule in finding 4]

10. **Endpoint heights: practitioners offset both ends by the same rule.** Combat Extended (the 3D-ish combat mod for the reference game) gives every thing a `CollisionVertical`: walls are 2 m tall (`WallCollisionHeight`); a standing pawn's shot height is its collision height less 15 % ("0.85 — altitude at which pawns hold guns"); crouching lowers the top to 45 % of height, raised to match adjacent cover; hits below 45 % land on the lower body region; a projectile starting above wall height under a roof ignores that roof. The shot line runs from the shooter's `shotHeight` to the *target's own* `shotHeight`, so both ends use one rule. A generic voxel-game answer in the search results is a ray "from a point above the origin of the observer to a point above the origin of the target", 1.5 m being a typical eye height. [Combat Extended `CollisionVertical.cs`, `Harmony_CompAbilityEffect_LaunchProjectile.cs`; search snippet]

11. **Distance across layers in the references.** CDDA's `trig_dist` is the plain 3D Euclidean with z unscaled (a layer counts as one cell) and `square_dist` is Chebyshev over all three axes; `sees` also refuses any pair further apart in z than `fov_3d_z_range`. Dwarf Fortress (Steam threads, snippets only, and they disagree): "range is an invisible 3D sphere — the most horizontal range at their own z-level and less as you go up and down", "up to 3 z-levels up or down"; another says a height difference "counts as an extra tile with no bonus from shooting from higher ground". Both agree there is **no height bonus**, matching the owner's decision. Fortifications "allow the passage of projectiles", and the standing archery-tower advice is to put the firing slits one level *above* where targets will be. [CDDA `line.cpp`, `map.cpp`; DF wiki and Steam snippets]

12. **Cost and caching.** A supercover walk visits at most |dx|+|dy|+|dz| cells plus one slab read per layer change — some 30–60 array reads for a 25-cell shot, single-digit microseconds. Fifty shooters on 1 s cooldowns fire fifty lines a second, under one a tick; the real load is target *selection*, and even fifty shooters each testing ten candidates once a second is ~8 walks a tick. CDDA nonetheless keeps a 100,000-entry LRU of `sees` results keyed on the canonicalised pair (its invalidation was not examined). Nothing read suggests a cache pays at Odyssey's scale; the combat gate already measures a whole fight at 0.02–0.09 ms a tick. [CDDA `map.cpp`; own arithmetic]

13. **Miss scatter.** Only snippets (the wiki and forum pages were blocked): in the reference the miss radius "can go up to 2.4 tiles off to each side of the target", stray shots have a 50 % chance of hitting nothing and 50 % of hitting whatever is on the cell they land on, and living pawns between shooter and target intercept. Nothing found treats scatter in three dimensions; Combat Extended keeps its scatter as an angle at the muzzle and lets the projectile fly a real arc, which is the opposite design from the reference's "pick a landing cell". [search snippets; CE via finding 10]

14. **Drawing a shot across layers.** The only shipped evidence is a bug: CDDA issue #29089 — with 3D vision on, the fire/throw interface showed a *blank* level and no cursor when aiming a level up, while the look-around interface drew it correctly; fixed in #29104 by making targeting draw the target's level the way look-around does. The lesson is that the targeting/tracer view must be driven by the *target's* layer, not the shooter's, and that the two interfaces must share one renderer. No source addressed a cut-away camera. [CDDA #29089]

## Recommendation

**Line algorithm — ranked.**

1. **Back this: an integer supercover walk (Amanatides–Woo in exact rationals) between cell centres, with CDDA's two-route rule at every layer change and at every exact edge/vertex tie.** Coordinates doubled so centres and faces are integers; a "next crossing" numerator per axis over the axis's extent; step the axis whose fraction is least by cross-multiplication; on a tie, do not pick an axis — resolve the corner with the two-route test. Symmetric by construction (finding 8), visits every cell the bullet actually passes through (so a wall corner cannot be clipped), and every event is either "entered cell (x,y,z)" or "crossed the z-plane inside column (x,y)", which is exactly the vocabulary the blocking rules need. All `int`, no division.
2. CDDA-style 3D Bresenham plus the two-route check on diagonal steps. Cheaper by a few reads, integer, proven in a shipped z-level game; but 26-connected (it skips corners a bullet grazes) and asymmetric unless the endpoints are canonically ordered, which then bakes in a one-sided bias.
3. A 2D line with z interpolated per step: the same connectivity problems as (2) with an extra rounding rule at .5 that is itself asymmetric.

(1) and (2) are close on cost and both pass the terrace-edge case; the observation that separates them is **mirror invariance**: mirror a board in x and the set of pairs that see each other must mirror too. The cheapest experiment is a fast-tier property test — random boards with walls, slabs and terraces, a few thousand random pairs, assert `sees(A,B) == sees(B,A)` and that mirroring the board mirrors the answers. (1) passes both by construction; (2) passes the first only after canonicalising and fails the second.

**Blocking rules — ranked, and the one to take.**

- *Entering a cell:* solid terrain blocks; a wall, pillar or closed door blocks; an open door passes; a window is a per-Def flag (see below); the target's own cell is never tested for opacity (a pawn in a doorway is hittable), as in CDDA.
- *Crossing the z-plane in column (x,y) from z to z+1 (either direction):* blocked if cell (x,y,z+1) carries a floor slab — that one read is the whole "roof/slab" rule, and it applies even when the crossing is the final step to the target.
- *A tie (the line passes through an edge or vertex):* take CDDA's lenient rule — pass if **either** monotone route round the corner is open, each route being its intermediate cell's entry test plus its own slab test. Reject the strict "all touched cells open" rule: it blocks a colonist shooting down off the lip of her own terrace (finding 9). A vertex tie (all three axes at once, rare between cell centres) is the same rule over the six two-cell routes.

**Endpoints.** Put every endpoint at the cell centre in x and y and at **mid-height, 1.5 m** — one rule for shooter and target, standing or downed, as Combat Extended does with one `shotHeight` per thing. It keeps the walk anisotropy-free (finding 1), keeps symmetry (both ends offset identically), and puts a same-layer shot on a horizontal line that can never graze a slab. Whether the target is lying down is the hit roll's business, not the line's. With no height bonus there is nothing an elevation angle would feed.

**Distance.** Squared Euclidean in decimetres — (25·dx)² + (25·dy)² + (30·dz)² — compared against a squared range; an integer square root only where accuracy needs the length itself.

**Cost.** No cache. Add one counter (walks per tick) to the combat soak; the day it reads in the hundreds, the symmetric canonical key makes a per-tick memo a ten-line change.

**Miss scatter.** Choose the miss cell in the **target's own horizontal layer**, within a radius in metres that grows with the true 3D distance, then walk the line from the shooter's centre to that cell's mid-height and strike the first pawn, wall or slab on it. A sphere would put miss cells in the air (nothing to hit) or under a floor (unreachable), and the reference's radius is a footprint on the ground. Seen from an oblique shooter the disc is foreshortened along the line of fire, which is what a shot from above does. If the chosen column is rock or void at that layer, the walk already answers it (the round stops in the wall, or passes on into the void).

**Drawing.** Drive the tracer and the impact by the target's layer, in a pass that draws through the cut-away the way `PowerLinePass` already draws lines through everything, so neither end is silently hidden; and let an incoming shot from a hidden layer jump the camera the way an Events row does. This is a recommendation from precedent, not from a source (finding 14 is the only one).

## Sources

- https://raw.githubusercontent.com/CleverRaven/Cataclysm-DDA/master/src/line.cpp
- https://raw.githubusercontent.com/CleverRaven/Cataclysm-DDA/master/src/map.cpp
- https://github.com/CleverRaven/Cataclysm-DDA/issues/14908
- https://github.com/CleverRaven/Cataclysm-DDA/issues/11422
- https://github.com/CleverRaven/Cataclysm-DDA/pull/13161
- https://github.com/CleverRaven/Cataclysm-DDA/issues/29089
- https://github.com/CleverRaven/Cataclysm-DDA/issues/30300
- https://github.com/cgyurgyik/fast-voxel-traversal-algorithm/blob/master/overview/FastVoxelTraversalOverview.md
- https://www.researchgate.net/publication/2611491_A_Fast_Voxel_Traversal_Algorithm_for_Ray_Tracing
- https://onlinelibrary.wiley.com/doi/10.1111/j.1467-8659.2004.00750.x
- https://raw.githubusercontent.com/CombatExtended-Continued/CombatExtended/Development/Source/CombatExtended/CombatExtended/CollisionVertical.cs
- https://github.com/CombatExtended-Continued/CombatExtended (code search: `Harmony_CompAbilityEffect_LaunchProjectile.cs`, `ProjectileCE.cs`)
- https://www.roguebasin.com/index.php?title=Bresenham's_Line_Algorithm (snippet only; blocked)
- https://www.albertford.com/shadowcasting/ (snippet only)
- https://github.com/denismr/SymmetricPCVT (snippet only)
- https://samdriver.xyz/article/line-of-sight-on-grid (snippet only; blocked)
- https://steamcommunity.com/app/975370/discussions/0/3969421533318098665/ (snippet only; blocked)
- https://steamcommunity.com/app/975370/discussions/0/5828254465008308843/ (snippet only)
- https://dwarffortresswiki.org/index.php/Fortification (snippet only; blocked)
- https://rimworldwiki.com/wiki/Combat (snippet only; blocked)

## Confidence

**Medium-high on the algorithm and the slab rule** — the CDDA rule was read from the current source and the supercover's properties follow from geometry; **low on Dwarf Fortress, RimWorld scatter and the drawing question**, which rest on search snippets because every community page was refused by the proxy.

## Could not be determined

- Whether a **window** should pass a shot: no read source states a rule (CDDA windows are transparent to *sight*; what they do to a projectile was not read). Give the edifice Def a flag and let the design decide.
- The reference's exact miss-cell selection and how its radius scales with distance — the wiki and forum were blocked; the caller's own description was assumed.
- How CDDA invalidates its `sees` LRU when the map changes.
- Any layered game's answer to drawing a shot whose far end is on a cut-away layer; CDDA's evidence is one bug about drawing the wrong level.
- Whether CDDA's inverted floor test in `clear_path` (finding 5) is a real bug or a deliberate asymmetry — worth knowing only as a warning.

## Layer question 5: can you shoot up and down?

Yes, and the line decides it, not a special case: walk an integer supercover from the shooter's cell centre at mid-height to the target's, test each entered cell for rock, wall or closed door, test the floor slab of the upper cell in the column where the line crosses a layer boundary, and at an exact corner let the shot through if either way round it is open. A slab, a roof, a closed door or rock anywhere on that line stops it; a terrace top, a rooftop or an upper floor with open air between fires freely down or up; distance is Euclidean in metres over the anisotropic cells with no height bonus, and a miss lands in the target's own layer and is walked the same way.
