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

        static WorldSnapshot WithStation(bool ready, params BillView[] bills)
        {
            WorldSnapshot snapshot = Frame.Write();
            int first = snapshot.BillCount;
            foreach (BillView bill in bills) snapshot.AddBill(bill);
            snapshot.AddStation(new StationView(AtIndex, EdificeHandle.Galley, ready, 0, 0, false, false, first, bills.Length));
            return snapshot;
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
            Assert.That(model.Status, Is.EqualTo(Registry.Label("ui.bill.none")));
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
            Assert.That(forever.Target, Is.Empty, "a bill that never stops counts to nothing");
            Assert.That(forever.State, Is.EqualTo(Registry.Label("ui.bill.suspended")));
            Assert.That(until.IsFirst && forever.IsLast, Is.True);
        }

        [Test]
        public void ADarkGalleySaysSo()
        {
            var model = new BillsModel();
            model.Refresh(WithStation(false, Until(10, 0)), At, AtIndex, EdificeHandle.Galley);
            Assert.That(model.Ready, Is.False);
            Assert.That(model.Status, Is.EqualTo(Registry.Label("ui.bill.unpowered")));
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
    }
}
