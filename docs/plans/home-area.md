# Plan — the home area, the Home view and the Assign tab

**Phase 3, 2026-09-25.** Interview: `docs/research/home-area-interview.md` (every recommendation
taken, 2026-09-24). Design: `docs/design/43-home-area.md`. Brief: `docs/reference/mockups/home-area-brief.md`.
**Approved 2026-09-25** (owner: *"approved start"*). H1 and H2 built; H3 and H4 wait on the brief.
All units land on `claude/sleepy-cannon-9d0evw` as separate commits, because this session may push
only there.

## Units

Each unit is its own `claude/home-*` branch and PR, merged in this order. H1 and H2 need only design
43 approved; H3 and H4 take their constants from the files Claude Design returns.

| Unit | What | Where the decision lives | Gate |
|---|---|---|---|
| **H1** the mask | `ColonyFootprint` (dirty layers, `Touch`) and `HomeArea` (`SeedLayer`, five-cell square growth per layer, one layer of margin, lazy rebuild, `Contains`, `IsEmpty`, `Version`, `CellsOn`, `EdgesOf`). `Touch` at every placement path in design 43 §3a. A `Home` seam on `PawnContext`; `RebuildDerived` marks all dirty. Neither saved nor hashed. | §3 | fast tier; `WhatOneHomeRebuildCosts` on three boards; no golden moves |
| **H2** the setting and the gate | `Pawn.Area`, `AssignSection` (`odyssey.assign`), hash bit 26, aspect `odyssey.pawn.area`, intent `SetPawnArea` (paused, checked). `PawnContext.CanTravel` / `MayWork` split; the fight and the walk toil move to `CanTravel`; the open-ground haul fallback asks `MayWork`. The walk home in the idle node. A new setting ends an outside job. | §4 | fast tier; Long tier with one colonist at Home; lockstep twin; no golden moves |
| **H3** the view | `OverlayDirector.HomeVisible`, `HudViews.Home`; the Menu row generalised off `HudViews.Keys` and `PowerOverlayKey` deleted; the `WatchHome` channel publishing edge bits; the draw pass in the look the brief returns, inside `FrameSection.Overlays`; the house glyph from the brief's SVG path. Registry row `ui.overlay.home`. | §5 | fast tier; Unity tiers; `TheHomeViewAgainstTheFrame`; player build smoke |
| **H4** the Assign tab | `AssignDirector`, `AssignLayout` (the brief's constants), `AssignModel` (colonists only, Area and Response cycling, twelve a page); F4 in `HudKey`, `HotkeyAction.AssignTab` appended, `Defaults`, `HotkeyUnity`, the Settings keys page; `HudShell.Assign.cs` via `DockedTab`; the four sibling tab handlers close Assign. Registry rows `ui.tab.assign`, `ui.keys.assign`, `ui.assign.*`. | §6 | fast tier; Unity tiers; `DockedTabGeometryTests` row |
| **H5** close | Wiki and label registry rebuilt with all three content gates; `docs/wiki/artifact.html` republished; the measurements into design 43 §7 with machine and date; `CLAUDE.md` status row; a playtest-queue row; journal. | — | the three content gates |

## Files, by unit

- **H1** (built 2026-09-25): added `Sim/World/ColonyFootprint.cs` (held by `CellGrid.Footprint`),
  `Sim/World/HomeArea.cs`, `Tests/Sim/HomeAreaTests.cs`; modified `Sim/World/CellGrid.cs`,
  `Sim/World/ZoneGrid.cs`, `Sim/World/SupportSolver.cs` (a collapsed floor of ours),
  `Sim/Construction/ConstructionGrid.cs`, `Sim/Power/PowerGrid.cs`, `Sim/Storage/StorageZones.cs`,
  `Sim/Growing/GrowingZones.cs`, `Sim/Pawns/PawnContext.cs`, `Sim/Pawns/ColonyComposition.cs`,
  `Sim/Pawns/ColonyWorld.cs`, `Tests/Sim/TickBenchmarkTests.cs`.
- **H2**: add `Sim/Pawns/PawnArea.cs`, `Sim/Saving/AssignSection.cs`, `Sim/Pawns/AreaAspects.cs`,
  `Sim/Pawns/JobSystem.Area.cs`, `Tests/Sim/PawnAreaTests.cs`; modify `Sim/Pawns/Pawn.cs`,
  `Sim.Contracts/Intents.cs`, `Sim/Pawns/PawnRegistry.cs`, `Sim/Pawns/PawnContext.cs`,
  `Sim/Pawns/JobSystem.cs`, `Sim/Pawns/Job.cs`, the combat files that ask `Reachable`
  (`CombatThinkNodes.cs`, `Melee.cs`, `AttackMeleeJobDriver.cs`, `FleeJobDriver.cs`),
  `Sim/Pawns/ColonyComposition.cs`, `Sim/Pawns/ColonyWorld.cs` (`SaveComponents`, appended),
  `Tests/Sim/ForcedOrderTests.cs`.
- **H3**: modify `Sim.Contracts/Intents.cs`, `Sim.Contracts/Views.cs`, `Sim/WorldViewStore.cs`,
  `Sim/SimWorld.cs`, `Hud/OverlayDirector.cs`, `Hud/HudViews.cs`, `Presentation/Ui/HudShell.Bar.cs`,
  `Presentation/Ui/HudShell.Orders.cs`, `Presentation/Ui/HudGlyph.cs`,
  `Presentation/Bootstrap/OdysseyBootstrap.cs`; add `Sim/Home/HomeAreaContributor.cs`,
  `Presentation/Rendering/HomeAreaPass.cs` (if the look is an edge) and `HomeLook`, tests
  `Tests/Hud/HomeViewTests.cs`, `Tests/Sim/WatchHomeTests.cs`, a `FrameTimeTests` arm.
- **H4**: add `Hud/AssignDirector.cs`, `Hud/AssignModel.cs`, `Hud/AreaAspectNames.cs`,
  `Presentation/Ui/HudShell.Assign.cs`, `Tests/Hud/AssignModelTests.cs`; modify `Hud/HudDirectors.cs`,
  `Hud/HudCommands.cs`, `Hud/HotkeyDirector.cs`, `Hud/SettingsLayout.cs`,
  `Presentation/Bootstrap/HotkeyUnity.cs`, `Presentation/Ui/HudShell.cs`, `HudShell.Bar.cs`, the four
  sibling tab partials, `Tests/Hud/HotkeyClashTests.cs`, `Tests/Hud/HotkeyDirectorTests.cs`,
  `Tests/PlayMode/DockedTabGeometryTests.cs`.

## Risks

- **A placement path that forgets `Touch`** is a home that silently fails to grow. Design 43 §3a is
  the checklist and `HomeAreaTests` covers each source once.
- **A combat call left on `Reachable`** would stop a Home colonist defending herself at the edge of
  home. H2 moves every one and `PawnAreaTests` covers self-defence and Flee at Home.
- **The fast tier compiles neither Presentation nor Editor**, so H3 and H4 are unproven until Unity
  compiles them.
- **The runner has no `Assets/Synty`**: the tab's portraits, if any, ask
  `PawnFigureDirector.CanDrawColonists`.
- **The playtest queue is over its ceiling** (process §4). This line adds one row, at H5, and nothing
  before.
