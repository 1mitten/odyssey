# b — Home and away: how the genre splits a player's attention

**Lane B, prior art.** Asked 2026-09-26 for design 64 (expeditions). The question: when a colony or
base game sends a team elsewhere, is the away place real, what happens at home, and how does the
player watch two places at once? It was asked because the owner wondered whether *"a split window
(optionally) … it could limit us"*.

**Clean room:** mechanics and design intent only.

**Access.** One capped pass of 10 searches. Some fandom and Steam pages were refused, so lines
marked **[recall]** are hypotheses.

## Findings (per game)

**RimWorld (caravans, multiple colonies).** Added: the closest match to this project and not on the question's list.
- (a) The world is a planet split into tiles. Caravans cross it as abstract tokens. When one arrives at a site, the game generates a real, playable map for it.
- (b) Every open map keeps simulating in full. The default cap is 1 colony because of performance, and players can raise it to 5 in options. Players say 2–3 maps run slower and need a lot of pausing.
- (c) [recall] The colonist portrait bar is grouped by map, each map has its own icon to switch to it, all maps share one alert list and one letter feed, and there is one global pause.
- (d) Players complain about double the events and micromanagement, the performance cost, and fiddly caravan setup. [recall] They like that away sites are real maps and that settlements can be abandoned or left once you're done.

**Dwarf Fortress (Steam) missions.**
- (a) Fully abstract. You set up raids, explorations and recovery missions on the world screen and pick the squads. Once they leave the map you have no control until they return.
- (b) The fortress keeps simulating in full.
- (c) There is no view of the away team. You get a report when they return: loot, rescued prisoners, dead, captured.
- (d) Common Steam complaints are squads that won't leave, don't return, or can't select targets. Players find it opaque. [recall] They like the low cost to attention.

**Oxygen Not Included: Spaced Out.**
- (a) Several small planetoids, each a real map, all simulated at once. Rockets travel on an abstract hex starmap. [recall]
- (b) Yes, everything simulates. Keeping each planetoid small keeps performance acceptable.
- (c) [recall] A planetoid selector sidebar with per-planetoid status icons, a starmap screen, and global pause.
- (d) Players call switching "clunky" and say neither place gets enough attention. Dupes die when left unsupervised, and many feel unsafe leaving the main base to settle a new rock. One suggestion was to put all asteroids on one screen, separated by neutronium. [recall] Players like the rocket and logistics puzzles between rocks.

**This War of Mine.**
- (a) The two places swap roles. At night the scavenging site is a real playable map, and the shelter becomes an abstract check. Staying survivors guard or sleep, a sleeping one counts about ¼ of a guard, and weapons left behind raise the odds.
- (b) Home is not simulated. Raids resolve off-screen, and a raid always succeeds if nobody is home.
- (c) Strict day/night phases, and the outcome is shown the next morning.
- (d) [recall] Players praise the tension and the "who stays home" decision. Complaints are that raid results feel random and you can't respond to them.

**Frostpunk 1 / 2.**
- (a) Scouts (FP1) or Frostland Teams (FP2) cross an abstract Frostland map. On arrival you get text events with choices, or resources/settlements are revealed. FP2 needs Logistics districts at the map edge to deploy teams. Dangerous expeditions cost lives and Trust.
- (b) The city keeps simulating in real time while teams travel. [recall]
- (c) [recall] A separate Frostland map toggle, arrival alerts, and time controls.
- (d) [recall] Players find it low-friction. Some find it thin or like busywork.

**XCOM 2** [recall].
- (a) The Geoscape is the abstract layer. Missions are real, separately loaded tactical maps.
- (b) Time on the Geoscape stops during a mission, so the base freezes.
- (c) A hard mode switch with a loading screen.
- (d) Players praise the clean focus. They complain about mission timers and never feeling in two places at once.

**Kenshi.**
- (a) One continuous world. Each squad is a real presence in it.
- (b) Only areas around your squads are simulated in detail. The rest is coarse, which is why a mod exists to add background simulation.
- (c) Tab cycles squads. Double-clicking or right-clicking a portrait jumps the camera to that character from any distance. One global pause.
- (d) [recall] Players like the freedom. They complain that a base gets raided while the camera is elsewhere, and about performance with widely spread squads.

**Against the Storm** [recall].
- (a) An abstract hex world map for the meta layer. Each settlement is a real map. "Glade events" are found inside the settlement map and resolved by spending workers/goods on text choices under a timer.
- (b) The world map is frozen while you play a settlement, so only one live place exists at a time.
- (d) Players praise the focus. They sometimes complain that glade event timers stack up and punish them.

**Battle Brothers / Mount & Blade** [recall].
- (a) An abstract party on the world map. An encounter creates a tactical map built from the local terrain.
- (b) World time pauses during battles. Battle Brothers has no home base. In Bannerlord, your fiefs tick abstractly.
- (d) Players praise the seamless handoff from world map to battle. They complain about repetitive battle maps.

**Anno 1800 (multi-session)** — added.
- (a) Old World, New World and other regions are separate real maps.
- (b) All sessions simulate in the background.
- (c) Numpad 1–4 hotkeys switch sessions. Notifications you miss are only shown when you next visit that session. Some alerts are only shown in the session you're in. Filters were added because of notification overload.
- (d) Players complain about notification spam and missed crises in other sessions.

**Factorio: Space Age** — added.
- (a) Several real surfaces: planets and space platforms.
- (b) All surfaces simulate. [recall]
- (c) A "remote view" lets you watch and act on another surface without being there. The design goal was that anything you can do locally, you can do remotely. Platforms can only be edited from remote view.
- (d) [recall] Widely praised. This is the strongest example of solving "you can't be everywhere" through remote control.

**Split-screen / picture-in-picture.**
- No single-player colony game with PiP of a second site turned up in searching.
- Settlers II and Star Reach used split screen only for two human players.
- Kingdom Two Crowns makes a solo player cover two fronts on one screen, and co-op splits the load.
- [recall] Supreme Commander offered a split-screen/dual-monitor view for one player. Hardcore players liked it, but it stayed niche.
- [recall] Factorio mods add camera widgets, which people use as PiP.
- The pattern is that PiP gets used as a way to watch, not as the main way to control.

## Synthesis

**Best fit: the RimWorld model, with Factorio's remote view and Frostpunk's arrival events.**
- **Travel:** an abstract token crossing hex tiles, with event rolls along the way (Frostpunk / RimWorld caravans).
- **Arrival:** generate a real, smaller map. Keep it small, as ONI Spaced Out does, so running two maps at once is affordable.
- **Home:** keeps simulating in full, but with a "steward" layer (standing orders and automatic defense) so leaving it alone isn't a trap.
- **Switching:** a map strip with badges for each site, one shared cross-site alert feed, hotkeys, and optional pause-on-alert.
- **Optional:** a small read-only home camera (PiP) that pops up on critical alerts.

**Top 5 pitfalls:**
1. **Per-site notifications.** Don't hide or delay the other site's alerts the way Anno does. Critical events at home must reach you wherever you are, jump you there, and optionally pause the game.
2. **Unsupervised death spirals.** ONI and Kenshi punish players for looking away. Give home autonomy, safety rules or a lighter difficulty curve while the player is away.
3. **Performance doubling.** RimWorld slows down with extra maps. Keep away maps small, and consider dropping home to lower-detail simulation while the player is away.
4. **Opaque abstract resolution.** Dwarf Fortress missions produce "squad never returned" confusion. Show progress, ETA, and a readable log of events on the journey.
5. **Event pile-up across sites.** RimWorld doubles events and Against the Storm's timers stack. Scale threat pacing to the number of live sites, and don't start timed choices at both sites at once.

## Sources (URLs)
- https://dwarffortresswiki.org/index.php/Mission
- https://steamcommunity.com/app/975370/discussions/0/3761105130209532684/
- https://steamcommunity.com/app/975370/discussions/0/3727323242812934162/
- https://oxygennotincluded.wiki.gg/wiki/Planetoid_Clusters
- https://steamcommunity.com/app/457140/discussions/0/3105765614220863818
- https://steamcommunity.com/app/457140/discussions/0/3142927376039435992
- https://steamcommunity.com/app/233860/discussions/0/810921274024833511/
- https://kenshi.fandom.com/wiki/Controls
- https://problemchild1500.itch.io/kenshi-virtual-simulation-engine
- https://www.thegamer.com/frostpunk-2-complete-guide-to-frostland-exploration/
- https://frostpunk.fandom.com/wiki/Scouting
- https://rimworldwiki.com/wiki/Caravan
- https://rimworldhub.com/post/second_colony_survival_in_rimworld_avoid_this_nightmare
- https://this-war-of-mine.fandom.com/wiki/Raids
- https://this-war-of-mine.fandom.com/wiki/Sleep
- https://www.anno-union.com/updates/game-update-6-december-10-2019/
- https://factorio.com/blog/post/fff-380
- https://wiki.factorio.com/Space_platform
- https://en.wikipedia.org/wiki/The_Settlers_II
- https://gamerant.com/best-local-co-op-split-screen-strategy-games-rank/

## Confidence (high/medium/low)
- **High:** Dwarf Fortress, This War of Mine, RimWorld and Anno mechanics; ONI switching complaints; Factorio remote view goal.
- **Medium:** Frostpunk, Kenshi, and the ONI UI details.
- **Low / [recall] only:** XCOM 2, Against the Storm, Battle Brothers, Mount & Blade, the Supreme Commander split screen, and the player-sentiment summaries marked [recall].

## Could not be determined
- Whether Frostpunk 2's city keeps full real-time simulation during Frostland travel (this is from recall and wasn't checked).
- Whether any shipped single-player colony game uses picture-in-picture of a second site as a core feature. None was found.
- The exact default in Against the Storm for whether glade event timers pause.
- Quantified player sentiment for any game. Only individual forum threads were found, not aggregated reviews.
- Details of RimWorld Odyssey's gravship mechanic (not searched; the 10-search cap was reached).

## What this means for us (design 64)

- **Split screen and picture-in-picture are not the answer.** No shipped single-player colony game
  found uses either as its main view. Where one exists, it is used to *watch*, not to *control*.
- **The answer is one screen with instant, global switching:**
  - alerts labelled by place and never hidden until you visit (the Anno trap);
  - a pause and Go for serious ones;
  - a strip showing the other group;
  - a readable journey log with an ETA (the Dwarf Fortress trap).
- **Home must survive being unwatched** (the Oxygen Not Included and Kenshi trap). Threat pacing
  counts only the colonists on the targeted board, and the undrafted response already fights,
  defends or flees without orders (design 33 §18).

## Layer questions touched

None.
