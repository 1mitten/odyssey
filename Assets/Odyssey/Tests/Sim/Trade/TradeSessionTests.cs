#nullable enable
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
    /// The negotiator (design 65 §6, T5): a colonist sent by Trade walks beside the trader, which
    /// holds still for her; arriving makes the session ready; the session ends with the window's
    /// Cancel, a draft, or the trader being sent away; the stay clock stops while it is open; and a
    /// save taken while it is ready resumes ready.
    /// </summary>
    public class TradeSessionTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ScenarioDef Scenario()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 2;
            scenario.beds = 0;
            return scenario;
        }

        static ColonyWorld Board()
        {
            ColonyWorld colony = ColonyWorld.Build(Size, 7u, Scenario(), barren: true, wooded: false);
            colony.World.Tick(30);
            return colony;
        }

        static IntentRejection Send(ColonyWorld colony, Intent intent)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(intent);
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        static (Visit Visit, Pawn Trader, Pawn Her) Arrived(ColonyWorld colony)
        {
            Assert.That(TraderVisitTests.Fire(colony), Is.EqualTo(IntentRejection.None));
            Visit visit = colony.Pawns.Trade!.Visits.Single();
            Pawn trader = colony.Pawns.Pawns.Get(new PawnId(visit.Pawn))!;
            Pawn her = colony.Pawns.Pawns.All.First(p => p.IsColonist);
            return (visit, trader, her);
        }

        static void TickUntil(ColonyWorld colony, System.Func<bool> done, int limit, string what)
        {
            for (int i = 0; i < limit && !done(); i++) colony.World.Tick();
            Assert.That(done(), Is.True, what);
        }

        static Intent Order(Pawn her, Pawn trader) => new Intent(IntentKind.OrderTrade, default, her.Id.Value, trader.Id.Value);

        [Test]
        public void TheNegotiatorWalksOverAndTheSessionBecomesReady()
        {
            ColonyWorld colony = Board();
            var (visit, trader, her) = Arrived(colony);
            Assert.That(Send(colony, Order(her, trader)), Is.EqualTo(IntentRejection.None));
            Assert.That(visit.Negotiator, Is.EqualTo(her.Id.Value));
            Assert.That(visit.Ready, Is.False, "not until she arrives");
            Assert.That(her.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Trade));

            TickUntil(colony, () => visit.Ready, 6_000, "she never reached the trader");
            Assert.That(visit.Session, Is.EqualTo(1));
            CellRef a = Size.FromIndex(her.Cell), b = Size.FromIndex(trader.Cell);
            Assert.That(System.Math.Max(System.Math.Abs(a.X - b.X), System.Math.Abs(a.Z - b.Z)), Is.LessThanOrEqualTo(1));

            colony.World.Tick();
            TradeView view = colony.World.Views.Current.Trades.ToArray().Single();
            Assert.That(view.Ready, Is.True);
            Assert.That(view.Negotiator, Is.EqualTo(her.Id));
        }

        [Test]
        public void TheTraderHoldsStillForHer()
        {
            ColonyWorld colony = Board();
            var (visit, trader, her) = Arrived(colony);
            colony.World.Tick(60);
            Assert.That(Send(colony, Order(her, trader)), Is.EqualTo(IntentRejection.None));
            colony.World.Tick(30);
            int at = trader.Cell;
            for (int i = 0; i < 300 && !visit.Ready; i++)
            {
                colony.World.Tick();
                Assert.That(trader.HasPath && trader.CurrentJob?.DefIndex == JobIndex.Wander, Is.False, $"tick {i}: the trader walked off");
            }
        }

        [Test]
        public void TheWindowsCancelEndsTheSessionAndHerJob()
        {
            ColonyWorld colony = Board();
            var (visit, trader, her) = Arrived(colony);
            Assert.That(Send(colony, Order(her, trader)), Is.EqualTo(IntentRejection.None));
            TickUntil(colony, () => visit.Ready, 6_000, "no session");

            Assert.That(Send(colony, new Intent(IntentKind.TradeCancel, default, trader.Id.Value)), Is.EqualTo(IntentRejection.None));
            Assert.That(visit.InSession, Is.False);
            colony.World.Tick(2);
            Assert.That(her.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.Trade));
        }

        [Test]
        public void DraftingTheNegotiatorEndsTheSession()
        {
            ColonyWorld colony = Board();
            var (visit, trader, her) = Arrived(colony);
            Assert.That(Send(colony, Order(her, trader)), Is.EqualTo(IntentRejection.None));
            TickUntil(colony, () => visit.Ready, 6_000, "no session");
            Assert.That(Send(colony, new Intent(IntentKind.SetDrafted, default, her.Id.Value, 1)), Is.EqualTo(IntentRejection.None));
            Assert.That(visit.InSession, Is.False);
        }

        [Test]
        public void OneNegotiatorAtATime()
        {
            ColonyWorld colony = Board();
            var (visit, trader, her) = Arrived(colony);
            Pawn other = colony.Pawns.Pawns.All.Where(p => p.IsColonist).Skip(1).First();
            Assert.That(Send(colony, Order(her, trader)), Is.EqualTo(IntentRejection.None));
            Assert.That(Send(colony, Order(her, trader)), Is.EqualTo(IntentRejection.AlreadyInThatState));
            Assert.That(Send(colony, Order(other, trader)), Is.EqualTo(IntentRejection.NotPermitted));
        }

        [Test]
        public void ATraderIsTheOnlyThingOneCanTradeWith()
        {
            ColonyWorld colony = Board();
            var (visit, trader, her) = Arrived(colony);
            Pawn other = colony.Pawns.Pawns.All.Where(p => p.IsColonist).Skip(1).First();
            Assert.That(Send(colony, Order(her, other)), Is.EqualTo(IntentRejection.NotPermitted), "a colonist is no trader");
            Assert.That(Send(colony, Order(trader, trader)), Is.EqualTo(IntentRejection.NotPermitted), "a trader is no negotiator");
        }

        [Test]
        public void TheStayClockStopsWhileTheSessionIsOpen()
        {
            ColonyWorld colony = Board();
            var (visit, trader, her) = Arrived(colony);
            Assert.That(Send(colony, Order(her, trader)), Is.EqualTo(IntentRejection.None));
            int left = visit.StayLeft;
            colony.World.Tick(200);
            Assert.That(visit.StayLeft, Is.EqualTo(left));
            Send(colony, new Intent(IntentKind.TradeCancel, default, trader.Id.Value));
            colony.World.Tick(100);
            Assert.That(visit.StayLeft, Is.LessThan(left), "the control: it runs again once the session ends");
        }

        [Test]
        public void SendingTheTraderAwayEndsTheSession()
        {
            ColonyWorld colony = Board();
            var (visit, trader, her) = Arrived(colony);
            Assert.That(Send(colony, Order(her, trader)), Is.EqualTo(IntentRejection.None));
            TickUntil(colony, () => visit.Ready, 6_000, "no session");
            colony.Pawns.Trade!.SendAway(visit, trader);
            Assert.That(visit.InSession, Is.False);
            colony.World.Tick(2);
            Assert.That(her.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.Trade));
        }

        [Test]
        public void ASaveWhileReadyResumesReady()
        {
            ColonyWorld original = Board();
            var (visit, trader, her) = Arrived(original);
            Assert.That(Send(original, Order(her, trader)), Is.EqualTo(IntentRejection.None));
            TickUntil(original, () => visit.Ready, 6_000, "no session");

            ColonyWorld restored = ColonyWorld.Build(Size, 7u, Scenario(), barren: true, wooded: false);
            restored.Load(original.Save());
            Visit back = restored.Pawns.Trade!.Visits.Single();
            Assert.That(back.Ready, Is.True);
            Assert.That(back.Negotiator, Is.EqualTo(her.Id.Value));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(original.World.ComputeStateHash().Value));
            for (int i = 0; i < 5; i++)
            {
                original.World.Tick(60);
                restored.World.Tick(60);
                Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(original.World.ComputeStateHash().Value));
            }
            Assert.That(back.Ready, Is.True, "the session outlived the load");
        }
    }
}
