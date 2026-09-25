#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Pawns
{
    // A critical blow knocks its target back a tile (design 33 §9b). Lane A's file, beside the one
    // method every blow is applied through.
    public partial class CombatSystem
    {
        /// <summary>
        /// Where a blow from <paramref name="attacker"/> would knock <paramref name="target"/>, or −1
        /// when it may not be knocked anywhere (design 33 §9b; owner, 2026-09-23):
        /// <list type="bullet">
        /// <item><b>directly away</b>: the step from the attacker's cell to the target's, continued
        /// one more cell — diagonal allowed. Only from a neighbouring cell on the same layer, which
        /// is every blow <see cref="Melee.InReach"/> allows but the same-cell one;</item>
        /// <item><b>on the same layer</b>, a step the target itself could take
        /// (<see cref="NavGraph.IsLegalStep"/> — no knocking through a wall or its corner);</item>
        /// <item><b>or one terrace step down</b>: the cell beyond is open air and the one beneath
        /// it is somewhere the target can stand, with neither corner a wall on a diagonal.
        /// <b>Never two layers or more down, never up;</b></item>
        /// <item><b>never water</b> — neither the cell beyond nor where it would land, nor the
        /// ground beneath either (<see cref="IsWater"/>);</item>
        /// <item><b>free of other fighters</b> — no cell <see cref="Melee.Holds"/> for anybody else,
        /// the same rule every fighter stops by (§8c), so a knockback never stands two fighters on
        /// one tile — <b>and never the tile of the one the target is itself fighting</b>
        /// (<see cref="OnWhoItFights"/>), which Holds leaves to her.</item>
        /// </list>
        /// <para><b>Scales with</b> the pawns on the board, for the one <see cref="Melee.Holds"/>
        /// pass, asked only for a critical that rolled its knockback.</para>
        /// </summary>
        public static int KnockbackCell(PawnContext ctx, Pawn attacker, Pawn target)
        {
            // A body in somebody's arms is not knocked out of them (design 33 §11a).
            if (target.CarriedBy != 0) return -1;
            GridSize size = ctx.Size;
            CellRef a = size.FromIndex(attacker.Cell), t = size.FromIndex(target.Cell);
            if (a.Y != t.Y) return -1;
            int dx = t.X - a.X, dz = t.Z - a.Z;
            if (dx == 0 && dz == 0) return -1;
            if (dx < -1 || dx > 1 || dz < -1 || dz > 1) return -1;

            int x = t.X + dx, z = t.Z + dz;
            if (!size.Contains(x, z, t.Y)) return -1;
            int beyond = size.Index(x, z, t.Y);
            if (IsWater(ctx, beyond)) return -1;

            TraverseMode mode = target.OwnMode;
            int land;
            if (ctx.Nav.IsLegalStep(target.Cell, beyond, mode))
            {
                land = beyond;
            }
            else
            {
                // One terrace step down, and no further: the cell beyond is air, and the one under
                // it is ground the target can stand on. Anything else — a wall, a rise, a drop of
                // two — is no knockback at all.
                if (t.Y == 0 || !ctx.Nav.Grid.IsAir(beyond)) return -1;
                int lower = beyond - size.LayerStride;
                if (IsWater(ctx, lower) || !ctx.Nav.Grid.CanEnter(lower, mode)) return -1;
                if (dx != 0 && dz != 0)
                {
                    if (!Passable(ctx, size.Index(t.X + dx, t.Z, t.Y), mode)) return -1;
                    if (!Passable(ctx, size.Index(t.X, t.Z + dz, t.Y), mode)) return -1;
                }
                land = lower;
            }

            if (Melee.Holds(ctx, target, land)) return -1;
            return OnWhoItFights(ctx, target, land) ? -1 : land;
        }

        /// <summary>
        /// Is <paramref name="cell"/> the side of the pawn <paramref name="pawn"/> is herself
        /// attacking? <see cref="Melee.Holds"/> does not count her own claims against her — an
        /// attacker's own side is hers to stand on — but the tile of the one she is fighting is
        /// held <i>only</i> by her claim when it does not fight back, so Holds let a knockback lay
        /// her on it, and a knocked-down fighter lies where she lands. A fighter never fights from
        /// her target's tile (<c>AttackMeleeJobDriver.MayFightFrom</c>), and she is never knocked
        /// on to it either. Found by <c>FightGuardTests.MixedBrawlsOnManySeeds</c> once the bandit
        /// carried blunt weapons (design 42): a colonist beating a rat, knocked on to the rat by a
        /// bandit's critical, lay there 90 ticks.
        /// </summary>
        static bool OnWhoItFights(PawnContext ctx, Pawn pawn, int cell)
        {
            if (!Melee.IsInAnAttack(pawn) || pawn.CombatTarget == 0) return false;
            Pawn? fought = ctx.Pawns.Get(new PawnId(pawn.CombatTarget));
            return fought != null && Melee.SideOf(fought) == cell;
        }

        /// <summary>
        /// Water, for a knockback (design 33 §9b: never water): the cell's own terrain is water, it
        /// is priced as a wade, or it stands on top of water. Any of the three is a swim or a wade,
        /// which is what the owner ruled out.
        /// </summary>
        public static bool IsWater(PawnContext ctx, int cell)
        {
            if (NaturalContent.IsWater(ctx.Cells.Terrain[cell])) return true;
            if (ctx.Nav.Grid.CostClass[cell] == NaturalContent.CostClassShallowWater) return true;
            int below = cell - ctx.Size.LayerStride;
            return below >= 0 && NaturalContent.IsWater(ctx.Cells.Terrain[below]);
        }

        /// <summary>A corner a body can pass: open air or a cell it could walk into, never a wall.</summary>
        static bool Passable(PawnContext ctx, int cell, TraverseMode mode) =>
            ctx.Nav.Grid.IsAir(cell) || ctx.Nav.Grid.CanWalkInto(cell, mode);

        /// <summary>
        /// Knock <paramref name="target"/> back a tile, if it may go anywhere (<see cref="KnockbackCell"/>);
        /// if not, nothing here happens — the stagger is presentation's, off the <c>Critical</c>
        /// moment. When it goes, in order and all in this call:
        /// <list type="number">
        /// <item>its job ends through <see cref="JobSystem.EndJob"/>, the one release path: a
        /// carried load is put down by its driver's cleanup where the blow found it, claims are let
        /// go, a swing in the air is lost, and the path is cleared — nothing of the step it was
        /// taking is kept, because it is not where that step began any more;</item>
        /// <item>it is moved to the landing cell;</item>
        /// <item>it is knocked down for <see cref="CombatDef.knockedDownTicks"/>: the job pipeline
        /// and the mover hold it where it lies (<c>JobSystem.TickPawn</c>,
        /// <c>MovementSystem.Advance</c>), exactly as a stun does, and it is published as
        /// <see cref="PawnFlags.KnockedDown"/> until it stands;</item>
        /// <item><see cref="CombatEventKind.KnockedBack"/> is reported, landing cell in the cell and
        /// the cell it was knocked from in the amount.</item>
        /// </list>
        /// Deterministic: no roll here — the chance was rolled with the swing.
        /// </summary>
        public bool KnockBack(Pawn target, Pawn attacker, int weapon, int tick)
        {
            int land = KnockbackCell(_ctx, attacker, target);
            if (land < 0) return false;

            // The player's attack order outlives the fall: a drafted colonist ordered on to a foe
            // gets up and goes back at it, rather than standing drafted and idle one tile off while
            // the foe she was sent at walks up to her (measured: the ordered duel in
            // AttackDriverTests.EveryReportCarriesTheWeapon, 23 swings in 3,000 ticks, fell to 12
            // without this). Read before the job ends, because the cleanup clears the target.
            Job? job = target.CurrentJob;
            bool ordered = job != null && CombatJobs.IsAttack(job.DefIndex) && job.PlayerForced;
            int foe = target.CombatTarget, toTheDeath = job?.DestCell ?? -1;
            int struck = job?.TargetCell ?? -1;
            TraverseMode mode = job?.Mode ?? target.OwnMode;

            int from = target.Cell;
            _jobs.EndJob(target, JobStatus.Failed);
            target.ClearPath();
            target.Destination = -1;
            target.Cell = land;
            target.KnockedDownUntilTick = tick + _ctx.Content.Combat.knockedDownTicks;

            // Given again, held with the rest of it until it stands (JobSystem.TickPawn).
            Pawn? still = ordered ? _ctx.Pawns.Get(new PawnId(foe)) : null;
            if (still != null && !Melee.IsDead(still))
            {
                target.CombatTarget = foe;
                Job again = target.JobBuffer;
                again.Reset(CombatJobs.AttackJobFor(target, _ctx));
                again.TargetCell = still.Cell;
                again.DestCell = toTheDeath;
                again.Mode = mode;
                again.PlayerForced = true;
                if (!_jobs.StartJob(target, again, tick)) target.CombatTarget = 0;
            }
            // An order on a building outlives the fall the same way (design 33 §13e): target 0, the
            // building by its record handle, which the job carried in DestCell.
            else if (ordered && foe == 0 && BuildingTargets.TryStanding(_ctx, toTheDeath, out _))
            {
                Job again = target.JobBuffer;
                again.Reset(JobIndex.AttackMelee);
                again.TargetCell = struck;
                again.DestCell = toTheDeath;
                again.Mode = mode;
                again.PlayerForced = true;
                _jobs.StartJob(target, again, tick);
            }

            _ctx.CombatLog.Report(CombatEventKind.KnockedBack, attacker.Id, target.Id, _ctx.Size.FromIndex(land), tick,
                from, weapon);
            return true;
        }
    }
}
