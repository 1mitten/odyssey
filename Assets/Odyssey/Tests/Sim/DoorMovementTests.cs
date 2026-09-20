#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    [TestFixture]
    public class DoorMovementTests
    {
        sealed class Fixture
        {
            public readonly SimWorld World;
            public readonly PawnContext Ctx;
            public readonly CellGrid Cells;
            public readonly NavGraph Nav;
            public readonly MovementSystem Movement;
            public readonly DoorSystem Doors;

            public Fixture(int sx = 16, int sz = 16, int sy = 2)
            {
                var size = new GridSize(sx, sz, sy);
                Cells = new CellGrid(size);
                for (int i = 0; i < size.CellCount; i++) Cells.Floor[i] = 1;

                Nav = new NavGraph(Cells);
                var finder = new PathFinder(Nav);
                var paths = new PathService(finder);
                Ctx = new PawnContext(Cells, Nav, paths, ContentPack.Pawns());

                Movement = new MovementSystem(Ctx);
                Doors = new DoorSystem(Ctx);
                Ctx.Doors = Doors;

                World = new SimWorldBuilder()
                    .WithSeed(20260920u)
                    .WithSize(size)
                    .AddTickable(_ => Ctx.Pawns)
                    .AddSnapshotContributor(Ctx.Pawns)
                    .AddSystem(_ => Movement)
                    .AddSystem(_ => Doors)
                    .Build();

                Nav.MarkAllDirty();
                Nav.Rebuild();
            }

            public int Cell(int x, int z, int y = 0) => Cells.Index(x, z, y);

            public Pawn SpawnPawn(int x, int z, int y = 0)
            {
                var pawn = Ctx.Pawns.Spawn(Cell(x, z, y));
                pawn.Needs[NeedIndex.Food] = 100;
                pawn.Needs[NeedIndex.Rest] = 100;
                return pawn;
            }

            public void PlaceDoor(int x, int z, int y = 0)
            {
                int c = Cell(x, z, y);
                Nav.SetDoor(c, isDoor: true, open: false);
                Nav.MarkAllDirty();
                Nav.Rebuild();
            }
        }

        [Test]
        public void ClosedDoorChargesDoorOpeningCost()
        {
            var f = new Fixture();
            int start = f.Cell(5, 5, 0);
            int door = f.Cell(6, 5, 0);
            f.PlaceDoor(6, 5, 0);

            var pawn = f.SpawnPawn(5, 5, 0);
            pawn.AdoptPath(new[] { start, door }, 2);

            f.World.Tick();

            // Expected cost: (Orthogonal (100) + DoorOpening (60)) * Rates.Scale
            int expectedCost = (MoveCost.Orthogonal + MoveCost.DoorOpening) * Rates.Scale;
            Assert.That(pawn.MoveStepCost, Is.EqualTo(expectedCost),
                "stepping into a closed door charges MoveCost.DoorOpening");
            Assert.That(f.Doors.IsOpen(door), Is.False, "door remains closed while approach cost is paid");
        }

        [Test]
        public void SteppingIntoClosedDoorOpensIt()
        {
            var f = new Fixture();
            int start = f.Cell(5, 5, 0);
            int door = f.Cell(6, 5, 0);
            int dest = f.Cell(7, 5, 0);
            f.PlaceDoor(6, 5, 0);

            var pawn = f.SpawnPawn(5, 5, 0);
            pawn.AdoptPath(new[] { start, door, dest }, 3);

            // Step cost is 160 * 1000 = 160,000. At full health/rest, rate is 1000/tick (160 ticks).
            for (int tick = 0; tick < 165; tick++)
            {
                f.World.Tick();
                if (pawn.Cell == door) break;
            }

            Assert.That(pawn.Cell, Is.EqualTo(door), "pawn reached the door cell");
            Assert.That(f.Doors.IsOpen(door), Is.True, "door opens upon pawn entering the cell");
            Assert.That((f.Nav.Grid.Flags[door] & NavFlags.DoorOpen), Is.Not.EqualTo(NavFlags.None),
                "NavFlags.DoorOpen flag is set on the cell");
        }

        [Test]
        public void SteppingThroughAlreadyOpenDoorDoesNotChargeOpeningCost()
        {
            var f = new Fixture();
            int start = f.Cell(5, 5, 0);
            int door = f.Cell(6, 5, 0);
            f.PlaceDoor(6, 5, 0);
            f.Nav.SetDoorOpen(door, true);

            var pawn = f.SpawnPawn(5, 5, 0);
            pawn.AdoptPath(new[] { start, door }, 2);

            f.World.Tick();

            int expectedCost = MoveCost.Orthogonal * Rates.Scale;
            Assert.That(pawn.MoveStepCost, Is.EqualTo(expectedCost),
                "stepping into an already open door does not charge MoveCost.DoorOpening");
        }

        [Test]
        public void DoorStaysOpenWhileOccupied()
        {
            var f = new Fixture();
            int door = f.Cell(6, 5, 0);
            f.PlaceDoor(6, 5, 0);

            // Pawn spawns directly on the door
            var pawn = f.SpawnPawn(6, 5, 0);

            // Tick for 100 ticks (longer than DefaultCloseDelayTicks of 30)
            for (int i = 0; i < 100; i++)
            {
                f.World.Tick();
            }

            Assert.That(pawn.Cell, Is.EqualTo(door));
            Assert.That(f.Doors.IsOpen(door), Is.True, "door stays open as long as a pawn stands in it");
        }

        [Test]
        public void DoorClosesAfterTimeoutWhenVacated()
        {
            var f = new Fixture();
            int start = f.Cell(5, 5, 0);
            int door = f.Cell(6, 5, 0);
            int dest = f.Cell(7, 5, 0);
            f.PlaceDoor(6, 5, 0);

            var pawn = f.SpawnPawn(5, 5, 0);
            pawn.AdoptPath(new[] { start, door, dest }, 3);

            // Run until pawn has completely cleared the door cell and reached dest
            for (int tick = 0; tick < 400; tick++)
            {
                f.World.Tick();
                if (pawn.Cell == dest) break;
            }

            Assert.That(pawn.Cell, Is.EqualTo(dest), "pawn reached destination beyond door");
            Assert.That(f.Doors.IsOpen(door), Is.True, "door is still open immediately after pawn steps out");

            // Tick for 28 ticks - should still be open (delay is 30)
            for (int tick = 0; tick < 28; tick++)
            {
                f.World.Tick();
            }
            Assert.That(f.Doors.IsOpen(door), Is.True, "door remains open during timeout countdown");

            // Tick a few more ticks to pass the timeout
            for (int tick = 0; tick < 5; tick++)
            {
                f.World.Tick();
            }

            Assert.That(f.Doors.IsOpen(door), Is.False, "door closes once timeout expires");
            Assert.That((f.Nav.Grid.Flags[door] & NavFlags.DoorOpen), Is.EqualTo(NavFlags.None),
                "NavFlags.DoorOpen is cleared");
        }

        [Test]
        public void ApproachingPawnKeepsOpenDoorFromClosing()
        {
            var f = new Fixture();
            int start1 = f.Cell(5, 5, 0);
            int door = f.Cell(6, 5, 0);
            int dest = f.Cell(7, 5, 0);
            f.PlaceDoor(6, 5, 0);

            // Pre-open the door
            f.Nav.SetDoorOpen(door, true);

            // Pawn 1 is at start1 moving into door
            var pawn1 = f.SpawnPawn(5, 5, 0);
            pawn1.AdoptPath(new[] { start1, door, dest }, 3);

            // Tick 35 ticks (more than 30) while pawn1 is mid-step from start1 into door
            for (int tick = 0; tick < 35; tick++)
            {
                f.World.Tick();
            }

            Assert.That(f.Doors.IsOpen(door), Is.True,
                "an approaching pawn keeps an already open door from closing in their face");
        }

        [Test]
        public void AnimalsCannotPassThroughClosedDoorButCanPassOpenDoor()
        {
            var f = new Fixture(16, 16, 1);
            int sx = 16;
            // Build a wall across z = 5 with only one door at (6, 5, 0)
            for (int x = 0; x < sx; x++)
            {
                int c = f.Cell(x, 5, 0);
                if (x == 6)
                {
                    f.Nav.SetDoor(c, isDoor: true, open: false);
                }
                else
                {
                    f.Cells.Flags[c] |= CellFlags.SolidTerrain;
                }
            }

            f.Nav.MarkAllDirty();
            f.Nav.Rebuild();

            int west = f.Cell(6, 2, 0);
            int east = f.Cell(6, 8, 0);

            Assert.That(f.Nav.Reachable(west, east, TraverseMode.Colonist), Is.True,
                "colonist can path through closed door");
            Assert.That(f.Nav.Reachable(west, east, TraverseMode.Animal), Is.False,
                "animal cannot path through closed door");

            // Open the door
            f.Nav.SetDoorOpen(f.Cell(6, 5, 0), true);
            f.Nav.Rebuild();

            Assert.That(f.Nav.Reachable(west, east, TraverseMode.Animal), Is.True,
                "animal can path through open door");
        }
    }
}
