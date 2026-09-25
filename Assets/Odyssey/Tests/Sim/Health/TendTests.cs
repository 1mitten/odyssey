#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Tending (design 43 §5): the doctor finds the hurt, fetches a medkit when one can be reached,
    /// tends every injury at one quality — skill × potency, clamped — and every bleed stops. Each
    /// claim beside the colony that has no doctor, no kit, or only herself to tend.
    /// </summary>
    public class TendTests
    {
        const int LegLeft = 4, Torso = 1;

        static (ColonyWorld colony, Pawn patient, Pawn doctor) Pair(int beds = 0)
        {
            var colony = Board(colonists: 2, beds: beds);
            colony.World.Tick(5);
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                for (int w = 0; w < pawn.WorkPriorities.Length; w++) pawn.WorkPriorities[w] = 0;
            return (colony, colony.Pawns.Pawns.All[0], colony.Pawns.Pawns.All[1]);
        }

        static void Wound(ColonyWorld colony, Pawn patient, int milli, AfflictionKind kind = AfflictionKind.Wound, int region = LegLeft) =>
            colony.Pawns.Combat!.Hurt(patient, null, milli, kind, HitSet.Melee, -1, colony.World.CurrentTick, region);

        static void SetMedicine(Pawn pawn, int level) =>
            pawn.Skills[SkillIndex.Medicine] = pawn.Content.Skills[SkillIndex.Medicine].ExperienceForLevel(level);

        static bool TendedWithin(ColonyWorld colony, Pawn patient, int ticks)
        {
            for (int t = 0; t < ticks; t++)
            {
                colony.World.Tick();
                if (patient.Health != null && patient.Health.Count > 0 && patient.Health.UntendedCount == 0) return true;
            }
            return false;
        }

        [Test]
        public void ADoctorTendsTheHurtBareHandedAndTheBleedingStops()
        {
            var (colony, patient, doctor) = Pair();
            SetMedicine(doctor, 5);
            doctor.WorkPriorities[WorkTypeIndex.Doctor] = 1;
            Wound(colony, patient, 12_000);

            Assert.That(TendedWithin(colony, patient, 6_000), Is.True, "a doctor with nothing else to do never tended");
            Affliction record = patient.Health![0];
            HealthDef body = patient.Body!;
            Assert.That(record.TendQualityPerMille, Is.EqualTo(body.TendQualityPerMille(5, medkit: false)));
            Assert.That(record.TendQualityPerMille, Is.EqualTo(210), "level 5 bare-handed is 700 x 0.3");
            Assert.That(patient.Health.BleedingSeverityMilli, Is.EqualTo(0));
            Assert.That(doctor.Skills[SkillIndex.Medicine],
                Is.GreaterThan(doctor.Content.Skills[SkillIndex.Medicine].ExperienceForLevel(5)), "a tend trained nothing");

            var (c2, p2, d2) = Pair();
            Wound(c2, p2, 12_000);
            Assert.That(TendedWithin(c2, p2, 6_000), Is.False, "the control: with Doctor off, nobody tends");
        }

        [Test]
        public void AMedkitWithinReachIsFetchedAndSpentAndTendsBetter()
        {
            var (colony, patient, doctor) = Pair();
            SetMedicine(doctor, 5);
            doctor.WorkPriorities[WorkTypeIndex.Doctor] = 1;
            int shelf = Near(colony, 4, 0);
            ThingId kits = colony.Pawns.Items.Spawn(ItemIndex.Medkit, shelf, 3);
            Wound(colony, patient, 12_000);

            Assert.That(TendedWithin(colony, patient, 8_000), Is.True);
            Assert.That(colony.Pawns.Items.Get(kits)!.Stack, Is.EqualTo(2), "the kit was not spent, or more than one was");
            Assert.That(patient.Health![0].TendQualityPerMille, Is.EqualTo(700), "level 5 with a kit is 700 x 1.0");
        }

        [Test]
        public void TheQualityIsSkillTimesPotencyClamped()
        {
            HealthDef body = Odyssey.Sim.Defs.ContentPack.Pawns().HealthOf(PawnKindIndex.Colonist)!;
            Assert.That(body.TendQualityPerMille(0, false), Is.EqualTo(60), "a novice's bare hands");
            Assert.That(body.TendQualityPerMille(20, false), Is.EqualTo(465), "a master's bare hands: 1550 x 0.3");
            Assert.That(body.TendQualityPerMille(10, true), Is.EqualTo(1_000), "clamped at the kit's 100 per cent");
            Assert.That(body.TendQualityPerMille(0, true), Is.EqualTo(200));
        }

        [Test]
        public void TendSpeedIsTheDoctorWorkCurve()
        {
            WorkTypeDef doctor = Odyssey.Sim.Defs.ContentPack.Pawns().WorkTypes[WorkTypeIndex.Doctor];
            Assert.That(doctor.rateSkill, Is.EqualTo(SkillIndex.Medicine));
            Assert.That(doctor.WorkRatePerMille(0), Is.EqualTo(400));
            Assert.That(doctor.WorkRatePerMille(10), Is.EqualTo(1_000));
            Assert.That(doctor.WorkRatePerMille(20), Is.EqualTo(1_600));
        }

        [Test]
        public void NobodyTendsHerself()
        {
            var colony = Board(colonists: 1, beds: 0);
            colony.World.Tick(5);
            Pawn alone = colony.Pawns.Pawns.All[0];
            for (int w = 0; w < alone.WorkPriorities.Length; w++) alone.WorkPriorities[w] = 0;
            alone.WorkPriorities[WorkTypeIndex.Doctor] = 1;
            colony.Pawns.Combat!.Hurt(alone, null, 5_000, AfflictionKind.Wound, HitSet.Melee, -1, colony.World.CurrentTick, LegLeft);
            Assert.That(TendedWithin(colony, alone, 4_000), Is.False, "she tended herself");
        }

        [Test]
        public void TheOrderSendsADoctorWhateverHerPriority()
        {
            var (colony, patient, doctor) = Pair();
            Wound(colony, patient, 12_000);
            Assert.That(Send(colony, new Intent(IntentKind.OrderTend, default, doctor.Id.Value, patient.Id.Value)),
                Is.EqualTo(IntentRejection.None));
            Assert.That(doctor.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Tend));
            Assert.That(TendedWithin(colony, patient, 6_000), Is.True, "an ordered tend never finished");

            Assert.That(Send(colony, new Intent(IntentKind.OrderTend, default, doctor.Id.Value, patient.Id.Value)),
                Is.EqualTo(IntentRejection.NotPermitted), "the control: nothing left untended");
            Assert.That(Send(colony, new Intent(IntentKind.OrderTend, default, doctor.Id.Value, doctor.Id.Value)),
                Is.EqualTo(IntentRejection.NotPermitted), "nobody is sent to tend herself");
        }

        [Test]
        public void TheBleedingAreTendedFirst()
        {
            var colony = Board(colonists: 3, beds: 0);
            colony.World.Tick(5);
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                for (int w = 0; w < pawn.WorkPriorities.Length; w++) pawn.WorkPriorities[w] = 0;
            Pawn doctor = colony.Pawns.Pawns.All[0], bruised = colony.Pawns.Pawns.All[1], cut = colony.Pawns.Pawns.All[2];
            doctor.WorkPriorities[WorkTypeIndex.Doctor] = 1;
            Stand(colony, doctor, Near(colony, 0, 0));
            Stand(colony, bruised, Near(colony, 1, 0));
            Stand(colony, cut, Near(colony, 6, 0));
            colony.Pawns.Combat!.Hurt(bruised, null, 5_000, AfflictionKind.Bruise, HitSet.Melee, -1, colony.World.CurrentTick, Torso);
            colony.Pawns.Combat!.Hurt(cut, null, 5_000, AfflictionKind.Wound, HitSet.Melee, -1, colony.World.CurrentTick, LegLeft);

            for (int t = 0; t < 2_000 && doctor.CombatTarget == 0; t++) colony.World.Tick();
            Assert.That(doctor.CombatTarget, Is.EqualTo(cut.Id.Value), "the nearer bruise was tended before the cut bleeding further off");
        }
    }
}
