#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Storage
{
    /// <summary>
    /// The colony's storage zones: cells the player has set aside for things to be put down in,
    /// and what each of those zones accepts.
    ///
    /// <para>A zone is authored state exactly as a designation or a growing zone is: the player
    /// painted it, so it is hashed, saved and published, and the simulation edits it only through
    /// the four intents below. The cell list is sorted, so the haul scan costs O(zoned cells) and
    /// never O(board).</para>
    ///
    /// <para><b>The join policy is not the growing zone's, and that is the point of the unit.</b>
    /// A growing zone joins whatever same-plant ground touches it and folds two zones into one,
    /// which loses nothing because a zone is identified by its crop. A storage zone carries a
    /// configuration, so folding two would silently destroy one of two filters. <b>The anchor
    /// decides</b>: a drag that begins inside a zone extends that zone, one that begins outside
    /// founds a new one, and a cell the drag crosses that belongs to another zone is
    /// <em>transferred</em> rather than shared. Two existing zones never merge. Overlap is then
    /// impossible by construction rather than guarded against — which is the fault
    /// <c>ColonyItems.AddStockpile</c> carried from the day it was written until this class
    /// replaced it.</para>
    ///
    /// <para><b>It owns membership; <see cref="ColonyItems"/> owns the things.</b> The two listers
    /// a haul scan walks — loose and stored — are bucketed by whether a cell is in a zone, so
    /// every join and every leave tells the items back through <see cref="ColonyItems.Rebucket"/>.
    /// That half never existed before: nothing could take a cell <em>out</em> of a zone, so a
    /// stored thing could never become loose again.</para>
    /// </summary>
    public sealed class StorageZones : ITickable, IStateHashable, ISaveable, ISnapshotContributor, IZoneMembership
    {
        readonly CellGrid _grid;
        readonly ZoneGrid _zones;
        readonly StorageSettingsTable _settings;
        readonly ColonyItems _items;

        /// <summary>The chunk sink to tell when a cell's look changes, or null for a headless run.</summary>
        readonly ChunkGrid? _chunks;

        /// <summary>
        /// The drag in progress: the anchor cell the intents named, and the zone that anchor
        /// resolved to.
        ///
        /// <para><b>One field, not a map</b>, because a rectangle is committed in one batch and
        /// drained contiguously, so only one drag is ever in flight — and a run whose anchor does
        /// not match starts a new one.</para>
        ///
        /// <para><b>And it is only needed for one case.</b> In the ordinary drag the anchor cell is
        /// itself painted, so from the second cell onward <see cref="TargetFor"/> finds the zone by
        /// asking the grid where the anchor is and the memo is never read. It earns its place when
        /// the anchor cell is <em>refused</em> — a box begun on water, a wall, a bed — and some
        /// later cell of the same box has to found the zone the rest will join. The residual case
        /// it cannot tell apart is two separate drags begun from the same refused cell, which
        /// become one zone; that is a worse answer than "two zones" and a much better one than
        /// "one corrupt zone", and it is the honest cost of not minting an id in the interface.</para>
        ///
        /// <para>Transient: not saved, not hashed.</para>
        /// </summary>
        (int Anchor, int Slot) _run = (-1, -1);

        public StorageZones(CellGrid grid, StorageSettingsTable settings, ColonyItems items,
            ChunkGrid? chunks = null)
        {
            _grid = grid ?? throw new System.ArgumentNullException(nameof(grid));
            _settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
            _items = items ?? throw new System.ArgumentNullException(nameof(items));
            _chunks = chunks;
            _zones = new ZoneGrid(grid.Size.CellCount);
        }

        /// <summary>Every storage cell, ascending. Stable order is what makes a scan deterministic.</summary>
        public IReadOnlyList<int> Cells => _zones.Cells;

        public int ZoneCount => _zones.Count;

        /// <summary>The settings table, for a caller that wants to read or edit a filter directly.</summary>
        public StorageSettingsTable Settings => _settings;

        /// <summary>The zone covering this cell, or -1.</summary>
        public int ZoneAt(int cell) => _zones.SlotAt(cell);

        /// <summary>Is this cell inside a storage zone? The question <see cref="ColonyItems"/> buckets on.</summary>
        public bool IsStorage(int cell) => _zones.SlotAt(cell) >= 0;

        /// <summary>The settings of the zone covering this cell, or null where there is none.</summary>
        /// <summary>
        /// The colony's built stores, or null where it has none.
        ///
        /// <para>Held here so that <see cref="SettingsAt"/> can answer for a shelf as well as for a
        /// zone — which is the whole of what makes the priority and filter intents drive a shelf
        /// without a second intent pair or a second control. The pane must not be able to say a
        /// cell is a store that the tool would refuse, and one resolver is how that stays true.</para>
        /// </summary>
        public StorageUnits? Units { get; set; }

        /// <summary>
        /// What the store covering this cell accepts, or null where nothing does.
        ///
        /// <para><b>The one resolver, and it answers for both kinds of store.</b> A painted zone
        /// first, then a shelf standing in the cell — the two can never both be true, because a
        /// shelf takes its cell out of any zone when it is raised and
        /// <see cref="SiteAllows"/> refuses to paint a zone over an edifice. Both storage intents
        /// come through here, which is what lets the priority ladder and the filter the player
        /// already knows drive a shelf with no second control anywhere.</para>
        /// </summary>
        public StorageSettings? SettingsAt(int cell)
        {
            int slot = _zones.SlotAt(cell);
            if (slot >= 0) return _settings[_zones.TagOf(slot)];

            StorageUnit? unit = Units?.AtCell(cell);
            return unit == null ? null : Units!.SettingsOf(unit);
        }

        /// <summary>The settings of one zone by slot.</summary>
        public StorageSettings SettingsOf(int slot) => _settings[_zones.TagOf(slot)];

        /// <summary>The cells one zone owns, ascending.</summary>
        public IReadOnlyList<int> CellsOf(int slot) => _zones.CellsOf(slot);

        /// <summary>
        /// This store's place among the colony's, from 1 — what an unnamed zone is called.
        ///
        /// <para>Counted in <b>cell order</b>: how many stores begin at a lower cell than this one.
        /// A slot index would have been free and is the wrong answer — slots move when a zone
        /// dissolves, so a store would be renamed by the deletion of an unrelated one across the
        /// map. Cell order moves only when a store that begins earlier goes, which is at least a
        /// change the player made near the one they are looking at.</para>
        ///
        /// <para>Derived rather than stored, so it is the same on both sides of a save with nothing
        /// written down. A typed name is what finally stops it drifting.</para>
        /// </summary>
        public int OrdinalOf(int slot)
        {
            if ((uint)slot >= (uint)_zones.Count) return 0;

            IReadOnlyList<int> mine = _zones.CellsOf(slot);
            return OrdinalOfCell(mine.Count > 0 ? mine[0] : int.MaxValue);
        }

        /// <summary>
        /// What a store beginning at this cell is called: one more than the number of stores that
        /// begin lower.
        ///
        /// <para><b>Both kinds are counted, and that is the whole reason this is separate from
        /// <see cref="OrdinalOf"/>.</b> A shelf is a store, so it takes its place in the same
        /// series — "Store 3" has to name exactly one thing, and counting zones and shelves apart
        /// would give the player two of them.</para>
        /// </summary>
        public int OrdinalOfCell(int firstCell)
        {
            int ordinal = 1;

            for (int other = 0; other < _zones.Count; other++)
            {
                IReadOnlyList<int> cells = _zones.CellsOf(other);
                if (cells.Count > 0 && cells[0] < firstCell) ordinal++;
            }

            if (Units != null)
                for (int u = 0; u < Units.Units.Count; u++)
                {
                    StorageUnit unit = Units.Units[u];
                    if (!unit.Removed && Units.CellOf(unit) < firstCell) ordinal++;
                }

            return ordinal;
        }

        /// <summary>
        /// Does this cell accept this def? Answered here rather than by reaching for the settings,
        /// because "there is no zone here" and "the zone here refuses it" are the same answer to
        /// the caller and two different lookups.
        /// </summary>
        public bool Accepts(int cell, int defIndex)
        {
            int slot = _zones.SlotAt(cell);
            return slot >= 0 && _settings[_zones.TagOf(slot)].Accepts(defIndex);
        }

        /// <summary>The priority of the zone covering this cell, or <see cref="int.MinValue"/> where there is none — the implicit "unstored" rank below every real one that <c>a-14</c> §1 infers.</summary>
        public int PriorityAt(int cell)
        {
            int slot = _zones.SlotAt(cell);
            return slot >= 0 ? _settings[_zones.TagOf(slot)].Priority : int.MinValue;
        }

        // ---- the siting gate --------------------------------------------------------------------

        /// <summary>
        /// May a store go in this cell? Asked here and nowhere else, so that a zone that exists is
        /// one that made sense when it was painted.
        ///
        /// <para>Far looser than a growing zone's gate, and deliberately: a store is a place to put
        /// something down, so the question is only whether something <em>can</em> be put down
        /// there. Anything a hauler can stand on and set a load on qualifies — bare rock, a wooden
        /// floor, a paved street, the inside of a room. Water is refused because a stack in a
        /// stream is not stored, and a cell holding furniture is refused because
        /// <c>ColonyItems</c> already refuses to put anything down in one.</para>
        ///
        /// <para>Whether it <em>still</em> makes sense is the haul's question, exactly as it is for
        /// a designation: a floor deconstructed under a zone does not unzone it, and
        /// <c>BestStorageSlot</c> asks again before it sends anybody.</para>
        /// </summary>
        /// <summary>
        /// The cell a store actually occupies, given a cell a <b>pointer</b> named.
        ///
        /// <para><b>A player can only ever click a surface, and a store does not live on one.</b>
        /// Things rest in the walkable cell a colonist stands in. Over open ground that cell is
        /// the air <em>above</em> the solid grass the pointer hit; over a built floor it is the
        /// cell whose lower boundary the slab is, which is the cell the pointer named. So the rule
        /// is not a lift, it is a question: solid terrain answers for the cell above it, and
        /// everything else answers for itself.</para>
        ///
        /// <para><b>A growing zone solves this in the interface and a store cannot.</b>
        /// <c>DesignateDirector.OnTheWorkingLayer</c> lifts a grow-zone cell by one
        /// unconditionally, which is right because nothing grows through a slab — the pointed cell
        /// is always soil. A store's commonest home is a wooden floor indoors, where that same
        /// lift would put the zone in the air a storey up. It belongs here, where the grid can be
        /// asked, rather than in the tool, which is deliberately Unity-free and grid-free.</para>
        ///
        /// <para><b>This was a real fault, reported the day S1 landed</b> (owner: <i>"I used the
        /// stockpile order and was able to highlight but then let go to place, nothing
        /// happened"</i>). Every cell of every drag on open ground arrived as the solid grass
        /// cell, <see cref="SiteAllows"/> answered "not walkable", and the whole rectangle was
        /// refused one cell at a time — visible only as a wall of <c>NotPermitted</c> warnings in
        /// the log. The same one-step-up rule was already written twice elsewhere, in
        /// <c>CellDetailContributor</c> and in the grow tool, and this is the third place it was
        /// needed and the first place it was missing.</para>
        /// </summary>
        public int StoreCellOf(int index)
        {
            if ((uint)index >= (uint)_grid.Size.CellCount) return index;
            if (!_grid.IsSolidTerrain(index)) return index;

            int above = index + _grid.Size.LayerStride;
            return above < _grid.Size.CellCount ? above : index;
        }

        public bool SiteAllows(int index)
        {
            if (!_grid.IsWalkable(index)) return false;
            // Wadeable water is walkable, which is exactly why it is asked apart.
            if (Worldgen.Natural.NaturalContent.IsWater(_grid.Terrain[index])) return false;
            if (_grid.Edifice[index] >= 0) return false;
            return true;
        }

        // ---- painting and cancelling ------------------------------------------------------------

        /// <summary>
        /// Put one cell into the storage zone this drag is building, founding one if the anchor
        /// named no zone. Out of the map is <see cref="IntentRejection.OutOfBounds"/>; a cell
        /// nothing can be put down in is <see cref="IntentRejection.NotPermitted"/>; a cell already
        /// in the drag's own zone is <see cref="IntentRejection.AlreadyInThatState"/>.
        ///
        /// <para>A cell belonging to <em>another</em> zone is not refused — it transfers. That is
        /// what makes repainting over a neighbour work, and it is the half of the anchor rule that
        /// keeps overlap impossible.</para>
        /// </summary>
        public IntentRejection Designate(CellRef cell, int anchor, int preset)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;

            int index = StoreCellOf(_grid.Index(cell));
            anchor = StoreCellOf(anchor);
            if (!SiteAllows(index)) return IntentRejection.NotPermitted;

            int target = TargetFor(anchor, preset);
            int here = _zones.SlotAt(index);
            if (here == target) return IntentRejection.AlreadyInThatState;

            if (here >= 0)
            {
                // Transferred, not shared — and the transfer can empty the old zone. A dissolve
                // moves the *last* slot into the vacated one, so the only index that can have
                // changed is the last one, and the only slot it can have become is the one that
                // died. Worked out from the two counts rather than searched for, because a search
                // for "the slot that used to be there" is a guess.
                int wasLast = _zones.Count - 1;
                _zones.Leave(index);
                if (_zones.Count == wasLast && here != wasLast && target == wasLast) target = here;
            }

            _zones.Join(target, index);
            _run = (anchor, target);
            _items.Rebucket(index);
            Mark(index);
            return IntentRejection.None;
        }

        /// <summary>
        /// Take one cell out of whatever zone holds it, by index. False when it was in none.
        ///
        /// <para><see cref="Cancel"/> is the player's door to this and calls it after
        /// <see cref="StoreCellOf"/>; the world's door is a shelf being raised, which takes its own
        /// cell out of any zone so that no cell is ever in two stores. One owner, because the
        /// re-bucket and the re-mesh are easy for a second caller to forget.</para>
        /// </summary>
        public bool LeaveCell(int index)
        {
            if (!_zones.Leave(index)) return false;

            // A dissolve can have moved the slot the run was pointing at, and a subtract and an add
            // are never the same gesture, so the memo is dropped rather than chased.
            _run = (-1, -1);
            _items.Rebucket(index);
            Mark(index);
            return true;
        }

        /// <summary>Take a cell back out of its storage zone. Anything lying in it becomes loose again.</summary>
        public IntentRejection Cancel(CellRef cell)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;

            int index = StoreCellOf(_grid.Index(cell));
            return LeaveCell(index) ? IntentRejection.None : IntentRejection.AlreadyInThatState;
        }

        /// <summary>
        /// Which zone this drag is building: the one the anchor sits in, the one an earlier cell of
        /// the same drag founded, or a new one.
        /// </summary>
        int TargetFor(int anchor, int preset)
        {
            // The anchor's own zone first, so a drag begun inside one extends it — and so a drag
            // whose anchor was painted a moment ago resolves through the grid rather than through
            // the memo.
            if ((uint)anchor < (uint)_grid.Size.CellCount)
            {
                int existing = _zones.SlotAt(anchor);
                if (existing >= 0) return existing;
            }

            if (_run.Anchor == anchor && _run.Slot >= 0 && _run.Slot < _zones.Count) return _run.Slot;

            return _zones.Found(_settings.Create(preset));
        }

        void Mark(int index)
        {
            if (_chunks == null) return;
            _chunks.MarkDirty(_grid.Size.FromIndex(index));
        }

        // ---- the intent seam ---------------------------------------------------------------------

        /// <summary><c>DesignateStorage(cell, A = anchor cell index, B = preset)</c>.</summary>
        public IntentRejection HandleDesignate(Intent intent) =>
            Designate(intent.Cell, intent.A, intent.B);

        /// <summary><c>CancelStorage(cell)</c>.</summary>
        public IntentRejection HandleCancel(Intent intent) => Cancel(intent.Cell);

        /// <summary><c>SetStoragePriority(cell, A = 0..4)</c>.</summary>
        public IntentRejection HandleSetPriority(Intent intent)
        {
            if (!_grid.Contains(intent.Cell.X, intent.Cell.Z, intent.Cell.Y)) return IntentRejection.OutOfBounds;
            if ((uint)intent.A >= StoragePriority.Count) return IntentRejection.NotPermitted;

            // Through StoreCellOf like every other way in, so that a rung set by clicking the
            // ground a store is drawn on lands on the store rather than on nothing.
            int cell = StoreCellOf(_grid.Index(intent.Cell));
            StorageSettings? settings = SettingsAt(cell);
            if (settings == null) return IntentRejection.NotPermitted;
            if (settings.Priority == intent.A) return IntentRejection.AlreadyInThatState;

            settings.Priority = intent.A;
            MarkZoneOf(cell);
            return IntentRejection.None;
        }

        /// <summary><c>SetStorageFilter(cell, A = scope, B = index, C = on)</c>.</summary>
        public IntentRejection HandleSetFilter(Intent intent)
        {
            if (!_grid.Contains(intent.Cell.X, intent.Cell.Z, intent.Cell.Y)) return IntentRejection.OutOfBounds;

            StorageSettings? settings = SettingsAt(StoreCellOf(_grid.Index(intent.Cell)));
            if (settings == null) return IntentRejection.NotPermitted;

            bool on = intent.C != 0;
            switch (intent.A)
            {
                case FilterScopeDef:
                    if ((uint)intent.B >= (uint)settings.Allow.Length) return IntentRejection.OutOfBounds;
                    if (settings.Allow[intent.B] == on) return IntentRejection.AlreadyInThatState;
                    settings.SetDef(intent.B, on);
                    return IntentRejection.None;

                case FilterScopeCategory:
                    if ((uint)intent.B >= ItemCategories.Count) return IntentRejection.OutOfBounds;
                    settings.SetCategory((ItemCategory)intent.B, on, _items.Content);
                    return IntentRejection.None;

                case FilterScopePreset:
                    if ((uint)intent.B >= StoragePreset.Count) return IntentRejection.OutOfBounds;
                    settings.ApplyPreset(intent.B);
                    return IntentRejection.None;

                default:
                    return IntentRejection.NotPermitted;
            }
        }

        public const int FilterScopeDef = 0;
        public const int FilterScopeCategory = 1;
        public const int FilterScopePreset = 2;

        /// <summary>Re-mesh every cell of the zone under this one — a priority change is a strength change on the ground.</summary>
        void MarkZoneOf(int index)
        {
            int slot = _zones.SlotAt(index);
            if (slot < 0 || _chunks == null) return;
            IReadOnlyList<int> cells = _zones.CellsOf(slot);
            for (int i = 0; i < cells.Count; i++) Mark(cells[i]);
        }

        /// <summary>Register everything this grid is: hashed state, saved state, a snapshot channel, four intents.</summary>
        public SimWorldBuilder Attach(SimWorldBuilder builder)
        {
            return builder
                .AddTickable(_ => this)
                .AddSnapshotContributor(this)
                .AddHashable(_settings)
                .AddIntentHandler(IntentKind.DesignateStorage, HandleDesignate)
                .AddIntentHandler(IntentKind.CancelStorage, HandleCancel)
                .AddIntentHandler(IntentKind.SetStoragePriority, HandleSetPriority)
                .AddIntentHandler(IntentKind.SetStorageFilter, HandleSetFilter);
        }

        // ---- ITickable: registration only, so the hash and the save see the zones ---------------

        public TickGroup TickGroup => TickGroup.Never;
        public int TickPhaseOffset => 0;
        public void Tick(SimWorld world) { }

        public void ContributeTo(ref StateHash hash)
        {
            // Cells with the priority of the zone they are in, which is what a haul reads. The
            // zone *structure* is deliberately not hashed beyond that, exactly as a growing zone's
            // is not: which patch a cell belongs to is a function of the cells and their settings,
            // and hashing a slot index would hash the order zones happened to be founded in.
            IReadOnlyList<int> cells = _zones.Cells;
            hash.Add(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                int index = cells[i];
                hash.Add(index);
                hash.Add(_settings[_zones.TagOf(_zones.SlotAt(index))].Priority);
                hash.Add(_zones.TagOf(_zones.SlotAt(index)));
            }
        }

        // ---- ISaveable ------------------------------------------------------------------------

        public string SaveKey => "odyssey.storage.zones";

        public void Save(SaveWriter writer)
        {
            writer.Write(_zones.Count);
            for (int slot = 0; slot < _zones.Count; slot++)
            {
                IReadOnlyList<int> cells = _zones.CellsOf(slot);
                writer.Write(_zones.TagOf(slot));
                writer.Write(cells.Count);
                for (int i = 0; i < cells.Count; i++) writer.Write(cells[i]);
            }
        }

        public void Load(SaveReader reader)
        {
            _zones.Clear();
            _run = (-1, -1);

            int zoneCount = reader.ReadInt();
            for (int z = 0; z < zoneCount; z++)
            {
                int settingsId = reader.ReadInt();
                int cellCount = reader.ReadInt();
                // A settings id the table does not hold is a corrupt file, not a version gap: the
                // table is written in the same save. Its cells are still consumed, because the
                // reader is sequential and a skipped record would corrupt every section after.
                bool known = (uint)settingsId < (uint)_settings.Count;
                int slot = known ? _zones.Found(settingsId) : -1;
                for (int i = 0; i < cellCount; i++)
                {
                    int index = reader.ReadInt();
                    if (slot < 0) continue;
                    if ((uint)index >= (uint)_grid.Size.CellCount) continue;
                    if (_zones.SlotAt(index) >= 0) continue; // a zone a field away must not be eaten by a corrupt overlap
                    _zones.Append(slot, index);
                }
            }

            _zones.RestoreOrder();
        }

        /// <summary>
        /// Adopt a zone read from a save written before this class existed — one priority, one
        /// filter, one set of cells, and no notion of a settings record. Called by
        /// <see cref="ColonyItems"/>'s v6 read, through <c>ColonyWorld.RebuildDerived</c>.
        ///
        /// <para><b>Overlap was legal in a v6 file and is not here.</b> Two piles could claim one
        /// cell and the last one written won, so that is what this does — the cell joins the later
        /// zone and leaves the earlier one — which reproduces exactly what the old world did
        /// rather than inventing a tidier answer for a colony somebody has already played.</para>
        /// </summary>
        public void AdoptLegacyZone(int priority, bool[] allow, IReadOnlyList<int> cells)
        {
            int slot = _zones.Found(_settings.Adopt(priority, allow, false));
            for (int i = 0; i < cells.Count; i++)
            {
                int index = cells[i];
                if ((uint)index >= (uint)_grid.Size.CellCount) continue;
                if (_zones.SlotAt(index) >= 0) _zones.Leave(index);
                _zones.Join(slot, index);
            }
        }

        /// <summary>Re-bucket every item against the zones, after a load has rebuilt both.</summary>
        public void RebucketAll()
        {
            IReadOnlyList<int> cells = _zones.Cells;
            for (int i = 0; i < cells.Count; i++) _items.Rebucket(cells[i]);
        }

        // ---- ISnapshotContributor ---------------------------------------------------------------

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            IReadOnlyList<int> cells = _zones.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                int index = cells[i];
                int slot = _zones.SlotAt(index);
                writer.AddStore(new StoreView(index, slot, (byte)_settings[_zones.TagOf(slot)].Priority));
            }
        }
    }
}
