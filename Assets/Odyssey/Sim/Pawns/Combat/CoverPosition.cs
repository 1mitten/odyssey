#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Where a fighter with a gun should stand to shoot from (design 53 §6): a cell near her with a
    /// line to her target and as much cover from it as she can get, not so far that she walks the
    /// fight away. The reference's ranged raiders do this; its drafted colonists do not, and neither
    /// do ours — a drafted colonist holds where the player put her.
    ///
    /// <para><b>Candidates</b> are the cells within <see cref="Radius"/> of her on her own layer that
    /// she may stand in (<see cref="Standing"/>), may shoot from, can reach, that no other fighter
    /// holds, that are in her gun's range with a clear line, and that are <b>no more than
    /// <see cref="BackOffCells"/> farther from the target than she is now</b> — design 47 §12's "no
    /// backing off to shoot … it invites kiting", kept.</para>
    ///
    /// <para><b>Score</b> = <c>4 × the cover the cell has from the target + her hit chance from it / 2
    /// − 60 × the cells to walk</c>, all per mille; the cell she is on wins a tie, then the lower
    /// index. INVENTED weights, the owner's to tune (design 53 §10).</para>
    ///
    /// <para><b>Asked rarely</b>: when her line first opens with no cover to speak of, and again only
    /// when her job is chosen afresh — never per tick. A call is at most 169 candidates, each a
    /// flag read or two and, for the few that pass, one line walk and eight cover reads.</para>
    /// </summary>
    public static class CoverPosition
    {
        /// <summary>How far round her the search looks, in cells (Chebyshev).</summary>
        public const int Radius = 6;

        /// <summary>How much farther from the target than she stands now a candidate may be, in cells.</summary>
        public const int BackOffCells = 2;

        /// <summary>The weight on cover in the score, per unit of cover per mille.</summary>
        public const int CoverWeight = 4;

        /// <summary>What each cell of walking costs in the score.</summary>
        public const int TravelCost = 60;

        /// <summary>
        /// How long into an attack a fighter may still be moving for cover, in ticks. Past it she
        /// shoots from where she stands until the attack is chosen afresh (every
        /// <c>CombatDef.rechooseTicks</c>), so a target that keeps moving — a colonist running —
        /// cannot keep her repositioning for ever and never firing. Read off the tick her job began,
        /// which is saved, so a load changes nothing. INVENTED: about six cells of walking.
        /// </summary>
        public const int SeekWindowTicks = 240;

        /// <summary>How many times the search has run in this process. A diagnostic, never state.</summary>
        public static long Searches;

        /// <summary>
        /// The best cell for <paramref name="me"/> to shoot <paramref name="target"/> from, which may
        /// be the cell she is on; -1 when nowhere in reach has a line.
        /// </summary>
        public static int Find(PawnContext ctx, Pawn me, Pawn target, in Armament armament)
        {
            Searches++;
            RangedDef? ranged = armament.Attack.ranged;
            if (ranged == null) return -1;
            GridSize size = ctx.Size;
            CellRef at = size.FromIndex(me.Cell);
            TraverseMode mode = me.Mode;
            long nowToTarget = Chebyshev(size, me.Cell, target.Cell);

            int best = -1;
            long bestScore = long.MinValue;
            for (int dz = -Radius; dz <= Radius; dz++)
            for (int dx = -Radius; dx <= Radius; dx++)
            {
                int x = at.X + dx, z = at.Z + dz;
                if (!size.Contains(x, z, at.Y)) continue;
                int cell = size.Index(x, z, at.Y);
                if (cell == target.Cell) continue;
                if (Chebyshev(size, cell, target.Cell) > nowToTarget + BackOffCells) continue;
                if (cell != me.Cell)
                {
                    if (!ctx.Nav.Grid.CanEnter(cell, mode)) continue;
                    if (!ctx.CanTravel(me, cell, mode)) continue;
                }
                if (!Ranged.CanShootFrom(ctx, cell)) continue;

                int travel = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz));
                // The most this cell could score, before its line and its cover are asked: a cell
                // that cannot beat the best so far is never walked.
                long ceiling = CoverWeight * 1_000L + 500 - (long)TravelCost * travel;
                if (ceiling < bestScore) continue;

                if (!Ranged.CanHit(ctx, cell, target.Cell, ranged)) continue;
                int cover = ctx.RangedRules.CoverPerMille(target.Cell, cell, ctx);
                int distance = RangedGeometry.DistanceMm(size, cell, target.Cell);
                int aim = ctx.RangedRules.HitChancePerMille(me, distance, armament, ctx);
                long score = (long)CoverWeight * cover + aim / 2 - (long)TravelCost * travel;

                bool better = score > bestScore
                    || (score == bestScore && best != me.Cell && (cell == me.Cell || cell < best));
                if (!better) continue;
                if (cell != me.Cell && Melee.Holds(ctx, me, cell)) continue;
                best = cell;
                bestScore = score;
            }
            return best;
        }

        static long Chebyshev(GridSize size, int a, int b)
        {
            CellRef p = size.FromIndex(a), q = size.FromIndex(b);
            return System.Math.Max(System.Math.Abs(p.X - q.X), System.Math.Abs(p.Z - q.Z));
        }
    }
}
