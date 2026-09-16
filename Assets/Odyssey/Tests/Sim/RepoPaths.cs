#nullable enable
using System;
using System.IO;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Where the repository is, found rather than configured.
    ///
    /// <para>The same tests run from two very different places: <c>tools/dotnet/…/bin/Debug/net8.0</c>
    /// under the fast tier, and <c>Library/ScriptAssemblies</c> under Unity. Both sit somewhere
    /// below the repository root, so walking up until a directory holds both <c>Assets</c> and
    /// <c>ProjectSettings</c> finds it from either — and a path baked into a constant would be
    /// wrong in one of the two, which is the sort of thing that makes a test "only fail in CI".</para>
    ///
    /// <para>Both markers are checked, not just <c>Assets</c>: a directory called Assets is a
    /// common enough name that one marker could match something that is not this repository.</para>
    /// </summary>
    public static class RepoPaths
    {
        static string? _root;

        /// <summary>The repository root. Throws with the path it searched from if it is not found.</summary>
        public static string Root => _root ??= Find();

        /// <summary>The core content pack: every Def the shipped game loads.</summary>
        public static string CoreDefs => Path.Combine(Root, "Assets", "Odyssey", "Defs", "Core");

        static string Find()
        {
            string start = Path.GetDirectoryName(typeof(RepoPaths).Assembly.Location) ?? Directory.GetCurrentDirectory();
            var cursor = new DirectoryInfo(start);

            while (cursor != null)
            {
                if (Directory.Exists(Path.Combine(cursor.FullName, "Assets")) &&
                    Directory.Exists(Path.Combine(cursor.FullName, "ProjectSettings")))
                    return cursor.FullName;
                cursor = cursor.Parent;
            }

            throw new DirectoryNotFoundException(
                $"no directory above '{start}' holds both Assets and ProjectSettings, so the repository root " +
                "could not be found. A test that reads committed content cannot run from here.");
        }
    }
}
