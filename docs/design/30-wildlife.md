# 30 — Wildlife: how a world gets its animals, and keeps them

**Status:** built 2026-09-23 on `claude/wildlife`, stacked on the animals unit (design 29,
PR #167). Interview: `docs/research/animal-generation-interview.md`; plan:
`docs/plans/wildlife.md`. The Animals panel (§6) is the next PR.

Design 29 put a hog and a rat in the game and a debug button to spawn them. Nothing generated
them. This document is what does: a **table** per world (§1), a **seeding** at tick zero (§2), a
**level** the board holds through arrivals and departures (§3), the **night** (§4), what was
measured (§5), and the panel that will show it (§6).

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
plus six), and fill the walkable census cells within three of the centre, skipping any cell a
pawn already stands on. A sounder lands together; a rat lands alone by a rock.

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

## 6. The Animals panel (next PR)

F2. One row per animal — kind, status, layer, distance from the colony — under a per-kind
count header; a click selects and jumps the camera; the hunt and tame columns are reserved, so
the tamed half of the tab lands in the same panel later. `AnimalsModel` in `Odyssey.Hud`,
Unity-free; the panel pooled and paged like the roster.

## 7. Open

- **Roam.** A wandering animal drifts within eight cells of wherever it is and never sees the
  rest of the board; a leaver walks the length of it. A long leg now and then is one number.
- **Reactions.** Nothing flees. `a-09` records flee as an override branch, not a leaf; it is
  the health unit's, where there is something to flee from.
- **The census is retaken per arrival check.** 14,000 columns with a reachability test each,
  six times a minute at most; not measured at the scale target.
