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
    /// <para><b>How the layer claim is forced, and why it has to be.</b> The colony is placed
    /// across three storeys — the store on the floor it wakes up on, the beds one above, the food
    /// two above — which a scenario could not ask for until <c>OQ-47</c>. Without that, the spiral
    /// took the nearest layer at every column, and on a city map nearly every column is walkable
    /// at the start layer, so everything landed on one floor and a full day passed without a
    /// single layer change. That measurement is not history: it is
    /// <see cref="ADayOnOneFloorNeverTouchesAStair"/>, which runs the same day with the offsets
    /// removed and asserts that nobody changes layer. It is the control. If the assertion below
    /// could pass without the stairs being used, that test would fail.</para>
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

        /// <summary>
        /// The store where they stand, the beds one storey up, the food two. Measured on this map
        /// and seed: every storey the scenario names is found and filled, and a third one above
        /// that is not (the map is five layers and the start is on the second), which is why the
        /// spread is one and two rather than something wider.
        /// </summary>
        const int StockpileStorey = 0;
        const int BedStorey = 1;
        const int MealStorey = 2;

        [Test, Category("Long")]
        public void ThreePawnsLiveInARuinedShellForADay()
        {
            ColonyWorld colony = Build(seed: 1u, acrossStoreys: true);

            int pawnCount = colony.Pawns.Pawns.Count;
            Assert.That(pawnCount, Is.EqualTo(Colonists), colony.Placement.ToString());

            // The run proves nothing about storeys if the colony was never spread over them. A
            // shortfall here is a placement failure, and it must not be allowed to read later as
            // a colonist who chose to stay put.
            Assert.That(colony.Placement.Beds, Is.EqualTo(colony.Scenario.beds),
                $"the beds did not all reach storey +{BedStorey}: {colony.Placement}");
            Assert.That(colony.Placement.Meals, Is.EqualTo(colony.Scenario.mealPiles),
                $"the food did not all reach storey +{MealStorey}: {colony.Placement}");
            Assert.That(colony.Placement.StockpileCells, Is.EqualTo(colony.Scenario.stockpileCells),
                $"the store did not fit on storey +{StockpileStorey}: {colony.Placement}");

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

            // M2's whole claim: not that a colonist *can* reach another storey, which
            // StampedConnectorTests proves on a graph, but that one does it in the course of an
            // ordinary day because what it needs is up there. Every colonist, not some: a pawn
            // that stayed on one floor all day either could not get off it or had no reason to,
            // and both are the failure this run exists to catch.
            //
            // **Measured as a SPAN, not as a count of storeys**, because unaided vertical movement
            // is exactly one block: a colonist jumps up onto the block next door or drops off it,
            // and nothing deeper is possible without a stair or a ladder. On the city's rubble that
            // is enough to touch two storeys by ordinary wandering — the control below does exactly
            // that — so two storeys is no longer evidence of anything. A span of two is: it cannot
            // be reached one hop at a time without something built to climb.
            //
            // Measured on this map and seed: the demo's colonists span 3, 2 and 2; the control's
            // span 1, 1 and 1.
            foreach (var pawn in visited)
                Assert.That(Span(pawn.Value), Is.GreaterThanOrEqualTo(2),
                    $"colonist {pawn.Key} kept to storeys {string.Join(", ", pawn.Value)}, which a hop " +
                    "alone could reach, with its bed one floor up and its food two");

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

        /// <summary>
        /// The control for the test above, and the reason its layer assertion is evidence.
        ///
        /// <para>The same map, the same seed, the same day — with the colony placed the way it was
        /// placed before a scenario could name a storey, everything on the floor it wakes up on.
        /// Nobody changes layer. So the stairs in the run above are not something the city
        /// generator would have given us anyway, or something the sampling would report whatever
        /// happened: they are the consequence of putting the beds and the food upstairs.</para>
        ///
        /// <para>If this ever starts failing it is good news that still needs looking at — some
        /// other reason to change storey has appeared, and the test above stops being a clean
        /// measurement of this one.</para>
        ///
        /// <para><b>It did start failing, and this is the looking at.</b> Hops arrived
        /// (2026-09-16): a colonist jumps up one block onto the block next door, or drops off it,
        /// with nothing built. On the city's rubble that is enough to touch a second storey while
        /// going about its business on one floor, and every colonist here now does — the original
        /// assertion was that each used exactly one storey, and one used two. What a hop cannot do
        /// is carry anybody <i>two</i> storeys, because it is one block and a repeat needs a block
        /// beside it at each step. So the control is now about the span rather than the count, and
        /// it is a sharper control than it was: the demo above no longer counts a second storey as
        /// evidence, only a spread that nothing unaided could produce.</para>
        /// </summary>
        [Test, Category("Long")]
        public void ADayOnOneFloorNeverTouchesAStair()
        {
            ColonyWorld colony = Build(seed: 1u, acrossStoreys: false);
            var visited = new Dictionary<int, HashSet<int>>();

            Record(colony, visited);
            for (int done = 0; done < Day; done += SampleEvery)
            {
                colony.World.Tick(Math.Min(SampleEvery, Day - done));
                Record(colony, visited);
            }

            foreach (var pawn in visited)
                Assert.That(Span(pawn.Value), Is.LessThanOrEqualTo(1),
                    $"colonist {pawn.Key} spanned storeys {string.Join(", ", pawn.Value)} with everything " +
                    "on one floor, so the spread in the demo run is no longer evidence that the " +
                    "scenario put it there");
        }

        /// <summary>
        /// How far apart the highest and lowest storey a colonist stood on are — 0 for a pawn that
        /// never left its floor, 1 for one that hopped onto the rubble next door and back.
        ///
        /// <para>The count of distinct storeys would say two in both of those cases and in the very
        /// different case of a pawn that climbed two flights, which is why neither test uses it.</para>
        /// </summary>
        static int Span(HashSet<int> storeys)
        {
            int low = int.MaxValue, high = int.MinValue;
            foreach (int y in storeys) { if (y < low) low = y; if (y > high) high = y; }
            return high - low;
        }

        static ulong RunForHash(uint seed)
        {
            ColonyWorld colony = Build(seed, acrossStoreys: true);
            colony.World.Tick(Day);
            return colony.World.ComputeStateHash().Value;
        }

        /// <summary>
        /// The ruined city, not the meadow, and not barren: the shells and their stairs are the
        /// point. Three colonists rather than five, as the row asks, so that one idle pawn cannot
        /// hide behind two busy ones.
        /// </summary>
        static ColonyWorld Build(uint seed, bool acrossStoreys)
        {
            var scenario = ScenarioDef.Bare();
            scenario.colonists = Colonists;
            if (acrossStoreys)
            {
                scenario.stockpileLayerOffset = StockpileStorey;
                scenario.bedLayerOffset = BedStorey;
                scenario.mealLayerOffset = MealStorey;
            }
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
