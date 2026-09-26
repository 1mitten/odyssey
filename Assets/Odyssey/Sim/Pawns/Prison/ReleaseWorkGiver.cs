#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// A warden goes to the nearest prisoner the player has chosen to release or exile and lets her
    /// go (design 60 §10). Named to sort after feeding and talking by the name tiebreak: letting
    /// somebody go keeps, and a hungry prisoner does not.
    /// </summary>
    public sealed class ReleaseWorkGiver : WorkGiver
    {
        public override string Name => "Release";

        public override int WorkType => WorkTypeIndex.Warden;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (!pawn.IsColonist || pawn.Downed) return false;
            Pawn? best = null;
            int bestDistance = int.MaxValue;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn prisoner = pawns[i];
                if (prisoner.Custody != PawnCustody.Prisoner || !PrisonRelease.Wanted(prisoner)) continue;
                if (prisoner.CarriedBy != 0) continue;
                int distance = ctx.Distance(pawn.Cell, prisoner.Cell);
                if (distance >= bestDistance) continue;
                if (!ctx.Reservations.CanReserve(pawn.Id, ReservationManager.Key(ReservationTargetKind.Pawn, prisoner.Id.Value))) continue;
                if (!ctx.Reachable(pawn, prisoner.Cell)) continue;
                best = prisoner;
                bestDistance = distance;
            }
            if (best == null) return false;
            job.Reset(JobIndex.Escort);
            job.WorkTicks = best.Id.Value;
            job.TargetCell = -1;
            return true;
        }
    }
}
