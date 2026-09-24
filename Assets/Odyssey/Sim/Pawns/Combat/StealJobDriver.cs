#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_Steal</c> (design 33 §17): a marauder with nobody to fight and nothing to break walks
    /// to the stack <see cref="Theft.TryFill"/> chose (<see cref="Job.TargetItem"/>, lying at
    /// <see cref="Job.TargetCell"/>), lifts it through <see cref="JobDriver.LiftToil"/> — the
    /// hauler's own stoop, grasp and rise, so the load is in its arms and drawn there for nothing —
    /// walks to the edge of the board (<see cref="Job.DestCell"/>) and leaves
    /// (<see cref="Theft.Leave"/>). With no stack (<see cref="Job.TargetItem"/> none) it walks
    /// straight to the edge and leaves empty-handed.
    ///
    /// <para><b>Every end but the leaving drops the load</b> (<see cref="Cleanup"/> →
    /// <see cref="JobDriver.DropCarried"/>, the rule every carrier shares): downed, killed, knocked
    /// back, struck by a colonist it then turns on, or looking up to find a fight.</para>
    ///
    /// <para><b>It looks up while it goes.</b> Once every <see cref="CombatDef.rechooseTicks"/>, at a
    /// step boundary, it asks the marauder's mind whether there is a colonist to reach or a building
    /// to break (<see cref="HostileThinkNode.HasAFight"/>); if there is, the theft ends and the next
    /// think fights. A hunt ends its job and thinks again on the same cadence; a thief cannot, because
    /// ending the job would put the load down every three hundred ticks. <see cref="Job.WorkTicks"/>,
    /// which a theft has no duration for, counts the looks already taken, so the cadence survives a
    /// save as the rest of the job does.</para>
    /// </summary>
    public class StealJobDriver : JobDriver
    {
        const int ToWalkToLoot = 0, ToLift = 1, ToWalkToEdge = 2, ToLeave = 3;

        public override bool TryMakeReservations(PawnContext ctx)
        {
            if (Job.DestCell < 0) return false;
            if (Job.TargetItem == ThingId.None) return true;

            ColonyItem? item = ctx.Items.Get(Job.TargetItem);
            if (item == null || ctx.WhereIs(item) < 0) return false;

            long key = ReservationManager.Key(ReservationTargetKind.Item, Job.TargetItem.Value);
            if (!ctx.Reservations.Reserve(Pawn.Id, key)) return false;
            Pawn.HeldReservations.Add(key);
            return true;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            if (ToilIndex == ToWalkToLoot && Job.TargetItem == ThingId.None) NextToil();
            if (ToilIndex == ToLift && Job.TargetItem == ThingId.None) NextToil();

            if (ToilIndex != ToLift && ToilIndex != ToLeave && LookUp(ctx)) return JobStatus.Failed;

            switch (ToilIndex)
            {
                case ToWalkToLoot:
                {
                    ColonyItem? item = ctx.Items.Get(Job.TargetItem);
                    // Taken, eaten or moved before it got there: think again.
                    if (item == null || item.Despawned || !StillAt(ctx, item, Job.TargetCell)) return JobStatus.Failed;
                    JobStatus walk = GotoCell(ctx, Job.TargetCell);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                case ToLift:
                {
                    ColonyItem? item = ctx.Items.Get(Job.TargetItem);
                    if (item == null || item.Despawned) return JobStatus.Failed;
                    return LiftToil(ctx, item);
                }

                case ToWalkToEdge:
                {
                    JobStatus walk = GotoCell(ctx, Job.DestCell);
                    if (walk == JobStatus.Failed) return JobStatus.Failed;
                    if (walk == JobStatus.Ongoing) return JobStatus.Ongoing;
                    NextToil();
                    Leave(ctx);
                    return JobStatus.Ongoing;
                }

                default:
                    // Standing on the edge, waiting for the end of the tick to take it off the
                    // board. A leave that was refused — nothing the game can do today — is asked
                    // for again rather than left waiting for ever.
                    Leave(ctx);
                    return JobStatus.Ongoing;
            }
        }

        void Leave(PawnContext ctx)
        {
            Pawn thief = Pawn;
            ctx.Defer(world => Theft.Leave(ctx, thief, world.CurrentTick));
        }

        /// <summary>
        /// Once per <see cref="CombatDef.rechooseTicks"/> of the job, at a step boundary: is there a
        /// fight to go back to? The count of looks taken is <see cref="Job.WorkTicks"/>, saved and
        /// hashed with the job, so the cadence is the same after a load.
        /// </summary>
        bool LookUp(PawnContext ctx)
        {
            int every = ctx.Content.Combat.rechooseTicks;
            if (every <= 0) return false;
            if (Pawn.MoveProgress >= Pawn.MoveRatePerMille()) return false;

            int window = (ctx.CurrentTick - Pawn.JobStartTick) / every;
            if (window <= Job.WorkTicks) return false;
            Job.WorkTicks = window;
            return HostileThinkNode.HasAFight(Pawn, ctx, Job.Mode);
        }

        public override void Cleanup(PawnContext ctx, JobStatus status) => DropCarried(ctx);
    }
}
