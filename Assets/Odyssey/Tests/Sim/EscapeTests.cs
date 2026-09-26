#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;
using static Odyssey.Tests.Sim.PrisonFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Escape (design 58 §9): a visible risk from conditions, divided by the square root of how
    /// many are held, rolled once a game hour against exactly the number the pane shows. An
    /// escapee breaks her cell door with her fists, runs for the edge and is gone; brought down on
    /// the way, she is a prisoner again.
    /// </summary>
    public class EscapeTests
    {
        static ColonyWorld Colony() => Board(colonists: 1, beds: 0);

        static Pawn Colonist(ColonyWorld colony) => colony.Pawns.Pawns.All[0];

        /// <summary>A prisoner at mood 500, fed, unhurt, with the colonist beside her cell.</summary>
        static (ColonyWorld, Cell, Pawn) Held()
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            prisoner.Mood = 500;
            prisoner.Needs[NeedIndex.Food] = colony.Pawns.Content.Needs[NeedIndex.Food].max;
            Stand(colony, Colonist(colony), cell.Outside);
            return (colony, cell, prisoner);
        }

        // ---- the risk ---------------------------------------------------------------------------

        [Test]
        public void AWellKeptWatchedPrisonerCarriesTheBaseRiskAdjustedForBeingWhole()
        {
            var (colony, _, prisoner) = Held();
            EscapeOdds odds = EscapeRisk.Odds(prisoner, colony.Pawns);
            Assert.That(odds.Reasons, Is.EqualTo(EscapeReasons.Unhurt | EscapeReasons.WellKept));
            Assert.That(odds.PerDayPpm, Is.EqualTo(EscapeRisk.BasePpm * 3 / 2 * 7 / 10), "2 % x 1.5 x 0.7");
            Assert.That(odds.Held, Is.EqualTo(1));
        }

        [Test]
        public void MiseryAndBeingUnwatchedRaiseIt()
        {
            var (colony, _, prisoner) = Held();
            int calm = EscapeRisk.Odds(prisoner, colony.Pawns).PerDayPpm;

            prisoner.Mood = 100;
            EscapeOdds miserable = EscapeRisk.Odds(prisoner, colony.Pawns);
            Assert.That(miserable.PerDayPpm, Is.EqualTo(calm * 3));
            Assert.That(miserable.Reasons & EscapeReasons.Miserable, Is.EqualTo(EscapeReasons.Miserable));

            Stand(colony, Colonist(colony), Near(colony, -20, -20));
            EscapeOdds alone = EscapeRisk.Odds(prisoner, colony.Pawns);
            Assert.That(alone.PerDayPpm, Is.EqualTo(calm * 3 * 3 / 2));
            Assert.That(alone.Reasons & EscapeReasons.Unwatched, Is.EqualTo(EscapeReasons.Unwatched));
        }

        [Test]
        public void ShacklesDoubleItAndAWoundHalvesIt()
        {
            ColonyWorld colony = Bodiless(Colony());
            colony.World.Tick();
            int bed = ShackleBed(colony, -8);
            Pawn prisoner = HeldOn(colony, bed, Near(colony, -6, 0));
            prisoner.Mood = 500;
            prisoner.Needs[NeedIndex.Food] = colony.Pawns.Content.Needs[NeedIndex.Food].max;
            Stand(colony, Colonist(colony), Near(colony, -4, 0));

            EscapeOdds shackled = EscapeRisk.Odds(prisoner, colony.Pawns);
            Assert.That(shackled.Reasons & EscapeReasons.Shackled, Is.EqualTo(EscapeReasons.Shackled));
            Assert.That(shackled.PerDayPpm, Is.EqualTo(EscapeRisk.BasePpm * 2 * 3 / 2 * 7 / 10));

            RaiseHp(prisoner, prisoner.HpMaxMilli / 2);
            EscapeOdds hurt = EscapeRisk.Odds(prisoner, colony.Pawns);
            Assert.That(hurt.Reasons & EscapeReasons.Hurt, Is.EqualTo(EscapeReasons.Hurt));
            Assert.That(hurt.PerDayPpm, Is.EqualTo(EscapeRisk.BasePpm * 2 / 2 * 7 / 10));
        }

        /// <summary>
        /// <b>The headcount fix.</b> Four prisoners each carry half the risk one would, so the
        /// prison's total doubles rather than quadruples — the reference's risk grew with every
        /// prisoner added and never said so.
        /// </summary>
        [Test]
        public void EachPrisonerCarriesOneOverTheRootOfHowManyAreHeld()
        {
            var (colony, cell, prisoner) = Held();
            int alone = EscapeRisk.Odds(prisoner, colony.Pawns).PerDayPpm;
            for (int i = 0; i < 3; i++)
            {
                Pawn other = Spawn(colony, PawnKindIndex.Bandit, Near(colony, -10 - 2 * i, 6));
                Assume.That(Send(colony, new Intent(IntentKind.DebugImprison, default, other.Id.Value, 0)), Is.EqualTo(IntentRejection.None));
            }
            EscapeOdds four = EscapeRisk.Odds(prisoner, colony.Pawns);
            Assert.That(four.Held, Is.EqualTo(4));
            Assert.That(four.PerDayPpm, Is.EqualTo(alone / 2));
        }

        /// <summary>
        /// <b>A breakout can cascade without a cascade rule</b> (design 58 §9c): a broken door opens
        /// the room, the room's bed has no walls round it any more, and every cellmate's risk
        /// doubles on the shackles' factor. Nothing in the escape code says so; it falls out of
        /// the bed purpose being derived from the room.
        /// </summary>
        [Test]
        public void ABrokenDoorDoublesACellmatesRisk()
        {
            var (colony, cell, prisoner) = Held();
            int shut = EscapeRisk.Odds(prisoner, colony.Pawns).PerDayPpm;
            Assume.That(colony.Construction.Demolish(colony.Pawns, cell.Door, out _), Is.True);
            colony.World.Tick();
            EscapeOdds open = EscapeRisk.Odds(prisoner, colony.Pawns);
            Assert.That(open.Reasons & EscapeReasons.Shackled, Is.EqualTo(EscapeReasons.Shackled),
                "the cell is gone; the bed is a bed in the open");
            Assert.That(open.PerDayPpm, Is.EqualTo(shut * 2));
        }

        [Test]
        public void TheDownedAndTheCarriedNeverRoll()
        {
            var (colony, _, prisoner) = Held();
            Strike(colony, Colonist(colony), prisoner, prisoner.HpMilli);
            Assume.That(prisoner.Downed, Is.True);
            Assert.That(EscapeRisk.Odds(prisoner, colony.Pawns).PerDayPpm, Is.Zero);
        }

        // ---- the roll -----------------------------------------------------------------------------

        [Test]
        public void EachPrisonerRollsOnceAGameHour()
        {
            var (_, _, prisoner) = Held();
            int due = 0;
            for (int tick = 0; tick < Calendar.TicksPerHour * 24; tick++) if (EscapeRisk.Due(prisoner, tick)) due++;
            Assert.That(due, Is.EqualTo(24));
        }

        /// <summary>
        /// <b>The risk shown is the risk rolled.</b> The threshold is rebuilt from the published
        /// aspect alone, and the roll agrees with it on every draw across a long run of hours; a
        /// threshold doubled — what a second copy of the arithmetic would drift to — disagrees.
        /// </summary>
        [Test]
        public void ThePublishedRiskIsExactlyWhatIsRolled()
        {
            var (colony, _, prisoner) = Held();
            prisoner.Mood = 0;
            Stand(colony, Colonist(colony), Near(colony, -20, -20));
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.TryGetPawnAspect(prisoner.Id, PrisonAspects.Escape, out int ppm), Is.True);
            Assume.That(ppm, Is.GreaterThan(0));
            int threshold = ppm / 24;

            int disagree = 0, disagreeDoubled = 0, breaks = 0;
            for (int hour = 0; hour < 20_000; hour++)
            {
                int tick = hour * Calendar.TicksPerHour;
                bool rolled = EscapeRisk.Rolls(prisoner, colony.Pawns, tick);
                int draw = DeterministicRandom.ForTick(colony.Pawns.Seed, tick, PrisonPurpose.Escape ^ (uint)prisoner.Id.Value)
                    .NextInt(1_000_000);
                if (rolled) breaks++;
                if (rolled != draw < threshold) disagree++;
                if (rolled != draw < threshold * 2) disagreeDoubled++;
            }
            Assert.That(disagree, Is.Zero);
            Assert.That(disagreeDoubled, Is.GreaterThan(0), "the negative control: a doubled threshold is caught");
            Assert.That(breaks, Is.GreaterThan(0), "and at this risk she does break out now and then");
        }

        // ---- the escape ---------------------------------------------------------------------------

        [Test]
        public void AnEscapeeBreaksTheDoorRunsAndIsGone()
        {
            var (colony, cell, prisoner) = Held();
            // Out of the way, so the fight is the door's alone.
            Stand(colony, Colonist(colony), Near(colony, -25, -25));
            Draft(colony, Colonist(colony));
            int escaped = colony.Incidents.Ledger.Fires(IncidentHandle.PrisonerEscaped);
            Assume.That(Send(colony, new Intent(IntentKind.DebugImprison, default, prisoner.Id.Value, 2)), Is.EqualTo(IntentRejection.None));
            Assert.That(prisoner.Custody, Is.EqualTo(PawnCustody.Escaping));
            Assert.That(prisoner.IsHostile, Is.True, "a runaway is fought");
            Assert.That(colony.World.Views.Current.TryGetPawn(prisoner.Id, out PawnView view) && view.IsPrisoner, Is.True,
                "and still wears the jumpsuit");

            bool doorDown = false;
            for (int t = 0; t < 60_000 && colony.Pawns.Pawns.Get(prisoner.Id) != null; t++)
            {
                colony.World.Tick();
                if (!doorDown && !BuildingTargets.TryFind(colony.Pawns, cell.Door, out _)) doorDown = true;
            }
            Assert.That(doorDown, Is.True, "the door was broken");
            Assert.That(colony.Pawns.Pawns.Get(prisoner.Id), Is.Null, "she got away");
            Assert.That(colony.Incidents.Ledger.Fires(IncidentHandle.PrisonerEscaped), Is.EqualTo(escaped + 1));
            Assert.That(colony.Construction.BedOwnerAt(cell.Bed), Is.Zero, "and her bed is free");
        }

        [Test]
        public void ADownedEscapeeIsAPrisonerAgain()
        {
            var (colony, _, prisoner) = Held();
            Assume.That(Send(colony, new Intent(IntentKind.DebugImprison, default, prisoner.Id.Value, 2)), Is.EqualTo(IntentRejection.None));
            Strike(colony, Colonist(colony), prisoner, prisoner.HpMilli);
            Assume.That(prisoner.Downed, Is.True);
            colony.World.Tick();
            Assert.That(prisoner.Custody, Is.EqualTo(PawnCustody.Prisoner));
            Assert.That(prisoner.IsHostile, Is.False);
            Assert.That(CaptureRules.WantsCapture(prisoner, colony.Pawns), Is.True, "and the warden's capture brings her back");
        }

        [Test]
        public void OnlyAHeldPrisonerCanBeBrokenOut()
        {
            var (colony, _, _) = Held();
            Assert.That(Send(colony, new Intent(IntentKind.DebugImprison, default, Colonist(colony).Id.Value, 2)),
                Is.EqualTo(IntentRejection.NotPermitted));
        }
    }
}
