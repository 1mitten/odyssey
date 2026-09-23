#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Sim.Contracts;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The interface's half of the combat contracts step (design 33 §5): the aspect spellings it
    /// reads, the flags that replaced "kind 0 is a person", and the seams lane C fills — each with
    /// its control.
    /// </summary>
    public class CombatAspectNamesTests
    {
        [Test]
        public void TheAspectNamesAreSpelledAsTheSimulationPublishesThem()
        {
            // Held to the literals Odyssey.Sim.Pawns.CombatAspects publishes;
            // CombatContractTests.TheAspectNamesAreSpelledAsTheInterfaceReadsThem holds the other side.
            Assert.That(CombatAspectNames.Hp, Is.EqualTo("odyssey.pawn.hp"));
            Assert.That(CombatAspectNames.HpMax, Is.EqualTo("odyssey.pawn.hp.max"));
            Assert.That(CombatAspectNames.Weapon, Is.EqualTo("odyssey.pawn.weapon"));
            Assert.That(CombatAspectNames.OrderTarget, Is.EqualTo("odyssey.pawn.order.target"));
            Assert.That(CombatAspectNames.HpKey, Is.EqualTo(AspectKey.Of("odyssey.pawn.hp")));
        }

        static WorldSnapshot FrameWithAMarauder()
        {
            var snapshot = Frame.Write();
            snapshot.AddPawn(new PawnView(new PawnId(1), new CellRef(1, 1, 0), 600, 800, 800, JobHandle.Wait));
            snapshot.AddPawn(new PawnView(new PawnId(2), new CellRef(2, 1, 0), 600, 800, 800, JobHandle.Wait,
                kind: 3, flags: PawnFlags.Person | PawnFlags.Hostile));
            snapshot.AddPawn(new PawnView(new PawnId(3), new CellRef(3, 1, 0), 600, 800, 800, JobHandle.Wander,
                kind: 1, flags: PawnFlags.None));
            return snapshot;
        }

        /// <summary>
        /// A marauder is kind 3 and a person. "Kind 0" would have kept it off the roster for the
        /// wrong reason, and "kind not 0 is an animal" would have put it in the Animals tab; the
        /// flags answer both honestly. The colonist beside it is the control.
        /// </summary>
        [Test]
        public void AMarauderIsNeitherOnTheRosterNorAnAnimal()
        {
            WorldSnapshot snapshot = FrameWithAMarauder();

            var roster = new RosterModel();
            roster.Refresh(snapshot, selected: new PawnId(1));
            Assert.That(roster.CustomOrder, Is.EqualTo(new[] { new PawnId(1) }));

            Assert.That(OrderModel.IsColonist(snapshot, new PawnId(1)), Is.True);
            Assert.That(OrderModel.IsColonist(snapshot, new PawnId(2)), Is.False);

            snapshot.TryGetPawn(new PawnId(2), out PawnView marauder);
            snapshot.TryGetPawn(new PawnId(3), out PawnView hog);
            Assert.That(PawnKindLabels.IsAnimal(marauder), Is.False);
            Assert.That(PawnKindLabels.IsAnimal(hog), Is.True);
            Assert.That(PawnKindLabels.IconKey(3), Is.EqualTo("ui.pawn.marauder"));
        }

        [Test]
        public void TheDraftKeyPassesOverAMarauder()
        {
            WorldSnapshot snapshot = FrameWithAMarauder();
            var sent = new List<Intent>();
            OrderModel.ToggleDraft(new[] { new PawnId(1), new PawnId(2), new PawnId(3) }, snapshot, sent);

            Assert.That(sent.Count, Is.EqualTo(1), "only the colonist is drafted");
            Assert.That(sent[0].A, Is.EqualTo(1));
        }

        /// <summary>
        /// Every job the simulation can run has a word, the combat five included: a job past the
        /// table reads as idle, which is what a fighting colonist must not.
        /// </summary>
        [Test]
        public void TheCombatJobsHaveTheirOwnWords()
        {
            Assert.That(JobLabels.IconKey(JobHandle.AttackMelee), Is.EqualTo("ui.status.fighting"));
            Assert.That(JobLabels.IconKey(JobHandle.Flee), Is.EqualTo("ui.status.fleeing"));
            Assert.That(JobLabels.IconKey(JobHandle.Downed), Is.EqualTo("ui.status.downed"));
            Assert.That(JobLabels.IconKey(JobHandle.Equip), Is.EqualTo("ui.status.equipping"));
            Assert.That(JobLabels.IconKey(JobHandle.Rescue), Is.EqualTo("ui.status.rescuing"));
            Assert.That(ItemLabels.IconKey(ItemHandle.ArcBlade), Is.EqualTo("ui.item.arcblade"));
        }

        /// <summary>
        /// The seams lane C fills say "nothing" until it does: no routing, no bar, no text, no
        /// marker. A right-click is therefore still exactly C1's move.
        /// </summary>
        [Test]
        public void TheLaneSeamsClaimNothingYet()
        {
            WorldSnapshot snapshot = FrameWithAMarauder();
            var sent = new List<Intent>();
            Assert.That(CombatOrders.Route(new[] { new PawnId(1) }, snapshot, new CellRef(2, 1, 0),
                new PawnId(2), ctrl: false, sent), Is.False);
            Assert.That(sent, Is.Empty);

            snapshot.TryGetPawn(new PawnId(2), out PawnView marauder);
            Assert.That(CombatFeedbackModel.HealthBar(snapshot, marauder, out _, out _), Is.False);
            Assert.That(CombatFeedbackModel.HostileMarker(marauder), Is.False);
            Assert.That(CombatFeedbackModel.FloatingText(default), Is.Empty);
        }
    }
}
