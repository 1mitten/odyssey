#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// What the prison's tests share (design 59): a cell built as a player builds one — walls, a
    /// door, a roof, a bed marked for prisoners — a prisoner held in it, and a shackle bed in the
    /// open. Test code only.
    /// </summary>
    static class PrisonFixture
    {
        const int Prison = (int)BedPurpose.Prison;

        internal sealed class Cell
        {
            public int Room, Bed, Door, Centre, Inside, Outside;
        }

        internal static void RaiseAt(ColonyWorld colony, int cell, int building, int facing = 0)
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
        internal static Cell BuildCell(ColonyWorld colony)
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

        internal static Pawn HeldIn(ColonyWorld colony, Cell cell)
        {
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, cell.Centre);
            Assume.That(Send(colony, new Intent(IntentKind.DebugImprison, default, bandit.Id.Value, 0)),
                Is.EqualTo(IntentRejection.None));
            Assume.That(Send(colony, new Intent(IntentKind.AssignBedOwner, Size.FromIndex(cell.Bed), bandit.Id.Value)),
                Is.EqualTo(IntentRejection.None));
            Stand(colony, bandit, cell.Centre);
            return bandit;
        }

        /// <summary>A bed in the open, marked for prisoners: a shackle bed.</summary>
        internal static int ShackleBed(ColonyWorld colony, int dx)
        {
            int bed = Near(colony, dx, 0);
            RaiseAt(colony, bed, BuildingHandle.Bed);
            Assume.That(Send(colony, new Intent(IntentKind.SetBedPurpose, Size.FromIndex(bed), Prison)),
                Is.EqualTo(IntentRejection.None));
            Assume.That(colony.Construction.Purposes!.IsShackled(bed), Is.True);
            return bed;
        }

        /// <summary>A bandit imprisoned by the debug row and given this bed.</summary>
        internal static Pawn HeldOn(ColonyWorld colony, int bed, int at)
        {
            Pawn bandit = Spawn(colony, PawnKindIndex.Bandit, at);
            Assume.That(Send(colony, new Intent(IntentKind.DebugImprison, default, bandit.Id.Value, 0)),
                Is.EqualTo(IntentRejection.None));
            Assume.That(Send(colony, new Intent(IntentKind.AssignBedOwner, Size.FromIndex(bed), bandit.Id.Value)),
                Is.EqualTo(IntentRejection.None));
            return bandit;
        }
    }
}
