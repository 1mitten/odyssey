#nullable enable
using System.Collections.Generic;
using System.IO;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.Saving;
using Odyssey.Sim.World;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// The standard colony world, built the one way everything agrees on: a map, the navigation
    /// graph with its stamped connectors, the support solver, the simulation systems in their
    /// fixed order, the designation grid, and a starting colony placed near the start cell.
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

        /// <summary>What the colony was given at the start, for a run's report to say so.</summary>
        public ScenarioDef Scenario { get; }

        /// <summary>The job pipeline, for the per-def counters a soak run asserts on.</summary>
        public JobSystem Jobs { get; }

        public CellRef Start => Outcome.StartCell;

        /// <summary>
        /// The world's state, in the order a save writes it. Order is part of the format: items
        /// before pawns, because a pawn's job refers to an item handle and a half-loaded registry
        /// would resolve it against the wrong list.
        /// </summary>
        public IReadOnlyList<ISaveable> SaveComponents { get; }

        readonly SupportSolver _solver;
        readonly NavGraph _nav;

        ColonyWorld(CellGrid grid, PawnContext pawns, DesignationGrid designations, SimWorld world,
            MapGenOutcome outcome, ScenarioDef scenario, ColonyScenario.Result placement, SupportSolver solver,
            NavGraph nav, JobSystem jobs)
        {
            Jobs = jobs;
            Scenario = scenario;
            Grid = grid;
            Pawns = pawns;
            Designations = designations;
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
                jobs,
                designations,
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
        /// <param name="scenario">Who and what is placed at the start, and which orders are already
        /// given. Headless runs and tests take <see cref="ScenarioDef.Bare"/>, the scene takes
        /// <see cref="ScenarioDef.Playtest"/>.</param>
        /// <param name="barren">Flat grass with no rock, ore or bare patches. False gives the full
        /// natural generator with hills, rock and ore.</param>
        /// <param name="chunks">The presentation chunk grid, when a renderer will be attached, so
        /// the support system and the jobs that edit the world can mark chunks dirty. Null for a
        /// purely headless run.</param>
        /// <param name="mapType">Natural by owner instruction, which is what the scene loads. The
        /// ruined city is still generated and still tested (ADR 0008), and is what the M2 demo
        /// needs, because it is the only map with storeys to climb between.</param>
        /// <param name="wooded">With <paramref name="barren"/>: keep the woodland, which is what
        /// the scene loads since 2026-09-16. False is the bare board the tests baseline on.</param>
        public static ColonyWorld Build(GridSize size, uint seed, ScenarioDef scenario, bool barren = true,
            ChunkGrid? chunks = null, MapType mapType = MapType.Natural, bool wooded = false)
        {
            MapGenDef gen = MapGenerator.DefaultDef(mapType, size);
            if (barren && gen is NaturalMapGenDef natural)
            {
                if (wooded) natural.MakeWooded();
                else natural.MakeBarren();
            }

            var grid = new CellGrid(size);
            MapGenOutcome outcome = MapGenerator.Generate(grid, seed, gen);

            var nav = new NavGraph(grid);

            // Before the first rebuild, not after: a connector added later needs another one to
            // appear in the region graph.
            ConnectorRegistrar.Register(nav, grid, outcome.Connectors);
            nav.Rebuild();
            var pawns = new PawnContext(grid, nav, new PathService(new PathFinder(nav)), PawnContent.Core())
            {
                Chunks = chunks,
            };
            var solver = new SupportSolver(grid);
            var support = new SupportSystem(grid, solver, chunks);
            var designations = new DesignationGrid(grid, outcome.Edifices);
            var jobs = new JobSystem(pawns);

            SimWorld world = new SimWorldBuilder()
                .WithSeed(seed)
                .WithSize(size)
                .AddColony(pawns, designations, support, nav, jobs)
                .Build();

            ColonyScenario.Result placement = ColonyScenario.Place(grid, pawns, outcome.StartCell, seed, scenario);
            ColonyScenario.GiveStartingOrders(designations, outcome.StartCell, scenario);

            var built = new ColonyWorld(grid, pawns, designations, world, outcome, scenario, placement, solver, nav, jobs);
            built.RebuildDerived();
            return built;
        }
    }
}
