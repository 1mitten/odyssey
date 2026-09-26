#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The kit (design 54): the few small stacks a colonist keeps on her, and the only code that
    /// writes <see cref="Pawn.KitItems"/>.
    ///
    /// <para><b>A kit thing is where a held weapon is</b> (<see cref="WeaponHand"/>): an ordinary
    /// thing in <see cref="ColonyItems"/>, carried by her, with no cell and no container. Every
    /// search in the game asks <see cref="PawnContext.WhereIs"/> and skips the -1 it answers for
    /// that, so no hauler, cook, eater, doctor or thief can see into anybody's kit, with no check
    /// written anywhere for it.</para>
    ///
    /// <para><b>A slot is one fact with one owner: the thing's carrier.</b> The pawn's array says
    /// which thing; <see cref="Held"/> believes it only while that thing is carried by her, as
    /// <see cref="WeaponHand.Held"/> does, so a slot whose thing has gone reads as empty rather
    /// than throwing.</para>
    ///
    /// <para>Every loop here is over her slots: a constant, whatever the colony.</para>
    /// </summary>
    public static class Kit
    {
        /// <summary>The belt's two slots. The pack's four arrive with G4.</summary>
        public const int BeltSlots = 2;

        /// <summary>Every slot a colonist has.</summary>
        public const int Slots = BeltSlots;

        /// <summary>The thing in <paramref name="slot"/>, or null for an empty slot.</summary>
        public static ColonyItem? Held(Pawn pawn, PawnContext ctx, int slot)
        {
            if ((uint)slot >= (uint)Slots) return null;
            int id = pawn.KitItems[slot];
            if (id == 0) return null;
            ColonyItem? item = ctx.Items.Get(new ThingId(id));
            return item != null && item.CarriedBy == pawn.Id.Value && item.Cell < 0 && item.ContainerId == 0
                ? item
                : null;
        }

        /// <summary>Which of her slots holds <paramref name="item"/>, or -1.</summary>
        public static int SlotOf(Pawn pawn, PawnContext ctx, ColonyItem item)
        {
            for (int slot = 0; slot < Slots; slot++)
                if (pawn.KitItems[slot] == item.Id.Value && Held(pawn, ctx, slot) == item) return slot;
            return -1;
        }

        /// <summary>Is <paramref name="item"/> in her own kit.</summary>
        public static bool IsHers(Pawn pawn, PawnContext ctx, ColonyItem item) => SlotOf(pawn, ctx, item) >= 0;

        /// <summary>Does anything of this kind go in a kit at all.</summary>
        public static bool Fits(PawnContext ctx, int def) =>
            (uint)def < (uint)ctx.Content.Items.Length && ctx.Content.Items[def].kitCap > 0;

        /// <summary>
        /// How many more of <paramref name="def"/> her kit would take: the room left in every slot
        /// already holding that kind, and a whole slot's cap for every empty one.
        /// </summary>
        public static int Room(Pawn pawn, PawnContext ctx, int def)
        {
            if (!Fits(ctx, def)) return 0;
            int cap = ctx.Content.Items[def].kitCap, room = 0;
            for (int slot = 0; slot < Slots; slot++)
            {
                ColonyItem? held = Held(pawn, ctx, slot);
                if (held == null) room += cap;
                else if (held.DefIndex == def && held.Stack < cap) room += cap - held.Stack;
            }
            return room;
        }

        /// <summary>
        /// How many of <paramref name="def"/> <b>one take</b> may lift (design 54 §3): the room left in
        /// the slots already holding that kind, or — when none has any — one empty slot's cap.
        /// <b>One slot's worth an order</b>, so a stack of eight fills one slot and not both: a second
        /// slot of the same thing is a choice the player makes with a second order, never a side
        /// effect of the stack being big. <see cref="Room"/> is whether a take could fit anything.
        /// </summary>
        public static int TakeRoom(Pawn pawn, PawnContext ctx, int def)
        {
            if (!Fits(ctx, def)) return 0;
            int cap = ctx.Content.Items[def].kitCap, topUp = 0;
            bool empty = false;
            for (int slot = 0; slot < Slots; slot++)
            {
                ColonyItem? held = Held(pawn, ctx, slot);
                if (held == null) empty = true;
                else if (held.DefIndex == def && held.Stack < cap) topUp += cap - held.Stack;
            }
            return topUp > 0 ? topUp : empty ? cap : 0;
        }

        /// <summary>
        /// Put <paramref name="carried"/> — a thing she is already carrying, as a lift or a split
        /// leaves it — into her kit: into the slots of its kind with room first, then an empty one.
        /// Returns how many would not fit, which stay in <paramref name="carried"/> in her arms for
        /// the caller to put down; nought when the caller asked <see cref="Room"/> first.
        /// </summary>
        public static int Put(Pawn pawn, PawnContext ctx, ColonyItem carried)
        {
            if (carried.CarriedBy != pawn.Id.Value || carried.Cell >= 0 || carried.ContainerId != 0)
                throw new System.InvalidOperationException($"thing {carried.Id.Value} is not in her arms");
            if (!Fits(ctx, carried.DefIndex)) return carried.Stack;
            int cap = ctx.Content.Items[carried.DefIndex].kitCap;

            // Topping up first, so two part-stacks of one kind are never left side by side.
            for (int slot = 0; slot < Slots && carried.Stack > 0; slot++)
            {
                ColonyItem? held = Held(pawn, ctx, slot);
                if (held == null || held == carried || held.DefIndex != carried.DefIndex || held.Stack >= cap) continue;
                int moved = System.Math.Min(cap - held.Stack, carried.Stack);
                held.Stack += moved;
                carried.Stack -= moved;
            }
            if (carried.Stack == 0)
            {
                ctx.Items.Despawn(carried);
                return 0;
            }

            for (int slot = 0; slot < Slots && carried.Stack > 0; slot++)
            {
                if (Held(pawn, ctx, slot) != null) continue;
                ColonyItem into = carried.Stack > cap ? ctx.Items.SplitOff(carried, cap, pawn.Id) : carried;
                pawn.KitItems[slot] = into.Id.Value;
                if (into == carried) return 0;
            }
            return carried.Stack;
        }

        /// <summary>
        /// Spend <paramref name="count"/> of the stack in her kit — a treatment's unit, a ration
        /// eaten — and empty the slot when it runs out. The one way a kit thing is used up.
        /// </summary>
        public static void Spend(Pawn pawn, PawnContext ctx, ColonyItem item, int count = 1)
        {
            int slot = SlotOf(pawn, ctx, item);
            if (slot < 0) throw new System.InvalidOperationException($"thing {item.Id.Value} is not in her kit");
            if (item.Stack > count)
            {
                item.Stack -= count;
                return;
            }
            pawn.KitItems[slot] = 0;
            ctx.Items.Despawn(item);
        }

        /// <summary>
        /// Lay the stack in <paramref name="slot"/> at <paramref name="cell"/>, or at the nearest cell
        /// that can take it, and empty the slot; forbidden if <paramref name="forbid"/>. Returns the
        /// thing on the ground, or null for an empty slot or a board with nowhere to put it (which
        /// loses it, the answer <see cref="WeaponHand.PutDown"/> gives for the same reason).
        /// </summary>
        public static ColonyItem? Lay(Pawn pawn, PawnContext ctx, int slot, int cell, bool forbid = false)
        {
            ColonyItem? held = Held(pawn, ctx, slot);
            if ((uint)slot < (uint)Slots) pawn.KitItems[slot] = 0;
            if (held == null) return null;

            int at = ctx.Items.NearestCellWithSpace(ctx.Cells, cell, held.DefIndex, held.Stack, JobDriver.DropSearchRadius);
            if (at < 0)
            {
                ctx.Items.Despawn(held);
                return null;
            }
            ColonyItem laid = ctx.Items.Drop(held, at);
            if (forbid) laid.Forbidden = true;
            return laid;
        }

        /// <summary>
        /// Lay the whole kit at <paramref name="cell"/> (design 54 §5): at a death, until Strip
        /// exists, and when she leaves the board.
        /// </summary>
        public static void LayAll(Pawn pawn, PawnContext ctx, int cell)
        {
            for (int slot = 0; slot < Slots; slot++) Lay(pawn, ctx, slot, cell);
        }

        /// <summary>Is her kit empty — every slot, whatever its id says.</summary>
        public static bool IsEmpty(Pawn pawn, PawnContext ctx)
        {
            for (int slot = 0; slot < Slots; slot++)
                if (Held(pawn, ctx, slot) != null) return false;
            return true;
        }

        /// <summary>The first thing in her kit that heals (design 37: anything whose Def does), or null.</summary>
        public static ColonyItem? Supplies(Pawn pawn, PawnContext ctx)
        {
            for (int slot = 0; slot < Slots; slot++)
            {
                ColonyItem? held = Held(pawn, ctx, slot);
                if (held != null && ctx.Content.Items[held.DefIndex].healPerUnit > 0) return held;
            }
            return null;
        }

        /// <summary>The best food in her kit — the lowest tier, as the eat scan ranks it — or null.</summary>
        public static ColonyItem? Food(Pawn pawn, PawnContext ctx)
        {
            ColonyItem? best = null;
            for (int slot = 0; slot < Slots; slot++)
            {
                ColonyItem? held = Held(pawn, ctx, slot);
                if (held == null) continue;
                ItemDef def = ctx.Content.Items[held.DefIndex];
                if (def.nutrition <= 0) continue;
                if (best == null || def.foodTier < ctx.Content.Items[best.DefIndex].foodTier) best = held;
            }
            return best;
        }
    }
}
