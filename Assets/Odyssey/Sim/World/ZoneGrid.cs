#nullable enable
using System.Collections.Generic;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// Cells grouped into zones: the container a growing zone and a storage zone are both made of,
    /// and nothing else.
    ///
    /// <para><b>A container, not a rule.</b> Who may join whom is the owner's business and the two
    /// owners do not agree — a growing zone folds any touching zone of the same plant into itself,
    /// because a zone is identified by its crop and two carrot fields are interchangeable, while a
    /// storage zone must never fold, because it carries a configuration that a fold would silently
    /// destroy. So the join policy stays where it is and this holds only the bookkeeping: which
    /// cell is in which slot, which cells a slot owns, and the sorted whole-world list every scan
    /// walks.</para>
    ///
    /// <para><b>What it is worth extracting.</b> Two invariants in here cost a bug each before they
    /// were understood, both inside <c>GrowingZones</c> and both found late:</para>
    /// <list type="bullet">
    /// <item><description><see cref="MergeInto"/> re-sorts. Two sorted runs appended are not one
    /// sorted list, and everything that reads a slot's cells binary-searches them — which zone
    /// survives a fold depends on which neighbour the stroke met first, so the absorbed cells can
    /// be the smaller indices. Unsorted, a later <see cref="Leave"/> searches negatively and
    /// <c>RemoveAt</c> throws inside the intent drain.</description></item>
    /// <item><description><see cref="Dissolve"/> swaps rather than shifts. <c>List.RemoveAt</c>
    /// would move every later slot down one and leave all but the first of them wearing an index
    /// that <see cref="_zoneAt"/> no longer agrees with — a field painted around an obstacle
    /// fragments into several zones, one stroke folds them, and the next paint on a folded cell
    /// reads past the end of the list.</description></item>
    /// </list>
    ///
    /// <para><b>A slot index is not a durable name.</b> <see cref="Dissolve"/> moves the last slot
    /// into the vacated one, so an index held across an edit can name a different zone afterwards.
    /// Hold a cell and ask <see cref="SlotAt"/>, exactly as the intent handlers do; nothing saves a
    /// slot index and nothing hashes one.</para>
    /// </summary>
    public sealed class ZoneGrid
    {
        /// <summary>The slot each cell belongs to, or -1. Full-size for O(1) answers, as the designation grid's byte array is.</summary>
        readonly int[] _zoneAt;

        /// <summary>Every zoned cell, ascending — the walk order for a hash, a save and a snapshot.</summary>
        readonly List<int> _cells = new List<int>();

        /// <summary>The cells each slot owns, ascending.</summary>
        readonly List<List<int>> _slots = new List<List<int>>();

        /// <summary>The owner's payload per slot: a plant handle for a field, a settings id for a store.</summary>
        readonly List<int> _tags = new List<int>();

        public ZoneGrid(int cellCount)
        {
            _zoneAt = new int[cellCount];
            System.Array.Fill(_zoneAt, -1);
        }

        /// <summary>How many zones exist. Slots are 0 to this less one.</summary>
        public int Count => _slots.Count;

        /// <summary>Every zoned cell index, ascending. Stable order is what makes a scan deterministic.</summary>
        public IReadOnlyList<int> Cells => _cells;

        /// <summary>The slot covering this cell, or -1 when it is in no zone.</summary>
        public int SlotAt(int cell) => _zoneAt[cell];

        /// <summary>The payload of this slot.</summary>
        public int TagOf(int slot) => _tags[slot];

        /// <summary>Change a slot's payload, which nothing about the geometry cares about.</summary>
        public void Retag(int slot, int tag) => _tags[slot] = tag;

        /// <summary>The cells this slot owns, ascending.</summary>
        public IReadOnlyList<int> CellsOf(int slot) => _slots[slot];

        /// <summary>Open a new, empty zone and return its slot.</summary>
        public int Found(int tag)
        {
            _slots.Add(new List<int>());
            _tags.Add(tag);
            return _slots.Count - 1;
        }

        /// <summary>Put a cell in a slot, keeping both lists ascending. The cell must be in no zone.</summary>
        public void Join(int slot, int cell)
        {
            List<int> cells = _slots[slot];
            cells.Insert(~cells.BinarySearch(cell), cell);
            _cells.Insert(~_cells.BinarySearch(cell), cell);
            _zoneAt[cell] = slot;
        }

        /// <summary>
        /// Take a cell out of whatever slot holds it, dissolving a slot that empties. False when
        /// the cell was in no zone, which is an ordinary answer and not a failure.
        /// </summary>
        public bool Leave(int cell)
        {
            int slot = _zoneAt[cell];
            if (slot < 0) return false;

            List<int> cells = _slots[slot];
            cells.RemoveAt(cells.BinarySearch(cell));
            _cells.RemoveAt(_cells.BinarySearch(cell));
            _zoneAt[cell] = -1;
            if (cells.Count == 0) Dissolve(slot, -1);
            return true;
        }

        /// <summary>
        /// Fold <paramref name="other"/> into <paramref name="home"/> and take its slot off the
        /// list. Returns the surviving home slot, <b>which is not always the one passed in</b>:
        /// dissolving a slot moves the last one into the gap, and the last one can be home.
        /// </summary>
        public int MergeInto(int home, int other)
        {
            if (home == other) return home;

            List<int> homeCells = _slots[home];
            List<int> otherCells = _slots[other];
            homeCells.AddRange(otherCells);
            homeCells.Sort();

            int survivor = Dissolve(other, home);

            // The absorbed cells still point at the slot their old zone died in, and a stroke that
            // caused the merge is often still reading neighbours — one of them could sit in an
            // absorbed cell. Point them at the survivor now; every later read sees a live answer.
            for (int i = 0; i < otherCells.Count; i++) _zoneAt[otherCells[i]] = survivor;
            return survivor;
        }

        /// <summary>Forget every zone. The load path's first move.</summary>
        public void Clear()
        {
            System.Array.Fill(_zoneAt, -1);
            _cells.Clear();
            _slots.Clear();
            _tags.Clear();
        }

        /// <summary>
        /// Put a cell in a slot <b>without</b> keeping order — the load path, which reads zone by
        /// zone and finishes with <see cref="RestoreOrder"/>. Cells within one saved zone arrive
        /// ascending, but the whole-world list does not, so order is restored once at the end
        /// rather than maintained by every append.
        /// </summary>
        public void Append(int slot, int cell)
        {
            _slots[slot].Add(cell);
            _cells.Add(cell);
            _zoneAt[cell] = slot;
        }

        /// <summary>Sort everything <see cref="Append"/> left unsorted.</summary>
        public void RestoreOrder()
        {
            _cells.Sort();
            for (int i = 0; i < _slots.Count; i++) _slots[i].Sort();
        }

        /// <summary>
        /// Take a slot off the list, moving the last slot into the gap and repointing its cells.
        /// <paramref name="track"/> is a slot the caller still needs the index of; its new index
        /// is returned.
        /// </summary>
        int Dissolve(int slot, int track)
        {
            int last = _slots.Count - 1;
            if (slot != last)
            {
                _slots[slot] = _slots[last];
                _tags[slot] = _tags[last];
                List<int> moved = _slots[slot];
                for (int i = 0; i < moved.Count; i++) _zoneAt[moved[i]] = slot;
                if (track == last) track = slot;
            }

            _slots.RemoveAt(last);
            _tags.RemoveAt(last);
            return track;
        }
    }
}
