#nullable enable
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using static Odyssey.Tests.Sim.CombatFixture;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The body (design 43 §2–§4, §9): six regions over the pool, pain shock, the vital regions,
    /// the ledger's merge and overflow, bleeding, a tend stopping it, the rates, the save and the
    /// snapshot. Every claim is set beside the same thing one thousandth short, or beside a pawn
    /// with nothing the claim is about.
    /// </summary>
    public class HealthTests
    {
        const int Head = 0, Torso = 1, ArmLeft = 2, ArmRight = 3, LegLeft = 4, LegRight = 5;

        static (ColonyWorld colony, Pawn victim, Pawn by) Two(int beds = -1)
        {
            var colony = Board(colonists: 2, beds: beds);
            colony.World.Tick(5);
            foreach (Pawn pawn in colony.Pawns.Pawns.All)
                for (int w = 0; w < pawn.WorkPriorities.Length; w++) pawn.WorkPriorities[w] = 0;
            return (colony, colony.Pawns.Pawns.All[0], colony.Pawns.Pawns.All[1]);
        }

        static bool Hurt(ColonyWorld colony, Pawn victim, int milli, AfflictionKind kind, int region, Pawn? by = null) =>
            colony.Pawns.Combat!.Hurt(victim, by, milli, kind, HitSet.Melee, -1, colony.World.CurrentTick, region);

        static int Loss(Pawn pawn) => pawn.HpMaxMilli - pawn.HpMilli;

        // ---- the ledger ------------------------------------------------------------------------

        [Test]
        public void EveryPointOfTheBlowIsOnTheLedgerAndThePoolAgrees()
        {
            var (colony, victim, by) = Two();
            for (int i = 0; i < 6; i++)
            {
                Strike(colony, by, victim, 7_345);
                Assert.That(victim.Health, Is.Not.Null, "a person was hurt and has no ledger");
                Assert.That(victim.Health!.TotalSeverityMilli, Is.EqualTo(Loss(victim)), $"blow {i}: the pool and the ledger disagree");
                if (victim.Downed) break;
            }
            Assert.That(victim.Health!.Count, Is.GreaterThan(1), "six blows all landed on one region: the roll never moved");
        }

        [Test]
        public void ALimbPassesWhatItCannotHoldToTheTorso()
        {
            var (colony, victim, _) = Two();
            Hurt(colony, victim, 45_000, AfflictionKind.Bruise, ArmLeft);
            PawnHealth health = victim.Health!;
            Assert.That(health.RegionDamageMilli(ArmLeft), Is.EqualTo(30_000), "the arm holds its thirty");
            Assert.That(health.RegionDamageMilli(Torso), Is.EqualTo(15_000), "the rest passed to the torso");
            Assert.That(health.TotalSeverityMilli, Is.EqualTo(Loss(victim)));

            var (c2, v2, _) = Two();
            Hurt(c2, v2, 29_000, AfflictionKind.Bruise, ArmLeft);
            Assert.That(v2.Health!.RegionDamageMilli(Torso), Is.EqualTo(0), "the control: an arm not full passes nothing");
        }

        [Test]
        public void TwoInjuriesOfAKindOnARegionAreOneRecord()
        {
            var (colony, victim, _) = Two();
            Hurt(colony, victim, 5_000, AfflictionKind.Wound, LegLeft);
            Hurt(colony, victim, 6_000, AfflictionKind.Wound, LegLeft);
            Hurt(colony, victim, 4_000, AfflictionKind.Bruise, LegLeft);
            PawnHealth health = victim.Health!;
            Assert.That(health.Count, Is.EqualTo(2), "a second cut made a second record");
            Assert.That(health[health.Find(LegLeft, AfflictionKind.Wound)].SeverityMilli, Is.EqualTo(11_000));
            Assert.That(health.Find(LegLeft, AfflictionKind.Bruise), Is.GreaterThanOrEqualTo(0), "the control: another kind is its own record");
        }

        [Test]
        public void ASharpBlowCutsAndABluntOneBruises()
        {
            var (colony, victim, by) = Two();
            var ctx = colony.Pawns;
            ctx.Combat!.ApplySwing(by, victim, Weapon(ctx, ItemHandle.Machete), Blow(5_000), colony.World.CurrentTick);
            Assert.That(victim.Health![0].Kind, Is.EqualTo(AfflictionKind.Wound));
            Assert.That(victim.Health[0].Bleeding, Is.True);

            var (c2, v2, b2) = Two();
            Strike(c2, b2, v2, 5_000);
            Assert.That(v2.Health![0].Kind, Is.EqualTo(AfflictionKind.Bruise), "the control: fists bruise");
            Assert.That(v2.Health[0].Bleeding, Is.False);
        }

        [Test]
        public void AnAnimalKeepsThePoolAlone()
        {
            var (colony, _, by) = Two();
            Pawn hog = Spawn(colony, PawnKindIndex.MiddenHog, Near(colony, 3, 3));
            Strike(colony, by, hog, 10_000);
            Assert.That(hog.HpMilli, Is.EqualTo(hog.HpMaxMilli - 10_000));
            Assert.That(hog.Health, Is.Null, "an animal grew a ledger");
            Assert.That(hog.Body, Is.Null);
        }

        // ---- pain and the downed line (design 43 §3) ------------------------------------------

        [Test]
        public void PainShockDownsAPersonAtSixtyFourPointsAndNotBefore()
        {
            var (colony, victim, _) = Two();
            Hurt(colony, victim, 30_000, AfflictionKind.Bruise, ArmLeft);
            Hurt(colony, victim, 30_000, AfflictionKind.Bruise, ArmRight);
            Hurt(colony, victim, 3_999, AfflictionKind.Bruise, LegLeft);
            Assert.That(victim.CurrentVitals().PainPerMille, Is.EqualTo(799));
            Assert.That(victim.Downed, Is.False, "the control: 63.999 points is standing");

            Hurt(colony, victim, 1, AfflictionKind.Bruise, LegRight);
            Assert.That(victim.CurrentVitals().PainPerMille, Is.EqualTo(800));
            Assert.That(victim.Downed, Is.True, "64 points of pain did not down her");
            Assert.That(victim.HpMilli, Is.EqualTo(36_000), "down from pain with the pool well above nought");
            Assert.That(victim.CurrentJob?.DefIndex, Is.EqualTo(JobIndex.Downed));
        }

        [Test]
        public void AVitalRegionAtNoughtDownsAndDoesNotKill()
        {
            var (colony, victim, _) = Two();
            Hurt(colony, victim, 24_999, AfflictionKind.Bruise, Head);
            Assert.That(victim.Downed, Is.False, "the control: a head with a thousandth left is standing");

            Hurt(colony, victim, 1, AfflictionKind.Bruise, Head);
            Assert.That(victim.CurrentVitals().ConsciousnessPerMille, Is.EqualTo(0));
            Assert.That(victim.Downed, Is.True, "a head at nought left her standing");
            colony.World.Tick(600);
            Assert.That(colony.Pawns.Pawns.Get(victim.Id), Is.SameAs(victim),
                "a vital region killed: death is the pool's line and blood loss (design 33 §3)");
        }

        [Test]
        public void BothLegsAtNoughtDownButOneDoesNot()
        {
            var (colony, victim, _) = Two();
            Hurt(colony, victim, 30_000, AfflictionKind.Bruise, LegLeft);
            Assert.That(victim.Downed, Is.False, "the control: one leg gone is a limp");
            Assert.That(victim.CurrentVitals().MovingPerMille, Is.LessThan(600));
            Hurt(colony, victim, 25_500, AfflictionKind.Bruise, LegRight);
            Assert.That(victim.CurrentVitals().MovingPerMille, Is.LessThanOrEqualTo(150));
            Assert.That(victim.Downed, Is.True, "moving at 15 % or less did not down her");
        }

        // ---- the rates (design 43 §9, design 17 §4e) --------------------------------------------

        [Test]
        public void AHurtLegSlowsTheWalkAndAHurtArmTheWork()
        {
            var (colony, victim, other) = Two();
            int walk = victim.MoveRatePerMille(), work = victim.WorkRatePerMille(WorkTypeIndex.Mining);
            Assert.That(victim.HealthMovingPerMille(), Is.EqualTo(1_000), "a whole pawn's factor is exact");

            Hurt(colony, victim, 15_000, AfflictionKind.Bruise, LegLeft);
            Assert.That(victim.MoveRatePerMille(), Is.LessThan(walk), "a hurt leg walks as fast");
            Hurt(colony, victim, 15_000, AfflictionKind.Bruise, ArmLeft);
            Assert.That(victim.WorkRatePerMille(WorkTypeIndex.Mining), Is.LessThan(work), "a hurt arm works as fast");

            Assert.That(other.HealthMovingPerMille(), Is.EqualTo(1_000), "the control: the other colonist is untouched");
        }

        // ---- blood (design 43 §4) ---------------------------------------------------------------

        [Test]
        public void AnUntendedTenPointCutKillsInFortyHoursAndABruiseNever()
        {
            var (colony, victim, _) = Two(beds: 0);
            Hurt(colony, victim, 10_000, AfflictionKind.Wound, LegLeft);
            int hour = colony.Pawns.Content.DayTicks / 24;

            colony.World.Tick(38 * hour);
            Assert.That(colony.Pawns.Pawns.Get(victim.Id), Is.SameAs(victim), "dead before forty hours");
            Assert.That(victim.Downed, Is.True, "past 60 % blood lost and still standing");
            Assert.That(victim.Health!.BloodLossMicro, Is.GreaterThan(900_000));

            colony.World.Tick(3 * hour);
            Assert.That(colony.Pawns.Pawns.Get(victim.Id), Is.Null, "still alive past forty hours of bleeding");
            Assert.That(colony.Pawns.Corpses.Count, Is.EqualTo(1), "bled out without a corpse");

            var (c2, v2, _) = Two(beds: 0);
            Hurt(c2, v2, 10_000, AfflictionKind.Bruise, LegLeft);
            c2.World.Tick(45 * hour);
            Assert.That(c2.Pawns.Pawns.Get(v2.Id), Is.SameAs(v2), "the control: a bruise bled");
            Assert.That(v2.Health!.BloodLossMicro, Is.EqualTo(0));
        }

        [Test]
        public void AnyTendStopsTheBleedingAndTheBloodComesBack()
        {
            var (colony, victim, _) = Two(beds: 0);
            Hurt(colony, victim, 10_000, AfflictionKind.Wound, LegLeft);
            int hour = colony.Pawns.Content.DayTicks / 24;
            colony.World.Tick(4 * hour);
            int lost = victim.Health!.BloodLossMicro;
            Assert.That(lost, Is.GreaterThan(0), "the control: an untended cut bleeds");

            Assert.That(colony.Pawns.Combat!.Tend(victim, 0), Is.EqualTo(1), "nought quality is still a tend");
            Assert.That(victim.Health.BleedingSeverityMilli, Is.EqualTo(0), "a tend left it bleeding");
            colony.World.Tick(4 * hour);
            Assert.That(victim.Health.BloodLossMicro, Is.LessThan(lost), "the blood did not come back");
        }

        [Test]
        public void ATendedInjuryHealsOutOfBed()
        {
            var (colony, victim, _) = Two(beds: 0);
            Hurt(colony, victim, 10_000, AfflictionKind.Bruise, ArmLeft);
            int day = colony.Pawns.Content.DayTicks;
            colony.World.Tick(day / 4);
            Assert.That(victim.HpMilli, Is.EqualTo(victim.HpMaxMilli - 10_000), "the control: untended and out of bed, nothing heals");

            colony.Pawns.Combat!.Tend(victim, 1_000);
            colony.World.Tick(day / 4);
            Assert.That(Loss(victim), Is.EqualTo(10_000 - 12_000 / 4).Within(50), "a full-quality tend is twelve points a day");
            Assert.That(victim.Health!.TotalSeverityMilli, Is.EqualTo(Loss(victim)), "healed the pool and not the ledger");
        }

        // ---- save, hash, snapshot (design 43 §9) -------------------------------------------------

        [Test]
        public void TheLedgerSurvivesTheRoundTripAndTheHashSeesIt()
        {
            var (colony, victim, _) = Two();
            Hurt(colony, victim, 12_000, AfflictionKind.Wound, LegLeft);
            Hurt(colony, victim, 9_000, AfflictionKind.Bruise, Torso);
            colony.Pawns.Combat!.Tend(victim, 640);
            Hurt(colony, victim, 3_000, AfflictionKind.Wound, ArmRight);
            victim.Health!.BloodLossMicro = 123_456;

            var restored = Board(colonists: 2);
            restored.Load(colony.Save());
            Pawn back = restored.Pawns.Pawns.Get(victim.Id)!;
            Assert.That(back.Health, Is.Not.Null, "the ledger did not come back");
            Assert.That(back.Health!.Count, Is.EqualTo(3));
            Assert.That(back.Health.BloodLossMicro, Is.EqualTo(123_456));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(back.Health[i].Region, Is.EqualTo(victim.Health[i].Region));
                Assert.That(back.Health[i].Kind, Is.EqualTo(victim.Health[i].Kind));
                Assert.That(back.Health[i].SeverityMilli, Is.EqualTo(victim.Health[i].SeverityMilli));
                Assert.That(back.Health[i].Tended, Is.EqualTo(victim.Health[i].Tended));
                Assert.That(back.Health[i].TendQualityPerMille, Is.EqualTo(victim.Health[i].TendQualityPerMille));
            }
            Assert.That(restored.World.ComputeStateHash().Value, Is.EqualTo(colony.World.ComputeStateHash().Value));

            ulong before = colony.World.ComputeStateHash().Value;
            victim.Health.BloodLossMicro++;
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(before), "the hash cannot see the blood");
        }

        [Test]
        public void APawnHealedWholeHashesAsOneNeverHurt()
        {
            var (colony, victim, _) = Two();
            var (twin, _, _) = Two();
            Assert.That(colony.World.ComputeStateHash().Value, Is.EqualTo(twin.World.ComputeStateHash().Value));

            Hurt(colony, victim, 5_000, AfflictionKind.Bruise, Torso);
            Assert.That(colony.World.ComputeStateHash().Value, Is.Not.EqualTo(twin.World.ComputeStateHash().Value),
                "the control: a bruise is seen");
            RaiseHp(victim, victim.HpMaxMilli);
            Assert.That(victim.HasHealthState, Is.False, "healed whole and still carrying a ledger");
            Assert.That(colony.World.ComputeStateHash().Value, Is.EqualTo(twin.World.ComputeStateHash().Value));
        }

        [Test]
        public void OnlyTheHurtPublishTheirBodies()
        {
            var (colony, victim, other) = Two(beds: 0);
            Hurt(colony, victim, 12_000, AfflictionKind.Wound, LegLeft);
            colony.World.Tick();
            WorldSnapshot view = colony.World.Views.Current;

            Assert.That(view.TryGetPawnAspect(victim.Id, HealthAspects.Pain, out int pain), Is.True);
            Assert.That(pain, Is.EqualTo(150));
            Assert.That(view.TryGetPawnAspect(victim.Id, HealthAspects.Region[LegLeft], out int leg), Is.True);
            Assert.That(leg, Is.EqualTo(600));
            Assert.That(view.TryGetPawnAspect(victim.Id, HealthAspects.BleedHours, out int hours), Is.True);
            Assert.That(hours, Is.InRange(32, 34), "twelve points untended is about thirty-three hours");
            Assert.That(view.TryGetPawnAspect(victim.Id, HealthAspects.Injury[LegLeft * HealthAspects.Kinds], out int points), Is.True);
            Assert.That(points, Is.EqualTo(12_000));
            Assert.That(view.TryGetPawnAspect(victim.Id, HealthAspects.Care[LegLeft * HealthAspects.Kinds], out int care), Is.True);
            Assert.That(care, Is.EqualTo(0), "untended is nought");

            Assert.That(view.TryGetPawnAspect(other.Id, HealthAspects.Pain, out _), Is.False, "the control: the whole publish nothing");

            colony.Pawns.Combat!.Tend(victim, 640);
            colony.World.Tick();
            view = colony.World.Views.Current;
            Assert.That(view.TryGetPawnAspect(victim.Id, HealthAspects.BleedHours, out _), Is.False, "tended and still counting down");
            view.TryGetPawnAspect(victim.Id, HealthAspects.Care[LegLeft * HealthAspects.Kinds], out care);
            Assert.That(care, Is.EqualTo(641), "tended is one plus the quality");
        }

        // ---- the arithmetic, apart from any pawn --------------------------------------------------

        [TestCase(1, 15_000)]
        [TestCase(2, 42_420)]
        [TestCase(3, 77_940)]
        [TestCase(4, 120_000)]
        [TestCase(5, 167_700)]
        public void AFallIsFifteenTimesTheLayersToTheOneAndAHalf(int layers, int milli)
        {
            HealthDef body = ContentPackBody();
            Assert.That(body.FallDamageMilli(layers), Is.EqualTo(milli));
        }

        [Test]
        public void TheIntegerRootIsExact()
        {
            for (long v = 0; v < 5_000; v++)
            {
                long r = HealthDef.IntegerSqrt(v);
                Assert.That(r * r <= v && (r + 1) * (r + 1) > v, Is.True, $"sqrt({v}) = {r}");
            }
            Assert.That(HealthDef.IntegerSqrt(16_000_000), Is.EqualTo(4_000));
        }

        static HealthDef ContentPackBody() =>
            Odyssey.Sim.Defs.ContentPack.Pawns().HealthOf(PawnKindIndex.Colonist)!;
    }
}
