# M2 report — Pawns

Written 2026-09-16 on the Windows dev machine (AMD Ryzen 7 9800X3D, 32 GB, Windows 11 build 26200,
NVIDIA RTX 5070 Ti, Unity 6000.3.24f1, URP 17.3.0, dotnet SDK 8.0.425 running the net8.0 test
project under CoreCLR), branch `main` at `37bb63d`. Every Unity command below was run in the
sibling checkout `D:\code\odyssey-ui` at the same commit, because the owner had the editor open on
the primary one.

**This is a stop-for-review report.** Everything in the standing gate is green. The two things
worth an argument are in §5: one open row that keeps a green gate item resting on a manual run
rather than a test, and a frame-time number that moved and has not been explained.

## 1. What M2 delivered

M2's goal in `08-milestones.md` is "colonists live in the ruin, move through it in three
dimensions, and look right doing it". All seven units exist and are tested.

| Unit | State | Where it is proven |
|---|---|---|
| **U17 Region graph** | Done. Regions per layer, portal edges, connectors registered from stamped stairs, incremental rebuild tested against a full rebuild as its oracle. | `NavigationSystemTests`, `StampedConnectorTests` |
| **U18 Pathfinding** | Done. Layer-aware A-star with deterministic tie-breaks, per-agent replan budget, paths invalidated by edits, reachability answered by district id before any path is computed. **2.4× faster than naive with budget exhaustion down 91%** (`05-ai-and-jobs.md` §6, the experiment that also falsified its own premise). | `PathingTests` |
| **U19 Needs and mood** | Done. Food, rest and joy on the 150-tick cadence; mood as base plus summed thought offsets with drift; one break behaviour. | `PawnTests` |
| **U20 Skills** | Done (OQ-14). Experience per skill, a passion rolled once from the seed and the pawn id, level 0–20 read off experience by the table and never stored, daily decay. | `SkillTests`, 13 tests |
| **U21 Job pipeline** | Done. Think tree, work givers on a sorted list, drivers and toils, all-or-nothing reservations released on any job end. | `PawnTests`, `WorldSystemTests`, and the reservation check every 1,000 ticks inside the soak |
| **U22 The first jobs** | Done, and gone further than the unit asked: haul, eat and sleep, plus **felling** and **stockpile stacking with re-stow** (OQ-24), which belong to M3. | `FellJobTests`, `StockpileTests` |
| **U23 Characters and animation** | Done in substance, **not as written**. See below. | `PawnPoseTests`, `GaitBlendTests`, `WorkSwingTests`, `ArmIkTests` |
| **U24 Pawn presentation** | Done. Pawns render, animate and are culled with the slice. | `PawnPoseTests`, `FrameTimeTests` (PlayMode) |

**U23 did not happen the way the plan wrote it, and the difference is worth recording.** The plan
says six clips — carry, mine, build, sleep, eat, downed — are retargeted from Mixamo into
`Assets/Art/`. None were, because none were needed yet and one of them turned out not to exist
anywhere: there is **no work animation in any of the 7,222 imported assets**. A colonist felling a
tree stood breathing in the idle for ten seconds and then the tree fell over. The answer was to
**compute the pose rather than author it** — `WorkSwing` turns a stroke phase into shoulder, elbow
and spine angles on the Humanoid rig, `ArmIk` puts the off hand on the haft, `WorkStance` steps the
drawn figure in to where its blade meets the wood, and `ChipDirector` throws debris on the frame
the blade lands. It stands in for art we do not have and goes when real clips exist. The design is
`06-rendering-and-camera.md` §6a.

## 2. The standing gate (`08-milestones.md`)

| Part | Requirement | Result |
|---|---|---|
| 1 | EditMode and PlayMode pass, no new Sim warnings | **Green.** EditMode **494 total, 492 passed, 0 failed** (two `[Explicit]` benchmarks skipped). PlayMode **7 of 7** — a real gate now, where at M1 it was vacuous. Zero `warning CS` in the whole EditMode compile log. |
| 2 | Same seed, same hash, two processes | **Green, and cross-runtime.** `Odyssey.EditorTools.OneDay.Run` under Unity (Mono) and `SoakRunTests.OneDay` under CoreCLR both end a 60,000-tick day on seed 1 at hash `e134005c5408818d`. Two processes, two runtimes, two toolchains, one number. It is still not a *standing* test — that is OQ-05, and it is open. |
| 3 | Save at N, load, run to N+K equals uninterrupted | **Green on a real world**, which at M1 it was not. `WorldRoundTripTests`: saving twice gives identical bytes, a loaded world resumes identically, derived state is rebuilt rather than restored, a world whose grid has been edited still resumes, reservations come back agreeing with what the pawns hold, and a save is refused by a world it does not describe. |
| 4 | Headless one-day run, zero errors | **Green.** `OneDay`: 120 × 120 × 16, seed 1, 60,000 ticks, **0 errors**, 5 colonists at the end, setup 51 ms, tick mean **0.002 ms**, worst 4.78 ms, **0 ticks over 5 ms**. |
| 5 | Report written; a clone without Synty passes every Sim test | **Green.** This file. `scripts/test-fast.sh` — no Unity, no Synty, no `Assets/` at all beyond the source — runs **329 Sim and 29 Hud** tests green, and the Long tier **9** more. |

## 3. The milestone demo, and the thing it could not prove until this week

> **Milestone demo:** three pawns live in a ruined shell — they walk upstairs, sleep, eat from a
> store and haul items, unattended, for a day.

`M2DemoTests`, `Category("Long")`, 60 × 60 × 5 ruined city, three colonists, 60,000 ticks:

```
[M2] ruined city 60x60x5, 3 colonists, 60,000 ticks in 0.1 s. Storeys used: 1, 2, 3.
     Jobs completed — haul 4, eat 6, sleep 3.
     Placement: 3 colonists, 12 meals, 5 beds, 9 stockpile cells, 8 salvage, from 41 spots
```

**The demo was green for a day before it could prove its own point, and that is the most useful
thing in this report.** The run asserted no exception, no colonist lost, needs recovering, and
haul, eat and sleep all completed — on stamped shells and rubble rather than on a meadow — and
hashed identically twice. What it could not assert was *upstairs*. The colony was placed by a
single spiral that took the nearest walkable layer per column, and on a city map nearly every
column is walkable at the start layer, so the beds, the food and the store all landed on the floor
the colonists woke up on. Measured at the time: thirty-three spots, one storey, a full day, not one
layer change. The finder's layer spread was not the lever either — it is a fallback for columns
with nothing walkable below, not a preference, and raising it changed nothing.

That was filed as OQ-47 and fixed rather than asserted-and-weakened. A scenario now names the
storey its meals, beds and stockpile go on; the demo puts the store on the start floor, the beds
one above and the food two above, and asserts that **every** colonist changed storey.

**The control is a test, not a memory.** `ADayOnOneFloorNeverTouchesAStair` runs the same map and
the same seed with the offsets removed and asserts that nobody changes layer. So the stairs in the
demo are the consequence of the scenario and not something the generator would have given us
anyway — and if some other reason to change storey ever appears, that test fails and tells us the
demo has stopped being a clean measurement.

M2's layer claim therefore now rests on two independent things: `StampedConnectorTests`, which
proves a colonist *can* reach an upper storey on the graph, and this run, which shows one *doing*
it because what it needs is up there.

## 4. Measured

**The ten-day soak** (`SoakRunTests.TenDays`, three seeds, 600,000 ticks each, 120 × 120 × 16
barren natural map, five colonists), re-run on `37bb63d` for this report and appended to
`soak-runs.md`. Run twice: the three hashes were identical both times.

| Seed | Wall | ms/tick mean | p95 | haul | eat | sleep | wander | failed | Meals left | Longest at zero | Final hash |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 1.0 s | 0.002 | 0.002 | 14 | 92 | 53 | 2,595 | 0 | 52 of 144 | 0 / 0 / 0 | `d037ff3042031430` |
| 2 | 0.9 s | 0.002 | 0.002 | 15 | 92 | 55 | 2,604 | 0 | 52 of 144 | 0 / 0 / 0 | `21c8902b93f9f322` |
| 3 | 0.9 s | 0.002 | 0.002 | 13 | 94 | 55 | 2,602 | 0 | 50 of 144 | 0 / 0 / 0 | `0c33a2c3a8e94a55` |

Five colonists eat 92 meals in ten days — 1.8 a day each, which is the vanilla figure in
`a-08-plants-growing-food.md` §2 — and nobody spends a single tick with a need at zero.

**Everything else on record**, cited rather than re-measured here: support solve 54 ms for 2.5M
cells and 0.003 ms an edit; worldgen 287 ms natural and 429 ms ruined city at 250 × 250 × 40
(OQ-02); a save of 0.70 MiB natural and 1.10 MiB city against a 4 MiB budget (OQ-08); the per-tick
hash trace costing +174% on a real colony, which is a bisecting tool and not a soak tool (OQ-06);
frame time under the real player loop at **0.99 ms** on the wooded board and **1.56 ms** on the
city against a 5 ms budget.

## 5. Not done, and two things that are not clean

**U23 and U24, the owner's half.** Everything in §1 is the headless half. Nobody has yet looked at
a colonist in `Play.unity` on a machine with the Synty packs and said the animation reads right —
the gait blend, the axe swing, the step-in to the tree and the chips are all tuned against contact
sheets (`scripts/unity.sh shot Odyssey.EditorTools.SwingCheck.Run`) rather than against a playing
game. That is owner work and it is the one part of "look right doing it" a test cannot close.

**OQ-05 is open, and gate part 2 is the weaker for it.** The cross-runtime hash agreement in §2 is
a real measurement made twice by hand today; it is not a committed table that fails a build when it
drifts. Until OQ-05 lands, a determinism regression is something a person has to notice.

**A frame-time number moved and has not been explained.** The ruined city is on record at 0.88 ms
and now measures 1.56 ms. The city has no water on it, so the water work is unlikely to be the
cause and different measurement conditions are the likelier answer — but "likelier" is not an
explanation, and the baseline re-run that would settle it (OQ-43) was attempted and refused because
the editor was open. It sits inside a 5 ms budget either way, which is why it is a note and not a
red result.

**Deferred out of M2 by the Q8 fast-track and still deferred:** social and trait depth, more than
one mental-break behaviour, and the remaining Lane A/B/C research rows.

## 6. Stop

Per the plan, work stops here for review.

M2 is closed. M3 is already under way ahead of itself — designations, felling and stockpiles are
merged, and mining is written on `claude/mines` and unmerged — so the recommended next step is not
a new feature but the seam work in `vertical-slice.md` under "Where the seams are": the mining line
is one feature in 73 files that had to edit six shared files to add itself, five of which should
have been extension points. OQ-15 and OQ-16 (content to XML Defs) first, then OQ-44, OQ-45 and
OQ-46, then merge mining into a codebase that has somewhere to put it.

The one thing that blocks a different line of work entirely is OQ-40: **mouse input cannot be
driven in a PlayMode test at all**, so nothing about pointer behaviour — the scroll-over-HUD guard,
drag-to-designate, ADR 0003's F3 — can be tested yet. Three tests written against it passed
vacuously and were deleted. That harness has to be solved before U25's designate tool is built,
not after.
