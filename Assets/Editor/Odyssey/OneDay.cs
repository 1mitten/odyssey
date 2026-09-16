#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Pawns;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Odyssey.EditorTools
{
    /// <summary>
    /// Part 4 of the standing milestone gate: a headless one-day run — 60,000 ticks of the play
    /// scene's own world — completing with zero errors and zero exceptions.
    ///
    /// Also the honest tick-cost measurement the budget in ADR 0005 has been waiting for. That
    /// ADR's figure came from a benchmark world; this is five colonists actually hauling, eating
    /// and sleeping on the map the game loads, which is the number that has to fit alongside
    /// rendering in a 16.6 ms frame on a 2022 laptop, after a 3x discount from this machine.
    ///
    /// Run with <c>scripts/unity.sh exec Odyssey.EditorTools.OneDay.Run</c>. Headless, no
    /// graphics device. Exits 1 if anything at all was logged as an error, so CI can gate on it.
    /// Writes <c>Logs/one-day.txt</c> for the milestone report.
    /// </summary>
    public static class OneDay
    {
        public const int TicksPerDay = 60_000;

        [MenuItem("Odyssey/Simulation/Run one day headless")]
        public static void RunFromMenu() => Execute(exitWhenDone: false);

        public static void Run() => Execute(Application.isBatchMode);

        static void Execute(bool exitWhenDone)
        {
            int exitCode = 0;
            int errors = 0;
            var firstErrors = new StringBuilder();

            // Anything the simulation logs as an error is a failure of the run, not a note. Caught
            // here rather than by reading the log afterwards, because a log can be grepped
            // wrongly and an exit code cannot.
            void OnLog(string message, string stack, LogType type)
            {
                if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
                errors++;
                if (errors <= 5) firstErrors.AppendLine($"  [{type}] {message}");
            }

            Application.logMessageReceived += OnLog;
            try
            {
                var size = new GridSize(120, 120, 16);
                var setup = Stopwatch.StartNew();
                ColonyWorld colony = ColonyWorld.Build(size, seed: 1u, ScenarioDef.Bare());
                setup.Stop();

                if (colony.Placement.Colonists != 5)
                    throw new InvalidOperationException($"placement failed: {colony.Placement}");

                // Timed per tick so the report can say what the worst one cost, not only the mean.
                // A mean that fits the budget with a max that does not is a stutter, and a
                // stutter is what the player notices.
                double total = 0d, worst = 0d;
                int over5ms = 0;
                var clock = new Stopwatch();
                for (int tick = 0; tick < TicksPerDay; tick++)
                {
                    clock.Restart();
                    colony.World.Tick();
                    clock.Stop();

                    double ms = clock.Elapsed.TotalMilliseconds;
                    total += ms;
                    if (ms > worst) worst = ms;
                    if (ms > 5d) over5ms++;
                }

                var snapshot = colony.World.Views.Current;
                StateHash hash = colony.World.ComputeStateHash();

                var report = new StringBuilder();
                report.AppendLine($"[OneDay] {size} seed 1, {TicksPerDay} ticks, setup {setup.ElapsedMilliseconds} ms");
                report.AppendLine($"[OneDay] tick mean {total / TicksPerDay:0.000} ms, worst {worst:0.00} ms, " +
                                  $"{over5ms} ticks over 5 ms, total {total / 1000d:0.0} s");
                report.AppendLine($"[OneDay] end: tick {snapshot.Tick}, {snapshot.PawnCount} colonists, " +
                                  $"{snapshot.ThingCount} things, hash {hash}");
                report.AppendLine($"[OneDay] errors logged: {errors}");
                if (errors > 0) report.Append(firstErrors);

                if (snapshot.PawnCount != 5)
                {
                    report.AppendLine("[OneDay] FAIL: a colonist is missing from the snapshot");
                    exitCode = 1;
                }
                if (errors > 0) exitCode = 1;

                report.AppendLine(exitCode == 0 ? "[OneDay] PASS" : "[OneDay] FAIL");

                Directory.CreateDirectory(Path.GetFullPath("Logs"));
                File.WriteAllText(Path.GetFullPath("Logs/one-day.txt"), report.ToString());
                Debug.Log(report.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[OneDay] threw: {e}");
                exitCode = 1;
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                if (exitWhenDone) EditorApplication.Exit(exitCode);
            }
        }
    }
}
