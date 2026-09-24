#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    // OrderRescue (design 33 §11). Its own partial file so the lane that writes it edits no file
    // another lane owns: the C4 lane (docs/plans/combat-contracts.md).
    public sealed partial class JobSystem
    {
        /// <summary>
        /// Send a drafted colonist (<c>A</c>) to carry a downed one (<c>B</c>) to her own bed, else
        /// the nearest free one (§11b). Refused for anybody but a drafted, standing, unbroken
        /// colonist; for a patient who is not a downed colonist lying where she fell; for a patient
        /// somebody is already coming for; and when there is no bed to take her to — the owner's
        /// "leave her", which the patient's <c>rescue.nobed</c> explains (§11d).
        /// </summary>
        public IntentRejection HandleOrderRescue(Intent intent)
        {
            Pawn? pawn = _ctx.Pawns.Get(new PawnId(intent.A));
            if (pawn == null || !pawn.IsColonist || !pawn.Drafted || pawn.Downed || pawn.IsBroken)
                return IntentRejection.NotPermitted;
            if (intent.B == 0) return IntentRejection.NotPermitted;

            Pawn? patient = _ctx.Pawns.Get(new PawnId(intent.B));
            if (patient == null || patient == pawn) return IntentRejection.NotPermitted;
            if (pawn.CurrentJob?.DefIndex == JobIndex.Rescue && pawn.CombatTarget == patient.Id.Value)
                return IntentRejection.AlreadyInThatState;

            if (!RescueRules.NeedsRescue(patient, _ctx)) return IntentRejection.NotPermitted;
            if (!_ctx.Reservations.CanReserve(pawn.Id, RescueRules.PatientKey(patient))) return IntentRejection.NotPermitted;
            if (!_ctx.Reachable(pawn, patient.Cell, RescueRules.Mode)) return IntentRejection.NotPermitted;
            int bed = RescueRules.BedFor(patient, pawn, _ctx);
            if (bed < 0) return IntentRejection.NotPermitted;

            int tick = IntentTick;
            pawn.DraftQuietSinceTick = tick;
            Interrupt(pawn, JobStatus.Failed);

            pawn.CombatTarget = patient.Id.Value;
            Job job = pawn.JobBuffer;
            job.Reset(JobIndex.Rescue);
            job.TargetCell = patient.Cell;
            job.DestCell = bed;
            job.Mode = RescueRules.Mode;
            job.PlayerForced = true;
            return StartJob(pawn, job, tick) ? IntentRejection.None : IntentRejection.NotPermitted;
        }
    }
}
