#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// The underground nobody has opened (design 62 §6): one bit a cell, set on every cell the
    /// cavern pass carves and cleared when a cut breaks into the chamber. An unseen cell is air to
    /// the simulation — the colony cannot reach it, it is sealed — and rock to everything that
    /// shows the colony the world: the render mirror, the inspect pane, and every order the player
    /// can give. <see cref="CellGrid.SeenTerrain"/> is the one owner of that lie.
    ///
    /// <para><b>A bitset beside the flags, not a flag.</b> <see cref="CellFlags"/> has no free bit
    /// (0–7 are all taken) and the render mirror stores the flags as a byte, so a ninth bit would
    /// have widened every cell of the board for the few thousand cells that are ever in a cavern.
    /// This is an eighth of a byte a cell, and allocated only for a board that has a cavern.</para>
    ///
    /// <para><b>State, not derived.</b> A chamber the colony has broken into and then walled off
    /// again is sealed and seen; nothing about the cells can tell it from one never opened. So it
    /// is hashed and saved (<c>odyssey.unseen</c>, appended, no format bump) — and hashed only
    /// while it holds a cell, so a board with no caverns hashes as it always did.</para>
    ///
    /// <para><b>The breach is <see cref="RevealFrom"/></b>, a flood over the connected unseen cells
    /// by their faces, bounded by the chamber and run once per chamber: the cells it clears are
    /// never visited again.</para>
    /// </summary>
    public sealed class UnseenCells : IStateHashable, ISaveable
    {
        readonly GridSize _size;
        ulong[]? _words;
        int _count;

        public UnseenCells(GridSize size)
        {
            _size = size;
        }

        /// <summary>How many cells are unseen.</summary>
        public int Count => _count;

        /// <summary>Is this cell inside a chamber nobody has opened?</summary>
        public bool Contains(int index)
        {
            if (_words == null || (uint)index >= (uint)_size.CellCount) return false;
            return (_words[index >> 6] & (1UL << (index & 63))) != 0;
        }

        /// <summary>Mark a cell unseen. The cavern pass, and a load.</summary>
        public void Add(int index)
        {
            if ((uint)index >= (uint)_size.CellCount) return;
            _words ??= new ulong[(_size.CellCount + 63) >> 6];
            ulong bit = 1UL << (index & 63);
            ref ulong word = ref _words[index >> 6];
            if ((word & bit) != 0) return;
            word |= bit;
            _count++;
        }

        /// <summary>Forget a cell. True if it was unseen.</summary>
        public bool Remove(int index)
        {
            if (_words == null || (uint)index >= (uint)_size.CellCount) return false;
            ulong bit = 1UL << (index & 63);
            ref ulong word = ref _words[index >> 6];
            if ((word & bit) == 0) return false;
            word &= ~bit;
            _count--;
            return true;
        }

        public void Clear()
        {
            if (_words != null) System.Array.Clear(_words, 0, _words.Length);
            _count = 0;
        }

        /// <summary>
        /// A cell has just been opened: reveal every unseen chamber it touches.
        ///
        /// <para>Seeded from the opened cell itself (an order worked on a cell that was unseen, from
        /// a diagonal stance) and from its six face neighbours, then flooded by faces — six, not
        /// twenty-six, the rule <see cref="CellGrid.RevealAround"/> keeps for the same reason: two
        /// chambers that meet only at an edge are two chambers. Every cell cleared has its solid
        /// neighbours <see cref="CellFlags.Discovered"/>, so the ore on a cavern wall shows the
        /// moment the cavern does (<c>docs/research/mining-interview.md</c> §4c).</para>
        ///
        /// <para>The cells revealed are appended to <paramref name="revealed"/>, in the order
        /// cleared, for the caller to re-draw and to take orders off; the count is returned.
        /// Nothing unseen touching the cell costs six bit tests.</para>
        /// </summary>
        public int RevealFrom(CellGrid grid, int opened, List<int> revealed)
        {
            if (_count == 0 || (uint)opened >= (uint)_size.CellCount) return 0;

            int start = revealed.Count;
            if (Remove(opened)) revealed.Add(opened);
            Seed(opened, revealed);

            for (int i = start; i < revealed.Count; i++)
            {
                int cell = revealed[i];
                grid.RevealAround(cell);
                Seed(cell, revealed);
            }

            return revealed.Count - start;
        }

        /// <summary>Clear and queue every unseen face neighbour of a cell.</summary>
        void Seed(int index, List<int> into)
        {
            int stride = _size.LayerStride;
            CellRef at = _size.FromIndex(index);

            if (at.X > 0 && Remove(index - 1)) into.Add(index - 1);
            if (at.X < _size.SizeX - 1 && Remove(index + 1)) into.Add(index + 1);
            if (at.Z > 0 && Remove(index - _size.SizeX)) into.Add(index - _size.SizeX);
            if (at.Z < _size.SizeZ - 1 && Remove(index + _size.SizeX)) into.Add(index + _size.SizeX);
            if (at.Y > 0 && Remove(index - stride)) into.Add(index - stride);
            if (at.Y < _size.SizeY - 1 && Remove(index + stride)) into.Add(index + stride);
        }

        /// <summary>
        /// Hold the rule "an unseen chamber is walled by solid ground on every face" over whatever
        /// the grid holds now, and reveal any chamber that breaks it. Returns the cells revealed.
        ///
        /// <para><b>For the load path.</b> A save written by this build carries the bitset and
        /// this finds nothing. A save from before it has no section, so the board keeps the bits
        /// its seed generated — including the chambers that save had already broken into, which
        /// this is what opens again. A bit left on a solid cell (nothing writes one) is dropped.</para>
        ///
        /// <para>Walks the set bits only: the chambers' cells, never the board.</para>
        /// </summary>
        public int RevealBreached(CellGrid grid, List<int> revealed)
        {
            if (_words == null || _count == 0) return 0;
            int start = revealed.Count;

            // Found first, revealed after: a flood clears bits in words not yet walked, and a
            // walk that edited the set under itself would be a walk nobody could reason about.
            var breached = new List<int>();
            for (int w = 0; w < _words.Length; w++)
            {
                ulong bits = _words[w];
                while (bits != 0)
                {
                    int cell = (w << 6) | TrailingZeros(bits);
                    bits &= bits - 1;
                    if (grid.IsSolidTerrain(cell) || Breached(grid, cell)) breached.Add(cell);
                }
            }

            for (int i = 0; i < breached.Count; i++)
            {
                int cell = breached[i];
                if (!Contains(cell)) continue;           // cleared by a flood from a sibling
                if (grid.IsSolidTerrain(cell)) { Remove(cell); continue; }
                RevealFrom(grid, cell, revealed);
            }

            return revealed.Count - start;
        }

        /// <summary>Has this unseen cell an open face neighbour that is not itself unseen?</summary>
        bool Breached(CellGrid grid, int index)
        {
            int stride = _size.LayerStride;
            CellRef at = _size.FromIndex(index);
            return (at.X > 0 && Open(grid, index - 1))
                || (at.X < _size.SizeX - 1 && Open(grid, index + 1))
                || (at.Z > 0 && Open(grid, index - _size.SizeX))
                || (at.Z < _size.SizeZ - 1 && Open(grid, index + _size.SizeX))
                || (at.Y > 0 && Open(grid, index - stride))
                || (at.Y < _size.SizeY - 1 && Open(grid, index + stride));
        }

        bool Open(CellGrid grid, int index) => !grid.IsSolidTerrain(index) && !Contains(index);

        static int TrailingZeros(ulong value)
        {
            int n = 0;
            while ((value & 1UL) == 0) { value >>= 1; n++; }
            return n;
        }

        // ---- the hash and the save: sparse by word, in word order ------------------------------

        public void ContributeTo(ref StateHash hash)
        {
            // Nothing while no chamber is unseen, so a board without caverns hashes as it did.
            if (_words == null || _count == 0) return;
            hash.Add(_count);
            for (int w = 0; w < _words.Length; w++)
            {
                if (_words[w] == 0) continue;
                hash.Add(w);
                hash.Add(_words[w]);
            }
        }

        public string SaveKey => "odyssey.unseen";

        /// <summary>
        /// The words that hold a cell, each with its index: a count of nought and nothing else on
        /// a board with no chamber left unseen.
        /// </summary>
        public void Save(SaveWriter writer)
        {
            int words = 0;
            if (_words != null)
                for (int w = 0; w < _words.Length; w++) if (_words[w] != 0) words++;

            writer.Write(words);
            if (_words == null) return;
            for (int w = 0; w < _words.Length; w++)
            {
                if (_words[w] == 0) continue;
                writer.Write(w);
                writer.Write(unchecked((long)_words[w]));
            }
        }

        public void Load(SaveReader reader)
        {
            Clear();
            int words = reader.ReadInt();
            for (int i = 0; i < words; i++)
            {
                int w = reader.ReadInt();
                ulong bits = unchecked((ulong)reader.ReadLong());
                for (int b = 0; b < 64 && bits != 0; b++, bits >>= 1)
                    if ((bits & 1UL) != 0) Add((w << 6) | b);
            }
        }
    }
}
