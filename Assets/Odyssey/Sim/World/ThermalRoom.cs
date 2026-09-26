#nullable enable
using System.Collections.Generic;

namespace Odyssey.Sim.World
{
    /// <summary>A contact between two rooms through a wall or a floor slab, or a door's far
    /// side, aggregated while the layer is solved. Symmetric: the pass applies one flow to both
    /// ends.</summary>
    public readonly struct RoomLink
    {
        public RoomLink(int other, int perMille) { Other = other; PerMille = perMille; }

        /// <summary>The other room's key, or 0 for the outdoors.</summary>
        public readonly int Other;

        /// <summary>Conductance in per-mille of the difference, per pass, already summed over
        /// every cell the contact covers.</summary>
        public readonly int PerMille;
    }

    /// <summary>
    /// A vertical opening between this room and the room directly below — a stairwell, a ladder
    /// shaft, a hole in the floor. Asymmetric on purpose: warm air climbs freely and cold does
    /// not fall as fast, which is the whole of buoyancy as far as a player can tell
    /// (design 28 §4).
    /// </summary>
    public readonly struct OpeningLink
    {
        public OpeningLink(int lower, int cell) { Lower = lower; Cell = cell; }

        /// <summary>The room below, by key. Never 0: an opening over open air is a hole, not an
        /// opening.</summary>
        public readonly int Lower;

        /// <summary>The open cell itself, whole-world index — one of this room's cells.</summary>
        public readonly int Cell;
    }

    /// <summary>A door on this room's boundary. Conductance is read per pass from the nav flag,
    /// because a door swinging open changes nothing structural and must not re-solve the
    /// layer.</summary>
    public readonly struct DoorLink
    {
        public DoorLink(int far, int cell) { Far = far; Cell = cell; }

        /// <summary>The room the door opens into, by key, or 0 for the outdoors.</summary>
        public readonly int Far;

        /// <summary>The door's own cell, whole-world index.</summary>
        public readonly int Cell;
    }

    /// <summary>
    /// One enclosed per-layer region as the thermal pass sees it: an identity, a volume, and the
    /// cached surfaces through which it exchanges heat — built by <see cref="EnclosureGrid"/> in
    /// the same layer solve that decides enclosure, and invalidated by the same layer-dirty
    /// marks (design 28 §3).
    ///
    /// <para><b>The key is the region's minimum cell index.</b> That is a property of the region
    /// itself and not of the iteration that found it, so the key is deterministic and stable for
    /// as long as the cell set is — which is what lets a room's temperature ride the save by
    /// key. Cross-layer contacts (openings, slabs onto a room below) are recorded by the
    /// <i>upper</i> room, because layers solve in ascending order and so the upper solve always
    /// reads a fresh lower layer.</para>
    ///
    /// <para><b>Air temperature is not here.</b> The room is structure; the temperature is state,
    /// owned by the thermal system and keyed by <see cref="Key"/> — so a rebuilt room is a new
    /// bag of surfaces that can inherit an old temperature, rather than a temperature that
    /// silently outlived its own walls.</para>
    /// </summary>
    public sealed class ThermalRoom
    {
        /// <summary>The region's minimum whole-world cell index. Unique per layer because layers
        /// are disjoint index spans, and stable across rebuilds while the cell set is.</summary>
        public int Key;

        public int Layer;

        /// <summary>Cells of interior air. The room's heat capacity, one cell being one volume
        /// by construction.</summary>
        public int CellCount;

        /// <summary>Roof cells with open air above — the size-independent loss, per area.</summary>
        public int CeilingSkyCells;

        /// <summary>Ceiling into solid rock: the ground boundary, not the sky.</summary>
        public int CeilingRockCells;

        /// <summary>Floor onto solid ground or onto open air that is nobody's room — the ground
        /// boundary again, and the common case under the meadow.</summary>
        public int FloorRockCells;

        /// <summary>Cells with no slab below and open air beneath: a hole in the floor over the
        /// outdoors, near-open to it.</summary>
        public int FloorHoleCells;

        /// <summary>Wall cells whose far side is open air, in per-mille of conductance already
        /// multiplied by each wall's material factor. Scales with perimeter.</summary>
        public int WallOutdoorPerMille;

        /// <summary>Wall cells into solid rock (or into another boundary thing — the dead air of
        /// a double wall reads as ground, as it should).</summary>
        public int WallRockCells;

        /// <summary>Contacts through walls with rooms on the same layer, aggregated by other
        /// room.</summary>
        public readonly List<RoomLink> WallLinks = new List<RoomLink>(4);

        /// <summary>Contacts through floor slabs with the rooms below, aggregated.</summary>
        public readonly List<RoomLink> SlabLinks = new List<RoomLink>(2);

        /// <summary>Vertical openings to the rooms below — the buoyancy surfaces.</summary>
        public readonly List<OpeningLink> Openings = new List<OpeningLink>(2);

        /// <summary>Doors on the boundary, with the room (or outdoors) each opens into.</summary>
        public readonly List<DoorLink> Doors = new List<DoorLink>(4);

        /// <summary>
        /// The inheritance ledger, filled at solve time before the old rooms are discarded: old
        /// room key → number of this room's cells that were in it. The thermal system resolves a
        /// new room's starting temperature as the area-weighted mix of these (and of the outdoor
        /// curve for cells that were outdoors), which is what makes sealing or splitting a room
        /// carry its heat rather than recomputing an equilibrium — the fault Going Medieval's own
        /// players report (design 28 §5).
        /// </summary>
        public readonly Dictionary<int, int> Inherit = new Dictionary<int, int>(4);

        /// <summary>The cells themselves, whole-world indices. Kept for the inheritance ledger of
        /// the *next* solve; rooms are few and their cells are bounded by
        /// <see cref="EnclosureGrid.MaxRoomCells"/>.</summary>
        public readonly List<int> Cells = new List<int>(64);

        /// <summary>
        /// The walls and doors the fill met, packed <c>(cell &lt;&lt; 2) | direction</c>, kept so
        /// the surfaces can be rebuilt when a neighbouring layer's rooms move without filling
        /// this layer again — surfaces are the second phase of the solve and identity the first.
        /// </summary>
        public readonly List<int> Boundary = new List<int>(32);

        /// <summary>
        /// True when this room came out of the first fill its layer ever had. The thermal
        /// system keeps a temperature it already holds for such a room (a save reattaching by
        /// key) and resolves every later room from the ledger, where "no votes" means the cells
        /// were outdoors and the room starts from the curve.
        /// </summary>
        public bool FirstSolve;

        /// <summary>Drop every cached surface so <see cref="EnclosureGrid"/> can classify the
        /// room again against settled neighbours. Identity — key, cells, ledger — stays.</summary>
        public void ClearSurfaces()
        {
            CeilingSkyCells = 0;
            CeilingRockCells = 0;
            FloorRockCells = 0;
            FloorHoleCells = 0;
            WallOutdoorPerMille = 0;
            WallRockCells = 0;
            WallLinks.Clear();
            SlabLinks.Clear();
            Openings.Clear();
            Doors.Clear();
        }
    }
}
