#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The questions every part of the fight asks about two pawns (design 33 §6A): is this one
    /// dead, is it on its feet, can that one reach it with a blow, is it a threat to me. <b>Lane
    /// A's file.</b> One owner for each answer, because the attack driver, the resolver, the draft's
    /// hold and three think nodes all ask them, and four copies of "in reach" would be the fault
    /// <c>docs/bug-patterns.md</c> lists first.
    ///
    /// <para>Every answer is read off saved state — cells, hit points, flags, the job in hand —
    /// never off a path, which is not saved: a save taken mid-fight must answer every one of these
    /// the same way after the load.</para>
    /// </summary>
    public static class Melee
    {
        /// <summary>
        /// Past saving: at or below its species' death line. A pawn is dead from the blow that put
        /// it there, although it stays in the registry until the deferred removal at the end of the
        /// tick (design 33 §3) — so everything that picks a target or lands a blow asks this first.
        /// </summary>
        public static bool IsDead(Pawn pawn) => pawn.HpMilli <= pawn.DeathAtMilli;

        /// <summary>On its feet and alive: a pawn a hunt may choose and a blow may be aimed at.</summary>
        public static bool IsStanding(Pawn pawn) => !pawn.Downed && !IsDead(pawn);

        /// <summary>
        /// Can <paramref name="attacker"/> strike <paramref name="target"/> from where each stands?
        /// The same cell, or the next one along on the same layer by a step the attacker could take
        /// — orthogonal, or diagonal with neither corner blocked. So no blow passes through a wall's
        /// corner, and nobody is struck from the block above: a hop-adjacent target is reached by
        /// the chase, not swung at over the edge.
        /// </summary>
        public static bool InReach(PawnContext ctx, Pawn attacker, Pawn target, TraverseMode mode)
        {
            int a = attacker.Cell, b = target.Cell;
            if (a == b) return true;
            int stride = ctx.Size.LayerStride;
            if (a / stride != b / stride) return false;
            return ctx.Nav.IsLegalStep(a, b, mode);
        }

        /// <summary>Is <paramref name="other"/> in a melee job aimed at <paramref name="me"/> right now?</summary>
        public static bool IsAttacking(Pawn other, Pawn me) =>
            other.CombatTarget == me.Id.Value && other.CurrentJob != null
            && other.CurrentJob.DefIndex == JobIndex.AttackMelee;

        /// <summary>
        /// Something a colonist hits without being told to (design 33 §1): a standing hostile, or
        /// anybody standing who is attacking her — a hog that turned, a colonist who struck her.
        /// </summary>
        public static bool IsThreatTo(Pawn other, Pawn me) =>
            other != me && IsStanding(other) && (other.IsHostile || IsAttacking(other, me));

        /// <summary>
        /// The first threat within reach of <paramref name="me"/>, in id order, or null. <b>Scales
        /// with the pawns on the board</b>: one integer comparison each and a step test for the
        /// few that are threats. Asked by a drafted colonist's hold every tick and by a colonist's
        /// self-defence at each think.
        /// </summary>
        public static Pawn? AdjacentThreat(PawnContext ctx, Pawn me)
        {
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (!IsThreatTo(other, me)) continue;
                if (InReach(ctx, me, other, TraverseMode.Colonist)) return other;
            }
            return null;
        }

        /// <summary>
        /// Which way a body falls, away from the blow: one of eight headings, 0 is +Z and clockwise
        /// from above (<see cref="Corpse.Facing"/>). Nought when there is nobody to fall away from.
        /// </summary>
        public static byte FallFacing(GridSize size, int from, int at)
        {
            if (from < 0 || from == at) return 0;
            CellRef a = size.FromIndex(from), b = size.FromIndex(at);
            int dx = System.Math.Sign(b.X - a.X), dz = System.Math.Sign(b.Z - a.Z);
            if (dx == 0 && dz == 0) return 0;
            // 0 +Z, 1 +Z+X, 2 +X, 3 -Z+X, 4 -Z, 5 -Z-X, 6 -X, 7 +Z-X.
            if (dx == 0) return (byte)(dz > 0 ? 0 : 4);
            if (dz == 0) return (byte)(dx > 0 ? 2 : 6);
            if (dx > 0) return (byte)(dz > 0 ? 1 : 3);
            return (byte)(dz > 0 ? 7 : 5);
        }
    }
}
