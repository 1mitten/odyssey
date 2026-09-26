#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim.Events
{
    /// <summary>
    /// Tension and difficulty (design 59 §5, §6): a colonist lost eases the next threats, a raider
    /// lost does not; a quiet day gives some back; the difficulty's adaptation scales both; a change
    /// of storyteller keeps it; and a save keeps all of it.
    /// </summary>
    public class StorytellerTensionTests
    {
        const int Lethal = 400_000;

        static ColonyWorld Told(int colonists = 3, uint seed = 7u)
        {
            ColonyWorld colony = Board(colonists: colonists, seed: seed);
            colony.World.Tick();
            Assert.That(Send(colony, new Intent(IntentKind.SetStoryteller, default, StorytellerHandle.Jacob)), Is.EqualTo(IntentRejection.None));
            return colony;
        }

        static Storyteller Of(ColonyWorld colony) => colony.Pawns.Storyteller!;

        static Pawn Colonist(ColonyWorld colony, int n = 0) => colony.Pawns.Pawns.All.Where(p => p.IsColonist).ElementAt(n);

        static void KillNow(ColonyWorld colony, Pawn by, Pawn target)
        {
            Strike(colony, by, target, Lethal);
            colony.World.Tick();
        }

        [Test]
        public void AColonistsDeathEasesItARaidersDoesNot()
        {
            ColonyWorld colony = Told();
            Storyteller teller = Of(colony);
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 3, 0));
            KillNow(colony, Colonist(colony), bandit);
            Assert.That(colony.Pawns.Pawns.Get(bandit.Id), Is.Null, "the control: the bandit is dead");
            Assert.That(teller.TensionPerMille, Is.EqualTo(Storyteller.TensionStart), "a raider's death eased the tension");

            KillNow(colony, Colonist(colony, 1), Colonist(colony, 0));
            Assert.That(teller.TensionPerMille, Is.EqualTo(Storyteller.TensionStart - Storyteller.DeathDrop),
                "one colonist of a small colony, killed outright, counts once and in full");
            Assert.That(teller.Cause, Is.EqualTo(TensionCauseKind.Died));
            StorytellerView view = colony.World.Views.Current.Storyteller;
            Assert.That(view.Band, Is.EqualTo(Storyteller.BandOf(teller.TensionPerMille)));
            Assert.That(view.Band, Is.LessThan(2), "a death leaves the colony easing");
        }

        [Test]
        public void ADownCountsLess()
        {
            ColonyWorld colony = Told();
            Pawn victim = Colonist(colony);
            colony.Pawns.Combat!.Down(victim, null, -1, colony.World.CurrentTick);
            Assert.That(Of(colony).TensionPerMille, Is.EqualTo(Storyteller.TensionStart - Storyteller.DownDrop));
            Assert.That(Of(colony).Cause, Is.EqualTo(TensionCauseKind.Downed));
        }

        [Test]
        public void AQuietDayGivesSomeBack()
        {
            ColonyWorld colony = Told();
            colony.Pawns.Combat!.Down(Colonist(colony), null, -1, colony.World.CurrentTick);
            Storyteller teller = Of(colony);
            int after = teller.TensionPerMille;

            colony.World.Tick(Calendar.TicksPerDay - colony.World.CurrentTick % Calendar.TicksPerDay + 1);
            Assert.That(teller.TensionPerMille, Is.EqualTo(after), "the day of the loss is not a quiet day");

            colony.World.Tick(Calendar.TicksPerDay);
            Assert.That(teller.TensionPerMille, Is.EqualTo(after + Storyteller.QuietBelow));
            Assert.That(teller.Cause, Is.EqualTo(TensionCauseKind.Quiet));
            Assert.That(colony.World.Views.Current.Storyteller.CauseDays, Is.EqualTo(1));
        }

        [Test]
        public void AdaptationScalesTheDrop()
        {
            ColonyWorld colony = Told();
            Send(colony, new Intent(IntentKind.SetDifficulty, default, 1, 30 | (200 << 16), 150 | (1 << 16)));
            colony.Pawns.Combat!.Down(Colonist(colony), null, -1, colony.World.CurrentTick);
            Assert.That(Of(colony).TensionPerMille, Is.EqualTo(Storyteller.TensionStart - 2 * Storyteller.DownDrop));

            ColonyWorld flat = Told();
            Send(flat, new Intent(IntentKind.SetDifficulty, default, 0, 10 | (0 << 16), 100));
            flat.Pawns.Combat!.Down(Colonist(flat), null, -1, flat.World.CurrentTick);
            Assert.That(Of(flat).TensionPerMille, Is.EqualTo(Storyteller.TensionStart), "Peaceful: no adaptation at all");
        }

        [Test]
        public void ItNeverFallsBelowTheFloor()
        {
            ColonyWorld colony = Told(colonists: 1);
            Send(colony, new Intent(IntentKind.SetDifficulty, default, 1, 30 | (200 << 16), 150 | (1 << 16)));
            for (int i = 0; i < 5; i++) Of(colony).NoteLoss(Colonist(colony), died: true, colony.World.CurrentTick);
            Assert.That(Of(colony).TensionPerMille, Is.EqualTo(Storyteller.TensionMin));
        }

        [Test]
        public void ChangingStorytellerKeepsTheTension()
        {
            ColonyWorld colony = Told();
            colony.Pawns.Combat!.Down(Colonist(colony), null, -1, colony.World.CurrentTick);
            int tension = Of(colony).TensionPerMille;
            int start = Of(colony).StartTick;
            Assert.That(Send(colony, new Intent(IntentKind.SetStoryteller, default, StorytellerHandle.Kano)), Is.EqualTo(IntentRejection.None));
            Assert.That(Of(colony).TensionPerMille, Is.EqualTo(tension));
            Assert.That(Of(colony).StartTick, Is.EqualTo(start), "a change is not a new colony");
        }

        [Test]
        public void ASaveKeepsTheTension()
        {
            ColonyWorld colony = Told(seed: 9u);
            colony.Pawns.Combat!.Down(Colonist(colony), null, -1, colony.World.CurrentTick);
            colony.World.Tick(100);

            ColonyWorld restored = Board(colonists: 3, seed: 9u);
            restored.Load(colony.Save());
            Assert.That(Of(restored).TensionPerMille, Is.EqualTo(Of(colony).TensionPerMille));
            Assert.That(Of(restored).Cause, Is.EqualTo(TensionCauseKind.Downed));
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));
        }

        [Test]
        public void TensionScalesTheRaid()
        {
            ColonyWorld colony = Told();
            IncidentContent content = colony.Incidents.Content;
            RaidParams p = content.Defs[IncidentHandle.Raid].raid!;
            Send(colony, new Intent(IntentKind.SetDifficulty, default, 6, 400 | (100 << 16), 100 | (1 << 16)));
            int before = RaidWorker.SizeFor(colony.Pawns, content, p, 0, -1, colony.World.CurrentTick);
            for (int i = 0; i < 3; i++) Of(colony).NoteLoss(Colonist(colony), died: true, colony.World.CurrentTick);
            int after = RaidWorker.SizeFor(colony.Pawns, content, p, 0, -1, colony.World.CurrentTick);
            Assert.That(after, Is.LessThan(before), "a colony reeling from losses met the same raid");
        }
    }
}
