#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Worldgen;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// One building a blow can be aimed at (design 33 §13): which record, where it stands, and how
    /// much it takes. A value read off the edifice list and the content, never kept: the order
    /// keeps only <see cref="Handle"/>, and everything else is read again from it.
    /// </summary>
    public readonly struct BuildingTarget
    {
        /// <summary>The record's position in the edifice list — stable, never reused, the building's identity.</summary>
        public readonly int Handle;

        /// <summary>The record's own cell: a two-cell thing's head. What <c>EdificeDamage</c> keys it on.</summary>
        public readonly int Anchor;

        /// <summary>A two-cell thing's far cell, or -1.</summary>
        public readonly int Second;

        /// <summary>What it is, as an edifice id.</summary>
        public readonly ushort Edifice;

        /// <summary>Its pool, in thousandths: the row's points times its material's factor.</summary>
        public readonly int MaxMilli;

        public BuildingTarget(int handle, int anchor, int second, ushort edifice, int maxMilli)
        {
            Handle = handle;
            Anchor = anchor;
            Second = second;
            Edifice = edifice;
            MaxMilli = maxMilli;
        }

        /// <summary>Is <paramref name="cell"/> one of the building's own cells?</summary>
        public bool Covers(int cell) => cell >= 0 && (cell == Anchor || cell == Second);
    }

    /// <summary>
    /// The questions the fight asks about a building (design 33 §13): is this a target, how much is
    /// left of it, can she strike it from there, where should she stand, what does a blow do.
    /// <b>One owner for each</b>, as <see cref="Melee"/> is for two pawns — the order, the driver,
    /// the resolver and the publish all ask here.
    ///
    /// <para><b>A target is an edifice standing in the cell whose building row has hit points</b>
    /// (§13b): a wall, a door, a ladder, a bed, a shelf, a campfire, a generator, a heater. Never a
    /// floor or deck plate (a slab, not an edifice), a conduit (its own layer), a tree or anything
    /// else the generator stamps without a building row, or a site. The ruined city's own walls
    /// and doors are walls and doors, and are targets: <see cref="PlacedEdifice.Built"/> is
    /// deconstruct's question, because its refund is a yield.</para>
    ///
    /// <para>Every answer is read off saved state — the edifice list, the grid, the damage store —
    /// so a save taken mid-fight answers each the same way after the load.</para>
    /// </summary>
    public static class BuildingTargets
    {
        /// <summary>
        /// The hit points the content gives the thing standing as this edifice, in whole points
        /// before its material, or nought where it is not a target. The one place the rule is
        /// written; <c>EdificeDamageContributor</c> publishes it for the interface.
        /// </summary>
        public static int MaxHitPointsOf(ushort edifice)
        {
            if (edifice == CoreContent.EdificeNone) return 0;
            int building = ConstructionContent.BuildingForEdifice(edifice);
            if (building == BuildingHandle.None) return 0;
            BuildingDef def = ConstructionContent.BuildingAt(building);
            // A slab or a line carries hit points too, and is never standing in a cell as an
            // edifice: BuildingForEdifice cannot reach either, since both are edifice 0.
            return def.maxHitPoints > 0 ? def.maxHitPoints : 0;
        }

        /// <summary>
        /// The pool of this edifice in this material, in thousandths (design 33 §13c): the row's
        /// points times the material's <see cref="StuffDef.hitPointsFactorPerMille"/>. A material
        /// the table does not know counts as the standard one.
        /// </summary>
        public static int MaxMilliOf(ushort edifice, ushort stuff)
        {
            int points = MaxHitPointsOf(edifice);
            if (points <= 0) return 0;
            int handle = ConstructionContent.StuffForValue(stuff);
            int factor = handle == StuffHandle.None ? 1_000 : ConstructionContent.StuffAt(handle).hitPointsFactorPerMille;
            long milli = (long)points * factor;
            return milli > int.MaxValue ? int.MaxValue : (int)milli;
        }

        /// <summary>The building standing in <paramref name="cell"/> that a blow can be aimed at, or false.</summary>
        public static bool TryFind(PawnContext ctx, int cell, out BuildingTarget target)
        {
            target = default;
            if ((uint)cell >= (uint)ctx.Size.CellCount) return false;
            return TryStanding(ctx, ctx.Cells.Edifice[cell], out target);
        }

        /// <summary>
        /// The building this record is, if it still stands and is still a target — the order's
        /// question every tick. False once it has been demolished, taken apart, or its cell given
        /// to another record: a wall pulled down and raised again is a new building.
        /// </summary>
        public static bool TryStanding(PawnContext ctx, int handle, out BuildingTarget target)
        {
            target = default;
            var construction = ctx.Construction;
            if (construction == null) return false;
            List<PlacedEdifice> records = construction.Edifices.Records;
            if (handle < 0 || handle >= records.Count) return false;

            PlacedEdifice placed = records[handle];
            if (placed.Removed) return false;
            if ((uint)placed.CellIndex >= (uint)ctx.Size.CellCount || ctx.Cells.Edifice[placed.CellIndex] != handle) return false;

            int max = MaxMilliOf(placed.Def, placed.Stuff);
            if (max <= 0) return false;

            int second = EdificeFootprint.SecondCell(placed.CellIndex, placed.Def, placed.Facing, ctx.Size);
            target = new BuildingTarget(handle, placed.CellIndex, second, placed.Def, max);
            return true;
        }

        /// <summary>What is left of it, in thousandths: its row in <c>EdificeDamage</c>, or its whole pool if it has none.</summary>
        public static int HpMilli(PawnContext ctx, in BuildingTarget target) =>
            ctx.EdificeDamage.TryGet(target.Anchor, out int hp) ? hp : target.MaxMilli;

        /// <summary>
        /// Can a pawn standing on <paramref name="from"/> strike the building? <b>The deconstructor's
        /// stance</b> (design 33 §13e, <c>JobDriver.StillInReach</c>): on the building's layer,
        /// within one cell of any of its cells, diagonals included, and never inside it — a wall
        /// fills its cell, so beside is the only place to strike one from, and a door, a bed or a
        /// shelf is struck from beside it too, so that the rule is one rule.
        /// </summary>
        public static bool InReach(PawnContext ctx, int from, in BuildingTarget target)
        {
            if ((uint)from >= (uint)ctx.Size.CellCount || target.Covers(from)) return false;
            return Beside(ctx.Size, from, target.Anchor) || (target.Second >= 0 && Beside(ctx.Size, from, target.Second));
        }

        static bool Beside(GridSize size, int a, int b)
        {
            CellRef p = size.FromIndex(a), q = size.FromIndex(b);
            if (p.Y != q.Y) return false;
            int dx = p.X - q.X, dz = p.Z - q.Z;
            return dx >= -1 && dx <= 1 && dz >= -1 && dz <= 1;
        }

        /// <summary>
        /// Which of the building's cells a pawn on <paramref name="from"/> strikes: the one beside
        /// her, the head on a tie. What the figure turns to and the number floats over.
        /// </summary>
        public static int StruckCell(PawnContext ctx, int from, in BuildingTarget target) =>
            target.Second >= 0 && !Beside(ctx.Size, from, target.Anchor) && Beside(ctx.Size, from, target.Second)
                ? target.Second
                : target.Anchor;

        /// <summary>
        /// Where <paramref name="me"/> should stand to strike the building (design 33 §13e, the pawn
        /// rule of §7c): of the cells in reach of it that she can stand on and reach, the one no
        /// other fighter holds (<see cref="Melee.Holds"/>), nearest her by squared distance in cells,
        /// a tie to the first in the fixed scan — each of the building's cells in turn, −Z to +Z then
        /// −X to +X. Her own cell counts as free for her. −1 when every such cell is held;
        /// <paramref name="reachable"/> is false when there is no cell in reach she could get to at
        /// all, held or not, which is an order that cannot be carried out.
        ///
        /// <para><b>Scales with</b> at most ten candidate cells for a two-cell thing, each costing
        /// one pass over the pawns and a reachability query. Asked when the attack is new, on
        /// arriving at a cell somebody else holds, and while waiting at the chase cadence — never
        /// per tick.</para>
        /// </summary>
        public static int ChooseSide(PawnContext ctx, Pawn me, in BuildingTarget target, TraverseMode mode, out bool reachable)
        {
            reachable = false;
            GridSize size = ctx.Size;
            CellRef m = size.FromIndex(me.Cell);
            int best = -1, bestDistance = int.MaxValue;

            for (int part = 0; part < 2; part++)
            {
                int centre = part == 0 ? target.Anchor : target.Second;
                if (centre < 0) continue;
                CellRef t = size.FromIndex(centre);

                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int x = t.X + dx, z = t.Z + dz;
                    if (!size.Contains(x, z, t.Y)) continue;
                    int cell = size.Index(x, z, t.Y);
                    if (target.Covers(cell)) continue;
                    if (cell != me.Cell && !ctx.Nav.Grid.CanEnter(cell, mode)) continue;

                    if (cell != me.Cell && !ctx.Reachable(me, cell, mode)) continue;
                    reachable = true;

                    int ex = x - m.X, ez = z - m.Z, ey = t.Y - m.Y;
                    int distance = ex * ex + ez * ez + ey * ey;
                    if (distance >= bestDistance) continue;
                    if (Melee.Holds(ctx, me, cell)) continue;
                    best = cell;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>
        /// Can <paramref name="pawn"/> get to strike the building at all — from where she stands, or
        /// from a cell in reach she can walk to? The order's question.
        /// </summary>
        public static bool CanReach(PawnContext ctx, Pawn pawn, in BuildingTarget target, TraverseMode mode)
        {
            if (InReach(ctx, pawn.Cell, target)) return true;
            ChooseSide(ctx, pawn, target, mode, out bool reachable);
            return reachable;
        }

        /// <summary>
        /// Decide one blow at a building, on the tick its wind-up begins (design 33 §13f): <b>it
        /// lands, for its damage in the spread, and nothing else</b> — a building cannot step aside,
        /// dodge, be stunned, be knocked back or take a critical. The one roll is the damage, on the
        /// pawn's own <see cref="PawnPurpose.MeleeDamage"/> stream salted by her id, exactly as a
        /// blow at a pawn draws it (<see cref="MeleeRules.DamageMilli"/>). Changes nothing: the
        /// driver holds the outcome on the pawn to the impact, and
        /// <see cref="CombatSystem.StrikeBuilding"/> applies it.
        /// </summary>
        public static SwingOutcome Resolve(Pawn attacker, in Armament armament, PawnContext ctx, int tick)
        {
            var roll = DeterministicRandom.ForTick(ctx.Seed, tick, PawnPurpose.MeleeDamage ^ (uint)attacker.Id.Value);
            return new SwingOutcome(CombatEventKind.Hit, MeleeRules.DamageMilli(armament.Attack, ctx, roll));
        }
    }
}
