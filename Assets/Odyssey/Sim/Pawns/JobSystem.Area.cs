#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    // Where a colonist may work (design 43 §4): the setting. In the job system for the reason the
    // response's handler gives — a new setting can end the job in hand, and ending jobs is what
    // the pipeline is.
    public sealed partial class JobSystem
    {
        /// <summary>
        /// <c>SetPawnArea(A = pawn, B = area)</c> (design 43 §4a).
        ///
        /// <para>Refused for a pawn that is not a colonist and for a value that is not an area;
        /// <c>AlreadyInThatState</c> for a no-op. Any colonist may be given one — drafted, downed
        /// or broken — because it is a standing setting; the draft overrides it while it lasts.</para>
        ///
        /// <para><b>A new setting answers at once</b> (§4f). Kept home while her job's target or
        /// the cell it takes her to is outside, an undrafted colonist is interrupted, keeping her
        /// step, and thinks again on her next tick. A fight, a flight and lying downed are never
        /// ended by it: those are the response's and the body's, not work.</para>
        /// </summary>
        public IntentRejection HandleSetPawnArea(Intent intent)
        {
            Pawn? pawn = _ctx.Pawns.Get(new PawnId(intent.A));
            if (pawn == null || !pawn.IsColonist) return IntentRejection.NotPermitted;
            if (intent.B < 0 || intent.B >= PawnAreas.Count) return IntentRejection.NotPermitted;

            var want = (PawnArea)intent.B;
            if (pawn.Area == want) return IntentRejection.AlreadyInThatState;
            pawn.Area = want;

            if (!pawn.Drafted && !pawn.Downed && pawn.CurrentJob is Job job && WorksOutside(pawn, job))
                Interrupt(pawn, JobStatus.Failed);
            return IntentRejection.None;
        }

        /// <summary>Does this job take her to, or act on, a cell she may not work in?</summary>
        bool WorksOutside(Pawn pawn, Job job)
        {
            if (job.DefIndex == JobIndex.AttackMelee || job.DefIndex == JobIndex.Flee
                || job.DefIndex == JobIndex.Downed)
                return false;
            return (job.TargetCell >= 0 && !_ctx.MayWork(pawn, job.TargetCell))
                   || (job.DestCell >= 0 && !_ctx.MayWork(pawn, job.DestCell));
        }
    }

    /// <summary>
    /// The walk home (design 43 §4e): for a colonist kept home, undrafted, standing outside a home
    /// that exists, a walk to the nearest home cell she can get to, on whatever layer it is.
    ///
    /// <para><b>What it scales with.</b> The home's cells, and only for a colonist standing
    /// outside home with nothing to do, which is rare; everybody else pays the first comparison.
    /// A tie keeps the lower cell index, so the choice is the same in a twin.</para>
    /// </summary>
    static class HomeTarget
    {
        public static bool Fill(Pawn pawn, PawnContext ctx, Job job)
        {
            if (pawn.Area != PawnArea.Home || pawn.Drafted || !pawn.IsColonist) return false;
            World.HomeArea? home = ctx.Home;
            if (home == null || home.IsEmpty || home.Contains(pawn.Cell)) return false;

            int cell = Nearest(pawn, ctx, home);
            if (cell < 0) return false;

            job.Reset(JobIndex.Wander);
            job.TargetCell = cell;
            job.Mode = TraverseMode.Colonist;
            return true;
        }

        static int Nearest(Pawn pawn, PawnContext ctx, World.HomeArea home)
        {
            // Every home cell, not the layers beside hers: home reaches one layer past what was
            // built, so a colonist two layers down a quarry or two terraces up has no home cell on
            // her own layer or either neighbour, and a search of those three left her there for
            // good. A tie keeps the lower cell index, so the choice does not depend on the order
            // the flood happened to list the cells in.
            IReadOnlyList<int> cells = home.Cells;
            int best = -1, bestDistance = int.MaxValue;
            for (int i = 0; i < cells.Count; i++)
            {
                int c = cells[i];
                int distance = ctx.Distance(pawn.Cell, c);
                if (distance > bestDistance || (distance == bestDistance && c > best)) continue;
                if (!ctx.CanTravel(pawn, c, TraverseMode.Colonist)) continue;
                bestDistance = distance;
                best = c;
            }
            return best;
        }
    }
}
