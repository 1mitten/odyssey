#nullable enable
using System;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;

namespace Odyssey.Sim.Events
{
    /// <summary>
    /// What a raid member does because it is in a raid (design 55 §3, §5), ahead of the bandit's
    /// own mind in <c>JobSystem.HostileTree</c>. It asks the member's group what the band is doing:
    /// <list type="bullet">
    /// <item><b>Arriving, gathering, probing</b>: mill about the band's point — a short walk within
    /// <see cref="RaidGroup.MillRadius"/> of it, or a wait.</item>
    /// <item><b>Assaulting</b>: walk toward the target a leg at a time, and <b>hand over</b> —
    /// return false, so the tree falls through to <see cref="HostileThinkNode"/> — whenever there is
    /// something to fight: a colonist near, a grudge, the target reached, or a target it cannot
    /// reach. The fighting is the bandit's own mind, unchanged (design 55 §12).</item>
    /// <item><b>Withdrawing</b>: walk to the nearest edge and leave (<see cref="Theft.FillLeave"/>).</item>
    /// </list>
    /// <para>A pawn in no raid is declined after one dictionary lookup, so a debug-spawned bandit
    /// thinks exactly as it did before raids. So is one that remembers who struck it, in every phase
    /// but the withdrawal: a poked band fights back rather than milling while it is hit.</para>
    /// </summary>
    public sealed class RaidThinkNode : ThinkNode
    {
        /// <summary>A standing colonist this near, in cells, and an assaulting member fights rather than walks. INVENTED.</summary>
        public const int EngageCells = 12;

        /// <summary>This near the target, in cells, and the member has arrived: it fights what is there. INVENTED.</summary>
        public const int ArriveCells = 6;

        /// <summary>How far an assaulting member walks before it thinks again, in cells. INVENTED.</summary>
        public const int LegCells = 16;

        /// <summary>A milling member's wait: this long, plus up to <see cref="WaitSpreadTicks"/>. Two to six seconds.</summary>
        public const int WaitTicks = 120;

        public const int WaitSpreadTicks = 240;

        public override string Name => "Raid";

        public override bool TryGiveJob(Pawn pawn, PawnContext ctx, Job job)
        {
            RaidGroup? group = ctx.Raids?.GroupOf(pawn.Id.Value);
            if (group == null || pawn.Downed) return false;
            TraverseMode mode = pawn.OwnMode;

            if (group.Phase == RaidPhase.Withdrawing)
            {
                if (Theft.FillLeave(ctx, pawn, mode, job)) return true;
                // No edge it can reach: let it go, or it holds the band open for ever (design 55 §6).
                ctx.Raids!.Release(pawn);
                return false;
            }
            if (pawn.RetaliateAgainst != 0 && ctx.CurrentTick < pawn.RetaliateUntilTick)
            {
                if (group.Phase == RaidPhase.Assaulting) ctx.Raids!.Engage(pawn);
                return false;
            }

            switch (group.Phase)
            {
                case RaidPhase.Arriving:
                case RaidPhase.Gathering:
                    return Mill(pawn, ctx, job, group.GatherCell, group.MillRadius, mode);
                case RaidPhase.Probing:
                    return Mill(pawn, ctx, job, group.ProbeCell, group.MillRadius, mode);
                default:
                    // Once it has turned to fight, the fight is its own mind's until the withdrawal:
                    // a chase that re-chooses must not send it back to the target (RaidMember.Engaged).
                    if (IsEngaged(group, pawn.Id.Value)) return false;
                    if (Advance(pawn, ctx, job, group.TargetCell, mode)) return true;
                    ctx.Raids!.Engage(pawn);
                    return false;
            }
        }

        static bool IsEngaged(RaidGroup group, int pawn)
        {
            for (int i = 0; i < group.Members.Count; i++)
                if (group.Members[i].Pawn == pawn) return group.Members[i].Engaged;
            return false;
        }

        /// <summary>
        /// About the band's point: a wait if it is there already and the draw says so, else a walk to
        /// a reachable cell within the radius. Never declines — a member with nowhere to walk waits —
        /// or it would fall through to the hunt and break the band's cover.
        /// </summary>
        static bool Mill(Pawn pawn, PawnContext ctx, Job job, int centre, int radius, TraverseMode mode)
        {
            var rng = DeterministicRandom.ForTick(ctx.Seed, ctx.CurrentTick, RaidPurpose.Mill ^ (uint)pawn.Id.Value);
            GridSize size = ctx.Size;
            CellRef c = size.FromIndex(centre);
            bool there = Cells(size, pawn.Cell, centre) <= radius;

            if (!there || rng.NextInt(2) == 0)
            {
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    int x = c.X + rng.NextInt(-radius, radius + 1);
                    int z = c.Z + rng.NextInt(-radius, radius + 1);
                    if (x < 0 || z < 0 || x >= size.SizeX || z >= size.SizeZ) continue;
                    int cell = ctx.Cells.NearestWalkableInColumn(x, z, c.Y);
                    if (cell < 0 || cell == pawn.Cell || !ctx.Reachable(pawn, cell, mode)) continue;
                    job.Reset(JobIndex.Wander);
                    job.TargetCell = cell;
                    job.Mode = mode;
                    return true;
                }
            }

            job.Reset(JobIndex.Wait);
            job.WorkTicks = WaitTicks + rng.NextInt(WaitSpreadTicks + 1);
            return true;
        }

        /// <summary>
        /// A leg toward the target, or false to let the bandit's own mind fight: a standing colonist
        /// within <see cref="EngageCells"/>, the target within <see cref="ArriveCells"/>, or a target
        /// it cannot reach — walled in, when its own mind breaks the nearest colony building or hunts
        /// the nearest colonist it can reach, exactly as a lone bandit does.
        /// </summary>
        static bool Advance(Pawn pawn, PawnContext ctx, Job job, int target, TraverseMode mode)
        {
            GridSize size = ctx.Size;
            if ((uint)target >= (uint)size.CellCount) return false;
            if (ColonistWithin(ctx, pawn, EngageCells, mode)) return false;
            if (Cells(size, pawn.Cell, target) <= ArriveCells) return false;
            if (!ctx.Reachable(pawn, target, mode)) return false;

            job.Reset(JobIndex.Wander);
            job.TargetCell = LegToward(ctx, pawn, target, mode);
            job.Mode = mode;
            return true;
        }

        /// <summary>
        /// The cell <see cref="LegCells"/> along the straight line toward the target, dropped to the
        /// nearest standable cell in its column, or the target itself when that is nearer or the
        /// point on the line cannot be reached.
        /// </summary>
        static int LegToward(PawnContext ctx, Pawn pawn, int target, TraverseMode mode)
        {
            GridSize size = ctx.Size;
            CellRef a = size.FromIndex(pawn.Cell);
            CellRef t = size.FromIndex(target);
            int dx = t.X - a.X, dz = t.Z - a.Z;
            int d = Math.Max(Math.Abs(dx), Math.Abs(dz));
            if (d <= LegCells) return target;

            int x = a.X + dx * LegCells / d;
            int z = a.Z + dz * LegCells / d;
            int y = a.Y + (t.Y - a.Y) * LegCells / d;
            int cell = ctx.Cells.NearestWalkableInColumn(x, z, y);
            return cell >= 0 && cell != pawn.Cell && ctx.Reachable(pawn, cell, mode) ? cell : target;
        }

        /// <summary>
        /// A standing colonist within <paramref name="reach"/> cells on the ground plan that the
        /// member can reach. One pass over the pawns, and a reachability question only for one that
        /// is near: a colonist behind a wall is not something to fight, and handing over to it would
        /// send the member off after whoever else its own mind finds nearest.
        /// </summary>
        static bool ColonistWithin(PawnContext ctx, Pawn pawn, int reach, TraverseMode mode)
        {
            GridSize size = ctx.Size;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (!other.IsColonist || !Melee.IsStanding(other)) continue;
                if (Cells(size, pawn.Cell, other.Cell) <= reach && ctx.Reachable(pawn, other.Cell, mode)) return true;
            }
            return false;
        }

        /// <summary>The larger of the two ground-plan distances between two cells, in cells.</summary>
        internal static int Cells(GridSize size, int a, int b)
        {
            CellRef pa = size.FromIndex(a);
            CellRef pb = size.FromIndex(b);
            return Math.Max(Math.Abs(pa.X - pb.X), Math.Abs(pa.Z - pb.Z));
        }
    }

    /// <summary>Where a raid's assault makes for (design 55 §5): the hearth, else the colony's start.</summary>
    public static class RaidTargets
    {
        /// <summary>How many rings out from the hearth or the start to look for a cell a raider can stand on.</summary>
        public const int SearchRings = 3;

        /// <summary>
        /// A standable cell at the hearth, else at the colony's start — each searched out a few
        /// rings, because a campfire is not a floor — or <paramref name="fallback"/> when there is
        /// neither, which is a test fixture with no colony.
        /// </summary>
        public static int Resolve(PawnContext ctx, int fallback)
        {
            GridSize size = ctx.Size;
            int anchor = -1;
            if (ctx.Hearth is { Exists: true } hearth) anchor = hearth.Cell;
            else if (ctx.ColonyStart is CellRef start && size.Contains(start)) anchor = size.Index(start.X, start.Z, start.Y);
            if (anchor < 0) return fallback;

            int found = Standable(ctx, anchor);
            return found >= 0 ? found : fallback;
        }

        static int Standable(PawnContext ctx, int anchor)
        {
            GridSize size = ctx.Size;
            CellRef at = size.FromIndex(anchor);
            for (int ring = 0; ring <= SearchRings; ring++)
            for (int dz = -ring; dz <= ring; dz++)
            for (int dx = -ring; dx <= ring; dx++)
            {
                if (Math.Abs(dx) != ring && Math.Abs(dz) != ring) continue;
                int x = at.X + dx, z = at.Z + dz;
                if (x < 0 || z < 0 || x >= size.SizeX || z >= size.SizeZ) continue;
                int cell = ctx.Cells.NearestWalkableInColumn(x, z, at.Y);
                if (cell >= 0 && ctx.Nav.Grid.CanEnter(cell, TraverseMode.Bandit)) return cell;
            }
            return -1;
        }
    }

    /// <summary>
    /// Named random purposes for the raid, beside <see cref="IncidentPurpose"/> and for its reason:
    /// each draw its own stream. SHA-256's round constants K24 to K27. The first three were K21 to
    /// K23 until cover reached <c>main</c> on them (<c>PawnPurpose.RangedCover*</c>) and a merge
    /// with no conflict marker put a raid's edge and a shot's cover roll on one stream; they moved
    /// on 2026-09-26 (design 55 §15). Grep the constant before taking the next one.
    /// </summary>
    public static class RaidPurpose
    {
        /// <summary>Which side of the board, and where on it, the band walks on.</summary>
        public const uint Edge = 0xA831_C66D;

        /// <summary>The order the band's kinds walk on in.</summary>
        public const uint Slots = 0xB003_27C8;

        /// <summary>How long it gathers.</summary>
        public const uint Loiter = 0xBF59_7FC7;

        /// <summary>A member's mill: wait or walk, and where. Mixed with the pawn's id, as a wander is.</summary>
        public const uint Mill = 0x983E_5152;
    }
}
