#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_AttackMelee</c> (design 33 §3, §6A): close on the target and swing at it on
    /// <see cref="Pawn.NextSwingTick"/>. The blow is decided when its wind-up begins and kept on the
    /// pawn until it lands (design 33 §9g). The target is <see cref="Pawn.CombatTarget"/> — set by the
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
    /// <see cref="WorkFocus"/> reports — and while approaching, the target's cell the side was
    /// chosen against), <see cref="Job.WorkTicks"/> (the tick of the last re-plan,
    /// the precedent <c>BuildJobDriver</c> set for a driver's own use of that field),
    /// <see cref="Job.DestCell"/> (<see cref="ToTheDeath"/>, <see cref="Joining"/> or -1), and on the pawn the target,
    /// the swing clock, the destination and the step progress. No decision reads the path, which
    /// is not saved, so a save taken mid-swing resumes on the same ticks.</para>
    ///
    /// <para><b>Side by side</b> (design 33 §7c). The walk is to a side of the target, not to its
    /// cell: <see cref="Melee.ChooseSide"/> picks the free cell beside it nearest the attacker, or
    /// one ring back when all eight are held. A side is held by walking to it or standing on it —
    /// <see cref="Pawn.Destination"/> and <see cref="Pawn.Cell"/>, both saved — so two attackers
    /// never stand on one tile and no new state was needed. The drafted hold's blow strikes from
    /// wherever she stands and holds her cell as her side — unless another fighter already holds
    /// it, when she steps to a free side like anybody (§8c); she still never chases.</para>
    ///
    /// <para><b>When it ends.</b> The target gone, dead, or — unless the job was ordered on a pawn
    /// already down — down (the owner's "until one of them goes down"); unreachable; out of reach
    /// for a drafted colonist's hold-attack, which never chases; for a drafted colonist who joined
    /// a fight nearby (<see cref="Joining"/>, design 33 §15), which does chase, when the attacker is
    /// on no colonist any more; and, for any attack nobody
    /// ordered, after <see cref="CombatDef.rechooseTicks"/> at a step boundary, so a hunt or a revenge picks
    /// its target again rather than chasing the first one across the board for ever.</para>
    ///
    /// <para><b>A building</b> (C6, design 33 §13e): a job with no <see cref="Pawn.CombatTarget"/>
    /// is an order on a building, carried by its record handle in <see cref="Job.DestCell"/> —
    /// <see cref="TickBuilding"/>, taken at the top of <see cref="Tick"/>.</para>
    /// </summary>
    public class AttackMeleeJobDriver : JobDriver
    {
        /// <summary>Walking to reach, or standing in reach waiting for the swing clock.</summary>
        public const int Approach = 0;

        /// <summary>A swing is in the air; <see cref="JobDriver.ToilProgress"/> counts its wind-up.</summary>
        public const int Windup = 1;

        /// <summary><see cref="Job.DestCell"/> for an order given on a pawn already down: carry on until it is dead.</summary>
        public const int ToTheDeath = 1;

        /// <summary>
        /// <see cref="Job.DestCell"/> for a drafted colonist who left her hold to join a fight
        /// nearby (design 33 §15): unforced, but she goes to the attacker, which the hold's own blow
        /// never does. Ends as any unforced attack ends, and also once the attacker is no longer
        /// fighting a colonist.
        /// </summary>
        public const int Joining = 2;

        public override bool TryMakeReservations(PawnContext ctx) => true;

        /// <summary>A swing is in the air.</summary>
        public bool InWindup => ToilIndex == Windup;

        /// <summary>The target's cell while a swing winds up, so the figure turns to it; -1 otherwise.</summary>
        public override int WorkFocus => ToilIndex == Windup ? Job.TargetCell : -1;

        /// <summary>Has the swing in the air wound up far enough to land, for this attack?</summary>
        public bool WindupDone(in Armament armament) =>
            ToilIndex == Windup && ToilProgress >= armament.Attack.windupTicks * Rates.Scale;

        /// <summary>
        /// The swing in the air landed, or was lost: back to standing ready, and the outcome decided
        /// at its start let go. Called by the resolver.
        /// </summary>
        public void EndSwing()
        {
            ToilIndex = Approach;
            ToilProgress = 0;
            Pawn.ClearSwing();
        }

        public override JobStatus Tick(PawnContext ctx)
        {
            // C6: a building target (design 33 §13e), at the branch point lane A left.
            if (Pawn.CombatTarget == 0) return TickBuilding(ctx);

            Pawn? target = ctx.Pawns.Get(new PawnId(Pawn.CombatTarget));
            if (target == null || Melee.IsDead(target)) return JobStatus.Succeeded;
            if (target.Downed && Job.DestCell != ToTheDeath) return JobStatus.Succeeded;
            // Taken — she surrendered under the last blow (design 59 §10): the fight with her is
            // over, as it is when she goes down.
            if (target.Custody == PawnCustody.Prisoner && Job.DestCell != ToTheDeath) return JobStatus.Succeeded;

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

            // A drafted colonist's own blow at an adjacent threat: she strikes from where she stands.
            // Not when she left her hold to join a fight nearby (design 33 §15): she goes to it.
            bool joining = Job.DestCell == Joining;
            bool hold = Pawn.Drafted && !Job.PlayerForced && !joining;

            // Joined to help a colonist: once the attacker is on no colonist — gone to a building,
            // between minds, or its revenge spent — the reason is over, and she holds where she is.
            if (joining && Melee.ColonistUnderAttackBy(ctx, target) == null)
                return boundary ? JobStatus.Succeeded : LandTheStep(ctx);

            bool inReach = Melee.InReach(ctx, Pawn, target, mode);

            if (inReach)
            {
                // Land a step that is well under way; stop on one that has barely begun.
                if (!boundary) return LandTheStep(ctx);

                // Side by side (design 33 §7c): she stops here only if it is a side of her own. The
                // hold too (§8c): she strikes from where she stands unless another fighter holds
                // that cell, and then she steps to a free side as anybody would.
                if (MayFightFrom(ctx, target, tick))
                {
                    Pawn.ClearPath();
                    Pawn.Destination = -1;
                    if (Job.WorkTicks == 0) Job.WorkTicks = tick;

                    if (tick >= Pawn.NextSwingTick) StartSwing(ctx, target, tick);
                    return JobStatus.Ongoing;
                }
            }

            // The hold never chases: out of reach, she holds again.
            if (hold && !inReach) return boundary ? JobStatus.Succeeded : LandTheStep(ctx);

            // A hunt, a revenge or a self-defence thinks again now and then.
            if (!Job.PlayerForced && boundary && tick - Pawn.JobStartTick >= ctx.Content.Combat.rechooseTicks) return JobStatus.Succeeded;

            if (!ctx.CanTravel(Pawn, target.Cell, mode)) return JobStatus.Failed;

            // Choose a side again (design 33 §7c) — the chase's own re-plan, against where the target
            // now stands. Walking: only at a step boundary (so the step in hand is never snapped
            // back), once the target has left the cell the side was chosen against, and no oftener
            // than the content allows. Not walking: at once when it has left, or the attack is new;
            // otherwise at the same cadence, which is how one waiting a ring back finds a side
            // freed up — and at once when she is in reach on a cell she may not fight from, so she
            // never waits out the cadence on somebody else's tile (§8c). Job.TargetCell is the
            // target's cell the side was chosen against.
            int dest = Pawn.Destination;
            bool moved = Job.TargetCell != target.Cell;
            bool due = tick - Job.WorkTicks >= ctx.Content.Combat.chaseRepathTicks;
            bool choose = dest >= 0 ? boundary && moved && due : inReach || moved || due || Job.WorkTicks == 0;
            if (choose)
            {
                Job.WorkTicks = tick;
                Job.TargetCell = target.Cell;
                dest = Melee.ChooseSide(ctx, Pawn, target, mode);
                if (dest < 0)
                {
                    // Nowhere free within two of it: wait where she is and look again.
                    Pawn.ClearPath();
                    Pawn.Destination = -1;
                    return JobStatus.Ongoing;
                }
            }

            // Standing a ring back, or wherever nothing was free, until it is time to look again.
            if (dest < 0) return JobStatus.Ongoing;

            JobStatus walk = GotoCell(ctx, dest);
            return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
        }

        /// <summary>
        /// A step is under way and the answer waits for it to land. The mover lands it along the path
        /// she holds — and <b>a path is never saved</b> (<see cref="MovementSystem"/>), so after a load
        /// she holds none, nothing asked for one, and she stood frozen part way through the step while
        /// the world that was saved walked on (found by the combat gate, C7:
        /// <c>AttackDriverTests.ASaveTakenMidStepInReachResumesTheSame</c>). Asked again here, the
        /// path is served before anybody steps in the same tick, so the step lands on the same tick
        /// either way. In a world that was never loaded she always holds one, so this does nothing
        /// there and no golden moved. A walk that can no longer be planned drops the step, so she is
        /// at a boundary next tick rather than part way through one for ever.
        /// </summary>
        JobStatus LandTheStep(PawnContext ctx)
        {
            if (Pawn.HasPath || Pawn.PathPending) return JobStatus.Ongoing;
            if (Pawn.Destination < 0 || GotoCell(ctx, Pawn.Destination) == JobStatus.Failed) Pawn.ClearPath();
            return JobStatus.Ongoing;
        }

        /// <summary>
        /// May she fight from the cell she is on (design 33 §7c, §8c)? Never from the target's own
        /// cell, never from a side another attacker holds, and never from the cell of a pawn
        /// somebody else is fighting (<see cref="Melee.Holds"/>) — so walking past a side somebody
        /// else is making for, she walks on. The drafted hold is asked too. <b>Scales with the pawns
        /// on the board</b>, and is asked only when the answer can have changed: at a step boundary
        /// in reach while walking, on the first stop, and before each swing. One standing waiting
        /// for her swing clock is on a side she already took, so she is not asked every tick.
        /// </summary>
        bool MayFightFrom(PawnContext ctx, Pawn target, int tick)
        {
            if (Pawn.Cell == target.Cell) return false;
            // Never from on top of cover (design 53 §5): she climbs over and fights from beside it.
            if (!Standing.CanStandAt(ctx, Pawn.Cell)) return false;
            // Standing on a side she took, waiting for her swing clock: nobody else chooses a cell
            // a fighter holds, so it is still hers. Job.WorkTicks is set from the first stop.
            if (Pawn.Destination < 0 && tick < Pawn.NextSwingTick && Job.WorkTicks != 0) return true;
            return !Melee.Holds(ctx, Pawn, Pawn.Cell);
        }

        /// <summary>
        /// The wind-up begins, and <b>the blow is decided now</b> (design 33 §9g; owner, 2026-09-23:
        /// a sharp critical's slice is heard during the swing): hit, dodge, damage, stun, critical
        /// and knockback, on the rules' own streams at this tick, kept on the pawn
        /// (<see cref="Pawn.HeldSwing"/>) until the impact applies exactly that. A critical that will
        /// land is published as <see cref="CombatEventKind.SwingCritical"/> in place of
        /// <see cref="CombatEventKind.Swing"/>.
        /// <para><b>A swing is activity for the draft</b> (design 33 §15), as it already was at a
        /// building (<see cref="StartSwingAtBuilding"/>): an attacker standing on her side in reach
        /// never re-thinks, so the quiet clock set when the fight began would otherwise run out
        /// under a long fight and let the draft go the tick it ended.</para>
        /// </summary>
        void StartSwing(PawnContext ctx, Pawn target, int tick)
        {
            Armament armament = ctx.WeaponRules.ArmamentOf(Pawn, ctx).Melee;
            Pawn.NextSwingTick = tick + armament.Attack.cooldownTicks;
            if (Pawn.Drafted) Pawn.DraftQuietSinceTick = tick;
            Pawn.BeginGesture(PawnGesture.Strike);
            SwingOutcome outcome = ctx.MeleeRules.Resolve(Pawn, target, armament, ctx, tick);
            Pawn.HoldSwing(outcome);
            CombatEventKind kind = outcome.Landed && outcome.Critical ? CombatEventKind.SwingCritical : CombatEventKind.Swing;
            ctx.CombatLog.Report(kind, Pawn.Id, target.Id, ctx.Size.FromIndex(target.Cell), tick,
                armament.Attack.windupTicks, armament.ItemDef);
            Job.TargetCell = target.Cell;
            ToilIndex = Windup;
            ToilProgress = 0;
        }

        /// <summary>
        /// The building mode (design 33 §13e). The building never moves, so it is simpler than the
        /// chase: stand beside it on a side nobody else holds, and swing on the weapon's cadence
        /// until it has gone. Everything it decides with is saved — the handle and struck cell on
        /// the job, the tick of the last look in <see cref="Job.WorkTicks"/>, and on the pawn the
        /// cells, the destination, the step and the swing clock.
        /// <list type="bullet">
        /// <item><b>Gone</b> — demolished by anyone, taken apart, or its cell given to a new record —
        /// is success.</item>
        /// <item><b>In reach at a step boundary on a cell nobody else holds</b>: stop, and swing
        /// when the clock allows.</item>
        /// <item>Otherwise <b>choose a side</b> when the attack is new, when she is in reach on a
        /// held cell, or, waiting, every <see cref="CombatDef.chaseRepathTicks"/>; not while walking,
        /// since nothing she walks to can move. No side she can reach at all: the order fails.
        /// Every side held: a player's order waits, a bandit's own choice thinks again (§19).</item>
        /// </list>
        /// </summary>
        JobStatus TickBuilding(PawnContext ctx)
        {
            // A job naming neither a pawn nor a building is nobody's order: it fails, as the stub did.
            if (Job.DestCell < 0) return JobStatus.Failed;
            if (!BuildingTargets.TryStanding(ctx, Job.DestCell, out BuildingTarget target)) return JobStatus.Succeeded;

            if (ToilIndex == Windup)
            {
                ToilProgress += Rates.Scale;
                return JobStatus.Ongoing;
            }

            int tick = ctx.CurrentTick;
            TraverseMode mode = Job.Mode;
            bool boundary = Pawn.MoveProgress < Pawn.MoveRatePerMille();

            // A bandit's own choice (design 33 §14b) thinks again every rechooseTicks, between
            // swings and in reach or not — unlike a hunt, which re-chooses only while chasing,
            // because one beating on a wall never chases and would not look up till it fell. A
            // player's order is forced and runs until the building has gone.
            if (!Job.PlayerForced && boundary && tick - Pawn.JobStartTick >= ctx.Content.Combat.rechooseTicks)
                return JobStatus.Succeeded;

            bool inReach = BuildingTargets.InReach(ctx, Pawn.Cell, target);

            if (inReach)
            {
                if (!boundary) return LandTheStep(ctx);
                if (MayStrikeFromHere(ctx, tick))
                {
                    Pawn.ClearPath();
                    Pawn.Destination = -1;
                    if (Job.WorkTicks == 0) Job.WorkTicks = tick;
                    if (tick >= Pawn.NextSwingTick) StartSwingAtBuilding(ctx, target, tick);
                    return JobStatus.Ongoing;
                }
            }

            int dest = Pawn.Destination;
            bool due = tick - Job.WorkTicks >= ctx.Content.Combat.chaseRepathTicks;
            if (dest < 0 && (inReach || due || Job.WorkTicks == 0))
            {
                Job.WorkTicks = tick;
                dest = BuildingTargets.ChooseSide(ctx, Pawn, target, mode, out bool reachable);
                if (!reachable) return JobStatus.Failed;
                if (dest < 0)
                {
                    // Every side held. A bandit's own choice thinks again at once (design 33
                    // §19): the choice takes only a building with a side free, so it goes to the
                    // next one rather than standing on "Fighting" behind the one who got the side —
                    // the owner's three at a wall on a terrace edge, which had one side. Two can
                    // choose one side in the same tick, before either has walked to it; this is
                    // what sorts them out.
                    if (!Job.PlayerForced) return boundary ? JobStatus.Succeeded : LandTheStep(ctx);

                    // A player's order waits where she is and looks again.
                    Pawn.ClearPath();
                    Pawn.Destination = -1;
                    return JobStatus.Ongoing;
                }
            }

            if (dest < 0) return JobStatus.Ongoing;

            JobStatus walk = GotoCell(ctx, dest);
            return walk == JobStatus.Failed ? JobStatus.Failed : JobStatus.Ongoing;
        }

        /// <summary>
        /// May she strike the building from the cell she is on? Not from a cell another fighter
        /// holds (<see cref="Melee.Holds"/>); standing on a side she took and waiting out her swing
        /// clock, it is still hers, so she is not asked every tick — as <see cref="MayFightFrom"/>.
        /// </summary>
        bool MayStrikeFromHere(PawnContext ctx, int tick)
        {
            if (Pawn.Destination < 0 && tick < Pawn.NextSwingTick && Job.WorkTicks != 0) return true;
            return !Melee.Holds(ctx, Pawn, Pawn.Cell);
        }

        /// <summary>
        /// A blow at a building begins (design 33 §13f): the clock forward by the attack's cooldown,
        /// the strike gesture, the outcome decided now (<see cref="BuildingTargets.Resolve"/>) and
        /// held on the pawn to the impact, the <see cref="CombatEventKind.Swing"/> reported against
        /// target 0 at the cell she strikes. <b>A swing is activity for the draft</b>: the quiet
        /// clock starts again, or a colonist who took more than four hours over a stone wall would
        /// undraft the tick it fell.
        /// </summary>
        void StartSwingAtBuilding(PawnContext ctx, in BuildingTarget target, int tick)
        {
            Armament armament = ctx.WeaponRules.ArmamentOf(Pawn, ctx).Melee;
            Pawn.NextSwingTick = tick + armament.Attack.cooldownTicks;
            Pawn.BeginGesture(PawnGesture.Strike);
            Pawn.HoldSwing(BuildingTargets.Resolve(Pawn, armament, target, ctx, tick));
            if (Pawn.Drafted) Pawn.DraftQuietSinceTick = tick;

            Job.TargetCell = BuildingTargets.StruckCell(ctx, Pawn.Cell, target);
            ctx.CombatLog.Report(CombatEventKind.Swing, Pawn.Id, default, ctx.Size.FromIndex(Job.TargetCell), tick,
                armament.Attack.windupTicks, armament.ItemDef);
            ToilIndex = Windup;
            ToilProgress = 0;
        }

        /// <summary>
        /// The order, the hunt or the revenge is over: the pawn is nobody's attacker, and a swing
        /// still in the air is lost with its decided outcome.
        /// </summary>
        public override void Cleanup(PawnContext ctx, JobStatus status)
        {
            Pawn.CombatTarget = 0;
            Pawn.ClearSwing();
        }
    }
}
