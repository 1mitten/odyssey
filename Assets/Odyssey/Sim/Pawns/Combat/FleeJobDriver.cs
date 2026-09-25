#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// <c>Job_Flee</c> (design 33 §1, §6A): run up to <see cref="CombatDef.fleeCells"/> from whatever
    /// hurt it — the animal that did not roll its revenge. The destination is chosen once, when
    /// the blow lands (<see cref="CombatSystem.ApplySwing"/>), and carried in
    /// <see cref="Job.TargetCell"/>; the walk is the wander's, under the species' own traverse
    /// mode. It ends on arrival, on a walk that cannot be made, or at the def's expiry. <b>Lane
    /// A's file</b> (<c>docs/plans/combat-contracts.md</c>).
    /// </summary>
    public class FleeJobDriver : JobDriver
    {
        public override bool TryMakeReservations(PawnContext ctx) => true;

        public override JobStatus Tick(PawnContext ctx) => GotoCell(ctx, Job.TargetCell);

        /// <summary>
        /// Somewhere to run to from <paramref name="threatCell"/>: a reachable cell about
        /// <paramref name="cells"/> away, straight away from the threat first, then turning 45° and
        /// then 90° either side, at the full distance and then halving it down to one cell. A fixed
        /// scan, so the answer is a function of the board and never of a die; -1 if nowhere will
        /// do. <b>Scales with nothing</b>: at most twenty-five column searches and reachability
        /// tests, each a few array reads.
        /// </summary>
        public static int FindFleeCell(PawnContext ctx, Pawn pawn, int threatCell, int cells, TraverseMode mode)
        {
            GridSize size = ctx.Size;
            CellRef at = size.FromIndex(pawn.Cell);
            CellRef from = threatCell >= 0 ? size.FromIndex(threatCell) : at;
            int dx = System.Math.Sign(at.X - from.X), dz = System.Math.Sign(at.Z - from.Z);
            if (dx == 0 && dz == 0) dx = 1;

            // The eight headings in clockwise order, so a turn is an index step.
            int heading = HeadingOf(dx, dz);
            int[] turns = { 0, 1, -1, 2, -2 };
            for (int reach = cells; reach >= 1; reach = reach > 1 ? reach / 2 : 0)
            {
                for (int t = 0; t < turns.Length; t++)
                {
                    int h = (heading + turns[t] + 8) & 7;
                    int x = at.X + StepX[h] * reach, z = at.Z + StepZ[h] * reach;
                    if (x < 0) x = 0; else if (x >= size.SizeX) x = size.SizeX - 1;
                    if (z < 0) z = 0; else if (z >= size.SizeZ) z = size.SizeZ - 1;

                    int cell = ctx.Cells.NearestWalkableInColumn(x, z, at.Y);
                    if (cell < 0 || cell == pawn.Cell) continue;
                    if (!ctx.CanTravel(pawn, cell, mode)) continue;
                    return cell;
                }
                if (reach == 1) break;
            }
            return -1;
        }

        // 0 +Z, 1 +Z+X, 2 +X, 3 -Z+X, 4 -Z, 5 -Z-X, 6 -X, 7 +Z-X: the corpse's headings.
        static readonly int[] StepX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        static readonly int[] StepZ = { 1, 1, 0, -1, -1, -1, 0, 1 };

        static int HeadingOf(int dx, int dz)
        {
            for (int h = 0; h < 8; h++)
                if (StepX[h] == dx && StepZ[h] == dz) return h;
            return 0;
        }
    }
}
