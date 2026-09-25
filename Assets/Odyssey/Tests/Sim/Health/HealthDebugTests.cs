#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The debug menu's Hurt, Heal and Kill (design 43 §11): on the colonist nearest the camera,
    /// through the one owner of damage and the one way to die, so a debug kill is a real death.
    /// </summary>
    public class HealthDebugTests
    {
        static (ColonyWorld colony, Pawn near, Pawn far) Two()
        {
            var colony = Board(colonists: 2, beds: 0);
            colony.World.Tick(5);
            foreach (Pawn p in colony.Pawns.Pawns.All)
                for (int w = 0; w < p.WorkPriorities.Length; w++) p.WorkPriorities[w] = 0;
            Pawn near = colony.Pawns.Pawns.All[0], far = colony.Pawns.Pawns.All[1];
            Stand(colony, near, Near(colony, 0, 0));
            Stand(colony, far, Near(colony, 12, 12));
            return (colony, near, far);
        }

        static IntentRejection Health(ColonyWorld colony, int op) =>
            Send(colony, new Intent(IntentKind.DebugHealth, colony.Start, op));

        [Test]
        public void HurtCutsTheNearestColonistAndNobodyElse()
        {
            var (colony, near, far) = Two();
            Assert.That(Health(colony, 0), Is.EqualTo(IntentRejection.None));
            Assert.That(near.HpMilli, Is.EqualTo(near.HpMaxMilli - 20_000));
            Assert.That(near.Health!.BleedingSeverityMilli, Is.EqualTo(20_000), "a debug hurt is a cut");
            Assert.That(far.HasHealthState, Is.False, "the control: the colonist further off is untouched");
        }

        [Test]
        public void HealMakesHerWholeAndStandsHerUp()
        {
            var (colony, near, _) = Two();
            for (int i = 0; i < 4; i++) Health(colony, 0);
            Assert.That(near.Downed, Is.True, "the fixture: eighty points of cuts down her");
            Assert.That(Health(colony, 1), Is.EqualTo(IntentRejection.None));
            Assert.That(near.HpMilli, Is.EqualTo(near.HpMaxMilli));
            Assert.That(near.HasHealthState, Is.False);
            Assert.That(near.Downed, Is.False);
            Assert.That(Health(colony, 1), Is.EqualTo(IntentRejection.AlreadyInThatState), "the control: healing the whole does nothing");
        }

        [Test]
        public void KillIsARealDeath()
        {
            var (colony, near, far) = Two();
            Assert.That(Health(colony, 2), Is.EqualTo(IntentRejection.None));
            colony.World.Tick();
            Assert.That(colony.Pawns.Pawns.Get(near.Id), Is.Null);
            Assert.That(colony.Pawns.Corpses.Count, Is.EqualTo(1), "a debug kill left no corpse");
            Assert.That(far.Memories.Exists(m => m.ThoughtIndex == ThoughtIndex.ColonistDied), Is.True, "a debug kill went unmourned");
        }
    }
}
