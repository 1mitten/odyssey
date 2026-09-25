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
            Corpses = new CorpseRegistry(cells.Size, content);
        }

        // ---- the fight's seams (design 33 §5) ---------------------------------------------------
        //
        // Built here rather than by the composition, like the items and the reservations, so that
        // a bare pawn fixture has them too: a test that spawns two pawns and makes one hit the
        // other should not have to assemble a colony first. The composition registers the ones that
        // are saved, hashed or published (ColonyComposition.AddColony).

        /// <summary>Who lands, who dodges, how hard (lane A). Settable so a test or a mod can swap it.</summary>
        public IMeleeRules MeleeRules { get; set; } = new MeleeRules();

        /// <summary>What a pawn swings with, and whether it may take a weapon up (lane D).</summary>
        public IWeaponRules WeaponRules { get; set; } = new WeaponRules();

        /// <summary>Damage, downed and died, heard by whoever registered (C3, C4, C5).</summary>
        public CombatHooks CombatHooks { get; } = new CombatHooks();

        /// <summary>The telling of every fight, for presentation. Not state.</summary>
        public CombatLog CombatLog { get; } = new CombatLog();

        /// <summary>The dead. Saved and hashed while there are any.</summary>
        public CorpseRegistry Corpses { get; }

        /// <summary>What is left of each struck building (C6). Saved and hashed while there is any.</summary>
        public EdificeDamage EdificeDamage { get; } = new EdificeDamage();

        /// <summary>The fight's own pass, when the world has one. Null in a bare pawn fixture.</summary>
        public CombatSystem? Combat { get; set; }

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

        /// <summary>
        /// The debug menu's <i>Jumps always fail</i> (design 46 §6): every jump over a stream falls
        /// short while it is set, because a one-in-thirty event is not something a playtest can
        /// wait for. <b>Debug only: unsaved and unhashed</b>, like the rest of the menu's switches —
        /// a run that used it is not a run anybody compares against.
        /// </summary>
        public bool DebugJumpsAlwaysFail { get; set; }

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

        /// <summary>The sky (design 43). Null in a bare pawn fixture, which is a world without weather.</summary>
        public Weather.WeatherSystem? Weather { get; set; }

        /// <summary>
        /// Where the rain stops, column by column: the one owner of "is this cell under cover"
        /// (design 43 §6). Pace, growth and an animal looking for cover all ask it. Null in a bare
        /// pawn fixture, where nothing is under cover and there is no weather to be under.
        /// </summary>
        public World.SkyColumns? Sky { get; set; }

        /// <summary>
        /// The power grid (design 32): lines, the orders for them, the buildings that make and
        /// spend power, and the nets between. Null in a bare pawn fixture, exactly as
        /// <see cref="Temperature"/> is — a fixture that never meant to wire anything has nothing
        /// to lay and nothing to refuel.
        /// </summary>
        public Power.PowerGrid? Power { get; set; }

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
        /// The chunk grid a job that edits the world tells which cell changed: the drawing re-meshes
        /// the chunk, and the sky map (<see cref="Sky"/>) recomputes the column. Every colony has
        /// one — <c>ColonyWorld.Build</c> makes its own when no renderer hands one in, because
        /// since design 43 the simulation reads it too. Null only in a bare pawn fixture.
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

        /// <summary>
        /// The colony's built stores — shelves — or null where it has none and in a bare fixture.
        /// Null-guarded at every read, exactly as <see cref="Storage"/> is, so a colony that was
        /// never given one simply has no containers rather than throwing.
        /// </summary>
        public Storage.StorageUnits? StorageUnits { get; set; }

        /// <summary>
        /// The colony's home (design 43): everything placed, grown by five cells. Null in a bare
        /// fixture that assembles no colony, where nothing is ever restricted.
        /// </summary>
        public World.HomeArea? Home { get; set; }

        /// <summary>The campfire home is centred on (design 43 §3f). Null in a bare fixture.</summary>
        public World.Hearth? Hearth { get; set; }

        /// <summary>
        /// Where a thing is, as a cell a colonist can walk to: its own cell, the cell of the store
        /// holding it, or -1 while it is in a pair of hands.
        ///
        /// <para><b>One owner, because five scans want it.</b> Every one of them used to write
        /// <c>item.Cell</c> and mean "where is it", and that was true while a thing was either on
        /// the floor or carried. With a third home the expression is wrong in a way that reads as
        /// right — a contained thing answers -1, so a distance to it is garbage and a reachability
        /// test against it is nonsense — and five copies of a wrong expression is five places to
        /// fix it.</para>
        /// </summary>
        public int WhereIs(ColonyItem item)
        {
            if (item.Cell >= 0) return item.Cell;
            if (item.ContainerId == 0) return -1;
            return StorageUnits?.CellOfContainer(item.ContainerId) ?? -1;
        }


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
        /// May this pawn take this cell as the place a job acts on or is done from? It can get
        /// there (<see cref="CanTravel(Pawn, int, TraverseMode)"/>) and it may work there
        /// (<see cref="MayWork"/>). Two array reads and an integer comparison for anybody not kept
        /// home — never a search. A work-giver scan asks this thousands of times per tick against
        /// candidate targets, and it is the reason the district table exists.
        ///
        /// <para><b>This is the question every work giver asks</b>, which is why the home gate is
        /// in it (design 43 §4b): a giver written next month is gated without knowing it. A
        /// question that is purely physical — the fight, the flight, the walk of a job already
        /// begun — asks <see cref="CanTravel(Pawn, int, TraverseMode)"/> instead, or a colonist
        /// kept home would stop defending herself at the edge of home.</para>
        /// </summary>
        public bool Reachable(Pawn pawn, int cell) => Reachable(pawn, cell, pawn.Mode);

        public bool Reachable(Pawn pawn, int cell, TraverseMode mode) =>
            CanTravel(pawn, cell, mode) && MayWork(pawn, cell);

        /// <summary>Can this pawn get there at all? Physical only: no setting is asked.</summary>
        public bool CanTravel(Pawn pawn, int cell) => CanTravel(pawn, cell, pawn.Mode);

        public bool CanTravel(Pawn pawn, int cell, TraverseMode mode) =>
            (uint)cell < (uint)Size.CellCount &&
            Nav.Grid.CanEnter(cell, mode) &&
            Nav.Reachable(pawn.Cell, cell, mode);

        /// <summary>
        /// May she work in this cell (design 43 §4b)? Yes unless she is a colonist kept home,
        /// undrafted, and the cell is outside a home that exists. <b>The only reader of
        /// <see cref="Pawn.Area"/></b> apart from the walk home; a giver that asks the setting
        /// itself is a second owner of the rule. One byte comparison for a colonist at the default.
        /// </summary>
        public bool MayWork(Pawn pawn, int cell) =>
            pawn.Area == PawnArea.Anywhere
            || pawn.Drafted
            || !pawn.IsColonist
            || Home == null
            || Home.IsEmpty
            || Home.Contains(cell);
    }
}
