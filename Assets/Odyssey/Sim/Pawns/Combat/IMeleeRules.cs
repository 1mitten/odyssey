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

        public SwingOutcome(CombatEventKind result, int damageMilli = 0, int stunTicks = 0)
        {
            Result = result;
            DamageMilli = damageMilli;
            StunTicks = stunTicks;
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
    /// on <see cref="PawnPurpose.MeleeDamage"/>, stun on <see cref="PawnPurpose.Stun"/>, each
    /// salted by the attacker's id as the other pawn rolls are.</para>
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
        /// Decide one swing that has reached its wind-up tick: roll hit, then dodge, then the
        /// damage within the spread, then the stun for a blunt blow. Changes nothing — applying
        /// the outcome is <c>CombatSystem</c>'s, so that every hit point anyone loses is lost
        /// through one method and every hook fires from one place.
        /// </summary>
        SwingOutcome Resolve(Pawn attacker, Pawn defender, in Armament armament, PawnContext ctx, int tick);
    }
}
