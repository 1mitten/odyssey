#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Power
{
    /// <summary>
    /// Power (design 32): the lines, the orders for them, the buildings that make and spend power,
    /// and the nets the lines join them into.
    ///
    /// <para><b>The one owner of lines.</b> A line is not an edifice — it lives in this class's own
    /// layer, so it can run through a wall or under a floor without taking the cell's one edifice
    /// slot (decision 2) — and nothing else writes one. <see cref="ConstructionGrid"/> hands a line
    /// order here and asks <see cref="AllowsLine"/> where one may go, so the cursor, the order and
    /// the rule are one answer.</para>
    ///
    /// <para><b>Saved and hashed:</b> the lines, the line orders and their banked work, the removal
    /// marks and theirs, and one record per power building — its switch, its fuel and the carried
    /// remainder of its burn. <b>Derived, never saved:</b> the nets, which building is on which,
    /// and the balance — all a pure function of the saved state, solved lazily the first time
    /// anything asks after a change (<see cref="EnsureSolved"/>), which is <c>EnclosureGrid</c>'s
    /// arrangement for rooms.</para>
    ///
    /// <para><b>Cost.</b> A clean tick costs one modulo. The burn pass, every
    /// <see cref="IntervalTicks"/>, is linear in power buildings. A solve is linear in lines —
    /// 0.08 ms for one edit at 2,000 of them at the scale target (<c>PowerCostProbe</c>, design 32
    /// §11) — and happens only after a line, a power building, a switch or an empty hopper
    /// changed: never per tick, never over the board (process §3).</para>
    /// </summary>
    public sealed class PowerGrid : IWorldSystem, IStateHashable, ISaveable, ISnapshotContributor
    {
        /// <summary>How often the generators burn: the thermal pass's own cadence, so a heater's
        /// power and its heat are judged on the same tick.</summary>
        public const int IntervalTicks = 120;

        /// <summary>Burn passes in a day — 500. Integer by construction: a day is 60,000 ticks.</summary>
        public const int PassesPerDay = Calendar.TicksPerDay / IntervalTicks;

        /// <summary>Milli-units of fuel to one whole unit.</summary>
        public const int Milli = 1_000;

        /// <summary>The section's own version, written first, so the layout can grow without a
        /// save-format bump (design 32 §8).</summary>
        const int SectionVersion = 1;

        readonly CellGrid _grid;
        readonly IReadOnlyList<PlacedEdifice> _edifices;
        readonly GridSize _size;

        /// <summary>One bit per cell: is there a line here. The fast half of membership.</summary>
        readonly ulong[] _bits;

        /// <summary>Every line cell, ascending: the deterministic half, and what every walk walks.</summary>
        readonly List<int> _lines = new List<int>();

        /// <summary>Ordered lines not yet laid, ascending, with their banked milliwork beside them.</summary>
        readonly List<int> _sites = new List<int>();
        readonly List<int> _siteWork = new List<int>();

        /// <summary>Built lines marked to come out, ascending, with their banked milliwork.</summary>
        readonly List<int> _marks = new List<int>();
        readonly List<int> _markWork = new List<int>();

        /// <summary>One record per standing power building, ascending by edifice index.</summary>
        readonly List<Device> _devices = new List<Device>();

        /// <summary>What a power building carries that the world does not: its switch and its fuel.</summary>
        public struct Device
        {
            /// <summary>The building's index in the edifice list — its identity for as long as it stands.</summary>
            public int Edifice;

            /// <summary>Switched on. A new building starts on (§5).</summary>
            public bool On;

            /// <summary>Fuel in the hopper, milli-units. Nought for anything that burns nothing.</summary>
            public int FuelMilli;

            /// <summary>The burn's carried remainder, so no fraction of a milli-unit is ever lost (§6).</summary>
            public int BurnRemainder;
        }

        public PowerGrid(CellGrid grid, IReadOnlyList<PlacedEdifice> edifices)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _edifices = edifices ?? throw new ArgumentNullException(nameof(edifices));
            _size = grid.Size;
            _bits = new ulong[(_size.CellCount + 63) >> 6];
        }

        /// <summary>
        /// Bumped on every change a drawing of the lines would show: a line, an order, a mark, or
        /// a net going live or dark. Fuel moving is deliberately not in it — the lines look the
        /// same half full as full — so a burning generator does not redraw the net every pass.
        /// </summary>
        public int Version { get; private set; }

        public IReadOnlyList<int> Lines => _lines;
        public IReadOnlyList<int> Sites => _sites;
        public IReadOnlyList<int> Marks => _marks;
        public IReadOnlyList<Device> Devices => _devices;

        /// <summary>Has the grid anything at all in it? The hash and the snapshot ask before walking.</summary>
        public bool IsEmpty => _lines.Count == 0 && _sites.Count == 0 && _marks.Count == 0 && _devices.Count == 0;

        public bool IsLine(int index) =>
            (uint)index < (uint)_size.CellCount && (_bits[index >> 6] & (1UL << (index & 63))) != 0;

        public bool HasSite(int index) => _sites.BinarySearch(index) >= 0;

        public bool IsMarked(int index) => _marks.BinarySearch(index) >= 0;

        // ---- where a line may go (§3) -----------------------------------------------------------

        /// <summary>
        /// Whether a line may be ordered into this cell: inside the board, not solid terrain, not
        /// water, not rubble. A wall, a door, furniture, a floor and open air are all fine — that
        /// is decision 2, and it is the whole reason a line is not an edifice.
        ///
        /// <para>Static in effect and the <b>one owner</b> of the rule: the construction grid's
        /// <c>Allows</c> and <c>WhereItWouldLand</c> ask here for a line, so the build cursor, the
        /// order and this cannot come to three answers.</para>
        /// </summary>
        public bool AllowsLine(int index)
        {
            if ((uint)index >= (uint)_size.CellCount) return false;
            if (_grid.IsSolidTerrain(index)) return false;
            ushort terrain = _grid.Terrain[index];
            if (NaturalContent.IsWater(terrain)) return false;
            // Rubble, as for a wall: a heap is cleared before anything is laid through it.
            return NaturalContent.TerrainAt(terrain).buildable;
        }

        /// <summary>
        /// A line order named at solid ground means the cell standing on it — the wall's lift
        /// (<c>ConstructionGrid.StandingOn</c>), asked with the line's own rule, because a cell
        /// with a wall in it refuses a wall and takes a line.
        /// </summary>
        public int WhereItWouldLand(int index)
        {
            if ((uint)index >= (uint)_size.CellCount) return index;
            if (!_grid.IsSolidTerrain(index)) return index;
            int above = index + _size.LayerStride;
            return above < _size.CellCount && AllowsLine(above) ? above : index;
        }

        // ---- orders (§3) ------------------------------------------------------------------------

        /// <summary>
        /// Order a line into the cell an order named at <paramref name="index"/> lands in. The same
        /// three answers every order gives: <see cref="IntentRejection.AlreadyInThatState"/> where
        /// a line or an order for one is already there, <see cref="IntentRejection.NotPermitted"/>
        /// where the rule refuses it.
        /// </summary>
        public IntentRejection PlaceLine(int index)
        {
            if ((uint)index >= (uint)_size.CellCount) return IntentRejection.OutOfBounds;
            index = WhereItWouldLand(index);
            if (IsLine(index) || HasSite(index)) return IntentRejection.AlreadyInThatState;
            if (!AllowsLine(index)) return IntentRejection.NotPermitted;

            int at = _sites.BinarySearch(index);
            _sites.Insert(~at, index);
            _siteWork.Insert(~at, 0);
            Version++;
            return IntentRejection.None;
        }

        /// <summary>
        /// Take back every line order in the cell a cancel named — an unlaid line or a removal
        /// mark — and say whether there was one. Nothing is refunded, because nothing is ever
        /// delivered to a line order: the colonist who lays it carries the wood and spends it there.
        /// </summary>
        public bool CancelAt(int index)
        {
            if ((uint)index >= (uint)_size.CellCount) return false;
            bool any = CancelOne(index);
            // Named at the ground, the order standing on it — the same lift the cancel of a
            // building makes (ConstructionGrid.SiteAt).
            if (!any && _grid.IsSolidTerrain(index))
            {
                int above = index + _size.LayerStride;
                if (above < _size.CellCount) any = CancelOne(above);
            }
            return any;
        }

        bool CancelOne(int index)
        {
            int site = _sites.BinarySearch(index);
            if (site >= 0)
            {
                _sites.RemoveAt(site);
                _siteWork.RemoveAt(site);
                Version++;
                return true;
            }

            int mark = _marks.BinarySearch(index);
            if (mark < 0) return false;
            _marks.RemoveAt(mark);
            _markWork.RemoveAt(mark);
            Version++;
            return true;
        }

        /// <summary>
        /// Mark the built line in the cell named here for taking up. Refused where there is no line
        /// (<see cref="IntentRejection.NotPermitted"/>) and where it is already marked.
        /// </summary>
        public IntentRejection MarkRemoval(int index)
        {
            if ((uint)index >= (uint)_size.CellCount) return IntentRejection.OutOfBounds;
            if (!IsLine(index) && _grid.IsSolidTerrain(index))
            {
                int above = index + _size.LayerStride;
                if (above < _size.CellCount) index = above;
            }
            if (!IsLine(index)) return IntentRejection.NotPermitted;
            int at = _marks.BinarySearch(index);
            if (at >= 0) return IntentRejection.AlreadyInThatState;

            _marks.Insert(~at, index);
            _markWork.Insert(~at, 0);
            Version++;
            return IntentRejection.None;
        }

        /// <summary>Milliwork banked on an ordered line, or 0 where there is no order.</summary>
        public int SiteWork(int index)
        {
            int at = _sites.BinarySearch(index);
            return at >= 0 ? _siteWork[at] : 0;
        }

        /// <summary>Bank milliwork on an ordered line and return the new total; 0 where there is no order.</summary>
        public int AddSiteWork(int index, int milliwork)
        {
            int at = _sites.BinarySearch(index);
            if (at < 0) return 0;
            _siteWork[at] += milliwork;
            return _siteWork[at];
        }

        /// <summary>Milliwork banked on a removal mark, or 0 where there is none.</summary>
        public int MarkWork(int index)
        {
            int at = _marks.BinarySearch(index);
            return at >= 0 ? _markWork[at] : 0;
        }

        /// <summary>Bank milliwork on a removal mark and return the new total; 0 where there is none.</summary>
        public int AddMarkWork(int index, int milliwork)
        {
            int at = _marks.BinarySearch(index);
            if (at < 0) return 0;
            _markWork[at] += milliwork;
            return _markWork[at];
        }

        /// <summary>
        /// The ordered line in this cell goes in: the order comes off and the line is laid. Called
        /// from the deferred phase by the laying job. False where the order has gone in the
        /// meantime, or the cell no longer takes a line.
        /// </summary>
        public bool Lay(int index)
        {
            int at = _sites.BinarySearch(index);
            if (at < 0) return false;
            _sites.RemoveAt(at);
            _siteWork.RemoveAt(at);
            Version++;
            if (!AllowsLine(index)) return false;
            AddLine(index);
            return true;
        }

        /// <summary>
        /// Take the marked line in this cell out. Called from the deferred phase by the removing
        /// job. False where the mark or the line has gone in the meantime.
        /// </summary>
        public bool TakeUp(int index)
        {
            int at = _marks.BinarySearch(index);
            if (at < 0) return false;
            _marks.RemoveAt(at);
            _markWork.RemoveAt(at);
            Version++;
            if (!IsLine(index)) return false;
            RemoveLine(index);
            return true;
        }

        /// <summary>Put a line down directly, bypassing the order. For tests and for a scenario.</summary>
        public void AddLine(int index)
        {
            if ((uint)index >= (uint)_size.CellCount || IsLine(index)) return;
            _bits[index >> 6] |= 1UL << (index & 63);
            int at = _lines.BinarySearch(index);
            _lines.Insert(~at, index);
            MarkDirty();
        }

        void RemoveLine(int index)
        {
            if (!IsLine(index)) return;
            _bits[index >> 6] &= ~(1UL << (index & 63));
            _lines.RemoveAt(_lines.BinarySearch(index));
            MarkDirty();
        }

        // ---- the power buildings (§5, §6) -----------------------------------------------------

        /// <summary>
        /// A power building has just been raised as this edifice record: give it a record of its
        /// own, switched on and with an empty hopper. Called by <c>ConstructionGrid.Raise</c>.
        /// </summary>
        public void AddDevice(int edifice)
        {
            if (DeviceIndex(edifice) >= 0) return;
            int at = ~FindDevice(edifice);
            _devices.Insert(at, new Device { Edifice = edifice, On = true });
            MarkDirty();
        }

        /// <summary>The building has come down: its record goes with it. Called by <c>ConstructionGrid.Demolish</c>.</summary>
        public void RemoveDevice(int edifice)
        {
            int at = DeviceIndex(edifice);
            if (at < 0) return;
            _devices.RemoveAt(at);
            MarkDirty();
        }

        /// <summary>
        /// Make the records agree with what is standing, for a colony that has just been loaded: a
        /// power building with no record gets a default one, and a record whose building is gone
        /// is dropped. On any save this code wrote the two already agree and it does nothing; it
        /// is the repair for a file that disagrees, not a second way records arrive.
        /// </summary>
        public void Reconcile()
        {
            for (int i = _devices.Count - 1; i >= 0; i--)
                if (!IsStandingPowerBuilding(_devices[i].Edifice)) { _devices.RemoveAt(i); MarkDirty(); }

            for (int e = 0; e < _edifices.Count; e++)
                if (IsStandingPowerBuilding(e) && DeviceIndex(e) < 0) AddDevice(e);

            MarkDirty();
        }

        bool IsStandingPowerBuilding(int edifice)
        {
            if ((uint)edifice >= (uint)_edifices.Count) return false;
            PlacedEdifice placed = _edifices[edifice];
            return !placed.Removed && DefOf(placed.Def) is { IsPowered: true };
        }

        /// <summary>The building def a power building's edifice id names, or null.</summary>
        static BuildingDef? DefOf(ushort edifice)
        {
            int building = ConstructionContent.BuildingForEdifice(edifice);
            return building == BuildingHandle.None ? null : ConstructionContent.BuildingAt(building);
        }

        /// <summary>The record's position for this edifice index, or -1.</summary>
        public int DeviceIndex(int edifice)
        {
            int at = FindDevice(edifice);
            return at >= 0 ? at : -1;
        }

        int FindDevice(int edifice)
        {
            int lo = 0, hi = _devices.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int e = _devices[mid].Edifice;
                if (e == edifice) return mid;
                if (e < edifice) lo = mid + 1; else hi = mid - 1;
            }
            return ~lo;
        }

        /// <summary>The power building standing in this cell, as its edifice index, or -1.</summary>
        public int EdificeAt(int cell)
        {
            if ((uint)cell >= (uint)_size.CellCount) return -1;
            int handle = _grid.Edifice[cell];
            return handle >= 0 && DeviceIndex(handle) >= 0 ? handle : -1;
        }

        /// <summary>
        /// Switch the power building in this cell on or off (<c>SetPowerSwitch</c>): either cell
        /// of a two-cell building names it, and it applies at once (§5).
        /// </summary>
        public IntentRejection SetSwitch(int cell, bool on)
        {
            if ((uint)cell >= (uint)_size.CellCount) return IntentRejection.OutOfBounds;
            int at = DeviceIndex(EdificeAt(cell));
            if (at < 0) return IntentRejection.NotPermitted;
            Device d = _devices[at];
            if (d.On == on) return IntentRejection.AlreadyInThatState;
            d.On = on;
            _devices[at] = d;
            MarkDirty();
            return IntentRejection.None;
        }

        /// <summary>Fuel in the hopper of this power building, milli-units; 0 for none.</summary>
        public int FuelMilli(int edifice)
        {
            int at = DeviceIndex(edifice);
            return at >= 0 ? _devices[at].FuelMilli : 0;
        }

        /// <summary>
        /// Does this building want fuel fetched — does it burn something, and is its hopper below
        /// half (§6)? The refuel giver's question, asked of the record so it cannot disagree with
        /// what the burn reads.
        /// </summary>
        public bool NeedsRefuel(int edifice)
        {
            int at = DeviceIndex(edifice);
            if (at < 0) return false;
            BuildingDef? def = DefOf(_edifices[edifice].Def);
            if (def == null || def.fuelItem < 0 || def.fuelCapacity <= 0) return false;
            return _devices[at].FuelMilli * 2 < def.fuelCapacity * Milli;
        }

        /// <summary>Whole units of fuel that would fill this building's hopper to the top.</summary>
        public int RoomForFuel(int edifice)
        {
            int at = DeviceIndex(edifice);
            if (at < 0) return 0;
            BuildingDef? def = DefOf(_edifices[edifice].Def);
            if (def == null || def.fuelItem < 0) return 0;
            int room = (def.fuelCapacity * Milli - _devices[at].FuelMilli) / Milli;
            return room < 0 ? 0 : room;
        }

        /// <summary>
        /// Tip whole units of fuel into the hopper, never past the top; returns how many went in.
        /// A hopper that was empty makes the net worth solving again.
        /// </summary>
        public int AddFuel(int edifice, int units)
        {
            int at = DeviceIndex(edifice);
            if (at < 0 || units <= 0) return 0;
            int take = Math.Min(units, RoomForFuel(edifice));
            if (take <= 0) return 0;
            Device d = _devices[at];
            bool wasEmpty = d.FuelMilli <= 0;
            d.FuelMilli += take * Milli;
            _devices[at] = d;
            if (wasEmpty) MarkDirty();
            return take;
        }

        /// <summary>Set a hopper directly, milli-units. For tests and for the debug menu.</summary>
        public void SetFuelMilli(int edifice, int milli)
        {
            int at = DeviceIndex(edifice);
            if (at < 0) return;
            Device d = _devices[at];
            d.FuelMilli = Math.Max(0, milli);
            _devices[at] = d;
            MarkDirty();
        }

        // ---- the nets (§4) --------------------------------------------------------------------

        bool _dirty = true;

        /// <summary>The net each line is on, as an index into <see cref="_nets"/>; parallel to <see cref="_lines"/>.</summary>
        readonly List<int> _lineNet = new List<int>();
        readonly List<Net> _nets = new List<Net>();


        /// <summary>Per device, parallel to <see cref="_devices"/>: its net index or -1, whether it is
        /// powered (a consumer) or running (a generator), and a generator's share of its net's demand.</summary>
        readonly List<int> _deviceNet = new List<int>();
        readonly List<bool> _devicePowered = new List<bool>();
        readonly List<int> _deviceLoad = new List<int>();

        /// <summary>A connected set of lines, and what is asked of it.</summary>
        public struct Net
        {
            /// <summary>The lowest line cell in it: deterministic, and the room key's rule.</summary>
            public int Key;
            public int SupplyW;
            public int DemandW;
            public PowerNetState State;
        }

        public IReadOnlyList<Net> Nets { get { EnsureSolved(); return _nets; } }

        void MarkDirty()
        {
            _dirty = true;
        }

        /// <summary>
        /// Solve the nets, attach the buildings and strike the balance, if anything has changed
        /// since the last time. Every reader calls it first; a clean grid returns at once.
        /// </summary>
        public void EnsureSolved()
        {
            if (!_dirty) return;
            _dirty = false;

            SolveNets();
            AttachDevices();
            Balance();
        }

        /// <summary>
        /// Join every line to the lines across its faces, and number what comes out.
        ///
        /// <para><b>A union-find over the sorted list, walked with three pointers.</b> The first
        /// cut flooded outward with a binary search per face and measured 3.1 ms for one edit at
        /// 10,000 lines (§11) — the NavGraph fault the audit found, told again. Every face is
        /// found from its lower side instead: the east neighbour is the next cell in the list or
        /// nothing, and the north and upper neighbours are <c>cell + SizeX</c> and
        /// <c>cell + LayerStride</c>, which only ever grow as the walk does — so a pointer for each
        /// advances monotonically and the whole solve is linear in lines, with no search.</para>
        ///
        /// <para><b>The root of a set is always its lowest position</b>, so a net's key is its
        /// lowest cell and the nets come out in key order when numbered by first appearance —
        /// which is what lets "the lowest key" be "the lowest index" everywhere below.</para>
        /// </summary>
        void SolveNets()
        {
            _nets.Clear();
            _lineNet.Clear();
            int n = _lines.Count;
            if (_parent.Length < n) _parent = new int[Math.Max(n, _parent.Length * 2)];
            for (int p = 0; p < n; p++) _parent[p] = p;

            int north = 0, up = 0;
            int sizeX = _size.SizeX, stride = _size.LayerStride;
            for (int p = 0; p < n; p++)
            {
                int cell = _lines[p];
                int x = cell % sizeX;
                int z = cell / sizeX % _size.SizeZ;
                int y = cell / stride;

                if (x + 1 < sizeX && p + 1 < n && _lines[p + 1] == cell + 1) Union(p, p + 1);

                if (z + 1 < _size.SizeZ)
                {
                    int target = cell + sizeX;
                    while (north < n && _lines[north] < target) north++;
                    if (north < n && _lines[north] == target) Union(p, north);
                }

                if (y + 1 < _size.SizeY)
                {
                    int target = cell + stride;
                    while (up < n && _lines[up] < target) up++;
                    if (up < n && _lines[up] == target) Union(p, up);
                }
            }

            for (int p = 0; p < n; p++)
            {
                int root = Find(p);
                if (root == p)
                {
                    _lineNet.Add(_nets.Count);
                    _nets.Add(new Net { Key = _lines[p] });
                }
                else
                {
                    // The root is below p and was numbered first.
                    _lineNet.Add(_lineNet[root]);
                }
            }
        }

        int[] _parent = new int[64];

        int Find(int p)
        {
            while (_parent[p] != p)
            {
                _parent[p] = _parent[_parent[p]];
                p = _parent[p];
            }
            return p;
        }

        /// <summary>Join two sets, the lower root winning, so every root is its set's lowest position.</summary>
        void Union(int a, int b)
        {
            a = Find(a);
            b = Find(b);
            if (a == b) return;
            if (a < b) _parent[b] = a; else _parent[a] = b;
        }

        /// <summary>
        /// The cell across one of the six faces, or -1 off the board. Faces 0–3 are north, east,
        /// south and west on the same layer; 4 is up and 5 is down. Diagonals are not faces: two
        /// lines touching corner to corner are two nets.
        /// </summary>
        int Neighbour(CellRef c, int face)
        {
            int x = c.X, z = c.Z, y = c.Y;
            switch (face)
            {
                case 0: z++; break;
                case 1: x++; break;
                case 2: z--; break;
                case 3: x--; break;
                case 4: y++; break;
                default: y--; break;
            }
            return _size.Contains(x, z, y) ? _size.Index(x, z, y) : -1;
        }

        /// <summary>The net index the line in this cell is on, or -1 where there is no line.</summary>
        int NetOfLine(int cell)
        {
            if (!IsLine(cell)) return -1;
            return _lineNet[_lines.BinarySearch(cell)];
        }

        void AttachDevices()
        {
            _deviceNet.Clear();
            for (int i = 0; i < _devices.Count; i++)
            {
                int best = -1;
                int edifice = _devices[i].Edifice;
                if ((uint)edifice < (uint)_edifices.Count)
                {
                    PlacedEdifice placed = _edifices[edifice];
                    best = BestNetAround(placed.CellIndex, best);
                    int second = EdificeFootprint.SecondCell(placed.CellIndex, placed.Def, placed.Facing, _size);
                    if (second >= 0) best = BestNetAround(second, best);
                }
                _deviceNet.Add(best);
            }
        }

        /// <summary>
        /// Of the lines in this cell and across its six faces, the net with the lowest key — which
        /// is the lowest net index, because nets are made in key order. A building touching two
        /// nets joins one of them and never bridges them (§4).
        /// </summary>
        int BestNetAround(int cell, int best)
        {
            int here = NetOfLine(cell);
            if (here >= 0 && (best < 0 || here < best)) best = here;

            CellRef c = _size.FromIndex(cell);
            for (int face = 0; face < 6; face++)
            {
                int n = Neighbour(c, face);
                if (n < 0) continue;
                int net = NetOfLine(n);
                if (net >= 0 && (best < 0 || net < best)) best = net;
            }
            return best;
        }

        /// <summary>
        /// Strike each net's balance: what its running generators can make against what its
        /// switched-on consumers want. Demand counts consumers that are <b>on</b>, not ones that
        /// are powered — counting only the powered would let a dark net shed its own demand, relight
        /// and go dark again for ever, which is the flicker a-07 records against the reference.
        /// </summary>
        void Balance()
        {
            _devicePowered.Clear();
            _deviceLoad.Clear();
            for (int i = 0; i < _devices.Count; i++)
            {
                _devicePowered.Add(false);
                _deviceLoad.Add(0);
            }

            for (int i = 0; i < _devices.Count; i++)
            {
                int net = _deviceNet[i];
                if (net < 0) continue;
                BuildingDef? def = DefOfDevice(i);
                if (def == null) continue;

                Net n = _nets[net];
                if (IsRunningGenerator(i, def)) n.SupplyW += def.powerOutputW;
                else if (def.powerDrawW > 0 && _devices[i].On) n.DemandW += def.powerDrawW;
                _nets[net] = n;
            }

            for (int k = 0; k < _nets.Count; k++)
            {
                Net n = _nets[k];
                n.State = n.DemandW > n.SupplyW ? PowerNetState.Dark
                    : n.SupplyW > 0 ? PowerNetState.Live
                    : PowerNetState.Idle;
                _nets[k] = n;
            }

            // The shares: each running generator on a live net carries demand × its output ÷ the
            // net's supply, rounded down, and the odd watts go to the first of them — the lowest
            // edifice index, because the records are in that order. Their sum is the demand
            // exactly, so the burn is the load and never more.
            for (int k = 0; k < _nets.Count; k++)
            {
                Net n = _nets[k];
                if (n.State != PowerNetState.Live || n.DemandW <= 0) continue;

                int given = 0, first = -1;
                for (int i = 0; i < _devices.Count; i++)
                {
                    if (_deviceNet[i] != k) continue;
                    BuildingDef? def = DefOfDevice(i);
                    if (def == null || !IsRunningGenerator(i, def)) continue;
                    int share = (int)((long)n.DemandW * def.powerOutputW / n.SupplyW);
                    _deviceLoad[i] = share;
                    given += share;
                    if (first < 0) first = i;
                }
                if (first >= 0) _deviceLoad[first] += n.DemandW - given;
            }

            for (int i = 0; i < _devices.Count; i++)
            {
                int net = _deviceNet[i];
                BuildingDef? def = DefOfDevice(i);
                if (net < 0 || def == null) continue;
                _devicePowered[i] = def.powerDrawW > 0
                    ? _devices[i].On && _nets[net].State == PowerNetState.Live
                    : IsRunningGenerator(i, def);
            }

            Version++;
        }

        bool IsRunningGenerator(int device, BuildingDef def) =>
            def.powerOutputW > 0 && _devices[device].On && _deviceNet[device] >= 0
            && (def.fuelItem < 0 || _devices[device].FuelMilli > 0);

        BuildingDef? DefOfDevice(int device)
        {
            int edifice = _devices[device].Edifice;
            return (uint)edifice < (uint)_edifices.Count ? DefOf(_edifices[edifice].Def) : null;
        }

        // ---- what the rest of the world asks ------------------------------------------------

        /// <summary>
        /// The heat this power building puts into its room this pass (design 32 §6–§7): a heater's
        /// whole <c>heatPerPass</c> while it is powered and on, a generator's in proportion to the
        /// load it carries, nothing otherwise. The thermal pass asks this instead of its own table
        /// for every power building.
        /// </summary>
        public int HeatOf(int edifice)
        {
            EnsureSolved();
            int at = DeviceIndex(edifice);
            if (at < 0) return 0;
            BuildingDef? def = DefOfDevice(at);
            if (def == null || def.heatPerPass == 0) return 0;

            if (def.powerDrawW > 0) return _devicePowered[at] ? def.heatPerPass : 0;
            if (def.powerOutputW > 0) return (int)((long)def.heatPerPass * _deviceLoad[at] / def.powerOutputW);
            return 0;
        }

        /// <summary>Is this consumer powered, or this generator running? False for anything else.</summary>
        public bool IsPowered(int edifice)
        {
            EnsureSolved();
            int at = DeviceIndex(edifice);
            return at >= 0 && _devicePowered[at];
        }

        /// <summary>The key of the net this power building is attached to, or -1.</summary>
        public int NetKeyOf(int edifice)
        {
            EnsureSolved();
            int at = DeviceIndex(edifice);
            if (at < 0 || _deviceNet[at] < 0) return -1;
            return _nets[_deviceNet[at]].Key;
        }

        /// <summary>The net a line in this cell is on, or null.</summary>
        public Net? NetAtLine(int cell)
        {
            EnsureSolved();
            int net = NetOfLine(cell);
            return net >= 0 ? _nets[net] : (Net?)null;
        }

        /// <summary>The net this power building is on, or null.</summary>
        public Net? NetOf(int edifice)
        {
            EnsureSolved();
            int at = DeviceIndex(edifice);
            return at >= 0 && _deviceNet[at] >= 0 ? _nets[_deviceNet[at]] : (Net?)null;
        }

        /// <summary>The watts this generator is carrying now, or 0.</summary>
        public int LoadOf(int edifice)
        {
            EnsureSolved();
            int at = DeviceIndex(edifice);
            return at >= 0 ? _deviceLoad[at] : 0;
        }

        /// <summary>
        /// The six faces of a line that another line is across, as bits 0–5 in <see cref="Neighbour"/>'s
        /// order. Published so presentation draws the links without working adjacency out itself
        /// (process §3).
        /// </summary>
        public byte LinksOf(int cell)
        {
            if (!IsLine(cell)) return 0;
            CellRef c = _size.FromIndex(cell);
            byte links = 0;
            for (int face = 0; face < 6; face++)
            {
                int n = Neighbour(c, face);
                if (n >= 0 && IsLine(n)) links |= (byte)(1 << face);
            }
            return links;
        }

        // ---- IWorldSystem: the burn (§6) --------------------------------------------------------

        public string Name => "Power";

        public TickPhase Phase => TickPhase.WorldSystems;

        /// <summary>
        /// After growing (40) and before the thermal pass (50), which asks <see cref="HeatOf"/>
        /// on the same tick: the heat it reads is the heat of the load this pass burned for.
        /// </summary>
        public int Order => 45;

        /// <summary>
        /// Every <see cref="IntervalTicks"/>: each running generator on a live net burns for its
        /// share of the net's demand. Linear in power buildings; a colony with none returns at once.
        ///
        /// <para><c>acc += fuelPerDay × 1000 × share; burn = acc ÷ (output × 500)</c>, the
        /// remainder carried in the record, so at full load a generator burns exactly its
        /// <c>fuelPerDay</c> over a day and at a fifth of it exactly a fifth, with nothing lost
        /// to rounding on any pass (§6).</para>
        /// </summary>
        public void Tick(SimWorld world)
        {
            if (world.CurrentTick % IntervalTicks != 0) return;
            if (_devices.Count == 0) return;
            EnsureSolved();

            bool emptied = false;
            for (int i = 0; i < _devices.Count; i++)
            {
                int load = _deviceLoad[i];
                if (load <= 0) continue;
                BuildingDef? def = DefOfDevice(i);
                if (def == null || def.fuelItem < 0 || def.powerOutputW <= 0) continue;

                Device d = _devices[i];
                int per = def.powerOutputW * PassesPerDay;
                // In long arithmetic: fuelPerDay × Milli × load can pass int range for a big
                // generator on a big net, and a wrapped burn is a generator that makes fuel.
                long acc = (long)d.BurnRemainder + (long)def.fuelPerDay * Milli * load;
                int burn = (int)(acc / per);
                d.BurnRemainder = (int)(acc - (long)burn * per);
                d.FuelMilli -= burn;
                if (d.FuelMilli <= 0)
                {
                    d.FuelMilli = 0;
                    d.BurnRemainder = 0;
                    emptied = true;
                }
                _devices[i] = d;
            }

            // An empty hopper stops a generator, and its net may go dark: solved again the next
            // time anything asks, which is the thermal pass later this same tick.
            if (emptied) MarkDirty();
        }

        // ---- ISnapshotContributor (§9, §10) ------------------------------------------------------

        /// <summary>The built lines' rows, rebuilt only when <see cref="Version"/> moves, so a
        /// frame spent watching copies a list rather than walking the net.</summary>
        readonly List<ConduitView> _lineViews = new List<ConduitView>();
        int _lineViewsVersion = -1;

        /// <summary>
        /// Every power building and every net, always — a handful of each. Every ordered line and
        /// every removal mark, always, as every other standing order is. And the built lines
        /// <b>only while the interface is watching</b> (<see cref="WorldViewStore.WatchPower"/>,
        /// process §3), from a cache the walk of the net fills once per change.
        /// </summary>
        public void Contribute(SimWorld world, SnapshotWriter writer)
        {
            if (IsEmpty) return;
            EnsureSolved();
            writer.SetPowerVersion(Version);

            for (int i = 0; i < _nets.Count; i++)
                writer.AddPowerNet(new PowerNetView(_nets[i].Key, _nets[i].SupplyW, _nets[i].DemandW, _nets[i].State));

            for (int i = 0; i < _devices.Count; i++)
            {
                BuildingDef? def = DefOfDevice(i);
                if (def == null) continue;
                PlacedEdifice placed = _edifices[_devices[i].Edifice];
                int net = _deviceNet[i];
                bool generator = def.powerOutputW > 0;
                writer.AddPowerDevice(new PowerDeviceView(
                    placed.CellIndex,
                    EdificeFootprint.SecondCell(placed.CellIndex, placed.Def, placed.Facing, _size),
                    (byte)ConstructionContent.BuildingForEdifice(placed.Def),
                    generator ? PowerRole.Generator : PowerRole.Consumer,
                    _devices[i].On,
                    _devicePowered[i],
                    net >= 0 ? _nets[net].Key : -1,
                    generator ? def.powerOutputW : def.powerDrawW,
                    _deviceLoad[i],
                    _devices[i].FuelMilli,
                    def.fuelItem >= 0 ? def.fuelCapacity * Milli : 0));
            }

            for (int i = 0; i < _sites.Count; i++)
                writer.AddConduit(new ConduitView(_sites[i], ConduitKind.Ordered, PowerNetState.Idle,
                    LinksOf(_sites[i], orders: true), -1));

            // A mark is an order too, and drawn like one whether the lines are shown or not.
            // While they are shown it rides with its line below instead, so it is not drawn twice.
            if (!world.Views.WatchPower)
            {
                for (int i = 0; i < _marks.Count; i++)
                {
                    Net? n = NetAtLine(_marks[i]);
                    writer.AddConduit(new ConduitView(_marks[i], ConduitKind.Marked,
                        n?.State ?? PowerNetState.Idle, LinksOf(_marks[i]), n?.Key ?? -1));
                }
                return;
            }

            if (_lineViewsVersion != Version)
            {
                _lineViews.Clear();
                for (int i = 0; i < _lines.Count; i++)
                {
                    int cell = _lines[i];
                    Net n = _nets[_lineNet[i]];
                    ConduitKind kind = _marks.BinarySearch(cell) >= 0 ? ConduitKind.Marked : ConduitKind.Built;
                    _lineViews.Add(new ConduitView(cell, kind, n.State, LinksOf(cell), n.Key));
                }
                _lineViewsVersion = Version;
            }
            for (int i = 0; i < _lineViews.Count; i++) writer.AddConduit(_lineViews[i]);
        }

        /// <summary>The links of an order: to lines and to other orders, so a planned run draws joined.</summary>
        byte LinksOf(int cell, bool orders)
        {
            CellRef c = _size.FromIndex(cell);
            byte links = 0;
            for (int face = 0; face < 6; face++)
            {
                int n = Neighbour(c, face);
                if (n >= 0 && (IsLine(n) || HasSite(n))) links |= (byte)(1 << face);
            }
            return links;
        }

        // ---- IStateHashable ---------------------------------------------------------------------

        /// <summary>
        /// Everything saved, in the order it is saved. <b>Nothing at all while the grid is
        /// empty</b>, so a colony that has built nothing electric hashes exactly as it did before
        /// power existed (§8) — which is what keeps every golden still until the jobs arrive.
        /// </summary>
        public void ContributeTo(ref StateHash hash)
        {
            if (IsEmpty) return;

            hash.Add(_lines.Count);
            for (int i = 0; i < _lines.Count; i++) hash.Add(_lines[i]);

            hash.Add(_sites.Count);
            for (int i = 0; i < _sites.Count; i++) { hash.Add(_sites[i]); hash.Add(_siteWork[i]); }

            hash.Add(_marks.Count);
            for (int i = 0; i < _marks.Count; i++) { hash.Add(_marks[i]); hash.Add(_markWork[i]); }

            hash.Add(_devices.Count);
            for (int i = 0; i < _devices.Count; i++)
            {
                Device d = _devices[i];
                hash.Add(d.Edifice);
                hash.Add(d.On);
                hash.Add(d.FuelMilli);
                hash.Add(d.BurnRemainder);
            }
        }

        // ---- ISaveable --------------------------------------------------------------------------

        public string SaveKey => "odyssey.power";

        public void Save(SaveWriter writer)
        {
            writer.Write(SectionVersion);

            writer.Write(_lines.Count);
            for (int i = 0; i < _lines.Count; i++) writer.Write(_lines[i]);

            writer.Write(_sites.Count);
            for (int i = 0; i < _sites.Count; i++) { writer.Write(_sites[i]); writer.Write(_siteWork[i]); }

            writer.Write(_marks.Count);
            for (int i = 0; i < _marks.Count; i++) { writer.Write(_marks[i]); writer.Write(_markWork[i]); }

            writer.Write(_devices.Count);
            for (int i = 0; i < _devices.Count; i++)
            {
                Device d = _devices[i];
                writer.Write(d.Edifice);
                writer.Write(d.On);
                writer.Write(d.FuelMilli);
                writer.Write(d.BurnRemainder);
            }
        }

        public void Load(SaveReader reader)
        {
            Array.Clear(_bits, 0, _bits.Length);
            _lines.Clear();
            _sites.Clear();
            _siteWork.Clear();
            _marks.Clear();
            _markWork.Clear();
            _devices.Clear();

            int version = reader.ReadInt();
            if (version < 1 || version > SectionVersion)
                throw new InvalidOperationException($"odyssey.power section version {version} is not one this build reads.");

            int lines = reader.ReadInt();
            for (int i = 0; i < lines; i++)
            {
                int cell = reader.ReadInt();
                if ((uint)cell >= (uint)_size.CellCount || IsLine(cell)) continue;
                _bits[cell >> 6] |= 1UL << (cell & 63);
                _lines.Add(cell);
            }
            _lines.Sort();

            int sites = reader.ReadInt();
            for (int i = 0; i < sites; i++)
            {
                int cell = reader.ReadInt();
                int work = reader.ReadInt();
                if ((uint)cell >= (uint)_size.CellCount) continue;
                int at = _sites.BinarySearch(cell);
                if (at >= 0) continue;
                _sites.Insert(~at, cell);
                _siteWork.Insert(~at, work);
            }

            int marks = reader.ReadInt();
            for (int i = 0; i < marks; i++)
            {
                int cell = reader.ReadInt();
                int work = reader.ReadInt();
                if ((uint)cell >= (uint)_size.CellCount) continue;
                int at = _marks.BinarySearch(cell);
                if (at >= 0) continue;
                _marks.Insert(~at, cell);
                _markWork.Insert(~at, work);
            }

            int devices = reader.ReadInt();
            for (int i = 0; i < devices; i++)
            {
                var d = new Device
                {
                    Edifice = reader.ReadInt(),
                    On = reader.ReadBool(),
                    FuelMilli = reader.ReadInt(),
                    BurnRemainder = reader.ReadInt(),
                };
                int at = FindDevice(d.Edifice);
                if (at >= 0) continue;
                _devices.Insert(~at, d);
            }

            MarkDirty();
            Version++;
        }
    }
}
