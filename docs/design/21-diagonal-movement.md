# 21. Diagonal movement

*Built 2026-09-18 on `claude/natural-movement`. Owner's report: colonists look "a bit square in
movement". This is the first half of the answer; per-colonist walk variance is the second and is
`22-walk-variance.md`.*

**Status: built, fast tier green (677 Sim, 358 Hud), three golden `Simulated` hashes re-baked.
Nobody has pressed Play on it.** Whether the routes stop reading as square is the owner's to judge
and nothing here can settle it.

---

## 1. What the squareness actually was

The intuitive diagnosis is that a 4-connected path is a staircase, and it is **wrong** — that was
measured and falsified on 2026-09-17, before this work started (`WalkHeadingMeasurementTests`, and
the journal entry for that day). A 30 × 30 diagonal walk came out at **60 steps and 5 turns**. On a
4-connected grid every monotone path between two points costs exactly the same, so the search is
free to break ties however it likes, and it spends that freedom on long straight runs.

So the route was never a zigzag. It was **a few long axis-aligned legs joined by right angles**,
which is what reads as square, and which drawn path-smoothing would not have touched: there were
only five corners in a journey of sixty steps to smooth. `CLAUDE.md` already carried the warning
**"do not 'fix' path smoothing on this theory"**, and that warning still holds. The route itself
had to be allowed to go diagonally.

## 2. This closes a designed-for gap rather than adding a feature

`MoveCost.Diagonal = 141` has existed since the cost table was written, carrying the comment
*"Reserved. The MVP search is 4-connected"*, with **no references anywhere in the codebase**.
`docs/research/d-04-pathfinding.md` §"Cost model" priced the step at 141 for 3.54 m. There is no
ADR, no design section and no rejection on grounds of look or cost — the only record was a deferral
row in `docs/plans/overnight-queue.md` ("it changes every path and every golden hash, and it is a
feature"). `PathFinder`'s own class comment named the three places the change would have to land,
and that comment was the specification this followed.

## 3. The corner rule — the owner's decision, and what it really buys

**A diagonal is refused unless *both* flanking cells are enterable.** Owner's decision, 2026-09-18,
against the alternative from `d-04` ("forbidden when both flanking cells block", which permits
slipping past a single wall corner).

`d-04`'s line is annotated in place as overruled.

**The reason first written down for this was wrong, and it is corrected here rather than quietly
dropped.** The argument on the day was that only the strict reading keeps a walled room sealed.
It does not, and `NeitherReadingOfTheCornerRuleCanChangeWhatIsReachable` is what falsified it:

> If one flank is open, then that open flank *is* an orthogonal way round — `from → flank → to`,
> two legal steps. So a diagonal that either reading permits joins two cells that were **already
> mutually reachable**. Neither reading can unseal a room, and neither can seal one.

What the strict rule actually buys is **the look**: a colonist is never drawn clipping through the
corner of a wall, and no route is quoted at 141 for a move that squeezes through masonry. That is
reason enough. It is not a connectivity guarantee and must not be cited as one.

## 4. The consequence that shrank the whole unit

Because 8-connectivity adds **no reachability anywhere**, the region graph did not have to change
at all — no 8-connected region flood, no block-corner zone, no new entries in
`CollectAffectedZones`, no district recompute. The plan for this unit assumed all of that and it
turned out to be unnecessary. Regions, links, districts and zones are byte-identical.

The evidence, and it is strong: **all three golden `Generated` hashes held.** They fold in
`NavGraph.ContributeTo` and `StructureFingerprint`, so a single changed link would have moved them.
Only the three `Simulated` values moved, which is exactly "worldgen untouched, behaviour changed".

**If a later change ever makes a diagonal reachability-creating** — a rule that permits a diagonal
with a blocked flank, a diagonal hop, a diagonal fall — that argument collapses and the region
flood has to go 8-connected with it. The test named above is the tripwire.

## 5. What changed, by file

| File | Change |
|---|---|
| `NavGrid.cs` | `DiagonalAllowed(flankX, flankZ, mode)` — the one statement of the corner rule; `DiagonalAllowedBetween(from, to, mode)` for callers that have two cells and are not in the hot loop; `EnterCost(index, mode, diagonal)` — the one owner of a step's price; `MoveCost.DiagonalExtra`. |
| `PathFinder.cs` | Four diagonal neighbours after the four orthogonals, in fixed order; `RelaxDiagonal`; both heuristics octile; the abstract estimate capped (§6). |
| `NavGraph.cs` | `IsLegalStep` accepts a diagonal, asking `NavGrid` for the rule. `IsHop` and `IsFallStep` stay orthogonal-only — a diagonal hop and a diagonal fall are separate features and neither was asked for. |
| `MovementSystem.cs` | `StepCost` prices a diagonal through `NavGrid.EnterCost(..., diagonal:)`. |

**The price has one owner and a test enforces it.**
`HopPriceHasOneOwnerTests.OnlyOneFileDecidesWhatADiagonalCosts` fails the fast tier if any file
under `Assets/Odyssey/Sim` other than `NavGrid.cs` names `MoveCost.Diagonal` in code. This is the
hop's lesson applied before it could bite twice: the planner and the mover both price the same step,
and the hop cost two rounds of debugging by having that arithmetic in two places. `DiagonalExtra` is
deliberately outside the guard — it is the heuristic's form of the number and decides no step's
price. **Both guards were verified by introducing a deliberate second owner and watching them fail.**

## 6. The heuristic, which is where this nearly failed quietly

The first working build walked the 30 × 30 diagonal in **53 steps with 12 turns** — worse than the
60 steps and 5 turns it replaced. The feature was failing silently.

**Measured cause:** the abstract region distance is a sum of region link costs and every one of
those is priced orthogonally, so it answered **6,600** for a journey a diagonal walk does for
**4,230** — a 56% over-estimate, which costs A* its admissibility. Before diagonals the same
estimate was only 10% over (6,600 against 6,000), which is why nothing had ever shown.

**The fix is one line:** `Math.Min(abstractDistance, octile)`. Octile is a true lower bound on an
8-connected grid, so the smaller of the two is admissible where the abstract figure alone is not.
The same walk is now **30 steps and 0 turns**.

**The worry about it was wrong, and measuring is what settled it.** The fear was that capping would
throw away the abstract stage's whole benefit — the case where the goal is one layer down and the
stair is sixty cells away, where a straight-line guess is useless. Two sweeps of 400 journeys:

| World (120 × 120 × 6) | Path cost | Node expansions |
|---|---|---|
| Rooms and doorways | **3.5% cheaper** | **19% fewer** (3,140 → 2,535 a path, budget 6,000) |
| 35% noise | **3.1% cheaper** | **11% fewer** |

Better on both axes, on both worlds. An over-estimating heuristic does not merely pick worse routes,
it orders the open list badly and re-expands. `Manhattan` carries `LayerChangeHint` for the vertical
case, and both benchmark worlds are six layers deep.

## 7. Two tests that were not tests, and how they were caught

Recorded because both are the same failure and the project keeps meeting it.

**`ForbidIntentTests.AForbiddenThingIsNotHauledUntilAllowed` was a race for scarce shelf space.**
It counted *stocked salvage* and this fixture has **nine stockpile cells of which three are free and
two loose salvage items** — so it turned on whether salvage or rations won those three slots.
Diagonals changed `PawnContext.Distance`, rations became the nearer haul, they took all three, and
the test failed with the colony hauling perfectly well and rather more of it. **Hauling was traced
before the test was touched**: jobs given, items carried for 719 ticks, the driver reaching its last
toil, not one path dropped, and the final stockpile full. It now counts what is still lying loose,
which is the property the file is actually about.

**Two of the new tests ran on an empty board.** `Scatter` wrote `cells.Flags` on a graph that already
existed and then called `Rebuild`, which rebuilds *dirty* blocks — and nothing had been marked dirty,
so not one wall reached the nav grid. Caught by printing the count of permitted diagonals and
reading **1,936**, which is 22 × 22 × 4: every candidate on the board. The helper now scatters before
the graph is constructed, and the test asserts a non-zero count of corner-squeezes so it cannot go
vacuous again.

> **The reusable rule, and it is `docs/lessons.md`'s already:** a test that has never been seen to
> fail is not yet a test. Every claim in this unit was checked by breaking it on purpose — the corner
> rule relaxed to `d-04`'s reading, a second owner introduced for both prices, the usability of each
> guard confirmed by the failure it produced.

## 8. What is not done

- **Diagonal hops and diagonal falls.** `IsHop` and `IsFallStep` remain `dx + dz == 1`. Neither was
  asked for and both would need the region-graph argument in §4 re-made.
- **`ChunkRenderer.FacingOf`** — the instanced crowd used for distant colonists **snaps** facing
  where the live figures ease it. That predates this and diagonals make it less visible, not more.
  Noted so nobody attributes it to this change.
- **Nobody has pressed Play.** `Logs/walk-heading.txt` says 30 steps and 0 turns, which is the
  objective half. Whether a colony of five now reads as moving naturally is the other half, and it
  is why `22-walk-variance.md` exists.

## 9. Performance — measured, because doubling the branching factor demands it

The owner asked for this line to be as fast as possible, and the worry is the obvious one: the
horizontal branching factor went from four to eight, so every expansion now does twice the
neighbour work.

**It is a net win, on the path the game actually runs.** `PathingBenchmark`, 250 × 250 × 40,
1,800 recorded requests, arm C — "two-stage + district check", which is the shipping configuration:

| | 4-connected | 8-connected | |
|---|---|---|---|
| **Rooms and doorways**, mean | 0.4249 ms | **0.3726 ms** | −12.3% |
| cell expansions | 1,428,377 | **935,714** | −34.5% |
| worst request | 3.328 ms | 3.732 ms | +12% |
| **35% noise**, mean | 0.6546 ms | **0.5995 ms** | −8.4% |
| cell expansions | 1,812,116 | **1,417,654** | −21.8% |
| worst request | 12.953 ms | **9.316 ms** | −28% |

A third fewer expansions more than pays for twice the neighbours per expansion. The cause is §6: an
admissible heuristic orders the open list properly and stops re-expanding, and the cap is what made
it admissible. **The one number that went the wrong way is the structured world's worst single
request, +0.4 ms** — still inside the 6,000-node per-request budget, and the noise world's worst
came *down* by a quarter.

**Two smaller things fell out of the same runs.**

`Manhattan` re-derived the **goal's** x, z and y on every call — four integer divisions per
heuristic, on the hottest arithmetic in the unit, for a value fixed for the whole search. Hoisted
into `CacheGoal`, called once in `SearchCells`. Measured on its own, with everything else held:
**770.4 → 728.4 ms** on the structured world, **identical cell expansions**, which is the signature
of a change that alters cost per node and nothing else. `CellHeuristic` lost its `goal` parameter in
the same change, so nothing can pass it a goal the cache does not know about.

The searches that used to **fail** now succeed: `ok` went 1,799 → 1,800 on structured and
1,796 → 1,797 on noise. That is not new connectivity — `district_unreachable` is 0 in both, and §4
says why it cannot be. It is the better heuristic finding, inside the same node budget, a path the
old one gave up on.

**One honest caveat.** `PathingBenchmark`'s two assertions — that the two-stage search beats the
naive one — **fail on both arms and failed before any of this**: on the 4-connected baseline,
naive is 0.128 ms against two-stage's 0.655 ms on noise. They are `[Explicit]`, so they run in no
tier and blocked nothing, and they are not this unit's to fix; but nobody should read them as
green. What this section claims is the *relative* change, measured with the same harness on both
sides, and that claim is unaffected.
