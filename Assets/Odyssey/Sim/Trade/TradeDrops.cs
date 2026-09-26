#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Trade
{
    /// <summary>
    /// Where what the colony bought is set down (design 65 §4): beside the trader, a stack at a time
    /// by each item's stack limit, through <see cref="ColonyItems.NearestCellWithSpace"/> — so a
    /// bought stack obeys the rule every drop obeys and never lands in a tree, on a bed or on a
    /// stack of something else. <b>Every cell is planned before anything changes</b>: a deal with
    /// nowhere to put what it bought is refused whole rather than half-delivered.
    /// </summary>
    public static class TradeDrops
    {
        /// <summary>How far from the trader a bought stack may land, in cells. INVENTED.</summary>
        public const int MaxRadius = 6;

        /// <summary>One stack to set down: what, how many, and where.</summary>
        public readonly struct Drop
        {
            public readonly int Item;
            public readonly int Count;
            public readonly int Cell;

            public Drop(int item, int count, int cell)
            {
                Item = item;
                Count = count;
                Cell = cell;
            }
        }

        /// <summary>
        /// Plan every stack of <paramref name="goods"/> (item, count) beside <paramref name="origin"/>,
        /// no two on one cell, into <paramref name="plan"/>. False, with the plan incomplete, when
        /// something has nowhere to go.
        /// </summary>
        public static bool TryPlan(PawnContext ctx, int origin, IReadOnlyList<(int Item, int Count)> goods, List<Drop> plan)
        {
            plan.Clear();
            var used = new HashSet<int>();
            for (int g = 0; g < goods.Count; g++)
            {
                int item = goods[g].Item;
                int left = goods[g].Count;
                int limit = System.Math.Max(1, ctx.Content.Items[item].stackLimit);
                while (left > 0)
                {
                    int n = System.Math.Min(limit, left);
                    int cell = ctx.Items.NearestCellWithSpace(ctx.Cells, origin, item, n, MaxRadius, c => !used.Contains(c));
                    if (cell < 0) return false;
                    used.Add(cell);
                    plan.Add(new Drop(item, n, cell));
                    left -= n;
                }
            }
            return true;
        }

        /// <summary>
        /// Set a plan down. A bought weapon is Normal: the trader sells serviceable goods, and a price
        /// that ignores quality should not buy a lottery ticket (design 65 §3).
        /// </summary>
        public static void Apply(PawnContext ctx, IReadOnlyList<Drop> plan)
        {
            for (int i = 0; i < plan.Count; i++)
            {
                Drop drop = plan[i];
                ThingId id = ctx.Items.Spawn(drop.Item, drop.Cell, drop.Count);
                if (ctx.Content.Items[drop.Item].weapon != null)
                {
                    ColonyItem? made = ctx.Items.Get(id);
                    if (made != null && made.Quality == 0) made.Quality = (byte)QualityHandle.Normal;
                }
            }
        }
    }
}
