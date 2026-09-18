#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Growing
{
    /// <summary>
    /// The colony's growing zones: cells the player has set aside for a crop, and the plants
    /// standing in them.
    ///
    /// <para>A zone is authored state, exactly as a <see cref="Designations.DesignationGrid"/>
    /// order or a stockpile is: the player painted it, so it is hashed, saved and published,
    /// and the simulation only ever edits it through the intents that create and cancel it or
    /// the sow and harvest jobs that work it. The scan lists are sorted, so a think scan costs
    /// O(zoned cells) and never O(board) — the same argument the designation grid's sorted cell
    /// list was built on, and for the same price paid for the same reason.</para>
    ///
    /// <para><b>One plant per zone</b>, because v1 has one crop and a zone that changes species
    /// under a standing crop is exactly the question the reference answers with "the standing
    /// crop keeps its old plant". Rather than carry per-cell species to answer a question with
    /// one answer, a cell joins a zone of its plant or is refused; repainting means cancelling
    /// first. The structure is ready for per-cell crops the day a second species lands and the
    /// mid-cycle rule gets a real answer (docs/design/22-growing.md §4).</para>
    ///
    /// <para><b>The crop belongs to its zone.</b> Cancelling a zone cell uproots what stands in
    /// it: a crop exists only inside its zone, and a harvest job that walked up to a zone that
    /// was cancelled mid-grow would be harvesting a field the player took away. The zone record
    /// itself is never drawn — the cells are the zone, and they publish as one sparse channel
    /// with the planting in a second.</para>
    /// </summary>
    public sealed class GrowingZones : ITickable, IStateHashable, ISaveable, ISnapshotContributor
    {
        readonly CellGrid _grid;

        /// <summary>The plant table, indexed by <see cref="PlantHandle"/>. Resolved once at construction.</summary>
        readonly PlantDef[] _plants;

        /// <summary>The zone each cell belongs to, or -1. Full-size for O(1) answers, as the designation grid's byte array is.</summary>
        readonly int[] _zoneAt;

        /// <summary>The crop standing in each cell as <c>PlantHandle + 1</c>, or 0 for fallow.</summary>
        readonly byte[] _cropAt;

        /// <summary>Accumulated growing-window ticks per cell. Meaningless where <see cref="_cropAt"/> is 0.</summary>
        readonly int[] _growthAt;

        readonly List<Zone> _zones = new List<Zone>();

        /// <summary>Every zoned cell, ascending — the walk order for the hash, the save and the snapshot.</summary>
        readonly List<int> _cells = new List<int>();

        /// <summary>Every planted cell, ascending — the growth scan and the planting channel.</summary>
        readonly List<int> _planted = new List<int>();

        /// <summary>One contiguous patch of one crop. <see cref="Id"/> is its slot in <see cref="_zones"/> and is kept true by every edit, because <see cref="_zoneAt"/> points at it.</summary>
        sealed class Zone
        {
            public int Id;
            public byte Plant;
            public readonly List<int> Cells = new List<int>();
        }

        public GrowingZones(CellGrid grid, PlantDef[] plants)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _plants = plants ?? throw new ArgumentNullException(nameof(plants));
            int count = grid.Size.CellCount;
            _zoneAt = new int[count];
            _cropAt = new byte[count];
            _growthAt = new int[count];
            Array.Fill(_zoneAt, -1);
        }

        public GridSize Size => _grid.Size;

        /// <summary>Every zoned cell index, ascending. Stable order is what makes a scan deterministic.</summary>
        public IReadOnlyList<int> Cells => _cells;

        /// <summary>Every planted cell index, ascending — what the growth pass and the sowing scan walk.</summary>
        public IReadOnlyList<int> Planted => _planted;

        public int ZoneCount => _zones.Count;

        public PlantDef Plant(int handle) => _plants[handle];

        /// <summary>Which plant the zone covering this cell grows, or -1 if the cell is in no zone.</summary>
        public int ZonePlantAt(int index) => _zoneAt[index] >= 0 ? _zones[_zoneAt[index]].Plant : -1;

        /// <summary>Is a crop standing in this cell? Not whether it is ripe — that is the growth's question.</summary>
        public bool IsPlanted(int index) => _cropAt[index] != 0;

        /// <summary>What grows in this cell as a <see cref="PlantHandle"/>, or -1 for fallow.</summary>
        public int CropPlant(int index) => _cropAt[index] - 1;

        /// <summary>Accumulated growing-window ticks standing in this cell.</summary>
        public int GrowthTicks(int index) => _growthAt[index];

        /// <summary>Does this cell hold a crop that has finished growing and is ready to cut?</summary>
        public bool IsRipe(int index) =>
            _cropAt[index] != 0 && _growthAt[index] >= _plants[_cropAt[index] - 1].growTicks;

        // ---- the siting gate -------------------------------------------------------------------

        /// <summary>
        /// May this seed go in this cell? The siting gate, asked here and nowhere else so that a
        /// zone that exists is one that made sense when it was painted.
        ///
        /// <para><b>The zone lives where the pawn stands, and the soil it asks about does
        /// not.</b> The surface cell of the meadow is the solid grass itself; the cell a
        /// colonist walks and sows in is the air cell above it, whose own terrain is air —
        /// fertility nought for every cell of every field ever. The ground that answers is the
        /// one below, exactly as the tree above it is an edifice of the air cell and not of the
        /// soil it roots in.</para>
        ///
        /// <para>Whether it <em>still</em> makes sense is the job's question, as it is for a
        /// designation: a roof raised over a zone after the fact does not unzone it, and the
        /// sowing work giver asks again before it sends anybody. The roof refusal is the light
        /// hook's v1 shape — there is no light model, so "no daylight" is spelled as "under a
        /// slab" rather than pretending a number was measured (docs/design/22-growing.md §4).</para>
        /// </summary>
        public bool SiteAllows(int index, PlantDef plant)
        {
            if (!_grid.IsWalkable(index)) return false;
            // Wadeable water is walkable, which is exactly why it is asked apart: a paddy is not
            // a carrot bed, and IsWalkable alone would paint one onto a stream. Water stands in
            // its own cell — the one a pawn wades in — so this reads the cell itself, while the
            // soil reads the cell below.
            if (NaturalContent.IsWater(_grid.Terrain[index])) return false;
            // Nothing grows through a slab, and something standing in the cell keeps it: a tree
            // is felled first, a wall is the player's own doing to undo.
            if (_grid.Floor[index] != 0) return false;
            if (_grid.Edifice[index] >= 0) return false;
            if (_grid.IsRoofed(index)) return false;

            int ground = index - _grid.Size.LayerStride;
            if (ground < 0) return false;
            return NaturalContent.TerrainAt(_grid.Terrain[ground]).fertility >= plant.minFertility;
        }

        // ---- painting and cancelling ------------------------------------------------------------

        /// <summary>
        /// Paint one cell into a zone of this plant, or say why not. Out of the map is
        /// <see cref="IntentRejection.OutOfBounds"/>; a cell the seed cannot take, or one
        /// already zoned for a different plant, is <see cref="IntentRejection.NotPermitted"/>;
        /// the same plant twice is <see cref="IntentRejection.AlreadyInThatState"/>.
        ///
        /// <para>A cell touching a zone of the same plant joins it — eight-neighbour, so a
        /// diagonal brush stroke is one field and not two — and zones folded together by a
        /// stroke across their corner dissolve into the survivor. The zone is the player's
        /// gesture made durable, not a thing with edges of its own to defend.</para>
        /// </summary>
        public IntentRejection Designate(CellRef cell, int plant)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;
            if ((uint)plant >= (uint)_plants.Length) return IntentRejection.NotPermitted;

            int index = _grid.Index(cell);
            int existing = _zoneAt[index];
            if (existing >= 0)
                return _zones[existing].Plant == plant
                    ? IntentRejection.AlreadyInThatState
                    : IntentRejection.NotPermitted;
            if (!SiteAllows(index, _plants[plant])) return IntentRejection.NotPermitted;

            Paint(index, (byte)plant);
            return IntentRejection.None;
        }

        /// <summary>Take a cell back out of its zone, crop and all, or say why not.</summary>
        public IntentRejection Cancel(CellRef cell)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;

            int index = _grid.Index(cell);
            int slot = _zoneAt[index];
            if (slot < 0) return IntentRejection.AlreadyInThatState;

            Zone zone = _zones[slot];
            zone.Cells.RemoveAt(zone.Cells.BinarySearch(index));
            _cells.RemoveAt(_cells.BinarySearch(index));
            _zoneAt[index] = -1;
            if (_cropAt[index] != 0) Uproot(index);
            if (zone.Cells.Count == 0) Dissolve(zone);
            return IntentRejection.None;
        }

        /// <summary>Join a cell into whatever same-plant ground touches it, or found a zone of its own.</summary>
        void Paint(int index, byte plant)
        {
            Zone? home = null;
            CellRef at = _grid.Size.FromIndex(index);
            for (int i = 0; i < 8; i++)
            {
                int x = at.X + NeighbourX[i];
                int z = at.Z + NeighbourZ[i];
                if (!_grid.Size.Contains(x, z, at.Y)) continue;
                int slot = _zoneAt[_grid.Size.Index(x, z, at.Y)];
                if (slot < 0) continue;
                Zone zone = _zones[slot];
                if (zone.Plant != plant) continue;
                if (home == null) home = zone;
                else if (zone != home) MergeInto(home, zone);
            }

            if (home == null)
            {
                home = new Zone { Id = _zones.Count, Plant = plant };
                _zones.Add(home);
            }

            home.Cells.Insert(~home.Cells.BinarySearch(index), index);
            _cells.Insert(~_cells.BinarySearch(index), index);
            _zoneAt[index] = home.Id;
        }

        /// <summary>Fold the other zone into the survivor and take it off the list.</summary>
        void MergeInto(Zone home, Zone other)
        {
            home.Cells.AddRange(other.Cells);
            Dissolve(other);
            // The absorbed cells point at the slot their old zone died in, and the stroke that
            // caused the merge is still reading neighbours — one of them could sit in an
            // absorbed cell and resolve to whichever zone now holds the slot. Point them at the
            // survivor now; every later read in this stroke sees a live answer.
            for (int i = 0; i < other.Cells.Count; i++) _zoneAt[other.Cells[i]] = home.Id;
        }

        /// <summary>
        /// Take a zone off the list. The last zone moves into the vacated slot, and every cell
        /// it owns moves with it — a slot that <see cref="_zoneAt"/> still points at must hold
        /// the same zone, or a cancel a field away would eat the wrong one.
        /// </summary>
        void Dissolve(Zone zone)
        {
            int slot = zone.Id;
            _zones.RemoveAt(slot);
            if (slot < _zones.Count)
            {
                Zone moved = _zones[slot];
                moved.Id = slot;
                for (int i = 0; i < moved.Cells.Count; i++) _zoneAt[moved.Cells[i]] = slot;
            }
        }

        // ---- sowing and growth ------------------------------------------------------------------
        //
        // The job side (U47) calls Sow and Uproot; the growth system calls Advance. Both write
        // the same three arrays the intents write, so the crop's state has one home.

        /// <summary>Put a seed of the zone's plant in this zoned cell, growth from nothing.</summary>
        public void Sow(int index)
        {
            int slot = _zoneAt[index];
            if (slot < 0) throw new ArgumentException($"cell {index} is in no growing zone.", nameof(index));
            if (_cropAt[index] != 0) throw new ArgumentException($"cell {index} already holds a crop.", nameof(index));

            _cropAt[index] = (byte)(_zones[slot].Plant + 1);
            _growthAt[index] = 0;
            _planted.Insert(~_planted.BinarySearch(index), index);
        }

        /// <summary>Take the crop out of this cell, roots and all: harvested or its zone cancelled.</summary>
        public void Uproot(int index)
        {
            if (_cropAt[index] == 0) return;
            _cropAt[index] = 0;
            _growthAt[index] = 0;
            _planted.RemoveAt(_planted.BinarySearch(index));
        }

        /// <summary>
        /// Add growing-window ticks to this cell's crop, capped at ripeness so a ripe field's
        /// counter stops moving. Returns the drawn stage after the advance, so the caller can
        /// re-mesh on the change and on nothing else.
        /// </summary>
        public int Advance(int index, int ticks)
        {
            _growthAt[index] = Math.Min(_growthAt[index] + ticks, _plants[_cropAt[index] - 1].growTicks);
            return _plants[_cropAt[index] - 1].StageOfTicks(_growthAt[index]);
        }

        // ---- the intent seam ---------------------------------------------------------------------

        /// <summary><c>DesignateZone(cell, A = PlantHandle + 1)</c> — one-based, as a designation's kind is, so that nought means "not set".</summary>
        public IntentRejection HandleDesignate(Intent intent)
        {
            if (intent.A <= 0 || intent.A > _plants.Length) return IntentRejection.NotPermitted;
            return Designate(intent.Cell, intent.A - 1);
        }

        /// <summary><c>CancelZone(cell)</c>.</summary>
        public IntentRejection HandleCancel(Intent intent) => Cancel(intent.Cell);

        /// <summary>Register everything this grid is: hashed state, saved state, a snapshot channel, two intents.</summary>
        public SimWorldBuilder Attach(SimWorldBuilder builder)
        {
            return builder
                .AddTickable(_ => this)
                .AddSnapshotContributor(this)
                .AddIntentHandler(IntentKind.DesignateZone, HandleDesignate)
                .AddIntentHandler(IntentKind.CancelZone, HandleCancel);
        }

        // ---- ITickable: registration only, so the hash and the save see the zones ---------------
        //
        // Growth is a system (PlantGrowthSystem), not this grid's tick: it wants the WorldSystems
        // phase's own cadence, and state that never ticks still has to be hashed, which is the
        // whole reason DesignationGrid parks itself in TickGroup.Never.

        public TickGroup TickGroup => TickGroup.Never;
        public int TickPhaseOffset => 0;
        public void Tick(SimWorld world) { }

        public void ContributeTo(ref StateHash hash)
        {
            // Cells with their crop and counter. The zone structure is deliberately not hashed:
            // which patch a cell belongs to is a function of which cells exist with which plant,
            // so hashing the cells hashes everything two machines could disagree about.
            hash.Add(_cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                int index = _cells[i];
                hash.Add(index);
                hash.Add(_cropAt[index]);
                hash.Add(_growthAt[index]);
            }
        }

        // ---- ISaveable ------------------------------------------------------------------------

        public string SaveKey => "odyssey.zones";

        public void Save(SaveWriter writer)
        {
            writer.Write(_zones.Count);
            for (int i = 0; i < _zones.Count; i++)
            {
                Zone zone = _zones[i];
                writer.Write(zone.Plant);
                writer.Write(zone.Cells.Count);
                for (int j = 0; j < zone.Cells.Count; j++)
                {
                    int index = zone.Cells[j];
                    writer.Write(index);
                    // -1 is fallow, so a painted-but-unseeded cell survives the round trip as
                    // itself and not as a crop at tick zero.
                    writer.Write(_cropAt[index] != 0 ? _growthAt[index] : -1);
                }
            }
        }

        public void Load(SaveReader reader)
        {
            foreach (Zone zone in _zones) zone.Cells.Clear();
            _zones.Clear();
            Array.Fill(_zoneAt, -1);
            Array.Clear(_cropAt, 0, _cropAt.Length);
            Array.Clear(_growthAt, 0, _growthAt.Length);
            _cells.Clear();
            _planted.Clear();

            int zoneCount = reader.ReadInt();
            for (int z = 0; z < zoneCount; z++)
            {
                byte plant = reader.ReadByte();
                int cellCount = reader.ReadInt();
                // An unknown plant (a save from a build with more species than this one) is
                // skipped, but its cells are still consumed: the reader is sequential, and a
                // skipped section that left its bytes unread would corrupt every section after.
                bool known = (uint)plant < (uint)_plants.Length;
                var zone = known ? new Zone { Id = _zones.Count, Plant = plant } : null;
                if (zone != null) _zones.Add(zone);
                for (int i = 0; i < cellCount; i++)
                {
                    int index = reader.ReadInt();
                    int growth = reader.ReadInt();
                    if (zone == null) continue;
                    if ((uint)index >= (uint)_zoneAt.Length) continue;
                    if (_zoneAt[index] >= 0) continue; // a zone a field away must not be eaten by a corrupt overlap

                    _zoneAt[index] = zone.Id;
                    zone.Cells.Add(index);
                    _cells.Add(index);
                    if (growth >= 0)
                    {
                        _cropAt[index] = (byte)(plant + 1);
                        _growthAt[index] = growth;
                        _planted.Add(index);
                    }
                }
            }

            // Zones arrive in record order; every scan in this class and both published channels
            // want ascending cells, so order is restored once here rather than maintained by
            // every writer.
            _cells.Sort();
            _planted.Sort();
            foreach (Zone zone in _zones) zone.Cells.Sort();
        }

        // ---- ISnapshotContributor: zones and crops, sparse, anywhere in the world ----------------

        /// <summary>
        /// Publish every zoned cell and every standing crop. Two channels, not one: the tint
        /// asks "is this cell in a zone", the crop asks "what stands here and how tall is it",
        /// and a fallow cell answers the first and not the second. Sparse, whole-world and in
        /// cell-index order, for the measured reason the designation grid's channel is (a layer
        /// walk costs 0.055 ms of mostly nothing; a field is tens to thousands of cells).
        /// </summary>
        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            for (int i = 0; i < _cells.Count; i++)
            {
                int index = _cells[i];
                writer.AddZone(new ZoneView(index, _zones[_zoneAt[index]].Plant));
            }

            for (int i = 0; i < _planted.Count; i++)
            {
                int index = _planted[i];
                PlantDef def = _plants[_cropAt[index] - 1];
                int ticks = _growthAt[index];
                writer.AddPlant(new PlantView(
                    index,
                    _cropAt[index],
                    (byte)def.StageOfTicks(ticks),
                    (byte)(def.Milligrowth(ticks) * 255 / 1000)));
            }
        }

        static readonly int[] NeighbourX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] NeighbourZ = { 0, 0, 1, -1, 1, -1, 1, -1 };
    }
}
