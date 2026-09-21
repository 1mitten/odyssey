#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The unattended run, in a container (OQ-11).
    ///
    /// <para>The Unity batch runner already does a day, but only on the Windows machine and only
    /// when somebody runs it. This is the same day in the fast tier, so the faults that need
    /// hours rather than seconds — a reservation leak, a need that never recovers, a colony that
    /// quietly stops working — are caught by a test rather than by a milestone.</para>
    ///
    /// <para><b>What makes these assertions worth the runtime.</b> "It did not throw" is nearly
    /// worthless over 60,000 ticks: a colony where every pawn wanders in a mental break for a day
    /// throws nothing at all. So the run is sampled, not just executed. Needs must recover, the
    /// reservation table must keep agreeing with what the pawns think they hold, and the job
    /// counters must show a haul, a meal and a sleep actually completed — the three behaviours
    /// M2 exists to prove.</para>
    /// </summary>
    public class SoakRunTests
    {
        /// <summary>The play scene's size, so the soak runs the world the game runs.</summary>
        static readonly GridSize PlaySize = new GridSize(120, 120, 16);

        const int Day = 60_000;
        const int SampleEvery = 100;
        const int ReservationCheckEvery = 1_000;

        /// <summary>
        /// A need at zero is survivable; a need at zero for hours is a colonist who cannot reach
        /// what it needs. 2,000 ticks is two minutes of game time at the standard rate.
        /// </summary>
        const int MaxTicksAtZero = 2_000;

        [Test, Category("Long")]
        public void OneDay() => Soak(seed: 1u, ticks: Day, budgetSeconds: 120);

        /// <summary>
        /// These two starved on day two when they were written, and the finding was filed as a
        /// scope problem (OQ-39: 48 meals, nothing in the slice makes more). The scope problem
        /// was real but four times smaller than measured: eating despawned the whole pile of four
        /// rather than one meal from it. With that fixed and the pantry sized for the run
        /// (<see cref="ScenarioDef.mealsPerPile"/>), the ten-day gate is a gate again.
        /// </summary>
        [Test, Category("Long")]
        public void ThreeDays() => Soak(seed: 1u, ticks: 3 * Day, budgetSeconds: 0);

        /// <summary>
        /// The M3 gate run: five pawns, ten in-game days, unattended. Three seeds rather than
        /// one, because a run that survives one map proves less than the same run surviving
        /// three: the colony lands somewhere different on each, and so does the salvage. The
        /// measurements each run prints are what <c>docs/milestones/soak-runs.md</c> records.
        /// </summary>
        [Test, Category("Long")]
        [TestCase(1u)]
        [TestCase(2u)]
        [TestCase(3u)]
        public void TenDays(uint seed) => Soak(seed, ticks: 10 * Day, budgetSeconds: 0);

        /// <summary>
        /// The ten-day gate again, with a field painted beside the start: OQ-39's pantry gets
        /// its first producer, and this is where "the crop feeds the colony" stops being a
        /// two-colonist, four-day test and becomes ten days of sowing, hauling and eating
        /// alongside everything else a colony does.
        ///
        /// <para>The field is eight by eight off the start cell — eleven-odd cells a head, the
        /// fat side of the ~7 the arithmetic in <c>22-growing.md</c> argues for. What the run
        /// asserts is the loop, not a ledger: something was sown, something ripened and was
        /// cut, and the zone still holds planted cells at the end, because a field that ends
        /// fallow has stopped feeding anybody.</para>
        /// </summary>
        [Test, Category("Long")]
        public void TenDaysOnAField()
            => Soak(seed: 1u, ticks: 10 * Day, budgetSeconds: 0,
                    seedWorld: PaintField, afterRun: TheFieldFedTheColony);

        // ------------------------------------------------------------------ the run

        static void Soak(uint seed, int ticks, int budgetSeconds,
                         Action<ColonyWorld>? seedWorld = null,
                         Action<ColonyWorld>? afterRun = null)
        {
            // The soak's whole claim is that a colony left alone for ten days keeps working, and
            // hauling is a third of the work it checks — so the fixture asks for the nine cells of
            // storage the scenario shipped until 2026-09-20, when a default store was taken out of
            // the game (owner: "there shouldn't be a default stockpile zone"). Without it the run
            // is honest and measures a different colony: one that fells and mines and then leaves
            // everything where it fell.
            ScenarioDef scenario = ScenarioDef.Bare();
            scenario.stockpileCells = 9;

            ColonyWorld colony = ColonyWorld.Build(PlaySize, seed, scenario);
            int pawnCount = colony.Pawns.Pawns.Count;
            Assert.That(pawnCount, Is.EqualTo(5), colony.Placement.ToString());

            seedWorld?.Invoke(colony);

            // A seeded world has already spent ticks — the field run designates through the
            // intent bus, which drains on the tick boundary. The gate is on the ticks soaked,
            // not on the clock having started at zero.
            int startTick = colony.World.CurrentTick;

            var zeroStreak = new int[pawnCount * NeedIndex.Count];
            var worstStreak = new int[NeedIndex.Count];
            var chunkMs = new List<double>(ticks / SampleEvery + 1);

            var total = Stopwatch.StartNew();
            for (int done = 0; done < ticks; done += SampleEvery)
            {
                int step = Math.Min(SampleEvery, ticks - done);

                var watch = Stopwatch.StartNew();
                Assert.DoesNotThrow(() => colony.World.Tick(step),
                    $"seed {seed} threw at about tick {done}");
                watch.Stop();
                chunkMs.Add(watch.Elapsed.TotalMilliseconds / step);

                SampleNeeds(colony, step, zeroStreak, worstStreak, seed, done);

                if ((done + step) % ReservationCheckEvery == 0)
                    AssertReservationsAgree(colony, done + step, seed);
            }
            total.Stop();

            Report(colony, seed, ticks, chunkMs, worstStreak, total.Elapsed.TotalSeconds);

            Assert.That(colony.World.CurrentTick, Is.EqualTo(ticks + startTick));
            Assert.That(colony.Pawns.Pawns.Count, Is.EqualTo(pawnCount), "a colonist left the registry");
            Assert.That(colony.World.Views.Current.PawnCount, Is.EqualTo(pawnCount),
                "a colonist vanished from the published snapshot");

            // The three behaviours M2 exists to prove. Failed counts are printed rather than
            // asserted on: a job that fails and is retried is ordinary, a job def that never once
            // succeeds across a whole day is not.
            AssertCompleted(colony, JobIndex.Haul, "haul");
            AssertCompleted(colony, JobIndex.Eat, "eat");
            AssertCompleted(colony, JobIndex.Sleep, "sleep");

            AssertReservationsAgree(colony, ticks, seed);
            afterRun?.Invoke(colony);

            if (budgetSeconds > 0)
                Assert.That(total.Elapsed.TotalSeconds, Is.LessThan(budgetSeconds),
                    $"the soak took {total.Elapsed.TotalSeconds:F0} s, over its {budgetSeconds} s budget");
        }

        /// <summary>
        /// An eight-by-eight field off the start cell. The scenario's beds and stockpiles share
        /// the same ground and a cell under one is refused, so the claim is that most of the
        /// block took, not all of it — a tighter floor would be asserting the placement table
        /// rather than the field.
        /// </summary>
        static void PaintField(ColonyWorld colony)
        {
            CellRef start = colony.Start;
            for (int dx = 0; dx < 8; dx++)
            for (int dz = 0; dz < 8; dz++)
                colony.World.Intents.Submit(new Intent(
                    IntentKind.DesignateZone,
                    new CellRef(start.X + dx, start.Z + dz, start.Y), PlantHandle.Carrot + 1));
            colony.World.Tick();

            Assert.That(colony.Growing!.Cells.Count, Is.GreaterThanOrEqualTo(48),
                $"only {colony.Growing.Cells.Count} of the 64 field cells were accepted, so " +
                "the ground by the start is less sowable than this run assumes");
        }

        /// <summary>The loop, at the end of ten days: sown, cut, and still in the ground.</summary>
        static void TheFieldFedTheColony(ColonyWorld colony)
        {
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Sow), Is.GreaterThan(0),
                "not one sowing completed in the whole run, so the field fed nobody");
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Harvest), Is.GreaterThan(0),
                "not one harvest completed, so the crop never came round");
            Assert.That(colony.Growing!.Planted.Count, Is.GreaterThan(0),
                "the field ended the run with nothing in the ground — the loop has stopped");
        }

        static void SampleNeeds(ColonyWorld colony, int step, int[] zeroStreak, int[] worstStreak,
                                uint seed, int tick)
        {
            var pawns = colony.Pawns.Pawns.All;
            for (int p = 0; p < pawns.Count; p++)
            for (int n = 0; n < NeedIndex.Count; n++)
            {
                int slot = p * NeedIndex.Count + n;
                if (pawns[p].Needs[n] > 0)
                {
                    zeroStreak[slot] = 0;
                    continue;
                }

                zeroStreak[slot] += step;
                if (zeroStreak[slot] > worstStreak[n]) worstStreak[n] = zeroStreak[slot];

                Assert.That(zeroStreak[slot], Is.LessThanOrEqualTo(MaxTicksAtZero),
                    $"seed {seed}: pawn {p}'s need {n} has been at zero for {zeroStreak[slot]} ticks " +
                    $"by tick {tick}, with {MealsLeft(colony)} meals left on the map. " +
                    "Either it cannot reach what it needs, or there is none left.");
            }
        }

        /// <summary>Meals left anywhere on the map, loose or stockpiled, counting stack sizes.</summary>
        static int MealsLeft(ColonyWorld colony)
        {
            int meals = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Despawned || item.DefIndex != ItemIndex.Meal) continue;
                meals += item.Stack;
            }
            return meals;
        }

        /// <summary>Carrots left on the map, the same count the meals line is: what the field
        /// has put there minus what the colony has eaten and what a hauler has not yet put
        /// down. Zero on a run with no field, which is why it prints rather than asserts.</summary>
        static int CarrotsLeft(ColonyWorld colony)
        {
            int carrots = 0;
            var items = colony.Pawns.Items.Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Despawned || item.DefIndex != ItemIndex.Carrots) continue;
                carrots += item.Stack;
            }
            return carrots;
        }

        /// <summary>
        /// The reservation invariant: the table's live claims are exactly the claims the pawns
        /// believe they hold. A leak here is the fault a long run exists to find — it does not
        /// throw, it does not show up in a hash, it just makes one target unusable forever.
        /// </summary>
        static void AssertReservationsAgree(ColonyWorld colony, int tick, uint seed)
        {
            int held = 0;
            var pawns = colony.Pawns.Pawns.All;
            for (int i = 0; i < pawns.Count; i++) held += pawns[i].HeldReservations.Count;

            Assert.That(colony.Pawns.Reservations.ActiveClaims, Is.EqualTo(held),
                $"seed {seed}, tick {tick}: the table holds {colony.Pawns.Reservations.ActiveClaims} " +
                $"claims and the pawns think they hold {held}");
        }

        static void AssertCompleted(ColonyWorld colony, int jobDef, string label)
        {
            Assert.That(colony.Jobs.CompletedOf(jobDef), Is.GreaterThan(0),
                $"not one {label} job completed in the whole run, so the colony was not doing that at all");
        }

        static void Report(ColonyWorld colony, uint seed, int ticks, List<double> chunkMs,
                           int[] worstStreak, double seconds)
        {
            var sorted = new List<double>(chunkMs);
            sorted.Sort();
            double mean = 0;
            for (int i = 0; i < chunkMs.Count; i++) mean += chunkMs[i];
            mean /= chunkMs.Count;
            double p95 = sorted[(int)(sorted.Count * 0.95)];

            TestContext.WriteLine(
                $"soak seed {seed}, {ticks:N0} ticks on {PlaySize}, {colony.Pawns.Pawns.Count} colonists: " +
                $"{seconds:F1} s wall, {mean:F3} ms/tick mean, {p95:F3} ms/tick p95");
            TestContext.WriteLine(
                $"  jobs completed — haul {colony.Jobs.CompletedOf(JobIndex.Haul)}, " +
                $"eat {colony.Jobs.CompletedOf(JobIndex.Eat)}, " +
                $"sleep {colony.Jobs.CompletedOf(JobIndex.Sleep)}, " +
                $"wander {colony.Jobs.CompletedOf(JobIndex.Wander)}, " +
                $"wait {colony.Jobs.CompletedOf(JobIndex.Wait)}; " +
                $"sow {colony.Jobs.CompletedOf(JobIndex.Sow)}, " +
                $"harvest {colony.Jobs.CompletedOf(JobIndex.Harvest)}");
            TestContext.WriteLine(
                $"  jobs failed — haul {colony.Jobs.FailedOf(JobIndex.Haul)}, " +
                $"eat {colony.Jobs.FailedOf(JobIndex.Eat)}, " +
                $"sleep {colony.Jobs.FailedOf(JobIndex.Sleep)}; started {colony.Jobs.JobsStarted}");
            TestContext.WriteLine(
                $"  longest stretch at zero — food {worstStreak[NeedIndex.Food]}, " +
                $"rest {worstStreak[NeedIndex.Rest]}, joy {worstStreak[NeedIndex.Joy]} ticks " +
                $"(limit {MaxTicksAtZero})");
            TestContext.WriteLine(
                $"  meals left {MealsLeft(colony)} of the {colony.Placement.Meals * colony.Scenario.mealsPerPile} placed — " +
                "nothing in the slice makes more (OQ-39), so the pantry is sized for the run");
            TestContext.WriteLine(
                $"  carrots on the map {CarrotsLeft(colony)}, still in the ground " +
                $"{colony.Growing!.Planted.Count} of {colony.Growing.Cells.Count} zone cells");
            TestContext.WriteLine($"  final hash {colony.World.ComputeStateHash().Value:x16}");
        }
    }
}
