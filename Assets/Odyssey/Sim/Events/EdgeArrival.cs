#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns.Wildlife;

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// Where something walks on from (design 55 §4, design 65 §5): a side of the board with any
    /// reachable edge cell, a centre drawn on it, and that side's cells sorted by distance from the
    /// centre, nearest first. The raid's and the trader's arrivals share it, so the two cannot come
    /// to disagree about which cells are an edge a pawn can walk in from. Lifted out of the raid's
    /// worker unchanged: the same two draws on the caller's stream, in the same order.
    /// </summary>
    public static class EdgeArrival
    {
        /// <summary>
        /// Pick a side, a centre and the side's slots from <paramref name="census"/>'s edge cells,
        /// drawing on <paramref name="purpose"/>'s stream. False when the census has no edge.
        /// </summary>
        public static bool TryPick(IncidentContext ctx, SurfaceCensus census, uint purpose,
            out int side, out int centre, out List<int> slots)
        {
            side = -1;
            centre = -1;
            slots = new List<int>();
            if (census.Edge.Count == 0) return false;

            GridSize grid = ctx.Size;
            var sides = new List<int>[4];
            for (int s = 0; s < 4; s++) sides[s] = new List<int>();
            for (int i = 0; i < census.Edge.Count; i++) sides[SideOf(grid, census.Edge[i])].Add(census.Edge[i]);
            var draw = ctx.Random(purpose);
            int open = 0;
            for (int s = 0; s < 4; s++) if (sides[s].Count > 0) open++;
            int pick = draw.NextInt(open);
            for (int s = 0; s < 4; s++)
            {
                if (sides[s].Count == 0) continue;
                if (pick-- == 0) { side = s; break; }
            }
            slots = sides[side];
            int c0 = slots[draw.NextInt(slots.Count)];
            centre = c0;
            CellRef c = grid.FromIndex(c0);
            slots.Sort((a, b) =>
            {
                CellRef pa = grid.FromIndex(a), pb = grid.FromIndex(b);
                int da = Math.Abs(pa.X - c.X) + Math.Abs(pa.Z - c.Z);
                int db = Math.Abs(pb.X - c.X) + Math.Abs(pb.Z - c.Z);
                return da != db ? da.CompareTo(db) : a.CompareTo(b);
            });
            return true;
        }

        /// <summary>West 0, east 1, south 2, north 3; a corner is its x side.</summary>
        public static int SideOf(GridSize size, int cell)
        {
            CellRef at = size.FromIndex(cell);
            if (at.X == 0) return 0;
            if (at.X == size.SizeX - 1) return 1;
            return at.Z == 0 ? 2 : 3;
        }
    }
}
