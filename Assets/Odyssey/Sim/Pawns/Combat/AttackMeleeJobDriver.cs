#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_AttackMelee</c> (design 33 §3, §6A): close on the target and swing at it on
    /// <see cref="Pawn.NextSwingTick"/>. The target is <see cref="Pawn.CombatTarget"/> — set by the
    /// order, the hunt, the revenge or the self-defence that started the job, and cleared when it
    /// ends. <b>Lane A's file</b> (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>Two toils.</b> <see cref="Approach"/> walks to within reach (<see cref="Melee.InReach"/>)
    /// and, once there and the swing clock allows, starts a swing: it sets the clock forward by
    /// the attack's cooldown, reports <see cref="PawnGesture.Strike"/> and the
    /// <see cref="CombatEventKind.Swing"/> moment, and moves to <see cref="Windup"/>, which counts
    /// the wind-up in milliwork. <b>The swing is resolved by <see cref="CombatSystem"/>, not
    /// here</b>, on the tick the wind-up completes — after every job has ticked and before anybody
    /// steps — and it puts this driver back into <see cref="Approach"/>. So a blow is decided
    /// against the positions the jobs saw, and every hit point is lost through one method.</para>
    ///
    /// <para><b>Everything it knows is saved.</b> The toil and its progress, the job's
    /// <see cref="Job.TargetCell"/> (the target's cell during the wind-up — what
    /// <see cref="WorkFocus"/> reports), <see cref="Job.WorkTicks"/> (the tick of the last re-plan,
    /// the precedent <c>BuildJobDriver</c> set for a driver's own use of that field),
    /// <see cref="Job.DestCell"/> (<see cref="ToTheDeath"/> or -1), and on the pawn the target,
    /// the swing clock, the destination and the step progress. No decision reads the path, which
    /// is not saved, so a save taken mid-swing resumes on the same ticks.</para>
    ///
    /// <para><b>When it ends.</b> The target gone, dead, or — unless the job was ordered on a pawn
    /// already down — down (the owner's "until one of them goes down"); unreachable; out of reach
    /// for a drafted colonist's hold-attack, which never chases; and, for any attack nobody
    /// ordered, after <see cref="CombatDef.rechooseTicks"/> at a step boundary, so a hunt or a revenge picks
    /// its target again rather than chasing the first one across the board for ever.</para>
    ///
    /// <para><b>Buildings are C6's.</b> A job with no <see cref="Pawn.CombatTarget"/> is the
    /// building branch and fails until C6 writes it — the one line marked below.</para>
    /// </summary>
    public class AttackMeleeJobDriver : JobDriver
    {
        /// <summary>Walking to reach, or standing in reach waiting for the swing clock.</summary>
        public const int Approach = 0;

        /// <summary>A swing is in the air; <see cref="JobDriver.ToilProgress"/> counts its wind-up.</summary>
        public const int Windup = 1;

        /// <summary><see cref="Job.DestCell"/> for an order given on a pawn already down: carry on until it is dead.</summary>
        public const int ToTheDeath = 1;

        public override bool TryMakeReservations(PawnContext ctx) => true;

        /// <summary>A swing is in the air.</summary>
        public bool InWindup => ToilIndex == Windup;

        /// <summary>The target's cell while a swing winds up, so the figure turns to it; -1 otherwise.</summary>
        public override int WorkFocus => ToilIndex == Windup ? Job.TargetCell : -1;

        /// <summary>Has the swing in the air wound up far enough to land, for this attack?</summary>
        public bool WindupDone(in Armament armament) =>
            ToilIndex == Windup && ToilProgress >= armament.Attack.windupTicks * Rates.Scale;

        /// <summary>The swing in the air landed, or was lost: back to standing ready. Called by the resolver.</summary>
        public void EndSwing()
        {
            ToilIndex = Approach;
            ToilProgress = 0;
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            // C6: a building target. The branch point the brief asks lane A to leave.
            if (Pawn.CombatTarget == 0) return JobStatus.Failed;

            Pawn? target = ctx.Pawns.Get(new PawnId(Pawn.CombatTarget));
            if (target == null || Melee.IsDead(target)) return JobStatus.Succeeded;
            if (target.Downed && Job.DestCell != ToTheDeath) return JobStatus.Succeeded;

            if (ToilIndex == Windup)
            {
                ToilProgress += Rates.Scale;
                Job.TargetCell = target.Cell;
                return JobStatus.Ongoing;
            }

            int tick = ctx.CurrentTick;
            TraverseMode mode = Job.Mode;

            // At a cell boundary: stepping now loses at most the one tick's movement banked past
            // the cell just landed on. Read off the saved progress, never off the path.
            bool boundary = Pawn.MoveProgress < Pawn.MoveRatePerMille();

            if (Melee.InReach(ctx, Pawn, target, mode))
            {
                // Land a step that is well under way; stop on one that has barely begun.
                if (!boundary) return JobStatus.Ongoing;
                Pawn.ClearPath();
                Pawn.Destination = -1;

                if (tick >= Pawn.NextSwingTick) StartSwing(ctx, target, tick);
                return JobStatus.Ongoing;
            }

            // A drafted colonist's own blow at an adjacent threat never chases: she holds.
            if (Pawn.Drafted && !Job.PlayerForced) return boundary ? JobStatus.Succeeded : JobStatus.Ongoing;

            // A hunt, a revenge or a self-defence thinks again now and then.
            if (!Job.PlayerForced && boundary && tick - Pawn.JobStartTick >= ctx.Content.Combat.rechooseTicks) return JobStatus.Succeeded;

            if (!ctx.Reachable(Pawn, target.Cell, mode)) return JobStatus.Failed;

            // Re-plan to where the target now stands: at once when not walking, else only at a
            // step boundary (so the step in hand is never snapped back) and no oftener than the
            // content allows.
            int dest = Pawn.Destination;
            if (dest != target.Cell
                && (dest < 0 || (boundary && tick - Job.WorkTicks >= ctx.Content.Combat.chaseRepathTicks)))
            {
                dest = target.Cell;
                Job.WorkTicks = tick;
            }

            JobStatus walk = GotoCell(ctx, dest);
            return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
        }

        void StartSwing(PawnContext ctx, Pawn target, int tick)
        {
            Armament armament = ctx.WeaponRules.ArmamentOf(Pawn, ctx);
            Pawn.NextSwingTick = tick + armament.Attack.cooldownTicks;
            Pawn.BeginGesture(PawnGesture.Strike);
            ctx.CombatLog.Report(CombatEventKind.Swing, Pawn.Id, target.Id, ctx.Size.FromIndex(target.Cell), tick,
                armament.Attack.windupTicks, armament.ItemDef);
            Job.TargetCell = target.Cell;
            ToilIndex = Windup;
            ToilProgress = 0;
        }

        /// <summary>The order, the hunt or the revenge is over: the pawn is nobody's attacker.</summary>
        public override void Cleanup(PawnContext ctx, JobStatus status)
        {
            Pawn.CombatTarget = 0;
        }
    }
}
