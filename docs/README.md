# Odyssey — documentation map and concepts

New here? Read [`CLAUDE.md`](../CLAUDE.md) for the working rules and current status, then this
page, then whatever the table below points you at. This page explains **where things live** and
**the handful of concepts the whole project hangs on**; it does not duplicate them. Where a
concept has one authoritative document, that document is named and wins over anything here.

## The map

| Where | What it holds |
|---|---|
| [`brief.md`](brief.md) | The governing brief: vision, agreed decisions, working agreement. Everything else descends from it. |
| [`code-map.md`](code-map.md) | **Where things live and how to add one of each**: the tick and the frame as built, every seam with its counts, the folders, a recipe per kind of addition, and the guards that fail far from the edit. Read it second. |
| [`design/`](design/) | One document per mechanic — the decisions, the measurements that decided them, and what not to undo by tidying. `00-vision.md` is the pitch; `03-systems-catalogue.md` is the no-drop contract listing every system with its milestone. |
| [`adr/`](adr/) | Ten short records of the irreversible decisions: engine and version, cell size, layer model, UI framework, the sim/UI contract, simulation architecture, layer visibility, the icon pipeline, water and the impassable bit, audio playback. |
| [`wiki/`](wiki/README.md) | The **generated** content wiki — every player-facing name with its stable key. Never hand-edit; edit the CSVs in `design/` and rebuild. |
| [`research/`](research/INDEX.md) | Research files, one question each, with sources and confidence. `INDEX.md` is the map. |
| [`plans/`](plans/) | Execution plans. `vertical-slice.md` is the ordered unit list; `refactoring.md` the cuts and the order they land in; `playtest-queue.md` is what is waiting for a person at the keyboard. |
| [`audit/`](audit/) | Audits of the whole project: `2026-09-19-baseline.md` (the tick, the monoliths, the process) and `2026-09-26-architecture-review.md` (shape, composition, what a session needs, the collision map). |
| [`milestones/`](milestones/) | Per-milestone reports (M1 world, M2 pawns, soak runs). |
| [`process.md`](process.md) | The cycle one unit of work goes through: ground → design → test → measure → hand over → merge → play → record. |
| [`journal.md`](journal.md) | The narrative record: every decision, measurement and reversal, in order. Append here; never rewrite. |
| [`lessons.md`](lessons.md) | Operational lessons that already cost time once. Read before losing an hour. |
| [`bug-patterns.md`](bug-patterns.md) | The recurring bug shapes and the check that catches each next one. Read before debugging a report. |
| [`setup/local-dev.md`](setup/local-dev.md) | Dev-machine setup for both machines, test tiers, tooling prerequisites. |
| [`reference/`](reference/) | Mockups, screenshots and audio sourcing. Reference material, not decisions. |

## Concepts

### The world is cells in layers

The board is a 3D grid of discrete cells, **2.5 × 2.5 × 3.0 m** — the size derived from the
licensed art modules, and irreversible (ADR 0002). One cell of height is one layer; there are no
slopes and no half-heights, and a built slab is simultaneously the ceiling below and the floor
above. Support is solved bottom-up and unsupported spans collapse. What may stand where —
including what the *drawing* may pretend fills a cell the simulation could fill — is governed in
[`design/02-world-and-layers.md`](design/02-world-and-layers.md).

### The tick is fixed and deterministic

The simulation steps on a fixed tick, single-threaded, through a fixed phase order (consume
intents → world systems → things → pawns → deferred structural changes → publish). Same seed,
same state hash — and that is a gate, not an aspiration: golden-master runs, byte-stable saves
and resume-equivalence tests all hang off the hash. Speeds multiply tick rate, never delta time.
Why and how: [`design/01-architecture.md`](design/01-architecture.md) §3–§4, §8.

### The seam: snapshot-read, intent-write

The simulation and everything the player sees touch only through a published immutable snapshot
(read) and an intent queue consumed at tick boundaries (write). Nothing in the simulation
references Unity, so it all runs and tests headless; a panel opened on something that dies holds
only an id and closes cleanly; intents are a replayable log. The contract is ADR 0004 and
[`design/01-architecture.md`](design/01-architecture.md) §6.

### Subsystems and directors are different words on purpose

A **subsystem** is simulation-side: registered with a phase and an order, runs inside a tick, may
mutate the world. A **director** is interface-side: runs per frame on the published snapshot and
never mutates anything. Do not unify the two words; the whole seam depends on the distinction
([`design/01-architecture.md`](design/01-architecture.md) §3a).

### The assemblies, and why the split matters

As built: `Odyssey.Sim` and `Odyssey.Sim.Contracts` (no UnityEngine), `Odyssey.Hud` (UI logic,
references only Sim.Contracts), `Odyssey.Presentation` (rendering, camera, audio, the UI Toolkit
shell), `Odyssey.Editor`, and a test assembly per area. A reflection test holds the sim to its
UnityEngine-free property, which is what makes the fast test tier possible at all.

### Content is Defs, and names live in one place

Game content is data-driven XML under `Assets/Odyssey/Defs/Core`, frozen into tables at load.
Player-facing names exist in exactly one source — the CSVs in `docs/design/` — from which two
generators build the wiki and the HUD's label registry, and tests fail the build if a name is
written literally in code instead of read from the registry. The pipeline:
[`wiki/README.md`](wiki/README.md) and the wiki section of [`CLAUDE.md`](../CLAUDE.md).

### Pawns think in an ordered scan

A colonist is not a behaviour tree: a priority-ordered list of work givers is scanned and the
first valid job wins, which keeps behaviour predictable and the scan cheap. Needs decay on a
fixed cadence; work speed comes from the skill curve, pace from condition. Jobs, reservations and
the rates: [`design/05-ai-and-jobs.md`](design/05-ai-and-jobs.md),
[`design/17-rates-and-stats.md`](design/17-rates-and-stats.md).

### Saves are sections; the hash is the contract

Binary container, per-section compression, Defs referenced by name so content can change under
an old save. Paths are never saved — they are recomputed on load — and regions, reachability and
support are re-derived rather than parsed. Format details:
[`design/01-architecture.md`](design/01-architecture.md) §7.

### Two test tiers, and neither is optional

**Fast** (`scripts/test-fast.sh`, seconds, no Unity) runs the sim and HUD tests through mirror
.NET projects built from the same sources. **Unity** (`scripts/unity.sh test editmode`) is the
authority. Both tiers gate every pull request, with the generated-content checks beside them.
What each covers and the traps between them: [`setup/local-dev.md`](setup/local-dev.md) §10 and
[`CLAUDE.md`](../CLAUDE.md) §Tests and gates.

### Presentation draws; the simulation knows

Nothing visual is in a cell, a save or the hash — grass, banks, tree colour, sound and the
see-through fade are drawn only. The deliberate exceptions are named where they are made. Two
load-bearing corollaries: what the player may click is decided by draw depth (ADR 0006), and a
façade that fills a cell the simulation could fill is a bug waiting to be reported, so it needs a
sim-side copy of its rule. See [`CLAUDE.md`](../CLAUDE.md) §Standing rules.

## Conventions before you edit anything

- **British English** in documentation.
- **Never hand-edit a generated file**: everything under [`wiki/`](wiki/), and the `*.g.cs`
  registries — edit the CSV source and rebuild.
- **`CLAUDE.md` holds what is true now;** the reasoning goes in [`journal.md`](journal.md) and
  the mechanics in [`design/`](design/). Keep status lines short and grep the code before
  trusting any of them.
- **A finished piece of work ends with a handover** — where, what changed, what to test — and a
  row in [`plans/playtest-queue.md`](plans/playtest-queue.md) if a person needs to look at it.
- **Work reaches `main` only through a pull request**, both tiers green, one approving review.
