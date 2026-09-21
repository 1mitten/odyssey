#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Temperature;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// Evaluates room enclosure and whether cells are indoors or outdoors.
    ///
    /// A space is strictly enclosed ("Indoors") if it is horizontally bounded on its layer by
    /// walls, doors, or solid terrain, does not touch the map edge, does not exceed the maximum
    /// room size (2,500 cells), and is 100% roofed (every interior cell has solid rock or a built
    /// floor slab on the layer immediately above).
    /// </summary>
    public sealed class EnclosureGrid : IWorldSystem
    {
        public const int MaxRoomCells = 2500;

        readonly CellGrid _cells;
        readonly IReadOnlyList<PlacedEdifice> _edifices;
        readonly bool[] _isIndoors;
        readonly bool[] _dirtyLayers;

        // Room identity, beside the indoors bool: which enclosed region each cell belongs to,
        // keyed by the region's minimum cell index (ThermalRoom.Key). Zero is nobody — outdoors,
        // a wall, a door, a room that failed one of the enclosure tests. Rebuilt by the same
        // lazy layer solve that rebuilds _isIndoors, and read by the thermal pass (design 28 §3).
        readonly int[] _roomAt;
        readonly List<ThermalRoom>[] _layerRooms;

        readonly int[] _visited;
        int _visitToken;
        readonly int[] _queue;
        readonly List<int> _currentRoomCells = new List<int>(512);
        readonly List<int> _touchingDoors = new List<int>(32);

        // Boundary records gathered across a whole layer sweep — cell, direction index (0–3)
        // and the room slot the record belongs to — classified once every fill on the layer has
        // finished, because the far side of a wall may belong to a room the sweep has not
        // reached yet. Staged per fill first and only promoted on qualification: a fill that
        // fails an enclosure test owns no room slot, and a record promoted for it would index
        // past the list or, worse, into the next room's.
        readonly List<int> _boundaryCells = new List<int>(128);
        readonly List<int> _boundaryDirs = new List<int>(128);
        readonly List<int> _boundaryOwners = new List<int>(128);
        readonly List<int> _fillBoundaryCells = new List<int>(32);
        readonly List<int> _fillBoundaryDirs = new List<int>(32);

        /// <summary>
        /// Raised for every room a solve builds, after its inheritance ledger is written — the
        /// thermal system's chance to give the room a starting temperature while the rooms it
        /// overlaps are still known. Null in a world with no thermal pass, which is every bare
        /// fixture.
        /// </summary>
        public event Action<ThermalRoom>? RoomResolved;

        public EnclosureGrid(CellGrid cells, IReadOnlyList<PlacedEdifice> edifices)
        {
            _cells = cells;
            _edifices = edifices;
            _isIndoors = new bool[cells.Size.CellCount];
            _dirtyLayers = new bool[cells.Size.SizeY];
            _roomAt = new int[cells.Size.CellCount];
            _layerRooms = new List<ThermalRoom>[cells.Size.SizeY];
            for (int y = 0; y < _layerRooms.Length; y++) _layerRooms[y] = new List<ThermalRoom>();
            _visited = new int[cells.Size.LayerStride];
            _queue = new int[cells.Size.LayerStride];
            MarkAllDirty();
        }

        public string Name => "Enclosure";

        public TickPhase Phase => TickPhase.WorldSystems;

        public int Order => 30; // After Support (10) and Navigation (20)

        public void MarkDirty(int cell)
        {
            if (cell < 0 || cell >= _cells.Size.CellCount) return;
            int y = cell / _cells.Size.LayerStride;
            MarkLayerDirty(y);
        }

        public void MarkDirty(int x, int z, int y)
        {
            if (y < 0 || y >= _cells.Size.SizeY) return;
            MarkLayerDirty(y);
        }

        /// <summary>
        /// A structural change on this layer invalidates the rooms of the layers beside it too.
        /// Below, because this layer's floors and walls are the boundaries of the rooms under
        /// them; above, because the rooms over this layer hold their cross-layer contacts
        /// (openings, shared slabs) by looking down at this layer's rooms — a wall that splits a
        /// cellar the hall above opens into must re-solve the hall's links as well. Over-marking
        /// costs one flood fill of a layer; under-marking is a room exchanging heat with a room
        /// that no longer exists.
        /// </summary>
        void MarkLayerDirty(int y)
        {
            _dirtyLayers[y] = true;
            if (y > 0) _dirtyLayers[y - 1] = true;
            if (y + 1 < _dirtyLayers.Length) _dirtyLayers[y + 1] = true;
        }

        public void MarkAllDirty()
        {
            for (int y = 0; y < _dirtyLayers.Length; y++)
                _dirtyLayers[y] = true;
        }

        public bool IsIndoors(int cell)
        {
            if (cell < 0 || cell >= _cells.Size.CellCount) return false;
            int y = cell / _cells.Size.LayerStride;
            EnsureSolved(y);
            return _isIndoors[cell];
        }

        public bool IsIndoors(int x, int z, int y)
        {
            if (!_cells.Size.Contains(x, z, y)) return false;
            EnsureSolved(y);
            return _isIndoors[_cells.Size.Index(x, z, y)];
        }

        /// <summary>
        /// The room a cell's air belongs to, by key, or 0 where it is nobody's — outdoors, a
        /// wall, a door, a space that failed an enclosure test. Lazily solved exactly as
        /// <see cref="IsIndoors(int)"/> is, and solved by the same pass, so the two answers
        /// cannot disagree.
        /// </summary>
        public int RoomAt(int cell)
        {
            if (cell < 0 || cell >= _cells.Size.CellCount) return 0;
            int y = cell / _cells.Size.LayerStride;
            EnsureSolved(y);
            return _roomAt[cell];
        }

        /// <summary>
        /// The enclosed rooms of one layer, in solve order — ascending first-cell scan, which is
        /// deterministic for a given world state. Lazily solved as above.
        /// </summary>
        public IReadOnlyList<ThermalRoom> RoomsOn(int y)
        {
            if (y < 0 || y >= _dirtyLayers.Length) return System.Array.Empty<ThermalRoom>();
            EnsureSolved(y);
            return _layerRooms[y];
        }

        /// <summary>
        /// The lazy solve runs the same convergent sweep the tick runs — one discipline, so a
        /// world asked about before its first tick and one asked after cannot disagree about
        /// which rooms exist.
        /// </summary>
        void EnsureSolved(int y)
        {
            if (!_dirtyLayers[y]) return;
            SolveAll();
        }

        public void Tick(SimWorld world) => SolveAll();

        /// <summary>
        /// Sweep the dirty layers ascending until a sweep changes nothing. A layer's fills read
        /// the room table of the layer above (the shaft rule), so each sweep pulls one more
        /// layer of a shaft chain to life — a cellar under a loft under an attic takes three —
        /// and stopping before fixed point would leave different worlds disagreeing about
        /// which rooms exist: the round trip found exactly that, a played world and a loaded
        /// one hashing apart over caverns three layers deep. The sweeps only run when something
        /// structural moved, which is every ordinary tick's one branch per layer, and they stop
        /// the moment a sweep is stable.
        /// </summary>
        void SolveAll()
        {
            bool any = false;
            for (int y = 0; y < _dirtyLayers.Length; y++)
                if (_dirtyLayers[y]) { any = true; break; }
            if (!any) return;

            var previous = new List<ThermalRoom>();
            for (int sweep = 0; sweep < _dirtyLayers.Length; sweep++)
            {
                bool changed = false;
                for (int y = 0; y < _dirtyLayers.Length; y++)
                {
                    if (!_dirtyLayers[y]) continue;
                    previous.Clear();
                    previous.AddRange(_layerRooms[y]);
                    SolveLayer(y);
                    // Re-mark for the next sweep: solving y may have changed what the layers
                    // beside it should read.
                    _dirtyLayers[y] = true;
                    if (!SameRooms(previous, _layerRooms[y])) changed = true;
                }
                if (!changed) break;
            }

            for (int y = 0; y < _dirtyLayers.Length; y++) _dirtyLayers[y] = false;
        }

        static bool SameRooms(List<ThermalRoom> a, List<ThermalRoom> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].Key != b[i].Key || a[i].CellCount != b[i].CellCount) return false;
            return true;
        }

        public void SolveLayer(int y)
        {
            _dirtyLayers[y] = false;
            GridSize size = _cells.Size;
            int stride = size.LayerStride;
            int baseCell = y * stride;

            // The old rooms of this layer, kept until the inheritance ledger is written — the
            // new rooms are about to replace them, and a rebuilt room's starting temperature is
            // the area-weighted mix of the old rooms it overlaps (design 28 §5).
            List<ThermalRoom> oldRooms = _layerRooms[y];
            List<ThermalRoom> rooms = new List<ThermalRoom>(oldRooms.Count);
            var byKey = new Dictionary<int, ThermalRoom>(oldRooms.Count + 4);

            Array.Clear(_isIndoors, baseCell, stride);
            Array.Clear(_roomAt, baseCell, stride);

            _visitToken++;
            if (_visitToken == int.MaxValue)
            {
                Array.Clear(_visited, 0, _visited.Length);
                _visitToken = 1;
            }

            _boundaryCells.Clear();
            _boundaryDirs.Clear();
            _boundaryOwners.Clear();

            int sizeX = size.SizeX;
            int sizeZ = size.SizeZ;

            for (int localIdx = 0; localIdx < stride; localIdx++)
            {
                int cell = baseCell + localIdx;

                if (IsBoundary(cell)) continue;
                if (_visited[localIdx] == _visitToken) continue;

                _currentRoomCells.Clear();
                _touchingDoors.Clear();
                _fillBoundaryCells.Clear();
                _fillBoundaryDirs.Clear();

                int head = 0, tail = 0;
                _queue[tail++] = localIdx;
                _visited[localIdx] = _visitToken;

                bool touchesMapEdge = false;
                bool lacksRoof = false;
                bool exceededLimit = false;
                int minCell = int.MaxValue;

                while (head < tail)
                {
                    int currLocal = _queue[head++];
                    int currCell = baseCell + currLocal;
                    _currentRoomCells.Add(currCell);
                    if (currCell < minCell) minCell = currCell;

                    if (!HasRoof(currCell) && !OpensIntoRoomAbove(currCell))
                        lacksRoof = true;

                    int x = currLocal % sizeX;
                    int z = currLocal / sizeX;

                    if (x == 0 || x == sizeX - 1 || z == 0 || z == sizeZ - 1)
                        touchesMapEdge = true;

                    if (_currentRoomCells.Count > MaxRoomCells)
                    {
                        exceededLimit = true;
                        break;
                    }

                    // North (z + 1)
                    if (z + 1 < sizeZ) ProcessNeighbour(baseCell, currLocal + sizeX, 0, ref tail);
                    else touchesMapEdge = true;

                    // South (z - 1)
                    if (z > 0) ProcessNeighbour(baseCell, currLocal - sizeX, 1, ref tail);
                    else touchesMapEdge = true;

                    // East (x + 1)
                    if (x + 1 < sizeX) ProcessNeighbour(baseCell, currLocal + 1, 2, ref tail);
                    else touchesMapEdge = true;

                    // West (x - 1)
                    if (x > 0) ProcessNeighbour(baseCell, currLocal - 1, 3, ref tail);
                    else touchesMapEdge = true;
                }

                bool isRoomIndoors = !touchesMapEdge && !lacksRoof && !exceededLimit;

                if (isRoomIndoors)
                {
                    for (int i = 0; i < _currentRoomCells.Count; i++)
                    {
                        int roomCell = _currentRoomCells[i];
                        _isIndoors[roomCell] = true;
                        _roomAt[roomCell] = minCell;
                    }

                    for (int i = 0; i < _touchingDoors.Count; i++)
                    {
                        int doorCell = _touchingDoors[i];
                        if (HasRoof(doorCell))
                            _isIndoors[doorCell] = true;
                    }

                    var room = BuildRoom(minCell, y, baseCell);
                    rooms.Add(room);
                    byKey[minCell] = room;

                    // Promoted only now that the fill has qualified and the slot exists — see
                    // the fields' own note for why a failed fill must not stage anything.
                    for (int i = 0; i < _fillBoundaryCells.Count; i++)
                    {
                        _boundaryCells.Add(_fillBoundaryCells[i]);
                        _boundaryDirs.Add(_fillBoundaryDirs[i]);
                        _boundaryOwners.Add(rooms.Count - 1);
                    }
                }
            }

            // The inheritance ledger, while the old rooms still know their cells: every cell of
            // every old room votes for the new room that now contains it. A room that came
            // through a re-solve unchanged votes for itself and is skipped — a self-entry says
            // nothing the missing key doesn't, and a room that has never been temperature-
            // resolved would then ask for its own absent temperature.
            for (int o = 0; o < oldRooms.Count; o++)
            {
                ThermalRoom old = oldRooms[o];
                for (int i = 0; i < old.Cells.Count; i++)
                {
                    int key = _roomAt[old.Cells[i]];
                    if (key == 0 || key == old.Key) continue;
                    if (!byKey.TryGetValue(key, out ThermalRoom? into) || into == null)
                        continue;
                    into.Inherit.TryGetValue(old.Key, out int count);
                    into.Inherit[old.Key] = count + 1;
                }
            }

            // Boundary classification, now that every fill on the layer has finished: the far
            // side of a wall or door may belong to a room the sweep reached later.
            for (int i = 0; i < _boundaryCells.Count; i++)
                ClassifyBoundary(_boundaryCells[i], _boundaryDirs[i], rooms[_boundaryOwners[i]]);

            // And the rooms themselves, ledger and surfaces settled — the thermal system's
            // starting temperatures, which must see the ledger and so run last.
            if (RoomResolved != null)
                for (int r = 0; r < rooms.Count; r++) RoomResolved(rooms[r]);

            _layerRooms[y] = rooms;
        }

        /// <summary>
        /// Build the room's cached surfaces from the settled fill: ceilings against sky and rock,
        /// floors against ground and the rooms below, and the cells themselves. Cross-layer
        /// contacts are recorded from this (upper) room only — layers solve in ascending order,
        /// so the layer below is always fresh when this one reads it.
        /// </summary>
        ThermalRoom BuildRoom(int key, int y, int baseCell)
        {
            GridSize size = _cells.Size;
            int stride = size.LayerStride;

            var room = new ThermalRoom
            {
                Key = key,
                Layer = y,
                CellCount = _currentRoomCells.Count,
            };

            for (int i = 0; i < _currentRoomCells.Count; i++)
            {
                int cell = _currentRoomCells[i];
                room.Cells.Add(cell);

                // The ceiling: solid rock above is the ground boundary; anything else is this
                // room's roof against the open air. A room above shares this slab, and records
                // the contact itself — from above, where the layer order makes it fresh.
                int above = cell + stride;
                if (_cells.IsSolidTerrain(above)) room.CeilingRockCells++;
                else room.CeilingSkyCells++;

                // The floor: a slab over another room is a shared slab (recorded here, this room
                // being the upper of the two); a slab or bare ground over anything else is the
                // ground boundary; no slab over open air is a hole to the outdoors.
                if (_cells.Floor[cell] != CoreContent.SlabNone)
                {
                    int below = cell - stride;
                    int belowRoom = below >= 0 ? _roomAt[below] : 0;
                    if (belowRoom != 0) AddSlabLink(room, belowRoom);
                    else room.FloorRockCells++;
                }
                else if (cell - stride < 0 || _cells.IsSolidTerrain(cell - stride))
                {
                    room.FloorRockCells++;
                }
                else if (_roomAt[cell - stride] != 0)
                {
                    room.Openings.Add(new OpeningLink(_roomAt[cell - stride], cell));
                }
                else
                {
                    room.FloorHoleCells++;
                }
            }

            return room;
        }

        void AddSlabLink(ThermalRoom room, int lowerKey)
        {
            for (int i = 0; i < room.SlabLinks.Count; i++)
            {
                if (room.SlabLinks[i].Other != lowerKey) continue;
                room.SlabLinks[i] = new RoomLink(lowerKey, room.SlabLinks[i].PerMille + TemperatureConductance.SlabPerMille);
                return;
            }
            room.SlabLinks.Add(new RoomLink(lowerKey, TemperatureConductance.SlabPerMille));
        }

        /// <summary>
        /// Classify one boundary record — a wall or door cell on a room's edge, with the
        /// direction the fill was walking when it met it — into the room's cached surfaces.
        /// The far cell is the one beyond the boundary thing; off the layer's edge it is the
        /// outdoors.
        /// </summary>
        void ClassifyBoundary(int cell, int dir, ThermalRoom room)
        {
            GridSize size = _cells.Size;
            int stride = size.LayerStride;
            int local = cell - room.Layer * stride;
            int x = local % size.SizeX;
            int z = local / size.SizeX;

            int dx = dir == 2 ? 1 : dir == 3 ? -1 : 0;
            int dz = dir == 0 ? 1 : dir == 1 ? -1 : 0;
            int fx = x + dx, fz = z + dz;

            int far;
            bool farOnBoard = fx >= 0 && fx < size.SizeX && fz >= 0 && fz < size.SizeZ;
            if (farOnBoard) far = cell + dz * size.SizeX + dx;
            else far = -1;

            if (IsDoor(cell))
            {
                int farRoom = farOnBoard && far >= 0 ? _roomAt[far] : 0;
                room.Doors.Add(new DoorLink(farRoom, cell));
                return;
            }

            // A wall. Its material sets the conductance where the far side is air; the far side
            // itself decides which boundary the wall is against.
            int stuffPerMille = TemperatureConductance.WallStuffPerMille(WallStuffOf(cell));

            int farRoomKey = farOnBoard && far >= 0 && !IsBoundary(far) ? _roomAt[far] : 0;
            if (!farOnBoard || far < 0 || IsBoundary(far) || _cells.IsSolidTerrain(far))
            {
                // Solid rock, another boundary thing (the dead air of a double wall), or off the
                // board: the ground boundary. Material is irrelevant — the earth is the
                // boundary, not the wall.
                room.WallRockCells++;
            }
            else if (farRoomKey != 0)
            {
                AddWallLink(room, farRoomKey, 4 * stuffPerMille / 1000);
            }
            else
            {
                room.WallOutdoorPerMille += 4 * stuffPerMille / 1000;
            }
        }

        void AddWallLink(ThermalRoom room, int otherKey, int perMille)
        {
            for (int i = 0; i < room.WallLinks.Count; i++)
            {
                if (room.WallLinks[i].Other != otherKey) continue;
                room.WallLinks[i] = new RoomLink(otherKey, room.WallLinks[i].PerMille + perMille);
                return;
            }
            room.WallLinks.Add(new RoomLink(otherKey, perMille));
        }

        /// <summary>The material a standing wall is made of, or none for the rock the generator
        /// laid — which the conductance table reads as the standard material.</summary>
        ushort WallStuffOf(int cell)
        {
            int handle = _cells.Edifice[cell];
            if (handle >= 0 && handle < _edifices.Count && !_edifices[handle].Removed)
                return _edifices[handle].Stuff;
            return 0;
        }

        void ProcessNeighbour(int baseCell, int nLocal, int dir, ref int tail)
        {
            int nCell = baseCell + nLocal;
            if (IsDoor(nCell))
            {
                _touchingDoors.Add(nCell);
                _fillBoundaryCells.Add(nCell);
                _fillBoundaryDirs.Add(dir);
                return;
            }

            if (IsWallOrRock(nCell))
            {
                _fillBoundaryCells.Add(nCell);
                _fillBoundaryDirs.Add(dir);
                return;
            }

            if (_visited[nLocal] != _visitToken)
            {
                _visited[nLocal] = _visitToken;
                _queue[tail++] = nLocal;
            }
        }

        public bool IsDoor(int cell)
        {
            int handle = _cells.Edifice[cell];
            if (handle >= 0 && handle < _edifices.Count)
            {
                PlacedEdifice placed = _edifices[handle];
                return !placed.Removed && placed.Def == CoreContent.EdificeDoor;
            }
            return false;
        }

        public bool IsWallOrRock(int cell)
        {
            if (_cells.IsSolidTerrain(cell)) return true;
            int handle = _cells.Edifice[cell];
            if (handle >= 0 && handle < _edifices.Count)
            {
                PlacedEdifice placed = _edifices[handle];
                return !placed.Removed && placed.Def == CoreContent.EdificeWall;
            }
            return false;
        }

        public bool IsBoundary(int cell) => IsWallOrRock(cell) || IsDoor(cell);

        public bool HasRoof(int cell)
        {
            int y = cell / _cells.Size.LayerStride;
            if (y + 1 >= _cells.Size.SizeY) return false;
            int above = cell + _cells.Size.LayerStride;
            return _cells.Floor[above] != CoreContent.SlabNone || _cells.IsSolidTerrain(above);
        }

        /// <summary>
        /// The shaft rule (design 28 §3): a cell with no slab above is still enclosed when what
        /// it opens into is the room above — a stairwell, a ladder shaft, a hatch — because the
        /// thermal model carries that hole as an <c>Opening</c> surface with conductance of its
        /// own, and a cellar with a way up is a cellar and not the sky. Only a hole to open air
        /// breaks enclosure. Reads the layer above's room table as it last solved, which is why
        /// <see cref="Tick"/> sweeps twice.
        /// </summary>
        bool OpensIntoRoomAbove(int cell)
        {
            int above = cell + _cells.Size.LayerStride;
            return above < _cells.Size.CellCount && _roomAt[above] != 0;
        }
    }
}
