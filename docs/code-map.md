# The code map — where things live, how the tick and the frame run, and how to add one of each

**For the session that has to change the code.** `CLAUDE.md` says what is true now and
`docs/design/01-architecture.md` says what the architecture is; this page says **where each kind
of thing goes, in what order it runs, and what will catch you if you put it in the wrong place.**
Everything here was read off the code on 2026-09-26 (`main` at `3e6b0007`), and the numbers are
that day's. When the code and this page disagree, the code is right and this page is a bug: fix
it in the same commit (`docs/audit/2026-09-26-architecture-review.md` §4 is why it exists).

The one-line version: **the simulation is a list of registered systems that mutate a world inside
a tick; the interface is a list of directors that read a published snapshot once a frame; the two
touch only through the snapshot (out) and the intent bus (in).** Every recipe below is "find the
list, add a row to it".

## 1. The four layers and the arrows

```
Odyssey.Sim.Contracts   4,918 lines   the seam: views, intents, handles, catalogue, hash     no UnityEngine
Odyssey.Sim            55,877 lines   the world, the systems, the pawns, saving, worldgen     no UnityEngine
Odyssey.Hud            36,305 lines   models and directors: what the interface shows/decides  no UnityEngine
Odyssey.Presentation   81,091 lines   rendering, camera, audio, the UI Toolkit shell          UnityEngine + URP
Odyssey.Editor         21,818 lines   scene builders, importers, 53 probes and contact sheets  UnityEditor
```

Arrows, enforced by the asmdefs and by the dotnet mirror projects (`tools/dotnet/`):
`Presentation → Hud → Sim.Contracts ← Sim`. **`Hud` never references `Sim`**, which is what lets
the whole interface model run in the fast tier without Unity. `Sim` and `Sim.Contracts` carry
`noEngineReferences: true`, and a reflection test holds them to it.

Tests sit beside each layer: `Tests/Sim` (204 files), `Tests/Hud` (102), `Presentation/Tests`
(147, EditMode, needs Unity), `Tests/PlayMode` (41). The fast tier (`scripts/test-fast.sh`) runs
the first two through the mirror projects; the Unity tier runs everything.

## 2. The tick, as built

`SimWorld.Tick()` (`Sim/SimWorld.cs`) runs seven segments in a fixed order, and the order is part
of the determinism contract (`TickSegment`: Intents, WorldSystems, Things, Pawns, Deferred,
Snapshot, Hash). Systems live in two open phases and are sorted **once, at construction**, by
`(Order, Name)` — so the sequence of `AddSystem` calls in the composition cannot change behaviour.

| Phase | Order | System | File | Scales with |
|---|---|---|---|---|
| WorldSystems | 10 | `Support` | `Sim/World/SupportSystem.cs` | dirty cells |
| WorldSystems | 20 | `Navigation` | `Sim/Pathing/NavigationSystem.cs` | dirty blocks (HT1 makes districts local) |
| WorldSystems | 25 | `trapped-pawns` | `Sim/Pawns/TrappedPawnSystem.cs` | pawns |
| WorldSystems | 30 | `Enclosure` | `Sim/World/EnclosureGrid.cs` | dirty rooms |
| WorldSystems | 35 | `Weather` | `Sim/Weather/WeatherSystem.cs` | constant (one roll per spell) |
| WorldSystems | 40 | `growing` | `Sim/Growing/PlantGrowthSystem.cs` | crops |
| WorldSystems | 45 | `Power` | `Sim/Power/PowerGrid.cs` | nets, lazily |
| WorldSystems | 50 | `Temperature` | `Sim/Temperature/TemperatureSystem.cs` | rooms, every 120 ticks |
| Pawns | −1000 | `StartingSkills` | `Sim/Pawns/StartingSkillsSystem.cs` | new pawns |
| Pawns | 10 | `Needs` | `Sim/Pawns/NeedsSystem.cs` | pawns, on a cadence |
| Pawns | 15 | `Raids` | `Sim/Events/Raids/RaidSystem.cs` | bands (hashed only while one exists) |
| Pawns | 20 | `Jobs` | `Sim/Pawns/JobSystem.cs` | pawns × givers |
| Pawns | 25 | `Combat` | `Sim/Pawns/Combat/CombatSystem.cs` | fighters |
| Pawns | 30 | `Movement` | `Sim/Pawns/MovementSystem.cs` | moving pawns |
| Pawns | 35 | `Doors` | `Sim/Pawns/DoorSystem.cs` | doors |

The **Things** phase is closed to systems: it is the tick-group dispatcher over `ITickable`s
(`Normal` every tick, `Rare` 250, `Long` 2000, phase-spread by `TickPhaseOffset`). Four things
actually tick — `Skyfallers` (Normal), `WildlifeSystem` and `NatureSystem` (Rare), `SkillSystem`
(Long); seven more register with `TickGroup.Never` so that they are enumerable state (the pawn
registry, the designation, construction, growing, storage and kitchen grids).

**Deferred** is where structural change happens: collapses, spawns, removals go through
`SimWorld.Defer` (next phase 5) or `DeferThisTick` (inside phase 5, for a death that must not
outlive its tick), never inline while another system may be scanning the grid.

**Where the list is.** `Sim/Pawns/ColonyComposition.cs` — `AddColony` is the one place a colony's
systems, hashables, contributors and intent handlers are registered, and `ColonyWorld.Build` is
the one place a world is built. Seven components attach themselves (`Attach(SimWorldBuilder)`:
designations, construction, incidents, growing, storage zones, storage units, kitchen); the rest
are wired inline in `AddColony`. Prefer `Attach` for anything new — it is the shape the file is
moving towards (`docs/plans/refactoring.md` R6).

**The shared state.** `Sim/Pawns/PawnContext.cs` is what every system and driver receives. It
carries the grid, the nav graph, the items, the reservations and the registry as constructor
arguments, and **twenty-one nullable slots** (`Construction`, `Storage`, `Temperature`, `Power`,
`Kitchen`, `Home`, `Raids`, …) that `AddColony` fills. A slot is null only in a test fixture that
did not build that part. Read a slot with `!` only in code that runs after `AddColony`; anything
that can run in a fixture asks `?.` and does nothing.

The seams a system uses, all in `Sim/SimWorld.cs`'s `SimWorldBuilder`:

| Seam | Interface | Registered by | Counts today |
|---|---|---|---|
| runs in the tick | `IWorldSystem` / `ITickable` | `AddSystem` / `AddTickable` | 15 / 11 |
| in the state hash | `IStateHashable` | `AddHashable` | 23 |
| in the save | `ISaveable` (`SaveKey`, `Save`, `Load`) | `ColonyWorld.SaveComponents`, in write order | 30 |
| published to the interface | `ISnapshotContributor` (`Contribute(world, writer)`) | `AddSnapshotContributor` | 20 |
| takes a player command | `Func<Intent, IntentRejection>` | `AddIntentHandler(kind, …)` | 37 of 49 kinds; the rest in `SimWorld.HandleIntent` |
| offers work | `WorkGiver` subclass with a parameterless constructor | joins by existing (`WorkGiverRegistry`) | 15 |
| does a job | `JobDriver` subclass | named by the `JobDef` | 28 |
| decides for a mind | `ThinkNode` subclass | `JobSystem.DefaultTree()` / the animal tree | 13 |

**Caches and derived state are never hashed** (`SkyColumns`, `HomeArea`, `Hearth` while empty,
the aspect index, every path). If a thing is not saved and not hashed, it must be rebuildable from
what is.

## 3. The frame, as built

`OdysseyBootstrap` (`Presentation/Bootstrap/`, a `MonoBehaviour`) is the composition root for
everything the player sees. `Update` runs the tick accumulator (`_world.Tick()` as many times as
the speed says, `ClockHeld` gating it during the wake); `LateUpdate` (288 lines) drives every
director in this order, each under a `FrameSection` the developer overlay and the perf trace read:

| Section | What runs | Owner |
|---|---|---|
| — | the menu bed; slice settings from the camera rig (`wallsLowered`, `landscapeGround`) | `SliceCameraRig`, `WallsView` |
| `Mirror` | `WorldRenderModel` takes sites, lines, crops, zones, stores from the snapshot | `Presentation/World/WorldRenderModel.cs` |
| `Sight` | sight lines for the draft | `Rendering/SightLines.cs` |
| `World`, `Surround` | `ChunkRenderer.Render`: chunks (frustum-culled, meshed under an 11-chunk / 2 ms budget), the skirt, the scenery | `Rendering/ChunkRenderer.cs`, `TerrainSkirt.cs`, `IndirectScenery.cs` |
| `Crowd` | `PawnCrowdIndex.Rebuild` — every pawn bucketed once on a 3 m grid | `Rendering/PawnCrowdIndex.cs` |
| `Figures` | `PawnFigureDirector.Sync`: the 64 nearest pawns as animated rigs, posed | `World/PawnFigureDirector*.cs` |
| `Audio` | `AudioDirector.Sync` | `Audio/AudioDirector.cs` |
| `Actors` | selection highlight mask; `ChunkRenderer.RenderActors`: far pawns, items, carried loads, falling things | `Rendering/ChunkRenderer.cs` |
| `Doors` | door leaves and corpses | `World/DoorDirector.cs`, `CorpseDirector.cs` |
| `Overlays` | fire; then the bootstrap's own passes: standing orders, cracks, demolition sounds, zones, sites, tool preview, power lines, home edge, selection cursor, draft marks, landing and lock-on rings, combat marks, shot readout, floaters, blood, weather, hearth | `OdysseyBootstrap.Draw*` (twenty-one methods; `docs/plans/refactoring.md` R1) |
| `Birds`, `Butterflies`, `Clouds` | the ambient life | `Hud/BirdSky.cs` + `World/BirdDirector.cs`, `Hud/ButterflyMeadow.cs` + `World/ButterflyDirector.cs`, `Hud/CloudDeck.cs` + `World/CloudDirector.cs` |

Then `HudShell.Update` (`Presentation/Ui/HudShell.cs`, also a `MonoBehaviour`): the wake, the
curtain, `HudDirectors.Refresh(snapshot)`, the ride, and three cadence buckets — fast, mid and
slow — into which every panel refresh is sorted so no frame carries a spike; then the marquee, the
armed banner, marks, bar keys and the context menu.

Two rules from `docs/process.md` §3 that this order embodies: **presentation per-frame work scales
with what is visible** (chunks in the frustum, the nearest 64 figures), never with the board; and
**presentation never derives a number the simulation knows** — it is published as a view field or
an aspect.

## 4. The folders

**`Assets/Odyssey/Sim/`** — one folder per area: `World` (grid, support, enclosure, sky columns,
hearth, home), `Pathing` (nav graph, path service, regions), `Pawns` (46 files at the top level:
the pawn, the registry, the context, the job pipeline, needs, movement, the colony composition;
`Combat/` 46 files, `Health/`, `Wildlife/`), `Construction`, `Designations`, `Storage`, `Growing`,
`Cooking`, `Power`, `Temperature`, `Weather`, `Events` (incidents, skyfallers, `Raids/`), `Defs`
(the loader), `Saving` (the sections and the format number), `Worldgen` (the passes, `Natural/`),
`Diagnostics` (hash and phase traces). The `*Content.cs` file in an area holds that area's `Def`
types and the tables built from the XML; the `*Aspects.cs` files hold the names of what it publishes.

**`Assets/Odyssey/Sim.Contracts/`** — `Views.cs` (26 view structs and `WorldSnapshot`),
`Intents.cs` (49 kinds, `IntentBus`, `PausedIntents`), `Catalogue.cs` (the handles both sides
name things by), `Determinism.cs` (`StateHash`), and the small ones (planet, sites, weather,
calendar, power and home views).

**`Assets/Odyssey/Hud/`** — 139 files in one flat folder, named by suffix, and the suffix is the
contract: `*Model` (22: what a surface shows, built from the snapshot, no Unity), `*Director`
(16: what a surface decides — open, selected, armed — session state), `*Labels` (12: how a Def or
a number is written, through `Registry.Label`), `*Layout` (7: the pixel constants, each with a
`HudLayoutTests` row), `*Catalogue` (5: what exists — skills, work types, research, the Almanac),
`*Names` (6: the aspect keys as the interface reads them). `HudDirectors` builds the directors
together so the rules between them live in one place. The ambient life's state machines
(`BirdSky`, `ButterflyMeadow`, `CloudDeck`) live here too, engine-free, so they run in the fast tier.

**`Assets/Odyssey/Presentation/`** — `Bootstrap` (the root, the presenters that realise a
director's decisions, settings and save files), `Rendering` (75 files: chunk meshing and drawing,
materials, the skirt, the scenery, the render features, the pose maths), `World` (54: the
directors that draw the things in the world and the poses — `SleepPose`, `SwimPose`, `CarryPose`,
`SitPose`, `ClimbPose`, `CombatPose` are static, testable pose functions), `Ui` (the UI Toolkit
shell: `HudShell` in 22 partials, one per panel, plus `HudGlyph`, `HudText`, `Hud.uss`), `Camera`,
`Audio`, `Diagnostics` (the frame trace), `Shaders`.

**`Assets/Odyssey/Defs/Core/`** — 24 XML files under `Pawns/`, `World/`, `Events/`: **the only
copy** of every tuning number. `docs/design/icon-keys.csv`, `proper-nouns.csv`,
`colonist-names.csv` — the only copy of every player-facing name.

**`Assets/Editor/Odyssey/`** — `PlayScene.cs` builds the played scene; `AudioSetup`,
`SyntyImport`, the module catalogue; and 53 probes, checks and contact sheets (`scripts/unity.sh
shot <Method>`).

**`tools/`** — `dotnet/` (the fast-tier mirrors), `wiki/` (`build_wiki.py`, `emit_labels.py`),
`icons/`, `perf/` (`trace.py`), `ci/` (`tiers.py` picks the tiers a change runs; `size_ratchet.py`
holds every large file to its ceiling), `mockups/`, `audio/`.

## 5. Recipes: to add one of each

Each recipe is the list to add a row to and the test that will fail if you forget. **Test-first
for anything in `Sim` or `Hud`, with the negative control** (`docs/process.md`).

**A subsystem.** A class implementing `IWorldSystem` in `Sim/<Area>/`; the doc comment names what
its loop scales with (§3 of `docs/process.md`); registered in `AddColony` — through an
`Attach(SimWorldBuilder)` on the owning component if it holds state. If it holds state it also
implements `IStateHashable` (and the hash sees it: `AddHashable`) and `ISaveable` (a section, added
to `ColonyWorld.SaveComponents` **in write order** and to `SaveFormat` if the format moves). A row
in `TickBenchmarkTests` if it runs every tick. **Every golden moves** if it hashes anything: re-bake
with `ODYSSEY_REGOLDEN=1`, run `GoldenColonyProbe` against `main` and write the sentence in
`Golden.cs` saying the colony did the same things.

**A job.** A `JobDef` row in `Defs/Core/Pawns/Jobs.xml` (the fingerprint in `PawnContentDefTests`
moves; say why); a `WorkGiver` subclass with a **public parameterless constructor** (it joins the
scan by existing; `WorkGiverRegistrationTests` pins the order, which comes from `WorkTypes.xml`,
never from discovery); a `JobDriver` subclass (`TryMakeReservations`, `Tick`, `Cleanup`;
`ToilProgress` counts milliwork in every driver — `ToilProgressHasOneUnitTests`); a `ui.job.*` key
in `icon-keys.csv`, then both generators. A per-job tally in the hash moves every golden's
`Generated` value before a tick runs, and that is expected (`Golden.cs`).

**A player command.** An `IntentKind` in `Sim.Contracts/Intents.cs` (and a row in
`PausedIntents` if it may apply while paused); a handler on the component that owns the state,
registered once with `AddIntentHandler` (two claimants throw); a `HudCommands` or director method
on the interface side that submits it; a test for each `IntentRejection` it can return, because a
command that silently does nothing is the project's commonest fault shape.

**Something the interface can see.** For a thing: a view struct in `Views.cs` and a row from a
contributor's `Contribute`. For a fact about a pawn: a **sparse aspect** — a name in the area's
`*Aspects.cs`, written with `writer.AddPawnAspect(id, key, value)` only when it is not the default,
read through the matching `Hud/*AspectNames.cs`. Detail views are subscription-gated
(`Views.WatchPower`, `WatchHome`, `CellDetail`): publish only to a subscriber. Nothing published
is hashed, so no golden moves — which also means a test must assert the row directly.

**A Def type.** A class `: Def` in the area's `*Content.cs`, its XML under `Defs/Core/`, loaded
through `ContentPack`; a fingerprint test on the built table (a deliberate change is one line, an
accidental one fails). A test that retunes content **replaces the Def, never writes through it**.

**A HUD panel.** In `Hud`: an `XModel` (built from the snapshot, with its test in `Tests/Hud`), an
`XDirector` held on `HudDirectors` (open/closed, what is selected; Escape's rung in
`SettingsDirector.Escape`), an `XLayout` (the constants, with a `HudLayoutTests` row and
`PanelOuterWidth`-style chrome arithmetic — never a literal width). In `Presentation/Ui`: a
`HudShell.X.cs` partial that builds the elements and syncs them in a cadence bucket. Every name
through `Registry.Label(key)` (`RegistryTests` reads the C# and fails on a literal); every glyph a
drawn `HudGlyph` or an ASCII string (`HudFontTests` reads both fonts); the key in `HotkeyAction`
(`HotkeyClashTests`). A `docs/design/NN-*.md` with the decisions.

**Something drawn in the world.** A director in `Presentation/World` (things that move) or
`Rendering` (things fixed to the grid), synced from `LateUpdate` under its `FrameSection`;
instanced (`RenderMesh` / `RenderPrimitives`), **never one draw per cell** (`docs/bug-patterns.md`
P10); anything fixed to the grid is draped, only what moves is lifted; nothing in a cell, a save or
the hash. A `FrameTimeTests` arm (`Category("Measurement")`) if it has a cost claim, with a
control in the same run. A shader found at runtime goes in `ShaderInclusion`
(`ShaderInclusionTests`), or no player can be built.

**A name.** A row in `docs/design/icon-keys.csv` (and `icon-map.csv`, `proper-nouns.csv` as
applies); `python3 tools/wiki/build_wiki.py`, `python3 tools/wiki/emit_labels.py`, `python3
tools/icons/icons.py validate`; all three `--check`s pass before the commit. Never edit
`docs/wiki/` or a `*.g.cs`.

**A measurement.** The tick: `TickBenchmarkTests` (the busy arm, not the resting one). The frame:
`FrameTimeTests`, with a control inside the same run, on the played map (`PlayedMap`) — a number
from another run compares with nothing on the owner's machine. Whether the colony did anything
different: `GoldenColonyProbe`. The GPU: `PlayerBench` in a development player. A session: the trace
in `Logs/perf/` and `tools/perf/trace.py`. The number goes in the design doc **with its machine and
date** beside the decision it made.

## 6. The guards that will catch you

Every one of these is a test that fails **far from the edit**, which is why the tiers are never
filtered by feature (`docs/process.md` §5):

| Guard | Catches |
|---|---|
| `Golden.cs` (six values) and `GoldenColonyProbe` | any change to what a colony does, and whether a hash move was the hash seeing more or the colony doing something different |
| the `Fingerprint` in each `*DefTests` | an accidental content change |
| `RegistryTests` (reads every C# file in `Hud` and `Presentation`) | a player-facing name written as a literal, or a key the CSV does not know |
| `HudFontTests` (parses both `.ttf` cmaps) | a character neither shipped font can draw |
| `HudStyleSheetTests`, `HudLayoutTests` | a C# layout constant disagreeing with `Hud.uss` |
| `HotkeyClashTests` | two actions on one key |
| `HopPriceHasOneOwnerTests`, `ToilProgressHasOneUnitTests`, `TemperatureLabelsTests` | one rule written in two places |
| `WorkGiverRegistrationTests` | scan order depending on discovery order |
| `FigureCeilingTests` | the 64-figure ceiling raised by an edit rather than a measurement |
| `SkyAgreementTests` | the render mirror's shelter disagreeing with the simulation's |
| `AlmanacCatalogueTests`, `AlmanacFactsTests` | a thing the HUD can show with no page, or a quoted number the Defs no longer say |
| `ShaderInclusionTests` | a runtime-found shader missing from the player |
| `PlayedMapTests` | a measurement taken on a board nobody plays |
| `FoundationTests` (reflection over the assemblies) | `Sim` or `Contracts` reaching for the engine |
| `tools/ci/size_ratchet.py` | a production file growing past its ceiling without a visible decision |

## 7. What is deliberately not here

Mechanics and their measurements (`docs/design/`), the reasoning behind a decision
(`docs/journal.md`), the bug shapes (`docs/bug-patterns.md`), and what is true right now
(`CLAUDE.md`). This page is the index to the code, and it stays short by pointing.
