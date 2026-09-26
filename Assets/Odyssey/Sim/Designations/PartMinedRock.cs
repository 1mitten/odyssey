#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Designations
{
    /// <summary>
    /// The work already cut into rock whose mining order was taken off (design 57 §3): a sparse
    /// store holding a row <b>only for a cell somebody started on and then cancelled</b>, so the
    /// face keeps its cracks and a new order carries on from where the last one stopped.
    ///
    /// <para><b>Owner, 2026-09-26: "Keep its state."</b> Until then a cancel zeroed the cell's
    /// ledger, so the rock healed — and with cracks drawn on it, visibly. The ledger itself stays on
    /// <see cref="DesignationGrid"/> while an order stands; this holds it only between orders, and
    /// <see cref="DesignationGrid.Designate"/> hands it back.</para>
    ///
    /// <para><b>A row names the terrain it was cut from.</b> Nothing but a mining order takes rock
    /// out of the world today, and a new order consumes the row — but a row outliving a change of
    /// terrain would give some other stone a head start it never earned. So a row whose cell no
    /// longer holds that terrain is <em>stale</em>: never handed back, never published, and dropped
    /// the next time a row is kept.</para>
    ///
    /// <para><b>Sorted by cell, never a dictionary</b>, because the hash and the save walk it.
    /// Saved (<c>odyssey.partmined</c>, appended, no format bump) and hashed only while it has a
    /// row, so no colony that never cancelled a started cut hashes differently.</para>
    /// </summary>
    public sealed class PartMinedRock : IStateHashable, ISaveable
    {
        readonly List<int> _cells = new List<int>();
        readonly List<ushort> _terrain = new List<ushort>();
        readonly List<int> _work = new List<int>();

        public int Count => _cells.Count;

        /// <summary>The cell of the <paramref name="index"/>th row, by cell ascending.</summary>
        public int CellAt(int index) => _cells[index];

        /// <summary>The terrain the <paramref name="index"/>th row was cut from.</summary>
        public ushort TerrainAt(int index) => _terrain[index];

        /// <summary>The milliwork already done on the <paramref name="index"/>th row's cell.</summary>
        public int WorkAt(int index) => _work[index];

        /// <summary>Whether this cell holds work kept for it, from this terrain.</summary>
        public bool Holds(int cell, ushort terrain)
        {
            int at = _cells.BinarySearch(cell);
            return at >= 0 && _terrain[at] == terrain;
        }

        /// <summary>Keep the work cut into this cell. Nothing is kept for no work.</summary>
        public void Keep(int cell, ushort terrain, int milliwork)
        {
            int at = _cells.BinarySearch(cell);
            if (milliwork <= 0)
            {
                if (at >= 0) RemoveAt(at);
                return;
            }
            if (at >= 0)
            {
                _terrain[at] = terrain;
                _work[at] = milliwork;
                return;
            }
            at = ~at;
            _cells.Insert(at, cell);
            _terrain.Insert(at, terrain);
            _work.Insert(at, milliwork);
        }

        /// <summary>
        /// Hand back and forget the work kept for this cell, or false with nothing kept — or with
        /// a row cut from other terrain, which is forgotten too.
        /// </summary>
        public bool TryTake(int cell, ushort terrain, out int milliwork)
        {
            milliwork = 0;
            int at = _cells.BinarySearch(cell);
            if (at < 0) return false;
            bool same = _terrain[at] == terrain;
            if (same) milliwork = _work[at];
            RemoveAt(at);
            return same;
        }

        /// <summary>Drop every row whose cell no longer holds the terrain it was cut from.</summary>
        public void DropStale(ushort[] terrain)
        {
            for (int i = _cells.Count - 1; i >= 0; i--)
            {
                int cell = _cells[i];
                if ((uint)cell >= (uint)terrain.Length || terrain[cell] != _terrain[i]) RemoveAt(i);
            }
        }

        void RemoveAt(int at)
        {
            _cells.RemoveAt(at);
            _terrain.RemoveAt(at);
            _work.RemoveAt(at);
        }

        public void ContributeTo(ref StateHash hash)
        {
            // Nothing while no started cut has been cancelled, so no golden moves.
            if (_cells.Count == 0) return;
            hash.Add(_cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                hash.Add(_cells[i]);
                hash.Add(_terrain[i]);
                hash.Add(_work[i]);
            }
        }

        public string SaveKey => "odyssey.partmined";

        public void Save(SaveWriter writer)
        {
            writer.Write(_cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                writer.Write(_cells[i]);
                writer.Write((int)_terrain[i]);
                writer.Write(_work[i]);
            }
        }

        public void Load(SaveReader reader)
        {
            _cells.Clear();
            _terrain.Clear();
            _work.Clear();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int cell = reader.ReadInt();
                ushort terrain = (ushort)reader.ReadInt();
                int work = reader.ReadInt();
                Keep(cell, terrain, work);
            }
        }
    }
}
