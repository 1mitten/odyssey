#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Capture (design 60 §7): a person marked and downed is carried to a prison bed by whoever has
    /// Warden work, and on the lay she is taken — in custody, the bed hers, dressed for the cell. No
    /// free prison bed means nothing moves and the reason is published. A raider taken stays on her
    /// band's roll counted as lost, which is what she already was lying downed. The doctor treats a
    /// held prisoner. The bed here stands in the open, so it shackles; the cells are the mind's tests'.
    /// </summary>
    public class CaptureTests
    {
        const int Prison = (int)BedPurpose.Prison;

        static ColonyWorld Colony()
        {
            ColonyWorld colony = Bodiless(Board(colonists: 1, beds: 0));
            colony.World.Tick();
            return colony;
        }

        static int PrisonBed(ColonyWorld colony, int dx)
        {
            int bed = Near(colony, dx, 0);
            Assume.That(colony.Construction.Place(Size.FromIndex(bed), BuildingHandle.Bed, StuffHandle.Wood, 0),
                Is.EqualTo(IntentRejection.None));
            Assume.That(colony.Construction.Raise(colony.Pawns, bed), Is.True);
            Assume.That(Send(colony, new Intent(IntentKind.SetBedPurpose, Size.FromIndex(bed), Prison)),
                Is.EqualTo(IntentRejection.None));
            return bed;
        }

        static Pawn DownedBandit(ColonyWorld colony, int dx)
        {
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, dx, 3));
            Strike(colony, colony.Pawns.Pawns.All[0], bandit, bandit.HpMilli);
            Assume.That(bandit.Downed, Is.True);
            return bandit;
        }

        static void TickUntil(ColonyWorld colony, System.Func<bool> done, int limit)
        {
            for (int t = 0; t < limit && !done(); t++) colony.World.Tick();
        }

        [Test]
        public void AMarkedDownedRaiderIsCarriedToAPrisonBedAndTaken()
        {
            ColonyWorld colony = Colony();
            int bed = PrisonBed(colony, -6);
            Pawn bandit = DownedBandit(colony, 4);
            Assume.That(Send(colony, new Intent(IntentKind.SetCaptureMark, default, bandit.Id.Value, 1)),
                Is.EqualTo(IntentRejection.None));

            TickUntil(colony, () => bandit.Custody == PawnCustody.Prisoner, 4_000);
            Assert.That(bandit.Custody, Is.EqualTo(PawnCustody.Prisoner), "taken on the lay");
            Assert.That(bandit.Cell, Is.EqualTo(bed), "into the prison bed");
            Assert.That(bandit.CarriedBy, Is.Zero, "and out of the arms");
            Assert.That(colony.Construction.BedOwnerAt(bed), Is.EqualTo(bandit.Id.Value), "the bed is hers");
            Assert.That(bandit.Prison!.Dressed, Is.True, "dressed for the cell");
            Assert.That(bandit.Prison.CaptureMark, Is.False, "the mark is spent");
            Assert.That(bandit.IsHostile, Is.False);
        }

        /// <summary>
        /// <b>Taken is disarmed</b> (design 60 §7; review 2026-09-26). A downed raider keeps her
        /// weapon by design, so a capture that did not take it would give a prisoner a machete to
        /// break out with.
        /// </summary>
        [Test]
        public void ACapturedRaiderIsDisarmed()
        {
            ColonyWorld colony = Colony();
            PrisonBed(colony, -6);
            Pawn bandit = DownedBandit(colony, 4);
            Assume.That(bandit.EquippedItem, Is.Not.Zero, "the control: she went down armed");
            Assume.That(Send(colony, new Intent(IntentKind.SetCaptureMark, default, bandit.Id.Value, 1)),
                Is.EqualTo(IntentRejection.None));
            TickUntil(colony, () => bandit.Custody == PawnCustody.Prisoner, 4_000);
            Assert.That(bandit.EquippedItem, Is.Zero);
        }

        [Test]
        public void AnUnmarkedDownedRaiderIsLeftWhereSheLies()
        {
            ColonyWorld colony = Colony();
            PrisonBed(colony, -6);
            Pawn bandit = DownedBandit(colony, 4);
            int lies = bandit.Cell;
            for (int t = 0; t < 2_000; t++) colony.World.Tick();
            Assert.That(bandit.Custody, Is.EqualTo(PawnCustody.Free), "nobody asked for her");
            Assert.That(bandit.Cell, Is.EqualTo(lies));
        }

        [Test]
        public void WithNoFreePrisonBedNothingMovesAndThePawnSaysWhy()
        {
            ColonyWorld colony = Colony();
            Pawn bandit = DownedBandit(colony, 4);
            Assume.That(Send(colony, new Intent(IntentKind.SetCaptureMark, default, bandit.Id.Value, 1)),
                Is.EqualTo(IntentRejection.None));
            int lies = bandit.Cell;
            for (int t = 0; t < 1_000; t++) colony.World.Tick();

            Assert.That(bandit.Custody, Is.EqualTo(PawnCustody.Free));
            Assert.That(bandit.Cell, Is.EqualTo(lies), "nobody lifts her with nowhere to take her");
            Assert.That(colony.World.Views.Current.TryGetPawnAspect(bandit.Id, PrisonAspects.NoBed, out _), Is.True,
                "the alert's reason is published");
            Assert.That(colony.World.Views.Current.TryGetPawnAspect(bandit.Id, PrisonAspects.CaptureMark, out _), Is.True);

            Pawn colonist = colony.Pawns.Pawns.All[0];
            Assert.That(Send(colony, new Intent(IntentKind.OrderCapture, default, colonist.Id.Value, bandit.Id.Value)),
                Is.EqualTo(IntentRejection.NotPermitted), "and the order is refused, the mark left standing");
        }

        [Test]
        public void TheOrderSendsTheColonistNow()
        {
            ColonyWorld colony = Colony();
            int bed = PrisonBed(colony, -6);
            Pawn bandit = DownedBandit(colony, 4);
            Pawn colonist = colony.Pawns.Pawns.All[0];

            Assert.That(Send(colony, new Intent(IntentKind.OrderCapture, default, colonist.Id.Value, bandit.Id.Value)),
                Is.EqualTo(IntentRejection.None));
            Assert.That(colonist.CurrentJob!.DefIndex, Is.EqualTo(JobIndex.Capture));
            TickUntil(colony, () => bandit.Custody == PawnCustody.Prisoner, 4_000);
            Assert.That(bandit.Cell, Is.EqualTo(bed));
        }

        [Test]
        public void AColonistIsNeverMarkedForCapture()
        {
            ColonyWorld colony = Colony();
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Assert.That(Send(colony, new Intent(IntentKind.SetCaptureMark, default, colonist.Id.Value, 1)),
                Is.EqualTo(IntentRejection.NotPermitted));
        }

        [Test]
        public void TheDoctorTreatsABleedingPrisoner()
        {
            ColonyWorld colony = Board(colonists: 1, beds: 0);
            colony.World.Tick();
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 4, 3));
            Assume.That(Send(colony, new Intent(IntentKind.DebugImprison, default, bandit.Id.Value, 0)),
                Is.EqualTo(IntentRejection.None));
            colony.Pawns.Combat!.Hurt(bandit, null, 20_000, AfflictionKind.Wound, HitSet.Melee, -1, colony.World.CurrentTick);
            Assume.That(Medical.IsBleeding(bandit), Is.True, "the wound bleeds");
            Assert.That(Medical.NeedsTreatment(bandit, colony.Pawns), Is.True,
                "a held prisoner is the colony's to keep alive");
        }

        /// <summary>
        /// <b>A raider taken stays on her band's roll and counts as out of the fight.</b> The band's
        /// standing count drops as each is taken — the same as if she had been downed — and when
        /// nobody is left standing the band is done with, rather than kept alive for ever by a
        /// prisoner standing in her cell.
        /// </summary>
        [Test]
        public void ATakenRaiderIsOutOfTheFightAndTheBandEndsWhenAllAreTaken()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 3;
            scenario.beds = 3;
            ColonyWorld colony = ColonyWorld.Build(Size, 7u, scenario, barren: true, wooded: false);
            colony.World.Tick(30);
            Assume.That(Send(colony, new Intent(IntentKind.InvokeIncident, default, IncidentHandle.Raid, 2, 1)),
                Is.EqualTo(IntentRejection.None));
            RaidSystem raids = colony.Pawns.Raids!;
            RaidGroup group = raids.Groups.Single();
            TickUntil(colony, () => group.Pending.Count == 0, 2_000);
            Assume.That(raids.Standing(group), Is.EqualTo(2));

            Pawn first = colony.Pawns.Pawns.Get(new PawnId(group.Members[0].Pawn))!;
            Pawn second = colony.Pawns.Pawns.Get(new PawnId(group.Members[1].Pawn))!;
            Assert.That(Send(colony, new Intent(IntentKind.DebugImprison, default, first.Id.Value, 0)), Is.EqualTo(IntentRejection.None));
            Assert.That(raids.Standing(group), Is.EqualTo(1), "held is out of the fight");
            Assert.That(raids.GroupOf(first.Id.Value), Is.SameAs(group), "but still on the roll");

            Assert.That(Send(colony, new Intent(IntentKind.DebugImprison, default, second.Id.Value, 0)), Is.EqualTo(IntentRejection.None));
            colony.World.Tick();
            Assert.That(raids.Groups, Is.Empty, "nobody left in the fight: the band is done with");
        }
    }
}
