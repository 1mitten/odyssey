#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Trade;

namespace Odyssey.Tests.Sim.Trade
{
    /// <summary>
    /// The trader's visit (design 57 §5, T3): the incident walks one trader on at an edge with a
    /// purse and a stock, it goes to the colony, it leaves when its stay runs out or a raid comes,
    /// it takes its pistol with it, and a save taken mid-visit resumes the same visit.
    /// </summary>
    public class TraderVisitTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ScenarioDef Scenario()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 2;
            scenario.beds = 2;
            return scenario;
        }

        static ColonyWorld Board(uint seed = 7u)
        {
            ColonyWorld colony = ColonyWorld.Build(Size, seed, Scenario(), barren: true, wooded: false);
            colony.World.Tick(30);
            return colony;
        }

        internal static IntentRejection Fire(ColonyWorld colony)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(new Intent(IntentKind.InvokeIncident, default, IncidentHandle.Trader));
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        static TradeSystem Trade(ColonyWorld colony) => colony.Pawns.Trade!;

        static Pawn Trader(ColonyWorld colony) => colony.Pawns.Pawns.Get(new PawnId(Trade(colony).Visits.Single().Pawn))!;

        static ulong Hash(ColonyWorld colony) => colony.World.ComputeStateHash().Value;

        static int Pistols(ColonyWorld colony) =>
            colony.Pawns.Items.Items.Count(i => !i.Despawned && i.DefIndex == ItemIndex.Pistol);

        static int ToStart(ColonyWorld colony, int cell)
        {
            CellRef at = Size.FromIndex(cell), s = colony.Start;
            return Math.Max(Math.Abs(at.X - s.X), Math.Abs(at.Z - s.Z));
        }

        [Test]
        public void TheIncidentWalksOneTraderOnAtAnEdge()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony), Is.EqualTo(IntentRejection.None));

            Assert.That(Trade(colony).Count, Is.EqualTo(1));
            Pawn trader = Trader(colony);
            Assert.That(trader.IsVisitor, Is.True);
            Assert.That(trader.Kind, Is.EqualTo(PawnKindIndex.Trader));
            CellRef at = Size.FromIndex(trader.Cell);
            Assert.That(at.X == 0 || at.Z == 0 || at.X == Size.SizeX - 1 || at.Z == Size.SizeZ - 1
                || ToStart(colony, trader.Cell) > 20, Is.True, "it arrives at the board's edge, far from the colony");
        }

        [Test]
        public void AVisitCarriesAPurseAndARolledStockButNeverGold()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony), Is.EqualTo(IntentRejection.None));
            Visit visit = Trade(colony).Visits.Single();
            TraderKindDef def = colony.Incidents.Content.Traders[visit.Kind].Def;

            Assert.That(visit.Purse, Is.InRange(def.purseMin, def.purseMax));
            int lines = visit.Stock.Count(n => n > 0);
            Assert.That(lines, Is.InRange(def.linesMin, def.linesMax));
            Assert.That(visit.Stock[ItemIndex.Gold], Is.Zero, "gold is the purse, never stock");
        }

        [Test]
        public void OnlyOneTraderVisitsAtATime()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony), Is.EqualTo(IntentRejection.None));
            Assert.That(Fire(colony), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(Trade(colony).Count, Is.EqualTo(1));
        }

        [Test]
        public void TheArrivalIsWrittenInTheLedger()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Incidents.Ledger.Fires(IncidentHandle.Trader), Is.EqualTo(1));
        }

        [Test]
        public void TheTraderWalksToTheColony()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony), Is.EqualTo(IntentRejection.None));
            Pawn trader = Trader(colony);
            Assert.That(ToStart(colony, trader.Cell), Is.GreaterThan(VisitorThinkNode.StayCells), "the control");
            for (int i = 0; i < 4_000 && ToStart(colony, trader.Cell) > VisitorThinkNode.StayCells + 2; i++) colony.World.Tick();
            Assert.That(ToStart(colony, trader.Cell), Is.LessThanOrEqualTo(VisitorThinkNode.StayCells + 2));
        }

        /// <summary>When its stay runs out it leaves by an edge and is gone, pistol and all.</summary>
        [Test]
        public void WhenItsStayRunsOutItLeavesWithItsPistol()
        {
            ColonyWorld colony = Board();
            int before = Pistols(colony);
            Assert.That(Fire(colony), Is.EqualTo(IntentRejection.None));
            Visit visit = Trade(colony).Visits.Single();
            Pawn trader = Trader(colony);
            colony.World.Tick(200);
            Assert.That(trader.Leaving, Is.False, "the control: it stays while its clock runs");

            visit.StayLeft = 1;
            colony.World.Tick(2);
            Assert.That(trader.Leaving, Is.True);

            for (int i = 0; i < 6_000 && Trade(colony).Count > 0; i++) colony.World.Tick();
            Assert.That(Trade(colony).Count, Is.Zero, "the trader never left the board");
            Assert.That(colony.Pawns.Pawns.Get(trader.Id), Is.Null);
            Assert.That(Pistols(colony), Is.EqualTo(before), "a guest's pistol was left behind");
        }

        [Test]
        public void TheStayIsADay()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony), Is.EqualTo(IntentRejection.None));
            Assert.That(Trade(colony).Visits.Single().StayLeft, Is.EqualTo(24 * Calendar.TicksPerHour).Within(2));
        }

        [Test]
        public void ARaidSendsTheTraderAway()
        {
            ColonyWorld colony = Board();
            Assert.That(Fire(colony), Is.EqualTo(IntentRejection.None));
            Pawn trader = Trader(colony);
            colony.World.Intents.Submit(new Intent(IntentKind.InvokeIncident, default, IncidentHandle.Raid, 3, 1));
            colony.World.Tick(3);
            Assert.That(trader.Leaving, Is.True);
        }

        /// <summary>The debug menu's spawned trader has no incident behind it, and still gets a visit.</summary>
        [Test]
        public void ASpawnedTraderIsAdoptedIntoAVisit()
        {
            ColonyWorld colony = Board();
            colony.World.Intents.Submit(new Intent(IntentKind.SpawnPawn, colony.Start, PawnKindIndex.Trader));
            colony.World.Tick(TradeSystem.AdoptTicks + 1);
            Assert.That(Trade(colony).Count, Is.EqualTo(1));
            Assert.That(Trade(colony).Visits[0].Purse, Is.GreaterThan(0));
        }

        [Test]
        public void AnEmptyTradeSystemAddsNothingToTheHash()
        {
            StateHash hash = StateHash.New();
            ((IStateHashable)Board().Pawns.Trade!).ContributeTo(ref hash);
            Assert.That(hash.Value, Is.EqualTo(StateHash.New().Value));
        }

        [Test]
        public void ASaveMidVisitResumesTheSameVisit()
        {
            ColonyWorld original = Board();
            Assert.That(Fire(original), Is.EqualTo(IntentRejection.None));
            original.World.Tick(600);

            ColonyWorld restored = ColonyWorld.Build(Size, 7u, Scenario(), barren: true, wooded: false);
            var header = restored.Load(original.Save());
            Assert.That(header.SkippedSections, Is.Empty);
            Assert.That(Hash(restored), Is.EqualTo(Hash(original)), "the hash differs immediately after loading");
            Assert.That(restored.Pawns.Trade!.Count, Is.EqualTo(1));
            Assert.That(restored.Pawns.Trade!.Visits[0].Purse, Is.EqualTo(original.Pawns.Trade!.Visits[0].Purse));

            for (int i = 0; i < 10; i++)
            {
                original.World.Tick(100);
                restored.World.Tick(100);
                Assert.That(Hash(restored), Is.EqualTo(Hash(original)), $"the worlds parted {100 * (i + 1)} ticks after the load");
            }
        }
    }
}
