#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// The cell record, structure-of-arrays.
    ///
    /// Systems touch one field across many cells far more often than many fields of one cell, so
    /// each field is its own flat array. That keeps scans in cache and makes any field trivially
    /// handable to a Burst job later without restructuring anything.
    ///
    /// Indexing is <see cref="GridSize.Index"/> and nothing else. At the scale target this is
    /// 2.5 million cells and roughly 22 MB, which the benchmark confirmed is not a bottleneck.
    ///
    /// A slab is stored once, on the cell **above** the boundary it occupies: the ceiling of a
    /// cell is the floor of the cell above it. That removes a whole class of "which of the two
    /// cells owns it" bugs and makes "is this cell roofed?" a single lookup one layer up.
    /// </summary>
    public sealed class CellGrid
    {
        public readonly GridSize Size;

        /// <summary>Natural material: rock, fill, soil, pavement, rubble. Def index, 0 = nothing.</summary>
        public readonly ushort[] Terrain;

        /// <summary>The slab at this cell's lower boundary. 0 = open, a hole.</summary>
        public readonly ushort[] Floor;

        /// <summary>What the slab is built from. Def index.</summary>
        public readonly ushort[] FloorStuff;

        /// <summary>Wall, door or pillar occupying the cell. -1 = none.</summary>
        public readonly int[] Edifice;

        /// <summary>Cached structural support, computed bottom-up. See <see cref="SupportSolver"/>.</summary>
        public readonly byte[] Support;

        public readonly CellFlags[] Flags;

        public CellGrid(GridSize size)
        {
            Size = size;
            int count = size.CellCount;
            Terrain = new ushort[count];
            Floor = new ushort[count];
            FloorStuff = new ushort[count];
            Edifice = new int[count];
            Support = new byte[count];
            Flags = new CellFlags[count];
            for (int i = 0; i < count; i++) Edifice[i] = -1;
        }

        public int Index(int x, int z, int y) => Size.Index(x, z, y);
        public int Index(CellRef cell) => Size.Index(cell);
        public CellRef FromIndex(int index) => Size.FromIndex(index);
        public bool Contains(int x, int z, int y) => Size.Contains(x, z, y);

        /// <summary>Solid natural material blocks movement and holds up whatever is above it.</summary>
        public bool IsSolidTerrain(int index) => (Flags[index] & CellFlags.SolidTerrain) != 0;

        /// <summary>An edifice that blocks movement, such as a wall or a closed door.</summary>
        public bool IsBlockedByEdifice(int index) => (Flags[index] & CellFlags.BlockingEdifice) != 0;

        /// <summary>Has the colony seen what this cell is made of? See <see cref="CellFlags.Discovered"/>.</summary>
        public bool IsDiscovered(int index) => (Flags[index] & CellFlags.Discovered) != 0;

        /// <summary>
        /// Open a cell up: everything solid that touches it face to face is now known.
        ///
        /// Six neighbours, not twenty-six. A colonist who cuts a shaft past the corner of a seam
        /// has not seen into it, and counting diagonals would reveal ore through an edge that no
        /// face was ever cut in.
        /// </summary>
        public void RevealAround(int index)
        {
            int stride = Size.LayerStride;
            CellRef at = FromIndex(index);

            if (at.X > 0) Reveal(index - 1);
            if (at.X < Size.SizeX - 1) Reveal(index + 1);
            if (at.Z > 0) Reveal(index - Size.SizeX);
            if (at.Z < Size.SizeZ - 1) Reveal(index + Size.SizeX);
            if (at.Y > 0) Reveal(index - stride);
            if (at.Y < Size.SizeY - 1) Reveal(index + stride);
        }

        void Reveal(int index)
        {
            if (IsSolidTerrain(index)) Flags[index] |= CellFlags.Discovered;
        }

        /// <summary>
        /// Take whatever stands in the cell out of the world: the handle goes, and so does the
        /// blocking flag. The placement list keeps its slot, so other handles stay valid. A caller
        /// removing something that blocked must mark navigation dirty itself; a tree blocks
        /// nothing, so felling one changes no path.
        /// </summary>
        public void RemoveEdifice(int index)
        {
            Edifice[index] = -1;
            Flags[index] &= ~CellFlags.BlockingEdifice;
        }

        /// <summary>Can a pawn stand here? Needs somewhere to stand on and nothing in the way.</summary>
        public bool IsWalkable(int index) =>
            !IsSolidTerrain(index) && !IsBlockedByEdifice(index) && HasFloor(index);

        /// <summary>A cell has something to stand on if it has a slab, or solid ground beneath it.</summary>
        public bool HasFloor(int index)
        {
            if (Floor[index] != 0) return true;
            int below = index - Size.LayerStride;
            return below >= 0 && IsSolidTerrain(below);
        }

        /// <summary>Is this cell covered? A slab one layer up is what makes it roofed.</summary>
        public bool IsRoofed(int index)
        {
            int above = index + Size.LayerStride;
            return above < Size.CellCount && Floor[above] != 0;
        }

        /// <summary>
        /// Folds the grid into the world state hash. Only authoritative fields contribute:
        /// support is derived and is rebuilt on load, so hashing it would make a save/load
        /// round trip appear to diverge for no reason. Reachability is not on the grid at all:
        /// <see cref="Pathing.NavGraph"/> keeps its own region array, and a per-cell region
        /// field here was 5 MB at the scale target that nothing ever wrote (OQ-38).
        /// </summary>
        public void ContributeTo(ref StateHash hash)
        {
            for (int i = 0; i < Terrain.Length; i++)
            {
                hash.Add(Terrain[i]);
                hash.Add(Floor[i]);
                hash.Add(FloorStuff[i]);
                hash.Add(Edifice[i]);
                hash.Add((int)Flags[i]);
            }
        }
    }

    [Flags]
    public enum CellFlags
    {
        None = 0,
        SolidTerrain = 1 << 0,
        BlockingEdifice = 1 << 1,
        Forbidden = 1 << 2,
        SupportDirty = 1 << 3,
        Reserved = 1 << 4,

        /// <summary>
        /// The colony has seen what this cell is made of. Set on every solid neighbour of a cell
        /// that is mined out, and never cleared.
        ///
        /// <para>It exists for ore: a seam is drawn as plain rock until a face of it is exposed,
        /// so finding one is worth something and a tunnel is a free look at a lot of rock
        /// (<c>docs/research/mining-interview.md</c>, answer 8). Nothing is discovered when the
        /// map is generated — not even the ore lining a cavern wall, which nobody has been in.</para>
        ///
        /// <para><b>Authored, not derived, and that is the deliberate part.</b> It would be
        /// tempting to compute it — "an ore cell with an open neighbour" — and keep it out of the
        /// save and the hash the way <see cref="CellGrid.Support"/> and <see cref="CellGrid.Region"/>
        /// are kept out. But it is a one-way latch: a seam the colony has seen and then walled
        /// back up is still a seam the colony knows about, and a derived bit would forget it the
        /// moment the wall went up and remember it again when the wall came down. Knowledge is
        /// history, so it is state, so it is hashed and saved like the rest of the flags.</para>
        /// </summary>
        Discovered = 1 << 5,
    }

    /// <summary>
    /// Per-layer 25 x 25 chunks, the unit of dirty tracking and work batching.
    ///
    /// Single-layer on purpose: a vertical chunk would couple layers that are otherwise
    /// independent, and the camera only ever draws a handful of layers. The save format groups
    /// five of these vertically for compression, so one save chunk is exactly five render chunks
    /// and the mapping is integer division with no lookup.
    /// </summary>
    public sealed class ChunkGrid
    {
        public const int ChunkSize = 25;

        readonly bool[] _dirty;

        public ChunkGrid(GridSize size)
        {
            Size = size;
            ChunksX = (size.SizeX + ChunkSize - 1) / ChunkSize;
            ChunksZ = (size.SizeZ + ChunkSize - 1) / ChunkSize;
            Count = ChunksX * ChunksZ * size.SizeY;
            _dirty = new bool[Count];
        }

        public GridSize Size { get; }
        public int ChunksX { get; }
        public int ChunksZ { get; }
        public int Count { get; }

        public int ChunkIndexOfCell(int x, int z, int y) =>
            (y * ChunksZ + z / ChunkSize) * ChunksX + x / ChunkSize;

        public int ChunkIndexOfCell(CellRef cell) => ChunkIndexOfCell(cell.X, cell.Z, cell.Y);

        public void MarkDirty(int x, int z, int y) => _dirty[ChunkIndexOfCell(x, z, y)] = true;

        public void MarkDirty(CellRef cell) => MarkDirty(cell.X, cell.Z, cell.Y);

        public bool IsDirty(int chunkIndex) => _dirty[chunkIndex];

        public void ClearDirty(int chunkIndex) => _dirty[chunkIndex] = false;

        public void ClearAll() => Array.Clear(_dirty, 0, _dirty.Length);

        public int DirtyCount()
        {
            int n = 0;
            for (int i = 0; i < _dirty.Length; i++) if (_dirty[i]) n++;
            return n;
        }
    }
}
