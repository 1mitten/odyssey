#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    [TestFixture]
    public class RoomEnclosureTests
    {
        sealed class Fixture
        {
            public readonly CellGrid Cells;
            public readonly List<PlacedEdifice> Edifices;
            public readonly EnclosureGrid Enclosure;
            public readonly GridSize Size;

            public Fixture(int sx = 20, int sz = 20, int sy = 4)
            {
                Size = new GridSize(sx, sz, sy);
                Cells = new CellGrid(Size);
                Edifices = new List<PlacedEdifice>();
                Enclosure = new EnclosureGrid(Cells, Edifices);

                // Floor layer 0 by default so pawns can walk
                for (int x = 0; x < sx; x++)
                for (int z = 0; z < sz; z++)
                {
                    Cells.Floor[Size.Index(x, z, 0)] = CoreContent.SlabBuilt;
                }
            }

            public int Cell(int x, int z, int y = 0) => Size.Index(x, z, y);

            public void BuildWall(int x, int z, int y = 0)
            {
                int c = Cell(x, z, y);
                Edifices.Add(new PlacedEdifice
                {
                    CellIndex = c,
                    Def = CoreContent.EdificeWall,
                    Removed = false
                });
                Cells.Edifice[c] = Edifices.Count - 1;
                Cells.Flags[c] |= CellFlags.BlockingEdifice;
                Enclosure.MarkDirty(c);
            }

            public void BuildDoor(int x, int z, int y = 0)
            {
                int c = Cell(x, z, y);
                Edifices.Add(new PlacedEdifice
                {
                    CellIndex = c,
                    Def = CoreContent.EdificeDoor,
                    Removed = false
                });
                Cells.Edifice[c] = Edifices.Count - 1;
                Enclosure.MarkDirty(c);
            }

            public void BuildRoof(int x, int z, int y = 1)
            {
                int c = Cell(x, z, y);
                Cells.Floor[c] = CoreContent.SlabBuilt;
                Enclosure.MarkDirty(c);
            }

            public void RemoveRoof(int x, int z, int y = 1)
            {
                int c = Cell(x, z, y);
                Cells.Floor[c] = CoreContent.SlabNone;
                Enclosure.MarkDirty(c);
            }

            public void Demolish(int x, int z, int y = 0)
            {
                int c = Cell(x, z, y);
                int handle = Cells.Edifice[c];
                if (handle >= 0 && handle < Edifices.Count)
                {
                    var ed = Edifices[handle];
                    ed.Removed = true;
                    Edifices[handle] = ed;
                    Cells.Edifice[c] = -1;
                    Cells.Flags[c] &= ~CellFlags.BlockingEdifice;
                    Enclosure.MarkDirty(c);
                }
            }

            /// <summary>
            /// Builds a rectangular room from (minX, minZ) to (maxX, maxZ) with a door at doorX, doorZ.
            /// </summary>
            public void BuildRoom(int minX, int minZ, int maxX, int maxZ, int doorX, int doorZ, bool withRoof = true)
            {
                for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    bool isPerimeter = x == minX || x == maxX || z == minZ || z == maxZ;
                    if (isPerimeter)
                    {
                        if (x == doorX && z == doorZ)
                            BuildDoor(x, z, 0);
                        else
                            BuildWall(x, z, 0);
                    }

                    if (withRoof)
                    {
                        BuildRoof(x, z, 1);
                    }
                }
            }
        }

        [Test]
        public void FullyEnclosedRoofedRoomIsIndoors()
        {
            var f = new Fixture();
            // 5x5 room from (5,5) to (9,9) with door at (7,5)
            f.BuildRoom(5, 5, 9, 9, doorX: 7, doorZ: 5, withRoof: true);

            // Interior cells
            Assert.That(f.Enclosure.IsIndoors(f.Cell(6, 6, 0)), Is.True, "(6,6) interior is indoors");
            Assert.That(f.Enclosure.IsIndoors(f.Cell(7, 7, 0)), Is.True, "(7,7) interior is indoors");
            Assert.That(f.Enclosure.IsIndoors(f.Cell(8, 8, 0)), Is.True, "(8,8) interior is indoors");

            // Door cell has roof overhead, bounds indoors space
            Assert.That(f.Enclosure.IsIndoors(f.Cell(7, 5, 0)), Is.True, "roofed door is indoors");

            // Outside cells
            Assert.That(f.Enclosure.IsIndoors(f.Cell(3, 3, 0)), Is.False, "(3,3) outdoors is not indoors");
            Assert.That(f.Enclosure.IsIndoors(f.Cell(7, 4, 0)), Is.False, "cell immediately outside door is outdoors");
        }

        [Test]
        public void MissingWallTileLeavesRoomOutdoors()
        {
            var f = new Fixture();
            f.BuildRoom(5, 5, 9, 9, doorX: 7, doorZ: 5, withRoof: true);

            // Create a hole in the north wall at (7, 9)
            f.Demolish(7, 9, 0);

            Assert.That(f.Enclosure.IsIndoors(f.Cell(6, 6, 0)), Is.False,
                "missing wall tile opens room to outdoors");
        }

        [Test]
        public void MissingDoorLeavesRoomOutdoorsAndBuildingDoorSealsIt()
        {
            var f = new Fixture();
            f.BuildRoom(5, 5, 9, 9, doorX: 7, doorZ: 5, withRoof: true);

            // Demolish the door, leaving an open doorway
            f.Demolish(7, 5, 0);

            Assert.That(f.Enclosure.IsIndoors(f.Cell(6, 6, 0)), Is.False,
                "open doorway without door leaves room outdoors");

            // Rebuild the door
            f.BuildDoor(7, 5, 0);

            Assert.That(f.Enclosure.IsIndoors(f.Cell(6, 6, 0)), Is.True,
                "building the door seals the room and makes it indoors");
        }

        [Test]
        public void MissingOverheadRoofTileLeavesRoomOutdoors()
        {
            var f = new Fixture();
            f.BuildRoom(5, 5, 9, 9, doorX: 7, doorZ: 5, withRoof: true);

            // Remove roof slab over interior cell (6, 6)
            f.RemoveRoof(6, 6, 1);

            Assert.That(f.Enclosure.IsIndoors(f.Cell(6, 6, 0)), Is.False,
                "room with missing roof tile is not enclosed");
            Assert.That(f.Enclosure.IsIndoors(f.Cell(7, 7, 0)), Is.False,
                "other cells in same unroofed room are also outdoors");

            // Put roof slab back
            f.BuildRoof(6, 6, 1);

            Assert.That(f.Enclosure.IsIndoors(f.Cell(6, 6, 0)), Is.True,
                "restoring the roof slab seals the room");
        }

        [Test]
        public void SolidMountainCaveIsIndoors()
        {
            var f = new Fixture();
            // Fill layer 1 with solid rock overhead (mountain roof)
            for (int x = 0; x < 20; x++)
            for (int z = 0; z < 20; z++)
            {
                f.Cells.Flags[f.Cell(x, z, 1)] |= CellFlags.SolidTerrain;
            }

            // Surround a 3x3 hollow on layer 0 with solid rock
            for (int x = 4; x <= 8; x++)
            for (int z = 4; z <= 8; z++)
            {
                bool perimeter = x == 4 || x == 8 || z == 4 || z == 8;
                if (perimeter)
                {
                    f.Cells.Flags[f.Cell(x, z, 0)] |= CellFlags.SolidTerrain;
                }
            }

            f.Enclosure.MarkAllDirty();

            Assert.That(f.Enclosure.IsIndoors(f.Cell(6, 6, 0)), Is.True,
                "cave carved out under mountain with solid rock roof is indoors");
        }

        [Test]
        public void DemolishingWallUnsealsRoom()
        {
            var f = new Fixture();
            f.BuildRoom(5, 5, 9, 9, doorX: 7, doorZ: 5, withRoof: true);

            Assert.That(f.Enclosure.IsIndoors(f.Cell(6, 6, 0)), Is.True);

            f.Demolish(5, 7, 0);

            Assert.That(f.Enclosure.IsIndoors(f.Cell(6, 6, 0)), Is.False,
                "demolishing a wall unseals the room");
        }
    }
}
