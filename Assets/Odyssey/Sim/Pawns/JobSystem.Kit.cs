#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    // The kit's three orders (design 54 §3), in their own partial file beside Equip's.
    public sealed partial class JobSystem
    {
        /// <summary>
        /// <c>OrderTakeIntoKit(cell, A = colonist, B = thing id)</c>: walk to the stack and take as
        /// many as fit into her kit.
        ///
        /// <para>Refused (<c>NotPermitted</c>) for anybody but a standing colonist of ours out of a
        /// mental break, and for a thing that does not exist, fits no kit, is forbidden, is in
        /// somebody's hands or kit, cannot be reached, is already being fetched, or for which her
        /// kit has no room. Every question first, then the interrupt, as Equip does.</para>
        /// </summary>
        public IntentRejection HandleOrderTakeIntoKit(Intent intent)
        {
            Pawn? pawn = OrderableColonist(intent.A);
            if (pawn == null) return IntentRejection.NotPermitted;

            ColonyItem? item = _ctx.Items.Get(new ThingId(intent.B));
            if (item == null || item.Forbidden || !Kit.Fits(_ctx, item.DefIndex)) return IntentRejection.NotPermitted;
            if (Kit.TakeRoom(pawn, _ctx, item.DefIndex) <= 0) return IntentRejection.NotPermitted;

            int at = _ctx.WhereIs(item);
            if (at < 0 || !_ctx.Reachable(pawn, at, TraverseMode.Colonist)) return IntentRejection.NotPermitted;

            long key = ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value);
            if (!_ctx.Reservations.CanReserve(pawn.Id, key)) return IntentRejection.NotPermitted;

            int tick = IntentTick;
            if (pawn.Drafted) pawn.DraftQuietSinceTick = tick;
            Interrupt(pawn, JobStatus.Failed);

            Job job = pawn.JobBuffer;
            job.Reset(JobIndex.TakeIntoKit);
            job.TargetItem = item.Id;
            job.TargetCell = at;
            job.PlayerForced = true;
            return StartJob(pawn, job, tick) ? IntentRejection.None : IntentRejection.NotPermitted;
        }

        /// <summary>
        /// <c>OrderKitDrop(A = colonist, B = slot, C = 0 Remove / 1 Drop)</c>: lay the slot's stack at
        /// her feet at once, through <see cref="Kit.Lay"/>. Instant with no job, like Unequip: the
        /// job in hand is left alone. <c>AlreadyInThatState</c> for an empty slot.
        /// </summary>
        public IntentRejection HandleOrderKitDrop(Intent intent)
        {
            Pawn? pawn = _ctx.Pawns.Get(new PawnId(intent.A));
            if (pawn == null || !pawn.IsColonist || pawn.Downed) return IntentRejection.NotPermitted;
            if ((uint)intent.B >= (uint)Kit.Slots) return IntentRejection.NotPermitted;
            if (Kit.Held(pawn, _ctx, intent.B) == null) return IntentRejection.AlreadyInThatState;

            Kit.Lay(pawn, _ctx, intent.B, pawn.Cell, forbid: intent.C != 0);
            return IntentRejection.None;
        }

        /// <summary>
        /// <c>OrderUseKit(A = colonist, B = slot)</c>: use it now. Medical supplies start the
        /// treatment of herself from that slot, with no walk; a ration starts a meal of it where she
        /// stands. Refused whenever <see cref="Kit.UseOf"/> — the rule the Use button is drawn from —
        /// says it is not usable.
        /// </summary>
        public IntentRejection HandleOrderUseKit(Intent intent)
        {
            Pawn? pawn = OrderableColonist(intent.A);
            if (pawn == null || (uint)intent.B >= (uint)Kit.Slots) return IntentRejection.NotPermitted;
            ColonyItem? item = Kit.Held(pawn, _ctx, intent.B);
            if (item == null) return IntentRejection.NotPermitted;

            if (Kit.UseOf(pawn, _ctx, item) != KitUseHandle.Usable) return IntentRejection.NotPermitted;
            bool heals = _ctx.Content.Items[item.DefIndex].healPerUnit > 0;

            int tick = IntentTick;
            if (pawn.Drafted) pawn.DraftQuietSinceTick = tick;
            Interrupt(pawn, JobStatus.Failed);

            Job job = pawn.JobBuffer;
            if (heals)
            {
                job.Reset(JobIndex.Treat);
                job.WorkTicks = pawn.Id.Value;
                job.DestCell = -1;
            }
            else
            {
                job.Reset(JobIndex.Eat);
            }
            job.TargetItem = item.Id;
            job.TargetCell = -1;
            job.PlayerForced = true;
            return StartJob(pawn, job, tick) ? IntentRejection.None : IntentRejection.NotPermitted;
        }

        /// <summary>A colonist of ours the player may order: standing, and not in a mental break. Else null.</summary>
        Pawn? OrderableColonist(int id)
        {
            Pawn? pawn = _ctx.Pawns.Get(new PawnId(id));
            return pawn == null || !pawn.IsColonist || pawn.Downed || pawn.IsBroken ? null : pawn;
        }
    }
}
