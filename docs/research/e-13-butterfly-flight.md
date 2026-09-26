# e-13 — Butterfly flight and wing motion for cheap ambient instances

**Question.** How do games draw convincing procedural butterfly flight and wing motion for many
cheap ambient instances, and what real kinematics make the motion read as a butterfly rather than
a moth, a bird or a bee? Context: a camera pitched about 48° down, 10–160 m from its focus (48 m by
default), stylised low-poly art, 150–300 butterflies on screen that flutter, land and rest, and
scatter from a walker. Cap: 8 searches and 6 page reads (subagent, 2026-09-25).

## Findings

**Kinematics**

1. **Wingbeat about 10 Hz.** Butterflies beat at roughly 10 Hz (13–16 Hz measured in one
   free-flight study; a cruising mean of 11 Hz across Neotropical species in Dudley), far below a
   hawkmoth (26 Hz) or a bumblebee (152 Hz). A butterfly's strokes are individually visible; a
   bee's blur. That is the main thing that separates the two on screen.
2. **Stroke amplitude about 100–120°** (Dudley's cruising mean 103°; another study 120 ± 10°),
   much wider than a bird's.
3. **Cruise a little over 1 m/s.** Escape flight trades speed for erratic heading. Palatable
   species fly more erratically than distasteful ones.
4. **The body bobs once per beat, rising on the downstroke** — in counter-phase with the wings —
   strongest in species with low wing loading and a low beat frequency. No amplitude in
   centimetres was found.
5. **Flap and glide alternate.** A glide is wings held still and sinking, never flapping.
6. **The stroke is asymmetric.** In the reference procedural implementation the downstroke is 42%
   of the cycle, the hindwing lags the forewing slightly, and rate and amplitude vary a little
   beat to beat.
7. **At rest the wings are closed upright**, which lets the butterfly leave in one stroke. Basking
   is either dorsal (spread flat to the sun) or lateral (closed, tilted side-on). Moths rest flat
   or tented, so **closed upright reads as butterfly**.
8. **Startle distance** depends on species and on how far away the approach starts. Observers
   were spotted from over 10 m and kept about 3 m back; some swallowtails did not flee even when
   touched.

**Techniques**

9. **The flap in the vertex shader.** Alisavakis cuts one quad twice and lifts the vertices with a
   mask that is zero on the body and largest at the tips, using a per-instance value to shift the
   frequency so neighbours do not flap together. `butterfly-gen` instead hinges each wing as its
   own mesh, so nothing is rebuilt.
10. **Paths.** `butterfly-gen` steers along a drifting curl-noise field with a soft pull home,
    banks into turns, bobs on each downstroke, and now and then stops flapping to glide and sink.
    It runs six butterflies, so it is not a crowd solution.
11. **Ghost of Tsushima** spawns its creatures from particles, and they sense characters through a
    low-value wind sphere around each one and scatter by a conditional event.

**Legibility at the play camera** (arithmetic, 60° vertical field of view assumed; the game's is
narrower, which only helps)

12. At 48 m the view is 2 × 48 × tan 30° ≈ 55 m tall: **19.5 px per metre at 1080p, 39 at 4K**. A
    real 7 cm wingspan is 1.4 px at 1080p — invisible. About 8 px reads as a flapping silhouette
    and 4 px as a flicker, so a **0.4 m wingspan at 1080p, 0.2 m at 4K**. At 160 m a 0.4 m wing is
    2.3 px; at 10 m it is about 37 px. So **four to six times life size**, fading out beyond about
    80 m. No published figure for how much top-down games exaggerate was found.
13. **At 60 fps a 10 Hz beat is six frames a stroke; at 15 Hz and 30 fps it is two and strobes.**
    Keep the drawn rate at or below about 10 Hz.

**What looks wrong**

14. Flapping in unison (fix: per-instance phase, a rate within ±15%, jitter); paths that are too
    smooth (sums of sines and Lissajous figures read as orbits — real flight changes heading
    abruptly); constant speed; flapping while gliding; a symmetric sine stroke; a rigid body with
    no bob; landing with the wings open; a bee-like blur.

## Recommendation

A small CPU state machine feeding one draw, with the wings animated in the vertex shader. A purely
analytic path cannot scatter when a colonist walks up, so some state is required.

- Per butterfly a few floats and a state (wander, approach, rest, take off, flee). Within a flight
  leg, re-roll the heading every 0.3–1 s rather than following a smooth field. Bursts of 3–8 beats
  alternate with 0.3–1 s sinking glides. Land on grass, rest 2–20 s with the wings closed upright,
  a third of them basking with the wings opening slowly. Flee a walker at about 2–2.5 m.
- Four wing panels hinged on the body axis. The shader computes the asymmetric stroke (42% down,
  about 110° peak to peak, the hindwing lagging), the counter-phase bob and the bank.
- About 0.3 m across, fading out beyond about 80 m.

## Sources

- https://pubmed.ncbi.nlm.nih.gov/24166827
- https://royalsocietypublishing.org/rsif/article/22/229/20250061/235630/Body-oscillations-couple-with-wing-flapping-to
- https://www.mdpi.com/2076-3417/11/6/2620 (403; search snippets only)
- https://journals.biologists.com/jeb/article/150/1/37/5704/Biomechanics-of-Flight-in-Neotropical-Butterflies
- https://www.biorxiv.org/content/10.64898/2026.06.03.729813v1.full (429; search snippets only)
- https://sicb.org/abstracts/erratic-flight-in-butterflies/
- https://pmc.ncbi.nlm.nih.gov/articles/PMC9091839/
- https://www.sciencedirect.com/science/article/abs/pii/S1226861518306149
- https://butterflyask.com/species-identification/butterfly-resting-wing-positions/
- https://pmc.ncbi.nlm.nih.gov/articles/PMC6987309/
- https://halisavakis.com/my-take-on-shaders-butterflies-and-fish-shader/
- https://github.com/tantaneity/butterfly-gen
- https://blog.playstation.com/2021/01/12/how-stunning-visual-effects-bring-ghost-of-tsushima-to-life/
- https://gdcvault.com/play/1027124/Blowing-from-the-West-Simulating

## Confidence

- Wingbeat frequency and amplitude: **high**.
- Speed and the body bob: **medium** (the direction of the bob is supported; no amplitude).
- Resting posture: **medium** (the basking detail is from popular sources).
- Startle distance: **low** (the numbers are in a supplement that was not read).
- Game techniques: **medium** (indie and GDC material; no AAA butterfly write-up found).
- The pixel arithmetic: **high**; the 8 px readability threshold is a rule of thumb, **medium**.
- Failure modes: **medium**, inferred from the kinematics.

## Could not be determined

- How far the body bobs (cm) or pitches (degrees).
- Measured startle distances in metres.
- How long butterflies rest or bask, and how often they glide against how often they flap.
- How much top-down games exaggerate a butterfly's size.
- Any AAA write-up on butterflies.
- Escape speed in m/s.
