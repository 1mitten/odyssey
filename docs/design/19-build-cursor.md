# 19 — The build cursor: showing what you are about to build, and where

**Status: designed 2026-09-17, interviewed before any code.** The owner's report:

> *"I don't know what I'm going to build is going to land — it should actually display the cursor as
> I move it around with what I've selected to build, as it's difficult to determine as I keep
> misplacing floors and walls."*

Four decisions, all interviewed and all taken. §7 is what is built and what is not.

## 1. What is actually wrong today

Two things, and only the second is a matter of taste.

**There is no hover cursor at all.** `OdysseyBootstrap.DrawToolPreview` returns immediately unless
`DesignateDirector.TryPreview` succeeds, and that needs `Dragging` — which is only true between a
press and a release. `SliceCameraRig` has no hover path either: it raises `ToolDragging` while a
button is held, `ToolDrag` on release, and `Picked` for selection. **With a build tool armed and the
button up, nothing whatever is drawn.** The player finds out where a thing lands by placing it.

That is not a tuning problem and no amount of adjusting the drag box fixes it. It is the report.

**And what is drawn during a drag is a marker, never the thing.** Every helper on `ChunkRenderer` —
`DrawCellOutline`, `DrawCellSpanBox`, `DrawCellSpanPlate`, `DrawCellMark`, `DrawCellShade` — draws a
bracket, a box or a plate. **Nothing in the project has ever drawn a building's own module as a
cursor.** The ingredients all exist (the `ModuleLibrary`, instanced submission, and the ghosting the
slice already does for storeys above the player), but they have never been put together for this.

## 2. Decision 1 — the cursor is a ghost of the real thing

Not a wireframe standing for it. The actual module, in the material chosen, drawn translucent.

It answers both halves of the complaint with one object: *what* am I holding, and *where* will it
go. It also reuses an idiom the player has already learned, because a storey above the slice is
ghosted in exactly this way — **translucency already means "this is there but not your business
right now"**, and a thing that does not exist yet is the same sentence.

**One instance per cell, and for a wall it is the wall's core rather than its panels.**
`ChunkMesher.EmitFacePanels` draws a wall as up to four face panels plus a filling core, chosen from
what stands beside it. A ghost has no neighbours yet — it is not in the grid — so there is nothing to
choose from, and reproducing that logic for a thing that does not exist would be a second copy of
the mesher's hardest rule. `ModuleIds.WallCore` is the cell-filling block and is exactly "a
wall-shaped thing of this material". A slab uses the slab module through the same placement
`EmitFloor` uses, which is already a single instance and needs no compromise.

**Draped, not lifted**, like everything fixed to the grid — the rule the stepped-wall fault settled.

## 3. Decision 2 — validity is shown per cell

Green where the order will be taken, red where it will not, **cell by cell**.

The whole-run red added earlier that day was the right first move and is not enough: on the played
board only 21 of the 441 cells within ten of the start will take a slab, so "something in this drag
will build" is nearly always true and nearly never useful. What a player needs to see is *which*
cells — the four that bridge and the fifth that does not.

**It asks `ConstructionGrid.Allows`**, the same method `Place` calls a moment later, so the cursor
and the order cannot come to disagree. That rule has been broken twice in this line of work already.

**It does not filter the order.** The illegal cells are still sent and still refused, because
`DesignatePresenter` deliberately filters nothing — the rule lives in the simulation and the
interface reports it. Dropping them would hide the rule rather than teach it, and would mean a drag
whose result depended on a second copy of the rule in a second place.

## 4. Decision 3 — a footprint on the ground, and a tether to it

A ghost hanging over a drop is ambiguous from every camera angle, and this is a game about layers.

Under each ghost cell: a faint outline painted on the first real surface below it
(`CellGrid.FirstFloorAtOrBelow`, which is the answer falling things already use), and a thin vertical
tether joining the two. It is the shadow trick, and it is the only cue that needs no reading and
works from any angle the rig can reach.

**Nothing when the ghost is standing on that surface** — a footprint drawn under a wall on the
ground is a line round the wall's own feet, which says nothing and costs instances. The cue appears
exactly when the thing is in the air, which is exactly when it is ambiguous.

## 5. Decision 4 — a readout of name, material and layer

One short line near the pointer: **`Wall · wood · L13`**.

The name matters more than it looks: two reachable tools now mean floor-ish things (`Slab` and
`Floor`, §7 of `18-paving.md`), and the cursor is where a player finds out which one they picked up.
The layer settles in words what the footprint settles in geometry, and a number is the right form for
it because a layer *is* a number in this game.

**Cost is deliberately left out for now.** It wants the stockpile totals, which are not wired to the
cursor, and a running total for a drag is a second question — what a box costs — that deserves its
own answer rather than being smuggled into this one.

**This is `15-building.md` §8's "blueprint readout"**, which has been designed and unjudged since
U26. This supersedes that line.

## 6. Where each piece lives

| Piece | Where | Note |
|---|---|---|
| Hover event | `SliceCameraRig` | A tool armed, not orbiting, not over the interface, not dragging: pick and raise `ToolHover`. The one new input path. |
| Which cells and which layer | `DesignateDirector` | Already computes exactly this for a drag; a hover is a one-cell drag that has not started. **No second copy of the geometry.** |
| The ghost, footprint and tether | `ChunkRenderer` | New `DrawGhost`, beside the existing marker helpers and using the same instanced submission. |
| Validity | `ConstructionGrid.Allows` | Asked, never reimplemented. |
| The readout | `HudShell` | **The only genuinely new UI**, and the one with a known trap: a screen position is bottom-left origin and a UI Toolkit panel is top-left, and `PointerOriginTests` exists because that flip shipped wrong twice. Use the shell's own `ToPanel`. |

## 7. What is built, and what is not

Ordered by how much of the complaint each piece answers.

1. **Hover ghost with per-cell validity** — the core. Answers *what*, *where* and *will it work*.
2. **Footprint and tether** — answers *which layer*, without reading.
3. **Readout** — names the tool and the layer in words.

**Not in scope, said plainly rather than discovered:**

- **The ghost does not show a wall's face panels**, only its core block. See §2 for why.
- **No cost, and no running total for a drag.**
- **Nothing here is in a cell, the save or the hash.** It is a cursor. That rule is load-bearing.
- **A ghost is not a blueprint.** Placed sites already draw their own marks; this is only the thing
  under the pointer before an order exists.

## 8. What would make this wrong

Worth writing down, because it is a look feature and nobody has pressed Play on it.

- **A ghost that is too solid reads as a building that is already there**, and a player would stop
  pressing. If it is misread that way, the answer is less alpha, not a different shape.
- **A ghost under every hover may be noisy** while sweeping the camera around with a tool still
  armed. Right-click already puts a tool down, which is the escape; if it still grates, the cursor
  could wait for a few frames of a settled pointer.
- **Per-cell colouring costs an `Allows` per cell per frame** on a live drag. It is a handful of
  array reads each and the board-sized drag that worries it already submits that many intents — but
  `FrameTimeTests` is the check, not this paragraph.
