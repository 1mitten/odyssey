#nullable enable
using System;
using System.Collections.Generic;
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
    public sealed class CellGrid : IStateHashable
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
        /// blocking flag, and the undergrowth flag a bush carries. The placement list keeps its
        /// slot, so other handles stay valid. A caller removing something that blocked, or a bush,
        /// must mark navigation dirty itself; a tree blocks nothing, so felling one changes no
        /// path, but a cleared bush changes what its cell costs to cross.
        /// </summary>
        public void RemoveEdifice(int index)
        {
            Edifice[index] = -1;
            Flags[index] &= ~(CellFlags.BlockingEdifice | CellFlags.Undergrowth);
        }

        /// <summary>Is there a bush in this cell? See <see cref="CellFlags.Undergrowth"/>.</summary>
        public bool IsUndergrowth(int index) => (Flags[index] & CellFlags.Undergrowth) != 0;

        /// <summary>Terrain a pawn can neither stand in nor stand on top of. Deep water.</summary>
        public bool IsImpassableTerrain(int index) =>
            (Flags[index] & CellFlags.ImpassableTerrain) != 0;

        /// <summary>Can a pawn stand here? Needs somewhere to stand on and nothing in the way.</summary>
        public bool IsWalkable(int index) =>
            !IsSolidTerrain(index) && !IsBlockedByEdifice(index) && !IsImpassableTerrain(index) &&
            HasFloor(index);

        /// <summary>A cell has something to stand on if it has a slab, or solid ground beneath it.</summary>
        public bool HasFloor(int index)
        {
            if (Floor[index] != 0) return true;
            int below = index - Size.LayerStride;
            return below >= 0 && IsSolidTerrain(below);
        }

        /// <summary>
        /// The walkable cell nearest a named layer in one column, or -1 if the column has none.
        ///
        /// <para>Exists because the interface names a <em>column</em> and cannot name a layer. The
        /// HUD reads snapshots, not the grid, so a debug spawn aimed "near the camera" arrives as
        /// the camera's own layer — which on open ground is the air several storeys above the
        /// terrain, and every such command was refused. The column is the part the player means;
        /// which cell in it can be stood in is the grid's business, and this is where that is
        /// answered once for everything that asks.</para>
        ///
        /// <para>Downwards first at equal distance, because the camera sits above the terrain and
        /// a colonist added under it should land on the ground rather than on whatever ledge
        /// happens to be the same number of layers up.</para>
        /// </summary>
        public int NearestWalkableInColumn(int x, int z, int preferredY)
        {
            if (x < 0 || x >= Size.SizeX || z < 0 || z >= Size.SizeZ) return -1;
            int from = preferredY < 0 ? 0 : preferredY >= Size.SizeY ? Size.SizeY - 1 : preferredY;

            for (int spread = 0; spread < Size.SizeY; spread++)
            {
                int down = from - spread;
                if (down >= 0)
                {
                    int index = Size.Index(x, z, down);
                    if (IsWalkable(index)) return index;
                }
                if (spread == 0) continue;
                int up = from + spread;
                if (up < Size.SizeY)
                {
                    int index = Size.Index(x, z, up);
                    if (IsWalkable(index)) return index;
                }
            }
            return -1;
        }

        /// <summary>
        /// The cell itself if it has something to stand on, else the first one below it that does.
        ///
        /// <para>Where a thing ends up when whatever it was resting on is taken away. It is the
        /// bottom of the fall and not the length of it: a drop of three layers and a drop of one
        /// both finish on the first real floor, because nothing in this game bounces.</para>
        ///
        /// <para>Falls out of the bottom of the world onto the lowest cell of the column rather
        /// than returning -1. There is no floor below layer nought and never will be, so a caller
        /// asking "where does this land" wants an answer it can put something in; the alternative
        /// is every caller writing the same guard and one of them forgetting.</para>
        ///
        /// <para>Note that this asks the <em>cell</em> grid, which knows about slabs and solid
        /// ground and nothing else. A ladder makes a cell standable to navigation but is not a
        /// floor, and a stack of stone left on a rung would be resting on air.</para>
        /// </summary>
        public int FirstFloorAtOrBelow(int index)
        {
            int at = index;
            while (!HasFloor(at))
            {
                int below = at - Size.LayerStride;
                if (below < 0) return at;
                at = below;
            }
            return at;
        }

        /// <summary>
        /// Is this cell covered? A slab one layer up is what makes it roofed. <b>Not the rain's
        /// rule</b>: a roof two storeys up or an overhang of rock is cover this cannot see. Whether
        /// the sky reaches a cell is <see cref="SkyColumns.ShelteredFromSky"/> (design 43 §6).
        /// </summary>
        public bool IsRoofed(int index)
        {
            int above = index + Size.LayerStride;
            return above < Size.CellCount && Floor[above] != 0;
        }

        /// <summary>
        /// Where something falling out of the sky over this column comes to rest, or -1 if it
        /// would not: the topmost cell that is not open air, if that cell can be stood in.
        ///
        /// <para>The first event lands things "through open sky" (design 23 §6), and this is the
        /// one rule that decides what that means. Walking down from the top of the world, the
        /// first cell met that has a floor, is solid, holds an edifice or is impassable water is
        /// where the fall stops. A rooftop slab is such a cell and is walkable, so a drop lands
        /// on the roof and never in the room under it. A wall's own cell, a tree's, deep water
        /// and bare rock are met first and are not walkable, so the column is refused rather
        /// than the load being put somewhere the rule did not say.</para>
        ///
        /// <para>Not <see cref="NearestWalkableInColumn"/> from the top, which would search
        /// <em>past</em> a wall to the floor beside its foot and past deep water to the bed under
        /// it — the two answers this exists to refuse. And not <see cref="FirstFloorAtOrBelow"/>,
        /// which is where a thing already inside the world goes when its floor is taken away and
        /// is happy to stop under a ceiling.</para>
        /// </summary>
        public int SkyLanding(int x, int z)
        {
            if (x < 0 || x >= Size.SizeX || z < 0 || z >= Size.SizeZ) return -1;

            for (int y = Size.SizeY - 1; y >= 0; y--)
            {
                int index = Size.Index(x, z, y);
                bool air = !HasFloor(index) && !IsSolidTerrain(index) && Edifice[index] < 0 &&
                           !IsImpassableTerrain(index);
                if (air) continue;
                return IsWalkable(index) ? index : -1;
            }
            return -1;
        }

        /// <summary>
        /// Folds the grid into the world state hash. Only authoritative fields contribute:
        /// support is derived and is rebuilt on load, so hashing it would make a save/load
        /// round trip appear to diverge for no reason. Reachability is not on the grid at all:
        /// <see cref="Pathing.NavGraph"/> keeps its own region array, and a per-cell region
        /// field here was 5 MB at the scale target that nothing ever wrote (OQ-38).
        ///
        /// <para><b>This was written long before anything called it</b> (OQ-50, 2026-09-17). The
        /// grid is not an <c>ITickable</c> and not an <c>IWorldSystem</c>, which were the only two
        /// things <c>SimWorld.ComputeStateHash</c> walked, so for most of the project's life the
        /// terrain, floors, edifices and flags were in no hash at all — mining a cell or felling a
        /// tree moved nothing. <c>SimWorldBuilder.AddHashable</c> is what reaches it now, and
        /// <c>ColonyComposition</c> registers it.</para>
        ///
        /// <para><b>It walks the whole grid every call, and that is the safe choice rather than
        /// the lazy one.</b> The arrays above are public and written directly from dozens of
        /// places — every generator pass, the support solver, mining, felling. A hash maintained
        /// incrementally on write would be silently wrong the first time anyone assigned to
        /// <c>Terrain[i]</c> without telling it, which is a worse failure than the one this fixes:
        /// it would claim two different worlds were the same. Recomputing cannot drift. The cost
        /// is real and measured — see <c>HashTraceTests.TheCostOfTracingIsMeasuredRatherThanAssumed</c>,
        /// which prints it — and it is paid per call, not per tick, because nothing in an ordinary
        /// run asks for the hash.</para>
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
        /// Terrain that cannot be stood in and cannot be stood on: deep water, and nothing else
        /// yet. It is its own bit because neither of the two that exist can express it. Solid
        /// terrain holds a colonist up on the cell above, so deep water marked solid would be a
        /// lake people walk across; non-solid terrain leaves the cell itself walkable, because
        /// the bed beneath it is a floor, so deep water marked non-solid would be a lake people
        /// walk *through*. <see cref="Worldgen.TerrainDef.impassable"/> drives it, and
        /// <see cref="CellGrid.IsWalkable"/> and <c>NavGrid.RefreshFrom</c> are the only readers.
        /// </summary>
        ImpassableTerrain = 1 << 5,

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
        ///
        /// <para><b>Bit six, not bit five, and the reason is a merge.</b> This and
        /// <see cref="ImpassableTerrain"/> were written on separate branches and both took
        /// <c>1 &lt;&lt; 5</c>. These flags are hashed and saved, so the numbering is part of the
        /// save contract and two meanings on one bit is a world that loads as a different world.
        /// Deep water landed first and keeps the bit it shipped with; this one moves.</para>
        /// </summary>
        Discovered = 1 << 6,

        /// <summary>
        /// A bush stands in this cell (design 45 §4). What navigation prices a cell by, since the
        /// grid holds an edifice's handle and not its kind: <c>NavGrid.ClassAt</c> reads this and
        /// charges <c>NaturalContent.CostClassBush</c>. Set where a bush is placed, taken away by
        /// <see cref="CellGrid.RemoveEdifice"/> with the bush, and saved and hashed with the rest.
        /// </summary>
        Undergrowth = 1 << 7,
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
            _columnEdited = new bool[size.LayerStride];
        }

        public GridSize Size { get; }
        public int ChunksX { get; }
        public int ChunksZ { get; }
        public int Count { get; }

        public int ChunkIndexOfCell(int x, int z, int y) =>
            (y * ChunksZ + z / ChunkSize) * ChunksX + x / ChunkSize;

        public int ChunkIndexOfCell(CellRef cell) => ChunkIndexOfCell(cell.X, cell.Z, cell.Y);

        public void MarkDirty(int x, int z, int y)
        {
            _dirty[ChunkIndexOfCell(x, z, y)] = true;

            int column = z * Size.SizeX + x;
            if (_columnEdited[column]) return;
            _columnEdited[column] = true;
            _editedColumns.Add(column);
        }

        public void MarkDirty(CellRef cell) => MarkDirty(cell.X, cell.Z, cell.Y);

        // The columns an edit touched, for the sky map (design 43 §6). Every edit path already
        // tells this grid which cell changed so the drawing re-meshes it; the sky map hears the
        // same notice, at column rather than chunk grain, so the two cannot be fresh about
        // different edits. Kept apart from the chunk flags because the renderer clears those on
        // its own schedule, and a headless world has no renderer to clear them at all.
        readonly bool[] _columnEdited;
        readonly List<int> _editedColumns = new List<int>();

        /// <summary>Has any cell been edited since the last <see cref="TakeEditedColumns"/>?</summary>
        public bool HasEditedColumns => _editedColumns.Count > 0;

        /// <summary>
        /// Every column edited since the last call, each once, in the order first touched, added
        /// to <paramref name="into"/>; then forget them. One reader: the sky map.
        /// </summary>
        public void TakeEditedColumns(List<int> into)
        {
            for (int i = 0; i < _editedColumns.Count; i++)
            {
                int column = _editedColumns[i];
                _columnEdited[column] = false;
                into.Add(column);
            }
            _editedColumns.Clear();
        }

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
