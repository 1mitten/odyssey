#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Odyssey.Presentation.Diagnostics
{
    /// <summary>
    /// Where a performance trace is written, and how many are kept.
    ///
    /// <para>The same shape as <see cref="Bootstrap.SaveFiles"/> and for the same reason:
    /// <c>Odyssey.Hud</c> is compiled without UnityEngine and cannot ask where anything lives, so
    /// the only thing this class knows is <i>where</i>.</para>
    ///
    /// <para><b>It parts company with <c>SaveFiles</c> on one point, deliberately.</b> A save goes
    /// to <see cref="Application.persistentDataPath"/> because "a playtest cannot leave save files
    /// in a working tree" — a rule about the player's own data. A trace is the opposite kind of
    /// thing: it exists to be read by whoever is working on the repository, minutes after it is
    /// written, and a path under <c>AppData\LocalLow\…</c> is a path somebody has to be told. So in
    /// the editor it goes to <b><c>Logs/perf/</c> at the repository root</b>, which is gitignored,
    /// is where every other editor-side artefact already goes (<c>Logs/one-day.txt</c>,
    /// <c>Logs/shot-*.png</c>, <c>Logs/hud-*.png</c>), and can be handed over as one short path. A
    /// built player has no repository to write to and falls back to the persistent data path.</para>
    ///
    /// <para>Static, because there is one such folder per machine and nothing about it varies.</para>
    /// </summary>
    public static class PerfTraceFiles
    {
        public const string FolderName = "perf";

        /// <summary>
        /// How many traces are kept. A session is a few hundred kilobytes, so this is about
        /// keeping the folder readable rather than about disk.
        /// </summary>
        public const int Keep = 20;

        /// <summary>
        /// The folder. <c>Logs/perf</c> under the project root in the editor, otherwise
        /// <c>&lt;persistentDataPath&gt;/perf</c>.
        /// </summary>
        public static string Folder =>
            Application.isEditor
                ? Path.Combine(Path.GetFullPath("Logs"), FolderName)
                : Path.Combine(Application.persistentDataPath, FolderName);

        /// <summary>The folder, made if it is not there. On demand, as <c>SaveFiles</c> does it.</summary>
        public static string EnsureFolder()
        {
            string folder = Folder;
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// A path for a trace starting now.
        ///
        /// <para>Sortable, local, and second-resolution: two traces in one second would be two
        /// sessions started inside a second of each other, which does not happen, and a sortable
        /// name means "the newest" is the last line of a directory listing rather than a question.
        /// </para>
        /// </summary>
        public static string PathForNow() => Path.Combine(EnsureFolder(),
            "trace-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".jsonl");

        /// <summary>Every trace in the folder, newest first. An empty list where there is no folder.</summary>
        public static IReadOnlyList<string> List()
        {
            string folder = Folder;
            if (!Directory.Exists(folder)) return Array.Empty<string>();

            var files = new List<string>(Directory.GetFiles(folder, "trace-*.jsonl"));
            // By name, which is by time, because the name is a sortable timestamp. Not by
            // LastWriteTime: a trace is appended to for as long as the session lasts, so write
            // times order sessions by when they *ended*, which is not the order anybody means.
            files.Sort(StringComparer.Ordinal);
            files.Reverse();
            return files;
        }

        /// <summary>
        /// Delete all but the newest <see cref="Keep"/>. Failures are swallowed: a trace that
        /// cannot be deleted is untidy, and throwing out of a diagnostic would be worse than
        /// untidy.
        /// </summary>
        public static void Prune()
        {
            IReadOnlyList<string> files = List();
            for (int i = Keep; i < files.Count; i++)
            {
                try { File.Delete(files[i]); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
