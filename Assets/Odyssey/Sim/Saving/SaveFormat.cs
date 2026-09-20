#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Saving
{
    /// <summary>Something whose state belongs in a save file.</summary>
    public interface ISaveable
    {
        /// <summary>
        /// Stable identifier for this component's section. Renaming it breaks old saves, so it is
        /// deliberately separate from the class name.
        /// </summary>
        string SaveKey { get; }

        void Save(SaveWriter writer);
        void Load(SaveReader reader);
    }

    /// <summary>
    /// Writes primitives little-endian, so a save is byte-identical across machines.
    ///
    /// Floating point is deliberately absent, matching <see cref="StateHash"/>: anything that
    /// cannot be written as an integer should not be in simulation state. Making that a compile
    /// error is better than making it a review note.
    /// </summary>
    public sealed class SaveWriter
    {
        readonly BinaryWriter _writer;

        internal SaveWriter(BinaryWriter writer) { _writer = writer; }

        public void Write(bool value) => _writer.Write(value);
        public void Write(byte value) => _writer.Write(value);
        public void Write(int value) => _writer.Write(value);
        public void Write(uint value) => _writer.Write(value);
        public void Write(long value) => _writer.Write(value);
        public void Write(short value) => _writer.Write(value);

        public void Write(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            _writer.Write(bytes.Length);
            _writer.Write(bytes);
        }

        public void Write(CellRef cell)
        {
            _writer.Write(cell.X);
            _writer.Write(cell.Z);
            _writer.Write(cell.Y);
        }

        public void Write(ReadOnlySpan<byte> bytes)
        {
            _writer.Write(bytes.Length);
            // One call, not one per byte: a 1.1 MiB grid section was a million writes (OQ-36).
            _writer.Write(bytes);
        }
    }

    /// <summary>Reads what <see cref="SaveWriter"/> wrote, in the same order.</summary>
    public sealed class SaveReader
    {
        readonly BinaryReader _reader;

        internal SaveReader(BinaryReader reader, int formatVersion)
        {
            _reader = reader;
            FormatVersion = formatVersion;
        }

        /// <summary>
        /// The file's format version, so a section can read the layout it was written in rather
        /// than the one it would write today. The only source is <see cref="WorldSave.Load"/>,
        /// which knows it from the header; a reader handed around anywhere else carries whatever
        /// that load was told.
        /// </summary>
        public int FormatVersion { get; }

        public bool ReadBool() => _reader.ReadBoolean();
        public byte ReadByte() => _reader.ReadByte();
        public int ReadInt() => _reader.ReadInt32();
        public uint ReadUInt() => _reader.ReadUInt32();
        public long ReadLong() => _reader.ReadInt64();
        public short ReadShort() => _reader.ReadInt16();

        public string ReadString()
        {
            int length = _reader.ReadInt32();
            if (length < 0) throw new SaveLoadException("Negative string length; the file is corrupt.");
            return Encoding.UTF8.GetString(_reader.ReadBytes(length));
        }

        public CellRef ReadCell() => new CellRef(_reader.ReadInt32(), _reader.ReadInt32(), _reader.ReadInt32());

        public byte[] ReadBytes()
        {
            int length = _reader.ReadInt32();
            if (length < 0) throw new SaveLoadException("Negative byte-array length; the file is corrupt.");
            return _reader.ReadBytes(length);
        }
    }

    public sealed class SaveLoadException : Exception
    {
        public SaveLoadException(string message) : base(message) { }
        public SaveLoadException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// The generation recipe a save's header carries beyond seed, size and tick (U36): what
    /// generator made the map, which scenario placed the colony, what the player named it, and
    /// the day the tick falls on. Together with the header's own seed, size and tick, this is
    /// everything a load screen needs to describe a save from its header alone.
    ///
    /// <para><b>Day is handed in already computed, not derived here.</b> The simulation counts
    /// ticks and nothing else — the tick-to-calendar mapping is <c>GameClock</c>, which lives in
    /// the Hud assembly, and Sim must never reference it (<c>Odyssey.Sim.csproj</c> enforces the
    /// dependency direction by referencing only <c>Odyssey.Sim.Contracts</c>). The caller, who
    /// already has both the tick and the clock, derives the day and hands it in here; nothing in
    /// Sim re-implements that conversion.</para>
    /// </summary>
    public readonly struct SaveRecipe
    {
        public readonly MapType Map;
        public readonly string Scenario;
        public readonly string ColonyName;
        public readonly int Day;

        /// <summary>
        /// Flat ground with no rock or ore, and — with it — whether the woodland was kept.
        ///
        /// <para><b>Added at format 3, and the gap they close is not cosmetic.</b>
        /// <see cref="Map"/> says <c>Natural</c> for three genuinely different boards: the full
        /// natural generator, the bare board, and the wooded meadow the scene actually loads. A
        /// header that names only the map type therefore rebuilds the wrong one.</para>
        ///
        /// <para><b>The state hash cannot catch it</b>, which is why it survived U36 and was found
        /// by U38's round-trip test rather than by a save test: <c>GridSaveSection</c> writes every
        /// cell of every field, so the wrong board is entirely overwritten and the hashes match.
        /// What is not overwritten is everything worldgen returns <i>beside</i> the cells — the
        /// start cell, the outcome, the count of cells marked for work. Measured on one seed: the
        /// wooded board starts a colony at (25, 22, L11) with 34 cells marked; the same header
        /// rebuilt on the default board gives (30, 30, L11) and none. Nothing in the simulation
        /// reads those, so nothing went wrong in a test — but the camera frames the colony on the
        /// start cell when a session is built, so a loaded game would open on empty ground a
        /// third of the map away from the colony it had just restored.</para>
        /// </summary>
        public readonly bool Barren;

        /// <summary>With <see cref="Barren"/>: keep the woodland. See that field for why both are
        /// here.</summary>
        public readonly bool Wooded;

        public SaveRecipe(MapType map, string scenario, string colonyName, int day,
            bool barren = false, bool wooded = false)
        {
            Map = map;
            Scenario = scenario ?? string.Empty;
            ColonyName = colonyName ?? string.Empty;
            Day = day;
            Barren = barren;
            Wooded = wooded;
        }

        /// <summary>
        /// What a save gets when nothing else says otherwise: a version 1 file, which recorded
        /// none of this, and a version 2 file written without an explicit recipe. Both read back
        /// the same way — <see cref="MapType.Unknown"/>, empty scenario and colony name, day -1 —
        /// so "unknown" means one thing however it came about.
        /// </summary>
        public static readonly SaveRecipe Unknown = new SaveRecipe(MapType.Unknown, string.Empty, string.Empty, -1);
    }

    /// <summary>
    /// The save container.
    ///
    /// Layout: a magic number, a format version, the world scalars, then a length-prefixed
    /// section per saveable component. **Length prefixes are the forward-compatibility
    /// mechanism**: a section this build does not recognise is skipped rather than fatal, so a
    /// save written with a mod installed still loads without it.
    ///
    /// What is deliberately not saved: paths, caches, region graphs, support values and anything
    /// else derivable. They are recomputed on load, because a recomputed value is correct by
    /// construction whereas a saved one can be stale. The research found this is where the real
    /// load-time budget goes, not in parsing.
    ///
    /// Defs are referenced by name, never deep-saved, so content can change under an existing
    /// save and a missing mod degrades to a named sentinel rather than a corrupt file.
    /// </summary>
    public static class WorldSave
    {
        const ulong Magic = 0x59455353594451; // "QDYSSEY" little-endian-ish; any stable value

        /// <summary>
/// 4 (beds): the edifice record grew <c>Facing</c>, <c>Quality</c> and <c>Owner</c>, and a
        /// construction site a facing byte. Older files read back zeros — no facing (north, and
        /// meaningless to a wall anyway), no quality (which nothing before the bed ever had) and
        /// no owner (pawn ids being 1-based, 0 is nobody) — so an older world loads with its
        /// behaviour unchanged. Section readers learn the version from
        /// <see cref="SaveReader.FormatVersion"/>, the one place it is threaded down to them.
        ///
        /// <para>5 (U42): the four work accumulators — <c>DesignationGrid._work</c>,
        /// <c>ConstructionGrid._work</c>, <c>Job.ToilProgress</c> and <c>Pawn.MoveProgress</c> —
        /// count milliwork (thousandths of a tick) instead of ticks, so a rate can change how fast
        /// a pawn pays without changing what anything costs. <see cref="Rates.FromSave"/> reads an
        /// old value at the old scale.</para>
        ///
        /// <para>6 (WS3): the pawn grew <c>StarvationSeverity</c>, the bar that fills while the
        /// food need sits at zero and steps condition down in three bands. Older files read back
        /// zero — nobody in an older world had ever been starving by this definition, so nobody
        /// loses a condition they had earned. A colonist's innate pace, rolled in the same unit,
        /// is deliberately <i>not</i> saved: it is a pure function of her seed and her id, both of
        /// which are already in the file, and a recomputed value is correct by construction.</para>
        ///
        /// <para>3 (U38): the recipe grew the two natural-board flags, <c>Barren</c> and
        /// <c>Wooded</c>. See <see cref="SaveRecipe.Barren"/> for what was wrong without them —
        /// in short, three different boards all called <c>Natural</c>, so a header could not
        /// rebuild the one it was written on, and the state hash could not notice because the
        /// cells are overwritten by the load.</para>
        ///
        /// <para>2 (U36): the header grew a <see cref="SaveRecipe"/> — map type, scenario, colony
        /// name and day — after the world scalars it always carried.</para>
        ///
        /// <para><b>Every version from 1 to 6 still loads</b>, and this sentence has said "1 to
        /// 5" through two bumps because it names the numbers rather than the current one. Each
        /// field added since is read behind a <c>FormatVersion >=</c> guard in its own component,
        /// and an older file takes that field's default; <see cref="ReadHeader"/> is the one place
        /// that knows which versions wrote what about the header itself. A save newer than this
        /// build is refused outright rather than read hopefully.</para>
        /// </summary>
        public const int CurrentFormatVersion = 7;

        public static void Save(SimWorld world, Stream stream, IReadOnlyList<ISaveable> components,
            SaveRecipe? recipe = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            using var binary = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

            SaveRecipe effective = recipe ?? SaveRecipe.Unknown;

            binary.Write(Magic);
            binary.Write(CurrentFormatVersion);
            binary.Write(world.Seed);
            binary.Write(world.Size.SizeX);
            binary.Write(world.Size.SizeZ);
            binary.Write(world.Size.SizeY);
            binary.Write(world.CurrentTick);
            binary.Write((int)effective.Map);
            WriteHeaderString(binary, effective.Scenario);
            WriteHeaderString(binary, effective.ColonyName);
            binary.Write(effective.Day);
            binary.Write(effective.Barren);
            binary.Write(effective.Wooded);

            binary.Write(components.Count);
            for (int i = 0; i < components.Count; i++)
            {
                var component = components[i];

                // Each section is written to a buffer first so its length is known up front.
                // That is what lets a reader skip a section it does not understand.
                using var buffer = new MemoryStream();
                using (var sectionWriter = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
                {
                    component.Save(new SaveWriter(sectionWriter));
                }

                byte[] payload = buffer.ToArray();
                byte[] key = Encoding.UTF8.GetBytes(component.SaveKey);
                binary.Write(key.Length);
                binary.Write(key);
                binary.Write(payload.Length);
                binary.Write(payload);
            }
        }

        /// <summary>Save straight to a path, for a caller that has one (a menu, a test fixture).
        /// Sim has no <c>UnityEngine.Application.persistentDataPath</c> to read, so the path —
        /// wherever the <c>Saves</c> folder turns out to live — is entirely the caller's to
        /// supply; this only owns the bytes written at the end of it.</summary>
        public static void SaveToFile(string path, SimWorld world, IReadOnlyList<ISaveable> components,
            SaveRecipe? recipe = null)
        {
            using var stream = File.Create(path);
            Save(world, stream, components, recipe);
        }

        /// <summary>
        /// Restore into an already-built world. The world is constructed from Defs and a seed by
        /// the composition root first, exactly as a new game would be, and then has its state
        /// loaded over the top. That keeps one construction path rather than two.
        /// </summary>
        public static SaveHeader Load(SimWorld world, Stream stream, IReadOnlyList<ISaveable> components)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            using var binary = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            SaveHeader header;
            try
            {
                header = ReadHeader(binary);

                if (header.Seed != world.Seed)
                    throw new SaveLoadException(
                        $"Save seed {header.Seed} does not match the world it is being loaded into ({world.Seed}).");
                if (!header.Size.Equals(world.Size))
                    throw new SaveLoadException($"Save is {header.Size} but the world is {world.Size}.");

                var byKey = new Dictionary<string, ISaveable>(StringComparer.Ordinal);
                for (int i = 0; i < components.Count; i++) byKey[components[i].SaveKey] = components[i];

                int sectionCount = binary.ReadInt32();
                for (int i = 0; i < sectionCount; i++)
                {
                    int keyLength = binary.ReadInt32();
                    string key = Encoding.UTF8.GetString(binary.ReadBytes(keyLength));
                    int payloadLength = binary.ReadInt32();
                    byte[] payload = binary.ReadBytes(payloadLength);
                    if (payload.Length != payloadLength)
                        throw new SaveLoadException($"Save is truncated inside section '{key}'.");

                    if (!byKey.TryGetValue(key, out var component))
                    {
                        // Unknown section: skip it. This is how a save written with a mod loads
                        // without that mod, rather than refusing outright.
                        header.AddSkippedSection(key);
                        continue;
                    }

                    using var buffer = new MemoryStream(payload, writable: false);
                    using var sectionReader = new BinaryReader(buffer, Encoding.UTF8, leaveOpen: true);
                    component.Load(new SaveReader(sectionReader, header.FormatVersion));
                }
            }
            catch (EndOfStreamException e)
            {
                throw new SaveLoadException("Save file is truncated.", e);
            }

            world.RestoreTick(header.Tick);
            return header;
        }

        /// <summary>Load straight from a path. See <see cref="SaveToFile"/> for why the path is
        /// the caller's to supply.</summary>
        public static SaveHeader LoadFromFile(string path, SimWorld world, IReadOnlyList<ISaveable> components)
        {
            using var stream = File.OpenRead(path);
            return Load(world, stream, components);
        }

        /// <summary>
        /// Read just the header — seed, size, tick and the <see cref="SaveRecipe"/> — without a
        /// world to load into and without touching a single component section. This is the whole
        /// point of the header carrying the recipe: a load screen calls this once per file in a
        /// folder and can list every save, without parsing any of their bodies.
        /// </summary>
        public static SaveHeader ReadHeaderOnly(Stream stream)
        {
            using var binary = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            try
            {
                return ReadHeader(binary);
            }
            catch (EndOfStreamException e)
            {
                throw new SaveLoadException("Save file is truncated.", e);
            }
        }

        /// <summary>Read just the header from a path. See <see cref="ReadHeaderOnly(Stream)"/>.</summary>
        public static SaveHeader ReadHeaderOnly(string path)
        {
            using var stream = File.OpenRead(path);
            return ReadHeaderOnly(stream);
        }

        /// <summary>
        /// Everything before the section list: the magic, the version, the world scalars every
        /// version has written, and — from version 2 on — the <see cref="SaveRecipe"/>. The one
        /// place that knows the layout differs by version, so <see cref="Load"/> and
        /// <see cref="ReadHeaderOnly(Stream)"/> cannot read it two different ways.
        /// </summary>
        static SaveHeader ReadHeader(BinaryReader binary)
        {
            ulong magic = binary.ReadUInt64();
            if (magic != Magic) throw new SaveLoadException("Not an Odyssey save file.");

            int version = binary.ReadInt32();
            if (version > CurrentFormatVersion)
                throw new SaveLoadException(
                    $"Save format version {version} is newer than this build understands ({CurrentFormatVersion}).");

            uint seed = binary.ReadUInt32();
            var size = new GridSize(binary.ReadInt32(), binary.ReadInt32(), binary.ReadInt32());
            int tick = binary.ReadInt32();

            // Version 1 wrote none of this — it predates SaveRecipe entirely — so a file that old
            // reads back Unknown rather than guessing at a map type or a name it never recorded.
            // Version 2 wrote everything but the two board flags, which is the one place a reader
            // has to fill in rather than read: false and false is the full natural generator,
            // which is what MapType.Natural meant on its own before version 3 could say otherwise.
            SaveRecipe recipe = SaveRecipe.Unknown;
            if (version >= 2)
            {
                var map = (MapType)binary.ReadInt32();
                string scenario = ReadHeaderString(binary);
                string colony = ReadHeaderString(binary);
                int day = binary.ReadInt32();
                bool barren = version >= 3 && binary.ReadBoolean();
                bool wooded = version >= 3 && binary.ReadBoolean();
                recipe = new SaveRecipe(map, scenario, colony, day, barren, wooded);
            }

            return new SaveHeader(version, seed, size, tick, recipe);
        }

        static void WriteHeaderString(BinaryWriter binary, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            binary.Write(bytes.Length);
            binary.Write(bytes);
        }

        static string ReadHeaderString(BinaryReader binary)
        {
            int length = binary.ReadInt32();
            if (length < 0) throw new SaveLoadException("Negative string length in the header; the file is corrupt.");
            return Encoding.UTF8.GetString(binary.ReadBytes(length));
        }
    }

    /// <summary>What the header said, plus anything the load had to skip.</summary>
    public sealed class SaveHeader
    {
        readonly List<string> _skipped = new List<string>();

        internal SaveHeader(int formatVersion, uint seed, GridSize size, int tick, SaveRecipe recipe)
        {
            FormatVersion = formatVersion;
            Seed = seed;
            Size = size;
            Tick = tick;
            Recipe = recipe;
        }

        public int FormatVersion { get; }
        public uint Seed { get; }
        public GridSize Size { get; }
        public int Tick { get; }

        /// <summary>
        /// The generation recipe: map type, scenario, colony name and day. A version 1 file reads
        /// back <see cref="SaveRecipe.Unknown"/>, since it recorded none of this.
        /// </summary>
        public SaveRecipe Recipe { get; }

        /// <summary>Sections this build did not recognise, usually a mod that is no longer installed.</summary>
        public IReadOnlyList<string> SkippedSections => _skipped;

        internal void AddSkippedSection(string key) => _skipped.Add(key);
    }
}
