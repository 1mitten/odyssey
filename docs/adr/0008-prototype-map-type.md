# ADR 0008 — The prototype starts on an empty natural map, not a ruined city

Status: **accepted (owner decision, 2026-09-15)**. This reverses an agreed decision in the brief, so it is recorded here rather than edited quietly into the design documents.

## Context

Brief §2 fixed the prototype map type as a **ruined city**: pre-placed multi-storey shells to occupy, streets to dig beneath, salvage instead of ore veins. That generator was built and works — ten passes, a full 250 × 250 × 40 map in 215 ms, deterministic, with cell-authored shell templates.

The owner then directed: *"The map should be empty to start with. Grass, trees, mines, and you build from there. Forget the ruined city for now."*

## Decision

**The prototype's starting map is an empty natural wilderness** — terraced ground, grass, trees, stone outcrops and ore deposits — and the colony builds from nothing.

**The ruined-city generator is kept, not deleted.** It becomes a selectable map type behind `MapGenDef`, and the brief already anticipates later map types (outskirts/frontier, living district) which it now serves.

## Why this is a reasonable reversal

Worth stating, because reversing an agreed decision deserves more than "the owner said so":

- **It is the smaller problem.** An empty map is strictly simpler to generate, to render and to reason about than a ruined one. It gets to a playable loop sooner, which is the stated goal.
- **It is the better tutorial for the mechanics.** Building up from nothing exercises the support-and-collapse model directly and visibly, because every slab the player places is one they chose to support. Occupying a pre-standing shell hides that model behind geometry the player did not build.
- **Nothing structural is lost.** The cell model, support solver, pathfinding, pawns, jobs, save format and rendering are all map-type agnostic. Only the generator changes.
- **It restores a conventional opening.** Starting in wilderness and building a colony is the genre's default for good reasons, and it makes the vertical slice comparable to its reference points.

## What it costs

- The shell templates, damage pass, street grid and salvage-deposit weighting become **dormant**, not dead. They are exercised by their own tests and stay green.
- Digging changes meaning. In the city it was breach a slab, clear rubble, or mine rock, resolved by depth. On a natural map it is mining rock and soil, with ore as the reward. `03-systems-catalogue.md` system 19 is affected and now covers both.
- Salvage as the primary material economy is deferred with the city; wood and stone lead instead, which suits the owned Synty Farm pack.

## Consequences

1. `NaturalMapGenerator` is the default; map type is a `MapGenDef` field, so both paths are live and both are tested.
2. The art requirement shifts toward the Farm and Western Frontier packs for trees, rock and ground cover. The Sci-Fi City modules remain the *built* vocabulary — the colony you construct is still sci-fi, the land it stands on is not.
3. Layer question 11 ("what does digging mean") has a second answer for this map type, recorded in the systems catalogue.
4. The brief's §2 setting row is superseded by this ADR for the prototype. The wider setting, a sci-fi city-world, is unchanged.
