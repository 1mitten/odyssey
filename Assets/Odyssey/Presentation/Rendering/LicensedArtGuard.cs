#nullable enable
using System.Collections.Generic;
using System.IO;

namespace Odyssey.Presentation.Rendering
{
    /// <summary>
    /// Licensed art that has landed where git would commit it (design 66 §2).
    ///
    /// <para>Every Synty pack lives under <c>Assets/Synty/</c>, which is ignored. SIMPLE Forest
    /// Animals does not install there: its package puts it at <c>Assets/SimpleForestAnimal/</c>,
    /// one <c>git add .</c> from a commit of art the project may use and may not redistribute.
    /// <c>tools/synty/unpack.py --remap</c> moves it on the way in, but the Package Manager's own
    /// import dialog is a route no script sees, so the catalogue build asks this first and refuses
    /// while any such folder exists. <c>.gitignore</c> names the folder too, so the worst case is an
    /// ignored folder rather than a commit.</para>
    /// </summary>
    public static class LicensedArtGuard
    {
        /// <summary>Where a licensed pack installs itself outside <c>Assets/Synty</c>.</summary>
        public static readonly string[] StrayFolders = { "Assets/SimpleForestAnimal" };

        /// <summary>
        /// Every stray pack folder present under <paramref name="projectRoot"/>, as the
        /// project-relative path; empty when there are none.
        /// </summary>
        public static List<string> Find(string projectRoot)
        {
            var found = new List<string>();
            foreach (string folder in StrayFolders)
                if (Directory.Exists(Path.Combine(projectRoot, folder))) found.Add(folder);
            return found;
        }

        /// <summary>What the refusal says: the folder, and the fix.</summary>
        public static string Refusal(IReadOnlyList<string> found) =>
            "Licensed art outside Assets/Synty: " + string.Join(", ", found) + ". Move it under " +
            "Assets/Synty/ (tools/synty/unpack.py extract <package> . --remap " +
            "Assets/SimpleForestAnimal=Assets/Synty/SimpleForestAnimal) before building the " +
            "catalogue, or it is one `git add .` from a commit (design 66, section 2).";
    }
}
