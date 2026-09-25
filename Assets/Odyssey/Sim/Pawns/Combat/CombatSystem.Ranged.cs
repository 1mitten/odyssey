#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    // The gun's two moments (design 47 §2c): the shot fired when an aim completes, and the bullet
    // landing on the tick decided then. Both inside CombatSystem.Tick, for the reason a swing lands
    // there: after the jobs, before anybody steps. Every hit point a bullet takes is taken through
    // ApplySwing, and every one it takes from a building through StrikeBuilding.
    public partial class CombatSystem
    {
        readonly List<Projectiles.Entry> _due = new List<Projectiles.Entry>();

        /// <summary>
        /// A gun-holder's reach rule (design 47 §12; the reference's: "when adjacent to an enemy, pawns
        /// will always fight in melee, even if they are holding a gun"). In a ranged attack with an
        /// enemy within reach, she swaps to a melee attack on it — an aim in hand is lost with no shot
        /// and its clock given back. In a melee attack, between swings, whose target has stepped out of
        /// reach, she swaps back to shooting it. An order carries across when the target is the one she
        /// was ordered on. Here, after the jobs and before anything lands or fires, so no shot is ever
        /// fired at an enemy she could strike. <b>Scales with the pawns on the board</b> for each pawn
        /// in a ranged attack; one reach test for a gun-holder in a melee one; nothing for anybody else.
        /// </summary>
        void SwapByReach(Pawn pawn, int tick)
        {
            Job? job = pawn.CurrentJob;
            if (job == null || pawn.Downed) return;

            if (job.DefIndex == JobIndex.AttackRanged)
            {
                Pawn? close = CombatJobs.EnemyInReach(_ctx, pawn);
                if (close == null) return;
                // The shot never happened: its clock comes back, as when an aim breaks.
                if (pawn.Driver is AttackRangedJobDriver { InAim: true }) pawn.NextSwingTick = 0;
                SwapAttack(pawn, close, JobIndex.AttackMelee, tick);
                return;
            }

            if (job.DefIndex != JobIndex.AttackMelee || pawn.CombatTarget == 0) return;
            if (pawn.Driver is AttackMeleeJobDriver { InWindup: true }) return;
            if (!CombatJobs.IsGunHolder(pawn, _ctx)) return;
            Pawn? target = _ctx.Pawns.Get(new PawnId(pawn.CombatTarget));
            if (target == null || Melee.IsDead(target) || Melee.InReach(_ctx, pawn, target, pawn.OwnMode)) return;
            SwapAttack(pawn, target, JobIndex.AttackRanged, tick);
        }

        /// <summary>
        /// End the attack in hand and start <paramref name="jobDef"/> on <paramref name="foe"/> in its
        /// place, keeping the step in progress (the order's own way), the traverse mode, and — when the
        /// foe is the one she was already on — whether the player forced it and whether it was to the
        /// death or a join, so an order survives the swap. The knockback's re-issue is the precedent.
        /// </summary>
        void SwapAttack(Pawn pawn, Pawn foe, int jobDef, int tick)
        {
            Job job = pawn.CurrentJob!;
            bool same = foe.Id.Value == pawn.CombatTarget;
            bool forced = same && job.PlayerForced;
            int dest = same ? job.DestCell : -1;
            Pathing.TraverseMode mode = job.Mode;

            _jobs.Interrupt(pawn, JobStatus.Succeeded);
            pawn.CombatTarget = foe.Id.Value;
            Job again = pawn.JobBuffer;
            again.Reset(jobDef);
            again.TargetCell = foe.Cell;
            again.DestCell = dest;
            again.Mode = mode;
            again.PlayerForced = forced;
            if (!_jobs.StartJob(pawn, again, tick)) pawn.CombatTarget = 0;
        }
        readonly SightLine _line = new SightLine();

        /// <summary>
        /// Land every bullet due this tick, in the order fired. At the top of the pass, before any
        /// shot is fired this tick, so a bullet fired now can never land now (a flight is at least a
        /// tick). <b>Scales with the bullets landing</b> — nothing at all while none is in the air.
        /// </summary>
        void LandDue(int tick)
        {
            if (_ctx.Projectiles.Count == 0) return;
            _ctx.Projectiles.TakeDue(tick, _due);
            for (int i = 0; i < _due.Count; i++) LandBullet(_due[i], tick);
            _due.Clear();
        }

        /// <summary>
        /// An aim in hand: lost if the shooter is stunned, down or dead (design 33 §5j's rule, as a
        /// swing is); else, once complete, <b>fired</b> — the shot rolled now
        /// (<see cref="IRangedRules.Resolve"/>), against where the target stands now, Shooting trained
        /// hit or miss, the <see cref="PawnGesture.Fire"/> on the serial, the
        /// <see cref="CombatEventKind.Shot"/> reported with the cell the bullet ends in, and the
        /// bullet launched. The target out of range or sight is not fired at — the driver breaks
        /// the aim on the same test before this runs, so it cannot happen here unless the target
        /// died this tick, and then there is nobody to shoot.
        /// </summary>
        void FireOrLose(Pawn shooter, AttackRangedJobDriver aim, int tick)
        {
            if (shooter.StunnedAt(tick) || shooter.Downed || Melee.IsDead(shooter))
            {
                aim.EndAim();
                return;
            }

            Armament armament = _ctx.WeaponRules.ArmamentOf(shooter, _ctx);
            if (!aim.AimDone(armament)) return;
            aim.EndAim();

            Pawn? target = _ctx.Pawns.Get(new PawnId(shooter.CombatTarget));
            RangedDef? ranged = armament.Attack.ranged;
            if (target == null || ranged == null || Melee.IsDead(target)) return;
            if (!Ranged.CanHit(_ctx, shooter.Cell, target.Cell, ranged)) return;

            Fire(shooter, target, armament, tick);
        }

        /// <summary>
        /// Fire one shot at <paramref name="target"/> now: decided, trained, reported and launched.
        /// Public so a test can fire an exact shot; the caller in the game is
        /// <see cref="FireOrLose"/>.
        /// </summary>
        public Projectiles.Entry Fire(Pawn shooter, Pawn target, in Armament armament, int tick)
        {
            ShotOutcome shot = _ctx.RangedRules.Resolve(shooter, target, armament, _ctx, tick);

            // A shot trains Shooting whatever it comes to (design 47 §3b) — here, not at the impact,
            // because a bullet that strikes a wall never reaches ApplySwing.
            if (shooter.IsPerson && armament.Attack.experiencePerSwing > 0)
                shooter.GainExperience(SkillIndex.Shooting, armament.Attack.experiencePerSwing, tick);

            if (shooter.Drafted) shooter.DraftQuietSinceTick = tick;
            shooter.BeginGesture(PawnGesture.Fire);
            _ctx.CombatLog.Report(CombatEventKind.Shot, shooter.Id, target.Id, _ctx.Size.FromIndex(shot.EndCell), tick,
                shot.FlightTicks, armament.ItemDef);

            bool toTheDeath = target.Downed && shooter.CurrentJob != null
                && shooter.CurrentJob.DestCell == AttackMeleeJobDriver.ToTheDeath;
            return _ctx.Projectiles.Launch(shooter.Id.Value, target.Id.Value, armament.ItemDef, shooter.Cell, shot.EndCell,
                tick, tick + shot.FlightTicks, shot.Aimed, toTheDeath, shot.DamageMilli);
        }

        /// <summary>
        /// A bullet arrives (design 47 §2c). The straight line from where it was fired to where it
        /// ends is walked cell by cell, and <b>the first of these takes it</b>:
        /// <list type="number">
        /// <item><b>a blocker</b> — a slab or a corner it cannot pass, solid rock, a wall, a door shut
        /// since it was fired: a building with hit points is struck at ×1, anything else takes a
        /// <see cref="CombatEventKind.Miss"/> reported where it stopped, for the chips;</item>
        /// <item><b>the intended target</b>, on a shot aimed true, standing on any cell of the line —
        /// it may have walked into it — or lying at the end on an order to finish it: a certain hit.
        /// A shot that missed never takes its own target: that is the near miss;</item>
        /// <item><b>a bystander</b> standing on a crossed cell, at its chance
        /// (<see cref="IRangedRules.InterceptPerMille"/>), which is nought within the dead zone round
        /// the shooter. <b>A downed pawn never takes a stray</b>, so an unordered fight still ends in
        /// downs and never deaths (design 33 §3);</item>
        /// <item><b>the ground</b> at the end: a <see cref="CombatEventKind.Miss"/> there, and — if
        /// the shot was at somebody — heard by them as an attack, whether or not it landed.</item>
        /// </list>
        /// A shooter who has died since still lands her bullet, with nobody to credit and nobody to
        /// answer. <b>Scales with the cells on the line</b>, and with the pawns on the board only for
        /// a line that crosses somebody.
        /// </summary>
        void LandBullet(Projectiles.Entry bullet, int tick)
        {
            Pawn? shooter = _ctx.Pawns.Get(new PawnId(bullet.Shooter));
            Pawn? target = bullet.Target != 0 ? _ctx.Pawns.Get(new PawnId(bullet.Target)) : null;
            AttackDef? attack = bullet.Weapon >= 0 && bullet.Weapon < _ctx.Content.Items.Length
                ? _ctx.Content.Items[bullet.Weapon].weapon : null;
            if (attack == null) return;
            var armament = new Armament(attack, bullet.Weapon);
            var hit = new SwingOutcome(CombatEventKind.Hit, bullet.DamageMilli);

            // A shot aimed true lands on its target wherever it now stands (owner, 2026-09-25: "make
            // sure shots that hit actually connect with the target directly"): the line is walked to
            // the target's cell at the impact, so cover it stepped behind or a body that stepped in
            // front still takes it, and nothing else does. The first rule walked to the cell it stood
            // in when fired, and a walking target was missed by most of the shots rolled to hit it.
            bool homing = bullet.Aimed && target != null && !Melee.IsDead(target)
                && (Melee.IsStanding(target) || bullet.ToTheDeath);
            int end = homing ? target!.Cell : bullet.EndCell;
            LineOfSight.Walk(_ctx, bullet.StartCell, end, _line);
            int previous = bullet.StartCell;
            for (int i = 1; i < _line.Count; i++)
            {
                if (_line.IsCorner(i)) continue;
                int cell = _line.Cells[i];

                // Into this cell at all: a slab between layers, or a corner neither way round which
                // is open, stops it at the boundary — it goes down where it was.
                if (!LineOfSight.Passes(_ctx, previous, cell))
                {
                    MissAt(shooter, target, bullet.Weapon, previous, tick);
                    return;
                }

                // Something solid in it. The end cell of a shot aimed true is the target's own, and
                // a pawn stands only where it can — a doorway included — so it is never tested.
                bool aimedEnd = homing && cell == end;
                if (!aimedEnd && LineOfSight.Blocks(_ctx.Cells, _ctx.Nav.Grid, cell))
                {
                    if (BuildingTargets.TryFind(_ctx, cell, out BuildingTarget building))
                    {
                        long scaled = (long)bullet.DamageMilli
                            * BuildingTargets.DamageFactorPerMille(attack.damageKind, building.Stuff) / 1_000;
                        StrikeBuilding(shooter, building, cell, armament,
                            new SwingOutcome(CombatEventKind.Hit, (int)scaled), tick);
                    }
                    else MissAt(shooter, target, bullet.Weapon, cell, tick);
                    return;
                }

                // The intended target, on a shot aimed true.
                if (homing && target!.Cell == cell)
                {
                    ApplySwing(shooter, target, armament, hit, tick);
                    return;
                }

                // A bystander on the line.
                Pawn? stray = Interceptor(bullet, cell, tick);
                if (stray != null)
                {
                    ApplySwing(shooter, stray, armament, hit, tick, stray: true);
                    return;
                }

                previous = cell;
            }

            // Nothing took it: into the ground where it was going.
            MissAt(shooter, target, bullet.Weapon, end, tick);
        }

        /// <summary>
        /// The first standing pawn on <paramref name="cell"/> — not the shooter, not the target —
        /// whose interception roll succeeds, in id order, or null. Each roll on
        /// <see cref="PawnPurpose.RangedIntercept"/> salted by the shooter and the cell, so two
        /// bystanders on two cells never share a draw.
        /// </summary>
        Pawn? Interceptor(Projectiles.Entry bullet, int cell, int tick)
        {
            Pawn? found = null;
            int distance = -1;
            DeterministicRandom roll = default;
            bool rolled = false;
            var pawns = _ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other.Cell != cell || other.Id.Value == bullet.Shooter || other.Id.Value == bullet.Target) continue;
                if (!Melee.IsStanding(other)) continue;

                if (!rolled)
                {
                    distance = RangedGeometry.DistanceMm(_ctx.Size, bullet.StartCell, cell);
                    roll = DeterministicRandom.ForTick(_ctx.Seed, tick,
                        PawnPurpose.RangedIntercept ^ (uint)bullet.Shooter ^ (uint)cell);
                    rolled = true;
                }
                if (roll.NextInt(1_000) < _ctx.RangedRules.InterceptPerMille(other, distance, _ctx))
                {
                    found = other;
                    break;
                }
            }
            return found;
        }

        /// <summary>
        /// The bullet went down at <paramref name="cell"/> having hit nobody: the
        /// <see cref="CombatEventKind.Miss"/> reported there — where the dust goes — and, when it was
        /// fired at somebody still alive, heard by them as an attack (design 33 §12's parity: a
        /// missed swing is an attack), which is how a colonist shot at by a colonist remembers it.
        /// </summary>
        void MissAt(Pawn? shooter, Pawn? target, int weapon, int cell, int tick)
        {
            if (target != null && !Melee.IsDead(target))
                _ctx.CombatHooks.RaiseSwingResolved(new SwingReport(target, shooter, CombatEventKind.Miss, weapon, tick));
            _ctx.CombatLog.Report(CombatEventKind.Miss, shooter?.Id ?? default, target?.Id ?? default,
                _ctx.Size.FromIndex(cell), tick, 0, weapon);
        }
    }
}
