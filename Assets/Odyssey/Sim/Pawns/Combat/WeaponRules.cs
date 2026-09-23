#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The one <see cref="IWeaponRules"/>. <b>Lane D's file</b> (<c>docs/plans/combat-contracts.md</c>).
    ///
    /// <para><b>A stub from the contracts step, and a correct one for C2.</b> No weapon can be in
    /// anybody's hand until lane D writes the equip job, so "the natural attack, else bare hands"
    /// is the whole truth of every pawn today, and lane A can fight with it while lane D works.
    /// What lane D adds is the first branch: <see cref="Pawn.EquippedItem"/> resolved through
    /// <c>ColonyItems</c> to its <see cref="ItemDef.weapon"/>. <see cref="CanEquip"/> answers no
    /// until then.</para>
    ///
    /// <para>Public, unsealed and virtual, per the code conventions.</para>
    /// </summary>
    public class WeaponRules : IWeaponRules
    {
        public virtual Armament ArmamentOf(Pawn pawn, PawnContext ctx)
        {
            // Lane D: the equipped weapon comes first, here.
            AttackDef? natural = pawn.Species.naturalAttack;
            return natural != null ? new Armament(natural) : new Armament(ctx.Content.Combat.fists);
        }

        public virtual bool CanEquip(Pawn pawn, ColonyItem item, PawnContext ctx) => false;

        /// <summary>
        /// Nothing until lane D writes it: the marauder's machete (<see cref="PawnContent.WeaponOf"/>)
        /// is named in content and put in the hand here. See <see cref="IWeaponRules.ArmOnSpawn"/>.
        /// </summary>
        public virtual void ArmOnSpawn(Pawn pawn, PawnContext ctx)
        {
        }
    }
}
