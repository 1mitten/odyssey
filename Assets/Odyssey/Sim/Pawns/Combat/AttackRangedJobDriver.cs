#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_AttackRanged</c> (design 47 §2d): stand where the line to the target is open and shoot
    /// at it. The melee driver's shape (<see cref="AttackMeleeJobDriver"/>) with the range-and-sight
    /// test where the reach test was. The target is <see cref="Pawn.CombatTarget"/>, set by whatever
    /// started the job and cleared when it ends.
    ///
    /// <para><b>Two toils.</b> <see cref="Approach"/> walks until the line opens and, standing on a
    /// cell no other fighter holds with the clock allowing, starts an aim: the clock forward by the
    /// gun's cooldown — which counts from the aim's start, as the swing clock counts from the
    /// swing's — and into <see cref="Aim"/>, which counts the aim in milliwork at the shooter's
    /// condition (a hungry or frozen colonist aims slower; Shooting buys accuracy, not speed).
    /// <b>The shot is fired by <see cref="CombatSystem"/>, not here</b>, on the tick the aim is
    /// complete — lost if the shooter is stunned, down or dead — and it puts this driver back into
    /// <see cref="Approach"/>. Symmetric with the swing: <c>FireOrLose</c> against
    /// <c>LandOrLose</c>.</para>
    ///
    /// <para><b>The aim breaks with no cooldown spent</b> when the target leaves range or sight: back
    /// to <see cref="Approach"/>, the clock given back, and the shot never happened. Charging a
    /// cooldown for it would make a colonist behind a wall a free kill.</para>
    ///
    /// <para><b>No minimum range</b>: an adjacent enemy is shot at point-blank. <b>The hold</b>
    /// (drafted, unforced, not joining) never walks: out of sight, she holds again. <b>A chase</b>
    /// walks toward a side of the target with the melee chase's cadence and <b>stops at the first
    /// step boundary where the line opens</b> — so a shooter's place is wherever the line first opens,
    /// and because she counts in <see cref="Melee.IsInAnAttack"/> the melee fighters treat her cell
    /// as held.</para>
    ///
    /// <para><b>Everything it decides with is saved</b> — the toil and its progress, the job's
    /// <see cref="Job.TargetCell"/>, <see cref="Job.WorkTicks"/> and <see cref="Job.DestCell"/>, and
    /// on the pawn the target, the clock, the destination and the step — and nothing is held on the
    /// pawn between the aim and the impact: the bullet in flight lives in <see cref="Projectiles"/>.</para>
    ///
    /// <para><b>A building</b> is not shot at yet (design 47 §2d, R6): a job with no pawn target fails.</para>
    /// </summary>
    public class AttackRangedJobDriver : JobDriver
    {
        /// <summary>Walking until the line opens, or standing with it open waiting for the clock.</summary>
        public const int Approach = 0;

        /// <summary>Aiming; <see cref="JobDriver.ToilProgress"/> counts the aim in milliwork.</summary>
        public const int Aim = 1;

        public override bool TryMakeReservations(PawnContext ctx) => true;

        /// <summary>An aim is under way.</summary>
        public bool InAim => ToilIndex == Aim;

        /// <summary>
        /// The target's cell while aiming or standing engaged between shots, so the figure faces it and
        /// holds its stance through the cooldown; -1 while walking.
        /// </summary>
        public override int WorkFocus => ToilIndex == Aim || (Pawn.Destination < 0 && Job.WorkTicks != 0) ? Job.TargetCell : -1;

        /// <summary>Has the aim run its course, for this gun?</summary>
        public bool AimDone(in Armament armament) =>
            ToilIndex == Aim && ToilProgress >= armament.Attack.windupTicks * Rates.Scale;

        /// <summary>
        /// The shot was fired, or lost: back to standing ready. Called by the resolver. The clock is
        /// left where the aim set it: a shot fired or lost to a stun spent its cooldown.
        /// </summary>
        public void EndAim()
        {
            ToilIndex = Approach;
            ToilProgress = 0;
        }

        /// <summary>
        /// The aim broke before the shot — the target left range or sight: back to standing ready
        /// with the clock given back, because the shot never happened (design 47 §7).
        /// </summary>
        void BreakAim()
        {
            ToilIndex = Approach;
            ToilProgress = 0;
            Pawn.NextSwingTick = 0;
        }

        /// <summary>How much of the aim a tick is worth, in milliwork: the shooter's condition, never her skill.</summary>
        int AimRate() => Rates.Scale * Pawn.ConditionPerMille() / 1_000;

        public override JobStatus Tick(PawnContext ctx)
        {
            if (Pawn.CombatTarget == 0) return JobStatus.Failed;

            Pawn? target = ctx.Pawns.Get(new PawnId(Pawn.CombatTarget));
            if (target == null || Melee.IsDead(target)) return JobStatus.Succeeded;
            if (target.Downed && Job.DestCell != AttackMeleeJobDriver.ToTheDeath) return JobStatus.Succeeded;
            // Taken — she surrendered under the last blow (design 58 §10): the fight with her is
            // over, as it is when she goes down.
            if (target.Custody == PawnCustody.Prisoner && Job.DestCell != AttackMeleeJobDriver.ToTheDeath) return JobStatus.Succeeded;

            // Her gun is what makes this job hers: without one (dropped, taken, never held) it is over.
            RangedDef? ranged = ctx.WeaponRules.ArmamentOf(Pawn, ctx).Attack.ranged;
            if (ranged == null) return JobStatus.Failed;

            int tick = ctx.CurrentTick;

            if (ToilIndex == Aim)
            {
                // Out of range, out of sight or into the water mid-aim: the shot never happened.
                if (!Ranged.CanShootFrom(ctx, Pawn.Cell) || !Ranged.CanHit(ctx, Pawn.Cell, target.Cell, ranged))
                {
                    BreakAim();
                    return JobStatus.Ongoing;
                }
                ToilProgress += AimRate();
                Job.TargetCell = target.Cell;
                return JobStatus.Ongoing;
            }

            TraverseMode mode = Job.Mode;
            bool boundary = Pawn.MoveProgress < Pawn.MoveRatePerMille();
            bool joining = Job.DestCell == AttackMeleeJobDriver.Joining;
            bool hold = Pawn.Drafted && !Job.PlayerForced && !joining;

            // Joined to help a colonist (design 33 §15): once the attacker is on no colonist the
            // reason is over.
            if (joining && Melee.ColonistUnderAttackBy(ctx, target) == null)
                return boundary ? JobStatus.Succeeded : LandTheStep(ctx);

            bool open = Ranged.CanShootFrom(ctx, Pawn.Cell) && Ranged.CanHit(ctx, Pawn.Cell, target.Cell, ranged);
            if (open)
            {
                // Land a step that is well under way; stop on one that has barely begun.
                if (!boundary) return LandTheStep(ctx);

                // A bandit, or a colonist fighting back undrafted, looks for cover before she shoots
                // (design 53 §6). A drafted colonist holds where the player put her, and an order the
                // player gave is carried out from where it finds her.
                if (!Pawn.Drafted && !Job.PlayerForced && tick - Pawn.JobStartTick < CoverPosition.SeekWindowTicks)
                {
                    int cover = CoverDestination(ctx, target);
                    if (cover >= 0 && cover != Pawn.Cell)
                    {
                        JobStatus toCover = GotoCell(ctx, cover);
                        if (toCover != JobStatus.Failed) return JobStatus.Ongoing;
                        Pawn.ClearPath();
                        Pawn.Destination = -1;
                    }
                }

                if (MayShootFrom(ctx, target, tick))
                {
                    Pawn.ClearPath();
                    Pawn.Destination = -1;
                    Job.TargetCell = target.Cell;
                    if (Job.WorkTicks == 0) Job.WorkTicks = tick;
                    if (tick >= Pawn.NextSwingTick) StartAim(ctx, target, tick);
                    return JobStatus.Ongoing;
                }
            }

            // The hold never walks: with no line from where she stands, she holds again.
            if (hold && !open) return boundary ? JobStatus.Succeeded : LandTheStep(ctx);

            // A hunt, a revenge or a self-defence thinks again now and then.
            if (!Job.PlayerForced && boundary && tick - Pawn.JobStartTick >= ctx.Content.Combat.rechooseTicks)
                return JobStatus.Succeeded;

            // Neither in sight nor reachable — a target on a roof she can neither see nor climb to.
            if (!ctx.CanTravel(Pawn, target.Cell, mode))
            {
                if (open) return JobStatus.Ongoing;
                return JobStatus.Failed;
            }

            // The chase: to a side of the target, re-chosen as the melee chase re-chooses it, and
            // left at the first boundary where the line opens (above).
            int dest = Pawn.Destination;
            bool moved = Job.TargetCell != target.Cell;
            bool due = tick - Job.WorkTicks >= ctx.Content.Combat.chaseRepathTicks;
            bool choose = dest >= 0 ? boundary && moved && due : moved || due || Job.WorkTicks == 0 || open;
            if (choose)
            {
                Job.WorkTicks = tick;
                Job.TargetCell = target.Cell;
                dest = Melee.ChooseSide(ctx, Pawn, target, mode);
                if (dest < 0)
                {
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
        /// A step is under way and the answer waits for it to land — the melee driver's own rule,
        /// copied rather than shared because it is three lines and a private seam
        /// (<c>AttackMeleeJobDriver.LandTheStep</c>, design 33 §21c): a path is never saved, so after
        /// a load it is asked for again here and the step lands on the same tick either way.
        /// </summary>
        JobStatus LandTheStep(PawnContext ctx)
        {
            if (Pawn.HasPath || Pawn.PathPending) return JobStatus.Ongoing;
            if (Pawn.Destination < 0 || GotoCell(ctx, Pawn.Destination) == JobStatus.Failed) Pawn.ClearPath();
            return JobStatus.Ongoing;
        }

        /// <summary>
        /// Where a fighter who takes cover should be going, with her line open from where she stands
        /// (design 53 §6): on to the cell she is already walking to while it has more cover from the
        /// target than this one; else, with this cell barely covered, the best cell
        /// <see cref="CoverPosition.Find"/> gives, which may be this one. -1 or her own cell: shoot
        /// from here.
        /// </summary>
        int CoverDestination(PawnContext ctx, Pawn target)
        {
            int here = ctx.RangedRules.CoverPerMille(target.Cell, Pawn.Cell, ctx);
            int going = Pawn.Destination;
            if (going >= 0 && going != Pawn.Cell && going != target.Cell
                && ctx.RangedRules.CoverPerMille(target.Cell, going, ctx) > here)
                return going;
            if (here >= ctx.Content.Combat.coverCrouchPerMille) return -1;
            return CoverPosition.Find(ctx, Pawn, target, ctx.WeaponRules.ArmamentOf(Pawn, ctx));
        }

        /// <summary>
        /// May she shoot from the cell she is on? Never from her target's cell, and never from a cell
        /// another fighter holds (<see cref="Melee.Holds"/>) — the melee driver's rule. Standing on a
        /// cell she took and waiting out her clock, it is still hers, so she is not asked every tick.
        /// </summary>
        bool MayShootFrom(PawnContext ctx, Pawn target, int tick)
        {
            if (Pawn.Cell == target.Cell) return false;
            if (Pawn.Destination < 0 && tick < Pawn.NextSwingTick && Job.WorkTicks != 0) return true;
            return !Melee.Holds(ctx, Pawn, Pawn.Cell);
        }

        /// <summary>
        /// The aim begins: the clock forward by the gun's cooldown, counted from here, and the draft's
        /// quiet clock started again — a shot is activity (design 33 §15). Nothing is decided yet:
        /// the shot is rolled when it is fired, against where things then stand.
        /// </summary>
        void StartAim(PawnContext ctx, Pawn target, int tick)
        {
            Armament armament = ctx.WeaponRules.ArmamentOf(Pawn, ctx);
            Pawn.NextSwingTick = tick + armament.Attack.cooldownTicks;
            if (Pawn.Drafted) Pawn.DraftQuietSinceTick = tick;
            Job.TargetCell = target.Cell;
            ToilIndex = Aim;
            ToilProgress = 0;
        }

        /// <summary>The attack is over: the pawn is nobody's attacker, and an aim in hand is lost.</summary>
        public override void Cleanup(PawnContext ctx, JobStatus status)
        {
            Pawn.CombatTarget = 0;
            ToilIndex = Approach;
            ToilProgress = 0;
        }
    }
}
