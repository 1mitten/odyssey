#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Move speed (WS3, design 17 §4): what composes into it is innate — who this colonist is —
    /// then condition — how she is right now — in that fixed order, with load and health to come
    /// later in the same product. The innate factor is movement's alone and is bounded by an
    /// animation threshold, not by taste: above about 2 m/s the drawn walk blends the run clip
    /// in, so variation is capped at ±15 per cent and everything faster is reserved for a
    /// deliberate run that nothing yet has a reason to take.
    ///
    /// <para>Condition is one scalar shared with the work rate, and starvation is what moves it:
    /// not a need effect but a bar that fills while the pantry is empty, in three bands worth
    /// −100, −200 and −300. It has a floor at 700 and a ceiling at 1,000, and the ceiling is the
    /// asymmetry worth testing — being dulled slows you, being alert never speeds you up.
    /// Exhaustion is deliberately not a rate at all: at zero rest a colonist collapses and sleeps
    /// where she stands, because a body on the floor is visible in a way a hidden slowdown never
    /// is.</para>
    /// </summary>
    public class MoveRateTests
    {
        static readonly GridSize Size = new GridSize(60, 60, 16);

        /// <summary>A colonist whose skills never move while she works.</summary>
        class Pinned : Pawn
        {
            public Pinned(PawnId id, int cell, PawnContent content) : base(id, cell, content) { }
            public override int LearningFactorPerMille() => 0;
            public override void DecayExperience(int skill, int amount) { }
        }

        /// <summary>A colonist with a pace of her own and a condition offset of her own, which is
        /// what both methods are virtual for.</summary>
        sealed class Paced : Pawn
        {
            readonly int _pace, _offset;

            public Paced(PawnId id, int cell, PawnContent content, int pace, int offset)
                : base(id, cell, content)
            {
                _pace = pace;
                _offset = offset;
            }

            public override int InnatePacePerMille() => _pace;
            public override int StarvationOffsetPerMille() => _offset;
        }

        /// <summary>A colonist whose skills never move and whose pace is one number.</summary>
        sealed class PinnedPaced : Pinned
        {
            readonly int _pace;

            public PinnedPaced(PawnId id, int cell, PawnContent content, int pace)
                : base(id, cell, content)
            {
                _pace = pace;
            }

            public override int InnatePacePerMille() => _pace;
        }

        /// <summary>The bare board with beds on it but nobody on it but who a test adopts.</summary>
        static ColonyWorld Board(uint seed = 1u, int beds = 3)
        {
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.colonists = 0;
            scenario.beds = beds;
            scenario.startingFellRadius = 0;
            return ColonyWorld.Build(Size, seed, scenario, barren: true, wooded: true);
        }

        static Pinned Adopt(ColonyWorld colony, uint id = 1u)
        {
            // Placement deals every colonist a roll seed from the world's; an adopted pawn
            // brings none, so the test deals the world's own, which is what the pace is keyed
            // on alongside the id.
            var pawn = new Pinned(new PawnId((int)id), Size.Index(colony.Start),
                ContentPack.Pawns())
            {
                RollSeed = colony.World.Seed,
            };
            colony.Pawns.Pawns.Adopt(pawn);
            return pawn;
        }

        /// <summary>Set a skill to the first experience of a level and freeze it there. Level 0
        /// pins to experience 1, because an exactly-zero skill is what the starting-skills roll
        /// reads as "not rolled yet" (see WorkRateTests.PinLevel).</summary>
        static void PinLevel(Pawn pawn, int skill, int level) =>
            pawn.Skills[skill] = level <= 0
                ? 1
                : pawn.Content.Skills[skill].ExperienceForLevel(level);

        // ---- the innate pace ---------------------------------------------------------------

        [Test]
        public void ThePaceIsRolledFromTheSeedAndTheIdAndNothingElse()
        {
            // Two colonies from one seed deal the same people: the same colonist walks at the
            // same pace however many times the world is built. Across seeds the roll produces
            // something other than one number, or the band is dead content.
            var first = Board(seed: 7u);
            var second = Board(seed: 7u);
            int[] paces = new int[6];
            bool allSame = true;

            for (int i = 0; i < 6; i++)
            {
                int a = Adopt(first, id: (uint)(i + 1)).InnatePacePerMille();
                int b = Adopt(second, id: (uint)(i + 1)).InnatePacePerMille();
                Assert.That(a, Is.EqualTo(b), $"colonist {i + 1} rolled a different pace twice");
                paces[i] = a;
            }

            for (uint seed = 1u; seed <= 6u; seed++)
            {
                var colony = Board(seed);
                for (int i = 0; i < 3; i++)
                    if (Adopt(colony, id: (uint)(i + 1)).InnatePacePerMille() != paces[i])
                        allSame = false;
            }

            Assert.That(allSame, Is.False,
                "eighteen rolls across six seeds and every colonist walked at the same pace");
        }

        [Test]
        public void ThePaceIsRedrawnWhenTheSeedItCameFromChanges()
        {
            // The pace is cached on first read, because the band needs no arithmetic twice — so
            // the cache has to be thrown away when the seed under it moves. It moves twice in the
            // real game: PawnSeedSection restores it on load, *after* the pawn section built the
            // pawn, and the select screen rewrites it when a candidate is rerolled. Nothing reads
            // a pace that early today; this is what keeps the day something does from being a
            // silent one, and the project has already rolled a whole colony from seed zero once
            // by exactly this route.
            ColonyWorld colony = Board();
            var pawn = Adopt(colony);

            int first = pawn.InnatePacePerMille();
            Assert.That(first, Is.Not.Zero, "the pace never rolled");

            // Walk seeds until one rolls differently, so the assertion is about the cache and not
            // about two seeds happening to agree inside a 301-wide band.
            int changed = first;
            for (uint seed = 1u; seed <= 64u && changed == first; seed++)
            {
                pawn.RollSeed = seed;
                changed = pawn.InnatePacePerMille();
            }

            Assert.That(changed, Is.Not.EqualTo(first),
                "sixty-four seeds and the pace never moved — the cache is holding the first roll");

            // And it is a cache, not a re-roll per call: asked twice on one seed it answers once.
            Assert.That(pawn.InnatePacePerMille(), Is.EqualTo(changed));
        }

        [Test]
        public void ThePaceStaysInsideTheBandAndUnderTheJogThreshold()
        {
            // The band is not free taste: movePerTick 1 is 1.5 m/s and the drawn walk cycle
            // covers about 2 m/s, so anything past 1,333 per mille would visibly jog while
            // ostensibly walking. ±15 per cent keeps every colonist inside a walk, which is why
            // the cap sits where it does (design 17 §4b).
            var content = ContentPack.Pawns();
            var movement = content.Movement;
            Assert.That(movement.innatePaceMaxPerMille, Is.LessThan(1_333),
                "a pace past the walk cycle's range is a run, and running is held (design 17 §4f)");
            Assert.That(movement.innatePaceMinPerMille, Is.GreaterThan(0),
                "a pace of zero is a colonist who never arrives");

            for (uint seed = 1u; seed <= 8u; seed++)
            {
                var colony = Board(seed);
                for (int i = 0; i < 3; i++)
                {
                    int pace = Adopt(colony, id: (uint)(i + 1)).InnatePacePerMille();
                    Assert.That(pace, Is.InRange(movement.innatePaceMinPerMille,
                        movement.innatePaceMaxPerMille));
                }
            }
        }

        // ---- condition: one scalar, both rates ----------------------------------------------

        [Test]
        public void TheSeverityBarStepsThroughTheThreeOffsets()
        {
            // Food at zero fills the bar; the bar's thirds are the bands. The edges are
            // INVENTED — the design pins the offsets and the floor and nothing between them —
            // and the test pins whatever the pawn answers so a retune has to say so.
            var colony = Board();
            var pawn = colony.Pawns.Pawns.Spawn(Size.Index(colony.Start));

            pawn.StarvationSeverity = 0;
            Assert.That(pawn.ConditionPerMille(), Is.EqualTo(1_000), "well fed is baseline");
            pawn.StarvationSeverity = 249;
            Assert.That(pawn.ConditionPerMille(), Is.EqualTo(1_000), "under the first band");
            pawn.StarvationSeverity = 250;
            Assert.That(pawn.ConditionPerMille(), Is.EqualTo(900), "minor");
            pawn.StarvationSeverity = 500;
            Assert.That(pawn.ConditionPerMille(), Is.EqualTo(800), "moderate");
            pawn.StarvationSeverity = 750;
            Assert.That(pawn.ConditionPerMille(), Is.EqualTo(700), "severe, exactly at the floor");
            pawn.StarvationSeverity = 1_000;
            Assert.That(pawn.ConditionPerMille(), Is.EqualTo(700),
                "the floor, not below — we have no downed state to fall through yet");
        }

        [Test]
        public void TheBarFillsAtTheCadenceItsCommentClaims()
        {
            // The bands were pinned by setting the bar by hand, so nothing held the one number
            // that decides how long starvation takes to bite — and the comment beside it was
            // wrong by four times. The needs cadence is 150 ticks against a 60,000-tick day, so
            // 400 intervals a day, and starvationPerInterval is per *interval*. Both halves are
            // asserted: the arithmetic, and that the tick path really does it.
            ColonyWorld colony = Board();
            var pawn = colony.Pawns.Pawns.Spawn(Size.Index(colony.Start));
            PawnContent content = colony.Pawns.Content;

            int intervalsPerDay = content.DayTicks / content.NeedsIntervalTicks;
            Assert.That(intervalsPerDay, Is.EqualTo(400), "the cadence this number is read against");

            int perDay = intervalsPerDay * content.Kind.starvationPerInterval;
            Assert.That(perDay, Is.EqualTo(400),
                "the bar fills in two and a half days of an empty pantry, which is what the Def " +
                "comment promises — change both or neither");

            // An empty pantry, held empty: she may reach a meal, and the bar is about the food
            // need being at zero rather than about whether anything is left to eat.
            pawn.StarvationSeverity = 0;
            for (int tick = 0; tick < content.DayTicks; tick++)
            {
                pawn.Needs[NeedIndex.Food] = 0;
                colony.World.Tick();
            }

            // One update may fall either side of the window's edges, so a day is worth perDay
            // give or take one interval's worth.
            Assert.That(pawn.StarvationSeverity, Is.EqualTo(perDay).Within(
                    content.Kind.starvationPerInterval),
                "a day of an empty pantry did not move the bar by a day's worth");

            // And recovery is symmetric by the same number — the brake that makes the spiral
            // recoverable, and the reason one meal arrests the bar rather than merely stopping it.
            int starved = pawn.StarvationSeverity;
            var food = content.Needs[NeedIndex.Food];
            for (int tick = 0; tick < content.DayTicks; tick++)
            {
                pawn.Needs[NeedIndex.Food] = food.max;
                colony.World.Tick();
            }

            Assert.That(pawn.StarvationSeverity, Is.LessThanOrEqualTo(
                    System.Math.Max(0, starved - perDay + content.Kind.starvationPerInterval)),
                "a fed day drained the bar more slowly than a starved day filled it");
        }

        [Test]
        public void TheCeilingHoldsAgainstWhateverPushesUp()
        {
            // Nothing may raise condition above baseline — not this code, which cannot produce a
            // negative offset, and not the future factor the test stands in for. A "well fed"
            // bonus quietly becoming a speed boost is the failure the clamp exists for.
            var colony = Board();
            var pawn = new Paced(new PawnId(1), Size.Index(colony.Start), ContentPack.Pawns(),
                pace: 1_000, offset: -150);
            Assert.That(pawn.ConditionPerMille(), Is.EqualTo(1_000));

            var absurd = new Paced(new PawnId(2), Size.Index(colony.Start), ContentPack.Pawns(),
                pace: 1_000, offset: -5_000);
            Assert.That(absurd.ConditionPerMille(), Is.EqualTo(1_000));
        }

        [Test]
        public void ConditionReachesBothRatesFromOnePlace()
        {
            // A starving colonist is at her condition on work and on movement from one scalar:
            // neither rate ever learns that hunger exists, which is what makes M4's capacities a
            // substitution rather than a rewrite when they arrive.
            ColonyWorld colony = Board();
            var pawn = Adopt(colony);
            PinLevel(pawn, SkillIndex.Mining, 5);
            int pace = pawn.InnatePacePerMille();
            int healthyWork = pawn.WorkRatePerMille(WorkTypeIndex.Mining);

            pawn.StarvationSeverity = 750;
            int condition = pawn.ConditionPerMille();
            Assert.That(condition, Is.EqualTo(700));

            Assert.That(pawn.WorkRatePerMille(WorkTypeIndex.Mining),
                Is.EqualTo(healthyWork * condition / 1_000));
            Assert.That(pawn.MoveRatePerMille(), Is.EqualTo(pace * condition / 1_000));
        }

        [Test]
        public void MoveRateComposesInnateThenCondition()
        {
            // The order is stated once (design 17 §4a) so that adding load and health later is
            // not a re-litigation of the first two factors. 1,000 × 1,150 / 1,000 × 700 / 1,000.
            ColonyWorld colony = Board();
            var pawn = new PinnedPaced(new PawnId(1), Size.Index(colony.Start),
                ContentPack.Pawns(), 1_150)
            {
                StarvationSeverity = 750,
            };

            Assert.That(pawn.MoveRatePerMille(), Is.EqualTo(805));
        }

        // ---- the planner is not a pawn -------------------------------------------------------

        [Test]
        public void ThePlannerChargesTheCellNotThePawn()
        {
            // Terrain cost prices the ground; the rate prices the pawn. Reversed, the planner's
            // route would disagree with the mover's price — the double-count §4g forbids. Two
            // colonists at different paces asking for the same walk must get the same route.
            var flat = new Flat();
            var slow = flat.Ctx.Pawns.Adopt(new Paced(
                new PawnId(1), flat.Size.Index(8, 8, 0), ContentPack.Pawns(), 850, 0));
            var quick = flat.Ctx.Pawns.Adopt(new Paced(
                new PawnId(2), flat.Size.Index(8, 8, 0), ContentPack.Pawns(), 1_150, 0));

            int dest = flat.Size.Index(8, 3, 0);
            foreach (var pawn in new[] { slow, quick })
            {
                pawn.ClearPath();
                pawn.Destination = dest;
                flat.Ctx.Paths.Enqueue(new PathRequest(pawn.Id.Value, pawn.Cell, dest,
                    TraverseMode.Colonist));
                pawn.PathPending = true;
            }

            flat.World.Tick();
            flat.World.Tick();

            Assert.That(slow.HasPath, Is.True, "the slow walker has no route at all");
            Assert.That(quick.Path, Is.EqualTo(slow.Path),
                "the planner dealt two different routes to two different paces");
        }

        // ---- collapse ------------------------------------------------------------------------

        [Test]
        public void AColonistAtZeroRestCollapsesWhereSheStands()
        {
            // The failure mode of this feature is every colonist sleeping in the mud, so the
            // control — a merely tired colonist still walks — is the next test down. What this
            // one holds is the other half: rest gone is a body on the floor, right here, and she
            // wakes where she fell.
            ColonyWorld colony = Board();
            var pawn = Adopt(colony);
            pawn.Needs[NeedIndex.Rest] = 0;

            int tick = 0;
            for (; tick < 2_000 && !pawn.Asleep; tick++) colony.World.Tick();
            Assert.That(pawn.Asleep, Is.True, "the collapsed colonist never slept");
            Assert.That(pawn.Cell, Is.EqualTo(Size.Index(colony.Start)),
                "she walked somewhere before going down");

            for (int i = 0; i < 60_000 && pawn.Asleep; i++) colony.World.Tick();
            Assert.That(pawn.Asleep, Is.False, "she never woke");
            Assert.That(pawn.Cell, Is.EqualTo(Size.Index(colony.Start)),
                "she woke somewhere other than where she fell");
            Assert.That(pawn.Memories.Exists(m => m.ThoughtIndex == ThoughtIndex.SleptOnGround),
                Is.True, "she slept in the open and carries no thought of it");
        }

        [Test]
        public void AMerelyTiredColonistStillWalksToTheBed()
        {
            ColonyWorld colony = Board();
            var pawn = Adopt(colony);
            pawn.Needs[NeedIndex.Rest] = 100;

            int tick = 0;
            for (; tick < 6_000 && !pawn.Asleep; tick++) colony.World.Tick();
            Assert.That(pawn.Asleep, Is.True, "the tired colonist never slept");

            var beds = colony.Pawns.Items.Beds;
            Assert.That(beds, Does.Contain(pawn.Cell),
                "she slept where she stood with a bed in reach");
            Assert.That(pawn.Memories.Exists(m => m.ThoughtIndex == ThoughtIndex.SleptOnGround),
                Is.False, "a bed slept in is not the ground");
        }

        [Test]
        public void TheCollapseCatchesUpOnTheWayToTheBed()
        {
            // Rest that runs out mid-walk drops the colonist where she is, bed or no bed: the
            // rule is about the body, not about the destination. Driven at the driver, because
            // the point is the toil's own guard and not the giver's choice.
            ColonyWorld colony = Board();
            var pawn = Adopt(colony);
            var beds = colony.Pawns.Items.Beds;
            Assume.That(beds.Count, Is.GreaterThan(0), "the scenario placed no bed");
            int bed = -1;
            foreach (int cell in beds)
                if (cell != pawn.Cell)
                {
                    bed = cell;
                    break;
                }
            Assume.That(bed, Is.GreaterThanOrEqualTo(0));

            pawn.Needs[NeedIndex.Rest] = 100;
            var driver = new SleepJobDriver();
            var job = new Job();
            job.Reset(JobIndex.Sleep);
            job.TargetCell = bed;
            driver.Begin(pawn, job);

            Assert.That(driver.Tick(colony.Pawns), Is.Not.EqualTo(JobStatus.Failed),
                "the walk to the bed failed outright");
            Assert.That(pawn.Asleep, Is.False, "asleep before the guard could run");

            pawn.Needs[NeedIndex.Rest] = 0;
            Assert.That(driver.Tick(colony.Pawns), Is.EqualTo(JobStatus.Ongoing));
            Assert.That(driver.ToilIndex, Is.EqualTo(1),
                "she kept walking with no rest left to walk on");
            Assert.That(driver.Tick(colony.Pawns), Is.EqualTo(JobStatus.Ongoing));
            Assert.That(pawn.Asleep, Is.True, "down on the road and still awake");
            Assert.That(pawn.Cell, Is.Not.EqualTo(bed), "she made it to the bed after all");
            Assert.That(pawn.Memories.Exists(m => m.ThoughtIndex == ThoughtIndex.SleptOnGround),
                Is.True, "she went down in the open and remembers nothing of it");
        }

        [Test]
        public void ACollapseOnTheBedItselfIsNotACollapse()
        {
            // The control on the guard above, and the bug it was written for: the zero-rest test
            // used to run before the arrival test, so a colonist whose rest reached zero on the
            // tick she stepped onto her own bed took the collapse branch. Rest effectiveness is
            // read off the cell and the thought was not, so she got the bed's rate and the mud's
            // memory. Arrival wins — there is no walk left to cut short.
            ColonyWorld colony = Board();
            var pawn = Adopt(colony);
            var beds = colony.Pawns.Items.Beds;
            Assume.That(beds.Count, Is.GreaterThan(0), "the scenario placed no bed");
            int bed = -1;
            foreach (int cell in beds)
            {
                bed = cell;
                break;
            }

            pawn.Cell = bed;
            pawn.Needs[NeedIndex.Rest] = 0;

            var driver = new SleepJobDriver();
            var job = new Job();
            job.Reset(JobIndex.Sleep);
            job.TargetCell = bed;
            driver.Begin(pawn, job);

            Assert.That(driver.Tick(colony.Pawns), Is.EqualTo(JobStatus.Ongoing));
            Assert.That(driver.ToilIndex, Is.EqualTo(1), "she never lay down");
            Assert.That(driver.Tick(colony.Pawns), Is.EqualTo(JobStatus.Ongoing));
            Assert.That(pawn.Asleep, Is.True);
            Assert.That(pawn.Cell, Is.EqualTo(bed), "she left the bed she was standing on");
            Assert.That(pawn.Memories.Exists(m => m.ThoughtIndex == ThoughtIndex.SleptOnGround),
                Is.False, "she slept in her own bed and remembers the ground");
        }

        // ---- the seam costs nothing -------------------------------------------------------------

        /// <summary>
        /// WS1's seam turns every rate into an integer accumulator counted in thousandths (see
        /// <c>Rates</c>), and the claim then was that the counting costs the tick nothing. Proven
        /// here rather than asserted, on the same calibrated instrument
        /// <see cref="PathAllocationTests"/> uses — a per-tick delegate slipping into the work
        /// path is exactly how the tick's 64 bytes arrived once before.
        ///
        /// <para>The colony is at work, not at rest: the fixture designates the nearest dozen
        /// rocks a miner can actually reach, and mining is the one job that writes WS1's
        /// milliwork outside the pawns — <c>DesignationGrid.AddWork</c> — as well as running a
        /// stroke clock, while the walks out to the faces feed the move accumulator the seam
        /// introduced. An idle colony is already measured in PathAllocationTests; a working one
        /// is where the per-mille path actually runs.</para>
        /// </summary>
        [Test, Category("Long")]
        public void AWorkingColonyCountsItsMilliworkWithoutAllocating()
        {
            // Long enough that the dozen completed cells' one-off costs amortise. Each mined
            // cell rebuilds the navigation and support structures behind it and spawns its
            // stone — the known cost of EDITING the world, not of ticking it, about 25 KB a
            // cell and some 300 KB for the dozen this fixture finishes. Over this window the
            // working tick reads about 10 bytes, within a whisker of the idle colony
            // PathAllocationTests measures; the budget keeps out a 64-byte-per-tick delegate —
            // the way this failed once before — with better than twice to spare.
            const int Ticks = 30_000;
            const double Budget = 24.0;

            // Without this the test would pass most loudly on exactly the runtimes that cannot
            // see the allocation it exists to forbid.
            if (!PathAllocationTests.AccountingIsFineGrained())
                Assert.Ignore("this runtime reports heap growth too coarsely for a per-tick budget");

            ColonyWorld colony = ColonyWorld.Build(Size, 1u, ScenarioDef.Bare(), barren: true,
                wooded: true);

            // Warm-up: the world is up, needs falling, colonists wandering — everything but
            // work, which is measured, not warmed.
            colony.World.Tick(4_000);

            // The orders the fixture can rely on, designated through the game's own seam: the
            // nearest rocks a miner could actually get at, the stance checked with the work
            // giver's own solver — MineJobTests' fixture, borrowed, where a marked cell is dug
            // out inside twelve thousand ticks. A dozen rather than one, so nobody finishes
            // the last of them before the window closes. The scenario's own outcrop orders
            // reach only as far as the map put its rock, and a fixture that bets on that
            // measures the map, not the tick. Placed only now, after the warm-up, so every
            // stroke the window is here to measure lands inside it: WorkDone is cleared the
            // moment an order is carried out, and an order finished during the warm-up would
            // leave nothing to read.
            Pawn first = colony.Pawns.Pawns.All[0];
            var start = colony.Start;
            var candidates = new List<(int distance, int index)>();
            for (int i = 0; i < Size.CellCount; i++)
            {
                if (!colony.Designations.CanMine(i)) continue;
                if (MineWorkGiver.StandToMine(colony.Pawns, first, i) < 0) continue;
                var here = Size.FromIndex(i);
                candidates.Add((Math.Abs(here.X - start.X) + Math.Abs(here.Z - start.Z)
                    + Math.Abs(here.Y - start.Y) * 4, i));
            }
            candidates.Sort((a, b) => a.distance != b.distance
                ? a.distance.CompareTo(b.distance)
                : a.index.CompareTo(b.index));

            int marked = 0;
            foreach (var (_, index) in candidates)
            {
                if (marked >= 12) break;
                if (colony.Designations.Designate(Size.FromIndex(index), DesignationKind.Mine)
                    == IntentRejection.None)
                    marked++;
            }
            Assert.That(marked, Is.GreaterThan(0),
                "the fixture is wrong: the board has no reachable rock, so nothing can be worked");

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long before = GC.GetTotalMemory(false);
            int gen0 = GC.CollectionCount(0);
            int finishedBefore = colony.Jobs.CompletedOf(JobIndex.Mine);

            colony.World.Tick(Ticks);

            long after = GC.GetTotalMemory(false);
            Assert.That(GC.CollectionCount(0), Is.EqualTo(gen0),
                "a collection ran during the window; the figure would be a lower bound");
            double perTick = (double)(after - before) / Ticks;
            TestContext.WriteLine($"working colony: {perTick:F1} bytes per tick");

            long workDone = 0;
            var cells = colony.Designations.Cells;
            for (int i = 0; i < cells.Count; i++) workDone += colony.Designations.WorkDone(cells[i]);
            Assert.That(
                colony.Jobs.CompletedOf(JobIndex.Mine) > finishedBefore || workDone > 0,
                Is.True,
                "the fixture is wrong: no order was worked inside the window, so it measured idling");

            Assert.That(perTick, Is.LessThan(Budget),
                "a working colony's tick started allocating. Look for a per-tick delegate, " +
                "closure, boxed struct or list growth on the rate path — the stroke clocks in " +
                "JobDrivers, DesignationGrid.AddWork, the mover's accumulator.");
        }

        // ---- the seam to presentation ---------------------------------------------------------

        [Test]
        public void AColonistPublishesHerMoveRateAsAnAspect()
        {
            // The work rate publishes while she works; the move rate is a fact about her
            // wherever she stands, so it publishes for every colonist, idle or not.
            ColonyWorld colony = Board();
            var pawn = Adopt(colony);

            colony.World.Tick();
            Assert.That(colony.World.Views.Current.TryGetPawnAspect(
                pawn.Id, RateAspects.Move, out int published), Is.True);
            Assert.That(published, Is.EqualTo(pawn.MoveRatePerMille()));
        }

        // ---- the harness -----------------------------------------------------------------------

        /// <summary>
        /// A flat floor with nothing on it, and only pawns and movement in the world — the least
        /// a pawn needs to be walked across, with no need and no job to interrupt.
        /// </summary>
        sealed class Flat
        {
            public readonly SimWorld World;
            public readonly PawnContext Ctx;
            public readonly GridSize Size;

            public Flat()
            {
                Size = new GridSize(16, 16, 2);
                var cells = new CellGrid(Size);
                for (int i = 0; i < Size.CellCount; i++) cells.Floor[i] = 1;

                var nav = new NavGraph(cells);
                Ctx = new PawnContext(cells, nav, new PathService(new PathFinder(nav)),
                    ContentPack.Pawns());
                var movement = new MovementSystem(Ctx);

                World = new SimWorldBuilder()
                    .WithSeed(20260915u)
                    .WithSize(Size)
                    .AddTickable(_ => Ctx.Pawns)
                    .AddSnapshotContributor(Ctx.Pawns)
                    .AddSystem(_ => movement)
                    .Build();

                nav.MarkAllDirty();
                nav.Rebuild();
            }
        }
    }
}
