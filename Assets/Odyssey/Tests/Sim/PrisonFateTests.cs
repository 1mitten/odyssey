#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;
using static Odyssey.Tests.Sim.PrisonFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The ways out of custody and the two new ways in (design 58 §10): release and exile, where a
    /// warden opens the door and she walks off the board — or, for an arrested colonist released,
    /// back to work; surrender, where a badly hurt raider gives up and walks to a cell; and arrest,
    /// where a colonist is taken by another's hand and the colony minds.
    /// </summary>
    public class PrisonFateTests
    {
        static ColonyWorld Colony(int colonists = 1) => Board(colonists: colonists, beds: 0);

        static Pawn Colonist(ColonyWorld colony, int i = 0) => colony.Pawns.Pawns.All[i];

        static IntentRejection SetMode(ColonyWorld colony, Pawn prisoner, PrisonMode mode) =>
            Send(colony, new Intent(IntentKind.SetPrisonMode, default, prisoner.Id.Value, (int)mode));

        static void TickUntil(ColonyWorld colony, System.Func<bool> done, int limit)
        {
            for (int t = 0; t < limit && !done(); t++) colony.World.Tick();
        }

        // ---- release and exile --------------------------------------------------------------------

        [TestCase(PrisonMode.Release)]
        [TestCase(PrisonMode.Exile)]
        public void AWardenLetsHerOutAndSheWalksOffTheBoard(PrisonMode mode)
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            prisoner.Mood = 1_000;
            Assume.That(SetMode(colony, prisoner, mode), Is.EqualTo(IntentRejection.None));

            TickUntil(colony, () => prisoner.Custody != PawnCustody.Prisoner, 6_000);
            Assert.That(prisoner.Custody, Is.EqualTo(PawnCustody.Released), "the warden let her go");
            Assert.That(prisoner.IsHostile, Is.False, "and she is nobody's enemy on her way");
            Assert.That(colony.Construction.BedOwnerAt(cell.Bed), Is.Zero, "the cell is free again");

            TickUntil(colony, () => colony.Pawns.Pawns.Get(prisoner.Id) == null, 40_000);
            Assert.That(colony.Pawns.Pawns.Get(prisoner.Id), Is.Null, "out through the door and off the board");
        }

        [Test]
        public void OnHoldNobodyLetsHerGo()
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            prisoner.Mood = 1_000;
            for (int t = 0; t < 4_000; t++) colony.World.Tick();
            Assert.That(prisoner.Custody, Is.EqualTo(PawnCustody.Prisoner));
        }

        [Test]
        public void AnArrestedColonistReleasedGoesBackToWorkAndRemembersIt()
        {
            ColonyWorld colony = Colony(colonists: 2);
            Cell cell = BuildCell(colony);
            Pawn arrested = Colonist(colony, 1);
            Assume.That(Send(colony, new Intent(IntentKind.DebugImprison, default, arrested.Id.Value, 0)), Is.EqualTo(IntentRejection.None));
            Assume.That(arrested.Prison!.Arrested, Is.True);
            Assume.That(Send(colony, new Intent(IntentKind.AssignBedOwner, Size.FromIndex(cell.Bed), arrested.Id.Value)), Is.EqualTo(IntentRejection.None));
            Stand(colony, arrested, cell.Centre);
            arrested.Mood = 1_000;
            Assume.That(SetMode(colony, arrested, PrisonMode.Release), Is.EqualTo(IntentRejection.None));

            TickUntil(colony, () => arrested.Custody != PawnCustody.Prisoner, 6_000);
            Assert.That(arrested.Custody, Is.EqualTo(PawnCustody.Free));
            Assert.That(arrested.IsColonist, Is.True, "one of ours again");
            Assert.That(arrested.Prison == null || !arrested.Prison.Dressed, Is.True, "out of the jumpsuit");
            Assert.That(arrested.Memories.Any(m => m.ThoughtIndex == ThoughtIndex.WasArrested), Is.True);
            for (int t = 0; t < 2_000; t++) colony.World.Tick();
            Assert.That(colony.Pawns.Pawns.Get(arrested.Id), Is.SameAs(arrested), "and she stays");
        }

        // ---- surrender ----------------------------------------------------------------------------

        static (ColonyWorld, Cell) WithACell()
        {
            ColonyWorld colony = Bodiless(Colony());
            colony.World.Tick();
            Cell cell = BuildCell(colony);
            return (colony, cell);
        }

        /// <summary>
        /// <b>A surrender ends the fight with her</b> (review 2026-09-26). The attack drivers ended
        /// only on a death or a down, so a colonist ordered to attack went on striking a raider who
        /// had given up and was walking to her cell.
        /// </summary>
        [Test]
        public void ASurrenderEndsTheAttackOnHer()
        {
            var (colony, _) = WithACell();
            Pawn colonist = Colonist(colony);
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, 2, 0));
            Assume.That(Draft(colony, colonist), Is.EqualTo(IntentRejection.None));
            Assume.That(Attack(colony, colonist, bandit), Is.EqualTo(IntentRejection.None));
            Assume.That(colonist.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.AttackMelee), "the control: attacking");

            Surrender.Yield(bandit, colony.Pawns, colony.World.CurrentTick);
            int hp = bandit.HpMilli;
            // A few ticks: a step the order interrupted is landed before the driver is asked.
            for (int t = 0; t < 60 && colonist.CurrentJob?.DefIndex == JobIndex.AttackMelee; t++) colony.World.Tick();
            Assert.That(colonist.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.AttackMelee));
            Assert.That(bandit.HpMilli, Is.EqualTo(hp), "and nobody struck her once she had given up");
        }

        [Test]
        public void TheBlowThatCrossesThreeTenthsIsTheOnlyOneThatAsks()
        {
            var (colony, _) = WithACell();
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, -6, 4));
            int max = bandit.HpMaxMilli;
            RaiseHp(bandit, max * 4 / 10);
            int before = bandit.HpMilli;
            RaiseHp(bandit, max * 2 / 10);
            Assert.That(Surrender.Crossed(bandit, before), Is.True);
            before = bandit.HpMilli;
            RaiseHp(bandit, max / 10);
            Assert.That(Surrender.Crossed(bandit, before), Is.False, "already under the line: not asked again");
        }

        [Test]
        public void NoFreePrisonBedNoSurrender()
        {
            ColonyWorld colony = Bodiless(Colony());
            colony.World.Tick();
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, -6, 4));
            Assert.That(Surrender.ChanceOf(bandit, colony.Pawns), Is.Zero);
            var (withCell, _) = WithACell();
            Pawn other = Spawn(withCell, PawnKindIndex.Bandit, Near(withCell, -6, 4));
            Assert.That(Surrender.ChanceOf(other, withCell.Pawns), Is.EqualTo(Surrender.ChancePerMille * 2),
                "the control: with a cell, a quarter — doubled, since she is alone");
        }

        [Test]
        public void ARaiderWhoYieldsDropsHerWeaponAndWalksToTheCell()
        {
            var (colony, cell) = WithACell();
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, -6, 4));
            int surrendered = colony.Incidents.Ledger.Fires(IncidentHandle.Surrendered);
            Surrender.Yield(bandit, colony.Pawns, colony.World.CurrentTick);

            Assert.That(bandit.Custody, Is.EqualTo(PawnCustody.Prisoner));
            Assert.That(bandit.EquippedItem, Is.Zero, "the weapon is on the ground");
            Assert.That(colony.Construction.BedOwnerAt(cell.Bed), Is.EqualTo(bandit.Id.Value), "given the bed");
            Assert.That(colony.Incidents.Ledger.Fires(IncidentHandle.Surrendered), Is.EqualTo(surrendered + 1));

            TickUntil(colony, () => colony.Pawns.Enclosure!.RoomAt(bandit.Cell) == cell.Room, 6_000);
            Assert.That(colony.Pawns.Enclosure!.RoomAt(bandit.Cell), Is.EqualTo(cell.Room), "she walked herself in");
            TickUntil(colony, () => bandit.Prison!.Dressed, 2_000);
            Assert.That(bandit.Prison!.Dressed, Is.True, "and is dressed for the cell at her bed");
        }

        [Test]
        public void SurrenderIsRolledWhereTheDamageIsDealt()
        {
            // Over many raiders, some yield to the crossing blow and some do not: the roll is real
            // and it is the damage owner's.
            var (colony, _) = WithACell();
            Pawn colonist = Colonist(colony);
            int yielded = 0, asked = 0;
            for (int i = 0; i < 40 && yielded == 0; i++)
            {
                Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, -12 + i % 8, 8 + i / 8));
                RaiseHp(bandit, bandit.HpMaxMilli * 35 / 100);
                colony.Pawns.Combat!.Hurt(bandit, colonist, bandit.HpMaxMilli / 10, AfflictionKind.Bruise, HitSet.Melee, -1,
                    colony.World.CurrentTick + i);
                asked++;
                if (bandit.Custody == PawnCustody.Prisoner) yielded++;
            }
            Assert.That(yielded, Is.EqualTo(1), $"one yielded in {asked} crossings");
        }

        // ---- arrest -------------------------------------------------------------------------------

        [Test]
        public void AnArrestTakesHerAndTheColonyMinds()
        {
            ColonyWorld colony = Bodiless(Colony(colonists: 3));
            colony.World.Tick();
            Cell cell = BuildCell(colony);
            Pawn warden = Colonist(colony, 0), target = Colonist(colony, 1), witness = Colonist(colony, 2);
            target.Mood = 1_000;
            int arrested = colony.Incidents.Ledger.Fires(IncidentHandle.Arrested);

            Assert.That(Send(colony, new Intent(IntentKind.OrderArrest, default, warden.Id.Value, target.Id.Value)),
                Is.EqualTo(IntentRejection.None));
            TickUntil(colony, () => target.Custody != PawnCustody.Free, 4_000);

            Assert.That(target.Custody, Is.Not.EqualTo(PawnCustody.Free), "taken");
            Assert.That(target.Prison!.Arrested, Is.True);
            Assert.That(witness.Memories.Any(m => m.ThoughtIndex == ThoughtIndex.ColonistArrested), Is.True, "the colony minds");
            Assert.That(colony.Incidents.Ledger.Fires(IncidentHandle.Arrested), Is.EqualTo(arrested + 1));
            if (target.Custody == PawnCustody.Prisoner)
            {
                Assert.That(colony.Construction.BedOwnerAt(cell.Bed), Is.EqualTo(target.Id.Value), "came quietly: a bed");
                TickUntil(colony, () => colony.Pawns.Enclosure!.RoomAt(target.Cell) == cell.Room, 6_000);
                Assert.That(colony.Pawns.Enclosure!.RoomAt(target.Cell), Is.EqualTo(cell.Room), "and walked there");
            }
        }

        [Test]
        public void ThePanesArrestSendsTheNearestColonist()
        {
            ColonyWorld colony = Colony(colonists: 3);
            BuildCell(colony);
            Pawn target = Colonist(colony, 0), near = Colonist(colony, 1), far = Colonist(colony, 2);
            Stand(colony, near, Near(colony, 1, 1));
            Stand(colony, far, Near(colony, -15, -15));
            Stand(colony, target, Near(colony, 0, 0));
            Assert.That(Send(colony, new Intent(IntentKind.OrderArrest, default, 0, target.Id.Value)), Is.EqualTo(IntentRejection.None));
            Assert.That(near.CurrentJob!.DefIndex, Is.EqualTo(JobIndex.Arrest));
            Assert.That(far.CurrentJob?.DefIndex, Is.Not.EqualTo(JobIndex.Arrest));
        }

        [TestCase(1_000, 50)]
        [TestCase(500, 200)]
        [TestCase(0, 450)]
        public void ResistanceFollowsMood(int mood, int perMille)
        {
            ColonyWorld colony = Colony();
            Pawn colonist = Colonist(colony);
            colonist.Mood = mood;
            Assert.That(Arrest.ResistPerMille(colonist), Is.EqualTo(perMille));
        }

        [Test]
        public void ArrestIsRefusedWithoutACellAndForAStranger()
        {
            ColonyWorld colony = Colony(colonists: 2);
            Pawn a = Colonist(colony, 0), b = Colonist(colony, 1);
            Assert.That(Send(colony, new Intent(IntentKind.OrderArrest, default, a.Id.Value, b.Id.Value)),
                Is.EqualTo(IntentRejection.NotPermitted), "no prison bed");
            BuildCell(colony);
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, -6, 4));
            Assert.That(Send(colony, new Intent(IntentKind.OrderArrest, default, a.Id.Value, bandit.Id.Value)),
                Is.EqualTo(IntentRejection.NotPermitted), "a raider is captured, not arrested");
            Assert.That(Send(colony, new Intent(IntentKind.OrderArrest, default, a.Id.Value, a.Id.Value)),
                Is.EqualTo(IntentRejection.NotPermitted));
            Assert.That(Send(colony, new Intent(IntentKind.OrderArrest, default, a.Id.Value, b.Id.Value)),
                Is.EqualTo(IntentRejection.None), "the control: with a cell, she can be");
        }

        [Test]
        public void AResistingArresteeRunsAndIsHostile()
        {
            ColonyWorld colony = Bodiless(Colony(colonists: 2));
            colony.World.Tick();
            BuildCell(colony);
            Pawn warden = Colonist(colony, 0), target = Colonist(colony, 1);
            target.Mood = 0;
            // Contact directly, on ticks until the roll says she resists: 45 % at mood nought.
            for (int t = 0; t < 50 && target.Custody == PawnCustody.Free; t++)
            {
                Pawn fresh = target;
                Arrest.Contact(warden, fresh, colony.Pawns);
                if (fresh.Custody == PawnCustody.Prisoner)
                {
                    // Came quietly this time: set her free and try on the next tick.
                    Assume.That(Send(colony, new Intent(IntentKind.DebugImprison, default, fresh.Id.Value, 1)), Is.EqualTo(IntentRejection.None));
                    Assume.That(fresh.Custody, Is.EqualTo(PawnCustody.Free));
                    fresh.Mood = 0;
                }
            }
            Assert.That(target.Custody, Is.EqualTo(PawnCustody.Escaping), "she resisted");
            Assert.That(target.IsHostile, Is.True);
            Assert.That(target.RetaliateAgainst, Is.EqualTo(warden.Id.Value), "and bears the arrester a grudge");
        }
    }
}
