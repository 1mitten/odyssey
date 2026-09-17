# 18 — Paving: a floor you lay on the ground

**Status: scoped, not built.** Written 2026-09-17 after the owner played U29 and reported *"I should
be able to just build a floor but nothing happens."* This is the scope and the cost; nothing here is
implemented, and no code should be written against it until the owner has approved the shape.

Design and mechanics only. Names live in `docs/design/icon-keys.csv`, and three of them are already
there — see §6.

## 1. Why the report was not a bug

U29's floor is a **structural slab**: a thing at the boundary between two layers, roof of the one
below and floor of the one above, held up by the support rule. `AllowsSlab` therefore refuses a cell
that *already* has a floor, which is exactly what the ground under your feet is.

Measured on the played wooded meadow, at the layer the slice starts on:

| | cells |
|---|---|
| already have a floor (the ground you are standing on) | 2,408 |
| no support (open air, nothing grounded within reach) | 9,198 |
| solid rock or earth | 119 |
| something standing in the way | 289 |
| **would be built** | **2,386** |

2,386 legal cells sounds generous until you look at where they are: **within ten cells of the start,
21 of 441.** They are the lips of terrace drops, scattered over the whole board. A player dragging
over the grass in front of them is refused about 95% of the time, correctly, and until 2026-09-17
the cursor was bright green over all of it. That half is fixed — the cursor goes red when a drag
would build nothing — but the fix only tells the player "no". It does not give them the thing they
were reaching for.

**The thing they were reaching for is a floor covering**, which this game does not have. Two
different objects share one English word:

| | a **slab** (U29, built) | a **covering** (this doc, not built) |
|---|---|---|
| What it is | structure: roof below, floor above | a surface laid on ground that is already there |
| Where it goes | a boundary with nothing under it | a boundary that already has ground under it |
| Needs support | yes, and it collapses without it | no — it rests on what is already holding it |
| Can it fall | yes, cascading | never; it is grounded by definition |
| What it is for | building upward; bridging | walking speed, cleanliness, beauty, rooms |

RimWorld has both and calls them roofs and floors; we have promoted the roof to a real thing, which
makes "floor" ambiguous in a way it is not there. §7 asks the owner whether to rename.

## 2. The rule

A covering wants **the exact opposite of a slab's first question, and none of its second**:

- the cell must **have a floor already** (`CellGrid.HasFloor`) — something to lay it on;
- the cell must have **no covering and no slab yet** (`Floor[index] == SlabNone`);
- the cell must be in open air, out of water, with nothing standing in it, on buildable terrain —
  the rules `Allows` already applies to everything;
- **no support check at all.** A covering over solid ground is grounded by `SupportSolver.IsGrounded`
  without anybody asking, so it can never collapse and needs no rule saying so.

That is one branch in `ConstructionGrid.Allows` beside `AllowsSlab`, and it is the whole mechanic.

## 3. Where it is stored — no new state

**A covering is written to `Floor[]` and `FloorStuff[]`, like every other slab**, as a fifth slab
kind `CoreContent.SlabPaved`. This is the part worth arguing about, so the argument is written down:

- `Floor[]` is **already saved and already hashed**, so a covering costs no new array, no new save
  section and no change to `StateHashCoverageTests`. U29 made the same argument for `SlabBuilt` and
  it held.
- `HasFloor` reads `Floor[index] != 0 || IsSolidTerrain(below)`, so a covering laid over ground
  makes no difference to anything that asks whether a cell is floored. It was floored already.
- `WorldRenderModel.FloorModule` returns the stuff group's slab module for **any** non-zero floor and
  never looks at the kind, so a covering draws the moment it is written, in its own material's tint,
  with no catalogue row and no mesher edit — exactly as `SlabBuilt` did.
- A **distinct kind** rather than reusing `SlabBuilt`, for the reason `SlabBuilt` is distinct from
  the generator's three: it is the only way to ask "is this ours, and is it structure or surface?"
  A future line that strips paving without touching floors needs that question to exist.

**The one risk this creates is drawing, not data.** A covering sits on the same plane as the top face
of the ground block beneath it, and two coplanar surfaces z-fight. `ChunkMesher.EmitFloor` places the
slab at `CellMetrics.FloorCentre`; the prefab is 0.10 m deep and may already resolve it, but this is
**unmeasured and is the first thing to check**, before any of the rest is written. If it does fight,
the fix is a millimetre lift in `EmitFloor` for covering kinds only — the same shape of answer the
banks and the water already use, and it must not move `SlabBuilt`, whose seam is measured at 1.1 mm.

## 4. Which cell a click means

**A covering uses the wall's lift, not the floor's.** A click on grass names the ground *block*
(`SlicePicker` stops at the first surface) and a covering goes in the air cell above it — which is
precisely what `ConstructionGrid.StandingOn` already does for a wall. `StandingOver`, U29's lift over
anything that fills a cell, is the slab's and stays the slab's.

So the three lifts line up, one per kind of thing, and each is one line:

| armed | named cell | order lands |
|---|---|---|
| wall | ground block | the air above it (`StandingOn`) |
| covering | ground block | the air above it (`StandingOn`) |
| slab | a wall, or anything filling a cell | the boundary on top of it (`StandingOver`) |

**`DesignateDirector.WorkingLayer` must be null for a covering.** A slab takes its layer from the
slice because a pointer cannot name open air; a covering is always laid on a surface, which is the
one thing a pointer *can* name, and forcing it onto the slice layer would make paving unusable the
moment the player scrolled a layer up. One condition in
`DesignatePresenter.TellTheDirectorWhichLayerItIsWorkingOn`, and it wants a test of its own.

## 5. Taking it up

`DesignationGrid.TryTakeApart` gains `SlabPaved` beside `SlabBuilt`, and `ConstructionGrid.RemoveSlab`
already removes whatever our slab kinds are. Refund is the existing half.

**Open, and the owner's:** should taking up paving be a *Deconstruct* order, or a separate "strip"
tool? Deconstruct is one line and is what the refund logic expects. Against it: a player
deconstructing a room usually means the walls, and a tool that also lifts the floor under them may
take more than was meant. Recommend **Deconstruct**, and revisit only if it bites in play.

## 6. The three coverings, which are already named

`icon-keys.csv` has carried these since the registry was written, in a `Floors` palette category that
has never had a live tool in it:

| key | label | description as the owner wrote it |
|---|---|---|
| `ui.arch.tool.deckplate` | Deck plate | Metal flooring. Fast to lay |
| `ui.arch.tool.grating` | Grating | See and fall through. Light passes |
| `ui.arch.tool.tile` | Tile | Clean and pretty. Hospitals and kitchens |

So **no new names, no wiki content change, and no owner approval needed on naming** — the labels and
descriptions are already yours and already published.

**Recommend building one, not three.** `Deck plate` in steel is the whole mechanic; tile and grating
are a `BuildingDef` row each afterwards, and grating in particular is not a covering at all — *"see
and fall through"* is a slab you can see through and stand on, which is a different thing wearing the
same category, and it should wait until there is something below worth seeing.

**Deliberately not in scope:** what a covering is *for*. Walking speed, cleanliness, beauty and room
stats are the reason paving exists in the genre and none of those systems exist here, so a first
covering is a surface that looks different and does nothing else. That is worth saying out loud
rather than discovering: **paving will be cosmetic until rooms are.**

## 7. One naming question for the owner

With both objects in the game, `Structure → Floor` and `Floors → Deck plate` sit two clicks apart and
mean different things. Options, ranked:

1. **Rename U29's tool to `Slab`.** It is the accurate word, `02-world-and-layers.md` already uses it
   throughout, and it leaves "floor" to mean what a player expects. One line in `icon-keys.csv` plus a
   wiki and label rebuild; the *key* stays `ui.arch.tool.roof`, because a key is forever.
2. Leave both called floor and rely on the category. Cheapest, and it is the confusion that produced
   this doc.
3. Rename the coverings to `Flooring`. Reads oddly beside `Deck plate`, `Grating`, `Tile`, which are
   already specific.

## 8. Cost

Comparable to U29's plumbing with the hard half removed — no support, no collapse, no falling.

| | |
|---|---|
| `BuildingDef` gains one field (`covering`) | S |
| `CoreContent.SlabPaved`, and the fifth kind through `Raise` / `TryTakeApart` | S |
| The `Allows` branch and the lift wiring, including `WorkingLayer` staying null | S |
| One `BuildingDef` row, one palette tool, one content fingerprint re-baked | S |
| The z-fighting check, and a lift if it fights | **unknown until measured — do this first** |
| Tests: the journey, the rule and its mirror, the lift, take-up and refund | M |

**Estimate: one session**, and the only thing that could make it two is §3's drawing risk. Nothing
here needs new art, new save state, a hash change or a wiki edit.

## 9. What this does not change

The slab, its support rule, its collapse and its cursor are all untouched. A covering is a fifth
value in an array that four values already live in, and every system that reads `Floor[]` keeps
reading it the same way.
