# Plan: deep mining

**Phase 3, 2026-09-26.** Everything this plan builds on:

| What | Where |
|---|---|
| Interview | `docs/research/deep-mining-interview.md` (16 answers, 2026-09-26) |
| Research | `docs/research/a-12-ore-by-depth.md`, `docs/research/a-04-cave-ins-and-prospecting.md` |
| Design | `docs/design/62-deep-mining.md` |
| Earlier | `docs/research/mining-interview.md`, `docs/design/28-map-size.md` §11 |

**Waiting for the owner's approval. No unit is started.** On approval the first session builds DM1
(and DM4 if it fits), then stops for review.

## Units

| Unit | What | Design | Gate |
|---|---|---|---|
| **DM1** rock is free | Solid terrain gets no navigation region (`NavGraph.FloodBlock`); the underground draw walk stops a fixed depth below the slice (`SliceSettings.LowestDrawnLayer`); the door list rescans only chunks whose version moved (`DoorDirector.EnsureDoorList`). No gameplay change. | §2, §3 | fast tier; reachability and path tests unchanged; `NavGraphStatisticsTests` updated deliberately; goldens re-baked **with `GoldenColonyProbe` showing identical colonies**; arms at 16/24/32 layers on Standard and Huge, before and after in one run — **the edit tick at 32 no worse than today's at 16**; the frame ten layers down, before and after |
| **DM2** 32 layers | Map sizes, `SiteRules.MountainLayers`/`BoardLayers` and the bootstrap default to 32. Old saves keep their own size. | §2d, §5a | fast and Long tiers; memory, generation and frame on all four boards recorded in `28-map-size.md`; goldens re-baked and measured |
| **DM3** strata and minerals | Deep stone; copper ore, gold ore, gems, Emberquartz as terrains and items; the ore table collapsed to one owner (XML); banded placement with shapes; larger deep caverns kept inside the cave-in span; a yield table in `MineJob`. | §5 | fast tier: the census per band per seed, no deposit outside rock, every cavern ceiling within the span; content gates (wiki, labels, icons); fingerprints re-pinned deliberately; goldens re-baked and measured |
| **DM4** Dig and Mine | Soft ground reads *Dig*; a Mine drag's rock-only mode chosen once from its start cell. | §4 | fast tier: a drag from rock across grass marks no grass, a drag from grass marks both, the decision never changes mid-drag; `RegistryTests`; content gates |
| **DM5** fog | The Unseen bitset, set by `CavernPass`, saved and hashed while non-empty; reveal by flood fill on a breach; unseen cells drawn and picked as rock; **the inspect pane stops naming undiscovered ore**. | §6 | fast tier: a sealed cavern draws and picks as rock until breached, a breach reveals the whole cavern once, save/load keeps it, the pane never names undiscovered ore; Unity EditMode for the mirror |
| **DM6** prospecting | The Prospect order and job; radius 3, one per skill band; revealed ore glows. | §7 | fast tier: the radius, no voids revealed, the skill band; content gates |
| **U44** stairs | The existing M3 unit, its own design. The prerequisite for hauling anything up. | — | its own |
| **DM7** cave-ins | A rock support maximum giving a ~6-cell span; rock drops at support 0; rubble counts as support; unsafe cells published, tinted and alerted; the Mine cursor's preview; harm through `Hurt` with a top-of-body set, capped to down; the mine prop; old saves grandfathered (on the owner's word). | §8 | fast tier: a span of 7 falls and 6 holds, a prop holds a span, no chain across a mine, a cave-in downs and never kills, a loaded wide room does not fall until edited; Long tier: ten days with a dug mine and no unexpected collapse; the support pass measured on a 1,000-cell mine |
| **DM8** smelter | A general recipe with ingredient lists and a sibling work giver; the smelter with a coal-or-wood hopper; iron and copper bars; bars spent where the owner decides. | §9 | fast tier: a bill makes bars from coal and from wood, coal the cheaper; content gates; Unity tiers |

## Order

- DM1 → DM2 → DM3: depth is made cheap before the board gets deeper, and the bands need the depth.
- DM4 at any time; it is small and independent.
- DM5 after DM3 (the larger caverns), DM6 after DM5.
- DM7 after DM3 (the caverns must be generated inside the span first).
- U44 in parallel with any of them; DM8 after DM3.
- Every unit is its own branch and PR, with a playtest-queue row when it lands.

## Performance guard rails

- Nothing new loops over every cell per tick. The support frontier, a flood fill on a breach and a
  prospect's radius are the only new work, and each is bounded by what was edited.
- Every number is taken with a control in the same run, on the played map (`PlayedMap`), with the
  machine and resolution beside it.
