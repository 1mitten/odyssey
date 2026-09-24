# 42 — Walls down: seeing inside a building

**Status: designed and built 2026-09-24, not yet played.** Branch `claude/walls-down`, worktree
`D:\code\odyssey-walls-down`. Research: `docs/research/b-walls-down-cutaway.md`.
**Read first:** `06-rendering-and-camera.md` §3 (the slice), §3a (the depth decides the treatment),
§3c (what can be clicked is what is drawn solid); ADR 0006 (the six above-modes).

## 1. The request

Owner, 2026-09-24: *"it is hard to see your colonists inside your own building."* At or above the
surface every storey above the slice is drawn solid, and the walls of the active storey are always
3 m tall. A room is therefore hidden behind its own walls and under the storey above it.

What was asked for: a toggle beside the R/F depth control, **on by default**, that **removes** the
walls rather than making them translucent, and leaves **a low stump** so the plan still reads.
**Build mode ignores it** and shows the whole construction, as it does today. Translucent floors
were left to try in play and were not built (§8).

## 2. The interview (owner, 2026-09-24)

| Question | Answer |
|---|---|
| What counts as build mode | The Build palette open, **or** Build or Deconstruct armed. Mine, Fell, Cancel and the zone tools keep the stumps, because seeing inside helps there too. |
| The stump | A plain capped block **0.75 m** tall (a quarter of the storey), in the wall's own stuff tint. The Synty panel is not squashed. |
| Storeys above | **Built storeys above the slice are hidden**; the landscape above stays (§3b's "never cut away"). R/F chooses the storey. |
| What lowers | Walls and windows, doors (the frame becomes a stump and the leaf is hidden), pillars. **Natural rock does not.** |
| Key | **H**, rebindable in Settings → Keys |
| The toggle | A drawn `HudGlyph` under the rail's "R / F" hint, lit while on, named with its key in the tooltip |
| Remembered | Per player, in the settings store, like see-through. On for a first run. |

## 3. The rule, and its one owner

Two things decide whether walls are lowered this frame:

- **The player's choice**, `GraphicsOption.WallsDown` in `SettingsDirector`, stored under
  `ui.settings.wallsdown`. The rail button, the H key and the Settings → Graphics row are three
  switches over that one value.
- **Build mode**, `WallsView.Lowered(choice, paletteOpen, tool)` in `Odyssey.Hud`. It is Unity-free,
  so the fast tier covers it.

The composition root evaluates `WallsView.Lowered` **once a frame** and writes the answer to
`SliceSettings.wallsLowered`. **Everything that draws, picks or places reads that field and nothing
else.** Pattern P1, one rule with two owners, is exactly what this avoids: a renderer and a picker
each working out build mode for themselves would disagree the first time a tool is added.

`SliceSettings` then answers the two questions everything asks:

- `LowersWallsOn(active, layer)` — walls on this layer are drawn as stumps. True for the active
  layer and every layer below it.
- `HidesBuiltOn(active, layer)` — built things on this layer are hidden. True above the active layer,
  **only where the above-mode draws solid**. Underground the one layer above is already x-rayed, and
  that ghost is left alone.

## 4. Drawing: chosen when drawn, never re-meshed

The toggle flips every time the palette opens, so it must cost nothing to flip. Re-meshing the board
is about 900 chunks at 11 a frame (`MeshBudgetPerFrame`), which is seconds of visible arrival.
`ChunkBatch` already rests on this principle: the roof is a separate list so the slice can drop it
"without rebuilding anything".

**The chunk batch gains two lists.**
- **`Walls`** holds what lowers: the face panels and core of every `WallPanel` edifice (wall,
  window, vault wall), a door's frame, and a pillar.
- **`Stumps`** holds their replacements, meshed at the same time.

`Body` no longer holds any of these. The mesher emits both forms, and the renderer draws one of them.

| Layer | Not lowered | Lowered |
|---|---|---|
| active and below | Body + **Walls** + Roof (as before) | Body + **Stumps** + Roof |
| above, drawn solid | Body + Walls + Roof | **landscape buckets only**, from Body and Roof |
| above, ghosted | unchanged | unchanged |

- **The stump.** `CellMetrics.StumpHeight` (0.75 m) of the plain `WallCoreModule` block, draped like
  the core, in the wall's stuff tint. A window gets one too, because a window is part of a wall line.
- **A doorway.** Two jambs, 0.35 m along the wall line, placed in the frame's own face and orientation
  (`DoorFacing`). The opening between them is what makes a doorway read as a gap in the line.
- **A pillar.** The same block, at the pillar module's own footprint.
- **Landscape.** Any bucket whose tint carries a terrain, foliage, water, tree or whole-surface bit
  (`TintCode.IsLandscape`). Anything built carries a plain stuff tint, a linen tint or the store edge.
  This is what keeps a hill, its grass and its trees above the slice while a house's upper storey
  goes.

The sight-line fade is untouched and applies to whatever is drawn.

## 5. Everything that has to agree with the picture

§3c's rule is that anything drawn solid can be clicked, and nothing that is not drawn can be. So:

- **Picking** (`SlicePicker`).
  - A lowered wall, window, pillar or door is hit **only up to the top of its stump**, the way
    `StandHeight` already works for beds and shelves. A click over a stump reaches the floor behind
    it.
  - On a layer that `HidesBuiltOn` covers, a built edifice, a built floor, a site and a line offer
    nothing. The ground and the trees still do.
- **Order marks** (`WorldRenderModel.MarkHeight(index, lowered)`) sit on top of the stump rather
  than floating 2.25 m above it.
- **Door leaves** (`DoorDirector`) are not drawn where walls are lowered or built things hidden. The
  door's state still runs, so it still opens, closes and sounds.
- **Actors.** Colonists, animals, items, corpses, fire, health bars and lock-on rings are hidden when
  they are above the slice on a hidden layer **and stand on something built**: a floor slab in their
  cell, or a built edifice. A colonist on a hilltop stands on landscape and is still drawn; one on
  the upper storey of a house is not. The one question is
  `SliceSettings.HidesStandingAt(active, cell, model)`.
- **Construction sites** above the slice on a hidden layer are not drawn, with the storey they
  belong to. On the active layer a wall site keeps its full-height translucent ghost: it is the
  order the player gave, and it is already see-through.

## 6. Build mode

`WallsView.Lowered` is false while the Build palette is open, or while Build or Deconstruct is armed.
Everything then draws exactly as it did before this document existed. Putting the tool down lowers
the walls again **on the next frame**, with nothing re-meshed.

**The rail icon keeps showing the player's choice** and dims while build mode overrides it, so a
player can see why the walls came back.

## 7. The toggle

- **Placement.** Under the Depth rail's "R / F" hint, in the gutter it shares with the orders strip.
- **The icon.** A drawn `HudGlyph`: `WallsUp` (a full brick wall) while off, and `WallsDown` (a
  stump, with the rest of the wall as a dashed outline) while on. It is lit in the accent colour
  while on, as the views strip is. It is drawn rather than typed because neither shipped font has a
  wall character, and `HudFontTests` would refuse one.
- **The key.** `HotkeyAction.WallsDown`, **H**, appended to the action list so no stored binding
  moves, and polled in `SliceCameraRig.ReadKeyboard` beside R and F.
- **The words.** `ui.settings.wallsdown` names the option everywhere it is shown, and
  `ui.keys.wallsdown` names the binding row. Neither is a literal.

## 8. Not done, on purpose

- **Natural rock is not lowered** (owner). A mine is read by slicing, not by lowering.
- **Floors are not made translucent.** Every complaint the research found was about see-through
  leftovers. Hiding the storeys above is what was asked for, and translucency is a later experiment
  if hiding proves not enough.
- **No cutaway mode** (only the walls nearest the camera lowered). It depends on the camera and is a
  second mode to learn; it can be added as another state of the same switch if play asks for it.
- **The wall ghost on the build cursor** is untouched, because build mode shows full walls anyway.

## 9. What is owed to play

`docs/plans/playtest-queue.md` holds the row. The questions only a person can answer:

- Does 0.75 m read as a wall line from the play camera?
- Can you tell what each colonist indoors is doing?
- Does a doorway read as a gap?
- Does opening B, or arming Deconstruct, bring the walls up without a visible delay?
- Is it confusing when a building on higher ground above the slice is hidden?
