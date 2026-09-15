# Lane A3 — Work and jobs: think tree, job pipeline, reservations, hauling

## Question

How does RimWorld's work-and-jobs AI work — work types and priorities, the ThinkTree → JobGiver → WorkGiver → Job → Toil structure, reservations, job interruption and failure, hauling, forbid/allow, and the daily schedule? Extracted as: the decision pipeline as a state machine, the Def data shapes (structure only), priority resolution order, reservation semantics, tick cadence, and tuning constants. Clean-room: mechanics and design intent only; everything below is paraphrased from public documentation and community explanations, with no decompiled source read and no Def XML reproduced.

## Findings

### 1. The pipeline at a glance

Every pawn action — sleeping, eating, hauling, shooting, wandering — is a **Job**. A per-pawn **job tracker** owns the current job and a short queue of player-ordered jobs. When a pawn needs a new job, the tracker consults a **think tree**: a hierarchy of nodes evaluated depth-first, where the first node to return a valid job wins. The result is a *ThinkResult*: the job plus the node that issued it. The job is then executed by a **JobDriver**, which decomposes it into an ordered sequence of **Toils** (atomic sub-steps: walk here, pick up, carry, place, do work for N ticks). When the job ends — success, failure, expiry or interruption — the tracker immediately re-consults the tree, in the same tick.

Design intent worth naming: this is **not** a pure behaviour tree and not global utility AI. The tree fixes a coarse, hand-authored ordering (safety before needs before work before idling); numeric scoring enters only at three narrow points — priority-sorter nodes whose children report a desirability number, the player's per-work-type priorities 1–4, and per-target scoring inside a single scanner (usually just distance). The pawn never compares "haul this" against "cook that" on one scale; it takes the *first* valid job in a well-chosen scan order. That makes decisions cheap, predictable and player-legible, at the cost of occasional myopia. The scan order *is* the tuning surface.

### 2. The decision pipeline as a state machine

States for one pawn:

- **NeedJob** → run think: (a) if the player order queue is non-empty, pop it; (b) otherwise traverse the main think tree to a ThinkResult. → **Starting**.
- **Starting** → build the JobDriver; the driver first attempts its **pre-toil reservations** (claiming every thing/cell the job will touch). Failure aborts the job instantly → back to **NeedJob** (counted by an error trap, see §9). Success → **Running**.
- **Running** → the toil sequence executes: each toil has an optional one-shot init action, an optional per-tick action, and a completion mode (instant; delay of N ticks, scaled by pawn stats; or never — ended only by an explicit condition). Per-toil and job-global fail conditions are checked continuously (target destroyed, forbidden mid-job, designation removed, pawn downed, thing burning…).
- Exits from **Running**:
  - *Succeeded* — final toil completed.
  - *Incompletable* — a fail condition tripped.
  - *Errored* — an exception in the driver; the job aborts and the pawn briefly stands.
  - *InterruptForced* — the constant tree, a threat response, drafting, or a player order preempts the job regardless of its wishes.
  - *InterruptOptional* — on periodic re-evaluation a better (higher-ordered) result appeared and the job permits casual interruption; player-forced jobs resist this.
  - *Expiry* — many jobs carry an expiry interval; when it elapses, either the job simply ends as succeeded, or (if the job asks for an override check) the think tree is re-run and the pawn keeps the job only if it is still the chosen result. This is how pawns "notice" newly available higher-priority work without rescanning every tick.
- Any exit → reservations released → **NeedJob** (immediately).

In parallel, a second, smaller **constant think tree** is evaluated on a short fixed interval (every 30 ticks) even while a job runs. It handles reflexes — fleeing an imminent explosion, reacting to hostiles — and its results force-interrupt the current job. Drafted pawns skip it, since drafting suspends autonomy.

### 3. The main humanlike tree, top to bottom (approximate, community-documented order)

Incapacitated/downed handling → mental states → joining caravans and other voluntarily-joinable group activities → drafted player orders → queued player orders → **lord duties** (raids, caravans and rituals inject behaviour by assigning a *duty*: a reference to a sub-tree plus a focus target, temporarily overriding normal life) → critical self-care (being tended, food when starving, collapse-level rest) → schedule-gated need satisfaction (sleep, recreation, meditation per the timetable, §8) → **work** (the single "do assigned work" giver that runs the whole work-scan of §4) → idle/wander. First valid result wins.

Node vocabulary (structure, not names to copy): *priority* nodes try children in listed order; *priority-sorter* nodes ask each child for a numeric priority and try them best-first (this is where need urgency vs. work is arbitrated); *conditional* nodes gate a subtree on a predicate (is colonist, schedule slot allows work, not in a mental state…); *random* nodes pick a child stochastically (used for idling variety); *subtree* nodes include another tree definition; *tag/insertion* nodes are named hook points where other trees (and mods) splice in with an insertion priority.

### 4. Work selection and priority resolution

Per-pawn **work settings** store a priority per work type: 0 = disabled, 1 (highest) to 4 (lowest). Simple mode is a checkbox that maps to a uniform middle priority (3); manual mode exposes the numbers. Some pawns are *incapable* of whole work-tag groups (violent, caring, intellectual, dumb labour…) via backstory, traits or roles, which hard-disables the corresponding work types.

Work types are an ordered list (the Work tab's left-to-right order is their natural order), roughly: firefight, patient, doctor, bed rest, childcare, basic, warden, handle, cook, hunt, construct, grow, mine, plant cut, smith, tailor, art, craft, haul, clean, research (plus DLC types). The order encodes designer intent: emergencies and life-safety leftmost, economy in the middle, low-urgency labour rightmost.

Each work type is a *container*; the actual scanning units are **work givers** (hauling alone bundles many: general haul, corpse haul, refuel, rearm, load transporters…). When the tree reaches the work node, the pawn walks a pre-sorted flat list of every enabled work giver:

1. **Numeric priority first**: all priority-1 givers before any priority-2, and so on.
2. **Within equal priority, work-type order** (left-to-right on the tab).
3. **Within one work type, the giver's own intra-type priority number** (each giver definition carries one).
4. An **emergency pass**: givers flagged emergency (fighting fires, tending, rescuing) are scanned ahead of non-emergency givers so a doctor at priority 1 drops hauling instantly, without the player micromanaging.
5. **Within one giver, per-target choice**: scanners usually take the *closest* valid target; some scanners are "prioritised" and rank targets by a giver-specific score instead (e.g. urgency).

For each giver the pawn checks giver-level gates first — required body capacities (e.g. manipulation), the pawn's allowed area, the schedule slot — then either asks a *non-scanner* giver directly for a job, or runs a *scanner*: enumerate candidate things (fed from per-map cached listers — haulable items, things awaiting bills, designations — never a raw whole-map sweep) or candidate cells; filter each by a validity test (not forbidden, reachable, reservable, still designated, capacity/skill adequate); pick the best survivor; construct the Job for it. The first giver to produce a job ends the scan.

Consequence the community documents loudly: a work type at priority 1 is exhausted *map-wide* before anything at priority 2 is touched — hauling at 1 means crossing the map for a distant chunk before cooking at 2. Distance only breaks ties *within* one giver, never across priorities.

Player right-click **forced prioritisation** bypasses the scan: it builds the job directly, marks it player-forced (sticky against optional interruption), and enqueues it; it still requires the pawn to be capable of, though not assigned to, the work type. Shift-clicking chains a queue.

### 5. Data shapes (structure only, no XML)

- **Work type def**: identifier; a family of labels (verb, gerund, pawn-facing label, description); a natural-priority number giving its tab position/order; the list of relevant skills; a set of work tags used by the incapability system; flags such as "starts active for new pawns" and visibility.
- **Work giver def**: identifier; label/verb; the behaviour class that does the scanning; a reference to its parent work type; an intra-type priority number; an emergency flag; required body capacities; flags for whether it scans things and/or cells; whether it is directly orderable by right-click; whether non-colonists (slaves, guests) may take it.
- **Think tree def**: identifier; one nested root node, each node being a behaviour-class reference plus per-class parameters plus a list of sub-nodes; optional insertion metadata (a target hook tag and an insertion priority) so a tree can splice itself into another.
- **Job def**: identifier; the driver class; a report string for the UI ("hauling X…"); behaviour flags — suspendable, casually interruptible, whether an override check runs on expiry or on damage, whether the job may take an opportunistic detour (see §7), recreation kind for joy jobs.
- **Job instance** (runtime): the def; up to three target slots (thing/cell) plus target queues for batch work; a stack count; an expiry interval and the override-on-expiry flag; a player-forced flag; a bill reference for crafting; haul mode; locomotion urgency.
- **JobDriver** (runtime): owns the job; a pre-toil reservation step (all-or-nothing claim; false aborts); a generator that yields the toil sequence up-front; job-global fail conditions; per-toil state (ticks remaining etc.).
- **Toil**: init action, tick action, completion mode (instant / N-tick delay scaled by stats / never-ends-until-condition), local fail conditions, progress and effect hooks.

### 6. Reservations

A per-map **reservation manager** records claims. One reservation stores: the claimant pawn, the job it belongs to, the target (thing or cell), a *reservation layer* (so distinct aspects of the same target can be claimed independently), a **maxPawns** count (most reservations are exclusive at 1; some allow N concurrent claimants, e.g. multi-pawn activities), and a **stackCount** (reserve only part of an item stack, so two haulers can split one pile).

Contract: scanners call a can-reserve test during validity filtering; the driver actually claims during its pre-toil step; every claim is released automatically when the owning job ends *for any reason*. Double-claims are logged as conflicts naming both pawns and jobs — a debugging staple. The check-then-claim gap is tolerated because the whole pipeline runs single-threaded per tick; anything that parallelises job assignment (as threading mods discovered) must make reserve-test-and-claim atomic. Reservations are the *only* coordination between pawns — there is no task auction or planner; combined with "closest target wins" it yields adequate emergent division of labour.

### 7. Hauling, storage and forbid/allow

- Items are **haulable** by default; the general-haul scanner draws from a per-map lister of items that are in no storage, or in storage of lower priority than some accepting storage. Validity: not forbidden, reachable, reservable, and a destination exists with capacity that *accepts* the item (storage filters) at the *highest available storage priority tier* (five tiers from low to critical); among equal-priority destinations, the closest wins. Storage priority beats distance. Stack-count reservations let stacks split between haulers.
- Corpse hauling, refuelling, turret rearming, transporter loading and delivery of ingredients/materials to bills and blueprints are separate givers (delivery is part of construction/crafting work, not the haul type — a pawn hauls its own building materials).
- **Opportunistic hauling**: jobs whose def opts in may prepend a detour — if a haulable lies roughly along the pawn's path to its real job and a storage cell lies near the destination, the pawn carries it en route. Pure pipeline garnish: it never changes what job was chosen.
- **Forbid/allow**: every loose item carries a forbidden bit (red X in the UI). Forbidden items are invisible to *all* work scanning — hauling, cooking ingredients, equipping, eating. Items dropped by non-colonists (dead raiders' gear) and items dropped outside the home area spawn forbidden; the player toggles the bit per item or with an area allow tool. Doors can be forbidden, which removes them from colonist pathing (mental-state pawns may ignore this). Forbidding is the player's cheap "not now" veto that composes with, rather than fights, the automatic scanning.

### 8. The daily schedule

Each pawn has a 24-slot timetable; each hour is one assignment. The assignment gates the tree's need-satisfaction and work branches with thresholds (documented values): **Anything** — work unless recreation < 35 %, food < 30 % or rest < 30 %, then satisfy the need. **Work** — only food < 30 % diverts; sleep and recreation are ignored until forced collapse. **Recreation** — as Anything but recreation is topped up towards ~95 %. **Sleep** — sleep unless rest is already high (~75 %) or food < 30 %. **Meditate** (DLC) — meditate unless food < 30 % or rest < 15 %. The schedule is consulted when *choosing* the next job, not mid-job: pawns finish the current task first, so needs can dip below threshold before the switch (drafting-undrafting force-reconsiders).

### 9. Tick cadence and tuning constants

- One in-game day = 60,000 ticks; 2,500 ticks per hour; 60 ticks per real second at normal speed (context from Lane A15).
- **Constant think tree: every 30 ticks** per pawn, even mid-job.
- **Main tree: event-driven**, not polled — re-run on job end, on job expiry with override check, on forced interrupts, on draft state change. No global per-tick job rescan; job **expiry intervals** (per job def, typically on the order of a few thousand ticks for open-ended work) provide the slow re-evaluation heartbeat.
- Need levels (which feed the priority-sorter arbitration) update at a fixed multi-tick interval (~150 ticks), not per tick.
- Priorities: 0 disabled, 1–4; checkbox mode = 3. Five storage priority tiers. Schedule thresholds as in §8.
- **Error trap**: a pawn that starts ~10 jobs within one tick/a very short window is assumed to be in a think-loop; the game logs it and parks the pawn in a brief stand-down recovery job. Any re-implementation wants the same circuit-breaker.
- Cost model: scans touch cached listers (haulables, designations, bill givers), validity tests are cheap-first (forbidden bit, area, capacities) with pathfinding-backed reachability last; "first valid wins" bounds per-decision work.

## 3D/layer impact

- **Reachability is the hot path.** Every scanner validity test calls "can this pawn reach that target"; at 250×250×40 that must be answered from a cached region/room connectivity graph in which vertical connectors (stairs = two cells, ladders = one, lifts later) are just region links — never a live pathfind per candidate. RimWorld's flat region system is the part that most needs a 3D redesign; the job pipeline above it can survive almost unchanged.
- **"Closest" must mean path cost, not Euclidean distance.** An item 3 m away horizontally but ten storeys down is not close. Within-giver target choice should score by approximate 3D travel cost (e.g. region-graph distance with a per-layer-change penalty), or hauliers will yo-yo between floors.
- **Listers stay map-global, validity stays layer-aware.** Keep one haulables/designations/bills lister per map keyed by (x, y, z); the priority resolution order (§4) is orthogonal to dimensionality.
- **Reservations generalise trivially**: claims keyed on thing-or-cell where cell is (x, y, z); layers-within-a-target semantics (§6) are unrelated to vertical map layers and keep their meaning.
- **Vertical hauling economics**: storage priority beating distance means a critical-priority stockpile four floors up will drag every hauler up the stairwell. Either accept it (it is legible), or fold vertical cost into the tie-break among equal-priority storage. Opportunistic hauling needs "roughly on the way" defined over the 3D path.
- **Constant-tree reflexes need vertical senses**: fire or hostiles one layer up/down must register for flee/response checks, which touches layer question 10's propagation model.
- Job expiry-and-override (§2) is a gift for 3D: expensive cross-layer rescans happen on the slow expiry heartbeat, not per tick.

## Ruined-city impact

- Ruins are loot-dense: forbid-by-default for anything outside the home area (RimWorld's exact rule) is essential, or every hauler dives into rubble. An explicit "salvage" designation (mirroring RimWorld's haul designation) makes looting opt-in per building.
- Collapse and structural failure mean targets and *routes* invalidate mid-job far more often than on a greenfield map: the fail-condition set (§2) must include "path no longer exists", and stale reservations must release on job failure exactly as RimWorld does.
- Forbidden doors as a cordon tool maps beautifully to sealing unstable ruin sections and unbreached floors.
- Scanner cost: a ruined map starts with thousands of haulable items and deconstructable things. Listers must be built at mapgen and updated incrementally; the "no whole-map sweeps" rule is load-bearing here, not an optimisation.
- New work types will be wanted (salvage, shoring/structural repair, clearing rubble); the work-type/work-giver split (§5) is exactly the extension point — add givers, keep the pipeline.

## Layer questions touched

- **Q1 (vertical movement/pathing model)**: job scanning presumes a cheap reachability oracle; the region graph must treat stairs/ladders as region links (§3D impact).
- **Q6 (zones and stockpiles per layer or volumes)**: hauling destination choice = storage priority then distance; whether a stockpile spans layers changes only the lister/filter, not the pipeline. Per-layer zones are simpler and match the camera slice.
- **Q7 (storyteller verticality)**: raids and set-pieces steer pawns via lord duties (a sub-tree plus focus target) — the natural hook for tunnelling/breaching behaviours.
- **Q10 (unit of simulation for fire etc.)**: the constant tree's 30-tick reflex scan must see cross-layer threats.
- **Q12 (what the slice must prove)**: the slice needs the full NeedJob→reserve→toils→end loop with one work type per pillar (construct, haul, eat, sleep) across at least two layers; priority-sorter subtleties, duties and opportunistic hauling can be stubbed.

## Sources

- https://github.com/CBornholdt/RimWorld-AI-Tutorial/wiki/Part-1---Introduction
- https://github.com/roxxploxx/RimWorldModGuide/wiki/SHORTTUTORIAL:-How-Pawns-Think
- https://github.com/roxxploxx/RimWorldModGuide/wiki/SHORTTUTORIAL:-Jobs-and-Work
- https://rimworldwiki.com/wiki/Work
- https://rimworldwiki.com/wiki/Schedule
- https://rimworldwiki.com/wiki/Orders
- https://rimworldwiki.com/wiki/Modding_Tutorials/Code_MendingJob
- https://rimworldmodding.wiki.gg/wiki/Def_Types
- https://rimworldaccess.com/colony/work/
- https://steamcommunity.com/app/294100/discussions/0/1744482417438350455/
- https://steamcommunity.com/app/294100/discussions/0/1740008353415460522/
- https://steamcommunity.com/app/294100/discussions/0/359543951707808172/
- https://github.com/cseelhoff/RimThreaded/issues/789

## Confidence

**Medium-high.** The pipeline shape (think tree → first-valid-job, job giver vs work giver, driver/toils, pre-toil reservations, constant tree at 30 ticks, duties) is corroborated by two independent modding tutorials and the official wiki's job tutorial — high confidence. Priority resolution (1–4, left-to-right, emergency pass, distance last) is consistently documented across the wiki and community — high. Reservation field structure, expiry/override behaviour and the error trap come from community discussion of internals rather than primary documentation — medium. Schedule thresholds are quoted from the wiki — high. Exact numeric values elsewhere (expiry intervals, need-update interval, auto-forbid rule edges) — medium/low, flagged below. rimworldwiki.com blocked direct fetching; its pages were read through a text proxy, which may omit detail.

## Could not be determined

- The exact node-by-node order of the shipped humanlike main think tree (the §3 order is a community-documented approximation; verifying it precisely would require reading game files, which the clean room forbids).
- Exact default expiry-interval values per job def, and the precise comparator used to sort the flat work-giver list (the three-key ordering in §4 is the documented behaviour; tie-break edge cases unverified).
- The full precise auto-forbid rule set (enemy drops and outside-home-area drops are documented; corner cases such as hunted-animal corpses and trader drops were not confirmed).
- The exact numeric priorities emitted by priority-sorter children (need urgency curves) — only the schedule thresholds are documented.
- Which shipped work givers use "prioritised" per-target scoring rather than nearest-target, and their scoring formulas.
- Whether the wiki's dedicated Hauling/Forbidding pages contain further detail — both returned empty/404 during this research window.
