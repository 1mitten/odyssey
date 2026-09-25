#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    public partial class JobSystem
    {
        /// <summary>
        /// The right-click's Tend (design 43 §11): one colonist sent to treat another, drafted or
        /// not, as Equip is. The same <c>Job_Treat</c> the Doctor work type gives (design 37) —
        /// medical supplies fetched if any can be reached, a bare dressing if not, the tend at the
        /// end — started as a player's order, so nothing casual takes it away and the patient need
        /// not be lying still: a drafted soldier bleeding where she stands can be sent a doctor.
        ///
        /// <para>Refused for a doctor who is down or broken, herself, or a patient nobody can reach
        /// or for whom a treatment would do nothing at all (<see cref="Medical.WorthAnOrder"/>).</para>
        /// </summary>
        public IntentRejection HandleOrderTend(Intent intent)
        {
            Pawn? doctor = _ctx.Pawns.Get(new PawnId(intent.A));
            if (doctor == null || !doctor.IsColonist || doctor.Downed || doctor.IsBroken) return IntentRejection.NotPermitted;

            Pawn? patient = intent.B == 0 ? null : _ctx.Pawns.Get(new PawnId(intent.B));
            if (patient == null || patient == doctor) return IntentRejection.NotPermitted;
            if (doctor.CurrentJob?.DefIndex == JobIndex.Treat && doctor.CurrentJob.WorkTicks == patient.Id.Value)
                return IntentRejection.AlreadyInThatState;

            if (!Medical.WorthAnOrder(patient, _ctx)) return IntentRejection.NotPermitted;
            long patientKey = ReservationManager.Key(ReservationTargetKind.Pawn, patient.Id.Value);
            if (!_ctx.Reservations.CanReserve(doctor.Id, patientKey)) return IntentRejection.NotPermitted;
            if (!_ctx.Reachable(doctor, patient.Cell)) return IntentRejection.NotPermitted;
            if (FellJobDriver.StandBeside(_ctx, doctor, patient.Cell) < 0) return IntentRejection.NotPermitted;

            int tick = IntentTick;
            if (doctor.Drafted) doctor.DraftQuietSinceTick = tick;
            Interrupt(doctor, JobStatus.Failed);

            ColonyItem? supplies = Medical.NearestSupplies(doctor, _ctx);
            Job job = doctor.JobBuffer;
            job.Reset(JobIndex.Treat);
            job.WorkTicks = patient.Id.Value;
            job.DestCell = patient.Cell;
            if (supplies != null)
            {
                job.TargetItem = supplies.Id;
                job.TargetCell = _ctx.WhereIs(supplies);
            }
            job.PlayerForced = true;
            return StartJob(doctor, job, tick) ? IntentRejection.None : IntentRejection.NotPermitted;
        }
    }
}
