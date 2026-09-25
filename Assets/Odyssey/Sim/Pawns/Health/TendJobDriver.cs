#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_Tend</c> (design 43 §5): fetch a medkit if the job names one, walk to the patient,
    /// work over her, and tend every injury she has at one quality — every bleed stops, whatever the
    /// quality (a-02:36).
    ///
    /// <para><b>The toil index says whether a kit is in hand</b>, so the job record needs no field
    /// of its own and nothing new is saved: toils 0 and 1 fetch and take the kit, 2 and 3 walk and
    /// tend with it, and a job begun with no kit starts at 4 and tends bare-handed at 5. The index
    /// is saved and hashed already.</para>
    ///
    /// <para><b>The kit is spent at the pick-up</b>, as a meal is at the table: one out of the stack,
    /// on the grasp. A tend abandoned after that spends the kit, which is the honest cost of a
    /// doctor called away and is cheaper than a carried thing nothing draws.</para>
    ///
    /// <para><b>The patient is the doctor's <see cref="Pawn.CombatTarget"/></b>, as a rescue's is,
    /// and claimed under the same key: somebody already coming for her — to carry her or to tend
    /// her — is enough.</para>
    /// </summary>
    public class TendJobDriver : JobDriver
    {
        const int FetchKit = 0, TakeKit = 1, WalkWithKit = 2, TendWithKit = 3, WalkBare = 4, TendBare = 5;

        Pawn? Patient(PawnContext ctx) =>
            Pawn.CombatTarget == 0 ? null : ctx.Pawns.Get(new PawnId(Pawn.CombatTarget));

        public override int WorkType => WorkTypeIndex.Doctor;

        /// <summary>The patient's cell while working over her; nothing on the walks and the fetch.</summary>
        public override int WorkFocus =>
            (ToilIndex == TendWithKit || ToilIndex == TendBare) && _patientCell >= 0 ? _patientCell : -1;

        // Where she lies, as of the last working tick: what the figure faces. Presentation only,
        // derived every tick from the saved patient, so neither saved nor hashed.
        int _patientCell = -1;

        public override bool TryMakeReservations(PawnContext ctx)
        {
            Pawn? patient = Patient(ctx);
            if (patient == null) return true;

            long patientKey = RescueRules.PatientKey(patient);
            ColonyItem? kit = Job.TargetItem.IsValid ? ctx.Items.Get(Job.TargetItem) : null;
            long kitKey = kit != null ? TendRules.KitKey(kit) : 0;

            // Every question first, then every claim.
            if (!ctx.Reservations.CanReserve(Pawn.Id, patientKey)
                || (kit != null && !ctx.Reservations.CanReserve(Pawn.Id, kitKey)))
            {
                Pawn.CombatTarget = 0;
                return false;
            }

            ctx.Reservations.Reserve(Pawn.Id, patientKey);
            Pawn.HeldReservations.Add(patientKey);
            if (kit != null)
            {
                ctx.Reservations.Reserve(Pawn.Id, kitKey);
                Pawn.HeldReservations.Add(kitKey);
            }
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            Pawn? patient = Patient(ctx);
            // Dead, carried off, or nothing left to tend: there is nobody to work over.
            if (patient == null || Melee.IsDead(patient) || patient.CarriedBy != 0) return JobStatus.Failed;
            // Under attack herself: the tend waits, and her own response to danger takes over.
            if (TendRules.UnderAttack(ctx, Pawn)) return JobStatus.Failed;
            if (patient.Health == null || patient.Health.UntendedCount == 0) return JobStatus.Succeeded;

            if (ToilIndex == FetchKit && !Job.TargetItem.IsValid) ToilIndex = WalkBare;

            switch (ToilIndex)
            {
                case FetchKit:
                {
                    ColonyItem? kit = ctx.Items.Get(Job.TargetItem);
                    if (kit == null || !StillAt(ctx, kit, Job.TargetCell)) return JobStatus.Failed;
                    JobStatus walk = GotoCell(ctx, Job.TargetCell);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                case TakeKit:
                {
                    ColonyItem? kit = ctx.Items.Get(Job.TargetItem);
                    int total = ctx.Content.LiftTicks * Rates.Scale;
                    int grasp = ctx.Content.LiftGraspTicks * Rates.Scale;
                    if (grasp > total) grasp = total;
                    if (grasp < 1) grasp = 1;
                    if (ToilProgress == 0) Pawn.BeginGesture(PawnGesture.Lift);
                    int elapsed = ToilProgress += Rates.Scale;
                    if (elapsed == grasp)
                    {
                        if (kit == null || !AtHand(ctx, kit)) return JobStatus.Failed;
                        // One out of the stack, as a meal is eaten from a pile.
                        if (kit.Stack > 1) kit.Stack--;
                        else ctx.Items.Despawn(kit);
                    }
                    if (elapsed < total) return JobStatus.Ongoing;
                    NextToil();
                    return JobStatus.Ongoing;
                }

                case WalkWithKit:
                case WalkBare:
                {
                    if (Beside(ctx, patient))
                    {
                        Stop();
                        NextToil();
                        return JobStatus.Ongoing;
                    }
                    return Follow(ctx, patient);
                }

                case TendWithKit:
                case TendBare:
                {
                    // Beside her, or following her: a patient still on her feet may walk on while
                    // she is worked over, and the work done so far is kept rather than thrown away
                    // (the progress is this toil's, and following does not leave it).
                    if (!Beside(ctx, patient)) return Follow(ctx, patient);
                    Stop();
                    _patientCell = patient.Cell;

                    Work(ctx);
                    ToilProgress += Pawn.WorkRatePerMille(WorkTypeIndex.Doctor);
                    if (ToilProgress < ctx.Content.Jobs[Job.DefIndex].workTicks * Rates.Scale) return JobStatus.Ongoing;

                    HealthDef? body = patient.Body;
                    if (body == null) return JobStatus.Failed;
                    int quality = body.TendQualityPerMille(Pawn.SkillLevel(SkillIndex.Medicine), ToilIndex == TendWithKit);
                    ctx.Combat?.Tend(patient, quality);
                    return JobStatus.Succeeded;
                }

                default:
                    return JobStatus.Succeeded;
            }
        }

        /// <summary>
        /// Standing beside her to work: in reach, <b>never on her own cell</b> — a patient lies in
        /// a bed, and a doctor who stood on it was a doctor who fell asleep in it (found by
        /// <c>RescueTests</c>) — and never on a cell somebody in a fight holds (design 33 §7c,
        /// found by <c>FightGuardTests.EveryMindAtOnce</c>).
        /// </summary>
        bool Beside(PawnContext ctx, Pawn patient) =>
            Pawn.Cell != patient.Cell
            && Melee.InReach(ctx, Pawn, patient, Job.Mode)
            && !Melee.Holds(ctx, Pawn, Pawn.Cell);

        /// <summary>
        /// Walk to a side of her — <see cref="Melee.ChooseSide"/>, the fight's own rule for a free
        /// cell beside somebody — choosing again only once she has moved away from the one in hand
        /// or a fighter has taken it. Re-aiming on every step she took threw the walk away each
        /// time (the shared walk toil clears the path when the destination changes), and a doctor
        /// chasing a colonist on her feet never arrived — found by <c>TendTests</c>.
        /// </summary>
        JobStatus Follow(PawnContext ctx, Pawn patient)
        {
            if (Job.DestCell < 0 || !SideCellOf(ctx, Job.DestCell, patient.Cell) || Melee.Holds(ctx, Pawn, Job.DestCell))
                Job.DestCell = Melee.ChooseSide(ctx, Pawn, patient, Job.Mode);
            if (Job.DestCell < 0) return JobStatus.Failed;
            JobStatus walk = GotoCell(ctx, Job.DestCell);
            return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
        }

        /// <summary>A cell within one of hers on her layer, and not hers.</summary>
        static bool SideCellOf(PawnContext ctx, int cell, int of)
        {
            if (cell == of) return false;
            CellRef a = ctx.Size.FromIndex(cell), b = ctx.Size.FromIndex(of);
            return a.Y == b.Y && System.Math.Abs(a.X - b.X) <= 1 && System.Math.Abs(a.Z - b.Z) <= 1;
        }

        /// <summary>Standing to work: whatever walk was in hand is over.</summary>
        void Stop()
        {
            if (Pawn.Destination < 0 && !Pawn.HasPath) return;
            Pawn.ClearPath();
            Pawn.Destination = -1;
        }

        public override void Cleanup(PawnContext ctx, JobStatus status) => Pawn.CombatTarget = 0;
    }
}
