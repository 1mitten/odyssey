# Floors, roofs and collapse — U29

**Status:** design settled by owner interview 2026-09-17; implementation on `claude/floors-and-collapse`.
**Read first:** `02-world-and-layers.md` §4 (the support rule, written in 2026 and unchanged),
`15-building.md` (the pipeline this reuses whole), `a-04-building-and-materials.md` (the reference's
roofs and why ours are not those), `a-02-health.md` (the fall-damage number we are **not** using yet
and why).

This is the unit the project exists to prove. It is also, on inspection, mostly wiring: the physics
was built in M1 and switched off.

## 1. What already existed

Grounding first, so the work is the gap and not the whole.

| Piece | State before this unit |
|---|---|
| The support rule — `S_max` on solid below, else `max(4 neighbours) − 1`, 0 collapses | **Built**, `SupportSolver`, bucket queue, incremental with a full solve as its oracle |
| Supported-by-construction trust and its revocation on first edit | **Built** — the ruined-shell mechanic's foundation |
| Worldgen pass 10: full solve, shed-rather-than-complain on a damaged map | **Built**, measured |
| Collapse detection, deferral to the structural phase, chunks marked | **Built**, `SupportSystem` |
| The consequences of a collapse | **`world.Defer(_ => { /* M3: rubble and fall damage */ });`** — an empty lambda |
| Slab storage (`Floor[]`, `FloorStuff[]` on the upper cell), saved and hashed | **Built** |
| Drawing a slab — `ChunkMesher.EmitFloor` → `FloorModule` → the stuff group's slab module | **Built**, and it ignores slab *kind*, so a built floor draws with no rendering change at all |
| `Rubble` terrain, `workToClear` 90 | **Built** |
| Dropping a pawn or an item whose floor has gone | **Built** — as two private methods of `MineJobDriver` |
| `Building_Floor` | Missing; `Buildings.xml` had one entry |
| Marking support dirty on build, demolish or mine | **Deliberately omitted in three places**, each naming U29 |
| A health model to hurt a falling colonist | **Does not exist** |

## 2. The decisions

Eleven questions, owner 2026-09-17. Every recommendation was taken.

| # | Question | Decision |
|---|---|---|
| 1 | Fall damage, with no health model | **Defer the injury, keep the fall.** Things drop a layer and the colonist remembers it; nothing is hurt. |
| 2 | What falls | **Pawns and loose items.** A wall does not fall — it loses its floor and is judged by the ordinary rule. |
| 3 | Does rubble destroy what it lands on | **No.** Terrain and item coexist in a cell. |
| 4 | What does the player click | **The cell they want a floor *in*.** The same lift a wall order gets. |
| 5 | Bridging out over a drop | **Allowed**, up to what support permits — a four-cell overhang, then a refusal. |
| 6 | Wood versus stone as floors | **Same `S_max`.** They differ in work and cost, as walls do. One variable at a time. |
| 7 | Deconstruct that would orphan | **Allowed.** U31's preview is where the danger is shown. |
| 8 | Is a rubble-filled cell buildable | **No — clear it first.** |
| 9 | The ruined-shell case | **In this unit.** It is the test that proves it. |
| 10 | How far into presentation | Sim, the def, and the Build palette row. |
| 11 | Goldens | Deliberate re-bake, with the reason written in `Golden.cs`. |

### Why the fall does not hurt anybody, and what that costs

`a-02` researched the number and recommends `fallDamage = 15 × layers^1.5` blunt on the bottom-facing
parts — one layer bruises, three down, five kill. It is calibrated against a **part tree** with pain,
shock and a 150 HP death threshold, and none of that exists. Applied to a single HP pool the
exponent means nothing, so building one now would be inventing a number in order to throw it away.

**The cost is real and should not be glossed:** "pulling out the wrong pillar hurts somebody" is half
of why collapse is interesting, and today it does not. What lands instead is the whole of the
*mechanic* — the fall, the mess, the lost work — with the injury as a one-line addition the day a
health model exists. `Thought_Fell` is the placeholder that keeps the event legible to the player in
the meantime: a colonist who rode a floor down is in a worse mood for a while.

## 3. What a built floor is

**A roof is a floor is a slab** (`02` §4). One thing, of a material, at the boundary between two
layers, stored on the upper cell. Building one is therefore not a new pipeline: it is
`Building_Floor` going through the same order → deliver → work → raise that a wall goes through,
with a different line in `ConstructionGrid.Raise`.

- **`BuildingDef.slab`** is the whole of the difference. True means the finished thing is written to
  `Floor[cell]` and `FloorStuff[cell]`; false means an edifice in the cell, as before. One bool
  rather than a second pipeline, which is the point of having the tables.
- **`CoreContent.SlabBuilt = 4`**, a new slab kind, is what a colony floor is written as. The three
  existing kinds are all the generator's — `SlabStructural`, `SlabDeck` and `SlabRoof` are stamped by
  `SurfacePasses` and `DepthPasses` — so a fourth is what makes *"deconstruct our own floors, not the
  ruined city's"* answerable at all. It is `PlacedEdifice.Built`'s argument, one level down, and it
  needs no new state: `Floor[]` is already saved and already hashed.
- **Nothing in presentation changes to draw it.** `WorldRenderModel.FloorModule` returns the stuff
  group's slab module for any non-zero floor and never looks at the kind, so a built floor draws as
  the ruined city's floors do, in its own material's tint, the day it is written.
- **Cost 4 stuff, 120 ticks of work**, against a wall's 5 and 135. Slightly less of both, because a
  slab is less material than a 3 m wall and the interesting part of building either is fetching the
  stuff. **These two numbers are the owner's to tune** and nothing derives from them.

### Where a slab may be ordered

`ConstructionGrid.Allows` becomes `Allows(index, building)` and asks a different question of each:

- **An edifice** wants a floor under it, open air, no water, nothing already standing there — the
  rule as it was.
- **A slab** wants the opposite of the first of those: the cell must have **no floor already** and no
  solid terrain beneath it, because a slab goes where there is nothing to stand on. It must not be
  inside solid rock, must not be in water, and — the new one — **the support rule must permit it**:
  `SupportSolver.SupportIfSlabAt(index) > 0`, which is `S_max` over something solid and
  `max(4 neighbours) − 1` otherwise.

That last line is the mechanic made playable. A colonist can bridge four cells out from a wall and
the fifth order is refused with a reason, rather than accepted and collapsed the moment it is
finished. It is the same argument `Allows` already makes about a wall hanging in the air: *refusing
the order is far better than collapsing it afterwards, because the player never gave an order that
could not be carried out.*

**Terrain gains `buildable` a reader.** `TerrainDef.buildable` has existed and been read by nothing;
`Allows` reads it now, and `Rubble` sets it false. That is the whole of "clear the mess first".

## 4. What a collapse does

The solver already finds them and already erases the slab. The deferred lambda in `SupportSystem` —
empty since M1, with `/* M3: rubble and fall damage */` in it — now does four things per collapsed
cell, in this order:

1. **Everything in the cell falls.** `Falling.OutOf(ctx, cell)` — the pawn to the first real floor at
   or below, and any item stack likewise.
2. **The colonist remembers it.** `Thought_Fell`, on anyone who actually moved. Not on a colonist who
   was standing somewhere the collapse merely opened a view of.
3. **The mess lands.** The first cell at or below with a floor becomes `Rubble`, if it was open air.
   Rubble is **not solid** — a colonist stands in it, an item stack shares the cell with it — so it
   obstructs building and nothing else, which is what decision 3 asked for.
4. **Navigation is told.** The collapsed cell has lost its floor and the cell that took the rubble
   has changed; both are marked dirty, as mining marks its own edits.

### `Falling` is an extraction, not new code

`MineJobDriver` has answered "the floor under this cell has gone" correctly since the mining line
landed, including two faults it had to learn: a pawn dropped **one layer** rather than to the first
real floor was left standing in mid-air over a two-deep shaft (measured: two of five colonists spent
12,600 ticks of a 40,000-tick run standing still in the air), and a cell with a climb footprint
counts as standable to navigation but is not somewhere to leave anybody.

Those two methods were private to the mining driver. A collapse asks exactly the same question, and
the *second* caller is where a copy would have been made — so they move to `Falling.OutOf` and mining
calls it. **No behaviour changes for mining**, and that is a test rather than a hope.

The memory is *not* inside `Falling`. A collapse is a frightening event; being asked to step down one
block into a hole you were told to dig is a Tuesday. Mining therefore stays exactly as it was, and
the thought is added by the collapse caller alone.

## 5. Clearing rubble

Decision 8 wants rubble cleared before rebuilding, which means it must be clearable, and it was not:
`DesignationGrid.CanMine` requires `IsSolidTerrain`, and rubble is not solid.

**`TerrainDef.clearable` is the fix, and it is a def flag rather than a rule.** `CanMine` accepts a
cell that is solid *or* clearable. `Rubble` is the only terrain that sets it. The alternative — "any
non-solid terrain with work to clear" — would have offered the player a Mine order on open water,
marsh and the soil band under the city, all of which are non-solid and all of which carry the default
`workToClear` of 100.

This is `vertical-slice.md`'s U28 line *"three speeds by target: breach a slab, clear rubble, mine
rock"*, and the middle one has been missing since mining landed. U29 absorbs it because U29 is what
first produces rubble at run time. Clearing yields **nothing**: the material went into the floor that
fell, and a collapse is not a deconstruction.

## 6. Taking a floor away

A built floor can be deconstructed, refunding half, exactly as a wall is. `CanDeconstruct` accepts a
cell whose `Floor[]` is `SlabBuilt` — never `SlabStructural`, `SlabDeck` or `SlabRoof`, so the ruined
city's decks stay the city's, which is the same line `PlacedEdifice.Built` draws for walls.

It is **not** refused when it would orphan something. Decision 7: the preview (U31) is where the
danger belongs, and a rule that refused every dangerous removal would make the mechanic
un-explorable. Pulling the last support out from under a room and watching it come down is the thing
this unit is for.

## 7. The three omissions, closed together

`ConstructionGrid.Raise`, `ConstructionGrid.Demolish` and `MineJobDriver.MineCell` each carried a
comment saying support is deliberately not marked dirty because nothing collapses yet, and that the
three should be wired together rather than one of them quietly acquiring behaviour the others lack.
They are wired together here. Each marks the cell **above** its edit, because that is the boundary
whose support source changed: a wall raised gives the slab above it full support, a wall demolished
takes it away, and rock mined out does the same.

`PawnContext.Support` is the seam — the solver, optional and null in a bare fixture, exactly as
`Designations`, `Construction` and `Chunks` already are.

## 7a. Where a colonist stands to build one — found by building it

The design above was complete and the feature still did not work: the journey test sat through
20,000 ticks and nobody built anything. **A slab has no neighbours on its own layer to stand on
until there is already a floor up there**, so `FellJobDriver.StandBeside` answered -1 for the first
slab of any storey, the work giver never offered the job, and the order sat there for ever. Nothing
logged, nothing failed — exactly the silent refusal `15-building.md` §6 was written about.

`BuildWorkGiver.StandToBuild` is the fix and it is mining's envelope rather than felling's: beside
it on its own layer first — so extending an existing floor still works from that floor — then
beside-and-one-below, then directly underneath. *A plank goes overhead exactly as a pick does*,
which is the argument mining's `layersBelow: 1` already made. `BuildJobDriver`'s working toil takes
the same envelope, because a stance the work toil then rejects is a colonist who walks to a site and
turns round again.

The deliverer uses the same function, for the same reason: material for a slab has to be put down
somewhere a colonist can reach.

## 7b. Binding the colony to the solver

`SupportSystem` finds the collapses and needed a colony to drop things into. Seventeen places build
one — the world, the screenshot harness, eleven editor probes, four test fixtures — and every one of
them hands it to `ColonyComposition.AddColony`, which is the one place that also holds the
`PawnContext`. So it is **bound there rather than constructed with**: a constructor argument would
have been seventeen edits and seventeen chances to pass null, and this way a colony cannot be
assembled without it. It is the argument that file already makes about the construction grid.

The first version did take a constructor argument, and the consequence was visible immediately: two
tests failed saying nothing fell and no rubble landed, because the one construction site that
mattered had not been told.

## 8. Test procedure

*Fast tier, Sim*, and every one of these is a claim that can fail:

- **The extraction changed nothing.** Mining's existing suite passes unedited, which is what makes
  `Falling` a move rather than a rewrite.
- **A floor can be ordered, fed, worked and raised**, and a colonist then walks on it — the wall
  journey with one word changed, which is the point of the pipeline being one pipeline.
- **A bridge reaches exactly four cells.** Order slabs outward from a supported edge: the fifth is
  refused, and the refusal is `NotPermitted` rather than silence. This is the support rule visible in
  a single test.
- **The ruined-shell case.** On the city map, mine the wall under a stamped slab and the slab comes
  down. The negative control is the same mine with a second wall still holding it.
- **A collapse cascades**: pull the one support from a run of slabs and the whole run comes down in
  one tick, not one slab per tick.
- **What was standing on it fell**, to the first real floor and not one layer, with a memory on the
  colonist and none on a colonist who did not move.
- **The rubble landed, is not solid, and blocks a rebuild** until it is cleared; and clearing it is a
  Mine order that a colonist actually takes.
- **A built floor deconstructs and refunds half; a stamped one is refused.**
- **Nothing leaks:** after all of it, the reservation table is empty and the state hash of a
  save → load round trip matches.

*By hand, and nobody has done it:* build a room with a floor over it, mine the wall out from under,
and watch the floor and whoever was on it come down.

## 9. Open

- **Fall damage.** The number is researched and waiting (`a-02`); it needs a health model.
- **`S_max` per material** (decision 6 took "same for both"). `SupportSolver` takes `maxSupport` as a
  constructor argument, so it is currently per *world*, not per material. Making spans a property of
  what you build with is a real design lever and a real change to the solver's shape.
- **U31, the support preview**, is what makes decision 7 fair to the player. Until it lands, the only
  way to discover an orphan is to cause one.
- **Rubble draws as a terrain colour**, not as a heap. It is in the palette and it is not art.
- **A slab with a hole in it** — `SM_Bld_Base_Floor_Hole_01` — is what a stair or ladder through a
  floor will want, and nothing needs it yet.
- **What the build cursor names when the player orders a floor over a drop.** The picker answers a
  click with the surface under it, which for a pit is the bottom of the pit — so a player standing
  on the rim and clicking into the hole would order a slab on the floor of it rather than at the rim
  they meant. Ordering along an existing edge works, because the cell the cursor lands on is beside
  something solid. This is a cursor question rather than a simulation one: the rule here is settled
  and the interface has to name the cell the player means. **Nobody has pressed Play on it.**
- **`Thought_Fell` is not in the wiki**, because no thought is. The registry covers what a player
  reads as a name; mood thoughts are surfaced as text nobody has designed yet, and the day they get
  a panel is the day the whole thought table wants rows.

## 10. The art

`e-01-module-mapping.md` settled this in Phase 2 and the inventory measured it:

| Use | Prefab | Size |
|---|---|---|
| The built slab | `PolygonGeneric/Prefabs/Base/SM_Bld_Base_Floor_Combined_01` | 2.50 × 0.10 × 2.50 |
| Underside skin, if a ceiling read is wanted from below | `SM_Bld_Base_Ceiling_01` | 2.50 × 0.00 × 2.50 |
| Stair and ladder penetrations, later | `SM_Bld_Base_Floor_Hole_01` | 2.50 × 0.10 × 2.50 |

The pitched `SM_Bld_Base_Roof_*` pieces are **not** floors — 3.25 m tall, a quarter of a metre over a
layer — and stay decorative caps on stamped ruins.

**No catalogue row is needed to play this.** A module with no row falls back to the cell-shaped
primitive wearing the stuff's own tint, which is exactly what `ModuleIds.WallCore` does today, so a
built floor is visible and judgeable the moment it is raised. The row is an upgrade, not a
dependency.
