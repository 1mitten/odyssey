#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Odyssey.Sim;
using Odyssey.Sim.Diagnostics;
using Odyssey.Sim.Saving;
using UnityEngine;

namespace Odyssey.Presentation.Diagnostics
{
    /// <summary>
    /// One session's replay recording on disk (design 63, HR0): a keyframe written the moment
    /// recording starts, and the order log beside it, rewritten every minute and on the way out.
    ///
    /// <para><b>A folder per recording</b>, <c>Logs/replay/replay-&lt;time&gt;/</c> in the editor,
    /// with <c>keyframe.odyssey</c> and <c>orders.oyreplay</c> in it. The folder is
    /// <see cref="PerfTraceFiles"/>'s choice for the same reason: it exists to be read by whoever
    /// works on the repository, and <c>tools/dotnet/Odyssey.ReplayProbe</c> replays everything in
    /// it with no Unity.</para>
    ///
    /// <para><b>What it costs while playing.</b> Starting writes one save and takes one full hash,
    /// inside the first frame of a session, which is already the loading screen's. Then an
    /// append per applied order, one light hash every 600 ticks, and a small file every minute. It
    /// never writes the save again — the autosave is untouched and does its own thing.</para>
    ///
    /// <para><b>Simulation sections only.</b> The keyframe carries <c>ColonyWorld.SaveComponents</c>
    /// and not the view or the typed names, because a replay is a simulation and nothing the
    /// camera did can be in one.</para>
    /// </summary>
    public sealed class ReplayRecording
    {
        public const string FolderName = "replay";
        public const string KeyframeFile = "keyframe.odyssey";
        public const string LogFile = "orders.oyreplay";

        /// <summary>How many recordings are kept. A session is a few hundred kilobytes to a few megabytes.</summary>
        public const int Keep = 12;

        /// <summary>How often the log is rewritten, in real seconds, so a crash loses at most this much.</summary>
        public const float FlushSeconds = 60f;

        readonly SimWorld _world;
        readonly ReplayRecorder _recorder;
        float _sinceFlush;

        public string Folder { get; }

        ReplayRecording(SimWorld world, ReplayRecorder recorder, string folder)
        {
            _world = world;
            _recorder = recorder;
            Folder = folder;
        }

        /// <summary>
        /// Write the keyframe and attach the recorder. Between ticks only. Returns null, having
        /// logged why, when the folder cannot be written — a recording is never worth a broken
        /// session.
        /// </summary>
        public static ReplayRecording? TryBegin(SimWorld world, IReadOnlyList<ISaveable> components,
            SaveRecipe recipe)
        {
            try
            {
                string folder = NewFolder();
                WorldSave.SaveToFile(Path.Combine(folder, KeyframeFile), world, components, recipe);
                ReplayRecorder recorder = ReplayRecorder.Begin(world, fullHashAtStart: true);
                recorder.Log.Note =
                    $"{Application.productName} {Application.version}, Unity {Application.unityVersion}, " +
                    $"{SystemInfo.deviceName}, {(Application.isEditor ? "editor" : "player")}, " +
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                var recording = new ReplayRecording(world, recorder, folder);
                recording.Flush(final: false);
                Prune();
                Debug.Log($"[Odyssey] replay recording from tick {world.CurrentTick} into {folder}");
                return recording;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Odyssey] replay recording could not start: {e.Message}");
                return null;
            }
        }

        /// <summary>Once a frame, between ticks.</summary>
        public void Update(float realSeconds)
        {
            _sinceFlush += realSeconds;
            if (_sinceFlush < FlushSeconds) return;
            _sinceFlush = 0f;
            Flush(final: false);
        }

        /// <summary>
        /// Write the log as it stands. The final write also takes the full hash (about 10 ms on the
        /// played board), which is the replay's proof; the minute-by-minute ones take the light hash
        /// only, so a recording cut short by a crash still has an end to check.
        /// </summary>
        void Flush(bool final)
        {
            try
            {
                _recorder.Seal(_world, fullHash: final);
                _recorder.Log.WriteToFile(Path.Combine(Folder, LogFile));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Odyssey] replay log could not be written: {e.Message}");
            }
        }

        /// <summary>Seal, write and detach. Before the session is torn down.</summary>
        public void Stop()
        {
            Flush(final: true);
            _recorder.Detach(_world);
            Debug.Log($"[Odyssey] replay recording stopped at tick {_world.CurrentTick}: " +
                      $"{_recorder.Log.Records.Count} orders, {_recorder.Log.Checkpoints.Count} checkpoints, {Folder}");
        }

        // ------------------------------------------------------------------ the folder

        /// <summary>The same three answers as <see cref="PerfTraceFiles.Folder"/>, under its own name.</summary>
        public static string Root
        {
            get
            {
                if (Application.isEditor) return Path.Combine(Path.GetFullPath("Logs"), FolderName);
                if (Debug.isDebugBuild)
                    return Path.GetFullPath(Path.Combine(Application.dataPath, "..", FolderName));
                return Path.Combine(Application.persistentDataPath, FolderName);
            }
        }

        static string NewFolder()
        {
            string folder = Path.Combine(Root,
                "replay-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>Keep the newest <see cref="Keep"/>; the names sort by time.</summary>
        static void Prune()
        {
            string root = Root;
            if (!Directory.Exists(root)) return;
            var folders = new List<string>(Directory.GetDirectories(root, "replay-*"));
            folders.Sort(StringComparer.Ordinal);
            for (int i = 0; i < folders.Count - Keep; i++)
            {
                try { Directory.Delete(folders[i], recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
