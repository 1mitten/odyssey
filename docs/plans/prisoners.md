# Prisoners — the plan

- **Design:** `docs/design/59-prisoners.md`.
- **Interview:** `docs/research/prisoner-interview.md`.
- **Reference:** `docs/research/a-20-prisoners.md`.
- **Branch:** `claude/prisoner-bed-assignment-98afc0` for P0. Each later unit was to get a
  short-lived `claude/*` branch and a PR, in unit order; **as built, all twelve landed on P0's branch**
  and were reviewed together (design 59 §15d, §16).

**Phase gate.** P0 is documents only. **No unit below P0 starts until the owner approves this
plan.**

**How every unit is built:**
- Tests first, in the fast tier (`Assets/Odyssey/Tests/Sim`, `Tests/Hud`, `scripts/test-fast.sh`).
- The Long tier before merging.
- A Unity run for anything touching Presentation or the HUD shell.
- A handover with both tables.

| Unit | What | Gate | Player sees |
|---|---|---|---|
| **P0** | Research a-20, the interview, design 59, this plan | docs only | — |
| **P1** | **One bed rule.** `BedRules.CanUse / MayOwn / CountsForBeds`. The four choosers (`TrySleep`, `Medical.BedFor`, `RescueRules.BedFor`, `TryClaimForSleeper`), `AssignOwnerAt` and the owner picker all route through it | fast tier; Unity (picker) | The bed picker stops offering hogs and bandits |
| **P2** | **Custody and `Allegiance`.** `Pawn.Custody` (hash bits 28–29), `Allegiance` as the one owner, `PrisonRecord` + `odyssey.prison`, `PawnView.Custody`, the outfit owner reading custody, a debug *Imprison* row | fast tier; goldens untouched | A debug prisoner is ignored by raiders and leaves the roster |
| **P3** | **Contracts and the one golden bake.** Social (`SkillIndex` 9, format 10 → 11, `BackfillSkills`), `WorkHandle.Warden` 8, job handles 28–35, thoughts, `PrisonPurpose` constants, fingerprints | fast + Long; goldens re-baked **once**, measured with `GoldenColonyProbe` | Social on the skill pane |
| **P4** | **Bed purpose and cells.** `odyssey.bedpurpose`, `SetBedPurpose` (normalises the room), `EnclosureGrid.Generation`, `PrisonCells`, eviction, the bed-pane toggle, the first registry keys | fast tier; content gates; Unity | Mark a bed Prisoner and its room follows |
| **P5** | **The prisoner mind.** `PrisonerTree` (needs, shackled, cell wander), Bandit mode, colonist healing and recovery, no breaks | fast + Long | A prisoner walks the cell and sleeps in its bed |
| **P6** | **Capture.** `Capture` driver and emergency giver, the menu (Capture / Finish off), the capture mark, the doctor treating prisoners, leaving the band, the jumpsuit, the Warden column live, *No prison bed* | fast; Unity; content gates | Downed raiders carried to cells, in orange |
| **P7** | **Feeding.** `FeedPrisoner`. Food in a cell is **not hauled out and not eaten by colonists** (design 59 §14) | fast + Long | Wardens bring meals |
| **P8** | **Recruitment.** `Chat`, `Recruitment.Factors`, the prisoner tab (modes, bar, ETA, blockers), joining, the bulletin | fast; Unity | A prisoner can be won over |
| **P9** | **Escape.** `EscapeRisk.PerDay`, the hourly roll, `EscapeTree` (door bashing), the cycling alert, recapture | fast + Long | Breakouts, and why |
| **P10** | **Release and Exile**, `Escort`, `LeaveFree`, the arrested colonist's return, the Ransom seam | fast | Letting people go |
| **P11** | **Surrender** | fast + Long | Raiders yield |
| **P12** | **Arrest** — menu row, resistance, the two thoughts | fast + Long | Arresting a colonist |
| Later | Execute · prison labour · ransom (M7) · kidnap (design 33 §17f) · room roles | — | — |

## Tests first, unit by unit

### P1

- `BedRulesTests` walks every chooser against `CanUse` for each of: colonist, prisoner, animal,
  bandit × colony bed, prison bed, owned by other.
- `TryClaimForSleeper` counts colonists only.
- `AssignOwnerAt` refuses an animal.
- `BedRulesAgreementTests`: the Hud picker's filter agrees with the Sim rule.
- **Measure the goldens.** If the claim fix moves them, hold it for P3's bake.

### P2

- The hash is unchanged while every pawn is Free.
- `odyssey.prison` round-trips.
- A save without the section loads with nobody in custody.
- `AllegianceConsumerTests`: one assertion per row of design 59 §4b's table.
- `Kind` is never written outside the loader (a grep test, in the shape of `RegistryTests`).

### P3

- `WorkTypeIndex.Names.Length == WorkHandle.Count`, and the same for skills.
- `BackfillSkills` is idempotent at format 10 and 11.
- The first nine skill rolls are unchanged by adding Social.
- A new `PrisonPurpose` value collides with no existing purpose constant.
- Re-bake, then **`GoldenColonyProbe` against `main`: every census number identical.**

### P4

- Marking one bed marks every bed in its room.
- A bed built later in the cell is a prison bed.
- Knocking out a wall leaves shackle beds, not colony beds.
- Merging a colonist's room into a cell evicts her.
- A bed in an unroofed room is a shackle bed.
- `PrisonCells` is not rebuilt when nothing changed (a counter).
- Layer-aware: two cells stacked on two layers are two cells.

### P5

- Over three simulated days, a prisoner in a closed cell never stands outside it.
- She sleeps in her own bed.
- A shackled prisoner never leaves her bed.
- A downed prisoner heals in bed and stands.
- Joy does not tick.
- No mental break at mood 0.

### P6

- A marked downed raider ends in a prison bed, `Dressed`, out of its band.
- The band's withdrawal tick is unchanged by the capture.
- No free bed: nothing moves and the alert stands.
- Right-click offers Capture before Finish off.
- Finish off is today's attack.
- The doctor treats a bleeding prisoner.

### P7

- A hungry prisoner is fed before starving, in a cell and when shackled.
- A meal in a cell is never hauled out.
- A colonist never eats it.

### P8

- The gain table of design 59 §8 reproduced exactly.
- The ETA equals the chats actually taken.
- The blockers name each factor.
- A recruit has passions and skills, Colonist traverse, `Issued`, a released bed, priorities of 3,
  and appears on the roster.

### P9

- The shown risk equals the rolled threshold, with a negative control.
- The √n normalisation.
- An escapee bashes a closed door and leaves.
- A downed escapee is a Prisoner again.
- A broken door raises its cellmates' risk.

### P10

- Released and exiled pawns leave by the edge.
- An arrested colonist released is a colonist again at the cell door.
- Ransom is refused `NotBuilt`.

### P11

- A raider under 30 % may yield.
- It never yields with no free bed.
- It walks to the cell.

### P12

- A resisting arrest ends in downs, never deaths.
- Both thoughts land.

## As built (2026-09-26)

All twelve units are in (design 59 §15). Of the bullets above, two are **not** covered by a test
yet, and say so rather than being quietly dropped:

- *A resisting arrest ends in downs, never deaths* — the escapee is hostile and the fight's own rule
  applies; no arrest-specific test asserts it.
- *Ransom is refused `NotBuilt`* — it is refused `NotPermitted`; there is no `NotBuilt` rejection.

## Merge order

In unit order:
- P3 carries the only golden bake, and every later unit depends on its handles.
- P1 and P2 are independent of each other and may go in either order.

## Owed on the owner's machine

- The Unity EditMode and PlayMode tiers for P1, P4, P6 and P8, because the fast tier compiles
  neither Presentation nor Editor.
- A play after P6 and after P9, following the handover tables.
- The confirmation of every proposed number in design 59 §8–§10.
