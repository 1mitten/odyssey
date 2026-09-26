#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Expeditions
{
    /// <summary>
    /// Distances and rings on the planet's hex grid (design 64 §5), on top of
    /// <see cref="HexGrid"/>'s neighbours. Offset rows, odd rows shifted half a hex east, wrapping
    /// east to west — so a distance is taken the short way round.
    /// </summary>
    public static class HexMath
    {
        /// <summary>Steps between two tiles, the short way round the planet.</summary>
        public static int Distance(int a, int b, int width)
        {
            int ac = HexGrid.Column(a, width), ar = HexGrid.Row(a, width);
            int bc = HexGrid.Column(b, width), br = HexGrid.Row(b, width);
            int best = int.MaxValue;
            for (int wrap = -1; wrap <= 1; wrap++)
            {
                int d = CubeDistance(ac, ar, bc + wrap * width, br);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// Every tile within <paramref name="radius"/> steps of <paramref name="centre"/>, centre
        /// first, in rings outward and each ring in the order the neighbours are walked, so the list
        /// is the same for the same arguments every time.
        /// </summary>
        public static void Within(int centre, int radius, int width, int height, List<int> into)
        {
            into.Clear();
            into.Add(centre);
            if (radius <= 0) return;
            var seen = new HashSet<int> { centre };
            int from = 0;
            for (int ring = 0; ring < radius; ring++)
            {
                int to = into.Count;
                for (int i = from; i < to; i++)
                {
                    int tile = into[i];
                    int column = HexGrid.Column(tile, width), row = HexGrid.Row(tile, width);
                    for (int d = 0; d < HexGrid.Directions; d++)
                    {
                        int next = HexGrid.Neighbour(column, row, d, width, height);
                        if (next >= 0 && seen.Add(next)) into.Add(next);
                    }
                }
                from = to;
            }
        }

        /// <summary>
        /// Which of the six directions leads from <paramref name="from"/> to its neighbour
        /// <paramref name="to"/>, or -1 if they are not neighbours.
        /// </summary>
        public static int DirectionTo(int from, int to, int width, int height)
        {
            int column = HexGrid.Column(from, width), row = HexGrid.Row(from, width);
            for (int d = 0; d < HexGrid.Directions; d++)
                if (HexGrid.Neighbour(column, row, d, width, height) == to) return d;
            return -1;
        }

        /// <summary>
        /// The side of a board that faces a hex direction (design 64 §6b): the board's north is the
        /// planet's north, so east is east and the two diagonals on each side fall to north or south.
        /// Directions are <see cref="HexGrid"/>'s: 0 east, then anticlockwise.
        /// </summary>
        public static BoardSide SideFacing(int direction)
        {
            switch (direction)
            {
                case 0: return BoardSide.East;
                case 1: case 2: return BoardSide.North;
                case 3: return BoardSide.West;
                default: return BoardSide.South;
            }
        }

        /// <summary>The side opposite: where a party walks on having come from <paramref name="side"/>'s way.</summary>
        public static BoardSide Opposite(BoardSide side)
        {
            switch (side)
            {
                case BoardSide.East: return BoardSide.West;
                case BoardSide.West: return BoardSide.East;
                case BoardSide.North: return BoardSide.South;
                default: return BoardSide.North;
            }
        }

        static int CubeDistance(int ac, int ar, int bc, int br)
        {
            int ax = ac - (ar - (ar & 1)) / 2, az = ar, ay = -ax - az;
            int bx = bc - (br - (br & 1)) / 2, bz = br, by = -bx - bz;
            int dx = System.Math.Abs(ax - bx), dy = System.Math.Abs(ay - by), dz = System.Math.Abs(az - bz);
            int max = dx > dy ? dx : dy;
            return max > dz ? max : dz;
        }
    }
}
