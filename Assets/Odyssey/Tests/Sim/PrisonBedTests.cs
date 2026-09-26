#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The prison bed and the cell it makes (design 60 §5b): what is saved is what the player
    /// marked, what a bed is for is derived from its room, and a prison bed with no room holds
    /// its prisoner shackled. The rooms are the real enclosure grid over a hand-built board, the
    /// way the temperature tests build theirs, so what is tested is the rule and not a stand-in.
    /// </summary>
    public class PrisonBedTests
    {
        sealed class Board
        {
            public readonly GridSize Size = new GridSize(20, 20, 4);
            public readonly CellGrid Cells;
            public readonly List<PlacedEdifice> Edifices = new List<PlacedEdifice>();
            public readonly EnclosureGrid Enclosure;
            public readonly BedPurposes Purposes;

            public Board()
            {
                Cells = new CellGrid(Size);
                for (int x = 0; x < Size.SizeX; x++)
                for (int z = 0; z < Size.SizeZ; z++)
                    Cells.Floor[Size.Index(x, z, 0)] = CoreContent.SlabBuilt;
                Enclosure = new EnclosureGrid(Cells, Edifices);
                Purposes = new BedPurposes(Cells, Edifices) { Enclosure = Enclosure };
            }

            public int Cell(int x, int z, int y = 0) => Size.Index(x, z, y);

            public void Wall(int x, int z, int y = 0)
            {
                int c = Cell(x, z, y);
                Edifices.Add(new PlacedEdifice { CellIndex = c, Def = CoreContent.EdificeWall, Stuff = CoreContent.StuffConcrete, Built = true });
                Cells.Edifice[c] = Edifices.Count - 1;
                Cells.Flags[c] |= CellFlags.BlockingEdifice;
                Enclosure.MarkDirty(c);
            }

            public void Unwall(int x, int z, int y = 0)
            {
                int c = Cell(x, z, y);
                int handle = Cells.Edifice[c];
                PlacedEdifice gone = Edifices[handle];
                gone.Removed = true;
                Edifices[handle] = gone;
                Cells.Edifice[c] = -1;
                Cells.Flags[c] &= ~CellFlags.BlockingEdifice;
                Enclosure.MarkDirty(c);
            }

            /// <summary>Walls round the rectangle's edge on layer 0 and a roof of slabs over its inside.</summary>
            public int Room(int x0, int z0, int x1, int z1)
            {
                for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                    if (x == x0 || x == x1 || z == z0 || z == z1) Wall(x, z);
                for (int x = x0 + 1; x < x1; x++)
                for (int z = z0 + 1; z < z1; z++)
                {
                    int c = Cell(x, z, 1);
                    Cells.Floor[c] = CoreContent.SlabBuilt;
                    Enclosure.MarkDirty(c);
                }
                return Enclosure.RoomAt(Cell(x0 + 1, z0 + 1));
            }

            public int Bed(int x, int z)
            {
                int c = Cell(x, z);
                Edifices.Add(new PlacedEdifice { CellIndex = c, Def = CoreContent.EdificeBed, Built = true, Quality = 3 });
                Cells.Edifice[c] = Edifices.Count - 1;
                return c;
            }

            public BedPurpose At(int cell) => Purposes.PurposeAt(cell);
        }

        const int Prison = (int)BedPurpose.Prison, Colony = (int)BedPurpose.Colony;

        [Test]
        public void EveryBedIsAColonyBedUntilOneIsMarked()
        {
            var b = new Board();
            b.Room(2, 2, 8, 8);
            int bed = b.Bed(4, 4);
            Assert.That(b.At(bed), Is.EqualTo(BedPurpose.Colony));
            Assert.That(b.Purposes.Any, Is.False);
            Assert.That(b.At(b.Cell(10, 10)), Is.EqualTo(BedPurpose.Colony), "a cell with no bed");
        }

        [Test]
        public void MarkingOneBedMarksEveryBedInItsRoomAndNoneOutside()
        {
            var b = new Board();
            int room = b.Room(2, 2, 8, 8);
            Assume.That(room, Is.Not.Zero, "the fixture built a room");
            int first = b.Bed(3, 3), second = b.Bed(6, 6), outside = b.Bed(12, 12);

            Assert.That(b.Purposes.SetPurpose(first, Prison), Is.EqualTo(IntentRejection.None));
            Assert.That(b.At(first), Is.EqualTo(BedPurpose.Prison));
            Assert.That(b.At(second), Is.EqualTo(BedPurpose.Prison), "the room is a cell");
            Assert.That(b.At(outside), Is.EqualTo(BedPurpose.Colony), "a bed in the open is not");
            Assert.That(b.Purposes.Marked, Is.EqualTo(new[] { first, second }), "both stored, ascending");
            Assert.That(b.Purposes.IsShackled(first), Is.False);
            Assert.That(b.Purposes.IsCell(room), Is.True);

            Assert.That(b.Purposes.SetPurpose(second, Prison), Is.EqualTo(IntentRejection.AlreadyInThatState));
        }

        [Test]
        public void ABedBuiltLaterInACellIsAPrisonBed()
        {
            var b = new Board();
            b.Room(2, 2, 8, 8);
            int first = b.Bed(3, 3);
            b.Purposes.SetPurpose(first, Prison);

            int later = b.Bed(6, 3);
            Assert.That(b.At(later), Is.EqualTo(BedPurpose.Prison), "derived from the room, with no order");
            Assert.That(b.Purposes.Marked, Has.No.Member(later), "and not stored");
        }

        [Test]
        public void UnmarkingOneBedUnmarksTheWholeRoom()
        {
            var b = new Board();
            b.Room(2, 2, 8, 8);
            int first = b.Bed(3, 3), second = b.Bed(6, 6);
            b.Purposes.SetPurpose(first, Prison);

            Assert.That(b.Purposes.SetPurpose(second, Colony), Is.EqualTo(IntentRejection.None));
            Assert.That(b.At(first), Is.EqualTo(BedPurpose.Colony));
            Assert.That(b.At(second), Is.EqualTo(BedPurpose.Colony));
            Assert.That(b.Purposes.Any, Is.False);
        }

        /// <summary>
        /// <b>A wall knocked out leaves shackle beds, not colony beds.</b> Both beds were stored when
        /// the room was marked, so the player's intent survives the room breaking: a prisoner in one
        /// is still held, on her bed, rather than finding herself in a colonist's.
        /// </summary>
        [Test]
        public void KnockingOutAWallLeavesShackleBeds()
        {
            var b = new Board();
            b.Room(2, 2, 8, 8);
            int first = b.Bed(3, 3), second = b.Bed(6, 6);
            b.Purposes.SetPurpose(first, Prison);

            b.Unwall(5, 2);
            Assert.That(b.Purposes.RoomOf(first), Is.Zero, "the room is gone");
            Assert.That(b.At(first), Is.EqualTo(BedPurpose.Prison));
            Assert.That(b.At(second), Is.EqualTo(BedPurpose.Prison));
            Assert.That(b.Purposes.IsShackled(first), Is.True);
            Assert.That(b.Purposes.IsShackled(second), Is.True);
        }

        [Test]
        public void APrisonBedInTheOpenIsAShackleBed()
        {
            var b = new Board();
            int bed = b.Bed(10, 10);
            Assert.That(b.Purposes.SetPurpose(bed, Prison), Is.EqualTo(IntentRejection.None));
            Assert.That(b.At(bed), Is.EqualTo(BedPurpose.Prison));
            Assert.That(b.Purposes.IsShackled(bed), Is.True);
        }

        [Test]
        public void ABedThatComesDownIsForgotten()
        {
            var b = new Board();
            int bed = b.Bed(10, 10);
            b.Purposes.SetPurpose(bed, Prison);
            int version = b.Purposes.Version;
            b.Purposes.Forget(bed);
            Assert.That(b.Purposes.Any, Is.False);
            Assert.That(b.Purposes.Version, Is.GreaterThan(version));
        }

        [Test]
        public void TheCellsAreNotRewalkedWhenNothingChanged()
        {
            var b = new Board();
            b.Room(2, 2, 8, 8);
            int bed = b.Bed(3, 3);
            b.Purposes.SetPurpose(bed, Prison);
            b.At(bed);
            long key = b.Purposes.StateKey;
            for (int i = 0; i < 10; i++) b.At(bed);
            Assert.That(b.Purposes.StateKey, Is.EqualTo(key), "asking does not move the key");

            b.Unwall(5, 2);
            b.At(bed);
            Assert.That(b.Purposes.StateKey, Is.Not.EqualTo(key), "a changed room does");
        }

        [Test]
        public void TwoCellsOnTwoLayersAreTwoCells()
        {
            var b = new Board();
            int low = b.Room(2, 2, 8, 8);
            // A second room on layer 1, over the first one's roof, roofed at layer 2.
            for (int x = 2; x <= 8; x++)
            for (int z = 2; z <= 8; z++)
                if (x == 2 || x == 8 || z == 2 || z == 8) b.Wall(x, z, 1);
            for (int x = 3; x < 8; x++)
            for (int z = 3; z < 8; z++)
            {
                int c = b.Cell(x, z, 2);
                b.Cells.Floor[c] = CoreContent.SlabBuilt;
                b.Enclosure.MarkDirty(c);
            }
            int high = b.Enclosure.RoomAt(b.Cell(4, 4, 1));
            Assume.That(high, Is.Not.Zero.And.Not.EqualTo(low));

            int lowBed = b.Bed(4, 4);
            b.Purposes.SetPurpose(lowBed, Prison);
            int upper = b.Cell(5, 5, 1);
            b.Edifices.Add(new PlacedEdifice { CellIndex = upper, Def = CoreContent.EdificeBed, Built = true, Quality = 3 });
            b.Cells.Edifice[upper] = b.Edifices.Count - 1;

            Assert.That(b.At(lowBed), Is.EqualTo(BedPurpose.Prison));
            Assert.That(b.At(upper), Is.EqualTo(BedPurpose.Colony), "the room above is not the cell below it");
        }

        [Test]
        public void TheMarksSurviveASaveAndAreHashedOnlyWhileThereAreAny()
        {
            var b = new Board();
            var empty = new StateHash();
            b.Purposes.ContributeTo(ref empty);
            Assert.That(empty.Value, Is.EqualTo(new StateHash().Value), "a board with none hashes as before");

            int bed = b.Bed(10, 10);
            b.Purposes.SetPurpose(bed, Prison);
            var marked = new StateHash();
            b.Purposes.ContributeTo(ref marked);
            Assert.That(marked.Value, Is.Not.EqualTo(new StateHash().Value));

            using var stream = new MemoryStream();
            using (var binary = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
                b.Purposes.Save(new SaveWriter(binary));
            stream.Position = 0;

            var fresh = new Board();
            fresh.Bed(10, 10);
            using (var reader = new BinaryReader(stream))
                fresh.Purposes.Load(new SaveReader(reader, WorldSave.CurrentFormatVersion));
            Assert.That(fresh.Purposes.Marked, Is.EqualTo(new[] { bed }));
            Assert.That(fresh.At(bed), Is.EqualTo(BedPurpose.Prison));
        }

        [Test]
        public void OnlyABuiltBedCanBeMarked()
        {
            var b = new Board();
            Assert.That(b.Purposes.SetPurpose(b.Cell(10, 10), Prison), Is.EqualTo(IntentRejection.NotPermitted));
            int bed = b.Bed(10, 10);
            Assert.That(b.Purposes.SetPurpose(bed, 7), Is.EqualTo(IntentRejection.NotPermitted), "not a purpose");
        }
    }
}
