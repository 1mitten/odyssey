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
    /// cadence, over rooms rather than cells: O(rooms + surfaces), never O(cells), which is the
    /// only reason any of this is affordable beside a 2.5 M cell board. The pass is Jacobi —
    /// every room's step is computed from the <i>old</i> temperatures and they all move at the
    /// end — so the order rooms are visited provably cannot matter.</para>
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

        public TemperatureSystem(PawnContext ctx, IReadOnlyList<Worldgen.PlacedEdifice> edifices,
            ClimateDef climate)
        {
            _ctx = ctx;
            _edifices = edifices;
            _climate = climate;
            _groundByLayer = new int[ctx.Size.SizeY];

            // Rooms resolve their starting temperature the moment they are built, not at the
            // next pass: a re-solve between passes (the enclosure sweep runs twice when a wall
            // moves) would otherwise replace a room with a copy of itself that has lost the
            // ledger, and the warm half of a split room would snap to the outdoors instead of
            // inheriting. A bare fixture's enclosure never gets one, and its rooms are simply
            // never asked about.
            if (ctx.Enclosure != null)
                ctx.Enclosure.RoomResolved += room =>
                {
                    if (_tempByKey.ContainsKey(room.Key)) return;
                    _tempByKey[room.Key] = ResolveInitial(room, ctx.CurrentTick, OutdoorTempC(ctx.CurrentTick));
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
            return room != 0 && _tempByKey.TryGetValue(room, out int temp) ? temp : OutdoorTempC(tick);
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
            for (int e = 0; e < _edifices.Count; e++)
            {
                var placed = _edifices[e];
                if (placed.Removed) continue;
                int heat = Construction.ConstructionContent
                    .BuildingAt(Construction.ConstructionContent.BuildingForEdifice(placed.Def)).heatPerPass;
                if (heat == 0) continue;
                if (_slotByKey.TryGetValue(_ctx.Enclosure!.RoomAt(placed.CellIndex), out int slot))
                    _sources[slot] += heat;
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

                // ONI's rule, per room, on the exchange half only: no step may exceed a quarter
                // of the largest difference the room faces — the cheapest known guarantee that
                // a coarse explicit integrator cannot oscillate or overshoot.
                int exchange = (int)(_deltas[i] / room.CellCount);
                int limit = _drives[i] / 4;
                if (exchange > limit) exchange = limit;
                if (exchange < -limit) exchange = -limit;

                int updated = _temps[i] + exchange + (int)(_sources[i] / room.CellCount);
                _temps[i] = updated;
                _tempByKey[room.Key] = updated;
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
        /// A room's temperature when it has none yet: the area-weighted mix of the old rooms it
        /// overlaps where the ledger has votes (a rebuild — sealing or splitting a room carries
        /// its heat rather than recomputing an equilibrium, the fault Going Medieval's own
        /// players report), and the outdoor curve for the rest, including the whole of a room
        /// that has simply never been asked about. Inherited keys are always known — entries
        /// outlive their rooms on purpose — so the mix never guesses.
        /// </summary>
        int ResolveInitial(ThermalRoom room, long tick, int outdoor)
        {
            if (_tempByKey.TryGetValue(room.Key, out int known)) return known;
            if (room.Inherit.Count == 0) return outdoor;

            long weighted = 0;
            int covered = 0;
            foreach (var pair in room.Inherit)
            {
                // A key with no temperature — a room that lived and died between two passes —
                // reads as the outdoors here, which is the only honest answer left for a room
                // nothing ever warmed.
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

        public void ContributeTo(ref StateHash hash)
        {
            int count = 0;
            foreach (var _ in OrderedPairs()) count++;
            hash.Add(count);
            foreach (var pair in OrderedPairs())
            {
                hash.Add(pair.Key);
                hash.Add(pair.Value);
            }
        }

        public string SaveKey => "odyssey.temperature";

        public void Save(SaveWriter writer)
        {
            int count = 0;
            foreach (var _ in OrderedPairs()) count++;
            writer.Write(count);
            foreach (var pair in OrderedPairs())
            {
                writer.Write(pair.Key);
                writer.Write(pair.Value);
            }
        }

        public void Load(SaveReader reader)
        {
            _tempByKey.Clear();
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int key = reader.ReadInt();
                int temp = reader.ReadInt();
                // A key this build cannot place (a save from a build whose rooms differed, which
                // a format change to worldgen could produce) is kept anyway: it costs nothing,
                // and a room that ever comes back should come back at its own temperature.
                _tempByKey[key] = temp;
            }
        }
    }
}
