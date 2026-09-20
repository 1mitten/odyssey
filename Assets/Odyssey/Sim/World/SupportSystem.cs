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
        readonly List<int> _fallen = new List<int>();
        readonly System.Action<SimWorld> _applyConsequences;

        public SupportSystem(CellGrid grid, SupportSolver solver, ChunkGrid? chunks = null)
        {
            _grid = grid;
            _solver = solver;
            _chunks = chunks;
            // Held rather than converted from a method group at every collapse, for the reason
            // SimWorld holds its intent handler: a fresh delegate per call is 64 bytes nobody
            // asked for, and a collapse cascade is the worst possible moment to allocate.
            _applyConsequences = ApplyConsequences;
        }

        /// <summary>
        /// The colony, when there is one. Null for a bare structural fixture — the solver tests
        /// drive 7,900 edits through a grid with no pawns, no items and no world at all, and they
        /// must go on being able to — and null is why nothing falls and no rubble lands in one.
        /// </summary>
        Pawns.PawnContext? _pawns;

        /// <summary>
        /// Tell this system whose colony it is collapsing floors on.
        ///
        /// <para><b>Bound rather than constructed with, and that is deliberate.</b> Seventeen places
        /// build a <c>SupportSystem</c> — the world, the screenshot harness, eleven editor probes
        /// and four test fixtures — and every one of them hands it to
        /// <c>ColonyComposition.AddColony</c>, which is the one place that also has the context.
        /// A constructor argument would have been seventeen edits and seventeen chances to pass
        /// null; binding in the composition root means a colony cannot be assembled without it,
        /// which is the argument that file already makes about the construction grid.</para>
        /// </summary>
        internal void Bind(Pawns.PawnContext pawns) => _pawns = pawns;

        /// <summary>
        /// The solver this system runs. Taken from here by the composition root rather than passed
        /// in beside it, so the solver a collapse is computed from and the solver a wall marks
        /// dirty cannot become two different objects.
        /// </summary>
        public SupportSolver Solver => _solver;

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

            // The consequences of a collapse run as a structural event, not inline: this is a
            // world system, and other systems in the same phase may be part-way through scanning
            // the grid it is about to move things around in.
            //
            // The cells are read off _lastCollapses when the event runs rather than captured,
            // which is why the delegate can be held in a field: it closes over nothing.
            _fallen.Clear();
            for (int i = 0; i < collapsed.Count; i++) _fallen.Add(_grid.Index(collapsed[i]));
            world.Defer(_applyConsequences);
        }

        /// <summary>
        /// What a collapse leaves behind, at the structural-events phase of the tick it happened in.
        ///
        /// <para>The solver has already erased the slab; this is everything else, and the order is
        /// the order it has to be in. <b>Things fall before the rubble lands</b>, or a colonist and
        /// a heap of debris arrive in the same cell in the wrong sequence and the rubble is written
        /// under somebody who has not moved yet.</para>
        ///
        /// <para><b>Nobody is hurt, deliberately and temporarily.</b> `a-02` has the number —
        /// <c>15 × layers^1.5</c> blunt on the bottom-facing parts — and there is no health model
        /// for it to act on, so applying it to something invented would be inventing a number in
        /// order to throw it away (owner, 2026-09-17). A colonist that rides a floor down is in a
        /// worse mood and otherwise unharmed; the injury is one line the day parts exist.</para>
        /// </summary>
        void ApplyConsequences(SimWorld world)
        {
            if (_pawns == null) return;

            for (int i = 0; i < _fallen.Count; i++)
            {
                int cell = _fallen[i];

                // 1. Everything that was in the cell has lost what it was standing on. The same
                //    answer a dig gives, from the same place, to the first real floor below rather
                //    than one layer down — see Falling, which is where mining's version went.
                Pawns.Falling.OutOf(_pawns, cell, Pawns.ThoughtIndex.Fell, world.CurrentTick);

                // 2. The mess. Where the debris lands, not where the slab was: a floor that falls
                //    into a stairwell ends up at the bottom of it, which is the rule the spoil from
                //    a dig already follows.
                Rubble(cell);

                // 3. The cell has lost its floor, so what is walkable changed there. The chunk was
                //    marked in the tick above; navigation is marked here because the rubble may
                //    have moved as well.
                _pawns.Nav.MarkDirty(cell);
            }

            Pawns.Falling.DropFloatingItems(_pawns);
            _fallen.Clear();
        }

        /// <summary>
        /// Fill the first floored cell at or below a collapse with rubble.
        ///
        /// <para><b>Only into open air.</b> Writing rubble over grass, pavement or water would
        /// destroy the terrain that was there and clearing it afterwards would leave a hole in the
        /// ground: the cell a colonist stands in is open air with something solid beneath it, and
        /// that air is what a heap of debris can occupy without taking anything's place.</para>
        ///
        /// <para>Rubble is <b>not solid</b>, so it buries nothing: the colonist who just fell is
        /// standing in it and a stack of wood shares the cell with it. What it does is refuse to be
        /// built on (<c>TerrainDef.buildable</c>) until somebody clears it, which is the whole of
        /// "a collapse leaves a mess to clear, not a clean hole" (02 §4).</para>
        /// </summary>
        void Rubble(int cell)
        {
            int landing = _grid.FirstFloorAtOrBelow(cell);
            if (landing == cell) return;
            if (_grid.Terrain[landing] != Worldgen.CoreContent.TerrainAir) return;

            _grid.Terrain[landing] = Worldgen.CoreContent.TerrainRubble;
            _chunks?.MarkDirty(_grid.Size.FromIndex(landing));
        }
    }
}
