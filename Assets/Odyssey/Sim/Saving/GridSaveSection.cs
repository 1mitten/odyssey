#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.World;

namespace Odyssey.Sim.Saving
{
    /// <summary>
    /// The cell grid as one save section, palette-encoded and bit-packed per 25 × 25 × 5 chunk,
    /// per <c>docs/research/d-06-save-load.md</c>.
    ///
    /// <para><b>Why chunks and a palette.</b> A 40-layer ruined city is vertically stratified:
    /// whole layers are uniform rock or uniform air. A chunk whose cells all hold one value stores
    /// its palette entry and no data array at all, which is the case that does nearly all of the
    /// work — the research estimated 15× smaller than plain binary for the material grid. The
    /// chunk is also the dirty-tracking unit, so an incremental autosave later rewrites what
    /// changed rather than the map. 25 × 25 × 5 is five render chunks stacked, and divides
    /// 250 × 250 × 40 exactly into 10 × 10 × 8 = 800 chunks with no padding.</para>
    ///
    /// <para><b>What is not here.</b> <see cref="CellGrid.Support"/> and
    /// <see cref="CellGrid.Region"/> are derived, and the container's rule is that derived state is
    /// recomputed on load because a recomputed value is correct by construction whereas a saved
    /// one can be stale. They are also excluded from <see cref="CellGrid.ContributeTo"/> for the
    /// same reason, so saving them would put bytes in the file that the state hash does not agree
    /// are state. <b>The load path owes a full support solve and a region rebuild</b>; until then
    /// a freshly loaded grid has zero support everywhere, which is not the same thing as
    /// unsupported.</para>
    ///
    /// <para><b>Byte stability.</b> Saving the same state twice must produce identical bytes —
    /// that is the cheap detector for unordered iteration. Palettes are therefore built in
    /// first-seen order over a fixed cell walk, and the dictionary below is used only to look an
    /// entry up, never to enumerate one.</para>
    /// </summary>
    public sealed class GridSaveSection : ISaveable
    {
        public const int ChunkX = 25;
        public const int ChunkZ = 25;
        public const int ChunkY = 5;

        /// <summary>Bumped when the encoding changes. The container version is separate and untouched.</summary>
        public const int SectionVersion = 1;

        /// <summary>
        /// Above this many distinct values a chunk stores its cells raw. Palette indices stop
        /// paying for themselves long before here; the cap exists so that a pathological chunk
        /// cannot make the palette itself larger than the data it describes.
        /// </summary>
        public const int MaxPaletteEntries = 256;

        readonly CellGrid _grid;

        // Reused across chunks and fields so a save does not allocate per chunk.
        readonly List<uint> _palette = new List<uint>();
        readonly Dictionary<uint, int> _paletteIndex = new Dictionary<uint, int>();
        readonly List<int> _indices = new List<int>();
        uint[] _values = new uint[ChunkX * ChunkZ * ChunkY];

        public GridSaveSection(CellGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        public string SaveKey => "odyssey.grid";

        public int ChunksX => (_grid.Size.SizeX + ChunkX - 1) / ChunkX;
        public int ChunksZ => (_grid.Size.SizeZ + ChunkZ - 1) / ChunkZ;
        public int ChunksY => (_grid.Size.SizeY + ChunkY - 1) / ChunkY;
        public int ChunkCount => ChunksX * ChunksZ * ChunksY;

        public void Save(SaveWriter writer)
        {
            var size = _grid.Size;
            writer.Write(SectionVersion);
            writer.Write(size.SizeX);
            writer.Write(size.SizeZ);
            writer.Write(size.SizeY);

            SaveField(writer, i => _grid.Terrain[i]);
            SaveField(writer, i => _grid.Floor[i]);
            SaveField(writer, i => _grid.FloorStuff[i]);
            SaveField(writer, i => unchecked((uint)_grid.Edifice[i]));
            SaveField(writer, i => unchecked((uint)(int)_grid.Flags[i]));
        }

        public void Load(SaveReader reader)
        {
            int version = reader.ReadInt();
            if (version != SectionVersion)
                throw new SaveLoadException(
                    $"Grid section version {version} is not {SectionVersion}; this build cannot read it.");

            var size = _grid.Size;
            int sizeX = reader.ReadInt(), sizeZ = reader.ReadInt(), sizeY = reader.ReadInt();
            if (sizeX != size.SizeX || sizeZ != size.SizeZ || sizeY != size.SizeY)
                throw new SaveLoadException(
                    $"Grid section is {sizeX}x{sizeZ}x{sizeY} but the world is {size}.");

            LoadField(reader, (i, v) => _grid.Terrain[i] = (ushort)v);
            LoadField(reader, (i, v) => _grid.Floor[i] = (ushort)v);
            LoadField(reader, (i, v) => _grid.FloorStuff[i] = (ushort)v);
            LoadField(reader, (i, v) => _grid.Edifice[i] = unchecked((int)v));
            LoadField(reader, (i, v) => _grid.Flags[i] = (CellFlags)unchecked((int)v));

            // Support and Region are not in the file. Zero them rather than leaving whatever the
            // world happened to hold, so a loaded grid cannot silently keep a stale derivation.
            Array.Clear(_grid.Support, 0, _grid.Support.Length);
            Array.Clear(_grid.Region, 0, _grid.Region.Length);
        }

        // ------------------------------------------------------------------ one field

        void SaveField(SaveWriter writer, Func<int, uint> read)
        {
            for (int cy = 0; cy < ChunksY; cy++)
            for (int cz = 0; cz < ChunksZ; cz++)
            for (int cx = 0; cx < ChunksX; cx++)
                SaveChunk(writer, read, cx, cz, cy);
        }

        void LoadField(SaveReader reader, Action<int, uint> write)
        {
            for (int cy = 0; cy < ChunksY; cy++)
            for (int cz = 0; cz < ChunksZ; cz++)
            for (int cx = 0; cx < ChunksX; cx++)
                LoadChunk(reader, write, cx, cz, cy);
        }

        void SaveChunk(SaveWriter writer, Func<int, uint> read, int cx, int cz, int cy)
        {
            int count = GatherChunk(read, cx, cz, cy);

            _palette.Clear();
            _paletteIndex.Clear();
            _indices.Clear();
            bool raw = false;

            for (int k = 0; k < count; k++)
            {
                uint value = _values[k];
                if (!_paletteIndex.TryGetValue(value, out int index))
                {
                    if (_palette.Count == MaxPaletteEntries) { raw = true; break; }
                    index = _palette.Count;
                    _palette.Add(value);
                    _paletteIndex[value] = index;
                }
                _indices.Add(index);
            }

            if (raw)
            {
                // Kind 2: no palette, every cell at full width.
                writer.Write((byte)2);
                for (int k = 0; k < count; k++) writer.Write(_values[k]);
                return;
            }

            if (_palette.Count == 1)
            {
                // Kind 0: uniform. One value and no data array — the case the whole scheme is for.
                writer.Write((byte)0);
                writer.Write(_palette[0]);
                return;
            }

            // Kind 1: palette plus bit-packed indices. The count is stored less one, so a full
            // 256-entry palette still fits the byte rather than wrapping to zero.
            writer.Write((byte)1);
            writer.Write((byte)(_palette.Count - 1));
            for (int p = 0; p < _palette.Count; p++) writer.Write(_palette[p]);

            int bits = BitsFor(_palette.Count);
            writer.Write((byte)bits);
            writer.Write(Pack(_indices, bits));
        }

        void LoadChunk(SaveReader reader, Action<int, uint> write, int cx, int cz, int cy)
        {
            int count = ChunkExtent(cx, cz, cy, out int x0, out int z0, out int y0,
                                    out int sx, out int sz, out int sy);
            byte kind = reader.ReadByte();

            switch (kind)
            {
                case 0:
                {
                    uint value = reader.ReadUInt();
                    for (int k = 0; k < count; k++) _values[k] = value;
                    break;
                }

                case 1:
                {
                    int entries = reader.ReadByte() + 1;
                    _palette.Clear();
                    for (int p = 0; p < entries; p++) _palette.Add(reader.ReadUInt());

                    int bits = reader.ReadByte();
                    byte[] packed = reader.ReadBytes();
                    Unpack(packed, bits, count, _values, entries);
                    for (int k = 0; k < count; k++) _values[k] = _palette[(int)_values[k]];
                    break;
                }

                case 2:
                    for (int k = 0; k < count; k++) _values[k] = reader.ReadUInt();
                    break;

                default:
                    throw new SaveLoadException($"Unknown grid chunk encoding {kind}; the file is corrupt.");
            }

            ScatterChunk(write, x0, z0, y0, sx, sz, sy);
        }

        // ------------------------------------------------------------------ the cell walk
        //
        // Within a chunk the walk is y, then z, then x, so that a layer slab is contiguous — the
        // property d-06 asks for. (That file also calls the order "z-major, then y, then x",
        // which would not give it; the stated reason is what is implemented here.)

        int GatherChunk(Func<int, uint> read, int cx, int cz, int cy)
        {
            int count = ChunkExtent(cx, cz, cy, out int x0, out int z0, out int y0,
                                    out int sx, out int sz, out int sy);
            var size = _grid.Size;

            int k = 0;
            for (int y = y0; y < y0 + sy; y++)
            for (int z = z0; z < z0 + sz; z++)
            for (int x = x0; x < x0 + sx; x++)
                _values[k++] = read(size.Index(x, z, y));

            return count;
        }

        void ScatterChunk(Action<int, uint> write, int x0, int z0, int y0, int sx, int sz, int sy)
        {
            var size = _grid.Size;

            int k = 0;
            for (int y = y0; y < y0 + sy; y++)
            for (int z = z0; z < z0 + sz; z++)
            for (int x = x0; x < x0 + sx; x++)
                write(size.Index(x, z, y), _values[k++]);
        }

        /// <summary>
        /// Where a chunk starts and how far it reaches. A map whose size is not a multiple of the
        /// chunk gets short chunks at the far edge rather than padding cells, so the encoding never
        /// stores a value for a cell that does not exist.
        /// </summary>
        int ChunkExtent(int cx, int cz, int cy, out int x0, out int z0, out int y0,
                        out int sx, out int sz, out int sy)
        {
            var size = _grid.Size;
            x0 = cx * ChunkX;
            z0 = cz * ChunkZ;
            y0 = cy * ChunkY;
            sx = Math.Min(ChunkX, size.SizeX - x0);
            sz = Math.Min(ChunkZ, size.SizeZ - z0);
            sy = Math.Min(ChunkY, size.SizeY - y0);
            return sx * sz * sy;
        }

        // ------------------------------------------------------------------ bit packing

        /// <summary>Bits needed to index a palette of this size. One is the floor, never zero.</summary>
        public static int BitsFor(int paletteCount)
        {
            int bits = 1;
            while ((1 << bits) < paletteCount) bits++;
            return bits;
        }

        static byte[] Pack(List<int> indices, int bits)
        {
            var packed = new byte[(indices.Count * bits + 7) / 8];
            int bit = 0;

            for (int i = 0; i < indices.Count; i++)
            {
                int value = indices[i];
                for (int b = 0; b < bits; b++, bit++)
                {
                    if ((value & (1 << b)) == 0) continue;
                    packed[bit >> 3] |= (byte)(1 << (bit & 7));
                }
            }

            return packed;
        }

        static void Unpack(byte[] packed, int bits, int count, uint[] into, int paletteCount)
        {
            if (bits < 1 || bits > 32)
                throw new SaveLoadException($"Grid chunk claims {bits} bits per index; the file is corrupt.");
            long needed = ((long)count * bits + 7) / 8;
            if (packed.Length != needed)
                throw new SaveLoadException(
                    $"Grid chunk has {packed.Length} packed bytes but {count} cells at {bits} bits need {needed}.");

            int bit = 0;
            for (int i = 0; i < count; i++)
            {
                int value = 0;
                for (int b = 0; b < bits; b++, bit++)
                    if ((packed[bit >> 3] & (1 << (bit & 7))) != 0) value |= 1 << b;

                if (value >= paletteCount)
                    throw new SaveLoadException(
                        $"Grid chunk index {value} is outside its {paletteCount}-entry palette; the file is corrupt.");
                into[i] = (uint)value;
            }
        }
    }
}
