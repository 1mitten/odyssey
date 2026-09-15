#nullable enable
using NUnit.Framework;
using Odyssey.Sim;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;

namespace Odyssey.Tests.Sim
{
    public class NavigationSystemTests
    {
        static CellGrid OpenGrid(GridSize size)
        {
            var grid = new CellGrid(size);
            // Solid floor on the bottom layer, everything above open and walkable.
            for (int z = 0; z < size.SizeZ; z++)
            for (int x = 0; x < size.SizeX; x++)
            {
                int ground = size.Index(x, z, 0);
                grid.Terrain[ground] = 1;
                grid.Flags[ground] |= CellFlags.SolidTerrain;
                for (int y = 1; y < size.SizeY; y++) grid.Floor[size.Index(x, z, y)] = y == 1 ? (ushort)1 : (ushort)0;
            }
            return grid;
        }

        [Test]
        public void NavigationRunsAfterSupportInTheSamePhase()
        {
            // A collapse changes what is walkable, so navigation must see a settled world.
            var grid = OpenGrid(new GridSize(8, 8, 3));
            var nav = new NavGraph(grid);
            var solver = new SupportSolver(grid);
            var support = new SupportSystem(grid, solver);
            var navigation = new NavigationSystem(nav, support);

            Assert.That(support.Phase, Is.EqualTo(navigation.Phase));
            Assert.That(support.Order, Is.LessThan(navigation.Order));

            var world = new SimWorldBuilder()
                .WithSize(grid.Size)
                .AddSystem(_ => navigation)
                .AddSystem(_ => support)
                .Build();

            // Registration order above is deliberately the wrong way round; the schedule must
            // still run support first.
            Assert.That(world.Systems.WorldSystems[0].Name, Is.EqualTo("Support"));
            Assert.That(world.Systems.WorldSystems[1].Name, Is.EqualTo("Navigation"));
        }

        [Test]
        public void AStillWorldCostsNothingToKeepCurrent()
        {
            var grid = OpenGrid(new GridSize(8, 8, 3));
            var nav = new NavGraph(grid);
            nav.Rebuild();

            var navigation = new NavigationSystem(nav);
            var world = new SimWorldBuilder().WithSize(grid.Size).AddSystem(_ => navigation).Build();

            world.Tick(100);
            Assert.That(navigation.RebuildCount, Is.Zero,
                "nothing changed, so no tick should have done rebuilding work");
        }

        [Test]
        public void AnEditIsPickedUpWithoutAnyoneCallingRebuildByHand()
        {
            // The gap this system closes: before it existed, nothing rebuilt the graph during a
            // tick, so pawns read a stale district table in a changing world.
            var grid = OpenGrid(new GridSize(8, 8, 3));
            var nav = new NavGraph(grid);
            nav.Rebuild();

            var navigation = new NavigationSystem(nav);
            var world = new SimWorldBuilder().WithSize(grid.Size).AddSystem(_ => navigation).Build();

            int wall = grid.Size.Index(4, 4, 1);
            grid.Flags[wall] |= CellFlags.BlockingEdifice;
            nav.MarkDirty(4, 4, 1);

            world.Tick();
            Assert.That(navigation.RebuildCount, Is.EqualTo(1));
        }
    }
}
