#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The prison bed in a whole colony (design 60 §5): the order through the intent, the owner of
    /// the wrong kind losing the bed at once, and every chooser reading the one rule. The bed stands
    /// in the open, so it is a shackle bed — the rooms themselves are <c>PrisonBedTests</c>'.
    /// </summary>
    public partial class BedTests
    {
        static IntentRejection MarkForPrisoners(ColonyWorld colony, int anyBedCell, bool prison = true)
        {
            colony.World.Intents.ClearRejected();
            colony.World.Intents.Submit(new Intent(
                IntentKind.SetBedPurpose, Size.FromIndex(anyBedCell), prison ? 1 : 0));
            colony.World.Tick();
            var rejected = colony.World.Intents.Rejected;
            return rejected.Count == 0 ? IntentRejection.None : rejected[0].Reason;
        }

        [Test]
        public void MarkingAColonistsBedForPrisonersTakesItFromHer()
        {
            ColonyWorld colony = Fresh();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            int head = OpenFootprint(colony, out _);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            RaiseABed(colony, head);
            Assume.That(Assign(colony, head, colonist.Id.Value), Is.EqualTo(IntentRejection.None));

            Assert.That(MarkForPrisoners(colony, head), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.BedPurposeAt(head), Is.EqualTo(BedPurpose.Prison));
            Assert.That(colony.Construction.BedOwnerAt(head), Is.Zero, "a colonist may not own a prison bed");
            Assert.That(colony.Construction.Purposes!.IsShackled(head), Is.True, "in the open it shackles");

            Assert.That(MarkForPrisoners(colony, head, prison: false), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.BedPurposeAt(head), Is.EqualTo(BedPurpose.Colony));
        }

        [Test]
        public void AColonistNeverChoosesAPrisonBedToSleepIn()
        {
            ColonyWorld colony = Alone();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            int head = OpenFootprint(colony, out _);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            RaiseABed(colony, head);

            var job = colonist.JobBuffer;
            colonist.Needs[NeedIndex.Rest] = 1;
            Assume.That(CriticalNeedsThinkNode.TrySleep(colonist, colony.Pawns, job), Is.True);
            Assume.That(job.TargetCell, Is.EqualTo(head), "the control: the only bed is hers to use");

            Assume.That(MarkForPrisoners(colony, head), Is.EqualTo(IntentRejection.None));
            Assert.That(CriticalNeedsThinkNode.TrySleep(colonist, colony.Pawns, job), Is.True);
            Assert.That(job.TargetCell, Is.Not.EqualTo(head), "a prison bed is not a colonist's to sleep in");
            Assert.That(Medical.BedFor(colonist, colony.Pawns), Is.Not.EqualTo(head), "nor to be a patient in");
        }

        [Test]
        public void APrisonerMayOwnAPrisonBedAndNotAColonyBed()
        {
            ColonyWorld colony = Fresh();
            int prisonBed = OpenFootprint(colony, out int foot);
            Assume.That(prisonBed, Is.GreaterThanOrEqualTo(0));
            RaiseABed(colony, prisonBed);
            int colonyBed = AnotherOpenFootprint(colony, prisonBed, foot, out _);
            Assume.That(colonyBed, Is.GreaterThanOrEqualTo(0));
            RaiseABed(colony, colonyBed);
            Assume.That(MarkForPrisoners(colony, prisonBed), Is.EqualTo(IntentRejection.None));

            Pawn bandit = colony.Pawns.Pawns.Spawn(Size.Index(colony.Start), PawnKindIndex.Bandit);
            colony.World.Intents.Submit(new Intent(IntentKind.DebugImprison, colony.Start, bandit.Id.Value, 0));
            colony.World.Tick();
            Assume.That(bandit.Custody, Is.EqualTo(PawnCustody.Prisoner));

            Assert.That(Assign(colony, colonyBed, bandit.Id.Value), Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(Assign(colony, prisonBed, bandit.Id.Value), Is.EqualTo(IntentRejection.None));
            Assert.That(Assign(colony, prisonBed, colony.Pawns.Pawns.All[0].Id.Value), Is.EqualTo(IntentRejection.NotPermitted),
                "and a colonist may not be given the prisoner's");

            // Unmarking takes it back from her, as marking took it from a colonist.
            Assume.That(MarkForPrisoners(colony, prisonBed, prison: false), Is.EqualTo(IntentRejection.None));
            Assert.That(colony.Construction.BedOwnerAt(prisonBed), Is.Zero);
        }

        [Test]
        public void ThePaneIsToldWhatTheBedIsFor()
        {
            ColonyWorld colony = Fresh();
            int head = OpenFootprint(colony, out _);
            Assume.That(head, Is.GreaterThanOrEqualTo(0));
            RaiseABed(colony, head);
            Assume.That(MarkForPrisoners(colony, head), Is.EqualTo(IntentRejection.None));

            colony.World.Intents.Submit(new Intent(IntentKind.QueryCell, Size.FromIndex(head)));
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.TryGetCellDetail(head, out CellDetail detail), Is.True);
            Assert.That(detail.BedPurpose, Is.EqualTo(CellDetail.BedShackles));
        }
    }
}
