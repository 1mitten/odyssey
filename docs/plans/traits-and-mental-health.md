# Plan — traits and mental health (TM)

**Approved 2026-09-25** (owner: *"yes implement"*, after two interview rounds). Interview
`docs/research/traits-interview.md`; design `docs/design/43-traits-and-mental-health.md`, whose
decision table is the contract. Research `a-18`, `a-19`, `b-mental-health-models`, `g-04`.

## Units

| Unit | What | Done when | Goldens |
|---|---|---|---|
| **TM0** | Interview, four research lanes, design 43, this plan | written and committed | none |
| **TM1** fix first | The simulation publishes the mood band, the target and the three lines; the roster, pane and alert read the band and copy no threshold; a rested colonist is Content | a test fails when the band is withheld; a colonist at the resting target reads Content | none (views are unhashed) |
| **TM2** the Thoughts tab | `ui.thought.*`; memories and situational offsets published; the tab enabled and drawn | the tab lists a friendly-fire memory with its −8 and its time left; wiki and registry gates pass | none |
| **TM3** traits in the simulation | `TraitDef`, the placeholder set, dealt on the first tick under a new stream, saved in `odyssey.pawn.mind`, hashed while present; mood, threshold, learning, work speed and incapable-of read them | a disabled work type is never taken; an old save round-trips traitless; the stream moves no passion or skill | every `Simulated` once, probe-diffed |
| **TM4** traits shown | Published per slot with effects; the inspect pane's rows; the select screen's detail pane; the Work tab's greyed cells | a rerolled card changes name, face, skills and traits together (playtest) | none |
| **TM5** tiers and breaks | Three lines and clocks; sulk, binge, tantrum, berserk beside wander; the break kind saved and hashed while set; an Events row and a tiered alert | a forced-mood test enters each break and sees its behaviour; a berserker is ended by being downed | none expected (no golden colony breaks) |
| **TM6** the gate | A ten-day run on three seeds with mood forced low; docs, playtest rows, handover | Long tier green, every break seen, lockstep twin agrees, a save mid-break resumes the same | none |

## Order

One branch, one commit per unit, TM1 → TM6. Single-threaded: every unit touches `Pawn.cs`,
`PawnContent.cs` and the registry CSV, and the goldens move once.

## What only the owner can do

- Fill the traits table in the interview file. The placeholder set is replaced then.
- Play it: the Unity tiers and the player build cannot run in this container, so every
  presentation file (`HudShell.*`) is unproven until Unity compiles it.
