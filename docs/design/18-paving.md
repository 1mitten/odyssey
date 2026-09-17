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
makes "floor" ambiguous in a way it is not there. **Both keep the word anyway** — the owner's
decision, §7 — so the palette category and the descriptions carry the distinction instead.

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

**The one risk this creates is drawing, not data** — and it has now been measured, before a line of
the unit was written. A covering sits on the same plane as the top face of the ground block beneath
it, and two coplanar surfaces z-fight.

**Measured 2026-09-17, `PavingProbe`: it does not fight.** 172 covering cells laid over open grass
on the played board, photographed at 18 m, 60 m and 150 m and once nearly edge-on at 6° of pitch —
the worst case, where the depth buffer has least to separate two coplanar surfaces with. **No
shimmer, no speckling and no bleed-through at any of the four.** The prefab's own 0.10 m depth
already lifts the drawn slab clear of the plane, so `EmitFloor` needs no covering-specific lift and
`SlabBuilt`'s measured 1.1 mm seam is not disturbed. The probe needed no part of U42 to exist: a
covering's geometry is decided entirely by where `EmitFloor` puts a slab, and that does not care
which kind of slab it is.

**What the probe found instead, which was not in the estimate: grass grows through paving.** The
tufts and the flower scatter still draw on a paved cell, because the scatter is keyed off the
terrain and knows nothing about `Floor[]`. It is unmistakable at every range. Nothing else in the
picture is wrong — the deck reads as a deck sitting proud of the grass, and trees correctly refuse
to be paved over — so this is the single drawing change U42 carries: **the scatter must skip a cell
that has a floor over it.** That is one condition where the scatter is gathered, it is presentation
only, and it is in neither a cell nor the save nor the hash.

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

## 7. Naming — settled: both say floor (owner, 2026-09-17)

The question was whether to rename U29's tool to `Slab`, so that "floor" meant only the covering.
**The owner's answer is that both say floor.** The recommendation to rename is overruled and this
section records that, so no later session re-opens it: `Structure → Floor` stays `Floor`, and the
coverings keep the specific names they already have — `Deck plate`, `Grating`, `Tile`. Nothing in
`icon-keys.csv` moves, and there is no wiki or label rebuild in this unit at all.

**What that costs, and where the cost has to be paid.** Two tools a player can reach both say floor,
so the *word* can no longer be what tells them apart — the palette category and the cursor have to.
That is a constraint on U42 rather than a problem with the decision, and it is already half met:

- **The category is the disambiguator.** `Structure` holds the thing that spans and falls;
  `Floors` holds the things you lay on ground. A covering must never appear under `Structure`, and
  the slab must never appear under `Floors`, however tempting a shortcut looks later.
- **The descriptions do the rest, and they are already written.** The registry has
  *"A slab you walk on. It roofs the layer beneath"* against `ui.arch.tool.roof` and *"Metal
  flooring. Fast to lay"* against `ui.arch.tool.deckplate`. Those two lines are the whole
  distinction, so whatever surfaces a tool's description — a tooltip, a popover — matters more here
  than it would if the names differed.
- **The cursors already differ and now must keep differing.** A slab draws a plate on the boundary
  it will occupy and goes red where it cannot stand; a covering will draw the same plate but will
  almost never be refused. That is a real cue and it falls out of the rules rather than being
  arranged.

**One thing to watch in play.** With both called floor, a player who arms the wrong one gets a
cursor that looks nearly identical and an order that behaves completely differently. If that bites,
the cheapest fix is not a rename but a clearer readout of *which* tool is armed — and the honest
note is that this doc recommended the rename and was overruled, so the failure mode was predicted
rather than missed.

## 8. Cost

Comparable to U29's plumbing with the hard half removed — no support, no collapse, no falling.

| | |
|---|---|
| `BuildingDef` gains one field (`covering`) | S |
| `CoreContent.SlabPaved`, and the fifth kind through `Raise` / `TryTakeApart` | S |
| The `Allows` branch and the lift wiring, including `WorkingLayer` staying null | S |
| One `BuildingDef` row, one palette tool, one content fingerprint re-baked | S |
| ~~The z-fighting check, and a lift if it fights~~ | **measured 2026-09-17: it does not fight. No work.** |
| Grass scatter must skip a cell with a floor over it — found by the same probe | S |
| Tests: the journey, the rule and its mirror, the lift, take-up and refund | M |

**Estimate: one session, and the thing that could have made it two is closed.** §3's drawing risk
was measured before anything was written and came back clean; what it turned up instead — grass
growing through the paving — is one condition in the scatter. Nothing here needs new art, new
save state, a hash change or a wiki edit.

## 9. What this does not change

The slab, its support rule, its collapse and its cursor are all untouched. A covering is a fifth
value in an array that four values already live in, and every system that reads `Floor[]` keeps
reading it the same way.
