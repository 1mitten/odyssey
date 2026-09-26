#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The warden's first duty (design 58 §7): carry the nearest downed pawn the colony means to
    /// hold — a person marked for capture, or a prisoner lying anywhere but a prison bed — to her
    /// own prison bed, else the nearest free one. An <b>emergency</b> giver, because a captured
    /// raider is usually bleeding: it scans ahead of every ordinary giver at the same priority.
    ///
    /// <para><b>Nobody down, nothing touched</b>, as the rescue's: <see cref="Pawn.Downed"/> first,
    /// so a colony that never fought pays one flag a pawn a think.</para>
    /// </summary>
    public sealed class CaptureWorkGiver : WorkGiver
    {
        public override string Name => "Capture";

        public override int WorkType => WorkTypeIndex.Warden;

        public override bool Emergency => true;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (!pawn.IsColonist || pawn.Downed) return false;

            Pawn? best = null;
            int bestBed = -1, bestDistance = int.MaxValue;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn target = pawns[i];
                if (target == pawn || !CaptureRules.WantsCapture(target, ctx)) continue;

                int distance = ctx.Distance(pawn.Cell, target.Cell);
                if (distance >= bestDistance) continue;
                if (!ctx.Reservations.CanReserve(pawn.Id, RescueRules.PatientKey(target))) continue;
                if (!ctx.Reachable(pawn, target.Cell, CaptureRules.Mode)) continue;
                int bed = CaptureRules.BedFor(target, pawn, ctx);
                if (bed < 0) continue;

                best = target;
                bestBed = bed;
                bestDistance = distance;
            }

            if (best == null) return false;
            pawn.CombatTarget = best.Id.Value;
            job.Reset(JobIndex.Capture);
            job.TargetCell = best.Cell;
            job.DestCell = bestBed;
            job.Mode = CaptureRules.Mode;
            return true;
        }
    }
}
