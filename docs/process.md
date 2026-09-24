# How a piece of work moves through this project

**Written 2026-09-19 from what has actually worked** (`docs/audit/2026-09-19-baseline.md` §6 has the
evidence), not from what a process document usually says. `CLAUDE.md` holds the rules that hold
everywhere; this file is the *cycle* — the order one unit of work goes through, and the gate at
each step. When the two disagree, `CLAUDE.md` wins and this file is stale.

The whole thing on one line: **ground → decide in a design doc → test first → measure → hand over
→ merge → play → record.** Every step leaves a file; the next session knows nothing else.

## 1. The cycle for one unit

| Step | What happens | The gate that ends it | Where it is written |
|---|---|---|---|
| **Ground** | Read `CLAUDE.md`, the design doc that owns the line (`CLAUDE.md` §"Read this before touching that line"), and `docs/bug-patterns.md` if it is a report. **Grep the code before trusting any status line** — three sessions rebuilt landed work. | You can say in one sentence what exists today and what the unit changes. | nothing yet |
| **Interview** | If a decision is the owner's — a number a test cannot settle, a look, a name, a scope cut — ask it *now*, with your assumption and why it matters, and stop. Do not run the next step on an assumed answer. | The owner has answered, or the assumption is written down as one. | the design doc's decisions section |
| **Design** | A section in the owning design doc, or a new `docs/design/NN-slug.md` for a new line: the decisions, the measurement that decided each, the alternatives rejected and why, and *what not to undo by tidying*. Mechanics go here; never in `CLAUDE.md`. | Another session could build it from the doc alone. | `docs/design/` |
| **Build** | Test-first for anything in `Odyssey.Sim` or `Odyssey.Hud`. **Always write the negative control** — the test that fails when the feature is withheld. A golden hash moves only with a reason line in `Golden.cs` and `ODYSSEY_REGOLDEN=1`. Content changes rebuild the wiki and the label registry in the same commit. | Fast tier green locally (`scripts/test-fast.sh`, seconds). | the code and its tests |
| **Measure** | Anything with a cost claim gets a number, taken where the cost lives: `TickBenchmarkTests` for the tick, `FrameTimeTests` for the frame, a probe under `Assets/Editor/Odyssey/` for a look. A plausible causal story attached to a real number is still a guess (`docs/lessons.md`). | The number is in the design doc beside the decision it made. | the design doc |
| **Prove** | Unity tier on CI (`scripts/unity.sh test editmode` and `playmode`, authoritative), selected by what the PR touches (§5). Anything build-shaped — shaders, `StreamingAssets`, paths read at runtime — also gets a player build and a `-odyssey-newgame` smoke run, because two green tiers say nothing about whether the game runs. | Both tiers green on the PR; build smoke where it applies. | CI |
| **Hand over** | The reply ends with the handover from `CLAUDE.md`: the full path and branch, the *what changed* table, the *what to test* table with what a wrong answer looks like. Say what is still owed, what is blocked, and the merge order if more than one branch is in flight. | The owner can act on it cold. | the reply, and `docs/plans/playtest-queue.md` |
| **Merge** | One PR per change, `claude/<slug>`, small, one concern. Both tiers green, one review, branch up to date; branch protection enforces it. Nothing is done until it is in the branch the owner plays. | Merged to `main`. | GitHub |
| **Play** | The owner presses Play against the *what to test* table and reports. A fix that comes out of it is a **bug-patterns row** and, where it changes a decision, a design-doc amendment. | The row in the playtest queue is closed with the verdict. | `docs/bug-patterns.md`, the design doc |
| **Record** | Append the reasoning to `docs/journal.md`; update the `CLAUDE.md` status table (what is true *now*, one line); add to `docs/lessons.md` if anything took more than ten minutes to diagnose. | `CLAUDE.md` is still short. | `docs/journal.md`, `CLAUDE.md` |

## 2. Rules that keep the cycle honest

- **Fix issues before features** (owner, 2026-09-15). A report about existing work goes ahead of
  any new unit.
- **One owner per shared file** when agents run in parallel; fan out on leaves, single-thread the
  spine (`CLAUDE.md`, `INDEX.md`, the CSVs, `vertical-slice.md`).
- **Research lives in subagents**, one question each, a hard cap, the fixed return format, and the
  result written to `docs/research/`. The main thread carries conclusions, not dumps.
- **Never `git add -A`** in a shared checkout; check `git status` first. Never prune a worktree
  without checking for the Synty junction.
- **A number in a doc names its machine and its date.** A timing without either is a rumour.
- **Do not diagnose a look by reading code.** Use the probe that photographs it, or the save probe
  (`tools/dotnet/Odyssey.SaveProbe`) for a tile.

## 3. Scaling rules, added 2026-09-19

The audit found the tick healthy today and three places where cost grows with the board rather
than with what is happening on it. These rules stop the next one landing unnoticed:

- **Every per-tick loop states what it scales with** — pawns, things, dirty cells, or the whole
  board — in its doc comment. A loop over the whole board every tick needs a dirty set or a
  cadence, and a row in `TickBenchmarkTests` before it merges.
- **Anything in `TickPhase.WorldSystems` is measured under edits**, not at rest: the busy arm of
  the tick benchmark (one mined cell a tick at the scale target) is the number to move, because a
  colony at rest hides every rebuild.
- **A snapshot channel is published only to a subscriber.** The seam already has the rule for
  detail views; the per-layer slice channel is the one exception and is on the hardening list.
- **Presentation never derives a number the simulation knows** (2026-09-19, the vibration).
  Publish it as a view field or an aspect.
- **Presentation per-frame work scales with what is visible**, never with the board: chunks in
  frustum, figures on drawn layers.

## 4. What the owner does and what the agents do

The owner is the only person who can press Play, and the only one who merges. Agents write code,
tests, research and documentation, and stop at every decision that is the owner's. **Merges have
run at ten to sixty a day and playtests at a handful**, so the playtest queue is the real
constraint: keep it short, keep it ordered by which verdict unblocks the most, and prefer finishing
a line that is waiting on a verdict over opening a new one.

## 5. What CI runs, added 2026-09-24

The Unity tier runs on one machine, the owner's, so every minute it spends on one PR is a minute
every other open PR queues. Measured on the runner that day: EditMode 91 s, of which **87.5 s were
the Sim tests the fast tier had just run**; PlayMode 260 s, of which **125 s were timing arms that
assert nothing but the timing**; and every merge ran the whole Unity tier a second time on `main`,
on a tree its PR had already tested, because branch protection requires a PR to be up to date. A PR
was taking 20–40 minutes end to end, most of it in the queue.

`tools/ci/tiers.py` reads the paths a PR changes and says which tiers run. The rules:

- **By assembly, never by feature.** A path maps to the assembly it belongs to, and the tiers that
  assembly can break are the ones that run; the compiler proves those edges. Choosing tests *inside*
  a tier — "touched storage, run the storage tests" — is not done and should not be added: the
  goldens, `RegistryTests` and the source-reading lint tests fail far from the edit that broke them,
  which is the project's commonest bug (one rule, two owners), and the whole fast tier is seconds.
- **Only skipping needs a rule.** A path no row claims runs everything, so a new folder costs a full
  run until somebody decides otherwise rather than silently running nothing.
- **The measurements are not a PR gate.** `FrameTimeTests` arms whose only verdict is a timing carry
  `Category("Measurement")` and run nightly and on the label `ci:perf`. An arm that also asserts a
  structural claim (draws in colours, the picture unchanged, a budget held) stays in the gate.
- **A push to `main` runs the hosted tiers only.** The nightly (03:00 UTC) runs everything on `main`,
  measurements and the Sim tests under Mono included, and skips itself when `main` has not moved
  since its last green run. It needs the runner machine on; a queued nightly waits for it.

| A PR that touches | Sim tests + Long | Hud tests | Content gates | Unity EditMode | Unity PlayMode |
|---|---|---|---|---|---|
| only `docs/`, `*.md`, `art-source/`, the CSVs, the Python tools | — | — | ✓ | — | — |
| `Hud`, `Presentation`, `Editor`, other `Assets/` | — | ✓ | ✓ | ✓ without `Odyssey.Tests.Sim` | ✓ without measurements |
| `Sim`, `Sim.Contracts`, `Defs`, `Tests/Sim` | ✓ | ✓ | ✓ | ✓ | ✓ without measurements |
| `.github/`, `scripts/`, `Packages/`, `ProjectSettings/`, anything unclaimed | ✓ | ✓ | ✓ | ✓ | ✓ without measurements |

The Hud tests run on a Presentation change because they read Presentation's C#, style sheet and
fonts off disk. The label `ci:full` runs every row; labels are read when an event fires, so add one
and push, or run `gh workflow run ci.yml --ref <branch>` for everything. The job summary of *Select
tiers* says what was chosen and why.

**Locally the same cut applies.** `scripts/test-fast.sh --project sim|hud` runs one project, and
`scripts/unity.sh test playmode -testCategory '!Measurement'` is the PR's PlayMode run. Run a
measurement by name when it is the thing being measured.

**"Fast tier" is an aggregator**, and must stay one. GitHub reports a job skipped by its `if:` as a
pass to a required check — which is what lets a docs PR merge without the runner, and is also how a
broken selector would skip every job and merge a PR on nothing. So *Fast tier* waits for the hosted
jobs and fails if the selector failed.

<!-- CI proof: a docs-only change; this PR is closed, never merged. -->
