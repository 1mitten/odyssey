#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
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

        readonly int[] _visited;
        int _visitToken;
        readonly int[] _queue;
        readonly List<int> _currentRoomCells = new List<int>(512);
        readonly List<int> _touchingDoors = new List<int>(32);

        public EnclosureGrid(CellGrid cells, IReadOnlyList<PlacedEdifice> edifices)
        {
            _cells = cells;
            _edifices = edifices;
            _isIndoors = new bool[cells.Size.CellCount];
            _dirtyLayers = new bool[cells.Size.SizeY];
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
            _dirtyLayers[y] = true;
            if (y > 0) _dirtyLayers[y - 1] = true;
        }

        public void MarkDirty(int x, int z, int y)
        {
            if (y < 0 || y >= _cells.Size.SizeY) return;
            _dirtyLayers[y] = true;
            if (y > 0) _dirtyLayers[y - 1] = true;
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
            if (_dirtyLayers[y]) SolveLayer(y);
            return _isIndoors[cell];
        }

        public bool IsIndoors(int x, int z, int y)
        {
            if (!_cells.Size.Contains(x, z, y)) return false;
            if (_dirtyLayers[y]) SolveLayer(y);
            return _isIndoors[_cells.Size.Index(x, z, y)];
        }

        public void Tick(SimWorld world)
        {
            for (int y = 0; y < _dirtyLayers.Length; y++)
            {
                if (_dirtyLayers[y]) SolveLayer(y);
            }
        }

        public void SolveLayer(int y)
        {
            _dirtyLayers[y] = false;
            GridSize size = _cells.Size;
            int stride = size.LayerStride;
            int baseCell = y * stride;

            Array.Clear(_isIndoors, baseCell, stride);

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

                int head = 0, tail = 0;
                _queue[tail++] = localIdx;
                _visited[localIdx] = _visitToken;

                bool touchesMapEdge = false;
                bool lacksRoof = false;
                bool exceededLimit = false;

                while (head < tail)
                {
                    int currLocal = _queue[head++];
                    int currCell = baseCell + currLocal;
                    _currentRoomCells.Add(currCell);

                    if (!HasRoof(currCell))
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
                    if (z + 1 < sizeZ) ProcessNeighbour(baseCell, currLocal + sizeX, ref tail);
                    else touchesMapEdge = true;

                    // South (z - 1)
                    if (z > 0) ProcessNeighbour(baseCell, currLocal - sizeX, ref tail);
                    else touchesMapEdge = true;

                    // East (x + 1)
                    if (x + 1 < sizeX) ProcessNeighbour(baseCell, currLocal + 1, ref tail);
                    else touchesMapEdge = true;

                    // West (x - 1)
                    if (x > 0) ProcessNeighbour(baseCell, currLocal - 1, ref tail);
                    else touchesMapEdge = true;
                }

                bool isRoomIndoors = !touchesMapEdge && !lacksRoof && !exceededLimit;

                if (isRoomIndoors)
                {
                    for (int i = 0; i < _currentRoomCells.Count; i++)
                    {
                        _isIndoors[_currentRoomCells[i]] = true;
                    }

                    for (int i = 0; i < _touchingDoors.Count; i++)
                    {
                        int doorCell = _touchingDoors[i];
                        if (HasRoof(doorCell))
                            _isIndoors[doorCell] = true;
                    }
                }
            }
        }

        void ProcessNeighbour(int baseCell, int nLocal, ref int tail)
        {
            int nCell = baseCell + nLocal;
            if (IsDoor(nCell))
            {
                _touchingDoors.Add(nCell);
                return;
            }

            if (IsWallOrRock(nCell))
            {
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
    }
}
