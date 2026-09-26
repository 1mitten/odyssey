# Architecture review, 2026-09-26 — readability, composition, what a session needs, and scale

The owner asked for four things: *review the code for readability and refactor where it makes
sense; make sense of composition and inheritance so we can componentise; lay the code out so
Claude Code can work in it every time, and update the docs for understanding of the subsystems;
ensure we scale out, think about systems colliding, and the best ways forward.* This is the
review. It is the successor to `2026-09-19-baseline.md`, which measured the tick and named the
monoliths; a week later this one measures the **shape** — how big things are, how they are put
together, how the sixty-odd merges since then landed, and where the next ones will collide.

**What was and was not possible here.** Everything was read and counted on `main` at `3e6b0007`
(the Almanac merge), in a container with Python 3.11 and no dotnet SDK — the SDK's download hosts
are refused by the container's proxy, so **no C# test ran in this session** and the review makes
no code change it could not prove. It ships three documents, one gate written in Python and
verified here, and a plan; the cuts themselves are units in `docs/plans/refactoring.md`, each with
the proof it owes, sequenced behind the pull requests that would otherwise conflict with it.

## 0. The verdict, ranked

The architecture is sound and has held under a week of very fast growth: the seams the brief asked
for are real and enforced, the registration lists mean a feature edits no file it does not own,
the content is written once, and the guard tests catch the project's commonest faults far from
the edit. **What has not held is size.** Every file the baseline audit named as a monolith is
between 1.8× and 3× larger than it was seven days ago, no cut was made, and nothing existed to
make the growth a decision. That is the finding; the rest are its consequences and the things that
make it hard to fix now.

| # | Finding | Evidence | What to do | Unit |
|---|---|---|---|---|
| 1 | **The monoliths doubled in a week, and nothing made it a decision.** The composition root 2,408 → **4,655** lines; `ChunkRenderer` 1,848 → **5,590** across four partials; `HudShell` ~6,300 → **16,257** across twenty-two; `PawnFigureDirector` 4,414 → **7,953** across nine; `JobSystem` **2,880** across eight; 38 production files are over 800 lines. HT5, the cut, was planned on 09-19 and never started. | §1, `tools/ci/size_ratchet.py --list` | **Done today:** a size ratchet in the content gates — every file over 800 lines is listed with a ceiling, and growing past it fails CI until the PR raises the number in the open. Then the cuts, in §5's order. | ratchet; R1–R5 |
| 2 | **Every cut collides with three to six open pull requests.** Twelve PRs are open touching 336 files: `OdysseyBootstrap` is in four of them, `HudShell.Inspect` four, `JobSystem` four, `PawnFigureDirector` four, `PawnRegistry` **six**, `Pawn` and `InspectModel` five. A cut landed now conflicts with all of them on moved code, which git cannot reconcile. | §5, the collision map | Cuts are sequenced **behind** the batch that touches the file, and a cut merges before the next feature on that file opens. The rule is written into the plan; the ratchet holds the line meanwhile. | R1–R5 |
| 3 | **`CLAUDE.md` is 149 KB and 60 per cent of it is the status table.** 88,679 bytes in 57 rows, each a paragraph of history; the file names this as its own failure mode and it recurred within ten days of the last split. Eight open PRs edit it, so it is also the most contended file in the repository. | `wc -c CLAUDE.md`; §4 | Move the table to `docs/status.md` with one or two lines a row and the reasoning where it already is (the journal, the design docs). Timed for the gap after the eight PRs merge, because the move conflicts with every one of them. | R0 |
| 4 | **The composition root draws.** Twenty-one `Draw*`/`Update*` methods, about 2,000 lines of overlay passes, in the class whose job is to build the session — and the frame has no registration list where the tick has one. `LateUpdate` is 288 lines of hand-ordered calls. | §2c, `OdysseyBootstrap.cs:1563–1851, 2034–3971` | An `IFramePass` list mirroring `IWorldSystem` — declared section, one `Sync(snapshot, frame)` — so a new drawn thing is a class and a row, and the `FrameSection` timing falls out of the loop. | R1 |
| 5 | **The shared simulation state is a bag of twenty-one nullable slots.** `PawnContext` is the right idea (RimWorld's `Map` is the same shape) but a forgotten slot is a null at first use, and `AddColony` is half-migrated: seven components attach themselves, the rest are wired inline in a 348-line method. | §2a, `PawnContext.cs`, `ColonyComposition.cs` | Finish the `Attach` migration and seal the context at `Build` so a missing slot throws at construction, where the fault is, not in the first tick that reads it. | R6 |
| 6 | **The HUD shell is twenty-two partials sharing 132 fields.** The `Hud` side has the best convention in the codebase (model, director, layout, labels per surface, all Unity-free); the view side is one class, so every panel can reach every other's elements and nothing says which does. | §2b | One view class per panel, starting with the sixteen panels no open PR touches, with the shell keeping the surfaces it composes. | R3 |
| 7 | **The `Hud` folder is 139 files in one directory.** Findable only by suffix; the suffixes are consistent, which is why it has worked this long. | §3 | Sub-folders by surface once the batch drains (a pure move, but every open Hud edit conflicts with it). | R7 |
| 8 | **Tuning knobs are process-wide mutable statics in Presentation** — 119 settable statics (`GroundRelief` 11, `SwimPose` 8, `Overcast` 8, `CarryPose` 7 …), the "settable so a sweep can replace a judged constant" pattern. Right for a sweep, wrong as a default: a test that sets one retunes every test after it, in the order the runner picks. | §2c | A rule, not a rewrite: a knob is a static only while its sweep is open; a shipped value is a `const` or an asset field. Convert as each file is cut. | R8 |
| 9 | **HT2 hygiene never landed.** No analysers, no `.editorconfig`, and the thirteen unused packages the baseline named are still in `Packages/manifest.json`. | `ls tools/dotnet`, `manifest.json` | Unchanged from the baseline plan; it makes every later cut faster to review. | R8 |
| 10 | **The design documents collide on seventeen numbers**, and the map to them is an 89-row table in `CLAUDE.md`. The right document is findable only by someone who already knows its name. | `ls docs/design` | A generated index (title, number, the lines it owns), as a content gate like the wiki; never rename a file. | R9 |
| 11 | **A container session cannot prove a C# change.** The fast tier needs a dotnet SDK the proxy will not let this container download; only the owner's two machines and CI can run it. | this session | Allow `builds.dotnet.microsoft.com` (or vendor the SDK tarball) for the remote environment, or accept that container sessions ship docs, tools and plans and leave code to CI. The owner's call. | — |

**What is good, and must not be undone by the tidying.** The `Sim` / `Contracts` / `Hud` /
`Presentation` split with the arrows enforced; the tick schedule sorted once by `(Order, Name)`;
the builder with one registration method per seam; the work givers that join by existing; the
sparse aspect rows; the goldens with their two-hash diagnosis and the colony probe; the content
fingerprints; the sixteen tests that read the source files to hold one rule to one owner; the
Model/Director/Layout/Labels convention in `Hud`; and the comment culture, which is the best this
reviewer has seen in a game codebase — nearly every non-obvious line says what it is for and
which measurement decided it. **The code's readability problem is size and location, not
style.** Nothing in this review asks for a comment to be rewritten.

## 1. What was measured today

Counted with `wc -l` on `main` at `3e6b0007`, production files only (tests and generated files
excluded), against the baseline audit's figures of 2026-09-19.

| Assembly | 09-19 | 09-26 | Growth | Files |
|---|---|---|---|---|
| `Odyssey.Sim.Contracts` | 1,784 | **4,918** | 2.8× | 14 |
| `Odyssey.Sim` | 25,000 | **55,877** | 2.2× | 197 |
| `Odyssey.Hud` | 12,000 | **36,305** | 3.0× | 139 |
| `Odyssey.Presentation` (production) | — | **81,091** | — | 192 |
| `Odyssey.Editor` | 14,093 | **21,818** | 1.5× | 69 |
| Tests (Sim, Hud, Presentation, PlayMode) | — | 139,507 | — | 494 |

The files the baseline called monoliths, then and now:

| File | 09-19 | 09-26 | Shape today |
|---|---|---|---|
| `Presentation/Bootstrap/OdysseyBootstrap.cs` | 2,408 | **4,655** | ~168 fields, ~60 methods; `LateUpdate` 288 lines; `DrawSelectionCursor` 211, `DrawHoverGhost` 120 |
| `Presentation/Rendering/ChunkRenderer.cs` (+3 partials) | 1,848 | **4,548 (5,590)** | ~131 fields; `DrawBuckets` 219, `RenderThings` 213 |
| `Presentation/Ui/HudShell.*.cs` (22 partials) | ~6,300 | **16,257** | 132 fields in the root partial; `HudShell.Inspect` 2,283 with `BuildInspectBody` 309 |
| `Presentation/World/PawnFigureDirector.*.cs` (9 partials) | 4,414 | **7,953** | `Pose` **563 lines**, `Create` 138 |
| `Sim/Pawns/JobSystem.*.cs` (8 partials) | — | **2,880** | the `ThinkNode` and `WorkGiver` base classes live inside it |
| `Sim/Construction/ConstructionGrid.cs` | 1,650 | **2,110** | 30 methods, none over 71 lines: long but not tangled |
| `Sim.Contracts/Views.cs` | — | **2,400** | 26 view types in one file |
| `Hud/HudLayout.cs` | 1,713 | **2,043** | constants; wants partials, as the baseline said |
| `Editor/PlayScene.cs` | 2,009 | **2,918** | the catalogue table the baseline wanted out is still in |

Thirty-eight production files are at or over 800 lines; they are the ratchet's list.

Other counts the sections below rest on: 15 `IWorldSystem`, 11 `ITickable`, 23 `IStateHashable`,
30 `ISaveable`, 20 `ISnapshotContributor`, 49 `IntentKind`s with 37 registered handlers; 32 `Def`
types, 28 `JobDriver`s, 15 `WorkGiver`s, 13 `ThinkNode`s; 31 presentation directors; 16 tests that
read source files; 10 settable statics in `Sim` (seven lazy content caches, two counters), 4 in
`Hud`, 119 in `Presentation`; 2 `TODO`s in 200,000 lines of production code.

## 2. Composition and inheritance, as built

### 2a. The simulation: registration lists over a shared context

Inheritance is used for exactly four open families, each a fixed contract with an unbounded number
of members: `Def` (32), `JobDriver` (28), `WorkGiver` (15), `ThinkNode` (13). That is the shape the
reference game proved and it is right here: a new job is one `JobDef`, one giver, one driver, and
the giver *joins the scan by existing* (`WorkGiverRegistry` reflects over the assembly, sorts by the
work type's order from the Defs, and throws on a giver with no parameterless constructor).
Everything else is composition through `PawnContext`, and everything that runs is on a list
(`SimWorldBuilder.AddSystem / AddTickable / AddHashable / AddSnapshotContributor /
AddIntentHandler`) sorted or ordered once at construction.

Two things to tighten, neither a rewrite:

- **`PawnContext` has twenty-one nullable slots** filled by `AddColony` (`Construction`, `Support`,
  `Doors`, `Enclosure`, `Temperature`, `Weather`, `Sky`, `Power`, `Kitchen`, `Chunks`, `Incidents`,
  `Growing`, `Nature`, `Storage`, `StorageUnits`, `Home`, `Hearth`, `Raids`, `Combat`,
  `ColonyStart`, `World`). The file's own comments record why each was made non-optional at the
  builder ("an optional parameter is how a caller forgets"), and then the slot itself is optional.
  The cheap close: `PawnContext.Seal()` at the end of `AddColony`, throwing on any slot the colony
  needs and is null, so the fault is at construction. Fixtures that build a partial context keep
  `?.` as they do now.
- **`AddColony` is two patterns at once.** Seven components own their registration
  (`Attach(SimWorldBuilder)`: designations, construction, incidents, growing, storage zones,
  storage units, kitchen); the other twenty-odd are wired inline in one 348-line method with the
  reasoning as comments. The reasoning is good and should stay; it should stay *on the component*.
  When every component attaches itself, `AddColony` is a list of `X.Attach(builder)` calls in
  construction order and reads in a minute.

The partials on `JobSystem` (`Draft`, `Attack`, `Area`, `Response`, `Equip`, `Rescue`) are intent
handlers that must share the pipeline's one path into `StartJob` — the comment says why, and it is
right: a second path in is a reservation leak. They are the seam working as designed and should
not be moved. What should move out of `JobSystem.cs` is what is *not* the pipeline: the
`ThinkNode` and `WorkGiver` base classes, and the think tree's nodes.

### 2b. The interface: the best convention in the codebase, and one class that ignores it

`Odyssey.Hud` is 139 files named by suffix, and the suffix is a contract: a `*Model` is what a
surface shows, built from the snapshot; a `*Director` is what it decides, session state, held on
`HudDirectors` so the rules between them live in one place; a `*Layout` is the pixel constants,
each with a `HudLayoutTests` row; `*Labels` write a Def or a number through `Registry.Label`. All
of it is Unity-free and runs in the fast tier. A session can add a panel's whole model and decision
side without touching a file it does not own, and the tests say exactly which convention it broke.

The Unity side of the same panels is **one class**: `HudShell`, twenty-two partials, 16,257 lines,
132 fields in the root partial that every partial reaches. The partial-per-panel layout is the
*right cut in the wrong syntax* — each `HudShell.X.cs` is already a panel view; making it a class
(`XPanelView`, owning its elements, taking a small `IShellServices` for the theme, the directors and
the intent sink) changes nothing a player sees and gives the panel a boundary the compiler
enforces. The tests reach the shell through 24 files; the baseline's plan for the start screen —
forwarding properties for one release — is the pattern.

### 2c. Presentation: directors, and the composition root that became one

The 31 directors in `Presentation/World` and `Rendering` are the pattern from `01-architecture.md`
§3a done properly: each reads the snapshot, holds its own drawn state, and is synced once a frame.
The poses beside them (`SleepPose`, `SwimPose`, `CarryPose`, `SitPose`, `ClimbPose`, `CombatPose`)
are static, engine-light functions with their own tests — composition, not inheritance, and right.

Three things sit outside that pattern and are where the frame-side growth went:

- **The composition root draws.** `OdysseyBootstrap` holds twenty-one `Draw*`/`Update*` methods —
  standing orders, cracks, zones, sites, ghosts, the tool preview, power lines, the home edge, the
  selection cursor, draft marks, landing and lock-on rings, combat marks, the shot readout — and
  `LateUpdate` calls them in a hand-kept order under `FrameSection` marks. Each is a director that
  was never given a class. `DrawZones` touches two fields; `DrawSelectionCursor` eight; none needs
  the root. The frame wants what the tick has: **a registered, ordered list of passes**, each
  declaring its section, so `LateUpdate` is a loop and a new drawn thing is a class and a row.
- **`PawnFigureDirector.Pose` is 563 lines** because every pose contributor (gait, work stroke,
  gesture, gaze, carry, sheath, aim, jump, swim, sleep, sit, climb) is a paragraph in one method
  rather than a stage in a pipeline. The baseline planned "re-sectioned into its nine named steps";
  the honest version is a list of `IPoseStage`s applied in order, because the order is the
  decision (`docs/lessons.md`, "the pose runs twice a frame, and only one of them starts from
  clean bones").
- **`ChunkRenderer` renders things as well as chunks** — `RenderThings` (213 lines), the carried
  loads, the falling items, the colonist modules and the cell plates share the class with the
  chunk batching. The seam is already there: `RenderActors` is a separate entry point with its own
  `FrameSection`; it wants its own class (`ThingRenderer`) holding the buckets it uses.

**Tuning knobs as statics.** 119 settable statics in Presentation are the "settable so a sweep could
replace a judged constant" pattern the surround work introduced and the record praises. It is right
for the sweep and wrong afterwards: a static is process-wide, so a test that sets `GroundRelief.X`
retunes every test the runner happens to run next, and nothing resets it. The rule to add: a knob
is a static while its sweep is open and a `const` (or an asset field) once the number is chosen —
and the sweep's own test restores it in a `finally`.

### 2d. What not to do

No dependency-injection container: the builder *is* the container and it is readable. No base
class for directors: they share a verb, not a contract, and the two `Sync` signatures that exist
are both right. No unifying of subsystems and directors, which `01-architecture.md` forbids for a
reason that still holds. No generic "component" layer under `PawnContext`: the slots are the
component list, and naming them is what makes `ctx.Storage` readable.

## 3. Readability

The comments are the readability. Every one of the eleven largest files was sampled and each
non-obvious decision carries its reason, its date and, where there was one, its measurement; the
"one owner" tests hold rules to one place; the naming is consistent (British, the project's own
words, `Something` for a thing and `SomethingSystem` for what ticks it). The problems are the
ones size makes:

- **Fifteen methods over 100 lines**, all but two in Presentation (the two are `InspectModel`'s):
  `Pose` 563, `BuildInspectBody` 309, `LateUpdate` 288, `SetCellRows` 277, `DrawBuckets` 219,
  `RenderThings` 213,
  `DrawSelectionCursor` 211, `BuildStoragePane` 161, `DescribeCellAt` 145, `SyncCellRows` and
  `RefreshInspect` 143, `Create` 138, `EmitEdifice` 130, `EmitScatter` 128, `FillStoragePanel` 124.
  Each is a sequence of named paragraphs already; each wants the paragraphs to be methods or
  stages. `Sim` has none over 80.
- **`ctx.Doors!`, `Pawns.Incidents!`** and the other twenty null-forgiving reads of the context are
  the reader's tax for finding 5: each is a place where a fixture that forgot a part fails with a
  null reference rather than a sentence.
- **`Views.cs` is 26 types in 2,400 lines.** One file per view family (`PawnViews.cs`,
  `ThingViews.cs`, `StorageViews.cs`, `CombatViews.cs`) is the cut; `PowerViews.cs` and
  `HomeViews.cs` already show the shape.
- **The `Hud` folder is flat.** 139 files whose only structure is their suffix. Sub-folders by
  surface (`Panels/`, `World/`, `Ambient/`, `Labels/`, `Layout/`) make the convention visible.

## 4. What a session needs

A session is effective in this codebase to the extent that it can find the list to add a row to
and the test that will fail if it forgets. Today that knowledge is in three places, and the
first of them is the problem:

- **`CLAUDE.md` is 769 lines and 148,922 bytes; the status table is 88,679 of them** — 60 per cent
  of the file every session reads first is a history of 57 tracks, most of it reasoning that the
  journal and the design documents already hold. The file's own text names this as the failure
  mode to watch for and records it happening once before (982 lines, 2026-09-16; the journal was
  the fix). It has recurred in ten days because the rule "update the status when it changes" was
  read as "append a paragraph". The fix is the same as last time (a short table here, the story
  there) and it is R0 in the plan — timed, because eight open PRs edit the table.
- **The 89-row "read this before touching that line" table** is the right idea and has outgrown
  the form: it is a linear scan. The code map replaces the scan with a structure — what layer, what
  list, what test — and the table stays for the mechanics.
- **`docs/design/` has 92 documents and seventeen numbers used twice or more.** `15-`, `17-`, `18-`,
  `19-`, `20-`, `21-`, `22-`, `23-`, `24-`, `26-`–`31-`, `42-`, `43-`. Renaming would break every
  cross-reference and the record says so; an index that is generated and gated (R9) is the cheap
  answer.

**Done today: `docs/code-map.md`.** The tick as built (every system with its phase, order, file and
what it scales with), the frame as built (every `FrameSection` and what runs under it), the seams
with their counts, the folders, a recipe for each kind of addition — a subsystem, a job, a
command, a published view, a Def type, a HUD panel, a drawn thing, a name, a measurement — and
the sixteen guards that fail far from the edit. It is linked from `CLAUDE.md`'s first line and
from `docs/README.md`, and the rule for it is the one the wiki has: if the code and the map
disagree, the map is the bug, fixed in the same commit.

## 5. Scaling out, and systems colliding

### 5a. The collision map

Twelve pull requests are open (one draft), touching 336 distinct files. The files most of them
touch are the ones any refactoring would move:

| File | Open PRs touching it |
|---|---|
| `CLAUDE.md`, `docs/journal.md` | 8 each: #143, #149, #192, #193, #211, #215, #231, #247 |
| `Sim/Pawns/PawnRegistry.cs` | 6: #193, #215, #231, #236, #248, #249 |
| `Hud/Registry.g.cs` (generated) | 6 |
| `Sim/Pawns/Pawn.cs`, `PawnContent.cs`, `ColonyWorld.cs`, `Hud/InspectModel.cs` | 5 each |
| `Sim/Pawns/JobSystem.cs`, `NeedsSystem.cs`, `Sim.Contracts/Catalogue.cs`, `Presentation/World/PawnFigureDirector.cs`, `Ui/HudShell.Inspect.cs`, `Bootstrap/OdysseyBootstrap.cs`, `Hud/DebugDirector.cs` | 4 each |
| `Sim/Construction/ConstructionGrid.cs`, `Sim/Pawns/PawnContext.cs`, `Ui/HudShell.cs`, `HudShell.Start.cs`, `Hud/HudLayout.cs` | 3 each |
| `Rendering/ChunkRenderer.cs` | 1: #248 |
| `Sim/Pawns/ColonyComposition.cs` | 2: #231, #249 |

Two things follow. **A cut is sequenced behind the batch on its file**, and merges before the next
feature on that file opens; a cut landed into an open batch costs every PR in it a conflict on
moved code that git cannot resolve and a human must. And **the spine is the pawn**: `Pawn`,
`PawnRegistry`, `PawnContent`, `InspectModel` and the inspect pane are what every feature adds a
field, a row and a line to. Those five want the same treatment the job pipeline got — a feature
adds *its own* file (an aspect, a section, a pane) rather than a paragraph to the shared one — and
the sparse-aspect seam already makes that possible for most of what they carry.

### 5b. The ratchet

`tools/ci/size_ratchet.py`, in the content-gates job from this commit: every production file at or
over 800 lines is listed in `tools/ci/size-ceilings.json` with a ceiling 25–74 lines above today's
size; a listed file may not grow past its ceiling and an unlisted one may not reach the threshold.
A PR that must grow one raises the number in the same diff and says why in the commit — **one
visible line, where before there was none.** `--bake` rewrites the file from today's sizes, so the
ratchet tightens by itself as the cuts land and loosens only by hand. Twelve unit tests, run with
the tier selector's. The four open PRs that grow the root, the renderer or the shell will meet it on
rebase and bump a number; that is the mechanism working, not a regression.

### 5c. The per-tick rules already hold

`docs/process.md` §3's scaling rules (every per-tick loop states what it scales with; whole-board
loops need a dirty set or a cadence and a benchmark row; snapshot channels publish to a subscriber;
presentation scales with what is visible) were checked against the fifteen systems and hold: the
table in the code map records what each scales with. The one open term is the navigation rebuild,
and it is in flight (HT1, PR #211, 1.80 → 0.18 ms per edit at the scale target).

### 5d. The HT track, a week on

HT1 in review (#211). **HT2 not started** (no analysers, no `.editorconfig`, the packages still
there). HT3 partly overtaken by the events work — the incident ledger and the bulletin views exist;
whether alerts read events or still infer from the pawn list was not re-checked here. HT4 undecided.
**HT5 not started, and its files doubled.** HT6 unmeasured. HT7 not re-checked. **HT8 closed** by
frustum culling (FC, 2026-09-23). HT9 still ignored tests. The hardening plan was written and
approved in principle and then outpaced by fifty feature merges, which is the process finding of
this review: **a plan that waits for a gap in the feature queue never runs.** The refactoring plan
therefore proposes one cut per batch gap as a standing rule rather than a track to schedule.

## 6. What could not be determined here

No dotnet SDK, no Unity, no GPU. Not checked: whether the fast and Unity tiers are green on `main`
today beyond what `CLAUDE.md` records (EditMode 4,551 / 1 failed on 09-26, the known
`WeaponSheathGapTests` bat); the frame cost of anything; which of the 119 statics are set by a test
that forgets to restore it (a grep finds 26 test files assigning one; which order they run in
is the runner's). The
collision map is from the PR file lists on 2026-09-26 at 17:30 UTC and is stale the moment one
merges.

## 7. The plan

`docs/plans/refactoring.md`: R0 the status table out of `CLAUDE.md`; R1 the frame passes; R2 the
thing renderer; R3 one view per panel; R4 the pose stages; R5 the job pipeline's bases out; R6 the
context sealed and `Attach` everywhere; R7 the `Hud` folder; R8 hygiene and the knobs; R9 the
design index. Each names the PRs it waits for, the proof it owes and what "done" is. The ratchet
and the code map are in from this commit; nothing else in this review changes code.
