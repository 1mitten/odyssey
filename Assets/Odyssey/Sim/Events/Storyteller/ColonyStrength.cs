#nullable enable
using System;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// What the colony can fight with (design 59 §4): the threat budget reads this, never wealth.
    ///
    /// <para><b>People and weapons only</b> (owner, 2026-09-26). Each standing colonist's power is
    /// <c>health x weapon x skill</c>; buildings, sandbags, walls, turrets, animals, prisoners and
    /// stockpiled weapons are never read, or building a defence would summon a bigger raid — the
    /// wealth meta again in a new form. Each of those has a negative-control test.</para>
    ///
    /// <para>All integers. A power is in thousandths of a hit point a second, scaled by the two
    /// per-mille factors: fists (4 points every 120 ticks) at full health and skill 10 are
    /// 2,000 x 1,000 x 1,000 / 1,000,000 = 2,000.</para>
    /// </summary>
    public static class ColonyStrength
    {
        /// <summary>The skill factor: x0.6 at level 0 to x1.4 at level 20 (INVENTED, design 59 §4a).</summary>
        public static int SkillFactorPerMille(int level) => 600 + 40 * Math.Max(0, Math.Min(20, level));

        /// <summary>
        /// A weapon's hitting power, milli-points a second: its damage over its cooldown, at its
        /// quality's damage, and for a gun its quality's accuracy too.
        /// </summary>
        public static int WeaponPowerMilli(in Armament armament)
        {
            AttackDef attack = armament.Attack;
            int cooldown = Math.Max(1, attack.cooldownTicks);
            long power = (long)attack.damage * 1000 * 60 / cooldown;
            power = power * WeaponQuality.DamagePerMille(armament) / 1000;
            if (attack.IsRanged) power = power * WeaponQuality.AccuracyPerMille(armament) / 1000;
            return (int)power;
        }

        /// <summary>
        /// One colonist's power: 0 if she is not standing; else her health (the fraction of her pool
        /// left, times her consciousness) times her weapon times the skill that weapon uses.
        /// </summary>
        public static int PowerOf(Pawn pawn, PawnContext ctx)
        {
            if (!Melee.IsStanding(pawn)) return 0;
            int max = Math.Max(1, pawn.HpMaxMilli);
            long health = Math.Max(0, Math.Min(1000L, (long)pawn.HpMilli * 1000 / max));
            health = health * pawn.CurrentVitals().ConsciousnessPerMille / 1000;

            Armament arms = ctx.WeaponRules.ArmamentOf(pawn, ctx);
            int skill = pawn.SkillLevel(arms.Attack.IsRanged ? SkillIndex.Shooting : SkillIndex.Melee);
            return (int)(WeaponPowerMilli(arms) * health / 1000 * SkillFactorPerMille(skill) / 1000);
        }

        /// <summary>The colony's strength: every standing colonist's power, summed.</summary>
        public static int Of(PawnContext ctx)
        {
            long total = 0;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (!pawn.IsColonist) continue;
                total += PowerOf(pawn, ctx);
            }
            return (int)Math.Min(int.MaxValue, total);
        }

        /// <summary>
        /// What one raider of a mix is worth, on average: each kind's weapons at Normal quality and
        /// skill 0 (a raider arrives with no rolled skills), full health, weighted by the mix. Fists
        /// for a kind that carries nothing. Never below 1, so a size can always be divided by it.
        /// </summary>
        public static int RaiderPowerOf(PawnContent content, RaidMix mix)
        {
            long total = 0;
            for (int k = 0; k < mix.Kinds.Length; k++)
                total += (long)KindPower(content, mix.Kinds[k]) * mix.PerMille[k] / 1000;
            return (int)Math.Max(1, total);
        }

        static int KindPower(PawnContent content, int kind)
        {
            int skill = SkillFactorPerMille(0);
            if (!content.ArmsOnSpawn(kind))
                return (int)((long)WeaponPowerMilli(new Armament(content.Combat.fists)) * skill / 1000);
            int[] weapons = content.KindWeapons[kind];
            long sum = 0;
            foreach (int item in weapons)
            {
                AttackDef? attack = content.Items[item].weapon;
                Armament arms = attack != null
                    ? new Armament(attack, item, (byte)Odyssey.Sim.Contracts.QualityHandle.Normal)
                    : new Armament(content.Combat.fists);
                sum += (long)WeaponPowerMilli(arms) * skill / 1000;
            }
            return (int)(sum / weapons.Length);
        }
    }
}
