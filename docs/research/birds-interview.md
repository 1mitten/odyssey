# Ambient birds — the interview, 2026-09-25

**Phase 1 for the birds unit**, an owner request ("could we procedurally generate some birds flying
around … or even a low poly bird"). The ground was `d-21-ambient-birds.md` (technique and prior art),
`e-10-bird-models.md` (models and licences) and the sketch in `docs/reference/mockups/birds/`
(hosted: https://claude.ai/artifact/7Y3abzdRug1KTkjU1xFiTZ), which the owner asked for before
deciding ("what would they look like — we need to be specific"). Three questions were put; every
answer below is the owner's, with the consequence beside it so the design can be written without
asking again.

## Answers

| # | Question | Answer | What it decides |
|---|---|---|---|
| 1 | How big is a bird drawn? | **It grows with zoom**: life size close in, rising to about ×1.75 at the 160 m zoom. | One scale uniform driven by camera distance, eased between the two ends. Not a fixed ×1.75 (oversized beside a colonist at 10–20 m) and not life size (a rook is ~8 px at 160 m, `07-game-160m-life-size.png`). The curve's shape is the design document's to set and the playtest's to judge. |
| 2 | Ground birds, or flying and perching only? | **Fly and perch only.** | No bird walks or pecks on the ground, so **no model is needed and nothing licensed is involved**. The whole unit is built in code: a procedural mesh and a vertex-shader flap. Perches are treetops and roofs, read from the sky height map the rain already uses. The ground pigeon of the sketch is dropped (it read as a dart at 16 m, `03-game-20m-pigeons.png`). A modelled ground bird is a possible later unit, not this one. |
| 3 | Which birds first? | **Rooks and the buzzard.** | Two species, two instanced draw calls plus their shadows. Rooks: flocks that circle, land in treetops and on roofs, scatter from colonists and edits, and gather at a rookery at dusk. The buzzard: one or two circling high, almost always gliding. Wood pigeons and swallows are left out; both remain in the sketch as reference. |

## What was already settled by the research (not asked)

- Presentation only: never in a cell, a save, the state hash or the pawn registry. Not clickable.
- CPU flocks (a leader on a smooth path, light flocking within its own flock), at most ~120 birds.
  Compute boids only if a later ask wants hundreds.
- Their own frame section and a timing arm, and the new shader in `ShaderInclusion` and
  `InstancingKeepAlive`, or the player build draws none.
- Time of day and weather follow the birdsong already in the ambience: roost at dusk, none flying at
  night, thinner in rain, none in a storm.

## Next

The design document (`docs/design/47-ambient-birds.md`), then the plan, both for approval before any
code.
