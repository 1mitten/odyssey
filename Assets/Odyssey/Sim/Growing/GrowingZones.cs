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

        /// <summary>
        /// Which cells are zoned and how they are grouped. The <b>container</b> only — the join
        /// policy is <see cref="Paint"/>'s, below, because a storage zone sits on the same
        /// container and must not fold (see <see cref="ZoneGrid"/>). Its tag is a
        /// <c>PlantHandle</c>.
        /// </summary>
        readonly ZoneGrid _zones;

        /// <summary>The crop standing in each cell as <c>PlantHandle + 1</c>, or 0 for fallow.</summary>
        readonly byte[] _cropAt;

        /// <summary>Accumulated growing-window ticks per cell. Meaningless where <see cref="_cropAt"/> is 0.</summary>
        readonly int[] _growthAt;

        /// <summary>Every planted cell, ascending — the growth scan and the planting channel.</summary>
        readonly List<int> _planted = new List<int>();

        /// <summary>
        /// The chunk sink to tell when a crop appears or vanishes, or null for a headless run.
        ///
        /// <para>Held here rather than left to the callers of <see cref="Sow"/> and
        /// <see cref="Uproot"/> because a crop's remesh has more than two callers and one of them
        /// — cancelling a zone — has no context to mark from: the intent handlers take a bare
        /// <see cref="Intent"/>, so a driver-side mark would have left the cancel path silently
        /// drawing a crop the simulation had already taken out of the world. The grid is the same
        /// object the renderer's mirror holds, exactly as <see cref="Pawns.PawnContext"/>.Chunks is.</para>
        /// </summary>
        readonly ChunkGrid? _chunks;

        public GrowingZones(CellGrid grid, PlantDef[] plants, ChunkGrid? chunks = null)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _plants = plants ?? throw new ArgumentNullException(nameof(plants));
            _chunks = chunks;
            int count = grid.Size.CellCount;
            _zones = new ZoneGrid(count);
            _cropAt = new byte[count];
            _growthAt = new int[count];
        }

        public GridSize Size => _grid.Size;

        /// <summary>Every zoned cell index, ascending. Stable order is what makes a scan deterministic.</summary>
        public IReadOnlyList<int> Cells => _zones.Cells;

        /// <summary>Every planted cell index, ascending — what the growth pass and the sowing scan walk.</summary>
        public IReadOnlyList<int> Planted => _planted;

        public int ZoneCount => _zones.Count;

        public PlantDef Plant(int handle) => _plants[handle];

        /// <summary>Which plant the zone covering this cell grows, or -1 if the cell is in no zone.</summary>
        public int ZonePlantAt(int index)
        {
            int slot = _zones.SlotAt(index);
            return slot >= 0 ? _zones.TagOf(slot) : -1;
        }

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
            int existing = _zones.SlotAt(index);
            if (existing >= 0)
                return _zones.TagOf(existing) == plant
                    ? IntentRejection.AlreadyInThatState
                    : IntentRejection.NotPermitted;
            if (!SiteAllows(index, _plants[plant])) return IntentRejection.NotPermitted;

            Paint(index, (byte)plant);
            // The ground under the cell changes with its zone - tilled rows arrive - so this is
            // a remesh, the same mark a crop's appearance makes, and the tufts beside it move
            // with it (see MarkCellAndSides).
            MarkCellAndSides(index);
            return IntentRejection.None;
        }

        /// <summary>Take a cell back out of its zone, crop and all, or say why not.</summary>
        public IntentRejection Cancel(CellRef cell)
        {
            if (!_grid.Contains(cell.X, cell.Z, cell.Y)) return IntentRejection.OutOfBounds;

            int index = _grid.Index(cell);
            if (!_zones.Leave(index)) return IntentRejection.AlreadyInThatState;

            if (_cropAt[index] != 0) Uproot(index);
            MarkCellAndSides(index);
            return IntentRejection.None;
        }

        /// <summary>
        /// Mark a cell's chunk and the chunks its four side neighbours stand in. The ground
        /// under a zoned cell changes with its zone - tilled rows arrive - and so do the tufts
        /// beside it: a grass clump's mesh reaches a metre past its own cell, and the tuft
        /// pass pulls those clumps off the tilled tile on the next mesh, which is a different
        /// cell's draw and can live in a different chunk. Sides only, because that is as far
        /// as a clump placed inside its own ring can reach.
        /// </summary>
        void MarkCellAndSides(int index)
        {
            if (_chunks == null) return;
            var size = _grid.Size;
            CellRef at = size.FromIndex(index);
            _chunks.MarkDirty(at);
            if (at.X + 1 < size.SizeX) _chunks.MarkDirty(new CellRef(at.X + 1, at.Z, at.Y));
            if (at.X > 0) _chunks.MarkDirty(new CellRef(at.X - 1, at.Z, at.Y));
            if (at.Z + 1 < size.SizeZ) _chunks.MarkDirty(new CellRef(at.X, at.Z + 1, at.Y));
            if (at.Z > 0) _chunks.MarkDirty(new CellRef(at.X, at.Z - 1, at.Y));
        }
        /// <summary>
        /// Join a cell into whatever same-plant ground touches it, or found a zone of its own.
        ///
        /// <para><b>This is the join policy, and it stays here.</b> Eight-neighbour, so a diagonal
        /// brush stroke is one field and not two, and zones folded together by a stroke across
        /// their corner dissolve into the survivor — which is sound only because a growing zone is
        /// identified by its plant and two touching carrot fields are interchangeable. A storage
        /// zone on the same container must not fold, because it carries a configuration a fold
        /// would destroy, so the rule belongs to the caller and the bookkeeping belongs to
        /// <see cref="ZoneGrid"/>.</para>
        /// </summary>
        void Paint(int index, byte plant)
        {
            int home = -1;
            CellRef at = _grid.Size.FromIndex(index);
            for (int i = 0; i < 8; i++)
            {
                int x = at.X + NeighbourX[i];
                int z = at.Z + NeighbourZ[i];
                if (!_grid.Size.Contains(x, z, at.Y)) continue;
                int slot = _zones.SlotAt(_grid.Size.Index(x, z, at.Y));
                if (slot < 0) continue;
                if (_zones.TagOf(slot) != plant) continue;
                // MergeInto answers with the surviving home slot, which is not always the one it
                // was given: dissolving a slot moves the last one into the gap, and the last one
                // can be home.
                home = home < 0 ? slot : _zones.MergeInto(home, slot);
            }

            if (home < 0) home = _zones.Found(plant);
            _zones.Join(home, index);
        }

        // ---- sowing and growth ------------------------------------------------------------------
        //
        // The job side (U47) calls Sow and Uproot; the growth system calls Advance. Both write
        // the same three arrays the intents write, so the crop's state has one home.

        /// <summary>
        /// Put a seed of the zone's plant in this zoned cell, growth from nothing.
        ///
        /// <para>This, not the sowing driver, tells the renderer: the crop is now in the world,
        /// and who changed the world owns the remesh mark — the same settlement U29 reached for
        /// the support solver. A mark from here covers every route in, including ones with no
        /// context to mark from, at the price of an idempotent extra mark from the driver's route.</para>
        /// </summary>
        public void Sow(int index)
        {
            int slot = _zones.SlotAt(index);
            if (slot < 0) throw new ArgumentException($"cell {index} is in no growing zone.", nameof(index));
            if (_cropAt[index] != 0) throw new ArgumentException($"cell {index} already holds a crop.", nameof(index));

            _cropAt[index] = (byte)(_zones.TagOf(slot) + 1);
            _growthAt[index] = 0;
            _planted.Insert(~_planted.BinarySearch(index), index);
            _chunks?.MarkDirty(_grid.Size.FromIndex(index));
        }

        /// <summary>
        /// Take the crop out of this cell, roots and all: harvested or its zone cancelled.
        ///
        /// <para>The mark lives here for the same reason <see cref="Sow"/>'s does, and the cancel
        /// route is the one that cannot do without it.</para>
        /// </summary>
        public void Uproot(int index)
        {
            if (_cropAt[index] == 0) return;
            _cropAt[index] = 0;
            _growthAt[index] = 0;
            _planted.RemoveAt(_planted.BinarySearch(index));
            _chunks?.MarkDirty(_grid.Size.FromIndex(index));
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

        /// <summary>
        /// Bring every standing crop to ripeness in one step, window and all. The debug menu's
        /// harvest test: the four-day wait is the thing being skipped, not the thing being
        /// simulated, so this writes the same end state the growth system would have reached —
        /// and marks the same re-meshes it would have marked, on the stage changes and nothing
        /// else. Refused when nothing stands, so the row can say why it did nothing.
        /// </summary>
        public IntentRejection RipenAll()
        {
            if (_planted.Count == 0) return IntentRejection.AlreadyInThatState;
            for (int i = 0; i < _planted.Count; i++)
            {
                int index = _planted[i];
                PlantDef def = _plants[_cropAt[index] - 1];
                int before = def.StageOfTicks(_growthAt[index]);
                _growthAt[index] = def.growTicks;
                if (def.StageOfTicks(_growthAt[index]) != before)
                    _chunks?.MarkDirty(_grid.Size.FromIndex(index));
            }
            return IntentRejection.None;
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

        /// <summary>Register everything this grid is: hashed state, saved state, a snapshot channel, two intents — and the debug menu's ripen, which owns crops and so lives here.</summary>
        public SimWorldBuilder Attach(SimWorldBuilder builder)
        {
            return builder
                .AddTickable(_ => this)
                .AddSnapshotContributor(this)
                .AddIntentHandler(IntentKind.DesignateZone, HandleDesignate)
                .AddIntentHandler(IntentKind.CancelZone, HandleCancel)
                .AddIntentHandler(IntentKind.DebugRipen, _ => RipenAll());
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
            IReadOnlyList<int> cells = _zones.Cells;
            hash.Add(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                int index = cells[i];
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
                IReadOnlyList<int> zoneCells = _zones.CellsOf(i);
                writer.Write((byte)_zones.TagOf(i));
                writer.Write(zoneCells.Count);
                for (int j = 0; j < zoneCells.Count; j++)
                {
                    int index = zoneCells[j];
                    writer.Write(index);
                    // -1 is fallow, so a painted-but-unseeded cell survives the round trip as
                    // itself and not as a crop at tick zero.
                    writer.Write(_cropAt[index] != 0 ? _growthAt[index] : -1);
                }
            }
        }

        public void Load(SaveReader reader)
        {
            _zones.Clear();
            Array.Clear(_cropAt, 0, _cropAt.Length);
            Array.Clear(_growthAt, 0, _growthAt.Length);
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
                int slot = known ? _zones.Found(plant) : -1;
                for (int i = 0; i < cellCount; i++)
                {
                    int index = reader.ReadInt();
                    int growth = reader.ReadInt();
                    if (slot < 0) continue;
                    if ((uint)index >= (uint)_cropAt.Length) continue;
                    if (_zones.SlotAt(index) >= 0) continue; // a zone a field away must not be eaten by a corrupt overlap

                    _zones.Append(slot, index);
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
            _zones.RestoreOrder();
            _planted.Sort();
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
            IReadOnlyList<int> cells = _zones.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                int index = cells[i];
                writer.AddZone(new ZoneView(index, (byte)_zones.TagOf(_zones.SlotAt(index))));
            }

            for (int i = 0; i < _planted.Count; i++)
            {
                int index = _planted[i];
                PlantDef def = _plants[_cropAt[index] - 1];
                int ticks = _growthAt[index];
                // The handle, not _cropAt: the view's contract says PlantHandle, which is
                // nought-based, and _cropAt is one-based so that nought can mean fallow. The
                // first version published _cropAt itself, the render mirror added its own one,
                // and the carrot landed on slot two of a one-plant table — where CropModule's
                // bounds guard quietly returned nought and a ripe field drew nothing at all,
                // while every render test fed the contract's nought and passed. The count the
                // tests pin and the field the game draws now come off the same byte.
                writer.AddPlant(new PlantView(
                    index,
                    (byte)(_cropAt[index] - 1),
                    (byte)def.StageOfTicks(ticks),
                    (byte)(def.Milligrowth(ticks) * 255 / 1000)));
            }
        }

        static readonly int[] NeighbourX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] NeighbourZ = { 0, 0, 1, -1, 1, -1, 1, -1 };
    }
}
