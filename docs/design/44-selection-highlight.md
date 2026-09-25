# 44 — The selection highlight: the selected thing is lit at its own edges

**Status:** designed and built 2026-09-25, branch `claude/selection-highlight`. Not yet played.

The owner, 2026-09-25: *"The current cursor is fine and should be left as an option, but the new
default selection cursor … when I click directly on an object/item/colonist it would have some kind
of graphical effect that would allow me to see who is clearly selected on the model itself … this
can apply to tiles also where it highlights the tile … make sure it's performant."*

## 1. What the owner chose (interview, 2026-09-25)

| Question | Answer |
|---|---|
| The look on an object | **A bright line hugging the silhouette, and the object itself faintly lifted** (asked as "about 8 %"; built as ×1.12, §6a) |
| Colour | **White**, the bracket cursor's own `SliceCameraRig.selectionColour` |
| Behind a wall or a roof | **A dimmed line shows through**, so a colonist indoors is never lost |
| A tile (ground, floor, water, a bank) | **A soft wash over its top face and the same line round it** |
| Hover | **Not now.** Selection only; the pipeline takes a hover later at almost no cost |
| A box selection | **The primary bright, the rest dimmer**, as the brackets already do (1.0 / 0.45) |
| Width | **About 2.5 px at 1080p**, scaled with the height of the frame so 4K reads the same |
| Where the choice lives | **Settings → Interface**, *Selection style: Highlight / Brackets*, remembered |

## 2. The mechanism: a mask, a dilation and two overlays

`SelectionHighlightFeature` is a URP renderer feature with two raster passes, after the transparents
and before post-processing:

1. **Mask.** Only the selected thing is drawn again, into an RGBA8 target the size of the camera's
   depth, against the camera's own depth attachment (read, never written). Two passes of
   `Odyssey/SelectionMask` per draw:
   - `Visible`: ZTest LEqual, writes **R** = strength (and **B** = strength × fill for a tile);
   - `Silhouette`: ZTest Always, writes **G** = strength.
   Max blending, so overlapping parts and several selected colonists combine rather than add.
2. **Composite.** `Odyssey/SelectionComposite`, two blits over the camera colour. Neither reads
   the scene, so there is no copy of the frame, and both are **scissored to the selection's own
   screen rectangle**:
   - *Line*: samples the mask on two rings of twelve taps and **alpha-blends** white. Outside the
     silhouette it draws the line, `max(visible ring, hidden ring × 0.4)`; over a tile's top face it
     draws the wash, 0.16.
   - *Lift*: **multiplies** the visible part of the thing by 1.12 (`Blend DstColor One`). §6a says
     why this is not a blend towards white.

**When nothing is selected, nothing is enqueued.** The feature costs zero passes, zero draws and zero
pixels, which is the common case.

### Why this and not the two alternatives

- **The character ink hull, recoloured by a property block.** It exists only on `Odyssey/Character`:
  colonists and corpses. Animals, weapons and tools wear pack shaders, and items and edifices are pack
  materials in instanced buckets. A hull also cracks along the hard edges of Synty's box props.
- **A rim or Fresnel term in the object's own shader.** Every pack shader would need editing, and a
  per-instance flag splits the instanced buckets that the whole renderer exists to keep whole.

The screen-space version gives one look for every kind of thing. The body, hair, beard and sheathed
weapon come out as **one** silhouette. A skinned pose is free, because `DrawRenderer` draws the
renderer's own skinning.

## 3. What is drawn into the mask

The shape comes from the thing as it is drawn, not from a copy of the rule that draws it, so the
highlight cannot drift from the picture:

| Kind | Source |
|---|---|
| Colonist, animal (live figure) | every enabled renderer under the figure's GameObject: skins, hair, beard, headgear, weapon, tool |
| Colonist past the figure cap | captured as `RenderActors` appends it: body module and head pieces at the same matrices |
| Corpse | the baked body's renderers, or the lent figure's while it is still falling |
| Item on the ground, on a shelf, landing | captured as `RenderThings` appends it: every lump of a heap, at the same matrices |
| Wall, door, bed, shelf, machine, ladder, tree, rock | `ChunkMesher.MeshCell`, the chunk mesher run over the one cell into a scratch batch, plus the door leaf where `DoorDirector` drew it |
| Ground, floor, water, a bank | a quad on the surface the bracket cursor already computes (drape, water line, bank shear, skin ramp corners), flagged to fill |

**Anything that produces nothing falls back to the brackets.** That covers a kind with no art, a
stand-in marker, a cell whose only surface is the ground skin, and a pipeline without the feature.
The selection is never invisible.

Alpha-clipped art (leaf cards) is clipped in the mask too. The mask reads the part's own albedo and
cut-off, or a crown would be outlined as a stack of rectangles. Wind is not reproduced: a crown's
outline can lag its sway by a few centimetres.

## 4. Cost

To be measured, with a control, in one run (`SelectionHighlightPlayTests.TheSelectionHighlightAgainstTheFrame`),
and at 4K on the owner's machine with the overlay's `gpu` line (§6).

## 5. Not done, on purpose

- **Hover.** The owner asked for selection only. A hover is the same list with a lower strength.
- **Wind in the mask.** Recorded above.
- **The bracket cursor is not deleted or changed.** It is the *Brackets* rung, and the fallback.

## 6. Measurements

### 6a. The lift has to multiply, not blend towards white (photographed, 2026-09-25)

The first build lifted the thing by alpha-blending white over it, at the 8 % the owner chose. The
same colonist was photographed from the same camera with and without the selection
(`SelectionHighlightShot`, `highlight-colonist-close.png` against `highlight-colonist-control.png`).
The orange jumpsuit came out washed towards cream. Dropping the blend to 3.5 % did not fix it.

The blend happens **in linear light, before the tonemapper**, and a white blend raises a near-zero
channel furthest. Orange's blue channel is about 0.01 linear, so a few per cent of 1.25 is several
times what was there, and after the display curve that reads as desaturation rather than light.

The lift is therefore a second, one-tap pass that multiplies instead (`Blend DstColor One`). At
×1.12 every channel rises in proportion and the jumpsuit stays the orange it is.

**The owner's "faint lift" means ×1.12, not 8 % of white.** Do not return to a white blend to tidy
the composite into one pass.

### 6b. What the photographs showed

All on the played board, at 1600 × 900 with MSAA ×2 (`scripts/unity.sh shot
Odyssey.EditorTools.SelectionHighlightShot.Shoot`, `Logs/highlight-*.png`):

| Subject | Result |
|---|---|
| Colonist, live figure | The line follows the silhouette to the shoes. Skinning is honoured, so the pose outlined is the pose drawn |
| Colonist among four others, low camera | The selected one is found at once |
| Sandstone heap | Every lump is outlined as one heap, at the matrices the heap is drawn at |
| Tree | Crown and trunk come out as one outline. The leaf cards are clipped by their own alpha, so the edge is leafy rather than rectangular |
| Tile | A washed quad with its line. Grass tufts standing in the cell stay unwashed, because they are in front of it |

**Not yet photographed: the dimmed line through a wall.** The probe board has no walls. It is the
owner's first test (playtest queue).

### 6c. Cost

*(pending: `FrameTimeTests`-style arms in `SelectionHighlightPlayTests.TheSelectionHighlightAgainstTheFrame`,
and the owner's `gpu` reading at 4K)*
