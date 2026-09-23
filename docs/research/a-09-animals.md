# a-09 — Animals: the data shape, the wild think tree, movement and arrival

**Phase 2, Lane A**, run 2026-09-22 for the animals unit (systems catalogue §9). One subagent,
capped at 8 searches / 10 reads; it used 5 and 2. Clean room: mechanics and field names only,
nothing pasted.

## Question

How does RimWorld model an animal as data and as behaviour — the race-versus-kind split, what
a wild animal does at rest, what it may walk through, and how wild animals arrive and how
many — at the level of shapes and mechanics a 3D colony sim can copy without copying values?

## Findings

1. **Two layers, with different lifetimes.** The *race* (a record on the species' thing Def)
   holds what the animal *is*: body size, health scale, hunger rate, a life-stage table with
   per-stage body-size and hunger multipliers, diet and fleshiness, the herd flag, the predator
   flag with a maximum prey size, trainability, whether it can be a pet. The *kind* (a
   spawnable unit) references a race and adds what the *encounter* is: a label, a combat power
   for the threat budget, an ecosystem weight for the wildlife budget, a wild-spawn commonality,
   and name pools or gear for humanlikes. Biomes reference kinds, never races. The split exists
   so one species can have several spawn configurations (faction variants, quest variants) and
   so everything that refers to the species — corpses, meat, taming — refers to one record.
2. **Wildness moved from a race field to an ordinary stat in 1.6**, so it is patchable like any
   stat. It is a 0–1 scalar shown as a percentage: it sets the minimum handling skill and
   multiplies both tame and train chance (0 % doubles tame chance, 50 % is neutral, 100 % is
   untameable). An animal needs over about 10 % wildness ever to revert to wild, and the
   tameness-decay interval scales inversely with it (roughly 100 to 200 in-game minutes).
   Per-animal *tameness* is a separate 0–5 relationship meter that decays without handling.
3. **A wild animal at rest** pursues an unmet need first — grazers eat any living plant
   including crops, predators hunt — then wanders the reachable map (or its pen if it has one),
   sleeps when tired or wounded (wild ones rest in place; tamed ones prefer an animal bed), and
   mates opportunistically. Flee and manhunter are a *separate top-level branch* that overrides
   the routine priorities rather than being woven into each leaf.
4. **Movement.** Wild animals cannot pass doors; tamed ones of a race that can, can — so the one
   structural precedent for a per-species terrain gate is keyed on race and overridden by tame
   state. There is no vertical axis and therefore no stairs-or-ladders capability anywhere in
   the model. Move speed comes through the general pawn stat pipeline (body size and health
   feed it), not an animal-only formula.
5. **Arrival and numbers.** Wild animals enter at map edges according to the biome, and by
   incidents (migrations, insanity). A biome maps a kind's commonality to a per-biome
   likelihood. Population is bounded by an ecosystem-weight budget per map; there is a
   documented rough guess of a kind's weight from body size × hunger rate, used as a sanity
   check on hand-authored weights.

## Recommendation

Copy the **two-layer split** — a species record (size, life stages, diet, movement
capability) and a spawn record (threat weight, ecosystem weight, commonality) — because it is
what lets corpses, meat and taming all point at one species while the storyteller points at
kinds. Copy the **flee branch as an override**, not as a case in every leaf. Copy the shape of
the door gate as the precedent for a **per-species vertical capability** (walk, hop, stairs,
ladder, swim) declared on the species and read by the pathfinder's traverse rules — the thing
RimWorld never needed and this game does. Do **not** chase the wander radius, cadence or
ecosystem numbers: they were not recoverable and are tuned to a different game; make them Def
fields and tune them by playing. For the MVP the species record needs only size, speed,
capability and a wander radius; the kind record needs only a label, because the debug menu is
the only spawner.

## Sources

- https://rimworldwiki.com/wiki/Modding_Tutorials/Docs/Pawn
- https://rimworldwiki.com/wiki/Animals
- https://rimworldwiki.com/wiki/Animal_husbandry
- https://rimworldwiki.com/wiki/Property:Wildness
- https://rimworldwiki.com/wiki/Property:Combat_Power
- https://rimworldwiki.com/wiki/Tame_Animal_Chance
- https://rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.6_Mod_Updates
- https://rimworldmodding.wiki.gg/wiki/Def_Types

## Confidence

- **Medium** for the race/kind split (confirmed across several pages, no single table read whole).
- **Medium-low** for the think tree (shape confirmed; wander radius and cadence not found).
- **Medium** for doors; **low** for the move-speed formula.
- **Medium** for the arrival mechanism; **low** for the density numbers.
- **High** for wildness.

## Could not be determined

- The wander destination algorithm: radius, how often, any terrain preference.
- The exact sleep trigger beyond "tired or wounded".
- Numeric biome density targets and the cadence of the wildlife population check.
- The animal move-speed formula relative to a human's.
