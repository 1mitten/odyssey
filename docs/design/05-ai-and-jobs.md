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
