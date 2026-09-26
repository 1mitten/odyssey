# e-14 — Procedural wing patterns, and colouring the night glow

**Question.** How can a shader generate varied, believable low-poly butterfly wing patterns from a
per-instance seed, and how should the night glow be coloured to stand out against this game's night
grade? Context: flat-shaded Synty-style art, butterflies of four wing panels, every one different,
no authored textures; at night the same butterflies glow in full-spectrum hues weighted to cyan,
violet, magenta and amber. Read against `Daylight.cs` and `PC_RPAsset.asset`. Cap: 7 searches and
6 reads (subagent, 2026-09-25).

## Findings

1. **The biology is a small set of switches.** Nijhout's nymphalid ground plan runs from the wing
   base to the edge: a basal symmetry system, a central symmetry system (a pair of bands with the
   discal spot between), a border symmetry system (eyespots between parafocal elements) and a
   marginal band. Each element repeats once per **wing cell** — the space between two veins — and
   each cell develops largely on its own, with eyespot foci on the cell's midline. Variety comes
   from switching elements on or off, fusing them and moving them. The mechanism is a gradient then
   a threshold, so a `step()` on a distance in the shader is biologically honest rather than a
   compromise. An eyespot is a pale focus, a coloured disc and a dark outer ring.
2. **A species table** (hex values approximate, from general knowledge):

   | Species | Ground | Dark | Accent | Elements |
   |---|---|---|---|---|
   | Monarch | `#E8751A` | `#1A1A1A` | `#FFFFFF` marginal spots | thick veins, marginal band, no eyespots |
   | Blue morpho | `#2F7CFF` | `#111111` | `#FFFFFF` | wide dark margin, white spots at the tip |
   | Cabbage white | `#F4F1E4` | `#3A3A3A` | — | dark tips, one or two discal spots |
   | Swallowtail | `#F5D531` | `#151515` | `#3A7BD5` lunules, `#E0452B` eyespot | bands, veins, one eyespot at the inner corner |
   | Red admiral | `#1C1A1A` | `#1C1A1A` | `#D8431E` band, `#FFFFFF` | central band, white spots at the tip |
   | Peacock | `#B4262E` | `#1A1A1A` | `#2C5FD6` / `#F2D24B` eyespot | one large eyespot per wing |

3. **The maths, in wing-local polar coordinates** from the wing root: `r` is the distance from the
   root over the outline's radius at that angle, `θ` the angle across the wing.
   - Cells and veins: `cell = floor(θ·N)`, vein where `|fract(θ·N) − ½| > ½ − w`, N from 5 to 7.
   - Eyespot: a circle distance field on the cell midline at `r ≈ 0.72`, rings by `step(d, t₁…t₃)`.
   - Central band: `|r − c + a·sin(θ·k)| < w`. Marginal band: `r > 1 − m`.
   - Variety: `hash(seed, cell)` decides each cell's eyespot and its size. Edges stay hard,
     anti-aliased over about a pixel with `fwidth`.
   `butterfly-gen` (Unity) and the Stanford procedural butterfly both work in polar coordinates;
   both bake to textures and neither is a stateless shader.
4. **Real geometry, not an alpha-clipped quad.** MSAA is off (`PC_RPAsset.asset` `m_MSAA: 1`), and
   alpha-to-coverage needs MSAA, so a clipped quad would shimmer as a stair-stepped edge; `discard`
   also defeats early depth. A faceted outline of 6–10 triangles a wing *is* the Synty look, costs
   nothing, and its corners can be nudged per seed in the vertex stage.
5. **The night palette in `Daylight.cs`** — moon key (0.62, 0.72, 1.00), sky ambient (0.13, 0.17,
   0.30), zenith (0.03, 0.05, 0.13) — sits at a hue of about 220–225°. **Amber (~35°) is its
   complement** and contrasts most; magenta (~310°) is 85° away and reads well; cyan (~185°) and
   violet (~270°) are only 40–45° away, and violet is also dark. **Cyan and violet must earn their
   contrast through brightness, not hue.**
6. **Colour blindness.** For deuteranopes and protanopes cyan near 498 nm is the neutral point and
   looks whitish grey; violet and magenta both collapse to blue — the night's own colour. Amber
   stays yellow. For those players amber is the only glow that differs from the night by hue; the
   rest must differ by brightness.
7. **Pulse.** A firefly flash lasts 20–40 ms and reads as a signal — blinking. A breathing indicator
   at 12 cycles a minute (0.2 Hz) on a smooth curve reads as calm and alive.

## Recommendation

- Four low-poly wing panels, no alpha clip; the seed jitters the outline by up to about 8%; the
  shader lays out wing-polar coordinates and draws with `step()`.
- The species from `hash(seed)`, then five hashed parameters: vein count (5–7), the central band and
  its place, which cells carry eyespots, the eyespot size (0.08–0.16), the margin's width
  (0.08–0.2). Jitter the hue ±6° within a species.
- **Glow in two tiers**: the whole wing faintly, below the bloom threshold, so the silhouette reads;
  the pattern elements brighter; the veins not at all, so the wing reads as stained glass. (See
  design 52 §5 for why the bright tier is kept at or under 1.0 and the spectacle moved to a drawn
  halo — `d-24` §7.)
- Hue weights cyan 0.3, magenta 0.25, amber 0.25, violet 0.2; violet pushed to about 285° and about
  1.5 times as bright; the hue drifting ±25° over 30–60 s with a random phase.
- Pulse at 0.15–0.25 Hz, a fast rise and a slow fall, **never below 55%** — a glow that goes dark
  reads as blinking.
- **Never let two butterflies differ only by hue.** Amber carries the difference for red–green
  deficient players and brightness the rest. Check with the dichromacy simulation
  `StorageThemeTests` already uses.

## Sources

- https://pmc.ncbi.nlm.nih.gov/articles/PMC7825419/
- https://www.frontiersin.org/journals/ecology-and-evolution/articles/10.3389/fevo.2020.00146/full
- https://royalsocietypublishing.org/doi/10.1098/rspb.1990.0009
- https://github.com/tantaneity/butterfly-gen
- https://graphics.stanford.edu/courses/cs348b-competition/cs348b-01/procedural_butterflies/
- https://docs.unity3d.com/6000.0/Documentation/Manual/writing-shader-alpha-to-mask.html
- https://www.patreon.com/posts/alpha-to-or-anti-33042721
- https://www.britannica.com/science/bioluminescence
- https://www.ncbi.nlm.nih.gov/pmc/articles/PMC9918520/
- https://medical-dictionary.thefreedictionary.com/deuteranopia
- https://journals.plos.org/plosone/article?id=10.1371%2Fjournal.pone.0107035
- https://grafik.agency/insight/apple-sleep-function/
- https://patents.google.com/patent/US6658577B2/en
- Repo: `Assets/Odyssey/Presentation/Rendering/Daylight.cs` (165–173), `Assets/Settings/PC_RPAsset.asset` (28)

## Confidence

1. **High.** 2. **Medium** (palettes from general knowledge, hex values approximate). 3. **Medium**
(the formulas are constructed on the sourced model). 4. **High** that MSAA is off; **medium** that
geometry reads more Synty-like. 5. **High** for the colours, **medium** for the hue-distance
reasoning. 6. **Medium-high.** 7. **High** for the rates, **medium** that 0.2 Hz reads as alive on a
butterfly.

## Could not be determined

- How many pixels a butterfly covers at the play camera, which decides whether the pattern tier is
  visible at all or only the halo is ever seen (answered by measurement in design 52).
- Whether URP's bloom threshold compares the brightest channel or luminance.
- Whether the post anti-aliasing pass matters (only if alpha clip were chosen).
- No stateless procedural-butterfly shader was found to compare against.
