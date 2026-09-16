# ADR 0007 — Pixel-art icons: author at 64 px, point-filtered, displayed at 32 and 64

- **Status:** accepted on engine source; two numbers still want a measurement (R2, R12)
- **Date:** 2026-09-15
- **Deciders:** owner (the art is theirs and is now the prototype's real look), with the atlas arithmetic below
- **Supersedes:** the 128-pixel authoring rule in `docs/design/09-ui-and-input.md` §7 and the icon-atlas budget row in §4.4
- **Related:** `docs/adr/0003-ui-framework.md`, `docs/design/11-icon-library.md`, `docs/design/09-ui-and-input.md` §§4.3, 4.4, 5, 7, 9 D4

## Context

The owner owns eight pixel-art icon sheets and has decided they are the prototype's real HUD art
rather than stand-ins. Everything in `09` §7 was written on the assumption of smooth vector-ish
icons authored large and scaled down. Pixel art inverts that assumption, and checking the
consequences turned up something worse than a style mismatch.

`09` §7 says icons are authored "at 128 pixels, uncompressed, no mips, so it is atlas-eligible by
construction", and §4.4 budgets "one 2048-pixel page, two as a hard cap". Both statements were
checked against UI Toolkit's dynamic atlas implementation rather than against the documentation.
Four facts, all from the engine's own source:

1. **`DynamicAtlasSettings.defaults` sets `maxSubTextureSize` to 64**, and the eligibility check
   rejects any texture larger than it on either axis. **A 128-pixel icon is not atlas-eligible at
   Unity's defaults.** The phrase "by construction" was exactly wrong.
2. **There are two atlas pages, and they are not a budget.** One is for point-filtered textures and
   one for bilinear. There is no overflow page. When a page fills, the texture is drawn as its own
   binding, costing a draw call in silence.
3. **Each entry allocates a one-texel border and rounds its shelf height to the next power of two**,
   and the page is partitioned into doubling size areas that are packed independently. Real capacity
   is well below a flat grid estimate.
4. **Mip-maps and compression are not eligibility filters.** Block-compressed formats pass. But
   **a readable texture is rejected**, and **under linear colour space a non-sRGB texture is
   rejected** — and neither condition appears anywhere in `09`.

### The arithmetic that decides it

Capacity of one 2048 page, computed against the real allocator rather than a flat grid:

| Authoring size | Flat-grid estimate | Real capacity | 382 keys | Page memory at 382 keys |
|---|---|---|---|---|
| 32 px | 3600 | 3564 | 11% | 2.0 MB |
| 64 px | 961 | 878 | 43% | 8.0 MB |
| 96 px | 441 | ~300 | does not fit | 16.8 MB |
| 128 px | 256 | 188 | **does not fit** | 16.8 MB at 95 keys |

Two results matter. **The registry is 382 keys** (`docs/design/icon-keys.csv`), not the "roughly 250"
that `10` states, and at 128 px fewer than half of them fit one page — the remainder would each cost
a draw call against §4.4's idle budget of eight. And **95 icons at 128 px already force a
2048 × 2048 page at 16.8 MB**, against §4.3's HUD footprint budget of 12 MB including atlases. The
128-pixel contract was unsatisfiable against this project's own budgets even at the vertical slice.
The flat-grid estimate of 256 is what made it look feasible, and it is wrong by 68 icons.

Separately, pixel art makes the display sizes in `09` §7 — 24, 32 and 48 — actively harmful. From a
32-pixel native drawing, 48 and 24 are non-integer ratios: point filtering drops pixels unevenly and
bilinear smears the hard edges that are the whole reason for the style. `hud-v1.html` meanwhile uses
16, 24 and 30 pixel chips, so the two documents already disagreed and neither set was integer-safe.

## Decision

**Author, export and store every interface icon at 64 × 64, RGBA8, point-filtered, uncompressed, no
mip-maps, sRGB on, Read/Write off. Display at 32 and 64 only, and at 128 for a 200 per cent
interface scale. Below 32 pixels do not draw a pixel-art icon at all: use the two-to-four character
text badge that `09` §7 already specifies.**

**Amended 2026-09-16 (owner).** The text badge is a rendering rule for a space too small to hold an
icon, not a way of naming anything. Until real icon art is in the build, every icon-bearing control
draws its **full name** beside the icon per `09` §7a, so the badge appears only inside a generated
placeholder tile and below 32 pixels — and never as the only thing identifying a control. The
pixel arithmetic in this ADR is unaffected; the labels are text and do not enter the icon atlas.

Consequences that follow and are therefore also decided:

- **Every texture under `Assets/Art/Ui/` is point-filtered, including the generated placeholders.**
  Filter mode selects the atlas page, so one bilinear texture in the set allocates the second page
  and spends the whole "two pages" budget by itself.
- **Interface scale quantises icons.** `09` §9 D4's 80-to-150 per cent slider scales text and padding
  continuously but steps icons through 32, 64 and 128. Pixel art buys crispness and pays in a
  stepped scale.
- **Mod-supplied icon overrides get the same rules at load time**, with oversized or wrongly filtered
  art logged and down-ranked, because one modder's 256-pixel set would otherwise cost a draw call per
  icon.
- **Source sheets live outside `Assets/`**, in `art-source/icons/sheets/`, so the rule "every texture
  under `Assets/Art/Ui/` is at most 64 pixels" needs no exception. A carve-out in an absolute rule is
  how the rule dies.

## Rationale

1. **64 is the largest size that is eligible without touching engine defaults.** It equals
   `maxSubTextureSize`, so the panel settings need no change and a future default cannot break us.
2. **64 fits the allocator's shelf exactly**, being a power of two. 96 would waste a whole shelf tier.
3. **64 never downscales the source.** It accommodates 32-pixel native art at 2×, and 48- or
   64-pixel native art at 1× with transparent padding. The sheets are only probably 32-pixel native,
   and eight sheets may not agree with each other; a 32-pixel target would force either a refusal or
   destruction of the art.
4. **Integer ratios are lossless in both directions, so the stored size has no visual consequence.**
   If the 8 MB page bites during the M1 performance pass, re-export at 32 and the HUD looks
   identical while the page drops to 2.0 MB. The decision is therefore cheap to revisit, which is why
   the target is a per-sheet column in the manifest rather than a constant in code.
5. **Removing the non-integer display sizes removes the need for a second authored size**, which
   `09` §7 offered as the fix for a rough 24-pixel case. There is no rough case left to fix.

## Alternatives considered

| Option | Verdict |
|---|---|
| Keep 128 px and raise `maxSubTextureSize` | Rejected. Fails both the page-capacity and the memory budget, and keeps the non-integer display scales |
| Author two or three sizes | Rejected. Two to three times the files and the atlas footprint, and it adds a size dimension to a resolution chain deliberately kept one-dimensional. It exists only to serve a 48-pixel token that has no reason to be 48 |
| Accept bilinear filtering at small sizes | Rejected. It destroys the quality the art was chosen for, and mixing filter modes allocates both atlas pages |
| Author at 32 px | Rejected, narrowly. Four times cheaper in atlas memory, but it would downscale any sheet whose native cell is larger than 32, and it leaves no margin if an element ever samples non-ideally |

## Consequences for other documents

`09` §7 loses the 128-pixel rule and the second-size advice, gains point filtering, sRGB and
non-readable as real eligibility conditions, and gains the distinction that no-mips and
uncompressed are kept for fidelity and memory rather than eligibility — a rule kept for the wrong
reason is a rule the next reader relaxes. §4.4's budget row is restated in entries rather than
pages, and says plainly that overflow is a draw call per icon rather than a second page. §4.3 breaks
the atlas out as its own line. §9 D4 gains the quantisation rule. §5 gains an `Odyssey.Tests.Editor`
assembly, because `AllIconTextures_MeetAtlasRules` must read importer settings and no existing test
assembly may reference `UnityEditor`.

## Flip conditions

- **F1.** R12 measures the eligibility rules and they differ from the engine source read here. The
  numbers move; the integer-scale reasoning does not.
- **F2.** The 8 MB page proves too much during the M1 performance pass. Re-export at 32 px; no
  visual change, and the doc already carries the decision.
- **F3.** The art turns out not to be 32-pixel native, or the sheets disagree with each other. 64 px
  absorbs 32, 48 and 64 without downscaling, so only a native cell above 64 would force a rethink.

## Status of the evidence

The capacity figures and the four engine facts come from reading UI Toolkit's dynamic atlas
implementation, not from documentation and not from a running editor. No Unity exists in the remote
container, so nothing here has been executed. R12 (eligibility rules) and R2 (atlas page count at
about 200 icons) in `docs/research/g-02-unity-ui-framework.md` remain the measurements that confirm
it, and R2's own description already used 64 px while `09` said 128 — that disagreement is now
resolved in favour of 64.
