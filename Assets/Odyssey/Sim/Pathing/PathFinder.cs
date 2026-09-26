#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pathing
{
    public enum PathStatus : byte
    {
        Success = 0,

        /// <summary>The district comparison said no. No search was run.</summary>
        Unreachable = 1,

        /// <summary>A path may exist; the node budget ran out before it was found.</summary>
        BudgetExhausted = 2,

        /// <summary>The start or the goal is not a cell this mode can occupy.</summary>
        InvalidRequest = 3,
    }

    public readonly struct PathOptions
    {
        /// <summary>Cell expansions allowed. Nodes, never milliseconds: a wall-clock budget makes
        /// the simulation depend on the machine, which is the same thing as non-determinism.</summary>
        public readonly int MaxCellNodes;

        /// <summary>Region expansions allowed in the abstract stage.</summary>
        public readonly int MaxRegionNodes;

        /// <summary>Permit one-way fall edges. Flee and collapse behaviours only.</summary>
        public readonly bool AllowFalls;

        /// <summary>Diagnostic: run the search even when districts say it is pointless.</summary>
        public readonly bool SkipReachabilityCheck;

        /// <summary>Diagnostic: skip the abstract stage and search cells unconstrained.</summary>
        public readonly bool SkipAbstractStage;

        /// <summary>Optional occupancy predicate for soft crowd avoidance.</summary>
        public readonly Func<int, bool>? Occupancy;

        public PathOptions(int maxCellNodes, int maxRegionNodes, bool allowFalls = false,
            bool skipReachabilityCheck = false, bool skipAbstractStage = false,
            Func<int, bool>? occupancy = null)
        {
            MaxCellNodes = maxCellNodes;
            MaxRegionNodes = maxRegionNodes;
            AllowFalls = allowFalls;
            SkipReachabilityCheck = skipReachabilityCheck;
            SkipAbstractStage = skipAbstractStage;
            Occupancy = occupancy;
        }

        /// <summary>The budget the D1 benchmark used, so results stay comparable to its baseline.</summary>
        public static PathOptions Default => new PathOptions(20_000, 20_000);

        public PathOptions With(bool? allowFalls = null, bool? skipReachability = null, bool? skipAbstract = null,
            Func<int, bool>? occupancy = null) =>
            new PathOptions(MaxCellNodes, MaxRegionNodes,
                allowFalls ?? AllowFalls,
                skipReachability ?? SkipReachabilityCheck,
                skipAbstract ?? SkipAbstractStage,
                occupancy ?? Occupancy);
    }

    public readonly struct PathResult
    {
        public readonly PathStatus Status;

        /// <summary>Cells in the path, start and goal included. Zero unless <see cref="Status"/> is Success.</summary>
        public readonly int Length;

        public readonly int Cost;
        public readonly int CellNodes;
        public readonly int RegionNodes;

        /// <summary>FNV of the cell sequence. Folded into the state hash so a divergent path is
        /// caught at the tick it happens rather than ten thousand ticks later.</summary>
        public readonly ulong Checksum;

        public PathResult(PathStatus status, int length, int cost, int cellNodes, int regionNodes, ulong checksum)
        {
            Status = status;
            Length = length;
            Cost = cost;
            CellNodes = cellNodes;
            RegionNodes = regionNodes;
            Checksum = checksum;
        }

        public bool Ok => Status == PathStatus.Success;
    }

    /// <summary>
    /// The two-stage search: an abstract A-star over regions that supplies a corridor and a
    /// heuristic, then a cell A-star confined to that corridor.
    ///
    /// <para><b>Why the abstract stage exists in 3D specifically.</b> A straight distance
    /// heuristic is catastrophically misleading when the goal is one layer below you and the
    /// nearest stair is sixty cells the other way, and a ruined multi-storey city produces that
    /// case constantly. The region search finds the stair before the cell search wastes a single
    /// expansion on the floor.</para>
    ///
    /// <para><b>Determinism.</b> Integer costs only. The open list is ordered on the total order
    /// (f, then h, then cell index), so no tie is ever left to heap internals. The neighbour
    /// order is a compile-time constant — minus x, plus x, minus z, plus z, then portal edges in
    /// ascending target cell, then fall edges. Budgets count node expansions, never milliseconds.
    /// Every tie-break is on a position, never on a region or link id, because ids come from free
    /// lists and so depend on the history of edits; positions do not. That is what makes a path
    /// found on an incrementally maintained graph identical to one found on a graph rebuilt from
    /// a save, which is a property the tests assert directly.</para>
    ///
    /// <para><b>Four-connected, deliberately.</b> The committed architecture allows eight
    /// horizontal moves. This implementation moves orthogonally only, because a diagonal step is
    /// a graph edge like any other and would have to appear identically in three places — the
    /// region flood, the boundary link generation (including the four-block corners that a
    /// diagonal crosses) and the cell search — or the districts and the paths would disagree
    /// about what is connected, which is the one failure the whole unit exists to prevent.
    /// Adding diagonals is a contained change to those three places, and the benchmark below is
    /// four-connected on both arms, so nothing in the measurement depends on it.</para>
    /// </summary>
    public sealed class PathFinder
    {
        /// <summary>
        /// What the heuristic assumes a layer change costs.
        ///
        /// This is the single most sensitive number in the search, and it is not the cost of
        /// climbing a stair. It is the cost of *getting to* a stair, which in a 250 x 250 world
        /// with a few hundred connectors is dominated by the horizontal walk to the nearest one.
        /// Set it to the climb cost and the heuristic under-estimates cross-layer requests so
        /// badly that the abstract search degenerates into a Dijkstra; set it far too high and
        /// paths get visibly silly. It is Def data for exactly that reason — it is a property of
        /// how densely a map is stitched together vertically, which only the graph knows.
        ///
        /// Left alone it tracks <see cref="NavGraph.EstimatedLayerChangeCost"/>, which is derived
        /// from connector density at every rebuild. Set it to override, for tuning or a test.
        /// </summary>
        public int LayerChangeHint
        {
            get => _layerChangeHint > 0 ? _layerChangeHint : _graph.EstimatedLayerChangeCost;
            set => _layerChangeHint = value;
        }

        int _layerChangeHint;

        /// <summary>
        /// Optional predicate determining if a cell is occupied by a stationary/working pawn,
        /// adding <see cref="MoveCost.OccupiedBias"/> during cell expansion.
        /// </summary>
        public Func<int, bool>? Occupancy { get; set; }

        readonly NavGraph _graph;
        readonly NavGrid _grid;
        readonly GridSize _size;
        readonly int _layerStride;
        readonly int _sizeX;

        // Per-cell scratch, allocated once and never cleared. A stamp holds +s while the cell is
        // open and -s once it is closed, so a new search costs one increment rather than a
        // 10 MB memset.
        readonly int[] _cellG;
        readonly int[] _cellFrom;
        readonly int[] _cellStamp;
        int _cellStampValue;

        int[] _regionG = new int[64];
        int[] _regionStamp = new int[64];
        int _regionStampValue;

        readonly MinHeap _cellHeap = new MinHeap(1024);
        readonly MinHeap _regionHeap = new MinHeap(256);

        int[] _pathBuffer = new int[256];
        int _pathLength;

        public PathFinder(NavGraph graph)
        {
            _graph = graph;
            _grid = graph.Grid;
            _size = graph.Size;
            _layerStride = _size.LayerStride;
            _sizeX = _size.SizeX;

            int n = _size.CellCount;
            _cellG = new int[n];
            _cellFrom = new int[n];
            _cellStamp = new int[n];
        }

        /// <summary>The cells of the last successful search, start first.</summary>
        public ReadOnlySpan<int> PathCells => new ReadOnlySpan<int>(_pathBuffer, 0, _pathLength);

        public int[] PathToArray()
        {
            var result = new int[_pathLength];
            Array.Copy(_pathBuffer, result, _pathLength);
            return result;
        }

        public PathResult FindPath(int start, int goal, TraverseMode mode) =>
            FindPath(start, goal, mode, PathOptions.Default);

        public PathResult FindPath(int start, int goal, TraverseMode mode, PathOptions options)
        {
            _pathLength = 0;

            if ((uint)start >= (uint)_size.CellCount || (uint)goal >= (uint)_size.CellCount)
                return new PathResult(PathStatus.InvalidRequest, 0, 0, 0, 0, 0);
            if (!_grid.CanEnter(start, mode) || !_grid.CanEnter(goal, mode))
                return new PathResult(PathStatus.InvalidRequest, 0, 0, 0, 0, 0);

            if (start == goal)
            {
                _pathLength = 1;
                _pathBuffer[0] = start;
                return new PathResult(PathStatus.Success, 1, 0, 0, 0, Checksum(1));
            }

            // Stage 1. The whole point of the unit: reject the hopeless request before a single
            // node is expanded. A failed A-star expands the entire connected component, so the
            // searches this rejects are exactly the most expensive searches there are.
            bool falls = options.AllowFalls;
            if (!falls && !options.SkipReachabilityCheck && !_graph.Reachable(start, goal, mode))
                return new PathResult(PathStatus.Unreachable, 0, 0, 0, 0, 0);

            EnsureRegionScratch();

            int regionNodes = 0;
            bool constrained = false;
            if (!falls && !options.SkipAbstractStage)
            {
                int startRegion = _graph.RegionOfCell(start);
                int goalRegion = _graph.RegionOfCell(goal);
                if (startRegion < 0 || goalRegion < 0)
                    return new PathResult(PathStatus.InvalidRequest, 0, 0, 0, 0, 0);

                AbstractOutcome outcome = SearchRegions(startRegion, goalRegion, start, mode,
                    options.MaxRegionNodes, out regionNodes);
                if (outcome == AbstractOutcome.Budget)
                    return new PathResult(PathStatus.BudgetExhausted, 0, 0, 0, regionNodes, 0);
                if (outcome == AbstractOutcome.NoPath)
                    return new PathResult(PathStatus.Unreachable, 0, 0, 0, regionNodes, 0);
                constrained = true;
            }

            Func<int, bool>? occupancy = options.Occupancy ?? Occupancy;
            int rstamp = constrained ? _regionStampValue : 0;
            PathStatus status = SearchCells(start, goal, mode, options, constrained, rstamp, occupancy,
                out int cost, out int cellNodes);

            // Stage 3 fallback. The corridor is an optimisation, never a source of truth: if it
            // did not contain a route and there is budget left, widen to the whole graph once.
            if (status == PathStatus.Unreachable && constrained && cellNodes < options.MaxCellNodes)
            {
                status = SearchCells(start, goal, mode, options, false, rstamp, occupancy, out cost, out int extra);
                cellNodes += extra;
            }

            if (status != PathStatus.Success)
                return new PathResult(status, 0, 0, cellNodes, regionNodes, 0);

            return new PathResult(PathStatus.Success, _pathLength, cost, cellNodes, regionNodes,
                Checksum(_pathLength));
        }

        // =====================================================================================
        // Stage 2 — abstract A-star over regions, backwards from the goal
        // =====================================================================================

        enum AbstractOutcome : byte { Found, NoPath, Budget }

        void EnsureRegionScratch()
        {
            int needed = Math.Max(1, _graph.RegionCapacity);
            if (_regionG.Length >= needed) return;
            int n = _regionG.Length;
            while (n < needed) n *= 2;
            _regionG = new int[n];
            _regionStamp = new int[n];
            _regionStampValue = 0;
        }

        AbstractOutcome SearchRegions(int startRegion, int goalRegion, int startCell,
            TraverseMode mode, int budget, out int expanded)
        {
            expanded = 0;
            int stamp = ++_regionStampValue;
            _regionHeap.Clear();

            _regionG[goalRegion] = 0;
            _regionStamp[goalRegion] = stamp;
            int h0 = RegionHeuristic(goalRegion, startCell);
            // The tie-break is the region's lowest member cell, never its id. Ids come from a
            // free list, so they depend on the history of edits; the lowest member cell is a
            // property of the world. Tying on position is what makes a path found on a graph
            // maintained incrementally identical to one found on a graph rebuilt from scratch —
            // which is to say, what makes a save/load resume walk the same route.
            _regionHeap.Push(h0, h0, _graph.RegionMinCell(goalRegion), goalRegion);

            while (_regionHeap.Count > 0)
            {
                _regionHeap.Pop(out _, out _, out int r);
                if (_regionStamp[r] == -stamp) continue;
                _regionStamp[r] = -stamp;
                expanded++;
                if (r == startRegion) return AbstractOutcome.Found;
                if (expanded >= budget) return AbstractOutcome.Budget;

                int g = _regionG[r];
                int s = _graph.AdjacencyStart(r);
                int e = s + _graph.AdjacencyCount(r);
                for (int i = s; i < e; i++)
                {
                    int link = _graph.AdjacencyLink(i);
                    // One-way edges never appear in the abstract graph: falling is a cell-level
                    // move requested explicitly, not something a corridor hands you by accident.
                    if (_graph.LinkIsOneWay(link)) continue;
                    if (!TraverseModes.Allows(_graph.LinkModeMask(link), mode)) continue;

                    int other = _graph.LinkOther(link, r);
                    // We are walking backwards, so the edge we price is other -> r.
                    int ng = g + _graph.LinkCostFrom(link, other) + _graph.RegionTransitCost(other);
                    int st = _regionStamp[other];
                    if (st == -stamp) continue;
                    if (st == stamp && _regionG[other] <= ng) continue;

                    _regionG[other] = ng;
                    _regionStamp[other] = stamp;
                    int h = RegionHeuristic(other, startCell);
                    _regionHeap.Push(ng + h, h, _graph.RegionMinCell(other), other);
                }
            }

            return AbstractOutcome.NoPath;
        }

        int RegionHeuristic(int region, int towardCell)
        {
            int a = _graph.RegionMinCell(region);
            int ay = a / _layerStride;
            int arem = a - ay * _layerStride;
            int az = arem / _sizeX;
            int ax = arem - az * _sizeX;

            int by = towardCell / _layerStride;
            int brem = towardCell - by * _layerStride;
            int bz = brem / _sizeX;
            int bx = brem - bz * _sizeX;

            int dx = Math.Abs(ax - bx);
            int dz = Math.Abs(az - bz);
            int min = dx < dz ? dx : dz;
            int max = dx < dz ? dz : dx;

            return min * MoveCost.Diagonal + (max - min) * MoveCost.Orthogonal
                   + Math.Abs(ay - by) * LayerChangeHint;
        }

        /// <summary>A region is in the corridor if the abstract search touched it.</summary>
        bool InCorridor(int region, int stamp) =>
            region >= 0 && (_regionStamp[region] == stamp || _regionStamp[region] == -stamp);

        // =====================================================================================
        // Stage 3 — cell A-star, confined to the corridor
        // =====================================================================================

        PathStatus SearchCells(int start, int goal, TraverseMode mode, PathOptions options,
            bool constrained, int rstamp, Func<int, bool>? occupancy, out int cost, out int expanded)
        {
            cost = 0;
            expanded = 0;

            int stamp = ++_cellStampValue;
            _cellHeap.Clear();

            _cellG[start] = 0;
            _cellFrom[start] = -1;
            _cellStamp[start] = stamp;
            int h0 = CellHeuristic(start, goal, rstamp);
            _cellHeap.Push(h0, h0, start, start);

            int budget = options.MaxCellNodes;
            bool falls = options.AllowFalls;

            // The price of a hop, asked once per search rather than once per neighbour. It is
            // NavGraph's to decide — see NavGraph.HopCost — because the region graph and the mover
            // price the same step, and three seams that each name the constants for themselves
            // agree only by coincidence.
            int hopUp = NavGraph.HopCost(up: true);
            int hopDown = NavGraph.HopCost(up: false);
            int jump = NavGraph.JumpCost();

            while (_cellHeap.Count > 0)
            {
                _cellHeap.Pop(out _, out _, out int c);
                // Lazy deletion. A discarded duplicate is not an expansion and does not count
                // against the budget — the same reading the D1 workload settled on.
                if (_cellStamp[c] == -stamp) continue;
                _cellStamp[c] = -stamp;
                expanded++;

                if (c == goal)
                {
                    cost = _cellG[c];
                    Reconstruct(start, goal);
                    return PathStatus.Success;
                }

                if (expanded >= budget) return PathStatus.BudgetExhausted;

                int g = _cellG[c];
                int y = c / _layerStride;
                int rem = c - y * _layerStride;
                int z = rem / _sizeX;
                int x = rem - z * _sizeX;

                // Compile-time neighbour order. Never generated by iterating a hash container.
                if (x > 0) Relax(c, c - 1, g, goal, mode, stamp, rstamp, constrained, occupancy);
                if (x + 1 < _sizeX) Relax(c, c + 1, g, goal, mode, stamp, rstamp, constrained, occupancy);
                if (z > 0) Relax(c, c - _sizeX, g, goal, mode, stamp, rstamp, constrained, occupancy);
                if (z + 1 < _size.SizeZ) Relax(c, c + _sizeX, g, goal, mode, stamp, rstamp, constrained, occupancy);

                if (x > 0 && z > 0) RelaxDiagonal(c, c - 1 - _sizeX, c - 1, c - _sizeX, g, goal, mode, stamp, rstamp, constrained, occupancy);
                if (x + 1 < _sizeX && z > 0) RelaxDiagonal(c, c + 1 - _sizeX, c + 1, c - _sizeX, g, goal, mode, stamp, rstamp, constrained, occupancy);
                if (x > 0 && z + 1 < _size.SizeZ) RelaxDiagonal(c, c - 1 + _sizeX, c - 1, c + _sizeX, g, goal, mode, stamp, rstamp, constrained, occupancy);
                if (x + 1 < _sizeX && z + 1 < _size.SizeZ) RelaxDiagonal(c, c + 1 + _sizeX, c + 1, c + _sizeX, g, goal, mode, stamp, rstamp, constrained, occupancy);

                // A hop: one block up or one block down, into the column next door. Unaided
                // vertical movement is exactly this and nothing else (owner, 2026-09-16) — the
                // cell entered has a floor, so no route can end in mid-air the way a climb could.
                // Priced by NavGraph.HopCost and not by naming the constants here, so that the
                // planner, the region graph and the mover cannot drift apart. Hoisted out of the
                // neighbour tests because this is the hot path, not because the call is dear.
                if (y + 1 < _size.SizeY)
                {
                    int up = c + _layerStride;
                    int gUp = g + hopUp;
                    if (x > 0) RelaxHop(c, up - 1, gUp, goal, mode, stamp, rstamp, constrained, occupancy);
                    if (x + 1 < _sizeX) RelaxHop(c, up + 1, gUp, goal, mode, stamp, rstamp, constrained, occupancy);
                    if (z > 0) RelaxHop(c, up - _sizeX, gUp, goal, mode, stamp, rstamp, constrained, occupancy);
                    if (z + 1 < _size.SizeZ) RelaxHop(c, up + _sizeX, gUp, goal, mode, stamp, rstamp, constrained, occupancy);
                }

                if (y > 0)
                {
                    int down = c - _layerStride;
                    int gDown = g + hopDown;
                    if (x > 0) RelaxHop(c, down - 1, gDown, goal, mode, stamp, rstamp, constrained, occupancy);
                    if (x + 1 < _sizeX) RelaxHop(c, down + 1, gDown, goal, mode, stamp, rstamp, constrained, occupancy);
                    if (z > 0) RelaxHop(c, down - _sizeX, gDown, goal, mode, stamp, rstamp, constrained, occupancy);
                    if (z + 1 < _size.SizeZ) RelaxHop(c, down + _sizeX, gDown, goal, mode, stamp, rstamp, constrained, occupancy);
                }

                // A jump over a one-cell stream, two cells straight across on this layer (design
                // 46). Priced by NavGraph.JumpCost and ruled by NavGraph.IsJumpAcross, never
                // restated here, for the hop's reason: three places that must agree.
                {
                    int gJump = g + jump;
                    if (x > 1) RelaxJump(c, c - 2, gJump, goal, mode, stamp, rstamp, constrained, occupancy);
                    if (x + 2 < _sizeX) RelaxJump(c, c + 2, gJump, goal, mode, stamp, rstamp, constrained, occupancy);
                    if (z > 1) RelaxJump(c, c - 2 * _sizeX, gJump, goal, mode, stamp, rstamp, constrained, occupancy);
                    if (z + 2 < _size.SizeZ) RelaxJump(c, c + 2 * _sizeX, gJump, goal, mode, stamp, rstamp, constrained, occupancy);
                }

                if ((_grid.Flags[c] & NavFlags.Connector) != 0)
                {
                    for (int e = _graph.FirstPortalEdge(c); e != -1; e = _graph.PortalEdgeNext(e))
                    {
                        if (!TraverseModes.Allows(_graph.PortalEdgeMode(e), mode)) continue;
                        int n = _graph.PortalEdgeTarget(e);
                        int pCost = _graph.PortalEdgeCost(e);
                        if (occupancy != null && occupancy(n)) pCost += MoveCost.OccupiedBias;
                        RelaxExplicit(c, n, g + pCost, goal, mode, stamp, rstamp, constrained);
                    }
                }

                if (falls)
                {
                    if (x > 0) TryFall(c, c - 1, y, g, goal, mode, stamp, rstamp);
                    if (x + 1 < _sizeX) TryFall(c, c + 1, y, g, goal, mode, stamp, rstamp);
                    if (z > 0) TryFall(c, c - _sizeX, y, g, goal, mode, stamp, rstamp);
                    if (z + 1 < _size.SizeZ) TryFall(c, c + _sizeX, y, g, goal, mode, stamp, rstamp);
                }
            }

            return PathStatus.Unreachable;
        }

        void Relax(int from, int n, int g, int goal, TraverseMode mode, int stamp, int rstamp,
            bool constrained, Func<int, bool>? occupancy)
        {
            // The horizontal expansion, and the one place a route over open air was being planned.
            // CanWalkInto refuses a cell that is standable only because a climb goes through it;
            // RelaxExplicit below keeps plain CanEnter, because the vertical expansion above it
            // is exactly how a colonist is supposed to reach one.
            if (!_grid.CanWalkInto(n, mode)) return;
            int stepCost = _grid.EnterCost(n, mode);
            if (occupancy != null && occupancy(n)) stepCost += MoveCost.OccupiedBias;
            RelaxExplicit(from, n, g + stepCost, goal, mode, stamp, rstamp, constrained);
        }

        void RelaxDiagonal(int from, int n, int c1, int c2, int g, int goal, TraverseMode mode,
            int stamp, int rstamp, bool constrained, Func<int, bool>? occupancy)
        {
            if (!_grid.CanWalkInto(n, mode)) return;
            if (!_grid.CanWalkInto(c1, mode) || !_grid.CanWalkInto(c2, mode)) return;
            int stepCost = _grid.EnterCost(n, mode, diagonal: true);
            if (occupancy != null && occupancy(n)) stepCost += MoveCost.OccupiedBias;
            RelaxExplicit(from, n, g + stepCost, goal, mode, stamp, rstamp, constrained);
        }

        /// <summary>
        /// A hop costs its own price rather than the destination's entry cost, and is refused
        /// unless the destination is somewhere a colonist could walk — the same test as a step on
        /// the flat, because the point of the rule is that both ends have a floor.
        /// </summary>
        void RelaxHop(int from, int n, int ng, int goal, TraverseMode mode, int stamp, int rstamp,
            bool constrained, Func<int, bool>? occupancy)
        {
            if (!_grid.CanWalkInto(n, mode)) return;

            // Onto a block, never up a storey: the upper end of the hop has to be standing on
            // solid terrain. See NavGraph.UpperEndIsABlockTop — without it every floor of every
            // building is one hop from the floor below and stairs are decoration.
            if (!_graph.UpperEndIsABlockTop(n > from ? n : from)) return;
            // And an animal only where the lower end is a drawn ramp. See NavGraph.HopMask.
            if (!_graph.HopAllowed(n > from ? from : n, mode)) return;

            if (occupancy != null && occupancy(n)) ng += MoveCost.OccupiedBias;
            RelaxExplicit(from, n, ng, goal, mode, stamp, rstamp, constrained);
        }

        /// <summary>
        /// A jump costs its own price, like a hop, and is legal exactly when
        /// <see cref="NavGraph.IsJumpAcross"/> says so.
        /// </summary>
        void RelaxJump(int from, int n, int ng, int goal, TraverseMode mode, int stamp, int rstamp,
            bool constrained, Func<int, bool>? occupancy)
        {
            if (!_graph.IsJumpAcross(from, n, mode)) return;
            if (occupancy != null && occupancy(n)) ng += MoveCost.OccupiedBias;
            RelaxExplicit(from, n, ng, goal, mode, stamp, rstamp, constrained);
        }

        void RelaxExplicit(int from, int n, int ng, int goal, TraverseMode mode, int stamp,
            int rstamp, bool constrained)
        {
            if (!_grid.CanEnter(n, mode)) return;
            if (constrained && !InCorridor(_graph.RegionOfCell(n), rstamp)) return;

            int st = _cellStamp[n];
            if (st == -stamp) return;
            if (st == stamp && _cellG[n] <= ng) return;

            _cellG[n] = ng;
            _cellFrom[n] = from;
            _cellStamp[n] = stamp;
            int h = CellHeuristic(n, goal, rstamp);
            _cellHeap.Push(ng + h, h, n, n);
        }

        void TryFall(int c, int hole, int y, int g, int goal, TraverseMode mode, int stamp, int rstamp)
        {
            if (!_grid.IsAir(hole)) return;
            int step = hole;
            for (int k = 1; k <= NavGraph.MaxFallLayers && y - k >= 0; k++)
            {
                step -= _layerStride;
                if ((_grid.Flags[step] & NavFlags.Walkable) != 0)
                {
                    RelaxExplicit(c, step, g + MoveCost.Fall, goal, mode, stamp, rstamp, false);
                    return;
                }

                if (!_grid.IsAir(step)) return;
            }
        }

        int CellHeuristic(int cell, int goal, int rstamp)
        {
            if (rstamp != 0)
            {
                int r = _graph.RegionOfCell(cell);
                if (r >= 0 && (_regionStamp[r] == rstamp || _regionStamp[r] == -rstamp))
                {
                    int d = _regionG[r];
                    return d == 0 ? Octile(cell, goal) : d;
                }
            }

            return Octile(cell, goal);
        }

        int Octile(int a, int b)
        {
            int ay = a / _layerStride;
            int arem = a - ay * _layerStride;
            int az = arem / _sizeX;
            int ax = arem - az * _sizeX;

            int by = b / _layerStride;
            int brem = b - by * _layerStride;
            int bz = brem / _sizeX;
            int bx = brem - bz * _sizeX;

            int dx = Math.Abs(ax - bx);
            int dz = Math.Abs(az - bz);
            int min = dx < dz ? dx : dz;
            int max = dx < dz ? dz : dx;

            return min * MoveCost.Diagonal + (max - min) * MoveCost.Orthogonal
                   + Math.Abs(ay - by) * LayerChangeHint;
        }

        void Reconstruct(int start, int goal)
        {
            int n = 0;
            for (int c = goal; c != -1; c = _cellFrom[c])
            {
                n++;
                if (c == start) break;
            }

            if (_pathBuffer.Length < n) _pathBuffer = new int[Math.Max(n, _pathBuffer.Length * 2)];
            int i = n;
            for (int c = goal; c != -1; c = _cellFrom[c])
            {
                _pathBuffer[--i] = c;
                if (c == start) break;
            }

            _pathLength = n;
        }

        ulong Checksum(int length)
        {
            var h = StateHash.New();
            for (int i = 0; i < length; i++) h.Add(_pathBuffer[i]);
            return h.Value;
        }

        /// <summary>
        /// Every consecutive pair of a path must be a real graph edge. No exceptions, and in
        /// particular no "a one-layer jump is acceptable" tolerance: that single line in
        /// Cataclysm DDA's route validator is why a broken vertical path there is indistinguishable
        /// from a working one.
        /// </summary>
        public bool ValidatePath(ReadOnlySpan<int> cells, TraverseMode mode, bool allowFalls = false)
        {
            if (cells.Length == 0) return false;
            if (!_grid.CanEnter(cells[0], mode)) return false;
            for (int i = 1; i < cells.Length; i++)
                if (!_graph.IsLegalStep(cells[i - 1], cells[i], mode, allowFalls)) return false;
            return true;
        }

        /// <summary>
        /// Binary min-heap on the total order (f, h, key). The tie-break is not decoration: an
        /// A-star whose frontier has no uniquely preferred node is non-deterministic, and the
        /// published experiments that looked for this found exactly that. Equal children pick the
        /// left one, so even the sift is fixed.
        /// </summary>
        sealed class MinHeap
        {
            int[] _f;
            int[] _h;
            int[] _k;
            int[] _p;
            int _count;

            public MinHeap(int capacity)
            {
                _f = new int[capacity];
                _h = new int[capacity];
                _k = new int[capacity];
                _p = new int[capacity];
            }

            public int Count => _count;

            public void Clear() => _count = 0;

            public void Push(int f, int h, int k, int payload)
            {
                if (_count == _f.Length)
                {
                    int n = _f.Length * 2;
                    Array.Resize(ref _f, n);
                    Array.Resize(ref _h, n);
                    Array.Resize(ref _k, n);
                    Array.Resize(ref _p, n);
                }

                int i = _count++;
                _f[i] = f;
                _h[i] = h;
                _k[i] = k;
                _p[i] = payload;
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (!Less(i, p)) break;
                    Swap(i, p);
                    i = p;
                }
            }

            public void Pop(out int f, out int h, out int payload)
            {
                f = _f[0];
                h = _h[0];
                payload = _p[0];
                int last = --_count;
                if (last == 0) return;
                _f[0] = _f[last];
                _h[0] = _h[last];
                _k[0] = _k[last];
                _p[0] = _p[last];

                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1;
                    if (l >= _count) break;
                    int r = l + 1;
                    int c = l;
                    if (r < _count && Less(r, l)) c = r; // strict: equal children choose the left
                    if (!Less(c, i)) break;
                    Swap(c, i);
                    i = c;
                }
            }

            bool Less(int a, int b)
            {
                if (_f[a] != _f[b]) return _f[a] < _f[b];
                if (_h[a] != _h[b]) return _h[a] < _h[b];
                return _k[a] < _k[b];
            }

            void Swap(int a, int b)
            {
                int t = _f[a]; _f[a] = _f[b]; _f[b] = t;
                t = _h[a]; _h[a] = _h[b]; _h[b] = t;
                t = _k[a]; _k[a] = _k[b]; _k[b] = t;
                t = _p[a]; _p[a] = _p[b]; _p[b] = t;
            }
        }
    }
}
