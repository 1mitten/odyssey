# Forest animals — the ground, the breakdown and the interview, 2026-09-26

**Phase 0–2 for the forest animals unit.** The owner supplied `SIMPLE_Forest_Animals_Unity_2020_3_v1_02.unitypackage`
and asked for *"a roster of animals that spawn into the world — performant — the same treatment
[as the hog, rat and frog] — and a recommendation for how we should stat them, how aggressive they
get (if at all) and their behaviours."* Research is in three files written for this unit:
`e-15-simple-forest-animals.md` (the package, measured from its files), `a-20-wild-animal-temperament.md`
(the reference game's stat block and aggression model, clean room) and `b-wild-animal-behaviour.md`
(other games and the real animals). The code ground below was taken on `origin/main` at `b3d3128d`.

## 1. What the package is

Synty Studios' **SIMPLE** line, Asset Store EULA: usable in the shipped game, not redistributable,
so it is **licensed art** — it lives under the gitignored `Assets/Synty/`, is never committed, and
the game and its tests must build and run without it. The package installs to
`Assets/SimpleForestAnimal/`, which is **not** ignored, so the import must move it in the same run.

| File (rig) | Species in it | Variants | Tris | Clips |
|---|---|---|---|---|
| `Rabbit.fbx` (RabbitRig, 38 joints) | rabbit | 3 | 702 | Idle, Walk, Run, Eat |
| `Deer.fbx` (Horse_Rig, 42) | doe, stag, moose cow, moose bull | 2 / 3 / 2 / 3 | 790–1,042 | the same four |
| `Fox.fbx` (GoatRig, 52) | fox, raccoon, skunk, wolf | 3 / 2* / 3 / 3 | 846–1,068 | the same four |
| `Boar.fbx` (PigRig2, 32) | boar | 3 | 668 | the same four |
| `Bear.fbx` (CowRig, 44) | bear | 3 | 874 | the same four |

\* `Raccoon_03` is the same colourway as `Raccoon_01` — a pack defect, two raccoons in practice.

- **Variants are colour only**: one geometry per species, the UVs moved over one 1024² palette.
- **No root motion**, 24 fps, all four clips loop. **No attack, death, downed, sleep or turn clip**
  on any rig — those are computed, as the hog's walk and the colonists' work strokes already are.
- **Not to scale with each other.** Species sharing a rig share a back height, so the bull moose is
  barely taller than the stag and the wolf is 1.3× the fox. The prefabs scale 5–13× and land at two
  to four times life. **Scale is set per species on our catalogue row, not per file.**
- Materials are on the built-in Standard shader (pink under URP); they need our own URP material
  over the palette, which is what `AnimalImport.Paints` already does for the frog.

## 2. What is true in the code today

- **An animal is a `Pawn` with a kind.** `SpeciesDef` (size, pace, traverse mode, wander radius,
  rest span, nocturnal, bank radius, rain, HP, revenge chance, natural attack, melee skill) and
  `PawnKindDef` (species, faction). Kinds today: 0 colonist, 1 midden hog, 2 duct rat, 3 bandit,
  4 gunman, 5 culvert frog. **The butcher (PR #248) takes kinds 6–9 and species 4–7**, so this
  roster starts at kind 10 / species 8 if it merges first. The numbering is hand-kept in three
  tables (`PawnKindIndex`, `PawnKindLabels`, `ModuleCatalogue.AnimalNames`).
- **The mind** is one tree: downed → combat (revenge) → shelter from rain → idle (wander or rest,
  nocturnal, bank, leave by the edge). **Flee and revenge are built**: a struck animal rolls
  `revengePerMille` and either fights whoever hit it or runs. **Nothing is proactive** — no
  predator, no startle, no warning, no herd, no hunger.
- **Health**: an animal is a hit-point pool alone (no regions, no bleeding); it can be downed and
  killed and leaves a corpse drawn from its own figure. Nothing hauls or butchers a corpse and
  there is no meat item: the **hunt designation, butchering and meat are cooking's K3**, planned
  and not built.
- **Wildlife** is seeded at worldgen from a C# table (kind, weight, group, habitat) at a density per
  ten thousand reachable columns, topped up at the edge, **capped at 24 animals a world** — the
  figure budget's share. Habitats: Any, Woodland, Rock, Bank.
- **Drawing**: animals are live figures only, inside the 64-figure ceiling (nearest the camera
  first). **An animal whose art did not resolve, or past the ceiling, is not drawn at all** — the
  baked far form skips animals. That is the runner's case for every Synty animal.
- **Clip choice is a speed blend** over the row's locomotion clips; there is no per-job clip, so the
  pack's **Eat** clip has no driver yet.

## 3. The breakdown — what "a roster with the same treatment" is

Each species needs the same eleven touches the hog, rat and frog had (Defs, the three kind tables,
catalogue row and scale, importer, wildlife table, debug Spawn row, wiki CSVs, Almanac entry, tests,
docs). That is mechanical. **The work that is not mechanical is behaviour**, because today every
animal behaves alike until it is struck. Five parts, in dependency order:

| Part | What | New code |
|---|---|---|
| **F1 Art** | Import the pack under `Assets/Synty/`, URP material, per-species scale from a probe shot, colourways dealt by hash, Eat looped | importer, catalogue rows |
| **F2 Roster** | The species and kinds, the wildlife table with habitats and groups, debug Spawn rows, wiki, Almanac | Defs, table, one or two habitats |
| **F3 Temperament** | A few independent fields on `SpeciesDef` read by the idle node: startle radius and response (bolt, freeze-then-bolt, stand, display, attack), warning display with bluff chance, herd response radius, active hours | one think node, one proximity scan |
| **F4 Signatures** | The per-species characters: skunk spray, rut, crop grazing, predators hunting, hibernation | one rule each |
| **F5 Frame** | The population ceiling, the figure budget, and what happens past it | a measurement first |

## 4. The recommended roster

Temperament rungs from `a-20` (each is a behaviour, driven by fields, not one hidden roll).
Size is life shoulder height; pace and HP are against a colonist's 1,000 and 100.

| Species | Rung | Shoulder | Pace | HP | Group | Habitat | Hours | What makes it play differently |
|---|---|---|---|---|---|---|---|---|
| Rabbit | Timid | 0.25 m | 1,300 | 12 | 1–3 | open grass | dawn, dusk | freezes, then bolts; outruns anyone; grazes crops |
| Deer (doe, stag) | Timid | 1.0 / 1.2 m | 1,200 | 90 | 3–6 | woodland edge | dawn, dusk | one bolts and the herd flags and follows; the stag stamps and charges in the autumn rut |
| Fox | Skittish | 0.40 m | 1,050 | 30 | 1 | woodland edge | night | keeps its distance; takes rabbits and rats |
| Raccoon | Skittish | 0.30 m | 900 | 25 | 1–2 | water banks | night | fights only when cornered; the store thief (later) |
| Skunk | Skittish, stands | 0.25 m | 700 | 20 | 1 | woodland | night | does not run: stamps, lifts its tail, sprays — a day's mood penalty, no damage |
| Boar | Defensive | 0.90 m | 1,000 | 70 | 3–5 | woodland | night | ignores you until struck; the sounder turns together |
| Moose (cow, bull) | Defensive | 1.8 / 1.9 m | 1,000 | 190 | 1–3 | banks, woodland | day | never flees; stands, ears back, bluff-charges; the bull is dangerous in the rut |
| Wolf | Predator | 0.80 m | 1,100 | 90 | 2–4 | deep woodland | night | a pack acts as one; hunts deer and rabbits when hungry |
| Bear | Territorial | 1.1 m | 1,000 | 250 | 1 | woodland, rock | day; sleeps the winter | warns (huff, rear), bluff-charges anyone lingering where it rests; its first blow stuns |

Performance targets for the plan (to be measured, not assumed): the wildlife layer under
**0.05 ms a tick** at 48 animals and 50 colonists (a startle scan is animals × colonists on the rare
tick, ~2,400 distance tests); each figure is under 1,100 triangles, against the rat's 4,004.

## 5. Questions

Put to the owner on 2026-09-26 in four rounds of four: the roster (scope, the hog, size, names),
aggression (the model, man-eaters, after a down, predation), character (signatures, hours, the kill
window, warnings) and delivery (population, far animals, hunting, PRs). Each carried a
recommendation; the owner took every one but one, **after a down**, where they chose that a
predator can kill.

## 6. Answers

| # | Question | Answer | What it decides |
|---|---|---|---|
| 1 | Which species? | **All nine**: rabbit, deer (doe and stag), fox, raccoon, skunk, boar, moose (cow and bull), wolf, bear. | Nine species, eleven drawn forms. Doe/stag and cow/bull are one species each with a sex (see decisions). |
| 2 | Hog against boar | **Both, different places.** | The boar is the woodland species on the meadow; the midden hog stays the ruined city's scavenger, and its CC0 art stays the proof that an animal draws on the runner. |
| 3 | Size | **Life size against a colonist.** | Real shoulder heights (§4), set per species on the catalogue row from a probe shot, never from the pack's prefab scale. |
| 4 | Names | **Flavoured, like the hog**, proposed by us and corrected by the owner in the wiki before anything ships. | The proposal is in §8. Every name goes through `proper-nouns.csv` and `icon-keys.csv`; none may be a reference-game animal name. |
| 5 | Temperament | **Five rungs, visible.** | Timid, Skittish, Defensive, Territorial, Predator — each a behaviour built from `SpeciesDef` fields, and the rung **written in words** on the inspect pane and in the Almanac. |
| 6 | Man-eaters | **Only when starving and the colonist is alone, and a world setting can switch it off.** | Predators take animals and avoid people otherwise. A predator stalking a colonist raises a **pinned alert** that jumps to it. |
| 7 | After a down | **It can kill.** | **A deliberate exception to design 33's "an unordered fight ends in downs, never deaths"**, for predators alone. Recorded in design 33 when built; the only thing in the game that kills without an order. |
| 8 | Predation | **Yes, and the kill is left.** | Predators get a hunger clock (the first animal need); a hungry predator chases prey smaller than itself, eats, and leaves a corpse that K3's hunting can later salvage. |
| 9 | Signatures | **All four**: skunk spray, the rut, crop grazing, raccoon store raids. | Spray is a mood thought and a smell for about a day, no damage; stag and bull moose are *In rut* in autumn; rabbits and deer eat growing crops at dawn and dusk; a raccoon walks into a stockpile or shelf at night and leaves with food. |
| 10 | Hours | **Night, day, dawn-and-dusk, and winter sleep.** | `nocturnal` becomes an activity pattern; the bear leaves sight for the winter. |
| 11 | The kill window | **A rescue window.** | A downed colonist under a predator loses health steadily for about one game hour (~40 s at speed 1) and dies at its end unless it is driven off. The alert names her. |
| 12 | Heeding a warning | **Back off and path round.** | An undrafted colonist stops, backs away and routes round a warning animal, and her activity line says so; a drafted one does what she is ordered. |
| 13 | Population | **Scale with the board: Small 32, Standard 48, Large 64, Huge 80.** | The 24 ceiling becomes per board. The wildlife layer's cost is measured in the work, target **under 0.05 ms a tick**. |
| 14 | Far animals | **The cheap far form.** | Past the 64-figure ceiling an animal is a frozen baked pose, one instanced call per species, as colonists are. Closes design 29 §8a. |
| 15 | Hunting | **Stats now, hunting in K3.** | Each species carries meat and hide yields as data; K3 reads them. A drafted colonist can still kill an animal and the corpse stays. |
| 16 | Delivery | **Three PRs, a playtest each.** | **FA1** art and roster (spawn and wander, life size, named); **FA2** temperament (rungs, warnings, herds, predators, the kill window, the far form); **FA3** signatures (spray, rut, grazing, raids, hours). Each is played before the next starts. |

## 7. Decisions made without asking, and why

Routine calls the answers imply; any can be overturned at the plan.

- **Sex is derived, not rolled into a new saved field.** Doe/stag and cow/bull come from the pawn's
  own roll seed, like a colonist's appearance, so the rut (which moves behaviour and therefore the
  hash) reads a deterministic value that is already saved. Colourway is dealt the same way.
- **The pack is imported to `Assets/Synty/SimpleForestAnimal/`** and moved there in the import run,
  with a guard that fails if `Assets/SimpleForestAnimal/` exists. Nothing under it is committed; a
  test that needs one of these animals asks whether *that kind's* row resolved.
- **The missing clips are computed**: the warning (stamp, rear, tail up), the lunge, the charge's
  stop-short, lying down to sleep and hibernate. The pack's **Eat** clip is looped and driven while
  grazing — the first per-job clip an animal has.
- **Temperament numbers start from `a-20`'s table** (converted from the reference, marked where ours)
  and are tuned in play, not argued in advance. Rage lasts 10,000–18,000 ticks; herds answer within
  12 cells.
- **Kinds are appended after the butcher's** if PR #248 merges first (kind 10 on), and the three
  hand-kept kind tables move together.
- **Stale lines found on the way are fixed in FA1**: the frog's "kind 4" comment, design 30's
  density and "nothing flees", design 29's hog pace, and `WildlifeSystem`'s "no death" comment.

## 8. Proposed names — for the owner to correct

Flavoured as the hog, rat and frog are. None is a reference-game animal name.

| Species | Proposed | Forms |
|---|---|---|
| Rabbit | **Verge rabbit** | — |
| Deer | **Hedgerow deer** | doe, stag |
| Fox | **Ash fox** | — |
| Raccoon | **Gutter raccoon** | — |
| Skunk | **Rubble skunk** | — |
| Boar | **Thicket boar** | — |
| Moose | **Mire moose** | cow, bull |
| Wolf | **Ridge wolf** | — |
| Bear | **Quarry bear** | — |

## 9. Next

Phase 3, the plan: `docs/plans/forest-animals.md` and a design document for the temperament model,
written and put to the owner before any code.
