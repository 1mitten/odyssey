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

        /// <summary>Jumps over a stream that landed on the far bank (design 43).</summary>
        public int JumpSteps { get; private set; }

        /// <summary>Jumps over a stream that fell short, into the water (design 43 §6).</summary>
        public int JumpsFailed { get; private set; }
        public int PathsServed { get; private set; }
        public int PathsFailed { get; private set; }

        public void Tick(SimWorld world)
        {
            _ctx.Sync(world);
            ApplyServedPaths();

            var pawns = _ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++) Advance(pawns[i]);

            // A carried pawn is where her carrier is (design 33 §11a), after every step of the
            // tick, so the two never disagree between ticks. One flag a pawn; nobody carried is
            // nothing more.
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn carried = pawns[i];
                if (carried.CarriedBy == 0) continue;
                Pawn? carrier = _ctx.Pawns.Get(new Contracts.PawnId(carried.CarriedBy));
                if (carrier != null) carried.Cell = carrier.Cell;
            }
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

            // Knocked down (design 33 §9b): lying where the blow put it, it takes no step at all.
            // The knockback cleared its path, so this is the backstop for an order given while it
            // lies there.
            if (pawn.KnockedDownAt(_ctx.CurrentTick)) return;

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

                // Take-off (design 43 §6). The one roll that decides a jump is made the first
                // tick it is the step in hand, before any of its price is paid, so the drawing
                // knows where it lands for the whole of the step rather than finding out at the
                // end — which would move the figure 2.5 m in a frame.
                if (pawn.JumpLanding < 0
                    && NavGraph.IsJump(_ctx.Size.FromIndex(pawn.Cell), _ctx.Size.FromIndex(next)))
                    next = CommitJump(pawn, next);

                // A jump in the air costs a jump whether or not it clears the water: the colonist
                // was in the air for the same time either way. A short one is a step into the
                // water beside the bank, which on its own would be priced as a drop.
                int cost = (pawn.JumpLanding >= 0 ? NavGraph.JumpCost() : StepCost(pawn.Cell, next, pawn.Mode))
                           * Rates.Scale;

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
                if (pawn.JumpLanding >= 0)
                {
                    // Counted before the layer test: a short landing changes layer, and it is a
                    // jump that failed rather than a hop.
                    if (pawn.Cell / _ctx.Size.LayerStride == next / _ctx.Size.LayerStride) JumpSteps++;
                    else JumpsFailed++;
                    pawn.JumpLanding = -1;
                }
                else if (pawn.Cell / _ctx.Size.LayerStride != next / _ctx.Size.LayerStride)
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
        /// Roll a jump over a stream once, at take-off, and record where it lands (design 43 §6).
        /// Returns the cell the step now ends in: the far bank, or the water short of it.
        ///
        /// <para>The chance is <see cref="MovementDef.jumpFailPerMille"/>, scaled up by
        /// <see cref="MovementDef.jumpFailCarryingPerMille"/> while carrying and by the inverse of
        /// the pawn's condition — the one scalar both rates already read, so a starving or frozen
        /// colonist is likelier to fall in and there is one place to ask why. Rolled off
        /// <see cref="PawnPurpose.Jump"/> keyed by tick and pawn, the melee roll's discipline, so
        /// a lockstep twin rolls the same.</para>
        /// </summary>
        int CommitJump(Pawn pawn, int far)
        {
            int fail = _ctx.DebugJumpsAlwaysFail
                ? Rates.Scale
                : JumpFailPerMille(_ctx.Content.Movement, IsCarrying(pawn), pawn.ConditionPerMille());

            if (!FallsShort(_ctx.Seed, _ctx.CurrentTick, pawn.Id.Value, fail))
            {
                pawn.JumpLanding = far;
                return far;
            }

            int water = _ctx.Nav.ShortLanding(pawn.Cell, far);
            pawn.LandShort(water);
            return water;
        }

        /// <summary>
        /// The chance a jump falls short, per mille (design 43 §6): the base, doubled (by default)
        /// while carrying, and divided by the pawn's condition — 1.0 when well, 1.43 at the floor.
        /// Clamped to 1,000. Public so the formula is tested as a formula and not only through a
        /// one-in-thirty event.
        /// </summary>
        public static int JumpFailPerMille(MovementDef def, bool carrying, int conditionPerMille)
        {
            long fail = def.jumpFailPerMille;
            if (carrying) fail = fail * def.jumpFailCarryingPerMille / Rates.Scale;
            if (conditionPerMille > 0) fail = fail * Rates.Scale / conditionPerMille;
            if (fail < 0) fail = 0;
            return fail > Rates.Scale ? Rates.Scale : (int)fail;
        }

        /// <summary>
        /// The one roll (design 43 §6), on <see cref="PawnPurpose.Jump"/> keyed by the tick and the
        /// pawn — the melee roll's discipline — so a lockstep twin rolls the same.
        /// </summary>
        public static bool FallsShort(uint seed, int tick, int pawnId, int failPerMille)
        {
            var roll = DeterministicRandom.ForTick(seed, tick, PawnPurpose.Jump ^ (uint)pawnId);
            return roll.NextInt(Rates.Scale) < failPerMille;
        }

        /// <summary>A load in the arms, or a person being carried to a bed.</summary>
        bool IsCarrying(Pawn pawn)
        {
            if (pawn.CurrentJob != null && pawn.CurrentJob.CarriedItem >= 0) return true;
            var all = _ctx.Pawns.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].CarriedBy == pawn.Id.Value) return true;
            return false;
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

            // A jump over a one-cell stream (design 43). Without this it fell through to
            // EnterCost and was charged as one flat cell for two cells of ground — the planner and
            // the mover disagreeing about a price, which is the fault HopCost exists to prevent.
            if (NavGraph.IsJump(pa, pb)) return NavGraph.JumpCost();

            bool diagonal = pa.X != pb.X && pa.Z != pb.Z;
            return _ctx.Nav.Grid.EnterCost(to, mode, diagonal);
        }
    }
}
