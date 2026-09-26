# Raids — the plan

The design is `docs/design/55-raids.md`. Branch `claude/sharp-lamport-8q5u4h`, one commit per
unit, one PR. The interview (2026-09-25) is recorded in design 55 §2.

| Unit | What | Gate |
|---|---|---|
| **R1** | Design 55, this plan, follow-on notes in designs 23 §8 and 43 §9 | docs only |
| **R2** | The arrival horn baked (`bake_raid.sh` → `alert-raid-arrive`), its catalogue row, and `SoundIds.AlertRaidArrive`. The assault horn is already `alert-raid` | measured loudness |
| **R3** | `PawnKind_Gunman` (kind 4), `RaidMixDef` and three mixes, largest-remainder composition, registry keys | fast tier; both content checks |
| **R4** | `RaidSystem` (phases, schedule, early trigger, withdrawal; save section `odyssey.raids`, hashed only while a group exists), `RaidThinkNode` ahead of `HostileThinkNode`, `RaidView` | fast tier; goldens untouched |
| **R5** | `RaidWorker`, `Incident_Raid` with its `<raid>` block, `InvokeIncident` B = size / C = mix + 1, `RaidBudget.AutoSize`, refuse at the ceiling | fast tier |
| **R6** | The debug Events tab: size slider 0–200 (Auto at 0) and mix dropdown | Hud tier; Unity compile owed |
| **R7** | The Events row plays the arrival horn (`BulletinChime`); the *Raid* alert while assaulting plays the assault horn; both jump the camera to the band | Hud tier; Unity owed |
| **R8** | `TickBenchmarkTests` with 200 raiders; index only what the numbers show; `PawnCeiling` 200 → 400 with `PawnCeilingTests` in the same commit | measurement in design 55 §11a |

**Merge order.** In unit order. R6 and R7 depend on R5's intent shape.

**Owed on the owner's machine.**
- The Unity EditMode and PlayMode tiers, because the fast tier compiles neither Presentation nor
  Editor.
- A play, following the handover table.
