#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    public partial class JobSystem
    {
        /// <summary>
        /// The right-click's Tend (design 43 §11): one colonist sent to tend another, drafted or
        /// not, as Equip is. The same job the Doctor work type gives — a medkit fetched if one can
        /// be reached, the patient worked over from a free side — started as a player's order, so
        /// nothing casual takes it away. Refused for a doctor who is down or broken, a patient with
        /// nothing untended, herself, or a patient nobody can reach or stand beside.
        /// </summary>
        public IntentRejection HandleOrderTend(Intent intent)
        {
            Pawn? doctor = _ctx.Pawns.Get(new PawnId(intent.A));
            if (doctor == null || !doctor.IsColonist || doctor.Downed || doctor.IsBroken) return IntentRejection.NotPermitted;

            Pawn? patient = intent.B == 0 ? null : _ctx.Pawns.Get(new PawnId(intent.B));
            if (patient == null || patient == doctor) return IntentRejection.NotPermitted;
            if (doctor.CurrentJob?.DefIndex == JobIndex.Tend && doctor.CombatTarget == patient.Id.Value)
                return IntentRejection.AlreadyInThatState;

            if (!TendRules.NeedsTending(patient)) return IntentRejection.NotPermitted;
            if (!_ctx.Reservations.CanReserve(doctor.Id, RescueRules.PatientKey(patient))) return IntentRejection.NotPermitted;
            if (!_ctx.Reachable(doctor, patient.Cell, doctor.OwnMode)) return IntentRejection.NotPermitted;
            if (Melee.ChooseSide(_ctx, doctor, patient, doctor.OwnMode) < 0) return IntentRejection.NotPermitted;

            int tick = IntentTick;
            if (doctor.Drafted) doctor.DraftQuietSinceTick = tick;
            Interrupt(doctor, JobStatus.Failed);

            ColonyItem? kit = TendRules.NearestMedkit(doctor, _ctx);
            doctor.CombatTarget = patient.Id.Value;
            Job job = doctor.JobBuffer;
            job.Reset(JobIndex.Tend);
            job.TargetItem = kit != null ? kit.Id : ThingId.None;
            job.TargetCell = kit != null ? _ctx.WhereIs(kit) : -1;
            job.DestCell = patient.Cell;
            job.Mode = doctor.OwnMode;
            job.PlayerForced = true;
            return StartJob(doctor, job, tick) ? IntentRejection.None : IntentRejection.NotPermitted;
        }
    }
}
