#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The arithmetic of a shot (design 47 §2a, §2c): the per-cell curve, the gun's own fall-off, the
    /// power over whole and part cells, the table the design promises, the scatter, the dead zone and
    /// the flight. Integer throughout, so every number here is exact.
    /// </summary>
    public class RangedMathTests
    {
        /// <summary>Rules that fire at a fixed Shooting level, whatever the pawn's skill says.</summary>
        sealed class AtLevel : RangedRules
        {
            public int Level;
            public override int ShootingLevel(Pawn pawn) => Level;
        }

        static PawnContent Content => ContentPack.Pawns();

        [Test]
        public void ThePerCellCurveIsTheDesignsThreePoints()
        {
            CombatDef combat = Content.Combat;
            Assert.That(combat.ShootingPerCellPerMille(0), Is.EqualTo(747));
            Assert.That(combat.ShootingPerCellPerMille(10), Is.EqualTo(903));
            Assert.That(combat.ShootingPerCellPerMille(20), Is.EqualTo(951));
            Assert.That(combat.ShootingPerCellPerMille(5), Is.EqualTo(747 + (903 - 747) * 5 / 10), "linear between");
            Assert.That(combat.ShootingPerCellPerMille(25), Is.EqualTo(951), "flat past the end");
        }

        [Test]
        public void ThePistolsAccuracyFallsAwayWithDistanceAndIsFlatPastTheEnds()
        {
            RangedDef pistol = Content.Items[ItemIndex.Pistol].weapon!.ranged!;
            Assert.That(pistol.AccuracyPerMille(0), Is.EqualTo(800));
            Assert.That(pistol.AccuracyPerMille(3_000), Is.EqualTo(800));
            Assert.That(pistol.AccuracyPerMille(7_500), Is.EqualTo(750), "half way from 3 m to 12 m");
            Assert.That(pistol.AccuracyPerMille(12_000), Is.EqualTo(700));
            Assert.That(pistol.AccuracyPerMille(25_000), Is.EqualTo(400));
            Assert.That(pistol.AccuracyPerMille(40_000), Is.EqualTo(300));
            Assert.That(pistol.AccuracyPerMille(90_000), Is.EqualTo(300));
            Assert.That(pistol.rangeMm, Is.EqualTo(26_000));
            Assert.That(pistol.speedMmPerTick, Is.EqualTo(1_000));
        }

        [Test]
        public void ThePowerCountsWholeCellsAndInterpolatesThePart()
        {
            Assert.That(RangedRules.PowPerMille(747, 0), Is.EqualTo(1_000), "no distance, no fall-off");
            Assert.That(RangedRules.PowPerMille(747, 2_500), Is.EqualTo(747), "one whole cell");
            Assert.That(RangedRules.PowPerMille(747, 5_000), Is.EqualTo(747 * 747 / 1_000), "two");
            // Half a cell more: 747 × (1000 − 500 + 500 × 747 / 1000) / 1000.
            Assert.That(RangedRules.PowPerMille(747, 3_750), Is.EqualTo(747 * (500 + 500 * 747 / 1_000) / 1_000));
            // A layer up is 3 m, not 2.5: the same shot straight up is longer, so harder.
            Assert.That(RangedRules.PowPerMille(903, 3_000), Is.LessThan(RangedRules.PowPerMille(903, 2_500)));
        }

        /// <summary>
        /// The design's table (§2a), to within a percentage point: the shape it promises — a close
        /// weapon at any skill, the skill buying the middle distance — is the number, not a claim.
        /// </summary>
        [TestCase(0, 1, 600)]
        [TestCase(10, 1, 720)]
        [TestCase(20, 1, 760)]
        [TestCase(0, 5, 160)]
        [TestCase(10, 5, 410)]
        [TestCase(20, 5, 540)]
        [TestCase(0, 10, 20)]
        [TestCase(10, 10, 140)]
        [TestCase(20, 10, 240)]
        public void TheHitChanceIsTheDesignsTable(int level, int cells, int expected)
        {
            var colony = Board();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            var rules = new AtLevel { Level = level };
            int chance = rules.HitChancePerMille(shooter, cells * GridSize.CellSizeXZMm,
                Weapon(colony.Pawns, ItemIndex.Pistol), colony.Pawns);
            Assert.That(chance, Is.EqualTo(expected).Within(10), $"level {level} at {cells} cells");
        }

        [Test]
        public void NoShotIsWorseThanTheFloor()
        {
            var colony = Board();
            Pawn shooter = colony.Pawns.Pawns.All[0];
            int chance = new AtLevel { Level = 0 }.HitChancePerMille(shooter, 40_000,
                Weapon(colony.Pawns, ItemIndex.Pistol), colony.Pawns);
            Assert.That(chance, Is.EqualTo(colony.Pawns.Content.Combat.hitFloorPerMille));
        }

        /// <summary>
        /// Four thousand shots at one, five and ten cells land within three points of the chance they
        /// were rolled against: the roll is the chance, not something near it.
        /// </summary>
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(10)]
        public void TheMeasuredHitRateIsTheChance(int cells)
        {
            var colony = Board();
            Pawn shooter = colony.Pawns.Pawns.All[0], target = colony.Pawns.Pawns.All[1];
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, target, Near(colony, cells, 0));
            SetShooting(shooter, 10);
            Armament pistol = Weapon(colony.Pawns, ItemIndex.Pistol);
            var rules = new RangedRules();
            int distance = RangedGeometry.DistanceMm(Size, shooter.Cell, target.Cell);
            int chance = rules.HitChancePerMille(shooter, distance, pistol, colony.Pawns);

            int hits = 0;
            const int Shots = 4_000;
            for (int tick = 0; tick < Shots; tick++)
                if (rules.Resolve(shooter, target, pistol, colony.Pawns, tick).Aimed) hits++;
            Assert.That(hits * 1_000 / Shots, Is.EqualTo(chance).Within(30), $"{cells} cells, chance {chance}");
        }

        [Test]
        public void TheScatterGrowsWithHowBadTheShotWas()
        {
            var colony = Board();
            var rules = new RangedRules();
            Assert.That(rules.ScatterRadius(760, colony.Pawns), Is.EqualTo(1), "a good shot's miss passes close");
            Assert.That(rules.ScatterRadius(20, colony.Pawns), Is.EqualTo(3), "a hopeless one's goes wide");
            Assert.That(rules.ScatterRadius(1_000, colony.Pawns), Is.EqualTo(1));
        }

        /// <summary>
        /// A miss's cell is never the target's, never further than the radius, always in the target's
        /// own layer, always on the board — at a corner of the board too, where the box is cut short.
        /// </summary>
        [Test]
        public void TheScatterCellIsNearTheTargetInItsLayerAndNeverOnIt()
        {
            var size = new GridSize(20, 20, 4);
            foreach (int centre in new[] { size.Index(10, 10, 2), size.Index(0, 0, 1), size.Index(19, 0, 3) })
            {
                CellRef c = size.FromIndex(centre);
                var seen = new HashSet<int>();
                for (int tick = 0; tick < 500; tick++)
                {
                    var roll = DeterministicRandom.ForTick(7u, tick, PawnPurpose.RangedScatter);
                    int cell = RangedRules.ScatterCell(size, centre, 3, roll);
                    CellRef s = size.FromIndex(cell);
                    Assert.That(cell, Is.Not.EqualTo(centre));
                    Assert.That(s.Y, Is.EqualTo(c.Y), "in the target's own layer");
                    Assert.That(System.Math.Abs(s.X - c.X) <= 3 && System.Math.Abs(s.Z - c.Z) <= 3, Is.True);
                    seen.Add(cell);
                }
                int boxX = System.Math.Min(c.X + 3, 19) - System.Math.Max(c.X - 3, 0) + 1;
                int boxZ = System.Math.Min(c.Z + 3, 19) - System.Math.Max(c.Z - 3, 0) + 1;
                Assert.That(seen.Count, Is.EqualTo(boxX * boxZ - 1), "every cell of the box, and only those, is drawn");
            }
        }

        [Test]
        public void TheDeadZoneRampsFromNothingToTheSpeciesChance()
        {
            var colony = Board();
            Pawn bystander = colony.Pawns.Pawns.All[0];
            var rules = new RangedRules();
            int person = bystander.Species.interceptPerMille;
            Assert.That(person, Is.EqualTo(400));
            Assert.That(rules.InterceptPerMille(bystander, 2_500, colony.Pawns), Is.EqualTo(0));
            Assert.That(rules.InterceptPerMille(bystander, 5_000, colony.Pawns), Is.EqualTo(0), "the edge of the dead zone");
            Assert.That(rules.InterceptPerMille(bystander, 8_500, colony.Pawns), Is.EqualTo(person / 2), "half way");
            Assert.That(rules.InterceptPerMille(bystander, 12_000, colony.Pawns), Is.EqualTo(person));
            Assert.That(rules.InterceptPerMille(bystander, 25_000, colony.Pawns), Is.EqualTo(person));
        }

        [Test]
        public void AFlightIsDistanceOverSpeedRoundedUpAndNeverUnderATick()
        {
            var colony = Board();
            Armament pistol = Weapon(colony.Pawns, ItemIndex.Pistol);
            Assert.That(RangedRules.FlightTicks(0, pistol), Is.EqualTo(1));
            Assert.That(RangedRules.FlightTicks(2_500, pistol), Is.EqualTo(3));
            Assert.That(RangedRules.FlightTicks(25_000, pistol), Is.EqualTo(25));
            Assert.That(RangedRules.FlightTicks(25_001, pistol), Is.EqualTo(26));
        }

        /// <summary>
        /// Each roll is its own stream (the melee rule, <c>CombatMathTests.EachRollIsItsOwnStream</c>):
        /// whether a shot is aimed true is exactly the hit stream's draw against the chance, whatever
        /// the damage or the scatter drew.
        /// </summary>
        [Test]
        public void EachRollIsItsOwnStream()
        {
            var colony = Board();
            Pawn shooter = colony.Pawns.Pawns.All[0], target = colony.Pawns.Pawns.All[1];
            Stand(colony, shooter, Near(colony, 0, 0));
            Stand(colony, target, Near(colony, 4, 0));
            Armament pistol = Weapon(colony.Pawns, ItemIndex.Pistol);
            var rules = new RangedRules();
            int chance = rules.HitChancePerMille(shooter, RangedGeometry.DistanceMm(Size, shooter.Cell, target.Cell),
                pistol, colony.Pawns);
            for (int tick = 0; tick < 200; tick++)
            {
                var hit = DeterministicRandom.ForTick(colony.Pawns.Seed, tick, PawnPurpose.RangedHit ^ (uint)shooter.Id.Value);
                bool expected = hit.NextInt(1_000) < chance;
                Assert.That(rules.Resolve(shooter, target, pistol, colony.Pawns, tick).Aimed, Is.EqualTo(expected));
            }
        }

        [Test]
        public void TheFourSaltsAreDistinctFromEveryOtherPurpose()
        {
            var all = new List<uint>();
            foreach (var field in typeof(PawnPurpose).GetFields())
                if (field.FieldType == typeof(uint)) all.Add((uint)field.GetValue(null)!);
            foreach (uint salt in new[] { PawnPurpose.RangedHit, PawnPurpose.RangedDamage, PawnPurpose.RangedScatter, PawnPurpose.RangedIntercept })
                Assert.That(all.FindAll(v => v == salt).Count, Is.EqualTo(1), $"0x{salt:X8} is claimed twice");
        }

        internal static void SetShooting(Pawn pawn, int level) =>
            pawn.Skills[SkillIndex.Shooting] = pawn.Content.Skills[SkillIndex.Shooting].ExperienceForLevel(level);
    }
}
