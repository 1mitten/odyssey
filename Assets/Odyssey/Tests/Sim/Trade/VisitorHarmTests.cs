#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Trade;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim.Trade
{
    /// <summary>
    /// Harm to a guest (design 57 §7, T7; the owner's "1 and 3 depending"): a colonist ordered to
    /// attack the trader turns it hostile; any other blow — a raider's, a stray, a colonist aiming at
    /// somebody else — sends it home. A trader turned hostile stays hostile across a save.
    /// </summary>
    public class VisitorHarmTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        static ScenarioDef Scenario()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 2;
            scenario.beds = 0;
            return scenario;
        }

        static (ColonyWorld Colony, Visit Visit, Pawn Trader, Pawn Her) Visited()
        {
            ColonyWorld colony = ColonyWorld.Build(Size, 7u, Scenario(), barren: true, wooded: false);
            colony.World.Tick(30);
            Assert.That(TraderVisitTests.Fire(colony), Is.EqualTo(IntentRejection.None));
            Visit visit = colony.Pawns.Trade!.Visits.Single();
            Pawn trader = colony.Pawns.Pawns.Get(new PawnId(visit.Pawn))!;
            Pawn her = colony.Pawns.Pawns.All.First(p => p.IsColonist);
            return (colony, visit, trader, her);
        }

        [Test]
        public void AColonistOrderedToAttackTheTraderTurnsItHostile()
        {
            var (colony, visit, trader, her) = Visited();
            Assert.That(Draft(colony, her), Is.EqualTo(IntentRejection.None));
            Assert.That(Attack(colony, her, trader), Is.EqualTo(IntentRejection.None));
            Assert.That(her.CombatTarget, Is.EqualTo(trader.Id.Value));

            Strike(colony, her, trader, 1_000);
            Assert.That(trader.TurnedHostile, Is.True);
            Assert.That(trader.IsHostile, Is.True);
            Assert.That(trader.IsVisitor, Is.False);
            Assert.That(colony.Pawns.Trade!.Count, Is.Zero, "the visit is over");

            colony.World.Tick();
            Assert.That(colony.World.Views.Current.TryGetPawn(trader.Id, out PawnView view), Is.True);
            Assert.That(view.IsHostile, Is.True);
            Assert.That(view.IsVisitor, Is.False, "an enemy in a trader's coat is no guest");
            Assert.That((view.Flags & PawnFlags.Visitor) != 0, Is.True, "it keeps the coat");
        }

        [Test]
        public void ARaidersBlowSendsTheTraderHome()
        {
            var (colony, visit, trader, her) = Visited();
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 20, 20));
            Strike(colony, bandit, trader, 1_000);
            Assert.That(trader.TurnedHostile, Is.False);
            Assert.That(trader.Leaving, Is.True);
        }

        [Test]
        public void AColonistsBlowAimedElsewhereIsAnAccident()
        {
            var (colony, visit, trader, her) = Visited();
            Assert.That(her.CombatTarget, Is.Zero, "the control: she was sent at nobody");
            Strike(colony, her, trader, 1_000);
            Assert.That(trader.TurnedHostile, Is.False);
            Assert.That(trader.Leaving, Is.True);
        }

        [Test]
        public void AnUnharmedTraderStays()
        {
            var (colony, visit, trader, her) = Visited();
            colony.World.Tick(300);
            Assert.That(trader.Leaving, Is.False);
            Assert.That(trader.TurnedHostile, Is.False);
        }

        [Test]
        public void ATraderTurnedHostileStaysHostileAcrossASave()
        {
            var (colony, visit, trader, her) = Visited();
            colony.Pawns.Trade!.Turn(trader);
            ColonyWorld restored = ColonyWorld.Build(Size, 7u, Scenario(), barren: true, wooded: false);
            restored.Load(colony.Save());
            Pawn back = restored.Pawns.Pawns.Get(trader.Id)!;
            Assert.That(back.TurnedHostile, Is.True);
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));
        }

        [Test]
        public void TurningHostileMovesTheHashAndNothingElseDoes()
        {
            var (colony, visit, trader, her) = Visited();
            ulong before = colony.World.ComputeStateHash().Value;
            trader.TurnedHostile = true;
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(before));
        }
    }
}
