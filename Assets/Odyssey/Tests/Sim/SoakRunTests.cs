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
        /// <b>Fails today, and the reason is content rather than code.</b> The starting scenario
        /// places twelve meal piles of four — 48 meals — and nothing in the slice makes more:
        /// plants, growing and cooking are all explicitly out of scope
        /// (<c>docs/plans/vertical-slice.md</c>, "what this plan deliberately leaves out"). Five
        /// colonists eat the lot by about tick 86,000, and the first of them is at zero food with
        /// none left on the map by tick 88,500.
        ///
        /// <para>That makes U32's gate — "five pawns survive ten in-game days" — unreachable as
        /// the slice is scoped, which is a decision for the owner and not something to fix by
        /// loosening the assertion. It is OQ-39. Until then these two runs are the measurement,
        /// kept explicit so they do not fail the suite for a reason nobody has chosen yet.</para>
        /// </summary>
        [Test, Category("Long"), Explicit("Starves on day two: 48 meals, nothing makes more. See OQ-39.")]
        public void ThreeDays() => Soak(seed: 1u, ticks: 3 * Day, budgetSeconds: 0);

        /// <summary>The M3 gate run. Blocked on the same thing as <see cref="ThreeDays"/>.</summary>
        [Test, Category("Long"), Explicit("Starves on day two: 48 meals, nothing makes more. See OQ-39.")]
        public void TenDays() => Soak(seed: 1u, ticks: 10 * Day, budgetSeconds: 0);

        // ------------------------------------------------------------------ the run

        static void Soak(uint seed, int ticks, int budgetSeconds)
        {
            ColonyWorld colony = ColonyWorld.Build(PlaySize, seed);
            int pawnCount = colony.Pawns.Pawns.Count;
            Assert.That(pawnCount, Is.EqualTo(5), colony.Placement.ToString());

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

            Assert.That(colony.World.CurrentTick, Is.EqualTo(ticks));
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

            if (budgetSeconds > 0)
                Assert.That(total.Elapsed.TotalSeconds, Is.LessThan(budgetSeconds),
                    $"the soak took {total.Elapsed.TotalSeconds:F0} s, over its {budgetSeconds} s budget");
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
                $"wait {colony.Jobs.CompletedOf(JobIndex.Wait)}");
            TestContext.WriteLine(
                $"  jobs failed — haul {colony.Jobs.FailedOf(JobIndex.Haul)}, " +
                $"eat {colony.Jobs.FailedOf(JobIndex.Eat)}, " +
                $"sleep {colony.Jobs.FailedOf(JobIndex.Sleep)}; started {colony.Jobs.JobsStarted}");
            TestContext.WriteLine(
                $"  longest stretch at zero — food {worstStreak[NeedIndex.Food]}, " +
                $"rest {worstStreak[NeedIndex.Rest]}, joy {worstStreak[NeedIndex.Joy]} ticks " +
                $"(limit {MaxTicksAtZero})");
            TestContext.WriteLine(
                $"  meals left {MealsLeft(colony)} of the {colony.Placement.Meals * 4} placed — " +
                "nothing in the slice makes more, which is what stops a ten-day run (OQ-39)");
            TestContext.WriteLine($"  final hash {colony.World.ComputeStateHash().Value:x16}");
        }
    }
}
