#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;

namespace Odyssey.Sim.Pawns
{
    /// <summary>A loose thing: something to haul, or something to eat.</summary>
    public sealed class ColonyItem
    {
        public ThingId Id;
        public int DefIndex;

        /// <summary>Cell index, or -1 while a pawn is carrying it.</summary>
        public int Cell = -1;

        public int Stack = 1;

        /// <summary>The player's cheap "not now" veto. Forbidden things are invisible to scanning.</summary>
        public bool Forbidden;

        /// <summary>Pawn id value of the carrier, or 0.</summary>
        public int CarriedBy;

        public bool Despawned;
    }

    /// <summary>
    /// A set of cells on <b>exactly one layer</b> sharing a storage setting.
    ///
    /// Per-layer is the answer layer question 6 settled on: contiguity across a stairwell is
    /// meaningless and capacity belongs to a floor rather than to a volume. The "one warehouse
    /// across three floors" case comes back later as a storage group — several per-layer zones
    /// sharing one settings record — which is geometry per layer and configuration grouped.
    /// </summary>
    public sealed class Stockpile
    {
        public Stockpile(int priority, int[] cells, bool[] allow)
        {
            Priority = priority;
            Cells = cells;
            Allow = allow;
            System.Array.Sort(Cells);
        }

        /// <summary>Higher wins. Priority orders the destination, never the haul queue.</summary>
        public int Priority;

        /// <summary>Ascending cell index, so scans are ordered without sorting at tick time.</summary>
        public readonly int[] Cells;

        /// <summary>The filter, by item def index. Shared in shape with bill ingredients later.</summary>
        public readonly bool[] Allow;

        public bool Accepts(int defIndex) => defIndex >= 0 && defIndex < Allow.Length && Allow[defIndex];
    }

    /// <summary>
    /// The minimum world-of-things the pawn simulation needs to be worth watching: loose items,
    /// per-layer stockpiles, and beds.
    ///
    /// <b>This is a placeholder for the things line of work.</b> A real thing has a ThingDef, a
    /// tick group, stuff, hit points and a stack, and it belongs in the Things phase with its own
    /// system. What is here is exactly what haul, eat and sleep need and no more, so that when
    /// the real thing arrives this can be deleted rather than migrated.
    /// </summary>
    public sealed class ColonyItems : ISaveable
    {
        readonly List<ColonyItem> _items = new List<ColonyItem>();
        readonly Dictionary<int, int> _itemAtCell = new Dictionary<int, int>();
        readonly List<int> _loose = new List<int>();
        readonly List<Stockpile> _stockpiles = new List<Stockpile>();
        readonly Dictionary<int, int> _stockpileAtCell = new Dictionary<int, int>();
        readonly List<int> _beds = new List<int>();

        int _nextId = 1;

        public PawnContent Content { get; }

        public ColonyItems(PawnContent content) { Content = content; }

        /// <summary>Every item ever spawned, in id order. Despawned entries stay as tombstones.</summary>
        public IReadOnlyList<ColonyItem> Items => _items;

        /// <summary>
        /// The haulables lister: item indices that are on the ground outside any stockpile,
        /// kept ascending and maintained incrementally. The research is emphatic that this must
        /// never be a whole-map sweep, because a ruined city starts with thousands of haulables.
        /// </summary>
        public IReadOnlyList<int> LooseItems => _loose;

        public IReadOnlyList<Stockpile> Stockpiles => _stockpiles;

        /// <summary>Bed cells, ascending.</summary>
        public IReadOnlyList<int> Beds => _beds;

        public ColonyItem? Get(ThingId id)
        {
            int index = id.Value - 1;
            if (index < 0 || index >= _items.Count) return null;
            var item = _items[index];
            return item.Despawned ? null : item;
        }

        public ColonyItem? ItemAt(int cell) =>
            _itemAtCell.TryGetValue(cell, out int index) ? _items[index] : null;

        public ThingId Spawn(int defIndex, int cell, int stack = 1)
        {
            var item = new ColonyItem { Id = new ThingId(_nextId++), DefIndex = defIndex, Cell = cell, Stack = stack };
            _items.Add(item);
            int index = _items.Count - 1;
            _itemAtCell[cell] = index;
            if (!IsStockpileCell(cell)) InsertLoose(index);
            return item.Id;
        }

        public void AddStockpile(Stockpile stockpile)
        {
            int index = _stockpiles.Count;
            _stockpiles.Add(stockpile);
            for (int i = 0; i < stockpile.Cells.Length; i++)
            {
                _stockpileAtCell[stockpile.Cells[i]] = index;

                // Anything already lying in the new zone stops being loose.
                if (_itemAtCell.TryGetValue(stockpile.Cells[i], out int item)) RemoveLoose(item);
            }
        }

        public void AddBed(int cell)
        {
            int at = _beds.BinarySearch(cell);
            if (at < 0) _beds.Insert(~at, cell);
        }

        public bool IsStockpileCell(int cell) => _stockpileAtCell.ContainsKey(cell);

        public Stockpile? StockpileAt(int cell) =>
            _stockpileAtCell.TryGetValue(cell, out int index) ? _stockpiles[index] : null;

        public void PickUp(ColonyItem item, PawnId carrier)
        {
            if (item.Cell >= 0)
            {
                _itemAtCell.Remove(item.Cell);
                RemoveLoose(item.Id.Value - 1);
            }
            item.Cell = -1;
            item.CarriedBy = carrier.Value;
        }

        public void Drop(ColonyItem item, int cell)
        {
            item.Cell = cell;
            item.CarriedBy = 0;
            int index = item.Id.Value - 1;
            _itemAtCell[cell] = index;
            if (IsStockpileCell(cell)) RemoveLoose(index);
            else InsertLoose(index);
        }

        public void Despawn(ColonyItem item)
        {
            if (item.Cell >= 0) _itemAtCell.Remove(item.Cell);
            RemoveLoose(item.Id.Value - 1);
            item.Cell = -1;
            item.CarriedBy = 0;
            item.Despawned = true;
        }

        /// <summary>Is this cell free to take one more item?</summary>
        public bool CellHasSpace(int cell) => !_itemAtCell.ContainsKey(cell);

        /// <summary>
        /// <c>SetForbidden(A = thing, B = on)</c>. Forbidding is the player taking a thing out of
        /// every scan without moving it: a hauler ignores a forbidden item and takes it once it is
        /// allowed again.
        /// </summary>
        public IntentRejection HandleSetForbidden(Intent intent)
        {
            var item = Get(new ThingId(intent.A));
            if (item == null) return IntentRejection.OutOfBounds;
            bool on = intent.B != 0;
            if (item.Forbidden == on) return IntentRejection.AlreadyInThatState;
            item.Forbidden = on;
            return IntentRejection.None;
        }

        /// <summary>
        /// Approximate travel cost between two cells: Manhattan on the layer plus a per-layer
        /// charge. An item three metres away horizontally but ten storeys down is not close, and
        /// a Euclidean measure would say it was.
        /// </summary>
        public static int Distance(int a, int b, GridSize size, int layerCostEstimate)
        {
            CellRef pa = size.FromIndex(a);
            CellRef pb = size.FromIndex(b);
            int dx = pa.X > pb.X ? pa.X - pb.X : pb.X - pa.X;
            int dz = pa.Z > pb.Z ? pa.Z - pb.Z : pb.Z - pa.Z;
            int dy = pa.Y > pb.Y ? pa.Y - pb.Y : pb.Y - pa.Y;
            return (dx + dz) * 100 + dy * layerCostEstimate;
        }

        void InsertLoose(int itemIndex)
        {
            int at = _loose.BinarySearch(itemIndex);
            if (at < 0) _loose.Insert(~at, itemIndex);
        }

        void RemoveLoose(int itemIndex)
        {
            int at = _loose.BinarySearch(itemIndex);
            if (at >= 0) _loose.RemoveAt(at);
        }

        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(_items.Count);
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                hash.Add(item.Id.Value);
                hash.Add(item.DefIndex);
                hash.Add(item.Cell);
                hash.Add(item.Stack);
                hash.Add(item.Forbidden);
                hash.Add(item.CarriedBy);
                hash.Add(item.Despawned);
            }
        }

        // ---- saving ----------------------------------------------------------------------
        //
        // Zones and beds are authored state and are saved; the loose lister and the cell index
        // are derived and are rebuilt, because a rebuilt index is correct by construction.

        public string SaveKey => "odyssey.items";

        public void Save(SaveWriter writer)
        {
            writer.Write(_nextId);
            writer.Write(_items.Count);
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                writer.Write(item.Id.Value);
                writer.Write(item.DefIndex);
                writer.Write(item.Cell);
                writer.Write(item.Stack);
                writer.Write(item.Forbidden);
                writer.Write(item.CarriedBy);
                writer.Write(item.Despawned);
            }

            writer.Write(_stockpiles.Count);
            for (int s = 0; s < _stockpiles.Count; s++)
            {
                var pile = _stockpiles[s];
                writer.Write(pile.Priority);
                writer.Write(pile.Cells.Length);
                for (int c = 0; c < pile.Cells.Length; c++) writer.Write(pile.Cells[c]);
                writer.Write(pile.Allow.Length);
                for (int a = 0; a < pile.Allow.Length; a++) writer.Write(pile.Allow[a]);
            }

            writer.Write(_beds.Count);
            for (int b = 0; b < _beds.Count; b++) writer.Write(_beds[b]);
        }

        public void Load(SaveReader reader)
        {
            _items.Clear();
            _itemAtCell.Clear();
            _loose.Clear();
            _stockpiles.Clear();
            _stockpileAtCell.Clear();
            _beds.Clear();

            _nextId = reader.ReadInt();
            int itemCount = reader.ReadInt();
            for (int i = 0; i < itemCount; i++)
            {
                var item = new ColonyItem
                {
                    Id = new ThingId(reader.ReadInt()),
                    DefIndex = reader.ReadInt(),
                    Cell = reader.ReadInt(),
                    Stack = reader.ReadInt(),
                    Forbidden = reader.ReadBool(),
                    CarriedBy = reader.ReadInt(),
                    Despawned = reader.ReadBool(),
                };
                _items.Add(item);
            }

            int pileCount = reader.ReadInt();
            for (int s = 0; s < pileCount; s++)
            {
                int priority = reader.ReadInt();
                var cells = new int[reader.ReadInt()];
                for (int c = 0; c < cells.Length; c++) cells[c] = reader.ReadInt();
                var allow = new bool[reader.ReadInt()];
                for (int a = 0; a < allow.Length; a++) allow[a] = reader.ReadBool();

                int index = _stockpiles.Count;
                _stockpiles.Add(new Stockpile(priority, cells, allow));
                for (int c = 0; c < cells.Length; c++) _stockpileAtCell[cells[c]] = index;
            }

            int bedCount = reader.ReadInt();
            for (int b = 0; b < bedCount; b++) _beds.Add(reader.ReadInt());

            // Re-derive the cell index and the loose lister in id order.
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.Despawned || item.Cell < 0) continue;
                _itemAtCell[item.Cell] = i;
                if (!IsStockpileCell(item.Cell)) _loose.Add(i);
            }
        }
    }
}
