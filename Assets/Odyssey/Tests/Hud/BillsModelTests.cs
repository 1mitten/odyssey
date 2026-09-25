#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>The bill list on a cooking station's pane (design 48 §5): rows from the frame, commands from the buttons.</summary>
    public class BillsModelTests
    {
        static readonly CellRef At = new CellRef(3, 4, 1);
        static readonly int AtIndex = new GridSize(10, 10, 4).Index(At);

        static WorldSnapshot WithStation(bool ready, params BillView[] bills) => WithStation(ready, 12, bills);

        static WorldSnapshot WithStation(bool ready, short rawMeals, params BillView[] bills)
        {
            WorldSnapshot snapshot = Frame.Write();
            int first = snapshot.BillCount;
            foreach (BillView bill in bills) snapshot.AddBill(bill);
            snapshot.AddStation(new StationView(AtIndex, EdificeHandle.Galley, ready, 0, 0, false, false, first,
                bills.Length, rawMeals));
            return snapshot;
        }

        static WorldSnapshot WithGalley(bool ready, bool switchOn, params BillView[] bills)
        {
            WorldSnapshot snapshot = WithStation(ready, bills);
            snapshot.AddPowerDevice(new PowerDeviceView(AtIndex, -1, BuildingHandle.Galley, PowerRole.Consumer,
                switchOn, ready, 7, 350, 0, 0, 0));
            return snapshot;
        }

        static BillView Paused(int target, int count) =>
            new BillView(RecipeHandle.Meal, BillModeHandle.UntilYouHave, target, 0, count, true, false);

        /// <summary>
        /// The pane says what a meal takes and whether there is any (owner, 2026-09-25: "I didn't
        /// know what ingredients I needed").
        /// </summary>
        [Test]
        public void ThePaneSaysWhatAMealTakesAndWhetherThereIsAny()
        {
            var model = new BillsModel();
            model.Refresh(WithStation(true, 12, Until(10, 0)), At, AtIndex, EdificeHandle.Galley);
            Assert.That(model.Needs, Is.EqualTo(Registry.Label("ui.bill.needs")));
            Assert.That(model.Needs, Does.Contain("carrots"));
            Assert.That(model.Supply, Does.Contain("12"));
            Assert.That(model.NoSupply, Is.False);

            model.Refresh(WithStation(true, 0, Until(10, 0)), At, AtIndex, EdificeHandle.Galley);
            Assert.That(model.Supply, Is.EqualTo(Registry.Label("ui.bill.nosupply")));
            Assert.That(model.NoSupply, Is.True);

            model.Refresh(Frame.Write(), At, AtIndex, EdificeHandle.Campfire);
            Assert.That(model.Needs, Does.Contain("wood"), "a campfire also burns a wood a meal");
            Assert.That(model.Supply, Is.Empty, "nothing is said about supply before the station is published");
        }

        static BillView Until(int target, int count, bool satisfied = false) =>
            new BillView(RecipeHandle.Meal, BillModeHandle.UntilYouHave, target, 0, count, false, satisfied);

        [Test]
        public void OnlyAGalleyOrACampfireShowsABillList()
        {
            var model = new BillsModel();
            model.Refresh(Frame.Write(), At, AtIndex, EdificeHandle.Heater);
            Assert.That(model.Showing, Is.False);
            model.Refresh(Frame.Write(), At, AtIndex, EdificeHandle.Campfire);
            Assert.That(model.Showing, Is.True, "a campfire cooks too");
            Assert.That(model.Rows, Is.Empty, "and one nobody has used has no bills");
            Assert.That(model.EmptyLabel, Is.EqualTo(Registry.Label("ui.bill.none")));
        }

        [Test]
        public void TheRowsSayWhatEachBillIsDoing()
        {
            var model = new BillsModel();
            model.Refresh(WithStation(true,
                    Until(10, 3),
                    new BillView(RecipeHandle.Meal, BillModeHandle.Times, 5, 5, 5, false, true),
                    new BillView(RecipeHandle.Meal, BillModeHandle.Forever, 10, 2, 0, true, false)),
                At, AtIndex, EdificeHandle.Galley);

            Assert.That(model.Rows.Count, Is.EqualTo(3));
            BillRow until = model.Rows[0], times = model.Rows[1], forever = model.Rows[2];
            Assert.That(until.Recipe, Is.EqualTo(Registry.Label("ui.recipe.meal")));
            Assert.That(until.ModeLabel, Is.EqualTo(Registry.Label("ui.bill.mode.until")));
            Assert.That(until.Progress, Is.EqualTo("3 / 10"));
            Assert.That(until.State, Is.Empty);
            Assert.That(times.State, Is.EqualTo(Registry.Label("ui.bill.done")), "its count is met");
            Assert.That(forever.Target, Is.EqualTo(BillsModel.NoFigure), "a bill that never stops counts to nothing");
            Assert.That(forever.State, Is.EqualTo(Registry.Label("ui.bill.suspended")));
            Assert.That(until.IsFirst && forever.IsLast, Is.True);
        }

        [Test]
        public void ADarkGalleySaysSo()
        {
            var model = new BillsModel();
            model.Refresh(WithStation(false, Until(10, 0)), At, AtIndex, EdificeHandle.Galley);
            Assert.That(model.Ready, Is.False);
            Assert.That(model.ProblemLabel, Is.EqualTo(Registry.Label("ui.bill.unpowered")));
        }

        [Test]
        public void EveryButtonNamesTheStationAndTheRow()
        {
            var model = new BillsModel();
            model.Refresh(WithStation(true, Until(10, 3), Until(4, 1)), At, AtIndex, EdificeHandle.Galley);
            BillRow second = model.Rows[1];

            Assert.That(model.PressAdd(out Intent add), Is.True);
            Assert.That(add.Kind, Is.EqualTo(IntentKind.EditBill));
            Assert.That(add.Cell, Is.EqualTo(At));
            Assert.That((add.A, add.B), Is.EqualTo((BillEdit.Add, RecipeHandle.Meal)));

            Intent mode = model.PressMode(second);
            Assert.That((mode.A, mode.B, mode.C), Is.EqualTo((BillEdit.SetMode, 1, BillModeHandle.Times)));

            Assert.That(model.PressNudge(second, +1, out Intent up), Is.True);
            Assert.That((up.A, up.B, up.C), Is.EqualTo((BillEdit.SetTarget, 1, 5)));
            Assert.That(model.PressNudge(second, -10, out Intent down), Is.True);
            Assert.That(down.C, Is.EqualTo(1), "never below one");

            Assert.That(model.PressUp(second, out Intent moveUp), Is.True);
            Assert.That((moveUp.A, moveUp.B), Is.EqualTo((BillEdit.MoveUp, 1)));
            Assert.That(model.PressDown(second, out _), Is.False, "the last cannot go further down");
            Assert.That(model.PressUp(model.Rows[0], out _), Is.False, "nor the first further up");

            Intent remove = model.PressRemove(second);
            Assert.That((remove.A, remove.B), Is.EqualTo((BillEdit.Remove, 1)));
            Intent pause = model.PressSuspend(second);
            Assert.That((pause.A, pause.B, pause.C), Is.EqualTo((BillEdit.SetSuspended, 1, 1)));
        }

        [Test]
        public void AFullListRefusesAnother()
        {
            var model = new BillsModel();
            model.Refresh(WithStation(true, Enumerable.Range(0, BillsModel.MaxRows).Select(_ => Until(10, 0)).ToArray()),
                At, AtIndex, EdificeHandle.Galley);
            Assert.That(model.Full, Is.True);
            Assert.That(model.PressAdd(out _), Is.False);
        }

        [Test]
        public void TheModeGoesRoundAndForeverHasNoTargetToNudge()
        {
            var model = new BillsModel();
            model.Refresh(WithStation(true, new BillView(RecipeHandle.Meal, BillModeHandle.Forever, 10, 0, 0, false, false)),
                At, AtIndex, EdificeHandle.Galley);
            BillRow row = model.Rows[0];
            Assert.That(model.PressMode(row).C, Is.EqualTo(BillModeHandle.UntilYouHave), "forever wraps round to the start");
            Assert.That(model.PressNudge(row, 1, out _), Is.False);
        }

        [Test]
        public void ThePaneIsLaidOutForTheFiveBillsAStationHolds()
        {
            // Kitchen.MaxBills is five, and Odyssey.Tests.Sim.CookingTests holds it there; the two
            // assemblies cannot see each other, so each side pins the same number.
            Assert.That(BillsModel.MaxRows, Is.EqualTo(5));
        }

        [Test]
        public void TheRecipeAndModeTablesAreTheContractsLength()
        {
            Assert.That(BillsModel.RecipeKeys.Length, Is.EqualTo(RecipeHandle.Count));
            Assert.That(BillsModel.ModeKeys.Length, Is.EqualTo(BillModeHandle.Count));
            foreach (string key in BillsModel.RecipeKeys.Concat(BillsModel.ModeKeys))
                Assert.That(Registry.Label(key), Is.Not.EqualTo(key), key + " is a name the registry knows");
        }
        // ---- design 49: the general bill list ------------------------------------------------------

        [Test]
        public void TheStatusLineSaysWhyInItsOwnInk()
        {
            var model = new BillsModel();
            model.Refresh(WithStation(true, Until(10, 3), Paused(10, 3), Until(4, 4, satisfied: true)),
                At, AtIndex, EdificeHandle.Galley);
            Assert.That((model.Rows[0].State, model.Rows[0].Tone), Is.EqualTo((string.Empty, BillTone.Meta)),
                "a bill being worked says nothing: there is no worker rule to name");
            Assert.That((model.Rows[1].State, model.Rows[1].Tone), Is.EqualTo((Registry.Label("ui.bill.suspended"), BillTone.Dim)));
            Assert.That((model.Rows[2].State, model.Rows[2].Tone), Is.EqualTo((Registry.Label("ui.bill.done"), BillTone.Meta)));

            model.Refresh(WithStation(false, Until(10, 3), Paused(10, 3)), At, AtIndex, EdificeHandle.Galley);
            foreach (BillRow row in model.Rows)
                Assert.That((row.State, row.Tone), Is.EqualTo((Registry.Label("ui.bill.waiting"), BillTone.Warn)),
                    "a dark cooker is waiting for power, and a paused bill there will be too once it is resumed");
        }

        [Test]
        public void ACampfireNeverWaitsForPower()
        {
            var model = new BillsModel();
            model.Refresh(WithStation(false, Until(10, 3)), At, AtIndex, EdificeHandle.Campfire);
            Assert.That(model.HasProblem, Is.False);
            Assert.That(model.Rows[0].Tone, Is.Not.EqualTo(BillTone.Warn));
        }

        [Test]
        public void TheStripIsUpOnlyWhileAStationThatNeedsPowerHasNone()
        {
            var model = new BillsModel();
            model.Refresh(WithGalley(true, true, Until(10, 3)), At, AtIndex, EdificeHandle.Galley);
            Assert.That(model.HasProblem, Is.False);
            Assert.That(model.ProblemLabel, Is.Empty);

            model.Refresh(WithGalley(false, true, Until(10, 3)), At, AtIndex, EdificeHandle.Galley);
            Assert.That(model.HasProblem, Is.True);
            Assert.That(model.ProblemLabel, Is.EqualTo(Registry.Label("ui.bill.unpowered")));
            Assert.That(model.SwitchLabel, Is.EqualTo(Registry.Label("ui.command.switchoff")),
                "switched on but on a dead net: the one thing the player can do here is switch it off");
            Assert.That(model.PressSwitch(out Intent off), Is.True);
            Assert.That((off.Kind, off.Cell, off.A), Is.EqualTo((IntentKind.SetPowerSwitch, At, 0)));

            model.Refresh(WithGalley(false, false, Until(10, 3)), At, AtIndex, EdificeHandle.Galley);
            Assert.That(model.SwitchLabel, Is.EqualTo(Registry.Label("ui.command.switchon")));
            Assert.That(model.PressSwitch(out Intent on), Is.True);
            Assert.That(on.A, Is.EqualTo(1));
        }

        [Test]
        public void TheStepperStopsAtOneAndForeverHasNothingToStep()
        {
            var model = new BillsModel();
            model.Refresh(WithStation(true,
                    Until(1, 0),
                    Until(12, 3),
                    new BillView(RecipeHandle.Meal, BillModeHandle.Forever, 10, 2, 0, false, false)),
                At, AtIndex, EdificeHandle.Galley);
            BillRow one = model.Rows[0], twelve = model.Rows[1], forever = model.Rows[2];

            Assert.That((one.CanLess, one.CanMore), Is.EqualTo((false, true)), "the minus is off at one");
            Assert.That(model.PressNudge(one, -1, out _), Is.False);
            Assert.That((twelve.CanLess, twelve.CanMore), Is.EqualTo((true, true)));
            Assert.That(twelve.Target, Is.EqualTo("12"));

            Assert.That((forever.CanLess, forever.CanMore), Is.EqualTo((false, false)));
            Assert.That((forever.Target, forever.Progress, forever.ProgressPerMille),
                Is.EqualTo((BillsModel.NoFigure, BillsModel.NoFigure, 0)), "dashes and an empty track");
        }

        [Test]
        public void TheTrackFillsToTheCountAndNoFurther()
        {
            var model = new BillsModel();
            model.Refresh(WithStation(true, Until(10, 3), Until(10, 25), Until(4, 0)),
                At, AtIndex, EdificeHandle.Galley);
            Assert.That(model.Rows.Select(r => r.ProgressPerMille), Is.EqualTo(new[] { 300, 1000, 0 }),
                "until-you-have counts the colony's meals, which can pass the target");
        }

        [Test]
        public void TheCountFitsItsColumnAtThreeDigits()
        {
            Assert.That(BillsModel.ProgressText(3, 10), Is.EqualTo("3 / 10"));
            Assert.That(BillsModel.ProgressText(10, 10), Is.EqualTo("10 / 10"));
            Assert.That(BillsModel.ProgressText(100, 120), Is.EqualTo("100/120"), "the spaces go before the column overflows");
            // 12 px IBM Plex Mono advances 0.6 em a character: seven is 50 px in a 64 px column.
            Assert.That(BillsModel.SpacedProgressMax * 12 * 0.6, Is.LessThanOrEqualTo(BillsLayout.ProgressColumn));
            Assert.That(BillsModel.ProgressText(100, 120).Length * 12 * 0.6, Is.LessThanOrEqualTo(BillsLayout.ProgressColumn));
        }

        [Test]
        public void TheHeadingCountsTheBillsAndAnEmptyListSaysSoInARow()
        {
            var model = new BillsModel();
            model.Refresh(WithStation(true, Until(10, 3), Until(4, 1)), At, AtIndex, EdificeHandle.Galley);
            Assert.That(model.CountLabel, Is.EqualTo("(2)"));
            Assert.That(model.EmptyLabel, Is.Empty);
            Assert.That(model.Rows.Select(r => r.Number), Is.EqualTo(new[] { "1", "2" }), "counted from one");

            model.Refresh(Frame.Write(), At, AtIndex, EdificeHandle.Galley);
            Assert.That(model.CountLabel, Is.EqualTo("(0)"));
            Assert.That(model.EmptyLabel, Is.EqualTo(Registry.Label("ui.bill.none")));
        }

        [Test]
        public void TheProductTileTakesItsCategorysHue()
        {
            var model = new BillsModel();
            model.Refresh(WithStation(true, Until(10, 3)), At, AtIndex, EdificeHandle.Campfire);
            Assert.That(model.Rows[0].ProductCategory, Is.EqualTo(0), "a meal is food");
            Assert.That(BillsModel.RecipeCategories.Length, Is.EqualTo(RecipeHandle.Count));
        }

        [Test]
        public void EveryStationIsOneRowAndAddsItsOwnRecipe()
        {
            // The composite's contract: what takes bills is a table, not a condition in the pane.
            Assert.That(BillsModel.Stations.Select(s => s.Edifice).Distinct().Count(), Is.EqualTo(BillsModel.Stations.Length));
            foreach (BillStation station in BillsModel.Stations)
            {
                Assert.That(BillsModel.IsStation(station.Edifice), Is.True);
                Assert.That((uint)station.DefaultRecipe, Is.LessThan((uint)RecipeHandle.Count));
                var model = new BillsModel();
                model.Refresh(Frame.Write(), At, AtIndex, station.Edifice);
                Assert.That(model.PressAdd(out Intent add), Is.True);
                Assert.That((add.A, add.B), Is.EqualTo((BillEdit.Add, station.DefaultRecipe)));
            }
            Assert.That(BillsModel.StationNeedsPower(EdificeHandle.Galley), Is.True);
            Assert.That(BillsModel.StationNeedsPower(EdificeHandle.Campfire), Is.False);
        }

        [Test]
        public void TheSignatureMovesWithEverythingTheListDraws()
        {
            var model = new BillsModel();
            model.Refresh(WithGalley(true, true, Until(10, 3)), At, AtIndex, EdificeHandle.Galley);
            int before = model.Signature();
            model.Refresh(WithGalley(true, true, Until(10, 3)), At, AtIndex, EdificeHandle.Galley);
            Assert.That(model.Signature(), Is.EqualTo(before), "the same frame draws nothing new");

            model.Refresh(WithGalley(true, true, Until(10, 4)), At, AtIndex, EdificeHandle.Galley);
            Assert.That(model.Signature(), Is.Not.EqualTo(before), "the count");
            model.Refresh(WithGalley(false, true, Until(10, 3)), At, AtIndex, EdificeHandle.Galley);
            Assert.That(model.Signature(), Is.Not.EqualTo(before), "the power");
            model.Refresh(WithGalley(true, false, Until(10, 3)), At, AtIndex, EdificeHandle.Galley);
            Assert.That(model.Signature(), Is.Not.EqualTo(before), "the switch");
        }
    }
}
