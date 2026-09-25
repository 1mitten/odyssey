#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    // What a blow does to the world (design 33 §3, §6A). Every hit point anybody loses is lost in
    // ApplySwing and nowhere else, so every hook fires from one place and a listener hears each
    // blow exactly once. Lane A's file.
    public partial class CombatSystem
    {
        /// <summary>
        /// Apply one resolved swing — the one method (design 33 §6A). In order:
        /// <list type="number">
        /// <item>the attacker's melee experience for the swing, landed or not (a person only);</item>
        /// <item>the moment reported — <c>Hit</c>, <c>Miss</c> or <c>Dodge</c> — with the
        /// armament's item def, −1 for fists or teeth;</item>
        /// <item>for a hit: the hit points, the <c>Critical</c> moment straight after the
        /// <c>Hit</c> for a critical, <see cref="CombatHooks.RaiseDamageApplied"/>, then death
        /// (deferred), going down, or — for a pawn still on its feet — the stun, the knockback a
        /// critical rolled (design 33 §9b), and the reaction: an animal's revenge roll, a
        /// colonist's retaliation.</item>
        /// </list>
        /// <para>Public so a test can land an exact blow; the only caller in the game is the
        /// resolver, inside <see cref="Tick"/>.</para>
        /// </summary>
        public void ApplySwing(Pawn attacker, Pawn target, in Armament armament, in SwingOutcome outcome, int tick)
        {
            if (attacker.IsPerson && armament.Attack.experiencePerSwing > 0)
                attacker.GainExperience(SkillIndex.Melee, armament.Attack.experiencePerSwing, tick);

            CellRef at = _ctx.Size.FromIndex(target.Cell);
            int weapon = armament.ItemDef;

            // Every swing that reaches her is heard, whatever came of it (design 33 §14f: a missed
            // swing is an attack), from here and nowhere else — before the outcome, so a hit's
            // DamageApplied follows it.
            CombatEventKind result = !outcome.Landed && outcome.Result == CombatEventKind.Dodge
                ? CombatEventKind.Dodge
                : outcome.Landed ? CombatEventKind.Hit : CombatEventKind.Miss;
            _ctx.CombatHooks.RaiseSwingResolved(new SwingReport(target, attacker, result, weapon, tick));

            if (!outcome.Landed)
            {
                CombatEventKind kind = outcome.Result == CombatEventKind.Dodge ? CombatEventKind.Dodge : CombatEventKind.Miss;
                _ctx.CombatLog.Report(kind, attacker.Id, target.Id, at, tick, 0, weapon);
                return;
            }

            int before = target.HpMilli;
            target.HpMilli = before - outcome.DamageMilli;
            _ctx.CombatLog.Report(CombatEventKind.Hit, attacker.Id, target.Id, at, tick, outcome.DamageMilli, weapon);
            // Straight after the Hit it qualifies (design 33 §9b): same tick, same pair, no amount.
            if (outcome.Critical)
                _ctx.CombatLog.Report(CombatEventKind.Critical, attacker.Id, target.Id, at, tick, 0, weapon);
            _ctx.CombatHooks.RaiseDamageApplied(new DamageReport(target, attacker, outcome.DamageMilli, weapon, tick));

            // Past the death line with this blow: dead now, gone at the end of the tick. Only the
            // blow that crosses it, so two blows on one tick cannot kill a pawn twice.
            if (target.HpMilli <= target.DeathAtMilli)
            {
                if (before > target.DeathAtMilli) Kill(target, attacker, weapon, tick);
                return;
            }

            if (target.HpMilli <= 0)
            {
                if (!target.Downed) Down(target, attacker, weapon, tick);
                return;
            }

            if (target.Downed) return;

            if (outcome.StunTicks > 0)
            {
                int until = tick + outcome.StunTicks;
                if (until > target.StunnedUntilTick) target.StunnedUntilTick = until;
                _ctx.CombatLog.Report(CombatEventKind.Stun, attacker.Id, target.Id, at, tick, outcome.StunTicks, weapon);
            }

            // A critical that rolled its knockback, on a target still on its feet: death and the
            // fall were resolved first and returned above (design 33 §9b). Before the reaction, so
            // an animal that runs runs from where it landed.
            if (outcome.Knockback) KnockBack(target, attacker, weapon, tick);

            React(target, attacker, tick);
        }

        /// <summary>
        /// Going down (design 33 §1, §5j): the job in hand ends — keeping the step in progress, so
        /// the body lands where its figure was drawn — the draft and any mental break end with it,
        /// and <c>Job_Downed</c> starts in the same call, so a paused frame already reads "Downed".
        /// </summary>
        public void Down(Pawn pawn, Pawn? by, int weapon, int tick)
        {
            _jobs.Interrupt(pawn, JobStatus.Failed);
            pawn.Drafted = false;
            // Otherwise JobSystem.TickPawn fails the downed job on every tick of the break and the
            // downed node restarts it (design 33 §5j).
            pawn.BreakTicksLeft = 0;
            pawn.Downed = true;
            pawn.CombatTarget = 0;
            // Down outranks knocked down: it lies where it is either way, and the knock-down's clock
            // would only stand it up again in the flags (design 33 §9b).
            pawn.KnockedDownUntilTick = 0;

            Job job = pawn.JobBuffer;
            job.Reset(JobIndex.Downed);
            job.Mode = pawn.OwnMode;
            _jobs.StartJob(pawn, job, tick);

            _ctx.CombatLog.Report(CombatEventKind.Downed, by?.Id ?? default, pawn.Id,
                _ctx.Size.FromIndex(pawn.Cell), tick, 0, weapon);
            _ctx.CombatHooks.RaiseDowned(pawn, by, tick);
        }

        /// <summary>
        /// Getting back up: the downed flag drops and <c>Job_Downed</c> ends as a success; the
        /// pawn's own mind gives it something to do on its next tick.
        /// </summary>
        public void Recover(Pawn pawn, int tick)
        {
            pawn.Downed = false;
            if (pawn.CurrentJob != null && pawn.CurrentJob.DefIndex == JobIndex.Downed)
                _jobs.EndJob(pawn, JobStatus.Succeeded);
            _ctx.CombatLog.Report(CombatEventKind.Recovered, default, pawn.Id, _ctx.Size.FromIndex(pawn.Cell), tick);
        }

        /// <summary>
        /// Death (design 33 §3): reported now, carried out at the end of the tick
        /// (<c>ctx.Defer</c>), never inside a loop over the pawns — <see cref="PawnRegistry.Despawn"/>
        /// shifts the list every such loop walks. Deferred, in order: the corpse, the
        /// <see cref="CombatHooks.RaiseDied"/> hook (the pawn still in the registry, as the hook
        /// promises), the job's end, and the despawn — which releases the pawn's reservations and
        /// beds itself, so they are not released twice here.
        /// </summary>
        public void Kill(Pawn pawn, Pawn? by, int weapon, int tick)
        {
            _ctx.CombatLog.Report(CombatEventKind.Died, by?.Id ?? default, pawn.Id,
                _ctx.Size.FromIndex(pawn.Cell), tick, 0, weapon);
            int from = by?.Cell ?? -1;
            _ctx.Defer(_ => Remove(pawn, by, from, tick));
        }

        void Remove(Pawn pawn, Pawn? by, int from, int tick)
        {
            if (_ctx.Pawns.Get(pawn.Id) != pawn) return;

            byte facing = Melee.FallFacing(_ctx.Size, from, pawn.Cell);
            int corpse = _ctx.Corpses.Add(pawn, tick, facing);
            _ctx.CombatHooks.RaiseDied(pawn, by, corpse, tick);
            _jobs.EndJob(pawn, JobStatus.Failed);
            _ctx.Pawns.Despawn(pawn);
        }

        /// <summary>
        /// Every attack on <paramref name="gone"/> ends now (design 33 §9e): it is dead, or leaving
        /// the board. Called by <see cref="PawnRegistry.Despawn"/>, the one way off the board — the
        /// deferred removal of the dead and a wild animal walking off an edge alike — so no attacker
        /// ends a tick still carrying an attack on a pawn that is not there. Before this each one
        /// carried it to its own next tick, which is the window the §9e guard first caught
        /// (measured: every brawl to the death on six seeds). Through
        /// <see cref="JobSystem.Interrupt"/>, keeping the step in hand, as the order that ends an
        /// attack does. <b>Scales with the pawns on the board</b>, once per pawn that leaves it.
        /// </summary>
        public void EndAttacksOn(Pawn gone)
        {
            var pawns = _ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other != gone && Melee.IsAttacking(other, gone)) _jobs.Interrupt(other, JobStatus.Succeeded);
            }
        }

        /// <summary>
        /// A pawn struck and still standing answers (design 33 §1). An animal rolls its species'
        /// revenge on every blow: turning, it hunts the attacker for
        /// <see cref="CombatDef.revengeTicks"/>; not turning, it runs — unless it had already
        /// turned on this attacker, which a failed roll does not undo. A colonist not under the
        /// player's hand stops what she is doing and fights back against whoever struck her for
        /// <see cref="CombatDef.retaliationTicks"/>. A bandit remembers a colonist hitting it for the
        /// same window and turns on her, unless it is already fighting somebody beside it.
        /// </summary>
        void React(Pawn target, Pawn attacker, int tick)
        {
            CombatDef combat = _ctx.Content.Combat;

            if (!target.IsPerson)
            {
                var roll = DeterministicRandom.ForTick(_ctx.Seed, tick, PawnPurpose.Revenge ^ (uint)target.Id.Value);
                bool turns = roll.NextInt(1_000) < target.Species.revengePerMille;
                bool turned = target.RetaliateAgainst == attacker.Id.Value && tick < target.RetaliateUntilTick;

                if (turns || turned)
                {
                    target.RetaliateAgainst = attacker.Id.Value;
                    target.RetaliateUntilTick = tick + combat.revengeTicks;
                    // Already at it: the job in hand is the right one.
                    if (!Melee.IsAttacking(target, attacker)) _jobs.Interrupt(target, JobStatus.Failed);
                    return;
                }

                Flee(target, attacker, tick);
                return;
            }

            // A bandit struck by a colonist it is not fighting remembers her for the retaliation
            // window, and its hunt prefers her (HostileThinkNode). Chasing somebody else, it turns
            // now. Already trading blows with a colonist beside it, it keeps to her: an interrupt
            // there threw away the swing in the air and left the bandit a whole cooldown, so two
            // colonists could keep it from ever landing a blow — and the re-think chose the
            // nearest, which on a tie was the lower id, not the hitter (review, 2026-09-23).
            if (target.IsHostile)
            {
                if (!attacker.IsColonist || Melee.IsAttacking(target, attacker)) return;
                target.RetaliateAgainst = attacker.Id.Value;
                target.RetaliateUntilTick = tick + combat.retaliationTicks;
                if (!FightingBeside(target)) _jobs.Interrupt(target, JobStatus.Failed);
                return;
            }

            if (!target.IsColonist || target.Drafted || target.IsBroken) return;

            // Whoever struck her, for the window: a colonist (the owner's rule), a bandit or a
            // hog alike. Remembering only the colonist was tried first and a struck colonist
            // stepped out of reach landing the step she was on, found nobody beside her, and went
            // back to wandering while the bandit beat her down (measured).
            target.RetaliateAgainst = attacker.Id.Value;
            target.RetaliateUntilTick = tick + combat.retaliationTicks;

            // Set to Flee (design 33 §18d) she runs rather than fighting back: the interrupt below
            // brings her to her self-defence, which asks her response first. The memory above is
            // still written, for when she is cornered and fights back as Fight back would.
            if (!Melee.IsAttacking(target, attacker)) _jobs.Interrupt(target, JobStatus.Failed);
        }

        /// <summary>
        /// Is <paramref name="pawn"/> attacking somebody standing within reach — in the fight
        /// rather than on the way to one?
        /// </summary>
        bool FightingBeside(Pawn pawn)
        {
            if (pawn.CombatTarget == 0 || pawn.CurrentJob == null || pawn.CurrentJob.DefIndex != JobIndex.AttackMelee)
                return false;
            Pawn? foe = _ctx.Pawns.Get(new PawnId(pawn.CombatTarget));
            return foe != null && Melee.IsStanding(foe) && Melee.InReach(_ctx, pawn, foe, pawn.OwnMode);
        }

        /// <summary>
        /// Run from <paramref name="threat"/>: <c>Job_Flee</c> to <see cref="FleeJobDriver.FindFleeCell"/>,
        /// started now, under the species' own traverse mode. Nowhere to run is no flight.
        /// </summary>
        void Flee(Pawn pawn, Pawn threat, int tick)
        {
            TraverseMode mode = pawn.OwnMode;
            int cell = FleeJobDriver.FindFleeCell(_ctx, pawn, threat.Cell, _ctx.Content.Combat.fleeCells, mode);
            if (cell < 0) return;

            _jobs.Interrupt(pawn, JobStatus.Failed);
            Job job = pawn.JobBuffer;
            job.Reset(JobIndex.Flee);
            job.TargetCell = cell;
            job.Mode = mode;
            _jobs.StartJob(pawn, job, tick);
        }
    }
}
