# Refactoring plan — the order the monoliths come down in, and what each cut must prove

Written 2026-09-26 from `docs/audit/2026-09-26-architecture-review.md`. It supersedes **HT5** in
`vertical-slice.md` (the monolith cuts), which was planned on 09-19, never started, and whose
files have since doubled. HT1–HT4 and HT6–HT9 stand as written there.

**The standing rule this plan adds.** A cut is not a track to schedule; it is *one unit per gap*.
When the batch of pull requests touching a file has merged, the next thing that opens on that file
is its cut, and the cut merges before the next feature on it. The size ratchet
(`tools/ci/size_ratchet.py`) holds the line between gaps: a file over its ceiling fails CI until
the growth is a visible decision. **Phase gate:** each unit below is designed here and built only
when its blocking PRs are merged and the owner says go; R0 is the owner's call on timing.

## 1. What every cut owes

Behaviour-preserving means *provably* so, and the proof is the same shape each time:

1. **One file per pull request**, `claude/cut-<file>`, merged with `main` on the day it opens and
   again before merge. No feature rides on it.
2. **Every golden identical** (`Golden.cs`, all six values) — a presentation cut cannot move one,
   and if it does the cut was not a cut. A `Sim` cut runs `GoldenColonyProbe` against `main` and
   writes the sentence.
3. **The screenshot probes that touch the file re-shot and identical**: `scripts/unity.sh shot
   <Probe>` before and after, on the same machine in the same hour, pixel-diffed. The probes for
   each unit are named below.
4. **The frame measured in one run** where the file is on the frame path: `FrameTimeTests` with
   the cut's before and after arms in the same run, because a number from another run compares
   with nothing on the owner's machine (`06-rendering-and-camera.md` §6c).
5. **No allocation added on the frame path**: the aspect and crowd tests' allocation windows, and
   `HudStressTests`, unchanged.
6. **The design document that owns the line gains one line** saying what moved where, and
   `docs/code-map.md` is updated in the same commit.
7. **`tools/ci/size_ratchet.py --bake`** after the cut, so the ratchet tightens to the new sizes.
8. **Both Unity tiers green on the runner** and the player build smoke run
   (`-odyssey-newgame`) where the file touches a shader, a resource path or the bootstrap.

A cut that cannot meet item 2 or 3 is a behaviour change wearing a refactor's name, and goes back
through `docs/process.md` as a unit of its own.

## 2. The units

Sizes: S under a day, M a day or two, L longer. "Waits for" names the open pull requests on the
file on 2026-09-26; the list is stale the moment one merges — re-run the collision check
(`docs/audit/2026-09-26-architecture-review.md` §5a's method: the PR file lists) before opening.

| Unit | Size | Waits for | What |
|---|---|---|---|
| **R0 The status table out of `CLAUDE.md`** | S | #143, #149, #192, #193, #211, #215, #231, #247 (all edit the table) | `docs/status.md` takes the table: one row per track, **one or two lines** each — state, branch or PR, the design doc — and nothing that is reasoning, which is already in the journal and the design docs and is linked, not repeated. `CLAUDE.md` keeps the working agreement, the handover, the wiki obligation, the standing rules, the gates, the "read this before touching" table and a two-line pointer; target under 300 lines. **Done when** `CLAUDE.md` is under 300 lines and `wc -c` under 40 KB, and a check (`tools/ci/`, standard library, in the content gates beside the ratchet) fails the build if `CLAUDE.md` passes 400 lines again — because the last split lasted ten days without one. The move conflicts with every open PR that edits the table, which is why it waits for the batch; each of those PRs then rebases by moving its row, a two-minute job the PR description of R0 spells out. |
| **R1 The frame passes** | M | #143, #193, #231, #248 (all touch the root) | `Presentation/Overlays/IFramePass` — `FrameSection Section { get; }` and `void Sync(in FrameContext frame)` where `FrameContext` carries the snapshot, the active layer, the slice, `movePerTick`, the tick alpha and the renderer. Each of the root's twenty-one `Draw*`/`Update*` methods becomes a class in `Presentation/Overlays/` holding the fields it touched (`DrawZones` two, `DrawSelectionCursor` eight; none needs the root). `LateUpdate` becomes: the mirror, then a loop over the registered passes marking each pass's section, then the ambient life. **Order is declared** in one list in the root, the way `AddColony` declares the systems — the first thing to write in the PR, before any class moves, so the diff shows the order unchanged. **Proof:** the frame probes (`FrameTimeTests.TheFrameAgainstColonySize`, `TheMarkPassCostsWhatItSubmits`) in one run before and after; the overlay probes identical (`SelectCheck`, `SelectionHighlightShot`, `SandbagCheck`, `ShelfCheck`, `CropCheck`, `WallCheck`, `CampfireCheck` — `scripts/unity.sh shot <Probe>.Shoot`); `OdysseyBootstrap.cs` under 2,500 lines. |
| **R2 The thing renderer** | M | #248 | `Rendering/ThingRenderer.cs` takes `RenderThings`, `RenderCarriedLoads`, `RenderFalling`, the colonist modules and the item heaps out of `ChunkRenderer`, behind the `RenderActors` entry point that already exists; `ChunkRenderer` keeps chunks, buckets, cell plates and the three partials (`Breaks`, `Cracks`, `Topple`), which are chunk geometry. `ICarriedLoads` is already the seam. **Proof:** `FrameSection.Actors` and `World` in one run; the item, carried-load and falling screenshots identical; every golden identical (trivially — nothing here is hashed — but the test runs). |
| **R3 One view per panel** | S–M each, twenty-two panels | none for Almanac, Animals, Assign, Bar, Bills, Build, Combat, ContextMenu, Inventory, Orders, Research, Ride, Settings, Wake, Work, World; #231/#249 for Panels; #193/#215/#231/#249 for Inspect and Start | `HudShell.X.cs` → `Ui/Panels/XPanelView.cs`: a class owning its elements and its cadence-bucket sync, constructed with an `IShellServices` (theme, `HudDirectors`, the intent sink, `Registry`, the tooltip). The shell keeps the surfaces it composes (the bar, the world layer, the modal, the curtain) and a list of its panels. One panel per PR, **the untouched ones first** (Research, Inventory, Almanac, Animals, Assign are self-contained and no open PR has them); Inspect last, after #193/#215/#231/#249. For each: the shell keeps forwarding members for one release so the 24 test files compile, then they move. **Proof:** `DockedTabGeometryTests` and the panel's own layout tests unchanged; the panel screenshot identical; `HudStressTests` unchanged; `HudShell.cs` shrinks by the panel's fields. |
| **R4 The pose stages** | M | #192, #247, #248, #249 | `PawnFigureDirector.Pose` (563 lines) → an ordered list of `IPoseStage` (`Apply(ref PoseState state, in PawnView pawn, Figure figure, float dt)`), one per paragraph: gait, work stroke, gesture, carry, sheath, aim, jump, swim, sleep, sit, climb, gaze, crowd sidestep. **The order is the decision** (`docs/lessons.md`, "the pose runs twice a frame") and is one list. The baseline's other half — the five per-figure aspect scans replaced by one walk of `PawnAspects` per `Sync` — goes in the same unit if the aspect index has not already made it moot (`31-aspect-lookup.md`). **Proof:** the figure sheets identical (`SwingCheck`, `HeavySwingSheet`, `SleepCheck`, `SwimCheck`, `ClimbCheck`, `GestureCheck`, `SheathProbe`, `HandProbe`, `FootingProbe`, `BanditSheet`); `TheFrameAgainstColonySize` in one run; the `Figures` section unchanged at 64/192/384. |
| **R5 The job pipeline's bases out** | S | #149, #215, #231, #249 | `ThinkNode` and `WorkGiver` to `Sim/Pawns/ThinkNode.cs` and `WorkGiver.cs`; the think tree's nodes (`CriticalNeedsThinkNode` and the rest of `DefaultTree()`) to `Sim/Pawns/Think/`; the six intent-handler partials **stay** (they are the one path into `StartJob`, by design). `JobSystem.cs` keeps the pipeline: the scan, `TickPawn`, `Think`, the counters, the hash and the save. **Proof:** every golden identical, `WorkGiverRegistrationTests` unchanged (discovery is by assembly, so moving a file cannot change the order — the test says so). |
| **R6 The context sealed, `Attach` everywhere** | S–M | #215, #231, #249 (the context); #231, #249 (the composition) | Every component wired inline in `AddColony` gains `Attach(SimWorldBuilder)` carrying its own registration and its comment (temperature, weather, power, raids, hearth, home, combat, movement, needs, doors, enclosure, support, navigation, trapped pawns, starting skills, plant growth, skills, nature, the registries, the contributors); `AddColony` becomes the construction of each part and the ordered list of `Attach` calls. Then `PawnContext.Seal()` at the end of `AddColony`, throwing with the slot's name on any the colony needs and is null; fixtures that build a partial context do not call it. **Proof:** every golden identical **and** a new test that builds a colony, drops one `Attach`, and gets the sentence rather than a null reference — the negative control the whole unit is for. The schedule is sorted by `(Order, Name)`, so moving a registration line cannot change behaviour, and the goldens are the proof that it did not. |
| **R7 The `Hud` folder** | S | every open PR that touches a `Hud` file (52 files on 09-26) — so, the drained batch | Sub-folders, pure moves with their `.meta` files: `Panels/` (the models, directors and layouts of the docked tabs and the inspect pane), `World/` (selection, slice, camera, overlays, orders, build), `Ambient/` (`BirdSky`, `ButterflyMeadow`, `CloudDeck` and their palettes), `Labels/`, `Layout/` (`HudLayout`, `HudTheme`, `HudType`), `Session/` (settings, hotkeys, save and leave prompts, the wake). One assembly still; the asmdef does not change. **Proof:** the fast tier's Hud project compiles and passes (it globs the folder); `RegistryTests` and `HudFontTests` still find every file (they walk the folder, not a list). |
| **R8 Hygiene, and the knobs** | S | none | HT2 as written in `vertical-slice.md` (analysers on the dotnet mirror at `latest`, warnings as errors for `Sim`, `Contracts` and `Hud`; `.editorconfig`; the thirteen packages out; the probes behind `ODYSSEY_PROBES`), plus the rule for tuning statics: **a knob is a static only while its sweep is open**; a shipped value is a `const` or an asset field, and a sweep's test restores what it set in a `finally`. Convert the statics in each file as that file is cut (R1, R2, R4), not in one sweep. **Proof:** both tiers, and a player build smoke run because removing packages is build-shaped. |
| **R9 The design index** | S | none | `docs/design/INDEX.md`, generated by `tools/wiki/build_wiki.py` (or a sibling script, standard library) from each document's title line and a `Owns:` line it may carry, gated with `--check` beside the wiki. No file is renamed; the seventeen colliding numbers stay and the index is how a session finds the right one. `CLAUDE.md`'s "read this before touching that line" table then points at the index for the mechanics and keeps only the rules. |

Two smaller cuts ride with whichever unit touches their file first: `Views.cs` into one file per
view family (`PawnViews`, `ThingViews`, `StorageViews`, `CombatViews`; the shape `PowerViews.cs`
already has) — waits for #248 and #249; and `PlayScene`'s catalogue table into
`ModuleCatalogueBuilder`, as the baseline planned — waits for #143 and #248.

## 3. The order, and why

R0 first, because it is the cheapest, it un-contends the most contended file in the repository,
and it is the one every session pays for on every read. Then R1, R2 and R5 as their batches
drain — they are the three cuts that turn a growing paragraph into a class-and-a-row, which is
what stops the next feature adding to the file. R6 with or just after R5 (same files). R3 panel
by panel in the gaps, the untouched panels at any time. R4 last of the big ones, because four
open PRs are in that file and the figure sheets are the slowest proof. R7, R8 and R9 fit in any
gap and R8 can go now.

**Estimated total: six to eight days of unit work spread over however many gaps the feature queue
gives.** None of it is player-visible, so none of it enters the playtest queue; the owner's cost is
the review of each PR and the re-shot sheets.

## 4. What this plan deliberately does not do

- It does not stop features to refactor. One cut per gap, and the ratchet meanwhile.
- It does not rename a design document, rewrite a comment, or change a golden.
- It does not introduce a container, a base class for directors, or a generic component layer —
  `docs/audit/2026-09-26-architecture-review.md` §2d.
- It does not touch `Sim`'s hashed state or any save section: R5 and R6 move code and add a
  construction-time check, and the goldens prove it.
- It does not cut `ConstructionGrid` (2,110 lines, no method over 71: long, not tangled) or
  `NavGraph` (in flight under HT1). Both are on the ratchet and come down when they next grow.

## 5. Gate

Each unit: the proof in §1, the collision check re-run before opening, `docs/code-map.md` and the
owning design document updated in the same commit, the ratchet re-baked, one line in `CLAUDE.md`'s
status (or `docs/status.md` after R0) and the reasoning in `docs/journal.md`. The plan is done when
`OdysseyBootstrap`, `ChunkRenderer`, `HudShell` (the root partial) and `PawnFigureDirector` are each
under 2,000 lines and the ratchet's list is under twenty files.
