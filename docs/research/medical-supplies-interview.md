# Medical supplies — the interview, 2026-09-24

**Phase 1 for the medical supplies unit.** The ground was `origin/main` at `f30049d1`, the day
combat merged (`docs/design/33-combat.md`). Health was one hit-point pool per pawn (person 100);
downed at 0, dead at −50 %. A colonist healed **only in a bed**, at 20 points a game day
(`CombatDef.bedHealPerDay`), and got up again at 15 %. A downed colonist could already be rescued
to a bed (`Work_Rescue`). No medical item existed: the registry had `ui.res.medkit` ("Medkit")
and `ui.res.medicine` ("Medicine") with no Def behind either. `ItemCategory.Medicine` and
`ui.res.category.medicine` already existed. `ui.work.doctor` and `ui.skill.medicine` were
registry rows with no Def either.

The owner's brief, in their words: *"Ensure items can be stored on stockpile, shelves and is
carried. Synty Battle royale. 'Medical Supplies' should be made uniformly small so it can be
stacked in shelves etc but then we'll recall the medikit to Medical Supplies and use this item.
This item will restore significant health but not all of it — as resting would then help restore
energy. Possible another seam for a weaker healing mechanism."*

## Answers

| # | Question | Answer | What it decides |
|---|---|---|---|
| 1 | How is the item used? | **Both: a doctor treats a colonist in bed, and self-treatment, "but much more ineffective self and takes longer"** | Two job paths: tending by another colonist, and self-treatment as a weaker fallback. |
| 2 | How much does one unit restore? | **+40, never past 80 % of the pool** | The last fifth always comes from bed rest. One treatment gets a downed colonist (≤ 0) back up, because 40 is past the 15 % recovery line. |
| 3 | The weaker healing seam | **Tend without supplies** | With no supplies anywhere in the colony, a doctor still dresses a wound for a small heal (about +10, under the same cap). The heal amount is a field on the Def, so a later weaker item is one Def row. |
| 4 | Where do supplies come from? | **Starting kit and the supply drop** | Neither a debug-menu spawn row nor worldgen scatter. |
| 5 | Self-treatment: how weak, and when? | **Half the heal, three times slower, fallback only** | +20 per unit, never past 60 %, three times the work. Only when no other colonist with Doctor enabled can reach the patient. A downed colonist can never treat themselves. |
| 6 | Doctor and Medicine? | **Both live, and skill buys speed only** | `Work_Doctor` becomes a live Work-tab column and treating trains `Skill_Medicine`. Skill makes a treatment faster on the same curve as the other work types; the heal amount stays fixed. The save format and the goldens will move. |
| 7 | How do supplies arrive by drop? | **A separate Medical drop** | A second `IncidentDef` beside the meal drop, dropping 4–8 units. It is content only, so the debug menu's Events tab lists it with no further work. |
| 8 | Stack size and starting amount | **Stack 10, start with 6** | `stackLimit` 10, which is one shelf bay; the starting kit carries one stack of 6. |

## Decisions I am making without asking, and why

- **Rename, not a new key.** `ui.res.medkit` keeps its key and its label becomes *Medical
  supplies*, with the defName `Item_MedicalSupplies`. `ui.res.medicine` stays reserved as a
  later, stronger tier.
- **The art is a Battle Royale medical box at one uniform small scale**, so it reads as a box on
  the ground, on a shelf and in an armful. Which of `SM_Prop_MedicalBox_01`, `_02` or
  `SM_Prop_Crate_Medical_01` is used is settled by a contact sheet the owner looks at. The pack
  is gitignored, so without it the item draws as the stand-in marker like any other item.
- **The heal is instant when a treatment finishes**, not spread over time. The bed rate stays as it
  is, and bed rest takes over from the treated level.
- **Storage, shelves and carrying need no new code.** Stores accept by category and by
  commodity, and the carry path is per-nothing (`24-carrying.md` §9a). The work is to prove it
  with tests rather than to build it.
