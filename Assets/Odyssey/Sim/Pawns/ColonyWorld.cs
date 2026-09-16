#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The standard colony world, built the one way everything agrees on: a natural map, the
    /// navigation graph, the support solver, the five simulation systems in their fixed order,
    /// and a starting colony placed near the start cell.
    ///
    /// **Why this exists.** The same dozen lines of wiring had grown up in four places — the
    /// composition root, the screenshot harness, the measurement harness and the scenario tests —
    /// and they had already drifted once: one built the support system with a chunk grid and the
    /// others without. A headless run that proves the game is bug-free proves nothing if it is
    /// not running the game's own world, so the world is built here and only here.
    ///
    /// Pure simulation: no UnityEngine, so it runs in the fast test tier and in any container.
    /// </summary>
    public sealed class ColonyWorld
    {
        public CellGrid Grid { get; }
        public PawnContext Pawns { get; }
        public SimWorld World { get; }
        public MapGenOutcome Outcome { get; }
        public ColonyScenario.Result Placement { get; }

        public CellRef Start => Outcome.StartCell;

        ColonyWorld(CellGrid grid, PawnContext pawns, SimWorld world, MapGenOutcome outcome,
            ColonyScenario.Result placement)
        {
            Grid = grid;
            Pawns = pawns;
            World = world;
            Outcome = outcome;
            Placement = placement;
        }

        /// <summary>
        /// Build the world the play scene plays.
        /// </summary>
        /// <param name="barren">Flat grass with nothing on it, which is what the scene loads by
        /// owner instruction. False gives the full natural generator with trees, rock and ore.</param>
        /// <param name="chunks">The presentation chunk grid, when a renderer will be attached, so
        /// the support system can mark chunks dirty. Null for a purely headless run.</param>
        public static ColonyWorld Build(GridSize size, uint seed, int colonists = 5, bool barren = true,
            ChunkGrid? chunks = null)
        {
            var gen = (NaturalMapGenDef)MapGenerator.DefaultDef(MapType.Natural, size);
            if (barren) gen.MakeBarren();

            var grid = new CellGrid(size);
            MapGenOutcome outcome = MapGenerator.Generate(grid, seed, gen);

            var nav = new NavGraph(grid);
            nav.Rebuild();
            var pawns = new PawnContext(grid, nav, new PathService(new PathFinder(nav)), PawnContent.Core());
            var support = new SupportSystem(grid, new SupportSolver(grid), chunks);

            // The order is the schedule: support before navigation because a collapse changes
            // what is walkable, and needs before jobs because a hungry pawn picks a different job.
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

            ColonyScenario.Result placement = ColonyScenario.Place(grid, pawns, outcome.StartCell, seed, colonists);
            return new ColonyWorld(grid, pawns, world, outcome, placement);
        }
    }
}
