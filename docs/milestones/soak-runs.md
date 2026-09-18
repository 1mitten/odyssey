# Soak runs

An append-only log of the unattended runs (`Tests/Sim/SoakRunTests.cs`, `Category("Long")`).
Each entry records what one run of the ten-day gate measured, on which commit and which
machine, so that a later run can be set beside it and a drift in cost, behaviour or hash is
seen rather than suspected. Newest entries go at the bottom. Nothing here is ever edited after
the fact; a correction is a new entry that says what it corrects.

The command is:

```
ODYSSEY_TEST_ALL=1 bash scripts/test-fast.sh --filter "FullyQualifiedName~SoakRunTests.TenDays" --logger "console;verbosity=normal"
```

which prints, per seed, the wall time, the mean and p95 cost per tick, the jobs completed and
failed per kind, the longest stretch any need sat at zero, the meals left, and the final state
hash. The run asserts along the way (no exception, every pawn alive and in the snapshot, no need
at zero for more than 2,000 consecutive ticks, the reservation table agreeing with the pawns
every 1,000 ticks, at least one haul, meal and sleep completed), so a row that says *passed*
means all of that held for 600,000 ticks.

What a row means: **commit** is the simulation code that ran, **machine** is where, **ms/tick** is
the mean over 100-tick samples and the 95th percentile of those samples, **jobs** are the per-def
completed counters (`JobSystem.CompletedOf`), and the **hash** is `SimWorld.ComputeStateHash` at
tick 600,000 — the number a second run on the same commit and seed must reproduce exactly, on
any machine and either runtime (CoreCLR here; Mono under `scripts/unity.sh`).

## 2026-09-16 — first entries, with skills in (OQ-12, after OQ-14)

Commit `8178b23` (OQ-14: skills land). Scenario `Scenario_Bare` on the play-scene size,
120 × 120 × 16, barren natural map, five colonists, twelve piles of twenty meals, five beds, nine
stockpile cells, eight loose salvage, no standing orders. Machine: the Windows dev machine, AMD
Ryzen 7 9800X3D, 32 GB, Windows 11 (build 26200), dotnet SDK 10.0.401 running the net8.0 test
project (CoreCLR). Wall times are one run each, not medians.

| Seed | Ticks | Result | Wall | ms/tick mean | ms/tick p95 | haul | eat | sleep | wander | wait | failed | Meals left | Longest at zero | Final hash |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 600,000 | passed | 1.2 s | 0.002 | 0.003 | 9 | 164 | 55 | 2,565 | 0 | 0 | 76 of 240 | 0 / 0 / 0 | `5ffbc9979d41905e` |
| 2 | 600,000 | passed | 1.1 s | 0.002 | 0.003 | 10 | 164 | 55 | 2,577 | 0 | 0 | 76 of 240 | 0 / 0 / 0 | `fa35d7b009055765` |
| 3 | 600,000 | passed | 1.1 s | 0.002 | 0.003 | 7 | 164 | 55 | 2,552 | 0 | 0 | 76 of 240 | 0 / 0 / 0 | `d14c70dd0234a4f8` |

("Longest at zero" is food / rest / joy, in ticks, against a limit of 2,000. "failed" is haul,
eat and sleep failures summed; every one was zero.)

What the three rows say, read together:

- **The gate passes on three maps, not one.** Seeds 2 and 3 were run for the first time here;
  until this entry `TenDays` had only ever run seed 1. All three land the colony somewhere
  different and scatter the salvage differently, and none of them found a fault the first did
  not.
- **The needs side is seed-independent, and that is expected.** Eat 164, sleep 55 and 76 meals
  left are identical on all three seeds because nothing in the needs arithmetic draws a random
  number: the fall rates are per band, the wake threshold is fixed, and the meals are the same
  count on every map. The only randomness in the colony is the mental-break roll (none fired in
  any run: mood never reached the threshold) and the wander target. What differs by seed is the
  map: how many hauls the eight salvage items take (7 to 10, since a pawn interrupted mid-carry
  drops and re-hauls) and how many wander jobs fit around the eating and sleeping.
- **Meals are on track for the pantry as sized.** 164 sittings over ten days for five colonists
  is 16.4 a day, which is the burn OQ-39 measured; 76 left of 240 puts the colony about 4.6 days
  from empty at the end, so the ten-day gate would not pass at 14 days on this pantry. That is
  the scenario's number, not a fault (nothing in the slice makes food).
- **Skills are in the hash now** (OQ-14, same commit). The haulers earned hauling experience on
  their carry ticks; nothing in `Scenario_Bare` trains cutting, and no colonist reaches level
  ten in ten days at this rate of work, so the decay path ran but had nothing to take. The
  hashes above are therefore the first with skill state in them and are not comparable to any
  hash printed before `8178b23`.
- **Cost is far under budget.** 0.002 ms a tick mean, 0.003 at p95, on a map with no
  pathfinding load to speak of (a barren board, five pawns, short walks); the D1 benchmark
  figure of 1.4 ms is for 250 × 250 × 40 with a structured map and fifty pawns, and OQ-19 is the
  row that measures that again with the real pathfinder. Nothing here should be read as a
  performance result beyond "the ten-day run costs about a second".

Nothing failed, so no row was added to the overnight queue by this run.

## 2026-09-16 — the M2 report run (OQ-21)

Commit `37bb63d` (`main`, immediately after OQ-47). Same scenario and machine as the entries
above — `Scenario_Bare`, 120 × 120 × 16 barren natural map, five colonists, twelve piles of meals,
five beds, nine stockpile cells, eight loose salvage, no standing orders — on the Windows dev
machine under CoreCLR (dotnet SDK 8.0.425, net8.0). Run **twice**; the three hashes were identical
both times, and the job counts with them.

| Seed | Ticks | Result | Wall | ms/tick mean | ms/tick p95 | haul | eat | sleep | wander | wait | failed | Meals left | Longest at zero | Final hash |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 600,000 | passed | 1.0 s | 0.002 | 0.002 | 14 | 92 | 53 | 2,595 | 0 | 0 | 52 of 144 | 0 / 0 / 0 | `d037ff3042031430` |
| 2 | 600,000 | passed | 0.9 s | 0.002 | 0.002 | 15 | 92 | 55 | 2,604 | 0 | 0 | 52 of 144 | 0 / 0 / 0 | `21c8902b93f9f322` |
| 3 | 600,000 | passed | 0.9 s | 0.002 | 0.002 | 13 | 94 | 55 | 2,602 | 0 | 0 | 50 of 144 | 0 / 0 / 0 | `0c33a2c3a8e94a55` |

**Every hash differs from the `8178b23` entries above, and that is expected, not a regression.**
Between the two commits the simulation gained water on the generator, felling, designations and
stockpile stacking, and the ration and the pantry were re-tuned (OQ-29, OQ-24): the meal pile is
twelve of twelve rather than twelve of twenty, which is why "meals left" reads 52 of 144 here and
76 of 240 before. A hash is only ever comparable within one commit.

**What moved in behaviour.** Hauls are up (7–10 → 13–15) and meals eaten are down (164 → 92) on
the same ten days. The meals figure is the ration change doing exactly what OQ-29 said it would:
five colonists now eat 1.8 meals a day each, the vanilla figure, where before they ate double.
Nothing sat at zero on any need on any seed, and no job failed.

Nothing failed, so no row was added to the overnight queue by this run.

## 2026-09-18 — WS2, work speed follows the skill curve (9ae7682)

Commit `9ae7682` (`claude/rates-and-stats`, WS2 of design 17). Same scenario and machine as the
OQ-21 entry above — `Scenario_Bare`, 120 × 120 × 16, five colonists, no standing orders — run
once, each seed freshly built and ticked 600,000 times.

| Seed | Ticks | Result | Wall | ms/tick mean | ms/tick p95 | haul | eat | sleep | wander | wait | failed | Meals left | Final hash |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 600,000 | passed | 1.9 s | 0.003 | 0.004 | 14 | 92 | 55 | 2,664 | 0 | 0 | 52 of 144 | `362b4c847c79cebb` |
| 2 | 600,000 | passed | 2.0 s | 0.003 | 0.005 | 15 | 94 | 55 | 2,613 | 0 | 0 | 50 of 144 | `0e3ff437f26f5f8d` |
| 3 | 600,000 | passed | 1.9 s | 0.003 | 0.004 | 13 | 93 | 55 | 2,590 | 0 | 0 | 51 of 144 | `ad58de4ae2cee30a` |

**The economy is byte-for-byte the WS1 baseline's, and that is the measured result, not a stale
run.** The comparison this entry exists for: every count and every hash equals the run captured
at `f6beb56` before the curve landed. The reason is the same one the goldens gave — the slice
runs no job whose pace a curve moves. Its tallies are haul, eat, sleep and wander; eating,
sleeping and idling are needs-driven and rate-free, and hauling is the one work type with no
rate skill, flat at 1,000 by the design's own rule that a skill drives either rate or quality.
Mining, cutting and construction — the three the curve slows to 0.55–0.70× for a novice — are
not designated in this scenario at all. The curve's effect on work speed is therefore proved by
the exactness tests (`WorkRateTests`: the level-20 face in the curve's ratio of ticks, two
miners of different skill emptying one cell together, the felling grant count walked along the
rising curve) and will first show in soak when a slice carries standing orders. Wall time
doubled against OQ-21 (1.0 → 1.9 s) with the same counts and the same work in them, and the
per-tick mean reads 0.002 → 0.003 — both are machine noise at a cost this small, not the curve,
which this run never asks anyone to swing a pickaxe fast or slow against.

Nothing failed, so no row was added to the overnight queue by this run.

## 2026-09-18 — WS3, a pace of her own (a4413df)

Commit `a4413df` (`claude/rates-and-stats`, WS3 of design 17). Same scenario and machine as the
WS2 entry above — `Scenario_Bare`, 120 × 120 × 16, five colonists, no standing orders — run once,
each seed freshly built and ticked 600,000 times.

| Seed | Ticks | Result | Wall | ms/tick mean | ms/tick p95 | haul | eat | sleep | wander | wait | failed | Meals left | Final hash |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 600,000 | passed | 2.0 s | 0.003 | 0.004 | 15 | 92 | 55 | 2,677 | 0 | 0 | 52 of 144 | `d6373d172e4f8cd7` |
| 2 | 600,000 | passed | 2.0 s | 0.003 | 0.005 | 15 | 93 | 54 | 2,667 | 0 | 0 | 51 of 144 | `7f5abb4158e70db7` |
| 3 | 600,000 | passed | 2.0 s | 0.003 | 0.004 | 13 | 92 | 54 | 2,776 | 0 | 0 | 52 of 144 | `085e7a6b0644f2d3` |

**Every hash moved against WS2, and this time the movement is the measured result.** WS3 is the
first change since the slice began that reaches `Pawn.MoveProgress`: each colonist now walks at a
pace of her own, 850 to 1,150, where before every colonist walked in step at exactly 1,000. The
wander count is up on all three seeds (2,664 → 2,677, 2,613 → 2,667, 2,590 → 2,776) — a quicker
walker finishes a stroll in fewer ticks and fits more of them into the same ten days, and a
colony of five different paces fits a different number of them per seed. The jobs that matter
stay within a whisker of the baseline (haul, eat, sleep and meals left each move by at most one):
eating and sleeping are needs-driven and only ride along on the length of a walk. Seed 1's extra
haul is the same effect seen where it pays — one more salvage run fits into ten days when the
average walker is a little quicker.

**The comparison the unit's done criterion asks for — against a run with condition disabled — is
vacuous here, and that is worth recording rather than manufacturing.** No need touched zero on
any seed on any of the ten days ("longest at zero" is 0 / 0 / 0), so the starvation bar never
grew, no collapse fired, and condition sat at its ceiling of 1,000 throughout: a
condition-disabled run would be this run, tick for tick. The spiral the design worries about —
the bar stepping both rates down, recovery by the same number, rest running out dropping the
colonist where she stands — is proved by the exactness tests (`MoveRateTests`: the three bands
against the bar, the ceiling that nothing may push past, the floor of 700 reaching both rates
from one place, the collapse with the tired-still-walks control, the mid-walk collapse), and it
will first show in soak when a slice runs a pantry that can empty.

Nothing failed, so no row was added to the overnight queue by this run.
