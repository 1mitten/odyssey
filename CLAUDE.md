# Odyssey — project guide for Claude Code

Read this first, then `docs/brief.md` (the governing brief) and `docs/research/INDEX.md` (what is known so far). Update the **Current status** section whenever it changes.

## What this is

A prototype colony sim in the RimWorld mould, in true 3D with discrete vertical layers, set in a ruined sci-fi city. Unity 6.3 LTS (6000.3.x), URP, C#. Art: Synty POLYGON Sci-Fi City. The owner does visual work in the editor on Pop!_OS; Claude Code does code, tests, research and documentation.

## Working agreement (from the brief; these hold everywhere)

- **Phases with hard stops.** Ground → Interview → Research → Plan → Execute. End the turn after each phase and wait. Never run a later phase on assumed answers. No gameplay code until Phase 4, and only after the plan is approved.
- **Research lives in subagents.** One subagent, one question, a hard cap ("stop after N searches / N reads"), fixed return format: Findings, Sources (URLs), Confidence (high/medium/low), Could not be determined. Write each result to `docs/research/<slug>.md` and keep `docs/research/INDEX.md` current (the naming scheme is in it).
- **Commit to recommendations.** Rank options and back one. If two are tied, name the observation that breaks the tie and the cheapest experiment that gets it.
- **Clean room.** Study RimWorld's mechanics, formulas, data shapes and design intent; never paste Def XML, decompiled code, art, audio, names or flavour text into this repo. Never decompile into the repo. Invent our own names.
- **Licensed assets stay licensed.** Synty content lives only under `Assets/Synty/`, which is gitignored. Never copy it elsewhere, never commit it, and never let the simulation or its tests depend on it (clones without the pack must still build and run headless).
- **Files outlive context.** Every phase produces files under `docs/`. Assume the next session knows nothing except what is written down.
- British English in documentation. No multiplayer, ever. *Ramble* (Godot) is reference only, no code reuse.

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

```
python3 tools/wiki/build_wiki.py            # rebuild docs/wiki
python3 tools/wiki/build_wiki.py --check    # exit 1 if stale; run before committing
python3 tools/wiki/emit_labels.py           # rebuild Assets/Odyssey/Hud/Registry.g.cs
python3 tools/wiki/emit_labels.py --check   # exit 1 if stale; run before committing
```

The HUD reads its labels from the same file: `emit_labels.py` generates `Registry.g.cs`
(`Registry.Label(key)`), `JobLabels` and `LedgerModel` name nothing themselves, and
`RegistryTests` fails the fast tier on any key the CSV does not know. A content commit runs both
checks.

Both `--check`s are the gate and belong in CI beside the test tiers. Two notes before extending
it. The registry is hand-authored **only until the Def set covers it**: then `icon-keys.csv` is generated
one way out of the Defs and committed, so the wiki and the build-gating icon tests share one
source. Do not create a second source of truth meanwhile. And the hosted copies are snapshots:
after a rebuild, republish `docs/wiki/artifact.html` and
`docs/reference/mockups/hud-v2.artifact.html` (regenerate the latter with
`python3 tools/mockups/artifact_body.py docs/reference/mockups/hud-v2.html`).

## Current status

- **Phase 0 (ground): complete 2026-09-15.** The import spike and asset inventory ran on the **Windows** dev machine: Unity 6000.3.24f1 LTS + URP 17.3.0 project at the repository root, five Synty packs imported headless under `Assets/Synty/` (7,222 assets, zero import errors). Record in `docs/research/synty-import.md`; measurements in `docs/research/synty-inventory.md`.
- **Phase 1 (interview): complete 2026-09-15** — all seven answers in `docs/research/phase1-answers.md`: packs supplied (Q1), cell confirmed (Q2), template stamping (Q3), IvanMurzak/Unity-MCP now (Q4), pragmatic TDD (Q5), two-way architecture benchmark with determinism-first threading (Q6), Windows machine primary (Q7).
- **Phase 2 (research): complete for the slice, 2026-09-15** — wave 1 (twelve files: Lane A pawns/jobs/building/mapgen/tick, Lane B Going Medieval, Lane C Cataclysm DDA, Lane E all four Synty files, Lane F prior art, which found a *Ramble* checkout at `D:\code\ramble`) plus wave 2 (A14 stockpiles, **D1 architecture benchmark**, D4 pathfinding, D6 save/load, D7 Defs, D8 CI). One-line results per file in `docs/research/INDEX.md`. Deferred until after the slice, by the Q8 fast-track: A2/A5–A11/A13/A16 and the remaining Lane B/C items. **Interface/UI research is owned by a separate agent session — do not duplicate it here** (`docs/design/ui-plan-reconciliation.md`).
- **Where this is, 2026-09-16.** M0 is closed (U08 CI landed: two tiers, the Unity one on the owner's
  machine, branch protection requiring both). M1 and M2 are done in substance and went further than
  planned — colonists walk, chop and have needs, mood and skills; a colony survives ten headless days
  on three seeds; there is a HUD, a naming registry, terrain relief, water and a work-pose system,
  none of which the plan asked for. M3 has started ahead of itself: designations, felling and
  stockpiles are in and mining is written on `claude/mines`, unmerged. **`OQ-20` closes M2.**
  **Before more features, `OQ-44` to `OQ-46` open the seams** — the mining line is 73 files and had
  to edit six shared files to add itself, five of which should have been extension points. The
  reasoning and the order are in `docs/plans/vertical-slice.md` under "Where the seams are".
- **Phase 4 (execution): M0 complete, M1 under way on branch `claude/m1-world`.** U01-U07 done, and the whole simulation stack runs: grid, support solver, two worldgen paths, pathfinding, pawns, save/load, Def loader, subsystem schedule, and the snapshot-read / intent-write seam. M1 presentation has instanced chunk rendering (no GameObject per cell), a slice camera rig, click-to-inspect and a generated play scene (`scripts/unity.sh exec Odyssey.EditorTools.PlayScene.Build`). **488 EditMode tests (486 green, 2 skipped) plus 7 PlayMode**, and 326 Sim plus 29 Hud in the fast tier (1.7 s fast tier, 37 s Unity gate, `unity.sh test playmode` for frame time). **Frame time is measured only by `FrameTimeTests` under the real player loop** — wooded meadow 0.41 ms, city 1.48 ms on the RTX 5070 Ti at 640 × 480 (2026-09-16, grass to the rim and trees) — never by an editor `camera.Render()` loop, which measures its own history (`docs/lessons.md`, "Benchmarking the renderer"). M1 report: `docs/milestones/M1-report.md`. **U08 CI is done (2026-09-16).** `.github/workflows/ci.yml` runs two tiers on every push and pull request. *Fast tier*, on GitHub-hosted Linux: the Sim and Hud tests, the Long tier, the content-registry check and the icon tooling tests. *Unity tests*, on the owner's Windows machine as a self-hosted runner (labels `self-hosted`, `windows`, `unity`, switched on by the repository variable `UNITY_RUNNER=1`): EditMode, PlayMode and the headless one-day simulation, results published as a check by `dorny/test-reporter`. First green end to end on `bcf40d9`: **EditMode 398 total, 396 passed, 0 failed** (the two `[Explicit]` benchmarks skipped), **PlayMode 4/4**, the headless day clean, the whole job in 1 m 46 s. Branch protection on `main` requires both checks, a pull request and one approving review, with branches up to date; so **nothing reaches `main` except through a pull request with both tiers green** — agents included. Two runner traps are in `docs/lessons.md`, "Continuous integration". Plan: `docs/plans/vertical-slice.md`.
- **The starting map is a wooded meadow (owner decision 2026-09-16; barren before that), now with water in it:** 120 x 120 x 16, flat, grass in every cell, woodland at the natural generator's density with a clearing at the start, a stream or two and a pond or two kept clear of that clearing (2026-09-16, ADR 0009), and no rock, ore or props — `NaturalMapGenDef.MakeWooded()`, chosen by `OdysseyBootstrap.woodedMap`. Trees are edifices that block nothing (a colonist walks through woodland) and felling them is the first job on the road to building. The bare board (`MakeBarren()`, `woodedMap` off) is kept as the test baseline on which anything that is not grass is a bug. The ruined-city generator is still present and still tested, but it is not what the scene loads. Tufts of grass are drawn over the ground as pure decoration by the mesher (`GroundScatter`, `OdysseyBootstrap.grassScatter`, default 120 per hundred cells, 0 to switch off); they are not simulation objects, block nothing and are not in the save.
- **Designations and the first job line, 2026-09-16.** The player's standing orders live in `Sim/Designations/DesignationGrid.cs` (one byte per cell; Mine, Deconstruct, Fell; validated on placement, hashed, saved, published as the snapshot's `Designations` channel one byte per cell of the active layer). Player commands reach the colony through the intent bus: `Designate(cell, A = kind)`, `CancelDesignation(cell)` and `SetForbidden(A = thing, B = on)` are handled by the components that own them via `SimWorldBuilder.AddIntentHandler`, and the colony is wired in one place, `ColonyComposition.AddColony`, used by `ColonyWorld`, the bootstrap and the screenshot harness alike. **Felling is the first job that edits the world:** `FellWorkGiver` (work type *cutting*, scanned before hauling) hands a marked, reachable tree to a colonist; `FellJobDriver` walks into the tree's cell, works `Job_Fell.workTicks` (600, **ASSUMED**), clears the order, and defers the edit to the structural phase, where the tree goes and `WoodPerTree` (20, **ASSUMED**) wood appears as one stack that the existing haul takes to the stockpile. What the colony starts with is a `ScenarioDef` (`Sim/Pawns/ColonyScenario.cs`: colonists, meal piles, meals per pile, beds, stockpile cells, salvage, `startingFellRadius`), built in code like `PawnContent.Core()`; the scene's `ScenarioDef.Playtest` marks every tree within 10 cells of the start before its first tick so the colony has work at once, `ScenarioDef.Bare` gives the same colony and no orders and is what headless runs and tests build on, and `OdysseyBootstrap.scenario` picks one by name — it flips to Bare when the UI line's drag-to-designate tool lands (that line owns the tool and reads the snapshot channel). Wood is `ItemIndex.Wood`, drawn as a log pile, named `ui.res.wood` in the registry with `ui.arch.tool.fell` for the order. Not yet: felled trees are not in the save (the grid itself is not saved, OQ-08). **Stacks merge (OQ-24, 2026-09-16):** a cell has space for a load only when the whole load fits under the def's `stackLimit` (wood 75, meals 20 so a starting pile is one stack, salvage 1); a haul dropped onto a stack of the same def joins it; the destination rule is filter, then space, then priority, then nearest; and a hauler with nothing loose to carry re-stows from a lower-priority pile into a strictly higher one that accepts the thing (`ColonyItems.StoredItems`, `HaulWorkGiver`). `StockpileTests` holds the four-pile fixture.
- **Skills (U20) landed 2026-09-16 (OQ-14).** A colonist has experience per skill (`SkillIndex`: hauling, cutting), a `Passion` per skill rolled once at placement from the seed and the pawn id, and a level 0–20 that `Pawn.SkillLevel` reads off the experience by the `SkillDef` table and is never stored. Work grants it: a job driver calls `Work(ctx)` on the ticks that are the work (the fell swing, the haul carry), which pays `JobDef.experiencePerWorkTick` into `JobDef.trainsSkill`, scaled by passion (×0.35 / ×1.0 / ×1.5, `a-01-pawns.md`) and slowed to a fifth after 4,000 points in a day; `SkillSystem` decays levels ten and up on the Long tick group. Experience is stored in thousandths of a point so every rate is an integer. Experience, passions and the day counter are hashed and saved (`SkillTests`, 13 tests). **ASSUMED** and marked in the code: the base rate per work tick (110 thousandths), the passion odds at spawn (35 % minor, 15 % major), the nine decay-ladder values between the two `a-01` measured, and the second slope of the level table (2,000 a level from 10, which is what reconciles `a-01`'s formula with its 265,000 total). Not carried: the 1,000-point grace before a level is lost (needs a stored level), the passion mood buff, and any effect of level on work speed — nothing reads the level yet except decay, and the HUD's Skills tab still says "no skill data yet" because `PawnView` does not carry it (a contract change for the UI line).
- **Mining, rock and caverns, 2026-09-16** (`docs/research/mining-interview.md` — four rounds of interview, the plan, and every departure from it). **The board has a mine under it.** `MakeWooded()` is a cover mode rather than the barren board with trees put back, so terracing, rock outcrops, ore and caverns all reach the board that is played. **Depth is what is left after headroom** — the ground sits as high as it can while leaving `headroomLayers` (3) of sky above the tallest terrace. It used to sit two fifths of the way up, leaving two layers of rock, so coal (band 7-22 cells down) *could never generate at all*; `NaturalDepthTests` measures that now. **Caverns** (pass 8, before ore, `NaturalGenPurpose.Caverns = 9`) are sealed: no mouth, carved strictly inside each column's rock band, found by mining into one. **Ore is invisible until a face is cut in it** — `CellFlags.Discovered` (bit **6**; deep water took bit 5 on main), a one-way latch set on the six solid neighbours of a mined cell, with the render mirror substituting plain rock for an undiscovered seam. **Mining is the second job that edits the world:** `MineWorkGiver` (work type *mining*, scanned after cutting and before hauling) and `MineJobDriver`, priced per material from `TerrainAt(terrain).workToClear` (rock 700, iron 900, coal 760) rather than one number on the job def, and banked **on the cell** so a miner who breaks off does not throw the morning away. A seam yields `OrePerCell` (15); plain rock yields `StonePerRock` (8) with `StoneChanceOneIn` (1), rolled from (world seed, **cell index**) so the answer belongs to the cell and survives a save. **Bedrock refuses the order** rather than quoting its 2,400 ticks, and so does the ground under a standing tree. **Three stances, in order: beside, on the rim a layer up, on top** — the rim only where the rock's own ceiling is open, or the giver hands out stances at buried rock; `WorkStyle.Dip` aims the stroke 45° down when the work is below the feet (measured: the edge lands 0.18 m above the boots against 1.34 m level) and the reach it costs is measured separately, or the stand solves 0.43 m too far back. **Spoil falls and merges**: `CellGrid.FirstFloorAtOrBelow` is the bottom of the fall for items and people alike, and landing merges up to the stack limit — 107 stacks became 81 on the same stone. `ItemHeap` draws rubble as 2-7 small rocks scattered over the floor rather than one cairn. **A shaft is climbed, not laddered** (`ConnectorKind.Climb`, `MoveCost.ClimbUp` 270 / `ClimbDown` 50): ladders stay a built thing the generator puts in buildings, nothing is drawn in a shaft, and `ApplyClimbPose` puts the hands on the rock. **A climb needs a block beside it and keeps needing one** — laid only where a face stands next to the lower cell, retired when that face is mined away, and it lands **on top of that block**, not in the air above the hole, or the pit seals itself shut. `NavFlags.ClimbOnly` splits the two questions `Walkable` was answering at once: a rock face is somewhere to be and not somewhere to walk to, so you may step **off** one onto ground and never onto one. Names: `ui.res.stone`, `ui.res.ironore`, `ui.res.coal`, all three art gaps. **Not yet:** the climb is free (no materials, no work — the building line's to price), mined cells and climbs are not in the save (OQ-08 again), mining collapses nothing (U29 keeps that), and a sealed cavern is still visible if the player scrolls the layer down — there is no fog of war, so what the ore-sight rule protects is the treasure, not the chamber.
- **Mining, rock and caverns, 2026-09-16** (`docs/research/mining-interview.md` — four rounds of interview, the plan, and every departure from it). **The board has a mine under it.** `MakeWooded()` is a cover mode rather than the barren board with trees put back, so terracing, rock outcrops, ore and caverns all reach the board that is played. **Depth is what is left after headroom** — the ground sits as high as it can while leaving `headroomLayers` (3) of sky above the tallest terrace. It used to sit two fifths of the way up, leaving two layers of rock, so coal (band 7-22 cells down) *could never generate at all*; `NaturalDepthTests` measures that now. **Caverns** (pass 8, before ore, `NaturalGenPurpose.Caverns = 9`) are sealed: no mouth, carved strictly inside each column's rock band, found by mining into one. **Ore is invisible until a face is cut in it** — `CellFlags.Discovered` (bit **6**; deep water took bit 5 on main), a one-way latch set on the six solid neighbours of a mined cell, with the render mirror substituting plain rock for an undiscovered seam. **Mining is the second job that edits the world:** `MineWorkGiver` (work type *mining*, scanned after cutting and before hauling) and `MineJobDriver`, priced per material from `TerrainAt(terrain).workToClear` (rock 700, iron 900, coal 760) rather than one number on the job def, and banked **on the cell** so a miner who breaks off does not throw the morning away. A seam yields `OrePerCell` (15); plain rock yields `StonePerRock` (8) with `StoneChanceOneIn` (1), rolled from (world seed, **cell index**) so the answer belongs to the cell and survives a save. **Bedrock refuses the order** rather than quoting its 2,400 ticks, and so does the ground under a standing tree. **Four stances, in order: beside, on the rim a layer up, from below, on top — and within each, square on to a face before round a corner** (owner, 2026-09-16: "they should place themselves in front of the block"). A miner is *drawn* stepping in towards what it cuts, so on a diagonal it steps towards the block's corner and into the two cells sharing it — the ones most likely to be solid stone when cutting a face. `NearestOfRing` tries the four faces before the four corners. Measured on the played board: 1,500 of 1,980 same-layer stances were diagonal, 961 of them with a face available and reachable anyway; afterwards 539 diagonal and **none** with a face going spare, and not one cell became unmineable. The residual 539 genuinely have no reachable face, and they want the *drawn* figure to stand further back — a body clears the corner-sharing cells only from about 2.4 m out along the diagonal against 1.65 m square on, and that is presentation's to fix, still open. The older wording of this line, kept because the reasoning still holds: **beside, on the rim a layer up, on top** — the rim only where the rock's own ceiling is open, or the giver hands out stances at buried rock; `WorkStyle.Dip` aims the stroke 45° down when the work is below the feet (measured: the edge lands 0.18 m above the boots against 1.34 m level) and the reach it costs is measured separately, or the stand solves 0.43 m too far back. **Spoil falls and merges**: `CellGrid.FirstFloorAtOrBelow` is the bottom of the fall for items and people alike, and landing merges up to the stack limit — 107 stacks became 81 on the same stone. `ItemHeap` draws rubble as 2-7 small rocks scattered over the floor rather than one cairn. **A shaft is climbed, not laddered** (`ConnectorKind.Climb`, `MoveCost.ClimbUp` 270 / `ClimbDown` 50): ladders stay a built thing the generator puts in buildings, nothing is drawn in a shaft, and `ApplyClimbPose` puts the hands on the rock. **A climb needs a block beside it and keeps needing one** — laid only where a face stands next to the lower cell, retired when that face is mined away, and it lands **on top of that block**, not in the air above the hole, or the pit seals itself shut. `NavFlags.ClimbOnly` splits the two questions `Walkable` was answering at once: a rock face is somewhere to be and not somewhere to walk to, so you may step **off** one onto ground and never onto one. Names: `ui.res.stone`, `ui.res.ironore`, `ui.res.coal`, all three art gaps. **Not yet:** the climb is free (no materials, no work — the building line's to price), mined cells and climbs are not in the save (OQ-08 again), mining collapses nothing (U29 keeps that), legs are missing from the climb pose, and a sealed cavern is still visible if the player scrolls the layer down — there is no fog of war, so what the ore-sight rule protects is the treasure, not the chamber.
- **Climbing is gone; a colonist jumps one block and no more (owner, 2026-09-16).** This supersedes
  the climb described in the bullet above — `ConnectorKind.Climb`, `MoveCost.ClimbUp`/`ClimbDown`,
  `NavFlags.ConnectorClimb`/`ClimbOnly`, `EnsureClimb`/`RemoveClimbAt` and `HasWallBeside` are all
  removed. **The whole of unaided vertical movement is a hop**: one block up into the column next
  door is a jump (`MoveCost.JumpUp` 270), one block down off it is a drop (`MoveCost.Drop` 50), and
  anything deeper wants a ladder, which is built. It is *not* straight up — that is what a climb
  did, and the cell it landed in had no floor. **Every cell a pawn can be in now has something
  under it**, which is what makes hanging in mid-air impossible rather than merely discouraged, and
  is why `CanWalkInto` collapsed back into `CanEnter`. **You jump onto ground, not up a storey:**
  `NavGraph.UpperEndIsABlockTop` requires the upper end to stand on solid *terrain*, or every floor
  of every building would be one hop from the one below it and stairs would be decoration.
  The owner's three playtest reports — sticking on faces over one block, climbing where nothing
  should be climbed, repeating a failing route for ever — were one mechanism, pinned by
  `VerticalMovementTests` before anything changed.
  - **A hop is three seams, and missing any one of them fails silently.** The cell search
    (`PathFinder.RelaxHop`), the region graph (`NavGraph.TryHopEdges`, or reachability says no and
    the work-giver never offers the job) and **the mover's price** (`MovementSystem.StepCost`).
    The third was the bug that stopped mining dead: a hop declares no connector, so `StepCost` fell
    through to `MoveCost.Fall` — 100,000, meaning "effectively forbidden" — and the pawn stood in
    front of the step with a legal path in hand, gaining about one unit a tick, still there after
    10,000 ticks. **A price the planner and the mover disagree about is worse than a wrong price,
    because nothing reports it.**
  - **The incremental nav rebuild needed a new invalidation rule.** A dirty block is re-flooded and
    its regions renumbered, so every zone holding a link into it must be relinked. Links used to be
    horizontal or downward only, so expanding the affected set sideways was enough. A hop is owned
    by the block holding its *lower* cell and reaches *up*, so `CollectAffectedZones` now also takes
    the plate one layer down (relinked, not reflooded). Without it a stale link joined an
    **Impassable** region into a district — the flood never seeds one but will absorb one through a
    link that should not exist.
  - **Mining refuses only the cut that strands the miner**, and it is asked of the *stance*, not of
    the cell: `DesignationGrid.CanBeLeftAfterCutting` is checked in `MineWorkGiver.StandToMine`'s
    on-top branch, the only stance that drops the colonist into the hole it just cut. Put in
    `CanMine` it refused every buried cell — 143 of 8,885 marked cells survived — which makes the
    first cut of a tunnel impossible. So the first cut into flat ground is allowed (the rim is one
    block up beside it) and deepening a one-wide shaft is not: **a quarry comes out as benches.**
  - **Mining was dead on `main` and the suite said nothing** — six `MineJobTests` were skipping on a
    failed `Assume` because no rock had a reachable stance. They run now, and `docs/lessons.md`
    carries the method note. `ADayOnOneFloorNeverTouchesAStair` measures a **span** rather than a
    count of storeys, because a hop is worth one storey on city rubble and only a stair is worth two
    (measured: control spans 1, 1, 1; demo 3, 2, 2).
  - **The climb animation is gone and a jump is half as dear (owner, 2026-09-16: "the animation
    going up and down heights is bad and looks bad … it looks buggy so remove it").** All of
    `ApplyClimbPose`, `ClimbPhase`/`ClimbFace`/`ClimbWeight`, `ClimbLean`, `ClimbEaseSeconds`,
    `TryWallBeside`, `DescribeClimb`, the director's `World` mirror and PlayScene's climbing shot
    are deleted; a hop now plays whatever the gait mixer gives it. That reads far better than it
    did, because a hop moves one cell *sideways* as well as one layer up — so ground speed is an
    ordinary step and the figure walks up onto the block, where the straight-up climb it replaced
    differenced to nought and blended to the idle. **`MoveCost.JumpUp` 270 → 135**: cost *is*
    duration here (a pawn retires `movePerTick` of it a tick and presentation glides across the
    whole step), so 270 was **4.5 s** to get up one block against 1.7 s for a flat cell — not a
    jump, a haul, and it read as the figure being stuck. `Drop` stays at 50, deliberately: that is
    0.83 s for three metres and a three-metre free fall takes 0.78 s, so halving it again would
    have a colonist outrun gravity. **Side effect:** hopping a
    one-block pile now costs 185 against 200 to walk round it, where it used to cost 320, so the
    preference flipped — but it fires rarely. Measured a day at a time with three colonists:
    **67 hops** on the ruined city and **4** on the meadow. (An earlier note here said 6,302, which
    was the broken counter described below.)
  - **A real proof that stairs are used had to be built**, because storeys-visited stopped meaning
    anything once hops were cheap: `MovementSystem.ConnectorSteps` and `HopSteps` count the two
    kinds of layer change. `M2DemoTests`'s control runs both configurations and differences them.
    - **The first version of those counters was wrong, and the owner caught it.** They sat above
      the `MoveProgress < cost` guard, where `StepsTaken++` correctly sits below it, so they fired
      on every tick a pawn spent part way through a vertical step: they counted **pawn-ticks
      weighted by the cost of the move**, not moves. A jump at 135 counted 135 times, a stair up at
      290 counted 290 — which inflated everything and made the two categories incomparable with
      each other, since stairs are dearer per traversal than hops. Every figure taken from them was
      wrong by about two orders of magnitude.
    - **The corrected numbers, control against demo over a day on four seeds:** 1/9, 0/15, 0/15,
      5/12. The test asserts twice the control and at least eight, which all four clear; three to
      one was tried first and seed 4 breaks it.
    - **The small counts are themselves a finding.** On seed 1 the demo takes 9 connector steps
      against 31 hops — most of its vertical movement is hops, not stairs, because a hop costs 135
      against a stair's 290 and the city is built of one-block rubble. **M2's claim that an ordinary
      day exercises the stair connectors is weaker than it was**, and widening the gap means
      making the map want a stair rather than tuning the test.
- **The HUD's first pass landed 2026-09-16,** the first built interface since the design docs: the Unity-free `Odyssey.Hud` assembly (roster, inspect, depth-ruler, ledger and calendar models behind ADR 0003's split, tested in `Odyssey.Tests.Hud` in both tiers) and a UI Toolkit shell `HudShell` styled by `Hud.uss` after the hud-v2 mockup, with every HUD region of the catalogue's screen map in place. **Live:** the colonist inspect pane (world click or roster card — needs, mood, tabs, commands), the roster bar, the clock, the speed buttons, Depth Ruler layer clicks, and the ledger's real rows. **Displayed for the look, disabled with a reason:** main tabs, overlay toggles, alerts, cancel. **The Build palette (A7, called Architect until the owner renamed it on 2026-09-16) is a button on the bottom bar, left of Work**, opening a panel above the bar; Trade was removed from the bar in the same change so the row did not grow. Icons are deterministic placeholder badges keyed by icon key; the ADR 0007 pipeline replaces them when the sheets land. Clicks on HUD regions are gated from the world by `SliceCameraRig.PointerOverInterface`. **Directors (2026-09-16):** `HudDirectors` in the Hud assembly holds `SelectionDirector`, `SliceDirector` and `CameraDirector` per 09 §3; the composition root makes them with the world, the rig, `SelectionPresenter` and the shell only realise them, and a new region arrives as a director plus a presenter, not as more shell. **View-level behaviour is proven in the playmode gate** by `HudSmokeTests` — every region built, roster bound to the frame, selection answered by name, the player-loop half of experiment R4 in `g-02`; whether a panel also resolves under `-nographics` is still open. **The look itself still needs eyes: press Play in `Play.unity`** (regenerated with the HUD).
- **Multi-selection landed 2026-09-16 (M2's selection slice, `claude/selection-mvp`).** `SelectionDirector` now holds an ordered set of colonist handles (`Pawns`, first is the primary `Pawn`) with `PickMany` (drag box / select-similar), shift-toggle on `Pick`/`Choose`/`Toggle`, and per-handle death pruning under the one-frame grace — items and cells stay single-subject, per 09 §3 row 5's "multi-select within one class". New reasons on `Changed`: `Boxed`, `Toggled`, `Similar`. **The gestures:** a left drag past a 6 px threshold in `SliceCameraRig` completes a screen rect against the world even over a panel (input case 1, §6); `SelectionPresenter` tests containment of each drawn colonist's chest point (feet-tweened, layer-filtered like the ray hit-test) and a double click on a colonist takes everything of that kind on screen — the docs' wording, not a vision radius, which stays a future knob. **The roster bar (A2) keeps pace:** every selected card is marked, shift-press toggles without the camera jump, shift-drag sweeps a range. The marquee is a UI Toolkit overlay in `Hud.uss`; multi-brackets are drawn by the composition root (primary full, rest at 0.45 alpha); the inspect pane prefixes "N selected" when the set is larger than one. Headless: seven new director tests in `DirectorTests`. **Not yet:** no per-colonist orders read the set — the command grid (A10) is the consumer this was built for; double-click select-similar does not yet jump the camera from the roster card (single double-click semantics still to settle).
- **The board no longer ends in mid-air (owner decision 2026-09-16).** A decorative surround carries
  the ground and the wood 1,220 m past the rim into the fog, so the map reads as a clearing in a
  landscape rather than as a board game on a table — `TerrainSkirt` and `SkirtLayout`, design in
  `06-rendering-and-camera.md` §2a. **Nothing out there is a cell**: not pathable, not selectable,
  not buildable, not in the save. It *measures* the board rather than being configured, so a bare
  board gets bare ground and a wooded one gets woodland at its own density. It is hidden below
  ground level, desaturates on a ramp from the rim so the playable area still reads as bounded, and
  is switched by `OdysseyBootstrap.terrainSkirt` with `skirtTreeDensity` as the cost lever. The
  camera's far plane went 600 m → 1,800 m to contain it.
- **The ground has a shape, and none of it is a cell (owner decision 2026-09-16).** The board read
  as a carpet of blocks because two things compound: the wooded board sets the generator's own
  `surfaceRelief` to zero, and a ground cell is drawn as one instanced 2.5 x 3.0 x 2.5 cube placed
  by a bare translate, so every top face is a flat quad at exactly the layer height. `GroundRelief`
  (`Presentation/Rendering/`) is the facade that fixes it, in the sense the grass tufts and the axe
  chips are facades: **drawn, and in no cell, no save and no hash**. `surfaceRelief` is still zero,
  the simulation is untouched, and the goldens did not move — ADR 0002's "no slopes, no
  half-heights" is a rule about cells and this adds nothing to a cell. **The governing rule is that
  relief is a drawing offset and never a position**, so `CellMetrics.FloorCentre` is untouched and
  every draw site applies it explicitly. The one thing that must follow the drawn ground is
  **picking**: `SlicePicker` now meets each cell's own tilted floor instead of one flat plane per
  layer, because otherwise a click lands most of a cell away at a shallow pitch, which is exactly
  the misclicking complaint the picker's own remarks cite Going Medieval for. **Ground is sheared,
  everything standing on it is lifted** — a per-cell offset alone gives plateaus with little steps,
  so each cell takes the tangent plane of the field, which is affine and fits in the instance matrix
  it already had: no extra instance, no extra draw call, no new mesh, no shader change, and the
  normal tilts under the ordinary inverse-transpose so the lighting is free. A colonist stands up on
  a hillside; only the ground lies along it. **Two layers of one field**: the board rolls 2 m over
  150 m, the surround adds hills of 50 m over 1,000 m ramped from zero at the rim to full height by
  700 m out, so the join is continuous by construction and the surround no longer cuts across the
  rolling board as a hard line. Hills cannot be the board's field turned up — amplitude and
  wavelength together decide a slope, and 50 m over 150 m stands at sixty degrees. **Two numbers
  came from measurement rather than taste**: a 0.35 m amplitude would have been invisible (a
  1.7-degree slope moves the lit value under one per cent against a 72-degree sun and strong
  ambient), and hills past about 900 m are pointless because fog is opaque at 1,100 m and at the
  default 48-degree pitch the horizon is not in frame at all — only ground 50–224 m away is. Cost,
  measured under the real player loop and never `RenderBench`: meadow 0.41 → **0.68 ms** mean,
  1.10 ms worst, city 0.88 ms, against a 5 ms budget; the shear is free (identical draw calls and
  instances off and on) and the 0.27 ms is the finer surround tiles, which went from 40 m and 120 m
  to 20 m and 60 m because a tilted tile disagrees with its neighbour as the *square* of its width —
  at 120 m that was twenty metres and read as diagonal cracks across the hillsides. Levers:
  `OdysseyBootstrap.groundRelief` and `groundReliefPeriod` (0 is the old flat board exactly),
  `GroundRelief.HillAmplitude/HillPeriod/HillRampMetres`. Judge it with **`Odyssey → Presentation →
  Check the ground relief`** (`scripts/unity.sh shot Odyssey.EditorTools.ReliefCheck.Run`), which
  shoots relief off and on at 48, 20 and 14 degrees. Design: `06-rendering-and-camera.md` §2b, which
  also records the amendment to §2a's "not a framing ring of hills". **Still wants the owner's eye
  in `Play.unity`**: this worktree has no Synty packs, so the ground there is untextured flat colour
  with no grain for a slope to catch, and the board's own roll reads far more weakly than it should.
  ~~Real terracing (`surfaceRelief = 2`) is deliberately left off.~~ **False since the mining
  merge**, which made `MakeWooded` a *cover* mode rather than `MakeBarren` with the trees put back,
  so it stopped zeroing anything: the played board has carried real 3 m terrace risers ever since,
  and `WoodedMapTests.TheDrySurfaceIsTerracedAndCoveredInGrass` requires them. See the next bullet.
- **Earth has a surface, and a terrace step has a way up (owner ask, 2026-09-16).** ADR 0002 fixes
  the layer at 3.0 m and calls it irreversible, so the owner's "can we span this out to half or
  quarter blocks" is answered in the **mesh**, where a variant costs one instancing *bucket* rather
  than one instance per cell and four bearings are free. Nothing here is a cell: no save, no hash,
  no pathing — §2b's "relief is a drawing offset, never a position" holds unchanged.
  `GroundMesh` is `RockMesh` for earth with one rule stone does not have — **the middle of the top
  face is pinned exactly**, because everything in the world is drawn standing at `FloorCentre` and a
  dished meadow would hover every colonist on it. Two meshes, which is a performance decision: only
  terrace risers, mined faces and outcrops ever show a side, so *Turf* is the cheap common case (32
  triangles) and the coursed *Face* (80) goes only to cells that show one.
  **`BankMesh` is the answer to the straight wall:** three treads and three risers drawn in the
  *empty cell* beside a one-layer step — which the simulation already lets a colonist hop
  (`MoveCost.JumpUp`), so the board was showing a wall where the game had a path. Stepped rather
  than smooth, because 3 m over one cell is a fifty-degree ramp however it is drawn and a smooth one
  reads as a road somebody built. Four conditions, each tested: empty and standing on ground, the
  step is **earth** (a quarry wall stays sheer), its top is open, and the cell is open to the sky.
  Water counts as the low side, so every stream bank stops being a 3 m ditch wall.
  **The landscape is one colour at every height:** the surface spans five layers and only one is
  ever active, so the depth shade was dimming grass two terraces down to 0.46 and the meadow came
  out in three greens. `TintCode.DaylitBase` exempts any cell with no slab and no solid cell above
  it — a tree is not a roof — while rock in a mine still dims, which is where that cue earns its
  keep. **Figures answer the ground too:** `Footing` leans the root toward the ground normal (a
  fraction of it, capped — people stand up on a hillside) and plants both feet with `ArmIk`, which
  was already a general two-bone solve; the hips drop to the deepest foot, which is the one thing
  that makes it read. Slope is not a cell property, so **nobody walks any slower**.
  **The black lines between tiles were never holes** (owner report, 2026-09-16). A gap would show
  the pale blue skybox; these were near-black, so they were geometry receiving no light — where two
  sheared cells disagree, the taller one's vertical side wall fills the step and a vertical face
  under a 72° sun with no shadow pass receives almost nothing. `GroundSeamTests` weighed the two
  sources rather than guessing: the relief field's own parting is **14.7 mm** (the "about 41 mm" on
  record was conservative) and that is the subtle line that has always been on `main`; a 3.6 cm rim
  ripple added **72 mm** on top, and that was the obvious one. **So the ripple ships at zero** — five
  times the artefact for a benefit no photograph could find — and the test asserts the default so
  turning it back on needs a fresh sheet. The residual 14.7 mm cannot be removed while each cell is
  its own box and does not need to be, only lit: **side faces carry shading normals tilted 38° up**
  (`SideNormalTiltDegrees`), which costs no vertex, no triangle and no draw call. Any per-cell rim
  movement disagrees with the neighbour by *twice* it, so genuinely uneven ground at cell scale wants
  shader displacement keyed to the **shared corner's world position** — that is the next piece of
  work, not a bigger number.
  **The lip of a step is cut back** (`ChamferMetres`, 22 cm), on the sides that are actually open and
  no others: chamfer all four and every riser cell grooves against the flat ground behind it, which
  is the ripple's mistake arriving again. Sixteen patterns of exposed sides fold onto **five**,
  because turning a mesh is free — at the price that a face spends its bearing orienting the pattern
  and varies by its courses alone.
  **Measured** (`SlopeCheck`, whole slice, no frustum culling, so compare within the run only):
  plain 1,061 draw calls / 31,089 instances → earth **1,312 / 31,089** → banks 1,411 / 32,220. Earth
  geometry costs buckets and **not one extra instance**, 137 of its 251 calls being the pattern
  split; the chamfer *amount* costs nothing at all (1,312 at 0, 22 and 45 cm), and with it at zero
  the five patterns collapse to one so that turning the lever off is not the expensive choice.
  Levers: `ChunkRenderer.EarthGeometry`, `ChunkRenderer.Banks`, `GroundMesh.SideNormalTiltDegrees`,
  `ChamferMetres`, `MaxRipple` — all off being exactly the old ground. The mesh levers are static and
  the meshes are held by reference inside a `ModuleLibrary`, so moving one needs an explicit
  `GroundMesh.Invalidate()` **and a fresh library**, or the ground draws against destroyed meshes and
  silently disappears. Judge it with **`Odyssey → Presentation → Check the slopes and banks`**
  (`scripts/unity.sh shot Odyssey.EditorTools.SlopeCheck.Run`), which finds the longest run of
  one-layer step on the board rather than being told where one is, and sweeps six conditions
  differing by one thing each. Design: `06-rendering-and-camera.md` §2c. **Frame time still needs
  `FrameTimeTests`** on a machine with the packs.
- **Colonists swing an axe, and no pack contains the clip, 2026-09-16.** There is no work animation anywhere in the 7,222 imported assets — `AnimationBaseLocomotion` ships idle, walk, run, sprint, crouch, in-air, turns, transitions and additive lean/look, and the character packs ship none — so a colonist felling a tree stood breathing in the idle for ten seconds and then the tree fell over. The pose is therefore **computed rather than authored**: every character is a Humanoid rig, so `WorkSwing` (`Presentation/World/`) turns a stroke phase into shoulder, elbow and spine angles, and `PawnFigureDirector` pitches those five bones **about the figure's own right-hand axis, never the bone's local axis** (local axes belong to whoever rigged the character; the plane an axe swings in is a fact about the figure), laid over whatever the gait mixer wrote. **The signs are not one convention**: an arm hangs down so a negative pitch carries it forward, a spine stands up so a positive one folds it forward, and the director subtracts the spine's pitch back out of the shoulders so the three angles are genuinely independent. The stroke is three unequal parts — long eased raise, short accelerating strike, dwell with the blade in the wood — because a sine reads as a metronome. It eases in and out over `WorkEaseSeconds`, and it **freezes when the game is paused**, inferred from the tick standing still: a paused pawn settles into the idle by itself, so a swinging colonist would otherwise be the only thing moving. The axe is an ordinary catalogue row, `ModuleIds.ToolAxe` (`SM_Gen_Wep_Axe_01`), parented to the right hand only while the work lasts; a clone without the packs fells trees bare-handed. It is **gripped by measurement, not by authored Euler angles**: the haft is the long axis of the combined mesh bounds, the head is the end the mass sits towards, the tool is laid along the forearm with the grip in the palm, and the blade's roll is computed so the bit faces the way the head is travelling — leaving `AxeBladeRoll` as a trim. **Both hands grip it**, the off hand placed by a two-bone IK solve (`ArmIk`), because no pair of angles will ever bring the second fist to a haft held in the first. And `WorkStance` draws a working figure **wherever puts its blade in the wood**, eased in with the swing — solved from the figure's whole measured strike offset, never from a scalar reach, because with the swing tilted over the shoulder 1.12 m of a 1.68 m strike is *sideways* and a figure stood at 1.68 m puts its axe a metre beside the tree. Contact is checked by `MeasuredBladeGap` (0.19 m from the trunk's middle) rather than by eye: a three-quarter photograph puts the woodcutter and her tree at different depths and cannot settle it. Only the drawn figure steps in; the pawn stays in its cell for picking, the cursor and the whole simulation.

  **Chips fly when the blade lands (2026-09-16).** `ChipDirector` (beside `PawnFigureDirector`, disposed with it) throws a few pieces of debris on the frame the stroke crosses the strike, which `WorkSwing.Lands` decides. **One particle system for the whole colony**, world-simulated, and the material is a `ChipRecipe` value — colour, size, speed, life, count and spread are per particle, so wood off an axe and stone off a pick share the system, the material and the draw call; `ChipRecipe.Stone` is written and waiting for mining. Gravity is the one thing a recipe cannot carry (it belongs to the system), so heavier debris leaves faster, smaller and shorter-lived. **It is warmed on construction** — an unwarmed particle material compiles its shader on the first frame it is drawn, which would be the exact frame the first axe lands. Chips are decoration like the grass tufts: no cell, no save, no hash.

  **The pose is the owner's, settled by interview 2026-09-16:** edge angled ~45 degrees down and into the trunk (a felling scarf), the axe travelling up past one shoulder and down diagonally across the body (`SwingTiltDegrees` -30), landing at waist height with the blade just into the bark, both fists together at the butt of the haft, and the **edge horizontal with the poll trailing back over the hands** (`AxeBladeRoll` 270, settled off an eight-roll contact sheet against a photograph of a real felling cut). **Every figure starts its stroke at the beginning** and is desynchronised by stroke *length* rather than by phase (`WorkSwing.StrokeSpread`, plus or minus 9%): shifting the phase meant a colonist took up an axe already half way through a swing, which no length of ease-in could make anything but a snap. With that fixed the ease could go from 0.25 s to 0.45 s, and the step up to the tree rides on the same weight. The simulation's half is one signal: `JobDriver.WorkFocus` (default -1, overridden by `FellJobDriver`) published as `PawnView.Working` and `PawnView.WorkCell` — a *cell*, because a pawn that has stopped walking has no heading left and the figure has to be turned to face what it is swinging at. Mining and building inherit the swing by overriding one expression. Design: `06-rendering-and-camera.md` §6a. Tuned by eye with `Odyssey → Presentation → Check the axe swing` (`scripts/unity.sh shot Odyssey.EditorTools.SwingCheck.Run`), which photographs one stroke of a real colonist at a real marked tree **side on to the line between the two** — in the board camera's three-quarter view they sit at different depths and the gap between blade and trunk reads as anything you like. The levers are `AxeBladeRoll`, `AxeGripFraction`, `WorkStance.StandOff` and the six angle constants in `WorkSwing`. **This stands in for art we do not have**: when real work clips exist they replace it and `WorkSwing` goes.
- **There is water on the board (owner decision by interview, 2026-09-16; ADR 0009).** `02-world-and-layers.md` §8 had deferred it outright, so this takes the deferral up. Ponds, winding 1–3 cell streams and marsh fringes on most maps, with a wide map-crossing **river** as a rarer variant (`riverChancePerMille` 120). **Shallow water is walkable at exactly a third of walking speed; deep water is impassable** and the pathfinder routes around it; marsh is ordinary ground that is merely slow. Water is three `TerrainDef` rows in `NaturalContent`, **not** a new grid, a save section or a snapshot channel — terrain was already a hashed, saved, chunk-encoded per-cell index.
  - **Deep water needed a flag of its own**, `CellFlags.ImpassableTerrain`, read only by `CellGrid.IsWalkable` and `NavGrid.RefreshFrom`: solid terrain holds a colonist up on the cell above (walk *across* the lake) and non-solid leaves the cell walkable because the bed is a floor (walk *through* it), so neither existing flag can say "neither stand in nor stand on".
  - **A channel is cut one layer down**, so a bank is real geometry a bridge will span. Deep water is **not** cut deeper: the bed is the column's surface, so a two-layer core would put neighbouring surface cells two layers apart. Depth is told by colour and opacity, and both depths draw their surface at the same height in the cell because a pond has one level. **Not one generator invariant was relaxed** to let water in — the column rule, one-layer-apart and nothing-floating all pass unmodified, which is the evidence the geometry is honest.
  - **Two passes, either side of the strata** (`NaturalWaterPasses.cs`; `PassCount` 7 → 9, and every later pass renumbered). Where water goes is a *column* decision that must precede the strata so the one full-grid loop builds a correct column under every bed; what a cell is made of is a *cell* decision that can only follow. Paths are a **noise-displaced straight line** — the cross coordinate is a function of the march step, so a channel cannot self-intersect, leave the map or stall — and depth is one bounded flood in from the shore, which makes a 1–3 cell brook wadeable end to end and gives a river a deep core from a single rule.
  - **Banks are settled in both directions.** A channel crossing a terrace step leaves a two-layer cliff, and one cut into a slope can leave water perched above lower ground. Both were found by tests, not by reasoning, and both are fixed by lowering whichever end of a disagreeing pair is too high until a bank stands *exactly* one layer over its bed. The relaxation must be **symmetric**: if only the wet end pulls its bank down, a bank lowered by another channel never tells the channel beside it to follow.
  - **No river ever cuts the colony off.** Fords are cut by construction, water is kept clear of the start (`startWaterClearance`), and a reachability flood forces more fords rather than re-rolling — re-rolling takes unbounded time on a bad seed and quietly uses a seed other than the one it was handed. Measured: **0 of 40** rivers need the backstop; with the fords switched off it fires on **40 of 40** and still leaves 80% of the board reachable, which is how we know it works rather than merely never fails.
  - **Movement uses the seam that was already there.** `NavGrid.CostClass` / `CostByClass` fed `EnterCost` since the pathfinder was written and **nothing had ever written either**: every step cost 100. Now clear 0, marsh +40, shallow water +200. The trap: **a cost class belongs to the cell *entered*** — wading that is the water cell, crossing a bog it is the air cell above the marsh, and the other way round marsh is free and nothing says so. The table is applied in the `NavGrid` constructor rather than handed down, because a `NavGraph` is built in a dozen places and a missed call is a wrong number, not a crash.
  - **Nothing is built in water.** `TerrainDef.buildable`/`bridgeable` are written for U26 to inherit; `DesignationGrid.Allows` refuses water (no behaviour change today, but its own doc says validation lives there); and `ColonyScenario.FindStartSpots` was a real bug — shallow water is walkable, so a bed could have been unpacked in a stream. **Bridge building is out**: there is no build pipeline at all.
  - **Water has a shader, not a tint** (`Odyssey/Water`): ripples, a sun glint, a Fresnel-weighted probe reflection and a shore that dissolves against the depth of the bed. Arithmetic only — no texture, no extra pass, no render target — drawn by the ordinary chunk machinery, so a clone without the packs draws the same water. It casts and receives **no shadows**, which is both the right picture and a third of the cost: the board ran 0.68 ms before water, 1.28 ms with shadowed water, **0.99 ms** without (city 1.56 ms; 5 ms budget). The 0.88 ms city figure on record was not reproducible in the same session and the discrepancy is **unexplained** — the city has no water, so different measurement conditions are the likelier answer.
  - **Marsh was the thing photographs caught.** With no catalogue row it drew as an untextured dark olive slab, which beside a lifted meadow reads as *shadow*, and there was more marsh than water on the board (427 columns to 382). It now has the bare-earth dirt material, a bright sour tint, one fringe ring instead of two and a threshold that frays it — and it reads as a **sandy bank**, which looks right but is not what "marsh" means. **Open for the owner: re-tint it greener or rename it.**
  - Judge it with **`Odyssey → Presentation → Check the water`** (`scripts/unity.sh shot Odyssey.EditorTools.WaterCheck.Run`), which shoots a stream and a forced river at three pitches — Fresnel is an angle, so the grazing shot is the only one where the surface shading really shows. Levers: `NaturalMapGenDef.water` (off is **byte-identical** to the pre-water generator, and six baked hashes prove it), `deepShoreDistance`, `marshFringe`, `riverChancePerMille`, `ChunkMesher.WaterSurface`.
- **Gestures — a figure doing something that is not walking and not a tool stroke (branch `claude/gestures`, 2026-09-16).** Settled by three rounds of interview and designed in `docs/design/13-gestures.md`. **The governing rule: author the angles when the figure aims at something whose position we do not know; solve to a point when it must meet something whose position we do.** The axe is the first kind, which is why every angle in §6a needed a photograph; the lift is the second, and needs none — the hands go to the figure's own feet, so it is right on all 61 rigs and on relief-tilted ground by construction. **A crouch is two angles and one translation**, and the translation is what keeps the boots on the ground: the pelvis drops first, then each leg is solved back to the foot the gait already put down (`TwoBoneIk`, renamed from `ArmIk` — a leg is two bones, and its pole goes in *front* of the knee) and the sole's rotation is restored or the toes point into the floor. Legs, hips and feet are bound at last, which the climb pose had recorded as a known gap. **The contract is two sticky bytes** (`PawnGesture`, `PawnView.Gesture`/`GestureSerial`): a one-tick flag is *unobservable*, because presentation reads one snapshot a frame and the sim runs several ticks between frames at speed 3 — so the report stands until the next gesture and a serial tells two lifts apart, a figure that has never seen a pawn poses nothing, and neither field is saved or hashed (a test asserts the hash does not move when every pawn gestures). The sim's whole part is two assignments in `HaulJobDriver`; the toil stays one tick, so no golden, throughput or balance number moves, and a figure may still be straightening as its pawn walks off — the accepted price of keeping the duration out of the simulation (owner, 2026-09-16). **A third work style, the builder's hammer**, went in ahead of the queue: `WorkStroke.Hammer`, `WorkStyle.Building`, `ChipRecipe.Timber`, `ModuleIds.ToolHammer`. Its own stroke rather than a fast axe because the haft is 0.63 m against 0.74 — a short tool is swung from the elbow, so the shoulder comes back less and the elbow cocks harder. `SM_Wep_Hammer_01` is the only hammer in all 7,222 assets, so it was not chosen on merit; it is the first prop with a butt pivot, which should not matter because the fitting works off mesh bounds, and the contact sheet is what proves that. Nothing in the game builds, so `IndexForJob` can never reach it and `PawnFigureDirector.StyleOverride` is the harness's way in. **Verified: it builds and the arithmetic holds** — fast tier 409 Sim and 29 Hud, and EditMode **639 total, 629 passed, 0 failed** in the worktree, including 8 new `GesturePoseTests`, 6 renamed `TwoBoneIkTests`, 6 on the hammer and 7 on the contract. **Not verified: whether any of it looks like anything.** No test can say that, which is why `GestureCheck` exists (`Odyssey → Presentation → Check the lift` / `Check the set-down`, and `Check the hammer swing` beside the axe's and the pick's). Until those are judged, every angle has the standing the pick's numbers have: an argument. Still open: crouched sustained work and the two-handed gun with recoil (§9 G6, G8), both harness-only because nothing farms and nothing shoots.
- **`AxeBladeRoll` 270 is a stale line in this file** (found 2026-09-16). The status entry above says the axe's blade roll was "settled off an eight-roll contact sheet" at 270°; the code says 0, and deliberately — commit `eb5371f` moved it there and added `BladeYaw` in the same change, because the roll turns the head about the very line the head is trying to be pointed along. The code is right. Correct this file when somebody next touches that entry.
- **Taking hold of something is no longer part of the axe (2026-09-16).** `Grasp` is the general
  solver: a `Hold` is a run of material with a thickness, a `GripArm` is two bones, a wrist and
  fingers with somewhere to send the elbow, and `One` / `Both` / `Opposed` are the three ways hands
  meet one — a haft, a rung, a rifle fore-end, a crate between two palms. **It iterates three
  times**, because palm-on-wood, palm-turned-to-wood and fingers-closed each move the other two
  (rolling a hand swings the palm right round the wrist the solve actually places); one pass
  finished 0.13–0.30 m out. A palm seats **half a thickness off the centre line**, or the wood is
  inside the hand. And the bug underneath all of it, which predated the fists: **a humanoid hand
  bone is the wrist**, so every tool in this project was seated five to eight centimetres behind the
  hand from the day there was an axe — `HandGrip.Palm` measures where a held thing really sits, off
  the knuckles, which only became possible once the fingers were bound. `MeasuredGripGap` and
  `MeasuredGripOverreach` are printed by `SwingCheck`; the second separates an arm that missed from
  an arm that could never have reached, which a photograph cannot.
- **Colonists climb with their legs now (G7, 2026-09-16).** `ClimbPose` puts the boots on the wall:
  **contralateral** (the right hand reaches with the left foot), **solved rather than authored** per
  `13-gestures.md` §3, and every number a fraction of the figure's own thigh-plus-shin so it is the
  same climb on all sixty-one characters. **A wall is a plane and the first version made it a
  cone** — written as a reach and an angle, the two boots stood at different distances from the rock
  and one of them thirty centimetres inside it, which no contact sheet of a figure on clear air
  would ever show; the rock distance is now fixed from `ClimbLean`'s own arithmetic and only the
  height varies. Judge it with **`Odyssey → Presentation → Check the climb`**
  (`scripts/unity.sh shot Odyssey.EditorTools.ClimbCheck.Run`), which forces the climb and walks the
  cycle by hand, shoots at **facing + 90°** (unlike `GestureCheck` — see §11 for why the two differ)
  and prints `MeasuredFootReach` beside each picture. In that sheet the boots stand in the grass:
  a forced climber is a colonist on a meadow rather than one half way between two layers, so read
  each boot against the hip. Levers: `ClimbPose.ExtendedDrop` / `SteppedDrop`. **Not done: the
  climbing hands are still open fists** — `Grasp` is now exactly what would close them on a hold.
- **What you can see is decided by how deep you are (owner decision 2026-09-16; ADR 0006 amended,
  `06-rendering-and-camera.md` §3a).** **At or above the surface: every layer above, drawn SOLID —
  no cap, no fade. Below the surface: one layer above, x-rayed, and every layer below to the
  floor.** Solid was a correction: the rule first shipped x-raying the stack and the owner's reply
  was *"this includes everything buildings, stones, rocks and everything, as I noticed the mining
  rocks were transparent"*. It resolves to `AboveMode.Full` for opacity only — the active layer
  stays roofless (`SliceSettings.SuppressCeilingAt`), because ADR 0006's `full` is the exterior
  view and keeps its lid, while "the active layer is drawn roofless" is a standing decision a
  default must not reverse silently. The lever is
  `SliceSettings.followDepth` (on by default) with `surfaceLayer` set from the start cell by the
  composition root; switching it off obeys the six ADR 0006 modes exactly as before, and the V key's
  first press does that for you — it pins whatever is on screen, then cycles, then hands the default
  back. The owner's reason: *"you need to be able to see within the environment — if there was ever
  digging introduced into the game or underground base."*
  - **The report was "I couldn't see another person mining above me", and that half was a bug, not a
    policy.** The terrain above the slice was x-rayed correctly; every *actor* in it was culled
    outright, by `PawnFigureDirector.Sync` and `ChunkRenderer.RenderActors` alike, both testing
    `cell.Y > activeLayer` — items too. A colonist working a storey up did not exist on screen. Both
    now cull against `SliceSettings.HighestVisibleLayer`, so **a figure is drawn on every layer the
    world is drawn on and on no other**. Solid, at full opacity (owner's call): a figure faded to
    match its surroundings is invisible within two layers, and being sure *who* is overhead beats
    being sure how far. Selection is unaffected and needed no guard — `SlicePicker` is clipped
    analytically to the active layer's slab and `PawnUnderRay` already refused anything above it, so
    nothing above the slice is a pointer target, as ADR 0006's Lane B amendment requires.
  - **Solid has no fade to stop the loop, so the top is capped by the geometry.**
    `ChunkRenderer.BatchFor` *meshes* a chunk when asked and again after every version bump, so an
    unbounded loop would mesh a dozen layers of empty sky on every edit of a tall map.
    `WorldRenderModel.HighestOccupiedLayer` is the top of the geometry plus the layer a colonist
    standing on it occupies — a high-water mark, raised as cells enter the mirror and lowered only
    by a full refresh, which is the safe direction. (The fade bound still governs an explicitly
    chosen `xray`: the ramp runs 0.380, 0.274, 0.197, 0.142, 0.102 … and crosses the 0.012 cutoff
    after eleven layers, the same constant the chunk loop skips on, so the two cannot drift.)
  - **It costs nothing on the board being played, and that is measured rather than hoped.** On the
    16-layer board with the surface at L11, "every layer above" is L12–L15 — four layers, exactly
    what the old `aboveDepth` of 4 drew. Underground at L6 the range was L3–L10 and is now L0–L7:
    eight layers either way. Deeper it is cheaper (L2: seven layers before, four now). Only a map
    tall enough for the eleven-layer bound to bite pays anything, which is ADR 0006's new flip
    condition F4.
  - **Not measured: frame time.** `FrameTimeTests` runs under the player loop and the owner's editor
    was holding the project, so the Unity gate did not run. The Presentation and Editor assemblies
    were compiled headlessly against `Library/ScriptAssemblies` (`docs/lessons.md`, "Compiling the
    game code while the editor holds the project") and the slice arithmetic was run outside the
    player to check every number above, but **`scripts/unity.sh test editmode` and the PlayMode
    frame-time gate are both unrun on this change.**
- **Work ends with a beat, not a snap (owner, 2026-09-16).** `JobDef.settleTicks` (fell 30, mine 30)
  holds a colonist still for half a second after the work is done — the tree is already down and
  the rock already gone — before the job ends. It is a settle *toil*, last in the driver, reached
  **before** the driver's own guards (by then the designation is cleared, so "is this still a
  marked tree" would fail the job on the first settle tick) and reporting `WorkFocus = -1`, so the
  drawn figure eases out of its work stance while standing still.
  - **It fixes a measured fault, not just a feel.** The figure steps *in* towards its work (about
    0.8 m for felling) and eases back out over `PawnFigureDirector.WorkEaseSeconds`, 0.45 s. Before
    the settle, **all 27** work-to-move transitions in 40,000 ticks began gliding within **1 to 3
    ticks** of the work stopping — so every one was walking and un-stepping at once, and because
    the gait blend leaves the stance out of the speed it measures, the feet played an ordinary walk
    while the body covered both. That is the "very quickly walk and then come to a normal pace" the
    owner reported. Afterwards every gap is 31–33 ticks and none is under the ease.
  - **So 30 is not taste: the settle must be at least the presentation ease** (27 ticks). A
    simulation constant chosen to cover a drawing constant is an uncomfortable coupling and it is
    the lesser one — the alternative is presentation reaching into job timing.
    `AFelledTreeIsFollowedThroughRatherThanSnappedOutOf` **asserts** that relation rather than
    assuming it, so zeroing the def fails the tier instead of quietly skipping it.
- **The game has sound, 2026-09-16** (ADR 0010, research `d-12-audio.md`; plan unit U33 in vertical-slice.md's "After the slice" section — audio playback was in no plan before this). `AudioDirector` (`Presentation/Audio/`) is a presentation **director** in the ChipDirector sense: it owns every AudioSource in the game, reads only the published frame and the render mirror, and no sound is a cell, a save key or a hash bit. **Pooled voices, culled before they are spent:** sixteen AudioSources serve the whole colony — work impacts fire from `PawnFigureDirector.BlowLanded` (the same stroke moment the chips fly, with the style and the edge position), distance-culled by each def's max range, repeat-gated per sound id (five woodcutters near the camera are one rhythm section), stolen only from lower-priority voices, with pitch/amplitude variance because an identical sample is recognisably identical. **Ambience is measured, not placed:** `AmbienceProbe` samples the terrain mirror in a disc around the *camera's focus* (not the camera, which is tens of metres in the air), water cells weighted by proximity, saturating at "clearly full water", the bed's one 3D voice placed at the weighted centroid so a river pans as the camera orbits; layer-aware — water under a descended slice's floor is not heard. **Music and alerts are 2D:** day/night tracks crossfaded from the tick through `GameClock` (two ping-ponged voices, never a gap); the starving alert is raised off the published pawn list by `AlertWatch` with hysteresis (chimes once at a crossing, re-arms past 30%), and every alert ducks the music. **Buses in code, gains in dB:** Master/Music/Ambience/Effects/Alerts, the mixer's concept set with the mixer's math and no mixer asset (no supported API creates one; adopting a real mixer later is per-voice routing plus moving `SetBusDb` — the stored settings keep their meaning). Volume settings are the **B17 stub**, dB faders in PlayerPrefs (`AudioSettingsStore`). **Clips are generated placeholders** (`scripts/unity.sh exec Odyssey.EditorTools.AudioSetup.Build` writes eight synthesised WAVs and the `AudioCatalogue` asset; import classes per the manual — PCM decompressed for impacts, ADPCM for the water bed, Vorbis streamed for music), so a clone without them runs silent and licensed audio drops in as data with zero code change. Tests: EditMode for math/probe/clock/watcher/director (stepped on the director's own clock, so edit and play mode answer identically) plus a PlayMode smoke test. The dev overlay (backtick) carries the audio counters.
  - **A voice is spatialised from the transform it shares, and they all shared one.** Every
    AudioSource was a component of a single GameObject, so writing a one-shot's position moved the
    lot — the axe sounded from wherever the last sound was written, and the water bed dragged every
    one-shot along as its centroid moved. A GameObject per voice. The test that passed through it
    played one sound; one sound cannot disagree with itself.
  - **The water bed was silent on every map the game generates.** The probe sampled terrain at the
    slice layer, which is the air a colonist stands *in* — terrain belongs to the solid cell under
    it, and a channel is settled one layer below the dry surface. The fixture agreed with the bug
    by putting its pond on the layer it probed. It reads the slice layer and the floor underfoot
    now: two layers, not the column, so a descended player still does not hear the river through
    rock.
  - **Two kinds of ambience, because there are two questions.** The water bed answers *how much of
    this is near me*; the **outdoor bed** answers *where am I*, which is not a quantity — it plays
    flat above the surface, is silent below it, and changes with the clock rather than the terrain.
    One loop per phase crossfaded at dawn and dusk, 2D because it is the air itself, on the
    Ambience bus, under everything as the floor of the mix (day 0.34, night 0.26). Music and the
    outdoor bed are the same shape of thing, so `PhaseLoop` is one class used twice: the
    ping-ponged pair, each track's own fade length, and the rule that the incoming voice is the one
    *not* fading out.
  - **The id lookup was the whole cost and it grew with the catalogue.** `AudioCostTests` measures
    the frame: a full `Sync` over the played board is **0.0035 ms**, and forty one-shots offered in
    one frame — four times the colony the slice will run — went **0.1527 → 0.0050 ms** against 252
    sounds once the director indexed the catalogue by id, keyed the cooldown by the def rather than
    its id, and moved the rolloff curve from every play to construction. The price no longer moves
    with the table's size; a 0.05 ms budget in the tier keeps it that way. The probe was left alone
    — 225 samples a frame is 0.0035 ms, so throttling it would optimise nothing.
  - **Real audio arrives as files, and the tool used to eat them.** `AudioSetup.Build` rewrote all
    the WAVs every run, so sourced audio under the names the catalogue reads would be replaced by
    the synthesised stand-in; a file that exists is never written now, and wiping the folder is its
    own menu item that asks first. `forceToMono` was applied to every clip and is now a property of
    the clip's use, so stereo music keeps its image. A sound takes variants — `chop_01.wav` beside
    `chop.wav`, up to sixteen, picked at random per blow. **What to source and what to call it is
    `docs/reference/audio-sourcing.md`**: eight files, lengths, which loop, which are mono, and why
    WAV rather than OGG or MP3 (the source is re-encoded on import, so a lossy master only stacks
    artefacts).
  - **Open: the ears are on the camera, which is 32–160 m from the ground.** The cull and the
    rolloff are measured from the listener, while the catalogue authors ranges as ground distances
    (chop at 48 m), so a colonist felling a tree dead-centre in frame plays at about a fifth gain
    at the default zoom and is culled outright past it. The fix is either to move the listener to
    the camera's focus — the usual answer, and what makes the authored numbers mean what ADR 0010
    says — or to re-author the ranges as camera-relative. Owner's call; it changes how the whole
    game sounds.
- **Sim vs UI vocabulary is deliberate:** simulation systems are *subsystems*, presentation-side coordinators are *directors* (`01-architecture.md` §3a). Do not unify the two words.
- **Phase 3 (design): complete 2026-09-15.** `docs/design/` 00, 01, 02, 03, 04, 05, 06, 07, 08; ADRs 0001, 0002, 0005; and the execution plan `docs/plans/vertical-slice.md` (32 units, M0→M3). **The Phase 3 → Phase 4 hard stop was cleared by the owner on 2026-09-15; execution is under way.**
- **Interface, icons and content naming (the UI line of work), 2026-09-15.** Design `09-ui-and-input.md`, `10-ui-panel-catalogue.md`, `11-icon-library.md`; ADRs 0003 UI framework, 0004 sim-to-UI contract, 0006 layer visibility, 0007 pixel-art icon pipeline; research `g-01`, `g-02`; mockups `hud-v1.html` (historical) and `hud-v2.html` (current). **Layer visibility decided:** x-ray by default with six modes shipped for playtest, amended by Lane B so that nothing above the active slice is ever a pointer target. **Icons:** 382 keys enumerated, 268 mapped to the owner's eight pixel-art sheets, 114 gaps listed in `11-icon-library.md` — the largest being people, since no sheet contains a human figure. **Names:** all 29 proper nouns proposed and awaiting the owner's veto, in `docs/design/proper-nouns.csv`.
- **Colonists are drawn from 61 Synty characters** across all four packs, chosen per pawn by id (`ColonistLook`), each with the locomotion clip set matching its name. The cast is the `Colonists` array in `PlayScene.cs` — strike a name and rebuild the catalogue to remove a face. Eight rigged-but-not-people prefabs (scarecrow, skeleton, robots, hologram, the two in underwear) are listed there as deliberately excluded.
- **Labels, not abbreviations (owner, 2026-09-16).** Until real icon art is in the build, every icon-bearing control draws its **full name** beside the icon; the two-to-four character badge is only a placeholder-tile and sub-32-pixel rendering rule, never the thing that names a control. Icon-only survives as a toggle and may become the default again on an owner screenshot call. Recorded in `09-ui-and-input.md` §7a (which closes open question D1), `10-ui-panel-catalogue.md`, ADR 0007; `hud-v2.html` now opens in *+ label* mode. Layouts are authored against the longest label, not the icon box.
- **Still needed from the owner for the UI line:** copy the eight icon sheets into `art-source/icons/sheets/` (that folder's README names them), and approve or strike the proposed names.
- **Cell size: FIXED at 2.5 × 2.5 × 3.0 m** (`docs/adr/0002-cell-size-and-layer-model.md`).
- **Architecture: FIXED — plain C# structure-of-arrays with Burst on measured hot paths** (`docs/adr/0005-simulation-architecture.md`), decided by a two-candidate benchmark in which both implementations produced the identical state hash. Plain 1.423 ms/tick vs ECS 2.446 ms at 250 × 250 × 40.
- **Top technical risk: pathfinding cost.** 65% of the measured tick is A-star. The explanation once recorded here — that these were futile searches for unreachable targets — was **falsified by its own follow-up experiment**: only 14% of budget exhaustions were unreachable, and under 1% on a structured map. `05-ai-and-jobs.md` and ADR 0005 were corrected. The fix is hierarchical search plus a better heuristic, with the district-id reachability check (`d-04-pathfinding.md`) measured first thing in M2. At a 3x hardware discount the tick leaves 3.8 ms of a frame for rendering; at 4x it leaves none.
- **Target: the playable MVP** (vertical slice, M0→M3) per `phase1-answers.md` Q8–Q9. Graphics bar: close to the concept renders (`d-03-rendering.md`). A look-check scene exists: `Assets/Scenes/Spikes/VisualBlock.unity` (regenerate via *Odyssey → Spikes*).
- Owner reference (2026-09-15): `github.com/RimWorldMods` for understanding RimWorld mechanics — clean-room rules apply, nothing is copied from it (note in `docs/research/INDEX.md`).

## Read this before losing an hour

`docs/lessons.md` collects the operational lessons that have already cost time once: the Unity batch run that finishes without exiting and locks the project, assembly definitions silently dropping implicit package references, why filtering tests saves nothing, and the working-method rules for parallel agents. **Add to it whenever something takes more than about ten minutes to diagnose.**

The two that come up daily:

- **Tests:** `scripts/test-fast.sh` while working (~1.7 s, no Unity), `scripts/unity.sh test editmode` before committing (authoritative).
- **Fix issues before adding features** (owner rule, 2026-09-15), and re-run after a fix to check the output *means something* — a plausibly wrong result is worse than an obviously broken one.

## Repository layout

- `docs/lessons.md` operational lessons (read it) · `docs/brief.md` governing brief · `docs/research/` research files and `INDEX.md` · `docs/reference/screenshots/` reference images and descriptions · `docs/setup/local-dev.md` dev-machine setup (§8 for Windows). Later phases add `docs/design/`, `docs/adr/`, `docs/plans/`, `docs/milestones/`.
- The Unity project lives at the **repository root** (`Assets/`, `Packages/`, `ProjectSettings/`), created 2026-09-15 on the Windows dev machine (Unity 6000.3.24f1, Universal 3D template).
- `Assets/Odyssey/` the game assemblies: `Sim.Contracts`, `Sim` (both UnityEngine-free), `Tests/Sim`. `Assets/Editor/Odyssey/` editor tooling: `SyntyInventory.cs`, `SyntyImport.cs`, `VisualBlockScene.cs`. `tools/dotnet/` mirror projects for the fast test tier. `Assets/Synty/` licensed packs, ignored by git.
- `scripts/unity.sh` headless Unity wrapper (`inventory`, `test`, `exec`, `shot`, `open`, `which`; `shot` takes an optional method, e.g. `shot Odyssey.EditorTools.ScatterSheet.Shoot` for a contact sheet of candidate props) and `scripts/test-fast.sh` the no-Unity test tier.
- `docs/wiki/` the generated content wiki (read it, never edit it — see the section above). `tools/wiki/build_wiki.py` builds it; `tools/icons/icons.py` is the icon pipeline (detect, contact, export, validate, emit-web) with 30 tests via `python3 -m unittest discover -s tools/icons -t tools/icons`; `tools/mockups/artifact_body.py` makes a mockup publishable. All three are standard library only, so they run in a container with no Unity.
- `art-source/` owner-owned source art kept **outside** `Assets/` so Unity does not import it. `art-source/icons/sheets/` is where the eight icon sheets go.

## Environment

- **Dev machines:** Pop!_OS (Unity Hub, RTX 5070 Ti) and Windows 11 (`D:\code\odyssey`, Unity CLI/Hub beta — see `docs/setup/local-dev.md` §8). Both run Unity 6000.3.x LTS; neither is the performance target (that is a 2022 mid-range laptop). Keep the project path free of spaces (a Unity-MCP constraint).
- **dotnet SDK 8.0.425** is installed on the Windows machine at `%USERPROFILE%/.dotnet` and powers `scripts/test-fast.sh`. A remote container without Unity can still run every Sim test through it, given an SDK.
- **Python 3.13.15** is installed on the Windows machine as of 2026-09-16 (`%LOCALAPPDATA%\Programs\Python\Python313`, ahead of `WindowsApps` in PATH, with a `python3.exe` copy beside `python.exe` because CPython ships none). The wiki, icon and mockup tooling therefore runs on **both** machines now. `PYTHONUTF8=1` is set for the user and is required: without it Windows Python reads the docs as cp1252 and `build_wiki.py --check` calls every file stale. See `docs/lessons.md`.
- **Blender (optional):** only for gaps no Synty asset fills (a stair or ladder variant at the cell size, UV or atlas fixes, rig or animation retargeting). Synty first. Blender-made pieces go under `Assets/Art/Custom/` and are committed; they must match the Synty style and snap to the cell grid.
- **Unity MCP:** IvanMurzak/Unity-MCP, installed per `docs/setup/local-dev.md`. Once connected, Claude Code can open scenes, run EditMode/PlayMode tests, read the console and execute editor C#. Prefer `scripts/unity.sh` for anything that must also work in CI.

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
