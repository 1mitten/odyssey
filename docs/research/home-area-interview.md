# The home area — owner interview

**Phase:** Interview (feature-level, in the shape of `meadow-interview.md` and `animals-tab-interview.md`).
**Date:** 2026-09-24. **Branch:** `claude/sleepy-cannon-9d0evw` (documents only; no code was written
before or under this file).
**Conducted by:** Claude Code, eight questions in two rounds, after a read-only exploration of the
views strip, the overlay director, the zone container, the job givers, the combat response and the
docked tabs.

The owner's brief: *"Could you plan out the home zone, make it another element to the toolbar that
contains power. This will help you visualise your home (use a home icon). Your home area consists of
the most outer region building, stockpile, path etc - plus a perimeter of 5 (or whatever you
recommend to assume). So this comes on to the next part to be able to order colonists (new tab I
think) so we can allow them everywhere or home for now - so it allows them to keep to home for
safety. We can provide a prompt to claude design to create this and ask me, interview me and
clarify all details."*

**Read next:** `docs/design/43-home-area.md` (the design these answers decide),
`docs/reference/mockups/home-area-brief.md` (the Claude Design prompt),
`docs/research/a-18-home-and-allowed-areas.md` (how the reference does it), `docs/plans/home-area.md`.

## 1. What the exploration found, put to the owner before the first question

- **Nothing of it exists.** There is no area mask, no per-colonist cell gate and no notion of the
  colony's home or centre beyond worldgen's start cell. A fleeing colonist runs twelve cells *away*
  from danger and stops, going nowhere in particular (design 33 §18d).
- **The names were reserved and never built.** `ui.arch.tool.allowed` *Allowed area*,
  `ui.command.setzone` *Set area* and `ui.overlay.zones` *Stockpiles, growing and allowed areas* sit
  in the registry. The panel catalogue's **B4 Assign** (design 10) is a per-colonist assignment tab
  with an area column, milestone M7. Design 33 §18h deferred *"the colony-wide rules panel until
  there is more than one rule to hold"*: this is the second rule.
- **The reference splits zones from areas** (research a-14): zones are things you configure and
  select, areas are masks you paint, and areas overlap freely. A restricted colonist takes no job
  outside her area but may walk through it (a-14, a-03).
- **The toolbar the owner means is the views strip** under the orders strip in the right-hand
  gutter (`HudViews`, design 32 §14), with Power its one switch. The Menu's overlay row is the same
  switch.
- **The per-colonist setting has a template**: the combat response (Fight back / Defend / Flee,
  design 33 §18c), saved only when not the default and hashed only when not the default, so no golden
  moved when it arrived.
- **One door for the gate**: nearly every job giver asks `PawnContext.Reachable(pawn, cell)`; animals
  and bandits ask a different overload.
- **The Animals tab is the precedent for a Claude Design brief** (`docs/reference/mockups/animals-tab-brief.md`):
  it came back as a specification and was built the same day.

## 2. The answers

Every recommendation was taken.

| # | Question | Answer |
|---|---|---|
| 1 | How is home decided? | **Derived automatically**: the union of everything the colony placed, grown by the perimeter, recomputed whenever something is built or removed. Nothing painted, nothing saved. A paint-over (add or exclude by hand) is a follow-on. |
| 2 | How big a perimeter, and what shape? | **5 cells, square** — every cell within five steps orthogonally or diagonally, 12.5 m. |
| 3 | What counts as placed? | **Placed things and sites**: walls, doors, floors, paving, beds, shelves, generators, heaters, campfires, ladders, power lines and line orders, stockpiles, growing zones, and build sites not yet raised. **Not** felling or mining marks. |
| 4 | Vertically? | **Per layer, one layer of margin**: each layer grows its own, and a home cell makes the cell directly above and below it home. A roof, a terrace step and a cellar door are inside; a mine three layers down is not. |
| 5 | Where does the per-colonist setting live? | **A new Assign tab on F4**, the dead *Colonists* slot, in the Work tab's shape: a row per colonist, **Area** (Anywhere / Home) and **Response** (Fight back / Defend / Flee). The pane's Response button stays. |
| 6 | What does Home stop? | **No work outside, and a draft overrides it.** She takes no job whose target or standing cell is outside home — hauling from outside and a right-click forced order included — but may walk through; idle outside, she walks home; drafted, she goes where sent. No pathfinder change. |
| 7 | What does Claude Design draw? | **All three**: the Assign tab's states, the home glyph as an SVG path, and the look of the area on the board. |
| 8 | Should Flee run towards home? | **Not in this unit.** Recorded as the hook. |

## 3. Why the recommendations were recommended

- **Derived, not painted.** The owner described home as a function of what is built, and the reference
  grows its own home area the same way. Derived needs no save section, no hash, no brush and no tool
  row. Painted-only never follows the colony and has to be repainted as it grows.
- **Five cells, square.** The owner's own number. Square is what the grid draws cleanly and is a
  separable dilation, two passes of a one-dimensional window. Round costs more for a softer corner
  nobody asked for.
- **Placed, not ordered.** The reason to keep someone home is that the work outside is where the
  danger is. A felling mark is exactly that work, so counting marks would put the danger inside home.
  A build site is different: it is the base the player is building, and a builder kept home should
  still be able to raise the house next door.
- **One layer of margin.** Per-layer matches how stockpiles and zones work here (a-14 layer question
  6). With no margin a colonist on a roof is "outside home"; a whole column would make a mine shaft
  home.
- **An Assign tab.** The Work tab already pages twenty-two columns and is about priorities; the
  catalogue reserved Assign for exactly this; and it gives the combat response the colony-wide view
  §18h deferred.
- **Gate the givers, not the paths.** A per-colonist path constraint would need a navigation mode per
  area, and each mode costs a district flood on every rebuild. The reference's rule — no job outside,
  walk through freely — needs only a bit test per candidate target.

## 4. Tensions the owner chose into, recorded so they are not read as faults

1. **A home that follows the base also moves under a colonist's feet.** Knock down the last wall of an
   outpost and the colonist standing in it is suddenly outside and walks home. That is the rule
   working.
2. **"Safe" means "stays in"; it does not mean "runs in".** A Home colonist chopping inside home when a
   bandit walks in still does whatever her Response says. Running *to* home is question 8, deferred.
3. **A whole colony at Home can starve beside a supply drop.** Meals dropped outside home are nobody's
   to haul while everyone is kept in. The reference has the same property and relies on the player;
   the Assign tab showing everyone at *Home* at a glance is the mitigation.
4. **The perimeter can reach across a river or up a terrace** onto ground nobody would call home. It
   is a square around what was built, not a flood of what is reachable. The first playtest is where
   that is judged.
5. **A new colony has no home.** Found after the interview, in the code: the played scenario gives
   **no beds and no stockpile** (owner, 2026-09-20), and the loose piles of food, stone and wood are
   items, not placements. So home is empty until the first thing is built or zoned, and **an empty
   home restricts nobody** — otherwise a colonist set to Home on the first morning could do nothing
   at all. The Assign tab says so when it is true (design 43 §4d).

## 5. What this does not settle

- **Eating and sleeping outside home.** The design gates the tree's every choice except the draft, so
  a Home colonist will not eat a meal lying outside. Beds and stockpiles are inside by construction,
  but the starting food lies loose wherever the kit put it and is only inside once something is
  built or zoned near it. Design 43 recommends the reference's escape hatch — a starving colonist
  may eat outside (a-18 finding 9). Say if eating and sleeping should be exempt outright.
- **An empty home restricts nobody** (tension 5): an assumption, not an answer.
- **Hand paint-over**, the reference's second half: add a patch, or exclude one. Recorded as the
  follow-on (design 43 §9).
- **An animal's area.** The Animals interview recommended an allowed area and deferred the tamed half
  entirely; the Assign tab is colonists only.
- **What else home should mean later**: free repair inside it, dropped items forbidden outside it,
  the cleaning and firefighting radius — all things the reference hangs on the same mask (a-18).
- **The look** — an edge, a wash, or both, and how a layer below reads under the x-ray — comes back
  from Claude Design.

## 6. Round three: the hearth (2026-09-25)

After H1 and H2 were built the owner asked: *"Maybe 1 campfire is the home centre - the thing that
dictates it - only one can be built. Would this make sense and make it easier - ask me questions
about the mechanic - what happens if you tried to deconstruct it - maybe you can move it?"*

Two things were put to the owner first. **Campfires heat rooms**, and in Rime a sealed room needs its
own, so capping them at one would cap heating. And **H2's tests had found a gap**: a build site is
home by itself, so a far site made an island of home and a colonist kept home walked out to it. A
hearth closes it. The genre has the shape too: Against the Storm's one main hearth among ordinary
ones, Frostpunk's single generator.

Eight questions in two rounds; every recommendation was taken.

| # | Question | Answer |
|---|---|---|
| 1 | Which fire? | **Mark one campfire as the hearth.** Campfires stay unlimited. *Make this the hearth* on a campfire's pane; marking another is how home moves. |
| 2 | How does it decide home? | **The base joined to it**: the earlier rule, keeping only the piece that contains the hearth. Outposts are not home. |
| 3 | Deconstructed or smashed? | **Home goes with it.** Allowed, warned first; no hearth means no home and nobody restricted; an alert says so. |
| 4 | When does a colony get one? | **The first campfire raised.** |
| 5 | How close to join? | **When the grown areas touch**, about eleven cells; no new number. |
| 6 | What is it called? | **Hearth.** |
| 7 | Anything else now? | **No**: bandits targeting it, Flee running to it and idlers preferring it are recorded hooks. |
| 8 | Does it look different? | **A house mark over it only while the Home view is on.** |

Nothing is promoted when the hearth is lost: a campfire raised while there is none takes the title,
or the player marks one. That is the reading of answers 3 and 4 together, recorded in design 43 §3f.
