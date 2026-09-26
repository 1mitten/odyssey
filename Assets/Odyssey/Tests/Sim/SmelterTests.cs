#nullable enable
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Cooking;
using Odyssey.Sim.Crafting;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Power;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The smelter (design 62 §9, DM8): a crafted recipe with ingredient lists, a sibling work
    /// giver, a hopper taking coal or wood, iron and copper bars, and where the bars are spent —
    /// each rule about behaviour run end to end by the colony's own colonists, as the kitchen's
    /// are, because a bill nobody works looks exactly like a bill never added.
    /// </summary>
    public class SmelterTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        const int Ore = ItemHandle.IronOre;
        const int CopperOre = ItemHandle.CopperOre;
        const int Bar = ItemHandle.IronBar;
        const int CopperBar = ItemHandle.CopperBar;
        const int Coal = ItemHandle.Coal;
        const int Wood = ItemHandle.Wood;

        // ---- fixture ----------------------------------------------------------------------------

        static ColonyWorld Board(int colonists = 2)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = colonists;
            scenario.startingFellRadius = 0;
            ColonyWorld colony = ColonyWorld.Build(Size, 1u, scenario, barren: true, wooded: false);
            colony.World.Tick();
            return colony;
        }

        static Workshop ShopOf(ColonyWorld colony) => colony.Pawns.Workshop!;

        static int Open(ColonyWorld colony, int dx, int dz)
        {
            CellRef s = colony.Start;
            int cell = Size.Index(s.X + dx, s.Z + dz, s.Y);
            Assume.That(colony.Construction.Allows(cell), Is.True, "an ordinary buildable cell");
            return cell;
        }

        static int RaiseNow(ColonyWorld colony, int cell, int building, int facing = 0)
        {
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), building, StuffHandle.Stone, facing),
                Is.EqualTo(IntentRejection.None));
            colony.Construction.Deliver(cell, ConstructionContent.BuildingAt(building).costCount);
            Assert.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
            return colony.Grid.Edifice[cell];
        }

        static int Smelter(ColonyWorld colony)
        {
            int cell = Open(colony, 6, 3);
            RaiseNow(colony, cell, BuildingHandle.Smelter);
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

        static void Clear(ColonyWorld colony, int item)
        {
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == item) colony.Pawns.Items.Despawn(items[i]);
        }

        static IntentRejection Edit(ColonyWorld colony, int station, int op, int b = 0, int c = 0) =>
            ShopOf(colony).HandleEditBill(new Intent(IntentKind.EditBill, Size.FromIndex(station), op, b, c));

        /// <summary>Through the world, so the colony's router decides whose list it is.</summary>
        static IntentRejection Send(ColonyWorld colony, Intent intent)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(intent);
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        static bool RunUntil(ColonyWorld colony, Func<bool> done, int maxTicks)
        {
            for (int t = 0; t < maxTicks; t++)
            {
                if (done()) return true;
                colony.World.Tick();
            }
            return done();
        }

        /// <summary>One colonist the only crafter; everybody still hauls, which is who fills the hopper.</summary>
        static Pawn OnlyCrafter(ColonyWorld colony)
        {
            var colonists = colony.Pawns.Pawns.All.Where(p => p.IsColonist).ToList();
            Pawn crafter = colonists[0];
            foreach (Pawn p in colonists)
                p.WorkPriorities[WorkTypeIndex.Crafting] = (byte)(p == crafter ? 1 : 0);
            return crafter;
        }

        static CraftStation StationAt(ColonyWorld colony, int cell) => ShopOf(colony).At(colony.Grid.Edifice[cell])!;

        /// <summary>Fuel units of one kind, on the board and in the hopper both: what has not been burnt yet.</summary>
        static int Unburnt(ColonyWorld colony, int item, CraftStation station, int worth) =>
            OnTheBoard(colony, item) + station.Fuel / worth;

        // ---- content ------------------------------------------------------------------------------

        [Test]
        public void TheSmeltingRecipesAreCraftedAndTheMealIsNot()
        {
            PawnContent content = ContentPack.Pawns();
            Assert.That(content.Recipes.Length, Is.EqualTo(RecipeHandle.Count));
            Assert.That(content.Recipes[RecipeHandle.Meal].Crafted, Is.False, "the kitchen's recipe keeps its shape");

            RecipeDef iron = content.Recipes[RecipeHandle.SmeltIron];
            Assert.That(iron.Crafted, Is.True);
            Assert.That(iron.ingredients.Single().Item, Is.EqualTo(Ore));
            Assert.That(iron.ingredients.Single().count, Is.EqualTo(10));
            Assert.That(iron.products.Single().Item, Is.EqualTo(Bar));
            Assert.That(iron.products.Single().count, Is.EqualTo(5));
            Assert.That(iron.At(BuildingHandle.Smelter)!.fuelPerBatch, Is.EqualTo(3));
            Assert.That(iron.At(BuildingHandle.Galley), Is.Null, "a cooker smelts nothing");

            RecipeDef copper = content.Recipes[RecipeHandle.SmeltCopper];
            Assert.That(copper.ingredients.Single().Item, Is.EqualTo(CopperOre));
            Assert.That(copper.products.Single().Item, Is.EqualTo(CopperBar));
            Assert.That(content.Recipes[RecipeHandle.Meal].At(BuildingHandle.Smelter), Is.Null, "a smelter cooks nothing");
        }

        /// <summary>The owner's rule, coal the better, as one number on the building: a batch is one coal or three wood.</summary>
        [Test]
        public void CoalIsWorthThreeWoodInTheHopper()
        {
            BuildingDef smelter = ConstructionContent.BuildingAt(BuildingHandle.Smelter);
            Assert.That(smelter.HasHopper, Is.True);
            Assert.That(smelter.IsPowered, Is.False, "no power");
            Assert.That(smelter.HasParts, Is.False, "and no scrap metal");
            Assert.That(smelter.HopperWorth(Coal), Is.EqualTo(3));
            Assert.That(smelter.HopperWorth(Wood), Is.EqualTo(1));
            Assert.That(smelter.HopperWorth(Ore), Is.Zero, "ore is not fuel");
            Assert.That(smelter.hopperFuels[0].item, Is.EqualTo(Coal), "coal is looked for first");
            int batch = ContentPack.Pawns().Recipes[RecipeHandle.SmeltIron].At(BuildingHandle.Smelter)!.fuelPerBatch;
            Assert.That(batch / smelter.HopperWorth(Coal), Is.EqualTo(1));
            Assert.That(batch / smelter.HopperWorth(Wood), Is.EqualTo(3));
        }

        [Test]
        public void TheKitchenAndTheWorkshopNeverClaimTheSameStation()
        {
            ColonyWorld colony = Board();
            int smelter = Smelter(colony);
            int fire = Open(colony, 6, 6);
            RaiseNow(colony, fire, BuildingHandle.Campfire);

            Assert.That(ShopOf(colony).IsStation(colony.Grid.Edifice[smelter]), Is.True);
            Assert.That(colony.Pawns.Kitchen!.IsStation(colony.Grid.Edifice[smelter]), Is.False);
            Assert.That(ShopOf(colony).IsStation(colony.Grid.Edifice[fire]), Is.False);
            Assert.That(colony.Pawns.Kitchen!.IsStation(colony.Grid.Edifice[fire]), Is.True);

            // Through the router, both ways, and each refuses the other's recipe.
            Assert.That(Send(colony, new Intent(IntentKind.EditBill, Size.FromIndex(smelter), BillEdit.Add, RecipeHandle.SmeltIron)),
                Is.EqualTo(IntentRejection.None));
            Assert.That(Send(colony, new Intent(IntentKind.EditBill, Size.FromIndex(smelter), BillEdit.Add, RecipeHandle.Meal)),
                Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(Send(colony, new Intent(IntentKind.EditBill, Size.FromIndex(fire), BillEdit.Add, RecipeHandle.Meal)),
                Is.EqualTo(IntentRejection.None));
            Assert.That(Send(colony, new Intent(IntentKind.EditBill, Size.FromIndex(fire), BillEdit.Add, RecipeHandle.SmeltIron)),
                Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(StationAt(colony, smelter).Bills.Count, Is.EqualTo(1));
            Assert.That(colony.Pawns.Kitchen!.At(colony.Grid.Edifice[fire])!.Bills.Count, Is.EqualTo(1));
        }

        // ---- smelting -----------------------------------------------------------------------------

        /// <summary>
        /// The whole of DM8 as a player meets it: a smelter, a bill, iron ore and coal. A hauler
        /// fills the hopper, a crafter fetches ten ore, works the batch, and five iron bars go down
        /// — one coal burnt.
        /// </summary>
        [Test]
        public void ABillMakesIronBarsFromOreBurningOneCoal()
        {
            ColonyWorld colony = Board();
            int smelter = Smelter(colony);
            OnlyCrafter(colony);
            Stock(colony, Ore, 20);
            Stock(colony, Coal, 5, dx: -3, dz: 1);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltIron);
            Edit(colony, smelter, BillEdit.SetMode, 0, BillModeHandle.Times);
            Edit(colony, smelter, BillEdit.SetTarget, 0, 5);
            CraftStation station = StationAt(colony, smelter);

            Assert.That(RunUntil(colony, () => OnTheBoard(colony, Bar) >= 5, 20_000), Is.True, "five iron bars were made");
            for (int t = 0; t < 2_000; t++) colony.World.Tick();
            Assert.That(OnTheBoard(colony, Bar), Is.EqualTo(5), "one batch, and the bill is done");
            Assert.That(OnTheBoard(colony, Ore), Is.EqualTo(10), "ten ore went in");
            Assert.That(Unburnt(colony, Coal, station, 3), Is.EqualTo(4), "one coal burnt");
            Assert.That(station.Bills[0].Done, Is.EqualTo(5), "the bill counts bars, as its target does");
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Craft), Is.GreaterThan(0));
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Refuel), Is.GreaterThan(0), "a hauler filled the hopper");
        }

        [Test]
        public void TheSameBatchOnWoodBurnsThree()
        {
            ColonyWorld colony = Board();
            int smelter = Smelter(colony);
            OnlyCrafter(colony);
            Clear(colony, Wood);
            Stock(colony, Ore, 10);
            Stock(colony, Wood, 20, dx: -3, dz: 1);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltIron);
            Edit(colony, smelter, BillEdit.SetMode, 0, BillModeHandle.Times);
            Edit(colony, smelter, BillEdit.SetTarget, 0, 5);
            CraftStation station = StationAt(colony, smelter);

            Assert.That(RunUntil(colony, () => OnTheBoard(colony, Bar) >= 5, 20_000), Is.True);
            Assert.That(Unburnt(colony, Wood, station, 1), Is.EqualTo(17), "three wood burnt where coal burns one");
        }

        /// <summary>Coal before wood: with both on the map the hopper is filled with coal.</summary>
        [Test]
        public void AHaulerFillsTheHopperWithCoalWhenThereIsAny()
        {
            ColonyWorld colony = Board();
            int smelter = Smelter(colony);
            OnlyCrafter(colony);
            Stock(colony, Wood, 30, dx: 3, dz: 1);
            Stock(colony, Coal, 10, dx: -3, dz: 1);
            int woodBefore = OnTheBoard(colony, Wood);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltIron);
            CraftStation station = StationAt(colony, smelter);

            Assert.That(RunUntil(colony, () => station.Fuel > 0, 6_000), Is.True);
            Assert.That(station.Fuel, Is.EqualTo(30), "ten coal at three");
            Assert.That(OnTheBoard(colony, Wood), Is.EqualTo(woodBefore), "no wood went in");
        }

        [Test]
        public void AnEmptyHopperStopsTheWorkAndAHaulerRefuelsIt()
        {
            ColonyWorld colony = Board();
            int smelter = Smelter(colony);
            OnlyCrafter(colony);
            Clear(colony, Wood);
            Stock(colony, Ore, 10);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltIron);
            CraftStation station = StationAt(colony, smelter);

            for (int t = 0; t < 5_000; t++) colony.World.Tick();
            Assert.That(OnTheBoard(colony, Bar), Is.Zero, "nothing to burn, nothing smelted");
            Assert.That(station.In[0], Is.EqualTo(10), "the ore went in all the same");
            Assert.That(station.CraftMilliwork, Is.Zero, "and no work was done on it");
            StationView view = colony.World.Views.Current.Stations.ToArray().Single(s => s.CellIndex == smelter);
            Assert.That(view.Ready, Is.False, "the pane says it cannot work");
            Assert.That(view.FuelBatches, Is.Zero);

            Stock(colony, Coal, 3, dx: -3, dz: 1);
            Assert.That(RunUntil(colony, () => OnTheBoard(colony, Bar) >= 5, 12_000), Is.True, "fuelled, it smelts");
        }

        [Test]
        public void ASmelterWithNoBillIsNotFed()
        {
            ColonyWorld colony = Board();
            int smelter = Smelter(colony);
            Stock(colony, Coal, 10, dx: -3, dz: 1);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltIron);
            Edit(colony, smelter, BillEdit.SetSuspended, 0, 1);
            for (int t = 0; t < 3_000; t++) colony.World.Tick();
            Assert.That(StationAt(colony, smelter).Fuel, Is.Zero, "a paused smelter draws no fuel into it");
        }

        /// <summary>The batch is the station's: a crafter drafted away leaves the work banked, and it is finished from the same ore.</summary>
        [Test]
        public void AnInterruptedBatchKeepsItsWork()
        {
            ColonyWorld colony = Board();
            int smelter = Smelter(colony);
            Pawn crafter = OnlyCrafter(colony);
            Stock(colony, Ore, 10);
            Stock(colony, Coal, 5, dx: -3, dz: 1);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltIron);
            CraftStation station = StationAt(colony, smelter);

            Assert.That(RunUntil(colony, () => station.CraftMilliwork > 0, 12_000), Is.True, "the work started");
            Assert.That(Send(colony, new Intent(IntentKind.SetDrafted, default, crafter.Id.Value, 1)), Is.EqualTo(IntentRejection.None));
            for (int t = 0; t < 300; t++) colony.World.Tick();
            int banked = station.CraftMilliwork;
            Assert.That(banked, Is.GreaterThan(0), "the work done is kept");
            Assert.That(station.FuelPaid, Is.True, "and the fuel it was paid with");
            for (int t = 0; t < 600; t++) colony.World.Tick();
            Assert.That(station.CraftMilliwork, Is.EqualTo(banked), "nobody works it meanwhile");
            Assert.That(OnTheBoard(colony, Bar), Is.Zero);

            int coalLeft = Unburnt(colony, Coal, station, 3);
            Send(colony, new Intent(IntentKind.SetDrafted, default, crafter.Id.Value, 0));
            Assert.That(RunUntil(colony, () => OnTheBoard(colony, Bar) >= 5, 12_000), Is.True, "and it is finished");
            Assert.That(OnTheBoard(colony, Ore), Is.Zero, "from the same ten ore");
            Assert.That(Unburnt(colony, Coal, station, 3), Is.EqualTo(coalLeft), "and no second coal");
        }

        // ---- bills ------------------------------------------------------------------------------

        [Test]
        public void UntilYouHaveCountsBars()
        {
            ColonyWorld colony = Board();
            int smelter = Smelter(colony);
            OnlyCrafter(colony);
            Stock(colony, Ore, 40);
            Stock(colony, Coal, 10, dx: -3, dz: 1);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltIron);
            Edit(colony, smelter, BillEdit.SetTarget, 0, 6);

            Assert.That(RunUntil(colony, () => OnTheBoard(colony, Bar) >= 10, 30_000), Is.True, "two batches for six bars");
            for (int t = 0; t < 3_000; t++) colony.World.Tick();
            Assert.That(OnTheBoard(colony, Bar), Is.EqualTo(10), "ten held is past six, and nobody smelts a third");
            Assert.That(ShopOf(colony).Satisfied(StationAt(colony, smelter).Bills[0]), Is.True);
        }

        [Test]
        public void ForeverRunsUntilTheOreIsGone()
        {
            ColonyWorld colony = Board();
            int smelter = Smelter(colony);
            OnlyCrafter(colony);
            Stock(colony, Ore, 30);
            Stock(colony, Coal, 10, dx: -3, dz: 1);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltIron);
            Edit(colony, smelter, BillEdit.SetMode, 0, BillModeHandle.Forever);

            Assert.That(RunUntil(colony, () => OnTheBoard(colony, Bar) >= 15, 40_000), Is.True);
            Assert.That(OnTheBoard(colony, Ore), Is.Zero);
        }

        [Test]
        public void ASuspendedBillIsSkippedForTheNextOne()
        {
            ColonyWorld colony = Board();
            int smelter = Smelter(colony);
            OnlyCrafter(colony);
            Stock(colony, Ore, 10);
            Stock(colony, CopperOre, 10, dx: 3, dz: -3);
            Stock(colony, Coal, 5, dx: -3, dz: 1);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltIron);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltCopper);
            Edit(colony, smelter, BillEdit.SetSuspended, 0, 1);

            Assert.That(RunUntil(colony, () => OnTheBoard(colony, CopperBar) >= 5, 20_000), Is.True);
            Assert.That(OnTheBoard(colony, Bar), Is.Zero, "the paused iron bill was passed over");
        }

        [Test]
        public void ABillsRecipeCanBeChangedInPlace()
        {
            ColonyWorld colony = Board();
            int smelter = Smelter(colony);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltIron);
            Edit(colony, smelter, BillEdit.SetMode, 0, BillModeHandle.Times);
            CraftStation station = StationAt(colony, smelter);
            station.Bills[0].Done = 5;

            Assert.That(Edit(colony, smelter, BillEdit.SetRecipe, 0, RecipeHandle.SmeltCopper), Is.EqualTo(IntentRejection.None));
            Assert.That(station.Bills[0].Recipe, Is.EqualTo(RecipeHandle.SmeltCopper));
            Assert.That(station.Bills[0].Done, Is.Zero, "the count starts again");
            Assert.That(station.Bills[0].Mode, Is.EqualTo(BillModeHandle.Times), "the mode is kept");
            Assert.That(Edit(colony, smelter, BillEdit.SetRecipe, 0, RecipeHandle.SmeltCopper), Is.EqualTo(IntentRejection.AlreadyInThatState));
            Assert.That(Edit(colony, smelter, BillEdit.SetRecipe, 0, RecipeHandle.Meal), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(Edit(colony, smelter, BillEdit.Add, RecipeHandle.Meal), Is.EqualTo(IntentRejection.NotPermitted));
        }

        [Test]
        public void TheStationIsPublishedWithItsFuel()
        {
            ColonyWorld colony = Board();
            int smelter = Smelter(colony);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltIron);
            Stock(colony, Ore, 25);
            CraftStation station = StationAt(colony, smelter);
            ShopOf(colony).SetFuel(station, 14);
            colony.World.Tick();

            StationView view = colony.World.Views.Current.Stations.ToArray().Single(s => s.CellIndex == smelter);
            Assert.That(view.Edifice, Is.EqualTo(EdificeHandle.Smelter));
            Assert.That(view.BillCount, Is.EqualTo(1));
            Assert.That(view.FuelBatches, Is.EqualTo(4), "fourteen worth at three a batch");
            Assert.That(view.RawMeals, Is.EqualTo(2), "twenty-five ore is two batches");
            Assert.That(view.Ready, Is.True);
        }

        [Test]
        public void TakingTheSmelterDownTakesItsBillsAndFuelWithIt()
        {
            ColonyWorld colony = Board();
            int smelter = Smelter(colony);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltIron);
            int edifice = colony.Grid.Edifice[smelter];
            ShopOf(colony).SetFuel(StationAt(colony, smelter), 30);
            Assert.That(colony.Construction.Demolish(colony.Pawns, smelter, out _), Is.True);
            Assert.That(ShopOf(colony).At(edifice), Is.Null);
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.StationCount, Is.Zero);
        }

        [Test]
        public void BillsTheBatchAndTheHopperComeBackFromASave()
        {
            ColonyWorld colony = Board();
            int smelter = Smelter(colony);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltIron);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltCopper);
            Edit(colony, smelter, BillEdit.SetMode, 1, BillModeHandle.Times);
            Edit(colony, smelter, BillEdit.SetTarget, 1, 15);
            Workshop shop = ShopOf(colony);
            CraftStation station = StationAt(colony, smelter);
            Assert.That(shop.AddIngredient(station, RecipeHandle.SmeltIron, Ore, 10), Is.True);
            shop.SetFuel(station, 20);
            Assert.That(shop.PayFuel(station, colony.Pawns.Content.Recipes[RecipeHandle.SmeltIron]), Is.True);
            station.CraftMilliwork = 23_456;

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
            CraftStation back = fresh.Pawns.Workshop!.At(fresh.Grid.Edifice[smelter])!;
            Assert.That(back.Bills.Count, Is.EqualTo(2));
            Assert.That(back.Bills[1].Recipe, Is.EqualTo(RecipeHandle.SmeltCopper));
            Assert.That(back.Bills[1].Target, Is.EqualTo(15));
            Assert.That(back.BatchRecipe, Is.EqualTo(RecipeHandle.SmeltIron));
            Assert.That(back.In, Is.EqualTo(new[] { 10 }));
            Assert.That(back.FuelPaid, Is.True, "the batch is not charged twice after a load");
            Assert.That(back.Fuel, Is.EqualTo(17));
            Assert.That(back.CraftMilliwork, Is.EqualTo(23_456));
        }

        /// <summary>And resumed from a mid-batch save, the colony finishes the batch it was working.</summary>
        [Test]
        public void AMidBatchSaveResumesAndFinishes()
        {
            ColonyWorld colony = Board();
            int smelter = Smelter(colony);
            OnlyCrafter(colony);
            Stock(colony, Ore, 10);
            Stock(colony, Coal, 5, dx: -3, dz: 1);
            Edit(colony, smelter, BillEdit.Add, RecipeHandle.SmeltIron);
            CraftStation station = StationAt(colony, smelter);
            Assert.That(RunUntil(colony, () => station.CraftMilliwork > 0, 12_000), Is.True);

            colony.RebuildDerived();
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

            Assert.That(RunUntil(fresh, () => OnTheBoard(fresh, Bar) >= 5, 12_000), Is.True);
            Assert.That(OnTheBoard(fresh, Ore), Is.Zero);
        }

        [Test]
        public void AWorkshopNobodyUsedHashesAsNothing()
        {
            ColonyWorld colony = Board();
            Smelter(colony);
            var hash = new StateHash();
            ShopOf(colony).ContributeTo(ref hash);
            Assert.That(hash.Value, Is.EqualTo(new StateHash().Value), "so a colony that never smelts hashes as before");
        }

        // ---- where the bars go --------------------------------------------------------------------

        [Test]
        public void SteelIsBuiltOfIronBars()
        {
            Assert.That(ConstructionContent.IsBuildable(StuffHandle.Steel), Is.True);
            Assert.That(ConstructionContent.StuffAt(StuffHandle.Steel).item, Is.EqualTo(Bar));
            Assert.That(ConstructionContent.IsBuildable(StuffHandle.Concrete), Is.False, "the city's other materials stay the city's");
            Assert.That(ConstructionContent.IsBuildable(StuffHandle.Composite), Is.False);
            Assert.That(ConstructionContent.StuffAt(StuffHandle.Steel).hitPointsFactorPerMille,
                Is.GreaterThan(ConstructionContent.StuffAt(StuffHandle.Stone).hitPointsFactorPerMille), "and it stands longest");
        }

        [Test]
        public void AColonistBuildsASteelWallFromIronBars()
        {
            ColonyWorld colony = Board();
            Stock(colony, Bar, 10);
            int cell = Open(colony, 5, 5);
            Assert.That(colony.Construction.Place(Size.FromIndex(cell), BuildingHandle.Wall, StuffHandle.Steel, 0),
                Is.EqualTo(IntentRejection.None));

            Assert.That(RunUntil(colony, () => colony.Grid.Edifice[cell] >= 0, 20_000), Is.True, "the wall went up");
            var placed = colony.Construction.Edifices.Records[colony.Grid.Edifice[cell]];
            Assert.That(placed.Stuff, Is.EqualTo(ConstructionContent.StuffAt(StuffHandle.Steel).stuff));
            Assert.That(OnTheBoard(colony, Bar), Is.EqualTo(10 - ConstructionContent.BuildingAt(BuildingHandle.Wall).costCount));
        }

        [Test]
        public void OnlyAPowerLineTakesACopperBarInPlaceOfScrap()
        {
            for (int b = 1; b < BuildingHandle.Count; b++)
            {
                BuildingDef def = ConstructionContent.BuildingAt(b);
                if (b == BuildingHandle.Conduit) Assert.That(def.partAltItem, Is.EqualTo(CopperBar));
                else Assert.That(def.partAltItem, Is.EqualTo(-1), def.defName + ": a site banks its parts by count and cannot tell them apart");
            }
        }

        [Test]
        public void APowerLineIsLaidWithACopperBarWhenThereIsNoScrap()
        {
            ColonyWorld colony = Board(3);
            PowerGrid power = colony.Pawns.Power!;
            Clear(colony, ItemHandle.Salvage);
            Stock(colony, CopperBar, 4);
            for (int x = 3; x <= 5; x++)
                colony.World.Intents.Submit(new Intent(IntentKind.PlaceBuilding, Size.FromIndex(Open(colony, x, 6)),
                    BuildingHandle.Conduit, StuffHandle.Wood));
            colony.World.Tick();
            Assume.That(power.Sites.Count, Is.EqualTo(3));

            Assert.That(RunUntil(colony, () => power.Sites.Count == 0, 30_000), Is.True, "every line was laid");
            colony.World.Tick(200);
            Assert.That(power.Lines.Count, Is.EqualTo(3));
            Assert.That(OnTheBoard(colony, CopperBar), Is.EqualTo(1), "one copper bar a line");
        }
    }
}
