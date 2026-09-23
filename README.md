# Odyssey

A prototype **colony sim in the RimWorld mould, in true 3D with discrete vertical layers**, set in a ruined sci-fi city. Unity 6.3 LTS (6000.3.x), URP, C#. The simulation is plain C# — single-threaded, deterministic, and testable with no Unity installed — and the board is a sliceable stack of layers you build up, dig down, and cut the camera through at any depth.

Odyssey is a work in progress by one owner and a bench of AI coding agents. It is **playable today as an early vertical slice**: a colony of five chops, mines, hauls, builds, eats and sleeps in a wooded valley. What is *not* there yet is listed just as honestly under [Limitations](#honest-limitations). The scale target — 250 × 250 × 40 cells, 50 colonists, 60 FPS at 3× on a 2022 mid-range laptop — is the destination, not the current board.

## The content wiki

Every named thing in the game — commodities, items, buildings, commands, work types, needs, body parts, alerts, proper nouns and the colonist name pool — with a stable key beside each (658 entries):

- **Browse:** [`docs/wiki/index.md`](docs/wiki/index.md), one page per section.
- **Searchable single page:** [`docs/wiki/index.html`](docs/wiki/index.html) — open it from disk or host it anywhere; no build step.
- **Hosted snapshot:** <https://claude.ai/artifact/JsYRQk1vnza2wFWSNfpQwr>

The wiki is **generated** from the design CSVs (`docs/design/icon-keys.csv` and friends). Never edit the wiki files by hand: correct a name in the CSV and rebuild with `python3 tools/wiki/build_wiki.py`. See [`docs/wiki/README.md`](docs/wiki/README.md).

## What is in the game now

Everything below is on `main` and playable.

**The world.** A 120 × 120 × 16-cell board (cells are 2.5 × 2.5 × 3.0 m) of wooded meadow with a clearing at the start, streams and ponds, 3 m terrace risers, and rock, ore and sealed caverns underground. A ruined-city generator also exists and is kept as a later map type. There is a day/night cycle with golden-hour grading, and water is drawn as bodies with falls, streaks and foam. The **slice camera** cuts the world at any depth: at the surface every layer above is drawn solid; below it, one layer is x-rayed and the rest drawn. Anything drawn solid is clickable; ghosts never are.

**Colonists.** Five per colony, with needs (food, rest, joy), mood from thoughts, and skills that set how fast they work. They are 61 Synty rigs recoloured by repainting atlas swatches — no modular bodies exist — with appearance rolled per pawn from its own seed, so the person on the setup card is the person who walks around. Portraits are the real character rendered once and cached. They walk (diagonally included), hop up one block, climb ladders, sidestep each other and trees, glance where they look, kneel to *computed* work strokes (no pack contains a work animation), claim beds and sleep lying down, and slow and collapse when starved or sleepless.

**Work.** A priority-ordered job scan keeps everyone employed: felling trees, mining, hauling to stockpiles, building, deconstructing, paving, eating, sleeping. Piles on the ground are drawn at their size; a carried load rides the hands, up out of the lift's crouch and back down on the stow.

**Building and digging.** Walls, floor slabs, paving, ladders and beds, in the materials the colony can gather. Support is solved bottom-up: take out what holds a span and it collapses, cascading down the layers. Construction can botch. A ladder needs its shaft left open through the floor above. Terrace steps are priced as slopes — the flats are walked, the ramp is climbed, at one speed each.

**Events.** An incident layer — gated Defs, a saved and hashed ledger, a skyfaller — with one event, the **supply drop**: ten to twenty meals fall from the sky and the colony hauls them in. It fires from the debug menu's Events tab and lands on the board with a chime; there is **no storyteller** yet, by decision.

**The interface.** A main screen with seed entry and reroll; three-candidate colonist select (name, age, occupation) and a naming page; the build palette (tools not yet built are drawn disabled); an orders strip whose colours are the same on panel, cursor and ground mark; a paged, drag-to-reorder roster; alerts with chimes; an events panel; a debug menu (grant food, wood or stone, spawn a pawn, developer overlay, fire an incident); audio faders; and named save/load.

**Under the hood.** A fixed-tick, single-threaded simulation whose state hash is a first-class test: same seed, same hash; byte-stable saves (format 6); resume-equivalence and golden-master one-day runs; ten headless days survived on three seeds. Two test tiers gate every change — roughly 1,300 fast-tier tests in seconds with no Unity, and the authoritative Unity tier at roughly 2,000 — plus generated-content checks that keep the wiki, the CSVs and the HUD labels from disagreeing.

## Honest limitations

What the game does **not** have yet, so nobody has to guess:

- **No storyteller.** Nothing fires on its own; the supply drop is debug-menu only. The seams a scheduler needs (incident gates, refire memory in the ledger) are in and waiting.
- **Nobody can be hurt.** There is no health model: no injuries, illness, death or combat. A colonist rides a collapsing floor down unharmed, and fall damage has a number and nothing to apply it to.
- **The weather is uniform.** Temperature is in (design 28): seasons, day and night, rooms that hold their air, a campfire to warm them, cold that slows and eventually drops a colonist, crops that wait for warmth. Weather, fire and light levels are still to come.
- **Food has no source.** Meals come from the starting kit or supply drops. No growing (a branch is in review), cooking, spoilage or seeds. Starving colonists collapse rather than die.
- **Going up is barely possible.** Stairs are the next unit, and a hauler cannot climb a ladder, so materials cannot be carried between floors — multi-storey building is impractical today.
- **No doors.** A functional-doors branch is in review; until it merges there is no room enclosure and every hut is open-fronted.
- **No fog of war.** A sealed cavern is visible if you scroll the layer down.
- **Known cosmetic faults.** A colonist who sleeps, or an item dropped, at the foot of a terrace step can be hidden inside the drawn bank façade. Swimming is a drawn pose only — deep water is not passable.
- **Backing out of the in-game load screen loses the colony** — the world is torn down before the save list appears, so there is nothing to go back to.
- **Everything else on the roadmap** — animals, health, combat, research, power, trade, factions, modding API — is catalogued with milestones in [`docs/design/03-systems-catalogue.md`](docs/design/03-systems-catalogue.md), not started.
- **Single-player only, ever.** Design nothing for multiplayer; nothing here is built for it.
- **The art is not in this repository.** Licensed Synty packs live under the gitignored `Assets/Synty/`. A clone without them builds, runs and tests — and draws untextured primitives.
- **Performance is measured on dev hardware only.** Frame time is comfortable on the RTX 5070 Ti dev box, but the 2022 mid-range laptop target has not been measured.
- **Clicks are not integration-tested.** The PlayMode harness cannot press a button, so input wiring is proven only by a person playing — which is why that line of work has had silent failures.

## Documentation

[`docs/README.md`](docs/README.md) is the map of everything below, plus a short primer on the project's concepts. The short version:

| Read | For |
|---|---|
| [`docs/README.md`](docs/README.md) | the documentation map and a concepts primer |
| [`CLAUDE.md`](CLAUDE.md) | the project guide: working rules, current status, and the index of which design doc owns which code |
| [`docs/brief.md`](docs/brief.md) | the governing brief — the decisions everything else descends from |
| [`docs/design/`](docs/design/) | one document per mechanic (world and layers, building, AI and jobs, UI, events, …) |
| [`docs/adr/`](docs/adr/) | ten short records of the irreversible decisions (engine, cell size, architecture, audio, …) |
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

Smoke-test a player build with `Build/Win64/Odyssey.exe -odyssey-newgame`, which boots straight into a colony. CI runs the fast tier on every push and pull request; the Unity tier runs on a self-hosted Windows runner behind the `UNITY_RUNNER=1` repository variable. Work reaches `main` only through a pull request with both tiers green and a review.

## Asset licensing

Synty POLYGON packs (Sci-Fi City, Farm, Western Frontier, Particle FX, ANIMATION Base Locomotion) are licensed content: they live only under `Assets/Synty/`, which is gitignored, are never committed, and the simulation and its tests never depend on them. The owner's source art sits outside `Assets/` in [`art-source/`](art-source/). Study other games' *mechanics* in a clean room; nothing copyrighted is copied into this repository.
