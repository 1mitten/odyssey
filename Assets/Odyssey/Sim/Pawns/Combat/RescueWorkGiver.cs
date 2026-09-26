#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The automatic rescue (design 33 §11b): a colonist whose Rescue priority is set carries the
    /// nearest downed colonist she can reach to that colonist's own bed, else the nearest free one.
    /// An <b>emergency</b> giver, so it scans ahead of every ordinary one at the same priority.
    ///
    /// <para><b>Nobody down, nothing touched.</b> The scan asks <see cref="Pawn.Downed"/> of each
    /// pawn first and answers no without a claim, a roll or a field written, so a colony that never
    /// fought pays one flag a pawn a think and no golden moves (<c>RescueTests</c>).</para>
    /// </summary>
    public sealed class RescueWorkGiver : WorkGiver
    {
        public override string Name => "Rescue";

        public override int WorkType => WorkTypeIndex.Rescue;

        public override bool Emergency => true;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (!pawn.IsColonist || pawn.Downed) return false;

            Pawn? best = null;
            int bestBed = -1, bestDistance = int.MaxValue;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn patient = pawns[i];
                if (patient == pawn || !RescueRules.NeedsRescue(patient, ctx)) continue;

                int distance = ctx.Distance(pawn.Cell, patient.Cell);
                if (distance >= bestDistance) continue;
                if (!ctx.Reservations.CanReserve(pawn.Id, RescueRules.PatientKey(patient))) continue;
                if (!ctx.Reachable(pawn, patient.Cell, RescueRules.Mode)) continue;
                int bed = RescueRules.BedFor(patient, pawn, ctx);
                if (bed < 0) continue;

                best = patient;
                bestBed = bed;
                bestDistance = distance;
            }

            if (best == null) return false;

            // The patient rides on the rescuer, as an attack's target does (Pawn.CombatTarget); the
            // driver's claims take it back off if they are refused.
            pawn.CombatTarget = best.Id.Value;
            job.Reset(JobIndex.Rescue);
            job.TargetCell = best.Cell;
            job.DestCell = bestBed;
            job.Mode = RescueRules.Mode;
            return true;
        }
    }
}
