#nullable enable
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pawns
{
    /// <summary>What one shot came to, decided on the tick it was fired and not yet applied. See <see cref="IRangedRules.Resolve"/>.</summary>
    public readonly struct ShotOutcome
    {
        /// <summary>Did the hit roll succeed? The bullet is aimed at the target's cell if so, at the scatter cell if not.</summary>
        public readonly bool Aimed;

        /// <summary>The chance the roll was made against, per mille — what the scatter's width is read from.</summary>
        public readonly int HitPerMille;

        /// <summary>
        /// Damage the bullet carries, in thousandths of a hit point: rolled for a miss too, because
        /// a stray still carries its weight into whoever it finds.
        /// </summary>
        public readonly int DamageMilli;

        /// <summary>The cell the bullet ends in if nothing takes it first.</summary>
        public readonly int EndCell;

        /// <summary>Ticks in the air: <c>max(1, ceil(distance / speed))</c> from the shooter to <see cref="EndCell"/>.</summary>
        public readonly int FlightTicks;

        public ShotOutcome(bool aimed, int hitPerMille, int damageMilli, int endCell, int flightTicks)
        {
            Aimed = aimed;
            HitPerMille = hitPerMille;
            DamageMilli = damageMilli;
            EndCell = endCell;
            FlightTicks = flightTicks;
        }
    }

    /// <summary>
    /// The arithmetic of a shot (design 47 §2a, §2c): whether it is aimed true, how hard it strikes,
    /// where a miss goes, how long it flies, and how likely a bystander on its line is to take it.
    /// The <see cref="IMeleeRules"/> shape: <b>decides and never applies</b>, and every roll is on its
    /// own stream salted by the shooter's id — hit on <see cref="PawnPurpose.RangedHit"/>, damage on
    /// <see cref="PawnPurpose.RangedDamage"/>, the scatter on <see cref="PawnPurpose.RangedScatter"/>,
    /// the interception on <see cref="PawnPurpose.RangedIntercept"/> salted by the cell too. The one
    /// implementation is <see cref="RangedRules"/>, settable on <see cref="PawnContext"/>.
    /// </summary>
    public interface IRangedRules
    {
        /// <summary>The Shooting level a pawn fires at: a person's <see cref="SkillIndex.Shooting"/>, nought for anything else.</summary>
        int ShootingLevel(Pawn pawn);

        /// <summary>The chance, per mille, that a shot over <paramref name="distanceMm"/> with this gun is aimed true.</summary>
        int HitChancePerMille(Pawn shooter, int distanceMm, in Armament armament, PawnContext ctx);

        /// <summary>
        /// Decide one shot <b>on the tick it is fired</b>: the hit, the damage (for a miss too), the
        /// end cell — the target's on a hit, a scatter cell round it on a miss — and the flight.
        /// Changes nothing; <c>CombatSystem</c> launches it and lands it.
        /// </summary>
        ShotOutcome Resolve(Pawn shooter, Pawn target, in Armament armament, PawnContext ctx, int tick);

        /// <summary>
        /// The chance, per mille, that a bystander standing on a crossed cell takes the bullet:
        /// its species' <see cref="SpeciesDef.interceptPerMille"/>, times the dead zone's ramp at
        /// its distance from where the bullet was fired.
        /// </summary>
        int InterceptPerMille(Pawn bystander, int distanceFromShooterMm, PawnContext ctx);
    }

    /// <summary>
    /// The one <see cref="IRangedRules"/> (design 47 §2a, §2c). Integer throughout, so it replays
    /// and hashes exactly; public, unsealed and virtual, per the code conventions.
    ///
    /// <para><b>The hit</b> is <c>pow(perCell(level), distance in cells) × the gun's accuracy at the
    /// distance × cover</c>, floored. The power is a loop over whole 2.5 m cells with the fraction
    /// of a cell interpolated linearly, so a shot up a layer — longer, because a layer is 3 m —
    /// is harder than one along it by exactly its extra length, and by nothing else.</para>
    ///
    /// <para><b>The scatter</b> is a cell drawn uniformly from the box of radius
    /// <c>min(max, 1 + (1000 − hit‰) × k / 1000)</c> round the target's cell, the centre excluded,
    /// in the target's own layer and on the board: a good shot's miss passes close, a hopeless
    /// one's goes wide. A sphere was rejected — it puts miss cells in the air or under a floor.</para>
    /// </summary>
    public class RangedRules : IRangedRules
    {
        public virtual int ShootingLevel(Pawn pawn) => pawn.IsPerson ? pawn.SkillLevel(SkillIndex.Shooting) : 0;

        public virtual int HitChancePerMille(Pawn shooter, int distanceMm, in Armament armament, PawnContext ctx)
        {
            CombatDef combat = ctx.Content.Combat;
            int perCell = combat.ShootingPerCellPerMille(ShootingLevel(shooter));
            long chance = PowPerMille(perCell, distanceMm);
            RangedDef? ranged = armament.Attack.ranged;
            if (ranged != null) chance = chance * ranged.AccuracyPerMille(distanceMm) / 1_000;
            chance = chance * combat.coverPerMille / 1_000;
            int floor = combat.hitFloorPerMille;
            return chance < floor ? floor : (int)chance;
        }

        /// <summary>
        /// <paramref name="perMille"/> raised to the distance in 2.5 m cells, per mille:
        /// <c>a^k × (1000 − f + f × a / 1000) / 1000</c> for <c>k</c> whole cells and <c>f</c> the
        /// remaining fraction of one, in thousandths. No float anywhere in it.
        /// </summary>
        public static int PowPerMille(int perMille, int distanceMm)
        {
            if (distanceMm <= 0) return 1_000;
            int cell = GridSize.CellSizeXZMm;
            int whole = distanceMm / cell;
            int fraction = (int)((long)(distanceMm - whole * cell) * 1_000 / cell);
            long p = 1_000;
            for (int i = 0; i < whole && p > 0; i++) p = p * perMille / 1_000;
            p = p * (1_000 - fraction + (long)fraction * perMille / 1_000) / 1_000;
            return (int)p;
        }

        public virtual ShotOutcome Resolve(Pawn shooter, Pawn target, in Armament armament, PawnContext ctx, int tick)
        {
            uint who = (uint)shooter.Id.Value;
            GridSize size = ctx.Size;
            int distance = RangedGeometry.DistanceMm(size, shooter.Cell, target.Cell);
            int hitPerMille = HitChancePerMille(shooter, distance, armament, ctx);

            var hit = DeterministicRandom.ForTick(ctx.Seed, tick, PawnPurpose.RangedHit ^ who);
            bool aimed = hit.NextInt(1_000) < hitPerMille;

            int damage = MeleeRules.DamageMilli(armament.Attack, ctx,
                DeterministicRandom.ForTick(ctx.Seed, tick, PawnPurpose.RangedDamage ^ who));

            int end = aimed ? target.Cell : ScatterCell(size, target.Cell, ScatterRadius(hitPerMille, ctx),
                DeterministicRandom.ForTick(ctx.Seed, tick, PawnPurpose.RangedScatter ^ who));

            return new ShotOutcome(aimed, hitPerMille, damage, end,
                FlightTicks(RangedGeometry.DistanceMm(size, shooter.Cell, end), armament));
        }

        /// <summary>The scatter's radius in cells for a shot made at <paramref name="hitPerMille"/>.</summary>
        public virtual int ScatterRadius(int hitPerMille, PawnContext ctx)
        {
            CombatDef combat = ctx.Content.Combat;
            int miss = 1_000 - hitPerMille;
            if (miss < 0) miss = 0;
            int r = 1 + miss * combat.scatterPerMissPerMille / 1_000;
            if (r > combat.scatterMaxCells) r = combat.scatterMaxCells;
            return r < 1 ? 1 : r;
        }

        /// <summary>
        /// A cell drawn uniformly from the <c>(2r + 1)²</c> box round <paramref name="centre"/>, the
        /// centre excluded, in its own layer, and on the board — the cells off the edge are not
        /// drawn at all, rather than clamped onto the edge, so no edge cell is likelier than another.
        /// </summary>
        public static int ScatterCell(GridSize size, int centre, int radius, DeterministicRandom roll)
        {
            CellRef c = size.FromIndex(centre);
            int x0 = c.X - radius < 0 ? 0 : c.X - radius;
            int x1 = c.X + radius >= size.SizeX ? size.SizeX - 1 : c.X + radius;
            int z0 = c.Z - radius < 0 ? 0 : c.Z - radius;
            int z1 = c.Z + radius >= size.SizeZ ? size.SizeZ - 1 : c.Z + radius;
            int count = (x1 - x0 + 1) * (z1 - z0 + 1) - 1;
            if (count <= 0) return centre;

            int pick = roll.NextInt(count);
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                if (x == c.X && z == c.Z) continue;
                if (pick-- == 0) return size.Index(x, z, c.Y);
            }
            return centre;
        }

        /// <summary>Ticks in the air over <paramref name="distanceMm"/>: <c>ceil(distance / speed)</c>, never under one.</summary>
        public static int FlightTicks(int distanceMm, in Armament armament)
        {
            int speed = armament.Attack.ranged?.speedMmPerTick ?? 0;
            if (speed <= 0) return 1;
            int ticks = (distanceMm + speed - 1) / speed;
            return ticks < 1 ? 1 : ticks;
        }

        public virtual int InterceptPerMille(Pawn bystander, int distanceFromShooterMm, PawnContext ctx)
        {
            CombatDef combat = ctx.Content.Combat;
            int near = combat.interceptDeadZoneMm, full = combat.interceptFullMm;
            int chance = bystander.Species.interceptPerMille;
            if (distanceFromShooterMm <= near) return 0;
            if (distanceFromShooterMm >= full || full <= near) return chance;
            return (int)((long)chance * (distanceFromShooterMm - near) / (full - near));
        }
    }
}
