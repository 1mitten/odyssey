# 30 — Wildlife: how a world gets its animals, and keeps them

**Status:** built 2026-09-23 on `claude/wildlife`, stacked on the animals unit (design 29,
PR #167). Interview: `docs/research/animal-generation-interview.md`; plan:
`docs/plans/wildlife.md`. The Wildlife panel (§6) is the same PR's second commit.

Design 29 put a hog and a rat in the game and a debug button to spawn them. Nothing generated
them. This document is what does: a **table** per world (§1), a **seeding** at tick zero (§2), a
**level** the board holds through arrivals and departures (§3), the **night** (§4), what was
measured (§5), and the panel that shows it (§6).

## 1. The table: what lives where

A world's generator record (`MapGenDef`, which the natural meadow's `NaturalMapGenDef` extends)
carries three new fields:

| Field | Meaning |
|---|---|
| `wildlife` | one `WildlifeEntry` per kind: the `PawnKindDef` name, a commonality weight, the group size that arrives together, and a habitat |
| `wildlifePer10000Columns` | the population the board holds, per ten thousand walkable, dry, **reachable** surface columns (§2) |
| `wildlifeCeiling` | the most the level-keeper will ever let the board carry; 24 |

| World | Entries | Density |
|---|---|---|
| The natural meadow, wooded or not | midden hog, weight 3, sounders of 3–5, **woodland**; duct rat, weight 2, alone, **rock** | 15 |
| The ruined city | duct rat, weight 3, 1–2, **rock**; midden hog, weight 1, 2–3, anywhere | 15 |
| The bare board | none | 0 |

The bare board is empty on purpose: it is the baseline on which anything that is not grass is a
bug, and a hog on it would be a hog to explain in every test that counts pawns. `MakeBarren`
clears the table the natural def's constructor filled, and `MakeWooded` fills it again, so the
played board — built barren-then-wooded — has its animals and the test baseline has none. A
fixture that deals pawn ids by hand (the pace is keyed on the id) can ask for a world without
wildlife through `ColonyRequest.Wildlife`; it is not a player setting.

**A kind the content does not know generates nothing rather than throwing.** A table is data
about a world; a build without that animal draws the world without it.

**Habitats** are where a group's centre is drawn from: *woodland* is within two cells of a
standing tree, *rock* is beside solid terrain on the cell's own layer — an outcrop's side, a
rock face, the wall of a cut — and *any* is any surface cell. A habitat a board has none of
falls back to *any*, so a treeless seed still has its hogs.

## 2. Seeding: a census, not a number

`WildlifeSeeder.Seed` runs once in `ColonyWorld.Build`, after the colonists are placed and
before the first tick, from the world's own seed (`WildlifePurpose.Seed`, its own stream).

**The target is taken from a census of the board as generated.** `SurfaceCensus` walks every
column, takes its topmost walkable cell, and keeps it if an animal may enter it (no water: no
animal swims, design 29 §4) and it can be walked to from the colony's start under the animal
traverse mode. That last word is the one that matters: an animal hops only where a ramp is
drawn (design 29 §4), so on the wooded meadow's seed 1 it can reach **6,354 of 14,391** surface
columns. Fifteen per ten thousand of those is nine or ten animals — about one per 1,500 cells of
board, which is what the interview asked for. The number was seven per ten thousand until the
census was read; seven of the reachable count was four hogs on the whole meadow.

Then groups until the target is met, at most 64 draws: pick an entry by weight, roll its group
size, draw a centre from its habitat cells (never inside the clearing: `startingFellRadius`
plus six), and **scatter** the members over the walkable census cells within four of the
centre, drawn at random, skipping any cell a pawn already stands on. A sounder lands together
but loosely, across its glade rather than knotted on one cell and its neighbours; a rat lands
alone by a rock.

**Groups keep their distance** (owner, 2026-09-23: *"spread the animals more as I noticed the
pigs were all together - which is fine sometimes"*). A centre is drawn up to twelve times
looking for one at least `GroupSpacing` (24 cells) from every centre already used, and the
last draw is taken whatever its distance, so a cramped board still seeds and a board with one
small woodland gets its hogs there. Two sounders and the rats land in different parts of the
meadow; `SoundersLandApartFromEachOtherAndLooselyWithin` holds both halves on the played
board. Arrivals at the edge scatter the same way. The goldens moved again for it, and the
probe says the same thing it said the first time: the animals' cells and nothing else.

**Nothing lands in the clearing and nothing lands sealed away.** Both are the census: the
clearing is excluded from every habitat list, and an unreachable column never enters the census
at all, so no hog is generated in a cavern nobody will ever open.

## 3. The level: arrivals and departures

`WildlifeSystem` ticks in the rare group (every 250 ticks). It is inert on a world whose table
is empty, which is the bare board and every test built on it.

**Departure.** Each animal that has not already decided to go has a two-per-mille chance per
rare tick of deciding — a mean stay of about two days. Deciding sets `Pawn.Leaving`, and the
animal's own think node does the rest: instead of a wander it takes a leg to the nearest edge
cell it can reach (`EdgeTarget`: the four edges walked outward from the point on each nearest
the animal, first reachable cell on the nearest edge wins), leg after leg as the wander's
expiry cuts the walk up, and **on the edge it stands** for one rare tick. The level-keeper
finds it there — on the ring, no path, nothing but a wait in hand — ends the job and removes it.

**Removal is new.** Nothing ever left the pawn registry before this: no health model, no death.
`PawnRegistry.Despawn` releases the pawn's reservations, removes it in place and renumbers the
id index after it, so iteration stays ascending by id. The figure director already retires a
figure whose pawn is not in the snapshot, and the interface already copes with a selected pawn
that is not there, because both cope with one on another layer. `Leaving` is hashed — folded
into the kind's word, so a board with no animals hashes exactly as it did — and saved in its
own section (`odyssey.wildlife`, ids of the leavers), so an older save loads with nobody
leaving and no format bump was needed.

**Arrival.** Below the ceiling, with a six-per-cent chance per rare tick, the census is retaken
(the board is mined and built on, and a stale edge list is an arrival in a wall), and if the
population is below the target a group of a kind picked by weight is placed round a random
census cell on the board's outer ring — the same placement the seeder uses, so an arrival is
a sounder walking in together. Sixty per mille is a top-up within about a minute of a shortfall.

Nothing about the level is saved or hashed: every decision is the world seed and the tick, and
the one memory is on the pawn. Two runs of one seed leave and arrive on the same ticks.

## 4. The night

`SpeciesDef.nocturnal`: a rat is out at night and rests by day; a hog is the other way. Night is
the board clock's 20:00 to 06:00. Off its hours an animal takes a third as many legs and rests
three times as long (`AnimalIdleThinkNode.OffHoursFactor`). No breeding, no diet, no grazing:
there is no age or health model to hang them on, and hogs raiding a growing zone — the obvious
hook — is a diet model that belongs with the health unit.

## 5. Measured

| What | Where | Result |
|---|---|---|
| Seeding, habitats, the clearing, reachability, determinism | `WildlifeTests` | 10 fast tests |
| A leaver walks off, one cell a tick, and is removed on the ring; the registry's order and index survive | `ADepartingAnimalWalksToTheEdgeAndIsRemovedThere` | passes; the first version of the think node handed a leaver on the edge another edge leg, and it walked the ring for ever |
| A short board is topped up at the edge and never past the ceiling | `AShortBoardIsToppedUpAtTheEdgeAndNeverPastTheCeiling` | passes |
| The night | `ARatIsOutAtNightAndAHogByDay` | passes |
| Ten days on the 120 × 120 meadow | `TenDaysOfWildlifeHoldsItsLevel` (Long) | the level holds between 1 and 24; things came and went |
| Goldens | `Golden.cs` | wooded meadow and city re-baked; **the bare meadow did not move**. The colony probe on both branches: item counts identical; food and rest sums differ by exactly the animals' own untouched needs (ten animals at 800 on the meadow, four then three on the city, one of which left inside the run). The colonists did the same things. |

The seeded animal that stands on the ring when it decides to go vanishes on the next rare tick.
That is the rule working — it was at the edge — and the save test had to pick one that was not.

## 6. The Animals tab

**One tab, called Animals, on F5** (owner, 2026-09-23: *"animals/wildlife under one tab - for
now - just call it animals"*; the tamed half deferred entirely). It was the Wildlife panel on F6
for a day — the registry has carried both tabs since M1, and F6 was the one whose placeholder
said "what is out there" — and it is the Animals tab now, rebuilt to a design brief
(`docs/reference/mockups/animals-tab-brief.md`, answered through Claude Design) with the
Wildlife item **off the bar altogether** (owner, 2026-09-23: "remove Wildlife bottom bar"); the
registry keeps `ui.tab.wildlife` for the day the tamed half arrives and the two halves want
naming apart. The Esc cap the brief drew beside the close X was built and then removed on the
same instruction; Escape still closes the tab.

**What it shows.** A count strip across the top, one entry per kind present — the word at the
meta step, the figure in mono beside it — then one row per animal: a 22 px portrait tile
(where the pixel icon goes), the kind, and what it is doing (wandering or resting, with a 12 px
state square). Rows are sorted by kind and then by distance from the colony, nearest first, and
either heading sorts on a click: the sorted heading turns accent and carries a drawn 7 × 5
triangle, because no shipped font has one. Twelve rows a page, a pager of two 22 px chevron
buttons and "1 / 2" in mono only when there is a second page, and one quiet meta line —
`ui.animals.empty`, "No animals on the board" — when there is nothing to list. **Distance is not
drawn**; it orders the rows, and it is the one column the earlier panel had that the brief took
away, along with the layer.

**The geometry is the brief's, one owner** (`AnimalsLayout`): the window is **560** wide and a
UI Toolkit width is a border box, so the panel's 12 px padding and 1 px border leave 534 for
the grid of 32 + 250 + 252; rows, the count strip and the pager are 30 high; the header 34.
`TheWindowIsTheGridPlusItsOwnPaddingAndBorder` holds the arithmetic and the PlayMode test
measures the built window. Tamed-animal columns are appended to the right of Doing later and
widen the window; nothing is drawn for them, not even greyed.

**The tab and the inspect pane never show together.** Opening the tab clears the selection, as
opening Build does; any selection — including the one a row click makes
(`HudDirectors.ChooseAnimal`: selection and camera, **and the depth left where it is** — owner,
2026-09-23: "can the depth remain the same"; the roster path's layer change read as the view
lurching, and a wild animal is almost always on the surface being looked at) — closes the tab,
so the pane that then shows the animal has the corner to itself. The brief also specifies a selected-row
style, accent fill with on-accent ink; it is built, and under this rule it is never seen,
because no selection survives the tab being open. Kept because it is cheap and the rule may
move when the tamed half arrives.

**Built the way the Work tab is built** (design 27 §16–17): one window docked bottom-left on
the command bar, the Animals item on the bar washed while it is open (`cmd--on`, as Build is),
rows pooled once at build and retexted on refresh, the count strip rebuilt only when the
number of kinds changes, nothing rebuilt per frame, the refresh in the Work tab's half-second
bucket. Opening any of Build, Work, the menu or this tab puts the others away; Escape closes it
at the Work tab's rung. `AnimalsModel` is Unity-free and runs in the fast tier; every rendered
string is ASCII, which `HudFontTests` and the PlayMode test both hold.

**The Almanac knows them.** Its Fauna category was two invented entries — a "Scraphound" that
is in no register and a rat with tame chances and bite damage the game does not have — and is
now the two animals the game has, by their registry names, saying only what the simulation
does: pace, habitat, group, day or night, what they climb, how long they stay. The inspect
pane's info button on an animal opens its entry (`AlmanacDirector.ResolveSelection`), which
returned nothing for an animal until there was an entry to open.
`TheAlmanacsFaunaAreTheAnimalsInTheGame` holds the names to the registry.

**The colony is where its people stand.** "How far" has to be from something the frame carries,
and the mean of the colonists' cells is; the start cell is not, and a colony that has moved
house has moved its animals' distances with it. With no colonists the distance is from the
board's origin, a number rather than a lie.

## 7. Open

- **Roam.** A wandering animal drifts within eight cells of wherever it is and never sees the
  rest of the board; a leaver walks the length of it. A long leg now and then is one number.
- **Reactions.** Nothing flees. `a-09` records flee as an override branch, not a leaf; it is
  the health unit's, where there is something to flee from.
- **The census is retaken per arrival check.** 14,000 columns with a reachability test each,
  six times a minute at most; not measured at the scale target.
