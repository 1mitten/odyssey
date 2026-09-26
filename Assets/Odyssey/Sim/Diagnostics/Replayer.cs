#nullable enable
using System;
using System.Diagnostics;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Diagnostics
{
    /// <summary>What a replay found. Every hash it could compare, and where the first one parted.</summary>
    public sealed class ReplayResult
    {
        /// <summary>The loaded keyframe hashed the same as the live world did when it was taken.</summary>
        public bool? StartMatches { get; internal set; }

        public int CheckpointsCompared { get; internal set; }
        public int CheckpointsMatched { get; internal set; }

        /// <summary>The first checkpoint tick that did not match, or -1.</summary>
        public int FirstDivergentTick { get; internal set; } = -1;

        /// <summary>The last checkpoint tick that did match, or -1. The divergence lies between the two.</summary>
        public int LastMatchingTick { get; internal set; } = -1;

        public bool? EndLightMatches { get; internal set; }
        public bool? EndFullMatches { get; internal set; }

        public int TicksRun { get; internal set; }
        public int RecordsApplied { get; internal set; }

        /// <summary>Records whose outcome (applied or refused, and why) differed from the live one.</summary>
        public int ResultsDiffering { get; internal set; }

        /// <summary>The first record whose outcome differed, for the report.</summary>
        public string FirstDifferingRecord { get; internal set; } = string.Empty;

        public double Milliseconds { get; internal set; }

        public double MillisecondsPerTick => TicksRun > 0 ? Milliseconds / TicksRun : 0d;

        /// <summary>Every comparison that could be made came out equal, and at least one was made.</summary>
        public bool Matches =>
            StartMatches != false && EndLightMatches != false && EndFullMatches != false &&
            FirstDivergentTick < 0 && ResultsDiffering == 0 &&
            (CheckpointsCompared > 0 || EndLightMatches == true || EndFullMatches == true);

        public override string ToString() =>
            $"start {Say(StartMatches)}, checkpoints {CheckpointsMatched}/{CheckpointsCompared}" +
            (FirstDivergentTick >= 0 ? $" (first parted at tick {FirstDivergentTick}, last agreed at {LastMatchingTick})" : "") +
            $", end light {Say(EndLightMatches)}, end full {Say(EndFullMatches)}, " +
            $"records {RecordsApplied} ({ResultsDiffering} with a different outcome), " +
            $"{TicksRun} ticks in {Milliseconds:0} ms = {MillisecondsPerTick:0.000} ms/tick";

        static string Say(bool? b) => b == null ? "not taken" : b.Value ? "match" : "DIFFER";
    }

    /// <summary>
    /// Run a world that stands at a keyframe forward through a <see cref="ReplayLog"/>, applying
    /// each recorded order exactly where the live game applied it, and compare the hashes it
    /// passes through with the ones the live colony passed through (design 63 §3a, §4).
    ///
    /// <para><b>The order of one tick, as the live game did it.</b> An order given while the clock
    /// was stopped at tick T was applied by <see cref="SimWorld.RepublishViews"/> before T ran;
    /// every other order at T was applied by T's own drain. So: submit T's paused records and
    /// republish, then submit the rest and tick. Records are submitted in the order they were
    /// applied, and the bus drains in submission order.</para>
    ///
    /// <para><b>Nothing here may touch the world except through the intent bus and
    /// <see cref="SimWorld.Tick"/></b> — the same rule the live game is held to, and the one HR0
    /// audits.</para>
    /// </summary>
    public static class Replayer
    {
        /// <summary>
        /// Replay the whole log. The world must have just been loaded from the keyframe.
        /// <paramref name="fullHashAtEnd"/> costs one full hash.
        /// </summary>
        public static ReplayResult Run(SimWorld world, ReplayLog log, bool fullHashAtEnd = true)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (log == null) throw new ArgumentNullException(nameof(log));
            if (world.CurrentTick != log.FromTick)
                throw new InvalidOperationException(
                    $"the world stands at tick {world.CurrentTick} and the log starts at {log.FromTick}");
            if (world.Seed != log.Seed || !world.Size.Equals(log.Size))
                throw new InvalidOperationException("the world is not the one the log was recorded on");

            var result = new ReplayResult();
            if (log.StartFullHash != 0)
                result.StartMatches = world.ComputeStateHash().Value == log.StartFullHash;

            // The replay must not record itself into whatever might be listening.
            IReplaySink? outer = world.ReplaySink;
            world.ReplaySink = null;

            // The view the live world stood on at the keyframe, put back before anything runs and
            // before the checker listens: it was in force live, so it is not an order to compare.
            foreach (var intent in log.Preamble) world.Intents.Submit(intent);
            world.ApplyPendingWithoutTicking();

            var check = new Checker(log, result);
            world.ReplaySink = check;

            var clock = Stopwatch.StartNew();
            try
            {
                var records = log.Records;
                int next = 0;
                for (int tick = log.FromTick; tick < log.EndTick; tick++)
                {
                    bool paused = false;
                    while (next < records.Count && records[next].Tick == tick && records[next].Paused)
                    {
                        check.Expect(records[next]);
                        world.Intents.Submit(records[next].Intent);
                        next++;
                        paused = true;
                    }
                    if (paused) world.RepublishViews();

                    while (next < records.Count && records[next].Tick == tick)
                    {
                        check.Expect(records[next]);
                        world.Intents.Submit(records[next].Intent);
                        next++;
                    }

                    world.Tick();
                    result.TicksRun++;
                }

                // A paused order at the end tick itself: given after the last tick, before the stop.
                bool trailing = false;
                while (next < records.Count && records[next].Tick == log.EndTick && records[next].Paused)
                {
                    check.Expect(records[next]);
                    world.Intents.Submit(records[next].Intent);
                    next++;
                    trailing = true;
                }
                if (trailing) world.RepublishViews();
            }
            finally
            {
                clock.Stop();
                world.ReplaySink = outer;
            }
            result.Milliseconds = clock.Elapsed.TotalMilliseconds;

            if (log.EndLightHash != 0)
                result.EndLightMatches = world.ComputeLightHash().Value == log.EndLightHash;
            if (fullHashAtEnd && log.EndFullHash != 0)
                result.EndFullMatches = world.ComputeStateHash().Value == log.EndFullHash;
            return result;
        }

        /// <summary>
        /// Stands where the recorder stood: hears every applied intent and every tick end, and
        /// compares instead of writing down.
        /// </summary>
        sealed class Checker : IReplaySink
        {
            readonly ReplayLog _log;
            readonly ReplayResult _result;
            readonly System.Collections.Generic.Queue<ReplayRecord> _expected =
                new System.Collections.Generic.Queue<ReplayRecord>();
            int _checkpoint;

            public Checker(ReplayLog log, ReplayResult result)
            {
                _log = log;
                _result = result;
                // Checkpoints before the start cannot be compared and are skipped.
                while (_checkpoint < log.Checkpoints.Count && log.Checkpoints[_checkpoint].Tick < log.FromTick)
                    _checkpoint++;
            }

            /// <summary>
            /// A record about to be submitted. Several can wait in the bus before a drain, and the
            /// bus applies them in submission order, so they are compared in that order.
            /// </summary>
            public void Expect(in ReplayRecord record) => _expected.Enqueue(record);

            public void Applied(int tick, bool paused, in Intent intent, IntentRejection result)
            {
                _result.RecordsApplied++;
                if (_expected.Count == 0)
                {
                    Differ($"an intent nobody submitted was applied: {intent} at {tick}");
                    return;
                }

                ReplayRecord want = _expected.Dequeue();
                if (want.Tick != tick || want.Paused != paused || want.Result != result)
                    Differ($"{want} but replayed as @{tick}{(paused ? " paused" : "")} -> {result}");
            }

            void Differ(string what)
            {
                if (_result.ResultsDiffering == 0) _result.FirstDifferingRecord = what;
                _result.ResultsDiffering++;
            }

            public void TickEnded(SimWorld world, int tick)
            {
                var points = _log.Checkpoints;
                if (_checkpoint >= points.Count || points[_checkpoint].Tick != tick) return;

                _result.CheckpointsCompared++;
                if (world.ComputeLightHash().Value == points[_checkpoint].LightHash)
                {
                    _result.CheckpointsMatched++;
                    if (_result.FirstDivergentTick < 0) _result.LastMatchingTick = tick;
                }
                else if (_result.FirstDivergentTick < 0)
                {
                    _result.FirstDivergentTick = tick;
                }
                _checkpoint++;
            }
        }
    }
}
