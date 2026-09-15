# Lane A14 — Bills, stockpiles and inventory

## Question

How do RimWorld's bills, stockpiles and inventory work — stockpile zones with priorities and filters, the storage-settings/filter data shape, item stacking, hauling-to-storage decisions, bill types and their counting modes, bill ingredient search radius, and item deterioration? Extracted as: data shapes (structure only), decision rules, the bill state machine, stacking rules, deterioration rates and tick cadence. Clean-room: mechanics and design intent only; everything below is paraphrased from the public wiki and community documentation, with no decompiled source read and no Def XML reproduced.

## Findings

### 1. The three objects and how they relate

RimWorld's storage layer is three small things wired together:

1. **A storage container** — anything that can hold items and carries a *storage settings* record. This is not only the stockpile zone: a stockpile zone, a "dumping" stockpile zone, a shelf, a grave, a sarcophagus and a hopper all carry the same settings record. That is why the wiki says settings can be copied *between* them (options absent from the source, e.g. food on a grave, arrive disallowed).
2. **A storage settings record** — a priority plus a filter. Held by the container, not the item.
3. **The hauling work-giver** — a scanner that matches loose items against containers and emits a haul job.

The important structural insight for us: **"stockpile zone" is a skin over the same interface a storage building implements.** If we model `IStorageContainer { StoragePriority Priority; ThingFilter Filter; IEnumerable<Cell> Cells; bool Accepts(Item); }` from the first commit, zones, shelves, salvage crates and pre-existing ruin containers are all one code path.

### 2. Storage settings — data shape (structure only)

A storage settings record carries:

| Field | Shape | Notes |
|---|---|---|
| Priority | enum, ordered | Player-facing levels are **Low, Normal, Preferred, Important, Critical** (five). Evidence for a sixth, implicit "unstored/none" baseline used for loose items is strong but inferred — the comparison "is the item already in storage of priority ≥ the candidate?" needs a floor value. |
| Filter | nested record (below) | The whole of "what may be stored here". |
| Owner/name | optional string | Storage can be *named*; a named container (or linked group) can be a bill's delivery target. |
| Group link | optional reference | Shelves can be **linked** into a group that shares one settings record. The link adopts the settings of the first container selected. |

The **filter** is the interesting shape, and it is reused verbatim in three places — storage settings, bill ingredient rules, and (per A3) apparel/food policies. Its fields:

| Field | Shape | Notes |
|---|---|---|
| Allowed things | set over a **category tree** | The UI is a tree of categories (e.g. Foods → Meals → Fine meals) with tri-state checkboxes; the stored form is an allow-set of leaf thing-defs plus the category nodes used for display/roll-up. |
| Allowed hit-points range | float range, 0–1 (percent of max HP) | Two-handled slider. |
| Allowed quality range | enum range (min quality … max quality) | Only meaningful for things that have quality. |
| Special filters | named boolean toggles | Cross-cutting predicates that are *not* a category — the wiki's version history records special filters added for "meat/veg ingredient-filled meals", "insect meat" and "cannibal food" in 1.3.3200. Structurally these are named predicate objects registered by def, evaluated per item. |
| Stuff/material restriction | set of material defs | Exposed on bill ingredient filters (make this out of steel, not gold); the same mechanism backs the storage tree's per-material leaves. |

Design intent worth naming: **the filter is a def-extensible predicate set, not a hard-coded list.** Special filters are the escape hatch that lets a mod (or, for us, a salvage system) add "is irradiated", "is pre-collapse tech", "is structurally load-bearing" without touching the category tree.

### 3. Hauling — the decision rule

The wiki states the destination-selection order as (and flags it `[Verify]`, so treat the exact tie-breaking as medium confidence):

1. The container **accepts** the item (filter passes).
2. The container **has space** for it.
3. Highest **priority** wins.
4. Among equals, **nearest** wins.

Three consequences the wiki is explicit about, and each is a design decision we must copy or reject deliberately:

- **If no container both accepts the item and has space, the item is simply not hauled.** There is no "haul it somewhere, anywhere" fallback. Items sit where they fall.
- **Priority does not order the haul queue.** An item destined for a Critical stockpile is not hauled *sooner* than one destined for a Normal stockpile; priority only decides *where* an item goes once it has been picked for hauling. The haul order is whatever the work-giver's scan order produces (per A3, roughly nearest-first within the region scan).
- **Re-stowing is a first-class job.** A hauler will move an item from a lower-priority container to a higher-priority one that also accepts it. This is what makes "small Important stockpile beside the workbench, pulled from the Normal warehouse" work, and it is the single most important emergent behaviour in the whole system.

Carrying rules:

- A hauler carries **one item type at a time**.
- It takes the lesser of its carrying capacity and **one full stack** — never more than a stack even if capacity allows (the wiki's worked examples: never more than 75 wood or 500 silver in one trip).
- If the destination cell cannot take the whole load, the surplus is **dropped nearby** rather than carried back.
- A hauler must be able to move and must have non-zero manipulation capacity.

What makes an item unhaulable:

- **Forbidden.** Everything *except* chunks is implicitly haulable while not forbidden; **chunks must be explicitly designated** with a "haul things" order. This is a deliberate noise-suppression choice: the item class that is heavy, abundant and usually worthless is opt-in.
- **Outside the hauler's allowed area.** Items, locations and tasks outside a pawn's allowed area are treated as forbidden *to that pawn*. A pawn may path *through* disallowed cells to connect two allowed regions, but will not interact with anything there.
- **Unreachable** — no path (per A3, resolved by the region/reachability oracle, not by running a path).
- **Fogged** — items in undiscovered fog do not count as colony inventory (1.4.3682 fixed this for bill counting specifically).

Haul is a **work type containing many work-givers**, ordered. The wiki's descending order: rearm turrets → refuel → unload carriers → load caravan → bank genepacks → load transporters → strip corpses → haul corpses → cremation bills → campfire bills → empty egg boxes → take beer from barrels → fill barrels → **haul general things** → deliver resources to frames → deliver to blueprints → **merge things**. Two observations: general hauling sits *low* in that list (most haul-type work outranks it), and **stack merging is the lowest-priority job of all** — tidying happens only when there is nothing else to do.

Non-colonist haulers: trained animals haul only occasionally, with a **mean time between of 1.5 in-game hours** (≈3,750 ticks at 2,500 ticks/hour). Dedicated hauler creatures (dryad carriers, mech lifters) haul continuously like colonists.

### 4. Stacking

- Every item def carries a **stack limit**. Observed values span 1 (anything with quality — weapons, apparel, art) to 75 (wood) to 500 (silver); the wiki tracks it as a per-thing property across ~224 pages.
- A stack occupies **one cell**. Storage capacity is therefore `cells × 1 stack` for a floor zone. Storage *buildings* override this: a shelf holds **3 stacks per cell**, 6 for a 2×1 shelf.
- **Items are tracked per stack, not per item.** When two stacks merge, per-item state is collapsed to a single averaged value — hit points and rot progress are averaged across the merged stack. This is a deliberate memory/CPU trade and it is why deterioration can be applied per stack rather than per item.
- Merging only happens via the lowest-priority "merge things" job, and (per a Ludeon bug report) vanilla merges within a container rather than across two same-priority containers.
- Storage buildings may exclude whole classes: a shelf refuses chunks, minified buildings, plants, toxic wastepacks and corpses above body size 0.75. So the container type imposes a hard constraint *on top of* the filter.

### 5. Bills — data shape and state machine

A bill is attached to a workbench (or to a pawn, for operations) and carries:

| Field | Shape |
|---|---|
| Recipe reference | def reference |
| Repeat mode | enum: DoXTimes, DoUntilYouHaveX, Forever |
| Target count | int (meaning depends on mode) |
| Pause when satisfied | bool (DoUntilYouHaveX only) |
| Unpause threshold | int (DoUntilYouHaveX only) |
| Suspended | bool |
| Ingredient filter | the same filter record as §2, plus its own hit-points range |
| Ingredient search radius | float, or unlimited |
| Product destination | enum + optional container reference: BestStockpile / DropOnFloor / TakeTo(named container or group) |
| Worker restriction | enum + optional pawn reference (anyone / specific pawn / category) |
| Allowed skill range | int range (min, max) |
| Order index | position in the bench's ordered list |

State machine, as the wiki describes it:

```
Created ──► Active ──┬─► (each completion) decrement/recount
                     ├─► Suspended   (manual S toggle, or auto-suspend condition met)
                     ├─► Paused      (DoUntilYouHaveX + "pause when satisfied" + count ≥ target)
                     └─► Completed   (DoXTimes only, repetitions exhausted → bill deletes itself)
Paused ──► Active     when count drops to the unpause threshold
Suspended ──► Active  manual resume
```

Selection, each time a pawn arrives at a bench: bills are attempted **top of the list downwards**, and the first workable one is taken. A bill is skipped when:

- its ingredients are unavailable, **or unavailable within the ingredient radius**;
- the pawn lacks the required skill;
- the pawn fails the worker restriction (named pawn, skill band, pawn category);
- the bill is suspended (manually or by an auto-condition);
- the recipe makes an item with **quality**, an **unfinished item** already exists from another pawn, and this pawn is not that author. (This is an authorship lock: quality products are bound to their maker mid-craft.)

If no bill on the bench is workable, the pawn does not work that bench at all.

Counting modes:

- **Do X times** — a plain repetition counter. Remaining count shown; self-deleting at zero.
- **Do until you have X** — recounts colony inventory before each repetition. **Only items in storage count.** Items on the floor do not, which is why "drop on floor" plus a Low-priority stockpile under the worker's feet is the standard idiom (otherwise the bill overproduces indefinitely). Fogged items do not count either. Newer versions also allow counting to be restricted to a specific stockpile rather than all storage (medium confidence — seen in community sources and mod changelogs rather than the wiki page).
- **Do forever** — runs until materials run out. Used for destructive/conversion recipes (butcher, disassemble, burn) where "how many" is meaningless.

The **ingredient radius** caps how far a worker will walk to fetch ingredients, measured from the bench. Hovering a radius-limited bill outlines the area. Its purpose is stated plainly: stop a stonecutter walking to the far corner of the map for a chunk. The **default is unlimited** (medium confidence — inferred from the existence and popularity of a "Default Ingredient Radius" mod, not stated on the wiki).

The **product destination** choice is a pure throughput lever, and the wiki spells out the trade: "take to best stockpile" makes the crafter do the haul (good for rare, urgent items); "drop on floor" keeps the crafter crafting and defers the haul to a hauler (good for bulk); "take to *named container*" is the compromise — a one-step placement onto an adjacent named shelf group that haulers then drain.

Bills can be **copied and pasted** between benches that share a base recipe set.

### 6. Deterioration

- Deterioration is **hit-point loss**, on the *same* HP pool as combat and fire damage. At 0 HP the item is destroyed. It is completely independent of food spoilage (which is temperature-driven); both run in parallel and neither feeds the other.
- Each item def carries a **deterioration rate in HP per day** while unroofed or outdoors. Observed span: 0.25 HP/day (packaged survival meal) to 6 HP/day (rice). A rate of 0 means immune.
- Multipliers: **×5 in rain**, **×3 on watery/marsh terrain**. **Indoors (roofed) = no deterioration at all.** **Anything in a shelf = no deterioration**, even outdoors in rain. Higher quality lowers the rate.
- Immune classes: metals, stone, persona cores, artefacts, minified furniture and buildings. Crafted items do *not* inherit their material's immunity.
- Worn apparel and armour deteriorate while equipped; equipped weapons and utility items do not.
- Effect of HP loss, beyond eventual destruction, is **market value and mood only** — no stat, damage, armour, insulation, nutrition or quality change. Value curve (a bare formula, quoted because precision matters):
  - 100% of value from 100% down to 90% HP;
  - −1.67% of max value per 1% HP lost between 90% and 60% HP;
  - −4% of max value per 1% HP lost between 60% and 50% HP;
  - −0.2% of max value per 1% HP lost between 50% and 0% HP;
  - so: 50% value at 60% HP, 10% value at 50% HP.
- Mood: any worn apparel at ≤50% HP gives a −3 thought; any worn item below 25% gives −5 (compare −6 for naked).
- Design intent: deterioration is the **pressure that makes roofs and storerooms an early goal**, and it doubles as a free disposal mechanism — a dumping zone outdoors in running water destroys corpses and junk at no labour cost. Both directions are intentional.

### 7. Tick cadence

Confirmed from the wiki: **60 ticks per second**, 2,500 ticks per in-game hour, 60,000 ticks per day. Deterioration is expressed per *day*, so it is applied as a fractional rate; because stacks average their HP, it can be applied once per stack per infrequent tick rather than per item.

The wiki does not state the actual tick intervals for deterioration checks, bill recounts, or storage re-scans. From general modding knowledge (**low confidence, flag for verification**), RimWorld uses coarse tick buckets — a "rare" bucket around 250 ticks and a "long" bucket around 2,000 ticks — and deterioration sits in one of those. What *is* certain from the observed behaviour is that none of these run every tick: bill counts, storage validity and deterioration are all cheap-if-infrequent and none needs sub-second latency.

For us the safe design is: put storage/bill/deterioration work on explicit slow tick groups (per A15's tick-group model) with a stable bucket assignment derived from entity id, so determinism holds.

## 3D/layer impact

- **Storage capacity is a floor property, not a volume property.** A stack sits on the floor of a cell; the 3 m of headroom above it is not usable storage unless a *building* (rack, gantry) says so. So the natural 3D generalisation is not "a stockpile is a box of cells" but "a stockpile is a set of floor footprints, one per layer".
- **Vertical hauling is expensive and must be priced into the distance term.** The destination rule's fourth clause is "nearest", and nearest must mean *path cost*, not Euclidean distance, or every hauler will pick the stockpile that is two layers down and forty seconds away. Per A3's region model, stairs are region links; the distance used for storage choice should be the region-graph distance with a **per-layer-transition penalty** baked into the link cost.
- **A hauler should prefer same-layer storage, and the mechanism should be the link cost, not a special rule.** Adding a hard "same layer first" rule breaks the priority ordering (a Critical stockpile one layer up would lose to a Low one on this layer). Instead: give a stair traversal a cost equivalent to some number of horizontal cells, and let the existing "highest priority, then nearest" rule do the rest. A defensible starting value is **a stair transition costing 8–12 horizontal cells** (≈20–30 m of walking at our 2.5 m cell), tuned so that a same-layer Normal stockpile beats a one-layer-away Normal stockpile but loses to a one-layer-away Important one. That number is a tuning knob for the slice, not a finding.
- **Re-stowing across layers is the failure mode to watch.** The lower→higher re-stow rule plus cross-layer paths can produce haulers endlessly carrying goods up and down stairs. Two mitigations, both cheap: require a **minimum priority gap or minimum gain** before a re-stow job is emitted, and exclude re-stow from any container whose settings match the source exactly.
- **Roofing and deterioration become genuinely three-dimensional.** "Indoors = no deterioration" is really "has a roof above this cell". In a layered world the roof of layer *n* is the floor of layer *n+1*, so an intact upper floor already protects everything beneath it. That is a strong, legible reward for occupying a building rather than its rubble-strewn top — the single best argument for keeping deterioration in the slice.
- **The haul work-giver's scan must be region-based, not whole-map.** At 250 × 250 × 40 there are 2.5 M cells; a naive "find all haulable things" sweep is impossible. Per-layer listers of haulable items and of containers-with-space, updated incrementally, are mandatory from the first commit.
- **Vertical throughput is a design lever we get for free.** Stair count and placement become a genuine logistics constraint — a warehouse three layers below the workshop is *cheap in area and expensive in labour*. That is exactly the kind of decision the 3D premise should be producing.

## Ruined-city impact

- **Salvage is the dominant item class, and the filter tree must be built for it.** A ruined city yields a wide, messy spread — structural scrap, electronics, fabric, sealed containers of unknown contents, contaminated material. The category tree should be organised by *what you do with it* (feedstock / component / consumable / hazard / intact device), not by what it used to be, because that is the axis the player filters on when placing a stockpile.
- **Chunk-style opt-in hauling is essential, not optional.** A collapsed building generates enormous quantities of rubble. RimWorld's decision to make chunks require an explicit designation, while everything else auto-hauls, is exactly the right precedent: rubble must be opt-in or the haul queue never empties and the player never sees the items they care about.
- **Pre-existing containers in ruins are the best fit for the storage-building interface.** Lockers, crates, shipping containers and racks already present at mapgen should implement the same `IStorageContainer` as a player-built shelf, with two twists: they start **locked/forbidden** until opened or claimed, and their contents are generated lazily. That gives free exploration reward, free early storage (a captured locker beats hauling to a floor pile), and free deterioration protection — which is a strong pull towards occupying intact interiors.
- **Deterioration is the ruin's clock.** Salvage left in the open under rain degrades; salvage under an intact floor does not. This single rule turns "find and roof a building" into the opening move of every game, without any scripted objective.
- **Dumping zones as disposal.** The "outdoors + water = free destruction" trick should survive, because in a ruined city the player will constantly be clearing material they do not want, and giving them a zero-labour sink is kinder than making them haul it to an incinerator.
- **Expect a huge initial item count at mapgen.** Every pre-placed salvage pile is a stack the lister must know about. Mapgen must populate the listers directly rather than relying on a post-load sweep.

## Layer questions touched

- **Q6 (are zones, stockpiles and growing areas per layer or 3D volumes?) — answered, with a recommendation.**

  **Recommendation: per layer. A zone is a set of (x, y) cells on exactly one z. Commit to this.**

  The evidence:
  1. RimWorld's own constraint is that **zones are contiguous and may not overlap other zones**, whereas *areas* (allowed area, home area, roof area) are overlapping, possibly-disjoint bitmask layers. That split is load-bearing: zones are *things you configure and select*, areas are *masks you paint*. Contiguity in 3D is nearly meaningless — two stacked floors are adjacent only via a stairwell, so a "contiguous 3D stockpile" would in practice be two disconnected slabs joined by a single cell, which is a lie the UI cannot usefully draw.
  2. **Storage capacity lives on the floor.** A 3D volume zone would claim headroom it cannot use, making "has space" ill-defined.
  3. **The camera slices by layer** (Q9). A zone the player cannot see in full is a zone they cannot reason about. Going Medieval — the closest true-3D analogue we have studied (`b-going-medieval.md`) — makes verticality a mechanic precisely by forcing the player to decide *which floor* the storage goes on; a volume zone would dissolve that decision.
  4. Per A3, whether a stockpile spans layers changes only the lister and the filter, **not the job pipeline**. So per-layer costs us nothing architecturally and we can relax it later if needed.

  The one thing we lose — "one warehouse spanning three floors, configured once" — we get back through **storage groups**: named groups of containers that *share one settings record*, exactly as RimWorld links shelves. A group may span layers freely, because a group is a reference set, not a geometric region. That is the right seam: **geometry is per layer; configuration is groupable across layers.**

  Growing zones follow the same rule for an additional reason: light and soil are per-layer properties (Q4), so a growing zone that spanned layers would have no single fertility or light value.

  The cheapest experiment that could overturn this: build the slice with per-layer zones plus cross-layer named groups, and watch whether players repeatedly draw the same stockpile on three consecutive floors. If they do, add a "clone zone to layer above/below" tool — which is a UI affordance, not a data-model change.

- **Q2 (is a roof a floor?)** — deterioration makes this mechanically visible: "indoors" for storage purposes is "roofed", and in a layered model the floor above *is* the roof. A pre-existing intact upper floor should count as roofing for deterioration immediately.
- **Q1 (vertical movement)** — the "nearest container" clause forces stair cost into the region-graph link weight; see §3D impact.
- **Q9 (camera slice and UI)** — per-layer zones plus cross-layer storage groups needs UI that shows "this group also has members on layers 2 and 4".
- **Q12 (what the slice must prove)** — the slice needs: per-layer stockpile zones with priority and a filter; one storage building type; the accept→space→priority→nearest rule with stair-weighted distance; lower→higher re-stow; "do until you have X" counting only stored items; and deterioration with roof immunity. It can stub: special filters, quality ranges, storage groups, animal haulers, the full haul-work-giver ordering, and merge-things.

## Sources

- https://rimworldwiki.com/wiki/Stockpile_zone
- https://rimworldwiki.com/wiki/Bill
- https://rimworldwiki.com/wiki/Hauling
- https://rimworldwiki.com/wiki/Deterioration
- https://rimworldwiki.com/wiki/Shelf
- https://rimworldwiki.com/wiki/Zone
- https://rimworldwiki.com/wiki/Zone/Area
- https://rimworldwiki.com/wiki/Property:Stack_Limit
- https://rimworldwiki.com/wiki/Template:Storage_Settings
- https://ludeon.com/forums/index.php?topic=50705.0
- https://ludeon.com/forums/index.php?topic=34431.0
- https://steamcommunity.com/sharedfiles/filedetails/?id=935982361
- https://steamcommunity.com/sharedfiles/filedetails/?id=857164561

## Confidence

**High** for: the storage-settings and filter field list; the five priority levels; the four-clause destination rule and the lower→higher re-stow behaviour; the "priority does not order the haul queue" point; the "no valid container → item is not hauled" point; the one-type/one-stack carry rule; chunks being opt-in while everything else is opt-out; the three bill repeat modes and their options; the bill-skipping reasons including the unfinished-item authorship lock; "only stored items count for do-until-you-have-X"; the product-destination options; deterioration rates, multipliers, immunities and the value curve; shelf capacity and shelf deterioration immunity; zones being contiguous and non-overlapping while areas are overlapping masks. All of this is stated plainly on the wiki pages listed.

**Medium** for: the exact tie-breaking order within the destination rule (the wiki itself marks that list `[Verify]`); the existence of an implicit sixth "unstored" priority; the "count products in a specific stockpile" bill option; the ingredient-radius default being unlimited; stack-merge semantics across containers.

**Low** for: tick intervals. The wiki gives the tick-per-second and tick-per-day conversions but never states how often deterioration, bill recounts or storage scans actually run. Everything in §7 beyond the conversions is inference and is flagged as such.

The 3D and layer-question sections are **reasoned recommendations**, not findings — they are ours, derived from the mechanics above plus A3 and the Going Medieval file.

## Could not be determined

- The actual tick cadence of deterioration, bill recounting, storage-validity rechecks and the haul work-giver scan. Community sources describe coarse tick buckets but the wiki does not document the intervals, and verifying them would require reading game source, which is out of bounds.
- The vanilla default value of a new bill's ingredient radius (unlimited is likely but unconfirmed from an authoritative page).
- Whether the destination rule truly evaluates priority before distance in *all* cases, or whether distance can dominate within a priority band under some threshold — the wiki flags its own list as unverified.
- The exact rule for how "has space" is computed for partially filled stacks (does a cell holding 40/75 wood count as space for a 75-wood haul, and is the surplus dropped or is the load split?). The "drops the extra nearby" line implies the former but does not settle it.
- The precise stack-merge algorithm for rot timers versus hit points (both are described as "averaged", but whether the average is count-weighted is not stated; count-weighted is the only sane reading).
- Whether storage groups (linked shelves) can span disconnected map regions, and therefore whether the layer-spanning group idea has a vanilla precedent at that scale.
- How many special filters exist in total and what the full registration surface looks like — only the 1.3.3200 additions are documented.
