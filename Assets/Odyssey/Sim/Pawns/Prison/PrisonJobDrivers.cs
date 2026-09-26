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

    /// <summary><c>Job_FeedPrisoner</c>: carry a meal to a prisoner. Written by P7.</summary>
    public class FeedPrisonerJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;
        public override JobStatus Tick(PawnContext ctx) => JobStatus.Failed;
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
