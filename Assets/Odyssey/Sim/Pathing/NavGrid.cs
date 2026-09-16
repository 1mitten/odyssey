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
        public const int LadderUp = 540;
        public const int LadderDown = 400;
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
        /// Impassable terrain — deep water — is neither solid nor blocked, so it would otherwise
        /// read as perfectly walkable: the bed beneath it is a floor. It is excluded here and
        /// nowhere else, which is what makes <see cref="KindOf"/> call it
        /// <see cref="RegionKind.Impassable"/> and keeps a lake out of every walkable region.
        /// </summary>
        public void RefreshFrom(CellGrid grid, int index)
        {
            NavFlags sticky = Flags[index] & NavFlags.Sticky;

            bool solid = grid.IsSolidTerrain(index);
            bool blocked = grid.IsBlockedByEdifice(index);
            bool floor = grid.HasFloor(index);
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
            return under < CostClassByTerrain.Length ? CostClassByTerrain[under] : (byte)0;
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
