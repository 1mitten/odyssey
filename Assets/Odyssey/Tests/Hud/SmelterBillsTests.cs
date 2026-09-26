#nullable enable
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The smelter's bill list (design 62 §9): the shared control with a second station row — two
    /// recipes, a hopper, no power — and the smelter on the Build palette with steel beside it.
    /// </summary>
    public class SmelterBillsTests
    {
        static readonly CellRef At = new CellRef(3, 4, 1);
        static readonly int AtIndex = new GridSize(10, 10, 4).Index(At);

        static WorldSnapshot WithSmelter(bool ready, short ore, short fuel, params BillView[] bills)
        {
            WorldSnapshot snapshot = Frame.Write();
            int first = snapshot.BillCount;
            foreach (BillView bill in bills) snapshot.AddBill(bill);
            snapshot.AddStation(new StationView(AtIndex, EdificeHandle.Smelter, ready, 0, 0, false, false, first,
                bills.Length, ore, fuel));
            return snapshot;
        }

        static BillView Bill(int recipe, int count = 0) =>
            new BillView(recipe, BillModeHandle.UntilYouHave, 10, 0, count, false, false);

        [Test]
        public void TheSmelterIsAStationThatBurnsAndNeedsNoPower()
        {
            Assert.That(BillsModel.IsStation(EdificeHandle.Smelter), Is.True);
            Assert.That(BillsModel.StationNeedsPower(EdificeHandle.Smelter), Is.False);
            var model = new BillsModel();
            model.Refresh(Frame.Write(), At, AtIndex, EdificeHandle.Smelter);
            Assert.That(model.Showing, Is.True);
            Assert.That(model.Burns, Is.True);
            Assert.That(model.CanChangeRecipe, Is.True, "it makes two things");
            Assert.That(model.Needs, Is.EqualTo(Registry.Label("ui.bill.needs.smelter")));
            Assert.That(model.HasProblem, Is.False, "a smelter nobody has used says nothing is wrong");
        }

        /// <summary>No menu: the first press adds iron, the second copper, the third iron again.</summary>
        [Test]
        public void AddingGoesRoundTheRecipesTheListHoldsFewestOf()
        {
            var model = new BillsModel();
            model.Refresh(Frame.Write(), At, AtIndex, EdificeHandle.Smelter);
            Assert.That(model.PressAdd(out Intent first), Is.True);
            Assert.That((first.A, first.B), Is.EqualTo((BillEdit.Add, RecipeHandle.SmeltIron)));

            model.Refresh(WithSmelter(true, 3, 5, Bill(RecipeHandle.SmeltIron)), At, AtIndex, EdificeHandle.Smelter);
            model.PressAdd(out Intent second);
            Assert.That(second.B, Is.EqualTo(RecipeHandle.SmeltCopper));

            model.Refresh(WithSmelter(true, 3, 5, Bill(RecipeHandle.SmeltIron), Bill(RecipeHandle.SmeltCopper)),
                At, AtIndex, EdificeHandle.Smelter);
            model.PressAdd(out Intent third);
            Assert.That(third.B, Is.EqualTo(RecipeHandle.SmeltIron));
        }

        [Test]
        public void PressingARowsNameGoesRoundWhatItMakes()
        {
            var model = new BillsModel();
            model.Refresh(WithSmelter(true, 3, 5, Bill(RecipeHandle.SmeltIron)), At, AtIndex, EdificeHandle.Smelter);
            BillRow row = model.Rows[0];
            Assert.That(row.Recipe, Is.EqualTo(Registry.Label("ui.recipe.smelt.iron")));
            Assert.That(row.ProductCategory, Is.EqualTo((int)ItemCategory.Materials));
            Assert.That(model.PressRecipe(row, out Intent next), Is.True);
            Assert.That((next.A, next.B, next.C), Is.EqualTo((BillEdit.SetRecipe, 0, RecipeHandle.SmeltCopper)));

            model.Refresh(WithSmelter(true, 3, 5, Bill(RecipeHandle.SmeltCopper)), At, AtIndex, EdificeHandle.Smelter);
            model.PressRecipe(model.Rows[0], out Intent back);
            Assert.That(back.C, Is.EqualTo(RecipeHandle.SmeltIron), "and round again");

            // A cooker makes one thing, so its name is not a button.
            var cooker = new BillsModel();
            WorldSnapshot frame = Frame.Write();
            int first = frame.BillCount;
            frame.AddBill(new BillView(RecipeHandle.Meal, BillModeHandle.UntilYouHave, 10, 0, 0, false, false));
            frame.AddStation(new StationView(AtIndex, EdificeHandle.Campfire, true, 0, 0, false, false, first, 1, 5));
            cooker.Refresh(frame, At, AtIndex, EdificeHandle.Campfire);
            Assert.That(cooker.CanChangeRecipe, Is.False);
            Assert.That(cooker.PressRecipe(cooker.Rows[0], out _), Is.False);
        }

        [Test]
        public void TheSupplyLineCarriesTheOreAndTheFuel()
        {
            var model = new BillsModel();
            model.Refresh(WithSmelter(true, 3, 12, Bill(RecipeHandle.SmeltIron)), At, AtIndex, EdificeHandle.Smelter);
            Assert.That(model.Supply, Does.StartWith(Registry.Label("ui.bill.supply.ore").Replace("{n}", "3")));
            Assert.That(model.Supply, Does.EndWith(Registry.Label("ui.bill.fuel").Replace("{n}", "12")));
            Assert.That(model.NoSupply, Is.False);

            model.Refresh(WithSmelter(true, 0, 12, Bill(RecipeHandle.SmeltIron)), At, AtIndex, EdificeHandle.Smelter);
            Assert.That(model.Supply, Does.StartWith(Registry.Label("ui.bill.noore")));
            Assert.That(model.NoSupply, Is.True);

            model.Refresh(WithSmelter(true, 3, 0, Bill(RecipeHandle.SmeltIron)), At, AtIndex, EdificeHandle.Smelter);
            Assert.That(model.Supply, Does.EndWith(Registry.Label("ui.bill.hopperempty")));
            Assert.That(model.NoSupply, Is.True, "an empty hopper is as much a warning as no ore");
            Assert.That(model.SupplyHint, Does.Contain("coal"));
        }

        /// <summary>The hopper stands where a cooker's power does: the strip says <i>No fuel</i>, and the rows wait for it.</summary>
        [Test]
        public void AHopperThatCannotPayForABatchSaysSo()
        {
            var model = new BillsModel();
            model.Refresh(WithSmelter(false, 3, 0, Bill(RecipeHandle.SmeltIron)), At, AtIndex, EdificeHandle.Smelter);
            Assert.That(model.HasProblem, Is.True);
            Assert.That(model.ProblemLabel, Is.EqualTo(Registry.Label("ui.bill.nofuel")));
            Assert.That(model.HasSwitch, Is.False, "and no switch is offered: there is nothing to switch on");
            Assert.That(model.Rows[0].State, Is.EqualTo(Registry.Label("ui.bill.waitingfuel")));
            Assert.That(model.Rows[0].Tone, Is.EqualTo(BillTone.Warn));

            model.Refresh(WithSmelter(true, 3, 4, Bill(RecipeHandle.SmeltIron)), At, AtIndex, EdificeHandle.Smelter);
            Assert.That(model.HasProblem, Is.False);
            Assert.That(model.Rows[0].State, Is.Empty);
        }

        [Test]
        public void TheSmelterIsOnThePaletteAndSteelIsAMaterial()
        {
            Assert.That(PaletteTools.TryGet(PaletteTools.Smelter, out PaletteTool tool), Is.True, "the chip is live");
            Assert.That(tool.WantsMaterial, Is.True);
            Assert.That(PaletteTools.Materials, Does.Contain(StuffHandle.Steel));
            Assert.That(BuildLabels.StuffKey(StuffHandle.Steel), Is.EqualTo("ui.res.steel"));
            Assert.That(HudTheme.MaterialTintOf(StuffHandle.Steel), Is.Not.Null, "steel arrives coloured");
            Assert.That(BuildLabels.BuildingKey(BuildingHandle.Smelter), Is.EqualTo(PaletteTools.Smelter));
            Assert.That(EdificeLabels.IconKey(EdificeHandle.Smelter), Is.EqualTo(PaletteTools.Smelter));
            Assert.That(BuildShapes.CellsOf(BuildingHandle.Smelter), Is.EqualTo(1));
            Assert.That(BuildShapes.CanRotate(BuildingHandle.Smelter), Is.True);
            Assert.That(ItemLabels.IconKey(ItemHandle.IronBar), Is.EqualTo("ui.res.ironbar"));
            Assert.That(ItemLabels.IconKey(ItemHandle.CopperBar), Is.EqualTo("ui.res.copperbar"));
            Assert.That(JobLabels.IconKey(JobHandle.Craft), Is.EqualTo("ui.status.crafting"));
        }
    }
}
