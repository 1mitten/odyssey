#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Pathing
{
    public enum LinkKind : byte
    {
        /// <summary>A boundary span between two adjacent regions on one layer.</summary>
        Span = 0,

        /// <summary>A declared connector: the only vertical edge that can be walked.</summary>
        Portal = 1,

        /// <summary>A missing floor. One-way, downward, and excluded from districts.</summary>
        Fall = 2,
    }

    /// <summary>
    /// Regions, links and districts: the structure that answers reachability without searching.
    ///
    /// The D1 architecture benchmark timed a naive layer-aware A-star at 65% of total tick time,
    /// and 1,058 of its 1,800 replans burned their entire 20,000-node budget and then failed. The
    /// first explanation — that those were searches for targets that were never reachable at all
    /// — was tested and falsified: only 14% of the exhausted budgets were genuinely unreachable,
    /// under 1% on a structured map (<c>docs/design/05-ai-and-jobs.md</c> §6). The measured
    /// win against the tick came from the abstract region stage and its connector-density
    /// heuristic, not from reachability. This unit exists for a different, larger reason: a
    /// job-giver scan asks "can this pawn get there?" thousands of times per tick against
    /// candidate targets, and answering that with A-star at that frequency is unaffordable
    /// regardless of failure rate. <see cref="Reachable"/> has to be free — two array reads,
    /// never a search.
    ///
    /// <para>The three tiers, from <c>docs/research/d-04-pathfinding.md</c>:</para>
    /// <list type="bullet">
    /// <item><b>Regions</b> — maximal connected sets of same-kind cells inside one 10 x 10 block
    /// of one layer. A region never spans a layer and never spans a block, so a rebuild is always
    /// bounded by 100 cells however open the map is.</item>
    /// <item><b>Links</b> — boundary spans between adjacent regions, portal links declared by
    /// connectors, and one-way fall edges where a floor is missing.</item>
    /// <item><b>Districts</b> — connected components of the region graph under one traverse mode,
    /// so <see cref="Reachable"/> is <c>districtId[mode][ra] == districtId[mode][rb]</c>. One
    /// integer comparison. Not a search, and not a cache that can miss.</item>
    /// </list>
    ///
    /// <para>Determinism. Region ids come from a lowest-first free list refilled before any
    /// allocation, so a dirty block normally reclaims the ids it just released; regions inside a
    /// block are discovered by an ascending cell scan, so the first cell found is the lowest
    /// member cell; the region adjacency is rebuilt as a CSR in ascending link id, which makes
    /// the successor order of a region a function of the graph alone and not of the order the
    /// edits happened to arrive in; district floods start at the lowest live region id and walk
    /// that adjacency. An incremental rebuild and a full rebuild therefore agree on structure,
    /// on successor order, and on every path that follows from them.</para>
    /// </summary>
    public sealed class NavGraph
    {
        /// <summary>A region block: 10 x 10 cells of one layer. Never larger, never vertical.</summary>
        public const int BlockSize = 10;

        /// <summary>
        /// How far a fall edge looks for its landing. Bounded so that an edit dirties a fixed
        /// number of blocks above it rather than a whole column.
        /// </summary>
        public const int MaxFallLayers = 2;

        const int NoRegion = -1;

        readonly CellGrid _cells;

        public NavGrid Grid { get; }
        public GridSize Size { get; }
        public int BlocksX { get; }
        public int BlocksZ { get; }
        public int BlockCount { get; }

        // ---- dirty tracking -------------------------------------------------------------
        readonly bool[] _dirty;
        bool _anyDirty;
        readonly List<int> _dirtyList = new List<int>();
        readonly HashSet<int> _affectedZoneSet = new HashSet<int>();
        readonly List<int> _affectedZones = new List<int>();

        // ---- regions (structure of arrays) ----------------------------------------------
        int _regionCount;
        bool[] _regionAlive = new bool[64];
        RegionKind[] _regionKind = new RegionKind[64];
        int[] _regionBlock = new int[64];
        int[] _regionMinCell = new int[64];
        int[] _regionCells = new int[64];
        int[] _regionVersion = new int[64];
        int[] _regionNextInBlock = new int[64];

        readonly int[] _blockRegionHead;
        readonly int[] _blockRegionTail;

        readonly List<int> _freeRegions = new List<int>();
        int _freeRegionCursor;

        readonly int[] _cellRegion;

        // ---- links -----------------------------------------------------------------------
        int _linkCount;
        bool[] _linkAlive = new bool[64];
        int[] _linkA = new int[64];
        int[] _linkB = new int[64];
        LinkKind[] _linkKind = new LinkKind[64];
        int[] _linkCostAB = new int[64];
        int[] _linkCostBA = new int[64];
        byte[] _linkMode = new byte[64];
        bool[] _linkOneWay = new bool[64];
        int[] _linkCellA = new int[64];
        int[] _linkCellB = new int[64];
        int[] _linkSpan = new int[64];
        int[] _linkZone = new int[64];
        int[] _linkNextInZone = new int[64];

        int[] _zoneLinkHead;

        readonly List<int> _freeLinks = new List<int>();
        int _freeLinkCursor;

        // ---- region adjacency, CSR, rebuilt in ascending link id -------------------------
        int[] _adjStart = new int[65];
        int[] _adjCount = new int[64];
        int[] _adjLinks = new int[64];
        int _adjTotal;

        // ---- portal edges per cell, for the concrete search ------------------------------
        readonly Dictionary<int, int> _cellPortalHead = new Dictionary<int, int>();
        int _portalEdgeCount;
        int[] _peTarget = new int[64];
        int[] _peCost = new int[64];
        byte[] _peMode = new byte[64];
        int[] _peNext = new int[64];
        int[] _peLink = new int[64];
        int[] _peFrom = new int[64];
        long[] _peKey = new long[64];
        int[] _peOrder = new int[64];

        // ---- districts --------------------------------------------------------------------
        readonly int[][] _district = new int[TraverseModes.Count][];
        readonly int[] _districtCount = new int[TraverseModes.Count];

        // ---- connectors -------------------------------------------------------------------
        readonly List<Connector?> _connectors = new List<Connector?>();
        readonly Dictionary<int, List<int>> _connectorsByBlock = new Dictionary<int, List<int>>();

        // ---- scratch (reused, never allocated per operation) -------------------------------
        readonly int[] _floodStack = new int[BlockSize * BlockSize];
        readonly Dictionary<long, int> _pairScratch = new Dictionary<long, int>();
        readonly Dictionary<long, int> _fallScratch = new Dictionary<long, int>();
        int[] _bfsQueue = new int[64];

        /// <summary>Bumped whenever the graph changed, so cached corridors can be invalidated.</summary>
        public int GraphVersion { get; private set; }

        /// <summary>
        /// What a heuristic should assume one layer change costs, derived from how densely the
        /// world is stitched together vertically.
        ///
        /// Emphatically not the cost of climbing a stair. In a 250 x 250 layer with five stairs
        /// on it, the cost of going up is overwhelmingly the cost of walking to a stair, and a
        /// heuristic that charges only the climb under-estimates every cross-layer request by two
        /// orders of magnitude — which turns the abstract A-star into a Dijkstra and is worth a
        /// measured 25% of total pathfinding time. The estimate here is the mean spacing between
        /// connectors on a layer, in cells, priced at the orthogonal step cost.
        /// </summary>
        public int EstimatedLayerChangeCost { get; private set; } = MoveCost.StairUp;

        public NavGraph(CellGrid cells)
        {
            _cells = cells;
            Size = cells.Size;
            Grid = new NavGrid(Size);

            BlocksX = (Size.SizeX + BlockSize - 1) / BlockSize;
            BlocksZ = (Size.SizeZ + BlockSize - 1) / BlockSize;
            BlockCount = BlocksX * BlocksZ * Size.SizeY;

            _dirty = new bool[BlockCount];
            _blockRegionHead = new int[BlockCount];
            _blockRegionTail = new int[BlockCount];
            for (int i = 0; i < BlockCount; i++) { _blockRegionHead[i] = -1; _blockRegionTail[i] = -1; }

            _cellRegion = new int[Size.CellCount];
            for (int i = 0; i < _cellRegion.Length; i++) _cellRegion[i] = NoRegion;

            _zoneLinkHead = new int[3 * BlockCount + 16];
            for (int i = 0; i < _zoneLinkHead.Length; i++) _zoneLinkHead[i] = -1;

            for (int m = 0; m < TraverseModes.Count; m++) _district[m] = new int[64];

            MarkAllDirty();
        }

        // =====================================================================================
        // Block geometry
        // =====================================================================================

        public int BlockIndexOfCell(int x, int z, int y) =>
            (y * BlocksZ + z / BlockSize) * BlocksX + x / BlockSize;

        public int BlockIndexOfCell(int cellIndex)
        {
            CellRef c = Size.FromIndex(cellIndex);
            return BlockIndexOfCell(c.X, c.Z, c.Y);
        }

        void BlockBounds(int block, out int x0, out int x1, out int z0, out int z1, out int y)
        {
            int perLayer = BlocksX * BlocksZ;
            y = block / perLayer;
            int rem = block - y * perLayer;
            int bz = rem / BlocksX;
            int bx = rem - bz * BlocksX;
            x0 = bx * BlockSize;
            z0 = bz * BlockSize;
            x1 = Math.Min(x0 + BlockSize, Size.SizeX);
            z1 = Math.Min(z0 + BlockSize, Size.SizeZ);
        }

        // =====================================================================================
        // Dirty marking
        // =====================================================================================

        public void MarkAllDirty()
        {
            for (int i = 0; i < BlockCount; i++) _dirty[i] = true;
            _anyDirty = true;
        }

        /// <summary>
        /// Mark the block holding this cell, plus the blocks directly above it.
        ///
        /// Two reasons for the blocks above, and they are not optional. A cell's "has something
        /// to stand on" reads the solidity of the cell below it, so removing rock at y changes
        /// walkability at y+1; and a fall edge looks <see cref="MaxFallLayers"/> layers down for
        /// its landing, so changing what is at y invalidates fall edges up to y+MaxFallLayers.
        /// </summary>
        public void MarkDirty(int cellIndex)
        {
            CellRef c = Size.FromIndex(cellIndex);
            MarkDirty(c.X, c.Z, c.Y);
        }

        public void MarkDirty(int x, int z, int y)
        {
            MarkBlockDirty(BlockIndexOfCell(x, z, y));
            for (int k = 1; k <= MaxFallLayers; k++)
                if (y + k < Size.SizeY) MarkBlockDirty(BlockIndexOfCell(x, z, y + k));
        }

        void MarkBlockDirty(int block)
        {
            _dirty[block] = true;
            _anyDirty = true;
        }

        public bool HasDirtyWork => _anyDirty;

        // =====================================================================================
        // Rebuild
        // =====================================================================================

        /// <summary>
        /// The nav rebuild, run once per tick at a fixed point in the tick order. Returns true if
        /// anything changed. No agent ever observes a half-rebuilt graph.
        /// </summary>
        public bool Rebuild()
        {
            if (!_anyDirty) return false;

            _dirtyList.Clear();
            for (int b = 0; b < BlockCount; b++) if (_dirty[b]) _dirtyList.Add(b);
            if (_dirtyList.Count == 0) { _anyDirty = false; return false; }

            CollectAffectedZones();

            // Free first, everything, then allocate. Because the free lists hand out the lowest
            // id available, a dirty block normally reclaims exactly the ids it released, which is
            // what keeps ids stable across an incremental rebuild.
            for (int i = 0; i < _affectedZones.Count; i++) FreeZoneLinks(_affectedZones[i]);
            for (int i = 0; i < _dirtyList.Count; i++) FreeBlockRegions(_dirtyList[i]);

            _freeRegions.Sort();
            _freeRegionCursor = 0;
            _freeLinks.Sort();
            _freeLinkCursor = 0;

            for (int i = 0; i < _dirtyList.Count; i++) FloodBlock(_dirtyList[i]);
            for (int i = 0; i < _affectedZones.Count; i++) BuildZoneLinks(_affectedZones[i]);

            if (_freeRegionCursor > 0) _freeRegions.RemoveRange(0, _freeRegionCursor);
            if (_freeLinkCursor > 0) _freeLinks.RemoveRange(0, _freeLinkCursor);
            _freeRegionCursor = 0;
            _freeLinkCursor = 0;

            RebuildPortalEdges();
            RebuildAdjacency();
            RecomputeDistricts();
            RecomputeLayerChangeEstimate();

            for (int i = 0; i < _dirtyList.Count; i++) _dirty[_dirtyList[i]] = false;
            _anyDirty = false;
            GraphVersion++;
            return true;
        }

        void CollectAffectedZones()
        {
            _affectedZoneSet.Clear();
            _affectedZones.Clear();

            int perLayer = BlocksX * BlocksZ;
            for (int i = 0; i < _dirtyList.Count; i++)
            {
                int b = _dirtyList[i];
                int y = b / perLayer;
                int rem = b - y * perLayer;
                int bz = rem / BlocksX;
                int bx = rem - bz * BlocksX;

                // The block's own interior, and the interiors of its four horizontal neighbours:
                // a fall edge is owned by the block its source cell sits in, and the hole it
                // falls through can be one block over.
                _affectedZoneSet.Add(b);
                if (bx > 0) _affectedZoneSet.Add(b - 1);
                if (bx + 1 < BlocksX) _affectedZoneSet.Add(b + 1);
                if (bz > 0) _affectedZoneSet.Add(b - BlocksX);
                if (bz + 1 < BlocksZ) _affectedZoneSet.Add(b + BlocksX);

                // The four shared boundaries.
                if (bx + 1 < BlocksX) _affectedZoneSet.Add(BlockCount + b);
                if (bz + 1 < BlocksZ) _affectedZoneSet.Add(2 * BlockCount + b);
                if (bx > 0) _affectedZoneSet.Add(BlockCount + b - 1);
                if (bz > 0) _affectedZoneSet.Add(2 * BlockCount + b - BlocksX);

                if (_connectorsByBlock.TryGetValue(b, out List<int>? ids))
                    for (int k = 0; k < ids.Count; k++) _affectedZoneSet.Add(3 * BlockCount + ids[k]);
            }

            foreach (int z in _affectedZoneSet) _affectedZones.Add(z);
            _affectedZones.Sort();
        }

        // ---- regions ----------------------------------------------------------------------

        void FreeBlockRegions(int block)
        {
            for (int r = _blockRegionHead[block]; r != -1;)
            {
                int next = _regionNextInBlock[r];
                _regionAlive[r] = false;
                _regionKind[r] = RegionKind.None;
                _regionVersion[r]++;
                _freeRegions.Add(r);
                r = next;
            }

            _blockRegionHead[block] = -1;
            _blockRegionTail[block] = -1;
        }

        int AllocRegion(RegionKind kind, int block, int minCell)
        {
            int id;
            if (_freeRegionCursor < _freeRegions.Count) id = _freeRegions[_freeRegionCursor++];
            else
            {
                id = _regionCount++;
                EnsureRegionCapacity(_regionCount);
            }

            _regionAlive[id] = true;
            _regionKind[id] = kind;
            _regionBlock[id] = block;
            _regionMinCell[id] = minCell;
            _regionCells[id] = 0;
            _regionVersion[id]++;
            _regionNextInBlock[id] = -1;

            int tail = _blockRegionTail[block];
            if (tail == -1) _blockRegionHead[block] = id; else _regionNextInBlock[tail] = id;
            _blockRegionTail[block] = id;
            return id;
        }

        void EnsureRegionCapacity(int needed)
        {
            if (needed <= _regionAlive.Length) return;
            int n = _regionAlive.Length;
            while (n < needed) n *= 2;
            Array.Resize(ref _regionAlive, n);
            Array.Resize(ref _regionKind, n);
            Array.Resize(ref _regionBlock, n);
            Array.Resize(ref _regionMinCell, n);
            Array.Resize(ref _regionCells, n);
            Array.Resize(ref _regionVersion, n);
            Array.Resize(ref _regionNextInBlock, n);
            Array.Resize(ref _adjCount, n);
            Array.Resize(ref _adjStart, n + 1);
            for (int m = 0; m < TraverseModes.Count; m++) Array.Resize(ref _district[m], n);
        }

        void FloodBlock(int block)
        {
            BlockBounds(block, out int x0, out int x1, out int z0, out int z1, out int y);
            int strideZ = Size.SizeX;

            // Flags first, for the whole block: the region kind of a cell is read off the flag
            // word, so every cell in the block has to be current before any of them is flooded.
            for (int z = z0; z < z1; z++)
            {
                int rowBase = Size.Index(x0, z, y);
                for (int x = 0; x < x1 - x0; x++)
                {
                    int idx = rowBase + x;
                    Grid.RefreshFrom(_cells, idx);
                    _cellRegion[idx] = NoRegion;
                }
            }

            for (int z = z0; z < z1; z++)
            {
                int rowBase = Size.Index(x0, z, y);
                for (int x = x0; x < x1; x++)
                {
                    int idx = rowBase + (x - x0);
                    if (_cellRegion[idx] != NoRegion) continue;

                    RegionKind kind = Grid.KindOf(idx);
                    if (kind == RegionKind.None) continue;

                    // The scan is ascending, so the first cell of a region is its lowest member.
                    int r = AllocRegion(kind, block, idx);
                    _cellRegion[idx] = r;

                    if (kind == RegionKind.Door)
                    {
                        // One region per door cell, always. A door is the thing most likely to
                        // change state, so it must never be able to drag a neighbour with it.
                        _regionCells[r] = 1;
                        continue;
                    }

                    int top = 0;
                    _floodStack[top++] = idx;
                    int count = 1;
                    while (top > 0)
                    {
                        int c = _floodStack[--top];
                        // Recover coordinates: c minus the layer base is z * strideZ + x.
                        int inLayer = c - y * Size.LayerStride;
                        int cx = inLayer % strideZ;
                        int cz = inLayer / strideZ;

                        if (cx > x0) TryFlood(c - 1, kind, r, ref top, ref count);
                        if (cx + 1 < x1) TryFlood(c + 1, kind, r, ref top, ref count);
                        if (cz > z0) TryFlood(c - strideZ, kind, r, ref top, ref count);
                        if (cz + 1 < z1) TryFlood(c + strideZ, kind, r, ref top, ref count);
                    }

                    _regionCells[r] = count;
                }
            }
        }

        void TryFlood(int n, RegionKind kind, int region, ref int top, ref int count)
        {
            if (_cellRegion[n] != NoRegion) return;
            if (Grid.KindOf(n) != kind) return;
            if (kind == RegionKind.Door) return;
            _cellRegion[n] = region;
            count++;
            _floodStack[top++] = n;
        }

        // ---- links ------------------------------------------------------------------------

        void EnsureZoneCapacity(int needed)
        {
            if (needed <= _zoneLinkHead.Length) return;
            int old = _zoneLinkHead.Length;
            int n = old;
            while (n < needed) n *= 2;
            Array.Resize(ref _zoneLinkHead, n);
            for (int i = old; i < n; i++) _zoneLinkHead[i] = -1;
        }

        void FreeZoneLinks(int zone)
        {
            EnsureZoneCapacity(zone + 1);
            for (int l = _zoneLinkHead[zone]; l != -1;)
            {
                int next = _linkNextInZone[l];
                _linkAlive[l] = false;
                _freeLinks.Add(l);
                l = next;
            }

            _zoneLinkHead[zone] = -1;
        }

        int AllocLink(int zone)
        {
            int id;
            if (_freeLinkCursor < _freeLinks.Count) id = _freeLinks[_freeLinkCursor++];
            else
            {
                id = _linkCount++;
                EnsureLinkCapacity(_linkCount);
            }

            _linkAlive[id] = true;
            _linkZone[id] = zone;
            _linkNextInZone[id] = _zoneLinkHead[zone];
            _zoneLinkHead[zone] = id;
            return id;
        }

        void EnsureLinkCapacity(int needed)
        {
            if (needed <= _linkAlive.Length) return;
            int n = _linkAlive.Length;
            while (n < needed) n *= 2;
            Array.Resize(ref _linkAlive, n);
            Array.Resize(ref _linkA, n);
            Array.Resize(ref _linkB, n);
            Array.Resize(ref _linkKind, n);
            Array.Resize(ref _linkCostAB, n);
            Array.Resize(ref _linkCostBA, n);
            Array.Resize(ref _linkMode, n);
            Array.Resize(ref _linkOneWay, n);
            Array.Resize(ref _linkCellA, n);
            Array.Resize(ref _linkCellB, n);
            Array.Resize(ref _linkSpan, n);
            Array.Resize(ref _linkZone, n);
            Array.Resize(ref _linkNextInZone, n);
        }

        void BuildZoneLinks(int zone)
        {
            _pairScratch.Clear();
            _fallScratch.Clear();
            if (zone < BlockCount) BuildInteriorZone(zone);
            else if (zone < 2 * BlockCount) BuildEdgeZone(zone - BlockCount, 1);
            else if (zone < 3 * BlockCount) BuildEdgeZone(zone - 2 * BlockCount, Size.SizeX);
            else BuildConnectorZone(zone - 3 * BlockCount);
        }

        void BuildInteriorZone(int block)
        {
            BlockBounds(block, out int x0, out int x1, out int z0, out int z1, out int y);
            int strideZ = Size.SizeX;

            for (int z = z0; z < z1; z++)
            {
                int rowBase = Size.Index(x0, z, y);
                for (int x = x0; x < x1; x++)
                {
                    int c = rowBase + (x - x0);
                    if (_cellRegion[c] == NoRegion) continue;

                    if (x + 1 < x1) TryPair(block, c, c + 1);
                    if (z + 1 < z1) TryPair(block, c, c + strideZ);

                    if (_regionKind[_cellRegion[c]] != RegionKind.Impassable)
                        TryFallEdges(block, c, x, z, y);
                }
            }
        }

        void BuildEdgeZone(int block, int delta)
        {
            BlockBounds(block, out int x0, out int x1, out int z0, out int z1, out int y);
            if (delta == 1)
            {
                if (x1 >= Size.SizeX || x1 != x0 + BlockSize) return;
                for (int z = z0; z < z1; z++)
                {
                    int c = Size.Index(x1 - 1, z, y);
                    TryPair(BlockCount + block, c, c + 1);
                }
            }
            else
            {
                if (z1 >= Size.SizeZ || z1 != z0 + BlockSize) return;
                for (int x = x0; x < x1; x++)
                {
                    int c = Size.Index(x, z1 - 1, y);
                    TryPair(2 * BlockCount + block, c, c + delta);
                }
            }
        }

        void TryPair(int zone, int c, int n)
        {
            int ra = _cellRegion[c];
            int rb = _cellRegion[n];
            if (ra == NoRegion || rb == NoRegion || ra == rb) return;
            if (_regionKind[ra] == RegionKind.Impassable || _regionKind[rb] == RegionKind.Impassable) return;

            int a = ra < rb ? ra : rb;
            int b = ra < rb ? rb : ra;
            int cellA = ra < rb ? c : n;
            int cellB = ra < rb ? n : c;

            long key = ((long)a << 32) | (uint)b;
            if (_pairScratch.TryGetValue(key, out int existing)) { _linkSpan[existing]++; return; }

            int id = AllocLink(zone);
            _linkA[id] = a;
            _linkB[id] = b;
            _linkKind[id] = LinkKind.Span;
            _linkCellA[id] = cellA;
            _linkCellB[id] = cellB;
            _linkCostAB[id] = StepCost(cellB);
            _linkCostBA[id] = StepCost(cellA);
            _linkOneWay[id] = false;
            _linkSpan[id] = 1;
            _linkMode[id] = ModeMaskFor(cellA, cellB);
            _pairScratch[key] = id;
        }

        int StepCost(int targetCell)
        {
            NavFlags f = Grid.Flags[targetCell];
            int cost = MoveCost.Orthogonal + Grid.ExtraCost(targetCell);
            if ((f & NavFlags.Door) != 0 && (f & NavFlags.DoorOpen) == 0) cost += MoveCost.DoorOpening;
            if ((f & NavFlags.Hazard) != 0) cost += MoveCost.HazardPenalty;
            return cost;
        }

        byte ModeMaskFor(int cellA, int cellB)
        {
            byte mask = 0;
            for (int m = 0; m < TraverseModes.Count; m++)
            {
                var mode = (TraverseMode)m;
                if (Grid.CanEnter(cellA, mode) && Grid.CanEnter(cellB, mode)) mask |= (byte)(1 << m);
            }

            return mask;
        }

        /// <summary>
        /// A hole in a floor is an edge, not a physics surprise. Stepping sideways into a cell
        /// with nothing to stand on drops the agent to the first cell below that has a floor.
        ///
        /// The edge is one-way and priced at <see cref="MoveCost.Fall"/>, and — this is the part
        /// that matters — it is excluded from the district flood. Districts are the undirected
        /// components of the graph, and an edge you cannot come back along must never merge two
        /// of them, or <see cref="Reachable"/> would start promising round trips that do not
        /// exist. The result is conservative in the safe direction.
        /// </summary>
        void TryFallEdges(int zone, int c, int x, int z, int y)
        {
            if (y == 0) return;
            if ((Grid.Flags[c] & NavFlags.Walkable) == 0) return;

            int strideZ = Size.SizeX;
            if (x > 0) TryFallEdge(zone, c, c - 1, y);
            if (x + 1 < Size.SizeX) TryFallEdge(zone, c, c + 1, y);
            if (z > 0) TryFallEdge(zone, c, c - strideZ, y);
            if (z + 1 < Size.SizeZ) TryFallEdge(zone, c, c + strideZ, y);
        }

        void TryFallEdge(int zone, int from, int hole, int y)
        {
            if (!Grid.IsAir(hole)) return;

            int landing = -1;
            int step = hole;
            for (int k = 1; k <= MaxFallLayers && y - k >= 0; k++)
            {
                step -= Size.LayerStride;
                if ((Grid.Flags[step] & NavFlags.Walkable) != 0) { landing = step; break; }
                if (!Grid.IsAir(step)) break;
            }

            if (landing < 0) return;

            int ra = _cellRegion[from];
            int rb = _cellRegion[landing];
            if (ra == NoRegion || rb == NoRegion || ra == rb) return;

            long key = ((long)ra << 32) | (uint)rb;
            if (_fallScratch.ContainsKey(key)) return;

            int id = AllocLink(zone);
            _linkA[id] = ra;
            _linkB[id] = rb;
            _linkKind[id] = LinkKind.Fall;
            _linkCellA[id] = from;
            _linkCellB[id] = landing;
            _linkCostAB[id] = MoveCost.Fall;
            _linkCostBA[id] = MoveCost.Fall * 1000; // never traversed; one-way is checked first
            _linkOneWay[id] = true;
            _linkSpan[id] = 1;
            _linkMode[id] = 0; // no standard mode falls on purpose
            _fallScratch[key] = id;
        }

        void BuildConnectorZone(int connectorId)
        {
            if (connectorId >= _connectors.Count) return;
            Connector? con = _connectors[connectorId];
            if (con == null) return;

            NavFlags footprint = con.FootprintFlag;
            for (int i = 0; i < con.LowerCells.Length; i++)
                if ((Grid.Flags[con.LowerCells[i]] & footprint) == 0) return;
            for (int i = 0; i < con.UpperCells.Length; i++)
                if ((Grid.Flags[con.UpperCells[i]] & footprint) == 0) return;

            int zone = 3 * BlockCount + connectorId;
            int pairs = Math.Max(con.LowerCells.Length, con.UpperCells.Length);
            for (int i = 0; i < pairs; i++)
            {
                int lower = con.LowerCells[Math.Min(i, con.LowerCells.Length - 1)];
                int upper = con.UpperCells[Math.Min(i, con.UpperCells.Length - 1)];
                int ra = _cellRegion[lower];
                int rb = _cellRegion[upper];
                if (ra == NoRegion || rb == NoRegion) continue;

                long key = ((long)ra << 32) | (uint)rb;
                if (_pairScratch.ContainsKey(key)) continue;

                int id = AllocLink(zone);
                _linkA[id] = ra;             // A is always the lower end of a portal
                _linkB[id] = rb;
                _linkKind[id] = LinkKind.Portal;
                _linkCellA[id] = lower;
                _linkCellB[id] = upper;
                _linkCostAB[id] = con.CostUp;
                _linkCostBA[id] = con.CostDown;
                _linkOneWay[id] = false;
                _linkSpan[id] = 1;
                _linkMode[id] = (byte)(con.ModeMask & ModeMaskFor(lower, upper));
                _pairScratch[key] = id;
            }
        }

        // ---- derived tables -----------------------------------------------------------------

        void RebuildPortalEdges()
        {
            _cellPortalHead.Clear();
            _portalEdgeCount = 0;

            int needed = 0;
            for (int l = 0; l < _linkCount; l++)
                if (_linkAlive[l] && _linkKind[l] == LinkKind.Portal) needed += 2;
            if (needed > _peTarget.Length)
            {
                int n = Math.Max(1, _peTarget.Length);
                while (n < needed) n *= 2;
                Array.Resize(ref _peTarget, n);
                Array.Resize(ref _peCost, n);
                Array.Resize(ref _peMode, n);
                Array.Resize(ref _peNext, n);
                Array.Resize(ref _peLink, n);
                Array.Resize(ref _peFrom, n);
                Array.Resize(ref _peKey, n);
                Array.Resize(ref _peOrder, n);
            }

            for (int l = 0; l < _linkCount; l++)
            {
                if (!_linkAlive[l] || _linkKind[l] != LinkKind.Portal) continue;
                AddPortalEdge(_linkCellA[l], _linkCellB[l], _linkCostAB[l], _linkMode[l], l);
                AddPortalEdge(_linkCellB[l], _linkCellA[l], _linkCostBA[l], _linkMode[l], l);
            }

            // Chain each cell's portal edges in ascending target cell, which is a property of the
            // world rather than of the order connectors were registered in. Successor order feeds
            // straight into which of several equal-cost predecessors a search records, so it has
            // to be canonical or a graph rebuilt from a save would produce a different path from
            // one maintained incrementally.
            int count = _portalEdgeCount;
            for (int i = 0; i < count; i++)
            {
                _peKey[i] = ((long)_peFrom[i] << 32) | (uint)_peTarget[i];
                _peOrder[i] = i;
            }

            Array.Sort(_peKey, _peOrder, 0, count);
            for (int i = count - 1; i >= 0; i--)
            {
                int e = _peOrder[i];
                int from = _peFrom[e];
                _peNext[e] = _cellPortalHead.TryGetValue(from, out int head) ? head : -1;
                _cellPortalHead[from] = e;
            }
        }

        void AddPortalEdge(int fromCell, int toCell, int cost, byte mode, int link)
        {
            int e = _portalEdgeCount++;
            _peFrom[e] = fromCell;
            _peTarget[e] = toCell;
            _peCost[e] = cost;
            _peMode[e] = mode;
            _peLink[e] = link;
            _peNext[e] = -1;
        }

        void RebuildAdjacency()
        {
            EnsureRegionCapacity(Math.Max(1, _regionCount));
            for (int r = 0; r < _regionCount; r++) _adjCount[r] = 0;

            for (int l = 0; l < _linkCount; l++)
            {
                if (!_linkAlive[l]) continue;
                _adjCount[_linkA[l]]++;
                _adjCount[_linkB[l]]++;
            }

            int total = 0;
            for (int r = 0; r < _regionCount; r++)
            {
                _adjStart[r] = total;
                total += _adjCount[r];
                _adjCount[r] = 0;
            }

            _adjStart[_regionCount] = total;
            _adjTotal = total;
            if (total > _adjLinks.Length)
            {
                int n = _adjLinks.Length;
                while (n < total) n *= 2;
                _adjLinks = new int[n];
            }

            for (int l = 0; l < _linkCount; l++)
            {
                if (!_linkAlive[l]) continue;
                int a = _linkA[l];
                int b = _linkB[l];
                _adjLinks[_adjStart[a] + _adjCount[a]++] = l;
                _adjLinks[_adjStart[b] + _adjCount[b]++] = l;
            }
        }

        void RecomputeDistricts()
        {
            if (_bfsQueue.Length < Math.Max(1, _regionCount)) _bfsQueue = new int[Math.Max(16, _regionCount)];

            for (int m = 0; m < TraverseModes.Count; m++)
            {
                int[] d = _district[m];
                for (int r = 0; r < _regionCount; r++) d[r] = -1;

                int next = 0;
                for (int seed = 0; seed < _regionCount; seed++)
                {
                    if (!_regionAlive[seed] || d[seed] != -1) continue;
                    RegionKind kind = _regionKind[seed];
                    if (kind == RegionKind.None || kind == RegionKind.Impassable) continue;

                    int id = next++;
                    d[seed] = id;
                    int head = 0, tail = 0;
                    _bfsQueue[tail++] = seed;
                    while (head < tail)
                    {
                        int r = _bfsQueue[head++];
                        int s = _adjStart[r];
                        int e = s + _adjCount[r];
                        for (int i = s; i < e; i++)
                        {
                            int l = _adjLinks[i];
                            // A one-way edge cannot merge two components: you could get there
                            // and not get back, which is not what "reachable" means.
                            if (_linkOneWay[l]) continue;
                            if ((_linkMode[l] & (1 << m)) == 0) continue;
                            int other = _linkA[l] == r ? _linkB[l] : _linkA[l];
                            if (d[other] != -1) continue;
                            d[other] = id;
                            _bfsQueue[tail++] = other;
                        }
                    }
                }

                _districtCount[m] = next;
            }
        }

        void RecomputeLayerChangeEstimate()
        {
            int portals = 0;
            for (int l = 0; l < _linkCount; l++)
                if (_linkAlive[l] && _linkKind[l] == LinkKind.Portal) portals++;

            int gaps = Math.Max(1, Size.SizeY - 1);
            int perLayer = Math.Max(1, portals / gaps);
            int areaPerPortal = Size.LayerStride / perLayer;

            int spacing = 0;
            while ((spacing + 1) * (spacing + 1) <= areaPerPortal) spacing++;

            EstimatedLayerChangeCost = Math.Max(MoveCost.StairUp, spacing * MoveCost.Orthogonal);
        }

        // =====================================================================================
        // Connectors
        // =====================================================================================

        /// <summary>
        /// Register a connector. Both ends are declared here and are never searched for later.
        /// The cells are flagged, the blocks are dirtied, and the portal link appears at the next
        /// <see cref="Rebuild"/>.
        /// </summary>
        public int AddConnector(ConnectorKind kind, int[] lowerCells, int[] upperCells)
        {
            int id = _connectors.Count;
            var con = new Connector(id, kind, lowerCells, upperCells, Size);
            _connectors.Add(con);
            EnsureZoneCapacity(3 * BlockCount + _connectors.Count);

            NavFlags footprint = con.FootprintFlag;
            for (int i = 0; i < con.LowerCells.Length; i++) FlagConnectorCell(con.LowerCells[i], footprint, id);
            for (int i = 0; i < con.UpperCells.Length; i++) FlagConnectorCell(con.UpperCells[i], footprint, id);
            return id;
        }

        void FlagConnectorCell(int cell, NavFlags footprint, int connectorId)
        {
            Grid.Flags[cell] |= footprint;
            int block = BlockIndexOfCell(cell);
            if (!_connectorsByBlock.TryGetValue(block, out List<int>? ids))
            {
                ids = new List<int>();
                _connectorsByBlock[block] = ids;
            }

            if (!ids.Contains(connectorId)) { ids.Add(connectorId); ids.Sort(); }

            MarkDirty(cell);
        }

        /// <summary>Tear a connector out. Its portal link goes with it at the next rebuild.</summary>
        public bool RemoveConnector(int id)
        {
            if (id < 0 || id >= _connectors.Count) return false;
            Connector? con = _connectors[id];
            if (con == null) return false;

            NavFlags footprint = con.FootprintFlag;
            for (int i = 0; i < con.LowerCells.Length; i++) UnflagConnectorCell(con.LowerCells[i], footprint, id);
            for (int i = 0; i < con.UpperCells.Length; i++) UnflagConnectorCell(con.UpperCells[i], footprint, id);
            _connectors[id] = null;
            return true;
        }

        void UnflagConnectorCell(int cell, NavFlags footprint, int connectorId)
        {
            // The block keeps its record of this connector id on purpose. That record is what
            // makes the connector's zone "affected" at the next rebuild, which is what frees its
            // portal link; drop the record here and the link outlives the stair.
            Grid.Flags[cell] &= ~footprint;
            MarkDirty(cell);
        }

        public Connector? GetConnector(int id) => id >= 0 && id < _connectors.Count ? _connectors[id] : null;

        // =====================================================================================
        // Doors and hazards — the sticky flags
        // =====================================================================================

        public void SetDoor(int cell, bool isDoor, bool open = false)
        {
            NavFlags f = Grid.Flags[cell] & ~(NavFlags.Door | NavFlags.DoorOpen);
            if (isDoor)
            {
                f |= NavFlags.Door;
                if (open) f |= NavFlags.DoorOpen;
            }

            Grid.Flags[cell] = f;
            MarkDirty(cell);
        }

        public void SetDoorOpen(int cell, bool open)
        {
            if ((Grid.Flags[cell] & NavFlags.Door) == 0) return;
            if (open) Grid.Flags[cell] |= NavFlags.DoorOpen;
            else Grid.Flags[cell] &= ~NavFlags.DoorOpen;
            MarkDirty(cell);
        }

        public void SetHazard(int cell, bool hazard)
        {
            if (hazard) Grid.Flags[cell] |= NavFlags.Hazard;
            else Grid.Flags[cell] &= ~NavFlags.Hazard;
            MarkDirty(cell);
        }

        // =====================================================================================
        // The API the rest of the simulation is built on
        // =====================================================================================

        /// <summary>
        /// Can an agent of this mode standing at <paramref name="cellA"/> get to
        /// <paramref name="cellB"/>?
        ///
        /// Two array reads and an integer comparison. This is the single most important call in
        /// the simulation: every job giver scanning hundreds of candidate targets asks it first,
        /// and the searches it rejects are precisely the ones that would have been most expensive.
        /// </summary>
        public bool Reachable(int cellA, int cellB, TraverseMode mode)
        {
            int ra = _cellRegion[cellA];
            if (ra < 0) return false;
            int rb = _cellRegion[cellB];
            if (rb < 0) return false;
            int[] d = _district[(int)mode];
            int da = d[ra];
            return da >= 0 && da == d[rb];
        }

        public bool ReachableRegions(int regionA, int regionB, TraverseMode mode)
        {
            if (regionA < 0 || regionB < 0) return false;
            int[] d = _district[(int)mode];
            int da = d[regionA];
            return da >= 0 && da == d[regionB];
        }

        public int RegionOfCell(int cell) => _cellRegion[cell];
        public int DistrictOfCell(int cell, TraverseMode mode)
        {
            int r = _cellRegion[cell];
            return r < 0 ? -1 : _district[(int)mode][r];
        }

        public int DistrictOfRegion(int region, TraverseMode mode) => _district[(int)mode][region];

        public int RegionCapacity => _regionCount;
        public bool IsRegionAlive(int region) => region >= 0 && region < _regionCount && _regionAlive[region];
        public RegionKind KindOfRegion(int region) => _regionKind[region];
        public int RegionMinCell(int region) => _regionMinCell[region];
        public int RegionCellCount(int region) => _regionCells[region];

        /// <summary>
        /// What the abstract search charges for crossing a region, standing in for the HPA-style
        /// cached intra-region crossing distance that the build order deliberately defers.
        ///
        /// It has to scale with the region, not be a constant. A ruined city fragments into
        /// regions of two or three cells, and charging a flat three-cell transit for each of them
        /// inflates g far faster than a straight-line heuristic grows, which quietly turns the
        /// abstract A-star into a Dijkstra. The square root of the cell count is the width of a
        /// square region and is within a factor of two of the truth for anything convex.
        /// </summary>
        public int RegionTransitCost(int region) => TransitTable[_regionCells[region]];

        static readonly int[] TransitTable = BuildTransitTable();

        static int[] BuildTransitTable()
        {
            var table = new int[BlockSize * BlockSize + 1];
            for (int s = 0; s < table.Length; s++)
            {
                int root = 0;
                while ((root + 1) * (root + 1) <= s) root++;
                table[s] = Math.Max(1, root) * MoveCost.Orthogonal;
            }

            return table;
        }
        public int RegionVersion(int region) => _regionVersion[region];
        public int RegionBlock(int region) => _regionBlock[region];

        public int LiveRegionCount()
        {
            int n = 0;
            for (int r = 0; r < _regionCount; r++) if (_regionAlive[r]) n++;
            return n;
        }

        public int LiveLinkCount()
        {
            int n = 0;
            for (int l = 0; l < _linkCount; l++) if (_linkAlive[l]) n++;
            return n;
        }

        public int DistrictCount(TraverseMode mode) => _districtCount[(int)mode];

        public int AdjacencyStart(int region) => _adjStart[region];
        public int AdjacencyCount(int region) => _adjCount[region];
        public int AdjacencyLink(int slot) => _adjLinks[slot];

        public int LinkA(int link) => _linkA[link];
        public int LinkB(int link) => _linkB[link];
        public LinkKind KindOfLink(int link) => _linkKind[link];
        public byte LinkModeMask(int link) => _linkMode[link];
        public bool LinkIsOneWay(int link) => _linkOneWay[link];
        public int LinkCellA(int link) => _linkCellA[link];
        public int LinkCellB(int link) => _linkCellB[link];
        public int LinkSpan(int link) => _linkSpan[link];

        public int LinkOther(int link, int fromRegion) => _linkA[link] == fromRegion ? _linkB[link] : _linkA[link];

        public int LinkCostFrom(int link, int fromRegion) =>
            _linkA[link] == fromRegion ? _linkCostAB[link] : _linkCostBA[link];

        public int FirstPortalEdge(int cell) => _cellPortalHead.TryGetValue(cell, out int e) ? e : -1;
        public int PortalEdgeNext(int edge) => _peNext[edge];
        public int PortalEdgeTarget(int edge) => _peTarget[edge];
        public int PortalEdgeCost(int edge) => _peCost[edge];
        public byte PortalEdgeMode(int edge) => _peMode[edge];
        public int PortalEdgeLink(int edge) => _peLink[edge];

        /// <summary>
        /// Is <paramref name="to"/> reachable from <paramref name="from"/> in exactly one move?
        ///
        /// This is the rule a path is validated against, and it has no tolerance in it. A step
        /// that changes layer without a registered portal — or without a fall edge, when falling
        /// is allowed — is a bug and is reported as one. CDDA wrote "jumps are acceptable on 1
        /// z-level changes" into its route validator and spent a decade unable to tell a broken
        /// path from a working one.
        /// </summary>
        public bool IsLegalStep(int from, int to, TraverseMode mode, bool allowFalls = false)
        {
            if (from == to) return false;
            CellRef a = Size.FromIndex(from);
            CellRef b = Size.FromIndex(to);

            if (a.Y == b.Y)
            {
                int dx = Math.Abs(a.X - b.X);
                int dz = Math.Abs(a.Z - b.Z);
                if (dx + dz != 1) return false;
                return Grid.CanEnter(from, mode) && Grid.CanEnter(to, mode);
            }

            for (int e = FirstPortalEdge(from); e != -1; e = _peNext[e])
                if (_peTarget[e] == to && TraverseModes.Allows(_peMode[e], mode)) return true;

            if (!allowFalls) return false;
            return IsFallStep(from, to, mode);
        }

        bool IsFallStep(int from, int to, TraverseMode mode)
        {
            CellRef a = Size.FromIndex(from);
            CellRef b = Size.FromIndex(to);
            if (b.Y >= a.Y) return false;
            if (a.Y - b.Y > MaxFallLayers) return false;
            int dx = Math.Abs(a.X - b.X);
            int dz = Math.Abs(a.Z - b.Z);
            if (dx + dz != 1) return false;
            if (!Grid.CanEnter(from, mode) || !Grid.CanEnter(to, mode)) return false;

            int hole = Size.Index(b.X, b.Z, a.Y);
            if (!Grid.IsAir(hole)) return false;
            int step = hole;
            for (int k = 1; k <= a.Y - b.Y; k++)
            {
                step -= Size.LayerStride;
                if (step == to) return (Grid.Flags[step] & NavFlags.Walkable) != 0;
                if (!Grid.IsAir(step)) return false;
            }

            return false;
        }

        /// <summary>
        /// Fold the graph into the state hash. Ids and district numbers are derived, but they are
        /// derived <em>deterministically</em>, so hashing them turns a divergence in the nav
        /// rebuild into a gate failure at the tick it happens.
        /// </summary>
        public void ContributeTo(ref StateHash hash)
        {
            hash.Add(_regionCount);
            for (int r = 0; r < _regionCount; r++)
            {
                if (!_regionAlive[r]) { hash.Add(-1); continue; }
                hash.Add((int)_regionKind[r]);
                hash.Add(_regionMinCell[r]);
                hash.Add(_regionCells[r]);
                for (int m = 0; m < TraverseModes.Count; m++) hash.Add(_district[m][r]);
            }

            for (int l = 0; l < _linkCount; l++)
            {
                if (!_linkAlive[l]) { hash.Add(-1); continue; }
                hash.Add(_linkA[l]);
                hash.Add(_linkB[l]);
                hash.Add((int)_linkKind[l]);
                hash.Add(_linkCostAB[l]);
                hash.Add(_linkCostBA[l]);
                hash.Add(_linkMode[l]);
            }
        }

        /// <summary>
        /// A canonical, id-independent fingerprint of the graph's <em>structure</em>.
        ///
        /// Region and link ids come from free lists, so an incremental rebuild and a rebuild from
        /// scratch may number things differently while describing exactly the same world. This
        /// folds in only what is id-independent — each cell's region identified by that region's
        /// lowest member cell, and each district identified by the lowest member cell in it — so
        /// it is the right oracle for "did the incremental rebuild get the same answer?".
        /// </summary>
        public ulong StructureFingerprint()
        {
            var hash = StateHash.New();
            var districtRep = new int[TraverseModes.Count][];
            for (int m = 0; m < TraverseModes.Count; m++)
            {
                districtRep[m] = new int[Math.Max(1, _districtCount[m])];
                for (int i = 0; i < districtRep[m].Length; i++) districtRep[m][i] = int.MaxValue;
                for (int r = 0; r < _regionCount; r++)
                {
                    if (!_regionAlive[r]) continue;
                    int d = _district[m][r];
                    if (d < 0) continue;
                    if (_regionMinCell[r] < districtRep[m][d]) districtRep[m][d] = _regionMinCell[r];
                }
            }

            for (int c = 0; c < _cellRegion.Length; c++)
            {
                int r = _cellRegion[c];
                if (r < 0) { hash.Add(-1); continue; }
                hash.Add((int)_regionKind[r]);
                hash.Add(_regionMinCell[r]);
                for (int m = 0; m < TraverseModes.Count; m++)
                {
                    int d = _district[m][r];
                    hash.Add(d < 0 ? -1 : districtRep[m][d]);
                }
            }

            return hash.Value;
        }
    }
}
