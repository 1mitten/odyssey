# 48 — Cooking: from a hog to a meal eaten at a table

**Status: approved 2026-09-25 (owner: *"go for it"*), §13 taken on the recommendations. K1 being
built on `claude/cooking`.** Interview:
`docs/research/cooking-interview.md` (twelve answers, every recommendation taken). Research:
`a-18-cooking-hunting-butchering.md`, `a-19-food-rot.md`, `e-10-shops-pack.md`,
`e-11-cook-and-eat-animation.md`, with `a-08` and `a-14` behind them. Plan: `docs/plans/cooking.md`.
Ground: `main` at `837c895a`, which includes medical supplies (design 37). Number 48 because open
branches hold 37 and 39–47.

## 1. What the player gets

A colony that feeds itself properly:

1. **Hunt.** Mark a wild hog to hunt. An armed colonist with Hunting enabled goes after it and
   fights it to the death.
2. **Butcher.** A cook kneels at the carcass where it fell and carves it into **raw meat**, which is
   left as a pile on that cell.
3. **Store.** Raw meat rots in two days. A **fridge** is a powered, one-cell store that holds eight
   stacks and stops the rot while it has power.
4. **Cook.** A **galley** (the electric cooker) or a **campfire** takes **bills**: *cook meals until
   you have N*, *N times*, or *forever*. The cook fetches any raw food (meat, carrots, and berries or
   mushrooms once design 45 lands) and cooks it in a pan on the hob, or a pot over the fire. A
   low-skill cook sometimes **burns** it.
5. **Eat.** A hungry colonist takes the best food they can reach. If there is a free chair at a
   table, they carry the meal there on a plate, sit, and eat with a knife and fork. With no table,
   they eat standing and mind a little.

Each of the four delivery units (§12) is a playable PR, and each is played before the next begins.

## 2. The ground that shapes it

| Fact on `main` | Consequence |
|---|---|
| `TryEat` (`JobSystem.cs`) takes the **nearest** item with `nutrition > 0`, whether on the ground or in a shelf. `EatJobDriver` eats one unit where it lies and always adds `Thought_AteMeal` (+20, a quarter of a day, stacks twice) | The preference rule (§8) and a thought per food both go into these two places, and nowhere else |
| `Item_Meal` (handle 0) is the **ration pack**, 900 nutrition, and is labelled `ui.res.meal` | The cooked meal needs that key. The ration pack moves to `ui.res.rations`, which the registry already carries ("never spoils, nobody enjoys it"). That is a visible rename, made in the same commit as the wiki rebuild |
| Carrots are 180 a unit and stack to 75 | A meal takes **500 of raw food by nutrition** (§5), which is 3 carrots (540) or 10 raw meat |
| `BuildingDef.powerDrawW` plus `PowerGrid.IsPowered(edifice)`; the heater is the consumer pattern (design 32) | The galley and the fridge are one field each. The galley's work giver asks `IsPowered`; the fridge's rot rule asks the same question |
| A def with `storageSlots` is a `StorageUnit` (the shelf, design 30), and `Raise` registers storage and power independently | **The fridge is a shelf with a plug**, plus one flag (§6). The shelf's hauling, filters, pane and drawn contents all come for free |
| The campfire (edifice 14) is heat only and **has no fuel**: fuel is a recorded hook (design 31) | A campfire meal costs **one wood**, taken with the ingredients (§5). There is no hopper, and a campfire stays free to burn |
| `RefuelWorkGiver`/`RefuelJobDriver` (`PowerJobs.cs`) and the treatment job (design 37) fetch an item and take it to a target | This is the template for the cook job's fetch toil |
| Attacking is **drafted only** (`JobSystem.Attack.cs:32`), and an unordered fight ends in a down, never a death (design 33) | A hunt is a **work-given** attack that is ordered to the death. It is the first undrafted kill |
| `CombatSystem.Kill` writes a `CorpseRegistry` record, which has **no `Remove`**. Corpses are not items, and nothing hauls or rots them | Butchering happens **on the corpse's cell** (owner #1) and needs a `Remove` |
| `SitPose` exists and is dormant: the crouching idle read as sneaking, and its comment says a sit **cannot be computed** (design 31 §18e) | That argument is about a sit **on the floor**. A chair sit is a hip on a measured seat and a 90° knee, which the leg solver handles easily. One contact sheet decides (§10, `e-11`) |
| Every stoop drops the pelvis **66 mm**: its depth is 0.33 × `StandingHipHeight`, which is the 0.2 m clamp on every rig (P11) | A held crouch for butchering and the campfire uses `LegLength` instead. This is a fault fixed on the way (§11) |
| Open PRs claim the next handle numbers: ranged combat (#225) takes skill 6, item 11 and job 23; nature (#222) takes items 12–13, job 25 and edifices 17–21 | **No handle numbers in this document.** Each unit appends after whatever is on `main` the day it is built |

## 3. Names

The display names below are proposals. **The owner corrects them in the wiki**, and every key
already exists in `icon-keys.csv` except the ones marked *new*.

| Thing | Key | Label | Note |
|---|---|---|---|
| Electric cooker | `ui.arch.tool.galley` | Galley | The registry's existing name for "cooks meals". The owner has called it "the cooker"; the choice is theirs |
| Fridge | `ui.arch.tool.fridge` *new* | Cold store | `cooler` already names a room chiller (moves heat out of a room), which is a different machine |
| Table | `ui.arch.tool.table` | Table | |
| Chair | `ui.arch.tool.chair` | Chair | |
| Cooked meal (with meat) | `ui.res.meal` | Meal | Takes the key back from the ration pack |
| Cooked meal (without meat) | `ui.res.meal.veg` *new* | Vegetable meal | See §5 |
| Burnt meal | `ui.res.meal.burnt` *new* | Burnt meal | |
| Raw meat | `ui.res.meat` | Raw meat | |
| Rotten food | `ui.res.rotten` *new* | Rotten food | |
| Ration pack | `ui.res.rations` | Rations | Moves from `ui.res.meal` |
| Cooking skill / work | `ui.skill.cooking`, `ui.work.cooking` | Cooking | The skill's description changes from "food poisoning avoided" to "fewer burnt meals, faster" |
| Hunting work | `ui.work.hunting` | Hunting | |
| Hunt / Butcher commands | `ui.command.hunt`, `ui.command.butcher` | Hunt, Butcher | |
| Recipe | `ui.recipe.meal` *new* | Cook a meal | A new namespace for bills |
| Thoughts | `ui.thought.*` *new, five* | Ate a cooked meal · Ate a ration · Ate burnt food · Ate raw food · Ate without a table | |
| Alert | `ui.alert.spoilage` | Spoiling | |

## 4. Items

Every food carries three fields on `ItemDef`, next to `nutrition`:

- `foodTier`: lower is preferred (§8).
- `rawIngredient`: whether it can go into a recipe.
- `ticksToRot`: `0` means it never rots (§6).

A fourth field, `ateThought`, names the thought eating it adds. The fields hang on the Def, never on
the handle, so a new food is one XML row.

| Item | Nutrition | Stack | Tier | Raw ingredient | Rots in | Ate thought |
|---|---:|---:|---:|:-:|---:|---|
| Meal (meat) | 900 | 10 | 0 | – | 4 days (240,000) | Ate a cooked meal **+50** |
| Vegetable meal | 900 | 10 | 0 | – | 4 days | Ate a cooked meal **+50** |
| Rations | 900 | 20 | 1 | – | never | Ate a ration **+20** |
| Burnt meal | 700 | 10 | 2 | – | 4 days | Ate burnt food **−40** |
| Carrots | 180 | 75 | 3 | yes | never (owner #2) | Ate raw food **−50** |
| Berries, mushrooms (design 45) | as that design says | | 3 | yes | never in this unit | Ate raw food **−50** |
| Raw meat | 50 | 75 | 4 | yes | 2 days (120,000) | Ate raw food **−50** |
| Rotten food | 0 | 75 | – | – | – | inedible |

**Thoughts last a quarter of a day.** The two good ones stack twice, as the old +20 did; the bad
ones do not, so two carrots are one grievance. Eating without a table adds a separate **−30**.

**On this project's scale, not the reference's.** Mood runs 0 to 1,000 around a base of 500, so a
reference point is ten of ours: sleeping on the ground is −4 there and −40 here. The table was
first written as +12 / +4 / −4 / −5 / −3, which read as smaller than today's +20 only because the
two were in different units — found while writing `Thoughts.xml` (K1) and corrected here to the
same intent:

- the cooked meal is the reference's fine meal, **+50**;
- the ration keeps exactly what every food gave before the kitchen, **+20**, so a colony living on
  rations feels what it always felt;
- burnt food is **−40**;
- raw food is **−50**, the reference's −7 softened;
- eating without a table is **−30**, the reference's −3.

**Today every food gives +20**, so a carrot-fed colony loses mood under this table. That is the
pressure the owner accepted (§13).

A burnt meal keeps less than a whole meal's nutrition (700 against 900). That is the owner's "less
nutrition" (#4); `a-18` recommended keeping it at 900.

## 5. Recipes, stations and bills

**`RecipeDef`** (XML, new) holds:

- `workTicks`;
- `ingredientNutrition` (500);
- `ingredientFilter` (any item with `rawIngredient`);
- a list of `stations`, each with a speed factor, a burn factor and an extra cost;
- `product`;
- `productNoMeat`;
- `burntProduct`.

It is the one recipe in this unit, *Cook a meal*:

| Station | Power | Work | Burn chance | Extra cost | Model |
|---|---|---|---|---|---|
| **Galley** (1 cell, rotatable, worked from the front) | **350 W**, drawn whenever it is switched on, as the heater does | 300 ticks ÷ cooking speed | the curve below, ×1.0 | – | Shops `Kitchen_Stove_Oven_01` (1.74 × 1.18 × 1.22 m) |
| **Campfire** (existing) | – | **600** ticks ÷ cooking speed | ×**1.5**, capped at 1000 | **1 wood** | existing art, plus Shops `Kitchen_Pot_01` over the fire |

- **Galley cost.** 350 W on the 1,000 W generator is about 0.04 wood a meal (`a-18`); the galley is
  built from stone plus scrap metal, like the heater. A galley that loses power **mid-meal** keeps
  its progress and waits, and the cook drops the job. The ingredients stay consumed and in the pan,
  a meal in progress.
- **Cooking speed** comes from the Cooking skill on the WS curve: `workRateBasePerMille` 400 and
  slope 60, so level 10 is 1.0. This is the reference's 40 % → 100 % → 160 % (`a-18`).
- **The burn chance** is per mille by level, from the owner's shape (#4: ~30 % at 0, ~5 % at 8, none
  at 14): 300, 250, 200, 160, 130, 100, 80, 65, 50, 40, 30, 20, 10, 5, then 0 from level 14.
- **The roll is made when the cooking toil starts**, from the tick RNG, and saved on the job. Two
  reasons:
  - it is deterministic whatever the frame rate;
  - it lets the presentation darken the pan **before** the meal comes out burnt (§10) rather than
    revealing a failure at the last frame.
- **Which product comes out.** A meal with **any** raw meat in it is a *Meal*; one with none is a
  *Vegetable meal*. That meets the "looks differ by what went in" of #6 with one bill, and is two
  defs rather than per-stack ingredient memory.
- **Fetching ingredients.** The cook takes the nearest reachable raw food by path. A tie goes to the
  stack closest to rotting. The cook carries **one kind a trip** and repeats until the pan holds 500.
  A mixed meal is two trips (`a-18`). The ingredients are consumed as they are put into the pan. At
  a campfire, the wood is the last fetch.
- **Bills** (owner #5) are the reference state machine from `a-14`, sized down. A station holds an
  **ordered list**, with the top bill first. A bill is `{recipe, mode, target, done, suspended}`, and
  the three modes are:

  | Mode | Stops when |
  |---|---|
  | **Until you have N** (the default, N = 10) | Meals and vegetable meals on the map reach N. That counts every spawned, unforbidden unit on the ground or in a store, but not burnt meals and not what is carried |
  | **N times** | N meals have been made |
  | **Forever** | Never |

- **No ingredient radius.** The reference's default radius is the whole map, so the setting buys
  nothing here. Nor is there a *pause when satisfied, unpause at* band: until-N's own hysteresis is
  recorded as a hook.
- **The cook job** is one job:
  - reserve the station;
  - fetch until the pan holds 500, reserving each stack;
  - roll the burn;
  - work;
  - put the product on the station's front cell;
  - hand it to the haulers.

  It sits in the **Cooking** work type, which the work giver scan puts after growing and before
  cutting. That is the reference's place for it, and it means a hungry colony cooks before it
  fells.

## 6. Rot and the fridge

This follows `a-19`, with one owner answer (#2) overriding its temperature recommendation.

- **`ColonyItem.RotTicks`**, an integer. It is saved (**save format +1**) and hashed.
- **`FoodRotSystem`** runs every 120 ticks over a **list of perishable stacks**, not over the items.
  That keeps its cost growing with food rather than with the world, which is process §3's scaling
  rule. A stack joins the list when it spawns or changes kind to a def with `ticksToRot > 0`, and
  leaves it when it despawns.
- **The rate depends only on where the stack is.** Temperature does **not** drive it in this unit:
  the owner chose rot without it. `CellTemp` is the hook (design 28 §10), and `a-19` has the
  reference's linear 0–10 °C curve ready for when it does.

  | Where the stack is | Rate |
  |---|---|
  | The ground, or carried | ×1 |
  | An unpowered store | ×1.5 (storage decision 24/27, kept) |
  | A **powered fridge** | ×0 |

- **The fridge** is `BuildingDef.storageSlots 8` + `powerDrawW 120` + a new `keepsCold` flag. Its
  filter defaults to the Food category, and it draws as the Shops drinks fridge
  (0.95 × 1.11 × 2.29 m). **Unpowered, it is an ordinary container, so it rots food faster.** That is
  decision 24 read literally, and it is why a power cut is felt.
- **Merging and splitting.** When stacks merge, rot is the **count-weighted average, rounded up**,
  so a merge can never make food fresher than either part. A split copies its parent's rot. `a-19`
  lists the three merge sites and the one split site in `ColonyItems.cs`.
- **At the clock**, a stack **changes kind** to *Rotten food*, keeping its count. Every store's
  filter refuses it. The existing refusal pass (design 26 §11) carries it out to open ground, where it
  **disappears after one day**. The owner asked for it to be hauled out (#2), which is why it is not
  simply deleted as the reference does.
- **The bug this invites** (`a-19` §9). Nothing has ever changed kind while standing on a cell.
  Suppose a hauler is carrying meat to a stack that rots while they walk. `Drop` throws on arrival,
  because the kinds no longer match. `PutDown` and `PutInto` re-check their destination, and eating
  and cooking fail cleanly on a target that rotted mid-job. Each of the three has its own
  regression test.
- **The *Spoiling* alert** fires on any food stack in its last quarter outside a powered fridge. It
  is one alert for the colony, and clicking it cycles through the stacks.

## 7. Hunting and butchering

- **Hunt** is a designation on a **wild** animal, set from the orders strip, from a right-click, or
  from the animal's pane. It is saved on the animal (**save format +1**, shared with anything else in
  the same unit).
- **`HuntWorkGiver`** sits in the new **Hunting** work type. It offers the nearest designated
  reachable animal to a colonist who has **a weapon equipped** (owner #10). It is scanned last, after
  hauling, so hunting never outranks chores unless the priorities say so.
- **The hunt job** walks to within reach and runs the existing melee attack with the fight ordered
  **to the death**, the flag a right-click on a downed pawn already sets.
  - A hog struck in melee **always fights back**, as the reference does for melee hunters (`a-18`).
    Nothing new is needed: the existing retaliation already handles it.
  - A hunter who is downed ends the job. The designation **stays**, so the next armed hunter takes it.
  - The hog, unhurt by the hunter's fall, walks off.
  - If ranged combat (#225) merges first, a pistol is a weapon like any other.
- **Death.** A designated animal's death marks its corpse **to butcher**. Any animal corpse can be
  marked by hand with the *Butcher* command. A colonist's corpse never can.
- **`ButcherWorkGiver`** sits in the **Cooking** work type (the registry's "prepare meals and
  butcher"). The job walks to the corpse cell, **crouches**, and works 450 ticks ÷ cooking speed.
  - **Yield:** `meatYield × efficiency`. `meatYield` is a new `SpeciesDef` field: hog **85** (a
    reference 0.85 body size × 140 × a field butcher's 0.7), rat **6**. Efficiency is
    750 ‰ + 25 ‰ a level, capped at 1500 ‰.
  - A hog gives about **64 meat at level 1 and 85 at level 10**, which is six to eight meals.
  - There is no damaged-corpse penalty: a melee kill always damages the body, so the reference's
    ×0.66 would be a constant.
  - The meat drops as ordinary stacks on the corpse's cell and its neighbours (a stack limit of 75),
    and the haulers take it from there.
- **`CorpseRegistry.Remove`** is added. Butchering is the first thing that ends a corpse. Corpses do
  not rot in this unit; corpse rot is later work.

## 8. Eating

**Preference.** `TryEat` looks at the reachable food and takes the **lowest `foodTier`**, and the
**nearest** within that tier. Quality beats distance, as it does in the reference (`a-18`).

- **Raw meat (tier 4)** is taken only when nothing of tiers 0–3 is reachable (owner #9).
- **Rotten food** is never eaten. Neither is anything **reserved as a cook's ingredient**, so a
  colonist cannot eat the carrot the cook is walking to.
- **Cost.** This is still one pass over the items, keeping the best candidate per tier, so it costs
  what today's nearest-only scan costs.

**Where the colonist eats** (unit K4):

- Having chosen a food, the colonist looks for a **free chair beside a table** within **30 cells by
  path of the food** (the reference's radius is 31, `a-18`).
- A chair is beside a table when it is orthogonally adjacent to one of the table's cells.
- The chair's **facing is set by the colonist sitting down**, turned towards the table. Chairs are
  rotatable for looks, and the rotation is not a rule.
- **If there is one**, the colonist claims the chair (a reservation), carries the food to it, sits,
  and eats there.
- **If there is none**, the colonist eats standing where the food is, as today, and gets *Ate
  without a table* (−3).
- A table is **1 × 2** and seats up to four on adjacent chairs (owner #11). The chair search runs
  once per eat job, not per tick.
- **Plates, knives and forks are drawn only** (owner #7). They are not items, not hauled and never
  washed.

## 9. Content summary

| Kind | New | Changed |
|---|---|---|
| **Items** | meal (veg), burnt meal, raw meat, rotten food | ration pack re-keyed. `ItemDef` gains `foodTier`, `rawIngredient`, `ticksToRot` and `ateThought` |
| **Buildings** | galley, fridge, table, chair | campfire becomes a station |
| **Skill / work types** | Cooking skill; Cooking and Hunting work types | `SkillCatalogue` and `WorkCatalogue` rows go live |
| **Jobs** | cook, butcher, hunt | eat (preference, table, thought per food) |
| **Defs** | `RecipeDef` (one) | `SpeciesDef.meatYield`; `BuildingDef.keepsCold` |
| **Thoughts** | five | `Thought_AteMeal` becomes *Ate a cooked meal* |
| **Save** | bills, `RotTicks`, the hunt and butcher marks | the format is bumped once for each unit that adds state |
| **Goldens** | every one moves: new job defs always do (memory, *content lives in Defs*) | each re-bake is **measured** with `GoldenColonyProbe`, and the diff is written in the PR |

## 10. How it looks

The rule from `e-11`: **every moment is computed, over measured points**. A chair, a table top, a
plate, a pot and a carcass are known positions, so the body can be placed on them and the hands
solved to them on all 61 rigs, the bargain `SleepPose` and the crouch already make. A Mixamo clip
would bake another rig's seat height and hand path, and its raw files may not be redistributed.

| Moment | What the player sees | Built from | New |
|---|---|---|---|
| **Hunt** | The hunter closes and swings; the hog turns and fights | Existing combat clips and hit reactions | Nothing |
| **Butcher** | Crouched at the carcass, knife in the right hand and the left steadying the body. Short fast carving strokes, a spot of blood at each (design 33 §10). The carcass is gone and the meat pile is there when done | The gesture crouch, **held**, with its depth from `LegLength` (§11); `WorkStyle`/`WorkStroke`; the blood marks | `WorkStroke.Carve` (≈0.6 s, low arc, large dip); `WorkStyle.Butchering` with a knife module; a `TwoHanded` flag so the off hand solves to the carcass |
| **Cook at the galley** | Standing at the hob, spatula in hand, stirring in a small circle with the occasional flip. The food in the pan goes **raw → cooked** with progress, and **darkens towards burnt over the last third** if the roll failed. Steam rises while it cooks | `WorkCell` face-turn; the tool grip; the carry seam for the food placement; PolygonParticleFX `FX_Steam_01` | Stir as a **hand solved to a point circling the pan's measured rim** (not a `WorkSwing`, which is pitch in one plane). Food model per state: Shops patty raw/cooked/burnt for a meat meal, carrot or salad for a vegetable one. **Aspects:** `odyssey.pawn.cooking` (product def), `odyssey.pawn.cooking.progress` (per mille, from the saved `ToilProgress`, the SK2 precedent) and `odyssey.pawn.cooking.burning` (0/1) |
| **Cook at a campfire** | Crouched at the fire, stirring a pot | The held crouch, the same stir solved lower, the pot on the fire's measured top | Style chosen by **(job, station def at `WorkCell`)**, still a presentation lookup |
| **Haul to the fridge** | The usual armful, the stow gesture, the load appearing on the fridge's shelves | Design 24, design 30's drawn contents | Nothing |
| **Carry a meal** | A plate held level in both palms | The carry path | **A tray hold in `CarryPose`**, chosen by item def (design 24 §9a) |
| **Sit** | Lowers onto the chair over 0.8 s, hips on the seat, knees at a right angle, feet flat ahead, leaning slightly towards the table | `SitPose.Settle`; `Seated` and the `WorkCell` face-turn (both exist); `TwoBoneIk` for the shins; foot planting | **`SeatPose`**: the pelvis band on the chair's measured seat top (P11: the band, not the lowest vertex), thighs at 85–95°, shins solved to floor targets a fraction of `LegLength` ahead |
| **Eat at the table** | Plate, fork and knife on the table in front. The fork goes plate → mouth → plate, about five seconds a bite, with the head lifting from its −25° eating tilt on each bite. The food on the plate goes full → half → empty | Tool grip; the gaze system's posture pitch | The **bite cycle** (≈5 s, phase jittered by pawn id, on the game clock); tableware as instanced placements on the table top |
| **Eat standing** | The plate held at the chest in the left hand, the fork in the right | The tray hold plus the bite cycle | Nothing further |
| **Stand up** | The seat settle reversed; the plate stays on the table and vanishes | `SitPose.Settle` | Nothing |
| **Past the 64-figure cap** | Standing beside the chair or stove, as sleepers stand in bed today | – | Accepted. The cheap mitigation, if a full hall shows it, is to rank a posed pawn above a standing one of similar distance in the cap order: a sort key, not a second bake |

**Scale.** The Shops pack's furniture is about 1.15× life size and its food about 1.6×. Colonists
are drawn at about 2.5 m. Everything is **fitted to target sizes in metres**, as `BedShape` does, not
multiplied by a constant:

- seat at about 0.25 of the crown;
- table top at about 0.42;
- the galley's hob at the height of a standing colonist's hands.

**The tie-breaker.** `SitPose.cs` says a sit cannot be computed; `e-11` says a chair sit can. The
cheapest experiment decides it before K4 is written. **`SeatProbe`**, modelled on `SleepProbe`,
poses four rigs on a chair and prints the clearance at the pelvis band and the sole. If the hip
skinning tears at 90°, the chair sit takes the authored-clip route (*ANIMATION – Idles*, or a
Blender clip through the dormant `sitClip`) and nothing else in this document changes.

**Art.** The Shops pack is imported **as `Assets/Synty/PolygonShops/` only** (`e-10`):

- It ships PolygonGeneric with identical GUIDs and older `.mat` files, which would roll 109
  materials back.
- It is imported into the real `Assets/Synty` behind the junctions, while **no editor is open** on
  any worktree that shares it. It is **never committed**.
- Every art-dependent test asks whether *these* rows resolved (the runner has no Synty).
- Tool modules not in any pack (spatula, fork) are few-polygon Blender pieces under
  `Assets/Art/Custom/`. Shops has both, so none is expected.

## 11. Faults found on the way

- **The stoop is 66 mm deep on every rig** (`e-11` §4). `ApplyGesturePose` scales the pelvis drop by
  `StandingHipHeight`, which P11 says is the floor of its own clamp on every character. Today's
  gesture crouch is tuned against that value, so that crouch is left alone and the **held** crouch
  takes its depth from `LegLength`. A new row goes into `docs/bug-patterns.md` (P11, a third
  occurrence).
- **The meaning of `Thought_AteMeal`** changes from "ate anything" to "ate a cooked meal". Any test
  counting it will say so.

## 12. Delivery

Each unit is a PR, played before the next, with tests first. The plan is `docs/plans/cooking.md`.

| Unit | Contains | Playable result |
|---|---|---|
| **K1 Kitchen** | Cooking skill and work type; `RecipeDef`; galley; campfire station; bills and their pane; the cook job; the burn roll; meal, vegetable meal and burnt meal; the ration re-key; tiers and thoughts; the stove and campfire animation; Shops import | Build a galley on power, add a bill, watch a cook turn carrots into meals, sometimes burnt |
| **K2 Cold store** | `RotTicks`, `FoodRotSystem`, merge and split rules, rotten food and its removal, the fridge, the *Spoiling* alert | Meals left out go off; meals in a powered fridge don't; a power cut is felt |
| **K3 Hunt and butcher** | Hunting work type; the Hunt designation, giver and job; the corpse butcher mark; `CorpseRegistry.Remove`; the butcher job and yield; raw meat; the held crouch, carve stroke and knife | Mark a hog, an armed hunter kills it, a cook carves it, the meat goes to the fridge and into meals |
| **K4 Dining** | `SeatProbe` first; table and chair; the chair search and claim; `SeatPose`; the tray hold; the bite cycle; tableware; the no-table thought | A colonist carries a plate to a chair, sits and eats with a knife and fork |

## 13. For the owner at the plan review — answered

Approved with *"go for it"*, 2026-09-25, which took every recommendation:

1. **Mood numbers (§4): as proposed.** A colony on carrots loses mood until it cooks. The numbers
   were then corrected to this project's scale (§4); the intent did not change.
2. **Names (§3): Galley and Cold store as working names.** The owner corrects them in the wiki.
3. **Rotten food is hauled out** and disappears after a day.
4. **An unpowered fridge rots food faster than the floor**, under storage decision 24.

## 14. What K1 built, and what it did not

- **The bill list is on the tile pane, not a tab.** A station's pane shows a fixed-height block —
  a heading, *Add a bill*, a line of state and five rows — above the tile's facts, so adding a
  bill never moves the pane. A station holds **five bills**, the rows the block is laid out for
  (`Kitchen.MaxBills`, `BillsModel.MaxRows`). Each row: what it makes, the mode (press to go
  round), the target with ‹ › (shift for ten), the count against it, suspend, reorder, remove.
- **The cook holds a frying pan and tosses it** (`WorkStroke.Stir`, `WorkStyle.Cooking`). The pan
  is Battle Royale's `SM_Wep_Pan_01`, the only one installed. The angles are proposals for a
  contact sheet, like the pick's were.
- **The galley draws as the Shops stove once that pack is imported**, and as the tinted block
  until then; the meals draw as Sci-Fi City food trays (three kinds, one each). Both are one
  catalogue row to change.
- **Not in K1: the food in the pan going raw → cooked → burnt, and the steam.** Both want the
  Shops food models, so they come with the import. The station already publishes what they need
  (`StationView.CookPerMille`, `Burning`, `HasMeat`).
