#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// How much of each struck building is left (design 33 §4, C6): a sparse store of hit points
    /// by cell, holding a row <b>only for a building that has been hit</b>. A building nobody has
    /// struck is at its <c>BuildingDef.maxHitPoints</c> and has no row, so the store is empty in
    /// every colony that has never fought one.
    ///
    /// <para><b>Written by C6</b> (design 33 §13): <see cref="CombatSystem.StrikeBuilding"/> sets a
    /// row, on the building's own cell (a two-cell bed or generator keys on its head), and
    /// <c>ConstructionGrid.Demolish</c> — the one way an edifice leaves the world — clears it, so a
    /// row cannot outlive its building by any route. A struck building's pool is
    /// <see cref="BuildingTargets.MaxMilliOf"/>, the row times its material.</para>
    ///
    /// <para><b>Sorted by cell, never a dictionary</b>, because the hash and the save walk it and a
    /// hash table's order is not allowed to be a simulation input. Saved (<c>odyssey.edificedamage</c>)
    /// and hashed only while it has a row. <b>Scales with</b> the buildings that have been hit.</para>
    /// </summary>
    public sealed class EdificeDamage : IStateHashable, ISaveable
    {
        readonly List<int> _cells = new List<int>();
        readonly List<int> _hpMilli = new List<int>();

        public int Count => _cells.Count;

        /// <summary>The cell of the <paramref name="index"/>th row, by cell ascending.</summary>
        public int CellAt(int index) => _cells[index];

        /// <summary>What is left of the <paramref name="index"/>th row's building, in thousandths.</summary>
        public int HpMilliAt(int index) => _hpMilli[index];

        /// <summary>The hit points left on the building in this cell, in thousandths, or false for one never struck.</summary>
        public bool TryGet(int cell, out int hpMilli)
        {
            int at = _cells.BinarySearch(cell);
            if (at < 0) { hpMilli = 0; return false; }
            hpMilli = _hpMilli[at];
            return true;
        }

        /// <summary>Record what is left of the building in this cell.</summary>
        public void Set(int cell, int hpMilli)
        {
            int at = _cells.BinarySearch(cell);
            if (at >= 0) { _hpMilli[at] = hpMilli; return; }
            at = ~at;
            _cells.Insert(at, cell);
            _hpMilli.Insert(at, hpMilli);
        }

        /// <summary>Forget the cell: the building was demolished, rebuilt or repaired whole.</summary>
        public void Clear(int cell)
        {
            int at = _cells.BinarySearch(cell);
            if (at < 0) return;
            _cells.RemoveAt(at);
            _hpMilli.RemoveAt(at);
        }

        public void ContributeTo(ref StateHash hash)
        {
            // Nothing while no building has been struck (design 33 §5).
            if (_cells.Count == 0) return;
            hash.Add(_cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                hash.Add(_cells[i]);
                hash.Add(_hpMilli[i]);
            }
        }

        public string SaveKey => "odyssey.edificedamage";

        public void Save(SaveWriter writer)
        {
            writer.Write(_cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                writer.Write(_cells[i]);
                writer.Write(_hpMilli[i]);
            }
        }

        public void Load(SaveReader reader)
        {
            _cells.Clear();
            _hpMilli.Clear();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++) Set(reader.ReadInt(), reader.ReadInt());
        }
    }
}
