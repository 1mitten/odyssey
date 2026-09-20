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
| **WS** rates | **`WS1`–`WS3` in** (`WS1`–`WS4` renumbered from `U42`–`U45`, which were taken): the per-mille seam, work speed from the skill curve with the stroke clock scaled by it, and innate pace with starvation on both rates and collapse at zero rest. `WS4` running is **held** — do not invent an urgency model. Save format 6. **Reviewed and fixed 2026-09-18** (`docs/journal.md`): `ToilProgress` counts milliwork in **every** driver including the rate-free ones, or one saved and hashed field carries two units; the four accumulators are hashed **whole**, not divided back; `starvationPerInterval` was four times faster than its own comment (the needs cadence is 400 intervals a day, not 200); `RollSeed` is a property whose setter drops the cached pace; and arrival beats collapse, so a colonist cannot go down on her own bed and be told she slept on the ground. All three goldens re-baked — **measured** to be the hash seeing more rather than the colony doing anything different. |
| **RP** roster paging | **Done.** Overflow pagination with right-docked toolbar widget (`<` / `>`), mouse wheel page cycling, selection synchronization on 3D click/alerts, right-click drag-and-drop slot swapping (A ↔ B) with drag ghost and edge-paging, and view persistence in `ViewStateSection` v2. |
| **CL** the carried load | **Merged 2026-09-19, PR #129**, played once, three faults fixed (`docs/design/24-carrying.md`). A load no longer vanishes when it is picked up: it rides the palms, so it travels up out of the lift's crouch with the hands and lowers again on the stow. The stoop and the grasp instant were already there and are untouched. The arms take an authored scoop and the cradle is **measured off the palms**, not solved to a point — arm length varies across the 61 rigs by more than the cradle does. Sim side is one gesture report (`DropCarried` reports the stow, reversing a deliberate silence) and two sparse aspects; neither is saved or hashed, so **no golden moved**. The armful is constant whatever the stack, so the amount now lives on the activity line and nowhere else. **In water the load is hidden**, a placeholder the owner asked for by name. The carry path is **per-nothing**: a new commodity inherits the hold, the turn and both hand-overs, and only opts in to being drawn as an armful (§9a). |
| **GR** growing zones | **In review — PR #119**, branch `claude/growing-zones` — `U46`–`U50`: carrot crop, a paint-a-zone tool in the palette and the orders strip, sow → daylight-window growth → harvest → auto re-sow. One raw-food commodity; cooking, spoilage, seeds and seasons are recorded hooks (`docs/design/22-growing.md`, which arrives with the PR). Growing carries no rate curve yet, so a skill still buys nothing at the hoe. The debug menu gained **Skip one day** and **Ripen crops** so the harvest can be seen without the four-day wait (`docs/design/18-debug-menu.md`). It has had its first play day — nine owner looks, six fixes: the sower kneels rather than chops, the zone is a near-black whole-tile cover, the ground is the terrain itself re-looked as earth, seeds speckle only under the kneel, the big carrot stage arrives at 85% so what looks pickable nearly is, and the pane reads Carrot × 5 — N% grown. |
| **EV** events | **Merged 2026-09-20 (PR #140)** — the incident layer: `IncidentDef` with gates and a named worker, `CanFireNow` / `TryExecute`, the saved and hashed incident ledger, the skyfaller, and the Events panel under the alerts. One event, the **supply drop**: the debug menu's *Events* tab (one row per incident Def, built from the content) drops ten to twenty meals from the sky on to the topmost walkable cell of a random column, anywhere on the board, and the colony hauls them. **No storyteller** (owner: debug menu only for now); the cadence vocabulary is mapped and the seams named in `docs/design/23-events-and-storyteller.md`. **First look 2026-09-20, four fixes** (§9): six seconds at one speed from 120 m, above the camera; the Events row jumps the camera only, not the slice; the Events tab; the chime's tail fades and the music swells back over a second. |
| **HT** hardening | **Audited 2026-09-19; audit and plan merged as PR #136. Nothing built.** `docs/audit/2026-09-19-baseline.md` is the baseline audit — scalability measured at the scale target, the monoliths, the process, and everything not yet addressed — and `docs/plans/vertical-slice.md` §HT is the ordered list of hardening units that came out of it. The first finding with a number: a tick that edits one cell at 250 × 250 × 40 costs 1.19 ms against 0.065 ms at rest, all of it `NavGraph.Rebuild` recomputing every district. **Phase gate: the plan is written and waits for approval; no unit is started.** |
| **WT** the Work tab | **In review — PR #145**, branch `claude/happy-tesla-2onz0q`, merged with `main` and corrected on a worktree 2026-09-20 (`docs/design/27-work-tab.md`). One table on F1: the twenty-two-column priority grid and the twenty-four-hour day share one frozen column of names, so a row is one colonist's whole day. Schedule is a new mechanic (`Pawn.ScheduleHours`, save format **6 → 7**, published hour by hour) and is **deliberately outside the state hash because no system reads it** — `ScheduleTests.EditingTheDayDoesNotMoveTheStateHash` is the assertion, and the day it fails is the day to re-bake. **The review moved seven things** (§15): Simple mode's tick and cross were characters **neither shipped font has**, so that whole mode drew empty boxes; the panel's fixed 1,756px hung off any screen under ~1,780; Escape did not close it; opening Build did not put it away; `Describe` was wired to no tooltip; `Attach` did not re-sync it across sessions; and `WorkGridLayout.Hours` was a literal `24` under a comment claiming it was `ScheduleHandle.Hours`. |
| **FI** falling items | **Merged 2026-09-20 (PR #138)** — `docs/design/26-falling-items.md`. Items and deconstruction refunds resting on destroyed floors or cleared cells drop onto the first solid floor below (or despawn if over void). Visual downward fall with gravitational acceleration ($t \propto \sqrt{h}$) and `SoundIds.CarryDrop` on landing. Fast tier (759 Sim, 449 Hud), EditMode (1902 passed, 0 failed), PlayMode (77 passed, 0 failed). |
| **WT** the Work tab | **Built on `claude/happy-tesla-2onz0q`, PR #145** — `docs/design/27-work-tab.md`. **One tab, one table: what they do on the left, when on the right, sharing one frozen column of names.** F1 or the bar item; click cycles, right-click cycles back, shift paints the column — the same three gestures in both halves. Twenty-two `ui.work.*` columns (four live, eighteen drawn as *not built yet*, a third state and deliberately not the incapable grey) plus the 24-hour band and a now-line off `GameClock`. **1,756px at our 34px pitch, which fits 1920; at the supplied spec's 40px it would be 2,026 and would not.** **Schedule has left the command bar** and F2 is free. **The schedule is authored, saved (format 7), published and editable — and obeyed by nothing yet**, so it is deliberately *outside* the state hash and no golden moved; `ScheduleTests.EditingTheDayDoesNotMoveTheStateHash` fails the day that changes, which is the signal to re-bake. `HudKey` gained its first function key. Drag-paint and presets are not built (OQ-W5, OQ-W6). |

**Work reaches `main` only through a pull request** with both tiers green, one approving review and
the branch up to date. Branch protection enforces it, agents included. `claude/*` branches are
per-change and short-lived; there is no long-lived feature branch.

### Read this before touching that line

Every live line has a design document that holds its decisions, its measurements and the things not
to undo by tidying. **Read the document before changing the code**, and add to it rather than to
this file.

| If you are touching | Read |
|---|---|
| Falling items, mid-air drops, landing motion | `docs/design/26-falling-items.md` |
| The Work tab, priorities, the rotated headers | `docs/design/27-work-tab.md` |
| Walls, sites, materials, the build botch | `docs/design/15-building.md` |
| Cancel, deconstruct | `docs/design/16-cancel-and-deconstruct.md` |
| The Build palette's three layouts | `docs/design/17-build-palette-layouts.md` |
| Floors, slabs, support, collapse | `docs/design/17-floors-and-collapse.md` |
| Paving | `docs/design/18-paving.md` |
| Beds, furniture, quality tiers, who owns one | `docs/design/20-beds.md` |
| How a pile on the ground says its size | `docs/design/24-pile-reading.md` |
| The debug menu | `docs/design/18-debug-menu.md` |
| The start screen, saving, loading | `docs/design/17-start-flow.md` |
| Colonist select | `docs/design/18-colonist-select.md` |
| Naming a colonist, the setup page | `docs/design/19-world-setup.md` §10 |
| What a colony starts with | `docs/design/22-starting-kit.md` |
| Growing zones, crops | `docs/design/22-growing.md` (arrives with PR #119) |
| Events, incidents, the supply drop, the Events panel | `docs/design/23-events-and-storyteller.md` |
| Text entry taking the keyboard | `docs/design/09-ui-and-input.md` §6a |
| Avatars and portraits | `docs/design/20-avatars.md` |
| Ladders, the shaft rule, the climb pose, what a click may land on | `docs/design/21-ladders-and-climbing.md` |
| Tree colour | `docs/design/21-tree-colours.md` |
| The sub-tile sidestep, crowd and tree avoidance | `docs/design/25-pawn-steering.md` |
| Where a colonist looks, the head turn | `docs/design/23-head-turning-and-gaze.md` |
| Terrace steps, banks, what may stand at the foot of one | `docs/design/22-terrace-steps.md` |
| Water, swimming, the float | `docs/design/20-swimming-and-water.md` |
| Picking up, carrying, putting down, the armful | `docs/design/24-carrying.md` |
| The Work tab, work priorities, the schedule grid | `docs/design/27-work-tab.md` |
| Work and move rates (WS) | `docs/design/17-rates-and-stats.md` |
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
- **A character the interface writes must exist in both shipped fonts, and a test reads the files
  to check.** The HUD ships Archivo Narrow and IBM Plex Mono; a label's face is picked by its
  `HudTextRole` and the `numeric` flag, and roles move. `HudFontTests` parses both `.ttf` cmap
  tables in the fast tier and fails on any non-ASCII character in a literal under `Odyssey.Hud` or
  `Odyssey.Presentation` that either face cannot draw. **Nothing else can catch it**: the fast tier
  has no text engine and the Unity tier asserts no pixels, so a missing glyph is a silent blank in
  both. It has already happened twice — the Work tab's Simple mode and the bed-owner picker's tick,
  the second of which shipped to `main` and was never drawn. **Draw the shape as a `HudGlyph`**
  rather than reaching for a Dingbat; the project draws its own icons for exactly this reason.
  `docs/bug-patterns.md` P10.
- **An order's colour has one owner, and it is `Odyssey.Hud.OrderColours`.** The chip in the orders
  strip, the palette header, the drag cursor and the mark left on the board are all the same hue.
  There were two tables in two assemblies for months and they disagreed on two of the four tools —
  deconstruct was orange on the panel and the *cancel* red on the ground. Never write a `Color` for
  an order in Presentation; ask. `OrderColoursTests` runs in the fast tier and walks every tool.
- **Where an order's mark sits is `WorldRenderModel.MarkHeight`** — the top of the cell for
  anything that fills it, the top of itself for anything that stands up without filling it, the
  floor for everything else. Trees are on the floor deliberately.
- *Subsystems* are simulation-side; *directors* are presentation-side. Do not unify the two words.

### Fixed decisions

- **Cell size: 2.5 × 2.5 × 3.0 m** (ADR 0002), irreversible. Half-heights and slopes are a drawing
  offset, never a cell.
- **Architecture: plain C# structure-of-arrays with Burst on measured hot paths** (ADR 0005),
  decided by a benchmark in which both candidates produced the identical state hash.
- **Determinism before threads.** Single-threaded fixed-tick sim with tick groups.
- **No multiplayer, ever.**

### Tests and gates

- **Fast tier** (`scripts/test-fast.sh`, ~35 s, no Unity): **826 Sim + 523 Hud** (2026-09-20, the reviewed Work tab branch merged with main); Long tier **21**.
  **It compiles neither Presentation nor Editor**, so a unit touching the composition root or the
  HUD shell is unproven until Unity has compiled it, however green the seconds look.
- **Unity tier** (`scripts/unity.sh test editmode`, authoritative), last run 2026-09-20 on the
  reviewed Work tab branch merged with main (doors included): EditMode **2,104 total, 2,091 passed,
  0 failed**; PlayMode **85 total, 80 passed, 0 failed**. The remainder are `[Explicit]` or ignored.
  The run before it, the same day on `claude/colonist-figures-and-portraits`, was EditMode
  2,003 / 1,990 and PlayMode 85 / 80.
- **Do not run the PlayMode tier while another Unity batch run is going.** It carries the timing
  tests, and `HudStressTests` failed at 3.770 ms against a 1.167 ms budget beside two other
  `unity.sh` runs and passed at 0.603 ms alone, on the same commit. **The baseline the test logs is
  the tell** — it moved 2.5× between the two and a real regression would have left it alone. Check
  `Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"` first, and wait for
  `TestResults/PlayMode.xml` to be *newer* than the run you started rather than merely to exist:
  the previous run's file sits there until the new one finishes. `docs/lessons.md`.
- **The runner has no `Assets/Synty`, so its PlayMode count is lower than this machine's and that
  is correct.** Everything that needs a colonist's art ignores itself there — on 2026-09-20 the
  same commit was 85/80/0 here and 85/75/0 with ten ignored on the runner. **A test that needs the
  packs must ask whether the art *resolved*, never whether there is a catalogue**: the catalogue is
  committed and its prefab references point into the gitignored folder, so it loads perfectly with
  every reference null on exactly the machine that can draw nobody. `PortraitStudio.Available` and
  `PawnFigureDirector.Enabled` are the two right questions; a `moduleCatalogue == null` check is
  the wrong one and has now turned the runner red twice.
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
- **Content gates:** `python3 tools/wiki/build_wiki.py --check` and
  `python3 tools/wiki/emit_labels.py --check`. Both must pass before a content commit.
- The two tiers **do not run the same NUnit**, and the fast tier's is newer; **a frame is not a
  tick**. Both traps are in `docs/lessons.md` and both have cost a Unity run.

Frame time under the real player loop, against a 5 ms budget: meadow ~0.99 ms, city ~1.56 ms on an
RTX 5070 Ti at 640 × 480. The city's move from 0.88 to 1.56 ms is **unexplained** and still open.

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
- **A colonist can still lie down inside a terrace bank.** Trees are guarded out of those cells at
  generation (`TerraceFoot`, `docs/design/22-terrace-steps.md`), and a walking figure is lifted onto
  the ramp, but a body lying down is not: sleep on the ground at the foot of a step and the façade
  hides you. §4 of that document holds the two candidate fixes and why neither was guessed at — both
  move the state hash. An item dropped in one has the same problem and is unreported.
- ~~A skill level buys nothing a player can feel~~ — **stale since WS2/WS3 (2026-09-18) and
  caught by the audit a day later**: work speed reads the skill curve and pace reads condition.
  Kept struck through for one release as the example of the failure this section warns about.
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
- **Forced orders have their simulation half only** — steps 3 and 4, the right-click/drag split and
  the context-menu panel, are not started, so nothing in the running game can send one.
- **The presentation half of `OdysseyBootstrap`** is still wired by hand. The simulation half was
  opened by `U34`; this is what is left of that chokepoint.
- **The design documents collide on numbers.** Four pairs share a `15-`/`17-`/`18-`/`19-`/`20-`
  prefix, and the unit numbers `U42`–`U45` meant two different things until 2026-09-18. Renaming
  files would break every cross-reference; it is recorded rather than fixed.

## Read this before losing an hour

`docs/lessons.md` collects the operational lessons that have already cost time once: the Unity batch run that finishes without exiting and locks the project, assembly definitions silently dropping implicit package references, why filtering tests saves nothing, and the working-method rules for parallel agents. **Add to it whenever something takes more than about ten minutes to diagnose.**

**`docs/bug-patterns.md` is the companion for the bugs themselves** — the symptom, the real cause, the
measurement that found it, and the check that catches the next one of its kind. **Read its patterns
before debugging a report**, because this project keeps meeting the same four faults in different
clothes: one rule with two owners; a rule that asks the built world and misses the order; a
compatibility clause keeping the bug alive; and a conditional rule applied per cell across a drag.
**Add a row whenever a bug is fixed.**

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
- `docs/wiki/` the generated content wiki (read it, never edit it — see the section above). `tools/wiki/build_wiki.py` builds it; `tools/icons/icons.py` is the icon pipeline (detect, contact, export, validate, emit-web) with 30 tests via `python3 -m unittest discover -s tools/icons -t tools/icons`; `tools/mockups/artifact_body.py` makes a mockup publishable. All three are standard library only, so they run in a container with no Unity.
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
