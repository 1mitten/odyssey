#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Tending (design 43 §5) as the end of a treatment (design 37), since the merge of the two on
    /// 2026-09-25 (design 43 §15): one doctor's job, <c>Job_Treat</c>, fetches medical supplies when
    /// any can be reached, and when the work is done tends every injury at one quality — skill ×
    /// potency, clamped — and every bleed stops. A bleeding colonist is a patient whatever her pool.
    /// Each claim beside the colony that has no doctor, no supplies, or only herself to treat.
    /// </summary>
    public class TendTests
    {
        const int LegLeft = 4, LegRight = 5, Torso = 1;
        const int Supplies = ItemIndex.MedicalSupplies;

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

        static int SuppliesOnBoard(ColonyWorld colony)
        {
            int total = 0;
            foreach (ColonyItem item in colony.Pawns.Items.Items)
                if (!item.Despawned && item.DefIndex == Supplies) total += item.Stack;
            return total;
        }

        /// <summary>
        /// A cut on a colonist at 88 % — above the treatment cap, so design 37 alone would leave her —
        /// bleeds her out in two days. She lies down for the doctor, the doctor comes, and the bleed
        /// stops: design 43 §15's "a bleeding colonist is a patient whatever her pool".
        /// </summary>
        [Test]
        public void ABleedingColonistAboveTheCapIsTreatedBareAndTheBleedingStops()
        {
            var (colony, patient, doctor) = Pair();
            SetMedicine(doctor, 5);
            doctor.WorkPriorities[WorkTypeIndex.Doctor] = 1;
            Wound(colony, patient, 12_000);
            Assume.That(Medical.Below(patient, colony.Pawns.Content.Combat.treatCapPerMille), Is.False);
            Assume.That(Medical.IsBleeding(patient), Is.True);

            Assert.That(TendedWithin(colony, patient, 6_000), Is.True, "a doctor with nothing else to do never tended");
            Affliction record = patient.Health![0];
            HealthDef body = patient.Body!;
            Assert.That(record.TendQualityPerMille, Is.EqualTo(body.TendQualityPerMille(5, supplies: false)));
            Assert.That(record.TendQualityPerMille, Is.EqualTo(210), "level 5 bare is 700 x 0.3");
            Assert.That(patient.Health.BleedingSeverityMilli, Is.EqualTo(0));
            Assert.That(doctor.Skills[SkillIndex.Medicine],
                Is.GreaterThan(doctor.Content.Skills[SkillIndex.Medicine].ExperienceForLevel(5)), "a treatment trained nothing");

            var (c2, p2, _) = Pair();
            Wound(c2, p2, 12_000);
            Assert.That(TendedWithin(c2, p2, 6_000), Is.False, "the control: with Doctor off and no supplies, nobody tends");
        }

        [Test]
        public void SuppliesWithinReachAreFetchedAndSpentAndTendBetter()
        {
            var (colony, patient, doctor) = Pair();
            SetMedicine(doctor, 5);
            doctor.WorkPriorities[WorkTypeIndex.Doctor] = 1;
            ThingId kits = colony.Pawns.Items.Spawn(Supplies, Near(colony, 4, 0), 3);
            Wound(colony, patient, 12_000);

            Assert.That(TendedWithin(colony, patient, 8_000), Is.True);
            Assert.That(SuppliesOnBoard(colony), Is.EqualTo(2), "the unit was not spent, or more than one was");
            Assert.That(colony.Pawns.Items.Get(kits)!.Stack, Is.EqualTo(2), "the rest of the pile stayed where it was");
            Assert.That(patient.Health![0].TendQualityPerMille, Is.EqualTo(700), "level 5 with supplies is 700 x 1.0");
        }

        [Test]
        public void TheQualityIsSkillTimesPotencyClamped()
        {
            HealthDef body = Odyssey.Sim.Defs.ContentPack.Pawns().HealthOf(PawnKindIndex.Colonist)!;
            Assert.That(body.TendQualityPerMille(0, false), Is.EqualTo(60), "a novice's bare dressing");
            Assert.That(body.TendQualityPerMille(20, false), Is.EqualTo(465), "a master's bare dressing: 1550 x 0.3");
            Assert.That(body.TendQualityPerMille(10, true), Is.EqualTo(1_000), "clamped at the supplies' 100 per cent");
            Assert.That(body.TendQualityPerMille(0, true), Is.EqualTo(200));
        }

        [Test]
        public void TreatmentSpeedIsTheDoctorWorkCurve()
        {
            WorkTypeDef doctor = Odyssey.Sim.Defs.ContentPack.Pawns().WorkTypes[WorkTypeIndex.Doctor];
            Assert.That(doctor.rateSkill, Is.EqualTo(SkillIndex.Medicine), "the one owner of how fast a doctor works");
        }

        /// <summary>
        /// Treating herself (design 37 §4), now a tend too: the lone colonist bleeding with supplies
        /// in reach stops her own bleed. With none, she cannot — design 37's rule, kept.
        /// </summary>
        [Test]
        public void TheLoneColonistStopsHerOwnBleedWithSuppliesAndNotWithout()
        {
            var colony = Board(colonists: 1, beds: 0);
            colony.World.Tick(5);
            Pawn alone = colony.Pawns.Pawns.All[0];
            for (int w = 0; w < alone.WorkPriorities.Length; w++) alone.WorkPriorities[w] = 0;
            colony.Pawns.Items.Spawn(Supplies, Near(colony, 2, 2), 3);
            Wound(colony, alone, 5_000);

            Assert.That(TendedWithin(colony, alone, 8_000), Is.True, "she never treated her own cut");
            Assert.That(alone.Health!.BleedingSeverityMilli, Is.EqualTo(0));
            Assert.That(SuppliesOnBoard(colony), Is.EqualTo(2));

            var bare = Board(colonists: 1, beds: 0);
            bare.World.Tick(5);
            Pawn other = bare.Pawns.Pawns.All[0];
            Wound(bare, other, 5_000);
            Assert.That(TendedWithin(bare, other, 4_000), Is.False, "the control: no supplies, no self-treatment");
        }

        [Test]
        public void TheOrderSendsADoctorWhateverHerPriority()
        {
            var (colony, patient, doctor) = Pair();
            Wound(colony, patient, 12_000);
            Assert.That(Send(colony, new Intent(IntentKind.OrderTend, default, doctor.Id.Value, patient.Id.Value)),
                Is.EqualTo(IntentRejection.None));
            Assert.That(doctor.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Treat));
            Assert.That(TendedWithin(colony, patient, 6_000), Is.True, "an ordered treatment never finished");

            Assert.That(Send(colony, new Intent(IntentKind.OrderTend, default, doctor.Id.Value, patient.Id.Value)),
                Is.EqualTo(IntentRejection.NotPermitted), "the control: nothing untended and above the cap");
            Assert.That(Send(colony, new Intent(IntentKind.OrderTend, default, doctor.Id.Value, doctor.Id.Value)),
                Is.EqualTo(IntentRejection.NotPermitted), "nobody is sent to treat herself");
        }

        /// <summary>
        /// A drafted colonist standing her ground is not lying still, so the doctor's round leaves
        /// her; an order reaches her where she stands and the bleed stops.
        /// </summary>
        [Test]
        public void AnOrderedTreatmentReachesAColonistOnHerFeet()
        {
            var (colony, patient, doctor) = Pair();
            Assume.That(Draft(colony, patient), Is.EqualTo(IntentRejection.None));
            Wound(colony, patient, 12_000);
            Assert.That(Send(colony, new Intent(IntentKind.OrderTend, default, doctor.Id.Value, patient.Id.Value)),
                Is.EqualTo(IntentRejection.None));
            Assert.That(TendedWithin(colony, patient, 6_000), Is.True, "the doctor never finished on a patient standing");
            Assert.That(patient.Health!.BleedingSeverityMilli, Is.EqualTo(0));
        }

        [Test]
        public void TheBleedingAreTreatedFirst()
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
            // Both down where they stand, so both are waiting for a doctor; only one is bleeding.
            DownWithoutAVitalAtNought(colony, bruised, AfflictionKind.Bruise);
            DownWithoutAVitalAtNought(colony, cut, AfflictionKind.Wound);
            Assume.That(bruised.Downed && cut.Downed, Is.True);

            var job = new Job();
            Assert.That(new DoctorWorkGiver().TryGiveJob(doctor, colony.Pawns, job), Is.True);
            Assert.That(job.WorkTicks, Is.EqualTo(cut.Id.Value), "the nearer bruise was taken before the cut bleeding further off");
        }

        /// <summary>
        /// Inside the cooldown a second cut is still treated — it bleeds — but the treatment tends
        /// and heals nothing: design 37's "a stack of supplies is not a substitute for rest", kept.
        /// </summary>
        [Test]
        public void ATreatmentInsideTheCooldownTendsAndDoesNotHeal()
        {
            var (colony, patient, doctor) = Pair();
            Wound(colony, patient, 30_000);
            patient.TreatedUntilTick = colony.World.CurrentTick + 50_000;
            Assume.That(Medical.NeedsTreatment(patient, colony.Pawns), Is.True, "bleeding outranks the cooldown");
            int before = patient.HpMilli;

            Assert.That(Send(colony, new Intent(IntentKind.OrderTend, default, doctor.Id.Value, patient.Id.Value)),
                Is.EqualTo(IntentRejection.None));
            Assert.That(TendedWithin(colony, patient, 6_000), Is.True);
            Assert.That(patient.HpMilli, Is.LessThan(before + 1_000),
                "the treatment healed the pool inside the cooldown (a tended injury's own 4-12 a day is all it may gain)");
        }

        /// <summary>
        /// The regression the merge found in design 37's driver: a downed patient stood up at 15 %
        /// a third of the way through her own treatment, stopped lying still, and the doctor's job
        /// failed — the unit back on the floor unused and no cooldown set, a free heal each time.
        /// Now she gets up when it ends: the unit is spent, the cooldown set, the heal whole.
        /// </summary>
        [Test]
        public void ADownedPatientGetsUpWhenHerTreatmentEndsAndTheUnitIsSpent()
        {
            var (colony, patient, doctor) = Pair();
            doctor.WorkPriorities[WorkTypeIndex.Doctor] = 1;
            Stand(colony, patient, Near(colony, 6, 6));
            colony.Pawns.Items.Spawn(Supplies, Near(colony, -3, -3), 6);
            DownWithoutAVitalAtNought(colony, patient, AfflictionKind.Bruise);
            Assume.That(patient.Downed, Is.True);
            int downAt = patient.HpMilli;

            for (int t = 0; t < 8_000 && patient.TreatedUntilTick == 0; t++)
            {
                colony.World.Tick();
                if (patient.TreatedUntilTick == 0)
                    Assert.That(patient.Downed, Is.True, "she got up before her treatment ended");
            }

            Assert.That(patient.TreatedUntilTick, Is.Not.Zero, "nobody treated her");
            Assert.That(SuppliesOnBoard(colony), Is.EqualTo(5), "the unit went back on the floor");
            int heal = colony.Pawns.Content.Items[Supplies].healPerUnit * Odyssey.Sim.Rates.Scale;
            Assert.That(patient.HpMilli, Is.InRange(downAt + heal, downAt + heal + 1_000), "one unit's heal, whole");
            Assert.That(patient.Health!.UntendedCount, Is.Zero);

            colony.World.Tick();
            Assert.That(patient.Downed, Is.False, "past the line and nothing on the body keeping her down");
        }

        /// <summary>
        /// Seventy points in three blows — both legs and the torso — downs her by pain and her legs
        /// without putting a vital region at nought, so a treatment's forty can stand her up.
        /// </summary>
        static void DownWithoutAVitalAtNought(ColonyWorld colony, Pawn pawn, AfflictionKind kind)
        {
            Wound(colony, pawn, 25_000, kind, LegLeft);
            Wound(colony, pawn, 25_000, kind, LegRight);
            Wound(colony, pawn, 20_000, kind, Torso);
        }
    }
}
