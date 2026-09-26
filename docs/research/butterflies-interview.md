# Ambient butterflies — the interview, 2026-09-25

**Phase 1 for the butterflies unit**, an owner request: *"Could we look into research and plan out
decent looking and moving procedural generated butterflies (if performant) - if it does hinder
performance - make sure it's a graphic setting. Also could you make them illuminate at night with
varying colours to make night time look spectacular. Interview me and explore."* The owner added
*"on your own worktree"*, so the work lives in `D:\code\odyssey-butterflies`.

Design 38 §10 had already reserved the slot: *"Ambient FX near the camera focus (butterflies,
petals, leaves, dust), drawn only, never in a cell, a save or the hash."* The ground was the
exploration of `origin/main` 4c40e189 and the three research files that followed the interview
(`e-13-butterfly-flight.md`, `d-24-butterfly-swarm-cost.md`, `e-14-butterfly-wings-and-glow.md`).
Eight questions were put in two rounds. Every answer below is the owner's, with its consequence
beside it so the design can be written without asking again.

## Answers

| # | Question | Answer | What it decides |
|---|---|---|---|
| 1 | What should night look like? | **The same butterflies glow**, each in its own hue, pulsing slowly. | One population and one pass, not a second night species. The glow is a term that rises with the night grade. |
| 2 | Should the glow light the world around them? | **Bloom plus a glow pool**: a faint pool of colour on the grass under each one. | No real lights (d-24: 256-per-camera cap, per-pixel cost at a GPU-bound 4K). The pool is an additive quad drawn by the pass itself. |
| 3 | How should they behave? (several allowed) | **All four**: flutter and drift; land and rest, wings folding and opening; scatter from colonists; seasons and weather. | A small state machine per butterfly. A stateless shader path cannot remember a startle (e-13). |
| 4 | What should the wings look like? | **Low-poly and patterned**: faceted wings in the Synty manner, colours and spots generated from each one's seed. | Real geometry rather than alpha-clipped quads (MSAA is off, e-14). A ground-plan pattern in the shader. |
| 5 | How should the graphics setting work? | **A density ladder**, Off / Few / Many / Swarm, default Many. | A `GraphicsLadder` in the Detail group, owned by the quality presets. |
| 6 | Which hues at night? | **Full spectrum**, weighted to cyan, violet, magenta and amber, each drifting slowly. | A four-hue weighted palette, hue drift per butterfly, checked for colour-blind players. |
| 7 | How many on screen at the default? | **Lively, about 150–300.** | Many = 200. |
| 8 | Where may they be? | **Over open grass** near the camera focus. Never indoors, underground, or over rock or water. | A habitat rule read from the render mirror: the top of the column is grass and nothing is built on it. |

## What was settled by the research and the exploration (not asked)

- Presentation only: never in a cell, a save, the state hash or the pawn registry. Not clickable.
- The shape of the birds unit (design 50, PR #230, in review) is followed so the two ambient passes
  read alike: an engine-free model in `Odyssey.Hud` behind a seam the render mirror answers, a
  frame section of its own, and the shader kept alive for the player build.
- Butterflies advance on real seconds while the world runs and freeze on pause. Birds and rain use
  game time; a butterfly cannot, because a 10 Hz wingbeat at speed 3 is 30 Hz and strobes (e-13).
- Drawn at about 0.36 m across (four to six times life size), or a real wingspan is one pixel at
  the default camera (e-13 §3).

## Next

The design (`docs/design/52-ambient-butterflies.md`) and the build, on one branch and one PR.
