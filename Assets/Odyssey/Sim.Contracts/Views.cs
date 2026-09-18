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

        /// <summary>
        /// Kneeling at a plot to work seed into it (owner, 2026-09-18: the sow must not chop).
        /// The pickup's own pose and curve, re-timed: down quickly, a long hold at the soil —
        /// the hold is the work — and up slowly. Reused rather than new because a sower kneels
        /// exactly where a lifter stooops, against ground the relief has tilted, and the solved
        /// pose is already correct on all sixty-one rigs.
        /// </summary>
        Sow = 3,
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

        /// <summary>
        /// Whether this pawn is asleep — so that it can be <b>drawn</b> asleep.
        ///
        /// <para><b>A field here rather than a <see cref="PawnAspect"/>, and the reason is the one
        /// the aspect seam itself gives:</b> aspects are selection-scoped, published for the pawn
        /// the interface asked about and no other, and every colonist on screen has to be drawn
        /// in the right pose whether anybody has selected them or not. It sits beside
        /// <see cref="Working"/> because it is the same kind of fact — a pose input, which is
        /// precisely why that field is on this view too.</para>
        ///
        /// <para><b>Written because a sleeping colonist was drawn standing up.</b> Nothing in the
        /// whole of presentation knew this, so a colonist who had walked to a bed and gone to
        /// sleep in it stood bolt upright in it all night — and the owner, watching, reported that
        /// colonists would not use the beds at all and stood outside instead (2026-09-18). They
        /// were in the beds. The simulation was measured and is correct; there was no way to see
        /// it.</para>
        ///
        /// <para>The direction to lie in is not carried: the pawn is in the bed's own cell and
        /// presentation already knows which way that bed faces, so a second field would be a
        /// second copy of an answer.</para>
        /// </summary>
        public readonly bool Asleep;

        public PawnView(
            PawnId id, CellRef cell, int food, int rest, int mood,
            int jobDef = -1, CellRef nextCell = default, int movePercent = 0,
            bool working = false, CellRef workCell = default,
            PawnGesture gesture = PawnGesture.None, byte gestureSerial = 0,
            bool asleep = false)
        {
            Asleep = asleep;
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

    /// <summary>
    /// The name of one thing a feature publishes about a pawn, reduced to a number.
    ///
    /// <para><b>Why a hashed name and not an enum.</b> An enum of aspect kinds would live in this
    /// assembly, which both sides reference, so every feature that wanted to say something new
    /// about a pawn would edit it — which is the cost this mechanism exists to remove. A key
    /// minted from a symbolic name is the one form that lets a feature declare its own vocabulary
    /// without touching a file anyone else owns, and it is the same reasoning that made interface
    /// icons symbolic keys rather than filenames (ADR 0007).
    /// </para>
    ///
    /// <para><b>Sixty-four bits, deliberately.</b> FNV-1a over thirty-two bits collides somewhere
    /// around one chance in two hundred thousand across a few hundred keys, and a collision here
    /// is not a crash — it is one feature silently reading another feature's number, in a value
    /// the player is looking at. At sixty-four the odds stop being worth a paragraph, which is
    /// cheaper than the central registry that would otherwise be needed to detect the clash, and a
    /// central registry is the shared file we are trying not to have.</para>
    ///
    /// <para><b>Mint it once.</b> <see cref="Of"/> walks the string, so a feature holds the answer
    /// in a <c>static readonly</c> field rather than calling it inside the publish loop, which is
    /// meant to stay allocation-free and cheap in steady state.</para>
    ///
    /// <para>The default value is zero, which <see cref="Of"/> never returns — even the empty
    /// string hashes to the FNV offset basis — so an unset key is always distinguishable from a
    /// real one.</para>
    /// </summary>
    public readonly struct AspectKey : IEquatable<AspectKey>
    {
        const ulong OffsetBasis = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;

        public readonly ulong Value;

        public AspectKey(ulong value) => Value = value;

        /// <summary>
        /// Mint the key for a symbolic name, such as <c>"odyssey.pawn.carrying"</c>.
        ///
        /// <para>Each character is folded in as its two bytes, low first, so the answer is the
        /// same number under Mono and CoreCLR and under any culture — the same promise
        /// <see cref="StateHash"/> makes, for the same reason and by the same method.</para>
        /// </summary>
        public static AspectKey Of(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            ulong h = OffsetBasis;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                h = (h ^ (byte)c) * Prime;
                h = (h ^ (byte)(c >> 8)) * Prime;
            }
            return new AspectKey(h);
        }

        public bool Equals(AspectKey other) => Value == other.Value;

        public override bool Equals(object? obj) => obj is AspectKey other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public static bool operator ==(AspectKey a, AspectKey b) => a.Value == b.Value;

        public static bool operator !=(AspectKey a, AspectKey b) => a.Value != b.Value;

        public override string ToString() => "aspect:" + Value.ToString("x16");
    }

    /// <summary>
    /// One number a feature has published about one pawn, under a name it chose itself.
    ///
    /// <para><b>What this is for.</b> <see cref="PawnView"/> is a struct every consumer reads, in
    /// the assembly both sides reference. Every new thing a pawn can do has so far widened it:
    /// felling added <see cref="PawnView.Working"/> and <see cref="PawnView.WorkCell"/>, hauling
    /// added <see cref="PawnView.Gesture"/> and its serial, and hauling water, sleeping in a bed
    /// and being injured all would in turn. Widening a shared contract is the chokepoint this
    /// mechanism exists to open: a feature living entirely outside this assembly can publish what
    /// the interface needs to see, and nothing anyone else owns changes.</para>
    ///
    /// <para><b>Sparse, like <see cref="OrderView"/> and for the same reason.</b> Most pawns have
    /// no aspects at all and none has many, so a row per published value is smaller than a field
    /// per aspect on every pawn, and it costs exactly nothing when a feature is not installed.</para>
    ///
    /// <para><b>One value type, and it is <c>int</c>.</b> Simulation state is integer — the save
    /// writer refuses floating point on purpose — and every richer thing the existing views carry
    /// is already an integer: a cell is a whole-world index (<see cref="OrderView.CellIndex"/>), a
    /// def is a table index, a flag is nought or one. A second value type would buy an
    /// expressiveness the simulation underneath does not have.</para>
    ///
    /// <para><b>Not saved and not hashed</b>, exactly as <see cref="PawnGesture"/> is not. What a
    /// feature publishes here is a report derived from state it holds itself; that state is what
    /// belongs in the save and the hash, and the feature is what owns it. Hashing the report
    /// instead would make the look of the game part of the determinism contract.</para>
    /// </summary>
    public readonly struct PawnAspect
    {
        public readonly PawnId Pawn;
        public readonly AspectKey Key;
        public readonly int Value;

        public PawnAspect(PawnId pawn, AspectKey key, int value)
        {
            Pawn = pawn;
            Key = key;
            Value = value;
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

        /// <summary>Units of material that have arrived.</summary>
        public readonly ushort Delivered;

        /// <summary>Units the site wants in total.</summary>
        public readonly ushort Cost;

        /// <summary>Ticks of work applied.</summary>
        public readonly int WorkDone;

        /// <summary>Ticks of work the thing costs, in this material.</summary>
        public readonly int WorkTotal;

        /// <summary>
        /// Real counts and real ticks, where <see cref="OrderView.Progress"/> is a quantised byte —
        /// and the difference is not an inconsistency.
        ///
        /// <para>An order's progress is <i>a picture</i>: what reads on screen is whether a face is
        /// barely scratched or nearly through, and a byte says that to a tenth of a per cent. A
        /// site is asked a <i>question</i> — the player clicks it and expects to be told what is
        /// going up, whether the wood has arrived, and how much longer. "Three of five wood" and
        /// "about nine seconds left" cannot be recovered from a fraction, and the alternative is
        /// the interface keeping its own copy of the cost table, which is two sources for one
        /// number.</para>
        /// </summary>
        public float Progress => WorkTotal <= 0 ? 0f : (float)WorkDone / WorkTotal;

        /// <summary>Has every unit arrived, so that the thing can be worked on?</summary>
        public bool IsFrame => Delivered >= Cost;

        /// <summary>
        /// The rotation the order was placed at, 0–3 — meaningful only while
        /// <see cref="Footprint"/> is greater than one, and carried so the interface can draw the
        /// site's marks over every cell the thing will occupy rather than its head alone.
        /// </summary>
        public readonly byte Facing;

        /// <summary>Cells the finished thing will occupy: two for the bed's order, one otherwise.</summary>
        public readonly byte Footprint;

        public SiteView(int cellIndex, byte building, byte stuff,
            ushort delivered, ushort cost, int workDone, int workTotal,
            byte facing = 0, byte footprint = 1)
        {
            CellIndex = cellIndex;
            Building = building;
            Stuff = stuff;
            Delivered = delivered;
            Cost = cost;
            WorkDone = workDone;
            WorkTotal = workTotal;
            Facing = facing;
            Footprint = footprint;
        }
    }

    /// <summary>
    /// One cell of a growing zone, planted or waiting for its seed.
    ///
    /// <para>Sparse and whole-world, exactly as <see cref="OrderView"/> is and for the same
    /// reason: zones are tens to thousands of cells on a board of millions, and a zone on layer
    /// three must draw when layer three is the slice. The overlay this feeds is a tint, not
    /// geometry; what stands planted in the cell rides <see cref="PlantView"/>, which is a
    /// different row because a crop can be asked about without its zone and a zone cell exists
    /// before anything is in it.</para>
    /// </summary>
    public readonly struct ZoneView
    {
        /// <summary>The cell, as a whole-world index. <c>GridSize.FromIndex</c> unpacks it.</summary>
        public readonly int CellIndex;

        /// <summary>What the zone grows here, as a <see cref="PlantHandle"/> value.</summary>
        public readonly byte Plant;

        public ZoneView(int cellIndex, byte plant)
        {
            CellIndex = cellIndex;
            Plant = plant;
        }
    }

    /// <summary>
    /// One standing crop: a planted cell, what grows there, and how far it has got.
    ///
    /// <para><b>Quantised growth, and it is not <see cref="SiteView"/>'s argument repeated.</b>
    /// A site is asked questions in a pane; a crop is only ever <i>looked</i> at, and what reads
    /// is whether the row is sprouting, half-grown or ripe — which is <see cref="Stage"/>, a
    /// bucket with three answers. The growth byte rides along so a later progress ring needs no
    /// contract change, quantised like <see cref="OrderView.Progress"/> because a picture is
    /// what it is for; the tick-true counter stays in the simulation, where the arithmetic is
    /// done.</para>
    /// </summary>
    public readonly struct PlantView
    {
        /// <summary>The cell, as a whole-world index. <c>GridSize.FromIndex</c> unpacks it.</summary>
        public readonly int CellIndex;

        /// <summary>What is growing, as a <see cref="PlantHandle"/> value.</summary>
        public readonly byte Plant;

        /// <summary>The drawn stage, 1–3: sprout, half-grown, mature. Harvestability is not this —
        /// it is the simulation's own rule, and the giver, not the picture, decides.</summary>
        public readonly byte Stage;

        /// <summary>How far through growing, 0–255 quantised.</summary>
        public readonly byte Growth;

        public PlantView(int cellIndex, byte plant, byte stage, byte growth)
        {
            CellIndex = cellIndex;
            Plant = plant;
            Stage = stage;
            Growth = growth;
        }
    }

    /// <summary>
    /// The answer to "what is this cell": one row, published for the one cell the interface has
    /// asked about and for no other.
    ///
    /// <para><b>Selection-scoped, like <see cref="PawnAspect"/> and for the same reason.</b> The
    /// per-cell channel the snapshot already carries is one byte per cell of the active layer,
    /// which was published deliberately minimal. A readout is a <i>question</i> the player asks of
    /// one cell at a time, so a row for the asked cell costs nothing while nothing is selected,
    /// and — unlike a layer-shaped channel — it answers for a cell on any layer, including the
    /// outcrop above the slice that a click can already reach (the lesson <see cref="OrderView"/>
    /// was widened for).</para>
    ///
    /// <para><b>Real values, not quantised</b>, by <see cref="SiteView"/>'s own argument: the
    /// player clicks a cell and expects to be told what it is made of and what crossing it costs,
    /// and "a third speed" cannot be recovered from a category byte. The movement cost is in
    /// thousandths of a clear crossing — 1000 is firm ground, 1400 bog, 3000 wading — because that
    /// is the ratio a player reads; the simulation's own per-cell addends stay in the simulation,
    /// which is where the arithmetic is done.</para>
    ///
    /// <para><b>Not saved and not hashed</b>, exactly as <see cref="PawnAspect"/> is not. The row
    /// is a report derived from state the grid already owns; hashing the report would make a
    /// question part of the determinism contract.</para>
    /// </summary>
    public readonly struct CellDetail
    {
        /// <summary>The cell asked about, as a whole-world index. <c>GridSize.FromIndex</c> unpacks it.</summary>
        public readonly int CellIndex;

        /// <summary>What the cell is made of, as a <see cref="TerrainHandle"/> value.</summary>
        public readonly byte Terrain;

        /// <summary>What stands in it, as an <see cref="EdificeHandle"/> value. 0 = nothing.</summary>
        public readonly byte Edifice;

        /// <summary>The material of the slab at its lower boundary, as a <c>StuffHandle</c> value. 0 = bare.</summary>
        public readonly byte FloorStuff;

        /// <summary>Cached structural support, 0 to 4, as the support solver computes it.</summary>
        public readonly byte Support;

        /// <summary>
        /// The cost of crossing this cell, in thousandths of a clear crossing. 1000 is firm
        /// ground, 1400 is bog, 3000 is wading. Zero means it cannot be crossed at all: impassable
        /// water, or a cell with nothing to stand on.
        /// </summary>
        public readonly ushort MoveCostPerMille;

        /// <summary>
        /// Ticks of work to take this cell's terrain out of the world, or 0 when there is nothing
        /// to clear. The honest measure of "how long is this rock", as <see cref="SiteView.WorkTotal"/>
        /// is the honest measure of a build.
        /// </summary>
        public readonly ushort WorkToClear;

        /// <summary>
        /// The tier the bed standing here finished at, 1–5, or 0 where there is no bed — which is
        /// every wall, for ever. Sparse like the row it rides: most cells answer nothing.
        /// </summary>
        public readonly byte EdificeQuality;

        /// <summary>
        /// The <c>PawnId</c> of the colonist the bed standing here belongs to, or 0 where it is
        /// nobody's — 0 being a value no pawn has, ids being 1-based.
        /// </summary>
        public readonly int EdificeOwner;

        /// <summary>
        /// What this cell's growing zone grows, as a <c>PlantHandle</c>, or 255 where the cell is
        /// in no zone. Sparse like the bed's fields: most cells answer nothing, and the pane says
        /// nothing for them.
        /// </summary>
        public readonly byte ZonePlant;

        /// <summary>
        /// How far the crop standing here has grown, in thousandths of ripeness — or
        /// <c>ushort.MaxValue</c> where the zone's cell is still waiting for its seed. Read as
        /// "43% grown" beside the plant's name; the changing number is why the pane is worth
        /// holding open over a field.
        /// </summary>
        public readonly ushort CropGrowth;

        /// <summary>
        /// How many plants a sown cell of this zone's crop stands — the yield the plot will give
        /// (owner, 2026-09-18: the pane should say how many carrots are growing in the plot, so
        /// tile and pane cannot disagree about it). 0 where there is no zone.
        /// </summary>
        public readonly byte ZoneYield;

        public CellDetail(int cellIndex, byte terrain, byte edifice, byte floorStuff, byte support,
            ushort moveCostPerMille, ushort workToClear, byte edificeQuality = 0, int edificeOwner = 0,
            byte zonePlant = 255, ushort cropGrowth = ushort.MaxValue, byte zoneYield = 0)
        {
            CellIndex = cellIndex;
            Terrain = terrain;
            Edifice = edifice;
            FloorStuff = floorStuff;
            Support = support;
            MoveCostPerMille = moveCostPerMille;
            WorkToClear = workToClear;
            EdificeQuality = edificeQuality;
            EdificeOwner = edificeOwner;
            ZonePlant = zonePlant;
            CropGrowth = cropGrowth;
            ZoneYield = zoneYield;
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
        ZoneView[] _zones = Array.Empty<ZoneView>();
        PlantView[] _plants = Array.Empty<PlantView>();

        PawnAspect[] _aspects = Array.Empty<PawnAspect>();
        CellDetail[] _cellDetails = Array.Empty<CellDetail>();

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

        /// <summary>How many growing-zone cells the world holds, anywhere in it.</summary>
        public int ZoneCount { get; private set; }

        /// <summary>How many planted cells are standing.</summary>
        public int PlantCount { get; private set; }

        /// <summary>How many aspects every feature published this frame, over all pawns.</summary>
        public int AspectCount { get; private set; }

        /// <summary>How many cells the interface asked about this frame. Zero or one today.</summary>
        public int CellDetailCount { get; private set; }

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

        /// <summary>
        /// Every growing-zone cell in the world, in cell-index order. Empty when the world has no
        /// zones. See <see cref="ZoneView"/>.
        /// </summary>
        public ReadOnlySpan<ZoneView> Zones => new ReadOnlySpan<ZoneView>(_zones, 0, ZoneCount);

        /// <summary>Every standing crop, in cell-index order. See <see cref="PlantView"/>.</summary>
        public ReadOnlySpan<PlantView> Plants => new ReadOnlySpan<PlantView>(_plants, 0, PlantCount);

        /// <summary>
        /// Everything features published about pawns this frame, in the order they published it.
        ///
        /// <para>Contributors run in the order the composition root added them, so this span is
        /// the same sequence for the same world state — which is what lets a published frame be
        /// compared between two runs at all.</para>
        /// </summary>
        public ReadOnlySpan<PawnAspect> PawnAspects => new ReadOnlySpan<PawnAspect>(_aspects, 0, AspectCount);

        /// <summary>
        /// The cells the interface asked about, in the order they were asked. See
        /// <see cref="CellDetail"/> for why this is a handful of rows rather than a channel.
        /// </summary>
        public ReadOnlySpan<CellDetail> CellDetails => new ReadOnlySpan<CellDetail>(_cellDetails, 0, CellDetailCount);

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

        /// <summary>
        /// Read back one aspect of one pawn. False when no feature published that name for that
        /// pawn this frame, which callers must handle and which is the ordinary case: the feature
        /// may not be installed, the pawn may not be doing the thing, or the pawn may have died.
        ///
        /// <para>A scan, like <see cref="TryGetPawn"/> beside it. The published set is tens of
        /// rows on a real colony — sparse is the whole shape of <see cref="PawnAspect"/> — so an
        /// index would cost a dictionary per frame to save arithmetic that does not show up.
        /// A reader that wants every aspect of every pawn walks <see cref="PawnAspects"/> once
        /// instead of calling this in a loop.</para>
        /// </summary>
        public bool TryGetPawnAspect(PawnId pawn, AspectKey key, out int value)
        {
            for (int i = 0; i < AspectCount; i++)
            {
                ref readonly var aspect = ref _aspects[i];
                if (aspect.Pawn != pawn || aspect.Key != key) continue;
                value = aspect.Value;
                return true;
            }
            value = 0;
            return false;
        }

        /// <summary>
        /// Read back the row for one cell. False when no question stands, which callers must
        /// handle: the row arrives the publish after the question, and is withdrawn the publish
        /// after the question is withdrawn.
        /// </summary>
        public bool TryGetCellDetail(int cellIndex, out CellDetail detail)
        {
            for (int i = 0; i < CellDetailCount; i++)
            {
                if (_cellDetails[i].CellIndex != cellIndex) continue;
                detail = _cellDetails[i];
                return true;
            }
            detail = default;
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
            ZoneCount = 0;
            PlantCount = 0;

            AspectCount = 0;
            CellDetailCount = 0;
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

        internal void AddZone(in ZoneView view)
        {
            Grow(ref _zones, ZoneCount + 1);
            _zones[ZoneCount++] = view;
        }

        internal void AddPlant(in PlantView view)
        {
            Grow(ref _plants, PlantCount + 1);
            _plants[PlantCount++] = view;
        }

        internal void AddPawnAspect(in PawnAspect aspect)
        {
            Grow(ref _aspects, AspectCount + 1);
            _aspects[AspectCount++] = aspect;
        }

        internal void AddCellDetail(in CellDetail detail)
        {
            Grow(ref _cellDetails, CellDetailCount + 1);
            _cellDetails[CellDetailCount++] = detail;
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
