#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    // OrderAttack (design 33 §2d, §5, §6A). Its own partial file so the lane that writes it edits no
    // file another lane owns: lane A (docs/plans/combat-contracts.md). On the job system for the
    // reason HandleForceJob gives — starting and ending jobs is what the pipeline is.
    public sealed partial class JobSystem
    {
        /// <summary>
        /// <c>OrderAttack(cell, A = attacker, B = target pawn, or 0 and the cell a building)</c>.
        /// A drafted colonist closes on the target and swings until one of them goes down — or,
        /// ordered on a pawn already down, until it is dead (the only way a marauder that stays
        /// down is finished). The job is forced, like a move (design 33 §2c): the same
        /// <c>Job_AttackMelee</c> the hunt uses, with <see cref="Job.PlayerForced"/> set and the
        /// target named on the pawn (<see cref="Pawn.CombatTarget"/>).
        ///
        /// <para><b>Refused</b> (<c>NotPermitted</c>) for an attacker that does not exist, is not
        /// one of ours, is not drafted (§5j: attack needs a draft) or is down; for a target of 0 —
        /// a building, which is C6's; for a target that does not exist, is dead, is the attacker
        /// herself, or cannot be reached. <b>Any pawn may be the target</b> — an animal, a
        /// marauder, a colonist: the Ctrl that a colonist target needs is the interface's gesture
        /// (<c>CombatOrders.Route</c>), and the contract gives the intent no argument to carry it
        /// (design 33 §6A).</para>
        /// </summary>
        public IntentRejection HandleOrderAttack(Intent intent)
        {
            Pawn? pawn = _ctx.Pawns.Get(new PawnId(intent.A));
            if (pawn == null || !pawn.IsColonist || !pawn.Drafted || pawn.Downed) return IntentRejection.NotPermitted;

            // C6: a building. Refused until that lane routes it (design 33 §5j).
            if (intent.B == 0) return IntentRejection.NotPermitted;

            Pawn? target = _ctx.Pawns.Get(new PawnId(intent.B));
            if (target == null || target == pawn || Melee.IsDead(target)) return IntentRejection.NotPermitted;
            if (!Melee.InReach(_ctx, pawn, target, TraverseMode.Colonist)
                && !_ctx.Reachable(pawn, target.Cell, TraverseMode.Colonist))
                return IntentRejection.NotPermitted;

            int tick = IntentTick;
            pawn.DraftQuietSinceTick = tick;

            // Ends the job in hand — an earlier attack's cleanup clears its target — keeping the
            // step in progress (design 33 §2d). The new target is named after it for that reason.
            Interrupt(pawn, JobStatus.Failed);
            pawn.CombatTarget = target.Id.Value;

            Job job = pawn.JobBuffer;
            job.Reset(JobIndex.AttackMelee);
            job.TargetCell = target.Cell;
            job.DestCell = target.Downed ? AttackMeleeJobDriver.ToTheDeath : -1;
            job.PlayerForced = true;
            return StartJob(pawn, job, tick) ? IntentRejection.None : IntentRejection.NotPermitted;
        }
    }
}
