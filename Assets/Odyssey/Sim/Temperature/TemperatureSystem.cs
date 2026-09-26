#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.Temperature
{
    /// <summary>
    /// The thermal pass (design 28): every enclosed room is one scalar, and every 120 ticks each
    /// room takes one explicit-Euler step toward its boundaries and its neighbours through the
    /// surfaces <see cref="EnclosureGrid"/> caches. Unenclosed air is never integrated — it reads
    /// the outdoor curve, which is a pure function of the tick.
    ///
    /// <para><b>Cadence and cost.</b> One pass per 120 ticks, matching the reference's own
    /// cadence, over rooms rather than cells: <b>O(rooms + surfaces + standing edifices)</b>,
    /// never O(cells), which is the only reason any of this is affordable beside a 2.5 M cell
    /// board. The pass is Jacobi — every room's step is computed from the <i>old</i> temperatures
    /// and they all move at the end — so the order rooms are visited provably cannot matter.</para>
    ///
    /// <para><b>The edifice term is real and it is the one that grows.</b> This summary claimed
    /// "O(rooms + surfaces)" until it was measured against a board with the edifice count printed
    /// beside the time (<c>TemperatureCostProbe</c>), and the shape came out the other way round:
    /// 250 × 250 × 40 barren — 2.5 M cells, <b>0 rooms, 5 edifices</b> — cost 0.0054 ms, while
    /// 240 × 240 × 16 wooded — 69 rooms, <b>6,311</b> edifices — cost <b>0.171 ms</b>. Nothing in
    /// those two numbers is the cell count and almost all of the second is the sweep for heat
    /// sources, which has to visit every standing thing to find the ones that are warm. A wooded
    /// board is mostly trees, so whatever that sweep does <i>per edifice</i> is what the pass
    /// costs — and it was asking <c>ConstructionContent.BuildingForEdifice</c>, a linear scan of
    /// the building table, which is a scan inside a sweep.</para>
    ///
    /// <para><b>0.171 ms → 0.051 ms on that board, and 0.041 → 0.013 on the played one</b>, both
    /// arms in one run so the ratio is this machine's own: <see cref="_heatByEdificeDef"/> is the
    /// same answer precomputed once, so the inner step is an array read. The term itself is still
    /// there. A source list maintained as things are raised and pulled down would remove it
    /// altogether and is the next move if it is ever the reason for a number; it is not today, at
    /// one pass in 120 ticks and 0.0004 ms a tick amortised on the largest board offered.</para>
    ///
    /// <para><b>State is one dictionary</b>, room key → centi-degrees. Everything else — the
    /// surfaces, the rooms themselves — is derived and rebuilds with the enclosure solve. That
    /// dictionary is the whole of what is saved and hashed.</para>
    /// </summary>
    public sealed class TemperatureSystem : IWorldSystem, IStateHashable, ISaveable
    {
        /// <summary>Pass interval, in ticks. The reference's own cadence, adopted as ours.</summary>
        public const int IntervalTicks = 120;

        readonly PawnContext _ctx;
        readonly ClimateDef _climate;

        /// <summary>Air temperature by room key — the one piece of authored-by-the-simulation
        /// state. Entries outlive the rooms that made them rather than being pruned on rebuild,
        /// because a room that comes back with the same key must come back at its own
        /// temperature, and a dictionary that grows by one entry per wall a player builds is a
        /// dictionary that never matters.</summary>
        readonly Dictionary<int, int> _tempByKey = new Dictionary<int, int>(64);

        /// <summary>
        /// Energy owed to a room that its cell count would not yet divide into one centi-degree,
        /// in centi-degree-cells, 0 ≤ r &lt; cells. Without it a source smaller than the room —
        /// one colonist's 15 in a 16-cell bedroom — truncated to nothing every pass and warmed
        /// nobody, and every room had a dead band around equilibrium the size of its own cell
        /// count (design 28 §12, F7). Saved and hashed beside the temperature, because a pass
        /// reads it.
        /// </summary>
        readonly Dictionary<int, int> _residualByKey = new Dictionary<int, int>(64);
        readonly List<int> _orphans = new List<int>(16);

        // Pass scratch, reused: rooms gathered across the layers, in (layer, solve-slot) order.
        readonly List<ThermalRoom> _rooms = new List<ThermalRoom>(64);
        readonly Dictionary<int, int> _slotByKey = new Dictionary<int, int>(64);
        // Pass scratch, resized rather than reallocated when a colony grows past the start.
        int[] _temps = new int[64];
        long[] _deltas = new long[64];
        long[] _sources = new long[64];
        int[] _drives = new int[64];
        readonly int[] _groundByLayer;

        /// <summary>The standing edifices, for the heat sources among them. The composition
        /// root's own list, the same one the enclosure grid reads — never a second copy.</summary>
        readonly IReadOnlyList<Worldgen.PlacedEdifice> _edifices;

        /// <summary>
        /// <c>heatPerPass</c> by edifice id, built once from the content.
        ///
        /// <para>The sweep for heat sources visits every standing edifice, and on a wooded board
        /// almost all of them are trees — so whatever it does per edifice is what the pass costs.
        /// It asked <c>ConstructionContent.BuildingForEdifice</c>, which is a linear scan of the
        /// building table, so the inner work was a scan inside a sweep. This is the same answer
        /// with the scan done once. It is a <i>derived cache of content</i>, not state: nothing
        /// saves it, nothing hashes it, and a Def reload rebuilds the system that holds it.</para>
        ///
        /// <para>Sized off the table rather than off <c>EdificeHandle.Count</c>, because the
        /// generators stamp ids this table has never heard of and an id past the end must read as
        /// "not a heat source" rather than as an index.</para>
        /// </summary>
        readonly int[] _heatByEdificeDef;

        /// <summary>Edifice ids whose heat the power grid decides (design 32 §7), indexed as the table above.</summary>
        readonly bool[] _powerDef;
        /// <summary><c>radiantC</c> by edifice id, built once from the content beside
        /// <see cref="_heatByEdificeDef"/> and for the same reason.</summary>
        readonly int[] _radiantByEdificeDef;

        /// <summary>
        /// Where the warm things are and how warm, refreshed by each pass (design 36).
        ///
        /// <para>This is the whole of what radiance costs in storage: a handful of entries, not a
        /// field over 2.5 M cells. <see cref="CellTemp"/> walks it, so a board with no fire on it
        /// pays one <c>Count == 0</c> test and nothing else — which is every board in the game
        /// until somebody builds one.</para>
        /// </summary>
        readonly List<RadiantSource> _radiant = new List<RadiantSource>(8);

        readonly struct RadiantSource
        {
            public readonly int Cell;
            public readonly int Room;
            public readonly int CentiC;

            public RadiantSource(int cell, int room, int centiC)
            {
                Cell = cell;
                Room = room;
                CentiC = centiC;
            }
        }

        public TemperatureSystem(PawnContext ctx, IReadOnlyList<Worldgen.PlacedEdifice> edifices,
            ClimateDef climate)
        {
            _ctx = ctx;
            _edifices = edifices;
            _climate = climate;
            _groundByLayer = new int[ctx.Size.SizeY];

            var table = Construction.ConstructionContent.Buildings;
            int widest = 0;
            for (int i = 1; i < table.Count; i++)
                if (table[i].edifice > widest) widest = table[i].edifice;
            _heatByEdificeDef = new int[widest + 1];
            _powerDef = new bool[widest + 1];
            for (int i = 1; i < table.Count; i++)
                if (table[i].IsPowered && table[i].heatPerPass != 0) _powerDef[table[i].edifice] = true;
            // A power building's heat is not in this table: it is gated on the building being
            // powered, or burning, and the power grid owns that answer (design 32 §7). Its row
            // stays zero here so an unpowered heater never warms a room through the back door.
            for (int i = 1; i < table.Count; i++)
                if (table[i].heatPerPass != 0 && !table[i].IsPowered)
                    _heatByEdificeDef[table[i].edifice] = table[i].heatPerPass;
            // Radiance is its own table and deliberately does NOT re-fill the heat one above.
            // The first cut of this merge did, and main's own
            // PowerHeatTests.ARoomWarmsOnlyWhileItsHeaterIsPowered caught it: writing every
            // building's heatPerPass back over that array undoes the gate on the line above it,
            // and an unpowered heater warms its room through the back door.
            _radiantByEdificeDef = new int[widest + 1];
            for (int i = 1; i < table.Count; i++)
                if (table[i].radiantC != 0) _radiantByEdificeDef[table[i].edifice] = table[i].radiantC;

            // Rooms resolve their starting temperature the moment they are built, not at the
            // next pass, while the ledger of what their cells used to be is fresh. A room from
            // a layer's first ever fill keeps whatever this system already holds for its key —
            // that is a save reattaching — and every later room is the area-weighted mix of the
            // ledger: itself, exactly, when nothing moved; both old rooms when two were joined;
            // the curve for cells that were outdoors. The first cut returned any known key
            // untouched, so a re-sealed room came back at the temperature it had a season ago
            // and a knocked-through hall took the cupboard's (design 28 §12, F4 and F5).
            if (ctx.Enclosure != null)
                ctx.Enclosure.RoomResolved += room =>
                {
                    int outdoor = OutdoorTempC(ctx.CurrentTick);
                    if (room.FirstSolve)
                    {
                        if (!_tempByKey.ContainsKey(room.Key)) _tempByKey[room.Key] = outdoor;
                        return;
                    }
                    _tempByKey[room.Key] = Inherited(room, outdoor);
                    // The residual is energy owed to a particular volume; a room whose cells
                    // moved is a different volume and starts square.
                    if (!room.Inherit.TryGetValue(room.Key, out int self) || self != room.CellCount)
                        _residualByKey.Remove(room.Key);
                };
        }

        public string Name => "Temperature";

        public TickPhase Phase => TickPhase.WorldSystems;

        /// <summary>
        /// After Enclosure (30) and growing (40): the pass reads settled rooms, and nothing after
        /// it in the phase needs this tick's temperatures — the needs system asks on its own
        /// cadence and the pane asks through the snapshot, both later than the whole phase.
        /// </summary>
        public int Order => 50;

        // ---- the curves -----------------------------------------------------------------------

        /// <summary>
        /// The fixed daily shape, per-mille of <see cref="ClimateDef.dailyAmplitudeC"/>: a cosine
        /// peaking at 14h and bottoming at 02h. A literal table rather than a computed one —
        /// <c>Math.Cos</c> at load time is a float the two runtimes could round apart on a knife
        /// edge, and the whole model is integers on purpose.
        /// </summary>
        public static readonly int[] DailyShape =
        {
            -866, -966, -1000, -966, -866, -707, -500, -259, 0, 259, 500, 707,
            866, 966, 1000, 966, 866, 707, 500, 259, 0, -259, -500, -707,
        };

        /// <summary>
        /// What an incident's weather does to the outdoors, in centi-degrees — a cold snap's
        /// −25 °C, one day. Zero today and nothing sets it; the field is the seam, so the
        /// incidents that arrive with weather add to the curve instead of restructuring it
        /// (design 28 §10).
        /// </summary>
        public int WeatherOffsetC { get; set; }

        /// <summary>The outdoor temperature at a tick — the answer every unenclosed cell gives,
        /// and the boundary above every roof.</summary>
        public int OutdoorTempC(long tick)
        {
            int month = Calendar.MonthOfYear(tick);
            int hour = Calendar.HourOfDay(tick);
            return _climate.annualMeanC + _climate.monthlyOffsetC[month]
                 + _climate.dailyAmplitudeC * DailyShape[hour] / 1000
                 + WeatherOffsetC;
        }

        /// <summary>
        /// The ground's temperature at a layer: the seasonal term alone (no daily swing — the
        /// ground never notices an afternoon), damped by depth. Damping compounds one layer at a
        /// time by integer multiplication, so no depth ever takes a logarithm or a float.
        /// </summary>
        public int GroundTempC(int layer)
        {
            int depth = _ctx.Size.SizeY - 1 - layer;
            int seasonal = _climate.monthlyOffsetC[Calendar.MonthOfYear(_ctx.CurrentTick)];
            int damp = 1_000;
            for (int d = 0; d < depth; d++)
            {
                damp = damp * _climate.groundOneLayerDampingPerMille / 1_000;
                if (damp <= 0) { damp = 1; break; }
            }
            return _climate.annualMeanC + seasonal * damp / 1_000;
        }

        /// <summary>
        /// The ambient temperature of one cell: its room's air where it is in a room, the
        /// outdoor curve where it is not. The question the pane, the needs system and the growth
        /// pass all ask, answered in one place so they cannot disagree.
        /// </summary>
        public int CellTemp(int cell, long tick)
        {
            if ((uint)cell >= (uint)_ctx.Size.CellCount) return OutdoorTempC(tick);
            int room = _ctx.Enclosure != null ? _ctx.Enclosure.RoomAt(cell) : 0;
            return RoomTempC(cell, tick) + RadianceAt(cell, room);
        }

        /// <summary>
        /// The <b>air</b> at this cell and nothing else: its room's scalar, or the outdoor curve
        /// where it is in no room. What design 28 models.
        ///
        /// <para><b>Ask for this when you mean the room, and <see cref="CellTemp"/> when you mean
        /// what it is like to stand somewhere.</b> The two were one method until radiance arrived
        /// (design 36), and the day they parted four tests of the air model started failing —
        /// not because the air had changed but because they were reading a cell with a fire in it
        /// and getting the fire as well. That is the right answer to the question
        /// <c>CellTemp</c> asks and the wrong answer to the one they were asking, which is why
        /// there are now two questions.</para>
        /// </summary>
        public int RoomTempC(int cell, long tick)
        {
            if ((uint)cell >= (uint)_ctx.Size.CellCount) return OutdoorTempC(tick);
            int room = _ctx.Enclosure != null ? _ctx.Enclosure.RoomAt(cell) : 0;
            return room != 0 && _tempByKey.TryGetValue(room, out int temp) ? temp : OutdoorTempC(tick);
        }

        /// <summary>
        /// What standing here adds on top of the room's air, in centi-degrees: the radiance of
        /// every heat source close enough to shine on this cell (design 36).
        ///
        /// <para><b>Design 28 models the air and this does not.</b> That model is one scalar per
        /// room and refuses — correctly — to store anything per cell, because per-cell is what
        /// makes a thermal model unaffordable beside a 2.5 M cell board. Radiance needs no storage
        /// at all: it is a pure function of how far away the fire is, so it is computed on the
        /// way out rather than kept. The two are different physics as well as different code — a
        /// fire warms the room slowly by heating its air, and warms <i>you</i> at once by shining
        /// on you — and keeping them named apart is what stops one being folded into the other.
        /// </para>
        ///
        /// <para><b>Walls stop it, and the test for that is one comparison.</b> A source only
        /// reaches cells in its own room, which is right for a fire behind a wall and costs no
        /// ray. Outdoors both rooms are 0, so the test passes and distance alone decides, which
        /// is also right: a fire in a field does warm the grass beside it.</para>
        ///
        /// <para><b>Chebyshev</b>, so the ring is square and matches the grid the player is
        /// looking at. Radiance does not cross layers: a fire is not a floor heater.</para>
        /// </summary>
        /// <summary>
        /// How many heat sources are standing, as of the last pass. A fireside is somewhere warm
        /// to be, and the thing that makes it warm is the thing this already tracks — so the job
        /// system asks here rather than keeping a second list of campfires that could disagree
        /// with this one about where the fires are (design 33 §2).
        /// </summary>
        public int HeatSourceCount => _radiant.Count;

        /// <summary>The cell a standing heat source occupies.</summary>
        public int HeatSourceCell(int index) => _radiant[index].Cell;

        public int RadianceAt(int cell, int room)
        {
            if (_radiant.Count == 0) return 0;

            GridSize size = _ctx.Size;
            CellRef at = size.FromIndex(cell);

            int total = 0;
            for (int i = 0; i < _radiant.Count; i++)
            {
                RadiantSource source = _radiant[i];
                if (source.Room != room) continue;

                CellRef from = size.FromIndex(source.Cell);
                if (from.Y != at.Y) continue;

                int dx = at.X > from.X ? at.X - from.X : from.X - at.X;
                int dz = at.Z > from.Z ? at.Z - from.Z : from.Z - at.Z;
                int steps = dx > dz ? dx : dz;
                if (steps > TemperatureConductance.RadiantRangeCells) continue;

                // Integer falloff, compounded a cell at a time, the way the ground's damping is:
                // no power, no float, and the same answer on both runtimes.
                int warmth = source.CentiC;
                for (int s = 0; s < steps; s++)
                    warmth = warmth * TemperatureConductance.RadiantFalloffPerMille / 1_000;

                total += warmth;
            }

            return total;
        }

        // ---- the pass -------------------------------------------------------------------------

        public void Tick(SimWorld world)
        {
            if (world.CurrentTick % IntervalTicks != 0) return;
            _ctx.Sync(world);

            long tick = world.CurrentTick;
            int outdoor = OutdoorTempC(tick);
            for (int y = 0; y < _ctx.Size.SizeY; y++) _groundByLayer[y] = GroundTempC(y);

            GatherRooms();
            int count = _rooms.Count;
            for (int i = 0; i < count; i++) _temps[i] = ResolveInitial(_rooms[i], tick, outdoor);

            for (int i = 0; i < count; i++) Exchange(_rooms[i], i, outdoor);

            // Sources push energy, not temperature — a campfire is the same campfire in a
            // cupboard and in a hall; what differs is the room's capacity to absorb it. Kept
            // apart from the exchanges, because the quarter clamp below is a bound on how fast
            // two temperatures may approach each other and a source is not an approach: a fire
            // in a room at one with the outdoors has every right to push it away.
            _radiant.Clear();
            for (int e = 0; e < _edifices.Count; e++)
            {
                var placed = _edifices[e];
                if (placed.Removed) continue;
                if (placed.Def >= _heatByEdificeDef.Length) continue;

                int room = _ctx.Enclosure!.RoomAt(placed.CellIndex);

                // The air half: energy into the room (design 28 §7).
                int heat = _heatByEdificeDef[placed.Def];

                // A power building's heat is the power grid's answer — a heater's while it is
                // powered, a generator's in proportion to its load (design 32 §6–§7). Asked only
                // of a building the table left at zero, so the woodland's trees cost one read.
                if (heat == 0 && _ctx.Power != null && _powerDef[placed.Def]) heat = _ctx.Power.HeatOf(e);
                if (heat != 0 && _slotByKey.TryGetValue(room, out int slot)) _sources[slot] += heat;

                // And the radiant half, which is not room-bound and is remembered rather than
                // applied: CellTemp asks it per cell, on the way out (design 36).
                //
                // Deliberately NOT gated on power, unlike the air half above. radiantC is what
                // a thing is like to stand beside, and a campfire — the only source that has
                // one today — burns whether or not there is a grid. The day a heater wants a
                // radiant ring it will want it only while powered, and that is the moment to
                // gate this too rather than now, on a guess.
                int radiant = _radiantByEdificeDef[placed.Def];
                if (radiant != 0) _radiant.Add(new RadiantSource(placed.CellIndex, room, radiant));
            }

            var pawns = _ctx.Pawns.All;
            int bodyHeat = _ctx.Content.Temperature.bodyHeatPerPass;
            for (int p = 0; p < pawns.Count; p++)
            {
                // Body heat, gated off in the heat the way the reference gates it: a crowded
                // room warms toward comfort and then stops, rather than running away.
                if (!_slotByKey.TryGetValue(_ctx.Enclosure!.RoomAt(pawns[p].Cell), out int slot)) continue;
                if (_temps[slot] >= _ctx.Content.Temperature.bodyHeatGateC) continue;
                _sources[slot] += bodyHeat;
            }

            for (int i = 0; i < count; i++)
            {
                var room = _rooms[i];
                int cells = room.CellCount;

                // ONI's rule, per room, on the exchange half only: no step may exceed a quarter
                // of the largest difference the room faces — the cheapest known guarantee that
                // a coarse explicit integrator cannot oscillate or overshoot. Applied in
                // centi-degree-cells, before the division, so that what the clamp leaves is
                // still counted to the last unit.
                long exchange = _deltas[i];
                long limit = (long)(_drives[i] / 4) * cells;
                if (exchange > limit) exchange = limit;
                if (exchange < -limit) exchange = -limit;

                // Energy in, this pass and what earlier passes could not yet spend; the room
                // moves by the whole centi-degrees and keeps the change, so nothing is ever
                // lost to the division (F7).
                _residualByKey.TryGetValue(room.Key, out int residual);
                long total = exchange + _sources[i] + residual;
                long step = total / cells;
                long rem = total - step * cells;
                if (rem < 0) { step--; rem += cells; }

                int updated = _temps[i] + (int)step;
                _temps[i] = updated;
                _tempByKey[room.Key] = updated;
                _residualByKey[room.Key] = (int)rem;
            }

            // Entries for rooms that no longer exist go, so that what is held is what is saved
            // and hashed, and a room that comes back resolves from what its cells were rather
            // than from what it was (F4).
            _orphans.Clear();
            foreach (var pair in _tempByKey)
                if (!_slotByKey.ContainsKey(pair.Key)) _orphans.Add(pair.Key);
            for (int i = 0; i < _orphans.Count; i++)
            {
                _tempByKey.Remove(_orphans[i]);
                _residualByKey.Remove(_orphans[i]);
            }
        }

        void GatherRooms()
        {
            _rooms.Clear();
            _slotByKey.Clear();
            var enclosure = _ctx.Enclosure!;
            for (int y = 0; y < _ctx.Size.SizeY; y++)
            {
                var layerRooms = enclosure.RoomsOn(y);
                for (int r = 0; r < layerRooms.Count; r++)
                {
                    _slotByKey[layerRooms[r].Key] = _rooms.Count;
                    _rooms.Add(layerRooms[r]);
                }
            }

            if (_rooms.Count > _temps.Length)
            {
                int capacity = _temps.Length;
                while (capacity < _rooms.Count) capacity *= 2;
                System.Array.Resize(ref _temps, capacity);
                System.Array.Resize(ref _deltas, capacity);
                System.Array.Resize(ref _sources, capacity);
                System.Array.Resize(ref _drives, capacity);
            }

            for (int i = 0; i < _rooms.Count; i++) _deltas[i] = 0;
            for (int i = 0; i < _rooms.Count; i++) _sources[i] = 0;
            for (int i = 0; i < _rooms.Count; i++) _drives[i] = 0;
        }

        /// <summary>
        /// A room's temperature at the pass: what the ledger holds for its key, which every
        /// live room has from the moment it was resolved, or the curve for a room built with no
        /// enclosure event to hear (a fixture's).
        /// </summary>
        int ResolveInitial(ThermalRoom room, long tick, int outdoor) =>
            _tempByKey.TryGetValue(room.Key, out int known) ? known : outdoor;

        /// <summary>
        /// A resolved room's starting temperature: the area-weighted mix of the old rooms its
        /// cells were in — sealing or splitting a room carries its heat rather than recomputing
        /// an equilibrium, the fault Going Medieval's own players report — and the outdoor curve
        /// for every cell that was nobody's. A room that came through unchanged votes for itself
        /// with every cell and so keeps its temperature to the unit.
        /// </summary>
        int Inherited(ThermalRoom room, int outdoor)
        {
            if (room.Inherit.Count == 0) return outdoor;

            long weighted = 0;
            int covered = 0;
            foreach (var pair in room.Inherit)
            {
                if (!_tempByKey.TryGetValue(pair.Key, out int oldTemp)) continue;
                weighted += (long)oldTemp * pair.Value;
                covered += pair.Value;
            }
            int uncovered = room.CellCount - covered;
            if (uncovered > 0) weighted += (long)outdoor * uncovered;
            return (int)(weighted / room.CellCount);
        }

        void Exchange(ThermalRoom room, int slot, int outdoor)
        {
            int t = _temps[slot];
            long delta = _deltas[slot];
            int drive = _drives[slot];

            // The envelope: walls against the open air (the perimeter term, shrinking with room
            // size), the roof against the sky (the per-area term, which is why roofing matters
            // more than wall material in a big room), the ground under and around (which is what
            // makes digging down mean something), and any hole in the floor over the open air.
            if (room.WallOutdoorPerMille != 0 || room.CeilingSkyCells != 0 || room.FloorHoleCells != 0)
            {
                drive = MaxDrive(drive, outdoor - t);
                delta += (long)room.WallOutdoorPerMille * (outdoor - t) / 1_000
                       + (long)room.CeilingSkyCells * TemperatureConductance.RoofPerMille * (outdoor - t) / 1_000
                       + (long)room.FloorHoleCells * TemperatureConductance.FloorHolePerMille * (outdoor - t) / 1_000;
            }

            int ground = _groundByLayer[room.Layer];
            if (room.WallRockCells != 0 || room.FloorRockCells != 0)
            {
                drive = MaxDrive(drive, ground - t);
                delta += (long)(room.WallRockCells + room.FloorRockCells)
                       * TemperatureConductance.GroundPerMille * (ground - t) / 1_000;
            }

            if (room.CeilingRockCells != 0)
            {
                int groundAbove = _groundByLayer[System.Math.Min(room.Layer + 1, _ctx.Size.SizeY - 1)];
                drive = MaxDrive(drive, groundAbove - t);
                delta += (long)room.CeilingRockCells
                       * TemperatureConductance.GroundPerMille * (groundAbove - t) / 1_000;
            }

            // Wall contacts, each counted once: a shared wall appears in both rooms' ledgers,
            // from each side, so only the room with the smaller key walks it.
            for (int i = 0; i < room.WallLinks.Count; i++)
            {
                var link = room.WallLinks[i];
                if (room.Key >= link.Other || !_slotByKey.TryGetValue(link.Other, out int other)) continue;
                Flow(slot, other, link.PerMille, ref delta, ref drive);
            }

            // Slabs onto the rooms below, and the vertical openings — recorded by this room
            // because it is the upper of the pair, so each exists once.
            for (int i = 0; i < room.SlabLinks.Count; i++)
            {
                var link = room.SlabLinks[i];
                if (!_slotByKey.TryGetValue(link.Other, out int other)) continue;
                Flow(slot, other, link.PerMille, ref delta, ref drive);
            }

            for (int i = 0; i < room.Openings.Count; i++)
            {
                var opening = room.Openings[i];
                if (!_slotByKey.TryGetValue(opening.Lower, out int lower)) continue;

                // The buoyancy rule: the conductance depends on the sign of the difference, not
                // on which room is which. Warm air climbs freely; cold does not fall as fast.
                int k = _temps[lower] > t
                    ? TemperatureConductance.OpeningUpPerMille
                    : TemperatureConductance.OpeningDownPerMille;
                Flow(slot, lower, k, ref delta, ref drive);
            }

            // Doors, likewise once each: a door between two rooms is recorded from both sides.
            var flags = _ctx.Nav.Grid.Flags;
            for (int i = 0; i < room.Doors.Count; i++)
            {
                var door = room.Doors[i];
                if (door.Far != 0 && room.Key >= door.Far) continue;

                int k = (flags[door.Cell] & NavFlags.DoorOpen) != 0
                    ? TemperatureConductance.DoorOpenPerMille
                    : TemperatureConductance.DoorClosedPerMille;

                if (door.Far == 0)
                {
                    drive = MaxDrive(drive, outdoor - t);
                    delta += (long)k * (outdoor - t) / 1_000;
                }
                else if (_slotByKey.TryGetValue(door.Far, out int other))
                {
                    Flow(slot, other, k, ref delta, ref drive);
                }
            }

            _deltas[slot] = delta;
            _drives[slot] = drive;
        }

        /// <summary>One symmetric exchange between two rooms: a flow computed once from the old
        /// temperatures and applied to both ends with opposite signs.</summary>
        void Flow(int slot, int other, int perMille, ref long delta, ref int drive)
        {
            int t = _temps[slot];
            int to = _temps[other];
            long q = (long)perMille * (to - t) / 1_000;
            delta += q;
            _deltas[other] -= q;

            int magnitude = to > t ? to - t : t - to;
            if (magnitude > drive) drive = magnitude;
            if (magnitude > _drives[other]) _drives[other] = magnitude;
        }

        static int MaxDrive(int drive, int difference)
        {
            int magnitude = difference < 0 ? -difference : difference;
            return magnitude > drive ? magnitude : drive;
        }

        // ---- state: hash and save -------------------------------------------------------------

        /// <summary>
        /// Room temperatures in gathered order — layers ascending, solve slots within a layer —
        /// which is deterministic for a given world state, exactly as the rooms themselves are.
        /// </summary>
        IEnumerable<KeyValuePair<int, int>> OrderedPairs()
        {
            for (int y = 0; y < _ctx.Size.SizeY; y++)
            {
                var layerRooms = _ctx.Enclosure!.RoomsOn(y);
                for (int r = 0; r < layerRooms.Count; r++)
                    if (_tempByKey.TryGetValue(layerRooms[r].Key, out int temp))
                        yield return new KeyValuePair<int, int>(layerRooms[r].Key, temp);
            }
        }

        int ResidualOf(int key) => _residualByKey.TryGetValue(key, out int r) ? r : 0;

        public void ContributeTo(ref StateHash hash)
        {
            int count = 0;
            foreach (var _ in OrderedPairs()) count++;
            hash.Add(count);
            foreach (var pair in OrderedPairs())
            {
                hash.Add(pair.Key);
                hash.Add(pair.Value);
                hash.Add(ResidualOf(pair.Key));
            }
        }

        public string SaveKey => "odyssey.temperature";

        /// <summary>Count, then (key, temperature, residual) per live room in gathered order.
        /// Live rooms only, and the pass prunes the rest, so what is saved is what is held.</summary>
        public void Save(SaveWriter writer)
        {
            int count = 0;
            foreach (var _ in OrderedPairs()) count++;
            writer.Write(count);
            foreach (var pair in OrderedPairs())
            {
                writer.Write(pair.Key);
                writer.Write(pair.Value);
                writer.Write(ResidualOf(pair.Key));
            }
        }

        public void Load(SaveReader reader)
        {
            _tempByKey.Clear();
            _residualByKey.Clear();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int key = reader.ReadInt();
                int temp = reader.ReadInt();
                int residual = reader.ReadInt();
                // A key this build cannot place (a save from a build whose rooms differed, which
                // a format change to worldgen could produce) is kept until the first pass, which
                // prunes what no room claims.
                _tempByKey[key] = temp;
                if (residual != 0) _residualByKey[key] = residual;
            }
        }
    }
}
