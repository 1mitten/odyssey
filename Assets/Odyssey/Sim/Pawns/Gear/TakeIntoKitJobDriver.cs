#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_TakeIntoKit</c> (design 54 §3): walk to a stack, stoop, and take <b>as many as fit</b>
    /// into her kit, leaving the rest where it lay.
    ///
    /// <para><b>One slot's worth</b> (<see cref="Kit.TakeRoom"/>): a slot of its kind topped up,
    /// or one empty slot filled, never both slots from one big stack.</para>
    ///
    /// <para>Toils: walk to where it lies (0), the lift (1), done (2). The lift is
    /// <see cref="JobDriver.LiftToil"/>'s motion and timing, taking only what fits rather than the
    /// stack, as the doctor's lift takes one unit; at the grasp the taken part goes straight from
    /// the arms into the kit (<see cref="Kit.Put"/>), so from then on the job carries nothing and
    /// no path through its ending can put it back on the ground.</para>
    ///
    /// <para>The stack is claimed for the length of the job (an item reservation, the one a haul
    /// or a meal takes), so a hauler cannot carry it off while she walks to it.</para>
    ///
    /// <para>Scales with nothing: one pawn, one thing, a constant amount of work a tick.</para>
    /// </summary>
    public class TakeIntoKitJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx)
        {
            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null) return true;
            if (ctx.WhereIs(item) < 0) return false;

            long key = ReservationManager.Key(ReservationTargetKind.Item, Job.TargetItem.Value);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null) return JobStatus.Failed;

            switch (ToilIndex)
            {
                case 0:
                {
                    if (!StillAt(ctx, item, Job.TargetCell)) return JobStatus.Failed;
                    if (Kit.TakeRoom(Pawn, ctx, item.DefIndex) <= 0) return JobStatus.Failed;
                    JobStatus walk = GotoCell(ctx, Job.TargetCell);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                case 1:
                    return Lift(ctx, item);

                default:
                    return JobStatus.Succeeded;
            }
        }

        JobStatus Lift(PawnContext ctx, ColonyItem item)
        {
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
                // What fits is asked again at the grasp: the kit may have changed on the walk.
                int room = Kit.TakeRoom(Pawn, ctx, item.DefIndex);
                if (room <= 0) return JobStatus.Failed;
                ColonyItem taken = ctx.Items.SplitOff(item, System.Math.Min(room, item.Stack), Pawn.Id);
                Kit.Put(Pawn, ctx, taken);
            }

            if (elapsed < total) return JobStatus.Ongoing;
            NextToil();
            return JobStatus.Ongoing;
        }

        /// <summary>Nothing is ever left in the arms (the grasp puts it straight in the kit), but the rule every carrier keeps is kept.</summary>
        public override void Cleanup(PawnContext ctx, JobStatus status) => DropCarried(ctx);
    }
}
