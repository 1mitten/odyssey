# 37 — Medical supplies, the doctor, and self-treatment

**Status: approved 2026-09-24, option A (§3) and every recommended number (owner: *"go with your recommendations"*). Being built.** Interview: `docs/research/medical-supplies-interview.md`.
Ground: `main` at `52f53112`, which includes the combat line (`docs/design/33-combat.md`).
Number 37 because PR #181 claims 36.

## 1. What the player gets

- **Medical supplies**: a small boxed item, stacking to **10**. It is hauled to stockpiles, sits
  on shelves and is carried like every other item. The colony starts with **6**, and a new
  **Medical drop** event brings 4–8.
- **Doctor** becomes a live Work-tab column and **Medicine** a live skill. A colonist with Doctor
  enabled treats hurt colonists.
- **Treatment with supplies** heals **+40**, but never past **80 %** of the pool. The last fifth
  only comes back through bed rest, so resting keeps a purpose.
- **Self-treatment** is the fallback when no doctor can reach the patient. It heals **+20**,
  never past **60 %**, and takes three times as long. A downed colonist can never treat
  themselves.
- **Treatment without supplies** happens when the colony has none. A doctor dresses the wound
  with what is at hand for **+10**, under the same 80 % cap. This is the weaker seam. The
  amount is a field on the Def, so a later bandage or herbal item is one Def row.
- **Medicine speeds up treatment and does not change the heal**, on the same work-speed curve as
  the other skills.

## 2. The ground that shapes it

| Fact on `main` | Consequence |
|---|---|
| One pool per pawn (person 100). Downed at ≤ 0 and back up at 15 %. A colonist heals only **in a bed**, at 20 points a day (`CombatSystem.Heal`, `InBed`) | +40 lifts any downed colonist past 15 % in one treatment. Nothing about bed healing changes. |
| **Rescue is still a stub.** `RescueJobDriver` fails and `RescueWorkGiver` answers no (C4, unbuilt; no open PR builds it) | **A downed colonist cannot be brought to a bed.** Today they lie where they fell and never heal. See §3. |
| No behaviour sends a hurt colonist who is still standing to bed. `ui.work.patient` and `ui.work.bedrest` are registry rows with nothing behind them | "Treated in bed" is out of reach unless something puts the patient there. See §3. |
| `ItemCategory.Medicine` and `ui.res.category.medicine` already exist | Stores filter it with no new code. |
| Items draw per type through `ModuleIds.ItemModules`, in `ItemIndex` order (`ModuleIdTests`), with art rows in `PlayScene.cs`. Shelves and the armful reuse the same row | One art row covers the ground, a shelf and a carrying colonist. |
| The supply drop is a worker plus Def rows (`Incident_ScrapDrop` is a second cargo) | The Medical drop is one `IncidentDef` and one bulletin key. |
| `RefuelWorkGiver` / `RefuelJobDriver` (`PowerJobs.cs`) fetch one unit of an item and take it to a target | This is the template for the treatment job. |

## 3. The one open decision: where treatment happens

The owner's answer was that the patient is *treated in bed*. Of today's patients, only the ones
who happen to be asleep are in a bed: a downed colonist cannot be carried to one, and a hurt
colonist has no reason to walk to one.

| Option | What it is | Cost | Verdict |
|---|---|---|---|
| **A. Treated where they are, and the hurt go to bed** | A downed colonist is treated **where they lie**, and +40 gets them up. A standing colonist under **50 %** goes to their own bed, or the nearest free one, and lies down as a **patient** (`Job_Patient`). There they heal at the bed rate and are treated. They get up at **80 %**, the treatment cap, and from then on normal sleep does the rest. | One new job beyond treatment, which reuses the sleep driver's lie-down | **Recommended.** Every patient can be reached, the bed rule still means something, and rescue (C4) stays a unit of its own that makes this better rather than replacing it. |
| B. Build rescue (C4) first, then treat only in bed | Design 33 §4's rescue: reserve a pawn, carry the body, lay them in a bed | Carrying a body is a new pose and a new reservation kind, a unit's worth on its own | Faithful to the wording, but twice the size, and it still needs the go-to-bed rule for standing colonists. |
| C. Treated wherever they are, no bed rule | The doctor walks to the patient and treats them standing or lying | Cheapest | Treatment happens, but a hurt colonist is never sent to rest, so the "rest restores the rest" half of the brief has nothing driving it. |

**The 50 % threshold and the 80 % release are invented numbers** and are open to tuning. 50 % is
low enough that a single scratch does not put a colonist to bed for a day.

## 4. The rules (option A)

- **A patient is a colonist below 80 % of their pool who has not been treated recently.**
  A treatment sets `Pawn.TreatedUntilTick` to a quarter of a day ahead. Until then no further
  treatment is given, so a stack of supplies cannot replace rest, and a doctor with no supplies
  cannot repeat +10 over and over. *Invented number.*
- **Doctor (`Work_Doctor`, emergency giver).** The giver looks for the nearest patient it can
  reach who is downed or lying as a patient. If the colony has supplies the doctor can reach, the
  job fetches one, carries it to the patient and treats them; otherwise it treats without
  supplies. **A doctor never treats themselves** through this giver.
- **Self-treatment.** A patient who is not downed, while **no colonist with Doctor enabled and
  not downed can reach them**, fetches one unit and treats themselves where they stand: three
  times the work, `selfHeal` 20, `selfCap` 60 %. With no supplies there is no self-treatment.
  After a self-treatment they go to bed as a patient if they are still under 50 %.
- **Work and skill.** One treatment is **600 ticks of work** at skill level 0 (*invented*), on
  the growing and cutting curve through `rateSkill`. It trains `Skill_Medicine`. Self-treatment
  also trains it.
- **The heal is applied in full when the work finishes**, through the same `HpMilli` write
  `CombatSystem` uses. It never raises the pool past the cap and never lowers it. Getting up
  from downed happens through the existing check (`downedRecoverAtPerMille`) on the next heal
  interval, or directly in the job, whichever `CombatSystem.Recover` allows. This is settled in
  the tests, not guessed.
- **An interrupted treatment uses no supplies.** The unit is consumed when the heal is applied;
  until then the doctor is simply carrying it.

The numbers live on a `MedicalDef` block in `Combat.xml` (or on the item: see §5), and none is
hard-coded:

| Field | Value | Owner or invented |
|---|---|---|
| `healPerUnit` (on the item) | 40 | owner |
| `healCapPerMille` | 800 | owner |
| `selfHealPerMille` of the heal / `selfCapPerMille` | 500 / 600 | owner (half, 60 %) |
| `selfWorkFactor` | 3 | owner |
| `bareHeal` (no supplies) | 10 | owner's "about +10" |
| `patientBelowPerMille` / `patientReleaseAtPerMille` | 500 / 800 | invented |
| `treatWork` | 600 ticks | invented |
| `treatedCooldownTicks` | ¼ day | invented |

## 5. Content

- `Item_MedicalSupplies`: category Medicine, `stackLimit` 10, `healPerUnit` 40, label
  *medical supplies*. `ItemHandle.MedicalSupplies` = 11. The table is append-only, and its
  module row goes at the end of `ItemModules`.
- `ui.res.medkit`: the key stays and the label becomes **Medical supplies**. `ui.res.medicine`
  stays reserved for a later, stronger tier.
- `Work_Doctor` (`WorkHandle.Doctor` = 6), `Skill_Medicine` (live in the Skills tab),
  `Job_Treat` (22) and `Job_Patient` (23), with self-treatment as a flag on `Job_Treat` rather
  than a job of its own.
- `Incident_MedicalDrop`: the supply-drop worker with a stack of 4–8, weight 40, and
  `ui.bulletin.medicaldrop`.
- Starting kit: one stack of 6 beside the meals (`ColonyScenario`, `22-starting-kit.md`).
- Registry and wiki: the relabel, `ui.work.doctor` and `ui.skill.medicine` go live, and new
  `ui.status.treating`, `ui.status.patient` and `ui.bulletin.medicaldrop`, with `icon-map.csv`
  rows written in the `-,gap` form.

**The art is a Battle Royale medical box**, `SM_Prop_MedicalBox_01`, `_02` or
`SM_Prop_Crate_Medical_01`, at one uniform small scale sized like the ore lumps (about 0.4 m
across). It has to read as a box you could carry, fit two to a shelf bay, and sit in an armful.
**The owner picks it from a contact sheet** in the first unit. Without the pack it draws as the
stand-in marker, like every item.

## 6. What moves

- **Save format 9 → 10**: `TreatedUntilTick` on the pawn, saved and hashed. The skill and
  work-priority arrays grow by one entry each; Melee showed that this moves the hash but not the
  format.
- **All six goldens move.** A sixth work priority, a seventh skill, two job counters, the new
  field, and six supplies in the starting kit that haulers will now move. **Measured with
  `GoldenColonyProbe`** to be the hash seeing more rather than the colony behaving differently,
  except for the hauling of the supplies, which is expected and will be named.
- **Scaling (process §3).** The Doctor giver scans colonists, not cells; it uses the existing
  nearest-reachable-item search for supplies and `ctx.Reachable` like every other giver. It runs
  as an emergency giver only when a patient exists, and that count is kept once per tick,
  otherwise the giver returns immediately. No per-cell loop is added.

## 7. Units

Each unit is test-first and goes through the full cycle in `docs/process.md`.

| Unit | What | Proven by |
|---|---|---|
| **MD1** The item | Def, handle, module row, art pick (contact sheet), wiki relabel, starting kit of 6, Medical drop | Tests that it is hauled to a Medicine-only stockpile, placed on a shelf (a bay holds 10), carried in an armful, dropped by the event, and present in the starting kit; `ModuleIdTests`; all three content gates |
| **MD2** Doctor and treatment | `Work_Doctor`, `Skill_Medicine`, `Job_Treat` (supplied and bare), the cooldown, the cap, treating a downed colonist where they lie | A downed colonist at 0 is up at 40 after one treatment; 70 goes to 80, not 110; a second treatment inside the cooldown is refused; with no supplies the heal is +10; a unit is used up only on success; an unreachable patient gives no job (the #119 lesson) |
| **MD3** Patient and self-treatment | `Job_Patient` (to bed under 50 %, up at 80 %), the self-treatment fallback | A colonist at 40 goes to bed; the lone colonist treats themselves at +20 capped at 60 % over three times the ticks; a downed colonist never self-treats; self-treatment never happens while a doctor can reach |
| **MD4** Interface | The Doctor column, the Medicine row, the activity-line labels, the Events tab row, goldens re-baked and probed | `RegistryTests`, `HudFontTests`, the Work tab's column-count tests, golden diffs with the probe's output |

MD1 can be played on its own (stockpiles, shelves, carrying). MD2 and MD3 are played together.

## 8. Not in this unit, recorded as hooks

- **Rescue (C4).** Once it exists, a downed colonist is carried to a bed and treated there.
  Option A's treat-where-they-lie stays as the field treatment.
- Medicine as the stronger tier (`ui.res.medicine`), crafted supplies, and treatment quality from
  skill.
- `ui.alert.nomedicine`, which is an alert that fires when there is a patient and no supplies.
  It is one line in the alert model and **is included if MD4 has room**.
- Body parts, bleeding, infection: none of these exist in design 33's model.

## 9. As built (2026-09-24)

All four units are in, on `claude/medical-supplies`. What moved from the plan, and why:

- **No game-wide save format bump.** The plan said 9 → 10. `TreatedUntilTick` went into the
  combat section, which carries its own layout number (3 → 4) and writes a record only for a pawn
  with combat state. The pawn section already writes its skill and priority array lengths, so an
  older save loads with Medicine at 0 and Doctor at the default priority of 3. `CombatContractTests`
  round-trips the new field and checks that it is hashed only while set.
- **The patient's identity is `Job.WorkTicks`** (a `PawnId` value), not `CombatTarget`. Every
  driver already uses that field for its own purpose, and it is saved and hashed with the job. A
  new reservation kind, `ReservationTargetKind.Pawn`, stops two doctors treating one patient;
  rescue (C4) can use it too.
- **One unit, not the stack.** `ColonyItems.SplitOff` takes a count off a stack straight into a
  colonist's hands. `LiftToil` picks up whole stacks, so without this a doctor would have carried
  all six boxes to the patient and left five on the floor. The unit is used up only when the heal
  lands; a treatment that fails first puts it down (`DropCarried`).
- **Getting up goes through the combat system.** A treatment that lifts a downed colonist past
  15 % asks `CombatSystem.Recover` at the end of the tick (`ctx.Defer`), because that is the one
  owner of ending `Job_Downed`.
- **Three patient rules the plan did not have, all found while writing it:**
  1. A patient gets up when hungry, because nothing in the job system interrupts a running job for
     a need, and a patient can be in bed for days.
  2. A patient lies on the ground only while a doctor could still come. Lying in the mud heals
     nothing, and without this rule a colonist would lie down and stand up again every tick.
  3. A patient in bed checks every 250 ticks whether she should get up and treat herself. That
     check scans the colony, so it is not run every tick.
- **A doctor treats a colonist asleep in a bed** as well as a patient or a downed one: a colonist
  who is hurt and sleeping is lying still in a bed, which is the owner's condition.
- **Art: `SM_Prop_MedicalBox_01` at scale 1.0** (0.48 × 0.13 × 0.39 m), picked from
  `MedicalBoxSheet` (`scripts/unity.sh shot Odyssey.EditorTools.MedicalBoxSheet.Shoot`). Both other
  candidates are olive and disappear into the grass. **The owner has the final say** (playtest
  queue).
- **`PlayScene.RebuildCatalogue` was not used to write the row.** Run in this worktree, it
  regenerated the catalogue with every colonist's skin, hair and cloth swatch data missing (3,678
  lines). The row was therefore added by hand, with the same fields as its neighbours. Why the
  rebuild drops the swatches is **unexplained**. It may be a fresh worktree's import state, and it
  is worth checking before anybody trusts a rebuild.
- **The goldens moved and were measured.** A seventh skill, a seventh priority and two job
  counters are hashed. `GoldenColonyProbe` gives identical output on `main` (52f53112) and on the
  branch for all three boards. MD1's twelfth item moved nothing, because no golden colony has a
  storage zone.

`ui.alert.nomedicine` is **not** built. It remains a hook.
