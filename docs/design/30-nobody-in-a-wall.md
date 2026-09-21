# 30 — Nobody in a wall: who may be standing where a building goes

**Status: built, tested, not yet played.** Written 2026-09-21 on branch
`claude/build-appearance-and-entombment`, out of an owner report:

> *"when colonists build a wall — sometimes they get stuck inside the wall itself. This should never
> and can't happen."*

Read `15-building.md` first — this is a rule added to the last line of the build pipeline it
describes — and `docs/bug-patterns.md` for the family this belongs to.

---

## 1. What was actually wrong

A build site is **walkable right up to the instant the thing exists**. That is deliberate and it is
what makes a half-built room usable: material is carried across it, the builder reaches the far side
of it, and a corridor with a door ordered in it is still a corridor. `ConstructionGrid.Raise` then
turns the cell blocking in one step — and until 2026-09-21 it asked nothing at all about who was
standing there.

The result is a colonist inside a wall, and inside is **permanent**:

- the cell is no longer walkable, so no path can *start* in it — every job the colonist takes fails
  at its first step;
- and no path can *end* in it, so nothing can route them out;
- nothing swept the world for pawns in impossible places, so the state simply persisted.

The owner's answer to "does it resolve itself" was **"stuck forever — never gets out"**, which
matches exactly.

**Who it happens to.** The builder stands *beside* what it builds (`BuildJob.StandToBuild`), so the
victim is almost always somebody else: a hauler crossing the site, a colonist walking to a meal. The
builder can only be caught by a wall somebody *else* finishes.

## 2. The rule, in three parts

The owner chose the three-part answer on 2026-09-21 ("refuse + push out + safety net"). Each part
answers a case the others cannot, and they are listed here in the order they act on a given tick.

| | Part | Where | Answers |
|---|---|---|---|
| 1 | **The detour** — a cell with a blocking building ordered in it costs `MoveCost.SiteDetour` extra to walk into | `NavFlags.BuildSite`, set from `ConstructionGrid.Set` | Keeps passers-by out of the cell in the first place, so the other two parts are rarely reached |
| 2 | **The guard** — a raise that would build over somebody either waits or moves them | `ConstructionGrid.Raise` → `MakeRoom` | No wall is ever raised around a person |
| 3 | **The sweep** — any pawn standing in a filled cell is moved to the nearest cell it can stand in, every tick | `TrappedPawnSystem` | The colonists already walled up in existing saves, and every future way a cell can close over somebody |

`PawnEviction` holds the one rule about *where* a displaced colonist goes, because parts 2 and 3
both need it and two copies would drift.

### 2a. The detour is a deterrent, never a rule

A site is **never made impassable**. That was the fourth option and it is wrong: a builder can be
inside the room they are walling, a doorway under construction can be the only way through, and a
site that cannot be crossed is a colony that strands its own haulers halfway round a plan.

`SiteDetour` is **120** against an orthogonal cell of 100 — slightly more than a whole extra cell of
walking. That is the relation that matters and it is deliberate on both sides:

- a **one-cell detour around** a site is preferred, so a colonist crossing a room goes round the
  wall somebody is putting up;
- a **two-cell detour is not**, so nobody walks the long way round a building site for no reason;
- and where there is no other route at all, the cell is still crossed.

It is **only on what will block the cell.** A floor, a bed, a ladder and a stair order carry no
detour — you walk over them, and charging for them would be a colony paying to cross its own
furniture. `ConstructionGrid.Set` asks `BuildingDef.blocking`, which is the same field the built
thing uses.

**Two readers, one price.** `NavGrid.EnterCost` prices the step the mover walks and
`NavGraph.StepCost` prices the same step for the abstract region search. They are mirrors of each
other and they must agree, for the reason `HopPriceHasOneOwnerTests` exists: a disagreement makes
the search plan a route the walk then pays a different price for, and it fails silently.
`EntombmentTests.TheSiteDetourIsPricedTheSameByBothReaders` is the guard.

### 2b. The guard: wait for a walker, move a loiterer

Two cases, because the two are genuinely different:

| Who is in the cell | What happens | Why |
|---|---|---|
| Somebody **with a path** — walking through | The raise is **refused**. Work and material stay banked; the last blow lands again a moment later | They will be gone in a second of their own accord, and a shove the player can see is exactly the cost being avoided |
| Somebody **standing still** | They are **moved** to the nearest cell they can stand in | A colonist idling in the cell is there for as long as the order lives. Waiting for them is the deadlock the owner asked not to have, and one cell of shove is cheaper than an order the colony can never finish |
| Somebody standing still with **nowhere to put them** | The raise is refused and asked again later | Enclosed in solid world on every side; inventing a teleport across the map would be a worse answer than the order waiting |

**The hammer is held before the roll, not after.** `BuildJobDriver` asks
`ConstructionGrid.CanRaiseNow` *before* it applies the last stroke of work, and simply returns
`Ongoing` when the answer is no. That ordering is the whole reason `CanRaiseNow` exists as a public
question separate from `Raise`: a raise refused *after* the success roll would mean rolling the
same wall twice, and a colony that politely waits a second for a passer-by would pay for the
courtesy in botched walls.

**And the retry closes the window the check cannot.** `CanRaiseNow` is asked in the pawn phase;
the raise itself happens in the deferred phase at the end of the same tick, and movement runs in
between — so a colonist can step into the cell after the check and before the wall. `Raise`
therefore reports whether it was refused, and `RaiseWhenClear` asks again on the next tick, naming
the order it was rolled for so a cancelled or replaced site is dropped rather than built with the
old dice. **The success roll happens once**; the retry is only what carries its result into the
world. Without it the site would sit finished and unraised until a work giver offered it again, and
the next builder's first stroke would roll for it a second time.

### 2c. The sweep is a statement about the world

`TrappedPawnSystem` runs in `TickPhase.WorldSystems` at order 25 — after navigation has rebuilt
(20), so the flags it reads are the ones last tick's edits produced.

It evicts on a **narrow** test: the cell must hold something solid or blocking that this pawn cannot
enter. "Not walkable" on its own would be far too wide — a cell with no floor, deep water and a
doorway are all things a colonist may legitimately be in the middle of, and evicting out of those
would be a teleport in answer to a situation that was never wrong.

It costs one flag read per pawn per tick and does nothing at all in the overwhelming case. It does
not scale with the board.

## 3. What this deliberately does not do

- **No damage, no crushing.** There is no health model (`CLAUDE.md`, known gaps), so "the wall
  crushes whoever is in it" is not available to be chosen. When health lands, this is the place
  that will offer the choice.
- **No pushing out of the way as a general mechanic.** Colonists do not shoulder each other aside;
  `PawnEviction` acts only where a person and a solid thing want the same cell.
- **Items are not evicted.** A stack of wood inside a raised building is a separate rule that
  already exists (`needsClearCell` holds a site's cells against items from the moment it is
  ordered, `15-building.md`).

## 4. How to test it by hand

| Test | Look for | A wrong answer looks like |
|---|---|---|
| Order a wall across a colonist's route and watch the last blow land | they finish the wall and nobody is inside it | a colonist standing in the wall, or a visible jump when it completes |
| Order a wall in a doorway of a room with colonists in it | they still cross the cell while it is a site, and the wall completes when the last one is clear | the room reads as sealed before the wall exists, or the order never finishes |
| Load a save from before this branch with somebody walled in | they step out on the first tick | they stay in the wall |

## 5. The other half of the report: how long a built thing takes to appear

The same session carried a second report — *"there is about a second or 3 delay when the object
appears when it's built"* — and the answer to it lives in `06-rendering-and-camera.md` §6c.8 rather
than here, because it is a rendering fault and not a construction one. Two things came out of
measuring it (`BuildAppearanceTests`):

- **The publish seam is innocent.** A raised wall is in the render mirror on the very next tick's
  publish and drawn on the frame after that. Whatever the seconds are, they are not the sim, the snapshot or the mesher.
- **The whole board was being re-meshed on every edit**, because `WorldRenderModel.Version` was one
  number for all of it. One wall cost 12.53 ms in the frame after the raise against 0.7 ms either
  side; per-chunk versions make it 3 chunks and 1.73 ms. That is the *glitch* in the report, and it
  is fixed.

## 6. The state hash

Nothing here is saved. The nav flag is derived from the site, which is already saved and hashed, and
is re-applied by `ConstructionGrid.Load` because loading goes through `Set` like everything else.

**The goldens did not move** — the Long tier was run before and after and is identical on all three
seeds. That is a measurement and not an assumption: the baked runs are on boards where no colonist's
route crosses a blocking site, so neither the detour nor the guard changes a decision in them. The
day one does, the re-bake is the ordinary one and this paragraph is what says the difference should
be route choice and nothing else.
