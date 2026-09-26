#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The questions every part of the ranged fight asks (design 47 §2d, §2e), beside
    /// <see cref="Melee"/> and for its reason: one owner for each answer, because the driver, the
    /// resolver, the hold and the think nodes all ask them. Read off saved state only — cells, the
    /// job, the hand — never off a path.
    /// </summary>
    public static class Ranged
    {
        /// <summary>Does <paramref name="armament"/> shoot?</summary>
        public static bool IsGun(in Armament armament) => armament.Attack.IsRanged;

        /// <summary>
        /// Is it this pawn's tick to look for something in sight (design 47 §2d)? Every
        /// <see cref="CombatDef.rangedScanTicks"/>, phase-spread by id as the healing is, so a line of
        /// shooters does not walk its lines on one tick.
        /// </summary>
        public static bool ScanDue(PawnContext ctx, Pawn pawn)
        {
            int every = ctx.Content.Combat.rangedScanTicks;
            return every <= 1 || (ctx.CurrentTick + pawn.Id.Value) % every == 0;
        }

        /// <summary>Is this pawn aiming right now — in a ranged attack, in its <c>Aim</c> toil?</summary>
        public static bool IsAiming(Pawn pawn) =>
            pawn.Driver is AttackRangedJobDriver { InAim: true };

        /// <summary>
        /// Is <paramref name="target"/> within the gun's range of <paramref name="from"/>, straight
        /// line in millimetres over the true cell? Integer, squared, no root.
        /// </summary>
        public static bool InRange(GridSize size, int from, int target, RangedDef ranged)
        {
            long range = ranged.rangeMm;
            return RangedGeometry.DistanceMm2(size, from, target) <= range * range;
        }

        /// <summary>In range, and the line between the two cells open (<see cref="LineOfSight.Clear"/>)?</summary>
        public static bool CanHit(PawnContext ctx, int from, int target, RangedDef ranged) =>
            InRange(ctx.Size, from, target, ranged) && LineOfSight.Clear(ctx, from, target);

        /// <summary>
        /// Can a gun be fired from <paramref name="cell"/> at all (design 47 §2e)? Not by a pawn in
        /// the water: both hands are in it. A ladder or a jump is a step in flight and an aim starts
        /// only at a boundary, so neither needs asking; a load is put down by the job that carried
        /// it before any attack begins.
        /// </summary>
        public static bool CanShootFrom(PawnContext ctx, int cell) =>
            ctx.Nav.Grid.CostClass[cell] != NaturalContent.CostClassShallowWater
            // Nor from on top of cover (design 53 §5): she climbs over it and fires from beside it.
            && Standing.CanStandAt(ctx, cell);

        /// <summary>
        /// The nearest pawn <paramref name="me"/> would shoot at unordered, within her gun's range
        /// and in sight, or null (design 47 §2d): for a colonist a threat to her
        /// (<see cref="Melee.IsThreatTo"/>), for anybody else a standing colonist. Nearest by squared
        /// millimetres, a tie to the lower id; <b>the line is walked only for a candidate nearer than
        /// the best so far</b>, so the walks are bounded by the hostiles in range.
        /// <para><b>Scales with the pawns on the board</b>: an integer comparison or two each, a
        /// squared distance for the candidates, and a line walk (a dozen or so array reads) for the
        /// few that could win.</para>
        /// </summary>
        public static Pawn? NearestTargetInSight(PawnContext ctx, Pawn me, RangedDef ranged)
        {
            GridSize size = ctx.Size;
            long range = ranged.rangeMm;
            long bestDistance = range * range + 1;
            Pawn? best = null;
            var pawns = ctx.Pawns.All;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == me || !Melee.IsStanding(other)) continue;
                bool foe = Allegiance.IsFoe(me, other);
                if (!foe) continue;
                long distance = RangedGeometry.DistanceMm2(size, me.Cell, other.Cell);
                if (distance > bestDistance || (distance == bestDistance && best != null && other.Id.Value > best.Id.Value)) continue;
                if (!LineOfSight.Clear(ctx, me.Cell, other.Cell)) continue;
                best = other;
                bestDistance = distance;
            }
            return best;
        }
    }
}
