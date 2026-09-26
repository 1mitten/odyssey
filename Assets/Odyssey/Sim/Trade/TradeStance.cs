#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Trade
{
    /// <summary>Where a negotiator stands to trade (design 65 §6): beside the trader.</summary>
    public static class TradeStance
    {
        /// <summary>The nearest walkable cell beside <paramref name="target"/> the pawn can reach, or -1.</summary>
        public static int Beside(PawnContext ctx, Pawn pawn, int target)
        {
            GridSize size = ctx.Size;
            CellRef at = size.FromIndex(target);
            int best = -1, bestDistance = int.MaxValue;
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                int x = at.X + dx, z = at.Z + dz;
                if (!size.Contains(x, z, at.Y)) continue;
                int cell = size.Index(x, z, at.Y);
                if (!ctx.Cells.IsWalkable(cell)) continue;
                int distance = ctx.Distance(pawn.Cell, cell);
                if (distance >= bestDistance) continue;
                if (!ctx.CanTravel(pawn, cell)) continue;
                bestDistance = distance;
                best = cell;
            }
            return best;
        }

        /// <summary>Is <paramref name="cell"/> one of the eight around <paramref name="target"/>, on its layer?</summary>
        public static bool IsBeside(GridSize size, int cell, int target)
        {
            CellRef a = size.FromIndex(cell), b = size.FromIndex(target);
            return a.Y == b.Y && System.Math.Max(System.Math.Abs(a.X - b.X), System.Math.Abs(a.Z - b.Z)) <= 1;
        }
    }
}
