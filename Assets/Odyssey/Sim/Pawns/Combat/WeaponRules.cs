#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The one <see cref="IWeaponRules"/>. <b>Lane D's file</b> (<c>docs/plans/combat-contracts.md</c>,
    /// design 33 §6D).
    ///
    /// <para>What a pawn swings with is the weapon in its hand (<see cref="WeaponHand.Held"/>),
    /// else its species' natural attack, else bare hands. <b>It never rolls</b>: the stun a bat or
    /// a crowbar carries is in the <see cref="Armament"/> it returns, and lane A's
    /// <c>MeleeRules</c> is what rolls it.</para>
    ///
    /// <para>Public, unsealed and virtual, per the code conventions.</para>
    /// </summary>
    public class WeaponRules : IWeaponRules
    {
        public virtual Armament ArmamentOf(Pawn pawn, PawnContext ctx)
        {
            ColonyItem? held = WeaponHand.Held(pawn, ctx);
            if (held != null)
            {
                AttackDef? weapon = ctx.Content.Items[held.DefIndex].weapon;
                if (weapon != null) return new Armament(weapon, held.DefIndex, held.Quality);
            }

            AttackDef? natural = pawn.Species.naturalAttack;
            return natural != null ? new Armament(natural) : new Armament(ctx.Content.Combat.fists);
        }

        /// <summary>
        /// A standing colonist of ours, and a weapon lying somewhere a colonist can take it from —
        /// on a cell or in a store, not in anybody's hands — that the player has not forbidden.
        /// Reachability is the order's question, not this one's: a menu asks this of a weapon
        /// across the board.
        /// </summary>
        public virtual bool CanEquip(Pawn pawn, ColonyItem item, PawnContext ctx)
        {
            if (pawn == null || item == null || item.Despawned) return false;
            if (!pawn.IsColonist || pawn.Downed) return false;
            if ((uint)item.DefIndex >= (uint)ctx.Content.Items.Length) return false;
            if (ctx.Content.Items[item.DefIndex].weapon == null) return false;
            if (item.Forbidden) return false;
            return ctx.WhereIs(item) >= 0;
        }

        /// <summary>
        /// The kind's weapon (<see cref="PawnContent.WeaponOf"/>) into the hand of a pawn just
        /// spawned: made on the nearest cell to the pawn that can take it and taken straight up,
        /// the way the equip job takes one (<see cref="WeaponHand.TakeUp"/>) — so the thing is an
        /// ordinary item from its first tick, and drops at the corpse like any other.
        ///
        /// <para>A board with no room within <see cref="JobDriver.DropSearchRadius"/> of the spawn
        /// leaves the pawn bare-handed rather than putting a thing on top of another; nothing in
        /// the game packs a board that full.</para>
        /// </summary>
        public virtual void ArmOnSpawn(Pawn pawn, PawnContext ctx)
        {
            int def = ctx.Content.WeaponFor(pawn.Kind, pawn.Id.Value, pawn.RollSeed);
            if (def < 0 || pawn.EquippedItem != 0) return;

            int cell = ctx.Items.NearestCellWithSpace(ctx.Cells, pawn.Cell, def, 1, JobDriver.DropSearchRadius);
            if (cell < 0) return;

            ThingId id = ctx.Items.Spawn(def, cell);
            WeaponHand.TakeUp(pawn, ctx.Items.Get(id)!, ctx);
        }
    }
}
