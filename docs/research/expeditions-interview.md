# Expeditions: travelling the planet to sites (owner interview)

**Phase:** Interview (feature-level, in the shape of `home-area-interview.md` and
`world-generation-interview.md`).
**Date:** 2026-09-26. **Branch:** `claude/compassionate-hypatia-cz8reg`. This is documents only: no
code was written before or under this file.
**Conducted by:** Claude Code. Nineteen questions in five rounds, asked after a read-only exploration
of the planet model, the session and tick architecture, the save format, the edge-exit paths and the
incident layer. Three capped research passes ran alongside.

The owner's brief:

> "this is purely for reference and I don't think it works well https://rimworldwiki.com/wiki/Caravan
> — But we want to be able to travel to other areas on map now we have world generation. Maybe we
> can consider how this mechanism could or would work as we want to be able to explore, could we
> even have sea generation across sea tiles for example with big vast oceans to travel potentially
> in a far bigger world area that works a little differently, can we have authored areas that we
> can travel around us that pop up with a situation or a random place to go and explore for
> colonists, rewards and questions. I want to focus on the exact mechanism to make the option of
> exploring / quests and how that could work - would be too much to have a split window (optionally)
> and move between two - it could limit us - what do you suggest. Explore, plan and research, come
> with validations - interview and clarify every detail"

**Read next:**

- `docs/design/64-expeditions.md`, the design these answers decide.
- `docs/plans/expeditions.md`, the units.
- The research:
  - `a-13-travel-and-caravans.md` (the reference)
  - `b-away-play.md` (the genre)
  - `b-voyages-and-situations.md` (the sea and site composition)

## 1. What the exploration found, put to the owner before the questions

- **The planet exists, but only before the game.** Design 59 generates a 128 × 64 hex planet wrapping
  east–west from the world seed in about 12 ms. The World screen picks a site from it, and
  `ReleaseWorldMap` frees it the moment a colony goes live. Design 59 §10 names *"a World tab in
  play, travel, caravans"* as M7 seams. There is no `Planet.Settlements` list, although the design
  mentions one.
- **One save is one board.** The header carries one seed, one size, one tick and the `SiteTile`, and
  it loads only into a world of the same seed and size (`SaveFormat.cs:364-368`).
- **The simulation can already hold two boards; the composition root cannot.**
  - `ColonyWorld.Build` makes a self-contained world, and `BanditSoakTests` ticks two in lockstep.
  - `OdysseyBootstrap.BuildSession` throws if a session exists (`:853`).
  - `PawnId`s restart at 1 in every `PawnRegistry` (`:26`), and `ColonistNames.Book` is keyed by
    them.
- **Nothing lets a colonist exist off the board.**
  - `PawnRegistry.Despawn` removes a pawn outright.
  - `Adopt` is the only way back in.
  - Animals, raiders and thieves already leave by the nearest edge (`JobSystem.EdgeTarget`), so
    walking off is solved; carrying a person through is not.
- **An idle board ticks in about 0.065 ms whatever its size** (design 28 §2). A Standard board is
  about 16 MiB of simulation, 4 MiB of render mirror and 24 ms to generate.
- **There is no choice UI anywhere in the game.** `BulletinModel` rows are dismissible, and
  `HudModal` serves only the start and leave prompts.

## 2. The answers

| # | Question | Answer |
|---|---|---|
| 1 | How is an away trip played? | **Journey told, site played** (recommended): an abstract token on the planet; the destination is a real board |
| 2 | What happens at home while a team is away? | **Runs fully, alerts pull me back** (recommended): both boards tick on one clock; only the viewed one is drawn |
| 3 | How do you watch two places? (the split-window question) | **One screen, switch + away strip** (recommended): board tabs, an away strip with faces, health, activity and Switch |
| 4 | How big a part is the sea? | **A later layer on the same planet** (recommended) |
| 5 | Where do destinations come from? | **Both** (recommended): fixed places under fog, found by travelling, and timed offers from the story |
| 6 | How is a site made? | **Authored recipes, random casting** (recommended) |
| 7 | What is worth travelling for? | **Salvage and loot; recruits; knowledge (research); the map itself**: all four |
| 8 | How much logistics at departure? | **Light** (recommended): pick people, tick what they carry, food auto-packed and shown as "enough for N days", one warning if short |
| 9 | How long is a trip to a nearby site? | **Hours to 2 days** (recommended): about 6 game hours a hex |
| 10 | What happens on the road? | **0–2 choice events; a fight opens a board** (recommended) |
| 11 | What remains after a site? | **Board discarded, site remembered** (recommended) |
| 12 | Can a site become an outpost? | **Later, recorded as a seam** (recommended) |
| 13 | How much of the planet is known at the start? | **A ring round home, travel reveals** (recommended): biomes always visible; sites within ~3 hexes known; an expedition reveals radius 1, a lone scout 2 |
| 14 | How big is a site board? | **The same as home, Standard 120 × 120 × 16**. The owner chose this over the recommended 64 × 64 |
| 15 | What happens on the unwatched board? | **Alert + pause, with a Go button** (recommended); pausing is a setting |
| 16 | What is the first slice? | **A round trip to a found site** (recommended) |
| 17 | What is the travelling group called? | **Expedition** (recommended) |
| 18 | What is "a far bigger world area" for the sea? | **Same planet, sea tiles cost differently** (recommended) |
| 19 | How is the planet opened in play? | **A World tab on a key, over the board** (recommended) |

## 3. Why the recommendations were recommended

- **Q1, journey told and site played.** It keeps the tactical game, which is what a colony sim is
  for, at the destination. It also spends no board on the empty hexes between. Playing every step
  on a real board costs a generation per hex. Telling it all as a story is the Dwarf Fortress
  missions model, which players call opaque ("the squad never returned").
- **Q2, home runs fully.** The determinism rule (one fixed-tick simulation, one hash) rules out a
  coarse home. A paused home removes the tension that makes leaving a decision. Running two full
  boards costs about 0.065 ms a tick at rest.
- **Q3, no split screen.** Every example found either failed or stayed niche:
  - Anno 1800 hides another session's alerts until you visit it.
  - Oxygen Not Included: Spaced Out's switching is called clunky.
  - Supreme Commander's dual view was loved by a few and ignored by most.
  - The praised pattern is Factorio's remote view: one screen, act anywhere, switch instantly.

  A split screen would also halve the frame budget and every panel's width. A watch-only inset stays
  possible later without changing the model.
- **Q5, both sources of destinations.** Found places reward exploring and never pressure. Offers make
  urgency and a trade against distance. Each covers the other's weakness (b-voyages-and-situations,
  Findings B).
- **Q6, authored recipes with random casting.** Wildermyth's roles-and-casting keeps written
  situations fresh because the cast changes. The reference's tagged site parts give combinations. A
  fully procedural site has no story; a fully hand-built one is the same every time.
- **Q8, light logistics.** The reference's commonest complaints are exactly the logistics: a slow
  formation, pawns loitering at the formation spot, micromanaging mass and food, and a dialog that
  cannot sort. Whole mods exist to strip them out.
- **Q9, hours to two days.** Long enough that home is felt to be alone, and short enough that a round
  trip fits a week and a season does not go by.
- **Q10, road events.** Travel with nothing in it is Sunless Sea's most repeated complaint. FTL's
  pace of about one event per jump is the good counter-example. Letting "fight" open a board reuses
  the site machinery rather than building a second one.
- **Q11, board discarded, site remembered.** Keeping every visited board grows the save and memory
  with every trip. Forgetting the site (the reference's quest maps) makes a revisit meaningless.
  Recording the state and rebuilding from the seed costs a few bytes a place.
- **Q15, pause with Go.** It answers Anno's hidden alerts and Oxygen Not Included's and Kenshi's
  unsupervised deaths directly.

## 4. Tensions the owner chose into

- **Standard-sized site boards (Q14).** Each live site is about 16 MiB of simulation, about 4 MiB of
  mirror and a 24 ms generation, against about 4 MiB and a few milliseconds at 64 × 64. It is more
  walking per visit, and much more room for a situation to be a place rather than an arena. The frame
  does not pay, because only one board is drawn. The cost to measure is the tick with colonists busy
  on both boards (design 64 §14).
- **Home keeps running (Q2).** A colony can be raided while half of it is away. That is the point,
  and it is why threat pacing counts only the colonists on the targeted board (design 64 §11).
- **Fog hides sites, not land (Q13).** The owner has already seen the planet on the World screen, so
  hiding the land would contradict it. Exploring pays in finds, not in terrain.

## 5. What this does not settle

- The **situation catalogue** beyond the first (the salvage cache with a hazard): which situations,
  their text, their roles. That belongs to each later unit with its own short interview.
- **Road events' content** and the choice modal's look (a Claude Design brief when EX12 starts).
- **Signals' cadence**, which needs the storyteller that does not exist (design 23 §8).
- **What knowledge is**: research is interface-only today (design 34), so a knowledge reward waits on
  research becoming simulation state.
- **The sea's numbers**: boat build cost, sea speed and the strain meter's shape.
- **Outposts and factions' settlements** (M7).
