#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The fight's own pass over the pawns (design 33 §3, §6A): where a swing that has reached the
    /// end of its wind-up is resolved and applied, where a stun, a swing clock and a retaliation
    /// run out, and where the hurt heal. <b>Lane A's file</b> (<c>docs/plans/combat-contracts.md</c>).
    /// The outcome of a blow is applied in <c>CombatSystem.Apply.cs</c>, through one method.
    ///
    /// <para><b>Registered in the Pawns phase at order 25</b> — after the job pipeline (20) has
    /// decided who swings, before movement (30) steps anybody — so a blow is decided against the
    /// positions the jobs saw and lands before anyone walks out of reach.</para>
    ///
    /// <para><b>Scales with the pawns on the board, one branch each, plus the swings landing this
    /// tick and the hurt pawns whose needs interval falls on it.</b> A colony at peace pays five
    /// integer comparisons a pawn a tick and changes nothing: no field it touches is set, so
    /// nothing it does reaches the hash, which is why no golden moved. The 20-against-20 row in
    /// <c>TickBenchmarkTests</c> measures it with a fight on.</para>
    ///
    /// <para>Not <see cref="IStateHashable"/>: every piece of combat state it touches is hashed where
    /// it lives — on the pawn (<see cref="Pawn.ContributeTo"/>), in the corpse registry and in the
    /// edifice damage store.</para>
    /// </summary>
    public partial class CombatSystem : IWorldSystem
    {
        readonly PawnContext _ctx;
        readonly JobSystem _jobs;

        /// <param name="jobs">The job pipeline, because going down and dying end a job and start
        /// another — <c>Job_Downed</c> — and every job starts and ends through the pipeline's one
        /// funnel (<see cref="JobSystem.StartJob"/>, <see cref="JobSystem.EndJob"/>,
        /// <c>JobSystem.Interrupt</c>), or a reservation leaks.</param>
        public CombatSystem(PawnContext ctx, JobSystem jobs)
        {
            _ctx = ctx ?? throw new System.ArgumentNullException(nameof(ctx));
            _jobs = jobs ?? throw new System.ArgumentNullException(nameof(jobs));
        }

        /// <summary>The pipeline the fight starts and ends jobs through.</summary>
        public JobSystem Jobs => _jobs;

        public string Name => "Combat";

        public TickPhase Phase => TickPhase.Pawns;

        /// <summary>After the job pipeline (20), before movement (30).</summary>
        public int Order => 25;

        /// <summary>The context the fight reads and writes through: its rules, hooks, log and corpses.</summary>
        public PawnContext Context => _ctx;

        /// <summary>
        /// Scales with the pawns (one branch each), plus the swings landing and the heals due. The
        /// list is walked by index and never changes length inside it: a death is deferred.
        /// </summary>
        public virtual void Tick(SimWorld world)
        {
            _ctx.Sync(world);
            int tick = world.CurrentTick;
            int interval = _ctx.Content.NeedsIntervalTicks;

            // The bullets due (design 47 §2c), before any shot is fired this tick.
            LandDue(tick);

            var pawns = _ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];

                // A gun-holder's reach rule (design 47 §12), before anything lands or fires: an enemy
                // within reach is clubbed, not shot, and one that steps away is shot again.
                SwapByReach(pawn, tick);

                if (pawn.Driver is AttackMeleeJobDriver swing && swing.InWindup) LandOrLose(pawn, swing, tick);
                else if (pawn.Driver is AttackRangedJobDriver aim && aim.InAim) FireOrLose(pawn, aim, tick);

                // The clocks run out: back to nought, so a pawn over its fight carries no combat
                // state and hashes exactly as it did before it (design 33 §6).
                if (pawn.StunnedUntilTick != 0 && tick >= pawn.StunnedUntilTick) pawn.StunnedUntilTick = 0;
                if (pawn.KnockedDownUntilTick != 0 && tick >= pawn.KnockedDownUntilTick) pawn.KnockedDownUntilTick = 0;
                if (pawn.KnockbackImmuneUntilTick != 0 && tick >= pawn.KnockbackImmuneUntilTick) pawn.KnockbackImmuneUntilTick = 0;
                if (pawn.HurlReadyTick != 0 && tick >= pawn.HurlReadyTick) pawn.HurlReadyTick = 0;
                if (pawn.NextSwingTick != 0 && tick >= pawn.NextSwingTick
                    && !(pawn.Driver is AttackMeleeJobDriver { InWindup: true }))
                    pawn.NextSwingTick = 0;
                if (pawn.RetaliateAgainst != 0 && tick >= pawn.RetaliateUntilTick)
                {
                    pawn.RetaliateAgainst = 0;
                    pawn.RetaliateUntilTick = 0;
                }

                // Healing, on the needs cadence and the needs system's own phase spreading, over
                // the hurt only: a whole pawn costs this one comparison.
                if (interval > 0 && (tick + pawn.Id.Value) % interval == 0)
                {
                    if (pawn.HpMilli < pawn.HpMaxMilli) Heal(pawn, tick, interval);
                    // The body (design 43 §4): bleeding, blood, and whether it still stands. A
                    // pawn with nothing on its ledger costs one flag.
                    if (pawn.HasHealthState) TickBody(pawn, tick, interval);
                }
            }
        }

        /// <summary>
        /// A swing in the air: lost if its attacker is stunned, down or dead (design 33 §5j — a
        /// stun is a pause for the job, so the wind-up would otherwise wait out the stun and land
        /// afterwards, which is the swing the stun exists to deny); else, once wound up, applied —
        /// <b>the outcome decided when the wind-up began</b> (design 33 §9g), kept on the pawn,
        /// exactly. A target that has stepped out of reach, died, or gone down on a job that stops
        /// at down since then is a miss: the blow falls on air, and nothing is reported that did
        /// not happen. A swing with no decided outcome — one in the air in a save older than the
        /// decision — is decided here, as every swing was before.
        /// </summary>
        void LandOrLose(Pawn attacker, AttackMeleeJobDriver swing, int tick)
        {
            if (attacker.StunnedAt(tick) || attacker.Downed || Melee.IsDead(attacker))
            {
                swing.EndSwing();
                return;
            }

            Armament armament = _ctx.WeaponRules.ArmamentOf(attacker, _ctx).Melee;
            if (!swing.WindupDone(armament)) return;
            bool decided = attacker.HasPendingSwing;
            SwingOutcome held = attacker.HeldSwing;
            // A sweep's facing, kept with the swing from its wind-up (design 62 §5); nought for
            // every swing that does not sweep.
            int facing = attacker.HeldFacing;
            swing.EndSwing();

            // A building (design 33 §13g): it cannot step away, so the blow lands on it if it still
            // stands and she is still beside it, and falls on nothing if it has gone.
            if (attacker.CombatTarget == 0)
            {
                Job job = attacker.CurrentJob!;
                if (!BuildingTargets.TryStanding(_ctx, job.DestCell, out BuildingTarget building)) return;
                if (!BuildingTargets.InReach(_ctx, attacker.Cell, building)) return;
                SwingOutcome blow = decided ? held : BuildingTargets.Resolve(attacker, armament, building, _ctx, tick);
                StrikeBuilding(attacker, building, BuildingTargets.StruckCell(_ctx, attacker.Cell, building), armament, blow, tick);
                return;
            }

            Pawn? target = _ctx.Pawns.Get(new PawnId(attacker.CombatTarget));
            if (target == null)
            {
                // The target has gone, and the cleaver still sweeps its arc.
                if (facing != 0) SweepFlanks(attacker, facing, attacker.CombatTarget, armament, tick);
                return;
            }

            // Stepped out of reach during the wind-up, or struck down by somebody else before this
            // blow arrived on a job that stops at that: the blow falls on air.
            bool whiff = Melee.IsDead(target)
                || !Melee.InReach(_ctx, attacker, target, attacker.Mode)
                || (target.Downed && attacker.CurrentJob!.DestCell != AttackMeleeJobDriver.ToTheDeath);

            SwingOutcome outcome = whiff
                ? new SwingOutcome(CombatEventKind.Miss)
                : decided ? held : _ctx.MeleeRules.Resolve(attacker, target, armament, _ctx, tick);
            ApplySwing(attacker, target, armament, outcome, tick);

            // A sweep carries on through the two cells either side of the target (design 62 §5),
            // whether the target was struck or had stepped away.
            if (facing != 0) SweepFlanks(attacker, facing, target.Id.Value, armament, tick);
        }

        /// <summary>
        /// One needs interval's healing (design 33 §1): an animal anywhere, at
        /// <see cref="CombatDef.animalHealPerDay"/>; a colonist only lying in a bed, at
        /// <see cref="CombatDef.bedHealPerDay"/>; a hostile never — a bandit stays down until it
        /// is killed. Exact over a day: the fraction a single interval cannot carry is spent by the
        /// interval index, the way <c>Pawn.RestGainPerInterval</c> spends rest's.
        /// </summary>
        void Heal(Pawn pawn, int tick, int interval)
        {
            CombatDef combat = _ctx.Content.Combat;
            int perDay;
            if (!pawn.IsPerson) perDay = combat.animalHealPerDay;
            // A colonist in a bed (design 33 §1), plus a tended injury's own heal wherever she is
            // (design 43 §6, a-02:41): a tended colonist still at work heals too.
            else if (pawn.IsColonist) perDay = (InBed(pawn) ? combat.bedHealPerDay : 0) + TendHealPerDay(pawn);
            // A visitor heals as an animal does, anywhere (design 65 §5): nobody will put a guest to
            // bed, and a downed trader that never healed would lie by the fire for ever.
            else if (pawn.IsVisitor) perDay = combat.animalHealPerDay;
            else return;

            int day = _ctx.Content.DayTicks;
            if (perDay <= 0 || day <= 0) return;

            long index = (tick + pawn.Id.Value) / interval;
            long before = index * perDay * interval / day;
            long after = (index + 1) * perDay * interval / day;
            int amount = (int)(after - before);
            if (amount <= 0) return;

            int hp = pawn.HpMilli + amount;
            if (hp > pawn.HpMaxMilli) amount -= hp - pawn.HpMaxMilli;
            pawn.HpMilli = hp > pawn.HpMaxMilli ? pawn.HpMaxMilli : hp;
            // The same points off the ledger, the worst injury first, in the same call: the pool
            // and the ledger never disagree (design 43 §2).
            pawn.Health?.Heal(amount);

            // Up when whole, for a colonist (design 33 §11c, owner): she heals only in a bed, and a
            // rescued colonist stays in it until she is. The content's threshold is the animals',
            // which heal where they lie. And only once the body lets her (design 43 §3).
            int recoverAt = pawn.IsColonist ? 1_000 : combat.downedRecoverAtPerMille;
            if (pawn.Downed && (long)pawn.HpMilli * 1_000 >= (long)pawn.HpMaxMilli * recoverAt
                && !pawn.CurrentVitals().Incapacitated)
                Recover(pawn, tick);
        }

        /// <summary>
        /// Lying in a bed: down or asleep, on a cell the colony has a bed in. Standing beside one
        /// is not being in it.
        /// </summary>
        bool InBed(Pawn pawn)
        {
            if (!pawn.Downed && !pawn.Asleep) return false;
            var beds = _ctx.Items.Beds;
            int low = 0, high = beds.Count - 1, cell = pawn.Cell;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                int value = beds[mid];
                if (value == cell) return true;
                if (value < cell) low = mid + 1;
                else high = mid - 1;
            }
            return false;
        }
    }
}
