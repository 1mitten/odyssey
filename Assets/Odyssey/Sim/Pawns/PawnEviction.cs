#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <b>Nobody is inside the world.</b> Where a colonist and a solid thing want the same cell,
    /// this is the one rule that moves the colonist.
    ///
    /// <para>Written 2026-09-21 on the owner's report: <i>"when colonists build a wall — sometimes
    /// they get stuck inside the wall itself. This should never and can't happen."</i> A site is
    /// walkable right up to the instant the wall exists (<c>ConstructionGrid.Raise</c>), and
    /// nothing asked who was standing there, so anyone crossing the cell on that tick was sealed
    /// in: the cell is not walkable any more, so no path can start in it and none can end in it,
    /// and the colonist stands inside a wall until the player deconstructs it.</para>
    ///
    /// <para><b>One owner, because the two callers must agree.</b> Raising a building asks before
    /// it builds, and <see cref="TrappedPawnSystem"/> asks every tick of every pawn, which is what
    /// frees the colonists already walled up in a save from before this existed and covers every
    /// other way a cell can close over somebody — a collapse, a mined ceiling, an edifice the city
    /// generator stamps. Two copies of "where does a displaced colonist go" would drift, and the
    /// symptom of drift is a colonist teleporting across a room.</para>
    /// </summary>
    public static class PawnEviction
    {
        /// <summary>
        /// How far from the cell a displaced colonist may be put. Three cells is generous for a
        /// wall in a corridor and still close enough that the eye reads it as a step aside rather
        /// than a teleport; past that, there is a wall of some thickness and no honest answer.
        /// </summary>
        public const int Radius = 3;

        /// <summary>
        /// The pawn standing in this cell, or stepping into it, or null.
        ///
        /// <para><b>Both, because a step is not instantaneous.</b> A pawn keeps its old
        /// <see cref="Pawn.Cell"/> until it arrives, so asking only where pawns are misses the one
        /// walking into the cell this very tick — which is the commonest way to be built over,
        /// since the builder stands beside the site and the victim is a passer-by.</para>
        /// </summary>
        public static Pawn? Occupant(PawnContext ctx, int cell)
        {
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn.Cell == cell) return pawn;
                if (pawn.HasPath && pawn.Path[pawn.PathIndex] == cell) return pawn;
            }

            return null;
        }

        /// <summary>
        /// Move a pawn out of a cell it must not be in, to the nearest cell it may stand in.
        /// True when it found somewhere; false when the pawn is enclosed in solid world, which is
        /// a case the caller has to decide about rather than one this can answer.
        ///
        /// <para>The search is a widening ring on the pawn's own layer first and then the layers
        /// either side of it, in a fixed order, so two runs of the same save displace the same
        /// colonist to the same cell. It is the walk the pawn would have made, minus the walking:
        /// the path is dropped, the job is left alone, and the pawn re-plans from where it now
        /// stands on the next tick — which is what makes this a shove and not a cancellation.</para>
        /// </summary>
        public static bool Evict(PawnContext ctx, Pawn pawn)
        {
            int to = NearestStandable(ctx, pawn.Cell, pawn.Mode);
            if (to < 0) return false;

            pawn.ClearPath();
            pawn.Cell = to;
            return true;
        }

        /// <summary>
        /// The nearest cell of this layer or the two beside it that this mode may stand in, or -1.
        /// </summary>
        public static int NearestStandable(PawnContext ctx, int from, TraverseMode mode)
        {
            GridSize size = ctx.Size;
            CellRef at = size.FromIndex(from);
            NavGraph nav = ctx.Nav;

            for (int radius = 1; radius <= Radius; radius++)
            for (int dy = 0; dy <= 1; dy++)
            for (int sign = 1; sign >= -1; sign -= 2)
            {
                int y = at.Y + dy * sign;
                if (dy == 0 && sign < 0) continue;      // the pawn's own layer, asked once
                if (y < 0 || y >= size.SizeY) continue;

                for (int dz = -radius; dz <= radius; dz++)
                for (int dx = -radius; dx <= radius; dx++)
                {
                    // The ring, not the square: the inner cells were asked at a smaller radius.
                    if (System.Math.Abs(dx) != radius && System.Math.Abs(dz) != radius) continue;

                    int x = at.X + dx, z = at.Z + dz;
                    if (!size.Contains(x, z, y)) continue;

                    int cell = size.Index(x, z, y);
                    if (!nav.Grid.CanEnter(cell, mode)) continue;
                    if (Occupant(ctx, cell) != null) continue;
                    return cell;
                }
            }

            return -1;
        }
    }
}
