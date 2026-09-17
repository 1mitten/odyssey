#nullable enable
using System;
using System.Diagnostics;

namespace Odyssey.Sim.Diagnostics
{
    /// <summary>
    /// Every segment of <see cref="SimWorld.Tick"/>, in the order they run, for timing.
    ///
    /// <para><b>Not a second copy of <see cref="TickPhase"/>, and numbered to match it.</b> That
    /// enum answers "where may a system register?" and deliberately offers only the three phases
    /// where registering one makes sense; this one answers "what did the tick spend its time on?"
    /// and has to name all seven, including the ones no system may join. Where they overlap the
    /// values are identical, and <c>TickBenchmarkTests</c> asserts it, so the two can never come
    /// to disagree about the order of the tick.</para>
    /// </summary>
    public enum TickSegment
    {
        /// <summary>1. Player commands, drained in submission order.</summary>
        Intents = 1,

        /// <summary>2. Grid propagation, support solving, region rebuild.</summary>
        WorldSystems = 2,

        /// <summary>3. Things, by tick group.</summary>
        Things = 3,

        /// <summary>4. Needs, the think tree, job execution and movement — where pathfinding lives.</summary>
        Pawns = 4,

        /// <summary>5. Deferred structural events, applied at one point.</summary>
        Deferred = 5,

        /// <summary>6. Publishing the snapshot the renderer reads.</summary>
        Snapshot = 6,

        /// <summary>7. The state hash, when a hash sink is attached. Near zero in an ordinary run.</summary>
        Hash = 7,
    }

    /// <summary>
    /// Something that wants to know how long each phase of a tick took. Attached to
    /// <see cref="SimWorld.PhaseSink"/>, which is null in every ordinary run.
    ///
    /// <para>The contract is the same one <see cref="ITickHashSink"/> carries, for the same
    /// reason: a sink is handed a number and must not touch the world. A sink that mutated
    /// anything would be changing the thing it is there to measure.</para>
    ///
    /// <para><b>Why this is on <see cref="SimWorld"/> and not assembled by the caller.</b> The
    /// phase order lives in <c>Tick()</c> and nowhere else, and <c>WorldSystemSchedule</c>'s run
    /// methods are internal. A benchmark that timed the phases by calling them itself would be a
    /// second copy of that order — the defect U34 had just finished deleting out of the
    /// composition root. So the measurement goes where the order is, as an opt-in hook beside the
    /// one that was already there.</para>
    ///
    /// <para>Raw <see cref="Stopwatch"/> ticks rather than milliseconds, so the phase boundary
    /// does no floating-point work; converting is the sink's business.</para>
    /// </summary>
    public interface ITickPhaseSink
    {
        void Record(int tick, TickSegment phase, long stopwatchTicks);
    }

    /// <summary>
    /// Per-phase timings for a run, kept as raw samples so percentiles are real rather than
    /// estimated from a running mean.
    ///
    /// <para><b>What it is for.</b> ADR 0005's margin table is per phase, and the only per-phase
    /// numbers it has ever had came from a standalone spike that mirrored the tick rather than
    /// being it. This measures the tick the game actually runs.</para>
    ///
    /// <para><b>Attaching one changes what is measured, a little.</b> Two
    /// <see cref="Stopwatch.GetTimestamp"/> calls per phase are tens of nanoseconds against phases
    /// measured in hundreds of microseconds, but it is not nothing, and it is the reason this is
    /// off by default rather than always on.</para>
    /// </summary>
    public sealed class PhaseTrace : ITickPhaseSink
    {
        // Indexed by the enum value, not by ordinal, because the values are pinned to TickPhase
        // and start at one. One wasted slot is cheaper than a mapping nobody would maintain.
        static readonly int Slots = MaxValue() + 1;

        static int MaxValue()
        {
            int max = 0;
            foreach (TickSegment phase in Enum.GetValues(typeof(TickSegment)))
                if ((int)phase > max) max = (int)phase;
            return max;
        }

        readonly long[][] _samples;
        readonly int[] _counts;

        public PhaseTrace(int capacity = 2048)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _samples = new long[Slots][];
            _counts = new int[Slots];
            for (int i = 0; i < Slots; i++) _samples[i] = new long[capacity];
        }

        /// <summary>Samples recorded for a phase. Every phase gets one per tick.</summary>
        public int Count(TickSegment phase) => _counts[(int)phase];

        public void Record(int tick, TickSegment phase, long stopwatchTicks)
        {
            int i = (int)phase;
            if (_counts[i] == _samples[i].Length) Array.Resize(ref _samples[i], _samples[i].Length * 2);
            _samples[i][_counts[i]++] = stopwatchTicks;
        }

        /// <summary>Throw away everything recorded so far, so warm-up does not enter the figures.</summary>
        public void Clear()
        {
            for (int i = 0; i < Slots; i++) _counts[i] = 0;
        }

        public double MeanMs(TickSegment phase)
        {
            int n = _counts[(int)phase];
            if (n == 0) return 0;

            long total = 0;
            long[] s = _samples[(int)phase];
            for (int i = 0; i < n; i++) total += s[i];
            return ToMs(total) / n;
        }

        /// <summary>
        /// The sample at the 95th percentile, by nearest rank on a sorted copy. A copy because a
        /// trace that sorted its own samples would lose the order a later reader might want.
        /// </summary>
        public double P95Ms(TickSegment phase) => PercentileMs(phase, 0.95);

        public double MaxMs(TickSegment phase)
        {
            int n = _counts[(int)phase];
            if (n == 0) return 0;

            long max = 0;
            long[] s = _samples[(int)phase];
            for (int i = 0; i < n; i++) if (s[i] > max) max = s[i];
            return ToMs(max);
        }

        public double TotalMs(TickSegment phase)
        {
            int n = _counts[(int)phase];
            long total = 0;
            long[] s = _samples[(int)phase];
            for (int i = 0; i < n; i++) total += s[i];
            return ToMs(total);
        }

        /// <summary>Mean of the whole tick: every phase of the same tick added together.</summary>
        public double MeanTickMs()
        {
            double sum = 0;
            foreach (TickSegment phase in Enum.GetValues(typeof(TickSegment))) sum += MeanMs(phase);
            return sum;
        }

        /// <summary>
        /// The 95th percentile of the whole tick, summed per tick before ranking. Adding each
        /// phase's own p95 would overstate it: the slowest world-systems tick and the slowest
        /// pawn tick are rarely the same tick.
        /// </summary>
        public double P95TickMs()
        {
            int n = int.MaxValue;
            foreach (TickSegment phase in Enum.GetValues(typeof(TickSegment)))
                if (_counts[(int)phase] < n) n = _counts[(int)phase];
            if (n == 0 || n == int.MaxValue) return 0;

            var totals = new long[n];
            for (int i = 0; i < n; i++)
            {
                long sum = 0;
                foreach (TickSegment phase in Enum.GetValues(typeof(TickSegment))) sum += _samples[(int)phase][i];
                totals[i] = sum;
            }

            Array.Sort(totals);
            return ToMs(totals[Rank(n, 0.95)]);
        }

        double PercentileMs(TickSegment phase, double fraction)
        {
            int n = _counts[(int)phase];
            if (n == 0) return 0;

            var copy = new long[n];
            Array.Copy(_samples[(int)phase], copy, n);
            Array.Sort(copy);
            return ToMs(copy[Rank(n, fraction)]);
        }

        static int Rank(int n, double fraction)
        {
            int rank = (int)Math.Ceiling(fraction * n) - 1;
            if (rank < 0) rank = 0;
            if (rank >= n) rank = n - 1;
            return rank;
        }

        static double ToMs(long stopwatchTicks) => stopwatchTicks * 1000.0 / Stopwatch.Frequency;
    }
}
