# a-04 (cave-ins and prospecting) — collapse rules, underground fog and reveal tools

**Lane A, item 4 (a second file on building and roofs).** Asked 2026-09-26 for deep mining (design
62). One subagent, one question, capped at 10 searches and 10 page reads. **Every wiki page was
refused by the proxy** (the RimWorld, Dwarf Fortress and Going Medieval wikis), so the findings come
from search extracts; claims marked **[mem]** are from the subagent's own knowledge and unchecked.
Clean room: rules and numbers in our own words.

## Question

What are good-practice rules for (A) underground cave-ins and roof collapse and (B) underground fog of
war and prospecting in colony sims, and how are they kept cheap to compute? Relevance: our
`SupportSolver` is an incremental dirty frontier that today collapses only floor slabs; `Discovered`
is a one-way per-cell latch set when a face is exposed; the health system can down without killing;
the owner chose cave-ins with a warning that down and never kill, unknown underground drawn as plain
rock, and a prospecting job now with a powered scanner later.

## Findings

**A. Cave-ins**

- **RimWorld.** A roof cell holds if it is within **6 tiles** of a support (walls, columns, natural
  rock, any fully impassable building, nearby supported roof), so interior spans up to 12 are safe.
  Remove the last support in range and the cells fall at once. Thick mountain roof drops collapsed
  rock dealing a huge crush hit (effectively lethal), and that rock then counts as support, so the
  collapse cannot spread. **No warning** beforehand, only a notice after; accidental deaths while
  mining out mountain bases are the standing complaint. The check is local **[mem]**.
- **Dwarf Fortress.** Anything disconnected from the rest of the map falls, connection counted on six
  axes (no diagonals, no bridges; stairs, constructions, supports and trunks count). Falling material
  injures or kills and the dust **knocks out** those it reaches; damage grows with height and mass
  **[mem]**. No warning; the classic accident is a floor slab falling on the miner. The check is a
  **global** flood fill, cheap to trigger but up to the size of the connected piece.
- **Going Medieval.** A wall or beam standing on ground has stability **4**, losing **1 per tile**
  away; a tile at 0 falls, giving spans of about 6 across and about 10 along a beam. Stability passes
  upward free, so digging below weakens what is above. Players say a 7 × 7 cellar caves in at the
  centre and keep unsupported runs to 3 or less. It is a **local** distance-decay rule — the shape of
  our solver.
- **Oxygen Not Included.** Only unstable materials (sand, regolith) fall when the cell below empties —
  purely per cell. No damage figures found.

**B. Fog and prospecting**

- **Dwarf Fortress.** Unmined stone is hidden; mining a tile reveals neighbouring minerals of the same
  kind, so a vein can be followed. Players do "exploratory mining" (patterned tunnels), then mine the
  vein once found. Complaint: without patterns it is a blind chore.
- **RimWorld.** The map starts fogged; a sealed space is revealed whole by a flood fill when breached
  **[mem]**. The ground-penetrating scanner needs an operator, does not work under a roof, and finds
  deep deposits for a deep drill, which works a ~2.6-tile radius. Power and find-rate figures are
  **[mem]**.
- **Factorio.** A radar permanently reveals 7 × 7 chunks (224 × 224 tiles) and slowly scans a 29 × 29
  chunk area, one chunk per 33.3 s at full power.

## Recommendation

- **Cave-in rule:** Going Medieval's distance decay on our existing frontier. Grounded rock, walls and
  props are sources; each horizontal step through unsupported rock ceiling costs one; a ceiling cell
  at 0 falls. **Span about 6** (RimWorld's number). Only cells within a few steps of an edit re-solve.
  Rubble that lands counts as support, so collapses do not chain.
- **Warning, three tiers:** preview what a Mine order would make unsafe; cells one step from failing
  tinted with an alert and a grace period, pawns preferring not to stand there; a pause-worthy alert
  after a collapse.
- **Harm:** a blunt hit capped to down, never kill; a short stun in a radius of 1–2 from dust; a pawn
  next to open ground may dodge.
- **Prospecting job:** a few seconds' work at an exposed face marks everything within **radius 3**
  (same layer and one above and below) as discovered; skill widens it by one per band. Mining keeps
  revealing only the six face neighbours. A "follow the vein" order is the fix for DF's chore, and a
  later unit.
- **Scanner (later):** powered and operated; reveals ore only, as a tint over plain rock; a small
  always-on radius and a slow sweep outward, written into the same one-way flag.

## Sources

- https://rimworldwiki.com/wiki/Roof
- https://rimworldwiki.com/wiki/Collapsed_rocks
- https://rimworldwiki.com/wiki/Ground-penetrating_scanner
- https://rimworldwiki.com/wiki/Deep_drill
- https://www.dwarffortresswiki.org/index.php/Cave-in
- https://dwarffortresswiki.org/Exploratory_mining
- https://goingmedieval.fandom.com/wiki/Stability
- https://steamcommunity.com/app/1029780/discussions/0/3055111535922874430/
- https://techraptor.net/gaming/guides/going-medieval-how-to-build-underground-guide
- https://oxygennotincluded.wiki.gg/wiki/Category:Unstable
- https://wiki.factorio.com/Radar

## Confidence

- **High:** RimWorld's 6-tile span and lethal thick-roof collapse; Dwarf Fortress's six-axis
  connectivity; Going Medieval's 4 and −1 a tile; Factorio's radar numbers.
- **Medium:** Dwarf Fortress's vein reveal, RimWorld's breach flood fill, the player complaints.
- **Low:** scanner power and timing; whether RimWorld's check is local.
- The recommended numbers are **our design suggestions**, not any game's.

## Could not be determined

- Dwarf Fortress's damage formula for height and mass.
- RimWorld scanner power and mean time between finds.
- Whether any of these games warns before a collapse (none found).
- Going Medieval collapse damage.
- Mindustry and Timberborn survey mechanics (not searched; cap reached).
