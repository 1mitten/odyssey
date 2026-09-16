#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The M2 demo, headless (OQ-20): colonists live in a ruined multi-storey shell for a day.
    ///
    /// <para><b>What this proves that the soak does not.</b> <see cref="SoakRunTests"/> runs the
    /// same length on a flat meadow, where a colony can do everything it needs without ever
    /// leaving the layer it started on. This runs the ruined city, where the things a colonist
    /// needs are on the storeys the generator put them on — so the run only succeeds if the
    /// region graph, the stair connectors and the job pipeline carry a pawn <i>up and down</i>.
    /// That is the whole of M2's claim, and it is the one claim a flat map cannot test.</para>
    ///
    /// <para><b>What it does not yet prove, and why.</b> The row this came from asks for beds on
    /// one storey, food on another and a stockpile on a third, so that the run demonstrates
    /// colonists using the stairs. The colony cannot be placed that way: <c>FindStartSpots</c>
    /// spirals outward taking the nearest layer it can find at each column, so wherever the ground
    /// is walkable — which on a city map is nearly everywhere — everything lands on the start
    /// layer. Measured on this map: thirty-three spots, all on one storey, and a full day without
    /// a single layer change. Raising the finder's layer spread does not help, because the spread
    /// is a fallback for columns with nothing walkable below, not a preference.
    ///
    /// So the layer claim is <b>not asserted here</b>, and is not quietly dropped either: it is
    /// <c>OQ-47</c>, and until that lands M2's demonstration of layers rests on
    /// <c>StampedConnectorTests</c>, which proves a colonist can reach an upper storey, rather
    /// than on a day's run showing one choosing to.</para>
    /// </summary>
    public class M2DemoTests
    {
        /// <summary>
        /// Small enough to generate quickly, deep enough to have storeys. The ruined-city
        /// generator stamps shells with stairs into this, which is what the run needs.
        /// </summary>
        static readonly GridSize CitySize = new GridSize(60, 60, 5);

        const int Day = 60_000;
        const int SampleEvery = 100;

        /// <summary>As the soak: a need at zero is survivable, hours at zero is a stuck colonist.</summary>
        const int MaxTicksAtZero = 2_000;

        const int Colonists = 3;

        [Test, Category("Long")]
        public void ThreePawnsLiveInARuinedShellForADay()
        {
            ColonyWorld colony = Build(seed: 1u);

            int pawnCount = colony.Pawns.Pawns.Count;
            Assert.That(pawnCount, Is.EqualTo(Colonists), colony.Placement.ToString());

            // Where each colonist has been. A set per pawn rather than a first-and-last, because
            // a pawn that goes up and comes back has changed layer twice and would read as
            // having stayed put.
            var visited = new Dictionary<int, HashSet<int>>();
            var zeroStreak = new int[pawnCount * NeedIndex.Count];

            Record(colony, visited);

            var total = Stopwatch.StartNew();
            for (int done = 0; done < Day; done += SampleEvery)
            {
                int step = Math.Min(SampleEvery, Day - done);
                int at = done;
                Assert.DoesNotThrow(() => colony.World.Tick(step), $"the city run threw at about tick {at}");

                Record(colony, visited);
                SampleNeeds(colony, step, zeroStreak, done);
            }
            total.Stop();

            var storeys = new SortedSet<int>();
            foreach (var pawn in visited.Values) storeys.UnionWith(pawn);

            Console.WriteLine(
                $"[M2] ruined city {CitySize.SizeX}x{CitySize.SizeZ}x{CitySize.SizeY}, {pawnCount} colonists, " +
                $"{Day:N0} ticks in {total.Elapsed.TotalSeconds:F1} s. " +
                $"Storeys used: {string.Join(", ", storeys)}. " +
                $"Jobs completed — haul {colony.Jobs.CompletedOf(JobIndex.Haul)}, " +
                $"eat {colony.Jobs.CompletedOf(JobIndex.Eat)}, " +
                $"sleep {colony.Jobs.CompletedOf(JobIndex.Sleep)}. " +
                $"Placement: {colony.Placement}");

            Assert.That(colony.World.CurrentTick, Is.EqualTo(Day));
            Assert.That(colony.Pawns.Pawns.Count, Is.EqualTo(pawnCount), "a colonist left the registry");

            // The storeys each colonist used are printed above rather than asserted on. See the
            // summary: the colony cannot currently be placed across storeys, so an assertion here
            // would be a demand the scenario system cannot meet, and making it pass would mean
            // weakening it until it meant nothing. OQ-47.

            // The three behaviours, as the soak asserts them — on the city map this time, which
            // is the part that is new: the same colony on stamped shells and rubble rather than
            // on a flat meadow.
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Haul), Is.GreaterThan(0), "no haul completed");
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Eat), Is.GreaterThan(0), "nobody ate");
            Assert.That(colony.Jobs.CompletedOf(JobIndex.Sleep), Is.GreaterThan(0), "nobody slept");
        }

        /// <summary>
        /// The same day twice from the same seed is the same day, hash for hash.
        ///
        /// Separate from the run above so a determinism failure and a behaviour failure cannot be
        /// mistaken for one another: this one says nothing about whether the colony did anything
        /// worth doing, only that it did the same thing twice.
        /// </summary>
        [Test, Category("Long")]
        public void TheSameDayTwiceIsTheSameDay()
        {
            ulong first = RunForHash(seed: 1u);
            ulong second = RunForHash(seed: 1u);

            Assert.That(second, Is.EqualTo(first),
                $"the city run diverged between two identical passes: {first:X16} then {second:X16}");
        }

        static ulong RunForHash(uint seed)
        {
            ColonyWorld colony = Build(seed);
            colony.World.Tick(Day);
            return colony.World.ComputeStateHash().Value;
        }

        /// <summary>
        /// The ruined city, not the meadow, and not barren: the shells and their stairs are the
        /// point. Three colonists rather than five, as the row asks, so that one idle pawn cannot
        /// hide behind two busy ones.
        /// </summary>
        static ColonyWorld Build(uint seed)
        {
            var scenario = ScenarioDef.Bare();
            scenario.colonists = Colonists;
            return ColonyWorld.Build(CitySize, seed, scenario, barren: false, chunks: null,
                mapType: MapType.RuinedCity);
        }

        static void Record(ColonyWorld colony, Dictionary<int, HashSet<int>> visited)
        {
            var pawns = colony.World.Views.Current.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                int id = pawns[i].Id.Value;
                if (!visited.TryGetValue(id, out HashSet<int>? layers))
                {
                    layers = new HashSet<int>();
                    visited[id] = layers;
                }
                layers.Add(pawns[i].Cell.Y);
            }
        }

        /// <summary>
        /// A need may hit zero; it may not stay there. Same rule and same threshold as the soak,
        /// because a colonist stuck on the wrong storey with food two floors down is exactly the
        /// failure this run exists to catch, and it would otherwise look like a quiet day.
        /// </summary>
        static void SampleNeeds(ColonyWorld colony, int step, int[] zeroStreak, int done)
        {
            var pawns = colony.World.Views.Current.Pawns;
            for (int i = 0; i < pawns.Length; i++)
            {
                Check(i, NeedIndex.Food, pawns[i].Food, "food");
                Check(i, NeedIndex.Rest, pawns[i].Rest, "rest");
            }

            void Check(int pawn, int need, int value, string label)
            {
                int slot = pawn * NeedIndex.Count + need;
                zeroStreak[slot] = value <= 0 ? zeroStreak[slot] + step : 0;
                Assert.That(zeroStreak[slot], Is.LessThanOrEqualTo(MaxTicksAtZero),
                    $"colonist {pawn} has had no {label} for {zeroStreak[slot]} ticks by tick {done}, " +
                    "which is a colonist who cannot reach what it needs rather than one having a bad day");
            }
        }
    }
}
