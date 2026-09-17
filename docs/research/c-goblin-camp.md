# C — Goblin Camp

## Question

OQ-34 (Lane C). Goblin Camp is an open-source colony sim in the Dwarf Fortress mould. Establish its
licence, copyright and project status first. Then study, **for technique only**, how it represented
and dispatched work, how items found their way into storage, how priorities and reservations
worked, and what the design got right and wrong — so that our own stockpile and hauling work starts
from somebody else's experience rather than from nothing.

## Findings

### Licence, copyright and project status — read this before anything else

- **Licence: GNU GPL version 3 or later**, for code and for the bundled media. Every source file in
  the archival tree carries the standard GPLv3 header. The bundled console library is under its own
  separate permissive licence.
- **Copyright: Ilkka Halila**, dated 2010–2011 in the file headers. He was the sole original author,
  writing as "Generic Container".
- **Status: the original project is dead.** The last original release was version 0.21 in July 2012.
  Three descendants exist, none of them the original:
  1. An **archival GitHub copy** (`TheCatPlusPlus/goblin-camp-legacy`), the 2012 code lightly
     modernised to compile with C++17, CMake and vcpkg, with the old Python scripting replaced by
     JSON. It was **archived read-only in July 2023** and is explicitly frozen.
  2. A **maintained GPLv3 fork**, *Goblins' Lot* (Nikolay Shaplov, from 2019), renamed because the
     original author reused the old name commercially. Still receiving commits and occasional
     releases.
  3. A **closed commercial remake** on Steam by Korppi Games, of which Halila is now CEO/CTO. It
     shares the name and the lineage, not the codebase, and it is **not** open source. Anything
     written about "Goblin Camp" after 2023 is almost certainly about this, not about the code
     studied here.

**What GPLv3 permits, and why it changes nothing for us.** GPLv3 would let us copy the code
verbatim *if* Odyssey were itself released under GPLv3 and its source distributed. Odyssey is not,
and will not be. But that is not the reason for the rule, and a future reader should not imagine
that a more permissive licence would have changed the answer. **Odyssey's rule is: read for
technique, never copy.** No source line, no data file, no identifier, no name and no flavour text
from this project enters our repository, and nothing in this file is a transcription. If tomorrow
somebody re-released the whole thing under MIT or into the public domain, the rule would still
stand — we want our own design, in our own vocabulary, that we can debug because we built it.
Everything below is a description in our own words of a mechanism observed in the public archive.

### Jobs and stockpiles

**A job is a script of small steps, not a single verb.** The unit of work is an ordered list of
elementary steps — move here, pick that up, move there, put it in that container, use this thing,
dig, fell, fill, pour, wield, and so on — with each step naming a target position and optionally a
target entity or an item category. A worker executes the steps in order and reports, per step,
success, non-fatal failure, fatal failure, or "still working". That is the whole vocabulary between
the job and the worker. The important consequence: **hauling, building, crafting and eating are the
same data structure.** "Store this in a stockpile" is four steps; "fell that tree" is two; "fill a
barrel from the pond and store it" is seven. There is no separate hauling subsystem, because hauling
is just the steps that every other job also uses.

**Dispatch is a global optimal assignment, not a per-worker scan.** This is the single most
interesting thing in the codebase. Idle workers register themselves on a waiting list rather than
each going looking for work. Once per dispatch pass the manager walks its queues from the highest
priority band down, takes up to twenty assignable jobs and all the waiting workers, builds a square
cost matrix of worker × job, and solves it with the **Kuhn–Munkres (Hungarian) algorithm** for the
best overall matching. The cost of a pairing is essentially a large constant minus the distance from
the worker to the first step's target, with a substantial penalty applied when the job requires a
tool the worker is not already holding; impossible or padding cells get a floor value. The twenty-job
cap is there in a comment explicitly because large matrices were measurably slow.

This is a genuinely different stance from the usual one. The common design — the one we would
otherwise reach for — is "each idle pawn searches the job list for the best job for itself", which
is greedy and produces the classic pathology of two workers crossing paths to take each other's
obvious task. A global matching removes that pathology by construction. The price is that dispatch
becomes a batched, periodic, O(n³) step rather than a per-pawn incremental one, which is why the
batch had to be capped.

**Two worker classes only.** A job is flagged menial or not; workers are flagged expert or not; the
two populations are matched in two separate matrices and never cross. That is the entire skill model
as far as dispatch is concerned. There is no per-skill suitability term in the cost matrix and no
per-pawn work-type priority table — the player's control over who does what is essentially the
menial/expert split plus the job's own priority band.

**Four priority bands, checked strictly in order.** Jobs live in one of four lists by priority
(very high, high, medium, low), plus a fifth "waiting" list for jobs that are blocked. Dispatch
drains the bands top-down and stops when workers run out, so a low-priority job simply never runs
while higher-priority work remains. Priority is set when the job is created, not by a player-facing
matrix.

**Prerequisites and parents make a dependency graph.** A job may hold weak references to
prerequisite jobs and to a parent job. A job whose prerequisites are unfinished is created paused and
parked on the waiting list; the manager's periodic pass unpauses it when they complete and promotes
it to the live queue. Notably the pass also works *backwards*: a blocked job that is not itself
somebody's prerequisite will unpause its own prerequisites, so demand pulls its dependencies into
existence rather than the player having to sequence them. Failure propagates upwards: a failed job
fails its parent.

**Reservations are held by the job and released by the job's destructor.** This is the cleanest idea
in the design and the one we should copy in spirit. A job can reserve:

- **entities** (the specific item being hauled, so two haulers cannot claim the same log),
- **a spot** — a stockpile, a tile within it, and the item type destined for it,
- **space inside a container**, by bulk, so two haulers cannot overfill one barrel,
- **a connected entity**, e.g. the tree or workshop the job belongs to, which is told to cancel its
  job if the job dies,
- **ground marks and map markers**, for designations and overlays.

All of these are undone in one place, when the job object is destroyed. Because the job owns every
claim it made, there is no path by which a cancelled, failed or abandoned job leaks a reservation —
the leak class that plagues hand-rolled reservation code simply does not arise. Cancelling a job
also detaches it from its worker, pauses it, and moves it back to the waiting list rather than
destroying it, so a job interrupted by a wandering worker is retried rather than lost.

**Attempts are bounded.** Every job carries an attempt counter and a maximum. Submitting a job that
has exhausted its attempts routes it to a failure list instead of the queue, and the periodic pass
fails those in a batch. Hauling jobs are created with a **single** attempt — if the situation has
changed by the time somebody gets there, the job dies rather than retrying forever. Tree felling, by
contrast, is created with a large attempt budget. This is a per-job-kind policy knob, and it is the
mechanism that stops a permanently impossible job from being re-dispatched every tick.

**Tool scarcity is modelled explicitly.** Jobs that need a tool of a given category are tracked in a
per-category list. Before an assignment pass the manager computes, per tool category, how many more
such jobs may be assigned: the number of tools the colony owns minus the number already out on
tool-requiring jobs. Jobs beyond that count are not even offered to the matcher. The stated reason is
that it is pointless to assign more tool jobs than there are tools.

**Stockpiles are zones made of per-tile containers.** A stockpile is a construction covering a
rectangle-ish region, with a container object per tile, an allow/deny flag per item category
(hierarchical: toggling a parent category can cascade to children), an optional numeric limit per
category, running amounts per category, a reserved flag per tile, and a per-tile colour used only
for drawing. Items nest: a tile holds a barrel, the barrel holds the liquid. The pile is itself a
listener on its containers, so adding or removing an item updates the pile's own counts without
anyone having to remember to.

**The pile can grow itself.** When asked for a free position and every tile is occupied or reserved,
the stockpile looks at the ring of tiles just outside its bounds, keeps those that are buildable and
of a permitted terrain and adjacent to the pile, and expands into one of them at random. "Is this
pile full?" is therefore answered as "is there no free tile, no non-full container that accepts this
item, *and* nowhere to expand to". A pile with room to grow is never full. Free-tile search itself is
a bounded random probe (a few random tiles, proportional to the pile's size) falling back to a full
scan — a deliberate cheap-first, correct-second pattern.

**Choosing the destination pile: nearest that allows and has room, with a demand override.** To
store a loose item, the game scans every stockpile, keeps those whose allow-set admits the item's
categories and which are not full, and picks the one whose centre is nearest the item. Two
refinements matter:

- **A full container is stored by its contents, not by itself.** A barrel of ale is routed as ale.
  An *empty* container is routed differently again — by **demand**.
- **Demand ranks empty containers instead of distance.** Each pile tracks, per category, how badly it
  wants empty containers of that kind, derived from its limits and its current holdings. When routing
  an empty container the game sorts by demand first and uses distance only to break ties. So empty
  barrels flow to the pile that will need them, not to the pile that happens to be closest. This is a
  small idea with a large effect: it is the difference between a colony that pre-positions its
  containers and one that shuffles them back and forth.

**Reserving a spot also adjusts the pile's books.** When a hauling job reserves a tile, the pile
immediately increments its own per-category amount (where a limit exists) and decrements its
container demand. The comment in the source is explicit about why: without it, the game queues far
too many hauling jobs to the same destination, because each new job sees a pile that has not yet
received the items already in flight. **Counting in-flight goods against the destination is the fix
for the "everyone hauls to the same pile" stampede**, and it is worth knowing that they hit that bug
and named it.

**Hauling priority is derived from scarcity.** The priority band of a "store this" job is not fixed.
Food is always high. Everything else is banded by the ratio of what the colony has to the minimum the
player asked for: comfortably stocked is low, mildly short is medium, badly short is high. So the
same job — put this ingot away — is urgent during a shortage and idle-time work during a glut,
without the player touching anything.

**Production is pull-based from stock minimums.** A separate stock manager holds, per item type, a
player-set minimum. Each pass it computes the shortfall, divides by the recipe's yield to get a job
count, subtracts the jobs already outstanding for that item, spreads the remainder across the
workshops capable of making it (clamped to ten queued per workshop), and queues the difference. Raw
materials are special-cased against the same machinery: items that come from trees consume the
player's tree designations and emit felling jobs; items that come from the ground consume bog
designations; water emits a find-an-empty-barrel-fill-it-store-it job. The player's interface is a
list of minimums; the colony works out the rest. This is exactly the "maintain a stock of N planks"
directive that contemporary commentary praised as the game's central simplification over Dwarf
Fortress — one player action where the older game wanted ten.

**Stockpile self-tidying is a low-priority job.** Loose items sitting in a pile that could live
inside a container in the same pile generate a low-priority reorganise job. It is a nice pattern:
housekeeping expressed as ordinary low-priority work that idle hands pick up, not as a special
background process.

**Item lookup for consumers goes through the piles.** "Find me the nearest item of category X" scans
every stockpile, asks each for a match, skips reserved items, and keeps the nearest — with flag
variants for "not full", "better than value V", "emptiest", "most decayed" and "avoid garbage". The
most-decayed variant substitutes decay for distance in the same comparison, which is how the colony
eats its oldest food first with no separate mechanism.

### What is worth learning

Stated as principles, in our own words, for a fresh implementation:

1. **One job structure, a list of primitive steps.** Resist a `HaulJob` class and a `BuildJob` class.
   Make hauling, building, felling and eating the same ordered list of small typed steps over the
   same tiny result vocabulary (done / failed-but-continue / failed-fatally / still working). It is
   what let a one-man project add new work kinds cheaply, and it is why hauling needed no subsystem
   of its own.
2. **The job owns every claim it makes, and releasing them is one code path.** Items, destination
   tiles, container capacity, ground marks — all reserved through the job, all released when the job
   ends however it ends. Never let a system reserve something on a job's behalf and remember to undo
   it separately.
3. **Count goods in flight against the destination at the moment of reservation.** The destination's
   idea of how full it is must include what is already on its way, or the dispatcher will queue a
   dozen haulers to the same pile in one pass. They hit this and said so in the code.
4. **Match workers to jobs globally, not greedily.** A batched optimal assignment over waiting
   workers and available jobs removes the crossing-paths pathology by construction. Cap the batch —
   they capped it at twenty for measured speed reasons — and run it periodically rather than per-pawn.
5. **Idle workers register; they do not search.** Inverting the pull into a push is what makes a
   batched global assignment possible at all, and it makes "how many pawns are idle" a number you
   already have rather than one you compute.
6. **Bound the attempts per job kind.** One attempt for opportunistic hauling, many for a job worth
   retrying. A failure list drained in a batch beats re-queuing an impossible job every tick.
7. **Derive urgency from scarcity rather than asking the player.** Band a hauling job's priority by
   how far the commodity is below its minimum. The player sets minimums, which are meaningful; they
   do not set priorities, which are not.
8. **Pull production from stock minimums, and spread it over the producers.** Shortfall ÷ yield −
   outstanding, divided among capable workshops, clamped. One number per commodity is the whole
   player interface to the economy.
9. **Route empty containers by demand, loaded ones by their contents.** Distance is the wrong sort
   key for a thing whose value is where it will be needed.
10. **Let a stockpile grow into adjacent legal ground instead of reporting itself full.** "Full"
    should mean "no room and nowhere to grow", not "every tile occupied".
11. **Cheap probe, then exhaustive scan.** Their free-tile search tries a few random tiles first and
    only then walks the pile. Good shape for any "find any acceptable slot" query.
12. **Express housekeeping as low-priority ordinary jobs.** Tidying, reorganising and consolidating
    should compete for labour like everything else, not run as privileged background code.
13. **Model tool scarcity at the dispatcher.** Do not offer more tool-requiring jobs than there are
    tools, and prefer a worker already holding the right tool by weighting the assignment rather than
    by filtering.
14. **Let blocked work unpause its own prerequisites.** Demand pulling its dependencies is a much
    better default than the player sequencing them.

### What to avoid

1. **Global mutable singletons everywhere.** The job manager, the stock manager and the game itself
   are classic singletons reached from anywhere. This is the single biggest structural obstacle to
   testing anything in isolation, and it is directly at odds with our composition-root rule and with
   our determinism harness. Our equivalents must be owned by the world and passed, not fetched.
2. **Linear scans as the universal query.** Finding a job by id, removing a job, finding an item, and
   choosing a destination pile are all full walks of a list or of every construction in the world.
   With a few hundred jobs and a handful of piles this is fine; it will not survive our map sizes. Any
   of these that we keep needs an index.
3. **The assignment matrix does not scale, and they knew it.** The twenty-job cap is a comment
   admitting that the cubic matcher was too slow. On a 250 × 250 × 40 map with a large colony we will
   hit this much sooner. Either keep the batch small and run it often, or pre-filter candidates
   spatially before building the matrix — but do not assume the matcher is free.
4. **Distance is straight-line, not path cost.** The dispatcher's cost matrix and the nearest-pile
   search both use a geometric distance. In a 2D open map that is a reasonable proxy; **in our layered
   world it is actively misleading**, because the nearest pile by Euclidean distance may be two
   layers down with no ladder. We must either use a reachability-aware estimate or accept and measure
   the error.
5. **Weak references as a lifetime model.** Jobs, items, workers and constructions refer to each other
   through weak handles that must be locked and null-checked at every single use. The code is dense
   with this ceremony and a missed check is a crash. Integer handles into dense arrays — which our
   structure-of-arrays architecture already gives us — remove the entire class of problem.
6. **Two worker classes is too coarse, and the cost function knows nothing about skill.** Menial
   versus expert, with a tool-possession penalty, is the whole model. There is no per-skill
   suitability, no per-pawn work priority table, and no way for the player to say "this one does not
   haul". Players of this genre expect that control; we should put skill and a per-pawn work-type
   preference into the assignment cost from the start, since the matrix makes that cheap to add.
7. **Priority is set at creation and cannot be re-evaluated.** A job's band is fixed when it is
   built. The scarcity-derived hauling priority is computed once, so a job queued during a glut stays
   low-priority through the famine that follows. If we adopt derived priority, re-derive it on the
   dispatch pass.
8. **Special cases leaking into the stock manager.** Trees, bog iron and water each have bespoke
   branches inside the production loop. This is the seam that should have been a data-driven
   "source" concept, and it is precisely the kind of chokepoint our own seam audit exists to prevent.
9. **Reservation bookkeeping mutates the pile's public counts.** Reserving a spot increments the
   pile's amount directly, so displayed stock includes goods not yet delivered. Correct for
   scheduling, arguably a lie to the player. Keep committed and in-flight as separate numbers.
10. **A single global tick order with no explicit schedule.** The managers are updated by direct
    calls from the game loop. We already do better with tick groups; do not regress towards this.

## Recommendation

For Odyssey's stockpile and hauling work I commit to the following positions.

1. **Adopt the job-as-step-list shape, with the job owning its reservations and one release path.**
   This is the strongest idea in the codebase and it costs us nothing to take. A job is an ordered
   list of typed steps over a fixed small result vocabulary; every claim a job makes — item, target
   cell, container capacity, designation mark — is registered on the job and released in exactly one
   place when the job ends, successfully or not. Our existing work-giver registration (OQ-44) supplies
   the jobs; this supplies their anatomy.
2. **Count in-flight goods against the destination.** Reserving a destination cell must immediately
   move the destination's "how full am I" number. Do this in the first version, not as a later fix.
   It is a two-line idea that prevents the stampede bug they had to go back and fix, and it is the
   kind of bug that is invisible in a two-pawn test and obvious in a twenty-pawn colony.
3. **Bound attempts per job kind, with opportunistic hauling at one attempt.** Cheap, and it is the
   difference between a colony that quietly drops an impossible task and one that burns the tick
   budget re-dispatching it.
4. **Derive hauling priority from scarcity, and re-derive it on every dispatch pass** — fixing their
   bug rather than inheriting it. The player sets minimums per commodity; the game decides urgency.
5. **Let a stockpile answer "full" as "no free cell, no accepting container, and nowhere legal to
   grow".** Growth into adjacent legal ground within the zone's permitted terrain, subject to our
   layer rules.
6. **Defer the Hungarian matcher, but design the dispatcher so it can be dropped in.** Ship greedy
   nearest-suitable-worker first, because it is simpler and because our top technical risk is already
   pathfinding cost at 65% of the tick and this would add a second cubic term. But keep the two
   preconditions the matcher needs: **idle pawns register on a waiting list rather than searching**,
   and **dispatch is a periodic batched pass over (waiting pawns × candidate jobs)**. Those two
   choices are free now and expensive to retrofit. If the crossing-paths pathology shows up in a
   twenty-pawn colony — and on the evidence it will — the matcher slots into the same pass.
   *The experiment that decides it:* instrument total pawn travel distance per simulated day under
   greedy dispatch on a fixed seed, then under a capped matcher on the same seed. If the matcher does
   not reduce travel by a margin worth its cost in a headless day, greedy stays.
7. **Do not use straight-line distance in the dispatch cost.** This is where our layered world
   diverges hardest from theirs. A geometric distance across layers is not just imprecise, it is
   wrong in kind — an unreachable pile can look nearest. Use the district-id reachability check
   already proposed in `d-04-pathfinding.md` as a gate, and a layer-weighted distance as the estimate.
   This is the one place where copying their technique unmodified would be a mistake.
8. **Do not adopt their skill model.** Put a per-skill suitability term and a per-pawn work-type
   preference into the assignment cost from the first version. Two classes will not survive contact
   with players and the cost function is the natural home for both.
9. **And to state it once more for the record:** nothing from this project is copied. The GPLv3
   licence is noted for completeness, not as permission; the practice is unchanged either way.

## Layer questions touched

Goblin Camp is strictly 2D — a single-level map, coordinates are (x, y) throughout, and nothing in
the job, stockpile or stock-manager code has any concept of a vertical dimension. There is therefore
**no layer technique to learn here, at all**, and this file should not be cited as evidence about
layered behaviour.

Two places where our third dimension breaks their assumptions are worth recording, and both are
covered in the Recommendation:

- **Distance as a proxy for effort.** Their dispatcher and their nearest-pile search both use planar
  distance. In a layered world with ladders, stairs, hops and drops, planar distance can rank an
  unreachable destination first. A reachability gate plus a layer-weighted estimate is required, not
  optional.
- **Stockpile self-expansion.** Growing into the ring of adjacent legal ground is a clean idea in 2D.
  In our world "adjacent" must mean same-layer and supported, or a pile will try to grow off a
  terrace riser. The permitted-terrain test they already had is the right hook for the extra
  condition.

## Sources

1. Goblin Camp on Libregamewiki (licence, copyright, release history, last release 0.21, July 2012) —
   https://libregamewiki.org/Goblin_Camp
2. Archival source repository, `TheCatPlusPlus/goblin-camp-legacy` (archived read-only, July 2023) —
   https://github.com/TheCatPlusPlus/goblin-camp-legacy
3. Job and job-manager sources in that archive, read for technique only —
   https://github.com/TheCatPlusPlus/goblin-camp-legacy/tree/main/goblin-camp/src
4. *Goblins' Lot*, the maintained GPLv3 fork by Nikolay Shaplov (renamed from Goblin Camp) —
   https://gitlab.com/dhyannataraj/goblins-lot
5. Gaslamp Games, "Game Design Dialectic: Dwarf Fortress and Goblin Camp" (2010) — contemporary
   commentary on the stock-minimums directive and the deliberate abstraction —
   https://archive-gaslamp.dredmor.com/2010/07/16/game-design-dialectic-dwarf-fortress-and-goblin-camp/
6. Goblin Camp on RogueBasin — https://www.roguebasin.com/index.php/Goblin_Camp
7. Goblin Camp on Open Hub (project activity, codebase size, licence) —
   https://openhub.net/p/goblin-camp
8. Korppi Games' Goblin Camp site, "Haven't I seen you guys before?" (2023) — establishes that the
   current commercial game shares the name and lineage but not the codebase —
   https://goblincamp.com/2023/07/03/Havent-I-seen-you-guys-before.html

## Confidence

- **High** on licence, copyright holder, dates and project status. The GPLv3 headers are in every
  source file, the archival repository states its own frozen status, and the release date is
  corroborated by two independent wikis.
- **High** on the mechanisms described under "Jobs and stockpiles". These were read directly from the
  archived source: the job/task structure, the Hungarian-algorithm dispatch with its twenty-job cap,
  the four priority bands, the waiting list and prerequisite promotion, the reservation set and its
  single release path, the attempt budget, the tool-scarcity gate, the per-tile container stockpile,
  self-expansion, demand-based routing of empty containers, scarcity-derived hauling priority, and
  pull-based production from stock minimums. Where a code comment stated a motive — the matrix being
  slow, the over-queuing of hauling jobs — that motive is reported as stated.
- **Medium** on the design *intent* and on what the design got wrong in play. The contemporary
  commentary is one article, and the criticisms listed under "What to avoid" are largely my reading
  of the code against our own constraints rather than a record of reported player complaints.
- **Low** on anything about how the game actually felt, how it performed at scale, or why it was
  abandoned. See below.

## Could not be determined

**The public record on this project is thin, and padding it would be worse than admitting that.**

- **No postmortem exists**, or none that surfaced. There is no statement from Ilkka Halila about why
  development stopped in 2012. The original forum is gone; the current site under the same name
  belongs to the commercial studio and does not discuss the old codebase's technical history.
- **No performance data.** Nothing states how many workers or how large a colony the job manager
  actually sustained, what the dispatch pass cost, or where it fell over. The twenty-job cap is
  evidence that it did fall over, but not evidence of where.
- **No record of player-reported job or stockpile bugs from the original 2010–2012 game.** Searches
  for these are swamped by a same-named Steam title, an unrelated Baldur's Gate 3 location, and a
  Stonehearth feature. Everything in "What to avoid" that is not a code comment is therefore
  inference from the source against our own requirements, and should be read as such.
- **The archival repository reports no detected licence** in GitHub's metadata, despite carrying the
  full GPLv3 text and per-file headers — presumably a filename the detector does not recognise. The
  licence is not in doubt; the metadata is simply unhelpful, and a future reader who checks only the
  sidebar will be misled.
- **The relationship between the archival copy's modernisation changes and the original 2012 code was
  not audited.** The modernisation swapped the scripting layer and the build system. Everything
  described above is from the archival tree; the odds that the job and stockpile logic was altered
  are low, but this was not verified against a 2012 tarball.
- **The maintained fork was not read.** *Goblins' Lot* has had years of commits since 2019 and may
  well have fixed several of the problems listed above. Determining that would be a separate
  question, and given how little we intend to take from here, probably not worth asking.
