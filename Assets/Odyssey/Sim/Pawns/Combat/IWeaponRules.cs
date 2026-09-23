#nullable enable

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// What a pawn fights with right now: the attack, and the item it came from.
    /// <see cref="AttackDef"/> is content and shared, so this is a pair of references and an
    /// integer, never a copy of the numbers.
    /// </summary>
    public readonly struct Armament
    {
        /// <summary>The attack: a weapon's block, an animal's natural attack, or bare hands.</summary>
        public readonly AttackDef Attack;

        /// <summary>The item def index of the weapon, or -1 for bare hands or a natural attack.</summary>
        public readonly int ItemDef;

        public Armament(AttackDef attack, int itemDef = -1)
        {
            Attack = attack;
            ItemDef = itemDef;
        }

        public bool Armed => ItemDef >= 0;
    }

    /// <summary>
    /// What a pawn is holding and whether it may take a weapon up (design 33 §4, C3). <b>Lane D
    /// owns this seam</b> (<c>docs/plans/combat-contracts.md</c>): the one implementation is
    /// <see cref="WeaponRules"/>.
    ///
    /// <para>The other half of the rules the contracts step split in two so that the fight's lane
    /// and the weapons' lane never edit one file. <see cref="IMeleeRules"/> reads an
    /// <see cref="Armament"/> from here and rolls; this never rolls.</para>
    /// </summary>
    public interface IWeaponRules
    {
        /// <summary>
        /// What this pawn swings with: the equipped weapon's block, else the species' natural
        /// attack, else <see cref="CombatDef.fists"/>.
        /// </summary>
        Armament ArmamentOf(Pawn pawn, PawnContext ctx);

        /// <summary>Could this pawn take this item into its hand? A weapon, not forbidden, and a colonist able to.</summary>
        bool CanEquip(Pawn pawn, ColonyItem item, PawnContext ctx);
    }
}
