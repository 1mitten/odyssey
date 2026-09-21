#nullable enable
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Designations;
using Odyssey.Sim.Pathing;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Pawns
{
    /// <summary>
    /// Everything the three pawn systems share, assembled once by the composition root.
    ///
    /// It exists so that the systems take one constructor argument instead of six, and so that a
    /// test builds the same object graph the game does. It is not a service locator: nothing
    /// looks anything up by name, and nothing registers itself into it.
    /// </summary>
    public sealed class PawnContext
    {
        public PawnContext(CellGrid cells, NavGraph nav, PathService paths, PawnContent content)
        {
            _openGround = cell =>
                (Growing == null || Growing.ZonePlantAt(cell) < 0)
                && (Storage == null || !Storage.IsStorage(cell)
                    || Storage.Accepts(cell, _openGroundDef));
            Cells = cells;
            Nav = nav;
            Paths = paths;
            Content = content;
            Items = new ColonyItems(content);
            Reservations = new ReservationManager();
            Pawns = new PawnRegistry(this);
        }

        public CellGrid Cells { get; }
        public NavGraph Nav { get; }
        public PathService Paths { get; }
        public PawnContent Content { get; }
        public ColonyItems Items { get; }
        public ReservationManager Reservations { get; }
        public PawnRegistry Pawns { get; }

        public GridSize Size => Cells.Size;

        /// <summary>The current tick, refreshed at the top of each pawn system.</summary>
        public int CurrentTick { get; internal set; }

        public uint Seed { get; internal set; }

        /// <summary>The standing orders, when the world has them. Null in a bare pawn fixture.</summary>
        public DesignationGrid? Designations { get; set; }

        /// <summary>
        /// The building sites, when the world has them. Null in a bare pawn fixture, exactly as
        /// <see cref="Designations"/> is, so the two work givers answer no rather than throwing in
        /// a test that never meant to build anything.
        /// </summary>
        public Construction.ConstructionGrid? Construction { get; set; }

        /// <summary>
        /// The structural solver, when the world has one. Null in a bare pawn fixture, exactly as
        /// <see cref="Designations"/> and <see cref="Construction"/> are.
        ///
        /// <para><b>It is here so that a job which edits the world can say the structure changed.</b>
        /// Raising a wall, pulling one down and mining rock out all alter what holds the boundary
        /// above them up, and all three of them said so in a comment and did nothing about it —
        /// each naming U29 and each warning that the three should be wired together rather than
        /// one of them quietly acquiring behaviour the others lack. This is the wire.</para>
        /// </summary>
        public World.SupportSolver? Support { get; set; }

        /// <summary>
        /// The door lifecycle system, when the world has one. Null in a bare pawn fixture.
        /// </summary>
        public DoorSystem? Doors { get; set; }

        /// <summary>
        /// The room enclosure solver, when the world has one. Null in a bare pawn fixture.
        /// </summary>
        public World.EnclosureGrid? Enclosure { get; set; }

        /// <summary>
        /// The thermal pass (design 28), when the world has one. Null in a bare pawn fixture,
        /// exactly as <see cref="Enclosure"/> is — a fixture that never meant to be cold reads
        /// the outdoor curve and nothing here is the wiser.
        /// </summary>
        public Temperature.TemperatureSystem? Temperature { get; set; }

        /// <summary>
        /// The structure of this cell changed, so the boundary above it has to be re-judged.
        ///
        /// <para>Both the cell and the one above it, always, because they are two different
        /// questions: the cell's own support may have changed, and the slab resting on top of it
        /// has certainly lost or gained a source. Over-marking costs a recompute of a handful of
        /// cells; under-marking is a floor that stays up because nobody asked.</para>
        ///
        /// <para>Silent when there is no solver, like every other optional seam here: a fixture
        /// that never meant to have structure does not have to acquire one to call a job driver.</para>
        /// </summary>
        public void MarkStructureChanged(int cell)
        {
            if (Support == null) return;
            if ((uint)cell >= (uint)Size.CellCount) return;

            Support.MarkDirty(cell);
            int above = cell + Size.LayerStride;
            if (above < Size.CellCount) Support.MarkDirty(above);
        }

        /// <summary>
        /// The presentation chunk grid, when a renderer is attached, so a job that edits the world
        /// can say which chunk to re-mesh. Null for a purely headless run.
        /// </summary>
        public ChunkGrid? Chunks { get; set; }

        /// <summary>
        /// The events (design 23), when the world has them. Null in a bare pawn fixture, exactly
        /// as <see cref="Designations"/> is. Set by the composition root so that
        /// <c>ColonyWorld</c> can list its save sections without a second wiring path.
        /// </summary>
        public Events.Incidents? Incidents { get; set; }

        /// <summary>
        /// The growing zones, when the world has them. Null in a bare pawn fixture, exactly as
        /// <see cref="Designations"/> is, so the sowing work giver answers no rather than throwing
        /// in a test that never meant to farm anything. Built by <see cref="ColonyComposition.AddColony"/>,
        /// which is the one place that holds both the cell grid the zones gate on and the plant
        /// table they are read against.
        /// </summary>
        public Growing.GrowingZones? Growing { get; set; }

        /// <summary>
        /// Where the colony puts things down, or null in a world that has none — a bare test
        /// fixture, or a board before the composition root has wired one. Every read here is
        /// null-guarded for that reason and not out of habit: the haul giver answers "no
        /// destination" rather than throwing, which is what a colony with no zones actually means.
        /// </summary>
        public Storage.StorageZones? Storage { get; set; }


        /// <summary>The def <see cref="OpenGroundFor"/> was last asked about.</summary>
        int _openGroundDef;

        /// <summary>The one instance of the predicate <see cref="OpenGroundFor"/> hands out.</summary>
        readonly System.Func<int, bool> _openGround;

        /// <summary>
        /// "A thing of this kind may be set down here and will not be in anybody's way": the cell
        /// is in no growing zone, and no store here refuses it.
        ///
        /// <para><b>It asks about both kinds of zone, and the def, because the callers always
        /// meant it to.</b> Until 2026-09-21 this was <c>NotZoned</c>, which knew only about
        /// growing zones while both of its call sites said in their comments that they were
        /// looking for ground "outside every zone" — so a rock lifted off a field could be set
        /// down inside a meals-only stockpile, which is the very state the haul scan now exists
        /// to undo. A store that <em>accepts</em> the thing is not excluded: that is a home, not
        /// an obstruction, and the destination scan would have chosen it first anyway.</para>
        ///
        /// <para><b>One delegate, allocated once in the constructor.</b> A lambda that captures
        /// anything is an allocation every time the line is reached — in a work-giver scan, that
        /// is per candidate per think. Measured on a 45 x 45 field: 199 bytes a tick with six
        /// colonists against 4 with none, all of it pawn-scaled. The def travels in a field
        /// rather than in a capture for that reason, which is safe because the simulation is
        /// single-threaded and the predicate is read inside the one call it is handed to — never
        /// stored, never deferred.</para>
        /// </summary>
        public System.Func<int, bool> OpenGroundFor(int defIndex)
        {
            _openGroundDef = defIndex;
            return _openGround;
        }

        /// <summary>The world being ticked, valid inside a pawn system's tick.</summary>
        public SimWorld? World { get; private set; }

        /// <summary>
        /// Queue a world edit for the structural-events phase of this tick. A job that changes
        /// the map goes through here rather than editing inline, like every other collapse,
        /// spawn or removal: a scan that is walking the world must never see it move.
        /// </summary>
        public void Defer(System.Action<SimWorld> action)
        {
            if (World == null) throw new System.InvalidOperationException("no world is being ticked");
            World.Defer(action);
        }

        internal void Sync(SimWorld world)
        {
            World = world;
            CurrentTick = world.CurrentTick;
            Seed = world.Seed;
        }

        /// <summary>Approximate travel cost, used to order candidates before anything is pathed.</summary>
        public int Distance(int a, int b) =>
            ColonyItems.Distance(a, b, Size, Content.Movement.layerChangeEstimate);

        /// <summary>
        /// Can this pawn get there at all? Two array reads and an integer comparison — never a
        /// search. A work-giver scan asks this thousands of times per tick against candidate
        /// targets, and it is the reason the district table exists.
        /// </summary>
        public bool Reachable(Pawn pawn, int cell) => Reachable(pawn, cell, pawn.Mode);

        public bool Reachable(Pawn pawn, int cell, TraverseMode mode) =>
            (uint)cell < (uint)Size.CellCount &&
            Nav.Grid.CanEnter(cell, mode) &&
            Nav.Reachable(pawn.Cell, cell, mode);
    }
}
