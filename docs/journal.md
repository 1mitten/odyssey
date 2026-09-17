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
