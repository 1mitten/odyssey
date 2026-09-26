#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Trade;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The visitor side (design 65 §5, T2): a trader is a person who is neither one of ours nor an
    /// enemy. It thinks with its own mind, has no needs, is published as a guest, walks to the
    /// hearth (else the colony's start), heals where it lies, and no bandit hunts it.
    /// </summary>
    public class VisitorTests
    {
        [Test]
        public void ATraderIsAPersonWhoIsNeitherOursNorHostile()
        {
            var colony = Board(colonists: 1);
            Pawn trader = Spawn(colony, PawnKindIndex.Trader, Near(colony, 6, 0));

            Assert.That(trader.IsPerson, Is.True);
            Assert.That(trader.IsVisitor, Is.True);
            Assert.That(trader.IsColonist, Is.False);
            Assert.That(trader.IsHostile, Is.False);
            Assert.That(trader.NeedsTick, Is.False, "a guest has no needs to tick");
            Assert.That(trader.Faction, Is.EqualTo(Faction.Visitor));
        }

        [Test]
        public void TheColonistIsNotAVisitor()
        {
            var colony = Board(colonists: 1);
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Assert.That(colonist.IsVisitor, Is.False, "the control");
            Assert.That(colonist.IsColonist, Is.True);
        }

        [Test]
        public void TheVisitorMindIsDownedThenTheVisitThenIdle()
        {
            Assert.That(JobSystem.VisitorMind.Select(n => n.GetType()), Is.EqualTo(new[]
                { typeof(DownedThinkNode), typeof(VisitorThinkNode), typeof(IdleThinkNode) }));
        }

        [Test]
        public void ATraderIsPublishedAsAGuestAndNotAsAColonist()
        {
            var colony = Board(colonists: 1);
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Pawn trader = Spawn(colony, PawnKindIndex.Trader, Near(colony, 6, 0));
            colony.World.Tick();

            WorldSnapshot frame = colony.World.Views.Current;
            Assert.That(frame.TryGetPawn(trader.Id, out PawnView guest), Is.True);
            Assert.That(guest.IsVisitor, Is.True);
            Assert.That(guest.IsColonist, Is.False, "a trader on the roster would be a colonist to order");
            Assert.That(guest.IsHostile, Is.False);
            Assert.That(guest.IsPerson, Is.True);

            Assert.That(frame.TryGetPawn(colonist.Id, out PawnView ours), Is.True);
            Assert.That(ours.IsColonist, Is.True, "the control");
            Assert.That(ours.IsVisitor, Is.False);
        }

        /// <summary>With no hearth the visit's anchor is the colony's start, and the trader walks there.</summary>
        [Test]
        public void ATraderWalksToTheColonyAndStaysAboutIt()
        {
            var colony = Board(colonists: 1);
            int far = Near(colony, 24, 0);
            Pawn trader = Spawn(colony, PawnKindIndex.Trader, far);
            Assert.That(Distance(colony, trader.Cell, Near(colony, 0, 0)), Is.GreaterThan(VisitorThinkNode.StayCells),
                "the control: it starts out of reach of the start");

            colony.World.Tick(2_000);
            Assert.That(Distance(colony, trader.Cell, Near(colony, 0, 0)), Is.LessThanOrEqualTo(VisitorThinkNode.StayCells + 2));
            for (int t = 0; t < 1_000; t++)
            {
                colony.World.Tick();
                int job = trader.CurrentJob?.DefIndex ?? JobIndex.Wait;
                Assert.That(job == JobIndex.Wander || job == JobIndex.Wait, Is.True,
                    $"tick {t}: a guest ran job {job}, which is not walking or waiting");
            }
        }

        [Test]
        public void ADownedTraderHeals()
        {
            var colony = Board(colonists: 1);
            Pawn trader = Spawn(colony, PawnKindIndex.Trader, Near(colony, 6, 0));
            colony.World.Tick();
            int before = trader.HpMaxMilli / 2;
            trader.HpMilli = before;
            colony.World.Tick(colony.Pawns.Content.DayTicks / 4);
            Assert.That(trader.HpMilli, Is.GreaterThan(before), "a guest nobody will put to bed must heal where it is");
        }

        /// <summary>A bandit hunts colonists only (design 33 §5): with a trader and a bandit alone, the trader is never struck.</summary>
        [Test]
        public void NoBanditHuntsATrader()
        {
            var colony = Board(colonists: 1, beds: 0);
            Pawn colonist = colony.Pawns.Pawns.All[0];
            colony.Pawns.Pawns.Despawn(colonist);
            Pawn trader = Spawn(colony, PawnKindIndex.Trader, Near(colony, 2, 0));
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 4, 0));
            int hp = trader.HpMilli;
            for (int t = 0; t < 600; t++)
            {
                colony.World.Tick();
                Assert.That(bandit.CombatTarget, Is.Not.EqualTo(trader.Id.Value), $"tick {t}: a bandit hunted a guest");
            }
            Assert.That(trader.HpMilli, Is.EqualTo(hp));
        }

        static int Distance(ColonyWorld colony, int a, int b)
        {
            CellRef p = colony.Pawns.Size.FromIndex(a), q = colony.Pawns.Size.FromIndex(b);
            return System.Math.Max(System.Math.Abs(p.X - q.X), System.Math.Abs(p.Z - q.Z));
        }
    }
}
