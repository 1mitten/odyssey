#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The trade window's model (design 65 §6; the owner's mockups 28a Sell and 28b Buy): it opens
    /// itself once per ready session, each mode lists only its side's stock under the game's category
    /// headings, the deal spans both modes, figures clamp, the foot totals the owner's sample deal,
    /// Confirm's three reasons, and exactly what Confirm and Cancel send.
    /// </summary>
    public class TradeModelTests
    {
        static readonly PawnId Guest = new PawnId(9), Ada = new PawnId(1);

        // The storage categories: Food 0, Medicine 1, Materials 2, Books 3, Items 4, Weapons 5.
        static int CategoryOf(int item) => item switch
        {
            ItemHandle.Meal or ItemHandle.Carrots or ItemHandle.Berries or ItemHandle.Mushrooms
                or ItemHandle.CookedMeal or ItemHandle.VegetableMeal or ItemHandle.BurntMeal => 0,
            ItemHandle.MedicalSupplies => 1,
            ItemHandle.Bat or ItemHandle.Crowbar or ItemHandle.Machete or ItemHandle.ArcBlade or ItemHandle.Pistol => 5,
            ItemHandle.Gold => 4,
            _ => 2,
        };

        /// <summary>The brief's table: the colony's stock and the trader's, with the prices the simulation makes.</summary>
        static WorldSnapshot Frame(int session = 1, bool ready = true, int colonyGold = 312, int purse = 640, bool present = true,
            int generation = 1)
        {
            WorldSnapshot frame = global::Odyssey.Tests.Hud.Frame.Write(layers: 4);
            frame.Generation = generation;
            frame.AddPawn(new PawnView(Ada, new CellRef(4, 4, 1), 800, 800, 700, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Guest, new CellRef(5, 4, 1), 800, 800, 700, kind: PawnKindLabels.Trader,
                flags: PawnFlags.Person | PawnFlags.Visitor));
            if (!present) return frame;
            frame.AddTrade(new TradeView(1, Guest, Ada, ready, session, purse, colonyGold, 14 * Calendar.TicksPerHour, false));
            if (!ready) return frame;
            void Row(int item, int colony, int trader, int buy, int sell, byte cq = 0, byte tq = 0) =>
                frame.AddTradeRow(new TradeRowView(1, item, colony, trader, buy, sell, cq, tq));
            Row(ItemHandle.Carrots, 90, 0, 1, 2);
            Row(ItemHandle.Berries, 25, 0, 1, 2);
            Row(ItemHandle.Meal, 12, 30, 3, 9);
            Row(ItemHandle.MedicalSupplies, 6, 8, 12, 28);
            Row(ItemHandle.Wood, 240, 0, 1, 2);
            Row(ItemHandle.Stone, 180, 0, 1, 2);
            Row(ItemHandle.IronOre, 0, 60, 1, 3);
            Row(ItemHandle.Salvage, 0, 40, 1, 5);
            Row(ItemHandle.Bat, 2, 0, 15, 35, cq: QualityHandle.Normal);
            Row(ItemHandle.Crowbar, 1, 1, 18, 42, cq: QualityHandle.Poor, tq: QualityHandle.Normal);
            Row(ItemHandle.Pistol, 0, 2, 90, 210, tq: QualityHandle.Normal);
            return frame;
        }

        static TradeModel Opened(WorldSnapshot? frame = null)
        {
            var model = new TradeModel();
            Assert.That(model.Refresh(frame ?? Frame(), CategoryOf), Is.True);
            return model;
        }

        static IEnumerable<int> Items(TradeModel model) => model.Lines.Where(l => !l.IsHeading).Select(l => l.Item);

        static void SampleDeal(TradeModel model)
        {
            model.SetMode(TradeMode.Sell);
            for (int i = 0; i < 12; i++) model.Step(ItemHandle.Wood, +1, shift: true);
            for (int i = 0; i < 4; i++) model.Step(ItemHandle.Carrots, +1, shift: true);
            model.SetMode(TradeMode.Buy);
            for (int i = 0; i < 3; i++) model.Step(ItemHandle.MedicalSupplies, +1, shift: false);
            model.Step(ItemHandle.Pistol, +1, shift: false);
        }

        // ---- opening ------------------------------------------------------------------------------

        [Test]
        public void AReadySessionOpensTheWindowOnSellWithNothingMoving()
        {
            TradeModel model = Opened();
            Assert.That(model.Showing, Is.True);
            Assert.That(model.Mode, Is.EqualTo(TradeMode.Sell));
            Assert.That(model.SellCount + model.BuyCount, Is.Zero);
            Assert.That(model.Trader, Is.EqualTo(Guest));
        }

        [Test]
        public void NoWindowUntilTheSessionIsReady()
        {
            var model = new TradeModel();
            Assert.That(model.Refresh(Frame(ready: false), CategoryOf), Is.False);
            Assert.That(model.Showing, Is.False);
        }

        [Test]
        public void TheWindowOpensOncePerSession()
        {
            TradeModel model = Opened();
            var sent = new List<Intent>();
            model.Cancel(sent);
            Assert.That(model.Refresh(Frame(session: 1), CategoryOf), Is.False, "the same session never reopens");
            Assert.That(model.Refresh(Frame(session: 2), CategoryOf), Is.True, "the next one does");
        }

        [Test]
        public void TheWindowClosesWhenTheSessionEnds()
        {
            TradeModel model = Opened();
            Assert.That(model.Refresh(Frame(present: false), CategoryOf), Is.False);
            Assert.That(model.Showing, Is.False);
        }

        // ---- the two modes ------------------------------------------------------------------------

        [Test]
        public void SellListsOnlyTheColonysStockUnderItsHeadings()
        {
            TradeModel model = Opened();
            Assert.That(Items(model), Is.EquivalentTo(new[]
            {
                ItemHandle.Carrots, ItemHandle.Berries, ItemHandle.Meal, ItemHandle.MedicalSupplies,
                ItemHandle.Wood, ItemHandle.Stone, ItemHandle.Bat, ItemHandle.Crowbar,
            }));
            Assert.That(model.Lines.Where(l => l.IsHeading).Select(l => l.Category), Is.EqualTo(new[] { 0, 1, 2, 5 }),
                "Food, Medicine, Materials, Weapons, in the game's order; the empty Books and Items never show");
            Assert.That(Items(model), Has.No.Member(ItemHandle.Gold));
            Assert.That(model.Lines.Single(l => l.Item == ItemHandle.Wood && !l.IsHeading).PriceText, Is.EqualTo("1"),
                "Sell's price is what the trader pays");
        }

        [Test]
        public void BuyListsOnlyTheTradersStock()
        {
            TradeModel model = Opened();
            model.SetMode(TradeMode.Buy);
            Assert.That(Items(model), Is.EquivalentTo(new[]
            {
                ItemHandle.Meal, ItemHandle.MedicalSupplies, ItemHandle.IronOre, ItemHandle.Salvage,
                ItemHandle.Crowbar, ItemHandle.Pistol,
            }));
            TradeLine med = model.Lines.Single(l => l.Item == ItemHandle.MedicalSupplies && !l.IsHeading);
            Assert.That((med.InStockText, med.PriceText), Is.EqualTo(("8", "28")), "Buy's stock is the trader's and its price what one costs");
        }

        [Test]
        public void TheDealSurvivesSwitchingModes()
        {
            TradeModel model = Opened();
            model.Step(ItemHandle.Wood, +1, shift: true);
            model.ToggleMode();
            model.Step(ItemHandle.Pistol, +1, shift: false);
            model.ToggleMode();
            Assert.That(model.QuantityOf(TradeMode.Sell, ItemHandle.Wood), Is.EqualTo(10));
            Assert.That(model.QuantityOf(TradeMode.Buy, ItemHandle.Pistol), Is.EqualTo(1));
            Assert.That((model.SellCount, model.BuyCount), Is.EqualTo((1, 1)));
        }

        [Test]
        public void QualityIsItsOwnColumnAndTheNameStaysBare()
        {
            TradeModel model = Opened();
            TradeLine crowbar = model.Lines.Single(l => l.Item == ItemHandle.Crowbar && !l.IsHeading);
            Assert.That(crowbar.Name, Is.EqualTo(ItemLabels.Label(ItemHandle.Crowbar)), "the name only, never \"Crowbar (Poor)\"");
            Assert.That(crowbar.Quality, Is.EqualTo(QualityHandle.Poor));
            Assert.That(crowbar.QualityLabel, Is.EqualTo(Registry.Label("ui.quality.poor")));
            Assert.That(model.Lines.Single(l => l.Item == ItemHandle.Wood && !l.IsHeading).QualityLabel, Is.Empty);
        }

        // ---- the quantity control -----------------------------------------------------------------

        [Test]
        public void TheFigureClampsBetweenNoughtAndInStock()
        {
            TradeModel model = Opened();
            model.Step(ItemHandle.Crowbar, -1, shift: false);
            Assert.That(model.QuantityOf(TradeMode.Sell, ItemHandle.Crowbar), Is.Zero);
            model.Step(ItemHandle.Crowbar, +1, shift: true);
            Assert.That(model.QuantityOf(TradeMode.Sell, ItemHandle.Crowbar), Is.EqualTo(1), "one crowbar held, ten asked for");
            TradeLine line = model.Lines.Single(l => l.Item == ItemHandle.Crowbar && !l.IsHeading);
            Assert.That(line.CanMore, Is.False, "plus is off at in stock");
            Assert.That(line.CanLess, Is.True);
            Assert.That(line.AllMoving, Is.True);
        }

        [Test]
        public void ShiftMovesTenAndAllMovesEverything()
        {
            TradeModel model = Opened();
            model.Step(ItemHandle.Wood, +1, shift: true);
            Assert.That(model.QuantityOf(TradeMode.Sell, ItemHandle.Wood), Is.EqualTo(10));
            model.All(ItemHandle.Stone);
            Assert.That(model.QuantityOf(TradeMode.Sell, ItemHandle.Stone), Is.EqualTo(180));
            TradeLine idle = model.Lines.Single(l => l.Item == ItemHandle.Berries && !l.IsHeading);
            Assert.That((idle.Moving, idle.CanLess, idle.TotalText), Is.EqualTo((false, false, string.Empty)),
                "an idle row: minus off and no total");
        }

        [Test]
        public void ResetClearsBothModes()
        {
            TradeModel model = Opened();
            SampleDeal(model);
            model.Reset();
            Assert.That(model.SellCount + model.BuyCount, Is.Zero);
            Assert.That(model.Balance, Is.Zero);
        }

        // ---- the foot -----------------------------------------------------------------------------

        [Test]
        public void TheOwnersSampleDealTotalsOneThirtyFourToPay()
        {
            TradeModel model = Opened();
            SampleDeal(model);
            Assert.That(model.SellingFor, Is.EqualTo(160), "120 wood and 40 carrots at 1");
            Assert.That(model.BuyingFor, Is.EqualTo(294), "3 medical supplies at 28 and a pistol at 210");
            Assert.That(model.Balance, Is.EqualTo(134));
            Assert.That(model.BalanceLabelKey, Is.EqualTo(TradeModel.YouPayKey));
            Assert.That(model.BalanceText, Is.EqualTo("134"));
            Assert.That(model.CanConfirm, Is.True);
            Assert.That(model.Lines.Single(l => l.Item == ItemHandle.Pistol && !l.IsHeading).TotalText, Is.EqualTo("210"));
        }

        [Test]
        public void NothingMovingReadsBalanceNoughtAndConfirmIsOff()
        {
            TradeModel model = Opened();
            Assert.That(model.BalanceLabelKey, Is.EqualTo(TradeModel.BalanceKey));
            Assert.That(model.CanConfirm, Is.False);
            Assert.That(model.DisabledReasonKey, Is.EqualTo(TradeModel.NothingKey));
        }

        [Test]
        public void TheColonyCannotPay()
        {
            TradeModel model = Opened();
            SampleDeal(model);
            model.Step(ItemHandle.Pistol, +1, shift: false);
            Assert.That(model.Balance, Is.EqualTo(344));
            Assert.That(model.ColonyShort, Is.True);
            Assert.That(model.DisabledReasonKey, Is.EqualTo(TradeModel.NoGoldKey));
        }

        [Test]
        public void TheTraderCannotPay()
        {
            TradeModel model = Opened(Frame(purse: 300));
            model.All(ItemHandle.Wood);
            model.All(ItemHandle.Stone);
            model.All(ItemHandle.Bat);
            Assert.That(model.SellingFor, Is.EqualTo(450));
            Assert.That(model.BalanceLabelKey, Is.EqualTo(TradeModel.YouReceiveKey));
            Assert.That(model.BalanceText, Is.EqualTo("450"));
            Assert.That(model.TraderShort, Is.True);
            Assert.That(model.DisabledReasonKey, Is.EqualTo(TradeModel.NoPurseKey));
        }

        [Test]
        public void TheHeaderSaysHowLongAndWho()
        {
            TradeModel model = Opened();
            Assert.That(model.MetaHours, Is.EqualTo("14 h"));
            Assert.That(model.MetaLead, Does.StartWith(Registry.Label(TradeModel.TraderKindKey)));
            Assert.That(model.MetaTail, Does.EndWith(Registry.Label(TradeModel.NegotiatingKey)));
        }

        // ---- what the buttons send ------------------------------------------------------------------

        [Test]
        public void ConfirmSendsEveryRowThenTheCommitAndWaitsForTheAnswer()
        {
            WorldSnapshot frame = Frame();
            TradeModel model = Opened(frame);
            SampleDeal(model);
            var sent = new List<Intent>();
            Assert.That(model.Confirm(sent, frame), Is.True);

            var lines = sent.Where(i => i.Kind == IntentKind.TradeLine).Select(i => (i.B, i.C)).ToList();
            Assert.That(lines, Is.EquivalentTo(new[]
            {
                (ItemHandle.Wood, -120), (ItemHandle.Carrots, -40), (ItemHandle.MedicalSupplies, 3), (ItemHandle.Pistol, 1),
            }));
            Assert.That(sent.All(i => i.A == Guest.Value), Is.True);
            Assert.That(sent[sent.Count - 1].Kind, Is.EqualTo(IntentKind.TradeCommit));
            Assert.That(sent[sent.Count - 1].B, Is.EqualTo(134));
            Assert.That(sent.Any(i => i.Kind == IntentKind.TradeCancel), Is.False, "the simulation ends an applied session itself");

            // Up, with the ledger, and Confirm off with nothing to say until the answer comes.
            Assert.That(model.Showing, Is.True);
            Assert.That(model.AwaitingAnswer, Is.True);
            Assert.That(model.CanConfirm, Is.False);
            Assert.That(model.DisabledReasonKey, Is.Empty);
            Assert.That(model.QuantityOf(TradeMode.Sell, ItemHandle.Wood), Is.EqualTo(120));

            // A second press while waiting sends nothing.
            var again = new List<Intent>();
            Assert.That(model.Confirm(again, frame), Is.False);
            Assert.That(again, Is.Empty);
        }

        [Test]
        public void ConfirmSendsNothingWhileItIsOff()
        {
            WorldSnapshot frame = Frame();
            TradeModel model = Opened(frame);
            var sent = new List<Intent>();
            Assert.That(model.Confirm(sent, frame), Is.False);
            Assert.That(sent, Is.Empty);
            Assert.That(model.Showing, Is.True);
        }

        /// <summary>The same frame again is no answer: a publish that predates the press cannot carry the verdict.</summary>
        [Test]
        public void TheFrameConfirmWasPressedOnIsNotTheAnswer()
        {
            WorldSnapshot frame = Frame();
            TradeModel model = Opened(frame);
            SampleDeal(model);
            Assert.That(model.Confirm(new List<Intent>(), frame), Is.True);
            Assert.That(model.Refresh(frame, CategoryOf), Is.True);
            Assert.That(model.AwaitingAnswer, Is.True);
            Assert.That(model.Refused, Is.False);
        }

        /// <summary>An applied deal ends the session (design 65 §12): the next frame has no ready session, and the window closes.</summary>
        [Test]
        public void AnAppliedDealClosesTheWindow()
        {
            WorldSnapshot frame = Frame();
            TradeModel model = Opened(frame);
            SampleDeal(model);
            Assert.That(model.Confirm(new List<Intent>(), frame), Is.True);
            Assert.That(model.Refresh(Frame(present: false, generation: 2), CategoryOf), Is.False);
            Assert.That(model.Showing, Is.False);
        }

        /// <summary>
        /// A refused deal leaves the session open (design 65 §12): the first frame after the press that
        /// still shows it ready is the refusal. The ledger stays as it was and Confirm is off with the
        /// reason, until a quantity changes — a changed ledger is a new deal.
        /// </summary>
        [Test]
        public void ARefusedDealSaysSoAndKeepsTheLedger()
        {
            WorldSnapshot frame = Frame();
            TradeModel model = Opened(frame);
            SampleDeal(model);
            Assert.That(model.Confirm(new List<Intent>(), frame), Is.True);

            Assert.That(model.Refresh(Frame(generation: 2), CategoryOf), Is.True);
            Assert.That(model.Showing, Is.True);
            Assert.That(model.AwaitingAnswer, Is.False);
            Assert.That(model.Refused, Is.True);
            Assert.That(model.DisabledReasonKey, Is.EqualTo(TradeModel.RefusedKey));
            Assert.That(model.CanConfirm, Is.False);
            Assert.That(model.QuantityOf(TradeMode.Sell, ItemHandle.Wood), Is.EqualTo(120), "the ledger is kept");
            Assert.That(model.QuantityOf(TradeMode.Buy, ItemHandle.Pistol), Is.EqualTo(1));

            model.SetMode(TradeMode.Sell);
            model.Step(ItemHandle.Wood, -1, shift: true);
            Assert.That(model.Refused, Is.False, "a changed ledger is a new deal");
            Assert.That(model.CanConfirm, Is.True);

            // Reset clears it too.
            Assert.That(model.Confirm(new List<Intent>(), Frame(generation: 2)), Is.True);
            Assert.That(model.Refresh(Frame(generation: 3), CategoryOf), Is.True);
            Assert.That(model.Refused, Is.True);
            model.Reset();
            Assert.That(model.Refused, Is.False);
        }

        /// <summary>
        /// A new colony (design 65 §12): the window forgets which session it opened for, or a loaded
        /// colony whose first visit is also visit 1, session 1 would never see its window.
        /// </summary>
        [Test]
        public void ANewColonyForgetsTheSessionThisOneOpenedFor()
        {
            TradeModel model = Opened();
            model.Cancel(new List<Intent>());
            Assert.That(model.Refresh(Frame(), CategoryOf), Is.False, "the control: the same session never reopens");

            model.Forget();
            Assert.That(model.Refresh(Frame(), CategoryOf), Is.True, "another colony's visit 1, session 1 opens");
            Assert.That(model.Showing, Is.True);
        }

        [Test]
        public void CancelEndsTheSession()
        {
            TradeModel model = Opened();
            var sent = new List<Intent>();
            model.Cancel(sent);
            Assert.That(sent.Single().Kind, Is.EqualTo(IntentKind.TradeCancel));
            Assert.That(model.Showing, Is.False);
        }

        // ---- paging and the layout ------------------------------------------------------------------

        [Test]
        public void AShortScreenPagesTheList()
        {
            var model = new TradeModel { RowsPerPage = 5 };
            model.Refresh(Frame(), CategoryOf);
            Assert.That(model.PageCount, Is.EqualTo(3), "8 items and 4 headings, five a page");
            Assert.That(model.Lines, Has.Count.EqualTo(5));
            model.NextPage(+1);
            Assert.That(model.Page, Is.EqualTo(1));
        }

        [Test]
        public void TheColumnsFillTheWindowExactly()
        {
            int sum = TradeLayout.ColIcon + TradeLayout.ColItem + TradeLayout.ColQuality + TradeLayout.ColInStock
                + TradeLayout.ColPrice + TradeLayout.ColQuantity + TradeLayout.ColTotal + 6 * TradeLayout.ColumnGap;
            Assert.That(sum + 2 * TradeLayout.SidePad, Is.EqualTo(TradeLayout.Width));
            Assert.That(TradeLayout.ColItem, Is.GreaterThan(150), "room for Medical supplies at 14/500");
            Assert.That(TradeLayout.QtyFigure, Is.GreaterThanOrEqualTo(40), "room for a three-figure count");
        }

        /// <summary>The owner's worst case: 18 items and 4 headings, plus the chrome, fits 1080 without a scrollbar.</summary>
        [Test]
        public void TheWorstCaseFitsTenEightyUnpaged()
        {
            int list = 18 * TradeLayout.RowHeight + 4 * TradeLayout.CategoryRowHeight;
            Assert.That(list + TradeLayout.ChromeHeight, Is.LessThan(1080));
        }

        [Test]
        public void EveryWordTheWindowWritesIsInTheRegistry()
        {
            foreach (string key in TradeModel.IconKeys)
                Assert.That(Registry.Label(key), Is.Not.EqualTo(key), key + " is not in the registry");
        }
    }
}
