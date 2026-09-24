#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The rules of treatment (design 37 §4), in one place so the doctor's giver, the patient's
    /// own mind and the job that does the work cannot come to disagree about who may be treated,
    /// by whom, or by how much.
    ///
    /// <para><b>Who is a patient.</b> A colonist below the treatment cap (80 % of her pool) who has
    /// not been treated inside the cooldown. A doctor comes to her only once she is <i>lying
    /// still</i> — downed where she fell, or in bed as a patient or asleep — because a doctor chasing
    /// a colonist who is walking about with a scratch is not a treatment, it is a pursuit.</para>
    ///
    /// <para><b>The heal.</b> The supplies' <see cref="ItemDef.healPerUnit"/>, or
    /// <see cref="CombatDef.bareHeal"/> with none; halved for treating yourself; clamped to the cap
    /// (80 %, or 60 % for yourself). Never lowers the pool: a treatment given above the cap does
    /// nothing and uses nothing.</para>
    /// </summary>
    public static class Medical
    {
        /// <summary>Is this cell the head of a bed the colony has? The list is sorted (<c>ColonyItems.AddBed</c>).</summary>
        public static bool IsBedCell(PawnContext ctx, int cell)
        {
            var beds = ctx.Items.Beds;
            int low = 0, high = beds.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (beds[mid] == cell) return true;
                if (beds[mid] < cell) low = mid + 1;
                else high = mid - 1;
            }
            return false;
        }

        /// <summary>Below the doctor's cap and outside the cooldown: a treatment would do something now.</summary>
        public static bool NeedsTreatment(Pawn pawn, PawnContext ctx) =>
            pawn.IsColonist && pawn.CarriedBy == 0
            && Below(pawn, ctx.Content.Combat.treatCapPerMille)
            && ctx.CurrentTick >= pawn.TreatedUntilTick;

        /// <summary>A patient a doctor may walk to: in need, and lying still — downed, or in bed.</summary>
        public static bool AwaitsDoctor(Pawn pawn, PawnContext ctx)
        {
            if (!NeedsTreatment(pawn, ctx)) return false;
            if (pawn.Downed) return true;
            if (!pawn.Asleep) return false;
            return IsPatient(pawn) || IsBedCell(ctx, pawn.Cell);
        }

        /// <summary>Lying down as a patient right now.</summary>
        public static bool IsPatient(Pawn pawn) =>
            pawn.CurrentJob != null && pawn.CurrentJob.DefIndex == JobIndex.Patient;

        /// <summary>Hit points below this fraction of the pool, per mille.</summary>
        public static bool Below(Pawn pawn, int perMille) =>
            (long)pawn.HpMilli * 1_000 < (long)pawn.HpMaxMilli * perMille;

        /// <summary>
        /// Could any colonist but <paramref name="patient"/> come and treat her? Standing, not
        /// broken, not drafted, Doctor switched on, and able to walk to where she is. The
        /// self-treatment fallback is the answer "no" to this and nothing else (owner, 2026-09-24).
        /// Scales with the colonists, and is asked only by a hurt colonist between jobs.
        /// </summary>
        public static bool DoctorCanReach(Pawn patient, PawnContext ctx)
        {
            var all = ctx.Pawns.All;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn doctor = all[i];
                if (doctor == patient || !doctor.IsColonist) continue;
                if (doctor.Downed || doctor.IsBroken || doctor.Drafted) continue;
                if (doctor.WorkPriority(WorkTypeIndex.Doctor) <= 0) continue;
                if (ctx.Reachable(doctor, patient.Cell)) return true;
            }

            return false;
        }

        /// <summary>
        /// The nearest supplies <paramref name="pawn"/> can reach and claim — anything whose Def
        /// heals, so a weaker item later is found by the same scan — or null.
        /// </summary>
        public static ColonyItem? NearestSupplies(Pawn pawn, PawnContext ctx)
        {
            ColonyItem? best = null;
            int bestDistance = int.MaxValue;
            var items = ctx.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                ColonyItem item = items[i];
                if (item.Despawned || item.Forbidden) continue;
                if (ctx.Content.Items[item.DefIndex].healPerUnit <= 0) continue;

                int at = ctx.WhereIs(item);
                if (at < 0) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, at);
                if (distance >= bestDistance) continue;
                if (!ctx.Reachable(pawn, at)) continue;

                bestDistance = distance;
                best = item;
            }

            return best;
        }

        /// <summary>
        /// What one treatment would leave <paramref name="patient"/> at, in thousandths: her pool
        /// plus the heal, clamped to the cap, and never below where she is.
        /// </summary>
        /// <param name="suppliesDef">The item used, or -1 for a bare dressing.</param>
        public static int HealedMilli(Pawn patient, PawnContext ctx, int suppliesDef, bool self)
        {
            CombatDef combat = ctx.Content.Combat;
            long heal = (suppliesDef >= 0 ? ctx.Content.Items[suppliesDef].healPerUnit : combat.bareHeal)
                        * (long)Rates.Scale;
            if (self) heal = heal * combat.selfHealPerMille / 1_000;

            int capPerMille = self ? combat.selfCapPerMille : combat.treatCapPerMille;
            long cap = (long)patient.HpMaxMilli * capPerMille / 1_000;

            long after = patient.HpMilli + heal;
            if (after > cap) after = cap;
            return after < patient.HpMilli ? patient.HpMilli : (int)after;
        }

        /// <summary>
        /// Apply a finished treatment: the pool, the cooldown, and — for a pawn who was down and is
        /// now past the line — getting up, at the end of the tick through the combat system, which
        /// owns that rule.
        /// </summary>
        public static void Apply(Pawn patient, PawnContext ctx, int healedMilli)
        {
            patient.HpMilli = healedMilli;
            patient.TreatedUntilTick = ctx.CurrentTick + ctx.Content.Combat.treatedCooldownTicks;

            if (!patient.Downed) return;
            if ((long)patient.HpMilli * 1_000 < (long)patient.HpMaxMilli * ctx.Content.Combat.downedRecoverAtPerMille)
                return;

            int tick = ctx.CurrentTick;
            CombatSystem? combat = ctx.Combat;
            if (combat != null) ctx.Defer(_ => { if (patient.Downed) combat.Recover(patient, tick); });
        }

        /// <summary>
        /// Is there any point in her lying down as a patient? In a bed, always: the bed heals. On
        /// the ground, only while a doctor could still come — lying in the mud heals nothing — which
        /// is also what stops her lying down and getting straight up again every tick.
        /// </summary>
        public static bool WorthLyingDown(Pawn pawn, PawnContext ctx, int bed) =>
            bed >= 0 || (ctx.CurrentTick >= pawn.TreatedUntilTick && DoctorCanReach(pawn, ctx));

        /// <summary>Would she treat herself right now, if she were up? The think node's first branch, as a question.</summary>
        public static bool WouldSelfTreat(Pawn pawn, PawnContext ctx) =>
            Below(pawn, ctx.Content.Combat.selfCapPerMille)
            && ctx.CurrentTick >= pawn.TreatedUntilTick
            && !DoctorCanReach(pawn, ctx)
            && NearestSupplies(pawn, ctx) != null;

        /// <summary>
        /// Where a hurt colonist lies down: her own bed, else the nearest free one she can reach,
        /// else -1 for the ground. The sleep's choice exactly, without the collapse rule — a
        /// patient is not exhausted, she is hurt.
        /// </summary>
        public static int BedFor(Pawn pawn, PawnContext ctx)
        {
            var beds = ctx.Items.Beds;
            int bestBed = -1, bestDistance = int.MaxValue;
            int me = pawn.Id.Value;
            for (int i = 0; i < beds.Count; i++)
            {
                int cell = beds[i];
                int owner = ctx.Construction != null ? ctx.Construction.BedOwnerAt(cell) : 0;
                if (owner != 0 && owner != me) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Cell, cell);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;
                if (!ctx.Reachable(pawn, cell)) continue;
                if (owner == me) return cell;

                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestBed = cell;
            }

            return bestBed;
        }
    }

    /// <summary>
    /// The Doctor work type's one giver (design 37 §4): the nearest patient lying still whom this
    /// colonist can reach and claim, treated with the nearest supplies — or with a bare dressing
    /// when the colony has none she can reach. An <b>emergency</b> giver, like rescue's. Never
    /// herself: that is the patient's own fallback (<see cref="PatientThinkNode"/>).
    ///
    /// <para>Scales with the colonists, and only on a think by a colonist with Doctor switched on.</para>
    /// </summary>
    public sealed class DoctorWorkGiver : WorkGiver
    {
        public override string Name => "Doctor";

        public override int WorkType => WorkTypeIndex.Doctor;

        public override bool Emergency => true;

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            Pawn? best = null;
            int bestDistance = int.MaxValue;
            var all = ctx.Pawns.All;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn patient = all[i];
                if (patient == pawn || !Medical.AwaitsDoctor(patient, ctx)) continue;

                long key = ReservationManager.Key(ReservationTargetKind.Pawn, patient.Id.Value);
                if (!ctx.Reservations.CanReserve(pawn.Id, key)) continue;

                int distance = ctx.Distance(pawn.Cell, patient.Cell);
                if (distance >= bestDistance) continue;
                if (!ctx.Reachable(pawn, patient.Cell)) continue;

                bestDistance = distance;
                best = patient;
            }

            if (best == null) return false;

            ColonyItem? supplies = Medical.NearestSupplies(pawn, ctx);
            job.Reset(JobIndex.Treat);
            job.WorkTicks = best.Id.Value;
            job.DestCell = best.Cell;
            if (supplies != null)
            {
                job.TargetItem = supplies.Id;
                job.TargetCell = ctx.WhereIs(supplies);
            }
            return true;
        }
    }

    /// <summary>
    /// A hurt colonist's own mind (design 37 §4), between the needs and the work: under the
    /// self-treatment cap, with supplies and no doctor who could come, she treats herself; under
    /// half her pool, she goes to bed as a patient. Declines for anybody else, which costs a
    /// comparison a think.
    /// </summary>
    public sealed class PatientThinkNode : ThinkNode
    {
        public override string Name => "Patient";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            if (!pawn.IsColonist || pawn.Downed || pawn.Asleep || pawn.IsBroken || pawn.Drafted) return false;
            CombatDef combat = ctx.Content.Combat;
            if (!Medical.Below(pawn, combat.treatCapPerMille)) return false;

            if (Medical.Below(pawn, combat.selfCapPerMille)
                && ctx.CurrentTick >= pawn.TreatedUntilTick
                && !Medical.DoctorCanReach(pawn, ctx))
            {
                // NearestSupplies is asked here rather than through WouldSelfTreat so the scan runs once.
                ColonyItem? supplies = Medical.NearestSupplies(pawn, ctx);
                if (supplies != null)
                {
                    job.Reset(JobIndex.Treat);
                    job.WorkTicks = pawn.Id.Value;
                    job.DestCell = -1;
                    job.TargetItem = supplies.Id;
                    job.TargetCell = ctx.WhereIs(supplies);
                    return true;
                }
            }

            if (!Medical.Below(pawn, combat.patientBelowPerMille)) return false;

            int bed = Medical.BedFor(pawn, ctx);
            if (!Medical.WorthLyingDown(pawn, ctx, bed)) return false;

            job.Reset(JobIndex.Patient);
            job.TargetCell = bed;
            return true;
        }
    }

    /// <summary>
    /// <c>Job_Treat</c> (design 37 §4): fetch one unit of supplies if the job names any, walk to
    /// the patient, and work until the treatment is done; then heal her. Treating herself, a
    /// colonist does it where she took the supplies up, at three times the work.
    ///
    /// <para>The patient is <see cref="Job.WorkTicks"/> — a <see cref="PawnId"/> value, saved and
    /// hashed with the job, the field every driver uses for its own purpose. The supplies are taken
    /// at the grasp and used on the heal; a treatment that fails in between puts the unit down
    /// again rather than using it (<see cref="Cleanup"/>).</para>
    /// </summary>
    public class TreatJobDriver : JobDriver
    {
        const int ToilFetch = 0, ToilLift = 1, ToilGo = 2, ToilTreat = 3;

        public override int WorkType => WorkTypeIndex.Doctor;

        public override int WorkFocus =>
            ToilIndex == ToilTreat && Job.DestCell >= 0 ? Job.DestCell : -1;

        bool Self => Job.WorkTicks == Pawn.Id.Value;

        public override void Begin(Pawn pawn, Job job)
        {
            base.Begin(pawn, job);
            // No supplies named: a bare dressing, so nothing to fetch.
            if (job.TargetItem == ThingId.None) ToilIndex = ToilGo;
        }

        public override bool TryMakeReservations(PawnContext ctx)
        {
            if (Job.TargetItem != ThingId.None)
            {
                var item = ctx.Items.Get(Job.TargetItem);
                if (item == null || ctx.WhereIs(item) < 0) return false;
                long itemKey = ReservationManager.Key(ReservationTargetKind.Item, Job.TargetItem.Value);
                if (!ctx.Reservations.Reserve(Pawn.Id, itemKey)) return false;
                Pawn.HeldReservations.Add(itemKey);
            }

            long patientKey = ReservationManager.Key(ReservationTargetKind.Pawn, Job.WorkTicks);
            if (!ctx.Reservations.Reserve(Pawn.Id, patientKey)) return false;
            Pawn.HeldReservations.Add(patientKey);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            Pawn? patient = ctx.Pawns.Get(new PawnId(Job.WorkTicks));
            if (patient == null || !Medical.NeedsTreatment(patient, ctx)) return JobStatus.Failed;
            if (!Self && !Medical.AwaitsDoctor(patient, ctx)) return JobStatus.Failed;

            switch (ToilIndex)
            {
                case ToilFetch:
                {
                    var item = ctx.Items.Get(Job.TargetItem);
                    if (item == null || !StillAt(ctx, item, Job.TargetCell)) return JobStatus.Failed;
                    JobStatus walk = GotoCell(ctx, Job.TargetCell);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                case ToilLift:
                    return Lift(ctx);

                case ToilGo:
                {
                    if (Self)
                    {
                        NextToil();
                        return JobStatus.Ongoing;
                    }

                    // Beside her rather than on her: she is lying in the cell.
                    Job.DestCell = patient.Cell;
                    int stand = FellJobDriver.StandBeside(ctx, Pawn, patient.Cell);
                    if (stand < 0) stand = patient.Cell;
                    if (StillInReach(ctx, Pawn, patient.Cell, 0, 0))
                    {
                        Pawn.ClearPath();
                        Pawn.Destination = -1;
                        NextToil();
                        return JobStatus.Ongoing;
                    }

                    JobStatus walk = GotoCell(ctx, stand);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                default:
                {
                    if (!Self && !StillInReach(ctx, Pawn, patient.Cell, 0, 0))
                    {
                        ToilIndex = ToilGo;
                        ToilProgress = 0;
                        return JobStatus.Ongoing;
                    }

                    CombatDef combat = ctx.Content.Combat;
                    int work = ctx.Content.Jobs[Job.DefIndex].workTicks * (Self ? combat.selfWorkFactor : 1);
                    ToilProgress += Pawn.WorkRatePerMille(WorkTypeIndex.Doctor);
                    Work(ctx);
                    if ((long)ToilProgress < (long)work * Rates.Scale) return JobStatus.Ongoing;

                    ColonyItem? carried = Job.CarriedItem >= 0 ? ctx.Items.Get(new ThingId(Job.CarriedItem)) : null;
                    int suppliesDef = carried != null ? carried.DefIndex : -1;
                    if (Job.TargetItem != ThingId.None && carried == null) return JobStatus.Failed;

                    Medical.Apply(patient, ctx, Medical.HealedMilli(patient, ctx, suppliesDef, Self));
                    if (carried != null)
                    {
                        ctx.Items.Despawn(carried);
                        Job.CarriedItem = -1;
                    }
                    return JobStatus.Succeeded;
                }
            }
        }

        /// <summary>
        /// <see cref="LiftToil"/>'s motion and timing, taking one unit rather than the stack: a
        /// doctor carries a box to the patient and leaves the rest of the pile where it was.
        /// </summary>
        JobStatus Lift(PawnContext ctx)
        {
            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null) return JobStatus.Failed;

            int total = ctx.Content.LiftTicks * Rates.Scale;
            int grasp = ctx.Content.LiftGraspTicks * Rates.Scale;
            if (grasp > total) grasp = total;
            if (grasp < 1) grasp = 1;

            if (ToilProgress == 0) Pawn.BeginGesture(PawnGesture.Lift);
            int elapsed = ToilProgress += Rates.Scale;

            if (elapsed < grasp) return AtHand(ctx, item) ? JobStatus.Ongoing : JobStatus.Failed;

            if (elapsed == grasp)
            {
                if (!AtHand(ctx, item)) return JobStatus.Failed;
                ColonyItem taken = ctx.Items.SplitOff(item, 1, Pawn.Id);
                Job.CarriedItem = taken.Id.Value;
            }

            if (elapsed < total) return JobStatus.Ongoing;
            NextToil();
            return JobStatus.Ongoing;
        }

        public override void Cleanup(PawnContext ctx, JobStatus status) => DropCarried(ctx);
    }

    /// <summary>
    /// <c>Job_Patient</c> (design 37 §4): walk to the bed <see cref="PatientThinkNode"/> chose — or
    /// lie down where she is when there is none — and sleep there until she is back to the
    /// release line. Asleep, so rest runs and so the bed's heal (<c>CombatSystem.Heal</c>, which
    /// asks for a pawn down or asleep on a bed) runs too; and asleep in a bed is what a doctor
    /// walks to.
    ///
    /// <para><b>On the ground she stays only while treatment can still come</b>: once she has been
    /// treated and nothing more can happen until the cooldown runs out, lying in the mud heals
    /// nothing, so she gets up and the next think decides again.</para>
    /// </summary>
    public class PatientJobDriver : JobDriver
    {
        /// <summary>How often a lying patient asks whether lying there still helps, in ticks.</summary>
        public const int RecheckTicks = 250;

        public override bool TryMakeReservations(PawnContext ctx)
        {
            if (Job.TargetCell < 0) return true;
            long key = ReservationManager.Key(ReservationTargetKind.Cell, Job.TargetCell);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            CombatDef combat = ctx.Content.Combat;
            if (!Medical.Below(Pawn, combat.patientReleasePerMille)) return JobStatus.Succeeded;

            if (ToilIndex == 0)
            {
                if (Job.TargetCell < 0 || Pawn.Cell == Job.TargetCell)
                {
                    if (Job.TargetCell >= 0) ctx.Construction?.TryClaimForSleeper(Job.TargetCell, Pawn.Id);
                    NextToil();
                    return JobStatus.Ongoing;
                }

                JobStatus walk = GotoCell(ctx, Job.TargetCell);
                if (walk == JobStatus.Succeeded)
                {
                    ctx.Construction?.TryClaimForSleeper(Job.TargetCell, Pawn.Id);
                    NextToil();
                }
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            // Hunger gets her up: nothing else interrupts a job, and a patient is in bed for days.
            if (Pawn.Needs[NeedIndex.Food] < ctx.Content.Needs[NeedIndex.Food].seekThreshold)
                return JobStatus.Succeeded;

            // Now and then, not every tick: both questions scan the colony.
            if (ToilProgress % (RecheckTicks * Rates.Scale) == 0)
            {
                if (!Medical.WorthLyingDown(Pawn, ctx, Job.TargetCell)) return JobStatus.Succeeded;
                if (Medical.WouldSelfTreat(Pawn, ctx)) return JobStatus.Succeeded;
            }

            Pawn.Asleep = true;
            ToilProgress += Rates.Scale;
            return JobStatus.Ongoing;
        }

        public override void Cleanup(PawnContext ctx, JobStatus status) => Pawn.Asleep = false;
    }
}
