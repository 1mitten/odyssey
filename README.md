# Odyssey

A prototype **colony sim in the RimWorld mould, in true 3D with discrete vertical layers**, set in a wooded valley on a ruined sci-fi world. Unity 6.3 LTS (6000.3.x), URP, C#. The simulation is plain C# — single-threaded, deterministic, and testable with no Unity installed — and the board is a sliceable stack of layers you build up, dig down, and cut the camera through at any depth.

Odyssey is a work in progress by one owner and a bench of AI coding agents. It is **playable today**: a colony of three founders chops, mines, hauls, builds, farms, cooks, tends its sick and wounded, defends against raiders, and lives through seasons, weather and the night. What is *not* there yet is listed just as honestly under [Limitations](#honest-limitations). The scale target — 250 × 250 × 40 cells, 50 colonists, 60 FPS at 3× on a 2022 mid-range laptop — is the destination, not the current board.

## The content wiki

Every named thing in the game — commodities, items, buildings, commands, work types, needs, body parts, alerts, proper nouns and the colonist name pool — with a stable key beside each (924 entries):

- **Browse:** [`docs/wiki/index.md`](docs/wiki/index.md), one page per section.
- **Searchable single page:** [`docs/wiki/index.html`](docs/wiki/index.html) — open it from disk or host it anywhere; no build step.
- **Hosted snapshot:** <https://claude.ai/artifact/JsYRQk1vnza2wFWSNfpQwr>

The wiki is **generated** from the design CSVs (`docs/design/icon-keys.csv` and friends). Never edit the wiki files by hand: correct a name in the CSV and rebuild with `python3 tools/wiki/build_wiki.py`. See [`docs/wiki/README.md`](docs/wiki/README.md).

## What is in the game now

Everything below is on `main`. Some of it is fresh enough that the owner has not played it yet — that is called out in `CLAUDE.md`, not hidden here.

### World

- **A wooded meadow**, 120 × 120 × 16 cells by default (cells are 2.5 × 2.5 × 3.0 m), with a clearing at the start, streams and ponds, 3 m terrace risers, and rock, ore and sealed caverns underground. Small, Large and Huge (240 × 240 × 16) boards are also selectable; a ruined-city generator exists as a later map type.
- **Trees, bushes and forage**: four tree species that fell and topple, berry bushes you harvest and that regrow, loose stones and mushrooms lying on the ground.
- **Growing zones**: paint a zone, sow carrots, they grow in the daylight window, harvest and auto-resow.
- **Weather and seasons**: Clear, Cloudy, Rain and Storm roll through by season, with rain that wets the ground, streaks the screen, patters on roofs and keeps colonists and crops dry or watered depending on cover.
- **Temperature**: every enclosed room holds its own reading; the seasons and the day's own swing move it, warm air rises through stairwells, and cold can band mood, slow sleep and work, and in the harshest climate turn lethal.
- **Day/night cycle** with golden-hour grading; **ambient birds** (rook flocks, a buzzard) and **ambient butterflies** (glowing at night) as pure decoration.
- **The slice camera** cuts the world at any depth: at the surface every layer above is drawn solid; below it, one layer is x-rayed and the rest drawn. A **walls-down** view drops walls to a stump and hides the storeys stacked above the slice, so you can see who is indoors. Anything drawn solid is clickable; ghosts never are.
- **Water** is drawn as bodies with falls, streaks and foam; a one-cell stream can be **jumped** at a run instead of waded.

### Colonists

- Drawn from a pool of 29 modular characters (hair, beards, an issued uniform over the base body), appearance rolled per pawn from its own seed, portraits rendered once and cached.
- **Needs** (food, rest, joy), **mood** from thoughts, and **skills** that gain experience, level up with an on-screen toast, and set how fast a colonist works.
- A **home area**, automatically grown around the hearth, that colonists idle and eat within unless drafted or starving.
- Walk (diagonally), hop a one-block rise, climb ladders, jump a stream, sidestep crowds and trees, glance where they look, and kneel to computed work strokes (no animation pack contains work poses, so the axe/pick/hammer swings are generated).
- Claim beds and sleep lying down; slow and eventually collapse when starved, sleepless or too cold or hot.

### Work and economy

- A **priority-ordered job scan** keeps everyone employed: felling, mining, hauling, building, deconstructing, paving, growing, cooking, cleaning up refused goods, tending the hurt.
- **Building and digging**: walls, floors, doors, windows, pillars, beds, ladders and paving, in the materials the colony can gather. Support is solved bottom-up — remove what holds a span and it collapses, cascading down the layers. Construction can botch; a ladder needs its shaft left open through the floor above.
- **Storage**: paint-a-zone stockpiles and one-cell shelves (eight stacks apiece), a priority ladder, category/tri-state filters, and a store that refuses and clears out anything it does not accept.
- **Cooking**: a galley or a campfire takes standing bills, a cook turns raw food into meals (with a burn curve for an unwatched pan), and colonists eat the best tier available.
- **Power**: a wood-fired generator, lines that run anywhere (through walls, under floors), and an electric heater — a net with no power goes dark as a whole.
- A carried load rides the hands, up out of the lift's crouch and back down on the stow; piles on the ground are drawn at their size.

### Combat and defence

- **Draft and move**, melee and **ranged combat** (a pistol, 3D line of sight, weapon quality, a reach rule that switches a gun to a club up close).
- **Cover**: partial cover from terrain and **sandbags**, drawn bag by bag, with crouching, a hit-chance readout at the pointer, and cover that wears down under fire.
- **Health**: injuries land on one of six body regions, merge, bleed and are tended by a doctor (or, in a pinch, self-treated); pain, consciousness and mobility are derived from the damage; falls hurt.
- **Bandits and raids**: dressed, named raiders that steal or fight; a raid is a band of hostiles that arrives, gathers, probes and assaults the colony's hearth, breaking off once it takes enough losses.
- Buildings and doors can be fought over, damaged and destroyed; friendly fire and rescue-to-a-bed are modelled.

### Wildlife and animals

- Worlds **generate wildlife** (hog sounders, rats) that wander, shelter from rain and eventually leave; a separate framework spawns **tame-able animals** with their own species, gait and simple wandering mind.
- An Animals tab lists what is on the board; wildlife stays off the main orders bar.

### The interface

- A main screen with **seed entry and reroll**; **three-candidate colonist select** (name, age, occupation, skills) and a naming page; named **save/load** with an autosave.
- **F1–F5 panels**: Work (a priority grid with a schedule), Research (the placeholder tree), Inventory (what every store holds, with a jump-to-store), Storage (per-store filters and priorities), Animals, Health, and an Assign tab for home area and combat response.
- The **build palette**, an orders strip whose colours match on panel, cursor and ground mark, a paged and drag-reorderable roster, alerts with chimes, an events panel, and a **debug menu** for granting resources, spawning pawns, firing events and skipping time.
- A redesigned **settings window and title screen**, graphics presets from Low to Ultra, and a **selection highlight** that outlines the selected thing at its own edges instead of bracketing it.

### Events

- An **incident layer** with gated Defs and a saved, hashed ledger: a **supply drop** (meals fall from the sky) and **raids** (a band that gathers, probes and assaults), both currently fired from the debug menu rather than an automatic storyteller.

### Under the hood

- A **fixed-tick, single-threaded simulation** whose state hash is a first-class test: same seed, same hash; byte-stable saves (format 10); resume-equivalence and golden-master one-day runs; ten headless days survived on three seeds, including seven raids.
- Two test tiers gate every change: a **fast tier** with no Unity (roughly 1,800 Sim + 1,200 Hud tests in well under a minute, plus a ~50-test Long tier), and the **authoritative Unity tier** (EditMode and PlayMode, several thousand tests between them) — plus generated-content checks that keep the wiki, the CSVs and the HUD labels from disagreeing.
- A running player writes its own **performance trace** (frame p50/p95/p99, GPU/CPU split, every render and tick section) to `Logs/perf/`, readable with `tools/perf/trace.py`.

## Honest limitations

What the game does **not** have yet, so nobody has to guess:

- **No storyteller.** Nothing fires an event or a raid on its own; both are debug-menu only. The seams a scheduler needs (incident gates, a raid ledger, refire memory) are in and waiting.
- **Medicine has no supply chain.** Medical supplies exist as an item but nothing in the world produces them yet; a standing patient can still walk mid-treatment; and while extreme cold and heat slow a colonist badly, dying of exposure or of starvation is not yet modelled.
- **Going up is barely possible.** Stairs are the next building unit, and a hauler cannot climb a ladder, so materials cannot move between floors — multi-storey building is impractical today.
- **No fog of war.** A sealed cavern is visible if you scroll the layer down.
- **The colonist schedule is not enforced.** The Work tab's day grid is real and saved, but a colonist still sleeps and eats by need rather than by the assigned hour.
- **Known cosmetic faults.** A colonist who sleeps, or an item dropped, at the foot of a terrace step can be hidden inside the drawn bank façade. An animal beyond the 64-figure cap is not drawn at all.
- **Backing out of the in-game load screen loses the colony** — the world is torn down before the save list appears, so there is nothing to go back to.
- **Everything else on the roadmap** — factions, trade, research that does anything, a world map, a modding API — is catalogued with milestones in [`docs/design/03-systems-catalogue.md`](docs/design/03-systems-catalogue.md).
- **Single-player only, ever.** Design nothing for multiplayer; nothing here is built for it.
- **The art is not in this repository.** Licensed Synty packs live under the gitignored `Assets/Synty/`. A clone without them builds, runs and tests — and draws untextured primitives.
- **Performance is measured on dev hardware only**, mostly at 640 × 480, on an RTX 5070 Ti; the 2022 mid-range laptop target and full 4K play resolution are only partly measured.
- **Clicks are not integration-tested.** The PlayMode harness cannot press a button, so input wiring is proven only by a person playing.

## Documentation

[`docs/README.md`](docs/README.md) is the map of everything below, plus a short primer on the project's concepts. The short version:

| Read | For |
|---|---|
| [`docs/README.md`](docs/README.md) | the documentation map and a concepts primer |
| [`CLAUDE.md`](CLAUDE.md) | the project guide: working rules, current status track-by-track, and the index of which design doc owns which code |
| [`docs/brief.md`](docs/brief.md) | the governing brief — the decisions everything else descends from |
| [`docs/design/`](docs/design/) | one document per mechanic (world and layers, building, AI and jobs, combat, health, weather, UI, …) |
| [`docs/adr/`](docs/adr/) | short records of the irreversible decisions (engine, cell size, architecture, audio, …) |
| [`docs/journal.md`](docs/journal.md) | the narrative record: every decision, measurement and reversal, and why |
| [`docs/process.md`](docs/process.md), [`docs/lessons.md`](docs/lessons.md), [`docs/bug-patterns.md`](docs/bug-patterns.md) | how work moves; what has cost time before; the recurring bug shapes and the checks that catch them |
| [`docs/audit/`](docs/audit/), [`docs/plans/playtest-queue.md`](docs/plans/playtest-queue.md), [`docs/milestones/`](docs/milestones/) | the baseline audit; what is waiting on a person to play it; milestone reports |
| [`docs/setup/local-dev.md`](docs/setup/local-dev.md) | dev-machine setup, both machines, Windows notes included |
| [`docs/research/INDEX.md`](docs/research/INDEX.md) | the research files behind the design, indexed |

## Building, testing, playing

Prerequisites: **Unity 6000.3.x LTS** (URP), **.NET SDK 8** for the fast test tier, **Python 3.11+** for the content tooling (standard library only). Full setup: [`docs/setup/local-dev.md`](docs/setup/local-dev.md).

```sh
scripts/unity.sh open              # open the project in the editor
scripts/unity.sh build             # player build → Build/Win64/Odyssey.exe (~15 s)
scripts/test-fast.sh               # fast test tier, seconds, no Unity needed
scripts/unity.sh test editmode     # authoritative tier before committing
python3 tools/wiki/build_wiki.py --check   # content gates — must pass on a content change
python3 tools/wiki/emit_labels.py --check  # (both of them)
```

Smoke-test a player build with `Build/Win64/Odyssey.exe -odyssey-newgame`, which boots straight into a colony. CI runs the fast tier on every push and pull request, selecting further tiers by which paths a change can break (`tools/ci/tiers.py`); the Unity tier runs on a self-hosted Windows runner behind the `UNITY_RUNNER=1` repository variable. Work reaches `main` only through a pull request with both tiers green and a review.

## Asset licensing

Synty POLYGON packs (Sci-Fi City, Farm, Western Frontier, Battle Royale, Particle FX, ANIMATION Base Locomotion, Shops) are licensed content: they live only under `Assets/Synty/`, which is gitignored, are never committed, and the simulation and its tests never depend on them. The owner's source art sits outside `Assets/` in [`art-source/`](art-source/). Study other games' *mechanics* in a clean room; nothing copyrighted is copied into this repository.
