# 24 — Reading a pile

*Written 2026-09-19, from one owner report. Companion to `24-carrying.md`, which covers the same
item once it is in somebody's arms.*

## 1. The report

> "When I click on wood — I can't see how many is this pile — can we combine piles up to a maximum —
> that could to stockpile a maximum in a tile but then have more graphics to represent a
> third/two-third and a full pile maybe — whatever you recommend and can be achieved"

Three asks, and **the first two were already built**. Checking that first is what made this a small
change rather than a rewrite:

| Ask | State when it was reported |
|---|---|
| combine piles to a maximum | **Done, long since.** `ColonyItems.Spawn` and `PutDown` merge into a resident stack of the same def; `CellHasSpace` holds the ceiling at `ItemDef.stackLimit`, which is **75** for wood. A felled tree yields 27 into the nearest cell with room (`JobDrivers.FellTree`), so a tile holds a shade under three trees. |
| see how many are in it | **Said, but not seen.** The inspect pane printed "27 in the pile" on the state line under the title. The owner read that pane and did not find it. |
| graphics for a third / two thirds / full | **Missing for wood only.** `ItemHeap` had drawn stone, iron ore and coal as a growing scatter since it was written; wood's row in `Recipes` was `null`, so 1 wood and 75 wood were the identical single prop in the identical place. |

So the work is: **move the number to where the eye lands, and let wood into the ramp that already
existed.**

## 2. The count is the title

`InspectModel` now builds an item's title as `"Wood × 27"`, and the state line below it stopped
counting and went back to saying where the thing is lying.

The count was not missing, it was **outranked**. A pile's size is the first question asked of it and
it was answering in the pane's smallest, greyest line, under a title that repeated what the icon had
already said. Saying it in both places would have been worse than either: a pane that repeats itself
teaches the reader to skim it.

`× n` and not `n in the pile`, because it is the form the growing pane already uses
(`Carrot × 5 — N% grown`) and one game should have one way of writing a count.

**A stack of one is not counted.** "Wood × 1" is a stilted way of saying "a log", and the number is
only worth the width when there is something to learn from it.

## 3. Wood joins the heap ramp

`ItemHeap` is the existing mechanism and it needed no new art: a stack draws *n* copies of the item's
own prop, placed on a sunflower spiral from the item's id, where *n* rises with the stack. Giving
wood a recipe is four numbers.

They are not stone's numbers:

| | wood | stone |
|---|---|---|
| fewest / biggest | **1 → 3** | 2 → 7 |
| full at | 75 | 75 |
| spread | 0.52 m | 0.62 m |
| size jitter | 0.10 | 0.22 |

**Three, not seven, because the prop is a bound log pile and stone is a boulder.** `SM_Prop_LogPile_01`
at its catalogue scale of 0.4 is a wide thing; seven of them in a 2.5 m cell is a log-jam, not a
stock. Three is what the cell holds before they read as one mass.

Three is also exactly the ramp that was asked for — *a third, two thirds, full* — arrived at through
the mechanism already in the game rather than through three new props and a tier system nothing else
would use. And the steps land somewhere legible: with 27 wood a tree and a limit of 75, one bundle is
about one tree, two is about two, three is a full square.

The jitter is lower for the same reason the count is: a log pile is a manufactured-looking object and
a 22% size wobble on one reads as a modelling error, where it reads as natural variation on a rock.

## 4. But a carried load is still one bundle

`Recipe.CarriedAsHeap`, false for wood and true for the three rubbles.

The ground heap and the armful are separate questions, and they were only ever the same answer
because rubble was the only thing that scattered. `ItemHeap.Armful` draws a fixed three, so letting
wood in unchanged would have put **three bound log piles in two hands** — and would have done it to a
load the owner had looked at and tuned the same week (`24-carrying.md` §6b, the yaw fix of
2026-09-19). One bundle is what a person carries.

This is the same argument as `24-carrying.md` §3a reached from the other end: the arms cap out well
below what the floor can show, so the floor gets the ramp and the arms get the words.

## 5. What a test can and cannot say

Held by `ItemHeapTests`:

- wood scatters at all, and rations still do not;
- the ramp is 1 at a stack of one, 3 at the limit, and never 4 however big the stack;
- it is **monotonic** across every stack size — a pile that grew must never look smaller;
- wood's `CarriedAsHeap` is false and stone's is true.

Held by `InspectReadoutTests`: the title of a 27-stack, and that a stack of one is not counted.

**What no test can say** is whether three bundles read as "full" at the play camera, and whether the
count in the title is now findable. Both are §6.

## 6. Reaching the tile under a pile (owner, 2026-09-19, second look)

> "it seems to be difficult to click on a tile with wood in — always the item takes precedence when
> clicking on a tile, then clicking on again would then go to the tile info — is this possible"

Yes, and it is the right shape. A thing wins the **first** click, which is correct — a pile of wood
is what the player pointed at, and that precedence was itself a fix (`DirectorTests`, 2026-09-17: a
pile resting on bare ground was unselectable because the click fell through to the cell).

But the precedence was absolute, so **there was no way at all to reach the tile under a pile** — and
a stockpile is wall-to-wall piles, which is precisely where a player wants to ask what the floor is,
whether the zone reaches here, how fast it is to walk over.

So: **thing, then cell, then thing again.** Two rungs and a loop, which is as deep as a repeated
click can go before the player has to count what they are on.

**The cycle is not a counter.** `SelectionDirector.Pick` reads it off the selection itself — the
same cell, holding the thing already selected — so anything that clears or moves the selection
starts it over with no flag to remember to reset, and a pile hauled away while its cell is held
cannot leave the cycle stranded. A colonist is never cycled past: a pick that lands on one selects
them and clears the cell tier outright, so clicking her twice is the same colonist both times.

## 7. And the bed, which was a quarter of a cell out

The same message reported a bed being *"really specific"* to click. Measured rather than reasoned
about, and it was neither the pane nor the footprint: `SlicePicker` resolved a **non-occluding** cell
at its floor plane, and a bed is drawn 0.70 m above that floor. At 48° that is **a quarter of a
cell** of drift, so the far end of the drawn bed selected the grass behind it and the grass in front
selected the bed.

`WorldRenderModel.StandHeight` is the seam: a cell whose edifice stands without occluding offers its
own top plane to the ray, and offers it first because it is nearer. Only a bed answers today; the
next non-occluding thing that stands up adds a line, and the whole story is in `docs/bug-patterns.md`
under *"What a thing is drawn on is not the surface it is picked at"*.

## 6. Open — for the keyboard

- **Does a full tile read as full?** Three log piles at 0.52 m spread, versus one. If three still
  reads as "some wood", the next step is four with a tighter spread, not a bigger prop.
- **Is `Wood × 27` findable now?** It is in the pane's largest type. If it is still missed, the
  problem is the pane's shape rather than this line.
- **Is 75 the right ceiling?** It is a shade under three trees to a tile. Lower makes stockpiles
  sprawl and the ramp coarser; higher makes a full tile mean less.
- **Does the second click read as a cycle or as a misfire?** Nobody has clicked twice on a pile.
  If it reads as the game ignoring the first click, the alternative is a modifier rather than a
  repeat.
- **Is the bed comfortable to click now?** It should be clickable exactly where it is drawn. The
  tell that something is still wrong is a *direction*: if it is fiddly from one camera bearing and
  fine from the opposite one, the drift has been reduced rather than removed.
