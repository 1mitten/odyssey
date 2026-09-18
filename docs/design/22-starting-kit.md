# 22 — What a colony starts with

**Status: decided 2026-09-18, from the owner, and implemented the same day.** The numbers live in
`ScenarioDef.Playtest()` and nowhere else.

Read first: `ColonyScenario.cs` (the def and the placement), `docs/design/08-milestones.md` for
where a food economy is meant to arrive.

---

## 1. What changed and why

Owner, 2026-09-18: *"don't start the game with any scrap and only 3 meal piles, but a couple piles
of stone and wood — make this the default for now."*

A new colony used to arrive with **144 meals, eight pieces of scrap, five beds, a nine-cell
stockpile and nothing to build with**. That is the wrong shape for a colony sim's first hour in
three separate ways:

- **Food was a non-question for a fortnight.** Five colonists eat about 1.8 meals a day each, so
  144 is roughly sixteen days of grace on a board where nothing yet makes food. The pantry was
  sized for the ten-day headless soak — it exists so the *gate* measures the simulation rather than
  a famine — and that number then followed the player into the game it was never sized for.
- **The only hauling job on the board was scrap.** Salvage was scattered at tick zero so hauling
  had work from the first tick, back when hauling was the thing being demonstrated. There is now
  felling, mining, building, floors, paving and a growing zone; scrap has no use, no recipe and no
  buyer, so eight piles of it is eight jobs that teach a player that hauling does not lead
  anywhere.
- **A player who wanted to put up a wall had to fell a tree first.** The build palette, the floor
  tool and the paving tool all landed in M3 and all of them are gated behind an axe.

## 2. The kit

| | Was | Is |
|---|---|---|
| Meal piles | 12 × 12 = **144 meals** | 3 × 12 = **36 meals** |
| Scrap | **8** loose pieces | **none** |
| Stone | none | **2 piles of 75 = 150** |
| Wood | none | **2 piles of 75 = 150** |
| Beds, stockpile, colonists | 5, 9 cells, 5 | unchanged |

36 meals is three or four days at the measured burn — enough that food becomes a *question* early
rather than an emergency on the first morning, which is the thing that cannot be tuned by looking
at it in the editor. 150 of each material is a hut and a few floors.

**Full stacks, because a pile is authored as a pile.** Stone and wood both stack to 75, so a full
pile is one square rather than a scatter. A scenario wanting 150 stone says *two piles of 75*; the
placement clamps to the item's own stack limit rather than laying down a stack of 200 that no hauler
could carry and no stockpile could take apart — `ColonyItems.Spawn` checks the limit only when a
cell already holds something, so this is the only place that check happens for an empty one.

**The material piles come off the food's storey budget**, and go down before the beds. They are the
same kind of thing — a pile on the ground the colony wakes up beside — and asking for their spots
separately would be a second answer to "which floor does the colony start on" that nothing keeps in
step with the first.

## 3. Only `Playtest` moves, and that is the whole safety argument

`ScenarioDef` has two presets. **`Bare()` is the measurement baseline**: the golden table, the
ten-day soak and every determinism gate build on it, so a kit added to the *field defaults* — which
is how this would most naturally have been written — re-bakes three hashes to say nothing at all
about the simulation.

So the new fields default to zero and `Playtest()` overrides them. `ScenarioDefTests` pins **both**
sides of that: `BareIsTheBaselineAndGivesNoOrders` asserts the baseline's own numbers outright
rather than comparing them to Playtest's, and `PlaytestStartsWithABuildingKitAndNoScrap` asserts the
kit on the *placement* rather than on the def, so a kit the map has nowhere to put fails in the fast
tier rather than in a playtest.

The test that used to say the two scenarios *"differ only in their orders"* is gone. Its real job
was stopping Bare drifting, and that job is now done by pinning Bare rather than by tying it to a
preset that is meant to be tuned.

## 4. Invited tuning

Nothing derives from these numbers and nothing else reads them. They are five integers in one
method, and the owner is expected to move them once they have played a first morning:

- **Whether 36 meals is three days of tension or three days of anxiety.** If the colony is starving
  before anybody has built anything, the answer is more meals, not faster growing.
- **Whether 150 of each material is generous.** It is deliberately enough to finish something, on
  the argument that a first building teaches more than a first shortage.
- **Whether scrap should come back with a use** rather than come back as hauling practice. It is
  still an item, still in the wiki, and still spawnable from the debug menu.

## 5. Not in this unit

- **Difficulty presets.** The fields exist to be varied per scenario and nothing varies them yet.
- **Starting research, tools or clothing.** None of those exist.
- **Where the kit is placed relative to the beds and the stockpile**, beyond the ordering above.
