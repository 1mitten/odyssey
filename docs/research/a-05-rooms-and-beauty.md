# A5 — rooms and beauty

## Question

How does the reference game (and its neighbours) find a room, keep it correct as walls change,
infer what the room is *for*, and turn its condition into pawn mood? Formulas and thresholds where
they are public. Then: **layer question 3, the rooms half** — do rooms span layers, or is our
recorded decision (per-layer flood fill, stairwells connect but do not merge) the right one? And
does the design position that **rooms are a separate atmosphere grouping over the same region
substrate** survive contact with how these games actually do it?

Hard cap observed: 8 searches, 8 page reads.

## Findings

### 1. Detection is a grouping over the pathfinding substrate, not a separate flood fill

This is the single most useful structural finding, and it validates `d-04-pathfinding.md` rather
than competing with it.

- The reference game divides the map into **12 x 12 chunks** at map generation. A region is a
  contiguous passable area *clipped to one chunk*; it never crosses a chunk boundary, and it is
  subdivided further if the area inside the chunk is not contiguous. Impassable things (walls,
  natural rock) belong to no region and split the regions around them. Regions fuse again when the
  obstruction goes.
- A **room is a set of regions**, capped at **36 regions**. That cap is where the famous room-size
  limit comes from: the theoretical maximum is 72 x 72 = **5,184 cells**, and the practical maximum
  before complex shapes start tripping the cap is about **50 x 50**. Note what this means — the
  region substrate's granularity leaks into the room layer as a hard design limit.
- Maintenance is **dirty-cell driven and batched**, not immediate. Building or destroying something
  marks cells dirty; a single updater then regenerates regions from the dirty cells, creates or
  updates the rooms over them, and marks everything clean. The updater can be *disabled* for the
  duration of a bulk map edit (worldgen, a large paste) and rebuilt once at the end — modders hit
  this as "region and room updater is disabled" warnings when they query rooms mid-edit. We should
  copy the disable/rebuild window in spirit: worldgen must not rebuild rooms 60,000 times.
- There are in fact **two levels of grouping above regions**, not one. The tight grouping is bounded
  by *anything impassable including doors*; the loose grouping joins those through doors and is what
  temperature and most stats actually run on. The two were renamed relative to each other in a later
  version (the old "room group" became "district"), which is why community sources use the words
  inconsistently. The important part is the **shape**: regions to hard-bounded cell groups to a
  looser grouping that unions them across portals. That three-level shape is exactly the seam we
  need for layers (see §9).

### 2. "Enclosed" is not one boolean — the reference game keeps three, with different thresholds

They deliberately do not agree, and each exists because a different system needed a different
answer:

| Test | Fires when | Governs |
|---|---|---|
| outdoor temperature | 25% or more unroofed, **or** touching the map edge | temperature, deterioration, plant dormancy |
| outdoors for work | more than 25% unroofed, **or** more than 100 unroofed cells | surgery / research / assembly speed, throne-room validity |
| psychologically outdoors | 300 or more unroofed cells, **or** 50%+ unroofed *and* open to the map edge | beauty, recreation, ritual sites |

A separate statement puts the temperature cut at "75% roofed or less stays at outdoor temperature",
which is the same 25% rule from the other side. The lesson for us is not the numbers, it is that a
single `IsEnclosed` flag will be wrong for somebody within a milestone. A walled, unroofed courtyard
is emphatically a room for beauty and emphatically not one for temperature.

### 3. Role inference: a scoring contest, and the scores are really priority bands

Roles are *inferred from contents*, never declared by the player. Every candidate role scores the
room's contents; the highest score is the displayed role. The magnitudes are not tuning — they are
bands, and a band gap of 10x means "this always wins":

| Band | Role | Score |
|---|---|---|
| absolute | prison cell / prison barracks | 170,000 |
| absolute | bedroom, hospital, barracks, nursery | ~100,000 (barracks 100,100 *per bed*; nursery 100,200 per crib, minimum 2) |
| strong | throne room | 10,000 |
| strong | temple | 75 per altar, minimum 2,000 |
| ordinary | laboratory 60/bench, kitchen 28/stove, workshop 27/bench, tomb 50/sarcophagus | per item |
| weak | dining 12/table, playroom and classroom 8 each (2+ items), barn 7.6/animal bed, rec 7/item, storeroom 1/shelf | per item |
| floor | plain room | 0.99 |

Three rules ride on top:

- **Validity guards beat score.** A bedroom scores its 100,000 only if the beds are non-medical,
  non-prisoner, and exactly one is assigned; a hospital scores zero if a prisoner bed is present.
  The guard is where "why is my hospital a prison cell" bugs live.
- **A room can serve several roles for mood even though it displays one.** A room with a bed and a
  table is a bedroom *and* a dining room, and both moodlets can fire.
- **The role gates work speed.** 19 production buildings take an **80% work-speed penalty** when
  used outside a room of the matching role. This is the mechanism that makes role inference a
  *gameplay* system rather than a label.

**Two contrasting designs are worth knowing.** Oxygen Not Included infers roles from required
buildings *plus a size window* and *forbidden* buildings — e.g. private bedroom 24–64 cells, exactly
one comfy bed, two or more decor items, no industrial machinery; great hall 32–120 cells, a table,
20+ decor with at least one item rated 20 or better, and a recreation building; morale bonuses run
+1 to +6. When a space satisfies several types it does **not** pick the best — it degrades to
"miscellaneous" with no bonus until the player specialises it. That is a much harsher rule, and it
generates a steady stream of "my lab is not detected" support threads. Dwarf Fortress does no
inference at all: the player designates a room *from a furniture item*, and it expands as a
rectangle until it meets walls or external doors, capped at 60 x 60.

Ranked for us: the scoring contest is right; ONI's forbidden-contents clause is a good cheap
addition (it lets a kitchen stop being a kitchen because a corpse pile is in it); ONI's
ambiguity-demotion is wrong (punishing the player for a room that is too good is not a lesson we
want to teach).

### 4. Impressiveness: a min-weighted average of four normalised stats, with a space cap

Four stats feed one composite. The published formula is four steps:

1. **Normalise.** wealth / 1500; beauty / 3; space / 125; cleanliness becomes `1 + cleanliness/2.5`.
   (So one "unit" costs 1,500 wealth, 3 beauty, 125 space, or 2.5 cleanliness.)
2. **Compress** anything outside −1..1 logarithmically: `m = 1 + ln(b)` for `b >= 1`, and the
   negative mirror for `b <= −1`; inside the band, unchanged.
3. **Weight towards the worst:** `I = 65 x mean(m) + 35 x min(m)`. The *lowest* stat therefore
   carries **51.25%** of the result and each of the other three carries 16.25%.
4. **Space soft-cap:** if `I > 500 x m_space`, blend `I' = 0.25 I + 0.75 x (500 x m_space)`.

Tiers and mood, bedrooms and dining/rec rooms:

| Score | Tier | Bedroom | Dining / rec |
|---|---|---|---|
| under 20 | awful | −2 | — |
| 20–29 | dull | 0 | — |
| 30–39 | mediocre | +1 | — |
| 40–49 | decent | +2 | +2 |
| 50–64 | slightly impressive | +3 | +3 |
| 65–84 | somewhat | +4 | +4 |
| 85–119 | very | +5 | +5 |
| 120–169 | extremely | +6 | +6 |
| 170–239 | unbelievably | +7 | +7 |
| 240+ | wondrously | +8 | +8 |

The design intent behind step 3 is the part worth stealing: **the weakest stat dominates**, so the
player's next action is always obvious ("it is filthy" / "it is tiny"), and stacking gold statues in
a cupboard cannot buy a good room. Step 4 exists to enforce that specifically. Step 2 stops a
million-silver throne room running away with the scale.

### 5. Beauty has two completely separate meanings and we must not conflate them

- **Room beauty** is an average over the room's cells *plus its bounding walls and doors*, with a
  small-room penalty baked into the divisor: `roomBeauty = totalBeauty / weightedSize`, where
  `weightedSize = size` if `size > 40`, else `20 + size/2`. So a 10-cell closet is divided by 25,
  not 10 — you cannot make a broom cupboard beautiful by putting one nice thing in it. Tiers:
  hideous below −3.5; ugly −3.5..0; neutral 0–2.4; pretty 2.4–5; beautiful 5–15; very beautiful
  15–50; extremely 50–100; unbelievable above 100.
- **Perceived beauty** is a *pawn-local* scan: roughly an 8-cell radius, about 241 cells, averaged
  over what is in line of sight, so walls and doors shrink the effective sample. It feeds a need
  whose thought runs **−15 to +15**: 6+ gives +15 (100%), 4.5+ gives +10, 2.5+ gives +5, −0.5..2.5
  gives 0 (the neutral band), −3.5..−0.5 gives −5, and below that −10 then −15.

Item values are small integers: bare floor −1, smoothed floor or carpet about +2, smoothed wall +2,
marble +1, jade wall +10. Filth is where the numbers get violent, and **indoors is punished harder
than outdoors**: dirt −15 indoors / −5 outdoors, blood −30 / −9, insect blood −40 / −12. (One wiki
page quotes the outdoor figures as if they were the only ones; the filth page gives both. Assume an
indoor/outdoor multiplier exists.)

### 6. Cleanliness is per-cell terrain plus filth, and it buys almost everything except mood directly

Room cleanliness is the average per-cell cleanliness over the room. Terrain contributes a fixed
figure — sterile tile +0.6, metal floors +0.2, ordinary constructed and stone 0, straw matting −0.1,
soil/gravel −1.0, marsh/mud −2.0 — and each piece of filth subtracts: dirt/rubble −5, blood −10,
vomit/insect blood/fuel −15, corpse bile −20. Tiers: very dirty below −1.1; dirty −1.1..−0.4;
slightly dirty −0.4..−0.05; clean −0.05..0.4; sterile 0.4 and above.

Cleanliness feeds **surgery success, research speed, food poisoning chance, infection chance and
gene-assembly speed** directly, and reaches mood only through the composite. That indirection is
deliberate and good: cleaning is worth doing for hard reasons, and the mood effect arrives as "this
room is grim", not as a separate nag.

**Filth dynamics** close the loop: pawns track dirt off soil onto built floors, animals dirty
floors, and bleeding, vomiting, burning, corpse rot, births and weapon impacts all spawn filth.
Cleaning costs **35–160 work units** per piece depending on type, metal and sterile floors clean
faster, and filth decays on its own over **5–50 days**; rain washes some types away where unroofed.
That is a self-balancing chore generator, and it is the reason a cleaning work type exists at all.

### 7. Mood coupling is event-driven, not continuous

The moodlet is applied when a pawn *begins the activity* in the room — sleeping, eating at a table,
playing a game — and lasts **24 hours**. Nobody gets a bedroom thought for walking through a
bedroom. Traits bend it: greedy pawns lose mood without an impressive bedroom, jealous pawns lose
mood when another pawn's room is noticeably better, ascetic pawns lose mood when a room is *too*
impressive. (The 24-hour figure is flagged "verify" on the wiki; treat it as the shape, not gospel.)

Dwarf Fortress prices the same thing as a single scalar: room **value** from floor, walls, furniture
and decoration, banded meagre 1, modest 100, standard 250, decent 500, fine 1,000, great 1,500,
grand 2,500, royal 10,000 — with a brutal **−75% to each room whose floor area overlaps another**
(shared walls are free, and the penalty applies once however many rooms overlap). Nobles demand
minimum bands. It is a cruder model but it makes the same point our composite should: a room is
judged as a whole thing, and cheating it — overlapping claims, a cupboard full of gold — is
explicitly taxed.

### 8. What the layered neighbours actually do

- **Dwarf Fortress**, which has had true z-levels since 2007, is unambiguous: *rooms cannot span
  z-levels; when you define a room it can only be on a single level.* No hedging, no special case
  for stairwells, after eighteen years of shipping.
- **Going Medieval**, the closest analogue to us (Unity, voxel, multi-storey, RimWorld-derived),
  **treats each level as its own room**, to the point that players are advised to put a door between
  floors so the game recognises them as distinct spaces. The artefacts are visible and players talk
  about them: a three-storey open hall could not be heated — "all the heat disappearing on the 3rd
  floor" — six braziers per floor plus firepits below kept it merely freezing, and splitting the
  same space into separate floors jumped it to 12 °C and above. Two-storey rooms are reported fine;
  three is where it breaks. Separately, players complain the room quality band appears to track size
  and ignore the gold furniture they crammed in — which, read against §4, is exactly what a
  min-weighted composite with a space term does when space is the weakest stat, and it reads as a
  bug to them because nothing explains it.
- **The reference game is 2D**, so it contributes no evidence here at all. Its per-room temperature
  does tell us the *mechanism* we would use for openings: walls equalise passively (a second wall
  layer halves the rate, a third does nothing, material is irrelevant), while **open doors, vents
  and coolers keep the rooms separate as rooms but equalise temperature at a very high rate**. That
  is precisely "a stairwell connects two rooms without merging them", already proven in 2D.

So: both games that have layers made the same call we made, independently, and one of them shows us
exactly what it costs.

### 9. What the separation costs us, concretely

Our substrate is chunk-local typed regions, 10 x 10 x 1, never spanning layers, with
per-traverse-mode district ids. Rooms as a second grouping over those regions is what the reference
game does, and it is the reason its room rebuild is cheap. Four costs, all nameable:

1. **The boundary predicates differ.** A region is bounded by *impassable to this traverse mode*; a
   room is bounded by *does not pass air*. A door is passable for a pawn and a boundary for a room.
   The reference game pays for this by making each door its own single-cell region, so the room
   layer can split on a region boundary that already exists. **We must adopt the same rule: a portal
   cell (door, hatch, vent) is its own region.** That is one line in the region builder and it is
   the entire price of the separation.
2. **The region cap leaks.** Their 36-region cap becomes a 5,184-cell room limit. At 10 x 10 our
   regions are smaller, so the equivalent cap bites sooner for the same region budget; either size
   the cap in cells directly, or accept an explicit "this space is too large to be a room" state and
   show it in the interface rather than silently misbehaving.
3. **Rooms need data regions do not carry** — the slab above each cell, filth, terrain cleanliness,
   building contents. Those are separate grids with separate dirty flags. Fine, but it means rooms
   are genuinely their own subsystem with their own update, not a field on the region.
4. **Two dirty channels, not one.** A wall change needs a **re-flood**; a chair, a floor, a puddle of
   filth needs only a **re-stat**. Conflating them turns every dropped item into a flood fill. This
   is the cheapest large win available in the whole feature.

Scale check for us: at 250 x 250 x 40 a single layer is 62,500 cells, so even a worst-case
whole-layer re-flood is a few tens of microseconds of integer work, and an incremental rebuild from
dirty regions is a small multiple of the touched region count. Rooms are not going to be a
performance problem; A-star still is.

### 10. Determinism hazard, specific to us

The impressiveness formula uses a natural logarithm. Our state hash covers the world, mood drives
behaviour, and our cross-runtime determinism is a standing test under both Mono and CoreCLR. **A
`Math.Log` in the mood path is a cross-runtime hash risk** — floating-point transcendentals are not
required to be bit-identical across runtimes and JITs. Either compute the composite in fixed point
with an integer piecewise-linear compression curve, or compute it in floats but quantise to an
integer band before anything simulated reads it. Recommend the second: the *tier* is what mood
consumes, and a tier is an integer.

## Recommendation

**Keep rooms per layer. Build the cross-layer link graph now, and defer true volumes indefinitely.**

Ranked:

1. **Per-layer rooms plus a typed opening graph (back this).** A room — propose the name
   **chamber** — is a flood fill within one layer over the region substrate, bounded by walls, the
   map edge and portal cells, and "sheltered" when enough of it has a slab above. Every boundary
   that lets air through becomes a typed **opening** record on a chamber-adjacency graph: *door*,
   *vent*, *stairwell*, *hatch*, *breach* (a missing or broken slab), each with a flow coefficient.
   Stairwells and breaches are simply openings whose two chambers sit on different layers. Room
   stats, roles and mood all evaluate per chamber; temperature, gas and light flow *along* the
   graph. This is what both layered neighbours ship, it is what the 2D reference already proves for
   doors, and it costs one extra data structure that both alternatives need anyway.
2. **True 3D volumetric rooms.** A single flood fill through vertical openings. Attractive for a
   two-storey atrium and nothing else. It makes every stat scale-dependent in a way the player
   cannot see (whose beauty is the gallery's?), it makes the "outdoors" tests ambiguous when one
   part of the volume is open to the sky, it merges a cellar into a kitchen the moment somebody cuts
   a hatch, and it deletes the per-layer cheapness that makes detection affordable. Dwarf Fortress
   has declined to do this for eighteen years.
3. **Volumetric rooms for atmosphere, per-layer rooms for stats, detected separately.** Two
   independent flood fills. Rejected: it is option 1 with the link graph replaced by a second full
   detection pass — twice the invalidation surface and two things to get out of step.

**If the tie between 1 and 2 needs breaking, the observation is: does an *open* multi-storey
interior occur in our game, and does the player build it deliberately?** In a ruined sci-fi city
with collapsed floors it occurs by accident constantly. The cheapest experiment is not code: stand a
two-storey atrium in the existing meadow test map (remove one 4 x 4 patch of slab), inspect the two
chambers' stats side by side, and ask whether "upper gallery: cramped, awful" reads as a bug.
**Predicted answer: it reads as a bug, and the fix is a stat-time union, not a detection change** —
when a chamber's floor is missing over more than about a third of its area and the chamber directly
below shares that footprint, sum their space and average their beauty for *stat* purposes only,
under a name like **hall**. Detection stays per layer; only the scoring sees the union. Defer until
the atrium exists, exactly as `03-systems-catalogue.md` already says.

**Adopt from the formulas, with our own constants:**

- The **min-weighted composite** — `65 x mean + 35 x min` over four normalised stats — because it
  makes the weakest stat carry over half the result, so the player always knows what to fix. Propose
  our stat names **space, beauty, order, value** and the composite **grandeur**, with integer tiers.
- The **space soft cap**, because it is the single rule that stops a gilded cupboard.
- The **small-room divisor** on beauty (`weightedSize = max(size, 20 + size/2)` in shape), same
  reason.
- The **role scoring contest with validity guards**, plus ONI's **forbidden-contents** clause; not
  ONI's demote-on-ambiguity.
- **Cleanliness reaching mood only through the composite**, while paying out directly in work speed
  and medical outcomes.
- **Event-driven moodlets on a 24-hour memory**, applied when the pawn starts the activity.

**Reject:** a single `IsEnclosed` boolean (keep two — `IsSheltered` for weather and temperature,
`IsIndoors` for mood and work speed — and expect to want a third); a logarithm anywhere a hashed
value can see it; and continuous per-tick restatting (rooms restat on a rare-tick cadence or on an
explicit dirty flag, never on every filth spawn).

**Milestone fit.** M3 needs only chamber detection plus `IsSheltered` — the flood fill and the slab
test, no stats, no roles. M4 adds the four stats, grandeur, roles and the moodlets. The opening
graph should land at M3 with detection, unused, because M4's temperature is its first consumer and
retrofitting it later means touching detection again.

## Layer questions touched

**Layer question 3, rooms half: rooms do not span layers. The recorded decision stands, and is now
evidenced rather than asserted.** Dwarf Fortress states the rule outright after eighteen years of
z-levels; Going Medieval, the closest structural analogue to Odyssey, independently made the same
call and instructs players to door off each floor. Neither has walked it back. The one real cost is
visible in Going Medieval's community: a tall open hall behaves oddly (heat "vanishes" above two
storeys) and a room's quality band appears to ignore what the player put in it. Both are
presentation-and-explanation failures more than model failures, and both are addressed by the
stat-time union above without touching detection.

The stairwell clause survives too, and gets support from an unexpected place: the 2D reference
already implements "connected but not merged" for doors and vents — they keep the rooms separate as
rooms while equalising temperature at a very high rate. Our stairwell is that, rotated ninety
degrees.

**Regions-as-substrate: the separation holds, and it is not merely compatible with the reference
design — it *is* the reference design.** Regions are the substrate; a hard-bounded cell group sits
over them; a looser grouping unions those across portals. Our three levels (regions, chambers, the
opening graph) are the same shape with the third level made explicit instead of implicit. Note that
our regions never spanning layers does **not** force per-layer rooms — a room could perfectly well
be a union of regions drawn from several layers — so the decision is genuinely about design, not
about what the substrate permits. It costs us the four things listed in §9, of which only one is
structural: **a portal cell must be its own region**, so the room layer can split on a boundary the
region builder has already drawn. Put that rule in the region builder before anything depends on it.

## Sources

- https://rimworldwiki.com/wiki/Rooms — the primary source: flood-fill detection, the 36-region /
  5,184-cell cap, the three enclosure tests with their thresholds, the full role scoring table, and
  every room stat's tier table.
- https://rimworldwiki.com/wiki/Impressiveness — the four-step composite formula, the 65/35
  weighting (51.25% to the worst stat), the space soft cap, and the tier-to-mood table.
- https://rimworldwiki.com/wiki/Room_stats — the mood offsets per tier for bedrooms and dining/rec
  rooms, and the "moodlet applied when the activity begins, lasts 24 h" rule (flagged for
  verification on the page itself).
- https://rimworldwiki.com/wiki/Beauty — the pawn-local beauty scan (about an 8-cell radius, about
  241 cells, line-of-sight averaged) and the −15..+15 need thresholds.
- https://rimworldwiki.com/wiki/Filth — filth sources, 35–160 work units to clean, 5–50 day natural
  decay, and the indoor/outdoor beauty asymmetry (dirt −15/−5, blood −30/−9).
- https://rimworldwiki.com/wiki/Temperature — per-room temperature, wall equalisation (a second
  layer halves it, a third does nothing, material irrelevant), and doors/vents keeping rooms
  separate while equalising fast: the mechanism our stairwell clause needs.
- https://dwarffortresswiki.org/index.php/DF2014:Room — explicit "rooms cannot span z-levels",
  designation-from-furniture, the 60 x 60 cap, the value bands 1–10,000 and the −75% overlap
  penalty.
- https://oxygennotincluded.wiki.gg/wiki/Room_Overlay — the contrasting role model: size windows
  (12–64, 12–96, 32–120), required *and forbidden* buildings, morale +1 to +6, and demotion to
  "miscellaneous" on ambiguity.
- https://steamcommunity.com/app/1029780/discussions/0/6832610053152554542/ — Going Medieval players
  measuring the per-floor room artefact: a three-storey hall unheatable, the same volume split into
  floors jumping to 12 °C and above, and the "size seems to be the only relevant factor" complaint
  about room quality.
- https://www.thegamer.com/rimworld-all-room-types-requirements-furniture/ — secondary confirmation
  of the role list and that one room can hold several roles at once.

## Confidence

**High** on the room stat formulas, tier thresholds and the numeric tables in §2, §4, §5 and §6 —
they come from the maintained wiki, are internally consistent across three of its pages, and the
composite formula is given step by step with the weighting derived rather than asserted.

**High** on the layer answer. Two independent shipped games with true vertical layers both keep
rooms per layer, one of them states it as a rule and the other is observable in player reports; and
the cost of that choice is documented rather than hypothetical.

**Medium-high** on the detection and maintenance architecture in §1 — the 12 x 12 chunking, the
36-region cap, the dirty-cell batched rebuild and the disable/rebuild window are all corroborated by
the wiki and by modder-facing symptoms, but the authoritative implementation was deliberately not
read (see below).

**Medium** on the two-level grouping and its renaming history: the *shape* is well attested by
behaviour (temperature crossing doors while rooms stay separate), the naming is community-sourced
and inconsistent across versions.

**Medium** on the Going Medieval specifics — player experiment threads, not developer statements.

**Low** on the mood offsets for roles other than bedroom, dining and rec: the wiki itself marks the
table incomplete and flags it for verification.

## Could not be determined

- **The mood offsets for most room roles.** Throne room, deathrest chamber, prison cell and barracks
  are all stated to produce mood, but no source gives the per-tier numbers; the wiki marks its own
  table incomplete. We will have to invent these, which is fine, but it means no external oracle for
  them.
- **Whether the 24-hour moodlet duration is exact**, and whether re-entering the room refreshes the
  memory or extends it. The wiki flags the figure itself for verification.
- **The exact incremental algorithm** — how a room is split when a wall bisects it and how two rooms
  are merged when a wall comes down, including which room keeps its identity (and therefore its
  assignments and its name). Only the public entry points and the dirty/clean cycle are documented.
  **This was a deliberate stop:** the authoritative answer exists only in a decompiled-source
  mirror, and the clean-room rule forbids reading it. We will design the merge/split identity rule
  ourselves; the obvious choice is "the larger surviving fragment keeps the identity, the smaller is
  born new".
- **How role scores interact with the multi-role mood rule.** We know one role displays and that a
  bed-and-table room counts as both; we do not know whether every role whose test passes fires its
  moodlet, or only some ranked subset.
- **Whether filth beauty really carries an indoor/outdoor multiplier or two independently authored
  values.** Two wiki pages quote different figures for the same filth type, which is most simply
  explained by an indoor/outdoor split, but that is inference.
- **Any published cost figure for room rebuilding** in any of these games. The estimate in §9 is
  ours, from cell counts, not measured.
