#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Events;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Pawns.Wildlife;

namespace Odyssey.Sim.Trade
{
    /// <summary>
    /// What a visitor does because it is visiting (design 57 §5), ahead of the idle node in
    /// <c>JobSystem.VisitorTree</c>:
    /// <list type="bullet">
    /// <item><b>Leaving</b>: walk to the nearest edge it can reach and wait there, where the trade
    /// system takes it off the board.</item>
    /// <item><b>Held</b> by a negotiation: stand where it is, so the negotiator can reach it.</item>
    /// <item><b>Far from the hearth</b> (else the colony's start): walk there.</item>
    /// <item><b>At the hearth</b>: decline, so the idle node settles it at the fire the way it
    /// settles an idle colonist.</item>
    /// </list>
    /// </summary>
    public sealed class VisitorThinkNode : ThinkNode
    {
        /// <summary>This near the hearth, in cells, and the visitor has arrived: the idle node has it. INVENTED.</summary>
        public const int StayCells = 8;

        /// <summary>A wait while held or at the edge, in ticks, before it thinks again.</summary>
        public const int WaitTicks = 60;

        public override string Name => "Visit";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (pawn.Downed) return false;
            TraverseMode mode = pawn.OwnMode;

            if (pawn.Leaving)
            {
                if (!WildlifeSystem.IsEdge(ctx.Size, pawn.Cell) && EdgeTarget.Fill(pawn, ctx, job, mode)) return true;
                return Wait(job, mode);
            }

            // Held by a negotiation (design 57 §6): stand where the negotiator is walking to.
            if (ctx.Trade?.VisitOf(pawn.Id.Value) is { InSession: true }) return Wait(job, mode);

            int anchor = RaidTargets.Resolve(ctx, -1);
            if (anchor < 0) return false;
            if (Cells(ctx.Size, pawn.Cell, anchor) <= StayCells) return false;
            if (!ctx.Reachable(pawn, anchor, mode)) return false;

            job.Reset(JobIndex.Wander);
            job.TargetCell = anchor;
            job.Mode = mode;
            return true;
        }

        static bool Wait(Job job, TraverseMode mode)
        {
            job.Reset(JobIndex.Wait);
            job.WorkTicks = WaitTicks;
            job.Mode = mode;
            return true;
        }

        /// <summary>Chebyshev distance across the board, in cells, ignoring the layer.</summary>
        static int Cells(GridSize size, int a, int b)
        {
            CellRef p = size.FromIndex(a), q = size.FromIndex(b);
            return System.Math.Max(System.Math.Abs(p.X - q.X), System.Math.Abs(p.Z - q.Z));
        }
    }
}
