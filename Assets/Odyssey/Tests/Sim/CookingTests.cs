#nullable enable
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Cooking;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Power;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The kitchen (design 48, K1): recipes, stations, bills, the cook, the burn, and what a hungry
    /// colonist chooses to eat — each run end to end by the colony's own colonists where the rule
    /// is about behaviour, because a bill nobody works looks exactly like a bill never added.
    /// </summary>
    public class CookingTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        const int Carrots = ItemHandle.Carrots;
        const int Meal = ItemHandle.CookedMeal;
        const int VegMeal = ItemHandle.VegetableMeal;
        const int Burnt = ItemHandle.BurntMeal;
        const int Rations = ItemHandle.Meal;

        // ---- fixture ----------------------------------------------------------------------------

        static ColonyWorld Board(int colonists = 2)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;
            scenario.startingFellRadius = 0;
            ColonyWorld colony = ColonyWorld.Build(Size, 1u, scenario, barren: true, wooded: false);
            // One tick so the starting skills are rolled; a test that sets a skill sets it after.
            colony.World.Tick();
            return colony;
        }

        static Kitchen KitchenOf(ColonyWorld colony) => colony.Pawns.Kitchen!;

        static int Open(ColonyWorld colony, int dx, int dz)
        {
            CellRef s = colony.Start;
            int cell = Size.Index(s.X + dx, s.Z + dz, s.Y);
            Assume.That(colony.Construction.Allows(cell), Is.True, "an ordinary buildable cell");
            return cell;
        }

        static int RaiseNow(ColonyWorld colony, int cell, int building, int facing = 0)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), building, StuffHandle.Wood, facing),
                Is.EqualTo(IntentRejection.None));
            colony.Construction.Deliver(cell, ConstructionContent.BuildingAt(building).costCount);
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
            return colony.Grid.Edifice[cell];
        }

        /// <summary>A galley on a live net: a run of line, a fuelled generator, and the galley on the line.</summary>
        static int PoweredGalley(ColonyWorld colony)
        {
            PowerGrid power = colony.Pawns.Power!;
            for (int x = 2; x <= 7; x++) power.AddLine(Open(colony, x, 2));
            int gen = RaiseNow(colony, Open(colony, 2, 3), BuildingHandle.Generator, facing: 1);
            power.SetFuelMilli(gen, 75_000);
            int cell = Open(colony, 6, 3);
            RaiseNow(colony, cell, BuildingHandle.Galley, facing: 0);
            colony.World.Tick();
            Assume.That(power.IsPowered(colony.Grid.Edifice[cell]), Is.True, "the galley is on a live net");
            return cell;
        }

        static void Stock(ColonyWorld colony, int item, int count, int dx = 1, int dz = -3)
        {
            int near = Open(colony, dx, dz);
            int at = colony.Pawns.Items.NearestCellWithSpace(colony.Grid, near, item, count, 8);
            Assume.That(at, Is.GreaterThanOrEqualTo(0));
            colony.Pawns.Items.Spawn(item, at, count);
        }

        static int OnTheBoard(ColonyWorld colony, int item)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == item) total += items[i].Stack;
            return total;
        }

        static int MealsMade(ColonyWorld colony) =>
            OnTheBoard(colony, Meal) + OnTheBoard(colony, VegMeal) + OnTheBoard(colony, Burnt);

        static IntentRejection Edit(ColonyWorld colony, int station, int op, int b = 0, int c = 0) =>
            KitchenOf(colony).HandleEditBill(new Intent(IntentKind.EditBill, Size.FromIndex(station), op, b, c));

        static bool RunUntil(ColonyWorld colony, Func<bool> done, int maxTicks)
        {
            for (int t = 0; t < maxTicks; t++)
            {
                if (done()) return true;
                colony.World.Tick();
            }
            return done();
        }

        /// <summary>Make one colonist the only cook, at a skill level, and keep everybody fed and rested.</summary>
        static Pawn OnlyCook(ColonyWorld colony, int level)
        {
            var colonists = colony.Pawns.Pawns.All.Where(p => p.IsColonist).ToList();
            Pawn cook = colonists[0];
            foreach (Pawn p in colonists)
                p.WorkPriorities[WorkTypeIndex.Cooking] = (byte)(p == cook ? 1 : 0);
            SetLevel(colony, cook, level);
            return cook;
        }

        static void SetLevel(ColonyWorld colony, Pawn pawn, int level)
        {
            SkillDef def = colony.Pawns.Content.Skills[SkillIndex.Cooking];
            int xp = 0;
            while (def.Level(xp) < level) xp += 1_000;
            pawn.Skills[SkillIndex.Cooking] = xp;
            Assume.That(pawn.SkillLevel(SkillIndex.Cooking), Is.EqualTo(level));
        }

        // ---- content -----------------------------------------------------------------------------

        [Test]
        public void TheMealRecipeIsTheDesignsOne()
        {
            PawnContent content = ContentPack.Pawns();
            RecipeDef recipe = content.Recipes[RecipeHandle.Meal];
            Assert.That(recipe.ingredientNutrition, Is.EqualTo(500), "three carrots' worth");
            Assert.That(recipe.ProductItem, Is.EqualTo(Meal));
            Assert.That(recipe.ProductNoMeatItem, Is.EqualTo(VegMeal));
            Assert.That(recipe.BurntItem, Is.EqualTo(Burnt));
            Assert.That(recipe.At(BuildingHandle.Galley), Is.Not.Null);
            Assert.That(recipe.At(BuildingHandle.Campfire), Is.Not.Null);
            Assert.That(recipe.At(BuildingHandle.Heater), Is.Null, "a heater cooks nothing");

            RecipeStation fire = recipe.At(BuildingHandle.Campfire)!;
            Assert.That(fire.workFactorPerMille, Is.EqualTo(2_000));
            Assert.That(fire.fuelItem, Is.EqualTo(ItemHandle.Wood));
            Assert.That(fire.fuelCount, Is.EqualTo(1));
        }

        /// <summary>The owner's shape (interview #4): about 30 % at 0, 5 % at 8, none from 14.</summary>
        [Test]
        public void TheBurnCurveFallsToNothingByLevelFourteen()
        {
            RecipeDef recipe = ContentPack.Pawns().Recipes[RecipeHandle.Meal];
            Assert.That(recipe.BurnPerMille(0), Is.EqualTo(300));
            Assert.That(recipe.BurnPerMille(8), Is.EqualTo(50));
            Assert.That(recipe.BurnPerMille(14), Is.EqualTo(0));
            Assert.That(recipe.BurnPerMille(20), Is.EqualTo(0), "a level past the table reads its last entry");
            for (int level = 1; level <= 14; level++)
                Assert.That(recipe.BurnPerMille(level), Is.LessThanOrEqualTo(recipe.BurnPerMille(level - 1)),
                    "a better cook never burns more");
        }

        [Test]
        public void ACampfireBurnsHalfAsOftenAgainAsAGalley()
        {
            ColonyWorld colony = Board();
            int galley = PoweredGalley(colony);
            int fire = Open(colony, 6, 6);
            RaiseNow(colony, fire, BuildingHandle.Campfire);
            Kitchen kitchen = KitchenOf(colony);
            Assert.That(Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal), Is.EqualTo(IntentRejection.None));
            Assert.That(Edit(colony, fire, BillEdit.Add, RecipeHandle.Meal), Is.EqualTo(IntentRejection.None));

            RecipeDef recipe = colony.Pawns.Content.Recipes[RecipeHandle.Meal];
            CookStation g = kitchen.At(colony.Grid.Edifice[galley])!;
            CookStation f = kitchen.At(colony.Grid.Edifice[fire])!;
            Assert.That(kitchen.BurnChancePerMille(g, recipe, 0), Is.EqualTo(300));
            Assert.That(kitchen.BurnChancePerMille(f, recipe, 0), Is.EqualTo(450));
            Assert.That(kitchen.BurnChancePerMille(f, recipe, 14), Is.EqualTo(0));
            Assert.That(kitchen.WorkMilli(f, recipe), Is.EqualTo(2 * kitchen.WorkMilli(g, recipe)));
        }

        // ---- bills ------------------------------------------------------------------------------

        [Test]
        public void ABillCanOnlyBeAddedToSomethingThatCooks()
        {
            ColonyWorld colony = Board();
            int heater = Open(colony, 4, 6);
            RaiseNow(colony, heater, BuildingHandle.Heater);
            Assert.That(Edit(colony, heater, BillEdit.Add, RecipeHandle.Meal), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(Edit(colony, Open(colony, 5, 7), BillEdit.Add, RecipeHandle.Meal),
                Is.EqualTo(IntentRejection.NotPermitted), "an empty cell is not a station");

            int galley = PoweredGalley(colony);
            Assert.That(Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal), Is.EqualTo(IntentRejection.None));
            Assert.That(Edit(colony, galley, BillEdit.Add, 99), Is.EqualTo(IntentRejection.NotPermitted),
                "a recipe that does not exist");
        }

        [Test]
        public void TheBillListIsEditedInPlace()
        {
            ColonyWorld colony = Board();
            int galley = PoweredGalley(colony);
            Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal);
            Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal);
            CookStation station = KitchenOf(colony).At(colony.Grid.Edifice[galley])!;

            Assert.That(station.Bills[0].Mode, Is.EqualTo(BillModeHandle.UntilYouHave), "until-N is the default");
            Assert.That(station.Bills[0].Target, Is.EqualTo(Kitchen.DefaultTarget));

            Assert.That(Edit(colony, galley, BillEdit.SetMode, 1, BillModeHandle.Times), Is.EqualTo(IntentRejection.None));
            Assert.That(Edit(colony, galley, BillEdit.SetTarget, 1, 5_000), Is.EqualTo(IntentRejection.None));
            Assert.That(station.Bills[1].Target, Is.EqualTo(Kitchen.MaxTarget), "clamped");
            Assert.That(Edit(colony, galley, BillEdit.SetTarget, 1, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(station.Bills[1].Target, Is.EqualTo(1), "clamped at the bottom too");

            Bill second = station.Bills[1];
            Assert.That(Edit(colony, galley, BillEdit.MoveUp, 1), Is.EqualTo(IntentRejection.None));
            Assert.That(station.Bills[0], Is.SameAs(second));
            Assert.That(Edit(colony, galley, BillEdit.MoveUp, 0), Is.EqualTo(IntentRejection.AlreadyInThatState));
            Assert.That(Edit(colony, galley, BillEdit.MoveDown, 1), Is.EqualTo(IntentRejection.AlreadyInThatState));

            Assert.That(Edit(colony, galley, BillEdit.SetSuspended, 0, 1), Is.EqualTo(IntentRejection.None));
            Assert.That(station.Bills[0].Suspended, Is.True);
            Assert.That(Edit(colony, galley, BillEdit.Remove, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(station.Bills.Count, Is.EqualTo(1));
            Assert.That(Edit(colony, galley, BillEdit.Remove, 5), Is.EqualTo(IntentRejection.NotPermitted));
        }

        /// <summary>
        /// Five bills, the rows the pane is laid out for (design 48 §14): the interface's
        /// <c>BillsModel.MaxRows</c> is the same five, held by its own test.
        /// </summary>
        [Test]
        public void AStationHoldsFiveBillsAndRefusesASixth()
        {
            ColonyWorld colony = Board();
            int galley = PoweredGalley(colony);
            Assert.That(Kitchen.MaxBills, Is.EqualTo(5));
            for (int i = 0; i < Kitchen.MaxBills; i++)
                Assert.That(Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal), Is.EqualTo(IntentRejection.None));
            Assert.That(Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal), Is.EqualTo(IntentRejection.NotPermitted));
        }

        [Test]
        public void TheBillsArePublishedWithTheirStation()
        {
            ColonyWorld colony = Board();
            int galley = PoweredGalley(colony);
            Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal);
            Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal);
            Edit(colony, galley, BillEdit.SetMode, 1, BillModeHandle.Forever);
            colony.World.Tick();

            var frame = colony.World.Views.Current;
            Assert.That(frame.StationCount, Is.EqualTo(1));
            StationView view = frame.Stations[0];
            Assert.That(view.CellIndex, Is.EqualTo(galley));
            Assert.That(view.Edifice, Is.EqualTo(EdificeHandle.Galley));
            Assert.That(view.Ready, Is.True);
            Assert.That(view.BillCount, Is.EqualTo(2));
            Assert.That(frame.Bills[view.FirstBill + 1].Mode, Is.EqualTo(BillModeHandle.Forever));
        }

        /// <summary>
        /// The station says how many meals the raw food on the map would make (design 48 §14):
        /// thirty carrots are 5,400 of nutrition, ten meals of 500; none says none.
        /// </summary>
        [Test]
        public void TheStationPublishesHowMuchThereIsToCook()
        {
            ColonyWorld colony = Board();
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && colony.Pawns.Content.Items[items[i].DefIndex].rawIngredient)
                    colony.Pawns.Items.Despawn(items[i]);
            int galley = PoweredGalley(colony);
            Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal);
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.Stations[0].RawMeals, Is.Zero);

            Stock(colony, Carrots, 30);
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.Stations[0].RawMeals, Is.EqualTo(10));
        }

        // ---- the cook -----------------------------------------------------------------------------

        /// <summary>
        /// The whole of K1 as a player meets it: a powered galley, a bill, a pile of carrots, and a
        /// colonist who fetches three at a time and puts a meal down. Carrots are not meat, so the
        /// meal is a vegetable one — and at level 14 it never burns.
        /// </summary>
        [Test]
        public void ACookTurnsCarrotsIntoAVegetableMeal()
        {
            ColonyWorld colony = Board();
            int galley = PoweredGalley(colony);
            OnlyCook(colony, 14);
            Stock(colony, Carrots, 30);
            Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal);

            Assert.That(RunUntil(colony, () => OnTheBoard(colony, VegMeal) >= 1, 8_000), Is.True,
                "a vegetable meal was cooked");
            Assert.That(OnTheBoard(colony, Meal), Is.Zero, "no meat went in");
            Assert.That(OnTheBoard(colony, Burnt), Is.Zero, "a level-14 cook never burns");
            Assert.That(OnTheBoard(colony, Carrots), Is.EqualTo(27), "three carrots, 540 of the meal's 500");
            CookStation station = KitchenOf(colony).At(colony.Grid.Edifice[galley])!;
            Assert.That(station.PanNutrition, Is.EqualTo(40), "and the forty over stay in the pan for the next");
        }

        [Test]
        public void ANoviceBurnsSomeAndAMasterNone()
        {
            int burntByNovice = 0, burntByMaster = 0;
            foreach (int level in new[] { 0, 14 })
            {
                ColonyWorld colony = Board();
                int galley = PoweredGalley(colony);
                OnlyCook(colony, level);
                Stock(colony, Carrots, 60);
                Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal);
                Edit(colony, galley, BillEdit.SetMode, 0, BillModeHandle.Times);
                Edit(colony, galley, BillEdit.SetTarget, 0, 16);
                RunUntil(colony, () => MealsMade(colony) >= 16, 60_000);
                Assume.That(MealsMade(colony), Is.EqualTo(16), "the bill ran to its count");
                if (level == 0) burntByNovice = OnTheBoard(colony, Burnt);
                else burntByMaster = OnTheBoard(colony, Burnt);
            }

            Assert.That(burntByMaster, Is.Zero);
            Assert.That(burntByNovice, Is.GreaterThan(0), "sixteen meals at 30 % burn some");
            Assert.That(burntByNovice, Is.LessThan(16), "and not all");
        }

        [Test]
        public void AnUnpoweredGalleyCooksNothing()
        {
            ColonyWorld colony = Board();
            int galley = Open(colony, 6, 3);
            RaiseNow(colony, galley, BuildingHandle.Galley);
            OnlyCook(colony, 10);
            Stock(colony, Carrots, 30);
            Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal);

            for (int t = 0; t < 4_000; t++) colony.World.Tick();
            Assert.That(MealsMade(colony), Is.Zero);
            Assert.That(OnTheBoard(colony, Carrots), Is.EqualTo(30), "nobody fetched for a dead galley");
            Assert.That(colony.World.Views.Current.Stations[0].Ready, Is.False);
        }

        [Test]
        public void UntilYouHaveStopsAtTheTarget()
        {
            ColonyWorld colony = Board();
            int galley = PoweredGalley(colony);
            OnlyCook(colony, 14);
            Stock(colony, Carrots, 60);
            Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal);
            Edit(colony, galley, BillEdit.SetTarget, 0, 2);

            Assert.That(RunUntil(colony, () => MealsMade(colony) >= 2, 20_000), Is.True);
            for (int t = 0; t < 4_000; t++) colony.World.Tick();
            Assert.That(MealsMade(colony), Is.EqualTo(2), "the bill is satisfied and nobody cooks a third");
            CookStation station = KitchenOf(colony).At(colony.Grid.Edifice[galley])!;
            Assert.That(KitchenOf(colony).Satisfied(station.Bills[0]), Is.True);
        }

        [Test]
        public void ASuspendedBillIsSkipped()
        {
            ColonyWorld colony = Board();
            int galley = PoweredGalley(colony);
            OnlyCook(colony, 14);
            Stock(colony, Carrots, 30);
            Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal);
            Edit(colony, galley, BillEdit.SetSuspended, 0, 1);

            for (int t = 0; t < 4_000; t++) colony.World.Tick();
            Assert.That(MealsMade(colony), Is.Zero);
        }

        [Test]
        public void ACampfireMealCostsOneWood()
        {
            ColonyWorld colony = Board();
            int fire = Open(colony, 6, 3);
            RaiseNow(colony, fire, BuildingHandle.Campfire);
            OnlyCook(colony, 14);
            Stock(colony, Carrots, 30);
            Stock(colony, ItemHandle.Wood, 5, dx: -3, dz: 1);
            int woodBefore = OnTheBoard(colony, ItemHandle.Wood);
            Edit(colony, fire, BillEdit.Add, RecipeHandle.Meal);
            Edit(colony, fire, BillEdit.SetMode, 0, BillModeHandle.Times);
            Edit(colony, fire, BillEdit.SetTarget, 0, 1);

            Assert.That(RunUntil(colony, () => MealsMade(colony) >= 1, 12_000), Is.True);
            Assert.That(OnTheBoard(colony, ItemHandle.Wood), Is.EqualTo(woodBefore - 1));
        }

        [Test]
        public void ACampfireWithNoWoodCooksNothing()
        {
            ColonyWorld colony = Board();
            int fire = Open(colony, 6, 3);
            RaiseNow(colony, fire, BuildingHandle.Campfire);
            OnlyCook(colony, 14);
            Stock(colony, Carrots, 30);
            Edit(colony, fire, BillEdit.Add, RecipeHandle.Meal);

            for (int t = 0; t < 6_000; t++) colony.World.Tick();
            Assert.That(MealsMade(colony), Is.Zero, "the food is in the pan and the fire has nothing to burn");
            CookStation station = KitchenOf(colony).At(colony.Grid.Edifice[fire])!;
            Assert.That(station.PanNutrition, Is.GreaterThanOrEqualTo(500), "the carrots went in all the same");
        }

        /// <summary>
        /// The pan is the station's: a meal half cooked when the power goes is waiting on the hob,
        /// work banked, when it comes back, and the next cook finishes it rather than starting again.
        /// </summary>
        [Test]
        public void AMealOnTheHobWaitsOutAPowerCut()
        {
            ColonyWorld colony = Board();
            int galley = PoweredGalley(colony);
            OnlyCook(colony, 0);
            Stock(colony, Carrots, 30);
            Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal);
            Kitchen kitchen = KitchenOf(colony);
            CookStation station = kitchen.At(colony.Grid.Edifice[galley])!;

            Assert.That(RunUntil(colony, () => station.CookMilliwork > 0, 8_000), Is.True, "cooking started");
            Assert.That(colony.Pawns.Power!.SetSwitch(galley, on: false), Is.EqualTo(IntentRejection.None));
            for (int t = 0; t < 600; t++) colony.World.Tick();
            int banked = station.CookMilliwork;
            Assert.That(banked, Is.GreaterThan(0), "the work done is kept");
            Assert.That(MealsMade(colony), Is.Zero);
            for (int t = 0; t < 600; t++) colony.World.Tick();
            Assert.That(station.CookMilliwork, Is.EqualTo(banked), "and nothing more is done without power");

            colony.Pawns.Power!.SetSwitch(galley, on: true);
            Assert.That(RunUntil(colony, () => MealsMade(colony) >= 1, 8_000), Is.True, "and it is finished");
            Assert.That(OnTheBoard(colony, Carrots), Is.EqualTo(27), "from the same three carrots");
        }

        [Test]
        public void TakingTheStationDownTakesItsBillsWithIt()
        {
            ColonyWorld colony = Board();
            int galley = PoweredGalley(colony);
            Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal);
            int edifice = colony.Grid.Edifice[galley];
            Assert.That(colony.Construction.Demolish(colony.Pawns, galley, out _), Is.True);
            Assert.That(KitchenOf(colony).At(edifice), Is.Null);
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.StationCount, Is.Zero);
        }

        [Test]
        public void BillsAndThePanComeBackFromASave()
        {
            ColonyWorld colony = Board();
            int galley = PoweredGalley(colony);
            Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal);
            Edit(colony, galley, BillEdit.Add, RecipeHandle.Meal);
            Edit(colony, galley, BillEdit.SetMode, 1, BillModeHandle.Times);
            Edit(colony, galley, BillEdit.SetTarget, 1, 7);
            CookStation station = KitchenOf(colony).At(colony.Grid.Edifice[galley])!;
            station.PanNutrition = 360;
            station.CookMilliwork = 12_345;
            station.Burn = 1;

            colony.RebuildDerived();
            ulong before = colony.World.ComputeStateHash().Value;
            using var stream = new MemoryStream();
            WorldSave.Save(colony.World, stream, colony.SaveComponents);
            stream.Position = 0;

            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 2;
            scenario.beds = 2;
            scenario.startingFellRadius = 0;
            ColonyWorld fresh = ColonyWorld.Build(Size, 1u, scenario, barren: true, wooded: false);
            WorldSave.Load(fresh.World, stream, fresh.SaveComponents);
            fresh.RebuildDerived();

            Assert.That(fresh.World.ComputeStateHash().Value, Is.EqualTo(before));
            CookStation back = fresh.Pawns.Kitchen!.At(fresh.Grid.Edifice[galley])!;
            Assert.That(back.Bills.Count, Is.EqualTo(2));
            Assert.That(back.Bills[1].Mode, Is.EqualTo(BillModeHandle.Times));
            Assert.That(back.Bills[1].Target, Is.EqualTo(7));
            Assert.That(back.PanNutrition, Is.EqualTo(360));
            Assert.That(back.CookMilliwork, Is.EqualTo(12_345));
            Assert.That(back.Burn, Is.EqualTo(1), "the roll is not made again after a load");
        }

        [Test]
        public void AKitchenNobodyUsedHashesAsNothing()
        {
            ColonyWorld colony = Board();
            var hash = new StateHash();
            KitchenOf(colony).ContributeTo(ref hash);
            Assert.That(hash.Value, Is.EqualTo(new StateHash().Value), "so a colony that never cooks hashes as before");
        }

        // ---- eating -------------------------------------------------------------------------------

        static Job? WhatSheWouldEat(ColonyWorld colony, Pawn pawn)
        {
            pawn.Needs[NeedIndex.Food] = 100;
            var job = new Job();
            bool found = new CriticalNeedsThinkNode().TryGiveJob(pawn, colony.Pawns, job);
            return found && job.DefIndex == JobIndex.Eat ? job : null;
        }

        static ThingId SpawnAt(ColonyWorld colony, int item, int dx, int dz, int count = 1)
        {
            int cell = Open(colony, dx, dz);
            return colony.Pawns.Items.Spawn(item, cell, count);
        }

        static void ClearFood(ColonyWorld colony)
        {
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && colony.Pawns.Content.Items[items[i].DefIndex].nutrition > 0)
                    colony.Pawns.Items.Despawn(items[i]);
        }

        /// <summary>Design 48 §8: the best food, then the nearest of it. A meal across the room beats a carrot at her feet.</summary>
        [Test]
        public void AHungryColonistTakesTheBestFoodBeforeTheNearest()
        {
            ColonyWorld colony = Board();
            ClearFood(colony);
            Pawn pawn = colony.Pawns.Pawns.All.First(p => p.IsColonist);
            CellRef at = Size.FromIndex(pawn.Cell);
            CellRef s = colony.Start;
            int dx = at.X - s.X, dz = at.Z - s.Z;

            ThingId carrot = SpawnAt(colony, Carrots, dx + 1, dz);
            ThingId ration = SpawnAt(colony, Rations, dx + 4, dz);
            ThingId meal = SpawnAt(colony, VegMeal, dx + 8, dz);

            Assert.That(WhatSheWouldEat(colony, pawn)!.TargetItem, Is.EqualTo(meal), "a cooked meal first");
            colony.Pawns.Items.Despawn(colony.Pawns.Items.Get(meal)!);
            Assert.That(WhatSheWouldEat(colony, pawn)!.TargetItem, Is.EqualTo(ration), "then a ration");
            colony.Pawns.Items.Despawn(colony.Pawns.Items.Get(ration)!);
            Assert.That(WhatSheWouldEat(colony, pawn)!.TargetItem, Is.EqualTo(carrot), "then raw food");
        }

        [Test]
        public void ABurntMealRanksUnderARationAndOverACarrot()
        {
            PawnContent content = ContentPack.Pawns();
            int burnt = content.Items[Burnt].foodTier;
            Assert.That(burnt, Is.GreaterThan(content.Items[Rations].foodTier));
            Assert.That(burnt, Is.LessThan(content.Items[Carrots].foodTier));
            Assert.That(content.Items[Meal].foodTier, Is.EqualTo(content.Items[VegMeal].foodTier),
                "a meal with meat and one without are as welcome");
        }

        /// <summary>What she thinks of what she ate is the food's own (design 48 §4).</summary>
        [Test]
        public void EachFoodIsThoughtOfAsItDeserves()
        {
            PawnContent content = ContentPack.Pawns();
            Assert.That(content.Items[Meal].ateThought, Is.EqualTo(ThoughtIndex.AteMeal));
            Assert.That(content.Items[VegMeal].ateThought, Is.EqualTo(ThoughtIndex.AteMeal));
            Assert.That(content.Items[Rations].ateThought, Is.EqualTo(ThoughtIndex.AteRation));
            Assert.That(content.Items[Burnt].ateThought, Is.EqualTo(ThoughtIndex.AteBurnt));
            Assert.That(content.Items[Carrots].ateThought, Is.EqualTo(ThoughtIndex.AteRaw));

            // Every food that is not cooked is raw: the wild foods (design 45) as much as the
            // carrots. foodTier defaults to 0, the cooked meal's, so a raw food that forgot to say
            // so would be taken before a meal — which is what the merge with main nearly shipped.
            for (int i = 0; i < content.Items.Length; i++)
            {
                ItemDef food = content.Items[i];
                if (food.nutrition <= 0 || i == Meal || i == VegMeal || i == Burnt || i == Rations) continue;
                Assert.That(food.rawIngredient, Is.True, food.defName + " is raw food a cook can use");
                Assert.That(food.foodTier, Is.GreaterThanOrEqualTo(3), food.defName + " ranks under the ration and the burnt meal");
                Assert.That(food.ateThought, Is.EqualTo(ThoughtIndex.AteRaw), food.defName + " is thought of as raw");
            }

            int meal = content.Thoughts[ThoughtIndex.AteMeal].moodOffset;
            int ration = content.Thoughts[ThoughtIndex.AteRation].moodOffset;
            int burntMood = content.Thoughts[ThoughtIndex.AteBurnt].moodOffset;
            int raw = content.Thoughts[ThoughtIndex.AteRaw].moodOffset;
            Assert.That(meal, Is.GreaterThan(ration));
            Assert.That(ration, Is.EqualTo(20), "a ration is what every food was worth before the kitchen");
            Assert.That(burntMood, Is.LessThan(0));
            Assert.That(raw, Is.LessThan(0));
        }

        [Test]
        public void EatingACarrotLeavesARawFoodThought()
        {
            ColonyWorld colony = Board();
            ClearFood(colony);
            Pawn pawn = colony.Pawns.Pawns.All.First(p => p.IsColonist);
            CellRef at = Size.FromIndex(pawn.Cell);
            CellRef s = colony.Start;
            SpawnAt(colony, Carrots, at.X - s.X + 1, at.Z - s.Z, 5);
            pawn.Needs[NeedIndex.Food] = 100;

            Assert.That(RunUntil(colony, () => pawn.Memories.Any(m => m.ThoughtIndex == ThoughtIndex.AteRaw), 3_000),
                Is.True);
            Assert.That(pawn.Memories.Any(m => m.ThoughtIndex == ThoughtIndex.AteMeal), Is.False,
                "and not the cooked meal's, which every food used to give");
        }
    }
}
