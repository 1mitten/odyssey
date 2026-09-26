#nullable enable
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

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

        sealed class Cell
        {
            public int Room, Bed, Door, Centre, Inside, Outside;
        }

        static void RaiseAt(ColonyWorld colony, int cell, int building, int facing = 0)
        {
            Assume.That(colony.Construction.Place(Size.FromIndex(cell), building, StuffHandle.Wood, facing),
                Is.EqualTo(IntentRejection.None), $"could not order {building} at {Size.FromIndex(cell)}");
            Assume.That(colony.Construction.Raise(colony.Pawns, cell), Is.True);
        }

        /// <summary>
        /// A 4 x 5 ring of wooden walls round a 2 x 3 floor, a door on the east, a roof of slabs
        /// over the whole box, and a bed inside marked for prisoners. Small enough that every
        /// inside cell touches a wall, so every slab of the roof is supported where it is ordered.
        /// </summary>
        static Cell BuildCell(ColonyWorld colony)
        {
            CellRef c = Size.FromIndex(Near(colony, 10, 0));
            int x0 = c.X - 1, x1 = c.X + 2, z0 = c.Z - 2, z1 = c.Z + 2;
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (x != x0 && x != x1 && z != z0 && z != z1) continue;
                int at = Size.Index(x, z, c.Y);
                RaiseAt(colony, at, (x, z) == (x1, c.Z) ? BuildingHandle.Door : BuildingHandle.Wall);
                colony.World.Tick();
            }

            // The roof over the whole box, walls included, on the one layer the run rule picks:
            // slabs ordered cell by cell over an open floor are each lifted on their own, which is
            // the fault FloorRunTests pins.
            var box = new System.Collections.Generic.List<CellRef>();
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
                box.Add(new CellRef(x, z, c.Y));
            int roofY = colony.Construction.RunLayerFor(box, BuildingHandle.Floor);
            Assume.That(roofY, Is.EqualTo(c.Y + 1), "the roof goes on top of the cell");
            foreach (CellRef at in box)
                RaiseAt(colony, Size.Index(new CellRef(at.X, at.Z, roofY)), BuildingHandle.Floor);

            int bed = Size.Index(c.X, c.Z - 1, c.Y);
            RaiseAt(colony, bed, BuildingHandle.Bed);
            colony.World.Tick();

            var cell = new Cell
            {
                Centre = Size.Index(c.X + 1, c.Z, c.Y),
                Inside = Size.Index(c.X + 1, c.Z + 1, c.Y),
                Bed = bed,
                Door = Size.Index(x1, c.Z, c.Y),
                Outside = Size.Index(c.X + 4, c.Z, c.Y),
            };
            cell.Room = colony.Pawns.Enclosure!.RoomAt(cell.Centre);
            Assume.That(cell.Room, Is.Not.Zero, "the fixture built an enclosed room");
            Assume.That(Send(colony, new Intent(IntentKind.SetBedPurpose, Size.FromIndex(bed), Prison)),
                Is.EqualTo(IntentRejection.None));
            Assume.That(colony.Construction.Purposes!.IsCell(cell.Room), Is.True);
            return cell;
        }

        static Pawn HeldIn(ColonyWorld colony, Cell cell)
        {
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, cell.Centre);
            Assume.That(Send(colony, new Intent(IntentKind.DebugImprison, default, bandit.Id.Value, 0)),
                Is.EqualTo(IntentRejection.None));
            Assume.That(Send(colony, new Intent(IntentKind.AssignBedOwner, Size.FromIndex(cell.Bed), bandit.Id.Value)),
                Is.EqualTo(IntentRejection.None));
            Stand(colony, bandit, cell.Centre);
            return bandit;
        }

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
