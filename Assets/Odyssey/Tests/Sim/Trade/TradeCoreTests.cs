#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Trade;

namespace Odyssey.Tests.Sim.Trade
{
    /// <summary>
    /// The trade itself (design 65 §3–§4, §6, T4): prices from one owner, a deal applied whole —
    /// sold goods out of the colony's stock, bought goods and gold owed set down beside the trader —
    /// and every way a deal is refused whole, with nothing moved.
    /// </summary>
    public class TradeCoreTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ColonyWorld Board()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 0;
            ColonyWorld colony = ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
            colony.World.Tick(30);
            return colony;
        }

        /// <summary>A trader in a ready session with the colony's one colonist, its stock and purse set by hand.</summary>
        static (Visit Visit, Pawn Trader) Session(ColonyWorld colony, int purse = 500)
        {
            Assert.That(TraderVisitTests.Fire(colony), Is.EqualTo(IntentRejection.None));
            Visit visit = colony.Pawns.Trade!.Visits.Single();
            System.Array.Clear(visit.Stock, 0, visit.Stock.Length);
            visit.Stock[ItemIndex.MedicalSupplies] = 5;
            visit.Stock[ItemIndex.Pistol] = 2;
            visit.Purse = purse;
            visit.Negotiator = colony.Pawns.Pawns.All.First(p => p.IsColonist).Id.Value;
            visit.Ready = true;
            visit.Session = 1;
            Pawn trader = colony.Pawns.Pawns.Get(new PawnId(visit.Pawn))!;
            return (visit, trader);
        }

        /// <summary>Put a stack loose beside the trader, where the colony can trade it from.</summary>
        static ColonyItem Beside(ColonyWorld colony, Pawn trader, int item, int count)
        {
            int cell = colony.Pawns.Items.NearestCellWithSpace(colony.Pawns.Cells, trader.Cell, item, count, 3);
            Assert.That(cell, Is.GreaterThanOrEqualTo(0));
            return colony.Pawns.Items.Get(colony.Pawns.Items.Spawn(item, cell, count))!;
        }

        static IntentRejection Deal(ColonyWorld colony, Pawn trader, int balance, params (int Item, int Count)[] lines)
        {
            colony.World.Intents.ClearRejected();
            foreach (var line in lines)
                colony.World.Intents.Submit(new Intent(IntentKind.TradeLine, default, trader.Id.Value, line.Item, line.Count));
            colony.World.Intents.Submit(new Intent(IntentKind.TradeCommit, default, trader.Id.Value, balance));
            colony.World.Tick();
            var commit = colony.World.Intents.Rejected.Where(r => r.Intent.Kind == IntentKind.TradeCommit).ToList();
            return commit.Count == 0 ? IntentRejection.None : commit[0].Reason;
        }

        static int Held(ColonyWorld colony, int item) =>
            colony.Pawns.Items.Items.Where(i => !i.Despawned && i.DefIndex == item && i.CarriedBy == 0).Sum(i => i.Stack);

        // ---- prices ---------------------------------------------------------------------------------

        [Test]
        public void ThePricesAreDesignFiftySevensFormula()
        {
            PawnContent content = ContentPack.Pawns();
            TraderKindDef kind = ContentPack.Incidents().Traders[0].Def;
            (int item, int buy, int sell)[] expected =
            {
                (ItemIndex.Wood, 1, 2), (ItemIndex.Meal, 3, 9), (ItemIndex.MedicalSupplies, 12, 28),
                (ItemIndex.Pistol, 90, 210), (ItemIndex.Bat, 15, 35), (ItemIndex.Salvage, 1, 5), (ItemIndex.IronOre, 1, 3),
            };
            foreach (var (item, buy, sell) in expected)
            {
                Assert.That(TradePricing.Buy(content.Items[item], kind), Is.EqualTo(buy), content.Items[item].defName + " buy");
                Assert.That(TradePricing.Sell(content.Items[item], kind), Is.EqualTo(sell), content.Items[item].defName + " sell");
            }
        }

        /// <summary>The trader always sells dearer than it buys, so buying and selling back always loses.</summary>
        [Test]
        public void NothingCanBeBoughtBackForLess()
        {
            PawnContent content = ContentPack.Pawns();
            TraderKindDef kind = ContentPack.Incidents().Traders[0].Def;
            foreach (ItemDef item in content.Items)
                Assert.That(TradePricing.Sell(item, kind), Is.GreaterThan(TradePricing.Buy(item, kind)), item.defName);
        }

        // ---- a deal -------------------------------------------------------------------------------

        [Test]
        public void ADealMovesGoodsAndGoldBothWays()
        {
            ColonyWorld colony = Board();
            var (visit, trader) = Session(colony);
            Beside(colony, trader, ItemIndex.Wood, 20);
            Beside(colony, trader, ItemIndex.Gold, 100);
            int purse = visit.Purse;

            // Sell 20 wood at 1 (+20), buy 1 medical supplies at 28 (-28): the colony pays 8.
            Assert.That(Deal(colony, trader, 8, (ItemIndex.Wood, -20), (ItemIndex.MedicalSupplies, 1)), Is.EqualTo(IntentRejection.None));

            Assert.That(Held(colony, ItemIndex.Wood), Is.Zero, "the wood went to the trader");
            Assert.That(visit.Stock[ItemIndex.Wood], Is.EqualTo(20));
            Assert.That(visit.Stock[ItemIndex.MedicalSupplies], Is.EqualTo(4));
            Assert.That(Held(colony, ItemIndex.MedicalSupplies), Is.EqualTo(1), "the medical supplies were set down");
            Assert.That(Held(colony, ItemIndex.Gold), Is.EqualTo(92));
            Assert.That(visit.Purse, Is.EqualTo(purse + 8));
        }

        /// <summary>
        /// The reason the trade radius exists (design 65 §4): what one deal pays out can be spent in the
        /// next before anybody hauls it. So a drop lands inside that radius, never merely near it.
        /// </summary>
        [Test]
        public void GoldOwedToTheColonyIsSetDownWhereTheNextDealCanSpendIt()
        {
            ColonyWorld colony = Board();
            var (visit, trader) = Session(colony);
            Beside(colony, trader, ItemIndex.Wood, 50);
            Assert.That(Deal(colony, trader, -50, (ItemIndex.Wood, -50)), Is.EqualTo(IntentRejection.None));
            Assert.That(Held(colony, ItemIndex.Gold), Is.EqualTo(50));
            ColonyItem gold = colony.Pawns.Items.Items.First(i => !i.Despawned && i.DefIndex == ItemIndex.Gold);
            CellRef at = Size.FromIndex(gold.Cell), t = Size.FromIndex(trader.Cell);
            Assert.That(System.Math.Max(System.Math.Abs(at.X - t.X), System.Math.Abs(at.Z - t.Z)),
                Is.LessThanOrEqualTo(ColonyTradeStock.TradeRadius));
            Assert.That(TradeDrops.MaxRadius, Is.LessThanOrEqualTo(ColonyTradeStock.TradeRadius),
                "a drop radius past the trade radius is gold the next deal cannot see");

            var counts = new int[colony.Pawns.Content.Items.Length];
            ColonyTradeStock.Count(colony.Pawns, trader.Cell, counts);
            Assert.That(counts[ItemIndex.Gold], Is.EqualTo(50), "the gold just paid counts towards the next deal");
        }

        /// <summary>
        /// An applied deal is the end of the session (design 65 §6, §12): the negotiator's job ends and
        /// the window closes on seeing no ready session. A refused one leaves it open, so the ledger
        /// stays up to be corrected rather than vanishing with nothing moved.
        /// </summary>
        [Test]
        public void AnAppliedDealEndsTheSessionAndARefusedOneDoesNot()
        {
            ColonyWorld colony = Board();
            var (visit, trader) = Session(colony, purse: 10);
            Beside(colony, trader, ItemIndex.Wood, 40);

            Assert.That(Deal(colony, trader, -40, (ItemIndex.Wood, -40)), Is.EqualTo(IntentRejection.NotPermitted), "the purse is 10");
            Assert.That(visit.InSession, Is.True, "refused: the session stays open");
            Assert.That(visit.Ready, Is.True);

            Assert.That(Deal(colony, trader, -10, (ItemIndex.Wood, -10)), Is.EqualTo(IntentRejection.None));
            Assert.That(visit.InSession, Is.False, "applied: the session is over");
            Assert.That(visit.Ready, Is.False);
        }

        [Test]
        public void GoldIsConservedAcrossADeal()
        {
            ColonyWorld colony = Board();
            var (visit, trader) = Session(colony);
            Beside(colony, trader, ItemIndex.Wood, 30);
            Beside(colony, trader, ItemIndex.Gold, 300);
            int before = Held(colony, ItemIndex.Gold) + visit.Purse;
            Assert.That(Deal(colony, trader, 180, (ItemIndex.Wood, -30), (ItemIndex.Pistol, 1)), Is.EqualTo(IntentRejection.None));
            Assert.That(Held(colony, ItemIndex.Gold) + visit.Purse, Is.EqualTo(before));
        }

        [Test]
        public void ABoughtWeaponIsNormal()
        {
            ColonyWorld colony = Board();
            var (visit, trader) = Session(colony);
            Beside(colony, trader, ItemIndex.Gold, 300);
            Assert.That(Deal(colony, trader, 210, (ItemIndex.Pistol, 1)), Is.EqualTo(IntentRejection.None));
            ColonyItem pistol = colony.Pawns.Items.Items.Last(i => !i.Despawned && i.DefIndex == ItemIndex.Pistol && i.CarriedBy == 0);
            Assert.That(pistol.Quality, Is.EqualTo(QualityHandle.Normal));
        }

        // ---- refused whole ------------------------------------------------------------------------

        static void AssertNothingMoved(ColonyWorld colony, Visit visit, int wood, int gold, int purse, int[] stock)
        {
            Assert.That(Held(colony, ItemIndex.Wood), Is.EqualTo(wood), "wood moved");
            Assert.That(Held(colony, ItemIndex.Gold), Is.EqualTo(gold), "gold moved");
            Assert.That(visit.Purse, Is.EqualTo(purse), "the purse moved");
            Assert.That(visit.Stock, Is.EqualTo(stock), "the stock moved");
        }

        [Test]
        public void ADealTheTraderCannotPayForIsRefusedWhole()
        {
            ColonyWorld colony = Board();
            var (visit, trader) = Session(colony, purse: 10);
            Beside(colony, trader, ItemIndex.Wood, 40);
            int[] stock = (int[])visit.Stock.Clone();
            Assert.That(Deal(colony, trader, -40, (ItemIndex.Wood, -40)), Is.EqualTo(IntentRejection.NotPermitted));
            AssertNothingMoved(colony, visit, 40, 0, 10, stock);
        }

        [Test]
        public void ADealTheColonyCannotPayForIsRefusedWhole()
        {
            ColonyWorld colony = Board();
            var (visit, trader) = Session(colony);
            Beside(colony, trader, ItemIndex.Gold, 100);
            int[] stock = (int[])visit.Stock.Clone();
            Assert.That(Deal(colony, trader, 210, (ItemIndex.Pistol, 1)), Is.EqualTo(IntentRejection.NotPermitted));
            AssertNothingMoved(colony, visit, 0, 100, 500, stock);
        }

        [Test]
        public void ABalanceThatIsNotTheLedgersIsRefused()
        {
            ColonyWorld colony = Board();
            var (visit, trader) = Session(colony);
            Beside(colony, trader, ItemIndex.Wood, 20);
            int[] stock = (int[])visit.Stock.Clone();
            Assert.That(Deal(colony, trader, -19, (ItemIndex.Wood, -20)), Is.EqualTo(IntentRejection.NotPermitted));
            AssertNothingMoved(colony, visit, 20, 0, 500, stock);
            Assert.That(Deal(colony, trader, -20, (ItemIndex.Wood, -20)), Is.EqualTo(IntentRejection.None), "the control");
        }

        [Test]
        public void MoreThanEitherSideHoldsIsRefused()
        {
            ColonyWorld colony = Board();
            var (visit, trader) = Session(colony);
            Beside(colony, trader, ItemIndex.Wood, 20);
            Beside(colony, trader, ItemIndex.Gold, 500);
            Assert.That(Deal(colony, trader, -21, (ItemIndex.Wood, -21)), Is.EqualTo(IntentRejection.NotPermitted), "more wood than the colony has");
            Assert.That(Deal(colony, trader, 168, (ItemIndex.MedicalSupplies, 6)), Is.EqualTo(IntentRejection.NotPermitted), "more supplies than the trader has");
        }

        [Test]
        public void NoDealWithoutAReadySession()
        {
            ColonyWorld colony = Board();
            var (visit, trader) = Session(colony);
            visit.Ready = false;
            Beside(colony, trader, ItemIndex.Wood, 20);
            Assert.That(Deal(colony, trader, -20, (ItemIndex.Wood, -20)), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(Held(colony, ItemIndex.Wood), Is.EqualTo(20));
        }

        [Test]
        public void GoldIsNeverARow()
        {
            ColonyWorld colony = Board();
            var (visit, trader) = Session(colony);
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(new Intent(IntentKind.TradeLine, default, trader.Id.Value, ItemIndex.Gold, 10));
            colony.World.Tick();
            Assert.That(colony.World.Intents.Rejected.Single().Reason, Is.EqualTo(IntentRejection.OutOfBounds));
        }

        /// <summary>A stack a hauler has claimed is not the colony's to sell: taking it would pull a load out of a pair of hands.</summary>
        [Test]
        public void AClaimedStackIsNotForSale()
        {
            ColonyWorld colony = Board();
            var (visit, trader) = Session(colony);
            ColonyItem wood = Beside(colony, trader, ItemIndex.Wood, 20);
            Pawn colonist = colony.Pawns.Pawns.All.First(p => p.IsColonist);
            colony.Pawns.Reservations.Reserve(colonist.Id, ReservationManager.Key(ReservationTargetKind.Item, wood.Id.Value));
            var counts = new int[colony.Pawns.Content.Items.Length];
            ColonyTradeStock.Count(colony.Pawns, trader.Cell, counts);
            Assert.That(counts[ItemIndex.Wood], Is.Zero);
        }

        [Test]
        public void ALooseStackFarFromTheTraderIsNotForSale()
        {
            ColonyWorld colony = Board();
            var (visit, trader) = Session(colony);
            CellRef t = Size.FromIndex(trader.Cell);
            int x = t.X > 30 ? t.X - 12 : t.X + 12;
            int far = colony.Pawns.Items.NearestCellWithSpace(colony.Pawns.Cells, colony.Pawns.Cells.NearestWalkableInColumn(x, t.Z, t.Y),
                ItemIndex.Wood, 20, 2);
            colony.Pawns.Items.Spawn(ItemIndex.Wood, far, 20);
            var counts = new int[colony.Pawns.Content.Items.Length];
            ColonyTradeStock.Count(colony.Pawns, trader.Cell, counts);
            Assert.That(counts[ItemIndex.Wood], Is.Zero, "loose wood twelve cells off is the colony's, but not here");
        }

        // ---- what the interface reads -------------------------------------------------------------

        [Test]
        public void AReadySessionPublishesItsRowsWithTheSimulationsPrices()
        {
            ColonyWorld colony = Board();
            var (visit, trader) = Session(colony);
            Beside(colony, trader, ItemIndex.Wood, 20);
            Beside(colony, trader, ItemIndex.Gold, 77);
            colony.World.Tick();

            WorldSnapshot frame = colony.World.Views.Current;
            TradeView view = frame.Trades.ToArray().Single();
            Assert.That(view.Ready, Is.True);
            Assert.That(view.ColonyGold, Is.EqualTo(77));
            Assert.That(view.Purse, Is.EqualTo(500));
            TradeRowView[] rows = frame.TradeRows.ToArray();
            Assert.That(rows.Select(r => r.Item), Is.EquivalentTo(new[] { ItemIndex.Wood, ItemIndex.MedicalSupplies, ItemIndex.Pistol }),
                "one row per item either side holds, and never gold");
            TradeRowView med = rows.Single(r => r.Item == ItemIndex.MedicalSupplies);
            Assert.That((med.BuyPrice, med.SellPrice, med.TraderCount, med.ColonyCount), Is.EqualTo((12, 28, 5, 0)));
            Assert.That(rows.Single(r => r.Item == ItemIndex.Pistol).TraderQuality, Is.EqualTo(QualityHandle.Normal));
        }

        [Test]
        public void NoRowsArePublishedWithoutASession()
        {
            ColonyWorld colony = Board();
            var (visit, trader) = Session(colony);
            visit.Negotiator = 0;
            visit.Ready = false;
            colony.World.Tick();
            WorldSnapshot frame = colony.World.Views.Current;
            Assert.That(frame.TradeCount, Is.EqualTo(1));
            Assert.That(frame.TradeRowCount, Is.Zero);
        }
    }
}
