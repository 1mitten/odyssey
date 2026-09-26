#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// EX1 (design 64 §4c): pawn ids that stay unique across boards, a registry that keeps its
    /// ascending order when a colonist with a lower id comes home, and a departure that is not a
    /// death.
    /// </summary>
    public class PawnIdSourceTests
    {
        static readonly GridSize Size = new GridSize(40, 40, 8);

        static ColonyWorld Colony(uint seed, int colonists, int beds = 0)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = beds;
            return ColonyWorld.Build(Size, seed, scenario);
        }

        [Test]
        public void ASourceNeverHandsOutAnIdTwice()
        {
            var ids = new PawnIdSource();
            Assert.That(ids.Next(), Is.EqualTo(1));
            Assert.That(ids.Next(), Is.EqualTo(2));
            ids.Observe(10);
            Assert.That(ids.Next(), Is.EqualTo(11), "an observed id is never handed out again");
            ids.Observe(4);
            Assert.That(ids.Peek, Is.EqualTo(12), "observing a lower id does not move the counter back");
            ids.Reset(3);
            Assert.That(ids.Next(), Is.EqualTo(3), "a load's counter is the truth");
        }

        [Test]
        public void TwoBoardsSharingASourceNeverRepeatAnId()
        {
            ColonyWorld home = Colony(11u, colonists: 3);
            ColonyWorld away = Colony(12u, colonists: 0);
            var shared = new PawnIdSource();
            shared.Reset(home.Pawns.Pawns.Ids.Peek);
            home.Pawns.Pawns.Ids = shared;
            away.Pawns.Pawns.Ids = shared;

            Pawn a = away.Pawns.Pawns.Spawn(Size.Index(away.Start));
            Pawn b = home.Pawns.Pawns.Spawn(Size.Index(home.Start));
            Pawn c = away.Pawns.Pawns.Spawn(Size.Index(away.Start));

            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (Pawn pawn in home.Pawns.Pawns.All) Assert.That(seen.Add(pawn.Id.Value), Is.True, $"id {pawn.Id.Value} repeated");
            foreach (Pawn pawn in away.Pawns.Pawns.All) Assert.That(seen.Add(pawn.Id.Value), Is.True, $"id {pawn.Id.Value} repeated");
            Assert.That(new[] { a.Id.Value, b.Id.Value, c.Id.Value }, Is.Ordered, "one counter, in the order asked");
        }

        [Test]
        public void AdoptingALowerIdKeepsTheRegistryInIdOrder()
        {
            ColonyWorld home = Colony(21u, colonists: 3);
            ColonyWorld away = Colony(22u, colonists: 0);
            Pawn traveller = home.Pawns.Pawns.All[0];
            home.Pawns.Pawns.Despawn(traveller, DespawnReason.Departed);

            // A pawn with a higher id is already on the board she arrives at.
            away.Pawns.Pawns.Ids.Reset(50);
            away.Pawns.Pawns.Spawn(Size.Index(away.Start));
            traveller.Cell = Size.Index(away.Start);
            away.Pawns.Pawns.Adopt(traveller);

            var all = away.Pawns.Pawns.All;
            Assert.That(all.Count, Is.EqualTo(2));
            Assert.That(all[0], Is.SameAs(traveller), "the lower id goes first, not last");
            Assert.That(all[0].Id.Value, Is.LessThan(all[1].Id.Value));
            Assert.That(away.Pawns.Pawns.Get(traveller.Id), Is.SameAs(traveller), "and the by-id map finds her");
            Assert.That(away.Pawns.Pawns.Get(all[1].Id), Is.SameAs(all[1]), "and still finds the pawn she stepped in front of");
        }

        [Test]
        public void ADepartureKeepsHerBedAndARemovalDoesNot()
        {
            ColonyWorld colony = Colony(31u, colonists: 2, beds: 2);
            Assume.That(colony.Pawns.Items.Beds.Count, Is.GreaterThanOrEqualTo(2), "the scenario raised two beds");
            Pawn leaver = colony.Pawns.Pawns.All[0];
            Pawn dead = colony.Pawns.Pawns.All[1];
            int bedA = colony.Pawns.Items.Beds[0], bedB = colony.Pawns.Items.Beds[1];
            Assign(colony, bedA, leaver.Id.Value);
            Assign(colony, bedB, dead.Id.Value);
            Assume.That(colony.Construction.BedOwnerAt(bedA), Is.EqualTo(leaver.Id.Value));
            Assume.That(colony.Construction.BedOwnerAt(bedB), Is.EqualTo(dead.Id.Value));

            colony.Pawns.Pawns.Despawn(leaver, DespawnReason.Departed);
            colony.Pawns.Pawns.Despawn(dead);

            Assert.That(colony.Construction.BedOwnerAt(bedA), Is.EqualTo(leaver.Id.Value), "she is coming back to it");
            Assert.That(colony.Construction.BedOwnerAt(bedB), Is.EqualTo(0), "a removal still releases the bed");
            Assert.That(colony.Pawns.Pawns.Get(leaver.Id), Is.Null, "and she is off the board either way");
        }

        static void Assign(ColonyWorld colony, int bedCell, int pawnId)
        {
            colony.World.Intents.Submit(new Intent(IntentKind.AssignBedOwner, Size.FromIndex(bedCell), pawnId));
            colony.World.Tick();
        }
    }
}
