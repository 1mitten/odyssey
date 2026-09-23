#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_Equip</c> (design 33 §4 C3, §6D): walk to a weapon, stoop for it, take it into the
    /// hand (<see cref="Pawn.EquippedItem"/>) and put down whatever was there. <b>Lane D's file</b>
    /// (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para>Toils: walk to where it lies (0), the lift (1), done (2). The lift is
    /// <c>LiftToil</c>, the same stoop, grasp and rise every job that takes a thing up uses, so a
    /// weapon is not acquired by magic while she stands upright. <b>It changes hands at the
    /// grasp</b>: the lift puts it in her arms as a job's load, and on that same tick it goes from
    /// the arms to the hand and the old weapon goes down. From then on the job carries nothing,
    /// so no path through its ending can put the new weapon back on the ground.</para>
    ///
    /// <para>The weapon is claimed for the length of the job (an item reservation, the one a
    /// haul or a meal takes), so a hauler cannot carry it off to a stockpile while she walks to it
    /// and a second colonist cannot be sent for it.</para>
    ///
    /// <para>Scales with nothing: one pawn, one thing, a constant amount of work a tick.</para>
    /// </summary>
    public class EquipJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx)
        {
            // A job whose thing is already gone claims nothing and fails on its first tick, which
            // is how every combat driver behaves when started on nothing (CombatContractTests).
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
                    JobStatus walk = GotoCell(ctx, Job.TargetCell);
                    if (walk == JobStatus.Succeeded) NextToil();
                    return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
                }

                case 1:
                {
                    JobStatus lift = LiftToil(ctx, item);
                    if (Job.CarriedItem == item.Id.Value)
                    {
                        // The grasp: out of the arms and into the hand, and the old one down.
                        Job.CarriedItem = -1;
                        WeaponHand.TakeUp(Pawn, item, ctx);
                    }
                    return lift;
                }

                default:
                    return JobStatus.Succeeded;
            }
        }

        /// <summary>
        /// A job that ended between the grasp and the hand would be holding a load; it cannot, since
        /// both happen on one tick, but the rule every carrier keeps is kept here too.
        /// </summary>
        public override void Cleanup(PawnContext ctx, JobStatus status) => DropCarried(ctx);
    }
}
