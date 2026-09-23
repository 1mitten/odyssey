#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Serves the path queue and walks pawns along what comes back.
    ///
    /// It runs last in the pawn phase because the order is decide, then move: a driver asks for a
    /// path during the job phase and the pawn takes its first step in the same tick, so a request
    /// never costs an idle tick.
    ///
    /// <para>Paths are <b>recomputed, never saved</b>. A recomputed path is correct by
    /// construction; a saved one can be stale in a world where floors collapse. Nothing about a
    /// path folds into the state hash either, so a resume does not look like a divergence.</para>
    ///
    /// <para>Every step is checked against the graph before it is taken. A path that skips a layer
    /// is a bug, not a convenience, and a connector is the only vertical edge that exists — so a
    /// step that is no longer legal invalidates the path rather than being tolerated.</para>
    /// </summary>
    public sealed class MovementSystem : IWorldSystem
    {
        readonly PawnContext _ctx;

        public MovementSystem(PawnContext ctx) { _ctx = ctx; }

        public string Name => "Movement";

        public TickPhase Phase => TickPhase.Pawns;

        /// <summary>Last in the phase: jobs have decided where to go before anyone moves.</summary>
        public int Order => 30;

        public int StepsTaken { get; private set; }

        /// <summary>
        /// Steps taken along a declared connector — a stair, a ladder or a lift.
        ///
        /// <para>Counted because "did anybody use the stairs" is otherwise unanswerable from
        /// outside. Layers visited used to stand in for it and no longer can: a hop moves a
        /// colonist a storey with nothing built, so wandering over rubble changes layer all day
        /// without a stair being touched. See <c>M2DemoTests</c>, whose control run this is
        /// for.</para>
        /// </summary>
        public int ConnectorSteps { get; private set; }

        /// <summary>Steps taken as a hop: one block up or down, with nothing built.</summary>
        public int HopSteps { get; private set; }
        public int PathsServed { get; private set; }
        public int PathsFailed { get; private set; }

        public void Tick(SimWorld world)
        {
            _ctx.Sync(world);
            ApplyServedPaths();

            var pawns = _ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++) Advance(pawns[i]);
        }

        /// <summary>
        /// Drain the request queue under its node budget and hand each result to its agent. The
        /// queue is FIFO with ties broken by agent id, so what gets served is a function of the
        /// simulation rather than of who happened to ask first in wall-clock terms.
        /// </summary>
        void ApplyServedPaths()
        {
            _ctx.Paths.Serve();
            var served = _ctx.Paths.Served;

            for (int i = 0; i < served.Count; i++)
            {
                var entry = served[i];
                var pawn = _ctx.Pawns.Get(new Contracts.PawnId(entry.Request.AgentId));
                if (pawn == null || !pawn.PathPending) continue;

                // A result for a walk the pawn has since abandoned is dropped, not applied.
                if (entry.Request.Start != pawn.Cell || entry.Request.Goal != pawn.Destination)
                {
                    pawn.PathPending = false;
                    continue;
                }

                if (!entry.Result.Ok || entry.Cells.Length == 0)
                {
                    pawn.PathPending = false;
                    pawn.PathFailed = true;
                    PathsFailed++;
                    continue;
                }

                pawn.AdoptPath(entry.Cells, entry.Cells.Length);
                PathsServed++;
            }
        }

        void Advance(Pawn pawn)
        {
            if (pawn.Asleep) return;

            if (!pawn.HasPath)
            {
                // A pawn with no path stands still, and there is nowhere it can be standing where
                // that is wrong. This used to have to catch a colonist left hanging on a rock face
                // — a cell that was standable without a floor — and let it go. Climbing is gone
                // (owner, 2026-09-16), every cell a pawn can be in has something under it, and the
                // let-go had nothing left to rescue.
                return;
            }

            // A stunned pawn lands the step it is part way through and takes no other (design 33
            // §4 C3, §5c) — never a snap back, never a new step. The job pipeline holds the rest of
            // it (JobSystem.TickPawn). Nought in every golden window, so no golden moves.
            bool stunned = pawn.StunnedAt(_ctx.CurrentTick);
            if (stunned && pawn.MoveProgress == 0) return;

            // Progress and cost both count thousandths of the raw nav cost (Rates): the planner's
            // prices are untouched, so no path changes — the pawn just retires more or fewer
            // thousandths a tick. Rate multiplies the pawn's progress; cost prices the cell, and
            // the two must never swap jobs (design 17 §4g).
            pawn.MoveProgress += pawn.MoveRatePerMille();

            while (pawn.HasPath)
            {
                int next = pawn.Path[pawn.PathIndex];

                if (!_ctx.Nav.IsLegalStep(pawn.Cell, next, pawn.Mode))
                {
                    // The world changed under the pawn. Drop the path; the driver will ask for a
                    // new one, or fail the job if the target is no longer reachable at all.
                    pawn.ClearPath();
                    return;
                }

                int cost = StepCost(pawn.Cell, next, pawn.Mode) * Rates.Scale;

                // Carried so presentation can glide the figure across the WHOLE step rather than
                // across its first hundred units. Scaled with progress, so the published ratio
                // reads what it always read. See Pawn.MoveStepCost.
                pawn.MoveStepCost = cost;
                if (pawn.MoveProgress < cost) return;

                pawn.MoveProgress -= cost;

                // Counted HERE, below the guard and beside StepsTaken, because a step is taken
                // once and paid for over many ticks.
                //
                // The first version of these counters sat above the guard, where they fired on
                // every tick a pawn spent part way through a vertical step — so they counted
                // pawn-ticks weighted by the cost of the move, not moves. A jump at 135 counted
                // 135 times, a drop at 50 counted 50, a stair up at 290 counted 290. That made
                // the two categories incomparable with each other as well as inflated: stairs are
                // dearer per traversal than hops, so "hops against connectors" was reading a
                // price difference as a frequency difference.
                if (pawn.Cell / _ctx.Size.LayerStride != next / _ctx.Size.LayerStride)
                {
                    if (NavGraph.IsHop(_ctx.Size.FromIndex(pawn.Cell), _ctx.Size.FromIndex(next)))
                        HopSteps++;
                    else
                        ConnectorSteps++;
                }

                pawn.Cell = next;
                pawn.PathIndex++;
                StepsTaken++;

                // The step in hand has landed; a stunned pawn stops here, with nothing banked.
                if (stunned)
                {
                    pawn.MoveProgress = 0;
                    if (!pawn.HasPath) break;
                    return;
                }
            }

            // Arrived. Anything left over is discarded rather than banked toward the next walk,
            // so a pawn cannot accumulate free movement by taking short journeys.
            // This also retires an interrupted step's mark (design 33 §2d): see Pawn.ClearPath.
            pawn.ClearPath();
        }

        /// <summary>
        /// What one step costs. A layer change charges the connector's own declared cost, which
        /// is where stair cost belongs — put it in the graph and ordinary distance ordering
        /// handles verticality correctly everywhere, including in cases nobody thought about.
        /// </summary>
        int StepCost(int from, int to, TraverseMode mode)
        {
            int stride = _ctx.Size.LayerStride;
            if (from / stride != to / stride)
            {
                // A declared connector is one of the two things that can authorise a layer change.
                // There is no run-time search for a landing, anywhere, ever.
                for (int edge = _ctx.Nav.FirstPortalEdge(from); edge != -1; edge = _ctx.Nav.PortalEdgeNext(edge))
                    if (_ctx.Nav.PortalEdgeTarget(edge) == to && TraverseModes.Allows(_ctx.Nav.PortalEdgeMode(edge), mode))
                        return _ctx.Nav.PortalEdgeCost(edge);

                // The other is a hop: one block up or one block down into the column next door,
                // which needs nothing built and so declares no connector to carry its price.
                //
                // **This is what "colonists stick on faces" was.** IsLegalStep let the hop
                // through and PathFinder planned it at JumpUp or Drop, but the price charged for
                // actually taking the step was read only off connectors — so a hop fell through
                // to MoveCost.Fall, which is 100,000 and means "effectively forbidden". The pawn
                // did not fail and did not re-plan: it stood in the cell before the step with a
                // legal path in hand, accumulating about one unit of progress a tick against a
                // bill of a hundred thousand. Measured on the mining fixture: still there, path
                // intact and next step legal, after 10,000 ticks. A price the planner and the
                // mover disagree about is worse than a wrong price, because nothing reports it.
                CellRef a = _ctx.Size.FromIndex(from);
                CellRef b = _ctx.Size.FromIndex(to);
                if (NavGraph.IsHop(a, b)) return NavGraph.HopCost(a, b);

                return MoveCost.Fall;
            }

            CellRef pa = _ctx.Size.FromIndex(from);
            CellRef pb = _ctx.Size.FromIndex(to);
            bool diagonal = pa.X != pb.X && pa.Z != pb.Z;
            return _ctx.Nav.Grid.EnterCost(to, mode, diagonal);
        }
    }
}
