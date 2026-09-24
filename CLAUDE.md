# Odyssey — project guide for Claude Code

Read this first, then `docs/brief.md` (the governing brief) and `docs/research/INDEX.md` (what is known so far). Update the **Current status** section whenever it changes, and **append the reasoning to `docs/journal.md`** rather than growing this file — that is what the journal is for.

## What this is

A prototype colony sim in the RimWorld mould, in true 3D with discrete vertical layers, set in a ruined sci-fi city. Unity 6.3 LTS (6000.3.x), URP, C#. Art: Synty POLYGON Sci-Fi City. The owner does visual work in the editor on Pop!_OS; Claude Code does code, tests, research and documentation.

## Working agreement (from the brief; these hold everywhere)

- **Phases with hard stops.** Ground → Interview → Research → Plan → Execute. End the turn after each phase and wait. Never run a later phase on assumed answers. No gameplay code until Phase 4, and only after the plan is approved.
- **Research lives in subagents.** One subagent, one question, a hard cap ("stop after N searches / N reads"), fixed return format: Findings, Sources (URLs), Confidence (high/medium/low), Could not be determined. Write each result to `docs/research/<slug>.md` and keep `docs/research/INDEX.md` current (the naming scheme is in it).
- **Commit to recommendations.** Rank options and back one. If two are tied, name the observation that breaks the tie and the cheapest experiment that gets it.
- **Clean room.** Study RimWorld's mechanics, formulas, data shapes and design intent; never paste Def XML, decompiled code, art, audio, names or flavour text into this repo. Never decompile into the repo. Invent our own names.
- **Licensed assets stay licensed.** Synty content lives only under `Assets/Synty/`, which is gitignored. Never copy it elsewhere, never commit it, and never let the simulation or its tests depend on it (clones without the pack must still build and run headless).
- **Files outlive context.** Every phase produces files under `docs/`. Assume the next session knows nothing except what is written down.
- **The cycle one unit of work goes through is `docs/process.md`** — ground, decide in a design doc, test first, measure, hand over, merge, play, record — and its scaling rules (§3) apply to every per-tick loop.
- British English in documentation. No multiplayer, ever. *Ramble* (Godot) is reference only, no code reuse.

## Every finished piece of work ends with a handover

**Owner rule, 2026-09-18:** *"could you put into each prompt — create a table of things to test
after completing work, stipulate a distinct table to explain changes made and how to test as I keep
losing track, and remind of the full folder."*

The owner is the only person who can press Play, and they are usually holding several branches at
once. A reply that ends in prose leaves them to work out where to go and what to look at. **So the
last thing in any reply that finishes a piece of work is a handover: the folder, then two tables.**
Not a summary of the conversation — the smallest thing somebody can act on cold.

### 1. Where

One line, before the tables: the **full path**, the branch, the PR, and whether the art is there.

> **`D:\code\odyssey-review-111`** — branch `claude/colonist-card-skills`, PR #114. Synty
> junctioned, packs imported. Press Play → New game.

Always the absolute path. There are a dozen worktrees on that machine (`git worktree list`) and
"the worktree" names none of them. Say if `Assets/Synty` is **not** junctioned, because without it
everything draws as untextured primitives and the first report back will be about the art.

### 2. What changed

One row per change a player could notice. **What it was, what it is, and where the decision lives**
— not the implementation.

| Change | Was | Is | Where |
|---|---|---|---|
| Selection outline stands clear of the face | border drawn hard against the portrait | 8 px pad, card 65 → 80 | `18-colonist-select.md` §6b |

Leave out anything invisible. A refactor with no player-facing effect belongs in the commit message,
not in this table — it is one of the things that makes the list too long to read.

### 3. What to test

One row per question **only a person at the keyboard can answer**, with what a wrong answer would
look like. This is the table that earns its keep: a test already says whether the geometry is right,
so do not ask for that again.

| Test | Look for | A wrong answer looks like |
|---|---|---|
| Pick a colonist without clicking each card | the three cards readable side by side | you still open each one to decide, so two skills is not enough |
| Reroll | name, face and skills all change together on an unkept card | one of the three lags, or a kept card moves |

Two rules for this table:

- **Never ask for something a test proves.** If the fast tier or the Unity tier can answer it, it is
  not a playtest item, and putting it there teaches the owner the list is padding.
- **State what failure looks like.** *"Check the cards read well"* is not actionable; *"if you still
  click each one to decide, two skills is not enough"* tells them what they are deciding and what to
  say back.

### The rest of the reply

Say what is **still owed** and what is **blocked on them** — an unshot screenshot, a Unity run that
cannot start because the editor is open, a merge waiting on review. And where more than one branch
is in flight, give the **merge order and the reason**, because that is the thing most easily lost
between sessions.

## The content wiki is a standing obligation

`docs/wiki/` is the naming reference for the whole game: every commodity, item, building, command,
work type, need, body part, alert and proper noun, with a stable key beside each. It exists so the
owner can read what is in the game and correct it, and so no session has to guess what something is
called. Hosted: [wiki](https://claude.ai/artifact/JsYRQk1vnza2wFWSNfpQwr) ·
[HUD mockup](https://claude.ai/artifact/PcHWoujGDvd4rzcH1LAwFG).

**The rule: any commit that changes game content updates the wiki in the same commit.** Game content
means anything a player could see named — a new commodity, a renamed building, a reworded
description, a new alert, a faction, a creature, a research project, a month of the calendar. Design
and mechanics are not wiki content; they stay in `docs/design/` and `docs/adr/`.

**Never hand-edit anything under `docs/wiki/`.** It is generated and your edit is silently
overwritten. Edit the source, then rebuild:

| Source | Holds |
|---|---|
| `docs/design/icon-keys.csv` | the name, namespace, milestone and description of every named thing |
| `docs/design/icon-map.csv` | whether the owner's pixel-art sheets can draw it |
| `docs/design/proper-nouns.csv` | people, places, factions, creatures, the calendar |
| `docs/design/colonist-names.csv` | the 240 colonist given names, with register and gender |

```
python3 tools/wiki/build_wiki.py            # rebuild docs/wiki
python3 tools/wiki/build_wiki.py --check    # exit 1 if stale; run before committing
python3 tools/wiki/emit_labels.py           # rebuild Assets/Odyssey/Hud/Registry.g.cs
python3 tools/wiki/emit_labels.py --check   # exit 1 if stale; run before committing
```

`emit_labels.py` generates **two** files from two CSVs — `Registry.g.cs` from the icon keys and
`ColonistNames.g.cs` from the name pool — so one script and one `--check` cover both. Adding a
third generated file goes in there rather than in a script of its own.

The HUD reads its labels from the same file: `emit_labels.py` generates `Registry.g.cs`
(`Registry.Label(key)`), `JobLabels` and `LedgerModel` name nothing themselves, and
`RegistryTests` fails the fast tier on any key the CSV does not know. A content commit runs both
checks.

**And the reverse is enforced too, since 2026-09-17** (owner: *"keep the consistent in the wiki and
the language and UI … ensure that consistency can be enforced using a centralised place"*).
`RegistryTests.NoPlayerFacingNameIsWrittenInCSharp` reads every C# file in `Odyssey.Hud` and
`Odyssey.Presentation` and fails on a string literal that equals a registry name in the six
namespaces where one thing is named on several surfaces at once (`ui.arch.tool.*`,
`ui.arch.category.*`, `ui.status.*`, `ui.res.*`, `ui.alert.*`, `ui.job.*`). **Do not answer it by
rewording the literal** — call `Registry.Label(key)`, or the wiki and the screen will disagree the
first time somebody corrects one of the two copies. It found two on the day it was written: the
seven Build category labels, and the armed banner's `"Building"`.

Both `--check`s are the gate and belong in CI beside the test tiers. Two notes before extending
it. The registry is hand-authored **only until the Def set covers it**: then `icon-keys.csv` is generated
one way out of the Defs and committed, so the wiki and the build-gating icon tests share one
source. Do not create a second source of truth meanwhile. And the hosted copies are snapshots:
after a rebuild, republish `docs/wiki/artifact.html` and
`docs/reference/mockups/hud-v2.artifact.html` (regenerate the latter with
`python3 tools/mockups/artifact_body.py docs/reference/mockups/hud-v2.html`).

## Current status

**This section is only what is true *now*.** The reasoning — every decision, measurement and
reversal — is `docs/journal.md`, and the mechanics are `docs/design/`. When something changes here,
write the *why* there and keep this short. It reached 982 lines once, 89% of a file that is read
into every session; that is the failure mode to watch for.

**Check the code before you trust any status line, here or in a plan.** `docs/plans/vertical-slice.md`
has misled three sessions into rebuilding work that had already landed, and a "known gap" in this
file outlived its own fix. A one-line grep is cheaper than a wasted session.

### Where the project is

Phases 0–3 (ground, interview, research, design) are complete. **Phase 4, execution, is under way.**

| Track | State |
|---|---|
| **M0** foundations | **Closed.** CI runs two tiers per push and PR: a *fast tier* on GitHub-hosted Linux (Sim, Hud, Long, both content checks) and a *Unity tier* on the owner's Windows machine as a self-hosted runner, switched on by the repository variable `UNITY_RUNNER=1`. |
| **M1** world, **M2** pawns | **Done and reported** — `docs/milestones/M1-report.md`, `M2-report.md`. Both went further than the plan asked. |
| **M3** build and dig | **Under way.** Designations, felling, stockpiles, mining, walls, deconstruction, floors and collapse, paving, ladders and beds are all in. Remaining: stairs (`U44`). The gate is a ten-day headless run. |
| **MS** the start flow | **Done**, `U34`–`U41`: a main screen, seed entry and reroll, three-candidate colonist select, save/load with a named binding, and flat avatars. Ran beside M3 because it is session lifecycle rather than colony mechanics. **The candidate card was re-derived 2026-09-18** (`18-colonist-select.md` §6b): it kept 47 px when the avatar doubled to 60, so the three faces overlapped, and its skills line had been squeezed out by the occupation — so the one screen whose job is telling three people apart showed nothing that varied by ability. The card is identity alone — name, age, occupation — at 76 px, which is the face plus its padding on both sides, and **a card is now asserted to clear its own avatar by that padding**; the skills live in the detail pane beside it, two columns and a heading. |
| **TS** terrace steps | **Merged 2026-09-18, PR #126.** Nothing generates at the foot of a step any more (`TerraceFoot`, a sim-side copy of the bank rule checked cell-by-cell against `BankLayout`), and crossing one is priced and drawn against the path it is *drawn* along rather than against a flat cell: the foot cell is a **slope** costing what the hop out of it costs, `PawnPose.StepPace` spends each step's time where its climbing is, and a climbing figure is drawn on the ramp surface itself. Came out of four owner reports in two days; the arithmetic and every rejected alternative are in `docs/design/22-terrace-steps.md` §4b–4c. Three faults older than the work fell out of it: a 1.51 m teleport climbing a sheer face, its 657 mm mirror on a sheer drop, and a two-frame hitch at the start of every step costing more than a flat cell. |
| **WS** rates | **`WS1`–`WS3` in** (`WS1`–`WS4` renumbered from `U42`–`U45`, which were taken): the per-mille seam, work speed from the skill curve with the stroke clock scaled by it, and innate pace with starvation on both rates and collapse at zero rest. `WS4` running is **held** — do not invent an urgency model — **except the one reason the owner has given: a drafted colonist runs** (2,000 per mille, `Pawn.UrgencyPerMille`, design 33 §2h, 2026-09-23). Save format 6. **Reviewed and fixed 2026-09-18** (`docs/journal.md`): `ToilProgress` counts milliwork in **every** driver including the rate-free ones, or one saved and hashed field carries two units; the four accumulators are hashed **whole**, not divided back; `starvationPerInterval` was four times faster than its own comment (the needs cadence is 400 intervals a day, not 200); `RollSeed` is a property whose setter drops the cached pace; and arrival beats collapse, so a colonist cannot go down on her own bed and be told she slept on the ground. All three goldens re-baked — **measured** to be the hash seeing more rather than the colony doing anything different. |
| **RP** roster paging | **Done.** Overflow pagination with right-docked toolbar widget (`<` / `>`), mouse wheel page cycling, selection synchronization on 3D click/alerts, right-click drag-and-drop slot swapping (A ↔ B) with drag ghost and edge-paging, and view persistence in `ViewStateSection` v2. |
| **CL** the carried load | **Merged 2026-09-19, PR #129**, played once, three faults fixed (`docs/design/24-carrying.md`). A load no longer vanishes when it is picked up: it rides the palms, so it travels up out of the lift's crouch with the hands and lowers again on the stow. The stoop and the grasp instant were already there and are untouched. The arms take an authored scoop and the cradle is **measured off the palms**, not solved to a point — arm length varies across the 61 rigs by more than the cradle does. Sim side is one gesture report (`DropCarried` reports the stow, reversing a deliberate silence) and two sparse aspects; neither is saved or hashed, so **no golden moved**. The armful is constant whatever the stack, so the amount now lives on the activity line and nowhere else. **In water the load is hidden**, a placeholder the owner asked for by name. The carry path is **per-nothing**: a new commodity inherits the hold, the turn and both hand-overs, and only opts in to being drawn as an armful (§9a). |
| **GR** growing zones | **In review — PR #119**, branch `claude/growing-zones` — `U46`–`U50`: carrot crop, a paint-a-zone tool in the palette and the orders strip, sow → daylight-window growth → harvest → auto re-sow. One raw-food commodity; cooking, spoilage, seeds and seasons are recorded hooks (`docs/design/22-growing.md`). **A skill now buys speed at the hoe** — `Work_Growing` carries `rateSkill 4` and cutting's curve, which three comments and this file denied for two days. The debug menu gained **Skip one day** and **Ripen crops**. **Reviewed against main twice on 2026-09-20** (`docs/journal.md`): both givers were missing the `ctx.Reachable` every other giver has, and one unreachable crop cost 159 failed jobs in 2,000 ticks; `PlantDef.yields` was declared and never read; and the zone's translucent cover was both the interior-edge borders the owner photographed and 2,065 draw calls a frame. The zone is a bit on the ground's terrain tint now (`TintCode.TilledBase`), which is no draws at all. |
| **SK** skills made visible | **Built, merged with `main` 2026-09-20, awaiting its first play — branch `claude/skills-system-design-k3ufis`, PR #139.** `SK2` publishes a per-mille progress-to-next-level aspect, derived where the ladder lives so the interface needs no copy of the tuning table. `SK3` draws it as an absolutely positioned 3 px underline on the four live rows — out of flow, so the fixed-height colonist pane does not grow and no layout test moves — tinted by passion, which is the ×0.35/×1.0/×1.5 it is filling at. `SK4` is a **toast** stack (design 09 §2.3's own word, and the channel it named but nobody built): a level-up is detected wholly on the presentation side by comparing a level already published for every colonist every frame, so **no event, no saved field and no hash**. `SK5` fixed a real bug — `SkillCatalogue` greyed out **Construction and Growing** long after both began training, and nothing was checking that a row's liveness was true. **There is no `SK1`**: it was the growing rate curve, and #119 had already done it. **Traits are out of scope**; `LearningFactorPerMille()` stays the empty seam. `docs/design/15-skills.md` §8. **Reviewed twice.** The first (§8f-bis) found the level watch keeping its marks across a session boundary, so the next colony's `PawnId` 1 announced a level she was rolled with, and found that the liveness guard the work claimed did not exist. The second, on merging `main` 2026-09-20, found that the toast stack and the Events panel (EV, merged meanwhile) **were solved to the same top in the same column** — the toast is last in the column now, under the Events panel, so a six-second row arriving never steps a standing panel up and down. **Played
2026-09-21** (*"it works great but some visual change"*) and the bar changed three ways (§8i): it
moved **into** the row between the label and the value rather than being an underline along its
bottom, it is the needs' **green** (`HudTokens.Good`) rather than tinted by passion, and it is
**6 px with 8 either side** rather than 3. **No layout constant moved** — the row is still 19 px and
the pane one height — but the row has a **width budget** now that the bar is in the flow, and
`HudLayoutTests.TheSkillRowsPartsFitTheRow` holds the name column to 95 px so widening the bar
cannot silently clip *Construction*. **The toast's level went amber the same day** (§8j): the line
is three labels split in the model on the `{level}` placeholder rather than one label carrying a
rich-text tag, because a tag that stopped being interpreted would put markup on screen and **neither
tier can see that** — P10 again. |
| **EV** events | **Merged 2026-09-20 (PR #140)** — the incident layer: `IncidentDef` with gates and a named worker, `CanFireNow` / `TryExecute`, the saved and hashed incident ledger, the skyfaller, and the Events panel under the alerts. One event, the **supply drop**: the debug menu's *Events* tab (one row per incident Def, built from the content) drops ten to twenty meals from the sky on to the topmost walkable cell of a random column, anywhere on the board, and the colony hauls them. **No storyteller** (owner: debug menu only for now); the cadence vocabulary is mapped and the seams named in `docs/design/23-events-and-storyteller.md`. **First look 2026-09-20, four fixes** (§9): six seconds at one speed from 120 m, above the camera; the Events row jumps the camera only, not the slice; the Events tab; the chime's tail fades and the music swells back over a second. |
| **ST** storage | **S0 merged (PR #151); S1 built, in review.** `ZoneGrid` is the zone container, extracted out of `GrowingZones` behaviour-preserving with every golden identical. S1 is the stockpile tool, the zone painted by a drag, the wash it wears on ground *and* on a built floor for no extra draw calls, a settings table zones point at by id, banded destination search, and save format **7 → 8** (written as 7, moved on the merge because the Work tab's schedule had taken it). **The anchor decides and two zones never merge** — a storage zone carries a filter that a fold would silently destroy, which is where it parts company with growing zones (`docs/design/26-storage.md` §2). **The zone pane is built** (PR #155): clicking any cell of a store selects the whole zone, and the inspect pane opens on *Storage* with the tile behind a second tab. Five priority rungs, Everything and Nothing, the six categories expanding to their commodities with tri-state boxes, and a search box that appears once the list passes twenty rows. **Every mark in it is a drawn `HudGlyph`** — neither shipped font has ✓, ▸ or ☑. It is laid out inline rather than on the inspect pane's row classes, which is why the first build had the text overlapping: those classes carry a two-column geometry this pane does not want. Goldens re-baked and **measured** — meadow and city identical in every economy number, the played board differing by one cell because a tree stands in the starting zone (§7). **A store now empties itself of what it refuses** (2026-09-21, owner: *"I expect the colonists to ensure that all those tiles are occupied by meals or nothing"*): the filter governed arrival only, so a rock already in a meals-only store sat there for ever — and because a cell holding a rock has no space for a meal, a store filled with refused things quietly stopped accepting anything, which is the second half of the same report. A refused thing is scanned in the **first** haul pass beside the loose things rather than in the tidying, and goes to open ground when no store will have it. `PawnContext.NotZoned` became `OpenGroundFor(defIndex)` on the way: it knew only about growing zones while both call sites said "outside every zone", so clearing a field could dump a rock into a stockpile that refused it. **No golden moved and that is the gap** — every golden zone is founded at *Everything*. §11. **The pane's first look moved five things** (§12): the "nothing accepted" warning sits under the control and takes its height **out of the list**, so the pane is one height and no row moves — putting it below the control alone shoved every control **up 90 px**, because the inspect panel is anchored to the bottom and grows upward. The Everything/Nothing chips are gone (they were Allow all / Clear all under a second name, which §9b Q1 had already decided). A store's own Close and its dead placeholder Rename are gone; every pane's top-right Close is the only one. The category count is `Row` 14/500 numeric rather than `Meta` 12. And **a press was one action stale while the game ran** — the pane refilled synchronously on a comment that is true only while paused, so Clear all left the sim at 0 of 7 and the pane showing 7 of 7; `SyncStoragePanel` rebuilds on a signature change now.  **S2 is the shelf** (`docs/design/30-shelves.md`, PR #158): one cell of furniture holding **eight stacks**, passable, rotatable, wood or stone at the bed's cost and work — the first store you *build* rather than paint. `ui.arch.tool.shelf` had sat dim in the palette's Furniture row since the palette was written, so the chip went live and **no wiki content moved but one alert key**. **It is driven by the control that already exists**: both storage intents name a cell and go through `StorageZones.SettingsAt`, and the pane leads with the store for either kind, so a shelf takes its place in the same numbered series — "Shelf 3" and "Stockpile 3" can never be the same store. A contained thing is `Cell = -1, ContainerId = n`, already how a carried thing is modelled, so **the ground stays strictly one stack per cell** and the six write paths that throw on a second are untouched. Save format stays at **8** (a new section; `ContainerId` went into the item record in S1 written by nothing, for exactly this). **The destination rule was generalised, not forked** — `TryBestStorageSlot` returns a cell or a store, and the evidence is that `StockpileTests` and `StorageZoneTests` pass *unedited*. Colonists **eat out of** shelves and builders **take material out of** them; without those a colony starves beside a full pantry and cannot build with timber it has tidied away. **A store being emptied refuses everything in it**, which is the same sentence as a filter refusing a thing and is why an ordered shelf gets the urgent pass and the clearance fallback for free — without it the deconstruct deadlocks. A store with anything in it is **never offered to a deconstructor**, only to haulers. Goldens re-baked and **measured** by `GoldenColonyProbe`, now committed rather than thrown away: all three colonies identical in every number. **The frame cost of drawn contents is measured** (§8b): a warehouse of 40 shelves and 320 stacks is 0.4 ms, the same as the stacks on the floor, and the only per-frame allocation on the path is gone. Still open: storage groups and a second tier. |
| **HT** hardening | **Audited 2026-09-19; audit and plan merged as PR #136. Nothing built.** `docs/audit/2026-09-19-baseline.md` is the baseline audit — scalability measured at the scale target, the monoliths, the process, and everything not yet addressed — and `docs/plans/vertical-slice.md` §HT is the ordered list of hardening units that came out of it. The first finding with a number: a tick that edits one cell at 250 × 250 × 40 costs 1.19 ms against 0.065 ms at rest, all of it `NavGraph.Rebuild` recomputing every district. **Phase gate: the plan is written and waits for approval; no unit is started.** |
| **WT** the Work tab | **In review — PR #145**, branch `claude/happy-tesla-2onz0q`, merged with `main` and corrected on a worktree 2026-09-20 (`docs/design/27-work-tab.md`). One table on F1: the twenty-two-column priority grid and the twenty-four-hour day share one frozen column of names, so a row is one colonist's whole day. Schedule is a new mechanic (`Pawn.ScheduleHours`, save format **6 → 7**, published hour by hour) and is **deliberately outside the state hash because no system reads it** — `ScheduleTests.EditingTheDayDoesNotMoveTheStateHash` is the assertion, and the day it fails is the day to re-bake. **The review moved seven things** (§15): Simple mode's tick and cross were characters **neither shipped font has**, so that whole mode drew empty boxes; the panel's fixed 1,756px hung off any screen under ~1,780; Escape did not close it; opening Build did not put it away; `Describe` was wired to no tooltip; `Attach` did not re-sync it across sessions; and `WorkGridLayout.Hours` was a literal `24` under a comment claiming it was `ScheduleHandle.Hours`. **Then the scrollbar went** (owner, 2026-09-20: *"this isn't a good interface"*) and the grid **pages** like the roster instead — **11 work columns a page** (two pages, panel a constant **1,385 px**) and **12 colonist rows a page**, with the day never paged. The panel's element count is now **bounded at 1,017 whatever the colony is**, against 6,916 at fifty before, and rows are pooled. §16. **A second look moved eleven more things** (§17): the panel was **drawn over the map** — UI Toolkit's `width` is a border box and `.panel`'s 12px padding was not in the number — **clicking a column header now sorts the colony by that skill** (priority for hauling, stable, reset by closing or by the button in the Colonist header), the key is **two keys** aligned to the two halves with the schedule's six as **armable paint buttons**, the column icon tiles are gone, each half carries its own title or pager, and **Simple is the default**. **Verified end to end before merge** (§18): `WorkPriorityEffectTests` proves a priority actually changes what a colonist does — *never* leaves a marked tree standing and the same colonist fells it once the number moves — and `WorkTabCostTests` measures the open panel at **0.031 ms against a 0.292 ms budget, 609 elements, no gen-0 collections**. A priority decides the **next** job and not the one in hand, which is deliberate and is the first thing a player will notice. |
| **PF** frame budget | **The mark pass, 2026-09-20.** The standing-order marks were the last per-cell draw pass and the last uncounted one (P10): they are gathered by colour and go out as one instanced call each, so 901 orders cost 2 draw calls rather than 901, and the pass finally appears in the budget. **Measured with a control inside one run** (`FrameTimeTests.TheMarkPassCostsWhatItSubmits`, `ChunkRenderer.InstanceCellPlates`): the whole pass is **0.40 ms at 901 orders** and the batching recovers **0.09** — a fortieth of what the 4.6 us constant predicts, which is the finding rather than the fix (§6c.1). **Then one sentence of Play found something forty times larger.** The owner watched the overlay while spawning colonists — *"it seemed to hover 1.7 ms no matter the colony size but then frames dropped after so many colonists"* — and the sweep that followed (`FrameTimeTests.TheFrameAgainstColonySize`, `OdysseyBootstrap.FrameSectionMs`) found **`PawnPose.Of` scans every other pawn for the crowd sidestep, once per posed pawn, every frame**: 13.3 ms of a 22.5 ms frame at 384 colonists, against 0.02 ms at 64. Not the tick (0.31 ms at 384), not draw calls (1,243 to 1,324 across a 48-fold colony). **Fixed 2026-09-23** — `PawnCrowdIndex` buckets every pawn's position once a frame on a 3 m grid, built in the composition root and shared by all three passes that pose a pawn. The cull is *exact*: `Proximity` is zero at and beyond `CrowdFarRadius`, so no drawn position moved and the judged sidestep is not re-opened (§6c.9, `25-pawn-steering.md` §9, P12). On a clear machine the frame at 384 colonists went **27.81 → 14.99 ms** and `Actors` **15.79 → 4.94**; the index itself costs 0.042 ms. **Two findings worth more than the fix.** The three-arm control the plan insisted on showed **44 per cent of the cost was the `WhereItIsNow` recompute rather than the quadratic** — one line, no data structure — so measure the cheap candidate alone even when you mean to build the expensive one. And the cull uncovered **a second O(N squared) in the same pass**: `WorldSnapshot.TryGetPawnAspect`, a linear scan over every published aspect, once per far-form pawn per frame. **Fixed the same day** (`docs/design/31-aspect-lookup.md`, §6c.10): a colonist publishes **57 aspect rows a tick** — measured, against a doc comment that claimed "tens of rows" and had been overtaken by the work priorities and the schedule — so the published set is 57 × colonists. A **lazy index built on the first lookup of each published frame** (lazy so the tick pays nothing and a headless run that reads no aspects pays nothing) took `Actors` at 384 colonists from 4.59 ms to **0.83** and `Figures` from 6.39 to **0.74** — and `Figures` is **flat at last** across 64/192/384, the first time the figure ceiling has actually capped anything. **The colony sweep now has no knee**: 2.20 to 4.82 ms across 8 to 384 colonists, against 27.81 ms at 384 before this line of work. The owner's *"frames dropped after so many colonists"* is closed; what is left is the hover. **The lesson is bigger than either fix** — the crowd scan was 3.5× the aspect scan, so while it stood the second looked like a constant: **after fixing a quadratic, measure the same pass again rather than declaring it linear** (P12). Also still open: **target hardware**; the zone snapshot republish and `BestStorageCell`, both with the storage work. **Then the decoration, 2026-09-21** (§6c.3): the owner reported that *"grass tufts and surrounding land have some impact of the FPS"*, and the project could not answer — the surround is submitted from inside `ChunkRenderer.Render` and was charged to `FrameSection.World` with the chunk buckets, so the one pass §6c spent a day cutting had no number of its own, and the tufts are meshed into the chunks and never had one at all. Four instruments went in: **`FrameSection.Surround`**, **`GpuFrameMs`/`CpuFrameMs`** off `FrameTimingManager` (`enableFrameTimingStats` is on since this date), two developer-overlay lines (`cpu … gpu … <resolution>` and the whole submit split, largest first), and **`FrameTimeTests.TheDecorationAgainstTheFrame`**, one world timed four ways. On the played meadow at 640 x 480: shipped **2.71 ms**, tufts off **2.52**, surround off **1.48**, neither **1.29** — so the **surround is 45 per cent of the frame and the tufts are 7**, and together they are more than half of it. The surround is a **flat tax** (1.14–1.24 ms from 8 pawns to 384) that scales with the ring rather than the board (1.07 / 1.78 / 2.05 ms on standard, large, huge), and the city pays 0.058 because it grows no wood. **All of it is CPU submission at 640 x 480 and the ranking may invert at play resolution**, which is what the GPU readout is for: one Play session at the owner's own resolution is the next step, not another test on this machine. **Then the surround was halved the same day** (§6c.4), because the owner said to focus on it. The census found the wood was **230 of the 266 batches at 17 trees a call**, 192 of them holding under 32, out of a key of **115 sectors × 4 mutes × 1 part × 2 tints × 16 themes** — and `SectorOf` **folds the variant into the sector number**, so the ladder §6c called saturated was really sixteen kinds multiplying every spatial cell. The two sector sizes and the variant count became settable statics so a sweep could replace a judged constant, and `TerrainSkirt` now ships **800 m / 1600 m / 8 kinds** against 400 / 800 / 16. **Surround 1.08 → 0.58 ms and the meadow frame 2.71 → 2.14, with all 3,907 trees standing**; it is now flat at about 0.6 ms on every board where it grew with the ring (Huge 2.05 → 0.63, and Huge's whole frame 8.07 → 6.09). ×4 is measured at 0.371 and **not taken** — a slot is a colour palette over one of two silhouettes, so halving them should be invisible and quartering them may not be; that is an eye on the horizon. `SurroundCostTests.HalvingTheVariantsHalvesTheWoodsBatchesAndNotTheWood` guards the factor rather than the number, and `TerrainSkirt.CensusOf`/`KeySpreadOf` are the instruments to reach for before the next guess about this pass. **Then 4K, on the owner's own machine** (§6c.5, three shots): frame ~16 ms at **3840 x 2160**, **gpu 8.15–9.12 ms**, submit 5.11–5.74, of which **World 4.09–4.66 and Surround 0.91–0.98**. So the surround is **six per cent** of a played frame and is done; **the GPU is the largest single item**, exactly as §6c predicted and could not test, which puts the **tufts** (pixels, not calls) back in question; and on the CPU side what is left is **`World`**, four to five times the surround over 3,747 draw calls and 413 chunks — `claude/frustum-culling` is the measured thing to weigh against it. **8.40 + 5.55 is not 16.79**: those frames are paced by a display, so the overlay now prints `vsync` and the frame `cap` beside the GPU figure and a reading without them compares with nothing. **`CpuFrameMs` was wrong and is deleted** — 16.81 ms, then 296.32, then 17,898.04 over twenty-five seconds; `frame` and `submit` are our own stopwatches and say what it would have. |
| **MB** meshing budget | **Done and played 2026-09-21.** The stutter the trace was written to find: `ChunkRenderer.BatchFor` meshed **every stale chunk the draw walk touched, in that frame, unbudgeted**. A traced player session on Huge at 4K measured it exactly — **56 seconds and 8,323 frames with no meshing produced not one frame over 33 ms, while all 192 slow frames fell in the 61 seconds where meshing ran**, and 61 captured stalls meshed 900 chunks apiece. `MeshBudgetPerFrame` is **11** (165 ms / 900 chunks is 0.18 ms each, and ~2 ms of a 5 ms frame is eleven); a deferred chunk keeps its old geometry and stays stale, so **the staleness is the queue** and there is no second list. `PrimeAll` is the one unbudgeted walk, called once from `BuildSession` so a new world still arrives whole inside the loading screen that was already stalling. **Measured with a control in one run**: a board-wide `Remesh()` is **156.1 ms → 8.8 ms**, 900 chunks → 11, 889 deferred (`TheMeshBudgetKeepsAWholeBoardRemeshOutOfOneFrame`). §6c.6 has the diagnosis, §6c.7 the fix, `docs/bug-patterns.md` P14 the instrument lesson that made it findable. **Played on the rebuilt player**: 116 s, 14,112 frames, **35,678 chunks meshed, zero frames over 100 ms**, and meshing seconds now cost exactly what quiet ones do — **0.02% over 33 ms against 0.02%**, where before it was 3.77% against 0.00%. The busiest second meshed 1,186 chunks for a worst frame of 12.7. Owner on the one thing no test could answer, whether the board is seen arriving eleven chunks at a time: *"The look is fine."* |
| **PT** perf tracing | **Built 2026-09-21, not yet run in Unity beyond the tiers.** The game writes what it costs while somebody plays: **a row a second** into `Logs/perf/` in the editor — frame **p50/p95/p99/max**, gpu, submit, tick, every `FrameSection`, every `TickSegment` (reusing `PhaseTrace`, which had existed since the tick benchmark and **had no consumer in the running game**), the counters, and a header naming the machine, the screen, the board and every graphics setting. **Any frame over 50 ms or 3x the last second's median is captured whole**, with its own split, rather than averaged into the second it interrupted. *Mark this moment* in the debug menu drops a marker. `python3 tools/perf/trace.py summarise` reads it; `compare` **refuses two traces whose headers disagree** about machine, screen, vsync or board unless forced, which is `docs/process.md`'s "a timing without its machine is a rumour" made executable. **It measures nothing** — every number is already a public property, deliberately, so it cannot become the next `CpuFrameMs`. Scoped to the loop; the regression record and the sim counters are later units and are added fields. `docs/design/29-perf-tracing.md`. |
| **NW** nobody in a wall | **Built 2026-09-21, not yet played** — `docs/design/30-nobody-in-a-wall.md`. A build site is walkable up to the instant the building exists and `Raise` asked nothing about who was standing there, so a colonist crossing the cell was sealed in **for ever** (no path can start in an unwalkable cell or end in one). Three parts, the owner's choice: a **detour** (`NavFlags.BuildSite`, `MoveCost.SiteDetour` 120) keeps passers-by out; the **guard** in `Raise` waits for somebody walking through and moves somebody standing still, asked by the driver through `CanRaiseNow` **before** the success roll so waiting never costs a botch; and `TrappedPawnSystem` **sweeps** every tick, which is the only thing that frees the colonists already walled up in existing saves. `PawnEviction` is the one owner of where a displaced colonist goes. Goldens did not move, measured on all three seeds. |
| **BA** build appearance | **Built 2026-09-21, not yet played.** The owner reported a one-to-three second delay before a built wall or door appears. Measured in a real player loop (`BuildAppearanceTests`) rather than reasoned about: **the publish seam is innocent** — the wall is in the render mirror on the next tick's publish and drawn on the frame after that. What the probe found instead is **P15**, and it is the **other half of MB above**: `WorldRenderModel.Version` was one number for the whole board, so one wall invalidated all 45 drawn chunks — **12.53 ms in that frame against 0.7 either side**. The budget caps how many chunks may be re-meshed in a frame; per-chunk versions (`ChunkVersion`) cap how many are invalidated at all, so one wall is **3 chunks and 1.73 ms** rather than 45 spread over four frames of stale geometry. **The seconds are still open**: the leading candidate is the editor's asynchronous shader compilation, which a batch run cannot reproduce (`ShaderUtil.allowAsyncCompilation` is false there), and the experiment is one toggle in a Play session (`06-rendering-and-camera.md` §6c.8). **A cell edit must now dirty every chunk whose mesh depends on it** — the global version was forgiving under-marking. |
| **FI** falling items | **Merged 2026-09-20 (PR #138)** — `docs/design/26-falling-items.md`. Items and deconstruction refunds resting on destroyed floors or cleared cells drop onto the first solid floor below (or despawn if over void). Visual downward fall with gravitational acceleration ($t \propto \sqrt{h}$) and `SoundIds.CarryDrop` on landing. Fast tier (759 Sim, 449 Hud), EditMode (1902 passed, 0 failed), PlayMode (77 passed, 0 failed). |
| **TE** temperature | **Merged with `main`, reviewed twice and ready for the playtest — PR #164.** M4's core pulled forward (design 28, the model `a-06` recommended): every enclosed room is one integer scalar, one pass per 120 ticks over cached surfaces — per-room, never per-cell. Seasons and the day's own swing come from a `ClimateDef` (Wash benign, Glare warm, **Rime kills**), the ground damps the seasonal swing by depth so a cellar lags and a deep mine holds the annual mean, and warm air climbs a stairwell at **4:1** over cold falling down it — the whole of buoyancy as one asymmetric conductance. Enclosure grew room identity keyed by min cell index and the **shaft rule** (a hole into the room above still counts as roofed). Temperature moves mood (banded), sleep (×0.9/0.75/0.55), work (×0.7 outside 8–35 °C), `TemperatureSeverity` (signed ±1000, starvation's exact shape, save format **8 → 9**) and crop growth (the 6/42/58 response). The pane says **how warm a tile is** on the click, tinted; the clock says the outdoor reading. **The first review (§12, §12a) found nine faults by probe test**: the severity slope was twenty times its own comments, a floor taken out never told the enclosure, a re-sealed room kept a season-old temperature, a shared slab was charged to the sky, body heat truncated to nothing, a colonist's ambient did not survive a load — and the fixed-point enclosure sweep was **replaced** by a two-phase top-down solve, because the change it converged on propagates downward and a cellar under a house roofed last was never re-solved. Fifteen regression tests. **The merge with `main` was not a formality (§13)**: the shelf reached main first and took edifice 13 and `BuildingHandle` 7, so the **campfire renumbers to 14 and 8**. `BuildShapes.Cells` did *not* conflict — both branches appended the same `1` — so the campfire had no shape until `RegistryTests` caught it, which is the second time that test has paid for itself on that exact fault. **Three more findings (§13a)**: the pass is O(standing edifices) and its own summary said it was not (0.171 → 0.051 ms on the huge board, measured with the edifice count beside the time); the temperature-to-text form had two owners and is now `TemperatureLabels`, guarded by a test that reads the C# files; and **Rime was sixty presses of *Skip one day* away**, so the debug menu gained **Skip one month** — six presses walk the year. Goldens re-baked three times, the last on the merge and **measured** with `GoldenColonyProbe` against both parents: all nine census numbers identical on all three boards, so the hash sees more and no colony does anything different. `docs/design/28-temperature.md`. |
| **SL** session lifecycle | **Built 2026-09-21, in review.** Three things either side of playing. **A pause resumes at the speed it stopped** (`SpeedControl`, its own PR) rather than at normal. **Escape on the main screen backs out one level** — its Load and New game screens had no rung in `SettingsDirector.Escape`, so the key fell through to the last one and opened the in-game settings window over the load list. And **leaving asks**: Quit to main menu and Quit raise `LeavePrompt` (save and leave / leave without saving / cancel) instead of arming twice, so the in-game quit rows no longer arm at all. **An autosave writes the colony over its own save every game day**, keeping `<name>-previous.odyssey` beside it, naming a colony that has never been saved and saying so on the Events panel; the ladder is Settings → **Gameplay**, a new tab, with Off as a rung. Nothing in it reaches a cell, a save section or the hash. `docs/design/17-start-flow.md` §13–§14, `09-ui-and-input.md` §12. |
| **PC** the pointer | **Built 2026-09-21, not yet played** — `docs/design/28-pointer-cursor.md`. The game draws its own cursor: an arrow, and a crosshair in the armed order's hue over the world, reverting to the arrow over the HUD. `Hud.uss` had **eighteen keyword `cursor:` declarations and every one was inert** — keyword cursors are Editor-only, so in the runtime panel they set nothing and logged once per repaint. **The accuracy half was unrelated and larger**: `SliceCameraRig.Update` cast every pointer ray in `ReadMouse`, which runs *before* `ApplyTransform`, so every hover, drag, box and click was resolved against the camera from the previous frame — exact while still, a constant one-frame lag the whole time the camera moved. `ReadMouse` now latches a verb and two screen points and `ResolvePointer` runs after the transform. Fast tier +3, EditMode +7. `docs/bug-patterns.md` P15. |
| **CM** combat | **C1 merged (PR #176). C2 health and melee, C3 weapons and three playtest rounds merged 2026-09-24, PR #180. Blood built 2026-09-24, not yet played — PR #182, branch `claude/combat-blood` (`D:\code\odyssey-blood`), design 33 §10:** a spurt and one mark per landed hit (a splatter along a cut, a spot for a blow), a pool under the downed and the dead that waits for the fall, a fade by the tick over a day, 200 marks oldest first, at most 19 draw calls; presentation only. Owner, after a battle with a crowd: *"it seems great ... happy to merge in"*, 3.5 ms a frame. Hit points (downed at 0, dead at −50 %), corpses, a debug-spawned marauder, right-click attack, a context menu to equip, four weapons sheathed at the hip and drawn for a fight, the lock-on ring, no shared tiles, hit reactions, criticals and knockback, health on the cards, swing/slice/thud sounds, spreading spawns, *Arm every colonist*. Combat's jobs are handles **17–21** after power's. **An unordered fight ends in downs, never deaths**, and a downed colonist cannot heal until rescue (C4). Next: C4–C7. `docs/design/33-combat.md`, plan `docs/plans/combat.md`. |
| **WL** wildlife | **Built 2026-09-23, in review** — `docs/design/30-wildlife.md`, plan `docs/plans/wildlife.md`, stacked on AN. A world **generates** its animals: `MapGenDef` carries a wildlife table (kind, weight, group, habitat) and a density per ten thousand walkable, dry, **reachable** surface columns; `WildlifeSeeder` places sounders of hogs in the woodland and rats by the rock at tick zero, outside the clearing, from a census of the board as generated; `WildlifeSystem` holds the level — an animal decides to leave about every two days, walks to the nearest reachable edge and is **removed** there (`PawnRegistry.Despawn`, the first removal the registry has ever had), and a short board is topped up by a group walking in at the edge. `SpeciesDef.nocturnal`: the rat is out at night. The bare board has none, on purpose. Two goldens re-baked and the probe says the colonists did the same things. **And the Animals tab on F5**, one tab for the wild animals now and the tamed ones later (owner, 2026-09-23), rebuilt to a Claude Design brief: a count strip, a row per animal (portrait tile, kind, doing), sorted by kind then distance, sortable headings, twelve a page, 560 wide by the brief's arithmetic; it never shows beside the inspect pane. Wildlife is off the bar. Sounders are seeded apart and loose; the Almanac's Fauna is the two real animals and an animal's info button opens its entry. |
| **AN** animals | **Built 2026-09-22, in review** — `docs/design/29-animals.md`, plan `docs/plans/animals.md`. The owner's MVP: spawn, wander, draw, click. An animal is a `Pawn` with a `Kind` (saved in its own section, no format bump; hashed, so all six goldens re-baked and the colony probe diffs clean on `main`); a `SpeciesDef` says what it is and `PawnKindDef` names one; the mind is one think node — a leg within its radius under its own `TraverseMode`, or a jittered rest — and every person-system skips it. **Rats climb anything; the midden hog never takes a ladder**, by the `TraverseMode.Animal` mask the graph already carried. Arrival is the debug menu only, on its own **Spawn** tab beside the colonist row; `AnimalSpawnTests` (PlayMode) proves intent → pawn → figure under the real bootstrap. Two CC0 models under `Assets/Art/Custom/Animals` at measured import scales; the rat walks on its clips, the hog on **`QuadrupedGait`**, a computed lateral-sequence walk laid over its idle in the pose pass. The animal rows resolve on the runner, so `AnimalFigureTests` is the first figure test CI can build. **Not drawn past the figure cap** and outside the crowd sidestep, both recorded. Next unit: the health model. |
| **PW** power | **Built 2026-09-23, merged with `main` 2026-09-24 (temperature, combat, wildlife), in review — PR #173, branch `claude/power`.** Its three jobs follow the draft's at handles 14–16; goldens re-baked, colony probe clean against `main`. Unity tiers on the merge: EditMode 2,754 / 2,726 / 0 failed, PlayMode 110 / 105 / 0; 2,000 lines shown cost 0.02 ms in 9 draw calls (design 32 §11). A wood-fired generator (1,000 W, 75-wood hopper, burns **in proportion to load**), lines that run **anywhere** — through walls, under floors, up shafts — in a layer of their own (`Sim/Power/PowerGrid`, not the edifice slot), and an electric heater that warms its room only while powered. A net is lines joined face to face and vertically; buildings attach to a line under or beside them and never bridge nets; a net short of power goes **dark, whole**. Haulers refuel below half; an on/off switch on the pane; *Power failure* and *Out of fuel* alerts. The lines are hidden except while a power tool, deconstruct or cancel is armed, a power building is selected or the Menu's Power overlay is on, and then drawn **through** everything (`Odyssey/PowerLine`, `PowerLinePass`). Lines come up with their own *Remove conduit* tool — deconstruct never takes one. **Lines, generators and heaters take scrap metal** (the old *Scrap* item, stacking to 50) as a second payment beside their material; wreckage and a scrap drop supply it. A line order is a pointer target with a Cancel on its pane (§14). New sections `odyssey.power` and `odyssey.construction.parts`, **no format bump**; three job defs moved every golden, measured to be their counters alone. `docs/design/32-power.md`. |
| **GS** graphics settings | **Built 2026-09-20, not yet run in Unity.** The Graphics tab gained a **Display** group beside the older toggles: VSync, frame cap, render scale, anti-aliasing, shadow distance, display mode and resolution. Numbers rather than yes/no, so `GraphicsLadder` is one owner for the snap-write-raise rule instead of seven copies, and the HUD's three duplicate ladder builders collapsed into `BuildLadderRow`. The three URP levers write through a **runtime copy** of the pipeline asset, the `PanelSettings` trick from `HudShell.EnsurePanelCopy`, or pressing a settings row would dirty the committed `PC_RPAsset.asset`. Nothing polls per frame. `docs/design/27-graphics-settings.md`. |
| **MC** modular colonists | **In review — PR #168.** POLYGON Battle Royale imported (that folder only: the package ships its own PolygonGeneric with **identical GUIDs**). The attachment half of the Synty character system — which both packs always shipped and nothing had wired up — is built: **15 hair pieces and 9 beards** as rigid props on the head bone, dealt by gender, greying from 45, baldness rising with age. The colonist pool is **29** (PolygonGeneric + Battle Royale, gendered); Farm, Sci-Fi and Western stay resolvable and leave the lottery. The packs' own hats, hoods and sunglasses are switched off, so the head is ours. **Every colonist wears an issued jumpsuit**, `#E8EDF6` with `#A8B2C2` trim, so identity is carried by face, hair and beard and clothing becomes progression — applied *after* the rolls, so taking it off gives back the cast that would have been dealt. **The measurement that paid for it:** most hair and beard meshes map every vertex to the single atlas texel the scalp uses, so one rectangle recolours scalp, hair and beard together and a matching beard is free (`docs/research/e-06-modular-colonists.md` §6). Four faults found by running it: `Equals` not knowing the new fields, an appearance book built from a row count, `[Serializable]` on the wrong type, and a cached portrait subject outliving its materials (P14). **MC6 is in**: the far form wears them as two instanced buckets keyed on the piece rather than the person, so the pass costs at most 24 draw calls whatever the colony; the head is captured at bake time and pushed through the same normalisation every part gets, and the bake bares the head too or a far PolygonGeneric colonist kept the pack's own hair. `docs/design/29-modular-colonists.md`. |
| **MZ** map size | **Built and measured 2026-09-21, PR #156.** A fourth board, **Huge 240 x 240 x 16** (921,600 cells), beside Small, Standard and Large; Standard stays the default and stays the baseline every number on record was taken on. The size picker already existed, so the content change is three lines and a CSV row; everything else is measurement. Measured on all four boards in one run each (`docs/design/28-map-size.md` §2): Huge is **0.865 ms per edited cell** against Standard's 0.278, **71.9 bytes a cell** (63.2 MiB, ~80 with the render mirror), **104 ms** to generate and **559 KB** to save. **These are the second set** — the first was taken on the wrong map (§2a): the played scene sets `barrenMap: 1, woodedMap: 1` so `ColonyWorld.Build` applies `MakeWooded()`, and every arm reached for the unmodified default instead. **Found from the owner's play log, not from a test.** Live regions and links came out *identical* on every board, so the ceiling argument is untouched; generation time, feature counts, memory and save size all moved a little. **The audit's 0.449 / 1.150 ms are corroborated.** Two findings fell out. **`TickBenchmarkTests` does not build the game's world** — its room lattice carries 19,606 regions at 120 x 120 x 16 against a generated map's 2,110, so its new edit arm reports an order of magnitude more than the same edit costs in the game; it now prints its own region count and names the arm to quote instead. And **"four times Standard" is the wrong multiplier for anything but cells**: Huge is 1.04x Large's render chunks, because a chunk is 25 x 25 within one layer. **And the frame says something the tick does not: Huge is over budget.** 7.82 ms against the 5 ms budget (Standard 3.18, Large 6.09) at 640 x 480 on a 5070 Ti, and **it is all one term** — `FrameSection.World` is 2.146 -> 5.950 ms while every other section is flat to two decimal places. 5,392 draw calls against Standard's 1,475. `ChunkRenderer.Render` still has **no frustum or distance test**, so all 443 drawn chunks are submitted wherever the camera points on a 600 m board seen through a 160 m camera — **culling is now HT8's only remaining decision and it has a number behind it.** `FrameSection.Doors` read 0.000 on every board, which is a **gap, not a result**: the arm designates and then settles, so `DoorDirector`'s 922k-cell rescan never fires. |
| **FC** frustum culling | **On by default since 2026-09-23** (`docs/design/28-map-size.md` §10). `ChunkRenderer.Render` walked every chunk of every drawn layer with no frustum test; a Huge board is 600 m across and the camera reaches 160 m, so most of what the layer band admits cannot be on screen. Measured on `main` after merging it: **Standard** 33 of 104 chunks culled, frame **2.53 → 2.19 ms**, 1,360 → 996 draw calls; **Huge** 317 of 443, frame **6.63 → 2.95 ms**, 5,083 → 1,744 calls. (The first reading on this branch gave 3.43 → 2.86 and 8.89 → 3.84; the chunk and call counts are identical to the digit and only the milliseconds moved, because `main` now carries the crowd and aspect indexes and the rest of the frame got faster underneath.) **The saving follows the player's shadow distance** — `ShadowCasterMarginMetres` *is* that distance, because a caster nearer than it may cast into the frustum — so at a 120 m setting Standard culls nothing at all. A number here without the shadow distance beside it is not a number. **It shipped off for two days because its proof was blind**: `CullingDoesNotChangeThePicture` imposed its "frustum admitting nothing" on a field the composition root rewrites every frame, so the control was a second copy of the culled shot — the tell was identical chunk and draw counts in both. Fixed with `FrustumOverride`, a paused world and a frozen `Time.timeScale`, and a *repeat* shot so the noise floor is measured rather than assumed: **the same shot twice 0.00%, culling 0.00%, a frustum admitting nothing 98.21%** run alone, and 0.04% / 0.02% / 98.23% inside the full tier. **The acceptance is calibrated against the floor the same run measures** (`culled <= noise + 0.002`) rather than a constant: a fixed 0.5% passed the isolated run and failed the tier on the same commit, because a busy run is still finishing shader variants and streaming. P18. |
| **MF** the Meadow overhaul | **Designed 2026-09-24, nothing built** — branch `claude/meadow-overhaul`, worktree `D:\code\odyssey-meadow`, design `docs/design/38-meadow-overhaul.md`, interview `docs/research/meadow-interview.md`, research `e-09`, `d-16`–`d-18`. Lush Meadow grass on every tile through our own foliage shader, all-Meadow trees as more sim species, and **a smooth skin drawn over the unchanged simulation layers** in place of the terraces, with ~8 layers of hills. Instanced LOD, no occlusion culling. Ultra 60 fps at 4K; Low 60 fps on an RTX 3050/3060 laptop. **M1 measured 2026-09-24** (design 38 §13, `FrameTimeTests.TheGrassAgainstTheFrame`): grass costs **1.1–1.3 ms at 3840 × 2160** on the RTX 5070 Ti shipped and 0.8–1.9 at full cover, over three runs — the cost is having grass, not how much — so lush on every tile is affordable at today's clump size. The opaque queue was cheaper all three times (0.2–2.5 ms), which makes a rendering-layer mask for the outline the lever to weigh in M3. Research had predicted six draws per instance; for grass it is one (no shadows, queue 2501 outside the prepass), so **depth priming is dropped**. The owner's GPU reading at 4K agrees: tufts off 6–7 ms, on ~7 with spikes to 8 while moving. **M2 built the same day** (§14): instanced levels of detail — one bucket per placement draws every level of a Meadow prefab — **off by default**, because the pack's switch heights put every tuft on screen at its crudest card from this camera; M4/M5 turn them on per art with `LodBias`. **M3 built** (§16): the Meadow grass is drawn by our own `Odyssey/Foliage` — wind on the game clock, grass clearing round items and marks, a lighter spring grade — and is 0.5–0.7 ms *cheaper* at 4K than the pack's shader. |
| **SW** the settings window | **Built 2026-09-24, not yet played** — branch `claude/settings-frame`, design **39**. One fixed 1240 x 720 frame, centred, the same on every tab: a rail of five tabs with Save / Save as / Load / Quit / Exit once at its foot, a title band, equal columns, a footer with *Reset \<tab\> to defaults*. The empty square in front of every row was `IconBadge`'s placeholder and is gone; Detail is switches that say On/Off; every mark is a drawn SVG path (`SvgPath`, `PathGlyph`) and every string ASCII. **It is a modal now** (scrim behind it). New: per-tab reset (`SettingsDirector.ResetTab`), Backspace clears a listening key slot, and a keyboard path (Tab, arrows, Enter/Space, one drawn focus ring, the game's keys gated while a control has focus). The leave prompt is restyled, focus on Cancel. |
| **RI** Research and Inventory tabs | **Played 2026-09-23/24 and ready to merge — PR #177** (owner: research *"fine"*, inventory *"good"* after its restyle) — branch `claude/research-tab`, designs **34** and **35**. **Research (F3)** is the interface only, as asked: two fields and four projects, only what the game has (Electricity, then Power lines and Generator; Ladder), named in the registry, and the state (in hand, queue, done) held on the interface side, unsaved and unhashed; progress never moves, and the debug menu's *Finish research* is the only way a project completes. **Inventory (F2)** lists what the stores hold by the six storage categories and, for one item, which stores hold it; **Go** moves the slice to the store's layer, selects the store's *cell* (`SelectionDirector.ChooseCell`) and opens its pane. Store numbers are **published** on `StoreView`/`StorageUnitView` (`Ordinal`) so the tab and the pane share `StorageZones.OrdinalOfCell`; views are unhashed, no golden moved. `emit_labels.py` now emits descriptions for `ui.research.project` (`Registry.Describe`). Where the builds depart from the owner's specs is tabled in 34 §5 and 35 §5. **The Inventory's headings are the storage pane's own** (tinted row, glyph, name in the hue) and **the six category hues were re-tuned for colour-blind players in both panes** — contrast was fine, deuteranopia folded three into one; `StorageThemeTests` simulates all three dichromacies (35 §5a). |

**Work reaches `main` only through a pull request** with both tiers green, one approving review and
the branch up to date. Branch protection enforces it, agents included. `claude/*` branches are
per-change and short-lived; there is no long-lived feature branch.

### Read this before touching that line

Every live line has a design document that holds its decisions, its measurements and the things not
to undo by tidying. **Read the document before changing the code**, and add to it rather than to
this file.

| If you are touching | Read |
|---|---|
| The mouse cursor, the crosshair, which frame a pick is resolved in | `docs/design/28-pointer-cursor.md` |
| The frame budget, draw calls, what a submission costs | `docs/design/06-rendering-and-camera.md` §6c, §6c.1 |
| Grass tufts, the surround, what the decoration costs, the GPU readout | `docs/design/06-rendering-and-camera.md` §6c.3 |
| Tree sectors, how many kinds of tree the surround draws, the batch census | `docs/design/06-rendering-and-camera.md` §6c.4 |
| Chunk meshing, the per-frame budget, why a board arrives late | `docs/design/06-rendering-and-camera.md` §6c.6, §6c.7 |
| What a frame costs at 4K, the GPU readout, vsync | `docs/design/06-rendering-and-camera.md` §6c.5 |
| The performance trace, what a row carries, reading one | `docs/design/29-perf-tracing.md` |
| **Board sizes, what a bigger map costs, the ceiling** | `docs/design/28-map-size.md` |
| **Meadow grass and trees, instanced LOD, the ground skin, quality presets** | `docs/design/38-meadow-overhaul.md` |
| Storage zones, what a store accepts, where a load goes, what it does with what it refuses | `docs/design/26-storage.md` (§11 for the refusal rule, §13 for the wash and the outline, and why a store marks the chunk below it) |
| **Shelves, containers, what is in one, and how its goods are drawn** | `docs/design/30-shelves.md` |
| Falling items, mid-air drops, landing motion | `docs/design/26-falling-items.md` |
| The Work tab, priorities, the rotated headers | `docs/design/27-work-tab.md` |
| The Research tab, the placeholder research state, *Finish research* | `docs/design/34-research-tab.md` |
| The Inventory tab, store numbers on the published views, Go | `docs/design/35-inventory-tab.md` |
| VSync, frame cap, render scale, resolution, the URP copy | `docs/design/27-graphics-settings.md` |
| Who may stand where a building goes, the eviction rule | `docs/design/30-nobody-in-a-wall.md` |
| Power: lines, nets, the generator, the heater, showing the lines | `docs/design/32-power.md` |
| Walls, sites, materials, the build botch | `docs/design/15-building.md` |
| Cancel, deconstruct | `docs/design/16-cancel-and-deconstruct.md` |
| The Build palette's three layouts | `docs/design/17-build-palette-layouts.md` |
| Floors, slabs, support, collapse | `docs/design/17-floors-and-collapse.md` |
| Paving | `docs/design/18-paving.md` |
| Beds, furniture, quality tiers, who owns one | `docs/design/20-beds.md` |
| How a sleeper is laid in a bed, and how big a drawn colonist is | `docs/design/20-beds.md` §7b |
| How a pile on the ground says its size | `docs/design/24-pile-reading.md` |
| Temperature, rooms, seasons, the campfire, what a tile says it is | `docs/design/28-temperature.md` |
| How the interface writes a temperature | `Odyssey.Hud.TemperatureLabels`, design 28 §13a |
| **The settings window, its frame, rail, controls and keyboard** | `docs/design/39-settings-window.md` |
| The debug menu | `docs/design/18-debug-menu.md` |
| The start screen, saving, loading | `docs/design/17-start-flow.md` |
| Colonist select | `docs/design/18-colonist-select.md` |
| Skills, the experience bar, the level-up toast | `docs/design/15-skills.md` (§8i for the bar's width budget) |
| Naming a colonist, the setup page | `docs/design/19-world-setup.md` §10 |
| What a colony starts with | `docs/design/22-starting-kit.md` |
| Growing zones, crops | `docs/design/22-growing.md` (arrives with PR #119) |
| Events, incidents, the supply drop, the Events panel | `docs/design/23-events-and-storyteller.md` |
| Text entry taking the keyboard | `docs/design/09-ui-and-input.md` §6a |
| Avatars and portraits | `docs/design/20-avatars.md` |
| Colonist bodies, hair, beards, the uniform, the cast pools | `docs/design/29-modular-colonists.md` |
| Ladders, the shaft rule, the climb pose, what a click may land on | `docs/design/21-ladders-and-climbing.md` |
| Tree colour | `docs/design/21-tree-colours.md` |
| **Frame cost, draw batching, the surround's price** | `docs/design/06-rendering-and-camera.md` §6c |
| The sub-tile sidestep, crowd and tree avoidance | `docs/design/25-pawn-steering.md` |
| Where a colonist looks, the head turn | `docs/design/23-head-turning-and-gaze.md` |
| Terrace steps, banks, what may stand at the foot of one | `docs/design/22-terrace-steps.md` |
| Water, swimming, the float | `docs/design/20-swimming-and-water.md` |
| Animals, the species and kind, the animal mind, the computed walk | `docs/design/29-animals.md` |
| Drafting, orders, combat, health, weapons, the Sword Combat clips | `docs/design/33-combat.md` |
| Which animals a world generates, the census, arrivals and departures, the night | `docs/design/30-wildlife.md` |
| Picking up, carrying, putting down, the armful | `docs/design/24-carrying.md` |
| The Work tab, work priorities, the schedule grid, its two pagers | `docs/design/27-work-tab.md` |
| Work and move rates (WS) | `docs/design/17-rates-and-stats.md` |
| Skills, the experience bar, passion, the level-up toast | `docs/design/15-skills.md` §8 |
| HUD regions, the orders strip, coverage | `docs/design/14-hud-layout.md` |
| The build cursor and its drag gesture | `docs/design/19-build-cursor.md` |
| The white selection cursor sitting flush | `docs/design/23-flush-selection-cursor.md` |
| Input cases, modality, live portraits | `docs/design/09-ui-and-input.md` |
| Panels | `docs/design/10-ui-panel-catalogue.md` |
| Alert chimes, and what picks one | `docs/design/24-alert-sounds.md` |
| The carry sounds, and their mix | `docs/design/24-carrying.md` §12 |
| The title screen's bed, and the hand-over into a world | `docs/design/17-start-flow.md` §12 |
| The audio framework itself | ADR 0010, `docs/reference/audio-sourcing.md` |
| Icons | `docs/design/11-icon-library.md`, ADR 0007 |

### What runs today

**Simulation** (`Odyssey.Sim`, `Odyssey.Sim.Contracts`, both UnityEngine-free) — grid, support
solver, two worldgen paths, pathfinding, pawns, save/load, Def loader, subsystem schedule, and the
snapshot-read / intent-write seam. Colonists walk, chop, mine, haul, build, deconstruct, eat and
sleep, with needs, mood and skills. A colony survives ten headless days on three seeds, and a
60,000-tick day ends on the same hash under both Mono and CoreCLR.

**The board is a wooded meadow**, 120 × 120 × 16 — grass, woodland with a clearing at the start,
streams and ponds, 3 m terrace risers, and rock, ore and sealed caverns beneath
(`NaturalMapGenDef.MakeWooded()`). The bare board (`MakeBarren()`) is the test baseline on which
anything that is not grass is a bug. The ruined-city generator is still present and still tested,
but it is not what the scene loads.

**Movement is walk, stair, ladder and a one-block hop.** Climbing was removed as a mechanic (owner,
2026-09-16). Every cell a pawn can be in has something under it. **A hop's price has one owner** —
`NavGraph.HopCost`, enforced by `HopPriceHasOneOwnerTests`, because the cell search, the region
graph and the mover must agree and a disagreement fails silently. **Every step is priced against the
path it is drawn along, not against a flat cell** (2026-09-18/19). A terrace climb is one ramp
charged as two steps — the walk into the foot cell and the hop out of it — so the foot cell carries
a **slope** cost class worth `JumpUp − Orthogonal`, and both halves cost 240; `PawnPose.StepPace`
then spends each step's time where its climbing is, so the flats are walked and the ramp is climbed
at one speed. Walking *along* a terrace foot is slow too, and colonists prefer the flat line one
cell out: that is a decision, not a side effect. `docs/design/22-terrace-steps.md` §4b–4c.

**Presentation** — instanced chunk rendering (no GameObject per cell), a slice camera rig, the HUD,
audio, a day/night cycle and golden-hour grading. No pack contains a work animation, so the axe,
pick and hammer strokes are **computed** (`WorkSwing`, `WorkStyle`).

**Colonists are 61 Synty characters, recoloured — not dressed.** No modular body exists in any pack,
so clothing, hair and skin are repainted by rewriting atlas swatch rectangles. Appearance derives
from the pawn's own `RollSeed`, so the person on the setup card is the person who walks around. A
portrait is the actual character rendered once at 128 px and cached on the appearance, not the pawn.

**What the player sees is decided by how deep they are.** At or above the surface every layer above
is drawn solid; below it, one layer above is x-rayed and every layer below is drawn. Anything drawn
solid is clickable; a ghost never is. **The landscape is never cut away.** The cut-away ceiling is
opt-in (`GraphicsOption.CutAwayCeiling`), because seeing what you just built is the commoner need.

**Water is a body, not a lid.** A water cell emits a face wherever the thing beside it is not water
at the same level, never between two water cells. Falls carry downward-scrolling streaks and foam,
because at the play camera's 48° the Fresnel returns 2.2% and anything carried by the normal is
invisible where the game is played.

### Standing rules that are load-bearing

- **Anything fixed to the grid is draped; only what moves over it is lifted.** Came out of walls
  going up stepped: `ChunkMesher` lifted every panel to one height sampled at one point.
- **Nothing in presentation is in a cell, a save or the hash** — grass tufts, ground relief, banks,
  chips, the surrounding land, sound, tree colour and the see-through fade are all drawn and none
  are simulated. The one deliberate exception is the saved **view** (camera, slice, selection,
  speed), which is `ISaveable` and pointedly *not* `IStateHashable`: determinism is the hash's
  business and where the camera points cannot affect a tick.
- **But a façade that fills a cell the simulation can fill is a bug waiting to be reported.** The
  bank at the foot of a terrace step fills its cell floor to rim, and worldgen grew trees inside it
  (2026-09-18). Every other façade is drawn on ground that stays empty. Ask it of any new one: *can
  the simulation put something where this is drawn?* If it can, the rule needs a sim-side copy —
  `TerraceFoot`, checked cell-by-cell against `BankLayout` — and the guard goes where the thing is
  placed. `docs/design/22-terrace-steps.md`.
- **A render that is kept is a render of everything that was true at that instant.** A portrait is
  taken once per appearance and cached for the session, and the daylight cycle writes the *global*
  ambient, fog, sky and sun — so until 2026-09-20 a colonist photographed after dusk kept a black
  card for ever. `PortraitStudio` now owns the whole environment for the synchronous instant of its
  render and hands it back. Anything else one-shot and cached owes the same. `docs/design/20-avatars.md`
  §10.7, and **measure the take-over rather than reading it**: three versions of that fix looked
  right and were not.
- **A budget applied in arrival order is a budget on identity.** `PawnFigureDirector.MaxFigures`
  caps live animated colonists and its own comment says the rest are "a long way off"; nothing
  sorted, so the frozen ones were the highest pawn ids wherever the camera was. It keeps the
  nearest now, and does nothing at all under the cap. **64 is a hard ceiling**
  (`PawnFigureDirector.FigureCeiling`, owner 2026-09-20): the setter clamps, `FigureCeilingTests`
  fails on anything that raises it, and moving it is a frame measurement rather than an edit.
  §11 of the same document.
- **Content is written once.** The XML under `Assets/Odyssey/Defs/Core` is the only copy of the pawn
  tuning and the world tables. Callers go through `ContentPack.Pawns()` and `WorldContent.Table`.
- **Which map a measurement is taken on has one owner, `ColonyWorld.DefFor`.** The played scene is
  *barren + wooded*, so `MakeWooded()` is applied on top of `MapGenerator.DefaultDef` — and for two
  days every per-board arm reached for the default and measured a board nobody plays. **A test that
  builds a world must go through `Odyssey.Tests.Sim.PlayedMap`**, which calls that chooser rather
  than copying it; `PlayedMapTests` holds it to an observable property of `MakeWooded` with a
  negative control. Note `ColonyWorld.Build`'s `wooded` parameter defaults to **false**, which is
  `MakeBarren()` — a board with no trees and no water, which is what `BoardMemoryTests` was
  measuring. `docs/design/28-map-size.md` §2a.
- **Content values are pinned by fingerprints, and they earn their keep.** Editing rock's
  `workToClear` from 700 to 701 once left all 448 tests green. A deliberate content change is one
  line; an accidental one now fails.
- **A test that retunes content replaces the Def, never writes through it.** The Defs a record's
  arrays point at are shared by every record in the process, so a write-through silently retunes
  every test that runs afterwards.
- **Never hand-edit `docs/wiki/`** — it is generated. Edit the CSVs and rebuild; both `--check`
  gates must pass before a content commit.
- **Do not answer `RegistryTests` by rewording a literal** — call `Registry.Label(key)`, or the wiki
  and the screen will disagree the first time somebody corrects one of the two copies.
- **The board's size is decided once, in `OdysseyBootstrap.BuildSession`, before the chunk grid
  and the render model are built from it.** It was two numbers until 2026-09-20 — the inspector's
  for those two, the setup page's for the world — and nothing could tell, because nothing wrote to
  the chunk grid during a build. The first thing that did threw out of bounds.
- **A panel that sets its own width must add the chrome the stylesheet puts inside it.**
  UI Toolkit's `width` is a *border box*: `.panel`'s `padding: 12px` and its 1px border are inside
  it, so setting a panel to the width of its contents leaves them 26px short and they overflow to
  the right, over the world. The Work tab did exactly that on 2026-09-20 and the owner reported the
  schedule half drawn over the map. `WorkGridLayout.PanelOuterWidth` is the pattern — the content
  width plus `2 * (HudLayout.Pad + HudTheme.BorderWidth)`, never a literal — and
  `ThePanelIsWideEnoughForItsOwnPaddingAndBorder` is the assertion. **Nothing else checks a C#
  layout constant against a USS rule**, so the two can disagree in silence indefinitely; that test
  is the first that does and is worth copying.
- **A character the interface writes must exist in both shipped fonts, and a test reads the files
  to check.** The HUD ships Archivo Narrow and IBM Plex Mono; a label's face is picked by its
  `HudTextRole` and the `numeric` flag, and roles move. `HudFontTests` parses both `.ttf` cmap
  tables in the fast tier and fails on any non-ASCII character in a literal under `Odyssey.Hud` or
  `Odyssey.Presentation` that either face cannot draw. **Nothing else can catch it**: the fast tier
  has no text engine and the Unity tier asserts no pixels, so a missing glyph is a silent blank in
  both. It has already happened twice — the Work tab's Simple mode and the bed-owner picker's tick,
  the second of which shipped to `main` and was never drawn. **Draw the shape as a `HudGlyph`**
  rather than reaching for a Dingbat; the project draws its own icons for exactly this reason.
  `docs/bug-patterns.md` P13.
- **An order's colour has one owner, and it is `Odyssey.Hud.OrderColours`.** The chip in the orders
  strip, the palette header, the drag cursor and the mark left on the board are all the same hue.
  There were two tables in two assemblies for months and they disagreed on two of the four tools —
  deconstruct was orange on the panel and the *cancel* red on the ground. Never write a `Color` for
  an order in Presentation; ask. `OrderColoursTests` runs in the fast tier and walks every tool.
- **Where an order's mark sits is `WorldRenderModel.MarkHeight`** — the top of the cell for
  anything that fills it, the top of itself for anything that stands up without filling it, the
  floor for everything else. Trees are on the floor deliberately.
- **A length taken off a rig is measured from the drawn mesh, never from a bone's name.** The
  Synty humanoid avatar maps `HumanBodyBones.Hips` to a bone called `Root` that stands on the
  floor, so `StandingHipHeight` is the 0.2 m floor of its own clamp on every one of the sixty-one
  characters — and `SleepPose` read it as a length and laid a 2.49 m colonist down 0.38 m long,
  hanging her off the end of her bed. `FigureBuild` bakes the posed mesh and takes the sole and the
  crown; `MeasureSole` already did the same for the same reason. `StandingHipHeight` is left as it
  is because the gesture crouch is tuned against what it returns — do not derive a length from it.
  `docs/design/20-beds.md` §7b, `docs/bug-patterns.md` P11.
- *Subsystems* are simulation-side; *directors* are presentation-side. Do not unify the two words.

### Fixed decisions

- **Cell size: 2.5 × 2.5 × 3.0 m** (ADR 0002), irreversible. Half-heights and slopes are a drawing
  offset, never a cell.
- **Architecture: plain C# structure-of-arrays with Burst on measured hot paths** (ADR 0005),
  decided by a benchmark in which both candidates produced the identical state hash.
- **Determinism before threads.** Single-threaded fixed-tick sim with tick groups.
- **No multiplayer, ever.**

### Tests and gates

- **Fast tier** (`scripts/test-fast.sh`, ~20 s, no Unity): **980 Sim + 717 Hud**
  (2026-09-22, `claude/modular-colonists` merged with main); Long tier **34**,
  up from 23 because the per-board measurement arms all carry `Category("Long")`.
  **It compiles neither Presentation nor Editor**, so a unit touching the composition root or the
  HUD shell is unproven until Unity has compiled it, however green the seconds look. It cost two
  rounds on 2026-09-21: a callback parameter in `FrameTimeTests` shadowed a local, and later a
  `List<>` went in without its `using` — the fast tier was green in twenty seconds both times and
  the second one put the editor into Safe Mode.
- **Unity tier** (`scripts/unity.sh test editmode`, authoritative), last run 2026-09-23 on
  `claude/research-tab` after merging `main` (#173, power): EditMode **2,788 total, 2,759 passed,
  0 failed**; PlayMode **111 total, 106 passed, 0 failed**. Before that, after #176: EditMode 2,660 / 0
  failed, PlayMode 108 / 0 failed, the new ones `StockpileDragTests` (a
  stockpile drag and a click-move-click, from the presenter to a published zone) and
  `DockedTabGeometryTests`, which lays
  both new windows out at 1920 x 1080 (at the runner's own small game view the panel scales to
  0.39 and every border rounds up to a physical pixel — design 34 §5b). The run before, on
  `claude/pf-crowd-scan` (off `origin/main`): EditMode **2,527 total, 2,503 passed, 0 failed**;
  PlayMode **102 total, 97 passed, 0 failed**. The nine new EditMode ones are `PawnCrowdIndexTests`,
  which pin the crowd cull as *exact* rather than close; the two new PlayMode ones are
  `TheCrowdScanCostsWhatItVisits` and its repeat. The run before it, 2026-09-22 on
  `claude/modular-colonists` **after merging main**: EditMode **2,518 total, 2,494 passed,
  0 failed**; PlayMode **100 total, 95 passed, 0 failed** (EditMode 2,520 / 2,496 with MC6's two head tests). The run before that, on
  `claude/build-appearance-and-entombment`, was EditMode 2,406 / 2,385 and PlayMode 98 / 93. The new ones are
  `EntombmentTests` in the fast tier and `BuildAppearanceTests` in PlayMode, whose logged line on
  that run is the whole of the build-delay answer: *in the mirror on frame 33 (1 ticks, 12.4 ms),
  drawn on frame 34, 3 chunks re-meshed*, with the twelve frames after the raise flat at 0.33–0.43
  ms — with the meshing budget also in, the spike frame is gone rather than merely smaller. On the runs before it: `claude/huge-map` **after merging
  main**: EditMode **2,366 total, 2,345 passed,
  0 failed**; PlayMode **97 total, 92 passed, 0 failed**. The new ones on this branch
  are `SurroundCostTests`, `MeshBudgetTests`, `FrameWindowTests`, `TraceWriterTests`, and in
  PlayMode `TheDecorationAgainstTheFrame`, `TheSurroundSectorSweep`,
  `TheTraceAgreesWithTheArmThatTimedIt` and `TheMeshBudgetKeepsAWholeBoardRemeshOutOfOneFrame`.
  On the runs before it:   `claude/storage-pane`: EditMode **2,323 total, 2,305 passed, 0 failed**; PlayMode **92 total,
  87 passed, 0 failed**. On the CI runner, which has no `Assets/Synty`, the same commit is
  2,323 / 2,302 / 0 and 92 / 82 / 0 — a lower *passed* with `failed` still 0 is the art-dependent
  tests ignoring themselves, and is correct. The eight new EditMode ones are the store's refusal
  rule and two tests in `GrowingJobTests` that had never actually run; the one new PlayMode one is
  `ZoneInspectTests.ClearingEveryCategoryDoesNotMoveTheRowsThatDidIt`, which is the only thing in
  the project that checks a panel does not shift under the pointer. The run before it, on the same
  branch before those: EditMode **2,315 total, 2,295 passed, 0 failed**; PlayMode **91 / 86 / 0**.
  The seventeen EditMode ones added there are the storage zone pane's model and the type scale's
  second heading role. The run before it, on PR #139
  merged with main after the experience bar's first look and the toast's amber level:
  EditMode **2,298 total, 2,278 passed, 0 failed**; PlayMode **91 total, 86 passed, 0 failed**. **PlayMode read 81 on the first attempt and that was the
  machine, not the branch**: `Assets/Synty` had gone missing, so the two `PortraitLightingTests`,
  the two `AvatarSheetTests` and `FigureCapTests` each ignored itself and said so in its skip
  reason. Re-run with the art restored it is 86, matching `main` exactly. **Read the skip reasons
  before reading a lower *passed* count as a regression** — `failed` is the number that matters and
  it was 0 both times.
  The run before it, on `claude/mark-pass-batching` after merging main: EditMode **2,228 total,
  2,210 passed, 0 failed**; PlayMode **91 total, 86 passed, 0 failed**. The seven new EditMode ones are `CellPlateTests`,
  the guard that a marked board costs draws in colours rather than in cells; the two new PlayMode
  ones are `FrameTimeTests.TheMarkPassCostsWhatItSubmits` and `TheFrameAgainstColonySize`. The
  remainder are `[Explicit]` or ignored. The run before it, on the Work tab branch, was EditMode
  2,211 / 2,193 and PlayMode 89 / 84.
- **Do not run the PlayMode tier while another Unity batch run is going.** It carries the timing
  tests, and `HudStressTests` failed at 3.770 ms against a 1.167 ms budget beside two other
  `unity.sh` runs and passed at 0.603 ms alone, on the same commit. **The baseline the test logs is
  the tell** — it moved 2.5x between the two and a real regression would have left it alone. Check
  `Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"` first, and wait for
  `TestResults/PlayMode.xml` to be *newer* than the run you started rather than merely to exist:
  the previous run's file sits there until the new one finishes. **A second batch run against a
  locked project is refused outright and writes nothing** — on 2026-09-20 that was read as a
  finished run whose numbers had not changed. `docs/lessons.md`.
- **The runner has no `Assets/Synty`, so its PlayMode count is lower than this machine's and that
  is correct.** Everything that needs a colonist's art ignores itself there — on 2026-09-20 the
  same commit was 85/80/0 here and 85/75/0 with ten ignored on the runner. **A test that needs the
  packs must ask whether the art *resolved*, never whether there is a catalogue**: the catalogue is
  committed and its prefab references point into the gitignored folder, so it loads perfectly with
  every reference null on exactly the machine that can draw nobody. `PortraitStudio.Available` and
  `PawnFigureDirector.Enabled` are the two right questions; a `moduleCatalogue == null` check is
  the wrong one and has now turned the runner red twice — and **"does any row have art" and
  `Enabled` itself are wrong for a colonist test too, since the animals unit committed art of the
  project's own** that resolves on the runner: ask about the rows the rule is about
  (`WorldRenderModelTests` and `PawnFigureDirector.CanDrawColonists`, 2026-09-23, the third and
  fourth times; and `FrameTimeTests.TheGrassAgainstTheFrame`, 2026-09-24, the fifth: it asserted grass
  existed, and the runner draws none — it now asks whether a foliage material was ever made).
- **An editor GUI appears on the project moments after a batch run finishes**, twice on 2026-09-18
  (09:25:52 and 09:47:19, against runs ending 09:25:19 and 09:47:13), and it locks the project
  against the next `unity.sh` command. The cause is unestablished — Hub, the licensing IPC, or a
  person — so check `Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"` before concluding a
  batch run failed, and do not kill a process that might be somebody's open editor.
- **Player build** (`scripts/unity.sh build`, ~15 s, 386 MB into gitignored `Build/`): the only
  thing that compiles the *player* assembly set and the only thing that can fail on a stripped
  shader or a path under `Assets/` read at runtime. **Two green tiers say nothing about whether
  the game runs** — both compile and run in the editor's domain, where every shader and every
  variant exists always. Smoke-test it with `Build/Win64/Odyssey.exe -odyssey-newgame -logFile <path>`,
  which boots straight into a colony; a clean log from the main menu proves nothing, and that
  mistake cost three passes on 2026-09-19. Three separate faults had to be fixed before the first
  player drew anything: `ShaderInclusion` (runtime-found shaders), `ContentPackBuild` (the Defs,
  via `StreamingAssets`), `InstancingKeepAlive` (the `INSTANCING_ON` variant, which
  always-included does *not* keep) and `SyntyInstancingKeepAlive` (the same variant for the
  **pack's own** Shader Graph shaders, which no `Shader.Find` ever names and which arrive on
  prefabs with instancing off — staged for the build and deleted after, because a keep-alive for a
  licensed shader must never be committed). Each was invisible until the one before it was fixed.
- **Before diagnosing anything build-shaped, `git diff HEAD -- ProjectSettings/ Assets/Settings/`.**
  An uncommitted flip of URP's `m_StripUnusedVariants` to `0` once took one shader pass from 64
  variants to 884,736 and the build from 12 seconds to an estimated day and a half.
- **Run the Long tier before merging:** `scripts/test-fast.sh --filter TestCategory=Long` (23
  tests, ~20 s). **The default fast tier excludes it**, so three green tiers can sit on top of a
  Long tier nobody ran — which is how PR #145 merged clean and turned `main` red on a wall-clock
  gate. `docs/lessons.md`.
- **Content gates — there are three, and the third is the one that gets forgotten:**
  `python3 tools/wiki/build_wiki.py --check`, `python3 tools/wiki/emit_labels.py --check`
  and `python3 tools/icons/icons.py validate` (plus its 30 unittest tests,
  `python3 -m unittest discover -s tools/icons -t tools/icons`). All three must pass
  before a content commit. The icon gate turned CI red on 2026-09-21 over a hand-written
  row in `icon-map.csv`: a key with no sheet behind it is `key,-,,,"description",-,gap`,
  not a row of empty fields.
- The two tiers **do not run the same NUnit**, and the fast tier's is newer; **a frame is not a
  tick**. Both traps are in `docs/lessons.md` and both have cost a Unity run.

Frame time under the real player loop, against a 5 ms budget — **and the budget is in the docs, not
in the assert**: `FrameTimeTests` passes anything under a 30 Hz frame, so a green PlayMode run says
nothing about it. Measured 2026-09-20 after the surround work: meadow **2.59 ms**, field (2,065
zone cells) **4.09 ms**, city **2.01 ms**, on an RTX 5070 Ti at 640 × 480. All four per-cell draw
passes are now fixed: the zone cover, the seed specks, the surround's batching, and — later the
same day — the standing-order marks, which a board of 901 orders took from 2,144 draw calls to
1,245.

**Two rules before you quote any of that.** This machine runs several editors at once, so a frame
number is only comparable with one taken in the **same run**: the city canary drifted from 2.01 to
4.01 ms in an afternoon purely on what a sibling worktree was doing, and the same pass read 1.57,
0.81 and 0.19 ms on that noise alone. And **"a submission costs about 4.6 us whatever is in it" is
not a per-call toll.** Measured with a control in one run, a mark plate's submission costs about
**0.1 us**; the readings the constant came from each drew a real mesh. It is what a *loaded*
submission costs — the thing to reach for when a pass is slow — and not arithmetic that condemns a
per-cell loop before it is measured (`docs/design/06-rendering-and-camera.md` §6c.1).

**Board size costs the frame, and it costs exactly one term** (2026-09-21, `28-map-size.md` §2):
Standard **3.18 ms**, Large **6.09**, Huge (240 x 240 x 16) **7.82** — over the 5 ms budget — with
`FrameSection.World` going 2.146 -> 4.513 -> 5.950 while `Figures`, `Overlays`, `Mirror`, `Sight`,
`Audio` and `Actors` stay flat across all three. Draw calls 1,475 -> 5,392. **`ChunkRenderer.Render`
has no frustum or distance test**, and on a 600 m board seen through a 160 m camera that is where
the money is; frustum culling is HT8's last open decision and now has a measurement behind it. All
three were timed **inside one run**, seconds apart, because that is the only comparison this machine
supports.

**And none of it is measured at a play resolution or on the target laptop**, which is the largest
open question in the renderer — and since 2026-09-21 the developer overlay can answer it: backtick
shows `cpu` against `gpu` and the whole submit split, so a Play session at the owner's own
resolution now says which side of the bus a slow frame is on. **Read the GPU figure first**; where
it is at or above the frame time no amount of batching will move it. The city's move from 0.88 to 1.56 ms is **unexplained** and still
open.

**Pathfinding is where the tick goes under load, and it is no longer a threat to the frame budget**
(OQ-19, measured on the real `SimWorld.Tick`): a colony of 50 on 250 × 250 × 40 costs 0.025 ms a
tick, and 0.438 ms under D1's replan rate — half what ADR 0005 estimated. The once-recorded
"futile searches for unreachable targets" explanation was **falsified by its own follow-up**.

### Waiting on the owner

- **The supply drop has had one look** (EV, `claude/events-system`), which moved four things
  (`docs/design/23-events-and-storyteller.md` §9). Not yet judged: whether six seconds at one
  speed from above the camera reads as a chute or as a lift and whether it is waited for, whether
  the pad on the landing cell helps or clutters, whether the Events row's jump without the slice
  moving is enough to find a rooftop drop, whether a second's swell back of the music is the room
  settling or the music being slow, and whether "anywhere on the board" is a pleasure or a chore
  to chase (§9, §10).
**The list lives in `docs/plans/playtest-queue.md` now** (2026-09-19): a finished piece of work
adds a row there and a verdict closes one. It had grown to 27 open items and 134 lines here, in the
file every session reads first. Twenty-seven unplayed changes against a handful of playtests a day
is the project's real constraint, and the audit says why (`docs/audit/2026-09-19-baseline.md` §6).

### Known gaps

- **No storyteller: nothing fires an event but the debug menu** (owner's call, 2026-09-20). The
  incident Defs carry their gates and the ledger keeps the refire memory, so a scheduler reads
  rather than restructures; the History screen (F9) is the other half still owed
  (`docs/design/23-events-and-storyteller.md` §8).
- **No health model**, so fall damage is designed with a number and nothing to apply it to, a
  colonist rides a collapsing floor down unharmed, and the debug menu has no kill or heal.
- **No fog of war**, so a sealed cavern is visible if the player scrolls the layer down.
- **An animal past the figure cap is not drawn at all** (2026-09-22, `docs/design/29-animals.md`
  §8a): the instanced baked pass deals every pawn a colonist's face, so animals are figures only.
  Under the 64-figure ceiling that is every animal on screen; a baked animal pose is the unit that
  closes it. Animals are also outside the crowd sidestep on both sides until P11 is fixed.
- **A colonist can still lie down inside a terrace bank.** Trees are guarded out of those cells at
  generation (`TerraceFoot`, `docs/design/22-terrace-steps.md`), and a walking figure is lifted onto
  the ramp, but a body lying down is not: sleep on the ground at the foot of a step and the façade
  hides you. §4 of that document holds the two candidate fixes and why neither was guessed at — both
  move the state hash. An item dropped in one has the same problem and is unreported.
- ~~A skill level buys nothing a player can feel~~ — **stale since WS2/WS3 (2026-09-18) and
  caught by the audit a day later**: work speed reads the skill curve and pace reads condition.
  Kept struck through for one release as the example of the failure this section warns about.
  **It caught a second session on 2026-09-20**, which branched from a head predating the audit, read
  this line as live and reported it as a finding. The line is doing its job.
- **`Skill_Hauling` accrues experience with nowhere to show it.** Hauling is a work type and not a
  skill by decision (`15-skills.md` §6.2, and the reference agrees), so it has no `ui.skill.*` row and
  no rate curve — but `Job_Haul` still trains it. Deleting it is a Defs change, a `SkillIndex` change,
  a save-format change and a hash change. The other four skills all read and all show.
- **Nothing tests that a click reaches the game.** A PlayMode test cannot press a button (input
  update type `Editor`, so `wasPressedThisFrame` never fires); `FloorToolClickTests` and
  `InputHarnessTests` carry ignored tests. Un-ignore them together the day the harness can. This is
  why this line of work has had three silent failures.
- **A hauler cannot climb a ladder**, so material cannot be carried up. Stairs (`U44`) are the next
  unit rather than a maybe.
- **A ladder now needs a hole left in the floor above it** (2026-09-18,
  `docs/design/21-ladders-and-climbing.md`). A ladder under an unbroken slab is refused at the order,
  and so is a slab poured over a standing ladder: the shaft cell stays open and the colonist steps
  off sideways on to the landing beside it. Nothing migrates — a real floor still counts, so old
  saves and the city's own ladders are untouched — but a player who builds a full upper floor first
  must deconstruct one slab before the ladder will go in.
- **Backing out of the in-game load screen still loses the colony.** Pressing Load with nothing
  readable in the Saves folder is safe now (the colony is untouched and the row says so), but the
  row still tears the world down *before* the list appears, so a player who changes their mind at
  the list has nowhere to go back to. The real fix is showing the browser over a live session, and
  the menu is tied to there being none (`OnSessionChanged` calls `SetShowing(live == null)`). A
  restructure of the start screen's modality, not a guard — `docs/design/17-start-flow.md` §5b.
- **The scenario table is written twice** — `OdysseyBootstrap.ScenarioFor` and
  `SessionRoundTripTests.ScenarioByName` each map two `defName`s by hand. Not urgent (a scenario
  acts only at tick zero) and both copies say so.
- **Nothing obeys the colonist schedule.** The Work tab's right-hand half is real, saved and editable, but the hour a colonist sleeps is still decided by their rest need. It is deliberately outside the state hash while that is true (`docs/design/27-work-tab.md` §12d); wiring it to the job system is the unit that re-bakes the goldens.
- **The forced-order context menu exists, with one row** (2026-09-23, design 33 §7a): right-click a
  weapon offers *Equip*. "Build this now" is not in it yet — it needs `JobSystem.CanForce`'s answer
  published to the snapshot first, because the Hud cannot call the simulation.
- **The presentation half of `OdysseyBootstrap`** is still wired by hand. The simulation half was
  opened by `U34`; this is what is left of that chokepoint.
- **The design documents collide on numbers.** Four pairs share a `15-`/`17-`/`18-`/`19-`/`20-`
  prefix, and the unit numbers `U42`–`U45` meant two different things until 2026-09-18. Renaming
  files would break every cross-reference; it is recorded rather than fixed.

## Read this before losing an hour

`docs/lessons.md` collects the operational lessons that have already cost time once: the Unity batch run that finishes without exiting and locks the project, assembly definitions silently dropping implicit package references, why filtering tests saves nothing, and the working-method rules for parallel agents. **Add to it whenever something takes more than about ten minutes to diagnose.**

**`docs/bug-patterns.md` is the companion for the bugs themselves** — the symptom, the real cause, the
measurement that found it, and the check that catches the next one of its kind. **Read its patterns
before debugging a report**, because this project keeps meeting the same five faults in different
clothes: one rule with two owners; a rule that asks the built world and misses the order; a
compatibility clause keeping the bug alive; a conditional rule applied per cell across a drag;
and **a pass that draws once per cell** (`P10`), which is a performance fault that reviews cannot
see and the frame budget could not either. **Add a row whenever a bug is fixed.**

**For a report about how something *looks*, start at that file's runbook, "a tile that looks wrong".**
One grey tile cost four rounds, three of which produced confident wrong answers reasoned from
screenshots while the save that settled it sat on the same disk. The first move is
`dotnet run --project tools/dotnet/Odyssey.SaveProbe` — it prints every floor, item and terrain in a
save with no Unity. **A cell can hold more than one drawable thing**, and the report will name only
the one the player recognises: that tile was a wood floor *and* rubble terrain, and the pane calling
it "Wood floor" was telling the truth.

The two that come up daily:

- **Tests:** `scripts/test-fast.sh` while working (~1.7 s, no Unity), `scripts/unity.sh test editmode` before committing (authoritative).
- **Fix issues before adding features** (owner rule, 2026-09-15), and re-run after a fix to check the output *means something* — a plausibly wrong result is worse than an obviously broken one.

## Repository layout

- `docs/process.md` the cycle one unit goes through · `docs/audit/` baseline audits (the first is 2026-09-19) · `docs/plans/playtest-queue.md` what is waiting for a person at the keyboard · `docs/lessons.md` operational lessons (read it) · `docs/bug-patterns.md` the bug-pattern catalogue and fix register (read it before debugging a report) · `docs/journal.md` the narrative record of how the build got here (why a decision was made, what was measured, what was later falsified) · `docs/brief.md` governing brief · `docs/research/` research files and `INDEX.md` · `docs/reference/screenshots/` reference images and descriptions · `docs/setup/local-dev.md` dev-machine setup (§8 for Windows). Later phases add `docs/design/`, `docs/adr/`, `docs/plans/`, `docs/milestones/`.
- The Unity project lives at the **repository root** (`Assets/`, `Packages/`, `ProjectSettings/`), created 2026-09-15 on the Windows dev machine (Unity 6000.3.24f1, Universal 3D template).
- `Assets/Odyssey/` the game assemblies: `Sim.Contracts`, `Sim` (both UnityEngine-free), `Tests/Sim`. `Assets/Editor/Odyssey/` editor tooling: `SyntyInventory.cs`, `SyntyImport.cs`, `VisualBlockScene.cs`. `tools/dotnet/` mirror projects for the fast test tier. `Assets/Synty/` licensed packs, ignored by git.
- `scripts/unity.sh` headless Unity wrapper (`inventory`, `test`, `build`, `exec`, `shot`, `open`, `which`; `shot` takes an optional method, e.g. `shot Odyssey.EditorTools.ScatterSheet.Shoot` for a contact sheet of candidate props) and `scripts/test-fast.sh` the no-Unity test tier.
- `docs/wiki/` the generated content wiki (read it, never edit it — see the section above). `tools/wiki/build_wiki.py` builds it; `tools/icons/icons.py` is the icon pipeline (detect, contact, export, validate, emit-web) with 30 tests via `python3 -m unittest discover -s tools/icons -t tools/icons`; `tools/mockups/artifact_body.py` makes a mockup publishable; **`tools/perf/trace.py` reads the performance traces the game writes into `Logs/perf/` while somebody plays** (`summarise` / `compare` / `list`, 14 tests via `python3 -m unittest discover -s tools/perf -t tools/perf`, `docs/design/29-perf-tracing.md`). All three are standard library only, so they run in a container with no Unity.
- `art-source/` owner-owned source art kept **outside** `Assets/` so Unity does not import it. `art-source/icons/sheets/` is where the eight icon sheets go.

## Environment

- **Dev machines:** Pop!_OS (Unity Hub, RTX 5070 Ti) and Windows 11 (`D:\code\odyssey`, Unity CLI/Hub beta — see `docs/setup/local-dev.md` §8). Both run Unity 6000.3.x LTS; neither is the performance target (that is a 2022 mid-range laptop). Keep the project path free of spaces (a Unity-MCP constraint).
- **dotnet SDK 8.0.425** is installed on the Windows machine at `%USERPROFILE%/.dotnet` and powers `scripts/test-fast.sh`. A remote container without Unity can still run every Sim test through it, given an SDK.
- **Python 3.13.15** is installed on the Windows machine as of 2026-09-16 (`%LOCALAPPDATA%\Programs\Python\Python313`, ahead of `WindowsApps` in PATH, with a `python3.exe` copy beside `python.exe` because CPython ships none). The wiki, icon and mockup tooling therefore runs on **both** machines now. `PYTHONUTF8=1` is set for the user and is required: without it Windows Python reads the docs as cp1252 and `build_wiki.py --check` calls every file stale. See `docs/lessons.md`.
- **Blender (optional):** only for gaps no Synty asset fills (a stair or ladder variant at the cell size, UV or atlas fixes, rig or animation retargeting). Synty first. Blender-made pieces go under `Assets/Art/Custom/` and are committed; they must match the Synty style and snap to the cell grid.
- **Unity MCP:** the project's committed `.mcp.json` registers a hosted **AI Game Developer**
  relay (the IvanMurzak/Unity-MCP lineage, `ai-game.dev`) at project scope; the in-manifest
  plugin route in `docs/setup/local-dev.md` §5 is **not** wired up. Once connected, Claude Code
  can open scenes, run EditMode/PlayMode tests, read the console and execute editor C#. Prefer
  `scripts/unity.sh` for anything that must also work in CI.

## Conventions for code (apply from Phase 4 / M0 onwards)

- C# with nullable enabled and analysers on. Assembly definitions per layer: Sim (no UnityEngine dependency where possible), Presentation, Editor, Tests.
- **Pragmatic TDD** (Phase 1 Q5): test-first for every Sim system; a determinism harness (same seed → same state hash) and golden-master one-day headless runs are first-class tests; presentation/tooling get smoke tests; throwaway spikes exempt until kept.
- **Determinism before threads** (Phase 1 Q6): single-threaded fixed-tick sim with tick groups; Burst jobs behind clean boundaries only on benchmark-proven hot paths. Composition root, no scattered manager singletons.
- Sim classes public, unsealed and virtual where cheap, so Harmony-style patching stays possible. Data-driven Defs with inheritance and patch operations from day one.
- Every system that touches a cell is layer-aware (x, y, z) from its first commit. No 2D-first code, ever.
- Scenes and prefab variants are generated by editor scripts, not hand-authored, so they are reproducible.
- Tests run headless via `scripts/unity.sh test`. Each milestone gate is: tests pass, a headless one-day simulation runs with no errors, `docs/milestones/Mx-report.md` written, stop for review.
- Commits: small, one concern each, descriptive message. Never commit `Assets/Synty/`, `Library/`, logs or test results.
- Interface icons are referenced by symbolic key, never by filename, and are 64 px, point-filtered, uncompressed, no mips, displayed at 32 and 64 only (`docs/adr/0007-pixel-art-icon-pipeline.md`).
- Content changes carry their regenerated wiki and label registry: `python3 tools/wiki/build_wiki.py --check` and `python3 tools/wiki/emit_labels.py --check` both pass before the commit.

## Starting a local session

Run `claude` in the repository root; this file is read automatically. Useful first prompts:

- "Read CLAUDE.md and docs/research/INDEX.md. Run the procedure in docs/research/synty-import.md, fix SyntyInventory.cs if it does not compile, commit the inventory, and report the implied cell size."
- "Phase 1 answers are: … Record them in docs/research/phase1-answers.md, update CLAUDE.md status, then start the Phase 2 lanes that do not depend on the inventory."
