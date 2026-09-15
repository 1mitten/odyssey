#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Take a loose thing to a stockpile.
    ///
    /// Toils: walk to the thing, pick it up, walk to the destination, put it down. Both the thing
    /// and the destination cell are claimed up front, which is what stops two haulers setting off
    /// for the same crate or for the same empty square.
    /// </summary>
    public class HaulJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx)
        {
            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null || item.Cell < 0) return false;
            if (!ctx.Items.CellHasSpace(Job.DestCell)) return false;

            long itemKey = ReservationManager.Key(ReservationTargetKind.Item, Job.TargetItem.Value);
            long cellKey = ReservationManager.Key(ReservationTargetKind.Cell, Job.DestCell);

            if (!ctx.Reservations.Reserve(Pawn.Id, itemKey)) return false;
            Pawn.HeldReservations.Add(itemKey);

            if (!ctx.Reservations.Reserve(Pawn.Id, cellKey)) return false;
            Pawn.HeldReservations.Add(cellKey);
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
                    if (item.Cell != Job.TargetCell) return JobStatus.Failed;
                    JobStatus walk = GotoCell(ctx, Job.TargetCell);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                case 1:
                {
                    if (item.Cell != Pawn.Cell) return JobStatus.Failed;
                    ctx.Items.PickUp(item, Pawn.Id);
                    Job.CarriedItem = item.Id.Value;
                    NextToil();
                    return JobStatus.Ongoing;
                }

                case 2:
                {
                    JobStatus walk = GotoCell(ctx, Job.DestCell);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                default:
                {
                    if (!ctx.Items.CellHasSpace(Job.DestCell)) return JobStatus.Failed;
                    ctx.Items.Drop(item, Job.DestCell);
                    Job.CarriedItem = -1;
                    return JobStatus.Succeeded;
                }
            }
        }

        public override void Cleanup(PawnContext ctx, JobStatus status)
        {
            // A job that fails mid-carry must put the thing down somewhere real. Anything else
            // deletes it, and a ten-day run would quietly eat the colony's stores.
            if (Job.CarriedItem < 0) return;
            var item = ctx.Items.Get(new ThingId(Job.CarriedItem));
            Job.CarriedItem = -1;
            if (item == null) return;
            ctx.Items.Drop(item, ctx.Items.CellHasSpace(Pawn.Cell) ? Pawn.Cell : item.Cell);
        }
    }

    /// <summary>Walk to food, eat it, remember having done so.</summary>
    public class EatJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx)
        {
            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null || item.Cell < 0) return false;

            long key = ReservationManager.Key(ReservationTargetKind.Item, Job.TargetItem.Value);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null) return JobStatus.Failed;

            if (ToilIndex == 0)
            {
                if (item.Cell != Job.TargetCell) return JobStatus.Failed;
                JobStatus walk = GotoCell(ctx, Job.TargetCell);
                if (walk == JobStatus.Succeeded) NextToil();
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            ToilProgress++;
            if (ToilProgress < ctx.Content.Jobs[Job.DefIndex].workTicks) return JobStatus.Ongoing;

            var need = ctx.Content.Needs[NeedIndex.Food];
            int nutrition = ctx.Content.Items[item.DefIndex].nutrition;
            Pawn.Needs[NeedIndex.Food] = System.Math.Min(need.max, Pawn.Needs[NeedIndex.Food] + nutrition);
            ctx.Items.Despawn(item);
            Pawn.AddMemory(ThoughtIndex.AteMeal, ctx.CurrentTick);
            return JobStatus.Succeeded;
        }
    }

    /// <summary>
    /// Sleep in a bed, or where the pawn stands when no bed is reachable.
    ///
    /// The rest a sleeping pawn gains is applied by the needs system rather than here, so that
    /// every rate in the game lives in one place and on one cadence.
    /// </summary>
    public class SleepJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx)
        {
            if (Job.TargetCell < 0) return true; // sleeping on the ground claims nothing

            long key = ReservationManager.Key(ReservationTargetKind.Cell, Job.TargetCell);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            if (ToilIndex == 0)
            {
                if (Job.TargetCell < 0 || Pawn.Cell == Job.TargetCell)
                {
                    NextToil();
                    return JobStatus.Ongoing;
                }

                JobStatus walk = GotoCell(ctx, Job.TargetCell);
                if (walk == JobStatus.Succeeded) NextToil();
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            Pawn.Asleep = true;
            ToilProgress++;
            if (Pawn.Needs[NeedIndex.Rest] < ctx.Content.Kind.wakeThreshold) return JobStatus.Ongoing;

            if (Job.TargetCell < 0) Pawn.AddMemory(ThoughtIndex.SleptOnGround, ctx.CurrentTick);
            return JobStatus.Succeeded;
        }

        public override void Cleanup(PawnContext ctx, JobStatus status) => Pawn.Asleep = false;
    }

    /// <summary>Walk somewhere nearby for no reason. What a broken or idle pawn does.</summary>
    public class WanderJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;

        public override JobStatus Tick(PawnContext ctx)
        {
            JobStatus walk = GotoCell(ctx, Job.TargetCell);
            return walk == JobStatus.Ongoing ? JobStatus.Ongoing : JobStatus.Succeeded;
        }
    }

    /// <summary>
    /// Stand still for a while. Also the stand-down the think-loop circuit breaker parks a pawn
    /// in, so a pawn that cannot find anything to do costs a counter rather than a rescan.
    /// </summary>
    public class WaitJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;

        public override JobStatus Tick(PawnContext ctx)
        {
            int duration = Job.WorkTicks > 0 ? Job.WorkTicks : ctx.Content.Jobs[Job.DefIndex].workTicks;
            ToilProgress++;
            return ToilProgress >= duration ? JobStatus.Succeeded : JobStatus.Ongoing;
        }
    }
}
