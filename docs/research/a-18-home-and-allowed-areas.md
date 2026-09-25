# a-18 — Home and allowed areas

**Question:** How does RimWorld (the reference) model the *home area* and the per-pawn *allowed
area* — how the home area grows and what it governs, how an allowed area gates job-giving and what
overrides it, how both are drawn — and what the rest of the colony-sim genre does about
restricting where a colonist may work?
**Lane:** A (reference mechanics). **Date:** 2026-09-25. **Cap:** 6 searches / 6 reads.

**How the cap was spent, and a caveat before anything else.** All six searches ran. All six page
reads were **refused by the network egress proxy** (`rimworldwiki.com`, `dwarffortresswiki.org`,
`steamcommunity.com` and `rimworldaccess.com` are all blocked from this container), so no page was
read whole. What follows is built from the search engine's extracts of the wiki pages, plus
recollection of the game where the extracts are silent; every recollected item is marked
**(recalled)** and carries lower confidence. A session on a machine that can reach the wiki should
spend one read each on the *Home area*, *Allowed area* and *Zone/Area* pages and strike the
markers. Clean room: mechanics and shapes only, nothing pasted.

## Findings

### 1. The home area

1. **It is an area, not a zone.** The reference groups three things under one heading — *zones*
   (stockpiles, growing) and *areas* (home, allowed, and some special-purpose ones such as the
   roof and snow-clearing areas) — and the home area is the one area every colony has from the
   start (search extract of the wiki's Zone/Area page).
2. **It grows automatically as the colony builds, and that automation is a toggle.** The wiki
   says the home area is expanded automatically as structures are built, and that the behaviour
   is switched by a house-shaped button in the bottom-right cluster beside the overview control
   (search extract of *Home area*). **(recalled)** The rule is: when a building belonging to the
   player is completed, every cell within a square margin of about **four cells** around the
   building's footprint is added. It is a square margin, not a circle. Player-built
   *structures* trigger it — walls, doors, furniture, workbenches, power buildings — and natural
   things, plants, items, zones and blueprints do not. It runs on completion, not on the
   blueprint, so a planned base is not home until it exists.
3. **It never shrinks on its own.** Deconstructing or losing a building leaves the cells marked;
   only the player's home-area tool removes them (the tool has an add mode and a remove mode;
   **(recalled)**). This matters because a home area that shrank when buildings were lost would
   silently switch off firefighting exactly when fire had just destroyed something.
4. **What it governs** (search extracts of *Home area*, and the mod thread asking for the three
   to be separated, which confirms they are one area today):
   - **Firefighting** — colonists fight fires only inside the home area; the fire alert is also
     raised for fires in it **(recalled)**. Outside it a fire is ignored unless a colonist is
     drafted and ordered to beat it.
   - **Repair** — damaged buildings are repaired only inside it. The wiki's own tip is to draw a
     one-cell-wide strip of home area along a long power line so a break is repaired unprompted,
     which shows the area is deliberately painted in shapes that have nothing to do with a house.
   - **Cleaning** — filth is cleaned only inside it; filth outside is invisible to the cleaner.
   - **Roofing** — **(recalled)** roofs are built automatically over enclosed rooms, and that
     automation only applies inside the home area; the reference also has an explicit
     build-roof / no-roof area pair for overriding it. Snow clearing is likewise home-only.
   - **Forbidding of dropped items is *not* a home-area rule.** The reference forbids an item by
     a per-item flag (toggled with one key); items dropped by non-colonists — a raider's weapon,
     a crash's cargo — arrive forbidden regardless of where they land, and items a colonist drops
     arrive allowed regardless of where they land **(recalled; the search extracts describe the
     per-item flag and say nothing tying it to the home area)**. The one line in the extracts
     that says "outside the area is forbidden" is about the *allowed* area (finding 6), not the
     home area.
5. **Design intent, read off the above.** The home area answers one question — *what does the
   colony look after unasked?* — and it exists so that the same three or four maintenance jobs
   do not send colonists across the whole map to douse a lightning fire in the woods or clean a
   cave. Growing it automatically means a new player never has to learn it exists; the toggle and
   the tool exist for the player who has learned. Its failure mode in play is the opposite one:
   the automatic margin annexes the ground around every outbuilding, and cleaners then sweep
   mud outdoors.

### 2. Allowed areas

6. **The gate is on the job's target, never on the path.** The wiki states the rule directly:
   colonists only perform work inside their allowed area, but the area does not stop them from
   walking through cells outside it to reach a destination (search extract of *Allowed area*).
   Everything outside — items, locations, tasks — is treated as forbidden *for that pawn*, which
   is how it is implemented: the same "is this forbidden to me" question the item flag answers is
   also answered by the area, so every job giver gets the restriction for free rather than each
   checking it **(recalled, but consistent with the extract's wording)**.
7. **A prioritised (right-click) order outside the area is refused, not obeyed.** The wiki's
   *Prioritize* page says the allowed area is considered when prioritising, and the player
   threads agree: a colonist told to do a job outside their area will not, and the remedy offered
   is to widen the area or draft them (search extracts of *Prioritize* and two Steam threads).
   **(recalled)** the right-click menu shows the entry greyed with a reason rather than hiding it.
8. **Drafting overrides everything.** A drafted colonist goes wherever they are sent and fights
   whatever they are pointed at; the area applies to work-giving, and a drafted pawn is not
   being given work (search extracts; every thread names drafting as the way round the gate).
   **(recalled)** Mental breaks and fleeing also ignore the area, and a colonist carried or
   rescued by another does not choose where they are carried.
9. **Eating and sleeping obey it, and that is the classic trap.** **(recalled)** A colonist whose
   bed is outside their area does not go to it; they sleep on the ground inside the area and take
   the "slept on the ground" mood. Food outside the area is not eaten until the pawn is actually
   starving, at which point the search widens — the same escape hatch Dwarf Fortress documents
   for burrows (finding 15), so the shape is genre-wide even where the threshold is not. The
   practical guides all say the same thing: put the bed, the table and a food store inside any
   area you restrict someone to. Medical: a doctor tends a patient in a bed outside the doctor's
   area? Not determined (see below).
10. **How many, how made.** Areas are created, deleted, renamed and *inverted* in a "manage
    areas" window (search extract; inversion is the one-click way to make "everywhere but the
    mine"). They are painted with an expand tool and a clear tool that live in the build menu's
    Zone category, because an area is one kind of zone to the reference (search extract).
    **(recalled)** there is a hard cap of about ten allowed areas, and one is enough for most
    colonies. Animals get the same mechanism from the animals table.
11. **Presentation.** The reference's *Restrict* tab was folded into the *Schedule* tab: one row
    per colonist, the twenty-four-hour timetable, and an area column (search extracts of *Menus*
    and *Allowed area*). **(recalled)** the area column is not a dropdown but a strip of buttons,
    one per area, with the chosen one filled in; **Unrestricted** is the default and the first
    button, **Home** is offered as a choice like any other area, custom areas follow, and
    *hovering* a button paints that area on the map for as long as the pointer rests. A
    "manage areas" button sits above the table.

### 3. Drawing

12. **Fill, not outline, and only on demand.** **(recalled, no extract describes it)** An area is
    drawn as a translucent colour wash over each of its cells — no edge line — in a colour dealt
    to the area when it is made (the home area has a fixed one of its own). It is shown while the
    matching expand/clear tool is armed, while a row's area button is hovered in the Schedule tab,
    and while the area is being edited in the manage window; otherwise it is invisible. The
    general zone-visibility toggle in the bottom-right cluster hides *stockpile and growing zones*
    and does not govern areas, because areas are never on screen unless something asks for them.
13. **Why on demand.** Areas overlap by design (Home overlaps everything; an inverted area is
    most of the map), so a permanent wash would be unreadable and would fight the stockpile and
    growing washes. Painting one at a time on hover is what makes overlap tolerable.

### 4. The rest of the genre

14. **Dwarf Fortress — burrows** (search extract of the wiki's *Burrow* page). A burrow restricts
    only where a dwarf may *do* jobs and *take materials from*; it never restricts movement, and a
    dwarf assigned to none ignores burrows entirely. A job that needs an item outside the burrow
    is cancelled rather than fetched. Dwarves will not eat or drink outside their burrow until
    starving or dehydrating — so the burrow must hold food, drink and beds. A burrow need not be
    contiguous, may overlap others, and **is a set of cells across z-levels: the player selects
    cubes, not rectangles**. A *civilian alert* confines every non-military dwarf to the alert's
    burrows for as long as it is raised — a one-switch "everyone indoors", which is the genre's
    answer to the raid.
15. **Going Medieval.** The searches found no per-settler allowed area; the only "restricted
    area" discussed is a margin at the map edge the player may not build in, added so raiders
    always have somewhere to spawn. Not the same mechanic.
16. **Oxygen Not Included.** No areas at all. Access is per *door*, per duplicant, per direction
    (both ways, one way, or none), obeyed even when the door stands open; because a duplicant
    cannot reach a region, it never considers work there. Restriction by topology rather than by
    paint — cheap to implement, unreadable at a glance, and the forums ask for areas.

## Recommendation

- **The owner's shape is the reference's shape, with one substitution.** A derived home area that
  a building extends by a square perimeter, never shrinking on its own, and a per-colonist
  *Anywhere / Home* choice with drafting overriding it, is exactly the reference: finding 2
  (square margin around a completed building), finding 3 (no automatic shrink), findings 6–8
  (target gate, path free, prioritised order refused, draft overrides). The one difference is
  the margin: the reference's is about four cells **(recalled)** and the owner has chosen five;
  a Def field, not a constant, and nothing to argue about.
- **Copy the target-not-path rule exactly, and put it in one place.** Answer "may this pawn take
  this target?" where the forbid flag is already answered, so every giver — haul, build, clean,
  fell, mine, sow — inherits it, and never in each giver. The reference and Dwarf Fortress both
  gate the target and free the path, and both games' players find that correct.
- **Decide eating and sleeping now, and decide against the trap.** The reference makes a
  restricted colonist sleep on the floor beside their own bed and go hungry beside a full
  store; two games document the same starvation escape hatch, which is an admission the rule is
  wrong. Odyssey has no custom areas yet — only *Home* — so let need-jobs (eat, sleep, and later
  tend) ignore the area outright. A colonist restricted to Home who eats from a store outside it
  is what every player wants and what the forum threads are asking how to get.
- **Home governs what the colony does unasked**: firefighting when fire exists, repair, cleaning,
  the roof-by-default rule when roofs are automatic. Do **not** make it forbid items: the
  reference does not, and the two rules should not share a name.
- **Draw a wash on demand, no edge.** A translucent fill in one hue, shown while the home tool is
  armed, while the area button in the colonist's row is hovered, and on a Menu overlay beside the
  Power one — never standing. Odyssey already has the machinery (`TintCode` on the ground's tint
  is no draws at all, per `26-storage.md` §13) and the storage wash to sit beside.
- **Invent, do not copy, the restrict surface.** The reference's Schedule tab is a strip of area
  buttons per row; Odyssey has a Work tab with a frozen name column already (design 27), and
  one more column — *Area: Anywhere / Home* — is cheaper than a tab. Keep the row-per-colonist
  and the hover-paints-the-area behaviour; drop the ten-area manager until a second area exists.

## Sources

- https://rimworldwiki.com/wiki/Home_area — auto-expansion, the toggle, fire/repair/clean, the
  conduit strip tip (search extract only; the page itself is blocked from this container)
- https://rimworldwiki.com/wiki/Allowed_area — the target-not-path rule, "outside is forbidden to
  them", the default of Unrestricted, animals (search extract only)
- https://rimworldwiki.com/wiki/Zone/Area — zones versus areas, the manage window
  (create/delete/invert/rename), areas living in the Zone build category (search extract only)
- https://rimworldwiki.com/wiki/Prioritize — the allowed area is honoured when prioritising
  (search extract only)
- https://rimworldwiki.com/wiki/Menus — the Schedule tab holds the area column (search extract)
- https://steamcommunity.com/app/294100/discussions/0/1639788130283064306/ and
  https://steamcommunity.com/app/294100/discussions/0/3865717501012950968/ — players on
  prioritised jobs refused outside the area, drafting as the way round (search extracts)
- https://steamcommunity.com/app/294100/discussions/0/1736588252371367036/ — a request to split
  cleaning, firefighting and repair into separate areas, confirming they are one (search extract)
- https://dwarffortresswiki.org/index.php/Burrow — burrows: jobs and materials not movement,
  eat/drink until starving, cubes across z-levels, civilian alert (search extract only)
- https://steamcommunity.com/app/1029780/discussions/0/4141690560288266402/ — Going Medieval's
  edge margin, the only "restricted area" found (search extract)
- https://oxygennotincluded.wiki.gg/wiki/Pneumatic_Door and
  https://forums.kleientertainment.com/forums/topic/86337-restricted-and-work-only-assigned-areas/
  — per-duplicant door permissions, obeyed while open; the request for real areas (search extracts)

## Confidence

- **1. Home area — medium** for the automatic growth, the toggle, no automatic shrink and the
  three maintenance jobs (several extracts agree); **low** for the four-cell margin and the
  roofing rule, which are recalled and unread.
- **2. Allowed areas — medium-high** for the target-not-path rule and drafting (stated flatly by
  the wiki extract and by every thread); **medium** for prioritised orders being refused (the
  wiki extract says the area is honoured; no page read confirmed the exact behaviour of the
  menu entry); **low-medium** for eating and sleeping, recalled and corroborated only by the
  Dwarf Fortress parallel; **low** for the ten-area cap and the button-strip layout.
- **3. Drawing — low.** No extract described it; the whole of §3 is recollection.
- **4. Other games — medium** for Dwarf Fortress (a detailed extract of the wiki page) and for
  Oxygen Not Included (two extracts agree); **medium** that Going Medieval has no such mechanic
  (absence in two searches, not a page that says so).

## Could not be determined

- The exact automatic margin around a building, and which building kinds are excluded from it.
- Whether the roofing automation is home-gated, and whether snow clearing is.
- The exact wording and state of a prioritised order outside the area (greyed with a reason, or
  hidden), and whether a doctor tends a patient whose bed is outside the doctor's area.
- The starvation threshold at which a restricted colonist leaves the area for food.
- Every drawing detail: hue, alpha, whether the home area's hue is fixed, and the precise set of
  moments an area is painted.
- Whether the reference caps the number of allowed areas, and at what.

## Layer questions touched

**Brief layer question 6 — are zones and areas per layer or 3D volumes.** The reference is flat
and says nothing; its four-cell margin is a square on one plane, and its allowed area is a set of
(x, z) cells. Dwarf Fortress is the one 3D precedent found and it answers plainly: a burrow is a
set of cells across z-levels, painted as **cubes**, non-contiguous and overlapping — and players
manage it, because the painting tool works a level at a time and the civilian alert does the
common case for them. For Odyssey the derived home area makes the question sharper than either
game: a building on the surface extends a five-cell square *on its own layer*, but a cellar under
the house and the roof over it are the same colony — a fire in the cellar is not outside the home
because it is one layer down. The cheapest rule consistent with both references is that the
derived margin is a **column**: the perimeter square extended one layer up and one down from the
building (or through every layer a stacked building occupies), with a per-layer paint tool for the
player who wants otherwise. That is a decision for design, not research; what research can say is
that nothing in the 2D reference forbids it and the one 3D reference does it.
