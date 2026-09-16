#nullable enable
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Show the owner where the pictures went.
    ///
    /// <para><b>Why this is a file of its own rather than two lines in each harness.</b> Every
    /// picture-taking command in this project writes PNGs into <c>Logs/</c> and then returns in
    /// silence: no window, no progress bar, nothing but a line in a console nobody has open. Run
    /// from the menu, a contact sheet is therefore indistinguishable from a menu item that does not
    /// work — which is exactly what the owner reported on 2026-09-16 after selecting three of
    /// them in a row (they had all run, and all written their files).</para>
    ///
    /// <para>The harnesses are useless if their output cannot be found, and an editor tool that
    /// gives no sign of having run is worse than useless: it teaches its user that the feature is
    /// broken. So a menu run opens the folder and puts a line in the status bar. A batch run does
    /// neither, because there is nobody there and <c>RevealInFinder</c> on a build machine opens a
    /// window that never closes.</para>
    /// </summary>
    public static class ShotFolder
    {
        /// <summary>Where every harness in this project writes its pictures.</summary>
        public const string Path = "Logs";

        /// <summary>
        /// Open <c>Logs/</c> in the file browser and say what to look for.
        ///
        /// <paramref name="pattern"/> is only for the message — the folder holds sheets from every
        /// harness, so "the newest files" is not good enough to go on.
        /// </summary>
        public static void Reveal(string pattern)
        {
            string full = System.IO.Path.GetFullPath(Path);
            if (!Directory.Exists(full))
            {
                Debug.LogWarning($"[Shot] nothing was written: {full} does not exist.");
                return;
            }

            int written = Directory.GetFiles(full, "*.png").Length;
            string message = $"Wrote to {full} — look for {pattern} ({written} pictures in the folder).";

            Debug.Log($"[Shot] {message}");
            EditorUtility.DisplayProgressBar("Odyssey", message, 1f);
            EditorUtility.ClearProgressBar();
            EditorUtility.RevealInFinder(full + System.IO.Path.DirectorySeparatorChar);
        }
    }
}
