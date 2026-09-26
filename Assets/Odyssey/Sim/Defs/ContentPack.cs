#nullable enable
using System;
using System.IO;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.Sim.Defs
{
    /// <summary>
    /// The one place that knows what the core content pack is made of, where it is, and how to
    /// turn it into the records the simulation reads.
    ///
    /// <para>It exists because half a pack is worse than none. The Def types are registered in
    /// three families — the pawn tuning (<see cref="Pawns.PawnContent.Register"/>), the world
    /// tables (<see cref="Worldgen.WorldContent.Register"/>) and what can be built
    /// (<see cref="Construction.ConstructionContent.Register"/>) — and a loader given only some of
    /// them reads the others' files as "unknown Def type" and fails with a list of errors that
    /// blames the content rather than the caller. Every caller that wants the shipped pack asks
    /// here instead, so adding a fourth family is one edit rather than a hunt.</para>
    ///
    /// <para><b>This XML is now the only copy of the content.</b> It used to be the second one:
    /// <c>PawnContent.Core()</c> held the same tables hand-written in C# and a test compared them
    /// field for field, which meant every new item, job or work type was written twice and the
    /// compiler checked neither against the other. That was the half-open chokepoint OQ-15 left
    /// behind, and deleting the C# copy is what closed it.</para>
    ///
    /// <para><b>The parse is cached; the records are not.</b> Reading and binding the pack is the
    /// expensive half and its result does not change, so it happens once. Building a
    /// <see cref="Pawns.PawnContent"/> out of it is a handful of table lookups and happens per
    /// call, so every caller gets its own record — which is exactly what <c>Core()</c> did, and
    /// not merely tidiness: <c>MineJobTests</c> writes to <c>Content.StoneChanceOneIn</c> on the
    /// record it was handed, and a shared one would leak that into every test that ran after it.
    /// What <em>is</em> shared is the Defs themselves, since a record's arrays point into the
    /// database; a test that wants different content loads its own pack rather than writing
    /// through this one.</para>
    /// </summary>
    public static class ContentPack
    {
        /// <summary>The shipped pack's id, as it appears in an error message.</summary>
        public const string CoreId = "Core";

        static DefDatabase? _core;
        static string? _root;

        /// <summary>Register every Def type the core pack contains.</summary>
        public static DefLoader Register(DefLoader loader) =>
            Events.IncidentContent.Register(
                Construction.ConstructionContent.Register(
                    WorldContent.Register(PawnContent.Register(loader))));

        /// <summary>Load the core pack from a directory: every <c>.xml</c> beneath it.</summary>
        public static DefDatabase LoadCore(string root) =>
            Register(new DefLoader()).AddSource(new DirectoryDefSource(CoreId, root)).Load();

        /// <summary>
        /// Load the pack from here instead of from the repository, and forget anything already
        /// loaded. A composition root calls this once before building a world; see
        /// <see cref="FindRoot"/> for when it has to.
        /// </summary>
        public static void UseRoot(string root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _core = null;
        }

        /// <summary>The loaded core pack. Parsed once; every caller shares it.</summary>
        public static DefDatabase Core => _core ??= LoadCore(_root ?? FindRoot());

        /// <summary>A fresh <see cref="PawnContent"/> built from the loaded pack.</summary>
        public static PawnContent Pawns() => PawnContent.FromDefs(Core);

        /// <summary>The terrain table, in the index order every save and every hash depends on.</summary>
        public static TerrainDef[] Terrain() => WorldContent.TerrainFromDefs(Core);

        /// <summary>The ore deposits, each naming the terrain it is made of.</summary>
        public static NaturalContent.OreKind[] Ores() => WorldContent.OresFromDefs(Core);

        /// <summary>The crops, in the handle order the zones and crops channels carry.</summary>
        public static Growing.PlantDef[] Plants() => WorldContent.PlantsFromDefs(Core);

        /// <summary>
        /// The incidents (design 23), in <c>IncidentHandle</c> order, each already bound to the
        /// item it pays out and the worker that fires it. A fresh record per call, as
        /// <see cref="Pawns"/> is, and for the same reason.
        /// </summary>
        public static Events.IncidentContent Incidents() => Events.IncidentContent.FromDefs(Core, Pawns());

        /// <summary>The storytellers (design 59), in <c>StorytellerHandle</c> order.</summary>
        public static Events.StorytellerContent Storytellers() => Events.StorytellerContent.FromDefs(Core);

        /// <summary>
        /// Forget the loaded pack and any root set by <see cref="UseRoot"/>, so the next read goes
        /// back to the repository's own content. For a test that loaded a pack of its own.
        /// </summary>
        public static void Reset()
        {
            _core = null;
            _root = null;
            WorldContent.Forget();
        }

        /// <summary>
        /// Find the repository's content pack by walking up from this assembly.
        ///
        /// <para><b>This is a development-time answer, and it says so when it fails.</b> The same
        /// code runs from <c>tools/dotnet/…/bin/Debug/net8.0</c> in the fast tier, from
        /// <c>Library/ScriptAssemblies</c> under Unity, and from the editor while playing. All
        /// three sit below a directory holding both <c>Assets</c> and <c>ProjectSettings</c>, so
        /// walking up finds the pack from any of them, and a path baked into a constant would be
        /// wrong in at least one — the sort of thing that makes a test "only fail in CI". Both
        /// markers are checked rather than just <c>Assets</c>, which is a common enough directory
        /// name to match something that is not this repository.</para>
        ///
        /// <para><b>A built player has neither directory and would land in the throw below, which
        /// is deliberate.</b> Nothing in CI or <c>scripts/</c> builds a player, so shipping the
        /// pack is not solved here rather than solved wrongly here. When it is wanted,
        /// <c>d-07-data-pipeline.md</c> already names the answer — keep the pack as plain files
        /// and copy it into <c>StreamingAssets</c> at build time — and the composition root then
        /// calls <see cref="UseRoot"/> with <c>Application.streamingAssetsPath</c>. That is why
        /// <see cref="UseRoot"/> exists and why nothing in this assembly mentions Unity.</para>
        /// </summary>
        static string FindRoot()
        {
            string start = Path.GetDirectoryName(typeof(ContentPack).Assembly.Location) ??
                           Directory.GetCurrentDirectory();
            var cursor = new DirectoryInfo(start);

            while (cursor != null)
            {
                if (Directory.Exists(Path.Combine(cursor.FullName, "Assets")) &&
                    Directory.Exists(Path.Combine(cursor.FullName, "ProjectSettings")))
                    return Path.Combine(cursor.FullName, "Assets", "Odyssey", "Defs", CoreId);
                cursor = cursor.Parent;
            }

            throw new DirectoryNotFoundException(
                $"no directory above '{start}' holds both Assets and ProjectSettings, so the content pack " +
                "could not be found. In a built player this is expected: copy the pack into StreamingAssets " +
                $"and call {nameof(ContentPack)}.{nameof(UseRoot)} before building a world.");
        }
    }
}
