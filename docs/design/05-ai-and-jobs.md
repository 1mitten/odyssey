# 05 — Pawn AI: needs, thoughts, jobs, reachability and pathfinding

How a colonist decides what to do, and how they get there. Research: `a-01-pawns.md`, `a-03-work-and-jobs.md`, `a-14-bills-stockpiles-inventory.md`, `d-04-pathfinding.md`.

The order of this document is the order of the problem. Movement comes last in the narrative and first in the cost: the D1 benchmark measured naïve layer-aware A-star at **65% of total tick time**, with **1,058 of 1,800 replans exhausting their search budget** on cross-layer targets. That number is why §4 exists and why it is not optional.

## 1. Needs and mood

Needs are scalars in 0..1, updated on a **150-tick interval** with per-band fall rates (a pawn at 20% rest falls at a different rate from one at 80%). The slice carries food, rest and a minimal joy.

Mood is a **difficulty base plus the sum of active thought offsets**, and the *displayed* mood drifts toward that target rather than snapping to it — which is what makes mood read as a mood rather than as a number. Thoughts come in two kinds:

- **Situational** — recomputed from the world, never stored. "In a dark room", "sleeping in the rubble".
- **Memory** — created once by an event, expiring after a duration, stacking up to a limit with a diminishing multiplier per additional copy.

Mental breaks are **mean-time-between-events rolls** while mood sits below a threshold, not a fixed trigger. A miserable colonist therefore breaks *probably soon*, which is both better drama and better design than a cliff edge. The slice implements one break behaviour; the taxonomy is M7.

Every constant here — fall rates, band boundaries, thresholds, durations, stack limits — is a Def field (`04-data-model.md`), not a literal.

**Layer awareness**, and the reassuring finding from `a-01-pawns.md`: only three environment queries need it — the beauty scan, the roofed check, and room membership. Everything else about a pawn is layer-agnostic.

## 2. The job pipeline

Straight from `a-03-work-and-jobs.md`, because the shape is well-proven and there is no reason to be clever:

```
think tree  →  job giver  →  work giver  →  job  →  toils
```

- The **think tree** is traversed depth-first, and the **first valid job wins**. This is an ordered scan, not a global utility argmax. That matters twice: it is far cheaper, and it is predictable, so a player can reason about why a colonist did something.
- A **constant tree** runs on a short cadence and can force-interrupt for reflexes (fleeing, collapse).
- **Work givers** sit on a flat, pre-sorted list ordered by the player's 1–4 priorities. Each scans candidate things or cells and returns a job or nothing.
- A **job** names a driver; the driver runs a sequence of **toils**.

Utility scoring appears in exactly three narrow places (priority-sorter nodes, the player's priority numbers, and per-target scoring inside one scan) rather than as a global architecture. Resisting the urge to make everything a utility system is a deliberate choice.

### Reservations

The only inter-pawn coordination in the system, and it earns its place. A claim is `(claimant, job, target, layer, maxPawns, stackCount)`. It is **tested during the scan**, **claimed all-or-nothing before the toils run**, and **released whenever the job ends** for any reason, including failure.

Check-then-claim is safe here without any locking because the tick is single-threaded by decision (Phase 1 Q6). That is one of several places where determinism-first quietly removes a whole class of bug.

A reservation leak is exactly the kind of fault that a ten-day unattended run surfaces and a two-minute test does not — which is why the slice's definition of done is ten days.

## 3. Stockpiles and hauling

Zones are **per layer** (layer question 6): a set of cells on exactly one layer. Contiguity across a stairwell is meaningless, and capacity belongs to a floor rather than to a volume. The "one warehouse across three floors" case returns as a named **storage group** — several per-layer zones sharing one settings record. Geometry per layer, configuration grouped.

A hauler picks a destination by, in order: **the filter accepts the item → it has space → highest priority → nearest**. Priority orders the *destination*, not the haul queue. Haulers will re-stow from a lower-priority container into a higher-priority one that also accepts the item.

The storage filter record (category allow-set, hit-point range, quality range, stuff set) is **shared with bill ingredients** — one implementation, one set of bugs (`04-data-model.md` §8).

**Stair cost belongs in the region-link weight, not in a special "prefer the same layer" rule.** This is worth stating because the special-case version is tempting and wrong: put the cost in the graph and ordinary distance ordering handles verticality correctly everywhere, for free, including in cases nobody thought about.

## 4. Reachability — answered before pathing, never by pathing

The single most important architectural decision in this document.

A work giver scanning for haulable items must answer "can this pawn get there?" for potentially thousands of candidates per scan. Answering that with A-star is unaffordable, and the benchmark proves it: most cross-layer searches burned their entire 20,000-node budget and returned failure. **Failed searches are the expensive ones**, and an unreachable target produces the most expensive possible search.

The structure, from `d-04-pathfinding.md`:

- **Regions**: chunk-local, **10 × 10 cells within one layer**, never spanning a layer or a chunk. Typed — walkable, door, connector, impassable, hazard — so that doors and connectors are their own single-cell regions and can carry their own rules.
- **Links**: boundary spans joining adjacent regions, plus **portal links** for connectors.
- **Districts**: regions grouped per traverse mode. A district id is an integer, so **`Reachable(a, b, mode)` is one integer comparison.** Not a search. Not a cache lookup that might miss. A comparison.
- **Rebuild**: dirty-chunk-local at a fixed point in the tick, then one district flood over roughly 50,000 graph nodes — orders of magnitude cheaper than flooding 2.5 million cells.

### Rooms are a separate grouping over the same regions

A genuinely useful distinction the research surfaced: **"can walk there" and "air mixes with there" are different questions**, and in 3D they diverge — a sealed hatch stops air but a ladder does not stop walking, and a window stops walking but not heat.

So: **regions are the shared substrate**, grouped two ways. Districts group them for *reachability*. Rooms group them for *atmosphere*. This supersedes the earlier sketch in `02-world-and-layers.md` that treated rooms as a plain per-layer flood fill, and it is a better answer because it leaves the M4 temperature decision open: whether an atmosphere grouping spans a stairwell becomes a tuning question over an existing structure, rather than an architecture rewrite. The slice needs only enclosure, so it uses the substrate and ignores the atmosphere grouping entirely.

## 5. Pathfinding

Two stages, following Factorio's published approach paired with HPA-style caching:

1. **Abstract A-star over the region graph** — reverse-resumable, supplying both a heuristic and a corridor of regions.
2. **Cell A-star constrained to that corridor**, with cached intra-region crossing distances at a deliberately small cluster size.

Rejected, with reasons: **JPS/JPS+** assumes uniform costs, has no natural portal extension, and its precomputation does not suit a world being constantly dug and built. **D\* Lite** carries a per-agent search tree over 2.5 million cells, adds determinism surface, and loses to from-scratch searches when edits are large — which, in a game about demolishing buildings, they are.

### Connectors declare their ends

A connector's Def names **both** ends (stair 2 + 2 cells, ladder 1 + 1, lift one shaft cell per layer). The link is registered when the connector is built or stamped by worldgen, and validated at placement.

This is the deliberate opposite of Cataclysm DDA, which matches stairs at run time by searching for a landing nearby — and whose NPC navigation has been broken by that decision for a decade (`c-cataclysm-dda.md`). We also **do not tolerate a one-layer discontinuity** in path validation; a path that skips a layer is a bug, not a convenience.

Missing floors become explicit **one-way fall edges**. Ladders are single-occupancy, reservable, with a deterministic queue.

### Determinism rules

Non-negotiable, since every milestone gate hashes state:

- **Integer costs only.** One unit = 1/100 of a flat-cell crossing. No floats anywhere in the cost model.
- **Terrain cost is live as of 2026-09-16** (ADR 0009), through `NavGrid.CostClass` and `CostByClass`, which had been wired into `EnterCost` since the pathfinder was written and which nothing had ever written: every walkable step cost exactly 100. Three classes now: clear ground 0, marsh +40, shallow water +200 — so wading is exactly a third of walking speed. Deep water has **no** class, because it is impassable, and a cost that says "very expensive" is a different and worse claim from one that says "not at all".

  The trap, worth stating once: **a cost class belongs to the cell being *entered*.** Wading, that is the water cell itself. Crossing a bog, it is the air cell above the marsh. Written the other way round, marsh is free and nothing says so.

  **Its twin, added 2026-09-17 when per-pawn move speed was designed** (`17-rates-and-stats.md` §4g): **a cost prices the cell, a rate multiplies the pawn's progress, and never the reverse.** A "this colonist is slow in water" factor would be paid twice — once by the planner choosing the route and once by the mover crossing it — and the planner's route would then disagree with the mover's price, which is the failure `HopPriceHasOneOwnerTests` exists to prevent for hops and which nothing guards for terrain. The same confusion has a user-facing form: the tile readout's `walk speed = 100%` is a fact about the **cell**, not about anybody standing on it.
- **Total order in the open list**: `(f, h, cellIndex)` — never a partial order that leaves ties to heap internals.
- **Compile-time neighbour order.**
- **Deterministic region and district id assignment**, and a deterministic flood order.
- **Budgets counted in nodes, never milliseconds.** A time-based budget makes the simulation depend on the machine, which is the same thing as making it non-deterministic.
- **FIFO request queue**, ties broken by agent id.
- If Burst is used, only as **fork-join over independent requests with results applied in request order**.
- **Path checksums fold into the state hash**, so a pathfinding divergence is caught by the ordinary determinism gate rather than by its symptoms three systems downstream.

## 6. Budgets and what is still unmeasured

Per-agent replan budgets are counted in nodes and set from measurement, not taste. The benchmark gives the pre-optimisation baseline: 0.93 ms mean for one replan per tick plus 50 pawns of movement, on a fast machine, with no reachability culling.

### The experiment was run, and it falsified the stated reason

**Corrected 2026-09-15.** This section previously claimed the 1,058 failed searches "were searches for targets that were never reachable", and predicted that a district check in front of the search would make them disappear. The experiment was run at full scale. The measured result:

| | Total | Mean replan | Succeeded | Budget exhausted |
|---|---|---|---|---|
| Naive cell A-star (baseline) | 2,189 ms | 1.216 ms | 830 | 826 |
| Districts + two-stage | 896 ms | 0.498 ms | 1,473 | 77 |

**2.4× faster overall, with budget exhaustion down 91%.** But **the premise was wrong**: only **250 of 1,800 targets (14%)** are district-unreachable, and on a structured rooms-and-doorways world only **6 of 1,800**. The futile searches do vanish exactly as designed and now cost two array reads, but they were a minority all along. What actually removed the 826 failures was the **abstract region stage plus a connector-density-derived heuristic**, not the reachability check.

Isolating the district check alone gives **1.4×** on the random world and **1.0×** on a structured one.

So pathfinding falls from roughly 65% of the old tick to roughly 45%, not to nothing.

**The architecture is still right, for a better-stated reason.** Reachability must be answered before pathing because **every job-giver scan asks it thousands of times per tick** against candidate targets, and it must be free there — two array reads rather than a search. That is a larger and more certain win than the replan saving, and it is the reason to keep it. The replan speed-up comes mostly from the hierarchical search.

The lesson generalises: a plausible causal story attached to a real number is still a guess until it is measured separately.

Still unmeasured, and honestly so: the right chunk and region size for a stamped ruined city (unmeasurable until mapgen exists), whether the cell A-star needs Burst at all (a D1 follow-up), and HPA-style crossing-distance caching under constant editing, for which no published measurement was found.

## 7. Keeping the region graph current without walking the board (HT1, 2026-09-25)

The hardening plan's first unit (`docs/plans/vertical-slice.md` §HT, audit §2a). **The one cost in
the simulation that grows with the board rather than with what is happening on it.**

### 7a. Measured first

`NavGraph.Rebuild` after one mined cell, split by segment (`NavGraph.RebuildTicks`, printed by
`NavGraphStatisticsTests`; mean of 200 rebuilds, the Windows dev machine, CoreCLR, 2026-09-25, the
played map). Only the first two segments are local to the edit; the other four walk every region or
link on the board:

| Board | Rebuild | Flood | Links | **Portals** | **Adjacency** | **Districts** | **Estimate** |
|---|---|---|---|---|---|---|---|
| Standard 120 × 120 × 16 | 0.330 ms | 0.027 | 0.092 | 0.083 | 0.018 | 0.102 | 0.003 |
| Large 180 × 180 × 24 | 0.733 | 0.034 | 0.100 | 0.196 | 0.048 | 0.330 | 0.008 |
| Huge 240 × 240 × 16 | 1.233 | 0.041 | 0.118 | 0.338 | 0.076 | 0.623 | 0.016 |
| Scale target 250 × 250 × 40 | **1.795** | 0.048 | 0.121 | 0.422 | 0.126 | **1.000** | 0.018 |
| Ruined city, scale target | **3.454** | 0.044 | 0.148 | 0.355 | 0.254 | **2.559** | 0.043 |

So the audit's "districts" is half of it on the natural map and three quarters on the city, and the
**portal table is the second cost** (a dictionary cleared, every portal link re-added and sorted). The
local work is 0.17 ms: **the 0.2 ms target means all four global passes go**, not districts alone.

### 7b. The decisions

- **Districts are repaired, not re-flooded — and not by the audit's option 1 as written.**
  "Re-flood the components the edit touched" is the full pass again on a real board, because nearly
  every region on the surface is one district: the touched component *is* the board. What is done
  instead:
  - **A split** of an old district can only happen if the regions that bordered the edit — the
    surviving ends of every link freed, and the new regions in the re-flooded blocks — stop reaching
    each other. (Any path between two surviving regions either avoids the edit, or enters and leaves
    it through border regions.) So a search starts from each border region, searches that meet are
    joined (union-find), and the search stops the moment each old district's borders are one group.
    The ordinary edit meets within a few steps, through the block just rebuilt. A search that runs
    out of frontier first has found a closed piece: it gets a fresh id, at the cost of that piece.
  - **A merge** — a new link joining two old districts — relabels the **smaller** one, by the member
    counts kept per district.
  - **Ids are kept per mode with a free list and a member count**; nothing compares them for order,
    and they are **not in the state hash** (`NavGraph.ContributeTo`, which would have hashed them, has
    no caller; its comment said otherwise). `DistrictCount` stays the number of live districts.
  - The full `RecomputeDistricts` stays: the first build, a load (`MarkAllDirty`) and the oracle.
- **Adjacency keeps its order exactly**: each region's incident links in ascending link id, as the
  CSR built them — the abstract search walks them in that order, so a path, and with it every golden,
  depends on it. Stored as a slotted CSR (each region owns a run of slots with room to grow), so
  `AdjacencyStart/Count/Link` keep their meaning and no caller changes. Only freed and built links, and
  freed and allocated regions, touch it.
- **Portal edges are maintained per cell** as portal links are freed and built, each cell's chain in
  ascending target cell as before, from a pool with a free list. **The estimate** counts portal links
  as they come and go.

### 7c. The oracle

`NavGraph.DerivedTablesDisagree()` rebuilds all four tables from scratch **inside the same graph** —
where ids agree, unlike a second graph — and says what differs: adjacency order exactly, portal chains
exactly, districts as a partition (a bijection between the two labellings), the live district count
and the estimate. It runs after every round of `IncrementalRebuildTests`' randomised edits and of a new
randomised run on generated maps, beside the existing checks (the id-independent fingerprint against a
graph built from scratch, and every path identical). Each piece was built with the oracle failing
first when it was withheld.

### 7d. Done when

The scale-target rebuild after one mined cell is **under 0.2 ms** on this machine, with every path
checksum, every golden and `NavGraphStatisticsTests`' region counts unchanged.

### 7e. Measured after

The same arm, the same machine, 2026-09-25, alone (no Unity process, CPU at 5 %):

| Board | Rebuild before | **after** | Districts before | after | Local work (flood + links) |
|---|---|---|---|---|---|
| Standard 120 × 120 × 16 | 0.330 ms | 0.294 | 0.102 | 0.021 | 0.263 (first arm; the JIT) |
| Large 180 × 180 × 24 | 0.733 | **0.156** | 0.330 | 0.014 | 0.125 |
| Huge 240 × 240 × 16 | 1.233 | **0.168** | 0.623 | 0.016 | 0.132 |
| **Scale target 250 × 250 × 40** | **1.795** | **0.184** | 1.000 | 0.005 | 0.127 |
| Ruined city, scale target | 3.454 | **0.263** | 2.559 | 0.037 | 0.176 |

**Done: 0.184 ms at the scale target, under the 0.2 ms the plan set.** Every golden, the combat
gate's hashes and every path checksum unchanged; `NavGraphStatisticsTests`' region counts unchanged.
What is left is the local work, and it now costs about the same on every board.

**The tick benchmark's edit arm**, on its room lattice (nine times a generated map's regions, so the
stress case, `28-map-size.md` §2), `main` against this branch in one sitting:

| Edit tick, lattice | `main` | HT1 |
|---|---|---|
| Standard | 7.48 ms | 3.08–4.18 |
| Large | 20.27 | 7.89–8.38 |
| Huge | 29.09 | 12.57–13.97 |
| Scale target | **63.77** | **11.16–17.57** |

On the lattice the repair's search costs more (1.45 ms per rebuild at the scale target): its seeds sit
in rooms whose shared district is reached round walls, through doorways, so the searches travel
before they meet. Still a thirtieth of what the full flood cost there.

### 7f. What the oracle caught

- **A first draft's stopping rule was wrong.** It searched until no two live groups shared an old id
  and every group carried one. Two groups with *different* ids can be joined through the edit itself —
  the gap reopened between two halves of a board — and stopping left them as two districts
  (`OpeningTheGapMergesTheTwoHalves`: "2 districts, expected 1"). The fix is the reason a merge can only
  happen through something new: the ends of every built link are joined before any search, after which
  distinct groups really are distinct components.
- **A new region's id came out of `Array.Resize` at district 0**, which is somebody's district; a
  recycled id had been reset as it was freed. On the played map, edit 1: "10 districts, expected 11".
- **The small fixtures never reached the repair.** A rebuild dirtying a quarter of the board takes the
  full pass, and the maze fixture and the first split and merge tests were small enough to do so every
  time; withholding the merge relabel passed them. They were enlarged until the repair is what runs,
  and then each withheld part failed.
- **One part could not be made to fail**: the ends of a freed link as seeds. Dirtying the edited cell's
  neighbouring blocks already re-floods every surviving region at the end of a link that did not come
  back, so those regions are new and seeded anyway. Kept as a second line, and written down so it is
  not mistaken for a tested rule.

### 7g. Found on the way, and not this unit's

With the navigation local, the busy tick's board-scaled cost is elsewhere. A throwaway probe timing
every world system on the scale-target lattice, one cell mined a tick: **Enclosure 8.04 ms** (the
temperature rooms, `28-temperature.md`), **Needs 4.16 ms**, Navigation 2.97 ms, everything else under
0.03. At rest the whole tick is 0.15 ms, so both scale with edits. The enclosure solve arrived after
the audit, which is why the audit's 1.19 ms edit tick was out of date by a factor of fifty on `main`.
Recorded as **HT10** in `docs/plans/vertical-slice.md`.
