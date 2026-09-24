#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Rescue (design 33 §11): a downed colonist carried to her own bed or the nearest free one,
    /// laid in it, and kept there until whole; never two rescuers for one patient; nobody left in
    /// a pair of arms; and no rescue at all without a bed, with the colonist saying so.
    /// </summary>
    public class RescueTests
    {
        static ulong Hash(ColonyWorld colony) => colony.World.ComputeStateHash().Value;

        static IntentRejection Rescue(ColonyWorld colony, Pawn rescuer, Pawn patient) =>
            Send(colony, new Intent(IntentKind.OrderRescue, Size.FromIndex(patient.Cell), rescuer.Id.Value, patient.Id.Value));

        /// <summary>Down a colonist where she stands: a blow that takes her just past nought.</summary>
        static void Down(ColonyWorld colony, Pawn patient, Pawn by)
        {
            Strike(colony, by, patient, patient.HpMilli + 1_000);
            Assume.That(patient.Downed, Is.True);
        }

        static bool InABed(ColonyWorld colony, Pawn pawn) => colony.Pawns.Items.Beds.Contains(pawn.Cell);

        static bool Holds(ColonyWorld colony, Pawn pawn, int cell) =>
            colony.Pawns.Reservations.IsReservedBy(pawn.Id, ReservationManager.Key(ReservationTargetKind.Cell, cell));

        /// <summary>Tick until the rescue is over: the patient in a bed, or the rescuer off the job.</summary>
        static void RunTheRescue(ColonyWorld colony, Pawn rescuer, int ticks = 8_000)
        {
            for (int t = 0; t < ticks && rescuer.CurrentJob?.DefIndex == JobIndex.Rescue; t++)
                colony.World.Tick();
        }

        static Board3 Three(int colonists = 3, int beds = -1)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = colonists;
            scenario.beds = beds < 0 ? colonists : beds;
            var colony = ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
            colony.World.Tick(5);
            var pawns = colony.Pawns.Pawns.All;
            // Nobody rescues on their own unless a test says so.
            foreach (Pawn pawn in pawns) pawn.WorkPriorities[WorkTypeIndex.Rescue] = 0;
            return new Board3(colony, pawns[0], pawns[1], pawns[2]);
        }

        readonly struct Board3
        {
            public Board3(ColonyWorld colony, Pawn rescuer, Pawn patient, Pawn by)
            {
                Colony = colony;
                Rescuer = rescuer;
                Patient = patient;
                By = by;
            }

            public readonly ColonyWorld Colony;
            public readonly Pawn Rescuer, Patient, By;

            public void Deconstruct(out ColonyWorld colony, out Pawn rescuer, out Pawn patient, out Pawn by)
            {
                colony = Colony;
                rescuer = Rescuer;
                patient = Patient;
                by = By;
            }
        }

        // ---- the order --------------------------------------------------------------------------

        [Test]
        public void AnOrderedRescueCarriesHerToAFreeBedAndLaysHerInIt()
        {
            var (colony, rescuer, patient, by) = Three();
            Stand(colony, patient, Near(colony, 8, 4));
            Down(colony, patient, by);
            Assert.That(Draft(colony, rescuer), Is.EqualTo(IntentRejection.None));
            int completed = colony.Jobs.CompletedOf(JobIndex.Rescue);

            Assert.That(Rescue(colony, rescuer, patient), Is.EqualTo(IntentRejection.None));
            Assert.That(rescuer.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Rescue));
            Assert.That(rescuer.CombatTarget, Is.EqualTo(patient.Id.Value));

            bool carried = false;
            for (int t = 0; t < 3_000 && rescuer.CurrentJob?.DefIndex == JobIndex.Rescue; t++)
            {
                colony.World.Tick();
                if (patient.CarriedBy == 0) continue;
                carried = true;
                Assert.That(patient.CarriedBy, Is.EqualTo(rescuer.Id.Value));
                Assert.That(patient.Cell, Is.EqualTo(rescuer.Cell), $"tick {t}: a carried patient is where her carrier is");
                Assert.That(patient.HasPath, Is.False, "and walks nowhere herself");
            }

            Assert.That(carried, Is.True, "she was never lifted");
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Rescue), Is.EqualTo(completed + 1));
            Assert.That(InABed(colony, patient), Is.True, "she was not laid on a bed's head cell");
            Assert.That(patient.CarriedBy, Is.Zero);
            Assert.That(patient.Downed, Is.True, "being carried is not being healed");
            Assert.That(rescuer.CombatTarget, Is.Zero, "the order's target outlived the order");
            Assert.That(Holds(colony, patient, patient.Cell), Is.True, "the bed did not pass to her");
            Assert.That(Holds(colony, rescuer, patient.Cell), Is.False, "and the rescuer kept it");
            Assert.That(rescuer.Drafted, Is.True, "a rescue does not end the draft");
        }

        [Test]
        public void TheOrderIsRefusedForAnythingButADraftedColonistAndADownedOne()
        {
            var (colony, rescuer, patient, by) = Three(colonists: 4);
            Pawn other = colony.Pawns.Pawns.All[3];
            Stand(colony, patient, Near(colony, 8, 4));
            Down(colony, patient, by);

            Assert.That(Rescue(colony, rescuer, patient), Is.EqualTo(IntentRejection.NotPermitted), "an undrafted rescuer");
            Draft(colony, rescuer);
            Assert.That(Rescue(colony, rescuer, other), Is.EqualTo(IntentRejection.NotPermitted), "a patient standing up");
            Assert.That(Rescue(colony, rescuer, rescuer), Is.EqualTo(IntentRejection.NotPermitted), "herself");

            Pawn marauder = Spawn(colony, PawnKindIndex.Marauder, Near(colony, -8, 4));
            Strike(colony, by, marauder, marauder.HpMilli + 1_000);
            Assume.That(marauder.Downed, Is.True);
            Assert.That(Rescue(colony, rescuer, marauder), Is.EqualTo(IntentRejection.NotPermitted), "a downed marauder");

            Assert.That(Rescue(colony, rescuer, patient), Is.EqualTo(IntentRejection.None), "the control");
            Assert.That(Rescue(colony, rescuer, patient), Is.EqualTo(IntentRejection.AlreadyInThatState));

            Draft(colony, other);
            Assert.That(Rescue(colony, other, patient), Is.EqualTo(IntentRejection.NotPermitted), "a second rescuer for one patient");
        }

        // ---- which bed ----------------------------------------------------------------------------

        [Test]
        public void HerOwnBedComesFirstAndSomebodyElsesNever()
        {
            var (colony, rescuer, patient, by) = Three(colonists: 3, beds: 3);
            var beds = colony.Pawns.Items.Beds;
            Stand(colony, patient, Near(colony, 8, 4));
            // Her bed is the one furthest from where she fell; the nearest is somebody else's.
            int[] byDistance = beds.OrderBy(b => colony.Pawns.Distance(patient.Cell, b)).ToArray();
            Assert.That(colony.Pawns.Construction!.AssignOwnerAt(byDistance[2], patient.Id.Value), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Pawns.Construction.AssignOwnerAt(byDistance[0], by.Id.Value), Is.EqualTo(IntentRejection.None));

            Down(colony, patient, by);
            Draft(colony, rescuer);
            Assert.That(Rescue(colony, rescuer, patient), Is.EqualTo(IntentRejection.None));
            RunTheRescue(colony, rescuer);
            Assert.That(patient.Cell, Is.EqualTo(byDistance[2]), "not her own bed");

            // Without her own, the nearest unowned: never the nearest, which is taken.
            var (colony2, rescuer2, patient2, by2) = Three(colonists: 3, beds: 3);
            var beds2 = colony2.Pawns.Items.Beds;
            Stand(colony2, patient2, Near(colony2, 8, 4));
            int[] near2 = beds2.OrderBy(b => colony2.Pawns.Distance(patient2.Cell, b)).ToArray();
            colony2.Pawns.Construction!.AssignOwnerAt(near2[0], by2.Id.Value);
            Down(colony2, patient2, by2);
            Draft(colony2, rescuer2);
            Assert.That(Rescue(colony2, rescuer2, patient2), Is.EqualTo(IntentRejection.None));
            RunTheRescue(colony2, rescuer2);
            Assert.That(patient2.Cell, Is.EqualTo(near2[1]), "the nearest bed nobody owns");
        }

        [Test]
        public void NoFreeBedMeansNoRescueAndTheColonistSaysSo()
        {
            // Three colonists and two beds, both owned by the two standing.
            var (colony, rescuer, patient, by) = Three(colonists: 3, beds: 2);
            var beds = colony.Pawns.Items.Beds;
            colony.Pawns.Construction!.AssignOwnerAt(beds[0], rescuer.Id.Value);
            colony.Pawns.Construction.AssignOwnerAt(beds[1], by.Id.Value);
            Stand(colony, patient, Near(colony, 8, 4));
            Down(colony, patient, by);
            Draft(colony, rescuer);

            Assert.That(Rescue(colony, rescuer, patient), Is.EqualTo(IntentRejection.NotPermitted), "a rescue to nowhere");
            Assert.That(NoBed(colony, patient), Is.True, "nothing says why she lies there");

            Draft(colony, rescuer, on: false);
            by.WorkPriorities[WorkTypeIndex.Rescue] = 1;
            colony.World.Tick(600);
            Assert.That(by.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.Rescue), "the giver sent somebody to a bed that is not there");
            Assert.That(patient.CarriedBy, Is.Zero);

            // A bed frees: the alert goes and the rescue comes.
            colony.Pawns.Construction.ReleaseBedsOf(by.Id.Value);
            colony.World.Tick();
            Assert.That(NoBed(colony, patient), Is.False, "the alert outlived its cause");
            for (int t = 0; t < 12_000 && !InABed(colony, patient); t++) colony.World.Tick();
            Assert.That(InABed(colony, patient), Is.True, "the freed bed was never used");
        }

        static bool NoBed(ColonyWorld colony, Pawn pawn) =>
            colony.World.Views.Current.TryGetPawnAspect(pawn.Id, CombatAspects.RescueNoBed, out _);

        // ---- the automatic rescue -----------------------------------------------------------------

        [Test]
        public void TheAutomaticRescueFetchesEveryoneAndNeverTwoForOne()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 5;
            scenario.beds = 5;
            var colony = ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
            colony.World.Tick(5);
            var all = colony.Pawns.Pawns.All;
            Pawn a = all[0], b = all[1], hurtA = all[2], hurtB = all[3], by = all[4];
            foreach (Pawn pawn in all) pawn.WorkPriorities[WorkTypeIndex.Rescue] = 0;
            a.WorkPriorities[WorkTypeIndex.Rescue] = 1;
            b.WorkPriorities[WorkTypeIndex.Rescue] = 1;
            Stand(colony, hurtA, Near(colony, 8, 4));
            Stand(colony, hurtB, Near(colony, -8, 4));
            Down(colony, hurtA, by);
            Down(colony, hurtB, by);

            for (int t = 0; t < 12_000 && !(InABed(colony, hurtA) && InABed(colony, hurtB)); t++)
            {
                colony.World.Tick();
                if (a.CurrentJob?.DefIndex == JobIndex.Rescue && b.CurrentJob?.DefIndex == JobIndex.Rescue)
                    Assert.That(a.CombatTarget, Is.Not.EqualTo(b.CombatTarget), $"tick {t}: two rescuers for one patient");
            }

            Assert.That(InABed(colony, hurtA) && InABed(colony, hurtB), Is.True, "somebody was left where she fell");
            Assert.That(hurtA.Cell, Is.Not.EqualTo(hurtB.Cell), "two patients in one bed");
        }

        [Test]
        public void TheGiverTouchesNothingWhenNobodyIsDown()
        {
            var (colony, rescuer, _, _) = Three();
            WorkGiver giver = colony.Jobs.Givers.Single(g => g.Name == "Rescue");
            ulong before = Hash(colony);
            Assert.That(giver.TryGiveJob(rescuer, colony.Pawns, new Job()), Is.False);
            Assert.That(Hash(colony), Is.EqualTo(before), "an idle rescue scan changed the world");
        }

        // ---- the bed is hers --------------------------------------------------------------------

        [Test]
        public void SheStaysInTheBedUntilWholeAndNobodyElseLiesInIt()
        {
            var (colony, rescuer, patient, by) = Three(colonists: 3, beds: 3);
            Stand(colony, patient, Near(colony, 8, 4));
            Down(colony, patient, by);
            Draft(colony, rescuer);
            Rescue(colony, rescuer, patient);
            RunTheRescue(colony, rescuer);
            Assume.That(InABed(colony, patient), Is.True);
            int bed = patient.Cell;

            // Everyone else exhausted: none of them may choose her bed.
            Draft(colony, rescuer, on: false);
            rescuer.Needs[NeedIndex.Rest] = 0;
            by.Needs[NeedIndex.Rest] = 0;

            int fifteen = patient.HpMaxMilli * colony.Pawns.Content.Combat.downedRecoverAtPerMille / 1_000;
            bool passedFifteen = false;
            int day = colony.Pawns.Content.DayTicks;
            for (int t = 0; t < 8 * day && patient.Downed; t++)
            {
                colony.World.Tick();
                if (patient.HpMilli >= fifteen && patient.Downed) passedFifteen = true;
                foreach (Pawn other in new[] { rescuer, by })
                    Assert.That(other.Cell == bed && other.Asleep, Is.False, $"tick {t}: somebody slept in her bed");
            }

            Assert.That(passedFifteen, Is.True, "she got up at fifteen per cent");
            Assert.That(patient.Downed, Is.False, "she never got up");
            Assert.That(patient.HpMilli, Is.EqualTo(patient.HpMaxMilli), "up before she was whole");
            Assert.That(Holds(colony, patient, bed), Is.False, "she kept the bed after getting up");
        }

        // ---- nobody left in a pair of arms --------------------------------------------------------

        [TestCase("released")]
        [TestCase("downed")]
        [TestCase("interrupted")]
        public void AnyEndPutsHerDown(string how)
        {
            var (colony, rescuer, patient, by) = Three();
            Stand(colony, patient, Near(colony, 12, 6));
            Down(colony, patient, by);
            Draft(colony, rescuer);
            Rescue(colony, rescuer, patient);
            for (int t = 0; t < 2_000 && patient.CarriedBy == 0; t++) colony.World.Tick();
            Assume.That(patient.CarriedBy, Is.EqualTo(rescuer.Id.Value), "never lifted");
            colony.World.Tick(20);
            Assume.That(patient.CarriedBy, Is.Not.Zero);

            switch (how)
            {
                case "released": Draft(colony, rescuer, on: false); break;
                case "downed": Strike(colony, by, rescuer, rescuer.HpMilli + 1_000); break;
                case "interrupted": colony.Jobs.EndJob(rescuer, JobStatus.Failed); break;
            }
            colony.World.Tick();

            Assert.That(patient.CarriedBy, Is.Zero, "left in the arms of somebody no longer carrying her");
            Assert.That(rescuer.CombatTarget, Is.Zero);
            Assert.That(colony.Pawns.Nav.Grid.CanEnter(patient.Cell, TraverseMode.Colonist), Is.True,
                "put down somewhere nobody can lie");
            Assert.That(patient.Downed, Is.True);
        }

        [Test]
        public void AFallenCarrierIsDroppedAndThePatientRescuedAgain()
        {
            var (colony, rescuer, patient, by) = Three(colonists: 4);
            Pawn second = colony.Pawns.Pawns.All[3];
            Stand(colony, patient, Near(colony, 12, 6));
            Down(colony, patient, by);
            Draft(colony, rescuer);
            Rescue(colony, rescuer, patient);
            for (int t = 0; t < 2_000 && patient.CarriedBy == 0; t++) colony.World.Tick();
            Assume.That(patient.CarriedBy, Is.Not.Zero);
            Strike(colony, by, rescuer, rescuer.HpMilli + 1_000);
            colony.World.Tick();
            Assume.That(patient.CarriedBy, Is.Zero);

            second.WorkPriorities[WorkTypeIndex.Rescue] = 1;
            for (int t = 0; t < 12_000 && !InABed(colony, patient); t++) colony.World.Tick();
            Assert.That(InABed(colony, patient), Is.True, "the patient dropped by a fallen carrier was never fetched");
        }

        // ---- a save in the middle ---------------------------------------------------------------

        [Test]
        public void ARescueSavedMidCarryResumesTheSameCarry()
        {
            var (colony, rescuer, patient, by) = Three();
            Stand(colony, patient, Near(colony, 12, 6));
            Down(colony, patient, by);
            Draft(colony, rescuer);
            Rescue(colony, rescuer, patient);
            for (int t = 0; t < 2_000 && patient.CarriedBy == 0; t++) colony.World.Tick();
            colony.World.Tick(10);
            Assume.That(patient.CarriedBy, Is.EqualTo(rescuer.Id.Value));

            var restored = Board(colonists: 3);
            restored.Load(colony.Save());
            Pawn back = restored.Pawns.Pawns.Get(rescuer.Id)!;
            Pawn backPatient = restored.Pawns.Pawns.Get(patient.Id)!;
            Assert.That(back.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Rescue));
            Assert.That(backPatient.CarriedBy, Is.EqualTo(back.Id.Value));
            Assert.That(Hash(restored), Is.EqualTo(Hash(colony)), "the loaded world is not the saved one");

            RunTheRescue(colony, rescuer);
            RunTheRescue(restored, back);
            Assert.That(restored.World.CurrentTick, Is.EqualTo(colony.World.CurrentTick));
            Assert.That(backPatient.Cell, Is.EqualTo(patient.Cell));
            Assert.That(Hash(restored), Is.EqualTo(Hash(colony)));
        }
    }
}
