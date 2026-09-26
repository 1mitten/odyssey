#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// Answers "what is this cell": publishes one <see cref="CellDetail"/> row for the cell the
    /// interface has asked about, and nothing for any other cell.
    ///
    /// <para><b>The per-cell half of the read contract</b>, beside <c>PawnRegistry</c>'s per-pawn
    /// half. Until now the snapshot's answer to "what is on this layer" was one byte per cell
    /// with four meanings, which is what overlays need and no way to describe a tile to a player
    /// who has clicked it. A click asks about one cell on any layer, so the answer is one row,
    /// published while a question stands and costing nothing when none does.</para>
    ///
    /// <para><b>The crossing cost is the simulation's own number, read from the table worldgen
    /// fills.</b> The addends live in <see cref="NaturalContent.ApplyCostClasses"/> — the one
    /// place that owns "boggy is +40, wading is +200" — and are restated here in thousandths of a
    /// clear crossing because that is the ratio a player reads. The class asked for is the
    /// cell's own terrain rather than the navigation grid's entry class: a click on solid ground
    /// asks about the surface, and the surface's price is the terrain's, not the air above it.</para>
    /// </summary>
    public sealed class CellDetailContributor : ISnapshotContributor
    {
        readonly CellGrid _grid;
        readonly IReadOnlyList<PlacedEdifice> _edifices;
        readonly Growing.GrowingZones? _zones;
        readonly EnclosureGrid? _enclosure;
        readonly Storage.StorageZones? _storage;

        /// <summary>The built stores, and the things they hold — how a shelf says how full it is.</summary>
        readonly Storage.StorageUnits? _units;
        readonly Pawns.ColonyItems? _items;

        readonly Temperature.TemperatureSystem? _temperature;

        readonly int[] _costByClass = new int[256];

        readonly Pawns.BedPurposes? _purposes;

        public CellDetailContributor(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices,
            Growing.GrowingZones? zones = null, EnclosureGrid? enclosure = null,
            Storage.StorageZones? storage = null, Storage.StorageUnits? units = null,
            Pawns.ColonyItems? items = null,
            Temperature.TemperatureSystem? temperature = null,
            Pawns.BedPurposes? purposes = null)
        {
            _purposes = purposes;
            _grid = grid;
            _edifices = edifices;
            _zones = zones;
            _enclosure = enclosure;
            _storage = storage;
            _units = units;
            _items = items;
            _temperature = temperature;
            NaturalContent.ApplyCostClasses(_costByClass);
        }

        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            int cell = world.Views.QueryCell;
            if (cell < 0 || cell >= _grid.Terrain.Length) return;

            ushort terrain = _grid.Terrain[cell];

            byte edifice = 0;
            byte quality = 0;
            int owner = 0;
            int handle = _grid.Edifice[cell];
            if (handle >= 0 && handle < _edifices.Count)
            {
                PlacedEdifice placed = _edifices[handle];
                if (!placed.Removed)
                {
                    edifice = (byte)placed.Def;

                    // A bed's own two answers, published beside what it is: the tier its finisher
                    // rolled and who it belongs to. Every other edifice carries zeros and the pane
                    // shows nothing, which is the same silence walls have always had (design 20
                    // §8).
                    if (placed.Def == CoreContent.EdificeBed)
                    {
                        quality = placed.Quality;
                        owner = placed.Owner;
                    }
                }
            }

            byte floorStuff = _grid.Floor[cell] != 0 ? (byte)_grid.FloorStuff[cell] : (byte)0;

            // Zero means "cannot be crossed": impassable water, or a cell with nothing to stand
            // on. Everything else is firm by default and slower only where worldgen says so.
            ushort cost = 0;
            if (!_grid.IsImpassableTerrain(cell) && (_grid.IsSolidTerrain(cell) || _grid.HasFloor(cell)))
                cost = (ushort)(1000 + _costByClass[_grid.IsUndergrowth(cell)
                    ? NaturalContent.CostClassBush : NaturalContent.CostClassOf(terrain)] * 10);

            ushort workToClear = (ushort)WorldContent.Table[terrain].workToClear;

            // The field's own two answers, beside the ground's: what the zone here grows and how
            // far the standing crop has come (owner, 2026-09-18 — clicking a zone should say what
            // is growing in it). 255 and MaxValue are the pane's "nothing to say", and the pane
            // stays silent for them exactly as it does for a wall's quality.
            byte zonePlant = byte.MaxValue;
            ushort cropGrowth = ushort.MaxValue;
            byte zoneYield = 0;
            if (_zones != null)
            {
                // A click on a field lands on the ground it is drawn on - the SOLID cell - while
                // the zone lives in the air cell a colonist stands in, exactly as a tree does.
                // So the ground answers for the zone above it, the same one-step-up lift the
                // picker's "block below" rule plays from the other side; asking only the clicked
                // cell made the pane silent over every field.
                int zoneCell = cell;
                if (_zones.ZonePlantAt(zoneCell) < 0
                    && _grid.IsSolidTerrain(cell)
                    && cell + _grid.Size.LayerStride < _grid.Terrain.Length)
                    zoneCell += _grid.Size.LayerStride;

                int plant = _zones.ZonePlantAt(zoneCell);
                if (plant >= 0)
                {
                    zonePlant = (byte)plant;
                    zoneYield = (byte)_zones.Plant(plant).yieldCount;
                    if (_zones.IsPlanted(zoneCell))
                        cropGrowth = (ushort)_zones.Plant(plant).Milligrowth(_zones.GrowthTicks(zoneCell));
                }
            }

            // And the store, if one covers this cell. Through `StorageZones.StoreCellOf`, which is
            // the one owner of "which cell does a store live in, given a cell somebody clicked" —
            // the same answer the designate and cancel intents get, so the pane cannot say a cell
            // is a store that the tool would refuse, or the other way round.
            int storageZone = -1;
            byte storagePriority = 0;
            int storageCells = 0;
            int storageOrdinal = 0;
            byte storeKind = CellDetail.StoreNone;
            byte storedStacks = 0, storeSlots = 0, storedDef = 255;
            int storedUnits = 0;

            // **One answer to "which cell is the store in", asked once.** StoreCellOf is the owner
            // of it — solid terrain answers for the cell above it, everything else for itself — and
            // asking it for the zone while asking the raw cell for the shelf is how the pane comes
            // to say a cell is not a store while the panel over it says it is.
            int storeCell = _storage?.StoreCellOf(cell) ?? cell;

            if (_storage != null)
            {
                int slot = _storage.ZoneAt(storeCell);
                if (slot >= 0)
                {
                    storageZone = slot;
                    storagePriority = (byte)_storage.SettingsOf(slot).Priority;
                    storageCells = _storage.CellsOf(slot).Count;
                    storageOrdinal = _storage.OrdinalOf(slot);
                    storeKind = CellDetail.StoreZone;
                }
            }

            // A built store, asked second and never at the same time: a shelf takes its cell out of
            // any zone when it is raised, and a zone cannot be painted over an edifice, so the two
            // answers are mutually exclusive by construction rather than by precedence here.
            Storage.StorageUnit? unit = _items == null ? null : _units?.AtCell(storeCell);
            if (unit != null)
            {
                storeKind = CellDetail.StoreShelf;
                storagePriority = (byte)_units!.PriorityOf(unit);
                storeSlots = (byte)unit.Slots;
                storedStacks = (byte)_units.StacksIn(unit);

                // One cell, and a place in the same numbered series the zones use: "Store 3" has to
                // name exactly one store whether it was painted or raised.
                storageCells = 1;
                storageOrdinal = _storage?.OrdinalOfCell(_units.CellOf(unit)) ?? 0;

                // **One kind, however many stacks of it.** A shelf holding several kinds says only
                // how full it is, because a pane row that listed them would be the storage panel
                // said twice — but a shelf of nothing but wood is the commonest thing a player
                // builds one for, and it is eight stacks rather than one, so asking "is there
                // exactly one item in here" would have left the ordinary case unnamed.
                System.Collections.Generic.IReadOnlyList<int> holds =
                    _items!.ContentsOf(Storage.StorageUnits.ContainerIdOf(unit.Edifice));

                int onlyDef = -1;
                for (int h = 0; h < holds.Count; h++)
                {
                    Pawns.ColonyItem held = _items.Items[holds[h]];
                    storedUnits += held.Stack;

                    if (h == 0) onlyDef = held.DefIndex;
                    else if (onlyDef != held.DefIndex) onlyDef = -1;
                }

                if (onlyDef >= 0) storedDef = (byte)onlyDef;
            }

            bool isIndoors = _enclosure?.IsIndoors(cell) ?? false;

            // The tile's own answer to "how warm is it here": its room's air where it is in a
            // room, the outdoor curve where it is not, plus the radiance of any heat source near
            // enough to shine on it (design 32). Read from the thermal system — the same one
            // source the needs system and the growth pass ask — so the pane cannot disagree with
            // the simulation about what a colonist is standing in.
            //
            // **Asked of the cell a colonist would STAND in, which is one up from a solid one.**
            // A click on open ground lands on the SOLID cell it is drawn on, exactly as a click
            // on a field does — the lift the zone read above performs for the same reason. The
            // air, the rooms and the heat sources all live in the cell above that, so asking the
            // clicked cell directly reported the outdoor curve while standing indoors, and
            // reported no warmth at all beside a campfire: the fire is in the air cell and
            // radiance does not cross layers, so the ground under it is a different storey.
            // Reported as "the surrounding tiles of the campfire didn't seem to happen"
            // (owner, 2026-09-23). Measured, it was worse than reported: with this lift
            // removed the fire's own tile, the one beside it and one six cells away all read
            // 1067 — the bare outdoor curve, identically. Nothing was reading the air at all,
            // indoors or out; the surrounding tiles were the visible half of a larger silence.
            //
            // No thermal system, nothing to say — the field's own silence, not a reading of 0 °C.
            int warmthCell = cell;
            if (_grid.IsSolidTerrain(cell) && cell + _grid.Size.LayerStride < _grid.Terrain.Length)
                warmthCell += _grid.Size.LayerStride;

            int ambientTempC = _temperature?.CellTemp(warmthCell, world.CurrentTick) ?? int.MinValue;

            writer.AddCellDetail(new CellDetail(
                cell, (byte)terrain, edifice, floorStuff, _grid.Support[cell], cost, workToClear,
                quality, owner, zonePlant, cropGrowth, zoneYield, isIndoors,
                storageZone, storagePriority, storageCells, storageOrdinal,
                storeKind, storedStacks, storeSlots, storedDef, storedUnits,
                storeKind == CellDetail.StoreNone ? -1 : storeCell,
                ambientTempC,
                // What the bed is for (design 59 §5b), beside who owns it.
                quality == 0 || _purposes == null ? CellDetail.BedForColony
                : _purposes.PurposeAt(cell) != BedPurpose.Prison ? CellDetail.BedForColony
                : _purposes.IsShackled(cell) ? CellDetail.BedShackles
                : CellDetail.BedForPrisoners));
        }
    }
}
