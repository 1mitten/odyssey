# Plan: expeditions, from the colony to a place and back

**Phase 3, 2026-09-26.**

- **Interview:** `docs/research/expeditions-interview.md` (19 answers, every recommendation taken
  but the site board's size).
- **Design:** `docs/design/64-expeditions.md`.
- **Research:** `a-13-travel-and-caravans.md`, `b-away-play.md`, `b-voyages-and-situations.md`.

**Phase gate: this plan waits for approval. No unit is started.** Each unit is its own
`claude/expeditions-*` branch and PR, merged in the order below.

## Before starting: the owner's queue

The playtest queue is well past its ceiling of about ten open rows (`playtest-queue.md`), and design
59's World screen has had one look. EX0 to EX8 are provable headless and add no row. The first row
lands with EX9 or EX10. **If the queue is still over its ceiling when EX9 is reached, stop and ask**,
under the queue's own rule.

## Units, the first slice (a round trip to one found place)

| Unit | What | Design | Gate |
|---|---|---|---|
| **EX0** spike | **A fast-tier test only, no product code.** It builds two `ColonyWorld`s, the second with `StartTick` equal to the first's tick. It takes an injured, armed, carrying colonist through `Despawn` → `Adopt`, ticks both boards a day, then saves and loads the second. Written to fail wherever pawn state is board-local, so the list of breaks is **measured**, not guessed, before EX1–EX3 are sized. Kept as a regression test. | §4b, §6b, §6e | fast tier; the list of breaks goes into design 64 §6b |
| **EX1** ids | `PawnIdSource` in `PawnRegistry` (`Next`, `Observe`), replacing `_nextId` at lines 26, 110, 446, 914 and 1010; the default is a local counter. | §4c | fast tier; **every golden identical**; a test that two registries sharing a source never repeat an id |
| **EX2** campaign core | `Sim/Expeditions/Campaign`: `Tick`, `GameSpeed`, `Board[]`, the lockstep invariant, `CampaignHash`, a shared `PawnContent` through an optional `ColonyRequest.Content`. The bootstrap ticks **only** through the campaign: `Update`'s loop, the speed-change tick, the debug skips and `RefreshAfterLoad`. | §4, §9 | fast tier; **a campaign wrapped round each golden gives the committed home hash exactly**; Unity tiers (the bootstrap is touched); player build smoke |
| **EX3** moving a pawn | The portable pawn codec (seven sections to one record). `Despawn(Departed)` keeps the bed and clears the combat and path fields; `Departure.Leave` packs cargo; arrival through `Adopt` gives it back. Departure is refused while downed, carried or bleeding. **The riskiest unit.** | §6b, §6e, §12 | fast tier; the codec round-trips a soaked colony's every colonist to identical `Pawn.ContributeTo` hashes; a round trip keeps skills, health, memories, priorities, weapon and quality, and the stack, leaks no reservation and keeps the bed |
| **EX4** places and chart | Places seeded from the world seed (density per biome, spacing); `Chart` fog; `odyssey.campaign`; **save format 11 → 12**; a format-11 save loads as a campaign with no expeditions. | §5, §12 | fast tier; seeding is deterministic across runs; format-11 fixtures load (`SaveFormat` and `SaveFixtures.AsFormat`) |
| **EX5** the road | `Expedition`: a hex route (A* on `HexGrid`, wrapping), the speed table, camping 21:00–06:00, the slowest member's pace, rations, `NeedsRules` extracted from `NeedsSystem.UpdatePawn` (one owner), health on the road, the journey log, charting as it goes. | §6c | fast tier; `NeedsRules` gives the same numbers on and off a board; the travel time for 1–3 hexes lands in the owner's range; save with a party on the road → same campaign hash a day later |
| **EX6** setting out | The `Depart` job to the edge facing the destination (reusing `EdgeTarget`), the `FormExpedition` campaign intent, food auto-packed from the stores nearest the edge. | §6a, §6b | fast tier; the content fingerprint changes by one line for the new job Def; Long tier |
| **EX7** the first situation | `SituationDef` and fragments; the **salvage cache with a hazard** (hogs, or two or three bandits through existing spawns); a site board built at Standard on arrival from `SiteRules.BoardSeed`; the place's state written and the board discarded when empty; a live site board saved as a nested board save. | §7, §8, §12 | fast tier; a revisit regenerates the same board and applies the state; save with a live site board → same campaign hash a day later; **the tick and memory arms of §14** |
| **EX8** switching boards | `BuildSession` and `TeardownSession` split into simulation and drawing halves; one `WorldRenderModel` per live board; directors rebuilt on focus; per-board camera, slice and selection; a `FocusChanged` event separate from `SessionChanged`; the switch veil. **Second riskiest.** De-risk first by timing a view rebuild over an already-built world in PlayMode. | §9 | Unity tiers; **the switch time measured**; the frame with a hidden board equals the baseline; player build smoke |
| **EX9** the World tab | A key (proposed M) opens the planet over the board, reusing `WorldMapView`, `WorldMapPainter`, `WorldMapGeometry` and `RegionNames`: fog, place pins, expedition tokens with route and ETA, the Form expedition panel. Registry and wiki rows for every name. **Claude Design brief first.** | §5, §6a | fast tier; Unity tiers; the three content gates; **first playtest row** |
| **EX10** away strip and alerts | Board tabs; the away strip; one `AlertModel` and `BulletinModel` per board, merged and labelled; pause with **Go** on Danger or a raid from an unwatched board (a setting under Gameplay). | §9, §10 | fast tier (the merge model is engine-free); Unity tiers; a playtest row |
| **EX11** acceptance | Headless: home → cache → home with loot on three seeds; a raid at home while the team is away; a save on the road and another at the site, each resumed to the same campaign hash; a lockstep twin campaign hashing the same every hour. `docs/milestones/expeditions-report.md`. | all | Long tier; the report; the playtest questions below |

## Later units (each gets a short interview and its own design section)

- **EX12 road events:** the deck, the first choice modal, *fight* opening an encounter board (§6d).
  A Claude Design brief for the modal.
- **EX13 encounter boards:** a one-fragment situation built where the party stands.
- **EX14 signals:** timed offers through `Incidents.TryFire`; the cadence waits on the storyteller.
- **EX15 recruits:** the survivor role; joining on a choice.
- **EX16 knowledge:** research becomes simulation state first (design 34), then salvage advances it.
- **EX17 map reveal:** a reward that charts, or points to the next place.
- **Outposts:** a board kept rather than discarded.
- **The sea:**
  - a boat at a coastal edge (design 59 §10's coast work);
  - ocean hexes by boat only;
  - the strain meter and the ocean deck;
  - islands as sea-only situations.
- **Settlements and the Cartage's caravans:** M7, with factions.

## What only the owner can answer (after EX11, not before)

| Test | Look for | A wrong answer looks like |
|---|---|---|
| Send three colonists to the nearest place and watch the road | the ETA and log make the wait feel like a journey | you speed up to 3 and stop reading, so 6 h a hex is too long or the road is empty until EX12 |
| Leave home with two colonists while a raid can come | the away strip and a paused **Go** are enough to leave home alone | you switch back every minute to check, so the strip does not say enough |
| A paused Go alert during the site's fight | it reads as help | it reads as an interruption, so the pause setting should default to off |
| The switch between boards | a short veil, then the other place where you left it | a stutter or a lost camera, or the veil feels like a load screen |
