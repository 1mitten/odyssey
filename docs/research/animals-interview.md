# Animals — the interview, 2026-09-22

**Phase 1 for the animals unit** (systems catalogue §9, pulled forward from M5). The ground
was `e-08-animal-fbx-inspection.md`: two Blender-made quadrupeds, the rat with a full clip set
and the pig with none it can walk on, no UVs, no health model and no species concept in the
simulation. Eight questions were put to the owner; every answer below is theirs, verbatim in
intent, with the consequence written beside it so the plan can be written without asking twice.

## Answers

| # | Question | Answer | What it decides |
|---|---|---|---|
| 1 | Where did the files come from? | **Quaternius or another CC0 set.** | They are **committed**, under `Assets/Art/Custom/Animals/`. The sim, the tests and CI may depend on them; nothing about them is licensed. Record the attribution beside the files. |
| 2 | What is the pig for? | **Both livestock and wild game, wild first.** | The wandering wild animal is the foundation; taming and pens are a later unit built on it. No pen, no animal food need, no ownership in the MVP. |
| 3 | What is the rat for? | **All four: vermin, small wild animal, threat, and the walking proof.** | The rat is the animal the MVP is proven on (it has every clip). Vermin (eats stored food), threat (bites) and hunting are later units and all wait on the health model. |
| 4 | How big is the first unit? | **Spawn, wander, draw, click.** | Animals exist, walk the nav graph, are drawn through the figure path and show a pane. No death, no meat, no taming. Save format moves (a new pawn kind is saved and hashed). |
| 5 | Where does the pig's gait come from? | **A computed trot.** | A procedural four-leg cycle driven from the rig, in the manner of `WorkSwing`. Works for any quadruped with the `Up`/`Low`/`Foot` convention; the rat's own `Walk` clip is the reference the trot is judged against. |
| 6 | Where do animals live and arrive? | **Debug menu spawn only for now.** | No worldgen scatter and no incident until the MVP has been played. A *Spawn pig* / *Spawn rat* row on the debug menu (`docs/design/18-debug-menu.md`), placed like the supply drop: on a walkable cell the player can find. Worldgen and a "wild animals wander in" `IncidentDef` are recorded as the two follow-ups. |
| 7 | How do animals use the third dimension? | **Rats climb anything; pigs take stairs and hops only, never a ladder.** | A per-species movement capability on the animal Def, read by the pathfinder's traverse rules. Layer-aware from the first commit. **Note:** stairs (`U44`) are not built, so a pig is hop-and-walk only until they are; the capability is declared now so nothing has to change when they land. |
| 8 | Which unit comes next? | **The health model.** | Alive / downed / dead for every pawn, animals included, is the unit after the MVP. Hunting, the rat threat, vermin culling and fall damage all queue behind it, in that order of dependency. |

## Decisions I am making without asking, and why

These are routine calls the answers above imply. Any of them can be overturned at the plan.

- **An animal is a pawn.** `Pawn` is one class with the position, the path buffer, the needs
  and the hash plumbing an animal needs. A *kind* on the pawn (colonist, pig, rat) with a Def
  behind it is the smallest change that keeps one mover, one save section and one figure
  director. A second class would duplicate the pathing and the save code for no gain.
- **Numbers.** The debug row spawns one animal a press. Nothing caps the count in the MVP; the
  figure ceiling of 64 live figures (`PawnFigureDirector.FigureCeiling`) already keeps the
  far ones frozen, nearest first, and the vision's 300-animal target is a measurement for the
  hardening track, not a feature of this unit.
- **Wandering** is a random walk to a reachable cell within a short radius, resting between
  legs, with the pig's radius longer than the rat's. No flocking, no fleeing, no grazing.
  The rat is drawn to darkness and the pig to grass only if that costs nothing extra.
- **The pane** says the species, a name if it has one (it does not, in the MVP), what it is
  doing and where it is. It reuses the colonist pane's shape.
- **Names go through the wiki.** "Pig" and "Rat" are player-facing content, so they enter
  `icon-keys.csv` and `proper-nouns.csv` in the same commit that adds the Defs, and the two
  `--check` gates run.
- **Style is a playtest question, not a build question.** The flat two-colour models will not
  read as the Synty family. The owner asked for a placeholder; the first play decides whether
  it clashes enough to matter.

## Open, for the research phase

- How RimWorld shapes an animal *kind* against a *race* (the data shape, not the values) and
  what a wild animal's think tree actually does at rest — one subagent, capped.
- Whether Unity imports the two rigs cleanly as Generic avatars at a sane size (the rat's
  armature carries a 39.55 scale against the mesh's 100). A Unity import in the worktree,
  measured with the same sole-and-crown bake the colonists use.
- Whether the rat's `Walk` and `Run` clips loop cleanly and whether `Jump` carries root motion.

## What this does not cover

Taming, pens, breeding, animal food, hunting, meat, the corpse, the rat eating stored food,
biting, predators, fleeing, worldgen scatter, the arrival incident. Each is named here so the
plan can list them as later units rather than as things forgotten.
