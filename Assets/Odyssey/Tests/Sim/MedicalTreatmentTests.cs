#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Treatment (design 37 §4, MD2 and MD3): a doctor treats a patient lying still with one unit
    /// of supplies, or a bare dressing with none; the badly hurt go to bed; and a colonist with
    /// nobody to come treats herself, for less and more slowly. Every number is the content's —
    /// +40 a unit, never past 80 %; +20 and 60 % for yourself at three times the work; +10 bare —
    /// read from the Defs rather than written here, so a retune moves the Defs and not the tests.
    /// </summary>
    public class MedicalTreatmentTests
    {
        const int Medical = ItemIndex.MedicalSupplies;

        static int Heal(ColonyWorld colony) => colony.Pawns.Content.Items[Medical].healPerUnit * Rates.Scale;

        static int Pool(Pawn pawn, int perMille) => pawn.HpMaxMilli * perMille / 1_000;

        static int SuppliesOnBoard(ColonyWorld colony)
        {
            int total = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
                if (!items[i].Despawned && items[i].DefIndex == Medical) total += items[i].Stack;
            return total;
        }

        /// <summary>Tick until she has been treated, or the budget runs out. Returns the ticks spent.</summary>
        static int UntilTreated(ColonyWorld colony, Pawn patient, int budget = 6_000)
        {
            for (int t = 0; t < budget; t++)
            {
                if (patient.TreatedUntilTick != 0) return t;
                colony.World.Tick();
            }
            return budget;
        }

        static Pawn DownOnTheGround(ColonyWorld colony, Pawn patient, Pawn by, int dx = 8, int dz = 8)
        {
            Stand(colony, patient, Near(colony, dx, dz));
            Assume.That(Odyssey.Sim.Pawns.Medical.IsBedCell(colony.Pawns, patient.Cell), Is.False);
            Strike(colony, by, patient, patient.HpMilli);
            Assume.That(patient.Downed, Is.True);
            Assume.That(patient.HpMilli, Is.EqualTo(0));
            return patient;
        }

        // ---- the arithmetic ----------------------------------------------------------------

        [Test]
        public void TheHealIsFortyNeverPastTheCapAndNeverDown()
        {
            var colony = Board(colonists: 1);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            var ctx = colony.Pawns;
            var combat = ctx.Content.Combat;

            pawn.HpMilli = Pool(pawn, 100);
            Assert.That(Odyssey.Sim.Pawns.Medical.HealedMilli(pawn, ctx, Medical, self: false),
                Is.EqualTo(Pool(pawn, 100) + Heal(colony)), "a unit of supplies is the item's healPerUnit");

            pawn.HpMilli = Pool(pawn, 700);
            Assert.That(Odyssey.Sim.Pawns.Medical.HealedMilli(pawn, ctx, Medical, self: false),
                Is.EqualTo(Pool(pawn, combat.treatCapPerMille)), "70 goes to 80, not 110");

            pawn.HpMilli = Pool(pawn, 900);
            Assert.That(Odyssey.Sim.Pawns.Medical.HealedMilli(pawn, ctx, Medical, self: false),
                Is.EqualTo(Pool(pawn, 900)), "a treatment above the cap never takes anything away");

            pawn.HpMilli = 0;
            Assert.That(Odyssey.Sim.Pawns.Medical.HealedMilli(pawn, ctx, -1, self: false),
                Is.EqualTo(combat.bareHeal * Rates.Scale), "a bare dressing is bareHeal");

            pawn.HpMilli = Pool(pawn, 100);
            Assert.That(Odyssey.Sim.Pawns.Medical.HealedMilli(pawn, ctx, Medical, self: true),
                Is.EqualTo(Pool(pawn, 100) + Heal(colony) * combat.selfHealPerMille / 1_000),
                "treating yourself heals half");

            pawn.HpMilli = Pool(pawn, 500);
            Assert.That(Odyssey.Sim.Pawns.Medical.HealedMilli(pawn, ctx, Medical, self: true),
                Is.EqualTo(Pool(pawn, combat.selfCapPerMille)), "and never past 60 per cent");
        }

        [Test]
        public void SplittingOneOffAStackLeavesTheRestWhereItLies()
        {
            var colony = Board(colonists: 1);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            int cell = Near(colony, 3, 3);
            ThingId id = colony.Pawns.Items.Spawn(Medical, cell, 6);
            ColonyItem pile = colony.Pawns.Items.Get(id)!;

            ColonyItem taken = colony.Pawns.Items.SplitOff(pile, 1, pawn.Id);

            Assert.That(taken, Is.Not.SameAs(pile));
            Assert.That(taken.Stack, Is.EqualTo(1));
            Assert.That(taken.Cell, Is.EqualTo(-1), "the unit is in her hands");
            Assert.That(taken.CarriedBy, Is.EqualTo(pawn.Id.Value));
            Assert.That(pile.Stack, Is.EqualTo(5));
            Assert.That(colony.Pawns.Items.ItemAt(cell), Is.SameAs(pile), "the rest has not moved");

            ColonyItem last = colony.Pawns.Items.SplitOff(pile, 5, pawn.Id);
            Assert.That(last, Is.SameAs(pile), "taking the whole stack is a pick-up, not a copy");
            Assert.That(colony.Pawns.Items.ItemAt(cell), Is.Null);
        }

        // ---- the doctor (MD2) --------------------------------------------------------------

        [Test]
        public void ADownedColonistIsTreatedWhereSheLiesAndGetsUp()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn patient = colony.Pawns.Pawns.All[0], doctor = colony.Pawns.Pawns.All[1];
            DownOnTheGround(colony, patient, doctor);
            colony.Pawns.Items.Spawn(Medical, Near(colony, -3, -3), 6);

            int spent = UntilTreated(colony, patient);
            Assert.That(patient.TreatedUntilTick, Is.Not.Zero, $"nobody treated her in {spent} ticks");

            Assert.That(patient.HpMilli, Is.EqualTo(Heal(colony)), "0 plus one unit, on the ground where no bed heals");
            Assert.That(SuppliesOnBoard(colony), Is.EqualTo(5), "one unit used and the rest left where it lay");
            Assert.That(patient.TreatedUntilTick,
                Is.EqualTo(colony.World.CurrentTick - 1 + colony.Pawns.Content.Combat.treatedCooldownTicks).Within(1));

            colony.World.Tick();
            Assert.That(patient.Downed, Is.False, "40 is past the 15 per cent line, so she is up");
        }

        [Test]
        public void WithNoSuppliesTheDoctorDressesTheWoundForLess()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn patient = colony.Pawns.Pawns.All[0], doctor = colony.Pawns.Pawns.All[1];
            DownOnTheGround(colony, patient, doctor);
            Assume.That(SuppliesOnBoard(colony), Is.Zero);

            UntilTreated(colony, patient);

            Assert.That(patient.HpMilli, Is.EqualTo(colony.Pawns.Content.Combat.bareHeal * Rates.Scale));
            colony.World.Tick();
            Assert.That(patient.Downed, Is.True, "ten is not enough to stand on");
        }

        [Test]
        public void TheCooldownRefusesASecondTreatment()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn patient = colony.Pawns.Pawns.All[0], doctor = colony.Pawns.Pawns.All[1];
            DownOnTheGround(colony, patient, doctor);
            colony.Pawns.Items.Spawn(Medical, Near(colony, -3, -3), 6);
            UntilTreated(colony, patient);
            Assume.That(patient.TreatedUntilTick, Is.Not.Zero);

            Assert.That(Odyssey.Sim.Pawns.Medical.NeedsTreatment(patient, colony.Pawns), Is.False);
            var job = new Job();
            Assert.That(new DoctorWorkGiver().TryGiveJob(doctor, colony.Pawns, job), Is.False,
                "a pile of supplies is not a substitute for rest");

            int hp = patient.HpMilli;
            colony.World.Tick(2_000);
            Assert.That(SuppliesOnBoard(colony), Is.EqualTo(5), "no second unit went on her");
            Assert.That(patient.HpMilli, Is.LessThan(hp + Heal(colony)), "no second treatment landed");
        }

        [Test]
        public void APatientNobodyCanReachGivesNoJob()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn patient = colony.Pawns.Pawns.All[0], doctor = colony.Pawns.Pawns.All[1];
            DownOnTheGround(colony, patient, doctor);

            // In the air three layers up: nowhere a path can end.
            CellRef at = Size.FromIndex(patient.Cell);
            patient.Cell = Size.Index(at.X, at.Z, at.Y + 3);
            Assume.That(colony.Pawns.Reachable(doctor, patient.Cell), Is.False);

            Assert.That(new DoctorWorkGiver().TryGiveJob(doctor, colony.Pawns, new Job()), Is.False,
                "a giver that offers the unreachable costs a failed job a think (the #119 lesson)");
        }

        [Test]
        public void ADoctorWithTheColumnOffTreatsNobody()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn patient = colony.Pawns.Pawns.All[0], doctor = colony.Pawns.Pawns.All[1];
            doctor.WorkPriorities[WorkTypeIndex.Doctor] = 0;
            DownOnTheGround(colony, patient, doctor);
            colony.Pawns.Items.Spawn(Medical, Near(colony, -3, -3), 6);

            colony.World.Tick(3_000);
            Assert.That(patient.TreatedUntilTick, Is.Zero);
            Assert.That(patient.HpMilli, Is.Zero);
        }

        // ---- the patient (MD3) -------------------------------------------------------------

        [Test]
        public void ABadlyHurtColonistGoesToBed()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn patient = colony.Pawns.Pawns.All[0];
            patient.HpMilli = Pool(patient, 400);

            for (int t = 0; t < 3_000 && !(Odyssey.Sim.Pawns.Medical.IsPatient(patient) && patient.Asleep); t++)
                colony.World.Tick();

            Assert.That(Odyssey.Sim.Pawns.Medical.IsPatient(patient), Is.True, "she did not lie down as a patient");
            Assert.That(Odyssey.Sim.Pawns.Medical.IsBedCell(colony.Pawns, patient.Cell), Is.True, "and not in a bed");
            Assert.That(patient.Asleep, Is.True);
        }

        [Test]
        public void ALightlyHurtColonistKeepsWorking()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            pawn.HpMilli = Pool(pawn, 700);

            for (int t = 0; t < 2_000; t++)
            {
                colony.World.Tick();
                Assert.That(Odyssey.Sim.Pawns.Medical.IsPatient(pawn), Is.False, "70 is not bad enough for a bed");
            }
        }

        [Test]
        public void WithADoctorToComeNobodyTreatsHerself()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn patient = colony.Pawns.Pawns.All[0];
            patient.HpMilli = Pool(patient, 300);
            colony.Pawns.Items.Spawn(Medical, Near(colony, -3, -3), 6);

            UntilTreated(colony, patient, budget: 8_000);
            Assert.That(patient.TreatedUntilTick, Is.Not.Zero);

            // The doctor's +40, give or take the little the bed has healed on the way; not 50, the self-treatment's.
            Assert.That(patient.HpMilli, Is.InRange(Pool(patient, 300) + Heal(colony), Pool(patient, 300) + Heal(colony) + 2_000));
        }

        [Test]
        public void TheLoneColonistTreatsHerselfForLessAndThreeTimesAsLong()
        {
            var colony = Board(colonists: 1);
            colony.World.Tick(5);
            Pawn pawn = colony.Pawns.Pawns.All[0];
            pawn.HpMilli = Pool(pawn, 300);
            colony.Pawns.Items.Spawn(Medical, Near(colony, 2, 2), 6);
            var combat = colony.Pawns.Content.Combat;

            int treating = 0;
            for (int t = 0; t < 12_000 && pawn.TreatedUntilTick == 0; t++)
            {
                colony.World.Tick();
                if (pawn.Driver is TreatJobDriver driver && driver.ToilIndex == 3) treating++;
            }

            Assert.That(pawn.TreatedUntilTick, Is.Not.Zero, "she never treated herself");
            Assert.That(pawn.HpMilli, Is.EqualTo(Pool(pawn, 300) + Heal(colony) * combat.selfHealPerMille / 1_000),
                "30 plus half a unit");
            Assert.That(SuppliesOnBoard(colony), Is.EqualTo(5));

            int work = colony.Pawns.Content.Jobs[JobIndex.Treat].workTicks * combat.selfWorkFactor;
            int rate = pawn.WorkRatePerMille(WorkTypeIndex.Doctor);
            int expected = (work * Rates.Scale + rate - 1) / rate;
            Assert.That(treating, Is.EqualTo(expected).Within(1),
                $"three times the {colony.Pawns.Content.Jobs[JobIndex.Treat].workTicks} ticks, at her pace");
        }

        [Test]
        public void ADownedColonistNeverTreatsHerself()
        {
            var colony = Board(colonists: 2);
            colony.World.Tick(5);
            Pawn patient = colony.Pawns.Pawns.All[0], other = colony.Pawns.Pawns.All[1];
            other.WorkPriorities[WorkTypeIndex.Doctor] = 0;
            DownOnTheGround(colony, patient, other);
            colony.Pawns.Items.Spawn(Medical, Near(colony, 9, 9), 6);

            colony.World.Tick(4_000);
            Assert.That(patient.TreatedUntilTick, Is.Zero);
            Assert.That(SuppliesOnBoard(colony), Is.EqualTo(6));
        }
    }
}
