#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// The whole join, end to end: generate a natural map, place a colony, build the world with
    /// its systems, tick it, and check the colonists reach the published snapshot.
    ///
    /// This is the test that was missing when the play scene showed a world with no visible
    /// colonists. Every component had its own tests and passed; nothing exercised the path from
    /// "spawn a pawn" to "presentation can see a pawn".
    /// </summary>
    public class ColonyScenarioTests
    {
        sealed class Harness
        {
            public CellGrid Grid = null!;
            public PawnContext Pawns = null!;
            public SimWorld World = null!;
            public ColonyScenario.Result Placement;
            public CellRef Start;
        }

        static Harness Build(int x, int z, int y, uint seed = 1u)
        {
            var size = new GridSize(x, z, y);
            var grid = new CellGrid(size);
            var outcome = MapGenerator.Generate(grid, seed, MapType.Natural);

            var nav = new NavGraph(grid);
            nav.Rebuild();
            var pawns = new PawnContext(grid, nav, new PathService(new PathFinder(nav)), PawnContent.Core());
            var solver = new SupportSolver(grid);
            var support = new SupportSystem(grid, solver);

            var world = new SimWorldBuilder()
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

            var placement = ColonyScenario.Place(grid, pawns, outcome.StartCell, seed, ScenarioDef.Bare());
            return new Harness
            {
                Grid = grid, Pawns = pawns, World = world,
                Placement = placement, Start = outcome.StartCell,
            };
        }

        [TestCase(60, 60, 5)]
        [TestCase(60, 60, 16)]
        public void ColonistsReachThePublishedSnapshot(int x, int z, int y)
        {
            var h = Build(x, z, y);
            Assert.That(h.Placement.Colonists, Is.EqualTo(5), $"placement: {h.Placement}");

            h.World.Tick();
            var snapshot = h.World.Views.Current;

            Assert.That(snapshot.PawnCount, Is.EqualTo(5),
                "colonists exist in the simulation but presentation cannot see them");
        }

        [Test]
        public void ColonistsAreSpreadOutRatherThanStackedInOneCell()
        {
            // The failure this guards against: when the spot search comes up short, everyone is
            // placed in the same cell and the colony is invisible because it is one marker.
            var h = Build(60, 60, 5);
            h.World.Tick();
            var pawns = h.World.Views.Current.Pawns;

            var seen = new System.Collections.Generic.HashSet<CellRef>();
            for (int i = 0; i < pawns.Length; i++) seen.Add(pawns[i].Cell);
            Assert.That(seen.Count, Is.GreaterThan(1), "every colonist spawned in the same cell");
        }

        [Test]
        public void ColonistsStandOnTheDrawnLayersNearTheStart()
        {
            // Presentation draws pawns only between the lowest drawn layer and the active layer,
            // and the camera opens at the start layer. A colonist placed well above or below it
            // is simulated but never seen.
            var h = Build(60, 60, 16);
            h.World.Tick();
            var pawns = h.World.Views.Current.Pawns;

            for (int i = 0; i < pawns.Length; i++)
            {
                int delta = pawns[i].Cell.Y - h.Start.Y;
                Assert.That(delta, Is.InRange(-2, 0),
                    $"colonist {i} is {delta} layers from the start layer and would not be drawn");
            }
        }

        [Test]
        public void EveryColonistStandsSomewhereWalkable()
        {
            var h = Build(60, 60, 16);
            h.World.Tick();
            var pawns = h.World.Views.Current.Pawns;
            for (int i = 0; i < pawns.Length; i++)
                Assert.That(h.Grid.IsWalkable(h.Grid.Index(pawns[i].Cell)), Is.True,
                    $"colonist {i} is standing inside solid ground at {pawns[i].Cell}");
        }

        [Test]
        public void TheColonyGetsFoodBedsAndAStockpile()
        {
            var h = Build(60, 60, 16);
            Assert.That(h.Placement.Meals, Is.GreaterThan(0), $"placement: {h.Placement}");
            Assert.That(h.Placement.Beds, Is.GreaterThan(0), $"placement: {h.Placement}");
            Assert.That(h.Placement.StockpileCells, Is.GreaterThan(0), $"placement: {h.Placement}");
        }

        [Test]
        public void ColonistsActuallyDoSomethingWithinADay()
        {
            // A colony that spawns and then stands still forever is not obviously different from
            // a broken one on screen, so assert that jobs are being taken.
            var h = Build(60, 60, 16);
            h.World.Tick(2000);

            var pawns = h.World.Views.Current.Pawns;
            int working = 0;
            for (int i = 0; i < pawns.Length; i++) if (pawns[i].JobDef >= 0) working++;
            Assert.That(working, Is.GreaterThan(0), "no colonist has taken a job after 2000 ticks");
        }

        [Test]
        public void ThePlacementIsDeterministic()
        {
            var a = Build(60, 60, 16, seed: 7);
            var b = Build(60, 60, 16, seed: 7);
            a.World.Tick(100);
            b.World.Tick(100);
            Assert.That(a.World.ComputeStateHash().Value, Is.EqualTo(b.World.ComputeStateHash().Value));
        }
    }
}
