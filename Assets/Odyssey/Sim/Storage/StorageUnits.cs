#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.Storage
{
    /// <summary>
    /// One built store: a shelf, and whatever larger thing comes after it.
    ///
    /// <para><b>Four scalars and no list.</b> What is in a shelf lives on the things themselves —
    /// <see cref="ColonyItem.ContainerId"/> — and is listed by <see cref="ColonyItems"/>. A
    /// contents list here would be a second copy of an answer that is already saved and already
    /// hashed, and the two would drift the first time something was eaten out of a shelf.</para>
    /// </summary>
    public sealed class StorageUnit
    {
        /// <summary>The index of the <see cref="PlacedEdifice"/> this store is. The key.</summary>
        public int Edifice;

        /// <summary>Into <see cref="StorageSettingsTable"/>, exactly as a zone holds one.</summary>
        public int SettingsId;

        /// <summary>
        /// How many stacks it holds, copied off the def when it was raised rather than read back
        /// from the def on every question.
        ///
        /// <para><b>Copied on purpose.</b> Re-tuning <c>storageSlots</c> in the content is a one
        /// line change, and if this were read live it would silently shrink a saved, full shelf
        /// out from under its contents — leaving stacks in a container with no slot for them and
        /// no error anywhere.</para>
        /// </summary>
        public int Slots;

        /// <summary>Tombstoned when the shelf comes down. The slot is kept so ids stay stable.</summary>
        public bool Removed;
    }

    /// <summary>
    /// Every built store the colony has.
    ///
    /// <para><b>A component, not a field on <see cref="PlacedEdifice"/>.</b> That record is a value
    /// struct and cannot hold what this needs, which is exactly why a bed's scalar <c>Owner</c>
    /// rides it and this does not.</para>
    ///
    /// <para><b>The id is the edifice index, and there is not a second one.</b> An edifice index is
    /// already stable by contract (removed slots are tombstoned, never reused), already what
    /// <c>CellGrid.Edifice[cell]</c> names, and already what the reservation key can be keyed on.
    /// A minted id of our own would be a second thing to keep in step with it — the fault
    /// <c>docs/bug-patterns.md</c> lists first. <see cref="ColonyItem.ContainerId"/> is that index
    /// <b>plus one</b>, and only because 0 already means "in no container" while edifice 0 is a
    /// real edifice.</para>
    ///
    /// <para><b>Its own save section, and therefore no format bump.</b> Sections are keyed and
    /// length-prefixed, so a reader skips what it does not know and a file written before shelves
    /// simply has no section here. The one thing that would have forced a version — the item
    /// record's layout — was changed once in S1, with <c>ContainerId</c> already in it and written
    /// by nothing, precisely so that this unit would not have to change it again.</para>
    /// </summary>
    public sealed class StorageUnits : ITickable, IStateHashable, ISaveable, ISnapshotContributor
    {
        readonly CellGrid _grid;
        readonly IReadOnlyList<PlacedEdifice> _edifices;
        readonly StorageSettingsTable _settings;
        readonly ColonyItems _items;

        /// <summary>The standing orders, or null in a fixture that has none. See <see cref="IsEmptying"/>.</summary>
        readonly DesignationGrid? _designations;

        /// <summary>Ascending by edifice index, because that is the order they are appended in.</summary>
        readonly List<StorageUnit> _units = new List<StorageUnit>();

        /// <summary>
        /// Edifice index to slot. Derived, rebuilt on load, and <b>probed, never iterated in a
        /// tick</b> — the rule <c>ReservationManager</c> states for the same reason: the iteration
        /// order of a hash table is not a simulation input.
        /// </summary>
        readonly Dictionary<int, int> _atEdifice = new Dictionary<int, int>();

        public StorageUnits(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices,
            StorageSettingsTable settings, ColonyItems items, DesignationGrid? designations = null)
        {
            _grid = grid ?? throw new System.ArgumentNullException(nameof(grid));
            _edifices = edifices ?? throw new System.ArgumentNullException(nameof(edifices));
            _settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
            _items = items ?? throw new System.ArgumentNullException(nameof(items));
            _designations = designations;
        }

        /// <summary>The container id a thing in this edifice carries. Never 0 for a real edifice.</summary>
        public static int ContainerIdOf(int edifice) => edifice + 1;

        /// <summary>The edifice a container id names, or -1 for "no container".</summary>
        public static int EdificeOf(int containerId) => containerId - 1;

        public int Count => _units.Count;

        /// <summary>
        /// The painted zones, so a shelf can be published with its number in the one series both
        /// kinds share (<see cref="StorageZones.OrdinalOfCell"/>). Null in a harness with no zones,
        /// where a shelf is published unnumbered.
        /// </summary>
        public StorageZones? Zones { get; set; }

        /// <summary>Every unit, ascending by edifice, tombstones included.</summary>
        public IReadOnlyList<StorageUnit> Units => _units;

        /// <summary>The store this edifice is, or null. A tombstoned one answers null.</summary>
        public StorageUnit? At(int edifice)
        {
            if (!_atEdifice.TryGetValue(edifice, out int slot)) return null;
            StorageUnit unit = _units[slot];
            return unit.Removed ? null : unit;
        }

        /// <summary>The store standing in this cell, or null.</summary>
        public StorageUnit? AtCell(int cell)
        {
            if ((uint)cell >= (uint)_grid.Size.CellCount) return null;
            int edifice = _grid.Edifice[cell];
            return edifice < 0 ? null : At(edifice);
        }

        public StorageUnit? ByContainerId(int containerId) =>
            containerId == 0 ? null : At(EdificeOf(containerId));

        /// <summary>Where a store stands. Taken off the edifice record, so there is one copy of it.</summary>
        public int CellOf(StorageUnit unit) => _edifices[unit.Edifice].CellIndex;

        /// <summary>Where the store holding this container id stands, or -1.</summary>
        public int CellOfContainer(int containerId)
        {
            StorageUnit? unit = ByContainerId(containerId);
            return unit == null ? -1 : CellOf(unit);
        }

        public StorageSettings SettingsOf(StorageUnit unit) => _settings[unit.SettingsId];

        public bool Accepts(StorageUnit unit, int defIndex) => SettingsOf(unit).Accepts(defIndex);

        public int PriorityOf(StorageUnit unit) => SettingsOf(unit).Priority;

        public int StacksIn(StorageUnit unit) => _items.StacksIn(ContainerIdOf(unit.Edifice));

        public bool IsEmpty(StorageUnit unit) => StacksIn(unit) == 0;

        /// <summary>Is the store standing in this cell empty? False where there is no store at all.</summary>
        public bool IsEmptyAt(int cell)
        {
            StorageUnit? unit = AtCell(cell);
            return unit != null && IsEmpty(unit);
        }

        /// <summary>
        /// Can this store take the whole of this load?
        /// <see cref="ColonyItems.CellHasSpace(int, int, int)"/>'s twin, with the same rule:
        /// <b>whole load or nothing</b>, either merging into a stack it already holds or taking a
        /// slot of its own. A shelf never splits a load across two slots.
        /// </summary>
        public bool HasSpaceFor(StorageUnit unit, int defIndex, int count)
        {
            if (unit.Removed) return false;

            int container = ContainerIdOf(unit.Edifice);
            if (_items.ContainerStackHasRoom(container, defIndex, count)) return true;

            // Or a fresh slot, and the load must fit in one of them. **A slot is a stack and not a
            // commodity**: a shelf takes eight stacks of wood as readily as eight different things,
            // which is what makes it worth building rather than painting eight tiles of floor.
            return _items.StacksIn(container) < unit.Slots
                && count <= _items.Content.Items[defIndex].stackLimit;
        }

        /// <summary>
        /// Is this store being emptied — ordered taken apart, so its contents should leave?
        ///
        /// <para><b>Derived from the standing order, and stored nowhere.</b> The designation is
        /// already authored, already saved and already hashed, so a flag beside it would be a
        /// second copy that could disagree with the order the player can see — and cancelling the
        /// order un-empties the shelf for free, through the one path that already exists.</para>
        /// </summary>
        public bool IsEmptying(StorageUnit unit) =>
            !unit.Removed && _designations != null
            && _designations.At(CellOf(unit)) == DesignationKind.Deconstruct;

        /// <summary>
        /// Put a carried thing into this store.
        ///
        /// <para><b>The one door, because it is the only place that knows the slot count.</b>
        /// <see cref="ColonyItems.PutIn"/> cannot refuse an overfull store — it has never heard of
        /// slots — so a caller that reached past this and forgot <see cref="HasSpaceFor"/> would
        /// quietly put a ninth stack on an eight-stack shelf and nothing anywhere would say so.
        /// Throwing here is the same contract <c>ColonyItems.Spawn</c> keeps for a cell.</para>
        ///
        /// <para>Returns the thing now in the store, which is <b>not</b> the one passed in when it
        /// merged into a stack already there.</para>
        /// </summary>
        public ColonyItem PutIn(StorageUnit unit, ColonyItem item)
        {
            if (!HasSpaceFor(unit, item.DefIndex, item.Stack))
                throw new System.InvalidOperationException(
                    $"store {unit.Edifice} holds {StacksIn(unit)} of {unit.Slots} stacks and cannot " +
                    $"take {item.Stack} of def {item.DefIndex}");

            return _items.PutIn(item, ContainerIdOf(unit.Edifice));
        }

        // ---- raising and dissolving ----------------------------------------------------------

        /// <summary>
        /// Mint the store a finished shelf is. Called by <c>ConstructionGrid.Raise</c>, which is
        /// the only thing that puts one of these on the board.
        ///
        /// <para>A new store is <c>Preferred</c> and accepts everything, which is a decision and
        /// not a default: a zone is <c>Normal</c>, two stores at one rung never re-stow between
        /// them, and a shelf built inside a warehouse that did nothing at all until its priority
        /// was raised by hand would read as a shelf that does not work.</para>
        /// </summary>
        public StorageUnit Raise(int edifice, int slots)
        {
            StorageUnit? already = At(edifice);
            if (already != null) return already;

            int settings = _settings.Create(StoragePreset.Everything);
            _settings[settings].Priority = StoragePriority.Preferred;

            var unit = new StorageUnit { Edifice = edifice, SettingsId = settings, Slots = slots };
            _atEdifice[edifice] = _units.Count;
            _units.Add(unit);
            return unit;
        }

        /// <summary>
        /// Could everything in this store be put down on the board, if it came apart now?
        ///
        /// <para>A dry run, and it is what the deconstruct job asks before it finishes: a player's
        /// order that cannot be carried out is refused rather than approximated, so nothing is ever
        /// destroyed by tidying. Two stacks cannot both be promised the same square, which is what
        /// <paramref name="taken"/> is for, and the store's own cell is excluded because a shelf
        /// that is coming down is not somewhere to put things.</para>
        /// </summary>
        public bool CanSpillAll(PawnContext ctx, StorageUnit unit)
        {
            IReadOnlyList<int> contents = _items.ContentsOf(ContainerIdOf(unit.Edifice));
            if (contents.Count == 0) return true;

            int home = CellOf(unit);
            var taken = new HashSet<int>();
            for (int i = 0; i < contents.Count; i++)
            {
                ColonyItem thing = _items.Items[contents[i]];
                int cell = _items.NearestCellWithSpace(ctx.Cells, home, thing.DefIndex, thing.Stack,
                    SpillRadius, at => at != home && !taken.Contains(at));
                if (cell < 0) return false;
                taken.Add(cell);
            }

            return true;
        }

        /// <summary>
        /// The store has gone. Spill what the board will take and lose the rest.
        ///
        /// <para><b>This is the destroying path, and it is right that it destroys.</b> Losing
        /// things to a building coming down around them is a consequence; losing them to a player's
        /// own deconstruct order is a bug, and that is refused one level up by
        /// <see cref="CanSpillAll"/> before the work is ever allowed to finish. What is left here
        /// is the same answer a loose stack over void already gets.</para>
        /// </summary>
        public void Dissolve(PawnContext ctx, int edifice)
        {
            StorageUnit? unit = At(edifice);
            if (unit == null) return;

            int home = CellOf(unit);
            int container = ContainerIdOf(edifice);

            // A copy, because taking a thing out edits the list being walked.
            var contents = new List<int>(_items.ContentsOf(container));
            for (int i = 0; i < contents.Count; i++)
            {
                ColonyItem thing = _items.Items[contents[i]];
                int cell = _items.NearestCellWithSpace(ctx.Cells, home, thing.DefIndex, thing.Stack,
                    SpillRadius, at => at != home);
                // Despawn unlists from the container and clears the id, so a thing lost with the
                // shelf leaves no ghost in a lister behind it.
                if (cell >= 0) _items.TakeOutTo(thing, cell);
                else _items.Despawn(thing);
            }

            unit.Removed = true;
        }

        /// <summary>How far a spill looks for somewhere to land. <c>JobDriver.DropSearchRadius</c>'s own number.</summary>
        public const int SpillRadius = 8;

        /// <summary>
        /// After a load: put back on the ground anything that names a store which is not there.
        ///
        /// <para>A corrupt file rather than a version gap — the same answer <c>StorageZones.Load</c>
        /// gives a settings id its table does not hold. It runs in <c>RebuildDerived</c>, where both
        /// sections have finished loading, so it never depends on which was written first.</para>
        /// </summary>
        public void AdoptContents(PawnContext ctx)
        {
            var orphans = new List<int>();
            IReadOnlyList<int> contained = _items.ContainedItems;
            for (int i = 0; i < contained.Count; i++)
            {
                ColonyItem thing = _items.Items[contained[i]];
                if (ByContainerId(thing.ContainerId) == null) orphans.Add(contained[i]);
            }

            // From the middle of the board, because the store that would have said where it was is
            // exactly the thing that is missing. A sweep wide enough to cross the map, and a
            // despawn if even that finds nowhere — which is a corrupt file losing a stack rather
            // than a colony doing so.
            GridSize size = _grid.Size;
            int middle = size.Index(size.SizeX / 2, size.SizeZ / 2, size.SizeY - 1);
            int reach = size.SizeX > size.SizeZ ? size.SizeX : size.SizeZ;

            for (int i = 0; i < orphans.Count; i++)
            {
                ColonyItem thing = _items.Items[orphans[i]];
                int cell = _items.NearestCellWithSpace(ctx.Cells, middle, thing.DefIndex, thing.Stack, reach);
                if (cell >= 0) _items.TakeOutTo(thing, cell);
                else _items.Despawn(thing);
            }
        }

        // ---- registration ----------------------------------------------------------------------

        public SimWorldBuilder Attach(SimWorldBuilder builder) =>
            // Registered as a tickable that never ticks, exactly as the zones are: it is how a
            // component that only holds state reaches the hash and the save.
            builder.AddTickable(_ => this).AddSnapshotContributor(this);

        /// <summary>
        /// Every live store, and how full it is.
        ///
        /// <para><b>The state, not the verdict.</b> Whether a stuck store is worth telling the
        /// player about — and after how long — is the alert bar's own latch, exactly as it is for
        /// an idle colonist. Deciding it here would put a wall-clock rule inside a fixed-tick
        /// simulation.</para>
        /// </summary>
        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                StorageUnit unit = _units[i];
                if (unit.Removed) continue;

                int cell = CellOf(unit);
                writer.AddStorageUnit(new StorageUnitView(
                    cell, (byte)StacksIn(unit), (byte)unit.Slots, IsEmptying(unit),
                    Zones?.OrdinalOfCell(cell) ?? 0));
            }
        }

        public TickGroup TickGroup => TickGroup.Never;
        public int TickPhaseOffset => 0;
        public void Tick(SimWorld world) { }

        // ---- the hash ---------------------------------------------------------------------------

        /// <summary>
        /// <b>What is in a shelf is not hashed here.</b> Every item's <c>ContainerId</c> is already
        /// in the hash through <see cref="ColonyItems"/>, and the per-container lister is derived
        /// from exactly that — so hashing it would only restate what it was rebuilt from, which is
        /// the argument that class already makes about its loose and stored listers.
        /// </summary>
        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(_units.Count);
            for (int i = 0; i < _units.Count; i++)
            {
                StorageUnit unit = _units[i];
                hash.Add(unit.Edifice);
                hash.Add(unit.SettingsId);
                hash.Add(unit.Slots);
                hash.Add(unit.Removed);
            }
        }

        // ---- the save ---------------------------------------------------------------------------

        public string SaveKey => "odyssey.storage.units";

        public void Save(SaveWriter writer)
        {
            writer.Write(_units.Count);
            for (int i = 0; i < _units.Count; i++)
            {
                StorageUnit unit = _units[i];
                writer.Write(unit.Edifice);
                writer.Write(unit.SettingsId);
                writer.Write(unit.Slots);
                writer.Write(unit.Removed);
            }
        }

        public void Load(SaveReader reader)
        {
            _units.Clear();
            _atEdifice.Clear();

            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var unit = new StorageUnit
                {
                    Edifice = reader.ReadInt(),
                    SettingsId = reader.ReadInt(),
                    Slots = reader.ReadInt(),
                    Removed = reader.ReadBool(),
                };

                _atEdifice[unit.Edifice] = _units.Count;
                _units.Add(unit);
            }
        }
    }
}
