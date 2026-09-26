#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>
    /// One cell drawn cracked this frame, and how badly (design 57 §2): the answer
    /// <see cref="CrackModel.Gather"/> hands the renderer.
    /// </summary>
    public readonly struct CrackedCell
    {
        /// <summary>The cell, as a whole-world index.</summary>
        public readonly int CellIndex;

        /// <summary>
        /// How badly, on the drawn ladder of <see cref="CrackModel.Levels"/>: 1 is the first
        /// hairline and <see cref="CrackModel.Levels"/> is about to come apart. Never 0: an intact
        /// cell is not listed. A wall's three stages are levels 2, 4 and 6; rock uses all six.
        /// </summary>
        public readonly int Level;

        /// <summary>
        /// Whether what cracks is the cell's ground — rock being mined — rather than a building
        /// standing in it. Decides which of the cell's meshes the renderer draws the cracks over.
        /// </summary>
        public readonly bool Ground;

        public CrackedCell(int cellIndex, int level, bool ground)
        {
            CellIndex = cellIndex;
            Level = level;
            Ground = ground;
        }
    }

    /// <summary>
    /// How broken a thing looks (design 57): <b>one rule for a struck wall and for rock being
    /// mined</b>, fed by one number — how much of it is gone, in thousandths — and answered in
    /// three stages at a quarter, a half and three quarters for a wall (owner, 2026-09-26) and six
    /// for rock being mined, which the owner wanted in more steps. Unity-free, so the
    /// fast tier owns the thresholds and the choice of what cracks; presentation only draws them.
    ///
    /// <para><b>Shares of the whole, never hit points.</b> A 300-point wall and a 55-point
    /// sandbag crack at the same fraction, and a rock's "how broken" is how far through the cut
    /// is. So the same stage means the same thing on everything that ever uses it.</para>
    ///
    /// <para><b>Stages, not a slide.</b> A player reads a step — "it just cracked" — where a
    /// continuous change is noticed only by comparing two moments, and a stage is also a bucket
    /// the renderer can batch: three materials, never one per cell.</para>
    ///
    /// <para><b>Scales with</b> the struck buildings and the orders, both sparse. Nothing is
    /// allocated once the list has grown to the busiest frame.</para>
    /// </summary>
    public static class CrackModel
    {
        /// <summary>Thousandths gone at which hairline cracks appear.</summary>
        public const int Hairline = 250;

        /// <summary>Thousandths gone at which the cracks widen and darken.</summary>
        public const int Cracked = 500;

        /// <summary>Thousandths gone at which it is crumbling.</summary>
        public const int Crumbling = 750;

        /// <summary>How many stages a wall shows; 0, intact, is drawn as nothing.</summary>
        public const int Stages = 3;

        /// <summary>
        /// The drawn ladder: how many looks the renderer keeps, one material each. Rock being
        /// mined climbs all of it (owner, 2026-09-26: <i>"the mine is fine enough but needs more
        /// stages"</i>); a wall's three stages are every other rung, so a wall and a face at the
        /// same level look equally broken.
        /// </summary>
        public const int Levels = 6;

        /// <summary>
        /// Thousandths of a cut done at which rock reaches each level, 1 to <see cref="Levels"/>.
        /// Starts early, at a tenth, so a face shows the pick from almost the first strokes.
        /// </summary>
        static readonly int[] RockThresholds = { 100, 250, 400, 550, 700, 850 };

        /// <summary>A wall's stage on the drawn ladder: stage 1, 2, 3 is level 2, 4, 6.</summary>
        public static int LevelOfStage(int stage) => stage * Levels / Stages;

        /// <summary>Rock's level for this much of the cut done: 0 untouched, then 1 to <see cref="Levels"/>.</summary>
        public static int RockLevelOf(int brokenPerMille)
        {
            int level = 0;
            for (int i = 0; i < RockThresholds.Length; i++)
                if (brokenPerMille >= RockThresholds[i]) level = i + 1;
            return level;
        }

        /// <summary>
        /// <c>DesignationKind.Mine</c>'s value, restated because the enum lives in the simulation
        /// and <see cref="OrderView.Kind"/> carries only the number (as <c>AlertModel</c> does).
        /// </summary>
        public const byte MineOrderKind = 1;

        /// <summary>The stage for this much gone: 0 intact, then 1, 2, 3.</summary>
        public static int StageOf(int brokenPerMille) =>
            brokenPerMille >= Crumbling ? 3
            : brokenPerMille >= Cracked ? 2
            : brokenPerMille >= Hairline ? 1
            : 0;

        /// <summary>Thousandths of a building's pool that are gone, clamped to 0–1000.</summary>
        public static int BrokenOfHitPoints(int hpMilli, int maxMilli)
        {
            if (maxMilli <= 0) return 0;
            if (hpMilli <= 0) return 1000;
            if (hpMilli >= maxMilli) return 0;
            return (int)(1000L * (maxMilli - hpMilli) / maxMilli);
        }

        /// <summary>Thousandths of a cut that are done, from the snapshot's 0–255 progress byte.</summary>
        public static int BrokenOfProgress(byte progress) => progress * 1000 / 255;

        /// <summary>
        /// Which buildings crack. <b>Walls only</b> in the first unit (owner, 2026-09-26: walls
        /// first, the rest once the look is right). Doors, sandbags and furniture join here — and
        /// furniture may want scratches rather than cracks, which is design 57 §8's question.
        /// </summary>
        public static bool Cracks(int edifice) => edifice == EdificeHandle.Wall;

        /// <summary>
        /// Every cell that should be drawn cracked this frame, into <paramref name="into"/>
        /// (cleared first), between <paramref name="lowest"/> and <paramref name="highest"/>
        /// layers inclusive. Three sources: struck walls, rock under a mining order, and rock
        /// somebody started on and left (<see cref="WorldSnapshot.PartMined"/>). Returns the count.
        /// </summary>
        public static int Gather(WorldSnapshot snapshot, List<CrackedCell> into, int lowest, int highest)
        {
            into.Clear();
            GridSize size = snapshot.Size;

            System.ReadOnlySpan<EdificeDamageView> struck = snapshot.EdificeDamage;
            for (int i = 0; i < struck.Length; i++)
            {
                EdificeDamageView row = struck[i];
                if (!Cracks(row.Edifice) || !Drawn(size, row.CellIndex, lowest, highest)) continue;
                int stage = StageOf(BrokenOfHitPoints(row.HpMilli, row.MaxMilli));
                if (stage > 0) into.Add(new CrackedCell(row.CellIndex, LevelOfStage(stage), ground: false));
            }

            System.ReadOnlySpan<OrderView> orders = snapshot.Orders;
            for (int i = 0; i < orders.Length; i++)
            {
                if (orders[i].Kind != MineOrderKind || !Drawn(size, orders[i].CellIndex, lowest, highest)) continue;
                int level = RockLevelOf(BrokenOfProgress(orders[i].Progress));
                if (level > 0) into.Add(new CrackedCell(orders[i].CellIndex, level, ground: true));
            }

            System.ReadOnlySpan<PartMinedView> left = snapshot.PartMined;
            for (int i = 0; i < left.Length; i++)
            {
                if (!Drawn(size, left[i].CellIndex, lowest, highest)) continue;
                int level = RockLevelOf(BrokenOfProgress(left[i].Progress));
                if (level > 0) into.Add(new CrackedCell(left[i].CellIndex, level, ground: true));
            }

            return into.Count;
        }

        static bool Drawn(GridSize size, int cellIndex, int lowest, int highest)
        {
            if ((uint)cellIndex >= (uint)size.CellCount) return false;
            int y = size.FromIndex(cellIndex).Y;
            return y >= lowest && y <= highest;
        }
    }
}
