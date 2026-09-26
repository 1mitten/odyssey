#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Trade
{
    /// <summary>
    /// What the colony can trade (design 57 §4): every stack in a store — a stockpile cell or a
    /// shelf, the set the Inventory tab counts — and every loose stack within
    /// <see cref="TradeRadius"/> of the trader, on its layer. Nothing carried, nothing forbidden and
    /// nothing a job has claimed. The radius is what lets gold a deal has just paid out, set down
    /// beside the trader, be spent in the next deal before anybody has hauled it.
    ///
    /// <para><b>Scales with the stacks in store</b>, and is asked only while a trader is on the
    /// board: once per publish of a ready session, and once per commit (process §3).</para>
    /// </summary>
    public static class ColonyTradeStock
    {
        /// <summary>How near the trader a loose stack must lie to count, in cells. INVENTED.</summary>
        public const int TradeRadius = 4;

        /// <summary>
        /// How many of each item the colony can trade, into <paramref name="counts"/> (one per item
        /// def), and the worst quality of each into <paramref name="worst"/> (0 for none).
        /// </summary>
        public static void Count(PawnContext ctx, int traderCell, int[] counts, byte[]? worst = null)
        {
            Array.Clear(counts, 0, counts.Length);
            if (worst != null) Array.Clear(worst, 0, worst.Length);
            ForEach(ctx, traderCell, item =>
            {
                if ((uint)item.DefIndex >= (uint)counts.Length) return;
                counts[item.DefIndex] += item.Stack;
                if (worst != null && item.Quality != 0
                    && (worst[item.DefIndex] == 0 || item.Quality < worst[item.DefIndex]))
                    worst[item.DefIndex] = item.Quality;
            });
        }

        /// <summary>
        /// Take <paramref name="count"/> of an item out of what the colony can trade: the smallest
        /// stacks first, so part-used stacks empty before full ones are split, and the worst weapon
        /// first. Returns how many were taken, which is <paramref name="count"/> whenever
        /// <see cref="Count"/> said there were that many.
        /// </summary>
        public static int Take(PawnContext ctx, int traderCell, int def, int count)
        {
            if (count <= 0) return 0;
            var stacks = new List<ColonyItem>();
            ForEach(ctx, traderCell, item => { if (item.DefIndex == def) stacks.Add(item); });
            stacks.Sort((a, b) =>
            {
                int byQuality = a.Quality.CompareTo(b.Quality);
                if (byQuality != 0) return byQuality;
                int byStack = a.Stack.CompareTo(b.Stack);
                return byStack != 0 ? byStack : a.Id.Value.CompareTo(b.Id.Value);
            });

            int taken = 0;
            for (int i = 0; i < stacks.Count && taken < count; i++)
            {
                ColonyItem stack = stacks[i];
                int from = Math.Min(stack.Stack, count - taken);
                taken += from;
                if (from >= stack.Stack) ctx.Items.Despawn(stack);
                else stack.Stack -= from;
            }
            return taken;
        }

        /// <summary>Is this stack the colony's to trade from here?</summary>
        public static bool Tradeable(PawnContext ctx, int traderCell, ColonyItem item, bool loose)
        {
            if (item.Despawned || item.CarriedBy != 0 || item.Forbidden) return false;
            if (ctx.Reservations.IsReservedByAnyone(ReservationManager.Key(ReservationTargetKind.Item, item.Id.Value)))
                return false;
            if (!loose) return true;
            if (item.Cell < 0) return false;
            CellRef a = ctx.Size.FromIndex(item.Cell), t = ctx.Size.FromIndex(traderCell);
            return a.Y == t.Y && Math.Max(Math.Abs(a.X - t.X), Math.Abs(a.Z - t.Z)) <= TradeRadius;
        }

        static void ForEach(PawnContext ctx, int traderCell, Action<ColonyItem> visit)
        {
            IReadOnlyList<ColonyItem> items = ctx.Items.Items;
            Visit(ctx, traderCell, items, ctx.Items.StoredItems, false, visit);
            Visit(ctx, traderCell, items, ctx.Items.ContainedItems, false, visit);
            Visit(ctx, traderCell, items, ctx.Items.LooseItems, true, visit);
        }

        static void Visit(PawnContext ctx, int traderCell, IReadOnlyList<ColonyItem> items, IReadOnlyList<int> list,
            bool loose, Action<ColonyItem> visit)
        {
            // A copy of the indices, because taking from a stack can move it between listers.
            var indices = new int[list.Count];
            for (int i = 0; i < list.Count; i++) indices[i] = list[i];
            for (int i = 0; i < indices.Length; i++)
            {
                int index = indices[i];
                if ((uint)index >= (uint)items.Count) continue;
                ColonyItem item = items[index];
                if (Tradeable(ctx, traderCell, item, loose)) visit(item);
            }
        }
    }
}
