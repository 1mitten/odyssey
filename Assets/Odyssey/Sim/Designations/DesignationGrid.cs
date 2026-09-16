#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Designations
{
    /// <summary>What the player has asked to happen to a cell. One per cell; a later order replaces an earlier one.</summary>
    public enum DesignationKind : byte
    {
        None = 0,

        /// <summary>Dig the cell out. Only on solid terrain.</summary>
        Mine = 1,

        /// <summary>Take a built thing apart. Only on a construction, never on a tree.</summary>
        Deconstruct = 2,

        /// <summary>Cut a tree down for wood. Only on a tree.</summary>
        Fell = 3,
    }

    /// <summary>
    /// The player's standing orders, one byte per cell.
    ///
    /// A designation is authored state: it is hashed, saved and published, and the simulation
    /// only ever reads it through the intents that create and cancel it. Work givers scan the
    /// designated cells rather than the whole grid, which is why the grid keeps a sorted list of
    /// them beside the byte array — a think scan over 230,000 cells per idle pawn would be the
    /// wrong price for finding three trees.
    ///
    /// Validation lives here and nowhere else, so a designation that exists is one that made
    /// sense when it was placed. Whether it still makes sense is the job's question: the tree
    /// may have been felled by somebody else, and the driver checks before it swings.
    /// </summary>
    public sealed class DesignationGrid : ITickable, IStateHashable, ISaveable, ISnapshotContributor
    {
        readonly CellGrid _grid;
        readonly IReadOnlyList<PlacedEdifice> _edifices;
        readonly byte[] _kinds;
        readonly List<int> _cells = new List<int>();

        public DesignationGrid(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _edifices = edifices ?? throw new ArgumentNullException(nameof(edifices));
            _kinds = new byte[grid.Size.CellCount];
        }

        public GridSize Size => _grid.Size;

        public DesignationKind At(int index) => (DesignationKind)_kinds[index];

        /// <summary>Every designated cell index, ascending. Stable order is what makes a scan deterministic.</summary>
        public IReadOnlyList<int> Cells => _cells;

        public int Count => _cells.Count;

        // ---- placing and cancelling --------------------------------------------------------

        /// <summary>
        /// Place an order, or say why not. Out of the map is <see cref="IntentRejection.OutOfBounds"/>;
        /// a kind the cell cannot take is <see cref="IntentRejection.NotPermitted"/>; the same order
        /// twice is <see cref="IntentRejection.AlreadyInThatState"/>.
        /// </summary>
        public IntentRejection Designate(CellRef cell, DesignationKind kind)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;
            if (kind == DesignationKind.None) return IntentRejection.NotPermitted;

            int index = _grid.Index(cell);
            if (!Allows(index, kind)) return IntentRejection.NotPermitted;
            if (_kinds[index] == (byte)kind) return IntentRejection.AlreadyInThatState;

            Set(index, kind);
            return IntentRejection.None;
        }

        public IntentRejection Cancel(CellRef cell)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;
            int index = _grid.Index(cell);
            if (_kinds[index] == 0) return IntentRejection.AlreadyInThatState;
            Set(index, DesignationKind.None);
            return IntentRejection.None;
        }

        /// <summary>Clear a cell without ceremony: the order was carried out.</summary>
        public void Clear(int index)
        {
            if (_kinds[index] != 0) Set(index, DesignationKind.None);
        }

        /// <summary>Whether the cell can take this kind of order right now.</summary>
        public bool Allows(int index, DesignationKind kind)
        {
            // Nothing is ordered in water. Every kind below would refuse it anyway today — mining
            // wants solid ground, felling wants a tree, deconstructing wants something built —
            // so this line changes no behaviour and is here for the kind that comes next, which
            // would otherwise have to rediscover that a river is not a building site. When the
            // build pipeline lands, the rule it needs is already stated, in the one place this
            // class's own doc says validation lives.
            if (IsWater(index)) return false;

            switch (kind)
            {
                case DesignationKind.Mine:
                    return _grid.IsSolidTerrain(index);
                case DesignationKind.Deconstruct:
                    return TryEdificeDef(index, out ushort built) && built < NaturalContent.FirstEdifice;
                case DesignationKind.Fell:
                    return IsTree(index);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Water of either depth. Shallow water is walkable, so a colonist may stand in it, and
        /// that is exactly why the question has to be asked separately from walkability: a cell
        /// you can wade through is still not one you can put a wall in.
        /// </summary>
        public bool IsWater(int index) => NaturalContent.IsWater(_grid.Terrain[index]);

        /// <summary>Whether a tree stands in the cell right now.</summary>
        public bool IsTree(int index) => TryEdificeDef(index, out ushort def) && NaturalContent.IsTree(def);

        bool TryEdificeDef(int index, out ushort def)
        {
            int handle = _grid.Edifice[index];
            if (handle < 0 || handle >= _edifices.Count)
            {
                def = 0;
                return false;
            }
            var placed = _edifices[handle];
            def = placed.Def;
            return !placed.Removed && def != CoreContent.EdificeNone;
        }

        void Set(int index, DesignationKind kind)
        {
            bool was = _kinds[index] != 0;
            _kinds[index] = (byte)kind;
            bool now = kind != DesignationKind.None;
            if (was == now) return;

            int at = _cells.BinarySearch(index);
            if (now) _cells.Insert(~at, index);
            else _cells.RemoveAt(at);
        }

        // ---- the intent seam -------------------------------------------------------------------

        /// <summary><c>Designate(cell, A = kind)</c>.</summary>
        public IntentRejection HandleDesignate(Intent intent)
        {
            if (intent.A <= 0 || intent.A > (int)DesignationKind.Fell) return IntentRejection.NotPermitted;
            return Designate(intent.Cell, (DesignationKind)intent.A);
        }

        /// <summary><c>CancelDesignation(cell)</c>.</summary>
        public IntentRejection HandleCancel(Intent intent) => Cancel(intent.Cell);

        /// <summary>Register everything this grid is: hashed state, saved state, a snapshot channel, two intents.</summary>
        public SimWorldBuilder Attach(SimWorldBuilder builder)
        {
            return builder
                .AddTickable(_ => this)
                .AddSnapshotContributor(this)
                .AddIntentHandler(IntentKind.Designate, HandleDesignate)
                .AddIntentHandler(IntentKind.CancelDesignation, HandleCancel);
        }

        // ---- ITickable: registration only, so the hash and the save see the orders -----------

        public TickGroup TickGroup => TickGroup.Never;
        public int TickPhaseOffset => 0;
        public void Tick(SimWorld world) { }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(_cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                hash.Add(_cells[i]);
                hash.Add(_kinds[_cells[i]]);
            }
        }

        // ---- ISaveable ------------------------------------------------------------------------

        public string SaveKey => "odyssey.designations";

        public void Save(SaveWriter writer)
        {
            writer.Write(_cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                writer.Write(_cells[i]);
                writer.Write(_kinds[_cells[i]]);
            }
        }

        public void Load(SaveReader reader)
        {
            Array.Clear(_kinds, 0, _kinds.Length);
            _cells.Clear();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int index = reader.ReadInt();
                byte kind = reader.ReadByte();
                if (index < 0 || index >= _kinds.Length || kind == 0) continue;
                Set(index, (DesignationKind)kind);
            }
        }

        // ---- ISnapshotContributor: one byte per cell of the active layer ---------------------

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            var size = _grid.Size;
            int layer = world.Views.SliceLayer;
            if (layer < 0) layer = 0;
            if (layer >= size.SizeY) layer = size.SizeY - 1;

            var channel = writer.BeginDesignations(size.LayerStride);
            new ReadOnlySpan<byte>(_kinds, layer * size.LayerStride, size.LayerStride).CopyTo(channel);
        }
    }
}
