#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The geometry of a sweep (design 62 §5): the eight directions a swing can face, numbered 1–8
    /// round from east so nought is "no facing", and the two flanks either side of one. Pure: no
    /// state, no rolls.
    /// </summary>
    public static class SweepArc
    {
        // 1 E, 2 NE, 3 N, 4 NW, 5 W, 6 SW, 7 S, 8 SE. Index 0 is unused so a facing reads as itself.
        static readonly int[] Dx = { 0, 1, 1, 0, -1, -1, -1, 0, 1 };
        static readonly int[] Dz = { 0, 0, 1, 1, 1, 0, -1, -1, -1 };

        /// <summary>
        /// The facing from <paramref name="from"/> to <paramref name="to"/>, 1–8, when they are
        /// neighbours on one layer; nought otherwise, which includes the same cell.
        /// </summary>
        public static int Facing(GridSize size, int from, int to)
        {
            CellRef a = size.FromIndex(from), b = size.FromIndex(to);
            if (a.Y != b.Y) return 0;
            int dx = b.X - a.X, dz = b.Z - a.Z;
            if (dx < -1 || dx > 1 || dz < -1 || dz > 1 || (dx == 0 && dz == 0)) return 0;
            for (int f = 1; f <= 8; f++)
                if (Dx[f] == dx && Dz[f] == dz) return f;
            return 0;
        }

        /// <summary>The step a facing points along. (0, 0) for nought.</summary>
        public static (int dx, int dz) Step(int facing) =>
            facing >= 1 && facing <= 8 ? (Dx[facing], Dz[facing]) : (0, 0);

        /// <summary>The facing a quarter-turn anticlockwise of this one — one flank.</summary>
        public static int Left(int facing) => facing % 8 + 1;

        /// <summary>The facing a quarter-turn clockwise of this one — the other flank.</summary>
        public static int Right(int facing) => (facing + 6) % 8 + 1;
    }

    /// <summary>
    /// Why a fling stopped where it did (design 62 §7). <see cref="Wall"/> and <see cref="Body"/>
    /// are slams; the others are not.
    /// </summary>
    public enum FlingStop : byte
    {
        /// <summary>It went the whole distance.</summary>
        None = 0,

        /// <summary>A wall, a closed door, a rise, a wall's corner, the board's edge: a slam.</summary>
        Wall = 1,

        /// <summary>Somebody standing there: a slam, for both.</summary>
        Body = 2,

        /// <summary>Water: it stops short, and nothing more (owner: never knocked into water).</summary>
        Water = 3,

        /// <summary>A fighter's claim on the cell with nobody's body in it yet: it stops short.</summary>
        Held = 4,

        /// <summary>Over a ledge: it fell, and the fall is the end of it.</summary>
        Fell = 5,
    }

    /// <summary>Where a fling ends and what ended it (design 62 §7).</summary>
    public readonly struct FlingPath
    {
        /// <summary>Where it lands — its own cell when the first step was refused.</summary>
        public readonly int Land;

        public readonly FlingStop Stop;

        /// <summary>The body it was flung into, for <see cref="FlingStop.Body"/>; else null.</summary>
        public readonly Pawn? Blocker;

        /// <summary>The layers it fell, for <see cref="FlingStop.Fell"/>; else nought.</summary>
        public readonly int Fell;

        public FlingPath(int land, FlingStop stop, Pawn? blocker = null, int fell = 0)
        {
            Land = land;
            Stop = stop;
            Blocker = blocker;
            Fell = fell;
        }

        /// <summary>Stopped short by something it hit: a wall or a body.</summary>
        public bool Slammed => Stop == FlingStop.Wall || Stop == FlingStop.Body;
    }

    // The butcher's sweep and fling (design 62 §5, §7), beside the one method every blow is applied
    // through.
    public partial class CombatSystem
    {
        /// <summary>The deepest a fling falls: below four layers there is no landing, and it is a wall.</summary>
        public const int FlingMaxFall = 4;

        readonly List<Pawn> _flank = new List<Pawn>();

        /// <summary>
        /// Where a fling from <paramref name="attacker"/> would carry <paramref name="target"/>
        /// (design 62 §7), <paramref name="distance"/> cells straight away from the attacker, one step
        /// at a time. Each step obeys the one-tile knockback's own ground rules
        /// (<see cref="KnockbackCell"/>): a step the target itself could take, never water, never a
        /// fighter's tile, never the tile of the one it is fighting — and adds three of its own:
        /// <list type="bullet">
        /// <item><b>a body standing there</b> stops it (<see cref="FlingStop.Body"/>), where the
        /// one-tile knockback lays it beside that body;</item>
        /// <item><b>a wall, a rise, a corner or the edge</b> stops it (<see cref="FlingStop.Wall"/>),
        /// where the knockback simply does not happen;</item>
        /// <item><b>a drop</b>: one layer is a step it takes and the fling ends there; two or more,
        /// down to <see cref="FlingMaxFall"/>, is a fall (<see cref="FlingStop.Fell"/>).</item>
        /// </list>
        /// A target not beside the attacker on its layer, or being carried, returns a path landing
        /// at −1: no fling at all.
        /// </summary>
        public static FlingPath FlingCell(PawnContext ctx, Pawn attacker, Pawn target, int distance)
        {
            if (target.CarriedBy != 0) return new FlingPath(-1, FlingStop.None);
            GridSize size = ctx.Size;
            int facing = SweepArc.Facing(size, attacker.Cell, target.Cell);
            if (facing == 0) return new FlingPath(-1, FlingStop.None);
            var (dx, dz) = SweepArc.Step(facing);
            TraverseMode mode = target.OwnMode;
            int stride = size.LayerStride;

            int at = target.Cell;
            for (int step = 0; step < distance; step++)
            {
                CellRef c = size.FromIndex(at);
                int x = c.X + dx, z = c.Z + dz;
                if (!size.Contains(x, z, c.Y)) return new FlingPath(at, FlingStop.Wall);
                int beyond = size.Index(x, z, c.Y);
                if (IsWater(ctx, beyond)) return new FlingPath(at, FlingStop.Water);

                int land = -1, fell = 0;
                if (ctx.Nav.IsLegalStep(at, beyond, mode))
                {
                    land = beyond;
                }
                else
                {
                    // Off an edge: the cell beyond is open air, neither corner of a diagonal is a
                    // wall, and something below it can be stood on within the reach of a fall.
                    if (c.Y == 0 || !ctx.Nav.Grid.IsAir(beyond)) return new FlingPath(at, FlingStop.Wall);
                    if (dx != 0 && dz != 0
                        && (!Passable(ctx, size.Index(c.X + dx, c.Z, c.Y), mode)
                            || !Passable(ctx, size.Index(c.X, c.Z + dz, c.Y), mode)))
                        return new FlingPath(at, FlingStop.Wall);
                    for (int down = 1; down <= FlingMaxFall && c.Y - down >= 0; down++)
                    {
                        int lower = beyond - down * stride;
                        if (IsWater(ctx, lower)) return new FlingPath(at, FlingStop.Water);
                        if (ctx.Nav.Grid.CanEnter(lower, mode)) { land = lower; fell = down; break; }
                        if (!ctx.Nav.Grid.IsAir(lower)) break;
                    }
                    if (land < 0) return new FlingPath(at, FlingStop.Wall);
                }

                Pawn? body = StandingAt(ctx, land, target, attacker);
                if (body != null) return new FlingPath(at, FlingStop.Body, body);
                if (Melee.Holds(ctx, target, land) || OnWhoItFights(ctx, target, land))
                    return new FlingPath(at, FlingStop.Held);

                at = land;
                if (fell >= 2) return new FlingPath(at, FlingStop.Fell, fell: fell);
                // One terrace step down is a step it takes, and the end of the fling.
                if (fell == 1) return new FlingPath(at, FlingStop.None);
            }
            return new FlingPath(at, FlingStop.None);
        }

        /// <summary>
        /// The first standing pawn on <paramref name="cell"/> other than these two, in id order, or
        /// null. <b>Scales with the pawns on the board</b>, asked only for a fling's steps.
        /// </summary>
        static Pawn? StandingAt(PawnContext ctx, int cell, Pawn target, Pawn attacker)
        {
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p.Cell == cell && p != target && p != attacker && p.CarriedBy == 0 && Melee.IsStanding(p)) return p;
            }
            return null;
        }

        /// <summary>
        /// Fling <paramref name="target"/> (design 62 §7): along <see cref="FlingCell"/>, knocked
        /// down there for <see cref="SweepDef.knockedDownTicks"/>, immune to any knockback for
        /// <see cref="SweepDef.immunityTicks"/> from now; then the slam — <see cref="SweepDef.slamDamage"/>
        /// through <see cref="Hurt"/> to it and to any body it hit — or the fall. A target stopped at
        /// its first step does not move and is still knocked down: it was hit hard enough to fly.
        /// Nothing happens to an unstoppable target or one still immune.
        /// </summary>
        public bool Fling(Pawn target, Pawn attacker, SweepDef sweep, int weapon, int tick)
        {
            if (target.Species.unstoppable || target.KnockbackImmuneAt(tick)) return false;
            FlingPath path = FlingCell(_ctx, attacker, target, sweep.distance);
            if (path.Land < 0) return false;

            Displace(target, attacker, path.Land, sweep.knockedDownTicks, weapon, tick);
            target.KnockbackImmuneUntilTick = tick + sweep.immunityTicks;

            if (path.Slammed)
            {
                Slam(target, attacker, sweep, weapon, tick);
                if (path.Blocker != null) Slam(path.Blocker, attacker, sweep, weapon, tick);
            }
            else if (path.Stop == FlingStop.Fell)
            {
                Fall(target, path.Fell, tick);
            }
            return true;
        }

        void Slam(Pawn pawn, Pawn attacker, SweepDef sweep, int weapon, int tick)
        {
            if (sweep.slamDamage <= 0 || !Melee.IsStanding(pawn)) return;
            int milli = sweep.slamDamage * Rates.Scale;
            _ctx.CombatLog.Report(CombatEventKind.Slam, attacker.Id, pawn.Id, _ctx.Size.FromIndex(pawn.Cell), tick,
                milli, weapon);
            Hurt(pawn, attacker, milli, AfflictionKind.Bruise, HitSet.Melee, weapon, tick);
        }

        /// <summary>
        /// The rest of a sweep (design 62 §5), after the blow at its target has been applied: every
        /// standing pawn not of the swinger's faction on the two flank cells of
        /// <paramref name="facing"/>, within reach, is struck with a blow of its own
        /// (<see cref="IMeleeRules.ResolveFlank"/>) through <see cref="ApplySwing"/>. The target
        /// itself is never struck twice. The victims are gathered before any blow lands, so a fling
        /// cannot move somebody into or out of the arc mid-swing. <b>Scales with the pawns on the
        /// board</b>, once per sweep that lands.
        /// </summary>
        internal void SweepFlanks(Pawn attacker, int facing, int primary, in Armament armament, int tick)
        {
            GridSize size = _ctx.Size;
            CellRef a = size.FromIndex(attacker.Cell);
            _flank.Clear();
            Gather(attacker, a, SweepArc.Left(facing), primary);
            Gather(attacker, a, SweepArc.Right(facing), primary);

            for (int i = 0; i < _flank.Count; i++)
            {
                Pawn victim = _flank[i];
                if (_ctx.Pawns.Get(victim.Id) != victim || !Melee.IsStanding(victim)) continue;
                SwingOutcome outcome = _ctx.MeleeRules.ResolveFlank(attacker, victim, armament, _ctx, tick);
                ApplySwing(attacker, victim, armament, outcome, tick);
            }
            _flank.Clear();
        }

        void Gather(Pawn attacker, CellRef a, int facing, int primary)
        {
            var (dx, dz) = SweepArc.Step(facing);
            GridSize size = _ctx.Size;
            if (!size.Contains(a.X + dx, a.Z + dz, a.Y)) return;
            int cell = size.Index(a.X + dx, a.Z + dz, a.Y);
            var pawns = _ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p.Cell != cell || p == attacker || p.Id.Value == primary) continue;
                if (p.CarriedBy != 0 || !Melee.IsStanding(p) || p.Faction == attacker.Faction) continue;
                if (!Melee.InReach(_ctx, attacker, p, attacker.Mode)) continue;
                _flank.Add(p);
            }
        }
    }
}
