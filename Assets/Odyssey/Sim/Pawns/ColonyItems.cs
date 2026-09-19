#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;

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
        readonly List<int> _stored = new List<int>();
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

        /// <summary>
        /// The other half of the lister: item indices lying in a stockpile cell, ascending. This
        /// is what re-stowing scans — a thing in a low-priority pile that a higher one would
        /// accept — and it is scanned only when there is nothing loose to haul, because tidying
        /// is the lowest job there is (a-14 §3).
        /// </summary>
        public IReadOnlyList<int> StoredItems => _stored;

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

        /// <summary>
        /// Put <paramref name="stack"/> of a def at a cell. A cell holding a stack of the same def
        /// with room for the load takes it and its id is returned; an empty cell gets a new
        /// thing. Anything else throws, because a caller that has not checked
        /// <see cref="CellHasSpace(int, int, int)"/> has a bug, and silently overwriting the
        /// cell index was how two things came to share a cell.
        /// </summary>
        public ThingId Spawn(int defIndex, int cell, int stack = 1)
        {
            if (_itemAtCell.TryGetValue(cell, out int resident))
            {
                var here = _items[resident];
                if (!Fits(here, defIndex, stack))
                    throw new System.InvalidOperationException(
                        $"cell {cell} holds {here.Stack} of def {here.DefIndex} and cannot take {stack} of def {defIndex}");
                here.Stack += stack;
                return here.Id;
            }

            var item = new ColonyItem { Id = new ThingId(_nextId++), DefIndex = defIndex, Cell = cell, Stack = stack };
            _items.Add(item);
            int index = _items.Count - 1;
            _itemAtCell[cell] = index;
            Enlist(cell, index);
            return item.Id;
        }

        public void AddStockpile(Stockpile stockpile)
        {
            int index = _stockpiles.Count;
            _stockpiles.Add(stockpile);
            for (int i = 0; i < stockpile.Cells.Length; i++)
            {
                _stockpileAtCell[stockpile.Cells[i]] = index;

                // Anything already lying in the new zone stops being loose and becomes stored.
                if (_itemAtCell.TryGetValue(stockpile.Cells[i], out int item))
                {
                    RemoveFrom(_loose, item);
                    InsertInto(_stored, item);
                }
            }
        }

        public void AddBed(int cell)
        {
            int at = _beds.BinarySearch(cell);
            if (at < 0) _beds.Insert(~at, cell);
        }

        /// <summary>
        /// Take a bed cell out of the list — the other half of <see cref="AddBed"/>, which until a
        /// bed could be built had no caller: a deconstructed bed must stop being slept in, and the
        /// scenario's own cells are never removed.
        /// </summary>
        public void RemoveBed(int cell)
        {
            int at = _beds.BinarySearch(cell);
            if (at >= 0) _beds.RemoveAt(at);
        }

        public bool IsStockpileCell(int cell) => _stockpileAtCell.ContainsKey(cell);

        public Stockpile? StockpileAt(int cell) =>
            _stockpileAtCell.TryGetValue(cell, out int index) ? _stockpiles[index] : null;

        public void PickUp(ColonyItem item, PawnId carrier)
        {
            if (item.Cell >= 0)
            {
                _itemAtCell.Remove(item.Cell);
                Unlist(item.Id.Value - 1);
            }
            item.Cell = -1;
            item.CarriedBy = carrier.Value;
        }

        /// <summary>
        /// Put a carried thing down. Onto a stack of the same def with room, it merges: the
        /// resident stack grows and the carried thing is despawned, because the resident is the
        /// one something else may hold a claim on. The caller checks
        /// <see cref="CellHasSpace(int, int, int)"/> first; a cell that cannot take it throws.
        /// Returns the thing now at the cell.
        /// </summary>
        public ColonyItem Drop(ColonyItem item, int cell)
        {
            if (_itemAtCell.TryGetValue(cell, out int resident))
            {
                var here = _items[resident];
                if (!Fits(here, item.DefIndex, item.Stack))
                    throw new System.InvalidOperationException(
                        $"cell {cell} holds {here.Stack} of def {here.DefIndex} and cannot take {item.Stack} of def {item.DefIndex}");
                here.Stack += item.Stack;
                item.Stack = 0;
                Despawn(item);
                return here;
            }

            item.Cell = cell;
            item.CarriedBy = 0;
            int index = item.Id.Value - 1;
            _itemAtCell[cell] = index;
            Enlist(cell, index);
            return item;
        }

        /// <summary>
        /// Move a thing that is already on the ground to another cell, merging if something of the
        /// same kind with room is already there.
        ///
        /// <para>The same landing rules as <see cref="Drop"/>, which is deliberate: a stack that
        /// falls down a shaft and a stack a colonist carries down it should end up in the same
        /// state, and two code paths for that would drift. The difference is only that this one
        /// has to take the thing off its old cell first — <see cref="Drop"/> starts from a pair of
        /// hands, which occupy no cell at all.</para>
        ///
        /// <para>The caller checks <see cref="CellHasSpace(int, int, int)"/>, exactly as it does
        /// for a drop. Returns the thing now at the cell, which is <em>not</em> the one passed in
        /// when a merge happened.</para>
        /// </summary>
        public ColonyItem MoveTo(ColonyItem item, int cell)
        {
            if (item.Cell == cell || item.Despawned) return item;

            if (item.Cell >= 0)
            {
                _itemAtCell.Remove(item.Cell);
                Unlist(item.Id.Value - 1);
                item.Cell = -1;
            }

            return Drop(item, cell);
        }

        public void Despawn(ColonyItem item)
        {
            if (item.Cell >= 0) _itemAtCell.Remove(item.Cell);
            Unlist(item.Id.Value - 1);
            item.Cell = -1;
            item.CarriedBy = 0;
            item.Despawned = true;
        }

        /// <summary>Is this cell empty? The question a bed, a spawn or a footprint asks.</summary>
        public bool CellHasSpace(int cell) =>
            !_itemAtCell.ContainsKey(cell) && !_noItems.Contains(cell);

        /// <summary>
        /// Cells that hold furniture nothing may be put down in — both cells of every bed.
        ///
        /// <para><b>Derived, not saved</b>, and rebuilt from the edifice list on load exactly as
        /// structural support, the region graph and a ladder's connector are
        /// (<c>ColonyWorld.RebuildDerived</c>). That is what keeps this out of the save format and
        /// out of the state hash.</para>
        ///
        /// <para><b>Why it is here and not only at the build order.</b> Refusing to <i>place</i> a
        /// bed on a pile is half the rule; the other half is that a hauler must not carry a pile on
        /// to a bed afterwards, which is the same picture arriving the other way round — the owner
        /// reported it as meals poking up through a mattress (2026-09-18), and that is exactly what
        /// <see cref="NearestCellWithSpace"/> would do with an empty, walkable, bed-shaped cell.
        /// It also stopped a bed cell from being a haul destination at all, which was locking a
        /// colonist out of their own bed: <c>TrySleep</c> checks the reservation before it checks
        /// whose bed it is, so a hauler's claim on the cell sent the owner to sleep on the
        /// ground.</para>
        /// </summary>
        readonly HashSet<int> _noItems = new HashSet<int>();

        /// <summary>Nothing may be put down in this cell while the furniture stands there.</summary>
        public void BlockItemsAt(int cell) => _noItems.Add(cell);

        /// <summary>The other half: the furniture has gone and the cell is ordinary ground again.</summary>
        public void AllowItemsAt(int cell) => _noItems.Remove(cell);

        /// <summary>Forget every block, before they are worked out again from the edifice list.</summary>
        public void ClearItemBlocks() => _noItems.Clear();

        /// <summary>
        /// Can this cell take <paramref name="count"/> of a def? Empty, or holding the same def
        /// with the whole load's worth of room under its <see cref="ItemDef.stackLimit"/>.
        ///
        /// Whole load or nothing. The research leaves partial fits open (a-14, "could not be
        /// determined"); the strict reading means a hauler never splits a stack or drops a
        /// surplus, and a cell that is "space" always takes what arrives. A full stack, or a
        /// stack of anything else, is not space.
        /// </summary>
        public bool CellHasSpace(int cell, int defIndex, int count) =>
            !_noItems.Contains(cell)
            && (!_itemAtCell.TryGetValue(cell, out int index) || Fits(_items[index], defIndex, count));

        bool Fits(ColonyItem resident, int defIndex, int count) =>
            resident.DefIndex == defIndex && resident.Stack + count <= Content.Items[defIndex].stackLimit;

        /// <summary>
        /// The cell itself if it can take the load, else the nearest walkable cell on the same
        /// layer that can, ring by ring out to <paramref name="maxRadius"/>; -1 when there is
        /// none. Where felled wood lands and where a failed haul puts its load down.
        /// </summary>
        public int NearestCellWithSpace(CellGrid cells, int origin, int defIndex, int count, int maxRadius,
            System.Func<int, bool>? accept = null)
        {
            if (CellHasSpace(origin, defIndex, count) && (accept == null || accept(origin))) return origin;

            GridSize size = cells.Size;
            CellRef at = size.FromIndex(origin);
            for (int radius = 1; radius <= maxRadius; radius++)
            for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz)) != radius) continue;
                int x = at.X + dx, z = at.Z + dz;
                if (!size.Contains(x, z, at.Y)) continue;
                int candidate = size.Index(x, z, at.Y);
                if (!cells.IsWalkable(candidate) || !CellHasSpace(candidate, defIndex, count)) continue;
                if (accept != null && !accept(candidate)) continue;
                return candidate;
            }
            return -1;
        }

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
        /// Debug menu: <c>IntentKind.GiveResource</c>. <c>A</c> is the item def index, <c>B</c> the
        /// count; the cell is where to try first. <see cref="NearestCellWithSpace"/> is the same
        /// widening search a delivered or dropped item already uses, so a granted stack behaves
        /// like any other item arriving rather than a second, debug-only way for one to appear.
        ///
        /// <para>Takes the grid rather than holding one, because <see cref="ColonyItems"/> itself
        /// does not — every other caller of <see cref="NearestCellWithSpace"/> already hands its
        /// own <c>CellGrid</c> in, and a field kept only for this handler would be a second copy of
        /// something <see cref="PawnContext"/> already owns.</para>
        /// </summary>
        public IntentRejection HandleGiveResource(Intent intent, CellGrid cells)
        {
            int defIndex = intent.A;
            int amount = intent.B;
            if (defIndex < 0 || defIndex >= Content.Items.Length) return IntentRejection.OutOfBounds;
            if (amount <= 0) return IntentRejection.NotPermitted;

            CellRef at = intent.Cell;
            if (!cells.Size.Contains(at.X, at.Z, at.Y)) return IntentRejection.OutOfBounds;

            int origin = cells.Size.Index(at);
            int cell = NearestCellWithSpace(cells, origin, defIndex, amount, maxRadius: 3);
            if (cell < 0) return IntentRejection.NotPermitted;

            Spawn(defIndex, cell, amount);
            return IntentRejection.None;
        }

        /// <summary>
        /// Approximate travel cost between two cells: Octile on the layer plus a per-layer
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
            int min = dx < dz ? dx : dz;
            int max = dx < dz ? dz : dx;
            return min * 141 + (max - min) * 100 + dy * layerCostEstimate;
        }

        /// <summary>A thing at a cell is on exactly one of the two listers, by where the cell is.</summary>
        void Enlist(int cell, int itemIndex) => InsertInto(IsStockpileCell(cell) ? _stored : _loose, itemIndex);

        void Unlist(int itemIndex)
        {
            RemoveFrom(_loose, itemIndex);
            RemoveFrom(_stored, itemIndex);
        }

        static void InsertInto(List<int> lister, int itemIndex)
        {
            int at = lister.BinarySearch(itemIndex);
            if (at < 0) lister.Insert(~at, itemIndex);
        }

        static void RemoveFrom(List<int> lister, int itemIndex)
        {
            int at = lister.BinarySearch(itemIndex);
            if (at >= 0) lister.RemoveAt(at);
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
            _stored.Clear();
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

            // Re-derive the cell index and both listers in id order, which is ascending by
            // construction, so the appends leave the lists sorted without a search.
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.Despawned || item.Cell < 0) continue;
                _itemAtCell[item.Cell] = i;
                if (IsStockpileCell(item.Cell)) _stored.Add(i);
                else _loose.Add(i);
            }
        }
    }
}
