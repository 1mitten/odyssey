# Lane A4 — Building and materials: blueprints, stuff, quality, roofs and support

## Question

How does RimWorld's building-and-materials system work — designations, blueprints → frames → built things, "stuff" materials with stat multipliers, construction skill and quality, work amounts, deconstruction, and roofs with their support radius and collapse — and what does each part imply for a true-3D layered game where a built roof is the floor of the layer above?

All content below is paraphrased from the RimWorld wiki (current as of game versions 1.5–1.6, read 2026-09-15). No Def XML or decompiled code was read or reproduced. Note to avoid confusion: RimWorld's own 2025 expansion is also called "Odyssey"; wiki references to it have nothing to do with this project.

## Findings

### 1. The build pipeline as a state machine

RimWorld's pipeline has three entity states plus terminal transitions. Bracketed items are not documented on the wiki and come from general play knowledge (flagged again under *Could not be determined*).

```
                     cancel (no loss)
                    ┌───────────────┐
 Architect menu     │               ▼
 pick def + stuff ──► BLUEPRINT ──────── materials hauled in ────► FRAME
 (free, instant)      ghost thing:                                 holds delivered resources,
                      def, rotation,                               accumulates work
                      chosen material                                │
                                                    work complete ──►│◄── cancel [materials refunded]
                                                                     │
                                                     roll Construct Success Chance
                                                       │success              │failure ("botched")
                                                       ▼                     ▼
                                                  BUILT THING            frame destroyed,
                                                  material stamped,      all work lost,
                                                  quality rolled now     some materials lost,
                                                  (if def has quality)   start again
```

Post-build transitions:

- **Deconstruct** (an order; only things flagged as buildings): refunds **50 % of build cost**, with a 50/50 random round-up/down on odd amounts (a 5-cost wall returns 2 or 3). Exceptions exist per def: a few special buildings refund 100 %, conduits/campfires/torches refund nothing, one building (biosculpter pod) refunds 25 %. Deconstruction work is derived from the thing's Work To Build, **clamped to 20–3,000 ticks**, executed at the pawn's Construction Speed, and grants a little Construction XP.
- **Violent destruction** yields less than deconstruction, per def: a destroyed wall drops nothing; a destroyed column drops 25 % of cost (5 of 20).
- **Repair**: damage (lost HP) is repaired by constructors at **zero resource cost** inside the home area. Breakdowns are a separate mechanic: they consume a component and use a separate Repair Success Chance stat.
- **Uninstall/reinstall**: defs flagged minifiable can be boxed into a movable item and re-placed via an install blueprint; walls, columns and other structures explicitly cannot.
- **Claim**: neutral/ownerless structures (map-generated ruins, dead faction bases) can be claimed, joining the home area, after which they are repaired, maintained and deconstructable like player buildings. The wiki explicitly recommends deconstructing map ruins for free materials.

Supporting mechanics:

- A separate **Plan** designation layer is pure annotation (no cost, no effect, auto-removed when built over).
- Placement is validated against **terrain affordance**: each material implies a required ground class — wooden wall needs light, steel/plasteel/precious-metal wall needs medium, stone wall needs heavy; bridges provide only light affordance (wooden walls yes, stone walls no).
- Some defs carry a **minimum Construction skill** (e.g. traps and precious-metal floor tiles need level 3); pawns below it cannot take the job.

### 2. Data shapes (structure only, paraphrased)

**Building def** — identity and category (which Architect tab); footprint size and rotatability; passability (impassable / pass-through-only / standable) and cover effectiveness; a base stat block (Max Hit Points, Work To Build, Flammability, Beauty, Market Value, and function stats like Rest Effectiveness or Door Opening Speed); an **"holds roof" flag** separating roof supports (walls, columns, natural rock) from everything else; edifice/block-wind flags; required terrain affordance; a cost expressed as *fixed ingredient list* (e.g. components) **plus** *a quantity of stuff* constrained to allowed **stuff categories**; minimum construction skill; research prerequisites; minifiable flag; whether it takes a quality component; and per-def deconstruct/destroy yield behaviour.

**Material ("stuff")** — an extension carried by a resource def: membership in one or more of the five stuff categories (**Stony, Metallic, Woody, Leathery, Fabric**; DLC adds oddities such as bioferrite); per-unit market value; commonality; a tint colour; a **small-volume flag** (silver, gold, jade — quantities count ×10, so a 5-stuff wall costs 50 units); and two stat maps: **multiplicative factors** and **additive offsets**, keyed by stat. The stats materials touch (columns of the wiki's material table): Beauty (factor and offset), Work To Make (factor), Work To Build (factor and offset), Max Hit Points (factor), Flammability (factor), Armour sharp/blunt/heat (factors), Insulation cold/heat (additive °C offsets), Melee blunt/sharp damage (factors), Melee cooldown (factor), Door Opening Speed (factor), Rest Effectiveness (factor).

**Quality** — a 7-value enum (Awful, Poor, Normal, Good, Excellent, Masterwork, Legendary) attached as an optional component. Furniture, art, weapons and apparel have it; walls, doors and production/power buildings do not.

**Roofs are not things.** A per-cell roof grid with four states — none, constructed, thin rock, thick rock ("overhead mountain") — with no HP, no material, no quality and no resource cost. Only work is spent to add or remove one.

### 3. The stuff formula (verified against worked examples)

```
final stat = (base stat of the def × material factor) + material offset
```

Cross-checked with the wiki's per-material wall table (wall base: 300 HP, 135 ticks work, 100 % flammability): granite (HP ×1.7, work ×6 +140) → 510 HP and 950 ticks; wood (×0.65, ×0.7) → 195 HP and 95 ticks; plasteel (×2.8, ×2.2) → 840 HP and 297 ticks; steel flammability 100 % × 0.4 = 40 %, stone × 0 = 0 %. The formula reproduces every row, so factor-then-offset per stat is the whole system.

Key structural materials (factor ×, offset +):

| Material | Value/unit | Work To Build | Max HP | Flammability | Beauty | Notes |
|---|---|---|---|---|---|---|
| Wood | 1.2 | ×0.7 | ×0.65 | ×1.0 | ×1 | fastest, burns |
| Steel | 1.9 | ×1.0 | ×1.0 | ×0.4 | ×1 | the baseline material |
| Plasteel | 9 | ×2.2 | ×2.8 | ×0 | ×1 | best HP |
| Uranium | 6 | ×1.9 | ×2.5 | ×0 | ×0.5 | second-best HP |
| Granite blocks | 0.9 | ×6 +140 | ×1.7 | ×0 | ×1 | best stone |
| Limestone blocks | 0.9 | ×6 +140 | ×1.55 | ×0 | ×1 | |
| Sandstone blocks | 0.9 | ×5 +140 | ×1.4 | ×0 | ×1.1 | cheapest stone work |
| Slate blocks | 0.9 | ×6 +140 | ×1.3 | ×0 | ×1.1 | |
| Marble blocks | 0.9 | ×5.5 +140 | ×1.2 | ×0 | ×1.35 +1 | beauty stone |
| Jade | 5 | ×5 | ×0.5 | ×0 | ×2.5 +10 | small volume (×10) |
| Silver | 1 | ×1 | ×0.7 | ×0.4 | ×2 +6 | small volume (×10) |
| Gold | 10 | ×0.9 | ×0.6 | ×0.4 | ×4 +20 | small volume (×10) |

Breadth examples: stony stuff also sets Door Opening Speed ×0.45 and Melee Cooldown ×1.3; wood gives +8 °C cold insulation on apparel; fabrics/leathers have armour and insulation factors but cannot be used for structures. Design intent is clear and cheap to copy: one small factor/offset table per material × one base stat block per def generates the entire combinatorial space, with stone trading build time (×5–6 **plus** a flat +140 ticks so even cheap stone things are slow) for fireproof bulk, wood trading HP for speed, precious metals buying beauty.

### 4. Construction skill, speed, success and quality

- ~~**Construction Speed** = 50 % at skill 0, +15 percentage points per level (skill 8 → 170 %, skill 20 → 350 %), further scaled by Manipulation and global work speed.~~ **Corrected 2026-09-17 by `work-speed-and-stats.md`: 30 % at skill 0, +8.75 points per level (skill 8 → 100 %, skill 20 → 205 %).** The two readings are decidable without a third source — every work-speed stat's slope is chosen so that **level 8 lands on exactly 100 %**, and the figures struck through here put it at 170 %. Nothing was built on the old number; it had never been read by any code.
- **Construct Success Chance** per completed build, by skill 0→8: 75, 80, 85, 87.5, 90, 92.5, 95, 97.5, **100 %** (capped at 100; skill 8+ never fails). Manipulation contributes at 30 % importance uncapped, Sight at 20 % importance capped at 100 %. Failure ("botch") wastes all work and some resources.
- **Base work amounts** (ticks; 60 ticks = 1 s): wall 135, barricade 320, animal bed 400, bookcase 500, bedroll 600, bed 800, battery 800, column 750, autodoor 1,100, billiards table 12,000, armchair 14,000, autocannon turret 15,000.
- **Costs**: wall 5 stuff, column 20 stuff (column deconstructs to 10, destroys to 5).
- **XP** accrues continuously while building (soft cap of 4,000 XP/day/skill applies as for all skills).
- **Quality is rolled once, at the moment of completion**, from the *finishing* pawn's relevant skill (Construction for buildings) on a bell-shaped distribution. Buildings record no author, so a low-skill pawn can do 99 % of the work and a master can finish it — a deliberate, exploitable property. Distribution by skill:

| Skill | Awful | Poor | Normal | Good | Excellent | Masterwork | Mean tier |
|---|---|---|---|---|---|---|---|
| 0 | 64.6 % | 30.2 % | 5.0 % | 0.2 % | 0.0 % | 0 % | 0.41 |
| 2 | 20.2 % | 53.2 % | 23.6 % | 3.0 % | 0.1 % | 0 % | 1.09 |
| 4 | 4.8 % | 45.2 % | 39.4 % | 10.0 % | 0.6 % | 0 % | 1.56 |
| 6 | 1.0 % | 24.3 % | 52.1 % | 20.4 % | 2.2 % | 0.02 % | 1.99 |
| 8 | 0.1 % | 9.0 % | 50.8 % | 33.5 % | 6.4 % | 0.15 % | 2.37 |
| 10 | 0.02 % | 3.3 % | 40.2 % | 43.8 % | 12.2 % | 0.44 % | 2.66 |
| 12 | 0 % | 1.0 % | 24.6 % | 52.6 % | 20.6 % | 1.2 % | 2.96 |
| 14 | 0 % | 0.4 % | 15.8 % | 54.3 % | 27.4 % | 2.1 % | 3.15 |
| 16 | 0 % | 0.1 % | 9.3 % | 52.4 % | 34.5 % | 3.7 % | 3.32 |
| 18 | 0 % | 0.04 % | 5.0 % | 47.6 % | 41.3 % | 6.1 % | 3.48 |
| 20 | 0 % | 0.01 % | 2.4 % | 37.4 % | 50.6 % | 9.5 % | 3.67 |

Legendary is never rolled naturally: it needs the **Inspired Creativity** inspiration (+2 tiers on the next quality item, minimum Normal even at skill 0) or the Ideology DLC production-specialist role. Quality multipliers, general: Beauty ×(−0.1, 0.5, 1, 2, 3, 5, 8) from Awful→Legendary (negative beauty not multiplied); Market Value ×(0.5, 0.75, 1, 1.25, 1.5, 2.5, 5) with absolute caps (+500 Good, +1000 Excellent, +2000 Masterwork, +3000 Legendary); Deterioration Rate ×(2, 1.5, 1, 0.8, 0.6, 0.3, 0.1). Building-specific: Comfort ×0.76→×1.70, Rest Effectiveness ×0.86→×1.60, Surgery Success ×0.90→×1.30, Recreation Power ×0.76→×1.80.

### 5. Roofs: support radius, thin vs thick, collapse

- **Support rule (two conditions)**: a roof cell must (a) lie within **6 cells** of a roof-holding structure — wall, column, natural rock wall, or any fully impassable edifice — in a "roughly circular" area, and (b) be **adjacent to another roof cell or a roof-bearing structure** (roofs grow inward from supported edges; no floating discs). A room with interior width ≤ 12 therefore roofs completely; wider spans need interior columns/wall stubs. A lone column supports the same radius as a wall at 4× the cost, so lone wall pieces are the meta.
- **Building/removing**: roofs cost **no resources**; **65 ticks** of work per cell, at the pawn's Construction Speed ×1.7. Constructing one cell also finishes all adjacent designated cells — up to **9 cells (3×3) per action**; removal is strictly one cell at a time. Zoning: *build-roof area* (auto-applied over a newly enclosed room, up to a maximum size; the Structure page states rooms over **300 cells** are not auto-roofed), *remove-roof area*, *ignore-roof area*.
- **Three types**: **constructed** (player-built, rebuildable; drop pods and mortar shells break through it, causing minor collapse damage); **thin rock** (natural, at mountain edges; removable like constructed but can never be rebuilt as natural); **thick rock / overhead mountain** (cannot be removed by any means; blocks transport-pod landings and erases mortar shells; buffers temperature; enables infestations).
- **Collapse trigger**: when the **last support within 6 cells** is removed — by mining, deconstruction or destruction — the dependent roof falls and the game pauses with a notification. Exception: removing the last *roof tile* that connects an unsupported section makes that section silently vanish with **no damage** — so peeling the outer ring of roof lets the interior evaporate safely, and a remove-roof area is the sanctioned way to defuse a collapse.
- **Collapse damage**: all collapses deal Crush damage. Constructed/thin rock: **15–30 damage, 0 % armour penetration**, aimed at the *top* of a pawn (neck and head and their children) — roughly a third of unarmoured hits destroy the head or neck outright, but helmets nearly neutralise it; roof removed, building/rock rubble filth left. Thick rock: **99,999 damage at 999 % AP** — annihilates everything, corpses and gear unrecoverable — and spawns **collapsed rocks** (mineable wall-like things) while the thick roof itself persists, so it can collapse again indefinitely.
- **Why roofs matter** (coupling points other systems read from the roof grid): light level; rain/snow/lightning blocking; item deterioration; room temperature (a room ≥ 75 % roofed holds temperature, below that it snaps to outdoors; < 300 unroofed cells for "roofed" status); electrics catching fire in rain when unroofed; mortars/scanners needing open sky; trees refusing to grow (or be roofed over). Players weaponise collapse deliberately (shooting out a pre-weakened column over a killbox).

### 6. Tuning constants, collected

| Constant | Value |
|---|---|
| Roof support radius | 6 cells; max fully-roofed interior span 12 |
| Roof connectivity | each roof cell adjacent to roof or support |
| Roof work | 65 ticks/cell; speed = Construction Speed ×1.7; 9 cells per action; removal 1 cell/action |
| Auto-roof room limit | 300 cells |
| Room temperature seal | ≥ 75 % roofed |
| Thin/constructed collapse | 15–30 Crush, 0 % AP, targets head/neck; rubble filth |
| Thick collapse | 99,999 Crush, 999 % AP, spawns collapsed rock, roof persists |
| Construction Speed | 50 % + 15 pts/skill level |
| Construct Success Chance | 75 % at 0 → 100 % at skill 8 (+5, +5, +2.5 …); manipulation 30 % importance uncapped, sight 20 % capped |
| Deconstruct refund | 50 % (stochastic rounding); per-def overrides 0 % / 25 % / 100 % |
| Deconstruct work | Work To Build clamped to [20, 3000] ticks |
| Destroy yield | less than deconstruct, per def (wall 0 %, column 25 %) |
| Small-volume materials | silver/gold/jade cost ×10 units |
| Stuff formula | stat = base × factor + offset, per stat |
| Stone work penalty | factor ×5–6 **plus** flat +140 ticks |
| Wall | 5 stuff, 135 ticks, 300 HP, 75 % cover, holds roof |
| Column | 20 stuff, 750 ticks, 160 HP, passable, 25 % cover, holds roof |
| Quality tiers | 7; rolled once at completion from finisher's skill; Legendary needs inspiration/role; inspiration = +2 tiers, min Normal |
| Daily XP soft cap | 4,000/skill/day (excess ×20 %) |

## 3D/layer impact

RimWorld's roof is a massless per-cell annotation with no material, no HP and no cost, which is exactly what Odyssey cannot keep: the brief makes a built roof the floor of the layer above, so the roof grid and the floor grid of layer z+1 must become the same object — a **slab** owned by the boundary between z and z+1, built as a thing with stuff (material factors give it HP, flammability, beauty and work exactly as walls get them), read downward by weather/light/temperature as "roof of z" and upward by pathing/room-stats as "floor of z+1". That single change forces the second one: RimWorld's support rule is a pure 2D query — a roof cell is fine if any roof-holding edifice stands within 6 cells horizontally and the roof is edge-connected to a support — and it never asks what the support itself stands on, because in one layer every wall is implicitly grounded. In 3D the recursion is the whole feature: a wall or column at layer z is a *valid support* only if it stands on the ground or on a supported slab; a slab over z is supported only if a valid support at z lies within R cells. Removing one ground-floor wall can then cascade — the slab above fails, the walls standing on that slab become invalid, their slab fails — and collapse must be processed bottom-up, with each failing cell converting to Crush damage plus rubble on the first solid surface below, RimWorld's 15–30-damage-to-the-head model applied once per fallen layer (falling debris through a stairwell shaft is the same event pipeline). The strong recommendation is to keep the rule **binary and geometric** as RimWorld does — supported/unsupported by distance-to-valid-support, no load numbers, no stress solver — because it is incrementally updatable (recompute only the affected radius on build/destroy events, which is visibly what RimWorld does), legible to players, and Going Medieval demonstrates the fiction survives the simplification; the one enrichment worth taking from Going Medieval rather than RimWorld is a **material-dependent support radius** (say wood 4, stone 5, steel 6 at our 2.5 m cell), because RimWorld's flat 6 means material choice never matters structurally, and in a game about towers it should. Slab construction should stay batched (RimWorld's 9-cells-per-action) and auto-designated over newly enclosed rooms (its 300-cell cap), but must cost materials and work like any built thing — the free-roof economy only worked because roofs were not floors. RimWorld's thin/thick distinction maps cleanly to slabs vs monolith: ordinary slabs behave like constructed/thin roof (removable, collapsible, modest damage), while an "overhead mountain" equivalent — indestructible mass that erases anything under its collapse and can never be removed — is the right engine concept for bedrock, foundation rafts and perhaps the cores of fallen megastructures where we want a hard "do not dig" guarantee.

## Ruined-city impact

RimWorld already ships the entire reclaim loop Odyssey needs, in miniature. Map generation stamps ownerless ruins; the **Claim** order converts them to colony property (auto-added to home area, then repaired, maintained and fire-fought like anything else); **repairing damage costs zero resources**, only constructor time; **deconstruction refunds 50 %** of notional build cost, and the wiki explicitly recommends stripping map ruins for free materials — in a city map this becomes the primary resource loop, the replacement for mining ore (layer question 11): rubble and standing structure *are* the ore body, and the 50 %-with-stochastic-rounding refund is a proven, tuned constant to start from. Three ruin-specific consequences of the support model: (1) the generator must emit shells that satisfy the support rule, or the mapgen step should run the collapse solver once and let whatever fails fall — which is not a bug but free set dressing, producing honestly half-collapsed towers whose remaining floors are guaranteed structurally consistent with the player-facing rules; (2) ruined shells should spawn with fractional HP so reclaiming is a time-cost (free repair) rather than a material cost, keeping the early game about labour allocation exactly as RimWorld's is; (3) because collapse is player-legible and weaponisable in RimWorld (columns shot out over killboxes), a ruined city can invert it — pre-weakened supports under salvage-rich floors, raiders or the storyteller triggering pancake collapses — giving verticality a threat dimension RimWorld can only fake with mountain roofs. Thin-rock roof's "removable but never rebuildable as natural" property is also the right template for pre-Fall monolithic floor plates: reclaimable in place, but once demolished they can only be replaced by the colony's own (weaker, flammable, material-typed) slabs.

## Layer questions touched

**Q2 — Is a roof a floor? What supports it, what collapses, and how do pre-existing ruined shells fit the support model?** Answered from evidence. In RimWorld a roof is *not* a floor: it is a free, massless, material-less per-cell annotation (none/constructed/thin rock/thick rock), walkable by no one, supported by any roof-holding edifice within 6 cells plus edge-connectivity, and collapse is a binary event (support removed → 15–30 Crush at 0 % AP for thin/constructed, obliteration plus collapsed rock for thick; unsupported roof removed *by deconstruction* vanishes harmlessly). For Odyssey the brief's answer stands and RimWorld's evidence tells us how to build it: promote the roof to a **slab thing** that is simultaneously the roof of z and the floor of z+1, give it stuff/HP/work via the verified `base × factor + offset` formula, and make support **recursive** — a support is valid only if grounded through supported slabs below, with a binary distance-R query per layer (no load solver), bottom-up cascade collapse, and rubble deposited on the first solid surface below. Pre-existing ruined shells fit by construction: the generator either emits support-valid geometry or runs the solver once at mapgen and keeps only what stands, and the claim → free-repair → 50 %-refund-deconstruct loop (all stock RimWorld) is the reclaim model. **Q11 (touched)**: deconstruction refunds position demolition as the city's ore. **Q12 (touched)**: M3 must prove slab-as-floor, recursive support and cascade collapse; material-dependent radius and partial-HP ruin spawning can be tuned later, but the recursive rule itself cannot be stubbed — RimWorld shows every other system (temperature, light, weather, deterioration) reads the roof grid, so its shape must be right from the first commit.

## Sources

- https://rimworldwiki.com/wiki/Roof
- https://rimworldwiki.com/wiki/Wall
- https://rimworldwiki.com/wiki/Column
- https://rimworldwiki.com/wiki/Structure
- https://rimworldwiki.com/wiki/Construction
- https://rimworldwiki.com/wiki/Construct_Success_Chance
- https://rimworldwiki.com/wiki/Work_To_Build
- https://rimworldwiki.com/wiki/Material
- https://rimworldwiki.com/wiki/Quality
- https://rimworldwiki.com/wiki/Orders
- https://rimworldwiki.com/wiki/Deconstruct

## Confidence

**High** for roofs (radius, types, collapse damage, work costs), the stuff factor/offset formula (cross-verified: the per-material wall table reproduces exactly from base stats × material factors), quality tiers/distributions/multipliers, construct success and speed curves, and deconstruction refunds — all read directly from current wiki pages, several cross-confirming each other. **Medium** for the blueprint→frame internals (frame HP, cancel refunds, botch loss fraction): the wiki has no Blueprint or Frame article, so the state machine's skeleton is wiki-confirmed but those specific transitions rest on general play knowledge. No decompiled source was consulted.

## Could not be determined

- The exact material-loss fraction on a botched construction (wiki says only "some resources going to waste").
- Whether cancelling a frame refunds 100 % of delivered materials (believed yes; unverified).
- Frame properties: HP, flammability, whether HP scales with build progress.
- The precise auto-roof room-size cap (Roof page has it flagged "[What size?]"; Structure page says rooms over 300 cells are not auto-roofed — taken as the working value).
- Whether the 6-cell support radius is Euclidean or Chebyshev ("roughly circular" suggests a radial mask, not a square).
- The construction XP rate (the wiki's "roughly 82 XP per point of work" is dimensionally inconsistent with the leveling table; treat as unreliable).
- The market-value formula for stuffed items (ingredient value + work term; the dedicated stat page was not consulted within the read budget).
