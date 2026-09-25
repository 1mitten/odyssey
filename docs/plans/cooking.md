# Cooking — the plan

**Status: written 2026-09-25, awaiting the owner's approval. No gameplay code has been written.**

- **Branch:** `claude/cooking`, worktree `D:\code\odyssey-cooking`.
- **Design:** `docs/design/48-cooking.md`.
- **Phases 1–2 on disk:**
  - `docs/research/cooking-interview.md` (twelve answers);
  - `a-18-cooking-hunting-butchering.md` (the reference's numbers);
  - `a-19-food-rot.md` (rot, and every merge site);
  - `e-10-shops-pack.md` (the pack measured, safe to import as one folder);
  - `e-11-cook-and-eat-animation.md` (every moment computed; the chair-sit tie-breaker).

## 1. What is being built

The owner's chain, in four playable units, **kitchen first** (interview #12):

1. **K1 Kitchen.** Cook carrots into meals at a powered galley or a campfire, from bills. A
   low-skill cook burns some. Colonists prefer a cooked meal and say so in their mood.
2. **K2 Cold store.** Meat and meals rot; a powered fridge stops it; a power cut is felt.
3. **K3 Hunt and butcher.** An armed hunter kills a marked hog; a cook carves it where it fell into
   raw meat.
4. **K4 Dining.** A colonist carries the meal on a plate to a chair at a table, sits, and eats with a
   knife and fork.

Each unit is a PR, reviewed, merged and **played** before the next is started. That is
`docs/process.md`: ground, decide, test first, measure, hand over, merge, play, record.

## 2. Before K1: two things the owner does or allows

1. **Import the Shops pack** (`e-10` §Recommendation). This happens while **no editor is open** on
   any worktree, because every worktree's `Assets/Synty` is a junction to the one real folder.
   Import it into `D:\code\odyssey` and untick PolygonGeneric and SyntyPackageHelper in the
   dialog. Alternatively, Claude builds a filtered `.unitypackage` holding only `PolygonShops/` in
   the scratchpad and imports it headless. Either way, nothing is committed.
2. **Answer design 48 §13**: the mood numbers, two names, the rotten-food haul, and the unpowered
   fridge.

## 3. Handle and format numbers

**None are fixed here.** Open PRs claim the next numbers (#225 skill 6, item 11, job 23; #222 items
12–13, job 25, edifices 17–21). Each unit, when it starts:

- merges `origin/main`;
- appends its handles after whatever is there;
- takes the next save format;
- **re-checks after every merge with `main`** before the PR is merged, because `RegistryTests` and
  `BuildShapesAgreeWithTheDefs` catch a collision only if the parallel arrays were both extended.

## 4. K1 — Kitchen

**Tests first** (fast tier unless marked):

- **Recipe and station**
  - `RecipeTests`: a meal takes 500 of raw food by nutrition. Carrots-only makes a vegetable meal;
    any meat makes a meal. A campfire charges one wood.
  - `BurnCurveTests`: the per-mille table by level; the campfire's ×1.5 capped at 1000. The roll
    is made once at the toil's start and survives a save mid-cook (a resume-equivalence test).
- **The cook job**
  - `CookJobTests`: a cook with Cooking enabled fetches, cooks and leaves the product on the front
    cell.
  - An unpowered galley offers no job.
  - A galley that loses power mid-cook keeps its progress, and the next cook finishes it.
  - An unreachable ingredient is skipped with no failed-job churn (the #119 lesson:
    `ctx.Reachable` in every giver).
- **Bills**
  - `BillTests`: *until N* stops at N and resumes below it; *N times* counts down; *forever*;
    suspended; the top bill wins.
  - Bills are saved, loaded and hashed.
- **Eating**
  - `EatPreferenceTests`: meal before ration before burnt before carrots; the nearest within a
    tier; raw meat is never chosen while anything better is reachable.
  - An ingredient reserved by a cook is not eaten.
  - `ThoughtPerFoodTests`: each food adds its own thought.
- **Registry and content**
  - `RegistryTests` passes, with the ration pack moved to `ui.res.rations`.
  - Fingerprints are updated for every content value that moves.
- **Unity tier**
  - The bill pane: add, reorder, change mode, suspend. Layout tests show the pane holds one height
    as bills are added (the storage-pane lesson).
  - `CookDrawnTests`: the food model swaps raw → cooked by the progress aspect.

**Build:**

- **Content**
  - The skill, the work type, `RecipeDef` and its loader.
  - `ItemDef` gains `foodTier`, `rawIngredient`, `ticksToRot` (read in K2) and `ateThought`.
  - Three items and five thoughts.
  - The galley def at 350 W, and the campfire as a station.
- **Simulation**
  - The bill store: per-station, saved and hashed, one new save section.
  - The cook giver and driver, modelled on the refuel and treatment drivers.
  - The `TryEat` tiers.
- **Interface**
  - The bill pane on the inspect panel for a galley or campfire.
  - Work-tab and skill rows go live.
- **Presentation**
  - The galley model; the pan and pot placements.
  - The stir solve.
  - The cooking progress and burning aspects.
  - Steam.
- **Wiki**
  - `icon-keys.csv` rows; both `--check`s pass; `emit_labels.py` is rerun.

**Measure:**

- **Goldens.** Every golden moves because of the new job defs. Take the census diff with
  `GoldenColonyProbe` against `main` and write it into the PR.
- **Cost of a colony that cooks.** `TickBenchmarkTests` gets a row with ten stations and fifty
  bills.

## 5. K2 — Cold store

**Tests first:**

- **The rot clock**
  - `FoodRotTests`: meat rots at 120,000 ticks on the ground, 80,000 in a shelf (×1.5), and never in
    a powered fridge.
  - An unpowered fridge counts as a shelf.
  - Rations and carrots never rot.
- **Stacks**
  - `RotMergeTests`: the count-weighted average, rounded up, at all three merge sites; a split
    copies its parent's rot.
- **The kind change**
  - `RotKindChangeTests`: this is the `a-19` §9 bug. A stack rots while a hauler walks more of the
    same kind to it, and the put-down re-plans rather than throwing. Eating and cooking a target
    that rotted mid-job fail cleanly.
- **Rotten food**
  - `RottenRemovalTests`: every store refuses rotten food; the refusal pass hauls it out; it
    disappears after a day.
- **The alert and the save**
  - `SpoilingAlertTests`.
  - Save round-trip at the new format. A save from the old format loads with every stack fresh.
- **Cost**
  - `FoodRotCostTests` (Long): the pass costs what the perishable list holds, not what the world
    holds. Measured at 1,000 and 10,000 stacks.

**Build:**

- **Simulation**
  - `ColonyItem.RotTicks`, saved and hashed.
  - `FoodRotSystem` and its perishable list.
  - The merge and split rules.
  - The kind change.
  - `BuildingDef.keepsCold`.
- **Content**
  - The fridge def: 8 slots, 120 W, Food by default.
  - The rotten-food item.
- **Interface and presentation**
  - The alert.
  - A rot line on an item's pane.
  - The fridge model and its drawn contents.

**Measure:**

- **The rot re-bake on its own.** Every golden moves when `RotTicks` enters the hash, even though
  nothing on those boards rots until meat exists. That re-bake is measured separately from K1's.

## 6. K3 — Hunt and butcher

**Tests first:**

- **Hunting**
  - `HuntTests`: only an armed colonist with Hunting enabled takes a designated wild animal. The
    fight is to the death.
  - A downed hunter leaves the mark standing, and a second hunter takes it.
  - Colonists and tamed animals cannot be marked.
- **Butchering**
  - `ButcherTests`: a hunted corpse is marked to butcher; a hand-marked one is too; a colonist's
    corpse never is.
  - The yield is `meatYield × efficiency`, with a hog at about 64 at level 1 and 85 at level 10.
  - Meat spills to neighbouring cells past 75 in a stack.
  - `CorpseRegistry.Remove` removes the corpse, and the removal is saved.
- **Unity tier**
  - `CarvePoseTests`: the held crouch drops the pelvis by a fraction of `LegLength`, measured from
    the drawn mesh, on four rigs.

**Build:**

- **Simulation**
  - The Hunting work type.
  - The Hunt intent and designation, saved.
  - The hunt giver and driver, reusing the melee attack with `ToTheDeath`.
  - `SpeciesDef.meatYield`.
  - The butcher mark and `Remove`.
  - The butcher giver and driver.
  - Raw meat, which is already perishable from K2.
- **Interface**
  - The Hunt command on the orders strip, a right-click and the animal's pane.
  - Butcher on a corpse's pane.
- **Presentation**
  - The held crouch.
  - `WorkStroke.Carve` and `WorkStyle.Butchering`.
  - The knife module; the `TwoHanded` flag.
  - Blood marks on each stroke.

**Record:**

- Add a P11 row in `docs/bug-patterns.md`: the stoop's 66 mm (design 48 §11).

## 7. K4 — Dining

**First, the tie-breaker:**

- **`SeatProbe`**: a contact sheet of four rigs on a chair, printing the clearance at the pelvis band
  and the sole. It **decides whether `SeatPose` is computed or authored**, before any dining code is
  written. Report the sheet to the owner.

**Tests first:**

- **Finding a seat**
  - `DiningTests`: a colonist with a meal and a free chair beside a table within 30 cells carries
    the meal there, sits and eats.
  - Two colonists never share a chair.
  - With no chair in range, the colonist eats where the food is and gets the no-table thought.
  - A table's four seats are all used.
- **Unity tier**
  - `SeatPoseTests`: the pelvis band sits on the seat top and the soles on the floor, within a
    tolerance, on four rigs.
  - `BiteCycleTests`: the hand reaches the mouth point and the plate point.

**Build:**

- **Content**
  - Table (1 × 2) and chair (1, rotatable).
- **Simulation**
  - The chair search and claim.
  - The eat driver's sit toil.
- **Presentation**
  - `SeatPose` (or the clip route).
  - The tray hold.
  - The bite cycle.
  - Tableware placements.
  - Chairs and tables fitted to target heights.

## 8. Gates for every unit

- **Tiers**
  - The fast tier, **and** the Long tier (`scripts/test-fast.sh --filter TestCategory=Long`).
  - Both Unity tiers, with **no other Unity batch run going** (check `Get-CimInstance
    Win32_Process -Filter "Name='Unity.exe'"`).
- **Content gates:** `build_wiki.py --check`, `emit_labels.py --check`, and `icons.py validate` with
  its unit tests.
- **Player build:** `scripts/unity.sh build`, smoke-tested with `-odyssey-newgame`. The Shops
  materials must keep their instancing variant, which the Synty keep-alive stages at build time.
- **Settings:** before committing, `git diff HEAD -- ProjectSettings/ Assets/Settings/`, restoring
  anything a batch run re-serialised.
- **Handover:** the full path, a what-changed table and a what-to-test table. A row in
  `docs/plans/playtest-queue.md`, and a line in `CLAUDE.md`'s status with the reasoning in
  `docs/journal.md`.

## 9. Recorded for later, not built

- **Temperature-driven rot** (the `CellTemp` hook, `a-19`'s 0–10 °C curve) and cold rooms with a
  cooler.
- **Corpse rot.**
- **Leather and bone.**
- **A butcher table** at the reference's full efficiency.
- **Fine and lavish meals**, and meal quality.
- **Food poisoning**, once health (#213) is in.
- **Washing plates.**
- **Taming, pens and slaughter** (the animals plan's later units).
- **A seated pose past the figure cap**, and the sort-key mitigation.
- **Bill hysteresis** (*unpause at*).
- **Ingredient radius.**
- **Hauling ingredients to a shelf beside the station.**
