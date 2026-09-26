# Highlights reels and replays in shipped games

**Lane B (prior art), owner request 2026-09-26** for the highlights reel (design 63). One subagent,
capped at ten searches and eight reads. **The egress proxy refused most page reads**: GDC Vault,
Game Developer, Dotesports, the Overwatch and RimWorld wikis, the Factorio wiki and Wikipedia. Only
the Progress Renderer README opened. Every other point rests on search-result summaries or on memory,
and the confidence column says which.

## Question

How do shipped games record the significant moments of a long run and play them back later as a
recap or reel? Which approach survives a game update, and how do they decide what is significant?

## Findings

1. **Frostpunk (2018)** ends a scenario with a timelapse of the player's own city being built,
   under narration that lists the decisions taken and closes on "was it worth it?". The footage is
   the player's city, not a pre-rendered film. A Steam thread asking for a city timelapse in
   Frostpunk 2 suggests the sequel dropped it; that could not be confirmed.
2. **Oxygen Not Included** has a *Colony Summary* with a timelapse built from screenshots it takes
   itself, roughly once a cycle and less often later in a run, played from a button on the summary.
   There is no export. Bug reports describe the timelapse capturing nothing or becoming corrupted.
   It stores finished images, so no update can stop it playing. From memory: per-cycle stat graphs
   and the duplicant list are kept for retired colonies.
3. **RimWorld's tales.** A significant event (a death, a finished research project, a trained
   animal) is registered in the save as a *tale*. An artwork keeps only a reference to its tale and
   a seed, and its description is regenerated from those two each time it is shown, never stored as
   text. From memory: each tale kind carries an interest value and an expiry, some are permanent,
   and the store is capped with the least interesting culled. The **Progress Renderer** mod renders
   the map without the interface on a schedule, from hourly to once a quadrum, into a folder per
   settlement; outside tools make the frames into a video.
4. **Dwarf Fortress's Legends mode** (from memory) browses the world's recorded history (figures,
   events, sites, artefacts), including the player's fort, and exports it as XML. **The Sims 2** had
   a storytelling mode: a captioned screenshot album (low confidence). Nothing was found in Against
   the Storm, Kenshi, Going Medieval or Timberborn that builds a recap.
5. **Input-log replays break across updates** unless the old build ships with them. Factorio's save
   replay stores only input actions and is **disabled by any game or mod update**; this is
   deliberate, because a changed rule snowballs until the replay destroys the factory. Players who
   kept days of play for a timelapse lost it that way. StarCraft II still plays very old replays
   only because it **downloads the matching old patch** on demand. Age of Empires II: DE and Halo
   Theater are widely reported to lose films after patches (not verified).
6. **Snapshot replays break too.** Rocket League replays saved before a 2025 patch failed with "An
   error occurred while viewing the replay". The developer acknowledged it, and a community tool
   converts the old files. From memory: CS2 demos are recorded network snapshots, and CS:GO demos do
   not play in CS2.
7. **Overwatch.** A GDC 2017 talk (Philip Orwig) covers the replay system. It was built together
   with the network model and has to produce and transfer a kill cam before the respawn deadline.
   The kill cam replays the last few seconds from recent state the client already holds. *Play of
   the Game* has categories (High Score, Shutdown, Sharpshooter, Lifesaver). Environmental kills
   rank high, area-damage ultimates were weighted down, and players complain that damage heroes win
   too often. Blizzard patented the scoring method (Variety, 2018).
8. **Deciding what counts.** Left 4 Dead's Director keeps an *intensity* per survivor that rises
   when they are hurt or kill close by, and paces a Build Up, Peak and Relax cycle. The peaks of
   that number are exactly the close calls. RimWorld's storyteller (from memory) paces tension with
   deliberate quiet gaps; tale interest is its other sense of what matters.
9. **What survives an update**, most durable first:
   - a dated record of what happened, keyed by stable ids, with the text written when it is shown
     (tales, legends)
   - images taken at the moment (Oxygen Not Included, Progress Renderer, Frostpunk)
   - network or state snapshots (Rocket League, CS2)
   - input logs re-simulated, unless every old build is kept, as StarCraft II does

   What breaks the tie is whether the viewer must be able to move a camera through the moment
   (only a replay allows it) or only needs to watch it (stills are enough).

## Recommendation

A **saved, update-proof chronicle of what happened** is the base every surviving system shares, and
it is worth building whatever else is chosen. On top of it, replays are the only form that lets a
camera move through a moment; they are cheap to store and cost nothing during play in a
deterministic simulation, but they break on every update. **Replays as a same-build bonus over a
chronicle, with a fallback that needs no footage**, is the shape that respects both findings. The
owner chose exactly that (`highlights-interview.md`): true replays, and a "then and now" card
when the build does not match.

For "significant", **a per-colonist danger margin (L4D's intensity turned round) is the close-call
detector**, and the storyteller's *tension* (design 59, on its own branch) is the natural ranking
signal once it lands.

## Sources

- https://gamermatters.com/frostpunks-scenarios-are-scripted-but-have-a-good-payoff-in-the-end/
- https://tvtropes.org/pmwiki/pmwiki.php/Awesome/Frostpunk
- https://steamcommunity.com/app/1601580/discussions/0/4853281047676700316/
- https://steamcommunity.com/app/457140/discussions/0/1750142427224984066/
- https://forums.kleientertainment.com/forums/topic/109844-how-to-use-time-lapse-feature-in-colony-summaries/
- https://kleiforums.com/klei-bug-tracker/oni/colony-summary-time-lapse-seems-corrupted-and-is-not-updating-r23656/
- https://steamcommunity.com/app/294100/discussions/0/1643170903484501774/
- https://steamcommunity.com/sharedfiles/filedetails/?id=1188524738
- https://github.com/Scherub/rw-progress-renderer
- https://forums.factorio.com/viewtopic.php?t=116507
- https://steamcommunity.com/app/427520/discussions/0/3078754982925905812/
- https://tl.net/forum/starcraft-2/526656-old-replays-from-146
- https://steamcommunity.com/app/813780/discussions/0/2289464808500559049
- https://x.com/RocketYota/status/1916288661504590175
- https://www.gdcvault.com/play/1024053/Replay-Technology-in-Overwatch-Kill
- https://variety.com/2018/gaming/news/blizzard-patent-overwatch-scoring-method-1202856902/
- https://dotesports.com/overwatch/news/how-does-overwatch-decide-play-of-the-game
- https://left4dead.fandom.com/wiki/The_Director

## Confidence

| Finding | Confidence |
|---|---|
| 1 Frostpunk timelapse / Frostpunk 2 lacking it | high / low |
| 2 ONI timelapse / stat graphs | high / medium |
| 3 Tales keyed by reference and seed / expiry and cap / Progress Renderer | high / low / high |
| 4 Dwarf Fortress / The Sims 2 / the other colony games | medium / low / low |
| 5 Factorio and StarCraft II / AoE2 and Halo | high / low |
| 6 Rocket League / CS2 | high / low |
| 7 Overwatch talk and categories / kill-cam internals | high / medium |
| 8 Left 4 Dead / RimWorld storyteller | high / medium |
| 9 The ranking | medium (inferred from 1–8) |

## Could not be determined

- Tale expiry figures and the size of the tale store.
- Play of the Game's weights and the window it scores (the patent was not read).
- How Forza and Halo films are recorded.
- Whether Against the Storm, Kenshi, Going Medieval, Timberborn or Frostpunk 2 have any chronicle
  or death recap.
- Where Oxygen Not Included keeps its timelapse images.
