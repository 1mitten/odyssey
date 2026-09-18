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

## 2026-09-18 — the growing-zone runs (U50)

Branch `claude/growing-zones` at `888ee8a` (the field soak is `0144969`, the Dissolve fix `5713ad9`).
Same machine as the entries above — Windows dev machine, Ryzen 7 9800X3D, CoreCLR (dotnet SDK
8.0.425, net8.0). Four runs: the three standard ten-day `Scenario_Bare` seeds, then the new
`TenDaysOnAField`, which is the same scenario with an eight-by-eight carrot field painted beside
the start before tick one. The report line gained sow/harvest counters and the carrots-on-the-map
figure, so the table grows those columns.

| Run | Ticks | Result | Wall | ms/tick mean | ms/tick p95 | haul | eat | sleep | wander | sow | harvest | Meals left | Carrots on map | Final hash |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Bare, seed 1 | 600,000 | passed | 1.9 s | 0.003 | 0.005 | 14 | 92 | 55 | 2,664 | 0 | 0 | 52 of 144 | 0 | `dc6eb87e01951758` |
| Bare, seed 2 | 600,000 | passed | 1.9 s | 0.003 | 0.004 | 15 | 94 | 55 | 2,613 | 0 | 0 | 50 of 144 | 0 | `53df1cb625080300` |
| Bare, seed 3 | 600,000 | passed | 1.9 s | 0.003 | 0.004 | 13 | 93 | 55 | 2,590 | 0 | 0 | 51 of 144 | 0 | `e610511ad034200f` |
| Field, seed 1 | 600,000 | passed | 3.9 s | 0.006 | 0.008 | 14 | 171 | 55 | 2,396 | 192 | 128 | 80 of 144 | 533 | `35befeeaf03415ea` |

- **Every hash differs from the 2026-09-16 rows, as expected** — beds, the build botch and their
  content landed between the commits, and a hash is only ever comparable within one commit. The
  three bare hashes were printed identically by the tree *before* the `Dissolve` fix, which is the
  locality check: the fix changes zone folding only, and nothing in a zone-free run touches it.
- **The field fed the colony, and the loop closed.** 192 sowings and 128 harvests in ten days;
  533 carrots on the map and 64 of 64 zone cells still planted, so every harvested cell re-entered
  the sowing queue — the continuous loop costs no code beyond the harvest itself. The colonists ate
  171 meals' worth where the bare run eats 92: crops carried roughly a third of the diet, and 80
  meals were left where the bare run ends on 52. OQ-39's pantry has its first producer.
- **Cost is still far under budget.** 0.006 ms a tick mean against the bare run's 0.003 with 64
  planted cells live; the growth pass is O(planted) every 250 ticks and it does not show. The
  render-side cost of a field is a different number and a different test — see
  `docs/design/22-growing.md` §6 for the two-thousand-cell frame figure and its caveats.

Nothing failed, so no row was added to the overnight queue by this run.
