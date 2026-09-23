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
        /// The same journey at a thousand steps instead of a hundred, 0 to 1000. Zero means the
        /// publisher did not say, and presentation falls back to <see cref="MovePercent"/>.
        ///
        /// <para><b>Why a second field for the same number.</b> A percent is the resolution the
        /// drawn figure actually moves at, because the sub-tick term cannot help: at 60 frames and
        /// 60 ticks a second there is about one frame to a tick and the leftover is nearly nought,
        /// so the figure advances by whatever the published percent advanced. On a step costing 100
        /// that is one percent a tick and exact. On a step costing 240 — a hop up a terrace — the
        /// percent moves by a whole point every 2.4 ticks, so the figure stands still for two
        /// frames and then jumps a hundredth of the step.</para>
        ///
        /// <para><b>Measured, not supposed</b> (2026-09-18): on the flat that jump is 25 mm and
        /// nobody has ever noticed it. Up a terrace, once the climb was drawn in strides rather
        /// than as an even glide, the same quantisation became <b>134 mm in one frame</b> — a
        /// visible stutter, and it is what <c>BankFootingTests.HoppingUpTheStepIsSmooth</c> failed
        /// on. Ten times the resolution puts it back to 13 mm, under the 25 mm an honest frame of
        /// walking moves.</para>
        ///
        /// <para>Added rather than widening <see cref="MovePercent"/> because a percent is what
        /// every reader of that field expects, and because the two disagree by rounding rather than
        /// by meaning — anything that only wants to know roughly how far along a pawn is can go on
        /// asking for the percent.</para>
        /// </summary>
        public readonly int MovePerMille;

        /// <summary>
        /// How much of this step the pawn retires in one tick, in the same thousandths as
        /// <see cref="MovePerMille"/>. Zero when it is not stepping, or when whoever built this
        /// view did not say.
        ///
        /// <para><b>Published because nothing outside the simulation can work it out.</b> A frame
        /// that lands between two ticks carries the figure on rather than waiting, and to do that
        /// it needs the rate. Presentation used to infer it: a global <c>movePerTick</c> from the
        /// Defs, added to a percentage as though every step cost <c>MoveCost.Orthogonal</c>. It is
        /// wrong twice over — the colonist's own pace and condition scale the rate (design 17
        /// §4a), and the step's price includes the terrain being entered, which no amount of
        /// looking at the two cells can recover.</para>
        ///
        /// <para><b>The symptom of inferring it was the figure walking backwards.</b> Whenever the
        /// guess ran ahead of what the tick actually retired, the next frame — taking the newly
        /// published figure — drew the colonist behind where the last frame had put her. Measured
        /// on the wooded meadow at two frames to the tick: <b>3,172 frames in 59,000</b> moved a
        /// colonist backwards along her own step, by up to <b>10.9 mm</b>, and the vertical
        /// sawtooth that came with it on the terrace banks is what the owner reported as vibrating
        /// where there is a terrain step (2026-09-19).</para>
        ///
        /// <para><b>Truncated down, deliberately.</b> An under-estimate makes the frame after a
        /// tick jump very slightly forward; an over-estimate makes it go backwards, and only one of
        /// those is visible. So the extrapolation is never allowed to promise more than the tick
        /// delivers.</para>
        /// </summary>
        public readonly int MoveDeltaPerMille;

        /// <summary>
        /// Is the pawn part way through a step at all?
        ///
        /// <para><b>Ask this rather than <c>MovePercent > 0</c>.</b> A percent rounds a step's first
        /// ticks down to nothing: a 240-tick hop spends its first 2.4 ticks at nought percent, so a
        /// reader gated on the percent drew the figure standing still and then caught it up in one
        /// frame — measured at 30 mm against the 10 mm a frame of that climb moves, which is a hitch
        /// at the start of every step dearer than a flat cell. Found by
        /// <c>HopArcTests.AClimbIsSmoothFrameToFrameAllTheWayUp</c> and not by anybody looking.</para>
        /// </summary>
        public bool Moving => MovePerMille > 0 || MovePercent > 0;

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

        /// <summary>
        /// What this pawn is: an index into the simulation's kind table, 0 for a colonist
        /// (design 29 §1). A field rather than an aspect for the reason <see cref="Asleep"/>
        /// gives — every figure on screen is drawn as what it is whether or not anybody selected
        /// it, and the roster has to leave the animals out before anyone clicks anything.
        /// Presentation turns the index into a species through its own catalogue.
        /// </summary>
        public readonly int Kind;

        public PawnView(
            PawnId id, CellRef cell, int food, int rest, int mood,
            int jobDef = -1, CellRef nextCell = default, int movePercent = 0,
            bool working = false, CellRef workCell = default,
            PawnGesture gesture = PawnGesture.None, byte gestureSerial = 0,
            bool asleep = false, int movePerMille = 0, int moveDeltaPerMille = 0,
            int kind = 0)
        {
            Kind = kind;
            MovePerMille = movePerMille;
            MoveDeltaPerMille = moveDeltaPerMille;
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
    /// One entry of the incident ledger: an event that happened, published so the interface can
    /// say so. The Events panel and, later, the History screen read these (design 23 §5).
    ///
    /// <para><b>The id is the edge.</b> A one-tick "something happened" flag would be missed by a
    /// panel that refreshes four times a second while the world ticks sixty (see
    /// <see cref="PawnView.GestureSerial"/> for the general rule). Ids are monotonic — the ledger
    /// only ever appends — so a reader keeps the highest it has seen and treats anything above it
    /// as new. The simulation publishes the ledger's tail, <see cref="PublishedTail"/> entries at
    /// most, so a frame is bounded whatever the colony's history; the whole ledger is the
    /// archive's business and arrives by another channel when that panel exists.</para>
    ///
    /// <para><b>Not state.</b> A row here is a report of state the ledger owns, saved and hashed
    /// there; this struct is neither, exactly as <see cref="PawnAspect"/> is neither.</para>
    /// </summary>
    public readonly struct BulletinView
    {
        /// <summary>How many of the newest entries a frame carries.</summary>
        public const int PublishedTail = 16;

        /// <summary>The ledger's own number for this entry, from 1, never reused.</summary>
        public readonly int Id;

        /// <summary>Which incident, as an <see cref="IncidentHandle"/> value.</summary>
        public readonly int IncidentDef;

        /// <summary>Where it happened — the cell to jump the camera to.</summary>
        public readonly CellRef Cell;

        /// <summary>When it happened, in ticks, for the row's own timestamp.</summary>
        public readonly int Tick;

        /// <summary>
        /// Whether it was a gift, a blow or neither: 0 neutral, 1 good, 2 bad, the values of the
        /// simulation's own favourability enum. Carried as a number so this assembly does not
        /// learn the enum — it drives the row's ink and its chime and nothing else.
        /// </summary>
        public readonly int Favourability;

        public BulletinView(int id, int incidentDef, CellRef cell, int tick, int favourability = 0)
        {
            Id = id;
            IncidentDef = incidentDef;
            Cell = cell;
            Tick = tick;
            Favourability = favourability;
        }
    }

    /// <summary>
    /// Something in the air on its way down: a skyfaller (design 23 §6). The simulation owns the
    /// flight — when it was launched, when it lands, where — and presentation only draws where
    /// along that line the current frame falls, exactly as it draws a pawn between two cells.
    ///
    /// <para>The thing does not exist as a <see cref="ThingView"/> until it lands, so nothing can
    /// haul, eat or count it in flight, and a save taken mid-air lands it on the same tick it
    /// would have landed anyway.</para>
    /// </summary>
    public readonly struct FallingView
    {
        /// <summary>What is falling, as an <see cref="ItemHandle"/> value.</summary>
        public readonly int ThingDef;

        public readonly int Stack;

        /// <summary>The cell it will land in — the topmost walkable cell of its column.</summary>
        public readonly CellRef Landing;

        /// <summary>The tick it was launched on, from which the descent is measured.</summary>
        public readonly int LaunchTick;

        /// <summary>The tick it lands on and becomes a thing.</summary>
        public readonly int LandTick;

        public FallingView(int thingDef, int stack, CellRef landing, int launchTick, int landTick)
        {
            ThingDef = thingDef;
            Stack = stack;
            Landing = landing;
            LaunchTick = launchTick;
            LandTick = landTick;
        }
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

        /// <summary>
        /// The store holding this thing, or 0 when it is lying on the floor.
        ///
        /// <para><b>A contained thing is still published, at the store's own cell.</b> That is the
        /// decision, and it is what keeps every consumer of "what does the colony hold" correct
        /// without being told anything: the stores panel counts stacks and not piles, the build
        /// palette sums the material it can afford, and the almanac's find-it jumps to a cell.
        /// Publishing shelved goods on a channel of their own would have given all four a second
        /// place to look, and the one that was forgotten would have undercounted in silence — a
        /// player refused a wall they can pay for.</para>
        ///
        /// <para>Two consumers must therefore <em>exclude</em> these rows rather than include them,
        /// and both are about position rather than quantity: the renderer draws them on the shelf
        /// instead of on the floor, and the picker does not offer them as click targets, because
        /// clicking a shelf selects the shelf.</para>
        /// </summary>
        public readonly int Container;

        /// <summary>
        /// Which of the store's slots this thing sits on, so the drawn goods have somewhere to
        /// stand. Meaningless where <see cref="Container"/> is 0.
        ///
        /// <para><b>Published rather than derived from the def, and that is the second answer to
        /// this question.</b> The first was the def itself, which is stable and needs no field —
        /// and is wrong the moment a store holds two stacks of one kind, which is the ordinary case
        /// for anything bulky: eight stacks of wood would all draw in the same place.</para>
        ///
        /// <para>It is the thing's place in its store's ordered contents, so a store <b>re-packs</b>
        /// when something leaves it and the goods behind shift along. That is a real motion on
        /// screen and the honest price of never overlapping two heaps.</para>
        /// </summary>
        public readonly byte Slot;

        public bool Contained => Container != 0;

        public ThingView(ThingId id, CellRef cell, int defIndex, int stuffIndex, int stack = 1,
            int container = 0, byte slot = 0)
        {
            Id = id;
            Cell = cell;
            DefIndex = defIndex;
            StuffIndex = stuffIndex;
            Stack = stack;
            Container = container;
            Slot = slot;
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
    /// One cell of a storage zone: where it is, which zone it belongs to, and how much the colony
    /// cares about it.
    ///
    /// <para><b>Its own channel rather than a field on <see cref="ZoneView"/></b>, which is the
    /// growing zone's. The two are drawn differently, answer different questions and a cell can
    /// be neither or either but never both — a growing zone is refused on anything but open
    /// fertile ground, and a storage zone's commonest home is a slab indoors.</para>
    ///
    /// <para>Sparse, one row per zone cell, whole-world and in cell-index order, for the reason
    /// <c>DesignationGrid.Contribute</c> gives at length: a layer is fourteen thousand cells and
    /// a warehouse is tens to thousands, and a zone painted on a layer that is not the active one
    /// must still be drawn.</para>
    /// </summary>
    public readonly struct StoreView
    {
        /// <summary>The cell, as a whole-world index. <c>GridSize.FromIndex</c> unpacks it.</summary>
        public readonly int CellIndex;

        /// <summary>
        /// Which zone this cell belongs to. A slot index, so it is stable only within the frame
        /// that published it — long enough to tell "these cells are one zone" apart from "these
        /// cells are two zones that touch", which is the only thing presentation asks of it.
        /// </summary>
        public readonly int Zone;

        /// <summary>The zone's <c>StoragePriority</c>, 0 to 4. Drawn as a strength, not a hue.</summary>
        public readonly byte Priority;

        /// <summary>
        /// The zone's place among the colony's stores, from 1: the "3" of "Stockpile 3".
        ///
        /// <para>Published rather than derived on the interface side, because
        /// <c>StorageZones.OrdinalOfCell</c> is the one owner of that rule and the inspect pane
        /// already reads it through <see cref="CellDetail.StorageOrdinal"/>. The Inventory tab
        /// (design 35) names every store at once, and a second copy of the count in the HUD would
        /// be the tab and the pane able to disagree about which store is which. 0 where no
        /// numbering was given.</para>
        /// </summary>
        public readonly int Ordinal;

        public StoreView(int cellIndex, int zone, byte priority, int ordinal = 0)
        {
            CellIndex = cellIndex;
            Zone = zone;
            Priority = priority;
            Ordinal = ordinal;
        }
    }

    /// <summary>
    /// One built store — a shelf — as the interface needs to know it.
    ///
    /// <para><b>Its own channel rather than a bit on <see cref="StoreView"/>,</b> which is one row
    /// per <i>cell</i> of a painted zone and exists to tint the ground. A shelf's ground is not
    /// tinted: the goods standing on it are the tell, and its cells are one each. The two answer
    /// different questions about different things.</para>
    ///
    /// <para>Sparse, one row per store, and it exists so that the alert bar can say a store is
    /// stuck without the simulation having to decide when to say so. What is published is the
    /// state; the latch that turns a state into a row is the interface's own, exactly as it is for
    /// an idle colonist.</para>
    /// </summary>
    public readonly struct StorageUnitView
    {
        /// <summary>The cell it stands in, as a whole-world index.</summary>
        public readonly int CellIndex;

        /// <summary>Slots in use, and slots it has.</summary>
        public readonly byte Stacks, Slots;

        /// <summary>Ordered taken apart, so its contents should be leaving.</summary>
        public readonly bool Emptying;

        /// <summary>The store's place in the same series zones are numbered in: the "3" of "Shelf 3". See <see cref="StoreView.Ordinal"/>.</summary>
        public readonly int Ordinal;

        public StorageUnitView(int cellIndex, byte stacks, byte slots, bool emptying, int ordinal = 0)
        {
            CellIndex = cellIndex;
            Stacks = stacks;
            Slots = slots;
            Emptying = emptying;
            Ordinal = ordinal;
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

        /// <summary>The drawn stage, 0–3: sown-not-sprouted
        /// (the seed day — specks and no plant), sprout, half-grown, mature. Harvestability is
        /// not this — it is the simulation's own rule, and the giver, not the picture, decides.</summary>
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

        /// <summary>Whether this cell is inside an enclosed, roofed room.</summary>
        public readonly bool IsIndoors;

        /// <summary>
        /// The storage zone covering this cell, or -1 where there is none. A slot index and so
        /// good only for this frame, which is all the pane needs: it asks the question again every
        /// time it is asked about a cell.
        /// </summary>
        public readonly int StorageZone;

        /// <summary>The store's <c>StoragePriority</c>, 0 to 4. Meaningless where <see cref="StoreKind"/> is 0.</summary>
        public readonly byte StoragePriority;

        /// <summary>How many cells the store covers — the extent the pane's title line carries.</summary>
        public readonly int StorageCells;

        /// <summary>
        /// The store's place among the colony's stores, from 1 — what an unnamed zone is called.
        ///
        /// <para>Counted in <b>cell order</b>: how many stores begin at a lower cell than this one.
        /// Deterministic, the same on both sides of a save, and nothing is written down for it. The
        /// price is that deleting an earlier store renumbers the later ones, which a typed name
        /// will fix; until then the number the pane says and the number the board says are the same
        /// number, which is the property that matters.</para>
        /// </summary>
        public readonly int StorageOrdinal;

        /// <summary>
        /// What kind of store covers this cell: 0 none, 1 a painted zone, 2 a built one.
        ///
        /// <para><b>A byte rather than "a capacity of nought means a zone".</b> The pane says
        /// different words for the two — a zone is so many tiles, a shelf so many of its stacks in
        /// use — and deriving the kind from a magic zero is how a shelf with nothing in it comes to
        /// read as a stockpile.</para>
        ///
        /// <para><see cref="StorageCells"/> and <see cref="StorageOrdinal"/> are answered for both
        /// kinds: a shelf is one cell and takes its place in the same cell-ordered count, so
        /// "Store 3" means the third store on the board whether it was painted or raised.</para>
        /// </summary>
        public readonly byte StoreKind;

        /// <summary>How many of a built store's slots are in use. 0 for anything else.</summary>
        public readonly byte StoredStacks;

        /// <summary>How many slots a built store has. 0 for anything else.</summary>
        public readonly byte StoreSlots;

        /// <summary>
        /// The one commodity a built store holds, as an <c>ItemHandle</c> — or 255 where it is
        /// empty or holds more than one kind.
        /// </summary>
        public readonly byte StoredDef;

        /// <summary>How many units of <see cref="StoredDef"/> are in there.</summary>
        public readonly int StoredUnits;

        /// <summary>
        /// The cell the store itself stands in, or -1 where no store covers this one.
        ///
        /// <para><b>Not always the cell that was clicked.</b> Solid terrain answers for the cell
        /// above it, so a click on the ground under a shelf is a click on the shelf, and
        /// <c>StorageZones.StoreCellOf</c> is the one owner of that rule. It is published because
        /// contained goods are published at their <em>store's</em> cell: without it a reader of
        /// this row cannot pick its own store's goods out of <see cref="WorldSnapshot.Things"/>,
        /// and would have to guess with the clicked cell and be wrong exactly where the pane and
        /// the panel already disagreed once.</para>
        /// </summary>
        public readonly int StoreCellIndex;

        public const byte StoreNone = 0;
        public const byte StoreZone = 1;
        public const byte StoreShelf = 2;

        /// <summary>
        /// How warm it is here, in centi-degrees (1,250 is 12.5 °C): the room's air where the
        /// cell is in an enclosed room, the outdoor curve where it is not. Every cell has an
        /// answer in a world with a thermal pass, which every colony has; <see cref="int.MinValue"/>
        /// is the one "nothing to say" — a hand-built detail from a fixture that never asked,
        /// and the pane stays silent for it exactly as it does for a wall's quality.
        /// </summary>
        public readonly int AmbientTempC;

        public CellDetail(int cellIndex, byte terrain, byte edifice, byte floorStuff, byte support,
            ushort moveCostPerMille, ushort workToClear, byte edificeQuality = 0, int edificeOwner = 0,
            byte zonePlant = 255, ushort cropGrowth = ushort.MaxValue, byte zoneYield = 0,
            bool isIndoors = false, int storageZone = -1, byte storagePriority = 0,
            int storageCells = 0, int storageOrdinal = 0,
            byte storeKind = StoreNone, byte storedStacks = 0, byte storeSlots = 0,
            byte storedDef = 255, int storedUnits = 0, int storeCellIndex = -1,
            int ambientTempC = int.MinValue)
        {
            StoreCellIndex = storeCellIndex;
            StorageZone = storageZone;
            StoragePriority = storagePriority;
            StorageCells = storageCells;
            StorageOrdinal = storageOrdinal;
            StoreKind = storeKind;
            StoredStacks = storedStacks;
            StoreSlots = storeSlots;
            StoredDef = storedDef;
            StoredUnits = storedUnits;
            AmbientTempC = ambientTempC;
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
            IsIndoors = isIndoors;
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
        StoreView[] _stores = Array.Empty<StoreView>();
        StorageUnitView[] _units = Array.Empty<StorageUnitView>();
        PlantView[] _plants = Array.Empty<PlantView>();

        PawnAspect[] _aspects = Array.Empty<PawnAspect>();
        CellDetail[] _cellDetails = Array.Empty<CellDetail>();
        BulletinView[] _bulletins = Array.Empty<BulletinView>();
        FallingView[] _falling = Array.Empty<FallingView>();

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

        /// <summary>How many storage-zone cells the world holds, anywhere in it.</summary>
        public int StoreCount { get; private set; }

        /// <summary>How many built stores this frame carries.</summary>
        public int StorageUnitCount { get; private set; }

        /// <summary>How many planted cells are standing.</summary>
        public int PlantCount { get; private set; }

        /// <summary>How many aspects every feature published this frame, over all pawns.</summary>
        public int AspectCount { get; private set; }

        /// <summary>How many cells the interface asked about this frame. Zero or one today.</summary>
        public int CellDetailCount { get; private set; }

        /// <summary>How many ledger entries this frame carries: the newest, up to <see cref="BulletinView.PublishedTail"/>.</summary>
        public int BulletinCount { get; private set; }

        /// <summary>How many things are in the air right now. Nearly always zero.</summary>
        public int FallingCount { get; private set; }

        /// <summary>
        /// The newest incidents, oldest first, so a reader walking forward meets ids in ascending
        /// order. See <see cref="BulletinView"/> for the edge rule.
        /// </summary>
        public ReadOnlySpan<BulletinView> Bulletins => new ReadOnlySpan<BulletinView>(_bulletins, 0, BulletinCount);

        /// <summary>Everything in the air, in launch order. See <see cref="FallingView"/>.</summary>
        public ReadOnlySpan<FallingView> Falling => new ReadOnlySpan<FallingView>(_falling, 0, FallingCount);

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

        /// <summary>
        /// Every storage-zone cell in the world, in cell-index order. Empty when the colony has
        /// drawn none. See <see cref="StoreView"/>.
        /// </summary>
        public ReadOnlySpan<StoreView> Stores => new ReadOnlySpan<StoreView>(_stores, 0, StoreCount);

        /// <summary>Every built store on the board. See <see cref="StorageUnitView"/>.</summary>
        public ReadOnlySpan<StorageUnitView> StorageUnits =>
            new ReadOnlySpan<StorageUnitView>(_units, 0, StorageUnitCount);

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
        /// <para><b>O(1), off an index built on the first lookup of each published frame</b>
        /// (2026-09-23, <c>docs/design/31-aspect-lookup.md</c>). It was a scan, and its comment
        /// here justified that with "the published set is tens of rows on a real colony". That
        /// stopped being true when work priorities and the colonist schedule were added: a
        /// colonist publishes <b>57 rows</b>, every tick, measured by
        /// <c>AspectScaleTests</c> — so the set is 57 × colonists, a lookup scanned half of it,
        /// and a caller doing one per colonist was quadratic in the colony. That was 4.9 ms of a
        /// frame at 384 colonists in the far-form renderer alone
        /// (<c>docs/design/25-pawn-steering.md</c> §9d).</para>
        ///
        /// <para><b>The advice in the old comment still stands, and is the reason this is not
        /// enough on its own:</b> sparse is the whole shape of <see cref="PawnAspect"/>, and a
        /// reader that wants many aspects of one pawn should still walk
        /// <see cref="PawnAspects"/> once rather than call this in a loop. What changed is what a
        /// single lookup costs.</para>
        /// </summary>
        public bool TryGetPawnAspect(PawnId pawn, AspectKey key, out int value)
        {
            if (!IndexAspects) return ScanForPawnAspect(pawn, key, out value);
            if (!_aspectsIndexed) BuildAspectIndex();

            int slot = AspectHash(pawn, key) & _aspectMask;
            while (true)
            {
                int row = _aspectSlots[slot];
                if (row < 0) { value = 0; return false; }

                ref readonly var aspect = ref _aspects[row];
                if (aspect.Pawn == pawn && aspect.Key == key)
                {
                    value = aspect.Value;
                    return true;
                }
                slot = (slot + 1) & _aspectMask;
            }
        }

        /// <summary>
        /// Whether lookups use the index. **A measurement control, not a setting** — it exists so
        /// <c>FrameTimeTests.TheAspectLookupCostsWhatItScans</c> can time the same world both ways
        /// in one run, which is the only comparison this project's machine supports
        /// (<c>docs/design/06-rendering-and-camera.md</c> §6c.1). False is the scan this replaced,
        /// kept as the control rather than as a second code path: both arms return the same answer
        /// and <c>AspectScaleTests</c> asserts it.
        /// </summary>
        public static bool IndexAspects = true;

        /// <summary>The lookup as it was before 2026-09-23. See <see cref="IndexAspects"/>.</summary>
        bool ScanForPawnAspect(PawnId pawn, AspectKey key, out int value)
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
        /// Row indices by <c>(pawn, key)</c>, open-addressed with linear probing; <c>-1</c> is a
        /// free slot. Rebuilt lazily — see <see cref="BuildAspectIndex"/>.
        /// </summary>
        int[] _aspectSlots = Array.Empty<int>();
        int _aspectMask;
        bool _aspectsIndexed;

        /// <summary>
        /// Index this frame's aspect rows, once, on the first lookup that wants one.
        ///
        /// <para><b>Lazy rather than built as rows are added</b>, which is the whole of the
        /// decision (<c>docs/design/31-aspect-lookup.md</c> §3). Indexing at publish time would
        /// put the cost inside the <i>tick</i> — the budget this project guards hardest — and
        /// would charge every world that publishes aspects whether anything ever read one; a
        /// headless golden run reads none at all. Built here, a snapshot nobody queries pays
        /// nothing, and one that is queried pays a single O(rows) pass that is then amortised
        /// over the hundreds of lookups a frame of interface makes.</para>
        ///
        /// <para><b>The first row wins, and that is not a detail.</b> The scan this replaces
        /// returned the earliest matching row, so if a contributor ever publishes one
        /// <c>(pawn, key)</c> twice, the index has to return the earlier one as well — otherwise a
        /// snapshot's answer would depend on whether anything had queried it yet, which is exactly
        /// the kind of order-dependence the published frame exists to not have.</para>
        ///
        /// <para>No <c>Dictionary</c> and no <c>Mathf</c>: this assembly is UnityEngine-free and
        /// the fast tier and the headless runs depend on its staying that way.</para>
        /// </summary>
        void BuildAspectIndex()
        {
            // Two slots a row keeps the probe short. The table is reused between frames and only
            // ever grows, so a colony crossing a power of two does not reallocate it every tick.
            int wanted = 64;
            while (wanted < (AspectCount + 1) * 2) wanted *= 2;
            if (_aspectSlots.Length < wanted) _aspectSlots = new int[wanted];
            _aspectMask = _aspectSlots.Length - 1;

            for (int i = 0; i < _aspectSlots.Length; i++) _aspectSlots[i] = -1;

            for (int i = 0; i < AspectCount; i++)
            {
                ref readonly var aspect = ref _aspects[i];
                int slot = AspectHash(aspect.Pawn, aspect.Key) & _aspectMask;
                while (true)
                {
                    int row = _aspectSlots[slot];
                    if (row < 0) { _aspectSlots[slot] = i; break; }

                    // Already published this frame: keep the earlier row, as the scan did.
                    ref readonly var seen = ref _aspects[row];
                    if (seen.Pawn == aspect.Pawn && seen.Key == aspect.Key) break;

                    slot = (slot + 1) & _aspectMask;
                }
            }

            _aspectsIndexed = true;
        }

        /// <summary>
        /// Mixes the pawn and the key into a slot. <see cref="AspectKey.Value"/> is already an FNV
        /// hash of the symbolic name, so its own bits are well spread; what this has to do is fold
        /// the pawn in and spread the result over the low bits the mask takes.
        /// </summary>
        static int AspectHash(PawnId pawn, AspectKey key)
        {
            unchecked
            {
                ulong h = key.Value ^ ((ulong)(uint)pawn.Value * 2654435761u);
                h ^= h >> 33;
                h *= 0xFF51AFD7ED558CCDUL;
                h ^= h >> 29;
                return (int)((uint)h & 0x7FFFFFFF);
            }
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
            StoreCount = 0;
            StorageUnitCount = 0;
            PlantCount = 0;

            AspectCount = 0;
            // The frame the index described has gone. Cleared rather than rebuilt: the next
            // reader rebuilds it, and a frame nobody asks about never pays for one at all.
            _aspectsIndexed = false;
            CellDetailCount = 0;
            BulletinCount = 0;
            FallingCount = 0;
        }

        internal void AddBulletin(in BulletinView view)
        {
            Grow(ref _bulletins, BulletinCount + 1);
            _bulletins[BulletinCount++] = view;
        }

        internal void AddFalling(in FallingView view)
        {
            Grow(ref _falling, FallingCount + 1);
            _falling[FallingCount++] = view;
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

        internal void AddStore(in StoreView view)
        {
            Grow(ref _stores, StoreCount + 1);
            _stores[StoreCount++] = view;
        }

        internal void AddStorageUnit(in StorageUnitView view)
        {
            Grow(ref _units, StorageUnitCount + 1);
            _units[StorageUnitCount++] = view;
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
