# Interview — how a world gets its animals, and the Animals menu

**Phase 1 for the next animals unit**, 2026-09-22. Owner: *"you could refer to RimWorld and work
out what would make a good generation for each world — and how the animals are generated — and
also look at the animals menu in what that could look — happy to research / plan first."*

The MVP (PR #167, `docs/design/29-animals.md`) put two species in the world by hand: a hog and a
rat, spawned from the debug menu, wandering and resting. Nothing generates them. This interview
decides what does, and what the player sees them in.

## Ground — what is true now

| Seam | State |
|---|---|
| Worlds | Three generator tables: the wooded meadow (what the scene loads), the bare board (the test baseline, on which anything that is not grass is a bug) and the ruined city (present, tested, not loaded). `NaturalMapGenDef` is per-world numbers: trees, outcrops, ore, ponds, streams. **No wildlife field.** |
| Scenario | `ColonyScenario` spawns colonists, meals, beds, salvage; **no animal**. |
| Species and kinds | `Species.xml`: Person, MiddenHog, DuctRat. Kinds are a save contract (append-only). The proper-noun register proposes three more creatures with **no art**: the girder cat (predator), the loper (feral machine), the dray hog (pack animal). |
| Events | One incident (the supply drop), debug-fired; no storyteller, no cadence. |
| Figures | **64 drawn figures, hard ceiling**, shared by colonists and animals; an animal past the cap is not drawn at all (known gap). |
| Models | No health, no age, no sex, no diet, no taming, no hunting. Animals need nothing and eat nothing. |
| HUD | F1 Work, F9 Almanac; the roster excludes animals; the inspect pane shows *Midden hog · Wandering* with no tabs. |
| Prior research | `a-09-animals.md`: a kind record carries threat weight, ecosystem weight and commonality; arrival is a spawner reaching a density target; wild animals cannot pass doors. |

## What RimWorld does (clean room: mechanics, not data)

- **Each biome carries a wildlife list** — kinds with a commonality weight — and an **animal
  density** scalar. Map generation seeds animals to that density; a **wild animal spawner** then
  tops the map up over time, spawning at the map edge, and animals occasionally wander off the
  edge. The population is a level the map holds, not a one-off scatter.
- **Herd animals arrive in groups** sized per kind; solitary kinds arrive alone. Spawn position
  is a random walkable cell away from the colony for the seed, the edge for arrivals.
- **The Animals tab** is for *tamed* animals: name, master, follow settings, training, allowed
  area, slaughter. **The Wildlife tab** lists *wild* animals on the map: kind, sex and age, the
  skill needed to tame, and the hunt and tame designations. Clicking a row selects and jumps.
- Day and night matter to some kinds (nocturnal flag), and wild animals graze and drink, which
  is what ties density to the biome's fertility.

## Questions

Each has a recommendation; **"go with the recommendations"** is a complete answer.

**1. Which animals in which world?** Only the hog and the rat have art. Recommend: a wildlife
table per world in `NaturalMapGenDef` (kind, weight, group size, habitat) listing only those
two — meadow: hog sounders in woodland and clearings, rats near outcrops and rubble; city: rats
dominant, a few hogs at middens; bare board: none. The cat, the loper and the dray hog stay
proposed until each has art, a gait and its own unit.

**2. Seed once, or hold a level?** (a) Seed at worldgen to a density target and top up over
time from the board edge, with animals occasionally walking off it (RimWorld's model);
(b) seed once and never again; (c) events only. Recommend **(a)**: it is one small subsystem,
it makes the wildlife a fact about the world rather than the start, and it is the seam a
storyteller will want.

**3. How many?** Recommend a target scaled by walkable surface area — about **one animal per
1,500 surface cells**, so roughly nine or ten on the 120 × 120 meadow: two sounders of three to
five hogs and three or four rats — **and a hard per-world ceiling** (say 24) so the 64-figure
budget stays the colonists'. Do you want more life than that, or less?

**4. Groups and placement.** Recommend hogs spawn together as a sounder and wander
independently for now (herding is later); rats spawn singly near rock, rubble or a cavern mouth;
nothing within the starting clearing (outside `startingFellRadius`); arrivals come in at the
board edge.

**5. Over time.** Recommend: edge arrivals and departures only; a **nocturnal flag** on the
species (rats out at night, hogs resting); **no breeding**, because there is no age or health
model to hang it on. Hogs raiding a growing zone is the obvious hook and is *not* proposed for
this unit — it is a diet model, and it belongs with the health unit.

**6. The Animals menu.** With no taming there is nothing for RimWorld's Animals tab to show, so
recommend building its **Wildlife** half now and naming the panel *Animals* so the tamed half
lands in it later: one row per animal — kind, status (wandering, resting), layer and distance
from the colony, click to select and jump the camera — under a per-kind count header, with the
hunt and tame columns reserved. **Hotkey F2.** Or do you want the full tab now with taming and
training as the next unit?

**7. Order.** Recommend two PRs, generation then menu, both branched from `main` after #167
merges (the code depends on it). Generation re-bakes the three goldens once, since the animals
join the state hash at tick zero.

## Answers

**2026-09-23, owner:** *"ok pig is good enough for now - please plan out and execute"* — the
recommendations adopted whole, and the plan and execute phases run together on the owner's
word. One number moved in execution: the density is **15** per ten thousand, not seven,
because the census counts *reachable* columns and an animal hops only at ramps, so it reaches
under half the meadow (§2 of design 30). The result is the nine or ten the question asked for.
