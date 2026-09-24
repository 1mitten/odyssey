#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Healing (design 33 §1, §6A): a colonist heals only lying in a bed, an animal wherever it
    /// lies, a marauder never; a downed pawn gets up at 15 % of its pool; the rate is the content's
    /// per day, exactly. Each claim is set beside the pawn that does not heal, over the same day.
    /// </summary>
    public class HealingTests
    {
        static int Day(ColonyWorld colony) => colony.Pawns.Content.DayTicks;

        /// <summary>Doctor off for everybody, so only the bed heals.</summary>
        static void NoDoctors(ColonyWorld colony)
        {
            foreach (Pawn pawn in colony.Pawns.Pawns.All) pawn.WorkPriorities[WorkTypeIndex.Doctor] = 0;
        }

        [Test]
        public void AColonistHealsInABedAndNowhereElse()
        {
            var colony = Board(colonists: 3);
            colony.World.Tick(5);
            Pawn inBed = colony.Pawns.Pawns.All[0], onGround = colony.Pawns.Pawns.All[1], by = colony.Pawns.Pawns.All[2];
            // The heal this asserts is the bed's; a doctor dressing the one on the ground is
            // design 37's and MedicalTreatmentTests' (it heals her by bareHeal, which is the point).
            NoDoctors(colony);
            int bed = colony.Pawns.Items.Beds[0];
            Stand(colony, inBed, bed);
            Stand(colony, onGround, Near(colony, 8, 8));
            Assume.That(bed, Is.Not.EqualTo(onGround.Cell));

            Strike(colony, by, inBed, inBed.HpMilli + 5_000);
            Strike(colony, by, onGround, onGround.HpMilli + 5_000);
            Assert.That(inBed.Downed && onGround.Downed, Is.True);

            colony.World.Tick(Day(colony) / 4);
            Assert.That(onGround.HpMilli, Is.EqualTo(-5_000), "a colonist healed on the ground");
            Assert.That(inBed.HpMilli, Is.EqualTo(-5_000 + colony.Pawns.Content.Combat.bedHealPerDay / 4).Within(50),
                "a quarter of a day in bed is a quarter of a day's healing");
        }

        /// <summary>
        /// Up at 15 % of the pool, no sooner: Job_Downed ends as a success and the moment is told.
        /// Then she goes about her business, and her needs, paused while she lay there, run again.
        /// </summary>
        [Test]
        public void ADownedColonistInABedGetsUpAtFifteenPerCent()
        {
            var colony = Board();
            colony.World.Tick(5);
            Pawn patient = colony.Pawns.Pawns.All[0], by = colony.Pawns.Pawns.All[1];
            Stand(colony, patient, colony.Pawns.Items.Beds[0]);
            Strike(colony, by, patient, patient.HpMilli);
            int completed = colony.Jobs.CompletedOf(JobIndex.Downed);
            var tape = new Tape();

            int recoverAt = patient.HpMaxMilli * colony.Pawns.Content.Combat.downedRecoverAtPerMille / 1_000;
            for (int t = 0; t < 2 * Day(colony) && patient.Downed; t++)
            {
                colony.World.Tick();
                if (patient.Downed) Assert.That(patient.HpMilli, Is.LessThan(recoverAt), "still down past the line");
            }
            tape.Read(colony);

            Assert.That(patient.Downed, Is.False, "she never got up");
            Assert.That(patient.HpMilli, Is.GreaterThanOrEqualTo(recoverAt));
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Downed), Is.EqualTo(completed + 1));
            Assert.That(tape.Of(CombatEventKind.Recovered).Count, Is.EqualTo(1));

            int food = patient.Needs[NeedIndex.Food];
            colony.World.Tick(2_000);
            Assert.That(patient.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.Downed));
            Assert.That(patient.Needs[NeedIndex.Food], Is.LessThan(food), "her needs stayed paused");
        }

        [Test]
        public void AnAnimalHealsWhereItLiesAndAMarauderNever()
        {
            var colony = Board();
            colony.World.Tick(5);
            Pawn by = colony.Pawns.Pawns.All[0];
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 10, 0));
            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, -10, 0));
            colony.World.Tick();

            Strike(colony, by, hog, hog.HpMilli + 1_000);
            Strike(colony, by, marauder, marauder.HpMilli + 1_000);
            Assert.That(hog.Downed && marauder.Downed, Is.True);

            colony.World.Tick(Day(colony));
            Assert.That(marauder.HpMilli, Is.EqualTo(-1_000), "a marauder healed");
            Assert.That(marauder.Downed, Is.True, "a marauder got up");
            Assert.That(hog.Downed, Is.False, "the hog never got up");
            Assert.That(hog.HpMilli, Is.GreaterThan(0));
        }

        /// <summary>A day's healing is the content's figure to the thousandth, whatever the interval does not divide.</summary>
        [Test]
        public void ADaysHealingIsExact()
        {
            var colony = Board();
            colony.World.Tick(5);
            Pawn by = colony.Pawns.Pawns.All[0];
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 10, 0));
            colony.World.Tick();
            Strike(colony, by, hog, 40_000);
            // Standing and hurt: it would run, which does not matter to the arithmetic; keep it still.
            hog.StunnedUntilTick = int.MaxValue;
            int start = hog.HpMilli;

            colony.World.Tick(Day(colony));
            Assert.That(hog.HpMilli - start, Is.EqualTo(colony.Pawns.Content.Combat.animalHealPerDay));
        }

        /// <summary>A whole pawn is not touched: nothing about healing reaches the hash of a colony nobody hurt.</summary>
        [Test]
        public void AWholeColonyCarriesNoCombatState()
        {
            var colony = Board();
            Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 10, 0));
            colony.World.Tick(3_000);
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                Assert.That(pawn.HasCombatState, Is.False, $"pawn {pawn.Id.Value}");
        }

        /// <summary>
        /// And a pawn over its fight carries none either: the stun and the swing clock run out to
        /// nought, so a colony that fought and healed hashes the way it would have had it never
        /// fought — the property that lets the combat lanes leave the goldens alone.
        /// </summary>
        [Test]
        public void TheClocksRunOutToNought()
        {
            var colony = Board();
            colony.World.Tick(5);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            pawn.StunnedUntilTick = colony.World.CurrentTick + 30;
            pawn.NextSwingTick = colony.World.CurrentTick + 40;
            Assert.That(pawn.HasCombatState, Is.True, "the control");
            colony.World.Tick(50);
            Assert.That(pawn.StunnedUntilTick, Is.EqualTo(0));
            Assert.That(pawn.NextSwingTick, Is.EqualTo(0));
            Assert.That(pawn.HasCombatState, Is.False);
        }
    }
}
