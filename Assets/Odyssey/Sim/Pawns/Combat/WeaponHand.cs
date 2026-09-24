#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The one hand (design 33 §1: "one hand slot"), and the only code that puts a weapon in it or
    /// takes one out. <b>Lane D's file</b> (<c>docs/plans/combat-contracts.md</c>, design 33 §6D).
    ///
    /// <para><b>A held weapon is an ordinary thing in <see cref="ColonyItems"/> with no cell,</b>
    /// carried by its holder (<see cref="ColonyItem.CarriedBy"/>) and named by the holder's
    /// <see cref="Pawn.EquippedItem"/>. It is taken up through <see cref="ColonyItems.PickUp"/> —
    /// the door every lift uses — and put down through <see cref="ColonyItems.Drop"/>, so it leaves
    /// and rejoins the listers exactly as a hauled load does, and every scan that looks for things
    /// to fetch already skips it: they all ask <see cref="PawnContext.WhereIs"/>, which answers -1
    /// for a thing in a pair of hands.</para>
    ///
    /// <para><b>Held is one fact with one owner: the item's carrier.</b> The pawn's field says
    /// which thing; <see cref="Held"/> believes it only while that thing is carried by that pawn.
    /// A field naming a thing on the ground, or a thing somebody else has, arms nobody.</para>
    ///
    /// <para><b>Why the hand is not the job's <see cref="Job.CarriedItem"/>.</b> That field is the
    /// load a job is moving and is dropped when the job ends (<c>JobDriver.DropCarried</c>); a
    /// weapon has to outlast every job she takes, a haul included, so it lives on the pawn. The two
    /// can be true at once — a colonist with a machete in her hand and a log in her arms — and
    /// neither reads the other.</para>
    /// </summary>
    public static class WeaponHand
    {
        /// <summary>The weapon in this pawn's hand, or null for bare hands.</summary>
        public static ColonyItem? Held(Pawn pawn, PawnContext ctx)
        {
            if (pawn.EquippedItem == 0) return null;
            ColonyItem? item = ctx.Items.Get(new ThingId(pawn.EquippedItem));
            return item != null && item.CarriedBy == pawn.Id.Value && item.Cell < 0 && item.ContainerId == 0
                ? item
                : null;
        }

        /// <summary>
        /// Take <paramref name="item"/> into the hand from wherever it is — a cell, a store, or the
        /// arms of the lift that has just raised it — and put down whatever was there.
        ///
        /// <para><b>The new one first, then the old one down</b>, so the cell the new one lay on is
        /// free to take the old one: a colonist swapping a bat for a blade leaves the bat where the
        /// blade was, which is where she is standing. What was there goes to the nearest cell that
        /// can take it (<see cref="PutDown"/>).</para>
        ///
        /// <para>No gesture: the equip job's stoop is already the motion (<c>LiftToil</c>), and a
        /// stow reported now would cut it off halfway down. Arming a spawned pawn is not seen at
        /// all.</para>
        /// </summary>
        public static void TakeUp(Pawn pawn, ColonyItem item, PawnContext ctx)
        {
            if (item.Despawned) throw new System.InvalidOperationException($"thing {item.Id.Value} is gone");
            if (pawn.EquippedItem == item.Id.Value && Held(pawn, ctx) == item) return;

            ColonyItem? old = Held(pawn, ctx);
            ctx.Items.PickUp(item, pawn.Id);
            pawn.EquippedItem = item.Id.Value;
            if (old != null) Lay(ctx, old, pawn.Cell);
        }

        /// <summary>
        /// Put the held weapon down at <paramref name="cell"/>, or at the nearest cell that can take
        /// it, and empty the hand. Returns the thing on the ground, or null when the hand was empty.
        ///
        /// <para>Where a corpse lets go of its weapon, and where a colonist puts down the one she is
        /// swapping. A board with nowhere within <see cref="JobDriver.DropSearchRadius"/> loses it,
        /// the answer <c>JobDriver.DropCarried</c> gives a load for the same reason.</para>
        /// </summary>
        public static ColonyItem? PutDown(Pawn pawn, PawnContext ctx, int cell)
        {
            ColonyItem? held = Held(pawn, ctx);
            pawn.EquippedItem = 0;
            return held != null ? Lay(ctx, held, cell) : null;
        }

        static ColonyItem? Lay(PawnContext ctx, ColonyItem item, int cell)
        {
            int at = ctx.Items.NearestCellWithSpace(ctx.Cells, cell, item.DefIndex, item.Stack, JobDriver.DropSearchRadius);
            if (at >= 0) return ctx.Items.Drop(item, at);
            ctx.Items.Despawn(item);
            return null;
        }
    }
}
