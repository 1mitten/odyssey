#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// How far an unsupported rock ceiling may reach from a support (design 62 §5d, §8): <b>the one
    /// owner of the span</b>. The cavern pass leaves pillars so every chamber it carves is within
    /// it, and the cave-in unit (DM7) holds a dug ceiling to the same number; two constants would
    /// be a chamber generated whole that the first support solve then brings down.
    ///
    /// <para><b>The rule.</b> A hole — any cell below the ground that is not solid — is held if,
    /// within <see cref="Cells"/> horizontal four-neighbour steps, there is a <b>supporting
    /// column</b>: one that is solid from the bottom of the world up to and including the hole's
    /// own ceiling, the first solid cell above the hole in the hole's column. That is "a rock-like
    /// cell continuous down to rock below the cave floor", asked of the grid rather than of how
    /// the hole was made, so the same question serves a carved cavern and a dug mine.</para>
    ///
    /// <para><b>Steps are Manhattan distance</b>, not a walk through the rock: the ring search
    /// below visits every column at each distance, whatever is between. The generator's own
    /// guarantee is stricter than this check (it counts only columns with no hole in them at all
    /// up to the chamber's top), so anything it carves passes here.</para>
    /// </summary>
    public static class RockSpan
    {
        /// <summary>The span, in horizontal four-neighbour steps. The owner said "about 6" (Q15).</summary>
        public const int Cells = 6;

        /// <summary>
        /// The layer of the first solid cell above <paramref name="cell"/> in its own column: the
        /// ceiling that has to be held. The top of the world if there is none, which a hole below
        /// ground never meets.
        /// </summary>
        public static int CeilingOf(GridSize size, Func<int, bool> isSolid, int cell)
        {
            CellRef at = size.FromIndex(cell);
            for (int y = at.Y + 1; y < size.SizeY; y++)
                if (isSolid(size.Index(at.X, at.Z, y))) return y;
            return size.SizeY - 1;
        }

        /// <summary>Is the column at (x, z) solid from layer 0 up to and including <paramref name="ceilingY"/>?</summary>
        public static bool SupportsTo(GridSize size, Func<int, bool> isSolid, int x, int z, int ceilingY)
        {
            // Downward from the ceiling: a column with a hole in it fails at the hole, which is
            // near the top of the band for a cavern and so found in a read or two.
            for (int y = ceilingY; y >= 0; y--)
                if (!isSolid(size.Index(x, z, y))) return false;
            return true;
        }

        /// <summary>
        /// Horizontal four-neighbour steps from <paramref name="cell"/> to the nearest supporting
        /// column for its ceiling, or -1 when there is none within <paramref name="limit"/>.
        /// </summary>
        public static int StepsToSupport(GridSize size, Func<int, bool> isSolid, int cell, int limit = Cells)
        {
            CellRef at = size.FromIndex(cell);
            int ceiling = CeilingOf(size, isSolid, cell);

            for (int r = 1; r <= limit; r++)
            {
                // The diamond of radius r: every (dx, dz) with |dx| + |dz| == r.
                for (int dx = -r; dx <= r; dx++)
                {
                    int rest = r - (dx < 0 ? -dx : dx);
                    int x = at.X + dx;
                    if ((uint)x >= (uint)size.SizeX) continue;

                    int z = at.Z + rest;
                    if ((uint)z < (uint)size.SizeZ && SupportsTo(size, isSolid, x, z, ceiling)) return r;
                    if (rest == 0) continue;
                    z = at.Z - rest;
                    if ((uint)z < (uint)size.SizeZ && SupportsTo(size, isSolid, x, z, ceiling)) return r;
                }
            }

            return -1;
        }

        /// <inheritdoc cref="StepsToSupport(GridSize, Func{int, bool}, int, int)"/>
        public static int StepsToSupport(CellGrid grid, int cell, int limit = Cells) =>
            StepsToSupport(grid.Size, grid.IsSolidTerrain, cell, limit);

        /// <summary>
        /// The first of <paramref name="holes"/> whose ceiling is further than
        /// <paramref name="limit"/> from a support, or -1 when every one is held. What a test
        /// asks of a generated cavern, and what DM7 can ask of a mine.
        /// </summary>
        public static int FirstUnsupported(CellGrid grid, IReadOnlyList<int> holes, int limit = Cells)
        {
            Func<int, bool> solid = grid.IsSolidTerrain;
            for (int i = 0; i < holes.Count; i++)
                if (StepsToSupport(grid.Size, solid, holes[i], limit) < 0) return holes[i];
            return -1;
        }
    }
}
