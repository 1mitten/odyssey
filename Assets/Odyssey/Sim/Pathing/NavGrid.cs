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
        /// Reserved. The MVP search is 4-connected — see the note on <see cref="PathFinder"/>.
        /// </summary>
        public const int Diagonal = 141;

        public const int StairUp = 290;
        public const int StairDown = 230;

        /// <summary>
        /// Climbing a layer. Dear, because it is work: a colonist hauling stone out of a shaft
        /// should prefer a ramp, and the pathfinder only learns that from the price.
        ///
        /// <para>Was 540 — nine seconds a rung at a hundred units to the tick, which the owner
        /// twice described as floating. Half of that is still three times a flat cell and still
        /// the dearest ordinary step there is.</para>
        /// </summary>
        public const int LadderUp = 270;

        /// <summary>
        /// Dropping a layer. Priced as a flat cell, because that is what it is: you let go.
        ///
        /// <para>Was 400. The asymmetry is the point and it is not a fudge — going down a hole and
        /// coming back up it are genuinely not the same job, and pricing them alike is what made a
        /// colonist take six and a half seconds to descend three metres.</para>
        /// </summary>
        public const int LadderDown = 100;
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

        public NavGrid(GridSize size)
        {
            Size = size;
            Flags = new NavFlags[size.CellCount];
            CostClass = new byte[size.CellCount];
        }

        public int ExtraCost(int index) => CostByClass[CostClass[index]];

        /// <summary>
        /// Recompute the terrain-derived bits for one cell, preserving <see cref="NavFlags.Sticky"/>.
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
        /// method's one question.</para>
        /// </summary>
        public void RefreshFrom(CellGrid grid, int index)
        {
            NavFlags sticky = Flags[index] & NavFlags.Sticky;

            bool solid = grid.IsSolidTerrain(index);
            bool blocked = grid.IsBlockedByEdifice(index);
            bool floor = grid.HasFloor(index) || (sticky & NavFlags.Connector) != 0;
            bool door = (sticky & NavFlags.Door) != 0;

            NavFlags f = sticky;
            if (solid) f |= NavFlags.Solid;
            if (blocked) f |= NavFlags.Blocked;
            if (floor) f |= NavFlags.HasFloor;
            if (floor && !solid && (!blocked || door)) f |= NavFlags.Walkable;

            Flags[index] = f;
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

        /// <summary>The cost of stepping into this cell, orthogonally, for this mode.</summary>
        public int EnterCost(int index, TraverseMode mode)
        {
            NavFlags f = Flags[index];
            int cost = MoveCost.Orthogonal + CostByClass[CostClass[index]];
            if ((f & NavFlags.Door) != 0 && (f & NavFlags.DoorOpen) == 0)
                cost += mode == TraverseMode.IgnoreDoors ? MoveCost.DoorBash : MoveCost.DoorOpening;
            if ((f & NavFlags.Hazard) != 0) cost += MoveCost.HazardPenalty;
            return cost;
        }
    }
}
