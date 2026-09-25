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

        /// <summary>
        /// The cell the bullet ends in if nothing takes it first: the target's on a shot aimed true
        /// (which follows the target at the impact), and on a miss the cell the line past the target
        /// first stops in.
        /// </summary>
        public readonly int EndCell;

        /// <summary>Ticks in the air: <c>max(1, ceil(distance / speed))</c> from the shooter to <see cref="EndCell"/>.</summary>
        public readonly int FlightTicks;

        /// <summary>
        /// The cover the target had from this shot, per mille (design 50 §2): what the cover roll
        /// was made against, nought in the open or when the aim roll already missed.
        /// </summary>
        public readonly int CoverPerMille;

        /// <summary>
        /// The piece of cover a shot the cover roll defeated is fired into (design 50 §2d), or -1.
        /// When set, <see cref="Aimed"/> is false and <see cref="EndCell"/> is this cell.
        /// </summary>
        public readonly int CoverCell;

        public ShotOutcome(bool aimed, int hitPerMille, int damageMilli, int endCell, int flightTicks,
            int coverPerMille = 0, int coverCell = -1)
        {
            Aimed = aimed;
            HitPerMille = hitPerMille;
            DamageMilli = damageMilli;
            EndCell = endCell;
            FlightTicks = flightTicks;
            CoverPerMille = coverPerMille;
            CoverCell = coverCell;
        }

        /// <summary>The chance the player is told, per mille: the aim times what the cover leaves (design 50 §2d).</summary>
        public int TotalPerMille => HitPerMille * (1_000 - CoverPerMille) / 1_000;
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
        /// How well a target in <paramref name="targetCell"/> is covered from a shot out of
        /// <paramref name="shooterCell"/>, per mille (design 50 §2): <see cref="Cover.Evaluate"/>,
        /// with the contributors written into <paramref name="report"/> when one is given.
        /// </summary>
        int CoverPerMille(int shooterCell, int targetCell, PawnContext ctx, CoverReport? report = null);

        /// <summary>
        /// The chance, per mille, that a stray crossing a cell whose cover is
        /// <paramref name="basePerMille"/> is caught by it (design 50 §2e): half the base (the
        /// combat Def's <c>coverInterceptPerMille</c>) times the dead zone's ramp from the shooter,
        /// so a colonist's own sandbags never take her outgoing shots.
        /// </summary>
        int CoverInterceptPerMille(int basePerMille, int distanceFromShooterMm, PawnContext ctx);

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
    /// distance</c>, floored. Cover is a second roll after this one (design 50 §2d), so this is
    /// the aim alone. The power is a loop over whole 2.5 m cells with the fraction
    /// of a cell interpolated linearly, so a shot up a layer — longer, because a layer is 3 m —
    /// is harder than one along it by exactly its extra length, and by nothing else.</para>
    ///
    /// <para><b>A miss carries on past its target</b> along the line of fire, by
    /// <c>min(max, 1 + (1000 − hit‰) × k / 1000)</c> cells, and ends where that line first stops —
    /// a wall, a slab, the ground (<see cref="LineOfSight.StopCell"/>). A good shot's miss lands just
    /// behind the target; a hopeless one's a little further. <b>Changed on the owner's first play
    /// (2026-09-25)</b>: the first rule drew a cell from a box round the target in its layer, which
    /// from a height put misses inside the terrace or off to one side, "not even in a place a gun
    /// would fire to".</para>
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
            // The gun's quality (design 47 §11).
            chance = WeaponQuality.Accuracy((int)chance, armament);
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

            // Cover is the second roll (design 50 §2d), made only after an aim that was true: the
            // reference's order, and why a miss never wears a sandbag down by the roll — only by
            // crossing it (§2e). A shot the cover wins is fired into one piece, chosen in
            // proportion to what each gave.
            int cover = 0, coverCell = -1;
            if (aimed)
            {
                cover = CoverPerMille(shooter.Cell, target.Cell, ctx, _cover);
                if (cover > 0 && DeterministicRandom.ForTick(ctx.Seed, tick, PawnPurpose.RangedCover ^ who).NextInt(1_000) < cover)
                {
                    int sum = _cover.Sum;
                    if (sum > 0)
                        coverCell = _cover.CellForPick(
                            DeterministicRandom.ForTick(ctx.Seed, tick, PawnPurpose.RangedCoverPick ^ who).NextInt(sum));
                    if (coverCell >= 0) aimed = false;
                }
            }

            int damage = WeaponQuality.Damage(MeleeRules.DamageMilli(armament.Attack, ctx,
                DeterministicRandom.ForTick(ctx.Seed, tick, PawnPurpose.RangedDamage ^ who)), armament);

            int end = aimed ? target.Cell
                : coverCell >= 0 ? coverCell
                : MissCell(ctx, shooter.Cell, target.Cell, ScatterRadius(hitPerMille, ctx),
                    DeterministicRandom.ForTick(ctx.Seed, tick, PawnPurpose.RangedScatter ^ who));

            return new ShotOutcome(aimed, hitPerMille, damage, end,
                FlightTicks(RangedGeometry.DistanceMm(size, shooter.Cell, end), armament), cover, coverCell);
        }

        /// <summary>The scratch the shot's cover is worked into: one per rules, and the simulation has one thread.</summary>
        readonly CoverReport _cover = new CoverReport();

        public virtual int CoverPerMille(int shooterCell, int targetCell, PawnContext ctx, CoverReport? report = null) =>
            Cover.Evaluate(ctx, shooterCell, targetCell, report);

        public virtual int CoverInterceptPerMille(int basePerMille, int distanceFromShooterMm, PawnContext ctx)
        {
            CombatDef combat = ctx.Content.Combat;
            int chance = basePerMille * combat.coverInterceptPerMille / 1_000;
            return Ramp(chance, distanceFromShooterMm, combat);
        }

        /// <summary>The dead zone's ramp (design 47 §2c): nought within it, the whole of <paramref name="chance"/> past its far edge, linear between.</summary>
        static int Ramp(int chance, int distanceFromShooterMm, CombatDef combat)
        {
            int near = combat.interceptDeadZoneMm, full = combat.interceptFullMm;
            if (distanceFromShooterMm <= near) return 0;
            if (distanceFromShooterMm >= full || full <= near) return chance;
            return (int)((long)chance * (distanceFromShooterMm - near) / (full - near));
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
        /// Where a miss ends: the line from <paramref name="from"/> through <paramref name="target"/>
        /// carried on by one to <paramref name="reach"/> cells (drawn on <paramref name="roll"/>), on the
        /// board, then cut where it first stops (<see cref="LineOfSight.StopCell"/>). Never the target's
        /// own cell. The extension keeps the shot's own direction in the ground plane and the target's
        /// layer, so a shot fired down off a terrace carries on at the target's height and comes down
        /// behind it.
        /// </summary>
        public static int MissCell(PawnContext ctx, int from, int target, int reach, DeterministicRandom roll)
        {
            GridSize size = ctx.Size;
            CellRef s = size.FromIndex(from), t = size.FromIndex(target);
            int dx = t.X - s.X, dz = t.Z - s.Z;
            int length = System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dz));
            int along = 1 + (reach > 1 ? roll.NextInt(reach) : 0);
            int x = t.X, z = t.Z;
            if (length > 0)
            {
                // Rounded to the nearest cell, half away from nought, in integers: the continuation of
                // the line, not of its dominant axis.
                x = t.X + RoundDiv(dx * along, length);
                z = t.Z + RoundDiv(dz * along, length);
            }
            else x = t.X + along;
            x = x < 0 ? 0 : x >= size.SizeX ? size.SizeX - 1 : x;
            z = z < 0 ? 0 : z >= size.SizeZ ? size.SizeZ - 1 : z;
            int end = size.Index(x, z, t.Y);
            if (end == target) return target == from ? target : LineOfSight.StopCell(ctx, from, target);
            return LineOfSight.StopCell(ctx, from, end);
        }

        /// <summary><paramref name="n"/> / <paramref name="d"/> rounded to the nearest, half away from nought. <paramref name="d"/> is positive.</summary>
        static int RoundDiv(int n, int d) => n >= 0 ? (2 * n + d) / (2 * d) : -((-2 * n + d) / (2 * d));

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
            return Ramp(bystander.Species.interceptPerMille, distanceFromShooterMm, ctx.Content.Combat);
        }
    }
}
