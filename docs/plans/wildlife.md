# Plan — wildlife: how a world gets its animals, and the Animals panel

**Phase 3, 2026-09-23.** Answers: the interview's recommendations, adopted whole (owner:
*"ok pig is good enough for now - please plan out and execute"*, which also ran this phase and
the next together). Interview: `docs/research/animal-generation-interview.md`. Design lands in
`docs/design/30-wildlife.md` as the code does.

## Units

| Unit | What | Where the decision lives |
|---|---|---|
| **WL1** the table | `MapGenDef` carries `wildlife` (kind, weight, group size, habitat), `wildlifePer10000Columns` and `wildlifeCeiling`. The wooded meadow lists hog sounders in woodland and rats by rock; the city lists rats first and a few hogs; the bare board lists nothing, because anything that is not grass on it is a bug. | design 30 §1 |
| **WL2** seeding | `WildlifeSeeder` at tick zero, after the colonists are placed: a census of the walkable, dry, reachable surface columns sets the target; groups are placed on habitat cells outside the starting clearing. Deterministic from the world seed. | §2 |
| **WL3** the level | `WildlifeSystem` on the rare tick: below target, a group arrives at the board edge; each animal has a small chance per check of deciding to leave, walks to the nearest reachable edge cell and is removed there. `PawnRegistry.Despawn` is new. `Pawn.Leaving` is hashed and saved in its own section, so no format bump. | §3 |
| **WL4** night | `SpeciesDef.nocturnal`: a rat is out at night and rests by day; a hog the other way round. Off-hours legs are a quarter as frequent and rests three times as long. | §4 |
| **WL5** proof | Fast tests for the census, habitats, the clearing, reachability, determinism, departure, arrival, the ceiling and the save; a Long ten-day run; the goldens re-baked with the probe's justification; CLAUDE.md, journal, playtest row. | §5 |
| **WL6** the Wildlife panel | Same PR in the end. **F6**, not F2, and **Wildlife**, not Animals: the registry and the bar have carried both tabs since M1 and F6 has been Wildlife's placeholder all along. One row per animal (kind, doing, layer, cells from the colony), per-kind counts, a click is the roster path. `WildlifeModel` in `Odyssey.Hud`, Unity-free; the panel pooled and paged like the roster. | §6 |

## Order and branches

`claude/wildlife` is stacked on `claude/animals` (PR #167); PR #169 targets that branch until it
merges, then `main`. WL1–WL6 landed as two commits on the one PR.

## Numbers, all playtest numbers

| | Value | Why |
|---|---|---|
| Target density | 15 per 10,000 walkable, dry, **reachable** surface columns | an animal reaches under half the meadow (6,354 of 14,400 columns on seed 1, because it hops only at ramps), so this is nine or ten animals: about one per 1,500 cells of board |
| Ceiling | 24 | the 64 drawn figures are the colonists' first |
| Hog sounder | 3–5, weight 3, woodland | a pig lives in a family group |
| Rat | 1, weight 2, by rock | solitary, and the rig has a walk clip so a rat can be watched |
| Clearing kept clear | `startingFellRadius` + 6 | nothing spawns in the colony's lap |
| Departure | 2 per mille per rare tick | a mean stay of about two days |
| Arrival, below target | 60 per mille per rare tick | topped up within about a minute |
