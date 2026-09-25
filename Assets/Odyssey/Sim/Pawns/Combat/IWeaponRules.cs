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

        /// <summary>
        /// The weapon's quality, a <c>QualityHandle</c> value, or 0 for none (design 47 §11): what
        /// <see cref="WeaponQuality"/> scales its damage and hit chance by.
        /// </summary>
        public readonly byte Quality;

        public Armament(AttackDef attack, int itemDef = -1, byte quality = 0)
        {
            Attack = attack;
            ItemDef = itemDef;
            Quality = quality;
        }

        public bool Armed => ItemDef >= 0;

        /// <summary>
        /// What this swings with when it swings (design 47 §12): a gun's own blow, on the same item
        /// and at the same quality; anything else is itself. Every melee path asks for this, so a
        /// gun-holder in melee never swings with the gun's bullet numbers.
        /// </summary>
        public Armament Melee =>
            Attack.ranged?.melee is AttackDef blow ? new Armament(blow, ItemDef, Quality) : this;
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

        /// <summary>
        /// Put the weapon a pawn's kind arrives holding (<see cref="PawnContent.WeaponOf"/>) into
        /// its hand. Called by <see cref="PawnRegistry.Spawn(int, int)"/> — the one way a pawn is
        /// built, the debug menu's included — for every pawn whose kind names a weapon, after the
        /// pawn is adopted, with <see cref="Pawn.Cell"/> already set. Never called by the loader,
        /// which restores <see cref="Pawn.EquippedItem"/> from the save.
        ///
        /// <para>Lane D's to write (C3): make the item through <c>ColonyItems</c>, take it off the
        /// ground the way the equip job does, and set <see cref="Pawn.EquippedItem"/>. No golden
        /// spawns a kind that names a weapon, so no golden moves.</para>
        /// </summary>
        void ArmOnSpawn(Pawn pawn, PawnContext ctx);
    }
}
