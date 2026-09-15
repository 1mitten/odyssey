# tools/icons

The icon pipeline: read the owner's pixel-art sheets, check the mapping, export one PNG per icon
key, and produce labelled contact sheets so the mapping can be corrected against the real art.

Standard library only. No Pillow, no ImageMagick, no Unity. That is deliberate: this repository's
remote container has none of them and its proxy blocks some hosts, so a vendored PNG codec removes
the whole class of "the icon gate cannot run here". The input domain is narrow and ours — 8-bit
non-interlaced PNG — and anything else is refused by name rather than guessed at.

## Commands

```
python3 tools/icons/icons.py validate [--strict]   # registry and mapping consistency
python3 tools/icons/icons.py emit-web              # the mockup's key table; needs no art
python3 tools/icons/icons.py detect  [--sheet ID]  # each sheet's grid and native pixel scale
python3 tools/icons/icons.py contact [--sheet ID] [--keys]   # labelled sheets for review
python3 tools/icons/icons.py export  [--dry-run]   # Assets/Art/Ui/icons/<key>.png
python3 -m unittest discover -s tools/icons -t tools/icons   # 30 tests, no art needed
```

`validate` and `emit-web` work today. `detect`, `contact` and `export` need the eight sheets in
`art-source/icons/sheets/`, which are not in the repository yet; that directory's README says why
and under what names they go in.

## Data it reads

| File | Role |
|---|---|
| `docs/design/icon-keys.csv` | the registry: every icon key the interface needs, 382 of them |
| `docs/design/icon-map.csv` | key to sheet and cell, with a description, a confidence and a status |
| `art-source/icons/sheets.csv` | per sheet: file, grid, trim mode, target size, native scale |

## Rules it enforces

From `docs/adr/0004-pixel-art-icon-pipeline.md`:

- Export at 32, 64 or 128 pixels only, 64 by default. Anything else is refused.
- Nearest-neighbour at an **integer** factor, and never a downscale. Pixel art scaled fractionally
  either shimmers or smears, so the tool refuses rather than producing it.
- The centring offset is rounded down to a multiple of the scale factor, so every source pixel stays
  on an exact block boundary and a later halving lands back on the native grid.
- Nothing is written until every icon has been computed and checked. A failed run leaves the
  exported set untouched.

## Two things worth knowing before you trust the output

**Grid detection needs transparent gutters.** It finds the gaps between cells in the alpha channel,
takes their midpoints as the interior boundaries, and extrapolates the two outer boundaries from the
interior pitch rather than from the outermost ink — otherwise a sheet whose art runs to the image
edge reports a short first cell and looks non-uniform. A sheet with no gutters at all, such as framed
tiles whose borders touch the cell edge, carries **no** grid information in its alpha channel, so it
must declare `cols` and `rows` in `sheets.csv`. Detection then reports that it was skipped rather
than inventing a grid. Where detection and the manifest disagree, the tool fails and prints both
figures: neither is preferred silently, because a disagreement means either the sheet was re-exported
or the manifest is wrong, and both want a human.

**Native scale detection only advises.** It reports the largest factor for which the whole image is
an exact nearest-neighbour upscale, which matters because a sheet may already have been exported at
2× and the integer-scale rule cannot be enforced against the wrong source size. But art made only of
large flat blocks satisfies the test at a higher factor than intended, since a 2×2 run of one colour
is indistinguishable from an upscaled pixel. So `detect` prints the figure for a human to confirm and
`sheets.csv` has a `native_scale` column that overrides it.
