# Cooking — the interview, 2026-09-25

**Phase 1 for the cooking unit.** The owner asked to understand how the colony cooks, starting
from the chain *raw meat from a hog, hauled to a fridge, cooked into a meal, eaten on a plate with
a knife and fork*. They supplied the POLYGON Shops pack for it, which has a cooker, fridges, plates,
cutlery, and meat in raw, cooked and burnt versions. They asked for every question that would need
answering, including whether tables and chairs come in, and added mid-interview that **every
animation for cooking and eating must make sense**.

The ground, read on `main` at `3a39dd8d`:

- **Eating.** A colonist eats the nearest thing with `nutrition > 0`, standing wherever it lies
  (`JobSystem.cs` `TryEat`, `JobDrivers.cs` `EatJobDriver`). Every food gives the same +20 thought.
- **Foods.** The only foods are the ration pack (`Item_Meal`, which is labelled `ui.res.meal`)
  and carrots.
- **Missing entirely.** There is no recipe, bill, workbench, spoilage, meat, table, chair or
  sitting pose.
- **Hogs and corpses.** Hogs exist and can be killed, but only by a drafted colonist. A corpse is a
  `CorpseRegistry` record that nothing can remove.
- **Building blocks to reuse.**
  - Power: `powerDrawW` and `PowerGrid.IsPowered` say whether a building has power. The heater is
    the pattern for a consumer.
  - Stores: a shelf is a built store (`storageSlots`). A def with both `storageSlots` and
    `powerDrawW` already registers as a store and as a power device.
- **Names already reserved.** Galley, table, chair, butcher, meat, meal, Spoiling, `ui.skill.cooking`,
  `ui.work.cooking` and `ui.work.hunting` are in the registry, greyed out.

Twelve questions were put, in three rounds of four. Each had a recommendation, and the owner took the
recommendation every time. The consequence is written beside each answer so the design can be written
without asking twice.

## Answers

| # | Question | Answer | What it decides |
|---|---|---|---|
| 1 | How does a colonist get meat from a hog? | **Hunt, and butcher on the spot.** | A **Hunt** order on a wild hog. An undrafted hunter kills it with the existing combat. A cook butchers the carcass **where it fell** into raw meat. No corpse hauling and no butcher table in this unit. Taming and pens stay later work (`animals-interview.md` #2). |
| 2 | Does food spoil? | **Yes: meat and meals rot.** | Raw meat (~2 days) and meals (~4 days) carry a rot clock. A **powered fridge stops it**. Ration packs and carrots never rot. Rotten food is inedible and gets hauled out. Storage decision 24 (containers speed rot) has to be reconciled in the design. |
| 3 | What is the cooking step? | **One step: raw → meal.** | The cooker turns raw ingredients straight into a meal item. The pan, the pot, and the pack's raw → cooked models are what the player **sees** during the work. There is no stored "cooked meat" ingredient. |
| 4 | What does a burn produce? | **A burnt meal: edible, worse.** | Less nutrition and a bad-food mood penalty in place of the good-meal one. The chance falls with Cooking skill (about 30% at 0, 5% at 8, none from 14). It is drawn with the pack's burnt models. Replaces the reference game's food poisoning. |
| 5 | How does the player order cooking? | **A bill list on the cooker pane.** | Each bill is "until you have N" (the default), "N times" or "forever". A cooker can hold several bills, and the top one goes first. `a-14` has the reference state machine. |
| 6 | What goes into a meal? | **Any raw food: meat or carrots.** | A meal takes a fixed amount of raw food of either kind, so the kitchen is useful before the first hunt. How a meal looks follows what went into it. Berries and mushrooms (PR #222) are raw foods too if that PR merges first. |
| 7 | How real are tables, chairs, plates and cutlery? | **Table and chair are real; the plate is drawn.** | Table and chair are buildable furniture. A colonist carries a meal to a free chair at a table, sits and eats. The plate, knife and fork appear with the meal and vanish afterwards. Eating without a table costs a small mood penalty. |
| 8 | Can anything cook without electricity? | **The campfire cooks too, slower.** | The existing campfire takes the same bills, burns wood, cooks slower and burns food more often. The electric cooker is the upgrade. **The fridge is electric only.** |
| 9 | What do colonists prefer to eat? | **Meal > ration > carrots; raw meat only when starving.** | A hungry colonist walks further for better food. Raw meat is eaten only when nothing else is reachable, with a raw-food mood penalty. A burnt meal ranks just under the ration. |
| 10 | Who may hunt? | **A Hunting work type, and a weapon is required.** | A new Hunting column. Only a colonist with a weapon equipped takes a Hunt order. A hunt is to the death, and the hunter may be downed (hogs retaliate 70% of the time). If ranged combat (PR #225) lands first, a pistol counts as a weapon. |
| 11 | What footprints? | **Fridge 1 cell, table 1×2, chair 1 cell.** | The fridge is upright in one cell, holding about 8 stacks like a shelf. The table is two cells long and seats up to 4 on adjacent chairs. Chairs are 1 cell and rotatable. The cooker is 1 cell, worked from the front. |
| 12 | In what order is it delivered? | **Kitchen first.** | Four PRs, each played before the next: **K1** cooker + campfire + bills + Cooking skill (meals from carrots, burning) → **K2** spoilage + fridge → **K3** hunt + butcher + raw meat → **K4** table, chair, sitting, plate. |
| — | (Added mid-interview) | **"Consider all the animations for cooking and eating make sense as well."** | Every step has a designed look: carving at the carcass, stirring at the hob with the food changing in the pan, carrying a plate, sitting, fork to mouth. Research lane `e-11`. |

## Decisions made without asking, and why

Any of these can be overturned at the plan.

- **Handle numbers are allocated at build time, not here.** Open PRs already claim the next
  numbers. Ranged combat (#225) takes skill 6, item 11 and job 23. Nature (#222) takes items 12–13,
  job 25 and edifices 17–21. Cooking appends after whatever is on `main` when K1 is built.
- **The design document is number 48.** Open branches hold 37 and 39–47.
- **The Shops pack is imported only as `Assets/Synty/PolygonShops/`.** The package ships its own
  PolygonGeneric with the same GUIDs, which is the Battle Royale lesson (design 29). The import waits
  until no editor is open on a worktree that shares the junction, because `Assets/Synty` is one
  folder behind every worktree.
- **The ration pack gives up the `ui.res.meal` key** to the cooked meal and takes `ui.res.rations`,
  which the registry already has. It is a content change, so it is made in the same commit as the
  wiki rebuild.
