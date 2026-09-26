#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// A hurt animal's answer (design 33 §1, §6A): a revenge roll on every blow, on its species'
    /// chance — a hog usually turns, a rat usually runs — seeded, so the same blow on the same tick
    /// comes out the same. A turned animal hunts its attacker; one that did not runs
    /// <see cref="CombatDef.fleeCells"/> away.
    /// </summary>
    public class AnimalRevengeTests
    {
        const int Trials = 400;

        static int Turns(ColonyWorld colony, Pawn by, Pawn animal)
        {
            int turned = 0;
            for (int k = 0; k < Trials; k++)
            {
                colony.Jobs.EndJob(animal, JobStatus.Failed);
                animal.RetaliateAgainst = 0;
                animal.RetaliateUntilTick = 0;
                colony.Pawns.Combat!.ApplySwing(by, animal, Fists(colony.Pawns), Blow(1), 20_000 + k);
                if (animal.RetaliateAgainst == by.Id.Value) turned++;
                else Assert.That(animal.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Flee), $"trial {k}: neither turned nor ran");
            }
            return turned;
        }

        [Test]
        public void AHogMostlyTurnsAndARatMostlyRuns()
        {
            var colony = Board();
            colony.World.Tick(5);
            Pawn by = colony.Pawns.Pawns.All[0];
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 6, 0));
            Pawn rat = Spawn(colony, PawnKindIndex.DuctRat, Near(colony, -6, 0));
            colony.World.Tick();

            int hogTurns = Turns(colony, by, hog), ratTurns = Turns(colony, by, rat);
            Assert.That(hogTurns * 1_000 / Trials, Is.EqualTo(hog.Species.revengePerMille).Within(60));
            Assert.That(ratTurns * 1_000 / Trials, Is.EqualTo(rat.Species.revengePerMille).Within(30));
            Assert.That(hogTurns, Is.GreaterThan(ratTurns * 5), "the control: the two species answer differently");

            // Seeded: the same blows on the same ticks, the same answers.
            Assert.That(Turns(colony, by, hog), Is.EqualTo(hogTurns));
        }

        /// <summary>A hog that turned hunts whoever struck it, and lands blows of its own.</summary>
        [Test]
        public void ATurnedHogHuntsItsAttacker()
        {
            var colony = Board();
            colony.World.Tick(5);
            Pawn by = colony.Pawns.Pawns.All[0];
            Stand(colony, by, Near(colony, 0, 0));
            Assert.That(Draft(colony, by), Is.EqualTo(IntentRejection.None));
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 5, 0));
            var rules = new RecordingRules();
            colony.Pawns.MeleeRules = rules;
            colony.World.Tick();

            int tick = colony.World.CurrentTick;
            for (int k = 0; hog.RetaliateAgainst == 0 && k < 50; k++)
                colony.Pawns.Combat!.ApplySwing(by, hog, Fists(colony.Pawns), Blow(1), tick + k);
            Assume.That(hog.RetaliateAgainst, Is.EqualTo(by.Id.Value), "fifty blows and the hog never turned");

            for (int t = 0; t < 2_000 && rules.TicksOf(hog).Count == 0; t++) colony.World.Tick();
            Assert.That(rules.TicksOf(hog).Count, Is.GreaterThan(0), "the hog never struck back");
            Assert.That(rules.Swings.Find(s => s.Attacker == hog.Id.Value).Target, Is.EqualTo(by.Id.Value));
        }

        /// <summary>A rat that did not turn runs: after the blow it is further from its attacker, by about the flee distance.</summary>
        [Test]
        public void ARatThatDoesNotTurnRuns()
        {
            var colony = Board();
            colony.World.Tick(5);
            Pawn by = colony.Pawns.Pawns.All[0];
            Stand(colony, by, Near(colony, 0, 0));
            Assert.That(Draft(colony, by), Is.EqualTo(IntentRejection.None));
            Pawn rat = Spawn(colony, PawnKindIndex.DuctRat, Near(colony, 2, 0));
            colony.World.Tick();

            int tick = colony.World.CurrentTick;
            for (int k = 0; rat.CurrentJob?.DefIndex != JobIndex.Flee && k < 50; k++)
            {
                rat.RetaliateAgainst = 0;
                colony.Pawns.Combat!.ApplySwing(by, rat, Fists(colony.Pawns), Blow(1), tick + k);
            }
            Assert.That(rat.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Flee));
            int before = colony.Pawns.Distance(by.Cell, rat.Cell);

            colony.World.Tick(1_200);
            int after = colony.Pawns.Distance(by.Cell, rat.Cell);
            Assert.That(after, Is.GreaterThan(before), "the rat did not get away");
            var at = Size.FromIndex(rat.Cell);
            var from = Size.FromIndex(by.Cell);
            int chebyshev = System.Math.Max(System.Math.Abs(at.X - from.X), System.Math.Abs(at.Z - from.Z));
            Assert.That(chebyshev, Is.GreaterThanOrEqualTo(colony.Pawns.Content.Combat.fleeCells / 2), "it ran a step and stopped");
        }
    }
}
