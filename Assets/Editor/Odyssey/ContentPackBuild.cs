#nullable enable
// Ships the content pack inside a player build.
//
// **The fault, 2026-09-19.** The owner ran the first player build: "there is no terrain — there
// seemed to be no graphics, terrain etc, apart from characters". The cause is not the renderer.
// `ContentPack.FindRoot` locates the Defs by walking up for a directory holding both `Assets` and
// `ProjectSettings`, and a built player has neither — so it throws, world generation never
// happens, and the scene is empty but for the figures the start flow had already made.
//
// `ContentPack.FindRoot`'s own remarks predicted this exactly, and named the answer: "keep the
// pack as plain files and copy it into StreamingAssets at build time — and the composition root
// then calls UseRoot with Application.streamingAssetsPath. That is why UseRoot exists and why
// nothing in this assembly mentions Unity." This is that, and `d-07-data-pipeline.md` §5 is where
// the shape was decided.
//
// **Copied at build time and removed again afterwards, never committed.** The XML under
// `Assets/Odyssey/Defs/Core` is the only copy of the pawn tuning and the world tables — CLAUDE.md
// calls that out as a standing rule — and a second copy sitting permanently in `StreamingAssets`
// would be exactly the second source of truth the rule exists to prevent. Somebody would edit one
// of the two.

using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Odyssey.EditorTools
{
    public class ContentPackBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        /// <summary>Where the pack lives in the repository. The only committed copy.</summary>
        public const string Source = "Assets/Odyssey/Defs";

        /// <summary>Where it is copied so the player can read it. Transient; never committed.</summary>
        public const string Staged = "Assets/StreamingAssets/Odyssey/Defs";

        /// <summary>
        /// What the composition root must hand <c>ContentPack.UseRoot</c> in a player, relative to
        /// <c>Application.streamingAssetsPath</c>. Named here so the two halves of the arrangement
        /// share one string rather than two spellings that agree today.
        /// </summary>
        public const string RuntimeRelative = "Odyssey/Defs";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Stage();

        public void OnPostprocessBuild(BuildReport report) => Unstage();

        /// <summary>Copy the pack into StreamingAssets so the build includes it.</summary>
        public static void Stage()
        {
            string source = Path.GetFullPath(Source);
            if (!Directory.Exists(source))
                throw new BuildFailedException($"the content pack is not at {Source}");

            Unstage();

            CopyTree(source, Path.GetFullPath(Staged));
            AssetDatabase.Refresh();

            Debug.Log($"[ContentPackBuild] staged {Source} -> {Staged} for this build.");
        }

        /// <summary>Take it out again, so the repository keeps exactly one copy.</summary>
        public static void Unstage()
        {
            string staged = Path.GetFullPath(Staged);
            if (Directory.Exists(staged)) Directory.Delete(staged, recursive: true);

            // The .meta beside it, or Unity re-creates the folder on the next refresh and leaves
            // an empty directory in the working tree that looks like somebody's mistake.
            string meta = staged + ".meta";
            if (File.Exists(meta)) File.Delete(meta);

            // `Assets/StreamingAssets` itself is left standing, empty, with its committed .meta.
            // Deleting it looks tidier and is not: Unity re-creates the folder and a fresh .meta
            // on the next refresh, so every build left one untracked file behind and the working
            // tree was never clean after one. An empty committed folder costs nothing and is
            // stable.
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Copy a directory tree, skipping Unity's <c>.meta</c> files.
        ///
        /// <para>The pack is read as plain files by <c>DirectoryDefSource</c>, which globs for
        /// <c>*.xml</c>, so the metas are dead weight in the player — and importing several
        /// hundred of them into a staged folder is the slowest part of this otherwise.</para>
        /// </summary>
        static void CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);

            foreach (string file in Directory.GetFiles(from))
            {
                if (Path.GetExtension(file) == ".meta") continue;
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), overwrite: true);
            }

            foreach (string directory in Directory.GetDirectories(from))
                CopyTree(directory, Path.Combine(to, Path.GetFileName(directory)));
        }
    }
}
