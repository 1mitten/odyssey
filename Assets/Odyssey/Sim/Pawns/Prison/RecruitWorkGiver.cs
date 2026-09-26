#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// A warden talks to the nearest prisoner in Recruit mode who is due a chat (design 59 §8): once
    /// every six game hours each, four a day. <b>Nobody held, nothing touched</b>: custody is asked
    /// first, so a colony with no prisoner pays one comparison a pawn.
    ///
    /// <para><b>Named to sort after feeding.</b> Givers of one work type are scanned by type name,
    /// and a hungry prisoner outranks a talk that will keep for an hour.</para>
    /// </summary>
    public sealed class RecruitWorkGiver : WorkGiver
    {
        public override string Name => "Recruit";

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
                if (prisoner.Custody != PawnCustody.Prisoner || !Recruitment.Due(prisoner, ctx.CurrentTick)) continue;
                if (prisoner.CarriedBy != 0) continue;
                int distance = ctx.Distance(pawn.Cell, prisoner.Cell);
                if (distance >= bestDistance) continue;
                if (!ctx.Reservations.CanReserve(pawn.Id, ReservationManager.Key(ReservationTargetKind.Pawn, prisoner.Id.Value))) continue;
                if (!ctx.Reachable(pawn, prisoner.Cell)) continue;
                best = prisoner;
                bestDistance = distance;
            }
            if (best == null) return false;
            job.Reset(JobIndex.Chat);
            job.WorkTicks = best.Id.Value;
            job.TargetCell = -1;
            return true;
        }
    }
}
