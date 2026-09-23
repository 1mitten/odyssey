#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Defs;

namespace Odyssey.Sim.Pawns
{
    /// <summary>How a blow does its damage (design 33 §1). Blunt may stun; sharp does not (bleeding waits for a body).</summary>
    public enum DamageKind : byte
    {
        Blunt = 0,
        Sharp = 1,
    }

    /// <summary>
    /// Which family of drawn swing an attack is, so presentation can pick a clip row without the
    /// simulation naming a clip (design 33 §1, <c>docs/research/synty-sword-combat.md</c>): bare
    /// hands, a light one-handed swing, a heavy one, or an animal's bite.
    /// </summary>
    public enum AttackStyle : byte
    {
        Fists = 0,
        Light = 1,
        Heavy = 2,
        Bite = 3,
    }

    /// <summary>
    /// One way of hitting something: a weapon's block (<see cref="ItemDef.weapon"/>), an
    /// animal's teeth (<see cref="SpeciesDef.naturalAttack"/>) or a person's bare hands
    /// (<see cref="CombatDef.fists"/>). One attack per weapon, deliberately — the reference's
    /// weighted choice between edge, point and handle was not taken (a-10).
    ///
    /// <para><b>Damage is in whole points here and thousandths on the pawn.</b> Content reads as
    /// the numbers the owner set (fists about 4, weapons 7–10); the rules multiply by 1,000 when
    /// they apply it, so a heal of a fraction of a point a tick is exact.</para>
    /// </summary>
    public class AttackDef
    {
        /// <summary>Points of damage a landed blow does before the spread.</summary>
        public int damage = 4;

        /// <summary>Ticks from one swing starting to the next one being allowed to start.</summary>
        public int cooldownTicks = 120;

        /// <summary>
        /// Ticks from the swing starting to it landing. The drawn clip is played at the speed that
        /// puts its authored impact frame on this tick.
        /// </summary>
        public int windupTicks = 20;

        public DamageKind damageKind = DamageKind.Blunt;

        /// <summary>Chance per mille that a landed blow stuns. Blunt only; zero for sharp.</summary>
        public int stunPerMille;

        /// <summary>How long a stun lasts, in ticks.</summary>
        public int stunTicks;

        /// <summary>Melee experience one swing is worth, landed or not, in thousandths of a point (the SkillDef unit).</summary>
        public int experiencePerSwing;

        public AttackStyle style = AttackStyle.Fists;
    }

    /// <summary>One point of a level curve: at this skill level, this per mille.</summary>
    public class CurvePoint
    {
        public int level;
        public int perMille;
    }

    /// <summary>
    /// The fight's numbers (design 33 §1, §3), in one record so the rules have one place to read
    /// them and a mod has one place to change them. <c>Combat.xml</c> holds the values and says
    /// which are the owner's and which are invented.
    /// </summary>
    public class CombatDef : Def
    {
        /// <summary>
        /// The attacker's chance to land a swing, per mille, by melee level (owner: 50 % at 0,
        /// 80 % at 10, 90 % at 20). Linear between the points and flat past the ends.
        /// </summary>
        public List<CurvePoint> hitCurve = new List<CurvePoint>();

        /// <summary>
        /// The defender's chance to dodge a swing that would have landed, per mille, by the
        /// <b>defender's</b> melee level (owner: 0 % at 0, 10 % at 10, 30 % at 20).
        /// </summary>
        public List<CurvePoint> dodgeCurve = new List<CurvePoint>();

        /// <summary>A person's bare hands: about 4 damage every two seconds.</summary>
        public AttackDef fists = new AttackDef();

        /// <summary>How far a landed blow may stray from its figure, per mille either way (±20 %).</summary>
        public int damageSpreadPerMille = 200;

        /// <summary>
        /// Hit points a colonist lying in a bed recovers per in-game day, in thousandths
        /// (owner: a colonist heals only in a bed).
        /// </summary>
        public int bedHealPerDay = 20_000;

        /// <summary>Hit points an animal recovers per in-game day wherever it lies, in thousandths
        /// (owner: an animal recovers on its own).</summary>
        public int animalHealPerDay = 12_000;

        /// <summary>
        /// A downed pawn gets back up once its hit points reach this fraction of its pool, per
        /// mille. Above nought on purpose, so a pawn is not knocked down by the first blow after
        /// it rises.
        /// </summary>
        public int downedRecoverAtPerMille = 150;

        /// <summary>How often a chase re-plans its path to a moving target, in ticks.</summary>
        public int chaseRepathTicks = 60;

        /// <summary>How long a colonist struck by a colonist fights back for, in ticks.</summary>
        public int retaliationTicks = 1_200;

        /// <summary>How long an animal that turned stays vengeful, in ticks (the reference's 10,000 floor).</summary>
        public int revengeTicks = 10_000;

        /// <summary>How far a fleeing animal runs from whatever hurt it, in cells.</summary>
        public int fleeCells = 12;

        /// <summary>
        /// The curve's value at a level: linear between the two points either side, flat beyond
        /// the ends, and a flat 0 for an empty curve. Integer throughout, so it hashes and replays
        /// exactly. Content arithmetic, not a rule: what to do with the chance is the rules'.
        /// </summary>
        public static int Evaluate(List<CurvePoint> curve, int level)
        {
            if (curve == null || curve.Count == 0) return 0;
            if (level <= curve[0].level) return curve[0].perMille;

            for (int i = 1; i < curve.Count; i++)
            {
                CurvePoint hi = curve[i];
                if (level > hi.level) continue;

                CurvePoint lo = curve[i - 1];
                int span = hi.level - lo.level;
                if (span <= 0) return hi.perMille;
                return lo.perMille + (hi.perMille - lo.perMille) * (level - lo.level) / span;
            }

            return curve[curve.Count - 1].perMille;
        }

        /// <summary>The hit chance at a melee level, per mille.</summary>
        public int HitChancePerMille(int level) => Evaluate(hitCurve, level);

        /// <summary>The dodge chance at a melee level, per mille.</summary>
        public int DodgeChancePerMille(int level) => Evaluate(dodgeCurve, level);
    }
}
