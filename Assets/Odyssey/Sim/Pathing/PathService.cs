#nullable enable
using System;
using System.Collections.Generic;

namespace Odyssey.Sim.Pathing
{
    public readonly struct PathRequest
    {
        public readonly int AgentId;
        public readonly int Start;
        public readonly int Goal;
        public readonly TraverseMode Mode;

        public PathRequest(int agentId, int start, int goal, TraverseMode mode)
        {
            AgentId = agentId;
            Start = start;
            Goal = goal;
            Mode = mode;
        }
    }

    public readonly struct ServedPath
    {
        public readonly PathRequest Request;
        public readonly PathResult Result;

        /// <summary>The cells, copied out of the finder's shared buffer. Empty unless successful.</summary>
        public readonly int[] Cells;

        public ServedPath(PathRequest request, PathResult result, int[] cells)
        {
            Request = request;
            Result = result;
            Cells = cells;
        }
    }

    /// <summary>
    /// The path service: a FIFO queue drained under a per-tick node budget.
    ///
    /// Two properties, both deliberate. The queue is drained in insertion order with ties broken
    /// by ascending agent id, so the order requests are served in is a function of the simulation
    /// and not of who happened to ask first in wall-clock terms. And the budget counts node
    /// expansions rather than elapsed time, so a slow machine produces a slow game rather than a
    /// different game.
    ///
    /// A request that does not fit in this tick's remaining budget stays queued at the head; the
    /// agent keeps walking its old path or waits. Nothing is dropped and nothing is reordered.
    /// </summary>
    public sealed class PathService
    {
        readonly PathFinder _finder;
        readonly List<PathRequest> _queue = new List<PathRequest>();
        readonly List<ServedPath> _served = new List<ServedPath>();
        int _head;

        public PathService(PathFinder finder, int nodeBudgetPerTick = 20_000, int nodeBudgetPerRequest = 6_000)
        {
            _finder = finder;
            NodeBudgetPerTick = nodeBudgetPerTick;
            NodeBudgetPerRequest = nodeBudgetPerRequest;
        }

        public int NodeBudgetPerTick { get; set; }
        public int NodeBudgetPerRequest { get; set; }

        /// <summary>
        /// Optional predicate determining if a cell is occupied by a stationary/working pawn,
        /// forwarded to the underlying <see cref="PathFinder"/>.
        /// </summary>
        public Func<int, bool>? Occupancy
        {
            get => _finder.Occupancy;
            set => _finder.Occupancy = value;
        }

        public int Pending => _queue.Count - _head;

        /// <summary>Results served by the last <see cref="Serve"/>, in request order.</summary>
        public IReadOnlyList<ServedPath> Served => _served;

        public void Enqueue(in PathRequest request) => _queue.Add(request);

        /// <summary>
        /// Drain the queue until the tick budget is spent. Requests are served in order; the
        /// caller applies the results in the same order.
        /// </summary>
        public int Serve()
        {
            _served.Clear();
            int spent = 0;

            while (_head < _queue.Count && spent < NodeBudgetPerTick)
            {
                PathRequest request = _queue[_head];
                int allowance = Math.Min(NodeBudgetPerRequest, NodeBudgetPerTick - spent);
                var options = new PathOptions(allowance, allowance);
                PathResult result = _finder.FindPath(request.Start, request.Goal, request.Mode, options);

                // A request that ran out of budget only because this tick was nearly spent is put
                // back, not failed: it would have succeeded on a quieter tick, and a result that
                // depends on how busy the tick was is exactly the kind of thing a determinism
                // gate is supposed to catch.
                if (result.Status == PathStatus.BudgetExhausted && allowance < NodeBudgetPerRequest)
                    break;

                _head++;
                spent += result.CellNodes;
                _served.Add(new ServedPath(request, result,
                    result.Ok ? _finder.PathToArray() : Array.Empty<int>()));
            }

            if (_head > 0 && _head == _queue.Count)
            {
                _queue.Clear();
                _head = 0;
            }

            return spent;
        }
    }
}
