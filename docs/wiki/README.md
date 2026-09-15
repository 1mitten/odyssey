# docs/wiki

The content wiki: every named thing in the game, so the names can be reviewed and corrected.

**Generated. Never edit these files by hand.** They are rebuilt from the design data by
`tools/wiki/build_wiki.py`, and a hand edit is silently overwritten on the next build.

| File | What it is |
|---|---|
| `index.html` | The whole wiki as one searchable page. A complete HTML document: open it from disk, or host it |
| `artifact.html` | The same page without the document wrapper, for publishing as a Claude Artifact |
| `index.md`, and one `.md` per section | The same content as Markdown, which GitHub renders directly. Easier to quote and correct in a pull request |

## Correcting a name

1. Find the row. Every entry carries a stable key (`ui.res.girder`) or id (`faction.raiders`).
2. Edit the source: names and descriptions in `docs/design/icon-keys.csv`, people and places in
   `docs/design/proper-nouns.csv`. Art assignments are in `docs/design/icon-map.csv`.
3. Rebuild: `python3 tools/wiki/build_wiki.py`.
4. Commit the CSV and the regenerated wiki together.

## Keeping it current

`python3 tools/wiki/build_wiki.py --check` rebuilds in memory and exits non-zero if what is on disk
differs. That makes staleness a failure rather than a habit, which is the point: a content reference
nobody trusts is worse than none. `CLAUDE.md` requires the check to pass in any commit that touches
game content.

## Hosting

`index.html` is self-contained apart from two Google Fonts stylesheets, and falls back to system
faces if those are blocked. Three ways to put it somewhere:

- **GitHub Pages**, the least work. Repository settings, Pages, deploy from a branch, folder
  `/docs`. The wiki lands at `https://<owner>.github.io/odyssey/wiki/`.
- **Any static host.** Copy `docs/wiki/` anywhere. There is no build step and no server code.
- **A Claude Artifact.** Publish `artifact.html` for a private link that needs no hosting at all.
  Already done: **<https://claude.ai/artifact/JsYRQk1vnza2wFWSNfpQwr>**. It is a snapshot, so
  republish to that same URL after a rebuild. The HUD mockup is published the same way, at
  <https://claude.ai/artifact/PcHWoujGDvd4rzcH1LAwFG>.
