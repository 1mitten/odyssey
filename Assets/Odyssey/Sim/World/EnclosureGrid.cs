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
    /// floor slab on the layer immediately above) — with the shaft rule: a cell that opens into
    /// the room above is roofed by that room (design 28 §3).
    ///
    /// <para><b>The solve is two phases, and the second reads only settled answers.</b> A layer's
    /// <i>identity</i> (which cells are in which room) depends on its own walls, on the floors of
    /// the layer above, and on the room table of the layer above (the shaft rule). Its
    /// <i>surfaces</i> (what each room exchanges heat through) depend on the room tables of the
    /// layers above and below it. So identity is solved top-down — every fill reads a layer above
    /// that is already final — and when a layer's rooms change the layer below is marked and the
    /// descent continues; surfaces are then built once for every layer whose neighbourhood
    /// moved. One fill per dirty layer, one surface build per touched layer, and no fixed-point
    /// sweep — the 2026-09-21 review measured the sweep at five to six times the cost of a
    /// solve on a wooded board and found it stopped early on a house roofed last
    /// (design 28 §12, F2 and F9).</para>
    /// </summary>
    public sealed class EnclosureGrid : IWorldSystem
    {
        public const int MaxRoomCells = 2500;

        readonly CellGrid _cells;
        readonly IReadOnlyList<PlacedEdifice> _edifices;
        readonly bool[] _isIndoors;

        // Identity dirt: this layer's fill must run again. Surface dirt: this layer's rooms must
        // re-read what is above and below them. A fill dirties surfaces around it; a fill that
        // changed its rooms dirties the identity of the layer below (whose roof it is).
        readonly bool[] _dirtyLayers;
        readonly bool[] _surfaceDirty;

        // Whether a layer has ever been filled — the one tell that separates "this room's cells
        // were outdoors" from "this grid has never looked", which the thermal system needs on a
        // load: a room found by the first solve keeps the temperature the file gave it, and a
        // room found by any later solve resolves from what its cells were (design 28 §5).
        readonly bool[] _layerSolved;

        // Room identity, beside the indoors bool: which enclosed region each cell belongs to,
        // keyed by the region's minimum cell index (ThermalRoom.Key). Zero is nobody — outdoors,
        // a wall, a door, a room that failed one of the enclosure tests.
        readonly int[] _roomAt;
        readonly List<ThermalRoom>[] _layerRooms;

        // Scratch: the layer's room table before a fill, so a fill can say whether it changed
        // anything — exactly, cell by cell, rather than by key and count.
        readonly int[] _oldRoomAt;

        readonly int[] _visited;
        int _visitToken;
        readonly int[] _queue;
        readonly List<int> _currentRoomCells = new List<int>(512);
        readonly List<int> _touchingDoors = new List<int>(32);

        // Boundary records staged per fill — cell and direction index (0–3), packed — and kept
        // on the room only once the fill has qualified: a fill that fails an enclosure test owns
        // no room, and a record kept for it would be classified against nothing.
        readonly List<int> _fillBoundary = new List<int>(64);

        /// <summary>
        /// Raised for every room a fill builds, after its inheritance ledger is written and
        /// before its surfaces are — the thermal system's chance to give the room a starting
        /// temperature while the rooms it overlaps are still known. Null in a world with no
        /// thermal pass, which is every bare fixture.
        /// </summary>
        public event Action<ThermalRoom>? RoomResolved;

        /// <summary>
        /// Moves every time a solve changes which room any cell is in. Not saved and not hashed: a
        /// counter for caches built over the rooms (the prison's cells, design 58 §5b) to know when
        /// to look again, so they never re-walk the rooms on a query.
        /// </summary>
        public int Generation
        {
            get
            {
                // Solved first, as every other question here is: a counter read before a pending
                // solve would call rooms current that are about to change.
                EnsureSolved();
                return _generation;
            }
        }

        int _generation;

        public EnclosureGrid(CellGrid cells, IReadOnlyList<PlacedEdifice> edifices)
        {
            _cells = cells;
            _edifices = edifices;
            _isIndoors = new bool[cells.Size.CellCount];
            _dirtyLayers = new bool[cells.Size.SizeY];
            _surfaceDirty = new bool[cells.Size.SizeY];
            _layerSolved = new bool[cells.Size.SizeY];
            _roomAt = new int[cells.Size.CellCount];
            _layerRooms = new List<ThermalRoom>[cells.Size.SizeY];
            for (int y = 0; y < _layerRooms.Length; y++) _layerRooms[y] = new List<ThermalRoom>();
            _oldRoomAt = new int[cells.Size.LayerStride];
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
        /// A structural change on this layer re-fills this layer and the one below (this layer's
        /// floors are its roof), and re-reads the surfaces of all three neighbours: the rooms
        /// above hold their slab links and openings by looking down at this layer's rooms, the
        /// rooms below classify their ceilings by looking up. A fill that changes its rooms
        /// carries the marks one layer further down itself (see <see cref="SolveAll"/>).
        /// </summary>
        void MarkLayerDirty(int y)
        {
            _dirtyLayers[y] = true;
            _surfaceDirty[y] = true;
            if (y > 0) { _dirtyLayers[y - 1] = true; _surfaceDirty[y - 1] = true; }
            if (y + 1 < _dirtyLayers.Length) _surfaceDirty[y + 1] = true;
        }

        public void MarkAllDirty()
        {
            for (int y = 0; y < _dirtyLayers.Length; y++)
            {
                _dirtyLayers[y] = true;
                _surfaceDirty[y] = true;
            }
        }

        public bool IsIndoors(int cell)
        {
            if (cell < 0 || cell >= _cells.Size.CellCount) return false;
            EnsureSolved();
            return _isIndoors[cell];
        }

        public bool IsIndoors(int x, int z, int y)
        {
            if (!_cells.Size.Contains(x, z, y)) return false;
            EnsureSolved();
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
            EnsureSolved();
            return _roomAt[cell];
        }

        /// <summary>
        /// The enclosed rooms of one layer, in solve order — ascending first-cell scan, which is
        /// deterministic for a given world state. Lazily solved as above.
        /// </summary>
        public IReadOnlyList<ThermalRoom> RoomsOn(int y)
        {
            if (y < 0 || y >= _dirtyLayers.Length) return System.Array.Empty<ThermalRoom>();
            EnsureSolved();
            return _layerRooms[y];
        }

        /// <summary>
        /// The lazy solve is the whole solve — one discipline, so a world asked about before its
        /// first tick and one asked after cannot disagree about which rooms exist.
        /// </summary>
        void EnsureSolved()
        {
            SolveAll();
        }

        public void Tick(SimWorld world) => SolveAll();

        /// <summary>Re-solve one layer on demand. Kept for callers that edit a layer directly;
        /// it marks and runs the whole solve, because a layer is never solved alone.</summary>
        public void SolveLayer(int y)
        {
            if (y < 0 || y >= _dirtyLayers.Length) return;
            MarkLayerDirty(y);
            SolveAll();
        }

        /// <summary>
        /// Phase one, identity, top-down: every fill reads a layer above that is already final,
        /// and a fill that changed its rooms marks the layer below — whose roof and shaft rule
        /// read this layer — so a house roofed last pulls its cellar to life on the same solve.
        /// Phase two, surfaces, once per layer whose rooms or neighbours moved, over settled
        /// room tables in both directions.
        /// </summary>
        void SolveAll()
        {
            bool any = false;
            for (int y = 0; y < _dirtyLayers.Length; y++)
                if (_dirtyLayers[y] || _surfaceDirty[y]) { any = true; break; }
            if (!any) return;

            for (int y = _dirtyLayers.Length - 1; y >= 0; y--)
            {
                if (!_dirtyLayers[y]) continue;
                bool changed = FillLayer(y);
                if (changed) _generation++;
                _dirtyLayers[y] = false;
                _surfaceDirty[y] = true;
                if (y + 1 < _dirtyLayers.Length) _surfaceDirty[y + 1] = true;
                if (y > 0)
                {
                    _surfaceDirty[y - 1] = true;
                    if (changed) _dirtyLayers[y - 1] = true;
                }
            }

            for (int y = 0; y < _surfaceDirty.Length; y++)
            {
                if (!_surfaceDirty[y]) continue;
                BuildSurfaces(y);
                _surfaceDirty[y] = false;
            }
        }

        /// <summary>
        /// Flood-fill one layer into rooms, write the inheritance ledger against the rooms it
        /// had, raise <see cref="RoomResolved"/>, and say whether any cell changed room.
        /// </summary>
        bool FillLayer(int y)
        {
            GridSize size = _cells.Size;
            int stride = size.LayerStride;
            int baseCell = y * stride;

            // The old rooms of this layer, kept until the inheritance ledger is written — the
            // new rooms are about to replace them, and a rebuilt room's starting temperature is
            // the area-weighted mix of the old rooms it overlaps (design 28 §5).
            List<ThermalRoom> oldRooms = _layerRooms[y];
            List<ThermalRoom> rooms = new List<ThermalRoom>(oldRooms.Count);
            var byKey = new Dictionary<int, ThermalRoom>(oldRooms.Count + 4);
            bool firstSolve = !_layerSolved[y];

            Array.Copy(_roomAt, baseCell, _oldRoomAt, 0, stride);
            Array.Clear(_isIndoors, baseCell, stride);
            Array.Clear(_roomAt, baseCell, stride);

            _visitToken++;
            if (_visitToken == int.MaxValue)
            {
                Array.Clear(_visited, 0, _visited.Length);
                _visitToken = 1;
            }

            int sizeX = size.SizeX;
            int sizeZ = size.SizeZ;

            for (int localIdx = 0; localIdx < stride; localIdx++)
            {
                int cell = baseCell + localIdx;

                if (IsBoundary(cell)) continue;
                if (_visited[localIdx] == _visitToken) continue;

                _currentRoomCells.Clear();
                _touchingDoors.Clear();
                _fillBoundary.Clear();

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

                    // **Flagged, never cut short** (design 58 §15d). The fill used to break here,
                    // leaving every cell it had queued marked visited and unprocessed — so a later
                    // fill that reached one treated it as a wall, and a room whose doorway opened on
                    // to a big outdoor region scanned first came out enclosed. A broken cell door
                    // left its cell a cell. Carrying on costs nothing: every cell of a layer is
                    // visited once either way, and the cells a break skipped were only flooded
                    // again as regions of their own.
                    if (_currentRoomCells.Count > MaxRoomCells) exceededLimit = true;

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
                if (!isRoomIndoors) continue;

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

                var room = new ThermalRoom
                {
                    Key = minCell,
                    Layer = y,
                    CellCount = _currentRoomCells.Count,
                    FirstSolve = firstSolve,
                };
                room.Cells.AddRange(_currentRoomCells);
                room.Boundary.AddRange(_fillBoundary);
                rooms.Add(room);
                byKey[minCell] = room;
            }

            // The inheritance ledger, while the old rooms still know their cells: every cell of
            // every old room votes for the new room that now contains it, a room that came
            // through unchanged voting for itself with every cell. The thermal system reads the
            // ledger as area weights, so an unchanged room keeps its temperature exactly, a
            // split carries it to both halves, a merge mixes by area, and a room whose cells
            // were outdoors since the last fill has no votes and starts from the curve — which
            // is the review's F4 and F5 (design 28 §12).
            for (int o = 0; o < oldRooms.Count; o++)
            {
                ThermalRoom old = oldRooms[o];
                for (int i = 0; i < old.Cells.Count; i++)
                {
                    int key = _roomAt[old.Cells[i]];
                    if (key == 0) continue;
                    if (!byKey.TryGetValue(key, out ThermalRoom? into) || into == null)
                        continue;
                    into.Inherit.TryGetValue(old.Key, out int count);
                    into.Inherit[old.Key] = count + 1;
                }
            }

            if (RoomResolved != null)
                for (int r = 0; r < rooms.Count; r++) RoomResolved(rooms[r]);

            _layerRooms[y] = rooms;
            _layerSolved[y] = true;

            for (int i = 0; i < stride; i++)
                if (_oldRoomAt[i] != _roomAt[baseCell + i]) return true;
            return false;
        }

        /// <summary>
        /// Build every room's cached surfaces on one layer from settled room tables: ceilings
        /// against sky, rock or the room above; floors against ground, the room below or the
        /// open air; and the walls and doors the fill met, classified by what is on their far
        /// side.
        /// </summary>
        void BuildSurfaces(int y)
        {
            List<ThermalRoom> rooms = _layerRooms[y];
            for (int r = 0; r < rooms.Count; r++)
            {
                ThermalRoom room = rooms[r];
                room.ClearSurfaces();
                ClassifyCells(room);
                for (int i = 0; i < room.Boundary.Count; i++)
                    ClassifyBoundary(room.Boundary[i] >> 2, room.Boundary[i] & 3, room);
            }
        }

        void ClassifyCells(ThermalRoom room)
        {
            GridSize size = _cells.Size;
            int stride = size.LayerStride;
            int cellCount = size.CellCount;

            for (int i = 0; i < room.Cells.Count; i++)
            {
                int cell = room.Cells[i];

                // The ceiling: solid rock above is the ground boundary; a cell in another room
                // above is that room's floor, and the contact — a shared slab or an opening — is
                // recorded once, by the upper room; anything else is this room's roof against
                // the open air. Counting a shared slab as sky as well made building upstairs
                // chill downstairs (design 28 §12, F6).
                int above = cell + stride;
                if (above >= cellCount || _cells.IsSolidTerrain(above)) room.CeilingRockCells++;
                else if (_roomAt[above] != 0) { /* the upper room's link */ }
                else room.CeilingSkyCells++;

                // The floor: a slab over another room is a shared slab (recorded here, this room
                // being the upper of the two); a slab or bare ground over anything else is the
                // ground boundary; no slab over open air is a hole to the outdoors.
                int below = cell - stride;
                if (_cells.Floor[cell] != CoreContent.SlabNone)
                {
                    int belowRoom = below >= 0 ? _roomAt[below] : 0;
                    if (belowRoom != 0) AddSlabLink(room, belowRoom);
                    else room.FloorRockCells++;
                }
                else if (below < 0 || _cells.IsSolidTerrain(below))
                {
                    room.FloorRockCells++;
                }
                else if (_roomAt[below] != 0)
                {
                    room.Openings.Add(new OpeningLink(_roomAt[below], cell));
                }
                else
                {
                    room.FloorHoleCells++;
                }
            }
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
                _fillBoundary.Add((nCell << 2) | dir);
                return;
            }

            if (IsWallOrRock(nCell))
            {
                _fillBoundary.Add((nCell << 2) | dir);
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
        /// breaks enclosure. Reads the layer above's room table, which the top-down fill order
        /// guarantees is final.
        /// </summary>
        bool OpensIntoRoomAbove(int cell)
        {
            int above = cell + _cells.Size.LayerStride;
            return above < _cells.Size.CellCount && _roomAt[above] != 0;
        }
    }
}
