# Interface mockups

Throwaway information-design artefacts. They exist so the interface can be judged before it is
built, and they share no code with the game.

Purpose follows brief §8: layout and information design only. Nothing here is traced from
another game, no Synty content appears in any of them, and every name is ours.

## Files

| File | Supplied | What it shows |
|---|---|---|
| `hud-v1.html` | 2026-09-15 | The complete HUD and every panel from `docs/design/10-ui-panel-catalogue.md`, with a working layer slice, the four icon debug modes, and per-region architecture annotations |

## Opening it

Open `hud-v1.html` in any browser, straight from disk. It is one self-contained file: no build
step, no server, no network. The only external request is a web font, and it falls back
cleanly when that is blocked.

It is designed at 1280 × 720 and scales down to fit the window, so a wide screen is worth
using. Below about 800 pixels the density stops being reviewable.

## What to review, in order

1. **The layer question.** Move the slice on the Depth Ruler, then cycle the **Above** control
   through Hide, Ghost and X-ray. This is the one genuinely open design decision and the
   concept renders could not answer it, because they are all single-layer cut-aways. Brief Lane
   B owns the prior-art half; this is the taste half.
2. **Density at a glance.** Can you read the screen without labels? The **Icons** control has
   five positions. *Icon* is the target. *+ label* is what a fallback looks like if icon-only
   proves unreadable. *Keys*, *Text only* and *Missing* are the three debug modes the game will
   actually ship.
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
- **No icon is real art.** Every one is a deterministic placeholder generated from its symbolic
  key, which is the strategy the game will use until art exists.

## Feedback

Mark up what is wrong and say which region by its catalogue identifier, for example "A11 should
show temperature per layer" or "B2 needs a filter row". The identifiers are stable between the
mockup and `docs/design/10-ui-panel-catalogue.md`, so a note against one lands in the other.
