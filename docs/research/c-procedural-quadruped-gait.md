# c — A computed quadruped walk: phases, duty factor, amplitudes

**Phase 2, Lane C (literature)**, run 2026-09-22 for the animals unit, because the pig has no
walk clip and the owner chose a computed gait over retargeting or authoring one. One subagent,
capped at 8 searches / 10 reads; it used 4 and 6.

## Question

What is the simplest convincing procedural four-legged walk for a stylised low-poly game seen
from 20–40 m, on a rig with UpLeg / LowLeg / Foot per leg, and what are its starting numbers?

## Findings

1. **Phases.** A walk is a four-beat *lateral sequence*: left hind, left fore, right hind,
   right fore, each a quarter cycle apart (0°, 90°, 180°, 270°). Diagonal legs are *not*
   paired in a walk. A trot pairs them — left fore with right hind, right fore with left hind —
   half a cycle apart. **Duty factor** (fraction of the cycle a foot is planted) is 0.6–0.75
   for a walk, so three feet are always down, and about 0.5 for a trot.
2. **Speed.** Speed rises mostly by lengthening the stride; cadence saturates. The Froude number
   `v² / (g · leg)` says where a walk becomes a trot: about 0.3–0.5 across species. A pig's leg
   is roughly 0.35–0.45 m, so it walks below about 1.3–1.5 m/s and a 1 m/s pig is comfortably
   walking. Stride ≈ speed × cycle time.
3. **The minimal motion that reads.** One sine per leg at the hip or shoulder, about ±25°
   fore-aft; a second, phase-shifted bend at the knee of ±35° peaking mid-swing so the foot
   clears the ground and staying near-straight in stance; a body bob of a few centimetres at
   twice the stride frequency, derived from the mean height of the planted feet; a pitch and
   roll of two or three degrees following the unsupported corner; and an optional head counter
   bob. Sway is commonly driven at *half* the leg frequency to avoid a double bounce.
4. **Forward kinematics versus foot targets.** Sinusoidal joint angles (FK) are the cheapest
   and are the standard stylised low-poly answer; two-bone IK to a foot target is what you add
   when feet must plant on uneven ground or the camera is close enough to see them slide. At
   20–40 m the deciding factor is the terrain, not the distance.

## Recommendation

Drive the legs with **FK sines** — it is what `WorkSwing` already does for arms, it needs no
solver, and the board's terrace steps are already handled for colonists by lifting the whole
figure on to the ramp rather than by planting feet. Use the lateral-sequence walk; a trot is a
second parameter set for a faster animal, not a second system. Judge it against the rat's own
authored `Walk` (1.33 s cycle) by playing the two side by side. Starting table for a pig walking
at 1 m/s:

| Parameter | Start value |
|---|---|
| Leg phases (LH, LF, RH, RF) | 0°, 90°, 180°, 270° |
| Duty factor | 0.65 |
| Cycle time | 0.9–1.1 s, scaled inversely with speed |
| Stride | ≈ speed × cycle time, about 1 m |
| Hip / shoulder swing | ±25°, sine |
| Knee flex | ±35°, peaking mid-swing, flat in stance |
| Body bob | ±2–3 cm at 2 × stride frequency |
| Pitch and roll | ±2–3° |
| Head counter bob | ±1–2° |

Trot, if wanted: diagonal pairs at 0°/180°, duty 0.5, cycle time halved, amplitudes up a
quarter.

## Sources

- https://elifesciences.org/articles/29495
- https://jimusherwoodresearch.com/quadruped-walk/
- https://en.wikipedia.org/wiki/Gait
- https://www.nature.com/articles/srep08169
- https://pmc.ncbi.nlm.nih.gov/articles/PMC6871776/
- https://journals.biologists.com/jeb/article/209/3/455/16439/Dynamically-similar-locomotion-in-horses
- https://journals.biologists.com/jeb/article-abstract/207/24/4215/2679/Biomechanical-and-energetic-determinants-of-the
- https://en.wikipedia.org/wiki/Transition_from_walking_to_running
- https://blog.littlepolygon.com/posts/loco1/
- https://www.wayline.io/blog/procedural-animation-techniques
- https://www.alanzucconi.com/2017/04/17/procedural-animations/
- https://www.misultin.com/simple-procedural-walk/

## Confidence

- **High** for phases and duty factor (biomechanics literature, consistent).
- **High** for the Froude rule; **medium** for the pig extrapolation (no pig study found).
- **Medium** for the amplitudes: consistent across tutorials, no single authoritative source.
- **Medium** for FK versus IK.

## Could not be determined

- A published, fully numbered procedural quadruped rig (Overgrowth's is referenced, not found).
- Pig-specific stride and cadence measurements.
- A hard number for head bob.
