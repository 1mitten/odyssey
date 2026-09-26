# 64 — Expeditions: travelling the planet to sites

**Status:** Designed 2026-09-26. **Nothing is built.**
**Branch:** `claude/compassionate-hypatia-cz8reg`.

**Decided by:** `docs/research/expeditions-interview.md` (19 answers).

**Research:**

- `a-13-travel-and-caravans.md` (the reference)
- `b-away-play.md` (the genre)
- `b-voyages-and-situations.md` (the sea; sites with a situation)

**Units:** `docs/plans/expeditions.md`.

**Builds on:**

- design 59 (the planet)
- design 23 (incidents)
- design 55 (raids leave by an edge)
- design 30 (wildlife leave by an edge)
- design 28 (what a board costs)

**Clean room.** The reference's caravans were studied for mechanics and complaints only. Every name
here is our own: *expedition*, *place*, *situation*, *signal*, *chart*, *campaign*. None of them is
game content until the unit that shows it to a player. That unit adds the keys under the wiki rule.

## 1. The loop in one paragraph

1. The player presses the **World** key (§5) and sees the planet over the running game. Home is
   marked, known **places** within a few hexes are pinned, and the rest of the planet is under
   **fog** for sites (the land itself is always visible).
2. They pick a place, choose who goes and tick what they carry, and press **Set out**. Food is
   packed automatically.
3. The chosen colonists walk off the board edge that faces the destination and become an
   **expedition**: a token that crosses hexes at about 6 game hours a hex, camps at night, may meet
   a choice on the road, and reveals the fog as it goes.
4. On arrival, a **site board** the size of home is generated for the place's **situation** (for
   example, a salvage cache with a hazard), and the team walks on at the edge they came from.
5. **Home keeps running in full the whole time.** The player watches one board at a time: they
   switch with a tab, a key or an alert's **Go**, and an **away strip** shows the other group.
6. When the last colonist walks off the site, what happened is written onto the place, and the board
   is thrown away.
7. The team walks home with what it found.

## 2. Decisions (owner, 2026-09-26)

| Decision | Choice |
|---|---|
| Journey vs destination | The journey is told (a token on the planet); the site is played (a real board) |
| Home while away | Runs fully; every board on one clock; only the viewed board is drawn |
| Two places on screen | One screen: board tabs, an away strip, Switch. **No split screen, no picture-in-picture** |
| Destinations | Fixed places seeded under fog **and** timed **signals** (later unit) |
| Site content | Authored **situations**, cast at random from the site's seed |
| Rewards | Salvage, recruits, knowledge, the map itself |
| Departure | Pick people and what they carry; food auto-packed; no mass budget |
| Trip length | Hours to two days each way |
| Road | 0–2 choice events per leg; *fight* opens an encounter board |
| After a site | Board discarded; the place remembers its state |
| Outposts | A later seam |
| Fog | Sites hidden beyond ~3 hexes of home; travel reveals radius 1 (a scout 2) |
| Site board size | **Standard, 120 × 120 × 16**, as home (the owner's choice over 64 × 64) |
| Alerts from the unwatched board | Labelled by place; serious ones pause (a setting) with **Go** |
| First slice | A round trip to one found place |
| Name | **Expedition** (*caravan* stays the Cartage's trade convoy, `proper-nouns.csv`) |
| Sea | The same planet: ocean hexes by boat, costed differently (§13) |
| The planet in play | A World tab on a key, over the running board |

## 3. Alternatives, and why this one

| Model | Who does it | Why not (or why) |
|---|---|---|
| **Journey told, site played, home runs in full** | The reference; Oxygen Not Included: Spaced Out; XCOM's arrival | **Chosen.** Tactical play where it matters, no board spent on empty hexes, and the simulation already holds two boards |
| Everything abstract (text, dice, a report) | Dwarf Fortress missions; Frostpunk | Cheapest, but players call it opaque ("never returned"). It survives as the **road events** (§6d) |
| Home pauses while away | This War of Mine; XCOM; Against the Storm | Clean focus, but leaving costs nothing, so it is not a decision |
| Home simulated coarsely | Kenshi outside its squads' bubbles | Breaks one simulation and one hash (ADR 0005, determinism before threads). **Rejected** |
| Every hex a real board | None found | A generation per hex, and walking across empty maps. **Rejected** |
| Split screen / picture-in-picture | Supreme Commander (niche); Factorio mods (watching only) | No colony game found uses it as its main view. It halves the frame and every panel's width. **Rejected**, but a *watch-only* inset stays possible later without changing the model |

**Best practice taken from the genre** (`b-away-play.md`):

- Alerts are global and labelled; Anno 1800 hid them until you visited.
- A journey has a log and an ETA; Dwarf Fortress missions have none.
- Home survives being unwatched (§11); Oxygen Not Included and Kenshi punish looking away.
- Timed choices are never started on two boards at once; Against the Storm's timers stack.
- Travel has a decision in it; Sunless Sea's commonest complaint is sailing with nothing to do.

## 4. The campaign

A new engine-free layer, `Odyssey.Sim.Expeditions` (under `Assets/Odyssey/Sim/Expeditions/`).

### 4a. What it owns

`Campaign` owns:

- **the clock:** `Tick` and `GameSpeed`;
- **`Board[]`:** slot 0 is home, and live site boards follow in slot order;
- a shared **`PawnIdSource`** (§4c);
- **`Chart`:** one fog bit per planet tile, 8,192 bits;
- **`Place[]`:** tile, situation name, seed and state flags, saved whole (§12);
- **`Expedition[]`:** the parties on the road;
- its own intent bus and views, for the World tab and the away strip;
- the **`PlanetView`**, rebuilt from the world seed in about 12 ms whenever it is needed (design 59
  §3). It is never saved.

### 4b. One campaign tick

1. Drain the campaign's intents (form an expedition, pick a road choice).
2. `home.Tick()`, then each site board's `Tick()`, in slot order.
3. Collect each board's **departures** (§6b).
4. Advance each expedition on the road; adopt the arrivals into their boards.
5. `Tick++`.

**The invariant: every board's `CurrentTick` equals `Campaign.Tick`.** It is not a preference,
because pawns carry absolute ticks: a memory's expiry, the skill day, the next swing, the treatment
cooldown. A site board is built with `ColonyRequest.StartTick = Campaign.Tick`, which
`ColonyWorld.Build` already passes to `SimWorld.StartAtTick` (`ColonyWorld.cs:519`, `SimWorld.cs:411`).
That also keeps `StartingSkillsSystem` from re-rolling, since it rolls only at tick zero.

### 4c. Per board, or per campaign

| Per board | Per campaign |
|---|---|
| Weather and temperature, from the board's own tile (`ColonyWorld.ClimateFor`, `WetFor`) | Tick and game speed (today `SimWorld.GameSpeed`, unhashed, set by an intent) |
| Incidents, the ledger, raids, skyfallers | **Pawn ids** |
| Items and `ThingId`s, storage, beds, the home area and hearth | One shared `PawnContent` (`Pawn.Content` is readonly, so an adopted pawn must find the same instance) |
| Alerts and bulletins (§10) | The colonist cast seed (the home board's seed, as the bootstrap takes it today) |
| | Later: research, factions |

**Pawn ids must be unique across boards.** Names come from `(RollSeed, PawnId)` in
`ColonistNames.Book`, so a traveller who kept her id keeps her name. `PawnRegistry` gets a pluggable
`PawnIdSource`: `Spawn` asks `Next()` and `Adopt` calls `Observe()`, which replaces `_nextId` at
lines 26, 110, 446, 914 and 1010. The default is a local counter, which is today's behaviour exactly.
The campaign installs one shared counter.

**Splitting the id range per board was rejected**: boards are regenerated and would reuse their
range, so a returning recruit could collide.

## 5. The planet in play

- **The World tab**: a key (proposed **M**; rebindable) opens the planet full-screen over the board.
  The game keeps running behind it, and Escape returns.
  - It reuses the World screen's pieces: `WorldMapView` (zoom and pan), `WorldMapPainter`,
    `WorldMapGeometry` (the hex pick), `RegionNames`.
  - `ReleaseWorldMap` stays: the texture is built when the tab opens and released when it closes.
- **Drawn on the planet**:
  - home;
  - known places, pinned by kind;
  - expeditions as tokens with a route line and an ETA;
  - live site boards marked;
  - the fog as a dimming over tiles whose sites are unknown.
- **Places are seeded from the world seed** when the campaign is created: a density per biome and a
  minimum spacing, deterministic, so every new game on a seed has the same places. Each is stored in
  `Place[]` with its situation chosen by weight from the situations that tag its biome.
- **Fog (`Chart`)**:
  - At the start, every tile within 3 hexes of home is charted.
  - An expedition charts radius 1 of every hex it enters; a lone colonist (a scout) charts radius 2.
  - Charting a tile reveals any place on it. It never hides land: the player has already seen the
    whole planet on the World screen.

## 6. Expeditions

### 6a. Forming one

- A panel on the World tab, opened by selecting a known place and pressing **Send expedition**.
- **Who:** the colonists, with their skills and health. Downed, carried and bleeding colonists
  cannot be chosen (§6b).
- **What they carry:** ticks against the kinds in store. There is no mass budget; each colonist
  carries one stack (their hands) plus a weapon, as they do now.
- **Food:** packed automatically. It is enough nutrition for the round trip plus a day, taken from
  the stores nearest the departure edge, and shown as *"food for N days"*. There is one warning if
  the stores are short, and it never blocks departure.
- **Set out** issues one `FormExpedition` campaign intent. Nobody gathers at a formation spot: the
  commonest complaint about the reference (`a-13-travel-and-caravans.md` §5).

### 6b. Leaving a board

- A new job, **`Depart`**, walks each member to the board edge that faces the destination hex. It
  reuses `EdgeTarget` (`JobSystem.cs:1283`), which already takes animals and raiders off the board.
  Its target and the expedition id ride in the job's saved `TargetCell` and `WorkTicks`.
- At the edge, `Departure.Leave` runs, modelled on `Theft.Leave` (`Combat/Theft.cs:171`). It:
  1. packs the carried stack and the held weapon into cargo records (def, stack, quality) and
     despawns them;
  2. ends the job;
  3. calls `PawnRegistry.Despawn(pawn, DespawnReason.Departed)` and hands the `Pawn` object to the
     board's departures for the campaign to collect.
- **A departure is not a death.** `Despawn` today ends attacks, releases reservations, releases beds
  and puts the weapon down (`PawnRegistry.cs:420-437`). With `Departed`:
  - it **keeps her bed**;
  - it clears the combat target, the retaliation target, being carried, the draft, the path, the
    destination and any held reservations.
- The home-area setting (`Area`) is kept on the traveller, set to Anywhere on a site, and restored at
  home.
- Everything else travels untouched on the `Pawn` object: skills, health, memories, work priorities,
  schedule.
- **Refused:** a downed, carried or bleeding colonist cannot depart. The intent says why.

### 6c. On the road

**Speed.** 15,000 ticks a hex (6 h × 2,500), multiplied by the tile entered:

| Terrain | × |
|---|---|
| Flat, Rolling | 1 |
| Hilly | 1.5 |
| Mountainous | 2.5 |
| Marsh | 1.5 |
| Sheer, Ice, Ocean | impassable on foot |

The slowest member sets the pace: the lowest move-rate per mille, from the rates that already exist
(design 17). The route is the cheapest path over the hex grid (A* over `HexGrid.Neighbour`, wrapping
east–west).

**Camp.** The party stops 21:00–06:00. Rest is restored only in camp. The nearest places (1–3 hexes)
are therefore 6 hours to about a day and a half away, which is the owner's range.

**Needs on the road.**

- A member with no board has no `Context`, so the party updates hunger and rest itself on the Rare
  cadence.
- It uses the **same arithmetic** as a board: `NeedsSystem.UpdatePawn`'s rules (`NeedsSystem.cs:69`)
  are extracted into `NeedsRules`, so the rules have one owner.
- Food is eaten from the packed rations.
- Injuries heal and bleeds run through the same health rules at the same cadence. A bleeding
  colonist cannot set out, but a wound can open on the road (§6d), so the health rules must run
  here too.

**Journey log.** Every hex entered, every camp and every event is a line with its time. The World
tab shows the log and the ETA, which answers the Dwarf Fortress complaint.

### 6d. Road events (a later unit, EX12)

- Each leg (one hex) may roll an event: a strict ceiling of 2 per leg and an expected 0.3.
- The events come from a deck weighted by biome and hill band: a stranger, a storm, a find, tracks,
  a hostile band.
- Each event offers 2–3 choices, and a choice can cost time, food, health or goods, or yield them.
- A **hostile** event offers *fight / avoid / pay*. **Fight** builds an **encounter board**, which
  is a site board (§7) with a one-fragment situation, and the party walks on at its edge. The rest of
  the expedition waits on the planet until the survivors leave.
- **The choice modal is the first in the game** (there is no choice UI today, only `HudModal`'s
  prompts). A road choice pauses the game, like a serious alert. It never runs a timer while another
  board is also waiting on a choice.

### 6e. Arriving

1. The site board is built (§7).
2. Each member's cell is set to an edge cell on the side they came from.
3. `PawnRegistry.Adopt` (`:440`) re-attaches the pawn: it sets `Context`, the driver pool and the id
   floor.
4. The cargo is spawned and given back: `Items.Spawn` plus `PickUp`, and `WeaponHand.TakeUp`.
5. Walking home from a site is the same path in reverse. At home, the members walk in at the edge
   facing the hex they came from.

## 7. Situations: what a place is

### 7a. The data shape (a Def, authored in XML)

`SituationDef` holds:

- **`tags`**: biomes it may stand in, whether it needs the coast, sea-only (§13).
- **`weight`**, **`uniquePerWorld`**, **`minHexesFromHome`**.
- **`board`**: the board recipe.
  - The default is the site tile's own board: `SiteRules.BoardSeed(worldSeed, tile)`
    (`Sites.cs:190`), its hill band and its climate through `SiteBoard` and `SiteClimate`
    (design 59 §5–§7).
  - The owner fixed the size at **Standard, 120 × 120 × 16**; a Mountainous tile keeps its 24 layers.
- **`fragments`**: weighted pieces with a count range (a cache, a hazard, a stranded survivor, a
  sealed door, a camp). Each fragment is a small placer: a room or clearing stamped on the board by
  rules, never a hand-built map.
- **`roles`**: named slots cast at arrival from the site seed.
  - A *guard* is a hostile kind and count from the threat budget.
  - A *survivor* is a person rolled like a colonist (a recruit, EX15).
  - A *prize* is a reward bundle.
- **`threat`** and **`reward`** budgets:
  - Threat scales with the expedition's size and the distance from home. It reuses
    `IncidentParms.Points`, which raids already read (design 55).
  - Reward is threat × a multiplier. It is spent on salvage, a recruit, knowledge or a map reveal
    (EX15–EX17).
- **Hooks**:
  - `onArrive`: text with role names substituted, and an optional choice.
  - `onCleared`: every guard downed or gone, or the cache opened.
  - `onLeave`.

### 7b. A place's state

`Place.state` is a small set of flags plus a counter per fragment:

- discovered
- visited
- cleared
- looted
- door opened
- survivor recruited or refused
- times visited

A **revisit** regenerates the board from the same seed, then applies the state: a looted cache is
empty, a cleared camp is abandoned. It is the same place with its changes, at a few bytes a place.

### 7c. The first situation (EX7)

**A salvage cache with a hazard.**

- One fragment: a sealed cache of salvage (scrap metal, components, medical supplies), placed near the
  board's centre.
- One hazard, chosen by the seed: a sounder of hogs (design 30), or two or three bandits from the
  raid mix (design 55). Both use spawns that already exist.
- **Cleared** when the hazard is downed, fled or gone.
- **Looted** when the cache is emptied.
- No text choice: that arrives with EX12.

## 8. After a site

- When the **last colonist** leaves a site board, the campaign:
  1. writes the place's state (§7b);
  2. drops the board's slot;
  3. releases its render mirror;
  4. lets the garbage collector take the rest.
- **What the players left behind is lost** (dropped items, corpses, built walls). That is the cost
  of discarding boards, which the owner chose over saving every board. It is stated in the Form
  panel's help line so it is not a surprise.
- **Outposts** (keeping a board, a second colony) are the same machinery with the discard skipped.
  They are a later seam: nothing in this design blocks them.

## 9. Two views: switching and the away strip

- **One board is drawn at a time.** The rest tick in the background at their idle cost (§14).
- **Board tabs** sit at the top edge: *Colony*, then each live site by place name, then any encounter.
  A number key or `[` / `]` cycles through them.
- **The away strip** is a thin bar above the bottom for each group that is not on the viewed board:
  - portraits (live, as the roster's are);
  - a health tick for each;
  - the group's activity (*Searching*, *On the road, 4 h to Home*);
  - **Switch**.

  Expeditions on the road appear there too, with their ETA.
- **Per-board view memory**: camera, slice, selection and the open pane are kept per board and
  restored on the switch back.
- **The composition root splits in two**:
  - The simulation half: `BuildHome` / `TeardownCampaign`.
  - The drawing half: `BuildView(board)` / `TeardownView`.

  Together these replace `BuildSession` / `TeardownSession` (`OdysseyBootstrap.cs:842`, `:4551`). The
  single-session fields `_world`, `_grid`, `_pawns`, `_colony`, `_model` and `_renderer` become
  accessors onto the focused board.
- **Every direct tick of `_world` goes through the campaign instead**: `Update`'s loop, the tick on a
  speed change, `DebugSkipTicks`, `DebugSkipToMorning` and `DebugSkipToNight`, and `RefreshAfterLoad`
  (otherwise home runs one tick ahead of the sites).
- **One `WorldRenderModel` is kept per live board**, about 4 MiB each at Standard. The mirror is
  fixed when the board is built (`ColonyRequest.Mirror`, `ColonyWorld.cs:505`), so it cannot be
  rebuilt on a switch. The **renderer and the directors are rebuilt** on a switch.
- **Directors holding per-board state, rebuilt on a switch**:
  - figures, corpses, blood, projectiles, doors, fires;
  - weather, birds, butterflies, clouds;
  - audio (the terrain mirror), power lines, the home edge, the hearth mark, floaters;
  - combat feedback, the crowd index, demolitions;
  - the drafted-last-frame set, the ring places, the shot shooter;
  - `HudDirectors` (its slice depth is the board's layer count: 24 on a Mountainous site);
  - the camera rig's surface layer.
- **A separate `FocusChanged` event**, not `SessionChanged`. The latter resets the HUD and releases
  the planet (`HudShell.Start.cs:1013-1060`). A switch must not clear `ColonistNames.Book` or the
  portrait cache.
- **The switch cost is estimated at 130–180 ms** (a third of the 380–540 ms session build is
  `PrimeAll`, design 38). It is drawn behind a short veil, the wake's (design 56) without the dream.
  **It is to be measured, not trusted** (§14).

## 10. Alerts across boards

- `AlertModel` keeps state between refreshes: sustain timers, the starving and breaking sets, latches
  (`AlertModel.cs`). `BulletinModel` tracks the highest ledger entry seen. Both are refreshed today
  from the one snapshot (`HudShell.Panels.cs:1192`, `:1273`).
- So there is **one `AlertModel` and one `BulletinModel` per live board**, each refreshed against its
  own board's `Views.Current`, which every board publishes every tick whether it is drawn or not.
- They are **merged** into one list of *(board, place name, row)*, drawn with the place as a prefix
  (*"Colony · Raid"*). Dismissal keys become *(board slot, key)*.
- **Danger** rows and a raid bulletin from an unwatched board **pause the game** and show a modal with
  **Go**. Go switches to that board and jumps to the alert's cell. Pausing is a setting under
  Settings → Gameplay, on by default. Chimes are merged, so two boards never double a sound.

## 11. Threat, and the end of the game

- **A raid's size counts only the colonists on the board it targets.** A colony that sent half its
  people away is raided as the half it has. This is the fairness rule the genre's players ask for.
  **It is already true, so keep it**: `RaidWorker` sizes a band from `ctx.Pawns`, the board's own
  registry (`RaidWorker.cs:224`). A test pins it, so a later storyteller reading a campaign-wide
  headcount fails loudly.
- **No incident targets a site board** unless its situation asks for one.
- **There is no game over today** (nothing in the code ends a colony). When one is built, it must
  count every colonist in the campaign: home, on the road and on sites. An empty home with a team on
  the road is not a loss.
- **Home survives being unwatched** through what already exists: the undrafted response (fight back,
  defend, flee; design 33 §18), the home area (design 43) and the pause on alert (§10).

## 12. Save and hash

- **Format 11 → 12.** The outer file stays the **home board's save**, header unchanged. A format-11
  save loads as a campaign with no expeditions.
- **Why bump at all**: a format-11 build would skip an unknown section in silence and lose everyone
  on an expedition. A bump makes the older build refuse instead.
- **A new section, `odyssey.campaign`**, appended to the list the save is written from. It holds:
  - the id counter, the board-slot counter, the speed;
  - the chart bits;
  - the places, saved **whole** (tile, situation name, seed, state), so retuning the planet
    generator cannot move a discovered place. This is the `SiteTile` rule of design 59 §8.
  - the expeditions: route, progress, rations, log, cargo, and members through a **portable pawn
    codec**;
  - each live site board as a **nested, complete board save**: its own header and view state, about
    137 KB at Standard (design 28 §2).
- **The portable pawn codec** exists because a pawn's state is spread over seven sections: the
  registry, the pawn seed, the pawn kind, combat, health, assign and wildlife. It writes and reads
  one pawn whole, and it is the same codec a nested board uses for its own pawns.
- **Loading**:
  1. Build home and load it; the campaign section fills a staging object.
  2. For each live site board, build from its place and tile recipe and load its blob. The seed and
     size check (`SaveFormat.cs:364-368`) guards each board.
  3. Assert that every board's tick equals the campaign's.
- **Hash**:
  - `CampaignHash` = H(campaign state, home hash, then each site board's hash in slot order).
  - **The home board's own `ComputeStateHash` is untouched.** Goldens hash a `ColonyWorld` built
    directly, and the id source's default is today's counter.
  - Any new pawn field hashes **nothing at its default**, by the flag-bit pattern
    `Pawn.ContributeTo` already uses (`Pawn.cs:1132`).
  - New jobs are sparse in the job hash.
  - **No golden moves** until a golden runs an expedition. The one deliberate change is the content
    fingerprint for the new job Def.

## 13. Seams (recorded, not designed here)

- **Signals (EX14)**: a timed offer (*"a distress call from the north"*) that creates a place with a
  deadline.
  - It is an incident worker whose `TryExecute` adds a place and a bulletin, going through
    `Incidents.TryFire` like every other incident (design 23 §8). There is no second door.
  - Its cadence waits on the storyteller.
- **Recruits (EX15)**: a *survivor* role rolled like a colonist, joining on a choice.
- **Knowledge (EX16)**: salvaged tech that advances or unlocks research. Research must first become
  simulation state (today it is interface-only, design 34).
- **Map reveal (EX17)**: a reward that charts a radius, or points at another place. This is how one
  place leads to the next.
- **Outposts**: keep a site board instead of discarding it (§8).
- **The sea.** The same planet, with ocean hexes costed differently, not a second map:
  - A **boat** is built at a coastal tile's edge. `SiteTile.Coastal` is already recorded, and a sea
    edge on the board is the generator work design 59 §10 names.
  - Ocean hexes are crossable **only by boat**, and quicker than land (a proposed 0.5×).
  - A voyage carries a **strain** meter that rises out of sight of land and falls in port or at an
    island. It is the one resource that makes distance matter, with food. (Sunless Sea's lesson:
    without a decision, a voyage is dead time.)
  - An **ocean event deck** at 0–2 per leg, weighted by region.
  - **Islands** are coastal situations tagged *sea-only*, charted only by a boat. Some need an item
    found at another site, which is how voyages link up.
  - A **bigger planet** was considered and not chosen: land places would drift further apart and the
    World map's memory would grow.
- **Factions' settlements and the Cartage's caravans (M7)**: places of a settlement kind, visited
  without a board, for trade.
- **A watch-only inset of the other board** (§3): possible later, and not planned.

## 14. Measurements owed (each by the unit that builds the piece)

| What | Arm | Expected |
|---|---|---|
| Tick with a second Standard board, at rest and with 3 colonists working the site | a `TickBenchmarkTests` arm | about 0.065 ms at rest; the working figure is the real question |
| Memory of a second board | a `BoardMemoryTests` arm | about 16 MiB simulation and 4 MiB mirror |
| Site generation | the generation arm | about 24 ms (design 28) |
| Frame with a hidden second board | `FrameTimeTests` | identical to the one-board baseline |
| Switch time | a PlayMode arm | estimated 130–180 ms |
| Save and load with a live site board | the save arms | about +137 KB, and a load time |
