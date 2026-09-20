#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Odyssey.Sim;
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
    /// <see cref="TheStairsInTheDemoAreTheScenariosDoingAndNotTheMaps"/>, which runs the same day
    /// with the offsets removed and differences the two. It is the control, and it counts stair
    /// steps rather than storeys visited — since hops became cheap a colonist wanders up and down
    /// rubble all day without a stair in sight, so storeys visited no longer separates the two
    /// runs and stair use separates them better than ten to one.</para>
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
            // ordinary day because what it needs is up there.
            //
            // A liveness check and NOT the evidence. Since hops became cheap a colonist spans
            // three storeys on a one-floor map too, so a spread here proves only that nobody sat
            // still all day — which is worth asserting and is not the claim. The claim is that the
            // stairs are the scenario's doing, and it is measured by differencing this run against
            // the one-floor control: see TheStairsInTheDemoAreTheScenariosDoingAndNotTheMaps.
            foreach (var pawn in visited)
                Assert.That(Span(pawn.Value), Is.GreaterThanOrEqualTo(2),
                    $"colonist {pawn.Key} kept to storeys {string.Join(", ", pawn.Value)}, " +
                    "with its bed one floor up and its food two");

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
        /// The control for the test above, and the reason its claim is evidence.
        ///
        /// <para>The same map, the same seed, the same day, run twice — once with the colony spread
        /// over three storeys and once with everything on the floor it wakes up on. The stairs in
        /// the demo run have to be the consequence of putting the beds and the food upstairs, and
        /// not something the city generator would have given us anyway.</para>
        ///
        /// <para><b>What it measures has had to change twice, and both times for the same reason:
        /// counting layers stopped meaning anything.</b> It began as "each colonist used exactly
        /// one storey". Then hops arrived — a jump up onto the block next door, with nothing built
        /// — and on the city's rubble one colonist touched two storeys without a stair in sight,
        /// so it became a span rather than a count. Then the jump was made half as dear and hopping
        /// a one-block pile became cheaper than walking round it: every colonist now spans three
        /// storeys on one floor.</para>
        ///
        /// <para>So it counts <b>stairs</b> — connector steps, the one thing a hop can never be.
        /// Measured over a day on four seeds, control against demo: 1/9, 0/15, 0/15, 5/12. The
        /// threshold is twice the control and at least eight, which every one of those clears; a
        /// three-to-one threshold was tried first and seed 4 breaks it.</para>
        ///
        /// <para><b>The counts are small, and that is itself the finding.</b> On seed 1 the demo
        /// takes 9 connector steps against 31 hops: most of its vertical movement is now hops, not
        /// stairs, because a hop is cheaper than a stair's 290 and the city is built of one-block
        /// rubble. M2's claim that an ordinary day exercises the stair connectors is therefore
        /// weaker than it was — true, but carried by single figures. Widening the gap again means
        /// making the map want a stair, not tuning this test.</para>
        ///
        /// <para><b>The gap narrowed on 2026-09-18</b>, when <c>MoveCost.JumpUp</c> went from 135 to
        /// 240 against that same 290 — so a hop is now barely cheaper than a stair and this balance
        /// is closer than it has ever been. The counts above were measured before that and are not
        /// re-measured here, because what this test asserts is a ratio between two configurations
        /// and not either count. If it ever starts failing on the threshold, read this paragraph
        /// first: the fix is the map, or the price, and not the number in the assertion.</para>
        ///
        /// <para>It is one test running both configurations rather than two tests with a magic
        /// number each, because the claim is a comparison and nothing else. "Never touches a stair"
        /// is no longer literally true — a colonist crosses one incidentally — and a control that
        /// overstates itself is worse than one that measures the difference it really has.</para>
        /// </summary>
        [Test, Category("Long")]
        public void TheStairsInTheDemoAreTheScenariosDoingAndNotTheMaps()
        {
            int spread = StairStepsOverADay(acrossStoreys: true);
            int oneFloor = StairStepsOverADay(acrossStoreys: false);

            Assert.That(spread, Is.GreaterThanOrEqualTo(8),
                $"the demo run took only {spread} stair steps in a day, with its beds one floor up "
                + "and its food two — the storey offsets have stopped sending anybody up a stair");
            Assert.That(spread, Is.GreaterThanOrEqualTo(oneFloor * 2),
                $"a colony spread over three storeys took {spread} stair steps and one living on a "
                + $"single floor took {oneFloor}. The demo's verticality is no longer evidence that "
                + "the scenario put its beds and its food upstairs — something else on this map is "
                + "sending colonists up and down.");
        }

        /// <summary>Steps taken along a declared connector over one day. See the control above.</summary>
        static int StairStepsOverADay(bool acrossStoreys)
        {
            ColonyWorld colony = Build(seed: 1u, acrossStoreys: acrossStoreys);
            colony.World.Tick(Day);

            foreach (IWorldSystem system in colony.World.Systems.PawnSystems)
                if (system is MovementSystem movement) return movement.ConnectorSteps;

            throw new InvalidOperationException("the colony has no movement system");
        }

        /// <summary>
        /// How far apart the highest and lowest storey a colonist stood on are — 0 for a pawn that
        /// never left its floor, 1 for one that hopped onto the rubble next door and back.
        ///
        /// <para>The count of distinct storeys would say two for that hopping pawn and two for one
        /// that climbed a flight of stairs, which is why the demo run differences it instead.</para>
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
            // A colony no longer starts with a store (owner, 2026-09-20), and this file's whole
            // subject is a colony living across three storeys — one of which is the store's. The
            // fixture asks for the nine cells the scenario used to ship, so what is measured here
            // is what was always measured.
            scenario.stockpileCells = 9;
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
