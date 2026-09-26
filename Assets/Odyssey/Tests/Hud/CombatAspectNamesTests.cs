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
            Assert.That(CombatAspectNames.RescueNoBed, Is.EqualTo("odyssey.pawn.rescue.nobed"));
            Assert.That(CombatAspectNames.Response, Is.EqualTo("odyssey.pawn.response"));
            Assert.That(CombatAspectNames.SweepFacing, Is.EqualTo("odyssey.pawn.sweep.facing"));
            Assert.That(CombatAspectNames.HpKey, Is.EqualTo(AspectKey.Of("odyssey.pawn.hp")));
        }

        static WorldSnapshot FrameWithABandit()
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
        /// A bandit is kind 3 and a person. "Kind 0" would have kept it off the roster for the
        /// wrong reason, and "kind not 0 is an animal" would have put it in the Animals tab; the
        /// flags answer both honestly. The colonist beside it is the control.
        /// </summary>
        [Test]
        public void ABanditIsNeitherOnTheRosterNorAnAnimal()
        {
            WorldSnapshot snapshot = FrameWithABandit();

            var roster = new RosterModel();
            roster.Refresh(snapshot, selected: new PawnId(1));
            Assert.That(roster.CustomOrder, Is.EqualTo(new[] { new PawnId(1) }));

            Assert.That(OrderModel.IsColonist(snapshot, new PawnId(1)), Is.True);
            Assert.That(OrderModel.IsColonist(snapshot, new PawnId(2)), Is.False);

            snapshot.TryGetPawn(new PawnId(2), out PawnView bandit);
            snapshot.TryGetPawn(new PawnId(3), out PawnView hog);
            Assert.That(PawnKindLabels.IsAnimal(bandit), Is.False);
            Assert.That(PawnKindLabels.IsAnimal(hog), Is.True);
            Assert.That(PawnKindLabels.IconKey(3), Is.EqualTo("ui.pawn.bandit"));
        }

        [Test]
        public void TheDraftKeyPassesOverABandit()
        {
            WorldSnapshot snapshot = FrameWithABandit();
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
        /// The seams lane C filled (design 33 §5f), read through the flags: the bandit wears the
        /// marker and the colonist beside it does not; an undrafted colonist's right-click on it
        /// sends nothing, because an attack needs a draft (§5j). Their full rules are
        /// <c>CombatOrdersTests</c> and <c>CombatFeedbackModelTests</c>; this replaced the contracts
        /// step's "claims nothing yet" when the lane filled them.
        /// </summary>
        [Test]
        public void TheLaneSeamsReadTheFlags()
        {
            WorldSnapshot snapshot = FrameWithABandit();
            var sent = new List<Intent>();
            Assert.That(CombatOrders.Route(new[] { new PawnId(1) }, snapshot, new CellRef(2, 1, 0),
                new PawnId(2), ctrl: false, sent), Is.False);
            Assert.That(sent, Is.Empty);

            snapshot.TryGetPawn(new PawnId(1), out PawnView colonist);
            snapshot.TryGetPawn(new PawnId(2), out PawnView bandit);
            Assert.That(CombatFeedbackModel.HostileMarker(bandit), Is.True);
            Assert.That(CombatFeedbackModel.HostileMarker(colonist), Is.False);
            Assert.That(CombatFeedbackModel.HealthBar(snapshot, bandit, out _, out _), Is.False,
                "no hit points were published, so no bar is owed");
            Assert.That(CombatFeedbackModel.FloatingText(default), Is.Empty);
        }
    }
}
