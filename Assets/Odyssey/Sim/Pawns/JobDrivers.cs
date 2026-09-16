#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Take a thing to a stockpile: a loose one to any pile that will have it, or a stored one
    /// to a better pile than it is in.
    ///
    /// Toils: walk to the thing, pick it up, walk to the destination, put it down. Both the thing
    /// and the destination cell are claimed up front, which is what stops two haulers setting off
    /// for the same crate or for the same square. The destination may already hold a stack of
    /// the same def: the load merges into it, and the cell claim is what keeps a second hauler
    /// from counting on the same room.
    /// </summary>
    public class HaulJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx)
        {
            var item = ctx.Items.Get(Job.TargetItem);
            if (item == null || item.Cell < 0) return false;
            if (!ctx.Items.CellHasSpace(Job.DestCell, item.DefIndex, item.Stack)) return false;

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
                    // Checked again on arrival: something may have been dropped or eaten here
                    // meanwhile, and the cell claim guards against haulers, not against eaters.
                    if (!ctx.Items.CellHasSpace(Job.DestCell, item.DefIndex, item.Stack)) return JobStatus.Failed;
                    ctx.Items.Drop(item, Job.DestCell);
                    Job.CarriedItem = -1;
                    return JobStatus.Succeeded;
                }
            }
        }

        public override void Cleanup(PawnContext ctx, JobStatus status)
        {
            // A job that fails mid-carry must put the thing down somewhere real. Anything else
            // deletes it, and a ten-day run would quietly eat the colony's stores. Where the
            // pawn stands is the first choice, then the nearest cell that can take the load;
            // it used to fall back to the cell the thing was carried from, which is -1 while
            // it is carried, and the thing then existed nowhere at all.
            if (Job.CarriedItem < 0) return;
            var item = ctx.Items.Get(new ThingId(Job.CarriedItem));
            Job.CarriedItem = -1;
            if (item == null) return;

            int at = ctx.Items.NearestCellWithSpace(ctx.Cells, Pawn.Cell, item.DefIndex, item.Stack, DropSearchRadius);
            // A board with no room within that radius is packed solid with things, which
            // nothing in the game can produce; losing the load is the least bad answer, because
            // putting it down on top of something else would corrupt the cell index.
            if (at >= 0) ctx.Items.Drop(item, at);
            else ctx.Items.Despawn(item);
        }

        /// <summary>Cells to search outward for somewhere to put a failed haul's load down.</summary>
        public const int DropSearchRadius = 8;
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

            // One meal from the pile, not the pile. Despawning the whole item ate four meals per
            // sitting, which is why the soak found the pantry empty by the end of day one and
            // why every pantry-size estimate made before this was four times too high.
            if (item.Stack > 1) item.Stack--;
            else ctx.Items.Despawn(item);
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

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Walk to a marked tree, work at it, and leave wood where it stood.
    ///
    /// A tree blocks nothing, but the colonist works from a neighbouring cell rather than from
    /// inside the trunk: <see cref="StandBeside"/> picks the stand, the job carries it as the
    /// target cell and the tree as the destination. The order is cleared the moment the last swing lands, so no other colonist sets off for
    /// it; the world edit itself (the tree going and the wood appearing) is a structural event
    /// and runs in the deferred phase of the same tick, like every collapse and removal.
    /// </summary>
    public class FellJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx)
        {
            if (Job.TargetCell < 0 || Job.DestCell < 0) return false;
            long key = ReservationManager.Key(ReservationTargetKind.Cell, Job.DestCell);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            var designations = ctx.Designations;
            if (designations == null) return JobStatus.Failed;

            int cell = Job.DestCell;
            // Somebody else felled it, or the player changed their mind: stop, do not swing at air.
            if (designations.At(cell) != DesignationKind.Fell || !designations.IsTree(cell))
                return JobStatus.Failed;

            if (ToilIndex == 0)
            {
                JobStatus walk = GotoCell(ctx, Job.TargetCell);
                if (walk == JobStatus.Succeeded) NextToil();
                return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
            }

            ToilProgress++;
            if (ToilProgress < ctx.Content.Jobs[Job.DefIndex].workTicks) return JobStatus.Ongoing;

            designations.Clear(cell);
            int yield = ctx.Content.WoodPerTree;
            ctx.Defer(_ => FellTree(ctx, cell, yield));
            return JobStatus.Succeeded;
        }

        /// <summary>
        /// The nearest walkable cell beside the tree that the pawn can reach, or -1. Beside means
        /// one of the eight neighbours on the same layer, so the colonist stands at the trunk's
        /// side and the wood falls where the tree stood.
        /// </summary>
        public static int StandBeside(PawnContext ctx, Pawn pawn, int tree)
        {
            GridSize size = ctx.Size;
            CellRef at = size.FromIndex(tree);
            int best = -1, bestDistance = int.MaxValue;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int x = at.X + dx, z = at.Z + dz;
                if (!size.Contains(x, z, at.Y)) continue;
                int cell = size.Index(x, z, at.Y);
                if (!ctx.Cells.IsWalkable(cell)) continue;
                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;
                if (!ctx.Reachable(pawn, cell)) continue;
                bestDistance = distance;
                best = cell;
            }
            return best;
        }

        static void FellTree(PawnContext ctx, int cell, int yield)
        {
            ctx.Cells.RemoveEdifice(cell);
            ctx.Chunks?.MarkDirty(ctx.Size.FromIndex(cell));

            // Where the tree stood, or the nearest cell nearby that can take the wood — which
            // includes a pile of wood from the tree next door with room on it, so a stand of
            // trees comes down into a few stacks rather than a scatter of small ones. Nowhere
            // within three cells is a board packed solid with things, which nothing in the game
            // can produce yet; losing the wood then is the least bad answer, because spawning
            // onto a cell that cannot take it would corrupt the cell index.
            int at = ctx.Items.NearestCellWithSpace(ctx.Cells, cell, ItemIndex.Wood, yield, maxRadius: 3);
            if (at >= 0) ctx.Items.Spawn(ItemIndex.Wood, at, yield);
        }
    }
}
