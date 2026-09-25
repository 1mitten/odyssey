#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The one <see cref="IMeleeRules"/>. <b>Lane A's file</b> (<c>docs/plans/combat-contracts.md</c>,
    /// design 33 §6A).
    ///
    /// <para><b>A swing is up to six rolls, each on its own stream</b>, drawn from
    /// <c>(ctx.Seed, tick, salt ^ attacker id)</c> the way every other pawn roll is:</para>
    /// <list type="number">
    /// <item><b>Hit</b> on <see cref="PawnPurpose.MeleeHit"/>, against the attacker's level on
    /// <see cref="CombatDef.hitCurve"/>. A failure is a <see cref="CombatEventKind.Miss"/>.</item>
    /// <item><b>Dodge</b> on <see cref="PawnPurpose.MeleeDodge"/>, against the <b>defender's</b>
    /// level on <see cref="CombatDef.dodgeCurve"/>, made only for a swing that would have landed.
    /// A pawn lying down does not dodge.</item>
    /// <item><b>Damage</b> on <see cref="PawnPurpose.MeleeDamage"/>: the attack's figure in
    /// thousandths, give or take <see cref="CombatDef.damageSpreadPerMille"/> of it, uniformly and
    /// inclusive of both ends.</item>
    /// <item><b>Stun</b> on <see cref="PawnPurpose.Stun"/>, for a <b>blunt</b> blow whose attack
    /// carries a chance. A sharp blow never stuns, whatever its numbers say.</item>
    /// <item><b>Critical</b> on <see cref="PawnPurpose.MeleeCritical"/> (design 33 §9b), for a blow
    /// that landed: <see cref="CombatDef.critChancePerMille"/> plus a little per four attacker
    /// levels, and ×<see cref="CombatDef.critDamagePerMille"/> on the damage. A critical then rolls
    /// its <b>knockback</b> on <see cref="PawnPurpose.Knockback"/>, surer with a blunt weapon.</item>
    /// </list>
    ///
    /// <para><b>Rolled when the wind-up begins, not when it lands</b> (design 33 §9g), so the
    /// tick in each stream is the swing's first. The attack driver asks, keeps the answer on the
    /// pawn through the wind-up, and publishes <see cref="CombatEventKind.SwingCritical"/> in
    /// place of <see cref="CombatEventKind.Swing"/> for a critical that will land.</para>
    ///
    /// <para>Separate streams rather than one stream drawn four times, so a change to one curve —
    /// the dodge chance, say — cannot shift what the hit roll saw: that is the test
    /// <c>CombatMathTests.EachRollIsItsOwnStream</c> holds.</para>
    ///
    /// <para><b>Decides and never applies.</b> What the outcome does to the world is
    /// <see cref="CombatSystem.ApplySwing"/>'s, the one method every hit point is lost through.</para>
    ///
    /// <para>Public, unsealed and virtual, per the code conventions: a mod patches a swing here.</para>
    /// </summary>
    public class MeleeRules : IMeleeRules
    {
        public virtual int MeleeLevel(Pawn pawn) =>
            pawn.IsPerson ? pawn.SkillLevel(SkillIndex.Melee) : pawn.Species.meleeSkill;

        public virtual int HitChancePerMille(Pawn attacker, PawnContext ctx) =>
            ctx.Content.Combat.HitChancePerMille(MeleeLevel(attacker));

        /// <summary>
        /// Nought for a pawn lying down, and for one <b>mid-aim</b> (design 47 §2a, the reference's
        /// rule): a shooter with her eye down the barrel does not step out of a blow.
        /// </summary>
        public virtual int DodgeChancePerMille(Pawn defender, PawnContext ctx) =>
            defender.Downed || Ranged.IsAiming(defender) ? 0 : ctx.Content.Combat.DodgeChancePerMille(MeleeLevel(defender));

        public virtual SwingOutcome Resolve(Pawn attacker, Pawn defender, in Armament armament, PawnContext ctx, int tick)
        {
            uint who = (uint)attacker.Id.Value;

            // The weapon's quality moves the hit chance and the damage (design 47 §11).
            var hit = DeterministicRandom.ForTick(ctx.Seed, tick, PawnPurpose.MeleeHit ^ who);
            if (hit.NextInt(1_000) >= WeaponQuality.Accuracy(HitChancePerMille(attacker, ctx), armament))
                return new SwingOutcome(CombatEventKind.Miss);

            int dodge = DodgeChancePerMille(defender, ctx);
            var dodgeRoll = DeterministicRandom.ForTick(ctx.Seed, tick, PawnPurpose.MeleeDodge ^ who);
            if (dodgeRoll.NextInt(1_000) < dodge)
                return new SwingOutcome(CombatEventKind.Dodge);

            AttackDef attack = armament.Attack;
            int damage = WeaponQuality.Damage(
                DamageMilli(attack, ctx, DeterministicRandom.ForTick(ctx.Seed, tick, PawnPurpose.MeleeDamage ^ who)), armament);

            int stun = 0;
            if (attack.damageKind == DamageKind.Blunt && attack.stunPerMille > 0 && attack.stunTicks > 0)
            {
                var stunRoll = DeterministicRandom.ForTick(ctx.Seed, tick, PawnPurpose.Stun ^ who);
                if (stunRoll.NextInt(1_000) < attack.stunPerMille) stun = attack.stunTicks;
            }

            // The critical (design 33 §9b): half as much again, and a chance to knock the target
            // back — the chance rolled here, the ground behind the target judged at the impact.
            var critRoll = DeterministicRandom.ForTick(ctx.Seed, tick, PawnPurpose.MeleeCritical ^ who);
            if (critRoll.NextInt(1_000) >= CriticalChancePerMille(attacker, ctx))
                return new SwingOutcome(CombatEventKind.Hit, damage, stun);

            damage = (int)((long)damage * ctx.Content.Combat.critDamagePerMille / 1_000);
            var knockRoll = DeterministicRandom.ForTick(ctx.Seed, tick, PawnPurpose.Knockback ^ who);
            bool knockback = knockRoll.NextInt(1_000) < KnockbackChancePerMille(armament, ctx);
            return new SwingOutcome(CombatEventKind.Hit, damage, stun, critical: true, knockback: knockback);
        }

        public virtual int CriticalChancePerMille(Pawn attacker, PawnContext ctx) =>
            ctx.Content.Combat.CritChancePerMille(MeleeLevel(attacker));

        public virtual int KnockbackChancePerMille(in Armament armament, PawnContext ctx) =>
            armament.Attack.damageKind == DamageKind.Blunt
                ? ctx.Content.Combat.knockbackBluntPerMille
                : ctx.Content.Combat.knockbackPerMille;

        /// <summary>
        /// The attack's figure in thousandths, spread uniformly over
        /// <c>[figure − spread, figure + spread]</c>, both ends included, and never below nought.
        /// </summary>
        public static int DamageMilli(AttackDef attack, PawnContext ctx, DeterministicRandom roll)
        {
            int figure = attack.damage * Rates.Scale;
            int spread = (int)((long)figure * ctx.Content.Combat.damageSpreadPerMille / 1_000);
            if (spread < 0) spread = 0;
            int damage = figure - spread + (spread > 0 ? roll.NextInt(2 * spread + 1) : 0);
            return damage < 0 ? 0 : damage;
        }
    }
}
