#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    // OrderEquip (design 33 §4, §5j, §6D). Its own partial file so the lane that writes it edits no
    // file another lane owns: lane D (docs/plans/combat-contracts.md). On the job system for the
    // reason HandleForceJob gives — starting and ending jobs is what the pipeline is.
    public sealed partial class JobSystem
    {
        /// <summary>
        /// <c>OrderEquip(cell, A = colonist, B = weapon thing id)</c>: walk to it and take it into
        /// the hand, putting down whatever was there.
        ///
        /// <para><b>Drafted or not</b> (design 33 §5j): a fetch, not a fight, and one weapon fills
        /// one hand. Refused (<c>NotPermitted</c>) for a pawn that does not exist, an animal, a
        /// hostile, a downed colonist and a colonist in a mental break — the one state the player
        /// may not command through, as for the draft; and for a thing that does not exist, is not
        /// a weapon, is forbidden, is in somebody's hands, cannot be reached, or is already being
        /// fetched. <c>AlreadyInThatState</c> for the weapon she already holds.</para>
        ///
        /// <para><b>Every question first, then the interrupt.</b> A refusal claims nothing and
        /// leaves her job alone; only an order that will start ends the job in hand, keeping the
        /// step she is part way through (<see cref="Interrupt"/>), as a move does. The job is
        /// forced, so a casual think does not take it away, and a drafted colonist returns to the
        /// hold when it ends. The cell is not read: the thing's own record says where it is.</para>
        /// </summary>
        public IntentRejection HandleOrderEquip(Intent intent)
        {
            Pawn? pawn = _ctx.Pawns.Get(new PawnId(intent.A));
            if (pawn == null || !pawn.IsColonist || pawn.Downed || pawn.IsBroken) return IntentRejection.NotPermitted;

            ColonyItem? item = _ctx.Items.Get(new ThingId(intent.B));
            if (item == null) return IntentRejection.NotPermitted;
            if (pawn.EquippedItem == item.Id.Value && WeaponHand.Held(pawn, _ctx) == item)
                return IntentRejection.AlreadyInThatState;
            if (!_ctx.WeaponRules.CanEquip(pawn, item, _ctx)) return IntentRejection.NotPermitted;

            int at = _ctx.WhereIs(item);
            if (!_ctx.Reachable(pawn, at, TraverseMode.Colonist)) return IntentRejection.NotPermitted;

            long key = ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value);
            if (!_ctx.Reservations.CanReserve(pawn.Id, key)) return IntentRejection.NotPermitted;

            int tick = IntentTick;
            if (pawn.Drafted) pawn.DraftQuietSinceTick = tick;

            Interrupt(pawn, JobStatus.Failed);

            Job job = pawn.JobBuffer;
            job.Reset(JobIndex.Equip);
            job.TargetItem = item.Id;
            job.TargetCell = at;
            job.PlayerForced = true;
            return StartJob(pawn, job, tick) ? IntentRejection.None : IntentRejection.NotPermitted;
        }
    }
}
