#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_Rescue</c> (design 33 §11a): walk to a downed colonist, lift her
    /// (<see cref="Pawn.CarriedBy"/>), carry her to her own bed or the nearest free one
    /// (<see cref="Job.DestCell"/>, chosen by <see cref="RescueRules.BedFor"/>), and lay her in it.
    ///
    /// <para><b>The patient is the rescuer's <see cref="Pawn.CombatTarget"/></b>, which is saved and
    /// hashed; a driver's own fields are not, and a job field would be a save-format bump. The
    /// toils are timed like an item's lift and put-down (<c>LiftTicks</c>, the
    /// <see cref="PawnGesture.Lift"/> and <see cref="PawnGesture.Stow"/> gestures), so the stoop the
    /// figures already draw is the one they draw here.</para>
    ///
    /// <para><b>Nobody is left in a pair of arms.</b> Every end that is not the lay runs
    /// <see cref="Cleanup"/>, which sets a carried patient down where her carrier stands.</para>
    /// </summary>
    public class RescueJobDriver : JobDriver
    {
        Pawn? Patient(PawnContext ctx) =>
            Pawn.CombatTarget == 0 ? null : ctx.Pawns.Get(new PawnId(Pawn.CombatTarget));

        /// <summary>
        /// The patient and her bed, both or neither — every question first, then every claim. A
        /// job started on nobody claims nothing and fails on its first tick, as every combat driver
        /// does (<c>CombatContractTests</c>); a claim refused takes the order's target with it, so
        /// a failed start leaves no combat state behind.
        /// </summary>
        public override bool TryMakeReservations(PawnContext ctx)
        {
            Pawn? patient = Patient(ctx);
            if (patient == null || Job.DestCell < 0) return true;

            long patientKey = RescueRules.PatientKey(patient);
            long bedKey = RescueRules.BedKey(Job.DestCell);
            if (!ctx.Reservations.CanReserve(Pawn.Id, patientKey) || !ctx.Reservations.CanReserve(Pawn.Id, bedKey))
            {
                Pawn.CombatTarget = 0;
                return false;
            }

            ctx.Reservations.Reserve(Pawn.Id, patientKey);
            Pawn.HeldReservations.Add(patientKey);
            ctx.Reservations.Reserve(Pawn.Id, bedKey);
            Pawn.HeldReservations.Add(bedKey);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            Pawn? patient = Patient(ctx);
            // Dead, or up on her own: there is nobody to carry.
            if (patient == null || !patient.Downed || Melee.IsDead(patient)) return JobStatus.Failed;

            switch (ToilIndex)
            {
                case 0:
                {
                    // To where she lies, following her if she is moved before she is reached.
                    if (patient.CarriedBy != 0) return JobStatus.Failed;
                    if (patient.Cell != Job.TargetCell) Job.TargetCell = patient.Cell;
                    JobStatus walk = GotoCell(ctx, Job.TargetCell);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                case 1:
                {
                    // The lift: bent over her until the grasp, then she is in the arms.
                    if (patient.CarriedBy == 0 && patient.Cell != Pawn.Cell) return JobStatus.Failed;
                    int total = ctx.Content.LiftTicks * Rates.Scale;
                    int grasp = ctx.Content.LiftGraspTicks * Rates.Scale;
                    if (grasp > total) grasp = total;
                    if (grasp < 1) grasp = 1;

                    if (ToilProgress == 0) Pawn.BeginGesture(PawnGesture.Lift);
                    int elapsed = ToilProgress += Rates.Scale;
                    if (elapsed == grasp)
                    {
                        patient.CarriedBy = Pawn.Id.Value;
                        patient.ClearPath();
                        patient.Destination = -1;
                    }
                    if (elapsed < total) return JobStatus.Ongoing;
                    NextToil();
                    return JobStatus.Ongoing;
                }

                case 2:
                {
                    // Carried to the bed. The bed is asked again every tick: taken up, given to
                    // somebody else or lain in since the choice, and the carry ends here.
                    if (patient.CarriedBy != Pawn.Id.Value) return JobStatus.Failed;
                    if (!StillFree(ctx, patient)) return JobStatus.Failed;
                    patient.Cell = Pawn.Cell;
                    JobStatus walk = GotoCell(ctx, Job.DestCell);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                case 3:
                {
                    // Laid down, and the bed passes to her (§11c).
                    if (patient.CarriedBy != Pawn.Id.Value) return JobStatus.Failed;
                    if (ToilProgress == 0) Pawn.BeginGesture(PawnGesture.Stow);
                    ToilProgress += Rates.Scale;
                    if (ToilProgress < ctx.Content.LiftTicks * Rates.Scale) return JobStatus.Ongoing;
                    Lay(ctx, patient);
                    return JobStatus.Succeeded;
                }

                default:
                    return JobStatus.Succeeded;
            }
        }

        /// <summary>The bed is still a bed, still hers or nobody's, and nobody else lies in it.</summary>
        bool StillFree(PawnContext ctx, Pawn patient)
        {
            int bed = Job.DestCell;
            if (!RescueRules.IsBed(ctx, bed)) return false;
            int owner = ctx.Construction?.BedOwnerAt(bed) ?? 0;
            if (owner != 0 && owner != patient.Id.Value) return false;
            return ctx.Reservations.IsReservedBy(Pawn.Id, RescueRules.BedKey(bed));
        }

        /// <summary>
        /// Set her on the bed's head cell, out of the arms, holding its reservation herself: the
        /// rescuer's claim ends with this job, and hers must last until she gets up (§11c).
        /// </summary>
        void Lay(PawnContext ctx, Pawn patient)
        {
            int bed = Job.DestCell;
            patient.CarriedBy = 0;
            patient.ClearPath();
            patient.Destination = -1;
            patient.Cell = bed;

            long key = RescueRules.BedKey(bed);
            ctx.Reservations.Release(Pawn.Id, key);
            Pawn.HeldReservations.Remove(key);
            if (ctx.Reservations.Reserve(patient.Id, key)) patient.HeldReservations.Add(key);
        }

        public override void Cleanup(PawnContext ctx, JobStatus status)
        {
            Pawn? patient = Patient(ctx);
            if (patient != null && patient.CarriedBy == Pawn.Id.Value) RescueRules.SetDown(ctx, patient, Pawn.Cell);
            Pawn.CombatTarget = 0;
        }
    }
}
