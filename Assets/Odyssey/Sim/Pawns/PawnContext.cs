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
        /// The growing zones, when the world has them. Null in a bare pawn fixture, exactly as
        /// <see cref="Designations"/> is, so the sowing work giver answers no rather than throwing
        /// in a test that never meant to farm anything. Built by <see cref="ColonyComposition.AddColony"/>,
        /// which is the one place that holds both the cell grid the zones gate on and the plant
        /// table they are read against.
        /// </summary>
        public Growing.GrowingZones? Growing { get; set; }


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
