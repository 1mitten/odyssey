#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The one <see cref="IMeleeRules"/>. <b>Lane A's file</b> (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>A stub from the contracts step.</b> The three reads of content are here because
    /// they are content and nothing more — a level off a skill or a species, a chance off a curve
    /// in <see cref="CombatDef"/>. <see cref="Resolve"/>, which is the rule, throws until lane A
    /// writes it: nothing calls it yet (<see cref="CombatSystem.Tick"/> is empty), and a stub that
    /// quietly answered "miss" would let a missing rule pass for a working one.</para>
    ///
    /// <para>Public, unsealed and virtual, per the code conventions: a mod patches a swing here.</para>
    /// </summary>
    public class MeleeRules : IMeleeRules
    {
        public virtual int MeleeLevel(Pawn pawn) =>
            pawn.IsPerson ? pawn.SkillLevel(SkillIndex.Melee) : pawn.Species.meleeSkill;

        public virtual int HitChancePerMille(Pawn attacker, PawnContext ctx) =>
            ctx.Content.Combat.HitChancePerMille(MeleeLevel(attacker));

        public virtual int DodgeChancePerMille(Pawn defender, PawnContext ctx) =>
            ctx.Content.Combat.DodgeChancePerMille(MeleeLevel(defender));

        public virtual SwingOutcome Resolve(Pawn attacker, Pawn defender, in Armament armament, PawnContext ctx, int tick) =>
            throw new System.NotImplementedException(
                "MeleeRules.Resolve is lane A's to write (docs/plans/combat-contracts.md): the contracts step declared it only.");
    }
}
