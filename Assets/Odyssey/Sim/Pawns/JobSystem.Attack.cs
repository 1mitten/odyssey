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
        /// ordered on a pawn already down, until it is dead (the only way a bandit that stays
        /// down is finished). The job is forced, like a move (design 33 §2c): the same
        /// <c>Job_AttackMelee</c> the hunt uses, with <see cref="Job.PlayerForced"/> set and the
        /// target named on the pawn (<see cref="Pawn.CombatTarget"/>).
        ///
        /// <para><b>Refused</b> (<c>NotPermitted</c>) for an attacker that does not exist, is not
        /// one of ours, is not drafted (§5j: attack needs a draft) or is down; for a target of 0 —
        /// a building (C6, design 33 §13d) — as <see cref="OrderAttackBuilding"/> says; for a
        /// target that does not exist, is dead, is the attacker
        /// herself, or cannot be reached. <b>Any pawn may be the target</b> — an animal, a
        /// bandit, a colonist: the Ctrl that a colonist target needs is the interface's gesture
        /// (<c>CombatOrders.Route</c>), and the contract gives the intent no argument to carry it
        /// (design 33 §6A). <b><c>AlreadyInThatState</c></b> for the order she is already carrying
        /// out: the same target, forced, to the same end.</para>
        /// </summary>
        public IntentRejection HandleOrderAttack(Intent intent)
        {
            Pawn? pawn = _ctx.Pawns.Get(new PawnId(intent.A));
            if (pawn == null || !pawn.IsColonist || !pawn.Drafted || pawn.Downed) return IntentRejection.NotPermitted;

            // C6: a building, named by a cell of it (design 33 §13d).
            if (intent.B == 0) return OrderAttackBuilding(pawn, intent.Cell);

            Pawn? target = _ctx.Pawns.Get(new PawnId(intent.B));
            if (target == null || target == pawn || Melee.IsDead(target)) return IntentRejection.NotPermitted;
            // With a gun, a target she can see in range is as good as one she can walk to (design
            // 47 §2d): a bandit on a roof she cannot climb to is still an order she can carry out.
            RangedDef? gun = _ctx.WeaponRules.ArmamentOf(pawn, _ctx).Attack.ranged;
            if (!Melee.InReach(_ctx, pawn, target, TraverseMode.Colonist)
                && !_ctx.Reachable(pawn, target.Cell, TraverseMode.Colonist)
                && !(gun != null && Ranged.CanHit(_ctx, pawn.Cell, target.Cell, gun)))
                return IntentRejection.NotPermitted;
            int attack = CombatJobs.AttackJobFor(pawn, _ctx);

            // The same order again — a confirm-click, sent for every selected drafted colonist — is
            // a no-op. Restarting threw away the swing in the air while the pawn's swing clock
            // still waited out a cooldown, so clicking faster than a wind-up stopped her landing
            // anything (review, 2026-09-23). Ordered on a target gone down since, the order is new:
            // it carries on to the death.
            int toTheDeath = target.Downed ? AttackMeleeJobDriver.ToTheDeath : -1;
            if (pawn.CurrentJob is { PlayerForced: true } current && current.DefIndex == attack
                && pawn.CombatTarget == target.Id.Value && current.DestCell == toTheDeath)
                return IntentRejection.AlreadyInThatState;

            int tick = IntentTick;
            pawn.DraftQuietSinceTick = tick;

            // Ends the job in hand — an earlier attack's cleanup clears its target — keeping the
            // step in progress (design 33 §2d). The new target is named after it for that reason.
            Interrupt(pawn, JobStatus.Failed);
            pawn.CombatTarget = target.Id.Value;

            Job job = pawn.JobBuffer;
            job.Reset(attack);
            job.TargetCell = target.Cell;
            job.DestCell = toTheDeath;
            job.PlayerForced = true;
            return StartJob(pawn, job, tick) ? IntentRejection.None : IntentRejection.NotPermitted;
        }

        /// <summary>
        /// <c>OrderAttack(cell, A, B = 0)</c>: beat down the building standing in <paramref name="at"/>
        /// (design 33 §13d). The drafted checks are the caller's; refused here when no target stands
        /// there (<see cref="BuildingTargets.TryFind"/> — a floor, a tree, bare ground) or no cell
        /// beside it can be reached.
        ///
        /// <para><b>The job carries the building by its record handle</b>, in
        /// <see cref="Job.DestCell"/> — handles are never reused, so a wall pulled down and raised
        /// again is a new building and the order does not carry on into it — and the cell she strikes
        /// at in <see cref="Job.TargetCell"/>. <see cref="Pawn.CombatTarget"/> stays 0, which is what
        /// says "a building" to the driver, the resolver and the publish. The same building again is
        /// <c>AlreadyInThatState</c>, as the same pawn is.</para>
        /// </summary>
        IntentRejection OrderAttackBuilding(Pawn pawn, CellRef at)
        {
            if (!_ctx.Size.Contains(at.X, at.Z, at.Y)) return IntentRejection.NotPermitted;
            // Not with a gun yet (design 47 §2d, R6): the ranged driver cannot shoot a building.
            if (!CombatJobs.CanBreakBuildings(pawn, _ctx)) return IntentRejection.NotPermitted;
            if (!BuildingTargets.TryFind(_ctx, _ctx.Size.Index(at), out BuildingTarget target)) return IntentRejection.NotPermitted;
            if (!BuildingTargets.CanReach(_ctx, pawn, target, TraverseMode.Colonist)) return IntentRejection.NotPermitted;

            if (pawn.CurrentJob is { DefIndex: JobIndex.AttackMelee, PlayerForced: true } current
                && pawn.CombatTarget == 0 && current.DestCell == target.Handle)
                return IntentRejection.AlreadyInThatState;

            int tick = IntentTick;
            pawn.DraftQuietSinceTick = tick;

            Interrupt(pawn, JobStatus.Failed);
            pawn.CombatTarget = 0;

            Job job = pawn.JobBuffer;
            job.Reset(JobIndex.AttackMelee);
            job.TargetCell = BuildingTargets.StruckCell(_ctx, pawn.Cell, target);
            job.DestCell = target.Handle;
            job.PlayerForced = true;
            return StartJob(pawn, job, tick) ? IntentRejection.None : IntentRejection.NotPermitted;
        }
    }
}
