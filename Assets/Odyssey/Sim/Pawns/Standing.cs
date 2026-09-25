#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <b>May a pawn end up here?</b> (design 50 §5) — the one owner of the question, which is not
    /// the same question as "may a pawn walk through here". Sandbags and a barricade are crossed and
    /// never stood on: a pawn may climb over one on the way somewhere, and may never stop on one to
    /// rest, work, wait, aim or fight. Cover is then always <i>beside</i> a pawn and never under
    /// her, and a line of it seals nobody in, because the cell stays walkable.
    ///
    /// <para><b>Asked by every place that picks where a pawn stops</b>: the walk toil snaps a goal
    /// on such a cell to a neighbour (<see cref="Resolve"/>), a gun is never fired from one
    /// (<c>Ranged.CanShootFrom</c>), a blow never struck from one, a side of a fight never chosen on
    /// one, and eviction never puts anybody on one. <see cref="TrappedPawnSystem"/> moves anybody
    /// who comes to rest on one anyway — the net under whatever the list above missed.</para>
    /// </summary>
    public static class Standing
    {
        /// <summary>Is <paramref name="cell"/> somewhere a pawn may stop — not a cell crossed but never stood on?</summary>
        public static bool CanStandAt(PawnContext ctx, int cell) =>
            (uint)cell < (uint)ctx.Size.CellCount && (ctx.Nav.Grid.Flags[cell] & NavFlags.PassThrough) == 0;

        /// <summary>
        /// Where a pawn asked to stop at <paramref name="cell"/> stops: the cell itself when it may
        /// stand there, else the first neighbour on its layer — the four sides first, then the
        /// corners, then the ring outside — that it can enter and may stand in, in a fixed order so
        /// the answer is the same every time it is asked. -1 when nothing within two cells will do.
        /// </summary>
        public static int Resolve(PawnContext ctx, int cell, TraverseMode mode)
        {
            if (cell < 0 || CanStandAt(ctx, cell)) return cell;
            GridSize size = ctx.Size;
            CellRef at = size.FromIndex(cell);
            NavGrid grid = ctx.Nav.Grid;
            for (int ring = 1; ring <= 2; ring++)
            {
                // Sides before corners at each ring: the nearer answer first.
                for (int pass = 0; pass < 2; pass++)
                for (int dz = -ring; dz <= ring; dz++)
                for (int dx = -ring; dx <= ring; dx++)
                {
                    if (System.Math.Abs(dx) != ring && System.Math.Abs(dz) != ring) continue;
                    bool corner = System.Math.Abs(dx) == System.Math.Abs(dz);
                    if (corner != (pass == 1)) continue;
                    int x = at.X + dx, z = at.Z + dz;
                    if (!size.Contains(x, z, at.Y)) continue;
                    int near = size.Index(x, z, at.Y);
                    if (!grid.CanEnter(near, mode) || !CanStandAt(ctx, near)) continue;
                    return near;
                }
            }
            return -1;
        }
    }
}
