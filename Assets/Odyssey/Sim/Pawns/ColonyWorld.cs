#nullable enable
using System.Collections.Generic;
using System.IO;
using Odyssey.Sim.Construction;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
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

        /// <summary>The colony's growing zones, built by the composition and reachable here for tests and the debug menu.</summary>
        public Growing.GrowingZones? Growing { get; }

        /// <summary>What the colony has ordered built but has not built yet.</summary>
        public ConstructionGrid Construction { get; }
        public SimWorld World { get; }
        public MapGenOutcome Outcome { get; }
        public ColonyScenario.Result Placement { get; }

        /// <summary>
        /// What was asked for. Kept so that a world can say how it was made without anyone having
        /// to remember separately — a log line, a save header, and the second build a load needs
        /// all want the same answer, and a copy of it is a copy that can drift.
        /// </summary>
        public ColonyRequest Request { get; }

        /// <summary>
        /// The generator settings this world came from. Kept because the renderer stamps a city
        /// map's shell templates from them after the build, and because a request that was
        /// answered is worth being able to read back.
        /// </summary>
        public MapGenDef Gen { get; }

        /// <summary>
        /// Cells the scenario marked for work before the first tick — trees near the start and the
        /// nearest outcrop. Reported rather than discarded because a scenario that marked nothing
        /// looks exactly like a colony that has decided to do nothing.
        /// </summary>
        public int MarkedForWork { get; }

        /// <summary>What the colony was given at the start, for a run's report to say so.</summary>
        public ScenarioDef Scenario { get; }

        /// <summary>The events (design 23): the ledger, the air, and the door an incident fires through.</summary>
        public Events.Incidents Incidents => Pawns.Incidents!;

        /// <summary>The job pipeline, for the per-def counters a soak run asserts on.</summary>
        public JobSystem Jobs { get; }

        /// <summary>The door lifecycle system.</summary>
        public DoorSystem Doors => Pawns.Doors!;

        public CellRef Start => Outcome.StartCell;

        /// <summary>
        /// The world's state, in the order a save writes it. Order is part of the format: items
        /// before pawns, because a pawn's job refers to an item handle and a half-loaded registry
        /// would resolve it against the wrong list.
        /// </summary>
        public IReadOnlyList<ISaveable> SaveComponents { get; }

        readonly SupportSolver _solver;
        readonly NavGraph _nav;

        ColonyWorld(CellGrid grid, PawnContext pawns, DesignationGrid designations,
            ConstructionGrid construction, SimWorld world,
            MapGenOutcome outcome, ScenarioDef scenario, ColonyScenario.Result placement, SupportSolver solver,
            NavGraph nav, JobSystem jobs, MapGenDef gen, int markedForWork, ColonyRequest request)
        {
            Request = request;
            Jobs = jobs;
            Scenario = scenario;
            Gen = gen;
            MarkedForWork = markedForWork;
            Grid = grid;
            Pawns = pawns;
            Designations = designations;
            Growing = pawns.Growing;
            Construction = construction;
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
                // After the designations and before the construction, appended rather than
                // spliced between two sections a save already depends on. A file written before
                // growing existed simply has no section here, and the zones come back as they
                // were when it did not: none.
                pawns.Growing!,
                construction,
                // Taken off the construction grid rather than built here, because that is the one
                // class that appends a building to the list at run time. The guard against
                // forgetting it is not vigilance: the edifice list is in the state hash, so
                // `WorldRoundTripTests.TheRoundTripReproducesTheStateExactly` fails the moment the
                // save stops covering what the hash covers.
                construction.Edifices,
                // After the pawn registry, because it writes into pawns that registry has just
                // rebuilt (U40). A save written before this section existed simply has no entry
                // here, and every restored colonist keeps the world seed — which is what that
                // colony was.
                new PawnSeedSection(pawns.Pawns),
                // Appended, never spliced in: a section's place in this list is its place in the
                // file, and an old save read against a new list would hand the wrong bytes to the
                // wrong reader. Both are new sections, so a save from before events simply has
                // neither and loads with an empty ledger and nothing in the air (design 23 §7).
                pawns.Incidents!.Ledger,
                pawns.Incidents!.Skyfallers,
                // Appended, as every section since the first has been. Two of them, because what a
                // store accepts and where a store is are answered by two components: the table is
                // shared — one record can be two zones' — and the zones point at it by id. A save
                // from before storage has neither, and loads with no zones and an empty table.
                pawns.Storage!.Settings,
                pawns.Storage!,
                // The built stores, appended after the painted ones. A shelf points at a record in
                // the table above, so the table has to be read before this is useful — but the two
                // are only ever read together at the end of the load, not during it, so this is an
                // ordering of convenience rather than one anything depends on.
                pawns.StorageUnits!,
                // Appended (design 29 §6). Writes into pawns the registry has rebuilt, on the
                // terms of the seed section above; a save from before animals has no entry here
                // and every restored pawn is the colonist it was.
                new PawnKindSection(pawns.Pawns),
                // Which animals have decided to leave (design 30 §3). Absent from an older save,
                // which loads with nobody leaving.
                new WildlifeSection(pawns.Pawns),
                // Who is drafted, and a step an order interrupted (design 33 §2a). Absent from an
                // older save, which loads with nobody drafted.
                new CombatSection(pawns.Pawns),
                // Appended, as every section since the first has been: the room temperatures,
                // keyed by room. A save from before temperature has no section and loads with
                // every room at the outdoor curve — which is what it was, in a world where
                // nothing was ever cold (design 28 §9).
                pawns.Temperature!,
                // Appended, as every section since the first has been: the lines, their orders,
                // and the switch and hopper of every power building (design 32 §8). A save from
                // before power has no section and loads with no lines — which is what it had.
                pawns.Power!,
                // The parts delivered to building sites (design 32 §14), after the construction
                // section whose sites they name. A save from before has none, and loads with every
                // site's parts at nought — which is what every site then had.
                construction.Parts,
                // The dead, and what is left of each struck building (design 33 §5). Appended;
                // absent from an older save, which loads with no corpses and every building whole.
                pawns.Corpses,
                pawns.EdificeDamage,
                // What a ledger entry is about — the stack a bandit carried off (design 33 §17).
                // Appended, after the ledger whose load clears it; absent from an older save, which
                // loads with no entry about anything, as none then was.
                pawns.Incidents!.Ledger.DetailSection,
                // The body's ledger (design 43 §9): who is injured where, and how much blood each
                // has lost. Appended, after the combat section that restores the pool it sits over;
                // absent from an older save, which loads with nobody injured.
                new HealthSection(pawns.Pawns),
                // The sky (design 43 §3): appended, no format bump. A save from before weather has
                // no section and rolls a sky on its first pass, which is what a new world does.
                pawns.Weather!,
            };
        }

        /// <summary>
        /// The recipe this world can state about itself without a calendar: map type, scenario
        /// and colony name all come from <see cref="Request"/>, which is exactly why it is kept.
        /// Day is not — <c>GameClock</c> lives in the Hud assembly and Sim must not reference it
        /// — so it is the one field every caller here has to supply for itself.
        /// </summary>
        public SaveRecipe Recipe(int day) =>
            new SaveRecipe(Request.Map, Scenario.defName, Request.Name, day, Request.Barren, Request.Wooded);

        /// <summary>Write the whole world to a stream.</summary>
        public void Save(Stream stream, SaveRecipe? recipe = null) => WorldSave.Save(World, stream, SaveComponents, recipe);

        public byte[] Save(SaveRecipe? recipe = null)
        {
            using var buffer = new MemoryStream();
            Save(buffer, recipe);
            return buffer.ToArray();
        }

        /// <summary>Write the whole world to a path. See <see cref="WorldSave.SaveToFile"/> for
        /// why the path is the caller's to supply.</summary>
        public void SaveToFile(string path, SaveRecipe? recipe = null) =>
            WorldSave.SaveToFile(path, World, SaveComponents, recipe);

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

        /// <summary>Load state from a path. See <see cref="WorldSave.LoadFromFile"/>.</summary>
        public SaveHeader LoadFromFile(string path)
        {
            var header = WorldSave.LoadFromFile(path, World, SaveComponents);
            RebuildDerived();
            return header;
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

            // A built ladder's connector is derived, not saved — the same argument as support, one
            // level along (U43). The edifice comes back with the save; the portal it opens between
            // two layers is worked out again here, before the rebuild that turns it into an edge.
            // Worldgen's own ladders are already registered, because the board is regenerated from
            // its seed before a save is read over it.
            Construction.RebuildLadderConnectors(Pawns);
            Construction.RebuildDoors(Pawns);

            // And which cells hold furniture nothing may be put down in — derived from the same
            // edifice list, for the same reason.
            Construction.RebuildItemBlocks();

            if (Pawns.Storage != null)
            {
                // A v6 save's zones, which the items section had to read and could not apply: the
                // component that owns zones is a different section, and a lister that depended on
                // which of two sections was written first would be a bug waiting for the next
                // appended one. Drained here, where both have finished loading.
                var legacy = Pawns.Items.PendingLegacyZones;
                for (int i = 0; i < legacy.Count; i++)
                    Pawns.Storage.AdoptLegacyZone(legacy[i].Priority, legacy[i].Allow, legacy[i].Cells);
                legacy.Clear();

                // Everything came back loose; the zoned cells move to the stored lister now that
                // the zones are known. On the generation path this does nothing, because nothing
                // is lying in a zone that was not put there through the zones.
                Pawns.Storage.RebucketAll();
            }

            // And anything that came back naming a shelf which is not there. Unlike the zones this
            // needs no hand-over — an item's own record says which container holds it, so the
            // listers were already right when the items section finished — but a file whose units
            // and items disagree is a file, and a stack that is in a container nothing can open is
            // worse than one on the floor.
            Pawns.StorageUnits?.AdoptContents(Pawns);

            // The power records against what is standing: the repair for a file where the two
            // disagree, and nothing at all on every file this build wrote (design 32 §8).
            Pawns.Power?.Reconcile();

            // **The whole graph, not the dirty blocks** (design 33 §19a). `NavGraph.Rebuild` floods
            // only the blocks something marked, and a load writes the cell arrays wholesale without
            // marking any: the graph the fresh world built for the generated board survived the
            // load everywhere a door or a ladder above did not happen to dirty a block. In the
            // owner's save seven walls of a house on the first column of a block (x = 60) stayed
            // walkable, and bandits walked through them and chose sides inside them. On the
            // generation path every block is still dirty from the graph's construction, so this
            // changes nothing there.
            _nav.MarkAllDirty();
            _nav.Rebuild();

            // The sky map is derived from the grid, and a load writes the grid wholesale without
            // telling the chunk grid a thing: rebuilt whole (design 43 §6). Now, so a board-wide
            // walk is paid inside the loading rather than on the first tick that asks.
            Pawns.Sky?.MarkAllDirty();
            Pawns.Sky?.Sync();
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
        /// purely headless run, which then gets one of its own: the sky map hears edits through it
        /// (design 43 §6).</param>
        /// <param name="mapType">Natural by owner instruction, which is what the scene loads. The
        /// ruined city is still generated and still tested (ADR 0008), and is what the M2 demo
        /// needs, because it is the only map with storeys to climb between.</param>
        /// <param name="wooded">With <paramref name="barren"/>: keep the woodland, which is what
        /// the scene loads since 2026-09-16. False is the bare board the tests baseline on.</param>
        /// <summary>
        /// Which generator definition a colony is built from — <b>the one owner of that choice</b>.
        ///
        /// <para>Extracted 2026-09-21, because it had two. This method was four lines inside
        /// <see cref="Build"/>, and every per-board measurement in the test assembly reached for
        /// <c>MapGenerator.DefaultDef</c> instead and so measured the *unmodified* def while the
        /// played scene (<c>barrenMap: 1, woodedMap: 1</c>) gets <see cref="NaturalMapGenDef.MakeWooded"/>
        /// applied on top. Two owners, silently disagreeing, for two days — the shape
        /// <c>docs/bug-patterns.md</c> keeps meeting. Now there is one, and
        /// <c>Odyssey.Tests.Sim.PlayedMap</c> calls it rather than re-deriving it.</para>
        ///
        /// <para>The def is mutable and is mutated here, so a caller gets a fresh one each time
        /// and must not cache it.</para>
        ///
        /// <para><paramref name="surfaceRelief"/> overrides a natural map's relief — the layers the
        /// surface may rise or fall from the ground layer — and re-derives the ground layer from it,
        /// as <see cref="NaturalMapGenDef.For"/> would have. <b>A measurement seam</b>
        /// (<c>docs/design/38-meadow-overhaul.md</c> §7, M7): the game passes -1, the def's own.
        /// It is here rather than in the arms so that the measured board is still the one this
        /// method chooses, which is why this method exists at all.</para>
        /// </summary>
        public static MapGenDef DefFor(MapType map, GridSize size, bool barren, bool wooded,
            int surfaceRelief = -1)
        {
            MapGenDef gen = MapGenerator.DefaultDef(map, size);
            if (barren && gen is NaturalMapGenDef natural)
            {
                if (wooded) natural.MakeWooded();
                else natural.MakeBarren();
            }

            if (surfaceRelief >= 0 && gen is NaturalMapGenDef hills)
            {
                hills.surfaceRelief = surfaceRelief;
                hills.groundLayer = hills.GroundLayerFor(size);
            }

            return gen;
        }

        public static ColonyWorld Build(GridSize size, uint seed, ScenarioDef scenario, bool barren = true,
            ChunkGrid? chunks = null, MapType mapType = MapType.Natural, bool wooded = false) =>
            Build(new ColonyRequest
            {
                Size = size,
                Seed = seed,
                Scenario = scenario,
                Barren = barren,
                Chunks = chunks,
                Map = mapType,
                Wooded = wooded,
            });

        /// <summary>
        /// Build the world the play scene plays, from a <see cref="ColonyRequest"/>.
        ///
        /// <para>This is the only build. The composition root used to have its own copy of these
        /// lines and had drifted from them twice over — it registered no connectors with the
        /// navigation graph, so a stair on a city map joined no region; it never ran the full
        /// support solve; and it assembled no <see cref="SaveComponents"/>, which is why the one
        /// world a player actually ran was the one world that could not be written to a file.</para>
        /// </summary>
        public static ColonyWorld Build(ColonyRequest request)
        {
            if (request == null) throw new System.ArgumentNullException(nameof(request));

            GridSize size = request.Size;
            uint seed = request.Seed;
            // A world without a renderer still needs the chunk grid: it is how the sky map hears
            // which columns an edit touched (design 43 §6), and a headless run that never heard
            // would keep a felled tree's shade for ever and disagree with the played game.
            ChunkGrid chunks = request.Chunks ?? new ChunkGrid(size);

            MapGenDef gen = DefFor(request.Map, size, request.Barren, request.Wooded, request.SurfaceRelief);
            if (!request.Wildlife)
            {
                gen.wildlife = System.Array.Empty<Wildlife.WildlifeEntry>();
                gen.wildlifePer10000Columns = 0;
            }

            var grid = new CellGrid(size);
            MapGenOutcome outcome = MapGenerator.Generate(grid, seed, gen);

            var nav = new NavGraph(grid);

            // Before the first rebuild, not after: a connector added later needs another one to
            // appear in the region graph.
            ConnectorRegistrar.Register(nav, grid, outcome.Connectors);
            nav.Rebuild();
            var pawns = new PawnContext(grid, nav, new PathService(new PathFinder(nav)), ContentPack.Pawns())
            {
                Chunks = chunks,
            };
            var solver = new SupportSolver(grid);
            var support = new SupportSystem(grid, solver, chunks);
            var designations = new DesignationGrid(grid, outcome.Edifices);
            var jobs = new JobSystem(pawns);

            var builder = new SimWorldBuilder()
                .WithSeed(seed)
                .WithSize(size);

            // The mirror publishes first so the geometry a frame shows is the one its pawns and
            // orders were computed against. Built here because it is built from the grid that was
            // generated four lines ago and could not have existed before it.
            if (request.Mirror != null) builder.AddSnapshotContributor(request.Mirror(grid, outcome));

            SimWorld world = builder
                .AddColony(pawns, designations, support, nav, outcome.Placements,
                    out ConstructionGrid construction, jobs)
                // The level-keeper for the world's animals (design 30 §3). Inert on a world whose
                // table is empty, which is the bare board and every test built on it.
                .AddTickable(_ => new Wildlife.WildlifeSystem(pawns, jobs, gen, outcome.StartCell,
                    request.Scenario.startingFellRadius + Wildlife.WildlifeSeeder.ClearingMargin))
                .Build();

            // Before anything is placed and before the first tick, which is the only window
            // SimWorld.StartAtTick allows: CurrentTick is in the state hash and seeds the per-tick
            // random stream. A start tick of zero is inert, so nothing baked moves.
            if (request.StartTick > 0) world.StartAtTick(request.StartTick);

            // The context learns the seed here rather than on the first tick, which is where
            // `Sync` would have given it (U40). Placement runs before any tick and spawns every
            // colonist, and a pawn's own roll seed defaults to the context's — so without this
            // line every colony in the game would roll its people from seed zero, whatever board
            // it was on. Found by `StartingSkillsTests`: two different world seeds produced
            // identical colonists. Only the seed is set, not the world and the tick `Sync` also
            // carries, because `PawnContext.Defer` uses a non-null world to mean "a tick is in
            // progress" and one is not.
            pawns.Seed = seed;

            ScenarioDef scenario = request.Scenario;
            ColonyScenario.Result placement = ColonyScenario.Place(grid, pawns, outcome.StartCell, seed,
                scenario, request.Colonists);
            int marked = ColonyScenario.GiveStartingOrders(designations, outcome.StartCell, scenario);
            // The world's animals, after its people and before its first tick (design 30 §2):
            // the seeder reads the trees and the rock the generator left and the clearing the
            // scenario is about to fell, and draws from the world's own seed.
            Wildlife.WildlifeSeeder.Seed(pawns, gen, outcome.StartCell,
                scenario.startingFellRadius + Wildlife.WildlifeSeeder.ClearingMargin, seed);

            var built = new ColonyWorld(grid, pawns, designations, construction, world, outcome, scenario, placement,
                solver, nav, jobs, gen, marked, request);
            built.RebuildDerived();
            return built;
        }
    }
}
