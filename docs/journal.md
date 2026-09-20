# Build journal

The narrative record of how Odyssey got here: what was decided, what it cost, what was measured
and what turned out to be wrong. It was the **Current status** section of `CLAUDE.md` until
2026-09-16, when that section had grown to 709 of the file's 814 lines — long enough that the file
every session reads first had become mostly history, with a duplicated entry and several
self-contradictions inside it.

**The split.** `CLAUDE.md` now carries only what is true *now*, in a form a new session can act on.
This file carries the reasoning behind it. Nothing was deleted in the move except one accidentally
duplicated entry (the mining bullet appeared twice; the shorter copy was quoted in full by the
longer one, so the longer was kept).

**How to use it.** Read `CLAUDE.md` first. Come here when you need to know *why* something is the
way it is, or when a decision looks arbitrary and you are about to change it. Entries are roughly
chronological and each names its date and, where there was one, the owner decision behind it.

**How to add to it.** Append; do not rewrite. When an entry here is superseded, say so in the new
entry and leave the old one standing — several entries below are valuable precisely because they
record a measurement that was later falsified. Design and mechanics still belong in `docs/design/`
and `docs/adr/`; operational traps still belong in `docs/lessons.md`. This is the record of the
work itself.

> Entries below are preserved verbatim from `CLAUDE.md`. Some describe a state of affairs that a
> later entry overturns — that is the point of a journal. Where an entry is known to be stale, a
> later entry says so.

## Entries


- **Phase 0 (ground): complete 2026-09-15.** The import spike and asset inventory ran on the **Windows** dev machine: Unity 6000.3.24f1 LTS + URP 17.3.0 project at the repository root, five Synty packs imported headless under `Assets/Synty/` (7,222 assets, zero import errors). Record in `docs/research/synty-import.md`; measurements in `docs/research/synty-inventory.md`.
- **Phase 1 (interview): complete 2026-09-15** — all seven answers in `docs/research/phase1-answers.md`: packs supplied (Q1), cell confirmed (Q2), template stamping (Q3), IvanMurzak/Unity-MCP now (Q4), pragmatic TDD (Q5), two-way architecture benchmark with determinism-first threading (Q6), Windows machine primary (Q7).
- **Phase 2 (research): complete for the slice, 2026-09-15** — wave 1 (twelve files: Lane A pawns/jobs/building/mapgen/tick, Lane B Going Medieval, Lane C Cataclysm DDA, Lane E all four Synty files, Lane F prior art, which found a *Ramble* checkout at `D:\code\ramble`) plus wave 2 (A14 stockpiles, **D1 architecture benchmark**, D4 pathfinding, D6 save/load, D7 Defs, D8 CI). One-line results per file in `docs/research/INDEX.md`. Deferred until after the slice, by the Q8 fast-track: A2/A5–A11/A13/A16 and the remaining Lane B/C items. **Interface/UI research is owned by a separate agent session — do not duplicate it here** (`docs/design/ui-plan-reconciliation.md`).
- **Where this is, 2026-09-16.** M0 is closed (U08 CI landed: two tiers, the Unity one on the owner's
  machine, branch protection requiring both). M1 and M2 are done in substance and went further than
  planned — colonists walk, chop and have needs, mood and skills; a colony survives ten headless days
  on three seeds; there is a HUD, a naming registry, terrain relief, water and a work-pose system,
  none of which the plan asked for. M3 has started ahead of itself: designations, felling and
  stockpiles are in, and **mining merged on 2026-09-16** (PRs #30 and #31) with its Defs and with a
  28x tick-cost regression fixed on the way in — `DesignationGrid` was publishing its progress
  channel by walking every cell of the active layer every tick, which took the ten-day soak from
  one second a seed to thirty-nine; it walks the sparse list of ordered cells now and a guard test
  fails if it ever walks the layer again. **Neither felling nor mining can be *ordered*:** there is
  no designate tool anywhere in the HUD or the input layer, so both get their orders from the
  scenario before the first tick. That is what `OQ-40` unblocks. **M2 is closed and
  reported: `docs/milestones/M2-report.md` (OQ-21, 2026-09-16).** All five parts of the standing
  gate are green — EditMode 494/492, PlayMode 7/7, a 60,000-tick day ending on hash
  `e134005c5408818d` under *both* Mono and CoreCLR, the save round trip on a real world, and three
  ten-day seeds clean. The demo asserts the milestone's own claim at last: with `OQ-47` a scenario
  names the storey a thing goes on, so the beds are a floor up and the food two, every colonist
  changes storey across the day, and **the control is a test** —
  `ADayOnOneFloorNeverTouchesAStair` runs the same day with the offsets removed and asserts nobody
  moves. Two things are green but not clean, both in §5 of the report: `OQ-05` is open, so
  cross-runtime determinism is a measurement rather than a standing test, and the city's frame time
  moved from 0.88 ms to 1.56 ms unexplained (`OQ-43`).
  **Before more features, `OQ-44` to `OQ-46` open the seams** — the mining line is 73 files and had
  to edit six shared files to add itself, five of which should have been extension points. The
  reasoning and the order are in `docs/plans/vertical-slice.md` under "Where the seams are".
- **Phase 4 (execution): M0 complete, M1 under way on branch `claude/m1-world`.** U01-U07 done, and the whole simulation stack runs: grid, support solver, two worldgen paths, pathfinding, pawns, save/load, Def loader, subsystem schedule, and the snapshot-read / intent-write seam. M1 presentation has instanced chunk rendering (no GameObject per cell), a slice camera rig, click-to-inspect and a generated play scene (`scripts/unity.sh exec Odyssey.EditorTools.PlayScene.Build`). **488 EditMode tests (486 green, 2 skipped) plus 7 PlayMode**, and 326 Sim plus 29 Hud in the fast tier (1.7 s fast tier, 37 s Unity gate, `unity.sh test playmode` for frame time). **Frame time is measured only by `FrameTimeTests` under the real player loop** — wooded meadow 0.41 ms, city 1.48 ms on the RTX 5070 Ti at 640 × 480 (2026-09-16, grass to the rim and trees) — never by an editor `camera.Render()` loop, which measures its own history (`docs/lessons.md`, "Benchmarking the renderer"). M1 report: `docs/milestones/M1-report.md`. **U08 CI is done (2026-09-16).** `.github/workflows/ci.yml` runs two tiers on every push and pull request. *Fast tier*, on GitHub-hosted Linux: the Sim and Hud tests, the Long tier, the content-registry check and the icon tooling tests. *Unity tests*, on the owner's Windows machine as a self-hosted runner (labels `self-hosted`, `windows`, `unity`, switched on by the repository variable `UNITY_RUNNER=1`): EditMode, PlayMode and the headless one-day simulation, results published as a check by `dorny/test-reporter`. First green end to end on `bcf40d9`: **EditMode 398 total, 396 passed, 0 failed** (the two `[Explicit]` benchmarks skipped), **PlayMode 4/4**, the headless day clean, the whole job in 1 m 46 s. Branch protection on `main` requires both checks, a pull request and one approving review, with branches up to date; so **nothing reaches `main` except through a pull request with both tiers green** — agents included. Two runner traps are in `docs/lessons.md`, "Continuous integration". Plan: `docs/plans/vertical-slice.md`.
- **The starting map is a wooded meadow (owner decision 2026-09-16; barren before that), now with water in it:** 120 x 120 x 16, flat, grass in every cell, woodland at the natural generator's density with a clearing at the start, a stream or two and a pond or two kept clear of that clearing (2026-09-16, ADR 0009), and no rock, ore or props — `NaturalMapGenDef.MakeWooded()`, chosen by `OdysseyBootstrap.woodedMap`. Trees are edifices that block nothing (a colonist walks through woodland) and felling them is the first job on the road to building. The bare board (`MakeBarren()`, `woodedMap` off) is kept as the test baseline on which anything that is not grass is a bug. The ruined-city generator is still present and still tested, but it is not what the scene loads. Tufts of grass are drawn over the ground as pure decoration by the mesher (`GroundScatter`, `OdysseyBootstrap.grassScatter`, default 120 per hundred cells, 0 to switch off); they are not simulation objects, block nothing and are not in the save.
- **Designations and the first job line, 2026-09-16.** The player's standing orders live in `Sim/Designations/DesignationGrid.cs` (one byte per cell; Mine, Deconstruct, Fell; validated on placement, hashed, saved, published in the snapshot (a per-layer `Designations` byte channel then; replaced 2026-09-16 by the sparse whole-world `Orders` list, so an order given above the slice is drawn)). Player commands reach the colony through the intent bus: `Designate(cell, A = kind)`, `CancelDesignation(cell)` and `SetForbidden(A = thing, B = on)` are handled by the components that own them via `SimWorldBuilder.AddIntentHandler`, and the colony is wired in one place, `ColonyComposition.AddColony`, used by `ColonyWorld`, the bootstrap and the screenshot harness alike. **Felling is the first job that edits the world:** `FellWorkGiver` (work type *cutting*, scanned before hauling) hands a marked, reachable tree to a colonist; `FellJobDriver` walks into the tree's cell, works `Job_Fell.workTicks` (600, **ASSUMED**), clears the order, and defers the edit to the structural phase, where the tree goes and `WoodPerTree` (20, **ASSUMED**) wood appears as one stack that the existing haul takes to the stockpile. What the colony starts with is a `ScenarioDef` (`Sim/Pawns/ColonyScenario.cs`: colonists, meal piles, meals per pile, beds, stockpile cells, salvage, `startingFellRadius`), built in code like `PawnContent.Core()`; the scene's `ScenarioDef.Playtest` marks every tree within 10 cells of the start before its first tick so the colony has work at once, `ScenarioDef.Bare` gives the same colony and no orders and is what headless runs and tests build on, and `OdysseyBootstrap.scenario` picks one by name — it flips to Bare when the UI line's drag-to-designate tool lands (that line owns the tool and reads the snapshot channel). Wood is `ItemIndex.Wood`, drawn as a log pile, named `ui.res.wood` in the registry with `ui.arch.tool.fell` for the order. Not yet: felled trees are not in the save (the grid itself is not saved, OQ-08). **Stacks merge (OQ-24, 2026-09-16):** a cell has space for a load only when the whole load fits under the def's `stackLimit` (wood 75, meals 20 so a starting pile is one stack, salvage 1); a haul dropped onto a stack of the same def joins it; the destination rule is filter, then space, then priority, then nearest; and a hauler with nothing loose to carry re-stows from a lower-priority pile into a strictly higher one that accepts the thing (`ColonyItems.StoredItems`, `HaulWorkGiver`). `StockpileTests` holds the four-pile fixture.
- **Skills (U20) landed 2026-09-16 (OQ-14).** A colonist has experience per skill (`SkillIndex`: hauling, cutting), a `Passion` per skill rolled once at placement from the seed and the pawn id, and a level 0–20 that `Pawn.SkillLevel` reads off the experience by the `SkillDef` table and is never stored. Work grants it: a job driver calls `Work(ctx)` on the ticks that are the work (the fell swing, the haul carry), which pays `JobDef.experiencePerWorkTick` into `JobDef.trainsSkill`, scaled by passion (×0.35 / ×1.0 / ×1.5, `a-01-pawns.md`) and slowed to a fifth after 4,000 points in a day; `SkillSystem` decays levels ten and up on the Long tick group. Experience is stored in thousandths of a point so every rate is an integer. Experience, passions and the day counter are hashed and saved (`SkillTests`, 13 tests). **ASSUMED** and marked in the code: the base rate per work tick (110 thousandths), the passion odds at spawn (35 % minor, 15 % major), the nine decay-ladder values between the two `a-01` measured, and the second slope of the level table (2,000 a level from 10, which is what reconciles `a-01`'s formula with its 265,000 total). Not carried: the 1,000-point grace before a level is lost (needs a stored level), the passion mood buff, and any effect of level on work speed — nothing reads the level yet except decay, and the HUD's Skills tab still says "no skill data yet" because `PawnView` does not carry it (a contract change for the UI line).
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
- **The HUD was rebuilt to an approved specification, 2026-09-16 (`docs/design/14-hud-layout.md`).** Same information, and **10.8 to 11.0% of the viewport at rest**, measured on the real panel at all three resolutions the criteria name. The “roughly 31% before” the brief quotes is the specification's own figure and was not re-measured here — the old HUD had no geometry model and no test that could produce one, which is most of why this pass exists. The first pass proved the catalogue and then could not be judged: it printed its own debug codes at the player ("A1 · RESOURCES"), carried three development notes as game text, put two or three letters of an icon key in a coloured tile wherever a picture was missing, and had accumulated **fourteen font sizes between 7 px and 16 px**, none chosen against the others, because every region picked its own as it was written. **No game logic changed**; this is the HUD layer only.
  - **Contrast comes from two always-on scrims, not from the panels**, and that is what let the panels shrink. A panel dark enough to hold 13 px text over bright terrain has to be nearly opaque, and a screen of nearly opaque panels *is* the coverage figure. `HudContrast` measures it rather than asserting it by eye: over a panel over pure white (worse than any terrain in the game, so the figure cannot go stale when a biome lands) primary ink reads **11.6:1**, meta **6.6:1**, dim **4.5:1**. Faint (.35) does not clear the threshold and is therefore used only for hotkey caps.
  - **The design system is in the Unity-free assembly, which is the whole reason the acceptance criteria are testable.** `HudTheme` (palette, spacing, radii, nine icon categories), `HudType` (six steps and no seventh, mono for every figure), `HudLayout` (where every region is anchored and how tall it comes out), `HudCommands` (the bar and its overflow rule) — all in `Odyssey.Hud`, so "no two panels overlap at three resolutions", "coverage at or under 18%", "every command item shows a hotkey" and "body text over 4.5:1" are **fast-tier arithmetic** rather than opinions in a review. 93 Hud tests, up from 60.
  - **And the PlayMode gate is what stops the model describing a screen nobody is looking at.** `HudGeometryTests` lays the real HUD out in a real panel at 1280x720, 1920x1080 and 2560x1440 and measures the boxes UI Toolkit actually produced: no overlaps, coverage measured, and every realised box **inside** its modelled box. The model was exactly right for the stores and clock panels on the first run, which is the kind of agreement that makes the pair worth having.
  - **`Hud.uss` sets no type at all** — not one `font-size`, `letter-spacing` or `-unity-font-style`, and a test asserts it. Type comes from `HudType` through `HudText`, because a size written in a stylesheet can only be tested by parsing the stylesheet, and two sources for one decision is exactly how fourteen font sizes happened. The sheet keeps its colour literals, which has always been its stated position, but `HudStyleSheetTests` now parses thirty colour declarations and forty length declarations out of it and compares them against the tokens — so the sheet can no longer hold a literal nobody agreed to.
  - **Three-letter placeholders are gone** (ADR 0007, amended again). MEA / WOO / SCR read as truncated data, and a filled colour tile behind a value outshouts the value. A missing glyph is a **single-colour outlined square** at a 1.6 px stroke in the key's category colour, and a PlayMode test fails on any two- or three-letter capitalised fragment reaching a HUD label. **Chrome is drawn rather than imported**: `HudGlyph` renders play, pause, forward, fast-forward, chevrons, close, hamburger, alert triangle and info as `Painter2D` paths at Lucide's own proportions — one path each, no texture, no atlas, no licence. The icon *pipeline* is untouched: same symbolic keys, same three sizes, and when the owner's sheets land `IconBadge` becomes a sprite lookup with nothing about the layout moving.
  - **Two OFL faces are committed** under `Assets/Odyssey/Presentation/Ui/Fonts/` with their licences: Archivo Narrow for words, IBM Plex Mono for every figure. A narrow face is not taste — the bar carries eleven labelled items across the screen and the criteria forbid both an abbreviation and an item running off the edge. **Weights 600 and 700 are synthesised**, because Google publishes Archivo Narrow only as a variable font and Unity's importer takes its default instance; `HudType.BoldFrom` is the one place that split lives. A clone without the fonts keeps the panel's theme face and the identical layout.
  - **The reference canvas moved from 1200x800 to 1920x1080.** Every anchor in the specification is a 1080p pixel, so the old reference drew all of them 1.6 times too large. `PlayScene.HudReferenceResolution` and the committed `HudPanelSettings.asset` both carry it; the play scene was **hand-edited rather than regenerated** to bind the two fonts, because regenerating a scene without the packs present is what rewrote `ModuleCatalogue.asset` with empty prefab references once already.
  - **The command bar may not wrap and may not overflow**; anything that does not fit moves into Menu, from the right, and Menu is never dropped. That is what lets the inspect pane above it stop guessing at the bar's height — a coupling the old sheet carried in a comment admitting it was one. The reflow uses widths UI Toolkit actually laid out, measured once while every item is present and **`visibility: hidden`**, because a `display: none` element has no width to read and the bar would otherwise have to be drawn overflowing for one frame to discover that it overflows. Hotkeys avoid every key the game already uses (M/C/X arm tools, R/F move the slice, V cycles visibility, Space and 1–3 are the clock), so Research is **E** and Colonists is **O**; **B** and **Escape** are live.
  - **The alerts panel is real and hides when it is empty.** `AlertModel` raises three conditions the published frame can support — starving, close to breaking, a colony standing idle — leads with the actionable clause and trails the detail dim. Each latches with hysteresis for the reason `AlertWatch` already gives about chimes; idle is *sustained* rather than latched, because it is momentary rather than a level. **Stores marks a falling stock** against a baseline resampled every ten seconds rather than against the last refresh: the panel refreshes four times a second and a hauler picking a stack up lowers every count on screen, so frame-to-frame comparison would flicker the whole panel amber all day.
  - **Every per-frame string is now built only when it would read differently**, because ADR 0003's flip condition F1 says the HUD allocates nothing per frame in steady state and `HudStressTests` asserts zero gen-0 collections. `InspectModel.Position` was rebuilding `"at 78, 59"` fifteen times a second; the alert leads were rebuilt four times a second for as long as an alert stood. Both now return the same instance until the value moves, which is also what makes the view's reference comparison work.
  - **Three model faults the new tests found before any of it was drawn.** The colonist strip is centred on the *screen* while the panels either side are different widths, so a strip sized to the free span and then centred poked seventeen pixels into the clock column. The depth rail is the one region the *world* sizes, so it has to be the one that gives: its cells shrink in proportion rather than the rail overflowing, and above all rather than silently losing its last layers, which was a playtest report. And the rail runs down to the command bar, not to the bottom of the screen.
  - **Escape now unwinds tool → Build palette → settings → menu**, one rule in `SettingsDirector.Escape` decided in the fast tier. The palette is new to that order because it used to be a permanently open column.
  - **Not done: nobody has looked at it.** No test can say whether it reads well. **Press Play in `Play.unity`.** Also open: real icon art (every game glyph is an outlined square), no backdrop blur (UI Toolkit has none, and the scrims carry the contrast that mattered), and true 500/600/700 weights.
- **The interface has a scale, and the camera turns freely (owner, 2026-09-16, both on the HUD rebuild branch).**
  - **The type read too small on 4K**, which is not a contradiction of the panel scaling with the
    screen: it subtends the same angle as at 1080p, but physically identical is not perceptually
    identical at arm's length from a large panel, and the HUD this replaced was drawn against a
    1200 x 800 reference, so every glyph on it was 1.6 times larger. The fix is the setting
    `09-ui-and-input.md` §9 D4 planned for all along, which **closes D4**: a **ladder** of 80 / 90 /
    100 / 110 / 125 / 150 per cent rather than a slider, because a HUD at a fractional scale puts
    its one-pixel hairlines between pixels. It works by **dividing the reference canvas** — at 125%
    the panel is told 1536 x 864 — so nothing else has to know: the anchored layout adapts, the
    colonist strip re-clamps and the command bar moves its tail into Menu. `UiScaleTests` walks
    every rung and asserts no two panels overlap at any of them. **The default is read off the
    screen**: 100 below 1440p, 110 at 1440p, **125 at 4K**, and the defaults are checked against the
    coverage ceiling (125% comes to about 17%, inside the 18% budget). Above that the player is
    knowingly trading board for legibility and each rung's tooltip says so. **The panel settings
    asset is copied before it is written**, the same trap the sky material already taught.
  - **B17 is tabbed: Interface, then Graphics.** It opens on Interface, because the scale is the
    one setting in it that changes the panel you are looking at while you look at it. Two new
    registry names, `ui.settings.interface` and `ui.settings.uiscale`; wiki and `Registry.g.cs`
    regenerated.
  - **Q and E rotate freely while held** (`SliceCameraRig.rotateSpeed`, 90°/s, multiplied by shift
    like every other camera speed). They used to snap ninety degrees a press, which is the genre's
    convention and assumes a board that reads the same from four sides — this one does not, since
    it is layered and its slice is cut at an angle, so a wall hides different things at fifty
    degrees than at ninety. The *target* angle is driven rather than the yaw, so mouse orbit and
    key rotation share one smoothing and cannot fight over the angle.
  - **The command bar's hotkeys were wrong and the test written to catch that passed anyway.** Five
    of eleven — W, S, E, A and B — were already camera keys (WASD pans, Q/E turn, B cycled the
    below-slice mode), and the guard compared against a **reserved list written from memory** that
    was missing all five. The panels are on **F1–F9** now (unclaimed, conventional for top-level
    panels, and narrow, which the overflow budget likes), **Build keeps B**, and the below-slice
    cycle moved to **shift-V** beside the above-slice cycle it belongs with. `HotkeyClashTests`
    replaces the list: it **greps the Presentation assembly** for every `keys.somethingKey` and
    allows a command's key only in the one file that reads it on that command's behalf. The lesson
    generalises and is in `docs/lessons.md` — *a reserved list maintained by hand is wrong the
    moment somebody binds a key without updating it.*
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
  - **Nothing grows inside a working, and that was a reported bug** (owner, 2026-09-16: *"there is
    a bug with the new terrain where there is mine sites, the terrain is curved and so when people
    are mining — you can't see the colonists"*). The bank rule's "the step is earth" reads as
    though it had already covered a quarry and it does not: grass, bare earth and subsoil are all
    mineable (60, 60 and 160 ticks to clear), so a hole cut into the meadow has earth walls with
    open tops, which is every condition a terrace step has. **A bank grew in the cell that had just
    been cut**, filling it from its floor to the rim, and the miner standing in it was drawn up to
    the chest in ground. Measured on the played board: a 3 × 3 pit one layer deep grew **8** banks
    and swallowed **one of the five** colonists whole.
  - **The mark is `CellFlags.Discovered`, read for its other meaning**, through
    `WorldRenderModel.IsCutFace`. The two are coextensive rather than merely similar: the flag is
    set by `CellGrid.RevealAround`, which is called from exactly one place
    (`MineJobDriver.MineCell`), and worldgen sets it on nothing at all. So no new sim state, no new
    saved bit, and a loaded game draws the same pit. If a deep scanner ever reveals rock nobody has
    cut, `IsCutFace` is the one place that changes.
  - **It has to be asked of the floor, not only of the sides**, and the shortfall is invisible in
    the obvious case: mining a cell reveals all six of its solid neighbours, so a cut cell's four
    sides are all cut faces, `StepsAround` comes back empty, and the **hip** branch then looks at
    the *diagonal* neighbours, which nothing reveals. Guarding only the sides still drew 1 bank for
    one cut cell and 2 for a four-cell bench. A mined cell's floor is revealed too, so asking the
    floor catches every shape of working at once.
  - **A figure now stands on a bank rather than in one**, which is the same fault with the opposite
    answer: on the hillside the ramp is the picture of a hop and must stay, so the figure comes up
    to meet it. Measured on the played board: **three of five** colonists at the foot of a terrace,
    every one 1.500 m through the slope; afterwards 0.000 m. The decision left the mesher into
    `BankLayout` (where a bank is, which shape, what of, and `RiseAt`), `ChunkMesher` turns that
    into an instance and `PawnPose` stands a figure on it, so the two cannot drift. The levers went
    static with it — `BankLayout.Enabled` / `InWorkings` / `LiftFigures` — because a per-renderer
    lever would let banks be off while colonists hovered 1.5 m over the meadow.
  - **The tiling is what makes it safe.** `BankMesh`'s three shapes already had tests saying they
    agree where they meet; those same properties are what make the surface continuous for a walker,
    so a run of bank, leaving one and walking onto one needed nothing extra. **Going up take the
    higher of chord and ground, going down fade the lift out** — a hop's chord runs 1.5 m *inside*
    the block it climbs (true before banks existed), while the drawn ground going down is a step
    function and following it would teleport the figure. The fade has to be at **both** ends: the
    first version forgot the cell being entered, which is a metre and a half dropping off a step
    *into* a bank, and a terrace has banks at the bottom of it by definition.
    `BankFootingTests` samples 400 points across each step and allows no jump over 5 cm.
  - **Open: whether the boots need the bank's own gradient.** `Footing` plants both feet off
    `GroundRelief.SlopeAt`, the rolling field, which is eight degrees where a bank is fifty — a
    0.3 m stance spans about 0.36 m of slope the feet know nothing about. The sheet could not
    settle it: the colonist the harness picked wears a full-length skirt. §2b's standing rule
    predicts the lift alone is enough.
  - Judge it with **`Odyssey → Presentation → Check a bank underfoot`**
    (`scripts/unity.sh shot Odyssey.EditorTools.BankCheck.Run`), which **builds** its step (earth
    laid beside a colonist — a cut one would grow no bank), clears the woodland out of the camera's
    way, and prints `MeasuredFootGap` for every colonist in a bank rather than only the subject,
    because a figure sunk into a ramp and one standing behind it look identical from every bearing.
  - Judge it with **`Odyssey → Presentation → Check a quarry`**
    (`scripts/unity.sh shot Odyssey.EditorTools.QuarryCheck.Run`), which cuts a 3 × 3 pit **under a
    colonist's feet** — `MineCell` steps whoever was standing on a cell down onto the floor it just
    cut — and shoots it with `ChunkRenderer.BanksInWorkings` on and then off. It prints how many
    colonists are standing in a bank, which is the number the whole thing is about. Five new
    `BankMeshTests` and 752 EditMode (750 passed, 0 failed) with it.
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
- **A click reaches anything drawn solid, at any depth (owner, 2026-09-16; ADR 0006 amended again,
  `06-rendering-and-camera.md` §3c).** The report: *"on my default depth level I can only select
  objects/things on my level — I couldn't select the stones for mining, for example, I should be
  able to click on an object in 3D space"*. This is the second half of the change above: once the
  whole stack above the surface drew **solid**, `SlicePicker`'s "never above the active layer" rule
  was handing the player a world they could see and not touch. **The rule is now: solid is
  clickable, a ghost never is** — which keeps the sentence that was actually carrying the Going
  Medieval misclick argument, since what makes a misclick a misclick is operating on a depth *cue*.
  Underground, where the layer overhead is x-rayed, a click still cannot leave the active layer
  upwards and **nothing about the old behaviour changed**.
  - **The band is asked of `SliceSettings`, not decided in the picker** —
    `HighestSelectableLayer` / `LowestSelectableLayer`, beside the methods that decide what is
    drawn, so "selectable" and "drawn solid" cannot drift apart. It is capped at
    `WorldRenderModel.HighestOccupiedLayer`, the same cap `ChunkRenderer.Render` uses, or a tall
    map would march through empty sky on every hover.
  - **A face belongs to whatever you clicked, or the click misses (owner, second correction the
    same day: *"I still wanted to select the tile below it or not at all"*).** The first attempt
    kept the picker's old convention — a floor crossing returns the *air* cell whose floor it
    crossed — which was never chosen: on one layer the air cell was the only cell on offer. Reaching
    up and down a stack it reads as clicking a rock and selecting the sky. Now: an occluder is its
    own cell; a cell holding an **edifice** or a **built floor slab** is its own cell; bare ground
    resolves to **the block beneath**, whose top face that is; and nothing beneath is nothing
    picked. A solid cell's top face and the floor of the air cell above it are **one surface at one
    distance**, so two layers bid at the same ray parameter — and they now resolve to the same
    block, which is what makes the tie harmless. Where they disagree is a tree, and **a thing beats
    bare ground**, failing which the layer nearer the slice wins. The occlusion hit's reported
    distance had to be the **drawn top face** rather than the slab clip, which the relief had
    opened up by half a metre.
  - **The edifice rule is not an exception and leaving it out would have broken felling outright.**
    A tree is an edifice that blocks nothing standing in the walkable cell, so "select the tile
    below" taken literally hands back the ground under every tree and Fell can never be ordered
    again. **And the order had to be lifted to match, for a drag**: a click on a tree names the
    tree, but a fell box begun on open grass anchors a layer too low and every cell of it is
    refused *in silence* — the tool swept across a wood and nothing happening.
    `DesignationGrid.Designate` reads a Fell order named at solid ground as the tree on it, and
    `Cancel` mirrors it. Only felling; mining means the block itself, which is what the click now
    gives. It is the same relation `CanMine` already knew from the other side.
  - **A surface that is not drawn is not clickable.** The active layer's ceiling is the slab of the
    layer above and the renderer meshes it away, so that one layer offers only what occludes: rock
    over your head stays pickable, the dropped slab does not.
  - **Colonists follow the same band**, with one extra rule for a ray: a pawn must be at or above
    the layer of the cell the ray ended on. The pick ray descends, so everything it met before the
    ground is at or above it — the cheap stand-in for comparing ray distances, and exact for the
    only camera this game has. It is what keeps a miner eight layers down unclickable while a
    colonist standing on an outcrop becomes clickable.
  - **A drag box still cannot climb a wall**: `DesignateDirector` pins every cell to the *anchor's*
    layer, which used to be a consequence of the picker's clip and is now a rule that class holds
    on its own.
  - **The order channel had to go whole-world, or the fix read as doing nothing.** Designations
    were published as two byte-per-cell arrays of the **active layer**, which was the right shape
    while a click could not leave it. Mark an outcrop standing over the meadow and the order was
    accepted, worked and never drawn. `WorldSnapshot.Orders` replaces both: a **sparse counted list
    of `OrderView`** (whole-world cell index, kind, progress) covering every order anywhere, which
    is *smaller* than the one layer of mostly nothing it replaces — a layer is 14,400 cells and a
    colony has tens of orders. `DrawStandingOrders` filters to the drawn band. Two side effects
    worth knowing: the double-buffer clear the old sparse write needed is gone by construction (a
    counted list cannot hold a stale byte), and `SliceLayer` no longer decides what is published,
    only what is drawn.

  - Five new `SlicePickerTests` pin both halves — the outcrop, the ghost, the meadow tie-break, the
    shaft below, the suppressed ceiling. The seven existing tests call the four-argument `Pick`,
    which means "the active layer alone" and is unchanged.

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
- **The golden-hour look: interviewed and researched 2026-09-16, not yet planned or built.** The
  owner asked for the lighting, rays, warm sky-into-fog and depth of field of *Station to Station*.
  Interview in `docs/research/look-interview.md`, references in
  `docs/reference/screenshots/station-to-station/` (six images, described in that folder's README),
  five research files (`d-12-urp-post-stack`, `d-13-light-shafts`, `d-14-aerial-perspective`,
  `b-station-to-station`, `b-low-sun-readability`) summarised in `docs/research/INDEX.md`.
  **Nothing under `Assets/` has changed and no design or ADR exists yet** — the next phase is a
  design section, an ADR, execution units and a `Check the light` contact sheet, and it waits for
  the owner. The grounding headline is that **no volume stack has ever been in effect**: the URP
  asset's default profile GUID resolves to nothing and `DefaultVolumeProfile.asset` is orphaned, so
  the project has never had tonemapping, grading, bloom, depth of field, vignette or anti-aliasing.
  Two recorded decisions are overridden by the owner and must not be re-argued from the old notes:
  the **72° sun in `PlayScene.BuildLighting` comes down to a raking 25–35°** (the comment there
  rejecting a 50° sun is superseded — the real fix is Shadow Strength below 1, which that decision
  never tried), and **bloom is adopted** against `d-09-stylised-rendering.md` §3.4's caution.
  The load-bearing findings: the rays may be **geometrically impossible at the default framing**
  (at a 48° pitch the sun can sit behind the camera, where a radial blur has nothing to radiate
  from), so a framing experiment comes before any shader; the tilt-shift is probably **a screen-Y
  blur rather than depth of field**, because URP's Gaussian blurs only the far field; the haze is
  **exp2 fog plus a sky given the fog colour**, not a fullscreen pass, which the camera geometry
  cannot justify; and the cascade splits are **already wrong for this camera**, spending half the
  shadow atlas on the empty air in front of it. Budget: about 2 ms more, quality-tiered, and **no
  measured millisecond figure for any URP post effect exists in any public source**, so every
  number must come from `FrameTimeTests` under the real player loop.
- **Escape opens a settings panel, and the graphics levers moved out of the inspector
  (2026-09-16).** Panel B17's M1 stub: a centred panel with one Graphics section of four
  switches — shadows, surrounding land, grass tufts, ground relief — thrown while the colony runs.
  It exists because every graphics lever was an `OdysseyBootstrap` inspector field, so comparing
  two looks meant stopping play, editing a number and starting a board that is no longer the board
  you were judging; nearly every look decision on record ended asking for the owner's eye in
  `Play.unity`. `SettingsDirector` (Unity-free, fast tier) holds what the panel holds and **what
  Escape means**; `SettingsPresenter` does what it says and turns a boolean into a call on the
  renderer; `HudShell.BuildSettings` draws it. **Escape is now decided in exactly one place** —
  it was the designate tool's alone, and two components reading one key would have disarmed the
  tool and opened the panel on the same keystroke, so `DesignatePresenter` reads no key and
  exposes `ToolArmed` / `PutToolAway` instead. **Two switches are free and two cost a remesh**:
  shadows and the surround are read as the frame is submitted, while grass and relief are baked
  into instance matrices at mesh time, so they are followed by `WorldRenderModel.Remesh()` (bumps
  the version, nothing else) and a skirt rebuild; the row's tooltip says which it is. **It is not
  modal, deliberately** — the world runs and the camera orbits while it is open, because watching
  the board is the entire point; clicks stop at the panel edge through the shell's existing
  pointer gate, and the price is that the tool keys still work behind it. **Preferences are on the
  machine, never in the colony save** (`PlayerPrefsSettingsStore`, the only `PlayerPrefs` user in
  the project, behind `ISettingsStore` because the Hud assembly has no UnityEngine): a graphics
  setting is presentation like the tufts themselves, with no cell, no save and no hash. **The
  scene still decides how a session starts** — the panel is seeded from the bootstrap's fields and
  a stored preference is laid over that, so preference beats scene beats nothing, and a panel
  cannot change the board merely by existing. Six `ui.settings.*` names are in `icon-keys.csv`
  with the wiki and `Registry.g.cs` regenerated, and `RegistryTests` now holds the panel to the
  CSV. Design: `10-ui-panel-catalogue.md` B17, `09-ui-and-input.md` §6 case 6. **Not built and not
  wanted yet:** a real modal, UI Toolkit `Toggle`/`Slider` controls (the switches are lit chips,
  the idiom this HUD already uses), audio, interface scale, accessibility and keybindings, all M8.
  The golden-hour work fills the Graphics section out, since its quality tier is a settings
  surface by definition.
- **There are trees on the background hills now (owner request, 2026-09-16).** The surround's wood
  stopped 90 m past the rim, where the hills have risen about six of their fifty metres, so every
  hill in the background was bare — and a bare hillside has nothing of known size on it, so the eye
  cannot place it and it flattens into a green backdrop. `SkirtLayout.BuildFarTrees` runs a second
  wood from 90 m out to **900 m**, chosen against two numbers that already existed: hills reach full
  height at 700 m, fog is opaque at 1,100 m. Scattered on a **15 m lattice with a jitter** rather
  than the cell grid, because the band is four million square metres and cell resolution would be
  670,000 samples for two thousand trees; the jitter is what stops it reading as an orchard, and a
  test holds that. **The cost is batches, not triangles**, and it was measured rather than assumed:
  at the near wood's 80 m sectors with all sixteen tree kinds the meadow drew **1,154 surround
  batches against 72** before, so the far sector went to 800 m and the far wood is capped to **four
  kinds** (at 300 m nobody can tell one conifer from another, so variety was a batch multiplier
  buying nothing). Measured under the real player loop with the city as a control: meadow **1.45 ms
  off → 1.38 ms on**, +52 draw calls, +2,577 instances, against a 5 ms budget — and since the city
  moved 1.70 → 1.84 ms between the same runs, ±0.14 ms is noise and **the honest claim is no
  measurable cost, not a speed-up**. Levers: `OdysseyBootstrap.skirtHillTrees`, and
  `skirtTreeDensity` scales it with the near wood. Design: `06-rendering-and-camera.md` §2a.
- **Two traps found while doing it, both in `docs/lessons.md`.** Rebuilding the scene in a worktree
  **without the Synty packs** rewrites `ModuleCatalogue.asset` with every prefab reference set to
  `{fileID: 0}` — 501 lines, exit code zero, no message — so a blanket `git add -A` commits a
  catalogue with no art and no failing test to explain it; the junction from the worktree lesson is
  the fix, and reading the diff is the guard. Worse, and independent of the packs: **the committed
  catalogue and `PlayScene.cs` had drifted apart.** The asset held a `terrain.marsh` row the builder
  no longer emitted, and lacked a `tool.hammer` row it did. Nothing could catch it — the asset is
  licensed art no test loads, the builder is editor tooling no test runs. The next rebuild for any
  reason would have dropped marsh to the untextured fallback, undoing the water work's marsh fix
  weeks later with nothing connecting the two. Marsh is restored in the builder and the catalogue
  rebuilt. **When a rebuild's diff shows a row disappearing, that is never churn.**
- **The rest of the look is deferred, not dropped (owner, 2026-09-16).** PR #50 carries the day cycle, the golden hour, the hill wood and the settings stub, with both CI tiers green and awaiting the owner's review. What is left is listed in order with its dependencies in `docs/plans/vertical-slice.md` under **"Deferred: the rest of the look"** — the Low quality tier (committed to in the interview, never built, and the only answer to the laptop budget since every figure on record is an RTX at 640 × 480), the new levers in the settings panel, the sun shafts, the tilt-shift, and marsh's name. **The governing fact is that nobody has pressed Play**: every judgement and every number in that PR comes from contact sheets and `FrameTimeTests`, and a sheet cannot show whether the light steps at speed 3, whether night is playable rather than merely pretty, or whether a seventeen-minute day is too fast. So the first item is not code. **One thing changed while it waited**: the shafts were blocked because a fixed overhead sun sits behind the camera at a 48° pitch, and the cycle now sweeps it east to west and low at both ends of the day, so the framing experiment `d-13` demanded has a real chance of paying.
- **The day runs: blue at noon, orange at dawn and dusk, dark at night (owner, 2026-09-16, design `06-rendering-and-camera.md` §2d).** The fixed golden hour below landed first and the owner reversed question 2 of the interview on seeing it, which is recorded in `look-interview.md` so that file does not read as stale. **`Daylight`** is a keyed table in the *Presentation* assembly — not the editor one, because a running game must sample it — and `DaylightDirector` applies it every frame from the tick. A table rather than a formula because no sun model can say that dawn should be held orange longer than dusk, which is an art direction. **Midnight is both the first key and the last**, so the wrap needs no special case and a test walks the whole day at five-minute steps failing on any jump. **Night is a readability floor, not realism**: the sun drops below the horizon but is never switched off, because a directional light at zero flattens every face to one value and the board reads as a paper cut-out. **The sky material is copied, never edited** — it is an asset on disk, and writing to it at runtime in the editor would leave the sky wherever the clock stopped, permanently. The ambient probe is the only real cost and is throttled to a tenth of a game hour; the sun, ambient and fog are not, because stepping those shows in the shadows. **Nothing here is simulation** — a pure function of the tick, not saved, not hashed, unreadable from the sim, so a colonist at midnight is not blind. Judge it with `Odyssey → Presentation → Check the daylight`, which shoots eight hours at two pitches. Levers: `OdysseyBootstrap.daylightCycle` and `sun`.
- **The golden hour is lit, 2026-09-16 (design `06-rendering-and-camera.md` §2d).** The look the
  owner asked for, as far as it can go without two measurements. **One file owns the palette** —
  `Assets/Editor/Odyssey/GoldenHour.cs` — because the effect rests on an identity that is invisible
  if its halves live apart: the colour the distance fades to *is* the colour the sky is at the
  horizon. **The grounding headline was that there had never been any post-processing**: the
  pipeline asset pointed its default profile at a GUID resolving to nothing and the one profile in
  the repo was orphaned, so no tonemapping, grading, bloom, vignette or anti-aliasing had ever run.
  Now: Neutral tonemapping (never ACES, which skews exactly the warm highlights this needs), HDR
  grading (in LDR the sun clips before the grade sees it), white balance, bloom above a threshold of
  1, a light vignette, and **SMAA** — chosen because FXAA destroys our one-pixel post-drawn outline,
  TAA would jitter it, and MSAA cannot touch it. **Two recorded decisions are overturned and the old
  comments are rewritten in place, not deleted:** the 72° sun comes down to **30°**, because the old
  decision blamed the angle for darkness that was really *shadow strength* (0.6 keeps the key
  light's hue in shadow and costs nothing); and **bloom is adopted** against `d-09` §3.4, which was
  written for a painted look. **A real shadow bug turned up:** cascade splits are fractions of
  distance *from the camera*, and this camera never sees ground nearer than 50 m, so the stock
  splits spent half the atlas on empty air — they now start at 0.30, with distance 50 → 250 m and
  normal rather than depth bias, which is the grazing-angle lever. **Fog moved onto the board
  deliberately**: exp2 at a density computed rather than chosen (1% at 50 m, a third at the rim, 97%
  by 900 m), where the old linear pair started past the far corner and is why the board had no depth
  in it. **Two faults found by photograph**, one silently true for months: URP keeps post-processing
  *per camera* and defaults it off, so every contact sheet ever taken here was of an ungraded image;
  and the first ambient put the woodland in near-silhouette, since a low sun barely reaches a
  crown. **Not built, and waiting on measurement rather than effort:** the sun shafts, because at a
  48° pitch the sun can sit behind the camera where a radial blur has nothing to radiate from, so a
  framing experiment comes first; and the tilt-shift, because URP's cheap depth of field blurs only
  the far field and cannot make a band at all. **Still wants the owner's eye in `Play.unity`.**
- **Whatever hides a selected colonist is drawn as a ghost (owner, 2026-09-16; design
  `06-rendering-and-camera.md` §3d).** The report was trees getting in the way of seeing the
  colonist: the board is a wood, the camera orbits rather than cuts, and a selected colonist who
  walks under a canopy is gone until the camera is spun to find a gap — which loses the bearing and
  has to be done again the moment they move. Now anything standing on the line from the eye to a
  selected colonist is drawn with the x-ray ghost material at `ChunkRenderer.SightFadeAlpha` (0.22)
  for as long as it stands there. Nothing else changes: the geometry is still submitted, still
  occludes what is behind *it*, and is still clickable; no cell, no save, no hash.
  - **It is a segment against real geometry, not a cell march**, and that is the whole design. A
    tree's cell is the one its trunk stands in, while what hides a colonist is its crown, six
    metres up and a cell nearer the camera — so marching cells fades the wrong ones.
    `SightLines.Blocks` tests the segment against the instance's own world bounds, which gets a
    tall thing right for the same reason it gets a wall right and needs nothing from the mesher.
  - **The verdict is taken on the module, not the part**, or a tree's crown fades while its trunk
    stays solid. A bucket stores `placement * part.Local`, so the placement is recovered once per
    bucket and the module's own bounds placed by it.
  - **The line stops at the chest** — the point the bracket is drawn at and the hit-test aims at.
    Geometry past the colonist hides nothing, and the floor under them is more than a beam radius
    below the end of the line, so the fade cannot open a hole under the person just selected.
  - **Two grains keep it cheap:** `Touches` is asked once per chunk and says no to nearly all of
    them; only inside the survivors does the per-instance test run, and grass is skipped outright
    (a tuft hides nobody and tufts are most of the instances). **A chunk's bounds are one layer
    high, so the coarse test needs an allowance for taller geometry** (`TallestModuleMetres`, 12 m)
    — without it the coarse test rejects the very chunk holding the crown and the feature does
    nothing at all while every per-instance test still passes. Lines are capped at eight, so a box
    selection of the whole colony cannot make the cost grow with the selection.
  - **Measured at the board camera's 48° pitch:** a nine-metre tree occludes only within about
    eight metres of the colonist, because the sight line rises 1.11 m per metre of ground. That is
    the geometry, not a shortfall — a tree further back is below the line and never hid anybody.
  - **Not faded:** other colonists, items and the live animated figures — a skinned figure would
    mean swapping materials on its renderers rather than partitioning an instance array.
  - **On by default, said in all three places it can be said.** The field initialiser,
    `SettingsDirector`'s starting state and the serialised scene: a field absent from the scene
    YAML falls back to the initialiser, so `Play.unity` was quietly right without saying so, which
    is the arrangement that breaks silently on the next rebuild. The scene names it and
    `PlaySceneContentsTests.TheSceneOpensWithTheSeeThroughFadeOn` guards it.
  - Levers: `OdysseyBootstrap.seeThroughToSelection` / `seeThroughRadius` / `seeThroughAlpha`, and
    a switch in the settings panel's **Graphics** tab (`ui.settings.seethrough`) — Graphics rather
    than Interface because the tabs divide on how the HUD is drawn against how the world is. A free
    lever: read as the frame is submitted, so no remesh. Judge it with **`Odyssey → Presentation → Check the see-through fade`**
    (`scripts/unity.sh shot Odyssey.EditorTools.SeeThroughCheck.Run`), which *finds* the colonist
    with the most geometry in the way at four bearings rather than being told where one is.
  - **Verified: it compiles and the geometry holds.** Fast tier 428 Sim and 51 Hud; the Presentation,
    Editor and test assemblies compiled headlessly against `Library/ScriptAssemblies` (the owner's
    editor held the project); and the nineteen sight-line cases were *run* outside the player
    against the built DLL, all passing. **Not verified: `scripts/unity.sh test editmode`, the
    PlayMode frame-time gate, and whether any of it looks like anything** — `SeeThroughCheck` is
    unrun, so the alpha and the radius have the standing of an argument.
- **Sim vs UI vocabulary is deliberate:** simulation systems are *subsystems*, presentation-side coordinators are *directors* (`01-architecture.md` §3a). Do not unify the two words.
- **Phase 3 (design): complete 2026-09-15.** `docs/design/` 00, 01, 02, 03, 04, 05, 06, 07, 08; ADRs 0001, 0002, 0005; and the execution plan `docs/plans/vertical-slice.md` (32 units, M0→M3). **The Phase 3 → Phase 4 hard stop was cleared by the owner on 2026-09-15; execution is under way.**
- **Interface, icons and content naming (the UI line of work), 2026-09-15.** Design `09-ui-and-input.md`, `10-ui-panel-catalogue.md`, `11-icon-library.md`; ADRs 0003 UI framework, 0004 sim-to-UI contract, 0006 layer visibility, 0007 pixel-art icon pipeline; research `g-01`, `g-02`; mockups `hud-v1.html` and `hud-v2.html`, **both historical since the HUD rebuild of 2026-09-16** — the built interface is specified by `docs/design/14-hud-layout.md` and neither mockup was updated to it, so read the design doc and the screenshot rather than either mockup for what the HUD looks like. **Layer visibility decided:** x-ray by default with six modes shipped for playtest, amended by Lane B so that nothing above the active slice is ever a pointer target. **Icons:** 382 keys enumerated, 268 mapped to the owner's eight pixel-art sheets, 114 gaps listed in `11-icon-library.md` — the largest being people, since no sheet contains a human figure. **Names:** all 29 proper nouns proposed and awaiting the owner's veto, in `docs/design/proper-nouns.csv`.
- **Colonists are drawn from 61 Synty characters** across all four packs, chosen per pawn by id (`ColonistLook`), each with the locomotion clip set matching its name. The cast is the `Colonists` array in `PlayScene.cs` — strike a name and rebuild the catalogue to remove a face. Eight rigged-but-not-people prefabs (scarecrow, skeleton, robots, hologram, the two in underwear) are listed there as deliberately excluded.
- **Labels, not abbreviations (owner, 2026-09-16).** Until real icon art is in the build, every icon-bearing control draws its **full name** beside the icon; the two-to-four character badge is only a placeholder-tile and sub-32-pixel rendering rule, never the thing that names a control. Icon-only survives as a toggle and may become the default again on an owner screenshot call. Recorded in `09-ui-and-input.md` §7a (which closes open question D1), `10-ui-panel-catalogue.md`, ADR 0007; `hud-v2.html` now opens in *+ label* mode. Layouts are authored against the longest label, not the icon box.
- **Still needed from the owner for the UI line:** copy the eight icon sheets into `art-source/icons/sheets/` (that folder's README names them), and approve or strike the proposed names.
- **Cell size: FIXED at 2.5 × 2.5 × 3.0 m** (`docs/adr/0002-cell-size-and-layer-model.md`).
- **Architecture: FIXED — plain C# structure-of-arrays with Burst on measured hot paths** (`docs/adr/0005-simulation-architecture.md`), decided by a two-candidate benchmark in which both implementations produced the identical state hash. Plain 1.423 ms/tick vs ECS 2.446 ms at 250 × 250 × 40.
- **Top technical risk: pathfinding cost.** 65% of the measured tick is A-star. The explanation once recorded here — that these were futile searches for unreachable targets — was **falsified by its own follow-up experiment**: only 14% of budget exhaustions were unreachable, and under 1% on a structured map. `05-ai-and-jobs.md` and ADR 0005 were corrected. The fix is hierarchical search plus a better heuristic, with the district-id reachability check (`d-04-pathfinding.md`) measured first thing in M2. At a 3x hardware discount the tick leaves 3.8 ms of a frame for rendering; at 4x it leaves none.
- **Target: the playable MVP** (vertical slice, M0→M3) per `phase1-answers.md` Q8–Q9. Graphics bar: close to the concept renders (`d-03-rendering.md`). A look-check scene exists: `Assets/Scenes/Spikes/VisualBlock.unity` (regenerate via *Odyssey → Spikes*).
- Owner reference (2026-09-15): `github.com/RimWorldMods` for understanding RimWorld mechanics — clean-room rules apply, nothing is copied from it (note in `docs/research/INDEX.md`).
- **The first real icons are in the HUD, and the resting HUD is smaller again (owner, 2026-09-16).**
  Four of the owner's 32 px drawings now draw themselves instead of an outlined square:
  `ui.res.wood`, `ui.res.stone`, `ui.res.ironore`, `ui.res.scrap`. **This is the sprite lookup ADR
  0007 promised, arriving one key at a time** — `IconArt` maps a key to a texture under
  `Assets/Art/Ui/Resources/odyssey/icons/`, a key with art draws it and a key without still draws
  the square, so the HUD is correct at every stage between no art and all of it, and a single icon
  can be judged in the running game before the set is drawn. Each is exported by an **integer 2×
  nearest-neighbour upscale to 64 × 64**, point-filtered, no mips, uncompressed, sRGB, and
  `IconArtTests` holds every file in the folder to those rules rather than to an eye, because a
  bilinear icon costs the second atlas page in silence. Real art is drawn **untinted**: the
  category colour says what a *placeholder* stands for and a picture says that for itself.
  - **The `Resources` namespace is flat and shared with every installed package.** A plain
    `icons/` folder collided at once — `Resources.LoadAll<Texture2D>("icons")` came back with
    Shader Graph's own `blackboard.png` at 16 px and bilinear, failing the ADR test on somebody
    else's art. Hence `odyssey/icons/`. A single-key `Load` would never have shown it.
  - **UI Toolkit's background defaults would have tiled a 64 px drawing inside a 17 px row** and
    shown the player its top-left corner. `background-size`, `-repeat` and `-position` are all set
    explicitly in `IconBadge`.
  - **Open, and the owner's call: the HUD draws icons at 16, 17 and 30 px, and ADR 0007 says not
    to draw pixel art below 32.** Measured on the real art: 30 px reads, 17 px loses the grooves,
    16 px goes to noise. And `ui.res.stone`'s ink fills 20 × 15 of its 32 × 32 frame where wood
    fills 26 × 24, so it reads as a pebble beside a plank. Equalising means scaling each icon's
    *trimmed* box by its own largest integer factor (what the sheet pipeline's `trim_mode` does),
    at the price of different pixel sizes between icons in a pixel-art set. Left faithful.
    Contact sheets: `Logs/icon-sizes.png` and `Logs/icon-rows.png`, made by a scratch script off
    `tools/icons/icons.py`'s codec.
  - **`ui.res.alloy`, `ui.res.concrete` and `ui.res.water` are struck out** (owner: "we won't be
    needing those"). Both CSVs, `HudTheme`'s category table, `LedgerModel.Planned` and one test's
    example row; wiki and `Registry.g.cs` regenerated — **405 keys, 265 with art, 130 gaps**.
    `11-icon-library.md`'s header counts are corrected and its §3 and §4 tables are now marked as
    predating the additions since. Nothing in the sim referenced any of the three; water *terrain*
    is untouched.
  - **Nothing selected, no pane** (owner). The inspect pane was a 41 px strip reading "Nothing
    selected", itself a cut-down of a three-sentence empty state; both were the interface talking
    about itself. It is `display:none` now — out of the layout, out of the measured region set and
    unable to take a click — and that is the HUD's default. `HudLayout.InspectHeight(0)` returns
    **0** and `Solve` gives an empty rect, so the model does not reserve a strip the shell never
    builds. What the model still cannot say is how tall an *item* or *cell* pane is; that was
    equally true before, and is only sound because every criterion stated against it assumes
    nothing selected or a colonist selected.
  - **The stores panel was 288 px and is 168** (owner: "far too wide"). 288 was never measured.
    `TheStoresPanelIsWideEnoughForItsRowsAndNoWider` asks the text engine how wide the panel's
    widest line really draws in the real face at the real size — **128 px**, and it is the
    *header* ("STORES" against "n / m"), not any commodity row. The extra forty is for figures
    rather than comfort: a fresh board holds two-digit counts and a stocked one holds five-digit
    ones, about 32 px more of mono. The test's bounds are two-sided, so a long name fails the
    lower one with a number attached. **A laid-out width could not have answered this** — a label
    with `flex-grow: 1` measures back as whatever the row gave it — so each child is asked for its
    own natural width instead.
  - **Coverage at rest fell from 10.8–11.0% to 9.0%** at all three resolutions, measured by
    `HudGeometryTests`. EditMode 916 total, 914 passed, 0 failed; PlayMode 23 total, 21 passed,
    0 failed; fast tier 428 Sim and 106 Hud.
  - **Still not judged by eye.** No test can say whether four grey-and-brown lumps read as four
    different commodities at 17 px. **Press Play in `Play.unity`.**
- **Colonists are recoloured, and there was never a body to dress (owner question, 2026-09-16; research `e-05-character-customisation.md`).** The question was whether a *generic* Synty model exists that we could make clothes for. It does not: a character is **one skinned mesh from scalp to boots with one material**, 69 of them across four packs, and nothing below the neck is separable anywhere. Synty's **Sidekick** line is the only route to real modular garments (free starter pack, ~£184 a pack or $30/mo) and the owner declined it — no purchases, no new art, *"recolour and retheme as much as we can without creating anything new"*.
  - **What made recolouring possible is a fact about the art, not a technique.** The pack atlas carries a labelled **`Character Colours`** block of small *flat* swatch cells, and every garment, hair patch and skin region is UV-mapped onto one of them — so **a vertex's cell already is its material identity**. No mask texture, no authored ID channel, no mesh surgery. `SwatchProbe` measured the claim before anything was built: the colour deviation inside every cluster of eight bodies across all four packs is **zero**. So the shader does not sample a different cell, it outputs a colour — no second fetch, no mip or derivative consequence, and the palette is no longer limited to what Synty painted. A *patterned* garment cell is the observation that would send this back to moving UVs.
  - **`Odyssey/Character`** is hand-written HLSL like the other three shaders here, every property in `UnityPerMaterial` so the SRP Batcher still sees one variant across a colony of materials. Three passes; the shadow pass repeats the forward pass's alpha clip or hair cards cast the shadow of a solid rectangle. **An unused slot is the rectangle `(1,1,0,0)`**, which no UV is inside, so "off" is a value rather than a branch.
  - **The rectangles are discovered, not measured by hand.** `CharacterSwatches` clusters each body's UVs at catalogue-build time — three of the four character FBXs are `isReadable: 0`, so `Mesh.uv` is unavailable at runtime for 43 of the 61 bodies, while in the editor every mesh is readable. What ships is a handful of coordinates per row in `ModuleCatalogue.asset`. **No import setting under `Assets/Synty` is touched**: that state is gitignored and would not reach CI or another clone.
  - **Three faults the whole cast found that five bodies had not.** It took the *first* active skinned renderer as the body, and PolygonGeneric ships hair as its own active child — three bodies came back as a hair slot covering the whole figure; it takes the **largest** active mesh now. **Skin is painted in three different places**, not one: the shared column plus Western Frontier's Native Americans at (0.2746, 0.1537) and the Sci-Fi cyborgs at (0.2591, 0.1075) — one column classified fourteen bodies as having no skin, seven of them bare-chested warriors. And the disjointness rule *noticed* overlaps without fixing them, so the catalogue was written with overlapping rectangles and a label saying so; same-slot rectangles are merged now and a genuine cross-slot collision drops the lesser slot.
  - **Final tally: 55 Full, 5 with no skin showing, 1 with neither skin nor hair — and every one of the 61 recolours its clothing.** The five are a helmeted cop, a masked cyborg ninja, a gowned medic and two aliens, which is correct rather than broken.
  - **A fresh cast every session, for now (owner, 2026-09-16).** `OdysseyBootstrap.randomCastEachSession` defaults **on**, so faces, hair, skin and clothes are rolled each time you press Play. It is there because the scene pins `seed = 1`: "the same world deals the same people" then means the same twelve people every single run, which is right for a saved colony and useless for judging a palette. Switching it off restores the stable cast exactly, and that is what a real saved game will want; `colonistLookSeed` still pins one cast over either, and the seed used is logged so a cast worth keeping can be copied back.
  - **Appearance is derived from the world seed and the pawn id**, so the same world deals the same people on every load — replacing a salt rolled at startup, whose recorded reason (a fixed hash made *"a cast of sixty-one read as a cast of five"*) is preserved because the seed still differs between worlds. `colonistLookSeed` remains as the override. Nothing is saved and nothing is hashed; `WorldSnapshot.Seed` is published for the same reason `GameSpeed` is, and the seed was already an input to the hash.
  - **`ColonistAppearanceBook` is one object both drawers hold**, so "the two drawers deal the same face" is a fact about the object graph rather than a convention two doc comments had to keep. **It fixed a latent bug**: the instanced renderer sized its lottery from every catalogue row while the figure director dropped unusable rows and *compacted the survivors*, so look *i* was not row *i* the moment anything was missing — invisible today because either all four packs are installed or none are, and with three of four every colonist would have changed face on crossing the figure cap.
  - **Judge it with `Odyssey → Presentation → Check the colonist colours`** (`scripts/unity.sh shot Odyssey.EditorTools.ColourCheck.Run`), which shoots a control with no character material at all, the real palette, and three sheets forcing one slot to magenta across the colony — a judgement about *place* rather than about shade. **Open for the owner: the palette is deliberately muted and the parade sheet differs from the control only slightly.** It may be too subtle to get a feel from; `ColonistPalette` is the one-line lever. **Not measured: frame time** — `FrameTimeTests` has not been run on this change.

- **The landscape is never cut away (owner report, 2026-09-17; ADR 0006 amended a third time,
  `06-rendering-and-camera.md` §3b).** The report came with a screenshot: on the low ground under
  the trees *"there appears to be no ground texture or grass"*. Nothing was wrong with the ground —
  **it was not being drawn at all**, and the flat, untextured, un-tufted plane in the picture was
  the sky showing through the hole. **The surface is terraced and the depth budget is not:**
  `surfaceRelief` 2 gives a five-step surface, so the outdoor ground of a wooded board spans five
  layers while `SliceSettings.LowestDrawnLayer` stopped `belowDepth` (3) layers under the slice.
  Stand on a high terrace and the low ones fall out of the band. **The trees survived the cut
  because a tree lives in the air cell one layer *above* the ground it grows from**, so the wood was
  inside the band and its ground was not — which is exactly the picture that arrived, and the reason
  the fault read as a texture problem rather than as missing geometry.
  - **Measured on the board that is played** (120 x 120 x 16). Seed 1: the ground runs L8 to L12
    with outcrops standing to L14, the colony opens on L12, and a slice one layer up draws from L10
    — where **6,140 of 14,400 columns have no ground at all**. At the opening layer itself it is
    383 columns, which is small enough to be missed and was. Seeds 2 and 3 open on L11 and lose 726
    and 427 a layer above that. Afterwards: **zero on all three, at both layers.**
  - **The argument is `TintCode.DaylitBase`'s, arriving one step earlier.** That bit exists because
    the depth *shade* was dimming a lower terrace to 0.46 and the meadow came out in three greens:
    the shade is a cue for looking **through** something and there is nothing over an outdoor
    surface for it to describe. The cut needed the identical exemption and did not have it. There
    the fix was that a lower terrace must not be dim; here it is that it must exist.
  - **The floor is measured off the generated board and never moves after.**
    `WorldRenderModel.LowestOutdoorLayer` walks every column from the sky down to the first solid
    cell or floor slab in it and keeps the lowest answer — four or five reads a column, taken once
    before the first frame. It is deliberately **not** maintained by `RefreshDirty`, which is the
    opposite choice to `HighestOccupiedLayer` and for the opposite reason: a roof somebody builds
    has to appear, whereas a pit somebody digs is precisely the "looking through a hole" case the
    depth budget is for. Without that, a shaft sunk to bedrock would force every cavern in the map
    to be drawn while the player stood in a meadow.
  - **It is a floor, not an override.** A landscape that stops inside the budget does not shrink the
    band, `BelowMode.Hide` still hides everything below the slice, underground — where the cap is
    already off downwards — it is not consulted, and `followDepth` off hands every field back
    exactly as before, the floor included.
  - **Everything that reads the band reads the same one** — `ChunkRenderer.Render` and
    `RenderActors`, `PawnFigureDirector.Sync`, `SlicePicker.Band` and
    `SliceCameraRig.LowestSelectableLayer`. Two consequences beyond the ground itself: a **colonist
    walking a low terrace was being culled along with the terrace**, which nobody had reported; and
    a terrace the player can see is now one they can mark for mining, because "selectable" and
    "drawn solid" must not drift apart (§3c).
  - **The cost is the terraces themselves and nothing else.** Buried cells are face-culled before
    they reach a bucket, so the two extra layers a slice at L13 now walks contribute the ground that
    was missing and no instances anywhere else, and the span is bounded by the generator's own
    relief at `surfaceRelief x 2 + 1` layers.
  - **Diagnosed from the screenshot's pixels rather than from the code.** Three readings of the
    rendering path produced three plausible and wrong culprits — an untextured fallback material, a
    desaturated surround, sand cover. What settled it was measuring the image: the plane was the
    *same* colour near and far (171,129,100 at both ends of the depth range) where exp2 fog would
    have graded it, and it carried no outline, no tufts and no shadows. A surface that does not fog
    is not a surface. `docs/lessons.md` carries the method note.
  - Guard tests: **`LandscapeBandTests`** generates the real wooded board on three seeds, mirrors it
    exactly as the bootstrap does, puts the slice where the composition root puts it and measures —
    every case against a control run on the band as it was, because a passing measurement means
    nothing until the same measurement is seen to fail. Six new `SliceSettingsTests` and three
    `WorldRenderModelTests` pin the arithmetic.
  - **Verified:** fast tier 428 Sim and 106 Hud; the Presentation and Presentation-test assemblies
    compiled headlessly against `Library/ScriptAssemblies` (the owner's editor held the project);
    the slice arithmetic and the three-seed board measurement were *run* outside the player against
    the built DLL. **Not verified:** `scripts/unity.sh test editmode`, PlayMode, frame time, and
    whether the recovered terraces look right.

- **A feature can now say something about a pawn without widening the contract everybody reads
  (OQ-45, 2026-09-17; ADR 0004 amended for the first time).** `PawnView` is a struct in the
  assembly both sides reference, and every feature so far has had to edit it to add itself: felling
  added `Working` and `WorkCell`, hauling added `Gesture` and its serial, mining widened it again,
  and hauling water, sleeping in a bed and being injured each would in turn. That is five features
  into a prototype. The seam is now a sparse `PawnAspect` row — `(PawnId, AspectKey, int)` —
  written with `SnapshotWriter.AddPawnAspect` and read with `WorldSnapshot.TryGetPawnAspect`.
  - **The cheapest part of the design was noticing that no new mechanism was needed.** The row
    asked for something "mirroring `ISnapshotContributor`", and the closest mirror turned out to be
    *reusing it*: a feature registers through `AddSnapshotContributor` exactly as it always could,
    and the seam that already existed for the frame now reaches pawns. A second registration path
    would have been a second thing to keep in step, for no gain. Sixty lines of contract, no
    subsystem.
  - **The key had to be a hashed name, and an enum would have recreated the problem.** An enum of
    aspect kinds would live in `Sim.Contracts` — the file the whole row exists to stop people
    editing — so the mechanism would have been the chokepoint it was built to open. A key minted
    from a symbolic name is the only form that lets a feature declare its own vocabulary against no
    shared file, which is the reasoning that already made interface icons symbolic keys rather than
    filenames. **Sixty-four bits, not thirty-two:** a collision here is not a crash, it is one
    feature silently reading another's number in a value the player is looking at, and at 32 bits
    that is roughly one chance in two hundred thousand over a few hundred keys. Detecting it
    instead would have needed a central registry, which is the shared file again.
  - **Every positive has a control, and the controls were run rather than asserted.** With
    `AddPawnAspect` stubbed to a no-op, **8 Sim and 3 Hud tests fail and every control stays
    green** — including `PublishingAnAspectDoesNotEnterTheStateHash`, whose own "the control must
    actually differ in what it published" guard is what makes it fail. Separately, perturbing the
    hash arithmetic fails **exactly one test**, the golden `TheKeyForANameIsFixedForEver`, and
    nothing else: every other test mints its keys through the same function and therefore cannot
    tell. That is the argument for the golden existing at all — the name-to-key mapping is a
    contract between two assemblies and across two runtimes, and only a literal can hold it.
  - **The `Tests/Hud` half is the architectural point, not a convenience.** That assembly cannot
    reference `Odyssey.Sim` — ADR 0004's strongest structural guard — so it cannot name the feature
    that published a value, cannot share an enum with it, and has nothing but the string. Four
    tests there prove the string is enough, including the case the contract was shaped around: a
    panel open on a pawn that dies reads nothing rather than stale numbers.
  - **Aspects are a report, not state**, exactly as `PawnGesture` is. The fixture feature's numbers
    enter the state hash and the save; its published report does not, proved by a control that
    stops it publishing and leaves the hash identical to a world that never published at all.
  - **What the amendment admits.** ADR 0004 specified that "expensive detail is
    subscription-scoped"; **no subscription mechanism was ever built**, and aspects are the
    unscoped form — every installed feature publishes for every pawn it tracks, every frame. At
    tens of rows against a 62,500-cell slice already in the same buffer that does not register, but
    the honest statement is that the cheap thing was built and the specified thing deferred. Flip
    condition F1 names the order of retreat and the 0.8 ms budget that triggers it.
  - **A stale plan misled this session before it started.** `docs/plans/vertical-slice.md` listed
    OQ-15/OQ-16 as "written and open" and as step 1 of the seam order; both had landed a day
    earlier, as had OQ-44, and the first answer given to the owner about what to do next was wrong
    because of it. Both plan files were audited against the code in the same change. **The audit
    found something neither the plan nor the queue said:** `PawnContent.Core()` still exists and
    the bootstrap and `ColonyWorld` still build from it, so the XML is a *checked mirror and not
    the source* — mining still added 59 lines to `PawnContent.cs`. That, and not this row, is now
    the cheapest remaining seam work.
  - **Verified:** fast tier **444 Sim + 110 Hud** (from 428 + 106), both content gates green, and
    both negative controls run and restored. A Unity editor held the main checkout throughout, so
    no batch run could be attempted locally; **both CI tiers ran it instead and passed** (PR #64),
    with `PawnViewContributorTests` and `PawnAspectReadTests` both in the EditMode results. That
    incidentally completes the golden's purpose: `TheKeyForANameIsFixedForEver` asserts the same
    literal under CoreCLR in the fast tier and Mono in the Unity tier, so the name-to-key mapping
    is now proved to agree across both runtimes rather than merely intended to.
- **The pawn content is written once now (2026-09-17, closing the half of OQ-15 left open).**
  `PawnContent.Core()` is deleted: 163 lines of content tables hand-written in C#, plus the private
  `Skill()` helper that built the experience and decay ladders. The XML at
  `Assets/Odyssey/Defs/Core/Pawns` is the only copy. Every call site — 10 editor tools, 6 test
  files, `ColonyWorld` and `OdysseyBootstrap`, about thirty-five in all — now goes through
  `ContentPack.Pawns()`.
  - **The blocker was never the switch, it was the path.** OQ-15 shipped `FromDefs` working and
    proved equal to the oracle, then stopped, because `FromDefs` needs a `DefDatabase`, a
    `DefDatabase` needs a directory, and "a world that needs a path on disk cannot be built from a
    unit test fixture". The answer was already sitting in the test assembly: `RepoPaths` walks up
    from the running assembly to the directory holding both `Assets` and `ProjectSettings`, which
    is true of the fast tier's `bin/Debug/net8.0`, of Unity's `Library/ScriptAssemblies` and of
    the editor while playing. Generalising that into `ContentPack.FindRoot` made every caller work
    with no path at all.
  - **A built player would fail this, and that is deliberate rather than overlooked.** Nothing in
    CI or `scripts/` builds one — checked — so shipping the pack is *not solved* here instead of
    solved speculatively. The throw names the answer `d-07-data-pipeline.md` already gave (copy the
    pack to `StreamingAssets`, call `ContentPack.UseRoot`), and `UseRoot` exists and is tested so
    the seam is not discovered broken on the day somebody first needs it. **Worth noting against
    d-07:** that research said the pack should live in `Content/` at the repository root, *outside*
    `Assets/`; OQ-15 put it inside. Nothing here depends on which is right, but the two disagree
    and nobody has recorded why.
  - **The parse is cached and the record is not, and that distinction is load-bearing.**
    `MineJobTests` writes `ctx.Content.StoneChanceOneIn = 4` on the record it is handed. A shared
    record would have leaked that into every test that ran afterwards, and it would have looked
    like a flaky test rather than a cache. So `ContentPack.Core` holds the parsed database and
    `Pawns()` rebuilds the cheap record per call — exactly `Core()`'s old semantics, which is why
    no call site needed anything but a rename. What *is* shared is the Defs themselves, since the
    record's arrays point into the database; the two fingerprint controls perturb Defs and
    therefore load a pack of their own rather than using the cached one.
  - **A per-frame allocation fell out of it.** `OdysseyBootstrap.MovePerTick` was
    `PawnContent.Core().Movement.movePerTick` — it built the *entire* content table, every Def,
    array and list, and took one integer off it. It is read three times a frame, twice by
    `SelectionPresenter` and once by the render path. It now reads off the colony that already
    holds the content. Nobody was looking for this; it turned up because switching a call site
    forces you to read it.
  - **The oracle had to go, and the file it lived in had already said so.** `PawnContentDefTests`
    argued *against* a golden — "a baked hash of the XML would prove only that the XML has not
    changed, which is not the question" — because while the simulation built from `Core()`, the
    code was the specification every soak hash and tuning decision had been measured against. The
    same comment set the expiry: the oracle stands *"until the bootstrap loads content at
    startup"*. It does now, there is no second copy to disagree with, and keeping one would have
    meant writing every new item twice for ever — which is the cost the row existed to remove. So
    the question becomes the one a golden answers, and `DefComparison.Fingerprint` folds the loaded
    record through the same reflective walk `Differences` uses into one literal. A content change
    costs one deliberate line; an accidental one fails, and `git diff Assets/Odyssey/Defs/Core` is
    the answer to "what moved" because that is the only way content *can* move.
  - **The controls are the reason to believe any of it.** With the walk stubbed to reach nothing,
    **all four fingerprint tests fail** — which is precisely the failure mode the original file
    warned a reflective comparer has, now caught rather than described. Run and restored.
  - **Content equivalence is proved by the history, not by an assertion.** The oracle test passed
    on every commit up to the one that deleted it, and `FromDefs` was not touched, so the content
    the simulation loads today is the content it was measured on. There was no moment at which
    both sides could have moved together.
  - **Deliberately left: the world half.** `CoreContent.Terrain` and `NaturalContent.Terrain` are
    still in-code tables duplicated by `Defs/Core/World/*.xml`, and `WorldContentDefTests` still
    uses them as its oracle. ~~They are read at runtime almost nowhere — the oracle and one count
    assertion~~ — **wrong, and corrected the same day by OQ-49 below.** That claim came from
    grepping the `Terrain` *list property* and not `TerrainAt()`, which is the accessor everything
    actually calls: mining prices `workToClear` through it on every work tick, `DepthPasses` reads
    `salvageWeight` per cell while generating, and the renderer resolves terrain names through it.
    The tables were load-bearing all along. Written down because the same mistake is in the
    `OQ-48` PR body and in the plan docs of that commit: **a grep for a symbol is not a search for
    its callers**, and the one I ran happened to match only documentation.
  - **Verified:** fast tier **432 Sim + 106 Hud** (from 428 + 106). **Not verified locally:**
    `scripts/unity.sh test editmode` and PlayMode — a Unity editor held the main checkout
    throughout, which matters more than usual here because **the ten editor tools are compiled by
    nothing else**, so CI's Unity tier is the first thing to compile a third of this change.

- **The roster card says what a colonist is doing with a picture (owner, 2026-09-17).** Three of
  the owner's 32 px drawings went in as `ui.status.felling`, `ui.status.mining` and
  `ui.status.building`, and an `IconBadge` leads the activity line on every card. **The slot is
  never empty**: hauling, eating, sleeping and idle have no art and draw the outlined square,
  because hiding the badge would move the word sideways every time a colonist changed job. The
  hammer has no job to be drawn for yet — there is no build driver in `JobIndex` — and costs
  nothing sitting there. `TheActivityLineLeadsWithAnIconAndStillHoldsItsWord` measures the word the
  text engine would really draw against the room the row gave it, because a word that no longer
  fits does not report itself: UI Toolkit lays it past the card's edge, where the rounded frame
  hides the tail. Measured at 1080p: "Mining" 31 px in 34, "Felling" 30 in 33, against 93 available.

- **The bars came off the card and it was sized by measurement (owner, 2026-09-17).** *"Remove the
  bars from the roster icons and then we can shorten and tighten them so we can carry many more —
  accommodate the longest name possible."* A card is two rows now, identity and activity, at
  **106 x 63** against 132 x 86: **1.24x as many colonists on the same bar** (4→5 at 720p, 8→10 at
  1080p, 13→16 at 1440p). **The width is measured, not chosen** —
  `TheCardIsWideEnoughForItsRowsAndNoWider` asks the text engine what the longest name the pool can
  deal and the longest `ui.status` word really draw: `Wrenn 10` at 50 px makes a 100 px identity row
  and `Sleeping` at 39 px a 78 px activity row, against a 106 px card. It sweeps **twelve cycles of
  the eight-name pool**, because the wide names are not the pool's own but the ones carrying a
  two-digit cycle suffix, and a colony of fifty reaches them. Food, rest and mood are on the inspect
  pane for whoever is selected and in the alerts panel for the colony — both built after the card
  was, and both answer properly what three 3 px bars answered at a glance.

- **Both bars docked, and the strip may wrap to two rows (owner, 2026-09-17).** `StripTop` and
  `BarBottom` are 0 where both were `Edge`. **A second row has to be earned:** two full rows are
  8.1% of a 1280 x 720 screen and took the resting HUD to **21.7%**, over the approved 18% ceiling,
  so `StripHeightShare` (0.14) caps the strip against the viewport — one row at 720p, two at 1080p
  and above, and back to one at a raised interface scale. The clamp matters more than the growth:
  the strip is the one region with no ceiling of its own. **The wrap happens at the strip's own
  room, not the screen's**, so the shell writes the strip's insets on every resize; without that
  the second row runs under the clock.

- **The Skills tab is real, and sheet 06 is in the repository (owner, 2026-09-17; design
  `docs/design/15-skills.md`).** The owner supplied `SurvivalSkills-Sheet.png` — 56 framed 32 px
  tiles, 8 x 7 — and it **is** the sheet the icon map had been calling 06 since that map was
  written blind. All 56 cells are catalogued in `15-skills.md` §2.
  - **The finding is that the sheet is a skill list for a game two or three milestones out.** Three
    cells draw something the simulation has; ten draw a skill the design has named and nothing
    implements; the other **forty-three argue for systems nobody has designed** — hunting, trapping
    and fishing, butchery and tanning, textiles, a refining chain, demolition, foraging, warmth and
    light as needs, injury and death, research and blueprints. So the rows had to be chosen
    deliberately rather than taken wholesale, or the interface would promise weaving to a player
    who can only chop and dig.
  - **Four decisions by interview** (§6): thirteen rows with the simulated ones live and the rest
    visibly unavailable; the thirteen in `icon-keys.csv` are canon, so hauling and cutting are work
    types and not skills; a row is an icon, a name, a level and a passion mark; and two icon idioms
    — framed painted tiles for skills against bare marks for status.
  - **It was built on its own contract and merged onto the aspect mechanism.** The first version
    added `SkillView`, `SkillHandle`, `Skills`, `SkillCount` and `AddSkill` to `Sim.Contracts` — a
    sparse counted list in `OrderView`'s shape, reasoned out independently and, as it turned out,
    the same idea as OQ-45's pawn aspects, which landed on `main` the same night. **All of it was
    deleted on merging.** Skills now publish as `odyssey.pawn.skill.<name>.level` / `.passion` /
    `.experience`, `SkillAspects` mints the keys in `Odyssey.Sim`, `SkillCatalogue` mints the same
    keys from the same literals in `Odyssey.Hud`, and **nothing in the shared assembly knows that
    skills exist** — which is exactly what that mechanism was added for. Two tests spell the
    literals out rather than asking either side what it is called, because a test that asks the
    thing under test agrees with itself whatever it has been renamed to.
  - **The pane has working tabs at last.** They were chips with no click and one body.
    `InspectModel.ActiveTab` is **model state**, so the choice survives the refresh that follows
    every click; in the view it would undo itself fifteen times a second.
  - **Twelve icons are cut by the pipeline, and that is the first time its output has been
    loadable.** `tools/icons/icons.py` wrote to `Assets/Art/Ui/icons/` while `IconArt` loads from
    `Assets/Art/Ui/Resources/odyssey/icons/` — so anything ever exported would have drawn the
    placeholder square, **silently**, because a key with no art is not an error and is not logged.
  - **Where the simulation's three went.** Mining is `ui.skill.mining`. **Cutting feeds
    `ui.skill.growing`** — felling is plant work, the canon work type `ui.work.cutting` is "cut
    plants and clear growth", and the row's tooltip says so precisely so it can be objected to.
    **`Skill_Hauling` should be deleted**: under the canon list hauling is a work type and not a
    skill, so its experience has nowhere to go. That is a Defs, `SkillIndex`, save-format and hash
    change and is **not done**.
  - **Not judged by eye, and one thing measured against it: a framed tile does not read at 17 px.**
    The frame is a large share of the tile and what is left of the painting is a few pixels of
    colour. `Logs/skill-icons.png`, 64 px over 17 px.

- **The command bar runs edge to edge, and its menus became popovers (owner, 2026-09-17).** *"Make
  the bottom bar the full width of the screen. When I click on build I expect the menu to appear
  directly above the build button, as I do all the menus… All windows can be escaped but also
  should have an X in the top right… There should be less transparency with these menus. Make this
  a consistent rule."*
  - The bar was a centred pill as wide as its items; it is now edge to edge with **no corner
    rounded and only its top hairline drawn** (`HudTheme.BarRadius` 6 → 0, `HudLayout.BarFrame` one
    border rather than two), and the overflow arithmetic measures against the bar's own padding
    rather than the screen margin — which puts **two more items on the bar** before anything goes
    into Menu.
  - **The rule is written once, in one helper.** A **window** is a panel you deliberately opened
    and are looking at: `HudTheme.PopoverFill` (0.96 against a board panel's 0.86) and a close X,
    which is the inspect pane's own control lifted out rather than reinvented. **The fill is not
    taste** — the two scrims carry a board panel's contrast so it can stay light enough to see
    terrain through, but the board behind a window is not being read. A **popover** is a window
    raised from a bar button: anchored to the **left edge** of that button, flush on the bar with
    **no gap**, bottom corners squared. Left-aligned rather than centred, because centring puts a
    wide popover off the screen for the leftmost button and has to clamp anyway.
  - **Only one popover at a time**, or two raised from the same bar would overlap each other over
    the buttons that raised them and the player could not tell which the next Escape belongs to.
    `EscapeAction.CloseMenu` is new: **the Menu popover was the one window with no way out but the
    button that opened it.**
  - Measured under the player loop at 1080p: the Build popover lands at x=5 against its button at
    5 and the Menu popover at 1191 against 1191, both with their bottom edge exactly on the bar's
    top at 1031.

- **The rest of the HUD docked too, and the Build palette stopped eating itself (owner,
  2026-09-17).** `HudLayout.Edge` is 0 where it was 20: the clock sat twenty pixels below a strip
  beside it that started at zero and the two read as misaligned *because they were*. Stores, the
  clock column, the depth rail and the inspect pane sit on their edges now, with the corners that
  lie on a screen edge squared. **The breathing room moved inside rather than going away** — a
  panel's `Pad` is still twelve on all four sides, so no text is nearer the edge than it was; what
  is gone is the strip of world between a border and the edge. It is also forty pixels of strip
  room. **For the Build palette the cap is the fix and the width is the cheap half:** the category
  list was `flex-grow: 1`, so it took every pixel the panel had and the tools got the remainder —
  the group the player is reaching into shrank as the group above it grew. `BuildCatRows` is 2 and
  anything past two rows scrolls, which is what the scroll view was put there for and never
  reached. A chip has an explicit 30 px height, because **a count of rows is meaningless if a row's
  height is content-driven**. Measured at 1080p: the palette is **176 px tall with categories at 68
  and tools at 44**, against a panel that ran to its 320 px ceiling with five rows of categories.
    (Both tiers went green on PR #65, and the Unity log was checked for `error CS`, for
    `Odyssey.Editor` and for the names of the changed tools rather than trusted on the tick.)

- **The world tables follow, and on the way it turned out nothing was guarding them (OQ-49,
  2026-09-17).** Terrain now loads from `Defs/Core/World/Terrain.xml` and the in-code copies are
  deleted — `CoreContent.BuildTerrain()` and `NaturalContent.BuildTerrain()`, twenty-one terrains
  written out twice. `WorldContent.Table` is the one table, lazily loaded from `ContentPack.Core`
  and cached.
  - **The headline is a measurement, not the migration.** Once the generators read the XML, the
    old field-for-field oracle was comparing the XML *against itself* and passed without asking
    anything. That was not reasoned about, it was checked: **rock's `workToClear` edited from 700
    to 701 — a number the mining job prices every work tick from — left all 448 tests green.**
    Nothing at all pinned the world's content values; a typo in `Terrain.xml` would have shipped
    in silence. A `DefComparison.Fingerprint` over the table now guards it, and the identical edit
    fails two tests. Both halves of that were run, before and after.
  - **The correction that made this worth doing at all.** Yesterday's entry, the OQ-48 PR body and
    the plan docs all said the in-code world tables were "read at runtime almost nowhere". Wrong:
    that came from grepping the `Terrain` *list property* rather than `TerrainAt()`, the accessor
    everything actually calls — mining, `DepthPasses`, the renderer, the ambience probe. **A grep
    for a symbol is not a search for its callers**, and the one that was run happened to match
    only documentation. The claim is struck through above and corrected in both plan docs.
  - **Lazy, not a static field initialiser, and for a specific reason.** Filling the table loads
    the pack, and loading the pack calls `WorldContent.Register` — on this very class. A field
    initialiser would run that inside the type's own static constructor; a property runs it after
    the type is initialised, where a plain static call back in is harmless. Cached because it is a
    hot path: `DepthPasses` asks for `salvageWeight` per cell while generating.
  - **A branch disappeared.** `NaturalContent.TerrainAt` used to dispatch on
    `terrain < FirstTerrain` between two hand-written tables. With one table the core terrains are
    simply its first ten rows — which is what `WorldContent.TerrainOrder` always said they were —
    so the branch, and the doc-comment rule warning never to call `CoreContent.TerrainAt` on a
    natural index, both go.
  - **What did not change is the numbering.** A terrain index is in every cell of every save and
    every hash. `TerrainOrder` still decides it, it is still not the loader's own by-defName sort,
    and the test that checks all twenty-one constants against it is untouched — joined now by one
    asserting the *loaded* table came back in that order.
  - **Verified:** fast tier **450 Sim + 110 Hud**. Content gates green. **Not verified locally:**
    the Unity tier, for the same reason as yesterday.

- **The golden-master gate, and the hole it found before it was committed (OQ-05, 2026-09-17).**
  Three worlds are now pinned to committed hashes: the barren meadow at 5,000 ticks on every save,
  and the wooded 120 x 120 x 16 board and the ruined city at 10,000 ticks on the Long tier.
  `ODYSSEY_REGOLDEN=1` prints replacements instead of asserting.
  - **Why this was worth doing at all.** Every other hash test here compares a run against another
    run of the same build. They prove repeatability and are *blind to change* — a build that broke
    felling this morning still agrees with itself perfectly. A committed number is the only thing
    in the suite that compares against the past, and therefore the only thing that can notice a
    change nobody intended.
  - **Two hashes per case, because one number cannot be read.** `Generated` is taken before the
    first tick and covers worldgen; `Simulated` after N ticks and covers everything. Both moved
    means the generator changed and the simulation inherited it; only the second means a system
    changed. The failure messages say which, and both were seen to fire correctly: perturbing a
    need's drain rate reported *"the board generated identically… so a simulation system changed"*,
    and perturbing terrain reported *"the generated world differs before a single tick ran"*.
  - **The hole. `CellGrid` is not in the state hash, and never has been.** The control that found
    it should have failed and did not: flipping `<impassable>` on deep water changes nine cells'
    flags on the played board and moved no hash at all. `CellGrid` does not implement
    `IStateHashable` and is never registered, so `SimWorld.ComputeStateHash()` covers the seed, the
    tick, the grid *size*, the designations, the jobs and the pawns — **not the terrain, the
    floors, the edifices or the flags**. `CellGrid.ContributeTo` exists, but its only callers are
    the two map generators' own worldgen check and one test.
    - **It reads as an oversight rather than a decision.** `GridSaveSection`'s doc comment argues
      about what to exclude *from* `CellGrid.ContributeTo` on the grounds that "saving it would put
      bytes in the file that the state hash does not agree are state" — written by someone who
      believed the grid was hashed.
    - **What it means today:** mining a cell, felling a tree and a collapse all edit the grid and
      none of them move the state hash; and `WorldRoundTripTests` proves a save round-trips
      "exactly" by comparing hashes that cannot see the grid.
  - **Not fixed here, and the reason is a measurement.** On the played board `ComputeStateHash()`
    costs **0.003 ms** and hashing the grid costs **10.3 ms** — about three thousand times more.
    Folding it in unconditionally would make the per-tick sink `HashTraceTests` uses take minutes
    over a day of ticks. So the golden folds its own composite and the canonical hash is untouched;
    the real fix wants an incremental hash over the chunk dirty-tracking `GridSaveSection` already
    maintains, which is `OQ-50` and probably an ADR note, since the state hash is what ADR 0005's
    "determinism before threads" rests on.
  - **With the composite, the same edit fails — and fails precisely.** Only the played-board case
    breaks, because a terrain histogram of all three boards shows the meadow and the city contain
    no deep water at all and the played board contains exactly nine cells of it. Measuring the
    boards rather than assuming their contents is what made that reading possible.
  - **Verified:** fast tier **455 Sim + 117 Hud**, Long tier **15**. Every control run and
    restored. **Not verified locally:** the Unity tier, which is where the cross-runtime half of
    this gate actually gets tested — one committed number that satisfies both CoreCLR and Mono.
    **It passed there:** all five golden tests ran under Mono, including both 10,000-tick Long
    cases, against numbers baked under CoreCLR. Cross-runtime determinism stops being a
    measurement someone took once and becomes a standing test.

- **The world is in the state hash (OQ-50, 2026-09-17; ADR 0005 amended).** `CellGrid` now
  implements `IStateHashable` and `ColonyComposition` registers it through a new third list,
  `SimWorldBuilder.AddHashable`, for state that belongs in the hash but neither ticks nor is a
  system. `CellGrid.ContributeTo` had existed all along; nothing but the generators' own worldgen
  check had ever called it.
  - **The approach is the opposite of what the row predicted, and the reason is the code rather
    than taste.** The row assumed an incremental hash maintained over the save's chunk
    dirty-tracking, because 10 ms a call sounded unaffordable. Then the cell arrays turned out to
    be **public and written directly from dozens of places** — every generator pass, the support
    solver, mining, felling. A maintained hash would be silently wrong the first time anybody
    assigned to `Terrain[i]` without telling it, and **a hash that wrongly says two different
    worlds are the same is a worse failure than the one being fixed.** Recomputation cannot drift.
    The public arrays are what make the cheap option unsafe and the expensive one correct.
  - **The cost is real and lands on the diagnostic.** On a 60 x 60 x 16 colony a plain tick is
    3.34 µs; a *traced* tick — one asking for the hash every tick — is now **2,803 µs**. Nothing in
    an ordinary run asks for the hash, so the game and the tests are untouched; what got slower is
    the hash trace, the tool for binary-searching the first tick two runs disagree on. A full day
    of a real colony is minutes now rather than seconds, so trace a window. The saving grace is
    that the project had already built the instrument that measures this —
    `HashTraceTests.TheCostOfTracingIsMeasuredRatherThanAssumed` prints the figure on every run, so
    the trade is visible rather than folklore. Its colony arm dropped from 2,000 ticks to 300,
    which measures the same per-tick number and gives the fast tier back five seconds.
  - **`WorldRoundTripTests` passed immediately**, which is the quietly good news: the save had been
    round-tripping the grid correctly all along, and simply had nothing checking it. The gap was in
    the verification, not in the save.
  - **`StateHashCoverageTests` names each field** — mining a cell away, a floor, an edifice, a cell
    flag — rather than asserting the vague "the hash changes when the world changes". It also
    asserts `Support` stays *out*: it is derived and rebuilt on load, so hashing it would make
    every load look like a desync. That is the half a careless "hash everything" would break.
  - **Control run and restored:** with the registration removed, exactly the four field tests and
    the meadow golden fail, while `SupportStaysOutOfTheHash` and the ask-twice control still pass.
  - **The golden lost its bespoke composite.** OQ-05 folded the grid in by hand precisely because
    the canonical hash could not see it; one day later it pins the same number as everything else.
  - **Verified:** fast tier **461 Sim + 117 Hud** in 11 s — the same wall-clock as before the
    change, after the trace measurement was trimmed — and Long tier **15**.

- **The settings panel grows up, 2026-09-17.** Keybindings, the audio faders, a camera-speed
  ladder, a developer-overlay toggle and a two-click exit landed in B17, on `feat/front-ui`
  ahead of a PR.

  - **`HotkeyDirector` exists at last.** Design 09 §3 row 23 had reserved it since the input
    spec was written; every key in the game was a hardcoded `Keyboard.current.xKey` poll in
    four files. It is Unity-free in `Odyssey.Hud`, holds bindings as **actions with two
    slots** (a primary and an alternate — the honest shape for a game that pans on WASD *or*
    arrows and steps the slice on R/F *or* PgUp/PgDn), refuses a key another action owns
    rather than silently swapping it (§6's rule), and persists as key-name strings under
    `odyssey.ui.keys.*` through `ISettingsStore`, which grew `ReadString/WriteString`.
    **One context, for now**: no key in the game means two things today, so the first cut is
    a single global map; contexts arrive with `InputRouter` the day a key earns a second
    meaning. Escape and Shift stay fixed and unbindable — the unwind rule and the fast
    modifier — and the function keys stay out because the command bar has promised them to
    panels.
  - **`HotkeyClashTests` changed shape, not job.** It still greps the Presentation assembly,
    but what it asserts now is that *no key is read by name at all*: the only whitelisted
    reads are `escapeKey` (the unwind rule), `allKeys` (the rebind capture, which names no
    key) and the two shift modifiers. The reserved set a command cap is checked against is
    read out of the binding map's own defaults — the hand-written list this test replaced in
    its last life was wrong in exactly the way its own doc comment describes.
  - **While a slot is listening, every key press belongs to the rebind.** The three pollers
    (rig, designate, bar) sit the frame out when `Listening != null`, so offering M to a
    slot cannot arm the mine tool on the way past. Escape cancels the wait *before* the
    unwind order runs — the rule lives in `HotkeyDirector.ConsumeEscape`, decided in the
    fast tier.
  - **The faders found the panel that was promised them.** `AudioSettingsStore` has held
    five dB faders, persisted and applied at boot, since the sound work landed — its own doc
    said they "belong in that panel beside them when B17 grows an audio section". The
    section is a dB rung ladder per bus (Mute, −36, −24, −16, −10, −5, 0), the Hud-side
    `SettingsBus` mirroring `SoundBus` across the ADR 0003 seam, and the presenter writes
    through the existing store — no second copy of a volume anywhere. **Rungs, not sliders,
    everywhere**, for the reason the interface scale set: honest answers, no fractional
    states, and the ladder idiom the panel already owns.
  - **Camera speed and the developer readout came along because they were free.** The speed
    is a three-rung multiplier (0.6×, 1×, 1.5×) on the rig's tuned pan/zoom — translation
    only, like shift, leaving orbit's mouse-delta mapping alone. The developer overlay is
    now a persisted toggle seeded from the overlay director: the backquote key still flips
    it, and the preference follows, because the key never wrote anything down and the row
    does.
  - **Exit is two clicks, pinned under the tabs.** Nothing is saved, so one click in a
    panel a player reaches across for the close button would be a trap; the first click arms
    the row ("Quit? Click again"), the second raises `ExitRequested`, which the presenter
    answers with `Application.Quit()` — stop-play in the editor, or a quit button that
    silently does nothing teaches the player not to trust it. Closing the panel stands the
    row down. It lives in B17 "for now"; B18's game menu is its documented home when that
    exists.
  - **One pre-existing red was retired on the way in** (fix-before-features rule): the
    building-work tripwire in `WorkSwingTests` fired — `JobHandle.Build` now maps to the
    hammer style — and the test's own comment says what to do when it does. Both the row and
    the tripwire are gone.
  - **Verified:** fast tier **158 Hud** (472 Sim), Unity EditMode **1064**, PlayMode **27**,
    both content gates `--check`. **Not verified:** a human eye — the Keys tab is the tallest
    thing the panel has held (19 rows in five groups); at 150 per cent interface scale on a
    1080p screen it is within a few pixels of the screen height, and if it clips, the window
    wants a max-height and a scroll, which no panel here has yet.

- **The region graph is measured, and it corrects the file that predicted it (OQ-18, 2026-09-17).**
  `docs/research/d-04-pathfinding.md` reasoned about forty layers from the map dimensions and said
  so in its own Confidence section; its "Could not be determined" list named the gap — the real
  distribution of live chunks at our dimensions, "needed to turn the region-count estimate from an
  upper bound into a budget". Both generators and the graph exist now, so
  `NavGraphStatisticsTests` builds each map at 250 × 250 × 40 and walks the graph.
  - **The budget was right and its stated reason was wrong**, which is the kind of result only a
    measurement produces. "Low tens of thousands, not the theoretical 50 k-plus" holds: 24,141
    regions on the wilderness, 23,240 on the city. But d-04 credited that to uniformity —
    "most chunks in a ruined-city column are all-air or all-solid; those allocate no regions at
    all" — and **all-solid is precisely the case that allocates here.** `RegionKind.Impassable`
    exists on purpose, so rooms and atmosphere have a substrate; underground rock therefore fills
    every block with exactly one region. The city is 35.0% live blocks and the wilderness
    **92.1%**, a factor of eight between two maps that are both correct.
  - **What actually bounds the count is the block.** A region is a connected part of one 10 × 10
    block of one layer, so the largest region on either map is exactly 100 cells and the floor is
    one region per live block. The ceiling is blocks × layers, 25,000, and both maps land just
    under it from below. The estimate would have been right whatever the generators did.
  - **The number that matters to the top technical risk is the walkable fraction, and it is
    small**: 1,649 of 24,141 regions on the wilderness (6.8%) and 9,006 of 23,240 on the city
    (38.8%). Impassable regions carry no links and are excluded from the district flood, so the
    hierarchical search d-04 recommends traverses far less than the region totals imply. Worth
    having before attacking the 65% of tick that is A-star rather than after.
  - **Three things nobody had asked for and the walk handed over anyway.** Falls are the city's
    dominant edge — 11,408 of 20,761 links, 55%, which puts a number behind "collapsed floors are
    the signature feature". Traverse mode changes connectivity only where there is architecture:
    colonists see 3,291 districts on the city and haulers, barred from ladders, see 3,628 — 337
    places reachable only by ladder — while all four modes see 37 on the wilderness. And the
    wilderness is **not one connected place**: 37 districts on a map with no buildings, which is
    the sealed caverns and ledges the generator makes.
  - **The assertion that failed first is the reason the test is worth its runtime.** It was written
    to encode d-04's uniformity claim, and it failed on the wilderness at 23,031 of 25,000 live
    blocks. The claim, not the code, was wrong. The uniformity figure is now recorded and not
    asserted — it is a generator property that differs eightfold between two correct maps, and an
    assertion on it would fail the day cavern density is tuned. What replaced it are the two
    structural guarantees the layered design rests on, checked over all 2.5 million cells of each
    board: **no region spans two layers**, and no region outgrows its block.
  - **Nothing in `NavGraph` was widened for this.** There is no public link enumerator, so links
    are counted by walking each live region's adjacency and de-duplicating by id. Adding an
    accessor for a test's convenience is the habit the seam work exists to break.
  - **Verified:** fast tier **478 Sim + 158 Hud**, Long tier **17** in 8 s — the two new cases add
    about 0.9 s. Full rebuild at the scale target: 168 ms wilderness, 124 ms city, the all-dirty
    worst case and not a per-tick cost.
  - **Found on the way:** `CLAUDE.md` on `main` recorded **461 Sim + 117 Hud** while `main` in fact
    ran 478 + 158. That line is edited by every branch that adds a test and is therefore wrong
    most of the time; PR #69 carries its own different number for the same line. It is a shared
    counter with no owner, and it will keep going stale until it is either generated or dropped.

- **The real tick is measured, and the estimate it replaces was conservative by half (OQ-19,
  2026-09-17; ADR 0005 addendum).** The ADR's margin table — "the most important number in the
  benchmark", by its own description — came from the D1 spike, a program that *mirrored* the tick
  rather than being it, and its post-hierarchical figure of 0.88 ms was arithmetic: the 2.4×
  pathfinder win applied by hand to the spike's phase 3. The ADR asked for the re-run in as many
  words. `TickBenchmarkTests` does it on the real `SimWorld.Tick`.
  - **The first run measured the wrong thing, and the number was the clue.** Fifty pawns on a
    250 × 250 × 40 board came out at **0.021 ms a tick** — forty times cheaper than the estimate.
    Not a triumph: a colony left to itself barely paths at all, because wandering picks a target
    a few cells away on its own layer and then walks a route it already has, while the D1 workload
    replans constantly. Publishing that as the replacement figure would have "confirmed" a 40×
    improvement that was really a change of workload.
  - **So there are two arms and they bracket the answer.** The second lays D1's request rate over
    the same world — one long-range path per tick, ±40 cells and ±3 layers, enqueued by a
    registered thing and served by the same `PathService` the pawns use, inside `MovementSystem`,
    inside the tick. **0.438 ms a tick, p95 1.253, Pawns 97.1% of it**, and the queue ends empty
    so every request really was served.
  - **The verdict in the margin table reverses.** Three ticks at the replan rate cost 1.31 ms;
    discounted 4× for the target laptop, 5.26 ms, leaving **11.3 ms of a 16.6 ms frame** for
    rendering. The row that read "discounted 4× — none, over budget" is no longer true. The frame
    budget was a pathfinding problem, the pathfinding was fixed, and this is the measurement
    saying so instead of the estimate.
  - **Three numbers, so nobody has to guess which one is "the" tick**: 0.003 ms for `OneDay` on the
    board the scene actually loads, 0.025 ms for fifty pawns on a board seven times larger, and
    0.438 ms for that board under a stress workload no colony has yet generated.
  - **Allocation is not zero, and the row asked for zero.** The D1 spike recorded a true
    `alloc_bytes_per_tick=0.000`; the real tick grows the heap by **76.7 bytes at rest and 284.6
    under replan pressure**, with no collection of any generation across either window — so those
    growth figures are the allocation figures and not a lower bound. The delta divides to about
    208 bytes per served path request, which *points at* the served path's cell array without
    demonstrating it, because nobody has measured where it comes from. About 17 MB over a day.
    Recorded as a finding and as a row to write, not quietly asserted away.
  - **Per-phase timing needed a seam, and there was already a shape for it.** `SimWorld.PhaseSink`
    is an opt-in diagnostic beside `HashSink`, null in every ordinary run, one branch per phase
    when unattached. It went on `SimWorld` rather than into the benchmark because the phase order
    lives in `Tick()` and `WorldSystemSchedule`'s run methods are internal — a benchmark that timed
    the phases by calling them itself would have been a second copy of the tick order, which is the
    defect U34 had just finished deleting out of the composition root. Two tests hold it honest:
    attaching a sink cannot change the state hash, and detaching one stops it recording.
  - **A name collision caught a real risk.** `Odyssey.Sim.TickPhase` already existed — the three
    phases a *system* may register in. The timing enum needs all seven, including the ones no
    system may join, so it is `TickSegment`, **numbered to match** where the two overlap, with a
    test asserting the values agree and that every registerable phase is a segment something times.
    Two enumerations of the tick order that could drift apart are worth one test.
  - **Verified:** fast tier **481 Sim + 158 Hud**, Long tier **17**. The benchmark itself is
    `[Explicit]` and never runs in CI, which is why its seam has three ordinary tests beside it.

- **The research lane, seven files in one afternoon (OQ-26 … OQ-35, 2026-09-17).** The last
  unwritten block of Phase 2 research, run the way the brief says to run it: one subagent per
  question, a hard cap of twelve searches or fifteen page reads, the fixed format, and the
  clean-room rule stated as binding rather than assumed. Seven files, one commit each, then the
  index reconciled last and alone because `INDEX.md` has a single owner.
  - **Two of them corrected the question they were sent to answer**, which is the argument for
    asking rather than assuming. `b-timberborn` was asked why Timberborn's slopes are what we chose
    not to do; its terrain is **cubes**, with no sloped geometry at all, so ADR 0002 has no
    counter-example there and the row's premise was simply wrong. And `a-16` was expected to
    describe how the multi-level mods move pawns between levels; what it found is that **movement
    was never the problem**.
  - **The single most useful finding is a-16's, and it changes what the vertical slice must prove.**
    Every generation of multi-level mod succeeded at *traversal* and failed at *queries*. Pawns
    walked between levels early. Bills, hauling, construction delivery, the right-click menu and
    reachability all stayed same-map, and the current mod bridges a bench to one other level by
    hand, per bench — a confession that the general query could not be made layer-aware. None ever
    delivered cross-layer line of sight or combat. **A colonist climbing a ladder demonstrates
    nothing anybody struggled with; a bill sourcing its ingredients two layers down is the test.**
  - **Layer question 3 is answered in both halves, by two files that agree.** Rooms stay **per
    layer** — DF states outright that a room cannot span z-levels and Going Medieval independently
    treats each storey as its own — and heat **rises without buoyancy**, as one asymmetric
    conductance on a vertical opening, about 4:1 up against down. The evidence for the second is
    negative and all the better for it: Going Medieval merges levels through a stair and averages
    them, and its own players report the consequence, a tall stack that loses its heat with loft
    and cellar reading the same. Both files independently ask for the same next artefact — a typed
    cross-layer **opening** graph built at M3 beside room detection, because temperature is its
    first consumer and retrofitting means touching detection twice.
  - **Three findings land directly on code we have or are about to write.** The impressiveness
    formula uses a natural logarithm, and a `Math.Log` in the mood path is a **cross-runtime hash
    hazard** now that the world is in the hash and Mono-versus-CoreCLR is a standing test —
    quantise to an integer tier before anything simulated reads it. "Enclosed" is three booleans
    with three deliberately disagreeing thresholds, so a single `IsEnclosed` flag will be wrong
    within a milestone. And Timberborn's ramp-as-object throws our **hop** into relief: ours is
    priced in three separate seams that our own notes say fail silently when they disagree, and
    nothing tests that they agree.
  - **Stonehearth named the counter we should have been watching.** Not frame time — frame time is
    the lagging indicator everyone watched while that game died — but **pathfinder calls per job
    assigned**. It is theory-free, which is the point: it would have caught our own falsified
    pathfinding explanation immediately, because it does not depend on any account of *why*
    searches are expensive. Its measured largest cost was an item-filter cache invalidated on every
    item move, scaling with **items and containers rather than agents** — and every benchmark we
    own scales pawns and map size, so we would not currently see that disease until we had its
    symptoms.
  - **Two stale figures were corrected on the way in, by me and not by the agents.** Both the DF
    and Stonehearth files reason against "our 65% of tick is A-star". That number is the D1 stress
    harness; `OQ-19` had measured the real tick hours earlier at 37% of a 0.025 ms tick for the
    whole pawn phase. Each file now carries a dated editor's note, because the advice survives the
    correction but a session hunting a 65% defect in the running game would be hunting a phantom.
  - **The clean-room rule held, and two agents demonstrated it rather than claiming it.** The
    temperature agent found a decompiled source file in its search results and deliberately did not
    open it. The Goblin Camp agent read GPLv3 source directly — permitted, since the rule is read
    for technique — and the file it produced contains no code, no identifiers and no transcription,
    and states explicitly that a permissive licence would not have changed the answer. I checked
    each file myself for code fences, Def XML and pasted identifiers rather than trusting the
    summaries.
  - **One negative source is cited as such.** The top search result for Stonehearth pathfinding
    optimisation is a post the community publicly flagged as fabricated, whose invented claims
    search engines now restate as fact. The file cites it as a caution rather than pretending it
    does not exist, which is the right way to leave a trap for the next reader to find.

- **The hop price gets one owner, and the warning becomes a test (2026-09-17).** `CLAUDE.md` has
  carried a sentence since the hop landed — *"a price the planner and the mover disagree about
  fails silently"* — earned the hard way: the mover once read a hop's price off connectors, found
  none, and fell through to `MoveCost.Fall`, which is 100,000 and means forbidden. The pawn did not
  throw and did not re-plan. It stood in the cell before the step holding a legal path, earning
  about one unit of progress a tick against a bill of a hundred thousand, and was still there after
  10,000 ticks. Nothing enforced the sentence. Today's Timberborn research arrived at the same
  recommendation from the outside — their ramp is an object in a cell, so one row answers the
  planner, the mover and the renderer at once — which is what moved this up the queue.
  - **Nothing was disagreeing, and that is the finding.** All three seams — `PathFinder`'s
    relaxation, `NavGraph.TryHopEdges` and `MovementSystem.StepCost` — arrived at the same number,
    because each named `MoveCost.JumpUp` and `MoveCost.Drop` for itself and the constants happened
    to be identical everywhere. **Agreement by coincidence**, which holds exactly until the price
    stops being a constant: a hop onto ice, a hop while carrying, a hop for a different traverse
    mode. Then two of the three would silently keep the old number and the failure would present as
    a colonist standing still.
  - **`NavGraph.HopCost` is now the only expression of the rule**, with a second overload taking a
    direction rather than two `CellRef`s. That overload exists for a measured reason: the search's
    inner loop walks cell indices and would have paid a division per neighbour to recover a
    `CellRef` it does not need. Pathfinding is the hot path, so the one-owner rule had to be free
    to obey or it would have been disobeyed for a good reason. The price is asked once per search
    and held in a local.
  - **The guard is a source grep, because what it forbids leaves no trace in IL.** Naming a
    constant compiles to the same instruction as calling a method that returns it, so there is
    nothing to inspect after the fact. `HopPriceHasOneOwnerTests` walks `Assets/Odyssey/Sim`,
    strips comments — a comment may *discuss* the price, only code may not *decide* it — and fails
    on any file but `NavGrid.cs` (which defines the constants) and `NavGraph.cs` (which owns the
    rule). Beside it, a test that reads the price off a **built region graph** rather than off the
    source, so a future `TryHopEdges` routed through a different rule fails even if it never names
    a constant.
  - **Controlled, not assumed.** Putting `MoveCost.JumpUp` back into `PathFinder` fails the guard
    with the exact offending file and line; restoring it passes. The failure message names the
    10,000-tick incident, so whoever trips it in two years learns why the rule exists rather than
    just that it does.
  - **No golden moved**, which is the point: who computes the number changed and the number did
    not. Verified fast tier **484 Sim + 158 Hud**, Long tier **17**.
  - **One fixture error worth recording.** The first region-graph test looked for the hop edge by
    its two cells and found nothing. `TryHopEdges` keys by *region pair* and stores whichever cell
    pair it met first, so asking for our own two cells was asking the wrong question. Found by the
    test failing rather than by reading the code — which is the third time this week that reading
    the code would have been wrong.

- **The tick's allocation is attributed, and most of it was a defect (2026-09-17).** `OQ-19` had
  left a loose end: the real tick grows the heap by 76.7 bytes at rest and 284.6 under replan
  pressure, against the D1 spike's true zero, and the ADR said the difference *pointed at* the
  served path's cell array without demonstrating it. This closes it, and the answer was not what
  the pointing suggested.
  - **The at-rest cost was not the colony, and bracketing is what showed it.** An *empty* world —
    no systems, no pawns, no snapshot contributors — allocated **67.4 bytes a tick**, and adding a
    whole colony of five with needs, jobs and movement added **nothing measurable**. A cost that
    scales with neither pawns nor systems nor contributors cannot be any of them, which narrowed
    it to the tick machinery in one measurement instead of a hunt.
  - **It was `Intents.Drain(HandleIntent)`.** `Drain` takes a `Func<Intent, IntentRejection>`, and
    a method group converts to a **fresh delegate on every call** — 64 bytes a tick, for a handler
    that never changes, paid by every tick of every game whether or not one intent was submitted.
    Holding it in a `readonly` field took the empty world to **1.6 bytes a tick** and the colony to
    3.3; on the real benchmark the colony went from **76.7 to 11.0**, about 4.6 MB a day down to
    0.66 MB. One field.
  - **The per-path half is now demonstrated rather than pointed at.** Serving the same request at
    two path lengths gives 53.4 bytes for 5 cells and 197.4 for 41 — **exactly 4.00 bytes per extra
    cell**, an `int`, so a request costs `≈32 + 4 × cells`. That reproduces the 208 the benchmark
    saw, which is the check that the attribution is the whole story and not a coincidence.
  - **The remaining 214 bytes a request is kept deliberately, and the reason is a hazard rather
    than a cost.** Pooling the served array would make `ServedPath.Cells` valid only until the next
    `Serve()`. It is safe *today* — `MovementSystem` is the only consumer and `Pawn.AdoptPath`
    copies into the pawn's own buffer, which I checked rather than assumed — but `Served` is
    public, and the first future consumer to hold the array would be silently reading somebody
    else's path. **The project has already decided this shape of question once**, on the state hash
    (OQ-50): an honest cost beats a silent wrongness. Same answer. It stays until pooling can be
    made safe *by construction* rather than safe by inspection.
  - **Controlled both ways.** Putting the method group back restores 67.4 bytes a tick and fails
    the new guard with the message naming the cause; restoring the field passes. The guard's budget
    is a loose 16 bytes on purpose — its job is to keep a 64-byte-per-tick delegate out, not to pin
    11.
  - **Reading the code would have found the delegate too, and would probably have found the array
    first and stopped.** The measurement is what said the array was the *smaller* half of the
    at-rest story and in fact no part of it at all. Fourth time this week.
  - **Verified:** fast tier **484 Sim + 158 Hud**, Long tier **19**, no golden moved — caching a
    delegate cannot change behaviour, and the goldens agree.

- **Walls went up stepped, and the fault was not building's (branch `claude/build-pipeline`,
  2026-09-17).** The owner pressed Play, ordered a room, and the colony built it — which settles
  §8's unreproduced report: the composition fix was the whole of it. But the finished wall was
  **not flush**: every panel sat a few centimetres above or below its neighbour, a notch at each
  cell join and at each corner. Three screenshots, and the cropped one is unambiguous — the outer
  faces are coplanar, the top edge steps vertically at exactly the cell pitch, no depth offset.
  - **Nothing about the build pipeline was wrong.** The fault was in `ChunkMesher` and was older
    than this line: a wall panel was placed with `GroundRelief.Lift`, which takes *one height from
    one point*. Two panels of one run stand 2.5 m apart on a drawn field of amplitude 2 m and
    period 150 m, so they differ by the field's slope across a whole cell. **Measured over the
    board: 73 mm on average, 220 mm at the worst, against a 3 m wall.** At the cells the new test
    uses, 147 mm.
  - **The fix is the operation the ground already used.** `GroundRelief.Drape` shears the piece
    onto the tangent plane of the field at its own centre, so two neighbours are tangent planes of
    one smooth surface and part company only by its *curvature* — second order. **The same seam
    measures 1.1 mm.** The shear leaves vertical edges vertical (determinant one, Y sheared by x
    and z), so the wall stays plumb and a full 3 m tall; only its head and foot rake with the
    ground, which is what a wall built along a slope does. With relief off, `Drape` is exactly a
    translate, so the flat board draws byte-identically and no contact sheet moves.
  - **`EmitWater` had already written the whole argument down**, having got it wrong twice — a
    lifted tile takes its height from its own centre and neighbours open slivers you can see the
    riverbed through. The built world was not reading it. The general rule now sits in
    `ChunkMesher`'s class comment: **anything fixed to the grid is draped; only what moves over it
    is lifted.** Floors, walls, doors, stairs, ladders, pillars and cell-filling edifices are
    draped; grass tufts, dropped items, figures and cursors stay lifted, because they stand at a
    point and share an edge with nothing. `EmitFloor`'s old comment — "a built floor is man-made
    and stays flat" — was the reasoning that produced the bug: staying flat is exactly what opens
    the seam.
  - **What made this quick was refusing to read code for it.** The relief field was re-implemented
    in twenty lines of Python and the step measured before a line of C# was touched, then measured
    again after. A fix whose before-and-after numbers are 147 mm and 1.1 mm needs no screenshot to
    be believed — though it still wants one, because nobody has pressed Play on it.
  - **Guards:** `ChunkMesherTests.AWallRunMeetsItselfAtOneHeightOnRollingGround` and
    `.AFloorMeetsItselfAtOneHeightOnRollingGround` walk every drawn corner, pair the ones standing
    over the same point of the board, and hold their disagreement under 20 mm — which the old code
    misses by sevenfold. `.ADrapedWallStaysVerticalAndFullHeight` stops the next person buying
    flushness by leaning the building over.

- **And the wall was hollow, which is a different fault the same screenshots showed (same branch,
  same day).** The owner, looking at the first finished room: *"the walls should be filled in with
  a top as well."* They were not. A wall cell is drawn as a panel on each face something can be
  seen through — which is what stops a one-cell wall reading as a 2.5 m slab and is deliberate —
  but a straight run puts two panels 2.5 m apart with 2.25 m of nothing between them and nothing
  over them. From a high camera every wall had a black slot down its middle.
  - **The owner's three questions came apart, and two of them were already answered.** "Colonists
    always build from the outside so they don't get stuck" is true today and is not a drawing
    question: both `DeliverWorkGiver` and `BuildWorkGiver` take their stand cell from
    `FellJobDriver.StandBeside`, and the comment at the delivery site says why — the site is
    walkable right up to the moment the wall goes up in it, so standing *in* it would work for the
    delivery and be exactly wrong for the build that follows. "Build electricity through it" is a
    question about what a cell may hold, which neither answer below changes.
  - **The third — "is it worth making the wall half the size of the cell and making it like a
    block?" — is two questions in one coat:** should the cell be filled, and how thick should a
    wall look. **They separate because a wall already occupied the full 2.5 m of its cell as
    drawn**, two faces of it and a hole. Filling it changes nothing about how thick a wall *reads*.
  - **Filled, thickness left alone (owner, 2026-09-17).** One block per wall cell behind the
    panels, `ModuleIds.WallCore`, core and cap in one. No art (it falls back to the cell-shaped
    primitive and wears the wall's own stuff tint, so it matches the panels in colour if not in
    texture), no orientation logic, one extra instance per wall cell. Recessed 1 cm below the
    panels' heads, because a panel straddles its face and 0.125 m of it stands inside the cell —
    the two tops would otherwise be coplanar, and a z-fight along the head of every wall in the
    colony is a shimmering line the camera cannot get away from. A centimetre is sub-pixel at 32 m,
    the nearest the camera comes. A window keeps its hollow.
  - **Held open: the half-cell wall.** It buys a wall that reads as a wall rather than as a
    rampart, and costs a per-cell run direction — which the earth blocks already solve, since
    `GroundMesh.CanonicalExposure` turns all sixteen neighbour patterns into five meshes and a
    rotation and a wall junction is the same problem — plus meshes for the straight, the corner and
    the tee, and something to cover the 0.6 m of bare cell it would leave each side, which today's
    floor slab does not reach. **The cap is the cheap experiment that settles it:** look at a room
    and say whether 2.5 m is a fortress or just a wall.

- **The build line caught up with `main`, and the golden re-bake was made to say why (branch
  `claude/build-pipeline`, 2026-09-17).** PR #63 had gone conflicting: `main` had baked new golden
  hashes when `CellGrid` joined the state hash (OQ-50) while the branch had baked its own on the
  old scheme, so neither side's numbers could survive. The merge is otherwise unremarkable — the
  journal's two appended entries both kept, `main`'s first because it finishes the bullet the
  branch's interrupted.
  - **The re-bake was not taken on trust.** The obvious story was "the construction grid is new
    hashed state, of course the number moved". A **control run** with `ConstructionGrid`'s
    `AddTickable` registration removed came back at 17805151056309647682 — not `main`'s
    13449042641056873599 — so the construction grid was *not* the whole of it. The rest is
    `Skill_Construction`: a new skill widens every pawn's skill array, which moves the generated
    hash before a tick has run. Two causes, both deliberate, and the second would have been
    invisible under a re-bake that stopped at the first plausible explanation.
  - **Both tiers green for the first time on this branch** (run 35194913924, and again after a
    second merge): fast tier 478 Sim + 134 Hud, Long 15, Unity 1,055 EditMode and 27 PlayMode, plus
    the headless day. The PR's "unrun: `unity.sh test editmode`" caveat is retired.
  - **Worth noticing for next time:** both grids register for the hash by being an `ITickable` with
    `TickGroup.Never`, a trick that pre-dates `SimWorldBuilder.AddHashable` and that `AddHashable`
    now exists to replace. Left alone here — moving them would shift every golden again for no
    behaviour — but the next person to touch either file should use the third list.

- **The build cursor became the wall, and a build box stopped widening by accident (branch
  `claude/build-pipeline`, 2026-09-17).** Two owner reports from the first playtest that built
  anything, and they are the same gesture seen from its two ends.
  - **"Use a cube instead, with all lines showing."** A build drag drew the selection bracket —
    eight corner stubs — once per cell, which along a six-cell run reads as a dotted line and says
    "these are things you have picked". It now draws one closed wireframe box, all twelve edges,
    spanning the whole run. The box is **draped** rather than lifted, so it shears onto the tangent
    plane of the drawn field at its own centre exactly as the wall it is promising will: the rule
    from the stepped-wall fault, applied to the cursor. Lifted, a fifteen-metre box takes one height
    from one point and floats at its far end.
  - **A run that steps up a riser is one box per level.** A build order is lifted onto the cell
    standing on solid ground, decided per column, so a run across a terrace stands on two layers and
    one box around all of it would be a box around neither. `BuildPreview.Gather` does that split in
    `Odyssey.Hud`, where the fast tier can hold it, with the lift passed in as a function of the
    column.
  - **"The building is a tad sensitive and by accident you can build dual walls."** A wall dragged
    along one axis with the pointer a single cell off the row covered two rows — two parallel walls,
    ordered, carried to and paid for, out of a gesture that meant one. The owner chose **hysteresis
    over a snap to a line**, so a rectangle of wall is still one gesture: the box widens at two
    cells clear across the run and the gate re-arms only back at the anchor's own row. Two
    thresholds, because one makes the box flicker between one row and two while the pointer sits on
    the boundary. Build only — one more cell marked to dig is a rounding error and one more row of
    wall is a wall.
  - **`Matrix4x4.TRS` was written out by hand**, and that is not a micro-optimisation. No bar of a
    box is rotated, so the quaternion is an identity multiplied through for nothing — but the real
    reason is that `TRS` is an engine call that throws outside the player, and with it in the way
    the cursor's geometry could only be checked by looking at it. Written out, the twelve edges are
    measured in a test: four per axis, each the full length of its side, every corner a three-way
    joint, and a draped box plumb, full height, and raking with the field's own slope at a point
    measured to be near the steepest the board gets.
  - **Control run:** with the edges shortened back to bracket stubs, three of the five cursor tests
    fail; with the box gate disabled, three of the six drag tests fail. Restored, both green.
  - **Verified:** fast tier **478 Sim + 146 Hud** (up 12), Long tier 15. The Presentation assembly
    compiles headlessly against the mirror DLLs and its new tests were *run* outside Unity through a
    throwaway `net8.0` NUnit project, which is `docs/lessons.md`'s trick used for the first time on
    tests rather than on a number. **Nobody has pressed Play on it.**

- **The widening gate was loosened the same day it landed, and the second fault was the
  interesting one (branch `claude/build-pipeline`, 2026-09-17).** The owner played the first
  version in this worktree's own editor — checked, rather than assumed, against
  `docs/lessons.md`'s "confirm delivery before diagnosing" — and reported it was still too easy to
  create double walls.
  - **The threshold was the obvious half.** Two cells clear is five metres of board, which sounds
    generous until twenty metres of wall is drawn at a camera looking down a slope. It is three now.
  - **The gate was also sticky, and that is what made it fail in practice.** It re-armed only on the
    anchor's *exact* row, so a single wander anywhere in a long drag latched the box wide for the
    rest of it — and a pointer that has strayed three cells rarely returns to precisely the row it
    left. The player would let go over a rectangle without ever having seen the moment it widened.
    Re-arming within one cell of the row instead keeps the two thresholds that stop the flicker
    while making a trip recoverable.
  - **Worth keeping in mind as a shape of bug:** a latch and a threshold are two separate decisions,
    and tuning the threshold alone would have made the same complaint come back quieter. The first
    version's own test — "once widened it does not flicker back on the boundary" — was passing and
    was pinning the sticky behaviour as if it were the feature.
  - **Verified:** fast tier 478 Sim + 146 Hud, unchanged in count because the three tests that
    encoded the old numbers were re-aimed rather than added to.

- **The build line was played and accepted (branch `claude/build-pipeline`, 2026-09-17).** The owner
  ordered walls in the running game with the loosened gate and reported it works. That closes the
  gesture: three rounds in one day — the box widening on a one-cell wander, then on a two-cell one,
  then not. **Both rounds were answered by changing what the rule *is*, not only its number**, and
  the second one would have been missed by tuning alone.
  - **What the playtests have actually judged** is ordering a wall, the material row, the cursor and
    the widening gate. The drape and the fill were in the same build and drew no complaint, which is
    weaker than a judgement and is written down as such. The site marks, the blueprint readout in
    the inspect pane and the computed hammer swing are in the build and nobody has said anything
    about them either way; `docs/design/15-building.md` §8 names them so the next session does not
    mistake silence for approval.
  - **The branch was merged with `main` three times in the day** — 27 commits in one of them — and
    the merges are the reason to notice how fast parallel branches are landing. A PR that sits for a
    few hours is a PR that conflicts, and when it conflicts GitHub stops scheduling its checks
    altogether, so the first symptom is not a red tick but no tick at all.
- **The scene could not save, and the reason was structural (U34, 2026-09-17).** `WorldSave` has
  been complete, versioned and tested for weeks. Every caller of it was a test. The composition root
  had forty lines that were a copy of `ColonyWorld.Build`, so it never built the `SaveComponents`
  list the save format is handed — and **the one world a player actually ran was the one world in
  the project that could not be written to a file.** Nothing said so, because nothing asked.
  - **The copy had drifted twice over, and neither drift was visible.** It registered no connectors
    with the navigation graph, so a stair on a city map joined no region and the two storeys it
    linked were unreachable from each other; natural maps have no connectors, which is the only
    reason nobody met it. And it never ran `RebuildDerived`, so the scene's support field was
    whatever worldgen pass 10 had left rather than a full solve. Both are fixed by deletion.
  - **A request rather than more parameters.** `ColonyWorld.Build` had seven, and the two the scene
    still needed — a clock that does not start at midnight, and a snapshot contributor for the
    renderer — would have made nine. That is the point at which people write their own build
    instead, which is precisely what had happened. `ColonyRequest` is also the shape a new-game
    screen has to hand around, so it was going to exist anyway.
  - **The mirror is a factory, not a contributor.** It is built *from* the generated grid, and
    generation is the first thing `Build` does, so it cannot be passed in ready-made. A
    `Func<CellGrid, MapGenOutcome, ISnapshotContributor>` keeps `ColonyWorld` free of any knowledge
    of `WorldRenderModel`, and keeps the registration order — mirror before colony — inside the one
    place that can guarantee it.
  - **A start tick, not a start hour.** `GameClock` lives in `Odyssey.Hud`, which `Odyssey.Sim` may
    not reference, so the caller converts. Zero is inert, which is what lets every baked hash stay
    where it was; there is a test asserting exactly that, because the alternative is moving every
    golden value in the repository by accident.
  - **The assertions were weaker than they read, and then the ground moved under them.** Every new
    test said "the same world" and compared `ComputeStateHash`, which at the time **could not see
    the cell grid** — so a save round trip asserted with it was blind to whether the board came back
    at all. This branch worked around it by lifting the golden's bespoke composite into
    `Golden.FullHash` and using that. **`OQ-50` landed on `main` the same evening** and put the grid
    in the canonical hash, so the workaround was obsolete before it merged: the fold is gone again,
    the tests are back on `ComputeStateHash`, and it now means what they always claimed. Worth
    recording because the branch was right about the problem and wrong about how long it would last.
  - **One claim was checked rather than inherited.** `RebuildDerived`'s doc comment has always said
    a full solve reproduces what worldgen settled. Nobody had measured it against the board the
    scene loads. It holds, and there is now a test that says so.
  - **What this was for.** It is `U34`, the first unit of **`MS`, the start flow** — menu, new game,
    colonist select, save and load — which the owner scheduled on 2026-09-17 beside M3 rather than
    inside it. The seam work comes first by the owner's decision, and this is the half of the
    bootstrap chokepoint that a save file cannot be built without.
  - **Verified:** fast tier **465 Sim + 117 Hud**, Long tier **15**, EditMode **1017 passed, 0
    failed**, PlayMode **29 passed, 0 failed** — including two new ones that ask the half only Unity
    can ask: that the live bootstrap, having started for real, is holding that world rather than a
    private copy of the wiring.

  - **Brought up to date 2026-09-17, after fifty-five commits had landed on `main` underneath it.**
    The merge was not mechanical, because the build line (`#63`) had meanwhile added the
    construction grid to **exactly the hand-rolled block this change deletes**. Resolved as a
    union rather than a choice: the composition root keeps U34's `ColonyRequest` build, and
    `ColonyWorld.Build` carries main's construction grid through it — so the scene now gets a
    construction grid that is registered, hashed and in `SaveComponents`, which the bootstrap's
    own copy had been discarding with `out _`. Checked that presentation reads construction
    through the snapshot and not by holding the grid, so nothing needed rewiring. The
    `Tests and gates` line conflicted for the reason recorded that morning — it is a shared
    counter with no owner — and took main's number before being re-measured. Verified after the
    merge: fast tier **494 Sim + 170 Hud**, Long tier **19**, both content gates green.

- **The instancing holds at scale, and the scale found a boundary the small fixtures could not
  (OQ-03, 2026-09-17).** The rendering design rests on one structural property: a draw bucket is
  one *(module, part, tint)* within one chunk, so submission is bounded by the **variety** on the
  board and not by the **quantity**. Every mesher test until now used an eight-by-eight board,
  where that distinction cannot appear at all.
  - **Measured: 20,000 wall cells of four stuffs over 64 chunks give 512 buckets and 100,000
    instances**, 195 instances per bucket. 512 is exactly 64 × 2 kinds × 4 tints — the bound is
    *met*, not merely respected, because this board carries every combination in every chunk. The
    claim holds.
  - **A checkerboard rather than a solid field, deliberately.** Twenty thousand walls packed solid
    would have almost no exposed faces: it would stress the bucket count and leave the instance
    count flat, and the instance count is the half that is supposed to grow. Islands give every
    wall four exposed sides.
  - **What the scale found.** The mesher draws **exactly five instances per wall cell** — four side
    panels and one fill-and-cap — whether or not a side is exposed. Counting faces independently
    off the grid gives **79,600**, not 80,000: the 400 missing are the faces that point off the
    edge of the board, 100 along each side, and the mesher draws them anyway.
    `TheWorldBoundaryIsNotAnExposedFace` establishes exactly that rule — *"there is no outside of
    the map, so there is nowhere those faces could be seen from"* — for **terrain**, and it is not
    applied to **edifice walls**. On an eight-by-eight fixture the difference is a handful of
    instances and invisible; at scale it is 400 of 100,000.
  - **0.4%, and recorded rather than fixed.** It costs nothing; it is an inconsistency between two
    kinds of geometry rather than a performance problem. This row may not touch
    `Presentation/Rendering/` and `ChunkMesher` is the owner's live file, so the test pins the
    behaviour **as it is**, with a failure message saying that a deliberate edge-culling fix should
    move the number. Drift fails; an intended change reads as intended.
  - **Both figures are exact on purpose**, because this is the *before* half of `OQ-46`, whose
    acceptance is that mesh contributors leave the bucket counts identical. "About the same" cannot
    be compared a month later.
  - **The slice measurement the row asks for**, on the played meadow at 120 × 120 × 16:
    `draw calls 1502, instances 34961, chunks drawn 104, materials 19`; first slice 56.03 ms
    including 200 chunk meshes, **steady submit 0.22 ms a frame**, warm full re-mesh of 200 chunks
    13.38 ms at 0.067 ms a chunk.
  - **Verified:** Unity EditMode **1121 total, 0 failed**.

- **A terrain kind brings its own meshing, and the seams list closes (OQ-46, 2026-09-17).** The last
  of the five chokepoints the mining line exposed. `ChunkMesher.EmitTerrain` was a chain of early
  returns — water, then surface, then an exposure gate, then stone, then earth, then a plain block —
  so every new terrain feature was an edit to a file on the queue's own do-not-touch list. Both the
  water line and the mining line edited it anyway, which is what a rule the design leaves no way to
  obey looks like from outside. It is now a registered list of `ITerrainContributor`; a contributor
  states its own condition and the first to claim a cell ends it.
  - **The acceptance was "change nothing", and it is met exactly.** `ChunkMesherTests` passes with
    no edits, and `ChunkBucketScaleTests` reports **512 buckets and 100,000 instances** — the same
    numbers, to the instance, as before the refactor. That is the reason OQ-03 was done first: a
    refactor whose whole promise is that output is unchanged needs an oracle that existed before it
    started, and "about the same" cannot be checked a month later.
  - **Three things were hoisted, each of which could have moved a pixel.** The tint and the drape
    are now computed before any contributor is asked, because in the chain water was decided
    *before* those two lines ran and everything else after, so what was available depended on where
    in the method you were; both are pure functions of the cell, so the cost is one tint lookup on a
    water cell. `ShowsAFace` is computed once and **only for solid cells**, which is exactly when
    the chain computed it — three contributors read it, and letting each work it out would pay for
    a neighbour scan three times on the mesher's hot path. And `ChunkRenderer.Earth` now forwards
    to the contributor that owns the switch rather than to a field, which is the failure this change
    could most easily have introduced: a toggle that silently stops toggling. There is a test that
    it still reaches the thing it switches.
  - **The new test was wrong before the code was, and the mistake is the useful part.** It
    registered a spy and asserted it got asked; it never was, and the message read as a broken seam.
    The fixture was built with `Solid()`, which defaults to rock, and **`StoneContributor` claims
    every rock cell before a registered contributor is reached**. So "registered ahead of the plain
    block" does not mean registered ahead of *everything* — only ahead of the fallback, which is the
    only place a new contributor can intercept. The test now uses `TerrainFill`, which is solid and
    neither stone nor earth, and its doc comment records why, including that the first version
    measured the wrong thing. Fifth time this week that reading the code would have misled and a
    test caught it.
  - **`StuffPalette.cs` was in the row's file list and turned out to have no switch in it at all**,
    so it was not touched. The row was written from a guess about where the branching lived.
  - **Verified:** Unity EditMode **1125 total, 0 failed**.
  - **What this leaves.** All five chokepoints named after the mining line are open. The one piece
    of the bootstrap row still standing is the **presentation half** — every director wired by hand
    in `OdysseyBootstrap` — and it has no queue row.

- **The session seam: a world can be put down and built again (U35, 2026-09-17).** The first unit of
  `MS` after U34, and the one `U38`–`U40` are all blocked on. Until now a world existed because
  `Start` made one and stopped existing because the scene closed; a main screen needs both halves on
  demand. `Start`'s 214 lines are now `BuildSession()` and `TeardownSession()`, both public, with
  `buildOnPlay` defaulting true so pressing Play still lands straight in a colony and **every
  existing PlayMode test passes unedited**, which the row required.
  - **Teardown drops references rather than only disposing them.** A disposed-but-reachable library
    would let the next session read a torn-down object and fail somewhere far from the cause, so
    every field is nulled. It is safe with no session and safe twice, because a menu unwinding and a
    scene closing both reach it and can arrive in either order. Building over a live session throws
    instead of silently doubling — that failure would leak a whole world and present as memory
    rather than as a bug.
  - **The hash test compares three worlds, not two.** Build → teardown → build, and a separately
    built rig. Comparing the rebuild only against the first build would pass if both were wrong in
    the same way, which is exactly what a leaked static does. It is the **full** hash including the
    board, which only became possible when the world joined the hash the same morning (OQ-50).
  - **It failed first, and the failure was the test's.** The first version read one hash after a
    timed warm-up and the other after a different timed warm-up — and the tick counter is *in* the
    hash, so it compared two worlds of different ages and called the difference a leak. Every hash
    is now read immediately after an explicit `BuildSession`, with no frame in between.
  - **The leak half is deferred, honestly, and this is the part worth reading.** It first counted
    colonist figures and found **zero**: the rig passes no module catalogue, so no character prefab
    is ever instantiated and the assertion could only ever have seen nothing. Retargeted at meshes —
    the leak `TeardownSession`'s own comment records, which "leaked the whole cast, every session,
    until the graphics device was reset" — and measured **45 → 45**. With no catalogue the library
    resolves every module to one of Unity's built-in shared primitives and bakes nothing, so there
    is no allocation to give back.
  - **And it cannot simply be fixed by giving the rig a catalogue**, because that would make a test
    depend on the licensed packs, against the standing rule that a clone without them still builds
    and runs. So the check `Assert.Ignore`s with that reason written out. A green tick there would
    have claimed coverage of exactly the failure it cannot see — the same call made earlier the same
    day about Mono's GC accounting, and for the same reason.
  - **Twice in this unit a control refused to let a test pass vacuously**, which is the second and
    third time today. Guessing what to count was wrong both times; measuring settled it in one run.
  - **Verified:** PlayMode **35 total, 32 passed, 0 failed**, one ignored with its reason recorded.

- **CLAUDE.md's status caught up with `main`, 2026-09-17.** Two lines had gone stale in the way
  this file warns its own readers about: U26 (building) still read "on `claude/build-pipeline`
  (PR #63), not yet merged" after the merge had happened and been reconciled against OQ-50's golden
  re-bake, and the `MS` section still said only "`U34` is done" after `U35` (the session seam)
  merged as PR #83. Both corrected in place rather than left for the next session to trip over.
  Nothing here changed the game; no wiki or registry rebuild applies.
- **The audio faders learned to drag, 2026-09-17.** The owner asked for the audio settings to
  be sliders that drag left to right rather than buttons. That reverses this journal's own
  "rungs, not sliders, everywhere" from the morning — recorded here because a reversal nobody
  can find is a decision that never happened — and it is the right reversal for this one
  control: a fader holds a continuum, six of the seven rungs sat between −36 and 0, so a
  slider that still snapped would have spent most of its travel as dead space. The part of the
  old argument worth keeping is kept: the track is **linear in dB**, so equal travel is equal
  change of loudness, and whole decibels only, so the thumb always rests somewhere the readout
  can say.

  - **The director takes what the thumb offers.** `SetBusDb` now clamps to
    `SettingsDirector.SilenceDb`/`UnityDb` (−80 and 0, restating `AudioMath` across the ADR
    0003 seam the rungs already crossed) instead of snapping to `VolumeDbRungs`, which is
    deleted. Seeding clamps the same way. The write-through bargain is untouched: the
    presenter still routes every change through `AudioSettingsStore`, so there is still no
    second copy of a volume anywhere, and a drag still costs one store save per whole dB it
    crosses — the shell rounds before it asks.
  - **The control is Unity's own `Slider`, restyled, not a hand-rolled thumb.** Its drag
    capture, track-jump and arrow keys are behaviour this HUD has no reason to re-derive, and
    the Build palette's scroller already showed the way in over the default theme. The sheet
    seats a 12 px accent thumb on a 3 px hairline track, centred on its value by a negative
    margin because Unity seats the dragger's *left edge* at the percentage. Each row says the
    figure beside its label — "Mute" at the floor as a word in the text face, a number in the
    mono face everywhere else.
  - **The loop is pinned from both sides.** The fast tier's volume test now asserts the
    continuum (any whole dB taken as it stands, clamped at both ends, announced only when it
    moves). `HudGeometryTests.AVolumeSliderDragsItsBusAndThePanelAgrees` drives the slider's
    value the way the engine would deliver a drag and asserts the whole round trip — fader to
    director to thumb and readout, one bus's fader not moving another's. A real pointer drag
    is beyond today's harness: `MouseHarness` cannot press a button in a batch run, which its
    own docs say in four acts.
  - **Verified:** fast tier **478 Sim + 158 Hud**, Unity EditMode **1070**, PlayMode **28 of
    30** (the two standing `[Ignore]`s). **Not verified:** an eye — as with everything else in
    the rebuilt interface, nobody has pressed Play on it yet.

- **Unity moved to the middle of the fader, 2026-09-17, an hour after it learned to drag.**
  The owner's second ask: seat the default (0 dB) at the centre of the slider, so dragging
  left of it lowers towards mute and dragging right makes the sound higher. A boost is a new
  capability, not a restatement — every clamp in the chain said "a bus cannot amplify, only
  attenuate" — so the ceiling had to move in one motion everywhere a fader's value is held.

  - **The span is now −80 to +12 dB, and the ceiling is +12 for a reason.** `AudioMath`
    gained `BoostDb` (+12, four times the amplitude: enough lift for a quiet mix, bounded
    because a boost multiplies the author's own volume and can clip), and every stop widened
    to it in the same change — `AudioSettingsStore.SetDb`, `AudioDirector.SetBusDb`, and
    `StackDb`, whose combination cap means master and bus both pushed to their tops still
    meet one ceiling. `SettingsDirector.BoostDb` mirrors it across the ADR 0003 seam the
    way `SilenceDb` always has.
  - **The centre is bought with a two-scale track.** One linear dB track cannot put unity in
    the middle: −80 to +12 would seat it six sevenths of the way right. So the slider's own
    value is track position, −1 to +1, and `SettingsDirector.TrackOf`/`DbOf` map each half
    separately — 80 dB of attenuation across the left half, the boost across the right, each
    linear in dB within itself. The price is that the right half is ~6.7× coarser per pixel
    than the left; paid in drag sensitivity rather than in honesty, and every whole dB still
    round-trips through its seat (a fast-tier test says so, and pins unity at exactly 0, the
    centre).
  - **A notch marks the centre.** A centre that means "the way it shipped" is only worth
    seating if it is findable afterwards: a 1 px mark under the thumb, drawn beneath the
    track and thumb both.
  - **A geometry bug in the morning's fader was found by this change and fixed.** The USS
    had given the thumb a −6 px left margin on a theory that Unity seats the dragger's left
    edge at the percentage. It does sweep the left edge — from the track's start to its end
    minus the thumb — so the honest construction is track end-margins of half the thumb and
    no offset on the thumb itself. With unity at a marked centre, a 6 px lie would have put
    the thumb visibly off the notch; the old −80-to-0 track merely hid it at the ends.
  - **Readouts say their plus.** `+6 dB` on the boost side, `-40 dB` on the attenuation
    side, `Mute` at the floor, `0 dB` at the centre — the two sides of the notch read as the
    different promises they are.
  - **Verified:** fast tier **478 Sim + 159 Hud**, Unity EditMode **1070**, PlayMode **28 of
    30**. **Not verified:** an eye, still — the faders remain unpressed-in-anger like the
    rest of the rebuilt interface.

- **The ambience was never off — it was mixed to a whisper, 2026-09-17.** The owner reported
  the ambience (water/forest) as turned off by default. Every stop in the chain was checked
  before touching a number: the faders all boot at 0 dB (and the machine's stored
  `odyssey.audio.*` prefs were all exactly 0 — not a saved Mute), the probe and the layer
  gating were sound. What remained was the authored mix: the outdoor bed at **0.09 by day
  and 0.07 by night**, behind a **ten-second arrival fade**, on a world whose `CurrentTick`
  starts at 0 — **midnight**, so the first bed a session ever plays is the quieter of the
  two. Roughly −21 dB arriving at a crawl reads, correctly, as nothing.

  - **The fix is the catalogue's mix, and only that.** Outdoor day 0.09 → **0.35**, night
    0.07 → **0.28** (night stays lower: the world is quieter after dark and the bed should
    say so), water 0.20 → **0.40** (it is still coverage-scaled by the probe, so 0.40 is
    "standing in the river", not everywhere), arrival fade 10 s → **4 s**. Applied to the
    committed `AudioCatalogue.asset` and to `AudioSetup.BuildCatalogue`, which remains the
    source of the same numbers. Night-under-day retained; the owner's ear is the final
    mix desk, as ever.
  - **The beds are real recordings, and the never-overwrite rule earned its keep.** Halfway
    through, the working theory was that the *synthesised* placeholder outdoor clip (raw
    peak ≈ 0.06) was shipping; three bed WAVs were deleted ready to regenerate. A
    byte-compare against git — the committed `water.wav` is 7 MB of forty-second stereo —
    said otherwise: water, ambience-day, ambience-night and campfire are all **sourced
    audio** under the placeholder names, exactly the case the tool refuses to overwrite
    without a dialog. Everything was restored from git untouched, verified clean.
  - **The placeholder path was independently inaudible, and is fixed too.** A clone without
    the recordings falls back to the synth, whose outdoor bed peaked around 0.06 — beneath
    even a def volume of 1.0. `AudioSetup` now normalises placeholder bed clips to a shared
    0.5 peak (`BedPeak`), so the def's Volume means the same thing on real audio and
    stand-ins alike. The shipped mix did not change by this route at all.
  - **"On by default" is now a test.** `TheShippedAmbienceBedsAreAudibleByDefault` loads the
    committed catalogue and holds every bed at or above a 0.2 floor — a floor, not the
    exact values, so tuning by ear stays free while drifting back to inaudible fails. It is
    the one audio-director test that reads a committed asset, and says why.
  - **Verified:** nothing to run yet that touches these files — the owner's editor holds the
    project lock, so the Unity tiers (and this new test) run when it next closes; the fader
    work earlier in the day was verified at **478 Sim + 159 Hud, EditMode 1071, PlayMode
    28 of 30** before any of this. **The ear is the gate that matters here**: press Play on
    a fresh boot and the forest should be present within a few seconds, quieter after dark,
    and the river should arrive as you pan to it.

- **The owner's first listen took a notch back off the new ambience defaults, 2026-09-17.**
  The first press of Play since the mix was raised: day 0.35 → **0.28**, night 0.28 →
  **0.22**, water 0.40 → **0.32** — about −2 dB across the beds, present but under the work.
  The floor test still holds (night, the lowest, sits above 0.2), and the generator and the
  committed catalogue carry the same numbers as ever.

- **The cancel tool was already built; nobody could find it (2026-09-17, `claude/cancel-tool`).**
  The owner asked for "a cancel button ... so we can deselect the build". The investigation's whole
  finding was that the mechanic had been in the game since the designate line landed —
  `DesignateTool.Cancel` on the **X** key, drawing a box and sending `CancelDesignation` +
  `CancelBuilding` per cell, with a complete simulation half and its negative controls
  (`FellJobTests.ACancelledOrderStopsTheJob`, `ConstructionTests.CancellingASiteGivesBackWhatWasCarriedToIt`).
  **Every way of finding it was missing**: no chip on the palette, though `ui.arch.tool.cancel` has
  been in the registry all along; no mention of the key anywhere on screen. This is the second time
  a feature has existed and behaved as though it did not, and the first
  (`15-building.md` §7, a composition fault) cost two playtests. This one cost none, because the
  question asked first was *is it there* rather than *how do I build it*.
  - **The chip said "Harvest" and the owner said "harvesting".** `ui.arch.tool.harvest` — "Take the
    crop" — was wired to the fell tool, while `ui.arch.tool.fell` sat unused. The label had taught
    the owner the wrong name for the tool, which is the clearest possible demonstration of what the
    naming registry is for and of what happens when a chip is pointed at the wrong row of it.
  - **Chop, not fell** (owner's call). Labels only: `ui.arch.tool.fell` → "Chop trees",
    `ui.keys.fell` → "Chop tool", `ui.status.felling` → "Chopping", and the axe's description.
    **Every key is unchanged**, because `ui.status.felling` is one of the nineteen that draws real
    art off sheet 06 and `icon-map.csv` is keyed the same way — a key rename would have silently
    dropped an icon back to an outlined square. The C# names (`DesignateTool.Fell`, `FellJobDriver`,
    `JobHandle.Fell`) are deliberately left alone: internal, about twenty files, and no player ever
    sees one.
  - **And the armed banner still said "Felling"**, in C#, which is the failure the registry rule
    exists to prevent and which the rename made visible in the same hour. Mine and Chop now read
    `Registry.Label("ui.status.*")` — those keys were *already* phrased as the thing being done, so
    the voice is unchanged and the next rename carries. Cancel keeps its own words, because there
    is no activity key for it: no colonist is ever *cancelling*.
  - **`PaletteTools` moved out of the shell**, for the reason `HudCommands` already lives in
    `Odyssey.Hud`: the palette is data, and the fast tier can see that assembly and cannot see a
    `MonoBehaviour`. That move is what made the rest testable — **`EveryPaletteKeyIsARegisteredName`
    could not have been written before it**, and it now covers every category and chip.
  - **Arming and lighting are one row now.** `MarkArmedTool` was a hard-coded chain of key
    comparisons a hundred lines from the table that armed them: two lists of the same three tools,
    and the symptom when they drifted was not a compile error but *a chip that arms a tool and never
    lights*, which a player reads as the click having missed. Adding cancel would have been the
    fourth tool and the first drift. A `PaletteTool` carries arm, is-armed and wants-material or it
    does not exist.
  - **Right-click puts the tool down** — input case 5 of `09-ui-and-input.md` §6, and the first half
    of it built. The rig mirrors, for the right button, the click-versus-drag split the left button
    has always made: travelled means it was an orbit, and only a press that never travelled disarms.
    **With nothing armed it is deliberately inert**, because that gesture is reserved for the
    forced-order context menu. It is *not* routed through `SettingsDirector.Escape`: Escape unwinds
    tool → panel → menu, right-click means exactly one thing, and merging them would recreate the
    two-components-one-key fault that rule was written for.
  - **The arithmetic came out of the rig** as `PressGesture`, UnityEngine-free, because the PlayMode
    harness still cannot deliver a synthetic mouse — the same lift that made `DesignateDirector`
    testable. **And it immediately caught a real trap**: `new PressGesture()` runs the implicit
    all-zeroes struct constructor rather than one declared with optional parameters, so a threshold
    held as a *field* was zero exactly where it was used and every press read as a drag. Three tests
    failed on the first run. The threshold is a constant now and the type no longer allows it. This
    is the whole argument for the lift in one incident: inside the rig it would have shipped, and
    the symptom would have been "right-drag puts my tool down", diagnosed as a threshold that wanted
    tuning.
  - **Verified:** fast tier **494 Sim + 187 Hud** (17 new), Unity EditMode **1138 total, 0 failed**,
    both content gates green. Deconstruct, and the save gap underneath it, are PR 2 —
    `docs/design/16-cancel-and-deconstruct.md` §4 and §5.

- **Deconstruct, and the save gap that had to be closed before it could be honest (2026-09-17,
  `claude/cancel-tool`).** The owner, playing the cancel tool: *"this works good"*, then *"also a
  deconstruct button as well"*. The button was four lines of palette table. It was blocked on
  something else entirely, and the plan's §4 had said so as a prediction rather than a fact.
  - **The prediction was measured first, and both halves held.** `EdificeRoundTripTests` was written
    to fail before a line of deconstruct existed: a wall a colonist raised on a bare board took
    handle 0, and after a reload **the restored list had no entries at all** — the cell still said a
    wall stood there and pointed at nothing. And a **wooden wall and a stone wall in the same cell
    hashed identically**. `List<PlacedEdifice>` was worldgen's and nobody else's: not an
    `ISaveable`, not an `IStateHashable`, while `CellGrid` faithfully saved and hashed an *index*
    into it. The OQ-50 shape one level down, found the same way.
  - **Where the fix is wired was the only real decision.** `AddColony` creates and hashes
    `EdificeSaveSection`; `ConstructionGrid` takes it instead of the raw list and hands it on as
    `.Edifices`, because that is the one class that appends to the list at run time. So the thing
    that raises a wall and the thing that writes it down cannot be wired up separately.
    **The guard against forgetting the save is not vigilance**: the list is in the hash, so
    `WorldRoundTripTests.TheRoundTripReproducesTheStateExactly` fails the moment the save stops
    covering what the hash covers. Hash coverage plus round-trip equality *is* save coverage.
  - **The shape was chosen partly because the editor was open.** Threading a new parameter through
    `AddColony` would have touched 16 call sites, 11 of them in `Assets/Editor/`, which only Unity
    compiles — unverifiable while the owner was playing. Going through `ConstructionGrid`'s
    constructor touched one. The better design and the workable one were the same design, which is
    luck worth noticing rather than a method.
  - **The whole list is saved, not just the colony's additions.** The generator's stamps could be
    recovered by regenerating from the seed, which is smaller and makes every save file depend on
    the generator never changing. A save that describes itself survives a worldgen edit.
  - **All six golden hashes moved twice in one day, for two different reasons**, and `Golden.cs`
    carries both sentences. First when the edifice list entered the hash — including the barren
    meadow with nothing standing on it, because an empty list still contributes its count. Then
    again when deconstruct added a **tenth job**: `JobSystem` hashes a completed-and-failed tally
    *per job*, sized from the job table, so one more zero in that walk moves the **pre-tick**
    number. The failure message points at the generator and the generator was untouched. Anything
    changing the *length* of a hashed per-job or per-work-type array will do this.
  - **"Ours only" was not expressible**, which the plan had predicted and the code confirmed in as
    many words: `ConstructionGrid.Raise`'s own comment says a colonist's wall and the generator's
    are indistinguishable downstream, **on purpose**. `PlacedEdifice.Built` is the exception that
    one operation needs. A flag rather than the free proxy — `IsBuildable(stuff)` would have worked
    today and would have started including city walls the day a salvage line gave steel an item —
    and it is the same bit Reclaim will flip when it lands.
  - **The refund is keyed on cell *and tick*, unlike stone yield, and the difference is the point.**
    Stone is a property of the rock and must answer the same for ever. A refund keyed on the cell
    alone would make every cell permanently a "2" or a "3": stable, discoverable, then farmable by
    rebuilding the good ones. `TheSameCellCanRefundDifferentlyAtADifferentMoment` is that argument
    as a test.
  - **The falsification probe found a second fault nobody was looking for.** All eight new tests
    passed first time, which on this project is a reason to check rather than to celebrate. With
    `DeconstructWorkGiver.TryGiveJob` stubbed to refuse, the end-to-end test failed as it should —
    and `TheRefundIsPaidInWhatTheThingWasMadeOf` went on **passing**, because it guarded its own
    claim with `Assume` rather than `Assert`. Inconclusive reported as green: the state-hash defect
    in miniature, in a test written the same hour by someone who had just written the journal entry
    about it. It is an `Assert` now.
  - **Verified:** fast tier **505 Sim + 189 Hud**, Long tier **19**, Unity EditMode **1158 total,
    0 failed**, both content gates green. Unplayed: deconstruct, right-click and the pinned row.

- **U27 Materials: the formula got its second term, and only one live stat to give it to,
  2026-09-17.** `StuffDef.workFactorPerMille` had no offset beside it — `stat = base × factor`,
  not `+ offset` — so a-04 §3's own formula was half-built. `workOffsetTicks` is the missing
  term: a plain integer in the base stat's own unit (ticks), added after the factor rather than
  folded into it as a second per-mille figure, because a fraction added post-multiplication would
  just be a factor with extra steps. `ConstructionContent.WorkFor` now reads
  `workToBuild × workFactorPerMille / 1000 + workOffsetTicks`, floored to one tick over the whole
  sum rather than the factored term alone.

  - **Wood gets 0, stone gets 15 — Odyssey's own numbers, not the reference's.** The offset
    stands for a cost the factor cannot express: a flat dressing-and-fitting pass a stone block
    wants regardless of the wall's own size, where wood's per-unit factor already tells its whole
    story (a-04's own wood row carries no offset either). 15 ticks is a tenth of the wall's own
    135-tick base — proportionate to the one factor Odyssey chose (1.7×) — rather than copied
    from a-04's stone rows, which stack a much larger flat offset (+140 on the same 135-tick base)
    across five or six stone materials each carrying its own steep ×5–6 factor. A stone wall now
    costs **244 ticks** (229 factored, +15), not 229.
  - **`hitPointsFactorPerMille` was left factor-only, on purpose.** It has carried a factor since
    U26 with nothing consuming it — no `BuildingDef.maxHitPoints` exists, and nothing gives a
    built wall a damage state — so giving it an offset now would add a second unused field with no
    test able to exercise it. That is the durability stat's job when it lands, not this one's to
    invent ahead of it.
  - **Two materials, not three.** The plan allows "two or three"; the natural third is already
    named in the code (`ConstructionContent.IsBuildable`'s own comment: a salvage line that gives
    concrete, steel or composite an `item` puts it on the menu with no other change) and is
    deferred to U28 on purpose, where mining's salvage line is what would actually produce it and
    a second building would give its own factor and offset something to be tuned against. Inventing
    a third material's numbers now, with one buildable thing and no source for it, would be tuning
    against nothing.
  - **The gap `ConstructionContentDefTests` closes.** `ConstructionContent`'s own class comment and
    `Buildings.xml`'s own file header both already read as if the XML mirror and the in-code oracle
    were held together by a test — neither existed. `ConstructionContentDefTests` is that test now:
    a field-by-field comparison between the two (the arrangement the comments already described)
    plus a pinned fingerprint over each table, the same double guard `WorldContentDefTests` uses,
    with the same control proving the walk actually reaches `workOffsetTicks` rather than passing
    by reaching nothing.
  - **Verified:** fast tier **504 Sim + 171 Hud** (10 new: 3 in `ConstructionTests` isolating the
    offset from the factor and the floor from both, 7 in the new `ConstructionContentDefTests`).
    Not run: the Unity tier — no Unity in this environment; it runs on the owner's self-hosted
    runner via CI on the pull request.

- **Starting skills, U37, 2026-09-17.** A colonist now spawns with a rolled level in every
  skill instead of a flat zero — the gap `CLAUDE.md` had flagged as blocking `U40`'s
  candidate cards, since three colonists with identical zeroes are nothing to choose between.

  - **Where the roll had to live was the actual problem, and it was found by measuring rather
    than by reasoning about it.** The plan's done criterion is that the roll moves every
    `Simulated` golden and no `Generated` one. `RollPassions` is called from
    `ColonyScenario.Place`, which runs inside `ColonyWorld.Build` — and `Build` is exactly
    what `GoldenMasterTests` hashes for `Generated`, before a single tick runs. Rolling
    skills the same way `RollPassions` does would have moved `Generated` too, which the plan
    explicitly does not want. So the roll lives in a new one-shot system,
    `StartingSkillsSystem`, that fires from inside `SimWorld.Tick()` the first time
    `CurrentTick` reads zero — after `Generated` is taken, before `Simulated` is, for any
    case that runs at least one tick. Confirmed by rebaking with `ODYSSEY_REGOLDEN=1` and
    reading the printed pairs before pasting anything: all three cases' `Generated` values
    came back byte-identical to what was already committed, and all three `Simulated` values
    moved. No per-pawn "already rolled" flag is needed — the guard is `CurrentTick == 0`,
    which a fresh world only ever satisfies once and a loaded save (restored to a non-zero
    tick) never satisfies again.
  - **The distribution is invented and said so where it lives.** Nothing in
    `docs/research/` or `docs/design/` pins a starting-skill spread — clean room, no
    RimWorld number to take — so `PawnKindDef.startingSkillLevelWeights` is a new Def-driven
    table, `{ 40, 20, 14, 10, 6, 4, 3, 2, 1 }` over levels 0–8, weighted toward a low
    baseline (mean 1.16) with a thin tail (levels 6–7 together are a 2% roll) so an
    occasional colonist starts competent rather than every one of five arriving identical.
    Marked INVENTED in both the C# field and the XML, per the clean-room rule, and rolled
    from its own stream (`PawnPurpose.StartingSkill`) rather than sharing `Passion`'s salt —
    the file's own comment on `StoneYield` already warns what sharing one does.
  - **The one-shot roll collided with a testing convention already in use, and the fix was
    to make the roll idempotent rather than the tests fragile.** Several existing tests
    build a `ColonyWorld`, poke a pawn's `Skills[]` directly, then call `Tick()` once to
    observe behaviour on that very tick — `ThePublishedFrameCarriesEveryColonistsSkills`,
    `DecayRunsInTheColonyTheGamePlays`. Because that first `Tick()` is also the roll's only
    chance to run, it was overwriting the value the test had just set. Fixed in
    `Pawn.RollStartingSkills`: the draw is always made, so a later skill's roll never
    depends on which earlier ones happened to be preset, but the result is only written into
    a skill still holding the constructor's zero. In the game every skill is zero at that
    point, always, so nothing changes there; in a fixture that decided a skill for its own
    reason, the roll leaves it alone. `FellingGrantsCuttingExperiencePerWorkTick` needed a
    real fix rather than a guard, because its premise ("nobody but the cutter has any cutting
    experience") is no longer true once starting skills are nonzero — it now ticks once to
    let the roll fire, snapshots every colonist's starting figure, and asserts deltas from
    that baseline instead of absolute zero.
  - **Content fingerprint moved on purpose.** `PawnKindDef` gaining a field moves
    `PawnContentDefTests`'s pinned fingerprint by construction; updated with the reason
    recorded beside it, not silently.
  - **New coverage, direct rather than only hash-shaped:** `StartingSkillsTests` proves the
    zero-before-any-tick state outright (not inferred from a hash not moving), that the roll
    is deterministic on `(seed, pawn id)` and nothing else, that it never re-fires on a later
    tick even when a skill is forced back to an arbitrary value, that it lands in the state
    hash, and that a rolled level survives a save/load round trip — including the case where
    a colonist has since earned real experience on top of the roll and loading must not
    disturb it.
  - **Verified:** fast tier **502 Sim + 171 Hud**, Long tier **19** (`ODYSSEY_TEST_ALL=1`:
    **521 Sim** total). Both content gates `--check` clean — this is a tuning constant, not
    player-visible named content, so neither the wiki nor the label registry needed a
    rebuild, and both confirm nothing went stale regardless. **Not verified:** the Unity
    tier, which this container cannot run; it is CI's job on the owner's self-hosted runner.

- **U36 Save format v2, and files on disk, 2026-09-17.** Sim-only, per the plan: `WorldSave` gained
  a `SaveRecipe` — map type, scenario, colony name, day — written into the header after the
  seed/size/tick it always carried, and `CurrentFormatVersion` moved to 2.

  - **Day is handed in, not derived in Sim.** `GameClock`'s tick-to-calendar mapping lives in the
    Hud assembly; `Odyssey.Sim.csproj`'s own comment says referencing only `Sim.Contracts` is what
    keeps the dependency direction enforced, and Hud is not on that list. So `SaveRecipe.Day` is
    whatever the caller — who already has both the tick and the clock — computed, and nothing in
    Sim re-implements the conversion. `ColonyWorld.Recipe(day)` fills in map, scenario and colony
    name from `Request` and still asks the caller for the day, for the same reason.
  - **A version 1 file reads back `SaveRecipe.Unknown` rather than guessing.** `MapType` gained a
    third value, `Unknown = 2`, specifically so a file that never recorded a map type does not have
    to borrow either real one to say so. `ReadHeader` is the one place that knows the layout differs
    by version, and both `Load` and the new `ReadHeaderOnly` go through it, so they cannot read an
    old file two different ways.
  - **The header-only read is the point of the exercise.** `WorldSave.ReadHeaderOnly(Stream | path)`
    needs no `SimWorld` and no component list — it reads magic, version, seed, size, tick and the
    recipe, then stops, never touching a section. That is what lets a load screen list a folder of
    saves from their headers alone, which is what the unit was for.
  - **`SaveToFile` / `LoadFromFile` exist because Sim cannot read
    `UnityEngine.Application.persistentDataPath`.** Both take a plain path and do nothing else with
    it — no `Saves`-folder convention, no extension, no enumeration policy. Deciding where that
    folder lives and listing what's in it is presentation's job (`U38`–`U40`), not this unit's.
  - **Every existing three-argument `WorldSave.Save(world, stream, components)` call site still
    compiles**, because the recipe is an optional fourth parameter; it now writes a version 2 file
    carrying `SaveRecipe.Unknown`, which reads back identically to an old file that never had one.
  - **Verified:** fast tier **525 Sim + 191 Hud** (10 new, `SaveFormatV2Tests`: the recipe round
    trip, the no-recipe-given default, a hand-built version 1 fixture read both header-only and
    through a full `Load`, the truncated/non-Odyssey failure modes on `ReadHeaderOnly`, a
    file-path round trip, and `ColonyWorld.Recipe`). Long tier unaffected (19). Wiki and label
    registries current (no content changed). Not run: the Unity tier — no Unity in this
    environment; it runs on the owner's self-hosted runner via CI on the pull request.

- **Forced orders, steps 1–2: the intent and the legality split, 2026-09-17.** Sim only, and the
  simulation half of `15-building.md`'s "Forced orders and the context menu". A player who wants
  *that* wall built *now* still cannot say so — the two steps that would let them say it are
  presentation's, and are not started — but everything under the words is built and tested.

  - **`Job.PlayerForced` has been saved and hashed since the job record was written, and nothing
    read it.** It is read now, and by exactly one thing: `JobSystem.HandleForceJob`. A forced order
    is emphatically not a new kind of job — same def, same `BuildJobDriver`, same toils, same
    reservation — it is the scan bypassed and the job pushed onto a named colonist.
  - **`ForceJob(cell, A = job, B = pawn)` is the first intent that names a pawn**, and it has to be.
    Every other command is about a cell and leaves *who answers it* to the work scan; a forced order
    is the player overruling that scan for one colonist, so the colonist is half of what is being
    said.
  - **The order of operations is the whole of "an illegal order changes nothing".** Legality is
    asked before the colonist's current job is touched, and the query claims nothing, so a refusal
    cannot leave a colonist idle, a cell claimed, or an order half given. When it is legal, the
    current job ends as a **failure** — which is how the mental-break interrupt twenty lines above
    already takes a job away, because failure is the ending that releases what was held and this
    class has exactly one release path on purpose.
  - **The real work was the split, and it was worth doing for a second reason.** Every giver decided
    legality and claimed its target in one pass, so the only way to ask "could this colonist build
    that" was to do it. `BuildWorkGiver.CanBuild(pawn, ctx, site, out stand)` answers it and reserves
    nothing; `JobSystem.CanForce` is the entry point a menu asks, switching on the job def with a
    default of no — a job nobody has decided the forced meaning of should not acquire one by
    omission. The A10 command grid needs the same split to grey a command out, and the context menu
    has to be built *before* the player chooses anything.
  - **It is the scan's own test, not a second opinion beside it.** `BuildWorkGiver.TryGiveJob` calls
    `CanBuild` for each candidate, so the offered path and the forced path cannot drift into
    disagreeing about what a colonist may build — the fault that would show up as a menu offering a
    command the colonist then refuses. Extracting it moved the cheap distance test ahead of the
    world reads, which cannot change the winner: a candidate that fails the legality test never
    moves `bestDistance`, so the order the two are asked in cannot decide anything. No golden moved,
    and none should have.
  - **`ConstructionGrid.SiteAt` is an extraction, not a second lookup.** The ground-to-site lift —
    a click names a surface, an order names a cell — was two lines inside `Cancel`; a forced order
    needs the same answer, and the menu will too. One copy, and `Cancel` now asks it.
  - **What the query deliberately does not answer is *why* not.** A greyed command wants a reason,
    and "nowhere to stand" and "somebody else has it" are not `IntentRejection` values. Inventing
    that vocabulary now would be inventing it for no reader; A10 is the first thing that can display
    one.
  - **The test that would have proved nothing, and what was written instead.** "The forced colonist
    started building" passes with the feature deleted, because the scan would have started a build
    anyway. So there is a measured control — `TheScanChoosesTheNearerSiteWhenNobodyForcesAnything`
    asserts the colonist really does take the nearer of two identical frames — and the claim is then
    that the far wall goes up **with the near one still standing**. Both were checked by mutation:
    stubbing `HandleForceJob` to refuse everything fails two tests, and making the query `Reserve`
    instead of `CanReserve` fails two others.
  - **A fixture that silently proved nothing, caught the same way.** The unreachable case walls a
    site into a sealed pocket, and the first version was reachable anyway: `NavGraph.Rebuild` walks
    dirty blocks and returns immediately when none are, so an edit that never called `MarkDirty`
    left the region graph certain the wall was not there. The test skipped rather than failed,
    because the guard was an `Assume`. Those guards are `Assert`s now — a fixture claim that
    load-bearing should fail loudly, not opt out.
  - **Verified:** fast tier **542 Sim + 191 Hud** (nine new, `ForcedOrderTests`), Long tier **19**,
    both content gates `--check` clean — this is plumbing, not named content, so neither the wiki
    nor the label registry had anything to rebuild. **Not run:** the Unity tier, which this
    container has no Unity for; CI's self-hosted runner does it on the pull request.
  - **Next, and explicitly not started:** step 3, separating a right-*click* from the camera rig's
    orbit-*drag* (half of it shipped with the cancel tool — right-click already puts an armed tool
    down, and is deliberately inert with nothing armed, reserved for this), and step 4, the menu
    itself. Nobody has pressed Play on a forced order, and nothing in the running game can send one
    yet.

- **U39's screen is blocked on U38 alone; its seed logic landed on its own merit (2026-09-17,
  `claude/exciting-albattani-4ua5z2`).** A session was sent to build U39, the new-game screen, and
  told to verify its dependencies against the code rather than the plan — because
  `docs/plans/vertical-slice.md` had already been caught ahead of the code once this week. Worth
  doing: at the moment the branch started, **U36 and U37 were both still open** and the plan's
  table did not say so. By the time it was pushed, both had merged into `main` (PR #87, the same
  afternoon) — `SaveFormat.CurrentFormatVersion` is **2** and `StartingSkillsTests` exists — so
  the check that mattered was the one that was re-run at the end rather than the one at the start.
  **`U38` is the remaining blocker and has not moved:** `HudShell.Bar.cs:562` still carries the
  comment that "the game menu it will one day belong to (B18) does not exist yet", and there is no
  menu panel anywhere in the presentation assembly. There is nowhere for a new-game screen to
  attach, so none was built.

  **What was built instead, and why this and nothing else.** `SeedEntry` in `Odyssey.Sim.Contracts`:
  draw a seed, reroll to a seed that is guaranteed different, format one for the player to read,
  and read back what they typed. That is the whole of U39 that does not depend on a screen, and it
  is the half that would otherwise have been written inside a text field's callback where the fast
  tier could never reach it. **Nothing was invented to route around U38** — no standalone menu —
  because a second source of truth for the shell is exactly what `CLAUDE.md` warns about while a
  seam is open.

  Four decisions worth not re-litigating:
  - **Decimal digits, not hex and not a word code.** It is already the form the project prints a
    seed in (`OdysseyBootstrap`'s cast-seed log line, the `colonistLookSeed` inspector field), so a
    number copied out of a log is a number that can be pasted back in, and it needs no vocabulary a
    player has to be taught. **The Minecraft idiom — type a word, have it hashed — is the genre's
    friendlier convention and was deliberately not taken**, because it only works if the typed text
    is kept beside the number. That is a `SaveRecipe` field, and now that U36 has landed it is a
    question that can actually be asked; it was not one while this was written.
  - **Zero is an ordinary seed**, so nothing special-cases it. `DeterministicRandom`'s constructor
    does map a state of 0 to 1, but no consumer ever reaches it that way: every stream comes from
    `ForTick`, which avalanche-mixes first (checked — there is no `new DeterministicRandom(` in
    `Sim` or `Presentation` outside `ForTick`). A test pins it, because a start screen showing "0"
    would otherwise be lying about which world it was about to build.
  - **A reroll may not return the seed it replaced**, and the guarantee is absolute rather than
    probabilistic. One in 2^32 is invisible in play but reads as a broken button when it happens,
    and the player cannot tell the two apart. The retry is bounded at eight draws and then takes
    the neighbouring seed, because an unbounded loop against a stuck source would hang rather than
    fail — and a source stuck on one value is exactly what a test double is.
  - **The one deliberately non-deterministic code in the simulation assemblies**, confined to
    `Draw()` and `Reroll()` and reachable from nowhere on a tick path. Entropy is `Guid.NewGuid`,
    folded FNV-1a and then put through `ForTick`'s own avalanche rather than truncated: six of a
    GUID's bits are fixed version and variant markers, and a truncation could land on them and
    quietly narrow the range of seeds a player can ever be dealt. Two tests cover it without
    flaking — 256 draws must yield at least 252 distinct values, and every one of the 32 bit
    positions must be seen both set and clear (a uniform bit is stuck by chance with probability
    2^-255).

  **Not run: either tier, and that is a real gap rather than a formality.** This container has no
  Unity *and no .NET SDK*, and the network policy answers 403 to `builds.dotnet.microsoft.com`,
  `dotnetcli.azureedge.net` and `download.visualstudio.microsoft.com`, so `scripts/test-fast.sh`
  could not be installed into, let alone run. The 17 tests in `SeedEntryTests` are written and
  unexecuted; CI on PR #89 is the first thing that will run them. The entropy fold's two
  statistical claims were modelled in Python against the same constants and held over five trials
  of 256 draws, which is not the same as running the test. The fast-tier counts in `CLAUDE.md` are
  deliberately left alone rather than advanced by a guess.

- **The tile answers, 2026-09-17.** Three owner reports, one session: a wood pile read as a
  generic placeholder with no amount, rocks could be clicked but not told from grass, and a water
  tile did not say it was water. Behind all three sat the same fact: **U16 "cell inspection" was
  marked done with M1 and only its click plumbing had landed** — the pane's own state line for a
  bare cell was the literal string "cell readout arrives with cell inspection", a promise shipped
  as a placeholder through two milestones and one M1 report claiming the unit existed and was
  tested. The readback's criteria (terrain, floor, edifice, support) were never built because no
  queue row and no milestone gate asked for them again; the M1 report measured the plumbing.
  - **The seam chose itself, mostly.** A layer-shaped terrain channel was the tempting widening —
    the slice channel already publishes one byte per cell of the active layer — and it is wrong for
    the same reason `OrderView` was widened: a click reaches above the slice (the outcrop the
    owner could see and not mine), so a layer-shaped answer either misses those cells or costs the
    whole drawn band every frame. What shipped is the `PawnAspect` shape applied to cells: **a
    sparse `CellDetail` row for the one asked-about cell** — terrain, edifice, floor stuff,
    support, work to clear, and the crossing cost in thousandths of a clear crossing — written by a
    sim-side contributor every colony gets through `ColonyComposition`, asked by a `QueryCell`
    intent, withdrawn by one. ADR 0004 gained its second amendment for the per-cell half.
  - **The crossing cost is worldgen's own number, restated once.** `NaturalContent.ApplyCostClasses`
    owns "+40 is boggy, +200 is wading" and fills the contributor's table the same way it fills the
    nav grid's; the pane reads 1000 / 1400 / 3000 — thousandths, the ratio a player reads — so
    there is one source for the addends and no second copy anywhere to drift. Zero is published as
    *cannot walk* for impassable water and for a cell with nothing to stand on, which is a
    different answer from slow and not an extreme of it.
  - **The paused world was the discovery of the session.** ADR 0004's Decision says "intents flush
    while the clock is paused", and the implementation honoured that exactly never: a paused world
    spends a tick only for a speed change, everything else queued waits for an unpause. Tolerable
    for commands (an order given while paused applying on unpause is sensible) and wrong for the
    first intent that is a question — inspecting a stopped world is when inspection happens. The
    answer is `SimWorld.RepublishViews`: apply the queued view intents and publish over the same
    settled world — no tick, no system, no hash, the counter unmoved. It is safe because a
    question owns no state, which is also why `IntentBus.DrainWhere` is documented as being for
    view intents and nothing else: a command applied off-boundary is a replay that cannot be
    reproduced.
  - **Two picking bugs fell out of the same reports, and both were ownership questions rather
    than arithmetic.** Water fills its cell but is not solid, so a pond click reached the bed and
    the block-below rule answered with rock the player cannot see — water owns its own floor now,
    with a slab still checked first (a bridge is walked on, not waded through). And a pile on bare
    ground sits in the air cell above the solid block the picker resolves to, so `ThingAt`'s exact
    match missed every pile not on a built slab — the director looks one cell up now, and the
    direct-cell match still wins where it applies.
  - **Content moved a little, by the wiki's own rules.** Three icon keys were added (packed
    gravel, the two trees — `ui.terrain.*`, M3); ore terrain reuses the resource's own name
    ("Iron ore" on a rock face is what the player needs to read there); the pane's hard-coded
    "Salvage" became **Scrap**, the word the ledger and the wiki already settled on; and the
    city's finished surfaces (pavement, cracked pavement, soil, engineered fill, the buried seam)
    are deliberately unnamed until the city map is loadable — the registry test tolerates blanks
    and nothing invents content to describe tiles a player cannot click.
  - **Measured:** fast tier **570 Sim + 209 Hud** green, Unity EditMode **1242 passed, 0 failed**
    of 1251, including a board-wide test that clicks every water column on the played map and a
    paused-world test proving the answer arrives with the tick counter unmoved and a queued
    command still queued. **Not measured: nobody has pressed Play.** The readout's judgement
    calls — walk speed shown at 100% rather than only when abnormal, support always said, the
    minable clause honest about grass ("about 1s of work") — are exactly the kind of thing the
    owner vetoes from a keyboard, and this pane is now verbose enough to be worth vetoing.

- **The readout's first playtest, 2026-09-17.** The owner pressed Play on the tile answers within
  the hour and came back with three reports, which is the loop working.
  - **Meals and scrap showed no cursor.** The item bracket was drawn at the cell the *pick*
    resolved to — the solid block under a pile on bare ground — so the bracket sat inside the
    ground, and a no-art module fell through to a ground highlight that never read as "around the
    object". The thing's own cell is in the snapshot the cursor routine already holds; the bracket
    and the fall-through both draw there now.
  - **The tile window reshaped.** Half the width, one fact per row in two fixed columns, order
    first, then minable, walk speed, floor, support — the same fact always in the same place,
    where the joined line made every number hunt for its label. The model's readout became rows
    (`InspectModel.CellRows`) and the pane a narrow variant (`inspect--narrow`, half the constant
    width, pinned like every other anchor); the colonist pane keeps its full width, because its
    tabs, needs and skills were sized for the band.
  - **The readout visibly skipped before settling.** The question was submitted *after* the
    selection changed, so the pane's first refresh painted the unanswered frame — "Ground", or
    the previous tile — and the answered one arrived a refresh later. The fix is ordering, not
    caching: the question is submitted and the view republished *before* `Pick` runs, because
    `Pick` raises the change whose handlers read the frame. One click, one paint, the right
    answer — and the same republish that serves the paused world serves the running one.

- **The readout audited for scale, 2026-09-17.** The owner asked whether the tile answers would
  hold up as the game grows, so every path the feature touches was walked and the two that matter
  were measured rather than argued.
  - **Measured, on the scale target** (250 × 250 × 40, fifty pawns, a question standing every
    tick): the answered publish — the Snapshot phase, which is also exactly what one click's
    `RepublishViews` costs — runs at **0.019 ms mean, 0.026 p95**, statistically identical to the
    no-question arm's 0.021 and 2.4% of the 0.8 ms publish budget. Allocation in the paired
    same-process measurement: **3.3 bytes per tick with the question standing against 3.3
    without — the standing question adds 0.0** over 5,000 ticks, no collection. The arm that
    produced this is `TickBenchmarkTests.TheColonyAnsweringACellQuestion`, and the guard that
    keeps it true is `PathAllocationTests.AStandingQuestionCostsTheTickNothing`, because the
    publish phase is allocation-free and a row-a-tick of heap traffic would be sixty objects a
    second the GC owned for one frame each.
  - **Walked, and O(1) in the board everywhere.** The contributor is one bounds check when
    nobody is asking and about eight array reads plus one terrain-table lookup when they are —
    the cost does not move with 2.5 million cells, because a question is about one of them. The
    pane at 15 Hz scans a channel of at most a handful of rows and rebuilds a row string only
    when the printed value changes (pinned by test, ADR 0003 F1). The picker's water rule is two
    comparisons a floor-crossing; the director's second look is one more scan of tens of things
    per click.
  - **Expansion is guarded at the edges.** The three label tables are held to their contract
    counts by `RegistryTests`, so a terrain or edifice or commodity added to the simulation
    cannot reach the pane unnamed — the fast tier says so. The contract's handles are welded to
    the simulation's tables by test on both sides. And the channel already fits design 09's
    32-subject selection-detail bound without change.
  - **The one thing to watch, written down where it will be looked for:** a hover tooltip (A13)
    is the one future consumer that asks questions *per hover* rather than per click. A
    `QueryCell` plus a republish per hover-frame would be sixty publishes a second and is the
    wrong shape — that feature wants a throttled question or a presentation-side read of the
    mirror, and ADR 0004 amendment 2's flip condition is where the decision is recorded.
- **U38, the start screen, 2026-09-17.** Design first (`docs/design/17-start-flow.md`), then an
  interview, then the code — and the interview is why this unit is not what the plan said it was.
  `vertical-slice.md` described an in-game menu that Escape would unwind into; the owner's answer
  was one sentence, *"There is already an in game menu/settings - reuse that"*, and it changed the
  shape of everything after it. **The plan's row had been written from the panel catalogue rather
  than from the screen**, and the screen already had a Menu popover and a settings panel with an
  exit row. So no second in-game menu was built, Save/Load/Quit-to-main-menu joined B17 beside the
  exit row, the quit half did *not* move out of B17 as the catalogue said it would, and
  **`SettingsDirector.Escape` gained no case at all** — the thing the plan named as U38's central
  change turned out to need no change. Both documents were corrected in the same branch rather than
  left to argue with the code.
- **What U38 actually is: the screen before the game**, and the project's first true modal.
  `09-ui-and-input.md` §6 case 5 — "a modal swallows every pointer and key event except its own
  dismissal" — had been specified since the interface was designed and had never had anything to be
  true of. **The mechanism is deliberately not a flag.** A pickable element covering the viewport
  sits under the panel and over everything else, so `PointOverUi` — which asks the panel what is
  under the cursor — answers "the interface" everywhere, and the camera rig already declines a press
  it is told belongs to the interface. The two always-on contrast scrims are explicitly *not*
  pickable, for the mirror-image reason, which is what made this one obvious. The alternative, a
  modal flag consulted in every input path, is the version that grows a case somebody forgets.
- **The consistency the owner asked for is enforced rather than remembered.** The instruction was
  that the new screen keep the existing interface's style *and that this be centralised in the
  work*. Four seams already did that job — the `Panel`→`Window`→`Popover` factory chain, `HudTheme`
  checked against `Hud.uss` by a test that parses the sheet, `HudType`'s closed six-step scale, and
  `Registry.Label` over the content CSV — so the screen was added *to* each rather than beside it:
  `Modal()` became the fourth link, the scrim became one token, and the title uses the existing
  `Name` role because the scale is closed and a seventh step fails the fast tier. The screen's rows
  are `.settings__row`, the same row the stores panel and the Menu popover use. **The one new seam
  is `SessionCommands`**, the row set as data — key, context, order, and whether it asks twice — so
  the start screen and the settings panel cannot drift apart. It is the bargain `HudCommands`
  already makes for the command bar, and `PaletteTools` for the build palette.
- **Ask-twice had two owners for about an hour, and the table is what found it.** `SettingsDirector`
  had the exit row's two clicks written into `RequestExit`; adding three more destructive rows would
  have meant the rule stated twice, in the file whose whole job is to have one. It reads the table
  now, and `ExitArmed` became `ArmedRow` — one armed row at a time, so pressing Load while Quit is
  armed stands Quit down. A screen with two rows both asking "are you sure?" is a screen where the
  second press lands on whichever one the hand reaches first.
- **`HudDirectors` stopped building the settings and hotkey directors and started taking them.**
  Nothing in either is a fact about a colony; both are preferences about the machine. They were
  built and thrown away per session, which was harmless until a screen existed that runs with no
  session — the start screen's Options row would have opened a panel nothing drove. Two instances
  would have been the other way out and the wrong one: two answers to "how large is the interface"
  is a setting that appears not to stick.
- **Save format 3, and the bug is a good example of what a state hash cannot see.** U38's
  round-trip test — build, run, save, tear down, rebuild *from the header alone*, load, compare —
  passed, and then its own follow-up question did not: `SaveRecipe` carried `MapType`, and
  `MapType.Natural` is three genuinely different boards depending on `Barren` and `Wooded`. So a
  loaded colony was rebuilt on the wrong board. **The hash agreed because `GridSaveSection` writes
  every cell of every field and the wrong board is entirely overwritten.** What is not overwritten
  is everything worldgen returns *beside* the cells: measured on one seed, the wooded board starts a
  colony at (25,22,L11) with 34 cells marked for work and the default board gives (30,30,L11) and
  none — and the camera frames a loaded colony on that start cell, so a restored game opened on
  empty ground a third of the map from the colony it had just restored. Two bools and a format bump;
  versions 1 and 2 still load, with a hand-built version 2 fixture proving it the way the version 1
  fixture already did. **The general lesson: a round trip that compares hashes proves the cells
  survived and says nothing about what generated them.**
- **`buildOnPlay` defaulting to false is the behaviour change, and the PlayMode tier is where it
  landed.** Nine rigs assumed `Start` builds a world. They now say so in one line each, which is
  better than inheriting it — what they were really asserting was "a session exists". Two PlayMode
  rules genuinely bent and were amended with their reasons rather than loosened: the start screen's
  root carries no close X, because a window with nothing behind it has nothing to close *to* and an
  X that does nothing is worse than no X (the exemption is a named list, and the test asserts the
  exempt window was actually on screen, so the hole cannot widen quietly); and `start` joined the
  framed-region roll call, since it is built whether or not a colony is.
- **Two NUnits, and the fast tier's is the newer one — two Unity runs to learn it.**
  `Assert.Multiple` does not exist under Unity at all, which is a *compile* error and aborts the
  whole batch before a single test runs, so it reads as a broken build rather than a test problem;
  and `Has.Count` throws `ArgumentException: Property Count was not found` against an
  `IReadOnlyList<T>`, while working fine on `List<T>` in the same file. Both are in
  `docs/lessons.md` now, together with the thing that makes them bite: **the fast tier compiles
  neither Presentation nor Editor**, so eleven green seconds say nothing about the composition root
  or the HUD shell.
- **Three agents ran in parallel on the leaves and the spine stayed single-threaded**, per
  `lessons.md`. Each owned its own new files and committed nothing; two of the three came back with
  findings that changed the design rather than just code — the recipe's missing board flags, and the
  ask-twice drift. Both were reported instead of patched, which is why they were fixed in the right
  place. One correction they surfaced in passing: `CLAUDE.md`'s "Known gaps" still claimed the
  designation grid is not saved, and it has been `ISaveable` and `IStateHashable` and in
  `SaveComponents` for some time. **A stale gap outlives its own fix and sends somebody to build a
  thing that already exists** — the line is struck through rather than deleted, so the correction is
  visible.
- **The owner played the start screen, 2026-09-17, and four things came back.** Worth recording as a
  set, because three of them were plainly right on sight and none of them was reachable by any tier.
  **(1) The worktree had no art** — not a design fault at all: `Assets/Synty/` is gitignored, so a
  worktree has none until it is junctioned, which `docs/lessons.md` already says and this session had
  not done. **(2) The camera and the view were not saved.** **(3) The settings panel appeared under
  the menu** rather than in its place. **(4) The panel resized between screens**, and the save rows
  could not be told apart.
- **The view is in the save now, and the rule that kept it out was being read too literally.**
  `CLAUDE.md` says nothing in presentation is in a cell, a save or the hash, and that rule is
  load-bearing — but **its purpose is determinism, and determinism is the hash's business, not the
  save's**. Where the camera is pointing cannot affect a tick. So there is a `"view"` section
  carrying the camera, the slice layer, the selection and the game speed, and what makes it safe is
  stated rather than assumed: it is `ISaveable` and **not** `IStateHashable`, so it cannot move the
  state hash, desync a load or appear in a determinism gate. Two properties that are not obvious:
  reading and applying are separate phases, because the section is read while the world is still
  being restored and the camera has not been pointed anywhere yet; and floats are written as exact
  bits rather than rounded, because **a camera that drifts slightly on every save-and-load round
  trip is a bug nobody notices for weeks and then cannot reproduce.**
- **Two centred panels stack, which is obvious once somebody sees it and was invisible in every
  test.** The settings panel and the start screen are both centred, so Options put one over the
  other and the pair read as a pile rather than as one screen showing what was asked for. Settings
  became a third *screen* of the menu — `MenuScreen.Settings`, with `Back()` as the way out, the
  same way out the load screen already had — and the scrim stays while it shows, because the state
  is still modal. **Closing the panel by any means returns to the menu**, driven by the panel rather
  than by the row that opened it: the ways out of that panel already existed, and this had to be all
  of them rather than the one the new code knew about.
- **A fixed panel, because a centred one that resizes moves every row under the pointer.** The
  screen sized itself to its content, so the menu and the load list were different boxes, and
  navigating between them shifted everything. `StartPanelHeight` is a constant now and the load
  list's ceiling is *derived* from the one body rather than written down beside it, so the two
  cannot disagree. The cost is air under the root screen's four rows, which is the cheaper of the
  two mistakes: the alternative is a list that scrolls at four.
- **A folder of saves is mostly repeated attempts at the same colony**, so "Ashford, Day 12" does not
  tell two rows apart — the owner asked for the date and time. It is formatted in `SaveFiles`, in
  the player's local time, and reaches `MenuDirector` as a **string**: formatting a date is a
  question about the player's machine, and the Hud assembly is compiled without any of that in
  mind. The panel widened 320 → 420 to carry the longer line rather than cutting a word, since the
  acceptance criteria allow an ellipsis on a colonist's name and on nothing else.
- **Saves are named and Save overwrites, 2026-09-17 — and the fault had already been written down.**
  The owner: *"I notice you keep saving a new game everytime. We should be able to name the save
  game (with a default) and then can overwrite that save if need be - otherwise lots of saves will
  be created."* `17-start-flow.md` §10 had listed exactly this under "things the code knows are
  unfinished" — *"Save always writes a new file… that is the right default for a prototype with no
  confirmation dialog, and it is not a policy anybody has chosen."* **A known gap written down is
  not a gap deferred**; it lasted one evening, which is about how long "nobody has chosen this"
  survives contact with somebody using it.
- **A session is bound to a file**, the one it was loaded from or last saved to, and Save writes
  over that. **Two rows rather than one prompt**, and the reasoning is the interesting part:
  prompting on every press does not multiply files and *does* annoy, because a player who saves
  often confirms the same name every time. Naming once and overwriting after is the ordinary case,
  so the ordinary case is one press and the exception — Save as — says what it is. The binding is
  cleared on teardown, because a new colony inheriting the last one's file would overwrite it on its
  first Save: the worst of both behaviours, a lost save *and* no prompt.
- **A player-chosen name is never disambiguated, and that reopened a trap the old scheme closed by
  accident.** The derived name always contained `-day-N`, and the author of the catalogue had
  noticed that this was what kept a colony called `con`, `aux` or `com1` off a **Windows reserved
  device name** — `con-day-4.odyssey` is creatable, `con.odyssey` is not. A typed name has no
  `-day-` in it, so the guard had to be put back deliberately. **A safety property that holds as a
  side effect of an unrelated decision is one you lose the moment that decision changes**, and the
  only reason this one was caught is that the person who found it the first time wrote down *why*
  it mattered rather than just fixing it.


- **The Build palette became three layouts, 2026-09-17** (`claude/build-palette-layouts`,
  `docs/design/17-build-palette-layouts.md`). The owner supplied a specification and three rendered
  mockups — 4a Rows, 4b Rail, 4c Bar — with 4a the default and the other two switchable from inside
  the panel. The whole design follows from one clause of it: *all three share one data source and
  one state object*. `BuildPaletteModel` in the Unity-free assembly holds the category, the
  sub-type, the per-sub-type material memory, the breadcrumb and the cost line, and the three layout
  builders in `HudShell.Build.cs` read it and decide nothing — so "switching layout never changes
  selection" is a property of the structure rather than a thing three builders have to remember, and
  it is checked in eleven seconds rather than by opening the game.

  **Dropping the Orders category would have hidden mining and chopping.** The specification takes
  Orders, Zones and Salvage off the palette, leaving exactly the seven it names. Zones and Salvage
  took nothing live with them; Orders held Mine and Chop, which are two of the five tools in this
  game that actually do anything. Taken literally it would have left both reachable by the `M` and
  `C` keys and by nothing a player could see — which is not a hypothetical, it is precisely what had
  happened to Cancel the day before: *the tool was never missing, every way of finding it was
  missing*, and it cost a playtest to find. Both are pinned in the panel header now beside
  Deconstruct and Cancel, on the test those two already passed — all four are verbs applied to what
  is already on the board, not nouns to place. `EveryLiveToolIsDrawnSomewhere` is the general form,
  so a third time cannot be silent.

  **The mockups were drawn over a bare board.** All three floated at a 28 px margin with the panel's
  corner in the screen's corner, and the real screen has the stores panel docked down the left edge
  and the roster strip across the whole top, so a panel there covers both while it is open. Asked
  which of three placements they wanted, the owner answered with a principle instead: *"tight and
  flush to other elements to enable full use of space"*. So the margins went, Rows and Bar span the
  screen edge to edge, Rail keeps its 840 anchored to the button that raised it, and all three dock
  flush on whatever is under them — the command bar, or the inspect pane's collapsed header. That is
  the rule the bar and the popovers already follow, and very nearly the words the owner used about
  the popovers a day earlier.

  **Thirty-seven icons are drawn rather than imported.** The specification asks for 1.8 px line art
  on a 24 px grid and forbids the placeholder square anywhere in the palette; the ADR 0007 pipeline
  covers nineteen keys and not one is an architecture tool, so every tile would have been an
  outlined box. They are `Painter2D` paths in `HudGlyph`'s existing 24-unit box, sharing its stroke
  rule and its helpers — one path each, no texture, no atlas, no licence — and the key is still the
  contract, so a sheet landing later takes the slot back with nothing moving. The materials are
  deliberately not among them: Wood and Stone keep the exact sprites the game already draws, which
  is the one tier the specification says not to touch.

  **Rail's only promise broke three times, and the test written for it found all three.** Its claim
  is that its height does not change when the category does, so nothing below it reflows. It did:
  376 px on Structure against 343 on Production. The sub-type grid was sizing itself to its
  contents; then, fixed, the material band was collapsing entirely for a category whose first
  buildable tool is not made of anything, which is five of the seven; then, fixed, the cost line was
  17 px shorter when empty. Each was found by printing the measurement rather than by reading the
  code — the second and third would both have read as correct. All seven now stand at 396 px, and
  the row count is held to the largest category in the fast tier so an eighth tool fails in eleven
  seconds rather than in a PlayMode run. The price is an empty band under the word MATERIAL while an
  order is armed, which is the honest cost of the promise and is visible only in Rail.

  **Mode colour, asked for mid-build** (owner: *"the cancel/deconstruct colours … should also be
  represented in the dialog … so it becomes clearer what mode you are in"*). Each of the four pinned
  actions has a hue — four existing signal tokens, not four new ones — and while one is held the
  panel wears it: the header line becomes that action's name in its colour, its icon appears beside
  it, and the panel's top edge becomes a 2 px hairline of it. The header line is a sentence to read;
  the edge is seen without reading, which is the half that answers the question. The breadcrumb is
  replaced rather than joined, because what the palette would have built is not what is about to
  happen. The floating armed banner is suppressed while the palette is open, since it said the same
  thing over the panel that had just set it.

  **One token departs from the specification, and the test is why.** The specified 0.30 disabled ink
  measures 2.71:1 against its own chip over the brightest terrain the game draws. WCAG exempts
  inactive controls, so nothing external said it was wrong; what says so is this palette at this
  moment — one of its twenty-seven sub-types is live, so the greyed-out state is very nearly the
  whole panel and is the only thing telling a player what the game will eventually let them build.
  0.35 measures 3.21:1. `HudTheme.SubTypeDisabledInk` records when to put it back, and the second
  half of the assertion stops the fix going too far: a disabled chip must stay obviously quieter
  than a live one.

  **Two general fixes fell out of it.** The ESC hint failed
  `NoLabelIsAThreeLetterPlaceholder` — correctly by the letter of that rule and wrongly by its
  meaning, since a key cap is a legend rather than a truncated word. The test had been excusing caps
  by naming each class one happened to be drawn in, three entries long, and this would have been a
  fourth; `HudText.Apply` now marks anything set in the Hotkey role and the test excuses the role,
  so a fifth cannot go wrong. And the palette's close button needed the shared `panel__close` class,
  or "every window carries an X" would have passed while the rule was broken.

  **Every PlayMode run now writes three portraits** to `Logs/palette-{rows,rail,bar}.png`. The rig
  already renders the real HUD into a render texture, so this costs one `ReadPixels` and is of the
  actual panel. It exists because everything else here asks whether the palette *fits*, and nothing
  can say whether the shape meant to be a bench reads as a bench — which is exactly where a mirrored
  axis hides in thirty-seven hand-written paths. The first set came out with each layout ghosting
  under the next, because nothing clears that texture when there is no camera drawing a world into
  it first; a picture with a ghost in it invites a diagnosis of a bug that is not there, so the
  portrait run clears it. Reading them found three real faults the tests could not: the cost readout
  was never wired to a cost table, the hint line was drawing through the material buttons, and the
  armed banner was floating over the panel.

  **Tiers: EditMode 1244, PlayMode 42 (39 passed, 3 ignored with reasons), fast tier 559 Sim + 217
  Hud.** One PlayMode failure was seen once and did not reproduce —
  `Adr0003_F1_TheDenseHudHoldsItsBudget` at 1.721 ms against a 1.167 ms budget, while four other
  Unity batch runs from other worktrees were on the machine; it measured 0.422 ms on the next two
  runs. Recorded rather than fixed: it is a shared-machine artefact of this dev box, not of the
  palette, and the palette is not in that test's dense layer.

  **Then the default view became a column** (owner, same afternoon, having looked at the portraits:
  *"make the 1st group of buttons short width as possible but evenly sized … you could probably fit
  4 on a row but increase the height and try to use the left hand side of the screen instead of the
  width … also make the stone/wood and material buttons evenly sized in font and size as the other
  buttons but keep the style"*). Rows had spanned the screen, which is what the mockup drew and what
  *"the first group should use the horizontal space"* had asked for back when the palette was ten
  wrapping chips; seven tiles stretched across 1920 are seven very wide tiles with a small icon
  adrift in each, and the board they cover is the board the player is aiming at. It is 372 px now —
  the narrowest that holds four category tiles, with `Recreation` setting the floor — so the seven
  stand two rows deep and the panel is a tall column against the left edge. Only Bar still spans.

  **The header had to break in two to fit.** Eight controls plus BUILD and a three-part breadcrumb
  came to 426 px against a 372 px panel, and the overflow test said so before anything was drawn.
  Rows stacks them: what is selected, then what you can press. The split is made in the shell rather
  than by letting the row wrap, because a wrapping row breaks wherever it runs out of room and could
  have put the close button on a line of its own.

  **Materials went back to the row's box and type** and kept their tint, doubled border and seated
  shadow. The specification had made them the loudest thing in the panel on the argument that a
  material is the terminal choice; in a narrow column that read as two buttons of a different kind
  rather than as the last tier of one control, and what carries "terminal" was never the extra eight
  pixels. Rail's 86 px grid is deliberately untouched — that is the shape of that layout rather than
  a row in it.

  **A third pass fixed the default's height, dropped the hint line, and found the tool that armed
  itself.** The owner asked for three things. The height must stay fixed *"as tall as the structure
  menu/selection goes so it can accommodate all of the menus"* — the panel is docked on the command
  bar and grows upward, so a category with fewer sub-types than the last does not shrink neatly, it
  drops the whole control down the screen while the player is aiming at it. It took four
  reservations, three of them the same faults Rail had already had one tier at a time: the sub-type
  band, the material row, the cost line, and finally the word MATERIAL itself, which was being
  hidden along with its buttons and took another 31 px with it. Unlike Rail the row count is not
  arithmetic — Rows wraps by how wide the *words* are, so the height is a measured constant and the
  test prints every category's band on every run so it can be re-derived rather than guessed twice.
  All seven now stand at 516 px. The hint line went outright: a sentence about the three most basic
  gestures in the game, printed permanently over the board.

  **The third ask uncovered a real defect.** The owner reported that the Build cap on the command
  bar *"still stays bold when it shouldn't"* after leaving build mode, and called it *"an indicator
  to whether you are truly in build mode"*. Two things were wrong and the second was serious. The
  cap could not report a state at all: Build is the bar's primary item and was drawn with a solid
  accent fill at all times, so `.cmd--on` was invisible underneath it — the fill is the state now
  and an outline is the resting style. And then the test written for it failed *before the palette
  had been opened*, which was the real finding: `BuildPaletteModel` is constructed when the HUD
  attaches to its directors, long before anybody opens anything, and its seeding pass **armed** the
  landing sub-type. The game began in build mode with a wall on the cursor that nobody had asked
  for, and a click on the world would have placed one. The lit cap had been telling the truth.

  Nothing is armed now until the player asks: the seeded pass sets where the palette is *pointing*,
  a click is what picks a tool *up*, and the sub-type tiles light by asking `DesignateDirector`
  what is in the player's hand rather than by comparing against what the palette points at. The
  half of that fix which could have gone wrong on its own is the guard in `SelectSubType` — with
  the landing sub-type seeded but unarmed, the first tile a player reaches for is usually the one
  already pointed at, so "same key, do nothing" would have made the first click of every session a
  dead button. The fast tier caught that one within a minute of the first.

  **A fourth pass pinned it into the corner and narrowed what the cap means.** The owner asked for
  the panel *"up against the left screen border and also attached to the bottom bar"*; it had been
  anchored under the Build cap by `PopoverLeft`, which is the rule every other popover follows and
  which left it a few pixels of the bar's own padding short of the edge. And *"if the tile info
  dialog is showing, that is closed down and the build mode is open"* — the specification had asked
  only for the inspect pane to collapse to its header, which was the wrong half of the idea: the
  pane is docked in the same corner, so a collapsed header is still a strip of panel wedged between
  the palette and the bar, describing a cell the player has stopped asking about. Clearing the
  selection also removed the last reason the palette's bottom edge had to be computed at all.

  **And the cap stopped counting an open panel as build mode.** It had counted "a tool is held or
  the panel is up", on the reasoning that a player who has opened the palette is about to build.
  The owner's correction — *"I click esc, that button is not highlighted at all"* — exposes why that
  is wrong: Escape puts the tool down before it closes anything, so counting the panel left the cap
  lit over an empty hand, which is the state the original complaint was about, one step further on.
  What it reports now is the honest question — will the next click on the world place, cancel or dig
  something rather than select it — and an open palette with nothing chosen is its own evidence that
  it is open.

  **The Unity tiers could not be run on this pass:** the owner had the editor open on this worktree,
  and an editor and a batch run cannot share a project. The fast tier is green (559 Sim, 219 Hud)
  and the Presentation changes were reviewed by reading rather than compiling — which caught one
  real error that a compiler would have, a conditional returning a length on one branch and a
  `StyleKeyword` on the other, with no common type between them. That is not a substitute for the
  tiers and the gap is recorded here rather than papered over.

  **Nobody has pressed Play on any of it.** The portraits are the only thing anyone has looked at.
- **The seed became something you can see, 2026-09-17 (U39).** `HudShell.OnNewGame` was one line —
  `BuildSession(SeedEntry.Draw(), null)` — so every colony came from a number that was drawn, used
  and shown to nobody. **The world a player got was unrepeatable by construction**, and nothing was
  broken: there was simply nowhere to read the number and nowhere to type it back. It is a fourth
  screen of the start menu now, in the same fixed box as the other three, and the root's New game
  row navigates rather than building.
- **The plan's table was wrong about this unit in two directions at once, which is why the first
  half hour went on reading code.** It said `U39` was "blocked on U38, which has not moved" — U38
  had merged as PR #92 the same afternoon — and it left `U37` with no done marker although
  `StartingSkillsSystem` had landed at `0baa37f` that morning. Both rows now say so in place rather
  than being quietly corrected, because **the third time a table misleads a session is the time to
  record that it does**, not the time to fix one cell. The check that settles it in a minute is to
  grep for the type the row describes.
- **The interesting decision was what an unreadable box does.** `SeedEntry.TryParse` was written to
  refuse rather than guess and to leave the caller holding the seed it already had, which sounds
  like a convenience and is really a trap: the obvious reading of it is "keep the old seed and carry
  on", and that produces a screen showing `twelve` over a world built from 3829174463. So `Usable`
  is false, Start refuses, and the row draws inert. **It is enforced twice on purpose** — in
  `MenuDirector.Start` as well as in the drawing — because a rule kept only by whoever draws it is
  a rule the next caller does not have, and the next caller here is U40's colonist screen.
- **Entering the screen deals a fresh seed rather than keeping the last one.** The cost is a typed
  seed lost by backing out and coming in again, which is a keystroke. The other mistake costs more
  and is invisible: press New game twice, get the same world both times, and the only available
  conclusion is that the reroll button does not work. A player cannot see that a repeat was chance.
- **The seed rides on the event rather than being read back off the field.** `StartRequested(uint)`
  replaced `NewGameRequested`, which carried nothing. A presenter that fetched the number separately
  could fetch a different one — the field having moved between the press and the read, or having
  been read without the guard that says it names a seed at all — and that whole class of bug is
  removed by making the number that passed the guard the number that is handed over, in one act.
- **Both controls were run.** With the presenter drawing its own seed instead of using the one it
  was given, exactly `TheWorldIsBuiltFromTheSeedInTheBox` goes red; with the guard removed from
  `Start()`, exactly `StartRefusesABoxThatNamesNoSeed` does. The first is the only claim in the unit
  no fast-tier test can make — it runs from a `TextField` in the presentation assembly, through the
  director, through the bootstrap, to `SimWorld.Seed` — and it is driven through the control rather
  than the director for that reason, typing a number the draw would never have produced so that
  passing cannot be a coincidence.
- **It was right first time** (owner, 2026-09-17: *"works spot on"*), which is the first thing on
  this screen that has been — U38 took four corrections off its own playtest, the build gesture
  before it took three rounds, and the naming prompt exists because one evening of saving found what
  §10 had already written down. The honest reading is not that this unit was done better. It is that
  it was **small and had somewhere to stand**: four decisions, three words, one new control, and
  every one of them made inside seams — the fixed box, `.settings__row`, `.field`, `Registry.Label`,
  `HudLayout` — that four earlier rounds of correction had already paid for. **Seam work does not
  show up in the unit that does it; it shows up in the one after**, and this is what that looks
  like from the other end.
- **The orders strip: Chop, Mine, Deconstruct and Cancel left the Build palette for the right-hand
  gutter (owner, 2026-09-17; design `docs/design/14-hud-layout.md` §5.4, code
  `HudShell.Orders.cs`).** The owner's words: *"the small buttons on the build menu for Chop Trees,
  Mine, Deconstruct, Cancel should be a vertical button strip that sits below the depth control and
  menu button — to the right hand side of the screen very close to the screen border … as this
  enables us to quickly give orders without having to click the build button — we can use this in
  future for more orders."*

  **The fault was in the phrase the old design used about them.** `PaletteTools.Pinned` called them
  "always on show" — always on show *inside a panel that is usually shut*, so giving an order cost
  opening the palette first and the cost was paid on every order. That is the third turn of one
  screw: Cancel *"was never missing — every way of finding it was missing"*, then Chop and Mine had
  to be caught as the Orders category was dropped, and now the way of finding all four was behind a
  button. They **moved** rather than being copied: the same verb reachable from two places is a
  question the player has to stop and answer, and `EveryLiveToolIsDrawnSomewhere` is satisfied by
  the strip.

  **The rail and the strip are one column, and that is the part worth keeping.** The depth rail is
  the one region the *world* sizes — its cells shrink to fit the screen — so its height is not a
  number anybody can write down, and a strip anchored at a top of its own would sit under a rail of
  one particular length and float away from or run into every other. They share an absolutely
  positioned gutter and stack inside it, which is the clock-and-alerts fix again and the same rule:
  never two panels in one corner with hand-picked tops. `RailPitch` gives the strip's room up
  *before* it divides what is left among the layers, so the rail is still the region that gives.
  Measured on the real panel: the rail ends at 412 and the strip runs 421 to 589 at 1920 × 1080,
  against a command bar starting at 1031.

  **The coverage ceiling moved from 18% to 19%, and it is the owner's to reverse.** The strip is
  0.80% of a 1280 × 720 canvas — the smallest the game draws, which is what 150 per cent interface
  scale gives on a 1080p monitor — and it took the model's worst resting case there from 17.75% to
  **18.55%**. The precedent in this file is the opposite one: two rows of colonist cards were 8.1%
  and `StripHeightShare` clamped the *region* rather than spending the budget. That worked because
  the strip had a variable height to clamp; this one is four fixed buttons, and even at the 26 px
  squares it wore in the palette header it measures 0.65%, so the choice was the control or the
  number. Per region at 720p: command bar 6.81%, colonist strip 3.81%, stores 2.59%, clock 2.57%,
  depth rail 1.97%, orders 0.80%. **The bar is the largest single spend** and it is full-width by
  the owner's own instruction where the specification drew a centred pill — that is where to look
  first if the number has to come back down. On the real panel the resting HUD still measures
  10.8–11.0%; the 18.55% is the model's arithmetic at the smallest canvas with the stores panel
  open and a colony past what the strip will draw.

  **Four is no longer the ceiling**, which is the half of the owner's instruction that was about
  the future rather than about today. `HudLayout.OrdersHeight` reads the length of
  `PaletteTools.Pinned` rather than a number beside it, so a fifth order is one entry in that table
  and one hue in `HudTheme.PinnedActionHue`; each further one costs 0.19% of a 720p canvas, which
  is the budget to watch rather than the width. The palette header is the switcher, ESC and the X
  now, and `RaiseBuildLayout` lost the hand-written re-index that put the header's four buttons
  back into the lit-state map after every layout rebuild.

  **Both tiers are green** — fast 600 Sim / 287 Hud, EditMode 1350, PlayMode 57 — with three new
  PlayMode tests: the strip is against the right edge and under the rail at all three resolutions,
  pressing a button arms the tool **without the palette opening**, and the open palette still wears
  the held order's colour when the order is picked up from the strip. `HudSmokeTests` counts eleven
  framed regions now rather than ten.

  **Nobody has pressed Play on it.** `Logs/palette-rows.png` shows the strip in the gutter and is
  the only thing anyone has looked at. What a picture cannot say: whether 34 px is the right size
  for a button nobody has aimed at, and whether the strip wants to sit lower down that edge —
  nearer the Menu button the owner named in the same sentence — rather than directly under the
  rail.


- **The header came back to one row, and the armed banner became the thing that says what mode you
  are in (owner, 2026-09-17).** Four instructions in one afternoon, all downstream of the orders
  strip.

  **The palette header is one row again.** *"Shift the toggle view, esc and x onto the same row as
  the build text — this will tidy that up."* Rows had a `flex-direction: column` override that
  stacked the header into two lines, and it existed for one reason: the controls were eight buttons
  and with BUILD and a three-part breadcrumb in front of them the row came to 426 px against a
  372 px panel. Five of those eight had just left. What makes one row survive the *longest*
  breadcrumb rather than only today's is that the identity half shrinks and its crumb ellipsises
  while the controls half does not shrink at all — so "Structure › Roof and floor above › Wood"
  gives way to the switcher and the X rather than pushing them off the end.

  **The armed banner wears the order's colour, three pixels of it.** *"The border around the big
  dialog … should be the same colour as that order … make that border much thicker."* It read
  `HudTheme.PinnedActionHue`, which is what the strip's buttons and the palette's top edge already
  use, so the button pressed, the panel and the banner cannot come to disagree about what a colour
  means. **The sub-line came off** on the same instruction — it was the only place in the game the
  right-click gesture was written down, and that loss is recorded rather than glossed; what covers
  it now is the strip button staying lit and putting the tool down when pressed again. **And it
  came down to the bar**, from 92 px to `ArmedBottom` (one panel gap above a 49 px bar), because
  43 px of clear board made it read as floating rather than as belonging to the controls it is
  about.

  **One name for one thing, and the centralised place is now enforced.** *"Rename 'Cancelling
  orders' to Cancel … rename this to 'Deconstruct' … keep the consistent in the wiki and the
  language and UI … ensure that consistency can be enforced using a centralised place."* The banner
  was the odd surface out: it said Chopping, Mining, Deconstructing and — in a C# literal, because
  cancel has no colonist activity to borrow — "Cancelling orders", while the wiki, the palette's
  breadcrumb and the strip's tooltips all said **Chop trees, Mine, Deconstruct, Cancel**. Both
  renames the owner asked for were already the registry's own words, so the fix was to stop writing
  any of the four in C#. The rule underneath: **an order is an imperative and an activity is a
  gerund** — "Chopping" is `ui.status.felling`, what a *colonist* is doing, and the roster card
  still draws it; the banner was mixing two namespaces.

  **`RegistryTests.NoPlayerFacingNameIsWrittenInCSharp` is the enforcement**, and it is the shape
  of `HopPriceHasOneOwnerTests`: read every C# file in `Odyssey.Hud` and `Odyssey.Presentation`,
  fail on a string literal that equals a registry name in the six namespaces where one thing is
  named on several surfaces at once. Scoped to those six because "Build" and "Menu" are registry
  names *and* ordinary words; the scan found **eight hits and no false positives**, so the boundary
  is measured rather than hoped for. **Two of them were live duplicates**, both agreeing with the
  registry at the time — which is what a silent duplicate looks like until somebody corrects one
  copy: the seven Build category labels sat in `PaletteTools.Categories` beside the keys that
  already named them (the tuple is `(key, tools)` now), and the banner's build fallback said
  `"Building"` beside `ui.status.building`. The test was verified by planting an offence and
  watching it fail, then removing it.

  **The banner has a floor, not a width.** *"Make the dialog a fixed predictable width — as the
  longest order … at least this size until further notice (and let it extend beyond that)."*
  `HudLayout.ArmedWidth` walks `PaletteTools.Pinned` through `OrderWord` and takes the longest, so
  a rename moves the number with it — the owner's guess was "Chopping" and it is "Deconstruct".
  Measured in PlayMode: all four orders draw at **139 px against a 138.5 px floor**, so the four
  boxes are identical and the banner no longer resizes under the eye as the mode changes. An armed
  build reads "Building wall of wood" and is allowed past it.

  **All three tiers green** — fast 600 Sim / 288 Hud, EditMode 1351, PlayMode 59 — with three more
  PlayMode tests: the header is one row in every layout at every category and resolution, the
  banner wears each of the four colours at the right thickness with exactly one label, and each
  order's box reaches the floor.

  **Backtick opens a debug menu now instead of toggling the developer overlay directly**
  (`claude/build-palette-layouts`, 2026-09-17, owner: *"we need a debug menu … currently \` is
  taken for the developer overlay. Instead create a new menu in the style, and format of all the
  other menus … include various things we can debug in game in future"*). `DebugDirector` is the
  new director — `Open`/`Toggle`/`Changed`, nothing else, because "is the panel open" is the only
  state a director needed to add; the two action rows submit intents straight from the presenter
  the way every other click in this HUD already does, and the overlay toggle stayed exactly where
  its persisted state lives (`SettingsDirector.DeveloperOverlay`), only its row moved. **The row
  moved rather than being copied**: it used to live in Settings' Interface tab, and this project has
  already paid twice for a control drawn in two places (`EveryLiveToolIsDrawnSomewhere`), so it has
  one home now, in the panel the key actually opens.

  Two cheats went in because both wrap sim APIs that already existed and touch no new mechanics:
  `IntentKind.SpawnPawn` wraps `PawnRegistry.Spawn`, `IntentKind.GiveResource` wraps
  `ColonyItems.Spawn` by way of the same `NearestCellWithSpace` an ordinary drop already uses, and
  both are registered in `ColonyComposition.AddColony` beside `ForceJob` — the fourth and fifth
  intent handlers to land there, none of them a new chokepoint. **What did not go in, on purpose**:
  a real "invoke event" (there is no repeatable event system in the sim at all, only a scenario that
  acts once at tick zero — the row is a visible, disabled placeholder with a tooltip saying so) and
  kill/damage/heal a colonist (`Pawn` has no health or injury model of any kind yet, so a debug
  "kill" would be inventing a mechanic rather than shortcutting one). Both are named in
  `docs/design/18-debug-menu.md` as the two things a real feature would have to land before this
  panel could grow into them.

  **The overlay text went bigger twice, and moved on the second pass.** First: *"make the text much
  much much bigger for the developer overlay in game as it cannot be read"* — `OdysseyBootstrap`'s
  `OnGUI` drew it through a cached `GUIStyle` at 28 pt, roughly triple the previous default, at its
  original top-left position. The owner played that and asked for a second pass the same day: *"move
  that developer overlay further (towards the bottom of the screen) and make the text much bigger."*
  56 pt now, and anchored to the bottom of the screen — `Screen.height` minus the text's own measured
  height minus a fixed clearance for the command bar — rather than a fixed top-left offset, because
  doubling the size again at the old position would have put it back on top of the HUD's own
  top-left ledger. Word wrap is off, so the line count the height is measured from stays exactly the
  number of `\n`s already in the string.

  **Two existing PlayMode tests broke and both were the debug panel colliding with something that
  already used the name it reached for.** `HudSmokeTests` hard-codes the HUD's framed-region names;
  a thirteenth region needed adding to the list, the same as every panel before it. The sharper one:
  the debug menu's disabled "Invoke event" row reused `.settings__row--off`, the exact class
  `StartScreenTests` queries globally to find the seed screen's own disabled Start row — with both
  panels built at startup, the query started matching two elements and the seed test's "and back to
  usable" assertion broke on an unrelated row it had never known existed. Fixed by giving the debug
  menu its own `.debug__row--off`, identical in style, so a class used as a test hook is not shared
  by two panels that have no reason to agree about it. Found by running the Unity tier rather than
  reasoning about it — the fast tier cannot compile `Odyssey.Presentation` at all and would have
  said nothing.

  All three tiers green: fast 603 Sim / 313 Hud, EditMode 1390 (1379 passed, 11 pre-existing
  explicit/ignored), PlayMode 65 (61 passed). `Logs/hud-debug.png` is the one picture anybody has
  taken of it; nobody has pressed Play.

- **A new colony stopped arriving with its work already ordered, 2026-09-17** (owner: *"at the start
  of game there are no orders, until you assign them for the time being"*). The interesting part is
  that `ScenarioDef.Playtest`'s own doc comment had promised exactly this: *"the colony has felling
  work the moment it exists, because there is no tool to give the order with yet. When the UI line's
  designate tool lands, the scene moves to `Bare` and the player gives the first order."* That tool
  landed in M3; the cancel tool and the Build palette landed after it; and this sat there through
  all of it, so every new colony still opened with a ring of trees marked and colonists already
  walking off to chop them. **A note saying what to do when a thing lands does not do it**, and the
  note was accurate, specific and completely inert for a milestone. The same shape as `17`'s §10
  admitting that Save always wrote a new file — except that one lasted an evening because somebody
  was using it.
- **Three tests were quietly living off those pre-marked trees.** They asserted a colony had felled
  something, and what they were really proving was "the scenario gives orders and the colony carries
  them out". They give the orders themselves now, which is both honest and closer to what the game
  does. Worth noticing as a class: a fixture that supplies a precondition for free will be depended
  on by tests that never mention it, and the day it changes they all fail for a reason none of them
  is about.
- **A colonist rolls from its own seed, 2026-09-17 (U40).** Skills and passions were already pure
  functions of `(world seed, pawn id)` — which is why a candidate can be shown before any world
  exists — but the seed was the *world's*, so there was no way to say "this one, differently".
  `Pawn.RollSeed` is the smallest thing that makes one person rerollable while leaving the board
  alone, and it defaults to the world's so that nothing which nobody chose moved at all.
- **The test written first caught the thing reading the code would not have.** `PawnContext.Seed`
  is populated by `Sync`, which runs on the first tick, and placement runs before any tick — so the
  first version of the seam rolled **every colony in the game from seed zero**, whatever board it
  was on. It presented as `StartingSkillsTests` reporting "two different seeds rolled identical
  starting skills", which is about as clear as a failure gets. The fix closes a latent trap rather
  than a U40 bug: anything else that read `ctx.Seed` before the first tick had the same problem.
- **The goldens moved both numbers this time, and that is the diagnosis working.** U37's roll happens
  on the world's first tick and so moved only `Simulated`; a seed is assigned at *placement*, which
  runs before `Generated` is taken. Nothing about those three worlds changed — the skills are
  byte-for-byte yesterday's — and what moved is that the hash can now see the number they were
  rolled from. The failure message points at the generator and says to check the grid hash before
  believing it, which is precisely the case it was written for.
- **"The name follows the roll" could not be taken literally.** The owner's ruling was that a reroll
  gives you a different person rather than the same person with different numbers, and the obvious
  reading is a hash of the seed. Measured: every colonist a world places itself shares that world's
  seed, and against a pool of eight that calls **two of five the same thing more often than not**.
  Two people called Wrenn in a colony of five is not a naming scheme. The seed chooses where in the
  pool the colony starts reading and the id says how far along — so the distinctness the id-only
  scheme gave for free survives, and the name still moves when the seed does. The select screen's
  three carry three *different* seeds and can still collide, so that guarantee lives on the screen
  rather than in the name, where it belongs.
- **The card has to be rolled for the slot it will occupy**, and the seam did not take one. Both
  draws mix the pawn's id in, the id comes from the slot, so every card rolled as slot 0 would have
  shown the right name and the **wrong skills** for two of the three — a screen that promises a
  miner and hands over a cook, with both halves individually correct and nothing wrong enough to
  throw. Caught by reading the code rather than by a failure, which is the uncomfortable kind: the
  fast tier could not have noticed, and the PlayMode test only checks the count.
- **The fixed box finally bit, and it was right to.** Three cards at a line per skill came to 296
  against the body's 284 — the first thing that has not fitted since the owner fixed the panel's
  height, and exactly the possibility `17-start-flow.md` §11.4a named when it said U40 might find
  the box the wrong size. It is not: both skills on one line is how the design's own sketch drew
  them, reads as one fact about a person rather than two, and comes to 242. **A constraint that
  eventually refuses something is the only kind worth having**, and the useful thing is that it
  refused in a fast-tier assertion rather than on screen.
- **The two NUnits disagreed again**, and it still cost a Unity run. `Has.Count` throws on an
  interface-typed collection under Unity's bundled version while the fast tier is perfectly happy,
  so three new tests were green in eleven seconds and red in the gate. `docs/lessons.md` has had
  this written down since the morning of the same day.


- **Beds, the first furniture, 2026-09-17** (`docs/design/20-beds.md`, interview in
  `docs/research/beds-interview.md`). The owner asked for the whole loop in one breath — bed
  from the Build palette, a rotatable two-tile outline on R, built through the pipeline, a
  skill-rolled quality of Poor/Normal/Decent/Uber/Epic, a colonist assigned to own it and sleep
  in it — and the interview settled the four decisions that shaped it: two tiles with computed
  placeholder art; R rotates only while a rotatable ghost is armed; assignment from the bed's
  own pane; the tiers scale rest effectiveness now, numbers provisional in XML.
  - **The ground found more standing than expected.** A "bed" already existed as an invisible
    scenario cell — the sleep chooser scans a list of them and rest already splits 100 in a
    bed against 80 on the ground — so the bed upgraded a notion rather than inventing one. The
    build pipeline turned out to be generic over the thing built, so a bed site flows through
    delivery and construction untouched. And nothing existed for the rest: no multi-cell
    edifice, no rotation input, no ownership state, no quality field.
  - **One record behind two slots, and the second cell is not stored.** Both cells'
    `Edifice[]` point at the same `PlacedEdifice`, whose facing says where the far cell is —
    derived by `EdificeFootprint`, never stored, so the record cannot disagree with itself. The
    first cut stored a `CellIndexB = -1`, and the reason it went is a language fact worth the
    journal: **Unity compiles C# 9, where struct field initializers do not exist**, while the
    dotnet mirror compiles `latest` — so `= -1` defaults would have passed every fast-tier run
    and broken the Unity tier, and cell 0 is a real cell besides. Facing, Quality and Owner are
    all zero-safe instead: facing 0 is north-and-meaningless, quality 0 is none, and owner 0 is
    nobody because pawn ids are 1-based.
  - **The bed's edifice id is 12, not 10, and the trees are why.** `NaturalContent.FirstEdifice`
    reserved 10 and 11 for conifer and broadleaf; the first cut took 10 anyway and would have
    drawn every bed as a conifer, named it one in the pane, and — because the render model
    routes ids ≥ 10 to the natural table — indexed past the natural module array. The contract
    weld test (`CellDetailTests`) caught it in the fast tier before Unity ever saw it, which is
    what it is for. The mesher's stuff-tint rule now asks `IsTree(def)` rather than an id
    range, so a wooden bed keeps its timber tint above the trees' ids.
  - **Quality closed U26's outstanding success roll** — rolled once, at completion, by the
    finishing colonist's Construction skill, on a bell whose weight falls off two a step from
    the tier the skill has earned: a novice never reaches Epic, a master never falls to Poor,
    and the tests pin exactly those endpoints, never a draw. The tiers are content
    (Quality.xml, fingerprinted beside the buildings) and the rest numbers — 85/100/112/125/140
    per cent of a plain bed, the owner's own — replaced the hardcoded 100 the needs system
    carried. The roll draws from (world seed, cell ^ tick) on a new `BuildQuality` salt: the
    refund's shape, not the yield's, because a bed rebuilt on the same spot is a new bed and
    may finish better, where a cell mined twice is not a thing that happens.
  - **Ownership lives on the bed record and nowhere else** — no pawn-side field, riding the
    edifice list into the save and the hash instead. `AssignBedOwner` is the handler's to keep
    honest: one bed per colonist is enforced by releasing the old bed in the same breath, a
    pawn who does not exist is refused, and either half of a bed names it. The sleep chooser
    learned the same rule — a bed that is somebody's is theirs and nobody else checks in, and
    a colonist's own bed wins outright however far away it is, measured by test: the owner
    walks past a nearer unowned bed, and the second colonist takes the unowned one.
  - **R is the game's first context rule.** R was already the slice's, and the owner's answer
    was R anyway — so instead of a second `HotkeyAction` (a clash the binding map refuses by
    design) the two consumers agree on one predicate, `DesignateDirector.RotatableArmed`,
    decided in the fast tier: the tool claims the slice-up key's press while a rotatable thing
    is armed, PageUp is never claimed, and design 09 §6 gained case 9 with its own "no key
    means two things today" line amended — the day one does has arrived, and this is how it
    was solved without breaking the one-key-one-action invariant.
  - **The pane's first interactive fact.** A bed's readout says its tier and its owner, and the
    owner row opens the colonist popover — built once, filled on open, a pick submitting
    `AssignBedOwner` exactly as every command is submitted, landing on the next tick or on
    unpause. The affordance is armed by `InspectModel.BedUnderPane`, cleared every refresh so
    it cannot outlive the bed.
  - **The placeholder is three scaled boxes** — frame, mattress, pillow — instances of a plain
    block module tinted by the bed's stuff, drawn once from the head cell, rotated by the
    stored facing, centred on the seam of the two cells and draped there: a bed is one thing
    fixed to the grid across two cells, and the rule the stepped walls settled is that
    anything fixed to the grid is draped. One catalogue row on `odyssey.module.bed` upgrades
    every bed the day real two-tile art exists.
  - **Two gates were built for this line.** A local C# 9 syntax gate — the whole Presentation
    assembly, Unity-side tests included, compiled against the installed Unity's own DLLs —
    because the fast tier cannot see Presentation at all and it caught `GroundRelief.Drape`
    returning a matrix where the first cut read a point. And a version-2 save fixture built by
    hand, proving the bed's three new record fields read back as zeros from a file the day-old
    build wrote. Save format is 3 on this branch; whichever of start-flow and beds merges
    second takes 4.
  - **Verified:** fast tier 586 Sim + 219 Hud green, one ignored (a board shape the solid-cell
    search could not find), both content gates green, the C# 9 gate clean. Not run: the Unity
    tier — it runs on the owner's self-hosted runner against the pull request. **Not measured:
    nobody has pressed Play.** The bed's placeholder art, the popover's reach and the R claim
    are all judgements for the keyboard, and design 20 §12 already lists what is open.

- **The bed and the floors, checked against the owner's use case, 2026-09-17.** The owner asked
  the question the flat-board tests had not: does the bed build on a floor above, and is the
  colonist assigned to it able to sleep in it — checked for floors above and below. Five tests
  came out of the asking, and nothing had to change: the seams were already right, they had
  simply never been asked.
  - **A slab is a floor, and the bed's footprint accepts both cells on one.** A bed ordered a
    storey above the ground, on slabs laid over open air, places, raises, publishes its tier and
    joins the sleep list — with the control proving the pass is the slab and not thin air (air
    over air is still refused). The slabs are laid straight onto the grid the way the floor half
    of the building line will lay them when its branch lands; the bed's claim is about what it
    stands on, not who put it there.
  - **A floor over a bed changes nothing.** A slab laid over the site between order and raise —
    the world moving over the order while the wood was out — leaves placement, raising and
    ownership untouched, because nothing in the bed's rules looks up.
  - **A storey up, end to end.** On the terraced meadow a bed ordered as a player orders it
    (an intent) on higher ground than the start is fed its wood up the riser, worked, and
    finished at a rolled tier by the driver's own completion branch — the one path no earlier
    bed test had reached, every other test raising beds directly. Its owner then climbs to it
    and sleeps there at the Epic rate.
  - **The honest limit, stated in the test's own comment:** a bed on a *built slab* storey can
    be placed, raised, owned and asked about today, but sleeping in it — and hauling to it —
    waits on a way up. The built-stair line has not landed; a ladder excludes a laden hauler by
    its own rule, and nothing else on this branch connects storeys. The terrace test reaches
    its bed the way the game currently can: a one-block hop. The day stairs land, the same
    tests will hold one storey higher without a line changing.


- **U29 floors and collapse, 2026-09-17.** The unit the project exists to prove, and on inspection
  mostly wiring: the support physics was built in M1 and switched off. `SupportSolver` had computed
  collapses since then, `SupportSystem` had deferred them, and the lambda at the end of it was
  empty with `/* M3: rubble and fall damage */` written inside it. Design, and the eleven owner
  decisions behind it, are `docs/design/17-floors-and-collapse.md`.

  - **A floor is one field on a `BuildingDef`.** `Building_Floor` is `Building_Wall` with
    `slab = true`, through the same order → deliver → work → raise. `Raise` writes
    `CoreContent.SlabBuilt` and the material into the cell's lower boundary instead of appending an
    edifice, and that is the whole difference. The claim is testable rather than rhetorical: the
    journey test is the wall's journey with one word changed.
  - **`SlabBuilt` is a fourth slab kind, and it is `PlacedEdifice.Built`'s argument one level
    down.** The three that existed are all the generator's — structural decks, plaza decks, roofs —
    so a fourth is what makes "take our own floors apart and not the ruined city's" a question that
    can be asked. It cost no new state: `Floor[]` has always been saved and always been hashed.
  - **Nothing in presentation changed to draw it**, which was luck worth noticing:
    `WorldRenderModel.FloorModule` returns the stuff group's slab module for *any* non-zero floor
    and never looks at the kind. A built floor draws in its own material's tint the moment it is
    written, with no catalogue row and no mesher edit.
  - **The support rule is now visible to the player rather than only to the solver.**
    `SupportIfSlabAt` answers what a slab *would* have if one were built, reserving nothing, and
    `Allows` refuses an order it says is zero. So a colonist bridges out from a wall as far as
    support reaches and the next order is refused with a reason, instead of being accepted, walked
    to, carried to, built, and collapsed on the tick it finished. **Nobody tuned that reach**: it is
    `MaxSupport` seen sideways, which is why the test reads it off the solver rather than spelling
    a 4.
  - **The three omissions are closed together, as all three comments demanded.** `Raise`, `Demolish`
    and `MineCell` each said support was deliberately not marked dirty, each named U29, and each
    warned that the three should be wired at once rather than one of them quietly acquiring
    behaviour the others lack. `PawnContext.Support` is the seam; `MarkStructureChanged` is the
    call, and it marks the cell *and the one above it* because they are two different questions.
  - **The design was complete and the feature still did not work.** The journey test sat through
    20,000 ticks and nobody built anything: **a slab has no neighbours on its own layer to stand on
    until there is already a floor up there**, so `StandBeside` answered -1 for the first slab of any
    storey and the work giver never offered the job. Nothing logged and nothing failed — the exact
    silent refusal `15-building.md` §6 was written about, found only because the test asserted a wall
    went up rather than that a job started. `StandToBuild` reaches a slab from below as well, which
    is mining's envelope and mining's argument: a plank goes overhead exactly as a pick does.
  - **A second wiring fault, found the same way.** `SupportSystem` needed a `PawnContext` to drop
    things into and took it as a constructor argument — and seventeen places build one, so the single
    site that mattered was not told and two tests said, correctly, that nothing fell and no rubble
    landed. It is bound in `ColonyComposition.AddColony` now, which is the one place holding both,
    and a colony therefore cannot be assembled without it.
  - **Rubble gave two long-dead flags their first readers.** `TerrainDef.buildable` had existed
    since the tables were written and nothing read it; `Allows` reads it now and rubble sets it
    false, which is the whole of "clear the mess first". `clearable` is new and set by rubble alone,
    because `CanMine` wants solid terrain and a heap on a floor is not a face to cut — that is
    U28's missing middle speed, "breach a slab, clear rubble, mine rock", landing with the unit
    that first produces any rubble at run time.
  - **Nobody is hurt by a fall, deliberately and temporarily.** `a-02` has the number —
    `15 × layers^1.5` blunt on the bottom-facing parts — and it is calibrated against a part tree
    with pain, shock and a 150 HP threshold, none of which exists. Applied to a single invented HP
    pool the exponent means nothing, so it would be a number built in order to be thrown away.
    `Thought_Fell` keeps the event legible meanwhile. **The cost is real and is written down rather
    than glossed:** "pulling out the wrong pillar hurts somebody" is half of why collapse is
    interesting, and today it does not.
  - **Content re-baked deliberately, three fingerprints, one line each** with what moved written
    beside it: the building table (the floor), the pawn table (the thought) and the terrain table
    (rubble's two flags). **No golden moved**, because no golden world has a slab in it — mining
    now marks support dirty and there was nothing anywhere for it to bring down.
  - **Verified:** fast tier **555 Sim + 191 Hud** (13 new, `FloorsAndCollapseTests`), Long tier
    **19**, both content gates `--check` clean — the floor tool reuses the catalogue's existing
    `ui.arch.tool.roof` key and only its label moved, because `icon-map.csv` is keyed the same way
    and a key is forever. **Not run:** the Unity tier, which this container has no Unity for.
  - **Nobody has pressed Play on any of it**, and one thing waits on that: the build cursor names
    the surface under a click, which for a pit is the bottom of the pit, so ordering a floor *over* a
    drop may name a cell the player did not mean. Ordering along an existing edge is unaffected. It
    is a cursor question, not a simulation one.

- **U29 reviewed on the way to a playtest: the floor tool was inert, and its own tests could not see
  it (2026-09-17, `claude/floors-review`).** PR #90 went conflicting against `origin/main` and was
  taken into a worktree to be merged, reviewed and played. The conflict was docs only — U39's seed
  entry had landed on `main` in the meantime — and both journal entries were kept, because both
  happened. What the review found was not in the conflict.

  **The measurement.** U29's thirteen tests all name their site in C#: `Above(wall)`, which is the
  right cell and is not the cell the running game sends. A probe ordered a floor at each cell a
  *click* can actually produce, on a board with one wall raised on it:

  | Pointed at | Cell the picker returns | Result |
  |---|---|---|
  | the wall's top face | the wall's **own** cell | `NotPermitted` |
  | bare grass | the ground **block** | `NotPermitted` |
  | — | the air over grass (unreachable) | `NotPermitted` |
  | — | **the cell above a wall** (unreachable) | `None` |

  The only cell that accepted a floor was the one nothing could name. `SlicePicker` stops the ray in
  the first cell whose face occludes it, and a wall's face occludes, so a click on a wall is the
  wall. The tool armed, dragged, drew its green preview box and did nothing — the silent refusal
  `15-building.md` §6 was written about, in a feature whose own commit message quotes that section.

  **Why the tests could not see it.** `SlicePicker` is in `Odyssey.Presentation` and
  `ConstructionGrid.Place` is in `Odyssey.Sim`, and neither assembly's tests can see the other. The
  two rules were each correct against their own fixture and disagreed about the only thing that
  matters, which is the cell in the middle. That is a seam, and a seam nobody tests is where this
  kind of fault lives — the same shape as the hop price, which is why `HopPriceHasOneOwnerTests`
  exists.

  **The fix is the design's own decision, which the implementation had departed from.** Decision 4
  of `17-floors-and-collapse.md` reads *"the cell they want a floor in — the same lift a wall order
  gets"*. The code deliberately did not lift a slab, reasoning that a floor ordered on solid ground
  is refused either way. Sound about *ground*, and about the wrong surface: **a floor's surface is
  just as often a wall**, because the first slab of any storey rests on the walls of the one below.
  `ConstructionGrid.StandingOver` is `StandingOn`'s twin — a slab ordered at anything that fills a
  cell, solid terrain or an edifice, means the boundary on top of it. Ground is untouched and
  deliberately so: a click on grass still lifts to the air above, `AllowsSlab` still refuses it for
  having a floor already, and the refusal is still reported at the cell the player clicked.

  **The cursor was lifted by the same rule in the same change.** `OdysseyBootstrap.PreviewLayerAt`
  asked the solid-terrain question whatever tool was armed, so a floor drawn over a run of walls
  would have put the green box one layer under the floor it was promising. It now asks the armed
  building whether it is a slab.

  **The seam is tested as a seam.** `Odyssey.Tests.Presentation.FloorToolReachTests` is in the one
  assembly that can see both halves and never writes a cell index down: it fires a ray, feeds
  whatever the picker returns straight into the order, and asserts a site appears on top of the
  wall. It carries the storey loop too — point at a wall, floor it, point at the floor, wall it —
  which is the loop that makes this a building game rather than one slab.

  **And a harness to look at it with.** `FloorCheck` is `WallCheck`'s other half: two rooms on the
  wooded board, roofed through `ConstructionGrid` with every cell named by the picker, then one wall
  pulled out of the second with `Demolish` and the world ticked so the solver finds the orphan.
  Six pictures, `Logs/floor-*.png`. It is deliberately built to fail loudly if the two rules ever
  disagree again: if the walls stand and no slab does, it logs the error and exits 1 rather than
  photographing its way past the fault.

  **Then the pictures found the second half of it, which the fix had not touched: a room could not
  be roofed.** `FloorCheck`'s first sheet showed both rooms with their walls capped and the middle
  open to the sky, and the instrumented re-run said why — all twelve clicks over the interior either
  named the floor of the room (the banded picker the rig uses) or hit nothing at all. **A pointer
  cannot name a cell of open air.** That is the picker's whole contract working as the owner settled
  it on 2026-09-16 (*"I still wanted to select the tile below it or not at all"*), and it cannot
  express the one cell a floor is for.

  **Decision 12, taken by the owner with the measurement in hand: the floor tool takes its column
  from the pointer and its layer from the slice.** Set the slice to the storey you are roofing and
  click inside the room. `DesignateDirector.WorkingLayer` is the substitution — there, so the
  geometry is still decided in the one class the fast tier can reach — and `DesignatePresenter`
  decides when, because it is the only place that can see both the rig's active layer and
  `ConstructionContent`. Null for every other tool, with a test that says so: a mine order sent to
  the layer the camera happens to be at rather than the rock the player clicked is exactly the
  misclick ADR 0006 exists to prevent. **It also closes the cursor question `17` §9 left open** —
  ordering a floor over a drop named the bottom of the drop, and names the layer being worked now.
  The roof went from 18 cells of ring to 28 of 30 on the same board.

  **Two things the harness found about itself, both worth keeping.** A hand-composed world does not
  get `ColonyWorld.RebuildDerived`, so its support field is zero everywhere and the first incremental
  solve is wrong — `CellGrid` says so in a comment and this is the first thing to be caught by it.
  And **a tree is a pillar**: `SupportSolver.IsGrounded` asks whether anything fills the cell below,
  an edifice included, so a trunk makes the boundary over it a full-support source and feeds that
  support sideways into a roof beside it. Every wall was pulled out from under a 6 × 5 roof and it
  did not move, held at 4, 3, 2 by two or three trees in its own footprint. That is the model
  working and it is a useless photograph, so the harness fells the site and a one-cell margin first,
  which is what `startingFellRadius` does for a colony anyway. **Worth the owner's eye rather than a
  fix:** felling a tree can now bring a roof down on somebody, and nothing warns them. U31, the
  support preview, is where that belongs.

  - **Verified after the merge and both fixes:** fast tier **573 Sim + 193 Hud** (three new), Long
    tier **19**, Unity EditMode **1,237 total, 1,228 passed, 0 failed** — which is the tier U29
    itself could not run, and the one that compiles `Odyssey.Presentation` and the editor tools at
    all. Both content gates `--check` clean, unchanged by any of this.
  - **Still open.** A bridge is ordered outward a cell at a time: support is a settled value read
    off the grid, so the second cell of a span is refused until the first is standing. That is the
    rule being honest rather than a fault, and whether a planned span should be orderable is a
    question for U31 alongside the preview that would make it legible.
  - **And one for the eye, found by the sheet.** Rubble is terrain, so it draws as a full cell
    block: a collapsed 6 x 5 room comes out as a clean rectangular plate one layer up from the
    ground, keeping the two-cell hole its roof had. A matched before-and-after pair of the same
    room from the same camera could not be told apart by eye, and the footprint had to be printed
    as characters to settle whether the collapse had happened. It had - every slab down, 28 cells
    of rubble, the walls gone. **A game about pulling buildings down should not need an ASCII dump
    to show that one came down**, so whatever rubble ends up looking like, it must not look like a
    floor. Recorded against `17` section 9's existing "rubble is in the palette and is not art" line,
    which turns out to be the same finding with the cost attached.
- **"Nothing happens when I try to lay down a floor" was delivery, not code (2026-09-17).** The
  owner played it and reported the floor tool doing nothing. **It was doing nothing because there
  was nothing there:** their checkout sits on `claude/status-sync`, where `BuildingHandle.Count` is
  **2** — None and Wall — and `Buildings.xml` has no `Building_Floor` at all. `origin/main` is the
  same. The whole of U29 is on PR #90, unmerged, so the roof chip they clicked was the
  drawn-disabled one that has never done anything and never claimed to. Checked before a single line
  was read, which is `lessons.md`'s own rule about confirming delivery first, and it saved the
  session hunting a second cause for a fault that did not exist in the code under it.

  **The second half of the report was real and is fixed.** *"The selection box for floors should be
  flat to the tile that it will be placed on rather than a cube."* A build drag draws one closed
  wireframe box over the whole run — played and accepted for a wall on 2026-09-17 — and for a slab
  it is wrong three ways at once: it claims a 3 m wall, it hides the tile it is promising underneath
  itself, and at the slice camera's 32–160 m nothing says which of two layers it means.

  `ChunkRenderer.DrawCellSpanPlate` is the floor's own cursor: same span, same inset, same drape,
  laid on `CellMetrics.FloorCentre` — the plane `ChunkMesher.EmitFloor` puts the slab itself on — so
  the cursor's underside is the floor being offered rather than something straddling it. It is the
  argument the box cursor already makes, applied to the other shape: **the cursor is the shape of
  the thing.**

  `PlateThickness` is a tenth of a cell and is **a cursor convention rather than a model of the
  slab**, said plainly so nobody later "corrects" it to the real number: the prefab is 0.10 m deep,
  which is under a pixel at working range, and a cursor nobody can see is worse than one slightly
  fatter than what it promises. `AFloorCursorIsAPlateOnTheBoundaryItWillBeLaidOn` pins the height
  **against the wall cursor** rather than against a number, so no tuning of that line can make the
  two read alike.

  - **Verified:** fast tier **573 Sim + 193 Hud**, Unity EditMode **1,238 total, 1,229 passed, 0
    failed**. Still nobody has pressed Play on the plate itself — the owner is playing the worktree
    `D:\code\odyssey-floors`, which is where U29 exists.
- **"4725 x PlaceBuilding: NotPermitted", and every one of them was right (2026-09-17).** The owner
  opened the worktree, armed the floor tool, dragged over the meadow and pasted a console full of
  refusals. `ReportRejections` aggregates over a one-second window, so that is one or two
  board-sized drags rather than a loop.

  **Measured before diagnosing, and the first hypothesis died.** The guess was "no cell on a bare
  board will take a floor". Wrong: on the played wooded meadow, at the layer the slice starts on,
  **2,386 of 14,400 cells will take one.** The rule that refuses the rest is not one rule but two —
  **2,408 already have a floor** (the ground under your feet) and **9,198 have no support** (open
  air with nothing grounded within reach). The number that actually explains the report is the
  local one: **within ten cells of the start, 21 of 441**. The legal cells are the lips of terrace
  drops, scattered over the whole board, and a player drags where they are standing.

  So the feature was correct and unusable, and the interface was lying: the cursor was bright green
  over all 95% of it. **`NothingHereWillBeBuilt` asks `ConstructionGrid.Allows`** — the same method
  `Place` calls a moment later, so the cursor and the order cannot come to disagree, which is the
  fault this line of work has now hit twice — and the cursor goes red when **not one cell** of the
  drag would be built. Only then: a wall dragged across a meadow routinely covers a tree and is
  expected to, and that gesture was played and accepted as it is. What has no defence is a green
  cursor over an order that does nothing.

  **And the report's real content was a missing feature.** *"I should be able to just build a
  floor"* means paving — a covering laid on ground that is already there. U29 built the structural
  slab, and the two share one English word only because this project promoted the roof to a real
  thing. `docs/design/18-paving.md` scopes it and `U42` carries it: the mirror of the slab rule,
  **no support check at all** because a covering over ground is grounded by definition, stored as a
  fifth kind in `Floor[]` so **no new save state and no hash change**, drawn by `FloorModule` with
  no catalogue row, and taking the *wall's* lift rather than the slab's. The three names were
  already in `icon-keys.csv` and already published, so nothing in the wiki moves.

  Three things put in the doc rather than left to be found. **Paving will be cosmetic until rooms
  are** — walking speed, cleanliness and beauty are why it exists in the genre and none of them
  exist here. **The one real risk is drawing, not data:** a covering is coplanar with the top face
  of the ground beneath it, so `EmitFloor` may z-fight, and that is the first thing to measure.
  And **the naming**: `Structure -> Floor` and `Floors -> Deck plate` will sit two clicks apart
  meaning different things, so the recommendation is to rename U29's tool to `Slab`, key unchanged
  because a key is forever.

  - **Verified:** fast tier **573 Sim + 193 Hud**, Unity EditMode **1,238 total, 1,229 passed, 0
    failed**, both content gates clean. **Nobody has pressed Play on the red cursor or on the flat
    floor cursor yet**, and no line of U42 is written.
- **Naming, settled against the recommendation: both say floor (owner, 2026-09-17).** `18-paving.md`
  §7 asked whether to rename U29's tool to `Slab`, so that "floor" would mean only the covering, and
  ranked that first. **The owner's answer is that both say floor.** Recorded as a decision rather
  than left as an open question, so no later session re-opens it: `Structure -> Floor` keeps its
  name, the coverings keep `Deck plate`, `Grating`, `Tile`, nothing in `icon-keys.csv` moves, and
  U42 carries no wiki or label rebuild at all.

  **What it costs is written down beside it**, because a decision taken against a recommendation is
  the kind that gets quietly reversed by somebody who does not know it was taken. Two reachable
  tools now share a word, so the word cannot be what tells them apart and three other things must:
  the palette **category** (`Structure` spans and falls, `Floors` is laid on ground — and a covering
  must never appear under `Structure`, however convenient that looks later), the **descriptions**,
  which are already written and already published, and the **cursors**, which differ because the
  rules differ rather than because anyone arranged it — a slab goes red where it cannot stand and a
  covering almost never will.

  The predicted failure mode is in the doc so that it reads as predicted rather than missed: a
  player who arms the wrong one of the two gets a nearly identical cursor and a completely different
  order. If that bites in play, the cheap answer is a clearer readout of which tool is armed, not a
  rename.

- **The one risk in U42 was measured before any of it was written (2026-09-17).** `18-paving.md` §3
  named z-fighting as the unit's only real unknown — a covering is a slab in a cell that already has
  solid ground beneath it, so the drawn slab and the ground block's top face are coplanar — and said
  to measure it first rather than discover it late and blame something else. `PavingProbe` does
  exactly that and **needs no part of U42 to exist**: a covering's geometry is decided entirely by
  where `ChunkMesher.EmitFloor` puts a slab, and that does not care which kind of slab it is, so
  writing `Floor[]` and `FloorStuff[]` straight over grass produces the pixels U42 would produce.
  Four shots, at three ranges and one grazing angle, because z-fighting is a depth-precision
  artefact: a patch that is clean at 18 m can shimmer at 150 m, and 32–160 m is where the slice
  camera actually sits, so a close-only answer would have been worse than none.

  **It does not fight.** 172 covering cells over open grass, four shots, no shimmer, no speckling
  and no bleed-through at any range or angle: the prefab's own 0.10 m depth already lifts the drawn
  slab clear of the plane, so `EmitFloor` needs no covering-specific lift and `SlabBuilt`'s measured
  1.1 mm seam is left alone. U42's estimate holds at one session with the drawing work struck out.

  **What the probe found instead was not in the estimate: grass grows through paving.** The tufts
  and the flower scatter still draw on a paved cell, because the scatter is keyed off the terrain
  and knows nothing about `Floor[]`, and it is unmistakable at every range. One condition where the
  scatter is gathered, presentation only, in neither a cell nor the save nor the hash — and a good
  argument for the probe having been worth running, since it is the sort of thing that would
  otherwise have been found by the owner on the day the feature was declared done.
- **U42, paving: the floor you lay on the ground (2026-09-17).** The owner reported a second time
  that they could not place a floor, with no rejection log this time. Two things came out of it.

  **First, a coverage gap that explains why this class of fault keeps escaping.** Nothing in the
  project tests that a *click* reaches the game. The Sim tier proves `ConstructionGrid.Place`, the
  EditMode tier proves `SlicePicker` and the seam between them, and between the two sit a rig, a
  presenter, a director and a gesture, any of which can swallow a press in silence.
  `FloorToolClickTests` was written to close it and **is ignored, because it cannot pass**: a
  PlayMode test's input update type is `Editor` and every edge property is gated on a player update,
  so `wasPressedThisFrame` never fires for game code — `MouseHarness` failure four, which
  `InputHarnessTests` has carried an ignored test about since `OQ-40`. `SliceCameraRig` reads exactly
  that edge. **Written and ignored rather than not written**, paired to the existing one, so the day
  the harness can press a button there is something to un-ignore. The control failing first is what
  proved it was the harness and not the game: had only the floor test failed, the obvious reading
  would have been the opposite and wrong.

  **Second, and the actual answer: paving.** A U29 floor is a structural slab and refuses a cell that
  already has a floor, which is what the ground is. What *"just build a floor"* means is a covering,
  and `docs/design/18-paving.md` had already scoped it. Built now, as `Building_DeckPlate`:

  - **The rule is `AllowsSlab` turned inside out and is genuinely two lines.** A covering wants a
    cell that **has** a floor — that floor is what it is laid on — and **never asks the support rule
    at all**, because whatever holds the ground up holds the covering up. It cannot fall, so there is
    no rule saying it cannot.
  - **No new state.** A fifth slab kind in `Floor[]`, which has always been saved and always been
    hashed. No new array, no save section, no hash coverage change, no catalogue row and no mesher
    edit — `FloorModule` draws any non-zero floor in its stuff's own tint.
  - **It takes the wall's lift, not the slab's.** A click on grass names the ground *block* and the
    covering goes in the air cell above it, which is what `StandingOn` already did. `WorkingLayer`
    stays **null** for it, or paving would break the moment the player scrolled a layer up.
  - **Grass no longer grows through it.** Found by `PavingProbe` before the unit was written and
    fixed where `EmitScatter` already refuses to draw under something solid, because a floor over a
    cell is the same argument. The kind is not examined: a built floor, a stamped deck and a deck
    plate all equally hide what is beneath.

  **Three places where the scope was wrong about the code, all recorded in `18` §10.** Steel was
  scoped and is not buildable at all — it is one of the four stuffs with no item, so a steel deck
  plate is an order that could never be filled; paving builds from wood or stone like everything
  else. `IsOurs` and `BuildingForSlab` are new, because `RemoveSlab` tested for `SlabBuilt` exactly
  and two kinds need one place that answers "is this ours" and a second that answers "which",
  since a deck plate refunds 3 and a floor 4. And a control had to grow before it meant anything:
  taking the ground from under one paved cell left it standing at `S_max - 1`, correctly, because
  the ground on every side still carries load sideways — it now clears `S_max + 1` cells around.
  **A test that fails for the right reason is worth more than one that passes for the wrong one.**

  - **Verified:** fast tier **578 Sim + 193 Hud** (five new), Unity EditMode **1,243 total, 1,234
    passed, 0 failed**, both content gates clean, the building fingerprint re-baked deliberately
    with what moved written beside it. `PavingProbe` re-shot against the real kind: a clean wooden
    deck at every range, no z-fighting, and the grass gone from the paved cells with the trees
    keeping their own unpaved squares.
  - **Still true and worth not forgetting: paving does nothing.** Walking speed, cleanliness, beauty
    and room stats are why it exists in the genre and none of them exist here, so it is a surface
    that looks different and that is all. Said in the scope before it was built and still true.
- **Slab and Floor: the rename, reversed the same day and better for it (owner, 2026-09-17).**
  Earlier that afternoon the owner answered "both say floor" and the recommendation to rename was
  recorded as overruled, with the failure mode it predicted written down beside it. The prediction
  came true three times in one session, ending in *"is a slab only supposed to be built at height —
  I'm so confused"*. Two reachable tools sharing a word cost more than the rename would have.

  **Settled: `Structure → Slab`, `Floors → Floor`.** The word lands on the thing a player means by
  it and that just works; "slab" is accurate and is already what `02-world-and-layers.md` says
  throughout. **The keys did not move** — `ui.arch.tool.roof` and `ui.arch.tool.deckplate` are
  forever — so it is two labels in `icon-keys.csv` and a rebuild, with no defs, no fingerprints and
  no save implications. The descriptions moved with them, and the old deck-plate line was wrong
  twice over once it became the default floor: *"Metal flooring. Fast to lay"* against a thing that
  builds in wood or stone.

  **Paving is now in two categories, and that was asked for rather than tidied in.** It belongs in
  `Floors`; it is **also** in `Structure` beside the wall and the slab, because *"it won't be painful
  having to go backwards and forwards between menus"* — a wall, its floor and the slab over it are
  one job and should be one row. Not a new idea in that table: `ui.arch.tool.reclaim` has sat in both
  `Structure` and `Salvage` since it was written, and `PaletteTools.TryGet` is keyed by the tool
  rather than by where it is drawn, so one key in two lists arms one tool and lights in both places.
  **The slab stays in `Structure` and nowhere else**, and a test says so: a slab under `Floors`
  would rebuild the confusion the rename exists to end.

  **The identifiers followed the labels; the handles deliberately did not.**
  `PaletteTools.Slab` and `PaletteTools.Paving` now read the way the screen does.
  `BuildingHandle.Floor` is still the slab and `BuildingHandle.DeckPlate` is still paving, because
  handle *values* are a save contract — swapping which constant means 2 and which means 3 would
  compile in silence and mean the other thing everywhere it was missed. A comment at both sites
  records the asymmetry rather than leaving it to be rediscovered.

  - **Verified:** fast tier **578 Sim + 195 Hud**, Unity EditMode **1,247 total, 1,238 passed, 0
    failed**, both content gates clean.
- **U43, the ladder: a second storey you can stand on (2026-09-17).** The owner played the build
  and asked the question that had not occurred to anybody: *"how do you even get up on the slab?"*

  **Measured before answering, and the answer was worse than "later work".** Every slab in the game
  came back **walkable and unreachable** — a lone slab on a wall, the corner of a roof, the middle
  of a roof. U29 shipped floors, collapse, rubble and a support model, and a colony could build a
  second storey, pull it down on itself, and never once stand on it. **Second storeys were
  decorative and nothing said so.**

  **Why:** vertical movement goes through a `Pathing.Connector`, connectors were produced by
  worldgen and registered once at world build, and nothing created one at run time. Stairs and
  ladders existed as edifices the generator stamps, with the climb pose live in presentation — but
  `BuildingOrder` held None, Wall, Floor, DeckPlate, so neither could be built, and the plan had no
  row for it because the vertical slice assumed the ruined city's own stairs. Fine for a city;
  useless on a meadow.

  **It was far cheaper than feared.** `NavGraph.AddConnector` and `RemoveConnector` already existed
  and already mark their cells dirty, so the incremental rebuild picks a new one up on its own. The
  unit is one `BuildingDef`, `blocking = false` so the cell can be stood in, and one idempotent
  `RefreshLadder` called from every place either end can change — the ladder going up, the floor
  above it going in, and either coming out. **Called from all four on purpose**: a player may build
  the ladder first or the floor first, and a rule that only worked in one order is a fault nobody
  could describe.

  **No save-format change, and that is the interesting half.** A built ladder is an edifice and
  edifices are saved; its connector is **derived**, rebuilt from the edifice list by
  `ColonyWorld.RebuildDerived` — the same argument that file already makes about structural support
  and the region graph. `NavGraph.OneCellConnectorAt` asks the graph rather than keeping a map from
  cell to connector id, because such a map would be empty after a load and the demolish path would
  quietly leave a portal behind wherever a loaded ladder used to be. `AWayUpSurvivesASaveAndALoad`
  is the test.

  **The wrong diagnosis it cost, recorded because it will happen again.** The first run of the
  headline test failed: the connector was registered, both ends walkable, and a full `Rebuild` did
  not help. Instrumenting rather than reading found it in one go — **`pawn mode Hauler`**.
  `Pawn.Mode` is the *current job's* mode and the colonist was mid-haul; `Connector` has always
  excluded haulers from a ladder (*"a hauler's bulky load and an animal's lack of hands both rule a
  ladder out"*). The feature was working and the test was asking in the wrong mode.

  **And that exclusion is a real consequence, not a detail.** A colonist can climb to an upper
  storey and **cannot carry building material up one**, so nothing can be built up there with a
  ladder alone. `AHaulerCannotClimbALadderSoNothingCanBeCarriedUpOne` pins it, so the next person
  reads the rule instead of rediscovering it the same way. **That is what makes stairs the next
  unit rather than a maybe** — `ConnectorKind.Stair` carries `AllMask`, and a stair is two cells
  rising 1.5 m each, so it wants a placement rule of its own.

  - **Verified:** fast tier **585 Sim + 199 Hud** (six new, `LadderTests`), both content gates
    clean, the building fingerprint re-baked deliberately with what moved beside it. **Nobody has
    pressed Play on a built ladder.**

### Two floors of different materials were the same floor (2026-09-17)

The owner: *"There is a bug as stone floors look like wood floors (or were built incorrectly) - and
I couldn't see the upper floor from the normal view still."* Two faults, and the interesting one is
the first, because the earlier answer to it — that the stone tint is nearly white and leaves the
brown prefab brown — was only the surface of it.

**The data was never wrong.** `Raise` converts the stuff handle to its value
(`StuffAt(_stuff[cell]).stuff`), `RaiseSlab` writes it to `FloorStuff`, the mesher reads it back as
`TintCode.Stuff`, and `StuffPalette` holds two plainly different colours — wood `(0.76, 0.59, 0.34)`
and stone `(0.86, 0.87, 0.88)`. Every step of that measured correct.

The fault was three lines in the scene generator:

```
Slab(ModuleIds.Slab,                   "SM_Bld_Base_Floor_Combined_01");
Slab("odyssey.module.slab.concrete",   "SM_Bld_Base_Floor_Combined_01");
Slab("odyssey.module.slab.deck",       "SM_Bld_Base_Floor_Combined_01");
```

Every floor id in the game resolved to the one wooden deck mesh. A slab took its mesh from the
*template's* group — and a colonist chooses the material long after the template is stamped, so the
material could only ever arrive as a tint. **A tint cannot separate wood from stone**: multiply only
darkens, so brown times near-white grey is browner. A stone floor was the wood deck 12% darker,
which is exactly what the owner saw.

So the material picks the mesh now. `ModuleIds.SlabOf(material)` gives wood the deck it already was
and stone the street tile (`SM_Env_Ground_Tile_Half_01` — one cell square, one material, already in
the build as Pavement, so its look is known rather than guessed). `WorldRenderModel` resolves the
two once into a table by stuff value and `FloorModule` prefers it over the group's slab.

**It only swaps when there is art to swap to.** An unknown module id does not resolve to nothing —
it resolves to a built-in primitive — so a table that trusted the resolver would have given every
clone without the licensed packs a bare block where the group's slab used to be. The row is kept
only if `UsesArt`; otherwise the template's slab stands, for both materials, exactly as before.
That is a test rather than a comment. The generator's concrete, steel and composite slabs have no
entry at all, so nothing in the ruined city moves.

**The second fault was a serialised value, and it was the second time.** `suppressActiveCeiling`
had already been flipped in code and the owner still could not see the floor above, because
`Play.unity` carried `suppressActiveCeiling: 1` from a build made before the flip and a serialised
value wins over a C# default. `BuildCamera` already carried that exact warning above
`selectionColour`; it now sets this one too, and the scene is corrected.

A trap found on the way out: rebuilding the module catalogue to pick up the two new rows erased the
61 colonists' atlas swatch rectangles — 665 insertions against 2,162 deletions, while the entry
count went reassuringly from 136 to 138. `docs/lessons.md` has it.

### A wall could not stand on a wall, and stone was steel with a different label (2026-09-17)

Three reports from one playtest. *"I've seen colonists go up ladders"* closes the ladder line.

**A wall on a wall.** *"On the next floor - I couldn't build a wall on top of the wall below, but
could on other tiles."* Reproduced before it was explained: a test that orders a wall, then asks
whether the cell above it will take another, failed on the first run. `ConstructionGrid.Allows`
asked `CellGrid.HasFloor`, which counts a slab at the boundary or solid terrain below and knows
nothing about what anybody has built — so the other tiles worked because the new storey's slab was
under them, and the wall's own head was the one place with neither.

**The support model had always disagreed with the rule.** `SupportSolver.IsGrounded` has counted
the cell above a blocking edifice as fully grounded since M1, so the wall would have stood
perfectly well; the order was refused for a reason the physics did not share. `SomethingUnderfoot`
is the permission catching up, not a new allowance — and it takes *blocking* edifices rather than
the solver's wider "any edifice at all", because the wider test is how a tree came to hold up a
roof, and felling first is a rule this file already had.

**The cursor was drawing nothing, and the comment said otherwise.** Yesterday's guard against
ghosting a ladder inside a wall returned early over any occupied cell, and its comment claimed the
refusal still read "because the cursor is red". It did not: hover draws nothing else, so the
pointer went blank over every wall and the player got no answer at all to "can I build here" —
worse than the wrong answer it replaced. The thing is still not drawn inside the obstruction; the
refusal is, as the drag's own red plate or box.

**And stone was steel.** *"The stone floor looks more like steel. I would expect a stone floor to
be more boring gray with some texture."* Put the two table entries side by side and there is
nothing to diagnose: steel is `(0.82, 0.86, 0.92)` and stone was `(0.86, 0.87, 0.88)` — the same
pale blue-grey, b over g over r, with **stone the brighter of the two**. Two materials the player
is asked to choose between were one colour with the labels swapped, and a near-white multiply
leaves whatever is under it looking polished, which is the one thing stone is not.

The replacement is not invented either. `StuffSolids` — the tint used where there is no art at all
— has held stone at `(0.52, 0.51, 0.49)` from the start: mid, warm-neutral, r over g over b. The
project had already decided what stone looks like and the over-art entry had never been made to
agree with it. `StuffPaletteTests` now asks the question that nobody was asking: every buildable
material is a measured distance from every other, stone is darker than steel and stone does not
lean blue.

**A standing test said the opposite, and it was half right.** `StoneStaysCoolWhileWoodIsWarm`
had pinned "stone is grey or cooler" since the wood tint was fixed. It was written to separate
stone from *wood*, which it does, and nothing in it had ever looked at steel — so the cool end it
permitted was precisely where steel already sat. The guarantee it exists for is untouched: wood is
warm, stone is not, and the two are still told apart by warmth rather than only by lightness. What
it no longer does is push stone into steel's corner in order to achieve that. `docs/lessons.md`
already has the rule this follows — a test anchored in a design fact fails when the design changes,
and that is correct — so the assertion was rewritten with the reason rather than deleted.

**One gap is recorded rather than asserted.** Concrete, steel and composite are within 0.09 of one
another: a pale near-white trio that the new test would fail on. None of them is buildable, so no
player is asked to choose between them and the ruined city is meant to be uniform anyway. The test
walks `ConstructionContent.IsBuildable` rather than a list written by hand, so the day one of them
gets an item it starts failing — which is the right moment to care, and is exactly the comparison
nobody had made for stone.

**The mesh is as good as this pack gets, and that is worth writing down.** A contact sheet of all
eleven cell-sized floor prefabs says the Synty packs contain **no stone floor**: the
`SM_Env_Ground_Tile_Half_*` family is sci-fi street plating — cross grooves, a manhole, notched
recesses — and `SM_Bld_Base_Floor_01`, the two-triangle flat quad that looked like the neutral
option on paper, is **wooden planks**. `Half_01` is the plainest of the five and is what stone
uses. A flat stone slab is a genuine gap of the kind `CLAUDE.md` reserves Blender for; nobody has
been asked yet.

### A paused world would not take an order (2026-09-17)

The owner: *"if I pause the game, go up a depth and create a slab, I place the slab but then
nothing appears until I press play - I can see the build selection for the slab."*

Not a rendering fault and not the construction grid. `SimWorld.RepublishViews` — the escape hatch
built so a paused world could answer a question about a cell — drained `QueryCell` and nothing
else, on an argument it stated outright: *"a command left pending stays pending, and is applied at
the next real tick as always."* So the order reached the queue, the queue was only drained by a
tick, and a paused world took the command and showed nothing for it. The build cursor still drew,
because the cursor is presentation and never asked the simulation anything — which is exactly why
it looked like a rendering bug from the outside.

**The caution in that sentence was right; its scope was too wide.** A question cannot desync a hash
because it changes nothing. A command does change hashed state — but while the clock is stopped
*nothing else runs*, so applying a player's order the moment it is given produces exactly the state
the next tick's drain would have produced, and the boundary the hash is taken at is unchanged. One
thing genuinely improves: a save written while paused now contains the orders the player has just
given, which it did not before.

`PausedIntents.AppliesWhilePaused` names the set rather than growing a second special case beside
`QueryCell`. In it: the questions, and the player's orders over a cell or a colonist — `Designate`,
`CancelDesignation`, `SetForbidden`, `PlaceBuilding`, `CancelBuilding`, `ForceJob`. The test is
whether an intent writes state the player authored and needs no system to finish it. Out of it:
`SetGameSpeed`, which keeps its own path because unpausing is what spends the tick and routing it
here would be circular, and `SetSliceLayer`, which is presentation state the simulation need never
hear about off-boundary.

`ASlabOrderedOnAPausedWorldIsThereWithoutATick` spends no tick on purpose — a tick would pass
whether the bug were fixed or not — and it was checked the only way worth checking: put the old
one-kind predicate back and it fails, restore the fix and it passes. Its first version failed for
the wrong reason and that was useful too: it ordered the slab on open ground, which `AllowsSlab`
refuses because ground already is a floor. The owner's case is a storey up, over a wall.

**And the meta-file warning, which is not the hitch.** *"Asset
Packages/com.unity.render-pipelines.universal/Tests/Editor/.../ReadonlyMaterialConverterTests.*.cs
has no meta file, but it's in an immutable folder."* Counted across every log in the project: 14
occurrences, of exactly two files, both URP's own editor-test sources. It fires on asset-database
refresh and not per frame, so it cannot be what stops or hitches a running game. Nothing in this
repository can fix it either — the folder is immutable by definition, and the consequence is that
two URP test files are ignored, which is what we want. If the noise is unwelcome, letting Unity
re-resolve the package (remove its `Library/PackageCache` folder and reopen) is the remedy; the
hitching wants measuring in PlayMode, where frame time is the only place it is ever measured.

### The start menu took the build cursor with it (2026-09-17)

The owner, over three rounds: the build cursor does not appear, in a new game or a loaded one; then
*"this is happening on other builds - did the new menus bust something? it used to highlight say -
the wall immediately onto the placement area, but it's completely not visible anymore."*

That last sentence is the one that solved it. Everything before it had been read as a fault in *this*
branch, and it was not on this branch at all.

`TeardownSession` dropped `_designate` along with the renderer, the render mirror, the colony and the
world. Those four are built by a session and must not outlive one. The presenter is not: it is a
sibling component on the same GameObject, found once by `WarnIfTheSceneIsStale` in `Start`, and
`Start` does not run twice. So the first teardown set the reference to null and nothing ever looked
for it again.

`DrawToolPreview` opens with `if (_renderer == null || _designate == null) return;`. From that
moment the build cursor, the drag box and the run's ghosts were all gone together — which is why the
report was "no highlight at all" rather than anything subtler.

**It was harmless until the menu existed.** A session used to be built once at `Start` and never
torn down, so the null was unreachable. `MS` made teardown-and-rebuild the ordinary way into a game:
every New game, every Load. The bug did not change; the path through it became the only path.

**Two things made it expensive, and both are worth keeping in mind.** The diagnostics added over the
previous two commits reported nothing, and I read that as "the cursor path is fine" when it meant
"the cursor path is not being entered" — the guards all live inside `DrawHoverGhost`, one level below
the return that was firing. Silence from an instrument is data about the instrument first. And every
PlayMode test in the suite builds exactly one session, so not one of them could see a fault that
begins at the second. `TheCursorSurvivesATeardownAndRebuild` asserts the ordinary path, and it was
checked the only way worth checking: put the null back, watch it fail on "the cursor path says
nothing at all", take it out again.

Six other causes were checked and cleared along the way, and they are worth not re-checking: the
rig's hover branch, the preview gate, the HUD re-adopting directors by identity after a rebuild,
`PointOverUi` claiming the whole screen, every palette chip arming its tool (now a fast-tier test),
and the ghost being drawn at an unseen layer — the owner's own guess, disproved by
`[Cursor] drawn: pointer L6 -> ghost L6, camera L6`.

- **A skill level has never meant anything, and now there is a plan for what it should mean
  (2026-09-17).** The owner asked whether anything determines that a better woodcutter chops
  faster and swings faster while their experience creeps up, and then, in the same breath, asked
  for a move speed that varies between characters and depends on their condition and health.
  Answering the first honestly took reading the code rather than the plan, and the answer was
  **half**.
  - **Experience is complete and correct.** `JobDriver.Work()` pays on the ticks that are work and
    not on the walk to it; the gain is base × learning factor × passion × the over-cap factor; the
    level is read off a Def table and never stored; it decays above ten; it is saved and hashed;
    `U37` now rolls a starting level. None of that was in question.
  - **Nothing reads the level back out**, and it is the same one line in four places:
    `AddWork(cell, 1)`. A level-20 miner and a level-0 miner clear the same rock in the same 700
    ticks. `15-skills.md` §1 had already said so in plain words — *"Nothing reads a skill level
    yet. Not work speed, not yield, not quality"* — and left it there, because that document was
    about icons. **`a-08-plants-growing-food.md` had gone further and written the instruction**:
    *"when OQ-14 lands skills, the felling driver multiplies work by the plant-work-speed curve"*.
    OQ-14 landed in the overnight queue; the multiplication did not; nothing connected the two,
    and no test could have, because a research recommendation is prose.
  - **Move speed could not have been built at all as the numbers stand, and the content file
    already knew.** `movePerTick` is `1` against a cell cost of `100`, so the only speeds
    expressible are 1.5, 3.0 and 4.5 m/s — there is no room between them for "this colonist is a
    little quicker". `Colonist.xml:36` had diagnosed it in a comment months ago: *"A pace between
    these integers wants the cost scale raised, not a fraction stored."* Nobody had needed the
    room until the owner asked for it.
  - **So the two asks are one mechanism**, which is why they became one design
    (`docs/design/17-rates-and-stats.md`) rather than two features: a per-pawn, per-activity rate
    in thousandths, multiplied into an accumulator. The decision that makes it cheap is **scale
    the accumulator, not the content**. `_work[cell]` and `MoveProgress` count thousandths and
    every comparison reads `cost × 1,000`; not one authored number in `Terrain.xml`, `Jobs.xml`,
    `ConstructionContent` or `MoveCost` moves, so both content fingerprints hold, no path changes,
    and `HopPriceHasOneOwnerTests` is not fought with. Two saved integers change scale and nothing
    else does.
  - **The alternative was ruled out by a decision already taken.** Dividing a job's total work by
    the worker's speed when the job starts is the obvious reading, and it is impossible here:
    **work is banked on the cell, not on the job** (`DesignationGrid`, "a miner who stopped for a
    meal took the whole morning's work with it"), so a cell worked by two colonists of different
    skill must accumulate in a unit that means the same to both.
  - **The research that was fetched corrected one of our own files.** One capped subagent
    (`docs/research/work-speed-and-stats.md`) found that every work-speed curve in the reference
    is dead linear with no diminishing returns, and that **every slope is chosen so level 8 reads
    exactly 100%** — mining steepest at 61× novice to master, construction shallowest at 6.8×
    because there skill buys quality rather than throughput, hauling with no skill speed at all.
    That invariant settles a disagreement without a third source: `a-04-building-and-materials.md`
    had construction at 50% + 15 points a level, which puts level 8 at 170%, and it is struck
    through and corrected in place. Nothing was built on it — no code had ever read a construction
    speed.
  - **The finding that changes the design, rather than filling it in: our colonists are not the
    reference's.** Its curves are anchored on a level-8 colonist; `Colonist.xml`'s roll has a
    **mean of 1.16**, so taking the curves verbatim would fell at 19.5% and mine at 16% — a 5–6×
    brake on every colonist in the game, which is not a balance tweak but a different game, and
    it would have been discovered as a failing soak rather than as a decision. The design
    therefore **re-anchors on our own average colonist and keeps the reference's relative
    character** (mining steepest, construction shallowest, hauling flat), which puts a novice at
    0.55–0.7× and a master at about 2.5×. Eight integers, all INVENTED, and the owner's to argue
    with at the keyboard.
  - **Hauling being flat is two arguments meeting.** The reference's general-labour stat has no
    skill term, and `15-skills.md` §6 had independently concluded — from an icon sheet — that
    hauling is a work type and not a skill. Two lines of reasoning arriving at the same place is
    the strongest evidence in the document.
  - **The swing is presentation's and stays presentation's.** `figure.SwingClock += deltaTime`
    becomes `+= deltaTime * rate / 1000`, one line, and the chips and impact audio follow for free
    off `BlowLanded`. The tighter design — one blow, one quantum of work — was refused for the
    reason `JobDef.settleTicks` records: a presentation constant in the tick, the save and the
    hash. Work stays continuous, the swing is scaled to match, and the two agree in aggregate
    without either owning the other.
  - **One honest argument against part of it, recorded rather than buried.** The reference *had* a
    mood-driven work-speed bonus and **removed** it, on the reasoning that mood should produce
    visible events rather than an invisible percentage tax. The design proposes exactly such a tax
    for movement (exhaustion and starvation, floored at ×0.70 so there is no death spiral). The
    difference it relies on is that ours will be visible in the inspect pane; if that turns out not
    to be enough in play, §3e is the reason to drop it rather than tune it.
  - **Planned as `U42`–`U45`** in `vertical-slice.md` §WS and `OQ-51`–`OQ-54` in the queue, in that
    order and beside M3 rather than inside it, because it moves the economy the ten-day gate
    measures. `U42` lands alone and its done criterion is **that nothing changes** — every golden,
    every path checksum and `OneDay` identical — which is what will make the deliberate re-bakes
    at `U43` and `U44` readable as tuning rather than as drift.
- **The three rate questions answered, and one of them widened the design (owner, 2026-09-17).**
  Asked the three questions `17-rates-and-stats.md` could not settle headless, the owner took the
  proposed curve anchor as a starting point (*"sure we start somewhere"*), said condition should
  bite (*"if exhausted, starving etc — all has an effect"*), parked running (*"not sure yet"*), and
  gave the governing rule for the whole line: *"use RimWorld as a rough reference to how this
  could work well."*
  - **The condition answer is the one that changed the design rather than confirming it.** As
    written, condition multiplied the *move* rate only. "All has an effect" reads wider than that,
    so it now multiplies **both** rates from a single `ConditionPerMille()` — one computation, one
    floor, two consumers, and one place to look when asking why a colonist is slow. The cost is
    that the starvation spiral the floor exists to prevent gained a second turn: a hungry colonist
    now also cooks and chops more slowly, so the soak comparison that was a sensible check is
    `U44`'s **done criterion**.
  - **"Rough reference" is now written into the design as a rule rather than left as a habit**
    (`17-rates-and-stats.md`, intro): shape, structure and intent taken — the linear curve, the
    counter model, the composition order, capacity weighting, the relative character of the skills,
    and even the decisions the reference *unmade*; constants re-anchored only where our own numbers
    differ, which is the one departure and is §3b's; never a name, a line of text, a Def or a line
    of code. A later session can hold the document to that.
  - **Running is held, deliberately and cheaply.** The capability is a multiplier on a rate `U44`
    already produces and the gait blend already turns it into a run above ~2 m/s, so leaving it
    unbuilt costs nothing and building it now would mean inventing an urgency model to justify it.
    `OQ-54` is marked blocked with that reason rather than left open to be picked up by a session
    looking for work.
- **"Use the reference roughly" was taken literally enough to check it, and it overturned half a
  decision that had just been made (2026-09-17).** The owner's steer prompted one capped follow-up
  question: do exhaustion and starvation actually slow a colonist in the reference, and by what
  path? The guess being tested was that they do, but through **health capacities** rather than
  mood — which would reconcile the owner's "all has an effect" with the earlier finding that the
  mood-driven work-speed bonus was deliberately removed.
  - **Half right, and the wrong half was the more useful finding.** Hunger behaves exactly as
    guessed: at zero food it stops being a need and becomes a *condition with a severity bar*,
    whose entire mechanical action is an **offset to consciousness** (−10/−20/−30%), and
    consciousness feeds both moving and manipulation. One offset, both rates, and the mood hit
    rides alongside causing none of it. **Exhaustion does not slow anybody down at all** — the rest
    bands touch mood and disease immunity and nothing else, and at zero rest the colonist collapses
    and sleeps where it stands.
  - **So it is one philosophy applied twice, and the mood removal was the third instance:** a
    condition either does nothing to your rate or it produces a visible, discrete event; the
    invisible percentage tax is refused systematically. Starvation is the exception that proves it
    and is allowed to slow you only because it has crossed out of being a need and become an
    injury.
  - **The design followed the finding rather than the draft.** `ConditionPerMille()` is now one
    consciousness-like scalar that starvation offsets, so neither rate ever learns hunger exists
    and M4's capacities will substitute for it rather than requiring a rewrite; it is floored at
    700 and **ceilinged at 1,000**, the reference's asymmetry — dulled slows you, alert never
    speeds you up — which is what stops a future "well fed" bonus quietly becoming a speed boost.
    The compounding that the draft had worried about is now evidence: one scalar on both rates
    means a starving colonist runs a walk-then-work round trip at about 0.49 throughput, which is
    what the reference does at severe malnutrition.
  - **Exhaustion lost its slowdown and gained a collapse**, which is a departure from the literal
    reading of the owner's answer and is flagged in §7 for veto rather than assumed. It is also
    smaller than it sounds: `SleepJobDriver` already owns `Pawn.Asleep` and nothing in the game can
    collapse today, so the whole of it is "at zero rest, sleep here instead of walking there" —
    with the control that a merely tired colonist still walks to a bed, because the failure mode is
    a colony that sleeps in the mud.
  - **The floor is ours and is honest about being a departure.** The reference has no soft landing:
    it has thresholds, and below 30% consciousness the colonist is unconscious. We have no downed
    state to fall through, so 700 stands in for one and should give way to a threshold the day
    health exists.
- **The rates design audited against the code, and it was wrong in five places (2026-09-17).** Asked
  to check the documentation for gaps, the useful move was not to read the documents against each
  other but to read them against the code. The design had been written from the simulation's side
  alone and had named none of what follows.
  - **`minSkill` already reads a skill level, so "nothing reads a level" was false.**
    `BuildWorkGiver.CanBuild` refuses to offer a site to a colonist below the building's `minSkill`
    (`BuildJob.cs:213`). It is inert — every shipped building is `minSkill = 0` — but it is not
    nothing, and it matters because it is a **gate, not a rate**. That is the reference's own
    division: a skill drives either what you may attempt or how fast you do it, and they are two
    mechanisms. This game had the first and not the second, which is a better description of the
    gap than the one three documents were carrying. Corrected in the design, in `15-skills.md` and
    in `15-building.md`.
  - **Scaling an internal accumulator by 1,000 breaks four things outside `Sim`, and all four are
    silent.** `CellDetail.WorkToClear` is a **`ushort`** and the dearest terrain costs 2,400, so
    ×1,000 wraps. `SiteView.WorkDone`/`WorkTotal` are documented as "real ticks" and drive *"about
    12s left"* in the pane, so publishing milliwork multiplies every estimate in the interface by a
    thousand. `DesignationGrid.Fraction()` divides banked work by a cost that lives in another
    class, so one side scales and the other does not and every progress bar fills a thousand times
    too fast. And `PawnRegistry`'s `movePercent = MoveProgress × 100 / MoveStepCost` is a **ratio**
    whose halves are assigned in different files — scale one and every figure teleports.
  - **So the rule the design needed and did not have: the scale stops at the contract.** Internally
    thousandths; across the sim→UI seam and in front of a human, ticks-at-standard-rate. Written up
    as §2bb and folded into `U42`'s done criteria, which now include every Hud readout test.
  - **One consequence is a wording question rather than a bug.** The tile readout has said
    `walk speed = 100%` since cell inspection shipped, and it is a fact about the **cell** — the
    terrain's crossing cost — not about anybody standing on it. A per-pawn move rate under the same
    words would put two meanings of "walk speed" in one interface, which is the double-counting
    trap in user-facing form. The cell keeps the phrase; the pawn wants different words. And
    *"about 12s of work"*, exact since the day it shipped, becomes "for a standard colonist".
  - **The lesson that generalises is in `lessons.md`:** a recommendation in a research file is
    enforced by nothing — `a-08` had written the instruction to do this work and it went unread for
    months — and **a design that changes a unit has to be walked to every place that unit is read**,
    which is ten minutes with `git grep` against a session spent discovering a `ushort` by watching
    a progress bar wrap.
  - **Cross-references added so the next session finds this from wherever it starts:**
    `05-ai-and-jobs.md` (the cost-prices-the-cell twin of the terrain-cost trap),
    `04-data-model.md` (where the new Def fields land, and that the scale reaches none of them),
    `08-milestones.md` (M2 delivered skills and a level still has no consequence),
    `10-ui-panel-catalogue.md` (the Skills tab gains what a level is worth),
    `15-building.md` (its tick figures become rate-relative), and a dated note on
    `a-08-plants-growing-food.md` recording that its own recommendation was never carried out.

- **Picking something up costs time again, 2026-09-17**, and the decision it reverses is a year old
  by the project's own clock — one day. The owner played it and said *"when picking up — it happens
  quickly in a stride — I think there should be time spent motion down, picking up object and
  standing up"*. **The motion was never missing.** `Gesture.Lift` has been a solved crouch of
  exactly 0.8 s since the gesture line landed: pelvis down, both legs IK'd back to the feet the
  gait had already put down, down fast, hold, up slow. What was missing is that the simulation
  spent none of it — `TakeUp` was one tick — so the pawn's next toil began while its figure was
  still straightening, and the figure walked away mid-rise. `TakeUp`'s own doc comment said so, in
  as many words, and called it *the accepted price of the owner's decision (2026-09-16) to keep the
  duration out of the simulation*. It stopped being accepted when somebody watched it.
- **A comment admitting a fault is not a defence against the fault.** This is the second time in two
  days that the thing the owner reported was already written down beside the code causing it — the
  first was `ScenarioDef.Playtest`'s note promising to stop giving starting orders "when the
  designate tool lands", months after it had. The pattern is worth naming: a note saying what to do
  when a thing changes does not do it, and neither does a note explaining why something looks
  wrong.
- **It became a toil rather than a number.** The instant `TakeUp` is gone entirely, replaced by
  `JobDriver.LiftToil` — stoop, grasp, rise — and there is deliberately no instant form left,
  because a driver that wanted one would be a driver whose colonist acquires things by magic while
  standing upright. Both carriers (haul, delivery) are one line each, so they cannot come to spend
  different amounts of time on the same motion. **The grasp is in the middle**, at 24 of 48 ticks,
  which is the middle of the drawn gesture's hold window: transfer at the start and the pile
  vanishes while the colonist is still upright; transfer at the end and it vanishes after they are
  upright again. Both are the magic the stoop exists to prevent, arrived at from opposite sides.
- **The number is a simulation constant chosen to cover a drawing constant**, which is the same
  uncomfortable coupling `JobDef.settleTicks` already carries and was argued out at the time. The
  alternative is presentation reaching into job timing. It lives on the colonist rather than on a
  job, because a lift is a lift — a harvest and a butcher's will want this number, not their own.
- **All three goldens moved and the re-bake was checked rather than trusted.** Every `Simulated`
  value moved; **no `Generated` one did**, which is the signature the change should have — the
  duration is content, content is not hashed, placement is untouched. Verified by running the table
  before re-baking and reading *which* assertion failed: all three failed on the second, and the
  second only fires once the first has passed. A moved `Generated` would have meant something else
  had come along for the ride.

- **The jolting near water: the first suspect was measured and it is not the answer, 2026-09-17.**
  The owner reported colonists that *"keep snapping in 2 directions quickly"*, mostly near water.
  Reading the code gave two candidates and the standing rule is that reading code has been wrong
  every time, so `WalkHeadingMeasurementTests` measured the likelier one first. The argument was
  good: `PathFinder` is orthogonal-only by design, there is **no path smoothing anywhere** in
  `Odyssey.Sim.Pathing`, and a drawn bearing is the raw step vector — so a diagonal journey should
  come out as a staircase and the figure should turn ninety degrees every cell.
- **It does not.** Open ground, 30 by 30 diagonal: **60 steps, 5 turns**. Forced along a diagonal
  band of impassable cells, which is what a stream looks like to a path: **60 steps, 7 turns**. A
  turn every fourteen to twenty seconds of walking is not what anybody saw. The reason is that on a
  4-connected grid every monotone path between two points costs exactly the same, so the search is
  free to break ties however it likes — and it spends that freedom on long straight runs. **A
  correct-sounding mechanism that the code genuinely contains can still produce none of the
  behaviour it predicts**, and the only way to know was to count.
- **What is left is the second suspect**, and it is per-step rather than per-journey:
  `PawnPose.OnTheDrawnGround` chooses which cell's relief the figure stands on with
  `CellRef over = t < 0.5f ? pawn.Cell : pawn.NextCell` — a hard switch at the midpoint of every
  step. Where that clamp is active it snaps the drawn height, and `ObserveSpeed` differences
  position frame to frame, so one snapped frame can kick the gait blend as well. It clusters at
  shorelines because that is where banks and water cells meet, which is the "around water" in the
  report. Measuring it needs a `WorldRenderModel`, so it is a Unity-tier measurement.

- **The second suspect was measured, and it was the fault. 2026-09-17.** `WalkOnReliefTests`:
  **81.9 mm of drawn height in a single sample, at phase 0.495 of the step**, on open rolling
  ground with no bank anywhere near it — against the **25 mm** an honest frame of walking carries a
  colonist. Three and a quarter frames of travel, vertically, once a step, on every cell of the
  board. `PawnPose.OnTheDrawnGround` was comparing the walker's own height — sampled where she is —
  against the ground height sampled at the **centre of whichever cell she was over**, and `over`
  flips at the midpoint. Sampling the relief where the walker is instead took it to **1.6 mm**, and
  the worst sample moved off the midpoint entirely, to phase 0.175, which is just ordinary
  ground-following.
- **Why five thorough tests never saw it, which is the part worth keeping.** `BankFootingTests`
  owns this exact question, measures it with this exact instrument — four hundred samples a step —
  and covers five crossings of a terrace. And `GroundRelief.Reset()` sets `Amplitude` to zero,
  every one of those cases inherits it from the fixture, and at zero amplitude `GroundRelief.Lift`
  returns its argument unchanged. **The whole continuity suite has only ever run on a perfectly
  flat field**, while the board the game loads has a 2 m one everywhere. The one test in that file
  that does switch the field on asks where a *standing* figure is, not whether a walking one moves
  smoothly. A fixture-wide default is a silent precondition on every test in the file, and this one
  disabled the thing the file exists to measure.
- **A smaller, older thing was found beside it and deliberately not folded in.** Crossing between a
  bank cell and the flat ground next to it jumps **28.7 mm** with the field on — and **30.0 mm with
  it off**, so the relief is not the cause and is marginally kinder. That one belongs to the bank
  surface, predates all of this, and sits under the 50 mm budget `BankFootingTests` has always
  asserted, which is why nothing has ever reported it. It is about one and a fifth frames of
  walking against the fault's three and a quarter. The test that found it asserts **"the rolling
  field adds nothing"** rather than an absolute budget, because an absolute budget at one frame of
  travel fails on the flat control too — it would have been a test that blamed the relief for
  something the relief does not do. The 30 mm is recorded as its own open question.
- **The control was measured because it was cheap, not because it was expected to matter.** It
  reversed the reading of the number entirely: without the flat comparison, 28.7 mm looks like a
  second relief bug and somebody spends an afternoon on it.

- **The water depth experiment was run, 2026-09-17** — `WaterDepthCheck`, the first item in the
  swimming design's own order of work, photographing a 1.8 m post standing on the bed of a stream
  beside a second post of the same height on the bank, at four surface heights. 0.72 (ships) is
  2.16 m and over a colonist's head; 0.50 is chest deep; 0.30 is 0.90 m and waist deep; 0.15 is
  knee deep. **If the owner takes 0.30, shallow water is waded upright and only deep water needs a
  swim pose**, which is half the work in that design.
- **Three framings were wrong before one was right, and the reasons are worth having.** A low
  camera near a stream looks *through* the bank, because a stream is cut into a channel — 12
  degrees at 10 m and 14 at 9 m both put the lens inside the ground. The post was banded over its
  lower metre, which is under every waterline being compared, so at 0.30 every band was submerged
  and the post read as plain and at 0.72 it read as absent; it is banded the whole way up now. And
  both posts were the same colour, so the one visible post could not be identified from the
  picture. **A photograph taken to answer a question has to be checked for whether it answers it**,
  which is the same discipline as re-running a test after a fix.
- **And the tool turned up a correction to the design it was written for.** Shallow water is not
  free to cross and never has been: `CostClassShallowWater` is 200, making a shallow cell 300
  against a flat cell's 100 — *exactly a third speed*, with a comment in `NaturalContent` saying
  so. The cost model already has a place for "water is slow" and uses it. What it has no place for
  is what a colonist *looks like* while being slowed, which is the whole of what is actually
  missing.

- **Colonists float and swim now, 2026-09-17, and the owner had to report it twice to get there.**
  After the depth experiment came back as four photographs and an open question, they played again:
  *"I saw someone walk under water again when it was 1 deep? — it's meant to float when this shallow
  if possible."* The first round had measured the problem carefully and **built nothing**, which is
  the right call when a decision is genuinely the owner's and the wrong one when the decision was
  only ever about how deep to draw the water. The ruling settled it in the other direction: do not
  lower the water, float in shallow too.
- **And the second ruling is what made it small.** *"Float is how it looks; shallow stays
  crossable"* — so in shallow water the float is a drawing and nothing else. No Def change, no cost
  change, **no golden moved**, nothing in the save or the hash. The entire shallow-water half is
  `WaterLine`, `SwimPose` and one branch in the director's pose dispatch. The alternative reading —
  that a swimmer is a swimmer everywhere — would have made every brook on the played board a wall
  that haulers route around, which is a large change to how a colony moves and was worth one
  question to avoid guessing at.
- **The draught was wrong by a hip height, and a photograph is what said so.** It is measured to the
  figure's root, which is at the feet; the pose tips the body about the **hips**, roughly 0.9 m
  higher. Reasoning "a floating body sits just under the surface" gave 0.25 m, which is true of the
  body and false of the root: the torso ended 0.65 m clear of the water and the colonist lay on the
  stream like a raft. 1.0 m sinks it to the waterline, head and shoulders out, legs visible
  trailing under the surface. **Nothing in the test suite could have caught this** — every
  assertion was about the root, and the root was exactly where it was asked to be.
- **The first sheet photographed the pose on grass with the weight forced on, and that answered half
  the question.** It showed the shape is a swim; it could not show how the shape sits against the
  waterline, which is the whole of what was reported — and a prone figure floating over a meadow
  reads as a body rather than as a swimmer, inviting a judgement the picture had no business
  inviting. Re-shot with a colonist spawned in a real stream. **Pinned there every tick**, because a
  colonist with nothing to do wanders and walks out of the water within seconds, which is correct
  behaviour and the wrong photograph.
- **The gait had to be told to stop walking, which is the climb's old fault arriving from the other
  side.** A swimmer *has* ground speed, and the mixer reads speed, so without a fade the figure
  strides along the surface of the water. The climb had the mirror image: a purely vertical step has
  *no* ground speed, so the mixer played the idle and a colonist went up a shaft standing to
  attention until the legs were bound. **Ground speed is a poor proxy for what the legs are doing,
  and every pose that is not walking has to say so.**

- **Getting out of the water, 2026-09-17.** The owner played the float: *"getting into the water
  looks fine, but getting out — the colonist ends up clipped and sunk half way into a terrain tile
  — is it possible the lift out of the water happens earlier or reaches the edge and pulls up"*.
  The cause was that the float was **added on top of** the ordinary ground clamp, and the two
  disagreed about when the step happens: the float decayed evenly across the step while the clamp
  jumped to the arriving cell at the midpoint. So a colonist leaving a channel spent the first half
  of the step *below* the bank, inside it, and the second half *above* it, having overshot by the
  float it had not yet lost. **Two faults from one composition, and the owner saw the first.**
- **The rule that replaced it is the owner's own sentence turned into arithmetic.** A step with
  water at either end is drawn by interpolating its two *resting heights*, because **between a
  waterline and the bank above it there is no drawn surface to follow** — ground-following is the
  right answer everywhere it has ground and there is none here. And the whole vertical change
  happens in the **water half** of the step: risen by the edge on the way out, not started until the
  edge on the way in. Both are what a person does, and both are exactly what keeps the figure out of
  the block it is climbing.
- **Measured on a generated board rather than a fixture, and the fixture is why.** Across **all 341
  places a colonist can climb out** on a 64 × 64 wooded map, the deepest it is ever inside the
  ground of the cell it is climbing into, while over that cell, is **0 cm**. The hand-built flat
  fixture could not have said so: with the bed at the same layer as the land beside it the waterline
  stands *above* the bank, so climbing out is a descent there and every claim about not overshooting
  is inverted — which is how the first version of that test failed. The crossing tests moved to a
  cut channel.
- **The pull-up is fast and the rate is not a free choice.** Finishing by the midpoint is what keeps
  the figure out of the block, so the rate falls out of the geometry: median 55 mm a frame, worst
  106 mm on a 3.53 m exit. Slowing it means finishing later, which puts the figure back inside the
  bank. The lever, if it ever needs one, is the step's own duration in the simulation.
- **The second report was the interesting one, because the obvious answer is wrong.** The owner also
  saw snapping *"as the colonist walks along the shoreline it just came out of"*. Drawn height was
  the obvious culprit and it is **falsified**: profiled across every one of 223 steps along a real
  generated shore, the worst step is a perfectly even ramp — 31.45 m to 31.61 m in identical 0.031 m
  samples — and the hand-over gap to the next step is **0.0 mm**. Thirty-one millimetres a frame is
  walking up a slope, and the 25 mm "one frame of walking" yardstick measures *horizontal* travel,
  so a slope beats it honestly. **A budget borrowed from one axis will accuse the other of a fault
  it does not have.**
- **What is left is the footing hand-over, and it was a genuine binary.** `ApplyFooting` skipped the
  whole pass while the swim weight was above a threshold, so the footing arrived **complete on one
  frame** as a colonist came ashore: hips dropping and both feet planting between one frame and the
  next, every time. It is multiplied by `1 - SwimWeight` now. **It is a hypothesis, not a
  demonstration** — the drawn-height explanation was measured and killed, and this one cannot be
  measured the same way because it is a rig pose rather than arithmetic. It wants a playtest.

- **Swimming is designed and not built, 2026-09-17** — `docs/design/20-swimming-and-water.md`. The
  owner asked whether colonists could float on top of the water, *"still vulnerable"*, with a
  paddling animation. **They are not walking through water, they are walking along the bottom of
  it:** both water rows are `solid=false` so the bed is the floor, and `ChunkMesher.WaterSurface`
  is 0.72 of a 3 m cell — **2.16 m of water over a 1.8 m person.** Deep water's `impassable` is the
  only thing that has been keeping them out of the middle of lakes.
- Four decisions taken: float and paddle rather than wade; deep water passable **but priced so a
  route round almost always wins**; slow and helpless with no drowning; and a load never goes into
  the water.
- **The last two fit machinery that already exists, which is the useful find.** `TraverseMode.Hauler`
  is there precisely because *what a colonist is carrying changes where it may go* — it forbids
  ladders. Deep water is the second instance, so "a colonist will not swim while carrying" is one
  mode rule: a haul path is planned in `Hauler` mode and therefore never crosses water, there is no
  drop-on-the-bank step to write, and a load cannot end up in a lake by accident. Swimming is
  likewise **not** a pawn state with a flag — the cell is already the authority, and a flag would be
  a second copy of a fact that can fall out of step with where the pawn actually is.
- **And the design found a question the owner has not been asked.** At 2.16 m, *shallow* water is
  over a colonist's head too, so on these decisions both depths are swum. Lowering `WaterSurface`
  to about 0.3 would make shallow water genuinely shallow and wadeable upright, leaving only deep
  water swum — one number, and it would halve the pose work. It is the first item in the design's
  order of work for that reason.
- **Water got sides, and the thing everybody was looking at turned out not to be water (owner
  report, 2026-09-17; `docs/research/d-15-water-body-rendering.md`).** The report came with four
  screenshots of a stream stepping down the terraced board: water "in mid air", strange gaps, water
  that "can't handle being at height", and - the owner's own guess - "maybe it's just a visual
  problem".
  - **The generator was innocent, and measuring it first is what made the rest cheap.** A throwaway
    probe over `MakeWooded()` at 120 x 120 x 16 on three seeds: **0 water cells with air beneath**
    on every seed, and the **greatest drop between adjacent water columns is 1 layer**, also on
    every seed. Both of ADR 0009's invariants hold exactly as decision 4 claims. `WaterFillPass`'s
    own comment had already said it - *"Depth is a rendering problem, not a geometry problem"* -
    and the measurement is what made it safe to believe rather than merely quote. The probe was
    deleted; its numbers are in `d-15`.
  - **The cause was one line.** `WorldRenderModel.ResolveTerrain` gives every non-solid terrain
    `ModuleShape.FloorSlab`, water is non-solid, so water was a floor slab - raised to 0.72 of its
    cell by `WaterContributor` and given no sides. What was drawn for a water cell was a lid
    **2.16 m above its own bed with open air between**. The void is walled only where the neighbour
    is a dry bank, which the generator puts exactly one layer up; wherever the neighbour is lower,
    the void is in plain view. That is the whole of "water in mid air", and the dry 3 m between two
    lids at a step is the whole of "water should fall down".
  - **One skirt, two triggers, because they are the same geometry.** A face goes on a side when the
    thing beside it is not water at the same level: with nothing there it closes the channel, and
    with water one layer down the same sheet spans the step. It is the `GroundFace` idiom - ground
    already has a separate shape for "the same block where a side of it can be seen" - rather than
    a new mechanism.
  - **The shader would have eaten the entire feature in silence.** `OdysseyWater.shader` carried
    `clip(input.normalWS.y - 0.5)`: discard every fragment whose geometric normal is not pointing
    up. It was there for a real fault - the surface was the unit cube squashed to a 0.15 m slab, so
    each cell had four side faces and an underside, and two coincident translucent faces either
    side of a shared edge each added their own alpha and **ruled the board into dark squares along
    every cell boundary**. Its comment weighed one instruction against "adding a mesh shape that
    nothing else would use" and took the instruction. The answer now is the mesh shape after all:
    `WaterMesh` is two **sheets**, a sheet has no spurious sides, so the clip is gone and the
    dark-square fault is answered by not building the faces rather than by discarding them. The
    drawn surface drops 0.15 m in the process, to exactly where `WaterSurface` always said it was -
    the old slab's *top* was what the player saw.
  - **Both guards were proved by control rather than by assertion.** Removing the
    "never between two water cells" test fails exactly `NoFaceIsDrawnBetweenTwoWaterCells` and
    nothing else; forcing the cascade drop back to the lip's fails exactly
    `WaterOverWaterFallsAFullCell` and nothing else.
  - **No test can see whether a face is actually drawn, and this nearly cost the feature.** The face
    tests assert matrices, which is the half that can be right while the sheet is invisible - a quad
    wound backwards, a normal the wrong way, the clip left in. So `WaterCheck` shoots contact
    sheets. Its **first three came back showing no change at all**, a pixel diff against the same
    frame with faces disabled was black, and the falls were very nearly written off as not drawing.
    They were drawing: the tool had **framed a spot with no cascade in it**, because it scored
    candidate sites by how much water lay nearby and so picked the middle of the widest pool. It
    scores by nearby *steps* now. The lesson is the old one in a new costume - the measurement was
    fine and the thing being measured was not what was believed.
  - **What it emits, measured afterwards: exactly 44 faces on seed 1, one per stepped pair, and not
    one lip.** The lip branch is correct and currently unreachable on the played board; it fires the
    first time somebody mines beside water, which is when it is wanted.
  - **And the owner's diagonal wedge was never water.** `d-15` had listed it as the one thing it
    could not determine. `Logs/water-lip.png` reproduces it - large diagonal green sheets standing
    proud of the meadow beside the channel - and shooting the identical frame with
    `BankLayout.Enabled = false` (`Logs/water-nobanks.png`) removes every one of them and leaves
    clean terrace risers. They are **bank ramps**: a channel cut through terraced ground grows one
    at practically every step, which is why they cluster along a stream and read as part of the
    water fault. **Left unfixed on purpose** - it is `BankLayout`/`BankMesh`'s own design, it is not
    what the question asked, and the owner should see the two apart. The paired shots are kept in
    the tool so the next session can tell a bank fault from a water one in one run.
  - **Verified:** fast tier 611 Sim and 349 Hud; EditMode **1443 total, 1432 passed, 0 failed**;
    PlayMode **66 total, 62 passed, 0 failed**. **Not verified:** frame time, and whether a vertical
    sheet carrying a ripple, glint and Fresnel authored for a horizontal surface reads as moving
    water or as a pane of glass. Nobody has pressed Play.

- **The falling water was mostly not being drawn, and it looked like a design problem rather than a
  bug (owner, 2026-09-17, second round on water).** The owner played the new water faces and said
  they read as not buggy but not water either: *"can it be made to be more water like ... a sense of
  flow/stream"*. Two options were agreed - **A**, stop the falls dissolving, and **B**, make them
  fall - with the flow work held back until those had been looked at.
  - **A: a falling sheet is not a shoreline, but the arithmetic could not tell.** Opacity is
    `base.a * shore`, and `shore` comes from `through` — the depth of water between the pixel and
    whatever was drawn behind it. Behind a fall is the rock face it pours over, a few centimetres
    away, so `through` is near zero and the shore rule — which exists to dissolve the hard line
    where water meets its bank — was erasing every waterfall on the board instead. A face now takes
    a thickness of its own (`_FallThickness`, 1.4 m against the 2.0 m fade) and keeps the measured
    one only where that is deeper.
  - **B: the ripple field cannot see a waterfall.** It is a function of `positionWS.xz`, which on a
    vertical sheet is constant the whole way down, so a fall had no variation along its own length
    at all. There are now streaks scrolling downward at `_FallSpeed`, and whitening over the bottom
    third of each sheet.
  - **Both are applied to colour and not to the normal, and the number is why.** At the play
    camera's 48 degrees the view sits 42 degrees off a level surface, where the Fresnel term returns
    `(1 - 0.743)^2.5 x 0.66` = **2.2%**; at the 20-degree grazing shot the shading was originally
    tuned on it returns **23%**. The one cue the shader's own comment calls "the one cue that
    separates water from coloured glass" barely fires at the angle the game is played at, so
    anything carried by the normal was never going to be the answer.
  - **`upness` is a smoothstep and not the raw normal, so the flat water is bit-identical.** The
    drape shears a surface by up to about five degrees, so a level tile's normal is 0.997 rather
    than 1; classifying on the raw value would bleed a few thousandths of the face treatment into
    every square metre of water the owner had already accepted.
  - **Then the real fault: the sheets were z-fighting with the rock they poured over, and losing.**
    A face centre is exactly the plane of the terrace riser below it, and two coplanar surfaces
    under `ZTest LEqual` are two candidates for the same pixel; the depth buffer picks whichever
    rounds higher, per triangle, so each 2.5 x 3 m sheet was reduced to **a triangular sliver of its
    own top corner**. That is why it read as a small pale wedge rather than as a missing feature,
    and why it survived several contact sheets — and why two rounds of shader tuning were spent on
    the foam term, which was working correctly the whole time and simply had no visible sheet to
    appear on.
  - **Settled in one run by tinting the sheet by its own UV.** Every visible fragment came back at
    `v = 1`, the top edge of the mesh. A debug tint is one line and cannot be argued with, where
    three rounds of squinting at a 2x crop had produced two confident and opposite readings of which
    end the foam was on. `docs/lessons.md` carries the general form.
  - **The fix is a 15 mm stand-off, and it opens a second-order fault worth knowing.** Standing a
    sheet off its wall leaves a slot between the two, and at the brow of the fall that slot is a line
    of sight onto the terrace behind it — a hairline of lit grass along the top of every waterfall.
    It is closed by starting the sheet 40 mm *above* its own surface and lengthening it by the same
    amount; lengthening is the half that is easy to forget, and a sheet tucked without being
    lengthened stops short at the bottom. Both halves are held by tests that assert **where the
    sheet's bottom edge lands** rather than how long it is, and removing the lengthening fails
    exactly those two.
  - **What this did not fix, and it is the next decision.** The shallow stream still reads pale at
    the play camera. That is not the shore fade, which reaches 1 across most of the body at this
    pitch: it is `StuffPalette`'s shallow water, `(0.28, 0.52, 0.55, 0.62)` — a light tint at 62%
    opacity over a bright, dry-looking sand bed. Raising the alpha is the obvious move and the less
    interesting one; **the bed is the better lever**, because real shallow water over pale sand *is*
    pale, and what is wrong is that the sand under water looks dry. `WaterFillPass` already writes
    a distinct terrain under water, so a wetter, darker submerged bed would fix the milkiness
    without making shallow water less legible.
  - **Verified:** fast tier 611 Sim and 349 Hud; EditMode **1443 total, 1432 passed, 0 failed**.
    **Not verified:** whether the downward scroll reads as falling rather than as a pattern sliding,
    which is the whole of B and cannot be judged in a still.
- **Flat avatars: a colonist's face beside their name, 2026-09-18** (`claude/flat-avatars`, `U41`).
  Design and the owner's seven decisions: `docs/design/20-avatars.md`. The owner asked for the
  profile next to the person's name, and for **one** avatar to serve both character selection and
  the roster bar â€” which chose the technology before anybody argued about it.
  - **The plan's row was the wrong unit, and the replacement was cheaper.** `U41 Portraits` asked
    for three 3D portraits rendered once per roll, cached and released, "under a test that fails if
    more than a fixed number of render textures are alive at once", with `09-ui-and-input.md` Â§4.5
    amended in the same commit. Composed flat avatars have no render textures, so that test has
    nothing to count â€” **and Â§4.5 needed no amendment at all**, because it already *prescribes*
    composed flat avatars for M2â€“M7 and refused only live portraits. The carve-out the plan promised
    was an exception to a rule we were about to obey.
  - **Two of the four sites already had an avatar slot drawing a placeholder**, which is why this
    was M and not L: the roster card had a People-coloured tile with the name's first letter on it,
    and the inspect header an outlined square keyed `ui.pawn.colonist` â€” a key `icon-map.csv`
    records as a gap with the note *"a human figure. No sheet contains one, and this is the
    most-used icon in the HUD."* That note is the whole reason the avatar is drawn rather than cut
    from a sheet: there is no sheet.
  - **The colour half had existed since M1 without anybody noticing it was an avatar recipe.**
    `ColonistAppearance` is a look index plus skin, hair and two garment colours as `Rgb24`, integer
    arithmetic over four independent hash streams. It was feeding a 3D character's atlas.
    `ColonistFace` calls it rather than deriving the colours again â€” the `NavGraph.HopCost` rule â€”
    and that is what forced the move into `Odyssey.Hud`, since Presentation depends on Hud and
    never back.
  - **The design corrected itself before any code was written.** Â§6 first left
    `ColonistAppearanceBook` in Presentation "because it touches the module catalogue", and Â§7 two
    paragraphs later promised the seed-fallback rule as a **fast-tier** test, which a Presentation
    class cannot be. The catalogue turned out to be one convenience constructor counting the
    colonist family; it is `AppearanceBooks.For` now and the rest went down, taking **eleven tests
    from the Unity tier into the fast one**.
  - **The contact sheet earned itself on its first run** (the owner's decision 6: a picture before a
    playtest). Two numbers in the drawing were wrong and no test could have said so. The three
    builds were 5.0, 6.3 and 7.6 half-units against a flare of 2.4, which put every bust between
    14.8 and 20.0 units wide in a 24-unit box â€” three builds that were one build; they are 11.8,
    15.0 and 18.2 now. The *long* and *ponytail* crowns reached 0.62 of the head's radius and read
    as sideburns.
  - **There is no face on the avatar, and that is a limit rather than a stage.** Our own measurement
    of our own pixel art (`Logs/skill-icons.png`) is that it loses its grooves at 17 px and goes to
    noise at 16; a roster card's avatar is 26. Two dots and a line at that size read as damage. It
    is a silhouette portrait, which is also what lets one drawing serve 26, 30 and 64 px unaltered.
  - **`Painter2D.Arc` is not used, and the reason is worth keeping.** Its angles are measured in the
    element's own space, where y runs down, so every "over the top of the head" would have been
    written back to front and drawn as a chin. The crowns are sampled into polygons at sixteen steps
    a half-turn, under a tenth of a pixel of chord error at 64 px.
  - **The last step was the one that could have made the whole feature a lie.** The card was keyed
    on `RollSeed` from the start; the figure in the world was still dealt from a world-level cast
    seed, so the person chosen on the setup screen and the person who walked around were two
    different people. `ColonistAppearanceBook.For` takes the pawn's seed now, with zero falling back
    to the book's â€” the compatibility path for a save written before U40, not a guard. **The cache
    is checked against the seed as well as the pawn**, because a figure leased before the aspect
    arrived would otherwise wear the fallback for the rest of the session; that one is a test.
  - **Two inspector switches would have quietly become decorative**, which is worse than removing
    them, because somebody would tick one and believe it. `randomCastEachSession` and
    `colonistLookSeed` now set `ColonistAppearanceBook.Pinned`, dealing the whole colony from one
    number and overruling the pawns â€” which is what both were always for, judging the palette over
    many colonists at once. Their tooltips say that ticking one means the people you chose are not
    the people you get.
  - **`Odyssey.Editor` did not reference `Odyssey.Hud`**, and 380 green fast-tier tests could not
    know: the project did not compile. `docs/lessons.md`'s standing warning, happening again, and
    the reason the Unity run came before the commit rather than after it.
  - **The roster card's initial letter took two exemptions with it** â€” the `.card__initial` rule
    with its stylesheet anchor, and the carve-out in `NoLabelIsAThreeLetterPlaceholder` that let a
    one-letter label through. A rule that shrinks as the interface improves is the right shape for
    that rule.
  - **Verified:** fast tier 645 Sim and 384 Hud; EditMode **1533 total, 1521 passed, 0 failed**;
    PlayMode **74 total, 69 passed, 0 failed**. All three gates the design named as its own check on
    itself â€” card geometry, the allocation budget, the coverage ceiling â€” pass unedited. Nothing is
    saved, nothing is hashed, **no golden moved**. **Not verified:** whether a colonist reads as a
    person at 26 px, whether any skin and garment pair comes out as one muddy value there, and
    whether the 64 px portrait belongs where it has been put. `Logs/avatars.png` and
    `Logs/setup-page.png` are what those questions get answered from.

- **Rendered portraits, 2026-09-18** (`claude/flat-avatars`, `docs/design/20-avatars.md` Â§10). The
  owner played the flat avatars and reported: *"the colonists look nothing like their profile
  picture."* They were right, and the fault was in the design rather than in the drawing.
  - **I had matched the palette and invented the person, and defended it in a comment.**
    `ColonistFace.Of` passed `lookCount: 1` to `ColonistAppearance.Of`, throwing away `Look` â€”
    *which of the sixty-one Synty characters this colonist is* â€” on the stated grounds that "an
    index into the catalogue's 3D colonist meshes means nothing to a drawing". It is the single
    most identity-bearing fact about a colonist. The colours did land (of 61 rows, 55 classify
    `Full`), so skin tone and garment hue matched while hair, build and clothing shape were all
    invented from unrelated salts. A colonist in a helmet was given a ponytail.
  - **Â§4.5 did not have to be overturned to fix it.** It refuses *fifty live render-textured
    portraits at 15 Hz while the world draws* and names a cached atlas as the graduation path.
    Nothing here is live: a portrait is rendered the first time an appearance is asked for and never
    again. The plan's original `U41 Portraits` row was closer to right than the unit that replaced
    it, and its leak test â€” which Â§9 said had nothing to count â€” now counts **one**.
  - **The cache is keyed on the appearance, not the pawn**, which is the whole performance story.
    Two colonists who genuinely look alike share one picture; a colony of twenty-six with a dozen
    distinct appearances is twelve renders for the session. **One `RenderTexture` exists for the
    entire game**, reused and read back into a 128Â² texture per appearance â€” 64 kB each, so a full
    roster is under 1.7 MB.
  - **The rig is off except during the render call.** A directional light is global in URP, so a
    portrait light would otherwise fall on the world. The alternatives were a spare layer, a
    rendering-layer mask (a project settings change) or lighting the portrait with whatever time of
    day it happened to be taken at and freezing it there. The render is synchronous, so enabling
    the rig, calling `Render` and disabling it again costs nothing and needs none of that.
  - **No animator and no `PlayableGraph`**, unlike `PawnFigureDirector.Create` â€” a portrait does not
    walk. A side effect worth having: a look whose *gaits* are missing, which `LooksFrom` drops
    outright, can still be photographed.
  - **`Logs/portraits.png` caught two faults, neither visible at 30 px and neither findable by a
    test.** The crop anchored on `body.max.y`, which is the top of whatever the character is
    *wearing*, so every bare head framed correctly and every hat-wearer was cut off at the chin â€”
    the pattern is the signature of exactly that fault and is why one image diagnosed it. It
    anchors on the **head bone** now; every pack character is a valid Mecanim humanoid, and the
    animator does not need to be enabled to read its bone map. And the key light was `Euler(28,
    200, 0)` against a camera looking the other way: aimed at the backs of their heads, which read
    as a murky render rather than as a backwards light.
  - **`randomCastEachSession` now defaults off, and the portraits are what closed it.** The switch
    overrules every pawn's own seed, the setup page photographs its candidates *before* a colony
    exists, and the pin is applied when the world is built â€” so with it on, pressing Start dealt
    three different people from the three on the cards. That is the owner's original complaint
    reappearing by construction. What the switch was *for* is now the contact sheet, which shows
    twenty-four at once without pressing Play.
  - **The drawn avatar is not wasted.** It is the fallback where `Assets/Synty` is absent, chosen by
    the idiom `IconBadge` already uses â€” art present suppresses the paint â€” so a clone without the
    packs is still correct, and the portrait tests ignore themselves with that reason on a runner
    that has none.
  - **Verified:** fast tier 645 Sim and 384 Hud; EditMode **1536 total, 1524 passed, 0 failed**;
    PlayMode **76 total, 71 passed, 0 failed**. **Not verified:** whether a 128 px render reads at
    26 px on a roster card, whether the head-bone framing suits all sixty-one bodies rather than the
    twenty-four on the sheet, and whether one key light flatters the cast or wants a fill.

- **The in-game avatar doubles, and takes four other numbers with it, 2026-09-18**
  (`docs/design/20-avatars.md` Â§10.6). The owner, having seen the portraits: *"can we make the
  in-game avatar profile twice as big as it's hard to see"*, then *"put a white border around the
  portraits"*.
  - **26 â†’ 52 on a roster card and 30 â†’ 60 in the inspect header**, and the card is re-derived
    rather than stretched: its width is still the wider of its two rows as the text engine measures
    them, and the name row's 16 + 52 + 8 + 50 = 126 overtakes the activity row's 106. 126 Ã— 89.
  - **The strip would have quietly halved the roster at 1080p.** Two rows cost
    `2 Ã— (CardHeight + CardGap)`, and the old 0.14 height share is 151 px there â€” two 63 px cards,
    or one 89 px card. Nothing reports that: the back row simply stops being drawn. `StripHeightShare`
    is 0.18, which is 194 px at 1080 and still deliberately one row at 720.
  - **The top scrim stopped reaching under the strip** â€” 185 px of two-row cards against a 170 px
    gradient, which is a colonist's name standing on bare meadow. 192.
  - **A tile's header must not follow a colonist's.** `InspectHeader` is the avatar's height now,
    and the tile readout shared that constant â€” but a tile's slot holds an `IconBadge`, and this
    interface draws icons at 17, 16 and 30 and no other size. Following would have minted a fourth
    icon size and stood a five-fact readout on a header two thirds the height of its own body.
    `InspectHeaderNarrow` stays at 38.
  - **The coverage ceiling went 19% â†’ 20%.** A two-row strip at 1280 Ã— 720 goes 3.81% â†’ 5.07% and
    the HUD to 19.80%. Two qualifications: that is the forced worst case rather than what the game
    draws there, since 1280 Ã— 720 is allowed one row and the *resting* measurement never left the
    old 19%; and the lever used on the previous two occasions is gone â€” both clamped the region
    instead of raising the ceiling, and clamping here means showing fewer colonists, when the card
    is this size precisely because the owner asked for the face in it to be legible. Recorded as
    theirs to reverse, with the cheapest reversal named: every pixel of avatar is four of card.
  - **Every one of those four was a failing test rather than something seen on screen**, which is
    the argument for the anchor tests stated better than the tests themselves state it. A change
    that looked like two constants came back with three consequences and a budget.
  - **The frame is 0.88 white at two pixels**, not pure white at one: a photograph has soft edges,
    and a hard white rectangle round it reads as a cut-out pasted on the card, while one pixel at
    52 reads as an artefact of the render rather than as an edge somebody chose. Set on
    `AvatarGlyph` rather than per site, so the setup page carries it too.
  - **Verified:** fast tier 645 Sim and 384 Hud; EditMode **1536 total, 1524 passed, 0 failed**;
    PlayMode **76 total, 71 passed, 0 failed**. `Logs/hud-shot.png` now photographs the HUD with the
    catalogue assigned, which it never did before â€” the rig built a bootstrap by hand and never had
    one, so that picture had gone on showing the drawn fallback after the portraits landed. It was
    the owner asking whether the roster used the portraits that found it.



### The wood was two colours, and one of them was nobody's choice (2026-09-18)

The owner played the meadow and said the world reads dull, naming the trees: *"they need to be a
variety of colours — mix in different shades brown and variation into this list"*, with a table of
six themes of four colours each, and *"bake it and make it performant"*.

- **The dullness was the consequence of a fix, not an oversight.** A tree draws with **no tint at
  all**, and `ChunkMesher.EmitEdifice` says why in place: a tree is placed with
  `NaturalContent.StuffWood` because that is what it is *made of*, not what it was *built from*, so
  when wood became a brown multiply on 2026-09-17 — to stop a wooden wall drawing as cream plaster
  — every tree on the board would have gone brown with it. Refusing the stuff tint was right. The
  side effect nobody wrote down is that it left a tree with no colour lever whatsoever, so the
  board's whole woodland was whatever two colours PolygonGeneric happened to ship.

- **A tint could not have been the answer even if one had been available.** Measured before
  anything was built (`TreeSwatchProbe`, `Logs/tree-swatches.txt`): each tree is **one mesh, one
  submesh, one material**, and the trunk and the canopy are different flat cells of the same
  4096 × 4096 atlas. `_BaseColor` — the lever every other module in the game is coloured with —
  multiplies both at once, so browning the bark browns the leaves. That is the same conflation the
  stuff tint already refuses to make, arriving from the other direction.

- **The three ways to give one mesh two colours, and why the third won.** Recolouring the atlas per
  theme is out on memory and the number is off the file rather than estimated: 4096 × 4096 is
  64 MB uncompressed and this wants about a dozen. Shifting UVs onto neighbouring cells needs spare
  cells nobody owns. Repainting the cells in the fragment shader costs four rectangle tests and no
  memory at all — and it is legitimate here for exactly the reason it was legitimate for colonists:
  the probe reports a **maximum texel deviation of 0** inside every cluster of every tree mesh in
  the pack. The cells really are flat, so replacing the colour inside one throws no art away. Had a
  canopy come back carrying a gradient, this would have been the wrong mechanism and the design
  would have had to settle for a multiply.

- **`Odyssey/Tree` is `Odyssey/Character` minus the ink hull, and the two are deliberately not
  merged.** The hull exists because skinned meshes are missing from the depth texture the outline
  pass reads; a tree is ordinary instanced geometry that pass inks perfectly well, and a second,
  closer line of its own would ink every tree twice. The shared half is thirty lines of rectangle
  arithmetic, and one include for both would have to fix one set of property names — which means
  renaming the character's, in a feature the owner has already judged.

- **The whole design is the performance question, and the first answer measured badly.** Drawing is
  bucketed per *(module, part, tint)* in a chunk, so a tree's colour **is** a bucket key and a draw
  call is what it costs. A chunk of woodland holds about 160 trees, so a colour rolled per tree
  saturates the palette in nearly every chunk: measured worst chunk **11 of 11 themes**. Dealing a
  colour to a *stand* of trees instead was the plan, and at the first stand size tried — 20 cells,
  chosen by eye against the 25-cell chunk — it measured **6.75 buckets a chunk, worst 10**. A
  saving that thin would not have been worth the feature. The bill is (stands overlapping a chunk)
  × 2 species, and a 25-cell chunk overlaps about five 20-cell squares; at 40 cells it overlaps
  two, and the same board measures **4.34 a chunk, worst 9**. The number was moved by the
  measurement, not by the argument that produced it.

- **And nothing was given up for it.** The obvious cost of wide stands is a small board carrying
  few colours, so it is measured at the size the game actually loads rather than inferred from the
  200-cell fixture: a 120-cell meadow shows **all eleven themes**.

- **Stands are cellular, not a quantised grid.** One line shorter and it draws colour boundaries
  with ruler-straight edges running the full width of the board, which nothing in a landscape does
  and which reads at once as a bug. Each stand square sows one jittered site and a tree joins the
  nearest, so a boundary is the bisector of two arbitrary points and wanders; the test measures
  that as "a boundary crosses 196 of 200 columns" rather than the 5 a grid would give.

- **Everything is a hash of the cell's own coordinates**, as `GroundLook` already required of the
  ground: a chunk is re-meshed whenever anything in it changes, so a stream of random numbers would
  recolour the wood every time a colonist felled a tree twenty metres away. Nothing here is saved,
  hashed or visible to the simulation, no Def moved and **no golden hash moved**.

- **The surround came free and had to be asked for anyway.** `TerrainSkirt` already samples the
  board's trees by frequency to decide what grows outside the rim, so pointing that sample at the
  new tint code makes the ring outside the board the same wood as the board. Without it the wood
  would have changed colour exactly at the rim, which is the one thing the surround exists to
  prevent. Its distance haze desaturates a tint, so it had to be applied to all four colours and
  joined the material key.

- **Cost of the third Unity run: `Does.Not.Contain(x)` resolves to the string overload** under
  Unity's NUnit, so a negated membership assertion against a `HashSet<int>` is a compile error
  while the *positive* `Does.Contain(x)` two lines above is fine. `docs/lessons.md` has it beside
  `Assert.Multiple` and `Has.Count`.

- **The draw-call bill on the real board is +57 of 1758, or 3.2%**, instances unchanged at 44,200
  (`TreeCheck`, wooded 120 x 120). The first run of that sheet reported **the same number three
  times over**, and the reason is worth keeping: switching the tree *materials* off still leaves the
  mesher splitting a chunk's trees into a bucket per stand, so the "before" column was the feature
  measured against itself. A before that is not a before reads exactly like a free feature.

- **And the fidelity control failed, which is what it was for.** With the repaint strength at zero —
  our shader drawing the pack's own colours — a tree comes out about **a tenth darker in sRGB**
  than `Synty/Generic_Standard` draws it, with the meadow beside it identical to the last digit.
  Three explanations were tested and all three died: **emission** (carried across now; changed the
  picture by nothing, because `_Emission_Color` is black), **the normal map** (the `flat` column,
  forced to zero, is identical to `plain` *byte for byte*, so the map contributes nothing at this
  distance), and **screen-space occlusion**, which is on at 0.4 and applied through a keyword —
  `CompareShaders` prints both keyword sets and **both declare it**. What is left is that the pack's
  shader is a Shader Graph carrying a built-in target as well as a URP one and ours calls
  `UniversalFragmentPBR` directly, and closing that means reverse-engineering the graph, which is
  the licensed-content line. **Not compensated for**: a gain on `_BaseColor` would cancel most of
  it, but the correction is not uniform (blue wants 1.24 where red wants 1.12) and a fudge factor
  fitted to two rectangles of one frame is the kind of number this project distrusts on principle.
  The brightness lever is the palette, and the palette is one table.

- **Verified:** EditMode 1533 total, 0 failed after the pin was moved to the measured figure;
  fast tier 645 Sim and 358 Hud.
  **Not verified:** whether any of it looks good. `scripts/unity.sh shot
  Odyssey.EditorTools.TreeCheck.Run` writes `Logs/tree-{pack,plain,themed}-{play,wood,close}.png`,
  where `pack` is the wood as the game drew it before and `plain` is our shader with the repaint at
  zero — the fidelity control that separates "our shader draws a Synty tree differently" from "the
  palette is wrong". Design, the owner's table and the five themes we added are
  `docs/design/21-tree-colours.md`.

### The pale tree was a mapping fault, and a wood is a mixture (2026-09-18)

The owner played the coloured wood and sent two notes: *"there was a shorter tree that was
white/pale leaves that looked odd"*, and *"it all needs a much larger variation of bark and leaf
colours, really vary it up as much as possible … but also really mix them in together"*.

- **The white tree was not a badly chosen colour; it was a colour put in the wrong place.** The
  owner's table is authored as a deep colour plus a *fresh leaf / highlight*, which reads as a small
  bright accent on a mass of the deep colour — and I took that at face value. The mesh is the other
  way round, and the probe had already said so: the broadleaf's **upper** canopy cell is **49.9%**
  of its vertices and the lower 24.3%. Whatever goes on top *is* the tree. Silver Birch's highlight
  #8F9779 and Mossy Birch's #9CAF88 measure luminance 145 and 151, and over half a tree that is a
  pale sage tree. The shorter tree is the broadleaf, 6.15 m against the pine's 9.47 m, which is what
  makes the report land on exactly the right mesh.

- **The fix is a change of concept rather than of numbers.** A colour is now **two faces of one
  colour**, lit and shaded, and the distance between them is taken from the art: the pack's own two
  canopy greens are a step of **1.21** in luminance, on both meshes. `TreeToneRules` holds that
  band, a brightness ceiling a little above the art's own brightest canopy, and a chroma floor,
  because a sage highlight is pale *and* nearly colourless and a colourless canopy reads as a dead
  tree. Three tests enforce them, so this particular fault cannot come back.

- **Bark needed a band of its own, and that was found rather than decided.** Holding trunks to the
  canopy's band failed six entries, three of them the owner's — Scots Pine at 2.13, Redwood at 1.79,
  Ancient Oak at 1.58 — which looked like the owner's table being wrong. Measuring the pack's own
  trunk pair settled it the other way: trunk #554B40 at luminance 77.9 against the branch-stub cell
  #9B7E5A at 130.6 is a step of **1.68**. A trunk is a cylinder with a lit side and a canopy is a
  cloud of leaves that has no such thing. **A rule derived from one kind of surface is a rule about
  that surface.**

- **"Vary it up as much as possible" is a cross product, not a longer list.** Eleven hand-written
  four-colour themes became fourteen leaf tones against eight barks for broadleaves and nine against
  six for conifers: **166 themes out of twenty-two readable lines**. Writing 166 themes by hand
  would have been 166 more chances to author a white tree. The owner's six survive as the tones they
  were built from, and the cross product contains their original pairings along with every other.
  It costs nothing, because the length of this table was never what a wood costs.

- **"Really mix them in together" is the part that does cost, and it is one number.** A stand used
  to deal one colour, which is what made a wood of uniform patches. A stand now deals a handful —
  `ThemesPerStand`, four — and each tree picks one by its own hash, so neighbours differ while two
  woods are different mixtures. Every step of that number multiplies the tree buckets in a chunk, so
  it is bought with draw calls and nothing else. The handful is drawn without replacement by walking
  the species' rows at a hashed coprime stride: four independent hashes would hand the same colour
  out twice about one stand in ten and narrow the mixing with nothing to show for it.

- **Unverified at the time of writing, and the reason is worth recording.** The Unity tier could not
  run: the owner had the editor open on this very worktree, which is what `check_project_lock` is
  for, and killing it would have taken the project out from under somebody looking at it. The tone
  tables were checked outside Unity instead, by parsing the C# table and applying the same
  arithmetic the tests do — all 37 tones inside their bands, 166 themes — which is a control on the
  numbers and not on the code.

- **What the mixing cost, measured on the board the game loads**: 1758 draw calls to **2135, +21.4%**,
  instances unchanged at 44,200. Structurally that is 4.34 tree buckets a chunk becoming **17.72**,
  worst chunk 9 becoming 29 — against **107** in the worst chunk for a colour rolled freely per
  tree, which is the row that says why a handful exists at all. The played meadow now draws **89 of
  166 themes** where it drew eleven, and 75.6% of neighbouring trees are a different colour from
  each other. It is one knob: `ThemesPerStand` at 3 or 2 gets most of the 21% back.

- **Verified:** EditMode **1544 total, 1532 passed, 0 failed**. **Not verified:** whether a wood
  this mixed is better than a wood in patches, and whether the plum and rust canopies belong on a
  board at all — they are the most distinctive rows in the table and the first to veto.

### Adding bright colours did not brighten the wood (2026-09-18)

The owner liked the mixed wood and asked for one more thing: *"can we add some bright colours into
the leaf — as it seems a bit dull still and needs brighten up"*.

- **The ceiling that stopped the white tree was what was holding the wood down**, so the first job
  was to work out which way to move it rather than simply raising it. Re-reading the earlier fault
  settles it: the two entries that caused it were not merely bright, they were bright **and nearly
  colourless** — #8F9779 is luminance 145 at a chroma of 30, #9CAF88 is 165 at 39. What reads as
  "white" is a *pale wash*, and a pale wash is high luminance with no colour left in it. So the
  allowance became a curve rather than a number: `108 + 0.62 x chroma`, capped at 195. A saturated
  lime may be 165 and a saturated gold 175; a sage at chroma 30 is still held to 127, and both
  originals are still rejected, by 18 and 33 points.

- **Ten bright tones went in and the table grew from 166 to 240 themes for 0.12 buckets a chunk** —
  17.72 to 17.84. That is the design's central claim holding under a 45% growth in the table, which
  is worth recording because it is the first time it has been tested by anything other than an
  argument.

- **And the board came back warmer and no brighter, which is the lesson.** Seven bright tones among
  twenty-one means a stand's handful of four draws about one on average and often draws none; the
  contact sheet's nearest stands had drawn coppers and rusts, and the play camera showed a maroon
  wood. **Adding a colour to a table dilutes it; it does not lift it.** What lifted it was
  *reserving a slot*: one of every stand's four is drawn from the bright subset, so every wood
  carries a bright note whatever else it drew. The handful is the same size, so it is free —
  18.08 buckets a chunk against 17.84 — and `EveryStandCarriesABrightLeaf` keeps the reservation,
  because a later session tidying `ThemesOfStand` would not otherwise know the slot was
  load-bearing.

- **The tint code was widened in the same round, and it was closer than it looked.** A theme index
  rode in the code's low byte, and at 240 themes the table was **one bark tone short of 255** —
  where it would have wrapped in silence and drawn one wood in another's colours. It has twelve bits
  at bit 16 now, clear of the terrain, foliage, water and daylight markers that live in the low
  bits, and `EveryThemeSurvivesTheTintCode` walks every index through the round trip and checks it
  trips none of them. The skirt's variant key had to widen with it: it packed the tint into twenty
  bits, which would have thrown the theme away and drawn every tree outside the board in one colour.

- **Verified:** EditMode **1546 total, 1534 passed, 0 failed**. Cost on the played board 1758 draw
  calls to **2142, +21.8%**, instances unchanged. **Not verified:** whether the cherry and flame
  canopies belong — they read as scarlet at the play camera, which is the most conspicuous thing on
  the board now, and they are the first rows to veto.

### The colour was a bucket key, and it did not have to be (2026-09-18)

The owner's answer to the mixed, brightened wood was *"make as performant as possible please"*. It
cost 384 draw calls on the played board, +21.8%, and every round up to here had worked around the
reason rather than at it.

- **The reason is one structural fact.** Drawing is bucketed per *(module, part, tint)* in a chunk,
  so while a tree's colour lived in its tint code the colour **was** a bucket key. Stands, handfuls
  and reserved bright slots were all devices for keeping *the number of colours standing in one
  chunk* small, because that number was the bill. Nothing about a colour requires it to be in the
  key: the tint code now says only which of the two trees it is — which is what decides the atlas
  cells to repaint, and so the material — and the four colours travel beside the matrices, read out
  of an instancing buffer by `Odyssey/Tree`.

- **The result is that a coloured wood is free.** 1,758 draw calls with the wood in two colours and
  1,758 with it in two hundred; 17.72 tree buckets a chunk becomes **2.00**, one per species; 240
  themes draw **2** materials instead of 240-odd. Every constraint in the two rounds before this is
  now a *look* decision rather than a cost one, and stands and the bright slot are kept because the
  board is better for them.

- **The picture did not change, and that was checked rather than asserted.** The two contact sheets
  differ by 5.3% of channels — which sounds like a lot until the *same* code shot twice differs by
  4.8%. The residual is the animated water and the anti-aliased silhouettes of ten thousand leaf
  cards: sampled canopy, trunk, gold-tree and red-tree patches are identical to a tenth of a unit,
  and the whole-image mean matches to 0.01 of 255. **A control run is what turned an alarming
  percentage into a floor.**

- **The first attempt did change it, and the difference image said exactly where.** A bright band of
  far trees across the horizon and nothing else: the *surround* had not been converted with the
  board, so it had fallen back to one colour per species. That is precisely the fault `TerrainSkirt`
  exists to prevent, and it took thirty seconds to find because the instrument was a picture of the
  difference rather than a number.

- **A property block's array is indexed from zero by every draw call**, not from the instance offset
  the call starts at. A bucket split across two calls would hand the second the colours of the
  first. The board slices into a scratch block; the surround, whose batches are each one theme,
  fills its block to the draw-call ceiling with identical entries so that any slice reads the same
  colour. Neither path can trigger on today's board — a chunk is 625 cells and a cell holds one tree
  — and both are there because "cannot happen" is a property of the board's dimensions rather than
  of the code, and the failure would be a patch of wood wearing its neighbour's colours.

- **A vector array is not a colour property, so the colour space became ours to get right.**
  `Material.SetColor` converts a `Color` property into the active colour space and `SetVectorArray`
  hands its contents over untouched. The conversion that used to happen for free is now explicit in
  `ChunkMesher.Colour`, and the identical sampled patches are the evidence it is right.

- **And a latent overflow was found on the way.** `ChunkMesher.Key` packed the tint into twenty
  bits — enough while every tint was a small material index, and silently not enough once a tree
  code carried a value at bit 16: fifteen million overflowed into the part field. Nothing was
  observably wrong, because the only modules with a tint that large had exactly one part. **Wrong
  only by luck is not a property to leave in a key**; it has thirty-two bits now.

### The beds merge, and reviewing it (2026-09-17)

`claude/beds` went conflicting against main after U29's floors, U42's paving, U43's ladders and
the rates design all landed. Fifteen conflicts; eleven were unions and four were real.

- **The bed's handle moved from 2 to 5.** It had been written as the next number after the
  wall; the floor, the deck plate and the ladder reached main first and took 2, 3 and 4. A
  handle position is a save contract and positions are append-only, so the later branch is the
  one that moves — safe here only because no save with a bed in it had ever left the branch.
  Save format is 4 for the same reason: the start flow took 3.
- **The renumbering's one silent casualty is the lesson worth keeping.** `BuildShapes` is a
  hand-written two-array table in the Hud assembly, parallel to `BuildingHandle`. Main never
  touched the file, so it was **not a conflict**: three entries merged in silence and the bed
  quietly became a one-cell thing that could not be turned. Three `DesignateDirector` tests
  failed and none of them named the cause. The class's own remarks claimed "the two tables are
  held together the same way the labels are — a test walks both", and no such test existed.
  The general form: **a parallel table needs a length assertion against the handle set's
  `Count`, or renumbering breaks it with no conflict to warn anybody.**
- **`WhereItWouldLand` is where the bed's "never lifted" rule belongs.** It had been written
  inline in `Place`; main had since lifted that arithmetic out precisely so the cursor and the
  order could not disagree. Leaving it behind would have drawn the bed's ghost with the wall's
  lift — the same class of fault the method was created to close.
- **Then the review found three things both tiers were green over.** Four of the six ownership
  tests were never running: `RaiseABed` called `Raise` without placing a site, `Raise` returns
  at once when there is none, and each test ended on an `Assume` that an unbuilt bed could be
  given an owner. **A failed `Assume` is Inconclusive**, which `dotnet test` reports as neither
  a pass nor a skip — the console says `Passed! Failed: 0, Skipped: 0` and the test is simply
  absent from the totals. The whole of the ownership feature had been untested since the day it
  was written. A fifth test ignored itself on every run, searching for solid ground at
  `start.Y` when the ground is the layer below. The feature was right; the tests were not
  asking. Nineteen bed tests now, none skipped.
- **And the build cursor knew nothing about beds.** Both ghost paths arrived from the
  build-cursor work after the bed design was written, and both drew one cell-filling module at
  the head cell. A bed ordered on grass was a block — and **R changed nothing anybody could
  see, because a cube looks the same all four ways round**, which is the feature the design's
  §5 exists to prove. `BedShape` owns the three boxes now; the mesher and both ghosts ask it.
  The seam test that would have caught it is in `FloorToolReachTests`, beside the one written
  when U29's floor tool turned out to be armable, draggable and inert: **point at bare grass
  and a bed must be ordered in the air above it.**

### The bed, after the owner first looked at it (2026-09-17)

Five reports, four of them about the bed being drawn as *boxes* rather than as a bed, and one
about a feature that had been unreachable since it was written.

- **`BedShape` is written in metres now**, not in raw scale factors. Two of the three parts are
  drawn from module boxes of different sizes, so a bare `Scale` meant a different thing for each
  — which is how six numbers that ought to touch drifted 0.56 m apart without anything failing.
- **The pillow floated**, because the mattress reached `z = ±1.02` and the pillow sat at
  `z = −1.75`, hanging past the end of the bed. The frame and mattress run the bed's whole
  length now. `BedShapeTests` asserts the *relations* — pillow on mattress, mattress on frame,
  all of it inside the two cells — rather than the numbers, so the six can be tuned freely and
  none of them into mid-air.
- **A pillow cannot be rounded by a matrix**, so it got a mesh: `PillowMesh`, a superellipsoid
  spanning the same −0.5..0.5 unit box every stand-in does, and the **only smooth-shaded thing
  in the renderer** — everything else is hard-normalled because the flat-lit look depends on it,
  and a pillow is the one thing in the game meant to read as soft. **It was inside-out at the
  poles**, and the reason is worth keeping: `Mathf.Cos(-π/2)` is `-4.4e-8` in float, not zero,
  and `SignedPow` carried that sign through — which mirrors the pole ring through the axis and
  reverses the winding of every triangle touching it. Clamping `cos v` at zero fixes it. Same
  class as the trap `PrimitiveMeshes` documents: an inside-out mesh is valid geometry, nothing
  throws, and the counts look healthy.
- **White needed a tint that is not a stuff**, because none of the six stuffs is cloth. Bedding
  got a `TintCode` bit of its own beside foliage and water, so a stone bed has the same linen
  pillow a wooden one does — and the ghost under the cursor draws it that way too, which is the
  build cursor's whole bargain.
- **Selecting a bed highlighted the whole cell.** A bed is two cells long, knee high, and the
  one edifice that does not fill the cell it stands in, so a cell highlight was wrong about its
  size, its facing and both of its ends. It is a bracket round the bed's own box now, measured
  from the head cell whichever half was clicked.
- **A quality tier has a colour, and `HudTheme.Quality` is the only place that decides it.**
  Normal returns **null** — "no change" read literally, which is not the same as returning the
  body colour, because a tier named on another surface must keep that surface's colour. The
  colour rides on the row rather than being chosen by whatever draws it.
- **And the owner could not find how to assign a bed at all.** The row had been pickable since
  it was written and said so with a pointer cursor and a hover brighten — sitting between
  "quality" and "walk speed", which are facts, and reading "—", which says there is nothing
  here. **Hover is not an affordance on a row nobody suspects.** It reads "Assign…" now, in a
  bordered box, with a chevron so the owned case says it is a control too. A fast-tier test had
  pinned the em dash; it pins the word.
- **One of the five test failures was the test's own fault**, and that is the note for next
  time: `BedShapeTests.LocalBox` applied the part matrix to a unit cube without composing the
  module's `local`, which is where the geometry actually ends up. Each part then had the
  module's size divided out and its centre at the origin, and four assertions failed over a bed
  whose numbers were right. A new test failing is not by itself a fault in the code under test,
  and "fixing" the bed to satisfy it would have shipped the wrong geometry.

### The bed's second look (2026-09-18)

Two reports from the owner, and both turned out to be about *drawing*. One of them was the most
instructive mistake this line has produced.

- **"I couldn't assign anyone with a bed"**, for the second time. The row had a pointer cursor,
  a hover brighten, a border and a chevron by then, and was still not found. **Hover is not an
  affordance**: a player does not hover a row to discover whether it is a control, they scan a
  panel and see facts. It is a filled accent box with a bed glyph, the word "Assign…" and a
  chevron now, set in the heavier `Row` type role — weight being `HudType`'s and never the
  stylesheet's, which `TheSheetSetsNoTypeAtAll` caught the moment the first attempt reached for
  `-unity-font-style`.
- **"Colonists stand outside rather than getting into a spare bed."** They do not, and this was
  measured before anything was changed: a probe on the owner's exact case — nobody owning
  anything, one spare bed, one tired colonist — walked the colonist into the bed and slept
  there. `TrySleep` picks the nearest reachable unowned bed and always had.
  **Nothing in the whole of presentation knew a pawn could be asleep**, so a colonist in a bed
  was drawn standing bolt upright in it, all night. The report was exactly right about what was
  on screen and the cause was one layer over from where it looked; taking it at face value would
  have meant rewriting a sleep chooser that was correct. **A simulation that works and cannot be
  seen is a simulation that gets reported broken.**
- **`SleepPose` is the fix** — a computed lying pose in the idiom `WorkSwing`, `ClimbPose` and
  `SwimPose` established, because no pack contains a sleep clip. It lies in a bed and on the
  floor, the owner's own second ask, because a colonist who cannot reach a bed lies down where it
  is — `SleptOnGround` made visible. **Four postures** off the owner's reference sheet, chosen
  from the pawn id so a colonist lies the same way every night and after a load, at no cost in
  state.
- **The posture hash was broken and its own test caught it.** The choice is `% 4`, so only the
  bottom two bits are ever read — and those are exactly the bits a Knuth multiply-and-shift
  leaves unmixed. Every colonist came out in one posture, which is the morgue the table exists to
  avoid. A full avalanche fixes it; the test now asserts all four appear among a dozen
  *consecutive* ids, because consecutive is what a colony has.
- **And measuring the first two found a third fault nobody had asked about: two of the five
  quality tiers did nothing.** Rest gain is `6 x effectiveness / 100`, giving 4.8, 5.1, 6.0,
  6.72, 7.5, 8.4 — truncated to 4, 5, 6, **6**, 7, 8. A Decent bed recovered rest at exactly a
  Normal bed's rate, so the tier a colonist rolled was worth nothing. The fractional part is
  spent by a Bresenham step over the interval index, which is derived from the tick and the pawn
  id and therefore stays out of the save and the hash. No golden moved: no colonist gets tired
  inside those windows.
- **What was deliberately left alone.** A plain bed is 1.25x the floor and an Epic one 1.75x,
  which follows from `groundRestEffectiveness = 80` — a number design 20 §2 records as the
  owner's. It is pinned by a test that states both figures out loud rather than changed quietly.
- Three of the tests written this round failed on their first run and were right to every time,
  and all three assert a **relation** rather than a value: the pillow rests on the mattress, each
  tier beats the one below, neighbouring colonists differ. Each fault had left every individual
  number looking perfectly reasonable.

### The last hammer blow is rolled, and a test wrote through the database (2026-09-18)

U26's last outstanding line: a completed build now rolls against the finishing builder's
construction skill and can **botch**. Design is `docs/design/15-building.md` §4a; what follows is
why the numbers are what they are and the one thing that nearly went in wrong.

- **The reference's curve could not be taken verbatim, and this is the third time that has been
  true.** a-04 §4 has a novice at 75% rising to a certain 100% at skill **8**. Our colonists start
  at an average of **1.16**, so shipping its anchor would have meant a colony botching most of its
  early walls — the same trap the rates line found when it discovered every work-speed slope in the
  reference reads 100% at level 8 because that is where its colonists live. Certainty sits at **3**
  here for exactly the reason it sits at 8 there: just above where a starting colony actually is.
  The integers — 850 base, 50 a level — are invented and the owner's to tune at the keyboard.

- **The finisher rolls, not whoever did the work, and it is kept on purpose.** A building records
  no author, so the level consulted is whoever landed the last tick. That is exploitable in exactly
  the reference's way, and the exploit reads as a sensible thing for a colony to do: a master walks
  over and finishes a novice's half-built wall. An "author" field would be new saved state bought
  to remove a behaviour nobody would report as a bug.

- **A botch had to cost material or it costs nothing.** The reference wastes "some resources" and
  a-04 records the fraction under *could not be determined*, so half is Odyssey's own number: a
  botch costs what a demolition refunds, which is at least an argument. The odd unit of an odd
  delivery goes to a seeded flip rather than a rounding rule, keyed on **cell ^ tick** like the
  deconstruct refund — keyed on the cell alone, every cell on the board would be permanently lucky
  or unlucky, which is stable, discoverable and then worth farming.

- **`Botch` marks nothing dirty, and that is a finding rather than an omission.** Every previous
  "deliberately not marked dirty" in this line turned out to be a debt U29 had to pay. This one is
  not: no wall appeared, so no chunk, no walkability and no support moved. The only state that
  changed is the site's two numbers, which the hash and the save already carry — so a botch replays
  from a seed and survives a reload, and the two givers simply ask their questions again on the next
  scan. A botch costs the colony work and material and **never the order**.

- **The expensive part was a test that wrote through the content database.** The first version of
  the botch tests set `WorkTypes[Construction].successBasePerMille` directly to force certainty.
  The Defs a content record's arrays point at are **shared by every record in the process** —
  `ContentPack`'s own caching rule — so that write silently retuned construction for every test that
  ran afterwards, and the new `ContentFingerprint` pinned in `PawnContentDefTests` was taken from the
  polluted database rather than from a clean load. Both tests were green. The fix is that a test
  **replaces the `WorkTypeDef` element** rather than assigning into it, and the fingerprint was
  re-taken from a fresh pack and then confirmed by running `PawnContentDefTests` alone, away from
  anything that could have tuned it. **A fingerprint is only worth what the database was worth when
  it was taken.**

- **And a dead branch hid the one claim the journey test existed to make.** `AFrameCanBeBotched`
  watched a site through fed → botched → **fed again**, and the re-feed branch sat behind
  `if (frame)` in the same else-chain, so `frame` was false by the time it was reached and
  `fedAgain` could never be set. It failed loudly rather than passing vacuously, which is the only
  reason it cost minutes: the assertion it could not satisfy is the one that proves a botched site
  does not wedge.

### Two branches each closed "U26's roll", and they were different rolls (2026-09-18)

`claude/build-botch` went conflicting against main the moment beds landed. Three conflicts, and the
interesting one was not a conflict in the text sense at all.

- **Both branches believed they were closing U26's outstanding roll, and both were half right.**
  The bed's commit rolled a **quality tier** at the moment of completion; this branch rolled
  **success or botch** at the same instant. U26 had left two rolls, not one, and each branch read
  the open item as the one it was building. Nothing in either branch's tests could have caught it:
  they are orthogonal features that happen to fire on the same line of `BuildJobDriver`, so both
  were green and the merge was where the ambiguity surfaced. **An open item phrased as "the roll"
  was the whole cause** — it named a mechanism rather than a question, and two people asked
  different questions of it.

- **They compose rather than compete, and the order is forced.** Success is asked first and a botch
  returns before the quality roll, because a thing that was not built has no quality to have. Both
  read the finishing colonist's Construction level and both keep the reference's exploitable
  "finisher rolls" property, which is the argument each branch had already written down
  independently — the strongest sign the two belong together rather than one replacing the other.
  Separate salts, so asking one does not move the other's answer.

- **The status line was corrected rather than left.** CLAUDE.md said the bed closed "U26's
  outstanding success roll", which would have told the next session the botch already existed. That
  is precisely the class of stale claim this file keeps finding — a note that outlived its own
  accuracy — so it is fixed in place with the misreading named, not quietly edited away.

- **A botched bed was the one genuinely new surface, and it needed a test.** A bed is the only thing
  that is both quality-bearing and two cells. It turned out already correct, for a reason worth
  recording: a site's work and delivery live on the **head** cell alone, the far cell being derived
  from footprint and facing, and `Botch` is handed exactly the cell `Raise` would have been. But
  "correct by inspection" is not the same as tested, and the interaction existed in neither branch.

- **That test skipped silently on its first run, which is worse than failing.** `Assume` found no
  bed footprint, NUnit recorded a skip, and the suite stayed green — a test that proves nothing
  while looking like it passed. The cause was ordering: it spawned forty wood near the head
  *before* placing the order, and the nearest space to the head **is the footprint**, so the pile
  landed on the bed's own cells and the order was refused. A site holds its cells against items
  from the moment it is ordered, so ordering first fixes it. Diagnosed by counting — 112 of the
  cells near the start take a bed, which ruled out the site finder in one measurement and pointed
  straight at the second `Assume`.

- **And it was made to fail before it was believed.** Forced to certain *success*, the assertion
  "a bed that botched every roll was raised anyway" fires. A test that has not been seen to fail is
  not evidence, and a test that can silently skip has already proved it can lie.

### CLAUDE.md had eaten itself, and four unit numbers meant two things (2026-09-18)

Two tidying jobs that turned out to be the same fault: the project's own record-keeping rules were
being followed everywhere except in the file that states them.

- **`CLAUDE.md` was 1,097 lines and 95,738 characters, and 982 of those lines were one section.**
  Its own opening paragraph says to update *Current status* and **append the reasoning to this
  file** rather than growing it. Every session since has instead written the reasoning into the
  status section, which is read into every session's context — so the cost of the narrative was
  being paid on every single turn, for ever, by every agent. It is 326 lines and 27,480 characters
  now, a 71% cut, with *Current status* at 206.

- **Nothing was thrown away without checking where else it lived**, because "it is surely in the
  journal" is exactly the assumption that loses things. Measured rather than assumed: of the 257
  distinct code identifiers the old section named, **243 appear in the journal, a design document
  or a plan**, and the remaining 14 are all live symbols in the C# — where the code is the
  authority and a prose mention is a copy. Of 38 measurements, 37 survive; the one that did not,
  the shoreline jitter's 31 mm, is in `20-swimming-and-water.md` and in this file, and its *open
  question* was put back into the owner list by hand. An exact-phrase comparison had said 206
  claims were unique to `CLAUDE.md`, which is what a phrase comparison always says when the same
  fact is written twice in different words; it was the wrong instrument and nearly the wrong
  conclusion.

- **What replaced the narrative is a table of where to read.** The single most useful thing the old
  section did was warn a session off breaking something — "read this before touching that line",
  "do not undo this by tidying". Those pointers survive as an index from each live line to its
  design document, which is where the warnings already are in full. The status section's job is
  what is true *now*; the journal's is why.

- **`U42`–`U45` each named two different units.** Paving, the ladder, stairs and beds hold those
  numbers in M3, three of them built; the four rates units in §WS were written with the same four
  numbers. "U42 is done" was true and false at once depending on which table you had open, and the
  rates seam is the one unit whose whole done criterion is *that nothing changes* — the worst
  possible thing to believe is already built. The rates units are now `WS1`–`WS4`. **The built ones
  kept their numbers** because this file records them that way and this file is not rewritten;
  entries above dated on or before 2026-09-17 still say `U42`–`U45` and mean paving and ladders.

- **The collision was found by counting references, not by reading.** It surfaced while sizing
  which of the two senses was cheaper to move — 12 journal references against a contained set of
  three planning documents — which is also what proved the built sense had to be the one that
  stayed.

### The rates landed, and the golden gate held three different ways (2026-09-18)

WS1–WS3 of design 17 went in on `claude/rates-and-stats` as three commits: the seam (`f6beb56`),
work speed from the skill curve plus the stroke clock (`9ae7682`), and innate pace, condition and
collapse (`a4413df`). WS4 running stays held — the plan's standing rule, *do not invent an urgency
model*, is still the right answer and needs no code to honour. What the unit taught:

- **The design's "two saved integers" turned out to be four accumulators.** §2b closed with *"two
  saved integers change scale, `_work[cell]` and `MoveProgress`, and nothing else does"* — and the
  implementation deliberately broke that sentence: `Job.ToilProgress` counts milliwork too, and
  `_work[cell]` lives in two grids (designations *and* construction). A rate must reach a surface
  to be visible, and the toil accumulator is the one the driver's own pacing reads; leaving it in
  ticks would have given the stroke clock a rate it could not apply. §2bb's rule — the scale is
  internal, everything crossing the sim→UI contract stays in ticks — held exactly as audited:
  `WorkToClear`'s ushort, `SiteView`'s seconds and `Fraction()`'s denominator all keep their units.

- **WS1's done criterion was nothing, and the gate for nothing is everything else.** The suite
  passed unedited — goldens, path checksums, HUD readouts, the one-day run — because
  `cost × 1,000 / 1,000` reads back exact. The one test the unit added for itself is the control
  the plan asked for: a rate of 500 provably takes twice as long, at both accumulators.

- **WS2's goldens were a no-op, and the reason was written down instead of a re-bake faked.** The
  regolden run printed the same three hashes it was fed. That is not luck: all three golden cases
  are `ScenarioDef.Bare`, which runs no job the curve moves — haul prices flat by the design's own
  rule that a skill drives either rate or quality, and nothing is mined, cut or built there. The
  same reasoning made WS2's soak byte-for-byte the WS1 baseline's. The discipline is: a no-op
  golden is only trustworthy when somebody can say *why* it was one.

- **WS3's re-bake was the first real one, and it had the right shape.** All three `Simulated`
  hashes moved and no `Generated` one did — the signature of a simulation change with the
  generator untouched. That shape exists because the pace roll is keyed like passions and starting
  skills but *drawn lazily on first read*: placement's dice never re-roll, so the generator's hash
  cannot move. For the first time in the unit it is the colonists and not the hash that changed —
  idle colonists crossing their boards at 850 to 1,150 instead of in step.

- **Two facts about the skill system were found the slow way by test fixtures, and are now
  written here so the next session finds them the fast way.** First, experience exactly 0 means
  "not rolled yet": `RollStartingSkills` skips any skill whose experience is zero and would
  overwrite a pin on the first tick — a level-0 pin must use experience 1. Second,
  `DecayExperience` drops a level-20 pawn to level 19 on any loss, so a rate pinned "at level 20"
  quietly became a rate at 19 halfway through a 2,556-tick walk — long fixtures must freeze decay.
  Both look like flaky rates and are neither.

- **"What is she working at" moved from the giver to the driver.** The rate publisher asks every
  publish, and the answer had lived on `WorkGiver`; the driver now carries a virtual `WorkType`
  defaulting to hauling — chosen so a driver that never swings (eating, sleeping) and is somehow
  asked anyway reads as the one work type that prices flat.

- **The work aspect's gate is Working, not merely "has a driver".** An adopted pawn takes a wander
  job on tick one, and the first version published 777 for an "idle" colonist. Only
  `workFocus >= 0 && Driver != null` gates `odyssey.pawn.rate.work`, because only the stroke clock
  reads it. The move aspect is the opposite: a fact about the pawn wherever she stands, so it
  publishes for everyone.

- **Format 6 appends last and saves nothing it can recompute.** `StarvationSeverity` sits at the
  very end of the pawn section so a v5 file reads positionally unchanged; the innate pace is
  deliberately *not* saved — a pure function of seed and id is correct by construction on load,
  and a saved copy would be a second thing to keep honest. The version-number test did its job:
  the bump is a deliberate line in a diff, again.

- **A fresh-eyes review of the branch found five things, and each fix is its own small commit.**
  The mid-walk collapse went down without the ground's thought — a giver-side collapse remembered
  it and a road collapse did not — so the walk toil's guard now adds it, once, and the bed stays
  reserved until the job ends rather than gaining a second exit. The pace band and the work
  curve's integers said nothing about being invented; they do now. WS2's soak had cited a baseline
  run the record never held, and the review could not verify it from the file: the run has been
  made at `f6beb56` at last and matches the table that cited it byte for byte, which is the
  difference between a claim checked and a claim trusted. The seam's "costs no allocation" claim
  is now a Long test on a working colony — 10.4 bytes a tick, with the dozen completed cells'
  ~25 KB apiece of nav and support rebuilds measured, attributed to editing the world rather than
  ticking it, and written into the test's comment. And the hash comment no longer promises exact
  division: the toils that count plain ticks — eat, sleep, wait — divide to near nothing and reach
  the hash through what their endings change.

### The inspect pane resized under the pointer (2026-09-18)

The owner, on the colonist card: *"When I click on tabs like skills/needs — it resizes every time —
it needs to be at least a fixed size (IE the size of the skills tab) — so that it doesn't resize to
the content of needs."*

**The pane grows upward from a docked bottom edge, which is what turned a height into a jump.**
`.inspect` sits at `bottom: 64px`, derived from the command bar, and `HudLayout.InspectHeight` added
up the *active* tab's rows: Needs is 59 px (two rows of 25, one 9 px gap), Skills is 157 (seven rows
of 19, six 4 px gaps). Because the bottom is pinned, all 98 px of the difference came off the top —
the portrait, the name, the tab strip and the first row all moved, and the tab strip is precisely
where the pointer is at the moment of the click. A pane docked to its *top* would have had the same
arithmetic and not been worth a complaint.

**Three questions were asked before any code was written, and the owner took the plainest option of
each**: the height is the tallest tab that exists today rather than a guess at the seven disabled
ones; a short tab's rows keep the position they already have, with the slack below them rather than
spread through them; and the tile readout is left content-sized, because with no tab strip it cannot
resize under the hand.

**The number is derived, not typed.** `HudLayout.InspectTabBody` is `max` of the needs body and the
skills body, computed from `SkillCatalogue.Rows` and a new `InspectNeeds`. A hand-set 160 would have
read the same today and clipped the day a fourteenth skill was added — and the skills grid is
already a computed seven rows precisely because it is the kind of list that grows.

**`InspectHeight`'s row arguments now say only whether there is a body.** They still size a tile's
readout. That is a deliberate asymmetry rather than an oversight, and the doc comment says so, since
the next tidy-up would otherwise "unify" the three branches straight back into the bug.

**The stylesheet is the second copy and it is held, not trusted.** `.inspect__tabbody` carries the
same 157, and the row added to `HudStyleSheetTests`'s table pairs it with `HudLayout.InspectTabBody`
— the same mechanism that already holds the pane's width, its bottom and its header. The fast tier
parses the USS, so the two cannot drift without a red test in twenty seconds.

**The test asserts the top edge, not the height.** Equal heights was the obvious assertion and it is
the weaker one: the complaint is about a thing moving, so `TheColonistPaneIsTheSameHeightOnEveryTab`
solves both tabs and compares `Y` first.

Nobody has pressed Play on it. The open question a picture cannot answer is whether ninety-eight
pixels of empty pane under three need bars reads as stable or as broken — and if it reads as broken,
the answer is more needs, not a shorter box.

### Four of the seven build categories held nothing, and looked no different (2026-09-18)

The owner: *"On the build menu could to disable when top groups that have nothing to build — just
gray them out for now … gray anything that cannot be built for the time being — so we understand
what we can build."*

**Half the ask was already done, and finding that out first changed what got built.** "Gray anything
that cannot be built" is the sub-type tier's behaviour since it existed: a tool with nothing behind
its key is drawn `bp__tile--off`, dim, unclickable, with a tooltip saying "not built yet; the tool
arrives with its content". What had never been done was the tier *above* it. Production, Power,
Security and Recreation hold nothing at all — 0 of 4, 0 of 4, 0 of 3, 0 of 3 — and were painted in
full category hue beside Structure's 4 of 7. So the answer was one tier, not two, and the existing
disabled vocabulary was already there to reuse rather than invent.

**The hue goes entirely rather than fading.** Fading was the obvious move and is the worse one: a
faded hue on a 20 px glyph reads as a rendering fault rather than a state, and it would have given
the panel a second way of saying what `SubTypeDisabledInk` already says one tier down. The category
tier is the one place in this HUD allowed a hue per row (design 17 §3) and this is a deliberate
exception to that rule — the hue is what makes a category identifiable, and a category holding
nothing is not one the player needs to identify yet.

**It still opens, and the test is on the second half of that claim.** Three treatments were offered;
the owner took "grey but still openable". Dimming is the information, and blocking the click would
only hide the plan — the reason to draw seven categories while four are empty is that a player can
look inside Power and see what is coming. That is only safe because opening an empty category cannot
arm anything: `SelectCategory` finds no live tool, and `ApplySubType` returns at its `TryGet`. So
`AnEmptyCategoryOpensAndArmsNothing` asserts the cursor is still empty afterwards, not merely that
the category opened. It also fails loudly if there is no empty category left to check, which is the
day to delete the dimming.

**The count is written as an invariant, not as seven numbers.** `ACategorysLiveCountIsWhatItsToolsSay`
compares `LiveToolsIn` against a walk of the same table rather than against "4, 0, 1, 0, 0, 1, 0".
`U44` puts a stair into Structure and the first workbench lights Production, and a test that has to
be edited on each of those is a test that gets edited without being read.

**The repaint loop was the trap.** Categories paint inline from code — `Hud.uss` says so at the
neutral tier — and `PaintBuild` rewrites their fill, border and ink every repaint. A class on the
tile alone would have looked right in the editor and been painted back over on the first refresh,
which is the same silent class of fault as a chip that arms a tool and never lights.

**What was deliberately not done: a tool you cannot afford.** A wall stays lit with no wood and no
stone. A blueprint can be placed and hauled to later, which is the genre's norm and the reason
`ReadStockFrom`'s null means "in stock"; the material tier already drops its tint to say what is
short. "Cannot build right now" is a second state and wants a second treatment.

### One panel in the corner, written in only one direction (2026-09-18)

The owner: *"If I have the build menu open and I haven't selected anything to build — I go to click
on any tile for info — that panel appears but underneath the build menu. What should happen is the
build menu closes and then the tile info can be seen — otherwise windows overlap."*

**This was not a new rule; it was the missing half of one the owner gave the day before.** On
2026-09-17: *"if the tile info dialog is showing, that is closed down and the build mode is open"*,
and both panels were docked into the same bottom-left corner on the same day. So `SetBuildPalette`
clears the selection as it opens. Selecting something *while* the palette was up was never wired,
and the pane opened underneath it.

**The palette's own comment had been asserting the invariant that was broken.** `PlaceBuildPalette`
says the bottom *"used to lift over the inspect pane when something was selected; the pane is closed
when the palette opens now, so there is nothing to lift over and the panel sits on the bar in every
case"*. Every word of that is true of one direction and was quietly assumed of both — the panel
stopped lifting, and the case where the pane arrives second stopped being handled at the same
moment. A comment that states an invariant is worth more when it names which direction it was
proved in.

**Asked of the reason, not of the input device.** What collides is the pane, so whatever raises the
pane closes the palette. That covers the roster card and the alert jump, which are the same overlap
reached another way and would have been left broken by a rule written about world clicks.

**`Cleared` had to be excluded, and the exclusion is the interesting part.** Opening the palette
clears the selection, so a close rule that fired on a cleared selection would have shut the palette
on the frame it opened — a Build button that does nothing, and a self-inflicted one. The test is
named for the trap rather than for the behaviour. `Died` and `LayerChanged` are out for the milder
version of the same reason: they take a selection away and draw nothing new.

**Nothing here can interfere with building, and that was checked rather than assumed.**
`SliceCameraRig.WorldToolArmed` routes a click to the designate path whenever a tool is held, so a
world click can only *select* when the player's hands are empty — which is exactly the case the
owner described. The only way to reach the new rule holding a tool is a roster click, and the owner
chose to keep the tool in hand there: closing a panel is not the same as putting a tool down.

Nobody has pressed Play on it.

### The order you gave from inside a menu left the menu standing (2026-09-18)

The owner, an hour after the pane-under-the-palette one: *"if I'm in the build menu (or any other
menu) and I click on an order — I expect that menu to be closed down and the dialog appear/order
would happen"*.

**The parenthesis was the whole requirement, and it is the reason this is one method rather than one
line.** The obvious fix — close the Build palette in the order button's handler — would have been
right about the panel that happened to be open when the owner noticed, and wrong about the Menu
popover and the bed picker on the same afternoon. `CloseMenusOverTheBoard` shuts all three, and the
next non-modal popover joins it in one place rather than in every call site that has learned to
close things.

**The modals turned out to need nothing, and that was worth checking rather than assuming.**
Settings and the debug windows are built through `HudModal`, which puts a pickable scrim over the
whole screen — so the orders strip cannot be clicked while one is up at all. Excluded by
construction. Written into the method's own comment, because a list of three in a HUD with eight
panels looks like an oversight until somebody says why it is not.

**Why the orders strip is the control this happens to.** It was taken out of the palette's header on
2026-09-17 exactly so that giving an order would not cost opening a panel first — *"this enables us
to quickly give orders without having to click the build button"*. That makes it the one control a
player reaches for from inside something else, which is precisely the case where leaving that
something else standing reads as the click not having landed. The same fix, one screw further on.

**It hands a job over rather than retiring one.** §7's mode colour — the palette wearing the held
order's hue — exists for "order held while the palette is open", and the floating armed banner is
suppressed for as long as that holds. Closing the palette on the click means the banner appears
instead, 3 px of the same hue against the panel's 2 px hairline, which is the louder of the two and
the one the owner asked to be *"much thicker"*. The palette's hairline keeps the cases it was really
for: an order armed by hotkey, or armed before the palette was opened.

**The test asserts the order still happens.** That is the half worth having. A close that also
swallowed the order would photograph perfectly and be a worse fault than the overlap it replaced —
the menu goes and nothing the player asked for does — so the tool is read back out of the director,
not just the panel's display. It has to be a PlayMode test: what is being proved is that a shell
built by the composition root wires the two together, and the fast tier compiles no shell. That is
the known gap in `CLAUDE.md` about clicks, met where it can be met.

Nobody has pressed Play on it.

### Three reports, one play session: the dead layer, the levitating climber, and the ladder that had to go through the floor (2026-09-18)

`docs/design/21-ladders-and-climbing.md` holds the rules. This is why they are those rules.

**The cancel bug was not in the cancel tool, and the clarifying question is what found it.** The
report was that a slab being built could not be cancelled. The follow-up — *did anything else on
that layer respond?* — came back "only blueprints were dead", and the slab was over open air. That
turns a tool bug into a picking bug in one sentence: the cancel path submits two intents per cell and
had never been reached, because `SliceCameraRig` raises no event at all when the pick misses.
`SlicePicker.Owner` knew an edifice, a floor, water and the block below; a site was none of them, and
sites are drawn straight into the renderer outside the mirror the picker walks. Over open air that is
fatal — nothing in the column — and over ground it merely answers with the cell one layer *down*,
which is why cancelling from the layer below sometimes worked and made the whole thing look
intermittent.

**The levitation was one missing case in a comment that had aged badly.** The climb pose was already
reached by a ladder step; every joint it moves is multiplied by `ClimbWeight`, which only rises while
a face is found, and the face came from a scan for a *solid* neighbour. The comment beside it said
the simulation refuses to lay a connector where there is no block, so a wall would always be found.
True of a mined shaft. Never true of a built ladder — `RefreshLadder` asks for no wall at all. A
ladder against slabs found nothing solid, the weight decayed to nought, and the figure rode the idle
up through the air.

**Two systems owned one plane, which is the fault this project has now had twice.** The mesher picked
the ladder's face from the first occluding neighbour and fell back to north; the director scanned for
solid in a different order and fell back to nothing. Where nothing occluded they disagreed
completely: a ladder drawn on the north face and a colonist standing up straight beside it. It is
`HopPriceHasOneOwnerTests` again, in presentation, and the answer is the same shape — one function on
the mirror both read, and a test that measures the drawn yaw rather than reading the source.

**The third report and the existing rule were the same arrangement seen from opposite sides.** A
ladder registered a connector only when both ends were walkable, and walkable needs a floor — so the
only ladder that had ever worked was one with a slab directly above it, which is precisely the
"colonists go through the floor" the owner was reporting. Refusing the placement on its own would not
have tightened ladders, it would have deleted them. So the shaft cell is open now, the ladder makes
its own top standable, and you step off sideways on to the slab beside it. `NavGrid.RefreshFrom`
already had the clause that makes it work — *a connector is its own floor* — written for shafts and
waiting.

**One deviation from the owner's answer, and it is not a softening.** They asked for no landing
anywhere to be refused at placement. Only the topmost ladder of a chain needs a landing and a player
builds a chain bottom-up, so demanding it at the order would refuse every ladder in a shaft except
the last, in the only order they can be built. It is asked at the connector instead, where
`RefreshLadder` already answers it again each time either end changes. The ladder is buildable and
opens nothing until the landing arrives.

**And the migration the plan worried about turned out not to exist.** The plan proposed stamping
holes in worldgen and accepting a save break, and flagged it as the one irreversible decision. It was
not needed: keeping "a real floor still counts" as one of the three ways a ladder may arrive makes
the new rule a superset of the old one, so every stamped city ladder and every ladder in an old save
keeps working. What the placement rule stops is any more being made. The cheapest fix and the
conservative one turned out to be the same fix.

**The golden master moved and named its own cause.** `Golden.City.Simulated` re-baked; `Generated`
untouched, which is the evidence that no generator pass changed. City ladders standing under an open
cell used to register nothing and now work, so the colony reaches places it could not. The meadow and
the played board did not move, because neither has a ladder on it.

**The photographs were the specification for the pose.** Five climbers from behind and one from the
side: both hands on rungs *above the head*, the trailing one at chin height, the stepped knee drawn
right up while the pushing leg stays nearly straight. The rock numbers say the lower hand hangs near
the hip and the step is modest, which is right for stone and reads as a shrug on a ladder. Four
numbers now branch on `Figure.OnLadder`; the pushing end of the cycle deliberately does not, because
full stretch is full stretch either way and lifting both feet reads as hanging.

Flushness needed no new number at all. `ClimbLean` already puts the body 0.30 m off the cell face,
about a body's depth from the rungs — it had simply never been applied to a ladder, because the face
was zero. Fixing the face fixed the lean with it.

Nobody has pressed Play on any of it.

### The second playtest: a blank roster, a ladder facing nowhere, and letting go after you have arrived (2026-09-18)

Four reports. `docs/design/21-ladders-and-climbing.md` §7 holds the rules; this is the reasoning.

**The first thing to establish was which build had been played.** The owner's checkout was on
`claude/rates-and-stats`, eight commits behind `origin/main`, with the ladder work still an open pull
request — so none of the morning's fixes were in what they were looking at. That is the
confirm-delivery lesson paying for itself: two of the four reports are about code they had not run,
and diagnosing them as regressions would have been a wasted afternoon.

**The roster's blank avatars were diagnosed by what still worked.** The pictures were gone from the
bar and present on the colonist card, and that asymmetry is the whole answer: a card is a slot that
re-reads itself only when the colonist in it changes, whereas the inspect pane asks afresh every time
it is opened. `PortraitStudio.Clear` destroys every texture when a colony is built or loaded, and a
new colony's pawn ids start at the same small numbers — so nothing about any slot had changed while
everything under it had been destroyed. An id cannot answer "does this picture still exist". A
generation counter can, and it costs one integer.

**The ladder's wrong side was an arbitrary answer the player could see was arbitrary.** `LadderFacing`
fell back to north wherever nothing occluded, which is fine as a tie-break nobody can observe and not
fine at all when the ladder is standing in the open. The owner picked exactly that case out of the
options offered. So the ladder rotates now — and the wall still wins wherever there is one, which is
not an exception but the rule: which side of a wall a ladder is bolted to is physics, not preference.

Two things had to follow it, and both would have failed silently. `RaiseEdifice` kept a facing only
for two-cell things, which was a perfectly good rule while a bed was the only rotatable thing and
would have dropped the player's rotation between the order and the built ladder. And a one-cell ghost
was drawn with no facing at all, so R would have turned nothing the player could see — the rotation
would have "worked" and looked broken.

**The jolt, the stall and the raised arms at the top were one fault.** The climb weight's target was a
flat yes-or-no on whether a face had been found, so it held at 1 for the whole step and only began
easing out on the frame the step *ended* — at which point the colonist was standing on the ledge.
Everything the owner described happened after the climbing was over: 0.15 s of a figure on solid floor
with its arms overhead, sliding most of a metre of lean back to the middle of its cell. Making letting
go part of the climb — the last quarter of the rise — fixes all three at once, because all three were
the same unwinding happening in the wrong place.

The direction matters and is the one thing a phase-only rule gets wrong: going down, the top of the
ladder is the *start* of the step, so reading the phase alone would have a colonist let go at the
bottom of every descent.

**The bed did not reproduce, and the test that failed to reproduce it is kept.** The far cell of a
two-cell thing is derived from the facing and validated at the order, for all four facings, before
the head cell's own check — and `BedTests.ABedIsRefusedWhenItsFarCellIsAWall` passes. The guard the
owner asked to be general already is: `Place` applies it to any `footprint > 1` def rather than to the
bed. So either what they saw is drawn rather than built, or it needs a sequence nobody has written
down. Keeping the test regardless: a guard nothing tests is a guard that gets tidied away, and this
one runs before a check that looks arbitrary until you need it.

### The bed still has not been reproduced, and the hunt for it found two other things (2026-09-18)

The owner reported the bed unchanged, which it would be — nothing had been changed about it. So the
placement path was walked end to end rather than reasoned about a third time: the intent seam
(`A`/`B`/`C` and `HandlePlace` passing them in that order), the gesture (`Commit` returns the anchor
for a single placement and `TryPreview` builds that same cell's footprint, so ghost and order agree
for every facing), the rotate key's shared binding (the rig skips slice-up exactly when
`RotatableArmed`, so R cannot quietly raise the slice under a bed), the mesher's yaw, and the drawn
bed's own extent — 4.6 m inside a 5 m footprint with 0.2 m clear at each end. Every one of them is
right. **It is still not reproduced**, and saying so is better than shipping a change that treats a
symptom nobody has pinned.

**One of the two things the hunt did find was an hour old and mine.** `Building_Ladder` gained
`rotates` in the Defs, and the HUD keeps a *parallel* table of footprints and rotatability because it
cannot see `Odyssey.Sim.Construction` (ADR 0003). `DesignateDirector.RotatableArmed` reads that
table — so the def alone would have left R raising the slice instead of turning the ghost, and the
player's rotation discarded, with every simulation test green, because the consequence is not in the
simulation at all. The fixture that was supposed to hold the two tables together checked their
*lengths* and spot-checked the wall and the bed: exactly the shape of test that passes while the row
that matters is wrong. It walks every handle against the Defs now.

That is the second time in two days that a rule with two owners has failed silently, after the
ladder's face. The pattern is worth naming: a parallel table is allowed here — ADR 0003 makes it
necessary — but a parallel table that nothing *compares* is a bug with a delay on it.

**The other was the guard the owner asked to be general, which already was, on the side they could
not see.** `Place` derives a two-cell thing's far cell from the facing and refuses the order when
anything stands in it. Correct, tested, and invisible: the ghost asked only about the cell under the
pointer, so a bed with its far half in a wall drew in its own material like any legal order and then
the click did nothing. A click that silently does nothing is indistinguishable from a click that
missed — which is one way "it doesn't respect where I placed it" gets reported, and it is worth
fixing whether or not it is the fault being chased. The ghost derives the footprint with
`EdificeFootprint` rather than restating it, because a cursor that disagrees with the order about
which cells a thing claims is the fault this line of work has already hit three times.

### The bed, found: two correct lines with a Clear between them (2026-09-18)

`Raise` derived the bed's far cell from `_facing[cell]`, called `Clear(cell)` — which zeroes the
site, facing included — and then read the facing **again**, out of the slot it had just wiped, on its
way to the record. Every rotatable thing was built facing north whatever the player chose.

**It hid because only the drawing was wrong.** The cells were derived before the clear and were
always right, so the footprint guard still refused a bed whose far half was in a wall, nothing was
ever built anywhere illegal, and the simulation was consistent with itself throughout. The record
said north, so the mesher drew the bed extending north from its head cell — into whatever was north,
walls included — while the cells it occupied were the ones the player asked for. A bed lying through
a wall it does not occupy. And `AimSleep` lays a sleeper out along the bed's facing, so the same
line put colonists across their beds: the "half way up the bed … hanging off" from the same report.

**Three tests had a clear shot and all three missed.** `ABedsFacingIsInTheStateHash` passes on the
difference between the two beds' *cells* rather than their facings — written to pin the facing,
pinning something that happened to move with it. `ABedIsRefusedWhenItsFarCellIsAWall`, written the
previous day expressly to reproduce this report, passes because the guard it tests was never the
broken part. And every other bed test places facing 0, which is also what a lost facing looks like.
The new test walks every facing the board allows and refuses to pass on fewer than two.

**The method is the lesson, not the line.** Three sessions of reading the placement path end to end
— the intent seam, the gesture, the shared rotate key, the mesher's yaw, the bed's drawn extent —
each concluded correctly that the part in front of it was right, and every one of those conclusions
was true. The fault was in the gap between two of them. Ten lines of throwaway test printing
*asked → got* found it on the first run. Reading tells you whether a line is correct; it does not
tell you what the value actually is at the moment it is used.

The screenshots were what made the probe possible. "It doesn't respect the rotation" is ambiguous
between the cell, the facing and the drawing; two pictures of the same three beds before and after
building said *a quarter turn*, which is one hypothesis and is testable in a single assertion.

## 2026-09-18 — "Sometimes" meant a race between two blueprints

Three ladder reports from the same playtest. The value of the day was in what did *not* get built.

**Report 2 was already done.** R, the turning ghost and the red refusal all landed in PR #110, which
is the build the owner was playing when they asked for them. Fifteen minutes of checking
`BuildShapes.Rotates`, the def and `OdysseyBootstrap.Refused` against the merge saved a unit of work
on a feature that already existed. Verify before building is not a slogan here; it is the second time
this week it has paid.

**Report 1 was two complaints and one cause.** "Can't place a ladder under a slab" is the rule the
owner asked for the day before and it stands — they chose refuse-and-say-nothing-more over
auto-deconstructing the slab or allowing an inert ladder, when all three were put to them. "And has
to be against the wall" sounded like a second bug and nothing in the code has ever asked for a
neighbouring wall. It is the same rule seen from inside a roofed room: every cell there is under a
slab, so the only cells that take a ladder are the ones past the slab's edge, which are the ones
beside the wall. Confirmed by the owner rather than assumed.

**Report 3's prime suspect was wrong, and the probe took one run to say so.** The standing theory —
written into the design document the day before as the deferred migration — was `LadderArrivesAt`'s
compatibility clause. It cannot be the cause on the board the owner played: **the wooded meadow
generates no ladders and no connectors at all**, measured on three seeds. That single number
redirected the whole hunt.

What it actually was: **the shaft rule asked the built world, and a blueprint is not built.** Order a
ladder, order a floor above it; each is legal on its own because neither exists yet. Both get built.
That is the whole of "sometimes" — it depended on which job a colonist picked up. The fix is that the
rule sees sites, and is asked again at `Raise`, because the rule spans two cells that are ordered
separately and the other order can legitimately arrive later. Only that rule is re-asked, not the
whole of `Allows`: a site with its own material hauled to it fails `needsClearCell`, and re-asking
everything would have refused every bed whose wood had been delivered. A general fix would have
introduced a worse bug than the one it closed.

**The clause went anyway and cost nothing, which is worth recording because the plan said otherwise.**
The owner accepted the save break; the plan said it meant stamping holes in worldgen and re-baking
`Golden.City`. All three golden masters came back byte-identical — worldgen's ladders reach the nav as
`StampedConnector`s and never consult `LadderArrivesAt` at all. The migration that had been deferred
as expensive turned out not to exist. Measuring the consequence beat reasoning about it, again.

**Two defects fell out that nobody had reported.** A shaft could only ever be one storey: a ladder is
`blocking false` so a colonist can stand in it, and `SomethingUnderfoot` wants a *blocking* edifice,
so the second ladder of a chain was refused and `LadderArrivesAt`'s own chain clause — written
expressly for that case — was unreachable for anything a player built. Every test in `LadderTests`
builds one ladder, so nothing had ever asked the question. And the first fix did not work: the
connector was gated on `CellGrid.IsWalkable`, which cannot see that a connector is its own floor, so
the chain built and the upper ladder silently had no connector. The pattern is the one this project
keeps meeting — two correct rules that disagree about the same question — and the answer was the same
as ever: one named owner for each half, `StandsOnSomething` for placement and `StandsOnAFooting` for
the connector, with the difference between them written down.

Both fixes were the owner's call, asked mid-unit rather than assumed, and both were told to go in.
### The candidate card lost its skills to a face, and nobody could see it (2026-09-18)

The owner, playing the setup page: *"I'm not seeing the skills rolled randomly on the character
generation screen — is that supposed to happen?"*

**The roll was never the problem, and measuring it first is what kept this from becoming a hunt
through `ColonistDraw`.** Three thousand draws off the same method the colony calls: only **2.6% of
candidates have every live skill at zero**, the best skill is 3 or better on **70%** of them, and a
sample deal reads `Hauling 5 · Cutting 8 · Mining 6 · Construction 2` beside `Hauling 1 · Mining 1`.
Every card also takes a fresh `SeedEntry.Draw()` off machine entropy, so Reroll genuinely redeals.
The simulation half was right all along.

**The card was showing a name and an occupation.** An occupation is drawn from its own salt —
deliberately, so that two facts about one person are not correlated — which means it tells a player
**nothing about what that person can do**. So the page whose entire job is telling three people
apart showed three names and three trades, and the skills were in the detail pane, for the one card
you had clicked. Comparing candidates meant clicking each in turn and remembering.

**Three dead artefacts said so, which is what made it attributable rather than merely visible.**
`HudLayout.ColonistCardSkills = 2`, read by nothing, carrying a comment about keeping the card's
height and its contents in step. `.colonist__skills` in the sheet, applied to no element.
`HudLayout.ColonistScreenHeight`, modelling a caption and a standalone colonist screen that
`BuildSetupPage` stopped drawing when the candidates joined the seed and the board size on one
full-viewport page — and the fast tier was asserting that model fits `StartListMax`, a box this
screen does not sit in.

**The cause was the avatar doubling, and it left a second mark that was in plain sight.**
`20-avatars.md` §10.6 took `Avatar` 30 → 60 and re-derived every card that carries one: the roster
card 106 × 63 → 126 × 89, the inspect header 38 → 60, the strip share, the top scrim, the coverage
ceiling. **The candidate card is not on that table.** It kept 47 and drew a 60 px face in it, at a
53 px pitch — so on `Logs/setup-page.png` the three faces run into each other and over the selection
outline, and the skills line had been squeezed out to make room for the trade.

**Nothing failed, and the reason is worth keeping.** `HudStyleSheetTests` pins `.colonist`'s height
to `HudLayout.ColonistCard`, so the sheet and the model agreed — because neither had moved. A
consistency test between two copies of a number cannot notice that the number is wrong. The check
that was missing is one line of arithmetic nobody thought to write: **a card is at least as tall as
the face it carries.** It is `EveryCardIsAtLeastAsTallAsTheFaceItCarries` now, asked of all three
cards at once, and `StartScreenTests.TheCandidateCardsDoNotRunIntoEachOther` asks the same of the
laid-out elements, where a player would ask it.

**What reading the code could not settle, and the picture did in one look.** Most of an hour went
into the fixed box's arithmetic — three lines come to 296 against a body of 284, so a third line
does not fit — and every bit of that was a correct answer to a question that had stopped applying.
The screenshot showed the setup page occupying the top third of a 1080p canvas with some seven
hundred empty pixels under the cards. The layout constants had modelled a screen that no longer
existed, and *reading them more carefully would only have made the wrong model more convincing.*
Run the shot first: `scripts/unity.sh test playmode -testFilter …PhotographTheSetupPage`.

**The card is re-derived from its own rows**, the way §10.6 did the roster card: 29 name + 18 trade
+ 18 skills = 65, clear of the 60 px face. The column went 260 → 300, because a 60 px face and a
third line left 176 px of text where §3 sized 206 and the page is the full viewport rather than the
fixed box. The trade drops from `TextMeta` to `TextDim` so the three lines read as a hierarchy on
the theme's four existing tokens rather than a fifth being invented — name, then what they can do,
then what they used to be. `SkillSummary` in `Odyssey.Hud` owns the line's rules, so live-only,
no-zeroes, ties-in-reading-order and the em dash for the one candidate in forty with nothing to show
are fast-tier tests rather than things discovered on screen.

**The general lesson, and it is the rates review's from the day before, arrived at from the other
end: a constant nothing reads is not harmless.** Three of them here described the screen as designed
while the screen had quietly become something else, and each of them would have been believed by the
next session to read it. One of them was being asserted by a passing test.

### The setup page, played twice in a day (2026-09-18)

Five corrections after the owner played §6b's card, and the first of them takes §6b's own feature
off again: *"from the left hand panels, no need to display any skills there — Name, Age, Occupation,
and resize occupation accordingly to a bigger size."*

**That is worth reading carefully, because it looks like a reversal and is not.** The original
report was that nothing on the page varied by ability, so the roll looked broken. §6b answered it in
two places at once: it put a skills line back on the card *and* it made the detail pane legible —
two columns, real spacing, a bigger type step. Having played that, the owner kept the second and
dropped the first. The report is still answered; the card is identity alone and the pane carries the
numbers. **The card's skills line should not be restored as a fix for the original report**, and
`18-colonist-select.md` §6c says so in place.

**`SkillSummary` and its seven tests are deleted rather than left unused.** That is §6b's own lesson
turned on §6b: three dead constants describing a line nothing drew are what made the first loss
invisible, and leaving a ninth-tenths-finished formatter behind "in case" would have been the same
mistake with fresher paint.

**Two headings wanted two different answers to one request.** "Bigger bolder headings" arrived for
section titles and for field captions in the same message. A section over a block on a full screen
read at leisure is `HudTextRole.Name`, 19/600 — the one step of the scale that is both bigger and
bolder than the body under it. A caption over a text field is `PanelLabel`, 11/600 upper and
tracked, which is what the stores panel, the rail and the alerts list are already introduced by and
reads unmistakably as a label rather than a value. Neither adds a rung to `HudType`.

**A specificity trap, found by reading and not on screen.** Outlining every pressable row on the
page needed `.setup .settings__row` — scoped, because that row is also the settings panel's and the
Menu popover's, and a grid of outlines over a running world is noise. That selector is 0,2,0 and the
green Start row's `.setup__commit` was 0,1,0, so **the grey border would have won and Start would
have quietly stopped being green** — a change that undoes a change made an hour earlier, with
nothing failing. Specificity beats order in USS as in CSS. The green rule is a descendant now too.

**And the owner's sharpest note of the day was three words long:** *"not to reinvent"*. The page
wanted a translucent backdrop; the first instinct was to pick a colour. It wears `.panel` and
`.window` instead — the classes the settings panel, the Menu popover and the start screen's own
panel are built from — so the fill, the border and the radius are tokens the sheet already pins and
nothing about the colour is restated. `.setup` overrides only where it sits and how much air it
keeps. **The general form: when a screen needs to look like the rest of the interface, wear the
interface's classes rather than copy its values.** A copied value is a value that drifts.

### A name pool that is one file, generated, and costs nothing to read (2026-09-18)

The owner supplied about 240 given names in three lists — ordinary ones, invented ones, and a run of
British nicknames (*Spudgun*, *Treacle*, *The Dude*) — and then, mid-change, the two constraints
that decided the shape: *"make it performant then and centralise it if need be."*

**The pool was eight names in a C# array**, with a comment promising it would reach "about forty at
M2, when pawn generation needs a pool that does not repeat in a colony of fifty". That promise was
three milestones old. It is 244 now, which is six times what it asked for, and
`ThePoolOutlastsAnyColonyThisGameBuilds` walks a colony of fifty and asserts no two share a name —
so the `"Wrenn 2"` suffix a ninth colonist used to get is unreachable by any colony this game
builds. The branch is kept because it is what makes the method total, and it is the only line in the
namer that allocates: on every path anybody actually walks, naming a colonist allocates nothing.

**Centralised the way the icon keys already were.** `docs/design/colonist-names.csv` is the one
place a name is decided; `emit_labels.py` — the generator that already turns `icon-keys.csv` into
`Registry.g.cs` — gained a second output rather than a script of its own, so it is still **one
generator and one `--check`**, and CI covers the new file without a workflow change. The wiki gained
a page listing all 244 with their register and gender, because the whole reason names are content is
that the owner can read them and strike the ones they do not want.

**Performance was the easy half and worth stating anyway.** The generator writes string literals, so
the pool lives in the assembly's constant pool and naming a colonist is an index and a modulo — no
parse, no file read, no dictionary, no allocation. That matters because the roster strip and the
inspect header ask per figure per frame.

**Two names were dropped as duplicates and the generator now refuses them.** *Nova* appeared in both
of the first two lists and *John* in the first and third; a pool with a repeat in it would name two
colonists in one colony the same thing, which is precisely the fault the whole seed-and-id scheme
exists to prevent. `load_names` raises on a repeat rather than silently deduping, because a name
quietly vanishing from a 244-row CSV is not something anybody would notice.

**The order of the CSV is load-bearing, and that is the trap to write down.** A name is arithmetic
on a saved seed and a slot, so **sorting the file renames every colonist in every existing save**.
Add to the end; never sort. Growing the pool from 8 to 244 already did this once — every colonist in
every save made before today now goes by a different name — which is harmless exactly once and the
reason the rule is stated in the CSV's own wiki page, in the generated header and in the namer.

**Gender is recorded and nothing reads it, deliberately.** The owner asked "if can apply to gender".
It cannot yet, and the reason is not the names: **no pawn in the simulation has a gender at all**,
and the drawn colonist is one of sixty-one Synty models in a single undifferentiated family, so a
gendered name would be contradicted by the figure beside it about half the time. Adding gendered
names without a gendered figure would make the game look *more* wrong, not less — there is currently
no expectation for a face to fail. The column is in the CSV because it cannot be re-derived cheaply
later and because the wiki is where the owner corrects it; it is **not** generated into C#, since a
constant nothing reads is the artefact this project keeps being bitten by.

**And the pool cost ten pixels a card, after two rounds of getting it wrong.** The Unity tier
failed on `TheCardIsWideEnoughForItsRowsAndNoWider`'s **lower** bound — the roster card budgets
50 px for a name and *Christopher* draws 60 — which is the bound that exists for exactly this and
which `CardWidth`'s own comment had predicted in words a month earlier.

The first answer was to shorten that one name, on the reasoning that it was the only entry over ten
characters. **The next run named *Alexander*: nine characters, 52 px, where *Christopher* is eleven
and 60.** Character count does not predict width, which is a sentence I had written into a test
comment on the previous commit and then immediately acted against. "Trim the long ones" is not a
rule anybody can apply — it is guessing until CI stops complaining, one round at a time, against a
list that is the owner's content rather than ours.

So the card is sized once to the widest name the pool can produce, which terminates, and the
coverage ceiling goes 20% → 21% (the forced two-row strip at 1280 × 720, 19.80% → 20.19%). **The
fourth raise of a number that is the owner's**, and it is recorded beside the other three with what
it buys: no colonist's name is cut short on the roster. The reversal is cheaper than the avatar's —
ten of the 136 pixels are the name budget, so putting the ceiling back is a content decision about
accepting an ellipsis on the longest few names, which is the one thing on a card this interface
already permits to be cut short.

**The reusable half is about proxies.** A fast-tier test cannot measure text, so it guarded the pool
by character count — and passed an eleven-character name that then failed the pixel measurement.
A proxy that does not fail where the real thing fails is not a cheap version of the gate; it is a
second opinion nobody asked for, and it is worse than nothing when it is believed. That test now
says out loud that it only catches the absurd and that `HudGeometryTests` is the gate.
### The rates line, reviewed: one field carrying two units (2026-09-18)

Five fixes off a fresh-eyes review of `WS1`–`WS3`, on a worktree built from the pull request head.
Four of the five are one fault wearing different clothes, and the fifth is a comment that had been
doing arithmetic on the wrong cadence.

**`ToilProgress` was counting ticks in three drivers and thousandths in four.** `WS1` scaled the
work toils so a rate could change how fast a colonist pays without changing what anything costs,
and left the three toils no rate can speed up — eating, sleeping, standing down — on a bare `++`.
Internally each was consistent, which is why nothing failed. Across the field they were not, and
the field is saved and hashed. Two things followed. `Rates.FromSave` is told a format version and
nothing else, so on a pre-format-5 file it multiplied *every* value by a thousand: right for a
half-mined rock, wrong for a half-eaten meal, which then finished on the next tick. And
`Pawn.ContributeTo` divided the field back, so eat, sleep and wait read zero for the whole of their
length and reached the hash not at all. A toil with no rate now pays at exactly `Rates.Scale` a
tick — the standard rate, said in the unit everybody else is speaking — and
`ToilProgressHasOneUnitTests` fails the fast tier on any `ToilProgress++` left in the simulation.

**The same division was costing the hash three decimal places everywhere else.** All four
accumulators were hashed divided back to whole ticks. That was WS1's price for landing with no
golden moving, and it was the right trade for one commit; WS3 then re-baked every `Simulated` value
anyway and the division outlived its reason, leaving a blind spot a thousand milliwork wide — two
runs could differ on a cell and agree until the difference happened to cross a tick boundary. A
hash that is late to notice a divergence is the thing this hash exists not to be. Hashed whole now.

**All three goldens moved, and that the run did not change was measured rather than argued.** With
the other three fixes in place and only these two lines reverted, the table comes back to the
values the branch committed, to the digit. So the colonists walked the same walks and swung the
same swings; what moved is what the hash can notice about them. No `Generated` value moved, as none
could — nothing here runs before the first tick.

**`starvationPerInterval` was four times faster than every sentence describing it.** The comment
read the needs cadence as 200 intervals a day. It is 400 — a 60,000-tick day over the 150-tick
cadence — so at 2 per interval the bar filled in a day and a quarter where the Def, the field and
the design all promised two and a half, and severe malnutrition arrived in the fourth day of not
eating rather than the fifth. **Nothing had ever measured it:** every band test set
`StarvationSeverity` by hand, so the only new Def integer with no test was the one that decides how
long starvation takes to bite. It is 1 now, the arithmetic is written out beside it rather than
summarised, and `TheBarFillsAtTheCadenceItsCommentClaims` holds the sum and the tick path together.
No golden moved — no golden window lets a need reach zero — which is also why the WS3 soak's
condition comparison was honestly vacuous.

**A pace cached off a seed that arrives later.** `InnatePacePerMille` caches on first read, which is
right; the seed it reads is restored by `PawnSeedSection`, which runs *after* the pawn section that
made the pawn, and is rewritten again whenever a candidate is rerolled on the select screen.
Nothing reads a pace that early today, so nothing was wrong — but this project has already rolled
an entire colony from seed zero by exactly that route, when `PawnContext.Seed` was unset until the
first tick. `RollSeed` is a property now and its setter drops the cache, which turns a live trap
into a closed one for the cost of four lines.

**And a colonist could collapse onto her own bed.** `SleepJobDriver` tested zero rest before it
tested arrival, so a colonist whose rest ran out on the tick she stepped onto her bed took the
collapse branch — and rest effectiveness is read off the cell while the thought was not, so she got
the bed's rate and the mud's memory. Arrival wins: there is no walk left to cut short.

**What the five have in common is that none of them could fail a test that existed.** Three were
invisible because the thing they corrupted was only ever read back by the same code that wrote it;
one was a comment; one needs a window a few ticks wide. The tests added here are the cheap general
forms — a source scan for the unit, one arithmetic assertion beside one tick-driven one for the
cadence, and a control apiece for the cache and the bed.

### The rates branch catches up, and the goldens did not move (2026-09-18)

`main` had gone twenty-two commits ahead, so `WS1`–`WS3` was merged up: the review fixes first, then
`main`. Two conflicts, both the same append-collision in `CLAUDE.md` and `docs/journal.md`.

**`Golden.cs` merged clean, which is the outcome to be suspicious of.** The last time these two
lines of work met, the city hash conflicted and *neither side's value was right for the merged
code*; a silent auto-merge is that same danger with nothing to flag it. So the three were run rather
than trusted — with the two controls beside them, `TheHashActuallyDependsOnTheWorld` and
`TheHashDependsOnHowLongItRan`, because a golden that passes because the hash has stopped depending
on anything is worse than one that fails.

All five pass, and the reason holds up to inspection. `main` touched `Golden.cs` not at all since the
branch diverged, and its only change under `Assets/Odyssey/Sim` is `ConstructionGrid`: the new
`RunLandsOn` for drag preview, and a rewrite of the ladder-under-slab placement rules. **Those are
order-time rules, and no golden issues an order** — every case builds on `Scenario_Bare`, which has
never given a standing order in its life. The first read of that diff was "purely additive", which
was wrong: twenty lines were removed. The claim that survives is narrower and is the one that
actually explains the result.

**The counts were resolved by running them, not by adding them up.** 721 Sim + 409 Hud, Long 21 —
the merge of a branch at 712 + 406 with a main at 694 + 409, which is not an arithmetic anybody
should attempt in their head.

### Chopping gets a skill of its own, found by a player feeling it work (2026-09-18)

The owner, playing WS2: *"I noticed the chopping varied in speed — could we possibly add that to
the skills in all the places it needs to be and assign one there?"*

**Chopping had no skill on screen, and the axe work was levelling up Growing.** `SkillCatalogue`
mapped the simulation's `Skill_Cutting` onto `ui.skill.growing`, on reasoning that was perfectly
defensible when it was written: felling is plant work, the canon work type is "cut plants and clear
growth", and growing was the only plant skill in the list. The consequence was that a colonist who
spent a day with an axe got better at *Growing*, and a player looking for the number behind the
speed they had just watched change found nothing called Chopping anywhere.

**It was found from the far end, which is the interesting part.** Nothing was broken — the sim had a
`cutting` skill all along, it was saved, hashed, and driving the rate correctly. What was missing
was only the name, and a missing name is invisible until somebody has a reason to go looking. WS2
gave them one: **a skill that does something is a skill people try to find.** For three milestones
the mapping was harmless because no rate read a level; the day one did, it stopped being harmless.

**The word is the owner's and the family now agrees.** The order says Chop, `ui.status.felling` says
Chopping, and `ui.work.cutting` said *Cutting* until this change brought it along. Four surfaces,
one word. **The key stays `cutting`** — `ui.skill.cutting` — because it matches the simulation's
`SkillIndex.Cutting` and a key is a stable identifier rather than a label; three rows of that
catalogue already do not spell their own labels.

**Growing goes back to being unsimulated**, with the reason every disabled row must carry: nothing
is planted yet. **The new row has no art** and says so in `icon-map.csv` rather than borrowing the
seed-sack picture — a wrong icon is worse than an outlined square, because the square admits it.

**Two tests changed and one got stronger.** The pair that pinned "felling is plant work and trains
growing" now pin chopping's own row; the assertion that a borrowed row explains itself was replaced
by one naming the live set outright — `ui.skill.mining` and `ui.skill.cutting`, each under its own
name — because asserting a borrow that no longer exists would pin the very thing this removed. The
grid is unmoved at seven rows: `(13+1)/2` and `(14+1)/2` are both 7, so nothing in the pane or the
setup page had to be re-derived.

**And the art gate caught the missing icon within one CI round.** Adding Chopping without a picture
failed `TheSkillsTabSwitchesAndDrawsTheOwnersArt`, which asserts `drawn == All.Length - 1` — *every
skill but social is cut from the owner's sheet*. Expected 13, drew 12. That is a self-maintaining
rule rather than a count somebody has to remember to bump: **add a skill and you have added an
obligation to draw it**, and the alternative was an outlined placeholder square sitting in the
Skills tab indefinitely with nothing to report it.

The owner supplied the tile the same afternoon, a 32 px framed action tile in sheet 06's own format.
**It went in as a sheet of its own rather than into sheet 06**, and the reason is worth keeping: the
icon map records which cells are *used by keys*, not which cells hold art, so an unmapped cell of
somebody's sheet is not a free cell — pasting into one risks painting over art nobody has mapped
yet. `09-supplied-tiles.png` is one cell wide and says in `sheets.csv` that it grows a column at a
time, so the next one-off has somewhere to go that costs nothing to find.

The pipeline then did the rest on its own terms: `detect` agreed with the registry, `export` wrote
64 x 64 RGBA8 at nearest-neighbour scale 2, and **no other icon changed by a byte** — which is the
check worth making after any export, because the tool rewrites all of them and a silently re-encoded
sheet would be invisible in a diff of thirteen files.

### Interactive Alerts: subject selection, dismissals, and vertical alignment (2026-09-18)

Settled through Ground → Interview → Plan → Execute on branch `claude/interactive-alerts`.

- **Alerts were verbose sentences with trailing details.** "A colonist is close to breaking — mood has fallen into the strained band" was passive and took two lines in a 242 px column, pushing the depth rail and panel stack down. It now takes the direct form "Theodore is close to breaking" or "Theodore is starving", with the subject highlighted in bold severity ink (Red for Danger, Amber/Yellow for Warning, Accent Gold for Notice).
- **Clicking the entire alert row selects and jumps.** It calls `HudDirectors.ChooseColonist(pawnId, snapshot)`, which sets the slice layer, selects the colonist in `SelectionDirector`, and focuses the camera directly on them — identical to clicking their card in the top roster bar. Hovering anywhere on the row highlights the row (`rgba(255, 255, 255, 0.05)`) and shows a link cursor.
- **Alert rows carry aligned dismiss 'X' buttons in a column.** Each row has an 18×18 px dismiss element with `HudGlyphKind.Close` aligned to the right edge. Dismissing suppresses the alert until the underlying need condition clears and later re-occurs (e.g. food climbing back above `StarveClearAt` removes the dismissed latch). `PointerDownEvent` and `ClickEvent` on dismiss call `StopPropagation()` so dismissing never triggers row selection.
- **A panel-level Clear All button sits in the header.** An 'X' in the top right-hand corner of the Alerts panel header (using the standard `CloseButton` styling opposite the "Alerts" label) clears all currently visible alerts at once.
- **Symbol vertical alignment and compact single-line height.** The alert symbol is vertically centered with the text (`align-items: center`). Single-line alert row height is updated to 26 px (`HudLayout.AlertHeight = 26`), matching UI Toolkit's 13 px text box, reducing HUD screen coverage and avoiding unnecessary multi-line row clamping.
- **Reconciled with `origin/main` to resolve name disconnection.** Merged PR #114, PR #115, and PR #111 into the branch. The old branch was generating names from the retired 8-name mockup array ("Wrenn", "Odile"...) while `origin/main` had moved to the generated 240-name pool (`ColonistNamePool.Names` in `ColonistNames.g.cs` from `docs/design/colonist-names.csv`). Reconciling ensures alerts, roster cards, inspect panels, and the start screen draw identical names from `ColonistNames.Of(snapshot, pawn.Id)`.
- **The fast tier caught the style rule; Unity caught the nullable contract.** `HudStyleSheetTests.TheSheetSetsNoTypeAtAll` prevented `-unity-font-style` in USS (font weight belongs strictly to `HudType`/`HudText` in C#). And Unity batch compile caught `CellRef` as a non-nullable value type, enforcing `CellRef?` across `AlertRow` and `AlertRowView`.
- **Gates verified:** Fast tier 721 Sim + 411 Hud passed; EditMode 1675 total, 1662 passed, 0 failed; PlayMode 80 total, 75 passed, 0 failed; both wiki checks clean (`build_wiki.py --check`, `emit_labels.py --check`).

### Roster Top Bar: vertical layout, badged activity icon, and widened name budget (2026-09-18)

Settled through Ground → Interview → Plan → Execute on branch `claude/roster-card-layout`.

The owner: *"We need to rearrange the roster top bar - the name of the person should appear directly below the portrait, remove the word of their activity and leave the icon. This icon would be displayed maybe right of the name to indicate what activity is taken place - or whatever you suggest - this would allow longer names to be used with the width reclaimed back from this change. Interview and clarify for details"*

**The old geometry constrained names to a 50 px box.** The previous card (126 × 89 px) placed the 52 × 52 px avatar on the left and the colonist's name on the right. With 8 px padding on each side, a 52 px face, and an 8 px gap, only `126 - 16 - 52 - 8 = 50 px` remained for the name (`CardNameBudget`). Longer names in `colonist-names.csv` had to be culled or truncated to fit. Beneath that row sat a full-width activity line with a 17 px icon and a text word (e.g. "Chopping", "Deconstructing").

**The vertical arrangement reclaims both top-bar capacity and name width:**
- **Vertical stacking:** Avatar sits at the top of the card inside `.card__avatar-box` (52 × 52 px). The colonist's name sits directly below the avatar, centered horizontally (`-unity-text-align: middle-center`).
- **Activity icon badge on avatar corner:** Rather than competing with the name for horizontal space on the name row, the 17 × 17 px activity icon (`.card__badge`) is docked as an overlay badge in the bottom-right corner of the avatar box (`position: absolute; right: -2px; bottom: -2px`). This frees 100% of the row beneath the avatar exclusively for the colonist's name.
- **Activity text word removed & clean presentation:** Per the interview decision, the visible text word for the activity is removed from the card, and verbose hover tooltips are suppressed. The colonist inspect card informs the player of all job, need, and layer details on click.
- **Card narrowed from 126 px to 96 px (`HudLayout.CardWidth = 96`):**
  - **Reclaimed screen width:** The top bar fits ~30% more colonists per row (12 cards vs 9 at 1080p; 5 vs 4 at 720p).
  - **Expanded name budget:** `CardNameBudget` is now `CardWidth - 2 * CardPad = 96 - 16 = 80 px` — a **60% increase** over the previous 50 px budget. Names up to ~14 characters fit comfortably without truncation.
  - **Height maintained at 89 px (`HudLayout.CardHeight = 89`):** 8 px top pad + 52 px avatar + 2 px gap + 19 px name + 8 px bottom pad = 89 px.
- **`StripClearance` (32 px) added to `StripRoom`:** Narrowing cards to 96 px allowed 6 cards to squeeze into 1280 × 720, crowding within 26 px of the clock and pushing resting HUD coverage to 20.63% (over the 20.00% ceiling). Adding a 32 px clearance margin ensures breathing room between the centered strip and the corner columns, capping 720p to 5 cards (508 px wide, 4.90% coverage), which keeps total resting coverage at **19.63%**, strictly preserving `HudLayout.CoverageCeiling = 0.20f`.
- **Tests updated:**
  - `HudStyleSheetTests`: token checks updated for `.card`, `.card__avatar`, `.card__avatar-box`, and `.card__badge` against `HudLayout`.
  - `HudGeometryTests`: `TheActivityLineLeadsWithAnIconAndStillHoldsItsWord` replaced by `TheRosterCardBadgesItsActivityIconOnTheAvatar`; `TheCardIsWideEnoughForItsRowsAndNoWider` updated to verify avatar row and 80 px name row on a 96 px card.
- **Gates verified:**
  - Fast tier: 742 Sim + 411 Hud passed.
  - Unity EditMode: 1706 total, 1692 passed, 0 failed.
  - Unity PlayMode: 80 total, 75 passed, 0 failed.
  - Content gates: `build_wiki.py --check` and `emit_labels.py --check` both clean.

### The floor's dotted seam was the tile's own rim, and four placement fixes proved it was not placement (2026-09-18, branch `claude/grey-floor-layer`)

The owner, with two screenshots of a wood slab field: *"you can see these slabs leave small
artifacts/lines or gaps that don't even up … you can notice this when you look at the ground from
certain angles — you see a slight issue with not being fully flush."*

- **It reproduces headlessly, and that is most of the work.** `SlabFlushProbe` lays a wood floor on
  the meadow — on the ground, which is what the owner is photographing — and shoots it at the play
  camera's 48° and at a grazing 25°, with the relief on and off. The artefact is a **dotted dark
  grid on the cell pitch**, dense at the low pitch and thinner at the play pitch. `SeamProbe`, which
  already existed for the same report a day earlier, had only ever shot a deck floating in the air,
  at 30 m, where the thing cannot be seen: its own comment says the mismatch is "about 2 mm, which
  is under a tenth of a pixel at the camera the owner was using", and that is why a day's work had
  come back "clean at every range".
- **Pixels, not pictures.** Counting pixels a good deal darker than all four of their neighbours,
  inside a box that holds nothing but floor, turned each experiment into one number. Every wrong
  answer below was killed by that number in one run apiece, and three of them had looked plausible
  in a screenshot.
- **Four placement fixes, all measured, all wrong.** *The drape's shear*: the flat board draws the
  same grid, so no. (The arithmetic is worth keeping: neighbouring tangent planes agree to 1.2 mm at
  the middle of a shared edge and part by **15 mm at the corners** — the `GroundRelief.Period`
  comment's 14 mm, not `SeamProbe`'s 2 mm, which was the mid-edge figure and the reason the shear
  was cleared too early.) *The ground or a wall beneath it*: a deck two layers up in the air draws
  it too. *Growing each tile so neighbours overlap*: 6 mm changed nothing and **200 mm changed
  nothing**, which is the measurement that finally pointed at the answer. *Staggering alternate
  tiles by a millimetre*: no change either, and for a reason worth writing down — a tie exposes the
  rim by nothing and a stagger exposes it for real, so there is no value of it that helps.
- **The art was measured too, because "it is the art" was the one hypothesis nobody had tested.**
  `SlabTopFaceProbe` turns Read/Write on for the one model, measures, and turns it back off. The
  piece is a plain box: 2.5000 × 2.5000 m, 40 vertices, its top face **flat to the micrometre** and
  the full width of the piece. So the art is exact and two tiles do meet flush.
- **Which leaves the rim, and the rim is the answer.** The top edge of a tile's rim *is* the
  perimeter of its top face, so it ends exactly in the plane of the neighbour's top face. Equal
  depth is a tie; a tie is decided per pixel; and a vertical face under a 72° sun comes back at four
  tenths of the brightness of the deck. Sampling the dots confirmed it before any fix was written —
  rgb(52, 36, 25) against rgb(139, 100, 65) beside it, the same hue at 0.37, which is wood in the
  dark and not a hole, not the grass and not the sky.
- **The fix is to stop drawing the rim.** `CellMetrics.FloorTile` squashes a floor plate to a sheet
  about its own walking surface — a tenth of a millimetre — so the rim is degenerate on screen and
  generates no fragments to win a tie with, and grows it 3 mm past its cell so two neighbours
  overlap rather than share an edge. **470 → 16** at 48°, **884 → 35** at 25°, **73 → 3** on the
  floating deck. Paving goes through the same matrix, because it is the same plate.
- **The project had already written the argument down and not applied it to floors.**
  `WorldRenderModel.ResolveTerrain`: *"Water is a surface, not a floor. It asks for a sheet rather
  than the slab every other non-solid terrain gets, because a slab has sides and an underside that
  water cannot afford to draw."* Every other non-solid terrain got the slab.
- **What it costs, said plainly because nobody has pressed Play on it.** A floor over open air loses
  101 mm of drawn thickness and its lip reads as paper seen edge-on; a floor on the ground had 93 of
  those millimetres buried anyway. If the lip matters, the answer is a **fascia on the face** — the
  idiom `ChunkMesher` already uses for walls — and that wants a panel module rather than a constant.
- **Guards:** `ChunkMesherTests.AFloorTileIsDrawnAsASheetAtTheHeightAColonistWalksOn` asserts flat
  **and** still a clearance above the cell floor plane, because squashing about zero is just as flat
  and puts every floor back on the plane it z-fights (P7, written about this very constant a day
  earlier). `.TwoNeighbouringFloorTilesOverlap` pins the knit. New pattern **P8** in
  `docs/bug-patterns.md`: when an artefact tracks the cell pitch and survives the board going flat,
  it is the piece's own edge and no amount of placement will move it.
- **Verified:** fast tier 723 Sim + 411 Hud; EditMode **1712 total, 1698 passed, 0 failed**;
  PlayMode **80 total, 75 passed, 0 failed**; both content checks current.

### The roster bar kept the last colony's names, and the guard for it had to be driven through the real shell (2026-09-18)

The owner: *"the colonist info card and the roster top bar names don't match up? there has been a
mix up?"*, and then the sentence that solved it — *"maybe to do with loading and saving another
game?"*

- **A pawn id is not a person.** Every colony numbers its pawns from one, so the first roster slot
  holds `PawnId(1)` in every game there has ever been. A roster card is a slot that re-reads itself
  only when the colonist in it changes, and it asked `view.LastId != model.Id`. Load another colony
  and that is false, so the name and the face — both read once, both derived from the roll seed —
  were never rewritten. The inspect pane reads afresh every frame, so it was right, and the two
  disagreed on screen.
- **This is the *third* time the same fault has been fixed, each time for one thing.** A few hours
  earlier the same slot logic left the bar's portraits blank on start and on load;
  `CardView.LastPortraits` fixed the picture with a generation counter and its comment spells the
  cause out in full — *"a new colony's pawn ids start at the same small numbers, so the slot's id
  had not changed"*. The name and the face were sitting two lines above it and were not looked at.
  Answering *is this the same person* would have covered *do the pictures still exist*; answering
  only the second did not.
- **The fix is one `||` and a published seed.** `RosterCard.Seed` carries the roll seed the name was
  made from, so the view compares a person rather than a number, and the model reads the seed once
  and uses it twice instead of the frame being asked the same question in two places.
- **The guard had to be a PlayMode test, and proving that was the point.** Every unit test of
  `RosterModel` passed throughout — the model always had the right names. The fault was entirely in
  the view, so `HudGeometryTests.TheRosterBarFollowsTheColonyIntoANewSession` builds the real shell,
  reads the labels the bar is actually drawing, tears the session down and builds another (which is
  `LoadSession`'s own first two lines), and reads them again. **Run against the bug it fails with
  the owner's report in its message**: the bar saying `Spudgun` where the frame says `Ivy`. A guard
  that has not been seen to fail is not a guard, and this project has shipped one of those before
  (P7).
- **And the Unity tier's only red was not this branch's work at all.**
  `EveryFloorSlabPutsItsWalkingSurfaceOnTheCellFloor` skips catalogue rows whose art is missing and
  then asserts ten rows were checked — two reasonable halves that together require `Assets/Synty`,
  which the self-hosted runner has not got. It had failed every CI run on the branch since it was
  written and looked like the branch's own doing. It now asks the library whether *anything* has art
  before demanding the count. `docs/lessons.md` has it, under the rule it broke.
- **Verified:** fast tier 723 Sim + 412 Hud; EditMode **1713 total, 1699 passed, 0 failed**;
  PlayMode **81 total, 76 passed, 0 failed**; both content checks current.

### Roster top bar pagination and slot reordering (2026-09-18)

The owner: *"when many colonists are generated - the roster top bar stops generating profile cards - I suggest a small toolbar control that sits alongside the right hand side of the roster bar which is effectively pages of colonists to display - so you switch between the pages of colonists to the maximum or maybe suggest a better way to handle many colonists - also take into account when you say click on an alert to go to a person, the profile switches to that page. Also if you could make it so if you right click and hold on a roster profile card you can drag and drop them between slots and it will swap them"*.

- **Overflow pagination**: When the colony size exceeds the capacity of the roster bar (computed via `CardsPerRow(width) * StripRowsAllowed(height)`), the roster now pages across colonists rather than truncating them or overflowing into adjacent regions.
- **The pager toolbar**: Docked cleanly alongside the right-hand edge of the card matrix. Features `< [page] / [total] >` controls with `ChevronLeft` and `ChevronRight` vector glyphs drawn via `Painter2D` in `HudGlyph`. It hides automatically (`display: none`) when all colonists fit on a single page. Mouse wheel over the strip cycles through pages.
- **Steady-state zero GC**: Following ADR 0003, page labels cache last-seen page indices and rebuild their string text only on page transitions.
- **Selection synchronization**: Selecting a colonist in the 3D world, clicking an alert, or navigating to a colonist flips the roster bar to the page containing that colonist's profile card, without resetting manual page navigation during steady state.
- **Direct slot swapping on right-click drag-and-drop**: Holding right-click and dragging a card shows a semi-transparent drag ghost following the cursor, highlights target cards with `.card--drag-target`, and directly swaps positions (A ↔ B) upon release. Edge hover paging allows dragging across page boundaries.
- **Persistence across save/load**: Custom colonist order and the active roster page persist in `ViewStateSection` (version 2) under the `"view"` save key, isolated from simulation hash determinism.
- **Guards**:
  - `HudModelTests.RosterPaginationClampsAndSlicesCorrectly` asserts pagination mathematics.
  - `HudModelTests.RosterEnsurePageForSwitchesActivePage` asserts selection synchronization.
  - `HudModelTests.RosterSwapDirectlySwapsColonistPositions` asserts direct slot swapping.
  - `ViewStateTests.TheRosterOrderAndPageSurviveAStreamAndRestore` asserts save/load round-trip in PlayMode.
- **Verified**: fast tier 723 Sim + 416 Hud; EditMode **1717 total, 1703 passed, 0 failed**; PlayMode **82 total, 77 passed, 0 failed**; both content checks current.

### Roster single row, 6-card capacity, and fixed-position docked pager (2026-09-18)

The owner: *"Should be one row with 6 on an more (no 2 rows or anthing - always one). The pagination control needs to stay in the exact same place but it moves around according to how many colonists on that page, also move this control flush next to the last possible 6th slot with little spacing and make sure it stays in fixed position"*.

- **Single row strictly enforced**: `HudLayout.StripRows = 1`, `StripRowsAllowed(height) => 1`, and `StripRowsUsed(...) => 1`. The roster bar never wraps to two rows at any resolution.
- **6-card capacity per page**: `HudLayout.StripCardsCap = 6` clamps `CardsPerRow(width)`. Colonies of 6 or fewer occupy a single row of up to 6 cards; colonies of 7 or more paginate across pages of 6 cards each.
- **Stationary pagination control**: Previously, `_cardsHost` dynamically sized to the count of cards on the active page, causing the pager to shift horizontally whenever a page had fewer cards than the capacity (e.g. jumping left on a 1- or 2-colonist remainder page). Now, when paginated (`PageCount > 1`), `_cardsHost` locks to the exact width of 6 card slots (`6 * (CardWidth + CardGap) = 618 px`) with `Justify.FlexStart` and `flexShrink = 0`. As a result, the 6 card slots and the pagination toolbar remain in the exact same screen position regardless of how many cards are on that page.
- **Flush docking with little spacing**: `.roster-pager` margin reduced to 0.5px (combined with card margin-right of 3.5px, providing a clean 4.0px gap flush next to the 6th slot). `HudLayout.PagerGap = 4`.
- **Verified**: fast tier 723 Sim + 418 Hud; EditMode **1725 total, 1711 passed, 0 failed**; PlayMode **82 total, 77 passed, 0 failed**; both content checks current.

### Depth control default button and flush colonist info card (2026-09-18)

The owner: *"- The button that is the default button in the depth control should be twice as big as the other ones and be tinted to indicate the default view to the player. - The colonist info card needs to be flush against the bottom bar as there is a gap/spacing to allow for maximise space for seeing"*.

- **Depth control default button (2× height & earth tint)**:
  - The starting/ground layer represents the default slice view. Its button on the depth rail now stands twice as tall at 32 px (`HudLayout.RailSurfaceCellHeight = 32`, 2× the 16 px of standard cells), while maintaining standard 26 px cell width.
  - Wears a subtle earth-green tint (`rgba(127, 201, 140, 0.25)` derived from `HudTheme.Good`, with a matching `rgba(127, 201, 140, 0.65)` border) when inactive, clearly distinguishing ground level from pale sky and dark subterranean rock. When active, the active cyan (`#6fd3e3`) highlight cleanly takes precedence.
  - Squeezing geometry in `HudLayout.RailPitch` and `HudLayout.RailHeight` now distributes room across `layers + 1` effective cell units, ensuring that on short viewports or deep boards the taller surface button never encroaches on the orders strip or command bar.
  - `HudShell.FitRail()` scales the surface view cell to `2f * cell` and preserves label visibility.
- **Colonist info card & info panels flush against bottom bar**:
  - `HudLayout.InspectToBar` dropped from 14 px to 0 px, moving `InspectBottom` from 64 px to 50 px (`BarBottom + HudCommands.BarHeight + Frame = 50`).
  - Shifting the inspect pane down eliminates the floating 14 px gap above the docked command bar, recovering 14 px of visible game world above the card.
  - In `Hud.uss`, `.inspect` updated to `bottom: 50px;` and both bottom corners squared off (`border-bottom-right-radius: 0;`), creating a seamless docked transition against the command bar edge that matches popover styling.
  - Applies to both wide colonist inspection (560 px) and narrow tile/pile readout (280 px).
- **Guards & Verification**:
  - `HudStyleSheetTests` asserts `.rail__cell--surface` height equals `HudLayout.RailSurfaceCellHeight` (32 px) and `.inspect` bottom equals `HudLayout.InspectBottom` (50 px).
  - `HudLayoutTests.TheOrdersStripStandsInTheGutterUnderTheRail` validates depth rail gutter clearance across all resolutions.
  - `HudLayoutTests.TheInspectPaneNeverReachesTheCommandBar` validates inspect pane bottom clearance.
  - Fast tier: 723 Sim + 418 Hud passed; both content checks current.

### Typing took the keyboard off the game, names became the player's, and the starting kit stopped being a pantry (2026-09-18, branch `claude/start-inventory-and-naming`)

Three owner asks in one branch, and the first turned out to be the biggest.

- **The save dialog never had the keyboard, and nothing noticed because the scrim looked modal.**
  Owner: *"when you type in during a save game the in game controls still work and can cause
  confusion."* Every in-game key is **polled** from `Keyboard.current` in six components; a UI
  Toolkit field only ever sees events the panel routes to it. Two systems, one keyboard, neither
  aware of the other — so typing `sss` panned the camera, a `1` changed the game speed and a `c`
  armed the Cancel tool behind the modal. The pointer *was* handled, by the scrim, which is exactly
  why this survived: the modal behaved like a modal in the one dimension anybody looked at.
- **Six copies of one guard was the real defect.** Each poller opened with its own
  `if (hotkeys.Listening != null) return;`, and the day a *second* reason to sit a frame out arrived,
  five of them would have kept going. So the rule moved into `HotkeyDirector.GameKeysLive` and the
  pollers ask that. One rule, one owner — the pattern `docs/bug-patterns.md` keeps catching.
- **A token, not a flag, and the reason is orderings nobody controls.** Focus moves as a blur and a
  focus and nothing promises which arrives first; a bool would be cleared by the field being *left*
  after the field being *entered* had set it, leaving the gate open with a cursor on screen.
  `AFieldThatHasAlreadyLostTheKeyboardCannotGiveItBack` is that case written down. `StopTyping`
  covers the mirror failure — a gate stuck *shut* is a game that has quietly stopped answering its
  keys, and that is the worse half.
- **Escape had to go with the keyboard, and Escape-after-blur reverted nothing.** The first cut
  invoked the field's escape handler *after* `Blur()`, so the blur ended the edit and the handler
  was then handed a field nobody was editing. Caught by writing the revert before the test, not
  after. It now runs before the blur, and the comment says why.
- **Renaming a colonist is the first piece of identity that cannot be derived.** Name, face, age and
  trade all fall out of a roll seed the simulation already saves, which is how the game carries
  sixty-one people in four bytes a head. A typed name falls out of nothing, so it needed a book, a
  save section and an argument for why presentation state is in a save file at all — and that
  argument already existed, written out in full on `ViewStateSection` for the camera. `ISaveable`,
  pointedly not `IStateHashable`: two colonies that tick identically must compare equal whether or
  not somebody typed a name over one.
- **The trap the select screen walks straight into.** Slot 0's `PawnId` is the same `1` the *last*
  colony's first colonist had, so dealing candidates through `ColonistNames.Of` would hand a fresh
  stranger a name typed in a game that is already over. Hence `Rolled` beside `Of` —
  and it is the third time this project has met "a new colony's pawn ids start at the same small
  numbers", after the roster bar's portraits and the roster bar's names.
- **A rename belongs to the person, not the slot** (owner's call). Reroll forgets the name of
  whoever it rerolled; a locked card keeps both. That is `18-colonist-select.md` §2 decision 2
  applied to the typed name for the reason it was applied to the dealt one.
- **Sixteen characters, chosen for the roster strip rather than for names.** The densest region in
  the interface is where a name that elides costs the most, and telling colonists apart at a glance
  is the whole point of naming one.
- **The starting kit: 144 meals and eight scrap was the soak's pantry, not a game's opening.** The
  meal count was sized so the ten-day headless gate measures the simulation rather than a famine,
  and it then followed the player into a game it was never sized for; the scrap predates there being
  any other hauling work on the board; and a player wanting a wall had to fell a tree first. Now 36
  meals, no scrap, 150 each of stone and wood — `docs/design/22-starting-kit.md`.
- **No golden moved, by construction.** The new fields default to zero and only `Playtest()`
  overrides them, because `Bare()` is what the golden table and the soak build on. The test that
  said the two scenarios *"differ only in their orders"* is gone: its real job was stopping Bare
  drifting, and that is now done by pinning Bare's own numbers rather than by tying it to a preset
  that is meant to be tuned.
- **`ColonyItems.Spawn` does not clamp an empty cell to the stack limit** — it checks the limit only
  when the cell already holds something. Found while writing the kit, and clamped in the placement,
  because a single stack of 200 stone is one no hauler could carry and no stockpile could take
  apart.
- **Verified:** fast tier **724 Sim + 432 Hud**; EditMode **1740 total, 1726 passed, 0 failed**;
  PlayMode **81 total, 76 passed, 0 failed**; both content checks current.
- **The white selection cursor sitting flush on terrain, floors, water, and banks (2026-09-18).**
  The white cursor bracket used to select cells and inspect info in the world previously failed to sit flush on sloped terrain:
  part of the bracket stubs sank into the ground (obscured from view) while the opposite edges hovered high in the air.
  - **Root causes in `ChunkRenderer.DrawFloorBracket`:**
    1. Stubs were placed with `Quaternion.identity` (pure horizontal orientation) and only 5 mm initial clearance (`0.04m - 0.035m`), while ground mesh and floor slabs are rendered as sheared tangent planes via `GroundRelief.Drape(centre)`. On meadow slopes (~8°), an unrotated horizontal stub sinks up to ~7 cm into the rising ground.
    2. Stubs sampled `GroundRelief.Lift` independently at each of the four cell corners. Because the ground relief is curved (sinusoids), four corner elevations disagree with the planar sheared mesh of the cell.
    3. `DrawFloorBracket` ignored water surface elevation (`WaterLine.SurfaceAbove`) and bank ramps (`BankLayout.At`), drawing the bracket at the submerged cell floor or buried inside bank ramps.
  - **The geometric solution (`docs/design/23-flush-selection-cursor.md`):**
    - The eight stubs of a floor bracket (two per corner) are defined in cell-local coordinates and transformed by the surface's placement matrix:
      `placement = GroundRelief.Drape(surfaceCentre)`.
    - Because the stubs share the exact shear transformation `(m10 = slopeX, m12 = slopeZ)` as the terrain mesh, every point on the bottom face of every stub maintains an exact, uniform clearance of `FloorBracketBias = 0.008f` (8 mm) above the draped ground plane, completely eliminating ground clipping and z-fighting on any slope.
    - Surface elevation resolution:
      1. Water cells: `placement = GroundRelief.Drape(CellMetrics.FloorCentre(cell) + Vector3.up * WaterLine.SurfaceAbove(_model, cell))` rests the cursor directly on the water surface.
      2. Straight bank risers: `BankLayout.StraightBankShear()` shears the bracket stubs by `SizeY / SizeXZ = 1.2` along local Z, so stubs along Z tilt with the 1.2 ramp slope from lower terrace to upper terrace while X stubs remain horizontal across the ramp.
      3. Corner bank risers: 4-corner rise values calculated from `BankMesh.HeightAt` lift corner stubs to conform to the inner/outer bank facets.
      4. Solid blocks / edifices: `DrawCellHighlight` updated from `Lift` to `Drape`, ensuring upright cell selection boxes remain plumb and full height on slopes.
  - **Asserted rather than looked at:** Added `Assets/Odyssey/Presentation/Tests/SelectionCursorTests.cs` verifying the 8-stub topology, exact 8 mm clearance on flat ground and ~8° slopes, straight bank shear slopes, corner rises, and water placement elevation.
  - **Verified:** fast tier **724 Sim + 438 Hud**; EditMode **1752 total, 1738 passed, 0 failed**; PlayMode **82 total, 77 passed, 0 failed**; both content checks current.

- **Colonist head turning and procedural gaze (2026-09-18, on `claude/colonist-head-turn`).**
  Colonists previously maintained rigid, forward-locked heads throughout all activities — chopping trees, mining rock, hauling, climbing ladders, and wandering across the meadow.
  - **Architecture & Seams:**
    - Presentation-only procedural kinematics (`Assets/Odyssey/Presentation/World/HeadLookKinematics.cs` and `PawnFigureDirector.cs`), completely decoupled from simulation and determinism (`Odyssey.Sim` untouched, zero hash impact).
    - Transforms applied directly to `Neck` and `Head` bones immediately following `Graph.Evaluate()` in `PawnFigureDirector.ApplyGazePose(deltaTime)`. Runs cleanly on top of `PlayableGraph` without requiring `OnAnimatorIK`.
    - World-axis rotation application (`rot * bone.rotation` around `chest.up` and `chest.right`): bone rotations are applied around chest reference frame axes rather than arbitrary local bone orientations, ensuring cross-rig consistency across all Synty models.
    - Reference frame decoupling from Chest/Spine: yaw and pitch are calculated relative to the colonist's upper torso orientation, ensuring that terrain slopes, locomotion leans, and crouch animations do not produce unnatural axial roll.
  - **Anatomical 30/70 Partition & Limits:**
    - Distributed 30% to `Neck` and 70% to `Head` (research `e-06-head-look-kinematics.md`). Low-poly character meshes have minimal neck topology; applying 100% to head causes severe mesh twisting/pinching, while rotating neck alone produces a stiff robotic column. 30/70 gives natural cervical curvature without polygon collapse.
    - Clamped to natural anatomical limits: yaw $\pm 60^\circ$, pitch down $-40^\circ$ (depression), pitch up $+35^\circ$ (elevation).
    - Cubic smoothstep rear hemisphere attenuation: targets beyond $60^\circ$ yaw attenuate to 0 weight by $95^\circ$, preventing backward neck-snapping when a target moves behind the character.
    - Smooth damping via `Mathf.SmoothDampAngle` ($300^\circ$/s max speed, 0.12s smooth time) produces organic saccadic head movement.
  - **6-Tier Pre-emptive Gaze Arbiter:**
    1. `SleepLock` (Tier 6): When asleep or in a bed, gaze weight immediately drops to 0, leaving the sleeper pose natural and resting.
    2. `LadderTraversal` (Tier 5): Climbing pawns look up ($+30^\circ$ pitch) when ascending or down ($-35^\circ$ pitch) when descending.
    3. `WorkFocus` (Tier 4): Working pawns dynamically track `WorkCentre` in world space, actively looking at the tree trunk while chopping, rock face while mining, or ground crop while sowing/harvesting.
    4. `SocialPassing` (Tier 3): Detects oncoming colonists within 6 m (~2.5 cells) and turns gaze toward the passing colonist's head for 1.3 s with a 20 s cooldown.
    5. `AmbientWander` (Tier 2): Idle and walking pawns occasionally look around ($\pm 15^\circ$ to $\pm 35^\circ$ yaw, $\pm 8^\circ$ pitch) for 1.2–1.8 s.
    6. `PathForward` (Tier 1): Default forward-facing gaze (3.0–6.0 s dwell), with job-specific posture pitch offsets (hauling/delivering carries $-12^\circ$ downward tilt; eating carries $-25^\circ$ downward gaze).
  - **Zero Allocations & Deterministic Cadence:**
    - `LookGazeState` is a pure struct stored inline in `PawnFigureDirector.Figure` (0 B GC.Alloc).
    - Cadence and ambient glance angles are derived from lightweight pseudo-random hashing seeded by pawn ID and state timers, completely independent of `UnityEngine.Random`.
  - **Tests & Verification:**
    - Unit test suite in `Assets/Odyssey/Presentation/Tests/HeadLookKinematicsTests.cs` (15 tests covering look-at trigonometry, reference frames, rear attenuation, limits, 30/70 partition, head-only fallback, ladder angles, hauling/eating posture offsets, and smooth neutral return).
    - Fast tier: **724 Sim + 438 Hud passed**.
    - EditMode: **1767 total, 1753 passed, 0 failed**.
    - PlayMode: **82 total, 77 passed, 0 failed**.
    - Content gates: `build_wiki.py --check` and `emit_labels.py --check` both clean.

- **Head turning refinement: pure cervical axial swivel and felling target elevation (2026-09-18, on `claude/colonist-head-turn`).**
  Owner PlayMode testing reported two distinct visual issues: (1) heads tilting in strange ways instead of cleanly swivelling left/right sat on the neck, and (2) colonists chopping trees looking down at the ground rather than at the tree trunk.
  - **Root Causes:**
    1. Using `refFrame = Chest` coupled with a 30% rotation on `Neck` rotated the head around an oblique axis whenever the chest leaned during walking or swung during an axe stroke, forcing the skull into an eccentric cone and inducing severe ear-to-shoulder roll (lateral flexion).
    2. Odyssey cell origins are at floor level ($Y = 0$). Colonists have eye height at $Y \approx 1.4$ m. Directing `WorkFocus` at `figure.WorkCentre` aimed at the dirt roots 2.5 m away ($\approx -35^\circ$ pitch), pulling the head down to its maximum chin-chest depression limit.
  - **Refined Kinematics & Fixes (`docs/research/e-07-head-axial-rotation-and-felling-gaze.md`):**
    1. **Pure Head-Only Rotation Sat on Neck (0% Neck, 100% Head):** The `Neck` bone remains static as a stable mounting post. Rotation is applied solely to the `Head` bone around the neck's longitudinal cervical axis (`neck.up` in world space) for yaw and transverse condyle axis (`neck.right`) for pitch. This produces clean left/right looking around sat on the neck with mathematically $0.0^\circ$ ear-to-shoulder roll.
    2. **Felling & Mining Work Target Elevation (+1.30m):** `TargetWorldPosition` during `WorkFocus` is elevated by $+1.30$ m above cell base floor level (`figure.WorkCentre + Vector3.up * 1.30f`). This brings the gaze to near-level ($\approx -2.3^\circ$), keeping the colonist focused directly on the tree trunk notch and foliage.
  - **Tests & Verification:**
    - Updated `Assets/Odyssey/Presentation/Tests/HeadLookKinematicsTests.cs`: verified Neck remains untouched (`Quaternion.identity`), Head receives 100% of yaw and pitch with exact $0.0^\circ$ roll, and felling target elevation yields $-2.3^\circ \pm 1.0^\circ$ pitch.
    - Fast tier: **724 Sim + 438 Hud passed**.
    - EditMode: **1767 total, 1753 passed, 0 failed**.
    - PlayMode: **82 total, 77 passed, 0 failed**.
    - Content gates: `build_wiki.py --check` and `emit_labels.py --check` both clean.

- **Head turning: eliminating ear-to-shoulder roll with orthonormal LookRotation (2026-09-18, on `claude/colonist-head-turn`).**
  Owner testing identified that while walking around and chopping, colonist heads were rolling (lateral tilt) rather than cleanly yawing left/right and pitching up/down: *"the head is rolling, - it needs to yaw to look left and right and pitch up and down slightly. When walking around"*.
  - **Empirical Diagnosis & Root Causes:**
    1. **Additive Multiplication on Swaying/Pitched Bones:** `ApplyAdditiveRotation` previously used `head.rotation = headRot * head.rotation`. In locomotion clips (walk/run), the pelvis and spine sway laterally with each step. In work poses (felling/mining), the spine is pitched and twisted by the diagonal axe swing (`SwingAxis` has 30° tilt). Multiplying an incremental rotation onto an already-swayed/twisted bone compounded with the torso tilt, converting horizontal yaw into diagonal ear-to-shoulder roll.
    2. **Un-yawed Pitch Axis Cross-Product:** Applying yaw and pitch via separate Euler-style factors (`yawRot * pitchRot`) without turning the pitch axis with the yaw produced cross-axis roll proportional to $\sin(\text{yaw}) \times \sin(\text{pitch})$ whenever both angles were active.
    3. **Double Application in Test/Editor Loops:** `PawnFigureDirector` runs `ApplyGazePose` in both `Sync` and `Evaluate`. Because the Synty locomotion clips do not key the `Head` bone, `Graph.Evaluate` never reset the head rotation between the two passes, causing additive angles to double within a single frame in editor harnesses.
  - **The Orthonormal Solution (`HeadLookKinematics.ApplyAdditiveRotation`):**
    - Head orientation is computed from the smoothed gaze angles as an **absolute orientation** rather than an incremental delta:
      1. Construct the gaze direction in body space: `localDir = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward`.
      2. Transform to world space: `worldDir = referenceFrame.TransformDirection(localDir)`.
      3. Align the head via `Quaternion.LookRotation(worldDir, yawAxis)`.
    - By construction, `Quaternion.LookRotation(worldDir, yawAxis)` forces the head's local upright to align strictly with the body's upright (`figure.Transform.up`), guaranteeing **mathematically $0.0^\circ$ ear-to-shoulder roll** regardless of spine bending or torso tilt.
    - Because the target orientation is absolute, running across both `Sync` and `Evaluate` produces the identical rotation and **cannot accumulate or drift**.
    - During ambient wandering and walking, the head turns cleanly left and right in pure yaw, nodding slightly in pitch, with zero lateral tilt.
    - During chopping and mining, the head faces squarely at the tree trunk/rock face at chest level ($+1.30$ m) with level ears, confirmed visually in contact sheets `Logs/swing-impact.png` and `Logs/swing-4.png`.
  - **Verified:** fast tier **724 Sim + 438 Hud passed**; EditMode **1767 total, 1753 passed, 0 failed**; PlayMode **82 total, 77 passed, 0 failed**; both content gates clean.



### Nothing grows at the foot of a terrace step (2026-09-18, branch `claude/terrace-foot-guard`)

Owner: *"The flat side of the terrain where the height changes, we created a façade of terrain but
the problem is that things generate in those tiles … Trees shouldn't be generated in those spots
because they get clipped by this façaded terrain."* The design is
`docs/design/22-terrace-steps.md`; what is worth keeping is why it could not be one line.

- **The bank is a façade, and that was the point until something stood in one.** A bank fills the
  empty cell at the foot of a terrace step from the floor to the rim above, and nothing in
  `Odyssey.Sim` knows it exists — not pathable, not saved, not hashed, like ground relief and grass
  tufts. That was harmless while the only question was what to draw. It stopped being harmless when
  the generator put a tree in the cell: the wedge of hillside shears the trunk off. A *walking*
  colonist is fine, because `PawnPose` lifts a figure onto the bank's surface — which is the reason
  the fault reads as an art bug rather than as a placement one.
- **The rule had to be stated twice, so it is checked rather than trusted.** `BankLayout` reads the
  render mirror; worldgen has a `CellGrid` and no mirror. One function could not serve both, so
  there is a second owner — `TerraceFoot.IsFoot` — and `TerraceFootTests` walks **every cell** of
  seven boards requiring the two answers to be identical: a one-layer step, flat ground, a two-layer
  riser, a rock face, a plateau corner, a notch, a quarry, ground under a roof. Four of those seven
  are boards where the interesting answer is *no bank*, which is where two copies of a rule usually
  drift apart.
- **The diagonal is the clause a hand-written guard would have missed.** A bank stands against an
  orthogonal step, or — where there is none — against a diagonal one: the outside-corner piece that
  wraps a convex corner, added when a run of banks was found to have a square bite out of it at
  every corner. So the guard reads eight neighbours, not four.
- **"Which terrains are earth" became one list on the way past.** It was `GroundLook.IsEarth`, a
  drawing judgement, and the step test needs the same judgement — earth spills down a step, stone is
  sheer. It is now `NaturalContent.IsEarth` with `GroundLook` calling it. A second copy would have
  been wrong the first time a soil was added and the symptom would have been a tree in a bank.
- **Measured, not estimated: 122 of some 1,600 would-be trees on the played board**, about one in
  thirteen, all along terrace edges (seeds 1, 7 and 42 give 122, 118, 122). The density roll is
  still drawn per column whatever the terrain, so the guard thins the wood along steps without
  reshuffling it anywhere else.
- **Three pinned numbers moved and the shape of the move is the evidence.** The wooded golden's two
  values and the six `dry` hashes in `WaterTests` moved, because a tree is an edifice in the grid.
  The barren meadow's golden, the ruined city's golden and all six `barren` hashes are
  byte-for-byte what they were — measured by running the tables before re-baking and reading which
  assertions failed. A guard on `TreePass` can reach no board that has no `TreePass`, and anything
  else moving would have meant something had come along uninvited.
- **What was deliberately not done.** The owner also reported a colonist sleeping in one and
  disappearing: *"not sure what to do to prevent sleeping in that spot"*. Two candidates are written
  down in §4 of the design — refuse the lie-down spot, or refuse a bed there — and both change the
  state hash, so they want a decision rather than a guess. The predicate is in place for whichever
  is chosen. Walking is untouched on purpose: the cell is the take-off cell for the hop, and the
  bank is drawn there to make that hop legible.
- **Verified:** fast tier **726 Sim + 438 Hud**, Long **21**; EditMode **1756 total, 1742 passed,
  0 failed**; PlayMode **82 total, 77 passed, 0 failed**; both content checks current.

### A colonist climbed a terrace faster than one walking beside it (2026-09-18, branch `claude/terrace-foot-guard`)

Owner, after the first look in play: *"Would it be possible to make the terrace step, if going down
the terrace step, you go a bit faster and if you going up, you go a bit slower … ensure the
animation/motion adjusts accordingly."* Then, having watched one: *"The colonists looked too fast
going up definitely … should be much slower."* The design is `docs/design/22-terrace-steps.md` §4b.

- **The asymmetry already existed, and was not the point.** Up was 135, down 50, flat 100 — down
  already 2.7 times quicker than up. The first answer to the question was therefore "it already does
  this", with the numbers. What the question found was a different fault underneath it.
- **The fault was in metres per second, not in the ratio.** A hop is *drawn* along the slope between
  two cell centres: 2.5 m across and 3.0 m up is **3.91 m**. At 135 — 2.25 s — that is **1.74 m/s**,
  against **1.50 m/s** for walking a flat cell. Climbing a terrace was literally quicker than
  strolling beside it. Nobody had measured the drawn path; the cost had only ever been compared with
  the cost of a flat cell, where 135 against 100 looks like effort.
- **So the new price is derived, and it is bounded on both sides.** Floor **156**, where a climb
  stops being drawn faster than a walk; ceiling **290**, `StairUp`, past which a colonist walks to a
  stair rather than hopping one block and a terraced board stops being crossable. **240** — 4.0 s,
  0.98 m/s along the slope — sits between them, and is still 11% quicker than the 270 that read as
  *stuck* the last time this constant was retuned. Both bounds are now assertions, so the fault
  cannot come back as a tuning.
- **The motion had to land with the price, or 240 would have been 270 again.** 270 failed because a
  slow slide up a bank is a colonist stuck on a hill. A hop is now drawn as a hop: `HopArc` gathers
  for 0.48 s, leaves the ground on a **solved parabola** that passes exactly 0.35 m over the lip and
  comes down onto it as the step ends.
- **The arc takes the real rise, and the first cut did not.** A colonist standing in the cell at the
  foot of a terrace is already half way up the bank, so the climb is about 1.5 m and not the 3.0 m
  of a layer. The first version added a fixed arch to a fixed climb and cleared the lip by **15 cm
  while claiming 35** — the two curves were fighting each other. Caught by the test that asserts the
  clearance on the board rather than on the curve, which is why that test was written that way.
- **A drop is a square, and the number fell out of what was already written down.** `MoveCost.Drop`
  is 0.83 s; a 3.0 m free fall takes 0.78 s; the difference is the step off the edge. The implied
  acceleration is 9.78 m/s², and the test pins it to gravity — so retuning the drop fails a test
  instead of quietly making colonists fall at the wrong speed.
- **One clamp replaced a hand-faded lift.** The descent used to fade its bank rise out over the step
  because taking the ground's maximum would hold the figure to the edge and drop it 1.5 m in a
  frame. With a ballistic curve the maximum is right for both halves: the body stays on the slope
  until the slope falls away faster than it does. Fewer rules, and the one that is left is physical.
- **The gait is held through a hop, because it is solved from horizontal speed and a hop is not
  ground locomotion.** Measured: a drop crosses a cell at 3.0 m/s, past the fastest gait this cast
  owns (2.60 m/s), so stepping off a terrace pinned the run cycle and rate-stretched it for eight
  tenths of a second. A climb at 240 is the opposite — 0.63 m/s across the cell, a third of the idle
  blended in, a dawdle. Holding the stride covers both; the price is a few frames of sliding during
  the gather, which is why the gather is short.
- **A stair would have been drawn vaulting up its own stairwell.** `NavGraph.IsHop` is pure geometry
  — one layer, one cell across — which is exactly the shape of a stair step. The simulation
  separates them with `UpperEndIsABlockTop`: you hop onto ground and take a stair to a storey. That
  second clause is in `PawnPose.IsDrawnAsAHop` with a test, written now, before `U44` lands, because
  nothing would have failed when it did.
- **The re-bake has the sharpest control this table has had.** Both boards with steps on them moved
  their `Simulated` value and nothing else moved at all — no `Generated` value, because a price is
  not content and nothing is placed differently, and **not one number on the barren meadow**, which
  is a flat table with no step to hop.
- **Verified:** fast tier **726 Sim + 438 Hud**, Long **21**; EditMode **1771 total, 1757 passed,
  0 failed**; PlayMode **82 total, 77 passed, 0 failed**, so the frame budget is unmoved by the arc.

### The climb stopped being a jump and became four strides (2026-09-18, branch `claude/terrace-foot-guard`)

Owner, on the arc that had just landed: *"when going up hill - it looks like they jump a bit and not
flat with the terrain - which they should be. The motion animation, doesn't quite match, would it be
possible they take actual steps up the terrain in a few motions."*

- **The arc was answering the simulation's word rather than the board's picture.** The simulation
  calls this step a *hop* — `MoveCost.JumpUp`, "a colonist can jump if they need to get up a +1
  height block" — so the first cut drew a jump: a solved parabola over the lip. But the board has a
  **bank** under that step, and `BankMesh.HeightAt` is a plane from the lower floor to the upper
  rim: a walkable ramp the whole way. A body arcing over a surface it could be walking on is a body
  ignoring the ground it is on, and that is exactly what the owner saw. The lesson is small and
  general: *the drawn motion has to answer to what is drawn, not to what the mechanic is called.*
- **So the climb became a function of height rather than of time.** `HopArc.Stepped` takes the
  drawn ground under the walker, the height being climbed on to and the whole rise, and hands back
  the tread the figure has its weight on. The shape of the bank therefore decides where the strides
  fall: flat ground gives no rise, steep ground gives them close together. Nothing about it is
  parameterised on the duration, so retuning the price cannot change the stepping.
- **The stride count comes out of the height.** `PreferredTread` is 0.4 m, so a terrace's 1.5 m is
  four strides of 0.375 m and a layer-high climb is eight. Fixing the *count* instead would draw a
  small step and a tall one in the same number of motions, which is the thing that would read as
  wrong at whichever end was not tuned for.
- **Stepping means leading the slope, and there is no way round it.** The figure is drawn at the
  tread it has stepped on to while the ramp beneath catches up — up to two thirds of a tread ahead,
  because your hips go up when your foot does. Quantising the other way makes the body sink into the
  hillside and the clamp then erases the whole effect. The honest alternative is foot IK, which is a
  different piece of work; the lever meanwhile is `PreferredTread`, where smaller reads as gliding
  and larger as floating.
- **A sheer face nearly shipped as a three-metre teleport.** A bank is refused against rock, inside a
  working and under a roof. There the ground under the walker is flat for the first half of the step
  and jumps a whole layer at the midpoint, because that is what `over` does — so a purely
  ground-driven climb would have drawn a colonist standing still and then teleporting. Caught by
  reasoning about the fixture rather than by a test, and then given both: the straight chord sits
  under the strides as a floor, and `ASheerFaceIsClimbedSmoothlyRatherThanInStrides` measures the
  largest single frame of such a climb.
- **What is still owed is the cadence.** The gait is held through a hop, so the legs keep the rhythm
  they arrived with while the body pushes up each tread. If that reads as sliding, the answer is a
  computed climb pose in the manner of `WorkSwing` — no pack we own has the clip — rather than
  solving the gait from a speed that swings between a push and a plant.
- **Not verified in Unity at the time of writing.** The owner's editor is open on this worktree
  (`odyssey-inspect`, the Play scene), which locks the project against a batch run, and
  `docs/lessons.md` is explicit that one must not be killed. The fast tier does not compile
  presentation, so this revision has its arithmetic reviewed and not run. Verified on a scratch
  worktree instead — see the commit that follows.

### The stride that could not be drawn, and the two teleports it found (2026-09-18, branch `claude/terrace-foot-guard`)

The stepping climb of the previous entry did not survive its first honest measurement, and what it
turned up had been in the game far longer than it had.

- **A percent was too coarse to draw a stride with.** `PawnView.MovePercent` is a whole percent of
  the step, and the sub-tick term cannot rescue it: at 60 frames and 60 ticks a second there is about
  one frame to a tick and the leftover is nearly nought, so the figure advances by whatever the
  published number advanced. On a flat cell that is exactly one point a tick, because a flat cell
  costs 100 — which is why nobody has ever seen it. On a 240-tick hop it is a whole point every 2.4
  ticks: two frames still, then a jump. Measured at **25 mm on the flat and 134 mm up a terrace**
  once the climb was drawn in strides, against the 50 mm `BankFootingTests` allows a frame.
- **The probe that said so was wrong twice before it was right**, which is the lesson worth keeping.
  The first version sampled only at `tickAlpha = 0`, so both arms of the comparison collapsed to the
  same number and appeared to exonerate the sub-tick term; the second measured per percent, which is
  a granularity the game no longer has. `PawnView.MovePerMille` is the fix — ten times the
  resolution, published beside the percent rather than instead of it — and `BankFootingTests.WorstJump`
  now samples per mille, because an instrument coarser than the thing it measures reports
  quantisation as a teleport.
- **Then the sheer face, which had been snapping 1.51 m since hops were first drawn.** With no bank
  there is no ramp, so the ground under the walker is flat for half the step and jumps a whole layer
  at the midpoint, when the cell it is over changes. Every climb curve timed across the whole step
  therefore reached half its height and was then clamped the rest of the way in one frame. **No test
  saw it because every fixture had a bank in it** — the same shape as the grass-tuft and bank faults
  before it: the fixture chose the case. A sheer climb now hauls up over the first half and walks
  forward over the second, peaking at 38 mm a frame.
- **And its mirror, 657 mm, on a sheer drop.** The clamp holds the figure on the upper floor until
  the boundary — rightly, it is standing on the ledge — so a fall timed across the whole step was
  66 cm below the ledge by the time the clamp let go. Timed into the second half instead, the release
  is continuous and what is left is 250 mm a frame: three metres inside the 25 ticks that half of
  `MoveCost.Drop` buys. That one is geometry, not curve, and it is pinned rather than papered over.
- **The stride is as concentrated as the frame budget allows.** 50 mm a frame is twice what an honest
  frame of walking moves, and a terrace's 1.5 m of rise inside half a 240-tick step is 25 mm a frame
  spread evenly. Concentrating it into a fraction *P* of each stride multiplies that by 1.5/*P*, so a
  third failed at 57 mm and a half passes at 38. Making the push snappier means slowing the climb,
  not steepening the curve.
- **Every one of these was found by measuring rather than by reading.** Three of the four were the
  opposite of what the code suggested, which is the fourth time this project has recorded that
  sentence.
- **Verified:** fast tier **726 Sim + 438 Hud**, Long **21**; EditMode **1776 total, 1762 passed,
  0 failed**; PlayMode **82 total, 75 passed, 0 failed**. Both Unity tiers ran on a scratch worktree
  because the owner's editor held `odyssey-inspect`, and that is why PlayMode passed 75 where the
  same suite passes 77 on the real checkout: a scratch worktree has no `Assets/Synty` junction, so
  the two portrait tests skip for want of the packs. Nothing this branch touches goes near them.

### The ramp is one slope and the game charged it as two steps (2026-09-19, branch `claude/terrace-foot-guard`)

Owner, on the third look: *"The slowness needs to start happening much earlier when entering the
beginning of the tile while going up and then reaching the top back to normal — it seems to be doing
it 75% up — you slow down and then you seem to still go slow on the flat so it's out of sync."* The
design is `docs/design/22-terrace-steps.md` §4c.

- **A seam, not a curve.** The bank spans one cell; a step spans two half-cells. So the drawn ramp is
  split down the middle of the foot cell between two steps priced for different things: the walk
  *into* the cell at flat-grass price, drawn at **1.9 m/s**, and the hop *out* of it at 240, drawn at
  0.62 — which also kept paying that price across the flat top. Both of the owner's complaints are
  that one seam, from either side of it.
- **The same fault as the first report, one step earlier.** A step priced for flat ground was being
  drawn along 3.2 m of path. That is exactly what `MoveCost.JumpUp` was re-derived for two days ago;
  nobody had asked the question of the step *before* the hop.
- **So the simulation learned that a slope is a slope.** A terrace foot carries a cost class of its
  own, worth `JumpUp − Orthogonal`, and that subtraction lives in `NavGrid.cs` beside the hop price
  because the two must be equal: if they differ, a colonist changes speed half way up a slope that
  does not change. `HopPriceHasOneOwnerTests` refused the first attempt, which named `MoveCost.JumpUp`
  from the content table — rightly, and the fix was to put the arithmetic where the guard allows it
  rather than to exempt the line.
- **A cell carries one cost, so walking *along* a terrace foot is slow too.** That was the owner's
  choice between three options, and it is the honest one: the figure is drawn part way up a tilted
  surface whichever way it crosses. Colonists now prefer the flat line one cell out.
- **Presentation spends each step's time where the climbing is.** `StepPace` models a step as three
  heights and weights its two halves by what they cost to cross, a metre of rise counting 2.3 metres
  of ground — derived from the prices rather than chosen, so it cannot drift from them. On flat
  ground the two halves weigh the same and the pacing is the identity, which is what keeps an
  ordinary walk untouched.
- **The goldens did not move, and that needed explaining rather than accepting.** A cost change that
  shifts no hash is either inert or lucky. It is lucky: the three golden windows are a flat meadow, a
  start clearing chosen for being flat, and a city of pavement — not a bank between them. So
  `TerraceSlopeCostTests` asserts the price directly, including that mining the step away takes the
  slope with it, which is the case `NavGraph.MarkDirty` had to grow a neighbour scan for: a cost that
  reads the cells *beside* a cell is the first one this grid has had.
- **Four regressions, and three of them were instruments.** The one real bug was walking *off* a
  bank: `HopArc.Stepped` returned `max(ground, landing)` where there was nothing to climb, which
  pinned the figure at the top of the ramp for the whole step and dropped it 1.5 m in the last frame
  — caught by a test that has been measuring that crossing since long before any of this. The other
  three were tests sampling the ground at the clock's position rather than the figure's, which are
  the same thing only while time is distance. They are not any more, and that is the change working.
- **Verified:** fast tier **730 Sim + 438 Hud**, Long **21**; EditMode **1781 total, 1767 passed,
  0 failed**; PlayMode **82 total, 75 passed, 0 failed** (the scratch worktree has no Synty
  junction, so the two portrait tests skip there — see the previous entry).

### Two inventions removed, and the hitch that was there all along (2026-09-19, branch `claude/terrace-foot-guard`)

Owner: *"It still doesn't look quite right — can we keep it simple and it's a consistently slow speed
from top to bottom and motions exactly just above the terrace surface as it jolts and jitters the
colonists at certain points and smoother is preferred and predictable."*

- **Both of the things that jolted were mine.** A hop up a terrace has been drawn three ways in two
  days: a solved parabola over the lip (reported as jumping), strides up the treads (reported as
  jolting), and now the ramp itself. The board had answered the question before either was written —
  `BankMesh.HeightAt` is a plane, so there is a surface the whole way and the right height for a
  climbing figure is that surface, sampled where it stands. **The lesson is the general one: when a
  drawn thing already exists, read it; do not model it.** A model of a surface can disagree with the
  surface, and the three-height model does exactly that at a corner, where the bank is two planes.
- **The strides could not have been smooth, and the arithmetic says so.** A stride is a hold and a
  push: it concentrates a climb's motion into part of its time, by construction. The only question
  was how much, and the answer — capped by the 50 mm a frame may move — was a rhythm either way.
  Something asked for as *a few motions* and something asked for as *smooth and predictable* are the
  same request read two ways, and the second reading is the one that survives contact.
- **The smoothness test then found a hitch that predates every bit of this.** `MovePercent` is a
  whole percent, so a 240-tick step spends its first 2.4 ticks at nought percent — and every reader
  gated on `MovePercent > 0` drew the figure standing still through them and caught it up in one
  frame. Measured at **30 mm against the 10 mm a frame that climb moves**. It has been in the game
  since any step cost more than 100 and nobody has ever reported it, because on a flat cell it does
  not happen at all. `PawnView.Moving` is the fix, and the pose, the climb phase and the gait hold
  all read it.
- **A test that measures variation rather than a maximum is what caught both.** A hold-and-push
  rhythm passes "no frame moves more than 50 mm" comfortably. `AClimbIsSmoothFrameToFrameAllTheWayUp`
  requires the largest frame on the ramp to be under 1.5× the smallest, which is a statement about
  *evenness*, and it is the assertion the owner's word "predictable" translates into.
- **What is left, measured:** 9.9 to 12.3 mm a frame all the way up the ramp — the spread is per-mille
  rounding and nothing else — and one 30 mm frame where the ramp's 0.62 m/s meets the flat top's
  1.5 m/s. That junction is the terrain changing and is left alone.
- **Verified:** fast tier **730 Sim + 438 Hud**, Long **21**; EditMode **1777 total, 1763 passed,
  0 failed**; PlayMode **82 total, 75 passed, 0 failed** (scratch worktree, so the two portrait
  tests skip for want of the Synty junction).
- **Re-verified on the merge with main** (head turning, the flush selection cursor), this time on
  the real checkout with the packs: fast tier **730 + 438**, Long **21**, EditMode **1798 total,
  1784 passed, 0 failed**, PlayMode **82 total, 77 passed, 0 failed**, both content gates clean.

## 2026-09-19 — The load that was never there

The report was that a colonist picks something up and it vanishes. The obvious reading is that
nothing about carrying is built, and that reading is wrong in an expensive way: **two of the
owner's three beats already existed.** `JobDriver.LiftToil` is a whole timed toil of 48 ticks, the
grasp lands at tick 24 in the middle of the drawn crouch's floor hold, and `PawnGesture.Stow` is
already reported when a load goes into a stockpile *or* into a build site. A session that had
believed the symptom would have rewritten the stoop.

What was actually missing was one thing: `ColonyItems.PickUp` sets `item.Cell = -1` and delists
it, so the load leaves the view feed and nothing downstream can draw it.

- **The interview's water question rested on a false premise, in both directions.** It asked what
  happens when a colonist *swims* with a load. Deep water is impassable — the pathfinder routes
  round a lake rather than pricing a swim nobody survives — so no hauler ever swims one. But
  shallow water takes `WaterLine.Weight` to 1 on the owner's own 2026-09-17 decision, and
  `SwimPose` strokes **both arms**. The case is therefore the common one, not a hypothetical, and
  the honest answer to "no special case" is a bundle swinging about in a swimmer's arms. Put back
  to the owner with the correction, the answer was to hide the load in water for now and decide
  later. `CarryPose.Drawn` is the single place that does it.
- **The draft designed the pose the wrong way round and the code does not.** It proposed computing
  a cradle point from hip height and solving both hands to it. Arm length varies across the 61
  rigs by more than the cradle does, so a solved point puts a short-armed colonist at full stretch
  and a long-armed one folded against its chest — two people carrying the same log in visibly
  different postures. Authoring the shoulder and elbow angles and then *measuring* where the palms
  ended up gives every rig the same posture, which is the thing a viewer reads. Only `Clearance`
  is solved, along the one axis where an authored angle fails outright rather than merely looks
  wrong: a load inside the colonist's own chest.
- **The stance waits for the gesture; the load does not.** A lift hands the thing over half way
  through the crouch, so for its second half the pawn is carrying while the gesture owns both
  arms. Letting the carry weight ease in there takes it to full strength unseen, and the frame the
  crouch lets go the arms snap into the cradle. Holding the target at nought until the gesture
  ends makes the fold start from where the rise left the hands. The load is unaffected either way,
  because it follows the palms and not the stance — which is also what makes it travel *up out of*
  the crouch instead of appearing at the waist.
- **Drawn off the aspect rows, not off the pawns, and that is the performance decision.** Carrying
  is sparse. Walking the pawns would mean an aspect scan per pawn — fifty scans of a thousand rows
  every frame to find three loads. One scan of the rows, then a lookup only carriers pay for.
- **It costs no draw call.** The load joins the same instanced batch as the pile it came off, so a
  carried rock is one more matrix in a buffer that was going to be submitted anyway — and it is
  the same mesh at the same scale as the ground prop by construction, rather than by a constant
  that could drift.
- **A partial delivery reports the stow twice on one tick** and it is harmless. The snapshot
  publishes once a tick, so presentation never sees the intermediate serial. Worth writing down
  because it looks like a double motion and is not, and because it is a property of the publish
  cadence rather than of the drivers.
- **`DropCarried`'s deliberate silence was reversed rather than worked around.** Its comment argued
  at length that an abandoning drop is a different motion from a stow, which was right while
  nothing was drawn and is wrong once the load is visible — the alternative to a motion is a
  commodity teleporting out of somebody's arms. The distinction is recorded as deferred, with the
  note that the second gesture belongs *there* and not in a second drop path, because two owners
  for one rule is this project's commonest fault.
- **Verified:** fast tier **735 Sim + 445 Hud**; EditMode **1824 total, 1810 passed, 0 failed**;
  PlayMode **82 total, 77 passed, 0 failed**; both content gates clean with no CSV change, since
  this adds no named thing. **No golden moved** — gestures and aspects are neither saved nor
  hashed, and the goldens ran green unchanged, which is the measurement rather than the assumption.

## 2026-09-19 — Three faults from the first look at a carried load

The owner played it and reported three things. All three were real, and two of them the design
document had explicitly claimed would not happen.

- **"Much stiffer and static, because it's taken weight."** Every pose in the director is additive
  over the walk clip, which is right for a gesture laid over a gait and wrong for a stance: added
  to a swinging arm, a scoop is a scoop that swings, and the load follows the palms so the load
  swung too. The owner read it as a fault in the load. The fix is to take the four arm bones off
  the clip first, back to a rest read off each rig at bind time — a constant would be wrong on
  sixty of the sixty-one. Written rather than blended, so the second of the two passes a frame is
  the identity.
- **"The logs don't turn with you."** One line. The yaw came from `ChunkRenderer.FacingOf`, which
  reads like "which way is this colonist facing" and is really "the last heading this loop drew a
  *stand-in* at" — and that loop skips every pawn with a live figure. So no real colonist ever had
  an entry, every load drew at a yaw of exactly nought, and nothing looked broken until something
  asymmetric was held in it. **The same fault existed one layer down**: `ItemHeap` lays its
  sunflower on the world axes, so the armful had to be turned about the cradle as a cluster rather
  than each rock about itself.
- **"It should fall into position, and be raised out of pick up."** The draft asserted the pickup
  was continuous for free, because the hands are at the floor on the grasp tick. True vertically;
  it misses that the pile is at the middle of the cell and the palms are a third of a metre in
  front of the colonist. The drop was worse and the draft did not consider it at all — `PutDown`
  moves the item to its cell in the same instant it begins the stow, so the thing appeared at the
  cell centre and the crouch then played over empty hands.
- **`CarryHandover` draws both, and they are deliberately different curves.** The raise eases out
  (quick off the floor, slowing into the cradle) and the fall eases in (slow out of the hands,
  quickest at the floor), which is `Gesture`'s own asymmetry argument applied to a thing that
  really is falling. Reverse them and a colonist places something delicately and then snatches it
  off the ground. The raise is capped at 0.30 s against the 0.4 s of crouch left after the grasp,
  or the load is still travelling once she has set off walking — `LiftTicks`'s original fault in a
  new costume.
- **The fall needed a third aspect.** A load set down stops being a load and becomes an item in a
  cell, drawn from a different list by code that never saw the hands. The def and the stack cannot
  match it — a stockpile of wood is full of loads that agree on both — so the `ThingId` is
  published.
- **Verified so far:** fast tier **736 Sim + 445 Hud**, both content gates clean. **The Unity tier
  has not been run on these fixes**: an editor was open on the worktree (`odyssey-inspect`) when
  they were finished, and the rule is not to batch-run against a project somebody may be playing.
  Nearly all three fixes are in Presentation, which the fast tier does not compile, so that is a
  real gap and not a formality.

## 2026-09-19 — The carry re-measured, and what a new commodity inherits

The three fixes went in against an open editor and could not be run past Unity at the time. Run
now that it is closed: fast tier **736 Sim + 445 Hud**, EditMode **1834 total, 1820 passed, 0
failed**, PlayMode **82 total, 77 passed, 0 failed**, both content gates clean. Nothing was wrong
— but the gap was real rather than a formality, because almost all of that change lives in
Presentation and the fast tier does not compile it.

- **"Stones should get the same treatment and future big items."** They already do, and the useful
  thing was to write down *why* rather than to build anything: the carry path is keyed on nothing
  per-commodity. A def index reaches the renderer, a module is resolved for it, the yaw turns it
  and `CarryHandover` is keyed on the thing's id and has never heard of what kind of thing it is.
  The only opt-in is `ItemHeap.Recipes` — a row there makes a commodity an armful of several
  instead of one prop, and a null row is what everything did before heaps existed.
- **The missing guard was on `Armful`, not on the concept.** `ItemHeapTests` already held `Place`
  to the buffer across every recipe and said nothing about `Armful`, which writes a different
  count from a different recipe. `EveryHeapCommodityCanBeCarriedAsAnArmful` closes that, so a bad
  recipe on the next commodity fails the Unity tier instead of showing up as rocks a metre from
  somebody's hands.
- **What a genuinely big item would still want is its own hold.** A girder is not scooped in two
  arms at the waist. Nothing in the game is that size, so the decision belongs with whatever
  introduces one — and the seam for it is `CarryPose`, not a special case in the renderer.

## 2026-09-19 — The first player build was empty, and the editor could never have told us

The owner ran `Build/Win64/Odyssey.exe` from the merge and reported no terrain, no graphics,
nothing but characters. In the editor the same commit is perfect.

- **Every shader this game asks for by name was being stripped.** The world is drawn entirely
  through `Graphics.RenderMeshInstanced` with materials built at runtime from `Shader.Find`, and
  a runtime material is not an asset, so nothing in the build referenced the shaders. Grepping
  `Odyssey_Data` for their names returned nothing for all five of ours, and the player's own log
  said it out loud: *"shader Odyssey/Outline not found; outlines are off"*.
- **Characters were visible because they are the only thing that is not instanced.** They are
  GameObjects wearing Synty's own material assets — real assets, so their shaders survived. They
  also drew in the pack's colours rather than recoloured, because `Odyssey/Character` had gone
  with the rest. That single exception is what makes the symptom diagnosable.
- **The expensive option turned out to be free.** Always-including a shader forces all its
  variants in, and the received wisdom is that doing this to URP/Lit is ruinous. Measured: 385 MB
  and 11 s before, 385 MB and 11 s after. The fear was worth a measurement rather than a design.
- **The test found two shaders the fix had missed**, `Odyssey/GradientSky` and `Standard`, on the
  first run of the tier that was meant to confirm the fix. A hand-written list of runtime-found
  shaders is a list that is short by one; `ShaderInclusionTests` derives it from the source
  instead, which is the same bargain `RegistryTests` makes for player-facing names.
- **A second, unrelated build-only fault in the same log.** `SettingsPresenter.Attach` threw a
  `NullReferenceException` every frame on `_bootstrap.Directors.Overlays`, three lines below its
  own comment explaining that attaching deliberately does not wait for `Directors`. `OnDestroy`
  had guarded that exact expression since it was written — one rule, two places, one of them
  knowing. The compiler had been printing `CS8602` at that line in every build log.
- **Verified:** the player log went from 109 lines with a per-frame exception and two
  shader-not-found warnings to **38 lines, clean**. Fast tier 736 + 445; EditMode **1837 total,
  1823 passed, 0 failed**; build 385 MB, 0 errors.
- **The gap this closes is a category, not a bug.** Two green tiers say nothing about whether the
  game runs, because both compile and run in the editor's domain. `PlayerBuild` now refuses to
  build with a runtime-found shader off the list, so the next one fails loudly instead of
  shipping an empty world.

## 2026-09-19 — Two wrong answers before the empty build gave up its cause

The owner's report after the first player build was "no terrain, no graphics, apart from
characters". It took three attempts, and the first two are the instructive part.

- **First answer: shader stripping.** Real, measured, and not the cause. Every shader the game
  finds at runtime was genuinely absent from the player, the log said so, and fixing it was
  worth doing. But I reported it as *the* fix on the strength of a clean player log — and the
  player had been sitting on the main screen, where none of the renderer under suspicion runs.
- **Second answer: the same mistake again.** A cleaner log, still from a menu. I had proved the
  shaders were missing and never proved that was *why* the world was empty. Those are different
  claims and I ran them together.
- **The real cause was written down in the code before the problem existed.**
  `ContentPack.FindRoot` walks up for a directory holding `Assets` and `ProjectSettings`, and its
  own remarks say: "A built player has neither directory and would land in the throw below, which
  is deliberate. Nothing in CI or scripts/ builds a player, so shipping the pack is not solved
  here rather than solved wrongly here" — and it names the answer, `UseRoot` with
  `Application.streamingAssetsPath`. `UseRoot` had existed, unused, waiting for the day somebody
  built a player. That day was four hours earlier.
- **A deliberate limitation outlives the sentence that justified it.** "Nothing builds a player"
  was true when written and false the moment `unity.sh build` landed. The note was findable and I
  did not find it until the third pass, because I was looking at the renderer.
- **The fix is the one the note specified.** `ContentPackBuild` stages `Assets/Odyssey/Defs` into
  `StreamingAssets` before a build and removes it after, so the repository keeps exactly one copy
  of the pawn tuning and the world tables — the standing rule in CLAUDE.md. The composition root
  calls `UseRoot` outside the editor only, so Def edits still take effect immediately on Play.
- **And the reason it took three passes is now fixed too.** `-odyssey-newgame` boots a player
  straight into a colony, so a build can be smoke-tested from a terminal. The log that finally
  settled it reads `world 120x120x16 seed 1 generated in 61 ms … catalogue 121/138 rows have
  art`, which is the sentence none of the earlier runs could have produced whatever was wrong.
- **Verified:** world generates in the player, no exceptions, no missing shaders. Fast tier 736 +
  445; EditMode **1837 total, 1823 passed, 0 failed**; build 386 MB, 0 errors.

## 2026-09-19 — The third cause of the empty player, and the flag that hid it

Two fixes had landed and the world still did not draw. This is the third cause, the wrong turn that
was taken instead of it, and why neither was visible from the editor.

- **The build could not finish at all, and that was a separate fault.** A cold build was compiling
  **884,736** variants of URP/Lit's `ForwardLit` fragment pass, at about six a second and falling —
  roughly a day and a half. The previous night's run had died mid-way with *"Internal error
  communicating with the shader compiler process… Protocol error - failed to read magic number"*,
  which reads like a flaky tool and is not: it is what happens when sixteen compiler workers grind
  at that for half an hour.
- **The cause was an uncommitted working-tree change.**
  `UniversalRenderPipelineGlobalSettings.asset` has `m_StripUnusedVariants: 1` in git and had been
  flipped to `0` locally, in all three places the asset stores it. The build log says what that
  cost in one line: *After built-in stripping: 884,736 → After scriptable stripping: 884,736* —
  URP's stripper ran and removed nothing. `PC_RPAsset.asset` was dirty in the same way and for the
  same reason: its `m_Prefilter*` fields are a cache the build preprocessor writes, and with
  stripping off the build had written back a version that prefilters nothing.
- **Restoring the committed value took the same pass to 64 variants and the build to 12 seconds**,
  386 MB — which is exactly the *"385 MB and 11 s before, 385 MB and 11 s after"* recorded when
  always-including URP/Lit was first measured. That measurement was honest; the flip came later.
  The build does not write the flag, so this was a person or an editor UI, once.
- **And the world was still empty, which is the part worth keeping.** A diagnostic in the player
  reported `draws 1731 instances 43921 chunks 104 materials 22 surround 18192`, with URP/Lit
  found, supported and carrying five passes. The renderer was doing all of its work and none of it
  reached the screen.
- **Always-included keeps the shader; it does not keep the variant.** `INSTANCING_ON` comes from
  `#pragma multi_compile_instancing`, and Unity's **built-in** stripping — the 226-million-to-384
  step, which runs whatever URP's setting is — drops that axis unless some **material asset** has
  instancing switched on. Every instanced material in this game is built at runtime from
  `Shader.Find`, so there was none. `Graphics.RenderMeshInstanced` then draws into a variant that
  does not exist, silently.
- **This is almost certainly why the flag was flipped.** Switching URP's stripping off is a
  plausible thing to reach for when variants are going missing. It does keep them — along with
  884,734 others.
- **The fix is one instancing-enabled material asset per kept shader**, under
  `Assets/Resources/OdysseyKeepAlive`. Measured, because the received wisdom is that this is
  expensive: it doubles the kept variants on the pass that matters and does nothing else — 64 →
  128 fragment, 16 → 32 vertex, which is the instancing axis and only that. 386 MB either way.
- **A ShaderVariantCollection was the alternative and is worse.** It has to name the keyword
  combination, and which combination a runtime material lands in is the pipeline's business — a
  named combination is a guess that goes stale when a quality setting moves. An instancing-enabled
  material states the one thing we actually know.
- **A test whose name claimed more than its assertion is why nobody looked here.**
  `TheInstancedVariantOfTheLitShaderIsKept` only asserted the shader's name was on a list. Being on
  that list is precisely what did *not* keep the instanced variant. It is now
  `TheLitShaderIsOnTheAlwaysIncludedList`, and `EveryKeptShaderAlsoHasAnInstancingKeepAliveMaterial`
  asserts the thing the old name promised — including that a keep-alive has not had its instancing
  switched off, because present is not the same as right.
- **`ShaderInclusion`'s header said "PlayScene.Build calls it" and nothing did**, from the day it
  was written. Corrected rather than propagated into the new file. This is the same shape as the
  note that cost three passes the night before: a sentence about the system that was never true.
- **Verified:** player built from a clean tree in 14 s, 386 MB, 0 errors; run headless with
  `-odyssey-newgame`, the log is 94 lines with no exceptions and no missing shaders, and a
  screenshot shows terrain, trees, grass and a colonist. Fast tier 736 Sim + 445 Hud. Both content
  gates clean.

## 2026-09-19 — The fourth cause: the pack's own shaders were never on anybody's list

The owner, on the build that had just been reported working: *"None of the items like meals, wood,
stone are visible"*, then *"no grass either"*. Terrain, trees, rock and colonists drew. Grass tufts,
bushes and every item pile did not.

- **The renderer was again doing all of its work.** A diagnostic reported `things=7
  itemInstances=19 kindsDrawn=3 kindsWithArt=6` — items existed, had art and were submitted, and
  none of them reached the screen. The same signature as the terrain fault two hours earlier, which
  is what made it obvious where to look and nearly made it obvious in the wrong place.
- **The material told the truth as soon as it was asked.** Logging the material behind each item
  def gave: `mat='Generic_01_A' shader='Synty/Generic_Standard' instancing=False
  kw=[_ALPHATEST_ON _EMISSION _NORMALMAP]` and `shader='Synty/Generic_Basic'`. **Props are not drawn
  with URP/Lit at all.** They wear the pack's own Shader Graph shaders, which `ShaderInclusion`
  never mentions because nothing ever calls `Shader.Find` for them — they ship because prefabs
  reference them.
- **And every material asset that references them has instancing off.** `ModuleLibrary` takes the
  prefab's `sharedMaterial` as it is, and `DressGround` explicitly returns the licensed source
  untouched when no adjustment is wanted. Built-in stripping therefore kept no instanced variant of
  those shaders, and `Graphics.RenderMeshInstanced` drew into a variant that was not there.
- **Terrain and trees were visible for the one reason that hid this**: `DressGround`'s *other*
  branch clones with `enableInstancing = true`, and `TreeMaterials` builds its own material. The
  two paths through one function differ in exactly the property that decides whether a thing is
  visible in a player, and only the cloning one had ever been exercised by anything that drew.
- **The fix cannot be committed, and that shapes it.** A keep-alive material for a pack shader
  references licensed content by GUID: committing one would put pack content in the repository and
  would dangle on a clone without the pack. So `SyntyInstancingKeepAlive` creates them before a
  build and deletes them after — the same bargain `ContentPackBuild` makes with the Defs. On a
  machine with no pack it finds nothing and does nothing.
- **Derived from the catalogue, and the difference is 54 MB and six minutes.** The first version
  scanned all 385 materials in the pack: **126** distinct shader/keyword combinations, 440 MB, 6m42s.
  Reading the module catalogue instead — the single place where an id becomes a mesh, and the same
  list `ModuleLibrary` reads at runtime — gives **8** combinations, 429 MB and 8 s. The 43 MB over
  the 386 MB baseline is the price of instanced variants for the pack's shaders and is not
  avoidable while props are drawn instanced.
- **Four causes, one symptom, and each fix made the next one visible.** Missing shaders, missing
  content pack, missing instancing variant for our shaders, missing instancing variant for the
  pack's. Nothing but a player build can see any of them, and each was invisible until the one
  before it was fixed — which is the argument for `unity.sh build` being a gate rather than a
  thing somebody remembers to run.
- **Verified:** build 14 s, 429 MB, 0 errors; run headless with `-odyssey-newgame`, 0 exceptions,
  and an in-game screenshot shows grass tufts, bushes, crates, wood logs and loose rocks. Fast tier
  736 Sim + 445 Hud.

## 2026-09-19 — 8-directional diagonal movement and navigation

The owner requested that colonists use diagonal movement in both the game system and the animation,
rather than walking in rigid 4-connected linear straight lines.

- **Full 8-directional horizontal navigation on the grid.** `NavGraph`, `NavGrid`, `PathFinder`,
  and `MovementSystem` now support 8 horizontal moves. Diagonal steps cost `MoveCost.Diagonal = 141`
  at baseline (~1.414x orthogonal), with terrain resistance and door/hazard penalties scaled
  proportionally by 1.41x reflecting 41% longer travel distance through the cell.
- **Strict corner-cutting prevention (RimWorld rule).** A diagonal step between $(x, z)$ and
  $(x \pm 1, z \pm 1)$ is legal only if both flanking orthogonal cells $(x \pm 1, z)$ and
  $(x, z \pm 1)$ are walkable for that traverse mode. Pawns cannot clip through outer building
  corners or squeeze through diagonal cracks between solid walls or rock.
- **Vertical hops remain orthogonal.** Unaided jumping up +1 or dropping down -1 layer into a
  neighbouring column remains 4-way cardinal. Diagonal movement is strictly horizontal on the
  same layer.
- **Octile distance heuristic replaces Manhattan.** `PathFinder.CellHeuristic`, `RegionHeuristic`,
  and `ColonyItems.Distance` now use the exact octile metric:
  $h = \min(dx, dz) \times 141 + (\max(dx, dz) - \min(dx, dz)) \times 100 + dy \times \text{LayerChangeHint}$.
  Because the heuristic matches true diagonal geometry, A* explores a narrower search corridor
  and finds direct paths in ~30% fewer steps.
- **Dynamic link generation and affected zones.** `NavGraph.BuildInteriorZone` and `BuildEdgeZone`
  now pair diagonal spans across region boundaries with strict corner-cutting validation.
  `CollectAffectedZones` marks all adjacent boundary zones dirty when a block changes, preserving
  full-vs-incremental rebuild determinism.
- **Presentation and animation are fluid out of the box.** `PawnPose` computes headings directly
  from travel vectors, and `PawnFigureDirector` smoothly rotates characters towards diagonal
  bearings (45°, 135°, etc.) at 540°/s with displacement-driven gait blending.
- **Verified:** fast tier **741 Sim + 445 Hud** (1,186 passed, 0 failed), including 5 new tests in
  `DiagonalMovementTests.cs` (straight diagonal path, wall corner-cutting prevention, seam block,
  proportional terrain cost scaling, and district reachability agreeing with exhaustive flood).
  Both wiki and registry content gates clean. Golden master simulated hashes re-baked in
  `Golden.cs` for `Meadow`, `PlayedBoard`, and `City` to reflect colonists travelling diagonally.


## 2026-09-19 — Five real chimes, and the one they replaced could never have fired

The owner supplied five notification recordings and called the sound already in the game *nasty*.
It was: `AudioSetup.Alert()` synthesised a 0.6 s two-note sine, 830 Hz stepping down to 622 Hz,
and the file it wrote sounded exactly like the two lines of trigonometry that made it. Replacing
it was meant to be an afternoon of ffmpeg and a catalogue row.

**The thing worth writing down is what turned up on the way.** `AlertWatch` — the only path from
the simulation to a chime — tested the published food need against `StarveThreshold = 12`, under a
comment reading *"food, in the published 0–100 units"*. Food is published 0–1000. `AlertModel`, the
red row on screen, uses `StarveAt = 120`. So the chime was set to fire at a hundredth of the food
the warning exists for, which is to say at a colonist who is already dying, long after the panel
had given up shouting. Nobody had reported it, because a sound that never plays sounds like a
sound you have not triggered yet.

Its three unit tests passed. They fed the watcher literal `13`, `5` and `0` — numbers chosen to
straddle the constant — so they proved the comparison worked and could never have noticed that the
constant was on a scale that does not exist. **A test that restates the number it is testing tests
the code around the number.** That is a variant of P1 worth having in the register beside the hop
price and the ladder face: not two owners disagreeing, but two owners on different *units*.

So the fix is not a corrected constant. `AlertWatch` is gone. `AlertChimeWatch` reads the alerts
panel's own rows and returns a sound when one appears; `AlertModel` is the single owner of what an
alert is, of its hysteresis, of its severity and of whether the player dismissed it. The chime is
raised from `HudShell.RefreshAlerts`, in the same pass that builds the row, because a sound landing
on a different frame from the line it belongs to reads as two events rather than one. A pleasant
side-effect: `AudioDirector.Sync` no longer walks the pawn list every frame, so the whole change is
a small saving on the frame rather than a cost.

**The mapping is severity first, key second**, and that was the deliberate decision rather than the
obvious one. The obvious shape is a table from alert key to sound, which is correct and which would
have covered the three alerts that exist. But `icon-keys.csv` declares twenty-two `ui.alert.*` keys
and nineteen of them are unimplemented; a key-first table means every one of those arrives silent,
and the person who implements fire or a hull breach has to know that a second file wants editing.
Severity-first means an unknown key already chimes — correctly, because the panel had to pick a
severity for it to be drawn at all — and `AlertChime.Overrides` exists only for conditions severity
undersells. It has one row: `ui.alert.raid`, because a raid and a starving colonist are both
`Danger` and must obviously not make the same noise. That row is also the seam doing its job in
advance: the raid siren is imported, mixed and mapped, and the day something raises that key it
plays with no code change.

**On the files themselves.** The five arrived spanning −14.5 to −24.5 LUFS. Ten decibels is the
difference between a chime that startles and one that is missed entirely, and no amount of
catalogue tuning fixes it properly, because `Volume` is where the *mix* lives — how important a
sound is — and it should not be absorbing how loud somebody's export happened to be. So
`tools/audio/bake_alerts.sh` matches them: two-pass EBU R128 to −18 LUFS with a −1.5 dBTP ceiling,
which lands every clip inside two decibels of every other. `alert-normal` stops at −20 because it
is a peaky bell and the true-peak ceiling binds before the loudness target does; squashing the
transient to reach −18 would have been changing the sound to satisfy a number, so it keeps its
crest and gets the two decibels back as catalogue `Volume` 0.95. That is the division of labour the
catalogue comment already claimed and this is the first case that tested it.

The raid siren also carried half a second of silence before it started — half a second of nothing
after an alarm has been raised — so the bake trims dead air at −45 dB in and −50 dB out with 6 ms
guard fades. Raid 10.73 → 9.54 s, joined 6.38 → 5.77 s, normal 1.96 → 1.57 s. **It does not
compress, EQ or shorten anything musical, and the script says so in its header**, because the
moment a bake tool starts making taste decisions nobody can tell which sound they are listening to.
Whether nine and a half seconds is an alert or a cutscene sting is a question for the owner, and it
is in the design doc as one.

Import class splits on whether the sound is on a critical frame. `normal` and `negative` fire in
play and are PCM decompressed on load, 168 KB each, no decode at the instant they sound. `happy`,
`joined` and `raid` are seconds long, rare and not latency-critical, so they ride ADPCM compressed
in memory: 1.3 MB of alerts in total rather than 2.8. None is forced to mono — Unity's importer
peak-normalises the downmix when `forceToMono` is set, which would throw away the loudness match
the bake exists to produce, and that trap is worth remembering the next time somebody tidies a
stereo clip.

Three of the five are in the library and played by nothing, which is `SoundIds.Campfire`'s bargain
and the same words are used for it. `happy` needs an `AlertSeverity.Good` that does not exist, and
a green row in a panel whose job is problems is a design question rather than a plumbing one.

Unity EditMode **1848 total, 1834 passed, 0 failed**. `docs/design/24-alert-sounds.md` holds the
decisions; ADR 0010's alert clause is amended in place rather than rewritten, because its playback
half — 2D, own bus, starts the duck — is exactly as decided and only the trigger moved.

## 2026-09-19 — One recording, two ends of a carry

The owner asked for pickup and drop sounds on the carry animation, from one supplied recording,
*"process and alter sound to distinguish pick up and drop … clarify the sound to me because it will
played often … also blend it into the environment."* Three requirements, and the second and third
are in tension with the first: a sound distinct enough to tell apart is a sound loud enough to
notice, and this one plays on every leg of every haul.

**The distinguishing is done by resampling, not by pitch-shifting.** The lift runs at ×1.14 and the
drop at ×0.82, which moves pitch and length together — up two and a half semitones and an eighth
shorter, down three and a half and a fifth longer. That is the right transform here *precisely
because it is not a clean pitch shift*: a bigger, heavier object really does sound both lower and
longer, so the artefact is the effect. A formant-preserving shift would have given two sounds of
the same size at different pitches, which the ear hears as one sample played twice. EQ seals it —
a high-pass and a shelf at 3 kHz on the way up, body at 220 Hz and a low-pass at 5.5 kHz on the way
down.

**Three takes of each**, at rates spread four per cent either side, on top of the catalogue's own
±7% per-play pitch. This will be the most repeated sound in the game once footsteps exist, and one
sample is recognisable as a sample within three or four plays.

**Blending into the environment is a mix decision and it is the whole of the third requirement.**
Volume 0.40 against the axe's 0.85, dying at 120 m against its 200, priority 150 against its 120.
Under the work rather than beside it. The short range is as much about the frame as the mix — the
director culls by range before it spends a voice, so a stockpile run at the far end of the board
costs nothing — and the low priority means an axe or a chime takes the voice when the pool is full,
which is the right way round: a hauler is the background of a colony.

**The drop fires at the end of the fall and not the start**, which is the one thing here that is a
fault rather than a taste. The load is visibly in the air for another third of a second after the
hands open, so a sound at the release reads as a colonist dropping something they are still
holding. That needed an edge rather than a condition: `CarryHandover.FallFinished` is true forever
afterwards, so polling it turns one thud into a buzz. `FallLanded(before, after)` takes both sides
of the frame step, fires exactly once, and still fires when a frame is longer than the whole fall —
a stall, a load screen, or a step taken the instant a paused game resumes. Both ends are published
as events from the figure director, `LoadLifted` and `LoadSet`, for the same reason `BlowLanded` is.

**Two things about the processing were worth the hour they cost.**

The bake for the alert chimes uses two-pass `loudnorm`, and reusing it was the obvious move. It
cannot be used: EBU R128's integrated loudness is gated in 400 ms blocks, these clips are under
half a second, and `loudnorm` reports `-inf` and refuses its own second pass. Peak-ceilinged RMS is
what a one-shot wants anyway — what matters about an impact is how hard it hits, not how loud it is
over time — so that is what `bake_carry.sh` does, and it says why in its header so the next person
does not repeat the experiment.

And the first working version came out **pinned at 0 dBFS on every clip**, three and a half
decibels hotter than the gain it had computed. The measurement pass ran the filter chain into
`-f null`, which is a pass cheaper; the stereo-to-mono downmix lands differently on the null muxer
than it does on a WAV, so the level it measured was not the level it wrote. The script now writes
the file first and measures the bytes that will ship. **Measure the artefact, not a proxy for it**
— which is the same lesson as `docs/lessons.md`' entry about checking that the edit an experiment
relies on actually applied, in a different costume.

The source was also very quiet, −44.6 LUFS with peaks at −28 dBFS: quiet enough that the whole
signal sits below the level a denoiser takes for noise and below the level a silence trim takes for
silence. Run either on the raw file and the clip comes out empty. Gain first, then clean.

Unity EditMode **1850 total, 1836 passed, 0 failed**. `docs/design/24-carrying.md` §12.

## 2026-09-19 — A bed for the title screen, and the volume slider that was never saved

The owner supplied a deep-space drone for the main screen: fade it in, loop it, fade it out into
the game audio when a world arrives, never play it in a colony, keep it really low.

**Where it plays was the only hard part, and it was hard for a structural reason.** `AudioDirector`
is built *from a world* — its grid size, its terrain mirror, its surface layer — and
`OdysseyBootstrap.LateUpdate` returns before touching it while `_world` is null. The menus are
precisely the state in which there is no world, so there is no audio of any kind on the title
screen and never has been. Making the director constructible without a world was the obvious move
and the wrong one: it would leave a half-built director whose beds and probe wait for a second
initialisation that nothing in the constructor hints at. A title screen wants one looping 2D voice
and a fade, so `MenuAmbience` is one looping 2D voice and a fade, owned by the root rather than by
the session — built once, surviving every world made and torn down, disposed with the component.
`Sync(unscaledDeltaTime, wanted: _world == null)` sits above the guard, and `wanted` is the whole
rule.

**It reads the player's faders rather than keeping any.** The temptation was a small `_busDb[5]`
of its own, which is a second owner of a rule and precisely the fault I had deleted from the alert
path earlier the same day. `AudioSettingsStore` is the one owner and this is a second reader:
`AudioSettingsStore.Load()` each step while the bed is audible, which is five PlayerPrefs lookups
against an in-memory dictionary, on a menu, and stops entirely once a world exists.

**That turned up a real gap.** `SettingsPresenter.ApplyBusDb` began `if (audio == null …) return;`
— and `audio` is null every moment before a world is built, while the settings page is perfectly
reachable from the main screen. So a player who set their volumes on the title screen had them
**silently discarded**, and would have found the Music fader drawn on that very page doing nothing
to the bed underneath it. The store is now written whether or not a colony exists, and the director
is pushed to only if it is there. Not a bug anybody had reported, and not one anybody would have
reported as a bug — "I set the volume and it didn't stick" is the sort of thing a player assumes
they imagined.

**The fades are asymmetric on purpose.** Eight seconds arriving, because the bed should already be
the air by the time the player has read the menu; four leaving, because it has to be *gone* before
the world it is handing to has finished arriving. The outdoor bed's `ArrivalFadeSeconds` is already
four, so the two cross rather than queue — and that relationship is pinned by tests that read the
shipped catalogue rather than restating constants, so retuning by ear stays free and drifting into
a gap fails.

**The arrival latch was wrong first time, and only a test that looked mid-fade could see it.** I
set `_arrived` on the first `Sync` rather than on reaching full, so the eight-second fade governed
one sixtieth of a second and the remaining 7.98 ran at the four-second leaving fade. Both endpoints
were correct — silent at nought, full at eight — so any test that checked the two ends would have
passed. The one that caught it asserted the level was between 0.2 and 0.6 at four-tenths of the
way through, and got 0.80.

**On the clip.** Deeper than it looks: 20–120 Hz carries almost all of it, 2 kHz and above is
effectively silence. That explains its −27.6 LUFS, which is mostly K-weighting doing what it does
to sub-bass against an actual peak of −11.4 dBFS, and it means **this will behave completely
differently on laptop speakers from headphones** — worth knowing before anybody retunes the level.
It is also genuinely wide, sum and difference within 2.4 dB, so it is emphatically not folded to
mono: the width is most of what makes a bed read as everywhere rather than as over there, and here
it is nearly all there is. The whole 157 s is kept, with the last six seconds blended into the
first six and the blended tail dropped — a seamless 151 s loop, measured at 0.4% of full scale at
the seam. Peak-normalised to the same `BedPeak` every other bed uses, so catalogue `Volume` means
the same thing across all three; left at its native 24 kHz, because resampling a signal with
nothing above 500 Hz to 44.1 would add eight megabytes of nothing to the repository.

Volume 0.18 against the day bed's 0.28. Music bus, not Ambience — it is a menu track, and a player
who turns music off should get a silent title screen.

Unity EditMode **1857 total, 1843 passed, 0 failed**. `docs/design/17-start-flow.md` §12.

## 2026-09-19 — Two asks that were half built already, and a rule with an exception

Owner, in one message: the bed submenu should say who is already housed; and *"when I click on wood
I can't see how many is this pile — can we combine piles up to a maximum"* with a third/two-thirds/
full graphic. Then, a minute later: *"can you auto assign a bed if it's been unoccupied or not
claimed for a while so colonists find an empty bed to sleep in — instead on the floor where
possible."*

**Grounding first paid for itself twice.** Two of the three things in the pile request were already
in the code: `ColonyItems.Spawn` has merged stacks to `stackLimit` (75 for wood) since it was
written, and the inspect pane has printed "27 in the pile" since 2026-09-17 — in the state line, in
the pane's smallest grey type, where the owner read it and did not see it. So the pile work was not
"build stacking and a counter"; it was **move the number to where the eye lands and let wood into
the ramp that already existed**. `ItemHeap` had drawn stone, ore and coal as a growing scatter for
weeks; wood's row was `null`.

The same check reframed the bed ask. `TrySleep` has always let anyone sleep in an unowned bed — an
unowned bed is a shared pool — so nobody was on the floor *for want of finding* a free bed.
Auto-assignment does not get anyone off the floor. What it buys is that who sleeps where stops
being redecided every night by whoever is nearest, which is what makes a tick in the picker worth
drawing.

**And it needed an exception, or it would have caused the complaint it was asked to fix.** Claiming
takes a bed out of the shared pool permanently. Two beds between three colonists, with a naive
claim-on-sleep, means the first two privatise them and the third sleeps on the floor for ever — a
strictly worse colony than before the feature. So the claim is conditional on the pool still
covering everyone who has none: `UnownedBedCount() - 1 >= bedless others`. With a bed each,
everybody claims and nothing is lost; short of beds, nobody claims and they stay shared.
`TooFewBedsToGoRoundAreLeftInTheSharedPool` is the test that holds it.

Three smaller decisions worth keeping:

- **The claim is on arrival, once.** Not every sleeping tick — the rule counts beds and colonists,
  and fifty colonists asking it sixty times a second would be the only thing in `SleepJobDriver`
  that cost anything. Not on the collapse branch either: a body that goes down on the way to a bed
  has not reached it.
- **Wood scatters on the floor but is still one bundle in the arms** (`Recipe.CarriedAsHeap`). The
  two were the same answer only because rubble was the only thing that scattered. `Armful` draws a
  fixed three, so letting wood through unchanged would have put three bound log piles in two hands
  — undoing a load the owner had looked at and tuned the same week.
- **Three bundles, not seven.** `SM_Prop_LogPile_01` is a wide prop where a boulder is a small one.
  Seven in a 2.5 m cell is a log-jam. Three is also exactly the third/two-thirds/full ramp asked
  for, reached through the mechanism already in the game rather than three new props.

The "unoccupied for a while" half — taking a bed back off an owner who has stopped using it — is
**not built and is recorded as open**. One bed per colonist is enforced at assignment and there is
no death model, so there is no state a staleness timer could fire on. A mechanism with nothing to
trigger it is a thing a later session would have to delete.

Fast tier **745 Sim + 446 Hud**, Long tier **21**. The ten-day goldens did not move, and the reason
is worth writing down: the scenario's own sleeping spots carry no edifice record, so `AssignOwnerAt`
refuses them and `UnownedBedCount` does not see them. They were never ownable and still are not.

`docs/design/24-pile-reading.md` (new), `docs/design/20-beds.md` §13 and §13a.

## 2026-09-19 (later) — A quarter of a cell, found by printing a table

Owner, after the first round: clicking a tile that has wood in it always gets the wood and never
the tile; and clicking a bed "seems to be really specific" when either of its two cells should do.

The first is a straightforward precedence problem with a two-rung answer — thing, then cell, then
thing — read off the selection rather than kept in a counter, so nothing has to remember to reset
it. Worth noting that the precedence it modifies was **itself a fix**: a pile on bare ground used to
be unselectable because the click fell through to the cell. Both reports are real and the answer is
an order, not a winner.

**The second one is the entry for `docs/bug-patterns.md`.** Reading the code found nothing, because
nothing in it is wrong: `SlicePicker` returns both bed cells correctly, `CellDetailContributor`
publishes bed facts for both, `InspectModel` renders them from either. Three files, all correct, and
a bug the owner can feel.

So: a probe test that swept a 48° ray along the bed in quarter cells and **printed the cell that
came back** — once aimed at the floor, once aimed at `BedShape.MattressTop`. The floor column was
perfect. The mattress column was shifted by exactly one quarter-cell step, all the way along:

```
aim at the grass in front of the bed  ->  the bed
aim at the near half of the bed       ->  the bed's OTHER cell
aim at the far end of the bed         ->  the grass behind it
```

`SlicePicker` resolves a **non-occluding** cell by crossing that cell's floor plane, and a bed is
drawn 0.70 m above its floor. At the play camera's 48°, `0.70 / tan(48°)` = 0.63 m — a quarter of a
cell. Where the bed is drawn and where it can be clicked had never agreed, in any direction the
camera faces.

`WorldRenderModel.StandHeight` is the fix and the seam: a cell whose edifice stands without
occluding offers its own top plane to the ray, first, because it is nearer. It also means a bed now
shadows the sliver of ground behind it, which is what it is drawn doing.

Two lessons, both already in the catalogue in other clothes:

- **Reading three correct files does not find a bug that is in the space between them.** Measure.
  The memory note says this and it has now been right every time.
- **Test the ends, not the middle.** `BedPickHeightTests` walks the bed in tenths of a cell, because
  a check of the two cell centres alone would have passed *before* the fix — the drift is a quarter
  cell and a centre has half a cell of slack either side. That is the general rule for any report
  whose word is "fiddly" rather than "broken".

Fast **745 Sim + 449 Hud**, EditMode **1871 total, 1857 passed, 0 failed**, PlayMode **82/77/0**.
No simulation change, so no golden moved and the Long tier was not re-run.

## 2026-09-19 — Soft crowd avoidance and sub-tile visual lateral steering

The owner requested that colonists no longer walk straight through one another, item piles, or trees,
and instead manoeuvre naturally to the side of a tile (or pass on the right when meeting oncoming
pawns), with anticipatory steering before reaching the obstacle.

- **Two-tier separation (Sim path bias + Presentation lateral steering).** Following the clean room
  analysis of prior art (RimWorld's `PawnPathCost` and crowd passing), avoidance is split:
  1. *Simulation (pure C#):* `MoveCost.OccupiedBias = 30` added to local cell expansion in `PathFinder`
     when a cell is occupied by a standing pawn, encouraging planners to prefer empty adjacent tiles
     or corridor branches when available without hard-blocking or dirtying the D4 macro-region graph.
  2. *Presentation (view-side smoothing):* Sub-tile lateral offsets are applied purely in presentation
     (`PawnPose.Of`), modifying neither the simulation grid, save state, nor state hashes.
- **Save/load determinism preserved.** Global `PawnContext.Paths.Occupancy` defaults to null to guarantee
  that paths re-planned across save/reload boundaries cannot drift the tick state hash due to transient
  crowd layout variations. The occupancy predicate is provided via `PathOptions` and `PathService` for
  callers requesting live soft avoidance.
- **Kinematics and $C^1$ bell envelope (`SteeringCurve.cs`).**
  Lateral displacement follows $B(t) = 16t^2(1-t)^2$ peaking at $1.0$ at midpoint ($t=0.5$), with zero
  value and zero first derivative at cell boundaries ($t=0, 1$). This guarantees $C^1$ continuity and
  eliminates velocity/acceleration jerks at cell transitions.
- **Mutual right-hand passing.** When opposing pawns meet ($\vec{d}_1 \cdot \vec{d}_2 < -0.5$), both
  veer to their right along the lateral normal $(dz, 0, -dx)$ by up to $0.60\text{ m}$. Together they
  achieve $1.20\text{ m}$ mutual clearance ($\ge 1.0\text{ m}$ target) while remaining well within the
  $2.5\text{ m}$ tile half-width ($1.25\text{ m}$), leaving $0.65\text{ m}$ buffer to corridor walls.
- **Anticipatory obstacle deflection.** For static obstacles like tree trunks (`WorldRenderModel.HasObstacle`),
  `AnticipatoryLeadIn(t)` begins veering right during the second half of the preceding cell ($t \in [0.5, 1.0]$),
  smoothing the trajectory before crossing into the obstacle cell where lateral offset is maintained around the trunk.
- **Ground relief integration.** Lateral steering displacement is added to `along` before `OnTheDrawnGround`,
  so bank rise and terrain relief are sampled at the exact steered feet position.
- **Visual boundary snap and gait flicker fixed (2026-09-19).**
  - *Symptom:* The owner reported that when passing a tree, towards the end of the animation there was a
    sudden snap/jolt to another position and the animation flickered quickly.
  - *Cause:* `AnticipatoryLeadIn` had ramped up lateral displacement during the approaching cell, reaching
    $0.60\text{ m}$ at the cell boundary ($s = 1.0$). Upon crossing the boundary into the obstacle cell,
    the offset jumped from $0.60\text{ m}$ to $0.42\text{ m}$ (or to $0.0\text{ m}$ upon stopping/arriving),
    and rotated sharply if the path changed heading. This 18–60 cm single-frame displacement spiked
    `PawnFigureDirector.ObserveSpeed` to 10–36 m/s, causing `GaitBlend` to flicker into a sprint before settling.
  - *Fix:* Obstacle deflection is governed strictly within the obstacle cell by the $C^1$ bell curve
    $B(s) = 16s^2(1-s)^2$. At cell entry ($s = 0$) and exit ($s = 1$), lateral offset and derivative are
    identically zero, guaranteeing perfect position and velocity continuity across all boundaries, turns,
    and arrival stops. Peak clearance ($0.60\text{ m}$) occurs at cell centre ($s = 0.5$) abreast of the trunk.
  - *Verified:* `StepTransition_IntoAndOutOfTreeCell_IsContinuous` in `ObstacleSteeringTests.cs` confirms
    delta across entry and exit boundaries is strictly under 0.1 mm (< $10^{-4}\text{ m}$).
- **Enforced motion-to-position and tree avoidance across crowds and slopes (2026-09-19).**
  - *Owner request:* In crowd scenarios with many colonists, figures were snapping to positions when getting
    around each other, and sometimes colonists walked straight through trees. Mandated rule: *"we need to prevent
    snapping to other positions - the rule must be to motion to that postion or close to (be forgiving)."*
  - *Motion-to-position rule in `PawnFigureDirector`:* Added `DrawnPosition` to `Figure`. Rather than snapping
    `figure.Transform.position = drawn`, the figure motions towards `drawn` at a bounded maximum rate
    (`MaxAdjustmentSpeed = 5.5f` m/s) using `Vector3.MoveTowards`. Sudden lateral shifts across crowd passing,
    flanking obstacles, or direction reversals smoothly glide and sway into position rather than teleporting.
    Ground relief sampling and footing lean follow `figure.DrawnPosition`.
  - *Tree avoidance vertical coverage on slopes/terraces (`WorldRenderModel.HasObstacle`):* Trees rooted on
    slopes and terraces at `cell.Y - 1` extend up into `cell.Y`. `HasObstacle` now checks both `cell.Y` and
    `cell.Y - 1`, ensuring trees on slopes/terrace steps are recognized by presentation steering.
  - *Diagonal corner tree deflection (`PawnPose.cs`):* Moving diagonally past a corner cell holding a tree
    now calculates a deflection vector away from the corner trunk, preventing diagonal moves from cutting corners
    through tree trunks.
  - *Continuous crowd proximity weighting (`PawnPose.cs`):* Replaced hard boolean distance threshold (`dist < 3.0f`)
    with continuous `SteeringCurve.SmoothStep` proximity weighting. Approaching and departing pawns ramp their
    passing offset smoothly from 0 at 3.0 m to peak clearance at $\le 1.5\text{ m}$, eliminating boundary pops.
  - *Verified:*
    - Fast tier: **746 Sim + 445 Hud = 1,191 passed, 0 failed**.
    - Unity EditMode: **1,861 total, 1,847 passed, 0 failed** (new tests `DiagonalStep_PastCornerTree_SteersAwayFromObstacleCorner`, `TreeObstacle_OnLowerTerraceLayer_IsDetectedAtColonistLayer`, `PassingEncounter_DistanceThreshold_ScalesSmoothlyWithoutThresholdPop`).
    - Unity PlayMode: **82 total, 77 passed, 0 failed**.
    - Content gates: `build_wiki.py --check` and `emit_labels.py --check` clean.

## 2026-09-19 — the crowd playtest could not start: the debug spawn had never worked

The avoidance work above needs a crowd, and the owner could not make one: *"Every time I tried to
generate more colonists — I get this warning message `[Odyssey] the simulation refused 1293 x
SpawnPawn: OutOfBounds`."*

**The message was wrong in both of its interesting parts**, which is why it read as a mystery.

*`OutOfBounds` was a lie.* `DebugAnchorCell` returned the middle of the map at the **active slice
layer**, and `HandleSpawnPawn` treated that layer as an instruction. The play camera looks down at
open ground, so the slice layer is the air several storeys above the terrain: the cell was inside
the map, had nothing to stand on, and the handler's one rejection for "not walkable" happened to be
spelled `OutOfBounds`. Every debug spawn since the row was written has been refused; nobody had
pressed it before (it is on the standing "nobody has pressed Play on the debug menu" list).

*1,293 was a lie.* Nothing in the build had ever called `IntentBus.ClearRejected`, so the list grew
for the life of the session and `ReportRejections` re-counted the whole of it every frame. A handful
of clicks became a four-figure count within seconds — and a four-figure count is what makes a reader
look for a loop submitting intents rather than for one wrong cell.

**The fix keeps the seam.** The tempting repair was a widening walkable search inside `HudShell`,
and a first pass wrote one: 150 lines of two-pass radial search. It could not compile, and the
reason it could not compile is the design answer — **presentation has no access to `CellGrid` at
all**, by the snapshot-read/intent-write rule. The shell cannot know what is standable. So the
split is: the shell names the **column** the player means (selected colonist, else selected cell,
else the cell under the camera's focus — not the middle of the map, which is a place nobody is
looking at and hundreds of metres from the colony), and the simulation settles the layer.
`CellGrid.NearestWalkableInColumn` is the one owner of that fall for a spawn; `FirstFloorAtOrBelow`,
which has existed all along for exactly this question, does it for a resource grant, which shares
the anchor and so shared the fault. A column with genuinely nowhere to stand now answers
`NotPermitted`.

Recorded in `docs/bug-patterns.md` as *the caller's guess taken as the caller's instruction*, and
the design doc's claim that the anchor was "always in bounds, so the two action rows never have a
reason to refuse" is corrected in place — in bounds it was; standable it was not.

- *Verified:*
  - Fast tier: **749 Sim + 445 Hud = 1,194 passed, 0 failed** (three new `DebugIntentTests`).
  - Unity EditMode: **1,864 total, 1,850 passed, 0 failed**.
  - Unity PlayMode: **82 total, 77 passed, 0 failed**.
  - Content gates: `build_wiki.py --check` and `emit_labels.py --check` clean.

## 2026-09-19 — the vibration: a rule with a threshold in it, sampled every tick

Owner, after the crowd playtest the spawn fix unblocked: *"it seems better but the colonists
sometimes vibrate quickly — as if it's fighting something or a indecision or a check that is
happening — it's mostly smooth — but then vibrates with an odd movement."*

**Measured before diagnosed**, and that was worth the twenty minutes. Reading the code produced
three confident candidates — the path bias, the `MoveTowards` rate limit, the gait blend — and the
first two were wrong. A probe that ticks a real colony of twenty for 3,000 ticks and counts
per-tick changes in the lateral offset found it in one run: **85 changes over 5 cm in a single
tick, worst case the full 0.600 m envelope in one sixtieth of a second.**

All of them were thresholds being re-decided sixty times a second: `dot < -0.5` for "is this
colonist oncoming"; a `swappingCells || sharingNext || sharingCell` override that forced the weight
to 1.0 whatever the distance and flickered as pawns re-planned; a distance measured in x and z
alone, which made a colonist on the terrace above **nought metres away** on a board that is 3 m
terrace risers from end to end; and a choice between candidate offsets by whichever was longest,
which swaps winner — and therefore sign — on any twitch.

The rewrite is one continuous signed scalar: a bell envelope over the step, a smoothstepped
proximity in three dimensions, a smoothstepped converging factor, obstacle terms summed rather than
competing, and one clamp at the end. Crossing traffic gets room now, which it never did.

**And the previous pass's `MoveTowards` came out.** It rate-limited the *whole drawn position* at
5.5 m/s to satisfy the owner's "motion to that position or close to (be forgiving)". That damps the
colonist's own walking — the figure lags the gait its legs are playing and then surges to catch up —
and it left the sidestep inside the position `ObserveSpeed` differences, so a 0.6 m swerve read as
6 m/s, past the fastest gait this cast owns, and threw the legs into a run. That is the "gait blend
flicker" the pass before it went looking for in the blend and did not find. The rule is honoured on
the right quantity instead: the **sidestep alone** eases at 1.2 m/s, and `PawnPose.Of` hands it back
separately so the gait is solved from walking rather than from swerving.

Design is `docs/design/25-pawn-steering.md`; the pattern is in `docs/bug-patterns.md`.

- *Measured*, 20 colonists, 3,000 ticks, 59,303 moving samples:

  | | Before | After |
  |---|---|---|
  | asked-for sidestep: jumps over 5 cm in a tick | 85 | 52 |
  | **drawn sidestep: jumps over 5 cm in a tick** | **85** | **0** |
  | drawn sidestep: worst single tick | 0.600 m | **0.020 m** |
  | drawn sidestep: mean lag behind the asked-for one | — | 0.0016 m |

  The 52 that remain are the one input that cannot be made continuous — somebody stopping, setting
  off or turning — which is exactly what the sway is for.

- *Verified:*
  - Fast tier: **749 Sim + 445 Hud = 1,194 passed, 0 failed**.
  - Unity EditMode: **1,870 total, 1,856 passed, 0 failed** (six new `SteeringContinuityTests`).
  - Unity PlayMode: **82 total, 75 passed, 0 failed** — run in a scratch worktree with no Synty
    junction, so `AvatarSheetTests`' two art tests skip; nothing regressed.
  - Content gates: `build_wiki.py --check` and `emit_labels.py --check` clean.

- *Noted, not changed:* the simulation-side path bias is dead code. `PathFinder.Occupancy` and
  `MoveCost.OccupiedBias` are implemented and tested, and **nothing in the build sets them**.
  Switching it on moves planned routes and therefore every golden hash, so it wants its own change
  with a re-bake, not a line in this one.

## 2026-09-19 — the vibration, part two: presentation was inferring a number the simulation knew

Owner, after the steering fix: *"it seems better but the colonists sometimes vibrate quickly ... it
happens sometimes when colonists are walking, particularly where there is a terrain step tile it
starts to vibrate and move oddly mostly at the beginning of the frames when going up — so it still
exists just less of it."*

**The obvious reading was that the steering fix had not gone far enough, and it was wrong.** Running
the same measurement with the steering switched off gave numbers identical to the frame. One run,
and this was a different fault.

Presentation carries a figure on past the tick it sits on, so a display faster than the tick does
not show the same position twice. To carry it on you need the rate, and it was **inferring** the
rate from a global `movePerTick` out of the Defs, added to a percentage as though every step cost
`MoveCost.Orthogonal`. Wrong by the geometry (a hop up is 240, not 100); wrong by the colonist's own
pace and condition; and wrong by the price of the terrain being entered, which lives inside the step
cost and cannot be recovered from the two cells at all. **An over-estimate draws the next frame
behind the last one.** On flat ground that is a few millimetres of stutter nobody names; on a
terrace bank, backward travel is *downward* travel, so it becomes a visible vertical buzz — which is
exactly why the report was about step tiles.

So the number is published now — `PawnView.MoveDeltaPerMille`, how much of *this* step *this*
colonist retires in one tick, computed where both halves are known. Truncated down on purpose: an
under-estimate makes the frame after a tick jump slightly forward, an over-estimate makes it go
backwards, and only one of those can be seen. It is a view field, so nothing is saved or hashed.

**A false start worth recording.** The first fix scaled the inferred term by a step cost derived
from the two cells — orthogonal, diagonal, or `NavGraph.HopCost`. It measured beautifully on a
hand-built terrace (346 vertical reversals in 480 frames, down to nought) and then measured almost
nothing on the real board, because the terrain price is in the cost and geometry cannot see it. That
code is gone; deriving the step cost in presentation is not a thing to try again.

**Every cheap fixture missed this, twice.** A hand-built world grows no banks — `BankLayout` reads
generated terrain — and a hand-built `PawnView` publishes no rate, so it takes the fallback path
rather than the one the game takes. `WalkContinuityTests` therefore ticks a real colony over a real
generated board, and asserts that no frame ever draws a colonist behind where the last one did.

- *Measured*, wooded meadow, 12 colonists, 2,500 ticks, two frames to the tick:

  | | Before | After |
  |---|---|---|
  | frames drawing a colonist **backwards** along her own step | 3,172 of 59,000 | **0** |
  | worst backward frame | 10.9 mm | **0** |
  | frames reversing vertically, walking on the flat | 1,406 (2.5%) | **0** |
  | frames reversing vertically, climbing | 2 | **0** |

- *Verified:*
  - Fast tier: **749 Sim + 445 Hud = 1,194 passed, 0 failed**.
  - Unity EditMode: **1,872 total, 1,858 passed, 0 failed** (two new `WalkContinuityTests`).
  - Unity PlayMode: **82 total, 75 passed, 0 failed** — scratch worktree with no Synty junction, so
    `AvatarSheetTests`' two art cases skip.
  - Content gates: both clean.

## 2026-09-19 — the baseline audit: a number nobody had, and a list nobody could find

Owner, with the slice nearly closed: *"We're now established workable baseline and this is the
time to establish ground rules and a proper way forward"* — scalability for larger maps and more
happening, the process that has been working written down, an audit of every system for gaps,
performance and monoliths, and everything not yet addressed brought up with a verdict. One agent
allowed, for tokens. The output is `docs/audit/2026-09-19-baseline.md`, `docs/process.md`,
`docs/plans/playtest-queue.md` and the **HT** track in `vertical-slice.md`; this entry is the
reasoning.

**The one measurement, and why it had never been taken.** Every tick benchmark on record holds
the world still: OQ-19's two arms are a colony at rest and a colony under replan pressure, and the
soak is five colonists on a meadow. A colony that is mining and building edits the world most
ticks, and an edit is the one thing that makes `NavGraph.Rebuild` run. So a throwaway console
program in the session scratchpad, never in the repository, built the wooded colony through
`ColonyWorld.Build` and mined one face a tick through the same edits `MineJobDriver` makes:

| Board | Colonists | Tick at rest | Tick with one mined cell | `nav.Rebuild` alone |
|---|---|---|---|---|
| 120 × 120 × 16 | 5 | 0.014 ms | **0.414 ms** | 0.449 ms |
| 250 × 250 × 40 | 50 | 0.065 ms | **1.187 ms** | 1.150 ms |

Container, 4-core Xeon at 2.8 GHz, .NET 8. Five edits a tick cost 1.47 ms, not five times more:
the cost is per *rebuild*, not per edit, and it is `RebuildAdjacency` and `RecomputeDistricts`
walking every one of 24,000 live regions for a change confined to one 10 × 10 block. The flood
itself is local and cheap; the three passes after it are global. That is the only cost this
project has measured that grows with the board rather than with what is happening on it, and it
is **HT1**. The rest cost at the scale target with fifty colonists — 0.065 ms — agrees with OQ-19
on a machine three times faster and says the design underneath is right.

**What the one agent was for.** The simulation could be audited by reading; the seven largest
presentation, HUD and editor files could not be held in one head beside it, so the agent read
them with a fixed brief and a fixed return format, and its report went into the audit verbatim
(§4d-i) after four of its line-level claims were spot-checked and held. It found three scaling
terms the tick audit could not see — one unbatched draw call per standing order and per site, a
band loop over every chunk with no frustum test, and five aspect-table scans per figure per frame
— and a low-risk first cut for each monolith. The bootstrap's overlay drawing and the renderer's
marker primitives are one concern split across two files, which is why they move together.

**The Burst reserve is a promise with no road to it.** ADR 0005 calls Burst "the main remaining
performance reserve"; the manifest has no Burst, Collections or Mathematics package, and
`Odyssey.Sim` has `noEngineReferences: true`, which is the property the fast tier and U01's
reflection test exist to keep. The queue has carried "decide whether Sim may reference Burst" in
its *needs the owner* list since 2026-09-16. The audit commits to an answer — no; kernel
interfaces in the Sim, `Odyssey.Sim.Native` implementing them, a test that both kernels hash
identically — as **HT4**, a decision rather than a build, because a hot path written against the
wrong seam is the expensive mistake and none has been written yet.

**No event seam, and two bugs that already needed one.** The chime that could not fire and the
vibration on the terrace steps were both presentation inferring something the simulation knew;
the journal's own lesson of 2026-09-18 says inferred state is late and partial. The storyteller at M6 has nowhere to write. **HT3** is
an event log published as sparse rows, the `PawnAspect` shape, not hashed and not saved because
an event is derived from a state change that already is. C# events and a message bus were
considered and rejected for the reasons the seam rules already give: cross-seam references,
ordering by subscription accident, nothing replayable.

**The process finding was the least technical and the most important.** Merges ran at 61, 37 and
12 a day over three days; playtests at a handful; `CLAUDE.md` held 27 unplayed changes in a
134-line section of a file its own warning says fails at 982 lines, and two of its lines had gone
stale in exactly the way it warns about (the skills gap outlived WS2/WS3 by a day; the test
counts by three PRs). The owner is the only person who can press Play, so the playtest list is
the critical resource, and it was the hardest thing in the repository to find. So: the list moved
verbatim to `docs/plans/playtest-queue.md` with a rule (a finished piece of work adds a row, a
verdict closes one, past about ten open rows the next session takes a fix or a measurement);
`docs/process.md` writes down the cycle that has actually been working, step by step with the
gate at each; `next-session-prompt.md` carries a superseded banner; `CLAUDE.md` is 441 lines.

**Two small things fixed on the way, and why they are recorded.** `build_wiki.py` used a nested
same-quote f-string that only Python 3.12 accepts, so the content gate could not run in this
container (3.11) — one line, and the floor is now stated. And `01-architecture.md` still named
assemblies (`Odyssey.Ui.Core`, `Ui.Unity`) that were built as `Odyssey.Hud` and
`Presentation/Ui`; a note says so rather than the document quietly being wrong.

**What was deliberately not done.** No code changed except the one-line tool fix. Nothing in HT
is started: the working agreement's phase gate says the plan waits, and this is the plan. The
job-scan risk (§2c) is read, not measured, and the audit says so; HT6 is the measurement, and the
listers it might justify are not designed until it has run.

- *Verified:* fast tier in the container **753 Sim + 449 Hud, 0 failed** (dotnet 8.0.131); both
  content gates clean after the f-string fix; `git diff HEAD -- ProjectSettings/ Assets/Settings/`
  empty. The Unity tier was not run here (no editor) — the last recorded run is 2026-09-19's
  1,872 / 82.

### Reviewed the same day, and six things it had missed

The audit was read back against the repository before it merged, which is the check its own §9
invites. Every load-bearing number held: the fast tier is **753 Sim + 449 Hud** exactly, both
content gates pass with the f-string fix, `OdysseyBootstrap` is 2,408 lines, `ChunkRenderer`
1,848, `HudLayout` 1,713, `ConstructionGrid` 1,650, `NavGraph` 1,517 with the three global passes
at 902/964/1003 where §0 says they are, `Odyssey.Sim.asmdef` does carry `noEngineReferences: true`,
the manifest has no Burst, Collections or Mathematics, there is no `.editorconfig` or
`Directory.Build.props` anywhere, `GridMirrorContributor`'s own doc comment says the renderer does
not read the slice channel, and no district id is saved or hashed — which is the claim **HT1**
rests on. The playtest queue is a byte-for-byte move of the 130 lines it replaced.

What it missed, all of it the same fault it was written to catch — a status line outliving what it
described:

- **Two track rows were stale.** TS said "in review — PR #126" (merged 2026-09-18) and CL said
  "PR #129 ready to merge" (merged 2026-09-19). The audit corrected two stale lines and walked past
  these.
- **The Unity tier line was two runs behind**: 1,857/1,843 was the alert-chimes run, while the
  journal's last entry against `main`'s tip records **1,872/1,858**. The audit's own §4f and §9
  carried the older number while the journal entry it shipped beside them carried the newer one.
- **The read-this table had the ladder line twice**, with two different descriptions of the same
  document. Now one row, the longer one.
- **Growing zones (PR #119) was nowhere.** The track table, the read-this table and the waiting
  list had no row for it, on `main` or on this branch — the text existed only as an uncommitted
  edit to `CLAUDE.md` in the Windows checkout, so the restructure would have buried it. It is now
  a track row, a read-this row and the first row of the playtest queue, which is where it belongs:
  it is the one row that blocks a merge.
- **The queue inherited `CLAUDE.md`'s gaps.** Five changes merged on 18–19 September have no row —
  pawn avoidance, diagonal movement, head turning, the flush cursor, the sight-fade exemptions —
  and four of them are looks. A *Not yet listed* table names them so the next session writes each
  row from the PR's own handover.
- **The ten-row rule was breached on the day it was written**, at 28 open rows. Left in place and
  said out loud, because the ceiling is the target; a rule quietly wrong on arrival is one the next
  session learns to ignore.

Small corrections beside those: `Assets/Editor/Odyssey` is 47 files, not 48; the unused-package
count is eighteen (ten packages, eight modules) rather than fifteen; `docs/setup/local-dev.md` §1
already states the Python floor, so HT2 owes only the `python3 --version` check; and the plan has
**nine** units, HT9 being a gap inherited rather than a finding made.

- *Verified:* fast tier on this branch, Windows, dotnet 8.0.425 — **753 Sim + 449 Hud, 0 failed**;
  both content gates clean. No Unity run: the numbers above are the journal's, not a fresh tier.

### The first five verdicts, 2026-09-20

The owner, on the *Not yet listed* table the day after it was written: *"gaze, flush cursor and
sight fade, avoidance and diagonals is all working now."* Five rows closed at once — head turning
and gaze (#128), the flush selection cursor (#127), the sight-fade exemptions (#123), soft crowd
avoidance and sub-tile steering (#134), and eight-directional movement with the strict corner rules
(#132). None of the five produced a fix, so there is no bug-patterns row and no design-doc
amendment; the *Judged* table records the date, the verdict and that the consequence was none.

Two things worth keeping from it. **The table earned its keep in a day** — those five were merged
on 18–19 September, had no row anywhere, and would have stayed unjudged because nothing was asking
about them; naming them was the whole of the work. And **a pass on the look is not a pass on tuning
invited by name**, which is why the closing note says so: the queue closes the question *does this
read right*, and a number a design doc still offers for tuning is a separate question that outlives
the verdict.

`23-head-turning-and-gaze.md` also gained the `CLAUDE.md` pointer it never had — a design document
with no row in the read-this table is one the next session does not find. `13-gestures.md` and
`15-skills.md` are still in that state and are not fixed here.

### Falling items and floor-drop motion, 2026-09-20

The owner, on loose items floating when ground or floors beneath them are destroyed: *"logs and items can appear in mid air - if the ground or floor beneath is destroyed, drop the object with a motion on to the floor below if possible? - or whatever you recommend"*.

**The design (`docs/design/26-falling-items.md`, branch `claude/falling-items`).**
Five questions resolved with the owner before any code was written:
1. **Simulation relocation is immediate; motion is presentation-only.** In the simulation (`Odyssey.Sim`), as soon as a floor slab is removed (`ConstructionGrid.RemoveSlab`) or a pawn deconstructs a floor (`DeconstructJob`), any items resting on that cell immediately relocate their `item.Cell` to the first solid floor directly below (`Cells.FirstFloorAtOrBelow`). If no floor exists anywhere below (over the bottomless void), the item despawns rather than hangs.
2. **Deconstruction salvage refunds drop directly to the landing floor.** When a floor slab or fixture is dismantled, salvage refunds spawned by `DeconstructJob` resolve `FirstFloorAtOrBelow` before finding space via `NearestCellWithSpace`, preventing materials from appearing in mid-air above an empty void.
3. **Pawn drop on deconstruction is distress-free.** A colonist deconstructing the floor beneath her own feet drops to the landing floor without thought (`Falling.NoThought`), matching the existing rule for digging out rock beneath oneself.
4. **Presentation tracks drops frame-to-frame with gravity acceleration.** `ItemFallingTracker` inspects `WorldSnapshot.Things` each frame. When an item's Y-coordinate drops between frames, an active fall record is registered. Downward offset accelerates quadratically ($t \propto \sqrt{h}$, quadratic easing $y(t) = h \cdot (1 - (t/T)^2)$, durations ~0.4s for 1 storey to ~0.85s for 4 storeys). When $t$ crosses $T$, `ItemLanded` fires, emitting `SoundIds.CarryDrop` at the touchdown coordinates.
5. **Renderer and Bootstrap integration.** `ChunkRenderer` offsets both `ItemHeap` props and individual commodity props during `RenderThings` by `worldFallOffset`. `OdysseyBootstrap` wires `FallingItems.ItemLanded` to `AudioSystem.PlayAt` and steps the tracker in `RenderWorld`.
6. **Safety sweep.** In addition to reactive drops on `RemoveSlab` and deconstruction, a safety check `Falling.DropFloatingItems(ctx)` sweeps orphaned floating items during world support consequence resolution, safeguarding against obscure mid-air item bugs.

**The trap:** In `ItemFallingTracker.Advance`, updating `_activeFalls[id] = fall;` inside a `foreach (var kvp in _activeFalls)` loop threw `InvalidOperationException: Collection was modified; enumeration operation may not execute` under Unity's Mono runtime. CoreCLR (.NET 8) in the fast tier had not thrown, and presentation is not tested in the fast tier anyway. Fixed cleanly by snapshotting keys into a reusable allocation-free scratch list `_activeKeys`.

- *Verified:*
  - Fast tier: **759 Sim + 449 Hud, 0 failed**; both content gates clean.
  - Unity EditMode: **1,916 total, 1,902 passed, 0 failed** (includes new tests in `ItemFallMotionTests`, `ItemFallingTrackerTests`, `FallingTests`, `FloorsAndCollapseTests`).
  - Unity PlayMode: **82 total, 77 passed, 0 failed**.

## 2026-09-20 — Events: the incident layer, and the storyteller that is deliberately not there

The owner asked for a world-event system aligned with RimWorld's — cadence types (regular, weekly,
ad hoc, condition-based; periodic or one-off), positive or negative by storyteller, rewards on
completion or the event as the reward, shown as an alert and in an activity log — and to start
with a meal dropping from the sky to be hauled. The result is `docs/design/23-events-and-storyteller.md`
and the `EV` track. The research is `docs/research/a-11-storyteller-incidents.md`.

**What the research settled.** The reference picks a category, then an incident by weight; its
storytellers are a difficulty curve plus independent generators on their own clocks; every
incident is a Def of gates plus a worker split into `CanFireNow` (cheap, side-effect-free) and
`TryExecute` (does it), so one worker serves the storyteller, a quest and the debug menu. Nothing
fires on a calendar date, which makes "weekly" the one owner term with no counterpart, and the
reason is design intent rather than omission: a schedulable event is one the player prepares for
perfectly. Reward-on-completion is a quest, a wrapper round incident verbs, not an incident.

**Four decisions, the owner's.** The display is a new Events panel under the alerts, not a row in
the alerts panel (the alert model rebuilds its rows from the frame every quarter second, which is
right for a condition and would wipe an event on the next refresh — the design catalogue had ruled
this in advance and was right). Firing is debug-only for now: no storyteller, but the gates are on
the Def and the ledger keeps the refire memory, so the scheduler reads rather than restructures.
The landing is anywhere on the board, uniform, which is the reference's own behaviour; I said what
it costs — most drops land out of sight and an unreachable one lies there — and named the one-line
knob if it maddens. The ledger lives in the sim now, saved and hashed; the History screen (F9,
reserved since M1) is the next unit.

**What was built.** `IncidentDef` and `IncidentContent` (a fourth content family, in
`ContentPack.Register`), `IncidentWorker` discovered by name the way work givers are,
`SupplyDropWorker`, `Skyfallers` (the flight is simulated: nothing exists until it lands, and a
mid-air save lands on the promised tick), `IncidentLedger` (append-only, monotonic ids, the tail
published), `Incidents` with `Attach` and the one door `TryFire`; `IntentKind.InvokeIncident`;
`CellGrid.SkyLanding`; `BulletinView` and `FallingView` channels; `BulletinModel` and the Events
panel; `FallArc` and `ChunkRenderer.RenderFalling`; `AudioDirector.StepLandings` for a sound the
catalogue does not yet hold. The debug row that stood disabled since 2026-09-17 fires it.

**The landing rule is the one that took thought.** `NearestWalkableInColumn` from the top would
search past a wall to the floor beside its foot and past deep water to the bed beneath it — the
two answers a drop "through open sky" must refuse — and `FirstFloorAtOrBelow` is happy to stop
under a ceiling. `SkyLanding` walks down to the first cell that is not open air and lands only if
that cell can be stood in; a rooftop slab qualifies, a wall, a lake and bare rock do not, and a
tree's cell does, because a tree blocks nothing and felled wood already lands there. Seven tests
pin it.

**What the hash saw.** All six golden numbers moved before a single tick ran: two new components
hash four zero integers. The control is that all three `Generated` values moved together,
including the barren meadow's, which no gameplay change has ever touched. Re-baked once, with
the paragraph in `Golden.cs`.

**Two things found on the way.** `AlertModel.IconKeys` said it existed "for the registry test"
from the day it was written and no such test existed; it does now, beside the incident one. And
the interface's key table for incidents cannot import the Def, so it is held to the Defs'
`bulletinKey` values by a test that reads the XML — the `CarryingAspect` bargain, made a second
time. `ui.bulletin.` joined the enforced namespaces without finding a duplicate literal.

**Owed.** The Unity tiers on this branch, the play day (design 23 §9, §10), the History screen,
a second incident to turn the debug row into a picker, and the storyteller when the owner wants
events that arrive unasked. The catalogue row for `odyssey.sound.drop.land` is the owner's, in the
editor; the director declines it silently until then.

## 2026-09-20 — Events, the first look: four things moved the same day

The owner pressed Play on the supply drop the afternoon it was built, and four things came back.
None was a fault in the incident layer; all four were the drawing, the sound and the menu around
it, which is where a first look usually lands.

**The fall was over before it was seen.** Two seconds from six metres above the top of the world,
gathering speed like a stone — a physically honest arc that entered the frame part-way down and
landed almost at once. Two changes. The duration is the Def's and is now six seconds (`fallTicks`
360); the fingerprint moved and was re-baked with the reason. The start height is presentation's:
`FallArc.DropHeight`, 120 m over the landing floor, which is above the play camera at any zoom
(32–160 m up, looking down at 48°), so the thing enters from beyond the top of the frame rather
than popping in. And the curve is a straight line now — a crate under a chute comes down at one
speed — because the square law that reads as a real fall when the fall is short reads as a thing
loitering and then dropping when it is six seconds long. `FallArcTests` was rewritten to say
this: equal steps, the same start over a roof as over the meadow, and a world taller than 120 m
still starting above its own top. The "second half covers more ground" test went with the arc.

**The Events row moved the depth.** Clicking a row moved the slice to the event's layer, selected
the cell and jumped the camera. The owner expected the camera to move and nothing else — the cut
away and the selection are theirs, and a panel that changes them is a panel that surprises. The
row jumps to the event's column at the active layer now and does nothing else. The cost is a
rooftop drop looked at from below the roof: the pad's column is where the camera goes, and the
slice is yours to raise. Design 23 §5 states it.

**Events want a tab of their own.** The debug menu had one flat list with an *Invoke event* row
at the bottom, and the owner asked for a second tab. `DebugDirector` gained a `Tab` (opens on
Cheats, because the overlay toggle is the row backtick was bound to for a day) and a `TabChanged`
event, in Settings' tab idiom so a third tab strip in this shell invents nothing. The Events tab
is built when the panel opens, from the open colony's `IncidentContent`, one row per Def named
through `IncidentLabels` and tooltipped by the Def's own `description` — so a second Def appears
by existing and `HudShell.Debug.cs` never learns its name, which is what design 18 had promised
the day a picker was wanted. The `ui.debug.invokeevent` key is gone; `ui.debug.tab.cheats` and
`ui.debug.tab.events` replace it, and `DebugDirector.IconKeys` is now held to the registry by
`EveryDebugKeyIsARegisteredName`, beside the alert and incident checks.

**The chime snapped off.** "The sound snaps to silence and the music switches back on." Two
causes, both in `AudioDirector`, neither in the recordings. The duck's release was its attack —
0.15 s down, 0.15 s back — and a tenth of a second is right for carving a chime's space and wrong
for handing it back; it is 1.0 s now, and the music swells rather than switches. And the clip
stopped dead: `StepChimeTails` fades the last 0.4 s of every voice on the Alerts bus, from the
gain it was played at, so a fader move during the tail is applied on top rather than fought.
Alerts only, because a chop or a pick is a transient and is meant to stop dead. The busy clock
already knew where each voice's end was (`_busyUntil` is arithmetic, not `isPlaying`), so the
tail costs a subtraction per voice per frame and works in edit mode and in a test. Two tests pin
it, measured at a fifth of a second after the duck ends and part-way into the tail.

**What did not change.** The landing rule, the ledger, the hash, the goldens: none of the four
touched a tick. The one Def edit moved the content fingerprint and nothing else, which is what a
fingerprint is for.

**Owed.** The Unity tiers on this round, the second look (design 23 §9: chute or lift, waited for
or not, the swell back), the History screen, and the storyteller when the owner wants events
that arrive unasked.

## 2026-09-20 — Events reviewed for the next kind: what a raid costs

The owner called the supply drop good enough for an MVP event system and asked for the PR to
be reviewed against the events to come — raids, encounters — so that adding one is a matter of
adding rather than restructuring. The review is design 23 §8, "Adding an incident: the recipe".

**The verdict.** Five edits, every one of them caught by the fast tier if missed: a worker
that joins by existing, a Def, a line in `Order`, a handle and a label in the same position,
and a registry key. The Events panel, the chime and the debug tab follow from the key and the
Def. The seams a storyteller needs — gates on the Def, `LastFiredTick` and `Fires` on the
ledger, `CanFire` / `TryFire` as the one door, `Points` on the parms, a category enum with the
threat, arrival and condition bags already named — are all present and unread, which is the
right state for them.

**One thing changed.** The content loader validated the supply drop's fields — stack range,
fall time — for every incident, so the first raid Def would have been held to rules about
falling meals. `IncidentWorker.Validate(def, pawns)` is a virtual hook now, the loader checks
only what every incident has (a key, a worker, an item the content carries), and
`SupplyDropWorker` owns its three rules and adds a fourth: a drop that pays out nothing is a
content error, not a silent no-op. Three tests, one of them a bare worker in the test
assembly proving a Def with none of the drop's fields loads.

**What was left narrow on purpose**, and written down so nobody reads it as the design: the
flat `IncidentDef`, which becomes per-worker nested blocks the day a second worker wants
parameters the first does not (the loader already reads them); `InvokeIncident` carrying the
Def index only, though the parms take a cell and a budget; and the four-field ledger entry,
which a raid's outcome or a condition's end will widen with a save-format bump. A condition is
the one kind of event the layer does not represent at all — a span, not a firing — and is the
first structural addition the next kind will ask for.

**What a raid needs that events should not provide:** a faction and a hostility model, a
non-colonist pawn kind, an arrival edge, combat and health. The incident is the thing that
asks for them at a moment; they are their own units.
## 2026-09-20 — four reports from one play session: beds, loading, and the colour of an order

Four owner reports in one message, on `claude/bed-assign-and-order-colours`. Three turned out to
share nothing; the two bed reports turned out to share a cause.

### The phantom bed cells, which were two bugs wearing one coat

> *"Some colonists still sleep off the bed … it looks like it's trying to rest them in the first
> tile in some circumstances where they are hanging off the bed."*

> *"When I assigned a bed to a colonist and they are asleep — I expect them to get up immediately
> and get into the bed they have been assigned to."*

**The first one nearly cost a session, because reading the code said it was fine.** `SleepPose`
lays a body from the pillow along the bed's own facing; the arithmetic puts a 1.8 m colonist
between −1.55 m and +0.26 m of a bed spanning ±2.30 m, which is comfortably on it. `AimSleep` runs
every frame, so nothing is stale. `GotoCell` requires exact arrival, so a sleeper cannot stop one
cell short. Every reading said the sleeper was on the bed.

So it was measured instead — three colonists, three built beds, three days, counting the ticks
each spent asleep on a bed cell and off one. With the fixture the tests use (`scenario.beds = 0`)
every sleep was on a bed. With the number the **game** actually ships (`ScenarioDef.beds = 5`) one
colonist spent **all 53,222** of her sleeping ticks off a bed and a second **17,399** of hers, and
every off-bed cell was **one to three cells from a real bed she never used**.

`ColonyScenario` had been putting five entries into `ColonyItems.Beds` that were *cells and nothing
else* — no edifice, no record. That was right when it was written: a bed was then a property of a
cell and there was nothing to build. It became a lie the day beds became furniture, because
everything a bed now is hangs off the record. The cell cannot be seen. It cannot be **owned**
(`AssignOwnerAt` refuses a cell with no edifice), so §7 and §8 of `20-beds.md` — the pane, the
popover, the whole assignment feature — were dead on every bed the colony woke up with. And a
colonist who "sleeps in it" gets the **ground** pose, flat on the grass along her last yaw, which
beside a real bed is a colonist hanging off it.

One fix: a starting bed is a real bed, raised through the construction grid, and `Raise` adds the
head cell to the list itself — one owner for "what counts as a bed". Two things the placement
needed, both found by tests rather than by thinking:

- **Eight footprints per spot, not four.** A bed is wider than the spot it is given, and the spot
  can be its head *or* its foot. Four facings left the ruined city three beds short of five,
  because a storey there is rooms and two spots had no free neighbour in the direction a head
  needed.
- **Keep off cells promised to another group.** Every storey is searched up front, so the
  stockpile's cells are chosen before any bed is raised and adding the bed's far cell to the taken
  set would be too late. `Storeys.Spoken` lets the bed ask instead. Not cosmetic: a bed claims its
  cells against items, so a stockpile cell under a bed's foot is a cell nothing can ever be put in.
  `ForbidIntentTests` caught it, reporting it as "forbidding is broken" — the colony was hauling
  perfectly and had filled its last three free cells with rations.

**The assignment half** is `JobSystem.GetOutOfTheWrongBed`, and the first draft of it was wrong
twice in ways worth keeping:

1. **Naming the colonists involved does not work.** The obvious version lists the bed's old owner
   and its new one. It misses the commonest case there is — a colony short of beds keeps them
   unowned and shared, so the colonist actually *lying in* the bed when the player gives it away is
   very often nobody's owner and is in no such list. `ASleeperWhoseBedIsGivenAwayGetsUp` failed on
   exactly that. Who is affected is a question about where people are sleeping, which the
   construction grid does not know; it raises a flag and the job system sweeps.
2. **Acting on the change rather than on the bed loops.** A sleeper claims an unowned bed the
   moment she arrives, through the same door a player's assignment uses. Waking on the change would
   get her up, walk her to the bed she is already in, and repeat for ever.

So the rule is *is she asleep somewhere that is not hers?*, and the negative control
(`ASleeperWhoClaimsTheBedSheIsLyingInIsNotWokenByHerOwnClaim`) is the test that matters. Full
account in `20-beds.md` §7a.

### Load with nothing to load

> *"I notice when you try a load a game in game and there is none available — you can end up losing
> your current game as it goes back to the main menu."*

The in-game Load row tears the colony down first, on purpose — the player watches the panel close,
the world go, the list arrive, which is Quit to main menu followed by Load and was built to look
like it. It never asked whether the list would have anything on it. It asks now, before anything
irreversible, and with nothing readable in the folder the colony is untouched and the row says
**No saved colonies** until the next press.

Recorded as still open: backing out of the load screen *after* going there still loses the colony,
because the teardown has already happened. Fixing that means showing the browser over a live
session, and the menu is currently tied to there being no session at all. That is a restructure of
the start screen's modality rather than a guard, and it wants its own round. `17-start-flow.md` §5b.

### One colour per order, and the question that had to be asked

> *"The deconstruct order when placed puts down an entire square as the blueprint to deconstruct,
> the placement shouldn't be red … make it mark the tile for deconstruction instead like you would
> mark in mining. Also match the orders blueprints/placement titles to the color assigned on their
> toolbar. Clarify this with me."*

Two mappings of tool to colour, in two assemblies, written months apart, disagreeing on two of the
four tools. Deconstruct was orange on the palette chip and **red** on the board — the interface's
own colour for *cancel* — so while the player held the deconstruct tool the panel and the cursor
said different things. A **P1**, and the plainest one in the register.

Nothing caught it because the board's copy lives in `Odyssey.Presentation`, which the fast tier does
not compile, and the only assertion on it was one of *totality*: every kind maps to something,
which a wrong colour satisfies perfectly. The mapping is `Odyssey.Hud.OrderColours` now — Unity-free,
so `OrderColoursTests` runs in the fast tier and asserts that the chip, the drag cursor and the
board mark are one hue per tool.

**The clarification was needed and the answer was not derivable.** Giving deconstruct the orange
its chip already had would have put it within a few points of mine's warm amber on the same board.
Three options were offered with the cost of each stated; the owner chose **the toolbar wins**, so
mine's board mark becomes its chip's blue and loses the warmth that had been chosen for standing
out against cool stone. That was a real reason and it lost to a better one. Mine's hue is the one
value here that is not an existing signal token, because `HudTheme.Info` is within a few points of
`Accent` — what a pending *build* is marked in — and a colony half dug and half planned would have
been two blues nobody could separate. Deeper and bluer, on **both** surfaces so the chip and the
mark still match, and `NoTwoOrdersLookAlikeOnTheBoard` pins the distance so somebody tuning `Info`
for a tooltip learns in two seconds.

**And the first pick of that blue was wrong in a way the test caught on itself.** `#5fb2d8` looked
deeper and bluer and measured at *exactly* 60 channel-points from the accent — it passed a
60-point threshold by sitting on it, for the one pair the threshold existed to police. A blue
reads as cyan when its green is near its blue, so the separation had to come out of green:
`#4a90c8`, 144 against the accent's 211, 131 points away. The threshold is 80 now, which is the
most the existing palette clears (orange against red is the closest pair at 83). A number chosen
to let the current values through is a number that asserts nothing, and it took measuring all ten
pairs to notice this one had become that.

**The shape half was simpler than §6a made it.** That section was right that a floor plate under a
wall is inside the wall, and reached for a different shape — a whole-cell wash. The answer was the
same shape at the right height, which is what a mine order already does to rock.
`WorldRenderModel.MarkHeight` is the one rule now: the top of the cell for anything that fills it,
the top of itself for anything that stands up without filling it (a bed), the floor for everything
else. Trees stay on the floor deliberately — a fell order is read on the ground the tree stands in.
`DrawCellShade` is deleted. `16-cancel-and-deconstruct.md` §6b.

### And a control that found a third thing

The owner added, mid-session: *"make sure saved games save orders assigned as well."* Every order
did round-trip — designation and its progress, blueprint with material and deliveries, bed owner,
zone, forbidding, work priorities — so the six new tests all passed first time. The **seventh**,
the control, did not.

`ColonyItems` hashed its things and not its stockpile zones or its bed list, both of which it had
been *saving* since they existed. That is the worse way round: `WorldRoundTripTests` proves a save
by comparing hashes, so a zone whose filter failed to round-trip would have come back accepting
everything and passed. OQ-50's shape exactly, and found the same way — flip one bit of a filter and
ask whether the number moved. It did not.

Both lists are hashed now. `OrdersSurviveASaveTests.EachOrderMovesTheStateHash` walks all eight
kinds of player order, and it is worth more than the six round-trips above it: those can only cover
the orders somebody thought to name.

### The goldens, re-baked twice in one commit

Every `Generated` and `Simulated` on all three cases moved, and it happened in two steps for two
reasons — the beds becoming records, then the zones entering the hash. Both are the hash **seeing
more**, not the colony doing anything different, and the evidence for that is specific rather than
asserted: the placement signature on both maps kept its part count at 34 either side of the bed
change, so the colony is the same colony with the same things in it and only the beds are
somewhere else. No generator pass changed.

`ScenarioDefTests.AScenarioThatNamesNoStoreyPlacesExactlyWhereItAlwaysDid` was re-baked too, and its
promise is narrower now than when it was written: it promised the storey work moved nothing, and a
deliberate content change has moved it. Said so in the test rather than quietly replacing the
number.

### And the bed change found a fourth thing, which is why a write is a probe

The starting beds were the first thing in the project ever to **write to the chunk grid during a
world build**. Three PlayMode tests threw `IndexOutOfRangeException` out of `ChunkGrid.MarkDirty`
the moment they did — and the new code was not wrong.

`OdysseyBootstrap.BuildSession` built the chunk grid and the render model from the inspector's
`new GridSize(sizeX, sizeZ, layers)`, and built the world from `sizeOverride ?? size`, the setup
page's. A new game on any board but the scene's default therefore had a mirror and a chunk grid of
one size over a world of another, and every cell index near the far edge fell outside them. A
plain P1 — one number, two owners — with the extra property that it was **latent behind a write
nobody had made**. The fast tier could not have seen it at any point: `PawnContext.Chunks` is null
headless, so `MarkChunksAround` returns on its first line, and 769 Sim tests were green the whole
time it was live.

Two halves to the fix, because either alone leaves the trap set. The size is decided once, before
the chunk grid and the mirror are built from it. And `MarkChunksAround` asks the grid it is about
to write into rather than the one beside it — it had been checking `ctx.Size`, the *cell* grid,
one line above a write to the *chunk* grid, which is why the guard passed and the array did not.

The lesson is about diagnosis rather than about sizes. A new write into a structure nothing wrote
to before is a probe: when it fails, suspect the structure's provenance before the write, because
the write is usually correct and merely first.

### What the tiers said in the end

Fast **769 Sim + 455 Hud**, Long **21**, EditMode **1,931 total, 1,918 passed, 0 failed**,
PlayMode **82 total, 77 passed, 0 failed**. Both content gates pass unchanged — the one new piece
of player-facing text on the Load row reuses `ui.start.empty`, which the registry already had.

**PlayMode earned its place in this round.** It is slow enough to be tempting to skip, and it is
the only tier that caught the board-size fault: the fast tier cannot, because `PawnContext.Chunks`
is null headless, and EditMode did not, because nothing there builds a session through
`OdysseyBootstrap`. Three tests failed, all in `StartScreenTests`, all with the same stack, and
the new code in the stack was not the wrong code.

## 2026-09-20 — The second play day: woken for a bed, and sent to work

The branch's first play day had put the bed rule in front of the owner, and the report back was
one line: *"the issue here is when I assigned someone else to a bed — everyone just started going
back to work — the rest of the fixes seemed fine."*

**Reproduced before it was read**, in two tests, because the last three days have taught that
reading this code says it is correct. The first is the report in its simplest shape: one colonist,
two beds, asleep in the near one, given the far one after half a night. The second is the colony
the owner actually played — three colonists, five starting beds, each claimed on the first night —
with one sleeper's bed given to another at night. Both failed on the same line: the woken colonist
was on a **work** job the tick after the assignment. The third colonist, whose bed had not
changed, slept on; so "everyone" was the two people concerned, which in a colony of three it is.

**The cause was the hand-over, not the sweep.** `GetOutOfTheWrongBed` ended the right sleeps and
then left each colonist to the think tree, and the tree's sleep branch is gated on rest below the
`seekThreshold` of 280 — the question *should she start sleeping?* A sleeper wakes at 950, so a
colonist got up at 600 was, by that gate, not tired, and the work branch took her. Every one of
the five existing tests had assigned the bed within a tick of her lying down, at the rest of 40 the
helper sets, and so never crossed the 670-point gap between the two thresholds. A test that probes
a range at one point proves the rule at that point.

**The fix is that an interrupted sleep resumes.** The sweep now runs in two passes — every
affected sleep ends first, so every claim is released, and then each woken colonist goes straight
back through `TrySleep`, past the gate, because she was asleep and the only question is where.
Two passes rather than one because the bed the player just gave B is the bed A is still lying in:
choose in the same pass and whether B gets her own bed or the nearest spare depends on which of the
two the colony list holds first, which is the kind of order-dependence that passes every test
until the day it does not. `TrySleep` became public for it; the scratch list of the woken is
cleared before and after every use and is neither saved nor hashed.

Fast tier **777 Sim + 455 Hud**, Long **21**. The goldens did not move: no golden run assigns a
bed, and an arrival claim on one's own bed still falls through the sweep's first case. Unity could
not be run from this session — the owner's editor is open on this worktree — so the Unity tier
is the pull request's to prove.

Also this session: `origin/main` had taken the falling-items branch (PR #138), and this branch
conflicted with it only in the three documents both had appended to. Both journal entries and both
playtest-queue rows were kept, and the tier line keeps this branch's counts, re-run after the merge.

## 2026-09-20 — The second merge of the day, and no beds at the start

`main` took the events layer (PR #140) between this branch's push and its review, and the pull
request conflicted again: the two documents both branches append to, and **all six golden hashes**.
The goldens were baked afresh from the merged code rather than taken from either side, because both
branches had moved every number for their own reasons — beds and zones entering the hash here, the
incident layer's state on `main` — and neither side's value was a number the merged code had ever
produced. `Generated` moved on all three cases because the events layer hashes before the first
tick, as it already did on `main`.

**And the owner ruled out starting beds:** *"beds should never be given on startup / new game — but
things seem to work fine."* The five real beds this branch had raised at tick zero were, from the
player's side, five beds the colony had never had — the phantom cells they replaced were invisible.
`ScenarioDef.Playtest` now has `beds = 0`; the colonists sleep on the ground and take the thought
for it until a bed is built, which is what makes a bed the first thing worth building. `Bare`
keeps its five, so no test, golden or ten-day run changed under it, because none of them
builds on `Playtest`. Everything §7a says about a starting bed
being a *real* bed still holds for the scenario that has some.

Fast tier **806 Sim + 471 Hud**, Long **21**. Unity not run here: the editor is open on this worktree.

## 2026-09-20 — Functional doors and room enclosure: sliding leaves, doorway traversal and sealed interiors

Functional, buildable auto-sliding doors and strict room enclosure are in. A doorway connects or seals
spaces, animates smoothly on pawn passage, and establishes indoor room environments.

**The simulation half: building, traversal, and lifecycle.**
- **Building handle & content:** `BuildingHandle.Door = 6` (`Count = 7`), registered under
  `Buildings.xml` and `ConstructionContent.cs` (edifice: `CoreContent.EdificeDoor` (2), cost 5 Wood/Stone,
  work 135). Buildable through `ui.arch.tool.door` on any standable cell with a floor below.
- **Traversal & door lifecycle:** `DoorSystem` ticks at order 35 in `TickPhase.Pawns`. When a pawn approaches
  or occupies a door cell, `DoorSystem` holds the door open. Traversal charges `MoveCost.DoorOpening`
  (already defined in `NavGrid`) when closed. Once the doorway and approach cells are clear, the door
  auto-closes after a 30-tick timeout.
- **Strict room enclosure solver:** `EnclosureGrid` implements a per-layer flood fill bounded horizontally
  by `EdificeWall`, `EdificeDoor`, or solid rock. Enclosure strictly requires 100% overhead roofs
  (either solid rock or a built floor slab on layer `y + 1`). Reaching the map edge or exceeding 2,500 cells
  marks the area outdoors. Dirty tracking wires into wall/door construction, mining, and collapse.
- **Inspect pane:** `InspectModel` displays `environment: indoors` on cell inspection when `IsIndoors` is true.

**The presentation half: frame, sliding leaf, and audio.**
- **Frame and leaf split:** The doorway frame (`SM_Bld_Base_Wall_Door_01`) is emitted into chunk batches via
  `ChunkMesher.EmitDoor`. The sliding leaf (`SM_Prop_Door_01`) is rendered dynamically via `DoorDirector`
  using `Graphics.RenderMeshInstanced` so chunk batches do not remesh during animation.
- **Lateral sliding animation:** When pawns approach within 1.6 m or pass through, `DoorDirector` smoothly
  slides the door leaf laterally into the wall frame pocket over 0.2 s.
- **Audio:** Transitions trigger `SoundIds.DoorOpen` and `SoundIds.DoorClose` on the world audio bus.
- **Orientation ownership:** `WorldRenderModel.DoorFacing(x, z, y)` owns doorway orientation across both
  `ChunkMesher` and `DoorDirector`, ensuring the leaf and frame never diverge.

**The golden master.**
Ruined city maps stamp `EdificeDoor` edifices. Previously, these edifices never had `NavFlags.Door` registered.
With `RebuildDoors` active on startup, ruined city doorways now charge opening cost and tick through `DoorSystem`.
The meadow baseline maps have no doors.

**Verification:**
Fast tier 822 Sim + 473 Hud; EditMode 2,024 / 2,011 / 0; PlayMode 82 / 77 / 0. Wiki and registry checks green.

## 2026-09-20 — Door wall alignment and colonist clearance: flush face placement and tall Base doors

The initial door implementation placed the frame and sliding leaf at `CellMetrics.FloorCentre(x, z, y)`.
Because walls in Odyssey are placed along cell boundary faces (`CellMetrics.FaceCentre(x, z, y, dir)`),
doors were recessed 1.25 m into the cell, creating a visible gap and misalignment with adjoining wall
panels. Furthermore, colonist figures with hats measure between 2.05 m and 2.20 m tall, clipping the
1.97 m opening of `SM_Bld_Base_Wall_Door_01`.

**Flush wall alignment and orientation:**
- **Face placement:** `ChunkMesher.EmitDoor` and `DoorDirector` now compute their transform anchors via
  `CellMetrics.FaceCentre(x, z, y, dir)`. The door frame now sits coplanar with neighbouring wall panels,
  forming a continuous wall line.
- **Exterior facade facing:** `WorldRenderModel.DoorFacing(x, z, y)` now evaluates `IsRoofed` on adjacent
  open sides. When dividing an interior room from an exterior space, the door frame automatically faces
  the outdoor facade.
- **Proximity and audio origin:** Pawns triggering door traversal and the positional open/close audio
  now calculate distance against the face center rather than cell center.

**Colonist clearance and asset consistency:**
- **Large Base door frame:** Swapped from `SM_Bld_Base_Wall_Door_01` to `SM_Bld_Base_Wall_Door_Large_01`
  (2.47 m clear opening height), providing ample clearance for all colonists and headgear without clipping.
- **Matching Base sliding leaf:** Swapped the temporary sci-fi prop door leaf for `SM_Bld_Base_Door_Large_01`
  (1.13 m wide, 2.47 m high), maintaining aesthetic consistency with the Base wall theme.
- **Slide distance:** Increased `DoorDirector.SlideDistance` from 1.05 m to 1.15 m to fully clear the
  wider opening into the wall pocket.

**Verification:**
Fast tier 822 Sim + 473 Hud; EditMode 2,025 / 2,012 / 0; PlayMode 82 / 77 / 0. Wiki and registry checks green.

