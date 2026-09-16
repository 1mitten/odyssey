#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The standard colony world, built the one way everything agrees on: a natural map, the
    /// navigation graph, the support solver, the simulation systems in their fixed order, the
    /// designation grid, and a starting colony placed near the start cell.
    ///
    /// **Why this exists.** The same dozen lines of wiring had grown up in four places — the
    /// composition root, the screenshot harness, the measurement harness and the scenario tests —
    /// and they had already drifted once: one built the support system with a chunk grid and the
    /// others without. A headless run that proves the game is bug-free proves nothing if it is
    /// not running the game's own world, so the world is built here and only here, and the
    /// systems themselves are listed once in <see cref="ColonyComposition.AddColony"/>.
    ///
    /// Pure simulation: no UnityEngine, so it runs in the fast test tier and in any container.
    /// </summary>
    public sealed class ColonyWorld
    {
        public CellGrid Grid { get; }
        public PawnContext Pawns { get; }
        public DesignationGrid Designations { get; }
        public SimWorld World { get; }
        public MapGenOutcome Outcome { get; }
        public ColonyScenario.Result Placement { get; }

        public CellRef Start => Outcome.StartCell;

        ColonyWorld(CellGrid grid, PawnContext pawns, DesignationGrid designations, SimWorld world,
            MapGenOutcome outcome, ColonyScenario.Result placement)
        {
            Grid = grid;
            Pawns = pawns;
            Designations = designations;
            World = world;
            Outcome = outcome;
            Placement = placement;
        }

        /// <summary>
        /// Build the world the play scene plays.
        /// </summary>
        /// <param name="barren">Flat grass with no rock, ore or bare patches. False gives the full
        /// natural generator with hills, rock and ore.</param>
        /// <param name="wooded">With <paramref name="barren"/>: keep the woodland, which is what
        /// the scene loads since 2026-09-16. False is the bare board the tests baseline on.</param>
        /// <param name="chunks">The presentation chunk grid, when a renderer will be attached, so
        /// the support system and the jobs that edit the world can mark chunks dirty. Null for a
        /// purely headless run.</param>
        /// <param name="fellRadius">Cells around the start within which every tree is marked for
        /// felling before the first tick, as the scene does. Zero marks nothing.</param>
        public static ColonyWorld Build(GridSize size, uint seed, int colonists = 5, bool barren = true,
            ChunkGrid? chunks = null, bool wooded = false, int fellRadius = 0)
        {
            var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
            if (barren)
            {
                if (wooded) gen.MakeWooded();
                else gen.MakeBarren();
            }

            var grid = new CellGrid(size);
            MapGenOutcome outcome = MapGenerator.Generate(grid, seed, gen);

            var nav = new NavGraph(grid);
            nav.Rebuild();
            var pawns = new PawnContext(grid, nav, new PathService(new PathFinder(nav)), PawnContent.Core())
            {
                Chunks = chunks,
            };
            var support = new SupportSystem(grid, new SupportSolver(grid), chunks);
            var designations = new DesignationGrid(grid, outcome.Edifices);

            SimWorld world = new SimWorldBuilder()
                .WithSeed(seed)
                .WithSize(size)
                .AddColony(pawns, designations, support, nav)
                .Build();

            ColonyScenario.Result placement = ColonyScenario.Place(grid, pawns, outcome.StartCell, seed, colonists);
            if (fellRadius > 0) ColonyScenario.DesignateTreesNear(designations, outcome.StartCell, fellRadius);
            return new ColonyWorld(grid, pawns, designations, world, outcome, placement);
        }
    }
}
