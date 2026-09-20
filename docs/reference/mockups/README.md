# Interface mockups

Throwaway information-design artefacts. They exist so the interface can be judged before it is
built, and they share no code with the game.

Purpose follows brief §8: layout and information design only. Nothing here is traced from
another game, no Synty content appears in any of them, and every name is ours.

## Files

| File | Supplied | Status | What it shows |
|---|---|---|---|
| `almanac.html` | 2026-09-20 | **current** | The Almanac (F9) in-game wiki reference browser: 3-column full-bleed reference system (250px category rail, 290px index, unified 4-band entry template with 520px properties column), 360px search with live count, lateral links, and unique SVG glyphs for every game entry |
| `work-v1.html` | 2026-09-20 | **current** | The Work tab, B2. The owner's supplied mockup re-pointed at our tokens: all twenty-two `ui.work.*` columns with the simulation's four live and eighteen drawn as not-built, rotated labels at the angle the pitch actually demands, the five-step skill border, passion flames, and a working Simple / Detailed switch. Decisions in `docs/design/27-work-tab.md` |
| `hud-v2.html` | 2026-09-15 | **current** | The layer HUD. All six above-slice visibility modes from ADR 0006 plus the depth cap and the below-slice treatment, six named presets, live tuning sliders, and every icon slot driven by the real icon mapping with a coverage read-out |
| `hud-v1.html` | 2026-09-15 | historical | The complete HUD and every panel from `docs/design/10-ui-panel-catalogue.md`, with a working layer slice, the four icon debug modes, and per-region architecture annotations |
| `icon-map.js` | generated | — | Written by `tools/icons/icons.py emit-web` from `docs/design/icon-map.csv`. Do not edit |

**v1 is kept unchanged on purpose.** It is the artefact that produced the x-ray decision: it
offered hide, ghost and x-ray as a live toggle, and the owner chose by looking. Rewriting it to
match the outcome would erase the evidence. So it still opens on ghost and still calls the question
open, and that is correct for what it is. Everything new is in v2.

## Opening them

Open either file in any browser, straight from disk. No build step, no server, no network.
v2 reads `icon-map.js` from the same directory and addresses the icon sheets in
`art-source/icons/sheets/` as CSS sprites; while those sheets are absent every slot falls back to a
deterministic placeholder, so the mockup is readable now and shows the real art the moment they
land, with no change to the file.

It is designed at 1280 × 720 and scales down to fit the window, so a wide screen is worth
using. Below about 800 pixels the density stops being reviewable.

## What to review, in order

1. **The layer question, now settled.** Move the slice on the Depth Ruler, then cycle the
   **Above** control through Hide, Ghost and X-ray. This mockup is how the decision was made:
   the owner picked **x-ray** by eye, and `docs/adr/0006-layer-visibility-policy.md` records it
   together with the three further modes and two axes the decision added. Brief Lane B still owns
   the prior-art half and now challenges a default rather than choosing one.
2. **Density at a glance.** The **Icons** control has five positions, and **+ label is now the
   default** — the owner's decision of 2026-09-16 (`09` §7a) is that while the icons are
   placeholders a control is named by its full word, never by an abbreviation. *Icon* is what the
   screen becomes again once real art is in the build; the question to review in that mode is
   whether density survives the labels going away. *Keys*, *Text only* and *Missing* are the three
   debug modes the game will actually ship.
3. **Whether the region set is right.** Turn on **Annotate regions**. Every region shows its
   catalogue identifier, the director that owns it, the view fields it reads and its update
   cadence. Anything missing from the screen is missing from the design.
4. **Panel density.** Open every panel and judge whether the floating-window rule holds when
   several are up at once. The **Open every panel** button does this in one click.

## Controls

| Action | Control |
|---|---|
| Change layer | Click the Depth Ruler, or `[` and `]`, or `Page Up` / `Page Down`, or `Shift` with the scroll wheel |
| Jump to ground | `Home` |
| Cancel | `Esc`, or the red control bottom-right |
| Select a colonist | Click a roster card. It also jumps the slice to their layer |
| Jump to an event | Click any alert or bulletin. It jumps the slice to where it happened |
| Move a panel | Drag its title bar |

## What is deliberately not real

- **The world is a placeholder renderer.** A seeded street grid with building shells and two
  levels of tunnel, drawn isometrically so the slice and the above-and-below policies can be
  judged by eye. It is not the game's renderer and implies nothing about it.
- **The work grid is 12 × 14.** The real one is 50 × 25 and virtualises to under forty realised
  rows. The mockup draws every cell, which is exactly what the real one must not do.
- **Panel positions do not persist.** The real ones remember where you left them, per type.
- **Every number is invented.** The budget monitor's figures are the targets from
  `docs/design/09-ui-and-input.md` §4, not measurements. Nothing has been profiled.
- **No icon is real art yet.** Each slot is a deterministic placeholder generated from its
  symbolic key, which is the strategy the game will use until art exists. In v2 a slot outlined in
  **magenta** means something stronger: that key has **no art anywhere in the owner's eight sheets**
  and is on the list in `docs/design/11-icon-library.md`. The roster bar is entirely magenta,
  because no sheet contains a human figure.
- **v2's tuning sliders change nothing but v2.** They exist so the x-ray falloff can be chosen by
  eye rather than argued about in prose. Copy the numbers you settle on into
  `docs/design/09-ui-and-input.md`; nothing reads them automatically.

## Feedback

Mark up what is wrong and say which region by its catalogue identifier, for example "A11 should
show temperature per layer" or "B2 needs a filter row". The identifiers are stable between the
mockup and `docs/design/10-ui-panel-catalogue.md`, so a note against one lands in the other.
