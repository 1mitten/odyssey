#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Odyssey.Hud;

namespace Odyssey.Tests.Hud
{
    /// <summary>
    /// The door list's rescan rule (design 62 §2c, DM1): a chunk is scanned again only when its
    /// version moves, what it held before is dropped, and a board-wide refresh rescans everything
    /// once. A fake board of four chunks of ten cells, each cell a door or not.
    /// </summary>
    public class ChunkedCellListTests
    {
        const int Chunks = 4;
        const int CellsPerChunk = 10;

        sealed class FakeBoard
        {
            public readonly int[] Version = new int[Chunks];
            public readonly bool[] Door = new bool[Chunks * CellsPerChunk];
            public int CellsRead;

            public void Scan(int chunk, List<int> into)
            {
                for (int c = chunk * CellsPerChunk; c < (chunk + 1) * CellsPerChunk; c++)
                {
                    CellsRead++;
                    if (Door[c]) into.Add(c);
                }
            }

            public void Edit(int cell, bool door)
            {
                Door[cell] = door;
                Version[cell / CellsPerChunk]++;
            }

            public void RefreshAll()
            {
                for (int i = 0; i < Chunks; i++) Version[i]++;
            }
        }

        static bool Update(ChunkedCellList list, FakeBoard board) =>
            list.Update(i => board.Version[i], board.Scan);

        [Test]
        public void TheFirstUpdateScansEveryChunk()
        {
            var board = new FakeBoard();
            board.Door[3] = true;
            board.Door[27] = true;
            var list = new ChunkedCellList(Chunks);

            Assert.That(Update(list, board), Is.True);
            Assert.That(list.ChunksScannedLastUpdate, Is.EqualTo(Chunks));
            Assert.That(list.Cells, Is.EqualTo(new[] { 3, 27 }));
        }

        [Test]
        public void NothingMovedScansNothing()
        {
            var board = new FakeBoard();
            board.Door[3] = true;
            var list = new ChunkedCellList(Chunks);
            Update(list, board);
            board.CellsRead = 0;

            Assert.That(Update(list, board), Is.False);
            Assert.That(list.ChunksScannedLastUpdate, Is.Zero);
            Assert.That(board.CellsRead, Is.Zero, "a still board was read again");
            Assert.That(list.Cells, Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public void AnEditRescansOnlyItsChunkAndDropsWhatItHeld()
        {
            var board = new FakeBoard();
            board.Door[3] = true;
            board.Door[15] = true;
            board.Door[35] = true;
            var list = new ChunkedCellList(Chunks);
            Update(list, board);
            board.CellsRead = 0;

            // The door in chunk 1 comes down and another goes up in the same chunk.
            board.Edit(15, false);
            board.Edit(18, true);
            Update(list, board);

            Assert.That(list.ChunksScannedLastUpdate, Is.EqualTo(1));
            Assert.That(board.CellsRead, Is.EqualTo(CellsPerChunk), "more than the edited chunk was read");
            Assert.That(list.Cells, Is.EqualTo(new[] { 3, 18, 35 }), "the old door survived its chunk's rescan");
        }

        [Test]
        public void AChunkThatLosesItsLastDoorHoldsNone()
        {
            var board = new FakeBoard();
            board.Door[22] = true;
            var list = new ChunkedCellList(Chunks);
            Update(list, board);

            board.Edit(22, false);
            Update(list, board);

            Assert.That(list.Cells, Is.Empty);
        }

        [Test]
        public void AFullRefreshRescansEverythingOnce()
        {
            var board = new FakeBoard();
            board.Door[3] = true;
            var list = new ChunkedCellList(Chunks);
            Update(list, board);

            board.RefreshAll();
            Assert.That(Update(list, board), Is.True);
            Assert.That(list.ChunksScannedLastUpdate, Is.EqualTo(Chunks));
            Assert.That(Update(list, board), Is.False, "a refresh was paid for twice");
        }

        /// <summary>
        /// The whole point, against the rule it replaces: the list after any edits is exactly what
        /// a scan of every cell would find. Randomised over a fixed seed so a failure repeats.
        /// </summary>
        [Test]
        public void AfterAnyEditsItMatchesAFullScan()
        {
            var board = new FakeBoard();
            var list = new ChunkedCellList(Chunks);
            var random = new System.Random(62);
            for (int round = 0; round < 200; round++)
            {
                int edits = random.Next(0, 4);
                for (int e = 0; e < edits; e++) board.Edit(random.Next(board.Door.Length), random.Next(2) == 0);
                if (random.Next(20) == 0) board.RefreshAll();
                Update(list, board);

                int[] truth = Enumerable.Range(0, board.Door.Length).Where(c => board.Door[c]).ToArray();
                Assert.That(list.Cells.OrderBy(c => c).ToArray(), Is.EqualTo(truth), $"round {round}");
            }
        }
    }
}
