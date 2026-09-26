#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The right-click's Trade (design 57 §6, T5): on a trader under the pointer with a colonist
    /// selected, one row that sends the first standing colonist; disabled with "Downed" when every
    /// selected colonist is down; no row while the trader is leaving or somebody else negotiates.
    /// </summary>
    public class TradeMenuTests
    {
        static readonly PawnId Ada = new PawnId(1), Bo = new PawnId(2), Guest = new PawnId(9);
        static readonly CellRef GuestCell = new CellRef(6, 6, 1);

        static WorldSnapshot Board(bool adaDowned = false, bool leaving = false, PawnId negotiator = default)
        {
            WorldSnapshot frame = Frame.Write(layers: 4);
            frame.AddPawn(new PawnView(Ada, new CellRef(4, 4, 1), 800, 800, 700,
                flags: adaDowned ? PawnFlags.Person | PawnFlags.Downed : PawnFlags.Person));
            frame.AddPawn(new PawnView(Bo, new CellRef(7, 4, 1), 800, 800, 700, flags: PawnFlags.Person));
            frame.AddPawn(new PawnView(Guest, GuestCell, 800, 800, 700, kind: PawnKindLabels.Trader,
                flags: PawnFlags.Person | PawnFlags.Visitor));
            frame.AddTrade(new TradeView(1, Guest, negotiator, ready: false, session: 0, purse: 600, colonyGold: 0,
                stayLeftTicks: 30_000, leaving: leaving));
            return frame;
        }

        static List<ContextMenuRow> RightClick(WorldSnapshot frame, params PawnId[] selection)
        {
            var into = new List<Intent>();
            var menu = new List<ContextMenuRow>();
            OrderModel.RightClick(selection, frame, GuestCell, Guest, ctrl: false, into, menu);
            Assert.That(into, Is.Empty, "a right-click on a trader asks; it never acts");
            return menu;
        }

        [Test]
        public void ARightClickOnATraderOffersTradeAndSendsTheFirstStandingColonist()
        {
            List<ContextMenuRow> menu = RightClick(Board(), Ada, Bo);
            Assert.That(menu, Has.Count.EqualTo(2), "Trade, then Cancel");
            ContextMenuRow trade = menu[0];
            Assert.That(trade.Key, Is.EqualTo(ContextMenuModel.TradeKey));
            Assert.That(trade.Enabled, Is.True);
            var sent = new List<Intent>();
            ContextMenuModel.Choose(trade, sent);
            Assert.That(sent, Has.Count.EqualTo(1));
            Assert.That(sent[0].Kind, Is.EqualTo(IntentKind.OrderTrade));
            Assert.That((sent[0].A, sent[0].B), Is.EqualTo((Ada.Value, Guest.Value)));
        }

        [Test]
        public void ADownedColonistIsPassedOverForTheNextStandingOne()
        {
            var sent = new List<Intent>();
            ContextMenuModel.Choose(RightClick(Board(adaDowned: true), Ada, Bo)[0], sent);
            Assert.That(sent[0].A, Is.EqualTo(Bo.Value));
        }

        [Test]
        public void EveryColonistDownDisablesTheRowWithAReason()
        {
            ContextMenuRow trade = RightClick(Board(adaDowned: true), Ada)[0];
            Assert.That(trade.Enabled, Is.False);
            Assert.That(trade.Reason, Is.EqualTo(Registry.Label(ContextMenuModel.DownedReasonKey)));
        }

        [Test]
        public void NoRowForALeavingTrader() =>
            Assert.That(RightClick(Board(leaving: true), Ada), Is.Empty);

        [Test]
        public void NoRowWhileSomebodyElseNegotiates() =>
            Assert.That(RightClick(Board(negotiator: Bo), Ada), Is.Empty);

        [Test]
        public void NoRowWithNoColonistSelected() =>
            Assert.That(RightClick(Board()), Is.Empty);

        /// <summary>A guest is attacked only on purpose (design 57 §7): Ctrl with a drafted colonist, as a colonist is.</summary>
        [Test]
        public void CtrlRightClickWithADraftedColonistAttacksTheTrader()
        {
            WorldSnapshot frame = Board();
            frame.AddPawnAspect(new PawnAspect(Ada, AspectKey.Of(OrderModel.DraftedAspect), 1));
            var into = new List<Intent>();
            var menu = new List<ContextMenuRow>();
            OrderModel.RightClick(new[] { Ada }, frame, GuestCell, Guest, ctrl: true, into, menu);
            Assert.That(menu, Is.Empty);
            Assert.That(into.Single().Kind, Is.EqualTo(IntentKind.OrderAttack));
            Assert.That(into.Single().B, Is.EqualTo(Guest.Value));

            into.Clear();
            OrderModel.RightClick(new[] { Ada }, frame, GuestCell, Guest, ctrl: false, into, menu);
            Assert.That(into, Is.Empty, "the control: without Ctrl it asks");
            Assert.That(menu, Is.Not.Empty);
        }

        [Test]
        public void ATraderTurnedHostileKeepsItsCoatAndIsNoGuest()
        {
            PawnFlags turned = PawnFlags.Person | PawnFlags.Visitor | PawnFlags.Hostile;
            Assert.That(PawnOutfits.For(turned), Is.EqualTo(PawnOutfit.Trader));
            var view = new PawnView(Guest, GuestCell, 800, 800, 700, kind: PawnKindLabels.Trader, flags: turned);
            Assert.That(view.IsVisitor, Is.False);
            Assert.That(view.IsHostile, Is.True);
            Assert.That(view.IsColonist, Is.False);
        }

        [Test]
        public void TheTradeVerbIsTheRegistrys() =>
            Assert.That(Registry.Label(ContextMenuModel.TradeKey), Is.Not.EqualTo(ContextMenuModel.TradeKey));
    }
}
