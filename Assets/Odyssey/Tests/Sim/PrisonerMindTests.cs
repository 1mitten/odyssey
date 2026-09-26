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
    /// The held prisoner's mind (design 58 §6): she walks her cell and never leaves it through a
    /// closed door, sleeps in her own prison bed, eats what is in her cell, stays on a shackle bed,
    /// heals in bed like a colonist, and has no mental breaks — low mood is the escape risk's.
    /// The cell is built the way a player would build one: a ring of walls with a door, and a roof.
    /// </summary>
    public class PrisonerMindTests
    {
        const int Prison = (int)BedPurpose.Prison;

        static ColonyWorld Colony() => Board(colonists: 1, beds: 0);

        // ---- confinement --------------------------------------------------------------------

        [Test]
        public void AHeldPrisonerWalksHerCellAndNeverLeavesIt()
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);

            int moved = 0, last = prisoner.Cell;
            for (int t = 0; t < 20_000; t++)
            {
                colony.World.Tick();
                if (prisoner.Cell != last) { moved++; last = prisoner.Cell; }
                if (t % 25 == 0)
                    Assert.That(colony.Pawns.Enclosure!.RoomAt(prisoner.Cell), Is.EqualTo(cell.Room),
                        $"tick {t}: she is outside her cell, at {Size.FromIndex(prisoner.Cell)}");
            }
            Assert.That(moved, Is.GreaterThan(0), "and she did walk it, rather than standing still");
        }

        [Test]
        public void ATiredPrisonerSleepsInHerOwnPrisonBed()
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            // Tired, not spent: at nought she would lie down where she stood, which is the collapse.
            prisoner.Needs[NeedIndex.Rest] = colony.Pawns.Content.Needs[NeedIndex.Rest].seekThreshold - 10;
            Assume.That(PrisonerTrees.OwnBed(prisoner, colony.Pawns), Is.EqualTo(cell.Bed), "she owns the bed");

            for (int t = 0; t < 2_000 && !prisoner.Asleep; t++) colony.World.Tick();
            Assert.That(prisoner.Asleep, Is.True);
            Assert.That(prisoner.Cell, Is.EqualTo(cell.Bed), "in her bed, not on the floor");
        }

        [Test]
        public void AHungryPrisonerEatsWhatIsInHerCellAndNothingOutsideIt()
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            colony.Pawns.Items.Spawn(ItemIndex.Meal, cell.Outside, 3);

            var job = prisoner.JobBuffer;
            prisoner.Needs[NeedIndex.Food] = 1;
            var node = new PrisonerNeedsThinkNode();
            job.Reset(JobIndex.Wait);
            prisoner.Needs[NeedIndex.Rest] = colony.Pawns.Content.Needs[NeedIndex.Rest].max;
            Assert.That(node.TryGiveJob(prisoner, colony.Pawns, job), Is.False, "the only food is outside");

            int inside = cell.Inside;
            colony.Pawns.Items.Spawn(ItemIndex.Meal, inside, 1);
            Assert.That(node.TryGiveJob(prisoner, colony.Pawns, job), Is.True);
            Assert.That(job.DefIndex, Is.EqualTo(JobIndex.Eat));
            Assert.That(job.TargetCell, Is.EqualTo(inside));
        }

        [Test]
        public void AShackledPrisonerStaysOnHerBed()
        {
            ColonyWorld colony = Colony();
            int bed = Near(colony, -8, 0);
            RaiseAt(colony, bed, BuildingHandle.Bed);
            Assume.That(Send(colony, new Intent(IntentKind.SetBedPurpose, Size.FromIndex(bed), Prison)), Is.EqualTo(IntentRejection.None));
            Assume.That(colony.Construction.Purposes!.IsShackled(bed), Is.True);

            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, Near(colony, -5, 0));
            Assume.That(Send(colony, new Intent(IntentKind.DebugImprison, default, bandit.Id.Value, 0)), Is.EqualTo(IntentRejection.None));
            Assume.That(Send(colony, new Intent(IntentKind.AssignBedOwner, Size.FromIndex(bed), bandit.Id.Value)), Is.EqualTo(IntentRejection.None));

            for (int t = 0; t < 1_500 && bandit.Cell != bed; t++) colony.World.Tick();
            Assert.That(bandit.Cell, Is.EqualTo(bed), "she goes to the bed she is shackled to");
            for (int t = 0; t < 5_000; t++)
            {
                colony.World.Tick();
                if (t % 50 == 0) Assert.That(bandit.Cell, Is.EqualTo(bed), $"tick {t}: she left the bed");
            }
        }

        /// <summary>
        /// <b>Marking an unowned bed wakes the colonist asleep in it</b> (review 2026-09-26). The
        /// wake sweep was raised only when an owner was stripped, and a bed two colonists share
        /// has none — so she slept on in a prison bed.
        /// </summary>
        [Test]
        public void MarkingAnUnownedBedWakesTheColonistInIt()
        {
            ColonyWorld colony = Board(colonists: 2, beds: 0);
            Cell cell = BuildCell(colony);
            Assume.That(Send(colony, new Intent(IntentKind.SetBedPurpose, Size.FromIndex(cell.Bed), (int)BedPurpose.Colony)),
                Is.EqualTo(IntentRejection.None));
            Pawn sleeper = colony.Pawns.Pawns.All[0];
            Stand(colony, sleeper, cell.Centre);
            sleeper.Needs[NeedIndex.Rest] = colony.Pawns.Content.Needs[NeedIndex.Rest].seekThreshold - 10;
            for (int t = 0; t < 2_000 && !(sleeper.Asleep && sleeper.Cell == cell.Bed); t++) colony.World.Tick();
            Assume.That(sleeper.Asleep && sleeper.Cell == cell.Bed, Is.True, "the control: asleep in the bed");
            Assume.That(colony.Construction.BedOwnerAt(cell.Bed), Is.Zero, "two colonists, one bed: nobody claims it");

            Assume.That(Send(colony, new Intent(IntentKind.SetBedPurpose, Size.FromIndex(cell.Bed), Prison)),
                Is.EqualTo(IntentRejection.None));
            colony.World.Tick();
            Assert.That(sleeper.CurrentJob?.DefIndex == JobIndex.Sleep && sleeper.CurrentJob.TargetCell == cell.Bed, Is.False,
                "woken out of a prison bed");
        }

        // ---- body and mood ------------------------------------------------------------------

        [Test]
        public void ADownedPrisonerHealsInBedAndGetsUp()
        {
            ColonyWorld colony = Bodiless(Colony());
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            Pawn colonist = colony.Pawns.Pawns.All[0];
            Strike(colony, colonist, prisoner, prisoner.HpMilli);
            Assume.That(prisoner.Downed, Is.True);
            Stand(colony, prisoner, cell.Bed);
            // Five points short of whole: a quarter of a day at the bed's twenty a day, where a
            // hostile heals none at all.
            RaiseHp(prisoner, prisoner.HpMaxMilli - 5_000);

            for (int t = 0; t < 60_000 && prisoner.Downed; t++) colony.World.Tick();
            Assert.That(prisoner.Downed, Is.False, "a hostile would have lain there for ever");
            Assert.That(prisoner.Custody, Is.EqualTo(PawnCustody.Prisoner));
        }

        [Test]
        public void APrisonerHasNoBreaksAndFeelsHeld()
        {
            ColonyWorld colony = Colony();
            Cell cell = BuildCell(colony);
            Pawn prisoner = HeldIn(colony, cell);
            prisoner.Mood = 0;
            Assert.That(prisoner.CanMentalBreak(), Is.False, "low mood is the escape risk's, not a break's");
            Assert.That(prisoner.NeedsTick, Is.True, "she gets hungry and tired");

            int joy = prisoner.Needs[NeedIndex.Joy];
            for (int t = 0; t < 2_000; t++) colony.World.Tick();
            Assert.That(prisoner.Memories.Any(m => m.ThoughtIndex == ThoughtIndex.Imprisoned), Is.True);
            Assert.That(prisoner.Needs[NeedIndex.Joy], Is.EqualTo(joy), "a cell has no way to meet joy, so it does not move");
            Assert.That(prisoner.BreakTicksLeft, Is.Zero);
        }
    }
}
