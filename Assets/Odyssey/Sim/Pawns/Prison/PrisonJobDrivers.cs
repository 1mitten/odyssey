#nullable enable

using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    // The prisoner line's drivers (design 58 §7), JobHandle 28 to 35, claimed together by the
    // contracts step (P3) so that every table a save depends on is extended once. Each is a stub
    // that fails the tick it starts until its own unit writes it, so nothing can run one yet.

    /// <summary>
    /// <c>Job_Capture</c> (design 58 §7): the rescue's walk, lift, carry and lay — the same stoop,
    /// the same cradle, the same set-down on every other end — to a prison bed rather than a
    /// colony one, and on the lay she is taken: into custody, given the bed, dressed for the cell.
    /// A subclass rather than a copy, so the carry has one owner.
    /// </summary>
    public class CaptureJobDriver : RescueJobDriver
    {
        /// <summary>Still a prison bed, still hers or nobody's, and still claimed by the carrier.</summary>
        protected override bool StillFree(PawnContext ctx, Pawn patient)
        {
            int bed = Job.DestCell;
            if (!RescueRules.IsBed(ctx, bed)) return false;
            if (!BedRule.MayUse(BedUser.Prisoner, patient.Id.Value, BedRules.PurposeAt(ctx, bed), BedRules.OwnerAt(ctx, bed)))
                return false;
            return ctx.Reservations.IsReservedBy(Pawn.Id, RescueRules.BedKey(bed));
        }

        /// <summary>Laid in the bed, then taken: custody, the bed hers, the jumpsuit on.</summary>
        protected override void Lay(PawnContext ctx, Pawn patient)
        {
            base.Lay(ctx, patient);
            CustodyRules.Take(patient, ctx);
            ctx.Construction?.AssignOwnerAt(Job.DestCell, patient.Id.Value);
            PrisonRecord record = patient.Prison ??= new PrisonRecord();
            record.Dressed = true;
        }
    }

    /// <summary>
    /// <c>Job_FeedPrisoner</c> (design 58 §7): fetch one portion of food, carry it to a prisoner and
    /// feed her there — through her door, to her bed if she is shackled or down. The doctor's shape
    /// (<see cref="TreatJobDriver"/>): one unit off the pile, not the pile, and a carried thing is
    /// put down by <see cref="JobDriver.DropCarried"/> on every other end. <see cref="Job.WorkTicks"/>
    /// names the prisoner, as it names the patient there, so no job field is added.
    /// </summary>
    public class FeedPrisonerJobDriver : JobDriver
    {
        const int ToilFetch = 0, ToilLift = 1, ToilGo = 2, ToilFeed = 3;

        /// <summary>How long a feeding takes at her side, in ticks: a meal held to her, not a banquet.</summary>
        public const int FeedTicks = 120;

        public override bool TryMakeReservations(PawnContext ctx)
        {
            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null || ctx.WhereIs(item) < 0) return false;
            long itemKey = ReservationManager.Key(ReservationTargetKind.Item, Job.TargetItem.Value);
            long prisonerKey = ReservationManager.Key(ReservationTargetKind.Pawn, Job.WorkTicks);
            if (!ctx.Reservations.CanReserve(Pawn.Id, itemKey) || !ctx.Reservations.CanReserve(Pawn.Id, prisonerKey))
                return false;
            ctx.Reservations.Reserve(Pawn.Id, itemKey);
            Pawn.HeldReservations.Add(itemKey);
            ctx.Reservations.Reserve(Pawn.Id, prisonerKey);
            Pawn.HeldReservations.Add(prisonerKey);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            Pawn? prisoner = ctx.Pawns.Get(new PawnId(Job.WorkTicks));
            if (prisoner == null || prisoner.Custody != PawnCustody.Prisoner || prisoner.CarriedBy != 0)
                return JobStatus.Failed;

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
                {
                    var item = ctx.Items.Get(Job.TargetItem);
                    if (item == null) return JobStatus.Failed;
                    int total = ctx.Content.LiftTicks * Rates.Scale;
                    int grasp = System.Math.Max(1, System.Math.Min(total, ctx.Content.LiftGraspTicks * Rates.Scale));
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

                case ToilGo:
                {
                    // Beside her, never on her: she may be lying in the cell.
                    if (Pawn.Cell != prisoner.Cell && StillInReach(ctx, Pawn, prisoner.Cell, 0, 0))
                    {
                        if (Pawn.Destination >= 0 || Pawn.HasPath) { Pawn.ClearPath(); Pawn.Destination = -1; }
                        NextToil();
                        return JobStatus.Ongoing;
                    }
                    // A free cell beside her, chosen once and kept while it is still beside her —
                    // the doctor's rule (design 43 §14c), or a warden following her never arrives.
                    int stand = Job.DestCell;
                    if (stand < 0 || stand == prisoner.Cell || !Beside(ctx.Size, stand, prisoner.Cell))
                        Job.DestCell = stand = FellJobDriver.StandBeside(ctx, Pawn, prisoner.Cell);
                    if (stand < 0) return JobStatus.Failed;
                    return GotoCell(ctx, stand) == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                default:
                {
                    ColonyItem? carried = Job.CarriedItem >= 0 ? ctx.Items.Get(new ThingId(Job.CarriedItem)) : null;
                    if (carried == null) return JobStatus.Failed;
                    ToilProgress += Rates.Scale;
                    if (ToilProgress < FeedTicks * Rates.Scale) return JobStatus.Ongoing;

                    ItemDef food = ctx.Content.Items[carried.DefIndex];
                    int max = ctx.Content.Needs[NeedIndex.Food].max;
                    prisoner.Needs[NeedIndex.Food] = System.Math.Min(max, prisoner.Needs[NeedIndex.Food] + food.nutrition);
                    int thought = food.ateThought;
                    if ((uint)thought < (uint)ctx.Content.Thoughts.Length) prisoner.AddMemory(thought, ctx.CurrentTick);
                    ctx.Items.Despawn(carried);
                    Job.CarriedItem = -1;
                    ctx.Kitchen?.Invalidate();
                    return JobStatus.Succeeded;
                }
            }
        }

        public override void Cleanup(PawnContext ctx, JobStatus status) => DropCarried(ctx);

        static bool Beside(GridSize size, int cell, int of)
        {
            if (cell == of) return false;
            CellRef a = size.FromIndex(cell), b = size.FromIndex(of);
            return a.Y == b.Y && System.Math.Abs(a.X - b.X) <= 1 && System.Math.Abs(a.Z - b.Z) <= 1;
        }
    }

    /// <summary><c>Job_Chat</c>: a warden talks a prisoner round. Written by P8.</summary>
    public class ChatJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;
        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }

    /// <summary><c>Job_Escort</c>: a warden walks a released prisoner out. Written by P10.</summary>
    public class EscortJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;
        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }

    /// <summary><c>Job_GoToCell</c>: a surrendered raider walks to a prison bed. Written by P11.</summary>
    public class GoToCellJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;
        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }

    /// <summary><c>Job_Escape</c>: bash the door, run for the edge. Written by P9.</summary>
    public class EscapeJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;
        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }

    /// <summary><c>Job_LeaveFree</c>: a pawn let go walks off the board. Written by P10.</summary>
    public class LeaveFreeJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;
        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }

    /// <summary><c>Job_Arrest</c>: walk to a colonist and take her. Written by P12.</summary>
    public class ArrestJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;
        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
    }
}
