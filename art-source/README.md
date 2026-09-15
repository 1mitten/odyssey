# art-source

Owner-owned source art, deliberately **outside `Assets/`**.

Unity imports everything under `Assets/`, and these sheets are six hundred cells each. Keeping them
here means no oversized texture is imported, cached or accidentally referenced, and it means the rule
that every texture under `Assets/Art/Ui/` is at most 64 pixels needs no exception. A carve-out in an
absolute rule is how the rule dies. See `docs/adr/0004-pixel-art-icon-pipeline.md`.

Everything here is committed. This is not the Synty boundary: Synty content is licensed and lives
only in the gitignored `Assets/Synty/`. These icons are the owner's to commit.

## icons/

| Path | What |
|---|---|
| `sheets/` | The eight pixel-art icon sheets. **Not yet present**; see `sheets/README.md` |
| `sheets.csv` | Per sheet: file, grid, cell size, trim mode, target size, native scale. Grid columns are blank until `icons.py detect` fills them |
| `contact/` | Generated labelled contact sheets for review. Gitignored; regenerate with `icons.py contact` |
| `export-lock.json` | Written by `icons.py export`: what was exported, at what size and scale |

The pipeline is `tools/icons/icons.py`. Standard library only, so it runs in the remote container and
in CI. `tools/icons/README.md` has the commands.
