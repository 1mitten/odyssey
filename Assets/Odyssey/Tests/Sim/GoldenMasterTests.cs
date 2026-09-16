#nullable enable
using System;
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The cross-process, cross-runtime gate: three scenarios, ticked to a fixed point and hashed
    /// against <see cref="Golden"/>. The default tier carries only the cheapest scenario, so the
    /// fast tier stays fast; the two that tick ten thousand times are <c>Category("Long")</c>.
    ///
    /// A hash mismatch between this tier (CoreCLR) and the Unity gate (Mono) is a genuine finding
    /// — record it in the row's status cell and in <c>docs/lessons.md</c> rather than re-baking
    /// one tier to match the other.
    /// </summary>
    public class GoldenMasterTests
    {
        const string RegoldenVar = "ODYSSEY_REGOLDEN";

        [Test]
        public void NaturalSmallMatchesTheGolden() =>
            Check(Golden.NaturalSmall, entry => BuildNatural(new GridSize(60, 60, 16), entry.Seed));

        [Test]
        [Category("Long")]
        public void NaturalLargeMatchesTheGolden() =>
            Check(Golden.NaturalLarge, entry => BuildNatural(new GridSize(120, 120, 16), entry.Seed));

        [Test]
        [Category("Long")]
        public void RuinedCitySliceMatchesTheGolden() =>
            Check(Golden.RuinedCitySlice, entry => BuildRuinedCity(new GridSize(60, 60, 5), entry.Seed));

        static void Check(Golden.Entry entry, Func<Golden.Entry, SimWorld> build)
        {
            SimWorld world = build(entry);
            world.Tick(entry.Ticks);
            StateHash hash = world.ComputeStateHash();

            bool regolden = Environment.GetEnvironmentVariable(RegoldenVar) == "1";
            TestContext.WriteLine(
                $"{entry.Scenario}: seed {entry.Seed}, {entry.Ticks} ticks -> 0x{hash.Value:x16}");
            if (regolden) return;

            Assert.That(hash.Value, Is.EqualTo(entry.Hash),
                $"{entry.Scenario} diverged from its golden hash at seed {entry.Seed} after " +
                $"{entry.Ticks} ticks; got 0x{hash.Value:x16}, expected 0x{entry.Hash:x16}. " +
                $"Re-run with {RegoldenVar}=1 to print the replacement.");
        }

        static SimWorld BuildNatural(GridSize size, uint seed) => ColonyWorld.Build(size, seed).World;

        /// <summary>
        /// The ruined-city equivalent of <see cref="ColonyWorld.Build"/>, composed here rather
        /// than folded into it: this row touches only this file and <see cref="Golden"/>, and
        /// <c>ColonyWorld</c> is the owner's live play-scene wiring for the natural map the scene
        /// actually loads.
        /// </summary>
        static SimWorld BuildRuinedCity(GridSize size, uint seed, int colonists = 5)
        {
            var grid = new CellGrid(size);
            MapGenOutcome outcome = MapGenerator.Generate(grid, seed, MapType.RuinedCity);

            var nav = new NavGraph(grid);
            nav.Rebuild();
            var pawns = new PawnContext(grid, nav, new PathService(new PathFinder(nav)), PawnContent.Core());
            var support = new SupportSystem(grid, new SupportSolver(grid), chunks: null);

            SimWorld world = new SimWorldBuilder()
                .WithSeed(seed)
                .WithSize(size)
                .AddSystem(_ => support)
                .AddSystem(_ => new NavigationSystem(nav, support))
                .AddSystem(_ => new NeedsSystem(pawns))
                .AddSystem(_ => new JobSystem(pawns))
                .AddSystem(_ => new MovementSystem(pawns))
                .AddTickable(_ => pawns.Pawns)
                .AddSnapshotContributor(pawns.Pawns)
                .Build();

            ColonyScenario.Place(grid, pawns, outcome.StartCell, seed, colonists);
            return world;
        }
    }
}
