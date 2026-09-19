#nullable enable
using System;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Pathing
{
    /// <summary>
    /// One 16-bit word per cell: everything the pathfinder is allowed to know.
    ///
    /// The rule from <c>docs/research/d-04-pathfinding.md</c> tier 0 is that nothing in the
    /// pathfinder ever reads a building or a thing. It reads this word and the cost class byte
    /// beside it, and nothing else. That is what makes the inner loop a scan over two flat
    /// arrays rather than a walk over object graphs.
    /// </summary>
    [Flags]
    public enum NavFlags : ushort
    {
        None = 0,

        /// <summary>A pawn can stand here. Doors count as walkable; the <em>mode</em> decides.</summary>
        Walkable = 1 << 0,

        /// <summary>Something to stand on: a slab at this cell's lower boundary, or solid ground below.</summary>
        HasFloor = 1 << 1,

        /// <summary>Solid natural material.</summary>
        Solid = 1 << 2,

        /// <summary>A blocking edifice — a wall, or a closed door (which also carries <see cref="Door"/>).</summary>
        Blocked = 1 << 3,

        Door = 1 << 4,
        DoorOpen = 1 << 5,

        ConnectorStair = 1 << 6,
        ConnectorLadder = 1 << 7,
        ConnectorLift = 1 << 8,

        Hazard = 1 << 9,

        // 1 << 10 and 1 << 11 were ConnectorClimb and ClimbOnly, removed with climbing
        // (owner, 2026-09-16). A colonist jumps up one block or drops down one; anything deeper
        // needs a ladder, which is a built thing. Nothing grants standing without a floor any
        // more, which is what makes a cell in mid-air impossible rather than merely discouraged.

        /// <summary>Any connector footprint cell.</summary>
        Connector = ConnectorStair | ConnectorLadder | ConnectorLift,

        /// <summary>
        /// Bits owned by registration rather than by the terrain. A flag rebuild recomputes
        /// everything else from the <see cref="CellGrid"/> and preserves these.
        /// </summary>
        Sticky = Door | DoorOpen | Connector | Hazard,
    }

    /// <summary>
    /// The closed set of movement capabilities. Small and fixed at design time on purpose: every
    /// mode costs one <c>int[]</c> over all regions in the district table, and every mode is a
    /// bit in a link's allow-mask.
    /// </summary>
    public enum TraverseMode : byte
    {
        /// <summary>Floors, stairs, ladders, doors it may open.</summary>
        Colonist = 0,

        /// <summary>As Colonist, but a bulky load forbids ladders.</summary>
        Hauler = 1,

        /// <summary>No ladders and no manipulable doors.</summary>
        Animal = 2,

        /// <summary>Raiders and bashers: a closed door is a cost, not an obstacle.</summary>
        IgnoreDoors = 3,
    }

    public static class TraverseModes
    {
        public const int Count = 4;
        public const byte AllMask = 0x0F;

        public static byte Mask(TraverseMode mode) => (byte)(1 << (int)mode);

        public static bool Allows(byte mask, TraverseMode mode) => (mask & (1 << (int)mode)) != 0;
    }

    /// <summary>
    /// Region types never merge with each other. Isolating doors and connectors into their own
    /// regions is what keeps a door opening, or a ladder being built, a perturbation of one
    /// singleton region and its links rather than a room-sized re-flood.
    /// </summary>
    public enum RegionKind : byte
    {
        /// <summary>Not part of any region: open air with nothing to stand on.</summary>
        None = 0,
        Walkable = 1,
        Door = 2,
        Connector = 3,

        /// <summary>Kept so rooms and atmosphere have a substrate. Never carries a link.</summary>
        Impassable = 4,
        Hazard = 5,
    }

    /// <summary>
    /// Every cost in the pathfinder is an <c>int</c>, and one unit is 1/100 of the time an
    /// unencumbered colonist takes to cross one flat, clear cell orthogonally.
    ///
    /// There is no float anywhere in this namespace. That is not a style preference: a float in
    /// a cost model is a machine-dependent tie-break, and a machine-dependent tie-break is the
    /// same thing as a non-deterministic simulation.
    /// </summary>
    public static class MoveCost
    {
        public const int Orthogonal = 100;

        /// <summary>
        /// What entering a cell diagonally costs at baseline (100 * sqrt(2) rounded to int).
        /// </summary>
        public const int Diagonal = 141;

        public const int StairUp = 290;
        public const int StairDown = 230;

        public const int LadderUp = 540;
        public const int LadderDown = 400;

        /// <summary>
        /// Hopping up onto a block one higher: the only way up that needs nothing built
        /// (owner, 2026-09-16 — "a colonist can jump if they need to get up +1 height block").
        ///
        /// <para>Dear, because it is effort: a colonist carrying stone should prefer a stair, and
        /// the pathfinder only learns that from the price. Half a built ladder's, which is the
        /// right relation — a ladder is a thing somebody made to make this easier — and still
        /// nearly three times a flat cell.</para>
        ///
        /// <para>This replaces a climb, which was a declared edge up a rock face and could end in
        /// mid-air. A jump cannot: both ends are cells a colonist can stand in.</para>
        ///
        /// <para><b>Halved to 135 on the owner's word, 2026-09-16: "it needs to happen quicker
        /// like a jump up — twice as quick maybe".</b> Cost is duration here — a pawn retires
        /// <c>movePerTick</c> of it a tick and presentation glides the figure across the whole
        /// step — so 270 was <b>4.5 seconds</b> to get up one block, against 1.7 for walking a flat
        /// cell. That is not a jump, it is a haul, and it read as the figure being stuck.</para>
        ///
        /// <para>Two relations survive the cut and one does not. It is still dearer than walking a
        /// flat cell, so nothing stops preferring a ramp; and it is still far dearer than the drop
        /// back down, which is the asymmetry that matters. It is no longer half a built ladder's
        /// <see cref="LadderUp"/> but a quarter of it — so a colonist offered both would jump
        /// rather than climb. That is the right answer anyway (hopping a one-block ledge really is
        /// quicker than a ladder) and they almost never compete: a ladder spans a shaft nothing
        /// can hop out of, and a hop needs a block top beside it that a shaft does not have.</para>
        ///
        /// <para><b>135 was too fast, seen in play on 2026-09-18</b> (owner: <i>"the colonists
        /// looked too fast going up definitely — I saw that … should be much slower"</i>), and this
        /// time the number is derived rather than halved. A hop is <b>drawn</b> along the slope
        /// from one cell centre to the next: 2.5 m across and 3.0 m up is a path
        /// <b>3.91 m</b> long. Cost is duration, so at 135 — 2.25 s — the figure was drawn covering
        /// it at <b>1.74 m/s against the 1.50 m/s of walking on the flat</b>. Climbing a terrace
        /// was literally quicker than strolling beside it, which is exactly what the eye picked
        /// up.</para>
        ///
        /// <para><b>The two bounds are what make 240 a choice rather than a guess.</b>
        /// <i>Floor:</i> 3.91 m at walking pace is 2.60 s, or <b>156</b> — below that a climb is
        /// drawn faster than a walk and no motion work can hide it. <i>Ceiling:</i>
        /// <see cref="StairUp"/> at <b>290</b> — past that a colonist walks to a stair rather than
        /// hopping a single block, and a hop must stay the cheapest way up one block or the
        /// terraces stop being crossable ground. 240 is 4.0 s, <b>0.98 m/s along the slope</b>,
        /// about two thirds of a walking pace: a visible labour, and still 11% quicker than the 270
        /// that once read as being stuck.</para>
        ///
        /// <para><b>And this time the motion carries it.</b> 270 read as stuck because the figure
        /// slid up the bank at a dawdle with a walk cycle under it. A hop is now drawn as a hop —
        /// <c>HopArc</c> gathers, heaves the figure over the lip and settles it — and the gait is
        /// held through the step rather than solved from the speed, because a hop is not ground
        /// locomotion. If 240 still reads as stuck, that is the pose to look at before this number.
        /// To retune: change this one constant, then re-bake the goldens
        /// (<c>ODYSSEY_REGOLDEN=1 scripts/test-fast.sh --filter TestCategory=Long</c>).</para>
        /// </summary>
        public const int JumpUp = 240;

        /// <summary>
        /// Dropping down onto the block below: half of a flat cell, because you mostly let go.
        ///
        /// <para>Tuned twice at the owner's word — 400, then 100, then half of that again: about
        /// five sixths of a second for three metres. The asymmetry against <see cref="JumpUp"/> is
        /// the point and not a fudge: going down a drop and getting back up it are genuinely not
        /// the same job, and pricing them alike is what made a colonist take six and a half seconds
        /// to descend three metres.</para>
        ///
        /// <para><b>Left alone when <see cref="JumpUp"/> was halved</b>, and the arithmetic is the
        /// reason. Cost is duration, so 50 is five sixths of a second to fall one layer — and a
        /// three-metre free fall takes 0.78 s. The drop is already at the speed of gravity;
        /// halving it again would have a colonist outrun its own weight, which is a different kind
        /// of wrong from the one being fixed. Say the word and it goes to 25.</para>
        /// </summary>
        public const int Drop = 50;
        /// <summary>
        /// What a cell of slope adds to walking into it — the addend behind
        /// <c>NaturalContent.CostClassSlope</c>, so that entering the foot of a terrace costs
        /// exactly <see cref="JumpUp"/>.
        ///
        /// <para><b>It is here, beside the hop, because the two must not drift apart.</b> A terrace
        /// climb is drawn as one ramp and charged as two steps — the walk on to the foot cell and
        /// the hop out of it — with the ramp split down the middle between them. If the two prices
        /// differ, the figure changes speed half way up a slope that does not change, which is what
        /// the owner reported on 2026-09-18 when the first half was priced as flat ground. Stating
        /// it as a subtraction rather than as 140 is what keeps them equal when either moves.</para>
        ///
        /// <para>This file and <c>NavGraph.cs</c> are the two <c>HopPriceHasOneOwnerTests</c> allows
        /// to name a hop's price, and that guard is why the arithmetic is here rather than next to
        /// the cost table it feeds.</para>
        /// </summary>
        public const int SlopeExtra = JumpUp - Orthogonal;

        public const int LiftUp = 400;
        public const int LiftDown = 400;

        /// <summary>Added when entering a closed door a mode is able to open.</summary>
        public const int DoorOpening = 60;

        /// <summary>Added when entering a closed door a mode has to break.</summary>
        public const int DoorBash = 400;

        public const int HazardPenalty = 500;

        /// <summary>Effectively forbidden: a fall edge exists so agents route <em>around</em> holes.</summary>
        public const int Fall = 100_000;

        /// <summary>
        /// Added during local A* cell expansion when a candidate cell contains a stationary
        /// pawn, gently biasing unconstrained pathfinders toward open adjacent corridors or clear paths.
        /// </summary>
        public const int OccupiedBias = 30;

        /// <summary>
        /// The floor on what the abstract search charges for crossing a region. The real charge
        /// scales with the region — see <see cref="NavGraph.RegionTransitCost"/>, which explains
        /// why a constant here was measurably wrong.
        /// </summary>
        public const int MinRegionTransit = 100;
    }

    /// <summary>
    /// The flag grid: <see cref="NavFlags"/> plus a terrain cost class per cell.
    ///
    /// Derived wholly from a <see cref="CellGrid"/> (which this class never writes to) plus the
    /// sticky registration bits. Rebuilt per dirty block, never wholesale.
    /// </summary>
    public sealed class NavGrid
    {
        public readonly GridSize Size;
        public readonly NavFlags[] Flags;

        /// <summary>Index into <see cref="CostByClass"/>. Class 0 is clear ground, addend 0.</summary>
        public readonly byte[] CostClass;

        /// <summary>Additive terrain cost per class. Def-driven in the real game.</summary>
        public readonly int[] CostByClass = new int[256];

        /// <summary>
        /// Cost class by terrain index — the table <see cref="RefreshFrom"/> reads to fill
        /// <see cref="CostClass"/>. Sized past any plausible terrain table, so the per-cell
        /// lookup needs no bounds test and an unknown terrain is simply ordinary ground.
        /// </summary>
        public readonly byte[] CostClassByTerrain = new byte[1024];

        /// <summary>
        /// A nav grid knows what terrain costs the moment it is built.
        ///
        /// The tidier arrangement would be for the composition root to hand the table down, and
        /// it was written that way first. It is wrong: a <see cref="NavGraph"/> is constructed in
        /// a dozen places and a grid that missed the call would silently price wading at the cost
        /// of walking — a wrong number, not a crash, in the one part of the simulation where a
        /// wrong number looks exactly like a right one. So the default is applied here, where it
        /// cannot be forgotten, and <see cref="SetTerrainCosts"/> remains for anyone who wants a
        /// different content set. When terrain becomes Defs this moves to the Def loader, which
        /// is the same argument arriving at a better place.
        /// </summary>
        public NavGrid(GridSize size)
        {
            Size = size;
            Flags = new NavFlags[size.CellCount];
            CostClass = new byte[size.CellCount];

            for (ushort terrain = 0; terrain < Worldgen.Natural.NaturalContent.TerrainCount; terrain++)
                CostClassByTerrain[terrain] = Worldgen.Natural.NaturalContent.CostClassOf(terrain);
            Worldgen.Natural.NaturalContent.ApplyCostClasses(CostByClass);
        }

        /// <summary>
        /// Point every terrain at its cost class and every class at its addend, replacing what
        /// the constructor put there. The per-cell grid follows on the next
        /// <see cref="RefreshFrom"/>, so a caller changing costs on a live grid must dirty it.
        /// </summary>
        public void SetTerrainCosts(byte[] classByTerrain, int[] costByClass)
        {
            if (classByTerrain != null)
                for (int t = 0; t < classByTerrain.Length && t < CostClassByTerrain.Length; t++)
                    CostClassByTerrain[t] = classByTerrain[t];
            if (costByClass != null)
                for (int c = 0; c < costByClass.Length && c < CostByClass.Length; c++)
                    CostByClass[c] = costByClass[c];
        }

        public int ExtraCost(int index) => CostByClass[CostClass[index]];

        /// <summary>
        /// Recompute the terrain-derived bits and the cost class for one cell, preserving
        /// <see cref="NavFlags.Sticky"/>.
        ///
        /// A door is walkable here whatever its state: the mode decides whether it may pass, and
        /// pushing that decision into the link and the per-cell entry test keeps closed doors
        /// from carving the region graph apart every time one shuts.
        ///
        /// <para><b>A connector is its own floor.</b> A cell carrying a declared stair, ladder or
        /// lift footprint has something to stand in whether or not a slab happens to be under it,
        /// because that is what those things are. Without this a vertical shaft is unusable: only
        /// its bottom cell rests on anything, so every cell above it fails the floor test, is
        /// therefore not walkable, and the portal links at both ends of the ladder have nothing to
        /// join — a shaft laddered from top to bottom that nothing can climb. The rule is stated
        /// here rather than worked around at each end because "can something stand here" is this
        /// method's one question. Every connector is a built thing with a tread under you, so
        /// every cell this grants a floor to is one a colonist can also walk in: climbing, which
        /// granted standing on a bare rock face without granting walking, is gone.</para>
        ///
        /// <para>Impassable terrain — deep water — is neither solid nor blocked, so it would
        /// otherwise read as perfectly walkable: the bed beneath it is a floor. It is excluded here
        /// and nowhere else, which is what makes <see cref="KindOf"/> call it
        /// <see cref="RegionKind.Impassable"/> and keeps a lake out of every walkable region.</para>
        /// </summary>
        public void RefreshFrom(CellGrid grid, int index)
        {
            NavFlags sticky = Flags[index] & NavFlags.Sticky;

            bool solid = grid.IsSolidTerrain(index);
            bool blocked = grid.IsBlockedByEdifice(index);
            bool realFloor = grid.HasFloor(index);
            bool floor = realFloor || (sticky & NavFlags.Connector) != 0;
            bool door = (sticky & NavFlags.Door) != 0;
            bool impassable = grid.IsImpassableTerrain(index);

            NavFlags f = sticky;
            if (solid) f |= NavFlags.Solid;
            if (blocked) f |= NavFlags.Blocked;
            if (floor) f |= NavFlags.HasFloor;
            if (floor && !solid && !impassable && (!blocked || door)) f |= NavFlags.Walkable;

            Flags[index] = f;
            CostClass[index] = ClassAt(grid, index);
        }

        /// <summary>
        /// The cost class of *entering* this cell, which is not always the class of its own
        /// terrain. Wading, the cell entered is the water itself, so the class is the water's.
        /// Crossing a bog, the cell entered is the air above the marsh, so the class is the one
        /// below. Own terrain first, the cell beneath second: air and rock are class 0, so a
        /// non-zero own class can only be something standable-in, and everything else defers
        /// downwards. Getting this the wrong way round gives free marsh and fails silently.
        /// </summary>
        byte ClassAt(CellGrid grid, int index)
        {
            ushort here = grid.Terrain[index];
            byte own = here < CostClassByTerrain.Length ? CostClassByTerrain[here] : (byte)0;
            if (own != 0) return own;

            int below = index - Size.LayerStride;
            if (below < 0) return 0;
            ushort under = grid.Terrain[below];
            byte beneath = under < CostClassByTerrain.Length ? CostClassByTerrain[under] : (byte)0;
            if (beneath != 0) return beneath;

            // **A slope, which no terrain says and the shape of the ground does.**
            //
            // The cell at the foot of a terrace step is drawn as a ramp from the lower floor to
            // the rim above (`TerraceFoot`, `BankLayout`), so crossing it is climbing, and it was
            // being priced as the flat grass beneath it. See NaturalContent.CostClassSlope.
            //
            // Asked last, so that a wet or boggy cell keeps the class its terrain gives it: a bank
            // may shelve into a stream, and water is the stronger claim about what it costs to
            // cross. Asked at all only when both terrain tests came back clear, which is most of
            // the board, so the cost of asking is the cheap half of TerraceFoot — see the note on
            // the neighbour scan there.
            return Worldgen.TerraceFoot.IsFoot(grid, index)
                ? Worldgen.Natural.NaturalContent.CostClassSlope
                : (byte)0;
        }

        public RegionKind KindOf(int index)
        {
            NavFlags f = Flags[index];
            if ((f & NavFlags.Walkable) != 0)
            {
                if ((f & NavFlags.Door) != 0) return RegionKind.Door;
                if ((f & NavFlags.Connector) != 0) return RegionKind.Connector;
                if ((f & NavFlags.Hazard) != 0) return RegionKind.Hazard;
                return RegionKind.Walkable;
            }

            return (f & (NavFlags.Solid | NavFlags.Blocked)) != 0
                ? RegionKind.Impassable
                : RegionKind.None;
        }

        /// <summary>Open air: nothing to stand on and nothing in the way. What you fall through.</summary>
        public bool IsAir(int index) =>
            (Flags[index] & (NavFlags.Solid | NavFlags.Blocked | NavFlags.Walkable)) == 0;

        /// <summary>Can this mode stand in this cell at all? Doors are the only interesting case.</summary>
        public bool CanEnter(int index, TraverseMode mode)
        {
            NavFlags f = Flags[index];
            if ((f & NavFlags.Walkable) == 0) return false;
            if ((f & NavFlags.Door) == 0 || (f & NavFlags.DoorOpen) != 0) return true;
            return mode != TraverseMode.Animal;
        }

        /// <summary>
        /// May a pawn <em>walk</em> into this cell — a step on its own layer?
        ///
        /// <para>Identical to <see cref="CanEnter"/> since climbing was removed (owner, 2026-09-16).
        /// It used to be narrower, because a cell could be standable purely because a climb passed
        /// through it — a rock face with nothing underneath — and walking into one was walking into
        /// mid-air. There is no such cell now: every cell a colonist may be in has a floor, so
        /// "can stand here" and "can walk in here" are the same question again.</para>
        ///
        /// <para>Kept as its own name rather than deleted, because the distinction is real the
        /// moment anything grants standing without a floor — a rope, a scaffold, a ledge — and the
        /// call sites that mean "walking" should go on saying so.</para>
        /// </summary>
        public bool CanWalkInto(int index, TraverseMode mode) => CanEnter(index, mode);

        /// <summary>The cost of stepping into this cell, orthogonally or diagonally, for this mode.</summary>
        public int EnterCost(int index, TraverseMode mode, bool diagonal = false)
        {
            NavFlags f = Flags[index];
            int baseCost = diagonal ? MoveCost.Diagonal : MoveCost.Orthogonal;
            int terrainExtra = CostByClass[CostClass[index]];
            if (diagonal && terrainExtra > 0)
                terrainExtra = (terrainExtra * MoveCost.Diagonal + 50) / MoveCost.Orthogonal;
            int cost = baseCost + terrainExtra;
            if ((f & NavFlags.Door) != 0 && (f & NavFlags.DoorOpen) == 0)
            {
                int doorCost = mode == TraverseMode.IgnoreDoors ? MoveCost.DoorBash : MoveCost.DoorOpening;
                if (diagonal) doorCost = (doorCost * MoveCost.Diagonal + 50) / MoveCost.Orthogonal;
                cost += doorCost;
            }
            if ((f & NavFlags.Hazard) != 0)
            {
                int hazardCost = MoveCost.HazardPenalty;
                if (diagonal) hazardCost = (hazardCost * MoveCost.Diagonal + 50) / MoveCost.Orthogonal;
                cost += hazardCost;
            }
            return cost;
        }
    }
}
