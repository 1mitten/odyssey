# Plan: cover, sandbags and the barricade

**Phase 3, 2026-09-25.** Everything this plan builds on:

| What | Where |
|---|---|
| Interview | `docs/research/cover-interview.md` (27 answers, 2026-09-25) |
| Research | `docs/research/a-10-cover.md` |
| Design | `docs/design/53-cover.md` |

**Approved 2026-09-25** (owner: *"implement"*). **CV1–CV9 built the same day** on
`claude/cool-darwin-akh02q` — this session may push only there — with `claude/ranged-combat` merged in;
the Presentation half is not yet compiled in Unity. As built, and every departure: design 53 §12.
CV6b (the art) is the owner's machine's work.

It stacks on ranged combat, whose code is on `claude/ranged-combat` and not yet on `main`. Every unit
branches from there (`claude/cover`), and **merges after ranged**. If ranged merges first, rebase
nothing: merge `main` into the cover branch instead.

## Units

| Unit | What | Design | Gate |
|---|---|---|---|
| **CV1** the rule | `Combat/Cover.cs` with `Cover.BaseAt` (the one owner), `Cover.Evaluate` (the 8 neighbours, integer angle bands, the diagonal penalty, the shooter distance, the elevation grading, noisy-OR) and `CoverReport`. `BuildingDef.coverPerMille` and `coverTall` on the existing rows. A natural cover column for trees and bushes. | §2, §3 | fast tier: band constants at each boundary, mirror invariance, distance, elevation table, noisy-OR 550/220/220 → 729, no float in the file |
| **CV2** the shot | Two-stage roll in `RangedRules.Resolve`, with salts `RangedCover` and `RangedCoverPick`. `CombatDef.coverPerMille` deleted and its fingerprint re-pinned. `Projectiles.Entry.CoverCell` behind a section layout field. `LandBullet`: the covered strike and the stray intercept (500 × base × the dead-zone ramp). `CombatEventKind.Covered`. `BuildingDef.wreckRefundPerMille`. | §2d, §2e | fast tier: a covered shot costs the sandbag hit points; the intercept rate over 10k seeded shots is within tolerance; a shooter's own adjacent sandbag never intercepts; the wreck is ¼; save mid-flight with a covered bullet |
| **CV3** the pieces | Two `BuildingDef` rows at the next free handles (sandbags 5 stone fixed; barricade 5 wood or stone). Edifice ids and `EdificeLimit`. The cost classes in `NavGrid.ClassAt` and `NavGraph.StepCost`, with a one-owner test. Palette: the Security row, `Live`, a `ToolSandbag` glyph. `BuildShapes` (line-only), `BuildLabels`, `EdificeLabels`. The sandbag's description in `icon-keys.csv`. `BuildingFingerprint` re-pinned. | §4 | fast tier; the three content gates; `NatureTests` and `CellDetailTests` handle counts |
| **CV4** pass-through | `NavFlags.PassThrough`. `Standing.CanStandAt` as the one owner, asked by every picker listed in §5. The raise guard. The sweep. The audit of the 31 `IsWalkable` callers, written into design 53 §5. | §5 | fast tier per picker; **Long tier**: ten days, a sandbag ring round the start, the sweep never fires |
| **CV5** the AI | `Combat/CoverPosition.cs`, used by `AttackRangedJobDriver` for bandits and undrafted defenders. The draft is unchanged. | §6 | fast tier: a bandit picks the covered cell, never backs off more than 2 cells, never dances between shots; the drafted colonist holds |
| **CV6** drawing | Placeholder sandbag pieces in code and the neighbour-mask join in `ChunkMesher`. The barricade rows in `PlayScene.cs` (wood → Western Frontier timber, stone → Meadow stone wall). `StandHeight`, `MarkHeight`, `ModuleForEdificeAt`, the ghost. The health bar over any struck building. The *Cover* floater and its dust. | §7 | Unity EditMode and PlayMode; a player-build smoke |
| **CV6b** the art | **Done in code instead, 2026-09-25** (design 53 §7a-bis): `SandbagMesh`, one bag, laid bag by bag by `CoverShape` in running bond, after research `e-12`. The Blender set is not needed unless the owner's eye says otherwise. The barricade was taken out on the same look (§13). | §7a-bis | Unity tiers; the owner's eye |
| **CV7** the crouch | The publish-time `CoverCrouch` aspect. The gesture stoop under the aim pose, with the depth measured from the mesh. | §8a | fast tier for the aspect; the owner's eye for the pose |
| **CV8** the readout | The `QueryShot` question intent, `ShotReportView`, the cursor tooltip. `ui.combat.*` keys with the wiki and labels rebuilt. | §8b | fast tier; the content gates; Unity tiers |
| **CV9** the gate | A headless skirmish on three seeds — pistol bandits against colonists behind a sandbag line and in the open — checked for: the hit ratio near the rule, sandbag wear, a lockstep twin, and a save mid-fight resuming the same. `Measurement` arms for `Cover.Evaluate` and `CoverPosition.Find`, with controls. `GoldenColonyProbe` against `claude/ranged-combat`. Playtest-queue rows. The CLAUDE.md status line. | §11 | the Long tier; both Unity tiers; the probe clean |

## Order

- CV1 → CV2 → CV3 → CV4 must go in sequence: the pieces need the rule, and pass-through needs the
  pieces.
- CV5 needs CV1 and CV4.
- CV6–CV8 need CV3 and can run in parallel.
- CV9 is last.

## Later units (design 53 §9)

- the embrasure
- the lean round a corner
- repair for every building
- boulders as simulated cover
- the real recipes, and possibly a sand commodity
- target choice that prefers exposed enemies
- melee bandits bashing cover
