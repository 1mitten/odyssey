#nullable enable
using System.Collections.Generic;
using System.IO;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Saving;
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

        /// <summary>
        /// The world's state, in the order a save writes it. Order is part of the format: items
        /// before pawns, because a pawn's job refers to an item handle and a half-loaded registry
        /// would resolve it against the wrong list.
        /// </summary>
        public IReadOnlyList<ISaveable> SaveComponents { get; }

        readonly SupportSolver _solver;
        readonly NavGraph _nav;

        ColonyWorld(CellGrid grid, PawnContext pawns, SimWorld world, MapGenOutcome outcome,
            ColonyScenario.Result placement, SupportSolver solver, NavGraph nav)
        {
            Grid = grid;
            Pawns = pawns;
            World = world;
            Outcome = outcome;
            Placement = placement;
            _solver = solver;
            _nav = nav;

            SaveComponents = new ISaveable[]
            {
                new GridSaveSection(grid),
                pawns.Items,
                pawns.Pawns,
            };
        }

        /// <summary>Write the whole world to a stream.</summary>
        public void Save(Stream stream) => WorldSave.Save(World, stream, SaveComponents);

        public byte[] Save()
        {
            using var buffer = new MemoryStream();
            Save(buffer);
            return buffer.ToArray();
        }

        /// <summary>
        /// Load state over a world already built from the same seed and size, then rebuild
        /// everything the file deliberately does not contain.
        /// </summary>
        public SaveHeader Load(Stream stream)
        {
            var header = WorldSave.Load(World, stream, SaveComponents);
            RebuildDerived();
            return header;
        }

        public SaveHeader Load(byte[] bytes)
        {
            using var buffer = new MemoryStream(bytes, writable: false);
            return Load(buffer);
        }

        /// <summary>
        /// Recompute what is derived rather than saved: structural support and the navigation
        /// graph. Called on both paths — after generation and after a load — so that "the derived
        /// state is now correct" has exactly one definition and cannot drift between them.
        ///
        /// <para>Support is not in the save file at all (see <see cref="GridSaveSection"/>), so
        /// without this a loaded grid would hold zero support everywhere and the first incremental
        /// solve after an edit would propagate from zeros. It is a full solve because that is the
        /// oracle the incremental path is measured against, and because a freshly generated map
        /// has already been settled by worldgen pass 10 — so on that path this recomputes the same
        /// values and brings nothing down.</para>
        /// </summary>
        public void RebuildDerived()
        {
            _solver.SolveFull();
            _nav.Rebuild();
        }

        /// <summary>
        /// Build the world the play scene plays.
        /// </summary>
        /// <param name="barren">Flat grass with nothing on it, which is what the scene loads by
        /// owner instruction. False gives the full natural generator with trees, rock and ore.</param>
        /// <param name="chunks">The presentation chunk grid, when a renderer will be attached, so
        /// the support system can mark chunks dirty. Null for a purely headless run.</param>
        /// <param name="mapType">Natural by owner instruction, which is what the scene loads. The
        /// ruined city is still generated and still tested (ADR 0008), and is what the M2 demo
        /// needs, because it is the only map with storeys to climb between.</param>
        public static ColonyWorld Build(GridSize size, uint seed, int colonists = 5, bool barren = true,
            ChunkGrid? chunks = null, MapType mapType = MapType.Natural)
        {
            MapGenDef gen = MapGenerator.DefaultDef(mapType, size);
            if (barren && gen is NaturalMapGenDef natural) natural.MakeBarren();

            var grid = new CellGrid(size);
            MapGenOutcome outcome = MapGenerator.Generate(grid, seed, gen);

            var nav = new NavGraph(grid);

            // Before the first rebuild, not after: a connector added later needs another one to
            // appear in the region graph.
            ConnectorRegistrar.Register(nav, grid, outcome.Connectors);
            nav.Rebuild();
            var pawns = new PawnContext(grid, nav, new PathService(new PathFinder(nav)), PawnContent.Core());
            var solver = new SupportSolver(grid);
            var support = new SupportSystem(grid, solver, chunks);

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
            var built = new ColonyWorld(grid, pawns, world, outcome, placement, solver, nav);
            built.RebuildDerived();
            return built;
        }
    }
}
