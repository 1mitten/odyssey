#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>What one swing came to, decided and not yet applied. See <see cref="IMeleeRules.Resolve"/>.</summary>
    public readonly struct SwingOutcome
    {
        /// <summary><see cref="CombatEventKind.Hit"/>, <see cref="CombatEventKind.Miss"/> or <see cref="CombatEventKind.Dodge"/>.</summary>
        public readonly CombatEventKind Result;

        /// <summary>Damage to apply, in thousandths of a hit point. Nought unless it hit.</summary>
        public readonly int DamageMilli;

        /// <summary>Ticks of stun to apply. Nought unless a blunt hit rolled one.</summary>
        public readonly int StunTicks;

        /// <summary>
        /// A critical blow (design 33 §9b): <see cref="DamageMilli"/> already carries the
        /// multiplier. Only ever set on a hit.
        /// </summary>
        public readonly bool Critical;

        /// <summary>
        /// The critical rolled a knockback. Whether the target is in fact knocked back is decided at
        /// the impact, against the ground behind it then. Only ever set on a critical.
        /// </summary>
        public readonly bool Knockback;

        public SwingOutcome(CombatEventKind result, int damageMilli = 0, int stunTicks = 0,
            bool critical = false, bool knockback = false)
        {
            Result = result;
            DamageMilli = damageMilli;
            StunTicks = stunTicks;
            Critical = critical;
            Knockback = knockback;
        }

        public bool Landed => Result == CombatEventKind.Hit;
    }

    /// <summary>
    /// The arithmetic of a swing (design 33 §3): who lands, who dodges, how hard, whether it
    /// stuns. <b>Lane A owns this seam</b> (<c>docs/plans/combat-contracts.md</c>): the one
    /// implementation is <see cref="MeleeRules"/>, and nothing else in the simulation decides a
    /// swing.
    ///
    /// <para>Split from <see cref="IWeaponRules"/> by the contracts step so that the fight's lane
    /// and the weapons' lane never edit one file: this interface reads an
    /// <see cref="Armament"/> and never asks where it came from; that one says what a pawn is
    /// holding and never rolls a die.</para>
    ///
    /// <para><b>Deterministic, and every roll on its own stream</b>: hit on
    /// <see cref="PawnPurpose.MeleeHit"/>, dodge on <see cref="PawnPurpose.MeleeDodge"/>, damage
    /// on <see cref="PawnPurpose.MeleeDamage"/>, stun on <see cref="PawnPurpose.Stun"/>, the
    /// critical on <see cref="PawnPurpose.MeleeCritical"/> and the knockback on
    /// <see cref="PawnPurpose.Knockback"/>, each salted by the attacker's id as the other pawn
    /// rolls are.</para>
    /// </summary>
    public interface IMeleeRules
    {
        /// <summary>The melee level a pawn fights at: a person's <see cref="SkillIndex.Melee"/>, an animal's <see cref="SpeciesDef.meleeSkill"/>.</summary>
        int MeleeLevel(Pawn pawn);

        /// <summary>The attacker's chance to land, per mille, off <see cref="CombatDef.hitCurve"/>.</summary>
        int HitChancePerMille(Pawn attacker, PawnContext ctx);

        /// <summary>The defender's chance to dodge a landing swing, per mille, off <see cref="CombatDef.dodgeCurve"/>.</summary>
        int DodgeChancePerMille(Pawn defender, PawnContext ctx);

        /// <summary>
        /// The attacker's chance that a blow which lands is critical, per mille
        /// (<see cref="CombatDef.CritChancePerMille"/> at its melee level, design 33 §9b).
        /// </summary>
        int CriticalChancePerMille(Pawn attacker, PawnContext ctx);

        /// <summary>
        /// The chance a critical blow with this attack knocks its target back, per mille:
        /// <see cref="CombatDef.knockbackBluntPerMille"/> for a blunt one, else
        /// <see cref="CombatDef.knockbackPerMille"/>.
        /// </summary>
        int KnockbackChancePerMille(in Armament armament, PawnContext ctx);

        /// <summary>
        /// Decide one swing <b>on the tick its wind-up begins</b> (design 33 §9g — owner,
        /// 2026-09-23: a critical's slice is heard during the swing, so the outcome must be known
        /// when it starts): roll hit, then dodge, then the damage within the spread, then the stun
        /// for a blunt blow, then the critical and, for a critical, the knockback. Changes nothing —
        /// the attack driver keeps the outcome on the pawn through the wind-up, and applying it is
        /// <c>CombatSystem</c>'s at the impact, so that every hit point anyone loses is lost
        /// through one method and every hook fires from one place.
        /// </summary>
        SwingOutcome Resolve(Pawn attacker, Pawn defender, in Armament armament, PawnContext ctx, int tick);
    }
}
