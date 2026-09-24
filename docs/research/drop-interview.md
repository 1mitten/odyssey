# The drop — owner interview

**Phase:** Interview (feature-level, in the shape of `meadow-interview.md`). **Date:** 2026-09-24.
**Branch:** `claude/intelligent-faraday-1660wy` (documents only; no code was written before or under
this file). **Conducted by:** Claude Code, after a read-only exploration of the start flow, the
skyfaller, the sky, the camera, the surround and the Synty inventory.

The owner's brief, verbatim: *"I want you to understand how we could do this every game — spawns a
random world map that has a series of 'tiles'; the player has a cinematic sequence of dropping from
a parachute (I can apply a synty asset) from very high in the sky and the player can control the
player dropping to somewhere on that map (random drop) but you guide into a direction. The idea is
that you are free falling through clouds (can we use volumetric toon clouds) — there are several
layers of clouds (like Zelda Tears of the Kingdom) where you are falling through layers but you can't
see the ground really (or the map) as you swing into the map — you get locked into a tile and then
you plummet towards the centre. Can you interview me for every detail and clarify what I'm proposing
here."*

**Read next, once answered:** the design this decides (a new `docs/design/NN-the-drop.md`), and the
research it will ask for (clouds in URP, the Battle Royale parachute, steering models).

## 1. What the exploration found, put to the owner before the first question

**Nothing of this exists yet, and three things it leans on are thinner than they look.**

- **There is no cloud anywhere in the project.** The sky is a flat painted gradient
  (`Odyssey/GradientSky`, `Assets/Settings/OdysseySky.mat`): overhead, horizon and below-horizon
  colours, no sun disc, no haze, no clouds, chosen over Unity's procedural sky on purpose. The only
  trace of a cloud is a planned weather row in the wiki (*Cloudy*, no art) and the campfire's smoke.
  URP has no volumetric cloud system of its own; the renderer carries two features (ambient
  occlusion and the ink outline) and one real render-graph pass, `OutlineFeature`, which is the
  precedent for adding another. Banded toon lighting was **rejected** on 2026-09-15 (`d-09`), which
  bears on how "toon" the clouds may be.
- **The packs do ship clouds.** Farm, Sci-Fi City, Generic and the Meadow's `PNB_Core` each carry
  low-poly cloud meshes — `SM_Env_Cloud_01–05` at 3–7 m, `SM_Gen_Env_Cloud_01–03` at ~45 m, and two
  `Cloud_Ring` prefabs at 280–345 m across (`docs/research/synty-inventory.csv`). They are the
  on-style candidate; nothing has ever drawn one.
- **The camera cannot get up there.** `SliceCameraRig` orbits at 10–160 m and pitches 20–80°, so
  its greatest height is about 158 m; the far clip plane is 1,800 m; `ApplyTransform` rebuilds the
  pose every frame from focus, pitch, yaw and distance, so a scripted move must go *through* the rig
  or pause it. The only scripted moves today are the instant `Bind`, `Frame()`, `FocusOn` (the
  opening zoom 48 → 32 m in under a second), `RestorePose` on load, and the sideways glide the
  Events row uses (`CameraDirector.JumpTo`). Timeline is installed and unused; Cinemachine is not
  installed. **A fall from kilometres up is a second camera.**
- **From 1,000 m the board would be fog.** Fog is exponential-squared on depth, tuned for a 48°
  camera at 48 m (1% at 50 m, a third at the rim, 97% by 900 m); at 1,000 m it covers ~94% at noon
  and ~99.9% at dawn or dusk, arithmetic from the code and not a rendered test. The surround is
  four rings of ground reaching 1,220 m beyond the board edge (a Standard world is ~2.74 km across
  in all), with trees to 900 m and hills to 50 m; there is no modelled horizon — the land ends in
  fog. Straight down from 1,000 m a Standard board fills about 40% of the frame height. So the
  drop's own sky, fog and horizon are its own to draw, and the daylight cycle — which writes the
  global sun, ambient and fog every frame and forces the fog colour to the horizon's — must be
  borrowed and handed back, the way `PortraitStudio` does it.
- **The skyfaller is the nearest thing to a falling colonist, and it is a simulation object.** The
  supply drop launches at a tick and lands at a tick (`fallTicks` 360, six seconds), saved and hashed;
  the presentation draws the item itself from `FallArc.DropHeight` 120 m at one speed, with a pad on
  the landing cell, and freezes it on pause. Its landing sound is named and plays nothing. **The
  drop proposed here is the other way round** — presentation with two inputs to the simulation —
  and should not be built on the skyfaller.
- **There is no loading screen.** *Start* calls `BuildSession` synchronously: worldgen, lighting,
  sound, `Bind`, `FocusOn`, then `PrimeAll` meshes every chunk (*"for the loading screen and nothing
  else"*), and the menu freezes until it is done — 14.7 s measured once on Huge. Then a hard cut
  from the starfield to the game; the only transition is the 4 s audio cross. **The cloud phase is
  the loading screen this project has never had**, if the tile is known before generation starts.
- **A game starts at noon** (`startHour = 12`), which decides what light the fall ends in.
- **The start flow today** is title → *New game* (draws a seed, deals three colonists) → one
  full-screen setup page (colony name, seed typed or rerolled, board size, three candidates with
  Keep and Reroll, click-to-rename; **no scenario and no board type**) → *Start* → `BuildSession`.
  The colonists do not arrive: they exist at tick zero, placed by `ColonyScenario.Place` in a
  spiral round the **start cell**, which `NaturalStartPass` picks with no randomness — the flattest,
  most tree-free 5 × 5 square nearest the middle of the board. The kit, the wreckage (fifteen cells
  off) and the wildlife's clearing margin are all laid out round it. **The "centre of the tile" the
  proposal plummets to already exists**, and it is deterministic.
- **One seed makes one board, and there is one climate.** `Climate_Temperate` is hard-coded in
  `WorldContent`; **Wash, Glare and Rime are its three seasons**, not three climates, though the Def
  is commented *one per map type* and nothing yet chooses between two. So the variety a tile could
  carry today is thinner than it sounds: board size, the wooded or bare recipe, the seed. A second
  climate is a Defs unit; a second terrain flavour is a worldgen unit.
- **The save has no world-setup section.** The header carries the seed, the size, the tick and a
  recipe (map, scenario, name, day, barren, wooded); colonist seeds are separate (`Pawn.RollSeed`)
  and hashed. A world map of tiles would be a new section beside them. The seed is shown nowhere in
  the game after the setup page.
- **The packs have no parachute, glider, pod, plane or balloon** — nothing across the eight
  inventoried packs. *Battle Royale* was imported after the inventory was taken and is not in it,
  and `Assets/Synty` is absent on this container, so whether it ships a parachute is the owner's to
  say. The packs do have skydomes (`SM_Env_Skydome_01`), smoke trails, cartoon jump puffs, drones,
  hover vehicles and a landing pad; Base Locomotion has **unused fall and landing clips** (`InAir
  fall short/large`, `land soft/medium/hard`), the first animation the project has found in a pack
  for anything but walking. No pack has a skydive or a hang under a canopy — those would be
  computed poses, the fourth and fifth of their kind after `SleepPose`, `SwimPose` and `ClimbPose`.
- **The menu's modality assumes a world either exists or does not** (design 17 §5b: the menu shows
  iff there is no live session). A fall during which the world is *being built* is a third state
  nothing is designed for, and it is the same restructure the in-game load screen is waiting on.
- **The playtest queue is long.** Its header says 58 open rows on 2026-09-22; a count today finds
  104 top-level rows under *Open*, and its own rule reads: *when more than about ten rows are open,
  the next session takes a fix or a measurement, not a new feature, unless the owner says
  otherwise.* This interview is the owner saying otherwise, or not — question 24.
- **The wiki already holds two unbuilt bulletins** for exactly this moment: `ui.bulletin.crash`
  (*"Something has come down nearby"*) and `ui.bulletin.arrival`. The colony's old default name was
  *Landfall*. Design 23 §8 defers *"a pod that opens, debris to haul — a second skyfaller kind."*

## 2. The proposal, restated

As read, the proposal is **four things**, and they can be built and judged separately:

1. **A world of tiles.** A new game makes a world map rather than one board: a grid of tiles, one
   of which becomes the colony. Today a seed makes exactly one board (`ColonyWorld.DefFor`), of
   a size picked on the setup page, under the one climate there is, and nothing outside it exists.
   The brief puts a world map in **M7** (*research, factions, trade and world map — scope lightly;
   late roadmap*), so this pulls a light form of it forward, the way temperature pulled M4's core
   forward.
2. **A steered fall.** Between the setup screens and the first frame of play, a cinematic: from
   very high, through several cloud layers that hide the ground, with the player nudging a
   direction. It is presentation and never touches a cell, a save or the hash — **except for two
   inputs it hands the simulation before tick zero**: which tile was landed (so which seed, climate
   and board are generated) and where on it the colonists stand.
3. **Lock-in and the plummet.** Below the last layer the fall locks to one tile and drops to its
   centre. Nothing is steered after that; this is the reveal and the hand-over into the slice
   camera.
4. **Toon clouds.** A cloud look the game does not have — nothing draws a cloud today, in the sky
   or on the board — and which Zelda TotK is the reference for. URP has no volumetric cloud
   system of its own, so this is either the packs' low-poly cloud meshes, a ray-marched pass of our
   own, or layered sheets; §4 ranks them.

## 3. The questions

Each carries the assumption the design will run on if it is not answered, and why it matters. Answer
only where the assumption is wrong.

### A. What it is, and where it sits

| # | Question | Assumption | Why it matters |
|---|---|---|---|
| 1 | **Who is falling?** The three colonists chosen on the select screen, each under a chute, with the camera on the group? One pod or capsule carrying all of them? Or a single "player" figure who is not a colonist? | The colonists themselves, each under a chute, the camera following them as a group. | Decides whether the figure rig needs a hanging pose (computed, like `WorkSwing` — no pack has a clip), whether the select screen must come *before* the drop, and whether the landing spreads them or stacks them. |
| 2 | **Every new game — and a load?** Does a loaded colony ever replay it? Is it skippable, from the first time, and how (hold a key)? | New game only; a load goes straight in; hold Space or Escape to skip, always, and skipping lands you on the same tile the fall was heading for. | A cinematic that cannot be skipped is the first thing a second playthrough resents; and a skip must still settle the tile. |
| 3 | **What happens to the existing setup page** — colony name, seed, board size, the three candidates with Keep and Reroll? Does it stay before the drop, does the drop replace any of it, or does the drop *become* the world choice (no seed typed)? | It stays as it is, and the drop replaces only the wait between *Start* and the first frame. The seed makes the world map; the tile is a second recorded choice. | The seed is shareable and typed today (design 19), and *"hunting for a map must not cost you a colonist you liked"* was the owner's own rule. If the drop is the only way to choose a world, "same seed → same colony" breaks unless the tile is recorded too. |
| 4 | **Is the world map ever seen as a map** — a screen, a minimap during the fall, a page in the Almanac afterwards — or only as the ground you fall towards? | Never as a map in this unit; only from the air. A map screen is M7's. | A map screen is a panel, a Hud model and a wiki namespace; the ground-from-the-air is drawing. Very different sizes of work. |

### B. The world and its tiles

| # | Question | Assumption | Why it matters |
|---|---|---|---|
| 5 | **What is a tile?** One whole board of the chosen size (so the world is, say, 5 × 5 possible boards and only the landed one is ever generated)? Or one board cut into a grid of landing tiles, all of it simulated? | A tile is one whole board. Only the landed tile is generated and simulated; the rest exist as a seed, a climate and a look. | The other reading multiplies the simulated world by the tile count, and the scale target is one board of 250 × 250 × 40. |
| 6 | **How many tiles, in what shape, and what varies between them?** Square grid or hexes; 3 × 3 or 8 × 8; and per tile: the seed only; the wooded or bare recipe; a *second climate* (there is one today, `Climate_Temperate`, whose seasons are Wash, Glare and Rime — a cold or a hot climate is a Defs unit); a terrain flavour (meadow, rock, ruin — only the wooded meadow exists); richness of ore or wood? | A square grid about 5 × 5. Each tile carries its own seed and one of a few *looks* the generator already has parameters for — how wooded, how much water, how much rock — and the world map records a climate slot so a second climate can fill it later. Board size stays the setup page's, for the whole world. | Variety is what makes steering a choice, but almost every axis is a unit of its own. The seed and the generator's existing knobs are the free ones; a second climate is the first cheap real difference. |
| 7 | **What does the player see of a tile before committing?** Nothing (a blind drop, the steering is flavour); the ground's colour through gaps in the last layer; a one-word label as you cross it (*Woodland*, *Rock*); a compass to the nearest of each kind? | The ground's tint through gaps in the last layer, and a one-word label on the HUD as the fall crosses into a tile — enough to steer *towards* woodland on purpose. | "You can't see the ground really" and "you guide into a direction" pull against each other. A wholly blind choice is not a choice, and the player will ask what the steering was for. |
| 8 | **Are the other tiles kept?** Saved as a record (seed, climate, which you hold) so M7's travel, trade and world map can read it later — or discarded on landing? | Kept: a small saved section, never simulated, never hashed. | Keeping it costs a save section now and saves rebuilding the world map later; discarding it means a second world-map design in M7. |
| 9 | **Does a typed seed still reproduce a colony?** Same seed + same tile → same board. Is the tile shown beside the seed on the setup summary and in the save's name row? | Yes: the seed makes the world map; the tile index is recorded with it; both shown. | Determinism is a gate. The fall's steering is player input and *must not* be re-derived from anything but the recorded tile. |

### C. The fall, step by step

| # | Question | Assumption | Why it matters |
|---|---|---|---|
| 10 | **How long?** Total seconds, and roughly per phase: the high fall above the clouds, the layers, the break-out, the locked plummet, the canopy and landing. | 20–30 s in all: ~4 above, ~12 through three layers, ~5 locked plummet, ~4 under canopy to the ground. | The cloud phase is where the board is generated and primed (§4, question 23), so it has a floor; the whole has a ceiling before it is a chore. |
| 11 | **The camera.** Third person, behind and above the falling figure (TotK); straight down like a bomb-sight; or first person? And at the end — does it sweep into the ordinary play framing over the landing, or cut? | Third person behind and above, tilting to look down as the last layer breaks; then one continuous move into the slice camera's default framing over the clearing. | The slice rig reaches 160 m and pitches to 48°; a fall from kilometres up is a second camera, and the hand-over is a scripted move the project has not got yet. |
| 12 | **The controls.** Keys, mouse, both? How much authority — enough to reach any tile from where you start, or only the neighbours of a random one? A dive to fall faster (TotK)? Gamepad? | WASD and mouse drift both; authority to cross about two tiles in the cloud phase; no dive; keyboard and mouse only. | Authority sets whether the drop is *random with a nudge* (the brief's words) or *chosen*. Two tiles of reach from a random start is the former. |
| 13 | **Lock-in.** What locks the tile — passing below the last layer, a timer, or the player pulling the chute? Once locked, is the landing point always the tile's centre, or does your position inside the tile carry through? | Altitude: crossing under the last layer locks whichever tile is beneath. The landing point is always the tile's start clearing — worldgen already makes one, and the kit is laid out around it. | "Plummet towards the centre" reads as the centre being fixed. If position carries, the clearing must be made where you land, which reaches into worldgen. |
| 14 | **The parachute.** Does the canopy open at lock-in (so the plummet is a fast canopy descent) or only in the last seconds (so the plummet is freefall and the canopy is the last beat)? | Freefall until ~30 m above the clearing, then the canopy, a short swing, and feet on the ground. | Decides which pose is on screen for most of the sequence, and which Synty piece is drawn when. |
| 15 | **The landing.** Soft, no consequence? Do the chutes vanish or lie on the ground as an item (cloth)? Does the starting kit — 36 meals, 150 stone, 150 wood, 5 beds — arrive by chute as well (the supply-drop skyfaller exists), or is it on the ground as today? | Soft; chutes vanish; the kit is already on the ground, unchanged. | No health model exists, so damage has nothing to apply to. Kit-by-chute is a second unit that reuses the supply drop. |
| 16 | **A mark of the landing.** An Events-panel row (*"The colony landed"*), the existing `ui.bulletin.crash` bulletin (*"Something has come down nearby"*, no art), a wreck or pod on the ground, nothing? | One Events row and nothing on the ground. | Anything named is wiki content in the same commit; anything on the ground is a cell, an item and the hash. |

### D. The look

| # | Question | Assumption | Why it matters |
|---|---|---|---|
| 17 | **Which clouds?** (a) The packs' low-poly cloud meshes — `SM_Env_Cloud_01–05` and the 300 m `Cloud_Ring` are already imported — instanced, with a stepped toon shader of our own; (b) a ray-marched volumetric pass of our own, the "volumetric toon" look proper; (c) layered sheets with a toon ramp, cheap and flat. Or a bought asset? | **(a)** for the layers you fall through and **(c)** for the far blanket; (b) measured only if (a) fails the look at first sight. Nothing bought. | (b) at 3840 × 2160 is the single most expensive thing the renderer would own, and it would run only for twenty seconds a game. (a) is on-style and cheap. |
| 18 | **The layers.** How many, how thick, how much gap? TotK is one deep sea; "several layers" suggests two to four with air between. | Three: a thick top sea, a broken middle, a thin last one with gaps the ground shows through. | The last layer's gaps are question 7's answer made visible; the count sets the sequence's rhythm. |
| 19 | **Time of day.** A game starts at noon today. Does the fall happen at noon too, or at dawn so it goes through a sunrise and the colony's first day is whole? Does a tile's look tint the clouds beneath it (grey over rock, gold over meadow) so the choice can be read from the sky itself? | Dawn, and the clock set so the colony lands at the hour the fall ends; tinted below the last layer. | The daylight cycle owns the global sky, ambient and fog; the drop must borrow it the way `PortraitStudio` does, and hand it back. Moving the start hour touches every golden. |
| 20 | **The parachute and the figure.** No inventoried pack has a parachute, glider or pod — which Synty piece do you mean (Battle Royale is not inventoried; does it ship one)? And the colonist: Base Locomotion has *unused* fall and landing clips (`InAir fall`, `land soft/medium/hard`) — use them for the freefall and the touchdown, with a computed hang under the canopy? | Battle Royale's parachute if it has one, else a Blender piece under `Assets/Art/Custom`; the pack's fall clip in freefall, a computed hang under the canopy, the pack's land clip on touchdown. | The fall clips are the first pack animation found for anything but walking; the hang is a computed pose, the project's pattern (`SleepPose`, `SwimPose`, `ClimbPose`), and a unit of its own. |
| 21 | **Sound.** The title bed carries into the fall and the game bed crosses in at the break-out (the §12 hand-over pattern)? Wind; the canopy's crack; a stinger at lock-in? | Yes to the cross; wind rising with speed; one crack; no stinger. | The audio hand-over exists and is tested; a new bed does not. |
| 22 | **Performance bar.** Same as the Meadow's — 60 fps at 3840 × 2160 on Ultra, 60 at 1080p on the laptop — or does a cinematic get more room? | The same bar, but the drop may spend the whole frame: no board is drawn while the clouds hide it. | A cloud pass measured at 640 × 480 says nothing; the 4K number is the gate, as it was for grass. |

### E. Fit and priority

| # | Question | Assumption | Why it matters |
|---|---|---|---|
| 23 | **Is the fall the loading screen?** Generate and prime the board during the cloud phase — the tile is known at lock-in, and the ground seen at break-out is the real board? | Yes. Lock-in triggers `BuildSession` for that tile; the clouds must last at least as long as generation and `PrimeAll`. | This is the strongest reason to build it: a load that has been a bare wait becomes the game's first scene, and no frame hitches because the board arrives whole behind cloud. |
| 24 | **When?** The playtest queue's header says 58 open rows and a count today finds 104; its own rule is that above about ten, the next session takes a fix or a measurement, *unless the owner says otherwise*. Several branches are in review. Is this next, after the current batch merges, or a design now and a build later? | Design now (this interview and its design doc), build after the current batch merges. | The queue's rule, and *fix issues before features* (owner, 2026-09-15). Saying otherwise here is enough. |
| 25 | **What first?** The cloud look alone in a probe scene (a shot), the steering with placeholder clouds, or the whole thing end to end? | The cloud look, as a photographed probe, because it is the part that can fail on sight; then the world of tiles (sim, tested headless); then the fall. | The look is the risk; the tiles are the thing every later system reads; the fall is the join. |

## 4. Best practice, alternatives, and how it would fit

### 4a. Does it already exist?

No part of it does. The nearest things: the brief's **M7 world map** (a planetary view of
settlements, factions and caravan routes — catalogue B11 — *scope lightly, late roadmap*); the
**skyfaller** (a simulation-side falling thing, six seconds from 120 m); the **audio hand-over**
between the title bed and the outdoor bed (design 17 §12, tested); a **`ui.bulletin.crash`** key in
the wiki (*"Something has come down nearby — a crashed ship or drop pod"*, no art, M2); the
**`ClimateDef`** (one, temperate, whose seasons are Wash, Glare and Rime; commented *one per map
type*), a world seed, and a deterministic start clearing; and salvage wreckage scattered at tick
zero by the scenario. Nothing draws a cloud, moves the camera on a path, or shows a loading screen.

### 4b. The precedents

| Precedent | What it does | What to take |
|---|---|---|
| **RimWorld** (the reference) | A planet of tiles; you pick one on a map screen; drop pods land, no steering; the tile's biome and hilliness set the board | The *tile record* — a seed, a biome, a climate — is the shape a world-map tile should have, so M7 can grow out of it |
| **Battle-royale drops** (PUBG, Fortnite, Apex) | Jump from a plane, steer in freefall with full sight of the map, choose the landing, a chute pulls at the end | The **steering model**: WASD/mouse drift, authority enough to matter, a chute that opens late. Synty's *Battle Royale* pack was made for exactly this |
| **Zelda: Tears of the Kingdom** (the owner's reference) | Third person behind the diver; tilt to steer, dive to speed; a cloud sea you break through to a reveal; toon-shaded volumetric-looking clouds | The **camera and the reveal**: the cloud must hide the ground so the break-out is the payoff; the last layer must be thin |
| **Loading-screen cinematics** (many) | The intro plays while the world builds; a hold-to-skip; never replayed on load | **Hide the generation and priming inside the fall**; skip must still settle the tile |

### 4c. Best practice this design should hold to

1. **Skippable, always, and the skip settles the same tile** the fall was heading for.
2. **Short.** Twenty to thirty seconds; the cloud phase no shorter than the board takes to build.
3. **Two inputs to the simulation, recorded in the save, nothing else.** The tile (so the seed,
   climate and generator) and the colonists' standing cells. Same seed + same tile → same hash. The
   steering is player input and is never re-derived.
4. **Presentation owns the whole thing** — a second camera, its own sky and fog, its own clouds — and
   borrows the daylight the way `PortraitStudio` borrows it, handing it back at the join.
5. **Measured at 4K on the owner's machine before it is called cheap**, as grass was; a cloud pass
   at 640 × 480 says nothing.
6. **The choice must be readable**, or the steering is a lie: some sign of what is beneath before
   lock-in (question 7).

### 4d. The four ways to build it, ranked

1. **Steered fall, tile locked by altitude, the fall as the loading screen** (the proposal, as
   assumed in §3). Backed: it is the only option that turns an existing 14.7 s freeze into the
   game's first scene, and it gives the world-of-tiles a reason to exist now.
2. **Map screen then a fixed drop** (RimWorld's shape): pick a tile on a screen, then a
   non-interactive fall onto it. Cheaper — no steering, no readability problem — but it is a panel
   and a wiki namespace, and the fall is a film.
3. **Hybrid**: a map screen picks a *region*; the fall steers within it. Both costs, the least
   surprise. Worth returning to if question 7 has no good answer.
4. **A film only**: the fall with no input. Cheapest; hides loading; teaches nothing about the
   world. Not recommended, but it is the first slice of option 1 with the input removed, so nothing
   is lost by building it on the way.

**If 1 and 2 tie**, the observation that breaks it is question 7's: can a player tell one tile's
look from another from the air before lock-in? The cheapest experiment is a probe shot from 300 m
through one thin cloud layer over two boards generated with different knobs.

### 4e. The clouds, ranked

1. **Instanced Synty cloud meshes with a stepped shader of our own** — on-style, cheap, the
   meshes are imported; `IndirectScenery` already draws thousands of instanced things with a
   compute cull. The risk is that low-poly lumps read as objects rather than a sea.
2. **Layered sheets** (a few large quads per layer, a toon ramp, noise-driven gaps) for the far
   blanket you are above and the gaps you see through. Cheapest by far; flat if looked at edge-on.
3. **A ray-marched volumetric pass** of our own — the "volumetric toon" look proper, TotK's
   silhouette. The most expensive thing the renderer would own, for twenty seconds a game, and at
   3840 × 2160 it is the GPU that was already 8–9 ms of a played frame. Measure it only if 1 + 2
   fail on sight.
4. **A bought asset.** Not in the working agreement; nothing bought until the owner says so.

**Recommended:** 1 for the layers you pass through, 2 for what is far above and below. URP's own
volumetrics are a research question (`d-*`) before anything is built.

### 4f. How it would fit the code

- **Simulation:** a `WorldMap` record — tile count and shape, per tile a seed, a generator recipe
  and a climate slot — made from the world seed by a pure function, saved in its own section
  beside the header's recipe, **not hashed** (nothing reads it during a tick). `ColonyWorld.DefFor`
  gains the tile as an input beside the size. `NaturalStartPass`'s clearing stays the landing site
  and the colonists spawn where they do today. The goldens should not move if the landed tile's
  seed *is* the world seed for tile zero; measure with `GoldenColonyProbe`.
- **Presentation:** a `DropDirector` owning a second camera, the cloud layers, the falling figures
  (the pack's fall clip, a computed hang like `SleepPose`, the pack's land clip), the steering
  input, and the join into `SliceCameraRig` (a `RestorePose`-shaped hand-over). It runs while the
  simulation is not yet built, then calls `BuildSession` for the locked tile at lock-in — which is
  the third menu state design 17 §5b says nothing is designed for, and has to be.
- **Hud:** a skip prompt, the tile's one-word label if question 7 says so, an Events row on landing
  (wiki content in the same commit).
- **Tests:** the world map's determinism and round trip in the fast tier; the steering's
  authority as arithmetic; a PlayMode photograph of the break-out; `FrameTimeTests` for the cloud
  pass at the owner's resolution.

## 5. What this does not settle, whatever the answers

- **Whether Battle Royale ships a parachute** — `Assets/Synty` is absent on the container that
  wrote this, and the pack is not inventoried. One look on the owner's machine.
- **Whether URP in 6000.3 has any volumetric cloud or fog of its own** the project could lean on —
  a research question (`d-`) before a cloud is drawn, along with what TotK's clouds actually are.
- **What the board looks like from 300 m** — nothing has rendered it; the fog and the surround were
  tuned for 48 m and a 48° pitch. A probe shot is the first build step whatever else is decided.
- **The third menu state** (a world being built with the menu gone) — design 17 §5b's restructure,
  which the in-game load screen also waits on. The drop forces it.
- **The 4K cost of any cloud** — measured on the owner's machine, never here.
