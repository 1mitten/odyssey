# World screen: Claude Design's specification (mockups 26a / 26b)

**Received 2026-09-26.** The owner pasted this into the session in answer to
`world-map-brief.md` (*"happy to go - here is the claude code design prompt"*). It is kept
verbatim below the rule, as the home-area specification was.

**Where it disagreed with design 59, the owner ruled (2026-09-26):**
- **Biomes: the spec's six.** Ocean, Meadow, Cold steppe, Dry scrub, Marsh and Ice, with its ramps.
- **Hill names: ours, not the spec's.** The spec's *Flat, Small hills, Large hills, Mountainous,
  Impassable* are the reference's own labels, which the clean-room rule forbids. They are built as
  **Flat, Rolling, Hilly, Mountainous, Sheer**, and the spec's marks map across one to one: Large
  hills is Hilly, and Impassable is Sheer.
- **Region names, zoom and pan: built as specified**, reversing design 59 §10's "no place names".

What was built, and every departure from the text below, is in design 59 §9a–§9c.

---

# Task: World screen restyle (WG3 visual target: mockup 26a / 26b)

Build the World screen's look, which sits between New game and the setup page. The map is **one
`Texture2D` painted per tile when the seed changes**, per P10: the whole map is one draw.
Everything below is presentation. The data comes from `PlanetGenerator` and `WorldChoice`.

## Frame (same as the New game page)

- Inset 24 from every edge. `Panel` at 96%, 1 px `Border`, 5 px radius, padding 32 top and bottom,
  40 left and right, flex column.
- **Title row:** "World" at 19/600. After a 12 gap, the subtitle at 12/400 `Meta`: "Pick a site to
  drop your colony" (`ui.world.subtitle`).
- **Top row,** 24 below, bottom-aligned, with 24 between groups:
  - **SEED:** a label at 11/600 .14em `Dim`, 9 below it a 200 x 36 field (the value in mono
    14/500), then 9 later **Reroll**: 36 high, 14 side padding, a drawn reset icon at 14 and an
    8 gap before the label.
  - **Random site:** a 36-high button with a drawn hex-die icon at 14.
  - A flex spacer, then the hint "The map wraps east to west" at 12/400 `Dim`.
- **Body,** 24 below the top row: `grid-template-columns: minmax(0,1fr) 340px`, 24 gap. The map
  column is a flex column with a 12 gap: the map box, then the legend.
- **Footer,** 24 below the body:
  - **Back:** 36 high, at least 160 wide, `Control border`, with a drawn left chevron.
  - **Next:** 36 high, at least 160 wide, 1 px `Accent` border, `Accent` at 12% fill, `Accent`
    ink at 600, with a drawn right chevron. **Next isn't green**, because Start stays the only
    green button.
  - **Next disabled** (a site that can't be settled): 13% border, `Dim` ink, 60% opacity.

## Map texture

**Grid:** 64 x 32, pointy-top hexes in offset rows, odd rows shifted half a hex. It wraps east to
west.

**Geometry at 1x:**
- Hex width is 18.4.
- Hex height is 18.4 x 2 / sqrt 3, about 21.2.
- Row step is 0.75 of the hex height.
- The texture is about 1187 x 515.
- Scale it into the map box with `object-fit: contain`, keeping that aspect ratio.
- Draw each hex 0.4 px oversize so no seams show.

**Map box:** 1 px `Border`, `#081521` behind the texture, clipped.

**Tile colour: a two-stop ramp per biome, not a flat fill.** Store the ramps as `BiomeDef` map
colours (`colourLow`, `colourHigh`):

```
Ocean      low #0b1d2c  high #2f6f8c    t = (elevation / seaLevel)^2.2   (deep dark, coast light)
Meadow     high #a3cf72 low #4f8f45     t = landHeight x 1.1 + jitter    (lowland light, upland dark)
Cold steppe high #cdc79c low #8c8766    same rule as Meadow
Dry scrub  high #e0b577 low #a8743f     same rule as Meadow
Marsh      high #5c9c8c low #2c6660     same rule as Meadow
Ice        low #b9c9d2  high #f4f8fa    t = 0.6 + (elevation / seaLevel) x 0.4 on sea ice; land rule on land
```

- `landHeight = (e - sea) / (1 - sea)`.
- Jitter is `(hash(col,row,7) - 0.5) x 0.12`, deterministic from the tile.
- Clamp t to 0..1 and lerp between the two stops.

**Hill marks,** drawn into the texture at the tile centre. They are the cue that doesn't rely on
colour. Skip them on Ice.

```
Large hills   a bump:  quadratic curve from (-4,+2) through a control point 5 up to (+4,+2)
              stroke rgba(12,16,20,.40), width 1.5
Mountainous   a peak:  (-4.5,+3) up to (0,-3) down to (+4.5,+3), stroke rgba(12,16,20,.55), width 1.6
Impassable    a tall peak: the same with an 8 px rise
```

Flat and Small hills get no mark.

**Finish overlays,** composited once into the same texture:
- **Polar haze:** a vertical ramp, `#e8f2f7` at 28% at the top and bottom edges, fading to 0% by
  14% and from 86%.
- **Sheen:** a diagonal ramp from white at 10% (top-left) through 0% at the middle to black at 18%
  (bottom-right).
- **Vignette:** radial, centred, radius 75%. Black at 0% out to 55%, then 55% at the edge.

These are flat ramps baked into the texture once. They aren't shader effects, and the HUD panels
stay flat.

## Selection and hover (a separate overlay layer, not in the texture)

```
selected  hex outline at +1.5 radius: first 4 px #0b1116 (the dark underline), then 2.2 px Accent on top;
          plus a dashed Accent ring at +7 radius, 1.5 px, dash 3 3
hover     hex outline at -0.5 radius, 1.5 px Text at 80%
```

These are drawn with `SvgPath`, so the selection reads on every biome, including Ice.

## Region names (new)

Place names are generated from the world seed with no registry entry per name. They are
procedural proper nouns, so add the generator's syllables to `proper-nouns.csv` as a pattern
note.

1. **Find the regions.** Take connected components by `land`, using six neighbours and wrapping
   east to west.
2. **Land labels:** the 7 largest land components of 14 tiles or more. Place each label at the
   component's centroid, using a circular mean for x so the wrap works.
3. **Sea labels:** up to 5, in the largest ocean component. Place them only on tiles whose six
   neighbours are all ocean, and not in the top or bottom 4 rows. Keep them at least 230 px apart
   (at 1x).
4. **Names:** a seeded PRNG builds each name from syllable tables:

   ```
   S1  Ver Cal Mor Ess Tal Ond Bra Hes Kel Iv Sar Dun Ul Quen Ash
   S2  an ere oth ia ul ane ess ir orra en ys
   S3  (blank) (blank) d n th ry ck

   word = S1 + S2 + S3
   land = one of: word, "word Reach", "The word Downs", "word Hold", "wordia", "Greater word"
   sea  = one of: "Sea of word", "word Deep", "The word Shelf", "word Sound", "Gulf of word"
   ```

   The frame words (Reach, Downs, Hold, Greater, Sea of, Deep, Shelf, Sound, Gulf of) are
   registry labels (`ui.world.region.*`), so they can be translated.

**Label style** (a HUD label layer over the map, centred on its point, no wrap, never hit-tested):

```
land   Archivo Narrow 600, 12 px, UPPERCASE, tracked .22em, ink rgba(12,16,20,.78),
       halo 0 0 3px white at 35% (a 1 px text outline if UI Toolkit lacks shadow)
sea    Archivo Narrow 500 italic, 14 px, tracked .06em, ink rgba(190,225,240,.85),
       shadow 0 1px 2px black at 60%
```

- **Labels counter-scale with zoom** (scale 1/z), so they stay at their pixel size and don't grow.
- The sea ink and the dark land ink are fixed map colours. Put them in `HudTheme` as `MapLandInk`
  and `MapSeaInk`.
- The fonts ship italic-free. If Archivo Narrow Italic isn't available, draw the sea labels
  upright and keep the tracking and colour. Record that in `41-the-draw.md`-style notes for
  design 59.

## Zoom and pan

**Zoom:** from 1x to 4x, in steps of x1.5.
- At 1x the map is centred and the pan offset is forced to 0.
- Ease each transform change over 180 ms, out.
- Clamp panning so the map can't be dragged fully off-screen. Wrap on x when zoomed in, as the
  planet wraps.

**Zoom control:** top-right of the map box, inset 12. It is a vertical stack with `Panel` fill and
a 1 px `Border`:

```
+       32 x 32, a drawn plus at 14, 2.6 stroke
1.5x    22 high, 11 mono Meta   (the current zoom)
-       32 x 32, a drawn minus
fit     32 x 32, drawn corner brackets at 14 (resets to 1x and centre)
```

- Rows are divided by `Rule`. The hover fill is white at 6%.

**Pointer:**
- **Drag** pans, only when zoomed in. The cursor is `grab` when zoomed in.
- **Double-click** zooms in x1.5, around the pointer.
- **Wheel** zooms around the pointer.
- **Click** selects a tile: hex picking from a pixel is pure arithmetic, tested including the
  wrap and the zoom and pan.

**Hint chip:** bottom-left, inset 12. `Panel` at 85%, 1 px `Border`, 6/9 padding, 12/400 `Meta`:
"Drag to pan, double-click to zoom" (`ui.world.zoomhint`).

**Keys:** `+`/`-` zoom, `0` fits, and the arrow keys pan. Escape backs out one level, per the
`SettingsDirector` rung.

## Legend (under the map, flex, wrap, 18 gap)

- Six biome entries: a 16 x 16 swatch in the ramp's **mid** colour with a 1 px `Border`, the name
  at 14/500, then 8 later the state at 12/400:
  - "Settleable" in `Good`, for Meadow only;
  - "Not yet available" in `Dim`, for the rest.
- Then a 1 x 18 `Border` divider.
- Then the three hill marks at 20 px in `Meta`, each with its name at 12/400: Large hills,
  Mountainous, Impassable.

## Stats panel (340 wide, 1 px Border, flex column)

**Header:** 16 padding and a bottom `Border`.
- A 44 x 44 swatch of the tile's actual colour.
- 12 later, "SELECTED SITE" at 11/600 `Dim`, then 7 below it the biome name at 19/600.

**Status,** 12 below the header, with a 16 margin:

```
settleable   30 high, 1 px Good at 60%, Good at 14% fill, drawn tick 14, "Settleable" 14/500 Good
not yet      1 px Warn at 50%, Warn at 10% fill, 8/10 padding: drawn info-circle 14 plus
             "Not yet available" 14/500 Warn, then the reason at 12/400 Meta 1.35
             ("Only Meadow sites can be settled for now. Other biomes arrive with a later art pack."
              from CanSettle's reason key)
```

**Stat rows:** 12/16 padding. Rows are 30 high with a bottom `Rule`, the label at 13/400 `Meta`
on the left and the value on the right at 14/500 (mono when it contains a figure):

```
Biome  ·  Hills  ·  Mean temperature "14 C"  ·  Seasons "2 to 26 C"  ·  Rainfall "940 mm"
Latitude "32 N"  ·  Tile "30, 11"
```

- Temperatures go through `TemperatureLabels`.
- When the site is mountainous, add "Board depth 24 layers".

**Foot:** pinned with `margin-top: auto`, 12/16 padding, a top `Rule`, 12/400 `Dim`: "Click a tile
to inspect it. Random site picks a temperate Meadow."

## Constants (HudLayout, with tests)

```
WORLD_INSET 24   WORLD_PAD 32/40   WORLD_STATS_W 340   WORLD_BODY_GAP 24
HEX_W 18.4   HEX_ROW_STEP 0.75   HEX_OVERSIZE 0.4   MAP_W 64   MAP_H 32
SEL_RING 2.2 over 4   SEL_DASH_OFFSET 7   HOVER_RING 1.5
ZOOM_MIN 1  ZOOM_MAX 4  ZOOM_STEP 1.5  ZOOM_EASE 180ms  ZOOM_BTN 32  ZOOM_READOUT_H 22
LABELS_LAND_MAX 7  LABELS_LAND_MIN_TILES 14  LABELS_SEA_MAX 5  LABELS_SEA_SPACING 230
LEGEND_SWATCH 16  LEGEND_HILL 20  STATS_SWATCH 44  STATS_ROW_H 30
```

## Acceptance

- The map is one texture that is repainted only when the seed changes. Tiles show per-biome
  gradients, hill marks, polar haze, sheen and vignette.
- Selection and hover read on every biome. A site that can't be settled shows the warn status
  with its reason, and Next is disabled.
- Zoom runs from 1x to 4x by button, wheel and double-click. It pans by drag and arrow keys, wraps
  east to west, and the fit button resets it.
- Region names are deterministic per seed, stay in place and at their pixel size while zooming,
  and never cover the zoom control.
- Tests: hex picking (wrap and zoom), label placement deterministic, `WorldChoiceTests`, and a
  PlayMode run through New game, World, Next, setup and Start.
- Every string is ASCII, and every colour is a token or a named `HudTheme` map colour.
