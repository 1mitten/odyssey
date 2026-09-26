#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Hud
{
    /// <summary>
    /// A list of the cells on the board that answer yes to one question — every door, say — kept
    /// chunk by chunk and rescanned only where a chunk's version has moved (design 62 §2c, DM1).
    ///
    /// <para><b>Why it exists.</b> <c>DoorDirector</c> kept its door list against the render
    /// model's one board-wide version, so any edit anywhere — one mined cell — rescanned every cell
    /// on the board: 921,600 on Huge at 16 layers, 1.8 million at 32. The model has carried a
    /// version per chunk since P15; this is the list that reads it. A chunk whose version moved
    /// loses the cells it held and is scanned again; every other chunk keeps its answer. A full
    /// refresh stamps every chunk, so it still rescans everything, once.</para>
    ///
    /// <para><b>Engine-free and here, in <c>Odyssey.Hud</c></b>, for the reason the birds and the
    /// butterflies are: the fast tier can test it. It knows nothing about the render model — the
    /// caller hands it a chunk's version and a scan of one chunk — so any director that hunts the
    /// board for one kind of cell (the fire director does the same thing today) can keep one.</para>
    ///
    /// <para><b>Order.</b> <see cref="Cells"/> is ascending by chunk and, within a chunk, in the
    /// order the scan added them. It is not ascending by cell index across chunks, and nothing
    /// drawn from it may assume so.</para>
    /// </summary>
    public sealed class ChunkedCellList
    {
        readonly int[] _scannedAt;
        readonly bool[] _everScanned;
        readonly List<int>?[] _perChunk;
        readonly List<int> _scratch = new List<int>();
        readonly List<int> _cells = new List<int>();

        public ChunkedCellList(int chunkCount)
        {
            if (chunkCount < 0) throw new ArgumentOutOfRangeException(nameof(chunkCount));
            _scannedAt = new int[chunkCount];
            _everScanned = new bool[chunkCount];
            _perChunk = new List<int>?[chunkCount];
        }

        /// <summary>How many chunks the list covers.</summary>
        public int ChunkCount => _scannedAt.Length;

        /// <summary>Every cell found, over every chunk.</summary>
        public IReadOnlyList<int> Cells => _cells;

        /// <summary>How many chunks the last <see cref="Update"/> scanned. For the test that says
        /// an edit rescans the chunks it touched and no others.</summary>
        public int ChunksScannedLastUpdate { get; private set; }

        /// <summary>
        /// Bring the list up to date: scan every chunk whose version differs from the one it was
        /// last scanned at (every chunk, the first time), dropping what it held before.
        ///
        /// <para>A version that differs rather than one that is larger, so a caller handing the
        /// list a different board's versions cannot leave it silently stale. Returns whether
        /// anything was scanned.</para>
        /// </summary>
        /// <param name="chunkVersion">The version of a chunk, by chunk index.</param>
        /// <param name="scanChunk">Adds the matching cells of one chunk to the list it is given.</param>
        public bool Update(Func<int, int> chunkVersion, Action<int, List<int>> scanChunk)
        {
            int scanned = 0;
            for (int chunk = 0; chunk < _scannedAt.Length; chunk++)
            {
                int version = chunkVersion(chunk);
                if (_everScanned[chunk] && _scannedAt[chunk] == version) continue;
                _scannedAt[chunk] = version;
                _everScanned[chunk] = true;
                scanned++;

                _scratch.Clear();
                scanChunk(chunk, _scratch);
                List<int>? held = _perChunk[chunk];
                if (_scratch.Count == 0)
                {
                    held?.Clear();
                    continue;
                }
                if (held == null) _perChunk[chunk] = held = new List<int>(_scratch.Count);
                else held.Clear();
                held.AddRange(_scratch);
            }

            ChunksScannedLastUpdate = scanned;
            if (scanned == 0) return false;

            // Rebuilt whole from the per-chunk answers: O(chunks + cells), which is a few thousand
            // reads on the largest board, against the per-cell rescan this replaces.
            _cells.Clear();
            for (int chunk = 0; chunk < _perChunk.Length; chunk++)
            {
                List<int>? held = _perChunk[chunk];
                if (held != null && held.Count > 0) _cells.AddRange(held);
            }
            return true;
        }
    }
}
