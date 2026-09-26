#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The body (<c>docs/design/43-health.md</c>): the one method every hit point is lost through,
    /// where the loss lands on the body, and the needs-cadence pass that bleeds, recovers blood and
    /// stands the hurt back up.
    /// </summary>
    public partial class CombatSystem
    {
        /// <summary>
        /// <b>The one owner of damage</b> (design 43 §7): a blow and a fall both come through here,
        /// so a fatal fall raises <c>Died</c>, leaves a corpse and is mourned exactly as a blow is,
        /// and there is no second <c>HpMilli -=</c> anywhere to disagree with this one
        /// (<c>docs/bug-patterns.md</c>'s first pattern).
        ///
        /// <para>In order: the pool, the ledger (a person with a body only), the
        /// <see cref="CombatHooks.RaiseDamageApplied"/> hook, then death past the pool's line —
        /// only the blow that crosses it, as before — and going down at nought or when the body
        /// says so (pain shock, consciousness, moving). Returns whether the pawn is still on its
        /// feet, which is what the swing's stun, knockback and reaction wait on.</para>
        /// </summary>
        /// <param name="region">The region it lands on, or -1 to roll one from the set's coverage.</param>
        public bool Hurt(Pawn target, Pawn? attacker, int damageMilli, AfflictionKind kind, HitSet set,
            int weapon, int tick, int region = -1)
        {
            int before = target.HpMilli;
            target.HpMilli = before - damageMilli;
            Record(target, attacker, damageMilli, kind, set, tick, region);
            _ctx.CombatHooks.RaiseDamageApplied(new DamageReport(target, attacker, damageMilli, weapon, tick));

            // Past the death line with this blow: dead now, gone at the end of the tick. Only the
            // blow that crosses it, so two blows on one tick cannot kill a pawn twice.
            if (target.HpMilli <= target.DeathAtMilli)
            {
                if (before > target.DeathAtMilli) Kill(target, attacker, weapon, tick);
                return false;
            }

            if (target.HpMilli <= 0 || target.CurrentVitals().Incapacitated)
            {
                if (!target.Downed) Down(target, attacker, weapon, tick);
                return false;
            }

            // Still standing, and badly hurt with this blow: a raider may give up (design 60 §10).
            // Her fight is over whether or not the blow's stun lands, so she is not standing in it.
            if (!target.Downed && Surrender.Consider(target, before, _ctx, tick)) return false;

            return !target.Downed;
        }

        /// <summary>
        /// A fall of <paramref name="layers"/> (design 43 §7): <c>15 × n^1.5</c> whole points
        /// (a-02:91), scaled by the species' pool against a person's hundred (a-02's health scale),
        /// landed as two to four blunt hits on the bottom-facing regions with a fifth either way
        /// (a-02:101), the worst of them a fracture from two layers. Through <see cref="Hurt"/>, so
        /// a fall that kills is mourned. A pawn with no body takes the points on its pool.
        /// </summary>
        public void Fall(Pawn pawn, int layers, int tick)
        {
            if (layers <= 0 || Melee.IsDead(pawn)) return;
            HealthDef? table = pawn.Body ?? _ctx.Content.HealthOf(PawnKindIndex.Colonist);
            if (table == null) return;

            long total = (long)table.FallDamageMilli(layers) * pawn.Species.healthPoints / 100;
            if (total <= 0) return;

            var roll = DeterministicRandom.ForTick(_ctx.Seed, tick, PawnPurpose.FallSplit ^ (uint)pawn.Id.Value);
            int span = table.fallHitsMax - table.fallHitsMin + 1;
            int hits = table.fallHitsMin + (span > 1 ? roll.NextInt(span) : 0);
            if (hits < 1) hits = 1;

            int each = (int)(total / hits);
            int spread = (int)((long)each * table.fallSpreadPerMille / 1_000);
            var blows = new int[hits];
            int worst = 0;
            long given = 0;
            for (int i = 0; i < hits; i++)
            {
                // The spread moves points between the hits and never the total (review 2026-09-25):
                // spread independently, a five-layer fall's 168 came out anywhere from 134 to 201
                // and a few in a hundred left the colonist alive past design §7's line. The last hit
                // is what is left, which the spread's bound keeps positive for up to five hits.
                int blow = i < hits - 1
                    ? each - spread + (spread > 0 ? roll.NextInt(2 * spread + 1) : 0)
                    : (int)(total - given);
                blows[i] = blow < 1 ? 1 : blow;
                given += blows[i];
                if (blows[i] > blows[worst]) worst = i;
            }

            HealthDef? body = pawn.Body;
            for (int i = 0; i < hits; i++)
            {
                if (_ctx.Pawns.Get(pawn.Id) != pawn || Melee.IsDead(pawn)) return;
                AfflictionKind kind = i == worst && layers >= table.fallFractureFromLayers ? AfflictionKind.Fracture : AfflictionKind.Bruise;
                int region = body != null ? RollRegion(body, pawn, null, HitSet.Fall, tick, i + 1) : -1;
                Hurt(pawn, null, blows[i], kind, HitSet.Fall, -1, tick, region);
            }
        }

        /// <summary>
        /// Put the points on the body: a region by the set's coverage (or the one asked for), what a
        /// limb cannot hold passed to the region it names (a-02:20), the rest kept where it fell. A
        /// pawn with no body keeps the pool alone and this does nothing.
        /// </summary>
        void Record(Pawn target, Pawn? attacker, int milli, AfflictionKind kind, HitSet set, int tick, int region)
        {
            HealthDef? body = target.Body;
            if (body == null || milli <= 0) return;

            if (region < 0 || region >= body.regions.Count) region = RollRegion(body, target, attacker, set, tick, 0);
            PawnHealth health = target.Health ??= new PawnHealth();

            int left = milli;
            for (int guard = 0; guard < body.regions.Count && left > 0; guard++)
            {
                int next = body.OverflowOf(region);
                if (next < 0)
                {
                    health.Add(region, kind, left, tick);
                    return;
                }

                int room = body.RegionMilli(region) - health.RegionDamageMilli(region);
                int take = room <= 0 ? 0 : left < room ? left : room;
                if (take > 0) health.Add(region, kind, take, tick);
                left -= take;
                region = next;
            }
            if (left > 0) health.Add(region, kind, left, tick);
        }

        /// <summary>
        /// A region drawn by coverage on its own stream (<see cref="PawnPurpose.HitRegion"/>), keyed
        /// by the target, the attacker and the hit's index in a fall, so two blows on one tick land
        /// independently and a roll here moves no other stream.
        /// </summary>
        int RollRegion(HealthDef body, Pawn target, Pawn? attacker, HitSet set, int tick, int index)
        {
            int total = body.CoverageTotal(set);
            if (total <= 0) return 0;
            uint key = PawnPurpose.HitRegion ^ (uint)target.Id.Value
                ^ ((uint)(attacker?.Id.Value ?? 0) << 12) ^ ((uint)index << 24) ^ ((uint)set << 28);
            var roll = DeterministicRandom.ForTick(_ctx.Seed, tick, key);
            return body.RegionAt(set, roll.NextInt(total));
        }

        /// <summary>
        /// The body's needs-cadence pass (design 43 §4), for a pawn with anything on its ledger:
        /// wounds bleed, blood comes back once nothing does, blood past the line kills — through
        /// <see cref="Kill"/>, so it is mourned — and the body downs or stands the pawn as its
        /// vitals now say. Exact over a day, by the interval index, as the heal is.
        /// </summary>
        void TickBody(Pawn pawn, int tick, int interval)
        {
            HealthDef? body = pawn.Body;
            PawnHealth? health = pawn.Health;
            if (body == null || health == null || health.IsEmpty) return;

            int day = _ctx.Content.DayTicks;
            if (day <= 0) return;
            long index = (tick + pawn.Id.Value) / interval;

            int bleeding = health.BleedingSeverityMilli;
            if (bleeding > 0)
            {
                // Millionths of blood a day: points in thousandths × per mille a point.
                long perDay = (long)bleeding * body.bleedPerPointPerDay;
                health.BloodLossMicro += Spread(perDay, index, interval, day);
                if (health.BloodLossMicro >= body.bloodDeathAtPerMille * 1_000)
                {
                    health.BloodLossMicro = body.bloodDeathAtPerMille * 1_000;
                    // Dead now as far as every rule this tick can tell (Melee.IsDead reads the pool),
                    // so a blow landing later in the same tick cannot cross the line and report the
                    // death a second time, and the debug kill and a fall leave her alone.
                    pawn.HpMilli = pawn.DeathAtMilli;
                    Kill(pawn, null, -1, tick);
                    return;
                }
            }
            else if (health.BloodLossMicro > 0)
            {
                long perDay = (long)body.bloodRecoveryPerDay * 1_000;
                health.BloodLossMicro -= Spread(perDay, index, interval, day);
                if (health.BloodLossMicro < 0) health.BloodLossMicro = 0;
            }

            Settle(pawn, tick);
        }

        /// <summary>
        /// The body's answer, after anything that changed it: down if it now says so, up if it no
        /// longer does and the pool is where getting up is allowed. A dead pawn is left alone.
        /// </summary>
        void Settle(Pawn pawn, int tick)
        {
            if (Melee.IsDead(pawn)) return;
            if (!pawn.Downed)
            {
                if (pawn.HpMilli <= 0 || pawn.CurrentVitals().Incapacitated) Down(pawn, null, -1, tick);
                return;
            }

            // Up when whole, for a colonist (design 33 §11c), and only once the body lets her too:
            // blood still past its worst stage keeps her down at a full pool (design 43 §3).
            int recoverAt = pawn.HealsAsAColonist ? 1_000 : _ctx.Content.Combat.downedRecoverAtPerMille;
            if ((long)pawn.HpMilli * 1_000 >= (long)pawn.HpMaxMilli * recoverAt && !pawn.CurrentVitals().Incapacitated)
                Recover(pawn, tick);
        }

        /// <summary>One interval's share of an amount a day, exact over the day by the interval index.</summary>
        static int Spread(long perDay, long index, int interval, int day)
        {
            long before = index * perDay * interval / day;
            long after = (index + 1) * perDay * interval / day;
            return (int)(after - before);
        }

        /// <summary>
        /// Thousandths of a point a day a tended injury adds to the heal (a-02:41): 4 at nought
        /// quality up to 12 at full, read at the best tend on the body. Nought with nothing tended.
        /// </summary>
        int TendHealPerDay(Pawn pawn)
        {
            HealthDef? body = pawn.Body;
            PawnHealth? health = pawn.Health;
            if (body == null || health == null) return 0;
            int quality = health.BestTendQualityPerMille;
            if (quality < 0) return 0;
            if (quality > 1_000) quality = 1_000;
            return body.tendHealMinPerDay + (body.tendHealMaxPerDay - body.tendHealMinPerDay) * quality / 1_000;
        }

        /// <summary>
        /// Tend every untended injury on <paramref name="patient"/> at one quality (design 43 §5):
        /// every bleed stops, whatever the quality — a-02:36, the most important fact in the design.
        /// Returns how many were tended.
        /// </summary>
        public int Tend(Pawn patient, int qualityPerMille)
        {
            PawnHealth? health = patient.Health;
            if (health == null) return 0;
            return health.TendAll(qualityPerMille);
        }
    }
}
