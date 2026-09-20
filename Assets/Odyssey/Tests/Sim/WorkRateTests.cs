#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Work speed from skill (WS2, design 17 §3): the rate is a def curve
    /// <c>base + slope × level</c>, anchored on our own starting roll rather than the
    /// reference's level 8, with mining steepest, construction shallowest and hauling flat.
    ///
    /// <para>The eight integers are INVENTED and accepted by the owner; what the tests pin is
    /// that the integers in the XML are the ones the design table names, that the curve reads
    /// the levels the table names, that a master really does finish a fixed cell in the curve's
    /// ratio of ticks, that two colonists of different skill pay into one cell in one unit, and
    /// that no def, level or pawn can ever produce a rate of zero — a colonist who can never
    /// finish a job is an infinite loop in the job system.</para>
    /// </summary>
    public class WorkRateTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        /// <summary>A colonist whose skills never move while she works: the learning-factor seam
        /// at zero blocks gains, and decay is overridden away because the walk to a far face is
        /// long enough for a day's decay to drop a pinned master a level before she swings
        /// once.</summary>
        sealed class Pinned : Pawn
        {
            public Pinned(PawnId id, int cell, PawnContent content) : base(id, cell, content) { }
            public override int LearningFactorPerMille() => 0;
            public override void DecayExperience(int skill, int amount) { }
        }

        /// <summary>A colonist whose work rate is one number whatever she is asked to do — the
        /// plumbing test's stand-in for a curve.</summary>
        sealed class Fixed : Pawn
        {
            public Fixed(PawnId id, int cell, PawnContent content) : base(id, cell, content) { }
            public override int WorkRatePerMille(int workType) => 777;
        }

        static ColonyWorld Board(uint seed = 1u)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 0;
            scenario.beds = 0;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: true);
        }

        static Pinned Adopt(ColonyWorld colony, uint id = 1u)
        {
            var pawn = new Pinned(new PawnId((int)id), Size.Index(colony.Start),
                ContentPack.Pawns());
            colony.Pawns.Pawns.Adopt(pawn);
            return pawn;
        }

        /// <summary>Set a skill to the first experience of a level and freeze it there. Level
        /// 0 pins to experience 1 rather than 0, because an exactly-zero skill is what
        /// <see cref="Pawn.RollStartingSkills"/> reads as "not rolled yet" and fills on the
        /// next tick — the pin has to survive it.</summary>
        static void PinLevel(Pawn pawn, int skill, int level) =>
            pawn.Skills[skill] = level <= 0
                ? 1
                : pawn.Content.Skills[skill].ExperienceForLevel(level);

        /// <summary>The nearest cell of plain rock the given pawn could actually get at.</summary>
        static int NearestRock(ColonyWorld colony, Pawn pawn)
        {
            CellRef start = colony.Start;
            int best = -1, bestDistance = int.MaxValue;

            for (int y = 0; y < Size.SizeY; y++)
            for (int z = 0; z < Size.SizeZ; z++)
            for (int x = 0; x < Size.SizeX; x++)
            {
                int index = Size.Index(x, z, y);
                if (colony.Grid.Terrain[index] != NaturalContent.TerrainRock) continue;
                if (!colony.Designations.CanMine(index)) continue;
                if (MineWorkGiver.StandToMine(colony.Pawns, pawn, index) < 0) continue;

                int distance = System.Math.Abs(x - start.X) + System.Math.Abs(z - start.Z)
                             + System.Math.Abs(y - start.Y) * 4;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = index;
            }

            return best;
        }

        static int CeilDiv(int value, int divisor) => (value + divisor - 1) / divisor;

        /// <summary>
        /// Order the nearest rock mined by a colonist of the given mining level, and return the
        /// tick the face comes out — walk included, which cancels in a difference taken between
        /// two runs of the same seed, board and stance.
        /// </summary>
        static int TicksToDig(uint seed, int miningLevel)
        {
            ColonyWorld colony = Board(seed);
            Pawn pawn = Adopt(colony);
            PinLevel(pawn, SkillIndex.Mining, miningLevel);

            int rock = NearestRock(colony, pawn);
            Assume.That(rock, Is.GreaterThanOrEqualTo(0), "the board has no reachable rock");

            colony.Designations.Designate(Size.FromIndex(rock), DesignationKind.Mine);
            int tick = 0;
            for (; tick < 60_000 && colony.Grid.IsSolidTerrain(rock); tick++) colony.World.Tick();
            Assume.That(colony.Grid.IsSolidTerrain(rock), Is.False, "the rock never came out");
            return tick;
        }

        // ---- the table -----------------------------------------------------------------------

        [Test]
        public void TheCurveTableIsTheTenAcceptedIntegers()
        {
            var content = ContentPack.Pawns();

            var mining = content.WorkTypes[WorkTypeIndex.Mining];
            Assert.That(mining.rateSkill, Is.EqualTo(SkillIndex.Mining));
            Assert.That(mining.workRateBasePerMille, Is.EqualTo(550), "a novice miner");
            Assert.That(mining.workRateSlopePerLevel, Is.EqualTo(105), "the steepest curve");

            var cutting = content.WorkTypes[WorkTypeIndex.Cutting];
            Assert.That(cutting.rateSkill, Is.EqualTo(SkillIndex.Cutting));
            Assert.That(cutting.workRateBasePerMille, Is.EqualTo(600));
            Assert.That(cutting.workRateSlopePerLevel, Is.EqualTo(100));

            var construction = content.WorkTypes[WorkTypeIndex.Construction];
            Assert.That(construction.rateSkill, Is.EqualTo(SkillIndex.Construction));
            Assert.That(construction.workRateBasePerMille, Is.EqualTo(700),
                "the shallowest curve: skill buys quality here as well");
            Assert.That(construction.workRateSlopePerLevel, Is.EqualTo(75));

            // Growing took cutting's curve exactly when it got one (SK1): the two plant work
            // types, and the design once had felling training Growing outright, so a difference
            // between them would need a justification nobody has measured.
            var growing = content.WorkTypes[WorkTypeIndex.Growing];
            Assert.That(growing.rateSkill, Is.EqualTo(SkillIndex.Growing));
            Assert.That(growing.workRateBasePerMille, Is.EqualTo(cutting.workRateBasePerMille),
                "growing and chopping are the two plant work types and share one curve");
            Assert.That(growing.workRateSlopePerLevel, Is.EqualTo(cutting.workRateSlopePerLevel));

            var hauling = content.WorkTypes[WorkTypeIndex.Haul];
            Assert.That(hauling.rateSkill, Is.EqualTo(-1),
                "hauling is a work type and not a skill (15-skills §6, and the reference agrees)");
            Assert.That(hauling.workRateBasePerMille, Is.EqualTo(Rates.Scale));
            Assert.That(hauling.workRateSlopePerLevel, Is.EqualTo(0));

            // Every work type that names a skill must name one that exists, or the rate lookup
            // reads off the end of the skill table the first time somebody works at it.
            for (int w = 0; w < content.WorkTypes.Length; w++)
            {
                int skill = content.WorkTypes[w].rateSkill;
                Assert.That(skill, Is.InRange(-1, SkillIndex.Count - 1),
                    $"work type {w} drives its rate from a skill that does not exist");
            }
        }

        [Test]
        public void TheCurveReadsTheLevelsTheDesignTableNames()
        {
            var mining = ContentPack.Pawns().WorkTypes[WorkTypeIndex.Mining];
            Assert.That(mining.WorkRatePerMille(0), Is.EqualTo(550));
            Assert.That(mining.WorkRatePerMille(1), Is.EqualTo(655), "the modal colonist, 0.66x");
            Assert.That(mining.WorkRatePerMille(5), Is.EqualTo(1_075), "a season in, 1.08x");
            Assert.That(mining.WorkRatePerMille(10), Is.EqualTo(1_600), "1.60x");
            Assert.That(mining.WorkRatePerMille(20), Is.EqualTo(2_650), "mastery, 2.65x");

            var construction = ContentPack.Pawns().WorkTypes[WorkTypeIndex.Construction];
            Assert.That(construction.WorkRatePerMille(20), Is.EqualTo(2_200), "2.20x");

            // SK1. A novice grower breaks ground at 0.60x and a master at 2.60x, which is what
            // makes the Growing skill worth levelling at all — before this it bought nothing.
            var growing = ContentPack.Pawns().WorkTypes[WorkTypeIndex.Growing];
            Assert.That(growing.WorkRatePerMille(0), Is.EqualTo(600), "a novice grower, 0.60x");
            Assert.That(growing.WorkRatePerMille(1), Is.EqualTo(700), "the modal colonist, 0.70x");
            Assert.That(growing.WorkRatePerMille(20), Is.EqualTo(2_600), "mastery, 2.60x");
        }

        /// <summary>
        /// SK1, at the pawn seam rather than the def's: a grower's own work rate answers to her
        /// Growing level, so the curve is actually wired to the skill the field trains and not
        /// merely present in the table.
        ///
        /// <para>Before SK1 this work type had no <c>rateSkill</c>, so every grower — novice or
        /// master — sowed at exactly the tuned speed, and the whole of the Growing skill bought
        /// nothing. That is the regression this asserts against.</para>
        /// </summary>
        [Test]
        public void AGrowersRateAnswersToHerGrowingLevel()
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 1;
            scenario.beds = 1;
            ColonyWorld colony = ColonyWorld.Build(Size, 1u, scenario, barren: true);
            Pawn pawn = colony.Pawns.Pawns.All[0];

            SkillDef skill = ContentPack.Pawns().Skills[SkillIndex.Growing];

            pawn.Skills[SkillIndex.Growing] = 0;
            int novice = pawn.WorkRatePerMille(WorkTypeIndex.Growing);

            pawn.Skills[SkillIndex.Growing] = skill.MaxExperience;
            int master = pawn.WorkRatePerMille(WorkTypeIndex.Growing);

            Assert.That(pawn.SkillLevel(SkillIndex.Growing), Is.EqualTo(skill.maxLevel),
                "the master is not at the top of the ladder");
            Assert.That(master, Is.GreaterThan(novice),
                "a master grower works no faster than a novice, so the curve is not wired");

            // The ratio the table names, not one this test invents: 2,600 over 600.
            Assert.That(master, Is.EqualTo(2_600));
            Assert.That(novice, Is.EqualTo(600));
        }

        [Test]
        public void ARateBelowTheFloorClampsAndZeroCannotExist()
        {
            var def = new WorkTypeDef
            {
                workRateBasePerMille = 50,
                workRateFloorPerMille = 100,
            };
            Assert.That(def.WorkRatePerMille(0), Is.EqualTo(100),
                "a curve that reaches the floor clamps rather than stalls the job system");

            var content = ContentPack.Pawns();
            for (int type = 0; type < content.WorkTypes.Length; type++)
            for (int level = 0; level <= 20; level++)
                Assert.That(content.WorkTypes[type].WorkRatePerMille(level), Is.GreaterThan(0),
                    $"work type {type} at level {level} priced a tick of work at nothing");
        }

        // ---- the timing ----------------------------------------------------------------------

        [Test]
        public void ALevelTwentyMinerDigsTheSameRockInTheCurveRatio()
        {
            int cost = NaturalContent.TerrainAt(NaturalContent.TerrainRock).workToClear;
            int novice = TicksToDig(seed: 1u, miningLevel: 0);
            int master = TicksToDig(seed: 1u, miningLevel: 20);

            // The walk is the same in both arms and cancels; what is left is the face, at
            // 550 a tick against 2,650.
            int expected = CeilDiv(cost * Rates.Scale, 550) - CeilDiv(cost * Rates.Scale, 2_650);
            Assert.That(novice - master, Is.EqualTo(expected),
                $"a level-0 face owes {expected} more ticks than a level-20 one");
        }

        [Test]
        public void TwoMinersOfDifferentSkillEmptyOneCellTogether()
        {
            ColonyWorld colony = Board();
            Pawn novice = Adopt(colony, id: 1u);
            Pawn master = Adopt(colony, id: 2u);
            PinLevel(novice, SkillIndex.Mining, 0);
            PinLevel(master, SkillIndex.Mining, 20);

            int rock = NearestRock(colony, novice);
            Assume.That(rock, Is.GreaterThanOrEqualTo(0));
            int stanceA = MineWorkGiver.StandToMine(colony.Pawns, novice, rock);
            int stanceB = MineWorkGiver.StandToMine(colony.Pawns, master, rock);
            Assume.That(stanceA, Is.GreaterThanOrEqualTo(0), "no stance for the novice");
            Assume.That(stanceB, Is.GreaterThanOrEqualTo(0), "no stance for the master");

            colony.Designations.Designate(Size.FromIndex(rock), DesignationKind.Mine);

            // The completion tick defers the rock's removal at the world, so the drivers need
            // a world to queue against; this test reads the designation, which clears inline.
            colony.Pawns.Sync(colony.World);

            var drivers = new (Pawn pawn, JobDriver driver)[]
            {
                (novice, DriveTo(colony, novice, rock, stanceA)),
                (master, DriveTo(colony, master, rock, stanceB)),
            };

            // Each driver's first tick is the walk toil completing; after that both pay into
            // the same ledger every tick, at 550 and 2,650 a tick respectively. The cell's
            // designation is cleared the tick the two contributions sum to its cost.
            int cost = NaturalContent.TerrainAt(NaturalContent.TerrainRock).workToClear;
            int expected = 1 + CeilDiv(cost * Rates.Scale, 550 + 2_650);

            int tick = 0;
            for (; tick < 10_000 && colony.Designations.At(rock) == DesignationKind.Mine; tick++)
                foreach (var (_, driver) in drivers) driver.Tick(colony.Pawns);

            Assert.That(colony.Designations.At(rock), Is.Not.EqualTo(DesignationKind.Mine),
                "the rock face never came out");
            Assert.That(tick, Is.EqualTo(expected),
                "two colonists on one cell sum their contributions in one unit, whatever their skills");
        }

        /// <summary>Begin a mine job at a stance the pawn already stands on: the first Tick is
        /// the walk toil completing, the second is the first swing.</summary>
        static JobDriver DriveTo(ColonyWorld colony, Pawn pawn, int rock, int stance)
        {
            pawn.Cell = stance;
            var driver = new MineJobDriver();
            var job = new Job();
            job.Reset(JobIndex.Mine);
            job.DestCell = rock;
            job.TargetCell = stance;
            driver.Begin(pawn, job);
            return driver;
        }

        // ---- the seam to presentation --------------------------------------------------------

        [Test]
        public void AWorkingPawnPublishesHerWorkRateAsAnAspect()
        {
            ColonyWorld colony = Board();
            var pawn = new Fixed(new PawnId(1), Size.Index(colony.Start), ContentPack.Pawns());
            colony.Pawns.Pawns.Adopt(pawn);

            // Standing: published at the standard rate, because a figure that is not working
            // runs no stroke clock at all.
            colony.World.Tick();
            Assert.That(colony.World.Views.Current.TryGetPawnAspect(
                pawn.Id, RateAspects.Work, out int idle), Is.True);
            Assert.That(idle, Is.EqualTo(Rates.Scale));

            // Working: the rate of the work the driver is swinging at. The driver is parked
            // straight into its work toil on a really designated rock, so the guards it asks
            // every tick pass and it survives the publish.
            int rock = NearestRock(colony, pawn);
            Assume.That(rock, Is.GreaterThanOrEqualTo(0));
            int stance = MineWorkGiver.StandToMine(colony.Pawns, pawn, rock);
            Assume.That(stance, Is.GreaterThanOrEqualTo(0));
            pawn.Cell = stance;
            colony.Designations.Designate(Size.FromIndex(rock), DesignationKind.Mine);

            var driver = new MineJobDriver();
            var job = new Job();
            job.Reset(JobIndex.Mine);
            job.DestCell = rock;
            job.TargetCell = stance;
            driver.Begin(pawn, job);
            driver.ToilIndex = 1;
            pawn.CurrentJob = job;
            pawn.Driver = driver;

            colony.World.Tick();
            Assert.That(colony.World.Views.Current.TryGetPawnAspect(
                pawn.Id, RateAspects.Work, out int working), Is.True);
            Assert.That(working, Is.EqualTo(777));
        }
    }
}
