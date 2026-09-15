#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// Runs the support solver inside the tick, and turns collapses into deferred structural
    /// events.
    ///
    /// The adapter exists so the solver itself stays a pure, testable library that knows nothing
    /// about ticks: it is handed a grid and asked a question. This is the seam that lets the
    /// solver be exercised by 7,900 random edits in a unit test without a world existing at all.
    ///
    /// Collapses are **deferred**, never applied inline. A collapse changes the grid, and other
    /// systems in the same phase may be part-way through scanning it; mutating underneath them is
    /// the classic source of both crashes and desyncs.
    /// </summary>
    public sealed class SupportSystem : IWorldSystem
    {
        readonly SupportSolver _solver;
        readonly CellGrid _grid;
        readonly ChunkGrid? _chunks;
        readonly List<CellRef> _lastCollapses = new List<CellRef>();

        public SupportSystem(CellGrid grid, SupportSolver solver, ChunkGrid? chunks = null)
        {
            _grid = grid;
            _solver = solver;
            _chunks = chunks;
        }

        public string Name => "Support";

        public TickPhase Phase => TickPhase.WorldSystems;

        /// <summary>
        /// Early in the phase: the world should settle structurally before anything reads it.
        /// Navigation rebuilds after this, because a collapse changes what is walkable.
        /// </summary>
        public int Order => 10;

        /// <summary>What collapsed on the most recent tick. Cleared at the start of each solve.</summary>
        public IReadOnlyList<CellRef> LastCollapses => _lastCollapses;

        public int TotalCollapses { get; private set; }

        public void Tick(SimWorld world)
        {
            _lastCollapses.Clear();
            if (_solver.DirtyCount == 0) return;

            var collapsed = _solver.SolveIncremental();
            if (collapsed.Count == 0) return;

            for (int i = 0; i < collapsed.Count; i++) _lastCollapses.Add(collapsed[i]);
            TotalCollapses += collapsed.Count;

            // Mark the affected chunks so rendering and navigation rebuild only what moved.
            if (_chunks != null)
                for (int i = 0; i < collapsed.Count; i++)
                    _chunks.MarkDirty(collapsed[i]);

            // The consequences of a collapse (rubble, fall damage, dropping what stood on it)
            // belong to M3 and run as a structural event, not inline.
            world.Defer(_ => { /* M3: rubble and fall damage for _lastCollapses */ });
        }
    }
}
