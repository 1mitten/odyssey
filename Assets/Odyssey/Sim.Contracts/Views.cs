#nullable enable
using System;

namespace Odyssey.Sim.Contracts
{
    /// <summary>
    /// A momentary thing a pawn did that presentation may want to draw, as opposed to a state it
    /// is in.
    ///
    /// <para><b>Why the contract needs this at all.</b> <see cref="PawnView.Working"/> is a
    /// sustained bit: it is true for the ten seconds a tree takes, and a figure can simply look at
    /// it every frame and pose accordingly. Picking a stack up is not like that. It takes no
    /// simulation time whatever — one toil, one tick, the item changes hands — so there is no
    /// state to observe, and the drawn lift is a presentation-side animation over an instant that
    /// has already happened. Something still has to say the instant occurred.</para>
    ///
    /// <para><b>None of these has any effect on the simulation.</b> They are a report, not state:
    /// not saved, not hashed, and nothing in <c>Sim</c> reads them back. Hashing them would make
    /// the look of the game part of the determinism contract, which is the wrong thing to promise
    /// and the wrong thing to be bound by.</para>
    /// </summary>
    public enum PawnGesture : byte
    {
        /// <summary>Nothing has happened worth drawing.</summary>
        None = 0,

        /// <summary>Stooping to take something off the ground.</summary>
        Lift = 1,

        /// <summary>Setting something down.</summary>
        Stow = 2,
    }

    /// <summary>
    /// What the presentation layer knows about one pawn. A view is a value: it holds an id, never
    /// a reference to a simulation object.
    ///
    /// That is what makes the awkward case safe. A panel open on a pawn that dies mid-tick holds
    /// only a <see cref="PawnId"/>; the next snapshot simply lacks that id, and the panel closes
    /// with a "no longer present" state instead of dereferencing freed state.
    /// </summary>
    public readonly struct PawnView
    {
        public readonly PawnId Id;
        public readonly CellRef Cell;
        public readonly int Food;
        public readonly int Rest;
        public readonly int Mood;

        /// <summary>
        /// The job this pawn is running, as an integer handle into the job table, or -1 when idle.
        ///
        /// An index rather than a label on purpose: a string per pawn per tick would allocate in
        /// the publish phase, which is meant to be allocation-free in steady state. Presentation
        /// turns the index into words, which is also where localisation belongs.
        ///
        /// This field exists because a colonist walking to a meal and a colonist wandering because
        /// they are miserable looked identical on screen, and a simulation you cannot read is a
        /// simulation you cannot trust.
        /// </summary>
        public readonly int JobDef;

        /// <summary>
        /// The cell being walked into, or the current cell when standing still.
        ///
        /// This and <see cref="MovePercent"/> exist so presentation can draw a pawn gliding
        /// between cells rather than snapping from one to the next. The simulation stays discrete
        /// and integer, which determinism requires; the smoothing is a facade over it, and is
        /// exactly how the genre's reference points do it.
        /// </summary>
        public readonly CellRef NextCell;

        /// <summary>How far from <see cref="Cell"/> to <see cref="NextCell"/>, 0 to 100.</summary>
        public readonly int MovePercent;

        /// <summary>
        /// True while the pawn is working a toil in place: swinging at a tree, and later mining
        /// or building. False while it walks, sleeps, eats or idles.
        ///
        /// Working is not the same as holding a job. A colonist spends most of a felling job on
        /// its feet, crossing the map, and a figure that swung an axe the whole way would be
        /// telling the player something untrue about where the work is happening.
        /// </summary>
        public readonly bool Working;

        /// <summary>
        /// What is being worked on, meaningful only while <see cref="Working"/>.
        ///
        /// A cell and not just a flag, because the pose needs a direction: a colonist has to face
        /// what it is swinging at, and presentation has no other way to learn which of the eight
        /// neighbours the tree is in. The pawn's own heading is zero the moment it stops walking,
        /// so by the time the work starts the last thing it could be derived from is gone.
        /// </summary>
        public readonly CellRef WorkCell;

        /// <summary>
        /// The last momentary thing this pawn did, which stays reported until it does another.
        ///
        /// <para><b>Sticky, and that is the whole design.</b> Presentation reads the latest
        /// snapshot once a frame, and the simulation runs several ticks between frames at speed
        /// three. A gesture flag set for the single tick it happened on would therefore be missed
        /// routinely — the lift would play at slow speeds, not play at fast ones, and the bug
        /// would look like a rendering glitch rather than a contract that cannot be observed.
        /// Left standing until the next gesture, no snapshot can miss it.</para>
        /// </summary>
        public readonly PawnGesture Gesture;

        /// <summary>
        /// Bumped every time a gesture begins, wrapping through 255 back to 0.
        ///
        /// <para>Stickiness alone is not enough: two lifts in a row leave
        /// <see cref="Gesture"/> reading <c>Lift</c> throughout, and presentation cannot tell one
        /// from two. The serial is what makes them distinct. Presentation fires when the serial
        /// differs from the one it last recorded for that pawn, which is correct whether it missed
        /// no snapshots, one, or forty — the test is <em>different</em>, never <em>greater</em>,
        /// so the wrap costs nothing.</para>
        ///
        /// <para>A figure that has never seen this pawn before must record the serial and pose
        /// nothing. Otherwise every colonist walking into view, and every colonist at all after a
        /// load, plays one lift it never made.</para>
        /// </summary>
        public readonly byte GestureSerial;

        public PawnView(
            PawnId id, CellRef cell, int food, int rest, int mood,
            int jobDef = -1, CellRef nextCell = default, int movePercent = 0,
            bool working = false, CellRef workCell = default,
            PawnGesture gesture = PawnGesture.None, byte gestureSerial = 0)
        {
            Gesture = gesture;
            GestureSerial = gestureSerial;
            Id = id;
            Cell = cell;
            Food = food;
            Rest = rest;
            Mood = mood;
            JobDef = jobDef;
            NextCell = nextCell;
            MovePercent = movePercent;
            Working = working;
            WorkCell = workCell;
        }
    }

    /// <summary>What the presentation layer knows about one thing.</summary>
    public readonly struct ThingView
    {
        public readonly ThingId Id;
        public readonly CellRef Cell;
        public readonly int DefIndex;
        public readonly int StuffIndex;

        /// <summary>How many are in the pile. A ledger counts these, never the piles.</summary>
        public readonly int Stack;

        public ThingView(ThingId id, CellRef cell, int defIndex, int stuffIndex, int stack = 1)
        {
            Id = id;
            Cell = cell;
            DefIndex = defIndex;
            StuffIndex = stuffIndex;
            Stack = stack;
        }
    }

    /// <summary>
    /// One standing order the player has given, wherever in the world it is.
    ///
    /// <para><b>A whole-world cell index, not an offset into a layer.</b> The two channels this
    /// replaced were one byte per cell of the active layer, which was the right shape while a
    /// click could not reach another layer. Since 2026-09-16 it can — an outcrop standing above
    /// the meadow is clickable where it is drawn — and an order the player had just given went
    /// undrawn because it was not on the published layer. The reading was "nothing happened".</para>
    ///
    /// <para>Sparse because orders are sparse: a layer is fourteen thousand cells on the prototype
    /// board and a colony has tens of orders, so this is smaller than the one layer it replaces
    /// even while covering every layer. Presentation filters to the layers it is drawing.</para>
    /// </summary>
    public readonly struct OrderView
    {
        /// <summary>The cell, as a whole-world index. <c>GridSize.FromIndex</c> unpacks it.</summary>
        public readonly int CellIndex;

        /// <summary>The standing order, as a <c>DesignationKind</c> value. Never 0.</summary>
        public readonly byte Kind;

        /// <summary>
        /// How far through the order that cell is, 0 for untouched and 255 for finished.
        ///
        /// <para>Quantised rather than exact because it is a picture, not a number: what reads on
        /// screen is whether a face is barely scratched, half cut or nearly through, and a byte
        /// says that to a tenth of a per cent. The exact tick count stays in the simulation, where
        /// the arithmetic is done.</para>
        /// </summary>
        public readonly byte Progress;

        public OrderView(int cellIndex, byte kind, byte progress)
        {
            CellIndex = cellIndex;
            Kind = kind;
            Progress = progress;
        }
    }


    /// <summary>
    /// A building site: something the player has asked for that is not there yet.
    ///
    /// <para>Its own channel rather than a sixth <c>DesignationKind</c>, because an order is a
    /// verb applied to a cell and a site is a <i>thing in waiting</i> — it has a def, a material
    /// and two independent measures of how far along it is. Sparse for the same reason
    /// <see cref="OrderView"/> is: a colony has tens of sites and a layer has thousands of
    /// cells.</para>
    ///
    /// <para><b>Two fractions, not one.</b> A site fills with material and then is worked, and a
    /// player needs to tell "nobody has brought the wood yet" from "it is half built" — they are
    /// different problems with different answers, and a single bar would merge them.</para>
    /// </summary>
    public readonly struct SiteView
    {
        /// <summary>The cell, as a whole-world index. <c>GridSize.FromIndex</c> unpacks it.</summary>
        public readonly int CellIndex;

        /// <summary>What is being built, as a <see cref="BuildingHandle"/> value. Never 0.</summary>
        public readonly byte Building;

        /// <summary>What of, as a <see cref="StuffHandle"/> value.</summary>
        public readonly byte Stuff;

        /// <summary>Material delivered, 0 for none and 255 for all of it.</summary>
        public readonly byte Delivered;

        /// <summary>Work applied, 0 for untouched and 255 for finished. Quantised, per <see cref="OrderView.Progress"/>.</summary>
        public readonly byte Progress;

        public SiteView(int cellIndex, byte building, byte stuff, byte delivered, byte progress)
        {
            CellIndex = cellIndex;
            Building = building;
            Stuff = stuff;
            Delivered = delivered;
            Progress = progress;
        }
    }
    /// <summary>
    /// One published frame of world state: everything presentation may read, and nothing else.
    ///
    /// Buffers are pooled and reused, so a snapshot is only valid until the next publish. The
    /// renderer reads it within the frame and does not retain it. Capacity grows but never
    /// shrinks, so steady-state publishing allocates nothing, which the benchmark confirmed at
    /// 0.186 ms and 69 KB for a full 62,500-cell slice.
    /// </summary>
    public sealed class WorldSnapshot
    {
        PawnView[] _pawns = Array.Empty<PawnView>();
        ThingView[] _things = Array.Empty<ThingView>();
        byte[] _sliceCells = Array.Empty<byte>();
        OrderView[] _orders = Array.Empty<OrderView>();
        SiteView[] _sites = Array.Empty<SiteView>();

        public int Tick { get; private set; }
        public int SliceLayer { get; private set; }
        public GridSize Size { get; private set; }

        /// <summary>
        /// The clock the world was running at when this frame was published: 0 paused, 1 normal,
        /// 2 fast, 3 very fast. Exactly <c>SimWorld.GameSpeed</c>, carried here rather than read
        /// off the world, because presentation reads the snapshot and nothing else.
        ///
        /// <para>It exists because <b>pause is a fact, not something to be guessed at</b>.
        /// Presentation used to infer it from the tick standing still, which cannot be done
        /// without a delay: a quarter of a second had to pass before a stopped tick could be told
        /// from a slow frame, and for those fifteen frames every colonist carried on swinging.
        /// A paused world publishes exactly one more frame — the tick spent letting the speed
        /// change through — and that frame says 0, so the lag is one frame.</para>
        ///
        /// <para>It defaults to 1 rather than 0 so that a snapshot nobody has written yet reads as
        /// a running world. A harness that builds one by hand, or an editor tool that poses a
        /// figure without ticking anything, gets movement rather than a board frozen solid.</para>
        /// </summary>
        public int GameSpeed { get; private set; } = 1;

        /// <summary>True while the simulation is advancing. See <see cref="GameSpeed"/>.</summary>
        public bool Running => GameSpeed > 0;

        /// <summary>
        /// The world's seed, carried here for the same reason <see cref="GameSpeed"/> is:
        /// presentation reads the snapshot and nothing else.
        ///
        /// <para>Nothing new enters the determinism hash by publishing it — the seed is already an
        /// input to that hash (<c>SimWorld.GetStateHash</c> adds it first). What it buys is a
        /// stable source of randomness for things that are <b>drawn and never simulated</b>, of
        /// which the colonist cast is the first: a face derived from the seed is the same face on
        /// every load of that world, and still a different cast in the next world.</para>
        ///
        /// <para>Zero on a snapshot nobody has written. Callers that derive an appearance from it
        /// must behave sensibly at zero rather than treating it as "unset", because a harness that
        /// builds a snapshot by hand is a legitimate caller.</para>
        /// </summary>
        public uint Seed { get; private set; }

        public int PawnCount { get; private set; }
        public int ThingCount { get; private set; }
        public int SliceCellCount { get; private set; }

        /// <summary>How many standing orders the colony has, anywhere in the world.</summary>
        public int OrderCount { get; private set; }

        /// <summary>How many building sites <see cref="Sites"/> holds.</summary>
        public int SiteCount { get; private set; }

        public ReadOnlySpan<PawnView> Pawns => new ReadOnlySpan<PawnView>(_pawns, 0, PawnCount);
        public ReadOnlySpan<ThingView> Things => new ReadOnlySpan<ThingView>(_things, 0, ThingCount);

        /// <summary>
        /// One byte per cell of the active layer, in index order. Presentation turns these into a
        /// chunk mesh; it is never one UI element per cell, because a layer is 62,500 cells.
        /// </summary>
        public ReadOnlySpan<byte> SliceCells => new ReadOnlySpan<byte>(_sliceCells, 0, SliceCellCount);

        /// <summary>
        /// Every standing order in the world, in cell-index order. See <see cref="OrderView"/>
        /// for why this is sparse and whole-world rather than one byte per cell of one layer.
        /// Empty when the world has no designation grid.
        /// </summary>
        public ReadOnlySpan<OrderView> Orders => new ReadOnlySpan<OrderView>(_orders, 0, OrderCount);

        /// <summary>Every building site in the world, in cell-index order. See <see cref="SiteView"/>.</summary>
        public ReadOnlySpan<SiteView> Sites => new ReadOnlySpan<SiteView>(_sites, 0, SiteCount);

        /// <summary>Find a pawn by id. Returns false when it is gone, which callers must handle.</summary>
        public bool TryGetPawn(PawnId id, out PawnView view)
        {
            for (int i = 0; i < PawnCount; i++)
            {
                if (_pawns[i].Id != id) continue;
                view = _pawns[i];
                return true;
            }
            view = default;
            return false;
        }

        // ---- writing side, used only by the simulation while building the back buffer ----

        internal void BeginWrite(int tick, GridSize size, int sliceLayer, int gameSpeed = 1,
                                 uint seed = 0u)
        {
            Tick = tick;
            Size = size;
            SliceLayer = sliceLayer;
            GameSpeed = gameSpeed;
            Seed = seed;
            PawnCount = 0;
            ThingCount = 0;
            SliceCellCount = 0;
            OrderCount = 0;
            SiteCount = 0;
        }

        internal void AddPawn(in PawnView view)
        {
            Grow(ref _pawns, PawnCount + 1);
            _pawns[PawnCount++] = view;
        }

        internal void AddThing(in ThingView view)
        {
            Grow(ref _things, ThingCount + 1);
            _things[ThingCount++] = view;
        }

        internal Span<byte> BeginSlice(int cellCount)
        {
            Grow(ref _sliceCells, cellCount);
            SliceCellCount = cellCount;
            return new Span<byte>(_sliceCells, 0, cellCount);
        }

        internal void AddOrder(in OrderView view)
        {
            Grow(ref _orders, OrderCount + 1);
            _orders[OrderCount++] = view;
        }

        internal void AddSite(in SiteView view)
        {
            Grow(ref _sites, SiteCount + 1);
            _sites[SiteCount++] = view;
        }

        static void Grow<T>(ref T[] array, int needed)
        {
            if (array.Length >= needed) return;
            int capacity = array.Length == 0 ? 64 : array.Length;
            while (capacity < needed) capacity *= 2;
            Array.Resize(ref array, capacity);
        }
    }
}
