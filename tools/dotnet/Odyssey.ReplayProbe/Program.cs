using System;
using System.IO;
using Odyssey.Sim.Diagnostics;
using Odyssey.Sim.Pawns;
using Odyssey.Sim.Saving;
using Odyssey.Sim.Worldgen;
using Odyssey.Sim.Worldgen.Natural;

namespace Odyssey.ReplayProbe
{
    /// <summary>
    /// <b>Does a real play session replay to the same colony?</b> HR0 of the highlights reel
    /// (<c>docs/design/63-highlights-reel.md</c> §8, <c>docs/plans/highlights-reel.md</c>).
    ///
    /// <para>The editor records every session it plays into <c>Logs/replay/replay-&lt;time&gt;/</c>:
    /// a keyframe (<c>keyframe.odyssey</c>, an ordinary save of the simulation sections) taken on
    /// the session's first frame, and every order applied after it with the light hash every 600
    /// ticks (<c>orders.oyreplay</c>). This loads each keyframe the way the game loads a save,
    /// re-runs the orders headless through <see cref="Replayer"/>, and prints whether every hash
    /// the live colony passed through came out the same, and what a re-simulated tick costs.</para>
    ///
    /// <para>The two numbers HR0 exists for: <b>does it match</b> (if not, the first checkpoint
    /// that parted names the quarter-hour of game time to bisect with <c>HashTrace</c>), and
    /// <b>ms per tick</b>, which says whether a day of re-simulation (60,000 ticks) is short enough
    /// to hide behind a fade.</para>
    ///
    /// <code>
    /// dotnet run --project tools/dotnet/Odyssey.ReplayProbe             # every recording in Logs/replay
    /// dotnet run --project tools/dotnet/Odyssey.ReplayProbe -- &lt;path&gt;   # one recording, or a folder of them
    /// dotnet run --project tools/dotnet/Odyssey.ReplayProbe -- --sample &lt;folder&gt;   # write a headless one to try it on
    /// </code>
    ///
    /// <para>Exit code 0 when every recording replayed to the same hashes, 1 when any did not, 2
    /// when there was nothing to replay.</para>
    /// </summary>
    static class Program
    {
        const string KeyframeFile = "keyframe.odyssey";
        const string LogFile = "orders.oyreplay";

        static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--sample") return WriteSample(args[1]);

            string target = args.Length > 0 ? args[0] : Path.Combine(RepositoryRoot(), "Logs", "replay");

            string[] recordings = File.Exists(Path.Combine(target, LogFile))
                ? new[] { target }
                : Directory.Exists(target)
                    ? Directory.GetDirectories(target, "replay-*")
                    : Array.Empty<string>();
            if (recordings.Length == 0)
            {
                Console.Error.WriteLine($"No recording at '{target}'.");
                return 2;
            }

            Array.Sort(recordings, StringComparer.Ordinal);
            int failed = 0;
            foreach (string folder in recordings)
            {
                try
                {
                    if (!Report(folder)) failed++;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{Path.GetFileName(folder)}: FAILED {e.GetType().Name}: {e.Message}");
                    failed++;
                }
                Console.WriteLine();
            }

            Console.WriteLine(failed == 0
                ? $"All {recordings.Length} recording(s) replayed to the same hashes."
                : $"{failed} of {recordings.Length} recording(s) did not replay to the same hashes.");
            return failed == 0 ? 0 : 1;
        }

        static bool Report(string folder)
        {
            string keyframe = Path.Combine(folder, KeyframeFile);
            ReplayLog log = ReplayLog.ReadFromFile(Path.Combine(folder, LogFile));
            SaveHeader header = WorldSave.ReadHeaderOnly(keyframe);

            Console.WriteLine($"=== {Path.GetFileName(folder)}  {header.Recipe.ColonyName}  " +
                              $"{header.Size.SizeX}x{header.Size.SizeZ}x{header.Size.SizeY}  seed {header.Seed}");
            Console.WriteLine($"    {log.Note}");
            int ticks = log.EndTick - log.FromTick;
            int paused = log.Records.FindAll(r => r.Paused).Count;
            Console.WriteLine($"    ticks {log.FromTick} to {log.EndTick} ({ticks:N0}, {ticks / 60000.0:0.00} days), " +
                              $"{log.Records.Count} orders ({paused} while paused), {log.Checkpoints.Count} checkpoints" +
                              (log.EndFullHash == 0 ? ", no final full hash (the session did not close cleanly)" : ""));

            ColonyWorld colony = ColonyWorld.Build(new ColonyRequest
            {
                Size = header.Size,
                Seed = header.Seed,
                Map = header.Recipe.Map,
                Barren = header.Recipe.Barren,
                Wooded = header.Recipe.Wooded,
                Name = header.Recipe.ColonyName,
                Scenario = ScenarioByName(header.Recipe.Scenario),
            });
            colony.LoadFromFile(keyframe);

            ReplayResult result = Replayer.Run(colony.World, log);
            Console.WriteLine($"    {result}");
            if (ticks > 0)
                Console.WriteLine($"    a whole day re-simulated at this rate: {result.MillisecondsPerTick * 60000 / 1000:0.0} s");
            if (result.ResultsDiffering > 0)
                Console.WriteLine($"    first order with a different outcome: {result.FirstDifferingRecord}");
            Console.WriteLine(result.Matches ? "    MATCH" : "    DIVERGED");
            return result.Matches;
        }

        /// <summary>
        /// Record a short headless session into <paramref name="root"/> in the layout the game
        /// writes, so the probe can be tried where there is no Unity: a keyframe at tick 1,500 of
        /// the wooded meadow, four trees marked, and 3,000 ticks.
        /// </summary>
        static int WriteSample(string root)
        {
            var request = new ColonyRequest
            {
                Size = new Odyssey.Sim.Contracts.GridSize(60, 60, 16),
                Seed = 7,
                Scenario = ScenarioDef.Playtest(),
                Barren = true,
                Wooded = true,
                Map = MapType.Natural,
                Name = "Sample",
            };
            ColonyWorld colony = ColonyWorld.Build(request);
            colony.World.Tick(1_500);

            string folder = Path.Combine(root, "replay-sample");
            Directory.CreateDirectory(folder);
            colony.SaveToFile(Path.Combine(folder, KeyframeFile), colony.Recipe(1));
            ReplayRecorder recorder = ReplayRecorder.Begin(colony.World);
            recorder.Log.Note = "headless sample, ReplayProbe --sample";

            var size = colony.Grid.Size;
            int marked = 0;
            for (int i = 0; i < colony.Grid.Terrain.Length && marked < 4; i++)
            {
                if (!colony.Designations.IsTree(i)) continue;
                colony.World.Intents.Submit(new Odyssey.Sim.Contracts.Intent(Odyssey.Sim.Contracts.IntentKind.Designate,
                    size.FromIndex(i), (int)Odyssey.Sim.Designations.DesignationKind.Fell));
                marked++;
            }
            colony.World.Tick(3_000);
            recorder.Seal(colony.World, fullHash: true);
            recorder.Detach(colony.World);
            recorder.Log.WriteToFile(Path.Combine(folder, LogFile));
            Console.WriteLine($"Wrote a sample recording to {folder}");
            return 0;
        }

        /// <summary>The fourth copy of this table (see <c>SaveProbe.ScenarioByName</c> for the other three).</summary>
        static ScenarioDef ScenarioByName(string defName) => defName switch
        {
            "Scenario_Bare" => ScenarioDef.Bare(),
            _ => ScenarioDef.Playtest(),
        };

        /// <summary>Walk up from this assembly to the folder holding Assets and ProjectSettings.</summary>
        static string RepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !(Directory.Exists(Path.Combine(dir.FullName, "Assets")) &&
                                    Directory.Exists(Path.Combine(dir.FullName, "ProjectSettings"))))
                dir = dir.Parent;
            return dir?.FullName ?? Directory.GetCurrentDirectory();
        }
    }
}
