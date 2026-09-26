#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// What protected a target from one shot (design 53 §2): up to eight neighbouring cells, what
    /// each was worth after the angle, the shooter's distance and the descent, and the whole. A
    /// scratch object the caller keeps and reuses, so a shot allocates nothing.
    /// </summary>
    public sealed class CoverReport
    {
        /// <summary>The most a report can hold: the eight neighbours of the target.</summary>
        public const int Capacity = 8;

        /// <summary>How many contributors are filled, in the order the neighbours were scanned.</summary>
        public int Count;

        /// <summary>The overall cover, per mille: the contributors folded by noisy-OR.</summary>
        public int Total;

        /// <summary>The cover cell of each contributor.</summary>
        public readonly int[] Cells = new int[Capacity];

        /// <summary>Its base value before any factor, per mille (design 53 §3).</summary>
        public readonly int[] Bases = new int[Capacity];

        /// <summary>What it gave this shot, per mille: base × angle × shooter's distance × descent.</summary>
        public readonly int[] PerMille = new int[Capacity];

        /// <summary>Was it tall cover rather than low?</summary>
        public readonly bool[] Tall = new bool[Capacity];

        /// <summary>The descent factor the shot met on the target's low cover, per mille — what the readout calls "from above".</summary>
        public int LowElevationPerMille = 1_000;

        public void Clear()
        {
            Count = 0;
            Total = 0;
            LowElevationPerMille = 1_000;
        }

        /// <summary>Does any contributor with something to give count as low cover? What a crouch asks.</summary>
        public bool HasLow
        {
            get
            {
                for (int i = 0; i < Count; i++)
                    if (!Tall[i] && PerMille[i] > 0) return true;
                return false;
            }
        }

        /// <summary>
        /// The contributor a cover roll lands on (design 53 §2d): <paramref name="pick"/> in
        /// <c>[0, sum of PerMille)</c> walked along the contributors, so each is chosen in
        /// proportion to what it gave. -1 if nothing gave anything.
        /// </summary>
        public int CellForPick(int pick)
        {
            for (int i = 0; i < Count; i++)
            {
                if (pick < PerMille[i]) return Cells[i];
                pick -= PerMille[i];
            }
            return -1;
        }

        /// <summary>The sum of the contributors' <see cref="PerMille"/>: the range a pick is drawn in.</summary>
        public int Sum
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < Count; i++) sum += PerMille[i];
                return sum;
            }
        }
    }

    /// <summary>
    /// Partial cover (design 53): how much the things beside a target protect it from one shot.
    /// The reference's rule in integers, with a descent term of our own because our world has
    /// layers.
    ///
    /// <para><b>Only the target's eight neighbours on its own layer count.</b> Each contributes
    /// <c>base × angle band × shooter's distance × descent</c>, and the contributions fold by
    /// noisy-OR. No float and no <c>atan</c> anywhere: an angle band is a squared-cosine comparison
    /// by cross-multiplication, and the descent is graded linearly in its tangent, so a cover value
    /// agrees under Mono and CoreCLR and replays into the state hash through the roll it feeds.</para>
    ///
    /// <para><b><see cref="BaseAt"/> is the one owner of what a cell is worth.</b> The shot, the
    /// stray's interception, the fighters' choice of where to stand, the crouch and the readout all
    /// ask it, and <c>CoverOwnerTests</c> reads the C# files for a second copy.</para>
    /// </summary>
    public static class Cover
    {
        /// <summary>The five band edges, 15° 27° 40° 52° 65°, as cos² in millionths — a cardinal neighbour.</summary>
        static readonly long[] CardinalCos2 = { 933_013, 793_893, 586_824, 379_039, 178_606 };

        /// <summary>The same edges divided by 1.75, for a diagonal neighbour (the reference's penalty).</summary>
        static readonly long[] DiagonalCos2 = { 977_786, 929_224, 849_118, 754_306, 635_420 };

        /// <summary>What each band leaves of the cover, per mille; past the last band, nothing.</summary>
        static readonly int[] BandFactor = { 1_000, 800, 600, 400, 200 };

        /// <summary>
        /// What the thing in <paramref name="cell"/> is worth as cover before any factor, per mille,
        /// and whether it is tall (design 53 §3). In order: a rock face (solid terrain) is full fill;
        /// a door is full fill shut and nothing open; a building row that names its cover gives that;
        /// a tree or a bush gives its plant's; anything else that fills its cell (a wall, a pillar, a
        /// window, a vault wall) is full fill; everything else, nothing. An unbuilt site is not an
        /// edifice and gives nothing.
        /// </summary>
        public static int BaseAt(PawnContext ctx, int cell, out bool tall)
        {
            tall = false;
            if ((uint)cell >= (uint)ctx.Size.CellCount) return 0;
            CellGrid cells = ctx.Cells;
            int full = ctx.Content.Combat.fullFillCoverPerMille;
            if (cells.IsSolidTerrain(cell))
            {
                tall = true;
                return full;
            }

            int handle = cells.Edifice[cell];
            if (handle < 0) return 0;

            ushort def = CoreContent.EdificeNone;
            var construction = ctx.Construction;
            if (construction != null)
            {
                List<PlacedEdifice> records = construction.Edifices.Records;
                if (handle < records.Count && !records[handle].Removed) def = records[handle].Def;
            }

            NavFlags nav = ctx.Nav.Grid.Flags[cell];
            if (def == CoreContent.EdificeDoor || (nav & NavFlags.Door) != 0)
            {
                if ((nav & NavFlags.DoorOpen) != 0) return 0;
                tall = true;
                return full;
            }

            if (def != CoreContent.EdificeNone)
            {
                int building = ConstructionContent.BuildingForEdifice(def);
                if (building != BuildingHandle.None)
                {
                    BuildingDef row = ConstructionContent.BuildingAt(building);
                    if (row.coverPerMille > 0)
                    {
                        tall = row.coverTall;
                        return row.coverPerMille;
                    }
                }
                WildPlantDef? plant = NaturalContent.WildPlantAt(def);
                if (plant != null)
                {
                    tall = plant.coverTall;
                    return plant.coverPerMille;
                }
            }

            if (cells.IsBlockedByEdifice(cell))
            {
                tall = true;
                return full;
            }
            return 0;
        }

        /// <summary>
        /// What the angle at the target leaves of a neighbour's cover, per mille (design 53 §2b).
        /// <paramref name="ux"/>, <paramref name="uz"/> run from the target to the shooter and
        /// <paramref name="vx"/>, <paramref name="vz"/> from the target to the neighbour, in cells.
        /// A band edge <c>B</c> is passed when <c>dot² × 10⁶ > cos²B × |u|² × |v|²</c> with the dot
        /// positive; a diagonal neighbour uses edges divided by 1.75.
        /// </summary>
        public static int AngleFactorPerMille(int ux, int uz, int vx, int vz)
        {
            long dot = (long)ux * vx + (long)uz * vz;
            if (dot <= 0) return 0;
            long u2 = (long)ux * ux + (long)uz * uz;
            long v2 = (long)vx * vx + (long)vz * vz;
            if (u2 == 0 || v2 == 0) return 0;
            long[] edges = vx != 0 && vz != 0 ? DiagonalCos2 : CardinalCos2;
            long lhs = dot * dot * 1_000_000;
            long rhs = u2 * v2;
            for (int band = 0; band < edges.Length; band++)
                if (lhs > edges[band] * rhs) return BandFactor[band];
            return 0;
        }

        /// <summary>
        /// What the shooter's own nearness to the cover leaves of it, per mille (design 53 §2b): a
        /// third under 1.9 cells, two thirds under 2.9, all of it beyond. Horizontal cells from the
        /// shooter to the <b>cover</b>, not to the target.
        /// </summary>
        public static int ShooterDistanceFactorPerMille(int dx, int dz)
        {
            long d2x100 = ((long)dx * dx + (long)dz * dz) * 100;
            if (d2x100 < 361) return 333;
            if (d2x100 < 841) return 667;
            return 1_000;
        }

        /// <summary>
        /// What a shot's descent leaves of cover of one class, per mille (design 53 §2b): the rise
        /// from the target up to the shooter over the horizontal distance, as a tangent in per mille,
        /// graded linearly between the class's two tangents. A shot from level or below leaves all
        /// of it; one from straight overhead leaves none.
        /// </summary>
        public static int ElevationFactorPerMille(int layersAbove, int horizontalMm, bool tall, CombatDef combat)
        {
            if (layersAbove <= 0) return 1_000;
            if (horizontalMm <= 0) return 0;
            long tan = (long)layersAbove * GridSize.CellSizeYMm * 1_000 / horizontalMm;
            int from = tall ? combat.coverTallFullTanPerMille : combat.coverLowFullTanPerMille;
            int to = tall ? combat.coverTallGoneTanPerMille : combat.coverLowGoneTanPerMille;
            if (tan <= from) return 1_000;
            if (tan >= to || to <= from) return 0;
            return (int)((to - tan) * 1_000 / (to - from));
        }

        /// <summary>Fold one more contribution into a noisy-OR total, per mille: <c>t + (1000 − t) × c / 1000</c>.</summary>
        public static int Combine(int total, int contribution) =>
            total + (1_000 - total) * contribution / 1_000;

        /// <summary>
        /// How well the target in <paramref name="targetCell"/> is covered against a shot from
        /// <paramref name="shooterCell"/>, per mille, with every contributor written into
        /// <paramref name="report"/> when one is given. Only the target's eight neighbours on its
        /// own layer are asked, the shooter's own cell never.
        /// </summary>
        public static int Evaluate(PawnContext ctx, int shooterCell, int targetCell, CoverReport? report = null)
        {
            report?.Clear();
            GridSize size = ctx.Size;
            if ((uint)shooterCell >= (uint)size.CellCount || (uint)targetCell >= (uint)size.CellCount) return 0;
            CellRef s = size.FromIndex(shooterCell), t = size.FromIndex(targetCell);
            int ux = s.X - t.X, uz = s.Z - t.Z;
            if (ux == 0 && uz == 0) return 0;

            CombatDef combat = ctx.Content.Combat;
            long hx = (long)ux * GridSize.CellSizeXZMm, hz = (long)uz * GridSize.CellSizeXZMm;
            int horizontal = RangedGeometry.ISqrt(hx * hx + hz * hz);
            int above = s.Y - t.Y;
            int lowElevation = ElevationFactorPerMille(above, horizontal, false, combat);
            int tallElevation = ElevationFactorPerMille(above, horizontal, true, combat);
            if (report != null) report.LowElevationPerMille = lowElevation;

            int total = 0;
            for (int vz = -1; vz <= 1; vz++)
            for (int vx = -1; vx <= 1; vx++)
            {
                if (vx == 0 && vz == 0) continue;
                int x = t.X + vx, z = t.Z + vz;
                if ((uint)x >= (uint)size.SizeX || (uint)z >= (uint)size.SizeZ) continue;
                int cell = size.Index(x, z, t.Y);
                if (cell == shooterCell) continue;

                int angle = AngleFactorPerMille(ux, uz, vx, vz);
                if (angle == 0) continue;
                int baseValue = BaseAt(ctx, cell, out bool tall);
                if (baseValue <= 0) continue;

                long c = (long)baseValue * angle / 1_000;
                c = c * ShooterDistanceFactorPerMille(s.X - x, s.Z - z) / 1_000;
                c = c * (tall ? tallElevation : lowElevation) / 1_000;
                if (c <= 0) continue;

                total = Combine(total, (int)c);
                if (report != null && report.Count < CoverReport.Capacity)
                {
                    int i = report.Count++;
                    report.Cells[i] = cell;
                    report.Bases[i] = baseValue;
                    report.PerMille[i] = (int)c;
                    report.Tall[i] = tall;
                }
            }
            if (report != null) report.Total = total;
            return total;
        }
    }
}
