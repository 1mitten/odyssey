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

        /// <summary>
        /// The container holding this thing, or 0 for none — the third answer to "where is it",
        /// beside a cell and a pair of hands.
        ///
        /// <para><b>Written by nothing yet, and here on purpose.</b> Storage units are S2 and the
        /// ground is strictly one stack per cell until they land (docs/plans/storage.md §1); what
        /// this field buys today is that the item record's byte layout changes <b>once</b>, in the
        /// same save version that moves the zones out of this section, rather than twice. A save
        /// written now reads back 0 for every item, which is exactly what it means.</para>
        /// </summary>
        public int ContainerId;

        public bool Despawned;
    }

    /// <summary>
    /// Whether a cell is inside a storage zone — the one question <see cref="ColonyItems"/> asks
    /// of the zones, and the reason it does not hold them.
    ///
    /// <para>An interface rather than a reference to <c>StorageZones</c> because the dependency
    /// only runs one way in meaning: the zones know about the things (they re-bucket them), and
    /// the things need one boolean. A colony with no zones at all — a test fixture, a bare board —
    /// leaves it null and everything is loose, which is the truth.</para>
    /// </summary>
    public interface IZoneMembership
    {
        bool IsStorage(int cell);
    }

    /// <summary>
    /// A storage zone as a save written before <c>StorageZones</c> existed described it: one
    /// priority, one filter, one set of cells, and no notion of a settings record.
    ///
    /// <para><b>Read by nothing but the v6 migration.</b> It is not a live type — the living one
    /// is <c>Odyssey.Sim.Storage.StorageZones</c> — and it is here rather than there because this
    /// is the section whose bytes it is: the reader is sequential, so the old records have to be
    /// consumed by whoever is reading the items section, and handed on afterwards.</para>
    /// </summary>
    public sealed class LegacyStockpile
    {
        public LegacyStockpile(int priority, int[] cells, bool[] allow)
        {
            Priority = priority;
            Cells = cells;
            Allow = allow;
            System.Array.Sort(Cells);
        }

        public readonly int Priority;
        public readonly int[] Cells;
        public readonly bool[] Allow;
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

        /// <summary>
        /// Everything inside any container, ascending. The third lister, beside
        /// <see cref="LooseItems"/> and <see cref="StoredItems"/>.
        /// </summary>
        readonly List<int> _contained = new List<int>();

        /// <summary>
        /// Container id to the items it holds, ascending. Probed, never iterated in a tick — the
        /// rule <c>ReservationManager</c> states for the same reason: the iteration order of a
        /// hash table is not a simulation input.
        /// </summary>
        readonly Dictionary<int, List<int>> _inContainer = new Dictionary<int, List<int>>();

        static readonly List<int> NoContents = new List<int>();

        readonly List<int> _beds = new List<int>();

        /// <summary>
        /// Zones read out of a v6 save, waiting for <c>ColonyWorld.RebuildDerived</c> to hand them
        /// to <c>StorageZones</c>. Empty at every other moment of a world's life.
        ///
        /// <para><b>Stashed rather than applied</b>, because section load order is the order of the
        /// components list and this section has no business depending on it. Draining afterwards is
        /// the one arrangement that works whichever way round the two sections are written.</para>
        /// </summary>
        public List<LegacyStockpile> PendingLegacyZones { get; } = new List<LegacyStockpile>();

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

        /// <summary>
        /// The third lister: item indices inside a container, ascending. What lets a shelf be
        /// re-stowed <em>out of</em> — a thing in a shelf that a better store would accept, or a
        /// thing in a shelf that is being emptied — and scanned last, because taking something out
        /// of a store is a lower job than tidying the floor.
        /// </summary>
        public IReadOnlyList<int> ContainedItems => _contained;

        /// <summary>The items in one container, ascending. Empty for a container holding nothing.</summary>
        public IReadOnlyList<int> ContentsOf(int containerId) =>
            _inContainer.TryGetValue(containerId, out var slots) ? slots : NoContents;

        /// <summary>
        /// How many distinct stacks a container holds — its slot count, asked of the things rather
        /// than kept as a number that could disagree with them.
        /// </summary>
        public int StacksIn(int containerId) =>
            _inContainer.TryGetValue(containerId, out var slots) ? slots.Count : 0;

        /// <summary>
        /// A stack of this def in this container, or null. The first, where there are several.
        ///
        /// <para>For reading — what a pane says a shelf holds. Putting something <em>in</em> asks
        /// <see cref="StackWithRoomIn"/> instead, because the first stack of a def is not
        /// necessarily one with room in it.</para>
        /// </summary>
        public ColonyItem? ResidentIn(int containerId, int defIndex)
        {
            if (!_inContainer.TryGetValue(containerId, out var slots)) return null;
            for (int i = 0; i < slots.Count; i++)
            {
                ColonyItem held = _items[slots[i]];
                if (held.DefIndex == defIndex) return held;
            }
            return null;
        }

        /// <summary>
        /// A stack of this def in this container with room for the whole load, or null.
        ///
        /// <para><b>A container holds several stacks of one kind, and that is the point of it.</b>
        /// A slot is a stack, not a commodity: eight slots of wood is 600 wood, which is what makes
        /// a shelf worth building rather than painting eight tiles. Merging into "the" stack of a
        /// def and refusing a second would cap a shelf at one stack per kind — 75 wood — and quietly
        /// turn a warehouse unit into a spice rack.</para>
        /// </summary>
        public ColonyItem? StackWithRoomIn(int containerId, int defIndex, int count)
        {
            if (!_inContainer.TryGetValue(containerId, out var slots)) return null;
            for (int i = 0; i < slots.Count; i++)
            {
                ColonyItem held = _items[slots[i]];
                if (Fits(held, defIndex, count)) return held;
            }
            return null;
        }

        /// <summary>
        /// Would this load merge into a stack the container already holds? The per-def half of
        /// "has it room"; how many slots the container has is the container's own half, and
        /// <c>StorageUnits.HasSpaceFor</c> is where the two meet.
        ///
        /// <para>Whole load or nothing, the same rule <see cref="CellHasSpace(int, int, int)"/>
        /// keeps one level up: a shelf never splits a load across two slots.</para>
        /// </summary>
        public bool ContainerStackHasRoom(int containerId, int defIndex, int count) =>
            StackWithRoomIn(containerId, defIndex, count) != null;

        /// <summary>
        /// Which cells are storage, or null where the colony has no zones at all. Set by the
        /// composition root; see <see cref="IZoneMembership"/> for why it is an interface.
        /// </summary>
        public IZoneMembership? Membership { get; set; }

        /// <summary>
        /// Move whatever lies in this cell to the lister its zone membership now says it belongs
        /// on. Called by the zones on every join and every leave.
        ///
        /// <para><b>The half that never existed.</b> <c>AddStockpile</c> moved an item from loose
        /// to stored when a zone was created over it, and there was no way at all to take a cell
        /// out of a zone — so a stored thing could never become loose again, and the first thing
        /// that could shrink a zone would have left its contents invisible to the haul scan for
        /// ever.</para>
        /// </summary>
        public void Rebucket(int cell)
        {
            if (!_itemAtCell.TryGetValue(cell, out int item)) return;
            Unlist(item);
            Enlist(cell, item);
        }

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

        public void AddBed(int cell)
        {
            int at = _beds.BinarySearch(cell);
            if (at < 0) _beds.Insert(~at, cell);
        }

        /// <summary>
        /// Take a bed cell out of the list — the other half of <see cref="AddBed"/>, which until a
        /// bed could be built had no caller: a deconstructed bed must stop being slept in.
        ///
        /// <para>Every cell in this list is now the head cell of a real bed, and
        /// <c>ConstructionGrid</c> is the only thing that adds to or removes from it. The scenario
        /// used to add five bare cells of its own, which is the bug
        /// <c>ColonyScenario.RaiseAStartingBed</c> records: a cell in this list with nothing
        /// standing in it cannot be seen, owned or lain on properly.</para>
        /// </summary>
        public void RemoveBed(int cell)
        {
            int at = _beds.BinarySearch(cell);
            if (at >= 0) _beds.RemoveAt(at);
        }

        /// <summary>Is this cell inside a storage zone? False everywhere when the colony has none.</summary>
        public bool IsStockpileCell(int cell) => Membership != null && Membership.IsStorage(cell);

        /// <summary>
        /// Take a thing into a pair of hands, from wherever it is.
        ///
        /// <para><b>The one door for all three homes</b> — a cell, a container, or already carried
        /// — which is what lets <c>JobDriver.LiftToil</c> stay one motion whether a colonist is
        /// stooping to the floor or reaching into a shelf. A second "take out of a container" entry
        /// beside this one would be the same rule with two owners.</para>
        /// </summary>
        public void PickUp(ColonyItem item, PawnId carrier)
        {
            if (item.Cell >= 0)
            {
                _itemAtCell.Remove(item.Cell);
                Unlist(item.Id.Value - 1);
            }
            else if (item.ContainerId != 0)
            {
                // Unlist reads the container off the record, so it is zeroed afterwards.
                Unlist(item.Id.Value - 1);
                item.ContainerId = 0;
            }

            item.Cell = -1;
            item.CarriedBy = carrier.Value;
        }

        /// <summary>
        /// Put a carried thing into a container. <see cref="Drop"/>'s twin, and it carries
        /// <see cref="Drop"/>'s contract and its trap: onto a stack of the same def with room it
        /// merges, the resident grows, and <b>the carried thing is despawned</b> — so the thing
        /// returned is <em>not</em> the one passed in when a merge happened, and a caller holding
        /// the old reference is holding a tombstone.
        ///
        /// <para>The caller asks <c>StorageUnits.HasSpaceFor</c> first. A load that cannot merge
        /// and has no free slot is the container's business to refuse; what throws here is the
        /// per-def overflow, because that is the half this class owns.</para>
        /// </summary>
        public ColonyItem PutIn(ColonyItem item, int containerId)
        {
            if (containerId == 0)
                throw new System.InvalidOperationException("a container id of 0 is 'no container'");

            ColonyItem? resident = StackWithRoomIn(containerId, item.DefIndex, item.Stack);
            if (resident != null)
            {
                resident.Stack += item.Stack;
                item.Stack = 0;
                Despawn(item);
                return resident;
            }

            if (item.Cell >= 0)
            {
                _itemAtCell.Remove(item.Cell);
                Unlist(item.Id.Value - 1);
            }

            item.Cell = -1;
            item.CarriedBy = 0;
            item.ContainerId = containerId;
            EnlistInContainer(containerId, item.Id.Value - 1);
            return item;
        }

        /// <summary>
        /// A contained thing back on to the ground, merging where it lands exactly as a drop does.
        /// What a spill uses, and what empties a shelf that is coming apart.
        /// </summary>
        public ColonyItem TakeOutTo(ColonyItem item, int cell)
        {
            if (item.ContainerId == 0)
                throw new System.InvalidOperationException("that thing is not in a container");

            Unlist(item.Id.Value - 1);
            item.ContainerId = 0;
            item.Cell = -1;
            return Drop(item, cell);
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
            // A contained thing leaves through TakeOutTo or PickUp, never straight on to a cell:
            // dropping one from here would leave it listed in its container for ever.
            if (item.ContainerId != 0)
                throw new System.InvalidOperationException(
                    $"thing {item.Id.Value} is in container {item.ContainerId}; take it out first");

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
            if (item.ContainerId != 0)
                throw new System.InvalidOperationException(
                    $"thing {item.Id.Value} is in container {item.ContainerId}; take it out first");

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
            // Before the container id is cleared, because that is what Unlist looks it up by. An
            // eaten meal that kept its id here would leave a ghost in the shelf's lister and a slot
            // nothing could ever use again.
            Unlist(item.Id.Value - 1);
            item.ContainerId = 0;
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
            if (!cells.HasFloor(origin))
                origin = cells.FirstFloorAtOrBelow(origin);

            if (cells.HasFloor(origin) && cells.IsWalkable(origin)
                && CellHasSpace(origin, defIndex, count)
                && (accept == null || accept(origin)))
                return origin;

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

            // The debug menu names a column, not a layer — "near the camera" is the air several
            // storeys above open ground — so a grant aimed at nothing falls to the first floor
            // under it, which is where a dropped stack would have ended up anyway.
            int origin = cells.FirstFloorAtOrBelow(cells.Size.Index(at));
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

        /// <summary>A thing inside a container is on the contained lister and on its container's own.</summary>
        void EnlistInContainer(int containerId, int itemIndex)
        {
            InsertInto(_contained, itemIndex);
            if (!_inContainer.TryGetValue(containerId, out var slots))
                _inContainer[containerId] = slots = new List<int>();
            InsertInto(slots, itemIndex);
        }

        /// <summary>
        /// Take a thing off every lister it can be on. It reads the container off the record, so
        /// callers clear <c>ContainerId</c> <em>after</em> calling this and never before.
        /// </summary>
        void Unlist(int itemIndex)
        {
            RemoveFrom(_loose, itemIndex);
            RemoveFrom(_stored, itemIndex);

            int container = _items[itemIndex].ContainerId;
            if (container == 0) return;
            RemoveFrom(_contained, itemIndex);
            if (_inContainer.TryGetValue(container, out var slots)) RemoveFrom(slots, itemIndex);
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

        /// <summary>
        /// Everything here that is authored: the things, the zones and the beds.
        ///
        /// <para><b>The zones and the bed list were missing until 2026-09-20</b>, and they had
        /// been <i>saved</i> since they existed — which is the worst of the two ways round. Save
        /// and hash are meant to cover the same set: <c>WorldRoundTripTests</c> proves a save by
        /// comparing hashes, so a zone that failed to round-trip would have come back accepting
        /// everything, hashed identically, and passed. Measured rather than reasoned:
        /// <c>OrdersSurviveASaveTests.EachOrderMovesTheStateHash</c> flips one filter bit and
        /// asks, and it is the control that found this. Exactly the shape of OQ-50, in which the
        /// whole cell grid sat outside the hash for a year.</para>
        ///
        /// <para>The derived halves stay out, as they must: <c>_itemAtCell</c>, <c>_loose</c>,
        /// <c>_stored</c> and <c>_stockpileAtCell</c> are all rebuilt from the three lists below,
        /// and hashing a rebuilt index would only ever restate what it was rebuilt from.</para>
        /// </summary>
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
                hash.Add(item.ContainerId);
                hash.Add(item.Despawned);
            }

            // The zones left this class in S1 and are hashed by `StorageZones` — cells with the
            // priority of the zone they are in — and their filters by `StorageSettingsTable`. The
            // pairing the note below records still holds; it is kept by two components now
            // instead of one, and `OrdersSurviveASaveTests.EachOrderMovesTheStateHash` is still
            // the control that would notice if either stopped.

            // And which cells the sleep chooser will look at. Derived from the edifice list in
            // every colony built today — `ConstructionGrid.Raise` is the only thing that adds to
            // it — but it is a list this class saves, and saved state that is not derived from
            // something already hashed belongs in the hash (OQ-50).
            hash.Add(_beds.Count);
            for (int b = 0; b < _beds.Count; b++) hash.Add(_beds[b]);
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
                writer.Write(item.ContainerId);
                writer.Write(item.Despawned);
            }

            // Format 8: the zones are gone from this section and live in `odyssey.storage.zones`.
            // Nothing is written in their place — a reader at 7 or later simply does not look.

            writer.Write(_beds.Count);
            for (int b = 0; b < _beds.Count; b++) writer.Write(_beds[b]);
        }

        public void Load(SaveReader reader)
        {
            _items.Clear();
            _itemAtCell.Clear();
            _loose.Clear();
            _stored.Clear();
            _beds.Clear();
            PendingLegacyZones.Clear();

            // Version 8 gave an item a container and took the zones out of this section. An older
            // file has neither: every item reads back ContainerId 0, which is "on the ground or in
            // a pair of hands" and is what every item in such a world was.
            bool containers = reader.FormatVersion >= 8;

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
                };
                if (containers) item.ContainerId = reader.ReadInt();
                item.Despawned = reader.ReadBool();
                _items.Add(item);
            }

            if (!containers)
            {
                // The old zone records. They have to be read whether or not anything wants them,
                // because the reader is sequential; they are stashed rather than applied, because
                // the component that owns zones now is a different section and load order is not
                // this section's business. `ColonyWorld.RebuildDerived` drains them.
                int pileCount = reader.ReadInt();
                for (int s = 0; s < pileCount; s++)
                {
                    int priority = reader.ReadInt();
                    var cells = new int[reader.ReadInt()];
                    for (int c = 0; c < cells.Length; c++) cells[c] = reader.ReadInt();
                    var allow = new bool[reader.ReadInt()];
                    for (int a = 0; a < allow.Length; a++) allow[a] = reader.ReadBool();
                    PendingLegacyZones.Add(new LegacyStockpile(priority, cells, allow));
                }
            }

            int bedCount = reader.ReadInt();
            for (int b = 0; b < bedCount; b++) _beds.Add(reader.ReadInt());

            // Re-derive the cell index and the listers in id order, which is ascending by
            // construction, so the appends leave the lists sorted without a search.
            //
            // **Everything lands loose, and is re-bucketed afterwards.** Zones are a different
            // save section now, so asking `IsStockpileCell` here would answer against whatever the
            // zones happened to hold at this point in the load — which depends on the order of the
            // components list, and a lister that is right only when two sections are written in
            // one particular order is a bug waiting for the next appended section.
            // `StorageZones.RebucketAll`, through `ColonyWorld.RebuildDerived`, is what sorts them.
            // Three homes, and the container one needs no other section: an item's own record says
            // which container holds it, so unlike zone membership this is answerable right here.
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.Despawned) continue;

                if (item.Cell >= 0)
                {
                    _itemAtCell[item.Cell] = i;
                    _loose.Add(i);
                }
                else if (item.ContainerId != 0)
                {
                    _contained.Add(i);
                    if (!_inContainer.TryGetValue(item.ContainerId, out var slots))
                        _inContainer[item.ContainerId] = slots = new List<int>();
                    slots.Add(i);
                }
            }
        }
    }
}
