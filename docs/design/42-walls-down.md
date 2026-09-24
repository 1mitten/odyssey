# 42 — Walls down: seeing inside a building

**Status: designed and built 2026-09-24; played, revised (§3a, §10) and played again the same day — owner: *"excellent"*. Ready to merge (PR #197).** Branch `claude/walls-down`, worktree
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
| Storeys above | **Built storeys above the slice are hidden**; the landscape above stays (§3b's "never cut away"). R/F chooses the storey. *Narrowed after the first play (§3a): only **upper storeys** are hidden — a house standing on a higher terrace's ground stays, as stumps.* |
| What lowers | Walls and windows, doors (the frame becomes a stump and the leaf is hidden), pillars. **Natural rock does not.** |
| Key | **H**, rebindable in Settings → Keys |
| The toggle | A drawn `HudGlyph` under the rail's "R / F" hint, lit while on, named with its key in the tooltip. *After the first play the hint went and the switch took its row (§7).* |
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

`SliceSettings` then answers the three questions everything asks:

- `LowersWallsOn(active, layer)` — walls on this layer are drawn as stumps. True on every drawn
  layer while the walls are down, so a house on a terrace above shows its plan the same way.
- `HidesStackedOn(active, layer)` — upper storeys on this layer are hidden. True above the active
  layer, **only where the above-mode draws solid**. Truly underground the one layer above is already
  x-rayed, and that ghost is left alone.
- `HidesStandingAt(active, cell, model)` — whatever stands in this cell goes with a hidden storey.

### 3a. A lower terrace is ground, and only upper storeys are hidden (first play, 2026-09-24)

The owner, the same day: *"works brilliantly but … when I'm at depth 10 for example (ground) — I
should be able to see the buildings and floors above me but they were transparent."*

**Why they were transparent.** `SliceSettings.surfaceLayer` is the one layer the colony opened on,
and the terraced ground runs several layers below it. On the played board (seed 1, measured by
`LandscapeBandTests.WithTheWallsDownEveryTerraceIsAboveGround`) the colony opens on L12 and the
lowest terrace's rock tops out at L8, so its ground is walked on L9. A player standing on real
ground at L10 was therefore "underground", and underground the one layer
above is an x-ray and nothing higher is drawn. The first build left that ghost alone on purpose,
which is exactly where the owner met it.

**The rule now, with the walls down**: the slice is above ground anywhere at or above the lowest
ground — `WorldRenderModel.LowestOutdoorLayer + 1`, handed over once a frame as
`SliceSettings.landscapeFloor` — so nothing above a terrace is see-through. Only a slice beneath the
whole landscape keeps the x-ray, because solid rock drawn overhead would bury a mine. With the
walls up nothing changes: the owner chose this over fixing the surface for everyone, which would
also have drawn rock solid over a tunnel dug into a hillside.

**And what is hidden above narrowed** (owner's choice, same interview): not everything built, but
only what is **stacked** — built on top of something built rather than on the ground
(`WorldRenderModel.IsStackedAt`: built, and no solid terrain directly beneath). The first floor of
the house being looked into goes; a house on the terrace beside it stays, drawn as stumps with its
floor, and so does anybody in it. It is absolute rather than relative to the slice, so the mesher
bakes it into each bucket's key (`InstanceBucket.Stacked`) and the renderer still only skips
buckets. What can change it is terrain changing underneath, and digging a cell out already dirties
the chunk above (`MineJob.MarkChunksAround`).

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
| above, drawn solid | Body + Walls + Roof | Body + **Stumps** + Roof, **stacked buckets skipped** |
| above, ghosted | unchanged | unchanged |

- **The stump.** `CellMetrics.StumpHeight` (0.75 m) of the plain `WallCoreModule` block, draped like
  the core, in the wall's stuff tint. A window gets one too, because a window is part of a wall line.
- **A doorway.** Two jambs, 0.35 m along the wall line, placed in the frame's own face and orientation
  (`DoorFacing`). The opening between them is what makes a doorway read as a gap in the line.
- **A pillar.** The same block, at the pillar module's own footprint.
- **Stacked.** A bucket built in a cell with no solid terrain under it (`InstanceBucket.Stacked`,
  part of the key). The first build filtered by tint instead — anything not landscape above the
  slice — which hid a house on a higher terrace along with the upper storeys (§3a).

The sight-line fade is untouched and applies to whatever is drawn.

## 5. Everything that has to agree with the picture

§3c's rule is that anything drawn solid can be clicked, and nothing that is not drawn can be. So:

- **Picking** (`SlicePicker`).
  - A lowered wall, window, pillar or door is hit **only up to the top of its stump**, the way
    `StandHeight` already works for beds and shelves. A click over a stump reaches the floor behind
    it.
  - On a layer that `HidesStackedOn` covers, a stacked edifice or floor, and a site with no ground
    under it, offer nothing. The ground, the trees and a house on a terrace still do.
- **Order marks** (`WorldRenderModel.MarkHeight(index, lowered)`) sit on top of the stump rather
  than floating 2.25 m above it.
- **Door leaves** (`DoorDirector`) are not drawn where walls are lowered or built things hidden. The
  door's state still runs, so it still opens, closes and sounds.
- **Actors.** Colonists, animals, items, corpses, fire, health bars and lock-on rings are hidden when
  they are above the slice **in a stacked cell** — the upper storey of a house. A colonist on a
  hilltop, or on the ground floor of a house up on a terrace, is still drawn. The one question is
  `SliceSettings.HidesStandingAt(active, cell, model)`.
- **Construction sites** above the slice with no ground under them are not drawn, with the storey
  they belong to. On the active layer a wall site keeps its full-height translucent ghost: it is the
  order the player gave, and it is already see-through.

## 6. Build mode

`WallsView.Lowered` is false while the Build palette is open, or while Build or Deconstruct is armed.
Everything then draws exactly as it did before this document existed. Putting the tool down lowers
the walls again **on the next frame**, with nothing re-meshed.

**The rail icon keeps showing the player's choice** and dims while build mode overrides it, so a
player can see why the walls came back.

## 7. The toggle

- **Placement.** Directly under the Depth rail's cells, in the row the "R / F" hint had. The hint
  was removed after the first play (owner: *"The 'R/F' label is still there — remove it"*); the keys
  are named in each rail cell's tooltip instead.
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
- ~~Is it confusing when a building on higher ground above the slice is hidden?~~ Answered by the
  first play: it should not be, and it no longer is (§3a).
- Standing on a lower terrace, is the terrace above drawn solid with its houses as stumps, and is
  only the storey over your own building gone?

## 10. The first play (2026-09-24)

*"Works brilliantly but a few things."* Two: the "R / F" label was still under the rail, and on a
lower terrace the building above was drawn see-through. Both are answered above — §7 for the label,
§3a for the terrace, with the owner's three choices from the follow-up interview: walls-down alone
decides the terrace question (nothing changes with the walls up), only upper storeys are hidden, and
the keys move into the rail's tooltip.
